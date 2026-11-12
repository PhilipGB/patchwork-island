using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

/// <summary>
/// The mission state machine. It owns exactly-once rewards and prerequisites,
/// but stays free of Godot so it is directly unit-testable.
/// </summary>
public sealed class MissionService
{
    public GameData Data { get; }
    public MissionCatalog Catalog { get; }
    public CommunityMissionTasks Community { get; }
    public FinaleMissionTasks Finale { get; }

    public MissionService(GameData data, MissionCatalog catalog)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Data.Normalise();
        Community = new CommunityMissionTasks(Data, CompleteActive);
        Finale = new FinaleMissionTasks(Data, CompleteActive);
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
        EnsureMissionStock(missionId);
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

    // ------------------------------------------------------- M21 roadworks
    /// <summary>
    /// Consumes the three marked cones, two barrier panels and toolbox at the
    /// worksite. This is only a partial step; the order task still has to be
    /// solved before the mission can complete.
    /// </summary>
    public GameplayOutcome DeliverRoadworksSupplies()
    {
        if (Data.ActiveMission != RoadworksRules.MissionId)
            return FailGame("Start Roadworks Rescue at the village JOBS board first.");
        if (Data.IsCompleted(RoadworksRules.MissionId))
            return FailGame("The city road is already open.");
        if (RoadworksRules.SuppliesDelivered(Data))
            return FailGame("The crew already has every delivery.");
        if (!RoadworksRules.HasAllSupplies(Data))
        {
            return FailGame(
                $"The crew still needs {RoadworksRules.ConeCount} cones, " +
                $"{RoadworksRules.PanelCount} barrier panels and one toolbox. " +
                $"Delivered {RoadworksRules.SupplyProgress(Data)} of " +
                $"{RoadworksRules.SupplyTotal}.");
        }

        Data.RemoveItem("safety_cone", RoadworksRules.ConeCount);
        Data.RemoveItem("barrier_panel", RoadworksRules.PanelCount);
        Data.RemoveItem("toolbox", RoadworksRules.ToolboxCount);
        Data.SetMissionCounter(RoadworksRules.MissionId,
            RoadworksRules.SuppliesDeliveredKey, 1);
        return new GameplayOutcome
        {
            Success = true,
            Message = "3 cones, 2 barrier panels and 1 toolbox delivered. Now help the crew check the loading order.",
        };
    }

