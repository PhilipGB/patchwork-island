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
        internal async Task CaptureTitle()
        {
            try
            {
                await Wait(1.2f);
                await Snap("p2_00_title.png", Main.DeveloperCaptureDirectory);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Title capture failed: {ex}");
            }
            _game.GetTree().Quit();
        }

        internal async Task RunPerformanceProbe()
        {
            try
            {
                bool highRes = Main.HasUserArg("--perf-1080");
                if (highRes)
                {
                    _game.GetWindow().ContentScaleSize = new Vector2I(1920, 1080);
                    _game.GetWindow().Size = new Vector2I(1920, 1080);
                }
                await Wait(1.5f);
                var frameMs = new List<double>(600);
                ulong previous = Time.GetTicksUsec();
                for (int i = 0; i < 600; i++)
                {
                    await _game.ToSignal(_game.GetTree(), SceneTree.SignalName.ProcessFrame);
                    ulong now = Time.GetTicksUsec();
                    frameMs.Add((now - previous) / 1000.0);
                    previous = now;
                }
                frameMs.Sort();
                double mean = frameMs.Average();
                double p95 = frameMs[(int)(frameMs.Count * 0.95)];
                double max = frameMs[^1];
                GD.Print($"PERF resolution={(highRes ? "1920x1080" : "1280x720")} " +
                         $"mean_ms={mean:0.00} p95_ms={p95:0.00} max_ms={max:0.00} " +
                         $"frames={frameMs.Count} nodes={_game.GetTree().GetNodeCount()} " +
                         $"draw_calls={(int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} " +
                         $"primitives={(int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Performance probe failed: {ex}");
            }
            _game.GetTree().Quit();
        }

        internal async Task CaptureP12()
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
                _game._hud.Visible = false;
                string p = highRes ? "p12_1080" : "p12_720";

                await SnapFreeCamera(new Vector3(25, 60, 78), new Vector3(70, 0, 0), $"{p}_00_village_overview.png");
                await SnapFreeCamera(new Vector3(45, 48, 12), new Vector3(72, 0, 0), $"{p}_01_rally_overview.png");
                await SnapFreeCamera(new Vector3(48, 7, 4), new Vector3(57, 0, -8), $"{p}_02_start_line.png");
                await SnapFreeCamera(new Vector3(48, 7, -12), new Vector3(56.6f, 1, -26.6f), $"{p}_03_gate1.png");
                await SnapFreeCamera(new Vector3(55, 12, 20), new Vector3(64.6f, 1, 43.3f), $"{p}_04_gate4.png");
                await SnapFreeCamera(new Vector3(78, 6, 12), new Vector3(86, 0, 0), $"{p}_05_road_join.png");
                await SnapFreeCamera(new Vector3(18, 3, 0.5f), new Vector3(24, 1, 5), $"{p}_06_clothes_shop.png");
                await SnapFreeCamera(new Vector3(10, 16, 34), new Vector3(-8, 0, 0), $"{p}_07_village_roads.png");
                await SnapFreeCamera(new Vector3(22, 3, -0.5f), new Vector3(-18, 1, 2), $"{p}_08_town_traffic.png");
                _game._hud.Visible = true;
                JobBoardUi.Show(_game._hud, _game._missions, _game._implemented, _ => { });
                await Wait(0.5f);
                await Snap($"{p}_09_job_board.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"P12 capture failed: {ex}");
            }
            _game.GetTree().Quit();
        }

        internal async Task CaptureP13()
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
                string p = highRes ? "p13_1080" : "p13_720";

                _game.Probe.PrepareChapterTwo();
                _game._data.ActiveMission = "M11";
                _game._data.SetMissionCounter("M11", "stock_granted", 1);
                foreach (string npc in ChapterTwoRules.CardNpcIds)
                    _game._data.SetMissionCounter("M11", $"card_{npc}", 1);
                _game.RefreshMissionState();

                _game._hud.Visible = false;
                await SnapFreeCamera(new Vector3(30, 70, 30), new Vector3(10, 0, 8), $"{p}_00_village_chapter_two.png");
                await SnapFreeCamera(new Vector3(15.2f, 12f, 19f), new Vector3(15.2f, 1.5f, 5.2f), $"{p}_01_bakery_stall.png");
                await SnapFreeCamera(new Vector3(-14.5f, 5f, 2.5f), new Vector3(-20.8f, 1.5f, 8.2f), $"{p}_02_library_shelf.png");
                await SnapFreeCamera(new Vector3(22, 30, 25), new Vector3(15, 0, 2), $"{p}_03_road_sign_sockets.png");
                await SnapFreeCamera(new Vector3(29, 12, 33), new Vector3(29, 0.5f, 21), $"{p}_04_community_garden.png");
                await SnapFreeCamera(new Vector3(-1.5f, 3.5f, 10.5f), new Vector3(-4.2f, 0.7f, 7.0f), $"{p}_05_book_pickup.png");

                _game._hud.Visible = true;
                JobBoardUi.Show(_game._hud, _game._missions, _game._implemented, _ => { }, null, true);
                await Wait(0.5f);
                await Snap($"{p}_06_job_board_chapter_two.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.25f);

                _game.Probe.TalkToNpc("ada");
                await Wait(0.5f);
                await Snap($"{p}_07_ada_welcome_cards.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.25f);

                _game._data.ActiveMission = "M12";
                BakeryUi.Show(_game._hud, _game._missions.Community, _ => { }, () => { });
                await Wait(0.5f);
                await Snap($"{p}_08_bakery_ui.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.25f);

                _game._data.ActiveMission = "M13";
                foreach (string book in ChapterTwoRules.BookIds)
                    _game._data.SetMissionCounter("M13", ChapterTwoRules.HasBookKey(book), 1);
                LibraryShelfUi.Show(_game._hud, _game._missions.Community, _ => { }, () => { });
                await Wait(0.5f);
                await Snap($"{p}_09_library_ui.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.25f);

                _game._data.ActiveMission = "M14";
                SignSocketUi.Show(_game._hud, _game._missions.Community, "market", _ => { }, () => { });
                await Wait(0.5f);
                await Snap($"{p}_10_sign_ui.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.25f);

                _game._data.ActiveMission = "M15";
                _game._data.SetMissionCounter("M15", "pad0", ChapterTwoRules.GardenCode("red"));
                _game._data.SetMissionCounter("M15", "pad1", ChapterTwoRules.GardenCode("blue"));
                GardenPadUi.Show(_game._hud, _game._missions.Community, 0, _ => { }, () => { });
                await Wait(0.5f);
                await Snap($"{p}_11_garden_ui.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.25f);

                foreach (var socket in ChapterTwoRules.SignSockets)
                    _game._data.SetMissionCounter("M14", $"sign_{socket.SocketId}", 1);
                for (int i = 0; i < ChapterTwoRules.GardenPattern.Length; i++)
                    _game._data.SetMissionCounter("M15", $"pad{i}",
                        ChapterTwoRules.GardenCode(ChapterTwoRules.GardenPattern[i]));
                for (int i = 0; i < ChapterTwoRules.BakeryOrders.Count; i++)
                    _game._data.SetMissionCounter("M12", $"order_{ChapterTwoRules.BakeryOrders[i].CustomerId}", 1);
                foreach (string book in ChapterTwoRules.BookIds)
                    _game._data.SetMissionCounter("M13", ChapterTwoRules.ShelfKey(book), 1);
                _game.RefreshChapterTwoWorld();
                _game._hud.Visible = false;
                await SnapFreeCamera(new Vector3(22, 12, 10), new Vector3(20, 0, -4), $"{p}_12_repaired_signs.png");
                await SnapFreeCamera(new Vector3(29, 12, 33), new Vector3(29, 0.5f, 21), $"{p}_13_garden_complete.png");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"P13 capture failed: {ex}");
            }
            _game.GetTree().Quit();
        }

        internal async Task CaptureP11()
        {
            try
            {
                await Wait(1.2f);
                await CaptureNpcPose("ada", "p11_01_ada_market.png");
                await CaptureNpcPose("milo", "p11_02_milo_post.png");
                await CaptureNpcPose("pip", "p11_03_pip_workshop.png");
                await CaptureNpcPose("fern", "p11_04_fern_garden.png");
                await CaptureNpcPose("ollie", "p11_05_ollie_town_hall.png");
                await CaptureNpcPose("ivy", "p11_06_ivy_green.png");

                _game._player.Teleport(new Vector3(-15f, 0.1f, 7.5f), Mathf.Pi);
                _game._rig.SetView(180f, 20f, 9.5f);
                await Wait(1.6f);
                await Snap("p11_07_town_traffic.png", Main.DeveloperCaptureDirectory);

                _game.Probe.TeleportPlayerNearNpc("ada");
                await Wait(0.4f);
                _game.Probe.TalkToNpc("ada");
                await Wait(0.6f);
                await Snap("p11_08_ada_dialogue.png", Main.DeveloperCaptureDirectory);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"P11 capture failed: {ex}");
            }
            _game.GetTree().Quit();
        }

        internal async Task CaptureNpcPose(string npcId, string fileName)
        {
            if (!_game._npcById.TryGetValue(npcId, out var npc)) return;
            Vector3 npcPosition = npc.GlobalPosition;
            float offsetZ = npcId is "pip" or "fern" or "ivy" ? 5.0f : -3.8f;
            Vector3 playerPosition = new(npcPosition.X + 0.4f, 0.1f, npcPosition.Z + offsetZ);
            float yaw = Mathf.Atan2(-(npcPosition.X - playerPosition.X), -(npcPosition.Z - playerPosition.Z));
            _game._player.Teleport(playerPosition, yaw);
            _game._rig.SetView(Mathf.RadToDeg(yaw), 16f, 8.0f);
            await Wait(0.8f);
            await Snap(fileName, Main.DeveloperCaptureDirectory);
        }

        internal async Task CaptureTour()
        {
            try
            {
                await Wait(1.3f);
                await Snap("p2_01_start.png", Main.DeveloperCaptureDirectory);

                var overview = new Camera3D { Fov = 55, Current = true };
                _game.AddChild(overview);
                overview.GlobalPosition = new Vector3(-8f, 52f, 74f);
                overview.LookAt(new Vector3(8f, 0f, -2f), Vector3.Up);
                await Wait(0.7f);
                await Snap("p2_02_village_overview.png", Main.DeveloperCaptureDirectory);
                overview.QueueFree();
                _game._rig.Current = true;

                JobBoardUi.Show(_game._hud, _game._missions, _game._implemented, _ => { });
                await Wait(0.6f);
                await Snap("p2_03_job_board.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.4f);

                // Put the player near the market with M01 active for evidence.
                _game._missions.StartMission("M01");
                _game.RefreshMissionState();
                _game._player.Teleport(new Vector3(-4.4f, 0.2f, 1.1f), Mathf.Pi);
                _game._rig.SetView(180f, 18f, 8.2f);
                await Wait(0.8f);
                await Snap("p2_04_apple_crate.png", Main.DeveloperCaptureDirectory);

                // Simulate a full M01 hand-in for screenshot evidence.
                _game._missions.CollectPickup("apple_0", "apple", "M01", 3);
                _game._missions.CollectPickup("apple_1", "apple", "M01", 3);
                _game._missions.CollectPickup("apple_2", "apple", "M01", 3);
                _game.RefreshMissionState();
                _game._player.Teleport(new Vector3(-20f, 0.2f, -14.0f), 0f);
                _game._rig.SetView(0f, 18f, 8.2f);
                await Wait(0.7f);
                await Snap("p2_05_red_house_delivery.png", Main.DeveloperCaptureDirectory);

                var completion = _game._missions.DeliverApples();
                if (completion.Success)
                {
                    _game.OnMissionCompleted(completion);
                    await Wait(0.6f);
                    await Snap("p2_06_job_complete.png", Main.DeveloperCaptureDirectory);
                    _game._hud.CloseModal();
                    await Wait(0.4f);
                }

                _game._missions.StartMission("M02");
                _game.RefreshMissionState();
                _game._player.Teleport(new Vector3(-40f, 0.2f, -24.0f), 0f);
                _game._rig.SetView(0f, 20f, 9f);
                await Wait(0.7f);
                await Snap("p2_07_grove.png", Main.DeveloperCaptureDirectory);

                _game._player.Teleport(new Vector3(14f, 0.2f, -27.0f), 0f);
                _game._rig.SetView(0f, 20f, 9f);
                await Wait(0.7f);
                await Snap("p2_08_stone_patch.png", Main.DeveloperCaptureDirectory);

                _game._player.Teleport(new Vector3(25f, 0.2f, -6.0f), 0f);
                _game._rig.SetView(0f, 18f, 8.2f);
                await Wait(0.7f);
                await Snap("p2_09_workshop.png", Main.DeveloperCaptureDirectory);

                // P9 character and clothes-shop evidence.
                CharacterChoiceUi.Show(_game._hud, _ => { }, () => { });
                await Wait(0.7f);
                await Snap("p9_01_character_choice.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.4f);

                _game._data.CharacterPreset = CharacterCustomisation.Girl;
                _game._data.EquippedOutfit = CharacterCustomisation.DefaultOutfit;
                _game.ApplyCosmetics();
                _game._player.Teleport(new Vector3(24f, 0.2f, 0.6f), Mathf.Pi);
                _game._rig.SetView(180f, 18f, 8.2f);
                await Wait(0.8f);
                await Snap("p9_02_girl_at_clothes_shop.png", Main.DeveloperCaptureDirectory);

                ClothesShopUi.Show(_game._hud, _game._data, () =>
                {
                    _game.ApplyCosmetics();
                    _game._hud.SetCoins(_game._data.Coins);
                    return SaveResult.Ok();
                });
                await Wait(0.7f);
                await Snap("p9_03_clothes_shop_ui.png", Main.DeveloperCaptureDirectory);
                _game.Probe.PressModalButton("Outfit_sunny");
                await Wait(0.7f);
                await Snap("p9_04_outfit_owned_and_worn.png", Main.DeveloperCaptureDirectory);
                _game._hud.CloseModal();
                await Wait(0.4f);

                _game._player.Teleport(new Vector3(24f, 0.2f, 0.6f), Mathf.Pi);
                _game._rig.SetView(180f, 18f, 8.2f);
                await Wait(0.7f);
                await Snap("p9_05_girl_sunny_outfit_world.png", Main.DeveloperCaptureDirectory);

                _game._data.CharacterPreset = CharacterCustomisation.Boy;
                _game._data.EquippedOutfit = CharacterCustomisation.DefaultOutfit;
                _game.ApplyCosmetics();
                await Wait(0.5f);
                await Snap("p9_06_boy_default_outfit_world.png", Main.DeveloperCaptureDirectory);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Capture failed: {ex}");
            }
            _game.GetTree().Quit();
        }

    }
}
