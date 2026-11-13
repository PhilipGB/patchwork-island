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
    private async Task TraversalChecks()
    {
        Expect(!Main.Probe.Started, "traversal test starts on the title screen");
        var started = Main.Probe.StartNewGameFromTitleConfirmed(CharacterCustomisation.Boy);
        await Wait(0.8f);
        Expect(started.Success && Main.Probe.Started, "traversal test starts a fresh village");

        Main.Probe.PrepareBridgeCrossing();
        await Wait(0.5f);
        Expect(Main.Probe.Mode == "Driving", "traversal test enters the van");
        Vector3 vanStart = Main.Probe.VanPosition;
        Input.ActionPress("move_forward");
        await Wait(5.0f);
        Input.ActionRelease("move_forward");
        await Wait(0.35f);
        Vector3 vanEnd = Main.Probe.VanPosition;
        Expect(vanEnd.X > 51.5f,
            $"van crosses the bridge and east road seam (x={vanEnd.X:0.0}, start={vanStart.X:0.0})");
        Expect(vanEnd.DistanceTo(vanStart) > 4.5f, "van keeps moving across the bridge");

        Main.Probe.LeaveVanForTest();
        await Wait(0.35f);
        Expect(Main.Probe.Mode == "Walking", "traversal test leaves the van");

        // Slow, pulsing approach: this is where a small raised collision lip
        // would otherwise catch the van at the bridge-to-connector seam.
        Main.Probe.PrepareBridgeCrossing();
        await Wait(0.4f);
        for (int i = 0; i < 14; i++)
        {
            Input.ActionPress("move_forward");
            await Wait(0.18f);
            Input.ActionRelease("move_forward");
            await Wait(0.1f);
        }
        await Wait(0.3f);
        Expect(Main.Probe.VanPosition.X > 51.5f,
            $"slow van crosses the bridge/connector seam (x={Main.Probe.VanPosition.X:0.0})");

        // Check both road edges: a leftover shoulder lip or rail collider must
        // not stop a van that is still inside the 6.4 m carriageway.
        foreach (float offset in new[] { -2.2f, 2.2f })
        {
            Main.Probe.PrepareBridgeCrossingAt(47f, offset, -Mathf.Pi / 2f);
            await Wait(0.4f);
            Vector3 offsetStart = Main.Probe.VanPosition;
            Input.ActionPress("move_forward");
            await Wait(4.0f);
            Input.ActionRelease("move_forward");
            await Wait(0.3f);
            Vector3 offsetEnd = Main.Probe.VanPosition;
            Expect(offsetEnd.X > 58f,
                $"van crosses the bridge at lateral offset z={offset:0.0} (x={offsetEnd.X:0.0})");
            Expect(Mathf.Abs(offsetEnd.Y - offsetStart.Y) < 0.06f,
                $"bridge seam has no vertical step at lateral offset z={offset:0.0}");
        }

        Main.Probe.PrepareBridgeCrossingAt(56f, 0f, Mathf.Pi / 2f);
        await Wait(0.4f);
        Vector3 westStart = Main.Probe.VanPosition;
        Input.ActionPress("move_forward");
        await Wait(2.5f);
        Input.ActionRelease("move_forward");
        await Wait(0.3f);
        Expect(Main.Probe.VanPosition.X < 42f,
            $"van crosses the bridge westbound (x={Main.Probe.VanPosition.X:0.0})");
        Expect(Mathf.Abs(Main.Probe.VanPosition.Y - westStart.Y) < 0.06f,
            "westbound bridge seam has no vertical step");
        Main.Probe.LeaveVanForTest();
        await Wait(0.35f);
        foreach (var gateTest in new[]
                 {
                     ("four-gate", false), ("grand", true),
                 })
        {
            if (gateTest.Item2) Main.Probe.PlacePlayerOutsideGrandGate(0);
            else Main.Probe.PlacePlayerOutsideRaceGate(0);
            await Wait(0.4f);
            Vector3 walkStart = Main.Probe.PlayerPosition;
            Input.ActionPress("move_forward");
            await Wait(2.5f);
            Input.ActionRelease("move_forward");
            await Wait(0.3f);
            Vector3 walkEnd = Main.Probe.PlayerPosition;
            float moved = new Vector2(walkEnd.X - walkStart.X, walkEnd.Z - walkStart.Z).Length();
            Expect(moved > 2.5f,
                $"player walks from grass onto the rally road near the {gateTest.Item1} gate 1 (moved {moved:0.0}m)");
            float beforeJump = Main.Probe.PlayerPosition.Y;
            Input.ActionPress("jump");
            await Wait(0.08f);
            Input.ActionRelease("jump");
            await Wait(0.25f);
            Expect(Main.Probe.PlayerPosition.Y > beforeJump + 0.2f,
                $"player can jump near the {gateTest.Item1} gate 1");
        }

        Main.Probe.PlacePlayerAtRaceGateLip(0);
        await Wait(1.0f);
        Vector3 lipStart = Main.Probe.PlayerPosition;
        Input.ActionPress("move_forward");
        await Wait(2.0f);
        Input.ActionRelease("move_forward");
        await Wait(0.3f);
        float lipMoved = new Vector2(Main.Probe.PlayerPosition.X - lipStart.X,
            Main.Probe.PlayerPosition.Z - lipStart.Z).Length();
        Expect(lipMoved > 1.5f,
            $"player starts from rest at the road lip and walks onto the shoulder (moved {lipMoved:0.0}m)");
    }

    // ---------------------------------------------- v1.2.1 persistence hotfix
    // Reproduces a partial M18 save in the real world, relaunches, and proves
    // the collected tyre does not respawn. Also proves an off-island traffic
    // route enters one terminal stopped state instead of erroring every frame.
}