    /// <summary>
    /// Short numeracy/order task at the worksite. A wrong answer costs no item
    /// or coin and leaves the mission active. The correct answer completes M21
    /// exactly once through CompleteActive.
    /// </summary>
    public GameplayOutcome CompleteRoadworksTask(int total, IReadOnlyList<string> order)
    {
        if (Data.ActiveMission != RoadworksRules.MissionId)
            return FailGame("Start Roadworks Rescue at the village JOBS board first.");
        if (Data.IsCompleted(RoadworksRules.MissionId))
            return FailGame("The city road is already open.");
        if (!RoadworksRules.SuppliesDelivered(Data))
            return FailGame("Deliver every item to the worksite first.");
        if (RoadworksRules.TaskDone(Data))
            return FailGame("The crew already checked the loading order.");
        if (!RoadworksRules.TotalIsCorrect(total))
            return FailGame(
                $"{RoadworksRules.ConeCount} cones + {RoadworksRules.PanelCount} panels + " +
                $"1 toolbox. Count again.");
        if (!RoadworksRules.OrderIsCorrect(order))
            return FailGame("Load the cones first, then the barrier panels, then the toolbox.");

        Data.SetMissionCounter(RoadworksRules.MissionId, RoadworksRules.TaskDoneKey, 1);
        var result = CompleteActive(0);
        return new GameplayOutcome
        {
            Success = result.Success,
            Message = result.Message,
            Completion = result.Success ? result : null,
        };
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
        if (Data.ActiveMission is not ("M05" or "M09" or "M19"))
            return new ActionOutcome { Success = false, Message = "No race job is active." };
        string id = Data.ActiveMission;
        int gateCount = id == "M19" ? FinaleRules.RallyGateCount : 4;
        int current = Data.GetMissionCounter(id, "checkpoint");
        if (current >= gateCount)
            return new ActionOutcome { Success = false, Message = "The race is already finished." };
        if (gateNumber != current + 1)
            return new ActionOutcome { Success = false, Message = $"Gate {current + 1} is next." };

        Data.SetMissionCounter(id, "checkpoint", gateNumber);
        return new ActionOutcome
        {
            Success = true,
            Message = gateNumber == gateCount
                ? "Finish gate reached!"
                : $"Gate {gateNumber}! Now gate {gateNumber + 1}.",
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

    // ---------------------------------------------------------------- swings

    public PickupResult PlaceSwingPiece(string pieceType, string socketId)
    {
        if (Data.ActiveMission != "M07")
            return new PickupResult { Success = false, Message = "Start Fix the Swings at the JOBS board first." };
        if (Data.IsCompleted("M07"))
            return new PickupResult { Success = false, Message = "The swings are already fixed." };
        if (!BuildRules.ValidateSwingPlacement(pieceType, socketId, Data.SwingPieces, out string message))
            return new PickupResult { Success = false, Message = message, Count = Data.SwingPieces.Count, Required = 6 };

        Data.SwingPieces.Add(socketId);
        return new PickupResult
        {
            Success = true,
            Count = Data.SwingPieces.Distinct().Count(),
            Required = 6,
            Message = pieceType == "rope" ? "Rope tied on." : "Seat added.",
        };
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

    private void EnsureMissionStock(string missionId)
    {
        if (missionId == "M11")
        {
            if (Data.GetMissionCounter("M11", "stock_granted") > 0) return;
            Data.SetMissionCounter("M11", "stock_granted", 1);
            foreach (string npc in ChapterTwoRules.CardNpcIds)
                Data.SetMissionCounter("M11", "card_" + npc, 1);
            return;
        }

        // M15 teaches the pattern with the first RED/BLUE pair already planted.
        // Only seed a genuinely new attempt; a player who clears a pad keeps
        // that choice after switching jobs or reloading.
        if (missionId == "M15" && !Data.MissionProgress.ContainsKey("M15"))
        {
            Data.SetMissionCounter("M15", "pad0", ChapterTwoRules.GardenCode("red"));
            Data.SetMissionCounter("M15", "pad1", ChapterTwoRules.GardenCode("blue"));
            return;
        }

        if (missionId == "M20" && Data.GetMissionCounter("M20", "stock_granted") <= 0)
        {
            Data.SetMissionCounter("M20", "stock_granted", 1);
            Data.SetMissionCounter("M20", "invite_stock", FinaleRules.FestivalInvitees.Length);
            Data.SetMissionCounter("M20", "table_stock", FinaleRules.FestivalTableCount);
            Data.SetMissionCounter("M20", "lantern_stock", FinaleRules.FestivalLanternCount);
        }
    }

    // ------------------------------------------------- chapter two: M11 cards
    // Compatibility facade: UI can depend directly on the narrower activity handlers.
    public GameplayOutcome HandWelcomeCard(string npcId, string cardId) => Community.HandWelcomeCard(npcId, cardId);
    public GameplayOutcome ServeBakeryOrder(string customerId, int bread, int cakes) => Community.ServeBakeryOrder(customerId, bread, cakes);
    public GameplayOutcome CollectBook(string bookId) => Community.CollectBook(bookId);
    public GameplayOutcome ShelveBook(string bookId, string shelfId) => Community.ShelveBook(bookId, shelfId);
    public GameplayOutcome InstallRoadSign(string socketId, string signId, string direction) => Community.InstallRoadSign(socketId, signId, direction);
    public GameplayOutcome RemoveRoadSign(string socketId) => Community.RemoveRoadSign(socketId);
    public GameplayOutcome PlantGarden(int padIndex, string color) => Community.PlantGarden(padIndex, color);
    public GameplayOutcome LoadDeliveryParcels() => Finale.LoadDeliveryParcels();
    public GameplayOutcome DeliverParcel(string npcId, string recipientId) => Finale.DeliverParcel(npcId, recipientId);
    public ActionOutcome AddMarketItem(string item) => Finale.AddMarketItem(item);
    public ActionOutcome RemoveMarketItem(string item) => Finale.RemoveMarketItem(item);
    public GameplayOutcome FinaliseMarket() => Finale.FinaliseMarket();
    public GameplayOutcome CollectRepairPart(string part, string pickupId) => Finale.CollectRepairPart(part, pickupId);
    public ActionOutcome InspectVehicle() => Finale.InspectVehicle();
    public GameplayOutcome FitRepairPart(string part) => Finale.FitRepairPart(part);
    public GameplayOutcome InviteForFestival(string npcId) => Finale.InviteForFestival(npcId);
    public GameplayOutcome PlaceFestivalTable(int index) => Finale.PlaceFestivalTable(index);
    public GameplayOutcome RemoveFestivalTable(int index) => Finale.RemoveFestivalTable(index);
    public GameplayOutcome HangFestivalLantern(int index) => Finale.HangFestivalLantern(index);
    public GameplayOutcome RemoveFestivalLantern(int index) => Finale.RemoveFestivalLantern(index);

    private static ActionOutcome FailAction(string message) =>
        new() { Success = false, Message = message };

    private static GameplayOutcome FailGame(string message) => new() { Success = false, Message = message };

    public MissionCompletionResult CompleteActive(int bonusCoins = 0)
    {
        if (string.IsNullOrEmpty(Data.ActiveMission))
            return Fail("No active job.");
        var mission = Catalog.Get(Data.ActiveMission);
        if (mission == null)
            return Fail("That job is missing from the content.");
        if (Data.IsCompleted(mission.Id))
            return Fail("That job is already finished.");

        // Domain guard: callers may not inject an arbitrary wallet bonus.
        // Only the three race jobs have an optional speed bonus, capped at the
        // legitimate maximum. The base mission reward is always exactly once.
        if (bonusCoins < 0)
            return Fail("The speed bonus is invalid.");
        if (bonusCoins > 0)
        {
            if (mission.Id is not ("M05" or "M09" or "M19"))
                return Fail("That job does not have a speed bonus.");
            if (bonusCoins > MissionRules.MaxOptionalRaceBonus)
                return Fail("The speed bonus is larger than the course allows.");

            // Verify the bonus against the persisted race clock rather than
            // trusting the caller. The stored milliseconds were rounded up, so
            // this preserves the exact 90 s / 75 s boundary without shortening
            // an elapsed time across save/reload.
            double elapsedSeconds = Data.GetMissionCounter(mission.Id, "elapsedMs") / 1000.0;
            int earned = MissionRules.OptionalRaceBonus(mission.Id, elapsedSeconds);
            if (bonusCoins > earned)
                return Fail("That speed bonus was not earned on this race.");
        }

        if (!MissionRules.IsObjectiveComplete(Data, mission.Id))
            return Fail("This job is not finished yet.");

        Data.CompletedMissions.Add(mission.Id);
        int total = mission.RewardCoins + bonusCoins;
        Data.Coins += total;
        string completedId = mission.Id;
        Data.ActiveMission = "";
        bool allMissions = Catalog.Missions.All(m => Data.IsCompleted(m.Id));
        bool allTwenty = new[]
        {
            "M01", "M02", "M03", "M04", "M05", "M06", "M07", "M08", "M09", "M10",
            "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20",
        }.All(Data.IsCompleted);

        if (completedId == "M01")
            UnlockCosmetic("red_cap");

        return new MissionCompletionResult
        {
            Success = true,
            Mission = mission,
            CoinsAwarded = mission.RewardCoins,
            BonusCoins = bonusCoins,
            AllComplete = allMissions,
            OriginalChapterComplete = MissionRules.OriginalChapterComplete(Data),
            AllTwentyComplete = allTwenty,
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
        "safety_cone" => "Safety cone",
        "barrier_panel" => "Barrier panel",
        "toolbox" => "Toolbox",
        _ => item,
    };
}
