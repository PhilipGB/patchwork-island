using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PatchworkIsland;

/// <summary>Mission metadata loaded from the provided MISSIONS.json content.</summary>
public sealed class MissionDefinition
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Short { get; set; } = "";
    public List<string> Prerequisites { get; set; } = new();
    public int RewardCoins { get; set; }

    // The fields below are authored design documentation. Gameplay rules live
    // in MissionRules/MissionService; keep them out of runtime balance logic.
    public string Category { get; set; } = "";
    public string Site { get; set; } = "";
    public string Goal { get; set; } = "";
    public List<string> Hints { get; set; } = new();
    public List<string> Learning { get; set; } = new();
    public string WorldEffect { get; set; } = "";
    /// <summary>Authored mechanic description, retained so full-file comparisons see it.</summary>
    public JsonElement Mechanics { get; set; }
}

public sealed class MissionContent
{
    public int SchemaVersion { get; set; }
    public List<string> DefaultActiveChoices { get; set; } = new();
    public List<MissionDefinition> Missions { get; set; } = new();
}

public sealed class MissionCatalog
{
    private readonly Dictionary<string, MissionDefinition> _byId = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<MissionDefinition> Missions { get; }

    public MissionCatalog(IEnumerable<MissionDefinition> missions)
    {
        var list = missions.ToList();
        foreach (var mission in list)
        {
            if (string.IsNullOrWhiteSpace(mission.Id))
                throw new InvalidDataException("A mission is missing its id.");
            if (_byId.ContainsKey(mission.Id))
                throw new InvalidDataException($"Duplicate mission id: {mission.Id}");
            mission.Prerequisites ??= new List<string>();
            mission.Hints ??= new List<string>();
            mission.Learning ??= new List<string>();
            _byId[mission.Id] = mission;
        }
        Missions = list;
    }

    public static MissionCatalog FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        var content = JsonSerializer.Deserialize<MissionContent>(json, options)
                      ?? throw new InvalidDataException("Mission content is empty.");
        if (content.Missions == null || content.Missions.Count == 0)
            throw new InvalidDataException("Mission content has no missions.");
        return new MissionCatalog(content.Missions);
    }

    public MissionDefinition Get(string id)
    {
        return id != null && _byId.TryGetValue(id, out var mission) ? mission : null;
    }

    public bool Exists(string id) => id != null && _byId.ContainsKey(id);
}
