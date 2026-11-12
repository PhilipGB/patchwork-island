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
    private async Task<bool> RestoreChecks()
    {
        if (RestorePhase == 0)
        {
            WriteRestoreSave(BuildM03RestoreSave());
            RestorePhase = 1;
            Main.Probe.ReloadScene();
            return true;
        }

        Expect(!Main.Probe.Started, $"restore phase {RestorePhase} starts at the title");
        Expect(Main.Probe.CanContinue, $"restore phase {RestorePhase} offers Continue");
        Main.Probe.Continue();
        await Wait(0.7f);
        Expect(Main.Probe.Started, $"restore phase {RestorePhase} continues into the town");

        switch (RestorePhase)
        {
            case 1:
                Expect(Main.Probe.ActiveMission == "M03", "partial M03 active job restores");
                Expect(Main.Probe.BridgePieceCount == 5 && Main.Probe.BridgePlacedSocketCount == 5,
                    "partial M03 bridge pieces restore as visible placed parts");
                Expect(!Main.Probe.BridgeBuilt && Main.Probe.BridgeBarrierVisible,
                    "partial M03 bridge is still visibly under construction");
                Expect(Main.Probe.WorldNodeVisible("BridgeBarrier"),
                    "partial M03 keeps the scene-owned barrier visible");
                return await NextRestoreSave(BuildM07RestoreSave(), 2);

            case 2:
                Expect(Main.Probe.ActiveMission == "M07", "partial M07 active job restores");
                Expect(Main.Probe.BridgeBuilt && !Main.Probe.BridgeBarrierVisible,
                    "completed M03 bridge restores in the same save");
                Expect(Main.Probe.SwingPieceCount == 3 && Main.Probe.SwingPlacedSocketCount == 3,
                    "partial M07 swing pieces restore as visible placed parts");
                Expect(!Main.Probe.SwingBuilt && !Main.Probe.PlaygroundFixHidden,
                    "partial M07 playground still shows its repair sign");
                Expect(Main.Probe.BrokenSwingLabelsVisible == Main.Probe.BrokenSwingLabelCount,
                    "partial M07 keeps every scene-authored BROKEN label visible");
                return await NextRestoreSave(BuildM08RestoreSave(), 3);

            case 3:
                Expect(Main.Probe.ActiveMission == "M08", "partial M08 active job restores");
                Expect(Main.Probe.SwingBuilt && Main.Probe.PlaygroundFixHidden,
                    "completed M07 swing state restores visually");
                Expect(Main.Probe.ParkPlacementCount == 1 && Main.Probe.ParkVisualCount == 1,
                    "partial M08 village-green placement restores visually");
                return await NextRestoreSave(BuildM10RestoreSave(), 4);

            case 4:
                Expect(Main.Probe.ActiveMission == "M10", "partial M10 active job restores");
                Expect(Main.Probe.FurniturePlacementCount == 2 && Main.Probe.FurnitureVisualCount == 2,
                    "partial M10 furniture placements restore inside the house");
                Expect(Main.Probe.ParkPlacementCount == 3 && Main.Probe.ParkVisualCount == 3,
                    "completed M08 park placements stay restored");
                return await NextRestoreSave(BuildM15RestoreSave(), 5);

            case 5:
                Expect(Main.Probe.ActiveMission == "M15", "partial M15 active job restores");
                Expect(Main.Probe.MissionCounter("M15", "pad0") == 1 &&
                       Main.Probe.MissionCounter("M15", "pad1") == 2,
                    "partial M15 garden counters restore");
                Expect(Main.Probe.GardenFlowerVisible(0) && Main.Probe.GardenFlowerVisible(1) &&
                       !Main.Probe.GardenFlowerVisible(2),
                    "partial M15 flowers restore exactly where planted");
                Expect(Main.Probe.SignVisualVisible(0) && Main.Probe.SignVisualVisible(1) &&
                       Main.Probe.SignVisualVisible(2), "completed M14 sign visuals stay restored");
                Expect(Main.Probe.BakerySlotVisible(0) && Main.Probe.BakerySlotVisible(1) &&
                       Main.Probe.BakerySlotVisible(2), "completed M12 bakery slots stay restored");
                Expect(Main.Probe.LibrarySlotVisible(0) && Main.Probe.LibrarySlotVisible(1) &&
                       Main.Probe.LibrarySlotVisible(2) && Main.Probe.LibrarySlotVisible(3),
                    "completed M13 library slots stay restored");
                return await NextRestoreSave(BuildM18RestoreSave(), 6);

            case 6:
                Expect(Main.Probe.ActiveMission == "M18", "partial M18 active job restores");
                Expect(Main.Probe.MissionCounter("M18", "has_tyre") == 1,
                    "partial M18 tyre progress restores");
                Expect(!Main.Probe.PickupPresent("part_tyre_0") && Main.Probe.RepairPickupCount == 3,
                    "partial M18 collected pickup does not respawn");
                Expect(Main.Probe.WorldNodeVisible("PipCarBroken") &&
                       !Main.Probe.WorldNodeVisible("PipCarFixed"),
                    "partial M18 keeps the broken car visible");
                Expect(Main.Probe.DeliveryBoardVisible && Main.Probe.MarketStockVisible,
                    "completed M16/M17 visuals stay restored");
                return await NextRestoreSave(BuildM20RestoreSave(), 7);

            case 7:
                Expect(Main.Probe.ActiveMission == "M20", "partial M20 active job restores");
                Expect(Main.Probe.MissionCounter("M20", "invited_ada") == 1 &&
                       Main.Probe.MissionCounter("M20", "invited_pip") == 1 &&
                       Main.Probe.MissionCounter("M20", "invited_ivy") == 1,
                    "partial M20 invitations restore");
                Expect(Main.Probe.FestivalTableVisible(0) && !Main.Probe.FestivalTableVisible(1),
                    "partial M20 table placement restores exactly");
                Expect(Main.Probe.FestivalLanternVisible(0) && !Main.Probe.FestivalLanternVisible(1) &&
                       !Main.Probe.FestivalLanternVisible(2),
                    "partial M20 lantern placement restores exactly");
                Expect(!Main.Probe.FestivalDecorVisible,
                    "partial M20 does not show the finished festival bunting");
                Expect(Main.Probe.DeliveryBoardVisible && Main.Probe.MarketStockVisible &&
                       Main.Probe.PipCarFixedVisible && Main.Probe.RallyPodiumVisible,
                    "completed M16-M19 finale visuals stay restored");
                Expect(Main.Probe.Coins == 320, "partial M20 wallet restores exactly");
                return false;
        }

        throw new InvalidOperationException($"Unknown restore phase {RestorePhase}.");
    }

    private async Task<bool> NextRestoreSave(GameData data, int phase)
    {
        Main.Probe.ReturnToTitle();
        await Wait(0.35f);
        WriteRestoreSave(data);
        RestorePhase = phase;
        Main.Probe.ReloadScene();
        return true;
    }

    private void WriteRestoreSave(GameData data)
    {
        File.WriteAllText(Main.Probe.SavePath,
            JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static GameData BuildRestoreSave(string activeMission, int coins, int completedThrough)
    {
        var data = new GameData
        {
            SchemaVersion = 3,
            ActiveMission = activeMission,
            Coins = coins,
            CharacterPreset = CharacterCustomisation.Boy,
        };
        for (int i = 1; i <= completedThrough; i++)
            data.CompletedMissions.Add($"M{i:00}");
        data.CelebrationSeen = completedThrough >= 10;
        data.TutorialsSeen.AddRange(new[] { "walk", "drive", "build" });
        data.Player = new PlayerSnapshot
        {
            X = -38.8f,
            Y = -0.04f,
            Z = 21.4f,
            RotationY = 3.14f,
            Valid = true,
        };
        data.Van = new VehicleSnapshot
        {
            X = -30f,
            Y = -0.05f,
            Z = 14.5f,
            RotationY = 0f,
            Valid = true,
        };
        return data;
    }

    private static GameData BuildM03RestoreSave()
    {
        var data = BuildRestoreSave("M03", 22, 2);
        data.BridgePieces.AddRange(new[]
        {
            "support_0", "support_1", "support_2", "plank_0", "plank_1",
        });
        return data;
    }

    private static GameData BuildM07RestoreSave()
    {
        var data = BuildRestoreSave("M07", 84, 6);
        data.BridgePieces.AddRange(BuildRules.BridgeSockets);
        data.SwingPieces.AddRange(new[] { "s1_rope_l", "s1_rope_r", "s1_seat" });
        return data;
    }

    private static GameData BuildM08RestoreSave()
    {
        var data = BuildRestoreSave("M08", 102, 7);
        data.BridgePieces.AddRange(BuildRules.BridgeSockets);
        data.SwingPieces.AddRange(BuildRules.SwingSockets);
        data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
        return data;
    }

    private static void AddCompletedChapterTwoState(GameData data, int completedThrough)
    {
        if (completedThrough >= 11)
            foreach (string npc in ChapterTwoRules.CardNpcIds)
                data.SetMissionCounter("M11", $"delivered_{npc}", 1);
        if (completedThrough >= 12)
            foreach (var order in ChapterTwoRules.BakeryOrders)
                data.SetMissionCounter("M12", $"order_{order.CustomerId}", 1);
        if (completedThrough >= 13)
            foreach (string book in ChapterTwoRules.BookIds)
                data.SetMissionCounter("M13", ChapterTwoRules.ShelfKey(book), 1);
        if (completedThrough >= 14)
            foreach (var socket in ChapterTwoRules.SignSockets)
                data.SetMissionCounter("M14", $"sign_{socket.SocketId}", 1);
        if (completedThrough >= 15)
            for (int i = 0; i < ChapterTwoRules.GardenPattern.Length; i++)
                data.SetMissionCounter("M15", $"pad{i}",
                    ChapterTwoRules.GardenCode(ChapterTwoRules.GardenPattern[i]));
        if (completedThrough >= 16)
            foreach (string npc in FinaleRules.DeliveryRecipients)
                data.SetMissionCounter("M16", FinaleRules.DeliveredKey(npc), 1);
        if (completedThrough >= 17)
            data.SetMissionCounter("M17", "finalised", 1);
        if (completedThrough >= 18)
            data.SetMissionCounter("M18", "battery_fitted", 1);
        if (completedThrough >= 19)
            data.SetMissionCounter("M19", "checkpoint", FinaleRules.RallyGateCount);
    }

    private static void AddCompletedConstructionState(GameData data)
    {
        data.BridgePieces.AddRange(BuildRules.BridgeSockets);
        data.SwingPieces.AddRange(BuildRules.SwingSockets);
        data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
        data.ParkPlacements.Add(new PlacedItem("flowerbed", "park1"));
        data.ParkPlacements.Add(new PlacedItem("lamp", "park2"));
        foreach (var pair in FurnitureRules.Required)
            data.FurnitureOwned[pair.Key] = pair.Value;
    }

    private static GameData BuildM10RestoreSave()
    {
        var data = BuildRestoreSave("M10", 122, 8);
        AddCompletedConstructionState(data);
        data.FurniturePlacements.Add(new PlacedItem("table", "house0"));
        data.FurniturePlacements.Add(new PlacedItem("chair", "house1"));
        return data;
    }

    private static GameData BuildM15RestoreSave()
    {
        var data = BuildRestoreSave("M15", 213, 14);
        AddCompletedConstructionState(data);
        AddCompletedChapterTwoState(data, 14);
        data.SetMissionCounter("M15", "pad0", 1);
        data.SetMissionCounter("M15", "pad1", 2);
        return data;
    }

    private static GameData BuildM18RestoreSave()
    {
        var data = BuildRestoreSave("M18", 271, 17);
        AddCompletedConstructionState(data);
        AddCompletedChapterTwoState(data, 17);
        data.SetMissionCounter("M18", "has_tyre", 1);
        data.CollectedPickups.Add("part_tyre_0");
        return data;
    }

    private static GameData BuildM20RestoreSave()
    {
        var data = BuildRestoreSave("M20", 320, 19);
        AddCompletedConstructionState(data);
        AddCompletedChapterTwoState(data, 19);
        foreach (string npc in FinaleRules.FestivalInvitees)
            data.SetMissionCounter("M20", FinaleRules.InvitedKey(npc), 1);
        data.SetMissionCounter("M20", FinaleRules.TableKey(0), 1);
        data.SetMissionCounter("M20", FinaleRules.LanternKey(0), 1);
        return data;
    }

    // ------------------------------------------- bridge and rally-road traversal
    // Regression for two reported stuck spots: the van at the bridge/road seam
    // and the player stepping from grass onto the raised-looking rally road.
}
