using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PatchworkIsland;

namespace PatchworkIsland.Tests;

internal static partial class Program
{
    private static void CityExpansionTests(MissionCatalog catalog, CityMissionCatalog cityCatalog)
    {
        Run("M21 is gated after the full required chain, not after optional racing", () =>
        {
            var before = CompleteM19(CompleteM18(CompleteM17(CompleteM16(
                CompleteFirstHalf(Fresh(catalog))))));
            Equal(MissionStatus.Available, before.GetStatus("M20"), "M20 opens after M19");
            Equal(MissionStatus.Locked, before.GetStatus("M21"), "M21 stays locked before M20");
            var after = CompleteM20(before);
            Equal(MissionStatus.Available, after.GetStatus("M21"), "M21 opens after M20");
            Check(after.Data.IsCompleted("M20"), "M20 finale required");
            Check(!after.Data.IsCompleted("M21"), "M21 is not auto-completed");
            Equal(21, catalog.Missions.Count, "catalogue keeps one extra unlock mission");
        });

        Run("Roadworks Rescue requires 3 cones + 2 panels + toolbox, then a 6/order check", () =>
        {
            var s = CompleteM20(CompleteM19(CompleteM18(CompleteM17(CompleteM16(
                CompleteFirstHalf(Fresh(catalog)))))));
            Check(s.StartMission("M21").Success, "M21 starts after the required chain");
            for (int i = 0; i < 3; i++)
                Check(s.CollectPickup($"roadworks_cone_{i}", "safety_cone", "M21", 3).Success,
                    $"cone {i} collected");
            for (int i = 0; i < 2; i++)
                Check(s.CollectPickup($"roadworks_panel_{i}", "barrier_panel", "M21", 2).Success,
                    $"panel {i} collected");
            Check(s.CollectPickup("roadworks_toolbox_0", "toolbox", "M21", 1).Success,
                "toolbox collected");
            Equal(6, RoadworksRules.SupplyProgress(s.Data), "six supplies counted");
            var early = s.CompleteRoadworksTask(6, RoadworksRules.CorrectOrder);
            Check(!early.Success, "task cannot finish before delivery");
            var delivered = s.DeliverRoadworksSupplies();
            Check(delivered.Success && delivered.Completion == null, "supply delivery is a partial step");
            Equal(1, s.Data.GetMissionCounter("M21", RoadworksRules.SuppliesDeliveredKey),
                "supply delivery persisted");
            Equal(0, s.Data.GetItem("safety_cone"), "cones consumed");
            Equal(0, s.Data.GetItem("barrier_panel"), "panels consumed");
            Equal(0, s.Data.GetItem("toolbox"), "toolbox consumed");
            Check(!s.CompleteRoadworksTask(5, RoadworksRules.CorrectOrder).Success,
                "wrong total rejected without cost");
            Check(!s.CompleteRoadworksTask(6, new[] { "panels", "cones", "toolbox" }).Success,
                "wrong loading order rejected");
            Equal(0, s.Data.GetMissionCounter("M21", RoadworksRules.TaskDoneKey),
                "wrong answers do not tick the task");
            int wallet = s.Data.Coins;
            var done = s.CompleteRoadworksTask(6, RoadworksRules.CorrectOrder);
            Check(done.Success && done.Completion != null, "correct total and order completes M21");
            Equal(25, done.Completion.CoinsAwarded, "M21 reward");
            Equal(wallet + 25, s.Data.Coins, "M21 paid once");
            Check(s.Data.IsCompleted("M21"), "M21 recorded complete");
            Check(MissionRules.IsObjectiveComplete(s.Data, "M21"), "M21 objective persisted");
            Check(!s.CompleteRoadworksTask(6, RoadworksRules.CorrectOrder).Success,
                "M21 cannot double-award");
            Equal(wallet + 25, s.Data.Coins, "wallet unchanged on repeat");
        });

        Run("M21 completion and CityRoadOpen persist without changing M01-M20 semantics", () =>
        {
            var s = CompleteM20(CompleteM19(CompleteM18(CompleteM17(CompleteM16(
                CompleteFirstHalf(Fresh(catalog)))))));
            Check(s.StartMission("M21").Success, "M21 starts");
            for (int i = 0; i < 3; i++)
                s.CollectPickup($"roadworks_cone_{i}", "safety_cone", "M21", 3);
            for (int i = 0; i < 2; i++)
                s.CollectPickup($"roadworks_panel_{i}", "barrier_panel", "M21", 2);
            s.CollectPickup("roadworks_toolbox_0", "toolbox", "M21", 1);
            s.DeliverRoadworksSupplies();
            var finished = s.CompleteRoadworksTask(6, RoadworksRules.CorrectOrder);
            Check(finished.Success, "M21 completed for save test");
            s.Data.CityRoadworksCompleted = true;
            s.Data.CityRoadworksRewardClaimed = true;
            s.Data.CityRoadOpen = true;
            s.Data.CitySeed = 424242;
            s.Data.CityLogicalSeconds = 987.25;
            s.Data.Player = new PlayerSnapshot
            {
                X = CityLayout.WorldX(0f), Y = 0.4f, Z = CityLayout.WorldZ(24f),
                RotationY = 0f, Valid = true,
            };
            string dir = TempDirectory("roadworks-save");
            try
            {
                string path = Path.Combine(dir, "save.json");
                SaveService.Save(s.Data, path, catalog);
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, loaded.Message);
                Check(loaded.Data.IsCompleted("M21"), "M21 survives reload");
                Check(loaded.Data.CityRoadOpen && loaded.Data.CityRoadworksCompleted &&
                      loaded.Data.CityRoadworksRewardClaimed, "road state survives reload");
                Equal(987.25, loaded.Data.CityLogicalSeconds, "city clock survives reload");
                Equal(424242, loaded.Data.CitySeed, "city seed survives reload");
                Check(loaded.Data.IsCompleted("M20"), "M20 stays complete");
                Check(AllTwentyComplete(loaded.Data), "required twenty unchanged");
                Check(!loaded.Data.IsCompleted("M22"), "no invented town mission");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("city job catalogue is distinct and C01-C03 are playable", () =>
        {
            Check(CityMissionCatalog.Validate(cityCatalog, out string error), "city catalogue valid: " + error);
            Equal(3, cityCatalog.Missions.Count, "three initial city jobs");
            Equal("C01", cityCatalog.Missions[0].Id, "C01 first");
            Equal("C03", cityCatalog.Missions[^1].Id, "C03 last");
            Check(cityCatalog.Missions.All(m => m.Id.StartsWith("C", StringComparison.Ordinal)),
                "city ids are C-prefixed");
            var data = new GameData();
            var service = new CityMissionService(data, cityCatalog);
            Check(!service.StartMission("C01").Success, "closed road blocks city jobs");
            data.CityRoadOpen = true;
            Check(service.StartMission("C01").Success, "C01 starts at CITY JOBS");
            Check(service.LoadCourierParcels().Success, "parcels load");
            Check(!service.DeliverCourierStop(2).Success, "stop 2 out of order rejected");
            Check(service.DeliverCourierStop(1).Success, "stop 1 accepted");
            Check(service.DeliverCourierStop(2).Success, "stop 2 accepted");
            Check(service.DeliverCourierStop(3).Success, "stop 3 accepted");
            int wallet = data.Coins;
            var c1 = service.TurnInAtBoard();
            Check(c1.Success, "C01 turns in");
            Equal(wallet + 20, data.Coins, "C01 pays 20 once");
            Check(!service.TurnInAtBoard().Success, "C01 cannot double-pay");

            Check(service.StartMission("C02").Success, "C02 starts after C01");
            for (int i = 0; i < 6; i++)
                Check(service.CollectMarketCrate(i).Success, $"C02 crate {i}");
            Check(!service.AnswerMarketStock(5).Success, "2 x 3 rejects 5");
            Check(service.AnswerMarketStock(6).Success, "2 x 3 accepts 6");
            wallet = data.Coins;
            Check(service.TurnInAtBoard().Success, "C02 turns in");
            Equal(wallet + 24, data.Coins, "C02 pays 24 once");

            Check(service.StartMission("C03").Success, "C03 starts after C02");
            Check(!service.RepairCabinet(1, "amber").Success, "cabinet 1 needs red");
            Check(service.RepairCabinet(1, "red").Success, "cabinet 1 red");
            Check(!service.RepairCabinet(3, "green").Success, "cabinet order enforced");
            Check(service.RepairCabinet(2, "amber").Success, "cabinet 2 amber");
            Check(service.RepairCabinet(3, "green").Success, "cabinet 3 green");
            wallet = data.Coins;
            var engineer = service.TalkToEngineer();
            Check(engineer.Completed, "engineer completes C03");
            Equal(wallet + 28, data.Coins, "C03 pays 28 once");
            Equal(3, data.CompletedCityMissions.Distinct().Count(), "three distinct city completions");
            Equal("", data.ActiveCityMission, "no city job left active");
        });

        Run("city progress, wallet and road state survive save/reload", () =>
        {
            var setup = CompleteM20(CompleteM19(CompleteM18(CompleteM17(CompleteM16(
                CompleteFirstHalf(Fresh(catalog)))))));
            setup.StartMission("M21");
            for (int i = 0; i < 3; i++)
                setup.CollectPickup($"roadworks_cone_{i}", "safety_cone", "M21", 3);
            for (int i = 0; i < 2; i++)
                setup.CollectPickup($"roadworks_panel_{i}", "barrier_panel", "M21", 2);
            setup.CollectPickup("roadworks_toolbox_0", "toolbox", "M21", 1);
            setup.DeliverRoadworksSupplies();
            setup.CompleteRoadworksTask(6, RoadworksRules.CorrectOrder);
            var data = setup.Data;
            data.CityRoadOpen = true;
            data.CityRoadworksCompleted = true;
            data.CityRoadworksRewardClaimed = true;
            data.CityVisited = true;
            data.CitySeed = 777;
            data.CityLogicalSeconds = 321.5;
            data.ActiveCityMission = "C01";
            data.CityMissionProgress["C01"] = new Dictionary<string, int>
            {
                [CityMissionService.KeyLoaded] = 1, ["stop_1"] = 1,
            };
            string dir = TempDirectory("city-save");
            try
            {
                string path = Path.Combine(dir, "save.json");
                SaveService.Save(data, path, catalog);
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, loaded.Message);
                Check(loaded.Data.CityRoadOpen && loaded.Data.CityVisited, "city flags restored");
                Equal("C01", loaded.Data.ActiveCityMission, "active city mission restored");
                Equal(1, loaded.Data.GetCityMissionCounter("C01", "stop_1"), "city progress restored");
                Equal(321.5, loaded.Data.CityLogicalSeconds, "city clock restored");
                Equal(777, loaded.Data.CitySeed, "city seed restored");
                Equal(SaveService.CurrentVersion, loaded.Data.SchemaVersion, "city save is current schema");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });

        Run("city save validation rejects impossible road and job states", () =>
        {
            var open = new GameData { CityRoadOpen = true };
            var openResult = SaveValidation.Validate(open, catalog);
            Check(!openResult.IsValid, "open road without M21 rejected");
            Check(openResult.Message.Contains("road"), "road validation is explicit");

            var done = new GameData { CityRoadworksCompleted = true };
            Check(!SaveValidation.Validate(done, catalog).IsValid,
                "roadworks completion without M21 rejected");

            var fakeCityJob = new GameData();
            fakeCityJob.CompletedCityMissions.Add("C01");
            Check(!SaveValidation.Validate(fakeCityJob, catalog).IsValid,
                "completed city job without progress rejected");

            var badCounter = new GameData();
            badCounter.CityMissionProgress["C02"] = new Dictionary<string, int> { ["crate_9"] = 1 };
            Check(!SaveValidation.Validate(badCounter, catalog).IsValid,
                "unknown city counter rejected");

            var cityPose = new GameData
            {
                Player = new PlayerSnapshot { X = 320f, Y = 0.4f, Z = 24f, Valid = true },
                Van = new VehicleSnapshot { X = 200f, Y = 0.4f, Z = 40f, Valid = true },
            };
            Check(SaveValidation.Validate(cityPose, catalog).IsValid,
                "city positions are inside the expanded save bounds");

            cityPose.Player.X = 9000f;
            Check(!SaveValidation.Validate(cityPose, catalog).IsValid,
                "absurd city positions are still rejected");
        });

        Run("city logical simulation is deterministic and bounded at 30 minutes", () =>
        {
            var vehicle = new[]
            {
                new CityRoutePoint(-40, -40), new CityRoutePoint(40, -40),
                new CityRoutePoint(40, 40), new CityRoutePoint(-40, 40),
            };
            var ped = new[]
            {
                new CityRoutePoint(-80, -80), new CityRoutePoint(80, -80),
                new CityRoutePoint(80, 80), new CityRoutePoint(-80, 80),
            };
            var a = new CitySimulation(new[] { vehicle }, new[] { ped }, 12345, 4, 6);
            var b = new CitySimulation(new[] { vehicle }, new[] { ped }, 12345, 4, 6);
            Equal(a.Agents.Count, 10, "bounded logical population");
            var start = a.Pose(10_000);
            a.Advance(1800);
            b.Advance(1800);
            Equal(1800.0, a.LogicalSeconds, "30-minute offscreen advance is accepted");
            var endA = a.Pose(10_000);
            var endB = b.Pose(10_000);
            Check(!(MathF.Abs(start.X - endA.X) < 0.01f && MathF.Abs(start.Z - endA.Z) < 0.01f),
                "traffic advances while no scene is loaded");
            Check(MathF.Abs(endA.X - endB.X) < 0.001f && MathF.Abs(endA.Z - endB.Z) < 0.001f,
                "two identical seeds produce identical far-city poses");
            var c = new CitySimulation(new[] { vehicle }, new[] { ped }, 999, 4, 6);
            c.Advance(1800);
            Check(MathF.Abs(c.Pose(10_000).X - endA.X) > 0.01f ||
                  MathF.Abs(c.Pose(10_000).Z - endA.Z) > 0.01f,
                "a different seed produces a different schedule");

            var clamped = new CitySimulation(new[] { vehicle }, new[] { ped }, 1, 1, 1);
            clamped.Advance(60 * 60);
            Equal(CitySimulation.MaxCatchUpSeconds, clamped.LogicalSeconds,
                "a single huge jump is clamped to 30 minutes");
        });

        Run("city scene contracts preserve authored layout, joins and collision", () =>
        {
            string root = FindRepoRoot();
            string district = File.ReadAllText(Path.Combine(root, "scenes", "city", "CityDistrict.tscn"));
            Equal(36, Regex.Matches(district,
                "\\[node name=\"Building_[0-9]+_[^\"]+\" parent=\"\\.\" instance=").Count,
                "36 authored building instances");
            Equal(12, Directory.GetFiles(Path.Combine(root, "scenes", "city", "assets", "hlod"),
                "*HLOD.tscn").Length, "12 authored HLOD proxies");
            var board = ParseNodePosition(district, "CityJobBoard");
            Equal(22f, board.Z, "city board is moved off the 28 m road edge");
            var boardAnchor = ParseNodePosition(district, "CityJobBoardAnchor");
            Equal(19f, boardAnchor.Z, "city board anchor stays off the carriageway");
            var arrival = ParseNodePosition(district, "CityArrival");
            Equal(-159f, arrival.X, "city arrival marker is stable");
            Check(district.Contains("CityGround"), "city ground collision is authored");
            Check(district.Contains("PlazaShape_0"), "plaza collision is authored");
            Check(district.Contains("SidewalkShape_NSSidewalk_0_0"),
                "sidewalk collision is authored");
            var cityBoxes = ParseBoxSizes(district, "BoxMesh");
            int sidewalkSegments = 0;
            foreach (Match match in Regex.Matches(district,
                         "\\[node name=\"(?<name>(?:NS|EW)Sidewalk_[^\"]+)\" type=\"MeshInstance3D\" parent=\"\\.\"\\]\\r?\\n" +
                         "position = Vector3\\((?<pos>[^)]+)\\)\\r?\\n" +
                         "mesh = SubResource\\(\"(?<mesh>[^\"]+)\"\\)",
                         RegexOptions.Singleline))
            {
                string name = match.Groups["name"].Value;
                var parts = match.Groups["pos"].Value.Split(',');
                float px = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
                float pz = float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
                var size = cityBoxes[match.Groups["mesh"].Value];
                bool alongZ = name.StartsWith("NS", StringComparison.Ordinal);
                float centre = alongZ ? pz : px;
                float half = (alongZ ? size.Z : size.X) * 0.5f;
                foreach (float axis in CityLayout.RoadAxes)
                {
                    float roadMin = axis - CityLayout.RoadWidth * 0.5f;
                    float roadMax = axis + CityLayout.RoadWidth * 0.5f;
                    Check(centre + half <= roadMin + 0.01f ||
                          centre - half >= roadMax - 0.01f,
                        $"sidewalk '{name}' does not cross the road at {axis:0} m");
                }
                sidewalkSegments++;
            }
            Check(sidewalkSegments >= 80,
                "sidewalks are split into intersection-safe authored segments");
            foreach (Match match in Regex.Matches(district,
                         "\\[node name=\"BlockPlaza_[^\"]+\" type=\"MeshInstance3D\" parent=\"\\.\"\\]\\r?\\n" +
                         "position = Vector3\\((?<pos>[^)]+)\\)\\r?\\n" +
                         "mesh = SubResource\\(\"(?<mesh>[^\"]+)\"\\)",
                         RegexOptions.Singleline))
            {
                var parts = match.Groups["pos"].Value.Split(',');
                float px = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
                float pz = float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
                var size = cityBoxes[match.Groups["mesh"].Value];
                foreach (float axis in CityLayout.RoadAxes)
                {
                    float roadMin = axis - CityLayout.RoadWidth * 0.5f;
                    float roadMax = axis + CityLayout.RoadWidth * 0.5f;
                    Check(px + size.X * 0.5f <= roadMin + 0.01f ||
                          px - size.X * 0.5f >= roadMax - 0.01f,
                        $"plaza at x={px:0} does not overlap a north/south road");
                    Check(pz + size.Z * 0.5f <= roadMin + 0.01f ||
                          pz - size.Z * 0.5f >= roadMax - 0.01f,
                        $"plaza at z={pz:0} does not overlap an east/west road");
                }
            }

            float outerEdge = CityLayout.RoadAxes[CityLayout.RoadAxes.Length - 1] +
                              CityLayout.RoadWidth * 0.5f;
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                Check(CityLayout.IsWithinGroundCollision(
                        CityLayout.WorldX(sx * outerEdge), CityLayout.WorldZ(sz * outerEdge)),
                    $"city ground collision covers the outer carriageway corner ({sx:0},{sz:0})");
            Check(CityLayout.IsWithinGroundCollision(CityLayout.WorldX(outerEdge),
                    CityLayout.WorldZ(0f)),
                "city ground collision covers the outer carriageway edge");
            Check(!CityLayout.IsWithinGroundCollision(0f, 0f),
                "the town centre stays outside the city ground-collision footprint");
            Check(CityLayout.GroundCollisionHalfExtent > outerEdge + 40f,
                "city ground collision keeps a safety margin beyond the outer carriageway");

            var jobs = File.ReadAllText(Path.Combine(root, "scenes", "city", "CityJobs.tscn"));
            Equal(15, Regex.Matches(jobs, "metadata/city_job_id =").Count,
                "15 authored city job anchors");
            var buildingMeshes = new Dictionary<string, (float X, float Z, float Hx, float Hz)>();
            foreach (string file in Directory.GetFiles(Path.Combine(root, "scenes", "city", "assets",
                         "buildings"), "*.tscn"))
            {
                string text = File.ReadAllText(file);
                Check(text.Contains("BuildingCollision"),
                    Path.GetFileName(file) + " has a building envelope collider");
                Check(text.Contains("DoorMarker"),
                    Path.GetFileName(file) + " has an authored DoorMarker");
                var match = Regex.Match(text,
                    "\\[node name=\"MainVolume\" type=\"MeshInstance3D\" parent=\"\\.\"\\]\\r?\\n" +
                    "position = Vector3\\(([^)]+)\\)\\r?\\nmesh = SubResource\\(\"([^\"]+)\"\\)");
                var size = ParseBoxSizes(text, "BoxMesh")[match.Groups[2].Value];
                buildingMeshes[Path.GetFileNameWithoutExtension(file)] =
                    (0f, 0f, size.X / 2f, size.Z / 2f);
            }

            foreach (Match match in Regex.Matches(district,
                         "\\[node name=\"Building_[0-9]+_(?<arch>[^\"]+)\" parent=\"\\.\" instance="))
            {
                string arch = match.Groups["arch"].Value;
                if (!arch.Contains("_")) continue;
            }
            var cityPositions = ParseCityBuildingPositions(district);
            foreach (Match marker in Regex.Matches(jobs,
                         "\\[node name=\"Marker_[^\"]+\" type=\"Marker3D\" parent=\"\\.\"\\]\\r?\\n" +
                         "position = Vector3\\(([^)]+)\\)"))
            {
                var parts = marker.Groups[1].Value.Split(',');
                float x = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
                float z = float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
                Check(x >= -180f && x <= 180f && z >= -180f && z <= 180f,
                    $"city job marker ({x:0},{z:0}) stays inside the district");
            }

            var approach = File.ReadAllText(Path.Combine(root, "scenes", "city",
                "TownCityApproach.tscn"));
            var townJoin = ParseNodePosition(approach, "TownApproachJoin");
            var cityJoin = ParseNodePosition(approach, "CityApproachJoin");
            Equal(CityLayout.WorldX(-132f), CityLayout.OriginX + cityJoin.X,
                "city-side join meets the west carriageway edge at 188 m");
            Equal(CityLayout.WorldX(-271f), CityLayout.OriginX + townJoin.X,
                "town-side join meets the bridge exit at 49 m");
            Check(approach.Contains("shape_road") && approach.Contains("RailNorthShape"),
                "approach road and bridge rails have authored collision");
            var approachWest = ParseNodePosition(approach, "WestApproach");
            var approachBridge = ParseNodePosition(approach, "BridgeDeck");
            var approachEast = ParseNodePosition(approach, "EastApproach");
            var approachCollision = ParseNodePosition(approach, "RoadShape");
            Check(approachWest.Y <= -0.07f && approachBridge.Y <= -0.08f &&
                  approachEast.Y <= -0.07f,
                "approach surfaces sit below the rally surface instead of forming a slab");
            Check(approachCollision.Y <= -0.14f,
                "approach road collision is flush with the island/rally floor");

            var roadworks = File.ReadAllText(Path.Combine(root, "scenes", "city",
                "TownCityRoadworks.tscn"));
            Check(roadworks.Contains("GateCollision"), "roadworks gate collider is authored");
            Check(roadworks.Contains("size = Vector3(0.8, 3.2, 16.6)"),
                "gate collision spans the full 16 m boundary opening");
            Check(roadworks.Contains("CrewForeman") && roadworks.Contains("CrewWorkerA"),
                "authored crew members are present for the completion animation");

            string terrain = File.ReadAllText(Path.Combine(root, "scenes", "world", "assets",
                "environment", "Terrain.tscn"));
            Check(terrain.Contains("BoundEastSouth") && terrain.Contains("BoundEastSouthShape"),
                "the east island boundary is split around the authored road opening");
            Check(terrain.Contains("size = Vector3(1.2, 1.4, 46)"),
                "boundary halves leave a 16 m road gap");
        });
    }

}
