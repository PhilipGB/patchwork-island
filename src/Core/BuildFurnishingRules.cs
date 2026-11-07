using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

/// <summary>M08 park budget and pad placement rules.</summary>
public static class ParkRules
{
    public static readonly string[] Slots = { "park0", "park1", "park2", "park3", "park4", "park5" };
    public static readonly string[] Items = { "bench", "flowerbed", "lamp" };

    public static int Count(GameData data, string item) =>
        data.ParkPlacements.Count(p => p.Type == item);

    public static int Spent(GameData data) =>
        BudgetRules.ParkTotal(Count(data, "bench"), Count(data, "flowerbed"), Count(data, "lamp"));

    public static int Left(GameData data) => Math.Max(0, BudgetRules.ParkBudget - Spent(data));

    public static ActionOutcome Place(GameData data, string item, string slot)
    {
        if (!Items.Contains(item))
            return new ActionOutcome { Success = false, Message = "Choose a bench, flowers or a lamp." };
        if (!Slots.Contains(slot))
            return new ActionOutcome { Success = false, Message = "Place the item on a green pad." };
        if (data.ParkPlacements.Any(p => p.Slot == slot))
            return new ActionOutcome { Success = false, Message = "That pad already has something on it." };
        int price = BudgetRules.ParkPrice(item);
        if (Spent(data) + price > BudgetRules.ParkBudget)
            return new ActionOutcome { Success = false, Message = "That would use more than the 20-coin park budget." };
        data.ParkPlacements.Add(new PlacedItem(item, slot));
        return new ActionOutcome { Success = true, Message = $"{Cap(item)} placed. {Left(data)} job coins left." };
    }

    public static PlacedItem UndoLast(GameData data)
    {
        if (data.ParkPlacements.Count == 0) return null;
        var last = data.ParkPlacements[^1];
        data.ParkPlacements.RemoveAt(data.ParkPlacements.Count - 1);
        return last;
    }

    public static bool IsComplete(GameData data) => MissionRules.HasParkPieces(data);

    public static string Cap(string item) => item switch
    {
        "bench" => "Bench",
        "flowerbed" => "Flowerbed",
        "lamp" => "Lamp",
        _ => item,
    };
}

/// <summary>M10 furniture purchasing, free fallback and house pad rules.</summary>
public static class FurnitureRules
{
    public static readonly string[] Slots = { "house0", "house1", "house2", "house3", "house4", "house5" };
    public static readonly string[] Items = { "table", "chair", "plant" };
    public static readonly Dictionary<string, int> Required = new()
    {
        ["table"] = 1,
        ["chair"] = 2,
        ["plant"] = 1,
    };

    public static int Owned(GameData data, string item) =>
        data.FurnitureOwned.TryGetValue(item, out int value) ? value : 0;

    public static int Placed(GameData data, string item) =>
        data.FurniturePlacements.Count(p => p.Type == item);

    public static int Available(GameData data, string item) =>
        Math.Max(0, Owned(data, item) - Placed(data, item));

    public static int MissingCost(GameData data)
    {
        int cost = 0;
        foreach (var (item, required) in Required)
        {
            int missing = Math.Max(0, required - Owned(data, item));
            cost += missing * BudgetRules.FurniturePrice(item);
        }
        return cost;
    }

    public static int FullSetCost => BudgetRules.FurnitureTotal(1, 2, 1);

    public static ActionOutcome Buy(GameData data, string item)
    {
        if (!Items.Contains(item))
            return new ActionOutcome { Success = false, Message = "The catalogue does not have that." };
        int price = BudgetRules.FurniturePrice(item);
        if (data.Coins < price)
            return new ActionOutcome { Success = false, Message = $"That costs {price} coins. You have {data.Coins}." };
        data.Coins -= price;
        data.FurnitureOwned[item] = Owned(data, item) + 1;
        return new ActionOutcome { Success = true, Message = $"{Label(item)} bought for {price} coins." };
    }

    public static ActionOutcome GrantFreeStarterSet(GameData data)
    {
        int added = 0;
        foreach (var (item, required) in Required)
        {
            int missing = Math.Max(0, required - Owned(data, item));
            if (missing <= 0) continue;
            data.FurnitureOwned[item] = Owned(data, item) + missing;
            added += missing;
        }
        return added > 0
            ? new ActionOutcome { Success = true, Message = $"Free starter furniture added: {added} piece(s)." }
            : new ActionOutcome { Success = false, Message = "You already have the starter furniture." };
    }

    public static ActionOutcome Place(GameData data, string item, string slot)
    {
        if (!Items.Contains(item))
            return new ActionOutcome { Success = false, Message = "Choose a furniture piece." };
        if (!Slots.Contains(slot))
            return new ActionOutcome { Success = false, Message = "Place it on one of the house pads." };
        if (data.FurniturePlacements.Any(p => p.Slot == slot))
            return new ActionOutcome { Success = false, Message = "That pad is already used. Click the item to move it." };
        if (Available(data, item) <= 0)
            return new ActionOutcome { Success = false, Message = $"No {Label(item).ToLowerInvariant()} ready to place. Buy one first." };
        data.FurniturePlacements.Add(new PlacedItem(item, slot));
        return new ActionOutcome { Success = true, Message = $"{Label(item)} placed." };
    }

    public static PlacedItem PickUp(GameData data, string slot)
    {
        var found = data.FurniturePlacements.FirstOrDefault(p => p.Slot == slot);
        if (found == null) return null;
        data.FurniturePlacements.Remove(found);
        return found;
    }

    public static string Label(string item) => item switch
    {
        "table" => "Table",
        "chair" => "Chair",
        "plant" => "Plant",
        _ => item,
    };
}
