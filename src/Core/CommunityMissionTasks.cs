using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

/// <summary>Authored community activities; completion and rewards remain owned by MissionService.</summary>
public sealed class CommunityMissionTasks
{
    public GameData Data { get; }
    private readonly Func<int, MissionCompletionResult> _complete;
    internal CommunityMissionTasks(GameData data, Func<int, MissionCompletionResult> complete)
    {
        Data = data;
        _complete = complete;
    }
    private MissionCompletionResult CompleteActive(int bonus) => _complete(bonus);
    private static GameplayOutcome FailGame(string message) => new() { Success = false, Message = message };
    private static ActionOutcome FailAction(string message) => new() { Success = false, Message = message };

    public GameplayOutcome HandWelcomeCard(string npcId, string cardId)
    {
        if (Data.ActiveMission != "M11")
            return FailGame("Start Meet the Neighbours at the JOBS board first.");
        if (Data.IsCompleted("M11"))
            return FailGame("The welcome cards are already delivered.");
        string expectedCard = ChapterTwoRules.CardFor(npcId);
        string expectedNpc = ChapterTwoRules.NpcForCard(cardId);
        if (string.IsNullOrEmpty(expectedCard) || string.IsNullOrEmpty(expectedNpc))
            return FailGame("That card does not belong to a villager.");
        if (Data.GetMissionCounter("M11", $"delivered_{npcId}") >= 1)
            return FailGame($"{ChapterTwoRules.CardLabel(cardId)} already has a welcome card.");
        if (Data.GetMissionCounter("M11", $"card_{npcId}") <= 0)
            return FailGame($"You do not have {ChapterTwoRules.CardLabel(expectedCard)}'s card any more.");

        if (expectedNpc != npcId)
            return FailGame($"That is {ChapterTwoRules.CardLabel(cardId)}'s card. Read the name and try the right neighbour.");

        Data.SetMissionCounter("M11", $"card_{npcId}", 0);
        Data.SetMissionCounter("M11", $"delivered_{npcId}", 1);
        var outcome = new GameplayOutcome
        {
            Success = true,
            Message = $"Correct! {ChapterTwoRules.CardLabel(cardId)} gets the {ChapterTwoRules.CardLabel(cardId)} card.",
        };
        if (ChapterTwoRules.M11Complete(Data))
            outcome.Completion = CompleteActive(0);
        return outcome;
    }

    // ------------------------------------------------------------- M12 bakery
    public GameplayOutcome ServeBakeryOrder(string customerId, int bread, int cakes)
    {
        if (Data.ActiveMission != "M12")
            return FailGame("Start Bakery Busy Day at the JOBS board first.");
        if (Data.IsCompleted("M12"))
            return FailGame("The bakery orders are already finished.");
        var order = ChapterTwoRules.FindOrder(customerId);
        if (order == null)
            return FailGame("That customer is not waiting today.");
        if (Data.GetMissionCounter("M12", $"order_{customerId}") >= 1)
            return FailGame($"{order.CustomerName}'s order is already served.");
        if (bread < 0 || cakes < 0)
            return FailGame("Counts cannot be negative.");

        if (bread != order.Bread || cakes != order.Cakes)
            return FailGame($"That is not {order.CustomerName}'s order. The card says {order.Bread} bread and {order.Cakes} cakes.");

        Data.SetMissionCounter("M12", $"order_{customerId}", 1);
        var outcome = new GameplayOutcome
        {
            Success = true,
            Message = $"{order.CustomerName}: {bread} bread + {cakes} cakes. Correct!",
        };
        if (ChapterTwoRules.M12Complete(Data))
            outcome.Completion = CompleteActive(0);
        return outcome;
    }

    // -------------------------------------------------------------- M13 books
    public GameplayOutcome CollectBook(string bookId)
    {
        if (Data.ActiveMission != "M13")
            return FailGame("Start Lost Library Books at the JOBS board first.");
        if (Data.IsCompleted("M13"))
            return FailGame("The library is already tidy.");
        if (!ChapterTwoRules.IsBook(bookId))
            return FailGame("That is not one of Ollie's books.");
        string pickupId = ChapterTwoRules.BookPickupId(bookId);
        if (Data.CollectedPickups.Contains(pickupId))
            return FailGame("You already have that book.");
        if (Data.GetMissionCounter("M13", ChapterTwoRules.ShelfKey(bookId)) >= 1)
            return FailGame("That book is already on its shelf.");

        Data.CollectedPickups.Add(pickupId);
        Data.SetMissionCounter("M13", ChapterTwoRules.HasBookKey(bookId), 1);
        return new GameplayOutcome
        {
            Success = true,
            Message = $"{ChapterTwoRules.BookLabel(bookId)} book collected. Take it to Ollie's shelves.",
        };
    }

