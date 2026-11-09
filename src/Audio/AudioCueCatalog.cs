using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PatchworkIsland;

/// <summary>
/// One central, testable list of every shipped music track and sound effect.
/// Runtime code uses cue IDs rather than scattering file paths through gameplay
/// systems. Paths are relative to res://assets/ so the pure .NET test runner can
/// verify the packaged payload without booting Godot.
/// </summary>
public static class AudioCueCatalog
{
    public const string MasterBus = "Master";
    public const string MusicBus = "Music";
    public const string SfxBus = "SFX";
    public const string UiBus = "UI";
    public const string VehiclesBus = "Vehicles";

    public static readonly string[] Buses = { MasterBus, MusicBus, SfxBus, UiBus, VehiclesBus };

    public sealed class MusicCue
    {
        public string Id { get; init; } = "";
        public string File { get; init; } = "";
        public string Bus { get; init; } = MusicBus;
        public bool Loop { get; init; } = true;
    }

    public sealed class SoundCue
    {
        public string Id { get; init; } = "";
        public string File { get; init; } = "";
        public string Bus { get; init; } = SfxBus;
        public bool Loop { get; init; }
    }

    public static readonly IReadOnlyList<MusicCue> Music = new List<MusicCue>
    {
        new() { Id = "title", File = "audio/music/music_title.ogg", Bus = MusicBus, Loop = true },
        new() { Id = "village", File = "audio/music/music_village_day.ogg", Bus = MusicBus, Loop = true },
        new() { Id = "rally", File = "audio/music/music_rally.ogg", Bus = MusicBus, Loop = true },
        new() { Id = "festival", File = "audio/music/music_festival.ogg", Bus = MusicBus, Loop = true },
    };

    public static readonly IReadOnlyList<SoundCue> Sounds = new List<SoundCue>
    {
        new() { Id = "ui_confirm", File = "audio/sfx/ui_confirm.wav", Bus = UiBus },
        new() { Id = "ui_cancel", File = "audio/sfx/ui_cancel.wav", Bus = UiBus },
        new() { Id = "ui_error", File = "audio/sfx/ui_error.wav", Bus = UiBus },
        new() { Id = "ui_hover", File = "audio/sfx/ui_hover.wav", Bus = UiBus },
        new() { Id = "coin_collect", File = "audio/sfx/coin_collect.wav", Bus = SfxBus },
        new() { Id = "coin_spend", File = "audio/sfx/coin_spend.wav", Bus = SfxBus },
        new() { Id = "shop_buy", File = "audio/sfx/shop_buy.wav", Bus = SfxBus },
        new() { Id = "outfit_equip", File = "audio/sfx/outfit_equip.wav", Bus = SfxBus },
        new() { Id = "mission_complete", File = "audio/sfx/mission_complete.wav", Bus = SfxBus },
        new() { Id = "chapter_unlock", File = "audio/sfx/chapter_unlock.wav", Bus = SfxBus },
        new() { Id = "race_checkpoint", File = "audio/sfx/race_checkpoint.wav", Bus = SfxBus },
        new() { Id = "race_finish", File = "audio/sfx/race_finish.wav", Bus = SfxBus },
        new() { Id = "npc_greet_01", File = "audio/sfx/npc_greet_01.wav", Bus = SfxBus },
        new() { Id = "npc_greet_02", File = "audio/sfx/npc_greet_02.wav", Bus = SfxBus },
        new() { Id = "book_collect", File = "audio/sfx/book_collect.wav", Bus = SfxBus },
        new() { Id = "parcel_deliver", File = "audio/sfx/parcel_deliver.wav", Bus = SfxBus },
        new() { Id = "garden_plant", File = "audio/sfx/garden_plant.wav", Bus = SfxBus },
        new() { Id = "build_place", File = "audio/sfx/build_place.wav", Bus = SfxBus },
        new() { Id = "jump", File = "audio/sfx/jump.wav", Bus = SfxBus },
        new() { Id = "land", File = "audio/sfx/land.wav", Bus = SfxBus },
        new() { Id = "footstep_grass_01", File = "audio/sfx/footstep_grass_01.wav", Bus = SfxBus },
        new() { Id = "footstep_grass_02", File = "audio/sfx/footstep_grass_02.wav", Bus = SfxBus },
        new() { Id = "footstep_road_01", File = "audio/sfx/footstep_road_01.wav", Bus = SfxBus },
        new() { Id = "footstep_road_02", File = "audio/sfx/footstep_road_02.wav", Bus = SfxBus },
        new() { Id = "npc_horn", File = "audio/sfx/npc_horn.wav", Bus = SfxBus },
        new() { Id = "engine_idle_loop", File = "audio/sfx/engine_idle_loop.wav", Bus = VehiclesBus, Loop = true },
        new() { Id = "engine_drive_loop", File = "audio/sfx/engine_drive_loop.wav", Bus = VehiclesBus, Loop = true },
        new() { Id = "festival_pop", File = "audio/sfx/festival_pop.wav", Bus = SfxBus },
    };

