using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

public enum CityActorTier
{
    Mid,
    Near,
}

/// <summary>
/// Bounded visual pool around the player. It only owns actors for the nearest
/// logical agents, makes at most one node per logical id, and updates mid-tier
/// impostors at 4 Hz. Near actors opt into physics and animation.
/// </summary>
public partial class CityActorPool : Node3D
{
    public const int MaxNearVehicles = 14;
    public const int MaxNearPedestrians = 22;
    public const float ActiveRadius = 175f;
    public const float NearRadius = 78f;
    public const float MaxVisualLagMeters = 18f;
    private const float RetentionRadius = ActiveRadius + 22f;
    private const float MidStep = 0.25f;
    private readonly Dictionary<int, float> _visualLags = new();

    public int VisualVehicleCount => _vehicles.Count;
    public int VisualPedestrianCount => _pedestrians.Count;
    public int NearVehicleCount => _vehicles.Values.Count(v => v.Tier == CityActorTier.Near);
    public int NearPedestrianCount => _pedestrians.Values.Count(p => p.Tier == CityActorTier.Near);
    public int MidVehicleCount => _vehicles.Values.Count(v => v.Tier == CityActorTier.Mid);
    public int MidPedestrianCount => _pedestrians.Values.Count(p => p.Tier == CityActorTier.Mid);

    private readonly Dictionary<int, CityVehicleVisual> _vehicles = new();
    private readonly Dictionary<int, CityPedestrianVisual> _pedestrians = new();
    private CitySimulation _sim;
    private IReadOnlyList<CityRoutePoint[]> _vehicleRoutes = Array.Empty<CityRoutePoint[]>();
    private IReadOnlyList<CityRoutePoint[]> _pedestrianRoutes = Array.Empty<CityRoutePoint[]>();
    private Func<Vector3> _playerPosition;
    private Func<Vector3> _vanPosition;
    private Func<bool> _driving;
    private float _midTimer;
    private float _poolTimer;

    public void Initialize(CitySimulation simulation,
        IReadOnlyList<CityRoutePoint[]> vehicleRoutes,
        IReadOnlyList<CityRoutePoint[]> pedestrianRoutes,
        Func<Vector3> playerPosition, Func<Vector3> vanPosition, Func<bool> driving)
    {
        _sim = simulation;
        _vehicleRoutes = vehicleRoutes;
        _pedestrianRoutes = pedestrianRoutes;
        _playerPosition = playerPosition;
        _vanPosition = vanPosition;
        _driving = driving;
        Name = "CityActorPool";
        ProcessMode = ProcessModeEnum.Pausable;
    }

    public void UpdatePool(float delta, Vector3 focusWorld, float speed, bool allowVisuals,
        float cameraAltitude)
    {
        if (_sim == null) return;
        _midTimer -= delta;
        _poolTimer -= delta;
        Vector3 localFocus = focusWorld - new Vector3(CityLayout.OriginX, 0f, CityLayout.OriginZ);
        float nearRadius = NearRadius + Mathf.Clamp(speed * 2.6f, 0f, 22f);
        if (cameraAltitude > 18f) nearRadius *= 0.78f;
        float activeRadius = ActiveRadius + Mathf.Clamp(speed * 4f, 0f, 30f);

        if (!allowVisuals)
        {
            ClearAll();
            return;
        }

        if (_poolTimer <= 0f)
        {
            _poolTimer = 0.5f;
            RefreshSelection(localFocus, activeRadius, nearRadius);
        }

        ReconcileVehicles();
        if (_midTimer <= 0f)
        {
            _midTimer = MidStep;
            foreach (var ped in _pedestrians.Values)
                ped.StepMid(MidStep);
        }
    }

    private static float SignedDelta(float from, float to, float length)
    {
        if (length <= 0.0001f) return 0f;
        float delta = (to - from) % length;
        if (delta > length * 0.5f) delta -= length;
        else if (delta < -length * 0.5f) delta += length;
        return delta;
    }

