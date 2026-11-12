using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

public partial class Main
{
    private RuntimeProbe _probe;
    internal RuntimeProbe Probe => _probe ??= new RuntimeProbe(this);

    /// <summary>Internal adapter for integration checks. Production gameplay never calls it.</summary>
    internal sealed partial class RuntimeProbe
    {
        private readonly Main _game;
        internal RuntimeProbe(Main game) => _game = game;
        internal bool Started => _game._started;
        internal bool Paused => _game.GetTree().Paused;
        internal string Mode => _game._controlMode.ToString();
        internal bool ModalOpen => _game._hud.ModalOpen;
        internal string ModalTitle => _game._hud.ModalTitleText;
        internal string ModalStatus => _game._hud.ModalStatusText;
        internal bool ModalStatusVisible => _game._hud.ModalStatusVisible;
        internal string ToastText => _game._hud.ToastText;
        internal bool ToastVisible => _game._hud.ToastVisible;
        internal bool CanContinue => _game._session.CanContinue;
        internal bool HasSaveFile => _game._session.HasSaveFile;
        internal bool MemoryMatchesPersisted => _game._session.MemoryMatchesPersisted;
        internal bool ContinueReloadPending => Main._continueAfterReload;
        internal bool NewerSavePresent => _game._session.NewerSavePresent;
        internal bool NewerReplacementAuthorized => _game._session.NewerReplacementAuthorized;
        internal string TitleNotice => _game._session.TitleNotice;
        internal string SavePath => Main.SavePath;
        internal MissionCatalog Catalog => _game._catalog;
        internal bool PlayerControlled => _game._player.ControlEnabled;
        internal bool VanControlled => _game._van.InputEnabled;
        internal bool PlayerVisible => _game._player.Visible;
        internal Vector3 PlayerPosition => _game._player.GlobalPosition;
        internal Vector3 VanPosition => _game._van.GlobalPosition;
        internal ProcessModeEnum PlayerProcessMode => _game._player.ProcessMode;
        internal ProcessModeEnum VanProcessMode => _game._van.ProcessMode;
        internal ProcessModeEnum WorldProcessMode => _game._world.ProcessMode;
        internal CityWorldAdapter City => _game._city;
        internal TownCityRoadworks Roadworks => _game._roadworks;
        internal CityMissionService CityMissions => _game._cityMissions;
        internal GameData Data => _game._data;
        internal GameData CityMissionData => _game._cityMissions.Data;
        internal CityMissionStartResult StartCityMission(string id) => _game._cityMissions.StartMission(id);
        internal void AdoptLoadedDataForTest(GameData data) => _game.AdoptLoadedData(data);
        internal void LoadSaveForTest(string path) => _game.LoadSavedGameIfAvailable(path);
        internal MissionService Missions => _game._missions;
        internal Vector3 CityBoardPosition => _game._city.BoardInteraction;
        internal Vector3 CityDistrictCentre => _game._city.DistrictCentre;

        internal void SuppressSaves() => _game._suppressScenarioSaves = true;

        internal Vector3 Destination => _game._destination;

        internal string DestinationLabel => _game._destLabel;

        internal void InteractRoadworks() => _game.CityGameplay.InteractRoadworks();

        internal void SetRoadState(bool open)
        {
            if (open)
            {
                if (!_game._data.CompletedMissions.Contains(RoadworksRules.MissionId))
                    _game._data.CompletedMissions.Add(RoadworksRules.MissionId);
                _game._data.SetMissionCounter(RoadworksRules.MissionId,
                    RoadworksRules.SuppliesDeliveredKey, 1);
                _game._data.SetMissionCounter(RoadworksRules.MissionId,
                    RoadworksRules.TaskDoneKey, 1);
            }
            _game._data.CityRoadOpen = open;
            _game._data.CityRoadworksCompleted = open;
            _game._data.CityRoadworksRewardClaimed = open;
            _game._roadworks.SetRoadState(open, false);
            _game.RefreshMissionState();
        }

