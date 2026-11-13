using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

public partial class RuntimeSelfTest
{
    private async Task RallyChecks()
    {
        Expect(!Main.Probe.Started, "rally test starts on the title screen");
        var started = Main.Probe.StartNewGameFromTitleConfirmed(CharacterCustomisation.Boy);
        await Wait(0.8f);
        Expect(started.Success && Main.Probe.Started, "rally test starts a fresh village");

        Expect(Main.Probe.PrepareRally("M05"), "M05 autopilot prepared");
        await Wait(0.4f);
        Expect(Main.Probe.RallyAutopilot && Main.Probe.Mode == "Driving", "M05 autopilot owns the van");
        float deadline = 60f;
        while (!Main.Probe.MissionCompleted("M05") && deadline > 0f)
        {
            deadline -= 0.5f;
            await Wait(0.5f);
        }
        Expect(Main.Probe.MissionCompleted("M05"), "M05 completes through real driving");
        Expect(Main.Probe.RaceCheckpoint("M05") == 4, "M05 reaches all four gates");
        if (!Main.Probe.MissionCompleted("M05"))
            GD.Print($"RALLY DIAG M05 checkpoint={Main.Probe.RaceCheckpoint("M05")} " +
                     $"van={Main.Probe.VanPosition} driving={Main.Probe.Mode}");
        Main.Probe.StopRallyAutopilot();
        Main.Probe.CloseModal();
        await Wait(0.5f);

        Expect(Main.Probe.PrepareRally("M09"), "M09 autopilot prepared");
        await Wait(0.4f);
        Expect(Main.Probe.RallyAutopilot && Main.Probe.Mode == "Driving", "M09 autopilot owns the van");
        deadline = 60f;
        while (!Main.Probe.MissionCompleted("M09") && deadline > 0f)
        {
            deadline -= 0.5f;
            await Wait(0.5f);
        }
        Expect(Main.Probe.MissionCompleted("M09"), "M09 completes through real driving");
        Expect(Main.Probe.RaceCheckpoint("M09") == 4, "M09 reaches all four gates");
        Main.Probe.StopRallyAutopilot();
        Main.Probe.CloseModal();
        await Wait(0.4f);
    }

}
