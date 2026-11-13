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

    /// <summary>
    /// Relative heading from the car to a target, in radians, wrapped to
    /// (-pi, pi]. Zero means straight ahead. Positive is the car's left,
    /// negative is the car's right (Godot convention: yaw 0 faces -Z and
    /// positive yaw turns towards -X, so a target to the right of a car
    /// facing -Z has a negative relative heading).
    /// </summary>
    public static float GuidanceRelativeRadians(float toX, float toZ, float yawRadians)
    {
        float desired = MathF.Atan2(-toX, -toZ);
        float relative = (desired - yawRadians) % (2f * MathF.PI);
        if (relative > MathF.PI) relative -= 2f * MathF.PI;
        if (relative < -MathF.PI) relative += 2f * MathF.PI;
        return relative;
    }

    /// <summary>
    /// Wayfinding label for a relative heading from
    /// <see cref="GuidanceRelativeRadians"/>. Sign convention: positive is the
    /// car's left, negative is the car's right.
    /// </summary>
    public static string GuidanceDirection(float relativeRadians) => relativeRadians switch
    {
        > -0.6f and < 0.6f => "straight ahead",
        >= 0.6f and < 2.0f => "ahead left",
        <= -0.6f and > -2.0f => "ahead right",
        >= 2.0f => "behind, turn left",
        _ => "behind, turn right",
    };

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

    // One second to react, then the learner controller's 11 m/s² braking.
    // Evaluated once at amber onset. A stopped car never gets an exception.
    public static bool AmberEntryUnavoidable(float distance, float approachSpeed) =>
        distance >= 0f && approachSpeed > StopSpeedMps &&
        distance < approachSpeed + approachSpeed * approachSpeed / (2f * 11f);

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
