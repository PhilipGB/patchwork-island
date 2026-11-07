using System;
using System.IO;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Composition root. Owns the world, player, camera, UI and the shared game
/// services. Gameplay milestones add their systems here without replacing the
/// stable boot/run flow.
/// </summary>
public partial class Main : Node3D
{
    private Hud _hud;
    private WorldBuilder _world;
    private PlayerController _player;
    private CameraRig _rig;
    private WorldMarker _marker;
    private Interactable _jobStation;
    private bool _started;
    private float _uiRefresh;
    private Vector3 _destination;
    private bool _hasDestination;
    private bool _lastIndoor;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        BuildEnvironment();

        _world = new WorldBuilder { Name = "WorldBuilder" };
        AddChild(_world);
        _world.Build();

        _player = new PlayerController { Name = "Player", Position = _world.PlayerSpawn };
        AddChild(_player);
        _player.Teleport(_world.PlayerSpawn, -Mathf.Pi / 2f);

        _rig = new CameraRig { Name = "CameraRig" };
        AddChild(_rig);
        _player.Rig = _rig;
        _rig.SetTarget(_player, false, 8.2f, 18f, 1.55f, true);
        _rig.SetView(-72f, 18f, 8.2f);

        _hud = new Hud { Name = "Hud" };
        AddChild(_hud);
        _hud.ModalStateChanged += OnModalStateChanged;
        _hud.JobsButtonPressed += OpenJobBoard;
        _hud.HintButtonPressed += ShowCurrentHint;
        _player.TargetChanged += OnTargetChanged;

        _marker = new WorldMarker();
        AddChild(_marker);
        _marker.ShowAt(_world.JobBoard, Palette.Yellow, "star");
        SetDestination(_world.JobBoard);

        CreateStations();
        _player.SetControlEnabled(false);
        Input.MouseMode = Input.MouseModeEnum.Visible;
        if (HasUserArg("--capture-title"))
        {
            _hud.ShowTitleScreen(false, null, StartAdventure, null, QuitGame);
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
        _hud.ShowTitleScreen(false, null, StartAdventure, null, QuitGame);
    }

