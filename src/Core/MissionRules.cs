using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

public static class MissionRules
{
    public static bool PrerequisitesMet(GameData data, MissionDefinition mission)
    {
        if (mission?.Prerequisites == null) return true;
        foreach (var prerequisite in mission.Prerequisites)
            if (!data.IsCompleted(prerequisite))
                return false;
        return true;
    }

    public static bool IsObjectiveComplete(GameData data, string missionId)
    {
        return missionId switch
        {
            "M01" => data.GetMissionCounter("M01", "delivered") >= 1,
            "M02" => data.GetMissionCounter("M02", "delivered") >= 1,
            "M03" => data.BridgePieces.Distinct().Count() >= 7,
            "M04" => data.GetMissionCounter("M04", "delivered") >= 1,
            "M05" => data.GetMissionCounter("M05", "checkpoint") >= 4,
            "M06" => data.GetMissionCounter("M06", "blueDelivered") >= 2 &&
                     data.GetMissionCounter("M06", "orangeDelivered") >= 1,
            "M07" => data.SwingPieces.Distinct().Count() >= 6,
            "M08" => HasParkPieces(data),
            "M09" => data.GetMissionCounter("M09", "checkpoint") >= 4,
            "M10" => HasHouseFurniture(data),
            "M11" => ChapterTwoRules.M11Complete(data),
            "M12" => ChapterTwoRules.M12Complete(data),
            "M13" => ChapterTwoRules.M13Complete(data),
            "M14" => ChapterTwoRules.M14Complete(data),
            "M15" => ChapterTwoRules.M15Complete(data),
            "M16" => FinaleRules.M16Complete(data),
            "M17" => FinaleRules.M17Complete(data),
            "M18" => FinaleRules.M18Complete(data),
            "M19" => FinaleRules.M19Complete(data),
            "M20" => FinaleRules.M20Complete(data),
            "M21" => RoadworksRules.IsComplete(data),
            _ => false,
        };
    }

    public static bool HasParkPieces(GameData data)
    {
        var types = new HashSet<string>(data.ParkPlacements.Select(p => p.Type));
        return types.Contains("bench") && types.Contains("flowerbed") && types.Contains("lamp");
    }

    public static bool HasHouseFurniture(GameData data)
    {
        int tables = data.FurniturePlacements.Count(p => p.Type == "table");
        int chairs = data.FurniturePlacements.Count(p => p.Type == "chair");
        int plants = data.FurniturePlacements.Count(p => p.Type == "plant");
        return tables >= 1 && chairs >= 2 && plants >= 1;
    }

    public static bool CanDeliverApples(GameData data) => data.GetItem("apple") >= 3;

    public static bool CanSubmitSupplies(GameData data) =>
        data.GetItem("wood") >= 5 && data.GetItem("stone") >= 3;

    /// <summary>Optional race goal; never required for mission completion.</summary>
    public const int MaxOptionalRaceBonus = 5;

    public static readonly string[] OriginalMissionIds =
    {
        "M01", "M02", "M03", "M04", "M05", "M06", "M07", "M08", "M09", "M10",
    };

    /// <summary>
    /// The original ten-job chapter is a separate milestone from the v1.2
    /// continuation. Persisted CelebrationSeen belongs to this milestone only.
    /// </summary>
    public static bool OriginalChapterComplete(GameData data) =>
        OriginalMissionIds.All(id => data.IsCompleted(id));

    public static int OptionalRaceBonus(string missionId, double elapsedSeconds)
    {
        return missionId switch
        {
            "M05" when elapsedSeconds <= 90.0 => MaxOptionalRaceBonus,
            "M09" when elapsedSeconds <= 75.0 => MaxOptionalRaceBonus,
            "M19" when elapsedSeconds <= FinaleRules.RallyBonusSeconds => MaxOptionalRaceBonus,
            _ => 0,
        };
    }
}
