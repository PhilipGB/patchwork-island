using System;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// A clickable snap socket on physics layer 4 (mask 8). It is deliberately not
/// collidable with the player or van; a build-mode camera ray finds it.
/// </summary>
public partial class BuildSocket : StaticBody3D
{
    public string SocketId = "";
    public string PieceType = "support";
    public bool Placed { get; private set; }
    public bool SocketEnabled { get; private set; } = true;

    private MeshInstance3D _ghost;
    private Sprite3D _ghostIcon;
    private Label3D _ghostLabel;
    private Node3D _placedVisual;
    private string _iconName;

    public void Setup(string socketId, string pieceType, Vector3 position, Vector3 ghostSize,
        string iconName, float ghostOffsetY = 0f)
    {
        Name = $"Socket_{socketId}";
        SocketId = socketId;
        PieceType = pieceType;
        Position = position;
        _iconName = iconName;
        CollisionLayer = 8;
        CollisionMask = 0;
        AddToGroup("build_socket");

        _ghost = new MeshInstance3D
        {
            Name = "Ghost",
            Mesh = new BoxMesh { Size = ghostSize },
            MaterialOverride = Geo.GhostMat(Color.FromHtml("66E58B")),
            Position = new Vector3(0, ghostOffsetY, 0),
        };
        AddChild(_ghost);
        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = ghostSize },
            Position = new Vector3(0, ghostOffsetY, 0),
        });
        _ghostIcon = Geo.IconSprite(this, new Vector3(0, ghostOffsetY + ghostSize.Y * 0.5f + 0.65f, 0),
            iconName, 0.018f, true);
        _ghostLabel = Geo.Label(this, new Vector3(0, ghostOffsetY + ghostSize.Y * 0.5f + 1.25f, 0),
            pieceType == "support" ? "SUPPORT" : "PLANK", 30,
            pieceType == "support" ? Color.FromHtml("0E5B2B") : Color.FromHtml("5B3A16"),
            true, 0.006f, 12);
    }

    public void SetPlacedVisual(Node3D visual)
    {
        _placedVisual = visual;
        AddChild(visual);
        visual.Visible = false;
    }

    public void MarkPlaced()
    {
        Placed = true;
        SocketEnabled = false;
        CollisionLayer = 0;
        if (_ghost != null) _ghost.Visible = false;
        if (_ghostIcon != null) _ghostIcon.Visible = false;
        if (_ghostLabel != null) _ghostLabel.Visible = false;
        if (_placedVisual != null) _placedVisual.Visible = true;
    }

    public void SetEnabled(bool enabled)
    {
        SocketEnabled = enabled;
        Visible = enabled;
        CollisionLayer = enabled && !Placed ? 8u : 0u;
    }

    public void SetValid(bool valid)
    {
        if (_ghost == null || Placed) return;
        _ghost.MaterialOverride = Geo.GhostMat(valid ? Color.FromHtml("57E27F") : Color.FromHtml("D77970"));
        if (_ghostIcon != null)
            _ghostIcon.Modulate = valid ? Colors.White : new Color(1f, 0.55f, 0.5f, 0.8f);
    }

    public void HideGhost()
    {
        if (_ghost != null) _ghost.Visible = false;
        if (_ghostIcon != null) _ghostIcon.Visible = false;
        if (_ghostLabel != null) _ghostLabel.Visible = false;
        CollisionLayer = 0;
    }
}
