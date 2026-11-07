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
    private readonly HashSet<string> _implemented = new() { "M01", "M02", "M03", "M04", "M05", "M06" };
    private readonly List<Pickup> _pickups = new();
    private Interactable _jobStation;
    private Interactable _redHouseStation;
    private Interactable _workshopStation;
    private Interactable _shopStation;
    private Interactable _houseStation;
    private Interactable _bridgeStation;
    private Interactable _townHallStation;
    private Interactable _parkingStation;
    private BridgeBuilder _bridgeBuilder;
    private VanController _van;
    private bool _buildMode;
    private bool _driving;
    private float _raceElapsed;
    private float _raceCheckTimer;
    private float _raceHudTimer;
    private float _raceWrongHintCooldown;

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
        ApplyDeveloperScenario();

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

        _bridgeBuilder = new BridgeBuilder();
        AddChild(_bridgeBuilder);
        _bridgeBuilder.Initialize(_data, _missions, _rig, _hud, _world, OnBridgeBuilt, SetBuildMode, RefreshMissionState);

        _van = new VanController();
        AddChild(_van);
        _van.Initialize(_world.VanSpawn, 0f, _hud);
        _van.EnterRequested += EnterVan;
        _van.Crashed += OnVanCrashed;

        CreateStations();
        SpawnPickups();
        RefreshMissionState();

        _player.SetControlEnabled(false);
        Input.MouseMode = Input.MouseModeEnum.Visible;

        if (HasUserArg("--scenario-m03"))
        {
            _started = true;
            _player.Teleport(new Vector3(34.2f, 0.2f, 4.2f), -Mathf.Pi / 2f);
            _rig.SetView(-90f, 18f, 8.2f);
            _player.SetControlEnabled(true);
            Input.MouseMode = Input.MouseModeEnum.Captured;
            RefreshMissionState();
            _hud.ShowToast("SCENARIO: M01+M02 done, Build the Bridge active. Press E at the worksite.", 4.5f);
            return;
        }
        if (HasUserArg("--scenario-m04"))
        {
            _started = true;
            _player.Teleport(new Vector3(5f, 0.2f, 0.8f), Mathf.Pi);
            _rig.SetView(180f, 18f, 8.2f);
            _player.SetControlEnabled(true);
            Input.MouseMode = Input.MouseModeEnum.Captured;
            RefreshMissionState();
            _hud.ShowToast("SCENARIO: M01 done, Market Basket active. Press E at the SHOP.", 4.5f);
            return;
        }
        if (HasUserArg("--scenario-m05"))
        {
            _started = true;
            _player.Teleport(new Vector3(-31f, 0.2f, 18.5f), 0f);
            _rig.SetView(0f, 18f, 8.2f);
            _player.SetControlEnabled(true);
            Input.MouseMode = Input.MouseModeEnum.Captured;
            RefreshMissionState();
            _hud.ShowToast("SCENARIO: M05 active. Drive the yellow van through gates 1-2-3-4.", 5f);
            return;
        }
        if (HasUserArg("--scenario-m06"))
        {
            _started = true;
            _player.Teleport(new Vector3(-9f, 0.2f, 4.5f), Mathf.Pi);
            _rig.SetView(180f, 18f, 8.2f);
            _player.SetControlEnabled(true);
            Input.MouseMode = Input.MouseModeEnum.Captured;
            RefreshMissionState();
            _hud.ShowToast("SCENARIO: M06 active. Collect BLUE and ORANGE parcels at the post counter.", 5f);
            return;
        }
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

    private void ApplyDeveloperScenario()
    {
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

        _townHallStation = Interactable.Create(this, _world.TownHallDoor + new Vector3(0, 0.1f, -1.0f),
            "E: Look at TOWN HALL", InteractTownHall, null, true, 3.2f, "TownHallStation");
        _townHallStation.SetPrompt(TownHallPrompt);

        _bridgeStation = Interactable.Create(this, _world.BridgeSite,
            "E: Bridge worksite", InteractBridgeSite, null, true, 3.8f, "BridgeStation");
        _bridgeStation.SetPrompt(BridgeSitePrompt);

        _parkingStation = Interactable.Create(this, _world.ParkingSign + new Vector3(0, 0.1f, -0.8f),
            "E: Call van to parking", ResetVanAtSign, () => !_driving, true, 3.2f, "VanParkingStation");
        _parkingStation.SetPrompt(() => _driving ? "E: Parking sign" : "E: Call van to parking");

        _houseStation = Interactable.Create(this, _world.PlayerHouseDoor + new Vector3(0, 0.1f, -1.0f),
            "E: Go inside YOUR HOUSE", () => _hud.ShowBigToast("Your cosy house. Furniture jobs open later."),
            null, true, 3.2f, "HouseStation");
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
        return "E: Look at the SHOP";
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

    private void InteractShop()
    {
        if (_data.ActiveMission == "M04")
        {
            GroceryUi.Show(_hud, _missions, RefreshMissionState);
            return;
        }
        if (_data.ActiveMission == "M01" && _data.GetItem("apple") < 3)
            _hud.ShowBigToast("The three apples are in the crate beside the SHOP.");
        else
            _hud.ShowInfo("SHOP", "The little market sells bread and milk. Start Market Basket at the JOBS board to use the 12-coin job purse.");
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
    }

    private void SpawnParcelPickups()
    {
        string[] types = { "blue_parcel", "blue_parcel", "orange_parcel" };
        for (int i = 0; i < types.Length && i < _world.ParcelSpawnPoints.Count; i++)
        {
            string type = types[i];
            string id = type == "blue_parcel" ? $"blue_parcel_{i}" : "orange_parcel_0";
            if (_data.HasCollected(id)) continue;
            int required = type == "blue_parcel" ? 2 : 1;
            var pickup = Pickup.Create(this, _world.ParcelSpawnPoints[i], id, type, "M06", required,
                TakePickup, () => PickupPrompt(type, "M06", required),
                () => _data.IsCompleted("M06"));
            _pickups.Add(pickup);
        }
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
                if (_driving)
                {
                    Input.MouseMode = Input.MouseModeEnum.Captured;
                }
                else
                {
                    _player.SetControlEnabled(true);
                    Input.MouseMode = Input.MouseModeEnum.Captured;
                    _player.ForceRefreshInteraction();
                }
            }
        }
    }

    private void OpenJobBoard()
    {
        if (!_started) return;
        if (_buildMode)
        {
            _hud.ShowBigToast("Finish this build first, then choose another job.", 2.6f, Palette.Lavender);
            return;
        }
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
        if (missionId == "M05" || missionId == "M09")
            _raceElapsed = _data.GetMissionCounter(missionId, "elapsedMs") / 1000f;
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

    // ---------------------------------------------------------------- driving

    private void EnterVan()
    {
        if (!_started || _hud.ModalOpen || _buildMode || _driving) return;
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
        _player.SetControlEnabled(true);
        Input.MouseMode = Input.MouseModeEnum.Captured;
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

    private void TickRace(float dt)
    {
        var mission = _missions.ActiveMission;
        if (!_driving || mission == null || (mission.Id != "M05" && mission.Id != "M09"))
            return;
        int checkpoint = _data.GetMissionCounter(mission.Id, "checkpoint");
        if (checkpoint > 0 && checkpoint < 4)
        {
            _raceElapsed += dt;
            _data.SetMissionCounter(mission.Id, "elapsedMs", (int)(_raceElapsed * 1000f));
        }

        _raceHudTimer -= dt;
        if (_raceHudTimer <= 0f)
        {
            _raceHudTimer = 0.8f;
            RefreshMissionState();
        }

        _raceCheckTimer -= dt;
        if (_raceCheckTimer > 0f || checkpoint >= 4) return;
        _raceCheckTimer = 0.15f;

        Vector3 gate = _world.RaceGates[checkpoint].Position;
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
            _raceElapsed = 0f;
            _data.SetMissionCounter(mission.Id, "started", 1);
        }
        _data.SetMissionCounter(mission.Id, "elapsedMs", (int)(_raceElapsed * 1000f));
        _hud.ShowBigToast(advance.Message, 2.4f, Palette.Yellow);
        RefreshMissionState();

        if (checkpoint + 1 >= 4)
        {
            int bonus = MissionRules.OptionalRaceBonus(mission.Id, _raceElapsed);
            var result = _missions.CompleteActive(bonus);
            if (result.Success)
                OnMissionCompleted(result);
            _raceElapsed = 0f;
        }
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{total / 60}:{total % 60:00}";
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
            _player.SetControlEnabled(false);
            _hud.SetPrompt(null);
            if (_marker != null) _marker.Visible = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        else
        {
            _hud.HideBuildBar();
            if (_started && !_hud.ModalOpen)
            {
                _player.SetControlEnabled(true);
                Input.MouseMode = Input.MouseModeEnum.Captured;
                _player.ForceRefreshInteraction();
            }
            RefreshMissionState();
        }
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
                rows.Add(("star", $"Time {FormatTime(_raceElapsed)} · bonus under {FormatTime(missionId == "M09" ? 75 : 90)}"));
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
            "M03" => "3 supports + 4 planks = a strong bridge. The meadow is open!",
            "M04" => "2 bread × 3 + 1 milk × 4 = 10. Change from 12 is 2. Delivered to TOWN HALL!",
            "M05" => result.BonusCoins > 0
                ? $"Four gates in {_raceElapsed:0.0}s — optional speed bonus +{result.BonusCoins}!"
                : $"Four gates finished in {_raceElapsed:0.0}s. Any pace is a winning pace!",
            "M06" => "2 BLUE parcels to RED HOUSE + 1 ORANGE parcel to WORKSHOP — every address correct!",
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
        if (_buildMode)
            _marker.HideMarker();
        else
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

        if (_buildMode && _bridgeBuilder != null && _bridgeBuilder.Active)
        {
            if (@event.IsActionPressed("pause"))
            {
                _bridgeBuilder.CloseBuildMode();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event.IsActionPressed("rotate_part"))
            {
                _bridgeBuilder.RotateSelected();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                _bridgeBuilder.HandleWorldClick(GetViewport().GetMousePosition());
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
                _hud.ShowPauseMenu(ResumeGame, SaveNow, OpenSettings, ConfirmNewGame, ReturnToTitle, QuitGame);
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
        if (_driving)
        {
            _driving = false;
            _van.SetDriven(false);
            _player.Visible = true;
            _rig.SetTarget(_player, false, 8.2f, 18f, 1.55f, true);
            _player.SetControlEnabled(true);
        }
        _van.ResetToParking();
        _raceElapsed = 0f;
        _data = new GameData();
        _missions = new MissionService(_data, _catalog);
        if (_bridgeBuilder != null)
        {
            _bridgeBuilder.CloseBuildMode();
            _bridgeBuilder.QueueFree();
        }
        if (_world.BridgeBarrier != null) _world.BridgeBarrier.Visible = true;
        _bridgeBuilder = new BridgeBuilder();
        AddChild(_bridgeBuilder);
        _bridgeBuilder.Initialize(_data, _missions, _rig, _hud, _world, OnBridgeBuilt, SetBuildMode, RefreshMissionState);
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

        _raceWrongHintCooldown = Mathf.Max(0f, _raceWrongHintCooldown - (float)delta);
        TickRace((float)delta);

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
        GD.Print($"PLAYER_POS {_player.GlobalPosition} ACTIVE={_started} PAUSED={GetTree().Paused} CONTROL={_player.ControlEnabled} VEL={_player.Velocity} TARGET={_player.NearestInteractable?.Name} DRIVING={_driving} VAN_POS={_van.GlobalPosition} VAN_YAW={_van.Rotation.Y:0.00} VAN_SPEED={_van.Speed:0.0}");
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
