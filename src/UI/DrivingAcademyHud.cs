using Godot;

namespace PatchworkIsland;

/// <summary>Small, persistent lesson HUD. Large text, current step and speed.</summary>
public partial class DrivingAcademyHud : CanvasLayer
{
    private Control _root;
    private PanelContainer _panel;
    private Label _title;
    private Label _step;
    private Label _hint;
    private Label _target;
    private Label _speed;
    private Label _light;

    public override void _Ready()
    {
        Layer = 19;
        ProcessMode = ProcessModeEnum.Pausable;
        _root = new Control { Name = "AcademyHudRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _panel = UiKit.Panel(_root, Palette.Cream, Palette.Yellow, 20, 5);
        // Top-right placement: a centred panel hides the traffic-signal head
        // from the learner's view at the stop line, which is exactly the view
        // the signal lessons depend on.
        UiKit.Anchor(_panel, 1, 0, 1, 0, -735, 92, -15, 250);
        _panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        var box = UiKit.VBox(_panel, 4);
        _title = UiKit.Label(box, "", 28, Palette.Navy, HorizontalAlignment.Center, false);
        _step = UiKit.Label(box, "", 19, Palette.Teal, HorizontalAlignment.Center, false);
        _hint = UiKit.Label(box, "", 20, Palette.Navy, HorizontalAlignment.Center);
        _target = UiKit.Label(box, "", 17, Palette.Teal, HorizontalAlignment.Center);
        var row = UiKit.HBox(box, 12);
        UiKit.Spacer(row);
        _speed = UiKit.Label(row, "0 mph", 24, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Spacer(row);
        _light = UiKit.Label(row, "", 19, Palette.Navy, HorizontalAlignment.Right, false);
        Visible = false;
    }

    public void ShowStage(string title, int step, int steps, string hint, float speed,
        AcademyLight? light, string target = null)
    {
        Visible = true;
        _title.Text = title;
        _step.Text = steps > 0 ? $"Step {step} of {steps}" : "Practice";
        _hint.Text = hint;
        float mph = speed / 0.44704f;
        _speed.Text = $"{mph:0} mph";
        _speed.AddThemeColorOverride("font_color",
            DrivingAcademyRules.IsSpeeding(speed) ? Palette.Red : Palette.Navy);
        _light.Text = light switch
        {
            AcademyLight.Red => "RED — STOP",
            AcademyLight.RedAmber => "RED + AMBER — STOP",
            AcademyLight.Green => "GREEN",
            AcademyLight.Amber => "AMBER — STOP",
            _ => "",
        };
        _light.AddThemeColorOverride("font_color", light switch
        {
            AcademyLight.Green => Palette.Success,
            null => Palette.Navy,
            _ => Palette.Red,
        });
        _light.Visible = light.HasValue;
        _target.Text = target ?? "";
        _target.Visible = !string.IsNullOrEmpty(target);
    }

    public string GuidanceText => _target?.Text ?? "";

    public void HideStage() => Visible = false;
}
