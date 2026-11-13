using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Drives the isolated academy venue: authored course lifetime, learner-car
/// physics, lesson evaluation, traffic-light/crossing state and safe return.
/// Academy-local positions are never written to the town/city save snapshots.
/// </summary>
public partial class DrivingAcademyController : Node3D
{
    public const string CoursePath = "res://scenes/academy/DrivingAcademyCourse.tscn";
    public const string TrainingCarPath =
        "res://scenes/academy/assets/TrainingCar_Visual.tscn";
    public const string CityFreeOrigin = "academy";

    public bool Active { get; private set; }
    public bool CanDriveRegularVan => Service.CanDriveRegularVan;
    public bool VenueLoaded => _courseRoot != null;
    public Vector3 VenueOrigin => _courseRoot?.GlobalPosition ?? Vector3.Zero;
    public bool InLesson => Active && _stage.Length > 0;
    public string CurrentStage => _stage;
    public VanController TrainingCar => _car;
    public int MarkerCount => _markers.Count;
    public AcademyLight Light => _light;
    public int MinorErrors => _minor;
    public int MajorErrors => _major;

    private GameSession _session;
    private WorldSceneAdapter _world;
    private Hud _hud;
    private PlayerController _player;
    private VanController _van;
    private CameraRig _rig;
    private AudioManager _audio;
    private IGameplayHost _host;
    private DrivingAcademyHud _academyHud;

    private Node3D _courseRoot;
    private Node3D _course;
    private VanController _car;
    private Node3D _pedestrian;
    private readonly Dictionary<string, Marker3D> _markers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MeshInstance3D> _lightLenses = new(StringComparer.Ordinal);

    private string _stage = "";
    private bool _practice;
    private int _step;
    private float _timer;
    private float _speedWarning;
    private float _reverseDistance;
    private bool _stoppedAtLine;
    private bool _lawfulSignalEntry;
    private bool _amberEntryUnavoidable;
    private bool _crossingEntered;
    private Vector3 _previousCrossingPosition;
    private bool _redOffence;
    private bool _lineCrossed;
    private Vector3 _previousSignalPosition;
    private bool _pedestrianOffence;
    private int _minor;
    private int _major;

    private AcademyLight _light = AcademyLight.Red;
    private float _lightTimer;
    private bool _pedestrianCrossing;
    private bool _pedestrianWaiting;
    private float _pedestrianT;

    private Vector3 _returnPlayer;
    private float _returnPlayerYaw;
    private Vector3 _stageSpawn;
    private float _stageYaw;
    private Interactable _schoolStation;
    private readonly List<Interactable> _coneStations = new();

    internal void Initialize(GameSession session, WorldSceneAdapter world, Hud hud,
        PlayerController player, VanController van, CameraRig rig, AudioManager audio,
        IGameplayHost host)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _hud = hud ?? throw new ArgumentNullException(nameof(hud));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _van = van ?? throw new ArgumentNullException(nameof(van));
        _rig = rig ?? throw new ArgumentNullException(nameof(rig));
        _audio = audio;
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Name = "DrivingAcademy";
        ProcessMode = ProcessModeEnum.Pausable;

