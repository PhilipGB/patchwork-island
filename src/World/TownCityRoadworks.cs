using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Runtime owner of the authored town/city roadworks gate. The scene is an
/// input asset: this class only toggles its named parts, disables the physical
/// gate collider on completion and animates the authored crew figures. The
/// operation is idempotent so restoring the same save never opens or rewards
/// twice.
/// </summary>
public partial class TownCityRoadworks : Node3D
{
    public const string ScenePath = "res://scenes/city/TownCityRoadworks.tscn";
    public const float WorldX = 96f;
    public const float WorldZ = 0f;

    public bool RoadOpen { get; private set; }
    public bool BarrierVisible => _roadClosed?.Visible == true;
    public bool ColliderEnabled => _gateCollision != null && _gateCollision.CollisionLayer != 0;
    public Vector3 InteractionPosition => _interaction?.GlobalPosition ?? new Vector3(WorldX - 2.6f, 0.2f, 4.8f);
    public Vector3 TownSideStop => _townStop?.GlobalPosition ?? new Vector3(WorldX - 6f, 0f, 0f);
    public Vector3 CitySideStop => _cityStop?.GlobalPosition ?? new Vector3(WorldX + 14f, 0f, 0f);

    private Node3D _roadClosed;
    private StaticBody3D _gateCollision;
    private Label3D _closedLabel;
    private Label3D _openLabel;
    private Node3D _fenceNorth;
    private Node3D _fenceSouth;
    private Marker3D _interaction;
    private Marker3D _townStop;
    private Marker3D _cityStop;
    private Node3D _crewForeman;
    private Node3D _crewA;
    private Node3D _crewB;
    private readonly List<CollisionShape3D> _shapes = new();
    private float _completionTime = -1f;
    private Vector3 _foremanHome;
    private Vector3 _crewAHome;
    private Vector3 _crewBHome;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        Position = new Vector3(WorldX, 0f, WorldZ);
        Name = "TownCityRoadworks";
    }

    public void Build()
    {
        var packed = ResourceLoader.Load<PackedScene>(ScenePath)
                     ?? throw new InvalidOperationException($"Missing authored roadworks scene {ScenePath}.");
        var root = packed.Instantiate<Node3D>()
                   ?? throw new InvalidOperationException($"Could not instantiate {ScenePath}.");
        root.Name = "AuthoredRoadworks";
        AddChild(root);

        _roadClosed = Require<Node3D>(root, "RoadClosed");
        _gateCollision = Require<StaticBody3D>(root, "GateCollision");
        _closedLabel = Require<Label3D>(root, "ClosedLabel");
        _openLabel = Require<Label3D>(root, "OpenLabel");
        _fenceNorth = Require<Node3D>(root, "FenceNorth");
        _fenceSouth = Require<Node3D>(root, "FenceSouth");
        _interaction = Require<Marker3D>(root, "RoadworksInteraction");
        _townStop = Require<Marker3D>(root, "TownSideStop");
        _cityStop = Require<Marker3D>(root, "CitySideStop");
        _crewForeman = Require<Node3D>(root, "CrewForeman");
        _crewA = Require<Node3D>(root, "CrewWorkerA");
        _crewB = Require<Node3D>(root, "CrewWorkerB");

        foreach (Node child in _gateCollision.GetChildren())
            if (child is CollisionShape3D shape)
                _shapes.Add(shape);
        if (_shapes.Count == 0)
            throw new InvalidOperationException("The roadworks gate has no physical collision shapes.");

        _foremanHome = _crewForeman.Position;
        _crewAHome = _crewA.Position;
        _crewBHome = _crewB.Position;
        SetRoadState(false, false);
    }

    /// <summary>Apply the persisted world invariant. Safe to call repeatedly.</summary>
    public void SetRoadState(bool open, bool animate)
    {
        RoadOpen = open;
        if (_roadClosed != null) _roadClosed.Visible = !open;
        if (_fenceNorth != null) _fenceNorth.Visible = !open;
        if (_fenceSouth != null) _fenceSouth.Visible = !open;
        if (_closedLabel != null)
        {
            _closedLabel.Visible = !open;
            // Signs survive; only the physical gate and text state change.
        }
        if (_openLabel != null) _openLabel.Visible = open;
        if (_gateCollision != null)
        {
            _gateCollision.CollisionLayer = open ? 0u : 1u;
            _gateCollision.CollisionMask = 0u;
        }
        foreach (var shape in _shapes)
            if (shape != null) shape.Disabled = open;

        if (open)
        {
            _crewForeman.Position = _foremanHome + new Vector3(3.8f, 0f, 1.2f);
            _crewA.Position = _crewAHome + new Vector3(4.6f, 0f, -0.7f);
            _crewB.Position = _crewBHome + new Vector3(4.1f, 0f, 1.8f);
            _crewForeman.Rotation = new Vector3(0, -1.2f, 0);
            _crewA.Rotation = new Vector3(0, -0.8f, 0);
            _crewB.Rotation = new Vector3(0, -1.5f, 0);
            if (animate)
            {
                _completionTime = 0f;
                _openLabel.Text = "ROAD\nOPEN";
                SetProcess(true);
            }
        }
        else
        {
            _crewForeman.Position = _foremanHome;
            _crewA.Position = _crewAHome;
            _crewB.Position = _crewBHome;
            _crewForeman.Rotation = Vector3.Zero;
            _crewA.Rotation = Vector3.Zero;
            _crewB.Rotation = Vector3.Zero;
            _completionTime = -1f;
            SetProcess(false);
        }
    }

    public override void _Process(double delta)
    {
        if (_completionTime < 0f) return;
        _completionTime += (float)delta;
        float t = Mathf.Clamp(_completionTime / 2.8f, 0f, 1f);
        float ease = t * t * (3f - 2f * t);
        float wave = Mathf.Sin(_completionTime * 7f) * 0.08f * (1f - t);
        _crewForeman.Position = _foremanHome.Lerp(_foremanHome + new Vector3(3.8f, 0f, 1.2f), ease);
        _crewA.Position = _crewAHome.Lerp(_crewAHome + new Vector3(4.6f, 0f, -0.7f), ease) +
                          new Vector3(0, Mathf.Abs(wave), 0);
        _crewB.Position = _crewBHome.Lerp(_crewBHome + new Vector3(4.1f, 0f, 1.8f), ease) +
                          new Vector3(0, Mathf.Abs(wave), 0);
        if (t >= 1f)
        {
            _completionTime = -1f;
            SetProcess(false);
        }
    }

    private static T Require<T>(Node root, string path) where T : Node
    {
        var node = root.GetNodeOrNull<T>(path);
        if (node == null)
            throw new InvalidOperationException($"The authored roadworks scene is missing '{path}'.");
        return node;
    }
}
