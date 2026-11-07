using Godot;

namespace PatchworkIsland;

/// <summary>
/// P0 boot scene: a minimal but real 3D scene with camera, sun, ground, avatar
/// placeholder and a working pause / quit flow. P1 replaces the placeholder
/// world with the full village.
/// </summary>
public partial class Main : Node3D
{
    private Control _pauseLayer;
    private bool _paused;

    public override void _Ready()
    {
        BuildEnvironment();
        BuildPlaceholderWorld();
        BuildPauseOverlay();
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void BuildEnvironment()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("75C9FF"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("d8f0ff"),
            AmbientLightEnergy = 0.85f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        AddChild(new WorldEnvironment { Environment = env });

        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-52, -35, 0),
            LightColor = new Color("fff4dc"),
            LightEnergy = 1.15f,
            ShadowEnabled = true,
        };
        AddChild(sun);

        var camera = new Camera3D
        {
            Position = new Vector3(0, 5.5f, 11),
            Current = true,
            Fov = 60,
        };
        AddChild(camera);
        camera.LookAt(new Vector3(0, 1, 0));
    }

    private void BuildPlaceholderWorld()
    {
        var ground = new StaticBody3D { Position = new Vector3(0, -0.5f, 0) };
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(40, 1, 40) },
            MaterialOverride = FlatMaterial(new Color("86CF55")),
        };
        ground.AddChild(mesh);
        ground.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(40, 1, 40) },
        });
        AddChild(ground);

        var avatar = new Node3D { Position = new Vector3(0, 1, 0) };
        avatar.AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.45f, Height = 1.8f },
            Position = new Vector3(0, 0.9f, 0),
            MaterialOverride = FlatMaterial(new Color("2674EC")),
        });
        avatar.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.42f, Height = 0.85f },
            Position = new Vector3(0, 1.95f, 0),
            MaterialOverride = FlatMaterial(new Color("F5C99B")),
        });
        avatar.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.18f },
            Position = new Vector3(0, 2.34f, 0),
            MaterialOverride = FlatMaterial(new Color("F45A54")),
        });
        AddChild(avatar);

        var label = new Label3D
        {
            Text = "Patchwork Island\nP0 boot check",
            Position = new Vector3(0, 3.4f, 0),
            FontSize = 64,
            OutlineSize = 10,
            Modulate = new Color("27364B"),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        };
        AddChild(label);
    }

    private static StandardMaterial3D FlatMaterial(Color color) => new()
    {
        AlbedoColor = color,
        Roughness = 1.0f,
        Metallic = 0.0f,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
    };

    private void BuildPauseOverlay()
    {
        var layer = new CanvasLayer();
        _pauseLayer = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _pauseLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_pauseLayer);
        AddChild(layer);

        var shade = new ColorRect { Color = new Color(0.08f, 0.12f, 0.18f, 0.62f) };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _pauseLayer.AddChild(shade);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(420, 300),
        };
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.Position = new Vector2(-210, -150);
        var style = new StyleBoxFlat
        {
            BgColor = new Color("FFF8EC"),
            BorderColor = new Color("2674EC"),
            BorderWidthLeft = 6, BorderWidthRight = 6, BorderWidthTop = 6, BorderWidthBottom = 6,
            CornerRadiusTopLeft = 24, CornerRadiusTopRight = 24,
            CornerRadiusBottomLeft = 24, CornerRadiusBottomRight = 24,
        };
        panel.AddThemeStyleboxOverride("panel", style);
        _pauseLayer.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 18);
        panel.AddChild(box);
        var title = new Label { Text = "Paused", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 40);
        title.AddThemeColorOverride("font_color", new Color("27364B"));
        box.AddChild(title);

        var resume = MakeButton("Keep Playing  (Esc)");
        resume.Pressed += TogglePause;
        box.AddChild(resume);

        var quit = MakeButton("Quit Game");
        quit.Pressed += () => GetTree().Quit();
        box.AddChild(quit);
    }

    private static Button MakeButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 56),
        };
        button.AddThemeFontSizeOverride("font_size", 24);
        return button;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pause"))
        {
            TogglePause();
            GetViewport().SetInputAsHandled();
        }
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pauseLayer.Visible = _paused;
        GetTree().Paused = _paused;
        Input.MouseMode = _paused ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }
}
