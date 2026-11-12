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
public partial class Main : Node3D, IGameplayHost
{
    private Hud _hud;
    private VillageStationController _villageStations;
    private VillageStationController VillageStations => _villageStations ??= new VillageStationController(
        _gameSession, _hud, this, () => _bridgeBuilder, () => _swingBuilder, () => _parkBuilder, () => _furnitureBuilder);
    private PickupController _pickupController;
    private PickupController Pickups => _pickupController ??= new PickupController(
        _gameSession, this, _world, _hud, _player, _pickups, _audio, this);
    private VillagePopulationController _population;
    private VillagePopulationController Population => _population ??= new VillagePopulationController(
        _gameSession, this, _world, _hud, _player, _van, _npcs, _npcById, _trafficCars, _audio, this);
    private CityGameplayController _cityGameplay;
    private CityGameplayController CityGameplay => _cityGameplay ??= new CityGameplayController(
        _gameSession, this, _city, _roadworks, _hud, _audio, this);

    bool IGameplayHost.Started => _started;
    bool IGameplayHost.Driving => _driving;
    void IGameplayHost.RefreshMission() => RefreshMissionState();
    void IGameplayHost.RefreshMusic() => RefreshMusicContext();
    void IGameplayHost.CompleteVillageMission(MissionCompletionResult result) => OnMissionCompleted(result);
    void IGameplayHost.SetObjective(Vector3 position, string label, string icon, Color color) => SetObjective(position, label, icon, color);
    SaveResult IGameplayHost.Save(SaveReason reason) => SaveGame(reason);
    void IGameplayHost.NotifyRecovery(string message) => _recoveredJobNote = message;
    private MissionHudPresenter _missionPresenter;
    private MissionHudPresenter MissionPresenter => _missionPresenter ??= new MissionHudPresenter(
        _gameSession, _hud, _world, _player, _van, _roadworks, _npcById, _pickups,
        () => _driving, () => _raceTimer, IsInsidePlayerHouse, SetObjective);
    private AudioManager _audio;
    private WorldSceneAdapter _world;
    private PlayerController _player;
    private CameraRig _rig;
    private WorldMarker _marker;
    private MissionCatalog _catalog;
    private CityMissionCatalog _cityCatalog;
    private DrivingAcademyCatalog _academyCatalog;
    private GameSession _gameSession;
    private GameData _data => _gameSession.Data;
    private MissionService _missions => _gameSession.Village;
    private CityMissionService _cityMissions => _gameSession.City;
    private CityWorldAdapter _city;
    private TownCityRoadworks _roadworks;
    private float _cityMissionObjectiveRefresh;
    private bool _suppressScenarioSaves;
    private readonly HashSet<string> _implemented = new()
    {
        "M01", "M02", "M03", "M04", "M05", "M06", "M07", "M08", "M09", "M10",
        "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20",
        "M21",
    };
    private readonly List<Pickup> _pickups = new();
    private readonly List<NpcActor> _npcs = new();
    private readonly Dictionary<string, NpcActor> _npcById = new(StringComparer.Ordinal);
    private readonly List<TrafficCar> _trafficCars = new();
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
    private DrivingAcademyController _academy;
    private bool _buildMode;
    private bool _driving;
    private RaceController _races;
    private RaceController Races => _races ??= new RaceController(_gameSession, _world, _van, _hud, _audio, this);
    private RaceTimer _raceTimer { get => Races.Timer; set => Races.Timer = value; }
    private ControlMode _controlMode;
    private bool _practiceRaceActive => Races.PracticeActive;
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
            (IsHotfixTest && RuntimeSelfTest.HotfixPhase == 0) ||
            (IsChapterTest && RuntimeSelfTest.ChapterPhase == 0) ||
            (IsRestoreTest && RuntimeSelfTest.RestorePhase == 0) ||
            (IsReleaseTest && RuntimeSelfTest.ReleasePhase == 0) ||
            IsCityTest || IsAcademyTest)
            DeleteSelfTestSaves("selftest-save.json");
        else if (IsRecoveryTest && RuntimeSelfTest.RecoveryPhase == 0)
            DeleteSelfTestSaves("selftest-recovery-save.json");
        LoadSavedGameIfAvailable();
        Dev.ApplyDeveloperScenario();
        ApplySettings();

        _world = new WorldSceneAdapter { Name = "Village", ProcessMode = ProcessModeEnum.Pausable };
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

        _academy = new DrivingAcademyController { Name = "DrivingAcademy" };
        AddChild(_academy);
        _academy.Initialize(_gameSession, _world, _hud, _player, _van, _rig, _audio, this);
        _van.SetEntryGate(() => _academy.CanDriveRegularVan,
            () => _academy.CanDriveRegularVan
                ? "E: Drive the delivery van"
                : "E: Driving licence needed — visit the school");

        _city = new CityWorldAdapter { ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_city);
        _city.Build();
        _city.RestoreSimulation(_data.CitySeed, _data.CityLogicalSeconds);

        _roadworks = new TownCityRoadworks();
        AddChild(_roadworks);
        _roadworks.Build();

        CreateBuilderSystems();
        CreateStations();
        CityGameplay.CreateCityStations();
        CreateChapterTwoWorldVisuals();
        Pickups.SpawnPickups();
        Population.SpawnVillagers();
        Population.SpawnTraffic();
        RestoreSavedState();
        CityGameplay.ApplyCityPersistedState();
        ReconcileRestoredProgress();
        CityGameplay.ReconcileCityProgress();
        RefreshMissionState();

        SyncControlMode();
        Input.MouseMode = Input.MouseModeEnum.Visible;

        if (Dev.TryLaunch()) return;
        ShowTitle();
        if (HasUserArg("--selftest-quality"))
        {
            AddChild(new QualityRuntimeChecks { Main = this });
            return;
        }
        if (DeveloperMode && HasUserArg("--playthrough"))
        {
            AddChild(new GameplayWalkthrough { Main = this });
            return;
        }
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

    private void LoadMissionContent()
    {
        string json = Godot.FileAccess.GetFileAsString("res://data/missions.json");
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("res://data/missions.json is missing or empty.");
        _catalog = MissionCatalog.FromJson(json);
        if (!MissionIntegrity.ValidateCatalog(_catalog, out string error))
            throw new InvalidDataException("Mission content is incomplete: " + error);

        string cityJson = Godot.FileAccess.GetFileAsString("res://data/city_missions.json");
        if (string.IsNullOrWhiteSpace(cityJson))
            throw new InvalidDataException("res://data/city_missions.json is missing or empty.");
        _cityCatalog = CityMissionCatalog.FromJson(cityJson);
        if (!CityMissionCatalog.Validate(_cityCatalog, out string cityError))
            throw new InvalidDataException("City job content is incomplete: " + cityError);

        string academyJson = Godot.FileAccess.GetFileAsString(
            "res://data/driving_academy_lessons.json");
        if (string.IsNullOrWhiteSpace(academyJson))
            throw new InvalidDataException(
                "res://data/driving_academy_lessons.json is missing or empty.");
        _academyCatalog = DrivingAcademyCatalog.FromJson(academyJson);

        _gameSession = new GameSession(_catalog, _cityCatalog, _academyCatalog, new GameData());
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
    private static bool IsReleaseTest => HasUserArg("--selftest-release");
    private static bool IsHotfixTest => HasUserArg("--selftest-hotfix");
    private static bool IsRestoreTest => HasUserArg("--selftest-restore");
    private static bool IsCityTest => HasUserArg("--selftest-city");
    private static bool IsAcademyTest => HasUserArg("--selftest-academy");

    private static bool IsSelfTest =>
        HasUserArg("--selftest-quality") || HasUserArg("--selftest") || HasUserArg("--selftest-resume") || IsRecoveryTest ||
        IsLivingTest || IsRallyTest || IsChapterTest || IsFinaleTest || IsTraversalTest ||
        IsReleaseTest || IsHotfixTest || IsRestoreTest || IsCityTest || IsAcademyTest;

    private static bool IsSelfTestResume => HasUserArg("--selftest-resume");

    private static bool LoadSelfTestSave =>
        IsSelfTestResume || IsRecoveryTest ||
        (IsChapterTest && RuntimeSelfTest.ChapterPhase > 0) ||
        (IsReleaseTest && RuntimeSelfTest.ReleasePhase > 0) ||
        (IsRestoreTest && RuntimeSelfTest.RestorePhase > 0) ||
        (IsHotfixTest && RuntimeSelfTest.HotfixPhase > 0);

    /// <summary>
    /// F12, screenshot tours and scenario helpers are development tools. They
    /// are available in the editor, in an explicit --dev run, or for the
    /// existing scenario/capture/self-test command lines, never by accident in
    /// a normal player session.
    /// </summary>
    internal GameSession RtSession => _gameSession;
    internal DrivingAcademyController RtAcademy => _academy;
    internal void RtOpenDrivingSchool() => _academy.OpenSchool();
    internal void RtSaveForAcademyTest() => SaveGame(SaveReason.Autosave);

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

    private void LoadSavedGameIfAvailable(string path = null)
    {
        if (path == null && IsDeveloperRun() && !LoadSelfTestSave) return;
        var loaded = SaveService.Load(path ?? SavePath, _catalog);
        _session.ApplyLoad(loaded);
        switch (loaded.Status)
        {
            case SaveLoadStatus.Loaded:
            case SaveLoadStatus.RecoveredBackup:
                AdoptLoadedData(loaded.Data);
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

    /// <summary>
    /// Adopt a loaded save for every service that caches the game data. The
    /// town and city services must point at the same object: the city board
    /// gates on the loaded CityRoadOpen, and a stale service would reject city
    /// jobs even though the road is open.
    /// </summary>
    private void AdoptLoadedData(GameData data)
    {
        _gameSession.ReplaceData(data);
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
        if (_city != null)
        {
            _data.CitySeed = _city.Seed == 0 ? _data.CitySeed : _city.Seed;
            _data.CityLogicalSeconds = _city.LogicalSeconds;
        }
    }

    private SaveResult SaveGame(SaveReason reason)
    {
        if (_suppressScenarioSaves)
            return SaveResult.Ok();
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
            "E: Look at RED HOUSE", VillageStations.InteractRedHouse, null, true, 3.2f, "RedHouseStation");
        _redHouseStation.SetPrompt(VillageStations.RedHousePrompt);

        _workshopStation = Interactable.Create(this, _world.WorkshopDoor + new Vector3(0, 0.1f, 1.0f),
            "E: Look at WORKSHOP", VillageStations.InteractWorkshop, null, true, 3.2f, "WorkshopStation");
        _workshopStation.SetPrompt(VillageStations.WorkshopPrompt);

        _shopStation = Interactable.Create(this, _world.ShopDoor + new Vector3(0, 0.1f, -1.1f),
            "E: Look at the SHOP", VillageStations.InteractShop, null, true, 3.2f, "ShopStation");
        _shopStation.SetPrompt(VillageStations.ShopPrompt);

        _clothesStation = Interactable.Create(this, _world.ClothesShopDoor + new Vector3(0, 0.1f, -1.1f),
            "E: Visit the CLOTHES SHOP", VillageStations.InteractClothesShop, null, true, 3.4f, "ClothesShopStation");
        _clothesStation.SetPrompt(VillageStations.ClothesShopPrompt);

        _townHallStation = Interactable.Create(this, _world.TownHallDoor + new Vector3(0, 0.1f, -1.0f),
            "E: Look at TOWN HALL", VillageStations.InteractTownHall, null, true, 3.2f, "TownHallStation");
        _townHallStation.SetPrompt(VillageStations.TownHallPrompt);

        _bridgeStation = Interactable.Create(this, _world.BridgeSite,
            "E: Bridge worksite", VillageStations.InteractBridgeSite, null, true, 3.8f, "BridgeStation");
        _bridgeStation.SetPrompt(VillageStations.BridgeSitePrompt);

        _parkingStation = Interactable.Create(this, _world.ParkingSign + new Vector3(0, 0.1f, -0.8f),
            "E: Call van to parking", ResetVanAtSign, () => !_driving, true, 3.2f, "VanParkingStation");
        _parkingStation.SetPrompt(() => _driving ? "E: Parking sign" : "E: Call van to parking");

        _playgroundStation = Interactable.Create(this, _world.PlaygroundWorksite,
            "E: Playground worksite", VillageStations.InteractPlayground, null, true, 4.0f, "PlaygroundStation");
        _playgroundStation.SetPrompt(VillageStations.PlaygroundPrompt);

        _parkStation = Interactable.Create(this, _world.VillageGreenSign + new Vector3(0, 0.1f, -0.9f),
            "E: Village green", VillageStations.InteractPark, null, true, 4.0f, "ParkStation");
        _parkStation.SetPrompt(VillageStations.ParkPrompt);

        _furnitureStation = Interactable.Create(this, _world.FurnitureStation,
            "E: Furniture catalogue", VillageStations.InteractFurniture, null, true, 3.6f, "FurnitureStation");
        _furnitureStation.SetPrompt(VillageStations.FurniturePrompt);

        _houseStation = Interactable.Create(this, _world.PlayerHouseDoor + new Vector3(0, 0.1f, -1.0f),
            "E: Go inside YOUR HOUSE", VillageStations.InteractHouseDoor, null, true, 3.2f, "HouseStation");
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
        RepairUi.Show(_hud, _missions.Finale, result => OnMissionCompleted(result), RefreshMissionState);
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
        SignSocketUi.Show(_hud, _missions.Community, socketId,
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
        GardenPadUi.Show(_hud, _missions.Community, index,
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
        var mode = ControlModeRules.Resolve(_started, _hud.ModalOpen, _buildMode, _driving,
            _academy?.Active == true);
        var previous = _controlMode;
        _controlMode = mode;

        _player.SetControlEnabled(ControlModeRules.PlayerProcessesInput(mode));
        _van.SetInputEnabled(ControlModeRules.VehicleProcessesInput(mode) &&
                             _academy?.Active != true);
        if (_academy?.Active == true)
        {
            _player.SetControlEnabled(false);
            _academy.SetInputEnabled(mode == ControlMode.Academy ||
                                     (mode == ControlMode.Walking && !_hud.ModalOpen));
        }
        else
        {
            _academy?.SetInputEnabled(false);
        }
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
        if (_cityMissions?.ActiveMission != null)
        {
            _hud.ShowBigToast(
                $"{_cityMissions.ActiveMission.Title} is in progress at Patchwork City. " +
                "Finish it before starting a village job.", 4f, Palette.Teal);
            return;
        }
        var active = _missions.ActiveMission;
        bool showChapterTwo = active != null
            ? string.CompareOrdinal(active.Id, "M11") >= 0
            : MissionRules.OriginalChapterComplete(_data);
        JobBoardUi.Show(_hud, _missions, _implemented, StartMissionFromBoard, Races.StartPracticeRace,
            showChapterTwo);
    }

    private void StartMissionFromBoard(string missionId)
    {
        if (_cityMissions?.ActiveMission != null)
        {
            _hud.SetModalStatus("Finish your city job first. Village and city jobs are independent.", true);
            return;
        }
        Races.CancelPractice();
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
        if (_cityMissions?.ActiveMission != null)
        {
            var cityMission = _cityMissions.ActiveMission;
            var hint = cityMission.Hints != null && cityMission.Hints.Count > 0
                ? cityMission.Hints[Math.Min(_cityMissions.CityHintStep(cityMission.Id),
                    cityMission.Hints.Count - 1)]
                : "Follow the teal city marker.";
            _hud.ShowHint(hint);
            return;
        }
        if (_missions.ActiveMission == null)
        {
            if (_data.CityRoadOpen)
            {
                _hud.ShowHint("The road east is open. The teal CITY JOBS board is in Patchwork City's central plaza.");
                return;
            }
            _hud.ShowHint("Find the bright yellow JOBS board near the town hall. It is marked with a star.");
            return;
        }
        var level = _missions.GetNextHint(_missions.ActiveMission.Id);
        _hud.ShowHint(level.Text);
    }

    private void OnTargetChanged(Interactable target)
    {
        _hud.SetPrompt(target?.GetPrompt());
    }

    // ---------------------------------------------------------------- driving

    private void EnterVan()
    {
        if (!_academy.CanDriveRegularVan)
        {
            _hud.ShowInfo("DRIVING LICENCE NEEDED",
                "New drivers must earn a licence at the Driving School in the west of the village. " +
                "The learner car is free to use once you enrol.");
            return;
        }
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
            using var query = PhysicsRayQueryParameters3D.Create(
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
            using var shape = new PhysicsShapeQueryParameters3D
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
        Pickups.RefreshPickupVisibility();
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
            Races.RefreshPracticeMissionUi();
            return;
        }

        if (_cityMissions?.ActiveMission != null)
        {
            CityGameplay.RefreshCityMissionState();
            return;
        }

        var mission = _missions.ActiveMission;
        if (mission == null)
        {
            _hud.SetNoMission();
            if (_data.CityRoadOpen &&
                _data.CompletedCityMissions.Count < _cityCatalog.Missions.Count)
                SetObjective(_city.BoardApproach, "CITY JOBS", "delivery", Palette.Teal);
            else if (_data.CityRoadOpen)
                SetObjective(_city.DistrictCentre, "PATCHWORK CITY", "star", Palette.Teal);
            else
                SetObjective(_world.JobBoard, "JOBS BOARD", "star", Palette.Yellow);
            return;
        }

        MissionPresenter.Show(mission);
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
            _rallyRival.Initialize(route, route.Points,
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
            "M21" => "3 cones + 2 barrier panels + 1 toolbox = 6. The crew finished the loading check and the CITY ROAD IS OPEN!",
            _ => result.Message,
        };
        if (result.Mission.Id == RoadworksRules.MissionId)
        {
            _data.CityRoadworksCompleted = true;
            _data.CityRoadworksRewardClaimed = true;
            _data.CityRoadOpen = true;
            _roadworks.SetRoadState(true, true);
        }
        Action afterSuccess = () => { };
        if (result.Mission.Id == "M20")
            afterSuccess = () => Callable.From(ShowVillageFestival).CallDeferred();
        else if (result.Mission.Id == RoadworksRules.MissionId)
            afterSuccess = () => Callable.From(ShowCityRoadOpened).CallDeferred();
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
            Races.StartPracticeRace();
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

    private void ShowCityRoadOpened()
    {
        var body = _hud.OpenModal("CITY ROAD OPEN!", new Vector2(760, 470), Palette.Success);
        UiKit.Icon(body, "road_signs", 48);
        UiKit.Label(body, "The roadworks crew has finished. The barrier is gone and the bridge to Patchwork City is open.", 22, Palette.Navy);
        UiKit.Label(body,
            "Drive east over the new bridge. At the city's central plaza, press E at the teal CITY JOBS board. " +
            "City jobs C01, C02 and C03 are separate from the village board and pay into the same wallet.",
            19, new Color("5C6B7A"));
        var button = UiKit.Button(body, "Drive to the city", 21, new Vector2(0, 54),
            new Color("DFF6E3"), Palette.Success);
        button.Pressed += () => _hud.CloseModal();
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
            Dev.CaptureManualScreenshotSafely();
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
                activeBuild.HandleWorldClick(mb.Position);
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
            else if (_academy?.Active == true)
            {
                _academy.ShowPauseMenu();
            }
            else
            {
                _hud.ShowPauseMenu(ResumeGame, SaveNow, OpenSettingsFromPause, ConfirmNewGame, ReturnToTitle, QuitGame);
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_academy?.Active == true && @event.IsActionPressed("reset_vehicle"))
        {
            _academy.ResetStage();
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
        Races.Reset();
        _vehicleSwitchCooldown = 0f;
        Races.CancelPractice();
        _celebrationShown = false;
        _festivalShown = false;
        if (_rallyRival != null)
        {
            _rallyRival.QueueFree();
            _rallyRival = null;
        }
        _lastIndoor = false;
        _rig.SetIndoor(false);
        _gameSession.ReplaceData(new GameData());
        if (CharacterCustomisation.IsValidPreset(preset))
        {
            _data.CharacterPreset = preset;
            _data.EquippedOutfit = CharacterCustomisation.DefaultOutfit;
        }

        _city?.RestoreSimulation(_data.CitySeed, 0);
        _roadworks?.SetRoadState(false, false);

        _saveNotice = "";
        _recoveredJobNote = "";

        if (_world.BridgeBarrier != null) _world.BridgeBarrier.Visible = true;
        if (_world.CelebrationDecor != null) _world.CelebrationDecor.Visible = false;
        if (_world.GoldCourseFlag != null) _world.GoldCourseFlag.Visible = false;
        CityGameplay.RefreshCityJobVisuals();

        CreateBuilderSystems();
        foreach (var pickup in _pickups)
        {
            if (!GodotObject.IsInstanceValid(pickup)) continue;
            RemoveChild(pickup);
            pickup.QueueFree();
        }
        _pickups.Clear();
        Pickups.SpawnPickups();
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
        Races.CancelPractice();
        ShowTitle();
    }

    private void DiscardUnsavedSessionAndShowTitle()
    {
        Races.CancelPractice();
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

        _vehicleSwitchCooldown = Mathf.Max(0f, _vehicleSwitchCooldown - (float)delta);
        Races.Tick((float)delta);

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

        Vector3 cityFocus = _driving ? _van.GlobalPosition : _player.GlobalPosition;
        float citySpeed = _driving
            ? Mathf.Abs(_van.Speed)
            : new Vector2(_player.Velocity.X, _player.Velocity.Z).Length();
        _city?.SetFocusActors(_player.GlobalPosition, _van.GlobalPosition, _driving);
        _city?.UpdateCity(delta, cityFocus, citySpeed, _rig.GlobalPosition.Y, true);
        if (!_data.CityVisited && CityLayout.IsInCity(cityFocus.X, cityFocus.Z))
        {
            _data.CityVisited = true;

            _hud.ShowBigToast(
                "Welcome to Patchwork City! Find the teal CITY JOBS board in the central plaza.",
                6f, Palette.Teal);
            SaveGame(SaveReason.Autosave);
            RefreshMissionState();
        }

        if (_cityMissions?.ActiveMission != null)
        {
            _cityMissionObjectiveRefresh -= (float)delta;
            if (_cityMissionObjectiveRefresh <= 0f)
            {
                _cityMissionObjectiveRefresh = 0.5f;
                CityGameplay.RefreshCityMissionState();
            }
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
        if (CityLayout.IsInCity(p.X, p.Z))
            safe = new Vector3(CityLayout.WorldX(0f), 0.5f, CityLayout.WorldZ(24f));
        else if (p.X < 45f)
            safe = new Vector3(35.5f, 0.5f, Mathf.Clamp(p.Z, -46f, 46f));
        else
            safe = new Vector3(54.5f, 0.5f, Mathf.Clamp(p.Z, -46f, 46f));
        _player.Teleport(safe);
        _hud.ShowToast(message, 2.6f, Palette.Water);
    }

    private void RefreshObjectiveUi()
    {
        // The academy is an isolated venue: a town job compass pointed at
        // village coordinates would be both wrong and confusing there.
        if (_academy?.Active == true)
        {
            _hud.SetDestination(null, 0, 0, null);
            return;
        }
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


}
