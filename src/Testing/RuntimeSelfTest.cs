using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Godot runtime verification for pause/processing and modal state. Run with:
///   godot --headless --path . -- --selftest
///   godot --headless --path . -- --selftest-resume
///   godot --headless --path . -- --selftest-recovery
/// It uses isolated user://selftest*.json data and exits non-zero on failure.
/// </summary>
public partial class RuntimeSelfTest : Node
{
    public Main Main { get; set; }

    // Static because the recovery and chapter phases deliberately reload the
    // real scene. The chapter phase proves a mid-mission save survives relaunch.
    internal static int RecoveryPhase { get; set; }
    internal static int ChapterPhase { get; set; }
    internal static int HotfixPhase { get; set; }
    internal static int ReleasePhase { get; set; }
    internal static int RestorePhase { get; set; }

    private static int _passed;
    private static int _failed;
    private static Vector3 _savedPlayerPosition;
    private static byte[] _seedBytes;
    private static byte[] _newerOriginalBytes;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _ = Run();
    }

    private async Task Run()
    {
        bool reloading = false;
        try
        {
            await Wait(0.7f);
            if (HasArg("--selftest-resume"))
                await ResumeChecks();
            else if (HasArg("--selftest-recovery"))
                reloading = await RecoveryChecks();
            else if (HasArg("--selftest-living"))
                await LivingChecks();
            else if (HasArg("--selftest-rally"))
                await RallyChecks();
            else if (HasArg("--selftest-chapter"))
                reloading = await ChapterChecks();
            else if (HasArg("--selftest-hotfix"))
                reloading = await HotfixChecks();
            else if (HasArg("--selftest-finale"))
                await FinaleChecks();
            else if (HasArg("--selftest-traversal"))
                await TraversalChecks();
            else if (HasArg("--selftest-release"))
                reloading = await ReleaseChecks();
            else if (HasArg("--selftest-restore"))
                reloading = await RestoreChecks();
            else
                await FreshSessionChecks();
        }
        catch (Exception ex)
        {
            Fail("unexpected runtime-test exception: " + ex);
        }

        if (reloading) return;
        Main.Probe.StopAudio();
        await Wait(0.6f);
        GD.Print($"RUNTIME RESULT: {_passed} passed, {_failed} failed");
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    private void CheckBytes(string path, byte[] expected, string name)
    {
        try
        {
            Expect(File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(expected), name);
        }
        catch (Exception ex)
        {
            Fail(name + ": " + ex.Message);
        }
    }

    private async Task Wait(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds, processAlways: true),
            SceneTreeTimer.SignalName.Timeout);
    }

    private static bool HasArg(string needle)
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg == needle) return true;
        return false;
    }

    private void Expect(bool condition, string name)
    {
        if (condition)
        {
            _passed++;
            GD.Print("PASS  " + name);
        }
        else
        {
            Fail(name);
        }
    }

    private void Fail(string name)
    {
        _failed++;
        GD.PrintErr("FAIL  " + name);
    }
}
