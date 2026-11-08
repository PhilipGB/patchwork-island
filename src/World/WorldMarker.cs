using Godot;

namespace PatchworkIsland;

/// <summary>A friendly floating icon/beam marking the current objective.</summary>
public partial class WorldMarker : Node3D
{
    private Sprite3D _icon;
    private MeshInstance3D _beam;
    private MeshInstance3D _baseRing;
    private StandardMaterial3D _beamMaterial;
    private float _time;
    private bool _reducedMotion;
    private string _lastIconName = "";
    private Color _lastColor = new(-1, -1, -1);
    private Vector3 _lastPosition = new(float.NaN, float.NaN, float.NaN);

    public void SetReducedMotion(bool reduced) => _reducedMotion = reduced;

    public override void _Ready()
    {
        Name = "WorldMarker";
        _beam = Geo.Cylinder(this, new Vector3(0, 1.8f, 0), 0.32f, 0.52f, 3.6f, Palette.Yellow, 8,
            null, true, "MarkerBeam");
        _beamMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.85f, 0.25f, 0.24f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = Palette.Yellow,
            EmissionEnergyMultiplier = 0.8f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
        };
        _beam.MaterialOverride = _beamMaterial;
        _baseRing = Geo.Disc(this, new Vector3(0, 0.08f, 0), 1.55f, Palette.Yellow, 0.45f, true);
        _icon = Geo.IconSprite(this, new Vector3(0, 4.4f, 0), "waypoint", 0.032f, true);
        Visible = false;
        SetProcess(true);
    }

    public void ShowAt(Vector3 worldPosition, Color color, string iconName)
    {
        var target = new Vector3(worldPosition.X, 0.05f, worldPosition.Z);
        if (Visible && target == _lastPosition && color == _lastColor && iconName == _lastIconName)
            return;
        _lastPosition = target;
        _lastColor = color;
        _lastIconName = iconName;
        GlobalPosition = target;
        Visible = true;
        if (_icon != null)
        {
            var texture = Geo.IconTexture(iconName);
            if (texture != null && _icon.Texture != texture) _icon.Texture = texture;
            _icon.Modulate = color.Lightened(0.2f);
        }
        if (_beamMaterial != null)
        {
            _beamMaterial.AlbedoColor = new Color(color.R, color.G, color.B, 0.22f);
            _beamMaterial.Emission = color;
        }
        _baseRing.MaterialOverride = Geo.Mat(color, true, 0.5f);
    }

    public void HideMarker() => Visible = false;

    public override void _Process(double delta)
    {
        if (!Visible) return;
        if (_reducedMotion)
        {
            if (_icon != null)
            {
                _icon.Position = new Vector3(0, 4.4f, 0);
                _icon.Rotation = Vector3.Zero;
            }
            if (_baseRing != null) _baseRing.Scale = Vector3.One;
            return;
        }

        _time += (float)delta;
        if (_icon != null)
        {
            _icon.Position = new Vector3(0, 4.4f + Mathf.Sin(_time * 2.2f) * 0.16f, 0);
            _icon.Rotation = new Vector3(0, _time * 0.6f, 0);
        }
        if (_baseRing != null)
        {
            float s = 1f + Mathf.Sin(_time * 3f) * 0.08f;
            _baseRing.Scale = new Vector3(s, 1, s);
        }
    }
}
