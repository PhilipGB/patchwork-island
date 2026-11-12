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
    private static void AppearanceContractsTests(MissionCatalog catalog, CityMissionCatalog cityCatalog, string root, string json)
    {
        Run("gameplay anchors match the historical v1.2.1 manifest with canonical overrides", () =>
        {
            string root = FindRepoRoot();
            using var manifest = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(root, "docs", "layout_manifest_v1.2.1.json")));
            var expected = manifest.RootElement.GetProperty("anchors");
            var markers = ParseMarkers(Path.Combine(root, "scenes", "world", "GameplayAnchors.tscn"),
                parent: ".");
            Equal(75, markers.Count, "gameplay anchor count");

            // v1.3.0 relocated the playground inside the canonical scene. The
            // v1.2.1 manifest remains the historical baseline for every other
            // anchor; PlaygroundCenter is deliberately scene-derived and has a
            // dedicated consistency check below.
            var canonicalOverrides = new Dictionary<string, (float X, float Y, float Z)>
            {
                ["PlaygroundCenter"] = ParseInstancePosition(
                    File.ReadAllText(Path.Combine(root, "scenes", "world", "Town.tscn")),
                    "Playground"),
            };

            int compared = 0;
            foreach (var anchor in expected.EnumerateObject())
            {
                var value = anchor.Value;
                var want = canonicalOverrides.TryGetValue(anchor.Name, out var canonical)
                    ? canonical
                    : (value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle());
                Check(markers.TryGetValue(anchor.Name, out var got), $"missing anchor {anchor.Name}");
                Check(Distance(got, want) <= 0.01f,
                    $"anchor {anchor.Name} is {got} not {want}");
                compared++;
            }
            Equal(markers.Count, compared, "every source anchor is represented");
        });

        Run("playground anchor and worksite follow the authored scene", () =>
        {
            string root = FindRepoRoot();
            var centre = ParseInstancePosition(
                File.ReadAllText(Path.Combine(root, "scenes", "world", "Town.tscn")),
                "Playground");
            var playgroundMarkers = ParseMarkers(
                Path.Combine(root, "scenes", "world", "assets", "landmarks", "Playground.tscn"),
                parent: ".");
            Check(playgroundMarkers.TryGetValue("WorksiteMarker", out var worksite),
                "Playground is missing its scene-owned WorksiteMarker");
            var anchors = ParseMarkers(
                Path.Combine(root, "scenes", "world", "GameplayAnchors.tscn"), parent: ".");
            Check(anchors.TryGetValue("PlaygroundCenter", out var anchor),
                "missing PlaygroundCenter anchor");
            Check(Distance(anchor, centre) <= 0.01f,
                $"PlaygroundCenter anchor {anchor} does not follow the playground root {centre}");
            Check(MathF.Abs(worksite.Z - 4.5f) <= 0.01f && MathF.Abs(worksite.Y - 0.1f) <= 0.01f,
                $"playground WorksiteMarker moved off the sand: {worksite}");
        });

        Run("all six building gables sit on the ridge ends", () =>
        {
            string root = FindRepoRoot();
            using var manifest = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(root, "docs", "layout_manifest_v1.3.0.json")));
            int checkedBuildings = 0;
            foreach (var building in manifest.RootElement
                         .GetProperty("building_instances").EnumerateArray())
            {
                string id = building.GetProperty("id").GetString();
                float width = building.GetProperty("width").GetSingle();
                float depth = building.GetProperty("depth").GetSingle();
                string scene = Path.Combine(root, building.GetProperty("scene").GetString());
                string text = File.ReadAllText(scene);
                Check(!text.Contains("GableFront") && !text.Contains("GableBack"),
                    $"{id} still places its gables on the eave walls");

                var east = ParseNodePosition(text, "GableEast");
                var west = ParseNodePosition(text, "GableWest");
                var eastRotation = ParseNodeRotation(text, "GableEast");
                var westRotation = ParseNodeRotation(text, "GableWest");
                float expectedX = width * 0.5f;
                Check(MathF.Abs(east.X - expectedX) <= 0.02f,
                    $"{id} east gable is at x={east.X}, expected {expectedX}");
                Check(MathF.Abs(west.X + expectedX) <= 0.02f,
                    $"{id} west gable is at x={west.X}, expected {-expectedX}");
                Check(MathF.Abs(east.Z) <= 0.02f && MathF.Abs(west.Z) <= 0.02f,
                    $"{id} gables drifted onto an eave wall");
                Check(MathF.Abs(MathF.Abs(eastRotation.Y) - MathF.PI / 2f) <= 0.01f &&
                      MathF.Abs(MathF.Abs(westRotation.Y) - MathF.PI / 2f) <= 0.01f,
                    $"{id} gables are not rotated onto the ridge ends");

                var prisms = ParseBoxSizes(text, "PrismMesh");
                Check(prisms.TryGetValue("GableMesh", out var gable),
                    $"{id} has no GableMesh");
                Check(MathF.Abs(gable.X - (depth + 0.3f)) <= 0.05f,
                    $"{id} gable width {gable.X} does not span depth {depth}");
                Check(gable.Y >= 1.8f && gable.Y <= 2.8f,
                    $"{id} gable height {gable.Y} is outside the roof profile");
                Check(Regex.IsMatch(text,
                        "\\[sub_resource type=\"StandardMaterial3D\" id=\"StandardMaterial3D_Gable\"\\]" +
                        "[\\s\\S]*?disable_receive_shadows = true"),
                    $"{id} gable still receives the roof-overhang shadow");
                Check(Regex.Matches(text,
                        "material_override = SubResource\\(\"StandardMaterial3D_Gable\"\\)").Count == 2,
                    $"{id} gable shadow-free material is not applied to both ends");
                checkedBuildings++;
            }
            Equal(6, checkedBuildings, "all six authored buildings checked");
        });

        Run("celebration banner is compact, free-standing and double-faced", () =>
        {
            string root = FindRepoRoot();
            string text = File.ReadAllText(Path.Combine(root, "scenes", "world",
                "assets", "landmarks", "CelebrationDecor.tscn"));
            Check(!text.Contains("billboard = 1"),
                "celebration labels must not billboard through the panel");
            var sizes = ParseBoxSizes(text, "BoxMesh");
            Check(sizes.TryGetValue("BoxMesh_57", out var banner), "banner mesh missing");
            Check(banner.X <= 5f && banner.Y <= 0.8f,
                $"celebration banner is still oversized: {banner.X}x{banner.Y}");
            Check(banner.X >= 4.7f,
                $"celebration banner panel leaves a gap to its posts: {banner.X}");
            var position = ParseNodePosition(text, "CelebrationSign");
            Check(position.Z >= 19f && position.Z <= 21f && position.Y >= 3f && position.Y <= 4f,
                $"celebration banner is not at the festival entrance: {position}");
            Check(text.Contains("MessageFront") && text.Contains("MessageBack"),
                "celebration banner needs front and back text faces");
            Check(text.Contains("PostWest") && text.Contains("PostEast"),
                "celebration banner is not free-standing on its own posts");
            string town = File.ReadAllText(Path.Combine(root, "scenes", "world", "Town.tscn"));
            var lantern = ParseInstancePosition(town, "FestivalLantern_2");
            Check(MathF.Abs(lantern.X - 15f) >= 2.5f,
                $"the centre festival lantern blocks the banner back face at x={lantern.X}");
        });

        Run("directional signpost is one clean stack away from Milo", () =>
        {
            string root = FindRepoRoot();
            string signText = File.ReadAllText(Path.Combine(root, "scenes", "world",
                "assets", "landmarks", "DirectionalSign.tscn"));
            Check(!signText.Contains("billboard = 1"),
                "directional labels must not billboard off their boards");
            Equal(4, Regex.Matches(signText, "\\[node name=\"Sign_[0-9]+\"").Count,
                "directional sign count");
            Equal(4, Regex.Matches(signText, "\\[node name=\"Label_[0-9]+\"").Count,
                "directional front label count");
            Equal(4, Regex.Matches(signText, "\\[node name=\"Label_[0-9]+_Back\"").Count,
                "directional back label count");
            string[] frontArrows = { "RED HOUSE →", "← WORKSHOP", "← SHOP", "← BRIDGE" };
            string[] backArrows = { "← RED HOUSE", "WORKSHOP →", "SHOP →", "BRIDGE →" };
            for (int i = 0; i < 4; i++)
            {
                Equal(frontArrows[i], ParseNodeText(signText, $"Label_{i}"),
                    $"directional front arrow {i} points the wrong way");
                Equal(backArrows[i], ParseNodeText(signText, $"Label_{i}_Back"),
                    $"directional back arrow {i} points the wrong way");
            }

            string town = File.ReadAllText(Path.Combine(root, "scenes", "world", "Town.tscn"));
            var direction = ParseInstancePosition(town, "DirectionPost");
            using var manifest = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(root, "docs", "layout_manifest_v1.3.0.json")));
            float nearest = float.MaxValue;
            foreach (var point in manifest.RootElement
                         .GetProperty("npc_patrol_routes").GetProperty("milo").EnumerateArray())
            {
                float dx = direction.X - point[0].GetSingle();
                float dz = direction.Z - point[2].GetSingle();
                nearest = MathF.Min(nearest, MathF.Sqrt(dx * dx + dz * dz));
            }
            Check(nearest > 2.5f,
                $"directional signpost is still inside Milo's patrol area ({nearest:0.0} m)");
            foreach (string neighbour in new[] { "DeliveryBoard", "MarketStock" })
            {
                var other = ParseInstancePosition(town, neighbour);
                float dx = direction.X - other.X;
                float dz = direction.Z - other.Z;
                float distance = MathF.Sqrt(dx * dx + dz * dz);
                Check(distance > 4f,
                    $"directional signpost still crowds {neighbour} ({distance:0.0} m)");
            }
            var delivery = ParseInstancePosition(town, "DeliveryBoard");
            var stock = ParseInstancePosition(town, "MarketStock");
            float propDx = delivery.X - stock.X;
            float propDz = delivery.Z - stock.Z;
            Check(MathF.Sqrt(propDx * propDx + propDz * propDz) > 5f,
                "MarketStock is still hidden behind DeliveryBoard");
        });

        Run("world flags and vehicle status labels avoid billboard clipping", () =>
        {
            string root = FindRepoRoot();
            foreach (string scene in new[] { "GoldCourseFlag.tscn", "RallyPodium.tscn" })
            {
                string text = File.ReadAllText(Path.Combine(root, "scenes", "world",
                    "assets", "landmarks", scene));
                Check(!text.Contains("billboard = 1"),
                    $"{scene} still uses a billboard label");
            }
            string gold = File.ReadAllText(Path.Combine(root, "scenes", "world",
                "assets", "landmarks", "GoldCourseFlag.tscn"));
            Check(gold.Contains("FlagTextFront") && gold.Contains("FlagTextBack"),
                "GOLD LAP needs two correctly oriented faces");
            string town = File.ReadAllText(Path.Combine(root, "scenes", "world", "Town.tscn"));
            var goldFlag = ParseInstancePosition(town, "GoldCourseFlag");
            var raceStartFlag = ParseInstancePosition(town, "RaceStartFlag");
            float poleDx = goldFlag.X - raceStartFlag.X;
            float poleDz = goldFlag.Z - raceStartFlag.Z;
            Check(MathF.Sqrt(poleDx * poleDx + poleDz * poleDz) > 6f,
                "GOLD LAP flag is still behind the race start pole");
            string podium = File.ReadAllText(Path.Combine(root, "scenes", "world",
                "assets", "landmarks", "RallyPodium.tscn"));
            Check(podium.Contains("HeadlineBoard") && podium.Contains("HeadlinePostWest") &&
                  podium.Contains("HeadlinePostEast"),
                "GRAND RALLY label is not attached to the podium");
            foreach (string car in new[] { "PipCarBroken.tscn", "PipCarFixed.tscn" })
            {
                string text = File.ReadAllText(Path.Combine(root, "scenes", "world",
                    "assets", "vehicles", car));
                Check(text.Contains("[node name=\"StatusAnchor\" type=\"Marker3D\" parent=\".\"]"),
                    $"{car} has no vehicle-relative status anchor");
                Check(text.Contains("[node name=\"Status\" type=\"Label3D\" parent=\"StatusAnchor\"]"),
                    $"{car} status label is not attached to its anchor");
            }
        });

        Run("panel-mounted world signs use two fixed faces", () =>
        {
            string root = FindRepoRoot();
            var scenes = new Dictionary<string, string[]>
            {
                ["DeliveryBoard.tscn"] = new[] { "Heading", "Addresses" },
                ["MarketStock.tscn"] = new[] { "Text" },
                ["PlaygroundFixSign.tscn"] = new[] { "Text" },
            };
            foreach (var (scene, fronts) in scenes)
            {
                string text = File.ReadAllText(Path.Combine(root, "scenes", "world",
                    "assets", "landmarks", scene));
                Check(!text.Contains("billboard = 1"),
                    $"{scene} still has a billboard label that can clip its panel");
                Check(Regex.Matches(text, "billboard = 0").Count >= 2,
                    $"{scene} needs a fixed label on both panel faces");
                foreach (string front in fronts)
                {
                    var frontRotation = ParseNodeRotation(text, front);
                    var backRotation = ParseNodeRotation(text, front + "_Back");
                    Check(MathF.Abs(MathF.Abs(frontRotation.Y) - MathF.PI) <= 0.01f,
                        $"{scene}/{front} does not face the front of its panel");
                    Check(MathF.Abs(backRotation.Y) <= 0.01f,
                        $"{scene}/{front}_Back does not face the back of its panel");
                }
            }
        });

        Run("bunting never casts rectangular road shadows", () =>
        {
            string root = FindRepoRoot();
            var expected = new Dictionary<string, int>
            {
                ["CelebrationDecor.tscn"] = 28,
                ["FestivalDecor.tscn"] = 8,
            };
            foreach (var (scene, minimum) in expected)
            {
                string text = File.ReadAllText(Path.Combine(root, "scenes", "world",
                    "assets", "landmarks", scene));
                int bunting = Regex.Matches(text, "\\[node name=\"Bunting").Count;
                int shadowOff = Regex.Matches(text,
                    "\\[node name=\"Bunting[^\"]*\" type=\"MeshInstance3D\" parent=\"\\.\"\\]\r?\ncast_shadow = 0").Count;
                Check(bunting >= minimum, $"{scene}: expected {minimum} bunting flags, found {bunting}");
                Equal(bunting, shadowOff, $"{scene} bunting shadows disabled");
            }
        });

    }
}
