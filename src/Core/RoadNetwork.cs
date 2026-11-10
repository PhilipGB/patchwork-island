using System;
using System.Collections.Generic;

namespace PatchworkIsland;

/// <summary>
/// The authored village road rectangles, kept pure so NPC traffic and tests can
/// verify every route waypoint is actually on a road. The eight entries mirror
/// the visible slabs in <c>TownRoads_Collidable.tscn</c> exactly; that scene is
/// authoritative, and the pure test suite fails if either drifts. SurfaceRules
/// adds the rally loop for footstep selection.
/// </summary>
public static class RoadNetwork
{
    public readonly struct Rect
    {
        public readonly float X;
        public readonly float Z;
        public readonly float HalfX;
        public readonly float HalfZ;

        public Rect(float x, float z, float halfX, float halfZ)
        {
            X = x;
            Z = z;
            HalfX = halfX;
            HalfZ = halfZ;
        }
    }

    public static readonly IReadOnlyList<Rect> Roads = new List<Rect>
    {
        new(-4f, 0f, 44f, 3.2f),       // main street, x -48..40
        new(69.25f, 0f, 18.75f, 3.2f), // east road, x 50.5..88
        new(-30f, 11.1f, 2.6f, 7.9f),  // north side road, z 3.2..19
        new(-38f, 11.1f, 1.7f, 7.9f),  // north side road, z 3.2..19
        new(-20f, -11.6f, 1.9f, 8.4f), // south side road, z -20..-3.2
        new(25f, -7.1f, 2.4f, 3.9f),   // workshop road, z -11..-3.2
        new(-14f, 6.1f, 1.8f, 2.9f),   // town-hall road, z 3.2..9
        new(15f, 11.4f, 1.6f, 8.2f),   // north-east road, z 3.2..19.6
    };

    public static bool Contains(float x, float z)
    {
        foreach (var road in Roads)
        {
            if (MathF.Abs(x - road.X) <= road.HalfX && MathF.Abs(z - road.Z) <= road.HalfZ)
                return true;
        }
        return false;
    }

    /// <summary>Distance to the nearest road edge; negative means inside.</summary>
    public static float DistanceToEdge(float x, float z)
    {
        float best = float.MaxValue;
        foreach (var road in Roads)
        {
            float dx = MathF.Abs(x - road.X) - road.HalfX;
            float dz = MathF.Abs(z - road.Z) - road.HalfZ;
            float distance;
            if (dx <= 0f && dz <= 0f)
                distance = MathF.Max(dx, dz); // negative inside
            else
                distance = MathF.Sqrt(MathF.Max(0f, dx) * MathF.Max(0f, dx) +
                                      MathF.Max(0f, dz) * MathF.Max(0f, dz));
            best = MathF.Min(best, distance);
        }
        return best;
    }
}
