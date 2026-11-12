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
    private static void WorldRulesTests(MissionCatalog catalog, CityMissionCatalog cityCatalog, string root, string json)
    {
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

    Run("six named villagers have verified patrols", () =>
    {
        Check(NpcCatalog.Validate(out string error), error);
        Equal(6, NpcCatalog.All.Count, "villager count");
        Equal("Milo, Ada, Pip, Fern, Ollie, Ivy",
            string.Join(", ", NpcCatalog.All.Select(n => n.Name)), "villager names");
        foreach (var npc in NpcCatalog.All)
        {
            Check(!string.IsNullOrWhiteSpace(npc.Role), $"{npc.Name} role");
            Check(!string.IsNullOrWhiteSpace(npc.Intro), $"{npc.Name} intro");
            Check(npc.PatrolPoints.Count >= 4, $"{npc.Name} patrol points");
            foreach (var point in npc.PatrolPoints)
            {
                Check(WorldAnchors.DistanceToNearestStation(point.X, point.Z) >= 3.35f,
                    $"{npc.Name} patrol competes with a station");
                Check(!WorldAnchors.InsideBuilding(point.X, point.Z),
                    $"{npc.Name} patrol is inside a building");
            }
        }
    });

    Run("traffic routes are sparse, road-bound and station safe", () =>
    {
        Check(TrafficCatalog.Validate(out string error), error);
        Equal(3, TrafficCatalog.Routes.Count, "traffic definition count");
        var freeRoam = TrafficCatalog.Routes.Where(r => !r.RallyOnly).ToList();
        Equal(2, freeRoam.Count, "active free-roam vehicles");
        foreach (var route in freeRoam)
        {
            Check(route.SpeedMetresPerSecond <= 4.2f, "free-roam speed cap");
            foreach (var point in route.Points)
            {
                Check(RoadNetwork.Contains(point.X, point.Z),
                    $"{route.Id} leaves the road at {point.X},{point.Z}");
                Check(WorldAnchors.DistanceToNearestStation(point.X, point.Z) >= 1.8f,
                    $"{route.Id} passes through a critical station");
            }
        }
        var rival = TrafficCatalog.Find("rally_rival");
        Check(rival != null && rival.RallyOnly, "rally rival is M19-only");
        Check(rival.SpeedMetresPerSecond <= 8f, "rival speed cap");
    });

    Run("free-roam traffic follows one shared village loop", () =>
    {
        var freeRoam = TrafficCatalog.Routes.Where(r => !r.RallyOnly).ToList();
        Equal(2, freeRoam.Count, "free-roam traffic count");
        Check(TrafficCatalog.VillageLoopLength > 100f,
            $"village loop is only {TrafficCatalog.VillageLoopLength:0.0}m");
        foreach (var route in freeRoam)
        {
            foreach (var point in route.Points)
            {
                Check(TrafficCatalog.DistanceToVillageLoop(point.X, point.Z) <= 0.01f,
                    $"{route.Id} leaves the shared loop at {point.X},{point.Z}");
            }
        }
    });

    Run("traffic following rule has a single deterministic right of way", () =>
    {
        Check(TrafficCatalog.ShouldYieldToTraffic(
                10f, "town_scooter", 12f, "delivery_car"),
            "the car behind must give way");
        Check(!TrafficCatalog.ShouldYieldToTraffic(
                12f, "delivery_car", 10f, "town_scooter"),
            "the car ahead must proceed");
        Check(TrafficCatalog.ShouldYieldToTraffic(
                10f, "town_scooter", 10f, "delivery_car"),
            "equal progress: only the larger id gives way");
        Check(!TrafficCatalog.ShouldYieldToTraffic(
                10f, "delivery_car", 10f, "town_scooter"),
            "equal progress: exactly one car gives way");
    });

    Run("traffic keeps a safe gap over six complete loops", () =>
    {
        var freeRoam = TrafficCatalog.Routes.Where(r => !r.RallyOnly)
            .OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
        Equal(2, freeRoam.Count, "free-roam vehicles");
        float length = TrafficCatalog.VillageLoopLength;
        float[] progress =
        {
            TrafficCatalog.ProgressOnVillageLoop(
                freeRoam[0].Points[0].X, freeRoam[0].Points[0].Z),
            TrafficCatalog.ProgressOnVillageLoop(
                freeRoam[1].Points[0].X, freeRoam[1].Points[0].Z),
        };
        float[] speeds = freeRoam.Select(r => r.SpeedMetresPerSecond).ToArray();
        string[] ids = freeRoam.Select(r => r.Id).ToArray();
        float[] travelled = new float[2];
        const float dt = 1f / 60f;
        int steps = (int)MathF.Ceiling(6f * length / speeds.Min() / dt);
        float minGap = float.MaxValue;
        int deadlocks = 0;
        for (int step = 0; step < steps; step++)
        {
            bool[] yields = new bool[2];
            for (int i = 0; i < 2; i++)
            {
                yields[i] = TrafficCatalog.ShouldYieldToTraffic(
                    progress[i], ids[i], progress[1 - i], ids[1 - i]);
            }
            if (yields[0] && yields[1]) deadlocks++;
            for (int i = 0; i < 2; i++)
            {
                if (yields[i]) continue;
                progress[i] += speeds[i] * dt;
                travelled[i] += speeds[i] * dt;
                if (progress[i] >= length) progress[i] -= length;
            }
            float raw = MathF.Abs(progress[0] - progress[1]);
            float gap = MathF.Min(raw, length - raw);
            if (step > 0) minGap = MathF.Min(minGap, gap);
        }
        Equal(0, deadlocks, "traffic following deadlocks");
        Check(minGap >= TrafficCatalog.MinimumTrafficGap - 0.1f,
            $"traffic cars closed to {minGap:0.00}m");
        Check(travelled[0] > 3f * length && travelled[1] > 3f * length,
            $"traffic cars did not complete three loops " +
            $"({travelled[0]:0.0}/{travelled[1]:0.0}m)");
    });

    Run("historical v1.2.1 manifest stays separate from v1.3.0", () =>
    {
        string root = FindRepoRoot();
        using var historical = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "docs", "layout_manifest_v1.2.1.json")));
        using var current = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "docs", "layout_manifest_v1.3.0.json")));
        var oldDelivery = historical.RootElement.GetProperty("traffic_routes")
            .GetProperty("delivery_car");
        var newDelivery = current.RootElement.GetProperty("traffic_routes")
            .GetProperty("delivery_car");
        Equal(12, oldDelivery.GetArrayLength(), "historical delivery waypoint count");
        Equal(20, newDelivery.GetArrayLength(), "v1.3.0 delivery waypoint count");
        Check(MathF.Abs(oldDelivery[0][0].GetSingle() - 66f) < 0.01f &&
              MathF.Abs(oldDelivery[0][2].GetSingle() + 1.5f) < 0.01f,
            "historical delivery route was rewritten");
        Check(MathF.Abs(newDelivery[0][0].GetSingle() - 34f) < 0.01f &&
              MathF.Abs(newDelivery[0][2].GetSingle() - 0.5f) < 0.01f,
            "v1.3.0 delivery route is not current");
        Check((historical.RootElement.GetProperty("source").GetString() ?? "")
            .Contains("v1.2.1", StringComparison.Ordinal),
            "historical manifest source changed");
        Check((current.RootElement.GetProperty("source").GetString() ?? "")
            .Contains("v1.3.0", StringComparison.Ordinal),
            "v1.3.0 manifest source missing");
        Equal(7, current.RootElement.GetProperty("street_roads").GetArrayLength(),
            "v1.3.0 street road count");
        var sceneDelivery = ParseMarkersByParent(Path.Combine(root, "scenes", "world",
            "TrafficRouteMarkers.tscn"))["delivery_car"];
        Equal(newDelivery.GetArrayLength(), sceneDelivery.Count,
            "v1.3.0 manifest does not match the scene");
        Check(sceneDelivery.Count != oldDelivery.GetArrayLength(),
            "historical manifest still matches the new scene");
    });

    Run("rally centreline does not self-overlap its own road width", () =>
    {
        var samples = RallyCourse.SampleClosed(20);
        int n = samples.Count;
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var a1 = samples[i];
            var a2 = samples[(i + 1) % n];
            for (int j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue; // adjacent across the seam
                if (i == n - 2 && j == n - 1) continue; // adjacent at the seam end
                int forwardGap = j - i;
                int backwardGap = n - forwardGap;
                if (forwardGap <= 40 || backwardGap <= 40) continue;
                var b1 = samples[j];
                var b2 = samples[(j + 1) % n];
                float distance = SegmentDistance(a1, a2, b1, b2);
                if (distance < best) best = distance;
            }
        }
        Check(best > RallyCourse.RoadWidth, $"rally road overlaps itself ({best:0.0}m)");
    });

    Run("partial four-gate progress survives the enlarged circuit", () =>
    {
        var service = CompleteBridge(Fresh(catalog));
        var started = service.StartMission("M05");
        Check(started.Success, "M05 starts after the bridge");
        service.Data.SetMissionCounter("M05", "checkpoint", 2);
        service.Data.SetMissionCounter("M05", "elapsedMs", 30_000);
        string dir = TempDirectory("rally-progress");
        try
        {
            string path = Path.Combine(dir, "save.json");
            SaveService.Save(service.Data, path, catalog);
            var loaded = SaveService.Load(path, catalog);
            Check(loaded.Success, loaded.Message);
            Equal(2, loaded.Data.GetMissionCounter("M05", "checkpoint"), "checkpoint preserved");
            Equal("M05", loaded.Data.ActiveMission, "active race preserved");
            Check(!MissionRules.IsObjectiveComplete(loaded.Data, "M05"), "race still in progress");
            Equal(4, RallyCourse.FourGates.Count, "four gate identities remain");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    });

    }
}
