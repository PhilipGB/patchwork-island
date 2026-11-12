using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PatchworkIsland;

namespace PatchworkIsland.Tests;

internal static partial class Program
{
    private static void SceneContractsTests(MissionCatalog catalog, CityMissionCatalog cityCatalog, string root, string json)
    {
        Run("authored road slabs, road colliders and RoadNetwork agree", () =>
        {
            string root = FindRepoRoot();
            string environment = Path.Combine(root, "scenes", "world", "assets", "environment");
            var slabs = ParseRoadSlabs(Path.Combine(environment, "TownRoads_Collidable.tscn"));
            var reference = ParseRoadSlabs(Path.Combine(environment, "TownRoads.tscn"));
            Equal(7, slabs.Count, "authored road slab count");
            Equal(reference.Count, slabs.Count, "reference road slab count");
            foreach (var slab in slabs)
            {
                var matches = reference.Where(candidate => candidate.Name == slab.Name).ToList();
                Equal(1, matches.Count, $"reference road slab {slab.Name}");
                Check(SameRoadRect(slab, matches[0]),
                    $"reference slab {slab.Name} is {Describe(matches[0])}, not {Describe(slab)}");
            }

            var colliders = ParseRoadColliders(
                Path.Combine(environment, "TownRoads_Collidable.tscn"));
            Equal(slabs.Count, colliders.Count, "one collider per road slab");
            var unmatched = new List<(string Name, float X, float Z, float HalfX, float HalfZ)>(colliders);
            foreach (var slab in slabs)
            {
                int index = unmatched.FindIndex(candidate => SameRoadRect(slab, candidate));
                Check(index >= 0, $"no collision shape matches road slab {slab.Name} " +
                                  $"at {Describe(slab)}");
                unmatched.RemoveAt(index);
            }
            Equal(0, unmatched.Count, "road colliders without a visible slab");

            Equal(slabs.Count, RoadNetwork.Roads.Count, "RoadNetwork road rectangle count");
            unmatched = RoadNetwork.Roads
                .Select(road => ("RoadNetwork", road.X, road.Z, road.HalfX, road.HalfZ))
                .ToList();
            foreach (var slab in slabs)
            {
                int index = unmatched.FindIndex(candidate => SameRoadRect(slab, candidate));
                Check(index >= 0, $"RoadNetwork has no rectangle for visible road slab " +
                                  $"{slab.Name} at {Describe(slab)}");
                unmatched.RemoveAt(index);
            }
            Equal(0, unmatched.Count, "RoadNetwork rectangles without a visible slab");
        });

        Run("authored buildings keep source centres and wall dimensions", () =>
        {
            string root = FindRepoRoot();
            using var manifest = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(root, "docs", "layout_manifest_v1.3.0.json")));
            string town = File.ReadAllText(Path.Combine(root, "scenes", "world",
                "Town.tscn"));
            foreach (var building in manifest.RootElement.GetProperty("building_instances")
                         .EnumerateArray())
            {
                string id = building.GetProperty("id").GetString();
                var position = building.GetProperty("position");
                var actual = ParseInstancePosition(town, id);
                var want = (position[0].GetSingle(), position[1].GetSingle(), position[2].GetSingle());
                Check(Distance(actual, want) <= 0.01f,
                    $"{id} instance moved to {actual}, expected {want}");

                string part = File.ReadAllText(Path.Combine(root, "scenes", "world",
                    "assets", "buildings", id + ".tscn"));
                var west = ParseWallSize(part, "WallWest");
                Check(Math.Abs(west.Y - building.GetProperty("wall_height").GetSingle()) <= 0.01f,
                    $"{id} wall height {west.Y}");
                Check(Math.Abs(west.Z - building.GetProperty("depth").GetSingle()) <= 0.01f,
                    $"{id} depth {west.Z}");
                var back = ParseWallSize(part, "WallBack");
                Check(Math.Abs(back.X - building.GetProperty("width").GetSingle()) <= 0.01f,
                    $"{id} width {back.X}");
            }
        });

        Run("authored town keeps static collision and dynamic finale roots", () =>
        {
            string root = FindRepoRoot();
            string town = File.ReadAllText(Path.Combine(root, "scenes", "world",
                "Town.tscn"));
            var required = new[]
            {
                "BridgeBarrier", "PipCarBroken", "PipCarFixed", "DeliveryBoard",
                "MarketStock", "RallyPodium", "FestivalDecor", "CelebrationDecor",
                "RaceGates4Root", "RaceGates6Root", "GameplayAnchors",
                "NpcPatrolMarkers", "TrafficRouteMarkers",
            };
            foreach (string id in required)
                Check(town.Contains($"name=\"{id}\"", StringComparison.Ordinal),
                    $"Town.tscn is missing {id}");
            string rally = File.ReadAllText(Path.Combine(root, "scenes", "world", "assets",
                "environment", "RallyRoad_Collidable.tscn"));
            Check(rally.Contains("RallyRoadCollision", StringComparison.Ordinal),
                "rally road part has no collision body");
            string house = File.ReadAllText(Path.Combine(root, "scenes", "world", "assets",
                "buildings", "PlayerHouse.tscn"));
            Check(house.Contains("DoorCameraBlocker", StringComparison.Ordinal) &&
                  house.Contains("collision_layer = 16", StringComparison.Ordinal),
                "enterable house keeps its layer-16 camera blocker");
        });

        Run("scene NPC and traffic markers match the authored routes", () =>
        {
            string root = FindRepoRoot();
            using var manifest = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(root, "docs", "layout_manifest_v1.3.0.json")));
            var npcRoutes = ParseMarkersByParent(Path.Combine(root, "scenes", "world",
                "NpcPatrolMarkers.tscn"));
            foreach (var route in manifest.RootElement.GetProperty("npc_patrol_routes").EnumerateObject())
            {
                Check(npcRoutes.TryGetValue(route.Name, out var points),
                    $"missing NPC route {route.Name}");
                Equal(route.Value.GetArrayLength(), points.Count, $"{route.Name} waypoint count");
                int i = 0;
                foreach (var point in route.Value.EnumerateArray())
                {
                    var want = (point[0].GetSingle(), point[1].GetSingle(), point[2].GetSingle());
                    Check(Distance(points[i], want) <= 0.01f,
                        $"{route.Name} waypoint {i} moved");
                    i++;
                }
            }

            var traffic = ParseMarkersByParent(Path.Combine(root, "scenes", "world",
                "TrafficRouteMarkers.tscn"));
            foreach (var route in manifest.RootElement.GetProperty("traffic_routes").EnumerateObject())
            {
                Check(traffic.TryGetValue(route.Name, out var points),
                    $"missing traffic route {route.Name}");
                Equal(route.Value.GetArrayLength(), points.Count, $"{route.Name} waypoint count");
                int i = 0;
                foreach (var point in route.Value.EnumerateArray())
                {
                    var want = (point[0].GetSingle(), point[1].GetSingle(), point[2].GetSingle());
                    Check(Distance(points[i], want) <= 0.01f,
                        $"{route.Name} waypoint {i} moved");
                    i++;
                }
            }
        });

    }
}
