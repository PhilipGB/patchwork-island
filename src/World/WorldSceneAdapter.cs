using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Runtime bridge between the exact baked v1.2.1 town scene and the existing
/// gameplay composition root. The adapter deliberately keeps the public
/// WorldBuilder surface used by Main and the build systems, while all static
/// meshes, collisions and landmark geometry come from Town_Baked.tscn.
///
/// Every required scene node and gameplay anchor is validated at load time.
/// Missing or moved geometry fails loudly; the adapter never invents a zero
/// position or silently falls back to procedural geometry.
/// </summary>
public partial class WorldSceneAdapter : WorldBuilder
{
    public const string TownScenePath = "res://scenes/world/baked/Town_Baked.tscn";
    public const float PositionTolerance = 0.01f;

    public Node3D Town { get; private set; }
    public int AnchorCount => _anchors.Count;

    private readonly Dictionary<string, Marker3D> _anchors = new(StringComparer.Ordinal);

    public Vector3 GetAnchorPosition(string id) => RequireAnchor(id).Position;

    public override void _ExitTree()
    {
        // Release the managed wrappers for the instanced scene before the C#
        // script bridge shuts down. Without this, abrupt process exits can
        // finalize hundreds of scene resources after the bridge is gone.
        Town = null;
        _anchors.Clear();
        BridgeBarrier = null;
        CelebrationDecor = null;
        RaceStartFlag = null;
        GoldCourseFlag = null;
        PlaygroundFixSign = null;
        DeliveryBoard = null;
        MarketStock = null;
        PipCarBroken = null;
        PipCarFixed = null;
        RallyPodium = null;
        FestivalDecor = null;
        RaceGates4Root = null;
        RaceGates6Root = null;
        RallyRoad = null;
        SignSocketRoots.Clear();
        BakeryDisplaySlots.Clear();
        LibraryShelfSlots.Clear();
        FestivalTableVisuals.Clear();
        FestivalLanternVisuals.Clear();
        BrokenSwingLabels.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        base._ExitTree();
    }

    public override void Build()
    {
        Name = "Village";

        var packed = ResourceLoader.Load<PackedScene>(TownScenePath);
        if (packed == null)
            throw new InvalidOperationException($"Missing baked town scene {TownScenePath}.");

        Town = packed.Instantiate<Node3D>();
        if (Town == null)
            throw new InvalidOperationException($"Could not instantiate {TownScenePath}.");
        AddChild(Town);
        Town.Position = Vector3.Zero;
        Town.Rotation = Vector3.Zero;

        CacheGameplayAnchors();
        ValidateGameplayAnchors();
        ResolveStaticSceneHandles();
        PopulateGameplayPositions();
        ValidateBuildingInstances();
        ValidateSwingFrames();
        ValidateRaceGates();
        ValidateNpcAndTrafficRoutes();

        GD.Print($"[WORLD SCENE] {TownScenePath}: {_anchors.Count} anchors, " +
                 $"{Town.GetChildCount()} top-level roots, " +
                 $"{RallySampleCount} rally samples, {RallyLength:0.00} m.");
    }

