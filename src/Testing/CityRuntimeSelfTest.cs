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
            Main.RtSuppressSaves();
            await Wait(0.7f);
            SceneContractChecks();
            await TownLogicalChecks();
            RoadworksDeliveryChecks();
            GateAndMissionsChecks();
            await CityActivationChecks();
            await HighSpeedAndReturnChecks();
            SaveRoundTripChecks();
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
        var city = Main.RtCity;
        var road = Main.RtRoadworks;
        Expect(city != null, "CityWorldAdapter is built");
        if (city == null) return;
        Expect(city.BuildingCount == 36, "36 authored city buildings instantiated");
        Expect(city.HlodCount == 36, "one authored HLOD proxy per city building");
        Expect(city.CellCount == 9, "the fixed district exposes nine block cells");
        Expect(city.Simulation != null, "compact logical city simulation exists");
        Expect(city.Simulation.VehicleRoutes.Count == 4, "four authored vehicle lanes");
        Expect(city.Simulation.PedestrianRoutes.Count == 11, "eleven authored pedestrian routes");
        Expect(city.BoardInteraction.DistanceTo(
            new Vector3(CityLayout.WorldX(0f), 0.4f, CityLayout.WorldZ(20f))) < 0.1f,
            "CITY JOBS interaction follows its authored marker off the road");
        Expect(road != null && road.BarrierVisible, "closed road starts with the authored barrier visible");
        Expect(road != null && road.ColliderEnabled, "closed road starts with physical collision");
        Expect(Main.RtCityMissions.GetStatus("C01") == CityMissionStatus.Available,
            "C01 is on the independent city catalogue");
        Expect(Main.RtCityMissions.GetStatus("C02") == CityMissionStatus.Available,
            "C02 is on the independent city catalogue");
        Expect(Main.RtCityMissions.GetStatus("C03") == CityMissionStatus.Available,
            "C03 is on the independent city catalogue");
    }

    private async Task TownLogicalChecks()
    {
        var city = Main.RtCity;
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
        var data = Main.RtData;
        data.ActiveMission = RoadworksRules.MissionId;
        data.SetMissionCounter(RoadworksRules.MissionId, RoadworksRules.SuppliesDeliveredKey, 0);
        data.SetMissionCounter(RoadworksRules.MissionId, RoadworksRules.TaskDoneKey, 0);
        data.AddItem("safety_cone", RoadworksRules.ConeCount);
        data.AddItem("barrier_panel", RoadworksRules.PanelCount);
        data.AddItem("toolbox", RoadworksRules.ToolboxCount);
        Expect(RoadworksRules.HasAllSupplies(data),
            "roadworks delivery test starts with all six supplies carried");
        Main.RtRefreshMissionState();
        Expect(Main.RtDestinationLabel == "ROADWORKS DELIVERY",
            "all six carried supplies move the objective off the workshop");
        Expect(Main.RtDestination.DistanceTo(Main.RtRoadworks.InteractionPosition) < 0.1f,
            "the delivery objective points at the closed city gate");

        Main.RtInteractRoadworks();
        Expect(RoadworksRules.SuppliesDelivered(data),
            "the real worksite interaction delivers all six supplies");
        Expect(data.GetItem("safety_cone") == 0 &&
               data.GetItem("barrier_panel") == 0 &&
               data.GetItem("toolbox") == 0,
            "worksite delivery consumes the carried supplies exactly once");
        Expect(Main.RtModalTitle == "Roadworks loading check",
            "worksite delivery opens the crew loading check");
        Main.RtCloseModal();

        data.ActiveMission = "";
        data.SetMissionCounter(RoadworksRules.MissionId, RoadworksRules.SuppliesDeliveredKey, 0);
        data.SetMissionCounter(RoadworksRules.MissionId, RoadworksRules.TaskDoneKey, 0);
    }

    private void GateAndMissionsChecks()
    {
        var cityMissions = Main.RtCityMissions;
        var startClosed = cityMissions.StartMission("C01");
        Expect(!startClosed.Success, "city jobs are gated by the closed road");
        Main.RtSetRoadState(true);
        Expect(!Main.RtRoadworks.BarrierVisible, "completed roadworks remove the barrier visual");
        Expect(!Main.RtRoadworks.ColliderEnabled, "completed roadworks disable the physical collider");
        Main.RtRoadworks.SetRoadState(true, false);
        Expect(Main.RtRoadworks.RoadOpen && !Main.RtRoadworks.ColliderEnabled,
            "road open state is idempotent");

        var c1 = cityMissions.StartMission("C01");
        Expect(c1.Success, "C01 starts from the independent city board");
        Expect(cityMissions.LoadCourierParcels().Success, "courier parcels load once");
        Expect(!cityMissions.DeliverCourierStop(2).Success, "courier rejects stop 2 before stop 1");
        Expect(cityMissions.DeliverCourierStop(1).Success, "courier accepts stop 1");
        Expect(cityMissions.DeliverCourierStop(2).Success, "courier accepts stop 2");
        Expect(cityMissions.DeliverCourierStop(3).Success, "courier accepts stop 3");
        int wallet = Main.RtData.Coins;
        var c1Done = cityMissions.TurnInAtBoard();
        Expect(c1Done.Success, "courier turns in at the city board");
        Expect(Main.RtData.Coins == wallet + 20, "courier pays its 20 coins once");
        Expect(!cityMissions.TurnInAtBoard().Success, "courier cannot be turned in twice");

        Expect(cityMissions.StartMission("C02").Success, "C02 starts independently");
        for (int i = 0; i < 6; i++)
            Expect(cityMissions.CollectMarketCrate(i).Success, $"market crate {i} collected");
        Expect(!cityMissions.AnswerMarketStock(5).Success, "market rejects 5");
        Expect(cityMissions.AnswerMarketStock(6).Success, "2 × 3 = 6 is accepted");
        wallet = Main.RtData.Coins;
        Expect(cityMissions.TurnInAtBoard().Success, "market stock-up turns in");
        Expect(Main.RtData.Coins == wallet + 24, "market stock-up pays 24 coins");

        Expect(cityMissions.StartMission("C03").Success, "C03 starts independently");
        Expect(!cityMissions.RepairCabinet(1, "amber").Success, "cabinet 1 rejects amber");
        Expect(cityMissions.RepairCabinet(1, "red").Success, "cabinet 1 accepts red");
        Expect(!cityMissions.RepairCabinet(3, "green").Success, "cabinet 3 rejects out-of-order repair");
        Expect(cityMissions.RepairCabinet(2, "amber").Success, "cabinet 2 accepts amber");
        Expect(cityMissions.RepairCabinet(3, "green").Success, "cabinet 3 accepts green");
        wallet = Main.RtData.Coins;
        var engineer = cityMissions.TalkToEngineer();
        Expect(engineer.Success && engineer.Completed, "engineer sign-off completes C03");
        Expect(Main.RtData.Coins == wallet + 28, "signals job pays 28 coins once");
        Expect(Main.RtData.CompletedCityMissions.Count == 3,
            "three city jobs are persisted as distinct completions");
    }

    private async Task CityActivationChecks()
    {
        var city = Main.RtCity;
        Main.RtTeleportPlayer(new Vector3(CityLayout.WorldX(0f), 0.5f, CityLayout.WorldZ(28f)), 0f);
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
    }

    private async Task HighSpeedAndReturnChecks()
    {
        var city = Main.RtCity;
        // Physical gate test at maximum van speed.
        Main.RtSetRoadState(false);
        Main.RtTeleportVan(new Vector3(80f, 0.4f, 0f), -Mathf.Pi / 2f);
        Main.RtLaunchVan(11.5f);
        await Wait(1.3f);
        Expect(Main.RtVanPosition.X < 95.0f,
            "closed gate physically stops a full-speed van");
        Main.RtSetRoadState(true);
        Main.RtTeleportVan(new Vector3(80f, 0.4f, 0f), -Mathf.Pi / 2f);
        Main.RtLaunchVan(11.5f);
        await Wait(2.2f);
        Expect(Main.RtVanPosition.X > 97.0f,
            "open gate lets the van drive onto the approach at full speed");

        Vector3 approach = new(118f, 0.4f, 0f);
        Main.RtProbeCityLod(approach, 11.5f);
        Expect(city.DistrictVisible && city.ApproachVisible,
            "high-speed approach keeps the join and district active");
        Expect(city.DetailedVisible + city.HlodVisible > 0,
            "high-speed approach has a visible authored cell budget");
        Main.RtProbeCityLod(new Vector3(CityLayout.WorldX(0f), 0.4f, CityLayout.WorldZ(0f)), 11.5f);
        Expect(city.DetailedVisible > 0 && city.HlodVisible >= 0,
            "high-speed centre keeps detailed geometry near the player");

        Main.RtTeleportPlayer(new Vector3(-38f, 0.4f, 9.5f), 0f);
        await Wait(0.9f);
        Expect(!city.DistrictVisible, "returning to town hides the whole city district");
        Expect(city.ActiveVisualActors == 0, "returning to town destroys every visual city actor");
        Expect(city.ActiveDistrictCollisionBodies == 0, "returning to town disables offscreen city collision");

        Main.RtSetRoadState(false);
        Expect(Main.RtRoadworks.BarrierVisible && Main.RtRoadworks.ColliderEnabled,
            "a fresh/closed state restores the gate exactly");
        Main.RtSetRoadState(true);
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
                    X = CityLayout.WorldX(0f), Y = 0.4f, Z = CityLayout.WorldZ(28f),
                    RotationY = 1.2f, Valid = true,
                },
                Van = new VehicleSnapshot
                {
                    X = CityLayout.WorldX(-40f), Y = 0.4f, Z = CityLayout.WorldZ(0f),
                    RotationY = -1.57f, Valid = true,
                },
            };
            data.CityMissionProgress["C01"] = new Dictionary<string, int>
            {
                [CityMissionService.KeyLoaded] = 1, ["stop_1"] = 1,
            };
            SaveService.Save(data, path, Main.RtCatalog);
            var loaded = SaveService.Load(path, Main.RtCatalog);
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
