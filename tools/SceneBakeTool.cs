using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// One-shot scene bake for the unmodified v1.2.1 procedural WorldBuilder.
/// Invoke the standalone SceneBakeTool.tscn scene from the target project.
/// The current game entrypoint is deliberately NOT modified by this tool.
///
/// Outputs res://scenes/world/baked/Town_Baked.tscn and reusable scenes in
/// res://scenes/world/baked/parts/. This preserves the source geometry,
/// static bodies, transforms, labels, world state-dependent *visual roots*,
/// and the exact seeded procedural foliage. It is not a runtime replacement
/// for WorldBuilder until the gameplay-facing WorldSceneAdapter is implemented.
/// </summary>
public partial class SceneBakeTool : Node
{
    private const string OutDir = "res://scenes/world/baked";
    private const string PartDir = OutDir + "/parts";

    private static readonly HashSet<string> SplitRoots = new(StringComparer.Ordinal)
    {
        "PlayerHouse", "TownHall", "RedHouse", "Market", "ClothesShop", "Workshop",
        "JobBoard", "DirectionPost", "BakeryStall", "LibraryShelf", "DeliveryBoard",
        "MarketStock", "PipCarBroken", "PipCarFixed", "RallyPodium",
        "RallyRoad", "RaceGates4Root", "RaceGates6Root", "BridgeBarrier",
        "CelebrationDecor", "FestivalDecor", "RaceStartFlag", "GoldCourseFlag",
        "PlaygroundFixSign",
    };

    public override void _Ready()
    {
        try
        {
            Bake();
            GD.Print("[SCENE BAKE] OK: Town_Baked.tscn and reusable landmark parts written.");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError("[SCENE BAKE] FAILED: " + ex);
            GetTree().Quit(1);
        }
    }

    private void Bake()
    {
        EnsureDirectory(OutDir);
        EnsureDirectory(PartDir);

        var legacy = new WorldBuilder { Name = "TemporaryLegacySource" };
        AddChild(legacy);
        legacy.Build();

        var town = new Node3D { Name = "Town_Baked" };
        AddChild(town);

        // Preserve layout transforms while moving all authored visuals from
        // the procedural root into a normal, serialisable packed-scene root.
        // Extract named reusable objects into their own .tscn first.
        int reusableCount = 0;
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Node child in legacy.GetChildren().ToArray())
        {
            legacy.RemoveChild(child);
            if (child is Node3D worldPart && ShouldExtract(child.Name.ToString()))
            {
                string name = child.Name.ToString();
                seen.TryGetValue(name, out int serial);
                seen[name] = serial + 1;
                string fileName = serial == 0 ? name : name + "_" + serial;
                string path = PartDir + "/" + fileName + ".tscn";

                // Reusable scene origin is local (0,0,0); original placement is
                // restored on the instance in Town_Baked.tscn.
                Transform3D original = worldPart.Transform;
                worldPart.Transform = Transform3D.Identity;
                PackAndSave(worldPart, path);
                worldPart.Free();

                PackedScene packed = ResourceLoader.Load<PackedScene>(path)
                    ?? throw new InvalidDataException("Cannot reload " + path);
                var instance = packed.Instantiate<Node3D>();
                instance.Name = name;
                instance.Transform = original;
                town.AddChild(instance);
                reusableCount++;
            }
            else
            {
                // Keep direct Geo primitives (signs, garden pads, roads,
                // river, planted decor, etc) in the town snapshot.
                town.AddChild(child);
            }
        }

        // Add the editor-visible, stable gameplay marker groups to the exact
        // static snapshot. These locations are independent of authored mesh
        // geometry and remain available to the future scene-world adapter.
        AddMarkerGroup(town, "res://scenes/world/GameplayAnchors.tscn", "GameplayAnchors");
        AddMarkerGroup(town, "res://scenes/world/NpcPatrolMarkers.tscn", "NpcPatrolMarkers");
        AddMarkerGroup(town, "res://scenes/world/TrafficRouteMarkers.tscn", "TrafficRouteMarkers");

        PackAndSave(town, OutDir + "/Town_Baked.tscn");
        GD.Print($"[SCENE BAKE] {reusableCount} reusable roots saved; " +
                 $"{town.GetChildCount()} town roots; version source: 1.2.1.");

        // Only the bake tool's temporary scene owns these nodes; no saved data
        // or running user game is changed.
        town.Free();
        legacy.Free();
    }

    private static void AddMarkerGroup(Node3D town, string path, string name)
    {
        PackedScene packed = ResourceLoader.Load<PackedScene>(path)
            ?? throw new InvalidDataException("Missing marker scene: " + path);
        var instance = packed.Instantiate<Node3D>();
        instance.Name = name;
        town.AddChild(instance);
    }

    private static bool ShouldExtract(string name) =>
        SplitRoots.Contains(name) || name == "SwingFrame" ||
        name.StartsWith("SignSocket_", StringComparison.Ordinal) ||
        name.StartsWith("FestivalTable_", StringComparison.Ordinal) ||
        name.StartsWith("FestivalLantern_", StringComparison.Ordinal);

    private static void PackAndSave(Node root, string path)
    {
        AssignOwners(root, root);
        var packed = new PackedScene();
        Error packResult = packed.Pack(root);
        if (packResult != Error.Ok)
            throw new InvalidDataException($"Pack failed ({packResult}) for {path}");
        Error result = ResourceSaver.Save(packed, path);
        if (result != Error.Ok)
            throw new IOException($"Save failed ({result}) for {path}");
    }

    private static void AssignOwners(Node node, Node packedRoot)
    {
        foreach (Node child in node.GetChildren())
        {
            // An instanced PackedScene owns its own internal nodes. Assign
            // only the instance root to the outer scene, preserving reusable
            // scene references and avoiding accidental flattening.
            child.Owner = packedRoot;
            if (string.IsNullOrEmpty(child.SceneFilePath))
                AssignOwners(child, packedRoot);
        }
    }

    private static void EnsureDirectory(string path)
    {
        string absolute = ProjectSettings.GlobalizePath(path);
        Error result = DirAccess.MakeDirRecursiveAbsolute(absolute);
        if (result != Error.Ok)
            throw new IOException($"Cannot create directory {path}: {result}");
    }
}
