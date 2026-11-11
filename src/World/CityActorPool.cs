using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

public enum CityActorTier
{
    Mid,
    Near,
}

/// <summary>
/// Visual vehicle for one logical city agent. Only near vehicles use physics
/// and wheel animation; mid vehicles are a one-mesh impostor updated at 4 Hz.
/// The actor never exists for a far cell, so no hidden Node3D is transformed or
/// simulated offscreen.
/// </summary>
public partial class CityVehicleVisual : CharacterBody3D
{
    public int AgentId { get; private set; }
    public CityActorTier Tier { get; private set; } = CityActorTier.Mid;
    public float Progress { get; private set; }
    public float Speed { get; private set; }
    public bool Yielding { get; private set; }
    public float ReconciliationLag { get; private set; }
    public float RouteLength => _length;

    private CityRoutePoint[] _route;
    private float[] _cumulative;
    private float _length;
    private float _yaw;
    private float _wheelAngle;
    private bool _testHold;
    private Node3D _full;
    private Node3D _proxy;
    private readonly List<Node3D> _wheels = new();
    private CollisionShape3D _shape;
    private Func<Vector3> _playerPosition;
    private Func<bool> _playerDriving;
    private Func<Vector3> _vanPosition;

    public void Initialize(CityAgentState agent, CityRoutePoint[] route,
        Func<Vector3> playerPosition, Func<bool> playerDriving, Func<Vector3> vanPosition)
    {
        AgentId = agent.Id;
        _route = route;
        _cumulative = BuildCumulative(route);
        _length = _cumulative[^1];
        Progress = agent.Phase;
        Speed = agent.Speed;
        _playerPosition = playerPosition;
        _playerDriving = playerDriving;
        _vanPosition = vanPosition;
        Name = $"CityVehicle_{agent.Id}";
        // City traffic is cooperative/decorative: cars collide with static
        // geometry but deliberately not with the player or van. ShouldYield
        // keeps them out of the player's path without letting traffic trap a
        // young player in an intersection.
        CollisionLayer = 0;
        CollisionMask = 1;
        FloorSnapLength = 0.6f;
        FloorMaxAngle = Mathf.DegToRad(42f);
        SafeMargin = 0.04f;
        UpDirection = Vector3.Up;
        ProcessMode = ProcessModeEnum.Pausable;

        _shape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.7f, 1.35f, 2.8f) },
            Position = new Vector3(0, 0.72f, 0),
            Disabled = true,
        };
        AddChild(_shape);
        BuildVisual(agent);
        SetPoseFromProgress(0.35f);
        SetTier(CityActorTier.Mid);
    }

    private void BuildVisual(CityAgentState agent)
    {
        _proxy = new Node3D { Name = "MidProxy" };
        AddChild(_proxy);
        Color colour = CityPalette.CarColour(agent.Colour);
        var body = Geo.Box(_proxy, new Vector3(0, 0.55f, 0), new Vector3(1.65f, 0.9f, 2.6f), colour);
        body.Name = "ProxyBody";
        Geo.Box(_proxy, new Vector3(0, 1.12f, -0.2f), new Vector3(1.35f, 0.52f, 1.3f),
            colour.Darkened(0.18f), null, false, "ProxyCabin");
        _proxy.Visible = true;

        _full = new Node3D { Name = "Detailed" };
        AddChild(_full);
        string path = agent.Variant == 2
            ? "res://scenes/world/assets/vehicles/RallyCar_Visual.tscn"
            : agent.Variant == 1
                ? "res://scenes/world/assets/vehicles/TownCar_Blue_Visual.tscn"
                : "res://scenes/world/assets/vehicles/TownCar_Green_Visual.tscn";
        Node3D template = VisualTemplate.Instantiate(_full, "Visual", path);
        VisualTemplate.ApplyVertexColours(template, VisualTemplate.VertexColourMaterial());
        VisualTemplate.DisableSelfShadow(template);
        _wheels.Clear();
        _wheels.AddRange(VisualTemplate.BuildWheelPivots(template));
        var shadow = Geo.Disc(_full, new Vector3(0, 0.015f, 0), 1f,
            new Color(0.06f, 0.10f, 0.16f), 0.15f);
        shadow.Name = "GroundShadow";
        shadow.Scale = new Vector3(1.05f, 1f, 1.55f);
        shadow.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _full.Visible = false;
    }

    public void SetTier(CityActorTier tier)
    {
        Tier = tier;
        bool near = tier == CityActorTier.Near;
        if (_full != null) _full.Visible = near;
        if (_proxy != null) _proxy.Visible = !near;
        CollisionLayer = near ? 4u : 0u;
        if (_shape != null) _shape.Disabled = !near;
        SetPhysicsProcess(near);
    }

    public void SpawnAt(CityAgentPose pose, CityActorTier tier, float lagMeters = 0f)
    {
        ReconciliationLag = Mathf.Clamp(lagMeters, -CityActorPool.MaxVisualLagMeters, CityActorPool.MaxVisualLagMeters);
        Progress = Wrap(pose.RouteProgress - ReconciliationLag, _length);
        _yaw = pose.Yaw;
        SetPoseFromProgress(0.35f);
        SetTier(tier);
    }

    public void SetReconciliationLag(float lagMeters) => ReconciliationLag = lagMeters;

    public void SnapToProgress(float progress)
    {
        Progress = Wrap(progress, _length);
        SetPoseFromProgress(0.35f);
    }

    public void StepMid(float delta)
    {
        if (Tier != CityActorTier.Mid) return;
        Progress = Wrap(Progress + Speed * delta, _length);
        SetPoseFromProgress(0.35f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Tier != CityActorTier.Near || _route == null) return;
        float dt = (float)delta;
        Yielding = ShouldYield();
        float correction = Mathf.Clamp(ReconciliationLag / 4f, -2f, 2.5f);
        float targetSpeed = Yielding ? 0f : Mathf.Max(0f, Speed + correction);
        targetSpeed *= CornerFactor();
        float current = new Vector2(Velocity.X, Velocity.Z).Length();
        current = Mathf.MoveToward(current, targetSpeed, (Yielding ? 10f : 3.4f) * dt);
        Progress = Wrap(Progress + current * dt, _length);
        CityRoutePoint target = PointAt(Wrap(Progress + 4f, _length));
        float desiredYaw = Mathf.Atan2(-(target.X - GlobalPosition.X), -(target.Z - GlobalPosition.Z));
        _yaw = Mathf.LerpAngle(_yaw, desiredYaw, 1f - Mathf.Exp(-dt * 4f));
        Rotation = new Vector3(0, _yaw, 0);
        Vector3 forward = -GlobalTransform.Basis.Z;
        Vector3 velocity = forward * current;
        velocity.Y = IsOnFloor() ? -0.3f : Velocity.Y - 23f * dt;
        Velocity = velocity;
        MoveAndSlide();
        _wheelAngle -= current * dt / 0.34f;
        foreach (var wheel in _wheels)
            wheel.Rotation = new Vector3(_wheelAngle, 0, 0);
        if (GlobalPosition.Y < -1.5f)
        {
            Progress = Wrap(Progress + 4f, _length);
            SetPoseFromProgress(0.35f);
        }
    }

    private float CornerFactor()
    {
        if (_route == null) return 1f;
        float length = _length;
        foreach (float corner in CornerProgresses())
        {
            float gap = Wrap(corner - Progress, length);
            if (gap is > 0.1f and < 11f) return 0.42f;
        }
        return 1f;
    }

    private IEnumerable<float> CornerProgresses()
    {
        for (int i = 0; i < _route.Length; i++)
            yield return _cumulative[i];
    }

    public static bool ShouldYieldTo(Vector3 carPosition, Vector3 forward,
        Vector3 playerPosition, bool playerDriving, Vector3 vanPosition)
    {
        forward.Y = 0;
        if (forward.LengthSquared() > 0.0001f) forward = forward.Normalized();
        Vector3 toPlayer = playerPosition - carPosition;
        toPlayer.Y = 0;
        float playerDistance = toPlayer.Length();
        if (playerDistance < 3.4f ||
            (playerDistance < 7f && toPlayer.Normalized().Dot(forward) > 0.1f))
            return true;
        if (playerDriving)
        {
            Vector3 toVan = vanPosition - carPosition;
            toVan.Y = 0;
            float vanDistance = toVan.Length();
            if (vanDistance < 5.2f ||
                (vanDistance < 9f && toVan.Normalized().Dot(forward) > 0.1f))
                return true;
        }
        return false;
    }

    private bool ShouldYield()
    {
        Vector3 player = _playerPosition?.Invoke() ?? GlobalPosition;
        Vector3 van = _vanPosition?.Invoke() ?? GlobalPosition;
        return _testHold || ShouldYieldTo(GlobalPosition, -GlobalTransform.Basis.Z,
            player, _playerDriving?.Invoke() == true, van);
    }

    /// <summary>Controlled yield seam for the city streaming reconciliation test.</summary>
    public void SetTestHold(bool hold) => _testHold = hold;

    private void SetPoseFromProgress(float y)
    {
        if (_route == null) return;
        CityRoutePoint point = PointAt(Progress);
        CityRoutePoint ahead = PointAt(Wrap(Progress + 2f, _length));
        _yaw = Mathf.Atan2(-(ahead.X - point.X), -(ahead.Z - point.Z));
        Position = new Vector3(point.X, y, point.Z);
        Rotation = new Vector3(0, _yaw, 0);
    }

    private CityRoutePoint PointAt(float progress)
    {
        if (_route.Length == 0) return default;
        progress = Wrap(progress, _length);
        int segment = 0;
        while (segment < _route.Length - 1 && _cumulative[segment + 1] < progress)
            segment++;
        CityRoutePoint a = _route[segment];
        CityRoutePoint b = _route[(segment + 1) % _route.Length];
        float segmentLength = _cumulative[segment + 1] - _cumulative[segment];
        float t = segmentLength > 0.0001f
            ? (progress - _cumulative[segment]) / segmentLength
            : 0f;
        return new CityRoutePoint(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
    }

    private static float[] BuildCumulative(CityRoutePoint[] route)
    {
        var result = new float[route.Length + 1];
        for (int i = 1; i <= route.Length; i++)
        {
            CityRoutePoint a = route[i - 1];
            CityRoutePoint b = route[i % route.Length];
            result[i] = result[i - 1] +
                        Mathf.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
        }
        return result;
    }

    private static float Wrap(float value, float length)
    {
        if (length <= 0.0001f) return 0;
        float wrapped = value % length;
        return wrapped < 0 ? wrapped + length : wrapped;
    }
}

