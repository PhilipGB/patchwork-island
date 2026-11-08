using System;

namespace PatchworkIsland;

/// <summary>
/// Mission-race clock. Restoring an active M05/M09 job must resume from the
/// persisted elapsed milliseconds rather than starting a fresh, shorter time.
///
/// There is no mandatory race time limit. The persisted representation is
/// integer milliseconds; the timer saturates at <see cref="MaxStoredMilliseconds"/>
/// (the largest int) instead of overflowing or wrapping negative. Saturation is
/// far beyond any playable session (about 24.8 days), and an optional speed
/// bonus can never be earned by a long race.
/// </summary>
public sealed class RaceTimer
{
    /// <summary>Largest elapsed value the integer save representation can hold.</summary>
    public const int MaxStoredMilliseconds = int.MaxValue;

    public const double MaxSeconds = MaxStoredMilliseconds / 1000.0;

    public double ElapsedSeconds { get; private set; }

    public static RaceTimer Restore(GameData data, string missionId)
    {
        var timer = new RaceTimer();
        if (data != null && !string.IsNullOrEmpty(missionId))
        {
            int stored = data.GetMissionCounter(missionId, "elapsedMs");
            if (stored < 0) stored = 0;
            timer.ElapsedSeconds = Math.Min(MaxSeconds, stored / 1000.0);
        }
        return timer;
    }

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            return;
        ElapsedSeconds = Math.Min(MaxSeconds, ElapsedSeconds + deltaSeconds);
    }

    public void Restart() => ElapsedSeconds = 0;

    public void Store(GameData data, string missionId)
    {
        if (data == null || string.IsNullOrEmpty(missionId)) return;
        // Round up so a save/reload never reports a shorter race than the
        // player actually drove. Clamp before the integer conversion.
        double milliseconds = Math.Ceiling(ElapsedSeconds * 1000.0);
        if (double.IsNaN(milliseconds) || milliseconds <= 0) milliseconds = 0;
        if (milliseconds > MaxStoredMilliseconds) milliseconds = MaxStoredMilliseconds;
        data.SetMissionCounter(missionId, "elapsedMs", (int)milliseconds);
    }

    public int OptionalBonus(string missionId) =>
        MissionRules.OptionalRaceBonus(missionId, ElapsedSeconds);

    public static string Format(double seconds)
    {
        int total = Math.Max(0, (int)Math.Round(seconds, MidpointRounding.AwayFromZero));
        return $"{total / 60}:{total % 60:00}";
    }
}
