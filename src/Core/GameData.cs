using System;
using System.Collections.Generic;

namespace PatchworkIsland;

/// <summary>
/// Plain serialisable save model. No Godot types live here so mission rules and
/// persistence can be tested in a normal .NET test harness.
/// </summary>
public sealed class GameData
{
    public int SchemaVersion { get; set; } = 1;

    public string ActiveMission { get; set; } = "";
    public List<string> CompletedMissions { get; set; } = new();
    public List<string> TutorialsSeen { get; set; } = new();
    public List<string> CollectedPickups { get; set; } = new();
    public List<string> UnlockedCosmetics { get; set; } = new();

    // P9 character personalisation. These are additive, optional fields inside
    // schema version 1 so v1.0.x saves load with safe defaults.
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
        if (SchemaVersion <= 0) SchemaVersion = 1;
        ActiveMission ??= "";
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
