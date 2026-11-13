using System;
using System.Collections.Generic;
using System.IO;
using PatchworkIsland;

namespace PatchworkIsland.Tests;

internal static partial class Program
{
    private static void AcademyTests(MissionCatalog catalog, string root)
    {
        var academy = DrivingAcademyCatalog.FromJson(
            File.ReadAllText(Path.Combine(root, "data", "driving_academy_lessons.json")));

        Run("academy lesson contract and thresholds match the authored handoff", () =>
        {
            Equal(6, academy.Lessons.Count, "six lessons");
            Equal("TEST01", academy.FinalAssessment.Id, "final assessment id");
            Equal(27, academy.RequiredMarkers.Count, "27 required scene markers");
            Equal(15, academy.EnrolmentCoins, "enrolment cost");
            Equal(3, academy.HelperCoins, "helper round pay");
            Equal(5, academy.GraduationCoins, "graduation gift");
            Equal("L01", academy.Lessons[0].Id, "first lesson");
            Equal("L06", academy.Lessons[^1].Id, "last lesson");
            Equal(8.9408f, DrivingAcademyRules.SpeedLimitMps, "20 mph in metres per second");
            Check(Math.Abs(DrivingAcademyRules.MphToMps(20f) - 8.9408f) < 0.0001f,
                "mph conversion");
        });

        Run("academy stop, parking and assessment rules are deterministic", () =>
        {
            Check(DrivingAcademyRules.StopHeld(0.3f, 1.5f), "stop at 0.3 m/s held 1.5 s");
            Check(!DrivingAcademyRules.StopHeld(0.31f, 1.5f), "0.31 m/s is moving");
            Check(!DrivingAcademyRules.StopHeld(0.3f, 1.49f), "1.49 s is too short");
            Check(DrivingAcademyRules.IsParked(1f, 1f, 0f, 0f, 0f, 0f),
                "parked inside bay");
            Check(DrivingAcademyRules.IsParked(1f, 1f, 180f, 0f, 0f, 0f),
                "bay axis accepts the opposite heading");
            Check(!DrivingAcademyRules.IsParked(1f, 1f, 30f, 0f, 0f, 0f),
                "30 degrees is outside tolerance");
            Check(!DrivingAcademyRules.IsParked(9f, 1f, 0f, 0f, 0f, 0f),
                "outside bay distance");
            Check(DrivingAcademyRules.GradeAssessment(0, 3).Passed, "three minor notes pass");
            Check(!DrivingAcademyRules.GradeAssessment(0, 4).Passed, "four minor notes fail");
            Check(!DrivingAcademyRules.GradeAssessment(1, 0).Passed, "major error fails");
        });

        Run("academy light cycle and red-line edge cases", () =>
        {
            Equal(AcademyLight.RedAmber, DrivingAcademyRules.NextLight(AcademyLight.Red), "red to red+amber");
            Equal(AcademyLight.Green, DrivingAcademyRules.NextLight(AcademyLight.RedAmber), "red+amber to green");
            Equal(AcademyLight.Amber, DrivingAcademyRules.NextLight(AcademyLight.Green), "green to amber");
            Equal(AcademyLight.Red, DrivingAcademyRules.NextLight(AcademyLight.Amber), "amber to red");
            Check(DrivingAcademyRules.LightMeansStop(AcademyLight.Red), "red stops");
            Check(DrivingAcademyRules.LightMeansStop(AcademyLight.RedAmber), "red+amber stops");
            Check(DrivingAcademyRules.LightMeansStop(AcademyLight.Amber), "amber stops");
            Check(DrivingAcademyRules.LightAllowsEntry(AcademyLight.Green), "green allows entry");
            Check(DrivingAcademyRules.IsRedLightOffence(AcademyLight.Red, true, false),
                "crossing on red is an offence");
            Check(!DrivingAcademyRules.IsRedLightOffence(AcademyLight.Red, true, true),
                "already past the line when red started is not an offence");
            Check(!DrivingAcademyRules.IsRedLightOffence(AcademyLight.Green, true, false),
                "green crossing is legal");
        });

        Run("academy amber exception uses safe stopping distance at onset", () =>
        {
            Check(!DrivingAcademyRules.AmberEntryUnavoidable(0f, 0f), "stopped car must wait");
            Check(!DrivingAcademyRules.AmberEntryUnavoidable(10f, 4f), "ample room to stop");
            Check(DrivingAcademyRules.AmberEntryUnavoidable(1f, 4f), "too close to stop safely");
            Check(!DrivingAcademyRules.AmberEntryUnavoidable(-1f, 4f), "already beyond line");
            Check(!DrivingAcademyRules.AmberEntryUnavoidable(1f, -4f), "moving away from line");
            float stopping = 4f + 16f / 22f;
            Check(!DrivingAcademyRules.AmberEntryUnavoidable(stopping, 4f), "exact stopping distance can stop");
        });

        Run("academy guidance directions match the car's handed sides at every heading", () =>
        {
            // Car at the origin; markers one metre along each cardinal world
            // direction. Godot convention: yaw 0 faces -Z (north), and a
            // positive relative heading is the car's LEFT. Reviewer P2: the
            // previous controller mapping labelled these sides backwards, so
            // an east-side marker from a north-facing car read "ahead left".
            // Expected labels are written out per case, not derived.
            const float pi = MathF.PI;
            (float yaw, string facing, float x, float z, string expected)[] cases =
            {
                (0f, "north", 0f, -1f, "straight ahead"),
                (0f, "north", 1f, 0f, "ahead right"),
                (0f, "north", -1f, 0f, "ahead left"),
                // Directly behind sits on the +/-pi wrap boundary; Atan2
                // resolves it to -pi, so the suggestion is "turn right".
                // Either suggestion would be correct there.
                (0f, "north", 0f, 1f, "behind, turn right"),
                (pi / 2f, "west", -1f, 0f, "straight ahead"),
                (pi / 2f, "west", 0f, -1f, "ahead right"),
                (pi / 2f, "west", 0f, 1f, "ahead left"),
                (pi / 2f, "west", 1f, 0f, "behind, turn right"),
                (pi, "south", 0f, 1f, "straight ahead"),
                (pi, "south", -1f, 0f, "ahead right"),
                (pi, "south", 1f, 0f, "ahead left"),
                (pi, "south", 0f, -1f, "behind, turn right"),
                (-pi / 2f, "east", 1f, 0f, "straight ahead"),
                (-pi / 2f, "east", 0f, 1f, "ahead right"),
                (-pi / 2f, "east", 0f, -1f, "ahead left"),
                (-pi / 2f, "east", -1f, 0f, "behind, turn left"),
            };
            foreach (var (yaw, facing, x, z, expected) in cases)
            {
                float relative = DrivingAcademyRules.GuidanceRelativeRadians(x, z, yaw);
                Equal(expected, DrivingAcademyRules.GuidanceDirection(relative),
                    $"{facing}-facing car, marker at ({x}, {z}) (relative {relative})");
            }
            Check(DrivingAcademyRules.GuidanceDirection(0.5f) == "straight ahead", "small positive is ahead");
            Check(DrivingAcademyRules.GuidanceDirection(-0.5f) == "straight ahead", "small negative is ahead");
            Check(DrivingAcademyRules.GuidanceDirection(0.7f) == "ahead left", "band start is left");
            Check(DrivingAcademyRules.GuidanceDirection(-0.7f) == "ahead right", "band start is right");
            Check(DrivingAcademyRules.GuidanceDirection(2.1f) == "behind, turn left", "behind band start is left");
            Check(DrivingAcademyRules.GuidanceDirection(-2.1f) == "behind, turn right", "behind band start is right");
        });

        Run("academy enrolment and helper job pay exactly once", () =>
        {
            var data = new GameData { Coins = 14 };
            var service = new DrivingAcademyService(data, academy);
            Check(!service.CanDriveRegularVan, "new save has no regular van permit");
            Check(!service.LicenceIssued, "new save has no licence");

            Check(!service.Enrol().Success, "14 coins cannot enrol");
            Equal(14, data.Coins, "failed enrolment charges nothing");

            Check(!service.AdvanceHelper(2).Success, "wrong first cone is rejected");
            service.AdvanceHelper(1);
            Check(SaveValidation.Validate(data).IsValid,
                "partial helper progress is valid before enrolment");
            data.AcademyHelperStep = 0;
            Equal(0, data.AcademyHelperStep, "wrong cone resets the sequence");
            for (int round = 0; round < 5; round++)
            {
                Check(service.AdvanceHelper(1).Success, "cone 1");
                Check(service.AdvanceHelper(2).Success, "cone 2");
                Check(service.AdvanceHelper(3).Success, "cone 3");
            }
            Equal(29, data.Coins, "five helper rounds add 15 coins");
            Equal(5, data.AcademyHelperRoundsPaid, "five completed rounds");

            Check(service.Enrol().Success, "15 coins enrol");
            Equal(14, data.Coins, "enrolment costs exactly 15");
            Check(!service.Enrol().Success, "second enrolment is rejected");
            Equal(14, data.Coins, "second enrolment charges nothing");
            Check(data.AcademyEnrolled, "enrolled flag set");
        });

        Run("academy lessons are sequential and the licence is exactly-once", () =>
        {
            var data = new GameData { Coins = 100 };
            var service = new DrivingAcademyService(data, academy);
            service.Enrol();
            Equal(85, data.Coins, "enrolment deducted once");

            Check(!service.StartLesson("L02").Success, "L02 locked before L01");
            Check(!service.StartLesson("TEST01").Success, "test locked before lessons");
            for (int i = 1; i <= 6; i++)
            {
                string id = $"L{i:00}";
                Check(service.StartLesson(id).Success, "start " + id);
                var done = service.CompleteLesson(id);
                Check(done.Success, "complete " + id);
            }
            Check(service.AllLessonsCompleted, "all six lessons complete");
            Equal(6, data.AcademyCompletedLessons.Count, "no duplicate lesson entries");

            Check(service.StartLesson("TEST01").Success, "test starts");
            var fail = service.SubmitAssessment(1, 0, false);
            Check(!fail.Passed, "major error fails the test");
            Check(!data.DrivingLicenceIssued, "failed test issues no licence");
            Equal(1, data.AcademyTestAttempts, "attempt counted");

            Check(service.StartLesson("TEST01").Success, "test retry starts");
            var pass = service.SubmitAssessment(0, 3, false);
            Check(pass.Passed, "three minor notes pass");
            Check(data.DrivingLicenceIssued && data.AcademyTestPassed, "licence issued");
            Check(data.AcademyGraduationRewardClaimed, "graduation gift claimed");
            Equal(90, data.Coins, "five-coin graduation gift paid once");

            var again = service.SubmitAssessment(0, 0, true);
            Check(again.Passed, "practice test passes");
            Equal(90, data.Coins, "practice never pays again");
            Check(data.DrivingLicenceIssued, "practice keeps the licence");
        });

        Run("academy save state validates and rejects impossible progress", () =>
        {
            var good = new GameData { Coins = 20 };
            var service = new DrivingAcademyService(good, academy);
            service.Enrol();
            Check(SaveValidation.Validate(good).IsValid, "fresh enrolled save is valid");

            var bad = new GameData { DrivingLicenceIssued = true };
            Check(!SaveValidation.Validate(bad).IsValid, "licence without test is invalid");

            var bad2 = new GameData { AcademyEnrolled = true };
            bad2.AcademyCompletedLessons.Add("L02");
            Check(!SaveValidation.Validate(bad2).IsValid, "L02 without L01 is invalid");

            var bad3 = new GameData { AcademyEnrolled = true, AcademyHelperStep = 3 };
            Check(SaveValidation.Validate(bad3).IsValid && bad3.AcademyHelperStep == 2,
                "helper step out of range is normalised");
        });

        Run("schema 1-4 saves all migrate to a legacy van permit without a licence", () =>
        {
            foreach (int version in new[] { 1, 2, 3, 4 })
            {
                string path = Path.Combine(Path.GetTempPath(),
                    "patchwork-academy-migration-v" + version + "-" + Guid.NewGuid() + ".json");
                try
                {
                    File.WriteAllText(path,
                        $"{{\"SchemaVersion\":{version},\"Coins\":12,\"CompletedMissions\":[\"M01\"]}}");
                    var loaded = SaveService.Load(path, catalog);
                    Check(loaded.Success, $"schema {version} loads: " + loaded.Message);
                    Equal(5, loaded.Data.SchemaVersion, $"schema {version} migrates to 5");
                    Check(loaded.Data.LegacyVanPermit, $"schema {version} gets a legacy permit");
                    Check(!loaded.Data.DrivingLicenceIssued,
                        $"schema {version} does not get a false licence");
                    Check(!loaded.Data.AcademyEnrolled, $"schema {version} does not enrol");
                }
                finally
                {
                    if (File.Exists(path)) File.Delete(path);
                    if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
                }
            }
        });

        Run("schema 4 saves migrate to a legacy van permit without a licence", () =>
        {
            string path = Path.Combine(Path.GetTempPath(), "patchwork-academy-migration-" + Guid.NewGuid() + ".json");
            try
            {
                File.WriteAllText(path,
                    "{\"SchemaVersion\":4,\"Coins\":12,\"CompletedMissions\":[\"M01\"]}");
                var loaded = SaveService.Load(path, catalog);
                Check(loaded.Success, "schema 4 loads: " + loaded.Message);
                Equal(5, loaded.Data.SchemaVersion, "migrated to schema 5");
                Check(loaded.Data.LegacyVanPermit, "legacy permit granted");
                Check(!loaded.Data.DrivingLicenceIssued, "legacy permit is not a licence");
                Check(!loaded.Data.AcademyEnrolled, "migration does not enrol");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
