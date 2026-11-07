using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Builds the whole contiguous village from procedural low-poly primitives.
/// Static world collision is on physics layer 1. Gameplay stations, pickups and
/// construction sockets are added by Main / BuildSystem on top of this.
/// </summary>
public partial class WorldBuilder : Node3D
{
    // ---- Landmark anchors (also used by mission markers) ----
    public Vector3 PlayerSpawn { get; } = new(-38.0f, 0.15f, 9.5f);
    public Vector3 VanSpawn { get; } = new(-30.0f, 0.15f, 14.5f);
    public Vector3 JobBoard { get; } = new(-22.0f, 0.0f, 5.0f);
    public Vector3 PlayerHouseDoor { get; } = new(-38.0f, 0.0f, 18.3f);
    public Vector3 PlayerHouseCenter { get; } = new(-38.0f, 0.0f, 24.0f);
    public Vector3 TownHallDoor { get; } = new(-14.0f, 0.0f, 8.4f);
    public Vector3 TownHallCenter { get; } = new(-14.0f, 0.0f, 14.0f);
    public Vector3 RedHouseDoor { get; } = new(-20.0f, 0.0f, -18.7f);
    public Vector3 RedHouseCenter { get; } = new(-20.0f, 0.0f, -24.0f);
    public Vector3 ShopDoor { get; } = new(5.0f, 0.0f, 3.8f);
    public Vector3 ShopCenter { get; } = new(5.0f, 0.0f, 9.0f);
    public Vector3 WorkshopDoor { get; } = new(25.0f, 0.0f, -10.3f);
    public Vector3 WorkshopCenter { get; } = new(25.0f, 0.0f, -16.0f);
    public Vector3 AppleCrate { get; } = new(-2.5f, 0.0f, 3.6f);
    public Vector3 PostCounter { get; } = new(-9.0f, 0.0f, 8.0f);
    public Vector3 BridgeSite { get; } = new(36.5f, 0.0f, 4.5f);
    public Vector3 BridgeCenter { get; } = new(45.0f, 0.0f, 0.0f);
    public Vector3 PlaygroundCenter { get; } = new(-38.0f, 0.0f, -5.0f);
    public Vector3 ParkCenter { get; } = new(15.0f, 0.0f, 16.0f);
    public Vector3 VillageGreenSign { get; } = new(10.0f, 0.0f, 10.7f);
    public Vector3 GroveCenter { get; } = new(-40.0f, 0.0f, -32.0f);
    public Vector3 StonePatchCenter { get; } = new(18.0f, 0.0f, -34.0f);
    public Vector3 RaceStart { get; } = new(55.0f, 0.0f, 1.5f);
    public Vector3 ParkingSign { get; } = new(-33.0f, 0.0f, 12.5f);
    public Vector3 FurnitureStation { get; } = new(-40.5f, 0.0f, 20.5f);

    public Vector3[] SwingFrames { get; } = { new(-41.0f, 0.0f, -5.0f), new(-35.0f, 0.0f, -5.0f) };
    public (Vector3 Position, float Yaw)[] RaceGates { get; } = new[]
    {
        (new Vector3(62.0f, 0f, 12.0f), 35.0f),
        (new Vector3(76.0f, 0f, 22.0f), 54.5f),
        (new Vector3(85.0f, 0f, 6.0f), 150.5f),
        (new Vector3(74.0f, 0f, -10.0f), -145.5f),
    };

    public Node3D BridgeBarrier { get; private set; }
    public Node3D FinishedBridge { get; private set; }
    public Node3D CelebrationDecor { get; private set; }
    public Node3D RaceStartFlag { get; private set; }
    public Node3D GoldCourseFlag { get; private set; }
    public Node3D PlaygroundFixSign { get; private set; }
    public List<Label3D> BrokenSwingLabels { get; } = new();
    public List<Vector3> WoodSpawnPoints { get; } = new();
    public List<Vector3> StoneSpawnPoints { get; } = new();
    public List<Vector3> AppleSpawnPoints { get; } = new();
    public List<Vector3> ParcelSpawnPoints { get; } = new();
    public List<Vector3> ParkPads { get; } = new();
    public List<Vector3> HousePads { get; } = new();

    private readonly RandomNumberGenerator _rng = new();
    private readonly List<(Vector2 Center, Vector2 HalfExtents)> _keepClear = new();

    private static readonly Color PathSand = Color.FromHtml("D9C49A");
    private static readonly Color RoofBlue = Color.FromHtml("3E7FD8");
    private static readonly Color RoofGreen = Color.FromHtml("3B9B5A");
    private static readonly Color WorkshopWall = Color.FromHtml("E8D5B0");

    public void Build()
    {
        _rng.Seed = 20261107;
        Name = "Village";

        BuildGround();
        BuildRoads();
        BuildBuildings();
        BuildJobBoardArea();
        BuildPlayground();
        BuildWorkSites();
        BuildParkPlot();
        BuildBridgeWorksite();
        BuildRaceMeadow();
        BuildFoliage();
        BuildDecorations();
        BuildClouds();
    }

    // ------------------------------------------------------------------ ground

    private void BuildGround()
    {
        // Island lawn split by the river (river gap is x = 41..49).
        Geo.StaticBox(this, "WestIsland", new Vector3(-7.25f, -0.5f, 0f), new Vector3(96.5f, 1f, 110f), Palette.Grass);
        Geo.StaticBox(this, "EastIsland", new Vector3(72.5f, -0.5f, 0f), new Vector3(47f, 1f, 110f), Palette.Grass);

        // Ocean skirt far below, purely visual.
        Geo.Box(this, new Vector3(18f, -2.4f, 0f), new Vector3(250f, 0.2f, 220f), Palette.Water, null, false, "Ocean");

        // Sandy edges around the island.
        Geo.Box(this, new Vector3(15f, 0.02f, -54.4f), new Vector3(148f, 0.06f, 1.6f), Palette.Sand);
        Geo.Box(this, new Vector3(15f, 0.02f, 54.4f), new Vector3(148f, 0.06f, 1.6f), Palette.Sand);
        Geo.Box(this, new Vector3(-54.4f, 0.02f, 0f), new Vector3(1.6f, 0.06f, 108f), Palette.Sand);
        Geo.Box(this, new Vector3(94.4f, 0.02f, 0f), new Vector3(1.6f, 0.06f, 108f), Palette.Sand);

        // Visible low hedge boundary (and matching colliders).
        Color hedge = Color.FromHtml("2E8B45");
        Geo.StaticBox(this, "BoundNorth", new Vector3(15, 0.7f, -54f), new Vector3(150, 1.4f, 1.2f), hedge);
        Geo.StaticBox(this, "BoundSouth", new Vector3(15, 0.7f, 54f), new Vector3(150, 1.4f, 1.2f), hedge);
        Geo.StaticBox(this, "BoundWest", new Vector3(-54.5f, 0.7f, 0), new Vector3(1.2f, 1.4f, 108f), hedge);
        Geo.StaticBox(this, "BoundEast", new Vector3(94.5f, 0.7f, 0), new Vector3(1.2f, 1.4f, 108f), hedge);

        // River water, banks, foam.
        Geo.Box(this, new Vector3(45f, -1.15f, 0f), new Vector3(8f, 0.16f, 108f), Palette.Water, null, false, "RiverWater");
        Geo.Box(this, new Vector3(45f, -2.25f, 0f), new Vector3(8f, 0.4f, 108f), Palette.Sand, null, false, "RiverBed");
        for (int i = 0; i < 11; i++)
        {
            float z = -48 + i * 9.6f;
            Geo.Box(this, new Vector3(41.2f, -1.02f, z), new Vector3(0.8f, 0.06f, 2.6f), Palette.White, null, true, "Foam");
            Geo.Box(this, new Vector3(48.8f, -1.02f, z + 2f), new Vector3(0.8f, 0.06f, 1.8f), Palette.White, null, true, "Foam");
        }
        // Bank rocks.
        for (int i = 0; i < 7; i++)
        {
            float z = -42 + i * 14f;
            Rock(new Vector3(40.1f, 0.55f, z), 1.3f + (i % 3) * 0.3f);
            Rock(new Vector3(49.9f, 0.55f, z + 6f), 1.1f + (i % 2) * 0.4f);
        }

        // Bridge abutments and the (initially closed) barrier.
        Geo.Box(this, new Vector3(39.9f, -0.25f, 0f), new Vector3(2.2f, 0.5f, 9.5f), Palette.Stone, null, false, "BridgeAbutWest");
        Geo.Box(this, new Vector3(50.1f, -0.25f, 0f), new Vector3(2.2f, 0.5f, 9.5f), Palette.Stone, null, false, "BridgeAbutEast");

        BridgeBarrier = new Node3D { Name = "BridgeBarrier", Position = new Vector3(38.2f, 0, 0) };
        AddChild(BridgeBarrier);
        for (int i = 0; i < 7; i++)
        {
            Geo.Box(BridgeBarrier, new Vector3(0, 0.75f + (i % 2) * 0.25f, -3.1f + i * 1.03f),
                new Vector3(0.22f, 0.52f, 1.05f), i % 2 == 0 ? Palette.Red : Palette.Cream, null, false, "Stripe");
            Geo.Box(BridgeBarrier, new Vector3(0, 1.25f, -3.1f + i * 1.03f),
                new Vector3(0.22f, 0.18f, 1.05f), Palette.Cream);
        }
        Geo.Cylinder(BridgeBarrier, new Vector3(0, 0.65f, -3.6f), 0.14f, 0.14f, 1.3f, Palette.WoodDark, 6);
        Geo.Cylinder(BridgeBarrier, new Vector3(0, 0.65f, 3.6f), 0.14f, 0.14f, 1.3f, Palette.WoodDark, 6);

        AddKeepClear(new Vector3(0, 0, 0), new Vector2(48, 8));
    }

