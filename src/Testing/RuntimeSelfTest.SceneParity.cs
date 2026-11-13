using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

public partial class RuntimeSelfTest
{
    private void WorldSceneParityChecks()
    {
        Expect(Main.Probe.WorldIsCanonicalScene, "world is the canonical Town.tscn scene");
        Expect(Main.Probe.WorldAnchorCount == 75, "all 75 authored gameplay anchors resolve");
        Expect(Main.Probe.WorldTopLevelRootCount >= 140,
            "canonical town keeps its authored top-level roots");
        Expect(Main.Probe.BrokenSwingLabelCount >= 2,
            "scene-owned BROKEN swing labels resolve");

        Vector3 playground = Main.Probe.WorldNodeGlobalPosition("Playground");
        Vector3 playgroundAnchor = Main.Probe.WorldAnchorPosition("PlaygroundCenter");
        Vector3 playgroundWorksite =
            Main.Probe.WorldNodeGlobalPosition("Playground/WorksiteMarker");
        Expect(playgroundAnchor.DistanceTo(playground) < 0.01f,
            "PlaygroundCenter anchor follows the authored playground root");
        Expect(playgroundWorksite.DistanceTo(
                playground + new Vector3(0f, 0.1f, 4.5f)) < 0.01f,
            "playground worksite marker stays inside the authored sand");
        Expect(Main.Probe.PlaygroundStationPosition.DistanceTo(playgroundWorksite) < 0.01f,
            "playground interaction station follows the scene-owned worksite marker");

        foreach (string id in new[]
                 {
                     "PlayerSpawn", "VanSpawn", "JobBoard", "PlayerHouseDoor",
                     "TownHallDoor", "RedHouseDoor", "ShopDoor", "ClothesShopDoor",
                     "WorkshopDoor", "BridgeSite", "BridgeCenter", "RepairCarPosition",
                     "HousePad_0", "ParkPad_0", "GardenPad_0", "FestivalTable_0",
                     "DeliveryBoard", "MarketStock", "RallyPodium",
                 })
        {
            Vector3 position = Main.Probe.WorldAnchorPosition(id);
            Expect(Mathf.Abs(position.X) < 1000f && Mathf.Abs(position.Y) < 1000f &&
                   Mathf.Abs(position.Z) < 1000f, $"anchor '{id}' has a usable transform");
        }

        foreach (string building in new[]
                 {
                     "PlayerHouse", "TownHall", "RedHouse", "Market", "ClothesShop", "Workshop",
                 })
        {
            Expect(Main.Probe.WorldNodeExists(building), $"{building} scene root exists");
            Vector3 centre = Main.Probe.WorldNodeGlobalPosition(building);
            Vector3 door = Main.Probe.WorldNodeGlobalPosition($"{building}/DoorMarker");
            Expect(Mathf.Abs(door.X) < 1000f && Mathf.Abs(door.Y) < 1000f &&
                   Mathf.Abs(door.Z) < 1000f, $"{building} DoorMarker resolves");
            Expect(door.DistanceTo(centre) < 20f,
                $"{building} DoorMarker travels with its building");
            if (building == "ClothesShop")
            {
                GD.Print($"[SCENE BINDING] ClothesShop centre={centre} door={door}");
            }
        }

        Expect(Main.Probe.WorldNodeExists("SignSocket_market"), "market sign socket is scene-owned");
        Expect(Main.Probe.WorldNodeExists("SignSocket_workshop"), "workshop sign socket is scene-owned");
        Expect(Main.Probe.WorldNodeExists("SignSocket_green"), "green sign socket is scene-owned");
        Expect(Main.Probe.WorldNodeExists("NpcPatrolMarkers/milo/Waypoint_00"),
            "NPC patrol markers are scene-owned");
        Expect(Main.Probe.WorldNodeExists("TrafficRouteMarkers/delivery_car/Waypoint_11"),
            "free-roam traffic markers are scene-owned");
        foreach (string npc in new[] { "milo", "ada", "pip", "fern", "ollie", "ivy" })
            Expect(Main.Probe.NpcRoutePointCount(npc) >= 2, $"scene NPC route '{npc}' resolves");
        Expect(Main.Probe.TrafficRoutePointCount("town_scooter") >= 4 &&
               Main.Probe.TrafficRoutePointCount("delivery_car") >= 4,
            "both free-roam traffic routes resolve from scene markers");

        Expect(Main.Probe.PlayerVisualTemplate.EndsWith("Player_Boy_Visual.tscn",
                   StringComparison.Ordinal),
            "player visual comes from the authored boy template");
        Expect(Main.Probe.VanVisualTemplate.EndsWith("Van_Visual.tscn", StringComparison.Ordinal),
            "delivery van visual comes from the authored van template");
        foreach (string npc in new[] { "milo", "ada", "pip", "fern", "ollie", "ivy" })
        {
            string visual = Main.Probe.NpcVisualTemplate(npc);
            Expect(visual.EndsWith($"{char.ToUpperInvariant(npc[0])}{npc.Substring(1)}_Visual.tscn",
                       StringComparison.Ordinal),
                $"NPC '{npc}' uses its authored GLB template");
        }
        Expect(Main.Probe.TrafficVisualTemplate("town_scooter")
                   .EndsWith("TownCar_Green_Visual.tscn", StringComparison.Ordinal),
            "town traffic uses the authored green town-car template");
        Expect(Main.Probe.TrafficVisualTemplate("delivery_car")
                   .EndsWith("TownCar_Blue_Visual.tscn", StringComparison.Ordinal),
            "delivery traffic uses the authored blue town-car template");
        string pickupTemplates = Main.Probe.PickupVisualTemplates;
        foreach (string expected in new[]
                 {
                     "apple=Pickup_Apple.tscn", "wood=Pickup_Wood.tscn",
                     "stone=Pickup_Stone.tscn", "blue_parcel=Pickup_Parcel.tscn",
                     "orange_parcel=Pickup_Parcel.tscn", "book_sun=Pickup_Book.tscn",
                     "tyre=Pickup_RepairTyre.tscn", "lamp=Pickup_RepairLamp.tscn",
                     "battery=Pickup_RepairBattery.tscn",
                 })
        {
            Expect(pickupTemplates.Contains(expected, StringComparison.Ordinal),
                $"pickup template '{expected}' is active");
        }

        Expect(Main.Probe.RaceGateCount(false) == 4, "four-gate rally root resolves");
        Expect(Main.Probe.RaceGateCount(true) == 6, "six-gate M19 root resolves");
        for (int i = 0; i < 4; i++)
        {
            Vector3 gate = Main.Probe.RaceGatePosition(false, i);
            Expect(Mathf.Abs(gate.X) < 1000f && Mathf.Abs(gate.Z) < 1000f,
                $"four-gate checkpoint {i + 1} has a usable position");
            Expect(Mathf.Abs(Main.Probe.RaceGateYaw(false, i)) < 1000f,
                $"four-gate checkpoint {i + 1} has a usable yaw");
        }
        for (int i = 0; i < 6; i++)
        {
            Vector3 gate = Main.Probe.RaceGatePosition(true, i);
            Expect(Mathf.Abs(gate.X) < 1000f && Mathf.Abs(gate.Z) < 1000f,
                $"M19 checkpoint {i + 1} has a usable position");
            Expect(Mathf.Abs(Main.Probe.RaceGateYaw(true, i)) < 1000f,
                $"M19 checkpoint {i + 1} has a usable yaw");
        }

        Expect(Main.Probe.WorldNodeVisible("PipCarBroken"), "broken Pip car starts visible");
        Expect(!Main.Probe.WorldNodeVisible("PipCarFixed"), "fixed Pip car starts hidden");
        Expect(!Main.Probe.WorldNodeVisible("DeliveryBoard"), "delivery board starts hidden");
        Expect(!Main.Probe.WorldNodeVisible("MarketStock"), "market stock starts hidden");
        Expect(!Main.Probe.WorldNodeVisible("RallyPodium"), "rally podium starts hidden");
        Expect(!Main.Probe.WorldNodeVisible("FestivalDecor"), "festival bunting starts hidden");
        Expect(!Main.Probe.WorldNodeVisible("CelebrationDecor"), "celebration bunting starts hidden");
        Expect(!Main.Probe.WorldNodeVisible("GoldCourseFlag"), "gold lap flag starts hidden");
        Expect(Main.Probe.WorldNodeVisible("RaceGates4Root"), "four-gate rally starts visible");
        Expect(!Main.Probe.WorldNodeVisible("RaceGates6Root"), "six-gate rally starts hidden");
        Expect(Main.Probe.BuildGhostLabelsHidden,
            "empty build ghosts and their labels stay hidden outside build mode");
        Expect(Main.Probe.WorldNodeVisible("BridgeBarrier"), "broken-bridge barrier starts visible");
        Expect(Main.Probe.WorldNodeVisible("PlaygroundFixSign"),
            "playground repair sign starts visible");
        foreach (string customer in new[] { "milo", "fern", "ivy" })
            Expect(Main.Probe.WorldNodeExists($"BakeryStall/BakeryOrder_{customer}"),
                $"bakery display slot '{customer}' is scene-owned");
        foreach (string book in new[] { "sun", "moon", "tree", "star" })
            Expect(Main.Probe.WorldNodeExists($"LibraryShelf/ShelfBook_{book}"),
                $"library slot '{book}' is scene-owned");

        Expect(Main.Probe.WorldRayHit(new Vector3(-4f, 2f, 0f), new Vector3(-4f, -1f, 0f), 1u) != "",
            "main road has layer-1 collision");
        Expect(Main.Probe.WorldRayHit(new Vector3(45f, 2f, 0f), new Vector3(45f, -1f, 0f), 1u) == "",
            "river remains an open collision gap before the bridge is built");
        string wallHit = Main.Probe.WorldRayHit(new Vector3(-44.8f, 1.5f, 24f),
            new Vector3(-37f, 1.5f, 24f), 1u);
        Expect(wallHit.StartsWith("WallWest", StringComparison.Ordinal),
            "player-house wall retains layer-1 collision");
        string doorRay = Main.Probe.WorldRayHit(new Vector3(-38f, 2.4f, 20f),
            new Vector3(-38f, 2.4f, 17f), 16u);
        Expect(doorRay == "DoorCameraBlocker",
            "player-house doorway keeps its layer-16 camera blocker");
    }

}
