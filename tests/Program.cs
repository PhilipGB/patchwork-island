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

        Run("fresh customisation state is safe and child-friendly", () =>
        {
            var data = new GameData();
            Equal(CharacterCustomisation.Boy, data.CharacterPreset, "default preset");
            Equal(CharacterCustomisation.DefaultOutfit, data.EquippedOutfit, "default outfit");
            Equal(0, data.OwnedOutfits.Count, "no purchased outfits");
            Check(CharacterCustomisation.Owns(data, CharacterCustomisation.DefaultOutfit),
                "starting outfit is always owned");
            Check(!CharacterCustomisation.Owns(data, "sunny"), "shop outfit starts unowned");
            Equal(5, CharacterCustomisation.Outfits.Count, "five outfit choices including Everyday");
            Equal(4, CharacterCustomisation.Outfits.Count(o => o.Price > 0), "four purchasable outfits");
            Check(CharacterCustomisation.StyleFor(CharacterCustomisation.Girl,
                      CharacterCustomisation.DefaultOutfit).MainHex !=
                  CharacterCustomisation.StyleFor(CharacterCustomisation.Boy,
                      CharacterCustomisation.DefaultOutfit).MainHex,
                "boy and girl starting looks are visually distinct");
        });

        Run("buying an outfit deducts coins once, owns it and wears it", () =>
        {
            var data = new GameData { Coins = 10 };
            var bought = CharacterCustomisation.BuyOutfit(data, "sunny");
            Check(bought.Success, "sunny bought");
            Equal(2, data.Coins, "price deducted once");
            Equal("sunny", data.EquippedOutfit, "purchase is worn immediately");
            Check(data.OwnedOutfits.Contains("sunny"), "outfit owned");
            Equal(1, data.OwnedOutfits.Count(o => o == "sunny"), "one ownership record");

            var repeat = CharacterCustomisation.BuyOutfit(data, "sunny");
            Check(!repeat.Success, "already-owned outfit cannot be bought twice");
            Equal(2, data.Coins, "repeat purchase spends nothing");
            Equal(1, data.OwnedOutfits.Count(o => o == "sunny"), "ownership stays unique");
        });

        Run("insufficient coins cannot buy an outfit", () =>
        {
            var data = new GameData { Coins = 7 };
            var result = CharacterCustomisation.BuyOutfit(data, "sunny");
            Check(!result.Success, "8-coin outfit refused with 7 coins");
            Check(result.Message.Contains("Not enough coins"), "message is explicit");
            Equal(7, data.Coins, "wallet unchanged");
            Equal(0, data.OwnedOutfits.Count, "nothing owned");
            Equal(CharacterCustomisation.DefaultOutfit, data.EquippedOutfit, "nothing equipped");
        });

        Run("only owned outfits can be equipped", () =>
        {
            var data = new GameData { Coins = 0 };
            Check(CharacterCustomisation.EquipOutfit(data, CharacterCustomisation.DefaultOutfit).Success,
                "free starting outfit can be worn");
            var unowned = CharacterCustomisation.EquipOutfit(data, "meadow");
            Check(!unowned.Success, "unowned outfit cannot be worn");
            Equal(CharacterCustomisation.DefaultOutfit, data.EquippedOutfit, "unowned refusal changes nothing");

            data.Coins = 20;
            Check(CharacterCustomisation.BuyOutfit(data, "meadow").Success, "buy meadow");
            Check(CharacterCustomisation.EquipOutfit(data, CharacterCustomisation.DefaultOutfit).Success,
                "switch back to starting outfit");
            Check(CharacterCustomisation.EquipOutfit(data, "meadow").Success,
                "owned meadow can be worn again for free");
            Equal("meadow", data.EquippedOutfit, "owned outfit equipped");
        });

        Run("gender preset changes presentation without spending coins", () =>
        {
            var data = new GameData { Coins = 9 };
            Check(CharacterCustomisation.SetPreset(data, CharacterCustomisation.Girl).Success, "choose girl");
            Equal(CharacterCustomisation.Girl, data.CharacterPreset, "girl preset saved");
            Equal(9, data.Coins, "preset change is free");
            Check(!CharacterCustomisation.SetPreset(data, "robot").Success, "unknown preset rejected");
            Equal(CharacterCustomisation.Girl, data.CharacterPreset, "rejected preset leaves choice alone");
        });

        Run("customisation survives a save/load round trip", () =>
        {
            string dir = TempDirectory("patchwork-customisation-roundtrip");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                var data = new GameData { Coins = 17, CharacterPreset = CharacterCustomisation.Girl };
                data.OwnedOutfits.Add("sunny");
                data.OwnedOutfits.Add("berry");
                data.EquippedOutfit = "berry";
                SaveService.Save(data, path, catalog);

                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, "customisation save loads");
                Equal(CharacterCustomisation.Girl, loaded.Data.CharacterPreset, "preset restored");
                Equal(2, loaded.Data.OwnedOutfits.Count, "owned outfits restored");
                Check(loaded.Data.OwnedOutfits.Contains("sunny"), "sunny restored");
                Check(loaded.Data.OwnedOutfits.Contains("berry"), "berry restored");
                Equal("berry", loaded.Data.EquippedOutfit, "equipped outfit restored");
                Equal(17, loaded.Data.Coins, "wallet restored unchanged");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("older saves default customisation safely", () =>
        {
            string dir = TempDirectory("patchwork-customisation-old-save");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                // A representative v1.0.x save: no customisation fields at all.
                File.WriteAllText(path,
                    "{\"SchemaVersion\":1,\"Coins\":24,\"CompletedMissions\":[\"M01\"]," +
                    "\"TutorialsSeen\":[\"walk\"]}");
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, "older save still loads");
                Equal(24, loaded.Data.Coins, "older wallet preserved");
                Equal(CharacterCustomisation.Boy, loaded.Data.CharacterPreset, "boy is the safe old-save default");
                Equal(CharacterCustomisation.DefaultOutfit, loaded.Data.EquippedOutfit,
                    "starting outfit is the safe old-save default");
                Equal(0, loaded.Data.OwnedOutfits.Count, "older save owns no shop outfits");

                File.WriteAllText(path, "{\"SchemaVersion\":0}");
                var minimal = SaveService.Load(path, catalog);
                Check(minimal.Success, "minimal historical save still loads");
                Equal(CharacterCustomisation.Boy, minimal.Data.CharacterPreset, "minimal save defaults preset");
                Equal(CharacterCustomisation.DefaultOutfit, minimal.Data.EquippedOutfit,
                    "minimal save defaults outfit");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("schema 1 saves migrate to schema 2 with all progress intact", () =>
        {
            string dir = TempDirectory("patchwork-save-schema-migration");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                File.WriteAllText(path,
                    "{\"SchemaVersion\":1,\"Coins\":24,\"ActiveMission\":\"M03\"," +
                    "\"CompletedMissions\":[\"M01\",\"M02\"]," +
                    "\"TutorialsSeen\":[\"walk\",\"drive\"]," +
                    "\"CollectedPickups\":[\"apple_0\",\"apple_1\"]," +
                    "\"Inventory\":{\"apple\":1}," +
                    "\"Player\":{\"X\":3.5,\"Y\":0.2,\"Z\":-7.5,\"RotationY\":1.25,\"Valid\":true}," +
                    "\"Settings\":{\"MusicEnabled\":false,\"ReducedMotion\":true}}");

                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, "schema 1 save loads");
                Equal(SaveService.CurrentVersion, loaded.Data.SchemaVersion,
                    "schema 1 is migrated to the current schema in memory");
                Equal(24, loaded.Data.Coins, "wallet survives migration");
                Equal("M03", loaded.Data.ActiveMission, "active mission survives migration");
                Equal(2, loaded.Data.CompletedMissions.Count, "completed missions survive migration");
                Check(loaded.Data.CompletedMissions.Contains("M01") &&
                      loaded.Data.CompletedMissions.Contains("M02"),
                    "both completed mission ids survive migration");
                Equal(2, loaded.Data.TutorialsSeen.Count, "tutorial flags survive migration");
                Equal(2, loaded.Data.CollectedPickups.Count, "pickup ids survive migration");
                Equal(1, loaded.Data.GetItem("apple"), "inventory survives migration");
                Equal(3.5f, loaded.Data.Player.X, "player position survives migration");
                Check(!loaded.Data.Settings.MusicEnabled && loaded.Data.Settings.ReducedMotion,
                    "settings survive migration");
                Equal(CharacterCustomisation.Boy, loaded.Data.CharacterPreset,
                    "missing character preset defaults to Boy");
                Equal(CharacterCustomisation.DefaultOutfit, loaded.Data.EquippedOutfit,
                    "missing outfit defaults to Everyday");
                Equal(0, loaded.Data.OwnedOutfits.Count, "missing outfit purchases default to none");

                SaveService.Save(loaded.Data, path, catalog);
                using (var document = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    Equal(2, document.RootElement.GetProperty("SchemaVersion").GetInt32(),
                        "rewritten save uses schema 2");
                }

                Check(File.Exists(SaveService.BackupPath(path)),
                    "the pre-migration schema 1 save is kept as backup");
                using (var backup = JsonDocument.Parse(File.ReadAllText(SaveService.BackupPath(path))))
                {
                    Equal(1, backup.RootElement.GetProperty("SchemaVersion").GetInt32(),
                        "backup still contains the original schema 1 save");
                }

                var reloaded = SaveService.Load(path, catalog);
                Check(reloaded.Success, "schema 2 save loads");
                Equal(SaveService.CurrentVersion, reloaded.Data.SchemaVersion,
                    "schema 2 remains current");
                Equal(24, reloaded.Data.Coins, "wallet survives the schema 2 rewrite");
                Equal("M03", reloaded.Data.ActiveMission, "active mission survives the schema 2 rewrite");
                Equal(2, reloaded.Data.CompletedMissions.Count,
                    "completed missions survive the schema 2 rewrite");
                Equal(2, reloaded.Data.TutorialsSeen.Count, "tutorials survive the schema 2 rewrite");
                Equal(2, reloaded.Data.CollectedPickups.Count, "pickups survive the schema 2 rewrite");
                Equal(1, reloaded.Data.GetItem("apple"), "inventory survives the schema 2 rewrite");
                Equal(3.5f, reloaded.Data.Player.X, "player position survives the schema 2 rewrite");
                Check(!reloaded.Data.Settings.MusicEnabled && reloaded.Data.Settings.ReducedMotion,
                    "settings survive the schema 2 rewrite");
                Equal(CharacterCustomisation.Boy, reloaded.Data.CharacterPreset,
                    "migrated preset remains Boy");
                Equal(CharacterCustomisation.DefaultOutfit, reloaded.Data.EquippedOutfit,
                    "migrated outfit remains Everyday");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("invalid customisation data is rejected, not silently repaired", () =>
        {
            var unknownPreset = new GameData { CharacterPreset = "robot" };
            Check(!SaveValidation.Validate(unknownPreset, catalog).IsValid, "unknown preset rejected");

            var unknownOutfit = new GameData();
            unknownOutfit.OwnedOutfits.Add("mystery_hat");
            Check(!SaveValidation.Validate(unknownOutfit, catalog).IsValid, "unknown owned outfit rejected");

            var unowned = new GameData { EquippedOutfit = "sunny" };
            Check(!SaveValidation.Validate(unowned, catalog).IsValid, "wearing an unowned outfit rejected");

            var duplicate = new GameData();
            duplicate.OwnedOutfits.Add("sunny");
            duplicate.OwnedOutfits.Add("sunny");
            duplicate.EquippedOutfit = "sunny";
            Check(!SaveValidation.Validate(duplicate, catalog).IsValid, "duplicate ownership rejected");
        });

        Run("new game data clears purchased outfits", () =>
        {
            var old = new GameData { Coins = 44, CharacterPreset = CharacterCustomisation.Girl };
            old.OwnedOutfits.Add("sunny");
            old.EquippedOutfit = "sunny";
            Equal(1, old.OwnedOutfits.Count, "old session has an outfit");

            var reset = new GameData(); // same reset used by Main.ResetSession
            Equal(CharacterCustomisation.Boy, reset.CharacterPreset, "reset preset");
            Equal(CharacterCustomisation.DefaultOutfit, reset.EquippedOutfit, "reset outfit");
            Equal(0, reset.OwnedOutfits.Count, "reset clears purchased outfits");
            Equal(0, reset.Coins, "reset clears wallet");
        });

        Run("job rewards stay exact after buying clothes", () =>
        {
            var s = Fresh(catalog);
            CompleteM01(s);
            Equal(10, s.Data.Coins, "M01 reward");
            Check(CharacterCustomisation.BuyOutfit(s.Data, "sunny").Success, "buy sunny for 8");
            Equal(2, s.Data.Coins, "clothing purchase deducted from the wallet");

            CompleteM02(s);
            Equal(14, s.Data.Coins, "M02 reward added exactly after clothing purchase");
            Equal(2, s.Data.CompletedMissions.Count, "both missions recorded");
            Check(!CharacterCustomisation.BuyOutfit(s.Data, "sunny").Success,
                "mission rewards cannot be used to rebuy an owned outfit");
            Equal(14, s.Data.Coins, "repeat clothing purchase does not touch reward coins");
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
                Equal(SaveService.CurrentVersion, loaded.Data.SchemaVersion,
                    "new saves use the current schema");
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

        Run("incompatible schema 3 and 99 saves are rejected without modification", () =>
        {
            string dir = TempDirectory("patchwork-save-invalid");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                var missing = SaveService.Load(path);
                Check(!missing.Success, "missing save is not loaded");

                foreach (int version in new[] { 3, 99 })
                {
                    // These values are deliberately incompatible with the
                    // current GameData types. The schema must be classified
                    // before typed deserialisation, otherwise the JsonException
                    // would look like corruption and could displace the file.
                    string original =
                        $"{{\"SchemaVersion\":{version},\"Coins\":\"100\"," +
                        "\"CharacterPreset\":[\"girl\"],\"Player\":{\"X\":\"left\"}}";
                    File.WriteAllText(path, original);
                    byte[] before = File.ReadAllBytes(path);

                    var newer = SaveService.Load(path, catalog);
                    Check(!newer.Success, $"schema {version} cannot be loaded");
                    Equal(SaveLoadStatus.UnsupportedNewer, newer.Status,
                        $"schema {version} is reported as unsupported newer");
                    Check(newer.Message.Contains($"version {version}") &&
                          newer.Message.Contains("newer"),
                        $"schema {version} message explains the refusal");
                    Check(File.Exists(path), $"schema {version} save must remain on disk");
                    Check(File.ReadAllBytes(path).SequenceEqual(before),
                        $"schema {version} load leaves every byte untouched");
                    Check(Directory.GetFiles(dir, "*.corrupt-*").Length == 0,
                        $"schema {version} must not be quarantined as corrupt");
                    Check(Directory.GetFiles(dir, "save.json.newer-v*").Length == 0,
                        $"schema {version} must not be renamed automatically");

                    var automatic = SaveService.TrySave(new GameData { Coins = 77 }, path, catalog);
                    Check(!automatic.Success && automatic.Outcome == SaveOutcome.InvalidState,
                        $"automatic save refuses to overwrite schema {version}");
                    Check(File.ReadAllBytes(path).SequenceEqual(before),
                        $"automatic save leaves schema {version} untouched");
                    Check(!File.Exists(SaveService.BackupPath(path)),
                        $"refused overwrite does not replace the backup for schema {version}");
                }
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
                // Occupy the atomic temporary path with a directory. This makes
                // the write fail deterministically on every platform, including
                // Windows, rather than relying on Unix permissions.
                Directory.CreateDirectory(path + ".tmp");
                var result = SaveService.TrySave(new GameData { Coins = 99 }, path, catalog);
                Check(!result.Success, "failed write reports an error");
                Equal(SaveOutcome.RecoverableFailure, result.Outcome, "I/O failure is recoverable");
                Equal(before, File.ReadAllText(path), "previous save bytes unchanged");
                Directory.Delete(path + ".tmp");
                var retry = SaveService.TrySave(new GameData { Coins = 99 }, path, catalog);
                Check(retry.Success, "retry after removing the obstacle succeeds");
                Equal(99, SaveService.Load(path, catalog).Data.Coins, "retry wrote the new state");
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
            string referenceJson = File.ReadAllText(Path.Combine(root, "MISSIONS.json"));
            Check(MissionContentConsistency.Compare(referenceJson, json, out string drift),
                "complete mission files match: " + drift);
            var reference = MissionCatalog.FromJson(referenceJson);
            Equal(reference.Missions.Count, catalog.Missions.Count, "reference mission count");
            Equal(JsonValueKind.Object, catalog.Get("M01").Mechanics.ValueKind,
                "authored mechanics parsed into the runtime DTO");
        });

        Run("mission consistency catches previously unchecked fields", () =>
        {
            // Normalise line endings so the negative mutations work on both
            // LF checkouts and Windows CRLF checkouts.
            string referenceJson = File.ReadAllText(Path.Combine(root, "MISSIONS.json"))
                .Replace("\r\n", "\n");
            string runtimeJson = json.Replace("\r\n", "\n");
            string changedLearning = referenceJson.Replace("count 1–3", "count 1-3");
            Check(changedLearning != referenceJson, "test input changed");
            Check(!MissionContentConsistency.Compare(changedLearning, runtimeJson, out string learningError),
                "learning drift is detected");
            Check(learningError.Contains("learning"), "error names the learning field");

            string changedMechanics = referenceJson.Replace(
                "\"required\": 3,\n        \"source\": \"market_crate\"",
                "\"required\": 4,\n        \"source\": \"market_crate\"");
            Check(changedMechanics != referenceJson, "mechanics test input changed");
            Check(!MissionContentConsistency.Compare(changedMechanics, runtimeJson, out string mechanicsError),
                "mechanics drift is detected");
            Check(mechanicsError.Contains("mechanics"), "error names the mechanics field");

            string changedMetadata = referenceJson.Replace(
                "\"world\": \"Patchwork Island Village\"",
                "\"world\": \"Wrong Island\"");
            Check(!MissionContentConsistency.Compare(changedMetadata, runtimeJson, out string metadataError),
                "top-level metadata drift is detected");
            Check(metadataError.Contains("world"), "error names the metadata field");
        });

        Run("mission consistency rejects duplicate object properties", () =>
        {
            string referenceJson = File.ReadAllText(Path.Combine(root, "MISSIONS.json"))
                .Replace("\r\n", "\n");
            string runtimeJson = json.Replace("\r\n", "\n");
            Check(MissionContentConsistency.Compare(referenceJson, runtimeJson, out string identicalError),
                "identical JSON compares equal: " + identicalError);
            Check(MissionContentConsistency.Compare(
                    "{\"first\":1,\"nested\":{\"b\":2,\"a\":1}}",
                    "{\"nested\":{\"a\":1,\"b\":2},\"first\":1}", out string reorderedError),
                "object property order is irrelevant: " + reorderedError);
            Check(!MissionContentConsistency.Compare("{\"items\":[1,2]}", "{\"items\":[2,1]}",
                    out string arrayOrderError), "array order remains significant");
            Check(arrayOrderError.Contains("items[0]"), "array order error names the index");

            string duplicateTop = referenceJson.Replace(
                "{\n  \"schemaVersion\"",
                "{\n  \"schemaVersion\": 1,\n  \"schemaVersion\"");
            Check(!MissionContentConsistency.Compare(duplicateTop, runtimeJson, out string topError),
                "duplicate top-level property rejected");
            Check(topError.Contains("schemaVersion") && topError.Contains("duplicate"),
                "top-level error names the duplicate property: " + topError);

            string duplicateNested = referenceJson.Replace(
                "\"mechanics\": {\n        \"item\": \"apple\"",
                "\"mechanics\": {\n        \"item\": \"apple\",\n        \"item\": \"apple\"");
            Check(!MissionContentConsistency.Compare(duplicateNested, runtimeJson, out string nestedError),
                "duplicate nested property rejected");
            Check(nestedError.Contains("mechanics") && nestedError.Contains("item"),
                "nested error names the path: " + nestedError);

            string duplicateArrayElement = referenceJson.Replace(
                "{\n      \"id\": \"M01\"",
                "{\n      \"id\": \"M01\",\n      \"id\": \"M01\"");
            Check(duplicateArrayElement != referenceJson, "array-element mutation changed the JSON");
            Check(!MissionContentConsistency.Compare(duplicateArrayElement, runtimeJson, out string arrayError),
                "duplicate property in an array element rejected");
            Check(arrayError.Contains("duplicate") && arrayError.Contains("missions[0]"),
                "array-element error names the path: " + arrayError);

            Check(!MissionContentConsistency.Compare("{ broken", runtimeJson, out string invalidError),
                "invalid JSON rejected");
            Check(invalidError.Contains("not valid JSON"), "invalid JSON explains the problem");
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
                var result = SaveService.TrySave(data, path, catalog);
                Check(!result.Success, "structured result reports failure");
                Equal(SaveOutcome.InvalidState, result.Outcome, "invalid state is distinguished from I/O");
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
                SaveService.Save(new GameData { Coins = 1 }, path, catalog,
                    allowNewerReplacement: true);
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

        Run("M10 rearrangement remains saveable after completion", () =>
        {
            string dir = TempDirectory("patchwork-save-furniture");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                var s = CompleteM10(Fresh(catalog));
                Check(s.Data.IsCompleted("M10"), "M10 completed");
                SaveService.Save(s.Data, path, catalog);
                var chair = FurnitureRules.PickUp(s.Data, "house1");
                Check(chair != null && chair.Type == "chair", "picked up a chair");
                Equal(2, FurnitureRules.Owned(s.Data, "chair"), "both chairs remain owned");
                Equal(3, s.Data.FurniturePlacements.Count, "one chair is unplaced");
                SaveService.Save(s.Data, path, catalog);
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, "save with an unplaced chair loads");
                Equal(2, FurnitureRules.Owned(loaded.Data, "chair"), "ownership survives");
                Equal(3, loaded.Data.FurniturePlacements.Count, "unplaced state survives");
                Check(FurnitureRules.Place(loaded.Data, "chair", "house4").Success, "chair placed on a new pad");
                SaveService.Save(loaded.Data, path, catalog);
                var reloaded = SaveService.Load(path, catalog);
                Check(reloaded.Success, "new arrangement loads");
                Check(reloaded.Data.FurniturePlacements.Any(p => p.Type == "chair" && p.Slot == "house4"),
                    "new chair pad persisted");
                Check(reloaded.Data.IsCompleted("M10"), "M10 stays complete");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("completed M10 with every piece unplaced still saves", () =>
        {
            string dir = TempDirectory("patchwork-save-furniture-empty");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                var s = CompleteM10(Fresh(catalog));
                foreach (string slot in new[] { "house0", "house1", "house2", "house3" })
                    Check(FurnitureRules.PickUp(s.Data, slot) != null, "picked up " + slot);
                Equal(0, s.Data.FurniturePlacements.Count, "no furniture placed");
                Check(FurnitureRules.OwnsRequiredSet(s.Data), "full set still owned");
                SaveService.Save(s.Data, path, catalog);
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, "emptied completed home saves and loads");
                Equal(0, loaded.Data.FurniturePlacements.Count, "emptied placements preserved");
                Check(FurnitureRules.OwnsRequiredSet(loaded.Data), "ownership preserved");
                Check(loaded.Data.IsCompleted("M10"), "M10 still complete");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("later mission progress survives an unplaced post-completion chair", () =>
        {
            string dir = TempDirectory("patchwork-save-furniture-progress");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                var s = CompleteM10(Fresh(catalog));
                FurnitureRules.PickUp(s.Data, "house1");
                Check(s.StartMission("M06").Success, "start M06 with a chair unplaced");
                s.CollectPickup("blue_parcel_0", "blue_parcel", "M06", 2);
                s.CollectPickup("blue_parcel_1", "blue_parcel", "M06", 2);
                s.CollectPickup("orange_parcel_0", "orange_parcel", "M06", 1);
                Check(s.DeliverBlueParcels().Success, "blue parcels delivered");
                Check(s.DeliverOrangeParcel().Success, "orange parcel delivered");
                var completion = s.CompleteActive(0);
                Check(completion.Success, "M06 completes");
                Equal(15, completion.CoinsAwarded, "M06 reward");
                SaveService.Save(s.Data, path, catalog);
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, "save after later mission loads");
                Check(loaded.Data.IsCompleted("M06"), "M06 completion persisted");
                Equal(3, loaded.Data.FurniturePlacements.Count, "chair still unplaced");
                Equal(2, FurnitureRules.Owned(loaded.Data, "chair"), "chair still owned");
                var restored = new MissionService(loaded.Data, catalog);
                Check(!restored.CompleteActive(0).Success, "no duplicate M06 reward");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("M10 still needs physical placements to complete", () =>
        {
            var s = CompleteM04(CompleteM08(Fresh(catalog)));
            Check(s.StartMission("M10").Success, "start M10");
            Check(FurnitureRules.Buy(s.Data, "table").Success, "buy table");
            Check(FurnitureRules.Buy(s.Data, "chair").Success, "buy chair 1");
            Check(FurnitureRules.Buy(s.Data, "chair").Success, "buy chair 2");
            Check(FurnitureRules.Buy(s.Data, "plant").Success, "buy plant");
            Check(!MissionRules.IsObjectiveComplete(s.Data, "M10"), "owned but unplaced does not complete M10");
            Check(!s.CompleteActive(0).Success, "completion refused without placements");
            FurnitureRules.Place(s.Data, "table", "house0");
            FurnitureRules.Place(s.Data, "chair", "house1");
            FurnitureRules.Place(s.Data, "chair", "house2");
            Check(!MissionRules.IsObjectiveComplete(s.Data, "M10"), "missing plant still blocks");
            FurnitureRules.Place(s.Data, "plant", "house3");
            Check(s.CompleteActive(0).Success, "placed set completes M10");
        });

        Run("long race times remain saveable and do not poison later saves", () =>
        {
            foreach (string raceId in new[] { "M05", "M09" })
            {
                string dir = TempDirectory("patchwork-save-long-race-" + raceId);
                string path = Path.Combine(dir, SaveService.DefaultFileName);
                try
                {
                    var s = raceId == "M05"
                        ? CompleteBridge(Fresh(catalog))
                        : CompleteM05(CompleteBridge(Fresh(catalog)));
                    var started = s.StartMission(raceId);
                    Check(started.Success, raceId + " starts");
                    foreach (int boundary in new[] { 3_599_000, 3_600_000, 3_600_001, 3_960_000, int.MaxValue })
                    {
                        s.Data.SetMissionCounter(raceId, "elapsedMs", boundary);
                        SaveService.Save(s.Data, path, catalog);
                        var loaded = SaveService.Load(path, catalog);
                        Check(loaded.Success, raceId + " saveable at " + boundary + " ms");
                        Equal(boundary, loaded.Data.GetMissionCounter(raceId, "elapsedMs"),
                            raceId + " elapsed restored at " + boundary);
                        var timer = RaceTimer.Restore(loaded.Data, raceId);
                        Check(timer.ElapsedSeconds >= 0, raceId + " non-negative at " + boundary);
                    }
                    s.AdvanceCheckpoint(1);
                    s.AdvanceCheckpoint(2);
                    s.AdvanceCheckpoint(3);
                    s.AdvanceCheckpoint(4);
                    var longTimer = RaceTimer.Restore(s.Data, raceId);
                    longTimer.Tick(60f);
                    longTimer.Store(s.Data, raceId);
                    int stored = s.Data.GetMissionCounter(raceId, "elapsedMs");
                    Check(stored > 0, raceId + " stored time positive");
                    var completion = s.CompleteActive(longTimer.OptionalBonus(raceId));
                    Check(completion.Success, raceId + " completes after a long race");
                    Equal(0, completion.BonusCoins, raceId + " no unfair bonus");
                    SaveService.Save(s.Data, path, catalog);
                    var completed = SaveService.Load(path, catalog);
                    Check(completed.Success, raceId + " completed save loads");
                    Check(completed.Data.IsCompleted(raceId), raceId + " stays complete");
                    Equal(15, completion.CoinsAwarded, raceId + " reward");
                }
                finally
                {
                    Directory.Delete(dir, true);
                }
            }
        });

        Run("switching away from a race preserves its elapsed time", () =>
        {
            var s = CompleteM04(CompleteBridge(Fresh(catalog)));
            Check(s.StartMission("M05").Success, "start M05");
            s.Data.SetMissionCounter("M05", "checkpoint", 1);
            s.Data.SetMissionCounter("M05", "elapsedMs", 20_000);
            Check(s.StartMission("M06").Success, "switch to M06");
            Check(s.StartMission("M05").Success, "switch back to M05");
            var timer = RaceTimer.Restore(s.Data, "M05");
            Equal(20.0, timer.ElapsedSeconds, "elapsed time survives mission switching");
            Equal(5, timer.OptionalBonus("M05"), "legitimate bonus still available");
            s.Data.SetMissionCounter("M05", "elapsedMs", 120_000);
            Equal(0, RaceTimer.Restore(s.Data, "M05").OptionalBonus("M05"),
                "long elapsed time cannot earn the bonus");
        });

        Run("race timer saturates instead of overflowing and round-trips", () =>
        {
            var timer = new RaceTimer();
            timer.Tick(66f * 60f);
            Equal(3960.0, Math.Round(timer.ElapsedSeconds, 3), "66 minutes retained");
            var data = new GameData();
            timer.Store(data, "M05");
            Equal(3_960_000, data.GetMissionCounter("M05", "elapsedMs"), "66 minutes stored as ms");
            var restored = RaceTimer.Restore(data, "M05");
            Check(restored.ElapsedSeconds >= timer.ElapsedSeconds, "reload does not shorten time");

            var huge = RaceTimer.Restore(new GameData(), "M05");
            huge.Tick(1e12f);
            huge.Store(data, "M05");
            int stored = data.GetMissionCounter("M05", "elapsedMs");
            Check(stored > 0, "huge time stored positive");
            Check(stored <= RaceTimer.MaxStoredMilliseconds, "stored within representation ceiling");
            var hugeRestored = RaceTimer.Restore(data, "M05");
            Check(hugeRestored.ElapsedSeconds >= 0, "huge restore non-negative");
            Check(hugeRestored.ElapsedSeconds <= RaceTimer.MaxStoredMilliseconds / 1000.0,
                "restore respects ceiling");
        });

        Run("optional race bonuses use exact boundaries", () =>
        {
            Equal(5, MissionRules.OptionalRaceBonus("M05", 90f), "M05 at 90 s");
            Equal(0, MissionRules.OptionalRaceBonus("M05", 90.001f), "M05 just over 90 s");
            Equal(5, MissionRules.OptionalRaceBonus("M09", 75f), "M09 at 75 s");
            Equal(0, MissionRules.OptionalRaceBonus("M09", 75.001f), "M09 just over 75 s");
            Equal(0, MissionRules.OptionalRaceBonus("M01", 1f), "other jobs have no bonus");
        });

        Run("mission completion rejects invalid bonus values", () =>
        {
            var s = CompleteBridge(Fresh(catalog));
            Check(s.StartMission("M05").Success, "start M05");
            s.AdvanceCheckpoint(1);
            s.AdvanceCheckpoint(2);
            s.AdvanceCheckpoint(3);
            s.AdvanceCheckpoint(4);
            int before = s.Data.Coins;
            var negative = s.CompleteActive(-50);
            Check(!negative.Success, "negative bonus rejected");
            Equal(before, s.Data.Coins, "negative bonus cannot reduce the wallet");
            var excessive = s.CompleteActive(500);
            Check(!excessive.Success, "excessive bonus rejected");
            Equal(before, s.Data.Coins, "excessive bonus cannot inflate the wallet");
            var legitimate = s.CompleteActive(5);
            Check(legitimate.Success, "legitimate bonus accepted");
            Equal(5, legitimate.BonusCoins, "bonus preserved");
            Equal(before + 20, s.Data.Coins, "base reward plus valid bonus");
        });

        Run("race bonuses are verified against the persisted race clock", () =>
        {
            foreach (string raceId in new[] { "M05", "M09" })
            {
                var s = raceId == "M05"
                    ? CompleteBridge(Fresh(catalog))
                    : CompleteM05(CompleteBridge(Fresh(catalog)));
                Check(s.StartMission(raceId).Success, raceId + " starts");
                s.AdvanceCheckpoint(1);
                s.AdvanceCheckpoint(2);
                s.AdvanceCheckpoint(3);
                s.AdvanceCheckpoint(4);

                int before = s.Data.Coins;
                s.Data.SetMissionCounter(raceId, "elapsedMs", 120_000);
                var slow = s.CompleteActive(5);
                Check(!slow.Success, raceId + " rejects a bonus on a slow race");
                Equal(before, s.Data.Coins, raceId + " slow race wallet unchanged");
                Check(s.Data.ActiveMission == raceId, raceId + " remains active after rejected bonus");

                int boundary = raceId == "M05" ? 90_000 : 75_000;
                s.Data.SetMissionCounter(raceId, "elapsedMs", boundary);
                var exact = s.CompleteActive(5);
                Check(exact.Success, raceId + " accepts the boundary bonus");
                Equal(5, exact.BonusCoins, raceId + " boundary bonus value");
            }
        });

        Run("swing placement rejects unknown part types", () =>
        {
            Check(!BuildRules.ValidateSwingPlacement("banana", "s1_rope_l", Array.Empty<string>(), out string message),
                "unknown part rejected");
            Check(message.Length > 0, "unknown part explains why");
            var s = CompleteBridge(Fresh(catalog));
            Check(s.StartMission("M07").Success, "start M07");
            var invalid = s.PlaceSwingPiece("banana", "s1_seat");
            Check(!invalid.Success, "mission cannot accept an unknown part");
            Equal(0, s.Data.SwingPieces.Count, "no socket consumed");
        });

        Run("furniture ownership is capped in the domain", () =>
        {
            var data = new GameData { Coins = 400 };
            foreach (var (item, required) in FurnitureRules.Required)
            {
                for (int i = 0; i < required; i++)
                    Check(FurnitureRules.Buy(data, item).Success, "buy required " + item);
                var extra = FurnitureRules.Buy(data, item);
                Check(!extra.Success, "cannot buy extra " + item);
                Equal(required, FurnitureRules.Owned(data, item), "owned count capped for " + item);
            }
        });

        Run("save feedback never confirms a failed manual save", () =>
        {
            var policy = new SaveFeedbackPolicy();
            var success = policy.Record(SaveReason.Manual, SaveResult.Ok());
            Check(success.Show && !success.IsError, "success is shown");
            Check(success.Text.Contains("saved"), "success wording");
            var failure = policy.Record(SaveReason.Manual, SaveResult.Recoverable("disk full"));
            Check(failure.Show && failure.IsError, "manual failure is shown as an error");
            Check(failure.Text.Contains("disk full"), "failure keeps the diagnostic");
            Check(!failure.Text.Contains("Game saved"), "failure must not claim success");
            var invalid = policy.Record(SaveReason.Manual, SaveResult.Invalid("invalid state"));
            Check(invalid.Show && invalid.IsError, "invalid state is reported");
        });

        Run("automatic save failures are visible and throttled until recovery", () =>
        {
            var policy = new SaveFeedbackPolicy();
            var first = policy.Record(SaveReason.Autosave, SaveResult.Recoverable("disk full"));
            Check(first.Show && first.IsError, "first autosave failure shown");
            var second = policy.Record(SaveReason.Autosave, SaveResult.Recoverable("disk full"));
            Check(!second.Show, "repeated autosave failure suppressed");
            var missionStart = policy.Record(SaveReason.MissionStart, SaveResult.Recoverable("disk full"));
            Check(!missionStart.Show, "throttle spans automatic save reasons");
            var success = policy.Record(SaveReason.Autosave, SaveResult.Ok());
            Check(!success.Show, "automatic success is quiet");
            var afterRecovery = policy.Record(SaveReason.Autosave, SaveResult.Recoverable("disk full"));
            Check(afterRecovery.Show, "a new failure after recovery is shown again");
        });

        Run("settings failures are visible without suppressing autosave warnings", () =>
        {
            var policy = new SaveFeedbackPolicy();
            var first = policy.Record(SaveReason.Settings, SaveResult.Recoverable("disk full"));
            Check(first.Show && first.IsError, "first settings failure is visible");
            Check(first.Text.Contains("session"), "settings message separates memory from disk");
            var repeated = policy.Record(SaveReason.Settings, SaveResult.Recoverable("disk full"));
            Check(!repeated.Show, "repeated settings failure is throttled");
            var autosave = policy.Record(SaveReason.Autosave, SaveResult.Recoverable("disk full"));
            Check(autosave.Show && autosave.Text.Contains("Autosave failed"),
                "settings failure does not suppress the first autosave warning");
            var anotherAuto = policy.Record(SaveReason.Autosave, SaveResult.Recoverable("disk full"));
            Check(!anotherAuto.Show, "autosave failure is still throttled");
            var recovered = policy.Record(SaveReason.Settings, SaveResult.Ok());
            Check(!recovered.Show, "successful settings save is quiet");
            var afterRecovery = policy.Record(SaveReason.Settings, SaveResult.Recoverable("disk full"));
            Check(afterRecovery.Show, "settings warning returns after a successful save");
        });

        Run("mission-completion save failure is always reported", () =>
        {
            var policy = new SaveFeedbackPolicy();
            policy.Record(SaveReason.Autosave, SaveResult.Recoverable("disk full"));
            var completion = policy.Record(SaveReason.MissionCompleted, SaveResult.Recoverable("disk full"));
            Check(completion.Show && completion.IsError, "completion failure bypasses autosave throttle");
            Check(completion.Text.Contains("progress"), "completion failure explains progress risk");
            var quit = policy.Record(SaveReason.Quit, SaveResult.Recoverable("disk full"));
            Check(quit.Show && quit.IsError, "quit failure is visible");
            var close = policy.Record(SaveReason.WindowClose, SaveResult.Recoverable("disk full"));
            Check(!close.Show, "window-close failure uses console diagnostics, not hidden UI");
        });

        Run("session save state does not claim a failed new game was persisted", () =>
        {
            var state = new SessionSaveState();
            state.ApplyLoad(new SaveLoadResult { Status = SaveLoadStatus.Missing });
            Check(!state.HasSaveFile && !state.CanContinue, "fresh state has no save");
            var failedFirst = state.RecordNewGameSave(
                SaveResult.Recoverable("disk full"), "new game not saved");
            Check(!failedFirst, "failed first new game");
            Check(!state.HasSaveFile && !state.CanContinue, "no false Continue after failed first save");
            Equal("new game not saved", state.TitleNotice, "title explains the failure");

            state.ApplyLoad(new SaveLoadResult
            {
                Status = SaveLoadStatus.UnsupportedNewer,
                Message = "Save version 99 is newer.",
            });
            Check(state.HasSaveFile && !state.CanContinue && state.NewerSavePresent,
                "newer save protected and not continuable");
            var failedReplace = state.RecordNewGameSave(
                SaveResult.Recoverable("disk full"), "replacement not saved");
            Check(!failedReplace, "failed replacement");
            Check(state.HasSaveFile && !state.CanContinue && state.NewerSavePresent,
                "failed replacement leaves the newer-save state intact");
            var replaced = state.RecordNewGameSave(SaveResult.Ok(), "unused");
            Check(replaced, "successful replacement");
            Check(state.HasSaveFile && state.CanContinue && !state.NewerSavePresent,
                "successful replacement updates persistence flags");
            Equal("", state.TitleNotice, "success clears the title warning");
        });

        Run("new game after backup recovery keeps Continue when the write fails", () =>
        {
            var state = new SessionSaveState();
            state.ApplyLoad(new SaveLoadResult
            {
                Status = SaveLoadStatus.RecoveredBackup,
                Data = new GameData { Coins = 25 },
                UsedBackup = true,
            });
            Check(state.CanContinue, "recovered save is continuable");
            state.RecordNewGameSave(SaveResult.Recoverable("disk full"), "new game not saved");
            Check(state.HasSaveFile && state.CanContinue, "recovered save remains available");
            Check(!state.MemoryMatchesPersisted, "failed new game is not the persisted session");
        });

        Run("discarding an unsaved session cannot continue it", () =>
        {
            var state = new SessionSaveState();
            state.ApplyLoad(new SaveLoadResult { Status = SaveLoadStatus.Loaded, Data = new GameData() });
            Check(state.MemoryMatchesPersisted, "loaded save matches memory");
            state.RecordSaveFailure();
            Check(!state.MemoryMatchesPersisted, "failed write marks memory unsaved");
            Check(state.CanContinue, "the old persisted save remains available");
            state.DiscardUnsavedSession();
            Check(!state.MemoryMatchesPersisted && state.CanContinue,
                "discarded memory remains distinct from the persisted save");
            state.RecordSaveSuccess();
            Check(state.MemoryMatchesPersisted, "a successful save re-establishes the match");
        });

        Run("newer replacement permission is session-scoped and retryable", () =>
        {
            var state = new SessionSaveState();
            state.ApplyLoad(new SaveLoadResult
            {
                Status = SaveLoadStatus.UnsupportedNewer,
                Message = "Save version 99 is newer.",
            });
            Check(state.NewerSavePresent && !state.CanContinue, "newer save protected");
            Check(!state.NewerReplacementAuthorized, "no permission before explicit confirmation");
            state.AuthorizeNewerReplacement();
            Check(state.NewerReplacementAuthorized, "explicit new game authorises replacement");
            state.RecordNewGameSave(SaveResult.Recoverable("disk full"), "new game not saved");
            Check(state.NewerSavePresent && state.NewerReplacementAuthorized,
                "failed replacement keeps permission for Save Now retries");
            Check(!state.MemoryMatchesPersisted, "failed replacement is not claimed as persisted");
            state.DiscardUnsavedSession();
            Check(!state.NewerReplacementAuthorized, "discard clears replacement permission");
            Check(state.NewerSavePresent, "discard does not pretend the newer save disappeared");
            state.ApplyLoad(new SaveLoadResult
            {
                Status = SaveLoadStatus.UnsupportedNewer,
                Message = "A different newer save.",
            });
            Check(!state.NewerReplacementAuthorized, "loading another newer save clears permission");
            state.AuthorizeNewerReplacement();
            state.RecordSaveSuccess();
            Check(!state.NewerSavePresent && !state.NewerReplacementAuthorized,
                "successful replacement clears protection and permission");
        });

        Run("forced save failures do not change the on-disk save", () =>
        {
            string dir = TempDirectory("patchwork-save-injected-failure");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                SaveService.Save(new GameData { Coins = 21 }, path, catalog);
                byte[] before = File.ReadAllBytes(path);
                SaveService.SetTestWriteFailure(full =>
                    string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                        ? new IOException("injected disk failure")
                        : null);
                var failed = SaveService.TrySave(new GameData { Coins = 99 }, path, catalog);
                Check(!failed.Success, "injected write fails");
                Equal(SaveOutcome.RecoverableFailure, failed.Outcome, "injected failure is a recoverable I/O error");
                Check(before.SequenceEqual(File.ReadAllBytes(path)), "injected failure leaves exact bytes intact");
                SaveService.ClearTestWriteFailure();
                var retry = SaveService.TrySave(new GameData { Coins = 99 }, path, catalog);
                Check(retry.Success, "retry after clearing the failure succeeds");
                Equal(99, SaveService.Load(path, catalog).Data.Coins, "retry persisted the new state");
            }
            finally
            {
                SaveService.ClearTestWriteFailure();
                Directory.Delete(dir, true);
            }
        });

    Run("audio manifest validates every shipped loop and effect", () =>
    {
        string assets = Path.Combine(root, "assets");
        Check(AudioCueCatalog.ValidateRepository(assets, out string error), error);
        Equal(4, AudioCueCatalog.Music.Count, "music loop count");
        Equal(28, AudioCueCatalog.Sounds.Count, "sound effect count");
    });

    Run("audio buses route music separately from effects and vehicles", () =>
    {
        Check(AudioCueCatalog.Buses.Contains(AudioCueCatalog.MusicBus), "music bus");
        Check(AudioCueCatalog.Buses.Contains(AudioCueCatalog.SfxBus), "sfx bus");
        Check(AudioCueCatalog.Buses.Contains(AudioCueCatalog.UiBus), "ui bus");
        Check(AudioCueCatalog.Buses.Contains(AudioCueCatalog.VehiclesBus), "vehicles bus");
        Check(AudioCueCatalog.FindMusic("title").Bus == AudioCueCatalog.MusicBus, "title music bus");
        Check(AudioCueCatalog.FindSound("ui_confirm").Bus == AudioCueCatalog.UiBus, "ui cue bus");
        Check(AudioCueCatalog.FindSound("engine_drive_loop").Bus == AudioCueCatalog.VehiclesBus,
            "engine cue bus");
        Check(AudioCueCatalog.FindSound("footstep_road_01").Bus == AudioCueCatalog.SfxBus,
            "world sfx bus");
    });

    Run("rally course is one continuous inside-island loop", () =>
    {
        var samples = RallyCourse.SampleClosed(20);
        Check(samples.Count >= 200, "sampled centreline");
        foreach (var point in samples)
            Check(RallyCourse.IsInsideIsland(point.X, point.Z),
                $"rally point outside island: {point.X:0.0},{point.Z:0.0}");
        float length = RallyCourse.Length();
        Check(length > 190f && length < 260f, $"rally length {length:0.0}");
        for (int i = 0; i < samples.Count; i++)
        {
            var a = samples[i];
            var b = samples[(i + 1) % samples.Count];
            float gap = MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
            Check(gap < 2.2f, $"rally centreline gap {gap:0.00}");
        }
    });

    Run("rally gate layouts preserve four-gate races and add six M19 gates", () =>
    {
        var four = RallyCourse.FourGates;
        var six = RallyCourse.SixGates;
        Equal(4, four.Count, "M05/M09 gate count");
        Equal(6, six.Count, "M19 gate count");
        for (int i = 0; i < four.Count; i++)
            Equal(i + 1, four[i].Number, "four-gate number");
        for (int i = 0; i < six.Count; i++)
            Equal(i + 1, six[i].Number, "six-gate number");
        foreach (var gate in four.Concat(six))
            Check(RallyCourse.NearestDistance(gate.X, gate.Z) < 0.45f,
                $"gate {gate.Number} is not on the centreline");
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

    private static MissionService CompleteM05(MissionService service)
    {
        var started = service.StartMission("M05");
        if (!started.Success) throw new Exception("M05 setup start failed: " + started.Message);
        for (int gate = 1; gate <= 4; gate++)
            service.AdvanceCheckpoint(gate);
        var result = service.CompleteActive(0);
        if (!result.Success) throw new Exception("M05 setup completion failed: " + result.Message);
        return service;
    }

    private static MissionService CompleteM10(MissionService service)
    {
        CompleteM04(service);
        CompleteM08(service);
        var started = service.StartMission("M10");
        if (!started.Success) throw new Exception("M10 setup start failed: " + started.Message);
        foreach (var (item, required) in FurnitureRules.Required)
            for (int i = 0; i < required; i++)
                if (!FurnitureRules.Buy(service.Data, item).Success)
                    FurnitureRules.GrantFreeStarterSet(service.Data);
        if (!FurnitureRules.Place(service.Data, "table", "house0").Success) throw new Exception("place table");
        if (!FurnitureRules.Place(service.Data, "chair", "house1").Success) throw new Exception("place chair 1");
        if (!FurnitureRules.Place(service.Data, "chair", "house2").Success) throw new Exception("place chair 2");
        if (!FurnitureRules.Place(service.Data, "plant", "house3").Success) throw new Exception("place plant");
        var result = service.CompleteActive(0);
        if (!result.Success) throw new Exception("M10 setup completion failed: " + result.Message);
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
