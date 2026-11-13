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
    private static void CustomisationTests(MissionCatalog catalog, CityMissionCatalog cityCatalog, string root, string json)
    {
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

        Run("schema 1 saves migrate to the current schema with all progress intact", () =>
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
                Check(!loaded.Data.FestivalSeen, "schema 1 migration leaves the festival unseen");
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
                    Equal(SaveService.CurrentVersion,
                        document.RootElement.GetProperty("SchemaVersion").GetInt32(),
                        "rewritten save uses the current schema");
                }

                Check(File.Exists(SaveService.BackupPath(path)),
                    "the pre-migration schema 1 save is kept as backup");
                using (var backup = JsonDocument.Parse(File.ReadAllText(SaveService.BackupPath(path))))
                {
                    Equal(1, backup.RootElement.GetProperty("SchemaVersion").GetInt32(),
                        "backup still contains the original schema 1 save");
                }

                var reloaded = SaveService.Load(path, catalog);
                Check(reloaded.Success, "current-schema save loads");
                Equal(SaveService.CurrentVersion, reloaded.Data.SchemaVersion,
                    "rewritten schema is current");
                Equal(24, reloaded.Data.Coins, "wallet survives the current-schema rewrite");
                Equal("M03", reloaded.Data.ActiveMission, "active mission survives the current-schema rewrite");
                Equal(2, reloaded.Data.CompletedMissions.Count,
                    "completed missions survive the current-schema rewrite");
                Equal(2, reloaded.Data.TutorialsSeen.Count, "tutorials survive the current-schema rewrite");
                Equal(2, reloaded.Data.CollectedPickups.Count, "pickups survive the current-schema rewrite");
                Equal(1, reloaded.Data.GetItem("apple"), "inventory survives the current-schema rewrite");
                Equal(3.5f, reloaded.Data.Player.X, "player position survives the current-schema rewrite");
                Check(!reloaded.Data.Settings.MusicEnabled && reloaded.Data.Settings.ReducedMotion,
                    "settings survive the current-schema rewrite");
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

    }
}
