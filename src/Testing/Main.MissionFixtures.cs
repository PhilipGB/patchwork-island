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
        internal void CompleteM01()
        {
            if (!_game._missions.StartMission("M01").Success) return;
            for (int i = 0; i < 3; i++)
                _game._missions.CollectPickup($"apple_{i}", "apple", "M01", 3);
            var result = _game._missions.DeliverApples();
            if (result.Success) _game.OnMissionCompleted(result);
        }

        /// <summary>
        /// Builds the same valid world state a v1.1 player has after finishing the
        /// original ten jobs. Runtime verification uses this only to reach chapter
        /// two; the chapter-two interactions afterwards all go through real buttons,
        /// stations, pickups and mission rules.
        /// </summary>
        internal void PrepareChapterTwo()
        {
            foreach (string id in MissionRules.OriginalMissionIds)
                if (!_game._data.CompletedMissions.Contains(id)) _game._data.CompletedMissions.Add(id);
            foreach (string socket in BuildRules.BridgeSockets)
                if (!_game._data.BridgePieces.Contains(socket)) _game._data.BridgePieces.Add(socket);
            foreach (string socket in BuildRules.SwingSockets)
                if (!_game._data.SwingPieces.Contains(socket)) _game._data.SwingPieces.Add(socket);
            if (ParkRules.Count(_game._data, "bench") == 0) ParkRules.Place(_game._data, "bench", "park0");
            if (ParkRules.Count(_game._data, "flowerbed") == 0) ParkRules.Place(_game._data, "flowerbed", "park1");
            if (ParkRules.Count(_game._data, "lamp") == 0) ParkRules.Place(_game._data, "lamp", "park2");
            FurnitureRules.GrantFreeStarterSet(_game._data);
            _game._data.CelebrationSeen = true;
            _game._data.Coins = 100;
            _game._data.ActiveMission = "";
            _game._world.CelebrationDecor.Visible = true;
            _game.RefreshMissionState();
            _game.RefreshChapterTwoWorld();
            _game._hud.SetCoins(_game._data.Coins);
        }

        /// <summary>
        /// Builds the valid world state for a player who has finished M11-M15, so
        /// the P14 runtime phase can exercise M16-M20 through their real stations
        /// without replaying the first chapter-two half.
        /// </summary>
        internal void PrepareFinale()
        {
            PrepareChapterTwo();
            foreach (string id in new[] { "M11", "M12", "M13", "M14", "M15" })
                if (!_game._data.CompletedMissions.Contains(id)) _game._data.CompletedMissions.Add(id);

            _game._data.SetMissionCounter("M11", "stock_granted", 1);
            foreach (string npc in ChapterTwoRules.CardNpcIds)
            {
                _game._data.SetMissionCounter("M11", $"card_{npc}", 0);
                _game._data.SetMissionCounter("M11", $"delivered_{npc}", 1);
            }
            foreach (var order in ChapterTwoRules.BakeryOrders)
                _game._data.SetMissionCounter("M12", $"order_{order.CustomerId}", 1);
            foreach (string book in ChapterTwoRules.BookIds)
            {
                string pickup = ChapterTwoRules.BookPickupId(book);
                if (!_game._data.CollectedPickups.Contains(pickup)) _game._data.CollectedPickups.Add(pickup);
                _game._data.SetMissionCounter("M13", ChapterTwoRules.HasBookKey(book), 0);
                _game._data.SetMissionCounter("M13", ChapterTwoRules.ShelfKey(book), 1);
            }
            foreach (var socket in ChapterTwoRules.SignSockets)
                _game._data.SetMissionCounter("M14", $"sign_{socket.SocketId}", 1);
            for (int i = 0; i < ChapterTwoRules.GardenPattern.Length; i++)
                _game._data.SetMissionCounter("M15", $"pad{i}",
                    ChapterTwoRules.GardenCode(ChapterTwoRules.GardenPattern[i]));

            _game._data.ActiveMission = "";
            _game.RefreshMissionState();
        }

        /// <summary>
        /// Prepares a valid post-M17 village so the v1.2.1 regression can start the
        /// real M18 job and exercise a partial save/reload without replaying M16-M17.
        /// </summary>
        internal void PrepareMechanicJob()
        {
            PrepareFinale();
            foreach (string id in new[] { "M16", "M17" })
                if (!_game._data.CompletedMissions.Contains(id)) _game._data.CompletedMissions.Add(id);
            _game._data.SetMissionCounter("M16", "loaded", 1);
            foreach (string npc in FinaleRules.DeliveryRecipients)
            {
                _game._data.SetMissionCounter("M16", FinaleRules.CarryingKey(npc), 0);
                _game._data.SetMissionCounter("M16", FinaleRules.DeliveredKey(npc), 1);
            }
            _game._data.SetMissionCounter("M17", "finalised", 1);
            _game._data.ActiveMission = "";
            _game.RefreshMissionState();
        }

        internal void TeleportVanNearNpc(string id)
        {
            if (!_game._npcById.TryGetValue(id, out var npc)) return;
            _game._van.GlobalPosition = npc.GlobalPosition + new Vector3(6.5f, 0.15f, 0f);
            _game._van.Rotation = Vector3.Zero;
            _game._van.Velocity = Vector3.Zero;
            _game._player.Teleport(npc.GlobalPosition + new Vector3(1.7f, 0.1f, 0f), Mathf.Pi / 2f);
            _game._player.ForceRefreshInteraction();
        }

        internal void BuildBridgeNow()
        {
            foreach (string id in BuildRules.BridgeSockets)
                if (!_game._data.BridgePieces.Contains(id)) _game._data.BridgePieces.Add(id);
            _game._bridgeBuilder?.RefreshFromData();
        }

        internal void PrepareBridgeCrossing() =>
            PrepareBridgeCrossingAt(47.0f, 0f, -Mathf.Pi / 2f);

        internal void PrepareBridgeCrossingAt(float x, float z, float yaw)
        {
            LeaveVanForTest();
            BuildBridgeNow();
            _game._van.GlobalPosition = new Vector3(x, 0.15f, z);
            _game._van.Rotation = new Vector3(0f, yaw, 0f);
            _game._van.Velocity = Vector3.Zero;
            _game._player.Teleport(new Vector3(x - 2.3f, 0.15f, z), yaw);
            _game._vehicleSwitchCooldown = 0f;
            _game.EnterVan();
            _game.RefreshMissionState();
        }

        internal void PlacePlayerOutsideGrandGate(int index)
        {
            if (_game._world == null || index < 0 || index >= _game._world.M19RaceGates.Length) return;
            PlacePlayerOutsideGate(_game._world.M19RaceGates[index].Position,
                RallyCourse.SixGateFractions[index], 6.2f);
        }

        internal Vector3 RaceGatePosition(int index) =>
            _game._world != null && index >= 0 && index < _game._world.RaceGates.Length
                ? _game._world.RaceGates[index].Position
                : new Vector3(9999, 9999, 9999);

        internal Vector3 GrandGatePosition(int index) =>
            _game._world != null && index >= 0 && index < _game._world.M19RaceGates.Length
                ? _game._world.M19RaceGates[index].Position
                : new Vector3(9999, 9999, 9999);

        internal void PlacePlayerOutsideRaceGate(int index)
        {
            if (_game._world == null || index < 0 || index >= _game._world.RaceGates.Length) return;
            PlacePlayerOutsideGate(_game._world.RaceGates[index].Position,
                RallyCourse.FourGateFractions[index], 6.2f);
        }

        internal void PlacePlayerAtRaceGateLip(int index)
        {
            if (_game._world == null || index < 0 || index >= _game._world.RaceGates.Length) return;
            PlacePlayerOutsideGate(_game._world.RaceGates[index].Position,
                RallyCourse.FourGateFractions[index], 5.55f);
        }

        private void PlacePlayerOutsideGate(Vector3 gate, float fraction, float distance)
        {
            var tangent = RallyCourse.Tangent(fraction);
            var left = new Vector3(-tangent.Z, 0f, tangent.X);
            Vector3 start = gate + left * distance + new Vector3(0f, 0.15f, 0f);
            float yaw = Mathf.Atan2(-(gate.X - start.X), -(gate.Z - start.Z));
            _game._player.Teleport(start, yaw);
            _game._player.ForceRefreshInteraction();
        }

        internal void TeleportNearPostCounter()
        {
            if (_game._postStation == null) return;
            _game._player.Teleport(_game._postStation.GlobalPosition + new Vector3(0f, 0.1f, -1.2f), 0f);
            _game._player.ForceRefreshInteraction();
        }

        internal void TeleportNearShop()
        {
            if (_game._shopStation == null) return;
            _game._player.Teleport(_game._shopStation.GlobalPosition + new Vector3(0f, 0.1f, 1.2f), 0f);
            _game._player.ForceRefreshInteraction();
        }

        internal void TeleportNearRepairCar()
        {
            if (_game._repairStation == null) return;
            _game._player.Teleport(_game._repairStation.GlobalPosition + new Vector3(0f, 0.1f, 1.2f), 0f);
            _game._player.ForceRefreshInteraction();
        }

        internal void TeleportNearFestivalTable(int index)
        {
            if (_game._world == null || index < 0 || index >= _game._world.FestivalTablePads.Count) return;
            _game._player.Teleport(_game._world.FestivalTablePads[index] + new Vector3(0f, 0.15f, 1.2f), 0f);
            _game._player.ForceRefreshInteraction();
        }

        internal void TeleportNearFestivalLantern(int index)
        {
            if (_game._world == null || index < 0 || index >= _game._world.FestivalLanternPads.Count) return;
            _game._player.Teleport(_game._world.FestivalLanternPads[index] + new Vector3(0f, 0.15f, 1.2f), 0f);
            _game._player.ForceRefreshInteraction();
        }

    }
}
