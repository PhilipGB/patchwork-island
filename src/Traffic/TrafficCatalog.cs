using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

public sealed class TrafficRouteDefinition
{
    public string Id { get; init; } = "";
    public string DriverId { get; init; } = "";
    public string VehicleKind { get; init; } = "car";
    public string ColourHex { get; init; } = "#8ac69c";
    public float SpeedMetresPerSecond { get; init; } = 3f;
    public bool RallyOnly { get; init; }
    public IReadOnlyList<NpcPoint> Points { get; init; } = Array.Empty<NpcPoint>();
}

/// <summary>
/// Authored, road-bound free-roam traffic. Every waypoint is validated against
/// the pure road network; there is no random navigation or visible teleport.
/// </summary>
public static class TrafficCatalog
{
    public static readonly IReadOnlyList<TrafficRouteDefinition> Routes = new List<TrafficRouteDefinition>
    {
        new()
        {
            Id = "town_scooter",
            DriverId = "milo",
            VehicleKind = "scooter",
            ColourHex = "#f6c55b",
            SpeedMetresPerSecond = 3.2f,
            Points = new[]
            {
                new NpcPoint(-42f, -2f), new NpcPoint(-32f, -2f),
                new NpcPoint(-24f, -2f), new NpcPoint(-14f, -2f),
                new NpcPoint(-4f, -2f), new NpcPoint(8f, -2f),
                new NpcPoint(18f, -2f), new NpcPoint(28f, -2f),
                new NpcPoint(34f, -2f), new NpcPoint(36f, 0f),
                new NpcPoint(34f, 0.5f), new NpcPoint(28f, 0.5f),
                new NpcPoint(18f, 0.5f), new NpcPoint(8f, 0.5f),
                new NpcPoint(-4f, 0.5f), new NpcPoint(-14f, 0.5f),
                new NpcPoint(-24f, 0.5f), new NpcPoint(-32f, 0.5f),
                new NpcPoint(-42f, 0.5f), new NpcPoint(-44f, 0f),
            },
        },
        new()
        {
            Id = "delivery_car",
            DriverId = "ivy",
            VehicleKind = "car",
            ColourHex = "#8ac69c",
            SpeedMetresPerSecond = 2.8f,
            Points = new[]
            {
                new NpcPoint(34f, 0.5f), new NpcPoint(28f, 0.5f),
                new NpcPoint(18f, 0.5f), new NpcPoint(8f, 0.5f),
                new NpcPoint(-4f, 0.5f), new NpcPoint(-14f, 0.5f),
                new NpcPoint(-24f, 0.5f), new NpcPoint(-32f, 0.5f),
                new NpcPoint(-42f, 0.5f), new NpcPoint(-44f, 0f),
                new NpcPoint(-42f, -2f), new NpcPoint(-32f, -2f),
                new NpcPoint(-24f, -2f), new NpcPoint(-14f, -2f),
                new NpcPoint(-4f, -2f), new NpcPoint(8f, -2f),
                new NpcPoint(18f, -2f), new NpcPoint(28f, -2f),
                new NpcPoint(34f, -2f), new NpcPoint(36f, 0f),
            },
        },
        new()
        {
            Id = "rally_rival",
            DriverId = "pip",
            VehicleKind = "rally_car",
            ColourHex = "#e98a75",
            SpeedMetresPerSecond = 6.7f,
            RallyOnly = true,
            Points = RallyRivalPoints(),
        },
    };

    private static IReadOnlyList<NpcPoint> RallyRivalPoints()
    {
        const int count = 32;
        var points = new NpcPoint[count];
        for (int i = 0; i < count; i++)
        {
            var sample = RallyCourse.Sample(i / (float)count);
            points[i] = new NpcPoint(sample.X, sample.Z);
        }
        return points;
    }

    /// <summary>
    /// Minimum centre-to-centre following gap along the shared village loop.
    /// The longest free-roam vehicle is 2.75 m, so 7 m leaves a clear bumper
    /// gap even after a braking step.
    /// </summary>
    public const float MinimumTrafficGap = 7f;

    /// <summary>
    /// Both free-roam vehicles follow the same closed loop, just starting at
    /// different authored waypoints. Progress is measured on this canonical
    /// order so the two routes stay comparable through every corner.
    /// </summary>
    public static readonly IReadOnlyList<NpcPoint> VillageLoop =
        Routes.First(route => !route.RallyOnly).Points;

    private static readonly float[] VillageLoopCumulative = BuildCumulative(VillageLoop);

    public static float VillageLoopLength { get; } =
        VillageLoopCumulative[VillageLoopCumulative.Length - 1] +
        FlatDistance(VillageLoop[VillageLoop.Count - 1], VillageLoop[0]);

    private static float[] BuildCumulative(IReadOnlyList<NpcPoint> loop)
    {
        var cumulative = new float[loop.Count];
        for (int i = 1; i < loop.Count; i++)
            cumulative[i] = cumulative[i - 1] + FlatDistance(loop[i - 1], loop[i]);
        return cumulative;
    }

