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
    private static void PersistenceTests(MissionCatalog catalog, CityMissionCatalog cityCatalog, string root, string json)
    {
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

        Run("incompatible newer schema 5 and 99 saves are rejected without modification", () =>
        {
            string dir = TempDirectory("patchwork-save-invalid");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                var missing = SaveService.Load(path);
                Check(!missing.Success, "missing save is not loaded");

                foreach (int version in new[] { 5, 99 })
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

    }
}
