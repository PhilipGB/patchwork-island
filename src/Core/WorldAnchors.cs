using System;
using System.Collections.Generic;

namespace PatchworkIsland;

/// <summary>
/// Stable world anchors used by pure route validation. These mirror the
/// landmark positions in WorldBuilder; tests use them to prove NPCs and traffic
/// keep clear of buildings and critical job interactions.
/// </summary>
public static class WorldAnchors
{
    public static readonly IReadOnlyList<(string Name, float X, float Z)> CriticalStations =
        new List<(string, float, float)>
        {
            ("job board", -22f, 4.15f),
            ("shop door", 5f, 2.7f),
            ("town hall door", -14f, 7.4f),
            ("red house door", -20f, -18.7f),
            ("workshop door", 25f, -9.3f),
            ("clothes shop door", 24f, 2.5f),
            ("park sign", 10f, 9.8f),
            ("bridge site", 36.5f, 4.5f),
            ("player house door", -38f, 17.3f),
            ("furniture station", -40.5f, 20.5f),
        };

    public static readonly IReadOnlyList<(float X, float Z, float HalfX, float HalfZ)> Buildings =
        new List<(float, float, float, float)>
        {
            (-38f, 24f, 6.8f, 5.3f),
            (-14f, 14f, 7.8f, 5.3f),
            (-20f, -24f, 5.8f, 4.8f),
            (5f, 9f, 6.3f, 4.8f),
            (24f, 7f, 4.8f, 3.8f),
            (25f, -16f, 6.8f, 5.3f),
        };

    public static float DistanceToNearestStation(float x, float z)
    {
        float best = float.MaxValue;
        foreach (var station in CriticalStations)
        {
            float dx = x - station.X;
            float dz = z - station.Z;
            best = MathF.Min(best, MathF.Sqrt(dx * dx + dz * dz));
        }
        return best;
    }

    public static bool InsideBuilding(float x, float z)
    {
        foreach (var building in Buildings)
        {
            if (MathF.Abs(x - building.X) < building.HalfX &&
                MathF.Abs(z - building.Z) < building.HalfZ)
                return true;
        }
        return false;
    }
}
