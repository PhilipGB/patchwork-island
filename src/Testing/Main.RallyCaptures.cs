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
        internal async Task CaptureP14()
        {
            try
            {
                await Wait(1.0f);
                bool highRes = Main.HasUserArg("--capture-1080");
                if (highRes)
                {
                    _game.GetWindow().ContentScaleSize = new Vector2I(1920, 1080);
                    _game.GetWindow().Size = new Vector2I(1920, 1080);
                    await Wait(0.6f);
                }
                string p = highRes ? "p14_1080" : "p14_720";
                _game.Probe.PrepareFinale();

                // M16 dialogue and post counter.
                _game._data.ActiveMission = "M16";
                _game._data.SetMissionCounter("M16", "loaded", 1);
                foreach (string npc in FinaleRules.DeliveryRecipients)
                    _game._data.SetMissionCounter("M16", FinaleRules.CarryingKey(npc), 1);
                _game.RefreshMissionState();
                _game._hud.Visible = true;
                _game.Probe.TalkToNpc("ada");
                await Wait(0.5f);
                await Snap($"{p}_00_delivery_dialogue.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.25f);

                _game._hud.Visible = false;
                await SnapFreeCamera(new Vector3(-14.5f, 14f, 2f), new Vector3(-14.5f, 1f, 6.5f),
                    $"{p}_01_post_counter.png");
                _game._data.CompletedMissions.Add("M16");
                foreach (string npc in FinaleRules.DeliveryRecipients)
                {
                    _game._data.SetMissionCounter("M16", FinaleRules.CarryingKey(npc), 0);
                    _game._data.SetMissionCounter("M16", FinaleRules.DeliveredKey(npc), 1);
                }
                _game.RefreshMissionState();
                await SnapFreeCamera(new Vector3(-2.5f, 4.5f, -1f), new Vector3(-2.5f, 1f, 4.7f),
                    $"{p}_02_delivery_board.png");

                // M17 checkout and market stock.
                _game._data.ActiveMission = "M17";
                _game._data.SetMissionCounter("M17", "bread", 2);
                _game._data.SetMissionCounter("M17", "milk", 1);
                _game._data.SetMissionCounter("M17", "apple", 2);
                _game._hud.Visible = true;
                MarketMathsUi.Show(_game._hud, _game._missions.Finale, _ => { }, () => { });
                await Wait(0.5f);
                await Snap($"{p}_03_market_maths_ui.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.25f);
                _game._data.CompletedMissions.Add("M17");
                _game._data.SetMissionCounter("M17", "finalised", 1);
                _game._data.ActiveMission = "";
                _game.RefreshMissionState();
                _game._hud.Visible = false;
                await SnapFreeCamera(new Vector3(9.2f, 1.7f, 0f), new Vector3(9.2f, 1.3f, 3.8f),
                    $"{p}_04_market_stock.png");

                // M18 broken car, repair bench, repaired car.
                _game._data.ActiveMission = "M18";
                _game._data.SetMissionCounter("M18", "inspected", 1);
                _game._data.SetMissionCounter("M18", "has_tyre", 2);
                _game._data.SetMissionCounter("M18", "has_lamp", 1);
                _game._data.SetMissionCounter("M18", "has_battery", 1);
                _game._data.SetMissionCounter("M18", "tyres_fitted", 1);
                _game.RefreshMissionState();
                _game._hud.Visible = true;
                RepairUi.Show(_game._hud, _game._missions.Finale, _ => { }, () => { });
                await Wait(0.5f);
                await Snap($"{p}_05_repair_ui.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.25f);
                _game._hud.Visible = false;
                await SnapFreeCamera(new Vector3(32f, 7f, -0.5f), new Vector3(35.2f, 1f, -7.8f),
                    $"{p}_06_broken_pip_car.png");
                _game._data.CompletedMissions.Add("M18");
                _game._data.SetMissionCounter("M18", "tyres_fitted", 2);
                _game._data.SetMissionCounter("M18", "lamp_fitted", 1);
                _game._data.SetMissionCounter("M18", "battery_fitted", 1);
                _game._data.ActiveMission = "";
                _game.RefreshMissionState();
                await SnapFreeCamera(new Vector3(32f, 7f, -0.5f), new Vector3(35.2f, 1f, -7.8f),
                    $"{p}_07_repaired_pip_car.png");

                // M19 six-gate circuit with Pip's rival car.
                _game._data.ActiveMission = "M19";
                _game.RefreshMissionState();
                _game._van.GlobalPosition = new Vector3(56f, 0.15f, -6f);
                _game._van.Rotation = Vector3.Zero;
                _game.RefreshMissionState();
                await Wait(0.4f);
                _game._hud.Visible = false;
                await SnapFreeCamera(new Vector3(74f, 42f, 4f), new Vector3(70f, 0f, 0f),
                    $"{p}_08_grand_rally_six_gates.png");
                _game._data.CompletedMissions.Add("M19");
                _game._data.SetMissionCounter("M19", "checkpoint", FinaleRules.RallyGateCount);
                _game._data.SetMissionCounter("M19", "started", 1);
                _game._data.SetMissionCounter("M19", "elapsedMs", 22_300);
                _game._data.ActiveMission = "";
                _game.RefreshMissionState();

                // M20 festival pads and second finale.
                foreach (string id in new[] { "M16", "M17", "M18" })
                    if (!_game._data.CompletedMissions.Contains(id)) _game._data.CompletedMissions.Add(id);
                _game._data.ActiveMission = "M20";
                foreach (string npc in FinaleRules.FestivalInvitees)
                    _game._data.SetMissionCounter("M20", FinaleRules.InvitedKey(npc), 1);
                _game._data.SetMissionCounter("M20", "table_0", 1);
                _game._data.SetMissionCounter("M20", "table_1", 1);
                _game._data.SetMissionCounter("M20", "lantern_0", 1);
                _game._data.SetMissionCounter("M20", "lantern_1", 1);
                _game._data.SetMissionCounter("M20", "lantern_2", 1);
                _game.RefreshMissionState();
                _game._hud.Visible = false;
                await SnapFreeCamera(new Vector3(15f, 9f, 31f), new Vector3(15f, 1f, 22f),
                    $"{p}_09_festival_setup.png");

                _game._data.CompletedMissions.Add("M20");
                _game._data.ActiveMission = "";
                _game.RefreshMissionState();
                _game._hud.Visible = false;
                await SnapFreeCamera(new Vector3(15f, 9f, 31f), new Vector3(15f, 1f, 22f),
                    $"{p}_10_festival_decor.png");

                _game._hud.Visible = true;
                JobBoardUi.Show(_game._hud, _game._missions, _game._implemented, _ => { }, null, true);
                await Wait(0.5f);
                await Snap($"{p}_11_job_board_all_complete.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.25f);
                _game._data.FestivalSeen = false;
                _game.ShowVillageFestival();
                await Wait(0.5f);
                await Snap($"{p}_12_village_festival.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.25f);

                _game.Probe.BuildBridgeNow();
                _game._hud.Visible = false;
                await SnapFreeCamera(new Vector3(35f, 8f, 12f), new Vector3(45f, 1f, 0f),
                    $"{p}_13_bridge_approach.png");
                await SnapFreeCamera(new Vector3(63f, 6f, -21f), new Vector3(56.8f, 2f, -27.6f),
                    $"{p}_14_gate_one.png");

                // Bridge/road seam from the driving viewpoint and above.
                _game._van.GlobalPosition = new Vector3(52f, 0.15f, 0f);
                _game._van.Rotation = new Vector3(0f, -Mathf.Pi / 2f, 0f);
                await Wait(0.3f);
                await SnapFreeCamera(new Vector3(44f, 5.5f, 1.6f),
                    new Vector3(54f, 0.4f, -0.5f), $"{p}_15_bridge_seam.png");
                await SnapFreeCamera(new Vector3(50f, 14f, 9f),
                    new Vector3(52f, 0f, -2f), $"{p}_16_bridge_junction.png");

                // Player cap/hair silhouette from behind.
                _game._player.SetAppearance(CharacterCustomisation.Girl, "berry");
                _game._player.Teleport(new Vector3(10f, 0.1f, 30f), 0f);
                await Wait(0.4f);
                await SnapFreeCamera(new Vector3(10f, 1.72f, 32.2f),
                    new Vector3(10f, 1.55f, 30f), $"{p}_17_player_cap.png");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"P14 capture failed: {ex}");
            }
            _game.GetTree().Quit();
        }

        internal async Task SnapFreeCamera(Vector3 position, Vector3 lookAt, string fileName)
        {
            var camera = new Camera3D { Fov = 62f, Current = true };
            _game.AddChild(camera);
            camera.GlobalPosition = position;
            camera.LookAt(lookAt, Vector3.Up);
            await Wait(0.35f);
            await Snap(fileName, Main.DeveloperCaptureDirectory);
            camera.QueueFree();
        }

    }
}
