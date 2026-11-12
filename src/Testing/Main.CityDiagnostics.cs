using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

public partial class Main
{
    private sealed partial class DeveloperTools
    {
        internal async Task RunCityProbe()
        {
            try
            {
                CompleteVillageForScenario();
                _game._data.CityRoadOpen = true;
                _game._data.CityRoadworksCompleted = true;
                _game._data.CityRoadworksRewardClaimed = true;
                _game._roadworks.SetRoadState(true, false);
                _game._player.Teleport(new Vector3(CityLayout.WorldX(0f), 0.5f, CityLayout.WorldZ(28f)), 0f);
                _game._rig.SetView(0f, 20f, 9.5f);
                await Wait(2.2f);
                GD.Print($"CITY_PROBE city {_game._city.StatsLine()} " +
                         $"collision={_game._city.ActiveDistrictCollisionBodies} " +
                         $"vehicles={_game._city.ActorPool?.VisualVehicleCount} peds={_game._city.ActorPool?.VisualPedestrianCount} " +
                         $"nearV={_game._city.ActorPool?.NearVehicleCount} nearP={_game._city.ActorPool?.NearPedestrianCount}");
                var board = _game._city.BoardInteraction;
                GD.Print($"CITY_PROBE board={board} roadworks={_game._roadworks.RoadOpen} " +
                         $"barrier={_game._roadworks.BarrierVisible} collider={_game._roadworks.ColliderEnabled}");
                double logical = _game._city.LogicalSeconds;
                _game._player.Teleport(new Vector3(-38f, 0.3f, 9.5f), 0f);
                _game._rig.SetView(0f, 20f, 8.2f);
                await Wait(2.2f);
                GD.Print($"CITY_PROBE town {_game._city.StatsLine()} logicalDelta={_game._city.LogicalSeconds - logical:0.00} " +
                         $"townVisible={_game._city.DistrictVisible}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"City probe failed: {ex}");
            }
            _game.GetTree().Quit();
        }

        internal async Task RunCityPerformanceProbe()
        {
            var results = new List<string>();
            try
            {
                CompleteVillageForScenario();
                Input.MouseMode = Input.MouseModeEnum.Visible;
                GD.Print("GPU_DEVICE " + RenderingServer.GetVideoAdapterName());
                _game._data.CityRoadOpen = true;
                _game._data.CityRoadworksCompleted = true;
                _game._data.CityRoadworksRewardClaimed = true;
                _game._roadworks.SetRoadState(true, false);
                _game._hud.Visible = false;
                if (!DisplayServer.GetName().Equals("headless", StringComparison.OrdinalIgnoreCase))
                    DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
                Vector2I window = DisplayServer.WindowGetSize();
                string resolution = $"{window.X}x{window.Y}";

                async Task<string> Measure(string stage, Vector3 position, float yaw, bool cityVisibility)
                {
                    _game._player.Teleport(position, yaw);
                    _game._rig.SetView(Mathf.RadToDeg(yaw), 20f, 9.2f);
                    _game._rig.Snap();
                    var transition = new List<double>(30);
                    ulong transitionPrevious = Time.GetTicksUsec();
                    for (int frame = 0; frame < 30; frame++)
                    {
                        await _game.ToSignal(_game.GetTree(), SceneTree.SignalName.ProcessFrame);
                        ulong now = Time.GetTicksUsec();
                        transition.Add((now - transitionPrevious) / 1000.0);
                        transitionPrevious = now;
                    }
                    await Wait(1.4f);
                    var frameMs = new List<double>(240);
                    ulong previous = Time.GetTicksUsec();
                    long allocatedBefore = System.GC.GetAllocatedBytesForCurrentThread();
                    int gen0Before = System.GC.CollectionCount(0);
                    for (int i = 0; i < 240; i++)
                    {
                        await _game.ToSignal(_game.GetTree(), SceneTree.SignalName.ProcessFrame);
                        ulong now = Time.GetTicksUsec();
                        frameMs.Add((now - previous) / 1000.0);
                        previous = now;
                    }
                    frameMs.Sort();
                    double p50 = frameMs[frameMs.Count / 2];
                    double p95 = frameMs[Math.Min(frameMs.Count - 1, (int)(frameMs.Count * 0.95))];
                    double avg = frameMs.Average();
                    int draw = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
                    int primitives = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
                    int physics = (int)Performance.GetMonitor(Performance.Monitor.Physics3DActiveObjects);
                    int objects = (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
                    long allocated = System.GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                    string line = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        resolution, stage,
                        frame_ms_p50 = Math.Round(p50, 3), frame_ms_p95 = Math.Round(p95, 3),
                        frame_ms_avg = Math.Round(avg, 3), fps_p50 = Math.Round(1000.0 / Math.Max(.001, p50), 1),
                        transition_frame_ms_max = Math.Round(transition.Max(), 3),
                        managed_bytes_per_frame = Math.Round(allocated / 240.0, 1),
                        gen0_collections = System.GC.CollectionCount(0) - gen0Before,
                        draw_calls = draw, primitives, physics_active = physics, object_nodes = objects,
                        city = _game._city.StatsLine(),
                        actor_near = _game._city.NearVisualActors,
                        actor_mid = (_game._city.ActorPool?.MidVehicleCount ?? 0) + (_game._city.ActorPool?.MidPedestrianCount ?? 0),
                        district_visible = _game._city.DistrictVisible,
                    });
                    GD.Print("CITY_PERF " + line);
                    if (Main.HasUserArg("--perf-captures"))
                    {
                        await _game.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        string folder = ProjectSettings.GlobalizePath("user://performance");
                        Directory.CreateDirectory(folder);
                        _game.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(folder, stage + ".png"));
                    }
                    return line;
                }

                results.Add(await Measure("town-only", new Vector3(-38f, 0.3f, 9.5f), 0f, false));
                results.Add(await Measure("city-approach", new Vector3(120f, 0.4f, 0f), -Mathf.Pi / 2f, true));
                results.Add(await Measure("city-street", new Vector3(CityLayout.WorldX(0f), 0.4f,
                    CityLayout.WorldZ(18f)), 0f, true));
                results.Add(await Measure("return-to-town", new Vector3(-38f, 0.3f, 9.5f), 0f, false));

                if (Main.HasUserArg("--perf-drive"))
                    results.Add(await MeasureCityDrive(resolution));

                // Measure the bounded logical catch-up cost without agents/nodes.
                var probe = new CitySimulation(_game._city.Simulation.VehicleRoutes,
                    _game._city.Simulation.PedestrianRoutes, _game._city.Seed);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                probe.Advance(1800.0);
                sw.Stop();
                var catchup = $"{{\"logical_catchup_30min_ms\":{sw.Elapsed.TotalMilliseconds:0.000}," +
                              $"\"logical_seconds\":{probe.LogicalSeconds:0.0}," +
                              $"\"active_actor_nodes\":{_game._city.ActorPool?.VisualVehicleCount + _game._city.ActorPool?.VisualPedestrianCount}}}";
                GD.Print("CITY_PERF " + catchup);
                results.Add(catchup);

                string directory = ProjectSettings.GlobalizePath("user://performance");
                Directory.CreateDirectory(directory);
                string file = Path.Combine(directory, $"city_perf_{resolution}.jsonl");
                File.WriteAllLines(file, results);
                GD.Print($"CITY_PERF_FILE {file}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"City performance probe failed: {ex}");
                _game.GetTree().Quit(1);
                return;
            }
            _game.GetTree().Quit();
        }

