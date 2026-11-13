using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PatchworkIsland;

namespace PatchworkIsland.Tests;

internal static partial class Program
{
    private static void SaveFeedbackTests(MissionCatalog catalog, CityMissionCatalog cityCatalog, string root, string json)
    {
        Run("save feedback never confirms a failed manual save", () =>
        {
            var policy = new SaveFeedbackPolicy();
            var success = policy.Record(SaveReason.Manual, SaveResult.Ok());
            Check(success.Show && !success.IsError, "success is shown");
            Check(success.Text.Contains("saved"), "success wording");
            var failure = policy.Record(SaveReason.Manual, SaveResult.Recoverable("disk full"));
            Check(failure.Show && failure.IsError, "manual failure is shown as an error");
            Check(failure.Text.Contains("disk full"), "failure keeps the diagnostic");
            Check(!failure.Text.Contains("Game saved"), "failure must not claim success");
            var invalid = policy.Record(SaveReason.Manual, SaveResult.Invalid("invalid state"));
            Check(invalid.Show && invalid.IsError, "invalid state is reported");
        });

        Run("automatic save failures are visible and throttled until recovery", () =>
        {
            var policy = new SaveFeedbackPolicy();
            var first = policy.Record(SaveReason.Autosave, SaveResult.Recoverable("disk full"));
            Check(first.Show && first.IsError, "first autosave failure shown");
            var second = policy.Record(SaveReason.Autosave, SaveResult.Recoverable("disk full"));
            Check(!second.Show, "repeated autosave failure suppressed");
            var missionStart = policy.Record(SaveReason.MissionStart, SaveResult.Recoverable("disk full"));
            Check(!missionStart.Show, "throttle spans automatic save reasons");
            var success = policy.Record(SaveReason.Autosave, SaveResult.Ok());
            Check(!success.Show, "automatic success is quiet");
            var afterRecovery = policy.Record(SaveReason.Autosave, SaveResult.Recoverable("disk full"));
            Check(afterRecovery.Show, "a new failure after recovery is shown again");
        });

        Run("settings failures are visible without suppressing autosave warnings", () =>
        {
            var policy = new SaveFeedbackPolicy();
            var first = policy.Record(SaveReason.Settings, SaveResult.Recoverable("disk full"));
            Check(first.Show && first.IsError, "first settings failure is visible");
            Check(first.Text.Contains("session"), "settings message separates memory from disk");
            var repeated = policy.Record(SaveReason.Settings, SaveResult.Recoverable("disk full"));
            Check(!repeated.Show, "repeated settings failure is throttled");
            var autosave = policy.Record(SaveReason.Autosave, SaveResult.Recoverable("disk full"));
            Check(autosave.Show && autosave.Text.Contains("Autosave failed"),
                "settings failure does not suppress the first autosave warning");
            var anotherAuto = policy.Record(SaveReason.Autosave, SaveResult.Recoverable("disk full"));
            Check(!anotherAuto.Show, "autosave failure is still throttled");
            var recovered = policy.Record(SaveReason.Settings, SaveResult.Ok());
            Check(!recovered.Show, "successful settings save is quiet");
            var afterRecovery = policy.Record(SaveReason.Settings, SaveResult.Recoverable("disk full"));
            Check(afterRecovery.Show, "settings warning returns after a successful save");
        });

        Run("mission-completion save failure is always reported", () =>
        {
            var policy = new SaveFeedbackPolicy();
            policy.Record(SaveReason.Autosave, SaveResult.Recoverable("disk full"));
            var completion = policy.Record(SaveReason.MissionCompleted, SaveResult.Recoverable("disk full"));
            Check(completion.Show && completion.IsError, "completion failure bypasses autosave throttle");
            Check(completion.Text.Contains("progress"), "completion failure explains progress risk");
            var quit = policy.Record(SaveReason.Quit, SaveResult.Recoverable("disk full"));
            Check(quit.Show && quit.IsError, "quit failure is visible");
            var close = policy.Record(SaveReason.WindowClose, SaveResult.Recoverable("disk full"));
            Check(!close.Show, "window-close failure uses console diagnostics, not hidden UI");
        });

        Run("session save state does not claim a failed new game was persisted", () =>
        {
            var state = new SessionSaveState();
            state.ApplyLoad(new SaveLoadResult { Status = SaveLoadStatus.Missing });
            Check(!state.HasSaveFile && !state.CanContinue, "fresh state has no save");
            var failedFirst = state.RecordNewGameSave(
                SaveResult.Recoverable("disk full"), "new game not saved");
            Check(!failedFirst, "failed first new game");
            Check(!state.HasSaveFile && !state.CanContinue, "no false Continue after failed first save");
            Equal("new game not saved", state.TitleNotice, "title explains the failure");

            state.ApplyLoad(new SaveLoadResult
            {
                Status = SaveLoadStatus.UnsupportedNewer,
                Message = "Save version 99 is newer.",
            });
            Check(state.HasSaveFile && !state.CanContinue && state.NewerSavePresent,
                "newer save protected and not continuable");
            var failedReplace = state.RecordNewGameSave(
                SaveResult.Recoverable("disk full"), "replacement not saved");
            Check(!failedReplace, "failed replacement");
            Check(state.HasSaveFile && !state.CanContinue && state.NewerSavePresent,
                "failed replacement leaves the newer-save state intact");
            var replaced = state.RecordNewGameSave(SaveResult.Ok(), "unused");
            Check(replaced, "successful replacement");
            Check(state.HasSaveFile && state.CanContinue && !state.NewerSavePresent,
                "successful replacement updates persistence flags");
            Equal("", state.TitleNotice, "success clears the title warning");
        });

        Run("new game after backup recovery keeps Continue when the write fails", () =>
        {
            var state = new SessionSaveState();
            state.ApplyLoad(new SaveLoadResult
            {
                Status = SaveLoadStatus.RecoveredBackup,
                Data = new GameData { Coins = 25 },
                UsedBackup = true,
            });
            Check(state.CanContinue, "recovered save is continuable");
            state.RecordNewGameSave(SaveResult.Recoverable("disk full"), "new game not saved");
            Check(state.HasSaveFile && state.CanContinue, "recovered save remains available");
            Check(!state.MemoryMatchesPersisted, "failed new game is not the persisted session");
        });

        Run("discarding an unsaved session cannot continue it", () =>
        {
            var state = new SessionSaveState();
            state.ApplyLoad(new SaveLoadResult { Status = SaveLoadStatus.Loaded, Data = new GameData() });
            Check(state.MemoryMatchesPersisted, "loaded save matches memory");
            state.RecordSaveFailure();
            Check(!state.MemoryMatchesPersisted, "failed write marks memory unsaved");
            Check(state.CanContinue, "the old persisted save remains available");
            state.DiscardUnsavedSession();
            Check(!state.MemoryMatchesPersisted && state.CanContinue,
                "discarded memory remains distinct from the persisted save");
            state.RecordSaveSuccess();
            Check(state.MemoryMatchesPersisted, "a successful save re-establishes the match");
        });

        Run("newer replacement permission is session-scoped and retryable", () =>
        {
            var state = new SessionSaveState();
            state.ApplyLoad(new SaveLoadResult
            {
                Status = SaveLoadStatus.UnsupportedNewer,
                Message = "Save version 99 is newer.",
            });
            Check(state.NewerSavePresent && !state.CanContinue, "newer save protected");
            Check(!state.NewerReplacementAuthorized, "no permission before explicit confirmation");
            state.AuthorizeNewerReplacement();
            Check(state.NewerReplacementAuthorized, "explicit new game authorises replacement");
            state.RecordNewGameSave(SaveResult.Recoverable("disk full"), "new game not saved");
            Check(state.NewerSavePresent && state.NewerReplacementAuthorized,
                "failed replacement keeps permission for Save Now retries");
            Check(!state.MemoryMatchesPersisted, "failed replacement is not claimed as persisted");
            state.DiscardUnsavedSession();
            Check(!state.NewerReplacementAuthorized, "discard clears replacement permission");
            Check(state.NewerSavePresent, "discard does not pretend the newer save disappeared");
            state.ApplyLoad(new SaveLoadResult
            {
                Status = SaveLoadStatus.UnsupportedNewer,
                Message = "A different newer save.",
            });
            Check(!state.NewerReplacementAuthorized, "loading another newer save clears permission");
            state.AuthorizeNewerReplacement();
            state.RecordSaveSuccess();
            Check(!state.NewerSavePresent && !state.NewerReplacementAuthorized,
                "successful replacement clears protection and permission");
        });

        Run("forced save failures do not change the on-disk save", () =>
        {
            string dir = TempDirectory("patchwork-save-injected-failure");
            string path = Path.Combine(dir, SaveService.DefaultFileName);
            try
            {
                SaveService.Save(new GameData { Coins = 21 }, path, catalog);
                byte[] before = File.ReadAllBytes(path);
                SaveService.SetTestWriteFailure(full =>
                    string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                        ? new IOException("injected disk failure")
                        : null);
                var failed = SaveService.TrySave(new GameData { Coins = 99 }, path, catalog);
                Check(!failed.Success, "injected write fails");
                Equal(SaveOutcome.RecoverableFailure, failed.Outcome, "injected failure is a recoverable I/O error");
                Check(before.SequenceEqual(File.ReadAllBytes(path)), "injected failure leaves exact bytes intact");
                SaveService.ClearTestWriteFailure();
                var retry = SaveService.TrySave(new GameData { Coins = 99 }, path, catalog);
                Check(retry.Success, "retry after clearing the failure succeeds");
                Equal(99, SaveService.Load(path, catalog).Data.Coins, "retry persisted the new state");
            }
            finally
            {
                SaveService.ClearTestWriteFailure();
                Directory.Delete(dir, true);
            }
        });

    }
}
