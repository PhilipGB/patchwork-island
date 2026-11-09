using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

/// <summary>M08 village-green decoration with a reversible 20-coin job budget.</summary>
public partial class ParkBuilder : Node3D, IBuildMode
{
    public bool Active { get; private set; }

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
    private string _selected = "bench";

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
        Name = "ParkBuilder";

        for (int i = 0; i < ParkRules.Slots.Length; i++)
        {
            var socket = new BuildSocket();
            AddChild(socket);
            Vector3 pos = i < _worldPadCount() ? Vector3.Zero : Vector3.Zero;
            socket.Setup(ParkRules.Slots[i], "pad", pos, new Vector3(2.35f, 0.08f, 2.0f),
                null, 0f, false);
            socket.SetEnabled(false);
            _sockets[socket.SocketId] = socket;
        }

        _buildCamera = new Camera3D { Name = "ParkBuildCamera", Fov = 58, Current = false };
        AddChild(_buildCamera);
        _buildCamera.GlobalPosition = new Vector3(15f, 8.5f, 27f);
        _buildCamera.LookAt(new Vector3(15f, 0.4f, 15.5f), Vector3.Up);
    }

    private Vector3[] _pads = Array.Empty<Vector3>();

    public void SetPads(Vector3[] pads)
    {
        _pads = pads;
        for (int i = 0; i < _sockets.Count && i < pads.Length; i++)
            _sockets[ParkRules.Slots[i]].GlobalPosition = pads[i] + Vector3.Up * 0.1f;
        RestorePlacements();
    }

    private int _worldPadCount() => _pads.Length;

    private void RestorePlacements()
    {
        foreach (var placement in _data.ParkPlacements)
        {
            if (!_sockets.TryGetValue(placement.Slot, out var socket)) continue;
            var visual = BuildItemVisual(placement.Type);
            socket.SetPlacedVisual(visual);
            socket.MarkPlaced();
            _visuals[placement.Slot] = visual;
        }
    }

    public void OpenBuildMode()
    {
        if (ParkRules.IsComplete(_data))
        {
            _hud.ShowBigToast("The village green is finished. Thank you!", 3.2f, Palette.Success);
            return;
        }
        if (_data.ActiveMission != "M08")
        {
            _hud.ShowHint("Start Park Planner at the yellow JOBS board first.");
            return;
        }
        Active = true;
        _selected = ParkRules.Items.FirstOrDefault(item => ParkRules.Count(_data, item) == 0) ?? "bench";
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
            if (!socket.Placed) socket.SetEnabled(false);
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
        var query = PhysicsRayQueryParameters3D.Create(origin, origin + normal * 80f, 8u, null);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || hit["collider"].AsGodotObject() is not BuildSocket socket)
        {
            _hud.ShowBigToast("Click an empty pad inside the village green.", 2.2f, Palette.Lavender);
            return true;
        }

        var outcome = ParkRules.Place(_data, _selected, socket.SocketId);
        if (!outcome.Success)
        {
            _hud.ShowHint(outcome.Message);
            return true;
        }

        var visual = BuildItemVisual(_selected);
        socket.SetPlacedVisual(visual);
        socket.MarkPlaced();
        _visuals[socket.SocketId] = visual;
        // Auto-advance to the next missing item for a smooth first play.
        string next = ParkRules.Items.FirstOrDefault(item => ParkRules.Count(_data, item) == 0);
        if (next != null) _selected = next;
        RefreshSockets();
        RefreshBar();
        _onProgress?.Invoke();
        _hud.ShowToast(outcome.Message, 2.4f, Palette.Success);
        AudioManager.PlayWorld("build_place");

        if (ParkRules.IsComplete(_data))
            CompleteBuild();
        return true;
    }

    public void SelectItem(string item)
    {
        _selected = item;
        RefreshSockets();
        RefreshBar();
    }

    public void UndoLast()
    {
        var removed = ParkRules.UndoLast(_data);
        if (removed == null)
        {
            _hud.ShowToast("Nothing to undo yet.", 2f, Palette.Stone);
            return;
        }
        if (_sockets.TryGetValue(removed.Slot, out var socket))
        {
            if (_visuals.TryGetValue(removed.Slot, out var visual) && GodotObject.IsInstanceValid(visual))
                visual.QueueFree();
            _visuals.Remove(removed.Slot);
            socket.ClearPlacement();
            socket.SetEnabled(true);
        }
        _selected = ParkRules.Items.FirstOrDefault(item => ParkRules.Count(_data, item) == 0) ?? removed.Type;
        RefreshSockets();
        RefreshBar();
        _onProgress?.Invoke();
        _hud.ShowToast(ParkRules.Cap(removed.Type) + " removed. Job coins refunded.", 2.4f, Palette.Water);
    }

    public void RotateSelected()
    {
        if (!Active) return;
        _hud.ShowBigToast("Items snap square to the pad — no rotation needed.", 2.2f, Palette.Lavender);
    }

    private void RefreshSockets()
    {
        foreach (var socket in _sockets.Values)
        {
            if (socket.Placed) continue;
            socket.SetEnabled(Active);
            socket.SetValid(Active);
        }
    }

    private void RefreshBar()
    {
        if (!Active) return;
        int spent = ParkRules.Spent(_data);
        int left = ParkRules.Left(_data);
        var options = new List<BuildBarOption>();
        foreach (var item in ParkRules.Items)
        {
            int price = BudgetRules.ParkPrice(item);
            int count = ParkRules.Count(_data, item);
            bool canAdd = spent + price <= BudgetRules.ParkBudget && count == 0;
            options.Add(new BuildBarOption
            {
                Label = ParkRules.Cap(item),
                Detail = count > 0 ? "placed" : $"{price}",
                Icon = item switch { "bench" => "bench", "flowerbed" => "flowerbed", _ => "lamp" },
                Selected = _selected == item,
                Enabled = canAdd,
                Action = () => SelectItem(item),
            });
        }
        string instruction = $"Choose one bench, one flowerbed and one lamp. Put each on a green pad. Spent {spent} of 20 · {left} left.";
        _hud.ShowBuildBar("Park Planner", instruction, options,
            "You place one of each for 6 + 4 + 9 = 19, leaving 1 job coin. Undo is free. Esc steps away.",
            CloseBuildMode, UndoLast);
    }

    private void CompleteBuild()
    {
        Active = false;
        foreach (var socket in _sockets.Values)
            socket.SetEnabled(false);
        _buildCamera.Current = false;
        _camera.Current = true;
        _camera.Snap();
        _onBuildModeChanged?.Invoke(false);
        _hud.HideBuildBar();
        _hud.ShowBigToast("The village green is decorated! 6 + 4 + 9 = 19, with 1 coin left.", 4f, Palette.Success);
        _onComplete?.Invoke();
    }

    private Node3D BuildItemVisual(string item)
    {
        var root = new Node3D { Name = $"Park_{item}" };
        switch (item)
        {
            case "bench":
                Geo.Box(root, new Vector3(0, 0.55f, 0), new Vector3(2.0f, 0.18f, 0.7f), Palette.Wood);
                Geo.Box(root, new Vector3(0, 0.95f, -0.32f), new Vector3(2.0f, 0.55f, 0.14f), Palette.Wood);
                Geo.Box(root, new Vector3(-0.75f, 0.28f, 0), new Vector3(0.16f, 0.5f, 0.55f), Palette.StoneDark);
                Geo.Box(root, new Vector3(0.75f, 0.28f, 0), new Vector3(0.16f, 0.5f, 0.55f), Palette.StoneDark);
                break;
            case "flowerbed":
                Geo.Box(root, new Vector3(0, 0.3f, 0), new Vector3(2.0f, 0.6f, 1.4f), Palette.Wood);
                Geo.Box(root, new Vector3(0, 0.56f, 0), new Vector3(1.7f, 0.15f, 1.1f), Color.FromHtml("5B3A16"));
                for (int i = 0; i < 5; i++)
                {
                    float x = -0.65f + i * 0.33f;
                    Geo.Cylinder(root, new Vector3(x, 0.75f, 0), 0.03f, 0.03f, 0.35f, Palette.GrassDark, 4);
                    Geo.Sphere(root, new Vector3(x, 0.96f, 0), 0.18f,
                        i % 2 == 0 ? Palette.Pink : Palette.Yellow, 6, 3);
                }
                break;
            default:
                Geo.Cylinder(root, new Vector3(0, 1.5f, 0), 0.1f, 0.14f, 2.7f, Palette.Navy, 6);
                Geo.Box(root, new Vector3(0, 2.9f, 0), new Vector3(0.6f, 0.25f, 0.6f), Palette.Navy);
                Geo.Sphere(root, new Vector3(0, 2.7f, 0), 0.28f, Color.FromHtml("FFE6A3"), 8, 4, null, "LampGlow");
                var light = new OmniLight3D
                {
                    Position = new Vector3(0, 2.6f, 0),
                    LightColor = new Color("FFE1A8"),
                    LightEnergy = 0.7f,
                    OmniRange = 5.5f,
                    ShadowEnabled = false,
                };
                root.AddChild(light);
                break;
        }
        return root;
    }
}
