using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// M03 construction. Three supports appear first, then four planks. Clicking a
/// correct glowing socket places the selected part; invalid slots never consume
/// anything. Completing all seven creates the physical bridge collider.
/// </summary>
public partial class BridgeBuilder : Node3D, IBuildMode
{
    public bool Active { get; private set; }
    public bool IsBuilt { get; private set; }

    private GameData _data;
    private MissionService _missions;
    private CameraRig _camera;
    private Hud _hud;
    private WorldBuilder _world;
    private Action _onBridgeComplete;
    private Action<bool> _onBuildModeChanged;
    private Action _onProgressChanged;
    private readonly Dictionary<string, BuildSocket> _sockets = new();
    private readonly Dictionary<string, Node3D> _placedVisuals = new();
    private string _selectedType = "support";
    private Camera3D _buildCamera;

    public void Initialize(GameData data, MissionService missions, CameraRig camera, Hud hud,
        WorldBuilder world, Action onBridgeComplete, Action<bool> onBuildModeChanged,
        Action onProgressChanged)
    {
        _data = data;
        _missions = missions;
        _camera = camera;
        _hud = hud;
        _world = world;
        _onBridgeComplete = onBridgeComplete;
        _onBuildModeChanged = onBuildModeChanged;
        _onProgressChanged = onProgressChanged;
        Name = "BridgeBuilder";
        BuildSockets();
        RestorePlaced();
        RefreshSocketAvailability();

        _buildCamera = new Camera3D { Name = "BridgeBuildCamera", Fov = 56, Current = false };
        AddChild(_buildCamera);
        _buildCamera.GlobalPosition = new Vector3(45f, 9.5f, 14.5f);
        _buildCamera.LookAt(new Vector3(45f, 0f, 0f), Vector3.Up);
    }

    private void BuildSockets()
    {
        for (int i = 0; i < 3; i++)
        {
            float x = 42f + i * 3f;
            var socket = new BuildSocket();
            AddChild(socket);
            socket.Setup($"support_{i}", "support", new Vector3(x, -0.55f, 0),
                new Vector3(1.5f, 1.5f, 1.5f), "stone", 1.05f);
            BuildSupportVisual(socket, i);
            _sockets[socket.SocketId] = socket;
        }

        for (int i = 0; i < 4; i++)
        {
            float z = -3f + i * 2f;
            var socket = new BuildSocket();
            AddChild(socket);
            socket.Setup($"plank_{i}", "plank", new Vector3(45f, 0.0f, z),
                new Vector3(10.9f, 0.18f, 1.8f), "wood");
            BuildPlankVisual(socket);
            _sockets[socket.SocketId] = socket;
        }
    }

    private void BuildSupportVisual(BuildSocket socket, int index)
    {
        var root = new Node3D { Name = "SupportVisual" };
        // Stone pier extending under the timber support.
        Geo.Box(root, new Vector3(0, -0.85f, 0), new Vector3(1.7f, 1.3f, 1.7f), Palette.Stone);
        Geo.Box(root, new Vector3(0, 0.0f, 0), new Vector3(1.35f, 1.2f, 1.35f), Palette.WoodDark);
        Geo.Box(root, new Vector3(0, 0.58f, 0), new Vector3(1.55f, 0.18f, 1.55f), Palette.Wood);
        socket.SetPlacedVisual(root);
        _placedVisuals[socket.SocketId] = root;
    }

    private void BuildPlankVisual(BuildSocket socket)
    {
        var root = new Node3D { Name = "PlankVisual" };
        Geo.Box(root, new Vector3(0, -0.075f, 0), new Vector3(10.9f, 0.18f, 1.8f), Palette.Wood);
        Geo.Box(root, new Vector3(0, 0.015f, 0), new Vector3(10.5f, 0.02f, 0.12f), Palette.WoodDark);
        Geo.Box(root, new Vector3(-4.9f, 0.0f, 0), new Vector3(0.1f, 0.22f, 1.85f), Palette.WoodDark);
        Geo.Box(root, new Vector3(4.9f, 0.0f, 0), new Vector3(0.1f, 0.22f, 1.85f), Palette.WoodDark);
        socket.SetPlacedVisual(root);
        _placedVisuals[socket.SocketId] = root;
    }

