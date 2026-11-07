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
    private WorldBuilder _world;
    private PlayerController _player;
    private CameraRig _rig;
    private WorldMarker _marker;
    private MissionCatalog _catalog;
    private GameData _data;
    private MissionService _missions;
    private readonly HashSet<string> _implemented = new() { "M01", "M02" };
    private readonly List<Pickup> _pickups = new();
    private Interactable _jobStation;
    private Interactable _redHouseStation;
    private Interactable _workshopStation;
    private Interactable _shopStation;
    private Interactable _houseStation;

    private bool _started;
    private float _uiRefresh;
    private Vector3 _destination;
    private string _destLabel = "";
    private string _destIcon = "star";
    private bool _hasDestination;
    private bool _lastIndoor;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        BuildEnvironment();
        LoadMissionContent();

        _world = new WorldBuilder { Name = "WorldBuilder" };
        AddChild(_world);
        _world.Build();

        _player = new PlayerController { Name = "Player", Position = _world.PlayerSpawn };
        AddChild(_player);

        _rig = new CameraRig { Name = "CameraRig" };
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

        _marker = new WorldMarker();
        AddChild(_marker);

        CreateStations();
        SpawnPickups();
        RefreshMissionState();

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

    private void LoadMissionContent()
    {
        try
        {
            string json = Godot.FileAccess.GetFileAsString("res://data/missions.json");
            _catalog = MissionCatalog.FromJson(json);
            _data = new GameData();
            _missions = new MissionService(_data, _catalog);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Mission content failed to load: {ex}");
            _catalog = new MissionCatalog(new List<MissionDefinition>
            {
                new()
                {
                    Id = "M01",
                    Title = "The Red House Delivery",
                    Short = "Deliver 3 apples to the RED HOUSE.",
                    RewardCoins = 10,
                    Hints = new List<string> { "Find the apples at the shop." },
                },
            });
            _data = new GameData();
            _missions = new MissionService(_data, _catalog);
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

        _houseStation = Interactable.Create(this, _world.PlayerHouseDoor + new Vector3(0, 0.1f, -1.0f),
            "E: Go inside YOUR HOUSE", () => _hud.ShowBigToast("Your cosy house. Furniture jobs open later."),
            null, true, 3.2f, "HouseStation");
    }

    private string RedHousePrompt()
    {
        if (_data.IsCompleted("M01")) return "E: RED HOUSE — delivery done";
        if (_data.ActiveMission == "M01")
        {
            int apples = _data.GetItem("apple");
            return apples >= 3 ? "E: Deliver 3 apples to RED HOUSE" : $"E: RED HOUSE — need 3 apples ({apples}/3)";
        }
        return "E: Look at RED HOUSE";
    }

    private string WorkshopPrompt()
    {
        if (_data.IsCompleted("M02")) return "E: WORKSHOP — supplies delivered";
        if (_data.ActiveMission == "M02")
        {
            int wood = _data.GetItem("wood");
            int stone = _data.GetItem("stone");
            return wood >= 5 && stone >= 3
                ? "E: Hand in 5 wood + 3 stone"
                : $"E: WORKSHOP — needs 5 wood + 3 stone";
        }
        return "E: Look at WORKSHOP";
    }

    private string ShopPrompt()
    {
        if (_data.ActiveMission == "M01" || _data.ActiveMission == "M02")
            return "E: Look at the SHOP";
        return "E: Look at the SHOP";
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
        if (_data.IsCompleted("M01"))
            _hud.ShowBigToast("The Red House already has its apples.");
        else
            _hud.ShowHint("Start The Red House Delivery at the yellow JOBS board.");
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
        if (_data.IsCompleted("M02"))
            _hud.ShowBigToast("The workshop is fully supplied.");
        else
            _hud.ShowHint("Start Workshop Supplies at the yellow JOBS board.");
    }

    private void InteractShop()
    {
        if (_data.ActiveMission == "M01" && _data.GetItem("apple") < 3)
            _hud.ShowBigToast("The three apples are in the crate beside the SHOP.");
        else
            _hud.ShowInfo("SHOP", "The little market sells bread and milk for future jobs. The apple crate is right outside.");
    }

    private void SpawnPickups()
    {
        SpawnItemSet("apple", "M01", 3, _world.AppleSpawnPoints, Palette.Red);
        SpawnItemSet("wood", "M02", 5, _world.WoodSpawnPoints, Palette.Wood);
        SpawnItemSet("stone", "M02", 3, _world.StoneSpawnPoints, Palette.Stone);
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
        _ => Palette.Yellow,
    };

    private void StartAdventure()
    {
        _started = true;
        _player.SetControlEnabled(true);
        _hud.CloseModal();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        RefreshMissionState();
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
                _player.ForceRefreshInteraction();
            }
        }
    }

    private void OpenJobBoard()
    {
        if (!_started) return;
        JobBoardUi.Show(_hud, _missions, _implemented, StartMissionFromBoard);
    }

    private void StartMissionFromBoard(string missionId)
    {
        var result = _missions.StartMission(missionId);
        _hud.CloseModal();
        if (!result.Success)
        {
            _hud.ShowHint(result.Message);
            return;
        }
        RefreshMissionState();
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

    private void RefreshMissionState()
    {
        _hud.SetCoins(_data.Coins);
        ApplyCosmetics();
        RefreshPickupVisibility();

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
        else
        {
            rows.Add(("star", "Follow the glowing marker"));
        }
        return rows;
    }

    private void RefreshPickupVisibility()
    {
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
        _player.SetCapColor(_data.UnlockedCosmetics.Contains("red_cap") ? Palette.Red : Palette.Blue);
    }

    private void OnMissionCompleted(MissionCompletionResult result)
    {
        RefreshMissionState();
        string detail = result.Mission.Id switch
        {
            "M01" => "3 apples delivered to the RED HOUSE. A red cap is now yours!",
            "M02" => "5 wood + 3 stone delivered — all 8 supplies counted!",
            _ => result.Message,
        };
        _hud.ShowSuccess(result.Mission.Title, result.TotalCoins, detail, () => { });
        if (result.AllComplete && _catalog.Missions.Count >= 10)
        {
            _world.CelebrationDecor.Visible = true;
        }
    }

    private void SetObjective(Vector3 position, string label, string icon, Color color)
    {
        _destination = position;
        _destLabel = label;
        _destIcon = icon;
        _hasDestination = true;
        _marker.ShowAt(position, color, icon);
        RefreshObjectiveUi();
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
                ResetSession();
                _hud.ShowBigToast("New game started. Find the JOBS board!");
            });
    }

    private void ResetSession()
    {
        _data = new GameData();
        _missions = new MissionService(_data, _catalog);
        foreach (var pickup in _pickups)
            if (GodotObject.IsInstanceValid(pickup))
                pickup.QueueFree();
        _pickups.Clear();
        SpawnPickups();
        _player.Teleport(_world.PlayerSpawn, -Mathf.Pi / 2f);
        _rig.SetView(-72f, 18f, 8.2f);
        RefreshMissionState();
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
        _hud.SetDestination(_destLabel, distance, arrow, _destIcon);
    }

    // ------------------------------------------------------------- dev capture

    private static bool HasUserArg(string needle)
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg == needle) return true;
        return false;
    }

    private async Task CaptureTitle()
    {
        await Wait(1.2f);
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "evidence");
        Directory.CreateDirectory(dir);
        await Snap("p2_00_title.png", dir);
        GetTree().Quit();
    }

    private async Task CaptureTour()
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "evidence");
        Directory.CreateDirectory(dir);
        try
        {
            await Wait(1.3f);
            await Snap("p2_01_start.png", dir);

            var overview = new Camera3D { Fov = 55, Current = true };
            AddChild(overview);
            overview.GlobalPosition = new Vector3(-8f, 52f, 74f);
            overview.LookAt(new Vector3(8f, 0f, -2f), Vector3.Up);
            await Wait(0.7f);
            await Snap("p2_02_village_overview.png", dir);
            overview.QueueFree();
            _rig.Current = true;

            JobBoardUi.Show(_hud, _missions, _implemented, _ => { });
            await Wait(0.6f);
            await Snap("p2_03_job_board.png", dir);
            _hud.CloseModal();
            await Wait(0.4f);

            // Put the player near the market with M01 active for evidence.
            _missions.StartMission("M01");
            RefreshMissionState();
            _player.Teleport(new Vector3(-4.4f, 0.2f, 1.1f), Mathf.Pi);
            _rig.SetView(180f, 18f, 8.2f);
            await Wait(0.8f);
            await Snap("p2_04_apple_crate.png", dir);

            // Simulate a full M01 hand-in for screenshot evidence.
            _missions.CollectPickup("apple_0", "apple", "M01", 3);
            _missions.CollectPickup("apple_1", "apple", "M01", 3);
            _missions.CollectPickup("apple_2", "apple", "M01", 3);
            RefreshMissionState();
            _player.Teleport(new Vector3(-20f, 0.2f, -14.0f), 0f);
            _rig.SetView(0f, 18f, 8.2f);
            await Wait(0.7f);
            await Snap("p2_05_red_house_delivery.png", dir);

            var completion = _missions.DeliverApples();
            if (completion.Success)
            {
                OnMissionCompleted(completion);
                await Wait(0.6f);
                await Snap("p2_06_job_complete.png", dir);
                _hud.CloseModal();
                await Wait(0.4f);
            }

            _missions.StartMission("M02");
            RefreshMissionState();
            _player.Teleport(new Vector3(-40f, 0.2f, -24.0f), 0f);
            _rig.SetView(0f, 20f, 9f);
            await Wait(0.7f);
            await Snap("p2_07_grove.png", dir);

            _player.Teleport(new Vector3(14f, 0.2f, -27.0f), 0f);
            _rig.SetView(0f, 20f, 9f);
            await Wait(0.7f);
            await Snap("p2_08_stone_patch.png", dir);

            _player.Teleport(new Vector3(25f, 0.2f, -6.0f), 0f);
            _rig.SetView(0f, 18f, 8.2f);
            await Wait(0.7f);
            await Snap("p2_09_workshop.png", dir);
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
}