/// <summary>Visual pedestrian. Near uses the authored character rig and limb
/// swing; mid uses a two-mesh impostor and no animation.</summary>
public partial class CityPedestrianVisual : Node3D
{
    public int AgentId { get; private set; }
    public CityActorTier Tier { get; private set; } = CityActorTier.Mid;
    public float Progress { get; private set; }
    public float Speed { get; private set; }

    private CityRoutePoint[] _route;
    private float[] _cumulative;
    private float _length;
    private Node3D _full;
    private Node3D _proxy;
    private Node3D _leftLeg;
    private Node3D _rightLeg;
    private Node3D _leftArm;
    private Node3D _rightArm;
    private float _anim;
    private float _yaw;

    public void Initialize(CityAgentState agent, CityRoutePoint[] route)
    {
        AgentId = agent.Id;
        _route = route;
        _cumulative = BuildCumulative(route);
        _length = _cumulative[^1];
        Progress = agent.Phase;
        Speed = agent.Speed;
        Name = $"CityPed_{agent.Id}";
        BuildVisual(agent);
        SetPoseFromProgress(0.2f);
        SetTier(CityActorTier.Mid);
    }

    private void BuildVisual(CityAgentState agent)
    {
        Color shirt = CityPalette.PedColour(agent.Colour);
        _proxy = new Node3D { Name = "MidProxy" };
        AddChild(_proxy);
        Geo.Cylinder(_proxy, new Vector3(0, 0.72f, 0), 0.22f, 0.28f, 1.25f, shirt, 7, null, false,
            "ProxyBody");
        Geo.Sphere(_proxy, new Vector3(0, 1.52f, 0), 0.24f, Palette.Skin, 8, 4, null,
            "ProxyHead");
        _proxy.Visible = true;

        _full = new Node3D { Name = "Detailed" };
        AddChild(_full);
        string path = agent.Variant == 0
            ? VisualTemplate.PlayerBoyPath
            : VisualTemplate.PlayerGirlPath;
        Node3D template = VisualTemplate.Instantiate(_full, "Visual", path);
        var tints = new Dictionary<int, Color>
        {
            [0] = shirt, [1] = shirt, [2] = shirt, [3] = shirt,
            [4] = shirt.Darkened(0.1f), [5] = shirt.Darkened(0.15f),
            [6] = Palette.Navy, [7] = Palette.Navy,
            [12] = Palette.Cream, [15] = shirt.Lightened(0.2f),
            [16] = shirt.Lightened(0.2f), [17] = shirt.Lightened(0.2f),
        };
        CharacterRig rig = VisualTemplate.BuildCharacterRig(template, tints);
        _leftLeg = rig.LeftLeg;
        _rightLeg = rig.RightLeg;
        _leftArm = rig.LeftArm;
        _rightArm = rig.RightArm;
        _full.Scale = new Vector3(0.86f, 0.86f, 0.86f);
        _full.Visible = false;
    }

