using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>Developer-only academy frame-interval probe (same method as the city probe).</summary>
public partial class DrivingAcademyPerf : Node
{
    public Main Main { get; set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _ = Run();
    }

    private async Task Run()
    {
        try
        {
            bool high = OS.GetCmdlineUserArgs().Contains("--capture-1080");
            if (high)
            {
                Main.GetWindow().ContentScaleSize = new Vector2I(1920, 1080);
                Main.GetWindow().Size = new Vector2I(1920, 1080);
            }
            Main.Probe.StartNewGameFromTitleConfirmed("boy");
            await Wait(1.0f);
            var data = Main.Probe.Data;
            var service = Main.RtSession.Academy;
            var academy = Main.RtAcademy;
            string res = high ? "1920x1080" : "1280x720";
            await Measure($"town-only {res}", 300);
            data.Coins = 15;
            service.Enrol();
            service.StartLesson("L01");
            academy.EnterStage("L01", false);
            await Wait(1.0f);
            await Measure($"academy-course {res}", 600);
            academy.ExitVenue(false);
            await Wait(1.0f);
            await Measure($"town-return {res}", 300);
        }
        catch (Exception ex)
        {
            GD.PrintErr("ACADEMY PERF FAIL: " + ex);
            GetTree().Quit(1);
            return;
        }
        GetTree().Quit();
    }

    private async Task Measure(string label, int frames)
    {
        var values = new List<double>(frames);
        ulong previous = Time.GetTicksUsec();
        for (int i = 0; i < frames; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            ulong now = Time.GetTicksUsec();
            values.Add((now - previous) / 1000.0);
            previous = now;
        }
        values.Sort();
        double p50 = values[values.Count / 2];
        double p95 = values[(int)(values.Count * 0.95)];
        double max = values[^1];
        GD.Print($"ACADEMY PERF {label} p50_ms={p50:0.00} p95_ms={p95:0.00} " +
                 $"max_ms={max:0.00} frames={values.Count} nodes={GetTree().GetNodeCount()} " +
                 $"draw_calls={(int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} " +
                 $"primitives={(int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)}");
    }

    private async Task Wait(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds, processAlways: true),
            SceneTreeTimer.SignalName.Timeout);
}
