using System;
using System.Collections.Generic;

namespace PatchworkIsland;

public readonly struct CityRoutePoint
{
    public readonly float X;
    public readonly float Z;

    public CityRoutePoint(float x, float z)
    {
        X = x;
        Z = z;
    }
}

public sealed class CityAgentState
{
    public int Id { get; init; }
    public bool IsVehicle { get; init; }
    public int RouteIndex { get; init; }
    public float Phase { get; init; }
    public float Speed { get; init; }
    public float ShiftOffset { get; init; }
    public int Variant { get; init; }
    public int Colour { get; init; }
}

public readonly struct CityAgentPose
{
    public readonly float X;
    public readonly float Z;
    public readonly float Yaw;
    public readonly bool OnDuty;
    public readonly float RouteProgress;

    public CityAgentPose(float x, float z, float yaw, bool onDuty, float progress)
    {
        X = x;
        Z = z;
        Yaw = yaw;
        OnDuty = onDuty;
        RouteProgress = progress;
    }
}

/// <summary>
/// Compact, deterministic city population. No Godot nodes live here. The city
/// clock is the only state that must advance while the city is unloaded;
/// positions are a pure function of (seed, logical time, authored route).
/// This makes a 30-minute offscreen advance O(1) and keeps near/mid/far visual
/// activation consistent with the same schedule.
/// </summary>
public sealed class CitySimulation
{
    public const int VehicleCount = 24;
    public const int PedestrianCount = 42;
    public const double MaxCatchUpSeconds = 30.0 * 60.0;
    public const double DutyCycleSeconds = 20.0 * 60.0;

    private readonly List<CityRoutePoint[]> _vehicleRoutes;
    private readonly List<CityRoutePoint[]> _pedestrianRoutes;
    private readonly List<float[]> _vehicleCumulative = new();
    private readonly List<float[]> _pedestrianCumulative = new();
    private readonly List<CityAgentState> _agents = new();

    public IReadOnlyList<CityAgentState> Agents => _agents;
    public IReadOnlyList<CityRoutePoint[]> VehicleRoutes => _vehicleRoutes;
    public IReadOnlyList<CityRoutePoint[]> PedestrianRoutes => _pedestrianRoutes;
    public int Seed { get; }
    public double LogicalSeconds { get; private set; }
    public int LastAdvanceClampedTicks { get; private set; }

    public CitySimulation(IReadOnlyList<CityRoutePoint[]> vehicleRoutes,
        IReadOnlyList<CityRoutePoint[]> pedestrianRoutes, int seed,
        int vehicleCount = VehicleCount, int pedestrianCount = PedestrianCount)
    {
        if (vehicleRoutes == null || vehicleRoutes.Count == 0)
            throw new ArgumentException("The city needs at least one vehicle route.");
        if (pedestrianRoutes == null || pedestrianRoutes.Count == 0)
            throw new ArgumentException("The city needs at least one pedestrian route.");
        _vehicleRoutes = NormaliseRoutes(vehicleRoutes, "vehicle");
        _pedestrianRoutes = NormaliseRoutes(pedestrianRoutes, "pedestrian");
        Seed = seed == 0 ? 20261111 : seed;

        for (int i = 0; i < _vehicleRoutes.Count; i++)
            _vehicleCumulative.Add(Cumulative(_vehicleRoutes[i]));
        for (int i = 0; i < _pedestrianRoutes.Count; i++)
            _pedestrianCumulative.Add(Cumulative(_pedestrianRoutes[i]));

        BuildAgents(vehicleCount, pedestrianCount);
    }

    public void Restore(double logicalSeconds)
    {
        if (double.IsNaN(logicalSeconds) || double.IsInfinity(logicalSeconds) || logicalSeconds < 0)
            logicalSeconds = 0;
        LogicalSeconds = logicalSeconds;
    }

