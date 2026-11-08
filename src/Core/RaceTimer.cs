using System;

namespace PatchworkIsland;

/// <summary>
/// Mission-race clock. Restoring an active M05/M09 job must resume from the
/// persisted elapsed milliseconds rather than starting a fresh, shorter time.
/// </summary>
public sealed class RaceTimer
{
    public float ElapsedSeconds { get; private set; }

    public static RaceTimer Restore(GameData data, string missionId)
    {
        var timer = new RaceTimer();
        if (data != null && !string.IsNullOrEmpty(missionId))
            timer.ElapsedSeconds = Math.Max(0, data.GetMissionCounter(missionId, "elapsedMs")) / 1000f;
        return timer;
    }

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds > 0f && !float.IsNaN(deltaSeconds) && !float.IsInfinity(deltaSeconds))
            ElapsedSeconds += deltaSeconds;
    }

    public void Restart() => ElapsedSeconds = 0f;

    public void Store(GameData data, string missionId)
    {
        if (data == null || string.IsNullOrEmpty(missionId)) return;
        int milliseconds = (int)Math.Round(ElapsedSeconds * 1000f, MidpointRounding.AwayFromZero);
        data.SetMissionCounter(missionId, "elapsedMs", Math.Max(0, milliseconds));
    }

    public int OptionalBonus(string missionId) =>
        MissionRules.OptionalRaceBonus(missionId, ElapsedSeconds);

    public static string Format(float seconds)
    {
        int total = Math.Max(0, (int)Math.Round(seconds, MidpointRounding.AwayFromZero));
        return $"{total / 60}:{total % 60:00}";
    }
}
