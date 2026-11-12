using System;
using Godot;

namespace PatchworkIsland;

/// <summary>School enrolment, lesson selection and licence presentation.</summary>
public static class DrivingAcademyUi
{
    public static void ShowSchool(Hud hud, DrivingAcademyService service,
        Action enrol, Action<string> start, Action resume, Action close)
    {
        var body = hud.OpenModal("DRIVING SCHOOL", new Vector2(980, 700), Palette.Yellow, true, close);
        var data = service.Data;

        var header = UiKit.HBox(body, 12);
        UiKit.Icon(header, "driving_licence", 40);
        var title = UiKit.Label(header, service.Enrolled ? "Patchwork Driving School" : "Learn to drive safely",
            26, Palette.Navy);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var coinRow = UiKit.HBox(header, 6);
        UiKit.Icon(coinRow, "coin", 28);
        UiKit.Label(coinRow, data.Coins.ToString(), 24, Palette.Navy, HorizontalAlignment.Left, false);

        if (!service.Enrolled)
        {
            UiKit.Label(body,
                $"Enrol once for {service.Catalog.EnrolmentCoins} coins. Practice, lessons and retries are free.",
                21, Palette.Navy);
            UiKit.Label(body,
                $"You have {data.Coins} coins. Need {service.CoinsNeededForEnrolment} more. " +
                $"The cone-sorting job outside pays {service.Catalog.HelperCoins} coins each round.",
                19, new Color("42526B"));
            if (service.HasLegacyPermit)
                UiKit.Label(body,
                    "Your older save keeps its van access, but an official licence is needed for future professional jobs.",
                    18, new Color("5C6B7A"));

            var row = UiKit.HBox(body, 10);
            var enrollButton = UiKit.Button(row,
                $"Enrol — {service.Catalog.EnrolmentCoins} coins", 22, new Vector2(300, 54),
                new Color("DFF6E3"), Palette.Success);
            enrollButton.Name = "AcademyEnrol";
            enrollButton.Disabled = data.Coins < service.Catalog.EnrolmentCoins;
            enrollButton.Pressed += () => enrol?.Invoke();
            var cones = UiKit.Button(row, "Cone job — 3 coins", 20, new Vector2(260, 54),
                Palette.Cream, Palette.Teal);
            cones.Name = "AcademyConeHelp";
            cones.Pressed += () => hud.ShowHint(
                "Walk to the three numbered cones beside the school. Press E on cone 1, then 2, then 3.");
        }
        else
        {
            if (!service.LicenceIssued)
            {
                string resumeId = service.ActiveStage;
                if (resumeId.Length > 0)
                {
                    var resumeRow = UiKit.HBox(body, 10);
                    var resumeButton = UiKit.Button(resumeRow,
                        $"Resume {StageName(service, resumeId)}", 22, new Vector2(300, 52),
                        new Color("E7F2FF"), Palette.Blue);
                    resumeButton.Name = "AcademyResume";
                    resumeButton.Pressed += () => resume?.Invoke();
                }
            }
            else
            {
                ShowLicenceCard(body, service);
            }

            foreach (var lesson in service.Catalog.Lessons)
            {
                bool done = service.LessonCompleted(lesson.Id);
                bool unlocked = service.IsUnlocked(lesson.Id);
                var card = UiKit.Panel(body,
                    done ? new Color("E5F6E8") : unlocked ? Palette.Cream : new Color("E4E0D8"),
                    done ? Palette.Success : unlocked ? Palette.Teal : new Color("B9B3A8"), 14, 3);
                var row = UiKit.HBox(card, 12);
                UiKit.Icon(row, IconFor(lesson.Id), 34);
                var text = UiKit.VBox(row, 2);
                text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                UiKit.Label(text, $"{lesson.Id} · {lesson.Title}", 21, Palette.Navy,
                    HorizontalAlignment.Left, false);
                UiKit.Label(text, done ? "Passed — replay any time" :
                    unlocked ? lesson.FirstHint : $"Pass {service.Catalog.PreviousLesson(lesson.Id)} first",
                    17, new Color("42526B"));
                if (unlocked)
                {
                    var button = UiKit.Button(row, done ? "Practise" : "Start", 19,
                        new Vector2(140, 46), new Color("E7F8F5"), Palette.Teal);
                    button.Name = "AcademyStart_" + lesson.Id;
                    string id = lesson.Id;
                    button.Pressed += () => start?.Invoke(id);
                }
                else
                {
                    UiKit.Label(row, "LOCKED", 17, new Color("6E747C"),
                        HorizontalAlignment.Right, false);
                }
            }

            var test = service.Catalog.FinalAssessment;
            bool testUnlocked = service.AllLessonsCompleted;
            var testCard = UiKit.Panel(body,
                service.LicenceIssued ? new Color("E5F6E8") : testUnlocked ? new Color("E7F2FF") : new Color("E4E0D8"),
                service.LicenceIssued ? Palette.Success : testUnlocked ? Palette.Blue : new Color("B9B3A8"), 14, 3);
            var testRow = UiKit.HBox(testCard, 12);
            UiKit.Icon(testRow, "traffic_light", 34);
            var testText = UiKit.VBox(testRow, 2);
            testText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            UiKit.Label(testText, $"{test.Id} · {test.Title}", 21, Palette.Navy,
                HorizontalAlignment.Left, false);
            UiKit.Label(testText, service.LicenceIssued
                ? "Licence earned — practice test is free"
                : testUnlocked
                    ? "Up to 3 minor notes. No major errors. Retries free."
                    : "Pass all six lessons first.",
                17, new Color("42526B"));
            if (testUnlocked)
            {
                var testButton = UiKit.Button(testRow, service.LicenceIssued ? "Practise" : "Start test",
                    19, new Vector2(140, 46), new Color("E7F8F5"), Palette.Blue);
                testButton.Name = "AcademyStart_TEST01";
                testButton.Pressed += () => start?.Invoke(test.Id);
            }

            UiKit.Label(body,
                $"Cone job: press E on cones 1, 2, 3 at the school. Each round pays {service.Catalog.HelperCoins} coins. " +
                "Wrong order costs nothing.",
                18, new Color("5C6B7A"));
        }
    }