    public void SetTier(CityActorTier tier)
    {
        Tier = tier;
        if (_full != null) _full.Visible = tier == CityActorTier.Near;
        if (_proxy != null) _proxy.Visible = tier == CityActorTier.Mid;
        SetProcess(tier == CityActorTier.Near);
    }

    public void SpawnAt(CityAgentPose pose, CityActorTier tier)
    {
        Progress = pose.RouteProgress;
        _yaw = pose.Yaw;
        SetPoseFromProgress(0.2f);
        SetTier(tier);
    }

    public void StepMid(float delta)
    {
        if (Tier != CityActorTier.Mid) return;
        Progress = Wrap(Progress + Speed * delta, _length);
        SetPoseFromProgress(0.2f);
    }

    public override void _Process(double delta)
    {
        if (Tier != CityActorTier.Near) return;
        float dt = (float)delta;
        Progress = Wrap(Progress + Speed * dt, _length);
        SetPoseFromProgress(0.2f);
        _anim += dt * Speed * 5.2f;
        float swing = Mathf.Sin(_anim) * 0.55f;
        if (_leftLeg != null) _leftLeg.Rotation = new Vector3(swing, 0, 0);
        if (_rightLeg != null) _rightLeg.Rotation = new Vector3(-swing, 0, 0);
        if (_leftArm != null) _leftArm.Rotation = new Vector3(-swing * 0.65f, 0, 0);
        if (_rightArm != null) _rightArm.Rotation = new Vector3(swing * 0.65f, 0, 0);
        _full.Position = new Vector3(0, Mathf.Abs(Mathf.Sin(_anim)) * 0.025f, 0);
    }