    // ------------------------------------------------------------------- roads

    private void BuildRoads()
    {
        AddRoadRect(new Vector3(-4f, 0, 0), 88f, 6.4f);
        AddRoadRect(new Vector3(68.5f, 0, 0), 39f, 6.4f);
        AddRoadRect(new Vector3(-30f, 0, 9.5f), 5.2f, 19f);
        AddRoadRect(new Vector3(-38f, 0, 9.5f), 3.4f, 19f);
        AddRoadRect(new Vector3(-20f, 0, -10f), 3.8f, 20f);
        AddRoadRect(new Vector3(25f, 0, -5.5f), 4.8f, 11f);
        AddRoadRect(new Vector3(-14f, 0, 4.5f), 3.6f, 9f);
        AddRoadRect(new Vector3(5f, 0, 1.9f), 3.8f, 3.8f);
        AddRoadRect(new Vector3(15f, 0, 9.8f), 3.2f, 19.6f);
        AddRoadRect(new Vector3(-32f, 0, -2.6f), 12f, 3.2f);

        // Main road edge lines and centre dashes.
        for (float x = -46; x < 39; x += 5.4f)
        {
            Geo.Box(this, new Vector3(x, 0.035f, 0), new Vector3(2.5f, 0.02f, 0.16f), Palette.RoadMark);
        }
        for (float x = 51.5f; x < 87; x += 5.4f)
        {
            Geo.Box(this, new Vector3(x, 0.035f, 0), new Vector3(2.5f, 0.02f, 0.16f), Palette.RoadMark);
        }
        for (float x = -46; x < 87; x += 4f)
        {
            if (x > 39 && x < 51) continue;
            Geo.Box(this, new Vector3(x, 0.034f, -3.05f), new Vector3(1.6f, 0.018f, 0.09f), Palette.RoadMark);
            Geo.Box(this, new Vector3(x, 0.034f, 3.05f), new Vector3(1.6f, 0.018f, 0.09f), Palette.RoadMark);
        }
        // Crosswalk outside the town hall.
        for (int i = 0; i < 7; i++)
        {
            Geo.Box(this, new Vector3(-14f, 0.04f, -2.6f + i * 0.9f), new Vector3(4.2f, 0.02f, 0.42f), Palette.RoadMark);
        }

        AddKeepClear(new Vector3(-20, 0, -10), new Vector2(6, 24));
        AddKeepClear(new Vector3(25, 0, -5), new Vector2(8, 15));
    }

    private void AddRoadRect(Vector3 center, float widthX, float lengthZ)
    {
        Geo.Box(this, new Vector3(center.X, 0.02f, center.Z), new Vector3(widthX, 0.035f, lengthZ), Palette.Road, null, false, "Road");
        AddKeepClear(center, new Vector2(widthX * 0.5f + 2.5f, lengthZ * 0.5f + 2.5f));
    }

    private void AddRoadSegment(Vector3 from, Vector3 to, float width, Color color)
    {
        Vector3 delta = to - from;
        float len = delta.Length();
        float yaw = Mathf.RadToDeg(Mathf.Atan2(delta.X, delta.Z));
        Vector3 midpoint = (from + to) * 0.5f;
        Geo.Box(this, new Vector3(midpoint.X, 0.022f, midpoint.Z), new Vector3(width, 0.035f, len),
            color, new Vector3(0, yaw, 0), false, "CourseRoad");
    }

    // --------------------------------------------------------------- buildings

    private void BuildBuildings()
    {
        BuildPlayerHouse();
        BuildTownHall();
        BuildRedHouse();
        BuildShop();
        BuildWorkshop();
    }

