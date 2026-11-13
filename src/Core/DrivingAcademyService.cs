using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

public sealed class AcademyActionResult
{
    public bool Success { get; init; }
    public bool Practice { get; init; }
    public int CoinsAwarded { get; init; }
    public string Message { get; init; } = "";
}

/// <summary>
/// Pure academy state machine. Every payment and licence award is one atomic
/// mutation of the supplied GameData, so repeated UI callbacks or a reload can
/// never duplicate a charge or a reward.
/// </summary>
public sealed class DrivingAcademyService
{
    public GameData Data { get; }
    public DrivingAcademyCatalog Catalog { get; }

    public DrivingAcademyService(GameData data, DrivingAcademyCatalog catalog)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Data.Normalise();
    }

    public bool Enrolled => Data.AcademyEnrolled;
    public bool LicenceIssued => Data.DrivingLicenceIssued;
    public bool TestPassed => Data.AcademyTestPassed;
    public bool CanDriveRegularVan => Data.DrivingLicenceIssued || Data.LegacyVanPermit;
    public bool HasLegacyPermit => Data.LegacyVanPermit;
    public int CompletedLessonCount => Data.AcademyCompletedLessons.Count;
    public string ActiveStage => Data.AcademyActiveStage ?? "";
    public int HelperStep => Data.AcademyHelperStep;
    public int HelperRounds => Data.AcademyHelperRoundsPaid;
    public int CoinsNeededForEnrolment =>
        Math.Max(0, Catalog.EnrolmentCoins - Data.Coins);

    public bool LessonCompleted(string id) =>
        Data.AcademyCompletedLessons.Contains(id);

    public bool AllLessonsCompleted =>
        Catalog.Lessons.All(lesson => LessonCompleted(lesson.Id));

    public bool IsUnlocked(string id)
    {
        if (!Catalog.IsLesson(id)) return id == Catalog.FinalAssessment.Id && AllLessonsCompleted;
        string previous = Catalog.PreviousLesson(id);
        return previous.Length == 0 || LessonCompleted(previous);
    }

    public string NextLesson =>
        Catalog.Lessons.FirstOrDefault(lesson => !LessonCompleted(lesson.Id))?.Id ?? "";

    public AcademyActionResult Enrol()
    {
        if (Data.AcademyEnrolled)
            return Fail("You are already enrolled at the Driving School.");
        if (Data.Coins < Catalog.EnrolmentCoins)
            return Fail($"Enrolment costs {Catalog.EnrolmentCoins} coins. " +
                        $"You have {Data.Coins}. The cone-sorting job pays {Catalog.HelperCoins} each round.");
        Data.Coins -= Catalog.EnrolmentCoins;
        Data.AcademyEnrolled = true;
        return Ok($"Enrolled! {Catalog.EnrolmentCoins} coins paid once. Lessons and retries are free.");
    }

    public AcademyActionResult StartLesson(string id)
    {
        if (!Data.AcademyEnrolled)
            return Fail("Enrol at the school first.");
        var stage = Catalog.Find(id);
        if (stage == null) return Fail("That academy stage is not on the course.");
        if (stage.IsFinal)
        {
            if (!AllLessonsCompleted)
                return Fail("Pass all six lessons before the final driving test.");
            if (LicenceIssued)
                return new AcademyActionResult
                {
                    Success = true, Practice = true,
                    Message = "Practice test — no licence or coins change hands.",
                };
            Data.AcademyActiveStage = id;
            return Ok("Final driving test started. Three minor notes are allowed; no major errors.");
        }
        if (!IsUnlocked(id))
            return Fail($"Pass {Catalog.PreviousLesson(id)} first.");
        if (LessonCompleted(id))
            return new AcademyActionResult
            {
                Success = true, Practice = true,
                Message = $"{stage.Title} practice — progress is already saved.",
            };
        Data.AcademyActiveStage = id;
        return Ok($"{stage.Title} started. {stage.FirstHint}");
    }

    public AcademyActionResult CompleteLesson(string id)
    {
        if (!Data.AcademyEnrolled) return Fail("Enrol at the school first.");
        if (!Catalog.IsLesson(id)) return Fail("That is not a practice lesson.");
        if (LessonCompleted(id))
            return new AcademyActionResult { Success = true, Practice = true, Message = "Lesson already passed." };
        if (!IsUnlocked(id)) return Fail($"Pass {Catalog.PreviousLesson(id)} first.");
        if (ActiveStage != id) return Fail("That lesson is not active.");

        Data.AcademyCompletedLessons.Add(id);
        Data.AcademyActiveStage = "";
        int passed = Data.AcademyCompletedLessons.Count;
        return Ok($"{Catalog.Find(id)?.Title} passed ({passed}/{Catalog.Lessons.Count}).");
    }

    /// <summary>
    /// One cone press. The player must press 1, 2, 3 in order. A wrong cone
    /// resets the round without cost. The third correct cone pays once.
    /// </summary>
    public AcademyActionResult AdvanceHelper(int coneNumber)
    {
        if (coneNumber < 1 || coneNumber > DrivingAcademyRules.HelperConeCount)
            return Fail("That cone is not part of the job.");

        int expected = Data.AcademyHelperStep + 1;
        if (coneNumber != expected)
        {
            Data.AcademyHelperStep = 0;
            return Fail($"Start with cone 1. The order is 1, 2, 3.");
        }

        Data.AcademyHelperStep++;
        if (Data.AcademyHelperStep < DrivingAcademyRules.HelperConeCount)
            return Ok($"Cone {coneNumber} checked. Next cone {Data.AcademyHelperStep + 1}.");

        Data.AcademyHelperStep = 0;
        Data.AcademyHelperRoundsPaid++;
        Data.Coins += Catalog.HelperCoins;
        return Ok($"Three cones sorted! +{Catalog.HelperCoins} coins. " +
                  $"You have {Data.Coins}.");
    }

    public AcademyActionResult AbandonStage()
    {
        bool wasActive = ActiveStage.Length > 0;
        Data.AcademyActiveStage = "";
        return new AcademyActionResult
        {
            Success = true,
            Message = wasActive ? "Lesson paused. You can resume it from the school." : "Returned to school.",
        };
    }

    /// <summary>Grades the final test. Practice runs never change coins or licence state.</summary>
    public AcademyAssessmentGrade SubmitAssessment(int majorErrors, int minorErrors, bool practice)
    {
        if (!Data.AcademyEnrolled)
            return new AcademyAssessmentGrade { Message = "Enrol at the school first." };
        if (!AllLessonsCompleted)
            return new AcademyAssessmentGrade { Message = "Pass all six lessons before the test." };
        if (!practice && ActiveStage != Catalog.FinalAssessment.Id)
            return new AcademyAssessmentGrade { Message = "The final test is not active." };

        var grade = DrivingAcademyRules.GradeAssessment(majorErrors, minorErrors);
        if (practice) return grade;

        Data.AcademyTestAttempts++;
        if (!grade.Passed) return grade;

        Data.AcademyTestPassed = true;
        Data.DrivingLicenceIssued = true;
        Data.AcademyActiveStage = "";
        if (!Data.AcademyGraduationRewardClaimed)
        {
            Data.AcademyGraduationRewardClaimed = true;
            Data.Coins += Catalog.GraduationCoins;
            return new AcademyAssessmentGrade
            {
                Passed = true,
                MajorErrors = majorErrors,
                MinorErrors = minorErrors,
                Message = $"{grade.Message} Graduation gift +{Catalog.GraduationCoins} coins.",
            };
        }
        return grade;
    }

    private static AcademyActionResult Ok(string message) =>
        new() { Success = true, Message = message };

    private static AcademyActionResult Fail(string message) =>
        new() { Success = false, Message = message };
}
