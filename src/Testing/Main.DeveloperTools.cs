using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

public partial class Main
{
    private DeveloperTools _developerTools;
    private DeveloperTools Dev => _developerTools ??= new DeveloperTools(this);

    /// <summary>Opt-in scenarios and diagnostic captures; not part of game progression.</summary>
    private sealed partial class DeveloperTools
    {
        private readonly Main _game;
        internal DeveloperTools(Main game) => _game = game;
        internal bool TryLaunch()
        {
            if (Main.HasUserArg("--scenario-m03"))
            {
                BeginScenario(new Vector3(34.2f, 0.2f, 4.2f), -Mathf.Pi / 2f,
                    "SCENARIO: M01+M02 done, Build the Bridge active. Press E at the worksite.");
                return true;
            }
            if (Main.HasUserArg("--scenario-m04"))
            {
                BeginScenario(new Vector3(5f, 0.2f, 0.8f), Mathf.Pi,
                    "SCENARIO: M01 done, Market Basket active. Press E at the SHOP.");
                return true;
            }
            if (Main.HasUserArg("--scenario-m05"))
            {
                BeginScenario(new Vector3(-31f, 0.2f, 18.5f), 0f,
                    "SCENARIO: M05 active. Drive the yellow van through gates 1-2-3-4.", 5f);
                return true;
            }
            if (Main.HasUserArg("--scenario-m06"))
            {
                BeginScenario(new Vector3(-9f, 0.2f, 4.5f), Mathf.Pi,
                    "SCENARIO: M06 active. Collect BLUE and ORANGE parcels at the post counter.", 5f);
                return true;
            }
            if (Main.HasUserArg("--scenario-m07"))
            {
                BeginScenario(new Vector3(-38f, 0.2f, -4.0f), Mathf.Pi,
                    "SCENARIO: M07 active. Press E by the broken swings.", 5f);
                return true;
            }
            if (Main.HasUserArg("--scenario-m08"))
            {
                BeginScenario(new Vector3(10f, 0.2f, 7.8f), Mathf.Pi,
                    "SCENARIO: M08 active. Press E at the village green.", 5f);
                return true;
            }
            if (Main.HasUserArg("--scenario-m09"))
            {
                _game._van.GlobalPosition = new Vector3(56f, 0.15f, -2.0f);
                _game._van.Rotation = new Vector3(0, 0, 0);
                BeginScenario(new Vector3(52.8f, 0.2f, 2.0f), 0f,
                    "SCENARIO: M09 active on the new long circuit. Drive gates 1-2-3-4; under 75 s is an optional bonus.", 5f);
                return true;
            }
            if (Main.HasUserArg("--scenario-m10"))
            {
                BeginScenario(new Vector3(-38f, 0.2f, 17.0f), Mathf.Pi,
                    "SCENARIO: M10 active. Walk into your house and press E at FURNITURE.", 5f);
                return true;
            }
            if (Main.HasUserArg("--scenario-final"))
            {
                BeginScenario(new Vector3(-38f, 0.2f, 21.0f), Mathf.Pi,
                    "SCENARIO: final job active. Furnish the house to trigger the village celebration.", 5f);
                return true;
            }
            if (Main.HasUserArg("--scenario-roadworks"))
            {
                CompleteVillageForScenario();
                BeginScenario(new Vector3(78f, 0.5f, 5.0f), -Mathf.Pi / 2f,
                    "SCENARIO: all 20 village jobs complete. Roadworks Rescue is ready at the closed gate.", 6f);
                return true;
            }
            if (Main.HasUserArg("--scenario-city"))
            {
                CompleteVillageForScenario();
                _game._data.CompletedMissions.Add(RoadworksRules.MissionId);
                _game._data.SetMissionCounter(RoadworksRules.MissionId, RoadworksRules.SuppliesDeliveredKey, 1);
                _game._data.SetMissionCounter(RoadworksRules.MissionId, RoadworksRules.TaskDoneKey, 1);
                _game._data.CityRoadworksCompleted = true;
                _game._data.CityRoadworksRewardClaimed = true;
                _game._data.CityRoadOpen = true;
                _game._roadworks.SetRoadState(true, false);
                _game._player.Teleport(new Vector3(CityLayout.WorldX(0f), 0.5f, CityLayout.WorldZ(28f)),
                    0f);
                _game._rig.SetView(0f, 20f, 9.5f);
                _game._started = true;
                _game.SyncControlMode();
                _game.RefreshMissionState();
                _game._hud.ShowBigToast("SCENARIO: Patchwork City central plaza. The teal CITY JOBS board is behind you.", 6f, Palette.Teal);
                return true;
            }
            if (Main.HasUserArg("--capture-city"))
            {
                _game._started = true;
                _game._hud.CloseModal();
                _game._player.SetControlEnabled(false);
                _game.SyncControlMode();
                _ = CaptureCity();
                return true;
            }
            if (Main.HasUserArg("--capture-city-defects"))
            {
                _game._started = true;
                _game._hud.CloseModal();
                _game._hud.Visible = false;
                _game._player.SetControlEnabled(false);
                _game.SyncControlMode();
                _ = CaptureCityDefects();
                return true;
            }
            if (Main.HasUserArg("--capture-title"))
            {
                _game._hud.ShowTitleScreen(false, false, null, _game.StartAdventure, null, _game.QuitGame);
                _ = CaptureTitle();
                return true;
            }
            if (Main.HasUserArg("--capture"))
            {
                _game._started = true;
                _game._player.SetControlEnabled(false);
                _ = CaptureTour();
                return true;
            }
            if (Main.HasUserArg("--capture-p11"))
            {
                _game._started = true;
                _game._hud.CloseModal();
                _game._player.SetControlEnabled(false);
                _ = CaptureP11();
                return true;
            }
            if (Main.HasUserArg("--capture-p12"))
            {
                _game._started = true;
                _game._hud.CloseModal();
                _game._player.SetControlEnabled(false);
                _ = CaptureP12();
                return true;
            }
            if (Main.HasUserArg("--capture-p13"))
            {
                _game._started = true;
                _game._hud.CloseModal();
                _game._player.SetControlEnabled(false);
                _ = CaptureP13();
                return true;
            }
            if (Main.HasUserArg("--capture-p14"))
            {
                _game._started = true;
                _game._hud.CloseModal();
                _game._player.SetControlEnabled(false);
                _ = CaptureP14();
                return true;
            }
            if (Main.HasUserArg("--perf-probe"))
            {
                _game._started = true;
                _game._hud.CloseModal();
                _game._player.SetControlEnabled(false);
                _ = RunPerformanceProbe();
                return true;
            }
            if (Main.HasUserArg("--city-probe"))
            {
                _game._started = true;
                _game._hud.CloseModal();
                _game._player.SetControlEnabled(false);
                _game.SyncControlMode();
                _ = RunCityProbe();
                return true;
            }
            if (Main.HasUserArg("--city-perf"))
            {
                _game._started = true;
                _game._hud.CloseModal();
                _game._player.SetControlEnabled(false);
                _game.SyncControlMode();
                _ = RunCityPerformanceProbe();
                return true;
            }
            if (Main.IsCityTest)
            {
                _game._started = true;
                _game._hud.CloseModal();
                _game.SyncControlMode();
                var cityTest = new CityRuntimeSelfTest { Name = "CityRuntimeSelfTest", Main = _game };
                _game.AddChild(cityTest);
                return true;
            }
            return false;
        }
        internal void BeginScenario(Vector3 position, float yaw, string message, float seconds = 4.5f)
        {
            _game._started = true;
            _game._player.Teleport(position, yaw);
            _game._rig.SetView(Mathf.RadToDeg(yaw), 18f, 8.2f);
            _game.SyncControlMode();
            _game.RefreshMissionState();
            _game._hud.ShowBigToast(message, seconds);
        }

