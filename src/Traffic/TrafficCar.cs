using System;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// A sparse, authored, road-bound village driver. Vehicles use a scripted
/// waypoint loop with capped speed and turn rate, slow down for the player/van
/// and never collide with gameplay bodies (layer 0). There are no random
/// navigation calls and no visible teleport.
/// </summary>
public partial class TrafficCar : CharacterBody3D
{
    public string RouteId { get; private set; } = "";
    public string DriverId { get; private set; } = "";
    public float PlanarSpeed { get; private set; }
    public bool Yielding { get; private set; }
    public bool HasEngineAudio => _engineIdle != null && _engineDrive != null;
    public int CurrentWaypoint { get; private set; }
    public float DistanceTravelled { get; private set; }
    public int YieldEvents { get; private set; }
    public bool RallyOnly { get; private set; }

    private TrafficRouteDefinition _route;
    private Func<Vector3> _playerPosition;
    private Func<Vector3> _vanPosition;
    private Func<bool> _playerDriving;
    private Node3D _visual;
    private readonly System.Collections.Generic.List<Node3D> _wheels = new();
    private float _yaw;
    private float _speed;
    private float _wheelAngle;
    private float _hornCooldown;
    private float _yieldTime;
    private AudioStreamPlayer3D _engineIdle;
    private AudioStreamPlayer3D _engineDrive;

    public void Initialize(TrafficRouteDefinition route, Func<Vector3> playerPosition,
        Func<Vector3> vanPosition, Func<bool> playerDriving)
    {
        _route = route ?? throw new ArgumentNullException(nameof(route));
        if (_route.Points.Count < 4)
            throw new ArgumentException("A traffic route needs at least four points.", nameof(route));
        _playerPosition = playerPosition;
        _vanPosition = vanPosition;
        _playerDriving = playerDriving;
        RouteId = route.Id;
        DriverId = route.DriverId;
        RallyOnly = route.RallyOnly;
        Name = $"Traffic_{route.Id}";
        Position = new Vector3(route.Points[0].X, 0.08f, route.Points[0].Z);
        CollisionLayer = 0; // never blocks the player, van or mission stations
        CollisionMask = 1;  // world geometry only
        FloorSnapLength = 0.5f;
        FloorMaxAngle = Mathf.DegToRad(42f);
        SafeMargin = 0.04f;
        UpDirection = Vector3.Up;
        ProcessMode = ProcessModeEnum.Pausable;
        CurrentWaypoint = 1 % route.Points.Count;
        SetYawToward(route.Points[CurrentWaypoint]);
    }

