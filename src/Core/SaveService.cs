using System;
using System.Collections.Generic;
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
    /// <summary>
    /// The persistence contract, deliberately separate from the game release
    /// version. Increment this only when the saved data shape changes.
    /// </summary>
    public const int CurrentVersion = 5;
    public const string DefaultFileName = "save.json";

    // Narrow test seam for deterministic write failures. It is process-local,
    // path-specific and never set by normal gameplay. Tests must clear it after
    // use so production writes are unaffected.
    private static Func<string, Exception> _testWriteFailure;

    internal static void SetTestWriteFailure(Func<string, Exception> failure) =>
        _testWriteFailure = failure;

    internal static void ClearTestWriteFailure() => _testWriteFailure = null;

    private static void InjectTestWriteFailure(string path)
    {
        var failure = _testWriteFailure;
        if (failure == null) return;
        var exception = failure(Path.GetFullPath(path));
        if (exception != null) throw exception;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string BackupPath(string path) => path + ".bak";

    public static void Save(GameData data, string path, MissionCatalog catalog = null,
        bool allowNewerReplacement = false)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A save path is required.", nameof(path));

        data.Normalise();
        data.SchemaVersion = CurrentVersion;
        data.SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var validation = SaveValidation.Validate(data, catalog);
        if (!validation.IsValid)
            throw new InvalidDataException("Refusing to write an invalid save: " + validation.Message);

        // An unsupported newer save is only ever replaced by an explicit New
        // Game / Save Now retry that the session has already authorised. An
        // ordinary autosave must leave it completely untouched.
        if (File.Exists(path))
        {
            var existing = ReadFile(path, catalog);
            if (existing.Status == SaveLoadStatus.UnsupportedNewer && !allowNewerReplacement)
                throw new InvalidDataException(
                    $"Refusing to overwrite newer save version {existing.SchemaVersion}; " +
                    $"this game supports up to version {CurrentVersion}.");
        }

        InjectTestWriteFailure(path);

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

    /// <summary>
    /// UI-facing wrapper: converts expected failures into a structured result
    /// without hiding diagnostics. Invalid in-memory state and I/O failures are
    /// kept separate so callers can phrase the message accurately.
    /// </summary>
    public static SaveResult TrySave(GameData data, string path, MissionCatalog catalog = null,
        bool allowNewerReplacement = false)
    {
        try
        {
            Save(data, path, catalog, allowNewerReplacement);
            return SaveResult.Ok();
        }
        catch (InvalidDataException ex)
        {
            return SaveResult.Invalid(ex.Message, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return SaveResult.Recoverable(ex.Message, ex);
        }
        catch (Exception ex)
        {
            return SaveResult.Invalid("An unexpected save error occurred. " + ex.Message, ex);
        }
    }

    /// <summary>
    /// Upgrades a supported older save in memory to the current schema. Schema
    /// 1 predates character customisation, so its missing fields are filled by
    /// Normalise with Boy + Everyday + no purchases. 2 predates the separate
    /// v1.2 festival marker, which defaults to false. 3 predates the city
    /// expansion; its city fields default to closed/empty and the deterministic
    /// city clock starts at zero. All progression fields are left intact. The
    /// file is only rewritten at the current schema by the next save.
    /// </summary>
    public static GameData Migrate(GameData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.SchemaVersion > CurrentVersion)
            throw new InvalidDataException(
                $"Cannot migrate newer save version {data.SchemaVersion}; " +
                $"this game supports up to version {CurrentVersion}.");

        data.Normalise();
        while (data.SchemaVersion < CurrentVersion)
        {
            switch (data.SchemaVersion)
            {
                case 1:
                    // Schema 1 predates customisation. Normalise has already
                    // supplied Boy + Everyday + no purchases without touching
                    // any progression field, so only the version changes.
                    data.SchemaVersion = 2;
                    break;

                case 2:
                    // Schema 2 predates the independent v1.2 festival marker.
                    // CelebrationSeen keeps its original M10 meaning.
                    data.FestivalSeen = false;
                    data.SchemaVersion = 3;
                    break;

                case 3:
                    // Schema 3 predates the city expansion. Normalise has
                    // already supplied an empty city board, a closed road and
                    // a deterministic default simulation seed. No town state
                    // is inferred or rewritten.
                    data.ActiveCityMission = "";
                    data.CompletedCityMissions ??= new List<string>();
                    data.CityMissionProgress ??= new Dictionary<string, Dictionary<string, int>>();
                    data.CityRoadworksCompleted = false;
                    data.CityRoadworksRewardClaimed = false;
                    data.CityRoadOpen = false;
                    data.CityVisited = false;
                    data.CityLogicalSeconds = 0;
                    if (data.CitySeed == 0) data.CitySeed = 20261111;
                    data.SchemaVersion = 4;
                    break;

                case 4:
                    // Schema 4 predates the driving academy. Every existing
                    // campaign keeps the van access it already had, but that is
                    // a legacy permit, not an earned official licence.
                    data.LegacyVanPermit = true;
                    data.SchemaVersion = 5;
                    break;

                default:
                    throw new InvalidDataException(
                        $"No migration path from save schema {data.SchemaVersion}.");
            }
        }
        return data;
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

            // Read SchemaVersion from the JSON document before touching the
            // typed model. A future schema may legitimately change an existing
            // field's type, and that must still be classified as unsupported
            // newer rather than corruption that could be quarantined.
            int declaredVersion = 1;
            using (var document = JsonDocument.Parse(json))
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return ReadOutcome.Corrupt("Save file is not a JSON object.");

                foreach (var property in root.EnumerateObject())
                {
                    if (!string.Equals(property.Name, "SchemaVersion",
                            StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (property.Value.ValueKind != JsonValueKind.Number ||
                        !property.Value.TryGetInt32(out declaredVersion))
                    {
                        return ReadOutcome.Corrupt("Save file has an invalid schema version.");
                    }
                }

                if (declaredVersion > CurrentVersion)
                {
                    return new ReadOutcome
                    {
                        Status = SaveLoadStatus.UnsupportedNewer,
                        SchemaVersion = declaredVersion,
                        Message = $"Save version {declaredVersion} is newer than this game " +
                                  $"(save schema {CurrentVersion}).",
                    };
                }
            }

            var data = JsonSerializer.Deserialize<GameData>(json, JsonOptions);
            if (data == null)
                return ReadOutcome.Corrupt("Save file is empty.");

            data.SchemaVersion = declaredVersion;
            Migrate(data);
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
