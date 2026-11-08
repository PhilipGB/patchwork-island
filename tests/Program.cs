using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PatchworkIsland;

namespace PatchworkIsland.Tests;

/// <summary>
/// Dependency-free test runner for mission rules, unlocks, exactly-once
/// rewards, duplicate pickup protection and serialisation. Exits non-zero on
/// failure so CI / the release gate can rely on it.
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;
    private static readonly List<string> Failures = new();

    private static int Main(string[] args)
    {
        string root = FindRepoRoot();
        string missionPath = Path.Combine(root, "data", "missions.json");
        string json = File.ReadAllText(missionPath);
        var catalog = MissionCatalog.FromJson(json);

        Run("catalog contains exactly ten missions", () =>
        {
            Equal(10, catalog.Missions.Count, "mission count");
            Equal("M01", catalog.Missions[0].Id, "first mission id");
            Equal("M10", catalog.Missions[^1].Id, "last mission id");
        });

        Run("initial availability and prerequisites", () =>
        {
            var s = Fresh(catalog);
            Equal(MissionStatus.Available, s.GetStatus("M01"), "M01 status");
            Equal(MissionStatus.Available, s.GetStatus("M02"), "M02 status");
            Equal(MissionStatus.Locked, s.GetStatus("M03"), "M03 locked by M02");
            Equal(MissionStatus.Locked, s.GetStatus("M04"), "M04 locked by M01");
            Equal(MissionStatus.Locked, s.GetStatus("M09"), "M09 locked by M05");
            Equal(MissionStatus.Locked, s.GetStatus("M10"), "M10 locked by M04 + M08");
        });

        Run("starting a job and switching preserves progress", () =>
        {
            var s = Fresh(catalog);
            var started = s.StartMission("M01");
            Check(started.Success, "start M01");
            Equal(MissionStatus.Active, s.GetStatus("M01"), "M01 active");
            var take = s.CollectPickup("apple_0", "apple", "M01", 3);
            Check(take.Success, "collect first apple");
            Equal(1, s.Data.GetItem("apple"), "apple count");
            var switched = s.StartMission("M02");
            Check(switched.Success, "switch to M02");
            Equal(MissionStatus.Active, s.GetStatus("M02"), "M02 active");
            var back = s.StartMission("M01");
            Check(back.Success, "switch back to M01");
            Equal(1, s.Data.GetItem("apple"), "apple count persisted");
        });

        Run("duplicate pickup is rejected and not consumed twice", () =>
        {
            var s = Fresh(catalog);
            s.StartMission("M01");
            Check(s.CollectPickup("apple_0", "apple", "M01", 3).Success, "first pickup");
            var duplicate = s.CollectPickup("apple_0", "apple", "M01", 3);
            Check(!duplicate.Success, "duplicate rejected");
            Equal(1, s.Data.GetItem("apple"), "apple count stays 1");
        });

        Run("M01 cannot complete early; completion awards 10 exactly once", () =>
        {
            var s = Fresh(catalog);
            s.StartMission("M01");
            s.CollectPickup("apple_0", "apple", "M01", 3);
            s.CollectPickup("apple_1", "apple", "M01", 3);
            var early = s.CompleteActive();
            Check(!early.Success, "early completion rejected");
            Equal(0, s.Data.Coins, "no coins early");
            s.CollectPickup("apple_2", "apple", "M01", 3);
            var completion = s.DeliverApples();
            Check(completion.Success, "M01 delivered");
            Equal(10, completion.CoinsAwarded, "M01 reward");
            Equal(10, s.Data.Coins, "wallet after M01");
            Check(s.Data.IsCompleted("M01"), "M01 recorded complete");
            Check(s.Data.UnlockedCosmetics.Contains("red_cap"), "red cap unlocked");
            var repeat = s.DeliverApples();
            Check(!repeat.Success, "repeat delivery rejected");
            Equal(10, s.Data.Coins, "coins not awarded twice");
            Equal(1, s.Data.CompletedMissions.Count, "one completion record");
            Equal(MissionStatus.Completed, s.GetStatus("M01"), "M01 stays completed");
        });

        Run("M01 unlock opens M04", () =>
        {
            var s = CompleteM01(Fresh(catalog));
            Equal(MissionStatus.Available, s.GetStatus("M04"), "M04 available");
            Equal(MissionStatus.Locked, s.GetStatus("M03"), "M03 still locked");
        });

        Run("M02 counts 5 wood + 3 stone and awards 12 once", () =>
        {
            var s = Fresh(catalog);
            s.StartMission("M02");
            for (int i = 0; i < 5; i++)
                Check(s.CollectPickup($"wood_{i}", "wood", "M02", 5).Success, $"collect wood {i}");
            for (int i = 0; i < 2; i++)
                Check(s.CollectPickup($"stone_{i}", "stone", "M02", 3).Success, $"collect stone {i}");
            var partial = s.SubmitSupplies();
            Check(!partial.Success, "partial stone rejected");
            Equal(0, s.Data.Coins, "no coins for partial");
            Check(s.CollectPickup("stone_2", "stone", "M02", 3).Success, "collect last stone");
            Equal(8, s.Data.GetItem("wood") + s.Data.GetItem("stone"), "eight supplies counted");
            var complete = s.SubmitSupplies();
            Check(complete.Success, "M02 submitted");
            Equal(12, complete.CoinsAwarded, "M02 reward");
            Equal(12, s.Data.Coins, "wallet");
            Equal(0, s.Data.GetItem("wood"), "wood consumed");
            Equal(0, s.Data.GetItem("stone"), "stone consumed");
            Equal(MissionStatus.Available, s.GetStatus("M03"), "M03 unlocked");
            Check(!s.SubmitSupplies().Success, "second submit rejected");
            Equal(12, s.Data.Coins, "coins once");
        });

        Run("locked mission cannot start", () =>
        {
            var s = Fresh(catalog);
            var result = s.StartMission("M03");
            Check(!result.Success, "M03 start rejected");
            Equal("", s.Data.ActiveMission, "no active mission");
        });

        Run("completed mission cannot be restarted for another reward", () =>
        {
            var s = CompleteM01(Fresh(catalog));
            var restart = s.StartMission("M01");
            Check(!restart.Success, "restart rejected");
            Equal(10, s.Data.Coins, "coins unchanged");
        });

        Run("picking items for an inactive job is rejected", () =>
        {
            var s = Fresh(catalog);
            s.StartMission("M02");
            var apple = s.CollectPickup("apple_0", "apple", "M01", 3);
            Check(!apple.Success, "inactive job pickup rejected");
            Equal(0, s.Data.GetItem("apple"), "no apple added");
        });

        Run("hints progress through the authored layers", () =>
        {
            var s = Fresh(catalog);
            s.StartMission("M01");
            var one = s.GetNextHint("M01");
            var two = s.GetNextHint("M01");
            var three = s.GetNextHint("M01");
            Check(!string.IsNullOrWhiteSpace(one.Text), "hint one present");
            Check(!string.IsNullOrWhiteSpace(two.Text), "hint two present");
            Check(!string.IsNullOrWhiteSpace(three.Text), "hint three present");
            Check(one.Text != two.Text || two.Text != three.Text, "hints vary");
        });

        Run("grocery arithmetic and task budget", () =>
        {
            Equal(10, BudgetRules.GroceryTotal(2, 1), "2 bread + 1 milk");
            Equal(2, BudgetRules.GroceryChange(2, 1), "change from 12");
            Check(BudgetRules.IsGroceryCorrect(2, 1), "exact basket accepted");
            Check(!BudgetRules.CanAddGrocery(2, 1, "bread"), "cannot overspend budget");
            Check(BudgetRules.CanAddGrocery(1, 0, "milk"), "affordable milk");
            Equal(19, BudgetRules.ParkTotal(1, 1, 1), "park total 6+4+9");
            Equal(1, BudgetRules.ParkBudget - BudgetRules.ParkTotal(1, 1, 1), "park change");
            Equal(21, BudgetRules.FurnitureTotal(1, 2, 1), "house total 8+5+5+3");
        });

        Run("M04 uses only the task purse and awards 12 once", () =>
        {
            var s = CompleteM01(Fresh(catalog));
            Equal(10, s.Data.Coins, "wallet after M01");
            Check(s.StartMission("M04").Success, "start M04");
            Check(s.AddGrocery("bread").Success, "add bread 1");
            Check(s.AddGrocery("bread").Success, "add bread 2");
            Check(s.AddGrocery("milk").Success, "add milk");
            Equal(10, s.Data.Coins, "core wallet untouched by shopping");
            Equal(10, s.GrocerySpent, "grocery total");
            Equal(2, s.GroceryChange, "grocery change");
            Check(!s.AddGrocery("bread").Success, "cannot exceed 12 job coins");
            Equal(2, s.GroceryBread, "bread count unchanged after refusal");
            Check(s.RemoveGrocery("milk").Success, "refund milk");
            Equal(0, s.GroceryMilk, "milk refunded");
            Check(s.AddGrocery("milk").Success, "buy milk again");
            var delivered = s.DeliverGroceries();
            Check(delivered.Success, "deliver groceries");
            Equal(12, delivered.CoinsAwarded, "M04 reward");
            Equal(22, s.Data.Coins, "wallet after M04");
            Equal(MissionStatus.Available, s.GetStatus("M06"), "M06 unlocked");
            Check(!s.DeliverGroceries().Success, "repeat delivery rejected");
            Equal(22, s.Data.Coins, "M04 reward once");
        });

        Run("bridge snap order, invalid placement and completion", () =>
        {
            var s = CompleteM02(Fresh(catalog));
            Check(s.StartMission("M03").Success, "start M03");
            var plankFirst = s.PlaceBridgePiece("plank", "plank_0");
            Check(!plankFirst.Success, "plank before supports rejected");
            Check(s.Data.BridgePieces.Count == 0, "invalid plank not consumed");
            var wrongSupport = s.PlaceBridgePiece("support", "plank_2");
            Check(!wrongSupport.Success, "support in plank socket rejected");
            for (int i = 0; i < 3; i++)
                Check(s.PlaceBridgePiece("support", $"support_{i}").Success, $"support {i}");
            Check(!s.PlaceBridgePiece("support", "support_0").Success, "duplicate support rejected");
            for (int i = 0; i < 4; i++)
                Check(s.PlaceBridgePiece("plank", $"plank_{i}").Success, $"plank {i}");
            Equal(7, s.Data.BridgePieces.Count, "seven bridge pieces");
            Check(MissionRules.IsObjectiveComplete(s.Data, "M03"), "bridge objective complete");
            var completion = s.CompleteActive();
            Check(completion.Success, "bridge mission completes");
            Equal(20, completion.CoinsAwarded, "M03 reward");
            Equal(MissionStatus.Available, s.GetStatus("M05"), "M05 unlocked");
            Equal(MissionStatus.Available, s.GetStatus("M07"), "M07 unlocked");
            Check(!s.CompleteActive().Success, "M03 cannot double-award");
        });

        Run("bridge rules are independent and enforce seven sockets", () =>
        {
            var placed = new List<string>();
            Check(!BuildRules.ValidateBridgePlacement("plank", "plank_0", placed, out _), "no plank first");
            Check(BuildRules.ValidateBridgePlacement("support", "support_0", placed, out _), "support valid");
            placed.Add("support_0");
            Check(!BuildRules.ValidateBridgePlacement("support", "support_0", placed, out _), "duplicate invalid");
            placed.Add("support_1");
            placed.Add("support_2");
            Check(BuildRules.ValidateBridgePlacement("plank", "plank_0", placed, out _), "plank after supports");
            Check(!BuildRules.BridgeComplete(placed), "not complete with three");
            placed.AddRange(new[] { "plank_0", "plank_1", "plank_2", "plank_3" });
            Check(BuildRules.BridgeComplete(placed), "complete with seven");
        });

        Run("checkpoint gates must be taken in order", () =>
        {
            var s = CompleteBridge(Fresh(catalog));
            Check(s.StartMission("M05").Success, "start M05");
            Check(!s.AdvanceCheckpoint(2).Success, "gate 2 before 1 rejected");
            Check(s.AdvanceCheckpoint(1).Success, "gate 1 accepted");
            Check(!s.AdvanceCheckpoint(3).Success, "gate 3 before 2 rejected");
            Check(!s.AdvanceCheckpoint(1).Success, "gate 1 repeat rejected");
            Check(s.AdvanceCheckpoint(2).Success, "gate 2 accepted");
            Check(s.AdvanceCheckpoint(3).Success, "gate 3 accepted");
            Check(!MissionRules.IsObjectiveComplete(s.Data, "M05"), "not complete before gate 4");
            Check(s.AdvanceCheckpoint(4).Success, "gate 4 accepted");
            Check(MissionRules.IsObjectiveComplete(s.Data, "M05"), "race complete");
            var before = s.Data.Coins;
            var complete = s.CompleteActive(0);
            Check(complete.Success, "M05 complete");
            Equal(15, complete.CoinsAwarded, "M05 base reward");
            Equal(before + 15, s.Data.Coins, "wallet after M05");
            Equal(MissionStatus.Available, s.GetStatus("M09"), "M09 unlocked");
            Check(!s.CompleteActive(0).Success, "M05 cannot complete twice");
        });

        Run("optional race bonus never blocks completion", () =>
        {
            Equal(5, MissionRules.OptionalRaceBonus("M05", 89f), "M05 bonus inside 90s");
            Equal(0, MissionRules.OptionalRaceBonus("M05", 91f), "M05 no bonus outside 90s");
            Equal(5, MissionRules.OptionalRaceBonus("M09", 75f), "M09 bonus inside 75s");
            Equal(0, MissionRules.OptionalRaceBonus("M09", 75.1f), "M09 no bonus outside 75s");
            Equal(0, MissionRules.OptionalRaceBonus("M06", 1f), "no race bonus on non-race");
        });

        Run("two-stop post enforces parcels and destination types", () =>
        {
            var s = CompleteM04(Fresh(catalog));
            Equal(MissionStatus.Available, s.GetStatus("M06"), "M06 unlocked after M04");
            Check(s.StartMission("M06").Success, "start M06");
            Check(!s.DeliverBlueParcels().Success, "cannot deliver blue without picking up");
            Check(!s.DeliverOrangeParcel().Success, "cannot deliver orange without picking up");
            Check(s.CollectPickup("blue_parcel_0", "blue_parcel", "M06", 2).Success, "blue 1");
            Check(s.CollectPickup("blue_parcel_1", "blue_parcel", "M06", 2).Success, "blue 2");
            Check(s.CollectPickup("orange_parcel_0", "orange_parcel", "M06", 1).Success, "orange 1");
            Equal(2, s.Data.GetItem("blue_parcel"), "two blue carried");
            Equal(1, s.Data.GetItem("orange_parcel"), "one orange carried");
            var blue = s.DeliverBlueParcels();
            Check(blue.Success, "deliver blue parcels");
            Equal(0, s.Data.GetItem("blue_parcel"), "blue consumed");
            Equal(2, s.Data.GetMissionCounter("M06", "blueDelivered"), "blue delivered count");
            Check(!MissionRules.IsObjectiveComplete(s.Data, "M06"), "not complete with orange outstanding");
            Check(!s.DeliverBlueParcels().Success, "blue cannot double-deliver");
            var orange = s.DeliverOrangeParcel();
            Check(orange.Success, "deliver orange parcel");
            Check(MissionRules.IsObjectiveComplete(s.Data, "M06"), "M06 objective complete");
            var completion = s.CompleteActive(0);
            Check(completion.Success, "M06 complete");
            Equal(15, completion.CoinsAwarded, "M06 reward");
            Check(!s.DeliverOrangeParcel().Success, "orange cannot double-deliver");
        });

        Run("M07 swing assembly enforces ropes and seats", () =>
        {
            var s = CompleteBridge(Fresh(catalog));
            Check(s.StartMission("M07").Success, "start M07");
            Check(!s.PlaceSwingPiece("seat", "s1_seat").Success, "seat before ropes rejected");
            Check(!s.PlaceSwingPiece("rope", "s1_seat").Success, "rope in seat socket rejected");
            Check(s.PlaceSwingPiece("rope", "s1_rope_l").Success, "s1 rope left");
            Check(!s.PlaceSwingPiece("seat", "s1_seat").Success, "seat needs both ropes");
            Check(s.PlaceSwingPiece("rope", "s1_rope_r").Success, "s1 rope right");
            Check(s.PlaceSwingPiece("seat", "s1_seat").Success, "s1 seat");
            Check(!s.PlaceSwingPiece("seat", "s1_seat").Success, "duplicate seat rejected");
            Check(s.PlaceSwingPiece("rope", "s2_rope_l").Success, "s2 rope left");
            Check(s.PlaceSwingPiece("rope", "s2_rope_r").Success, "s2 rope right");
            Check(s.PlaceSwingPiece("seat", "s2_seat").Success, "s2 seat");
            Equal(6, s.Data.SwingPieces.Count, "six swing pieces");
            var completion = s.CompleteActive();
            Check(completion.Success, "M07 complete");
            Equal(18, completion.CoinsAwarded, "M07 reward");
            Check(!s.CompleteActive().Success, "M07 cannot double award");
        });

        Run("M08 park budget, pad uniqueness and undo", () =>
        {
            var s = CompleteM07(Fresh(catalog));
            Equal(MissionStatus.Available, s.GetStatus("M08"), "M08 unlocked");
            Check(s.StartMission("M08").Success, "start M08");
            int walletBefore = s.Data.Coins;
            Check(ParkRules.Place(s.Data, "bench", "park0").Success, "place bench");
            Check(ParkRules.Place(s.Data, "flowerbed", "park1").Success, "place flowerbed");
            Check(!ParkRules.Place(s.Data, "bench", "park2").Success, "only one bench allowed");
            Check(ParkRules.Place(s.Data, "lamp", "park2").Success, "place lamp");
            Equal(19, ParkRules.Spent(s.Data), "park total 6+4+9");
            Equal(1, ParkRules.Left(s.Data), "one job coin left");
            Equal(walletBefore, s.Data.Coins, "park budget separate from wallet");
            var undo = ParkRules.UndoLast(s.Data);
            Check(undo != null, "undo returns item");
            Equal(0, ParkRules.Count(s.Data, "lamp"), "lamp removed");
            Check(ParkRules.Place(s.Data, "lamp", "park2").Success, "lamp placed again");
            Check(!ParkRules.Place(s.Data, "bench", "park3").Success, "budget prevents a second bench");
            Check(!ParkRules.Place(s.Data, "bench", "park0").Success, "pad uniqueness enforced");
            Equal(3, s.Data.ParkPlacements.Count, "three placements");
            Check(MissionRules.IsObjectiveComplete(s.Data, "M08"), "park objective complete");
            var complete = s.CompleteActive();
            Check(complete.Success, "M08 complete");
            Equal(20, complete.CoinsAwarded, "M08 reward");
            Equal(walletBefore + 20, s.Data.Coins, "wallet after park reward");
        });

        Run("M10 furniture cost and house prerequisites", () =>
        {
            var s = CompleteM08(Fresh(catalog));
            Equal(MissionStatus.Locked, s.GetStatus("M10"), "M10 locked until M04");
            s = CompleteM04(s);
            Equal(MissionStatus.Available, s.GetStatus("M10"), "M10 available after M04 + M08");
            Check(s.StartMission("M10").Success, "start M10");
            int walletBefore = s.Data.Coins;
            Equal(21, FurnitureRules.FullSetCost, "full set 8+10+3");
            Check(FurnitureRules.Buy(s.Data, "table").Success, "buy table");
            Check(FurnitureRules.Buy(s.Data, "chair").Success, "buy chair 1");
            Check(FurnitureRules.Buy(s.Data, "chair").Success, "buy chair 2");
            Check(FurnitureRules.Buy(s.Data, "plant").Success, "buy plant");
            Equal(walletBefore - 21, s.Data.Coins, "furniture spent 21 from wallet");
            Check(FurnitureRules.Place(s.Data, "table", "house0").Success, "place table");
            Check(FurnitureRules.Place(s.Data, "chair", "house1").Success, "place chair 1");
            Check(FurnitureRules.Place(s.Data, "chair", "house2").Success, "place chair 2");
            Check(!FurnitureRules.Place(s.Data, "chair", "house2").Success, "occupied pad rejected");
            Check(FurnitureRules.Place(s.Data, "plant", "house3").Success, "place plant");
            Equal(4, s.Data.FurniturePlacements.Count, "four furniture placements");
            var moved = FurnitureRules.PickUp(s.Data, "house3");
            Check(moved != null && moved.Type == "plant", "pick up plant");
            Check(FurnitureRules.Place(s.Data, "plant", "house4").Success, "move plant to new pad");
            Check(MissionRules.IsObjectiveComplete(s.Data, "M10"), "house objective complete");
            var completion = s.CompleteActive();
            Check(completion.Success, "M10 complete");
            Equal(20, completion.CoinsAwarded, "M10 reward");
        });

        Run("M10 free starter set prevents a currency softlock", () =>
        {
            var data = new GameData { Coins = 0 };
            foreach (string id in new[] { "M01", "M02", "M03", "M04", "M07", "M08" })
                data.CompletedMissions.Add(id);
            var s = new MissionService(data, catalog);
            Check(s.StartMission("M10").Success, "start M10 with no coins");
            Check(MissionRules.HasHouseFurniture(data) == false, "not furnished");
            var free = FurnitureRules.GrantFreeStarterSet(data);
            Check(free.Success, "free starter set granted");
            Equal(0, data.Coins, "free set costs no coins");
            Check(FurnitureRules.Place(data, "table", "house0").Success, "free table placed");
            Check(FurnitureRules.Place(data, "chair", "house1").Success, "free chair 1 placed");
            Check(FurnitureRules.Place(data, "chair", "house2").Success, "free chair 2 placed");
            Check(FurnitureRules.Place(data, "plant", "house3").Success, "free plant placed");
            var completion = s.CompleteActive();
            Check(completion.Success, "free set still completes");
            Equal(20, completion.CoinsAwarded, "free set mission reward");
            var again = FurnitureRules.GrantFreeStarterSet(data);
            Check(!again.Success, "free set cannot be farmed for spare items");
        });

        Run("all ten jobs form a completable graph", () =>
        {
            var s = CompleteM08(Fresh(catalog));
            s = CompleteM04(s);
            Equal(MissionStatus.Available, s.GetStatus("M05"), "M05 after bridge");
            Check(s.StartMission("M05").Success, "start M05");
            for (int gate = 1; gate <= 4; gate++)
                Check(s.AdvanceCheckpoint(gate).Success, $"M05 gate {gate}");
            Check(s.CompleteActive().Success, "M05 complete");
            Equal(MissionStatus.Available, s.GetStatus("M09"), "M09 after M05");

            Equal(MissionStatus.Available, s.GetStatus("M06"), "M06 after M04");
            Check(s.StartMission("M06").Success, "start M06");
            Check(s.CollectPickup("blue_parcel_0", "blue_parcel", "M06", 2).Success, "blue 1");
            Check(s.CollectPickup("blue_parcel_1", "blue_parcel", "M06", 2).Success, "blue 2");
            Check(s.CollectPickup("orange_parcel_0", "orange_parcel", "M06", 1).Success, "orange");
            Check(s.DeliverBlueParcels().Success, "blue delivered");
            Check(s.DeliverOrangeParcel().Success, "orange delivered");
            Check(s.CompleteActive().Success, "M06 complete");

            Check(s.StartMission("M09").Success, "start M09");
            for (int gate = 1; gate <= 4; gate++)
                Check(s.AdvanceCheckpoint(gate).Success, $"M09 gate {gate}");
            var m09 = s.CompleteActive(0);
            Check(m09.Success, "M09 complete");
            Equal(15, m09.CoinsAwarded, "M09 base reward");

            Equal(MissionStatus.Available, s.GetStatus("M10"), "M10 after M04 + M08");
            Check(s.StartMission("M10").Success, "start M10");
            Check(FurnitureRules.GrantFreeStarterSet(s.Data).Success, "free furniture avoids softlock");
            Check(FurnitureRules.Place(s.Data, "table", "house0").Success, "table");
            Check(FurnitureRules.Place(s.Data, "chair", "house1").Success, "chair 1");
            Check(FurnitureRules.Place(s.Data, "chair", "house2").Success, "chair 2");
            Check(FurnitureRules.Place(s.Data, "plant", "house3").Success, "plant");
            var m10 = s.CompleteActive();
            Check(m10.Success, "M10 complete");
            Check(m10.AllComplete, "all-complete flag set");
            Equal(10, s.Data.CompletedMissions.Distinct().Count(), "ten distinct completions");
            Equal(MissionStatus.Completed, s.GetStatus("M09"), "M09 stays completed");
            int wallet = s.Data.Coins;
            Check(!s.CompleteActive().Success, "cannot claim an eleventh reward");
            Equal(wallet, s.Data.Coins, "wallet unchanged after repeat");
        });

        Run("save files round trip atomically and keep a backup", () =>
        {
            string dir = TempDirectory("patchwork-save-roundtrip");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                var data = new GameData { Coins = 47, ActiveMission = "M03" };
                data.CompletedMissions.Add("M01");
                data.BridgePieces.Add("support_0");
                data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
                data.FurnitureOwned["chair"] = 2;
                data.Player = new PlayerSnapshot { X = 3, Y = 4, Z = 5, RotationY = 1.5f, Valid = true };
                data.Van = new VehicleSnapshot { X = 7, Y = 0, Z = 8, RotationY = 0.5f, Valid = true };
                data.Settings.MusicEnabled = false;
                data.Settings.ReducedMotion = true;

                SaveService.Save(data, path);
                Check(File.Exists(path), "main save written");
                Check(!File.Exists(path + ".tmp"), "temporary file cleaned up");
                var loaded = SaveService.Load(path);
                Check(loaded.Success, "save loaded");
                Equal(47, loaded.Data.Coins, "coins restored");
                Equal("M03", loaded.Data.ActiveMission, "active mission restored");
                Equal(1, loaded.Data.BridgePieces.Count, "bridge pieces restored");
                Equal("bench", loaded.Data.ParkPlacements[0].Type, "park placement restored");
                Equal(2, loaded.Data.FurnitureOwned["chair"], "furniture owned restored");
                Equal(3f, loaded.Data.Player.X, "player restored");
                Equal(8f, loaded.Data.Van.Z, "van restored");
                Check(!loaded.Data.Settings.MusicEnabled, "settings restored");
                Check(loaded.Data.Settings.ReducedMotion, "reduced motion restored");
                Check(loaded.Data.SavedAtUnix > 0, "timestamp written");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("damaged save recovers the backup and quarantines the bad file", () =>
        {
            string dir = TempDirectory("patchwork-save-backup");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                SaveService.Save(new GameData { Coins = 12 }, path);
                File.Copy(path, SaveService.BackupPath(path), true);
                File.WriteAllText(path, "{ this is not valid json");
                var loaded = SaveService.Load(path);
                Check(loaded.Success, "backup recovered");
                Check(loaded.UsedBackup, "backup flag set");
                Check(loaded.RecoveredFromCorrupt, "corrupt flag set");
                Equal(12, loaded.Data.Coins, "backup coins restored");
                Check(!File.Exists(path), "corrupt main moved aside");
                Check(Directory.GetFiles(dir, "save.json.corrupt-*").Length == 1, "corrupt file kept for inspection");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("missing and newer saves are handled safely", () =>
        {
            string dir = TempDirectory("patchwork-save-invalid");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                var missing = SaveService.Load(path);
                Check(!missing.Success, "missing save is not loaded");
                File.WriteAllText(path, "{\"SchemaVersion\":99,\"Coins\":5}");
                var newer = SaveService.Load(path);
                Check(!newer.Success, "newer version rejected");
                Check(newer.Message.Contains("newer"), "newer version message");
                Check(File.Exists(path), "newer save must not be moved away as corrupt");
                Check(Directory.GetFiles(dir, "*.corrupt-*").Length == 0, "newer save must not be quarantined");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("structurally invalid nested records are rejected", () =>
        {
            string dir = TempDirectory("patchwork-save-structure");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                File.WriteAllText(path, "{\"SchemaVersion\":1,\"ParkPlacements\":[null]}");
                var loaded = SaveService.Load(path);
                Check(!loaded.Success, "null park placement must not load as a valid game");
                Check(loaded.Message.Length > 0, "malformed save reports why it failed");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("unknown and conflicting placements are rejected", () =>
        {
            string dir = TempDirectory("patchwork-save-placement");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                File.WriteAllText(path,
                    "{\"SchemaVersion\":1,\"ParkPlacements\":[" +
                    "{\"Type\":\"bench\",\"Slot\":\"park0\"}," +
                    "{\"Type\":\"bench\",\"Slot\":\"park9\"}]}");
                Check(!SaveService.Load(path).Success, "unknown pad and duplicate item rejected");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("invalid mission references and counters are rejected", () =>
        {
            string dir = TempDirectory("patchwork-save-missions");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                File.WriteAllText(path, "{\"SchemaVersion\":1,\"ActiveMission\":\"M99\"}");
                Check(!SaveService.Load(path, catalog).Success, "unknown active mission rejected");

                File.WriteAllText(path,
                    "{\"SchemaVersion\":1,\"ActiveMission\":\"M05\",\"MissionProgress\":{\"M05\":{\"checkpoint\":-2}}}");
                Check(!SaveService.Load(path, catalog).Success, "negative checkpoint rejected");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("a corrupt main save never replaces a good backup", () =>
        {
            string dir = TempDirectory("patchwork-save-backup-protect");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                SaveService.Save(new GameData { Coins = 33 }, path);
                File.Copy(path, SaveService.BackupPath(path), true);
                File.WriteAllText(path, "{\"SchemaVersion\":1,\"ParkPlacements\":[null]}");
                var loaded = SaveService.Load(path);
                Check(loaded.Success && loaded.UsedBackup, "good backup recovered");
                Equal(33, loaded.Data.Coins, "backup coins");
                Check(File.Exists(SaveService.BackupPath(path)), "backup remains in place");
                var backupLoaded = SaveService.Load(SaveService.BackupPath(path));
                Check(backupLoaded.Success && backupLoaded.Data.Coins == 33, "backup still valid after recovery");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("saving over a corrupt main keeps the good backup", () =>
        {
            string dir = TempDirectory("patchwork-save-corrupt-write");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                SaveService.Save(new GameData { Coins = 33 }, path, catalog);
                SaveService.Save(new GameData { Coins = 33 }, path, catalog);
                File.WriteAllText(path, "{\"SchemaVersion\":1,\"ParkPlacements\":[null]}");
                SaveService.Save(new GameData { Coins = 44 }, path, catalog);
                var main = SaveService.Load(path, catalog);
                Check(main.Success, "new main loads");
                Equal(44, main.Data.Coins, "new main coins");
                var backup = SaveService.Load(SaveService.BackupPath(path), catalog);
                Check(backup.Success, "backup still loads");
                Equal(33, backup.Data.Coins, "good backup was not replaced by corrupt data");
                Check(Directory.GetFiles(dir, "save.json.corrupt-*").Length >= 1,
                    "corrupt main was preserved for inspection");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("a minimal historical save gets safe defaults", () =>
        {
            string dir = TempDirectory("patchwork-save-minimal");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                File.WriteAllText(path, "{\"SchemaVersion\":0}");
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, "old/minimal save loads");
                Equal(0, loaded.Data.Coins, "default coins");
                Check(loaded.Data.Settings.MusicEnabled, "default settings");
                Check(loaded.Data.Player != null && loaded.Data.Van != null, "snapshots defaulted");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("unrecoverable corruption is reported, not loaded", () =>
        {
            string dir = TempDirectory("patchwork-save-unrecoverable");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                File.WriteAllText(path, "{ broken");
                File.WriteAllText(SaveService.BackupPath(path), "{ also broken");
                var loaded = SaveService.Load(path, catalog);
                Check(!loaded.Success, "no data loaded");
                Equal(SaveLoadStatus.Unrecoverable, loaded.Status, "unrecoverable status");
                Check(loaded.Message.Contains("backup"), "message explains the failed recovery");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("completed world jobs require their physical state", () =>
        {
            var data = new GameData();
            data.CompletedMissions.Add("M02");
            data.CompletedMissions.Add("M03");
            var validation = SaveValidation.Validate(data, catalog);
            Check(!validation.IsValid, "M03 without bridge rejected");
            data.BridgePieces.AddRange(BuildRules.BridgeSockets);
            Check(SaveValidation.Validate(data, catalog).IsValid, "M03 with bridge accepted");
            data.CompletedMissions.Add("M07");
            Check(!SaveValidation.Validate(data, catalog).IsValid, "M07 without swings rejected");
            data.SwingPieces.AddRange(BuildRules.SwingSockets);
            Check(SaveValidation.Validate(data, catalog).IsValid, "M07 with swings accepted");
        });

        Run("a failed write leaves the previous save intact", () =>
        {
            string dir = TempDirectory("patchwork-save-write-failure");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                SaveService.Save(new GameData { Coins = 21 }, path);
                string before = File.ReadAllText(path);
                bool threw = false;
                if (OperatingSystem.IsWindows())
                {
                    // The permission-based write failure is Linux/macOS specific.
                    threw = true;
                }
                else
                {
                    // Make the directory non-writable for this process; the atomic
                    // temporary file cannot be created, so the old save must remain.
                    File.SetUnixFileMode(dir,
                        UnixFileMode.UserRead | UnixFileMode.UserExecute);
                    try { SaveService.Save(new GameData { Coins = 99 }, path); }
                    catch (Exception) { threw = true; }
                    finally
                    {
                        File.SetUnixFileMode(dir,
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                    }
                }
                Check(threw, "failed write reports an error");
                Equal(before, File.ReadAllText(path), "previous save bytes unchanged");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("save/reload cannot duplicate a mission reward", () =>
        {
            string dir = TempDirectory("patchwork-save-reward");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                var s = CompleteM01(Fresh(catalog));
                SaveService.Save(s.Data, path);
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, "save loaded with catalog");
                var restored = new MissionService(loaded.Data, catalog);
                Equal(10, restored.Data.Coins, "coins restored");
                var repeat = restored.DeliverApples();
                Check(!repeat.Success, "completed delivery rejected after reload");
                Equal(10, restored.Data.Coins, "no duplicate reward");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("game state survives a JSON round trip", () =>
        {
            var s = CompleteM01(Fresh(catalog));
            s.Data.SetMissionCounter("M09", "checkpoint", 2);
            s.Data.BridgePieces.Add("support_0");
            s.Data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
            s.Data.FurnitureOwned["chair"] = 2;
            s.Data.Player = new PlayerSnapshot { X = 1, Y = 2, Z = 3, RotationY = 4, Valid = true };
            s.Data.Van = new VehicleSnapshot { X = 5, Y = 6, Z = 7, RotationY = 8, Valid = true };
            string jsonOut = JsonSerializer.Serialize(s.Data);
            var restored = JsonSerializer.Deserialize<GameData>(jsonOut);
            restored.Normalise();
            Equal(10, restored.Coins, "coins round trip");
            Check(restored.IsCompleted("M01"), "completion round trip");
            Equal(2, restored.GetMissionCounter("M09", "checkpoint"), "counters round trip");
            Equal(1, restored.BridgePieces.Count, "bridge list round trip");
            Equal("bench", restored.ParkPlacements[0].Type, "park placement round trip");
            Equal(2, restored.FurnitureOwned["chair"], "furniture owned round trip");
            Equal(1f, restored.Player.X, "player X round trip");
            Equal(5f, restored.Van.X, "van X round trip");
        });

        Run("control ownership has one deterministic mode", () =>
        {
            Equal(ControlMode.Title, ControlModeRules.Resolve(false, false, false, true), "title wins");
            Equal(ControlMode.Modal, ControlModeRules.Resolve(true, true, true, true), "modal wins");
            Equal(ControlMode.Building, ControlModeRules.Resolve(true, false, true, true), "building wins over driving");
            Equal(ControlMode.Driving, ControlModeRules.Resolve(true, false, false, true), "driving");
            Equal(ControlMode.Walking, ControlModeRules.Resolve(true, false, false, false), "walking");
            Check(ControlModeRules.PlayerProcessesInput(ControlMode.Walking), "walking owns player input");
            Check(!ControlModeRules.PlayerProcessesInput(ControlMode.Driving), "driving releases player input");
            Check(ControlModeRules.VehicleProcessesInput(ControlMode.Driving), "driving owns van input");
            Check(!ControlModeRules.VehicleProcessesInput(ControlMode.Modal), "modal releases van input");
            Check(ControlModeRules.CapturesMouse(ControlMode.Walking), "walking captures mouse");
            Check(!ControlModeRules.CapturesMouse(ControlMode.Building), "building shows mouse");
            Check(ControlModeRules.PausesWorld(ControlMode.Modal), "modal pauses world");
            Check(!ControlModeRules.PausesWorld(ControlMode.Driving), "driving does not pause");
        });

        Run("race elapsed time survives save and reload", () =>
        {
            string dir = TempDirectory("patchwork-save-race");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                var s = CompleteM04(CompleteBridge(Fresh(catalog)));
                Check(s.StartMission("M05").Success, "start M05");
                Check(s.AdvanceCheckpoint(1).Success, "gate 1");
                Check(s.AdvanceCheckpoint(2).Success, "gate 2");
                s.Data.SetMissionCounter("M05", "elapsedMs", 42_000);
                SaveService.Save(s.Data, path, catalog);

                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, "mid-race save loads");
                Equal(2, loaded.Data.GetMissionCounter("M05", "checkpoint"), "checkpoint restored");
                var timer = RaceTimer.Restore(loaded.Data, "M05");
                Equal(42f, timer.ElapsedSeconds, "elapsed time restored");
                timer.Tick(3.5f);
                Equal(45.5f, timer.ElapsedSeconds, "timer continues rather than resetting");
                Equal(5, timer.OptionalBonus("M05"), "bonus still earned inside 90 s");

                var restored = new MissionService(loaded.Data, catalog);
                Check(restored.AdvanceCheckpoint(3).Success, "gate 3 after reload");
                Check(restored.AdvanceCheckpoint(4).Success, "gate 4 after reload");
                var completion = restored.CompleteActive(timer.OptionalBonus("M05"));
                Check(completion.Success, "race completes after reload");
                Equal(15, completion.CoinsAwarded, "base race reward");
                Equal(5, completion.BonusCoins, "optional bonus");
                Equal(20, completion.TotalCoins, "total race reward");
                Check(!restored.CompleteActive(0).Success, "race reward cannot repeat");

                SaveService.Save(restored.Data, path, catalog);
                var reloaded = SaveService.Load(path, catalog);
                Check(reloaded.Success, "completed race reloads");
                Check(reloaded.Data.IsCompleted("M05"), "M05 stays complete");
                Check(!new MissionService(reloaded.Data, catalog).CompleteActive(0).Success,
                    "reloaded race cannot claim again");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("mission catalogue is complete and matches the content source", () =>
        {
            Check(MissionIntegrity.ValidateCatalog(catalog, out string error), "catalogue valid: " + error);
            var reference = MissionCatalog.FromJson(
                File.ReadAllText(Path.Combine(root, "MISSIONS.json")));
            Equal(reference.Missions.Count, catalog.Missions.Count, "reference mission count");
            foreach (var expected in reference.Missions)
            {
                var actual = catalog.Get(expected.Id);
                Check(actual != null, $"runtime mission {expected.Id} exists");
                Equal(expected.Title, actual.Title, $"{expected.Id} title");
                Equal(expected.Short, actual.Short, $"{expected.Id} instruction");
                Equal(expected.RewardCoins, actual.RewardCoins, $"{expected.Id} reward");
                Equal(string.Join(",", expected.Prerequisites),
                    string.Join(",", actual.Prerequisites), $"{expected.Id} prerequisites");
                Equal(string.Join("|", expected.Hints),
                    string.Join("|", actual.Hints), $"{expected.Id} hints");
            }
        });

        Run("missing mission content fails integrity validation", () =>
        {
            var incomplete = new MissionCatalog(catalog.Missions.Where(m => m.Id != "M10"));
            Check(!MissionIntegrity.ValidateCatalog(incomplete, out string error), "missing M10 rejected");
            Check(error.Contains("M10"), "error names the missing mission");
        });

        Run("invalid numeric data is refused before writing", () =>
        {
            string dir = TempDirectory("patchwork-save-numeric");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                var data = new GameData
                {
                    Player = new PlayerSnapshot { X = float.NaN, Y = 1, Z = 1, Valid = true },
                };
                bool threw = false;
                try { SaveService.Save(data, path, catalog); }
                catch (InvalidDataException) { threw = true; }
                Check(threw, "non-finite player position rejected");
                Check(!File.Exists(path), "invalid data was not written");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("explicit overwrite preserves a newer save", () =>
        {
            string dir = TempDirectory("patchwork-save-newer");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                File.WriteAllText(path, "{\"SchemaVersion\":99,\"Coins\":5}");
                SaveService.Save(new GameData { Coins = 1 }, path, catalog);
                var preserved = Directory.GetFiles(dir, "save.json.newer-v99-*");
                Equal(1, preserved.Length, "newer save preserved before overwrite");
                Check(File.ReadAllText(preserved[0]).Contains("\"SchemaVersion\":99"),
                    "preserved copy keeps the original version");
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, "replacement save loads");
                Equal(1, loaded.Data.Coins, "replacement coins");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("missing main save recovers the backup", () =>
        {
            string dir = TempDirectory("patchwork-save-missing-main");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                SaveService.Save(new GameData { Coins = 18 }, path, catalog);
                SaveService.Save(new GameData { Coins = 24 }, path, catalog);
                File.Delete(path);
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, "backup loaded when main is missing");
                Check(loaded.UsedBackup, "backup flag set");
                Equal(18, loaded.Data.Coins, "previous save restored");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Console.WriteLine();
        Console.WriteLine($"RESULT: {_passed} passed, {_failed} failed");
        if (_failed == 0) return 0;
        foreach (var failure in Failures)
            Console.WriteLine("FAILED: " + failure);
        return 1;
    }

    private static MissionService Fresh(MissionCatalog catalog) => new(new GameData(), catalog);

    private static string TempDirectory(string prefix)
    {
        string path = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static MissionService CompleteM01(MissionService service)
    {
        service.StartMission("M01");
        for (int i = 0; i < 3; i++)
            service.CollectPickup($"apple_{i}", "apple", "M01", 3);
        var result = service.DeliverApples();
        if (!result.Success) throw new Exception("M01 setup completion failed: " + result.Message);
        return service;
    }

    private static MissionService CompleteM07(MissionService service)
    {
        CompleteBridge(service);
        var started = service.StartMission("M07");
        if (!started.Success) throw new Exception("M07 setup start failed: " + started.Message);
        foreach (string id in new[] { "s1_rope_l", "s1_rope_r", "s2_rope_l", "s2_rope_r" })
            service.PlaceSwingPiece("rope", id);
        service.PlaceSwingPiece("seat", "s1_seat");
        service.PlaceSwingPiece("seat", "s2_seat");
        var result = service.CompleteActive(0);
        if (!result.Success) throw new Exception("M07 setup completion failed: " + result.Message);
        return service;
    }

    private static MissionService CompleteM08(MissionService service)
    {
        CompleteM07(service);
        var started = service.StartMission("M08");
        if (!started.Success) throw new Exception("M08 setup start failed: " + started.Message);
        ParkRules.Place(service.Data, "bench", "park0");
        ParkRules.Place(service.Data, "flowerbed", "park1");
        ParkRules.Place(service.Data, "lamp", "park2");
        var result = service.CompleteActive(0);
        if (!result.Success) throw new Exception("M08 setup completion failed: " + result.Message);
        return service;
    }

    private static MissionService CompleteM04(MissionService service)
    {
        CompleteM01(service);
        var started = service.StartMission("M04");
        if (!started.Success) throw new Exception("M04 setup start failed: " + started.Message);
        service.AddGrocery("bread");
        service.AddGrocery("bread");
        service.AddGrocery("milk");
        var result = service.DeliverGroceries();
        if (!result.Success) throw new Exception("M04 setup completion failed: " + result.Message);
        return service;
    }

    private static MissionService CompleteBridge(MissionService service)
    {
        CompleteM02(service);
        var started = service.StartMission("M03");
        if (!started.Success) throw new Exception("M03 setup start failed: " + started.Message);
        for (int i = 0; i < 3; i++)
            service.PlaceBridgePiece("support", $"support_{i}");
        for (int i = 0; i < 4; i++)
            service.PlaceBridgePiece("plank", $"plank_{i}");
        var result = service.CompleteActive(0);
        if (!result.Success) throw new Exception("M03 setup completion failed: " + result.Message);
        return service;
    }

    private static MissionService CompleteM02(MissionService service)
    {
        var started = service.StartMission("M02");
        if (!started.Success) throw new Exception("M02 setup start failed: " + started.Message);
        for (int i = 0; i < 5; i++)
            service.CollectPickup($"wood_{i}", "wood", "M02", 5);
        for (int i = 0; i < 3; i++)
            service.CollectPickup($"stone_{i}", "stone", "M02", 3);
        var result = service.SubmitSupplies();
        if (!result.Success) throw new Exception("M02 setup completion failed: " + result.Message);
        return service;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine($"PASS  {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Failures.Add($"{name}: {ex.Message}");
            Console.WriteLine($"FAIL  {name}: {ex.Message}");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"{message}: expected {expected}, got {actual}");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data", "missions.json")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
