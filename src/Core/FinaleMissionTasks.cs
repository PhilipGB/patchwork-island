using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

/// <summary>Authored finale activities; completion and rewards remain owned by MissionService.</summary>
public sealed class FinaleMissionTasks
{
    public GameData Data { get; }
    private readonly Func<int, MissionCompletionResult> _complete;
    internal FinaleMissionTasks(GameData data, Func<int, MissionCompletionResult> complete)
    {
        Data = data;
        _complete = complete;
    }
    private MissionCompletionResult CompleteActive(int bonus) => _complete(bonus);
    private static GameplayOutcome FailGame(string message) => new() { Success = false, Message = message };
    private static ActionOutcome FailAction(string message) => new() { Success = false, Message = message };

    public GameplayOutcome LoadDeliveryParcels()
    {
        if (Data.ActiveMission != "M16")
            return FailGame("Start The Delivery Round at the JOBS board first.");
        if (Data.IsCompleted("M16"))
            return FailGame("The delivery round is already finished.");
        if (Data.GetMissionCounter("M16", "loaded") >= 1)
            return FailGame("The three parcels are already loaded in the van.");

        Data.SetMissionCounter("M16", "loaded", 1);
        foreach (string npc in FinaleRules.DeliveryRecipients)
        {
            Data.SetMissionCounter("M16", FinaleRules.CarryingKey(npc), 1);
            Data.SetMissionCounter("M16", FinaleRules.DeliveredKey(npc), 0);
        }
        return new GameplayOutcome
        {
            Success = true,
            Message = "Loaded ADA, PIP and IVY parcels. Park near each neighbour, step out and deliver.",
        };
    }

    public GameplayOutcome DeliverParcel(string npcId, string recipientId)
    {
        if (Data.ActiveMission != "M16")
            return FailGame("Start The Delivery Round at the JOBS board first.");
        if (Data.IsCompleted("M16"))
            return FailGame("The delivery round is already finished.");
        if (!FinaleRules.DeliveryRecipients.Contains(npcId) ||
            !FinaleRules.DeliveryRecipients.Contains(recipientId))
            return FailGame("That parcel does not belong to a delivery neighbour.");
        if (Data.GetMissionCounter("M16", "loaded") <= 0)
            return FailGame("Load the three parcels at the POST COUNTER first.");
        if (Data.GetMissionCounter("M16", FinaleRules.DeliveredKey(recipientId)) >= 1)
            return FailGame($"{FinaleRules.DeliveryName(recipientId)} already has that parcel.");
        if (Data.GetMissionCounter("M16", FinaleRules.CarryingKey(recipientId)) <= 0)
            return FailGame($"You are not carrying {FinaleRules.DeliveryName(recipientId)}'s parcel.");
        if (!string.Equals(npcId, recipientId, StringComparison.Ordinal))
            return FailGame($"That parcel is for {FinaleRules.DeliveryName(recipientId)}, not {FinaleRules.DeliveryName(npcId)}. Nothing is lost.");

        Data.SetMissionCounter("M16", FinaleRules.CarryingKey(recipientId), 0);
        Data.SetMissionCounter("M16", FinaleRules.DeliveredKey(recipientId), 1);
        var outcome = new GameplayOutcome
        {
            Success = true,
            Message = $"{FinaleRules.DeliveryName(recipientId)} got the right parcel!",
        };
        if (FinaleRules.M16Complete(Data))
            outcome.Completion = CompleteActive(0);
        return outcome;
    }

