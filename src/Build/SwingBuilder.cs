using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>M07 playground repair: four ropes and two seats across two frames.</summary>
public partial class SwingBuilder : Node3D, IBuildMode
{
    public bool Active { get; private set; }
    public bool IsBuilt { get; private set; }

    private GameData _data;
    private MissionService _missions;
    private CameraRig _camera;
    private Hud _hud;
    private WorldBuilder _world;
    private Action _onComplete;
    private Action<bool> _onBuildModeChanged;
    private Action _onProgress;
    private Camera3D _buildCamera;
    private readonly Dictionary<string, BuildSocket> _sockets = new();
    private readonly List<Node3D> _swingGroups = new();
    private readonly List<float> _swingTimers = new();
    private readonly List<Interactable> _swingInteractables = new();
    private string _selected = "rope";

    public void Initialize(GameData data, MissionService missions, CameraRig camera, Hud hud,
        WorldBuilder world, Action onComplete, Action<bool> onBuildModeChanged, Action onProgress)
    {
        _data = data;
        _missions = missions;
        _camera = camera;
        _hud = hud;
        _world = world;
        _onComplete = onComplete;
        _onBuildModeChanged = onBuildModeChanged;
        _onProgress = onProgress;
        Name = "SwingBuilder";

        BuildSockets();
        _buildCamera = new Camera3D { Name = "SwingBuildCamera", Fov = 58, Current = false };
        AddChild(_buildCamera);
        _buildCamera.GlobalPosition = new Vector3(-38f, 6.2f, 6.0f);
        _buildCamera.LookAt(new Vector3(-38f, 1.8f, -4.0f), Vector3.Up);

        RestorePieces();
        SetProcess(true);
    }

    private void BuildSockets()
    {
        for (int swing = 0; swing < _world.SwingFrames.Length; swing++)
        {
            Vector3 frame = _world.SwingFrames[swing];
            float z = frame.Z + 0.9f;
            string prefix = swing == 0 ? "s1" : "s2";
            AddRopeSocket($"{prefix}_rope_l", new Vector3(frame.X - 0.9f, 1.8f, z));
            AddRopeSocket($"{prefix}_rope_r", new Vector3(frame.X + 0.9f, 1.8f, z));
            AddSeatSocket($"{prefix}_seat", new Vector3(frame.X, 0.9f, z));
        }
    }

    private void AddRopeSocket(string id, Vector3 pos)
    {
        var socket = new BuildSocket();
        AddChild(socket);
        socket.Setup(id, "rope", pos, new Vector3(0.42f, 2.35f, 0.42f), "rope", 0f, true);
        var visual = new Node3D { Name = "RopeVisual" };
        Geo.Cylinder(visual, new Vector3(0, 0.32f, 0), 0.07f, 0.07f, 2.2f, Palette.WoodDark, 6);
        Geo.Cylinder(visual, new Vector3(0, -0.78f, 0), 0.09f, 0.09f, 0.18f, Palette.Yellow, 6);
        socket.SetPlacedVisual(visual);
        _sockets[id] = socket;
    }

    private void AddSeatSocket(string id, Vector3 pos)
    {
        var socket = new BuildSocket();
        AddChild(socket);
        socket.Setup(id, "seat", pos, new Vector3(1.75f, 0.22f, 0.7f), "seat", 0f, true);
        var visual = new Node3D { Name = "SeatVisual" };
        Geo.Box(visual, Vector3.Zero, new Vector3(1.75f, 0.2f, 0.68f), Palette.Blue);
        Geo.Box(visual, new Vector3(0, 0.14f, 0.24f), new Vector3(1.75f, 0.16f, 0.14f), Color.FromHtml("1C55B8"));
        socket.SetPlacedVisual(visual);
        _sockets[id] = socket;
    }

    private void RestorePieces()
    {
        foreach (string id in _data.SwingPieces)
            if (_sockets.TryGetValue(id, out var socket))
                socket.MarkPlaced();
        if (BuildRules.SwingComplete(_data.SwingPieces))
            CompleteVisuals(false);
        RefreshAvailability();
    }

    public void OpenBuildMode()
    {
        if (IsBuilt || BuildRules.SwingComplete(_data.SwingPieces))
        {
            _hud.ShowBigToast("The swings are fixed. Press E at a seat for a swing!", 3.5f, Palette.Success);
            return;
        }
        if (_data.ActiveMission != "M07")
        {
            _hud.ShowHint("Start Fix the Swings at the yellow JOBS board first.");
            return;
        }

        Active = true;
        _selected = BuildRules.CountOf(_data.SwingPieces, new[] { "s1_rope_l", "s1_rope_r", "s2_rope_l", "s2_rope_r" }) < 4 ? "rope" : "seat";
        _buildCamera.Current = true;
        _onBuildModeChanged?.Invoke(true);
        _hud.SetPrompt(null);
        RefreshAvailability();
        RefreshBar();
    }

