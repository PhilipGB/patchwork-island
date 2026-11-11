using System;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Optional strongly validated lookup for the stable source positions in the
/// baked Town scene. This helper is additive and does NOT alter Main or saves.
/// </summary>
public static class WorldSceneAnchors
{
    public static Marker3D Require(Node3D town, string gameplayId)
    {
        if (town is null) throw new ArgumentNullException(nameof(town));
        if (string.IsNullOrWhiteSpace(gameplayId))
            throw new ArgumentException("A gameplay anchor ID is required.", nameof(gameplayId));
        var root = town.GetNodeOrNull<Node3D>("GameplayAnchors")
            ?? throw new InvalidOperationException("Town is missing GameplayAnchors.tscn");
        var marker = root.GetNodeOrNull<Marker3D>(gameplayId)
            ?? throw new InvalidOperationException($"Missing world anchor '{gameplayId}'");
        return marker;
    }

    public static Vector3 Position(Node3D town, string gameplayId) =>
        Require(town, gameplayId).GlobalPosition;
}
