using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Runtime bridge for the authored CityDistrict asset pack plus the small
/// authored connector/route/job scenes. It performs no procedural city
/// generation: every building, route, prop and marker is instantiated from
/// scenes/city, and every city job point comes from a named scene marker.
///
/// The adapter owns the tiered cell visibility, collision gating, HLOD swap and
/// bounded visual actor pool. Logical time advances independently in
/// <see cref="Simulation"/> even while the whole district is hidden.
/// </summary>
public partial class CityWorldAdapter : Node3D
{
    public const string DistrictPath = "res://scenes/city/CityDistrict.tscn";
    public const string JobsPath = "res://scenes/city/CityJobs.tscn";
    public const string LifePath = "res://scenes/city/CityLife.tscn";
    public const string ApproachPath = "res://scenes/city/TownCityApproach.tscn";
    public const string HlodFolder = "res://scenes/city/assets/hlod/";

    public Node3D DistrictRoot { get; private set; }
    public Node3D District { get; private set; }
    public Node3D Jobs { get; private set; }
    public Node3D Life { get; private set; }
    public Node3D Approach { get; private set; }
    public CityActorPool ActorPool { get; private set; }
    public CitySimulation Simulation { get; private set; }
    public int Seed => Simulation?.Seed ?? 0;
    public double LogicalSeconds => Simulation?.LogicalSeconds ?? 0;

    public int BuildingCount { get; private set; }
    public int HlodCount { get; private set; }
    public int DetailedVisible { get; private set; }
    public int HlodVisible { get; private set; }
    public int CellCount => _cells.Select(cell => cell.Index).Distinct().Count();
    public int CellBuildingCount => _cells.Count;
    public bool DistrictVisible => DistrictRoot?.Visible == true;
    public bool ApproachVisible => Approach?.Visible == true;
    public int ActiveVisualActors => ActorPool?.VisualVehicleCount + ActorPool?.VisualPedestrianCount ?? 0;
    public int NearVisualActors => ActorPool?.NearVehicleCount + ActorPool?.NearPedestrianCount ?? 0;

    public Vector3 BoardInteraction => _boardMarker?.GlobalPosition ?? throw new InvalidOperationException("City job board marker missing.");
    public Vector3 BoardApproach => Anchor("CityJobBoardAnchor") + new Vector3(0, 0.1f, -1.4f);
    public Vector3 DistrictCentre => new(CityLayout.OriginX, 0f, CityLayout.OriginZ);

    private readonly Dictionary<string, Marker3D> _anchors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Marker3D> _jobMarkers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Node3D> _jobProps = new(StringComparer.Ordinal);
    private readonly List<CityCell> _cells = new();
    private readonly List<StaticBody3D> _districtBodies = new();
    private readonly List<CollisionShape3D> _districtShapes = new();
    private readonly List<StaticBody3D> _groundBodies = new();
    private bool _groundCollisionActive;
    public const float GroundCollisionRadius = 185f;
    private Marker3D _boardMarker;
    private Node3D _boardRoot;
    private float _lodTimer;
    private float _cameraAltitude;
    private float _focusSpeed;
    private int _lastVisibleCells = -1;
    private bool _districtVisibleState;
    private bool _approachCollisionActive;

    public override void _Ready()
    {
        Name = "CityWorld";
        ProcessMode = ProcessModeEnum.Pausable;
    }