        internal void TeleportPlayer(Vector3 position, float yaw = 0f)
        {
            _game._driving = false;
            _game._van.SetDriven(false);
            _game._player.Visible = true;
            _game._player.Teleport(position, yaw);
            _game._rig.SetTarget(_game._player, false, 8.2f, 18f, 1.55f, true);
            _game._rig.SetView(Mathf.RadToDeg(yaw), 20f, 9f);
            _game.SyncControlMode();
        }

        internal void TeleportVan(Vector3 position, float yaw = 0f)
        {
            _game._van.GlobalPosition = position;
            _game._van.Rotation = new Vector3(0, yaw, 0);
            _game._van.SetDriven(true);
            _game._driving = true;
            _game._player.Visible = false;
            _game._rig.SetTarget(_game._van, true, 10.6f, 20f, 1.5f, true);
            _game.SyncControlMode();
        }

        internal void LaunchVan(float speed) => _game._van.TestLaunch(speed);
        internal float VanSpeed => _game._van.Speed;

        internal void StartVanRoute(IReadOnlyList<Vector3> points) => _game._van.StartRouteAutopilot(points);
        internal void StopVanRoute() => _game._van.StopAutopilot();

        internal void ProbeCityLod(Vector3 position, float speed)
        {
            _game._city.SetFocusActors(position, position, true);
            _game._city.UpdateCity(0.6, position, speed, 8f, true);
            _game._city.UpdateCity(0.6, position, speed, 8f, true);
        }

        internal void RefreshMissionState() => _game.RefreshMissionState();
        internal string CityStats => _game._city?.StatsLine() ?? "no city";
        internal int CityActiveCollisionBodies => _game._city?.ActiveDistrictCollisionBodies ?? 0;
        internal bool CityGroundCollisionActive => _game._city?.GroundCollisionActive == true;
        internal int CityActorNodes => _game._city?.ActiveVisualActors ?? 0;
        internal bool WorldIsCanonicalScene =>
            _game._world?.Town != null &&
            _game._world.Town.SceneFilePath == WorldSceneAdapter.TownScenePath &&
            _game._world.Town.Name == "Town";
        internal int WorldAnchorCount => _game._world?.AnchorCount ?? 0;
        internal int WorldTopLevelRootCount => _game._world?.Town?.GetChildCount() ?? 0;
        internal int BrokenSwingLabelCount => _game._world?.BrokenSwingLabels.Count ?? 0;
        internal Vector3 WorldAnchorPosition(string id) =>
            _game._world?.GetAnchorPosition(id) ?? new Vector3(9999, 9999, 9999);
        internal Vector3 WorldNodePosition(string path)
        {
            var node = _game._world?.Town?.GetNodeOrNull<Node3D>(path);
            return node?.Position ?? new Vector3(9999, 9999, 9999);
        }
        internal Vector3 WorldNodeGlobalPosition(string path)
        {
            var node = _game._world?.Town?.GetNodeOrNull<Node3D>(path);
            return node?.GlobalPosition ?? new Vector3(9999, 9999, 9999);
        }
        internal int NpcRoutePointCount(string id) =>
            _game._world?.GetNpcPatrol(id)?.Count ?? 0;
        internal int TrafficRoutePointCount(string id) =>
            _game._world?.GetTrafficPoints(id)?.Count ?? 0;
        internal int RaceGateCount(bool grand) =>
            grand ? _game._world?.M19RaceGates.Length ?? 0 : _game._world?.RaceGates.Length ?? 0;
        internal Vector3 RaceGatePosition(bool grand, int index)
        {
            var gates = grand ? _game._world?.M19RaceGates : _game._world?.RaceGates;
            return gates != null && index >= 0 && index < gates.Length
                ? gates[index].Position
                : new Vector3(9999, 9999, 9999);
        }
        internal float RaceGateYaw(bool grand, int index)
        {
            var gates = grand ? _game._world?.M19RaceGates : _game._world?.RaceGates;
            return gates != null && index >= 0 && index < gates.Length
                ? gates[index].Yaw
                : 9999f;
        }
        internal bool WorldNodeExists(string path) =>
            _game._world?.Town?.GetNodeOrNull<Node>(path) != null;
        internal bool WorldNodeVisible(string path) =>
            _game._world?.Town?.GetNodeOrNull<Node3D>(path)?.Visible == true;
        internal Vector3 WorldBoxMeshSize(string path)
        {
            var node = _game._world?.Town?.GetNodeOrNull<Node>(path);
            var mesh = node?.GetChildren().OfType<MeshInstance3D>()
                .Select(instance => instance.Mesh as BoxMesh)
                .FirstOrDefault(box => box != null);
            return mesh?.Size ?? Vector3.Zero;
        }
        internal string WorldRayHit(Vector3 from, Vector3 to, uint mask)
        {
            using var query = PhysicsRayQueryParameters3D.Create(from, to, mask);
            var hit = _game.GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count == 0) return "";
            return (hit["collider"].AsGodotObject() as Node)?.Name.ToString() ?? "unknown";
        }
        internal double RaceElapsed => _game._raceTimer.ElapsedSeconds;
        internal bool BridgeBuilt => _game._bridgeBuilder?.IsBuilt == true;
        internal int BridgePieceCount => _game._data.BridgePieces.Count;
        internal int BridgePlacedSocketCount => _game._bridgeBuilder?.PlacedSocketCount ?? 0;
        internal bool SwingBuilt => _game._swingBuilder?.IsBuilt == true;
        internal int SwingPlacedSocketCount => _game._swingBuilder?.PlacedSocketCount ?? 0;
        internal int BrokenSwingLabelsVisible =>
            _game._world?.BrokenSwingLabels.Count(label => label.Visible) ?? 0;
        internal bool BridgeBarrierVisible => _game._world?.BridgeBarrier?.Visible == true;
        internal bool BuildGhostLabelsHidden =>
            !HasVisibleBuildGhostLabel(_game._bridgeBuilder) && !HasVisibleBuildGhostLabel(_game._swingBuilder);

