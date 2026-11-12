using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// City/roadworks activity controller. Owns city interactions and presentation;
/// session replacement, persistence policy and village completion stay at the host.
/// </summary>
internal sealed class CityGameplayController
{
    private readonly GameSession _session;
    private readonly Node3D _root;
    private readonly CityWorldAdapter _city;
    private readonly TownCityRoadworks _roadworks;
    private readonly Hud _hud;
    private readonly AudioManager _audio;
    private readonly IGameplayHost _host;
    private GameData _data => _session.Data;
    private MissionService _missions => _session.Village;
    private CityMissionService _cityMissions => _session.City;

    internal CityGameplayController(GameSession session, Node3D root,
        CityWorldAdapter city, TownCityRoadworks roadworks, Hud hud,
        AudioManager audio, IGameplayHost host)
    {
        _session = session; _root = root; _city = city; _roadworks = roadworks;
        _hud = hud; _audio = audio; _host = host;
    }

    internal void CreateCityStations()
    {
        var roadworksStation = Interactable.Create(_root, _roadworks.InteractionPosition,
            "E: Roadworks notice", InteractRoadworks, null, true, 5.0f, "RoadworksStation");
        roadworksStation.SetPrompt(RoadworksPrompt);

        var cityJobStation = Interactable.Create(_root, _city.BoardInteraction,
            "E: Read CITY JOBS", OpenCityJobBoard,
            () => _data.CityRoadOpen, true, 3.6f, "CityJobBoardStation");
        cityJobStation.SetPrompt(() => _data.CityRoadOpen
            ? "E: Read the CITY JOBS board"
            : "CITY JOBS — road still closed");

        AddCityStation("CityDepotStation", "C01_Depot", "E: City depot", InteractCityDepot,
            () => CityDepotPrompt());
        for (int i = 1; i <= 3; i++)
        {
            int stop = i;
            AddCityStation($"CityStopStation_{stop}", $"C01_Stop{stop}",
                $"E: Parcel stop {stop}", () => InteractCityStop(stop),
                () => CityStopPrompt(stop));
        }
        for (int i = 0; i < 6; i++)
        {
            int crate = i;
            var station = AddCityStation($"CityCrateStation_{crate}", $"C02_Crate_{crate}",
                "E: Collect stock crate", () => InteractCityCrate(crate),
                () => CityCratePrompt(crate));
            station.AvailableProvider = () => _data.CityRoadOpen &&
                _cityMissions.Progress("C02", $"crate_{crate}") <= 0;
        }
        AddCityStation("CityStallStation", "C02_Stall", "E: Market stall", InteractCityMarketStall,
            () => _cityMissions.ActiveMission?.Id == "C02"
                ? "E: Check the market stock"
                : "E: Market stall");
        for (int i = 1; i <= 3; i++)
        {
            int cabinet = i;
            AddCityStation($"CityCabinetStation_{cabinet}", $"C03_Cabinet{cabinet}",
                $"E: Signal cabinet {cabinet}", () => InteractCityCabinet(cabinet),
                () => CityCabinetPrompt(cabinet));
        }
        AddCityStation("CityEngineerStation", "C03_Engineer", "E: Talk to the engineer",
            InteractCityEngineer, CityEngineerPrompt);
        RefreshCityJobVisuals();
    }

    private Interactable AddCityStation(string name, string markerId, string fallbackPrompt,
        Action action, Func<string> prompt)
    {
        var station = Interactable.Create(_root, _city.JobPoint(markerId),
            fallbackPrompt, action, null, true, 3.6f, name);
        station.SetPrompt(prompt);
        return station;
    }

    private string RoadworksPrompt()
    {
        if (_data.IsCompleted(RoadworksRules.MissionId))
            return "E: Roadworks — CITY ROAD OPEN";
        if (_data.ActiveMission == RoadworksRules.MissionId)
        {
            if (RoadworksRules.SuppliesDelivered(_data))
                return "E: Help the crew finish the roadworks";
            return RoadworksRules.HasAllSupplies(_data)
                ? "E: Deliver roadworks supplies"
                : "E: Roadworks supplies needed";
        }
        return _missions.GetStatus(RoadworksRules.MissionId) == MissionStatus.Available
            ? "E: Read the roadworks notice"
            : "CITY ROAD CLOSED — finish all 20 village jobs";
    }