    public void Build()
    {
        var districtPacked = ResourceLoader.Load<PackedScene>(DistrictPath)
                             ?? throw new InvalidOperationException($"Missing authored city {DistrictPath}.");
        District = districtPacked.Instantiate<Node3D>()
                   ?? throw new InvalidOperationException($"Could not instantiate {DistrictPath}.");
        District.Name = "CityDistrict";
        var districtRoot = new Node3D
        {
            Name = "CityDistrictRoot",
            Position = new Vector3(CityLayout.OriginX, CityLayout.OriginY, CityLayout.OriginZ),
        };
        DistrictRoot = districtRoot;
        AddChild(DistrictRoot);
        DistrictRoot.AddChild(District);

        var jobsPacked = ResourceLoader.Load<PackedScene>(JobsPath)
                         ?? throw new InvalidOperationException($"Missing authored city jobs {JobsPath}.");
        Jobs = jobsPacked.Instantiate<Node3D>()
               ?? throw new InvalidOperationException($"Could not instantiate {JobsPath}.");
        Jobs.Name = "CityJobs";
        DistrictRoot.AddChild(Jobs);

        var lifePacked = ResourceLoader.Load<PackedScene>(LifePath)
                         ?? throw new InvalidOperationException($"Missing authored city life {LifePath}.");
        Life = lifePacked.Instantiate<Node3D>()
               ?? throw new InvalidOperationException($"Could not instantiate {LifePath}.");
        Life.Name = "CityLife";
        DistrictRoot.AddChild(Life);

        var approachPacked = ResourceLoader.Load<PackedScene>(ApproachPath)
                             ?? throw new InvalidOperationException($"Missing authored approach {ApproachPath}.");
        Approach = approachPacked.Instantiate<Node3D>()
                   ?? throw new InvalidOperationException($"Could not instantiate {ApproachPath}.");
        Approach.Name = "TownCityApproach";
        // The connector is authored in the same local space as CityDistrict.
        // Parent it to the origin-shifted district root so its measured world
        // join is 49 m..188 m, not a negative local coordinate.
        DistrictRoot.AddChild(Approach);

        CacheAnchors();
        CacheJobs();
        BuildCellsAndHlod();
        CacheCollision();
        BuildSimulation();
        BuildActorPool();
        // Force one real transition so every authored collider starts disabled
        // while the district is hidden in town.
        _districtVisibleState = true;
        _approachCollisionActive = true;
        ApplyDistrictVisible(false);
        Approach.Visible = false;
        SetApproachCollision(false);
        ApplyVisibilityGeometryRanges();

        GD.Print($"[CITY SCENE] {DistrictPath}: {BuildingCount} buildings, {HlodCount} HLOD proxies, " +
                 $"{CellCount} cells, {_jobMarkers.Count} job markers, " +
                 $"{Simulation.VehicleRoutes.Count} vehicle routes, " +
                 $"{Simulation.PedestrianRoutes.Count} pedestrian routes.");
    }

    public void RestoreSimulation(int seed, double logicalSeconds)
    {
        if (Simulation == null) return;
        if (Simulation.Seed != seed || seed == 0)
        {
            // A different deterministic seed is a content change; preserve the
            // clock but rebuild the compact schedule from the authored routes.
            BuildSimulation(seed);
            if (ActorPool != null)
            {
                ActorPool.Initialize(Simulation, Simulation.VehicleRoutes,
                    Simulation.PedestrianRoutes, () => _focusPlayer, () => _focusVan,
                    () => _focusDriving);
            }
        }
        Simulation.Restore(logicalSeconds);
    }

    public Vector3 Anchor(string id)
    {
        if (_anchors.TryGetValue(id, out var marker)) return marker.GlobalPosition;
        throw new InvalidOperationException($"The city scene is missing gameplay anchor '{id}'.");
    }

    public Vector3 JobPoint(string id)
    {
        if (_jobMarkers.TryGetValue(id, out var marker)) return marker.GlobalPosition;
        throw new InvalidOperationException($"The city jobs scene is missing marker '{id}'.");
    }

    public Node3D JobProp(string id) =>
        _jobProps.TryGetValue(id, out var node) ? node : null;

    /// <summary>Call once per unpaused gameplay frame.</summary>
    public void UpdateCity(double delta, Vector3 focusWorld, float planarSpeed,
        float cameraAltitude, bool visualsAllowed)
    {
        if (Simulation == null) return;
        Simulation.Advance(delta);
        _focusSpeed = Mathf.Max(0f, planarSpeed);
        _cameraAltitude = Mathf.Max(0f, cameraAltitude);

        bool districtVisible = visualsAllowed && CityLayout.DistanceToCityCentre(
            focusWorld.X, focusWorld.Z) < 335f;
        bool approachVisible = visualsAllowed && focusWorld.X > 28f;
        ApplyDistrictVisible(districtVisible);
        if (Approach != null) Approach.Visible = approachVisible;
        SetApproachCollision(approachVisible);
        if (districtVisible) UpdateGroundCollision(focusWorld);

        if (!districtVisible)
        {
            ActorPool?.UpdatePool((float)delta, focusWorld, _focusSpeed, false, _cameraAltitude);
            return;
        }

        _lodTimer -= (float)delta;
        if (_lodTimer <= 0f)
        {
            _lodTimer = 0.22f;
            UpdateCellTiers(focusWorld);
        }
        ActorPool?.UpdatePool((float)delta, focusWorld, _focusSpeed, true, _cameraAltitude);
    }

