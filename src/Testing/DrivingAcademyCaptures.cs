using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>Developer-only 720p/1080p academy screenshot tour.</summary>
public partial class DrivingAcademyCaptures : Node
{
    public Main Main { get; set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _ = Run();
    }

    private async Task Run()
    {
        try
        {
            bool high = OS.GetCmdlineUserArgs().Contains("--capture-1080");
            if (high)
            {
                Main.GetWindow().ContentScaleSize = new Vector2I(1920, 1080);
                Main.GetWindow().Size = new Vector2I(1920, 1080);
                await Wait(0.8f);
            }
            string prefix = high ? "academy_1080" : "academy_720";
            string folder = ProjectSettings.GlobalizePath("user://screenshots");
            Directory.CreateDirectory(folder);

            Main.Probe.RequestNewGameFromTitle();
            await Wait(0.3f);
            Main.Probe.PressModalButton("Choose_boy");
            await Wait(0.8f);
            var data = Main.Probe.Data;
            var service = Main.RtSession.Academy;
            GD.Print($"CAPTURE STATE legacy={data.LegacyVanPermit} licence={data.DrivingLicenceIssued} schema={data.SchemaVersion}");
            data.Coins = 15;
            var world = Main.GetNode<WorldSceneAdapter>("Village");
            Vector3 school = world.SchoolMarker("EntranceApproach").GlobalPosition;
            Main.Probe.TeleportPlayer(school + new Vector3(0f, 0.1f, -2f), Mathf.Pi);
            await Wait(0.5f);
            await Snap(folder, $"{prefix}_01_school_office.png");

            Main.RtOpenDrivingSchool();
            await Wait(0.4f);
            await Snap(folder, $"{prefix}_02_enrolment_ui.png");

            service.Enrol();
            Main.RtOpenDrivingSchool();
            await Wait(0.4f);
            await Snap(folder, $"{prefix}_03_lesson_board.png");
            Main.Probe.CloseModal();

            service.StartLesson("L01");
            Main.RtAcademy.EnterStage("L01", false);
            await Wait(0.7f);
            await Snap(folder, $"{prefix}_04_lesson01.png");
            service.CompleteLesson("L01");

            // Complete the six lessons in the domain, then show authored rule scenes.
            foreach (string id in new[] { "L02", "L03", "L04", "L05", "L06" })
            {
                Main.RtAcademy.ExitVenue(false);
                var started = service.StartLesson(id);
                if (!started.Success) throw new InvalidOperationException(started.Message);
                Main.RtAcademy.EnterStage(id, false);
                await Wait(0.25f);
                var done = service.CompleteLesson(id);
                if (!done.Success) throw new InvalidOperationException(done.Message);
            }
            Main.RtAcademy.ExitVenue(false);
            service.StartLesson("L04");
            Main.RtAcademy.EnterStage("L04", false);
            Main.RtAcademy.TestSetLight(AcademyLight.Red);
            Main.RtAcademy.TestPlaceCar(Main.RtAcademy.TestMarker("Lesson04_TrafficLight") +
                new Vector3(0, 0, -13), 180f, 0f);
            await Wait(0.6f);
            await SnapFree(folder, $"{prefix}_05_road_rules.png",
                new Vector3(-16, 8, -30), new Vector3(4, 1.5f, -8));
            Main.RtAcademy.TestSetLight(AcademyLight.Green);
            await Wait(0.3f);
            await SnapFree(folder, $"{prefix}_09_crossing_pedestrian.png",
                new Vector3(12, 6, -12), new Vector3(22, 1, 0));

            Main.RtAcademy.ExitVenue(false);
            service.StartLesson("L05");
            Main.RtAcademy.EnterStage("L05", false);
            Main.RtAcademy.TestPlaceCar(Main.RtAcademy.TestMarker("Lesson05_ParkingTarget"), 180f, 0f);
            await Wait(0.6f);
            await Snap(folder, $"{prefix}_06_parking.png");

            Main.RtAcademy.ExitVenue(false);
            service.StartLesson("TEST01");
            Main.RtAcademy.EnterStage("TEST01", false);
            Main.RtAcademy.TestPlaceCar(Main.RtAcademy.TestMarker("Final_Stop") +
                new Vector3(0, 0, -4), 180f, 0f);
            await Wait(0.6f);
            await Snap(folder, $"{prefix}_07_exam.png");

            Main.RtAcademy.ExitVenue(false);
            data.AcademyTestPassed = true;
            data.DrivingLicenceIssued = true;
            data.AcademyGraduationRewardClaimed = true;
            Main.RtOpenDrivingSchool();
            await Wait(0.5f);
            await Snap(folder, $"{prefix}_08_licence.png");
            Main.Probe.CloseModal();
            GD.Print($"ACADEMY CAPTURES {prefix} -> {folder}");
        }
        catch (Exception ex)
        {
            GD.PrintErr("Academy captures failed: " + ex);
            GetTree().Quit(1);
            return;
        }
        GetTree().Quit();
    }

    private async Task Snap(string folder, string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture()?.GetImage();
        if (image == null) return;
        image.SavePng(Path.Combine(folder, name));
        GD.Print("ACADEMY SHOT " + name);
    }

    private async Task SnapFree(string folder, string name, Vector3 eye, Vector3 target)
    {
        var camera = new Camera3D { Position = Main.RtAcademy.VenueOrigin + eye, Fov = 58f };
        AddChild(camera);
        camera.LookAt(Main.RtAcademy.VenueOrigin + target);
        camera.MakeCurrent();
        await Wait(0.4f);
        await Snap(folder, name);
        camera.QueueFree();
    }

    private async Task Wait(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds, processAlways: true),
            SceneTreeTimer.SignalName.Timeout);
}
