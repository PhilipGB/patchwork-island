using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>Village counters and build-site UI; builder references remain live across New Game.</summary>
internal sealed class VillageStationController
{
    private readonly GameSession _session;
    private readonly Hud _hud;
    private readonly IGameplayHost _host;
    private readonly Func<BridgeBuilder> _bridge;
    private readonly Func<SwingBuilder> _swings;
    private readonly Func<ParkBuilder> _park;
    private readonly Func<FurnitureBuilder> _furniture;
    private GameData _data => _session.Data;
    private MissionService _missions => _session.Village;
    private BridgeBuilder _bridgeBuilder => _bridge();
    private SwingBuilder _swingBuilder => _swings();
    private ParkBuilder _parkBuilder => _park();
    private FurnitureBuilder _furnitureBuilder => _furniture();
    internal VillageStationController(GameSession session, Hud hud, IGameplayHost host,
        Func<BridgeBuilder> bridge, Func<SwingBuilder> swings,
        Func<ParkBuilder> park, Func<FurnitureBuilder> furniture)
    {
        _session = session; _hud = hud; _host = host;
        _bridge = bridge; _swings = swings; _park = park; _furniture = furniture;
    }
    internal string RedHousePrompt()
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

    internal string WorkshopPrompt()
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

    internal string ShopPrompt()
    {
        if (_data.ActiveMission == "M04") return "E: Shop for Market Basket";
        if (_data.ActiveMission == "M17") return "E: Market Day Maths checkout";
        if (_data.IsCompleted("M17")) return "E: Market stall — stocked";
        return "E: Look at the SHOP";
    }

    internal string ClothesShopPrompt()
    {
        string wearing = CharacterCustomisation.Equipped(_data)?.Name ?? "Everyday";
        return $"E: CLOTHES SHOP — wearing {wearing}";
    }

    internal string TownHallPrompt()
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

    internal string PlaygroundPrompt()
    {
        if (_data.IsCompleted("M07")) return "E: Playground — swings fixed";
        if (_data.ActiveMission == "M07") return "E: Fix the swings";
        return "E: Playground worksite";
    }

    internal string ParkPrompt()
    {
        if (_data.IsCompleted("M08")) return "E: Village green — decorated";
        if (_data.ActiveMission == "M08") return "E: Decorate the village green";
        return "E: Village green";
    }

    internal string FurniturePrompt()
    {
        if (_data.IsCompleted("M10")) return "E: Rearrange furniture";
        if (_data.ActiveMission == "M10") return "E: Furnish your home";
        return "E: Furniture catalogue";
    }

    internal string BridgeSitePrompt()
    {
        if (_bridgeBuilder != null && _bridgeBuilder.IsBuilt) return "E: Bridge is open";
        if (_data.ActiveMission == "M03") return "E: Build the bridge";
        return "E: Bridge worksite";
    }

    internal void InteractRedHouse()
    {
        if (_data.ActiveMission == "M01")
        {
            var result = _missions.DeliverApples();
            if (result.Success)
                _host.CompleteVillageMission(result);
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

    internal void InteractWorkshop()
    {
        if (_data.ActiveMission == "M02")
        {
            var result = _missions.SubmitSupplies();
            if (result.Success)
                _host.CompleteVillageMission(result);
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

    internal void FinishPostIfReady()
    {
        if (MissionRules.IsObjectiveComplete(_data, "M06"))
        {
            var completion = _missions.CompleteActive(0);
            if (completion.Success) _host.CompleteVillageMission(completion);
        }
        else
        {
            _host.RefreshMission();
        }
    }

    internal void InteractHouseDoor()
    {
        if (_data.ActiveMission == "M10")
            _hud.ShowBigToast("Walk inside. The furniture catalogue is on the little stand.", 3.2f, Palette.Lavender);
        else if (_data.IsCompleted("M10"))
            _hud.ShowBigToast("Your cosy, furnished home.", 2.6f, Palette.Success);
        else
            _hud.ShowBigToast("Your cosy house. Furniture jobs open later.", 2.6f);
    }

    internal void InteractPlayground()
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

    internal void InteractPark()
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

    internal void InteractFurniture()
    {
        if (_data.ActiveMission == "M10")
        {
            FurnitureShopUi.Show(_hud, _data, () => _furnitureBuilder?.OpenBuildMode(), _host.RefreshMission);
            return;
        }
        if (_data.IsCompleted("M10"))
        {
            _furnitureBuilder?.OpenBuildMode();
            return;
        }
        _hud.ShowHint("Start Home Sweet Home at the yellow JOBS board after Park Planner.");
    }

    internal void InteractShop()
    {
        if (_data.ActiveMission == "M04")
        {
            GroceryUi.Show(_hud, _missions, _host.RefreshMission);
            return;
        }
        if (_data.ActiveMission == "M17")
        {
            MarketMathsUi.Show(_hud, _missions.Finale, result => _host.CompleteVillageMission(result), _host.RefreshMission);
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

    internal void InteractClothesShop()
    {
        ClothesShopUi.Show(_hud, _data, PersistCustomisation);
    }

    /// <summary>The shop calls this after a purchase, outfit change or preset change.</summary>
    internal SaveResult PersistCustomisation()
    {
        _host.RefreshMission();
        return _host.Save(SaveReason.Customisation);
    }

    internal void InteractTownHall()
    {
        if (_data.ActiveMission == "M04")
        {
            var result = _missions.DeliverGroceries();
            if (result.Success) _host.CompleteVillageMission(result);
            else _hud.ShowHint(result.Message);
            return;
        }
        if (_data.IsCompleted("M04"))
            _hud.ShowBigToast("The Town Hall has its bread and milk. Thank you!");
        else
            _hud.ShowInfo("TOWN HALL", "The Town Hall clock keeps the village on time. Groceries and post arrive here.");
    }

    internal void InteractBridgeSite()
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

}
