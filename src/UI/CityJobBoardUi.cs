using System;
using Godot;

namespace PatchworkIsland;

/// <summary>Independent CITY JOBS board. It never mixes C-prefixed jobs into the village board.</summary>
public static class CityJobBoardUi
{
    public static void Show(Hud hud, CityMissionService service, Action<string> startMission,
        Action turnIn)
    {
        var data = service.Data;
        var body = hud.OpenModal("CITY JOBS", new Vector2(940, 650), Palette.Teal);

        var header = UiKit.HBox(body, 12);
        UiKit.Icon(header, "coin", 34);
        UiKit.Label(header, $"{data.Coins} coins", 25, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Spacer(header);
        UiKit.Label(header, "Patchwork City · 3 jobs", 19, Palette.Teal, HorizontalAlignment.Right, false);

        var note = UiKit.Panel(body, new Color("E7F8F5"), Palette.Teal, 14, 3);
        UiKit.Label(note,
            "This is the city board, separate from the village board. Read the job card, " +
            "follow the numbered places, and press E when you arrive. Wrong answers never cost coins.",
            19, Palette.Navy);

        var active = service.ActiveMission;
        if (active != null)
        {
            var card = UiKit.Panel(body, new Color("E7F2FF"), Palette.Blue, 16, 4);
            var row = UiKit.HBox(card, 14);
            UiKit.Icon(row, IconFor(active.Id), 40);
            var text = UiKit.VBox(row, 4);
            text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            UiKit.Label(text, active.Title, 25, Palette.Navy, HorizontalAlignment.Left, false);
            UiKit.Label(text, ProgressLine(service, active.Id), 20, new Color("42526B"));
            var right = UiKit.VBox(row, 8);
            right.CustomMinimumSize = new Vector2(190, 0);
            if (service.CanTurnIn)
            {
                var finish = UiKit.Button(right, "Finish job", 20, new Vector2(180, 48),
                    new Color("DFF6E3"), Palette.Success);
                finish.Name = "CityFinish";
                finish.Pressed += () => turnIn?.Invoke();
            }
            else
            {
                UiKit.Label(right, "IN PROGRESS", 18, Palette.Blue,
                    HorizontalAlignment.Right, false);
            }
        }
        else
        {
            UiKit.Label(body,
                "Choose one city job. Your city progress is saved separately from the village.",
                20, new Color("42526B"));
        }

        foreach (var mission in service.Catalog.Missions)
        {
            var status = service.GetStatus(mission.Id);
            Color border = status switch
            {
                CityMissionStatus.Completed => Palette.Success,
                CityMissionStatus.Active => Palette.Blue,
                _ => Palette.Teal,
            };
            Color background = status switch
            {
                CityMissionStatus.Completed => new Color("E5F6E8"),
                CityMissionStatus.Active => new Color("E7F2FF"),
                _ => Palette.Cream,
            };
            var card = UiKit.Panel(body, background, border, 14, 3);
            var row = UiKit.HBox(card, 12);
            UiKit.Icon(row, IconFor(mission.Id), 34);
            var text = UiKit.VBox(row, 2);
            text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var titleRow = UiKit.HBox(text, 8);
            UiKit.Label(titleRow, mission.Title, 22, Palette.Navy,
                HorizontalAlignment.Left, false);
            UiKit.Spacer(titleRow);
            UiKit.Icon(titleRow, "coin", 18);
            UiKit.Label(titleRow, mission.RewardCoins.ToString(), 18, Palette.Navy,
                HorizontalAlignment.Left, false);
            UiKit.Label(text, mission.Short, 18, new Color("42526B"));
            var right = UiKit.VBox(row, 4);
            right.CustomMinimumSize = new Vector2(160, 0);
            switch (status)
            {
                case CityMissionStatus.Completed:
                    UiKit.Label(right, "DONE", 20, Palette.Success,
                        HorizontalAlignment.Right, false);
                    break;
                case CityMissionStatus.Active:
                    UiKit.Label(right, "CURRENT JOB", 18, Palette.Blue,
                        HorizontalAlignment.Right, false);
                    break;
                default:
                    var start = UiKit.Button(right, active == null ? "Start" : "Switch",
                        19, new Vector2(130, 44), new Color("E7F8F5"), Palette.Teal);
                    start.Name = "CityStart_" + mission.Id;
                    var captured = mission.Id;
                    start.Pressed += () => startMission?.Invoke(captured);
                    break;
            }
        }

        UiKit.Label(body,
            "City jobs: C01 number order · C02 counting and multiplication · C03 colour and sequence.",
            17, new Color("5C6B7A"));
    }

    public static string IconFor(string id) => id switch
    {
        "C01" => "delivery",
        "C02" => "market_maths",
        "C03" => "road_signs",
        _ => "star",
    };

    public static string ProgressLine(CityMissionService service, string id)
    {
        return id switch
        {
            "C01" => service.HasCourierParcels
                ? $"Parcels delivered: {service.C01Stops} / 3 (next: {NextStop(service)})"
                : "Load 3 numbered parcels at the depot",
            "C02" => service.HasMarketAnswer
                ? "2 × 3 = 6 checked. Return to this board."
                : $"Crates collected: {service.C02Crates} / 6",
            "C03" => service.C03Cabinets >= 3
                ? "Cabinets fixed. Talk to the city engineer."
                : $"Cabinets fixed: {service.C03Cabinets} / 3 (next: red, amber, green)",
            _ => "",
        };
    }

    private static string NextStop(CityMissionService service)
    {
        int next = service.C01Stops + 1;
        return next > 3 ? "return to CITY JOBS" : next.ToString();
    }
}
