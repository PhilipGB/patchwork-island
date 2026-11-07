using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

/// <summary>Pure objective completion rules for all ten missions.</summary>
public static class MissionRules
{
    public static bool PrerequisitesMet(GameData data, MissionDefinition mission)
    {
        if (mission?.Prerequisites == null) return true;
        foreach (var prerequisite in mission.Prerequisites)
            if (!data.IsCompleted(prerequisite))
                return false;
        return true;
    }

    public static bool IsObjectiveComplete(GameData data, string missionId)
    {
        return missionId switch
        {
            "M01" => data.GetMissionCounter("M01", "delivered") >= 1,
            "M02" => data.GetMissionCounter("M02", "delivered") >= 1,
            "M03" => data.BridgePieces.Distinct().Count() >= 7,
            "M04" => data.GetMissionCounter("M04", "delivered") >= 1,
            "M05" => data.GetMissionCounter("M05", "checkpoint") >= 4,
            "M06" => data.GetMissionCounter("M06", "blueDelivered") >= 2 &&
                     data.GetMissionCounter("M06", "orangeDelivered") >= 1,
            "M07" => data.SwingPieces.Distinct().Count() >= 6,
            "M08" => HasParkPieces(data),
            "M09" => data.GetMissionCounter("M09", "checkpoint") >= 4,
            "M10" => HasHouseFurniture(data),
            _ => false,
        };
    }

    public static bool HasParkPieces(GameData data)
    {
        var types = new HashSet<string>(data.ParkPlacements.Select(p => p.Type));
        return types.Contains("bench") && types.Contains("flowerbed") && types.Contains("lamp");
    }

    public static bool HasHouseFurniture(GameData data)
    {
        int tables = data.FurniturePlacements.Count(p => p.Type == "table");
        int chairs = data.FurniturePlacements.Count(p => p.Type == "chair");
        int plants = data.FurniturePlacements.Count(p => p.Type == "plant");
        return tables >= 1 && chairs >= 2 && plants >= 1;
    }

    public static bool CanDeliverApples(GameData data) => data.GetItem("apple") >= 3;

    public static bool CanSubmitSupplies(GameData data) =>
        data.GetItem("wood") >= 5 && data.GetItem("stone") >= 3;

    /// <summary>Optional race goal; never required for mission completion.</summary>
    public static int OptionalRaceBonus(string missionId, float elapsedSeconds)
    {
        return missionId switch
        {
            "M05" when elapsedSeconds <= 90f => 5,
            "M09" when elapsedSeconds <= 75f => 5,
            _ => 0,
        };
    }
}

public enum MissionStatus
{
    Locked,
    Available,
    Active,
    Completed,
}

public sealed class MissionStartResult
{
    public bool Success;
    public string Message = "";
    public MissionDefinition Mission;
}

public sealed class PickupResult
{
    public bool Success;
    public string Message = "";
    public int Count;
    public int Required;
}

public sealed class MissionCompletionResult
{
    public bool Success;
    public string Message = "";
    public MissionDefinition Mission;
    public int CoinsAwarded;
    public int BonusCoins;
    public bool AllComplete;

    public int TotalCoins => CoinsAwarded + BonusCoins;
}

public sealed class HintResult
{
    public string Text = "";
    public int Level = 1;
    public int TotalLevels = 1;
}

public sealed class ActionOutcome
{
    public bool Success;
    public string Message = "";
}

/// <summary>
/// The mission state machine. It owns exactly-once rewards and prerequisites,
/// but stays free of Godot so it is directly unit-testable.
/// </summary>
public sealed class MissionService
{
    public GameData Data { get; }
    public MissionCatalog Catalog { get; }

