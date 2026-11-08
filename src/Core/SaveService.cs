using System;
using System.IO;
using System.Text.Json;

namespace PatchworkIsland;

public enum SaveLoadStatus
{
    Loaded,
    Missing,
    Corrupt,
    UnsupportedNewer,
    RecoveredBackup,
    Unrecoverable,
}

public sealed class SaveLoadResult
{
    public GameData Data { get; init; }
    public SaveLoadStatus Status { get; init; } = SaveLoadStatus.Missing;
    public bool Success => Status is SaveLoadStatus.Loaded or SaveLoadStatus.RecoveredBackup;
    public bool UsedBackup { get; init; }
    public bool RecoveredFromCorrupt { get; init; }
    public bool UnsupportedNewer => Status == SaveLoadStatus.UnsupportedNewer;
    public string Message { get; init; } = "";
}

/// <summary>
/// Versioned, atomic save files. This type deliberately has no Godot dependency
/// so the test runner can exercise round-trips, corrupt files and backups.
/// </summary>
public static class SaveService
{
    public const int CurrentVersion = 1;
    public const string DefaultFileName = "save.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string BackupPath(string path) => path + ".bak";

    public static void Save(GameData data, string path, MissionCatalog catalog = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A save path is required.", nameof(path));

        data.Normalise();
        data.SchemaVersion = CurrentVersion;
        data.SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var validation = SaveValidation.Validate(data, catalog);
        if (!validation.IsValid)
            throw new InvalidDataException("Refusing to write an invalid save: " + validation.Message);

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        string temp = path + ".tmp";
        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
            WriteBytes(temp, bytes);

            if (File.Exists(path))
                ProtectExistingSave(path, catalog);

            File.Move(temp, path, true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    public static SaveLoadResult Load(string path, MissionCatalog catalog = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return LoadBackup(path, catalog, "No save file yet.", missingMain: true);

        var primary = ReadFile(path, catalog);
        switch (primary.Status)
        {
            case SaveLoadStatus.Loaded:
                return new SaveLoadResult
                {
                    Data = primary.Data,
                    Status = SaveLoadStatus.Loaded,
                    Message = primary.Message,
                };

            case SaveLoadStatus.UnsupportedNewer:
                return new SaveLoadResult
                {
                    Status = SaveLoadStatus.UnsupportedNewer,
                    Message = primary.Message + " The save has been left untouched.",
                };

            case SaveLoadStatus.Missing:
                return LoadBackup(path, catalog, primary.Message, missingMain: true);
        }

        // The primary file is syntactically or structurally damaged. Keep the
        // evidence, but never let this copy replace a good backup.
        string quarantined = TryQuarantine(path);
        var backup = LoadBackup(path, catalog, primary.Message, missingMain: false);
        if (backup.Success)
        {
            return new SaveLoadResult
            {
                Data = backup.Data,
                Status = SaveLoadStatus.RecoveredBackup,
                UsedBackup = true,
                RecoveredFromCorrupt = true,
                Message = "The main save was damaged. Recovered the previous save" +
                          (quarantined == null
                              ? "."
                              : $" (damaged file kept as {Path.GetFileName(quarantined)})."),
            };
        }

        return new SaveLoadResult
        {
            Status = SaveLoadStatus.Unrecoverable,
            UsedBackup = backup.UsedBackup,
            RecoveredFromCorrupt = true,
            Message = primary.Message +
                      (quarantined == null ? "" : " The damaged file was set aside.") +
                      (string.IsNullOrEmpty(backup.Message) ? "" : " " + backup.Message),
        };
    }

    private static SaveLoadResult LoadBackup(string path, MissionCatalog catalog,
        string originalMessage, bool missingMain)
    {
        string backupPath = BackupPath(path);
        if (!File.Exists(backupPath))
        {
            return new SaveLoadResult
            {
                Status = missingMain ? SaveLoadStatus.Missing : SaveLoadStatus.Unrecoverable,
                Message = missingMain
                    ? originalMessage
                    : originalMessage + " There is no backup save to recover.",
            };
        }

        var backup = ReadFile(backupPath, catalog);
        if (backup.Status == SaveLoadStatus.Loaded)
        {
            return new SaveLoadResult
            {
                Data = backup.Data,
                Status = SaveLoadStatus.RecoveredBackup,
                UsedBackup = true,
                Message = missingMain
                    ? "Recovered the backup save because the main save was unavailable."
                    : "Recovered the backup save.",
            };
        }

        if (backup.Status == SaveLoadStatus.UnsupportedNewer)
        {
            return new SaveLoadResult
            {
                Status = SaveLoadStatus.UnsupportedNewer,
                Message = backup.Message + " The backup has been left untouched.",
            };
        }

        return new SaveLoadResult
        {
            Status = SaveLoadStatus.Unrecoverable,
            Message = originalMessage + " The backup could not be read: " + backup.Message,
        };
    }

    private static ReadOutcome ReadFile(string path, MissionCatalog catalog)
    {
        try
        {
            string json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<GameData>(json, JsonOptions);
            if (data == null)
                return ReadOutcome.Corrupt("Save file is empty.");

            int declaredVersion = data.SchemaVersion;
            if (declaredVersion > CurrentVersion)
            {
                return new ReadOutcome
                {
                    Status = SaveLoadStatus.UnsupportedNewer,
                    SchemaVersion = declaredVersion,
                    Message = $"Save version {declaredVersion} is newer than this game (version {CurrentVersion}).",
                };
            }

            data.Normalise();
            data.SchemaVersion = CurrentVersion;
            var validation = SaveValidation.Validate(data, catalog);
            if (!validation.IsValid)
                return ReadOutcome.Corrupt("Save data is invalid: " + validation.Message);

            return new ReadOutcome
            {
                Status = SaveLoadStatus.Loaded,
                Data = data,
                SchemaVersion = CurrentVersion,
                Message = "Save loaded.",
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return ReadOutcome.Corrupt("Save file could not be read: " + ex.Message);
        }
    }

    /// <summary>
    /// The existing file is about to be replaced. Keep a valid previous copy as
    /// the backup, but quarantine corruption and preserve newer-version saves
    /// instead of overwriting the only good backup with unusable data.
    /// </summary>
    private static void ProtectExistingSave(string path, MissionCatalog catalog)
    {
        var existing = ReadFile(path, catalog);
        switch (existing.Status)
        {
            case SaveLoadStatus.Loaded:
                byte[] bytes = File.ReadAllBytes(path);
                string backupTemp = BackupPath(path) + ".tmp";
                try
                {
                    WriteBytes(backupTemp, bytes);
                    File.Move(backupTemp, BackupPath(path), true);
                }
                catch
                {
                    TryDelete(backupTemp);
                    throw;
                }
                break;

            case SaveLoadStatus.UnsupportedNewer:
                PreserveCopy(path, $".newer-v{existing.SchemaVersion}-");
                break;

            case SaveLoadStatus.Corrupt:
                PreserveCopy(path, ".corrupt-");
                break;
        }
    }

    private static void PreserveCopy(string path, string suffix)
    {
        string preserved = UniqueSibling(path, suffix);
        File.Copy(path, preserved, false);
    }

    private static string TryQuarantine(string path)
    {
        try
        {
            string quarantined = UniqueSibling(path, ".corrupt-");
            File.Move(path, quarantined, false);
            return quarantined;
        }
        catch
        {
            return null;
        }
    }

    private static string UniqueSibling(string path, string suffix)
    {
        string stem = path + suffix + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        string candidate = stem;
        int counter = 1;
        while (File.Exists(candidate))
            candidate = stem + "-" + counter++;
        return candidate;
    }

    private static void WriteBytes(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // A stale temporary file is harmless and is never loaded.
        }
    }

    private sealed class ReadOutcome
    {
        public SaveLoadStatus Status { get; init; }
        public GameData Data { get; init; }
        public int SchemaVersion { get; init; }
        public string Message { get; init; } = "";

        public static ReadOutcome Corrupt(string message) =>
            new() { Status = SaveLoadStatus.Corrupt, Message = message };
    }
}
