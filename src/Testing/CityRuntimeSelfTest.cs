using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Godot runtime acceptance for the city expansion. It exercises the real
/// authored scene, the physical gate, the independent city job state machine,
/// a mid-drive save, LOD/actor activation and a return trip. Run:
///   godot --headless --path . -- --selftest-city
/// </summary>
public partial class CityRuntimeSelfTest : Node
{
    public Main Main { get; set; }

    private int _passed;
    private int _failed;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _ = Run();
    }

    private async Task Run()
    {
        try
        {
            Main.Probe.SuppressSaves();
            await Wait(0.7f);
            SceneContractChecks();
            await TownLogicalChecks();
            RoadworksDeliveryChecks();
            GateAndMissionsChecks();
            await CityActivationChecks();
            await CityTrafficCooperationChecks();
            await HighSpeedAndReturnChecks();
            await CityTraversalChecks();
            await CityOuterLoopCollisionChecks();
            SaveRoundTripChecks();
            LoadedOpenRoadCityChecks();
        }
        catch (Exception ex)
        {
            Fail("unexpected city runtime-test exception: " + ex);
        }

        GD.Print($"CITY RUNTIME RESULT: {_passed} passed, {_failed} failed");
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    private void SceneContractChecks()
    {
        var city = Main.Probe.City;
        var road = Main.Probe.Roadworks;
        Expect(city != null, "CityWorldAdapter is built");
        if (city == null) return;
        Expect(city.BuildingCount == 36, "36 authored city buildings instantiated");
        Expect(city.HlodCount == 36, "one authored HLOD proxy per city building");
        Expect(city.CellCount == 9, "the fixed district exposes nine block cells");
        Expect(city.Simulation != null, "compact logical city simulation exists");
        Expect(city.Simulation.VehicleRoutes.Count == 4, "four authored vehicle lanes");
        Expect(city.Simulation.PedestrianRoutes.Count == 11, "eleven authored pedestrian routes");
        Expect(city.BoardInteraction.DistanceTo(
            new Vector3(CityLayout.WorldX(0f), 0.29f, CityLayout.WorldZ(20f))) < 0.1f,
            "CITY JOBS interaction follows its authored marker off the road");
        Expect(road != null && road.BarrierVisible, "closed road starts with the authored barrier visible");
        Expect(road != null && road.ColliderEnabled, "closed road starts with physical collision");
        Expect(Main.Probe.CityMissions.GetStatus("C01") == CityMissionStatus.Available,
            "C01 is on the independent city catalogue");
        Expect(Main.Probe.CityMissions.GetStatus("C02") == CityMissionStatus.Available,
            "C02 is on the independent city catalogue");
        Expect(Main.Probe.CityMissions.GetStatus("C03") == CityMissionStatus.Available,
            "C03 is on the independent city catalogue");
    }

    private async Task TownLogicalChecks()
    {
        var city = Main.Probe.City;
        Expect(!city.DistrictVisible, "district is not rendered while the player is in town");
        Expect(city.ActiveVisualActors == 0, "no city actor Node3Ds exist while far away in town");
        double before = city.LogicalSeconds;
        await Wait(1.1f);
        Expect(city.LogicalSeconds - before > 0.8,
            "city logical time advances while the city scene is hidden");
        Expect(!city.DistrictVisible && city.ActiveVisualActors == 0,
            "hidden city remains free of actor Nodes after logical advance");
    }

    private void RoadworksDeliveryChecks()
    {
        var data = Main.Probe.Data;
        data.ActiveMission = RoadworksRules.MissionId;
        data.SetMissionCounter(RoadworksRules.MissionId, RoadworksRules.SuppliesDeliveredKey, 0);
        data.SetMissionCounter(RoadworksRules.MissionId, RoadworksRules.TaskDoneKey, 0);
        data.AddItem("safety_cone", RoadworksRules.ConeCount);
        data.AddItem("barrier_panel", RoadworksRules.PanelCount);
        data.AddItem("toolbox", RoadworksRules.ToolboxCount);
        Expect(RoadworksRules.HasAllSupplies(data),
            "roadworks delivery test starts with all six supplies carried");
        Main.Probe.RefreshMissionState();
        Expect(Main.Probe.DestinationLabel == "ROADWORKS DELIVERY",
            "all six carried supplies move the objective off the workshop");
        Expect(Main.Probe.Destination.DistanceTo(Main.Probe.Roadworks.InteractionPosition) < 0.1f,
            "the delivery objective points at the closed city gate");

        Main.Probe.InteractRoadworks();
        Expect(RoadworksRules.SuppliesDelivered(data),
            "the real worksite interaction delivers all six supplies");
        Expect(data.GetItem("safety_cone") == 0 &&
               data.GetItem("barrier_panel") == 0 &&
               data.GetItem("toolbox") == 0,
            "worksite delivery consumes the carried supplies exactly once");
        Expect(Main.Probe.ModalTitle == "Roadworks loading check",
            "worksite delivery opens the crew loading check");
        Main.Probe.CloseModal();

        data.ActiveMission = "";
        data.SetMissionCounter(RoadworksRules.MissionId, RoadworksRules.SuppliesDeliveredKey, 0);
        data.SetMissionCounter(RoadworksRules.MissionId, RoadworksRules.TaskDoneKey, 0);
    }

    private void GateAndMissionsChecks()
    {
        var cityMissions = Main.Probe.CityMissions;
        var startClosed = cityMissions.StartMission("C01");
        Expect(!startClosed.Success, "city jobs are gated by the closed road");
        Main.Probe.SetRoadState(true);
        Expect(!Main.Probe.Roadworks.BarrierVisible, "completed roadworks remove the barrier visual");
        Expect(!Main.Probe.Roadworks.ColliderEnabled, "completed roadworks disable the physical collider");
        Main.Probe.Roadworks.SetRoadState(true, false);
        Expect(Main.Probe.Roadworks.RoadOpen && !Main.Probe.Roadworks.ColliderEnabled,
            "road open state is idempotent");

        var c1 = cityMissions.StartMission("C01");
        Expect(c1.Success, "C01 starts from the independent city board");
        Expect(cityMissions.LoadCourierParcels().Success, "courier parcels load once");
        Expect(!cityMissions.DeliverCourierStop(2).Success, "courier rejects stop 2 before stop 1");
        Expect(cityMissions.DeliverCourierStop(1).Success, "courier accepts stop 1");
        Expect(cityMissions.DeliverCourierStop(2).Success, "courier accepts stop 2");
        Expect(cityMissions.DeliverCourierStop(3).Success, "courier accepts stop 3");
        int wallet = Main.Probe.Data.Coins;
        var c1Done = cityMissions.TurnInAtBoard();
        Expect(c1Done.Success, "courier turns in at the city board");
        Expect(Main.Probe.Data.Coins == wallet + 20, "courier pays its 20 coins once");
        Expect(!cityMissions.TurnInAtBoard().Success, "courier cannot be turned in twice");

        Interactable Crate(int index) => GetTree().GetNodesInGroup("interactable")
            .OfType<Interactable>().FirstOrDefault(n => n.Name == $"CityCrateStation_{index}");

        Expect(!Crate(0).IsAvailable(), "C02 crates take no focus with no active C02");
        Expect(cityMissions.StartMission("C02").Success, "C02 starts independently");
        Expect(Crate(0).IsAvailable(), "C02 crates take focus when C02 is active");
        Expect(cityMissions.CollectMarketCrate(0).Success, "market crate 0 collected");
        Expect(!Crate(0).IsAvailable(), "a consumed crate loses E focus immediately");
        Expect(Crate(1).IsAvailable(), "the next unconsumed crate keeps focus");
        Expect(cityMissions.StartMission("C03").Success, "switch away from C02");
        Expect(!Crate(1).IsAvailable(), "crates lose focus while another city job is active");
        Expect(cityMissions.StartMission("C02").Success, "switch back to C02");
        Expect(Crate(1).IsAvailable(), "crates regain focus with C02 restored");
        for (int i = 1; i < 6; i++)
            Expect(cityMissions.CollectMarketCrate(i).Success, $"market crate {i} collected");
        Expect(!cityMissions.AnswerMarketStock(5).Success, "market rejects 5");
        Expect(cityMissions.AnswerMarketStock(6).Success, "2 × 3 = 6 is accepted");
        wallet = Main.Probe.Data.Coins;
        Expect(cityMissions.TurnInAtBoard().Success, "market stock-up turns in");
        Expect(Main.Probe.Data.Coins == wallet + 24, "market stock-up pays 24 coins");
        Expect(!Crate(0).IsAvailable() && !Crate(5).IsAvailable(),
            "completed C02 keeps every crate out of the E scan");

        Expect(cityMissions.StartMission("C03").Success, "C03 starts independently");
        Expect(!cityMissions.RepairCabinet(1, "amber").Success, "cabinet 1 rejects amber");
        Expect(cityMissions.RepairCabinet(1, "red").Success, "cabinet 1 accepts red");
        Expect(!cityMissions.RepairCabinet(3, "green").Success, "cabinet 3 rejects out-of-order repair");
        Expect(cityMissions.RepairCabinet(2, "amber").Success, "cabinet 2 accepts amber");
        Expect(cityMissions.RepairCabinet(3, "green").Success, "cabinet 3 accepts green");
        wallet = Main.Probe.Data.Coins;
        var engineer = cityMissions.TalkToEngineer();
        Expect(engineer.Success && engineer.Completed, "engineer sign-off completes C03");
        Expect(Main.Probe.Data.Coins == wallet + 28, "signals job pays 28 coins once");
        Expect(Main.Probe.Data.CompletedCityMissions.Count == 3,
            "three city jobs are persisted as distinct completions");
    }

    private async Task CityActivationChecks()
    {
        var city = Main.Probe.City;
        Main.Probe.TeleportPlayer(new Vector3(CityLayout.WorldX(0f), 0.5f, CityLayout.WorldZ(28f)), 0f);
        await Wait(1.1f);
        Expect(city.DistrictVisible, "district activates near the city board");
        Expect(city.ActiveDistrictCollisionBodies > 0, "near city cells expose physical collision");
        Expect(city.ActorPool.VisualVehicleCount > 0, "near city spawns bounded vehicle visuals");
        Expect(city.ActorPool.VisualPedestrianCount > 0, "near city spawns bounded pedestrians");
        Expect(city.ActorPool.NearVehicleCount + city.ActorPool.NearPedestrianCount > 0,
            "very near actors enable animation/physics tier");
        Expect(city.ActorPool.VisualVehicleCount <= CityActorPool.MaxNearVehicles,
            "visible vehicle count stays within the authored budget");
        Expect(city.ActorPool.VisualPedestrianCount <= CityActorPool.MaxNearPedestrians,
            "visible pedestrian count stays within the authored budget");
        var names = city.ActorPool.GetChildren().Select(n => n.Name.ToString()).ToList();
        Expect(names.Count == names.Distinct().Count(), "pool contains no duplicate logical actors");
        Expect(city.DetailedVisible > 0, "near cells render detailed building meshes");
        Expect(city.HlodVisible > 0, "distant city cells render authored HLOD proxies");

        Main.Probe.TeleportPlayer(new Vector3(CityLayout.WorldX(0f), 0.5f, CityLayout.WorldZ(34f)), 0f);
        await Wait(0.5f);
        Input.ActionPress("move_forward");
        await Wait(2.0f);
        Input.ActionRelease("move_forward");
        Expect(Main.Probe.PlayerPosition.Z < CityLayout.WorldZ(29f),
            "player walks from the road onto the central plaza");
    }

    private static float RouteDelta(float from, float to, float length)
    {
        float delta = (to - from) % length;
        if (delta > length * 0.5f) delta -= length;
        else if (delta < -length * 0.5f) delta += length;
        return delta;
    }

    private async Task CityTrafficCooperationChecks()
    {
        var city = Main.Probe.City;
        var centre = new Vector3(CityLayout.WorldX(0f), 0.4f, CityLayout.WorldZ(0f));
        Vector3 forward = new(0f, 0f, -1f);
        Expect(CityVehicleVisual.ShouldYieldTo(centre, forward,
                centre + forward * 3f, false, Vector3.Zero),
            "city traffic yields to a player directly ahead");
        Expect(!CityVehicleVisual.ShouldYieldTo(centre, forward,
                centre + forward * 20f, false, Vector3.Zero),
            "city traffic does not yield to a distant player");

        var car = city.ActorPool.GetChildren().OfType<CityVehicleVisual>()
            .Where(v => v.Tier == CityActorTier.Near &&
                        v.GlobalPosition.DistanceTo(centre) < 90f)
            .OrderBy(v => v.GlobalPosition.DistanceTo(centre))
            .FirstOrDefault();
        Expect(car != null, "a near city car is available for the cooperation test");
        if (car == null) return;
        int id = car.AgentId;
        car.SetTestHold(true);
        await Wait(5.0f);
        car = city.ActorPool.GetChildren().OfType<CityVehicleVisual>()
            .FirstOrDefault(v => v.AgentId == id);
        float physicalLag = car == null ? 0f :
            RouteDelta(car.Progress, city.Simulation.Pose(id).RouteProgress, car.RouteLength);
        Expect(car != null && car.Yielding, "long-held city car is in its yield state");
        Expect(car != null &&
               Mathf.Abs(car.ReconciliationLag) <= CityActorPool.MaxVisualLagMeters + 0.1f,
            "long-held city car keeps a bounded reconciliation lag");
        Expect(car != null && Mathf.Abs(car.ReconciliationLag) > 0.1f,
            "long-held city car records its visual lag");
        Expect(physicalLag > CityActorPool.MaxVisualLagMeters + 1f,
            "a long hold leaves the physical car farther behind than the visual cap");
        Expect(car != null &&
               physicalLag - Mathf.Abs(car.ReconciliationLag) > 1f,
            "a long hold stores less lag than the true physical gap");
        car?.SetTestHold(false);

        // Stream the car out while the player is in town, then return.
        Main.Probe.TeleportPlayer(new Vector3(-38f, 0.4f, 9.5f), 0f);
        await Wait(1.0f);
        Expect(city.ActiveVisualActors == 0, "city actors stream out from town");

        Main.Probe.TeleportPlayer(new Vector3(CityLayout.WorldX(0f), 0.4f, CityLayout.WorldZ(28f)), 0f);
        await Wait(1.2f);
        var returned = city.ActorPool.GetChildren().OfType<CityVehicleVisual>()
            .FirstOrDefault(v => v.AgentId == id);
        Expect(returned != null, "the same city car returns after streaming");
        if (returned != null)
        {
            Expect(Mathf.Abs(returned.ReconciliationLag) <=
                   CityActorPool.MaxVisualLagMeters + 0.1f,
                "returned city car resumes with bounded reconciliation");
            returned.SetTestHold(true);
            await Wait(0.8f);
            Expect(returned.Yielding, "returned city car can yield again");
            returned.SetTestHold(false);
        }

        Main.Probe.TeleportPlayer(new Vector3(CityLayout.WorldX(0f), 0.4f, CityLayout.WorldZ(28f)), 0f);
        await Wait(0.4f);
    }

    private async Task HighSpeedAndReturnChecks()
    {
        var city = Main.Probe.City;
        // Physical gate test at maximum van speed.
        Main.Probe.SetRoadState(false);
        Main.Probe.TeleportVan(new Vector3(80f, 0.4f, 0f), -Mathf.Pi / 2f);
        Main.Probe.LaunchVan(11.5f);
        await Wait(1.3f);
        Expect(Main.Probe.VanPosition.X < 95.0f,
            "closed gate physically stops a full-speed van");
        Main.Probe.SetRoadState(true);
        Main.Probe.TeleportVan(new Vector3(80f, 0.4f, 0f), -Mathf.Pi / 2f);
        Main.Probe.LaunchVan(11.5f);
        await Wait(2.2f);
        Expect(Main.Probe.VanPosition.X > 97.0f,
            "open gate lets the van drive onto the approach at full speed");

        // The city entrance must be drivable through the west sidewalk gap.
        Main.Probe.TeleportVan(new Vector3(178f, 0.4f, 0f), -Mathf.Pi / 2f);
        Main.Probe.LaunchVan(11.5f);
        await Wait(3.0f);
        Expect(Main.Probe.VanPosition.X > 188.0f,
            "the van crosses the west sidewalk gap into the city road");

        Vector3 approach = new(118f, 0.4f, 0f);
        Main.Probe.ProbeCityLod(approach, 11.5f);
        Expect(city.DistrictVisible && city.ApproachVisible,
            "high-speed approach keeps the join and district active");
        Expect(city.DetailedVisible + city.HlodVisible > 0,
            "high-speed approach has a visible authored cell budget");
        Main.Probe.ProbeCityLod(new Vector3(CityLayout.WorldX(0f), 0.4f, CityLayout.WorldZ(0f)), 11.5f);
        Expect(city.DetailedVisible > 0 && city.HlodVisible >= 0,
            "high-speed centre keeps detailed geometry near the player");

        var cars = Main.Probe.City.ActorPool.GetChildren().OfType<CityVehicleVisual>()
            .Where(v => v.Tier == CityActorTier.Near).ToList();
        Expect(cars.Count >= 2, "at least two near city cars are active");
        var carStarts = cars.ToDictionary(v => v.AgentId, v => v.GlobalPosition);
        foreach (var car in cars)
        {
            float lx = car.GlobalPosition.X - CityLayout.OriginX;
            float lz = car.GlobalPosition.Z - CityLayout.OriginZ;
            bool onRoad = CityLayout.RoadAxes.Any(axis =>
                MathF.Abs(lx - axis) <= 13f || MathF.Abs(lz - axis) <= 13f);
            Expect(onRoad, $"city car {car.AgentId} stays on the road grid");
        }
        await Wait(2.5f);
        var carEnds = Main.Probe.City.ActorPool.GetChildren().OfType<CityVehicleVisual>()
            .ToDictionary(v => v.AgentId, v => v.GlobalPosition);
        int moved = carStarts.Count(pair =>
            carEnds.TryGetValue(pair.Key, out Vector3 end) &&
            end.DistanceTo(pair.Value) > 2f);
        Expect(moved >= 2,
            "at least two near city cars keep moving through intersections");

        Main.Probe.TeleportPlayer(new Vector3(0f, 0.4f, 0f), 0f);
        await Wait(0.9f);
        Expect(city.DistrictVisible, "distant city stays visible from the town centre");
        Expect(city.ActiveDistrictCollisionBodies == 0,
            "distant city keeps physical collision disabled at the town centre");

        Main.Probe.TeleportPlayer(new Vector3(-38f, 0.4f, 9.5f), 0f);
        await Wait(0.9f);
        Expect(!city.DistrictVisible, "returning to town hides the whole city district");
        Expect(city.ActiveVisualActors == 0, "returning to town destroys every visual city actor");
        Expect(city.ActiveDistrictCollisionBodies == 0, "returning to town disables offscreen city collision");

        Main.Probe.SetRoadState(false);
        Expect(Main.Probe.Roadworks.BarrierVisible && Main.Probe.Roadworks.ColliderEnabled,
            "a fresh/closed state restores the gate exactly");
        Main.Probe.SetRoadState(true);
    }

    private async Task CityTraversalChecks()
    {
        Main.Probe.SetRoadState(true);
        Main.Probe.TeleportVan(new Vector3(180f, 0.4f, 0f), -Mathf.Pi / 2f);
        Main.Probe.StartVanRoute(new[]
        {
            new Vector3(200f, 0.4f, 0f),
            new Vector3(200f, 0.4f, -40f),
            new Vector3(280f, 0.4f, -40f),
            new Vector3(280f, 0.4f, -120f),
            new Vector3(200f, 0.4f, -120f),
            new Vector3(200f, 0.4f, 0f),
            new Vector3(170f, 0.4f, 0f),
        });

        bool entered = false;
        bool reachedFirst = false;
        bool reachedCentre = false;
        bool returned = false;
        for (int i = 0; i < 170 && !returned; i++)
        {
            await Wait(0.5f);
            Vector3 van = Main.Probe.VanPosition;
            entered |= van.X > 188f;
            reachedFirst |= van.X > 195f && van.Z < -35f;
            reachedCentre |= van.X > 275f && van.Z < -35f;
            returned |= reachedCentre && van.X < 180f;
        }
        Main.Probe.StopVanRoute();
        GD.Print($"CITY_TRAVERSAL entered={entered} first={reachedFirst} " +
                 $"centre={reachedCentre} returned={returned} van={Main.Probe.VanPosition} " +
                 $"speed={Main.Probe.VanSpeed:0.00}");
        Main.Probe.TeleportPlayer(new Vector3(-38f, 0.4f, 9.5f), 0f);
        await Wait(0.4f);

        Expect(entered, "sustained driving enters the city road");
        Expect(reachedFirst, "sustained driving reaches the first city intersection");
        Expect(reachedCentre, "sustained driving reaches the central city block");
        Expect(returned, "sustained driving returns from the city to the approach");
    }

    private async Task CityOuterLoopCollisionChecks()
    {
        var city = Main.Probe.City;

        // The outer carriageways end at local ±132; their corners are ~187 m
        // from the centre, outside the old 185 m circle. Every carriageway
        // edge point must stay inside the authored square footprint.
        var edgePoints = new (float X, float Z)[]
        {
            (-132f, -132f), (132f, -132f), (132f, 132f), (-132f, 132f),
            (-120f, 0f), (120f, 0f), (0f, -120f), (0f, 120f),
        };
        foreach (var point in edgePoints)
            Expect(CityLayout.IsWithinGroundCollision(
                    CityLayout.WorldX(point.X), CityLayout.WorldZ(point.Z)),
                $"outer road point ({point.X:0},{point.Z:0}) keeps city ground collision active");

        var corner = new Vector3(CityLayout.WorldX(132f), 0.4f, CityLayout.WorldZ(132f));
        Main.Probe.ProbeCityLod(corner, 8f);
        Expect(city.GroundCollisionActive,
            "outer carriageway corner activates city ground collision");
        Expect(city.ActiveDistrictCollisionBodies > 0,
            "outer carriageway corner exposes its ground collision body");

        // Drive the full outer loop on the outer lane and assert that ground
        // collision stays on for every sample of the loop.
        Main.Probe.TeleportVan(new Vector3(CityLayout.WorldX(0f), 0.4f,
            CityLayout.WorldZ(-126f)), -Mathf.Pi / 2f);
        Main.Probe.StartVanRoute(new[]
        {
            new Vector3(CityLayout.WorldX(0f), 0.4f, CityLayout.WorldZ(-126f)),
            new Vector3(CityLayout.WorldX(126f), 0.4f, CityLayout.WorldZ(-126f)),
            new Vector3(CityLayout.WorldX(126f), 0.4f, CityLayout.WorldZ(126f)),
            new Vector3(CityLayout.WorldX(-126f), 0.4f, CityLayout.WorldZ(126f)),
            new Vector3(CityLayout.WorldX(-126f), 0.4f, CityLayout.WorldZ(-126f)),
            new Vector3(CityLayout.WorldX(0f), 0.4f, CityLayout.WorldZ(-126f)),
        });

        bool alwaysActive = true;
        float furthest = 0f;
        var visited = new bool[4];
        for (int i = 0; i < 460 && !visited.All(v => v); i++)
        {
            await Wait(0.4f);
            Vector3 van = Main.Probe.VanPosition;
            alwaysActive &= Main.Probe.CityGroundCollisionActive;
            furthest = Mathf.Max(furthest, CityLayout.DistanceToCityCentre(van.X, van.Z));
            float localX = van.X - CityLayout.OriginX;
            float localZ = van.Z - CityLayout.OriginZ;
            if (localX > 110f && localZ < -110f) visited[0] = true;
            if (localX > 110f && localZ > 110f) visited[1] = true;
            if (localX < -110f && localZ > 110f) visited[2] = true;
            if (localX < -110f && localZ < -110f) visited[3] = true;
        }
        Main.Probe.StopVanRoute();
        GD.Print($"CITY_OUTER_LOOP active={alwaysActive} furthest={furthest:0.0} " +
                 $"corners={visited.Count(v => v)}/4");
        Main.Probe.TeleportPlayer(new Vector3(-38f, 0.4f, 9.5f), 0f);
        await Wait(0.4f);

        Expect(alwaysActive, "city ground collision stays active around the outer loop");
        Expect(visited.Count(v => v) == 4, "the outer-loop drive visits all four outer corners");
        Expect(furthest > 170f, "the outer-loop drive reaches the outer carriageway");
    }

    private void SaveRoundTripChecks()
    {
        string path = ProjectSettings.GlobalizePath("user://selftest-city-mid-drive.json");
        try
        {
            var data = new GameData
            {
                ActiveCityMission = "C01",
                CityLogicalSeconds = 4321.5,
                CityVisited = true,
                Driving = true,
                Player = new PlayerSnapshot
                {
                    X = CityLayout.WorldX(0f),
                    Y = 0.4f,
                    Z = CityLayout.WorldZ(28f),
                    RotationY = 1.2f,
                    Valid = true,
                },
                Van = new VehicleSnapshot
                {
                    X = CityLayout.WorldX(-40f),
                    Y = 0.4f,
                    Z = CityLayout.WorldZ(0f),
                    RotationY = -1.57f,
                    Valid = true,
                },
            };
            data.CityMissionProgress["C01"] = new Dictionary<string, int>
            {
                [CityMissionService.KeyLoaded] = 1,
                ["stop_1"] = 1,
            };
            SaveService.Save(data, path, Main.Probe.Catalog);
            var loaded = SaveService.Load(path, Main.Probe.Catalog);
            Expect(loaded.Success, "mid-drive city save loads: " + loaded.Message);
            Expect(loaded.Data.ActiveCityMission == "C01", "active city job survives the save");
            Expect(loaded.Data.GetCityMissionCounter("C01", "stop_1") == 1,
                "city number-order progress survives the save");
            Expect(Mathf.IsEqualApprox(loaded.Data.Player.X, CityLayout.WorldX(0f)),
                "city player position survives the extended save bounds");
            Expect(loaded.Data.Driving && loaded.Data.Van.Valid,
                "mid-drive state and van snapshot round-trip");
            Expect(Math.Abs(loaded.Data.CityLogicalSeconds - 4321.5) < 0.001,
                "city logical clock round-trips");
            Expect(loaded.Data.SchemaVersion == SaveService.CurrentVersion,
                "mid-drive save is written on the current schema");
        }
        finally
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            catch
            {
                // Test cleanup only.
            }
        }
    }

    private void LoadedOpenRoadCityChecks()
    {
        var original = Main.Probe.Data;
        string path = ProjectSettings.GlobalizePath("user://selftest-city-open-road.json");
        try
        {
            // Load a valid minimal save through the real load path, then mark
            // its road open in memory. If the city service kept the pre-load
            // GameData, it would still see a closed road and reject the job.
            SaveService.Save(new GameData(), path, Main.Probe.Catalog);
            Main.Probe.LoadSaveForTest(path);

            Expect(ReferenceEquals(Main.Probe.CityMissionData, Main.Probe.Data),
                "the city job service follows the loaded save");
            Main.Probe.Data.CityRoadOpen = true;
            Expect(Main.Probe.CityMissionData.CityRoadOpen,
                "an open road on the loaded save reaches the city job service");
            var started = Main.Probe.StartCityMission("C01");
            Expect(started.Success,
                "a loaded open-road save can start a city job: " + started.Message);
        }
        finally
        {
            Main.Probe.AdoptLoadedDataForTest(original);
            try
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            catch
            {
                // Test cleanup only.
            }
        }
    }

    private async Task Wait(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds, processAlways: true),
            SceneTreeTimer.SignalName.Timeout);
    }

    private void Expect(bool condition, string name)
    {
        if (condition)
        {
            _passed++;
            GD.Print("PASS  " + name);
        }
        else
        {
            Fail(name);
        }
    }

    private void Fail(string name)
    {
        _failed++;
        GD.PrintErr("FAIL  " + name);
    }
}
