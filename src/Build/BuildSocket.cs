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
        string iconName, float ghostOffsetY = 0f, bool showLabel = true, string labelText = null)
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
        if (!string.IsNullOrEmpty(iconName))
            _ghostIcon = Geo.IconSprite(this, new Vector3(0, ghostOffsetY + ghostSize.Y * 0.5f + 0.65f, 0),
                iconName, 0.018f, true);
        if (showLabel)
        {
            string text = labelText ?? pieceType.ToUpperInvariant();
            _ghostLabel = Geo.Label(this, new Vector3(0, ghostOffsetY + ghostSize.Y * 0.5f + 1.25f, 0),
                text, 30, Color.FromHtml("0E5B2B"), true, 0.006f, 12);
        }
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
        Visible = true;
        if (_ghost != null) _ghost.Visible = false;
        if (_ghostIcon != null) _ghostIcon.Visible = false;
        if (_ghostLabel != null) _ghostLabel.Visible = false;
        if (_placedVisual != null) _placedVisual.Visible = true;
    }

    /// <summary>Lets build mode pick a placed item back up without showing its ghost.</summary>
    public void SetClickable(bool enabled)
    {
        if (!Placed) return;
        Visible = true;
        CollisionLayer = enabled ? 8u : 0u;
    }

    public void SetEnabled(bool enabled)
    {
        SocketEnabled = enabled;
        // A placed visual stays visible outside build mode (for example after a
        // mission completes); only empty ghost pads are hidden.
        Visible = enabled || Placed;
        CollisionLayer = enabled && !Placed ? 8u : 0u;
    }

    public void SetValid(bool valid)
    {
        if (_ghost == null || Placed) return;
        _ghost.MaterialOverride = Geo.GhostMat(valid ? Color.FromHtml("57E27F") : Color.FromHtml("D77970"));
        if (_ghostIcon != null)
            _ghostIcon.Modulate = valid ? Colors.White : new Color(1f, 0.55f, 0.5f, 0.8f);
    }

    public void ClearPlacement()
    {
        Placed = false;
        SocketEnabled = true;
        CollisionLayer = 8;
        if (_placedVisual != null) _placedVisual.Visible = false;
        if (_ghost != null) _ghost.Visible = true;
        if (_ghostIcon != null) _ghostIcon.Visible = true;
        if (_ghostLabel != null) _ghostLabel.Visible = true;
    }

    public void HidePlacedVisual()
    {
        if (_placedVisual != null) _placedVisual.Visible = false;
    }

    public void HideGhost()
    {
        if (_ghost != null) _ghost.Visible = false;
        if (_ghostIcon != null) _ghostIcon.Visible = false;
        if (_ghostLabel != null) _ghostLabel.Visible = false;
        CollisionLayer = 0;
    }
}
