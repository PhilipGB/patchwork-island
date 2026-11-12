using System;
using System.Collections.Generic;

namespace PatchworkIsland;

/// <summary>
/// Plain serialisable save model. No Godot types live here so mission rules and
/// persistence can be tested in a normal .NET test harness.
/// </summary>
public sealed class GameData
{
    public int SchemaVersion { get; set; } = SaveService.CurrentVersion;

    public string ActiveMission { get; set; } = "";
    public List<string> CompletedMissions { get; set; } = new();
    public List<string> TutorialsSeen { get; set; } = new();
    public List<string> CollectedPickups { get; set; } = new();
    public List<string> UnlockedCosmetics { get; set; } = new();

    // P9 character personalisation, introduced by save schema 2. Older schema
    // 1 saves have no such fields and receive these defaults when migrated.
    public string CharacterPreset { get; set; } = CharacterCustomisation.DefaultPreset;
    public List<string> OwnedOutfits { get; set; } = new();
    public string EquippedOutfit { get; set; } = CharacterCustomisation.DefaultOutfit;

    public int Coins { get; set; }
    public Dictionary<string, int> Inventory { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> MissionProgress { get; set; } = new();
    public Dictionary<string, int> HintLevels { get; set; } = new();

    // World construction / customisation state used by later milestones.
    public List<string> BridgePieces { get; set; } = new();
    public List<string> SwingPieces { get; set; } = new();
    public List<PlacedItem> ParkPlacements { get; set; } = new();
    public List<PlacedItem> FurniturePlacements { get; set; } = new();
    public Dictionary<string, int> FurnitureOwned { get; set; } = new();

    public PlayerSnapshot Player { get; set; } = new();
    public VehicleSnapshot Van { get; set; } = new();
    public bool Driving { get; set; }
    public SettingsData Settings { get; set; } = new();

    public bool CelebrationSeen { get; set; }

    // v1.2 second-chapter finale. Deliberately separate from CelebrationSeen,
    // which permanently means the original M01–M10 celebration.
    public bool FestivalSeen { get; set; }

    // v1.4 city expansion. Town progress above remains the single source of
    // truth for M01–M21; city jobs have their own ids, progress and wallet
    // bookkeeping so they cannot alter the village mission graph.
    public bool CityRoadworksCompleted { get; set; }
    public bool CityRoadworksRewardClaimed { get; set; }
    public bool CityRoadOpen { get; set; }
    public bool CityVisited { get; set; }
    public string ActiveCityMission { get; set; } = "";
    public List<string> CompletedCityMissions { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> CityMissionProgress { get; set; } = new();
    public int CitySeed { get; set; } = 20261111;
    public double CityLogicalSeconds { get; set; }

    public long SavedAtUnix { get; set; }

    public void Normalise()
    {
        CompletedMissions ??= new();
        TutorialsSeen ??= new();
        CollectedPickups ??= new();
        UnlockedCosmetics ??= new();
        CharacterPreset = CharacterCustomisation.NormalisePreset(CharacterPreset);
        OwnedOutfits ??= new();
        EquippedOutfit = CharacterCustomisation.NormaliseOutfit(EquippedOutfit);
        Inventory ??= new();
        MissionProgress ??= new();
        HintLevels ??= new();
        BridgePieces ??= new();
        SwingPieces ??= new();
        ParkPlacements ??= new();
        FurniturePlacements ??= new();
        FurnitureOwned ??= new();
        Player ??= new();
        Van ??= new();
        Settings ??= new();
        ActiveCityMission ??= "";
        CompletedCityMissions ??= new();
        CityMissionProgress ??= new();
        if (CitySeed == 0) CitySeed = 20261111;
        if (double.IsNaN(CityLogicalSeconds) || double.IsInfinity(CityLogicalSeconds) ||
            CityLogicalSeconds < 0)
            CityLogicalSeconds = 0;
        if (SchemaVersion <= 0) SchemaVersion = 1;
        ActiveMission ??= "";
        NormaliseRepairPickups();
    }

    /// <summary>
    /// v1.2.0 wrote M18 carried/fitted counts without the physical pickup IDs.
    /// Infer how many parts were actually acquired and record that many IDs so a
    /// legacy partial save cannot respawn a duplicate after upgrading. IDs that
    /// are already recorded always win; this never adds more than were earned.
    /// </summary>
    private void NormaliseRepairPickups()
    {
        int acquiredTyres = Math.Clamp(
            GetMissionCounter("M18", "has_tyre") + GetMissionCounter("M18", "tyres_fitted"),
            0, FinaleRules.RequiredTyres);
        int acquiredLamps = Math.Clamp(
            GetMissionCounter("M18", "has_lamp") + GetMissionCounter("M18", "lamp_fitted"),
            0, FinaleRules.RequiredLamps);
        int acquiredBatteries = Math.Clamp(
            GetMissionCounter("M18", "has_battery") + GetMissionCounter("M18", "battery_fitted"),
            0, FinaleRules.RequiredBatteries);

        MarkAcquiredRepairPickups("tyre", acquiredTyres);
        MarkAcquiredRepairPickups("lamp", acquiredLamps);
        MarkAcquiredRepairPickups("battery", acquiredBatteries);
    }

    private void MarkAcquiredRepairPickups(string part, int acquired)
    {
        if (acquired <= 0) return;
        int recorded = 0;
        foreach (string pickupId in FinaleRules.RepairPickupIds)
            if (FinaleRules.RepairPickupPart(pickupId) == part && CollectedPickups.Contains(pickupId))
                recorded++;
        int missing = acquired - recorded;
        if (missing <= 0) return;
        foreach (string pickupId in FinaleRules.RepairPickupIds)
        {
            if (missing <= 0) break;
            if (FinaleRules.RepairPickupPart(pickupId) != part) continue;
            if (CollectedPickups.Contains(pickupId)) continue;
            CollectedPickups.Add(pickupId);
            missing--;
        }
    }

    public int GetItem(string item)
    {
        return Inventory.TryGetValue(item, out int value) ? value : 0;
    }

    public void AddItem(string item, int amount = 1)
    {
        Inventory[item] = GetItem(item) + amount;
    }

    public bool RemoveItem(string item, int amount)
    {
        int have = GetItem(item);
        if (have < amount) return false;
        Inventory[item] = have - amount;
        if (Inventory[item] <= 0) Inventory.Remove(item);
        return true;
    }

    public int GetMissionCounter(string missionId, string key)
    {
        if (!MissionProgress.TryGetValue(missionId, out var counters) || counters == null)
            return 0;
        return counters.TryGetValue(key, out int value) ? value : 0;
    }

    public void SetMissionCounter(string missionId, string key, int value)
    {
        if (!MissionProgress.TryGetValue(missionId, out var counters) || counters == null)
        {
            counters = new Dictionary<string, int>();
            MissionProgress[missionId] = counters;
        }
        counters[key] = value;
    }

    public void IncrementMissionCounter(string missionId, string key, int amount = 1)
    {
        SetMissionCounter(missionId, key, GetMissionCounter(missionId, key) + amount);
    }

    public int GetCityMissionCounter(string missionId, string key)
    {
        if (!CityMissionProgress.TryGetValue(missionId, out var counters) || counters == null)
            return 0;
        return counters.TryGetValue(key, out int value) ? value : 0;
    }

    public void SetCityMissionCounter(string missionId, string key, int value)
    {
        if (!CityMissionProgress.TryGetValue(missionId, out var counters) || counters == null)
        {
            counters = new Dictionary<string, int>();
            CityMissionProgress[missionId] = counters;
        }
        counters[key] = value;
    }

    public bool IsCompleted(string missionId) => CompletedMissions.Contains(missionId);

    public bool HasCollected(string pickupId) => CollectedPickups.Contains(pickupId);

    public bool IsTutorialSeen(string id) => TutorialsSeen.Contains(id);

    public void MarkTutorial(string id)
    {
        if (!TutorialsSeen.Contains(id)) TutorialsSeen.Add(id);
    }

    public int GetHintLevel(string missionId) => HintLevels.TryGetValue(missionId, out int value) ? value : 0;

    public void SetHintLevel(string missionId, int level) => HintLevels[missionId] = level;
}

public sealed class PlacedItem
{
    public string Type { get; set; } = "";
    public string Slot { get; set; } = "";

    public PlacedItem() { }

    public PlacedItem(string type, string slot)
    {
        Type = type;
        Slot = slot;
    }
}

public sealed class PlayerSnapshot
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float RotationY { get; set; }
    public bool Valid { get; set; }
}

public sealed class VehicleSnapshot
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float RotationY { get; set; }
    public bool Valid { get; set; }
}

public sealed class SettingsData
{
    public bool MusicEnabled { get; set; } = true;
    public bool SfxEnabled { get; set; } = true;
    public bool ReducedMotion { get; set; }
    public float TextScale { get; set; } = 1f;
}
