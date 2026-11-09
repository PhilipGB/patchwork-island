using System;
using System.Collections.Generic;

namespace PatchworkIsland;

/// <summary>
/// The authored village road rectangles, kept pure so NPC traffic and tests can
/// verify every route waypoint is actually on a road. WorldBuilder mirrors these
/// rectangles; SurfaceRules adds the rally loop for footstep selection.
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
        new(-4f, 0f, 44f, 3.2f),
        new(68.5f, 0f, 19.5f, 3.2f),
        new(-30f, 9.5f, 2.6f, 9.5f),
        new(-38f, 9.5f, 1.7f, 9.5f),
        new(-20f, -10f, 1.9f, 10f),
        new(25f, -5.5f, 2.4f, 5.5f),
        new(-14f, 4.5f, 1.8f, 4.5f),
        new(5f, 1.9f, 1.9f, 1.9f),
        new(15f, 9.8f, 1.6f, 9.8f),
        new(-32f, -2.6f, 6f, 1.6f),
        new(-22f, 3f, 3.25f, 2f),
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
