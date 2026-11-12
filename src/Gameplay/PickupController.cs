using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>Pickup node lifecycle and interaction feedback, backed by live mission services.</summary>
internal sealed class PickupController
{
    private readonly GameSession _session;
    private readonly Node3D _root;
    private readonly WorldModel _world;
    private readonly Hud _hud;
    private readonly PlayerController _player;
    private readonly List<Pickup> _pickups;
    private readonly AudioManager _audio;
    private readonly IGameplayHost _host;
    private GameData _data => _session.Data;
    private MissionService _missions => _session.Village;
    internal PickupController(GameSession session, Node3D root, WorldModel world,
        Hud hud, PlayerController player, List<Pickup> pickups, AudioManager audio, IGameplayHost host)
    {
        _session = session; _root = root; _world = world; _hud = hud;
        _player = player; _pickups = pickups; _audio = audio; _host = host;
    }
    internal void SpawnPickups()
    {
        SpawnItemSet("apple", "M01", 3, _world.AppleSpawnPoints, Palette.Red);
        SpawnItemSet("wood", "M02", 5, _world.WoodSpawnPoints, Palette.Wood);
        SpawnItemSet("stone", "M02", 3, _world.StoneSpawnPoints, Palette.Stone);
        SpawnParcelPickups();
        SpawnBookPickups();
        SpawnRepairParts();
        SpawnRoadworksPickups();
    }

    internal void SpawnRoadworksPickups()
    {
        var positions = new[]
        {
            new Vector3(20.5f, 0.2f, -5.4f),
            new Vector3(22.2f, 0.2f, -5.4f),
            new Vector3(23.9f, 0.2f, -5.4f),
            new Vector3(26.4f, 0.2f, -5.0f),
            new Vector3(28.4f, 0.2f, -5.0f),
            new Vector3(30.2f, 0.2f, -5.0f),
        };
        string[] items = { "safety_cone", "safety_cone", "safety_cone",
            "barrier_panel", "barrier_panel", "toolbox" };
        int[] required = { 3, 3, 3, 2, 2, 1 };
        for (int i = 0; i < items.Length; i++)
        {
            string item = items[i];
            int max = required[i];
            string pickupId = RoadworksRules.SupplyPickupIds[i];
            var pickup = Pickup.Create(_root, positions[i], pickupId, item,
                RoadworksRules.MissionId, max, OnRoadworksSupplyPicked,
                () => $"E: Pick up {MissionService.ItemName(item).ToLowerInvariant()}",
                () => _missions.GetStatus(RoadworksRules.MissionId) != MissionStatus.Active);
            _pickups.Add(pickup);
        }
    }

    internal void OnRoadworksSupplyPicked(Pickup pickup)
    {
        var result = _missions.CollectPickup(pickup.PickupId, pickup.ItemType,
            RoadworksRules.MissionId, pickup.Required);
        if (!result.Success)
        {
            _hud.ShowHint(result.Message);
            return;
        }
        pickup.CollectVisual();
        _hud.ShowBigToast(result.Message, 3f, Palette.Orange);
        _audio?.PlaySfx("coin_collect", -7f);
        _host.RefreshMission();
        _host.Save(SaveReason.Autosave);
    }

    internal void SpawnParcelPickups()
    {
        string[] types = { "blue_parcel", "blue_parcel", "orange_parcel" };
        for (int i = 0; i < types.Length && i < _world.ParcelSpawnPoints.Count; i++)
        {
            string type = types[i];
            string id = type == "blue_parcel" ? $"blue_parcel_{i}" : "orange_parcel_0";
            if (_data.HasCollected(id)) continue;
            int required = type == "blue_parcel" ? 2 : 1;
            var pickup = Pickup.Create(_root, _world.ParcelSpawnPoints[i], id, type, "M06", required,
                TakePickup, () => PickupPrompt(type, "M06", required),
                () => _data.IsCompleted("M06"));
            pickup.ProcessMode = Node.ProcessModeEnum.Pausable;
            _pickups.Add(pickup);
        }
    }

    internal void SpawnBookPickups()
    {
        foreach (var entry in _world.BookSpawnPoints)
        {
            string pickupId = $"book_{entry.Book}";
            if (_data.HasCollected(pickupId)) continue;
            string book = entry.Book;
            var pickup = Pickup.Create(_root, entry.Position, pickupId, pickupId, "M13", 1,
                TakeBookPickup,
                () => _data.ActiveMission == "M13"
                    ? $"E: Pick up the {book.ToUpperInvariant()} book"
                    : "E: A library book for Ollie",
                () => _data.IsCompleted("M13"));
            pickup.ProcessMode = Node.ProcessModeEnum.Pausable;
            _pickups.Add(pickup);
        }
    }

