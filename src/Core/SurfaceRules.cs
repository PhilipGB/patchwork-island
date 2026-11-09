using Godot;

namespace PatchworkIsland;

/// <summary>
/// Small spatial helper used for footstep surface selection and gameplay
/// safety checks. Kept Godot-value-only so tests can reason about the pure road
/// network and rally course separately.
/// </summary>
public static class SurfaceRules
{
    public static bool IsRoad(Vector3 position)
    {
        if (RoadNetwork.Contains(position.X, position.Z))
            return true;
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

        public static float DistanceToCentreline(Vector3 position) =>
            RallyCourse.NearestDistance(position.X, position.Z);
    }
}
