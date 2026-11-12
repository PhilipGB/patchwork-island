using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Data contract shared by the runtime world and the gameplay systems.
/// The town itself is authored in <c>scenes/world/Town.tscn</c> and its asset
/// scenes; this type exposes the anchors, spawn points and scene handles that
/// the adapter resolves from those assets.
/// </summary>
public abstract partial class WorldModel : Node3D
{
    // ---- Landmark anchors (also used by mission markers) ----
    public Vector3 PlayerSpawn { get; protected set; } = new(-38.0f, 0.15f, 9.5f);
    public Vector3 VanSpawn { get; protected set; } = new(-30.0f, 0.15f, 14.5f);
    public Vector3 JobBoard { get; protected set; } = new(-22.0f, 0.0f, 5.0f);
    public Vector3 PlayerHouseDoor { get; protected set; } = new(-38.0f, 0.0f, 18.3f);
    public Vector3 PlayerHouseCenter { get; protected set; } = new(-38.0f, 0.0f, 24.0f);
    public Vector3 TownHallDoor { get; protected set; } = new(-14.0f, 0.0f, 8.4f);
    public Vector3 TownHallCenter { get; protected set; } = new(-14.0f, 0.0f, 14.0f);
    public Vector3 RedHouseDoor { get; protected set; } = new(-20.0f, 0.0f, -18.7f);
    public Vector3 RedHouseCenter { get; protected set; } = new(-20.0f, 0.0f, -24.0f);
    public Vector3 ShopDoor { get; protected set; } = new(5.0f, 0.0f, 3.8f);
    public Vector3 ShopCenter { get; protected set; } = new(5.0f, 0.0f, 9.0f);
    public Vector3 ClothesShopDoor { get; protected set; } = new(24.0f, 0.0f, 3.5f);
    public Vector3 ClothesShopCenter { get; protected set; } = new(24.0f, 0.0f, 7.0f);
    public Vector3 WorkshopDoor { get; protected set; } = new(25.0f, 0.0f, -10.3f);
    public Vector3 WorkshopCenter { get; protected set; } = new(25.0f, 0.0f, -16.0f);
    public Vector3 AppleCrate { get; protected set; } = new(-2.5f, 0.0f, 3.6f);
    public Vector3 PostCounter { get; protected set; } = new(-9.0f, 0.0f, 8.0f);
    public Vector3 BridgeSite { get; protected set; } = new(36.5f, 0.0f, 4.5f);
    public Vector3 BridgeCenter { get; protected set; } = new(45.0f, 0.0f, 0.0f);
    public Vector3 PlaygroundCenter { get; protected set; } = new(-38.0f, 0.0f, -9.5f);
    public Vector3 PlaygroundWorksite { get; protected set; } = new(-38.0f, 0.1f, -5.0f);
    public Vector3 ParkCenter { get; protected set; } = new(15.0f, 0.0f, 16.0f);
    public Vector3 VillageGreenSign { get; protected set; } = new(10.0f, 0.0f, 10.7f);
    public Vector3 GroveCenter { get; protected set; } = new(-40.0f, 0.0f, -32.0f);
    public Vector3 StonePatchCenter { get; protected set; } = new(18.0f, 0.0f, -34.0f);
    public Vector3 RaceStart { get; protected set; } = new(56.0f, 0.0f, -6.0f);
    public Vector3 ParkingSign { get; protected set; } = new(-33.0f, 0.0f, 12.5f);
    public Vector3 FurnitureStation { get; protected set; } = new(-40.5f, 0.0f, 20.5f);

    public Vector3[] SwingFrames { get; protected set; } = { new(-41.0f, 0.0f, -11.5f), new(-35.0f, 0.0f, -11.5f) };
    public (Vector3 Position, float Yaw)[] RaceGates { get; protected set; } = System.Array.Empty<(Vector3, float)>();
    public (Vector3 Position, float Yaw)[] M19RaceGates { get; protected set; } = System.Array.Empty<(Vector3, float)>();
    public Node3D RallyRoad { get; protected set; }
    public Node3D RaceGates4Root { get; protected set; }
    public Node3D RaceGates6Root { get; protected set; }
    public int RallySampleCount { get; protected set; }
    public float RallyLength { get; protected set; }

    public Node3D BridgeBarrier { get; protected set; }
    public Node3D FinishedBridge { get; protected set; }
    public Node3D CelebrationDecor { get; protected set; }
    public Node3D RaceStartFlag { get; protected set; }
    public Node3D GoldCourseFlag { get; protected set; }
    public Node3D PlaygroundFixSign { get; protected set; }
    public List<Label3D> BrokenSwingLabels { get; } = new();
    public List<Vector3> WoodSpawnPoints { get; } = new();
    public List<Vector3> StoneSpawnPoints { get; } = new();
    public List<Vector3> AppleSpawnPoints { get; } = new();
    public List<Vector3> ParcelSpawnPoints { get; } = new();
    public List<Vector3> ParkPads { get; } = new();
    public List<Vector3> HousePads { get; } = new();
    public List<Vector3> GardenPads { get; } = new();
    public List<Vector3> SignSocketPositions { get; } = new();
    public List<Node3D> SignSocketRoots { get; } = new();
    public List<Node3D> LibraryShelfSlots { get; } = new();
    public List<Node3D> BakeryDisplaySlots { get; } = new();
    public List<(string Book, Vector3 Position)> BookSpawnPoints { get; } = new();
    public Vector3 BakeryStall { get; protected set; } = new(15.2f, 0f, 5.2f);
    public Vector3 LibraryShelf { get; protected set; } = new(-20.8f, 0f, 8.2f);

    // ---- P14 finale anchors and visuals ----
    public Vector3 RepairCarPosition { get; protected set; } = new(35.2f, 0f, -7.8f);
    public List<Vector3> RepairPartSpawnPoints { get; } = new();
    public List<Vector3> FestivalTablePads { get; } = new();
    public List<Vector3> FestivalLanternPads { get; } = new();
    public List<Node3D> FestivalTableVisuals { get; } = new();
    public List<Node3D> FestivalLanternVisuals { get; } = new();
    public Node3D DeliveryBoard { get; protected set; }
    public Node3D MarketStock { get; protected set; }
    public Node3D PipCarBroken { get; protected set; }
    public Node3D PipCarFixed { get; protected set; }
    public Node3D RallyPodium { get; protected set; }
    public Node3D FestivalDecor { get; protected set; }

    /// <summary>Resolve the authored scene and populate this contract.</summary>
    public abstract void Build();
}
