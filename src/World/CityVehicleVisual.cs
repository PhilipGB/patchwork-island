using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

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
