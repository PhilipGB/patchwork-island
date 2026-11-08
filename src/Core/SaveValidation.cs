using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

public sealed class SaveValidationResult
{
    public bool IsValid { get; init; }
    public string Message { get; init; } = "";

    public static SaveValidationResult Ok() => new() { IsValid = true };

    public static SaveValidationResult Fail(string message) =>
        new() { IsValid = false, Message = message };
}

/// <summary>
/// Structural and progression validation for loaded saves. It deliberately
/// rejects broken nested records instead of filtering them out: a builder or UI
/// must never be handed a null placement or an impossible counter.
/// </summary>
public static class SaveValidation
{
    private static readonly HashSet<string> InventoryItems = new(StringComparer.Ordinal)
    {
        "apple", "wood", "stone", "blue_parcel", "orange_parcel",
    };

    private static readonly HashSet<string> PickupIds = new(StringComparer.Ordinal)
    {
        "apple_0", "apple_1", "apple_2",
        "wood_0", "wood_1", "wood_2", "wood_3", "wood_4",
        "stone_0", "stone_1", "stone_2",
        "blue_parcel_0", "blue_parcel_1", "orange_parcel_0",
    };

    private static readonly Dictionary<string, int> InventoryMaximums = new(StringComparer.Ordinal)
    {
        ["apple"] = 3,
        ["wood"] = 5,
        ["stone"] = 3,
        ["blue_parcel"] = 2,
        ["orange_parcel"] = 1,
    };

    private static readonly Dictionary<string, Dictionary<string, int>> CounterMaximums = new(StringComparer.Ordinal)
    {
        ["M01"] = new() { ["delivered"] = 1 },
        ["M02"] = new() { ["delivered"] = 1 },
        ["M04"] = new() { ["bread"] = 4, ["milk"] = 3, ["delivered"] = 1 },
        ["M05"] = new()
        {
            ["checkpoint"] = 4, ["started"] = 1,
            ["elapsedMs"] = RaceTimer.MaxStoredMilliseconds,
        },
        ["M06"] = new() { ["blueDelivered"] = 2, ["orangeDelivered"] = 1 },
        ["M09"] = new()
        {
            ["checkpoint"] = 4, ["started"] = 1,
            ["elapsedMs"] = RaceTimer.MaxStoredMilliseconds,
        },
    };

    public static SaveValidationResult Validate(GameData data, MissionCatalog catalog = null)
    {
        if (data == null) return SaveValidationResult.Fail("The save has no game data.");
        data.Normalise();

        if (data.SchemaVersion < 1 || data.SchemaVersion > SaveService.CurrentVersion)
            return SaveValidationResult.Fail($"Unsupported save schema version {data.SchemaVersion}.");
        if (!Finite(data.Settings.TextScale) || data.Settings.TextScale < 0.5f || data.Settings.TextScale > 3f)
            return SaveValidationResult.Fail("The saved text scale is invalid.");
        if (data.Coins < 0 || data.Coins > 1_000_000)
            return SaveValidationResult.Fail($"The saved coin balance is invalid ({data.Coins}).");

        if (!ValidateStrings(data.CompletedMissions, "completed mission", out string error))
            return SaveValidationResult.Fail(error);
        if (!ValidateStrings(data.TutorialsSeen, "tutorial", out error))
            return SaveValidationResult.Fail(error);
        if (!ValidateStrings(data.UnlockedCosmetics, "cosmetic", out error))
            return SaveValidationResult.Fail(error);
        if (!ValidateStrings(data.CollectedPickups, "collected pickup", out error))
            return SaveValidationResult.Fail(error);

        foreach (string pickup in data.CollectedPickups)
            if (!PickupIds.Contains(pickup))
                return SaveValidationResult.Fail($"Unknown collected pickup '{pickup}'.");

        foreach (var pair in data.Inventory)
        {
            if (!InventoryItems.Contains(pair.Key))
                return SaveValidationResult.Fail($"Unknown inventory item '{pair.Key}'.");
            if (pair.Value < 0 || pair.Value > InventoryMaximums[pair.Key])
                return SaveValidationResult.Fail($"Impossible amount of '{pair.Key}' in the inventory.");
        }

        foreach (var pair in data.MissionProgress)
        {
            if (pair.Value == null)
                return SaveValidationResult.Fail($"Mission progress '{pair.Key}' is empty.");
            foreach (var counter in pair.Value)
            {
                if (counter.Value < 0)
                    return SaveValidationResult.Fail($"Negative progress counter '{pair.Key}.{counter.Key}'.");
            }
        }

        if (!ValidatePlacements(data.ParkPlacements, ParkRules.Slots, ParkRules.Items, "park", true, out error))
            return SaveValidationResult.Fail(error);
        if (!ValidatePlacements(data.FurniturePlacements, FurnitureRules.Slots, FurnitureRules.Items, "furniture", false, out error))
            return SaveValidationResult.Fail(error);
        if (!ValidateFurnitureOwnership(data, out error))
            return SaveValidationResult.Fail(error);
        if (!ValidateBridgePieces(data.BridgePieces, out error))
            return SaveValidationResult.Fail(error);
        if (!ValidateSwingPieces(data.SwingPieces, out error))
            return SaveValidationResult.Fail(error);
        if (!ValidateSnapshots(data, out error))
            return SaveValidationResult.Fail(error);

        if (ParkRules.Spent(data) > BudgetRules.ParkBudget)
            return SaveValidationResult.Fail("The park placements exceed the 20-coin job budget.");

        if (catalog != null)
        {
            if (!MissionIntegrity.ValidateCatalog(catalog, out error))
                return SaveValidationResult.Fail("Mission catalogue is invalid: " + error);
            if (!ValidateMissions(data, catalog, out error))
                return SaveValidationResult.Fail(error);
        }

        return SaveValidationResult.Ok();
    }

