using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>Loads the six supplied PNG portraits once and caches them.</summary>
public static class NpcPortrait
{
    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);

    public static Texture2D Load(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId)) return null;
        if (Cache.TryGetValue(npcId, out var cached)) return cached;
        string path = $"res://assets/ui/npc_portraits/{npcId}.png";
        Texture2D texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        if (texture == null)
            GD.PushError($"NPC portrait is missing: {path}");
        Cache[npcId] = texture;
        return texture;
    }

    public static Texture2D LoadDefinition(NpcDefinition definition) =>
        definition == null ? null : Load(definition.Id);
}