    // -------------------------------------------------------- M17 market maths
    public ActionOutcome AddMarketItem(string item)
    {
        if (Data.ActiveMission != "M17")
            return FailAction("Start Market Day Maths at the JOBS board first.");
        if (FinaleRules.M17Complete(Data))
            return FailAction("The market list is already paid for.");
        if (!FinaleRules.MarketItems.Contains(item))
            return FailAction("That is not on the shopping list.");
        int bread = FinaleRules.MarketCount(Data, "bread");
        int milk = FinaleRules.MarketCount(Data, "milk");
        int apple = FinaleRules.MarketCount(Data, "apple");
        if (!FinaleRules.CanAddMarketItem(bread, milk, apple, item))
            return FailAction($"That would go over the {FinaleRules.MarketBudget} job coins.");
        Data.SetMissionCounter("M17", item, FinaleRules.MarketCount(Data, item) + 1);
        return new ActionOutcome
        {
            Success = true,
            Message = $"Added {item}. Total {FinaleRules.MarketSpent(Data)} of {FinaleRules.MarketBudget}, " +
                      $"change {FinaleRules.MarketChange(Data)}.",
        };
    }

    public ActionOutcome RemoveMarketItem(string item)
    {
        if (Data.ActiveMission != "M17")
            return FailAction("Start Market Day Maths at the JOBS board first.");
        if (FinaleRules.M17Complete(Data))
            return FailAction("The market list is already paid for.");
        if (!FinaleRules.MarketItems.Contains(item))
            return FailAction("That is not on the shopping list.");
        int count = FinaleRules.MarketCount(Data, item);
        if (count <= 0)
            return FailAction($"There is no {item} in the basket to take back.");
        Data.SetMissionCounter("M17", item, count - 1);
        return new ActionOutcome
        {
            Success = true,
            Message = $"Removed {item}. Total {FinaleRules.MarketSpent(Data)} of {FinaleRules.MarketBudget}, " +
                      $"change {FinaleRules.MarketChange(Data)}.",
        };
    }

    public GameplayOutcome FinaliseMarket()
    {
        if (Data.ActiveMission != "M17")
            return FailGame("Start Market Day Maths at the JOBS board first.");
        if (FinaleRules.M17Complete(Data))
            return FailGame("The market list is already paid for.");
        int bread = FinaleRules.MarketCount(Data, "bread");
        int milk = FinaleRules.MarketCount(Data, "milk");
        int apple = FinaleRules.MarketCount(Data, "apple");
        if (!FinaleRules.MarketListExact(bread, milk, apple))
            return FailGame($"The list needs exactly 2 bread, 1 milk and 2 apples. " +
                           $"Your basket has {bread} bread, {milk} milk and {apple} apples.");
        Data.SetMissionCounter("M17", "finalised", 1);
        var outcome = new GameplayOutcome
        {
            Success = true,
            Message = "2 bread + 1 milk + 2 apples = 14. Change from 20 is 6. Paid with job coins, not your wallet.",
            Completion = CompleteActive(0),
        };
        return outcome;
    }

    // ------------------------------------------------------------ M18 mechanic
    public GameplayOutcome CollectRepairPart(string part, string pickupId)
    {
        if (Data.ActiveMission != "M18")
            return FailGame("Start Mechanic's Toolbox at the JOBS board first.");
        if (FinaleRules.M18Complete(Data))
            return FailGame("Pip's car is already repaired.");
        int required = FinaleRules.PartRequired(part);
        if (required <= 0)
            return FailGame("That is not one of the repair parts.");
        if (FinaleRules.RepairPickupPart(pickupId) != part)
            return FailGame("That is not the right repair part.");
        if (Data.HasCollected(pickupId))
            return FailGame("You already collected that part.");
        int have = FinaleRules.HasPart(Data, part);
        if (have >= required)
            return FailGame($"You already have enough {FinaleRules.PartLabel(part).ToLowerInvariant()}s.");

        // Record the physical pickup so a partial M18 save cannot respawn it and
        // hand out a second copy of the same tyre after Continue.
        Data.CollectedPickups.Add(pickupId);
        Data.SetMissionCounter("M18", "has_" + part, have + 1);
        return new GameplayOutcome
        {
            Success = true,
            Message = $"Found a {FinaleRules.PartLabel(part).ToLowerInvariant()} ({have + 1}/{required}).",
        };
    }