    private static bool ValidateMissions(GameData data, MissionCatalog catalog, out string error)
    {
        error = "";
        var completed = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in data.CompletedMissions)
        {
            if (!catalog.Exists(id))
            {
                error = $"Unknown completed mission '{id}'.";
                return false;
            }
            if (!completed.Add(id))
            {
                error = $"Mission '{id}' is completed more than once.";
                return false;
            }
        }

        if (!string.IsNullOrEmpty(data.ActiveMission))
        {
            if (!catalog.Exists(data.ActiveMission))
            {
                error = $"Unknown active mission '{data.ActiveMission}'.";
                return false;
            }
            if (completed.Contains(data.ActiveMission))
            {
                error = $"Mission '{data.ActiveMission}' is both active and completed.";
                return false;
            }
            var active = catalog.Get(data.ActiveMission);
            foreach (string prerequisite in active.Prerequisites ?? new List<string>())
            {
                if (!completed.Contains(prerequisite))
                {
                    error = $"Active mission '{data.ActiveMission}' is missing prerequisite '{prerequisite}'.";
                    return false;
                }
            }
        }

        foreach (string id in data.CompletedMissions)
        {
            var mission = catalog.Get(id);
            foreach (string prerequisite in mission.Prerequisites ?? new List<string>())
            {
                if (!completed.Contains(prerequisite))
                {
                    error = $"Mission '{id}' is completed before its prerequisite '{prerequisite}'.";
                    return false;
                }
            }
        }

        foreach (var pair in data.MissionProgress)
        {
            if (!catalog.Exists(pair.Key))
            {
                error = $"Progress refers to unknown mission '{pair.Key}'.";
                return false;
            }
            if (CounterMaximums.TryGetValue(pair.Key, out var allowed))
            {
                foreach (var counter in pair.Value)
                {
                    if (!allowed.TryGetValue(counter.Key, out int max))
                    {
                        error = $"Unknown progress counter '{pair.Key}.{counter.Key}'.";
                        return false;
                    }
                    if (counter.Value > max)
                    {
                        error = $"Impossible progress counter '{pair.Key}.{counter.Key}' = {counter.Value}.";
                        return false;
                    }
                }
            }
            else if (pair.Value.Count > 0)
            {
                error = $"Mission '{pair.Key}' cannot have progress counters.";
                return false;
            }
        }

        foreach (var pair in data.HintLevels)
        {
            var mission = catalog.Get(pair.Key);
            if (mission == null)
            {
                error = $"Hint progress refers to unknown mission '{pair.Key}'.";
                return false;
            }
            int hintCount = mission.Hints?.Count ?? 0;
            if (pair.Value < 0 || pair.Value > hintCount)
            {
                error = $"Impossible hint level {pair.Value} for '{pair.Key}'.";
                return false;
            }
        }

        foreach (string id in completed)
        {
            if (!MissionObjectiveMatches(data, id))
            {
                error = $"Mission '{id}' is marked complete but its world state is missing.";
                return false;
            }
        }

        if (data.CelebrationSeen && !catalog.Missions.All(m => completed.Contains(m.Id)))
        {
            error = "The celebration is marked seen before all ten jobs are complete.";
            return false;
        }

