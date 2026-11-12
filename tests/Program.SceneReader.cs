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
    private static (float X, float Y, float Z) ParseInstancePosition(
        string sceneText, string name)
    {
        string pattern = "\\[node name=\"" + Regex.Escape(name) + "\" parent=\"\\.\"" +
                         "[^\\]]*\\](?<body>.*?)(?=\\n\\[node |\\z)";
        var match = Regex.Match(sceneText, pattern, RegexOptions.Singleline);
        if (!match.Success) throw new Exception($"Instance {name} missing");
        return ParseVector(match.Groups["body"].Value);
    }

    private static string ParseNodeBody(string sceneText, string name)
    {
        string pattern = "\\[node name=\"" + Regex.Escape(name) + "\"[^\\]]*\\]" +
                         "(?<body>.*?)(?=\\n\\[node |\\z)";
        var match = Regex.Match(sceneText, pattern, RegexOptions.Singleline);
        if (!match.Success) throw new Exception($"Node {name} missing");
        return match.Groups["body"].Value;
    }

    private static (float X, float Y, float Z) ParseNodePosition(string sceneText, string name)
    {
        return ParseVector(ParseNodeBody(sceneText, name));
    }

    private static string ParseNodeText(string sceneText, string name)
    {
        var match = Regex.Match(ParseNodeBody(sceneText, name), "text = \"([^\"]*)\"");
        if (!match.Success) throw new Exception($"Node {name} has no text");
        return match.Groups[1].Value;
    }

    private static (float X, float Y, float Z) ParseNodeRotation(string sceneText, string name)
    {
        var match = Regex.Match(ParseNodeBody(sceneText, name),
            "rotation = Vector3\\(([^)]+)\\)");
        if (!match.Success) return (0f, 0f, 0f);
        float[] values = match.Groups[1].Value.Split(',')
            .Select(part => float.Parse(part.Trim(), CultureInfo.InvariantCulture))
            .ToArray();
        return (values[0], values[1], values[2]);
    }

    private static List<(string Name, float X, float Z, float HalfX, float HalfZ)> ParseRoadSlabs(
        string path)
    {
        string text = File.ReadAllText(path);
        var meshes = ParseBoxSizes(text, "BoxMesh");
        const string pattern = "\\[node name=\"(?<name>RoadSlab[^\"]*)\" " +
                               "type=\"MeshInstance3D\" parent=\"\\.\"\\]" +
                               "(?<body>.*?)(?=\\n\\[node |\\z)";
        var result = new List<(string, float, float, float, float)>();
        foreach (Match match in Regex.Matches(text, pattern, RegexOptions.Singleline))
        {
            string name = match.Groups["name"].Value;
            var (x, _, z) = ParseVector(match.Groups["body"].Value);
            var reference = Regex.Match(match.Groups["body"].Value,
                "mesh = SubResource\\(\"(?<id>[^\"]+)\"\\)");
            if (!reference.Success || !meshes.TryGetValue(reference.Groups["id"].Value, out var size))
                throw new Exception($"Road slab {name} has no BoxMesh");
            result.Add((name, x, z, size.X / 2f, size.Z / 2f));
        }
        return result;
    }

    private static List<(string Name, float X, float Z, float HalfX, float HalfZ)> ParseRoadColliders(
        string path)
    {
        string text = File.ReadAllText(path);
        var shapes = ParseBoxSizes(text, "BoxShape3D");
        const string pattern = "\\[node name=\"(?<name>Collision_[^\"]*)\" " +
                               "type=\"CollisionShape3D\" parent=\"TownRoadCollision\"\\]" +
                               "(?<body>.*?)(?=\\n\\[node |\\z)";
        var result = new List<(string, float, float, float, float)>();
        foreach (Match match in Regex.Matches(text, pattern, RegexOptions.Singleline))
        {
            string name = match.Groups["name"].Value;
            var (x, _, z) = ParseVector(match.Groups["body"].Value);
            var reference = Regex.Match(match.Groups["body"].Value,
                "shape = SubResource\\(\"(?<id>[^\"]+)\"\\)");
            if (!reference.Success || !shapes.TryGetValue(reference.Groups["id"].Value, out var size))
                throw new Exception($"Road collider {name} has no BoxShape3D");
            result.Add((name, x, z, size.X / 2f, size.Z / 2f));
        }
        return result;
    }

    private static Dictionary<string, (float X, float Y, float Z)> ParseBoxSizes(
        string sceneText, string type)
    {
        var result = new Dictionary<string, (float, float, float)>(StringComparer.Ordinal);
        string pattern = "\\[sub_resource type=\"" + type + "\" id=\"(?<id>[^\"]+)\"\\]" +
                         "(?<body>.*?)(?=\\n\\[|\\z)";
        foreach (Match match in Regex.Matches(sceneText, pattern, RegexOptions.Singleline))
        {
            var size = Regex.Match(match.Groups["body"].Value,
                "size = Vector3\\(([^)]+)\\)");
            if (!size.Success) continue;
            float[] values = size.Groups[1].Value.Split(',')
                .Select(part => float.Parse(part.Trim(), CultureInfo.InvariantCulture))
                .ToArray();
            result[match.Groups["id"].Value] = (values[0], values[1], values[2]);
        }
        return result;
    }

    private static bool SameRoadRect(
        (string Name, float X, float Z, float HalfX, float HalfZ) a,
        (string Name, float X, float Z, float HalfX, float HalfZ) b)
    {
        return MathF.Abs(a.X - b.X) <= 0.01f &&
               MathF.Abs(a.Z - b.Z) <= 0.01f &&
               MathF.Abs(a.HalfX - b.HalfX) <= 0.01f &&
               MathF.Abs(a.HalfZ - b.HalfZ) <= 0.01f;
    }

    private static string Describe(
        (string Name, float X, float Z, float HalfX, float HalfZ) rect)
    {
        return $"({rect.X:0.###}, {rect.Z:0.###}) " +
               $"{rect.HalfX * 2f:0.###}x{rect.HalfZ * 2f:0.###}";
    }

    private static Dictionary<string, (float X, float Y, float Z)> ParseMarkers(
        string path, string parent)
    {
        string text = File.ReadAllText(path);
        string pattern = "\\[node name=\"(?<name>[^\"]+)\" type=\"Marker3D\" parent=\"" +
                         Regex.Escape(parent) + "\"\\](?<body>.*?)(?=\\n\\[node |\\z)";
        var result = new Dictionary<string, (float, float, float)>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(text, pattern, RegexOptions.Singleline))
        {
            result[match.Groups["name"].Value] = ParseVector(match.Groups["body"].Value);
        }
        return result;
    }

    private static Dictionary<string, List<(float X, float Y, float Z)>> ParseMarkersByParent(
        string path)
    {
        string text = File.ReadAllText(path);
        string pattern = "\\[node name=\"Waypoint_[0-9]+\" type=\"Marker3D\" " +
                         "parent=\"(?<parent>[^\"]+)\"\\](?<body>.*?)(?=\\n\\[node |\\z)";
        var result = new Dictionary<string, List<(float, float, float)>>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(text, pattern, RegexOptions.Singleline))
        {
            string parent = match.Groups["parent"].Value;
            if (!result.TryGetValue(parent, out var list))
            {
                list = new List<(float, float, float)>();
                result[parent] = list;
            }
            list.Add(ParseVector(match.Groups["body"].Value));
        }
        return result;
    }

    private static (float X, float Y, float Z) ParseVector(string text)
    {
        var match = Regex.Match(text, "position = Vector3\\(([^)]+)\\)");
        if (!match.Success) throw new Exception("Vector3 position missing");
        float[] values = match.Groups[1].Value.Split(',')
            .Select(part => float.Parse(part.Trim(), CultureInfo.InvariantCulture))
            .ToArray();
        return (values[0], values[1], values[2]);
    }

    private static (float X, float Y, float Z) ParseWallSize(string sceneText, string wallName)
    {
        var boxMeshes = new Dictionary<string, (float, float, float)>(StringComparer.Ordinal);
        string boxPattern = "\\[sub_resource type=\"BoxMesh\" id=\"(?<id>[^\"]+)\"\\]" +
                            "(?<body>.*?)(?=\\n\\[sub_resource |\\n\\[node |\\z)";
        foreach (Match match in Regex.Matches(sceneText, boxPattern, RegexOptions.Singleline))
        {
            var size = Regex.Match(match.Groups["body"].Value, "size = Vector3\\(([^)]+)\\)");
            if (!size.Success) continue;
            float[] values = size.Groups[1].Value.Split(',')
                .Select(part => float.Parse(part.Trim(), CultureInfo.InvariantCulture))
                .ToArray();
            boxMeshes[match.Groups["id"].Value] = (values[0], values[1], values[2]);
        }

        string directPattern = "\\[node name=\"" + Regex.Escape(wallName) +
                               "\" type=\"MeshInstance3D\"[^\\]]*\\]" +
                               "(?<body>.*?)(?=\\n\\[node |\\z)";
        var direct = Regex.Match(sceneText, directPattern, RegexOptions.Singleline);
        string meshBody = direct.Success ? direct.Groups["body"].Value : null;
        if (meshBody == null)
        {
            string meshPattern = "\\[node name=\"[^\"]+\" type=\"MeshInstance3D\" parent=\"" +
                                 Regex.Escape(wallName) + "\"[^\\]]*\\](?<body>.*?)(?=\\n\\[node |\\z)";
            var meshNode = Regex.Match(sceneText, meshPattern, RegexOptions.Singleline);
            if (!meshNode.Success) throw new Exception($"No mesh under {wallName}");
            meshBody = meshNode.Groups["body"].Value;
        }
        var reference = Regex.Match(meshBody,
            "mesh = SubResource\\(\"(?<id>[^\"]+)\"\\)");
        if (!reference.Success) throw new Exception($"No BoxMesh on {wallName}");
        return boxMeshes[reference.Groups["id"].Value];
    }

    private static float Distance((float X, float Y, float Z) a, (float X, float Y, float Z) b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static bool AllTwentyComplete(GameData data)
    {
        for (int i = 1; i <= 20; i++)
            if (!data.IsCompleted($"M{i:00}")) return false;
        return true;
    }

    private static List<(string Arch, float X, float Z)> ParseCityBuildingPositions(string text)
    {
        var result = new List<(string, float, float)>();
        string pattern = "\\[node name=\"Building_[0-9]+_(?<arch>[^\"]+)\" parent=\"\\.\" instance=" +
                         "[^\\]]*\\]\\r?\\n(?<body>.*?)(?=\\r?\\n\\[|\\z)";
        foreach (Match match in Regex.Matches(text, pattern, RegexOptions.Singleline))
        {
            var position = Regex.Match(match.Groups["body"].Value,
                "position = Vector3\\(([^)]+)\\)");
            if (!position.Success) continue;
            float[] values = position.Groups[1].Value.Split(',')
                .Select(part => float.Parse(part.Trim(), CultureInfo.InvariantCulture))
                .ToArray();
            result.Add((match.Groups["arch"].Value, values[0], values[2]));
        }
        return result;
    }

}
