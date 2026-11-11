using System;
using System.Collections.Generic;
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
    public bool Stopped { get; private set; }
    public int StopEvents { get; private set; }

    private TrafficRouteDefinition _route;
    private IReadOnlyList<NpcPoint> _points = Array.Empty<NpcPoint>();
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

    public void Initialize(TrafficRouteDefinition route,
        IReadOnlyList<NpcPoint> points, Func<Vector3> playerPosition,
        Func<Vector3> vanPosition, Func<bool> playerDriving)
    {
        _route = route ?? throw new ArgumentNullException(nameof(route));
        _points = points ?? route.Points;
        if (_points.Count < 4)
            throw new ArgumentException("A traffic route needs at least four points.", nameof(route));
        _playerPosition = playerPosition;
        _vanPosition = vanPosition;
        _playerDriving = playerDriving;
        RouteId = route.Id;
        DriverId = route.DriverId;
        RallyOnly = route.RallyOnly;
        Name = $"Traffic_{route.Id}";
        Position = new Vector3(_points[0].X, 0.08f, _points[0].Z);
        CollisionLayer = 0; // never blocks the player, van or mission stations
        CollisionMask = 1;  // world geometry only
        FloorSnapLength = 0.5f;
        FloorMaxAngle = Mathf.DegToRad(42f);
        SafeMargin = 0.04f;
        UpDirection = Vector3.Up;
        ProcessMode = ProcessModeEnum.Pausable;
        CurrentWaypoint = 1 % _points.Count;
        SetYawToward(_points[CurrentWaypoint]);
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
    }

    public string VisualTemplatePath { get; private set; } = "";

    private void BuildVisual()
    {
        _visual = new Node3D { Name = "TrafficVisual" };
        AddChild(_visual);
        if (!VisualTemplate.TrafficPaths.TryGetValue(RouteId, out string templatePath))
            templatePath = VisualTemplate.TrafficPaths["town_scooter"];
        Node3D template = VisualTemplate.Instantiate(_visual, "Visual", templatePath);
        VisualTemplatePath = templatePath;
        VisualTemplate.ApplyVertexColours(template, VisualTemplate.VertexColourMaterial());
        _wheels.Clear();
        _wheels.AddRange(VisualTemplate.BuildWheelPivots(template));

        // Same missing-normal self-shadow fix as the player van.
        VisualTemplate.DisableSelfShadow(template);
        bool scooter = _route.VehicleKind == "scooter";
        var groundShadow = Geo.Disc(this, new Vector3(0, 0.015f, 0), 1.0f,
            new Color(0.06f, 0.10f, 0.16f), 0.15f);
        groundShadow.Name = "GroundShadow";
        groundShadow.Scale = scooter
            ? new Vector3(0.62f, 1f, 0.95f)
            : new Vector3(1.05f, 1f, 1.55f);
        groundShadow.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
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
        if (_route == null || Stopped) return;
        _hornCooldown = Mathf.Max(0f, _hornCooldown - dt);

        var target = _points[CurrentWaypoint];
        Vector3 targetPos = new(target.X, GlobalPosition.Y, target.Z);
        Vector3 to = targetPos - GlobalPosition;
        to.Y = 0;
        float distance = to.Length();
        if (distance < 0.85f)
        {
            CurrentWaypoint = (CurrentWaypoint + 1) % _points.Count;
            target = _points[CurrentWaypoint];
            targetPos = new Vector3(target.X, GlobalPosition.Y, target.Z);
            to = targetPos - GlobalPosition;
            to.Y = 0;
            distance = to.Length();
        }

        bool yield = ShouldYield();
        float angle = 0f;
        if (!yield)
        {
            Vector3 direction = distance > 0.001f ? to / distance : -GlobalTransform.Basis.Z;
            float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
            angle = Mathf.Abs(Mathf.Wrap(targetYaw - _yaw, -Mathf.Pi, Mathf.Pi));
            float turnRate = 1.25f; // radians per second
            _yaw = Mathf.MoveToward(_yaw, _yaw + Mathf.Wrap(targetYaw - _yaw, -Mathf.Pi, Mathf.Pi),
                turnRate * dt);
            Rotation = new Vector3(0, _yaw, 0);
        }

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
            // Never teleport into view. Stop the route permanently instead of
            // re-entering movement every physics frame, which used to emit the
            // same error repeatedly.
            Stopped = true;
            StopEvents++;
            _speed = 0f;
            PlanarSpeed = 0f;
            Velocity = Vector3.Zero;
            Yielding = false;
            _engineIdle?.Stop();
            _engineDrive?.Stop();
            SetPhysicsProcess(false);
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
