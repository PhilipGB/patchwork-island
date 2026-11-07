using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

/// <summary>Pure snap-socket validation, kept independent of Godot nodes.</summary>
public static class BuildRules
{
    public static readonly string[] BridgeSupportSockets = { "support_0", "support_1", "support_2" };
    public static readonly string[] BridgePlankSockets = { "plank_0", "plank_1", "plank_2", "plank_3" };
    public static readonly string[] BridgeSockets = BridgeSupportSockets.Concat(BridgePlankSockets).ToArray();

    public static readonly string[] SwingSockets =
    {
        "s1_rope_l", "s1_rope_r", "s1_seat",
        "s2_rope_l", "s2_rope_r", "s2_seat",
    };

    public static bool IsBridgeSocket(string socketId) => BridgeSockets.Contains(socketId);

    public static bool ValidateBridgePlacement(string pieceType, string socketId,
        IReadOnlyCollection<string> placed, out string message)
    {
        message = "";
        if (!IsBridgeSocket(socketId))
        {
            message = "That is not a bridge socket.";
            return false;
        }
        if (placed.Contains(socketId))
        {
            message = "That piece is already in place.";
            return false;
        }

        bool isSupportSocket = BridgeSupportSockets.Contains(socketId);
        if (pieceType == "support" && !isSupportSocket)
        {
            message = "That long socket needs a plank.";
            return false;
        }
        if (pieceType == "plank" && isSupportSocket)
        {
            message = "That square socket needs a support.";
            return false;
        }
        if (pieceType != "support" && pieceType != "plank")
        {
            message = "Choose a support or a plank.";
            return false;
        }

        int supports = placed.Count(BridgeSupportSockets.Contains);
        int planks = placed.Count(BridgePlankSockets.Contains);

        if (pieceType == "plank" && supports < 3)
        {
            message = "The three supports must go in first.";
            return false;
        }
        if (pieceType == "support" && planks > 0)
        {
            message = "Supports go in before the planks.";
            return false;
        }
        return true;
    }

    public static bool BridgeComplete(IReadOnlyCollection<string> placed)
    {
        var set = new HashSet<string>(placed);
        return BridgeSockets.All(set.Contains);
    }

    public static int BridgeSupportCount(IReadOnlyCollection<string> placed) =>
        placed.Count(BridgeSupportSockets.Contains);

    public static int BridgePlankCount(IReadOnlyCollection<string> placed) =>
        placed.Count(BridgePlankSockets.Contains);

    public static bool ValidateSwingPlacement(string pieceType, string socketId,
        IReadOnlyCollection<string> placed, out string message)
    {
        message = "";
        if (!SwingSockets.Contains(socketId))
        {
            message = "That is not a swing socket.";
            return false;
        }
        if (placed.Contains(socketId))
        {
            message = "That part is already in place.";
            return false;
        }
        bool ropeSocket = socketId.EndsWith("_rope_l") || socketId.EndsWith("_rope_r");
        if (pieceType == "rope" && !ropeSocket)
        {
            message = "That socket is for a seat.";
            return false;
        }
        if (pieceType == "seat" && ropeSocket)
        {
            message = "That socket is for a rope.";
            return false;
        }
        if (pieceType == "seat")
        {
            string swing = socketId.StartsWith("s1") ? "s1" : "s2";
            int ropes = placed.Count(id => id.StartsWith(swing) && id.Contains("rope"));
            if (ropes < 2)
            {
                message = "Hang both ropes before adding the seat.";
                return false;
            }
        }
        return true;
    }

    public static bool SwingComplete(IReadOnlyCollection<string> placed)
    {
        var set = new HashSet<string>(placed);
        return SwingSockets.All(set.Contains);
    }

    public static int CountOf(IReadOnlyCollection<string> placed, IEnumerable<string> ids) =>
        placed.Count(new HashSet<string>(ids).Contains);
}
