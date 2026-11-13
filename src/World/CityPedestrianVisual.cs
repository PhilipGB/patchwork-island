using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

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
            [0] = shirt,
            [1] = shirt,
            [2] = shirt,
            [3] = shirt,
            [4] = shirt.Darkened(0.1f),
            [5] = shirt.Darkened(0.15f),
            [6] = Palette.Navy,
            [7] = Palette.Navy,
            [12] = Palette.Cream,
            [15] = shirt.Lightened(0.2f),
            [16] = shirt.Lightened(0.2f),
            [17] = shirt.Lightened(0.2f),
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