    private async Task CaptureTitle()
    {
        await Wait(1.2f);
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "evidence");
        Directory.CreateDirectory(dir);
        await Snap("p1_00_title.png", dir);
        GetTree().Quit();
    }

    private static bool HasUserArg(string needle)
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg == needle) return true;
        return false;
    }

    private async Task CaptureTour()
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "evidence");
        Directory.CreateDirectory(dir);
        try
        {
            await Wait(1.6f);
            await Snap("p1_01_start.png", dir);

            var overview = new Camera3D { Fov = 55, Current = true };
            AddChild(overview);
            overview.GlobalPosition = new Vector3(-8f, 52f, 74f);
            overview.LookAt(new Vector3(8f, 0f, -2f), Vector3.Up);
            await Wait(0.8f);
            await Snap("p1_02_village_overview.png", dir);
            overview.QueueFree();
            _rig.Current = true;

            _player.Teleport(new Vector3(-22f, 0.2f, 0.5f), Mathf.Pi);
            _rig.SetView(180f, 18f, 8.2f);
            await Wait(0.8f);
            await Snap("p1_03_jobs_and_town.png", dir);

            _player.Teleport(new Vector3(-20f, 0.2f, -12.0f), 0f);
            _rig.SetView(0f, 18f, 8.2f);
            await Wait(0.7f);
            await Snap("p1_04_red_house.png", dir);

            _player.Teleport(new Vector3(25f, 0.2f, -4.0f), 0f);
            _rig.SetView(0f, 18f, 8.2f);
            await Wait(0.7f);
            await Snap("p1_05_workshop.png", dir);

            _player.Teleport(new Vector3(34f, 0.2f, 0.0f), -Mathf.Pi / 2f);
            _rig.SetView(-90f, 18f, 8.2f);
            await Wait(0.7f);
            await Snap("p1_06_bridge_and_meadow.png", dir);

            _player.Teleport(new Vector3(60f, 0.2f, 4.0f), -2.4f);
            _rig.SetView(-140f, 20f, 9.5f);
            await Wait(0.7f);
            await Snap("p1_07_race_meadow.png", dir);
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

    private async Task CaptureManualScreenshot()
    {
        GD.Print($"PLAYER_POS {_player.GlobalPosition} ACTIVE={_started} PAUSED={GetTree().Paused} CONTROL={_player.ControlEnabled} VEL={_player.Velocity} TARGET={_player.NearestInteractable?.Name}");
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "evidence");
        Directory.CreateDirectory(dir);
        await Wait(0.15f);
        await Snap($"manual_{DateTime.Now:HHmmss}.png", dir);
    }

    private async Task Snap(string fileName, string directory)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        string path = Path.Combine(directory, fileName);
        Error error = image.SavePng(path);
        GD.Print($"SCREENSHOT {path} -> {error}");
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

        // Friendly landmark interactions. These are replaced by mission-specific
        // stations in P2, but keep the world legible and interactive in P1.
        Interactable.Create(this, _world.RedHouseDoor + new Vector3(0, 0.1f, 1.0f),
            "E: Look at RED HOUSE", () => _hud.ShowInfo("RED HOUSE",
                "A bright red house. This is where deliveries will arrive. The sign says RED HOUSE."),
            null, true, 3.2f, "RedHouseLook");
        Interactable.Create(this, _world.ShopDoor + new Vector3(0, 0.1f, -1.0f),
            "E: Look at SHOP", () => _hud.ShowInfo("SHOP",
                "The market shop. Crates, groceries and a shopping basket are waiting for the Market Basket job."),
            null, true, 3.2f, "ShopLook");
        Interactable.Create(this, _world.WorkshopDoor + new Vector3(0, 0.1f, 1.0f),
            "E: Look at WORKSHOP", () => _hud.ShowInfo("WORKSHOP",
                "The orange WORKSHOP sign. Wood and stone are dropped off here."),
            null, true, 3.2f, "WorkshopLook");
        Interactable.Create(this, _world.PlayerHouseDoor + new Vector3(0, 0.1f, -1.0f),
            "E: Go inside YOUR HOUSE", () => { _hud.ShowBigToast("Your cosy house. Furniture comes later."); },
            null, true, 3.2f, "HouseLook");
    }

    private void StartAdventure()
    {
        _started = true;
        _player.SetControlEnabled(true);
        _hud.CloseModal();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _hud.ShowToast("Find the yellow JOBS board near the town hall. Walk with WASD or the arrow keys.", 6f);
    }

    private void OnModalStateChanged(bool open)
    {
        if (open)
        {
            _hud.SetPrompt(null);
            _player.SetControlEnabled(false);
            Input.MouseMode = Input.MouseModeEnum.Visible;
            GetTree().Paused = true;
        }
        else
        {
            GetTree().Paused = false;
            if (_started)
            {
                _player.SetControlEnabled(true);
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }
    }

    private void OpenJobBoard()
    {
        if (!_started) return;
        var body = _hud.OpenModal("Patchwork Jobs", new Vector2(760, 500), Palette.Blue);
        UiKit.Label(body, "Job board", 28, Palette.Blue);
        UiKit.Label(body, "The people of Patchwork Island need a helper. Ten jobs are being pinned to this board.", 21, Palette.Navy);
        UiKit.Label(body, "• The Red House Delivery — 3 apples\n• Workshop Supplies — 5 wood + 3 stone\n• Build the Bridge\n• Market Basket\n• Village Road Rally\n• Two-Stop Post\n• Fix the Swings\n• Park Planner\n• Race the Clock\n• Home Sweet Home", 19, Palette.Navy);
        UiKit.Label(body, "Jobs can be started from this board once the mission system is switched on.", 17, new Color("5C6B7A"));
    }

    private void ShowCurrentHint()
    {
        if (!_started) return;
        _hud.ShowHint("Find the bright yellow JOBS board by the town hall. It is marked with a star.");
    }

    private void OnTargetChanged(Interactable target)
    {
        _hud.SetPrompt(target?.GetPrompt());
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.F12)
        {
            _ = CaptureManualScreenshot();
            GetViewport().SetInputAsHandled();
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
                _hud.ShowPauseMenu(ResumeGame, SaveNow, OpenSettings, ConfirmNewGame, ReturnToTitle, QuitGame);
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_started || _hud.ModalOpen) return;
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

    private void SaveNow()
    {
        _hud.ShowToast("Saving arrives in the persistence milestone.", 2.5f, Palette.Stone);
    }

    private void OpenSettings()
    {
        var body = _hud.OpenModal("Settings", new Vector2(560, 340), Palette.Lavender);
        UiKit.Label(body, "Music and sound toggles arrive with the audio polish milestone.", 20, Palette.Navy);
        UiKit.Label(body, "The game is fully playable without audio.", 18, new Color("5C6B7A"));
    }

    private void ConfirmNewGame()
    {
        _hud.Confirm("Start a new game?", "This restarts the village from the beginning. Are you sure?", "Start new game",
            () =>
            {
                _player.Teleport(_world.PlayerSpawn, 0f);
                _hud.ShowBigToast("New game started. Find the JOBS board!");
            });
    }

    private void ReturnToTitle()
    {
        _started = false;
        _player.SetControlEnabled(false);
        _hud.ShowTitleScreen(false, null, StartAdventure, null, QuitGame);
    }

    private void QuitGame() => GetTree().Quit();

    public override void _Process(double delta)
    {
        if (!_started) return;
        if (GetTree().Paused) return;

        _uiRefresh -= (float)delta;
        if (_uiRefresh <= 0f)
        {
            _uiRefresh = 0.12f;
            RefreshObjectiveUi();
            if (_player.NearestInteractable != null)
                _hud.SetPrompt(_player.NearestInteractable.GetPrompt());
            else
                _hud.SetPrompt(null);
        }

        bool indoor = IsInsidePlayerHouse(_player.GlobalPosition);
        if (indoor != _lastIndoor)
        {
            _lastIndoor = indoor;
            _rig.SetIndoor(indoor);
        }

        Vector3 p = _player.GlobalPosition;
        bool inRiver = p.X > 40.7f && p.X < 49.3f && p.Y < 0.2f;
        if (p.Y < -5f || inRiver)
        {
            RecoverPlayer($"Splash! Back to dry land.");
        }
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

    private void SetDestination(Vector3 position)
    {
        _destination = position;
        _hasDestination = true;
    }

    private void RefreshObjectiveUi()
    {
        if (!_hasDestination)
        {
            _hud.SetDestination(null, 0, 0, null);
            return;
        }
        Vector3 p = _player.GlobalPosition;
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
        _hud.SetDestination("JOBS BOARD", distance, arrow, "star");
    }
}
