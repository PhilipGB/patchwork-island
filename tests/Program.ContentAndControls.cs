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
    private static void ContentAndControlsTests(MissionCatalog catalog, CityMissionCatalog cityCatalog, string root, string json)
    {
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

    }
}