        internal async Task<string> MeasureCityDrive(string resolution)
        {
            _game._data.ActiveMission = "";
            _game._data.ActiveCityMission = "";
            if (!_game._bridgeBuilder.IsBuilt)
            {
                _game._data.BridgePieces.Clear();
                _game._data.BridgePieces.AddRange(BuildRules.BridgeSockets);
                _game._bridgeBuilder.RefreshFromData();
            }
            // Stay east of the bridge: the test autopilot intentionally cuts
            // throttle inside its river safety zone. Normal input is unaffected.
            _game._van.GlobalPosition = new Vector3(80, .4f, 0);
            _game._van.Rotation = new Vector3(0, -Mathf.Pi / 2f, 0);
            _game._player.Teleport(new Vector3(78, .4f, 0));
            _game.EnterVan();
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _game._van.StartRouteAutopilot(new[]
            {
                new Vector3(80, .4f, 0), new Vector3(180, .4f, 0),
                new Vector3(200, .4f, 0), new Vector3(200, .4f, -40), new Vector3(280, .4f, -40),
                new Vector3(280, .4f, -120), new Vector3(200, .4f, -120), new Vector3(200, .4f, 0),
                new Vector3(170, .4f, 0), new Vector3(80, .4f, 0),
            });
            var frames = new List<double>(100000);
            var transitions = new List<double>(4096);
            ulong began = Time.GetTicksUsec(), previous = began;
            int lastDetail = _game._city.DetailedVisible, lastHlod = _game._city.HlodVisible;
            bool lastVisible = _game._city.DistrictVisible;
            int transitionWindow = 0;
            float furthest = 0;
            bool returned = false;
            while (Time.GetTicksUsec() - began < 240_000_000)
            {
                await _game.ToSignal(_game.GetTree(), SceneTree.SignalName.ProcessFrame);
                ulong now = Time.GetTicksUsec();
                double ms = (now - previous) / 1000.0; previous = now;
                frames.Add(ms);
                bool changed = lastDetail != _game._city.DetailedVisible || lastHlod != _game._city.HlodVisible || lastVisible != _game._city.DistrictVisible;
                if (changed) transitionWindow = 4;
                if (transitionWindow-- > 0) transitions.Add(ms);
                lastDetail = _game._city.DetailedVisible; lastHlod = _game._city.HlodVisible; lastVisible = _game._city.DistrictVisible;
                furthest = Mathf.Max(furthest, _game._van.GlobalPosition.X);
                if (furthest > 275 && _game._van.GlobalPosition.X < 88) { returned = true; break; }
            }
            _game._van.StopAutopilot();
            if (!returned) throw new InvalidOperationException("GPU drive did not finish: " + _game._van.GlobalPosition);
            frames.Sort();
            string line = System.Text.Json.JsonSerializer.Serialize(new
            {
                resolution, stage = "physical-city-return-drive", samples = frames.Count,
                elapsed_seconds = Math.Round((Time.GetTicksUsec() - began) / 1_000_000.0, 2),
                frame_ms_p50 = Math.Round(frames[frames.Count / 2], 3),
                frame_ms_p95 = Math.Round(frames[(int)(frames.Count * .95)], 3),
                frame_ms_max = Math.Round(frames[^1], 3),
                frames_over_33ms = frames.Count(ms => ms > 33),
                transition_samples = transitions.Count,
                transition_frame_ms_max = Math.Round(transitions.DefaultIfEmpty(0).Max(), 3),
                returned, furthest_x = furthest,
            });
            GD.Print("CITY_PERF " + line);
            return line;
        }

