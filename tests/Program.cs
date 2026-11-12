using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PatchworkIsland;

namespace PatchworkIsland.Tests;

/// <summary>
/// Dependency-free test runner for mission rules, unlocks, exactly-once
/// rewards, duplicate pickup protection and serialisation. Exits non-zero on
/// failure so CI / the release gate can rely on it.
/// </summary>
internal static partial class Program
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
        string cityPath = Path.Combine(root, "data", "city_missions.json");
        var cityCatalog = CityMissionCatalog.FromJson(File.ReadAllText(cityPath));

        CoreMissionsTests(catalog, cityCatalog, root, json);
        CommunityTests(catalog, cityCatalog, root, json);
        FinaleTests(catalog, cityCatalog, root, json);
        MigrationsTests(catalog, cityCatalog, root, json);
        CustomisationTests(catalog, cityCatalog, root, json);
        PersistenceTests(catalog, cityCatalog, root, json);
        ContentAndControlsTests(catalog, cityCatalog, root, json);
        PlacementAndRaceTests(catalog, cityCatalog, root, json);
        SaveFeedbackTests(catalog, cityCatalog, root, json);
        WorldRulesTests(catalog, cityCatalog, root, json);
        AppearanceContractsTests(catalog, cityCatalog, root, json);
        SceneContractsTests(catalog, cityCatalog, root, json);

        CityExpansionTests(catalog, cityCatalog);

        Console.WriteLine();
        Console.WriteLine($"RESULT: {_passed} passed, {_failed} failed");
        if (_failed == 0) return 0;
        foreach (var failure in Failures)
            Console.WriteLine("FAILED: " + failure);
        return 1;
    }

    private static MissionService Fresh(MissionCatalog catalog) => new(new GameData(), catalog);

    private static float SegmentDistance(RallyCourse.Point a, RallyCourse.Point b,
        RallyCourse.Point c, RallyCourse.Point d)
    {
        if (SegmentsIntersect(a, b, c, d)) return 0f;
        return MathF.Min(
            MathF.Min(PointSegmentDistance(a, c, d), PointSegmentDistance(b, c, d)),
            MathF.Min(PointSegmentDistance(c, a, b), PointSegmentDistance(d, a, b)));
    }

    private static bool SegmentsIntersect(RallyCourse.Point a, RallyCourse.Point b,
        RallyCourse.Point c, RallyCourse.Point d)
    {
        float d1 = Cross(c, d, a);
        float d2 = Cross(c, d, b);
        float d3 = Cross(a, b, c);
        float d4 = Cross(a, b, d);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
               ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static float Cross(RallyCourse.Point a, RallyCourse.Point b, RallyCourse.Point c) =>
        (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);

    private static float PointSegmentDistance(RallyCourse.Point p, RallyCourse.Point a,
        RallyCourse.Point b)
    {
        float dx = b.X - a.X;
        float dz = b.Z - a.Z;
        float lengthSq = dx * dx + dz * dz;
        if (lengthSq < 1e-8f)
            return MathF.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Z - a.Z) * (p.Z - a.Z));
        float t = ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / lengthSq;
        t = Math.Clamp(t, 0f, 1f);
        float cx = a.X + dx * t;
        float cz = a.Z + dz * t;
        return MathF.Sqrt((p.X - cx) * (p.X - cx) + (p.Z - cz) * (p.Z - cz));
    }

    private static string TempDirectory(string prefix)
    {
        string path = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
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