        private static bool HasVisibleBuildGhostLabel(Node root)
        {
            if (root == null || !GodotObject.IsInstanceValid(root)) return false;
            foreach (Node node in root.FindChildren("*", "Label3D", true, false))
            {
                if (node is not Label3D label || !label.IsVisibleInTree()) continue;
                if (label.Text is "SUPPORT" or "PLANK" or "ROPE" or "SEAT") return true;
            }
            return false;
        }
        internal bool PlaygroundFixHidden => _game._world?.PlaygroundFixSign?.Visible == false;
        internal Vector3 PlaygroundStationPosition =>
            _game._playgroundStation?.GlobalPosition ?? new Vector3(9999, 9999, 9999);
        internal int ParkPlacementCount => _game._data.ParkPlacements.Count;
        internal int ParkVisualCount => _game._parkBuilder?.PlacedVisualCount ?? 0;
        internal int FurniturePlacementCount => _game._data.FurniturePlacements.Count;
        internal int FurnitureVisualCount => _game._furnitureBuilder?.PlacedVisualCount ?? 0;
        internal int SwingPieceCount => _game._data.SwingPieces.Count;
        internal string CharacterPreset => _game._data.CharacterPreset;
        internal string EquippedOutfit => _game._data.EquippedOutfit;
        internal int OwnedOutfitCount => _game._data.OwnedOutfits.Count;
        internal bool OwnsOutfit(string outfitId) => CharacterCustomisation.Owns(_game._data, outfitId);
        internal string PlayerPreset => _game._player.AppearancePreset;
        internal string PlayerOutfit => _game._player.AppearanceOutfit;
        internal bool PlayerHasLongHair => _game._player.HasLongHair;
        internal bool PlayerWearsDress => _game._player.WearsDress;
        internal int PlayerAppearanceRevision => _game._player.AppearanceRevision;

    }
}
