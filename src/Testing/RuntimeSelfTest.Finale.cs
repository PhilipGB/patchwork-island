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
    private async Task FinaleChecks()
    {
        Expect(!Main.Probe.Started, "finale test starts on the title screen");
        var started = Main.Probe.StartNewGameFromTitleConfirmed(CharacterCustomisation.Boy);
        await Wait(0.8f);
        Expect(started.Success && Main.Probe.Started, "finale test starts a fresh village");
        Main.Probe.PrepareFinale();
        await Wait(0.5f);
        Expect(Main.Probe.Coins == 100, "finale wallet starts at the prepared 100 coins");
        Expect(Main.Probe.MissionStatus("M16") == MissionStatus.Available,
            "M16 is available after M14 and M15");
        Expect(Main.Probe.MissionStatus("M17") == MissionStatus.Locked,
            "M17 waits for M16");

        // M16: load at the post counter, reject a wrong parcel and deliver all three.
        Expect(await StartJobFromBoard("M16"), "M16 starts from the job board");
        Main.Probe.TeleportNearPostCounter();
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "PostCounterStation",
            "post counter is nearest");
        await PressInteract();
        Expect(Main.Probe.MissionCounter("M16", "loaded") == 1, "three parcels loaded");
        Expect(Main.Probe.Coins == 100, "loading parcels spends no wallet coins");

        await OfferParcel("ada", "pip");
        Expect(Main.Probe.MissionCounter("M16", "delivered_ada") == 0,
            "wrong parcel not delivered");
        Expect(Main.Probe.MissionCounter("M16", "carrying_pip") == 1,
            "wrong parcel stays carried");
        Expect(Main.Probe.Coins == 100, "wrong parcel spends nothing");
        await OfferParcel("ada", "ada");
        Expect(Main.Probe.MissionCounter("M16", "delivered_ada") == 1, "Ada parcel delivered");
        await OfferParcel("pip", "pip");
        Expect(Main.Probe.MissionCounter("M16", "delivered_pip") == 1, "Pip parcel delivered");
        await OfferParcel("ivy", "ivy");
        await Wait(0.45f);
        Expect(Main.Probe.MissionCompleted("M16"), "M16 completes through real dialogue");
        Expect(Main.Probe.Coins == 120, "M16 reward is 20 coins");
        Expect(Main.Probe.DeliveryBoardVisible, "delivery board appears after M16");
        await CloseCompletion("M16");

        // M17: the 20-coin job purse, exact list, personal wallet untouched.
        Expect(await StartJobFromBoard("M17"), "M17 starts from the job board");
        Main.Probe.TeleportNearShop();
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "ShopStation", "market shop is nearest");
        await PressInteract();
        Expect(Main.Probe.ModalTitle == "Market Day Maths", "market maths checkout opens");
        for (int i = 0; i < 2; i++) Expect(Main.Probe.PressModalButton("MarketAdd_bread"), $"bread {i}");
        Expect(Main.Probe.PressModalButton("MarketAdd_milk"), "milk");
        for (int i = 0; i < 2; i++) Expect(Main.Probe.PressModalButton("MarketAdd_apple"), $"apple {i}");
        await Wait(0.3f);
        Expect(Main.Probe.Coins == 120, "job purse never spends personal coins");
        Expect(Main.Probe.ModalLabelText("MarketTotal").Contains("14"), "job total is 14");
        Expect(Main.Probe.PressModalButton("MarketFinalise"), "pay with job coins");
        await Wait(0.45f);
        Expect(Main.Probe.MissionCompleted("M17"), "M17 completes through the checkout");
        Expect(Main.Probe.Coins == 142, "M17 reward is 22 coins");
        Expect(Main.Probe.MarketStockVisible, "market stock appears after M17");
        await CloseCompletion("M17");

        // M18: four real part pickups and the tyre-lamp-battery repair bench.
        Expect(await StartJobFromBoard("M18"), "M18 starts from the job board");
        foreach (string partId in new[] { "part_tyre_0", "part_tyre_1", "part_lamp_0", "part_battery_0" })
        {
            Main.Probe.TeleportNearPickup(partId);
            await Wait(0.3f);
            Expect(Main.Probe.NearestInteractableName == "Pickup_" + partId, $"{partId} is nearest");
            await PressInteract();
        }
        Expect(Main.Probe.MissionCounter("M18", "has_tyre") == 2, "two tyres collected");
        Expect(Main.Probe.MissionCounter("M18", "has_lamp") == 1, "lamp collected");
        Expect(Main.Probe.MissionCounter("M18", "has_battery") == 1, "battery collected");

        Main.Probe.TeleportNearRepairCar();
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "RepairStation", "repair bench is nearest");
        await PressInteract();
        Expect(Main.Probe.ModalTitle == "Mechanic's Toolbox", "repair bench opens");
        Expect(Main.Probe.PressModalButton("InspectCar"), "inspect action works");
        await Wait(0.2f);
        Expect(Main.Probe.PressModalButton("Fit_lamp"), "lamp fit action is available");
        await Wait(0.25f);
        Expect(Main.Probe.ModalLabelText("RepairStatus").Contains("tyres"),
            "lamp before tyres gives a stage hint");
        Expect(Main.Probe.MissionCounter("M18", "has_lamp") == 1, "wrong stage keeps the lamp");
        for (int i = 0; i < 2; i++) Expect(Main.Probe.PressModalButton("Fit_tyre"), $"fit tyre {i}");
        Expect(Main.Probe.PressModalButton("Fit_lamp"), "fit lamp");
        Expect(Main.Probe.PressModalButton("Fit_battery"), "fit battery");
        await Wait(0.45f);
        Expect(Main.Probe.MissionCompleted("M18"), "M18 completes through the repair bench");
        Expect(Main.Probe.Coins == 166, "M18 reward is 24 coins");
        Expect(Main.Probe.PipCarFixedVisible, "Pip's car is visibly repaired");
        await CloseCompletion("M18");

        // M19: real van autopilot through the six-gate grand rally.
        Expect(Main.Probe.PrepareRally("M19"), "M19 autopilot prepared");
        await Wait(0.5f);
        Expect(Main.Probe.RallyRivalPresent, "Pip's rally rival is present");
        float deadline = 90f;
        while (!Main.Probe.MissionCompleted("M19") && deadline > 0f)
        {
            deadline -= 0.5f;
            await Wait(0.5f);
        }
        Expect(Main.Probe.MissionCompleted("M19"), "M19 completes through real driving");
        Expect(Main.Probe.RaceCheckpoint("M19") == 6, "M19 reaches all six gates");
        Main.Probe.StopRallyAutopilot();
        await Wait(0.3f);
        Expect(Main.Probe.Coins == 196, "M19 reward and medal total 30 coins");
        Expect(Main.Probe.ModalTitle == "JOB COMPLETE!", "M19: completion modal opens");
        Main.Probe.CloseModal();
        await Wait(0.3f);
        Expect(!Main.Probe.Paused, "M19: closing completion unpauses the village");
        Main.Probe.LeaveVanForTest();
        await Wait(0.35f);
        Expect(Main.Probe.Mode == "Walking" && !Main.Probe.Paused, "finale leaves the rally van");
        Expect(Main.Probe.RallyPodiumVisible, "grand rally podium appears after M19");

        // M20: stage errors, invitations, tables, lanterns and the second finale.
        Expect(await StartJobFromBoard("M20"), "M20 starts from the job board");
        Main.Probe.TeleportNearFestivalTable(0);
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "FestivalTableStation_0",
            "festival table pad is nearest");
        await PressInteract();
        Expect(Main.Probe.MissionCounter("M20", "table_0") == 0,
            "table before invitations is refused");
        Main.Probe.TeleportNearFestivalLantern(0);
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "FestivalLanternStation_0",
            "festival lantern hook is nearest");
        await PressInteract();
        Expect(Main.Probe.MissionCounter("M20", "lantern_0") == 0,
            "lantern before invitations is refused");

        foreach (string npc in FinaleRules.FestivalInvitees)
        {
            await TalkTo(npc, FinaleRules.DeliveryName(npc));
            Expect(Main.Probe.PressModalButton("FestivalInvite"), $"invite {npc}");
            await Wait(0.35f);
            Expect(Main.Probe.MissionCounter("M20", FinaleRules.InvitedKey(npc)) == 1,
                $"{npc} invited");
        }

        for (int i = 0; i < FinaleRules.FestivalTableCount; i++)
        {
            Main.Probe.TeleportNearFestivalTable(i);
            await Wait(0.3f);
            Expect(Main.Probe.NearestInteractableName == $"FestivalTableStation_{i}",
                $"table pad {i + 1} is nearest");
            await PressInteract();
            Expect(Main.Probe.MissionCounter("M20", FinaleRules.TableKey(i)) == 1,
                $"table {i + 1} placed");
        }
        // Undo and re-place the first table.
        Main.Probe.TeleportNearFestivalTable(0);
        await Wait(0.3f);
        await PressInteract();
        Expect(Main.Probe.MissionCounter("M20", "table_0") == 0, "table 1 taken back");
        await PressInteract();
        Expect(Main.Probe.MissionCounter("M20", "table_0") == 1, "table 1 placed again");

        for (int i = 0; i < FinaleRules.FestivalLanternCount; i++)
        {
            Main.Probe.TeleportNearFestivalLantern(i);
            await Wait(0.3f);
            Expect(Main.Probe.NearestInteractableName == $"FestivalLanternStation_{i}",
                $"lantern hook {i + 1} is nearest");
            await PressInteract();
            Expect(Main.Probe.MissionCounter("M20", FinaleRules.LanternKey(i)) == 1,
                $"lantern {i + 1} hung");
            if (i == 0)
            {
                await PressInteract();
                Expect(Main.Probe.MissionCounter("M20", "lantern_0") == 0,
                    "lantern 1 taken down");
                await PressInteract();
                Expect(Main.Probe.MissionCounter("M20", "lantern_0") == 1,
                    "lantern 1 hung again");
            }
        }
        await Wait(0.5f);
        Expect(Main.Probe.MissionCompleted("M20"), "M20 completes through the festival pads");
        Expect(Main.Probe.Coins == 226, "M20 reward is 30 coins");
        Expect(Main.Probe.ModalTitle == "JOB COMPLETE!", "M20: completion modal opens");
        Main.Probe.CloseModal();
        await Wait(0.6f);
        Expect(Main.Probe.ModalTitle == "VILLAGE FESTIVAL!", "second finale opens once");
        Expect(Main.Probe.FestivalSeen, "FestivalSeen is recorded");
        Expect(Main.Probe.FestivalDecorVisible, "festival bunting is visible");
        Expect(Main.Probe.AllTwentyComplete, "all twenty jobs complete");
        Expect(Main.Probe.MusicId == "festival", "festival music context starts");
        Main.Probe.CloseModal();
        await Wait(0.35f);
        Expect(Main.Probe.SaveNow(), "completed festival state saves");
        var disk = SaveService.Load(Main.Probe.SavePath, Main.Probe.Catalog);
        Expect(disk.Success, disk.Message);
        Expect(disk.Success && disk.Data.CompletedMissions.Count == 20,
            "saved finale holds twenty completions");
        Expect(disk.Success && disk.Data.FestivalSeen && disk.Data.CelebrationSeen,
            "both finale markers are persisted separately");
        Expect(disk.Success && disk.Data.Coins == 226, "saved finale wallet is exact");
        Expect(Main.Probe.MissionStatus("M20") == MissionStatus.Completed,
            "M20 remains completed");
        Expect(Main.Probe.MissionStatus("M16") == MissionStatus.Completed &&
               Main.Probe.MissionStatus("M17") == MissionStatus.Completed &&
               Main.Probe.MissionStatus("M18") == MissionStatus.Completed &&
               Main.Probe.MissionStatus("M19") == MissionStatus.Completed,
            "M16-M19 remain completed");
        Expect(Main.Probe.CelebrationSeen, "original M10 celebration marker survives");

        Main.Probe.ReturnToTitle();
        await Wait(0.4f);
        Main.Probe.Continue();
        await Wait(0.6f);
        Expect(Main.Probe.Started, "completed festival save continues");
        Expect(Main.Probe.ModalTitle != "VILLAGE FESTIVAL!",
            "second finale does not reopen after Continue");
    }

    private async Task OfferParcel(string npcId, string recipientId)
    {
        Main.Probe.TeleportVanNearNpc(npcId);
        await Wait(0.2f);
        await TalkTo(npcId, FinaleRules.DeliveryName(npcId));
        Expect(Main.Probe.PressModalButton("Deliver_" + recipientId),
            $"deliver {recipientId} at {npcId}");
        await Wait(0.4f);
    }

    private async Task PressInteract()
    {
        Input.ActionPress("interact");
        Input.ActionRelease("interact");
        await Wait(0.35f);
    }

    private async Task TalkTo(string id, string title)
    {
        Main.Probe.TeleportPlayerNearNpc(id);
        await Wait(0.35f);
        Expect(Main.Probe.NearestInteractableName == "NpcTalk_" + id,
            $"{id} is the nearest E interaction");
        await PressInteract();
        if (Main.Probe.ModalTitle != title)
            GD.Print($"TALK_FAIL id={id} expected='{title}' actual='{Main.Probe.ModalTitle}' nearest='{Main.Probe.NearestInteractableName}'");
        Expect(Main.Probe.ModalTitle == title, $"{id}'s real dialogue opens");
    }

    private async Task<bool> StartJobFromBoard(string missionId)
    {
        Main.Probe.TeleportNearJobBoard();
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "JobBoardStation",
            $"{missionId}: job board is nearest");
        await PressInteract();
        Expect(Main.Probe.ModalTitle == "Patchwork Jobs", $"{missionId}: job board opens");
        bool pressed = Main.Probe.PressModalButton("Start_" + missionId);
        await Wait(0.4f);
        bool started = pressed && Main.Probe.ActiveMission == missionId;
        Expect(started, $"{missionId}: Start button starts the job");
        return started;
    }

    private async Task CloseCompletion(string missionId)
    {
        Expect(Main.Probe.ModalTitle == "JOB COMPLETE!", $"{missionId}: completion modal opens");
        Main.Probe.CloseModal();
        await Wait(0.3f);
        Expect(!Main.Probe.Paused && Main.Probe.Mode == "Walking",
            $"{missionId}: closing completion resumes walking");
    }

    private static bool Finite(Vector3 value) =>
        !float.IsNaN(value.X) && !float.IsNaN(value.Y) && !float.IsNaN(value.Z) &&
        !float.IsInfinity(value.X) && !float.IsInfinity(value.Y) && !float.IsInfinity(value.Z);

    // ---------------------------------------------------- real autopilot rally
    // Drives the actual van physics and the new continuous road/collision mesh
    // through every gate. This is deliberately not a teleport-through-gates
    // shortcut: the van steers, accelerates and collides with the world.
}