    public void CloseBuildMode()
    {
        if (!Active) return;
        Active = false;
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
        if (hit.Count == 0)
        {
            _hud.ShowBigToast("Click a glowing swing socket.", 2.2f, Palette.Lavender);
            return true;
        }
        if (hit["collider"].AsGodotObject() is not BuildSocket socket)
        {
            _hud.ShowBigToast("Click a glowing swing socket.", 2.2f, Palette.Lavender);
            return true;
        }

        var result = _missions.PlaceSwingPiece(_selected, socket.SocketId);
        if (!result.Success)
        {
            _hud.ShowHint(result.Message);
            return true;
        }
        socket.MarkPlaced();
        _onProgress?.Invoke();
        if (BuildRules.SwingComplete(_data.SwingPieces))
        {
            CompleteBuild();
            return true;
        }
        if (BuildRules.CountOf(_data.SwingPieces, new[] { "s1_rope_l", "s1_rope_r", "s2_rope_l", "s2_rope_r" }) >= 4)
            _selected = "seat";
        RefreshAvailability();
        RefreshBar();
        _hud.ShowToast(result.Message, 2f, Palette.Success);
        AudioManager.PlayWorld("build_place");
        return true;
    }

    public void SelectPiece(string type)
    {
        _selected = type;
        RefreshAvailability();
        RefreshBar();
    }

    public void RotateSelected()
    {
        if (Active) _hud.ShowBigToast("Swing parts snap into place — no rotation needed.", 2.4f, Palette.Lavender);
    }

    private void RefreshAvailability()
    {
        foreach (var socket in _sockets.Values)
        {
            if (socket.Placed) continue;
            socket.SetEnabled(true);
            bool valid = BuildRules.ValidateSwingPlacement(_selected, socket.SocketId, _data.SwingPieces, out _);
            socket.SetValid(valid);
        }
    }

    private void RefreshBar()
    {
        if (!Active) return;
        int ropes = BuildRules.CountOf(_data.SwingPieces, new[] { "s1_rope_l", "s1_rope_r", "s2_rope_l", "s2_rope_r" });
        int seats = BuildRules.CountOf(_data.SwingPieces, new[] { "s1_seat", "s2_seat" });
        var options = new List<BuildBarOption>
        {
            new()
            {
                Label = "Rope",
                Detail = $"{ropes}/4",
                Icon = "rope",
                Selected = _selected == "rope",
                Enabled = ropes < 4,
                Action = () => SelectPiece("rope"),
            },
            new()
            {
                Label = "Seat",
                Detail = $"{seats}/2",
                Icon = "seat",
                Selected = _selected == "seat",
                Enabled = seats < 2,
                Action = () => SelectPiece("seat"),
            },
        };
        _hud.ShowBuildBar("Fix the Swings", "Each swing needs two ropes and one seat. Click the glowing ROPE sockets first.",
            options, "4 ropes + 2 seats = 6 pieces. Wrong parts are never used up. Esc steps away.", CloseBuildMode);
    }

    private void CompleteBuild()
    {
        if (IsBuilt) return;
        Active = false;
        _buildCamera.Current = false;
        _camera.Current = true;
        _camera.Snap();
        _onBuildModeChanged?.Invoke(false);
        _hud.HideBuildBar();
        CompleteVisuals(true);
        _hud.ShowBigToast("Both swings are fixed! Press E at a seat to swing.", 4f, Palette.Success);
        _onComplete?.Invoke();
    }

    private void CompleteVisuals(bool playFeedback)
    {
        if (IsBuilt) return;
        IsBuilt = true;
        foreach (var socket in _sockets.Values)
        {
            socket.HidePlacedVisual();
            socket.HideGhost();
            socket.Visible = false;
        }
        if (_world.PlaygroundFixSign != null) _world.PlaygroundFixSign.Visible = false;
        foreach (var label in _world.BrokenSwingLabels)
            if (label != null) label.Visible = false;

        for (int i = 0; i < _world.SwingFrames.Length; i++)
        {
            Vector3 frame = _world.SwingFrames[i];
            Vector3 anchor = new(frame.X, 3.26f, frame.Z + 0.9f);
            var pivot = new Node3D { Name = $"SwingPivot{i}", Position = anchor };
            AddChild(pivot);
            Geo.Cylinder(pivot, new Vector3(-0.9f, -1.1f, 0), 0.06f, 0.06f, 2.2f, Palette.WoodDark, 6);
            Geo.Cylinder(pivot, new Vector3(0.9f, -1.1f, 0), 0.06f, 0.06f, 2.2f, Palette.WoodDark, 6);
            Geo.Box(pivot, new Vector3(0, -2.25f, 0), new Vector3(1.75f, 0.2f, 0.68f), Palette.Blue);
            Geo.Box(pivot, new Vector3(0, -2.11f, 0.24f), new Vector3(1.75f, 0.16f, 0.14f), Color.FromHtml("1C55B8"));
            _swingGroups.Add(pivot);
            _swingTimers.Add(0f);

            int captured = i;
            var station = Interactable.Create(this, new Vector3(frame.X, 0.1f, frame.Z + 0.9f),
                "E: Have a swing", () =>
                {
                    _swingTimers[captured] = 5.5f;
                    _hud.ShowBigToast("Wheee! Swing, swing!", 2f, Palette.Yellow);
                }, null, true, 3.6f, $"SwingStation{i}");
            _swingInteractables.Add(station);
        }
        if (playFeedback) { }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = 0; i < _swingGroups.Count; i++)
        {
            if (_swingTimers[i] > 0f)
            {
                _swingTimers[i] -= dt;
                float angle = Mathf.Sin(Time.GetTicksMsec() / 260f) * 0.42f;
                _swingGroups[i].Rotation = new Vector3(angle, 0, 0);
            }
            else
            {
                _swingGroups[i].Rotation = new Vector3(
                    Mathf.Lerp(_swingGroups[i].Rotation.X, 0f, 1f - Mathf.Exp(-dt * 4f)), 0, 0);
            }
        }
    }
}