    internal void InteractRoadworks()
    {
        if (_data.IsCompleted(RoadworksRules.MissionId))
        {
            _hud.ShowBigToast("The city road is open. Drive east to the CITY JOBS board.", 4f, Palette.Success);
            return;
        }
        if (_data.ActiveMission != RoadworksRules.MissionId)
        {
            if (_missions.GetStatus(RoadworksRules.MissionId) != MissionStatus.Available)
            {
                _hud.ShowInfo("CITY ROAD CLOSED",
                    "The roadworks crew is waiting for every village job to be finished. " +
                    "Complete all 20 required village jobs, then come back. Optional practice races are never required.");
                return;
            }
            var start = _missions.StartMission(RoadworksRules.MissionId);
            if (!start.Success)
            {
                _hud.ShowHint(start.Message);
                return;
            }
            _host.Save(SaveReason.MissionStart);
            _host.RefreshMission();
            _hud.ShowBigToast(start.Message + " Collect 3 cones, 2 barrier panels and 1 toolbox from the workshop.", 6f, Palette.Yellow);
            return;
        }
        if (!RoadworksRules.SuppliesDelivered(_data))
        {
            if (!RoadworksRules.HasAllSupplies(_data))
            {
                _hud.ShowHint(
                    $"The crew needs 3 cones, 2 barrier panels and 1 toolbox. " +
                    $"Delivered {RoadworksRules.SupplyProgress(_data)} of {RoadworksRules.SupplyTotal}. " +
                    "The marked pickups are beside the WORKSHOP.");
                return;
            }

            var delivered = _missions.DeliverRoadworksSupplies();
            if (!delivered.Success)
            {
                _hud.ShowHint(delivered.Message);
                return;
            }
            _host.RefreshMission();
            _host.Save(SaveReason.Autosave);
            _hud.ShowBigToast(delivered.Message, 4f, Palette.Orange);
            _audio?.PlaySfx("coin_collect", -7f);
            ShowRoadworksTask();
            return;
        }
        ShowRoadworksTask();
    }

    private void ShowRoadworksTask()
    {
        var body = _hud.OpenModal("Roadworks loading check", new Vector2(720, 520), Palette.Orange);
        UiKit.Label(body, "The crew has every delivery. Help them check the load.", 24, Palette.Navy);
        UiKit.Label(body, "3 safety cones + 2 barrier panels + 1 toolbox = ?", 21, Palette.Navy);
        int? selectedTotal = null;
        var order = new List<string>();
        var status = UiKit.Label(body, "", 18, Palette.Red);
        var orderLabel = UiKit.Label(body, "Load order: (not chosen)", 19, Palette.Blue);

        var totalRow = UiKit.HBox(body, 10);
        foreach (int value in new[] { 5, 6, 7 })
        {
            int captured = value;
            var button = UiKit.Button(totalRow, value.ToString(), 24, new Vector2(0, 52),
                Palette.Cream, Palette.Orange);
            button.Name = $"RoadworksTotal_{value}";
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Pressed += () =>
            {
                selectedTotal = captured;
                status.Text = $"You chose {captured}. Now choose the loading order.";
                status.AddThemeColorOverride("font_color", Palette.Blue);
            };
        }

        UiKit.Label(body, "Choose the order the crew should load the three kinds.", 19, Palette.Navy);
        var orderRow = UiKit.HBox(body, 8);
        foreach (string kind in new[] { "cones", "panels", "toolbox" })
        {
            string captured = kind;
            var button = UiKit.Button(orderRow, RoadworksRules.LabelFor(kind), 19,
                new Vector2(0, 48), Palette.Cream, Palette.Teal);
            button.Name = $"RoadworksOrder_{kind}";
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Pressed += () =>
            {
                if (!order.Contains(captured)) order.Add(captured);
                orderLabel.Text = "Load order: " + RoadworksRules.OrderLabel(order);
            };
        }

        var check = UiKit.Button(body, "Check with the crew", 22, new Vector2(0, 54),
            new Color("DFF6E3"), Palette.Success);
        check.Name = "RoadworksCheck";
        check.Pressed += () =>
        {
            if (!selectedTotal.HasValue)
            {
                status.Text = "Choose the total first: 3 + 2 + 1.";
                status.AddThemeColorOverride("font_color", Palette.Red);
                return;
            }
            var result = _missions.CompleteRoadworksTask(selectedTotal.Value, order);
            if (!result.Success)
            {
                status.Text = result.Message;
                status.AddThemeColorOverride("font_color", Palette.Red);
                return;
            }
            _hud.CloseModal();
            if (result.Completion != null)
                _host.CompleteVillageMission(result.Completion);
        };
    }

    private void OpenCityJobBoard()
    {
        if (!_host.Started) return;
        if (!_data.CityRoadOpen)
        {
            _hud.ShowInfo("CITY JOBS", "The city road is closed. Finish Roadworks Rescue in the village first.");
            return;
        }
        CityJobBoardUi.Show(_hud, _cityMissions, StartCityMissionFromBoard, TurnInCityJob);
    }