        /// <summary>
        /// Developer/capture setup only. It marks the required chain complete so a
        /// screenshot or probe can reach the city without replaying twenty jobs;
        /// it is never used by the normal save/migration path.
        /// </summary>
        internal void CompleteVillageForScenario()
        {
            _game._suppressScenarioSaves = true;
            for (int i = 1; i <= 20; i++)
            {
                string id = $"M{i:00}";
                if (!_game._data.CompletedMissions.Contains(id))
                    _game._data.CompletedMissions.Add(id);
            }
            _game._data.CelebrationSeen = true;
            _game._data.FestivalSeen = true;

        }


        internal void ApplyDeveloperScenario()
        {
            if (!Main.DeveloperMode) return;
            if (Main.HasUserArg("--scenario-m03"))
            {
                if (!_game._data.CompletedMissions.Contains("M01")) _game._data.CompletedMissions.Add("M01");
                if (!_game._data.CompletedMissions.Contains("M02")) _game._data.CompletedMissions.Add("M02");
                _game._data.Coins = 22;
                _game._data.ActiveMission = "M03";
                if (!_game._data.UnlockedCosmetics.Contains("red_cap")) _game._data.UnlockedCosmetics.Add("red_cap");
            }
            else if (Main.HasUserArg("--scenario-m04"))
            {
                if (!_game._data.CompletedMissions.Contains("M01")) _game._data.CompletedMissions.Add("M01");
                _game._data.Coins = 10;
                _game._data.ActiveMission = "M04";
                if (!_game._data.UnlockedCosmetics.Contains("red_cap")) _game._data.UnlockedCosmetics.Add("red_cap");
            }
            else if (Main.HasUserArg("--scenario-m05"))
            {
                foreach (string id in new[] { "M01", "M02", "M03", "M04" })
                    if (!_game._data.CompletedMissions.Contains(id)) _game._data.CompletedMissions.Add(id);
                _game._data.BridgePieces.Clear();
                _game._data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
                _game._data.Coins = 54;
                _game._data.ActiveMission = "M05";
                if (!_game._data.UnlockedCosmetics.Contains("red_cap")) _game._data.UnlockedCosmetics.Add("red_cap");
            }
            else if (Main.HasUserArg("--scenario-m06"))
            {
                foreach (string id in new[] { "M01", "M02", "M03", "M04" })
                    if (!_game._data.CompletedMissions.Contains(id)) _game._data.CompletedMissions.Add(id);
                _game._data.BridgePieces.Clear();
                _game._data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
                _game._data.Coins = 54;
                _game._data.ActiveMission = "M06";
                if (!_game._data.UnlockedCosmetics.Contains("red_cap")) _game._data.UnlockedCosmetics.Add("red_cap");
            }
            else if (Main.HasUserArg("--scenario-m07"))
            {
                foreach (string id in new[] { "M01", "M02", "M03", "M04" })
                    if (!_game._data.CompletedMissions.Contains(id)) _game._data.CompletedMissions.Add(id);
                _game._data.BridgePieces.Clear();
                _game._data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
                _game._data.Coins = 54;
                _game._data.ActiveMission = "M07";
                if (!_game._data.UnlockedCosmetics.Contains("red_cap")) _game._data.UnlockedCosmetics.Add("red_cap");
            }
            else if (Main.HasUserArg("--scenario-m08"))
            {
                foreach (string id in new[] { "M01", "M02", "M03", "M04", "M07" })
                    if (!_game._data.CompletedMissions.Contains(id)) _game._data.CompletedMissions.Add(id);
                _game._data.BridgePieces.Clear();
                _game._data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
                _game._data.SwingPieces.Clear();
                _game._data.SwingPieces.AddRange(BuildRules.SwingSockets);
                _game._data.Coins = 72;
                _game._data.ActiveMission = "M08";
                if (!_game._data.UnlockedCosmetics.Contains("red_cap")) _game._data.UnlockedCosmetics.Add("red_cap");
            }
            else if (Main.HasUserArg("--scenario-m09"))
            {
                foreach (string id in new[] { "M01", "M02", "M03", "M04", "M05" })
                    if (!_game._data.CompletedMissions.Contains(id)) _game._data.CompletedMissions.Add(id);
                _game._data.BridgePieces.Clear();
                _game._data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
                _game._data.Coins = 69;
                _game._data.ActiveMission = "M09";
                if (!_game._data.UnlockedCosmetics.Contains("red_cap")) _game._data.UnlockedCosmetics.Add("red_cap");
            }
            else if (Main.HasUserArg("--scenario-final"))
            {
                foreach (string id in new[] { "M01", "M02", "M03", "M04", "M05", "M06", "M07", "M08", "M09" })
                    if (!_game._data.CompletedMissions.Contains(id)) _game._data.CompletedMissions.Add(id);
                _game._data.BridgePieces.Clear();
                _game._data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
                _game._data.SwingPieces.Clear();
                _game._data.SwingPieces.AddRange(BuildRules.SwingSockets);
                _game._data.ParkPlacements.Clear();
                _game._data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
                _game._data.ParkPlacements.Add(new PlacedItem("flowerbed", "park1"));
                _game._data.ParkPlacements.Add(new PlacedItem("lamp", "park2"));
                _game._data.Coins = 137;
                _game._data.ActiveMission = "M10";
                if (!_game._data.UnlockedCosmetics.Contains("red_cap")) _game._data.UnlockedCosmetics.Add("red_cap");
            }
            else if (Main.HasUserArg("--scenario-m10"))
            {
                foreach (string id in new[] { "M01", "M02", "M03", "M04", "M07", "M08" })
                    if (!_game._data.CompletedMissions.Contains(id)) _game._data.CompletedMissions.Add(id);
                _game._data.BridgePieces.Clear();
                _game._data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
                _game._data.SwingPieces.Clear();
                _game._data.SwingPieces.AddRange(BuildRules.SwingSockets);
                _game._data.ParkPlacements.Clear();
                _game._data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
                _game._data.ParkPlacements.Add(new PlacedItem("flowerbed", "park1"));
                _game._data.ParkPlacements.Add(new PlacedItem("lamp", "park2"));
                _game._data.Coins = 92;
                _game._data.ActiveMission = "M10";
                if (!_game._data.UnlockedCosmetics.Contains("red_cap")) _game._data.UnlockedCosmetics.Add("red_cap");
            }
        }