    private Node3D BuildHouseShell(string name, Vector3 center, float width, float depth, float wallHeight,
        Color wallColor, Color roofColor, int frontSign, bool enterable, float doorWidth,
        out float frontZ, out Node3D root)
    {
        root = new Node3D { Name = name, Position = center };
        AddChild(root);
        frontZ = frontSign * depth * 0.5f;

        Color trim = Palette.Cream.Darkened(0.06f);
        if (enterable)
        {
            // A thin, non-colliding plinth keeps the doorway walkable for children.
            Geo.Box(root, new Vector3(0, 0.04f, 0),
                new Vector3(width + 0.6f, 0.08f, depth + 0.6f), Palette.Stone, null, false, "Foundation");
            Geo.Box(root, new Vector3(0, 0.035f, frontZ + frontSign * 0.5f),
                new Vector3(doorWidth, 0.07f, 1.2f), Palette.StoneDark, null, false, "DoorThreshold");
        }
        else
        {
            Geo.StaticBox(root, "Foundation", new Vector3(0, 0.2f, 0), new Vector3(width + 0.6f, 0.4f, depth + 0.6f), Palette.Stone);
        }
        // side walls
        Geo.StaticBox(root, "WallWest", new Vector3(-width * 0.5f + 0.15f, wallHeight * 0.5f + 0.25f, 0),
            new Vector3(0.3f, wallHeight, depth), wallColor);
        Geo.StaticBox(root, "WallEast", new Vector3(width * 0.5f - 0.15f, wallHeight * 0.5f + 0.25f, 0),
            new Vector3(0.3f, wallHeight, depth), wallColor);
        // back + front walls
        Geo.StaticBox(root, "WallBack", new Vector3(0, wallHeight * 0.5f + 0.25f, -frontZ),
            new Vector3(width, wallHeight, 0.3f), wallColor);
        if (enterable)
        {
            float segment = (width - doorWidth) * 0.5f;
            Geo.StaticBox(root, "WallFrontL", new Vector3(-(doorWidth * 0.5f + segment * 0.5f), wallHeight * 0.5f + 0.25f, frontZ),
                new Vector3(segment, wallHeight, 0.3f), wallColor);
            Geo.StaticBox(root, "WallFrontR", new Vector3(doorWidth * 0.5f + segment * 0.5f, wallHeight * 0.5f + 0.25f, frontZ),
                new Vector3(segment, wallHeight, 0.3f), wallColor);
            // Door frame.
            Geo.Box(root, new Vector3(-doorWidth * 0.5f, 1.45f, frontZ), new Vector3(0.2f, 2.5f, 0.4f), trim);
            Geo.Box(root, new Vector3(doorWidth * 0.5f, 1.45f, frontZ), new Vector3(0.2f, 2.5f, 0.4f), trim);
            Geo.Box(root, new Vector3(0, 2.5f, frontZ), new Vector3(doorWidth + 0.2f, 0.2f, 0.4f), trim);
            // Invisible camera blocker across the doorway: the player walks
            // through, but the third-person camera cannot slip outside.
            var cameraBlocker = new StaticBody3D
            {
                Name = "DoorCameraBlocker",
                Position = new Vector3(0, 2.4f, frontZ),
                CollisionLayer = 16,
                CollisionMask = 0,
            };
            cameraBlocker.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(doorWidth, 4.8f, 0.12f) },
            });
            root.AddChild(cameraBlocker);
            // An open door leaf against the inner wall.
            Geo.Box(root, new Vector3(-doorWidth * 0.5f - 0.9f, 1.25f, frontZ + frontSign * 0.85f),
                new Vector3(1.9f, 2.3f, 0.12f), Palette.Wood, new Vector3(0, frontSign * 75f, 0));
        }
        else
        {
            Geo.StaticBox(root, "WallFront", new Vector3(0, wallHeight * 0.5f + 0.25f, frontZ),
                new Vector3(width, wallHeight, 0.3f), wallColor);
        }

        // Roof (two pitched slabs, ridge along X).
        float roofHeight = Mathf.Clamp(depth * 0.26f, 1.4f, 2.6f);
        float angle = Mathf.RadToDeg(Mathf.Atan2(roofHeight, depth * 0.5f));
        float slopeLen = Mathf.Sqrt(roofHeight * roofHeight + depth * depth * 0.25f) + 0.7f;
        Geo.Box(root, new Vector3(0, wallHeight + 0.25f + roofHeight * 0.5f, -depth * 0.25f),
            new Vector3(width + 1.2f, 0.26f, slopeLen), roofColor, new Vector3(-angle, 0, 0), false, "RoofNorth");
        Geo.Box(root, new Vector3(0, wallHeight + 0.25f + roofHeight * 0.5f, depth * 0.25f),
            new Vector3(width + 1.2f, 0.26f, slopeLen), roofColor, new Vector3(angle, 0, 0), false, "RoofSouth");
        // Gable infill.
        float gzFront = frontSign * depth * 0.5f;
        Geo.Triangle(root, "GableFront",
            new Vector3(-width * 0.5f, wallHeight + 0.25f, gzFront),
            new Vector3(width * 0.5f, wallHeight + 0.25f, gzFront),
            new Vector3(0, wallHeight + 0.25f + roofHeight, gzFront), wallColor,
            flip: frontSign < 0);
        Geo.Triangle(root, "GableBack",
            new Vector3(-width * 0.5f, wallHeight + 0.25f, -gzFront),
            new Vector3(width * 0.5f, wallHeight + 0.25f, -gzFront),
            new Vector3(0, wallHeight + 0.25f + roofHeight, -gzFront), wallColor,
            flip: frontSign > 0);
        // Chimney for warmth.
        Geo.Box(root, new Vector3(width * 0.28f, wallHeight + roofHeight + 0.1f, -depth * 0.18f),
            new Vector3(0.8f, 1.9f, 0.8f), Color.FromHtml("C7775D"));
        Geo.Box(root, new Vector3(width * 0.28f, wallHeight + roofHeight + 1.08f, -depth * 0.18f),
            new Vector3(1.0f, 0.18f, 1.0f), Palette.StoneDark);

        // Windows (front + sides).
        AddWindow(root, new Vector3(-width * 0.28f, 2.25f, frontZ + frontSign * 0.08f), frontSign);
        AddWindow(root, new Vector3(width * 0.28f, 2.25f, frontZ + frontSign * 0.08f), frontSign);
        AddSideWindow(root, -width * 0.5f - 0.02f, 2.2f, 0f, false);
        AddSideWindow(root, width * 0.5f + 0.02f, 2.2f, 0f, true);

        // Door itself for solid buildings.
        if (!enterable)
        {
            Geo.Box(root, new Vector3(0, 1.35f, frontZ + frontSign * 0.09f), new Vector3(2.0f, 2.2f, 0.14f), Palette.Wood);
            Geo.Box(root, new Vector3(0, 2.55f, frontZ + frontSign * 0.1f), new Vector3(2.5f, 0.22f, 0.3f), trim);
            Geo.Sphere(root, new Vector3(0.65f, 1.35f, frontZ + frontSign * 0.19f), 0.08f, Palette.Yellow);
            Geo.Box(root, new Vector3(0, 0.08f, frontZ + frontSign * 0.8f), new Vector3(3f, 0.14f, 1.5f), Palette.Stone);
        }
        else
        {
            // Interior floor + cosy rug.
            Geo.Box(root, new Vector3(0, 0.035f, 0), new Vector3(width - 0.7f, 0.06f, depth - 0.7f), Color.FromHtml("E8CC9A"));
            Geo.Box(root, new Vector3(0, 0.075f, depth * 0.16f), new Vector3(4.2f, 0.03f, 3f), Palette.Lavender);
            Geo.Box(root, new Vector3(0, 0.08f, depth * 0.16f), new Vector3(3.4f, 0.032f, 2.2f), Palette.Cream);
            var light = new OmniLight3D
            {
                Position = new Vector3(0, wallHeight - 0.4f, 0),
                LightColor = new Color("FFE3B0"),
                LightEnergy = 1.4f,
                OmniRange = 9f,
                ShadowEnabled = false,
            };
            root.AddChild(light);
            Geo.Cylinder(root, new Vector3(0, wallHeight - 0.1f, 0), 0.06f, 0.06f, 0.5f, Palette.Navy, 6);
            Geo.Sphere(root, new Vector3(0, wallHeight - 0.38f, 0), 0.22f, new Color("FFE6A3"), 8, 4, null, "Bulb");
            // Wall picture.
            Geo.Box(root, new Vector3(width * 0.24f, 2.3f, -depth * 0.5f + 0.2f), new Vector3(1.4f, 1.1f, 0.08f), Palette.Lavender);
            Geo.Box(root, new Vector3(width * 0.24f, 2.3f, -depth * 0.5f + 0.25f), new Vector3(1.1f, 0.8f, 0.06f), Palette.Yellow);
        }

        // Porch posts and planters.
        Geo.Box(root, new Vector3(-1.7f, 1.2f, frontZ + frontSign * 0.85f), new Vector3(0.16f, 2.2f, 0.16f), trim);
        Geo.Box(root, new Vector3(1.7f, 1.2f, frontZ + frontSign * 0.85f), new Vector3(0.16f, 2.2f, 0.16f), trim);
        Geo.Box(root, new Vector3(0, 2.32f, frontZ + frontSign * 0.85f), new Vector3(3.7f, 0.16f, 0.5f), roofColor);
        AddFlowerPlanter(root, new Vector3(-2.5f, 0, frontZ + frontSign * 0.8f));
        AddFlowerPlanter(root, new Vector3(2.5f, 0, frontZ + frontSign * 0.8f));

        return root;
    }

    private static void AddWindow(Node root, Vector3 pos, int frontSign)
    {
        Geo.Box(root, pos, new Vector3(1.55f, 1.45f, 0.08f), Palette.Cream);
        Geo.Box(root, pos + new Vector3(0, 0, frontSign * 0.04f), new Vector3(1.25f, 1.15f, 0.1f), Color.FromHtml("8FD7FF"));
        Geo.Box(root, pos + new Vector3(0, 0, frontSign * 0.06f), new Vector3(0.07f, 1.15f, 0.11f), Palette.Cream);
        Geo.Box(root, pos + new Vector3(0, 0.12f, frontSign * 0.06f), new Vector3(1.25f, 0.07f, 0.11f), Palette.Cream);
    }

    private static void AddSideWindow(Node root, float x, float y, float z, bool east)
    {
        int sign = east ? 1 : -1;
        Geo.Box(root, new Vector3(x + sign * 0.02f, y, z), new Vector3(0.08f, 1.3f, 1.4f), Palette.Cream);
        Geo.Box(root, new Vector3(x + sign * 0.05f, y, z), new Vector3(0.1f, 1.0f, 1.1f), Color.FromHtml("8FD7FF"));
        Geo.Box(root, new Vector3(x + sign * 0.07f, y, z), new Vector3(0.11f, 0.07f, 1.1f), Palette.Cream);
    }

    private void BuildPlayerHouse()
    {
        var root = BuildHouseShell("PlayerHouse", PlayerHouseCenter, 13f, 10f, 3.4f,
            Palette.Cream, RoofBlue, -1, true, 2.4f, out var frontZ, out _);
        Geo.SignBoard(root, new Vector3(0, 2.85f, frontZ - 0.22f), 180f, "YOUR HOUSE", Palette.Blue, 5.6f, 1.15f, "house", 58);
        // Blue mailbox by the path.
        Geo.Cylinder(this, new Vector3(-33.2f, 0.7f, 17.2f), 0.09f, 0.09f, 1.4f, Palette.WoodDark, 6);
        Geo.Box(this, new Vector3(-33.2f, 1.55f, 17.2f), new Vector3(0.7f, 0.65f, 1.1f), Palette.Blue);
        Geo.Box(this, new Vector3(-33.2f, 1.72f, 17.77f), new Vector3(0.5f, 0.12f, 0.05f), Palette.Cream);
        Geo.Label(this, new Vector3(-33.2f, 2.3f, 17.2f), "MAIL", 40, Palette.Navy, true, 0.005f);
        // Interior furniture catalogue stand.
        Geo.Box(this, new Vector3(-40.5f, 0.45f, 21.1f), new Vector3(1.7f, 0.9f, 0.7f), Palette.Wood);
        Geo.Box(this, new Vector3(-40.5f, 0.93f, 21.1f), new Vector3(1.5f, 0.1f, 0.55f), Palette.Cream);
        Geo.Box(this, new Vector3(-40.5f, 1.18f, 21.05f), new Vector3(0.55f, 0.4f, 0.06f), Palette.Lavender,
            new Vector3(-16, 0, 0));
        Geo.Label(this, new Vector3(-40.5f, 1.65f, 21.1f), "FURNITURE", 42, Palette.Navy, true, 0.006f);
        // Keep yard clear / pads for furniture (grid of six).
        for (int i = 0; i < 6; i++)
        {
            float x = (i % 3 - 1) * 2.8f;
            float z = -0.7f + (i / 3) * 3.3f;
            HousePads.Add(PlayerHouseCenter + new Vector3(x, 0.08f, z));
        }
        foreach (var pad in HousePads)
        {
            Geo.Box(this, new Vector3(pad.X, 0.06f, pad.Z), new Vector3(2.2f, 0.025f, 2.2f),
                new Color(0.95f, 0.87f, 0.66f, 0.8f), null, false, "HousePad");
        }
    }

    private void BuildTownHall()
    {
        var root = BuildHouseShell("TownHall", TownHallCenter, 15f, 10f, 4.0f,
            Palette.Cream, RoofBlue, -1, false, 2.2f, out var frontZ, out _);
        Geo.SignBoard(root, new Vector3(0, 3.35f, frontZ - 0.22f), 180f, "TOWN HALL", Palette.Blue, 7.2f, 1.25f, "star", 62);
        // Clock tower.
        Geo.Box(root, new Vector3(0, 5.4f, 0.2f), new Vector3(2.3f, 2.5f, 2.1f), Palette.Cream);
        Geo.Box(root, new Vector3(0, 6.75f, 0.2f), new Vector3(2.8f, 0.3f, 2.6f), RoofBlue);
        Geo.Cylinder(root, new Vector3(0, 6.1f, -0.88f), 0.62f, 0.62f, 0.12f, Palette.Cream, 14, new Vector3(90, 0, 0));
        Geo.Cylinder(root, new Vector3(0, 6.1f, -0.95f), 0.53f, 0.53f, 0.08f, Palette.White, 14, new Vector3(90, 0, 0));
        Geo.Box(root, new Vector3(0, 6.18f, -1.0f), new Vector3(0.07f, 0.38f, 0.05f), Palette.Navy);
        Geo.Box(root, new Vector3(0.14f, 6.1f, -1.0f), new Vector3(0.28f, 0.06f, 0.05f), Palette.Navy);
        Geo.Sphere(root, new Vector3(0, 7.2f, 0.2f), 0.45f, Palette.Yellow);
        // Wide steps (visual, no step collision).
        Geo.Box(this, new Vector3(-14, 0.08f, 8.2f), new Vector3(8, 0.16f, 1.6f), Palette.Stone);
        Geo.Box(this, new Vector3(-14, 0.16f, 8.6f), new Vector3(6, 0.16f, 1.0f), Palette.Stone);
        // Little notice sign, outside the door.
        Geo.SignBoard(this, new Vector3(-18.2f, 0.8f, 8.4f), 180f, "POST", Palette.Red, 2.0f, 0.6f, "parcel", 40);
        // Post counter prop.
        Geo.Box(this, new Vector3(-9.0f, 0.6f, 8.0f), new Vector3(2.4f, 1.2f, 1.0f), Palette.Wood);
        Geo.Box(this, new Vector3(-9.0f, 0.72f, 8.0f), new Vector3(2.1f, 0.1f, 0.85f), Palette.Cream);
    }

    private void BuildRedHouse()
    {
        var root = BuildHouseShell("RedHouse", RedHouseCenter, 11f, 9f, 3.4f,
            Palette.Cream, Palette.Red, 1, false, 2.2f, out var frontZ, out _);
        Geo.SignBoard(root, new Vector3(0, 2.85f, frontZ + 0.22f), 0f, "RED HOUSE", Palette.Red, 6.2f, 1.2f, "house", 58);
        // Red mailbox.
        Geo.Cylinder(this, new Vector3(-15.0f, 0.7f, -18.2f), 0.09f, 0.09f, 1.4f, Palette.WoodDark, 6);
        Geo.Box(this, new Vector3(-15.0f, 1.55f, -18.2f), new Vector3(0.7f, 0.65f, 1.1f), Palette.Red);
        Geo.Label(this, new Vector3(-15.0f, 2.25f, -18.2f), "RED", 42, Palette.Navy, true, 0.005f);
        // Picket fence and flowers.
        FenceLine(new Vector3(-26.5f, 0, -18.2f), new Vector3(-29.5f, 0, -23f), Palette.White);
        FenceLine(new Vector3(-13.5f, 0, -18.2f), new Vector3(-10.5f, 0, -23f), Palette.White);
        for (int i = 0; i < 6; i++)
        {
            Flower(new Vector3(-26f + i * 1.2f, 0, -17.2f), i % 2 == 0 ? Palette.Red : Palette.Pink);
        }
        // A bright red arrow sign near the door.
        Geo.SignBoard(this, new Vector3(-27.2f, 1.4f, -16.0f), -35f, "RED >", Palette.Red, 2.1f, 0.65f, "apple", 38);
    }

    private void BuildShop()
    {
        var root = BuildHouseShell("Market", ShopCenter, 12f, 9f, 3.4f,
            Color.FromHtml("FFF1CA"), RoofBlue, -1, false, 2.2f, out var frontZ, out _);
        Geo.SignBoard(root, new Vector3(0, 2.9f, frontZ - 0.23f), 180f, "SHOP", Palette.Blue, 4.4f, 1.15f, "basket", 60);
        // Striped awning.
        for (int i = 0; i < 10; i++)
        {
            float x = -5.6f + i * 1.24f;
            Geo.Box(root, new Vector3(x, 2.3f, frontZ - 0.85f), new Vector3(1.23f, 0.18f, 1.8f),
                i % 2 == 0 ? Palette.Blue : Palette.Cream, new Vector3(-12, 0, 0));
        }
        Geo.Box(root, new Vector3(0, 2.0f, frontZ - 1.55f), new Vector3(12.2f, 0.14f, 0.18f), Palette.Blue);
        // Produce stall / apple crate at the front.
        Geo.Box(this, new Vector3(AppleCrate.X, 0.55f, AppleCrate.Z), new Vector3(2.3f, 0.7f, 1.2f), Palette.Wood);
        Geo.Box(this, new Vector3(AppleCrate.X, 0.82f, AppleCrate.Z), new Vector3(2.1f, 0.18f, 1.0f), Color.FromHtml("7B5A37"));
        Geo.SignBoard(this, new Vector3(AppleCrate.X - 2.9f, 1.15f, AppleCrate.Z - 0.5f), 180f, "APPLES", Palette.Red, 2.4f, 0.62f, "apple", 38);
        // Baskets and crates.
        Crate(new Vector3(-2.0f, 0, 2.2f));
        Crate(new Vector3(9.5f, 0, 2.2f));
        Crate(new Vector3(10.0f, 0, 2.7f));
        Geo.Cylinder(this, new Vector3(8.2f, 0.55f, 2.0f), 0.45f, 0.5f, 0.8f, Palette.Wood, 8);
        AddKeepClear(new Vector3(ShopCenter.X, 0, 1.2f), new Vector2(8, 4));
    }

    private void BuildWorkshop()
    {
        var root = BuildHouseShell("Workshop", WorkshopCenter, 13f, 10f, 3.7f,
            WorkshopWall, RoofGreen, 1, false, 2.2f, out var frontZ, out _);
        Geo.SignBoard(root, new Vector3(0, 3.1f, frontZ + 0.23f), 0f, "WORKSHOP", Palette.Orange, 7.2f, 1.2f, "hammer", 58);
        // Wide workshop door.
        Geo.Box(root, new Vector3(0, 1.75f, frontZ + 0.18f), new Vector3(4.4f, 3.2f, 0.12f), Color.FromHtml("5A4634"));
        Geo.Box(root, new Vector3(0, 3.35f, frontZ + 0.22f), new Vector3(5.2f, 0.3f, 0.3f), Palette.WoodDark);
        // Tool props / wood pile.
        Geo.Box(this, new Vector3(30.2f, 0.35f, -9.7f), new Vector3(2.4f, 0.7f, 1.5f), Palette.Wood);
        for (int i = 0; i < 5; i++)
        {
            Geo.Cylinder(this, new Vector3(20.0f + i * 0.45f, 0.3f, -8.9f), 0.22f, 0.22f, 2.2f, Palette.Wood, 6,
                new Vector3(0, 0, 90));
        }
        Crate(new Vector3(30.6f, 0, -12.5f));
        Crate(new Vector3(31.4f, 0, -12.7f));
        // Bridge-parts sign.
        Geo.SignBoard(this, new Vector3(33.5f, 1.0f, -9.4f), 0f, "BRIDGE PARTS", Palette.Orange, 3.4f, 0.7f, "wood", 34);
        Geo.Box(this, new Vector3(33.5f, 0.3f, -10.2f), new Vector3(2.8f, 0.6f, 1.4f), Palette.Wood);
    }

    // ------------------------------------------------------------------ job board

    private void BuildJobBoardArea()
    {
        AddRoadRect(new Vector3(-22f, 0, 3.0f), 6.5f, 4.0f);
        AddKeepClear(new Vector3(-22, 0, 5), new Vector2(5, 4));
        var posts = new Node3D { Name = "JobBoard", Position = JobBoard };
        AddChild(posts);
        float yaw = 180f; // face north, toward the road and approaching player
        posts.RotationDegrees = new Vector3(0, yaw, 0);
        Geo.Box(posts, new Vector3(-1.8f, 1.3f, 0), new Vector3(0.2f, 2.6f, 0.2f), Palette.WoodDark);
        Geo.Box(posts, new Vector3(1.8f, 1.3f, 0), new Vector3(0.2f, 2.6f, 0.2f), Palette.WoodDark);
        Geo.Box(posts, new Vector3(0, 1.85f, 0), new Vector3(4.6f, 1.55f, 0.18f), Palette.Cream);
        Geo.Box(posts, new Vector3(0, 2.65f, 0), new Vector3(4.9f, 0.24f, 0.3f), Palette.Yellow);
        Geo.Box(posts, new Vector3(0, 1.08f, 0), new Vector3(4.9f, 0.22f, 0.3f), Palette.Yellow);
        Geo.Box(posts, new Vector3(0, 2.2f, 0.11f), new Vector3(3.4f, 0.06f, 0.04f), Palette.Stone);
        Geo.Label(posts, new Vector3(0, 2.32f, 0.11f), "JOBS", 72, Palette.Navy, false, 0.008f);
        Geo.Label(posts, new Vector3(0, 1.82f, 0.11f), "Pick a job here", 32, Palette.Navy, false, 0.006f);
        Geo.IconSprite(posts, new Vector3(0, 3.55f, 0), "star", 0.035f, true);
        AddFlowerPlanter(posts, new Vector3(-2.0f, 0, 0.4f));
        AddFlowerPlanter(posts, new Vector3(2.0f, 0, 0.4f));

        // Direction post by the road, intentionally readable.
        var sign = new Node3D { Name = "DirectionPost", Position = new Vector3(-10.8f, 0, 2.8f) };
        AddChild(sign);
        Geo.Box(sign, new Vector3(0, 1.7f, 0), new Vector3(0.16f, 3.4f, 0.16f), Palette.WoodDark);
        Geo.SignBoard(sign, new Vector3(-0.9f, 2.8f, 0.1f), -90f, "RED HOUSE", Palette.Red, 2.8f, 0.55f, "house", 32);
        Geo.SignBoard(sign, new Vector3(0.9f, 2.15f, 0.1f), 90f, "WORKSHOP", Palette.Orange, 2.8f, 0.55f, "hammer", 30);
        Geo.SignBoard(sign, new Vector3(-0.7f, 1.45f, 0.1f), -90f, "SHOP", Palette.Blue, 2.2f, 0.5f, "basket", 32);
        Geo.SignBoard(sign, new Vector3(0.8f, 0.8f, 0.1f), 90f, "BRIDGE", Palette.Water, 2.2f, 0.5f, "waypoint", 30);
    }

    // ------------------------------------------------------------------ playground

    private void BuildPlayground()
    {
        Geo.Box(this, new Vector3(PlaygroundCenter.X, 0.015f, PlaygroundCenter.Z), new Vector3(18f, 0.03f, 13f),
            Color.FromHtml("D8D09B"), null, false, "PlaygroundSand");
        Geo.SignBoard(this, new Vector3(PlaygroundCenter.X - 6.0f, 2.1f, PlaygroundCenter.Z + 6.5f), 0f, "PLAYGROUND", Palette.Lavender, 5.2f, 0.9f, "flag", 46);
        Geo.Box(this, new Vector3(PlaygroundCenter.X - 8.8f, 0.8f, PlaygroundCenter.Z + 6.5f), new Vector3(0.18f, 1.6f, 0.18f), Palette.WoodDark);
        Geo.Box(this, new Vector3(PlaygroundCenter.X - 3.2f, 0.8f, PlaygroundCenter.Z + 6.5f), new Vector3(0.18f, 1.6f, 0.18f), Palette.WoodDark);

        foreach (var frame in SwingFrames)
        {
            var f = new Node3D { Name = "SwingFrame", Position = frame };
            AddChild(f);
            Color wood = Palette.Wood;
            // A-frame legs.
            Geo.Box(f, new Vector3(-1.8f, 1.6f, 0), new Vector3(0.22f, 3.4f, 0.22f), wood, new Vector3(0, 0, -12));
            Geo.Box(f, new Vector3(1.8f, 1.6f, 0), new Vector3(0.22f, 3.4f, 0.22f), wood, new Vector3(0, 0, 12));
            Geo.Box(f, new Vector3(-1.8f, 1.6f, 1.8f), new Vector3(0.22f, 3.4f, 0.22f), wood, new Vector3(12, 0, -12));
            Geo.Box(f, new Vector3(1.8f, 1.6f, 1.8f), new Vector3(0.22f, 3.4f, 0.22f), wood, new Vector3(-12, 0, 12));
            Geo.Box(f, new Vector3(0, 3.26f, 0.9f), new Vector3(4.2f, 0.24f, 0.24f), Palette.WoodDark);
            // Empty frame sign.
            BrokenSwingLabels.Add(Geo.Label(f, new Vector3(0, 3.75f, 0.9f), "BROKEN", 36, Palette.Navy, true, 0.006f));
        }
        // Sandpit and a broken rope heap.
        Geo.Box(this, new Vector3(-43f, 0.08f, -9.0f), new Vector3(5f, 0.16f, 4f), Color.FromHtml("E4CF92"));
        Geo.Box(this, new Vector3(-43f, 0.14f, -9.0f), new Vector3(4.4f, 0.2f, 3.4f), Color.FromHtml("F0DFA9"));
        Geo.Cylinder(this, new Vector3(-32.8f, 0.3f, -8.0f), 0.14f, 0.14f, 1.8f, Palette.WoodDark, 6,
            new Vector3(0, 0, 70));
        Geo.Box(this, new Vector3(-32.2f, 0.3f, -8.3f), new Vector3(0.9f, 0.2f, 0.6f), Palette.Blue);
        PlaygroundFixSign = Geo.SignBoard(this, new Vector3(-38f, 0.8f, -9.6f), 0f, "FIX ME", Palette.Red, 2.4f, 0.6f, "hammer", 34);
        AddKeepClear(PlaygroundCenter, new Vector2(11, 8));
    }

    // ------------------------------------------------------------------ work sites

    private void BuildWorkSites()
    {
        // Grove clearing.
        Geo.Box(this, new Vector3(GroveCenter.X, 0.015f, GroveCenter.Z), new Vector3(18f, 0.03f, 14f),
            Color.FromHtml("6DBF4A"), null, false, "GroveClearing");
        Geo.SignBoard(this, new Vector3(GroveCenter.X + 2.0f, 2.1f, GroveCenter.Z + 7.0f), 0f, "GROVE — 5 WOOD", Palette.GrassDark, 5.2f, 0.8f, "wood", 38);
        for (int i = 0; i < 5; i++)
        {
            WoodSpawnPoints.Add(new Vector3(GroveCenter.X - 6f + i * 2.2f, 0.0f, GroveCenter.Z - 2.3f + (i % 2) * 1.0f));
        }
        Geo.StaticBox(this, "GroveStump", new Vector3(GroveCenter.X + 6.5f, 0.3f, GroveCenter.Z + 4.2f),
            new Vector3(1.2f, 0.6f, 1.2f), Palette.WoodDark);
        AddRoadSegment(new Vector3(-26f, 0, -22f), new Vector3(-34f, 0, -30f), 3.5f, PathSand);
        AddRoadSegment(new Vector3(-34f, 0, -30f), new Vector3(-38f, 0, -31f), 3.5f, PathSand);
        AddKeepClear(GroveCenter, new Vector2(12, 10));

        // Stone patch.
        Geo.Box(this, new Vector3(StonePatchCenter.X, 0.015f, StonePatchCenter.Z), new Vector3(14f, 0.03f, 11f),
            Color.FromHtml("B6C06D"), null, false, "StoneClearing");
        Geo.SignBoard(this, new Vector3(StonePatchCenter.X, 2.1f, StonePatchCenter.Z + 5.8f), 0f, "STONE PATCH — 3", Palette.StoneDark, 5.4f, 0.8f, "stone", 38);
        for (int i = 0; i < 3; i++)
        {
            StoneSpawnPoints.Add(new Vector3(StonePatchCenter.X - 3.4f + i * 3.2f, 0.0f, StonePatchCenter.Z - 0.8f + (i % 2) * 2.0f));
        }
        for (int i = 0; i < 7; i++)
        {
            Rock(new Vector3(StonePatchCenter.X - 7f + i * 2.2f, 0.55f, StonePatchCenter.Z + 3.2f + (i % 3) * 0.8f), 0.9f + (i % 3) * 0.35f);
        }
        AddKeepClear(StonePatchCenter, new Vector2(10, 8));

        // Apple pickup positions around the market crate.
        for (int i = 0; i < 3; i++)
            AppleSpawnPoints.Add(new Vector3(AppleCrate.X - 0.7f + i * 0.7f, 1.15f, AppleCrate.Z + 0.1f));
        // Parcel pickup positions by the post counter.
        for (int i = 0; i < 3; i++)
            ParcelSpawnPoints.Add(new Vector3(PostCounter.X - 0.8f + i * 0.8f, 1.3f, PostCounter.Z - 0.1f));
    }

    private void BuildParkPlot()
    {
        // 14 x 10 m green with a stone border, six snap pads.
        Geo.Box(this, new Vector3(ParkCenter.X, 0.015f, ParkCenter.Z), new Vector3(14f, 0.03f, 10f), Color.FromHtml("9BDD6E"), null, false, "VillageGreen");
        Color border = Palette.Lavender;
        Geo.Box(this, new Vector3(ParkCenter.X, 0.06f, ParkCenter.Z - 5f), new Vector3(14.4f, 0.12f, 0.25f), border);
        Geo.Box(this, new Vector3(ParkCenter.X, 0.06f, ParkCenter.Z + 5f), new Vector3(14.4f, 0.12f, 0.25f), border);
        Geo.Box(this, new Vector3(ParkCenter.X - 7f, 0.06f, ParkCenter.Z), new Vector3(0.25f, 0.12f, 10.2f), border);
        Geo.Box(this, new Vector3(ParkCenter.X + 7f, 0.06f, ParkCenter.Z), new Vector3(0.25f, 0.12f, 10.2f), border);
        Geo.SignBoard(this, new Vector3(VillageGreenSign.X, 1.9f, VillageGreenSign.Z), 180f, "VILLAGE GREEN", Palette.Lavender, 5.4f, 0.85f, "star", 40);
        for (int i = 0; i < 6; i++)
        {
            float x = ParkCenter.X + (i % 3 - 1) * 3.7f;
            float z = ParkCenter.Z - 1.5f + (i / 3) * 3.2f;
            ParkPads.Add(new Vector3(x, 0.09f, z));
            Geo.Box(this, new Vector3(x, 0.07f, z), new Vector3(2.7f, 0.04f, 2.3f), Color.FromHtml("D6CFA5"), null, false, "ParkPad");
        }
        FenceLine(new Vector3(7.5f, 0, 11.2f), new Vector3(7.5f, 0, 20.5f), Palette.White);
        AddKeepClear(ParkCenter, new Vector2(9, 7));
    }

    private void BuildBridgeWorksite()
    {
        Geo.SignBoard(this, new Vector3(35.2f, 2.2f, 5.2f), 180f, "BRIDGE WORKS", Palette.Red, 4.6f, 0.85f, "hammer", 38);
        Geo.Box(this, new Vector3(36.2f, 0.45f, 5.4f), new Vector3(2.2f, 0.9f, 1.0f), Palette.Wood);
        for (int i = 0; i < 3; i++)
        {
            Geo.Box(this, new Vector3(36.2f - 0.6f + i * 0.6f, 0.5f + i * 0.0f, 5.4f),
                new Vector3(0.4f, 1.0f + i * 0.1f, 0.6f), Palette.WoodDark);
        }
        // Decorative construction cones.
        for (int i = 0; i < 3; i++)
        {
            float z = -4.0f + i * 4.0f;
            Geo.Cone(this, new Vector3(37.4f, 0.45f, z), 0.35f, 0.9f, Palette.Orange, 8);
            Geo.Box(this, new Vector3(37.4f, 0.12f, z), new Vector3(0.8f, 0.1f, 0.8f), Palette.Navy);
        }
    }

    // ------------------------------------------------------------------ race meadow

    private void BuildRaceMeadow()
    {
        // Wide, forgiving meadow track loop.
        Vector3 start = RaceStart;
        Vector3 g1 = RaceGates[0].Position;
        Vector3 g2 = RaceGates[1].Position;
        Vector3 g3 = RaceGates[2].Position;
        Vector3 g4 = RaceGates[3].Position;
        AddRoadSegment(start, g1, 9.0f, Color.FromHtml("8A8491"));
        AddRoadSegment(g1, g2, 9.0f, Color.FromHtml("8A8491"));
        AddRoadSegment(g2, g3, 9.0f, Color.FromHtml("8A8491"));
        AddRoadSegment(g3, g4, 9.0f, Color.FromHtml("8A8491"));
        AddRoadSegment(g4, start, 9.0f, Color.FromHtml("8A8491"));
        // Shortcut through the meadow.
        AddRoadSegment(new Vector3(68f, 0, 16f), new Vector3(78f, 0, 10f), 5.5f, PathSand);
        Geo.SignBoard(this, new Vector3(71f, 1.4f, 18f), 180f, "SHORTCUT", Palette.Yellow, 3.0f, 0.65f, "waypoint", 30);

        Geo.SignBoard(this, new Vector3(54.5f, 2.7f, 4.2f), 180f, "RACE MEADOW", Palette.Yellow, 6.0f, 1.0f, "flag", 46);
        Geo.SignBoard(this, new Vector3(54.5f, 1.2f, 4.2f), 180f, "DRIVE GATES 1-2-3-4", Palette.Red, 5.4f, 0.55f, null, 28);
        // Start/finish checks.
        for (int i = 0; i < 8; i++)
        {
            Geo.Box(this, new Vector3(52.0f, 0.045f, -4.2f + i * 1.05f), new Vector3(0.8f, 0.025f, 0.5f), Palette.RoadMark);
        }
        for (int i = 0; i < 4; i++)
            Geo.Cone(this, new Vector3(52.0f, 0.4f, -5.5f + i * 3.4f), 0.3f, 0.75f, Palette.Orange, 8);

        for (int i = 0; i < RaceGates.Length; i++)
        {
            BuildRaceGate(i + 1, RaceGates[i].Position, RaceGates[i].Yaw);
        }

        // Race flags around the meadow.
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Pi * 2f / 8f;
            Vector3 p = new Vector3(72f + Mathf.Cos(angle) * 19f, 0, 7f + Mathf.Sin(angle) * 19f);
            Geo.Box(this, new Vector3(p.X, 1.5f, p.Z), new Vector3(0.1f, 3f, 0.1f), Palette.WoodDark);
            Geo.Box(this, new Vector3(p.X + 0.4f, 2.8f, p.Z), new Vector3(0.8f, 0.6f, 0.06f),
                i % 2 == 0 ? Palette.Yellow : Palette.Red, null, true);
        }
        // Grandstand decoration.
        Geo.StaticBox(this, "Grandstand", new Vector3(58.0f, 0.7f, -15.0f), new Vector3(8f, 1.4f, 2.5f), Color.FromHtml("E9D8B7"));
        Geo.Box(this, new Vector3(58.0f, 1.5f, -14.5f), new Vector3(8.2f, 0.25f, 1.3f), Palette.Red);
        for (int i = 0; i < 3; i++)
            Flag(new Vector3(56f + i * 2.5f, 0, -16.5f), i % 2 == 0 ? Palette.Yellow : Palette.Lavender);

        RaceStartFlag = new Node3D { Name = "RaceStartFlag", Position = new Vector3(52f, 0, -6.8f) };
        AddChild(RaceStartFlag);
        Geo.Box(RaceStartFlag, new Vector3(0, 3.0f, 0), new Vector3(0.12f, 6f, 0.12f), Palette.WoodDark);
        Geo.Box(RaceStartFlag, new Vector3(1.0f, 5.4f, 0), new Vector3(2.0f, 1.2f, 0.08f), Palette.Yellow, null, true);
        Geo.Label(RaceStartFlag, new Vector3(1.0f, 5.4f, 0.06f), "RACE", 54, Palette.Navy, false, 0.006f);

        // M09's gold pennant appears only after the bonus race is completed.
        GoldCourseFlag = new Node3D { Name = "GoldCourseFlag", Position = new Vector3(54.4f, 0, -6.8f), Visible = false };
        AddChild(GoldCourseFlag);
        Color gold = Color.FromHtml("F4C542");
        Geo.Cylinder(GoldCourseFlag, new Vector3(0, 2.4f, 0), 0.09f, 0.12f, 4.8f, Palette.WoodDark, 6);
        Geo.Box(GoldCourseFlag, new Vector3(1.05f, 4.45f, 0), new Vector3(2.1f, 1.35f, 0.08f), gold, null, true);
        Geo.Label(GoldCourseFlag, new Vector3(1.05f, 4.45f, 0.06f), "GOLD LAP", 42, Palette.Navy, false, 0.006f);
        Geo.Sphere(GoldCourseFlag, new Vector3(0, 5.05f, 0), 0.28f, gold, 8, 4, null, "GoldOrb");
        AddKeepClear(new Vector3(72, 0, 6), new Vector2(23, 23));
    }

    private void BuildRaceGate(int number, Vector3 position, float yaw)
    {
        var root = new Node3D { Name = $"RaceGate{number}", Position = position, RotationDegrees = new Vector3(0, yaw, 0) };
        AddChild(root);
        Color gateColor = number switch
        {
            1 => Palette.Yellow,
            2 => Palette.Blue,
            3 => Palette.Lavender,
            _ => Palette.Orange,
        };
        Geo.Box(root, new Vector3(-5.2f, 2.4f, 0), new Vector3(0.8f, 4.8f, 0.8f), gateColor);
        Geo.Box(root, new Vector3(5.2f, 2.4f, 0), new Vector3(0.8f, 4.8f, 0.8f), gateColor);
        Geo.Box(root, new Vector3(0, 4.75f, 0), new Vector3(11.2f, 0.8f, 0.8f), gateColor);
        Geo.Box(root, new Vector3(0, 5.45f, 0), new Vector3(3.2f, 2.3f, 0.3f), Palette.Cream);
        Geo.Label(root, new Vector3(0, 5.45f, 0.19f), number.ToString(), 180, Palette.Navy, false, 0.006f);
        Geo.Label(root, new Vector3(0, 5.45f, -0.19f), number.ToString(), 180, Palette.Navy, false, 0.006f,
            0, new Vector3(0, 180, 0));
        Geo.Label(root, new Vector3(0, 6.9f, 0), number.ToString(), 160, gateColor, true, 0.007f);
        Flag(root, new Vector3(-5.2f, 4.8f, 0), gateColor);
        Flag(root, new Vector3(5.2f, 4.8f, 0), gateColor);

        // Camera-only blockers keep the view out of the heavy gate posts/beam.
        var cameraBlocker = new StaticBody3D
        {
            Name = "GateCameraBlocker",
            CollisionLayer = 16,
            CollisionMask = 0,
        };
        cameraBlocker.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(0.9f, 4.9f, 0.9f) },
            Position = new Vector3(-5.2f, 2.4f, 0),
        });
        cameraBlocker.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(0.9f, 4.9f, 0.9f) },
            Position = new Vector3(5.2f, 2.4f, 0),
        });
        cameraBlocker.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(11.2f, 0.9f, 0.9f) },
            Position = new Vector3(0, 4.75f, 0),
        });
        root.AddChild(cameraBlocker);
    }

    // ------------------------------------------------------------------- foliage

    private void BuildFoliage()
    {
        // Hand-placed clusters.
        Tree(new Vector3(-48, 0, 30), 1.1f);
        Tree(new Vector3(-46, 0, 4), 1.0f);
        Tree(new Vector3(-47, 0, -12), 1.2f);
        Tree(new Vector3(-28, 0, 32), 1.0f);
        Tree(new Vector3(-10, 0, 28), 1.1f);
        Tree(new Vector3(14, 0, 31), 0.9f);
        Tree(new Vector3(30, 0, 13), 1.0f);
        Tree(new Vector3(36, 0, -24), 1.2f);
        Tree(new Vector3(-8, 0, -14), 0.8f);
        Tree(new Vector3(12, 0, -12), 1.0f);
        Tree(new Vector3(31, 0, -9), 0.8f);
        Tree(new Vector3(-50, 0, -38), 1.1f);
        Tree(new Vector3(-33, 0, -43), 1.2f);
        Tree(new Vector3(-25, 0, -36), 0.9f);
        Tree(new Vector3(-12, 0, -40), 1.1f);
        Tree(new Vector3(4, 0, -38), 1.0f);
        Tree(new Vector3(30, 0, -35), 1.2f);
        Tree(new Vector3(40, 0, -30), 0.9f);
        Fir(new Vector3(-44, 0, -44), 1.1f);
        Fir(new Vector3(-15, 0, -46), 1.2f);
        Fir(new Vector3(36, 0, 30), 1.1f);
        Fir(new Vector3(87, 0, 35), 1.1f);
        Fir(new Vector3(87, 0, -26), 1.0f);
        Tree(new Vector3(86, 0, 28), 0.9f);
        Tree(new Vector3(85, 0, -22), 1.0f);
        Tree(new Vector3(62, 0, -28), 1.2f);
        Tree(new Vector3(90, 0, 12), 0.9f);

        // Grove cluster leaves a clear southern entrance for the wood pickups.
        Tree(new Vector3(GroveCenter.X - 12, 0, GroveCenter.Z - 2), 1.0f);
        Tree(new Vector3(GroveCenter.X - 10, 0, GroveCenter.Z - 11), 1.1f);
        Tree(new Vector3(GroveCenter.X - 3, 0, GroveCenter.Z - 13), 0.9f);
        Tree(new Vector3(GroveCenter.X + 7, 0, GroveCenter.Z - 9), 1.0f);
        Tree(new Vector3(GroveCenter.X + 11, 0, GroveCenter.Z + 1), 1.2f);
        Tree(new Vector3(GroveCenter.X - 9, 0, GroveCenter.Z + 8), 0.9f);
        Fir(new Vector3(GroveCenter.X + 13, 0, GroveCenter.Z + 6), 0.9f);
        // Bushes and flowers.
        for (int i = 0; i < 18; i++)
        {
            Vector3 p = RandomOpenPoint(14);
            Bush(p, 0.7f + _rng.Randf() * 0.7f);
        }
        for (int i = 0; i < 35; i++)
        {
            Vector3 p = RandomOpenPoint(8);
            Flower(p, i % 3 == 0 ? Palette.Pink : (i % 3 == 1 ? Palette.Yellow : Palette.White));
        }
    }

    private Vector3 RandomOpenPoint(float margin)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float x = _rng.RandfRange(-50f, 90f);
            float z = _rng.RandfRange(-49f, 49f);
            Vector3 p = new(x, 0, z);
            bool clear = true;
            foreach (var rect in _keepClear)
            {
                if (Mathf.Abs(p.X - rect.Center.X) < rect.HalfExtents.X + margin &&
                    Mathf.Abs(p.Z - rect.Center.Y) < rect.HalfExtents.Y + margin)
                {
                    clear = false;
                    break;
                }
            }
            if (clear && (p.X < 39 || p.X > 51)) return p;
        }
        return new Vector3(-48, 0, 38);
    }

    private void Tree(Vector3 position, float scale)
    {
        var root = new Node3D { Name = "Tree", Position = position, Scale = new Vector3(scale, scale, scale) };
        AddChild(root);
        Geo.StaticBox(root, "Trunk", new Vector3(0, 1.1f, 0), new Vector3(0.62f, 2.2f, 0.62f), Palette.WoodDark);
        Geo.Box(root, new Vector3(0, 2.65f, 0), new Vector3(2.5f, 1.7f, 2.3f), Palette.GrassDark);
        Geo.Box(root, new Vector3(-0.9f, 2.2f, 0.4f), new Vector3(1.5f, 1.2f, 1.4f), Palette.Grass);
        Geo.Box(root, new Vector3(0.9f, 3.2f, -0.2f), new Vector3(1.6f, 1.1f, 1.5f), Color.FromHtml("55B85D"));
        Geo.Box(root, new Vector3(0.1f, 3.8f, 0.2f), new Vector3(1.2f, 0.9f, 1.2f), Palette.Grass);
    }

    private void Fir(Vector3 position, float scale)
    {
        var root = new Node3D { Name = "Fir", Position = position, Scale = new Vector3(scale, scale, scale) };
        AddChild(root);
        Geo.StaticBox(root, "Trunk", new Vector3(0, 0.9f, 0), new Vector3(0.45f, 1.8f, 0.45f), Palette.WoodDark);
        Geo.Cone(root, new Vector3(0, 2.6f, 0), 1.7f, 2.4f, Palette.GrassDark, 7);
        Geo.Cone(root, new Vector3(0, 3.8f, 0), 1.3f, 2.1f, Palette.GrassDark, 7);
        Geo.Cone(root, new Vector3(0, 4.9f, 0), 0.85f, 1.8f, Palette.Grass, 7);
    }

    private void Bush(Vector3 position, float scale)
    {
        var root = new Node3D { Name = "Bush", Position = position, Scale = new Vector3(scale, scale, scale) };
        AddChild(root);
        Geo.Box(root, new Vector3(0, 0.35f, 0), new Vector3(1.1f, 0.7f, 1.0f), Palette.GrassDark);
        Geo.Box(root, new Vector3(0.4f, 0.6f, 0.1f), new Vector3(0.7f, 0.6f, 0.7f), Palette.Grass);
    }

    private void Flower(Vector3 position, Color color)
    {
        var root = new Node3D { Name = "Flower", Position = position };
        AddChild(root);
        Geo.Cylinder(root, new Vector3(0, 0.24f, 0), 0.025f, 0.025f, 0.48f, Palette.GrassDark, 4);
        Geo.Sphere(root, new Vector3(0, 0.53f, 0), 0.14f, color, 6, 3);
    }

    private static void AddFlowerPlanter(Node parent, Vector3 pos)
    {
        Geo.Box(parent, pos + new Vector3(0, 0.25f, 0), new Vector3(0.8f, 0.5f, 0.6f), Palette.Wood);
        Geo.Sphere(parent, pos + new Vector3(-0.18f, 0.6f, 0), 0.18f, Palette.Pink, 6, 3);
        Geo.Sphere(parent, pos + new Vector3(0.18f, 0.55f, 0.08f), 0.15f, Palette.Red, 6, 3);
        Geo.Cylinder(parent, pos + new Vector3(0, 0.4f, 0), 0.02f, 0.02f, 0.4f, Palette.GrassDark, 4);
    }

    private void Rock(Vector3 position, float scale)
    {
        var root = new Node3D { Name = "Rock", Position = position, Scale = new Vector3(scale, scale * 0.75f, scale) };
        AddChild(root);
        Geo.Sphere(root, Vector3.Zero, 0.75f, Palette.Stone, 6, 3);
        Geo.Sphere(root, new Vector3(0.35f, 0.25f, 0.1f), 0.45f, Palette.StoneDark, 5, 2);
    }

    private void Crate(Vector3 pos)
    {
        Geo.Box(this, pos + new Vector3(0, 0.5f, 0), new Vector3(1.0f, 1.0f, 1.0f), Palette.Wood);
        Geo.Box(this, pos + new Vector3(0, 0.5f, 0.51f), new Vector3(1.02f, 0.12f, 0.03f), Palette.WoodDark);
        Geo.Box(this, pos + new Vector3(0, 0.5f, 0.51f), new Vector3(0.12f, 1.02f, 0.03f), Palette.WoodDark);
    }

    private void Flag(Vector3 pos, Color color)
    {
        Geo.Box(this, new Vector3(pos.X, 1.5f, pos.Z), new Vector3(0.09f, 3f, 0.09f), Palette.WoodDark);
        Geo.Box(this, new Vector3(pos.X + 0.4f, 2.8f, pos.Z), new Vector3(0.8f, 0.55f, 0.06f), color, null, true);
    }

    private static void Flag(Node parent, Vector3 pos, Color color)
    {
        Geo.Box(parent, new Vector3(pos.X, 1.5f, pos.Z), new Vector3(0.09f, 3f, 0.09f), Palette.WoodDark);
        Geo.Box(parent, new Vector3(pos.X + 0.4f, 2.8f, pos.Z), new Vector3(0.8f, 0.55f, 0.06f), color, null, true);
    }

    private void BuildDecorations()
    {
        AddLamp(new Vector3(-6f, 0, 2.3f));
        AddLamp(new Vector3(11.8f, 0, 2.3f));
        AddLamp(new Vector3(-24f, 0, 2.3f));
        AddLamp(new Vector3(19f, 0, 2.3f));
        AddLamp(new Vector3(-36f, 0, 12f));
        AddLamp(new Vector3(53f, 0, 5f));

        // Benches and community details.
        Bench(new Vector3(13.5f, 0, 24f), 0f);
        Bench(new Vector3(-45f, 0, -4f), 90f);
        Bench(new Vector3(-9f, 0, 4.5f), 180f);
        Bench(new Vector3(57f, 0, -8f), 0f);
        // Picnic / market signage.
        Geo.SignBoard(this, new Vector3(-1.3f, 1.0f, -14.0f), 180f, "TO VILLAGE", Palette.Blue, 3.0f, 0.6f, "waypoint", 28);

        // Van parking pad and readable sign.
        Geo.Box(this, new Vector3(VanSpawn.X, 0.025f, VanSpawn.Z - 0.4f), new Vector3(4.4f, 0.03f, 6.2f),
            Color.FromHtml("8C877B"), null, false, "VanParkingPad");
        for (int i = 0; i < 3; i++)
        {
            Geo.Box(this, new Vector3(VanSpawn.X - 1.6f + i * 1.6f, 0.045f, VanSpawn.Z - 1.8f),
                new Vector3(0.18f, 0.02f, 2.2f), Palette.RoadMark);
        }
        Geo.Box(this, new Vector3(ParkingSign.X, 0.6f, ParkingSign.Z), new Vector3(0.18f, 1.2f, 0.18f), Palette.WoodDark);
        Geo.SignBoard(this, new Vector3(ParkingSign.X, 1.7f, ParkingSign.Z), 90f, "VAN PARKING", Palette.Yellow, 4.2f, 0.8f, "van", 40);
        Geo.Box(this, new Vector3(ParkingSign.X + 0.6f, 0.35f, ParkingSign.Z), new Vector3(0.1f, 0.7f, 0.1f), Palette.WoodDark);
        // A small paddock fence near the meadow.
        FenceLine(new Vector3(51.5f, 0, -18f), new Vector3(88f, 0, -18f), Palette.White);
        FenceLine(new Vector3(88f, 0, -18f), new Vector3(88f, 0, -8f), Palette.White);

        // Celebration decorations, hidden until all jobs are done.
        CelebrationDecor = new Node3D { Name = "CelebrationDecor", Visible = false };
        AddChild(CelebrationDecor);
        Color[] bunting = { Palette.Yellow, Palette.Red, Palette.Blue, Palette.Lavender, Palette.Orange };
        for (int i = 0; i < 14; i++)
        {
            float x = -21f + i * 1.1f;
            float y = 4.8f + Mathf.Sin(i * 0.7f) * 0.35f;
            Geo.Box(CelebrationDecor, new Vector3(x, y, 5.8f), new Vector3(0.7f, 0.45f, 0.05f),
                bunting[i % bunting.Length], new Vector3(0, 0, 25), true, "Bunting");
        }
        for (int i = 0; i < 14; i++)
        {
            float x = -21f + i * 1.1f;
            float y = 4.8f + Mathf.Sin(i * 0.7f) * 0.35f;
            Geo.Box(CelebrationDecor, new Vector3(x, y, 22.2f), new Vector3(0.7f, 0.45f, 0.05f),
                bunting[(i + 2) % bunting.Length], new Vector3(0, 0, -25), true, "Bunting");
        }
        Geo.SignBoard(CelebrationDecor, new Vector3(-6.5f, 4.0f, 6.0f), 180f, "VILLAGE FIXED!", Palette.Yellow, 8f, 1.5f, "star", 58);
    }

    private void AddLamp(Vector3 pos)
    {
        Geo.Cylinder(this, pos + new Vector3(0, 1.8f, 0), 0.09f, 0.12f, 3.6f, Palette.Navy, 6);
        Geo.Box(this, pos + new Vector3(0, 3.55f, 0), new Vector3(0.55f, 0.25f, 0.55f), Palette.Navy);
        Geo.Sphere(this, pos + new Vector3(0, 3.38f, 0), 0.26f, new Color("FFE6A3"), 8, 4, null, "LampGlow");
        var lamp = new OmniLight3D
        {
            Position = pos + new Vector3(0, 3.3f, 0),
            LightColor = new Color("FFE1A8"),
            LightEnergy = 0.45f,
            OmniRange = 5.5f,
            ShadowEnabled = false,
        };
        AddChild(lamp);
    }

    private void Bench(Vector3 pos, float yawDegrees)
    {
        var root = new Node3D { Name = "Bench", Position = pos, RotationDegrees = new Vector3(0, yawDegrees, 0) };
        AddChild(root);
        Geo.Box(root, new Vector3(0, 0.5f, 0), new Vector3(2.0f, 0.16f, 0.65f), Palette.Wood);
        Geo.Box(root, new Vector3(0, 0.86f, -0.32f), new Vector3(2.0f, 0.5f, 0.12f), Palette.Wood);
        Geo.Box(root, new Vector3(-0.75f, 0.24f, 0), new Vector3(0.14f, 0.48f, 0.5f), Palette.StoneDark);
        Geo.Box(root, new Vector3(0.75f, 0.24f, 0), new Vector3(0.14f, 0.48f, 0.5f), Palette.StoneDark);
    }

    private void FenceLine(Vector3 from, Vector3 to, Color color)
    {
        Vector3 delta = to - from;
        int count = Mathf.CeilToInt(delta.Length() / 2f);
        for (int i = 0; i <= count; i++)
        {
            Vector3 p = from + delta * (i / (float)Math.Max(1, count));
            Geo.Box(this, new Vector3(p.X, 0.65f, p.Z), new Vector3(0.13f, 1.3f, 0.13f), color);
        }
        float yaw = Mathf.RadToDeg(Mathf.Atan2(delta.X, delta.Z));
        Vector3 mid = (from + to) * 0.5f;
        Geo.Box(this, new Vector3(mid.X, 0.9f, mid.Z), new Vector3(0.09f, 0.12f, delta.Length()),
            color, new Vector3(0, yaw, 0));
        Geo.Box(this, new Vector3(mid.X, 0.55f, mid.Z), new Vector3(0.09f, 0.1f, delta.Length()),
            color, new Vector3(0, yaw, 0));
    }

    private void BuildClouds()
    {
        Color cloud = new Color(1f, 1f, 1f, 0.92f);
        for (int i = 0; i < 9; i++)
        {
            float x = -55 + i * 18f;
            float z = -48 + (i % 4) * 25f;
            float y = 22 + (i % 3) * 4f;
            var root = new Node3D { Name = "Cloud", Position = new Vector3(x, y, z) };
            AddChild(root);
            Geo.Sphere(root, Vector3.Zero, 3.4f, cloud, 7, 4, new Vector3(1.6f, 0.65f, 1f));
            Geo.Sphere(root, new Vector3(3.2f, -0.3f, 0.5f), 2.3f, cloud, 7, 4, new Vector3(1.4f, 0.6f, 1f));
            Geo.Sphere(root, new Vector3(-3.1f, -0.4f, 0.2f), 2.1f, cloud, 7, 4, new Vector3(1.4f, 0.6f, 1f));
        }
    }

    private void AddKeepClear(Vector3 center, Vector2 halfExtents)
    {
        _keepClear.Add((new Vector2(center.X, center.Z), halfExtents));
    }
}
