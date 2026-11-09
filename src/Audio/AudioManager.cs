using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Owns the four music loops, a bounded pool of UI/SFX voices and the shared
/// bus routing. The manager is always processing so title music and UI feedback
/// keep working while the world is paused. Gameplay never creates a new audio
/// player per tick.
/// </summary>
public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; }

    public const float MusicBaseDb = -9.5f;
    public const float SfxBaseDb = -4.0f;
    private const int SfxVoiceCount = 14;
    private const int SpatialVoiceCount = 6;
    private const float SilenceDb = -46f;

    public string CurrentMusicId { get; private set; } = "";
    public string LastSfxId { get; private set; } = "";
    public int LoadFailureCount { get; private set; }
    public bool MusicEnabled { get; private set; } = true;
    public bool SfxEnabled { get; private set; } = true;

    private AudioStreamPlayer _musicPlayer;
    private readonly List<AudioStreamPlayer> _sfxVoices = new();
    private readonly List<AudioStreamPlayer3D> _spatialVoices = new();
    private readonly Dictionary<string, AudioStream> _cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ulong> _lastTrigger = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _playCounts = new(StringComparer.Ordinal);
    private AudioCueCatalog.MusicCue _pendingMusic;
    private int _nextSfx;
    private int _nextSpatial;
    private float _fade = 1f;
    private float _fadeSeconds = 0.5f;
    private MusicPhase _phase = MusicPhase.Idle;

    private enum MusicPhase
    {
        Idle,
        FadingOut,
        FadingIn,
    }

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        EnsureBuses();
        BuildPlayers();
    }

    public override void _ExitTree()
    {
        StopAll();
        _cache.Clear();
        if (Instance == this) Instance = null;
    }

    public void StopAll()
    {
        if (_musicPlayer != null)
        {
            _musicPlayer.Stop();
            _musicPlayer.Stream = null;
        }
        foreach (var voice in _sfxVoices)
        {
            voice.Stop();
            voice.Stream = null;
        }
        foreach (var voice in _spatialVoices)
        {
            voice.Stop();
            voice.Stream = null;
        }
    }

    public static void EnsureBuses()
    {
        foreach (string busName in new[]
                 {
                     AudioCueCatalog.MusicBus, AudioCueCatalog.SfxBus,
                     AudioCueCatalog.UiBus, AudioCueCatalog.VehiclesBus,
                 })
        {
            int index = AudioServer.GetBusIndex(busName);
            if (index >= 0) continue;
            index = AudioServer.BusCount;
            AudioServer.AddBus();
            AudioServer.SetBusName(index, busName);
            AudioServer.SetBusSend(index, AudioCueCatalog.MasterBus);
            AudioServer.SetBusVolumeDb(index, 0f);
        }
    }

    private void BuildPlayers()
    {
        _musicPlayer = new AudioStreamPlayer
        {
            Name = "MusicPlayer",
            Bus = AudioCueCatalog.MusicBus,
            VolumeDb = SilenceDb,
            ProcessMode = ProcessModeEnum.Always,
        };
        AddChild(_musicPlayer);

        for (int i = 0; i < SfxVoiceCount; i++)
        {
            var voice = new AudioStreamPlayer
            {
                Name = $"SfxVoice{i}",
                Bus = AudioCueCatalog.SfxBus,
                VolumeDb = SfxBaseDb,
                ProcessMode = ProcessModeEnum.Always,
            };
            AddChild(voice);
            _sfxVoices.Add(voice);
        }

        for (int i = 0; i < SpatialVoiceCount; i++)
        {
            var voice = new AudioStreamPlayer3D
            {
                Name = $"SpatialVoice{i}",
                Bus = AudioCueCatalog.SfxBus,
                VolumeDb = SfxBaseDb,
                UnitSize = 8f,
                MaxDistance = 32f,
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                ProcessMode = ProcessModeEnum.Always,
            };
            AddChild(voice);
            _spatialVoices.Add(voice);
        }
    }

    public void ApplySettings(SettingsData settings)
    {
        MusicEnabled = settings?.MusicEnabled ?? true;
        SfxEnabled = settings?.SfxEnabled ?? true;
        SetBusMuted(AudioCueCatalog.MusicBus, !MusicEnabled);
        SetBusMuted(AudioCueCatalog.SfxBus, !SfxEnabled);
        SetBusMuted(AudioCueCatalog.UiBus, !SfxEnabled);
        SetBusMuted(AudioCueCatalog.VehiclesBus, !SfxEnabled);
        SetBusMuted(AudioCueCatalog.MasterBus, false);
    }

    private static void SetBusMuted(string busName, bool muted)
    {
        int index = AudioServer.GetBusIndex(busName);
        if (index < 0) return;
        AudioServer.SetBusMute(index, muted);
    }

    public static bool IsBusMuted(string busName)
    {
        int index = AudioServer.GetBusIndex(busName);
        return index >= 0 && AudioServer.IsBusMute(index);
    }

    public static bool HasRequiredBuses()
    {
        foreach (string bus in AudioCueCatalog.Buses)
            if (AudioServer.GetBusIndex(bus) < 0) return false;
        return true;
    }

    public void PlayMusic(string id, float fadeSeconds = 0.55f)
    {
        var cue = AudioCueCatalog.FindMusic(id);
        if (cue == null)
        {
            GD.PushWarning($"Unknown music cue '{id}'.");
            return;
        }
        if (CurrentMusicId == id && _musicPlayer.Playing && _phase != MusicPhase.FadingOut)
            return;

        CurrentMusicId = id;
        _pendingMusic = cue;
        _fadeSeconds = Mathf.Max(0.05f, fadeSeconds);
        if (_musicPlayer.Playing)
        {
            _phase = MusicPhase.FadingOut;
            _fade = 0f;
        }
        else
        {
            StartPendingMusic();
            _phase = MusicPhase.FadingIn;
            _fade = 0f;
        }
    }

    public void StopMusic(float fadeSeconds = 0.45f)
    {
        if (!_musicPlayer.Playing) return;
        CurrentMusicId = "";
        _pendingMusic = null;
        _fadeSeconds = Mathf.Max(0.05f, fadeSeconds);
        _phase = MusicPhase.FadingOut;
        _fade = 0f;
    }

    private void StartPendingMusic()
    {
        if (_pendingMusic == null)
        {
            _musicPlayer.Stop();
            return;
        }
        var stream = LoadStream(_pendingMusic.File, _pendingMusic.Loop);
        if (stream == null)
        {
            _musicPlayer.Stop();
            return;
        }
        _musicPlayer.Stream = stream;
        _musicPlayer.VolumeDb = SilenceDb;
        _musicPlayer.Play();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        switch (_phase)
        {
            case MusicPhase.FadingOut:
                _fade = Mathf.Min(1f, _fade + dt / _fadeSeconds);
                _musicPlayer.VolumeDb = Mathf.Lerp(MusicBaseDb, SilenceDb, _fade);
                if (_fade >= 1f)
                {
                    _musicPlayer.Stop();
                    if (_pendingMusic != null)
                    {
                        StartPendingMusic();
                        _phase = MusicPhase.FadingIn;
                        _fade = 0f;
                    }
                    else
                    {
                        _phase = MusicPhase.Idle;
                        _musicPlayer.VolumeDb = SilenceDb;
                    }
                }
                break;
            case MusicPhase.FadingIn:
                _fade = Mathf.Min(1f, _fade + dt / _fadeSeconds);
                _musicPlayer.VolumeDb = Mathf.Lerp(SilenceDb, MusicBaseDb, _fade);
                if (_fade >= 1f)
                {
                    _phase = MusicPhase.Idle;
                    _musicPlayer.VolumeDb = MusicBaseDb;
                }
                break;
        }
    }

    public void PlaySfx(string id, float volumeDb = SfxBaseDb, float pitchScale = 1f)
    {
        var cue = AudioCueCatalog.FindSound(id);
        if (cue == null)
        {
            GD.PushWarning($"Unknown sound cue '{id}'.");
            return;
        }
        if (!SfxEnabled) return;
        ulong now = Time.GetTicksMsec();
        if (_lastTrigger.TryGetValue(id, out ulong previous) && now - previous < 35)
            return;
        _lastTrigger[id] = now;

        var stream = LoadStream(cue.File, cue.Loop);
        if (stream == null) return;

        var voice = _sfxVoices[_nextSfx++ % _sfxVoices.Count];
        voice.Stop();
        voice.Bus = cue.Bus;
        voice.Stream = stream;
        voice.VolumeDb = volumeDb;
        voice.PitchScale = Mathf.Clamp(pitchScale, 0.5f, 2f);
        voice.Play();
        LastSfxId = id;
        _playCounts[id] = PlayCount(id) + 1;
    }

    public static void PlayUi(string id) => Instance?.PlaySfx(id, SfxBaseDb - 1f);

    public static void PlayWorld(string id, float volumeDb = SfxBaseDb, float pitch = 1f) =>
        Instance?.PlaySfx(id, volumeDb, pitch);

    public void PlayAt(string id, Vector3 position, float volumeDb = SfxBaseDb - 2f)
    {
        var cue = AudioCueCatalog.FindSound(id);
        if (cue == null || !SfxEnabled) return;
        var stream = LoadStream(cue.File, cue.Loop);
        if (stream == null) return;
        var voice = _spatialVoices[_nextSpatial++ % _spatialVoices.Count];
        voice.Stop();
        voice.Bus = cue.Bus;
        voice.Stream = stream;
        voice.VolumeDb = volumeDb;
        voice.GlobalPosition = position;
        voice.Play();
        LastSfxId = id;
        _playCounts[id] = PlayCount(id) + 1;
    }

    public int PlayCount(string id) => _playCounts.TryGetValue(id, out int count) ? count : 0;

    public static AudioStreamPlayer3D CreateVehicleLoop(Node3D parent, string cueId, float unitSize = 7f,
        float maxDistance = 42f, float volumeDb = -14f)
    {
        var cue = AudioCueCatalog.FindSound(cueId);
        if (cue == null) return null;
        var player = new AudioStreamPlayer3D
        {
            Name = $"Engine_{cueId}",
            Bus = AudioCueCatalog.VehiclesBus,
            UnitSize = unitSize,
            MaxDistance = maxDistance,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            VolumeDb = volumeDb,
            ProcessMode = ProcessModeEnum.Pausable,
        };
        parent.AddChild(player);
        var stream = LoadStreamStatic(cue.File, true);
        if (stream == null) return null;
        player.Stream = stream;
        return player;
    }

    public bool HasMusicStream => _musicPlayer?.Stream != null;
    public bool IsMusicPlaying => _musicPlayer?.Playing == true;
    public double MusicPlaybackPosition => _musicPlayer?.GetPlaybackPosition() ?? 0.0;
    public float MusicVolumeDb => _musicPlayer?.VolumeDb ?? 0f;
    public int SfxVoiceTotal => _sfxVoices.Count;
    public int SpatialVoiceTotal => _spatialVoices.Count;

    private AudioStream LoadStream(string relativeFile, bool loop)
    {
        if (_cache.TryGetValue(relativeFile, out var cached))
            return cached;
        var stream = LoadStreamStatic(relativeFile, loop);
        if (stream != null) _cache[relativeFile] = stream;
        return stream;
    }

    private static AudioStream LoadStreamStatic(string relativeFile, bool loop)
    {
        string path = AudioCueCatalog.ResourcePath(relativeFile);
        if (!ResourceLoader.Exists(path))
        {
            GD.PushError($"Audio asset is missing from the package: {path}");
            return null;
        }
        var stream = GD.Load<AudioStream>(path);
        if (stream == null)
        {
            GD.PushError($"Audio asset could not be loaded: {path}");
            return null;
        }
        if (loop)
        {
            if (stream is AudioStreamOggVorbis ogg)
            {
                ogg.Loop = true;
                ogg.LoopOffset = 0;
            }
            else if (stream is AudioStreamWav wav)
            {
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                wav.LoopBegin = 0;
                wav.LoopEnd = Math.Max(1, wav.Data.Length / 2);
            }
        }
        return stream;
    }
}