        internal async Task Wait(float seconds)
        {
            await _game.ToSignal(_game.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        }

        internal void CaptureManualScreenshotSafely()
        {
            _ = CaptureManualScreenshotAsync();
        }

        internal async Task CaptureManualScreenshotAsync()
        {
            try
            {
                GD.Print($"PLAYER_POS {_game._player.GlobalPosition} ACTIVE={_game._started} PAUSED={_game.GetTree().Paused} CONTROL={_game._player.ControlEnabled} VEL={_game._player.Velocity} TARGET={_game._player.NearestInteractable?.Name} DRIVING={_game._driving} VAN_POS={_game._van.GlobalPosition} VAN_YAW={_game._van.Rotation.Y:0.00} VAN_SPEED={_game._van.Speed:0.0} NODES={_game.GetTree().GetNodeCount()} MODE={_game._controlMode}");
                await Wait(0.15f);
                await Snap($"manual_{DateTime.Now:HHmmss}.png", Main.ScreenshotDirectory);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Screenshot failed: {ex}");
            }
        }

        internal async Task Snap(string fileName, string directory)
        {
            await _game.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var viewport = _game.GetViewport();
            if (viewport == null) throw new InvalidOperationException("No viewport is available for a screenshot.");
            var image = viewport.GetTexture()?.GetImage();
            if (image == null) throw new InvalidOperationException("The viewport image is unavailable.");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            Error error = image.SavePng(path);
            if (error != Error.Ok)
                GD.PrintErr($"SCREENSHOT FAILED {path} -> {error}");
            else
                GD.Print($"SCREENSHOT {path} -> {error}");
        }
    }
}
