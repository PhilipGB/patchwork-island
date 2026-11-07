using Godot;

namespace PatchworkIsland;

/// <summary>Consistent child-friendly UI building blocks: cream cards, thick
/// coloured borders, large text and 52px buttons.</summary>
public static class UiKit
{
    public static StyleBoxFlat Style(Color bg, Color border, int radius = 18, int borderWidth = 4,
        int padH = 20, int padV = 14)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = padH,
            ContentMarginRight = padH,
            ContentMarginTop = padV,
            ContentMarginBottom = padV,
        };
    }

    public static PanelContainer Panel(Control parent, Color? bg = null, Color? border = null,
        int radius = 18, int borderWidth = 4)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel",
            Style(bg ?? Palette.Cream, border ?? Palette.Blue, radius, borderWidth));
        parent?.AddChild(panel);
        return panel;
    }

    public static Label Label(Node parent, string text, int size = 22, Color? color = null,
        HorizontalAlignment align = HorizontalAlignment.Left, bool autowrap = true)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = align,
            AutowrapMode = autowrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Palette.Navy);
        parent?.AddChild(label);
        return label;
    }

    public static Button Button(Node parent, string text, int size = 24, Vector2? min = null,
        Color? bg = null, Color? border = null)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = min ?? new Vector2(0, 52),
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeColorOverride("font_color", Palette.Navy);
        button.AddThemeColorOverride("font_hover_color", Palette.Navy);
        button.AddThemeColorOverride("font_pressed_color", Palette.White);
        button.AddThemeColorOverride("font_focus_color", Palette.Navy);
        button.AddThemeColorOverride("font_disabled_color", new Color(0.42f, 0.45f, 0.5f));
        button.AddThemeStyleboxOverride("normal", Style(bg ?? Palette.Cream, border ?? Palette.Blue, 14, 3, 18, 8));
        button.AddThemeStyleboxOverride("hover", Style(new Color("E7F2FF"), border ?? Palette.Blue, 14, 4, 18, 8));
        button.AddThemeStyleboxOverride("pressed", Style(border ?? Palette.Blue, border ?? Palette.Blue, 14, 3, 18, 8));
        button.AddThemeStyleboxOverride("focus", Style(new Color("FFF3C9"), Palette.Yellow, 14, 5, 18, 8));
        button.AddThemeStyleboxOverride("disabled", Style(new Color("E4E0D8"), new Color("B9B3A8"), 14, 3, 18, 8));
        parent?.AddChild(button);
        return button;
    }

    public static TextureRect Icon(Node parent, string name, int size = 32,
        TextureRect.StretchModeEnum stretch = TextureRect.StretchModeEnum.KeepAspectCentered)
    {
        var textureRect = new TextureRect
        {
            Texture = Geo.IconTexture(name),
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = stretch,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        parent?.AddChild(textureRect);
        return textureRect;
    }

    public static void Anchor(Control control, float left, float top, float right, float bottom,
        float offsetLeft, float offsetTop, float offsetRight, float offsetBottom)
    {
        control.AnchorLeft = left;
        control.AnchorTop = top;
        control.AnchorRight = right;
        control.AnchorBottom = bottom;
        control.OffsetLeft = offsetLeft;
        control.OffsetTop = offsetTop;
        control.OffsetRight = offsetRight;
        control.OffsetBottom = offsetBottom;
    }

    public static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    public static ColorRect Shade(Node parent, float alpha = 0.55f)
    {
        var shade = new ColorRect
        {
            Color = new Color(0.08f, 0.12f, 0.18f, alpha),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        parent?.AddChild(shade);
        return shade;
    }

    public static HBoxContainer HBox(Node parent, int separation = 12)
    {
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", separation);
        parent?.AddChild(box);
        return box;
    }

    public static VBoxContainer VBox(Node parent, int separation = 12)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", separation);
        parent?.AddChild(box);
        return box;
    }

    public static HSeparator Separator(Node parent)
    {
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 8);
        parent?.AddChild(sep);
        return sep;
    }

    public static MarginContainer Margin(Node parent, int left, int top, int right, int bottom)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
        parent?.AddChild(margin);
        return margin;
    }

    public static Control Spacer(Node parent, float minWidth = 0, float minHeight = 0)
    {
        var spacer = new Control
        {
            CustomMinimumSize = new Vector2(minWidth, minHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        parent?.AddChild(spacer);
        return spacer;
    }

    public static string KeyPrompt(string key, string action) => $"{key}   {action}";
}
