using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Godot runtime verification for pause/processing and modal state. Run with:
///   godot --headless --path . -- --selftest
///   godot --headless --path . -- --selftest-resume
///   godot --headless --path . -- --selftest-recovery
/// It uses isolated user://selftest*.json data and exits non-zero on failure.
/// </summary>
public partial class RuntimeSelfTest : Node
{
    public Main Main { get; set; }

    // Static because the recovery phase deliberately reloads the real scene.
    internal static int RecoveryPhase { get; set; }

    private static int _passed;
    private static int _failed;
    private static Vector3 _savedPlayerPosition;
    private static byte[] _seedBytes;
    private static byte[] _newerOriginalBytes;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _ = Run();
    }

    private async Task Run()
    {
        bool reloading = false;
        try
        {
            await Wait(0.7f);
            if (HasArg("--selftest-resume"))
                await ResumeChecks();
            else if (HasArg("--selftest-recovery"))
                reloading = await RecoveryChecks();
            else
                await FreshSessionChecks();
        }
        catch (Exception ex)
        {
            Fail("unexpected runtime-test exception: " + ex);
        }

        if (reloading) return;
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

    // ------------------------------------------------- recovery integration
    // These phases reload the real scene so they exercise the same startup,
    // builder, pickup, snapshot and save path as a player relaunch.

    private async Task<bool> RecoveryChecks()
    {
        switch (RecoveryPhase)
        {
            case 0: return await RecoverySeedSave();
            case 1: return await RecoveryFailedNewGame();
            case 2: return await RecoveryDiscardedChanges();
            case 3: return await RecoverySuccessfulNewGame();
            case 4: return await RecoveryNoSave();
            case 5: return await RecoveryBackup();
            case 6: return await RecoveryAfterBackup();
            case 7: return await RecoveryNewerSave();
            case 8: return await RecoveryRelaunch();
            default:
                Fail("unknown recovery phase " + RecoveryPhase);
                return false;
        }
    }

    private async Task<bool> RecoverySeedSave()
    {
        SaveService.ClearTestWriteFailure();
        string path = Main.RtSavePath;
        SaveService.Save(BuildNineMissionSave(), path, Main.RtCatalog);
        Expect(File.Exists(path), "nine-mission seed save written");
        RecoveryPhase = 1;
        Main.RtReloadScene();
        return true;
    }

    private async Task<bool> RecoveryFailedNewGame()
    {
        string path = Main.RtSavePath;
        byte[] seed = File.ReadAllBytes(path);
        _seedBytes = seed;
        Expect(Main.RtCanContinue && Main.RtHasSaveFile, "seed save offers Continue");
        Expect(Main.RtMemoryMatchesPersisted, "loaded seed matches memory");

        Main.RtRequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Start a new game?",
            "new game confirmation opens from title");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(!Main.RtStarted && Main.RtMode == "Title", "cancelled confirmation returns to title");
        CheckBytes(path, seed, "cancelled confirmation leaves the save untouched");

        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected save failure")
                : null);
        var result = Main.RtStartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!result.Success, "failed New Game reports failure");
        Expect(Main.RtStarted && Main.RtCoins == 0 && Main.RtCompletedCount == 0,
            "failed New Game runs the new in-memory session");
        Expect(!Main.RtMemoryMatchesPersisted, "failed New Game is not marked persisted");
        CheckBytes(path, seed, "failed New Game preserves the previous save");

        Main.RtReturnToTitle();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Return to title without saving?",
            "failed return-to-title asks before discarding");
        Main.RtReturnAnyway();
        await Wait(0.25f);
        Expect(!Main.RtStarted && Main.RtMode == "Title", "return anyway shows the title");
        Expect(Main.RtCanContinue && !Main.RtMemoryMatchesPersisted,
            "Continue is still offered for the persisted save, not discarded memory");
        Expect(Main.RtTitleNotice.Contains("last successfully saved"),
            "title explains that Continue reloads the persisted village");
        CheckBytes(path, seed, "discarding the new session leaves the save untouched");

        SaveService.ClearTestWriteFailure();
        RecoveryPhase = 2;
        Main.RtContinue();
        bool reloadRequested = Main.RtContinueReloadPending;
        Expect(reloadRequested, "Continue requested a persisted-session reload");
        return reloadRequested;
    }

    private async Task<bool> RecoveryDiscardedChanges()
    {
        string path = Main.RtSavePath;
        Expect(Main.RtStarted && Main.RtCoins == 130 && Main.RtCompletedCount == 9,
            "Continue restored the persisted nine-mission village");
        Expect(Main.RtMemoryMatchesPersisted, "reloaded village matches memory");
        Expect(Main.RtBridgeBuilt && Main.RtSwingPieceCount == 6 && Main.RtParkPlacementCount == 3,
            "Continue rebuilt bridge, swings and park");
        Expect(Main.RtPlayerPosition.DistanceTo(new Vector3(-38.8f, -0.04f, 21.4f)) < 0.1f,
            "Continue restored the saved player position");
        Expect(Main.RtActiveMission == "M10", "Continue restored the active mission");
        Expect(Main.RtCanContinue, "loaded save remains continuable");
        CheckBytes(path, _seedBytes, "Continue reload does not overwrite the persisted save");

        // Settings failures must be visible in the settings modal and must not
        // consume the automatic-failure throttle used by autosave.
        Main.RtPause();
        await Wait(0.2f);
        Main.RtOpenSettingsFromPause();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Settings", "settings modal opens from pause");
        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected settings failure")
                : null);
        Main.RtToggleMusic();
        await Wait(0.15f);
        Expect(Main.RtModalStatusVisible && Main.RtModalStatus.Contains("could not be saved"),
            "settings failure is shown inside the modal");
        Expect(!Main.RtMusicEnabled, "settings change takes effect in memory before persistence");
        Expect(Main.RtModalTitle == "Settings", "settings failure keeps the modal open");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Paused" && Main.RtPaused,
            "settings returns to pause after failure");
        Main.RtResume();
        await Wait(0.2f);

        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected autosave failure")
                : null);
        Expect(!Main.RtForceAutosave(), "autosave failure is reported");
        await Wait(0.1f);
        Expect(Main.RtToastVisible && Main.RtToastText.Contains("Autosave failed"),
            "first autosave warning survives a settings failure");
        SaveService.ClearTestWriteFailure();
        Expect(Main.RtForceAutosave(), "autosave succeeds after recovery");

        Main.RtPause();
        await Wait(0.2f);
        Main.RtOpenSettingsFromPause();
        await Wait(0.2f);
        Main.RtToggleMusic();
        await Wait(0.15f);
        Expect(Main.RtModalStatusVisible && Main.RtModalStatus.Contains("saved"),
            "settings retry replaces the failure message");
        Expect(Main.RtMusicEnabled, "settings retry applies the persisted value");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Paused" && Main.RtPaused,
            "settings returns to pause after success");
        Main.RtResume();
        await Wait(0.2f);

        // Scenario B: a discarded gameplay change must not be continued.
        byte[] persisted = File.ReadAllBytes(path);
        _savedPlayerPosition = Main.RtPlayerPosition;
        Input.ActionPress("move_forward");
        await Wait(0.55f);
        Input.ActionRelease("move_forward");
        await Wait(0.2f);
        Expect(_savedPlayerPosition.DistanceTo(Main.RtPlayerPosition) > 0.05f,
            "unsaved gameplay movement changed the live session");
        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected return failure")
                : null);
        Main.RtReturnToTitle();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Return to title without saving?",
            "failed return offers discard");
        Main.RtReturnAnyway();
        await Wait(0.25f);
        Expect(!Main.RtStarted && Main.RtCanContinue && !Main.RtMemoryMatchesPersisted,
            "discarded movement leaves Continue pointing at the persisted save");
        CheckBytes(path, persisted, "failed return leaves the disk save unchanged");
        SaveService.ClearTestWriteFailure();
        RecoveryPhase = 3;
        Main.RtContinue();
        return true;
    }

    private async Task<bool> RecoverySuccessfulNewGame()
    {
        string path = Main.RtSavePath;
        Expect(Main.RtStarted && Main.RtCoins == 130 && Main.RtCompletedCount == 9,
            "Continue after discard restores persisted progress");
        Expect(_savedPlayerPosition.DistanceTo(Main.RtPlayerPosition) < 0.1f,
            "Continue after discard restores the persisted player position");
        Expect(Main.RtActiveMission == "M10", "Continue after discard restores the active mission");

        // Scenario C: a successful New Game becomes the persisted session.
        Main.RtReturnToTitle();
        await Wait(0.25f);
        Expect(!Main.RtStarted && Main.RtCanContinue && Main.RtMemoryMatchesPersisted,
            "successful return keeps the current session continuable");
        var result = Main.RtStartNewGameFromTitleConfirmed();
        await Wait(0.35f);
        Expect(result.Success, "successful New Game persists");
        Expect(Main.RtStarted && Main.RtCoins == 0 && Main.RtCompletedCount == 0,
            "successful New Game starts at zero progress");
        Expect(Main.RtMemoryMatchesPersisted, "successful New Game is marked persisted");
        var disk = SaveService.Load(path, Main.RtCatalog);
        Expect(disk.Success && disk.Data.Coins == 0 && disk.Data.CompletedMissions.Count == 0,
            "successful New Game is on disk");

        Main.RtReturnToTitle();
        await Wait(0.25f);
        Expect(Main.RtCanContinue && Main.RtMemoryMatchesPersisted,
            "title continues the successful new session");
        Main.RtContinue();
        await Wait(0.35f);
        Expect(Main.RtStarted && Main.RtCoins == 0 && Main.RtCompletedCount == 0,
            "Continue restores the successful new session, not the previous village");

        // Scenario D starts from no save at all.
        File.Delete(path);
        if (File.Exists(SaveService.BackupPath(path))) File.Delete(SaveService.BackupPath(path));
        RecoveryPhase = 4;
        Main.RtReloadScene();
        return true;
    }

    private async Task<bool> RecoveryNoSave()
    {
        string path = Main.RtSavePath;
        Expect(!Main.RtStarted && !Main.RtHasSaveFile && !Main.RtCanContinue,
            "missing save shows no false Continue");
        Expect(!File.Exists(path), "no save is present for the first-ever attempt");
        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected first-save failure")
                : null);
        var result = Main.RtStartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!result.Success, "failed first-ever New Game reports failure");
        Expect(Main.RtStarted && Main.RtCoins == 0, "failed first-ever New Game still runs");
        Expect(!Main.RtHasSaveFile && !Main.RtCanContinue && !Main.RtMemoryMatchesPersisted,
            "failed first-ever save creates no false Continue state");
        Expect(!File.Exists(path), "failed first-ever save writes no file");

        Main.RtReturnToTitle();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Return to title without saving?",
            "failed first-ever return asks before discarding");
        Main.RtReturnAnyway();
        await Wait(0.25f);
        Expect(!Main.RtStarted && !Main.RtCanContinue && !Main.RtHasSaveFile,
            "failed first-ever session has no Continue after discard");
        Expect(!Main.RtTitleNotice.Contains("Continue loads"),
            "no-save discard does not promise a Continue that does not exist");
        SaveService.ClearTestWriteFailure();

        Main.RtRequestNewGameFromTitle();
        await Wait(0.3f);
        Expect(Main.RtStarted && Main.RtCoins == 0 && Main.RtCompletedCount == 0,
            "Start adventure resets a discarded session when no save exists");
        Expect(File.Exists(path), "Start adventure writes the fresh no-save session");

        // Scenario E: create a valid backup-only recovery seed.
        File.Delete(path);
        if (File.Exists(SaveService.BackupPath(path))) File.Delete(SaveService.BackupPath(path));
        SaveService.Save(BuildNineMissionSave(), path, Main.RtCatalog);
        File.Copy(path, SaveService.BackupPath(path), true);
        File.Delete(path);
        Expect(!File.Exists(path) && File.Exists(SaveService.BackupPath(path)),
            "backup-only recovery seed prepared");
        RecoveryPhase = 5;
        Main.RtReloadScene();
        return true;
    }

    private async Task<bool> RecoveryBackup()
    {
        string path = Main.RtSavePath;
        string backup = SaveService.BackupPath(path);
        byte[] backupBytes = File.ReadAllBytes(backup);
        Expect(!Main.RtStarted && Main.RtCanContinue && Main.RtHasSaveFile,
            "backup recovery offers Continue");
        Expect(Main.RtMemoryMatchesPersisted, "recovered backup matches memory");

        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected backup replacement failure")
                : null);
        var result = Main.RtStartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!result.Success && Main.RtStarted, "failed New Game after recovery is reported");
        Expect(Main.RtCoins == 0 && Main.RtCompletedCount == 0, "failed recovery reset is in memory");
        Expect(!File.Exists(path), "failed write does not create a main save");
        CheckBytes(backup, backupBytes, "failed write leaves the recovered backup untouched");
        Main.RtReturnToTitle();
        await Wait(0.2f);
        Expect(Main.RtModalTitle == "Return to title without saving?", "recovered backup discard is confirmed");
        Main.RtReturnAnyway();
        await Wait(0.25f);
        Expect(Main.RtCanContinue && !Main.RtMemoryMatchesPersisted,
            "recovered backup remains continuable after discard");
        SaveService.ClearTestWriteFailure();
        RecoveryPhase = 6;
        Main.RtContinue();
        return true;
    }

    private async Task<bool> RecoveryAfterBackup()
    {
        string path = Main.RtSavePath;
        Expect(Main.RtStarted && Main.RtCoins == 130 && Main.RtCompletedCount == 9,
            "Continue after recovered backup restores the backup village");
        Expect(Main.RtMemoryMatchesPersisted, "recovered Continue matches memory");

        // Scenario: an unsupported newer save must remain protected until the
        // player explicitly confirms New Game.
        File.WriteAllText(path, "{\"SchemaVersion\":99,\"Coins\":5}");
        _newerOriginalBytes = File.ReadAllBytes(path);
        RecoveryPhase = 7;
        Main.RtReloadScene();
        return true;
    }

    private async Task<bool> RecoveryNewerSave()
    {
        string path = Main.RtSavePath;
        string directory = Path.GetDirectoryName(path);
        string newerPattern = Path.GetFileName(path) + ".newer-v99-*";
        Expect(!Main.RtStarted && !Main.RtCanContinue, "unsupported newer save has no Continue");
        Expect(Main.RtNewerSavePresent && !Main.RtNewerReplacementAuthorized,
            "newer save is protected before confirmation");
        Expect(Main.RtTitleNotice.Contains("newer"), "title explains the newer save");
        CheckBytes(path, _newerOriginalBytes, "newer save is untouched on load");

        Main.RtRequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Start a new game?",
            "newer-save replacement confirmation opens");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(!Main.RtStarted && !Main.RtNewerReplacementAuthorized,
            "cancelled confirmation grants no replacement permission");
        CheckBytes(path, _newerOriginalBytes, "cancelled confirmation leaves newer save untouched");

        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected newer replacement failure")
                : null);
        var failed = Main.RtStartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!failed.Success, "failed newer-save replacement is reported");
        Expect(Main.RtStarted && Main.RtNewerSavePresent && Main.RtNewerReplacementAuthorized,
            "failed replacement keeps the confirmed permission for Save Now");
        Expect(Main.RtCoins == 0 && Main.RtCompletedCount == 0, "newer replacement session is in memory");
        CheckBytes(path, _newerOriginalBytes, "failed replacement leaves newer save untouched");
        Expect(Directory.GetFiles(directory, newerPattern).Length == 0,
            "failed replacement creates no misleading preserved copy");

        Main.RtReturnToTitle();
        await Wait(0.2f);
        Expect(Main.RtModalTitle == "Return to title without saving?", "newer replacement discard is confirmed");
        Main.RtReturnAnyway();
        await Wait(0.25f);
        Expect(!Main.RtCanContinue && Main.RtNewerSavePresent && !Main.RtNewerReplacementAuthorized,
            "discard clears replacement permission and keeps newer protection");
        Expect(Main.RtTitleNotice.Contains("newer") && Main.RtTitleNotice.Contains("protected"),
            "newer-save discard notice explains the protected file");
        CheckBytes(path, _newerOriginalBytes, "discard leaves newer save untouched");

        var failedAgain = Main.RtStartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!failedAgain.Success && Main.RtNewerReplacementAuthorized,
            "second confirmed replacement regains retry permission");
        SaveService.ClearTestWriteFailure();
        Expect(Main.RtSaveNow(), "Save Now retries and replaces the newer save");
        await Wait(0.2f);
        Expect(!Main.RtNewerSavePresent && !Main.RtNewerReplacementAuthorized,
            "successful replacement clears newer protection and permission");
        Expect(Main.RtMemoryMatchesPersisted && Main.RtCoins == 0 && Main.RtCompletedCount == 0,
            "replacement save records the new session");
        var loaded = SaveService.Load(path, Main.RtCatalog);
        Expect(loaded.Success && loaded.Data.SchemaVersion == SaveService.CurrentVersion,
            "replacement file is a current-schema save");
        string[] preserved = Directory.GetFiles(directory, newerPattern);
        Expect(preserved.Length == 1, "exactly one preserved newer-version copy exists");
        if (preserved.Length == 1)
            CheckBytes(preserved[0], _newerOriginalBytes, "preserved copy matches the original newer save");

        Main.RtReturnToTitle();
        await Wait(0.25f);
        Expect(Main.RtCanContinue && Main.RtMemoryMatchesPersisted,
            "successful replacement session is continuable");
        Main.RtContinue();
        await Wait(0.35f);
        Expect(Main.RtStarted && Main.RtCoins == 0 && Main.RtCompletedCount == 0,
            "Continue loads the successful replacement session");

        RecoveryPhase = 8;
        Main.RtReloadScene();
        return true;
    }

    private async Task<bool> RecoveryRelaunch()
    {
        Expect(!Main.RtStarted && Main.RtCanContinue && Main.RtMemoryMatchesPersisted,
            "replacement session survives a relaunch");
        Expect(!Main.RtNewerSavePresent, "relaunched replacement is a normal save");
        Main.RtContinue();
        await Wait(0.35f);
        Expect(Main.RtStarted && Main.RtCoins == 0 && Main.RtCompletedCount == 0,
            "Continue after relaunch restores the replacement new game");
        return false;
    }

    private static GameData BuildNineMissionSave()
    {
        var data = new GameData { Coins = 130, ActiveMission = "M10" };
        data.CompletedMissions.AddRange(new[] { "M01", "M02", "M03", "M04", "M05", "M06", "M07", "M08", "M09" });
        data.TutorialsSeen.AddRange(new[] { "walk", "drive", "build" });
        data.UnlockedCosmetics.Add("red_cap");
        data.BridgePieces.AddRange(BuildRules.BridgeSockets);
        data.SwingPieces.AddRange(BuildRules.SwingSockets);
        data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
        data.ParkPlacements.Add(new PlacedItem("flowerbed", "park1"));
        data.ParkPlacements.Add(new PlacedItem("lamp", "park2"));
        data.MissionProgress["M01"] = new Dictionary<string, int> { ["delivered"] = 1 };
        data.MissionProgress["M02"] = new Dictionary<string, int> { ["delivered"] = 1 };
        data.MissionProgress["M04"] = new Dictionary<string, int>
        {
            ["bread"] = 2, ["milk"] = 1, ["delivered"] = 1,
        };
        data.MissionProgress["M05"] = new Dictionary<string, int>
        {
            ["checkpoint"] = 4, ["started"] = 1, ["elapsedMs"] = 30_000,
        };
        data.MissionProgress["M06"] = new Dictionary<string, int> { ["blueDelivered"] = 2, ["orangeDelivered"] = 1 };
        data.MissionProgress["M09"] = new Dictionary<string, int>
        {
            ["checkpoint"] = 4, ["started"] = 1, ["elapsedMs"] = 30_000,
        };
        data.Player = new PlayerSnapshot
        {
            X = -38.8f, Y = -0.04f, Z = 21.4f, RotationY = 3.14f, Valid = true,
        };
        data.Van = new VehicleSnapshot
        {
            X = -30f, Y = -0.05f, Z = 14.5f, RotationY = 0f, Valid = true,
        };
        return data;
    }

    private void CheckBytes(string path, byte[] expected, string name)
    {
        try
        {
            Expect(File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(expected), name);
        }
        catch (Exception ex)
        {
            Fail(name + ": " + ex.Message);
        }
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