    private void SetPoseFromProgress(float y)
    {
        if (_route == null) return;
        CityRoutePoint point = PointAt(Progress);
        CityRoutePoint ahead = PointAt(Wrap(Progress + 1.2f, _length));
        _yaw = Mathf.Atan2(-(ahead.X - point.X), -(ahead.Z - point.Z));
        Position = new Vector3(point.X, y, point.Z);
        Rotation = new Vector3(0, _yaw, 0);
    }

    private CityRoutePoint PointAt(float progress)
    {
        if (_route.Length == 0) return default;
        progress = Wrap(progress, _length);
        int segment = 0;
        while (segment < _route.Length - 1 && _cumulative[segment + 1] < progress)
            segment++;
        CityRoutePoint a = _route[segment];
        CityRoutePoint b = _route[(segment + 1) % _route.Length];
        float segmentLength = _cumulative[segment + 1] - _cumulative[segment];
        float t = segmentLength > 0.0001f
            ? (progress - _cumulative[segment]) / segmentLength
            : 0f;
        return new CityRoutePoint(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
    }

    private static float[] BuildCumulative(CityRoutePoint[] route)
    {
        var result = new float[route.Length + 1];
        for (int i = 1; i <= route.Length; i++)
        {
            CityRoutePoint a = route[i - 1];
            CityRoutePoint b = route[i % route.Length];
            result[i] = result[i - 1] +
                        Mathf.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
        }
        return result;
    }

    private static float Wrap(float value, float length)
    {
        if (length <= 0.0001f) return 0;
        float wrapped = value % length;
        return wrapped < 0 ? wrapped + length : wrapped;
    }
}

public static class CityPalette
{
    public static readonly Color[] Cars =
    {
        Palette.Red, Palette.Blue, Palette.Teal, Palette.Yellow,
        Palette.Lavender, Palette.Orange,
    };

    public static readonly Color[] Pedestrians =
    {
        Palette.Blue, Palette.Orange, Palette.Teal, Palette.Lavender,
        Palette.Red, Palette.Grass, Palette.Pink, Palette.Cream,
    };

