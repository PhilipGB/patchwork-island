using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// M14 roadside socket. The player reads the destination label, chooses a sign
/// and chooses an arrow direction. Wrong combinations are reversible and never
/// consume a sign.
/// </summary>
public static class SignSocketUi
{
    public static void Show(Hud hud, CommunityMissionTasks service, string socketId,
        Action<MissionCompletionResult> onComplete, Action onChanged)
    {
        var socket = ChapterTwoRules.FindSocket(socketId);
        if (socket == null) return;
        var body = hud.OpenModal($"Road Sign: {socket.Label}", new Vector2(820, 620), Palette.Orange);
        var header = UiKit.HBox(body, 12);
        UiKit.Icon(header, "road_signs", 42);
        var intro = UiKit.Label(header,
            $"Which sign belongs at the {socket.Label} socket, and which way should the arrow point? " +
            "A wrong choice can be removed and tried again.",
            19, Palette.Navy);
        intro.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var status = UiKit.Label(body, "", 19, Palette.Blue);
        status.Name = "SignSocketStatus";

        string selectedSign = ChapterTwoRules.SignIds[0];
        string selectedDirection = "north";
        var signButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        var directionButtons = new Dictionary<string, Button>(StringComparer.Ordinal);

        UiKit.Label(body, "1. Choose the destination sign", 21, Palette.Navy);
        var signRow = UiKit.HBox(body, 10);
        foreach (string sign in ChapterTwoRules.SignIds)
        {
            var button = UiKit.Button(signRow, ChapterTwoRules.SignLabel(sign), 19, new Vector2(0, 54),
                Palette.Cream, Palette.Blue);
            button.Name = $"Sign_{sign.ToUpperInvariant()}";
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            signButtons[sign] = button;
            string captured = sign;
            button.Pressed += () =>
            {
                selectedSign = captured;
                Refresh(signButtons, directionButtons, service, socketId, selectedSign, selectedDirection);
                status.Text = $"Selected {ChapterTwoRules.SignLabel(captured)}. Now choose the arrow.";
                status.AddThemeColorOverride("font_color", Palette.Blue);
            };
        }

        UiKit.Label(body, "2. Choose the arrow direction", 21, Palette.Navy);
        var directionRow = UiKit.HBox(body, 10);
        foreach (string direction in ChapterTwoRules.Directions)
        {
            var button = UiKit.Button(directionRow,
                $"{ChapterTwoRules.ArrowFor(direction)} {direction.ToUpperInvariant()}", 18,
                new Vector2(0, 52), Palette.Cream, Palette.Lavender);
            button.Name = $"Direction_{direction.ToUpperInvariant()}";
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            directionButtons[direction] = button;
            string captured = direction;
            button.Pressed += () =>
            {
                selectedDirection = captured;
                Refresh(signButtons, directionButtons, service, socketId, selectedSign, selectedDirection);
                status.Text = $"Arrow points {captured.ToUpperInvariant()}. Press Install when ready.";
                status.AddThemeColorOverride("font_color", Palette.Blue);
            };
        }

        var actions = UiKit.HBox(body, 10);
        var install = UiKit.Button(actions, "Install sign", 20, new Vector2(0, 54),
            new Color("DFF6E3"), Palette.Success);
        install.Name = "InstallSign";
        install.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var remove = UiKit.Button(actions, "Remove sign", 18, new Vector2(0, 54),
            new Color("FFE0D6"), Palette.Red);
        remove.Name = "RemoveSign";
        remove.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var close = UiKit.Button(actions, "Step away", 18, new Vector2(0, 54),
            Palette.Cream, Palette.Stone);
        close.Name = "SignStepAway";
        close.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        close.Pressed += () => hud.CloseModal();

        install.Pressed += () =>
        {
            var result = service.InstallRoadSign(socketId, selectedSign, selectedDirection);
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
            status.AddThemeColorOverride("font_color", Palette.Success);
            AudioManager.PlayWorld("build_place", -5f);
            onChanged?.Invoke();
            Refresh(signButtons, directionButtons, service, socketId, selectedSign, selectedDirection);
        };
        remove.Pressed += () =>
        {
            var result = service.RemoveRoadSign(socketId);
            status.Text = result.Message;
            status.AddThemeColorOverride("font_color", result.Success ? Palette.Orange : Palette.Red);
            if (result.Success)
            {
                AudioManager.PlayUi("ui_cancel");
                onChanged?.Invoke();
            }
            Refresh(signButtons, directionButtons, service, socketId, selectedSign, selectedDirection);
        };

        UiKit.Label(body,
            "The socket is a safe roadside marker. Signs never block the road, traffic or bridge.",
            17, new Color("5C6B7A"));
        Refresh(signButtons, directionButtons, service, socketId, selectedSign, selectedDirection);
    }

    private static void Refresh(Dictionary<string, Button> signButtons,
        Dictionary<string, Button> directionButtons, CommunityMissionTasks service, string socketId,
        string selectedSign, string selectedDirection)
    {
        bool installed = service.Data.GetMissionCounter("M14", $"sign_{socketId}") >= 1;
        foreach (var pair in signButtons)
        {
            pair.Value.Disabled = installed;
            pair.Value.Text = pair.Key == selectedSign
                ? $"▶ {ChapterTwoRules.SignLabel(pair.Key)}"
                : ChapterTwoRules.SignLabel(pair.Key);
        }
        foreach (var pair in directionButtons)
        {
            pair.Value.Disabled = installed;
            pair.Value.Text = pair.Key == selectedDirection
                ? $"▶ {ChapterTwoRules.ArrowFor(pair.Key)} {pair.Key.ToUpperInvariant()}"
                : $"{ChapterTwoRules.ArrowFor(pair.Key)} {pair.Key.ToUpperInvariant()}";
        }
    }
}
