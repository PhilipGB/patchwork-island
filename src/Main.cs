using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Composition root. Owns the world, player, camera, UI and shared game
/// services. Gameplay milestones add their systems here while keeping the boot
/// and restore flow stable.
/// </summary>
public partial class Main : Node3D
{
    private Hud _hud;
    private AudioManager _audio;
    private WorldBuilder _world;
    private PlayerController _player;
    private CameraRig _rig;
    private WorldMarker _marker;
    private MissionCatalog _catalog;
    private GameData _data;
    private MissionService _missions;
    private readonly HashSet<string> _implemented = new()
    {
        "M01", "M02", "M03", "M04", "M05", "M06", "M07", "M08", "M09", "M10",
        "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20",
    };
    private readonly List<Pickup> _pickups = new();
    private readonly List<NpcActor> _npcs = new();
    private readonly Dictionary<string, NpcActor> _npcById = new(StringComparer.Ordinal);
    private readonly List<TrafficCar> _trafficCars = new();
    private NpcActor _talkingNpc;
    private readonly List<Interactable> _signStations = new();
    private readonly List<Interactable> _gardenStations = new();
    private readonly List<Interactable> _festivalTableStations = new();
    private readonly List<Interactable> _festivalLanternStations = new();
    private Interactable _postStation;
    private Interactable _repairStation;
    private TrafficCar _rallyRival;
    private bool _festivalShown;
    private readonly List<Node3D> _signVisuals = new();
    private readonly List<Node3D> _gardenFlowerRoots = new();
    private readonly List<MeshInstance3D> _gardenFlowerHeads = new();
    private Interactable _jobStation;
    private Interactable _redHouseStation;
    private Interactable _workshopStation;
    private Interactable _shopStation;
    private Interactable _clothesStation;
    private Interactable _houseStation;
    private Interactable _bridgeStation;
    private Interactable _townHallStation;
    private Interactable _parkingStation;
    private Interactable _playgroundStation;
    private Interactable _parkStation;
    private Interactable _furnitureStation;
    private BridgeBuilder _bridgeBuilder;
    private SwingBuilder _swingBuilder;
    private ParkBuilder _parkBuilder;
    private FurnitureBuilder _furnitureBuilder;
    private readonly List<IBuildMode> _buildModes = new();
    private VanController _van;
    private bool _buildMode;
    private bool _driving;
    private RaceTimer _raceTimer = new();
    private ControlMode _controlMode;
    private float _raceCheckTimer;
    private float _raceHudTimer;
    private float _raceWrongHintCooldown;
    private bool _practiceRaceActive;
    private int _practiceCheckpoint;
    private float _practiceElapsed;
    private float _practiceCheckTimer;
    private float _practiceHudTimer;
    private bool _celebrationShown;
    private float _vehicleSwitchCooldown;

    private bool _started;
    private float _uiRefresh;
    private Vector3 _destination;
    private string _destLabel = "";
    private string _destIcon = "star";
    private bool _hasDestination;
    private bool _lastIndoor;
    private readonly SessionSaveState _session = new();
    private readonly SaveFeedbackPolicy _saveFeedback = new();
    private string _recoveredJobNote = "";
    private bool _loadedFromSave;
    private string _saveNotice = "";
    private float _autosaveTimer;
    private static bool _continueAfterReload;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        BuildEnvironment();
        _audio = new AudioManager { Name = "AudioManager" };
        AddChild(_audio);
        try
        {
            LoadMissionContent();
        }
        catch (Exception ex)
        {
            ShowFatalContentError(ex.Message);
            return;
        }
        if (HasUserArg("--selftest") || IsFinaleTest || IsTraversalTest ||
            (IsChapterTest && RuntimeSelfTest.ChapterPhase == 0))
            DeleteSelfTestSaves("selftest-save.json");
        else if (IsRecoveryTest && RuntimeSelfTest.RecoveryPhase == 0)
            DeleteSelfTestSaves("selftest-recovery-save.json");
        LoadSavedGameIfAvailable();
        ApplyDeveloperScenario();
        ApplySettings();

        _world = new WorldBuilder { Name = "WorldBuilder", ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_world);
        _world.Build();

        _player = new PlayerController
        {
            Name = "Player",
            Position = _world.PlayerSpawn,
            ProcessMode = ProcessModeEnum.Pausable,
        };
        AddChild(_player);

        _rig = new CameraRig { Name = "CameraRig", ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_rig);
        _player.Rig = _rig;
        _rig.SetTarget(_player, false, 8.2f, 18f, 1.55f, true);
        _rig.SetView(-72f, 18f, 8.2f);
        _player.Teleport(_world.PlayerSpawn, -Mathf.Pi / 2f);

        _hud = new Hud { Name = "Hud" };
        AddChild(_hud);
        _hud.ModalStateChanged += OnModalStateChanged;
        _hud.JobsButtonPressed += OpenJobBoard;
        _hud.HintButtonPressed += ShowCurrentHint;
        _player.TargetChanged += OnTargetChanged;

        _marker = new WorldMarker { ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_marker);

        _van = new VanController { ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_van);
        _van.Initialize(_world.VanSpawn, 0f, _hud);
        _van.EnterRequested += EnterVan;
        _van.Crashed += OnVanCrashed;

        CreateBuilderSystems();
        CreateStations();
        CreateChapterTwoWorldVisuals();
        SpawnPickups();
        SpawnVillagers();
        SpawnTraffic();
        RestoreSavedState();
        ReconcileRestoredProgress();
        RefreshMissionState();

        SyncControlMode();
        Input.MouseMode = Input.MouseModeEnum.Visible;