    private void ReconcileVehicles()
    {
        if (_sim == null) return;
        foreach (var pair in _vehicles)
        {
            var vehicle = pair.Value;
            var agent = _sim.Agents.FirstOrDefault(a => a.Id == pair.Key);
            if (agent == null) continue;
            var pose = _sim.PoseAt(agent);
            if (vehicle.Tier == CityActorTier.Mid)
            {
                vehicle.SnapToProgress(pose.RouteProgress);
                vehicle.SetReconciliationLag(0f);
                _visualLags[pair.Key] = 0f;
                continue;
            }
            float lag = SignedDelta(vehicle.Progress, pose.RouteProgress, vehicle.RouteLength);
            // Keep the stored lag bounded, but never teleport a near actor
            // forward while it is yielding (that can push it through the
            // player). The physical car keeps its place and catches up when
            // the yield ends; streaming re-entry uses the bounded stored lag.
            lag = Mathf.Clamp(lag, -MaxVisualLagMeters, MaxVisualLagMeters);
            vehicle.SetReconciliationLag(lag);
            _visualLags[pair.Key] = lag;
        }
    }

    private void RefreshSelection(Vector3 focus, float activeRadius, float nearRadius)
    {
        var selectedVehicles = new List<(float Distance, CityAgentState Agent)>();
        var selectedPeds = new List<(float Distance, CityAgentState Agent)>();
        float keepRadius = Mathf.Max(activeRadius, RetentionRadius);

        foreach (var agent in _sim.Agents)
        {
            bool alreadyVisible = agent.IsVehicle
                ? _vehicles.ContainsKey(agent.Id)
                : _pedestrians.ContainsKey(agent.Id);
            if (!_sim.IsOnDuty(agent) && !alreadyVisible) continue;
            var pose = _sim.PoseAt(agent);
            float dx = pose.X - focus.X;
            float dz = pose.Z - focus.Z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            float radius = alreadyVisible ? keepRadius : activeRadius;
            if (distance > radius) continue;
            if (!alreadyVisible && distance < 6f) continue;
            if (agent.IsVehicle) selectedVehicles.Add((distance, agent));
            else selectedPeds.Add((distance, agent));
        }

        selectedVehicles.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        selectedPeds.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        var keepVehicles = new HashSet<int>(selectedVehicles
            .Take(MaxNearVehicles).Select(v => v.Agent.Id));
        var keepPeds = new HashSet<int>(selectedPeds
            .Take(MaxNearPedestrians).Select(v => v.Agent.Id));

        foreach (int id in _vehicles.Keys.Where(id => !keepVehicles.Contains(id)).ToList())
        {
            _vehicles[id].QueueFree();
            _vehicles.Remove(id);
        }
        foreach (int id in _pedestrians.Keys.Where(id => !keepPeds.Contains(id)).ToList())
        {
            _pedestrians[id].QueueFree();
            _pedestrians.Remove(id);
        }

        foreach (var entry in selectedVehicles.Take(MaxNearVehicles))
        {
            if (!_vehicles.TryGetValue(entry.Agent.Id, out var vehicle))
            {
                vehicle = new CityVehicleVisual();
                AddChild(vehicle);
                vehicle.Initialize(entry.Agent, _vehicleRoutes[entry.Agent.RouteIndex],
                    _playerPosition, _driving, _vanPosition);
                var pose = _sim.PoseAt(entry.Agent);
                float savedLag = _visualLags.TryGetValue(entry.Agent.Id, out float lag) ? lag : 0f;
                vehicle.SpawnAt(pose, TierFor(entry.Distance, nearRadius), savedLag);
                _vehicles[entry.Agent.Id] = vehicle;
            }
            else
            {
                vehicle.SetTier(TierFor(entry.Distance, nearRadius));
            }
        }
        foreach (var entry in selectedPeds.Take(MaxNearPedestrians))
        {
            if (!_pedestrians.TryGetValue(entry.Agent.Id, out var ped))
            {
                ped = new CityPedestrianVisual();
                AddChild(ped);
                ped.Initialize(entry.Agent, _pedestrianRoutes[entry.Agent.RouteIndex]);
                var pose = _sim.PoseAt(entry.Agent);
                ped.SpawnAt(pose, TierFor(entry.Distance, nearRadius));
                _pedestrians[entry.Agent.Id] = ped;
            }
            else
            {
                ped.SetTier(TierFor(entry.Distance, nearRadius));
            }
        }
    }

    private static CityActorTier TierFor(float distance, float nearRadius) =>
        distance <= nearRadius ? CityActorTier.Near : CityActorTier.Mid;

    private void ClearAll()
    {
        foreach (var vehicle in _vehicles.Values) vehicle.QueueFree();
        foreach (var ped in _pedestrians.Values) ped.QueueFree();
        _vehicles.Clear();
        _pedestrians.Clear();
    }

    public void ResetAll()
    {
        ClearAll();
        _visualLags.Clear();
        _poolTimer = 0f;
        _midTimer = 0f;
    }
}