        internal async Task CaptureCityDefects()
        {
            try
            {
                CompleteVillageForScenario();
                _game._data.CityRoadOpen = true;
                _game._data.CityRoadworksCompleted = true;
                _game._data.CityRoadworksRewardClaimed = true;
                _game._roadworks.SetRoadState(true, false);
                _game._hud.Visible = false;
                _game._player.Teleport(new Vector3(170f, 0.5f, 0f), -Mathf.Pi / 2f);
                _game._rig.SetView(-90f, 20f, 9f);
                _game._rig.Snap();
                await Wait(1.2f);

                // Approach and city entrance: the west sidewalk strip crosses z=0.
                await SnapFreeCamera(new Vector3(170f, 4.2f, 9f), new Vector3(202f, 0.5f, 0f),
                    "p24_01_city_entry_side.png");
                await SnapFreeCamera(new Vector3(185f, 18f, 7f), new Vector3(198f, 0f, 0f),
                    "p24_02_city_entry_overhead.png");

                // Real van physics: launch east at full speed from the town side.
                _game.Probe.TeleportVan(new Vector3(170f, 0.4f, 0f), -Mathf.Pi / 2f);
                _game.Probe.LaunchVan(11.5f);
                for (int i = 0; i < 8; i++)
                {
                    await Wait(0.5f);
                    GD.Print($"CITY_DEFECT van_t{i} x={_game._van.GlobalPosition.X:0.00} " +
                             $"y={_game._van.GlobalPosition.Y:0.00} speed={_game._van.Speed:0.00}");
                }
                GD.Print($"CITY_DEFECT ground_185={_game.Probe.WorldRayHit(new Vector3(185f, 2f, 0f), new Vector3(185f, -1f, 0f), 1u)} " +
                         $"ground_190={_game.Probe.WorldRayHit(new Vector3(190f, 2f, 0f), new Vector3(190f, -1f, 0f), 1u)} " +
                         $"ground_200={_game.Probe.WorldRayHit(new Vector3(200f, 2f, 0f), new Vector3(200f, -1f, 0f), 1u)}");
                GD.Print($"CITY_DEFECT forward_190={_game.Probe.WorldRayHit(new Vector3(190f, 1f, 0f), new Vector3(205f, 1f, 0f), 1u)} " +
                         $"forward_191={_game.Probe.WorldRayHit(new Vector3(191f, 1f, 0f), new Vector3(205f, 1f, 0f), 1u)}");
                await SnapFreeCamera(_game._van.GlobalPosition + new Vector3(-7f, 5f, 9f),
                    _game._van.GlobalPosition + new Vector3(6f, 0.8f, 0f),
                    "p24_03_van_stopped_at_entry.png");

                // Overhead intersection: continuous sidewalk bands cross both roads.
                await SnapFreeCamera(new Vector3(CityLayout.WorldX(-40f), 26f, CityLayout.WorldZ(-16f)),
                    new Vector3(CityLayout.WorldX(-40f), 0f, CityLayout.WorldZ(-40f)),
                    "p24_04_intersection_overhead.png");

                // Activate the city actors near the inner route and record car poses.
                _game.Probe.TeleportPlayer(new Vector3(CityLayout.WorldX(-40f), 0.5f, CityLayout.WorldZ(-20f)), 0f);
                await Wait(2.2f);
                var before = _game._city.ActorPool.GetChildren().OfType<CityVehicleVisual>()
                    .Select(v => (Id: v.AgentId, Tier: v.Tier, Position: v.GlobalPosition,
                        Progress: v.Progress, Speed: v.Speed))
                    .ToList();
                foreach (var v in before)
                    GD.Print($"CITY_DEFECT car_before id={v.Id} tier={v.Tier} " +
                             $"pos=({v.Position.X:0.0},{v.Position.Y:0.0},{v.Position.Z:0.0}) " +
                             $"progress={v.Progress:0.00} speed={v.Speed:0.00}");

                await SnapFreeCamera(new Vector3(CityLayout.WorldX(-52f), 8f, CityLayout.WorldZ(-44f)),
                    new Vector3(CityLayout.WorldX(24f), 0.5f, CityLayout.WorldZ(-44f)),
                    "p24_05_sidewalk_crossing_cars.png");
                await Wait(4.0f);

                var after = _game._city.ActorPool.GetChildren().OfType<CityVehicleVisual>()
                    .Select(v => (Id: v.AgentId, Tier: v.Tier, Position: v.GlobalPosition,
                        Progress: v.Progress, Speed: v.Speed))
                    .ToList();
                foreach (var v in after)
                    GD.Print($"CITY_DEFECT car_after id={v.Id} tier={v.Tier} " +
                             $"pos=({v.Position.X:0.0},{v.Position.Y:0.0},{v.Position.Z:0.0}) " +
                             $"progress={v.Progress:0.00} speed={v.Speed:0.00}");

                var target = after
                    .Where(v => v.Tier == CityActorTier.Near)
                    .OrderBy(v => v.Position.DistanceTo(
                        new Vector3(CityLayout.WorldX(-26f), 0.5f, CityLayout.WorldZ(-44f))))
                    .FirstOrDefault();
                if (target.Position != Vector3.Zero)
                {
                    await SnapFreeCamera(target.Position + new Vector3(5.5f, 4.2f, 7f),
                        target.Position + new Vector3(0f, 0.7f, 0f),
                        "p24_06_stuck_city_car_close.png");
                }

                // Wide grid view: sidewalk bands and plaza slabs visibly cross roads.
                await SnapFreeCamera(new Vector3(CityLayout.WorldX(0f), 135f, CityLayout.WorldZ(210f)),
                    new Vector3(CityLayout.WorldX(0f), 0f, CityLayout.WorldZ(0f)),
                    "p24_07_city_grid_overview.png");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"City defect capture failed: {ex}");
            }
            _game.GetTree().Quit();
        }