    private static float FlatDistance(NpcPoint a, NpcPoint b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Distance along the canonical village loop, in metres.</summary>
    public static float ProgressOnVillageLoop(float x, float z)
    {
        var loop = VillageLoop;
        float bestDistanceSq = float.MaxValue;
        float bestProgress = 0f;
        for (int i = 0; i < loop.Count; i++)
        {
            NpcPoint a = loop[i];
            NpcPoint b = loop[(i + 1) % loop.Count];
            float dx = b.X - a.X;
            float dz = b.Z - a.Z;
            float lengthSq = dx * dx + dz * dz;
            float t = lengthSq > 1e-8f
                ? Math.Clamp(((x - a.X) * dx + (z - a.Z) * dz) / lengthSq, 0f, 1f)
                : 0f;
            float px = a.X + dx * t;
            float pz = a.Z + dz * t;
            float distanceSq = (x - px) * (x - px) + (z - pz) * (z - pz);
            if (distanceSq < bestDistanceSq)
            {
                bestDistanceSq = distanceSq;
                bestProgress = VillageLoopCumulative[i] + MathF.Sqrt(lengthSq) * t;
            }
        }
        return bestProgress;
    }

    public static float DistanceToVillageLoop(float x, float z)
    {
        var loop = VillageLoop;
        float bestDistanceSq = float.MaxValue;
        for (int i = 0; i < loop.Count; i++)
        {
            NpcPoint a = loop[i];
            NpcPoint b = loop[(i + 1) % loop.Count];
            float dx = b.X - a.X;
            float dz = b.Z - a.Z;
            float lengthSq = dx * dx + dz * dz;
            float t = lengthSq > 1e-8f
                ? Math.Clamp(((x - a.X) * dx + (z - a.Z) * dz) / lengthSq, 0f, 1f)
                : 0f;
            float px = a.X + dx * t;
            float pz = a.Z + dz * t;
            float distanceSq = (x - px) * (x - px) + (z - pz) * (z - pz);
            if (distanceSq < bestDistanceSq) bestDistanceSq = distanceSq;
        }
        return MathF.Sqrt(bestDistanceSq);
    }

    /// <summary>
    /// True when <paramref name="selfId"/> is the following vehicle and the
    /// other vehicle is less than <see cref="MinimumTrafficGap"/> ahead on the
    /// one-way loop. At exactly equal progress the lexicographically larger id
    /// gives way, so only one car can ever yield and the loop cannot deadlock.
    /// </summary>
    public static bool ShouldYieldToTraffic(float selfProgress, string selfId,
        float otherProgress, string otherId)
    {
        float gap = otherProgress - selfProgress;
        if (gap < 0f) gap += VillageLoopLength;
        if (gap < 0.001f)
            return string.CompareOrdinal(selfId, otherId) > 0;
        return gap < MinimumTrafficGap;
    }

    public static TrafficRouteDefinition Find(string id) =>
        Routes.FirstOrDefault(route => string.Equals(route.Id, id, StringComparison.Ordinal));

    public static bool Validate(out string error)
    {
        error = "";
        if (Routes.Count != 3)
        {
            error = $"Expected three traffic definitions, found {Routes.Count}.";
            return false;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int freeRoam = 0;
        foreach (var route in Routes)
        {
            if (string.IsNullOrWhiteSpace(route.Id) || !ids.Add(route.Id))
            {
                error = "Traffic route ids must be present and unique.";
                return false;
            }
            if (NpcCatalog.Find(route.DriverId) == null)
            {
                error = $"Traffic route '{route.Id}' has unknown driver '{route.DriverId}'.";
                return false;
            }
            if (route.SpeedMetresPerSecond <= 0f ||
                (!route.RallyOnly && route.SpeedMetresPerSecond > 4.2f) ||
                route.SpeedMetresPerSecond > 8f)
            {
                error = $"Traffic route '{route.Id}' has an unsafe speed.";
                return false;
            }
            if (route.RallyOnly) continue;
            freeRoam++;
            if (route.Points.Count < 4)
            {
                error = $"Traffic route '{route.Id}' needs a closed route.";
                return false;
            }
            for (int i = 0; i < route.Points.Count; i++)
            {
                var point = route.Points[i];
                if (!RoadNetwork.Contains(point.X, point.Z))
                {
                    error = $"Traffic route '{route.Id}' leaves the road at {point.X:0.0},{point.Z:0.0}.";
                    return false;
                }
                if (DistanceToVillageLoop(point.X, point.Z) > 0.01f)
                {
                    error = $"Traffic route '{route.Id}' leaves the shared village loop at " +
                            $"{point.X:0.0},{point.Z:0.0}.";
                    return false;
                }
                var next = route.Points[(i + 1) % route.Points.Count];
                float gap = MathF.Sqrt((point.X - next.X) * (point.X - next.X) +
                                       (point.Z - next.Z) * (point.Z - next.Z));
                if (gap > 14f)
                {
                    error = $"Traffic route '{route.Id}' has a long uncontrolled jump.";
                    return false;
                }
            }
            foreach (var station in WorldAnchors.CriticalStations)
            {
                if (route.Points.Any(p =>
                        MathF.Sqrt((p.X - station.X) * (p.X - station.X) +
                                   (p.Z - station.Z) * (p.Z - station.Z)) < 1.8f))
                {
                    error = $"Traffic route '{route.Id}' passes through critical station '{station.Name}'.";
                    return false;
                }
            }
        }
        if (freeRoam != 2)
        {
            error = $"Expected exactly two free-roam drivers, found {freeRoam}.";
            return false;
        }
        return true;
    }
}
