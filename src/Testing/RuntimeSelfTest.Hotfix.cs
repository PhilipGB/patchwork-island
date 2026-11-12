using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

public partial class RuntimeSelfTest
{
    private async Task<bool> HotfixChecks()
    {
        if (HotfixPhase == 0) return await HotfixFreshChecks();
        return await HotfixResumeChecks();
    }

    private async Task<bool> HotfixFreshChecks()
    {
        Expect(!Main.Probe.Started, "hotfix test starts on the title screen");
        var started = Main.Probe.StartNewGameFromTitleConfirmed(CharacterCustomisation.Boy);
        await Wait(0.8f);
        Expect(started.Success && Main.Probe.Started, "hotfix test starts a fresh village");

        Main.Probe.PrepareMechanicJob();
        await Wait(0.4f);
        Expect(Main.Probe.MissionStatus("M18") == MissionStatus.Available,
            "M18 is available after the prepared M16/M17 jobs");
        Expect(await StartJobFromBoard("M18"), "M18 starts from the job board");

        Main.Probe.TeleportNearPickup("part_tyre_0");
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "Pickup_part_tyre_0",
            "the first tyre is the nearest E interaction");
        await PressInteract();
        Expect(Main.Probe.MissionCounter("M18", "has_tyre") == 1,
            "first tyre collected through the real pickup");
        Expect(Main.Probe.CollectedPickup("part_tyre_0"),
            "first tyre pickup ID is recorded");
        Expect(Main.Probe.RepairPickupCount == 3,
            "three remaining repair-part pickups stay in the world");

        Expect(Main.Probe.SaveNow(), "partial M18 progress saves");
        var partial = SaveService.Load(Main.Probe.SavePath, Main.Probe.Catalog);
        Expect(partial.Success, partial.Message);
        Expect(partial.Success && partial.Data.ActiveMission == "M18",
            "saved partial job is M18");
        Expect(partial.Success && partial.Data.GetMissionCounter("M18", "has_tyre") == 1,
            "saved partial tyre count is on disk");
        Expect(partial.Success && partial.Data.HasCollected("part_tyre_0"),
            "saved file records the collected tyre pickup");

        Main.Probe.DropTrafficOffIsland("town_scooter");
        await Wait(0.6f);
        Expect(Main.Probe.TrafficStopped("town_scooter"),
            "off-island traffic enters a terminal stopped state");
        Expect(Main.Probe.TrafficStopEvents("town_scooter") == 1,
            "off-island traffic logs one failure");
        Vector3 resting = Main.Probe.TrafficPosition("town_scooter");
        await Wait(0.6f);
        Expect(Main.Probe.TrafficStopEvents("town_scooter") == 1,
            "stopped traffic does not repeat its failure");
        Expect(resting.DistanceTo(Main.Probe.TrafficPosition("town_scooter")) < 0.001f,
            "stopped traffic no longer moves");

        HotfixPhase = 1;
        Main.Probe.ReloadScene();
        return true;
    }

    private async Task<bool> HotfixResumeChecks()
    {
        Expect(!Main.Probe.Started, "hotfix resume starts on the title screen");
        Expect(Main.Probe.CanContinue, "the partial M18 save offers Continue");
        Main.Probe.Continue();
        await Wait(0.5f);
        Expect(Main.Probe.Started && Main.Probe.Mode == "Walking", "Continue resumes the village");
        Expect(Main.Probe.ActiveMission == "M18", "Continue restores active M18");
        Expect(Main.Probe.MissionCounter("M18", "has_tyre") == 1,
            "Continue restores the collected tyre");
        Expect(Main.Probe.CollectedPickup("part_tyre_0"),
            "Continue restores the collected pickup ID");
        Expect(!Main.Probe.PickupPresent("part_tyre_0"),
            "the collected tyre does not respawn after relaunch");
        Expect(Main.Probe.RepairPickupCount == 3,
            "only the three uncollected parts remain after relaunch");

        foreach (string partId in new[] { "part_tyre_1", "part_lamp_0", "part_battery_0" })
        {
            Main.Probe.TeleportNearPickup(partId);
            await Wait(0.3f);
            Expect(Main.Probe.NearestInteractableName == "Pickup_" + partId,
                $"{partId} is the nearest E interaction after relaunch");
            await PressInteract();
        }
        Expect(Main.Probe.MissionCounter("M18", "has_tyre") == 2,
            "second tyre collected after relaunch");
        Expect(Main.Probe.MissionCounter("M18", "has_lamp") == 1,
            "lamp collected after relaunch");
        Expect(Main.Probe.MissionCounter("M18", "has_battery") == 1,
            "battery collected after relaunch");
        Expect(Main.Probe.RepairPickupCount == 0, "all four part pickups are gone");

        Main.Probe.TeleportNearRepairCar();
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "RepairStation", "repair bench is nearest");
        await PressInteract();
        Expect(Main.Probe.ModalTitle == "Mechanic's Toolbox", "repair bench opens");
        Expect(Main.Probe.PressModalButton("InspectCar"), "inspect action works after relaunch");
        await Wait(0.2f);
        for (int i = 0; i < 2; i++)
            Expect(Main.Probe.PressModalButton("Fit_tyre"), $"fit tyre {i} after relaunch");
        Expect(Main.Probe.PressModalButton("Fit_lamp"), "fit lamp after relaunch");
        Expect(Main.Probe.PressModalButton("Fit_battery"), "fit battery after relaunch");
        await Wait(0.45f);
        Expect(Main.Probe.MissionCompleted("M18"), "M18 completes after the partial reload");
        Expect(Main.Probe.Coins == 124, "M18 still pays its 24-coin reward once");
        foreach (string partId in FinaleRules.RepairPickupIds)
            Expect(Main.Probe.CollectedPickup(partId), $"{partId} remains recorded after completion");
        return false;
    }

    // -------------------------------------------------- chapter-two finale
    // Drives the real M16 delivery round, M17 checkout, M18 repair bench,
    // M19 six-gate rally and M20 festival through their stations, pickups,
    // dialogues and buttons.
}