    public MissionService(GameData data, MissionCatalog catalog)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Data.Normalise();
    }

    public MissionDefinition ActiveMission =>
        string.IsNullOrEmpty(Data.ActiveMission) ? null : Catalog.Get(Data.ActiveMission);

    public MissionStatus GetStatus(string missionId)
    {
        var mission = Catalog.Get(missionId);
        if (mission == null) return MissionStatus.Locked;
        if (Data.IsCompleted(missionId)) return MissionStatus.Completed;
        if (Data.ActiveMission == missionId) return MissionStatus.Active;
        return MissionRules.PrerequisitesMet(Data, mission) ? MissionStatus.Available : MissionStatus.Locked;
    }

    public bool CanStart(string missionId) => GetStatus(missionId) == MissionStatus.Available;

    public MissionStartResult StartMission(string missionId)
    {
        var mission = Catalog.Get(missionId);
        if (mission == null)
            return new MissionStartResult { Success = false, Message = "That job is not on the board.", Mission = null };
        if (Data.IsCompleted(missionId))
            return new MissionStartResult { Success = false, Message = "That job is already finished.", Mission = mission };
        if (!MissionRules.PrerequisitesMet(Data, mission))
        {
            string needed = string.Join(" and ", mission.Prerequisites.Select(p => $"{p} {Catalog.Get(p)?.Title ?? ""}".Trim()));
            return new MissionStartResult
            {
                Success = false,
                Message = $"Finish {needed} first.",
                Mission = mission,
            };
        }

        bool switching = !string.IsNullOrEmpty(Data.ActiveMission) && Data.ActiveMission != missionId;
        Data.ActiveMission = missionId;
        string switchNote = switching ? " Your other job keeps its progress." : "";
        return new MissionStartResult
        {
            Success = true,
            Mission = mission,
            Message = $"{mission.Title} started.{switchNote}",
        };
    }

    public PickupResult CollectPickup(string pickupId, string item, string missionId, int required)
    {
        if (Data.IsCompleted(missionId))
            return new PickupResult { Success = false, Message = "That job is already finished." };
        if (Data.ActiveMission != missionId)
            return new PickupResult
            {
                Success = false,
                Message = $"Start {Catalog.Get(missionId)?.Title ?? missionId} at the JOBS board first.",
            };
        if (Data.HasCollected(pickupId))
            return new PickupResult { Success = false, Message = "Already collected.", Count = Data.GetItem(item), Required = required };
        int current = Data.GetItem(item);
        if (current >= required)
            return new PickupResult { Success = false, Message = "You have enough of those.", Count = current, Required = required };

        Data.CollectedPickups.Add(pickupId);
        Data.AddItem(item, 1);
        int now = Data.GetItem(item);
        return new PickupResult
        {
            Success = true,
            Count = now,
            Required = required,
            Message = $"{ItemName(item)} {now} of {required}",
        };
    }

    public MissionCompletionResult DeliverApples()
    {
        if (Data.ActiveMission != "M01")
            return Fail("Start The Red House Delivery at the job board first.");
        if (!MissionRules.CanDeliverApples(Data))
            return Fail($"RED HOUSE needs 3 apples. You have {Data.GetItem("apple")}.");
        Data.RemoveItem("apple", 3);
        Data.SetMissionCounter("M01", "delivered", 1);
        return CompleteActive(0);
    }

    public MissionCompletionResult SubmitSupplies()
    {
        if (Data.ActiveMission != "M02")
            return Fail("Start Workshop Supplies at the job board first.");
        if (!MissionRules.CanSubmitSupplies(Data))
            return Fail($"The workshop needs 5 wood and 3 stone. You have {Data.GetItem("wood")} wood and {Data.GetItem("stone")} stone.");
        Data.RemoveItem("wood", 5);
        Data.RemoveItem("stone", 3);
        Data.SetMissionCounter("M02", "delivered", 1);
        return CompleteActive(0);
    }

    /// <summary>Awards a mission exactly once; safe to call repeatedly.</summary>
    // ------------------------------------------------------------ groceries

    public int GroceryBread => Data.GetMissionCounter("M04", "bread");
    public int GroceryMilk => Data.GetMissionCounter("M04", "milk");
    public int GrocerySpent => BudgetRules.GroceryTotal(GroceryBread, GroceryMilk);
    public int GroceryChange => BudgetRules.GroceryChange(GroceryBread, GroceryMilk);

    public ActionOutcome AddGrocery(string item)
    {
        if (Data.ActiveMission != "M04")
            return new ActionOutcome { Success = false, Message = "Start Market Basket at the JOBS board first." };
        if (Data.IsCompleted("M04"))
            return new ActionOutcome { Success = false, Message = "The basket is already delivered." };
        if (!BudgetRules.CanAddGrocery(GroceryBread, GroceryMilk, item))
            return new ActionOutcome { Success = false, Message = "That would use more than the 12 job coins." };

        if (item == "bread") Data.IncrementMissionCounter("M04", "bread");
        else if (item == "milk") Data.IncrementMissionCounter("M04", "milk");
        else return new ActionOutcome { Success = false, Message = "The shop does not sell that." };

        return new ActionOutcome
        {
            Success = true,
            Message = $"{ItemName(item)} added. Total {GrocerySpent}, change {GroceryChange}.",
        };
    }

    public ActionOutcome RemoveGrocery(string item)
    {
        if (Data.ActiveMission != "M04")
            return new ActionOutcome { Success = false, Message = "No grocery job is active." };
        if (item == "bread" && GroceryBread > 0) Data.SetMissionCounter("M04", "bread", GroceryBread - 1);
        else if (item == "milk" && GroceryMilk > 0) Data.SetMissionCounter("M04", "milk", GroceryMilk - 1);
        else return new ActionOutcome { Success = false, Message = "Nothing to put back." };
        return new ActionOutcome
        {
            Success = true,
            Message = $"Refunded. Total {GrocerySpent}, change {GroceryChange}.",
        };
    }

    public MissionCompletionResult DeliverGroceries()
    {
        if (Data.ActiveMission != "M04")
            return Fail("Start Market Basket at the JOBS board first.");
        if (!BudgetRules.IsGroceryListComplete(GroceryBread, GroceryMilk))
            return Fail($"The list needs 2 bread and 1 milk. You have {GroceryBread} bread and {GroceryMilk} milk.");
        Data.SetMissionCounter("M04", "delivered", 1);
        return CompleteActive(0);
    }

    // ---------------------------------------------------------- checkpoint race

    public ActionOutcome AdvanceCheckpoint(int gateNumber)
    {
        if (Data.ActiveMission != "M05" && Data.ActiveMission != "M09")
            return new ActionOutcome { Success = false, Message = "No race job is active." };
        string id = Data.ActiveMission;
        int current = Data.GetMissionCounter(id, "checkpoint");
        if (current >= 4)
            return new ActionOutcome { Success = false, Message = "The race is already finished." };
        if (gateNumber != current + 1)
            return new ActionOutcome { Success = false, Message = $"Gate {current + 1} is next." };

        Data.SetMissionCounter(id, "checkpoint", gateNumber);
        return new ActionOutcome
        {
            Success = true,
            Message = gateNumber == 4 ? "Finish gate reached!" : $"Gate {gateNumber}! Now gate {gateNumber + 1}.",
        };
    }

    // ------------------------------------------------------------- two-stop post

    public ActionOutcome DeliverBlueParcels()
    {
        if (Data.ActiveMission != "M06")
            return new ActionOutcome { Success = false, Message = "Start Two-Stop Post at the JOBS board first." };
        int delivered = Data.GetMissionCounter("M06", "blueDelivered");
        if (delivered >= 2)
            return new ActionOutcome { Success = false, Message = "The RED HOUSE already has both blue parcels." };
        int carried = Data.GetItem("blue_parcel");
        if (carried <= 0)
            return new ActionOutcome { Success = false, Message = "Pick up the BLUE parcels at the post counter first." };
        int amount = Math.Min(carried, 2 - delivered);
        Data.RemoveItem("blue_parcel", amount);
        Data.SetMissionCounter("M06", "blueDelivered", delivered + amount);
        return new ActionOutcome
        {
            Success = true,
            Message = $"{amount} BLUE parcel{(amount == 1 ? "" : "s")} delivered to the RED HOUSE.",
        };
    }

    public ActionOutcome DeliverOrangeParcel()
    {
        if (Data.ActiveMission != "M06")
            return new ActionOutcome { Success = false, Message = "Start Two-Stop Post at the JOBS board first." };
        if (Data.GetMissionCounter("M06", "orangeDelivered") >= 1)
            return new ActionOutcome { Success = false, Message = "The WORKSHOP already has its orange parcel." };
        if (Data.GetItem("orange_parcel") <= 0)
            return new ActionOutcome { Success = false, Message = "Pick up the ORANGE parcel at the post counter first." };
        Data.RemoveItem("orange_parcel", 1);
        Data.SetMissionCounter("M06", "orangeDelivered", 1);
        return new ActionOutcome { Success = true, Message = "ORANGE parcel delivered to the WORKSHOP." };
    }

    // --------------------------------------------------------------- bridge

    public int BridgePlacedCount => Data.BridgePieces.Distinct().Count();

    public PickupResult PlaceBridgePiece(string pieceType, string socketId)
    {
        if (Data.ActiveMission != "M03")
            return new PickupResult { Success = false, Message = "Start Build the Bridge at the JOBS board first." };
        if (Data.IsCompleted("M03"))
            return new PickupResult { Success = false, Message = "The bridge is already built." };
        if (!BuildRules.ValidateBridgePlacement(pieceType, socketId, Data.BridgePieces, out string message))
            return new PickupResult { Success = false, Message = message, Count = BridgePlacedCount, Required = 7 };

        Data.BridgePieces.Add(socketId);
        return new PickupResult
        {
            Success = true,
            Count = BridgePlacedCount,
            Required = 7,
            Message = pieceType == "support"
                ? $"Support {BuildRules.BridgeSupportCount(Data.BridgePieces)} of 3 placed."
                : $"Plank {BuildRules.BridgePlankCount(Data.BridgePieces)} of 4 placed.",
        };
    }

    public MissionCompletionResult CompleteActive(int bonusCoins = 0)
    {
        if (string.IsNullOrEmpty(Data.ActiveMission))
            return Fail("No active job.");
        var mission = Catalog.Get(Data.ActiveMission);
        if (mission == null)
            return Fail("That job is missing from the content.");
        if (Data.IsCompleted(mission.Id))
            return Fail("That job is already finished.");
        if (!MissionRules.IsObjectiveComplete(Data, mission.Id))
            return Fail("This job is not finished yet.");

        Data.CompletedMissions.Add(mission.Id);
        int total = mission.RewardCoins + bonusCoins;
        Data.Coins += total;
        string completedId = mission.Id;
        Data.ActiveMission = "";

        if (completedId == "M01")
            UnlockCosmetic("red_cap");

        return new MissionCompletionResult
        {
            Success = true,
            Mission = mission,
            CoinsAwarded = mission.RewardCoins,
            BonusCoins = bonusCoins,
            AllComplete = Data.CompletedMissions.Distinct().Count() >= Catalog.Missions.Count,
            Message = $"{mission.Title} complete! +{total} coins.",
        };
    }

    public void UnlockCosmetic(string id)
    {
        if (!Data.UnlockedCosmetics.Contains(id))
            Data.UnlockedCosmetics.Add(id);
    }

    public HintResult GetNextHint(string missionId)
    {
        var mission = Catalog.Get(missionId);
        if (mission == null || mission.Hints == null || mission.Hints.Count == 0)
            return new HintResult { Text = "Follow the glowing marker.", Level = 1, TotalLevels = 1 };
        int level = Math.Clamp(Data.GetHintLevel(missionId), 0, mission.Hints.Count - 1);
        string text = mission.Hints[level];
        if (level < mission.Hints.Count - 1)
            Data.SetHintLevel(missionId, level + 1);
        return new HintResult { Text = text, Level = level + 1, TotalLevels = mission.Hints.Count };
    }

    private MissionCompletionResult Fail(string message) => new() { Success = false, Message = message };

    public static string ItemName(string item) => item switch
    {
        "apple" => "Apple",
        "wood" => "Wood",
        "stone" => "Stone",
        "bread" => "Bread",
        "milk" => "Milk",
        "blue_parcel" => "Blue parcel",
        "orange_parcel" => "Orange parcel",
        "rope" => "Rope",
        "seat" => "Seat",
        _ => item,
    };
}
