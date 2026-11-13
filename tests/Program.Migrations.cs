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
    private static void MigrationsTests(MissionCatalog catalog, CityMissionCatalog cityCatalog, string root, string json)
    {
        Run("v1.1 ten-complete save keeps possessions and opens M11", () =>
        {
            var old = BuildOriginalTenSave();
            old.SchemaVersion = 2;
            old.CharacterPreset = CharacterCustomisation.Girl;
            old.OwnedOutfits.Add("berry");
            old.EquippedOutfit = "berry";
            old.UnlockedCosmetics.Add("red_cap");
            old.TutorialsSeen.AddRange(new[] { "walk", "drive", "build" });
            old.Settings.MusicEnabled = false;
            old.Settings.ReducedMotion = true;
            old.Player = new PlayerSnapshot
            {
                X = -38.8f, Y = -0.04f, Z = 21.4f, RotationY = 3.14f, Valid = true,
            };
            old.Van = new VehicleSnapshot
            {
                X = -30f, Y = -0.05f, Z = 14.5f, RotationY = 0f, Valid = true,
            };
            var node = JsonSerializer.SerializeToNode(old,
                new JsonSerializerOptions { WriteIndented = true })!;
            node.AsObject().Remove("FestivalSeen");

            string dir = TempDirectory("v110-migration");
            try
            {
                string path = Path.Combine(dir, SaveService.DefaultFileName);
                File.WriteAllText(path, node.ToJsonString());
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, loaded.Message);
                Equal(SaveService.CurrentVersion, loaded.Data.SchemaVersion,
                    "v1.1 save migrates to the current schema");
                Equal(CharacterCustomisation.Girl, loaded.Data.CharacterPreset,
                    "girl preset preserved");
                Equal("berry", loaded.Data.EquippedOutfit, "worn outfit preserved");
                Check(loaded.Data.OwnedOutfits.Contains("berry"), "outfit ownership preserved");
                Check(loaded.Data.UnlockedCosmetics.Contains("red_cap"), "cosmetic preserved");
                Check(!loaded.Data.Settings.MusicEnabled && loaded.Data.Settings.ReducedMotion,
                    "settings preserved");
                Equal(130, loaded.Data.Coins, "wallet preserved");
                Equal(10, loaded.Data.CompletedMissions.Count, "ten completions preserved");
                Check(loaded.Data.CelebrationSeen, "original celebration preserved");
                Check(!loaded.Data.FestivalSeen, "new festival marker starts unseen");
                Equal(-38.8f, loaded.Data.Player.X, "player position preserved");
                Equal(-30f, loaded.Data.Van.X, "van position preserved");
                var service = new MissionService(loaded.Data, catalog);
                Equal(MissionStatus.Available, service.GetStatus("M11"),
                    "M11 unlocks immediately on a v1.1 save");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("chapter-two partial progress survives save and reload", () =>
        {
            var s = CompleteOriginalTen(Fresh(catalog));
            s.Data.CelebrationSeen = true;
            Check(s.StartMission("M11").Success, "start M11 before saving");
            Check(s.HandWelcomeCard("ada", "card_ada").Success, "deliver one card before saving");
            int wallet = s.Data.Coins;

            string dir = TempDirectory("chapter-two-save");
            try
            {
                string path = Path.Combine(dir, "save.json");
                SaveService.Save(s.Data, path, catalog);
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, loaded.Message);
                Equal(SaveService.CurrentVersion, loaded.Data.SchemaVersion, "current schema");
                Equal("M11", loaded.Data.ActiveMission, "active M11 preserved");
                Equal(1, loaded.Data.GetMissionCounter("M11", "delivered_ada"), "Ada preserved");
                Equal(0, loaded.Data.GetMissionCounter("M11", "card_ada"), "spent card preserved");
                Equal(1, loaded.Data.GetMissionCounter("M11", "card_ollie"), "Ollie card preserved");
                Equal(1, loaded.Data.GetMissionCounter("M11", "card_fern"), "Fern card preserved");
                Equal(wallet, loaded.Data.Coins, "wallet preserved");
                Check(loaded.Data.CelebrationSeen, "original M10 celebration preserved");
                Check(!loaded.Data.FestivalSeen, "second finale still unseen");
                Equal(10, loaded.Data.CompletedMissions.Count, "ten original completions preserved");

                var resumed = new MissionService(loaded.Data, catalog);
                Check(resumed.HandWelcomeCard("ollie", "card_ollie").Success, "resume Ollie");
                var last = resumed.HandWelcomeCard("fern", "card_fern");
                Check(last.Success && last.Completion != null, "resume completes M11");
                Equal(12, last.Completion.CoinsAwarded, "resumed reward");
                Equal(wallet + 12, resumed.Data.Coins, "resumed reward paid once");
                SaveService.Save(resumed.Data, path, catalog);
                var reloaded = SaveService.Load(path, catalog);
                Equal(11, reloaded.Data.CompletedMissions.Count, "M11 completion persisted");
                Equal(wallet + 12, reloaded.Data.Coins, "wallet after resumed completion");
                Check(!reloaded.Data.IsCompleted("M20"), "second chapter remains open");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("schema 3 village save gains closed-city defaults without losing progress", () =>
        {
            var old = BuildOriginalTenSave();
            old.SchemaVersion = 3;
            old.Coins = 77;
            var node = JsonSerializer.SerializeToNode(old,
                new JsonSerializerOptions { WriteIndented = true })!;
            foreach (string field in new[]
                     {
                         "CityRoadworksCompleted", "CityRoadworksRewardClaimed",
                         "CityRoadOpen", "CityVisited", "ActiveCityMission",
                         "CompletedCityMissions", "CityMissionProgress", "CitySeed",
                         "CityLogicalSeconds",
                     })
                node.AsObject().Remove(field);

            string dir = TempDirectory("schema3-city-migration");
            try
            {
                string path = Path.Combine(dir, SaveService.DefaultFileName);
                File.WriteAllText(path, node.ToJsonString());
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, loaded.Message);
                Equal(SaveService.CurrentVersion, loaded.Data.SchemaVersion,
                    "schema 3 is migrated to current");
                Equal(77, loaded.Data.Coins, "wallet preserved");
                Equal(10, loaded.Data.CompletedMissions.Count, "village progress preserved");
                Check(!loaded.Data.CityRoadOpen && !loaded.Data.CityRoadworksCompleted,
                    "city road starts closed");
                Equal("", loaded.Data.ActiveCityMission, "no city job invented");
                Equal(0, loaded.Data.CompletedCityMissions.Count, "no city completion invented");
                Equal(0.0, loaded.Data.CityLogicalSeconds, "city clock starts at zero");
                Check(loaded.Data.CitySeed != 0, "deterministic city seed defaulted");

                SaveService.Save(loaded.Data, path, catalog);
                var rewriten = SaveService.Load(path, catalog);
                Check(rewriten.Success, "migrated city save rewrites");
                Equal(SaveService.CurrentVersion, rewriten.Data.SchemaVersion,
                    "rewritten save is current schema");
                Check(!rewriten.Data.CityRoadOpen, "rewritten save keeps closed road");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("schema 2 original-ten save upgrades to the current schema intact", () =>
        {
            var old = BuildOriginalTenSave();
            old.SchemaVersion = 2;
            var node = JsonSerializer.SerializeToNode(old, new JsonSerializerOptions { WriteIndented = true })!;
            node.AsObject().Remove("FestivalSeen");

            string dir = TempDirectory("chapter-two-migration");
            try
            {
                string path = Path.Combine(dir, SaveService.DefaultFileName);
                File.WriteAllText(path, node.ToJsonString());
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, loaded.Message);
                Equal(SaveService.CurrentVersion, loaded.Data.SchemaVersion, "migrated schema");
                Equal(10, loaded.Data.CompletedMissions.Count, "ten completions preserved");
                Check(loaded.Data.CelebrationSeen, "original celebration preserved");
                Check(!loaded.Data.FestivalSeen, "second finale is not invented");
                Check(loaded.Data.Coins == old.Coins, "wallet preserved");
                Check(loaded.Data.BridgePieces.Count == BuildRules.BridgeSockets.Length,
                    "bridge world state preserved");
                Check(loaded.Data.IsCompleted("M10"), "M10 remains complete");
                var service = new MissionService(loaded.Data, catalog);
                Equal(MissionStatus.Available, service.GetStatus("M11"), "M11 available on upgrade");

                SaveService.Save(loaded.Data, path, catalog);
                var rewriten = SaveService.Load(path, catalog);
                Check(rewriten.Success, "rewritten upgraded save loads");
                Equal(SaveService.CurrentVersion, rewriten.Data.SchemaVersion,
                    "rewritten save uses the current schema");
                Check(!rewriten.Data.FestivalSeen, "rewritten save keeps the festival unseen");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("celebration and festival markers require their own finales", () =>
        {
            var data = BuildOriginalTenSave();
            Check(SaveValidation.Validate(data, catalog).IsValid,
                "valid original-ten save may mark the M10 celebration");
            data.CelebrationSeen = true;
            Check(SaveValidation.Validate(data, catalog).IsValid,
                "celebrated original-ten save is valid");
            data.CompletedMissions.Remove("M10");
            data.FestivalSeen = true;
            var invalid = SaveValidation.Validate(data, catalog);
            Check(!invalid.IsValid, "markers without their finales are rejected");
            data.FestivalSeen = false;
            var missingCelebration = SaveValidation.Validate(data, catalog);
            Check(!missingCelebration.IsValid, "M10 celebration without M10 is rejected");
            Check(missingCelebration.Message.Contains("celebration"),
                "celebration validation is explicit");
        });

    }
}
