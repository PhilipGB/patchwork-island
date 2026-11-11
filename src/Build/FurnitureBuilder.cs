using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

/// <summary>M10 house furnishing: snap pads, buy/place/move and persistence hooks.</summary>
public partial class FurnitureBuilder : Node3D, IBuildMode
{
    public bool Active { get; private set; }
    public int PlacedVisualCount => _visuals.Count;

    private GameData _data;
    private MissionService _missions;
    private CameraRig _camera;
    private Hud _hud;
    private Action _onComplete;
    private Action<bool> _onBuildModeChanged;
    private Action _onProgress;
    private Camera3D _buildCamera;
    private readonly Dictionary<string, BuildSocket> _sockets = new();
    private readonly Dictionary<string, Node3D> _visuals = new();
    private string _selected = "table";

    public void Initialize(GameData data, MissionService missions, CameraRig camera, Hud hud,
        Action onComplete, Action<bool> onBuildModeChanged, Action onProgress)
    {
        _data = data;
        _missions = missions;
        _camera = camera;
        _hud = hud;
        _onComplete = onComplete;
        _onBuildModeChanged = onBuildModeChanged;
        _onProgress = onProgress;
        Name = "FurnitureBuilder";

        for (int i = 0; i < FurnitureRules.Slots.Length; i++)
        {
            var socket = new BuildSocket();
            AddChild(socket);
            socket.Setup(FurnitureRules.Slots[i], "pad", Vector3.Zero,
                new Vector3(2.0f, 0.1f, 2.0f), null, 0f, false);
            socket.SetEnabled(false);
            _sockets[socket.SocketId] = socket;
        }

        _buildCamera = new Camera3D { Name = "FurnitureBuildCamera", Fov = 70, Current = false };
        AddChild(_buildCamera);
        _buildCamera.GlobalPosition = new Vector3(-38f, 3.2f, 19.7f);
        _buildCamera.LookAt(new Vector3(-38f, 0.55f, 25.2f), Vector3.Up);
    }

    public void SetPads(Vector3[] pads)
    {
        for (int i = 0; i < _sockets.Count && i < pads.Length; i++)
            _sockets[FurnitureRules.Slots[i]].GlobalPosition = pads[i] + Vector3.Up * 0.1f;
        RestorePlacements();
    }

    private void RestorePlacements()
    {
        foreach (var placement in _data.FurniturePlacements)
        {
            if (!_sockets.TryGetValue(placement.Slot, out var socket)) continue;
            var visual = BuildFurnitureVisual(placement.Type);
            socket.SetPlacedVisual(visual);
            socket.MarkPlaced();
            _visuals[placement.Slot] = visual;
        }
    }

    public void OpenBuildMode()
    {
        bool canArrange = _data.IsCompleted("M10") || _data.ActiveMission == "M10";
        if (!canArrange)
        {
            _hud.ShowHint("Start Home Sweet Home at the yellow JOBS board first.");
            return;
        }
        bool any = FurnitureRules.Items.Any(item =>
            FurnitureRules.Available(_data, item) > 0) || _data.FurniturePlacements.Count > 0;
        if (!any)
        {
            _hud.ShowBigToast("Buy some furniture from the catalogue first.", 2.8f, Palette.Lavender);
            return;
        }

        Active = true;
        _selected = FurnitureRules.Items.FirstOrDefault(item => FurnitureRules.Available(_data, item) > 0) ?? "table";
        _buildCamera.Current = true;
        _onBuildModeChanged?.Invoke(true);
        _hud.SetPrompt(null);
        RefreshSockets();
        RefreshBar();
    }

    public void CloseBuildMode()
    {
        if (!Active) return;
        Active = false;
        foreach (var socket in _sockets.Values)
        {
            if (socket.Placed) socket.SetClickable(false);
            else socket.SetEnabled(false);
        }
        _buildCamera.Current = false;
        _camera.Current = true;
        _camera.Snap();
        _onBuildModeChanged?.Invoke(false);
        _hud.HideBuildBar();
    }