    public static Color CarColour(int index) => Cars[Math.Abs(index) % Cars.Length];
    public static Color PedColour(int index) => Pedestrians[Math.Abs(index) % Pedestrians.Length];
}

/// <summary>
/// Bounded visual pool around the player. It only owns actors for the nearest
/// logical agents, makes at most one node per logical id, and updates mid-tier
/// impostors at 4 Hz. Near actors opt into physics and animation.
/// </summary>
public partial class CityActorPool : Node3D
{
    public const int MaxNearVehicles = 14;
    public const int MaxNearPedestrians = 22;
    public const float ActiveRadius = 175f;
    public const float NearRadius = 78f;
    public const float MaxVisualLagMeters = 18f;
    private const float RetentionRadius = ActiveRadius + 22f;
    private const float MidStep = 0.25f;
    private readonly Dictionary<int, float> _visualLags = new();

    public int VisualVehicleCount => _vehicles.Count;
    public int VisualPedestrianCount => _pedestrians.Count;
    public int NearVehicleCount => _vehicles.Values.Count(v => v.Tier == CityActorTier.Near);
    public int NearPedestrianCount => _pedestrians.Values.Count(p => p.Tier == CityActorTier.Near);
    public int MidVehicleCount => _vehicles.Values.Count(v => v.Tier == CityActorTier.Mid);
    public int MidPedestrianCount => _pedestrians.Values.Count(p => p.Tier == CityActorTier.Mid);

    private readonly Dictionary<int, CityVehicleVisual> _vehicles = new();
    private readonly Dictionary<int, CityPedestrianVisual> _pedestrians = new();
    private CitySimulation _sim;
    private IReadOnlyList<CityRoutePoint[]> _vehicleRoutes = Array.Empty<CityRoutePoint[]>();
    private IReadOnlyList<CityRoutePoint[]> _pedestrianRoutes = Array.Empty<CityRoutePoint[]>();
    private Func<Vector3> _playerPosition;
    private Func<Vector3> _vanPosition;
    private Func<bool> _driving;
    private float _midTimer;
    private float _poolTimer;

    public void Initialize(CitySimulation simulation,
        IReadOnlyList<CityRoutePoint[]> vehicleRoutes,
        IReadOnlyList<CityRoutePoint[]> pedestrianRoutes,
        Func<Vector3> playerPosition, Func<Vector3> vanPosition, Func<bool> driving)
    {
        _sim = simulation;
        _vehicleRoutes = vehicleRoutes;
        _pedestrianRoutes = pedestrianRoutes;
        _playerPosition = playerPosition;
        _vanPosition = vanPosition;
        _driving = driving;
        Name = "CityActorPool";
        ProcessMode = ProcessModeEnum.Pausable;
    }

    public void UpdatePool(float delta, Vector3 focusWorld, float speed, bool allowVisuals,
        float cameraAltitude)
    {
        if (_sim == null) return;
        _midTimer -= delta;
        _poolTimer -= delta;
        Vector3 localFocus = focusWorld - new Vector3(CityLayout.OriginX, 0f, CityLayout.OriginZ);
        float nearRadius = NearRadius + Mathf.Clamp(speed * 2.6f, 0f, 22f);
        if (cameraAltitude > 18f) nearRadius *= 0.78f;
        float activeRadius = ActiveRadius + Mathf.Clamp(speed * 4f, 0f, 30f);

        if (!allowVisuals)
        {
            ClearAll();
            return;
        }

        if (_poolTimer <= 0f)
        {
            _poolTimer = 0.5f;
            RefreshSelection(localFocus, activeRadius, nearRadius);
        }

        ReconcileVehicles();
        if (_midTimer <= 0f)
        {
            _midTimer = MidStep;
            foreach (var ped in _pedestrians.Values)
                ped.StepMid(MidStep);
        }
    }

    private static float SignedDelta(float from, float to, float length)
    {
        if (length <= 0.0001f) return 0f;
        float delta = (to - from) % length;
        if (delta > length * 0.5f) delta -= length;
        else if (delta < -length * 0.5f) delta += length;
        return delta;
    }

    private void ReconcileVehicles()
    {
        if (_sim == null) return;
        foreach (var pair in _vehicles)
        {
            var vehicle = pair.Value;
            var agent = _sim.Agents.FirstOrDefault(a => a.Id == pair.Key);
            if (agent == null) continue;
            var pose = _sim.PoseAt(agent);
            if (vehicle.Tier == CityActorTier.Mid)
            {
                vehicle.SnapToProgress(pose.RouteProgress);
                vehicle.SetReconciliationLag(0f);
                _visualLags[pair.Key] = 0f;
                continue;
            }
            float lag = SignedDelta(vehicle.Progress, pose.RouteProgress, vehicle.RouteLength);
            // Keep the stored lag bounded, but never teleport a near actor
            // forward while it is yielding (that can push it through the
            // player). The physical car keeps its place and catches up when
            // the yield ends; streaming re-entry uses the bounded stored lag.
            lag = Mathf.Clamp(lag, -MaxVisualLagMeters, MaxVisualLagMeters);
            vehicle.SetReconciliationLag(lag);
            _visualLags[pair.Key] = lag;
        }
    }

