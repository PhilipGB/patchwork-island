using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PatchworkIsland;

namespace PatchworkIsland.Tests;

internal static partial class Program
{
    private static void PlacementAndRaceTests(MissionCatalog catalog, CityMissionCatalog cityCatalog, string root, string json)
    {
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

    }
}
