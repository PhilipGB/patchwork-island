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
        internal int NpcCount => _game._npcs.Count;
        internal string NpcIds => string.Join(",", _game._npcs.Select(n => n.NpcId));
        internal string NpcNames => string.Join(", ", _game._npcs.Select(n => n.Definition.Name));
        internal bool NpcPresent(string id) => _game._npcById.ContainsKey(id);
        internal Vector3 NpcPosition(string id) =>
            _game._npcById.TryGetValue(id, out var npc) ? npc.GlobalPosition : new Vector3(9999, 9999, 9999);
        internal string NpcNameLabel(string id) =>
            _game._npcById.TryGetValue(id, out var npc) ? npc.NameLabelText : "";
        internal bool NpcTalking(string id) => _game._npcById.TryGetValue(id, out var npc) && npc.Talking;
        internal bool NpcMoving(string id) => _game._npcById.TryGetValue(id, out var npc) && npc.IsMoving;
        internal ProcessModeEnum NpcProcessMode(string id) =>
            _game._npcById.TryGetValue(id, out var npc) ? npc.ProcessMode : ProcessModeEnum.Inherit;

        internal void TeleportPlayerToClothesDoor()
        {
            _game._player.Teleport(_game._world.ClothesShopDoor + new Vector3(0f, 0.1f, -1.1f));
            _game._player.ForceRefreshInteraction();
        }

        internal void TeleportPlayerNearNpc(string id)
        {
            if (!_game._npcById.TryGetValue(id, out var npc)) return;
            Vector3 p = npc.GlobalPosition + new Vector3(1.7f, 0.1f, 0f);
            _game._player.Teleport(p, Mathf.Pi / 2f);
            _game._player.ForceRefreshInteraction();
        }

        internal bool TalkToNpc(string id)
        {
            if (!_game._npcById.TryGetValue(id, out var npc)) return false;
            _game.Population.TalkToNpc(npc);
            return _game._hud.ModalOpen;
        }

        internal string NearestInteractableName => _game._player?.NearestInteractable?.Name.ToString() ?? "";

        internal bool InteractNearest()
        {
            var target = _game._player?.NearestInteractable;
            if (target == null || !target.IsAvailable()) return false;
            target.Interact();
            return true;
        }

        internal void TeleportNearPickup(string pickupId)
        {
            var pickup = _game._pickups.FirstOrDefault(p => p.PickupId == pickupId);
            if (pickup == null) return;
            _game._player.Teleport(pickup.GlobalPosition + new Vector3(1.4f, 0.15f, 0f), -Mathf.Pi / 2f);
            _game._player.ForceRefreshInteraction();
        }

        internal int BookPickupCount => _game._pickups.Count(p => p.MissionId == "M13");

        internal void TeleportNearSignSocket(int index)
        {
            if (_game._world == null || index < 0 || index >= _game._world.SignSocketPositions.Count) return;
            _game._player.Teleport(_game._world.SignSocketPositions[index] + new Vector3(0f, 0.15f, 1.45f), 0f);
            _game._player.ForceRefreshInteraction();
        }

        internal void TeleportNearGardenPad(int index)
        {
            if (_game._world == null || index < 0 || index >= _game._world.GardenPads.Count) return;
            _game._player.Teleport(_game._world.GardenPads[index] + new Vector3(0f, 0.15f, 0.8f), 0f);
            _game._player.ForceRefreshInteraction();
        }

        internal void TeleportNearJobBoard()
        {
            if (_game._jobStation == null || _game._player == null) return;
            _game._player.Teleport(_game._jobStation.GlobalPosition + new Vector3(0f, 0.1f, 1.5f), 0f);
            _game._player.ForceRefreshInteraction();
        }

        internal int TrafficCount => _game._trafficCars.Count;
        internal string TrafficIds => string.Join(",", _game._trafficCars.Select(n => n.RouteId));
        internal Vector3 TrafficPosition(string id) =>
            _game._trafficCars.FirstOrDefault(c => c.RouteId == id)?.GlobalPosition ?? new Vector3(9999, 9999, 9999);
        internal bool TrafficOnRoad(string id)
        {
            var car = _game._trafficCars.FirstOrDefault(c => c.RouteId == id);
            return car != null && RoadNetwork.Contains(car.GlobalPosition.X, car.GlobalPosition.Z);
        }
        internal bool TrafficYielding(string id) =>
            _game._trafficCars.FirstOrDefault(c => c.RouteId == id)?.Yielding == true;
        internal float TrafficSpeed(string id) =>
            _game._trafficCars.FirstOrDefault(c => c.RouteId == id)?.PlanarSpeed ?? -1f;
        internal float TrafficDistance(string id) =>
            _game._trafficCars.FirstOrDefault(c => c.RouteId == id)?.DistanceTravelled ?? -1f;
        internal int TrafficWaypoint(string id) =>
            _game._trafficCars.FirstOrDefault(c => c.RouteId == id)?.CurrentWaypoint ?? -1;
        internal bool TrafficHasEngineAudio(string id) =>
            _game._trafficCars.FirstOrDefault(c => c.RouteId == id)?.HasEngineAudio == true;
        internal string PlayerVisualTemplate => _game._player?.VisualTemplatePath ?? "";
        internal string VanVisualTemplate => _game._van?.VisualTemplatePath ?? "";
        internal string NpcVisualTemplate(string id) =>
            _game._npcById.TryGetValue(id, out NpcActor npc) ? npc.VisualTemplatePath : "";
        internal string TrafficVisualTemplate(string id) =>
            _game._trafficCars.FirstOrDefault(c => c.RouteId == id)?.VisualTemplatePath ?? "";
        internal string PickupVisualTemplates =>
            string.Join(",", _game._pickups
                .Where(p => !string.IsNullOrEmpty(p.VisualTemplatePath))
                .Select(p => p.ItemType + "=" + p.VisualTemplatePath.GetFile())
                .Distinct()
                .OrderBy(text => text, StringComparer.Ordinal));
        internal bool TrafficStopped(string id) =>
            _game._trafficCars.FirstOrDefault(c => c.RouteId == id)?.Stopped == true;
        internal int TrafficStopEvents(string id) =>
            _game._trafficCars.FirstOrDefault(c => c.RouteId == id)?.StopEvents ?? -1;

        internal void DropTrafficOffIsland(string id)
        {
            var car = _game._trafficCars.FirstOrDefault(c => c.RouteId == id);
            if (car == null) return;
            car.GlobalPosition = new Vector3(car.GlobalPosition.X, -6f, car.GlobalPosition.Z);
            car.Velocity = Vector3.Zero;
        }

        internal bool PickupPresent(string pickupId) =>
            _game._pickups.Any(p => p.PickupId == pickupId);
        internal int RepairPickupCount => _game._pickups.Count(p => p.MissionId == "M18");
        internal bool CollectedPickup(string pickupId) => _game._data.HasCollected(pickupId);

        internal void TeleportPlayerAheadOfTraffic(string id, float distance)
        {
            var car = _game._trafficCars.FirstOrDefault(c => c.RouteId == id);
            if (car == null) return;
            Vector3 forward = -car.GlobalTransform.Basis.Z;
            forward.Y = 0;
            forward = forward.Normalized();
            _game._player.Teleport(car.GlobalPosition + forward * distance + Vector3.Up * 0.1f,
                car.Rotation.Y + Mathf.Pi);
            _game._player.ForceRefreshInteraction();
        }

        internal void ResetPlayerToSpawn()
        {
            _game._player.Teleport(_game._world.PlayerSpawn, -Mathf.Pi / 2f);
            _game._player.ForceRefreshInteraction();
        }
        internal bool SfxEnabled => _game._data.Settings.SfxEnabled;

        internal void StopAudio()
        {
            _game._van?.StopEngineAudio();
            _game._audio?.StopAll();
        }

        internal void SetMusicEnabled(bool enabled)
        {
            _game._data.Settings.MusicEnabled = enabled;
            _game.ApplySettings();
        }

        internal void SetSfxEnabled(bool enabled)
        {
            _game._data.Settings.SfxEnabled = enabled;
            _game.ApplySettings();
        }

        internal void SetPlayerAirborne()
        {
            _game._player.Velocity = Vector3.Zero;
            _game._player.Teleport(_game._player.GlobalPosition + Vector3.Up * 3f);
        }

    }
}
