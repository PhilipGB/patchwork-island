using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Presents a village mission snapshot. Reads the live session facade on each
/// refresh so loading a save never leaves the HUD bound to obsolete services.
/// </summary>
public sealed class MissionHudPresenter
{
    private readonly GameSession _session;
    private readonly Hud _hud;
    private readonly WorldModel _world;
    private readonly PlayerController _player;
    private readonly VanController _van;
    private readonly TownCityRoadworks _roadworks;
    private readonly IReadOnlyDictionary<string, NpcActor> _npcById;
    private readonly IReadOnlyList<Pickup> _pickups;
    private readonly Func<bool> _isDriving;
    private readonly Func<RaceTimer> _timer;
    private readonly Func<Vector3, bool> _insideHouse;
    private readonly Action<Vector3, string, string, Color> _objective;
    private GameData _data => _session.Data;
    private MissionService _missions => _session.Village;
    private bool _driving => _isDriving();
    private RaceTimer _raceTimer => _timer();

    public MissionHudPresenter(GameSession session, Hud hud, WorldModel world,
        PlayerController player, VanController van, TownCityRoadworks roadworks,
        IReadOnlyDictionary<string, NpcActor> npcs, IReadOnlyList<Pickup> pickups,
        Func<bool> isDriving, Func<RaceTimer> timer, Func<Vector3, bool> insideHouse,
        Action<Vector3, string, string, Color> objective)
    {
        _session = session; _hud = hud; _world = world; _player = player;
        _van = van; _roadworks = roadworks; _npcById = npcs; _pickups = pickups;
        _isDriving = isDriving; _timer = timer; _insideHouse = insideHouse;
        _objective = objective;
    }
    private bool IsInsidePlayerHouse(Vector3 position) => _insideHouse(position);
    private void SetObjective(Vector3 position, string label, string icon, Color color) =>
        _objective(position, label, icon, color);

    public void Show(MissionDefinition mission)
    {
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
            case "M21":
                {
                    if (RoadworksRules.SuppliesDelivered(_data))
                    {
                        SetObjective(_roadworks.InteractionPosition, "ROADWORKS CREW", "road_signs", Palette.Orange);
                    }
                    else if (RoadworksRules.HasAllSupplies(_data))
                    {
                        SetObjective(_roadworks.InteractionPosition, "ROADWORKS DELIVERY", "road_signs", Palette.Yellow);
                    }
                    else
                    {
                        SetObjective(_world.WorkshopDoor + new Vector3(0, 0, 8f),
                            "ROADWORKS SUPPLIES", "road_signs", Palette.Orange);
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
                rows.Add(("star", $"Time {RaceTimer.Format(_raceTimer.ElapsedSeconds)} · bonus under {RaceTimer.Format(missionId == "M09" ? 75 : 90)}"));
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
                ? (_van.IsParkedNear(_player.GlobalPosition) ? "Van parked — step out to deliver" : "Drive close, stop, then step out")
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
                rows.Add(("star", $"Time {RaceTimer.Format(_raceTimer.ElapsedSeconds)} · medal under 2:35"));
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
        else if (missionId == RoadworksRules.MissionId)
        {
            rows.Add(("road_signs", RoadworksRules.SuppliesDelivered(_data)
                ? "Supplies delivered 6 / 6"
                : $"Supplies {RoadworksRules.SupplyProgress(_data)} / {RoadworksRules.SupplyTotal}"));
            rows.Add(("star", _data.GetItem("safety_cone") + " cones · " +
                               _data.GetItem("barrier_panel") + " panels · " +
                               _data.GetItem("toolbox") + " toolbox"));
            rows.Add(("star", RoadworksRules.SuppliesDelivered(_data)
                ? "Now finish the 3 + 2 + 1 loading check"
                : RoadworksRules.HasAllSupplies(_data)
                    ? "Now drive to the closed CITY ROAD gate"
                    : "Deliver all six items to the closed gate"));
        }
        else
        {
            rows.Add(("star", "Follow the glowing marker"));
        }
        return rows;
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

}
