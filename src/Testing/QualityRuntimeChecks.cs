using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using Godot;

namespace PatchworkIsland;

/// <summary>Input-boundary and native-object lifetime regressions; disposable test save only.</summary>
public partial class QualityRuntimeChecks : Node
{
    public Main Main { get; set; }
    private int _passed;
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _ = Run();
    }
    private static void SendInput(InputEvent input)
    {
        using (input) Input.ParseInputEvent(input);
    }

    private async Task Run()
    {
        try
        {
            await Wait();
            if (DisplayServer.GetName() == "headless")
                throw new InvalidOperationException("Quality input checks require a graphical display; use xvfb-run on Linux.");
            Main.Probe.StartNewGameFromTitleConfirmed("boy");
            await Wait();
            var rig = Main.GetNode<CameraRig>("CameraRig");
            var hud = Main.GetNode<Hud>("Hud");
            Main.Probe.Missions.StartMission("M01");
            Main.Probe.RefreshMissionState();
            await Wait(.7);
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                string folder = ProjectSettings.GlobalizePath("user://quality");
                Directory.CreateDirectory(folder);
                GetViewport().GetTexture().GetImage().SavePng(Path.Combine(folder, "spawn-shadows.png"));
            }
            var origin = Main.Probe.PlayerPosition;
            SendInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
            await Wait();
            Main.Probe.Pause();
            await Wait();
            SendInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false });
            await Wait();
            Main.Probe.Resume();
            await Wait();
            var before = rig.OrbitYaw;
            SendInput(new InputEventMouseMotion { Relative = new Vector2(250, 0) });
            await Wait();
            Check(Mathf.IsEqualApprox(rig.OrbitYaw, before), "mouse released during pause cannot orbit after resume");
            Check(Main.Probe.PlayerPosition.DistanceTo(origin) < 0.2f, "camera regression leaves player stationary");
            SendInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
            await Wait();
            SendInput(new InputEventMouseMotion { Relative = new Vector2(100, 0) });
            await Wait();
            Check(!Mathf.IsEqualApprox(rig.OrbitYaw, before), "held right mouse still orbits");
            SendInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false });
            Main.Probe.TeleportNearPickup("apple_0");
            var competitor = Interactable.Create(Main, Main.Probe.PlayerPosition, "E: Chat", () => { }, name: "PriorityCompetitor");
            await Wait();
            Check(Main.Probe.NearestInteractableName.StartsWith("Pickup_apple_"), "active pickup wins focus over a closer generic interaction");
            Main.Probe.Missions.StartMission("M02");
            Main.Probe.RefreshMissionState();
            await Wait();
            Check(Main.Probe.NearestInteractableName == "PriorityCompetitor", "available inactive pickups do not outrank nearer interactions");
            competitor.QueueFree();
            Main.Probe.Data.CompletedMissions.Add("M01");
            Main.Probe.Data.ActiveMission = "M04";
            Main.Probe.RefreshMissionState();
            Check(!Main.GetNode<Pickup>("Pickup_book_moon").IsAvailable(), "locked chapter-two pickup cannot take E focus");
            Check(!Main.GetNode<Pickup>("Pickup_book_moon").Visible, "locked book label is hidden");
            hud.SetPrompt("E: Pick up apple");
            Check(hud.PromptActionText == "Pick up apple", "HUD does not duplicate key in action text");
            GroceryUi.Show(hud, Main.Probe.Missions, () => { });
            hud.CloseModal();
            FurnitureShopUi.Show(hud, Main.Probe.Data, () => { }, () => { });
            hud.CloseModal();
            await Wait();
            double orphans = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            for (int i = 0; i < 30; i++)
            {
                GroceryUi.Show(hud, Main.Probe.Missions, () => { });
                hud.CloseModal();
                FurnitureShopUi.Show(hud, Main.Probe.Data, () => { }, () => { });
                hud.CloseModal();
                await Wait(0.02);
            }
            await Wait();
            Check(Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount) == orphans,
                "60 shop opens/closes do not increase orphan Node count");
            Main.Probe.SuppressSaves();
            Main.Probe.SetRoadState(true);
            foreach (var (offset, action) in new[]
            {
                (new Vector3(31, .02f, 0), "move_left"),
                (new Vector3(-31, .02f, 0), "move_right"),
                (new Vector3(0, .02f, -34), "move_back"),
                (new Vector3(0, .02f, 34), "move_forward"),
            })
            {
                var start = new Vector3(CityLayout.OriginX, 0, CityLayout.OriginZ) + offset;
                Main.Probe.ProbeCityLod(start, 0);
                Main.Probe.TeleportPlayer(start);
                await Wait(.8);
                var grounded = Main.Probe.PlayerPosition;
                Input.ActionPress(action);
                await Wait(2);
                Input.ActionRelease(action);
                var finish = Main.Probe.PlayerPosition;
                GD.Print($"CURB {action} from={grounded} to={finish}");
                Check(finish.DistanceTo(grounded) > 6f && finish.Y > .15f,
                    "grounded player walks onto city plaza via " + action + " without jumping");
            }
            var cityStart = Main.Probe.CityMissions.StartMission("C02");
            var cityCollect = Main.Probe.CityMissions.CollectMarketCrate(0);
            var consumedCrate = GetTree().GetNodesInGroup("interactable").OfType<Interactable>()
                .Single(station => station.Name == "CityCrateStation_0");
            Check(cityStart.Success && cityCollect.Success && !consumedCrate.IsAvailable(),
                "collected city crates cannot keep stealing interaction focus");
            var tall = Geo.StaticBox(Main, "QualityTallWall", new Vector3(480, .5f, -156), new Vector3(3, 1, 1), Palette.Stone);
            var low = Geo.StaticBox(Main, "QualityLowStep", new Vector3(486, .15f, -156), new Vector3(3, .3f, 1), Palette.Stone);
            var ceiling = Geo.StaticBox(Main, "QualityCeiling", new Vector3(486, 1.8f, -156), new Vector3(3, .3f, 3), Palette.Stone);
            foreach (float x in new[] { 480f, 486f })
            {
                var start = new Vector3(x, .02f, -153);
                Main.Probe.ProbeCityLod(start, 0);
                Main.Probe.TeleportPlayer(start);
                await Wait(.8);
                Input.ActionPress("move_forward");
                await Wait(2);
                Input.ActionRelease("move_forward");
                var finish = Main.Probe.PlayerPosition;
                GD.Print($"STEP SAFETY x={x} finish={finish}");
                Check(finish.Z > -155.3f && finish.Y < .15f,
                    x == 480f ? "small-step assist cannot climb a one-metre wall" : "small-step assist rejects blocked headroom");
            }
            tall.QueueFree(); low.QueueFree(); ceiling.QueueFree();
            // Academy graphical input/UI lifecycle on the actual rendered venue.
            var academy = Main.RtAcademy;
            var academyService = Main.RtSession.Academy;
            Main.Probe.Data.Coins = 15;
            var world = Main.GetNode<WorldSceneAdapter>("Village");
            Main.Probe.TeleportPlayer(world.SchoolMarker("EntranceApproach").GlobalPosition +
                new Vector3(0, 0.1f, -1.2f), Mathf.Pi);
            await Wait(.6);
            Main.RtOpenDrivingSchool();
            await Wait(.4);
            Check(Main.Probe.ModalTitle == "DRIVING SCHOOL" &&
                  Main.Probe.FindModalButton("AcademyEnrol") != null,
                "academy office renders its enrolment UI");
            Main.Probe.PressModalButton("AcademyEnrol");
            await Wait(.5);
            Check(academyService.Enrolled, "academy enrolment click is accepted");
            Main.Probe.PressModalButton("AcademyStart_L01");
            await Wait(.8);
            Check(academy.Active && academy.TrainingCar != null,
                "academy lesson renders the learner car and HUD");
            academy.TestPlaceCar(academy.TestMarker("Lesson01_Accelerate"),
                academy.TestMarkerYaw("Lesson01_Accelerate"), 3f);
            await Wait(.3);
            academy.TestPlaceCar(academy.TestMarker("Lesson01_Stop"),
                academy.TestMarkerYaw("Lesson01_Stop"), 0f);
            await Wait(1.8);
            Check(academyService.LessonCompleted("L01"),
                "academy lesson completes through the rendered scene");
            Main.Probe.PressModalButton("AcademyBackToSchool");
            await Wait(.6);
            Check(!academy.Active && Main.Probe.PlayerVisible,
                "academy returns to the walking town without an orphaned venue");
            if (Main.Probe.ModalOpen) Main.Probe.CloseModal();
            await Wait(.3);

            var audio = Main.GetNode<AudioManager>("AudioManager");
            audio.PlayMusic("rally", .3f);
            audio.StopAll();
            await Wait(.7);
            Check(!audio.IsMusicPlaying && audio.CurrentMusicId == "",
                "stopping audio cancels a pending music transition");
            GD.Print($"QUALITY RESULT: {_passed} passed, 0 failed");
            GetTree().Quit();
        }
        catch (Exception ex)
        {
            GD.PrintErr("QUALITY FAIL: " + ex);
            GetTree().Quit(1);
        }
    }
    private void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        _passed++;
        GD.Print("PASS  " + message);
    }
    private async Task Wait(double seconds = 0.3) =>
        await ToSignal(GetTree().CreateTimer(seconds, processAlways: true), SceneTreeTimer.SignalName.Timeout);
}
