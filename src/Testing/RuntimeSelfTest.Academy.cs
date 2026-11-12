using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

public partial class RuntimeSelfTest
{
    private async Task AcademyChecks()
    {
        GameData data = Main.Probe.Data;
        DrivingAcademyController academy = Main.RtAcademy;
        DrivingAcademyService service = Main.RtSession.Academy;

        Main.Probe.RequestNewGameFromTitle();
        await Wait(0.25f);
        Expect(Main.Probe.PressModalButton("Choose_boy"), "academy test starts a fresh v5 game");
        await Wait(0.5f);
        data = Main.Probe.Data;
        service = Main.RtSession.Academy;
        academy = Main.RtAcademy;

        Expect(!academy.Active, "academy starts unloaded");
        Expect(!data.LegacyVanPermit && !data.DrivingLicenceIssued,
            "fresh v5 save has neither a legacy permit nor a licence");
        Expect(service.Catalog.Lessons.Count == 6 &&
               service.Catalog.RequiredMarkers.Count == 27,
            "authored academy contract loads six lessons and 27 markers");

        // School placement and physical cone job.
        Vector3 school = Main.GetNode<WorldSceneAdapter>("Village").SchoolMarker(
            "EntranceApproach").GlobalPosition;
        Main.Probe.TeleportPlayer(school + new Vector3(0f, 0.1f, -1.2f), Mathf.Pi);
        await Wait(0.35f);
        Expect(school.X > 20f && school.X < 40f && school.Z > 40f,
            "school approach sits on the measured south-lawn site");
        Main.RtOpenDrivingSchool();
        await Wait(0.25f);
        Expect(Main.Probe.ModalTitle == "DRIVING SCHOOL", "school opens its own board");
        Expect(Main.Probe.FindModalButton("AcademyEnrol")?.Disabled == true,
            "zero-coin player cannot enrol until the cone job pays");

        for (int round = 0; round < 5; round++)
        {
            for (int cone = 1; cone <= 3; cone++)
            {
                var station = GetTree().GetNodesInGroup("interactable").OfType<Interactable>()
                    .FirstOrDefault(n => n.Name == $"AcademyConeStation_{cone}");
                Expect(station != null, $"physical cone {cone} is present");
                station?.Interact();
                await Wait(0.12f);
            }
        }
        Expect(data.Coins == 15, "five physical cone rounds pay exactly 15 coins");
        Expect(data.AcademyHelperRoundsPaid == 5, "helper rounds are persisted");

        Main.RtOpenDrivingSchool();
        await Wait(0.2f);
        Expect(Main.Probe.PressModalButton("AcademyEnrol"), "enrol button works at 15 coins");
        await Wait(0.35f);
        Expect(data.AcademyEnrolled && data.Coins == 0,
            "one-time 15-coin enrolment is deducted exactly once");
        Expect(Main.Probe.ModalTitle == "DRIVING SCHOOL", "school reopens after enrolment");

        // L01 through the real UI and learner-car predicates.
        Expect(Main.Probe.PressModalButton("AcademyStart_L01"), "L01 start button works");
        await Wait(0.4f);
        Expect(academy.Active && academy.CurrentStage == "L01", "L01 loads the isolated venue");
        Expect(academy.MarkerCount >= 31, "course exposes all authored markers");
        Expect(academy.VenueLoaded && academy.VenueOrigin.X >= 1900f,
            "academy venue is physically isolated from town, city and rally");
        foreach (Vector3 lane in new[]
                 {
                     new Vector3(-60, 0, -68), new Vector3(60, 0, -68),
                     new Vector3(60, 0, 60), new Vector3(-60, 0, 60),
                     new Vector3(-35, 0, 36), new Vector3(37, 0, 36),
                 })
        {
            var probe = academy.VenueOrigin + lane;
            academy.TestPlaceCar(probe, 0f, 0f);
            await Wait(0.2f);
            Vector3 at = academy.TrainingCar.GlobalPosition;
            float dx = at.X - probe.X;
            float dz = at.Z - probe.Z;
            Expect(at.Y > -0.4f && Mathf.Sqrt(dx * dx + dz * dz) < 2f,
                $"authored road surface supports the car at {lane}");
        }
        Expect(Main.Probe.Data.AcademyActiveStage == "L01", "active lesson is persisted");
        academy.TestPlaceCar(academy.TestMarker("Lesson01_Accelerate"),
            academy.TestMarkerYaw("Lesson01_Accelerate"), 3f);
        await Wait(0.3f);
        Expect(academy.TestStep == 1, "L01 passing speed advances the step");
        academy.TestPlaceCar(academy.TestMarker("Lesson01_Stop"),
            academy.TestMarkerYaw("Lesson01_Stop"), 0f);
        await Wait(1.8f);
        Expect(service.LessonCompleted("L01"), "L01 completes after a held stop");

        // Save and resume mid-lesson.
        Main.Probe.PressModalButton("AcademyBackToSchool");
        await Wait(0.5f);
        service.StartLesson("L02");
        academy.EnterStage("L02", false);
        await Wait(0.3f);
        Main.RtSaveForAcademyTest();
        academy.ExitVenue(false);
        await Wait(0.4f);
        Expect(!academy.Active && data.AcademyActiveStage == "L02",
            "mid-lesson exit keeps L02 resumable");
        Expect(Mathf.Abs(data.Player.X) < 1000f && Mathf.Abs(data.Player.Z) < 1000f,
            "town player snapshot stays inside the valid world bounds");
        Main.RtOpenDrivingSchool();
        await Wait(0.25f);
        Expect(Main.Probe.PressModalButton("AcademyResume"), "resume button appears");
        await Wait(0.5f);
        Expect(academy.Active && academy.CurrentStage == "L02", "L02 resumes safely");

        foreach (string gate in new[] { "Lesson02_Gate01", "Lesson02_Gate02", "Lesson02_Gate03" })
        {
            academy.TestPlaceCar(academy.TestMarker(gate), academy.TestMarkerYaw(gate), 0f);
            await Wait(0.25f);
        }
        Expect(service.LessonCompleted("L02"), "L02 ordered gates complete");
        await Wait(0.3f);
        Main.Probe.PressModalButton("AcademyBackToSchool");
        await Wait(0.45f);

        // L03 stop line and release.
        service.StartLesson("L03");
        academy.EnterStage("L03", false);
        await Wait(0.3f);
        academy.TestPlaceCar(academy.TestMarker("Lesson03_StopApproach"), 180f, 0f);
        await Wait(1.8f);
        academy.TestPlaceCar(academy.TestMarker("Lesson03_AfterStop"), 180f, 0f);
        await Wait(0.4f);
        Expect(service.LessonCompleted("L03"), "L03 requires a held stop before the line");
        Main.Probe.PressModalButton("AcademyBackToSchool");
        await Wait(0.45f);

        // L04 signal and crossing.
        service.StartLesson("L04");
        academy.EnterStage("L04", false);
        await Wait(0.3f);
        academy.TestSetLight(AcademyLight.Red);
        academy.TestPlaceCar(academy.TestMarker("Lesson04_TrafficLight"), 180f, 0f);
        await Wait(0.3f);
        academy.TestSetLight(AcademyLight.Green);
        academy.TestPlaceCar(academy.TestMarker("Lesson04_TrafficLight") +
            new Vector3(0f, 0f, 7f), 180f, 2f);
        await Wait(0.3f);
        academy.TestPlaceCar(academy.TestMarker("Lesson04_Crossing"), -90f, 0f);
        await Wait(0.3f);
        academy.TestPlaceCar(academy.TestMarker("Lesson04_GiveWay"), 180f, 0f);
        await Wait(0.5f);
        Expect(service.LessonCompleted("L04"), "L04 green/crossing path completes");
        Main.Probe.PressModalButton("AcademyBackToSchool");
        await Wait(0.45f);

        // L05 reverse and park with real reverse input.
        service.StartLesson("L05");
        academy.EnterStage("L05", false);
        await Wait(0.3f);
        academy.TestPlaceCar(academy.TestMarker("Lesson05_Start"),
            academy.TestMarkerYaw("Lesson05_Start"), 0f);
        Input.ActionPress("move_back");
        await Wait(2.0f);
        Input.ActionRelease("move_back");
        await Wait(0.3f);
        Expect(academy.TestStep >= 1, "L05 reverse distance is measured");
        academy.TestPlaceCar(academy.TestMarker("Lesson05_ParkingTarget"), 180f, 0f);
        await Wait(1.8f);
        Expect(service.LessonCompleted("L05"), "L05 parks inside the bay");
        Main.Probe.PressModalButton("AcademyBackToSchool");
        await Wait(0.45f);

        // L06 checkpoints.
        service.StartLesson("L06");
        academy.EnterStage("L06", false);
        await Wait(0.3f);
        foreach (string point in new[]
                 {
                     "Lesson06_Checkpoint01", "Lesson06_Checkpoint02",
                     "Lesson06_Checkpoint03", "Lesson06_Checkpoint04",
                 })
        {
            academy.TestPlaceCar(academy.TestMarker(point), 0f, 0f);
            await Wait(0.25f);
        }
        Expect(service.LessonCompleted("L06"), "L06 visits all checkpoints in order");
        await Wait(0.35f);
        Main.Probe.PressModalButton("AcademyBackToSchool");
        await Wait(0.45f);

        // Final assessment: stop, green, crossing, park.
        service.StartLesson("TEST01");
        academy.EnterStage("TEST01", false);
        await Wait(0.35f);
        academy.TestSetLight(AcademyLight.Red);
        academy.TestPlaceCar(academy.TestMarker("Final_Stop"), 180f, 0f);
        await Wait(1.8f);
        Expect(academy.TestStep >= 1, "test holds at the stop line");
        academy.TestSetLight(AcademyLight.Green);
        academy.TestPlaceCar(academy.TestMarker("Final_Stop") +
            new Vector3(0f, 0f, 9f), 180f, 2f);
        await Wait(0.4f);
        academy.TestPlaceCar(academy.TestMarker("Final_Crossing") +
            new Vector3(3f, 0f, 0f), -90f, 0f);
        await Wait(4.2f);
        academy.TestPlaceCar(academy.TestMarker("Final_Park"), 0f, 0f);
        await Wait(2.0f);
        Expect(data.DrivingLicenceIssued && data.AcademyTestPassed,
            "final assessment issues the official licence");
        Expect(data.AcademyGraduationRewardClaimed && data.Coins == 5,
            "five-coin graduation gift is paid once");
        Expect(service.CanDriveRegularVan, "licence unlocks the regular van");

        // Practice is free and pays nothing.
        service.StartLesson("TEST01");
        academy.EnterStage("TEST01", true);
        await Wait(0.3f);
        Expect(academy.Active, "practice test can be replayed");
        academy.ExitVenue(false);
        await Wait(0.3f);
        Expect(data.Coins == 5, "practice test pays no coins");
        Main.RtSaveForAcademyTest();
        var persisted = SaveService.Load(Main.Probe.SavePath, Main.Probe.Catalog);
        Expect(persisted.Success && persisted.Data.DrivingLicenceIssued &&
               persisted.Data.AcademyTestPassed &&
               persisted.Data.AcademyCompletedLessons.Count == 6,
            "licence and six lessons persist on schema 5");
        Expect(persisted.Data.Coins == 5 &&
               persisted.Data.AcademyGraduationRewardClaimed,
            "graduation gift survives reload without duplication");

        // Repeated venue load/unload leaves no orphaned course.
        int rootBefore = GetTree().Root.GetChildCount();
        for (int i = 0; i < 30; i++)
        {
            academy.EnterStage("L01", true);
            await Wait(0.04f);
            academy.ExitVenue(false);
            await Wait(0.04f);
        }
        int orphanCourses = GetTree().Root.FindChildren("AcademyVenue", "Node3D", true, false).Count;
        Expect(!academy.Active && !academy.VenueLoaded && orphanCourses == 0,
            "30 school returns leave no course, collision or HUD orphan");
        Expect(GetTree().Root.GetChildCount() <= rootBefore + 2,
            "school returns do not accumulate scene nodes");
        Expect(Main.Probe.PlayerVisible && Main.Probe.Mode != "Academy",
            "return restores the walking player");
        Expect(Main.Probe.PlayerPosition.Y > -1f, "return position is safe and in town");
    }
}
