using System;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// M15 garden pad. Six pads repeat RED, BLUE in pairs. Choosing a colour is
/// reversible and seed stock is unlimited; the player sees the whole pattern.
/// </summary>
public static class GardenPadUi
{
    public static void Show(Hud hud, CommunityMissionTasks service, int padIndex,
        Action<MissionCompletionResult> onComplete, Action onChanged)
    {
        int shown = padIndex + 1;
        var body = hud.OpenModal($"Garden Pad {shown}", new Vector2(760, 520), Palette.GrassDark);
        var header = UiKit.HBox(body, 12);
        UiKit.Icon(header, "garden", 44);
        var intro = UiKit.Label(header,
            "The garden repeats RED, BLUE, RED, BLUE, RED, BLUE. " +
            "Choose a flower for this pad. You can change it as often as you like.",
            20, Palette.Navy);
        intro.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var status = UiKit.Label(body, "", 19, Palette.Blue);
        status.Name = "GardenPadStatus";

        var pattern = UiKit.HBox(body, 8);
        for (int i = 0; i < ChapterTwoRules.GardenPattern.Length; i++)
        {
            string expected = ChapterTwoRules.GardenPattern[i];
            int code = service.Data.GetMissionCounter("M15", $"pad{i}");
            string current = ChapterTwoRules.GardenColor(code);
            Color color = current == "red" ? Palette.Red : current == "blue" ? Palette.Blue : Palette.Stone;
            var label = UiKit.Label(pattern,
                i == padIndex ? $"[{i + 1}] {current.ToUpperInvariant()}" : $"{i + 1} {current.ToUpperInvariant()}",
                17, color, HorizontalAlignment.Center, false);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }

        var row = UiKit.HBox(body, 12);
        var red = UiKit.Button(row, "RED flower", 21, new Vector2(0, 60),
            new Color("FFE0D6"), Palette.Red);
        red.Name = "GardenRed";
        red.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var blue = UiKit.Button(row, "BLUE flower", 21, new Vector2(0, 60),
            new Color("E7F2FF"), Palette.Blue);
        blue.Name = "GardenBlue";
        blue.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var clear = UiKit.Button(row, "Clear pad", 18, new Vector2(0, 60),
            Palette.Cream, Palette.Stone);
        clear.Name = "GardenClear";
        clear.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        red.Pressed += () => Plant(hud, service, padIndex, "red", status, pattern, onComplete, onChanged);
        blue.Pressed += () => Plant(hud, service, padIndex, "blue", status, pattern, onComplete, onChanged);
        clear.Pressed += () => Plant(hud, service, padIndex, "empty", status, pattern, onComplete, onChanged);

        var close = UiKit.Button(body, "Done  [Esc]", 18, new Vector2(0, 46),
            new Color("E8E1FF"), Palette.Lavender);
        close.Name = "GardenDone";
        close.Pressed += () => hud.CloseModal();
        UiKit.Label(body,
            "Six beds make three RED-BLUE pairs. The first two flowers show the pattern.",
            17, new Color("5C6B7A"));
    }

    private static void Plant(Hud hud, CommunityMissionTasks service, int padIndex, string color,
        Label status, HBoxContainer pattern, Action<MissionCompletionResult> onComplete, Action onChanged)
    {
        var result = service.PlantGarden(padIndex, color);
        if (!result.Success)
        {
            status.Text = result.Message;
            status.AddThemeColorOverride("font_color", Palette.Red);
            AudioManager.PlayUi("ui_error");
            return;
        }
        if (result.Completion != null)
        {
            hud.CloseModal();
            onComplete?.Invoke(result.Completion);
            return;
        }
        status.Text = result.Message;
        status.AddThemeColorOverride("font_color",
            ChapterTwoRules.GardenPadMatches(padIndex, color) ? Palette.Success : Palette.Orange);
        AudioManager.PlayWorld("garden_plant", -6f);
        onChanged?.Invoke();
        // Refresh the six labels in place.
        var labels = pattern.GetChildren();
        for (int i = 0; i < labels.Count && i < ChapterTwoRules.GardenPattern.Length; i++)
        {
            if (labels[i] is not Label label) continue;
            int code = service.Data.GetMissionCounter("M15", $"pad{i}");
            string current = ChapterTwoRules.GardenColor(code);
            label.Text = i == padIndex
                ? $"[{i + 1}] {current.ToUpperInvariant()}"
                : $"{i + 1} {current.ToUpperInvariant()}";
            label.AddThemeColorOverride("font_color",
                current == "red" ? Palette.Red : current == "blue" ? Palette.Blue : Palette.Stone);
        }
    }
}
