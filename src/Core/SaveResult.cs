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

    public SaveNotice Record(SaveReason reason, SaveResult result)
    {
        if (result == null) return SaveNotice.None;

        if (result.Success)
        {
            _automaticFailureReported = false;
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
/// session. A failed New Game write must not advertise the new session as the
/// save that Continue will load.
/// </summary>
public sealed class SessionSaveState
{
    public bool HasSaveFile { get; private set; }
    public bool CanContinue { get; private set; }
    public bool NewerSavePresent { get; private set; }
    public string TitleNotice { get; private set; } = "";

    public void ApplyLoad(SaveLoadResult result)
    {
        if (result == null) return;
        switch (result.Status)
        {
            case SaveLoadStatus.Loaded:
            case SaveLoadStatus.RecoveredBackup:
                HasSaveFile = true;
                CanContinue = true;
                NewerSavePresent = false;
                TitleNotice = "";
                break;

            case SaveLoadStatus.UnsupportedNewer:
                HasSaveFile = true;
                CanContinue = false;
                NewerSavePresent = true;
                TitleNotice = result.Message +
                              " Start a new game only if you are happy to replace it; a preserved copy will be kept.";
                break;

            case SaveLoadStatus.Unrecoverable:
            case SaveLoadStatus.Missing:
                HasSaveFile = false;
                CanContinue = false;
                NewerSavePresent = false;
                TitleNotice = "";
                break;
        }
    }

    public void RecordSaveSuccess()
    {
        HasSaveFile = true;
        CanContinue = true;
        NewerSavePresent = false;
        TitleNotice = "";
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
        TitleNotice = failureNotice ?? TitleNotice;
        return false;
    }

    public void SetTitleNotice(string notice) => TitleNotice = notice ?? "";
}