    private void RefreshSelection(Vector3 focus, float activeRadius, float nearRadius)
    {
        var selectedVehicles = new List<(float Distance, CityAgentState Agent)>();
        var selectedPeds = new List<(float Distance, CityAgentState Agent)>();
        float keepRadius = Mathf.Max(activeRadius, RetentionRadius);

        foreach (var agent in _sim.Agents)
        {
            bool alreadyVisible = agent.IsVehicle
                ? _vehicles.ContainsKey(agent.Id)
                : _pedestrians.ContainsKey(agent.Id);
            if (!_sim.IsOnDuty(agent) && !alreadyVisible) continue;
            var pose = _sim.PoseAt(agent);
            float dx = pose.X - focus.X;
            float dz = pose.Z - focus.Z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            float radius = alreadyVisible ? keepRadius : activeRadius;
            if (distance > radius) continue;
            if (!alreadyVisible && distance < 6f) continue;
            if (agent.IsVehicle) selectedVehicles.Add((distance, agent));
            else selectedPeds.Add((distance, agent));
        }

        selectedVehicles.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        selectedPeds.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        var keepVehicles = new HashSet<int>(selectedVehicles
            .Take(MaxNearVehicles).Select(v => v.Agent.Id));
        var keepPeds = new HashSet<int>(selectedPeds
            .Take(MaxNearPedestrians).Select(v => v.Agent.Id));

        foreach (int id in _vehicles.Keys.Where(id => !keepVehicles.Contains(id)).ToList())
        {
            _vehicles[id].QueueFree();
            _vehicles.Remove(id);
        }
        foreach (int id in _pedestrians.Keys.Where(id => !keepPeds.Contains(id)).ToList())
        {
            _pedestrians[id].QueueFree();
            _pedestrians.Remove(id);
        }

        foreach (var entry in selectedVehicles.Take(MaxNearVehicles))
        {
            if (!_vehicles.TryGetValue(entry.Agent.Id, out var vehicle))
            {
                vehicle = new CityVehicleVisual();
                AddChild(vehicle);
                vehicle.Initialize(entry.Agent, _vehicleRoutes[entry.Agent.RouteIndex],
                    _playerPosition, _driving, _vanPosition);
                var pose = _sim.PoseAt(entry.Agent);
                float savedLag = _visualLags.TryGetValue(entry.Agent.Id, out float lag) ? lag : 0f;
                vehicle.SpawnAt(pose, TierFor(entry.Distance, nearRadius), savedLag);
                _vehicles[entry.Agent.Id] = vehicle;
            }
            else
            {
                vehicle.SetTier(TierFor(entry.Distance, nearRadius));
            }
        }
        foreach (var entry in selectedPeds.Take(MaxNearPedestrians))
        {
            if (!_pedestrians.TryGetValue(entry.Agent.Id, out var ped))
            {
                ped = new CityPedestrianVisual();
                AddChild(ped);
                ped.Initialize(entry.Agent, _pedestrianRoutes[entry.Agent.RouteIndex]);
                var pose = _sim.PoseAt(entry.Agent);
                ped.SpawnAt(pose, TierFor(entry.Distance, nearRadius));
                _pedestrians[entry.Agent.Id] = ped;
            }
            else
            {
                ped.SetTier(TierFor(entry.Distance, nearRadius));
            }
        }
    }

    private static CityActorTier TierFor(float distance, float nearRadius) =>
        distance <= nearRadius ? CityActorTier.Near : CityActorTier.Mid;

    private void ClearAll()
    {
        foreach (var vehicle in _vehicles.Values) vehicle.QueueFree();
        foreach (var ped in _pedestrians.Values) ped.QueueFree();
        _vehicles.Clear();
        _pedestrians.Clear();
    }

    public void ResetAll()
    {
        ClearAll();
        _visualLags.Clear();
        _poolTimer = 0f;
        _midTimer = 0f;
    }
}