        internal async Task CaptureCity()
        {
            try
            {
                CompleteVillageForScenario();
                _game._data.CompletedMissions.Add(RoadworksRules.MissionId);
                _game._data.SetMissionCounter(RoadworksRules.MissionId, RoadworksRules.SuppliesDeliveredKey, 1);
                _game._data.SetMissionCounter(RoadworksRules.MissionId, RoadworksRules.TaskDoneKey, 1);
                _game._data.CityRoadworksCompleted = true;
                _game._data.CityRoadworksRewardClaimed = true;
                _game._data.CityRoadOpen = false;
                _game._roadworks.SetRoadState(false, false);
                _game.RefreshMissionState();
                _game._hud.Visible = false;

                await SnapFreeCamera(new Vector3(80f, 3.6f, 7f), new Vector3(97f, 1.2f, 0f),
                    "p16_01_roadworks_closed.png");

                _game._data.CityRoadOpen = true;
                _game._roadworks.SetRoadState(true, false);
                await Wait(0.4f);
                await SnapFreeCamera(new Vector3(80f, 3.6f, 7f), new Vector3(97f, 1.2f, 0f),
                    "p16_02_roadworks_open.png");
                _game._player.Teleport(new Vector3(122f, 0.5f, 0f), -Mathf.Pi / 2f);
                await Wait(0.8f);
                await SnapFreeCamera(new Vector3(112f, 5.2f, 12f), new Vector3(150f, 0.6f, 0f),
                    "p16_03_city_approach.png");

                _game._player.Teleport(new Vector3(CityLayout.WorldX(-80f), 0.5f, CityLayout.WorldZ(-40f)),
                    Mathf.Pi);
                _game._rig.SetView(180f, 22f, 10f);
                await Wait(0.8f);
                await Snap("p16_04_city_street.png", Main.DeveloperCaptureDirectory);
                await SnapFreeCamera(new Vector3(CityLayout.WorldX(0f), 125f, CityLayout.WorldZ(285f)),
                    new Vector3(CityLayout.WorldX(0f), 0f, CityLayout.WorldZ(0f)),
                    "p16_05_city_hlod_overview.png");

                _game._player.Teleport(new Vector3(CityLayout.WorldX(0f), 0.5f, CityLayout.WorldZ(18f)),
                    Mathf.Pi);
                _game._rig.SetView(180f, 18f, 8.0f);
                await Wait(0.8f);
                await Snap("p16_06_city_jobs_plaza.png", Main.DeveloperCaptureDirectory);

                _game._hud.Visible = true;
                CityJobBoardUi.Show(_game._hud, _game._cityMissions, _ => { }, null);
                await Wait(0.5f);
                await Snap("p16_07_city_jobs_board.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.25f);

                _game._cityMissions.StartMission("C01");
                _game._cityMissions.LoadCourierParcels();
                _game._cityMissions.DeliverCourierStop(1);
                _game._cityMissions.DeliverCourierStop(2);
                _game.RefreshMissionState();
                await Wait(0.4f);
                await Snap("p16_08_city_courier_progress.png", Main.DeveloperCaptureDirectory);

                _game._player.Teleport(new Vector3(CityLayout.WorldX(80f), 0.5f, CityLayout.WorldZ(8f)),
                    0f);
                _game._rig.SetView(0f, 18f, 7.5f);
                await Wait(0.8f);
                await Snap("p16_09_city_cabinets.png", Main.DeveloperCaptureDirectory);
                _game._hud.Visible = false;
                _game._player.Teleport(new Vector3(30f, 0.5f, 0f), Mathf.Pi / 2f);
                await Wait(0.6f);
                await SnapFreeCamera(new Vector3(40f, 4.2f, 6.5f), new Vector3(12f, 1f, 0f),
                    "p16_10_return_town.png");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"City capture failed: {ex}");
            }
            _game.GetTree().Quit();
        }

    }
}
