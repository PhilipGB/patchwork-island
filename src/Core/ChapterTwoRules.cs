using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

/// <summary>
/// Authored pure rules for the first half of chapter two. Gameplay methods in
/// MissionService use these definitions; tests can therefore verify correct and
/// incorrect choices without booting Godot.
/// </summary>
public static class ChapterTwoRules
{
    public static readonly string[] MissionIds =
    {
        "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20",
    };

    public static readonly string[] FirstHalfIds = { "M11", "M12", "M13", "M14", "M15" };

    // ------------------------------------------------------------------ cards
    public static readonly string[] CardNpcIds = { "ada", "ollie", "fern" };

    public static string CardFor(string npcId) => npcId switch
    {
        "ada" => "card_ada",
        "ollie" => "card_ollie",
        "fern" => "card_fern",
        _ => "",
    };

    public static string NpcForCard(string cardId) => cardId switch
    {
        "card_ada" => "ada",
        "card_ollie" => "ollie",
        "card_fern" => "fern",
        _ => "",
    };

    public static string CardLabel(string cardId) => cardId switch
    {
        "card_ada" => "ADA",
        "card_ollie" => "OLLIE",
        "card_fern" => "FERN",
        _ => cardId,
    };

    // --------------------------------------------------------------- bakery
    public sealed class BakeryOrder
    {
        public string CustomerId { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public int Bread { get; init; }
        public int Cakes { get; init; }
    }

    public static readonly IReadOnlyList<BakeryOrder> BakeryOrders = new List<BakeryOrder>
    {
        new() { CustomerId = "milo", CustomerName = "Milo", Bread = 2, Cakes = 1 },
        new() { CustomerId = "fern", CustomerName = "Fern", Bread = 3, Cakes = 2 },
        new() { CustomerId = "ivy", CustomerName = "Ivy", Bread = 1, Cakes = 3 },
    };

    public static BakeryOrder FindOrder(string customerId) =>
        BakeryOrders.FirstOrDefault(order => string.Equals(order.CustomerId, customerId,
            StringComparison.Ordinal));

    // --------------------------------------------------------------- books
    public static readonly string[] BookIds = { "sun", "moon", "tree", "star" };

    public static string BookLabel(string bookId) => bookId?.ToUpperInvariant() ?? "";

    public static bool IsBook(string bookId) =>
        bookId != null && BookIds.Contains(bookId, StringComparer.Ordinal);

    public static string HasBookKey(string bookId) => $"has_{bookId}";
    public static string ShelfKey(string bookId) => $"shelf_{bookId}";
    public static string BookPickupId(string bookId) => $"book_{bookId}";

    // --------------------------------------------------------------- signs
    public sealed class SignSocket
    {
        public string SocketId { get; init; } = "";
        public string SignId { get; init; } = "";
        public string Direction { get; init; } = "";
        public string Label { get; init; } = "";
    }

    public static readonly IReadOnlyList<SignSocket> SignSockets = new List<SignSocket>
    {
        new() { SocketId = "market", SignId = "market", Direction = "west", Label = "MARKET" },
        new() { SocketId = "workshop", SignId = "workshop", Direction = "north", Label = "WORKSHOP" },
        new() { SocketId = "green", SignId = "green", Direction = "south", Label = "GREEN" },
    };

    public static readonly string[] SignIds = { "market", "workshop", "green" };
    public static readonly string[] Directions = { "north", "east", "south", "west" };

    public static string SignLabel(string signId) => signId?.ToUpperInvariant() ?? "";

    public static string ArrowFor(string direction) => direction switch
    {
        "north" => "▲",
        "east" => "▶",
        "south" => "▼",
        "west" => "◀",
        _ => "•",
    };

    public static SignSocket FindSocket(string socketId) =>
        SignSockets.FirstOrDefault(socket => string.Equals(socket.SocketId, socketId,
            StringComparison.Ordinal));

    public static bool SignPlacementMatches(string socketId, string signId, string direction)
    {
        var socket = FindSocket(socketId);
        return socket != null && socket.SignId == signId && socket.Direction == direction;
    }

    // -------------------------------------------------------------- garden
    public static readonly string[] GardenPattern = { "red", "blue", "red", "blue", "red", "blue" };

    public static int GardenCode(string color) => color switch
    {
        "red" => 1,
        "blue" => 2,
        _ => 0,
    };

    public static string GardenColor(int code) => code switch
    {
        1 => "red",
        2 => "blue",
        _ => "empty",
    };

    public static bool GardenPadMatches(int padIndex, string color) =>
        padIndex >= 0 && padIndex < GardenPattern.Length &&
        string.Equals(GardenPattern[padIndex], color, StringComparison.Ordinal);

    // ---------------------------------------------------------------- status
    public static bool M11Complete(GameData data) =>
        CardNpcIds.All(npc => data.GetMissionCounter("M11", $"delivered_{npc}") >= 1);

    public static bool M12Complete(GameData data) =>
        BakeryOrders.All(order => data.GetMissionCounter("M12", $"order_{order.CustomerId}") >= 1);

    public static bool M13Complete(GameData data) =>
        BookIds.All(book => data.GetMissionCounter("M13", ShelfKey(book)) >= 1);

    public static bool M14Complete(GameData data) =>
        SignSockets.All(socket => data.GetMissionCounter("M14", $"sign_{socket.SocketId}") >= 1);

    public static bool M15Complete(GameData data) =>
        Enumerable.Range(0, GardenPattern.Length).All(i =>
            data.GetMissionCounter("M15", $"pad{i}") == GardenCode(GardenPattern[i]));

    public static bool FirstHalfComplete(GameData data) =>
        FirstHalfIds.All(data.IsCompleted);
}
