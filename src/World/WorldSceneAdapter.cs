using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Runtime bridge for the canonical, editor-authored town scene.
///
/// Static scenery is instantiated from <see cref="TownScenePath"/>; gameplay
/// positions are read from scene-owned markers, building DoorMarkers and gate
/// nodes. The adapter keeps the WorldBuilder-facing API used by Main and the
/// build systems but never falls back to procedural geometry or hard-coded
/// coordinates for placement.
/// </summary>
public partial class WorldSceneAdapter : WorldBuilder
{
    public const string TownScenePath = "res://scenes/world/Town.tscn";

    public Node3D Town { get; private set; }
    public int AnchorCount => _anchors.Count;

    private readonly Dictionary<string, Marker3D> _anchors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<NpcPoint>> _npcRoutes =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<NpcPoint>> _trafficRoutes =
        new(StringComparer.Ordinal);

    public Vector3 GetAnchorPosition(string id) => RequireAnchor(id).GlobalPosition;

    public IReadOnlyList<NpcPoint> GetNpcPatrol(string id) =>
        _npcRoutes.TryGetValue(id, out var points) ? points : null;

    public IReadOnlyList<NpcPoint> GetTrafficPoints(string id) =>
        _trafficRoutes.TryGetValue(id, out var points) ? points : null;

