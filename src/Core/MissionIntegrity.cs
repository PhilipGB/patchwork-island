using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

/// <summary>
/// Release checks for the authored mission catalogue. A missing or malformed
/// mission must stop startup rather than silently substituting placeholder
/// content.
/// </summary>
public static class MissionIntegrity
{
    public static readonly string[] RequiredMissionIds =
    {
        "M01", "M02", "M03", "M04", "M05", "M06", "M07", "M08", "M09", "M10",
        "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20",
        "M21",
    };

    public static bool ValidateCatalog(MissionCatalog catalog, out string error)
    {
        error = "";
        if (catalog == null)
        {
            error = "No mission catalogue was loaded.";
            return false;
        }
        if (catalog.Missions.Count != RequiredMissionIds.Length)
        {
            var present = new HashSet<string>(catalog.Missions.Select(m => m?.Id ?? ""));
            string missing = string.Join(", ",
                RequiredMissionIds.Where(id => !present.Contains(id)));
            string extra = string.Join(", ",
                catalog.Missions.Where(m => m == null || !RequiredMissionIds.Contains(m.Id))
                    .Select(m => m?.Id ?? "<null>"));
            error = $"Expected {RequiredMissionIds.Length} missions, found {catalog.Missions.Count}." +
                    (missing.Length > 0 ? $" Missing: {missing}." : "") +
                    (extra.Length > 0 ? $" Unexpected: {extra}." : "");
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mission in catalog.Missions)
        {
            if (mission == null)
            {
                error = "The catalogue contains an empty mission.";
                return false;
            }
            if (!RequiredMissionIds.Contains(mission.Id))
            {
                error = $"Unknown mission id '{mission.Id}'.";
                return false;
            }
            if (!ids.Add(mission.Id))
            {
                error = $"Duplicate mission id '{mission.Id}'.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(mission.Title) || string.IsNullOrWhiteSpace(mission.Short))
            {
                error = $"Mission '{mission.Id}' has no title or instruction.";
                return false;
            }
            if (mission.RewardCoins <= 0 || mission.RewardCoins > 100)
            {
                error = $"Mission '{mission.Id}' has an invalid reward.";
                return false;
            }
            if (mission.Hints == null || mission.Hints.Count == 0)
            {
                error = $"Mission '{mission.Id}' has no hints.";
                return false;
            }
            if (mission.Prerequisites == null)
            {
                error = $"Mission '{mission.Id}' has no prerequisite list.";
                return false;
            }
            foreach (string prerequisite in mission.Prerequisites)
            {
                if (prerequisite == mission.Id)
                {
                    error = $"Mission '{mission.Id}' depends on itself.";
                    return false;
                }
                if (!catalog.Exists(prerequisite))
                {
                    error = $"Mission '{mission.Id}' needs unknown mission '{prerequisite}'.";
                    return false;
                }
            }
        }

        foreach (string required in RequiredMissionIds)
        {
            if (!ids.Contains(required))
            {
                error = $"Mission '{required}' is missing.";
                return false;
            }
        }

        // Depth-first cycle check; the mission graph is deliberately tiny.
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mission in catalog.Missions)
        {
            if (!Visit(mission.Id, catalog, visiting, visited, out error))
                return false;
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal) { "M01", "M02" };
        bool changed;
        do
        {
            changed = false;
            foreach (var mission in catalog.Missions)
            {
                if (reachable.Contains(mission.Id)) continue;
                if (mission.Prerequisites.All(reachable.Contains))
                {
                    reachable.Add(mission.Id);
                    changed = true;
                }
            }
        } while (changed);

        foreach (string id in RequiredMissionIds)
        {
            if (!reachable.Contains(id))
            {
                error = $"Mission '{id}' cannot be reached from the starting jobs.";
                return false;
            }
        }
        return true;
    }

    private static bool Visit(string id, MissionCatalog catalog, HashSet<string> visiting,
        HashSet<string> visited, out string error)
    {
        error = "";
        if (visited.Contains(id)) return true;
        if (!visiting.Add(id))
        {
            error = $"Mission prerequisites form a cycle at '{id}'.";
            return false;
        }
        foreach (string prerequisite in catalog.Get(id)?.Prerequisites ?? new List<string>())
        {
            if (!Visit(prerequisite, catalog, visiting, visited, out error))
                return false;
        }
        visiting.Remove(id);
        visited.Add(id);
        return true;
    }
}
