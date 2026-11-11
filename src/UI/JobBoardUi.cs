using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

/// <summary>Readable job board / journal built as data-driven rows from MISSIONS.json.</summary>
public static class JobBoardUi
{
    public static void Show(Hud hud, MissionService service, ISet<string> implemented, Action<string> startMission,
        Action practiceRace = null, bool showChapterTwo = false)
    {
        var data = service.Data;
        var body = hud.OpenModal("Patchwork Jobs", new Vector2(900, 620), Palette.Blue);

        var tabs = UiKit.HBox(body, 8);
        var chapterOne = UiKit.Button(tabs, "Village jobs  (1–10)", 19, new Vector2(0, 44),
            showChapterTwo ? Palette.Cream : new Color("FFF3C9"),
            showChapterTwo ? Palette.Stone : Palette.Orange);
        chapterOne.Name = "ChapterOneTab";
        chapterOne.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        chapterOne.Pressed += () =>
        {
            if (showChapterTwo)
                Show(hud, service, implemented, startMission, practiceRace, false);
        };
        var chapterTwo = UiKit.Button(tabs, "New adventures  (11–21)", 19, new Vector2(0, 44),
            showChapterTwo ? new Color("FFF3C9") : Palette.Cream,
            showChapterTwo ? Palette.Orange : Palette.Lavender);
        chapterTwo.Name = "ChapterTwoTab";
        chapterTwo.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        chapterTwo.Pressed += () =>
        {
            if (!showChapterTwo)
                Show(hud, service, implemented, startMission, practiceRace, true);
        };

        var summary = UiKit.HBox(body, 10);
        UiKit.Icon(summary, "coin", 26);
        UiKit.Label(summary, $"{data.Coins} coins", 21, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Spacer(summary);
        var active = service.ActiveMission;
        if (active != null)
        {
            UiKit.Icon(summary, IconFor(active.Id), 24);
            UiKit.Label(summary, $"Current job: {active.Title}", 19, Palette.Blue,
                HorizontalAlignment.Left, false);
        }
        else if (data.IsCompleted("M20"))
        {
            UiKit.Label(summary,
                data.CityRoadOpen
                    ? "All 20 village jobs complete — the CITY ROAD is open!"
                    : "All 20 required jobs complete — Roadworks Rescue unlocks the city!",
                19, data.CityRoadOpen ? Palette.Teal : Palette.Success,
                HorizontalAlignment.Left, false);
        }
        else
        {
            UiKit.Label(summary, "Pick any available job with a Start button.", 19, Palette.Success,
                HorizontalAlignment.Left, false);
        }

        UiKit.Separator(body);

        if (showChapterTwo)
        {
            if (!MissionRules.OriginalChapterComplete(data))
            {
                var locked = UiKit.Panel(body, new Color("F0EEE8"), Palette.Stone, 14, 3);
                var lockedRow = UiKit.HBox(locked, 10);
                UiKit.Icon(lockedRow, "star", 30);
                UiKit.Label(lockedRow,
                    "New adventures unlock when all ten Village jobs are finished. " +
                    "Your current village and every completed job stay exactly as they are.",
                    19, Palette.Navy);
                return;
            }
            var note = UiKit.Panel(body, new Color("E8F4FF"), Palette.Blue, 14, 3);
            UiKit.Label(note,
                "Chapter two: meet the neighbours, help the bakery and library, repair signs " +
                "and grow a pattern garden. More jobs appear as the chapter is completed.",
                18, Palette.Navy);
        }

        if (!showChapterTwo && practiceRace != null && data.IsCompleted("M09"))
        {
            var practicePanel = UiKit.Panel(body, new Color("FFF3C9"), Palette.Orange, 14, 3);
            var practiceRow = UiKit.HBox(practicePanel, 10);
            UiKit.Icon(practiceRow, "flag", 28);
            var practiceText = UiKit.Label(practiceRow,
                "Practice race: gates 1-2-3-4, no rewards and no time limit.",
                18, Palette.Navy);
            practiceText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var practice = UiKit.Button(practiceRow, "Drive practice", 18, new Vector2(170, 44),
                Palette.Cream, Palette.Orange);
            practice.Pressed += () => practiceRace();
        }

        var missions = service.Catalog.Missions
            .Where(m => showChapterTwo ? m.Id.CompareTo("M11") >= 0 : m.Id.CompareTo("M11") < 0)
            .OrderBy(m => m.Id);
        foreach (var mission in missions)
            AddMissionRow(body, service, mission, implemented.Contains(mission.Id), startMission);

        UiKit.Label(body, showChapterTwo
                ? "Chapter-two progress is saved with the village. Wrong choices never cost an item or a coin."
                : "Jobs will wait for you. You cannot lose progress by choosing another job.",
            17, new Color("5C6B7A"));
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
                start.Name = "Start_" + mission.Id;
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
        "M11" => "neighbours",
        "M12" => "bakery",
        "M13" => "library",
        "M14" => "road_signs",
        "M15" => "garden",
        "M16" => "delivery",
        "M17" => "market_maths",
        "M18" => "mechanic",
        "M19" => "grand_rally",
        "M20" => "festival",
        "M21" => "road_signs",
        _ => "star",
    };
}