    public override void _ExitTree()
    {
        // Release managed wrappers before the C# script bridge shuts down.
        Town = null;
        _anchors.Clear();
        _npcRoutes.Clear();
        _trafficRoutes.Clear();
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
            throw new InvalidOperationException($"Missing canonical town scene {TownScenePath}.");

        Town = packed.Instantiate<Node3D>();
        if (Town == null)
            throw new InvalidOperationException($"Could not instantiate {TownScenePath}.");
        AddChild(Town);

        CacheGameplayAnchors();
        ResolveStaticSceneHandles();
        BindSceneAuthoritativePositions();
        BindRaceGates();
        PopulateGameplayPositions();
        ValidateBuildingsAndDoors();
        ValidateNpcAndTrafficRoutes();
        ValidateCollisionContract();

        GD.Print($"[WORLD SCENE] {TownScenePath}: {_anchors.Count} anchors, " +
                 $"{Town.GetChildCount()} top-level roots, " +
                 $"{_npcRoutes.Count} NPC routes, {_trafficRoutes.Count} free-roam routes, " +
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

    private void ResolveStaticSceneHandles()
    {
        BridgeBarrier = RequireNode<Node3D>("BridgeBarrier");
        CelebrationDecor = RequireNode<Node3D>("CelebrationDecor");
        RaceStartFlag = RequireNode<Node3D>("RaceStartFlag");
        GoldCourseFlag = RequireNode<Node3D>("GoldCourseFlag");
        PlaygroundFixSign = RequireNode<Node3D>("PlaygroundFixSign");
        DeliveryBoard = RequireNode<Node3D>("DeliveryBoard");
        MarketStock = RequireNode<Node3D>("MarketStock");
        PipCarBroken = RequireNode<Node3D>("PipCarBroken");
        PipCarFixed = RequireNode<Node3D>("PipCarFixed");
        RallyPodium = RequireNode<Node3D>("RallyPodium");
        FestivalDecor = RequireNode<Node3D>("FestivalDecor");
        RaceGates4Root = RequireNode<Node3D>("RaceGates4Root");
        RaceGates6Root = RequireNode<Node3D>("RaceGates6Root");
        RallyRoad = RequireNode<Node3D>("RallyRoad");

        FinishedBridge = null; // Built bridge state remains owned by BridgeBuilder.

        RallySampleCount = RallyCourse.SampleClosed(28).Count;
        RallyLength = RallyCourse.Length();

        var labels = new List<Label3D>();
        foreach (string frame in new[] { "SwingFrame1", "SwingFrame2" })
        {
            foreach (Label3D label in Descendants(RequireNode<Node3D>(frame)).OfType<Label3D>())
                if (label.Text == "BROKEN")
                    labels.Add(label);
        }
        if (labels.Count < 2)
            throw new InvalidOperationException("Both swing frames need scene-owned BROKEN labels.");
        BrokenSwingLabels.AddRange(labels);
    }

    private void BindSceneAuthoritativePositions()
    {
        PlayerSpawn = AnchorPosition("PlayerSpawn");
        VanSpawn = AnchorPosition("VanSpawn");
        JobBoard = AnchorPosition("JobBoard");
        AppleCrate = AnchorPosition("AppleCrate");
        PostCounter = AnchorPosition("PostCounter");
        BridgeCenter = AnchorPosition("BridgeCenter");
        PlaygroundCenter = AnchorPosition("PlaygroundCenter");
        ParkCenter = AnchorPosition("ParkCenter");
        VillageGreenSign = AnchorPosition("VillageGreenSign");
        GroveCenter = AnchorPosition("GroveCenter");
        StonePatchCenter = AnchorPosition("StonePatchCenter");
        ParkingSign = AnchorPosition("ParkingSign");
        FurnitureStation = AnchorPosition("FurnitureStation");

        BridgeSite = RequireNode<Node3D>("BridgeSite/WorksiteAnchor").GlobalPosition;

        PlayerHouseCenter = BindBuildingDoor("PlayerHouse", "PlayerHouseDoor",
            out Vector3 playerHouseDoor);
        PlayerHouseDoor = playerHouseDoor;
        TownHallCenter = BindBuildingDoor("TownHall", "TownHallDoor",
            out Vector3 townHallDoor);
        TownHallDoor = townHallDoor;
        RedHouseCenter = BindBuildingDoor("RedHouse", "RedHouseDoor",
            out Vector3 redHouseDoor);
        RedHouseDoor = redHouseDoor;
        ShopCenter = BindBuildingDoor("Market", "ShopDoor", out Vector3 shopDoor);
        ShopDoor = shopDoor;
        ClothesShopCenter = BindBuildingDoor("ClothesShop", "ClothesShopDoor",
            out Vector3 clothesShopDoor);
        ClothesShopDoor = clothesShopDoor;
        WorkshopCenter = BindBuildingDoor("Workshop", "WorkshopDoor",
            out Vector3 workshopDoor);
        WorkshopDoor = workshopDoor;

        BakeryStall = RequireNode<Node3D>("BakeryStall").GlobalPosition;
        LibraryShelf = RequireNode<Node3D>("LibraryShelf").GlobalPosition;
        RepairCarPosition = PipCarBroken.GlobalPosition;

        SwingFrames = new[]
        {
            RequireNode<Node3D>("SwingFrame1").GlobalPosition,
            RequireNode<Node3D>("SwingFrame2").GlobalPosition,
        };
        RaceStart = RequireNode<Marker3D>("RaceStart").GlobalPosition;
    }

    private Vector3 BindBuildingDoor(string buildingName, string bindingId, out Vector3 door)
    {
        Node3D building = RequireNode<Node3D>(buildingName);
        var marker = building.GetNodeOrNull<Marker3D>("DoorMarker")
            ?? throw new InvalidOperationException(
                $"Building '{buildingName}' is missing its DoorMarker.");
        string sceneBinding = marker.GetMeta("scene_binding_id", "").AsString();
        if (sceneBinding != bindingId)
            throw new InvalidOperationException(
                $"Building '{buildingName}' DoorMarker id '{sceneBinding}' != '{bindingId}'.");

        door = marker.GlobalPosition;
        if (!IsFinite(door))
            throw new InvalidOperationException(
                $"Building '{buildingName}' DoorMarker has a non-finite transform.");
        if (door.DistanceTo(building.GlobalPosition) > 20f)
            throw new InvalidOperationException(
                $"Building '{buildingName}' DoorMarker is unreasonably far from its building.");
        return building.GlobalPosition;
    }

    private void BindRaceGates()
    {
        RaceGates = ReadGates(RaceGates4Root, "RaceGate", 4);
        M19RaceGates = ReadGates(RaceGates6Root, "M19Gate", 6);
    }

    private (Vector3 Position, float Yaw)[] ReadGates(Node3D root, string prefix, int count)
    {
        var gates = new (Vector3, float)[count];
        for (int i = 0; i < count; i++)
        {
            string gateName = $"{prefix}{i + 1}";
            Node3D gate = root.GetNodeOrNull<Node3D>(gateName)
                ?? throw new InvalidOperationException($"Missing race gate '{gateName}'.");
            var trigger = gate.GetNodeOrNull<Marker3D>("GateTrigger")
                ?? throw new InvalidOperationException($"'{gateName}' has no GateTrigger.");
            var blocker = gate.GetNodeOrNull<StaticBody3D>("CameraOnlyBlocker")
                ?? throw new InvalidOperationException($"'{gateName}' has no camera blocker.");
            if (blocker.CollisionLayer != 16u || blocker.GetChildCount() == 0)
                throw new InvalidOperationException(
                    $"'{gateName}' camera blocker must be on layer 16 with collision shapes.");
            if (!Descendants(gate).OfType<MeshInstance3D>().Any())
                throw new InvalidOperationException($"'{gateName}' has no gate visuals.");

            Vector3 position = trigger.GlobalPosition;
            if (!IsFinite(position))
                throw new InvalidOperationException($"'{gateName}' has a non-finite trigger.");
            gates[i] = (position, gate.GlobalRotation.Y);
        }
        return gates;
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

        for (int i = 0; i < ChapterTwoRules.SignSockets.Count; i++)
        {
            string socketId = ChapterTwoRules.SignSockets[i].SocketId;
            Node3D socket = RequireNode<Node3D>($"SignSocket_{socketId}");
            SignSocketRoots.Add(socket);
            SignSocketPositions.Add(socket.GlobalPosition);
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
            LibraryShelfSlots.Add(RequireNode<Node3D>(
                $"LibraryShelf/ShelfBook_{books[i]}"));

        for (int i = 0; i < 4; i++)
            RepairPartSpawnPoints.Add(AnchorPosition($"RepairPickup_{i}"));
        for (int i = 0; i < 2; i++)
        {
            Node3D table = RequireNode<Node3D>($"FestivalTable_{i}");
            FestivalTablePads.Add(table.GlobalPosition);
            FestivalTableVisuals.Add(table);
        }
        for (int i = 0; i < 3; i++)
        {
            Node3D lantern = RequireNode<Node3D>($"FestivalLantern_{i}");
            FestivalLanternPads.Add(lantern.GlobalPosition);
            FestivalLanternVisuals.Add(lantern);
        }
    }

    private void ValidateBuildingsAndDoors()
    {
        var specs = new (string Name, string Binding)[]
        {
            ("PlayerHouse", "PlayerHouseDoor"),
            ("TownHall", "TownHallDoor"),
            ("RedHouse", "RedHouseDoor"),
            ("Market", "ShopDoor"),
            ("ClothesShop", "ClothesShopDoor"),
            ("Workshop", "WorkshopDoor"),
        };

        foreach (var spec in specs)
        {
            Node3D building = RequireNode<Node3D>(spec.Name);
            if (!Descendants(building).OfType<MeshInstance3D>().Any())
                throw new InvalidOperationException($"Building '{spec.Name}' has no mesh content.");
            bool hasFloorCollision = Descendants(building).OfType<StaticBody3D>()
                .Any(body => body.CollisionLayer == 1u && body.GetChildCount() > 0);
            if (!hasFloorCollision)
                throw new InvalidOperationException(
                    $"Building '{spec.Name}' has no layer-1 collision.");

            var door = building.GetNodeOrNull<Marker3D>("DoorMarker")
                ?? throw new InvalidOperationException(
                    $"Building '{spec.Name}' is missing its DoorMarker.");
            if (door.GetMeta("scene_binding_id", "").AsString() != spec.Binding)
                throw new InvalidOperationException(
                    $"Building '{spec.Name}' DoorMarker binding changed.");
            if (!IsFinite(door.GlobalPosition))
                throw new InvalidOperationException(
                    $"Building '{spec.Name}' DoorMarker is not finite.");
        }

        var cameraBlocker = RequireNode<StaticBody3D>("PlayerHouse/DoorCameraBlocker");
        if (cameraBlocker.CollisionLayer != 16u || cameraBlocker.GetChildCount() == 0)
            throw new InvalidOperationException(
                "PlayerHouse doorway camera blocker must remain on layer 16.");
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
            var points = ReadRoutePoints(route, 2, definition.Id);
            _npcRoutes[definition.Id] = points;
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
            var points = ReadRoutePoints(route, 4, routeDefinition.Id);
            for (int i = 0; i < points.Count; i++)
            {
                NpcPoint a = points[i];
                NpcPoint b = points[(i + 1) % points.Count];
                float gap = Mathf.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
                if (gap > 14f)
                    throw new InvalidOperationException(
                        $"Traffic route '{routeDefinition.Id}' has a long jump at waypoint {i}.");
            }
            _trafficRoutes[routeDefinition.Id] = points;
        }
    }

    private IReadOnlyList<NpcPoint> ReadRoutePoints(Node3D route, int minimum, string routeId)
    {
        var markers = route.GetChildren().OfType<Marker3D>()
            .OrderBy(marker => marker.Name.ToString(), StringComparer.Ordinal)
            .ToList();
        if (markers.Count < minimum)
            throw new InvalidOperationException(
                $"Route '{routeId}' needs at least {minimum} waypoints.");

        var points = new List<NpcPoint>(markers.Count);
        for (int i = 0; i < markers.Count; i++)
        {
            Vector3 position = markers[i].GlobalPosition;
            if (!IsFinite(position) || position.X < -54f || position.X > 94f ||
                position.Z < -54f || position.Z > 54f ||
                (position.X > 40.5f && position.X < 49.5f))
            {
                throw new InvalidOperationException(
                    $"Route '{routeId}' waypoint {i} is outside safe play space: {position}.");
            }
            points.Add(new NpcPoint(position.X, position.Z));
        }
        return points;
    }

    private void ValidateCollisionContract()
    {
        var townRoads = RequireNode<StaticBody3D>("TownRoads/TownRoadCollision");
        if (townRoads.CollisionLayer != 1u || townRoads.GetChildCount() < 11)
            throw new InvalidOperationException(
                "TownRoads needs a layer-1 collision body with all road slabs.");

        var rallyCollision = RequireNode<StaticBody3D>("RallyRoad/RallyRoadCollision");
        if (rallyCollision.CollisionLayer != 1u || rallyCollision.GetChildCount() == 0)
            throw new InvalidOperationException(
                "RallyRoad needs a layer-1 triangle collision body.");

        var terrain = RequireNode<StaticBody3D>("Terrain/WestIslandCollision");
        if (terrain.CollisionLayer != 1u)
            throw new InvalidOperationException("Terrain collision must remain on layer 1.");
    }

    private Marker3D RequireAnchor(string id)
    {
        if (_anchors.TryGetValue(id, out Marker3D marker))
            return marker;
        throw new InvalidOperationException($"Missing gameplay anchor '{id}'.");
    }

    private Vector3 AnchorPosition(string id) => RequireAnchor(id).GlobalPosition;

    private T RequireNode<T>(string path) where T : Node
    {
        var node = Town.GetNodeOrNull<T>(path);
        if (node == null)
            throw new InvalidOperationException($"Canonical town is missing required node '{path}'.");
        return node;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child))
                yield return descendant;
        }
    }
}