        if (HasUserArg("--scenario-m03"))
        {
            BeginScenario(new Vector3(34.2f, 0.2f, 4.2f), -Mathf.Pi / 2f,
                "SCENARIO: M01+M02 done, Build the Bridge active. Press E at the worksite.");
            return;
        }
        if (HasUserArg("--scenario-m04"))
        {
            BeginScenario(new Vector3(5f, 0.2f, 0.8f), Mathf.Pi,
                "SCENARIO: M01 done, Market Basket active. Press E at the SHOP.");
            return;
        }
        if (HasUserArg("--scenario-m05"))
        {
            BeginScenario(new Vector3(-31f, 0.2f, 18.5f), 0f,
                "SCENARIO: M05 active. Drive the yellow van through gates 1-2-3-4.", 5f);
            return;
        }
        if (HasUserArg("--scenario-m06"))
        {
            BeginScenario(new Vector3(-9f, 0.2f, 4.5f), Mathf.Pi,
                "SCENARIO: M06 active. Collect BLUE and ORANGE parcels at the post counter.", 5f);
            return;
        }
        if (HasUserArg("--scenario-m07"))
        {
            BeginScenario(new Vector3(-38f, 0.2f, 3.0f), 0f,
                "SCENARIO: M07 active. Press E by the broken swings.", 5f);
            return;
        }
        if (HasUserArg("--scenario-m08"))
        {
            BeginScenario(new Vector3(10f, 0.2f, 7.8f), Mathf.Pi,
                "SCENARIO: M08 active. Press E at the village green.", 5f);
            return;
        }
        if (HasUserArg("--scenario-m09"))
        {
            _van.GlobalPosition = new Vector3(56f, 0.15f, -2.0f);
            _van.Rotation = new Vector3(0, 0, 0);
            BeginScenario(new Vector3(52.8f, 0.2f, 2.0f), 0f,
                "SCENARIO: M09 active on the new long circuit. Drive gates 1-2-3-4; under 75 s is an optional bonus.", 5f);
            return;
        }
        if (HasUserArg("--scenario-m10"))
        {
            BeginScenario(new Vector3(-38f, 0.2f, 17.0f), Mathf.Pi,
                "SCENARIO: M10 active. Walk into your house and press E at FURNITURE.", 5f);
            return;
        }
        if (HasUserArg("--scenario-final"))
        {
            BeginScenario(new Vector3(-38f, 0.2f, 21.0f), Mathf.Pi,
                "SCENARIO: final job active. Furnish the house to trigger the village celebration.", 5f);
            return;
        }
        if (HasUserArg("--capture-title"))
        {
            _hud.ShowTitleScreen(false, false, null, StartAdventure, null, QuitGame);
            _ = CaptureTitle();
            return;
        }
        if (HasUserArg("--capture"))
        {
            _started = true;
            _player.SetControlEnabled(false);
            _ = CaptureTour();
            return;
        }
        if (HasUserArg("--capture-p11"))
        {
            _started = true;
            _hud.CloseModal();
            _player.SetControlEnabled(false);
            _ = CaptureP11();
            return;
        }
        if (HasUserArg("--capture-p12"))
        {
            _started = true;
            _hud.CloseModal();
            _player.SetControlEnabled(false);
            _ = CaptureP12();
            return;
        }
        if (HasUserArg("--capture-p13"))
        {
            _started = true;
            _hud.CloseModal();
            _player.SetControlEnabled(false);
            _ = CaptureP13();
            return;
        }
        if (HasUserArg("--capture-p14"))
        {
            _started = true;
            _hud.CloseModal();
            _player.SetControlEnabled(false);
            _ = CaptureP14();
            return;
        }
        if (HasUserArg("--perf-probe"))
        {
            _started = true;
            _hud.CloseModal();
            _player.SetControlEnabled(false);
            _ = RunPerformanceProbe();
            return;
        }
        ShowTitle();
        if (IsSelfTest)
        {
            var test = new RuntimeSelfTest { Name = "RuntimeSelfTest", Main = this };
            AddChild(test);
        }
        if (_continueAfterReload)
        {
            _continueAfterReload = false;
            ContinueAdventure();
        }
    }

    private static void DeleteSelfTestSaves(string fileName)
    {
        string directory = ProjectSettings.GlobalizePath("user://");
        if (!Directory.Exists(directory)) return;
        foreach (string pattern in new[]
                 {
                     fileName, fileName + ".bak",
                     fileName + ".corrupt-*", fileName + ".newer-*",
                 })
        {
            foreach (string path in Directory.GetFiles(directory, pattern))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Self-test cleanup failed: {ex.Message}");
                }
            }
        }
    }

    private void BeginScenario(Vector3 position, float yaw, string message, float seconds = 4.5f)
    {
        _started = true;
        _player.Teleport(position, yaw);
        _rig.SetView(Mathf.RadToDeg(yaw), 18f, 8.2f);
        SyncControlMode();
        RefreshMissionState();
        _hud.ShowBigToast(message, seconds);
    }

    private void LoadMissionContent()
    {
        string json = Godot.FileAccess.GetFileAsString("res://data/missions.json");
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("res://data/missions.json is missing or empty.");
        _catalog = MissionCatalog.FromJson(json);
        if (!MissionIntegrity.ValidateCatalog(_catalog, out string error))
            throw new InvalidDataException("Mission content is incomplete: " + error);
        _data = new GameData();
        _missions = new MissionService(_data, _catalog);
    }

    private void ShowFatalContentError(string message)
    {
        GD.PrintErr("FATAL: Patchwork Island could not load its mission content: " + message);
        if (DisplayServer.GetName() == "headless")
        {
            GetTree().Quit(1);
            return;
        }

        var layer = new CanvasLayer { Name = "FatalError", Layer = 100, ProcessMode = ProcessModeEnum.Always };
        AddChild(layer);
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);
        var shade = new ColorRect { Color = new Color("172434") };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(shade);
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);
        var panel = UiKit.Panel(center, Palette.Cream, Palette.Red, 24, 6);
        panel.CustomMinimumSize = new Vector2(760, 320);
        var margin = UiKit.Margin(panel, 24, 22, 24, 22);
        var box = UiKit.VBox(margin, 14);
        UiKit.Label(box, "Patchwork Island could not start", 34, Palette.Red);
        UiKit.Label(box,
            "The mission content is missing or incomplete. The game stopped instead of starting an unfinished village.",
            22, Palette.Navy);
        UiKit.Label(box, message, 17, new Color("42526B"));
        var quit = UiKit.Button(box, "Quit", 22, new Vector2(0, 52), new Color("FFE0D6"), Palette.Red);
        quit.Pressed += () => GetTree().Quit(1);
        Callable.From(quit.GrabFocus).CallDeferred();
    }

    private static bool IsRecoveryTest => HasUserArg("--selftest-recovery");

    private static bool IsLivingTest => HasUserArg("--selftest-living");

    private static bool IsRallyTest => HasUserArg("--selftest-rally");

    private static bool IsChapterTest => HasUserArg("--selftest-chapter");
    private static bool IsFinaleTest => HasUserArg("--selftest-finale");
    private static bool IsTraversalTest => HasUserArg("--selftest-traversal");

    private static bool IsSelfTest =>
        HasUserArg("--selftest") || HasUserArg("--selftest-resume") || IsRecoveryTest ||
        IsLivingTest || IsRallyTest || IsChapterTest || IsFinaleTest || IsTraversalTest;

    private static bool IsSelfTestResume => HasUserArg("--selftest-resume");

    private static bool LoadSelfTestSave =>
        IsSelfTestResume || IsRecoveryTest ||
        (IsChapterTest && RuntimeSelfTest.ChapterPhase > 0);

    /// <summary>
    /// F12, screenshot tours and scenario helpers are development tools. They
    /// are available in the editor, in an explicit --dev run, or for the
    /// existing scenario/capture/self-test command lines, never by accident in
    /// a normal player session.
    /// </summary>
    internal static bool DeveloperMode =>
        OS.HasFeature("editor") || HasUserArg("--dev") || IsDeveloperRun();

    private static string SavePath => ProjectSettings.GlobalizePath(
        IsRecoveryTest
            ? "user://selftest-recovery-save.json"
            : IsSelfTest
                ? "user://selftest-save.json"
                : "user://" + SaveService.DefaultFileName);

    private static bool IsDeveloperRun() =>
        IsSelfTest || OS.GetCmdlineUserArgs().Any(arg =>
            arg.StartsWith("--scenario") || arg.StartsWith("--capture"));

    private void LoadSavedGameIfAvailable()
    {
        if (IsDeveloperRun() && !LoadSelfTestSave) return;
        var loaded = SaveService.Load(SavePath, _catalog);
        _session.ApplyLoad(loaded);
        switch (loaded.Status)
        {
            case SaveLoadStatus.Loaded:
            case SaveLoadStatus.RecoveredBackup:
                _data = loaded.Data;
                _missions = new MissionService(_data, _catalog);
                _loadedFromSave = true;
                _saveNotice = loaded.RecoveredFromCorrupt || loaded.UsedBackup ? loaded.Message : "";
                break;

            case SaveLoadStatus.UnsupportedNewer:
                // Never replace a newer save merely because this build cannot
                // read it. A new game makes an explicit preservation copy first.
                break;

            case SaveLoadStatus.Unrecoverable:
                _saveNotice = loaded.Message + " Starting a fresh adventure.";
                break;

            case SaveLoadStatus.Missing:
                break;
        }
    }

    private void RestoreSavedState()
    {
        if (!_loadedFromSave) return;

        if (_data.Player.Valid)
        {
            _player.Teleport(new Vector3(_data.Player.X, _data.Player.Y, _data.Player.Z),
                _data.Player.RotationY);
            _rig.SetView(Mathf.RadToDeg(_data.Player.RotationY), 18f, 8.2f);
        }
        if (_data.Van.Valid)
        {
            _van.GlobalPosition = new Vector3(_data.Van.X, _data.Van.Y, _data.Van.Z);
            _van.Rotation = new Vector3(0, _data.Van.RotationY, 0);
        }
        if (_data.ActiveMission is "M05" or "M09" or "M19")
            _raceTimer = RaceTimer.Restore(_data, _data.ActiveMission);
        else
            _raceTimer = new RaceTimer();

        if (_data.Driving)
        {
            _driving = true;
            _player.SetControlEnabled(false);
            _player.Visible = false;
            _van.SetDriven(true);
            _rig.SetTarget(_van, true, 10.6f, 20f, 1.5f, true);
        }
        ApplySettings();
    }

    /// <summary>
    /// A save written between a world change and its reward (or after a crash)
    /// must not leave an objective that can never complete. Complete any active
    /// job whose physical objective is already satisfied, exactly once.
    /// </summary>
    private void ReconcileRestoredProgress()
    {
        if (_missions.ActiveMission == null) return;
        string missionId = _missions.ActiveMission.Id;
        if (!MissionRules.IsObjectiveComplete(_data, missionId)) return;

        int bonus = 0;
        double raceSeconds = 0;
        if (missionId is "M05" or "M09" or "M19")
        {
            raceSeconds = _raceTimer.ElapsedSeconds;
            bonus = _raceTimer.OptionalBonus(missionId);
        }
        var result = _missions.CompleteActive(bonus);
        if (!result.Success) return;
        result.RaceSeconds = raceSeconds;
        _recoveredJobNote = $"Recovered a finished job: {result.Message}";
    }

    private void ApplySettings()
    {
        _audio?.ApplySettings(_data.Settings);
        if (_marker != null)
            _marker.SetReducedMotion(_data.Settings.ReducedMotion);
    }

    private string RoamMusicId =>
        _data != null && _data.IsCompleted("M20") ? "festival" : "village";

    private void RefreshMusicContext()
    {
        if (!_started)
        {
            _audio?.PlayMusic("title");
            return;
        }
        var mission = _missions?.ActiveMission;
        if (_practiceRaceActive || mission?.Id is "M05" or "M09" or "M19")
            _audio?.PlayMusic("rally");
        else
            _audio?.PlayMusic(RoamMusicId);
    }

    private void CaptureSnapshots()
    {
        _data.Player = new PlayerSnapshot
        {
            X = _player.GlobalPosition.X,
            Y = _player.GlobalPosition.Y,
            Z = _player.GlobalPosition.Z,
            RotationY = _player.FacingYaw,
            Valid = true,
        };
        _data.Van = new VehicleSnapshot
        {
            X = _van.GlobalPosition.X,
            Y = _van.GlobalPosition.Y,
            Z = _van.GlobalPosition.Z,
            RotationY = _van.Rotation.Y,
            Valid = true,
        };
        _data.Driving = _driving;
    }

    private SaveResult SaveGame(SaveReason reason)
    {
        if (_world == null || _player == null || _van == null)
            return SaveResult.Invalid("The game world is not ready yet.");
        if (_session.NewerSavePresent && !_session.NewerReplacementAuthorized)
            return SaveResult.Invalid("A newer save is present; it was not overwritten.");

        SaveResult result;
        try
        {
            CaptureSnapshots();
            result = SaveService.TrySave(_data, SavePath, _catalog,
                _session.NewerReplacementAuthorized);
            if (result.Success)
                _session.RecordSaveSuccess();
            else
                _session.RecordSaveFailure();
        }
        catch (Exception ex)
        {
            // TrySave handles expected failures; this is a last-resort guard so
            // a save bug can never crash gameplay. Keep it visible and logged.
            GD.PrintErr($"Save failed (unexpected): {ex}");
            result = SaveResult.Invalid("An unexpected save error occurred. " + ex.Message, ex);
            _session.RecordSaveFailure();
        }

        if (!result.Success && result.Error != null)
        {
            if (result.Outcome == SaveOutcome.RecoverableFailure)
                GD.PrintErr($"Save failed (recoverable I/O): {result.Error}");
            else
                GD.PrintErr($"Save refused (invalid state): {result.Error}");
        }

        var notice = _saveFeedback.Record(reason, result);
        if (notice.Show)
            _hud.ShowToast(notice.Text, notice.IsError ? 5.5f : 2.8f,
                notice.IsError ? Palette.Red : Palette.Success);
        return result;
    }

    private void ApplyDeveloperScenario()
    {
        if (!DeveloperMode) return;
        if (HasUserArg("--scenario-m03"))
        {
            if (!_data.CompletedMissions.Contains("M01")) _data.CompletedMissions.Add("M01");
            if (!_data.CompletedMissions.Contains("M02")) _data.CompletedMissions.Add("M02");
            _data.Coins = 22;
            _data.ActiveMission = "M03";
            if (!_data.UnlockedCosmetics.Contains("red_cap")) _data.UnlockedCosmetics.Add("red_cap");
        }
        else if (HasUserArg("--scenario-m04"))
        {
            if (!_data.CompletedMissions.Contains("M01")) _data.CompletedMissions.Add("M01");
            _data.Coins = 10;
            _data.ActiveMission = "M04";
            if (!_data.UnlockedCosmetics.Contains("red_cap")) _data.UnlockedCosmetics.Add("red_cap");
        }
        else if (HasUserArg("--scenario-m05"))
        {
            foreach (string id in new[] { "M01", "M02", "M03", "M04" })
                if (!_data.CompletedMissions.Contains(id)) _data.CompletedMissions.Add(id);
            _data.BridgePieces.Clear();
            _data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
            _data.Coins = 54;
            _data.ActiveMission = "M05";
            if (!_data.UnlockedCosmetics.Contains("red_cap")) _data.UnlockedCosmetics.Add("red_cap");
        }
        else if (HasUserArg("--scenario-m06"))
        {
            foreach (string id in new[] { "M01", "M02", "M03", "M04" })
                if (!_data.CompletedMissions.Contains(id)) _data.CompletedMissions.Add(id);
            _data.BridgePieces.Clear();
            _data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
            _data.Coins = 54;
            _data.ActiveMission = "M06";
            if (!_data.UnlockedCosmetics.Contains("red_cap")) _data.UnlockedCosmetics.Add("red_cap");
        }
        else if (HasUserArg("--scenario-m07"))
        {
            foreach (string id in new[] { "M01", "M02", "M03", "M04" })
                if (!_data.CompletedMissions.Contains(id)) _data.CompletedMissions.Add(id);
            _data.BridgePieces.Clear();
            _data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
            _data.Coins = 54;
            _data.ActiveMission = "M07";
            if (!_data.UnlockedCosmetics.Contains("red_cap")) _data.UnlockedCosmetics.Add("red_cap");
        }
        else if (HasUserArg("--scenario-m08"))
        {
            foreach (string id in new[] { "M01", "M02", "M03", "M04", "M07" })
                if (!_data.CompletedMissions.Contains(id)) _data.CompletedMissions.Add(id);
            _data.BridgePieces.Clear();
            _data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
            _data.SwingPieces.Clear();
            _data.SwingPieces.AddRange(BuildRules.SwingSockets);
            _data.Coins = 72;
            _data.ActiveMission = "M08";
            if (!_data.UnlockedCosmetics.Contains("red_cap")) _data.UnlockedCosmetics.Add("red_cap");
        }
        else if (HasUserArg("--scenario-m09"))
        {
            foreach (string id in new[] { "M01", "M02", "M03", "M04", "M05" })
                if (!_data.CompletedMissions.Contains(id)) _data.CompletedMissions.Add(id);
            _data.BridgePieces.Clear();
            _data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
            _data.Coins = 69;
            _data.ActiveMission = "M09";
            if (!_data.UnlockedCosmetics.Contains("red_cap")) _data.UnlockedCosmetics.Add("red_cap");
        }
        else if (HasUserArg("--scenario-final"))
        {
            foreach (string id in new[] { "M01", "M02", "M03", "M04", "M05", "M06", "M07", "M08", "M09" })
                if (!_data.CompletedMissions.Contains(id)) _data.CompletedMissions.Add(id);
            _data.BridgePieces.Clear();
            _data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
            _data.SwingPieces.Clear();
            _data.SwingPieces.AddRange(BuildRules.SwingSockets);
            _data.ParkPlacements.Clear();
            _data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
            _data.ParkPlacements.Add(new PlacedItem("flowerbed", "park1"));
            _data.ParkPlacements.Add(new PlacedItem("lamp", "park2"));
            _data.Coins = 137;
            _data.ActiveMission = "M10";
            if (!_data.UnlockedCosmetics.Contains("red_cap")) _data.UnlockedCosmetics.Add("red_cap");
        }
        else if (HasUserArg("--scenario-m10"))
        {
            foreach (string id in new[] { "M01", "M02", "M03", "M04", "M07", "M08" })
                if (!_data.CompletedMissions.Contains(id)) _data.CompletedMissions.Add(id);
            _data.BridgePieces.Clear();
            _data.BridgePieces.AddRange(new[] { "support_0", "support_1", "support_2", "plank_0", "plank_1", "plank_2", "plank_3" });
            _data.SwingPieces.Clear();
            _data.SwingPieces.AddRange(BuildRules.SwingSockets);
            _data.ParkPlacements.Clear();
            _data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
            _data.ParkPlacements.Add(new PlacedItem("flowerbed", "park1"));
            _data.ParkPlacements.Add(new PlacedItem("lamp", "park2"));
            _data.Coins = 92;
            _data.ActiveMission = "M10";
            if (!_data.UnlockedCosmetics.Contains("red_cap")) _data.UnlockedCosmetics.Add("red_cap");
        }
    }

    private void BuildEnvironment()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky
            {
                SkyMaterial = new ProceduralSkyMaterial
                {
                    SkyTopColor = Color.FromHtml("47B5F5"),
                    SkyHorizonColor = Color.FromHtml("BCE9FF"),
                    GroundHorizonColor = Color.FromHtml("9CDD7A"),
                    GroundBottomColor = Color.FromHtml("6CB94A"),
                    SunAngleMax = 12f,
                },
            },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Color.FromHtml("D9EEFF"),
            AmbientLightEnergy = 0.32f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
            TonemapMode = Godot.Environment.ToneMapper.Linear,
            TonemapExposure = 1.0f,
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.18f,
            AdjustmentContrast = 1.04f,
            AdjustmentBrightness = 0.98f,
            FogEnabled = false,
        };
        AddChild(new WorldEnvironment { Environment = env });

        var sun = new DirectionalLight3D
        {
            Name = "Sun",
            RotationDegrees = new Vector3(-52, -34, 0),
            LightColor = Color.FromHtml("FFF9EE"),
            LightEnergy = 0.72f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 90f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            ShadowBias = 0.04f,
        };
        AddChild(sun);
    }

    private void CreateStations()
    {
        _jobStation = Interactable.Create(this, _world.JobBoard + new Vector3(0, 0.1f, -0.85f),
            "E: Read the job board", OpenJobBoard, null, true, 3.4f, "JobBoardStation");

        _redHouseStation = Interactable.Create(this, _world.RedHouseDoor + new Vector3(0, 0.1f, 1.0f),
            "E: Look at RED HOUSE", InteractRedHouse, null, true, 3.2f, "RedHouseStation");
        _redHouseStation.SetPrompt(RedHousePrompt);

        _workshopStation = Interactable.Create(this, _world.WorkshopDoor + new Vector3(0, 0.1f, 1.0f),
            "E: Look at WORKSHOP", InteractWorkshop, null, true, 3.2f, "WorkshopStation");
        _workshopStation.SetPrompt(WorkshopPrompt);

        _shopStation = Interactable.Create(this, _world.ShopDoor + new Vector3(0, 0.1f, -1.1f),
            "E: Look at the SHOP", InteractShop, null, true, 3.2f, "ShopStation");
        _shopStation.SetPrompt(ShopPrompt);

        _clothesStation = Interactable.Create(this, _world.ClothesShopDoor + new Vector3(0, 0.1f, -1.1f),
            "E: Visit the CLOTHES SHOP", InteractClothesShop, null, true, 3.4f, "ClothesShopStation");
        _clothesStation.SetPrompt(ClothesShopPrompt);

        _townHallStation = Interactable.Create(this, _world.TownHallDoor + new Vector3(0, 0.1f, -1.0f),
            "E: Look at TOWN HALL", InteractTownHall, null, true, 3.2f, "TownHallStation");
        _townHallStation.SetPrompt(TownHallPrompt);

        _bridgeStation = Interactable.Create(this, _world.BridgeSite,
            "E: Bridge worksite", InteractBridgeSite, null, true, 3.8f, "BridgeStation");
        _bridgeStation.SetPrompt(BridgeSitePrompt);

        _parkingStation = Interactable.Create(this, _world.ParkingSign + new Vector3(0, 0.1f, -0.8f),
            "E: Call van to parking", ResetVanAtSign, () => !_driving, true, 3.2f, "VanParkingStation");
        _parkingStation.SetPrompt(() => _driving ? "E: Parking sign" : "E: Call van to parking");

        _playgroundStation = Interactable.Create(this, new Vector3(-38f, 0.1f, -1.5f),
            "E: Playground worksite", InteractPlayground, null, true, 4.0f, "PlaygroundStation");
        _playgroundStation.SetPrompt(PlaygroundPrompt);

        _parkStation = Interactable.Create(this, _world.VillageGreenSign + new Vector3(0, 0.1f, -0.9f),
            "E: Village green", InteractPark, null, true, 4.0f, "ParkStation");
        _parkStation.SetPrompt(ParkPrompt);

        _furnitureStation = Interactable.Create(this, _world.FurnitureStation,
            "E: Furniture catalogue", InteractFurniture, null, true, 3.6f, "FurnitureStation");
        _furnitureStation.SetPrompt(FurniturePrompt);

        _houseStation = Interactable.Create(this, _world.PlayerHouseDoor + new Vector3(0, 0.1f, -1.0f),
            "E: Go inside YOUR HOUSE", InteractHouseDoor, null, true, 3.2f, "HouseStation");
        _houseStation.SetPrompt(() => _data.ActiveMission == "M10" ? "E: Go inside YOUR HOUSE" : "E: Look at YOUR HOUSE");

        for (int i = 0; i < _world.SignSocketPositions.Count; i++)
        {
            int index = i;
            var station = Interactable.Create(this, _world.SignSocketPositions[i] + new Vector3(0, 0.1f, 0),
                "E: Road sign socket", () => InteractSignSocket(index), null, true, 3.2f,
                $"SignSocketStation_{index}");
            station.SetPrompt(() => SignSocketPrompt(index));
            _signStations.Add(station);
        }
        for (int i = 0; i < _world.GardenPads.Count; i++)
        {
            int index = i;
            var station = Interactable.Create(this, _world.GardenPads[i] + new Vector3(0, 0.1f, 0),
                "E: Garden pad", () => InteractGardenPad(index), null, true, 3.0f,
                $"GardenPadStation_{index}");
            station.SetPrompt(() => GardenPadPrompt(index));
            _gardenStations.Add(station);
        }

        _postStation = Interactable.Create(this, _world.PostCounter + new Vector3(-2.5f, 0.1f, 0f),
            "E: Post counter", InteractPostCounter, null, true, 3.2f, "PostCounterStation");
        _postStation.SetPrompt(PostCounterPrompt);

        _repairStation = Interactable.Create(this,
            _world.RepairCarPosition + new Vector3(0, 0.1f, 2.4f),
            "E: Pip's broken car", InteractRepairCar, null, true, 3.4f, "RepairStation");
        _repairStation.SetPrompt(RepairPrompt);

        for (int i = 0; i < _world.FestivalTablePads.Count; i++)
        {
            int index = i;
            var station = Interactable.Create(this,
                _world.FestivalTablePads[i] + new Vector3(0, 0.1f, 0.9f),
                "E: Festival table pad", () => InteractFestivalTable(index), null, true, 3.0f,
                $"FestivalTableStation_{index}");
            station.SetPrompt(() => FestivalTablePrompt(index));
            _festivalTableStations.Add(station);
        }
        for (int i = 0; i < _world.FestivalLanternPads.Count; i++)
        {
            int index = i;
            var station = Interactable.Create(this,
                _world.FestivalLanternPads[i] + new Vector3(0, 0.1f, 0.8f),
                "E: Festival lantern hook", () => InteractFestivalLantern(index), null, true, 3.0f,
                $"FestivalLanternStation_{index}");
            station.SetPrompt(() => FestivalLanternPrompt(index));
            _festivalLanternStations.Add(station);
        }
    }

    private string SignSocketPrompt(int index)
    {
        string socketId = ChapterTwoRules.SignSockets[index].SocketId;
        string label = ChapterTwoRules.SignSockets[index].Label;
        if (_data.GetMissionCounter("M14", $"sign_{socketId}") >= 1)
            return $"E: {label} sign repaired";
        if (_data.ActiveMission == "M14")
            return $"E: Repair {label} sign socket";
        return "E: Road sign socket";
    }

    private string GardenPadPrompt(int index)
    {
        if (_data.ActiveMission == "M15")
            return $"E: Garden pad {index + 1}";
        if (_data.IsCompleted("M15"))
            return $"E: Garden pad {index + 1}";
        return "E: Community garden pad";
    }

    private string PostCounterPrompt()
    {
        if (_data.ActiveMission == "M16")
            return _data.GetMissionCounter("M16", "loaded") > 0
                ? "E: Post counter — parcels loaded"
                : "E: Load the three delivery parcels";
        if (_data.IsCompleted("M16")) return "E: Post counter — delivery route complete";
        return "E: Post counter";
    }

    private string RepairPrompt()
    {
        if (_data.IsCompleted("M18")) return "E: Pip's car — repaired";
        if (_data.ActiveMission == "M18") return "E: Repair Pip's car";
        return "E: Pip's broken car";
    }

    private string FestivalTablePrompt(int index)
    {
        if (_data.IsCompleted("M20")) return $"E: Festival table {index + 1}";
        if (_data.ActiveMission == "M20")
            return _data.GetMissionCounter("M20", FinaleRules.TableKey(index)) >= 1
                ? $"E: Take back table {index + 1}"
                : $"E: Place table {index + 1}";
        return $"E: Festival table pad {index + 1}";
    }

    private string FestivalLanternPrompt(int index)
    {
        if (_data.IsCompleted("M20")) return $"E: Festival lantern {index + 1}";
        if (_data.ActiveMission == "M20")
            return _data.GetMissionCounter("M20", FinaleRules.LanternKey(index)) >= 1
                ? $"E: Take down lantern {index + 1}"
                : $"E: Hang lantern {index + 1}";
        return $"E: Festival lantern hook {index + 1}";
    }

    private void InteractPostCounter()
    {
        if (_data.IsCompleted("M16"))
        {
            _hud.ShowBigToast("The delivery board shows all three parcels arrived.", 2.8f, Palette.Success);
            return;
        }
        if (_data.ActiveMission != "M16")
        {
            _hud.ShowHint("Start The Delivery Round at the yellow JOBS board first.");
            return;
        }
        var result = _missions.LoadDeliveryParcels();
        if (!result.Success)
        {
            _hud.ShowHint(result.Message);
            return;
        }
        AudioManager.PlayWorld("parcel_deliver", -6f);
        _hud.ShowBigToast(result.Message, 4.0f, Palette.Orange);
        RefreshMissionState();
    }

    private void InteractRepairCar()
    {
        if (_data.IsCompleted("M18"))
        {
            _hud.ShowBigToast("Pip's car is repaired and ready for the rally.", 2.6f, Palette.Success);
            return;
        }
        if (_data.ActiveMission != "M18")
        {
            _hud.ShowHint("Start Mechanic's Toolbox at the yellow JOBS board first.");
            return;
        }
        RepairUi.Show(_hud, _missions, result => OnMissionCompleted(result), RefreshMissionState);
    }

    private void InteractFestivalTable(int index)
    {
        if (_data.IsCompleted("M20"))
        {
            _hud.ShowBigToast("The festival tables are part of the finished celebration.", 2.6f, Palette.Success);
            return;
        }
        if (_data.ActiveMission != "M20")
        {
            _hud.ShowHint("Start Village Festival at the yellow JOBS board first.");
            return;
        }
        bool placed = _data.GetMissionCounter("M20", FinaleRules.TableKey(index)) >= 1;
        var result = placed
            ? _missions.RemoveFestivalTable(index)
            : _missions.PlaceFestivalTable(index);
        if (!result.Success)
        {
            AudioManager.PlayUi("ui_error");
            _hud.ShowHint(result.Message);
            return;
        }
        AudioManager.PlayWorld(placed ? "ui_cancel" : "build_place", -6f);
        _hud.ShowBigToast(result.Message, 2.4f, Palette.Lavender);
        RefreshMissionState();
    }

    private void InteractFestivalLantern(int index)
    {
        if (_data.IsCompleted("M20"))
        {
            _hud.ShowBigToast("The festival lanterns are part of the finished celebration.", 2.6f, Palette.Success);
            return;
        }
        if (_data.ActiveMission != "M20")
        {
            _hud.ShowHint("Start Village Festival at the yellow JOBS board first.");
            return;
        }
        bool hanging = _data.GetMissionCounter("M20", FinaleRules.LanternKey(index)) >= 1;
        var result = hanging
            ? _missions.RemoveFestivalLantern(index)
            : _missions.HangFestivalLantern(index);
        if (!result.Success)
        {
            AudioManager.PlayUi("ui_error");
            _hud.ShowHint(result.Message);
            return;
        }
        AudioManager.PlayWorld(hanging ? "ui_cancel" : "festival_pop", -5f);
        if (result.Completion != null)
        {
            OnMissionCompleted(result.Completion);
            return;
        }
        _hud.ShowBigToast(result.Message, 2.4f, Palette.Yellow);
        RefreshMissionState();
    }

    private void CreateChapterTwoWorldVisuals()
    {
        foreach (var root in _world.SignSocketRoots)
        {
            var socket = ChapterTwoRules.FindSocket(root.Name.ToString().Replace("SignSocket_", ""));
            if (socket == null) continue;
            var board = Geo.SignBoard(root, new Vector3(0, 2.05f, 0), 0f,
                $"{socket.Label} {ChapterTwoRules.ArrowFor(socket.Direction)}",
                Palette.Blue, 2.15f, 0.72f, null, 28);
            board.Visible = false;
            _signVisuals.Add(board);
        }

        foreach (var pad in _world.GardenPads)
        {
            var root = new Node3D { Name = "GardenFlower", Position = pad + new Vector3(0, 0.1f, 0), Visible = false };
            AddChild(root);
            Geo.Cylinder(root, new Vector3(0, 0.25f, 0), 0.035f, 0.035f, 0.5f, Palette.GrassDark, 5);
            var head = Geo.Sphere(root, new Vector3(0, 0.56f, 0), 0.17f, Palette.Red, 7, 4);
            _gardenFlowerRoots.Add(root);
            _gardenFlowerHeads.Add(head);
        }
    }

    private void InteractSignSocket(int index)
    {
        if (index < 0 || index >= ChapterTwoRules.SignSockets.Count) return;
        string socketId = ChapterTwoRules.SignSockets[index].SocketId;
        if (_data.IsCompleted("M14"))
        {
            _hud.ShowBigToast("This road sign is repaired and permanent.", 2.6f, Palette.Success);
            return;
        }
        if (_data.ActiveMission != "M14")
        {
            _hud.ShowHint("Start Road Sign Rescue at the yellow JOBS board first.");
            return;
        }
        SignSocketUi.Show(_hud, _missions, socketId,
            result => OnMissionCompleted(result), RefreshMissionState);
    }

    private void InteractGardenPad(int index)
    {
        if (index < 0 || index >= _world.GardenPads.Count) return;
        if (_data.IsCompleted("M15"))
        {
            _hud.ShowBigToast("The community garden is finished. Six flowers, three pairs!", 2.8f,
                Palette.Success);
            return;
        }
        if (_data.ActiveMission != "M15")
        {
            _hud.ShowHint("Start Garden Patterns at the yellow JOBS board first.");
            return;
        }
        GardenPadUi.Show(_hud, _missions, index,
            result => OnMissionCompleted(result), RefreshMissionState);
    }

    private void RefreshFinaleWorld()
    {
        if (_world == null) return;
        if (_world.DeliveryBoard != null)
            _world.DeliveryBoard.Visible = _data.IsCompleted("M16");
        if (_world.MarketStock != null)
            _world.MarketStock.Visible = _data.IsCompleted("M17");
        bool repaired = _data.IsCompleted("M18");
        if (_world.PipCarBroken != null) _world.PipCarBroken.Visible = !repaired;
        if (_world.PipCarFixed != null) _world.PipCarFixed.Visible = repaired;
        if (_world.RallyPodium != null)
            _world.RallyPodium.Visible = _data.IsCompleted("M19");
        for (int i = 0; i < _world.FestivalTableVisuals.Count; i++)
            _world.FestivalTableVisuals[i].Visible =
                _data.GetMissionCounter("M20", FinaleRules.TableKey(i)) >= 1;
        for (int i = 0; i < _world.FestivalLanternVisuals.Count; i++)
            _world.FestivalLanternVisuals[i].Visible =
                _data.GetMissionCounter("M20", FinaleRules.LanternKey(i)) >= 1;
        if (_world.FestivalDecor != null)
            _world.FestivalDecor.Visible = _data.IsCompleted("M20");
    }

    private void RefreshChapterTwoWorld()
    {
        for (int i = 0; i < _world.SignSocketRoots.Count && i < _signVisuals.Count; i++)
        {
            string socketId = ChapterTwoRules.SignSockets[i].SocketId;
            bool installed = _data.GetMissionCounter("M14", $"sign_{socketId}") >= 1;
            _signVisuals[i].Visible = installed;
            foreach (var child in _world.SignSocketRoots[i].GetChildren())
                if (child is Label3D label && label.Text == "?")
                    label.Visible = !installed;
        }

        for (int i = 0; i < _gardenFlowerRoots.Count && i < ChapterTwoRules.GardenPattern.Length; i++)
        {
            int code = _data.GetMissionCounter("M15", $"pad{i}");
            _gardenFlowerRoots[i].Visible = code != 0;
            if (_gardenFlowerHeads[i] != null)
                _gardenFlowerHeads[i].MaterialOverride = Geo.Mat(
                    code == 1 ? Palette.Red : code == 2 ? Palette.Blue : Palette.Stone);
        }

        for (int i = 0; i < _world.BakeryDisplaySlots.Count && i < ChapterTwoRules.BakeryOrders.Count; i++)
        {
            string customer = ChapterTwoRules.BakeryOrders[i].CustomerId;
            _world.BakeryDisplaySlots[i].Visible =
                _data.GetMissionCounter("M12", $"order_{customer}") >= 1;
        }
        for (int i = 0; i < _world.LibraryShelfSlots.Count && i < ChapterTwoRules.BookIds.Length; i++)
        {
            string book = ChapterTwoRules.BookIds[i];
            _world.LibraryShelfSlots[i].Visible =
                _data.GetMissionCounter("M13", ChapterTwoRules.ShelfKey(book)) >= 1;
        }
    }

    private string RedHousePrompt()
    {
        if (_data.ActiveMission == "M01")
        {
            int apples = _data.GetItem("apple");
            return apples >= 3 ? "E: Deliver 3 apples to RED HOUSE" : $"E: RED HOUSE — need 3 apples ({apples}/3)";
        }
        if (_data.ActiveMission == "M06")
        {
            if (_data.GetItem("blue_parcel") > 0) return "E: Deliver BLUE parcels to RED HOUSE";
            if (_data.GetMissionCounter("M06", "blueDelivered") < 2) return "E: RED HOUSE — need BLUE parcels";
            return "E: RED HOUSE — blue parcels done";
        }
        if (_data.IsCompleted("M01")) return "E: RED HOUSE — apples delivered";
        if (_data.IsCompleted("M06")) return "E: RED HOUSE — post delivered";
        return "E: Look at RED HOUSE";
    }

    private string WorkshopPrompt()
    {
        if (_data.ActiveMission == "M02")
        {
            int wood = _data.GetItem("wood");
            int stone = _data.GetItem("stone");
            return wood >= 5 && stone >= 3
                ? "E: Hand in 5 wood + 3 stone"
                : "E: WORKSHOP — needs 5 wood + 3 stone";
        }
        if (_data.ActiveMission == "M06")
        {
            if (_data.GetItem("orange_parcel") > 0) return "E: Deliver ORANGE parcel to WORKSHOP";
            if (_data.GetMissionCounter("M06", "orangeDelivered") < 1) return "E: WORKSHOP — need ORANGE parcel";
            return "E: WORKSHOP — orange parcel done";
        }
        if (_data.IsCompleted("M02")) return "E: WORKSHOP — supplies delivered";
        if (_data.IsCompleted("M06")) return "E: WORKSHOP — post delivered";
        return "E: Look at WORKSHOP";
    }

    private string ShopPrompt()
    {
        if (_data.ActiveMission == "M04") return "E: Shop for Market Basket";
        if (_data.ActiveMission == "M17") return "E: Market Day Maths checkout";
        if (_data.IsCompleted("M17")) return "E: Market stall — stocked";
        return "E: Look at the SHOP";
    }

    private string ClothesShopPrompt()
    {
        string wearing = CharacterCustomisation.Equipped(_data)?.Name ?? "Everyday";
        return $"E: CLOTHES SHOP — wearing {wearing}";
    }

    private string TownHallPrompt()
    {
        if (_data.IsCompleted("M04")) return "E: TOWN HALL — groceries delivered";
        if (_data.ActiveMission == "M04")
        {
            return BudgetRules.IsGroceryListComplete(_missions.GroceryBread, _missions.GroceryMilk)
                ? "E: Deliver groceries to TOWN HALL"
                : "E: TOWN HALL — basket not ready";
        }
        return "E: Look at TOWN HALL";
    }

    private string PlaygroundPrompt()
    {
        if (_data.IsCompleted("M07")) return "E: Playground — swings fixed";
        if (_data.ActiveMission == "M07") return "E: Fix the swings";
        return "E: Playground worksite";
    }

    private string ParkPrompt()
    {
        if (_data.IsCompleted("M08")) return "E: Village green — decorated";
        if (_data.ActiveMission == "M08") return "E: Decorate the village green";
        return "E: Village green";
    }

    private string FurniturePrompt()
    {
        if (_data.IsCompleted("M10")) return "E: Rearrange furniture";
        if (_data.ActiveMission == "M10") return "E: Furnish your home";
        return "E: Furniture catalogue";
    }

    private string BridgeSitePrompt()
    {
        if (_bridgeBuilder != null && _bridgeBuilder.IsBuilt) return "E: Bridge is open";
        if (_data.ActiveMission == "M03") return "E: Build the bridge";
        return "E: Bridge worksite";
    }

    private void InteractRedHouse()
    {
        if (_data.ActiveMission == "M01")
        {
            var result = _missions.DeliverApples();
            if (result.Success)
                OnMissionCompleted(result);
            else
                _hud.ShowHint(result.Message);
            return;
        }
        if (_data.ActiveMission == "M06")
        {
            var parcels = _missions.DeliverBlueParcels();
            if (!parcels.Success)
            {
                _hud.ShowHint(parcels.Message);
                return;
            }
            _hud.ShowBigToast(parcels.Message, 3f, Palette.Blue);
            FinishPostIfReady();
            return;
        }
        if (_data.IsCompleted("M01"))
            _hud.ShowBigToast("The Red House already has its apples.");
        else
            _hud.ShowHint("Start The Red House Delivery or Two-Stop Post at the yellow JOBS board.");
    }

    private void InteractWorkshop()
    {
        if (_data.ActiveMission == "M02")
        {
            var result = _missions.SubmitSupplies();
            if (result.Success)
                OnMissionCompleted(result);
            else
                _hud.ShowHint(result.Message);
            return;
        }
        if (_data.ActiveMission == "M06")
        {
            var parcels = _missions.DeliverOrangeParcel();
            if (!parcels.Success)
            {
                _hud.ShowHint(parcels.Message);
                return;
            }
            _hud.ShowBigToast(parcels.Message, 3f, Palette.Orange);
            FinishPostIfReady();
            return;
        }
        if (_data.IsCompleted("M02"))
            _hud.ShowBigToast("The workshop is fully supplied.");
        else
            _hud.ShowHint("Start Workshop Supplies or Two-Stop Post at the yellow JOBS board.");
    }

    private void FinishPostIfReady()
    {
        if (MissionRules.IsObjectiveComplete(_data, "M06"))
        {
            var completion = _missions.CompleteActive(0);
            if (completion.Success) OnMissionCompleted(completion);
        }
        else
        {
            RefreshMissionState();
        }
    }

    private void InteractHouseDoor()
    {
        if (_data.ActiveMission == "M10")
            _hud.ShowBigToast("Walk inside. The furniture catalogue is on the little stand.", 3.2f, Palette.Lavender);
        else if (_data.IsCompleted("M10"))
            _hud.ShowBigToast("Your cosy, furnished home.", 2.6f, Palette.Success);
        else
            _hud.ShowBigToast("Your cosy house. Furniture jobs open later.", 2.6f);
    }

    private void InteractPlayground()
    {
        if (_data.IsCompleted("M07"))
        {
            _hud.ShowBigToast("The swings are fixed. Press E at either seat for a swing!", 3.4f, Palette.Success);
            return;
        }
        if (_data.ActiveMission == "M07")
        {
            _swingBuilder?.OpenBuildMode();
            return;
        }
        _hud.ShowHint("Start Fix the Swings at the yellow JOBS board. The workshop unlocks it.");
    }

    private void InteractPark()
    {
        if (_data.IsCompleted("M08"))
        {
            _hud.ShowBigToast("Bench + flowerbed + lamp = a lovely village green.", 3.2f, Palette.Success);
            return;
        }
        if (_data.ActiveMission == "M08")
        {
            _parkBuilder?.OpenBuildMode();
            return;
        }
        _hud.ShowHint("Start Park Planner at the yellow JOBS board after fixing the swings.");
    }

    private void InteractFurniture()
    {
        if (_data.ActiveMission == "M10")
        {
            FurnitureShopUi.Show(_hud, _data, () => _furnitureBuilder?.OpenBuildMode(), RefreshMissionState);
            return;
        }
        if (_data.IsCompleted("M10"))
        {
            _furnitureBuilder?.OpenBuildMode();
            return;
        }
        _hud.ShowHint("Start Home Sweet Home at the yellow JOBS board after Park Planner.");
    }

    private void InteractShop()
    {
        if (_data.ActiveMission == "M04")
        {
            GroceryUi.Show(_hud, _missions, RefreshMissionState);
            return;
        }
        if (_data.ActiveMission == "M17")
        {
            MarketMathsUi.Show(_hud, _missions, result => OnMissionCompleted(result), RefreshMissionState);
            return;
        }
        if (_data.IsCompleted("M17"))
        {
            _hud.ShowBigToast("The market stall is stocked for the village festival.", 2.6f, Palette.Success);
            return;
        }
        if (_data.ActiveMission == "M01" && _data.GetItem("apple") < 3)
            _hud.ShowBigToast("The three apples are in the crate beside the SHOP.");
        else
            _hud.ShowInfo("SHOP", "The little market sells bread and milk. Start Market Basket at the JOBS board to use the 12-coin job purse.");
    }

    private void InteractClothesShop()
    {
        ClothesShopUi.Show(_hud, _data, PersistCustomisation);
    }

    /// <summary>The shop calls this after a purchase, outfit change or preset change.</summary>
    private SaveResult PersistCustomisation()
    {
        RefreshMissionState();
        return SaveGame(SaveReason.Customisation);
    }

    private void InteractTownHall()
    {
        if (_data.ActiveMission == "M04")
        {
            var result = _missions.DeliverGroceries();
            if (result.Success) OnMissionCompleted(result);
            else _hud.ShowHint(result.Message);
            return;
        }
        if (_data.IsCompleted("M04"))
            _hud.ShowBigToast("The Town Hall has its bread and milk. Thank you!");
        else
            _hud.ShowInfo("TOWN HALL", "The Town Hall clock keeps the village on time. Groceries and post arrive here.");
    }

    private void InteractBridgeSite()
    {
        if (_bridgeBuilder != null && _bridgeBuilder.IsBuilt)
        {
            _hud.ShowBigToast("The bridge is open. The meadow is across the river!", 3.2f, Palette.Success);
            return;
        }
        if (_data.ActiveMission == "M03")
        {
            _bridgeBuilder?.OpenBuildMode();
            return;
        }
        _hud.ShowHint("Start Build the Bridge at the yellow JOBS board. The workshop unlocks it after its supplies arrive.");
    }

    private void SpawnPickups()
    {
        SpawnItemSet("apple", "M01", 3, _world.AppleSpawnPoints, Palette.Red);
        SpawnItemSet("wood", "M02", 5, _world.WoodSpawnPoints, Palette.Wood);
        SpawnItemSet("stone", "M02", 3, _world.StoneSpawnPoints, Palette.Stone);
        SpawnParcelPickups();
        SpawnBookPickups();
        SpawnRepairParts();
    }

    private void SpawnParcelPickups()
    {        string[] types = { "blue_parcel", "blue_parcel", "orange_parcel" };
        for (int i = 0; i < types.Length && i < _world.ParcelSpawnPoints.Count; i++)
        {
            string type = types[i];
            string id = type == "blue_parcel" ? $"blue_parcel_{i}" : "orange_parcel_0";
            if (_data.HasCollected(id)) continue;
            int required = type == "blue_parcel" ? 2 : 1;
            var pickup = Pickup.Create(this, _world.ParcelSpawnPoints[i], id, type, "M06", required,
                TakePickup, () => PickupPrompt(type, "M06", required),
                () => _data.IsCompleted("M06"));
            pickup.ProcessMode = ProcessModeEnum.Pausable;
            _pickups.Add(pickup);
        }
    }

    private void SpawnBookPickups()
    {
        foreach (var entry in _world.BookSpawnPoints)
        {
            string pickupId = $"book_{entry.Book}";
            if (_data.HasCollected(pickupId)) continue;
            string book = entry.Book;
            var pickup = Pickup.Create(this, entry.Position, pickupId, pickupId, "M13", 1,
                TakeBookPickup,
                () => _data.ActiveMission == "M13"
                    ? $"E: Pick up the {book.ToUpperInvariant()} book"
                    : "E: A library book for Ollie",
                () => _data.IsCompleted("M13"));
            pickup.ProcessMode = ProcessModeEnum.Pausable;
            _pickups.Add(pickup);
        }
    }

    private void SpawnRepairParts()
    {
        if (_data.IsCompleted("M18")) return;
        string[] partTypes = { "tyre", "tyre", "lamp", "battery" };
        for (int i = 0; i < partTypes.Length && i < _world.RepairPartSpawnPoints.Count; i++)
        {
            string part = partTypes[i];
            string pickupId = part == "tyre" ? $"part_tyre_{i}" : $"part_{part}_0";
            if (_data.HasCollected(pickupId)) continue;
            var pickup = Pickup.Create(this, _world.RepairPartSpawnPoints[i], pickupId, part,
                "M18", 1, TakeRepairPart,
                () => _data.ActiveMission == "M18"
                    ? $"E: Pick up the {FinaleRules.PartLabel(part).ToLowerInvariant()}"
                    : "E: A repair part for Pip's car",
                () => _data.IsCompleted("M18"));
            pickup.ProcessMode = ProcessModeEnum.Pausable;
            _pickups.Add(pickup);
        }
    }

    private void TakeRepairPart(Pickup pickup)
    {
        if (_data.ActiveMission != "M18")
        {
            _hud.ShowHint("Start Mechanic's Toolbox at the yellow JOBS board first.");
            return;
        }
        var result = _missions.CollectRepairPart(pickup.ItemType);
        if (!result.Success)
        {
            _hud.ShowHint(result.Message);
            return;
        }
        pickup.CollectVisual();
        _pickups.Remove(pickup);
        AudioManager.PlayWorld("parcel_deliver", -6f);
        _hud.ShowToast(result.Message, 2.4f, Palette.Orange);
        RefreshMissionState();
        _player.ForceRefreshInteraction();
    }

    private void TakeBookPickup(Pickup pickup)
    {
        if (_data.ActiveMission != "M13")
        {
            _hud.ShowHint("Start Lost Library Books at the yellow JOBS board first.");
            return;
        }
        string book = pickup.ItemType.StartsWith("book_", StringComparison.Ordinal)
            ? pickup.ItemType.Substring(5)
            : "";
        var result = _missions.CollectBook(book);
        if (!result.Success)
        {
            _hud.ShowHint(result.Message);
            return;
        }
        pickup.CollectVisual();
        _pickups.Remove(pickup);
        AudioManager.PlayWorld("book_collect", -5f);
        _hud.ShowToast(result.Message, 2.4f, Palette.Blue);
        RefreshMissionState();
        _player.ForceRefreshInteraction();
    }

    private List<NpcOption> BuildNpcMissionOptions(NpcActor npc)
    {
        var options = new List<NpcOption>();
        if (_data.ActiveMission == "M11" && ChapterTwoRules.CardNpcIds.Contains(npc.NpcId))
        {
            foreach (string card in new[] { "card_ada", "card_ollie", "card_fern" })
            {
                string capturedCard = card;
                string cardNpc = ChapterTwoRules.NpcForCard(card);
                if (_data.GetMissionCounter("M11", $"card_{cardNpc}") <= 0) continue;
                options.Add(new NpcOption
                {
                    Text = $"Give {ChapterTwoRules.CardLabel(card)} card",
                    Name = $"GiveCard_{cardNpc}",
                    Border = Palette.Orange,
                    Close = false,
                    Action = () => CloseNpcDialogueThen(() =>
                    {
                        var result = _missions.HandWelcomeCard(npc.NpcId, capturedCard);
                        if (!result.Success)
                        {
                            AudioManager.PlayUi("ui_error");
                            _hud.ShowHint(result.Message);
                            return;
                        }
                        _audio?.PlaySfx("coin_collect", -7f);
                        if (result.Completion != null)
                            OnMissionCompleted(result.Completion);
                        else
                        {
                            _hud.ShowBigToast(result.Message, 3.2f, Palette.Success);
                            RefreshMissionState();
                        }
                    }),
                });
            }
        }

        if (_data.ActiveMission == "M12" && npc.NpcId == "ada")
        {
            options.Add(new NpcOption
            {
                Text = "Help with today's orders",
                Name = "BakeryHelp",
                Border = Palette.Orange,
                Close = false,
                Action = () => CloseNpcDialogueThen(() =>
                    BakeryUi.Show(_hud, _missions,
                        result => OnMissionCompleted(result), RefreshMissionState)),
            });
        }

        if (_data.ActiveMission == "M13" && npc.NpcId == "ollie")
        {
            options.Add(new NpcOption
            {
                Text = "Match books to shelves",
                Name = "LibraryHelp",
                Border = Palette.Blue,
                Close = false,
                Action = () => CloseNpcDialogueThen(() =>
                    LibraryShelfUi.Show(_hud, _missions,
                        result => OnMissionCompleted(result), RefreshMissionState)),
            });
        }

        if (_data.ActiveMission == "M15" && npc.NpcId == "fern")
        {
            options.Add(new NpcOption
            {
                Text = "Show me the garden pattern",
                Name = "GardenPatternHelp",
                Border = Palette.GrassDark,
                Close = false,
                Action = () => CloseNpcDialogueThen(() =>
                {
                    _hud.ShowHint("RED, BLUE, RED, BLUE, RED, BLUE. " +
                                 "Use the six garden pads east of the village green.");
                    AudioManager.PlayWorld("npc_greet_02", -8f);
                }),
            });
        }

        if (_data.ActiveMission == "M16" &&
            FinaleRules.DeliveryRecipients.Contains(npc.NpcId) &&
            _data.GetMissionCounter("M16", "loaded") > 0)
        {
            foreach (string recipient in FinaleRules.DeliveryRecipients)
            {
                if (_data.GetMissionCounter("M16", FinaleRules.CarryingKey(recipient)) <= 0)
                    continue;
                string captured = recipient;
                options.Add(new NpcOption
                {
                    Text = $"Deliver {FinaleRules.DeliveryName(recipient)} parcel",
                    Name = $"Deliver_{recipient}",
                    Border = Palette.Orange,
                    Close = false,
                    Action = () => CloseNpcDialogueThen(() => DeliverParcelFromVan(npc, captured)),
                });
            }
        }

        if (_data.ActiveMission == "M20" &&
            FinaleRules.FestivalInvitees.Contains(npc.NpcId) &&
            _data.GetMissionCounter("M20", FinaleRules.InvitedKey(npc.NpcId)) <= 0)
        {
            options.Add(new NpcOption
            {
                Text = "Give festival invitation",
                Name = "FestivalInvite",
                Border = Palette.Lavender,
                Close = false,
                Action = () => CloseNpcDialogueThen(() => InviteNeighbour(npc.NpcId)),
            });
        }

        return options;
    }

    private void CloseNpcDialogueThen(Action action)
    {
        if (_hud.ModalOpen) _hud.CloseModal();
        action?.Invoke();
    }

    private bool VanParkedNear(Vector3 position) =>
        _van != null && _van.GlobalPosition.DistanceTo(position) <= 10f &&
        Mathf.Abs(_van.Speed) < 0.8f;

    private void DeliverParcelFromVan(NpcActor npc, string recipientId)
    {
        if (!VanParkedNear(npc.GlobalPosition))
        {
            _hud.ShowHint("Park the van close to the neighbour and stop, then step out to deliver.");
            return;
        }
        var result = _missions.DeliverParcel(npc.NpcId, recipientId);
        if (!result.Success)
        {
            AudioManager.PlayUi("ui_error");
            _hud.ShowHint(result.Message);
            return;
        }
        AudioManager.PlayWorld("parcel_deliver", -5f);
        if (result.Completion != null)
        {
            OnMissionCompleted(result.Completion);
            return;
        }
        _hud.ShowBigToast(result.Message, 3f, Palette.Orange);
        RefreshMissionState();
    }

    private void InviteNeighbour(string npcId)
    {
        var result = _missions.InviteForFestival(npcId);
        if (!result.Success)
        {
            AudioManager.PlayUi("ui_error");
            _hud.ShowHint(result.Message);
            return;
        }
        AudioManager.PlayWorld("festival_pop", -7f);
        _hud.ShowBigToast(result.Message, 2.8f, Palette.Yellow);
        RefreshMissionState();
    }

    private void SpawnVillagers()
    {
        if (!NpcCatalog.Validate(out string error))
            throw new InvalidDataException("Villager content is invalid: " + error);
        foreach (var definition in NpcCatalog.All)
        {
            var actor = new NpcActor { Name = $"Npc_{definition.Id}" };
            actor.Initialize(definition);
            actor.TalkRequested += TalkToNpc;
            AddChild(actor);
            _npcs.Add(actor);
            _npcById[definition.Id] = actor;
        }
    }

    private void SpawnTraffic()
    {
        if (!TrafficCatalog.Validate(out string error))
            throw new InvalidDataException("Traffic content is invalid: " + error);
        foreach (var route in TrafficCatalog.Routes)
        {
            if (route.RallyOnly) continue;
            var car = new TrafficCar { Name = $"Traffic_{route.Id}" };
            car.Initialize(route,
                () => _player?.GlobalPosition ?? Vector3.Zero,
                () => _van?.GlobalPosition ?? Vector3.Zero,
                () => _driving);
            AddChild(car);
            _trafficCars.Add(car);
        }
    }

    private void TalkToNpc(NpcActor npc)
    {
        if (npc == null || _talkingNpc != null || !_started) return;
        if (_driving)
        {
            _hud.ShowBigToast("Park the van, then step out to chat.", 2.8f, Palette.Orange);
            return;
        }
        _talkingNpc = npc;
        npc.SetTalking(true);
        npc.FaceToward(_player.GlobalPosition);
        AudioManager.Instance?.PlayAt(npc.Definition.GreetingCue,
            npc.GlobalPosition + Vector3.Up * 1.4f, -10f);
        var options = new List<NpcOption>
        {
            new()
            {
                Text = "Tell me about the village",
                Close = false,
                Border = Palette.Blue,
                Action = () => _hud.SetModalStatus(npc.Definition.VillageFact, false),
            },
        };
        options.AddRange(BuildNpcMissionOptions(npc));
        NpcDialogueUi.Show(_hud, npc.Definition, npc.Definition.Intro, options, () =>
        {
            npc.SetTalking(false);
            _talkingNpc = null;
            RefreshMissionState();
        });
    }

    private void SpawnItemSet(string item, string missionId, int required, List<Vector3> positions, Color color)
    {
        for (int i = 0; i < positions.Count && i < required; i++)
        {
            string id = $"{item}_{i}";
            if (_data.HasCollected(id)) continue;
            var pickup = Pickup.Create(this, positions[i], id, item, missionId, required, TakePickup,
                () => PickupPrompt(item, missionId, required),
                () => _data.IsCompleted(missionId));
            pickup.ProcessMode = ProcessModeEnum.Pausable;
            _pickups.Add(pickup);
        }
    }

    private string PickupPrompt(string item, string missionId, int required)
    {
        if (_data.IsCompleted(missionId)) return "E: Already delivered";
        if (_data.ActiveMission != missionId)
            return $"E: {MissionService.ItemName(item)} for a job";
        int count = _data.GetItem(item);
        if (count >= required) return "E: You have enough";
        return $"E: Pick up {MissionService.ItemName(item).ToLowerInvariant()} ({count + 1}/{required})";
    }

    private void TakePickup(Pickup pickup)
    {
        if (_data.IsCompleted(pickup.MissionId))
        {
            _hud.ShowBigToast("That job is already finished.");
            return;
        }
        if (_data.ActiveMission != pickup.MissionId)
        {
            string title = _catalog.Get(pickup.MissionId)?.Title ?? pickup.MissionId;
            _hud.ShowHint($"Start {title} at the yellow JOBS board first.");
            return;
        }

        var result = _missions.CollectPickup(pickup.PickupId, pickup.ItemType, pickup.MissionId, pickup.Required);
        if (!result.Success)
        {
            _hud.ShowHint(result.Message);
            return;
        }

        pickup.CollectVisual();
        _pickups.Remove(pickup);
        RefreshMissionState();
        _hud.ShowToast(result.Message, 2.1f, ItemColor(pickup.ItemType));
        _player.ForceRefreshInteraction();
    }

    private static Color ItemColor(string item) => item switch
    {
        "apple" => Palette.Red,
        "wood" => Palette.Wood,
        "stone" => Palette.Stone,
        "blue_parcel" => Palette.Blue,
        "orange_parcel" => Palette.Orange,
        "book_sun" => Palette.Yellow,
        "book_moon" => Palette.Blue,
        "book_tree" => Palette.Grass,
        "book_star" => Palette.Pink,
        _ => Palette.Yellow,
    };

    private void StartAdventure()
    {
        _started = true;
        if (_driving)
        {
            _player.Visible = false;
            _van.SetDriven(true);
            _rig.SetTarget(_van, true, 10.6f, 20f, 1.5f, true);
        }
        else
        {
            _player.Visible = true;
            _van.SetDriven(false);
            _rig.SetTarget(_player, false, 8.2f, 18f, 1.55f, true);
        }
        _hud.CloseModal();
        SyncControlMode();
        ApplySettings();
        RefreshMissionState();
        RefreshMusicContext();
        _autosaveTimer = 20f;
        if (AllMissionsComplete() && !_data.CelebrationSeen)
            Callable.From(ShowVillageCelebration).CallDeferred();
        else if (AllTwentyMissionsComplete() && !_data.FestivalSeen)
            Callable.From(ShowVillageFestival).CallDeferred();

        if (!string.IsNullOrEmpty(_recoveredJobNote))
        {
            _hud.ShowBigToast(_recoveredJobNote, 6f, Palette.Success);
            _recoveredJobNote = "";
            SaveGame(SaveReason.Recovery);
            return;
        }
        if (!string.IsNullOrEmpty(_saveNotice))
        {
            _hud.ShowBigToast(_saveNotice, 6f, Palette.Orange);
            _saveNotice = "";
            return;
        }
        if (!_session.NewerSavePresent && !_data.IsTutorialSeen("walk"))
        {
            _data.MarkTutorial("walk");
            _hud.ShowBigToast("Walk with WASD or the arrow keys. E interacts, J opens the job board, Esc pauses.", 6f,
                Palette.Yellow);
            SaveGame(SaveReason.Autosave);
        }
        else if (_missions.ActiveMission != null)
        {
            _hud.ShowBigToast($"Welcome back. {_missions.ActiveMission.Title} is waiting for you.", 4f, Palette.Yellow);
        }
        else
        {
            _hud.ShowBigToast("Find the yellow JOBS board near the town hall, or press J anywhere.", 5f,
                Palette.Yellow);
        }
    }

    private void ContinueAdventure()
    {
        if (!_session.CanContinue)
        {
            ShowTitle();
            return;
        }

        // Continue always represents the last successfully persisted save. If
        // the current memory session was discarded or never persisted (for
        // example a failed New Game), reload the scene so every builder,
        // pickup, placement and snapshot is rebuilt from that save.
        if (!_session.MemoryMatchesPersisted)
        {
            _continueAfterReload = true;
            Error error = GetTree().ReloadCurrentScene();
            if (error != Error.Ok)
            {
                _continueAfterReload = false;
                _hud.ShowBigToast("The saved village could not be reloaded. Please try again.", 6f, Palette.Red);
            }
            return;
        }

        if (string.IsNullOrEmpty(_saveNotice))
            _saveNotice = "Welcome back to Patchwork Island.";
        StartAdventure();
    }

    private void StartNewGameFromTitle()
    {
        if (!_session.HasSaveFile)
        {
            // There is no persisted session to replace, but a discarded
            // in-memory session must not leak through the title's New Game
            // button either. Choose a character first, then start fresh.
            ShowCharacterChoice(preset => BeginNewGameFromTitle(preset), ShowTitle);
            return;
        }
        _hud.Confirm("Start a new game?",
            _session.NewerSavePresent
                ? "This will replace the newer save with a fresh adventure. A preserved copy of the newer save will be kept first."
                : "This will replace the saved village with a fresh adventure. Your current save will be overwritten.",
            "Start a new game",
            () => ShowCharacterChoice(preset => BeginNewGameFromTitle(preset), ShowTitle),
            "Cancel", ShowTitle);
    }

    private void ShowCharacterChoice(Action<string> onChosen, Action onCancel)
    {
        // A fresh village always begins with the default Everyday outfit; the
        // chosen preset is applied by ResetSession below.
        CharacterChoiceUi.Show(_hud, onChosen, onCancel);
    }

    private SaveResult BeginNewGameFromTitle(string preset)
    {
        var result = ResetSession(preset);
        StartAdventure();
        if (!result.Success)
            _hud.ShowBigToast(
                "The new game is running, but it could not be saved yet. Use Save now from the pause menu to try again.",
                7f, Palette.Red);
        return result;
    }

    private void ShowTitle()
    {
        _started = false;
        SyncControlMode();
        _audio?.PlayMusic("title");
        _hud.ShowTitleScreen(_session.HasSaveFile, _session.CanContinue, ContinueAdventure,
            StartNewGameFromTitle, OpenSettingsFromTitle, QuitGame, _session.TitleNotice);
    }

    private void OpenSettingsFromTitle() => OpenSettings(ShowTitle);

    private void OnModalStateChanged(bool open)
    {
        // A close callback may already have opened the next modal. The Hud only
        // reports real state transitions, and this guard protects older callers.
        if (!open && _hud.ModalOpen) return;
        if (open) _hud.SetPrompt(null);
        SyncControlMode();
    }

    /// <summary>
    /// One authoritative transition point for player, van, mouse and world
    /// pause state. Scene code only changes the underlying flags and calls this.
    /// </summary>
    private void SyncControlMode()
    {
        if (_hud == null || _player == null || _van == null) return;
        var mode = ControlModeRules.Resolve(_started, _hud.ModalOpen, _buildMode, _driving);
        var previous = _controlMode;
        _controlMode = mode;

        _player.SetControlEnabled(ControlModeRules.PlayerProcessesInput(mode));
        _van.SetInputEnabled(ControlModeRules.VehicleProcessesInput(mode));
        GetTree().Paused = ControlModeRules.PausesWorld(mode);

        var mouse = ControlModeRules.CapturesMouse(mode)
            ? Input.MouseModeEnum.Captured
            : Input.MouseModeEnum.Visible;
        if (Input.MouseMode != mouse) Input.MouseMode = mouse;

        if (!ControlModeRules.PlayerProcessesInput(mode) && !ControlModeRules.VehicleProcessesInput(mode))
            _hud.SetPrompt(null);
        if (mode == ControlMode.Walking && previous != ControlMode.Walking)
            _player.ForceRefreshInteraction();
    }

    private void OpenJobBoard()
    {
        if (!_started) return;
        if (_buildMode)
        {
            _hud.ShowBigToast("Finish this build first, then choose another job.", 2.6f, Palette.Lavender);
            return;
        }
        var active = _missions.ActiveMission;
        bool showChapterTwo = active != null
            ? string.CompareOrdinal(active.Id, "M11") >= 0
            : MissionRules.OriginalChapterComplete(_data);
        JobBoardUi.Show(_hud, _missions, _implemented, StartMissionFromBoard, StartPracticeRace,
            showChapterTwo);
    }

    private void StartMissionFromBoard(string missionId)
    {
        _practiceRaceActive = false;
        var result = _missions.StartMission(missionId);
        _hud.CloseModal();
        if (!result.Success)
        {
            _hud.ShowHint(result.Message);
            return;
        }
        if (missionId is "M05" or "M09" or "M19")
            _raceTimer = RaceTimer.Restore(_data, missionId);
        else
            _raceTimer = new RaceTimer();
        RefreshMissionState();
        SaveGame(SaveReason.MissionStart);
        RefreshMusicContext();
        var mission = _missions.ActiveMission;
        string firstHint = mission?.Hints != null && mission.Hints.Count > 0 ? mission.Hints[0] : "Follow the glowing marker.";
        _hud.ShowBigToast($"{result.Message} {firstHint}", 5f, Palette.Yellow);
    }

    private void ShowCurrentHint()
    {
        if (!_started) return;
        if (_missions.ActiveMission == null)
        {
            _hud.ShowHint("Find the bright yellow JOBS board near the town hall. It is marked with a star.");
            return;
        }
        var hint = _missions.GetNextHint(_missions.ActiveMission.Id);
        _hud.ShowHint(hint.Text);
    }

    private void OnTargetChanged(Interactable target)
    {
        _hud.SetPrompt(target?.GetPrompt());
    }

    // ---------------------------------------------------------------- driving

    private void EnterVan()
    {
        if (!_started || _hud.ModalOpen || _buildMode || _driving) return;
        if (_vehicleSwitchCooldown > 0f) return;
        if (_player.GlobalPosition.DistanceTo(_van.GlobalPosition) > 5.2f)
        {
            _hud.ShowHint("Walk closer to the yellow van, then press E.");
            return;
        }
        _driving = true;
        _player.SetControlEnabled(false);
        _player.Visible = false;
        _van.SetDriven(true);
        _rig.SetTarget(_van, true, 10.6f, 20f, 1.5f, true);
        SyncControlMode();
        _hud.SetPrompt(null);
        bool first = !_data.IsTutorialSeen("drive");
        if (first)
        {
            _data.MarkTutorial("drive");
            _hud.ShowBigToast("Drive: W/S go and brake/reverse. A/D steer (or turn in place at a stop). E exits when slow. R resets the van.", 7f, Palette.Yellow);
        }
        else
        {
            _hud.ShowBigToast("W/S drive · A/D steer or pivot at a stop · E exit when slow · R reset", 3.4f, Palette.Yellow);
        }
        RefreshMissionState();
    }

    private void ExitVan()
    {
        if (!_driving) return;
        if (Mathf.Abs(_van.Speed) > 2.5f)
        {
            _hud.ShowBigToast("Slow down, then press E to get out.", 2.2f, Palette.Orange);
            return;
        }

        if (!FindSafeExitPosition(out Vector3 spot))
        {
            _van.ResetToParking();
            if (!FindSafeExitPosition(out spot))
                spot = _world.PlayerSpawn;
            _hud.ShowBigToast("Van moved to a safe parking spot so you can step out.", 3f, Palette.Water);
        }

        _driving = false;
        _van.SetDriven(false);
        _player.Visible = true;
        _player.Teleport(spot, _van.Rotation.Y);
        _rig.SetTarget(_player, false, 8.2f, 18f, 1.55f, true);
        _rig.SetView(Mathf.RadToDeg(_van.Rotation.Y), 18f, 8.2f);
        _vehicleSwitchCooldown = 0.45f;
        SyncControlMode();
        _hud.ShowToast("Out of the van. E gets back in.", 2.2f, Palette.Yellow);
        RefreshMissionState();
    }

    private bool FindSafeExitPosition(out Vector3 result)
    {
        result = default;
        var space = GetWorld3D().DirectSpaceState;
        var exclude = new Godot.Collections.Array<Rid> { _van.GetRid(), _player.GetRid() };
        foreach (var candidate in _van.GetExitCandidates())
        {
            var query = PhysicsRayQueryParameters3D.Create(
                new Vector3(candidate.X, 3f, candidate.Z),
                new Vector3(candidate.X, -4f, candidate.Z), 1u, exclude);
            var hit = space.IntersectRay(query);
            if (hit.Count == 0) continue;
            Vector3 ground = (Vector3)hit["position"];
            if (ground.Y < -0.5f) continue;
            bool onBridge = _bridgeBuilder != null && _bridgeBuilder.IsBuilt &&
                            ground.X > 39f && ground.X < 51f && Mathf.Abs(ground.Z) < 4.3f;
            if (ground.X > 40.6f && ground.X < 49.4f && !onBridge) continue;

            Vector3 stand = new(ground.X, ground.Y + 0.12f, ground.Z);
            var shape = new PhysicsShapeQueryParameters3D
            {
                Shape = new CapsuleShape3D { Radius = 0.34f, Height = 1.5f },
                Transform = new Transform3D(Basis.Identity, stand + Vector3.Up * 0.86f),
                CollisionMask = 1u,
                Exclude = exclude,
            };
            var overlaps = space.IntersectShape(shape);
            if (overlaps.Count > 0) continue;
            result = stand;
            return true;
        }
        return false;
    }

    private void ResetVanWithToast()
    {
        if (!_driving) return;
        _van.ResetToParking();
        _rig.SetTarget(_van, true, 10.6f, 20f, 1.5f, true);
        _hud.ShowBigToast("Van reset to its parking spot.", 3f, Palette.Water);
        RefreshMissionState();
    }

    private void ResetVanAtSign()
    {
        if (_driving) return;
        _van.ResetToParking();
        _hud.ShowBigToast("Van called back to its parking spot.", 3f, Palette.Yellow);
        RefreshMissionState();
    }

    private void OnVanCrashed()
    {
        _hud.ShowBigToast("Splash! The van was recovered to its parking spot.", 3.6f, Palette.Water);
        if (_driving)
            _rig.SetTarget(_van, true, 10.6f, 20f, 1.5f, true);
        RefreshMissionState();
    }

    // --------------------------------------------------------- free-roam practice

    private void StartPracticeRace()
    {
        if (!_data.IsCompleted("M05") && !_data.IsCompleted("M09"))
        {
            _hud.ShowHint("Finish Village Road Rally before practising the race.");
            return;
        }
        if (_missions.ActiveMission != null)
        {
            _hud.ShowHint("Finish or switch your current job before a practice race.");
            return;
        }

        _hud.CloseModal();
        _practiceRaceActive = true;
        _practiceCheckpoint = 0;
        _practiceElapsed = 0f;
        _practiceCheckTimer = 0f;
        _practiceHudTimer = 0f;
        _audio?.PlayMusic("rally");
        _hud.ShowBigToast(
            "Practice race: get into the yellow van and drive through gates 1-2-3-4. No rewards, no time limit.",
            5f, Palette.Yellow);
        RefreshMissionState();
    }

    private void TickPracticeRace(float dt)
    {
        if (!_practiceRaceActive || !_driving) return;

        if (_practiceCheckpoint > 0 && _practiceCheckpoint < 4)
            _practiceElapsed += dt;

        _practiceHudTimer -= dt;
        if (_practiceHudTimer <= 0f)
        {
            _practiceHudTimer = 0.8f;
            RefreshMissionState();
        }

        _practiceCheckTimer -= dt;
        if (_practiceCheckTimer > 0f || _practiceCheckpoint >= 4) return;
        _practiceCheckTimer = 0.15f;

        Vector3 gate = _world.RaceGates[_practiceCheckpoint].Position;
        float distance = new Vector2(_van.GlobalPosition.X - gate.X, _van.GlobalPosition.Z - gate.Z).Length();
        if (distance > 7f) return;

        _practiceCheckpoint++;
        _audio?.PlaySfx("race_checkpoint");
        if (_practiceCheckpoint == 1)
        {
            _practiceElapsed = 0f;
            _hud.ShowBigToast("Gate 1! Practice timer started.", 2.4f, Palette.Yellow);
        }
        else if (_practiceCheckpoint < 4)
        {
            _hud.ShowBigToast($"Gate {_practiceCheckpoint}! Next: gate {_practiceCheckpoint + 1}.", 2.2f, Palette.Lavender);
        }
        else
        {
            float time = _practiceElapsed;
            _practiceRaceActive = false;
            _practiceCheckpoint = 0;
            _practiceElapsed = 0f;
            _audio?.PlaySfx("race_finish");
            RefreshMusicContext();
            _hud.ShowBigToast(
                time <= 75f
                    ? $"Practice lap complete in {FormatTime(time)} — a gold-lap pace!"
                    : $"Practice lap complete in {FormatTime(time)}. Nicely driven!",
                5f, Palette.Yellow);
        }
        RefreshMissionState();
    }

    private void RefreshPracticeMissionUi()
    {
        var rows = new List<(string Icon, string Text)>
        {
            ("flag", $"Gate {_practiceCheckpoint} / 4"),
            ("star", _practiceCheckpoint > 0 && _practiceCheckpoint < 4
                ? $"Practice time {FormatTime(_practiceElapsed)}"
                : "No rewards · any pace is fine"),
        };
        _hud.SetMission("Practice Race", "Drive the yellow van through gates 1-2-3-4. Just for fun!", "flag", rows);

        if (!_driving)
        {
            SetObjective(_van.GlobalPosition, "YELLOW VAN", "van", Palette.Yellow);
        }
        else if (_practiceCheckpoint == 0 && _van.GlobalPosition.X < 40f)
        {
            SetObjective(_world.BridgeCenter, "BRIDGE TO MEADOW", "waypoint", Palette.Water);
        }
        else if (_practiceCheckpoint < 4)
        {
            var gate = _world.RaceGates[_practiceCheckpoint];
            SetObjective(gate.Position, $"GATE {_practiceCheckpoint + 1}", "flag", Palette.Yellow);
        }
        else
        {
            SetObjective(_world.RaceStartFlag.GlobalPosition, "FINISH", "flag", Palette.Yellow);
        }
    }

    private void TickRace(float dt)
    {
        var mission = _missions.ActiveMission;
        if (!_driving || mission == null ||
            mission.Id is not ("M05" or "M09" or "M19"))
            return;
        int gateCount = mission.Id == "M19" ? FinaleRules.RallyGateCount : 4;
        var gates = mission.Id == "M19" ? _world.M19RaceGates : _world.RaceGates;
        int checkpoint = _data.GetMissionCounter(mission.Id, "checkpoint");
        if (checkpoint > 0 && checkpoint < gateCount)
        {
            _raceTimer.Tick(dt);
            _raceTimer.Store(_data, mission.Id);
        }

        _raceHudTimer -= dt;
        if (_raceHudTimer <= 0f)
        {
            _raceHudTimer = 0.8f;
            RefreshMissionState();
        }

        _raceCheckTimer -= dt;
        if (_raceCheckTimer > 0f || checkpoint >= gateCount) return;
        _raceCheckTimer = 0.15f;

        Vector3 gate = gates[checkpoint].Position;
        float distance = new Vector2(_van.GlobalPosition.X - gate.X, _van.GlobalPosition.Z - gate.Z).Length();
        if (distance > 7.0f) return;

        var advance = _missions.AdvanceCheckpoint(checkpoint + 1);
        if (!advance.Success)
        {
            if (_raceWrongHintCooldown <= 0f)
            {
                _hud.ShowHint(advance.Message);
                _raceWrongHintCooldown = 2.5f;
            }
            return;
        }

        if (checkpoint == 0)
        {
            _raceTimer.Restart();
            _data.SetMissionCounter(mission.Id, "started", 1);
        }
        _raceTimer.Store(_data, mission.Id);
        _audio?.PlaySfx("race_checkpoint");
        _hud.ShowBigToast(advance.Message, 2.4f, Palette.Yellow);
        RefreshMissionState();

        if (checkpoint + 1 >= gateCount)
        {
            double raceSeconds = _raceTimer.ElapsedSeconds;
            int bonus = _raceTimer.OptionalBonus(mission.Id);
            var result = _missions.CompleteActive(bonus);
            if (result.Success)
            {
                result.RaceSeconds = raceSeconds;
                _audio?.PlaySfx("race_finish");
                OnMissionCompleted(result);
                _raceTimer = new RaceTimer();
            }
        }
    }

    private static string FormatTime(double seconds) => RaceTimer.Format(seconds);

    private IBuildMode GetActiveBuildMode()
    {
        foreach (var mode in _buildModes)
            if (mode != null && mode.Active) return mode;
        return null;
    }

    private void CreateBuilderSystems()
    {
        _buildModes.Clear();

        _bridgeBuilder = new BridgeBuilder { ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_bridgeBuilder);
        _bridgeBuilder.Initialize(_data, _missions, _rig, _hud, _world, OnBridgeBuilt, SetBuildMode, RefreshMissionState);

        _swingBuilder = new SwingBuilder { ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_swingBuilder);
        _swingBuilder.Initialize(_data, _missions, _rig, _hud, _world, OnSwingsBuilt, SetBuildMode, RefreshMissionState);

        _parkBuilder = new ParkBuilder { ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_parkBuilder);
        _parkBuilder.Initialize(_data, _missions, _rig, _hud, OnParkBuilt, SetBuildMode, RefreshMissionState);
        _parkBuilder.SetPads(_world.ParkPads.ToArray());

        _furnitureBuilder = new FurnitureBuilder { ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_furnitureBuilder);
        _furnitureBuilder.Initialize(_data, _missions, _rig, _hud, OnHomeFurnished, SetBuildMode, RefreshMissionState);
        _furnitureBuilder.SetPads(_world.HousePads.ToArray());

        _buildModes.Add(_bridgeBuilder);
        _buildModes.Add(_swingBuilder);
        _buildModes.Add(_parkBuilder);
        _buildModes.Add(_furnitureBuilder);
    }

    private void OnSwingsBuilt()
    {
        var result = _missions.CompleteActive(0);
        if (result.Success) OnMissionCompleted(result);
    }

    private void OnParkBuilt()
    {
        var result = _missions.CompleteActive(0);
        if (result.Success) OnMissionCompleted(result);
    }

    private void OnHomeFurnished()
    {
        var result = _missions.CompleteActive(0);
        if (result.Success) OnMissionCompleted(result);
    }

    private void OnBridgeBuilt()
    {
        var result = _missions.CompleteActive(0);
        if (result.Success)
            OnMissionCompleted(result);
        else
            RefreshMissionState();
    }

    private void SetBuildMode(bool active)
    {
        _buildMode = active;
        if (active)
        {
            _hud.SetPrompt(null);
            if (_marker != null) _marker.Visible = false;
            if (!_data.IsTutorialSeen("build"))
            {
                _data.MarkTutorial("build");
                _hud.ShowBigToast("Click a glowing pad or socket to build. Wrong pieces never cost anything. Esc steps away.", 5f,
                    Palette.Lavender);
                SaveGame(SaveReason.Autosave);
            }
        }
        else
        {
            _hud.HideBuildBar();
            RefreshMissionState();
        }
        SyncControlMode();
    }

    private void RefreshMissionState()
    {
        _hud.SetCoins(_data.Coins);
        ApplyCosmetics();
        RefreshPickupVisibility();
        RefreshRaceGateVisibility();
        RefreshChapterTwoWorld();
        RefreshFinaleWorld();
        EnsureRallyRival();
        if (_world.GoldCourseFlag != null)
            _world.GoldCourseFlag.Visible = _data.IsCompleted("M09");
        if (_world.CelebrationDecor != null)
            _world.CelebrationDecor.Visible = AllMissionsComplete();

        if (_practiceRaceActive)
        {
            RefreshPracticeMissionUi();
            return;
        }

        var mission = _missions.ActiveMission;
        if (mission == null)
        {
            _hud.SetNoMission();
            SetObjective(_world.JobBoard, "JOBS BOARD", "star", Palette.Yellow);
            return;
        }

        _hud.SetMission(mission.Title, mission.Short, JobBoardUi.IconFor(mission.Id), ProgressRows(mission.Id));

        switch (mission.Id)
        {
            case "M01":
            {
                int apples = _data.GetItem("apple");
                if (apples < 3)
                    SetObjective(_world.AppleCrate, "SHOP APPLES", "apple", Palette.Red);
                else
                    SetObjective(_world.RedHouseDoor, "RED HOUSE", "house", Palette.Red);
                break;
            }
            case "M02":
            {
                int wood = _data.GetItem("wood");
                int stone = _data.GetItem("stone");
                if (wood < 5)
                    SetObjective(_world.GroveCenter, "GROVE — 5 WOOD", "wood", Palette.Wood);
                else if (stone < 3)
                    SetObjective(_world.StonePatchCenter, "STONE PATCH", "stone", Palette.Stone);
                else
                    SetObjective(_world.WorkshopDoor, "WORKSHOP", "hammer", Palette.Orange);
                break;
            }
            case "M03":
                SetObjective(_world.BridgeCenter, "BRIDGE", "hammer", Palette.Wood);
                break;
            case "M04":
                if (BudgetRules.IsGroceryListComplete(_missions.GroceryBread, _missions.GroceryMilk))
                    SetObjective(_world.TownHallDoor, "TOWN HALL", "star", Palette.Blue);
                else
                    SetObjective(_world.ShopDoor, "SHOP BASKET", "basket", Palette.Blue);
                break;
            case "M05":
            case "M09":
            {
                int checkpoint = _data.GetMissionCounter(mission.Id, "checkpoint");
                if (!_driving)
                {
                    SetObjective(_van.GlobalPosition, checkpoint == 0 ? "YELLOW VAN" : "BACK TO VAN", "van", Palette.Yellow);
                }
                else if (checkpoint >= 4)
                {
                    SetObjective(_world.JobBoard, "JOBS BOARD", "star", Palette.Yellow);
                }
                else if (checkpoint == 0 && _van.GlobalPosition.X < 40f)
                {
                    SetObjective(_world.BridgeCenter, "BRIDGE TO MEADOW", "waypoint", Palette.Water);
                }
                else
                {
                    var gate = _world.RaceGates[checkpoint];
                    SetObjective(gate.Position, $"GATE {checkpoint + 1}", "flag",
                        checkpoint == 0 ? Palette.Yellow : Palette.Lavender);
                }
                break;
            }
            case "M06":
            {
                int blueDelivered = _data.GetMissionCounter("M06", "blueDelivered");
                int orangeDelivered = _data.GetMissionCounter("M06", "orangeDelivered");
                if (blueDelivered < 2)
                {
                    if (_data.GetItem("blue_parcel") > 0)
                        SetObjective(_world.RedHouseDoor, "RED HOUSE — BLUE", "parcel", Palette.Blue);
                    else
                        SetObjective(_world.PostCounter, "POST COUNTER", "parcel", Palette.Blue);
                }
                else if (orangeDelivered < 1)
                {
                    if (_data.GetItem("orange_parcel") > 0)
                        SetObjective(_world.WorkshopDoor, "WORKSHOP — ORANGE", "parcel", Palette.Orange);
                    else
                        SetObjective(_world.PostCounter, "POST COUNTER", "parcel", Palette.Orange);
                }
                else
                {
                    SetObjective(_world.JobBoard, "JOBS BOARD", "star", Palette.Yellow);
                }
                break;
            }
            case "M07":
                SetObjective(_world.PlaygroundCenter, "PLAYGROUND", "hammer", Palette.Lavender);
                break;
            case "M08":
                SetObjective(_world.VillageGreenSign, "VILLAGE GREEN", "flowerbed", Palette.Lavender);
                break;
            case "M10":
                SetObjective(IsInsidePlayerHouse(_player.GlobalPosition) ? _world.FurnitureStation : _world.PlayerHouseDoor,
                    IsInsidePlayerHouse(_player.GlobalPosition) ? "FURNITURE" : "YOUR HOUSE", "house", Palette.Lavender);
                break;
            case "M11":
            {
                var target = NextCardNpc();
                if (target != null)
                    SetObjective(target.GlobalPosition, $"{target.Definition.Name.ToUpperInvariant()} — CARD",
                        "neighbours", Palette.Orange);
                else
                    SetObjective(_world.JobBoard, "JOBS BOARD", "star", Palette.Yellow);
                break;
            }
            case "M12":
            {
                var ada = FindNpc("ada");
                SetObjective(ada?.GlobalPosition ?? _world.BakeryStall, "ADA'S BAKERY", "bakery", Palette.Orange);
                break;
            }
            case "M13":
            {
                Vector3? bookTarget = NextBookWorldTarget();
                if (bookTarget.HasValue)
                    SetObjective(bookTarget.Value, "LIBRARY BOOK", "library", Palette.Blue);
                else
                    SetObjective(FindNpc("ollie")?.GlobalPosition ?? _world.LibraryShelf,
                        "OLLIE'S SHELF", "library", Palette.Blue);
                break;
            }
            case "M14":
            {
                int socket = NextSignSocket();
                SetObjective(_world.SignSocketPositions[socket],
                    $"{ChapterTwoRules.SignSockets[socket].Label} SIGN", "road_signs", Palette.Orange);
                break;
            }
            case "M15":
            {
                int pad = NextGardenPad();
                SetObjective(_world.GardenPads[pad], $"GARDEN PAD {pad + 1}", "garden", Palette.GrassDark);
                break;
            }
            case "M16":
            {
                if (_data.GetMissionCounter("M16", "loaded") <= 0)
                {
                    SetObjective(_world.PostCounter, "POST COUNTER — LOAD", "parcel", Palette.Orange);
                    break;
                }
                var next = NextDeliveryNpc();
                if (next != null)
                    SetObjective(next.GlobalPosition, $"{next.Definition.Name.ToUpperInvariant()} — PARCEL",
                        "delivery", Palette.Orange);
                else
                    SetObjective(_world.JobBoard, "JOBS BOARD", "star", Palette.Yellow);
                break;
            }
            case "M17":
                SetObjective(_world.ShopDoor, "MARKET CHECKOUT", "market_maths", Palette.Blue);
                break;
            case "M18":
                SetObjective(_world.RepairCarPosition, "PIP'S CAR — REPAIR", "mechanic", Palette.Orange);
                break;
            case "M19":
            {
                int checkpoint = _data.GetMissionCounter("M19", "checkpoint");
                if (!_driving)
                    SetObjective(_van.GlobalPosition, checkpoint == 0 ? "YELLOW VAN" : "BACK TO VAN",
                        "van", Palette.Yellow);
                else if (checkpoint >= FinaleRules.RallyGateCount)
                    SetObjective(_world.JobBoard, "JOBS BOARD", "star", Palette.Yellow);
                else
                {
                    var gate = _world.M19RaceGates[checkpoint];
                    SetObjective(gate.Position, $"GRAND GATE {checkpoint + 1}", "flag",
                        checkpoint == 0 ? Palette.Yellow : Palette.Lavender);
                }
                break;
            }
            case "M20":
            {
                if (!FinaleRules.InvitesComplete(_data))
                {
                    var invite = NextFestivalInvitee();
                    if (invite != null)
                    {
                        SetObjective(invite.GlobalPosition,
                            $"INVITE {invite.Definition.Name.ToUpperInvariant()}", "festival", Palette.Yellow);
                        break;
                    }
                }
                if (!FinaleRules.TablesComplete(_data))
                {
                    int pad = NextFestivalTablePad();
                    SetObjective(_world.FestivalTablePads[pad], $"FESTIVAL TABLE {pad + 1}",
                        "festival", Palette.Lavender);
                }
                else if (!FinaleRules.LanternsComplete(_data))
                {
                    int hook = NextFestivalLanternPad();
                    SetObjective(_world.FestivalLanternPads[hook], $"FESTIVAL LANTERN {hook + 1}",
                        "festival", Palette.Yellow);
                }
                else
                {
                    SetObjective(_world.JobBoard, "JOBS BOARD", "star", Palette.Yellow);
                }
                break;
            }
            default:
                SetObjective(_world.JobBoard, "JOBS BOARD", "star", Palette.Yellow);
                break;
        }
    }

    private List<(string Icon, string Text)> ProgressRows(string missionId)
    {
        var rows = new List<(string, string)>();
        if (missionId == "M01")
        {
            int apples = _data.GetItem("apple");
            rows.Add(("apple", $"{apples} / 3 apples"));
            rows.Add(("house", apples >= 3 ? "Now visit RED HOUSE" : "Collect from the SHOP crate"));
        }
        else if (missionId == "M02")
        {
            int wood = _data.GetItem("wood");
            int stone = _data.GetItem("stone");
            rows.Add(("wood", $"{wood} / 5 wood"));
            rows.Add(("stone", $"{stone} / 3 stone"));
            rows.Add(("hammer", wood >= 5 && stone >= 3 ? "Now visit WORKSHOP" : $"Collected {wood + stone} of 8"));
        }
        else if (missionId == "M03")
        {
            int supports = BuildRules.BridgeSupportCount(_data.BridgePieces);
            int planks = BuildRules.BridgePlankCount(_data.BridgePieces);
            rows.Add(("stone", $"{supports} / 3 supports"));
            rows.Add(("wood", $"{planks} / 4 planks"));
            rows.Add(("hammer", supports < 3 ? "Supports first" : "Now place the planks"));
        }
        else if (missionId == "M04")
        {
            rows.Add(("bread", $"{_missions.GroceryBread} / 2 bread"));
            rows.Add(("milk", $"{_missions.GroceryMilk} / 1 milk"));
            rows.Add(("coin", $"Spent {_missions.GrocerySpent} of 12 · change {_missions.GroceryChange}"));
        }
        else if (missionId is "M05" or "M09")
        {
            int checkpoint = _data.GetMissionCounter(missionId, "checkpoint");
            rows.Add(("flag", $"Gate {checkpoint} / 4"));
            if (checkpoint > 0 && checkpoint < 4)
                rows.Add(("star", $"Time {FormatTime(_raceTimer.ElapsedSeconds)} · bonus under {FormatTime(missionId == "M09" ? 75 : 90)}"));
            else if (!_driving)
                rows.Add(("van", "Get into the yellow van"));
            else if (checkpoint == 0 && _van.GlobalPosition.X < 40f)
                rows.Add(("waypoint", "Cross the bridge first"));
            else
                rows.Add(("flag", "Gate 1 starts the timer"));
        }
        else if (missionId == "M06")
        {
            int blue = _data.GetMissionCounter("M06", "blueDelivered");
            int orange = _data.GetMissionCounter("M06", "orangeDelivered");
            rows.Add(("parcel", $"BLUE → RED HOUSE: {blue}/2"));
            rows.Add(("parcel", $"ORANGE → WORKSHOP: {orange}/1"));
            rows.Add(("van", $"Carrying {_data.GetItem("blue_parcel")} blue · {_data.GetItem("orange_parcel")} orange"));
        }
        else if (missionId == "M07")
        {
            int ropes = BuildRules.CountOf(_data.SwingPieces, new[] { "s1_rope_l", "s1_rope_r", "s2_rope_l", "s2_rope_r" });
            int seats = BuildRules.CountOf(_data.SwingPieces, new[] { "s1_seat", "s2_seat" });
            rows.Add(("rope", $"{ropes} / 4 ropes"));
            rows.Add(("seat", $"{seats} / 2 seats"));
            rows.Add(("star", "4 ropes + 2 seats = 6 pieces"));
        }
        else if (missionId == "M08")
        {
            int spent = ParkRules.Spent(_data);
            rows.Add(("bench", $"Bench: {ParkRules.Count(_data, "bench")}/1"));
            rows.Add(("flowerbed", $"Flowerbed: {ParkRules.Count(_data, "flowerbed")}/1"));
            rows.Add(("lamp", $"Lamp: {ParkRules.Count(_data, "lamp")}/1"));
            rows.Add(("coin", $"Job budget spent {spent}/20 · {ParkRules.Left(_data)} left"));
        }
        else if (missionId == "M10")
        {
            rows.Add(("table", $"Table: {FurnitureRules.Placed(_data, "table")}/1"));
            rows.Add(("chair", $"Chairs: {FurnitureRules.Placed(_data, "chair")}/2"));
            rows.Add(("plant", $"Plant: {FurnitureRules.Placed(_data, "plant")}/1"));
            rows.Add(("coin", $"House set costs 8+5+5+3 = 21"));
        }
        else if (missionId == "M11")
        {
            int delivered = ChapterTwoRules.CardNpcIds.Count(n =>
                _data.GetMissionCounter("M11", $"delivered_{n}") >= 1);
            int cards = ChapterTwoRules.CardNpcIds.Count(n =>
                _data.GetMissionCounter("M11", $"card_{n}") >= 1);
            rows.Add(("neighbours", $"{delivered} / 3 welcome cards delivered"));
            rows.Add(("star", $"{cards} cards in hand"));
            rows.Add(("star", "Read the name before giving a card"));
        }
        else if (missionId == "M12")
        {
            foreach (var order in ChapterTwoRules.BakeryOrders)
            {
                bool done = _data.GetMissionCounter("M12", $"order_{order.CustomerId}") >= 1;
                rows.Add(("bakery", $"{order.CustomerName}: {(done ? "served ✓" : $"{order.Bread} bread + {order.Cakes} cakes")}"));
            }
        }
        else if (missionId == "M13")
        {
            int found = ChapterTwoRules.BookIds.Count(b =>
                _data.GetMissionCounter("M13", ChapterTwoRules.HasBookKey(b)) >= 1 ||
                _data.GetMissionCounter("M13", ChapterTwoRules.ShelfKey(b)) >= 1);
            int shelved = ChapterTwoRules.BookIds.Count(b =>
                _data.GetMissionCounter("M13", ChapterTwoRules.ShelfKey(b)) >= 1);
            rows.Add(("library", $"Books found {found} / 4"));
            rows.Add(("library", $"Shelves matched {shelved} / 4"));
            rows.Add(("star", "Match SUN, MOON, TREE, STAR"));
        }
        else if (missionId == "M14")
        {
            foreach (var socket in ChapterTwoRules.SignSockets)
            {
                bool done = _data.GetMissionCounter("M14", $"sign_{socket.SocketId}") >= 1;
                rows.Add(("road_signs", $"{socket.Label}: {(done ? "repaired ✓" : "needs sign")}"));
            }
        }
        else if (missionId == "M15")
        {
            int correct = 0;
            for (int i = 0; i < ChapterTwoRules.GardenPattern.Length; i++)
                if (ChapterTwoRules.GardenPadMatches(i,
                        ChapterTwoRules.GardenColor(_data.GetMissionCounter("M15", $"pad{i}"))))
                    correct++;
            rows.Add(("garden", $"Pattern pads {correct} / 6"));
            rows.Add(("star", "RED, BLUE, RED, BLUE, RED, BLUE"));
            rows.Add(("star", "Wrong colours can be changed freely"));
        }
        else if (missionId == "M16")
        {
            int delivered = FinaleRules.DeliveryRecipients.Count(n =>
                _data.GetMissionCounter("M16", FinaleRules.DeliveredKey(n)) >= 1);
            rows.Add(("parcel", $"{delivered} / 3 parcels delivered"));
            rows.Add(("van", _data.GetMissionCounter("M16", "loaded") > 0
                ? (VanParkedNear(_player.GlobalPosition) ? "Van parked — step out to deliver" : "Drive close, stop, then step out")
                : "Load parcels at the POST COUNTER"));
        }
        else if (missionId == "M17")
        {
            rows.Add(("bread", $"{FinaleRules.MarketCount(_data, "bread")} / 2 bread"));
            rows.Add(("milk", $"{FinaleRules.MarketCount(_data, "milk")} / 1 milk"));
            rows.Add(("apple", $"{FinaleRules.MarketCount(_data, "apple")} / 2 apples"));
            rows.Add(("coin", $"Job total {FinaleRules.MarketSpent(_data)} / 20 · change {FinaleRules.MarketChange(_data)}"));
        }
        else if (missionId == "M18")
        {
            rows.Add(("mechanic", $"Tyres {_data.GetMissionCounter("M18", "tyres_fitted")} / 2"));
            rows.Add(("star", $"Lamp {(_data.GetMissionCounter("M18", "lamp_fitted") > 0 ? "fitted ✓" : "not fitted")}"));
            rows.Add(("star", $"Battery {(_data.GetMissionCounter("M18", "battery_fitted") > 0 ? "fitted ✓" : "not fitted")}"));
            rows.Add(("star", "Inspect, two tyres, lamp, battery last"));
        }
        else if (missionId == "M19")
        {
            int checkpoint = _data.GetMissionCounter("M19", "checkpoint");
            rows.Add(("flag", $"Grand gate {checkpoint} / 6"));
            if (checkpoint > 0 && checkpoint < FinaleRules.RallyGateCount)
                rows.Add(("star", $"Time {FormatTime(_raceTimer.ElapsedSeconds)} · medal under 2:35"));
            else if (!_driving)
                rows.Add(("van", "Get into the yellow van"));
            rows.Add(("star", "Pip is a friendly rival — finishing is enough"));
        }
        else if (missionId == "M20")
        {
            int invited = FinaleRules.FestivalInvitees.Count(n =>
                _data.GetMissionCounter("M20", FinaleRules.InvitedKey(n)) >= 1);
            int tables = Enumerable.Range(0, FinaleRules.FestivalTableCount)
                .Count(i => _data.GetMissionCounter("M20", FinaleRules.TableKey(i)) >= 1);
            int lanterns = Enumerable.Range(0, FinaleRules.FestivalLanternCount)
                .Count(i => _data.GetMissionCounter("M20", FinaleRules.LanternKey(i)) >= 1);
            rows.Add(("festival", $"Invitations {invited} / 3"));
            rows.Add(("star", $"Tables {tables} / 2"));
            rows.Add(("star", $"Lanterns {lanterns} / 3"));
            rows.Add(("star", "Invite, then tables, then lanterns"));
        }
        else
        {
            rows.Add(("star", "Follow the glowing marker"));
        }
        return rows;
    }

    private void RefreshRaceGateVisibility()
    {
        bool secondChapterRally = _missions?.ActiveMission?.Id == "M19";
        if (_world?.RaceGates4Root != null)
            _world.RaceGates4Root.Visible = !secondChapterRally;
        if (_world?.RaceGates6Root != null)
            _world.RaceGates6Root.Visible = secondChapterRally;
    }

    private void EnsureRallyRival()
    {
        bool wanted = _started && _missions?.ActiveMission?.Id == "M19" &&
                      (_data.GetMissionCounter("M19", "checkpoint") > 0 ||
                       _van.GlobalPosition.DistanceTo(_world.RaceStart) < 30f);
        if (wanted && _rallyRival == null)
        {
            var route = TrafficCatalog.Find("rally_rival");
            if (route == null) return;
            _rallyRival = new TrafficCar { Name = "RallyRival", ProcessMode = ProcessModeEnum.Pausable };
            _rallyRival.Initialize(route,
                () => _player?.GlobalPosition ?? Vector3.Zero,
                () => _van?.GlobalPosition ?? Vector3.Zero,
                () => _driving);
            AddChild(_rallyRival);
        }
        else if (!wanted && _rallyRival != null)
        {
            _rallyRival.QueueFree();
            _rallyRival = null;
        }
    }

    private NpcActor FindNpc(string id) =>
        _npcById.TryGetValue(id, out var npc) ? npc : null;

    private NpcActor NextCardNpc()
    {
        foreach (string npcId in ChapterTwoRules.CardNpcIds)
            if (_data.GetMissionCounter("M11", $"delivered_{npcId}") <= 0)
                return FindNpc(npcId);
        return null;
    }

    private Vector3? NextBookWorldTarget()
    {
        foreach (var pickup in _pickups)
        {
            if (!GodotObject.IsInstanceValid(pickup) || !pickup.Visible) continue;
            if (pickup.MissionId != "M13") continue;
            return pickup.GlobalPosition;
        }
        return null;
    }

    private int NextSignSocket()
    {
        for (int i = 0; i < ChapterTwoRules.SignSockets.Count; i++)
            if (_data.GetMissionCounter("M14", $"sign_{ChapterTwoRules.SignSockets[i].SocketId}") <= 0)
                return i;
        return 0;
    }

    private int NextGardenPad()
    {
        for (int i = 0; i < ChapterTwoRules.GardenPattern.Length; i++)
        {
            string color = ChapterTwoRules.GardenColor(_data.GetMissionCounter("M15", $"pad{i}"));
            if (!ChapterTwoRules.GardenPadMatches(i, color)) return i;
        }
        return 0;
    }

    private NpcActor NextDeliveryNpc()
    {
        foreach (string npcId in FinaleRules.DeliveryRecipients)
            if (_data.GetMissionCounter("M16", FinaleRules.DeliveredKey(npcId)) <= 0)
                return FindNpc(npcId);
        return null;
    }

    private NpcActor NextFestivalInvitee()
    {
        foreach (string npcId in FinaleRules.FestivalInvitees)
            if (_data.GetMissionCounter("M20", FinaleRules.InvitedKey(npcId)) <= 0)
                return FindNpc(npcId);
        return null;
    }

    private int NextFestivalTablePad()
    {
        for (int i = 0; i < FinaleRules.FestivalTableCount; i++)
            if (_data.GetMissionCounter("M20", FinaleRules.TableKey(i)) <= 0)
                return i;
        return 0;
    }

    private int NextFestivalLanternPad()
    {
        for (int i = 0; i < FinaleRules.FestivalLanternCount; i++)
            if (_data.GetMissionCounter("M20", FinaleRules.LanternKey(i)) <= 0)
                return i;
        return 0;
    }

    private void RefreshPickupVisibility()    {
        foreach (var pickup in _pickups.ToList())
        {
            if (!GodotObject.IsInstanceValid(pickup)) continue;
            bool completed = _data.IsCompleted(pickup.MissionId);
            pickup.Visible = !completed;
            if (completed) pickup.Visible = false;
            pickup.SetProcess(!completed);
        }
    }

    private void ApplyCosmetics()
    {
        if (_player == null) return;
        _player.SetAppearance(_data.CharacterPreset, _data.EquippedOutfit);
        _player.SetCapColor(_data.UnlockedCosmetics.Contains("red_cap") ? Palette.Red : Palette.Blue);
    }

    private void OnMissionCompleted(MissionCompletionResult result)
    {
        RefreshMissionState();
        string detail = result.Mission.Id switch
        {
            "M01" => "3 apples delivered to the RED HOUSE. A red cap is now yours!",
            "M02" => "5 wood + 3 stone delivered — all 8 supplies counted!",
            "M03" => "3 supports + 4 planks = a strong bridge. The meadow is open!",
            "M04" => "2 bread × 3 + 1 milk × 4 = 10. Change from 12 is 2. Delivered to TOWN HALL!",
            "M05" => result.BonusCoins > 0
                ? $"Four gates in {result.RaceSeconds:0.0}s — optional speed bonus +{result.BonusCoins}!"
                : $"Four gates finished in {result.RaceSeconds:0.0}s. Any pace is a winning pace!",
            "M06" => "2 BLUE parcels to RED HOUSE + 1 ORANGE parcel to WORKSHOP — every address correct!",
            "M07" => "4 ropes + 2 seats = 6 pieces. Both swings are fixed and ready to ride!",
            "M08" => $"Bench 6 + flowers 4 + lamp 9 = 19, leaving 1 of the 20 job coins. Beautiful green!",
            "M09" => result.BonusCoins > 0
                ? $"Four gates in {result.RaceSeconds:0.0}s — a gold-lap bonus +{result.BonusCoins}!"
                : $"Four gates finished in {result.RaceSeconds:0.0}s. Any pace is a winning pace — the gold flag is yours!",
            "M10" => "Table 8 + 2 chairs 10 + plant 3 = 21. Your home is ready to enjoy!",
            "M11" => "Three names, three cards, three happy neighbours. The new chapter is open!",
            "M12" => "2+1, 3+2 and 1+3 — every bakery order counted and served!",
            "M13" => "SUN, MOON, TREE and STAR all matched to their shelves. The library is tidy!",
            "M14" => "MARKET, WORKSHOP and GREEN signs repaired with the right arrows!",
            "M15" => "RED, BLUE, RED, BLUE, RED, BLUE — three perfect pairs in the garden!",
            "M16" => "ADA, PIP and IVY parcels delivered — every name matched!",
            "M17" => "2 bread + 1 milk + 2 apples = 14. Change from 20 is 6. Paid with job coins, not your wallet.",
            "M18" => "Two tyres, one lamp and the battery last — Pip's car is ready to drive!",
            "M19" => result.BonusCoins > 0
                ? $"Six grand gates in {result.RaceSeconds:0.0}s — an optional medal bonus +{result.BonusCoins}!"
                : $"Six grand gates finished in {result.RaceSeconds:0.0}s. Finishing is the win — Pip is a friendly rival!",
            "M20" => "Three invitations, two tables and three lanterns — the village festival is ready!",
            _ => result.Message,
        };
        Action afterSuccess = () => { };
        if (result.Mission.Id == "M20")
            afterSuccess = () => Callable.From(ShowVillageFestival).CallDeferred();
        else if (result.OriginalChapterComplete && _data.CelebrationSeen == false)
            afterSuccess = () => Callable.From(ShowVillageCelebration).CallDeferred();
        _hud.ShowSuccess(result.Mission.Title, result.TotalCoins, detail, afterSuccess);
        _audio?.PlaySfx("mission_complete");
        _audio?.PlaySfx("coin_collect", -7f);
        if (result.Mission.Id == "M10" && MissionRules.OriginalChapterComplete(_data))
            _audio?.PlaySfx("chapter_unlock");
        RefreshMusicContext();
        if (result.OriginalChapterComplete)
            _world.CelebrationDecor.Visible = true;
        var save = SaveGame(SaveReason.MissionCompleted);
        if (result.Mission.Id is "M05" or "M09" or "M19")
            GD.Print($"RACE_TIME {result.Mission.Id} {result.RaceSeconds:0.00}s bonus={result.BonusCoins}");
        if (!save.Success)
            _hud.SetModalStatus(
                "Your job is complete in this session, but the save failed. Use Save now from the pause menu to retry.",
                true);
    }

    private bool AllMissionsComplete() =>
        _catalog != null && MissionRules.OriginalChapterComplete(_data);

    private bool AllTwentyMissionsComplete() =>
        _catalog != null && _data != null && _data.IsCompleted("M20");

    private void ShowVillageCelebration()
    {
        if (_celebrationShown || _data.CelebrationSeen || !AllMissionsComplete()) return;
        _celebrationShown = true;
        _data.CelebrationSeen = true;
        _world.CelebrationDecor.Visible = true;

        var body = _hud.OpenModal("VILLAGE FIXED!", new Vector2(760, 500), Palette.Yellow);
        var titleRow = UiKit.HBox(body, 12);
        UiKit.Icon(titleRow, "star", 52);
        var title = UiKit.Label(titleRow, "All ten jobs complete!", 30, Palette.Success, HorizontalAlignment.Left, false);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UiKit.Label(body,
            "Patchwork Island is sparkling! The bridge is open, the swings work, the green is decorated and your home is cosy.",
            22, Palette.Navy);
        UiKit.Label(body,
            "Free roam is yours. Drive the meadow course, visit the playground, or rearrange your house. " +
            "The job board keeps a practice race and a record of every finished job.",
            18, new Color("5C6B7A"));

        var actions = UiKit.HBox(body, 10);
        var roam = UiKit.Button(actions, "Play freely", 21, new Vector2(0, 54), new Color("DFF6E3"), Palette.Success);
        roam.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        roam.Pressed += () => _hud.CloseModal();
        var practice = UiKit.Button(actions, "Practice race", 21, new Vector2(0, 54), new Color("FFF3C9"), Palette.Orange);
        practice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        practice.Pressed += () =>
        {
            _hud.CloseModal();
            StartPracticeRace();
        };
        var jobs = UiKit.Button(actions, "See the job board", 21, new Vector2(0, 54), Palette.Cream, Palette.Blue);
        jobs.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        jobs.Pressed += () =>
        {
            _hud.CloseModal();
            Callable.From(OpenJobBoard).CallDeferred();
        };

        var save = SaveGame(SaveReason.Celebration);
        if (!save.Success)
            _hud.SetModalStatus("The celebration is shown, but it could not be saved yet. Use Save now.", true);
    }

    private void ShowVillageFestival()
    {
        if (_festivalShown || _data.FestivalSeen || !AllTwentyMissionsComplete()) return;
        _festivalShown = true;
        _data.FestivalSeen = true;
        if (_world.FestivalDecor != null) _world.FestivalDecor.Visible = true;
        _audio?.PlaySfx("festival_pop");
        RefreshMusicContext();

        var body = _hud.OpenModal("VILLAGE FESTIVAL!", new Vector2(780, 520), Palette.Yellow);
        var titleRow = UiKit.HBox(body, 12);
        UiKit.Icon(titleRow, "festival", 56);
        var title = UiKit.Label(titleRow, "All twenty jobs complete!", 30, Palette.Success,
            HorizontalAlignment.Left, false);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UiKit.Label(body,
            "The village festival is glowing! Three neighbours are invited, two tables are set and " +
            "three lanterns light the green. Pip's car is ready and the grand rally trophy is home.",
            22, Palette.Navy);
        UiKit.Label(body,
            "This is the second chapter finale. The original ten-job celebration remains a separate memory. " +
            "Free roam, the job board and every finished job stay available.",
            18, new Color("5C6B7A"));

        var actions = UiKit.HBox(body, 10);
        var enjoy = UiKit.Button(actions, "Enjoy the festival", 20, new Vector2(0, 54),
            new Color("DFF6E3"), Palette.Success);
        enjoy.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        enjoy.Pressed += () => _hud.CloseModal();
        var jobs = UiKit.Button(actions, "See the job board", 20, new Vector2(0, 54),
            Palette.Cream, Palette.Blue);
        jobs.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        jobs.Pressed += () =>
        {
            _hud.CloseModal();
            Callable.From(OpenJobBoard).CallDeferred();
        };

        var save = SaveGame(SaveReason.Celebration);
        if (!save.Success)
            _hud.SetModalStatus("The festival is shown, but it could not be saved yet. Use Save now.", true);
    }

    private void SetObjective(Vector3 position, string label, string icon, Color color)
    {
        _destination = position;
        _destLabel = label;
        _destIcon = icon;
        _hasDestination = true;
        if (_buildMode)
            _marker.HideMarker();
        else
            _marker.ShowAt(position, color, icon);
        RefreshObjectiveUi();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo &&
            keyEvent.Keycode == Key.F12 && DeveloperMode)
        {
            CaptureManualScreenshotSafely();
            GetViewport().SetInputAsHandled();
            return;
        }

        var activeBuild = GetActiveBuildMode();
        if (activeBuild != null)
        {
            if (@event.IsActionPressed("pause"))
            {
                activeBuild.CloseBuildMode();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event.IsActionPressed("rotate_part"))
            {
                activeBuild.RotateSelected();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                activeBuild.HandleWorldClick(GetViewport().GetMousePosition());
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (@event.IsActionPressed("pause"))
        {
            if (!_started) return;
            if (_hud.ModalOpen)
            {
                if (_hud.ModalDismissible)
                    _hud.CloseModal();
            }
            else
            {
                _hud.ShowPauseMenu(ResumeGame, SaveNow, OpenSettingsFromPause, ConfirmNewGame, ReturnToTitle, QuitGame);
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_started || _hud.ModalOpen) return;
        if (_driving)
        {
            if (@event.IsActionPressed("interact"))
            {
                ExitVan();
                GetViewport().SetInputAsHandled();
            }
            else if (@event.IsActionPressed("reset_vehicle"))
            {
                ResetVanWithToast();
                GetViewport().SetInputAsHandled();
            }
            else if (@event.IsActionPressed("jobs"))
            {
                OpenJobBoard();
                GetViewport().SetInputAsHandled();
            }
            else if (@event.IsActionPressed("hint"))
            {
                ShowCurrentHint();
                GetViewport().SetInputAsHandled();
            }
            return;
        }
        if (@event.IsActionPressed("jobs"))
        {
            OpenJobBoard();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("hint"))
        {
            ShowCurrentHint();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ResumeGame() => _hud.CloseModal();

    private void ShowPauseMenu() =>
        _hud.ShowPauseMenu(ResumeGame, SaveNow, OpenSettingsFromPause, ConfirmNewGame, ReturnToTitle, QuitGame);

    private void SaveNow()
    {
        var result = SaveGame(SaveReason.Manual);
        if (!_hud.ModalOpen) return;
        if (result.Success)
        {
            _hud.SetModalTitle("Paused — Saved");
            _hud.SetModalStatus("Game saved. Your village will be waiting.", false);
        }
        else
        {
            _hud.SetModalTitle("Save failed");
            _hud.SetModalStatus(
                "Nothing was lost on disk; your previous save is unchanged. Please try again.", true);
        }
    }

    private void OpenSettingsFromPause() => OpenSettings(ShowPauseMenu);

    private void OpenSettings(Action onClosed)
    {
        var body = _hud.OpenModal("Settings", new Vector2(620, 500), Palette.Lavender, true, onClosed);
        UiKit.Label(body, "Sound and comfort", 24, Palette.Navy);
        AddSettingsToggle(body, "Music", _data.Settings.MusicEnabled, value =>
        {
            _data.Settings.MusicEnabled = value;
            ApplySettings();
            SaveSettings();
        });
        AddSettingsToggle(body, "Sound effects", _data.Settings.SfxEnabled, value =>
        {
            _data.Settings.SfxEnabled = value;
            ApplySettings();
            SaveSettings();
        });
        AddSettingsToggle(body, "Reduced motion", _data.Settings.ReducedMotion, value =>
        {
            _data.Settings.ReducedMotion = value;
            ApplySettings();
            SaveSettings();
        });
        UiKit.Label(body,
            "The game always gives visual feedback, so it is fully playable with sound off. " +
            "Large text and 720p-friendly layout are always on.",
            18, new Color("5C6B7A"));
        UiKit.Label(body, "Settings are saved with your village.", 17, new Color("5C6B7A"));
    }

    private SaveResult SaveSettings()
    {
        var result = SaveGame(SaveReason.Settings);
        if (!_hud.ModalOpen) return result;
        if (result.Success)
        {
            _hud.SetModalStatus("Setting applied and saved.", false);
        }
        else
        {
            _hud.SetModalStatus(
                "Setting applied for this session, but it could not be saved: " + result.Message,
                true);
        }
        return result;
    }

    private static void AddSettingsToggle(VBoxContainer body, string label, bool initial, Action<bool> changed)
    {
        var row = UiKit.HBox(body, 10);
        var text = UiKit.Label(row, label, 21, Palette.Navy);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var toggle = UiKit.Button(row, initial ? "On" : "Off", 19, new Vector2(120, 44),
            initial ? new Color("DFF6E3") : Palette.Cream,
            initial ? Palette.Success : Palette.Stone);
        toggle.Pressed += () =>
        {
            bool next = toggle.Text != "On";
            toggle.Text = next ? "On" : "Off";
            changed(next);
        };
    }

    private void ConfirmNewGame()
    {
        _hud.Confirm("Start a new game?", "This restarts the village from the beginning. Are you sure?", "Start new game",
            () => ShowCharacterChoice(preset =>
            {
                var result = ResetSession(preset);
                if (result.Success)
                    _hud.ShowBigToast("New game started. Find the JOBS board!");
                else
                    _hud.ShowBigToast(
                        "New game started, but it could not be saved yet. Use Save now to try again.",
                        7f, Palette.Red);
            }, ShowPauseMenu),
            "Cancel", ShowPauseMenu);
    }

    private SaveResult ResetSession(string preset = null)
    {
        foreach (var mode in _buildModes)
        {
            if (mode == null) continue;
            if (mode.Active) mode.CloseBuildMode();
        }
        foreach (var mode in _buildModes)
        {
            if (mode is Node node && GodotObject.IsInstanceValid(node))
            {
                RemoveChild(node);
                node.QueueFree();
            }
        }
        _buildModes.Clear();
        _bridgeBuilder = null;
        _swingBuilder = null;
        _parkBuilder = null;
        _furnitureBuilder = null;

        if (_driving)
        {
            _driving = false;
            _van.SetDriven(false);
            _player.Visible = true;
        }
        _van.ResetToParking();
        _raceTimer = new RaceTimer();
        _raceCheckTimer = 0f;
        _raceHudTimer = 0f;
        _raceWrongHintCooldown = 0f;
        _vehicleSwitchCooldown = 0f;
        _practiceRaceActive = false;
        _practiceCheckpoint = 0;
        _practiceElapsed = 0f;
        _celebrationShown = false;
        _festivalShown = false;
        if (_rallyRival != null)
        {
            _rallyRival.QueueFree();
            _rallyRival = null;
        }
        _lastIndoor = false;
        _rig.SetIndoor(false);
        _data = new GameData();
        if (CharacterCustomisation.IsValidPreset(preset))
        {
            _data.CharacterPreset = preset;
            _data.EquippedOutfit = CharacterCustomisation.DefaultOutfit;
        }
        _missions = new MissionService(_data, _catalog);
        _saveNotice = "";
        _recoveredJobNote = "";

        if (_world.BridgeBarrier != null) _world.BridgeBarrier.Visible = true;
        if (_world.CelebrationDecor != null) _world.CelebrationDecor.Visible = false;
        if (_world.GoldCourseFlag != null) _world.GoldCourseFlag.Visible = false;

        CreateBuilderSystems();
        foreach (var pickup in _pickups)
        {
            if (!GodotObject.IsInstanceValid(pickup)) continue;
            RemoveChild(pickup);
            pickup.QueueFree();
        }
        _pickups.Clear();
        SpawnPickups();
        _player.Teleport(_world.PlayerSpawn, -Mathf.Pi / 2f);
        _rig.SetView(-72f, 18f, 8.2f);
        _rig.SetTarget(_player, false, 8.2f, 18f, 1.55f, true);
        ApplySettings();
        ApplyCosmetics();
        RefreshMissionState();
        // Confirming New Game is explicit permission to replace an unsupported
        // newer save. The permission survives a failed initial write so Save Now
        // can retry after the I/O problem is resolved.
        _session.AuthorizeNewerReplacement();
        var result = SaveGame(SaveReason.NewGame);
        _session.RecordNewGameSave(result,
            "A new game is running, but it could not be saved. The previous save on disk is still available " +
            "from Continue; use Save now to retry the new game.");
        SyncControlMode();
        return result;
    }

    private void ReturnToTitle()
    {
        var result = SaveGame(SaveReason.ReturnToTitle);
        if (!result.Success)
        {
            _hud.Confirm("Return to title without saving?",
                "Progress could not be saved. You can keep playing and try Save now again, or return to the title anyway.",
                "Return anyway",
                DiscardUnsavedSessionAndShowTitle,
                "Keep playing", ShowPauseMenu);
            return;
        }
        _practiceRaceActive = false;
        ShowTitle();
    }

    private void DiscardUnsavedSessionAndShowTitle()
    {
        _practiceRaceActive = false;
        _session.DiscardUnsavedSession();
        if (_session.NewerSavePresent)
        {
            _session.SetTitleNotice(
                "Your most recent progress could not be saved. The newer save on disk remains protected.");
        }
        else if (_session.CanContinue)
        {
            _session.SetTitleNotice(
                "Your most recent progress could not be saved. Continue loads the last successfully saved village.");
        }
        else
        {
            _session.SetTitleNotice(
                "Your most recent progress could not be saved. There is no earlier save to continue.");
        }
        ShowTitle();
    }

    private void QuitGame()
    {
        if (!_started)
        {
            GetTree().Quit();
            return;
        }
        var result = SaveGame(SaveReason.Quit);
        if (!result.Success)
        {
            _hud.Confirm("Quit without saving?",
                "Progress could not be saved. You can keep playing and try Save now again, or quit and lose this session's unsaved work.",
                "Quit anyway", () => GetTree().Quit(),
                "Keep playing", ShowPauseMenu);
            return;
        }
        GetTree().Quit();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest && _started)
        {
            var result = SaveGame(SaveReason.WindowClose);
            if (!result.Success)
                GD.PrintErr("Window is closing and the final save failed: " + result.Message);
        }
    }

    public override void _Process(double delta)
    {
        if (!_started) return;
        if (GetTree().Paused) return;

        _raceWrongHintCooldown = Mathf.Max(0f, _raceWrongHintCooldown - (float)delta);
        _vehicleSwitchCooldown = Mathf.Max(0f, _vehicleSwitchCooldown - (float)delta);
        TickRace((float)delta);
        TickPracticeRace((float)delta);

        _autosaveTimer -= (float)delta;
        if (_autosaveTimer <= 0f)
        {
            _autosaveTimer = 20f;
            if (!_hud.ModalOpen &&
                (!_session.NewerSavePresent || _session.NewerReplacementAuthorized))
                SaveGame(SaveReason.Autosave);
        }

        _uiRefresh -= (float)delta;
        if (_uiRefresh <= 0f)
        {
            _uiRefresh = 0.12f;
            RefreshObjectiveUi();
            if (_driving)
                _hud.SetPrompt(Mathf.Abs(_van.Speed) > 2.5f ? "Slow down to exit   ·   R reset" : "Exit van   ·   R reset");
            else if (_player.NearestInteractable != null)
                _hud.SetPrompt(_player.NearestInteractable.GetPrompt());
            else
                _hud.SetPrompt(null);
        }

        if (_driving) return;

        bool indoor = IsInsidePlayerHouse(_player.GlobalPosition);
        if (indoor != _lastIndoor)
        {
            _lastIndoor = indoor;
            _rig.SetIndoor(indoor);
        }

        Vector3 p = _player.GlobalPosition;
        bool onFinishedBridge = _bridgeBuilder != null && _bridgeBuilder.IsBuilt &&
                                p.X > 39.0f && p.X < 51.0f && Mathf.Abs(p.Z) < 4.2f && p.Y > -0.4f;
        bool inRiver = p.X > 40.7f && p.X < 49.3f && p.Y < 0.2f && !onFinishedBridge;
        if (p.Y < -5f || inRiver)
            RecoverPlayer("Splash! Back to dry land.");
    }

    private bool IsInsidePlayerHouse(Vector3 p)
    {
        return p.X > -44.6f && p.X < -31.4f && p.Z > 18.8f && p.Z < 29.2f && p.Y < 6f;
    }

    private void RecoverPlayer(string message)
    {
        Vector3 p = _player.GlobalPosition;
        Vector3 safe;
        if (p.X < 45f)
            safe = new Vector3(35.5f, 0.5f, Mathf.Clamp(p.Z, -46f, 46f));
        else
            safe = new Vector3(54.5f, 0.5f, Mathf.Clamp(p.Z, -46f, 46f));
        _player.Teleport(safe);
        _hud.ShowToast(message, 2.6f, Palette.Water);
    }

    private void RefreshObjectiveUi()
    {
        if (!_hasDestination)
        {
            _hud.SetDestination(null, 0, 0, null);
            return;
        }
        Vector3 p = _driving ? _van.GlobalPosition : _player.GlobalPosition;
        Vector3 to = new Vector3(_destination.X - p.X, 0, _destination.Z - p.Z);
        float distance = to.Length();
        Vector3 f = -_rig.GlobalTransform.Basis.Z;
        f.Y = 0;
        f = f.Normalized();
        Vector3 right = _rig.GlobalTransform.Basis.X;
        right.Y = 0;
        right = right.Normalized();
        float angle = Mathf.Atan2(to.Normalized().Dot(right), to.Normalized().Dot(f));
        int arrow = Mathf.PosMod(Mathf.RoundToInt(angle / (Mathf.Pi / 4f)), 8);
        _hud.SetDestination(_destLabel, distance, arrow, _destIcon);
    }

    // ------------------------------------------------- runtime self-test hooks
    // These are internal, non-shipping test seams used by RuntimeSelfTest. They
    // call the same gameplay transitions a player would trigger; they do not
    // bypass mission rules or validators.

    internal bool RtStarted => _started;
    internal bool RtPaused => GetTree().Paused;
    internal string RtMode => _controlMode.ToString();
    internal bool RtModalOpen => _hud.ModalOpen;
    internal string RtModalTitle => _hud.ModalTitleText;
    internal string RtModalStatus => _hud.ModalStatusText;
    internal bool RtModalStatusVisible => _hud.ModalStatusVisible;
    internal string RtToastText => _hud.ToastText;
    internal bool RtToastVisible => _hud.ToastVisible;
    internal bool RtCanContinue => _session.CanContinue;
    internal bool RtHasSaveFile => _session.HasSaveFile;
    internal bool RtMemoryMatchesPersisted => _session.MemoryMatchesPersisted;
    internal bool RtContinueReloadPending => _continueAfterReload;
    internal bool RtNewerSavePresent => _session.NewerSavePresent;
    internal bool RtNewerReplacementAuthorized => _session.NewerReplacementAuthorized;
    internal string RtTitleNotice => _session.TitleNotice;
    internal string RtSavePath => SavePath;
    internal MissionCatalog RtCatalog => _catalog;
    internal bool RtPlayerControlled => _player.ControlEnabled;
    internal bool RtVanControlled => _van.InputEnabled;
    internal bool RtPlayerVisible => _player.Visible;
    internal Vector3 RtPlayerPosition => _player.GlobalPosition;
    internal Vector3 RtVanPosition => _van.GlobalPosition;
    internal ProcessModeEnum RtPlayerProcessMode => _player.ProcessMode;
    internal ProcessModeEnum RtVanProcessMode => _van.ProcessMode;
    internal ProcessModeEnum RtWorldProcessMode => _world.ProcessMode;
    internal double RtRaceElapsed => _raceTimer.ElapsedSeconds;
    internal bool RtBridgeBuilt => _bridgeBuilder?.IsBuilt == true;
    internal int RtParkPlacementCount => _data.ParkPlacements.Count;
    internal int RtSwingPieceCount => _data.SwingPieces.Count;
    internal string RtCharacterPreset => _data.CharacterPreset;
    internal string RtEquippedOutfit => _data.EquippedOutfit;
    internal int RtOwnedOutfitCount => _data.OwnedOutfits.Count;
    internal bool RtOwnsOutfit(string outfitId) => CharacterCustomisation.Owns(_data, outfitId);
    internal string RtPlayerPreset => _player.AppearancePreset;
    internal string RtPlayerOutfit => _player.AppearanceOutfit;
    internal bool RtPlayerHasLongHair => _player.HasLongHair;
    internal bool RtPlayerWearsDress => _player.WearsDress;
    internal int RtPlayerAppearanceRevision => _player.AppearanceRevision;

    internal void RtStart() => StartAdventure();
    internal void RtContinue() => ContinueAdventure();
    internal void RtPause() => ShowPauseMenu();
    internal void RtResume() => ResumeGame();
    internal void RtOpenSettingsFromPause() => OpenSettingsFromPause();
    internal void RtOpenSettingsFromTitle() => OpenSettingsFromTitle();
    internal void RtConfirmNewGame() => ConfirmNewGame();
    internal void RtRequestNewGameFromTitle() => StartNewGameFromTitle();
    internal SaveResult RtStartNewGameFromTitleConfirmed() =>
        BeginNewGameFromTitle(CharacterCustomisation.DefaultPreset);
    internal SaveResult RtStartNewGameFromTitleConfirmed(string preset) => BeginNewGameFromTitle(preset);
    internal SaveResult RtStartNewGame(string preset) => ResetSession(preset);
    internal void RtOpenClothesShop() => InteractClothesShop();
    internal bool RtPressModalButton(string name)
    {
        if (!_hud.ModalOpen) return false;
        var button = _hud.FindChild(name, true, false) as Button;
        if (button == null || button.Disabled || !button.Visible) return false;
        button.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    internal bool RtFocusModalButton(string name)
    {
        if (!_hud.ModalOpen) return false;
        var button = _hud.FindChild(name, true, false) as Button;
        if (button == null || button.Disabled || !button.Visible) return false;
        button.GrabFocus();
        return true;
    }

    internal string RtModalLabelText(string name)
    {
        if (!_hud.ModalOpen) return "";
        return (_hud.FindChild(name, true, false) as Label)?.Text ?? "";
    }

    internal ActionOutcome RtShopBuy(string outfitId)
    {
        var outcome = CharacterCustomisation.BuyOutfit(_data, outfitId);
        if (outcome.Success) PersistCustomisation();
        return outcome;
    }
    internal ActionOutcome RtShopWear(string outfitId)
    {
        var outcome = CharacterCustomisation.EquipOutfit(_data, outfitId);
        if (outcome.Success) PersistCustomisation();
        return outcome;
    }
    internal void RtReturnToTitle() => ReturnToTitle();
    internal void RtReturnAnyway() => DiscardUnsavedSessionAndShowTitle();
    internal Error RtReloadScene() => GetTree().ReloadCurrentScene();
    internal void RtCloseModal() => _hud.CloseModal();
    internal bool RtSaveNow() => SaveGame(SaveReason.Manual).Success;
    internal bool RtForceAutosave() => SaveGame(SaveReason.Autosave).Success;
    internal SaveResult RtStartNewGame() => ResetSession();
    internal int RtCoins => _data.Coins;
    internal string RtCoinText => _hud.CoinText;
    internal int RtCompletedCount => _data.CompletedMissions.Count;
    internal string RtActiveMission => _data.ActiveMission;
    internal bool RtMusicEnabled => _data.Settings.MusicEnabled;

    internal void RtToggleMusic()
    {
        _data.Settings.MusicEnabled = !_data.Settings.MusicEnabled;
        ApplySettings();
        SaveSettings();
    }

    internal void RtToggleSfx()
    {
        _data.Settings.SfxEnabled = !_data.Settings.SfxEnabled;
        ApplySettings();
        SaveSettings();
    }

    internal string RtMusicId => _audio?.CurrentMusicId ?? "";
    internal bool RtMusicPlaying => _audio?.IsMusicPlaying == true;
    internal bool RtHasMusicStream => _audio?.HasMusicStream == true;
    internal double RtMusicPosition => _audio?.MusicPlaybackPosition ?? 0.0;
    internal bool RtMusicBusMuted => AudioManager.IsBusMuted(AudioCueCatalog.MusicBus);
    internal bool RtSfxBusMuted => AudioManager.IsBusMuted(AudioCueCatalog.SfxBus);
    internal bool RtUiBusMuted => AudioManager.IsBusMuted(AudioCueCatalog.UiBus);
    internal bool RtVehiclesBusMuted => AudioManager.IsBusMuted(AudioCueCatalog.VehiclesBus);
    internal bool RtAudioBusesPresent => AudioManager.HasRequiredBuses();
    internal bool RtVanHasEngineAudio => _van?.HasEngineAudio == true;
    internal bool RtVanEngineIdlePlaying => _van?.EngineIdlePlaying == true;
    internal bool RtVanEngineDrivePlaying => _van?.EngineDrivePlaying == true;
    internal string RtLastSfx => _audio?.LastSfxId ?? "";
    internal int RtSfxPlayCount(string id) => _audio?.PlayCount(id) ?? 0;

    internal int RtNpcCount => _npcs.Count;
    internal string RtNpcIds => string.Join(",", _npcs.Select(n => n.NpcId));
    internal string RtNpcNames => string.Join(", ", _npcs.Select(n => n.Definition.Name));
    internal bool RtNpcPresent(string id) => _npcById.ContainsKey(id);
    internal Vector3 RtNpcPosition(string id) =>
        _npcById.TryGetValue(id, out var npc) ? npc.GlobalPosition : new Vector3(9999, 9999, 9999);
    internal string RtNpcNameLabel(string id) =>
        _npcById.TryGetValue(id, out var npc) ? npc.NameLabelText : "";
    internal bool RtNpcTalking(string id) => _npcById.TryGetValue(id, out var npc) && npc.Talking;
    internal bool RtNpcMoving(string id) => _npcById.TryGetValue(id, out var npc) && npc.IsMoving;
    internal ProcessModeEnum RtNpcProcessMode(string id) =>
        _npcById.TryGetValue(id, out var npc) ? npc.ProcessMode : ProcessModeEnum.Inherit;

    internal void RtTeleportPlayerNearNpc(string id)
    {
        if (!_npcById.TryGetValue(id, out var npc)) return;
        Vector3 p = npc.GlobalPosition + new Vector3(1.7f, 0.1f, 0f);
        _player.Teleport(p, Mathf.Pi / 2f);
        _player.ForceRefreshInteraction();
    }

    internal bool RtTalkToNpc(string id)
    {
        if (!_npcById.TryGetValue(id, out var npc)) return false;
        TalkToNpc(npc);
        return _hud.ModalOpen;
    }

    internal string RtNearestInteractableName => _player?.NearestInteractable?.Name.ToString() ?? "";

    internal bool RtInteractNearest()
    {
        var target = _player?.NearestInteractable;
        if (target == null || !target.IsAvailable()) return false;
        target.Interact();
        return true;
    }

    internal void RtTeleportNearPickup(string pickupId)
    {
        var pickup = _pickups.FirstOrDefault(p => p.PickupId == pickupId);
        if (pickup == null) return;
        _player.Teleport(pickup.GlobalPosition + new Vector3(1.4f, 0.15f, 0f), -Mathf.Pi / 2f);
        _player.ForceRefreshInteraction();
    }

    internal int RtBookPickupCount => _pickups.Count(p => p.MissionId == "M13");

    internal void RtTeleportNearSignSocket(int index)
    {
        if (_world == null || index < 0 || index >= _world.SignSocketPositions.Count) return;
        _player.Teleport(_world.SignSocketPositions[index] + new Vector3(0f, 0.15f, 1.45f), 0f);
        _player.ForceRefreshInteraction();
    }

    internal void RtTeleportNearGardenPad(int index)
    {
        if (_world == null || index < 0 || index >= _world.GardenPads.Count) return;
        _player.Teleport(_world.GardenPads[index] + new Vector3(0f, 0.15f, 0.8f), 0f);
        _player.ForceRefreshInteraction();
    }

    internal void RtTeleportNearJobBoard()
    {
        if (_jobStation == null || _player == null) return;
        _player.Teleport(_jobStation.GlobalPosition + new Vector3(0f, 0.1f, 1.5f), 0f);
        _player.ForceRefreshInteraction();
    }

    internal int RtTrafficCount => _trafficCars.Count;
    internal string RtTrafficIds => string.Join(",", _trafficCars.Select(n => n.RouteId));
    internal Vector3 RtTrafficPosition(string id) =>
        _trafficCars.FirstOrDefault(c => c.RouteId == id)?.GlobalPosition ?? new Vector3(9999, 9999, 9999);
    internal bool RtTrafficOnRoad(string id)
    {
        var car = _trafficCars.FirstOrDefault(c => c.RouteId == id);
        return car != null && RoadNetwork.Contains(car.GlobalPosition.X, car.GlobalPosition.Z);
    }
    internal bool RtTrafficYielding(string id) =>
        _trafficCars.FirstOrDefault(c => c.RouteId == id)?.Yielding == true;
    internal float RtTrafficSpeed(string id) =>
        _trafficCars.FirstOrDefault(c => c.RouteId == id)?.PlanarSpeed ?? -1f;
    internal float RtTrafficDistance(string id) =>
        _trafficCars.FirstOrDefault(c => c.RouteId == id)?.DistanceTravelled ?? -1f;
    internal int RtTrafficWaypoint(string id) =>
        _trafficCars.FirstOrDefault(c => c.RouteId == id)?.CurrentWaypoint ?? -1;
    internal bool RtTrafficHasEngineAudio(string id) =>
        _trafficCars.FirstOrDefault(c => c.RouteId == id)?.HasEngineAudio == true;

    internal void RtTeleportPlayerAheadOfTraffic(string id, float distance)
    {
        var car = _trafficCars.FirstOrDefault(c => c.RouteId == id);
        if (car == null) return;
        Vector3 forward = -car.GlobalTransform.Basis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        _player.Teleport(car.GlobalPosition + forward * distance + Vector3.Up * 0.1f,
            car.Rotation.Y + Mathf.Pi);
        _player.ForceRefreshInteraction();
    }

    internal void RtResetPlayerToSpawn()
    {
        _player.Teleport(_world.PlayerSpawn, -Mathf.Pi / 2f);
        _player.ForceRefreshInteraction();
    }
    internal bool RtSfxEnabled => _data.Settings.SfxEnabled;

    internal void RtStopAudio()
    {
        _van?.StopEngineAudio();
        _audio?.StopAll();
    }

    internal void RtSetMusicEnabled(bool enabled)
    {
        _data.Settings.MusicEnabled = enabled;
        ApplySettings();
    }

    internal void RtSetSfxEnabled(bool enabled)
    {
        _data.Settings.SfxEnabled = enabled;
        ApplySettings();
    }

    internal void RtSetPlayerAirborne()
    {
        _player.Velocity = Vector3.Zero;
        _player.Teleport(_player.GlobalPosition + Vector3.Up * 3f);
    }

    internal void RtCompleteM01()
    {
        if (!_missions.StartMission("M01").Success) return;
        for (int i = 0; i < 3; i++)
            _missions.CollectPickup($"apple_{i}", "apple", "M01", 3);
        var result = _missions.DeliverApples();
        if (result.Success) OnMissionCompleted(result);
    }

    /// <summary>
    /// Builds the same valid world state a v1.1 player has after finishing the
    /// original ten jobs. Runtime verification uses this only to reach chapter
    /// two; the chapter-two interactions afterwards all go through real buttons,
    /// stations, pickups and mission rules.
    /// </summary>
    internal void RtPrepareChapterTwo()
    {
        foreach (string id in MissionRules.OriginalMissionIds)
            if (!_data.CompletedMissions.Contains(id)) _data.CompletedMissions.Add(id);
        foreach (string socket in BuildRules.BridgeSockets)
            if (!_data.BridgePieces.Contains(socket)) _data.BridgePieces.Add(socket);
        foreach (string socket in BuildRules.SwingSockets)
            if (!_data.SwingPieces.Contains(socket)) _data.SwingPieces.Add(socket);
        if (ParkRules.Count(_data, "bench") == 0) ParkRules.Place(_data, "bench", "park0");
        if (ParkRules.Count(_data, "flowerbed") == 0) ParkRules.Place(_data, "flowerbed", "park1");
        if (ParkRules.Count(_data, "lamp") == 0) ParkRules.Place(_data, "lamp", "park2");
        FurnitureRules.GrantFreeStarterSet(_data);
        _data.CelebrationSeen = true;
        _data.Coins = 100;
        _data.ActiveMission = "";
        _world.CelebrationDecor.Visible = true;
        RefreshMissionState();
        RefreshChapterTwoWorld();
        _hud.SetCoins(_data.Coins);
    }

    /// <summary>
    /// Builds the valid world state for a player who has finished M11-M15, so
    /// the P14 runtime phase can exercise M16-M20 through their real stations
    /// without replaying the first chapter-two half.
    /// </summary>
    internal void RtPrepareFinale()
    {
        RtPrepareChapterTwo();
        foreach (string id in new[] { "M11", "M12", "M13", "M14", "M15" })
            if (!_data.CompletedMissions.Contains(id)) _data.CompletedMissions.Add(id);

        _data.SetMissionCounter("M11", "stock_granted", 1);
        foreach (string npc in ChapterTwoRules.CardNpcIds)
        {
            _data.SetMissionCounter("M11", $"card_{npc}", 0);
            _data.SetMissionCounter("M11", $"delivered_{npc}", 1);
        }
        foreach (var order in ChapterTwoRules.BakeryOrders)
            _data.SetMissionCounter("M12", $"order_{order.CustomerId}", 1);
        foreach (string book in ChapterTwoRules.BookIds)
        {
            string pickup = ChapterTwoRules.BookPickupId(book);
            if (!_data.CollectedPickups.Contains(pickup)) _data.CollectedPickups.Add(pickup);
            _data.SetMissionCounter("M13", ChapterTwoRules.HasBookKey(book), 0);
            _data.SetMissionCounter("M13", ChapterTwoRules.ShelfKey(book), 1);
        }
        foreach (var socket in ChapterTwoRules.SignSockets)
            _data.SetMissionCounter("M14", $"sign_{socket.SocketId}", 1);
        for (int i = 0; i < ChapterTwoRules.GardenPattern.Length; i++)
            _data.SetMissionCounter("M15", $"pad{i}",
                ChapterTwoRules.GardenCode(ChapterTwoRules.GardenPattern[i]));

        _data.ActiveMission = "";
        RefreshMissionState();
    }

    internal void RtTeleportVanNearNpc(string id)
    {
        if (!_npcById.TryGetValue(id, out var npc)) return;
        _van.GlobalPosition = npc.GlobalPosition + new Vector3(6.5f, 0.15f, 0f);
        _van.Rotation = Vector3.Zero;
        _van.Velocity = Vector3.Zero;
        _player.Teleport(npc.GlobalPosition + new Vector3(1.7f, 0.1f, 0f), Mathf.Pi / 2f);
        _player.ForceRefreshInteraction();
    }

    internal void RtBuildBridgeNow()
    {
        foreach (string id in BuildRules.BridgeSockets)
            if (!_data.BridgePieces.Contains(id)) _data.BridgePieces.Add(id);
        _bridgeBuilder?.RefreshFromData();
    }

    internal void RtPrepareBridgeCrossing()
    {
        RtLeaveVanForTest();
        RtBuildBridgeNow();
        _van.GlobalPosition = new Vector3(47.0f, 0.15f, 0f);
        _van.Rotation = new Vector3(0f, -Mathf.Pi / 2f, 0f);
        _van.Velocity = Vector3.Zero;
        _player.Teleport(new Vector3(44.7f, 0.15f, 0f), -Mathf.Pi / 2f);
        _vehicleSwitchCooldown = 0f;
        EnterVan();
        RefreshMissionState();
    }

    internal void RtPlacePlayerOutsideGrandGate(int index)
    {
        if (_world == null || index < 0 || index >= _world.M19RaceGates.Length) return;
        PlacePlayerOutsideGate(_world.M19RaceGates[index].Position,
            RallyCourse.SixGateFractions[index], 6.2f);
    }

    internal Vector3 RtRaceGatePosition(int index) =>
        _world != null && index >= 0 && index < _world.RaceGates.Length
            ? _world.RaceGates[index].Position
            : new Vector3(9999, 9999, 9999);

    internal Vector3 RtGrandGatePosition(int index) =>
        _world != null && index >= 0 && index < _world.M19RaceGates.Length
            ? _world.M19RaceGates[index].Position
            : new Vector3(9999, 9999, 9999);

    internal void RtPlacePlayerOutsideRaceGate(int index)
    {
        if (_world == null || index < 0 || index >= _world.RaceGates.Length) return;
        PlacePlayerOutsideGate(_world.RaceGates[index].Position,
            RallyCourse.FourGateFractions[index], 6.2f);
    }

    internal void RtPlacePlayerAtRaceGateLip(int index)
    {
        if (_world == null || index < 0 || index >= _world.RaceGates.Length) return;
        PlacePlayerOutsideGate(_world.RaceGates[index].Position,
            RallyCourse.FourGateFractions[index], 5.55f);
    }

    private void PlacePlayerOutsideGate(Vector3 gate, float fraction, float distance)
    {
        var tangent = RallyCourse.Tangent(fraction);
        var left = new Vector3(-tangent.Z, 0f, tangent.X);
        Vector3 start = gate + left * distance + new Vector3(0f, 0.15f, 0f);
        float yaw = Mathf.Atan2(-(gate.X - start.X), -(gate.Z - start.Z));
        _player.Teleport(start, yaw);
        _player.ForceRefreshInteraction();
    }

    internal void RtTeleportNearPostCounter()
    {
        if (_postStation == null) return;
        _player.Teleport(_postStation.GlobalPosition + new Vector3(0f, 0.1f, -1.2f), 0f);
        _player.ForceRefreshInteraction();
    }

    internal void RtTeleportNearShop()
    {
        if (_shopStation == null) return;
        _player.Teleport(_shopStation.GlobalPosition + new Vector3(0f, 0.1f, 1.2f), 0f);
        _player.ForceRefreshInteraction();
    }

    internal void RtTeleportNearRepairCar()
    {
        if (_repairStation == null) return;
        _player.Teleport(_repairStation.GlobalPosition + new Vector3(0f, 0.1f, 1.2f), 0f);
        _player.ForceRefreshInteraction();
    }

    internal void RtTeleportNearFestivalTable(int index)
    {
        if (_world == null || index < 0 || index >= _world.FestivalTablePads.Count) return;
        _player.Teleport(_world.FestivalTablePads[index] + new Vector3(0f, 0.15f, 1.2f), 0f);
        _player.ForceRefreshInteraction();
    }

    internal void RtTeleportNearFestivalLantern(int index)
    {
        if (_world == null || index < 0 || index >= _world.FestivalLanternPads.Count) return;
        _player.Teleport(_world.FestivalLanternPads[index] + new Vector3(0f, 0.15f, 1.2f), 0f);
        _player.ForceRefreshInteraction();
    }

    internal bool RtPrepareRace()
    {
        // Build a valid prerequisite state, including the physical bridge so
        // the runtime test does not create a save the validator must reject.
        if (!_data.CompletedMissions.Contains("M02")) _data.CompletedMissions.Add("M02");
        if (!_data.CompletedMissions.Contains("M03"))
        {
            _data.CompletedMissions.Add("M03");
            foreach (string socket in BuildRules.BridgeSockets)
                if (!_data.BridgePieces.Contains(socket)) _data.BridgePieces.Add(socket);
        }
        if (!_missions.StartMission("M05").Success) return false;
        _data.SetMissionCounter("M05", "checkpoint", 1);
        _data.SetMissionCounter("M05", "elapsedMs", 12_500);
        _raceTimer = RaceTimer.Restore(_data, "M05");
        return true;
    }

    internal void RtPlaceVanAtRallyStart()
    {
        if (_driving)
        {
            _driving = false;
            _van.SetDriven(false);
            _player.Visible = true;
        }
        _van.StopAutopilot();
        var start = RallyCourse.Sample(0f);
        _van.GlobalPosition = new Vector3(start.X, 0.2f, start.Z);
        _van.Rotation = new Vector3(0, 0, 0);
        _van.Velocity = Vector3.Zero;
        _player.Teleport(_van.GlobalPosition + new Vector3(-2.6f, 0.1f, 0f), 0f);
        _vehicleSwitchCooldown = 0f;
        EnterVan();
    }

    internal bool RtPrepareRally(string missionId)
    {
        if (missionId is not ("M05" or "M09" or "M19")) return false;
        foreach (string id in new[] { "M01", "M02", "M03", "M04" })
            if (!_data.CompletedMissions.Contains(id)) _data.CompletedMissions.Add(id);
        foreach (string socket in BuildRules.BridgeSockets)
            if (!_data.BridgePieces.Contains(socket)) _data.BridgePieces.Add(socket);
        if (missionId == "M09" && !_data.CompletedMissions.Contains("M05"))
            _data.CompletedMissions.Add("M05");
        if (!_missions.StartMission(missionId).Success) return false;
        _raceTimer = new RaceTimer();
        RtPlaceVanAtRallyStart();
        if (!_driving) return false;
        _van.StartRallyAutopilot();
        RefreshMissionState();
        return true;
    }

    internal void RtStopRallyAutopilot() => _van?.StopAutopilot();
    internal bool RtMissionCompleted(string id) => _data.IsCompleted(id);
    internal bool RtAllTwentyComplete => _data.IsCompleted("M20");
    internal MissionStatus RtMissionStatus(string id) => _missions.GetStatus(id);
    internal int RtMissionCounter(string id, string key) => _data.GetMissionCounter(id, key);
    internal bool RtCelebrationSeen => _data.CelebrationSeen;
    internal bool RtFestivalSeen => _data.FestivalSeen;
    internal int RtSignSocketCount => _world?.SignSocketPositions.Count ?? 0;
    internal int RtGardenPadCount => _world?.GardenPads.Count ?? 0;
    internal int RtBakerySlotCount => _world?.BakeryDisplaySlots.Count ?? 0;
    internal int RtLibrarySlotCount => _world?.LibraryShelfSlots.Count ?? 0;
    internal bool RtBakerySlotVisible(int index) =>
        _world != null && index >= 0 && index < _world.BakeryDisplaySlots.Count &&
        _world.BakeryDisplaySlots[index].Visible;
    internal bool RtLibrarySlotVisible(int index) =>
        _world != null && index >= 0 && index < _world.LibraryShelfSlots.Count &&
        _world.LibraryShelfSlots[index].Visible;
    internal bool RtSignVisualVisible(int index) =>
        index >= 0 && index < _signVisuals.Count && _signVisuals[index].Visible;
    internal bool RtGardenFlowerVisible(int index) =>
        index >= 0 && index < _gardenFlowerRoots.Count && _gardenFlowerRoots[index].Visible;
    internal bool RtDeliveryBoardVisible => _world?.DeliveryBoard?.Visible == true;
    internal bool RtMarketStockVisible => _world?.MarketStock?.Visible == true;
    internal bool RtPipCarFixedVisible => _world?.PipCarFixed?.Visible == true;
    internal bool RtRallyPodiumVisible => _world?.RallyPodium?.Visible == true;
    internal bool RtFestivalDecorVisible => _world?.FestivalDecor?.Visible == true;
    internal bool RtRallyRivalPresent => _rallyRival != null && GodotObject.IsInstanceValid(_rallyRival);
    internal Vector3 RtRallyRivalPosition =>
        _rallyRival != null && GodotObject.IsInstanceValid(_rallyRival)
            ? _rallyRival.GlobalPosition
            : new Vector3(9999, 9999, 9999);
    internal bool RtRallyAutopilot => _van?.Autopilot == true;
    internal int RtRaceCheckpoint(string id) => _data.GetMissionCounter(id, "checkpoint");
    internal int RtRallySampleCount => _world?.RallySampleCount ?? 0;
    internal float RtRallyLength => _world?.RallyLength ?? 0f;

    internal int RtRallyCollisionCoverage()
    {
        if (_world == null) return 0;
        var samples = RallyCourse.SampleClosed(4);
        var space = GetWorld3D().DirectSpaceState;
        int hits = 0;
        foreach (var sample in samples)
        {
            var query = PhysicsRayQueryParameters3D.Create(
                new Vector3(sample.X, 2.5f, sample.Z),
                new Vector3(sample.X, -1f, sample.Z), 1u);
            var result = space.IntersectRay(query);
            if (result.Count == 0) continue;
            Vector3 hit = (Vector3)result["position"];
            if (hit.Y >= 0.03f) hits++;
        }
        return hits;
    }

    internal void RtEnterVan()
    {
        _vehicleSwitchCooldown = 0f;
        _player.Teleport(_van.GlobalPosition + new Vector3(-2.6f, 0.1f, 0f), _van.Rotation.Y);
        EnterVan();
    }

    internal void RtExitVan() => ExitVan();

    internal void RtLeaveVanForTest()
    {
        if (!_driving) return;
        _van.SetDriven(false);
        _driving = false;
        _player.Visible = true;
        _player.Teleport(_van.GlobalPosition + new Vector3(-2.6f, 0.15f, 0f), _van.Rotation.Y);
        _player.SetControlEnabled(true);
        _rig.SetTarget(_player, false, 8.2f, 18f, 1.55f, true);
        _vehicleSwitchCooldown = 0f;
        SyncControlMode();
        RefreshMissionState();
    }

    // ------------------------------------------------------------- dev capture

    private static bool HasUserArg(string needle)
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg == needle) return true;
        return false;
    }

    private static string ScreenshotDirectory =>
        ProjectSettings.GlobalizePath(IsSelfTest ? "user://selftest-captures" : "user://screenshots");

    private static string DeveloperCaptureDirectory =>
        ProjectSettings.GlobalizePath("user://dev-captures");

    private async Task CaptureTitle()
    {
        try
        {
            await Wait(1.2f);
            await Snap("p2_00_title.png", DeveloperCaptureDirectory);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Title capture failed: {ex}");
        }
        GetTree().Quit();
    }

    private async Task RunPerformanceProbe()
    {
        try
        {
            bool highRes = HasUserArg("--perf-1080");
            if (highRes)
            {
                GetWindow().ContentScaleSize = new Vector2I(1920, 1080);
                GetWindow().Size = new Vector2I(1920, 1080);
            }
            await Wait(1.5f);
            var frameMs = new List<double>(600);
            ulong previous = Time.GetTicksUsec();
            for (int i = 0; i < 600; i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                ulong now = Time.GetTicksUsec();
                frameMs.Add((now - previous) / 1000.0);
                previous = now;
            }
            frameMs.Sort();
            double mean = frameMs.Average();
            double p95 = frameMs[(int)(frameMs.Count * 0.95)];
            double max = frameMs[^1];
            GD.Print($"PERF resolution={(highRes ? "1920x1080" : "1280x720")} " +
                     $"mean_ms={mean:0.00} p95_ms={p95:0.00} max_ms={max:0.00} " +
                     $"frames={frameMs.Count} nodes={GetTree().GetNodeCount()} " +
                     $"draw_calls={(int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} " +
                     $"primitives={(int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Performance probe failed: {ex}");
        }
        GetTree().Quit();
    }

    private async Task CaptureP12()
    {
        try
        {
            await Wait(1.0f);
            bool highRes = HasUserArg("--capture-1080");
            if (highRes)
            {
                GetWindow().ContentScaleSize = new Vector2I(1920, 1080);
                GetWindow().Size = new Vector2I(1920, 1080);
                await Wait(0.6f);
            }
            _hud.Visible = false;
            string p = highRes ? "p12_1080" : "p12_720";

            await SnapFreeCamera(new Vector3(25, 60, 78), new Vector3(70, 0, 0), $"{p}_00_village_overview.png");
            await SnapFreeCamera(new Vector3(45, 48, 12), new Vector3(72, 0, 0), $"{p}_01_rally_overview.png");
            await SnapFreeCamera(new Vector3(48, 7, 4), new Vector3(57, 0, -8), $"{p}_02_start_line.png");
            await SnapFreeCamera(new Vector3(48, 7, -12), new Vector3(56.6f, 1, -26.6f), $"{p}_03_gate1.png");
            await SnapFreeCamera(new Vector3(55, 12, 20), new Vector3(64.6f, 1, 43.3f), $"{p}_04_gate4.png");
            await SnapFreeCamera(new Vector3(78, 6, 12), new Vector3(86, 0, 0), $"{p}_05_road_join.png");
            await SnapFreeCamera(new Vector3(18, 3, 0.5f), new Vector3(24, 1, 5), $"{p}_06_clothes_shop.png");
            await SnapFreeCamera(new Vector3(10, 16, 34), new Vector3(-8, 0, 0), $"{p}_07_village_roads.png");
            await SnapFreeCamera(new Vector3(22, 3, -0.5f), new Vector3(-18, 1, 2), $"{p}_08_town_traffic.png");
            _hud.Visible = true;
            JobBoardUi.Show(_hud, _missions, _implemented, _ => { });
            await Wait(0.5f);
            await Snap($"{p}_09_job_board.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"P12 capture failed: {ex}");
        }
        GetTree().Quit();
    }

    private async Task CaptureP13()
    {
        try
        {
            await Wait(1.0f);
            bool highRes = HasUserArg("--capture-1080");
            if (highRes)
            {
                GetWindow().ContentScaleSize = new Vector2I(1920, 1080);
                GetWindow().Size = new Vector2I(1920, 1080);
                await Wait(0.6f);
            }
            string p = highRes ? "p13_1080" : "p13_720";

            RtPrepareChapterTwo();
            _data.ActiveMission = "M11";
            _data.SetMissionCounter("M11", "stock_granted", 1);
            foreach (string npc in ChapterTwoRules.CardNpcIds)
                _data.SetMissionCounter("M11", $"card_{npc}", 1);
            RefreshMissionState();

            _hud.Visible = false;
            await SnapFreeCamera(new Vector3(30, 70, 30), new Vector3(10, 0, 8), $"{p}_00_village_chapter_two.png");
            await SnapFreeCamera(new Vector3(15.2f, 12f, 19f), new Vector3(15.2f, 1.5f, 5.2f), $"{p}_01_bakery_stall.png");
            await SnapFreeCamera(new Vector3(-14.5f, 5f, 2.5f), new Vector3(-20.8f, 1.5f, 8.2f), $"{p}_02_library_shelf.png");
            await SnapFreeCamera(new Vector3(22, 30, 25), new Vector3(15, 0, 2), $"{p}_03_road_sign_sockets.png");
            await SnapFreeCamera(new Vector3(29, 12, 33), new Vector3(29, 0.5f, 21), $"{p}_04_community_garden.png");
            await SnapFreeCamera(new Vector3(-1.5f, 3.5f, 10.5f), new Vector3(-4.2f, 0.7f, 7.0f), $"{p}_05_book_pickup.png");

            _hud.Visible = true;
            JobBoardUi.Show(_hud, _missions, _implemented, _ => { }, null, true);
            await Wait(0.5f);
            await Snap($"{p}_06_job_board_chapter_two.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.25f);

            RtTalkToNpc("ada");
            await Wait(0.5f);
            await Snap($"{p}_07_ada_welcome_cards.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.25f);

            _data.ActiveMission = "M12";
            BakeryUi.Show(_hud, _missions, _ => { }, () => { });
            await Wait(0.5f);
            await Snap($"{p}_08_bakery_ui.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.25f);

            _data.ActiveMission = "M13";
            foreach (string book in ChapterTwoRules.BookIds)
                _data.SetMissionCounter("M13", ChapterTwoRules.HasBookKey(book), 1);
            LibraryShelfUi.Show(_hud, _missions, _ => { }, () => { });
            await Wait(0.5f);
            await Snap($"{p}_09_library_ui.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.25f);

            _data.ActiveMission = "M14";
            SignSocketUi.Show(_hud, _missions, "market", _ => { }, () => { });
            await Wait(0.5f);
            await Snap($"{p}_10_sign_ui.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.25f);

            _data.ActiveMission = "M15";
            _data.SetMissionCounter("M15", "pad0", ChapterTwoRules.GardenCode("red"));
            _data.SetMissionCounter("M15", "pad1", ChapterTwoRules.GardenCode("blue"));
            GardenPadUi.Show(_hud, _missions, 0, _ => { }, () => { });
            await Wait(0.5f);
            await Snap($"{p}_11_garden_ui.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.25f);

            foreach (var socket in ChapterTwoRules.SignSockets)
                _data.SetMissionCounter("M14", $"sign_{socket.SocketId}", 1);
            for (int i = 0; i < ChapterTwoRules.GardenPattern.Length; i++)
                _data.SetMissionCounter("M15", $"pad{i}",
                    ChapterTwoRules.GardenCode(ChapterTwoRules.GardenPattern[i]));
            for (int i = 0; i < ChapterTwoRules.BakeryOrders.Count; i++)
                _data.SetMissionCounter("M12", $"order_{ChapterTwoRules.BakeryOrders[i].CustomerId}", 1);
            foreach (string book in ChapterTwoRules.BookIds)
                _data.SetMissionCounter("M13", ChapterTwoRules.ShelfKey(book), 1);
            RefreshChapterTwoWorld();
            _hud.Visible = false;
            await SnapFreeCamera(new Vector3(22, 12, 10), new Vector3(20, 0, -4), $"{p}_12_repaired_signs.png");
            await SnapFreeCamera(new Vector3(29, 12, 33), new Vector3(29, 0.5f, 21), $"{p}_13_garden_complete.png");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"P13 capture failed: {ex}");
        }
        GetTree().Quit();
    }

    private async Task CaptureP14()
    {
        try
        {
            await Wait(1.0f);
            bool highRes = HasUserArg("--capture-1080");
            if (highRes)
            {
                GetWindow().ContentScaleSize = new Vector2I(1920, 1080);
                GetWindow().Size = new Vector2I(1920, 1080);
                await Wait(0.6f);
            }
            string p = highRes ? "p14_1080" : "p14_720";
            RtPrepareFinale();

            // M16 dialogue and post counter.
            _data.ActiveMission = "M16";
            _data.SetMissionCounter("M16", "loaded", 1);
            foreach (string npc in FinaleRules.DeliveryRecipients)
                _data.SetMissionCounter("M16", FinaleRules.CarryingKey(npc), 1);
            RefreshMissionState();
            _hud.Visible = true;
            RtTalkToNpc("ada");
            await Wait(0.5f);
            await Snap($"{p}_00_delivery_dialogue.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.25f);

            _hud.Visible = false;
            await SnapFreeCamera(new Vector3(-14.5f, 14f, 2f), new Vector3(-14.5f, 1f, 6.5f),
                $"{p}_01_post_counter.png");
            _data.CompletedMissions.Add("M16");
            foreach (string npc in FinaleRules.DeliveryRecipients)
            {
                _data.SetMissionCounter("M16", FinaleRules.CarryingKey(npc), 0);
                _data.SetMissionCounter("M16", FinaleRules.DeliveredKey(npc), 1);
            }
            RefreshMissionState();
            await SnapFreeCamera(new Vector3(-2.5f, 4.5f, -1f), new Vector3(-2.5f, 1f, 4.7f),
                $"{p}_02_delivery_board.png");

            // M17 checkout and market stock.
            _data.ActiveMission = "M17";
            _data.SetMissionCounter("M17", "bread", 2);
            _data.SetMissionCounter("M17", "milk", 1);
            _data.SetMissionCounter("M17", "apple", 2);
            _hud.Visible = true;
            MarketMathsUi.Show(_hud, _missions, _ => { }, () => { });
            await Wait(0.5f);
            await Snap($"{p}_03_market_maths_ui.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.25f);
            _data.CompletedMissions.Add("M17");
            _data.SetMissionCounter("M17", "finalised", 1);
            _data.ActiveMission = "";
            RefreshMissionState();
            _hud.Visible = false;
            await SnapFreeCamera(new Vector3(3.5f, 4f, 1.5f), new Vector3(-2f, 1f, 4.8f),
                $"{p}_04_market_stock.png");

            // M18 broken car, repair bench, repaired car.
            _data.ActiveMission = "M18";
            _data.SetMissionCounter("M18", "inspected", 1);
            _data.SetMissionCounter("M18", "has_tyre", 2);
            _data.SetMissionCounter("M18", "has_lamp", 1);
            _data.SetMissionCounter("M18", "has_battery", 1);
            _data.SetMissionCounter("M18", "tyres_fitted", 1);
            RefreshMissionState();
            _hud.Visible = true;
            RepairUi.Show(_hud, _missions, _ => { }, () => { });
            await Wait(0.5f);
            await Snap($"{p}_05_repair_ui.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.25f);
            _hud.Visible = false;
            await SnapFreeCamera(new Vector3(32f, 7f, -0.5f), new Vector3(35.2f, 1f, -7.8f),
                $"{p}_06_broken_pip_car.png");
            _data.CompletedMissions.Add("M18");
            _data.SetMissionCounter("M18", "tyres_fitted", 2);
            _data.SetMissionCounter("M18", "lamp_fitted", 1);
            _data.SetMissionCounter("M18", "battery_fitted", 1);
            _data.ActiveMission = "";
            RefreshMissionState();
            await SnapFreeCamera(new Vector3(32f, 7f, -0.5f), new Vector3(35.2f, 1f, -7.8f),
                $"{p}_07_repaired_pip_car.png");

            // M19 six-gate circuit with Pip's rival car.
            _data.ActiveMission = "M19";
            RefreshMissionState();
            _van.GlobalPosition = new Vector3(56f, 0.15f, -6f);
            _van.Rotation = Vector3.Zero;
            RefreshMissionState();
            await Wait(0.4f);
            _hud.Visible = false;
            await SnapFreeCamera(new Vector3(74f, 42f, 4f), new Vector3(70f, 0f, 0f),
                $"{p}_08_grand_rally_six_gates.png");
            _data.CompletedMissions.Add("M19");
            _data.SetMissionCounter("M19", "checkpoint", FinaleRules.RallyGateCount);
            _data.SetMissionCounter("M19", "started", 1);
            _data.SetMissionCounter("M19", "elapsedMs", 22_300);
            _data.ActiveMission = "";
            RefreshMissionState();

            // M20 festival pads and second finale.
            foreach (string id in new[] { "M16", "M17", "M18" })
                if (!_data.CompletedMissions.Contains(id)) _data.CompletedMissions.Add(id);
            _data.ActiveMission = "M20";
            foreach (string npc in FinaleRules.FestivalInvitees)
                _data.SetMissionCounter("M20", FinaleRules.InvitedKey(npc), 1);
            _data.SetMissionCounter("M20", "table_0", 1);
            _data.SetMissionCounter("M20", "table_1", 1);
            _data.SetMissionCounter("M20", "lantern_0", 1);
            _data.SetMissionCounter("M20", "lantern_1", 1);
            _data.SetMissionCounter("M20", "lantern_2", 1);
            RefreshMissionState();
            _hud.Visible = false;
            await SnapFreeCamera(new Vector3(15f, 9f, 31f), new Vector3(15f, 1f, 22f),
                $"{p}_09_festival_setup.png");

            _data.CompletedMissions.Add("M20");
            _data.ActiveMission = "";
            RefreshMissionState();
            _hud.Visible = false;
            await SnapFreeCamera(new Vector3(15f, 9f, 31f), new Vector3(15f, 1f, 22f),
                $"{p}_10_festival_decor.png");

            _hud.Visible = true;
            JobBoardUi.Show(_hud, _missions, _implemented, _ => { }, null, true);
            await Wait(0.5f);
            await Snap($"{p}_11_job_board_all_complete.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.25f);
            _data.FestivalSeen = false;
            ShowVillageFestival();
            await Wait(0.5f);
            await Snap($"{p}_12_village_festival.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.25f);

            RtBuildBridgeNow();
            _hud.Visible = false;
            await SnapFreeCamera(new Vector3(35f, 8f, 12f), new Vector3(45f, 1f, 0f),
                $"{p}_13_bridge_approach.png");
            await SnapFreeCamera(new Vector3(63f, 6f, -21f), new Vector3(56.8f, 2f, -27.6f),
                $"{p}_14_gate_one.png");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"P14 capture failed: {ex}");
        }
        GetTree().Quit();
    }

    private async Task SnapFreeCamera(Vector3 position, Vector3 lookAt, string fileName)
    {
        var camera = new Camera3D { Fov = 62f, Current = true };
        AddChild(camera);
        camera.GlobalPosition = position;
        camera.LookAt(lookAt, Vector3.Up);
        await Wait(0.35f);
        await Snap(fileName, DeveloperCaptureDirectory);
        camera.QueueFree();
    }

    private async Task CaptureP11()
    {
        try
        {
            await Wait(1.2f);
            await CaptureNpcPose("ada", "p11_01_ada_market.png");
            await CaptureNpcPose("milo", "p11_02_milo_post.png");
            await CaptureNpcPose("pip", "p11_03_pip_workshop.png");
            await CaptureNpcPose("fern", "p11_04_fern_garden.png");
            await CaptureNpcPose("ollie", "p11_05_ollie_town_hall.png");
            await CaptureNpcPose("ivy", "p11_06_ivy_green.png");

            _player.Teleport(new Vector3(-15f, 0.1f, 7.5f), Mathf.Pi);
            _rig.SetView(180f, 20f, 9.5f);
            await Wait(1.6f);
            await Snap("p11_07_town_traffic.png", DeveloperCaptureDirectory);

            RtTeleportPlayerNearNpc("ada");
            await Wait(0.4f);
            RtTalkToNpc("ada");
            await Wait(0.6f);
            await Snap("p11_08_ada_dialogue.png", DeveloperCaptureDirectory);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"P11 capture failed: {ex}");
        }
        GetTree().Quit();
    }

    private async Task CaptureNpcPose(string npcId, string fileName)
    {
        if (!_npcById.TryGetValue(npcId, out var npc)) return;
        Vector3 npcPosition = npc.GlobalPosition;
        float offsetZ = npcId is "pip" or "fern" or "ivy" ? 5.0f : -3.8f;
        Vector3 playerPosition = new(npcPosition.X + 0.4f, 0.1f, npcPosition.Z + offsetZ);
        float yaw = Mathf.Atan2(-(npcPosition.X - playerPosition.X), -(npcPosition.Z - playerPosition.Z));
        _player.Teleport(playerPosition, yaw);
        _rig.SetView(Mathf.RadToDeg(yaw), 16f, 8.0f);
        await Wait(0.8f);
        await Snap(fileName, DeveloperCaptureDirectory);
    }

    private async Task CaptureTour()
    {
        try
        {
            await Wait(1.3f);
            await Snap("p2_01_start.png", DeveloperCaptureDirectory);

            var overview = new Camera3D { Fov = 55, Current = true };
            AddChild(overview);
            overview.GlobalPosition = new Vector3(-8f, 52f, 74f);
            overview.LookAt(new Vector3(8f, 0f, -2f), Vector3.Up);
            await Wait(0.7f);
            await Snap("p2_02_village_overview.png", DeveloperCaptureDirectory);
            overview.QueueFree();
            _rig.Current = true;

            JobBoardUi.Show(_hud, _missions, _implemented, _ => { });
            await Wait(0.6f);
            await Snap("p2_03_job_board.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.4f);

            // Put the player near the market with M01 active for evidence.
            _missions.StartMission("M01");
            RefreshMissionState();
            _player.Teleport(new Vector3(-4.4f, 0.2f, 1.1f), Mathf.Pi);
            _rig.SetView(180f, 18f, 8.2f);
            await Wait(0.8f);
            await Snap("p2_04_apple_crate.png", DeveloperCaptureDirectory);

            // Simulate a full M01 hand-in for screenshot evidence.
            _missions.CollectPickup("apple_0", "apple", "M01", 3);
            _missions.CollectPickup("apple_1", "apple", "M01", 3);
            _missions.CollectPickup("apple_2", "apple", "M01", 3);
            RefreshMissionState();
            _player.Teleport(new Vector3(-20f, 0.2f, -14.0f), 0f);
            _rig.SetView(0f, 18f, 8.2f);
            await Wait(0.7f);
            await Snap("p2_05_red_house_delivery.png", DeveloperCaptureDirectory);

            var completion = _missions.DeliverApples();
            if (completion.Success)
            {
                OnMissionCompleted(completion);
                await Wait(0.6f);
                await Snap("p2_06_job_complete.png", DeveloperCaptureDirectory);
                _hud.CloseModal();
                await Wait(0.4f);
            }

            _missions.StartMission("M02");
            RefreshMissionState();
            _player.Teleport(new Vector3(-40f, 0.2f, -24.0f), 0f);
            _rig.SetView(0f, 20f, 9f);
            await Wait(0.7f);
            await Snap("p2_07_grove.png", DeveloperCaptureDirectory);

            _player.Teleport(new Vector3(14f, 0.2f, -27.0f), 0f);
            _rig.SetView(0f, 20f, 9f);
            await Wait(0.7f);
            await Snap("p2_08_stone_patch.png", DeveloperCaptureDirectory);

            _player.Teleport(new Vector3(25f, 0.2f, -6.0f), 0f);
            _rig.SetView(0f, 18f, 8.2f);
            await Wait(0.7f);
            await Snap("p2_09_workshop.png", DeveloperCaptureDirectory);

            // P9 character and clothes-shop evidence.
            CharacterChoiceUi.Show(_hud, _ => { }, () => { });
            await Wait(0.7f);
            await Snap("p9_01_character_choice.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.4f);

            _data.CharacterPreset = CharacterCustomisation.Girl;
            _data.EquippedOutfit = CharacterCustomisation.DefaultOutfit;
            ApplyCosmetics();
            _player.Teleport(new Vector3(24f, 0.2f, 0.6f), Mathf.Pi);
            _rig.SetView(180f, 18f, 8.2f);
            await Wait(0.8f);
            await Snap("p9_02_girl_at_clothes_shop.png", DeveloperCaptureDirectory);

            ClothesShopUi.Show(_hud, _data, () =>
            {
                ApplyCosmetics();
                _hud.SetCoins(_data.Coins);
                return SaveResult.Ok();
            });
            await Wait(0.7f);
            await Snap("p9_03_clothes_shop_ui.png", DeveloperCaptureDirectory);
            RtPressModalButton("Outfit_sunny");
            await Wait(0.7f);
            await Snap("p9_04_outfit_owned_and_worn.png", DeveloperCaptureDirectory);
            _hud.CloseModal();
            await Wait(0.4f);

            _player.Teleport(new Vector3(24f, 0.2f, 0.6f), Mathf.Pi);
            _rig.SetView(180f, 18f, 8.2f);
            await Wait(0.7f);
            await Snap("p9_05_girl_sunny_outfit_world.png", DeveloperCaptureDirectory);

            _data.CharacterPreset = CharacterCustomisation.Boy;
            _data.EquippedOutfit = CharacterCustomisation.DefaultOutfit;
            ApplyCosmetics();
            await Wait(0.5f);
            await Snap("p9_06_boy_default_outfit_world.png", DeveloperCaptureDirectory);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Capture failed: {ex}");
        }
        GetTree().Quit();
    }

    private async Task Wait(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }

    private void CaptureManualScreenshotSafely()
    {
        _ = CaptureManualScreenshotAsync();
    }

    private async Task CaptureManualScreenshotAsync()
    {
        try
        {
            GD.Print($"PLAYER_POS {_player.GlobalPosition} ACTIVE={_started} PAUSED={GetTree().Paused} CONTROL={_player.ControlEnabled} VEL={_player.Velocity} TARGET={_player.NearestInteractable?.Name} DRIVING={_driving} VAN_POS={_van.GlobalPosition} VAN_YAW={_van.Rotation.Y:0.00} VAN_SPEED={_van.Speed:0.0} NODES={GetTree().GetNodeCount()} MODE={_controlMode}");
            await Wait(0.15f);
            await Snap($"manual_{DateTime.Now:HHmmss}.png", ScreenshotDirectory);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Screenshot failed: {ex}");
        }
    }

    private async Task Snap(string fileName, string directory)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var viewport = GetViewport();
        if (viewport == null) throw new InvalidOperationException("No viewport is available for a screenshot.");
        var image = viewport.GetTexture()?.GetImage();
        if (image == null) throw new InvalidOperationException("The viewport image is unavailable.");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        Error error = image.SavePng(path);
        if (error != Error.Ok)
            GD.PrintErr($"SCREENSHOT FAILED {path} -> {error}");
        else
            GD.Print($"SCREENSHOT {path} -> {error}");
    }

}
