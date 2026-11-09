using System;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// M18 repair bench. The four parts are found in the workshop tool area; the
/// car needs two tyres, then the lamp, then the battery. Wrong stages give a
/// hint without consuming a part.
/// </summary>
public static class RepairUi
{
    public static void Show(Hud hud, MissionService service, Action<MissionCompletionResult> onComplete,
        Action onChanged)
    {
        if (service.Data.ActiveMission != "M18")
        {
            hud.ShowInfo("MECHANIC", "Start Mechanic's Toolbox at the JOBS board to repair Pip's car.");
            return;
        }

        var body = hud.OpenModal("Mechanic's Toolbox", new Vector2(880, 640), Palette.Orange);
        var header = UiKit.HBox(body, 12);
        UiKit.Icon(header, "mechanic", 44);
        var intro = UiKit.Label(header,
            "Inspect Pip's car, then fit two tyres, one lamp and the battery last. " +
            "Wrong stages cost nothing; every part stays in your tool bag.",
            19, Palette.Navy);
        intro.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var stage = UiKit.Panel(body, new Color("FFF3C9"), Palette.Orange, 14, 3);
        var stageRow = UiKit.HBox(stage, 18);
        var tyres = UiKit.Label(stageRow,
            $"Tyres {service.Data.GetMissionCounter("M18", "tyres_fitted")} / 2", 21,
            Palette.Navy, HorizontalAlignment.Left, false);
        tyres.Name = "RepairTyres";
        var lamp = UiKit.Label(stageRow,
            $"Lamp {(service.Data.GetMissionCounter("M18", "lamp_fitted") > 0 ? "fitted" : "not fitted")}",
            21, Palette.Navy, HorizontalAlignment.Left, false);
        lamp.Name = "RepairLamp";
        var battery = UiKit.Label(stageRow,
            $"Battery {(service.Data.GetMissionCounter("M18", "battery_fitted") > 0 ? "fitted" : "not fitted")}",
            21, Palette.Navy, HorizontalAlignment.Left, false);
        battery.Name = "RepairBattery";
        UiKit.Spacer(stageRow);
        var bag = UiKit.Label(stageRow,
            $"Bag: {FinaleRules.HasPart(service.Data, "tyre")} tyre · " +
            $"{FinaleRules.HasPart(service.Data, "lamp")} lamp · " +
            $"{FinaleRules.HasPart(service.Data, "battery")} battery",
            18, Palette.Blue, HorizontalAlignment.Left, false);
        bag.Name = "RepairBag";

        var status = UiKit.Label(body,
            service.Data.GetMissionCounter("M18", "inspected") > 0
                ? "Inspection done. Two tyres first, then the lamp, then the battery."
                : "Start by inspecting the broken car.",
            19, Palette.Blue);
        status.Name = "RepairStatus";
        status.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var actions = UiKit.HBox(body, 10);
        var inspect = UiKit.Button(actions, "Inspect car", 20, new Vector2(0, 54),
            new Color("E7F2FF"), Palette.Blue);
        inspect.Name = "InspectCar";
        inspect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var fitTyre = UiKit.Button(actions, "Fit tyre", 20, new Vector2(0, 54),
            new Color("DFF6E3"), Palette.Success);
        fitTyre.Name = "Fit_tyre";
        fitTyre.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var fitLamp = UiKit.Button(actions, "Fit lamp", 20, new Vector2(0, 54),
            new Color("FFF3C9"), Palette.Orange);
        fitLamp.Name = "Fit_lamp";
        fitLamp.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var fitBattery = UiKit.Button(actions, "Fit battery", 20, new Vector2(0, 54),
            new Color("E8E1FF"), Palette.Lavender);
        fitBattery.Name = "Fit_battery";
        fitBattery.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        inspect.Pressed += () =>
        {
            var result = service.InspectVehicle();
            status.Text = result.Message;
            status.AddThemeColorOverride("font_color", result.Success ? Palette.Blue : Palette.Red);
            if (result.Success) AudioManager.PlayWorld("ui_confirm", -8f);
            else AudioManager.PlayUi("ui_error");
            Refresh(service, tyres, lamp, battery, bag, null);
            onChanged?.Invoke();
        };

        fitTyre.Pressed += () => Fit(service, hud, "tyre", status, tyres, lamp, battery, bag,
            onComplete, onChanged);
        fitLamp.Pressed += () => Fit(service, hud, "lamp", status, tyres, lamp, battery, bag,
            onComplete, onChanged);
        fitBattery.Pressed += () => Fit(service, hud, "battery", status, tyres, lamp, battery, bag,
            onComplete, onChanged);

        var close = UiKit.Button(body, "Step away", 19, new Vector2(0, 50),
            Palette.Cream, Palette.Stone);
        close.Name = "RepairClose";
        close.Pressed += () => hud.CloseModal();

        UiKit.Label(body,
            "The tool area is east of the workshop. Four visible parts: two tyres, one lamp, one battery.",
            17, new Color("5C6B7A"));
        Refresh(service, tyres, lamp, battery, bag, status);
    }

    private static void Fit(MissionService service, Hud hud, string part, Label status,
        Label tyres, Label lamp, Label battery, Label bag,
        Action<MissionCompletionResult> onComplete, Action onChanged)
    {
        var result = service.FitRepairPart(part);
        if (!result.Success)
        {
            status.Text = result.Message;
            status.AddThemeColorOverride("font_color", Palette.Red);
            AudioManager.PlayUi("ui_error");
            Refresh(service, tyres, lamp, battery, bag, null);
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
        AudioManager.PlayWorld("build_place", -6f);
        Refresh(service, tyres, lamp, battery, bag, null);
        onChanged?.Invoke();
    }

    private static void Refresh(MissionService service, Label tyres, Label lamp, Label battery,
        Label bag, Label status)
    {
        var data = service.Data;
        tyres.Text = $"Tyres {data.GetMissionCounter("M18", "tyres_fitted")} / 2";
        lamp.Text = $"Lamp {(data.GetMissionCounter("M18", "lamp_fitted") > 0 ? "fitted" : "not fitted")}";
        battery.Text = $"Battery {(data.GetMissionCounter("M18", "battery_fitted") > 0 ? "fitted" : "not fitted")}";
        bag.Text = $"Bag: {FinaleRules.HasPart(data, "tyre")} tyre · " +
                   $"{FinaleRules.HasPart(data, "lamp")} lamp · " +
                   $"{FinaleRules.HasPart(data, "battery")} battery";
        if (status != null && data.GetMissionCounter("M18", "inspected") > 0 &&
            data.GetMissionCounter("M18", "tyres_fitted") == 0 &&
            data.GetMissionCounter("M18", "lamp_fitted") == 0)
        {
            status.Text = "Inspection done. Two tyres first, then the lamp, then the battery.";
            status.AddThemeColorOverride("font_color", Palette.Blue);
        }
    }
}