    internal void SpawnRepairParts()
    {
        if (_data.IsCompleted("M18")) return;
        for (int i = 0; i < FinaleRules.RepairPickupIds.Length &&
                        i < _world.RepairPartSpawnPoints.Count; i++)
        {
            string pickupId = FinaleRules.RepairPickupIds[i];
            string part = FinaleRules.RepairPickupPart(pickupId);
            if (string.IsNullOrEmpty(part)) continue;
            if (_data.HasCollected(pickupId)) continue;
            var pickup = Pickup.Create(_root, _world.RepairPartSpawnPoints[i], pickupId, part,
                "M18", 1, TakeRepairPart,
                () => _data.ActiveMission == "M18"
                    ? $"E: Pick up the {FinaleRules.PartLabel(part).ToLowerInvariant()}"
                    : "E: A repair part for Pip's car",
                () => _data.IsCompleted("M18"));
            pickup.ProcessMode = Node.ProcessModeEnum.Pausable;
            _pickups.Add(pickup);
        }
    }

    internal void TakeRepairPart(Pickup pickup)
    {
        if (_data.ActiveMission != "M18")
        {
            _hud.ShowHint("Start Mechanic's Toolbox at the yellow JOBS board first.");
            return;
        }
        var result = _missions.CollectRepairPart(pickup.ItemType, pickup.PickupId);
        if (!result.Success)
        {
            _hud.ShowHint(result.Message);
            return;
        }
        pickup.CollectVisual();
        _pickups.Remove(pickup);
        AudioManager.PlayWorld("parcel_deliver", -6f);
        _hud.ShowToast(result.Message, 2.4f, Palette.Orange);
        _host.RefreshMission();
        _player.ForceRefreshInteraction();
    }

    internal void TakeBookPickup(Pickup pickup)
    {
        if (_data.ActiveMission != "M13")
        {
            _hud.ShowHint("Start Lost Library Books at the yellow JOBS board first.");
            return;
        }
        string book = pickup.ItemType.StartsWith("book_", StringComparison.Ordinal)
            ? pickup.ItemType.Substring(5)
            : "";
        var result = _missions.CollectBook(book);
        if (!result.Success)
        {
            _hud.ShowHint(result.Message);
            return;
        }
        pickup.CollectVisual();
        _pickups.Remove(pickup);
        AudioManager.PlayWorld("book_collect", -5f);
        _hud.ShowToast(result.Message, 2.4f, Palette.Blue);
        _host.RefreshMission();
        _player.ForceRefreshInteraction();
    }

    internal void SpawnItemSet(string item, string missionId, int required, List<Vector3> positions, Color color)
    {
        for (int i = 0; i < positions.Count && i < required; i++)
        {
            string id = $"{item}_{i}";
            if (_data.HasCollected(id)) continue;
            var pickup = Pickup.Create(_root, positions[i], id, item, missionId, required, TakePickup,
                () => PickupPrompt(item, missionId, required),
                () => _data.IsCompleted(missionId));
            pickup.ProcessMode = Node.ProcessModeEnum.Pausable;
            _pickups.Add(pickup);
        }
    }

    internal string PickupPrompt(string item, string missionId, int required)
    {
        if (_data.IsCompleted(missionId)) return "E: Already delivered";
        if (_data.ActiveMission != missionId)
            return $"E: {MissionService.ItemName(item)} for a job";
        int count = _data.GetItem(item);
        if (count >= required) return "E: You have enough";
        return $"E: Pick up {MissionService.ItemName(item).ToLowerInvariant()} ({count + 1}/{required})";
    }

    internal void TakePickup(Pickup pickup)
    {
        if (_data.IsCompleted(pickup.MissionId))
        {
            _hud.ShowBigToast("That job is already finished.");
            return;
        }
        if (_data.ActiveMission != pickup.MissionId)
        {
            string title = _missions.Catalog.Get(pickup.MissionId)?.Title ?? pickup.MissionId;
            _hud.ShowHint($"Start {title} at the yellow JOBS board first.");
            return;
        }

        var result = _missions.CollectPickup(pickup.PickupId, pickup.ItemType, pickup.MissionId, pickup.Required);
        if (!result.Success)
        {
            _hud.ShowHint(result.Message);
            return;
        }

        pickup.CollectVisual();
        _pickups.Remove(pickup);
        _host.RefreshMission();
        _hud.ShowToast(result.Message, 2.1f, ItemColor(pickup.ItemType));
        _player.ForceRefreshInteraction();
    }

    internal static Color ItemColor(string item) => item switch
    {
        "apple" => Palette.Red,
        "wood" => Palette.Wood,
        "stone" => Palette.Stone,
        "blue_parcel" => Palette.Blue,
        "orange_parcel" => Palette.Orange,
        "book_sun" => Palette.Yellow,
        "book_moon" => Palette.Blue,
        "book_tree" => Palette.Grass,
        "book_star" => Palette.Pink,
        _ => Palette.Yellow,
    };

    internal void RefreshPickupVisibility()
    {
        foreach (var pickup in _pickups)
        {
            if (!GodotObject.IsInstanceValid(pickup)) continue;
            bool completed = _data.IsCompleted(pickup.MissionId);
            bool hidden = pickup.HiddenProvider?.Invoke() == true;
            bool available = _missions.GetStatus(pickup.MissionId) is MissionStatus.Active or MissionStatus.Available;
            pickup.MissionAvailable = available;
            pickup.Priority = _data.ActiveMission == pickup.MissionId ? 1 : 0;
            pickup.Visible = !completed && !hidden && available;
            pickup.SetProcess(!completed && !hidden && available);
        }
    }

}
