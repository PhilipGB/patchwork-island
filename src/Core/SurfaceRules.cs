using System;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Small spatial helper used for footstep surface selection and gameplay
/// safety checks. Kept pure/Godot-value-only so tests can reason about it.
/// </summary>
public static class SurfaceRules
{
    // Authored road rectangles: centre X/Z plus half extents. These mirror the
    // visible village road network built by WorldBuilder.
    private static readonly (float X, float Z, float HalfX, float HalfZ)[] Roads =
    {
        (-4f, 0f, 44f, 3.2f),
        (68.5f, 0f, 19.5f, 3.2f),
        (-30f, 9.5f, 2.6f, 9.5f),
        (-38f, 9.5f, 1.7f, 9.5f),
        (-20f, -10f, 1.9f, 10f),
        (25f, -5.5f, 2.4f, 5.5f),
        (-14f, 4.5f, 1.8f, 4.5f),
        (5f, 1.9f, 1.9f, 1.9f),
        (15f, 9.8f, 1.6f, 9.8f),
        (-32f, -2.6f, 6f, 1.6f),
        (-22f, 3f, 3.25f, 2f),
    };

    public static bool IsRoad(Vector3 position)
    {
        foreach (var road in Roads)
        {
            if (Mathf.Abs(position.X - road.X) <= road.HalfX &&
                Mathf.Abs(position.Z - road.Z) <= road.HalfZ)
                return true;
        }
        // The rally meadow is a broad paved circuit on the east island.
        return RallyTrack.IsOnRoad(position);
    }

    /// <summary>
    /// The rally corridor is generated from the continuous centreline. Keeping
    /// this check here lets footsteps, traffic and tests share one definition.
    /// </summary>
    public static class RallyTrack
    {
        public static bool IsOnRoad(Vector3 position)
        {
            if (position.X < 49f || position.X > 94f) return false;
            return DistanceToCentreline(position) <= 4.6f;
        }

        public static float DistanceToCentreline(Vector3 position)
        {
            return RallyCourse.NearestDistance(position.X, position.Z);
        }
    }
}
