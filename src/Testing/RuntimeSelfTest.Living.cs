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
    private async Task LivingChecks()
    {
        Expect(!Main.Probe.Started, "living test starts on the title screen");
        var started = Main.Probe.StartNewGameFromTitleConfirmed(CharacterCustomisation.Boy);
        await Wait(0.8f);
        Expect(started.Success && Main.Probe.Started, "living test starts a fresh village");
        Expect(Main.Probe.NpcCount == 6, "living test sees six villagers");
        Expect(Main.Probe.TrafficCount == 2, "living test sees two drivers");

        string[] npcIds = { "milo", "ada", "pip", "fern", "ollie", "ivy" };
        string[] trafficIds = { "town_scooter", "delivery_car" };
        foreach (string id in npcIds)
        {
            Vector3 position = Main.Probe.NpcPosition(id);
            Expect(Finite(position), $"villager '{id}' has a finite position");
            Expect(WorldAnchors.DistanceToNearestStation(position.X, position.Z) >= 3.3f,
                $"villager '{id}' does not block a mission station");
        }
        foreach (string id in trafficIds)
        {
            Expect(Main.Probe.TrafficOnRoad(id), $"traffic '{id}' starts on the road");
            Expect(Main.Probe.TrafficHasEngineAudio(id), $"traffic '{id}' has bounded engine audio");
        }

        var lastPositions = new Dictionary<string, Vector3>();
        var startDistances = new Dictionary<string, float>();
        foreach (string id in trafficIds)
        {
            lastPositions[id] = Main.Probe.TrafficPosition(id);
            startDistances[id] = Main.Probe.TrafficDistance(id);
        }

        for (int second = 1; second <= 24; second++)
        {
            await Wait(1f);
            foreach (string id in trafficIds)
            {
                Vector3 position = Main.Probe.TrafficPosition(id);
                Expect(Finite(position), $"traffic '{id}' position is finite at {second}s");
                Expect(Main.Probe.TrafficOnRoad(id), $"traffic '{id}' stays on the road at {second}s");
                float gap = position.DistanceTo(lastPositions[id]);
                Expect(gap < 5.5f, $"traffic '{id}' does not teleport (gap {gap:0.0}m at {second}s)");
                Expect(WorldAnchors.DistanceToNearestStation(position.X, position.Z) >= 1.7f,
                    $"traffic '{id}' keeps clear of critical stations at {second}s");
                lastPositions[id] = position;
            }
        }

        foreach (string id in trafficIds)
        {
            float travelled = Main.Probe.TrafficDistance(id) - startDistances[id];
            Expect(travelled > 45f, $"traffic '{id}' completes a sustained route ({travelled:0.0}m)");
        }
        Main.Probe.ResetPlayerToSpawn();
        await Wait(0.5f);

        foreach (string id in trafficIds)
        {
            Main.Probe.TeleportPlayerAheadOfTraffic(id, 4f);
            await Wait(2.4f);
            Expect(Main.Probe.TrafficYielding(id), $"traffic '{id}' yields to the player");
            Expect(Main.Probe.TrafficSpeed(id) < 0.9f, $"traffic '{id}' stops safely for the player");
            Main.Probe.ResetPlayerToSpawn();
            await Wait(1.4f);
            Expect(Main.Probe.TrafficSpeed(id) > 0.15f, $"traffic '{id}' resumes after yielding");
        }

        foreach (string id in npcIds)
        {
            Vector3 position = Main.Probe.NpcPosition(id);
            Expect(Finite(position), $"villager '{id}' still has a finite position");
            Expect(WorldAnchors.DistanceToNearestStation(position.X, position.Z) >= 3.3f,
                $"villager '{id}' still keeps clear of mission stations");
            Expect(Main.Probe.NpcProcessMode(id) == ProcessModeEnum.Pausable,
                $"villager '{id}' remains pausable");
        }
    }

    // ------------------------------------------------ chapter two integration
    // Phase 0 starts a fresh village, builds the same valid world state as a
    // finished v1.1 save, then uses the real job board, NPC dialogues and UI
    // buttons to deliver one M11 card before saving and relaunching. Phase 1
    // continues from that save and plays M11-M15 through their real stations,
    // pickups, dialogues and buttons, proving progress and rewards survive.
}
