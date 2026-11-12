using System;

namespace PatchworkIsland;

public enum AcademyLight
{
    Red,
    RedAmber,
    Green,
    Amber,
}

public sealed class AcademyAssessmentGrade
{
    public bool Passed { get; init; }
    public string Message { get; init; } = "";
    public int MajorErrors { get; init; }
    public int MinorErrors { get; init; }
}

/// <summary>
/// Deterministic academy thresholds shared by the runtime, UI and tests. All
/// speeds are metres per second; 20 mph is 8.9408 m/s.
/// </summary>
public static class DrivingAcademyRules
{
    public const int EnrolmentCoins = 15;
    public const int HelperCoins = 3;
    public const int GraduationCoins = 5;
    public const int MaxMinorOffences = 3;
    public const int HelperConeCount = 3;

    public const float StopSpeedMps = 0.3f;
    public const float StopHoldSeconds = 1.5f;
    public const float ParkingHoldSeconds = 1.5f;
    public const float ParkingRadiusMetres = 3.2f;
    public const float ParkingHeadingToleranceDegrees = 25f;
    public const float ReverseDistanceMetres = 4f;
    public const float SpeedLimitMph = 20f;
    public const float SpeedLimitMps = 8.9408f;
    public const float SpeedWarningMarginMps = 1.0f;
    public const float GateRadiusMetres = 8f;
    public const float CheckpointRadiusMetres = 10f;
    public const float StopZoneRadiusMetres = 9f;

    public static float MphToMps(float mph) => mph * 0.44704f;

    public static bool IsStopped(float speed) => MathF.Abs(speed) <= StopSpeedMps;

    public static bool StopHeld(float speed, float heldSeconds) =>
        IsStopped(speed) && heldSeconds >= StopHoldSeconds;

    public static bool IsParked(float x, float z, float yawDegrees,
        float targetX, float targetZ, float targetYawDegrees)
    {
        float dx = x - targetX;
        float dz = z - targetZ;
        if (MathF.Sqrt(dx * dx + dz * dz) > ParkingRadiusMetres) return false;
        float delta = MathF.Abs(MathfWrapDegrees(yawDegrees - targetYawDegrees));
        // A marked bay has an axis, not a one-way heading: either direction is
        // aligned. This also covers reversing into a bay.
        return delta <= ParkingHeadingToleranceDegrees ||
               MathF.Abs(delta - 180f) <= ParkingHeadingToleranceDegrees;
    }

    public static bool IsSpeeding(float speed) => speed > SpeedLimitMps + SpeedWarningMarginMps;

    public static float MathfWrapDegrees(float degrees)
    {
        degrees %= 360f;
        if (degrees > 180f) degrees -= 360f;
        if (degrees < -180f) degrees += 360f;
        return degrees;
    }

    public static AcademyLight NextLight(AcademyLight light) => light switch
    {
        AcademyLight.Red => AcademyLight.RedAmber,
        AcademyLight.RedAmber => AcademyLight.Green,
        AcademyLight.Green => AcademyLight.Amber,
        _ => AcademyLight.Red,
    };

    /// <summary>Time each phase stays active. Green is long enough for a calm start.</summary>
    public static float LightSeconds(AcademyLight light) => light switch
    {
        AcademyLight.Red => 5f,
        AcademyLight.RedAmber => 2f,
        AcademyLight.Green => 8f,
        _ => 2f,
    };

    public static bool LightMeansStop(AcademyLight light) =>
        light is AcademyLight.Red or AcademyLight.RedAmber or AcademyLight.Amber;

    /// <summary>Red and red+amber prohibit crossing; amber asks a moving car to stop.</summary>
    public static bool LightProhibitsEntry(AcademyLight light) =>
        light is AcademyLight.Red or AcademyLight.RedAmber;

    public static bool LightAllowsEntry(AcademyLight light) => light == AcademyLight.Green;

    /// <summary>
    /// A red-light offence only applies when the front of the car crosses the
    /// stop line after the signal turned red. A car already past the line when
    /// the light changes is not retroactively penalised.
    /// </summary>
    public static bool IsRedLightOffence(AcademyLight light, bool frontPastLine,
        bool wasPastLineWhenRedStarted) =>
        frontPastLine && LightProhibitsEntry(light) && !wasPastLineWhenRedStarted;

    public static AcademyAssessmentGrade GradeAssessment(int majorErrors, int minorErrors)
    {
        bool passed = majorErrors == 0 && minorErrors <= MaxMinorOffences;
        string message;
        if (passed)
            message = minorErrors == 0
                ? "Perfect drive — licence earned!"
                : $"Licence earned with {minorErrors} minor note{(minorErrors == 1 ? "" : "s")}.";
        else if (majorErrors > 0)
            message = "Not yet — a major safety error means a retry. Retries are free.";
        else
            message = $"Not yet — {minorErrors} minor notes. Up to {MaxMinorOffences} are allowed. Retry free.";
        return new AcademyAssessmentGrade
        {
            Passed = passed,
            Message = message,
            MajorErrors = majorErrors,
            MinorErrors = minorErrors,
        };
    }
}