    public string StatsLine() =>
        $"visible={DistrictVisible} approach={ApproachVisible} cells={CellCount} " +
        $"detail={DetailedVisible} hlod={HlodVisible} actors={ActiveVisualActors} " +
        $"nearActors={NearVisualActors} " +
        $"midActors={(ActorPool?.MidVehicleCount ?? 0) + (ActorPool?.MidPedestrianCount ?? 0)} " +
        $"sim={Simulation?.LogicalSeconds ?? 0:0.0}s";

    public int ActiveDistrictCollisionBodies =>
        _districtBodies.Count(b => b.CollisionLayer != 0);

    private void CacheAnchors()
    {
        foreach (var marker in District.GetChildren().OfType<Marker3D>())
        {
            string id = marker.GetMeta("gameplay_id", "").AsString();
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!_anchors.TryAdd(id, marker))
                throw new InvalidOperationException($"Duplicate city anchor '{id}'.");
        }
        foreach (string id in new[]
                 {
                     "CityArrival", "CityJobBoardAnchor", "CityDepot", "CityHospital",
                     "CityLibrary", "CityMarket", "CityPark", "CityClockSquare",
                     "CityGarage", "CityBridgeApproach", "CityReturnRoad",
                 })
        {
            if (!_anchors.ContainsKey(id))
                throw new InvalidOperationException($"City anchor '{id}' is missing.");
        }