    public static MusicCue FindMusic(string id) =>
        Music.FirstOrDefault(cue => string.Equals(cue.Id, id, StringComparison.Ordinal));

    public static SoundCue FindSound(string id) =>
        Sounds.FirstOrDefault(cue => string.Equals(cue.Id, id, StringComparison.Ordinal));

    public static string ResourcePath(string relativeFile) => "res://assets/" + relativeFile;

    /// <summary>
    /// Checks the complete manifest against a repository/export root. Used by
    /// pure tests; a missing or empty packaged asset is a hard release failure.
    /// </summary>
    public static bool ValidateRepository(string assetsRoot, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(assetsRoot) || !Directory.Exists(assetsRoot))
        {
            error = "Audio assets root does not exist.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cue in Music)
        {
            if (!ValidateCue(assetsRoot, cue.Id, cue.File, cue.Bus, ids, out error))
                return false;
        }
        foreach (var cue in Sounds)
        {
            if (!ValidateCue(assetsRoot, cue.Id, cue.File, cue.Bus, ids, out error))
                return false;
        }

        if (Music.Count != 4)
        {
            error = $"Expected 4 music loops, found {Music.Count}.";
            return false;
        }
        if (Sounds.Count != 28)
        {
            error = $"Expected 28 sound effects, found {Sounds.Count}.";
            return false;
        }
        foreach (string required in new[] { "title", "village", "rally", "festival" })
            if (FindMusic(required) == null)
            {
                error = $"Required music cue '{required}' is missing.";
                return false;
            }
        foreach (string required in new[]
                 {
                     "ui_confirm", "coin_collect", "mission_complete", "race_checkpoint",
                     "race_finish", "npc_greet_01", "npc_greet_02", "engine_idle_loop",
                     "engine_drive_loop", "footstep_grass_01", "footstep_road_01",
                 })
            if (FindSound(required) == null)
            {
                error = $"Required sound cue '{required}' is missing.";
                return false;
            }
        return true;
    }

    private static bool ValidateCue(string assetsRoot, string id, string file, string bus,
        HashSet<string> ids, out string error)
    {
        error = "";
        if (!ids.Add(id))
        {
            error = $"Duplicate audio cue id '{id}'.";
            return false;
        }
        if (!Buses.Contains(bus))
        {
            error = $"Audio cue '{id}' uses unknown bus '{bus}'.";
            return false;
        }
        string extension = Path.GetExtension(file).ToLowerInvariant();
        if (extension is not (".ogg" or ".wav"))
        {
            error = $"Audio cue '{id}' has an unsupported format '{extension}'.";
            return false;
        }
        string path = Path.Combine(assetsRoot, file.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            error = $"Audio cue '{id}' is missing file '{file}'.";
            return false;
        }
        if (new FileInfo(path).Length <= 64)
        {
            error = $"Audio cue '{id}' file '{file}' is empty or truncated.";
            return false;
        }
        return true;
    }
}
