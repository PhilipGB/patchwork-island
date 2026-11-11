using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

/// <summary>
/// Pure rules for the final town-side roadworks job. This is deliberately
/// separate from the rest of the mission service so save validation,
/// interaction code and tests all agree on the exact quantities and order.
/// </summary>
public static class RoadworksRules
{
    public const string MissionId = "M21";
    public const int ConeCount = 3;
    public const int PanelCount = 2;
    public const int ToolboxCount = 1;
    public const int SupplyTotal = ConeCount + PanelCount + ToolboxCount;

    public static readonly string[] SupplyItems =
    {
        "safety_cone", "safety_cone", "safety_cone",
        "barrier_panel", "barrier_panel",
        "toolbox",
    };

    public static readonly string[] SupplyPickupIds =
    {
        "roadworks_cone_0", "roadworks_cone_1", "roadworks_cone_2",
        "roadworks_panel_0", "roadworks_panel_1",
        "roadworks_toolbox_0",
    };

    public const string SuppliesDeliveredKey = "supplies_delivered";
    public const string TaskDoneKey = "task_done";

    /// <summary>The authored loading order for the three supply kinds.</summary>
    public static readonly string[] CorrectOrder = { "cones", "panels", "toolbox" };

    public static bool HasAllSupplies(GameData data)
    {
        if (data == null) return false;
        return data.GetItem("safety_cone") >= ConeCount &&
               data.GetItem("barrier_panel") >= PanelCount &&
               data.GetItem("toolbox") >= ToolboxCount;
    }

    public static int SupplyProgress(GameData data)
    {
        if (data == null) return 0;
        return Math.Min(ConeCount, data.GetItem("safety_cone")) +
               Math.Min(PanelCount, data.GetItem("barrier_panel")) +
               Math.Min(ToolboxCount, data.GetItem("toolbox"));
    }

    public static bool SuppliesDelivered(GameData data) =>
        data?.GetMissionCounter(MissionId, SuppliesDeliveredKey) > 0;

    public static bool TaskDone(GameData data) =>
        data?.GetMissionCounter(MissionId, TaskDoneKey) > 0;

    public static bool IsComplete(GameData data) =>
        SuppliesDelivered(data) && TaskDone(data);

    public static bool OrderIsCorrect(IReadOnlyList<string> order)
    {
        if (order == null || order.Count != CorrectOrder.Length) return false;
        for (int i = 0; i < CorrectOrder.Length; i++)
            if (!string.Equals(order[i], CorrectOrder[i], StringComparison.Ordinal))
                return false;
        return true;
    }

    public static bool TotalIsCorrect(int total) => total == SupplyTotal;

    public static string OrderLabel(IReadOnlyList<string> order)
    {
        if (order == null || order.Count == 0) return "(not chosen)";
        return string.Join(" → ", order.Select(LabelFor));
    }

    public static string LabelFor(string kind) => kind switch
    {
        "cones" => "Cones",
        "panels" => "Barrier panels",
        "toolbox" => "Toolbox",
        _ => kind ?? "",
    };
}
