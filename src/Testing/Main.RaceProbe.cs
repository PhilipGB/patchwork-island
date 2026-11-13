using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

public partial class Main
{
    internal sealed partial class RuntimeProbe
    {
        internal bool PrepareRace()
        {
            // Build a valid prerequisite state, including the physical bridge so
            // the runtime test does not create a save the validator must reject.
            if (!_game._data.CompletedMissions.Contains("M02")) _game._data.CompletedMissions.Add("M02");
            if (!_game._data.CompletedMissions.Contains("M03"))
            {
                _game._data.CompletedMissions.Add("M03");
                foreach (string socket in BuildRules.BridgeSockets)
                    if (!_game._data.BridgePieces.Contains(socket)) _game._data.BridgePieces.Add(socket);
            }
            if (!_game._missions.StartMission("M05").Success) return false;
            _game._data.SetMissionCounter("M05", "checkpoint", 1);
            _game._data.SetMissionCounter("M05", "elapsedMs", 12_500);
            _game._raceTimer = RaceTimer.Restore(_game._data, "M05");
            return true;
        }

        internal void PlaceVanAtRallyStart()
        {
            if (_game._driving)
            {
                _game._driving = false;
                _game._van.SetDriven(false);
                _game._player.Visible = true;
            }
            _game._van.StopAutopilot();
            var start = RallyCourse.Sample(0f);
            _game._van.GlobalPosition = new Vector3(start.X, 0.2f, start.Z);
            _game._van.Rotation = new Vector3(0, 0, 0);
            _game._van.Velocity = Vector3.Zero;
            _game._player.Teleport(_game._van.GlobalPosition + new Vector3(-2.6f, 0.1f, 0f), 0f);
            _game._vehicleSwitchCooldown = 0f;
            _game.EnterVan();
        }

        internal bool PrepareRally(string missionId)
        {
            if (missionId is not ("M05" or "M09" or "M19")) return false;
            foreach (string id in new[] { "M01", "M02", "M03", "M04" })
                if (!_game._data.CompletedMissions.Contains(id)) _game._data.CompletedMissions.Add(id);
            foreach (string socket in BuildRules.BridgeSockets)
                if (!_game._data.BridgePieces.Contains(socket)) _game._data.BridgePieces.Add(socket);
            if (missionId == "M09" && !_game._data.CompletedMissions.Contains("M05"))
                _game._data.CompletedMissions.Add("M05");
            if (!_game._missions.StartMission(missionId).Success) return false;
            _game._raceTimer = new RaceTimer();
            PlaceVanAtRallyStart();
            if (!_game._driving) return false;
            _game._van.StartRallyAutopilot();
            _game.RefreshMissionState();
            return true;
        }

