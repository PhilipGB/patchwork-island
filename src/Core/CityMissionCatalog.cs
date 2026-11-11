using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PatchworkIsland;

/// <summary>A playable city job, loaded from the authored city catalogue.</summary>
public sealed class CityMissionDefinition
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Short { get; set; } = "";
    public string Category { get; set; } = "";
    public string Site { get; set; } = "";
    public string Goal { get; set; } = "";
    public int RewardCoins { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public List<string> Hints { get; set; } = new();
    public List<string> Steps { get; set; } = new();
    public List<string> Learning { get; set; } = new();
}

public sealed class CityMissionContent
{
    public int SchemaVersion { get; set; }
    public string Status { get; set; } = "";
    public List<CityMissionDefinition> Missions { get; set; } = new();
}

/// <summary>
/// City jobs are intentionally a separate catalogue from the town board. Their
/// ids start with C, their progress lives in separate save fields, and their
/// board never appears in the town's M01–M21 UI.
/// </summary>
public sealed class CityMissionCatalog
{
    public static readonly string[] RequiredIds = { "C01", "C02", "C03" };

    private readonly Dictionary<string, CityMissionDefinition> _byId =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<CityMissionDefinition> Missions { get; }

    public CityMissionCatalog(IEnumerable<CityMissionDefinition> missions)
    {
        var list = missions?.ToList() ?? throw new ArgumentNullException(nameof(missions));
        foreach (var mission in list)
        {
            if (string.IsNullOrWhiteSpace(mission.Id))
                throw new InvalidDataException("A city job is missing its id.");
            if (_byId.ContainsKey(mission.Id))
                throw new InvalidDataException($"Duplicate city job id: {mission.Id}");
            mission.Prerequisites ??= new List<string>();
            mission.Hints ??= new List<string>();
            mission.Steps ??= new List<string>();
            mission.Learning ??= new List<string>();
            _byId[mission.Id] = mission;
        }
        Missions = list;
    }

    public static CityMissionCatalog FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        var content = JsonSerializer.Deserialize<CityMissionContent>(json, options)
                      ?? throw new InvalidDataException("City mission content is empty.");
        if (content.Missions == null || content.Missions.Count == 0)
            throw new InvalidDataException("City mission content has no jobs.");
        return new CityMissionCatalog(content.Missions);
    }

    public CityMissionDefinition Get(string id) =>
        !string.IsNullOrWhiteSpace(id) && _byId.TryGetValue(id, out var mission) ? mission : null;

    public bool Exists(string id) => Get(id) != null;

    public static bool Validate(CityMissionCatalog catalog, out string error)
    {
        error = "";
        if (catalog == null)
        {
            error = "No city job catalogue was loaded.";
            return false;
        }
        if (catalog.Missions.Count != RequiredIds.Length)
        {
            var ids = new HashSet<string>(catalog.Missions.Select(m => m.Id), StringComparer.Ordinal);
            string missing = string.Join(", ", RequiredIds.Where(id => !ids.Contains(id)));
            error = $"Expected {RequiredIds.Length} city jobs, found {catalog.Missions.Count}." +
                    (missing.Length > 0 ? $" Missing: {missing}." : "");
            return false;
        }
        foreach (string id in RequiredIds)
        {
            var mission = catalog.Get(id);
            if (mission == null)
            {
                error = $"City job '{id}' is missing.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(mission.Title) || string.IsNullOrWhiteSpace(mission.Short))
            {
                error = $"City job '{id}' has no title or instruction.";
                return false;
            }
            if (mission.RewardCoins <= 0 || mission.RewardCoins > 100)
            {
                error = $"City job '{id}' has an invalid reward.";
                return false;
            }
            if (mission.Hints.Count == 0)
            {
                error = $"City job '{id}' has no hints.";
                return false;
            }
            if (mission.Prerequisites.Any(p => !catalog.Exists(p)))
            {
                error = $"City job '{id}' has an unknown prerequisite.";
                return false;
            }
        }
        return true;
    }
}
