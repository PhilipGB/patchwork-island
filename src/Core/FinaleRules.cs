using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

/// <summary>
/// Authored pure rules for the second half of chapter two (M16-M20). The
/// mission state machine and the Godot UI use these definitions so every
/// positive and negative path can be exercised without booting the game.
/// </summary>
public static class FinaleRules
{
    public static readonly string[] MissionIds = { "M16", "M17", "M18", "M19", "M20" };

    // ------------------------------------------------------------- M16 parcels
    public static readonly string[] DeliveryRecipients = { "ada", "pip", "ivy" };

    public static string DeliveryName(string npcId) => npcId switch
    {
        "ada" => "Ada",
        "pip" => "Pip",
        "ivy" => "Ivy",
        _ => npcId,
    };

    public static string DeliveredKey(string npcId) => $"delivered_{npcId}";
    public static string CarryingKey(string npcId) => $"carrying_{npcId}";

    public static bool M16Complete(GameData data) =>
        DeliveryRecipients.All(npc => data.GetMissionCounter("M16", DeliveredKey(npc)) >= 1);

    // --------------------------------------------------------------- M17 market
    public const int MarketBudget = 20;
    public const int MarketExpectedTotal = 14;
    public const int MarketExpectedChange = 6;

    public static readonly string[] MarketItems = { "bread", "milk", "apple" };

    public static readonly Dictionary<string, int> MarketPrices = new(StringComparer.Ordinal)
    {
        ["bread"] = 3,
        ["milk"] = 4,
        ["apple"] = 2,
    };

    public static readonly Dictionary<string, int> MarketRequired = new(StringComparer.Ordinal)
    {
        ["bread"] = 2,
        ["milk"] = 1,
        ["apple"] = 2,
    };

    public static int MarketPrice(string item) =>
        MarketPrices.TryGetValue(item, out int price) ? price : 0;

    public static int MarketCount(GameData data, string item) =>
        data.GetMissionCounter("M17", item);

    public static int MarketSpent(int bread, int milk, int apple) =>
        bread * MarketPrices["bread"] + milk * MarketPrices["milk"] + apple * MarketPrices["apple"];

    public static int MarketSpent(GameData data) =>
        MarketSpent(MarketCount(data, "bread"), MarketCount(data, "milk"), MarketCount(data, "apple"));

    public static int MarketChange(GameData data) => MarketBudget - MarketSpent(data);

    public static bool MarketListComplete(int bread, int milk, int apple) =>
        bread >= MarketRequired["bread"] && milk >= MarketRequired["milk"] &&
        apple >= MarketRequired["apple"];

    public static bool MarketListExact(int bread, int milk, int apple) =>
        bread == MarketRequired["bread"] && milk == MarketRequired["milk"] &&
        apple == MarketRequired["apple"];

    public static bool CanAddMarketItem(int bread, int milk, int apple, string item)
    {
        int price = MarketPrice(item);
        return price > 0 && MarketSpent(bread, milk, apple) + price <= MarketBudget;
    }

    public static bool M17Complete(GameData data) =>
        data.GetMissionCounter("M17", "finalised") >= 1;

    // -------------------------------------------------------------- M18 repair
    public const int RequiredTyres = 2;
    public const int RequiredLamps = 1;
    public const int RequiredBatteries = 1;

    public static readonly string[] RepairParts = { "tyre", "lamp", "battery" };

    public static int PartRequired(string part) => part switch
    {
        "tyre" => RequiredTyres,
        "lamp" => RequiredLamps,
        "battery" => RequiredBatteries,
        _ => 0,
    };

    public static string PartLabel(string part) => part switch
    {
        "tyre" => "Tyre",
        "lamp" => "Lamp",
        "battery" => "Battery",
        _ => part,
    };

    /// <summary>Stable pickup IDs for the four M18 parts in spawn order.</summary>
    public static readonly string[] RepairPickupIds =
    {
        "part_tyre_0", "part_tyre_1", "part_lamp_0", "part_battery_0",
    };