    public static void ShowLicenceCard(Control parent, DrivingAcademyService service)
    {
        var card = UiKit.Panel(parent, new Color("FFF6D8"), Palette.Yellow, 18, 5);
        var row = UiKit.HBox(card, 14);
        UiKit.Icon(row, "driving_licence", 58);
        var text = UiKit.VBox(row, 2);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UiKit.Label(text, "PATCHWORK ISLAND DRIVING LICENCE", 24, Palette.Navy,
            HorizontalAlignment.Left, false);
        UiKit.Label(text, "Official learner qualification · Patchwork Island only", 18,
            new Color("42526B"));
        UiKit.Label(text, "This is a game certificate, not a real-world driving licence.", 16,
            new Color("6E747C"));
    }

    public static void ShowResult(Hud hud, string title, string message, bool success,
        Action retry, Action school, Action close)
    {
        var body = hud.OpenModal(title, new Vector2(680, 340),
            success ? Palette.Success : Palette.Orange, true, close);
        var row = UiKit.HBox(body, 12);
        UiKit.Icon(row, success ? "driving_licence" : "steering_wheel", 48);
        var label = UiKit.Label(row, message, 23, Palette.Navy);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var buttons = UiKit.HBox(body, 10);
        UiKit.Spacer(buttons);
        if (retry != null)
        {
            var retryButton = UiKit.Button(buttons, "Try again", 21, new Vector2(190, 52),
                Palette.Cream, Palette.Orange);
            retryButton.Name = "AcademyRetry";
            retryButton.Pressed += () => retry();
        }
        var schoolButton = UiKit.Button(buttons, "Back to school", 21, new Vector2(220, 52),
            new Color("E7F8F5"), Palette.Teal);
        schoolButton.Name = "AcademyBackToSchool";
        schoolButton.Pressed += () => school?.Invoke();
        var closeButton = UiKit.Button(buttons, "Close", 21, new Vector2(140, 52));
        closeButton.Name = "AcademyResultClose";
        closeButton.Pressed += () => close?.Invoke();
    }

    public static string IconFor(string id) => id switch
    {
        "L01" => "steering_wheel",
        "L02" => "steering_wheel",
        "L03" => "traffic_light",
        "L04" => "traffic_light",
        "L05" => "parking",
        "L06" => "steering_wheel",
        _ => "driving_licence",
    };

    private static string StageName(DrivingAcademyService service, string id) =>
        service.Catalog.Find(id)?.Title ?? id;
}
