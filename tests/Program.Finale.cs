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
    private static void FinaleTests(MissionCatalog catalog, CityMissionCatalog cityCatalog, string root, string json)
    {
        Run("M16 loads once, preserves wrong parcels and pays 20 once", () =>
        {
            var s = CompleteFirstHalf(Fresh(catalog));
            Equal(MissionStatus.Available, s.GetStatus("M16"), "M16 after M14 and M15");
            int wallet = s.Data.Coins;
            Check(!s.DeliverParcel("ada", "ada").Success, "delivery before loading is refused");
            Check(s.StartMission("M16").Success, "start M16");
            Equal(0, s.Data.GetMissionCounter("M16", "loaded"), "nothing loaded yet");
            Check(s.LoadDeliveryParcels().Success, "load the three parcels");
            Equal(1, s.Data.GetMissionCounter("M16", "loaded"), "parcels loaded once");
            Check(!s.LoadDeliveryParcels().Success, "parcels cannot be loaded twice");
            Equal(wallet, s.Data.Coins, "loading spends no personal coins");

            var wrong = s.DeliverParcel("ada", "pip");
            Check(!wrong.Success, "wrong parcel refused");
            Equal(1, s.Data.GetMissionCounter("M16", "carrying_pip"), "wrong parcel stays carried");
            Equal(0, s.Data.GetMissionCounter("M16", "delivered_ada"), "wrong parcel not delivered");
            Equal(wallet, s.Data.Coins, "wrong parcel spends nothing");

            Check(s.DeliverParcel("ada", "ada").Success, "Ada parcel delivered");
            Check(s.DeliverParcel("pip", "pip").Success, "Pip parcel delivered");
            var last = s.DeliverParcel("ivy", "ivy");
            Check(last.Success && last.Completion != null, "third parcel completes M16");
            Equal(20, last.Completion.CoinsAwarded, "M16 reward");
            Equal(wallet + 20, s.Data.Coins, "M16 pays once");
            Equal(MissionStatus.Available, s.GetStatus("M17"), "M17 opens after M16");
            Equal(MissionStatus.Locked, s.GetStatus("M18"), "M18 waits for M17");
            Check(!s.DeliverParcel("ivy", "ivy").Success, "repeat parcel refused");
            Equal(wallet + 20, s.Data.Coins, "repeat parcel pays nothing");
        });

        Run("M17 uses a separate 20-coin purse and pays 22 once", () =>
        {
            var s = CompleteM16(CompleteFirstHalf(Fresh(catalog)));
            Equal(MissionStatus.Available, s.GetStatus("M17"), "M17 after M16");
            s.Data.Coins = 0;
            int wallet = 0;
            Check(s.StartMission("M17").Success, "start M17 with an empty personal wallet");
            Check(s.AddMarketItem("bread").Success, "bread 1");
            Check(s.AddMarketItem("bread").Success, "bread 2");
            Check(s.AddMarketItem("milk").Success, "milk 1");
            Check(s.AddMarketItem("apple").Success, "apple 1");
            Check(s.AddMarketItem("apple").Success, "apple 2");
            Equal(14, FinaleRules.MarketSpent(s.Data), "job total 14");
            Equal(6, FinaleRules.MarketChange(s.Data), "job change 6");
            Equal(wallet, s.Data.Coins, "market basket never uses personal coins");

            Check(s.AddMarketItem("bread").Success, "extra bread still inside budget");
            Check(s.AddMarketItem("bread").Success, "second extra bread reaches 20");
            var over = s.AddMarketItem("milk");
            Check(!over.Success, "over-budget item refused");
            Equal(20, FinaleRules.MarketSpent(s.Data), "total stays at 20");
            Equal(wallet, s.Data.Coins, "over-budget refusal spends nothing");
            Check(!s.FinaliseMarket().Success, "wrong basket cannot finalise");
            Equal(0, s.Data.GetMissionCounter("M17", "finalised"), "failed checkout not recorded");
            Equal(wallet, s.Data.Coins, "failed checkout spends nothing");
            Check(s.RemoveMarketItem("bread").Success, "refund extra bread 1");
            Check(s.RemoveMarketItem("bread").Success, "refund extra bread 2");
            Equal(14, FinaleRules.MarketSpent(s.Data), "total returns to 14");
            Check(s.RemoveMarketItem("apple").Success, "remove an apple");
            Check(!s.FinaliseMarket().Success, "incomplete basket cannot finalise");
            Check(s.AddMarketItem("apple").Success, "put apple back");
            var completion = s.FinaliseMarket();
            Check(completion.Success && completion.Completion != null, "exact basket completes M17");
            Equal(22, completion.Completion.CoinsAwarded, "M17 reward");
            Equal(wallet + 22, s.Data.Coins, "M17 pays once");
            Equal(MissionStatus.Available, s.GetStatus("M18"), "M18 opens after M17");
            Check(!s.FinaliseMarket().Success, "repeat checkout refused");
            Equal(wallet + 22, s.Data.Coins, "repeat checkout pays nothing");
        });

        Run("M18 enforces tyre-lamp-battery order without losing parts", () =>
        {
            var s = CompleteM17(CompleteM16(CompleteFirstHalf(Fresh(catalog))));
            Equal(MissionStatus.Available, s.GetStatus("M18"), "M18 after M17");
            int wallet = s.Data.Coins;
            Check(s.StartMission("M18").Success, "start M18");
            Check(s.InspectVehicle().Success, "inspect succeeds");
            Check(!s.FitRepairPart("tyre").Success, "cannot fit an uncollected tyre");
            Check(s.CollectRepairPart("tyre", "part_tyre_0").Success, "tyre 1 collected");
            Check(s.Data.HasCollected("part_tyre_0"), "tyre 1 pickup recorded");
            Check(!s.CollectRepairPart("tyre", "part_tyre_0").Success,
                "same tyre pickup cannot be collected twice");
            Check(!s.CollectRepairPart("lamp", "part_tyre_0").Success,
                "pickup must match its part type");
            Check(s.CollectRepairPart("tyre", "part_tyre_1").Success, "tyre 2 collected");
            Check(!s.CollectRepairPart("tyre", "part_tyre_1").Success, "second tyre not duplicated");
            Equal(2, FinaleRules.HasPart(s.Data, "tyre"), "two tyres carried");

            Check(s.FitRepairPart("tyre").Success, "tyre 1 fitted");
            Check(!s.FitRepairPart("lamp").Success, "lamp before both tyres refused");
            Equal(0, FinaleRules.HasPart(s.Data, "lamp"), "lamp not lost");
            Check(s.FitRepairPart("tyre").Success, "tyre 2 fitted");
            Check(!s.FitRepairPart("battery").Success, "battery before lamp refused");
            Equal(0, FinaleRules.HasPart(s.Data, "battery"), "battery not lost");
            Check(s.CollectRepairPart("lamp", "part_lamp_0").Success, "lamp collected");
            Check(s.FitRepairPart("lamp").Success, "lamp fitted");
            Check(s.CollectRepairPart("battery", "part_battery_0").Success, "battery collected");
            var completion = s.FitRepairPart("battery");
            Check(completion.Success && completion.Completion != null, "battery completes M18");
            Equal(24, completion.Completion.CoinsAwarded, "M18 reward");
            Equal(wallet + 24, s.Data.Coins, "M18 pays once");
            Equal(MissionStatus.Available, s.GetStatus("M19"), "M19 opens after M18");
            Check(FinaleRules.M18Complete(s.Data), "Pip's car recorded repaired");
            Check(!s.FitRepairPart("battery").Success, "repeat repair refused");
            Equal(wallet + 24, s.Data.Coins, "repeat repair pays nothing");
        });

        Run("M18 partial pickup survives save and cannot respawn", () =>
        {
            var s = CompleteM17(CompleteM16(CompleteFirstHalf(Fresh(catalog))));
            Check(s.StartMission("M18").Success, "start M18 before partial save");
            Check(s.CollectRepairPart("tyre", "part_tyre_0").Success, "collect first tyre");
            Equal(1, FinaleRules.HasPart(s.Data, "tyre"), "one tyre carried before save");

            string dir = TempDirectory("m18-partial");
            try
            {
                string path = Path.Combine(dir, "save.json");
                SaveService.Save(s.Data, path, catalog);
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, loaded.Message);
                Equal("M18", loaded.Data.ActiveMission, "active M18 preserved");
                Equal(1, FinaleRules.HasPart(loaded.Data, "tyre"), "carried tyre preserved");
                Check(loaded.Data.HasCollected("part_tyre_0"), "collected tyre pickup preserved");

                // v1.2.0 wrote the count but not the pickup ID. Normalise must
                // infer exactly one tyre pickup so the same part cannot respawn.
                loaded.Data.CollectedPickups.Remove("part_tyre_0");
                string legacyPath = Path.Combine(dir, "legacy.json");
                SaveService.Save(loaded.Data, legacyPath, catalog);
                var legacy = SaveService.Load(legacyPath, catalog);
                Check(legacy.Success, legacy.Message);
                Equal(1, legacy.Data.CollectedPickups.Count(id =>
                        FinaleRules.RepairPickupPart(id) == "tyre"),
                    "legacy partial save infers one acquired tyre pickup");
                Check(legacy.Data.HasCollected("part_tyre_0"),
                    "legacy partial save records the first tyre pickup");

                // If the fixed game already recorded the second tyre, the
                // normaliser must not add the first one as well.
                loaded.Data.CollectedPickups.Remove("part_tyre_0");
                loaded.Data.CollectedPickups.Add("part_tyre_1");
                string recordedPath = Path.Combine(dir, "recorded.json");
                SaveService.Save(loaded.Data, recordedPath, catalog);
                var recorded = SaveService.Load(recordedPath, catalog);
                Check(recorded.Success, recorded.Message);
                Check(!recorded.Data.HasCollected("part_tyre_0") &&
                      recorded.Data.HasCollected("part_tyre_1"),
                    "normaliser keeps an already recorded second tyre only");
                Equal(1, recorded.Data.CollectedPickups.Count(id =>
                        FinaleRules.RepairPickupPart(id) == "tyre"),
                    "normaliser never invents an extra tyre pickup");

                var resumed = new MissionService(legacy.Data, catalog);
                Check(!resumed.CollectRepairPart("tyre", "part_tyre_0").Success,
                    "reloaded tyre pickup cannot be collected again");
                Check(resumed.CollectRepairPart("tyre", "part_tyre_1").Success,
                    "the second real tyre is still available");
                Equal(2, FinaleRules.HasPart(resumed.Data, "tyre"), "both tyres carried after resume");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("M19 has six ordered gates and a 155-second optional medal", () =>
        {
            var s = CompleteM18(CompleteM17(CompleteM16(CompleteFirstHalf(Fresh(catalog)))));
            Equal(MissionStatus.Available, s.GetStatus("M19"), "M19 after M18");
            int wallet = s.Data.Coins;
            Check(s.StartMission("M19").Success, "start M19");
            Check(!s.AdvanceCheckpoint(2).Success, "gate 2 before gate 1 refused");
            for (int gate = 1; gate <= 6; gate++)
                Check(s.AdvanceCheckpoint(gate).Success, $"M19 gate {gate}");
            Check(!s.AdvanceCheckpoint(6).Success, "duplicate final gate refused");
            Equal(6, s.Data.GetMissionCounter("M19", "checkpoint"), "six gates recorded");
            Equal(5, MissionRules.OptionalRaceBonus("M19", 155.0), "155 s medal earned");
            Equal(0, MissionRules.OptionalRaceBonus("M19", 155.001), "155.001 s medal refused");
            Equal(5, MissionRules.OptionalRaceBonus("M05", 90.0), "M05 boundary unchanged");
            Equal(0, MissionRules.OptionalRaceBonus("M05", 90.001), "M05 late bonus unchanged");
            Equal(5, MissionRules.OptionalRaceBonus("M09", 75.0), "M09 boundary unchanged");
            Equal(0, MissionRules.OptionalRaceBonus("M09", 75.001), "M09 late bonus unchanged");

            s.Data.SetMissionCounter("M19", "elapsedMs", 150_000);
            var completion = s.CompleteActive(5);
            Check(completion.Success, "M19 completes with earned medal");
            Equal(30, completion.TotalCoins, "M19 base 25 + medal 5");
            Equal(wallet + 30, s.Data.Coins, "M19 pays once");
            Equal(MissionStatus.Available, s.GetStatus("M20"), "M20 opens after M19");
            Check(!s.CompleteActive(0).Success, "repeat M19 refused");
            Equal(wallet + 30, s.Data.Coins, "repeat M19 pays nothing");
        });

        Run("M20 follows invite-table-lantern and pays 30 once", () =>
        {
            var s = CompleteM19(CompleteM18(CompleteM17(CompleteM16(CompleteFirstHalf(Fresh(catalog))))));
            Equal(MissionStatus.Available, s.GetStatus("M20"), "M20 after M19");
            int wallet = s.Data.Coins;
            Check(s.StartMission("M20").Success, "start M20");
            Equal(3, s.Data.GetMissionCounter("M20", "invite_stock"), "three invitations stocked");
            Equal(2, s.Data.GetMissionCounter("M20", "table_stock"), "two tables stocked");
            Equal(3, s.Data.GetMissionCounter("M20", "lantern_stock"), "three lanterns stocked");
            Check(!s.PlaceFestivalTable(0).Success, "table before invitations refused");
            Check(!s.HangFestivalLantern(0).Success, "lantern before invitations refused");

            Check(s.InviteForFestival("ada").Success, "invite Ada");
            Check(s.InviteForFestival("pip").Success, "invite Pip");
            Check(s.InviteForFestival("ivy").Success, "invite Ivy");
            Equal(0, s.Data.GetMissionCounter("M20", "invite_stock"), "invitations used");
            Check(!s.InviteForFestival("ada").Success, "duplicate invitation refused");
            Check(!s.HangFestivalLantern(0).Success, "lantern before tables refused");

            Check(s.PlaceFestivalTable(0).Success, "table 1 placed");
            Check(s.PlaceFestivalTable(1).Success, "table 2 placed");
            Check(s.RemoveFestivalTable(0).Success, "table 1 removed for retry");
            Equal(1, s.Data.GetMissionCounter("M20", "table_stock"), "table stock refunded");
            Check(s.PlaceFestivalTable(0).Success, "table 1 placed again");
            Check(s.HangFestivalLantern(0).Success, "lantern 1 hung");
            Check(s.RemoveFestivalLantern(0).Success, "lantern 1 removed for retry");
            Equal(3, s.Data.GetMissionCounter("M20", "lantern_stock"), "lantern stock refunded");
            Check(s.HangFestivalLantern(0).Success, "lantern 1 hung again");
            Check(s.HangFestivalLantern(1).Success, "lantern 2 hung");
            var last = s.HangFestivalLantern(2);
            Check(last.Success && last.Completion != null, "lantern 3 completes M20");
            Equal(30, last.Completion.CoinsAwarded, "M20 reward");
            Equal(wallet + 30, s.Data.Coins, "M20 pays once");
            Check(last.Completion.OriginalChapterComplete, "original finale marker preserved");
            Check(last.Completion.AllTwentyComplete, "all twenty jobs complete");
            Check(FinaleRules.M20Complete(s.Data), "festival world state complete");
            Check(!s.HangFestivalLantern(2).Success, "repeat lantern refused");
            Equal(wallet + 30, s.Data.Coins, "repeat festival pays nothing");
        });

        Run("M19 six-gate progress survives save and reload", () =>
        {
            var s = CompleteM18(CompleteM17(CompleteM16(CompleteFirstHalf(Fresh(catalog)))));
            Check(s.StartMission("M19").Success, "start M19");
            for (int gate = 1; gate <= 3; gate++)
                Check(s.AdvanceCheckpoint(gate).Success, $"M19 pre-save gate {gate}");
            s.Data.SetMissionCounter("M19", "elapsedMs", 150_000);

            string dir = TempDirectory("m19-progress");
            try
            {
                string path = Path.Combine(dir, "save.json");
                SaveService.Save(s.Data, path, catalog);
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, loaded.Message);
                Equal(3, loaded.Data.GetMissionCounter("M19", "checkpoint"),
                    "M19 checkpoint preserved");
                Equal(150_000, loaded.Data.GetMissionCounter("M19", "elapsedMs"),
                    "M19 time preserved");
                Equal("M19", loaded.Data.ActiveMission, "active M19 preserved");
                Check(!MissionRules.IsObjectiveComplete(loaded.Data, "M19"),
                    "M19 still in progress");
                var resumed = new MissionService(loaded.Data, catalog);
                for (int gate = 4; gate <= 6; gate++)
                    Check(resumed.AdvanceCheckpoint(gate).Success, $"M19 resumed gate {gate}");
                Check(FinaleRules.M19Complete(resumed.Data), "six gates complete after reload");
                Equal(5, resumed.CompleteActive(5).BonusCoins, "earned medal survives reload");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("finale partial progress saves and reloads", () =>
        {
            var s = CompleteM19(CompleteM18(CompleteM17(CompleteM16(CompleteFirstHalf(Fresh(catalog))))));
            Check(s.StartMission("M20").Success, "start M20 before saving");
            Check(s.InviteForFestival("ada").Success, "invite Ada before saving");
            Check(s.InviteForFestival("pip").Success, "invite Pip before saving");
            Check(s.InviteForFestival("ivy").Success, "invite Ivy before saving");
            Check(s.PlaceFestivalTable(0).Success, "place table 1 before saving");
            int wallet = s.Data.Coins;

            string dir = TempDirectory("finale-save");
            try
            {
                string path = Path.Combine(dir, "save.json");
                SaveService.Save(s.Data, path, catalog);
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, loaded.Message);
                Equal("M20", loaded.Data.ActiveMission, "active M20 preserved");
                Equal(1, loaded.Data.GetMissionCounter("M20", "invited_ada"), "Ada invite preserved");
                Equal(1, loaded.Data.GetMissionCounter("M20", "table_0"), "table preserved");
                Equal(1, loaded.Data.GetMissionCounter("M20", "table_stock"), "table stock preserved");
                Equal(3, loaded.Data.GetMissionCounter("M20", "lantern_stock"), "lantern stock preserved");
                Equal(wallet, loaded.Data.Coins, "finale wallet preserved");
                Check(!loaded.Data.FestivalSeen, "festival unseen while M20 is active");

                var resumed = new MissionService(loaded.Data, catalog);
                Check(resumed.PlaceFestivalTable(1).Success, "resume table 2");
                Check(resumed.HangFestivalLantern(0).Success, "resume lantern 1");
                Check(resumed.HangFestivalLantern(1).Success, "resume lantern 2");
                var last = resumed.HangFestivalLantern(2);
                Check(last.Success && last.Completion != null, "resume completes M20");
                Equal(30, last.Completion.CoinsAwarded, "resumed M20 reward");
                Equal(wallet + 30, resumed.Data.Coins, "resumed festival pays once");
                resumed.Data.FestivalSeen = true;
                Check(SaveValidation.Validate(resumed.Data, catalog).IsValid,
                    "festival marker is valid after M20");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("all twenty jobs complete in one clean-save walkthrough", () =>
        {
            var s = CompleteM20(CompleteM19(CompleteM18(CompleteM17(CompleteM16(
                CompleteFirstHalf(Fresh(catalog)))))));
            foreach (string id in FinaleRules.MissionIds)
                Check(s.Data.IsCompleted(id), $"{id} completed in the full walkthrough");
            Equal(20, s.Data.CompletedMissions.Distinct().Count(),
                "twenty distinct mission completions");
            Check(s.Data.Coins > 0, "full walkthrough earned coins");
            Equal("", s.Data.ActiveMission, "no job left active");
            Check(MissionRules.OriginalChapterComplete(s.Data),
                "original chapter remains complete");
            Check(FinaleRules.M20Complete(s.Data), "festival world state complete");

            string dir = TempDirectory("full-release-save");
            try
            {
                string path = Path.Combine(dir, "save.json");
                s.Data.FestivalSeen = true;
                s.Data.CelebrationSeen = true;
                SaveService.Save(s.Data, path, catalog);
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, loaded.Message);
                Equal(20, loaded.Data.CompletedMissions.Count,
                    "full release save round-trips twenty completions");
                Check(loaded.Data.FestivalSeen && loaded.Data.CelebrationSeen,
                    "both finale markers round-trip");
                Equal(s.Data.Coins, loaded.Data.Coins,
                    "full release wallet round-trips");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

    }
}