        return true;
    }

    private static bool MissionObjectiveMatches(GameData data, string missionId)
    {
        // Only physical world state is required to match a completed mission.
        // Counters for deliveries/races are informational; the world-changing
        // jobs must never be marked complete without their bridge/swing/park/
        // furniture state, because that would make the island unreachable.
        return missionId switch
        {
            "M03" => BuildRules.BridgeComplete(data.BridgePieces),
            "M07" => BuildRules.SwingComplete(data.SwingPieces),
            "M08" => MissionRules.HasParkPieces(data) &&
                     ParkRules.Count(data, "bench") == 1 &&
                     ParkRules.Count(data, "flowerbed") == 1 &&
                     ParkRules.Count(data, "lamp") == 1,
            // M10 is intentionally different: after completion the player may
            // pick up and rearrange furniture. Ownership proves the set was
            // acquired, while the placement list is still fully validated for
            // valid slots, uniqueness and ownership limits elsewhere. The
            // mission itself still requires physical placements to complete.
            "M10" => FurnitureRules.OwnsRequiredSet(data),
            _ => true,
        };
    }

    private static bool ValidatePlacements(List<PlacedItem> placements, string[] slots, string[] items,
        string label, bool uniqueTypes, out string error)
    {
        error = "";
        if (placements == null)
        {
            error = $"The {label} placements are missing.";
            return false;
        }
        var usedSlots = new HashSet<string>(StringComparer.Ordinal);
        var usedTypes = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            if (placement == null)
            {
                error = $"The {label} placements contain an empty record.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(placement.Type) || !items.Contains(placement.Type))
            {
                error = $"Unknown {label} item '{placement.Type}'.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(placement.Slot) || !slots.Contains(placement.Slot))
            {
                error = $"Unknown {label} pad '{placement.Slot}'.";
                return false;
            }
            if (!usedSlots.Add(placement.Slot))
            {
                error = $"The {label} pad '{placement.Slot}' is used twice.";
                return false;
            }
            if (uniqueTypes && !usedTypes.Add(placement.Type))
            {
                error = $"The {label} contains more than one '{placement.Type}'.";
                return false;
            }
        }
        return true;
    }

    private static bool ValidateFurnitureOwnership(GameData data, out string error)
    {
        error = "";
        foreach (var pair in data.FurnitureOwned)
        {
            if (!FurnitureRules.Items.Contains(pair.Key))
            {
                error = $"Unknown furniture item '{pair.Key}'.";
                return false;
            }
            if (pair.Value < 0 || pair.Value > 100)
            {
                error = $"Impossible furniture count for '{pair.Key}'.";
                return false;
            }
        }
        foreach (string item in FurnitureRules.Items)
        {
            if (FurnitureRules.Placed(data, item) > FurnitureRules.Owned(data, item))
            {
                error = $"More {item}s are placed than owned.";
                return false;
            }
        }
        return true;
    }

    private static bool ValidateBridgePieces(List<string> pieces, out string error)
    {
        error = "";
        if (!ValidateStrings(pieces, "bridge piece", out error)) return false;
        foreach (string piece in pieces)
        {
            if (!BuildRules.IsBridgeSocket(piece))
            {
                error = $"Unknown bridge socket '{piece}'.";
                return false;
            }
        }
        int supports = BuildRules.BridgeSupportCount(pieces);
        int planks = BuildRules.BridgePlankCount(pieces);
        if (planks > 0 && supports < 3)
        {
            error = "Bridge planks are present before all three supports.";
            return false;
        }
        if (supports > 3 || planks > 4)
        {
            error = "The bridge has too many pieces.";
            return false;
        }
        return true;
    }

    private static bool ValidateSwingPieces(List<string> pieces, out string error)
    {
        error = "";
        if (!ValidateStrings(pieces, "swing piece", out error)) return false;
        foreach (string piece in pieces)
        {
            if (!BuildRules.SwingSockets.Contains(piece))
            {
                error = $"Unknown swing socket '{piece}'.";
                return false;
            }
        }
        foreach (string swing in new[] { "s1", "s2" })
        {
            if (pieces.Contains($"{swing}_seat") &&
                !(pieces.Contains($"{swing}_rope_l") && pieces.Contains($"{swing}_rope_r")))
            {
                error = $"Swing '{swing}' has a seat without both ropes.";
                return false;
            }
        }
        return true;
    }

    private static bool ValidateSnapshots(GameData data, out string error)
    {
        error = "";
        if (!ValidateSnapshot(data.Player, "player", out error)) return false;
        if (!ValidateSnapshot(data.Van, "van", out error)) return false;
        if (data.Driving && (!data.Player.Valid || !data.Van.Valid))
        {
            error = "The save says the player is driving but a vehicle snapshot is missing.";
            return false;
        }
        return true;
    }

    private static bool ValidateSnapshot(PlayerSnapshot snapshot, string label, out string error)
    {
        error = "";
        if (snapshot == null)
        {
            error = $"The {label} snapshot is missing.";
            return false;
        }
        return ValidateTransform(snapshot.X, snapshot.Y, snapshot.Z, snapshot.RotationY, label, out error);
    }

    private static bool ValidateSnapshot(VehicleSnapshot snapshot, string label, out string error)
    {
        error = "";
        if (snapshot == null)
        {
            error = $"The {label} snapshot is missing.";
            return false;
        }
        return ValidateTransform(snapshot.X, snapshot.Y, snapshot.Z, snapshot.RotationY, label, out error);
    }

    private static bool ValidateTransform(float x, float y, float z, float rotation, string label, out string error)
    {
        error = "";
        if (!Finite(x) || !Finite(y) || !Finite(z) || !Finite(rotation))
        {
            error = $"The {label} position contains a non-finite number.";
            return false;
        }
        if (x < -150f || x > 150f || y < -15f || y > 100f || z < -150f || z > 150f)
        {
            error = $"The {label} position is outside the island.";
            return false;
        }
        return true;
    }

    private static bool ValidateStrings(List<string> values, string label, out string error)
    {
        error = "";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"An empty {label} identifier was found.";
                return false;
            }
            if (!seen.Add(value))
            {
                error = $"Duplicate {label} identifier '{value}'.";
                return false;
            }
        }
        return true;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