        _boardRoot = District.GetNodeOrNull<Node3D>("CityJobBoard")
                     ?? throw new InvalidOperationException("CityDistrict is missing CityJobBoard.");
        var interaction = _boardRoot.GetNodeOrNull<Marker3D>("InteractionMarker")
                          ?? throw new InvalidOperationException("CityJobBoard is missing its InteractionMarker.");
        _boardMarker = interaction;
    }

    private void CacheJobs()
    {
        foreach (var marker in Jobs.GetChildren().OfType<Marker3D>())
        {
            string id = marker.GetMeta("city_job_id", "").AsString();
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!_jobMarkers.TryAdd(id, marker))
                throw new InvalidOperationException($"Duplicate city job marker '{id}'.");
        }
        foreach (Node node in Descendants(Jobs))
        {
            string id = node.GetMeta("city_prop_id", "").AsString();
            if (!string.IsNullOrWhiteSpace(id)) _jobProps[id] = node as Node3D;
        }
        _jobProps["C01_DepotParcels"] = Jobs.GetNodeOrNull<Node3D>("C01DepotParcels");
        _jobProps["C03_Cabinet1"] = Jobs.GetNodeOrNull<Node3D>("C03Cabinet1");
        _jobProps["C03_Cabinet2"] = Jobs.GetNodeOrNull<Node3D>("C03Cabinet2");
        _jobProps["C03_Cabinet3"] = Jobs.GetNodeOrNull<Node3D>("C03Cabinet3");

        foreach (string id in new[]
                 {
                     "C01_Depot", "C01_Stop1", "C01_Stop2", "C01_Stop3",
                     "C02_Crate_0", "C02_Crate_1", "C02_Crate_2",
                     "C02_Crate_3", "C02_Crate_4", "C02_Crate_5",
                     "C02_Stall", "C03_Cabinet1", "C03_Cabinet2",
                     "C03_Cabinet3", "C03_Engineer",
                 })
        {
            if (!_jobMarkers.ContainsKey(id))
                throw new InvalidOperationException($"City jobs marker '{id}' is missing.");
        }
    }

    private void BuildCellsAndHlod()
    {
        var buildingArchetypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (Node child in District.GetChildren())
        {
            if (child is not Node3D building ||
                !building.Name.ToString().StartsWith("Building_", StringComparison.Ordinal))
                continue;
            BuildingCount++;
            string name = building.Name.ToString();
            string[] parts = name.Split('_', 3);
            if (parts.Length < 3)
                throw new InvalidOperationException($"City building '{name}' has no archetype.");
            string archetype = parts[2];
            buildingArchetypes.Add(archetype);
            var door = building.GetNodeOrNull<Marker3D>("DoorMarker")
                       ?? throw new InvalidOperationException($"City building '{name}' has no DoorMarker.");
            if (string.IsNullOrWhiteSpace(door.GetMeta("scene_binding_id", "").AsString()))
                throw new InvalidOperationException($"City building '{name}' has no door binding id.");

            string hlodPath = HlodFolder + archetype + "HLOD.tscn";
            var hlodPacked = ResourceLoader.Load<PackedScene>(hlodPath);
            if (hlodPacked == null)
                throw new InvalidOperationException($"Missing city HLOD proxy '{hlodPath}'.");
            var hlod = hlodPacked.Instantiate<Node3D>()
                       ?? throw new InvalidOperationException($"Could not instantiate '{hlodPath}'.");
            hlod.Name = "HLOD_" + name;
            hlod.Position = building.Position;
            hlod.Rotation = building.Rotation;
            hlod.Scale = building.Scale;
            hlod.Visible = false;
            DistrictRoot.AddChild(hlod);
            HlodCount++;

            int cell = CityLayout.CellIndex(building.Position.X, building.Position.Z);
            var cityCell = new CityCell
            {
                Index = cell,
                Building = building,
                Hlod = hlod,
            };
            foreach (var body in Descendants(building).OfType<StaticBody3D>())
                cityCell.CollisionBodies.Add(body);
            _cells.Add(cityCell);
        }
        if (BuildingCount != 36)
            throw new InvalidOperationException($"Expected 36 authored city buildings, found {BuildingCount}.");
        if (HlodCount != 36)
            throw new InvalidOperationException($"Expected 36 instantiated HLOD proxies, found {HlodCount}.");
        if (buildingArchetypes.Count != 12)
            throw new InvalidOperationException(
                $"Expected 12 city building archetypes, found {buildingArchetypes.Count}.");
    }

    private void CacheCollision()
    {
        var ground = District.GetNodeOrNull<StaticBody3D>("CityGround");
        if (ground == null)
            throw new InvalidOperationException("CityDistrict is missing its CityGround collision body.");
        _groundBodies.Add(ground);

        foreach (var body in Descendants(District).OfType<StaticBody3D>())
        {
            _districtBodies.Add(body);
            foreach (Node child in body.GetChildren())
                if (child is CollisionShape3D shape)
                    _districtShapes.Add(shape);
        }
        foreach (var body in Descendants(Approach).OfType<StaticBody3D>())
        {
            _approachBodies.Add(body);
        }
    }

    private readonly List<StaticBody3D> _approachBodies = new();

    private void BuildSimulation(int seedOverride = 0)
    {
        var vehicleRoutes = new List<CityRoutePoint[]>();
        var pedestrianRoutes = new List<CityRoutePoint[]>();
        foreach (Node child in Life.GetChildren())
        {
            if (child is not Node3D routeNode) continue;
            string kind = routeNode.GetMeta("route_kind", "").AsString();
            if (kind is not ("vehicle" or "pedestrian")) continue;
            var points = new List<CityRoutePoint>();
            foreach (var marker in routeNode.GetChildren().OfType<Marker3D>())
                points.Add(new CityRoutePoint(marker.Position.X, marker.Position.Z));
            if (points.Count < 4)
                throw new InvalidOperationException(
                    $"City route '{routeNode.Name}' needs at least four authored waypoints.");
            if (kind == "vehicle") vehicleRoutes.Add(points.ToArray());
            else pedestrianRoutes.Add(points.ToArray());
        }
        if (vehicleRoutes.Count == 0 || pedestrianRoutes.Count == 0)
            throw new InvalidOperationException("CityLife must author both vehicle and pedestrian routes.");

        int seed = seedOverride != 0
            ? seedOverride
            : Simulation?.Seed ?? 20261111;
        Simulation = new CitySimulation(vehicleRoutes, pedestrianRoutes, seed);
        if (ActorPool != null) ActorPool.ResetAll();
    }

    private void BuildActorPool()
    {
        ActorPool = new CityActorPool();
        DistrictRoot.AddChild(ActorPool);
        ActorPool.Initialize(Simulation, Simulation.VehicleRoutes, Simulation.PedestrianRoutes,
            () => _focusPlayer, () => _focusVan, () => _focusDriving);
    }

    private Vector3 _focusPlayer;
    private Vector3 _focusVan;
    private bool _focusDriving;

    /// <summary>Set before UpdateCity so lightweight actors can avoid the player/van.</summary>
    public void SetFocusActors(Vector3 player, Vector3 van, bool driving)
    {
        _focusPlayer = player;
        _focusVan = van;
        _focusDriving = driving;
    }

    private void ApplyDistrictVisible(bool visible)
    {
        if (DistrictRoot == null) return;
        if (_districtVisibleState == visible)
        {
            if (visible && _lastVisibleCells <= 0) _lodTimer = 0f;
            return;
        }
        _districtVisibleState = visible;
        DistrictRoot.Visible = visible;
        if (!visible)
        {
            _groundCollisionActive = false;
            foreach (var body in _districtBodies)
                body.CollisionLayer = 0u;
            DetailedVisible = 0;
            HlodVisible = 0;
            _lastVisibleCells = 0;
            foreach (var cell in _cells)
            {
                cell.Tier = 2;
                if (cell.Building != null) cell.Building.Visible = false;
                if (cell.Hlod != null) cell.Hlod.Visible = false;
                foreach (var body in cell.CollisionBodies)
                    body.CollisionLayer = 0u;
            }
        }
        else
        {
            // Visibility and collision are separate: distant HLOD/streaming may
            // keep the district visible without enabling streets under the town.
            _lodTimer = 0f;
        }
    }

    private void UpdateGroundCollision(Vector3 focusWorld)
    {
        bool active = CityLayout.DistanceToCityCentre(focusWorld.X, focusWorld.Z)
                      <= GroundCollisionRadius;
        if (active == _groundCollisionActive) return;
        _groundCollisionActive = active;
        foreach (var body in _groundBodies)
            body.CollisionLayer = active ? 1u : 0u;
    }

    private void SetApproachCollision(bool active)
    {
        if (_approachCollisionActive == active) return;
        _approachCollisionActive = active;
        foreach (var body in _approachBodies)
            body.CollisionLayer = active ? 1u : 0u;
    }

    private void UpdateCellTiers(Vector3 focusWorld)
    {
        float near = 100f + Mathf.Clamp(_focusSpeed * 2.2f, 0f, 24f);
        float mid = 214f + Mathf.Clamp(_focusSpeed * 4.0f, 0f, 40f);
        if (_cameraAltitude > 10f) near *= 0.9f;
        if (_cameraAltitude > 22f) near *= 0.78f;
        if (_cameraAltitude > 38f) mid *= 0.82f;

        var cellCentres = new Dictionary<int, float>();
        foreach (var cell in _cells)
        {
            var centre = CityLayout.CellCentres[cell.Index];
            float dx = CityLayout.OriginX + centre.X - focusWorld.X;
            float dz = CityLayout.OriginZ + centre.Z - focusWorld.Z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (!cellCentres.TryGetValue(cell.Index, out float existing) || distance < existing)
                cellCentres[cell.Index] = distance;
        }

        // Render budget: never show more than five fully detailed cells. The
        // nearest five win; corners become HLOD first.
        var nearest = cellCentres.OrderBy(pair => pair.Value).Take(5)
            .Select(pair => pair.Key).ToHashSet();
        DetailedVisible = 0;
        HlodVisible = 0;
        foreach (var cell in _cells)
        {
            float distance = cellCentres[cell.Index];
            int tier = CityLayout.TierFor(distance, near, mid, cell.Tier);
            if (tier == 0 && !nearest.Contains(cell.Index)) tier = 1;
            cell.Tier = tier;
            cell.Building.Visible = tier == 0;
            cell.Hlod.Visible = tier == 1;
            if (tier == 0) DetailedVisible++;
            else if (tier == 1) HlodVisible++;
            foreach (var body in cell.CollisionBodies)
                body.CollisionLayer = tier == 0 ? 1u : 0u;
        }
        _lastVisibleCells = DetailedVisible + HlodVisible;
    }

    private void ApplyVisibilityGeometryRanges()
    {
        foreach (var cell in _cells)
        {
            foreach (var mesh in Descendants(cell.Building).OfType<GeometryInstance3D>())
            {
                mesh.VisibilityRangeEnd = 260f;
                mesh.VisibilityRangeEndMargin = 12f;
            }
            foreach (var mesh in Descendants(cell.Hlod).OfType<GeometryInstance3D>())
            {
                mesh.VisibilityRangeEnd = 420f;
                mesh.VisibilityRangeEndMargin = 18f;
            }
        }
    }

    private sealed class CityCell
    {
        public int Index;
        public Node3D Building;
        public Node3D Hlod;
        public int Tier = 2;
        public readonly List<StaticBody3D> CollisionBodies = new();
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
}
