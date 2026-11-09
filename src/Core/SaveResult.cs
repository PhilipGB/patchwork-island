using System;
using System.IO;

namespace PatchworkIsland;

public enum SaveReason
{
    Manual,
    Autosave,
    MissionStart,
    MissionCompleted,
    Settings,
    Customisation,
    NewGame,
    ReturnToTitle,
    Quit,
    WindowClose,
    Recovery,
    Celebration,
}

public enum SaveOutcome
{
    Success,
    /// <summary>An I/O or permission problem; retrying may work.</summary>
    RecoverableFailure,
    /// <summary>The in-memory state was refused by validation or a bug occurred.</summary>
    InvalidState,
}

public sealed class SaveResult
{
    public SaveOutcome Outcome { get; init; } = SaveOutcome.Success;
    public string Message { get; init; } = "";
    public Exception Error { get; init; }

    public bool Success => Outcome == SaveOutcome.Success;

    public static SaveResult Ok() => new();

    public static SaveResult Recoverable(string message, Exception error = null) =>
        new() { Outcome = SaveOutcome.RecoverableFailure, Message = message, Error = error };

    public static SaveResult Invalid(string message, Exception error = null) =>
        new() { Outcome = SaveOutcome.InvalidState, Message = message, Error = error };
}

public readonly struct SaveNotice
{
    public bool Show { get; init; }
    public string Text { get; init; }
    public bool IsError { get; init; }

    public static SaveNotice None => default;
}

/// <summary>
/// One policy for save feedback. Manual saves always confirm; failures that
/// lose progress are visible; repeated automatic failures are throttled until a
/// later save succeeds. UI text lives here so Main and Hud cannot disagree.
/// </summary>
public sealed class SaveFeedbackPolicy
{
    private bool _automaticFailureReported;
    private bool _settingsFailureReported;

    public SaveNotice Record(SaveReason reason, SaveResult result)
    {
        if (result == null) return SaveNotice.None;

        if (result.Success)
        {
            _automaticFailureReported = false;
            _settingsFailureReported = false;
            return reason == SaveReason.Manual
                ? new SaveNotice
                {
                    Show = true,
                    IsError = false,
                    Text = "Game saved. Your village will be waiting.",
                }
                : SaveNotice.None;
        }

        string detail = string.IsNullOrWhiteSpace(result.Message)
            ? "The save could not be written."
            : result.Message;

        switch (reason)
        {
            case SaveReason.Manual:
                return Error($"Could not save: {detail} Your previous save is unchanged. Please try again.");
            case SaveReason.MissionCompleted:
                return Error($"Job complete, but progress could not be saved: {detail}");
            case SaveReason.NewGame:
                return Error($"The new game could not be saved: {detail} Your previous save is safe.");
            case SaveReason.ReturnToTitle:
                return Error($"Progress could not be saved before returning to the title: {detail}");
            case SaveReason.Quit:
                return Error($"Progress could not be saved before quitting: {detail}");
            case SaveReason.WindowClose:
                // The window is closing; console diagnostics are the reliable path.
                return SaveNotice.None;
            case SaveReason.Celebration:
                return Error($"Your completed village could not be saved: {detail}");
            case SaveReason.Settings:
                // Settings failures have their own throttle and are always shown
                // inline by the settings modal. They must not suppress the first
                // autosave warning later in the same session.
                if (_settingsFailureReported) return SaveNotice.None;
                _settingsFailureReported = true;
                return Error($"Setting applied for this session, but it could not be saved: {detail}");
            case SaveReason.Customisation:
                return Error($"Your new look is applied for this session, but it could not be saved: {detail}");
            default:
                if (_automaticFailureReported) return SaveNotice.None;
                _automaticFailureReported = true;
                string prefix = reason == SaveReason.Autosave ? "Autosave failed" : "Progress could not be saved";
                return Error($"{prefix}: {detail} Your latest progress may not be saved.");
        }
    }

    private static SaveNotice Error(string text) =>
        new() { Show = true, IsError = true, Text = text };
}

/// <summary>
/// Tracks what is actually persisted on disk independently of the in-memory
/// session. Continue must only resume the persisted session it represents; a
/// discarded New Game session is never continued through the old save's button.
/// </summary>
public sealed class SessionSaveState
{
    public bool HasSaveFile { get; private set; }
    public bool CanContinue { get; private set; }
    public bool NewerSavePresent { get; private set; }
    public string TitleNotice { get; private set; } = "";

    /// <summary>
    /// True only while the current in-memory session is known to have been
    /// written to the save file. Any failed or skipped write makes this false.
    /// </summary>
    public bool MemoryMatchesPersisted { get; private set; }

    /// <summary>
    /// Session-scoped permission to replace an unsupported newer save. It is
    /// granted only by an explicit New Game confirmation and cleared once the
    /// session is saved, discarded, or a different save is loaded.
    /// </summary>
    public bool NewerReplacementAuthorized { get; private set; }

    public void ApplyLoad(SaveLoadResult result)
    {
        if (result == null) return;
        NewerReplacementAuthorized = false;
        switch (result.Status)
        {
            case SaveLoadStatus.Loaded:
            case SaveLoadStatus.RecoveredBackup:
                HasSaveFile = true;
                CanContinue = true;
                NewerSavePresent = false;
                MemoryMatchesPersisted = true;
                TitleNotice = "";
                break;

            case SaveLoadStatus.UnsupportedNewer:
                HasSaveFile = true;
                CanContinue = false;
                NewerSavePresent = true;
                MemoryMatchesPersisted = false;
                TitleNotice = result.Message +
                              " Start a new game only if you are happy to replace it; a preserved copy will be kept.";
                break;

            case SaveLoadStatus.Unrecoverable:
            case SaveLoadStatus.Missing:
                HasSaveFile = false;
                CanContinue = false;
                NewerSavePresent = false;
                MemoryMatchesPersisted = false;
                TitleNotice = "";
                break;
        }
    }

    public void RecordSaveSuccess()
    {
        HasSaveFile = true;
        CanContinue = true;
        NewerSavePresent = false;
        NewerReplacementAuthorized = false;
        MemoryMatchesPersisted = true;
        TitleNotice = "";
    }

    /// <summary>
    /// A failed write means memory can no longer be assumed to match the file.
    /// Replacement authorisation is deliberately preserved so Save Now can
    /// retry the explicitly confirmed newer-save replacement.
    /// </summary>
    public void RecordSaveFailure() => MemoryMatchesPersisted = false;

    /// <summary>
    /// Called only after the player explicitly confirms New Game. Merely
    /// opening and cancelling the confirmation must not call this.
    /// </summary>
    public void AuthorizeNewerReplacement()
    {
        if (NewerSavePresent) NewerReplacementAuthorized = true;
    }

    /// <summary>
    /// A New Game reset may run even when the initial write fails. Leave the
    /// previous persisted-save flags untouched in that case and explain the
    /// mismatch on the title screen.
    /// </summary>
    public bool RecordNewGameSave(SaveResult result, string failureNotice)
    {
        if (result != null && result.Success)
        {
            RecordSaveSuccess();
            return true;
        }
        MemoryMatchesPersisted = false;
        TitleNotice = failureNotice ?? TitleNotice;
        return false;
    }

    /// <summary>
    /// The player chose to leave the current unsaved session. The persisted
    /// save remains available to Continue, but this memory session and any
    /// newer-replacement permission must not leak into that next session.
    /// </summary>
    public void DiscardUnsavedSession()
    {
        MemoryMatchesPersisted = false;
        NewerReplacementAuthorized = false;
    }

    public void SetTitleNotice(string notice) => TitleNotice = notice ?? "";
}