    private void StartCityMissionFromBoard(string missionId)
    {
        if (_missions.ActiveMission != null)
        {
            _hud.SetModalStatus("Finish or switch your village job first. City jobs are independent.", true);
            return;
        }
        var result = _cityMissions.StartMission(missionId);
        if (!result.Success)
        {
            _hud.SetModalStatus(result.Message, true);
            return;
        }
        _hud.CloseModal();
        _host.RefreshMission();
        RefreshCityJobVisuals();
        _host.Save(SaveReason.MissionStart);
        string hint = result.Mission.Hints.FirstOrDefault() ?? "Follow the glowing city marker.";
        _hud.ShowBigToast($"{result.Message} {hint}", 5.5f, Palette.Teal);
    }

    private void TurnInCityJob()
    {
        var result = _cityMissions.TurnInAtBoard();
        if (!result.Success)
        {
            _hud.SetModalStatus(result.Message, true);
            return;
        }
        _hud.CloseModal();
        OnCityMissionCompleted(result);
    }

    private void OnCityMissionCompleted(CityMissionCompletionResult result)
    {
        _host.RefreshMission();
        RefreshCityJobVisuals();
        string detail = result.Mission.Id switch
        {
            "C01" => "Parcels 1, 2 and 3 delivered in number order. The city depot is proud of you!",
            "C02" => "2 stalls × 3 crates = 6. Every market stall is stocked!",
            "C03" => "Red → amber → green, then the engineer's sign-off. City signals are safe again!",
            _ => result.Message,
        };
        _hud.ShowSuccess(result.Mission.Title, result.CoinsAwarded, detail, () => { });
        _audio?.PlaySfx("mission_complete");
        _audio?.PlaySfx("coin_collect", -7f);
        var save = _host.Save(SaveReason.MissionCompleted);
        if (!save.Success)
            _hud.SetModalStatus("The city job is complete in this session, but it could not be saved yet.", true);
    }

    private void ShowCityOutcome(CityProgressOutcome outcome)
    {
        if (outcome.Completed && outcome.Completion != null)
        {
            OnCityMissionCompleted(outcome.Completion);
            return;
        }
        if (outcome.Success)
        {
            _hud.ShowBigToast(outcome.Message, 3.8f, Palette.Teal);
            _audio?.PlaySfx("ui_confirm");
        }
        else
        {
            _hud.ShowHint(outcome.Message);
        }
        RefreshCityJobVisuals();
        _host.RefreshMission();
        _host.Save(SaveReason.Autosave);
    }

    private void InteractCityDepot() => ShowCityOutcome(_cityMissions.LoadCourierParcels());

    private void InteractCityStop(int stop) =>
        ShowCityOutcome(_cityMissions.DeliverCourierStop(stop));

    private void InteractCityCrate(int index) =>
        ShowCityOutcome(_cityMissions.CollectMarketCrate(index));

    private void InteractCityCabinet(int cabinet)
    {
        if (_cityMissions.ActiveMission?.Id != "C03")
        {
            _hud.ShowHint("Start Signals and Signs at the CITY JOBS board first.");
            return;
        }
        var body = _hud.OpenModal($"Signal cabinet {cabinet}", new Vector2(640, 390), Palette.Teal);
        UiKit.Label(body, "Which colour does this cabinet need?", 23, Palette.Navy);
        var status = UiKit.Label(body, "", 18, Palette.Red);
        var row = UiKit.HBox(body, 10);
        foreach (var (colour, label, swatch) in new[]
                 {
                     ("red", "RED", Palette.Red),
                     ("amber", "AMBER", Palette.Yellow),
                     ("green", "GREEN", Palette.Grass),
                 })
        {
            var button = UiKit.Button(row, label, 21, new Vector2(0, 52), Palette.Cream, swatch);
            button.Name = $"Cabinet_{colour}";
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            string captured = colour;
            button.Pressed += () =>
            {
                var outcome = _cityMissions.RepairCabinet(cabinet, captured);
                if (!outcome.Success)
                {
                    status.Text = outcome.Message;
                    return;
                }
                _hud.CloseModal();
                ShowCityOutcome(outcome);
            };
        }
    }

