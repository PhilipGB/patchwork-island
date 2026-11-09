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
                new NpcPoint(-42f, -1.5f), new NpcPoint(-32f, -1.5f),
                new NpcPoint(-24f, -1.5f), new NpcPoint(-14f, -1.5f),
                new NpcPoint(-4f, -1.5f), new NpcPoint(8f, -1.5f),
                new NpcPoint(18f, -1.5f), new NpcPoint(28f, -1.5f),
                new NpcPoint(34f, -1.5f), new NpcPoint(36f, 0f),
                new NpcPoint(34f, 1.5f), new NpcPoint(28f, 1.5f),
                new NpcPoint(18f, 1.5f), new NpcPoint(8f, 1.5f),
                new NpcPoint(-4f, 1.5f), new NpcPoint(-14f, 1.5f),
                new NpcPoint(-24f, 1.5f), new NpcPoint(-32f, 1.5f),
                new NpcPoint(-42f, 1.5f), new NpcPoint(-44f, 0f),
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
                new NpcPoint(66f, -1.5f), new NpcPoint(70f, -1.5f),
                new NpcPoint(74f, -1.5f), new NpcPoint(78f, -1.5f),
                new NpcPoint(80f, -1.5f), new NpcPoint(80f, 0f),
                new NpcPoint(80f, 1.5f), new NpcPoint(78f, 1.5f),
                new NpcPoint(74f, 1.5f), new NpcPoint(70f, 1.5f),
                new NpcPoint(66f, 1.5f), new NpcPoint(65f, 0f),
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