    private void CacheGameplayAnchors()
    {
        var root = RequireNode<Node3D>("GameplayAnchors");
        foreach (Node child in root.GetChildren())
        {
            if (child is not Marker3D marker)
                throw new InvalidOperationException(
                    $"GameplayAnchors/{child.Name} is not a Marker3D.");

            string id = marker.GetMeta("gameplay_id", "").AsString();
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException(
                    $"Gameplay anchor '{marker.Name}' has no gameplay_id metadata.");
            if (!string.Equals(id, marker.Name.ToString(), StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Gameplay anchor '{marker.Name}' declares mismatched id '{id}'.");
            if (!_anchors.TryAdd(id, marker))
                throw new InvalidOperationException($"Duplicate gameplay anchor '{id}'.");
        }

        if (_anchors.Count == 0)
            throw new InvalidOperationException("GameplayAnchors is empty.");
    }

    private void ValidateGameplayAnchors()
    {
        var expected = new List<(string Id, Vector3 Position)>
        {
            ("PlayerSpawn", PlayerSpawn),
            ("VanSpawn", VanSpawn),
            ("JobBoard", JobBoard),
            ("PlayerHouseDoor", PlayerHouseDoor),
            ("TownHallDoor", TownHallDoor),
            ("RedHouseDoor", RedHouseDoor),
            ("ShopDoor", ShopDoor),
            ("ClothesShopDoor", ClothesShopDoor),
            ("WorkshopDoor", WorkshopDoor),
            ("AppleCrate", AppleCrate),
            ("PostCounter", PostCounter),
            ("BridgeSite", BridgeSite),
            ("BridgeCenter", BridgeCenter),
            ("PlaygroundCenter", PlaygroundCenter),
            ("ParkCenter", ParkCenter),
            ("VillageGreenSign", VillageGreenSign),
            ("GroveCenter", GroveCenter),
            ("StonePatchCenter", StonePatchCenter),
            ("ParkingSign", ParkingSign),
            ("FurnitureStation", FurnitureStation),
            ("BakeryStall", BakeryStall),
            ("LibraryShelf", LibraryShelf),
            ("RepairCarPosition", RepairCarPosition),
            ("RepairStation", new Vector3(35.2f, 0f, -5.4f)),
            ("DeliveryBoard", new Vector3(-2.5f, 0f, 4.2f)),
            ("MarketStock", new Vector3(-2f, 0f, 4.8f)),
            ("RallyPodium", new Vector3(60.5f, 0f, -13.5f)),
        };

        for (int i = 0; i < 6; i++)
        {
            float hx = (i % 3 - 1) * 2.8f;
            float hz = -0.7f + (i / 3) * 3.3f;
            expected.Add(($"HousePad_{i}", PlayerHouseCenter + new Vector3(hx, 0.08f, hz)));
            float px = ParkCenter.X + (i % 3 - 1) * 3.7f;
            float pz = ParkCenter.Z - 1.5f + (i / 3) * 3.2f;
            expected.Add(($"ParkPad_{i}", new Vector3(px, 0.09f, pz)));
            float gx = 26f + (i % 3) * 3f;
            float gz = 20.8f + (i / 3) * 2.6f;
            expected.Add(($"GardenPad_{i}", new Vector3(gx, 0.09f, gz)));
        }

        for (int i = 0; i < 5; i++)
        {
            expected.Add(($"WoodPickup_{i}", new Vector3(
                GroveCenter.X - 6f + i * 2.2f, 0f,
                GroveCenter.Z - 2.3f + (i % 2) * 1.0f)));
        }
        for (int i = 0; i < 3; i++)
        {
            expected.Add(($"StonePickup_{i}", new Vector3(
                StonePatchCenter.X - 3.4f + i * 3.2f, 0f,
                StonePatchCenter.Z - 0.8f + (i % 2) * 2.0f)));
            expected.Add(($"ApplePickup_{i}", new Vector3(
                AppleCrate.X - 0.7f + i * 0.7f, 1.15f, AppleCrate.Z + 0.1f)));
            expected.Add(($"ParcelPickup_{i}", new Vector3(
                PostCounter.X - 0.8f + i * 0.8f, 1.3f, PostCounter.Z - 0.1f)));
            expected.Add(($"SignSocket_{i}", new Vector3(
                i == 0 ? 16.2f : i == 1 ? 22.0f : -2.5f, 0f,
                i == 0 ? 9.6f : i == 1 ? -4.2f : 10.5f)));
        }

        var books = new[]
        {
            new Vector3(9.4f, 0.15f, 3.75f),
            new Vector3(-4.2f, 0.15f, 7f),
            new Vector3(-30.8f, 0.15f, -8.8f),
            new Vector3(13.6f, 0.15f, 13.1f),
        };
        for (int i = 0; i < books.Length; i++)
            expected.Add(($"BookPickup_{i}", books[i]));

        var tables = new[]
        {
            new Vector3(12f, 0.09f, 22.4f),
            new Vector3(18f, 0.09f, 22.4f),
        };
        for (int i = 0; i < tables.Length; i++)
            expected.Add(($"FestivalTable_{i}", tables[i]));

        var lanterns = new[]
        {
            new Vector3(9f, 0.09f, 22.4f),
            new Vector3(21f, 0.09f, 22.4f),
            new Vector3(15f, 0.09f, 24.2f),
        };
        for (int i = 0; i < lanterns.Length; i++)
            expected.Add(($"FestivalLantern_{i}", lanterns[i]));

        for (int i = 0; i < 4; i++)
        {
            expected.Add(($"RepairPickup_{i}", new Vector3(37.4f, 0.15f, -9f - i * 3f)));
        }

        if (expected.Count != _anchors.Count)
        {
            throw new InvalidOperationException(
                $"GameplayAnchors has {_anchors.Count} ids but the v1.2.1 parity " +
                $"set requires {expected.Count}.");
        }

        foreach (var (id, position) in expected)
        {
            Marker3D marker = RequireAnchor(id);
            if (marker.Position.DistanceTo(position) > PositionTolerance)
            {
                throw new InvalidOperationException(
                    $"Gameplay anchor '{id}' moved: scene {marker.Position} != expected {position}.");
            }
        }
    }

    private void ResolveStaticSceneHandles()
    {
        BridgeBarrier = RequireNode<Node3D>("BridgeBarrier");
        CelebrationDecor = RequireNode<Node3D>("CelebrationDecor");
        RaceStartFlag = RequireNode<Node3D>("RaceStartFlag");
        GoldCourseFlag = RequireNode<Node3D>("GoldCourseFlag");
        PlaygroundFixSign = RequireNode<Node3D>("Sign_FIX ME");
        DeliveryBoard = RequireNode<Node3D>("DeliveryBoard");
        MarketStock = RequireNode<Node3D>("MarketStock");
        PipCarBroken = RequireNode<Node3D>("PipCarBroken");
        PipCarFixed = RequireNode<Node3D>("PipCarFixed");
        RallyPodium = RequireNode<Node3D>("RallyPodium");
        FestivalDecor = RequireNode<Node3D>("FestivalDecor");
        RaceGates4Root = RequireNode<Node3D>("RaceGates4Root");
        RaceGates6Root = RequireNode<Node3D>("RaceGates6Root");
        RallyRoad = RequireNode<Node3D>("RallyRoad");

        if (RallyRoad.GetNodeOrNull<StaticBody3D>("RallyRoadCollision") == null)
            throw new InvalidOperationException("Baked rally road has no collision body.");

        FinishedBridge = null; // The built bridge remains owned by BridgeBuilder.
        RallySampleCount = RallyCourse.SampleClosed(28).Count;
        RallyLength = RallyCourse.Length();
        var start = RallyCourse.Sample(0f);
        RaceStart = new Vector3(start.X, 0f, start.Z);
        RaceGates = ToGateArray(RallyCourse.FourGates);
        M19RaceGates = ToGateArray(RallyCourse.SixGates);

        DeliveryBoard.Position = RequireAnchor("DeliveryBoard").Position;
        MarketStock.Position = RequireAnchor("MarketStock").Position;
        RallyPodium.Position = RequireAnchor("RallyPodium").Position;
        PipCarBroken.Position = RepairCarPosition;
        PipCarFixed.Position = RepairCarPosition;

        var brokenLabels = Descendants(Town)
            .OfType<Label3D>()
            .Where(label => label.Text == "BROKEN")
            .OrderBy(label => label.GlobalPosition.X)
            .ToList();
        if (brokenLabels.Count != SwingFrames.Length)
        {
            throw new InvalidOperationException(
                $"Expected {SwingFrames.Length} BROKEN swing labels, found {brokenLabels.Count}.");
        }
        BrokenSwingLabels.AddRange(brokenLabels);
    }

    private void PopulateGameplayPositions()
    {
        for (int i = 0; i < 5; i++)
            WoodSpawnPoints.Add(AnchorPosition($"WoodPickup_{i}"));
        for (int i = 0; i < 3; i++)
            StoneSpawnPoints.Add(AnchorPosition($"StonePickup_{i}"));
        for (int i = 0; i < 3; i++)
            AppleSpawnPoints.Add(AnchorPosition($"ApplePickup_{i}"));
        for (int i = 0; i < 3; i++)
            ParcelSpawnPoints.Add(AnchorPosition($"ParcelPickup_{i}"));
        for (int i = 0; i < 6; i++)
        {
            HousePads.Add(AnchorPosition($"HousePad_{i}"));
            ParkPads.Add(AnchorPosition($"ParkPad_{i}"));
            GardenPads.Add(AnchorPosition($"GardenPad_{i}"));
        }
        for (int i = 0; i < 3; i++)
        {
            SignSocketPositions.Add(AnchorPosition($"SignSocket_{i}"));
            string socketId = ChapterTwoRules.SignSockets[i].SocketId;
            SignSocketRoots.Add(RequireNode<Node3D>($"SignSocket_{socketId}"));
        }

        string[] books = ChapterTwoRules.BookIds;
        for (int i = 0; i < books.Length; i++)
            BookSpawnPoints.Add((books[i], AnchorPosition($"BookPickup_{i}")));

        for (int i = 0; i < ChapterTwoRules.BakeryOrders.Count; i++)
        {
            string customer = ChapterTwoRules.BakeryOrders[i].CustomerId;
            BakeryDisplaySlots.Add(RequireNode<Node3D>(
                $"BakeryStall/BakeryOrder_{customer}"));
        }
        for (int i = 0; i < books.Length; i++)
        {
            LibraryShelfSlots.Add(RequireNode<Node3D>(
                $"LibraryShelf/ShelfBook_{books[i]}"));
        }

        for (int i = 0; i < 4; i++)
            RepairPartSpawnPoints.Add(AnchorPosition($"RepairPickup_{i}"));
        for (int i = 0; i < 2; i++)
            FestivalTablePads.Add(AnchorPosition($"FestivalTable_{i}"));
        for (int i = 0; i < 3; i++)
            FestivalLanternPads.Add(AnchorPosition($"FestivalLantern_{i}"));
        for (int i = 0; i < 2; i++)
            FestivalTableVisuals.Add(RequireNode<Node3D>($"FestivalTable_{i}"));
        for (int i = 0; i < 3; i++)
            FestivalLanternVisuals.Add(RequireNode<Node3D>($"FestivalLantern_{i}"));
    }

    private void ValidateBuildingInstances()
    {
        var specs = new (string Name, Vector3 Center, float Width, float Depth, float WallHeight)[]
        {
            ("PlayerHouse", PlayerHouseCenter, 13f, 10f, 3.4f),
            ("TownHall", TownHallCenter, 15f, 10f, 4f),
            ("RedHouse", RedHouseCenter, 11f, 9f, 3.4f),
            ("Market", ShopCenter, 12f, 9f, 3.4f),
            ("ClothesShop", ClothesShopCenter, 9f, 7f, 3.2f),
            ("Workshop", WorkshopCenter, 13f, 10f, 3.7f),
        };

        foreach (var spec in specs)
        {
            Node3D root = RequireNode<Node3D>(spec.Name);
            if (root.Position.DistanceTo(spec.Center) > PositionTolerance)
            {
                throw new InvalidOperationException(
                    $"Building '{spec.Name}' centre {root.Position} != {spec.Center}.");
            }
            if (root.Rotation.Length() > 0.01f)
            {
                throw new InvalidOperationException(
                    $"Building '{spec.Name}' yaw changed from the source layout.");
            }

            BoxMesh west = RequireWallMesh(root, "WallWest", spec.Name);
            if (Mathf.Abs(west.Size.Y - spec.WallHeight) > PositionTolerance ||
                Mathf.Abs(west.Size.Z - spec.Depth) > PositionTolerance)
            {
                throw new InvalidOperationException(
                    $"Building '{spec.Name}' wall mismatch: {west.Size} vs " +
                    $"{spec.Width}x{spec.Depth} h{spec.WallHeight}.");
            }

            BoxMesh back = RequireWallMesh(root, "WallBack", spec.Name);
            if (Mathf.Abs(back.Size.X - spec.Width) > PositionTolerance)
            {
                throw new InvalidOperationException(
                    $"Building '{spec.Name}' width mismatch: {back.Size.X} != {spec.Width}.");
            }
        }
    }

    private static BoxMesh RequireWallMesh(Node3D building, string wallName, string buildingName)
    {
        var wall = building.GetNodeOrNull<Node3D>(wallName)
            ?? throw new InvalidOperationException(
                $"Building '{buildingName}' is missing {wallName}.");
        var mesh = wall.GetChildren().OfType<MeshInstance3D>()
            .Select(instance => instance.Mesh as BoxMesh)
            .FirstOrDefault(box => box != null)
            ?? throw new InvalidOperationException(
                $"Building '{buildingName}/{wallName}' has no BoxMesh.");
        return mesh;
    }

    private void ValidateSwingFrames()
    {
        var placement = Town.GetChildren().OfType<Node3D>()
            .Where(node => node.Position.DistanceTo(SwingFrames[0]) <= PositionTolerance ||
                           node.Position.DistanceTo(SwingFrames[1]) <= PositionTolerance)
            .Select(node => node.Position)
            .ToList();
        foreach (Vector3 frame in SwingFrames)
        {
            int count = placement.Count(position =>
                position.DistanceTo(frame) <= PositionTolerance);
            if (count == 0)
                throw new InvalidOperationException($"No swing frame geometry at {frame}.");
        }
    }

    private void ValidateRaceGates()
    {
        ValidateGateNodes(RaceGates4Root, RaceGates, "RaceGate");
        ValidateGateNodes(RaceGates6Root, M19RaceGates, "M19Gate");
    }

    private static void ValidateGateNodes(Node3D root, (Vector3 Position, float Yaw)[] gates,
        string prefix)
    {
        for (int i = 0; i < gates.Length; i++)
        {
            var gate = root.GetNodeOrNull<Node3D>($"{prefix}{i + 1}")
                ?? throw new InvalidOperationException(
                    $"Missing race gate {prefix}{i + 1} in {root.Name}.");
            if (gate.Position.DistanceTo(gates[i].Position) > PositionTolerance)
            {
                throw new InvalidOperationException(
                    $"Race gate {prefix}{i + 1} moved to {gate.Position}, " +
                    $"expected {gates[i].Position}.");
            }
            float expectedYaw = Mathf.DegToRad(gates[i].Yaw);
            if (Mathf.Abs(Mathf.AngleDifference(gate.Rotation.Y, expectedYaw)) > 0.01f)
            {
                throw new InvalidOperationException(
                    $"Race gate {prefix}{i + 1} yaw {gate.Rotation.Y} != {expectedYaw}.");
            }
        }
    }

    private void ValidateNpcAndTrafficRoutes()
    {
        var npcRoot = RequireNode<Node3D>("NpcPatrolMarkers");
        foreach (var definition in NpcCatalog.All)
        {
            Node3D route = npcRoot.GetNodeOrNull<Node3D>(definition.Id)
                ?? throw new InvalidOperationException(
                    $"Missing NPC patrol route '{definition.Id}'.");
            if (route.GetMeta("route_id", "").AsString() != definition.Id)
                throw new InvalidOperationException(
                    $"NPC route '{definition.Id}' has wrong route_id metadata.");

            var points = route.GetChildren().OfType<Marker3D>()
                .OrderBy(marker => marker.Name.ToString(), StringComparer.Ordinal)
                .ToList();
            if (points.Count != definition.PatrolPoints.Count)
            {
                throw new InvalidOperationException(
                    $"NPC route '{definition.Id}' has {points.Count} markers, " +
                    $"expected {definition.PatrolPoints.Count}.");
            }
            for (int i = 0; i < points.Count; i++)
            {
                NpcPoint expected = definition.PatrolPoints[i];
                if (points[i].Position.DistanceTo(
                        new Vector3(expected.X, 0f, expected.Z)) > PositionTolerance)
                {
                    throw new InvalidOperationException(
                        $"NPC route '{definition.Id}' waypoint {i} moved to {points[i].Position}.");
                }
            }
        }

        var trafficRoot = RequireNode<Node3D>("TrafficRouteMarkers");
        foreach (var routeDefinition in TrafficCatalog.Routes.Where(route => !route.RallyOnly))
        {
            Node3D route = trafficRoot.GetNodeOrNull<Node3D>(routeDefinition.Id)
                ?? throw new InvalidOperationException(
                    $"Missing traffic route '{routeDefinition.Id}'.");
            if (route.GetMeta("route_id", "").AsString() != routeDefinition.Id)
                throw new InvalidOperationException(
                    $"Traffic route '{routeDefinition.Id}' has wrong route_id metadata.");

            var points = route.GetChildren().OfType<Marker3D>()
                .OrderBy(marker => marker.Name.ToString(), StringComparer.Ordinal)
                .ToList();
            if (points.Count != routeDefinition.Points.Count)
            {
                throw new InvalidOperationException(
                    $"Traffic route '{routeDefinition.Id}' has {points.Count} markers, " +
                    $"expected {routeDefinition.Points.Count}.");
            }
            for (int i = 0; i < points.Count; i++)
            {
                NpcPoint expected = routeDefinition.Points[i];
                if (points[i].Position.DistanceTo(
                        new Vector3(expected.X, 0f, expected.Z)) > PositionTolerance)
                {
                    throw new InvalidOperationException(
                        $"Traffic route '{routeDefinition.Id}' waypoint {i} moved to " +
                        $"{points[i].Position}.");
                }
            }
        }
    }

    private Marker3D RequireAnchor(string id)
    {
        if (_anchors.TryGetValue(id, out Marker3D marker))
            return marker;
        throw new InvalidOperationException($"Missing gameplay anchor '{id}'.");
    }

    private Vector3 AnchorPosition(string id) => RequireAnchor(id).Position;

    private T RequireNode<T>(string path) where T : Node
    {
        var node = Town.GetNodeOrNull<T>(path);
        if (node == null)
            throw new InvalidOperationException($"Baked town is missing required node '{path}'.");
        return node;
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static (Vector3 Position, float Yaw)[] ToGateArray(
        IReadOnlyList<RallyCourse.Gate> gates) =>
        gates.Select(gate => (new Vector3(gate.X, 0f, gate.Z), gate.YawDegrees)).ToArray();
}
