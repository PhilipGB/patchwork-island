using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

public partial class RuntimeSelfTest
{
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
        string path = Main.Probe.SavePath;
        SaveService.Save(BuildNineMissionSave(), path, Main.Probe.Catalog);
        Expect(File.Exists(path), "nine-mission seed save written");
        RecoveryPhase = 1;
        Main.Probe.ReloadScene();
        return true;
    }

    private async Task<bool> RecoveryFailedNewGame()
    {
        string path = Main.Probe.SavePath;
        byte[] seed = File.ReadAllBytes(path);
        _seedBytes = seed;
        Expect(Main.Probe.CanContinue && Main.Probe.HasSaveFile, "seed save offers Continue");
        Expect(Main.Probe.MemoryMatchesPersisted, "loaded seed matches memory");

        Main.Probe.RequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Start a new game?",
            "new game confirmation opens from title");
        Main.Probe.CloseModal();
        await Wait(0.25f);
        Expect(!Main.Probe.Started && Main.Probe.Mode == "Title", "cancelled confirmation returns to title");
        CheckBytes(path, seed, "cancelled confirmation leaves the save untouched");

        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected save failure")
                : null);
        var result = Main.Probe.StartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!result.Success, "failed New Game reports failure");
        Expect(Main.Probe.Started && Main.Probe.Coins == 0 && Main.Probe.CompletedCount == 0,
            "failed New Game runs the new in-memory session");
        Expect(!Main.Probe.MemoryMatchesPersisted, "failed New Game is not marked persisted");
        CheckBytes(path, seed, "failed New Game preserves the previous save");

        Main.Probe.ReturnToTitle();
        await Wait(0.2f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Return to title without saving?",
            "failed return-to-title asks before discarding");
        Main.Probe.ReturnAnyway();
        await Wait(0.25f);
        Expect(!Main.Probe.Started && Main.Probe.Mode == "Title", "return anyway shows the title");
        Expect(Main.Probe.CanContinue && !Main.Probe.MemoryMatchesPersisted,
            "Continue is still offered for the persisted save, not discarded memory");
        Expect(Main.Probe.TitleNotice.Contains("last successfully saved"),
            "title explains that Continue reloads the persisted village");
        CheckBytes(path, seed, "discarding the new session leaves the save untouched");

        SaveService.ClearTestWriteFailure();
        RecoveryPhase = 2;
        Main.Probe.Continue();
        bool reloadRequested = Main.Probe.ContinueReloadPending;
        Expect(reloadRequested, "Continue requested a persisted-session reload");
        return reloadRequested;
    }

    private async Task<bool> RecoveryDiscardedChanges()
    {
        string path = Main.Probe.SavePath;
        Expect(Main.Probe.Started && Main.Probe.Coins == 130 && Main.Probe.CompletedCount == 9,
            "Continue restored the persisted nine-mission village");
        Expect(Main.Probe.MemoryMatchesPersisted, "reloaded village matches memory");
        Expect(Main.Probe.BridgeBuilt && Main.Probe.SwingPieceCount == 6 && Main.Probe.ParkPlacementCount == 3,
            "Continue rebuilt bridge, swings and park");
        Expect(Main.Probe.PlayerPosition.DistanceTo(new Vector3(-38.8f, -0.04f, 21.4f)) < 0.1f,
            "Continue restored the saved player position");
        Expect(Main.Probe.ActiveMission == "M10", "Continue restored the active mission");
        Expect(Main.Probe.CharacterPreset == CharacterCustomisation.Girl &&
               Main.Probe.PlayerPreset == CharacterCustomisation.Girl && Main.Probe.PlayerWearsDress,
            "Continue reload restores the saved girl character");
        Expect(Main.Probe.EquippedOutfit == "berry" && Main.Probe.OwnsOutfit("berry") &&
               Main.Probe.PlayerOutfit == "berry",
            "Continue reload restores the saved outfit ownership and appearance");
        Expect(Main.Probe.CanContinue, "loaded save remains continuable");
        CheckBytes(path, _seedBytes, "Continue reload does not overwrite the persisted save");

        // Settings failures must be visible in the settings modal and must not
        // consume the automatic-failure throttle used by autosave.
        Main.Probe.Pause();
        await Wait(0.2f);
        Main.Probe.OpenSettingsFromPause();
        await Wait(0.2f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Settings", "settings modal opens from pause");
        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected settings failure")
                : null);
        Main.Probe.ToggleMusic();
        await Wait(0.15f);
        Expect(Main.Probe.ModalStatusVisible && Main.Probe.ModalStatus.Contains("could not be saved"),
            "settings failure is shown inside the modal");
        Expect(!Main.Probe.MusicEnabled, "settings change takes effect in memory before persistence");
        Expect(Main.Probe.ModalTitle == "Settings", "settings failure keeps the modal open");
        Main.Probe.CloseModal();
        await Wait(0.25f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Paused" && Main.Probe.Paused,
            "settings returns to pause after failure");
        Main.Probe.Resume();
        await Wait(0.2f);

        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected autosave failure")
                : null);
        Expect(!Main.Probe.ForceAutosave(), "autosave failure is reported");
        await Wait(0.1f);
        Expect(Main.Probe.ToastVisible && Main.Probe.ToastText.Contains("Autosave failed"),
            "first autosave warning survives a settings failure");
        SaveService.ClearTestWriteFailure();
        Expect(Main.Probe.ForceAutosave(), "autosave succeeds after recovery");

        Main.Probe.Pause();
        await Wait(0.2f);
        Main.Probe.OpenSettingsFromPause();
        await Wait(0.2f);
        Main.Probe.ToggleMusic();
        await Wait(0.15f);
        Expect(Main.Probe.ModalStatusVisible && Main.Probe.ModalStatus.Contains("saved"),
            "settings retry replaces the failure message");
        Expect(Main.Probe.MusicEnabled, "settings retry applies the persisted value");
        Main.Probe.CloseModal();
        await Wait(0.25f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Paused" && Main.Probe.Paused,
            "settings returns to pause after success");
        Main.Probe.Resume();
        await Wait(0.2f);

        // Scenario B: a discarded gameplay change must not be continued.
        byte[] persisted = File.ReadAllBytes(path);
        _savedPlayerPosition = Main.Probe.PlayerPosition;
        Input.ActionPress("move_forward");
        await Wait(0.55f);
        Input.ActionRelease("move_forward");
        await Wait(0.2f);
        Expect(_savedPlayerPosition.DistanceTo(Main.Probe.PlayerPosition) > 0.05f,
            "unsaved gameplay movement changed the live session");
        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected return failure")
                : null);
        Main.Probe.ReturnToTitle();
        await Wait(0.2f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Return to title without saving?",
            "failed return offers discard");
        Main.Probe.ReturnAnyway();
        await Wait(0.25f);
        Expect(!Main.Probe.Started && Main.Probe.CanContinue && !Main.Probe.MemoryMatchesPersisted,
            "discarded movement leaves Continue pointing at the persisted save");
        CheckBytes(path, persisted, "failed return leaves the disk save unchanged");
        SaveService.ClearTestWriteFailure();
        RecoveryPhase = 3;
        Main.Probe.Continue();
        return true;
    }

    private async Task<bool> RecoverySuccessfulNewGame()
    {
        string path = Main.Probe.SavePath;
        Expect(Main.Probe.Started && Main.Probe.Coins == 130 && Main.Probe.CompletedCount == 9,
            "Continue after discard restores persisted progress");
        Expect(_savedPlayerPosition.DistanceTo(Main.Probe.PlayerPosition) < 0.1f,
            "Continue after discard restores the persisted player position");
        Expect(Main.Probe.ActiveMission == "M10", "Continue after discard restores the active mission");

        // Scenario C: a successful New Game becomes the persisted session.
        Main.Probe.ReturnToTitle();
        await Wait(0.25f);
        Expect(!Main.Probe.Started && Main.Probe.CanContinue && Main.Probe.MemoryMatchesPersisted,
            "successful return keeps the current session continuable");
        var result = Main.Probe.StartNewGameFromTitleConfirmed();
        await Wait(0.35f);
        Expect(result.Success, "successful New Game persists");
        Expect(Main.Probe.Started && Main.Probe.Coins == 0 && Main.Probe.CompletedCount == 0,
            "successful New Game starts at zero progress");
        Expect(Main.Probe.MemoryMatchesPersisted, "successful New Game is marked persisted");
        var disk = SaveService.Load(path, Main.Probe.Catalog);
        Expect(disk.Success && disk.Data.Coins == 0 && disk.Data.CompletedMissions.Count == 0,
            "successful New Game is on disk");

        Main.Probe.ReturnToTitle();
        await Wait(0.25f);
        Expect(Main.Probe.CanContinue && Main.Probe.MemoryMatchesPersisted,
            "title continues the successful new session");
        Main.Probe.Continue();
        await Wait(0.35f);
        Expect(Main.Probe.Started && Main.Probe.Coins == 0 && Main.Probe.CompletedCount == 0,
            "Continue restores the successful new session, not the previous village");

        // Scenario D starts from no save at all.
        File.Delete(path);
        if (File.Exists(SaveService.BackupPath(path))) File.Delete(SaveService.BackupPath(path));
        RecoveryPhase = 4;
        Main.Probe.ReloadScene();
        return true;
    }

    private async Task<bool> RecoveryNoSave()
    {
        string path = Main.Probe.SavePath;
        Expect(!Main.Probe.Started && !Main.Probe.HasSaveFile && !Main.Probe.CanContinue,
            "missing save shows no false Continue");
        Expect(!File.Exists(path), "no save is present for the first-ever attempt");
        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected first-save failure")
                : null);
        var result = Main.Probe.StartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!result.Success, "failed first-ever New Game reports failure");
        Expect(Main.Probe.Started && Main.Probe.Coins == 0, "failed first-ever New Game still runs");
        Expect(!Main.Probe.HasSaveFile && !Main.Probe.CanContinue && !Main.Probe.MemoryMatchesPersisted,
            "failed first-ever save creates no false Continue state");
        Expect(!File.Exists(path), "failed first-ever save writes no file");

        Main.Probe.ReturnToTitle();
        await Wait(0.2f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Return to title without saving?",
            "failed first-ever return asks before discarding");
        Main.Probe.ReturnAnyway();
        await Wait(0.25f);
        Expect(!Main.Probe.Started && !Main.Probe.CanContinue && !Main.Probe.HasSaveFile,
            "failed first-ever session has no Continue after discard");
        Expect(!Main.Probe.TitleNotice.Contains("Continue loads"),
            "no-save discard does not promise a Continue that does not exist");
        SaveService.ClearTestWriteFailure();

        Main.Probe.RequestNewGameFromTitle();
        await Wait(0.3f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Who would you like to play?",
            "no-save Start adventure asks for a character");
        var startedAfterDiscard = Main.Probe.StartNewGameFromTitleConfirmed(CharacterCustomisation.Boy);
        await Wait(0.35f);
        Expect(startedAfterDiscard.Success && Main.Probe.Started && Main.Probe.Coins == 0 &&
               Main.Probe.CompletedCount == 0,
            "choosing a character resets a discarded session when no save exists");
        Expect(File.Exists(path), "Start adventure writes the fresh no-save session");

        // Scenario E: create a valid backup-only recovery seed.
        File.Delete(path);
        if (File.Exists(SaveService.BackupPath(path))) File.Delete(SaveService.BackupPath(path));
        SaveService.Save(BuildNineMissionSave(), path, Main.Probe.Catalog);
        File.Copy(path, SaveService.BackupPath(path), true);
        File.Delete(path);
        Expect(!File.Exists(path) && File.Exists(SaveService.BackupPath(path)),
            "backup-only recovery seed prepared");
        RecoveryPhase = 5;
        Main.Probe.ReloadScene();
        return true;
    }

    private async Task<bool> RecoveryBackup()
    {
        string path = Main.Probe.SavePath;
        string backup = SaveService.BackupPath(path);
        byte[] backupBytes = File.ReadAllBytes(backup);
        Expect(!Main.Probe.Started && Main.Probe.CanContinue && Main.Probe.HasSaveFile,
            "backup recovery offers Continue");
        Expect(Main.Probe.MemoryMatchesPersisted, "recovered backup matches memory");

        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected backup replacement failure")
                : null);
        var result = Main.Probe.StartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!result.Success && Main.Probe.Started, "failed New Game after recovery is reported");
        Expect(Main.Probe.Coins == 0 && Main.Probe.CompletedCount == 0, "failed recovery reset is in memory");
        Expect(!File.Exists(path), "failed write does not create a main save");
        CheckBytes(backup, backupBytes, "failed write leaves the recovered backup untouched");
        Main.Probe.ReturnToTitle();
        await Wait(0.2f);
        Expect(Main.Probe.ModalTitle == "Return to title without saving?", "recovered backup discard is confirmed");
        Main.Probe.ReturnAnyway();
        await Wait(0.25f);
        Expect(Main.Probe.CanContinue && !Main.Probe.MemoryMatchesPersisted,
            "recovered backup remains continuable after discard");
        SaveService.ClearTestWriteFailure();
        RecoveryPhase = 6;
        Main.Probe.Continue();
        return true;
    }

    private async Task<bool> RecoveryAfterBackup()
    {
        string path = Main.Probe.SavePath;
        Expect(Main.Probe.Started && Main.Probe.Coins == 130 && Main.Probe.CompletedCount == 9,
            "Continue after recovered backup restores the backup village");
        Expect(Main.Probe.MemoryMatchesPersisted, "recovered Continue matches memory");

        // Scenario: an unsupported newer save must remain protected until the
        // player explicitly confirms New Game.
        File.WriteAllText(path, "{\"SchemaVersion\":99,\"Coins\":5}");
        _newerOriginalBytes = File.ReadAllBytes(path);
        RecoveryPhase = 7;
        Main.Probe.ReloadScene();
        return true;
    }

    private async Task<bool> RecoveryNewerSave()
    {
        string path = Main.Probe.SavePath;
        string directory = Path.GetDirectoryName(path);
        string newerPattern = Path.GetFileName(path) + ".newer-v99-*";
        Expect(!Main.Probe.Started && !Main.Probe.CanContinue, "unsupported newer save has no Continue");
        Expect(Main.Probe.NewerSavePresent && !Main.Probe.NewerReplacementAuthorized,
            "newer save is protected before confirmation");
        Expect(Main.Probe.TitleNotice.Contains("newer"), "title explains the newer save");
        CheckBytes(path, _newerOriginalBytes, "newer save is untouched on load");

        Main.Probe.RequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Start a new game?",
            "newer-save replacement confirmation opens");
        Main.Probe.CloseModal();
        await Wait(0.25f);
        Expect(!Main.Probe.Started && !Main.Probe.NewerReplacementAuthorized,
            "cancelled confirmation grants no replacement permission");
        CheckBytes(path, _newerOriginalBytes, "cancelled confirmation leaves newer save untouched");

        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected newer replacement failure")
                : null);
        var failed = Main.Probe.StartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!failed.Success, "failed newer-save replacement is reported");
        Expect(Main.Probe.Started && Main.Probe.NewerSavePresent && Main.Probe.NewerReplacementAuthorized,
            "failed replacement keeps the confirmed permission for Save Now");
        Expect(Main.Probe.Coins == 0 && Main.Probe.CompletedCount == 0, "newer replacement session is in memory");
        CheckBytes(path, _newerOriginalBytes, "failed replacement leaves newer save untouched");
        Expect(Directory.GetFiles(directory, newerPattern).Length == 0,
            "failed replacement creates no misleading preserved copy");

        Main.Probe.ReturnToTitle();
        await Wait(0.2f);
        Expect(Main.Probe.ModalTitle == "Return to title without saving?", "newer replacement discard is confirmed");
        Main.Probe.ReturnAnyway();
        await Wait(0.25f);
        Expect(!Main.Probe.CanContinue && Main.Probe.NewerSavePresent && !Main.Probe.NewerReplacementAuthorized,
            "discard clears replacement permission and keeps newer protection");
        Expect(Main.Probe.TitleNotice.Contains("newer") && Main.Probe.TitleNotice.Contains("protected"),
            "newer-save discard notice explains the protected file");
        CheckBytes(path, _newerOriginalBytes, "discard leaves newer save untouched");

        var failedAgain = Main.Probe.StartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!failedAgain.Success && Main.Probe.NewerReplacementAuthorized,
            "second confirmed replacement regains retry permission");
        SaveService.ClearTestWriteFailure();
        Expect(Main.Probe.SaveNow(), "Save Now retries and replaces the newer save");
        await Wait(0.2f);
        Expect(!Main.Probe.NewerSavePresent && !Main.Probe.NewerReplacementAuthorized,
            "successful replacement clears newer protection and permission");
        Expect(Main.Probe.MemoryMatchesPersisted && Main.Probe.Coins == 0 && Main.Probe.CompletedCount == 0,
            "replacement save records the new session");
        var loaded = SaveService.Load(path, Main.Probe.Catalog);
        Expect(loaded.Success && loaded.Data.SchemaVersion == SaveService.CurrentVersion,
            "replacement file is a current-schema save");
        string[] preserved = Directory.GetFiles(directory, newerPattern);
        Expect(preserved.Length == 1, "exactly one preserved newer-version copy exists");
        if (preserved.Length == 1)
            CheckBytes(preserved[0], _newerOriginalBytes, "preserved copy matches the original newer save");

        Main.Probe.ReturnToTitle();
        await Wait(0.25f);
        Expect(Main.Probe.CanContinue && Main.Probe.MemoryMatchesPersisted,
            "successful replacement session is continuable");
        Main.Probe.Continue();
        await Wait(0.35f);
        Expect(Main.Probe.Started && Main.Probe.Coins == 0 && Main.Probe.CompletedCount == 0,
            "Continue loads the successful replacement session");

        RecoveryPhase = 8;
        Main.Probe.ReloadScene();
        return true;
    }

    private async Task<bool> RecoveryRelaunch()
    {
        Expect(!Main.Probe.Started && Main.Probe.CanContinue && Main.Probe.MemoryMatchesPersisted,
            "replacement session survives a relaunch");
        Expect(!Main.Probe.NewerSavePresent, "relaunched replacement is a normal save");
        Main.Probe.Continue();
        await Wait(0.35f);
        Expect(Main.Probe.Started && Main.Probe.Coins == 0 && Main.Probe.CompletedCount == 0,
            "Continue after relaunch restores the replacement new game");
        return false;
    }

    private static GameData BuildNineMissionSave()
    {
        var data = new GameData
        {
            Coins = 130,
            ActiveMission = "M10",
            CharacterPreset = CharacterCustomisation.Girl,
            EquippedOutfit = "berry",
        };
        data.CompletedMissions.AddRange(new[] { "M01", "M02", "M03", "M04", "M05", "M06", "M07", "M08", "M09" });
        data.TutorialsSeen.AddRange(new[] { "walk", "drive", "build" });
        data.UnlockedCosmetics.Add("red_cap");
        data.OwnedOutfits.Add("berry");
        data.BridgePieces.AddRange(BuildRules.BridgeSockets);
        data.SwingPieces.AddRange(BuildRules.SwingSockets);
        data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
        data.ParkPlacements.Add(new PlacedItem("flowerbed", "park1"));
        data.ParkPlacements.Add(new PlacedItem("lamp", "park2"));
        data.MissionProgress["M01"] = new Dictionary<string, int> { ["delivered"] = 1 };
        data.MissionProgress["M02"] = new Dictionary<string, int> { ["delivered"] = 1 };
        data.MissionProgress["M04"] = new Dictionary<string, int>
        {
            ["bread"] = 2,
            ["milk"] = 1,
            ["delivered"] = 1,
        };
        data.MissionProgress["M05"] = new Dictionary<string, int>
        {
            ["checkpoint"] = 4,
            ["started"] = 1,
            ["elapsedMs"] = 30_000,
        };
        data.MissionProgress["M06"] = new Dictionary<string, int> { ["blueDelivered"] = 2, ["orangeDelivered"] = 1 };
        data.MissionProgress["M09"] = new Dictionary<string, int>
        {
            ["checkpoint"] = 4,
            ["started"] = 1,
            ["elapsedMs"] = 30_000,
        };
        data.Player = new PlayerSnapshot
        {
            X = -38.8f,
            Y = -0.04f,
            Z = 21.4f,
            RotationY = 3.14f,
            Valid = true,
        };
        data.Van = new VehicleSnapshot
        {
            X = -30f,
            Y = -0.05f,
            Z = 14.5f,
            RotationY = 0f,
            Valid = true,
        };
        return data;
    }

}
