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

        Run("game state survives a JSON round trip", () =>
        {
            var s = CompleteM01(Fresh(catalog));
            s.Data.SetMissionCounter("M09", "checkpoint", 2);
            s.Data.BridgePieces.Add("support_0");
            s.Data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
            s.Data.FurnitureOwned["chair"] = 2;
            s.Data.Player = new PlayerSnapshot { X = 1, Y = 2, Z = 3, RotationY = 4, Valid = true };
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
        });

        Console.WriteLine();
        Console.WriteLine($"RESULT: {_passed} passed, {_failed} failed");
        if (_failed == 0) return 0;
        foreach (var failure in Failures)
            Console.WriteLine("FAILED: " + failure);
        return 1;
    }

    private static MissionService Fresh(MissionCatalog catalog) => new(new GameData(), catalog);

    private static MissionService CompleteM01(MissionService service)
    {
        service.StartMission("M01");
        for (int i = 0; i < 3; i++)
            service.CollectPickup($"apple_{i}", "apple", "M01", 3);
        var result = service.DeliverApples();
        if (!result.Success) throw new Exception("M01 setup completion failed: " + result.Message);
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