    /// <summary>
    /// Advances the logical clock only. Positions are derived lazily, so there
    /// is no per-agent catch-up loop and no multi-second stall after an absence.
    /// A single call is clamped to 30 minutes to bound deterministic jumps.
    /// </summary>
    public void Advance(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) return;
        LastAdvanceClampedTicks = 0;
        if (seconds > MaxCatchUpSeconds)
        {
            LastAdvanceClampedTicks = 1;
            seconds = MaxCatchUpSeconds;
        }
        LogicalSeconds += seconds;
    }

    /// <summary>Test seam for a deterministic 30-minute offscreen advance.</summary>
    public void AdvanceBounded(double seconds) => Advance(seconds);

    public CityAgentPose PoseAt(CityAgentState agent, double? atSeconds = null)
    {
        if (agent == null) return default;
        double time = atSeconds ?? LogicalSeconds;
        if (agent.IsVehicle)
        {
            var route = _vehicleRoutes[agent.RouteIndex];
            var cumulative = _vehicleCumulative[agent.RouteIndex];
            float t = Wrap(agent.Phase + (float)(time * agent.Speed), cumulative[^1]);
            bool duty = DutyAt(time, agent.ShiftOffset);
            return PoseOnRoute(route, cumulative, t, duty);
        }
        else
        {
            var route = _pedestrianRoutes[agent.RouteIndex];
            var cumulative = _pedestrianCumulative[agent.RouteIndex];
            float t = Wrap(agent.Phase + (float)(time * agent.Speed), cumulative[^1]);
            bool duty = DutyAt(time, agent.ShiftOffset);
            return PoseOnRoute(route, cumulative, t, duty);
        }
    }

    public CityAgentPose Pose(int agentId, double? atSeconds = null)
    {
        foreach (var agent in _agents)
            if (agent.Id == agentId)
                return PoseAt(agent, atSeconds);
        return default;
    }

    /// <summary>
    /// Deterministic duty windows keep a visible population varied while still
    /// allowing a scene to keep an already-visible actor until it leaves range.
    /// </summary>
    public bool IsOnDuty(CityAgentState agent) => agent != null && DutyAt(LogicalSeconds, agent.ShiftOffset);

    /// <summary>Nearest on-duty logical agents for the visual pool. Pure and testable.</summary>
    public List<CityAgentState> AgentsNear(float x, float z, float radius, int maxVehicles, int maxPedestrians)
    {
        var vehicles = new List<(float Distance, CityAgentState Agent)>();
        var pedestrians = new List<(float Distance, CityAgentState Agent)>();
        foreach (var agent in _agents)
        {
            if (!IsOnDuty(agent)) continue;
            var pose = PoseAt(agent);
            float dx = pose.X - x;
            float dz = pose.Z - z;
            float distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance > radius) continue;
            (agent.IsVehicle ? vehicles : pedestrians).Add((distance, agent));
        }
        vehicles.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        pedestrians.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        var result = new List<CityAgentState>(maxVehicles + maxPedestrians);
        for (int i = 0; i < Math.Min(maxVehicles, vehicles.Count); i++)
            result.Add(vehicles[i].Agent);
        for (int i = 0; i < Math.Min(maxPedestrians, pedestrians.Count); i++)
            result.Add(pedestrians[i].Agent);
        return result;
    }

    public static bool DutyAt(double logicalSeconds, float offset)
    {
        double phase = (logicalSeconds + offset) % DutyCycleSeconds;
        return phase < DutyCycleSeconds * 0.82;
    }

    private void BuildAgents(int vehicleCount, int pedestrianCount)
    {
        for (int i = 0; i < vehicleCount; i++)
        {
            int routeIndex = i % _vehicleRoutes.Count;
            var route = _vehicleRoutes[routeIndex];
            float length = _vehicleCumulative[routeIndex][^1];
            _agents.Add(new CityAgentState
            {
                Id = 10_000 + i,
                IsVehicle = true,
                RouteIndex = routeIndex,
                Phase = Unit(i, 0) * length,
                Speed = 5.2f + Unit(i, 1) * 3.4f,
                ShiftOffset = Unit(i, 2) * (float)DutyCycleSeconds,
                Variant = i % 3,
                Colour = i % 4,
            });
        }
        for (int i = 0; i < pedestrianCount; i++)
        {
            int routeIndex = i % _pedestrianRoutes.Count;
            float length = _pedestrianCumulative[routeIndex][^1];
            _agents.Add(new CityAgentState
            {
                Id = 20_000 + i,
                IsVehicle = false,
                RouteIndex = routeIndex,
                Phase = Unit(i, 7) * length,
                Speed = 0.85f + Unit(i, 8) * 0.7f,
                ShiftOffset = Unit(i, 9) * (float)DutyCycleSeconds,
                Variant = i % 2,
                Colour = i % 6,
            });
        }
    }

    private static List<CityRoutePoint[]> NormaliseRoutes(
        IReadOnlyList<CityRoutePoint[]> routes, string label)
    {
        var result = new List<CityRoutePoint[]>();
        for (int r = 0; r < routes.Count; r++)
        {
            var route = routes[r];
            if (route == null || route.Length < 4)
                throw new ArgumentException($"The {label} route {r} needs four points.");
            var points = new CityRoutePoint[route.Length];
            for (int i = 0; i < route.Length; i++)
            {
                points[i] = route[i];
                if (!float.IsFinite(points[i].X) || !float.IsFinite(points[i].Z))
                    throw new ArgumentException($"The {label} route {r} has a non-finite point.");
            }
            result.Add(points);
        }
        return result;
    }

    private static float[] Cumulative(CityRoutePoint[] route)
    {
        var cumulative = new float[route.Length + 1];
        for (int i = 1; i <= route.Length; i++)
        {
            CityRoutePoint a = route[i - 1];
            CityRoutePoint b = route[i % route.Length];
            float dx = b.X - a.X;
            float dz = b.Z - a.Z;
            cumulative[i] = cumulative[i - 1] + MathF.Sqrt(dx * dx + dz * dz);
        }
        return cumulative;
    }

    private static CityAgentPose PoseOnRoute(CityRoutePoint[] route, float[] cumulative,
        float progress, bool duty)
    {
        float length = cumulative[^1];
        if (length <= 0.001f)
            return new CityAgentPose(route[0].X, route[0].Z, 0, duty, 0);
        progress = Wrap(progress, length);
        int segment = 0;
        while (segment < route.Length - 1 && cumulative[segment + 1] < progress)
            segment++;
        CityRoutePoint a = route[segment];
        CityRoutePoint b = route[(segment + 1) % route.Length];
        float segmentLength = cumulative[segment + 1] - cumulative[segment];
        float t = segmentLength > 0.0001f
            ? (progress - cumulative[segment]) / segmentLength
            : 0f;
        float x = a.X + (b.X - a.X) * t;
        float z = a.Z + (b.Z - a.Z) * t;
        float dx = b.X - a.X;
        float dz = b.Z - a.Z;
        float yaw = MathF.Abs(dx) + MathF.Abs(dz) > 0.0001f
            ? MathF.Atan2(-dx, -dz)
            : 0f;
        return new CityAgentPose(x, z, yaw, duty, progress);
    }

    private static float Wrap(float value, float length)
    {
        if (length <= 0.0001f) return 0;
        float wrapped = value % length;
        return wrapped < 0 ? wrapped + length : wrapped;
    }

    /// <summary>Stable FNV-style unit hash; never uses System.Random.</summary>
    private float Unit(int index, int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)Seed) * 16777619u;
            hash = (hash ^ (uint)index) * 16777619u;
            hash = (hash ^ (uint)salt) * 16777619u;
            hash ^= hash >> 13;
            hash *= 1274126177u;
            hash ^= hash >> 16;
            return (hash & 0xFFFFFF) / 16777216f;
        }
    }
}