    /// <summary>Test/restore seam: rebuild placed visuals and the deck collider
    /// from the saved bridge pieces without replaying the build UI.</summary>
    public void RefreshFromData()
    {
        RestorePlaced();
        RefreshSocketAvailability();
    }

    private void RestorePlaced()
    {
        foreach (string id in _data.BridgePieces)
        {
            if (_sockets.TryGetValue(id, out var socket))
                socket.MarkPlaced();
        }
        if (BuildRules.BridgeComplete(_data.BridgePieces) || IsBuilt)
            FinishBridgeStructure(false);
    }

    public void OpenBuildMode()
    {
        if (IsBuilt || BuildRules.BridgeComplete(_data.BridgePieces))
        {
            _hud.ShowBigToast("The bridge is already built. The meadow path is open!", 3.2f, Palette.Success);
            return;
        }
        if (_data.ActiveMission != "M03")
        {
            _hud.ShowHint("Start Build the Bridge at the yellow JOBS board first.");
            return;
        }
        Active = true;
        if (_world.BridgeBarrier != null) _world.BridgeBarrier.Visible = false;
        _selectedType = BuildRules.BridgeSupportCount(_data.BridgePieces) < 3 ? "support" : "plank";
        if (_buildCamera != null) _buildCamera.Current = true;
        _onBuildModeChanged?.Invoke(true);
        _hud.SetPrompt(null);
        RefreshSocketAvailability();
        RefreshBar();
    }

    public void CloseBuildMode()
    {
        if (!Active) return;
        Active = false;
        if (_buildCamera != null) _buildCamera.Current = false;
        if (_camera != null) { _camera.Current = true; _camera.Snap(); }
        if (!IsBuilt && _world.BridgeBarrier != null) _world.BridgeBarrier.Visible = true;
        _onBuildModeChanged?.Invoke(false);
        _hud.HideBuildBar();
    }

    public bool HandleWorldClick(Vector2 screenPosition)
    {
        if (!Active) return false;
        Camera3D rayCamera = _buildCamera != null && _buildCamera.Current ? _buildCamera : _camera;
        Vector3 origin = rayCamera.ProjectRayOrigin(screenPosition);
        Vector3 normal = rayCamera.ProjectRayNormal(screenPosition);
        using var query = PhysicsRayQueryParameters3D.Create(origin, origin + normal * 80f, 8u, null);
        query.CollideWithAreas = false;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
        {
            _hud.ShowBigToast("Click one of the glowing bridge sockets.", 2.2f, Palette.Lavender);
            return true;
        }

        var collider = hit["collider"].AsGodotObject();
        if (collider is not BuildSocket socket)
        {
            _hud.ShowBigToast("Click one of the glowing bridge sockets.", 2.2f, Palette.Lavender);
            return true;
        }

        var result = _missions.PlaceBridgePiece(_selectedType, socket.SocketId);
        if (!result.Success)
        {
            _hud.ShowHint(result.Message);
            return true;
        }

        socket.MarkPlaced();
        if (BuildRules.BridgeSupportCount(_data.BridgePieces) >= 3 &&
            BuildRules.BridgePlankCount(_data.BridgePieces) < 4)
        {
            _selectedType = "plank";
        }
        RefreshSocketAvailability();
        RefreshBar();
        _onProgressChanged?.Invoke();
        _hud.ShowToast(result.Message, 2.2f, Palette.Success);
        AudioManager.PlayWorld("build_place");

        if (BuildRules.BridgeComplete(_data.BridgePieces))
            CompleteBridge();
        return true;
    }

    public void SelectPiece(string type)
    {
        if (type == "plank" && BuildRules.BridgeSupportCount(_data.BridgePieces) < 3)
        {
            _hud.ShowHint("Place the three supports first.");
            return;
        }
        if (type == "support" && BuildRules.BridgeSupportCount(_data.BridgePieces) >= 3)
            return;
        _selectedType = type;
        RefreshSocketAvailability();
        RefreshBar();
    }

    public void RotateSelected()
    {
        if (!Active) return;
        _hud.ShowBigToast("Bridge pieces snap in automatically — no rotation needed.", 2.4f, Palette.Lavender);
    }