    public bool HandleWorldClick(Vector2 screenPosition)
    {
        if (!Active) return false;
        Camera3D camera = _buildCamera.Current ? _buildCamera : _camera;
        Vector3 origin = camera.ProjectRayOrigin(screenPosition);
        Vector3 normal = camera.ProjectRayNormal(screenPosition);
        using var query = PhysicsRayQueryParameters3D.Create(origin, origin + normal * 60f, 8u, null);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || hit["collider"].AsGodotObject() is not BuildSocket socket)
        {
            _hud.ShowBigToast("Click a furniture pad on the floor.", 2.2f, Palette.Lavender);
            return true;
        }

        if (socket.Placed)
        {
            var picked = FurnitureRules.PickUp(_data, socket.SocketId);
            if (picked == null) return true;
            if (_visuals.TryGetValue(socket.SocketId, out var visual) && GodotObject.IsInstanceValid(visual))
                visual.QueueFree();
            _visuals.Remove(socket.SocketId);
            socket.ClearPlacement();
            socket.SetEnabled(true);
            _selected = picked.Type;
            RefreshSockets();
            RefreshBar();
            _onProgress?.Invoke();
            _hud.ShowBigToast($"{FurnitureRules.Label(picked.Type)} picked up. Click an empty pad.", 2.8f, Palette.Lavender);
            return true;
        }

        var outcome = FurnitureRules.Place(_data, _selected, socket.SocketId);
        if (!outcome.Success)
        {
            _hud.ShowHint(outcome.Message);
            return true;
        }
        var placedVisual = BuildFurnitureVisual(_selected);
        socket.SetPlacedVisual(placedVisual);
        socket.MarkPlaced();
        socket.SetClickable(true);
        _visuals[socket.SocketId] = placedVisual;
        string next = FurnitureRules.Items.FirstOrDefault(item => FurnitureRules.Available(_data, item) > 0);
        if (next != null) _selected = next;
        RefreshSockets();
        RefreshBar();
        _onProgress?.Invoke();
        _hud.ShowToast(outcome.Message, 2.2f, Palette.Success);
        AudioManager.PlayWorld("build_place");

