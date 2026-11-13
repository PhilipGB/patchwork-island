using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

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
    /// <summary>All M01–M10 are complete (the original v1.0/v1.1 chapter).</summary>
    public bool OriginalChapterComplete;
    /// <summary>All M01–M20 are complete (the v1.2 chapter).</summary>
    public bool AllTwentyComplete;
    /// <summary>Elapsed race time captured for the completion panel, when relevant.</summary>
    public double RaceSeconds;

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
/// Result of one authored chapter-two interaction. A partial step has
/// Success=true and no Completion; the final correct step carries the
/// exactly-once MissionCompletionResult for Main to present.
/// </summary>
public sealed class GameplayOutcome
{
    public bool Success;
    public string Message = "";
    public MissionCompletionResult Completion;
}