    private void RefreshSocketAvailability()
    {
        int supports = BuildRules.BridgeSupportCount(_data.BridgePieces);
        int planks = BuildRules.BridgePlankCount(_data.BridgePieces);
        foreach (var socket in _sockets.Values)
        {
            if (socket.Placed) continue;
            if (socket.PieceType == "support")
            {
                socket.SetEnabled(supports < 3);
                socket.SetValid(_selectedType == "support");
            }
            else
            {
                socket.SetEnabled(supports >= 3 && planks < 4);
                socket.SetValid(_selectedType == "plank");
            }
        }
    }

    private void RefreshBar()
    {
        if (!Active) return;
        int supports = BuildRules.BridgeSupportCount(_data.BridgePieces);
        int planks = BuildRules.BridgePlankCount(_data.BridgePieces);
        string instruction = supports < 3
            ? "Click the three glowing square sockets under the crossing to place the supports."
            : "Now click the four long glowing sockets to lay the planks across the water.";
        var options = new List<BuildBarOption>
        {
            new()
            {
                Label = "Support",
                Detail = $"{supports}/3",
                Icon = "stone",
                Selected = _selectedType == "support",
                Enabled = supports < 3,
                Action = () => SelectPiece("support"),
            },
            new()
            {
                Label = "Plank",
                Detail = $"{planks}/4",
                Icon = "wood",
                Selected = _selectedType == "plank",
                Enabled = supports >= 3 && planks < 4,
                Action = () => SelectPiece("plank"),
            },
        };
        _hud.ShowBuildBar("Build the Bridge", instruction, options,
            "Click a glowing socket. Wrong parts are never used up. Press Esc to step away.", CloseBuildMode);
    }

    private void CompleteBridge()
    {
        if (IsBuilt) return;
        Active = false;
        if (_buildCamera != null) _buildCamera.Current = false;
        if (_camera != null) { _camera.Current = true; _camera.Snap(); }
        _hud.HideBuildBar();
        _onBuildModeChanged?.Invoke(false);
        FinishBridgeStructure(true);
        _hud.ShowBigToast("The last plank is in place. The bridge is open!", 3.5f, Palette.Success);
        _onBridgeComplete?.Invoke();
    }

    private void FinishBridgeStructure(bool playFeedback)
    {
        if (IsBuilt) return;
        IsBuilt = true;
        foreach (var socket in _sockets.Values)
        {
            socket.MarkPlaced();
            socket.HideGhost();
        }
        if (_world.BridgeBarrier != null)
            _world.BridgeBarrier.Visible = false;

        // Dark underlay closes the 20 cm gaps between planks so the grass or
        // water underneath cannot read as neon seams.
        Geo.Box(this, new Vector3(45f, -0.035f, 0f),
            new Vector3(10.9f, 0.08f, 8.2f), Palette.WoodDark, null, false, "BridgeDeckUnderlay");

        // Real walk/drive collider flush with the road.
        var body = new StaticBody3D { Name = "BridgeDeckCollider", CollisionLayer = 1, CollisionMask = 0 };
        body.Position = new Vector3(45f, -0.15f, 0f);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(11.4f, 0.3f, 8.6f) } });
        AddChild(body);

        // Safe side rails with colliders. They stop at the water span (x=49)
        // so the road entrance is visibly open and the last rail/posts cannot
        // read as a barrier across the bridge exit.
        for (int side = -1; side <= 1; side += 2)
        {
            float z = side * 4.35f;
            for (int i = 0; i < 5; i++)
            {
                float x = 40.5f + i * 2.1f;
                Geo.Box(this, new Vector3(x, 0.5f, z), new Vector3(0.16f, 1.0f, 0.16f), Palette.WoodDark);
            }
            Geo.Box(this, new Vector3(44.3f, 1.0f, z), new Vector3(9.4f, 0.15f, 0.16f), Palette.Wood);
            Geo.Box(this, new Vector3(44.3f, 0.62f, z), new Vector3(9.4f, 0.12f, 0.16f), Palette.WoodDark);
            var rail = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0, Position = new Vector3(44.3f, 0.5f, z) };
            rail.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(9.4f, 0.85f, 0.18f) } });
            AddChild(rail);
        }

        if (playFeedback)
        {
            // The mission completion panel is raised by Main after the world change.
        }
    }
}