    public GameplayOutcome ShelveBook(string bookId, string shelfId)
    {
        if (Data.ActiveMission != "M13")
            return FailGame("Start Lost Library Books at the JOBS board first.");
        if (Data.IsCompleted("M13"))
            return FailGame("The library is already tidy.");
        if (!ChapterTwoRules.IsBook(bookId) || !ChapterTwoRules.IsBook(shelfId))
            return FailGame("Choose a labelled book and shelf.");
        if (Data.GetMissionCounter("M13", ChapterTwoRules.HasBookKey(bookId)) <= 0)
            return FailGame($"You are not carrying the {ChapterTwoRules.BookLabel(bookId)} book.");
        if (!string.Equals(bookId, shelfId, StringComparison.Ordinal))
            return FailGame($"The {ChapterTwoRules.BookLabel(bookId)} book does not belong on the {ChapterTwoRules.BookLabel(shelfId)} shelf.");

        Data.SetMissionCounter("M13", ChapterTwoRules.HasBookKey(bookId), 0);
        Data.SetMissionCounter("M13", ChapterTwoRules.ShelfKey(bookId), 1);
        var outcome = new GameplayOutcome
        {
            Success = true,
            Message = $"{ChapterTwoRules.BookLabel(bookId)} matched to the {ChapterTwoRules.BookLabel(bookId)} shelf.",
        };
        if (ChapterTwoRules.M13Complete(Data))
            outcome.Completion = CompleteActive(0);
        return outcome;
    }

    // --------------------------------------------------------------- M14 signs
    public GameplayOutcome InstallRoadSign(string socketId, string signId, string direction)
    {
        if (Data.ActiveMission != "M14")
            return FailGame("Start Road Sign Rescue at the JOBS board first.");
        if (Data.IsCompleted("M14"))
            return FailGame("All road signs are already repaired.");
        var socket = ChapterTwoRules.FindSocket(socketId);
        if (socket == null)
            return FailGame("That is not a road sign socket.");
        if (!ChapterTwoRules.SignIds.Contains(signId) || !ChapterTwoRules.Directions.Contains(direction))
            return FailGame("Choose one of the three signs and a direction.");
        if (Data.GetMissionCounter("M14", $"sign_{socketId}") >= 1)
            return FailGame($"{socket.Label} socket is already repaired.");
        if (!ChapterTwoRules.SignPlacementMatches(socketId, signId, direction))
            return FailGame($"That sign or arrow does not fit the {socket.Label} socket. Try another direction.");

        Data.SetMissionCounter("M14", $"sign_{socketId}", 1);
        var outcome = new GameplayOutcome
        {
            Success = true,
            Message = $"{ChapterTwoRules.SignLabel(signId)} {ChapterTwoRules.ArrowFor(direction)} installed.",
        };
        if (ChapterTwoRules.M14Complete(Data))
            outcome.Completion = CompleteActive(0);
        return outcome;
    }

    public GameplayOutcome RemoveRoadSign(string socketId)
    {
        if (Data.ActiveMission != "M14")
            return FailGame("Start Road Sign Rescue at the JOBS board first.");
        if (Data.IsCompleted("M14"))
            return FailGame("Repaired road signs are permanent.");
        if (Data.GetMissionCounter("M14", $"sign_{socketId}") <= 0)
            return FailGame("That socket is already empty.");
        Data.SetMissionCounter("M14", $"sign_{socketId}", 0);
        return new GameplayOutcome { Success = true, Message = "Sign removed. Try again when ready." };
    }

    // -------------------------------------------------------------- M15 garden
    public GameplayOutcome PlantGarden(int padIndex, string color)
    {
        if (Data.ActiveMission != "M15")
            return FailGame("Start Garden Patterns at the JOBS board first.");
        if (Data.IsCompleted("M15"))
            return FailGame("The garden pattern is already finished.");
        if (padIndex < 0 || padIndex >= ChapterTwoRules.GardenPattern.Length)
            return FailGame("That is not a garden pad.");
        int code = ChapterTwoRules.GardenCode(color);
        if (string.Equals(color, "empty", StringComparison.Ordinal))
        {
            Data.SetMissionCounter("M15", $"pad{padIndex}", 0);
            return new GameplayOutcome { Success = true, Message = "Flower cleared. Seed stock is unlimited." };
        }
        if (code == 0)
            return FailGame("Choose a RED or BLUE flower.");

        Data.SetMissionCounter("M15", $"pad{padIndex}", code);
        bool correct = ChapterTwoRules.GardenPadMatches(padIndex, color);
        var outcome = new GameplayOutcome
        {
            Success = true,
            Message = correct
                ? $"{color.ToUpperInvariant()} flower planted on pad {padIndex + 1}."
                : $"That pad needs a different colour. Try again — nothing is lost.",
        };
        if (ChapterTwoRules.M15Complete(Data))
            outcome.Completion = CompleteActive(0);
        return outcome;
    }

    // ------------------------------------------------------- M16 delivery round
}