        _academyHud = new DrivingAcademyHud { Name = "DrivingAcademyHud" };
        AddChild(_academyHud);
        CreateSchoolStations();
        SetProcess(true);
    }

    private DrivingAcademyService Service => _session.Academy;

    private void CreateSchoolStations()
    {
        if (_world.SchoolOffice == null) return;
        Marker3D enrollment = _world.SchoolMarker("EnrollmentInteraction");
        _schoolStation = Interactable.Create(this, enrollment.GlobalPosition,
            "E: Driving school", OpenSchool, null, true, 4.0f, "DrivingSchoolStation");
        _schoolStation.SetPrompt(() => Service.LicenceIssued
            ? "E: Driving school — licence earned"
            : Service.Enrolled
                ? "E: Driving school lessons"
                : $"E: Enrol at the driving school ({Service.Catalog.EnrolmentCoins} coins)");

        for (int i = 1; i <= DrivingAcademyRules.HelperConeCount; i++)
        {
            int number = i;
            Marker3D marker = _world.YardMarker($"ConeSlot_{i}/HelperCone_{i}");
            var station = Interactable.Create(this, marker.GlobalPosition,
                $"E: Check cone {number}", () => CheckCone(number), null, true, 2.4f,
                $"AcademyConeStation_{number}");
            station.SetPrompt(() => Service.Enrolled || Service.CoinsNeededForEnrolment > 0
                ? $"E: Check cone {number}"
                : $"E: Check cone {number}");
            _coneStations.Add(station);
        }
    }

    public void OpenSchool()
    {
        if (Active) return;
        if (_van.Driven)
        {
            _hud.ShowInfo("DRIVING SCHOOL", "Park the van and walk to the school door first.");
            return;
        }
        DrivingAcademyUi.ShowSchool(_hud, Service, Enrol, StartFromSchool, Resume, null);
    }

    private void Enrol()
    {
        var result = Service.Enrol();
        if (!result.Success)
        {
            _hud.SetModalStatus(result.Message, true);
            return;
        }
        _hud.SetCoins(Service.Data.Coins);
        _host.Save(SaveReason.MissionStart);
        _hud.ShowBigToast(result.Message, 4f, Palette.Success);
        _hud.CloseModal();
        OpenSchool();
    }

    private void StartFromSchool(string id)
    {
        var result = Service.StartLesson(id);
        if (!result.Success)
        {
            _hud.SetModalStatus(result.Message, true);
            return;
        }
        _hud.CloseModal();
        EnterStage(id, result.Practice);
    }

    private void Resume()
    {
        string id = Service.ActiveStage;
        if (id.Length == 0) return;
        bool practice = Service.LessonCompleted(id) || Service.LicenceIssued;
        _hud.CloseModal();
        EnterStage(id, practice);
    }

    private void CheckCone(int number)
    {
        bool fromSchoolModal = _hud.ModalOpen;
        var result = Service.AdvanceHelper(number);
        _host.Save(SaveReason.Autosave);
        if (result.Success)
        {
            _hud.SetCoins(Service.Data.Coins);
            _audio?.PlaySfx(result.CoinsAwarded > 0 ? "coin_collect" : "ui_confirm", -6f);
            _hud.ShowBigToast(result.Message, 3.4f,
                result.CoinsAwarded > 0 ? Palette.Success : Palette.Yellow);
            if (fromSchoolModal)
            {
                _hud.CloseModal();
                OpenSchool();
            }
        }
        else
        {
            _hud.ShowHint(result.Message);
        }
    }

    public void EnterStage(string id, bool practice)
    {
        if (_hud.ModalOpen) _hud.CloseModal();
        var stage = Service.Catalog.Find(id);
        if (stage == null)
        {
            _hud.ShowInfo("DRIVING SCHOOL", "That lesson is missing from the course.");
            return;
        }
        if (!Active) EnterVenue();
        _stage = id;
        _practice = practice || Service.LicenceIssued;
        ResetStageState();
        BuildPedestrian();
        UpdatePedestrian(0f);
        _academyHud.Visible = true;
        _car.SetInputEnabled(true);
        _hud.ShowBigToast(
            $"{stage.Title}. W/S drive, A/D steer, R restart, Esc pause. {stage.FirstHint}",
            6f, Palette.Yellow);
        _host.Save(SaveReason.MissionStart);
    }

    private void EnterVenue()
    {
        _returnPlayer = _player.GlobalPosition;
        _returnPlayerYaw = _player.Rotation.Y;
        _player.Visible = false;
        _player.SetControlEnabled(false);
        _van.SetDriven(false);
        _van.Visible = false;
        _van.SetInputEnabled(false);

        _courseRoot = new Node3D { Name = "AcademyVenue", Position = new Vector3(2000f, 0f, 0f) };
        AddChild(_courseRoot);
        var packed = ResourceLoader.Load<PackedScene>(CoursePath)
                     ?? throw new InvalidOperationException($"Missing authored academy {CoursePath}.");
        _course = packed.Instantiate<Node3D>()
                  ?? throw new InvalidOperationException($"Could not instantiate {CoursePath}.");
        _course.Name = "DrivingAcademyCourse";
        _courseRoot.AddChild(_course);
        CacheMarkers();

        _car = new VanController { Name = "AcademyTrainingCar", ProcessMode = ProcessModeEnum.Pausable };
        _courseRoot.AddChild(_car);
        _car.Initialize(Vector3.Zero, 0f, _hud, TrainingCarPath, false,
            "AcademyTrainingCar", false);
        _car.SetDriven(true);
        _car.SetInputEnabled(false);
        _car.SetEntryGate(() => false, () => "Academy learner car");
        _car.Crashed += OnCourseCrash;

        _rig.SetTarget(_car, true, 9.8f, 18f, 1.45f, true);
        _hud.SetAcademyOverlay(true);
        Active = true;
        ApplyLight();
    }

    private void CacheMarkers()
    {
        _markers.Clear();
        var root = _course.GetNodeOrNull<Node3D>("GameplayMarkers")
                   ?? throw new InvalidOperationException(
                       "DrivingAcademyCourse is missing GameplayMarkers.");
        foreach (Node child in root.GetChildren())
        {
            if (child is not Marker3D marker) continue;
            string id = marker.GetMeta("academy_id", "").AsString();
            if (string.IsNullOrWhiteSpace(id)) id = marker.Name.ToString();
            if (!_markers.TryAdd(id, marker))
                throw new InvalidOperationException($"Duplicate academy marker id '{id}'.");
        }
        foreach (string required in Service.Catalog.RequiredMarkers)
            if (!_markers.ContainsKey(required))
                throw new InvalidOperationException(
                     $"DrivingAcademyCourse is missing required marker '{required}'.");
        foreach (string id in new[] { "Lesson04_Crossing", "Final_Crossing" })
        {
            Marker3D marker = _markers[id];
            foreach (string key in new[] { "crossing_half_width", "crossing_half_length" })
                if (!marker.HasMeta(key) || marker.GetMeta(key).AsSingle() <= 0f)
                    throw new InvalidOperationException($"Academy crossing '{id}' needs authored '{key}'.");
        }
        foreach (string id in new[] { "Lesson04_TrafficLight", "Final_Stop" })
        {
            Marker3D marker = _markers[id];
            foreach (string key in new[] { "stop_line_half_width", "stop_line_approach_length" })
                if (!marker.HasMeta(key) || marker.GetMeta(key).AsSingle() <= 0f)
                    throw new InvalidOperationException($"Academy signal '{id}' needs authored '{key}'.");
        }
    }

    private void ResetStageState()
    {
        _step = 0;
        _timer = 0f;
        _speedWarning = 0f;
        _reverseDistance = 0f;
        _stoppedAtLine = false;
        _lawfulSignalEntry = false;
        _amberEntryUnavoidable = false;
        _crossingEntered = false;
        _redOffence = false;
        _lineCrossed = false;
        _pedestrianOffence = false;
        _minor = 0;
        _major = 0;
        _light = AcademyLight.Red;
        _lightTimer = 0f;
        _pedestrianT = 0f;
        _pedestrianWaiting = _stage is "L04" or "TEST01";
        _pedestrianCrossing = false;
        ApplyLight();

        string spawnId = _stage switch
        {
            "L03" => "Lesson03_Start",
            "L04" => "Lesson04_Start",
            "L05" => "Lesson05_Start",
            "L06" => "Lesson06_Start",
            "TEST01" => "Final_Start",
            _ => "PracticeVehicleSpawn",
        };
        Marker3D spawn = _markers.TryGetValue(spawnId, out var found)
            ? found
            : _markers["PracticeVehicleSpawn"];
        _stageSpawn = spawn.GlobalPosition;
        _stageYaw = spawn.RotationDegrees.Y;
        _car.TeleportTo(_stageSpawn, _stageYaw);
        _previousCrossingPosition = CrossingMarker().ToLocal(_stageSpawn);
        _previousSignalPosition = SignalMarker().ToLocal(_stageSpawn);
    }

    public void SetInputEnabled(bool enabled)
    {
        _car?.SetInputEnabled(enabled && Active && !_hud.ModalOpen);
    }

    public void ResetStage()
    {
        if (!InLesson) return;
        ResetStageState();
        UpdatePedestrian(0f);
        _hud.ShowBigToast("Fresh attempt — all safety notes cleared. Take your time.", 3f, Palette.Yellow);
    }

    public void ShowPauseMenu()
    {
        if (!Active) return;
        var body = _hud.OpenModal("Driving lesson paused", new Vector2(640, 470), Palette.Yellow);
        UiKit.Label(body, Service.Catalog.Find(_stage)?.Title ?? "Driving lesson", 26, Palette.Navy);
        UiKit.Label(body, "Your progress is saved. Retries are always free.", 19,
            new Color("42526B"));
        var resume = UiKit.Button(body, "Resume driving", 22, new Vector2(0, 54),
            new Color("DFF6E3"), Palette.Success);
        resume.Name = "AcademyPauseResume";
        resume.Pressed += () => _hud.CloseModal();
        var retry = UiKit.Button(body, "Restart attempt — clear safety notes  [R]", 21, new Vector2(0, 52),
            Palette.Cream, Palette.Orange);
        retry.Name = "AcademyPauseRetry";
        retry.Pressed += () =>
        {
            _hud.CloseModal();
            ResetStage();
        };
        var save = UiKit.Button(body, "Save now", 21, new Vector2(0, 52));
        save.Name = "AcademyPauseSave";
        save.Pressed += () =>
        {
            var result = _host.Save(SaveReason.Manual);
            _hud.SetModalStatus(result.Success
                ? "Saved. You can resume this lesson from the school."
                : "Save failed. Your previous save is unchanged.", !result.Success);
        };
        var exit = UiKit.Button(body, "Return to school", 21, new Vector2(0, 52),
            Palette.Cream, Palette.Teal);
        exit.Name = "AcademyPauseExit";
        exit.Pressed += () => ExitVenue(true);
    }

    public void ExitVenue(bool openSchool)
    {
        if (!Active)
        {
            if (openSchool) OpenSchool();
            return;
        }
        _car.StopEngineAudio();
        _courseRoot?.QueueFree();
        _courseRoot = null;
        _course = null;
        _car = null;
        _markers.Clear();
        _lightLenses.Clear();
        _pedestrian = null;
        Active = false;
        _stage = "";
        _academyHud.HideStage();

        _player.Visible = true;
        _player.Teleport(_returnPlayer, _returnPlayerYaw);
        _player.SetControlEnabled(true);
        _van.Visible = true;
        _rig.SetTarget(_player, false, 8.2f, 18f, 1.55f, true);
        _hud.SetPrompt(null);
        _hud.SetAcademyOverlay(false);
        _host.RefreshMission();
        _host.Save(SaveReason.Manual);
        if (openSchool) Callable.From(OpenSchool).CallDeferred();
    }

    private void OnCourseCrash()
    {
        if (!Active) return;
        GD.Print($"ACADEMY CRASH stage={_stage} at={_car.GlobalPosition} step={_step}");
        if (_stage == "TEST01" && !_practice) AddMajor("A dangerous off-course movement.");
        _car.TeleportTo(_stageSpawn, _stageYaw);
        _hud.ShowBigToast("Learner car recovered to the safe start.", 3f, Palette.Water);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Active || _car == null || _hud.ModalOpen) return;
        float dt = (float)delta;
        UpdateLight(dt);
        UpdatePedestrian(dt);
        UpdateHud();
        switch (_stage)
        {
            case "L01": Lesson01(dt); break;
            case "L02": Lesson02(dt); break;
            case "L03": Lesson03(dt); break;
            case "L04": Lesson04(dt); break;
            case "L05": Lesson05(dt); break;
            case "L06": Lesson06(dt); break;
            case "TEST01": FinalTest(dt); break;
        }
    }

    private void UpdateHud()
    {
        var stage = Service.Catalog.Find(_stage);
        if (stage == null) return;
        int steps = StageSteps(_stage);
        _academyHud.ShowStage(stage.Title, Math.Min(_step + 1, steps), steps,
            HintFor(_stage), Math.Abs(_car.Speed),
            _stage is "L04" or "TEST01" ? _light : null, TargetGuidance());
    }

    /// <summary>Marker the learner should aim for right now, for HUD wayfinding.</summary>
    private string TargetMarkerFor() => _stage switch
    {
        "L01" => _step == 0 ? "Lesson01_Accelerate" : "Lesson01_Stop",
        "L02" => $"Lesson02_Gate0{Math.Min(_step + 1, 3)}",
        "L03" => _step == 0 ? "Lesson03_StopLine" : "Lesson03_AfterStop",
        "L04" => _step switch { 0 => "Lesson04_TrafficLight", 1 => "Lesson04_Crossing", _ => "Lesson04_GiveWay" },
        "L05" => "Lesson05_ParkingTarget",
        "L06" => $"Lesson06_Checkpoint0{Math.Min(_step + 1, 4)}",
        "TEST01" => _step switch { 0 => "Final_Stop", 1 => "Final_Crossing", _ => "Final_Park" },
        _ => null,
    };

    private string TargetGuidance()
    {
        if (_car == null || !_markers.TryGetValue(TargetMarkerFor() ?? "", out var marker))
            return null;
        Vector3 to = marker.GlobalPosition - _car.GlobalPosition;
        float distance = new Vector2(to.X, to.Z).Length();
        if (distance < 4f) return null;
        float desired = Mathf.Atan2(-to.X, -to.Z);
        float relative = Mathf.Wrap(desired - _car.Rotation.Y, -Mathf.Pi, Mathf.Pi);
        string direction = relative switch
        {
            > -0.6f and < 0.6f => "straight ahead",
            >= 0.6f and < 2.0f => "ahead right",
            <= -0.6f and > -2.0f => "ahead left",
            >= 2.0f => "behind, turn right",
            _ => "behind, turn left",
        };
        return $"Next marker: {distance:0} m {direction}";
    }

    private static int StageSteps(string id) => id switch
    {
        "L01" => 2,
        "L02" => 3,
        "L03" => 2,
        "L04" => 3,
        "L05" => 2,
        "L06" => 4,
        "TEST01" => 5,
        _ => 1,
    };

    private string HintFor(string id) => id switch
    {
        "L01" => _step == 0 ? "Hold W and reach a gentle speed." : "Press S and stop on the marked zone.",
        "L02" => "Follow the three gate markers in order. Slow before corners.",
        "L03" => _step == 0 ? "Stop before the white line and wait 1.5 seconds." : "Move on when it is safe.",
        "L04" => _light is AcademyLight.Red or AcademyLight.RedAmber
            ? "Red means stop before the line."
            : _light == AcademyLight.Amber
                ? "Amber: stop unless you are too close to stop safely."
            : _pedestrianCrossing
                ? "Wait for the pedestrian to finish crossing."
                : "Green: go only when the crossing is clear.",
        "L05" => _step == 0 ? "Reverse gently at least 4 metres." : "Hold still inside the bay, within 25 degrees.",
        "L06" => "Visit checkpoints 1-4 in order. Watch the 20 mph sign.",
        "TEST01" => _step switch
        {
            0 => "Drive to the STOP line and wait for green.",
            1 => "Cross only on green and keep left.",
            2 => "Give way at the zebra crossing.",
            3 => "Park inside the marked bay and hold still.",
            _ => "Finish the drive safely.",
        },
        _ => "",
    };

    private void Lesson01(float dt)
    {
        float z = _car.GlobalPosition.Z;
        if (_step == 0)
        {
            if (Math.Abs(_car.Speed) >= 2f && z < _stageSpawn.Z + 6f)
                Advance(1, "Nice gentle speed. Now brake to a stop.");
            return;
        }
        if (Near("Lesson01_Stop", DrivingAcademyRules.StopZoneRadiusMetres) &&
            DrivingAcademyRules.IsStopped(_car.Speed))
            _timer += dt;
        else
            _timer = 0f;
        if (DrivingAcademyRules.StopHeld(_car.Speed, _timer))
            CompleteLessonStage();
        else if (Near("Lesson01_Stop", 14f) && !DrivingAcademyRules.IsStopped(_car.Speed) &&
                 _timer == 0f && _car.Speed > 3f)
            Hint("Ease off and press S before the marker.");
    }

    private void Lesson02(float dt)
    {
        string[] gates = { "Lesson02_Gate01", "Lesson02_Gate02", "Lesson02_Gate03" };
        if (_step >= gates.Length)
        {
            CompleteLessonStage();
            return;
        }
        if (Near(gates[_step], DrivingAcademyRules.GateRadiusMetres))
        {
            _step++;
            _hud.ShowBigToast($"Gate {_step} of 3. Keep going.", 2.4f, Palette.Success);
        }
    }

    private void Lesson03(float dt)
    {
        Marker3D line = _markers["Lesson03_StopLine"];
        Marker3D after = _markers["Lesson03_AfterStop"];
        if (_step == 0)
        {
            bool beforeLine = _car.GlobalPosition.Z < line.GlobalPosition.Z + 1f &&
                              _car.GlobalPosition.Z > line.GlobalPosition.Z - 16f &&
                              Math.Abs(_car.GlobalPosition.X - line.GlobalPosition.X) < 8f;
            if (beforeLine && DrivingAcademyRules.IsStopped(_car.Speed))
                _timer += dt;
            else if (!beforeLine)
                _timer = 0f;
            if (DrivingAcademyRules.StopHeld(_car.Speed, _timer))
            {
                _stoppedAtLine = true;
                Advance(1, "Stopped before the line. Move on when safe.");
            }
            else if (_car.GlobalPosition.Z > line.GlobalPosition.Z + 2f && _car.Speed > 1f)
                Hint("Stop before the white line, then count to two.");
            return;
        }
        // "Move on when safe": the marker sits a safe distance past the stop
        // line, so reaching its 3 m approach completes the release.
        if (_car.GlobalPosition.Z > after.GlobalPosition.Z - 3f)
            CompleteLessonStage();
    }

    private void Lesson04(float dt)
    {
        Marker3D line = _markers["Lesson04_TrafficLight"];
        Marker3D crossing = _markers["Lesson04_Crossing"];
        Marker3D giveWay = _markers["Lesson04_GiveWay"];
        if (DrivingAcademyRules.IsStopped(_car.Speed) && InsideSignalApproach(line, line.ToLocal(_car.GlobalPosition)))
            _stoppedAtLine = true;
        UpdateStopLine(line);
        if (_step == 0 && _stoppedAtLine)
            Advance(1, "Waiting at the line. Red and red+amber mean stop.");
        else if (_step == 1 && _lawfulSignalEntry)
            Advance(2, "Green — cross when the pedestrian is clear.");

        if (InsideCrossing(crossing) &&
            _pedestrianCrossing && Math.Abs(_car.Speed) > 0.3f)
            PedestrianOffence();

        if (Near("Lesson04_GiveWay", 11f))
        {
            if (_major == 0 && _lawfulSignalEntry)
                CompleteLessonStage();
            else if (_major == 0)
                Hint(_stoppedAtLine
                    ? "Wait for green, then cross before the GIVE WAY marker."
                    : "Stop at the red light before moving on.");
        }
    }

    private void Lesson05(float dt)
    {
        if (_step == 0)
        {
            if (_car.Speed < -0.05f)
                _reverseDistance += -_car.Speed * dt;
            if (_reverseDistance >= DrivingAcademyRules.ReverseDistanceMetres)
                Advance(1, "Good reversing. Now hold still inside the bay.");
            return;
        }
        Marker3D target = _markers["Lesson05_ParkingTarget"];
        bool parked = DrivingAcademyRules.IsParked(
            _car.GlobalPosition.X, _car.GlobalPosition.Z, _car.RotationDegrees.Y,
            target.GlobalPosition.X, target.GlobalPosition.Z, target.RotationDegrees.Y);
        if (parked && DrivingAcademyRules.IsStopped(_car.Speed))
            _timer += dt;
        else
            _timer = 0f;
        if (_timer >= DrivingAcademyRules.ParkingHoldSeconds)
            CompleteLessonStage();
    }

    private void Lesson06(float dt)
    {
        string[] points =
        {
            "Lesson06_Checkpoint01", "Lesson06_Checkpoint02",
            "Lesson06_Checkpoint03", "Lesson06_Checkpoint04",
        };
        if (_step >= points.Length)
        {
            CompleteLessonStage();
            return;
        }
        TrackSpeeding(dt);
        if (Near(points[_step], DrivingAcademyRules.CheckpointRadiusMetres))
        {
            _step++;
            _hud.ShowBigToast($"Checkpoint {_step} of 4.", 2.2f, Palette.Success);
        }
    }

    private void FinalTest(float dt)
    {
        Marker3D stopLine = _markers["Final_Stop"];
        Marker3D crossing = _markers["Final_Crossing"];
        Marker3D park = _markers["Final_Park"];
        TrackSpeeding(dt);

        UpdateStopLine(stopLine);

        switch (_step)
        {
            case 0:
                if (InsideSignalApproach(stopLine, stopLine.ToLocal(_car.GlobalPosition)) &&
                    Near("Final_Stop", 10f) && DrivingAcademyRules.IsStopped(_car.Speed))
                    _timer += dt;
                else
                    _timer = 0f;
                if (DrivingAcademyRules.StopHeld(_car.Speed, _timer))
                {
                    _stoppedAtLine = true;
                    _step = 1;
                    _hud.ShowBigToast("Stopped at the line. Wait for green.", 2.6f, Palette.Success);
                }
                break;
            case 1:
                if (_lawfulSignalEntry &&
                    stopLine.ToLocal(_car.GlobalPosition).Z < -2f)
                {
                    _step = 2;
                    _previousCrossingPosition = crossing.ToLocal(_car.GlobalPosition);
                }
                break;
            case 2:
                UpdateCrossing(crossing);
                break;
            case 3:
                bool parked = DrivingAcademyRules.IsParked(
                    _car.GlobalPosition.X, _car.GlobalPosition.Z, _car.RotationDegrees.Y,
                    park.GlobalPosition.X, park.GlobalPosition.Z, park.RotationDegrees.Y);
                if (parked && DrivingAcademyRules.IsStopped(_car.Speed))
                    _timer += dt;
                else
                    _timer = 0f;
                if (_timer >= DrivingAcademyRules.ParkingHoldSeconds)
                {
                    _step = 4;
                    FinishAssessment();
                }
                break;
        }
    }

    /// <summary>
    /// One-shot stop-line evaluation. The offence is decided only at the moment
    /// the car crosses the line, so a car that legally entered on green is not
    /// penalised when the signal later turns red while it is still clearing the
    /// junction. Amber permits entry only if safe stopping was impossible when
    /// amber began (not based on the speed chosen later at the line).
    /// </summary>
    private void UpdateStopLine(Marker3D line)
    {
        if (_lineCrossed) return;
        Vector3 current = line.ToLocal(_car.GlobalPosition);
        Vector3 previous = _previousSignalPosition;
        _previousSignalPosition = current;
        // A finite, directed segment, not an infinite world-Z plane. The car
        // must approach within the authored lane and travel through local Z=0
        // towards -Z. Side re-entry beyond the line cannot latch green entry.
        if (!InsideSignalApproach(line, previous) || current.Z >= 0f ||
            Math.Abs(current.X) > line.GetMeta("stop_line_half_width").AsSingle()) return;
        _lineCrossed = true;
        _lawfulSignalEntry = DrivingAcademyRules.LightAllowsEntry(_light) ||
                            (_light == AcademyLight.Amber && _amberEntryUnavoidable);
        if (DrivingAcademyRules.LightProhibitsEntry(_light))
            RedOffence();
        else if (!_lawfulSignalEntry)
            AddMajor("Amber means stop when you can do so safely. Retry is free.");
    }

    private Marker3D SignalMarker() => _markers[
        _stage == "TEST01" ? "Final_Stop" : "Lesson04_TrafficLight"];

    private static bool InsideSignalApproach(Marker3D line, Vector3 position) =>
        Math.Abs(position.X) <= line.GetMeta("stop_line_half_width").AsSingle() &&
        position.Z >= 0f && position.Z <= line.GetMeta("stop_line_approach_length").AsSingle();

    private Marker3D CrossingMarker() => _markers[
        _stage == "TEST01" ? "Final_Crossing" : "Lesson04_Crossing"];

    private bool InsideCrossing(Marker3D crossing)
    {
        Vector3 p = crossing.ToLocal(_car.GlobalPosition);
        return Math.Abs(p.X) <= crossing.GetMeta("crossing_half_width").AsSingle() &&
               Math.Abs(p.Z) <= crossing.GetMeta("crossing_half_length").AsSingle();
    }

    private void UpdateCrossing(Marker3D crossing)
    {
        // The authored marker faces along the lane (-local Z). Enter at its
        // approach face, stay within its width, then clear its departure face.
        Vector3 p = crossing.ToLocal(_car.GlobalPosition);
        float width = crossing.GetMeta("crossing_half_width").AsSingle();
        float length = crossing.GetMeta("crossing_half_length").AsSingle();
        if (Math.Abs(p.X) > width)
            _crossingEntered = false;
        else
        {
            if (p.Z <= length && p.Z >= -length &&
                _previousCrossingPosition.Z > length &&
                Math.Abs(_previousCrossingPosition.X) <= width)
                _crossingEntered = true;
            if (InsideCrossing(crossing) && _pedestrianCrossing && Math.Abs(_car.Speed) > 0.3f)
                PedestrianOffence();
            if (_crossingEntered && p.Z < -length && !_pedestrianCrossing &&
                !_pedestrianOffence && p.Z < _previousCrossingPosition.Z)
                Advance(3, "Crossing safely cleared. Now park in the marked bay.");
        }
        _previousCrossingPosition = p;
    }

    private void SelectLight(AcademyLight light)
    {
        _light = light;
        _lightTimer = 0f;
        _amberEntryUnavoidable = false;
        if (light == AcademyLight.Amber && !_lineCrossed)
        {
            Marker3D line = SignalMarker();
            Vector3 position = line.ToLocal(_car.GlobalPosition);
            float approachSpeed = Math.Max(0f, -line.GlobalBasis.Z.Dot(_car.Velocity));
            _amberEntryUnavoidable = InsideSignalApproach(line, position) &&
                DrivingAcademyRules.AmberEntryUnavoidable(position.Z, approachSpeed);
        }
        ApplyLight();
    }

    private void TrackSpeeding(float dt)
    {
        if (DrivingAcademyRules.IsSpeeding(Math.Abs(_car.Speed)))
        {
            _speedWarning += dt;
            if (_speedWarning >= 1.2f)
            {
                _speedWarning = 0f;
                _minor++;
                Hint($"Watch the 20 mph sign. Minor note {_minor}.");
            }
        }
        else
        {
            _speedWarning = 0f;
        }
    }

    private void RedOffence()
    {
        if (_redOffence) return;
        _redOffence = true;
        AddMajor("You crossed the stop line on red.");
        _hud.ShowBigToast("Red means stop before the line. Retry is free.", 3.6f, Palette.Red);
    }

    private void PedestrianOffence()
    {
        if (_pedestrianOffence) return;
        _pedestrianOffence = true;
        AddMajor("Give way — the pedestrian was on the crossing.");
        _hud.ShowBigToast("Zebra crossing: let the pedestrian finish.", 3.6f, Palette.Red);
    }

    private void AddMajor(string message)
    {
        _major++;
        _hud.ShowBigToast(message, 3.2f, Palette.Red);
    }

    private void Advance(int step, string message)
    {
        _step = step;
        _timer = 0f;
        _hud.ShowBigToast(message, 2.8f, Palette.Success);
    }

    private void CompleteLessonStage()
    {
        if (_practice || Service.LessonCompleted(_stage) || Service.LicenceIssued)
        {
            ShowPracticeResult();
            return;
        }
        var result = Service.CompleteLesson(_stage);
        if (!result.Success)
        {
            _hud.ShowInfo("DRIVING SCHOOL", result.Message);
            return;
        }
        _audio?.PlaySfx("mission_complete", -5f);
        _host.Save(SaveReason.MissionCompleted);
        _academyHud.HideStage();
        string completed = _stage;
        _stage = "";
        DrivingAcademyUi.ShowResult(_hud, "LESSON PASSED", result.Message, true,
            null, () => ExitVenue(true), () => ExitVenue(false));
        _host.RefreshMission();
    }

    private void ShowPracticeResult()
    {
        _academyHud.HideStage();
        _hud.ShowBigToast("Practice complete. No coins or licence changes.", 4f, Palette.Success);
        ExitVenue(true);
    }

    private void FinishAssessment()
    {
        bool practice = _practice || Service.LicenceIssued;
        var grade = Service.SubmitAssessment(_major, _minor, practice);
        if (grade.Passed)
        {
            _audio?.PlaySfx("mission_complete", -4f);
            _host.Save(SaveReason.MissionCompleted);
            _academyHud.HideStage();
            _stage = "";
            string detail = practice
                ? "Practice test complete. Your licence is unchanged."
                : $"{grade.Message} Open the school to see your licence card.";
            DrivingAcademyUi.ShowResult(_hud, practice ? "PRACTICE PASSED" : "LICENCE EARNED",
                detail, true, null, () => ExitVenue(true), () => ExitVenue(false));
            _host.RefreshMission();
            return;
        }

        _host.Save(SaveReason.Autosave);
        _academyHud.HideStage();
        _stage = "";
        DrivingAcademyUi.ShowResult(_hud, "TEST NOT PASSED", grade.Message, false,
            () => RetryTest(), () => ExitVenue(true), () => ExitVenue(false));
    }

    private void RetryTest()
    {
        _hud.CloseModal();
        EnterStage("TEST01", Service.LicenceIssued);
    }

    private void UpdateLight(float dt)
    {
        if (_stage is not ("L04" or "TEST01")) return;
        _lightTimer += dt;
        if (_lightTimer >= DrivingAcademyRules.LightSeconds(_light))
        {
            SelectLight(DrivingAcademyRules.NextLight(_light));
        }
    }

    private void ApplyLight()
    {
        if (_course == null) return;
        var lightNode = _course.GetNodeOrNull<Node3D>("Props/TrafficLightCentre");
        if (lightNode == null) return;
        SetLens(lightNode, "Red", _light is AcademyLight.Red or AcademyLight.RedAmber);
        SetLens(lightNode, "Amber", _light is AcademyLight.Amber or AcademyLight.RedAmber);
        SetLens(lightNode, "Green", _light == AcademyLight.Green);
    }

    private void SetLens(Node3D lightNode, string name, bool on)
    {
        var mesh = lightNode.GetNodeOrNull<MeshInstance3D>(name);
        if (mesh == null) return;
        Color color = name switch
        {
            "Red" => new Color("E0524F"),
            "Amber" => new Color("F4C944"),
            _ => new Color("59C86B"),
        };
        mesh.MaterialOverride = on ? Geo.Mat(color, true) : Geo.Mat(color.Darkened(0.72f));
        mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    private void BuildPedestrian()
    {
        if (_pedestrian != null)
        {
            _pedestrian.QueueFree();
            _pedestrian = null;
        }
        if (_stage is not ("L04" or "TEST01")) return;
        var packed = ResourceLoader.Load<PackedScene>(VisualTemplate.PlayerGirlPath)
                     ?? throw new InvalidOperationException("Missing academy pedestrian visual.");
        _pedestrian = packed.Instantiate<Node3D>();
        VisualTemplate.ApplyVertexColours(_pedestrian, VisualTemplate.VertexColourMaterial());
        _pedestrian.Name = "AcademyCrossingPedestrian";
        _courseRoot.AddChild(_pedestrian);
    }

    private void UpdatePedestrian(float dt)
    {
        if (_pedestrian == null) return;
        _pedestrianWaiting = _stage is "L04" or "TEST01" &&
                             DrivingAcademyRules.LightMeansStop(_light);
        _pedestrianCrossing = _stage is "L04" or "TEST01" &&
                              _light == AcademyLight.Green && _lightTimer < 3.5f;
        _pedestrian.Visible = _pedestrianWaiting || _pedestrianCrossing;
        if (!_pedestrian.Visible) return;
        _pedestrianT = _pedestrianCrossing
            ? Mathf.Clamp(_lightTimer / 3.5f, 0f, 1f)
            : 0f;
        Marker3D crossing = CrossingMarker();
        float x = _pedestrianCrossing
            ? Mathf.Lerp(-4.5f, 4.5f, _pedestrianT)
            : -4.5f;
        _pedestrian.GlobalPosition = crossing.ToGlobal(new Vector3(x, 0f, 0f));
    }

    internal void TestPlaceCar(Vector3 position, float yawDegrees, float speed)
    {
        if (_car == null) return;
        _car.TeleportTo(position, yawDegrees);
        _car.TestLaunch(speed);
    }

    internal void TestSetLight(AcademyLight light)
    {
        SelectLight(light);
        _lightTimer = light == AcademyLight.Green ? 1f : 0f;
    }

    internal Vector3 TestMarker(string id) =>
        _markers.TryGetValue(id, out Marker3D marker) ? marker.GlobalPosition : Vector3.Zero;

    internal float TestMarkerYaw(string id) =>
        _markers.TryGetValue(id, out Marker3D marker) ? marker.RotationDegrees.Y : 0f;

    internal int TestStep => _step;
    internal bool TestLawfulSignalEntry => _lawfulSignalEntry;
    internal float TestSpeedWarning => _speedWarning;
    internal float TestLightRemaining => DrivingAcademyRules.LightSeconds(_light) - _lightTimer;
    internal string TestGuidance => _academyHud?.GuidanceText ?? "";
    internal Node3D TestPedestrian => _pedestrian;
    internal bool TestPedestrianCrossing => _pedestrianCrossing;

    private bool Near(string markerId, float radius)
    {
        if (!_markers.TryGetValue(markerId, out Marker3D marker)) return false;
        Vector3 a = _car.GlobalPosition;
        Vector3 b = marker.GlobalPosition;
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return dx * dx + dz * dz <= radius * radius;
    }

    private void Hint(string message)
    {
        if (_stage.Length == 0) return;
        _hud.ShowHint(message);
    }
}