    private void InteractCityMarketStall()
    {
        if (_cityMissions.ActiveMission?.Id != "C02")
        {
            _hud.ShowHint("Start Market Stock-Up at the CITY JOBS board first.");
            return;
        }
        if (_cityMissions.C02Crates < 6)
        {
            _hud.ShowHint($"Collect all six crates first. You have {_cityMissions.C02Crates}.");
            return;
        }
        var body = _hud.OpenModal("Market stalls", new Vector2(640, 390), Palette.Teal);
        UiKit.Label(body, "2 stalls each need 3 crates. How many crates altogether?", 23, Palette.Navy);
        var status = UiKit.Label(body, "", 18, Palette.Red);
        var row = UiKit.HBox(body, 10);
        foreach (int value in new[] { 5, 6, 7 })
        {
            int captured = value;
            var button = UiKit.Button(row, value.ToString(), 24, new Vector2(0, 52),
                Palette.Cream, Palette.Teal);
            button.Name = $"MarketAnswer_{value}";
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Pressed += () =>
            {
                var outcome = _cityMissions.AnswerMarketStock(captured);
                if (!outcome.Success)
                {
                    status.Text = outcome.Message;
                    return;
                }
                _hud.CloseModal();
                ShowCityOutcome(outcome);
            };
        }
    }

    private void InteractCityEngineer()
    {
        var outcome = _cityMissions.TalkToEngineer();
        if (!outcome.Success)
        {
            _hud.ShowHint(outcome.Message);
            return;
        }
        if (outcome.Completed && outcome.Completion != null)
            OnCityMissionCompleted(outcome.Completion);
        else
            ShowCityOutcome(outcome);
    }

    private string CityDepotPrompt()
    {
        if (_cityMissions.ActiveMission?.Id != "C01") return "E: City depot";
        return _cityMissions.HasCourierParcels
            ? "E: City depot — parcels loaded"
            : "E: Load the three numbered parcels";
    }

    private string CityStopPrompt(int stop)
    {
        if (_cityMissions.ActiveMission?.Id != "C01") return $"E: Parcel stop {stop}";
        if (!_cityMissions.HasCourierParcels) return "E: Collect parcels at the depot first";
        int expected = _cityMissions.C01Stops + 1;
        if (stop < expected) return $"E: Parcel {stop} already delivered";
        return stop == expected
            ? $"E: Deliver parcel {stop}"
            : $"E: Parcel stop {stop} — parcel {expected} is next";
    }

    private string CityCratePrompt(int index)
    {
        if (_cityMissions.ActiveMission?.Id != "C02") return "E: Market stock crate";
        return _cityMissions.Progress("C02", $"crate_{index}") > 0
            ? "E: Crate already collected"
            : "E: Collect stock crate";
    }

    private string CityCabinetPrompt(int cabinet)
    {
        if (_cityMissions.ActiveMission?.Id != "C03") return $"E: Signal cabinet {cabinet}";
        if (_cityMissions.Progress("C03", $"cabinet_{cabinet}") > 0)
            return $"E: Signal cabinet {cabinet} — fixed";
        int expected = _cityMissions.C03Cabinets + 1;
        return cabinet == expected
            ? $"E: Repair cabinet {cabinet}"
            : $"E: Cabinet {expected} is next";
    }

    private string CityEngineerPrompt()
    {
        if (_cityMissions.ActiveMission?.Id != "C03") return "E: City engineer";
        return _cityMissions.C03Cabinets >= 3
            ? "E: Talk to the city engineer"
            : "E: Engineer — repair all three cabinets first";
    }

    internal void RefreshCityJobVisuals()
    {
        var parcels = _city.JobProp("C01_DepotParcels");
        if (parcels != null)
            parcels.Visible = !_data.CompletedCityMissions.Contains("C01") &&
                              !_cityMissions.HasCourierParcels;
        for (int i = 0; i < 6; i++)
        {
            var crate = _city.JobProp($"C02_Crate_{i}");
            if (crate != null)
                crate.Visible = _cityMissions.Progress("C02", $"crate_{i}") <= 0;
        }
        for (int i = 1; i <= 3; i++)
        {
            var cabinet = _city.JobProp($"C03_Cabinet{i}");
            var body = cabinet?.GetNodeOrNull<MeshInstance3D>("Body");
            if (body == null) continue;
            bool fixedCabinet = _cityMissions.Progress("C03", $"cabinet_{i}") > 0;
            body.MaterialOverride = fixedCabinet
                ? Geo.Mat(Palette.Grass)
                : Geo.Mat(Palette.Stone);
            if (cabinet.GetNodeOrNull<MeshInstance3D>("LensRed") is { } red)
                red.MaterialOverride = Geo.Mat(Palette.Red);
            if (cabinet.GetNodeOrNull<MeshInstance3D>("LensAmber") is { } amber)
                amber.MaterialOverride = Geo.Mat(Palette.Yellow);
            if (cabinet.GetNodeOrNull<MeshInstance3D>("LensGreen") is { } green)
                green.MaterialOverride = Geo.Mat(Palette.Grass);
        }
    }