    public ActionOutcome InspectVehicle()
    {
        if (Data.ActiveMission != "M18")
            return FailAction("Start Mechanic's Toolbox at the JOBS board first.");
        if (FinaleRules.M18Complete(Data))
            return FailAction("Pip's car is already repaired.");
        Data.SetMissionCounter("M18", "inspected", 1);
        return new ActionOutcome
        {
            Success = true,
            Message = "Inspection: two tyres first, then the lamp, then the battery last.",
        };
    }

    public GameplayOutcome FitRepairPart(string part)
    {
        if (Data.ActiveMission != "M18")
            return FailGame("Start Mechanic's Toolbox at the JOBS board first.");
        if (!FinaleRules.CanFitPart(Data, part, out string message))
            return FailGame(message);

        if (part == "tyre")
        {
            Data.SetMissionCounter("M18", "has_tyre", FinaleRules.HasPart(Data, "tyre") - 1);
            Data.SetMissionCounter("M18", "tyres_fitted",
                Data.GetMissionCounter("M18", "tyres_fitted") + 1);
        }
        else if (part == "lamp")
        {
            Data.SetMissionCounter("M18", "has_lamp", FinaleRules.HasPart(Data, "lamp") - 1);
            Data.SetMissionCounter("M18", "lamp_fitted", 1);
        }
        else
        {
            Data.SetMissionCounter("M18", "has_battery", FinaleRules.HasPart(Data, "battery") - 1);
            Data.SetMissionCounter("M18", "battery_fitted", 1);
        }

        var outcome = new GameplayOutcome
        {
            Success = true,
            Message = $"{FinaleRules.PartLabel(part)} fitted. " +
                      $"{Data.GetMissionCounter("M18", "tyres_fitted")} tyre(s), " +
                      $"lamp {(Data.GetMissionCounter("M18", "lamp_fitted") > 0 ? "on" : "off")}, " +
                      $"battery {(Data.GetMissionCounter("M18", "battery_fitted") > 0 ? "on" : "off")}.",
        };
        if (FinaleRules.M18Complete(Data))
            outcome.Completion = CompleteActive(0);
        return outcome;
    }

    // ------------------------------------------------------------- M20 festival
    public GameplayOutcome InviteForFestival(string npcId)
    {
        if (Data.ActiveMission != "M20")
            return FailGame("Start Village Festival at the JOBS board first.");
        if (FinaleRules.M20Complete(Data))
            return FailGame("The festival is already ready.");
        if (!FinaleRules.FestivalInvitees.Contains(npcId))
            return FailGame("That neighbour is not on the invitation list.");
        if (Data.GetMissionCounter("M20", FinaleRules.InvitedKey(npcId)) >= 1)
            return FailGame($"{FinaleRules.DeliveryName(npcId)} already has an invitation.");
        if (Data.GetMissionCounter("M20", "invite_stock") <= 0)
            return FailGame("There are no invitations left in the job stock.");

        Data.SetMissionCounter("M20", FinaleRules.InvitedKey(npcId), 1);
        Data.SetMissionCounter("M20", "invite_stock",
            Data.GetMissionCounter("M20", "invite_stock") - 1);
        return new GameplayOutcome
        {
            Success = true,
            Message = $"{FinaleRules.DeliveryName(npcId)} is invited to the village festival.",
        };
    }

