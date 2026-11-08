using System;

namespace PatchworkIsland;

/// <summary>The only gameplay input owner at any moment.</summary>
public enum ControlMode
{
    Title,
    Walking,
    Driving,
    Building,
    Modal,
}

/// <summary>
/// Pure precedence rules for control ownership. Keeping this small model out of
/// the scene makes save/load and modal transitions directly testable.
/// </summary>
public static class ControlModeRules
{
    public static ControlMode Resolve(bool started, bool modalOpen, bool building, bool driving)
    {
        if (!started) return ControlMode.Title;
        if (modalOpen) return ControlMode.Modal;
        if (building) return ControlMode.Building;
        if (driving) return ControlMode.Driving;
        return ControlMode.Walking;
    }

    public static bool PlayerProcessesInput(ControlMode mode) => mode == ControlMode.Walking;

    public static bool VehicleProcessesInput(ControlMode mode) => mode == ControlMode.Driving;

    public static bool CapturesMouse(ControlMode mode) =>
        mode is ControlMode.Walking or ControlMode.Driving;

    public static bool PausesWorld(ControlMode mode) =>
        mode is ControlMode.Title or ControlMode.Modal;
}