    public override void _Ready()
    {
        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D
            {
                Size = _route.VehicleKind == "scooter"
                    ? new Vector3(0.9f, 1.35f, 1.8f)
                    : new Vector3(1.65f, 1.35f, 2.75f),
            },
            Position = new Vector3(0, 0.72f, 0),
        });
        BuildVisual();
        _engineIdle = AudioManager.CreateVehicleLoop(this, "engine_idle_loop", 7f, 38f, -27f);
        _engineDrive = AudioManager.CreateVehicleLoop(this, "engine_drive_loop", 7f, 38f, -40f);
        _wheels.Clear();
        foreach (var child in _visual.GetChildren())
            if (child is Node3D node && node.Name.ToString().StartsWith("Wheel", StringComparison.Ordinal))
                _wheels.Add(node);
    }

    private void BuildVisual()
    {
        _visual = new Node3D { Name = "TrafficVisual" };
        AddChild(_visual);
        Color main = Color.FromHtml(_route.ColourHex);
        Color dark = main.Darkened(0.22f);
        Color glass = Color.FromHtml("9BD8F2");
        Color tyre = Color.FromHtml("343A46");
        if (_route.VehicleKind == "scooter")
            BuildScooter(main, dark, glass, tyre);
        else if (_route.VehicleKind == "rally_car")
            BuildRallyCar(main, dark, glass, tyre);
        else
            BuildCar(main, dark, glass, tyre);
    }

    private void BuildCar(Color main, Color dark, Color glass, Color tyre)
    {
        Geo.Box(_visual, new Vector3(0, 0.58f, 0), new Vector3(1.6f, 0.5f, 2.65f), main);
        Geo.Box(_visual, new Vector3(0, 1.02f, 0.18f), new Vector3(1.36f, 0.58f, 1.35f), main);
        Geo.Box(_visual, new Vector3(0, 1.08f, -0.52f), new Vector3(1.28f, 0.48f, 0.06f), glass);
        Geo.Box(_visual, new Vector3(0, 1.08f, 0.9f), new Vector3(1.2f, 0.48f, 0.06f), glass);
        Geo.Box(_visual, new Vector3(0, 1.35f, 0.2f), new Vector3(1.3f, 0.12f, 1.1f), dark);
        Geo.Box(_visual, new Vector3(0, 0.62f, -1.36f), new Vector3(1.66f, 0.18f, 0.12f), dark);
        Geo.Box(_visual, new Vector3(0, 0.62f, 1.36f), new Vector3(1.66f, 0.18f, 0.12f), dark);
        Geo.Box(_visual, new Vector3(-0.52f, 0.68f, -1.37f), new Vector3(0.34f, 0.22f, 0.06f),
            Color.FromHtml("FFF3C9"));
        Geo.Box(_visual, new Vector3(0.52f, 0.68f, -1.37f), new Vector3(0.34f, 0.22f, 0.06f),
            Color.FromHtml("FFF3C9"));
        Geo.Box(_visual, new Vector3(-0.52f, 0.68f, 1.37f), new Vector3(0.28f, 0.2f, 0.06f),
            Palette.Red);
        Geo.Box(_visual, new Vector3(0.52f, 0.68f, 1.37f), new Vector3(0.28f, 0.2f, 0.06f),
            Palette.Red);
        AddWheel(new Vector3(-0.79f, 0.35f, -0.82f), tyre);
        AddWheel(new Vector3(0.79f, 0.35f, -0.82f), tyre);
        AddWheel(new Vector3(-0.79f, 0.35f, 0.82f), tyre);
        AddWheel(new Vector3(0.79f, 0.35f, 0.82f), tyre);
        AddDriverSilhouette(new Vector3(-0.34f, 1.25f, 0.12f));
        Geo.Label(_visual, new Vector3(0, 1.15f, 1.22f), "VILLAGE", 30, Palette.Navy,
            false, 0.003f, 0, null, "CarLabel");
    }

    private void BuildRallyCar(Color main, Color dark, Color glass, Color tyre)
    {
        Geo.Box(_visual, new Vector3(0, 0.48f, 0), new Vector3(1.58f, 0.42f, 2.6f), main);
        Geo.Box(_visual, new Vector3(0, 0.88f, 0.12f), new Vector3(1.28f, 0.42f, 1.25f), main);
        Geo.Box(_visual, new Vector3(0, 0.94f, -0.55f), new Vector3(1.18f, 0.34f, 0.06f), glass);
        Geo.Box(_visual, new Vector3(0, 1.22f, 1.15f), new Vector3(1.9f, 0.12f, 0.55f), dark);
        Geo.Box(_visual, new Vector3(-0.55f, 1.32f, 1.32f), new Vector3(0.1f, 0.35f, 0.12f), dark);
        Geo.Box(_visual, new Vector3(0.55f, 1.32f, 1.32f), new Vector3(0.1f, 0.35f, 0.12f), dark);
        Geo.Box(_visual, new Vector3(0, 0.55f, -1.31f), new Vector3(1.64f, 0.18f, 0.1f), dark);
        Geo.Box(_visual, new Vector3(0, 0.72f, -1.37f), new Vector3(1.3f, 0.28f, 0.04f), Palette.Yellow);
        Geo.Label(_visual, new Vector3(0, 0.72f, -1.4f), "6", 72, Palette.Navy, false, 0.0025f);
        AddWheel(new Vector3(-0.78f, 0.33f, -0.8f), tyre);
        AddWheel(new Vector3(0.78f, 0.33f, -0.8f), tyre);
        AddWheel(new Vector3(-0.78f, 0.33f, 0.8f), tyre);
        AddWheel(new Vector3(0.78f, 0.33f, 0.8f), tyre);
        AddDriverSilhouette(new Vector3(-0.34f, 1.12f, 0.1f));
    }

    private void BuildScooter(Color main, Color dark, Color glass, Color tyre)
    {
        Geo.Box(_visual, new Vector3(0, 0.42f, 0), new Vector3(0.62f, 0.16f, 1.35f), main);
        Geo.Box(_visual, new Vector3(0, 0.68f, 0.36f), new Vector3(0.52f, 0.28f, 0.72f), dark);
        Geo.Box(_visual, new Vector3(0, 0.96f, -0.52f), new Vector3(0.16f, 0.8f, 0.16f), main,
            new Vector3(-12, 0, 0));
        Geo.Box(_visual, new Vector3(0, 1.3f, -0.6f), new Vector3(0.78f, 0.1f, 0.1f), Palette.Navy);
        Geo.Box(_visual, new Vector3(0, 0.78f, 0.72f), new Vector3(0.58f, 0.44f, 0.55f), main);
        Geo.Box(_visual, new Vector3(0, 0.95f, 0.98f), new Vector3(0.5f, 0.3f, 0.06f), Palette.Red);
        Geo.Box(_visual, new Vector3(0, 1.33f, -0.66f), new Vector3(0.22f, 0.18f, 0.08f),
            Color.FromHtml("FFF3C9"));
        Geo.Label(_visual, new Vector3(0, 1.05f, 0.72f), "POST", 26, Palette.Navy, false, 0.0025f);
        // Rider silhouette so the scooter clearly has a driver.
        Geo.Box(_visual, new Vector3(0, 1.38f, 0.2f), new Vector3(0.44f, 0.62f, 0.36f), main);
        Geo.Box(_visual, new Vector3(0, 1.78f, 0.14f), new Vector3(0.38f, 0.36f, 0.34f), Palette.Skin);
        Geo.Box(_visual, new Vector3(0, 1.98f, 0.14f), new Vector3(0.4f, 0.12f, 0.36f),
            Color.FromHtml("#664b39"));
        AddWheel(new Vector3(0, 0.34f, -0.72f), tyre);
        AddWheel(new Vector3(0, 0.34f, 0.7f), tyre);
    }

    private void AddWheel(Vector3 position, Color tyre)
    {
        var pivot = new Node3D { Name = $"Wheel_{_wheels.Count}", Position = position };
        _visual.AddChild(pivot);
        Geo.Cylinder(pivot, Vector3.Zero, 0.32f, 0.32f, 0.22f, tyre, 12,
            new Vector3(0, 0, 90));
        Geo.Cylinder(pivot, new Vector3(0.12f, 0, 0), 0.13f, 0.13f, 0.04f,
            Color.FromHtml("AEB6C2"), 10, new Vector3(0, 0, 90));
        _wheels.Add(pivot);
    }

    private void AddDriverSilhouette(Vector3 position)
    {
        NpcDefinition definition = NpcCatalog.Find(DriverId) ?? NpcCatalog.All[0];
        Geo.Box(_visual, position, new Vector3(0.4f, 0.52f, 0.34f),
            Color.FromHtml(definition.MainHex));
        Geo.Box(_visual, position + new Vector3(0, 0.34f, -0.04f), new Vector3(0.34f, 0.32f, 0.3f),
            Palette.Skin);
        Geo.Box(_visual, position + new Vector3(0, 0.52f, -0.04f), new Vector3(0.36f, 0.1f, 0.32f),
            Color.FromHtml(definition.HairHex));
    }

    private void SetYawToward(NpcPoint target)
    {
        float dx = target.X - Position.X;
        float dz = target.Z - Position.Z;
        if (Mathf.Abs(dx) + Mathf.Abs(dz) < 0.001f) return;
        _yaw = Mathf.Atan2(-dx, -dz);
        Rotation = new Vector3(0, _yaw, 0);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_route == null) return;
        _hornCooldown = Mathf.Max(0f, _hornCooldown - dt);

        var target = _route.Points[CurrentWaypoint];
        Vector3 targetPos = new(target.X, GlobalPosition.Y, target.Z);
        Vector3 to = targetPos - GlobalPosition;
        to.Y = 0;
        float distance = to.Length();
        if (distance < 0.85f)
        {
            CurrentWaypoint = (CurrentWaypoint + 1) % _route.Points.Count;
            target = _route.Points[CurrentWaypoint];
            targetPos = new Vector3(target.X, GlobalPosition.Y, target.Z);
            to = targetPos - GlobalPosition;
            to.Y = 0;
            distance = to.Length();
        }

        Vector3 direction = distance > 0.001f ? to / distance : -GlobalTransform.Basis.Z;
        float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
        float angle = Mathf.Abs(Mathf.Wrap(targetYaw - _yaw, -Mathf.Pi, Mathf.Pi));
        float turnRate = 1.25f; // radians per second
        _yaw = Mathf.MoveToward(_yaw, _yaw + Mathf.Wrap(targetYaw - _yaw, -Mathf.Pi, Mathf.Pi),
            turnRate * dt);
        Rotation = new Vector3(0, _yaw, 0);

        bool yield = ShouldYield();
        if (yield && !Yielding)
        {
            YieldEvents++;
            if (_hornCooldown <= 0f)
            {
                AudioManager.Instance?.PlayAt("npc_horn", GlobalPosition + Vector3.Up * 1.2f, -18f);
                _hornCooldown = 7f;
            }
        }
        Yielding = yield;
        float targetSpeed = yield ? 0f : _route.SpeedMetresPerSecond;
        if (angle > 0.95f) targetSpeed *= 0.18f;
        else if (angle > 0.5f) targetSpeed *= 0.5f;
        float accel = targetSpeed < _speed ? 7.5f : 2.8f;
        _speed = Mathf.MoveToward(_speed, targetSpeed, accel * dt);
        if (yield) _yieldTime += dt; else _yieldTime = 0f;

        Vector3 forward = -Basis.Z;
        Vector3 velocity = forward * _speed;
        velocity.Y = IsOnFloor() ? -0.3f : Velocity.Y - 23f * dt;
        Velocity = velocity;
        Vector3 before = GlobalPosition;
        MoveAndSlide();
        DistanceTravelled += new Vector2(GlobalPosition.X - before.X, GlobalPosition.Z - before.Z).Length();
        PlanarSpeed = new Vector2(Velocity.X, Velocity.Z).Length();

        _wheelAngle -= _speed * dt / 0.32f;
        foreach (var wheel in _wheels)
            wheel.Rotation = new Vector3(_wheelAngle, 0, 0);

        UpdateEngineAudio();
        if (GlobalPosition.Y < -2f)
        {
            // Never teleport into view. Stop the route instead of popping.
            _speed = 0f;
            Velocity = Vector3.Zero;
            GD.PushError($"Traffic '{RouteId}' left the island at {GlobalPosition}; route stopped.");
        }
    }

    private void UpdateEngineAudio()
    {
        float ratio = Mathf.Clamp(PlanarSpeed / Mathf.Max(0.1f, _route.SpeedMetresPerSecond), 0f, 1f);
        if (_engineIdle != null)
        {
            if (!_engineIdle.Playing) _engineIdle.Play();
            _engineIdle.VolumeDb = Mathf.Lerp(-34f, -27f, ratio);
        }
        if (_engineDrive != null)
        {
            if (ratio > 0.03f)
            {
                if (!_engineDrive.Playing) _engineDrive.Play();
                _engineDrive.VolumeDb = Mathf.Lerp(-38f, -19f, ratio);
                _engineDrive.PitchScale = 0.8f + ratio * 0.5f;
            }
            else
            {
                _engineDrive.VolumeDb = -40f;
            }
        }
    }

    private bool ShouldYield()
    {
        Vector3 forward = -Basis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        Vector3 player = _playerPosition?.Invoke() ?? Vector3.Zero;
        Vector3 toPlayer = player - GlobalPosition;
        toPlayer.Y = 0;
        float playerDistance = toPlayer.Length();
        if (playerDistance < 2.8f) return true;
        if (playerDistance < 5.2f && toPlayer.Normalized().Dot(forward) > 0.15f) return true;
        if (_playerDriving?.Invoke() == true)
        {
            Vector3 van = _vanPosition?.Invoke() ?? Vector3.Zero;
            Vector3 toVan = van - GlobalPosition;
            toVan.Y = 0;
            float vanDistance = toVan.Length();
            if (vanDistance < 4.2f) return true;
            if (vanDistance < 7.5f && toVan.Normalized().Dot(forward) > 0.1f) return true;
        }
        return false;
    }

    public void StopAtRouteEnd()
    {
        _speed = 0f;
        Velocity = Vector3.Zero;
    }
}
