using System;
using System.Collections.Generic;

namespace PatchworkIsland;

/// <summary>
/// Fixed world layout shared by the runtime adapter and scene-contract tests.
/// Values are measured from the authored CityDistrict scene, not generated.
/// </summary>
public static class CityLayout
{
    public const float OriginX = 320f;
    public const float OriginY = 0f;
    public const float OriginZ = 0f;

    /// <summary>Local district footprint is -180..+180 on both axes.</summary>
    public const float DistrictHalfExtent = 180f;
    public const float RoadAxisSpacing = 80f;
    public const float RoadWidth = 24f;
    public const float JointRoadWidth = 14f;

    public static readonly float[] RoadAxes = { -120f, -40f, 40f, 120f };

    /// <summary>The nine authored block centres, in city-local coordinates.</summary>
    public static readonly (float X, float Z)[] CellCentres =
    {
        (-80f, -80f), (0f, -80f), (80f, -80f),
        (-80f, 0f), (0f, 0f), (80f, 0f),
        (-80f, 80f), (0f, 80f), (80f, 80f),
    };

    public const float CellHalfSize = 40f;

    /// <summary>
    /// Physical city-ground collision uses the authored square footprint, not a
    /// radius around the centre. The outer carriageways reach ±132 m and their
    /// corners are ~187 m from the centre, so a 185 m circle clipped the road
    /// corners. The footprint keeps ~48 m of margin beyond every drivable edge.
    /// </summary>
    public const float GroundCollisionHalfExtent = DistrictHalfExtent;

    public static bool IsWithinGroundCollision(float worldX, float worldZ) =>
        worldX >= OriginX - GroundCollisionHalfExtent &&
        worldX <= OriginX + GroundCollisionHalfExtent &&
        worldZ >= OriginZ - GroundCollisionHalfExtent &&
        worldZ <= OriginZ + GroundCollisionHalfExtent;

    public static float WorldX(float localX) => OriginX + localX;

    public static float WorldZ(float localZ) => OriginZ + localZ;

    public static int CellIndex(float localX, float localZ)
    {
        int column = localX < -40f ? 0 : localX < 40f ? 1 : 2;
        int row = localZ < -40f ? 0 : localZ < 40f ? 1 : 2;
        return row * 3 + column;
    }

    public static bool IsInCity(float worldX, float worldZ) =>
        worldX >= OriginX - DistrictHalfExtent - 8f &&
        worldX <= OriginX + DistrictHalfExtent + 8f &&
        worldZ >= OriginZ - DistrictHalfExtent - 8f &&
        worldZ <= OriginZ + DistrictHalfExtent + 8f;

    public static float DistanceToCityCentre(float worldX, float worldZ)
    {
        float dx = worldX - OriginX;
        float dz = worldZ - OriginZ;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Hysteresis-friendly tier selection for one city cell.</summary>
    public static int TierFor(float distance, float nearDistance, float midDistance,
        int previousTier)
    {
        // 0 = detailed, 1 = HLOD, 2 = hidden. Margin prevents rapid thrashing.
        const float Hysteresis = 18f;
        bool near = distance <= nearDistance + (previousTier == 0 ? Hysteresis : 0f);
        if (near) return 0;
        bool mid = distance <= midDistance + (previousTier == 1 ? Hysteresis : 0f);
        if (mid) return 1;
        return 2;
    }
}