        if (_data.ActiveMission == "M10" && MissionRules.IsObjectiveComplete(_data, "M10"))
            CompleteBuild();
        return true;
    }

    public void SelectItem(string item)
    {
        if (FurnitureRules.Available(_data, item) <= 0) return;
        _selected = item;
        RefreshSockets();
        RefreshBar();
    }

    public void RotateSelected()
    {
        if (Active) _hud.ShowBigToast("Furniture snaps tidily to the floor pads.", 2.2f, Palette.Lavender);
    }

    private void RefreshSockets()
    {
        foreach (var socket in _sockets.Values)
        {
            if (socket.Placed)
            {
                socket.SetClickable(Active);
                continue;
            }
            socket.SetEnabled(Active);
            socket.SetValid(Active);
        }
    }

    private void RefreshBar()
    {
        if (!Active) return;
        var options = new List<BuildBarOption>();
        foreach (var item in FurnitureRules.Items)
        {
            int available = FurnitureRules.Available(_data, item);
            int placed = FurnitureRules.Placed(_data, item);
            options.Add(new BuildBarOption
            {
                Label = FurnitureRules.Label(item),
                Detail = $"{available} ready · {placed} placed",
                Icon = item,
                Selected = _selected == item,
                Enabled = available > 0,
                Action = () => SelectItem(item),
            });
        }
        _hud.ShowBuildBar("Home Sweet Home",
            "Choose a piece, then click a glowing floor pad. Click a placed piece to pick it up and move it.",
            options,
            "The house list needs 1 table, 2 chairs and 1 plant. You can rearrange for free.",
            CloseBuildMode);
    }

    private void CompleteBuild()
    {
        Active = false;
        foreach (var socket in _sockets.Values)
        {
            if (socket.Placed) socket.SetClickable(false);
            else socket.SetEnabled(false);
        }
        _buildCamera.Current = false;
        _camera.Current = true;
        _camera.Snap();
        _onBuildModeChanged?.Invoke(false);
        _hud.HideBuildBar();
        _hud.ShowBigToast("Your house is furnished! Table + 2 chairs + plant = 21 coins.", 4f, Palette.Success);
        _onComplete?.Invoke();
    }

    private Node3D BuildFurnitureVisual(string item)
    {
        var root = new Node3D { Name = $"Furniture_{item}" };
        switch (item)
        {
            case "table":
                Geo.Box(root, new Vector3(0, 0.8f, 0), new Vector3(1.9f, 0.16f, 1.15f), Palette.Wood);
                Geo.Box(root, new Vector3(0, 0.91f, 0), new Vector3(1.7f, 0.06f, 0.95f), Palette.WoodDark);
                Geo.Box(root, new Vector3(-0.75f, 0.4f, -0.4f), new Vector3(0.15f, 0.8f, 0.15f), Palette.WoodDark);
                Geo.Box(root, new Vector3(0.75f, 0.4f, -0.4f), new Vector3(0.15f, 0.8f, 0.15f), Palette.WoodDark);
                Geo.Box(root, new Vector3(-0.75f, 0.4f, 0.4f), new Vector3(0.15f, 0.8f, 0.15f), Palette.WoodDark);
                Geo.Box(root, new Vector3(0.75f, 0.4f, 0.4f), new Vector3(0.15f, 0.8f, 0.15f), Palette.WoodDark);
                break;
            case "chair":
                Geo.Box(root, new Vector3(0, 0.52f, 0), new Vector3(0.8f, 0.15f, 0.8f), Palette.Wood);
                Geo.Box(root, new Vector3(0, 0.98f, 0.32f), new Vector3(0.8f, 0.75f, 0.14f), Palette.Wood);
                Geo.Box(root, new Vector3(-0.3f, 0.25f, -0.3f), new Vector3(0.12f, 0.5f, 0.12f), Palette.WoodDark);
                Geo.Box(root, new Vector3(0.3f, 0.25f, -0.3f), new Vector3(0.12f, 0.5f, 0.12f), Palette.WoodDark);
                Geo.Box(root, new Vector3(-0.3f, 0.25f, 0.3f), new Vector3(0.12f, 0.5f, 0.12f), Palette.WoodDark);
                Geo.Box(root, new Vector3(0.3f, 0.25f, 0.3f), new Vector3(0.12f, 0.5f, 0.12f), Palette.WoodDark);
                break;
            default:
                Geo.Cylinder(root, new Vector3(0, 0.35f, 0), 0.36f, 0.28f, 0.7f, Palette.Orange, 9);
                Geo.Cylinder(root, new Vector3(0, 0.72f, 0), 0.34f, 0.34f, 0.08f, Palette.WoodDark, 9);
                Geo.Cylinder(root, new Vector3(0, 0.95f, 0), 0.04f, 0.04f, 0.55f, Palette.GrassDark, 5);
                Geo.Sphere(root, new Vector3(-0.18f, 1.18f, 0), 0.23f, Palette.GrassDark, 6, 3,
                    new Vector3(1f, 0.55f, 1f));
                Geo.Sphere(root, new Vector3(0.18f, 1.23f, 0.05f), 0.24f, Palette.Grass, 6, 3,
                    new Vector3(1f, 0.55f, 1f));
                Geo.Sphere(root, new Vector3(0, 1.35f, -0.12f), 0.2f, Color.FromHtml("55B85D"), 6, 3,
                    new Vector3(1f, 0.55f, 1f));
                break;
        }
        return root;
    }
}