    public GameplayOutcome PlaceFestivalTable(int index)
    {
        if (Data.ActiveMission != "M20")
            return FailGame("Start Village Festival at the JOBS board first.");
        if (FinaleRules.M20Complete(Data))
            return FailGame("The festival is already ready.");
        if (index < 0 || index >= FinaleRules.FestivalTableCount)
            return FailGame("That is not a festival table pad.");
        if (!FinaleRules.InvitesComplete(Data))
            return FailGame("Invite Ada, Pip and Ivy before placing the tables.");
        if (Data.GetMissionCounter("M20", FinaleRules.TableKey(index)) >= 1)
            return FailGame("That table is already placed.");
        if (Data.GetMissionCounter("M20", "table_stock") <= 0)
            return FailGame("There are no tables left in the job stock.");

        Data.SetMissionCounter("M20", FinaleRules.TableKey(index), 1);
        Data.SetMissionCounter("M20", "table_stock",
            Data.GetMissionCounter("M20", "table_stock") - 1);
        return new GameplayOutcome
        {
            Success = true,
            Message = $"Table {index + 1} placed.",
        };
    }

    public GameplayOutcome RemoveFestivalTable(int index)
    {
        if (Data.ActiveMission != "M20")
            return FailGame("Start Village Festival at the JOBS board first.");
        if (FinaleRules.M20Complete(Data))
            return FailGame("The finished festival decorations are permanent.");
        if (index < 0 || index >= FinaleRules.FestivalTableCount)
            return FailGame("That is not a festival table pad.");
        if (Data.GetMissionCounter("M20", FinaleRules.TableKey(index)) <= 0)
            return FailGame("That table is already clear.");
        Data.SetMissionCounter("M20", FinaleRules.TableKey(index), 0);
        Data.SetMissionCounter("M20", "table_stock",
            Data.GetMissionCounter("M20", "table_stock") + 1);
        return new GameplayOutcome
        {
            Success = true,
            Message = $"Table {index + 1} taken back. Try another spot.",
        };
    }

    public GameplayOutcome HangFestivalLantern(int index)
    {
        if (Data.ActiveMission != "M20")
            return FailGame("Start Village Festival at the JOBS board first.");
        if (FinaleRules.M20Complete(Data))
            return FailGame("The festival is already ready.");
        if (index < 0 || index >= FinaleRules.FestivalLanternCount)
            return FailGame("That is not a festival lantern hook.");
        if (!FinaleRules.InvitesComplete(Data))
            return FailGame("Invite Ada, Pip and Ivy before hanging the lanterns.");
        if (!FinaleRules.TablesComplete(Data))
            return FailGame("Place both tables before hanging the lanterns.");
        if (Data.GetMissionCounter("M20", FinaleRules.LanternKey(index)) >= 1)
            return FailGame("That lantern is already hanging.");
        if (Data.GetMissionCounter("M20", "lantern_stock") <= 0)
            return FailGame("There are no lanterns left in the job stock.");

        Data.SetMissionCounter("M20", FinaleRules.LanternKey(index), 1);
        Data.SetMissionCounter("M20", "lantern_stock",
            Data.GetMissionCounter("M20", "lantern_stock") - 1);
        var outcome = new GameplayOutcome
        {
            Success = true,
            Message = $"Lantern {index + 1} is glowing.",
        };
        if (FinaleRules.M20Complete(Data))
            outcome.Completion = CompleteActive(0);
        return outcome;
    }

    public GameplayOutcome RemoveFestivalLantern(int index)
    {
        if (Data.ActiveMission != "M20")
            return FailGame("Start Village Festival at the JOBS board first.");
        if (FinaleRules.M20Complete(Data))
            return FailGame("The finished festival decorations are permanent.");
        if (index < 0 || index >= FinaleRules.FestivalLanternCount)
            return FailGame("That is not a festival lantern hook.");
        if (Data.GetMissionCounter("M20", FinaleRules.LanternKey(index)) <= 0)
            return FailGame("That lantern hook is already empty.");
        Data.SetMissionCounter("M20", FinaleRules.LanternKey(index), 0);
        Data.SetMissionCounter("M20", "lantern_stock",
            Data.GetMissionCounter("M20", "lantern_stock") + 1);
        return new GameplayOutcome
        {
            Success = true,
            Message = $"Lantern {index + 1} taken down. Try again when ready.",
        };
    }

}