        internal void StopRallyAutopilot() => _game._van?.StopAutopilot();
        internal bool MissionCompleted(string id) => _game._data.IsCompleted(id);
        internal bool AllTwentyComplete => _game._data.IsCompleted("M20");
        internal MissionStatus MissionStatus(string id) => _game._missions.GetStatus(id);
        internal int MissionCounter(string id, string key) => _game._data.GetMissionCounter(id, key);
        internal bool CelebrationSeen => _game._data.CelebrationSeen;
        internal bool FestivalSeen => _game._data.FestivalSeen;
        internal int SignSocketCount => _game._world?.SignSocketPositions.Count ?? 0;
        internal int GardenPadCount => _game._world?.GardenPads.Count ?? 0;
        internal int BakerySlotCount => _game._world?.BakeryDisplaySlots.Count ?? 0;
        internal int LibrarySlotCount => _game._world?.LibraryShelfSlots.Count ?? 0;
        internal bool BakerySlotVisible(int index) =>
            _game._world != null && index >= 0 && index < _game._world.BakeryDisplaySlots.Count &&
            _game._world.BakeryDisplaySlots[index].Visible;
        internal bool LibrarySlotVisible(int index) =>
            _game._world != null && index >= 0 && index < _game._world.LibraryShelfSlots.Count &&
            _game._world.LibraryShelfSlots[index].Visible;
        internal bool SignVisualVisible(int index) =>
            index >= 0 && index < _game._signVisuals.Count && _game._signVisuals[index].Visible;
        internal bool GardenFlowerVisible(int index) =>
            index >= 0 && index < _game._gardenFlowerRoots.Count && _game._gardenFlowerRoots[index].Visible;
        internal bool DeliveryBoardVisible => _game._world?.DeliveryBoard?.Visible == true;
        internal bool MarketStockVisible => _game._world?.MarketStock?.Visible == true;
        internal bool PipCarFixedVisible => _game._world?.PipCarFixed?.Visible == true;
        internal bool RallyPodiumVisible => _game._world?.RallyPodium?.Visible == true;
        internal bool FestivalDecorVisible => _game._world?.FestivalDecor?.Visible == true;
        internal bool FestivalTableVisible(int index) =>
            _game._world != null && index >= 0 && index < _game._world.FestivalTableVisuals.Count &&
            _game._world.FestivalTableVisuals[index].Visible;
        internal bool FestivalLanternVisible(int index) =>
            _game._world != null && index >= 0 && index < _game._world.FestivalLanternVisuals.Count &&
            _game._world.FestivalLanternVisuals[index].Visible;
        internal bool RallyRivalPresent => _game._rallyRival != null && GodotObject.IsInstanceValid(_game._rallyRival);
        internal Vector3 RallyRivalPosition =>
            _game._rallyRival != null && GodotObject.IsInstanceValid(_game._rallyRival)
                ? _game._rallyRival.GlobalPosition
                : new Vector3(9999, 9999, 9999);
        internal bool RallyAutopilot => _game._van?.Autopilot == true;
        internal int RaceCheckpoint(string id) => _game._data.GetMissionCounter(id, "checkpoint");
        internal int RallySampleCount => _game._world?.RallySampleCount ?? 0;
        internal float RallyLength => _game._world?.RallyLength ?? 0f;

        internal int RallyCollisionCoverage()
        {
            if (_game._world == null) return 0;
            var samples = RallyCourse.SampleClosed(4);
            var space = _game.GetWorld3D().DirectSpaceState;
            int hits = 0;
            foreach (var sample in samples)
            {
                using var query = PhysicsRayQueryParameters3D.Create(
                    new Vector3(sample.X, 2.5f, sample.Z),
                    new Vector3(sample.X, -1f, sample.Z), 1u);
                var result = space.IntersectRay(query);
                if (result.Count == 0) continue;
                var collider = result["collider"].AsGodotObject();
                // Collision is intentionally flush with the ground now; prove the
                // ray hit the rally mesh, or the town-road collider where the two
                // authored surfaces overlap, rather than the island lawn.
                if (collider is StaticBody3D body &&
                    (body.Name == "RallyRoadCollision" || body.Name == "TownRoadCollision"))
                    hits++;
            }
            return hits;
        }

        internal void EnterVan()
        {
            _game._vehicleSwitchCooldown = 0f;
            _game._player.Teleport(_game._van.GlobalPosition + new Vector3(-2.6f, 0.1f, 0f), _game._van.Rotation.Y);
            _game.EnterVan();
        }

        internal void ExitVan() => _game.ExitVan();

        internal void LeaveVanForTest()
        {
            if (!_game._driving) return;
            _game._van.SetDriven(false);
            _game._driving = false;
            _game._player.Visible = true;
            _game._player.Teleport(_game._van.GlobalPosition + new Vector3(-2.6f, 0.15f, 0f), _game._van.Rotation.Y);
            _game._player.SetControlEnabled(true);
            _game._rig.SetTarget(_game._player, false, 8.2f, 18f, 1.55f, true);
            _game._vehicleSwitchCooldown = 0f;
            _game.SyncControlMode();
            _game.RefreshMissionState();
        }

    }
}
