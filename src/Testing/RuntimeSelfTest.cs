using System;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Godot runtime verification for pause/processing and modal state. Run with:
///   godot --headless --path . -- --selftest
///   godot --headless --path . -- --selftest-resume
/// It uses isolated user://selftest-save.json data and exits non-zero on failure.
/// </summary>
public partial class RuntimeSelfTest : Node
{
    public Main Main { get; set; }

    private int _passed;
    private int _failed;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _ = Run();
    }

    private async Task Run()
    {
        try
        {
            await Wait(0.7f);
            if (HasArg("--selftest-resume"))
                await ResumeChecks();
            else
                await FreshSessionChecks();
        }
        catch (Exception ex)
        {
            Fail("unexpected runtime-test exception: " + ex);
        }

        GD.Print($"RUNTIME RESULT: {_passed} passed, {_failed} failed");
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    private async Task FreshSessionChecks()
    {
        // Title + processing modes.
        Expect(!Main.RtStarted, "starts on the title screen");
        Expect(Main.RtPaused, "title screen keeps the world paused");
        Expect(Main.RtMode == "Title", "title control mode");
        Expect(Main.RtPlayerProcessMode == ProcessModeEnum.Pausable, "player is pausable");
        Expect(Main.RtVanProcessMode == ProcessModeEnum.Pausable, "van is pausable");
        Expect(Main.RtWorldProcessMode == ProcessModeEnum.Pausable, "world is pausable");

        Main.RtOpenSettingsFromTitle();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Settings", "title opens settings");
        Expect(Main.RtPaused && Main.RtMode == "Title", "title settings remains paused");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(Main.RtModalOpen && !Main.RtStarted && Main.RtPaused && Main.RtMode == "Title",
            "settings returns to the title screen");
        Expect(Main.RtCanContinue == false, "fresh self-test has no continue");

        // Start and walk.
        Main.RtStart();
        await Wait(0.3f);
        Expect(Main.RtStarted && !Main.RtPaused && Main.RtMode == "Walking", "start resumes walking");
        Expect(Main.RtPlayerControlled && !Main.RtVanControlled, "walking owns player input only");
        Vector3 walkStart = Main.RtPlayerPosition;
        Input.ActionPress("move_forward");
        await Wait(0.45f);
        Input.ActionRelease("move_forward");
        await Wait(0.15f);
        Expect(walkStart.DistanceTo(Main.RtPlayerPosition) > 0.05f, "player moves when unpaused");

        // Walking pause; physics must stop even with a living player.
        Main.RtPause();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Paused", "pause modal opens");
        Expect(Main.RtPaused && Main.RtMode == "Modal", "pause owns the world");
        Expect(!Main.RtPlayerControlled && !Main.RtVanControlled, "pause disables gameplay input");
        Main.RtSetPlayerAirborne();
        Vector3 airborne = Main.RtPlayerPosition;
        await Wait(0.6f);
        Expect(airborne.DistanceTo(Main.RtPlayerPosition) < 0.001f,
            "player physics stops while paused");

        // Pause -> Settings -> Pause.
        Main.RtOpenSettingsFromPause();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Settings", "pause opens settings");
        Expect(Main.RtPaused && Main.RtMode == "Modal", "settings stays paused");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Paused" && Main.RtPaused &&
               Main.RtMode == "Modal", "settings returns to pause without unpausing");
        Main.RtResume();
        await Wait(0.25f);
        Expect(!Main.RtPaused && Main.RtMode == "Walking" && Main.RtPlayerControlled,
            "resume returns to walking");

        // Pause -> New Game confirmation -> Cancel -> Pause.
        Main.RtPause();
        await Wait(0.15f);
        Main.RtConfirmNewGame();
        await Wait(0.2f);
        Expect(Main.RtModalTitle == "Start a new game?" && Main.RtPaused, "new-game confirmation opens");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Paused" && Main.RtPaused &&
               Main.RtMode == "Modal", "cancelling new game returns to pause");
        Main.RtResume();
        await Wait(0.2f);

        // New Game reset through the same coordinator used by the button.
        var newGame = Main.RtStartNewGame();
        await Wait(0.25f);
        Expect(newGame.Success, "new game persists successfully");
        Expect(Main.RtMode == "Walking" && !Main.RtPaused, "new game resumes walking");
        Expect(Main.RtCoins == 0 && Main.RtCompletedCount == 0 && Main.RtActiveMission == "",
            "new game resets wallet, missions and active job");
        Expect(Main.RtForceAutosave(), "automatic save path succeeds");

        // Mission completion modal pauses, then resumes.
        Main.RtCompleteM01();
        await Wait(0.35f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "JOB COMPLETE!", "completion modal opens");
        Expect(Main.RtPaused && Main.RtMode == "Modal", "completion modal pauses gameplay");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(!Main.RtPaused && Main.RtMode == "Walking", "closing completion resumes walking");

        // Driving pause: input, physics and race timer must all stop.
        Main.RtEnterVan();
        await Wait(0.3f);
        Expect(Main.RtMode == "Driving" && Main.RtVanControlled && !Main.RtPlayerControlled,
            "entering the van switches control");
        Expect(!Main.RtPlayerVisible, "player is hidden while driving");
        Expect(Main.RtPrepareRace(), "race state prepared for timer test");

        Main.RtPause();
        await Wait(0.2f);
        double pausedRaceTime = Main.RtRaceElapsed;
        Vector3 parkedVan = Main.RtVanPosition;
        Input.ActionPress("move_forward");
        await Wait(0.6f);
        Input.ActionRelease("move_forward");
        Expect(Math.Abs(Main.RtRaceElapsed - pausedRaceTime) < 0.001,
            "race timer does not advance while paused");
        Expect(parkedVan.DistanceTo(Main.RtVanPosition) < 0.001f,
            "van does not move while paused");
        Expect(Main.RtMode == "Modal" && !Main.RtVanControlled, "driving pause releases van input");

        Main.RtResume();
        await Wait(0.75f);
        Expect(Main.RtMode == "Driving" && Main.RtVanControlled && !Main.RtPaused,
            "driving resumes after pause");
        Expect(Main.RtRaceElapsed > pausedRaceTime + 0.1, "race timer resumes after pause");

        Vector3 driveStart = Main.RtVanPosition;
        Input.ActionPress("move_forward");
        await Wait(0.5f);
        Input.ActionRelease("move_forward");
        await Wait(1.2f);
        Expect(driveStart.DistanceTo(Main.RtVanPosition) > 0.05f, "van drives after resume");

        Main.RtExitVan();
        await Wait(0.3f);
        Expect(Main.RtMode == "Walking" && Main.RtPlayerControlled && Main.RtPlayerVisible,
            "player exits the van");

        // Save while driving for the resume phase.
        Main.RtEnterVan();
        await Wait(0.3f);
        Expect(Main.RtMode == "Driving", "re-entered van for save");
        Expect(Main.RtSaveNow(), "manual save succeeds while driving");
        await Wait(0.2f);
    }

    private async Task ResumeChecks()
    {
        Expect(!Main.RtStarted, "resume phase starts at the title screen");
        Expect(Main.RtPaused && Main.RtMode == "Title", "resume title is paused");
        Expect(Main.RtCanContinue, "driving save offers Continue");

        Main.RtContinue();
        await Wait(0.45f);
        Expect(Main.RtStarted && !Main.RtPaused && Main.RtMode == "Driving",
            "Continue restores driving mode");
        Expect(Main.RtVanControlled && !Main.RtPlayerControlled && !Main.RtPlayerVisible,
            "driving control and hidden player restored");

        Vector3 hiddenStart = Main.RtPlayerPosition;
        Vector3 vanStart = Main.RtVanPosition;
        Input.ActionPress("move_forward");
        await Wait(0.5f);
        Input.ActionRelease("move_forward");
        await Wait(0.2f);
        Expect(hiddenStart.DistanceTo(Main.RtPlayerPosition) < 0.01f,
            "hidden player does not walk while driving after reload");
        Expect(vanStart.DistanceTo(Main.RtVanPosition) > 0.05f, "restored van drives");

        Main.RtPause();
        await Wait(0.2f);
        Expect(Main.RtMode == "Modal" && Main.RtPaused && !Main.RtVanControlled,
            "restored driving session pauses correctly");
        Main.RtResume();
        await Wait(0.25f);
        Expect(Main.RtMode == "Driving" && !Main.RtPaused && Main.RtVanControlled,
            "restored driving session resumes correctly");

        Main.RtExitVan();
        await Wait(0.3f);
        Expect(Main.RtMode == "Walking" && Main.RtPlayerControlled && Main.RtPlayerVisible,
            "restored session exits the van");
    }

    private async Task Wait(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds, processAlways: true),
            SceneTreeTimer.SignalName.Timeout);
    }

    private static bool HasArg(string needle)
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg == needle) return true;
        return false;
    }

    private void Expect(bool condition, string name)
    {
        if (condition)
        {
            _passed++;
            GD.Print("PASS  " + name);
        }
        else
        {
            Fail(name);
        }
    }

    private void Fail(string name)
    {
        _failed++;
        GD.PrintErr("FAIL  " + name);
    }
}
