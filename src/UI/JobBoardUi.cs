using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

/// <summary>Readable job board / journal built as data-driven rows from MISSIONS.json.</summary>
public static class JobBoardUi
{
    public static void Show(Hud hud, MissionService service, ISet<string> implemented, Action<string> startMission)
    {
        var data = service.Data;
        var body = hud.OpenModal("Patchwork Jobs", new Vector2(900, 590), Palette.Blue);

        var summary = UiKit.HBox(body, 10);
        UiKit.Icon(summary, "coin", 26);
        UiKit.Label(summary, $"{data.Coins} coins", 21, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Spacer(summary);
        var active = service.ActiveMission;
        if (active != null)
        {
            UiKit.Icon(summary, IconFor(active.Id), 24);
            UiKit.Label(summary, $"Current job: {active.Title}", 19, Palette.Blue, HorizontalAlignment.Left, false);
        }
        else
        {
            UiKit.Label(summary, "Pick any job with a Start button.", 19, Palette.Success, HorizontalAlignment.Left, false);
        }

        UiKit.Separator(body);

        foreach (var mission in service.Catalog.Missions.OrderBy(m => m.Id))
        {
            AddMissionRow(body, service, mission, implemented.Contains(mission.Id), startMission);
        }

        UiKit.Label(body, "Jobs will wait for you. You cannot lose progress by choosing another job.", 17,
            new Color("5C6B7A"));
    }

    private static void AddMissionRow(VBoxContainer parent, MissionService service, MissionDefinition mission,
        bool implemented, Action<string> startMission)
    {
        var status = service.GetStatus(mission.Id);
        Color border = status switch
        {
            MissionStatus.Completed => Palette.Success,
            MissionStatus.Active => Palette.Blue,
            MissionStatus.Available => Palette.Yellow,
            _ => new Color("B9B3A8"),
        };
        Color background = status switch
        {
            MissionStatus.Completed => new Color("E5F6E8"),
            MissionStatus.Active => new Color("E7F2FF"),
            MissionStatus.Available => Palette.Cream,
            _ => new Color("F0EEE8"),
        };

        var card = UiKit.Panel(parent, background, border, 14, 3);
        card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var margin = UiKit.Margin(card, 12, 7, 12, 7);
        var row = UiKit.HBox(margin, 12);
        UiKit.Icon(row, IconFor(mission.Id), 34);

        var text = UiKit.VBox(row, 1);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var titleRow = UiKit.HBox(text, 8);
        UiKit.Label(titleRow, mission.Title, 21, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Spacer(titleRow, 0, 0);
        var reward = UiKit.HBox(titleRow, 4);
        UiKit.Icon(reward, "coin", 18);
        UiKit.Label(reward, mission.RewardCoins.ToString(), 18, Palette.Navy, HorizontalAlignment.Left, false);
        var shortLabel = UiKit.Label(text, mission.Short, 18, new Color("42526B"));
        shortLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var right = UiKit.VBox(row, 4);
        right.CustomMinimumSize = new Vector2(150, 0);
        switch (status)
        {
            case MissionStatus.Completed:
                UiKit.Label(right, "DONE", 20, Palette.Success, HorizontalAlignment.Right, false);
                break;
            case MissionStatus.Active:
                UiKit.Label(right, "CURRENT JOB", 18, Palette.Blue, HorizontalAlignment.Right, false);
                break;
            case MissionStatus.Available when implemented:
            {
                var start = UiKit.Button(right, "Start", 19, new Vector2(120, 44), new Color("FFF3C9"), Palette.Orange);
                start.Pressed += () => startMission?.Invoke(mission.Id);
                break;
            }
            case MissionStatus.Available:
                UiKit.Label(right, "COMING SOON", 16, new Color("6E747C"), HorizontalAlignment.Right, false);
                break;
            default:
                string needs = string.Join(", ", mission.Prerequisites);
                UiKit.Label(right, "Finish " + needs, 16, new Color("6E747C"), HorizontalAlignment.Right, false);
                break;
        }
    }

    public static string IconFor(string missionId) => missionId switch
    {
        "M01" => "apple",
        "M02" => "wood",
        "M03" => "hammer",
        "M04" => "basket",
        "M05" => "flag",
        "M06" => "parcel",
        "M07" => "star",
        "M08" => "house",
        "M09" => "flag",
        "M10" => "house",
        _ => "star",
    };
}