    internal void ApplyCityPersistedState()
    {
        if (_data.IsCompleted(RoadworksRules.MissionId))
        {
            _data.CityRoadworksCompleted = true;
            _data.CityRoadworksRewardClaimed = true;
            _data.CityRoadOpen = true;
        }
        _roadworks.SetRoadState(_data.CityRoadOpen, false);
        _city.RestoreSimulation(_data.CitySeed, _data.CityLogicalSeconds);
        RefreshCityJobVisuals();
    }

    internal void ReconcileCityProgress()
    {
        if (_missions.ActiveMission?.Id == RoadworksRules.MissionId &&
            MissionRules.IsObjectiveComplete(_data, RoadworksRules.MissionId))
        {
            var result = _missions.CompleteActive();
            if (result.Success)
            {
                _data.CityRoadworksCompleted = true;
                _data.CityRoadworksRewardClaimed = true;
                _data.CityRoadOpen = true;
                _roadworks.SetRoadState(true, false);
                _host.NotifyRecovery($"Recovered a finished city road job: {result.Message}");
            }
        }
        if (_cityMissions.ActiveMission != null &&
            CityMissionService.ObjectiveComplete(_data, _cityMissions.ActiveMission.Id))
        {
            var result = _cityMissions.ReconcileActiveObjective();
            if (result.Success)
                _host.NotifyRecovery($"Recovered a finished city job: {result.Message}");
        }
    }

    internal void RefreshCityMissionState()
    {
        var mission = _cityMissions.ActiveMission;
        if (mission == null) return;
        _hud.SetMission(mission.Title, mission.Short, CityJobBoardUi.IconFor(mission.Id),
            CityProgressRows(mission.Id));

        switch (mission.Id)
        {
            case "C01":
                if (!_cityMissions.HasCourierParcels)
                    _host.SetObjective(_city.JobPoint("C01_Depot"), "CITY DEPOT", "delivery", Palette.Teal);
                else if (_cityMissions.C01Stops >= 3)
                    _host.SetObjective(_city.BoardApproach, "CITY JOBS", "star", Palette.Teal);
                else
                    _host.SetObjective(_city.JobPoint($"C01_Stop{_cityMissions.C01Stops + 1}"),
                        $"PARCEL {_cityMissions.C01Stops + 1}", "delivery", Palette.Teal);
                break;
            case "C02":
                if (_cityMissions.C02Crates < 6)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        if (_cityMissions.Progress("C02", $"crate_{i}") > 0) continue;
                        _host.SetObjective(_city.JobPoint($"C02_Crate_{i}"), "STOCK CRATE", "basket", Palette.Teal);
                        break;
                    }
                }
                else if (!_cityMissions.HasMarketAnswer)
                    _host.SetObjective(_city.JobPoint("C02_Stall"), "MARKET STALL", "market_maths", Palette.Teal);
                else
                    _host.SetObjective(_city.BoardApproach, "CITY JOBS", "star", Palette.Teal);
                break;
            case "C03":
                if (_cityMissions.C03Cabinets < 3)
                    _host.SetObjective(_city.JobPoint($"C03_Cabinet{_cityMissions.C03Cabinets + 1}"),
                        $"SIGNAL CABINET {_cityMissions.C03Cabinets + 1}", "road_signs", Palette.Teal);
                else
                    _host.SetObjective(_city.JobPoint("C03_Engineer"), "CITY ENGINEER", "neighbours", Palette.Teal);
                break;
        }
    }

    private List<(string Icon, string Text)> CityProgressRows(string id)
    {
        var rows = new List<(string, string)>();
        switch (id)
        {
            case "C01":
                rows.Add(("parcel", _cityMissions.HasCourierParcels
                    ? "3 parcels loaded"
                    : "Load parcels at the depot"));
                rows.Add(("star", $"Stops in order: {_cityMissions.C01Stops} / 3"));
                break;
            case "C02":
                rows.Add(("basket", $"Crates: {_cityMissions.C02Crates} / 6"));
                rows.Add(("star", _cityMissions.HasMarketAnswer ? "2 × 3 = 6 checked" : "Then check 2 × 3 at the stall"));
                break;
            case "C03":
                rows.Add(("road_signs", $"Cabinets fixed: {_cityMissions.C03Cabinets} / 3"));
                rows.Add(("star", "Order: RED → AMBER → GREEN"));
                break;
        }
        return rows;
    }

}
