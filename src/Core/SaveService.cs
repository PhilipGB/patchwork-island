using System;
using System.IO;
using System.Text.Json;

namespace PatchworkIsland;

public sealed class SaveLoadResult
{
    public GameData Data { get; init; }
    public bool Success => Data != null;
    public bool UsedBackup { get; init; }
    public bool RecoveredFromCorrupt { get; init; }
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

    public static void Save(GameData data, string path)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A save path is required.", nameof(path));

        data.Normalise();
        data.SchemaVersion = CurrentVersion;
        data.SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        string temp = path + ".tmp";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        if (File.Exists(path))
            File.Copy(path, BackupPath(path), true);
        File.Move(temp, path, true);
    }

    public static SaveLoadResult Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return TryBackup(path, "No save file yet.");

        var primary = ReadFile(path);
        if (primary.Success) return primary;

        // Keep the unreadable file for inspection, then try the previous save.
        string quarantined = null;
        try
        {
            quarantined = path + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Move(path, quarantined, true);
        }
        catch
        {
            quarantined = null;
        }

        var backup = TryBackup(path, primary.Message);
        return new SaveLoadResult
        {
            Data = backup.Data,
            UsedBackup = backup.UsedBackup,
            RecoveredFromCorrupt = true,
            Message = backup.Success
                ? $"The main save was damaged. Recovered the previous save" +
                  (quarantined == null ? "." : $" (damaged file kept as {Path.GetFileName(quarantined)}).")
                : primary.Message + (quarantined == null ? "" : " The damaged file was set aside."),
        };
    }

    private static SaveLoadResult TryBackup(string path, string originalMessage)
    {
        string backupPath = BackupPath(path);
        if (!File.Exists(backupPath))
            return new SaveLoadResult { Message = originalMessage };

        var backup = ReadFile(backupPath);
        if (!backup.Success)
            return new SaveLoadResult { Message = originalMessage };

        return new SaveLoadResult
        {
            Data = backup.Data,
            UsedBackup = true,
            Message = "Recovered the backup save because the main save was unavailable.",
        };
    }

    private static SaveLoadResult ReadFile(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<GameData>(json, JsonOptions);
            if (data == null)
                return new SaveLoadResult { Message = "Save file is empty." };

            data.Normalise();
            if (data.SchemaVersion > CurrentVersion)
                return new SaveLoadResult
                {
                    Message = $"Save version {data.SchemaVersion} is newer than this game (version {CurrentVersion}).",
                };
            data.SchemaVersion = CurrentVersion;
            return new SaveLoadResult { Data = data, Message = "Save loaded." };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new SaveLoadResult { Message = "Save file could not be read: " + ex.Message };
        }
    }
}
