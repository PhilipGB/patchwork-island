using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

public sealed class NpcOption
{
    public string Text { get; init; } = "";
    public Action Action { get; init; }
    public bool Close { get; init; } = true;
    public Color? Border { get; init; }
}

/// <summary>
/// Readable villager conversation card with the supplied portrait, name, role
/// and up to four large choices. Every action is also expressed as text so the
/// game remains fully playable with sound off.
/// </summary>
public static class NpcDialogueUi
{
    public static void Show(Hud hud, NpcDefinition definition, string message,
        IReadOnlyList<NpcOption> options, Action onClosed = null)
    {
        var body = hud.OpenModal(definition.Name, new Vector2(760, 540), Palette.Lavender, true, onClosed);

        var header = UiKit.HBox(body, 16);
        var portraitPanel = UiKit.Panel(header, new Color("F7E9C9"), Palette.Lavender, 16, 4);
        var portrait = new TextureRect
        {
            Texture = NpcPortrait.LoadDefinition(definition),
            CustomMinimumSize = new Vector2(150, 150),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        portraitPanel.AddChild(portrait);

        var text = UiKit.VBox(header, 4);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UiKit.Label(text, definition.Name, 32, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Label(text, definition.Role, 20, Palette.Blue, HorizontalAlignment.Left, false);
        var messageLabel = UiKit.Label(text, message, 21, Palette.Navy);
        messageLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        messageLabel.CustomMinimumSize = new Vector2(0, 72);

        UiKit.Separator(body);
        UiKit.Label(body, "Choose with the mouse or keyboard. Nothing here can lose your progress.", 17,
            new Color("5C6B7A"));

        var row = UiKit.HBox(body, 10);
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option.Text)) continue;
            var button = UiKit.Button(row, option.Text, 19, new Vector2(0, 50), Palette.Cream,
                option.Border ?? Palette.Blue);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Pressed += () =>
            {
                option.Action?.Invoke();
                if (option.Close) hud.CloseModal();
            };
        }

        var close = UiKit.Button(body, "Close  [Esc]", 19, new Vector2(0, 46),
            new Color("E8E1FF"), Palette.Lavender);
        close.Pressed += () => hud.CloseModal();
    }

    public static void ShowGreeting(Hud hud, NpcDefinition definition, Action onClosed)
    {
        AudioManager.PlayWorld(definition.GreetingCue, -8f);
        Show(hud, definition, definition.Intro, new List<NpcOption>
        {
            new()
            {
                Text = "Tell me about the village",
                Close = false,
                Action = () => hud.SetModalStatus(definition.VillageFact, false),
                Border = Palette.Blue,
            },
            new()
            {
                Text = "Wave goodbye",
                Action = () => AudioManager.PlayWorld("ui_cancel", -9f),
                Border = Palette.Lavender,
            },
        }, onClosed);
    }
}