    /// <summary>Part type carried by a physical M18 pickup, or "" if unknown.</summary>
    public static string RepairPickupPart(string pickupId) => pickupId switch
    {
        "part_tyre_0" or "part_tyre_1" => "tyre",
        "part_lamp_0" => "lamp",
        "part_battery_0" => "battery",
        _ => "",
    };

    public static int HasPart(GameData data, string part) =>
        data.GetMissionCounter("M18", "has_" + part);

    public static bool M18Complete(GameData data) =>
        data.GetMissionCounter("M18", "battery_fitted") >= 1;

    public static bool CanFitPart(GameData data, string part, out string message)
    {
        message = "";
        if (M18Complete(data))
        {
            message = "Pip's car is already repaired.";
            return false;
        }
        if (data.GetMissionCounter("M18", "inspected") <= 0)
        {
            message = "Inspect Pip's car before fitting anything.";
            return false;
        }
        if (part == "tyre")
        {
            if (data.GetMissionCounter("M18", "lamp_fitted") > 0 ||
                data.GetMissionCounter("M18", "battery_fitted") > 0)
            {
                message = "The tyres belong before the lamp and battery.";
                return false;
            }
            if (data.GetMissionCounter("M18", "tyres_fitted") >= RequiredTyres)
            {
                message = "Both tyres are already fitted.";
                return false;
            }
            if (HasPart(data, "tyre") <= 0)
            {
                message = "Find a tyre in the workshop tool area first.";
                return false;
            }
            return true;
        }
        if (part == "lamp")
        {
            if (data.GetMissionCounter("M18", "tyres_fitted") < RequiredTyres)
            {
                message = "Fit both tyres before the lamp.";
                return false;
            }
            if (data.GetMissionCounter("M18", "lamp_fitted") > 0)
            {
                message = "The lamp is already fitted.";
                return false;
            }
            if (data.GetMissionCounter("M18", "battery_fitted") > 0)
            {
                message = "The lamp belongs before the battery.";
                return false;
            }
            if (HasPart(data, "lamp") <= 0)
            {
                message = "Find the lamp in the workshop tool area first.";
                return false;
            }
            return true;
        }
        if (part == "battery")
        {
            if (data.GetMissionCounter("M18", "lamp_fitted") <= 0)
            {
                message = "Fit the lamp before the battery.";
                return false;
            }
            if (data.GetMissionCounter("M18", "battery_fitted") > 0)
            {
                message = "The battery is already fitted.";
                return false;
            }
            if (HasPart(data, "battery") <= 0)
            {
                message = "Find the battery in the workshop tool area first.";
                return false;
            }
            return true;
        }
        message = "Choose a tyre, lamp or battery.";
        return false;
    }

    // --------------------------------------------------------------- M19 rally
    public const int RallyGateCount = 6;
    public const double RallyBonusSeconds = 155.0;

    public static bool M19Complete(GameData data) =>
        data.GetMissionCounter("M19", "checkpoint") >= RallyGateCount;

    // ------------------------------------------------------------- M20 festival
    public static readonly string[] FestivalInvitees = { "ada", "pip", "ivy" };
    public const int FestivalTableCount = 2;
    public const int FestivalLanternCount = 3;

    public static string InvitedKey(string npcId) => $"invited_{npcId}";
    public static string TableKey(int index) => $"table_{index}";
    public static string LanternKey(int index) => $"lantern_{index}";

    public static bool InvitesComplete(GameData data) =>
        FestivalInvitees.All(npc => data.GetMissionCounter("M20", InvitedKey(npc)) >= 1);

    public static bool TablesComplete(GameData data) =>
        Enumerable.Range(0, FestivalTableCount)
            .All(i => data.GetMissionCounter("M20", TableKey(i)) >= 1);

    public static bool LanternsComplete(GameData data) =>
        Enumerable.Range(0, FestivalLanternCount)
            .All(i => data.GetMissionCounter("M20", LanternKey(i)) >= 1);

    public static bool M20Complete(GameData data) =>
        InvitesComplete(data) && TablesComplete(data) && LanternsComplete(data);
}
