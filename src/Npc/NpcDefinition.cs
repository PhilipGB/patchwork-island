using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

public readonly struct NpcPoint
{
    public readonly float X;
    public readonly float Z;

    public NpcPoint(float x, float z)
    {
        X = x;
        Z = z;
    }
}

/// <summary>Pure data for one named villager. Visuals and behaviour live in NpcActor.</summary>
public sealed class NpcDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Role { get; init; } = "";
    public string Intro { get; init; } = "";
    public string VillageFact { get; init; } = "";
    public string PortraitFile { get; init; } = "";
    public string MainHex { get; init; } = "#648abd";
    public string HairHex { get; init; } = "#664b39";
    public string AccentHex { get; init; } = "#e2b064";
    public bool LongHair { get; init; }
    public bool Dress { get; init; }
    public float WalkSpeed { get; init; } = 1.2f;
    public string GreetingCue { get; init; } = "npc_greet_01";
    public IReadOnlyList<string> MissionIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<NpcPoint> PatrolPoints { get; init; } = Array.Empty<NpcPoint>();

    public NpcPoint Home => PatrolPoints.Count > 0 ? PatrolPoints[0] : new NpcPoint(0f, 0f);
}

public static class NpcCatalog
{
    public static readonly IReadOnlyList<NpcDefinition> All = new List<NpcDefinition>
    {
        new()
        {
            Id = "milo",
            Name = "Milo",
            Role = "Post worker",
            Intro = "Hello! I sort the village post. Can you read the names on the parcels?",
            VillageFact = "Milo knows every letterbox on the island. He says short names are easiest to match.",
            PortraitFile = "ui/npc_portraits/milo.png",
            MainHex = "#cf674c",
            HairHex = "#664b39",
            AccentHex = "#82c2e8",
            LongHair = false,
            Dress = false,
            WalkSpeed = 1.5f,
            GreetingCue = "npc_greet_01",
            MissionIds = new[] { "M12", "M16" },
            PatrolPoints = new[]
            {
                new NpcPoint(-11.0f, 4.0f),
                new NpcPoint(-9.4f, 3.8f),
                new NpcPoint(-7.8f, 4.4f),
                new NpcPoint(-9.4f, 5.2f),
            },
        },
        new()
        {
            Id = "ada",
            Name = "Ada",
            Role = "Baker",
            Intro = "Welcome to the bakery! I have three orders today. Can you help me count them?",
            VillageFact = "Ada bakes bread and cakes. Two bread plus one cake makes a very happy customer.",
            PortraitFile = "ui/npc_portraits/ada.png",
            MainHex = "#d9a454",
            HairHex = "#744c2f",
            AccentHex = "#e9a4c3",
            LongHair = true,
            Dress = true,
            WalkSpeed = 1.1f,
            GreetingCue = "npc_greet_02",
            MissionIds = new[] { "M11", "M12", "M16", "M17" },
            PatrolPoints = new[]
            {
                new NpcPoint(12.6f, 4.5f),
                new NpcPoint(13.2f, 5.6f),
                new NpcPoint(12.4f, 6.4f),
                new NpcPoint(11.8f, 5.2f),
            },
        },
        new()
        {
            Id = "pip",
            Name = "Pip",
            Role = "Mechanic",
            Intro = "My little rally car needs a check-up. Tyres, lamp, battery — in the right order!",
            VillageFact = "Pip is the village mechanic. He says two tyres come before the lamp, and the battery goes in last.",
            PortraitFile = "ui/npc_portraits/pip.png",
            MainHex = "#a25a46",
            HairHex = "#2e383d",
            AccentHex = "#f6bd5c",
            LongHair = false,
            Dress = false,
            WalkSpeed = 1.2f,
            GreetingCue = "npc_greet_01",
            MissionIds = new[] { "M16", "M18", "M19" },
            PatrolPoints = new[]
            {
                new NpcPoint(28.8f, -8.6f),
                new NpcPoint(30.4f, -7.6f),
                new NpcPoint(29.8f, -6.0f),
                new NpcPoint(28.0f, -6.6f),
            },
        },
        new()
        {
            Id = "fern",
            Name = "Fern",
            Role = "Gardener",
            Intro = "Look at the garden! Red, blue, red, blue. Can you spot the pattern?",
            VillageFact = "Fern plants two colours in a repeating pattern. Six flower beds make three pairs.",
            PortraitFile = "ui/npc_portraits/fern.png",
            MainHex = "#8bb06f",
            HairHex = "#634a33",
            AccentHex = "#ac9ddc",
            LongHair = true,
            Dress = true,
            WalkSpeed = 1.0f,
            GreetingCue = "npc_greet_02",
            MissionIds = new[] { "M11", "M15" },
            PatrolPoints = new[]
            {
                new NpcPoint(8.9f, 15.2f),
                new NpcPoint(9.8f, 16.4f),
                new NpcPoint(9.0f, 17.6f),
                new NpcPoint(8.4f, 16.0f),
            },
        },
        new()
        {
            Id = "ollie",
            Name = "Ollie",
            Role = "Librarian",
            Intro = "Please help put these books back. Each cover has a word, and each shelf has a match.",
            VillageFact = "Ollie runs the little library by the town hall. SUN, MOON, TREE and STAR are his favourite shelves.",
            PortraitFile = "ui/npc_portraits/ollie.png",
            MainHex = "#648abd",
            HairHex = "#5b463a",
            AccentHex = "#e2b064",
            LongHair = false,
            Dress = false,
            WalkSpeed = 1.0f,
            GreetingCue = "npc_greet_01",
            MissionIds = new[] { "M11", "M13" },
            PatrolPoints = new[]
            {
                new NpcPoint(-18.4f, 8.4f),
                new NpcPoint(-17.6f, 7.2f),
                new NpcPoint(-17.8f, 6.2f),
                new NpcPoint(-18.6f, 7.4f),
            },
        },
        new()
        {
            Id = "ivy",
            Name = "Ivy",
            Role = "Festival organiser",
            Intro = "I am planning a village festival. Invitations, tables, then lanterns — a proper plan!",
            VillageFact = "Ivy makes lists for everything. Invite the guests first, then set the tables, then hang the lanterns.",
            PortraitFile = "ui/npc_portraits/ivy.png",
            MainHex = "#a76b9a",
            HairHex = "#443f49",
            AccentHex = "#95cfae",
            LongHair = true,
            Dress = true,
            WalkSpeed = 1.15f,
            GreetingCue = "npc_greet_02",
            MissionIds = new[] { "M16", "M20" },
            PatrolPoints = new[]
            {
                new NpcPoint(19.2f, 12.4f),
                new NpcPoint(20.6f, 13.0f),
                new NpcPoint(20.0f, 14.0f),
                new NpcPoint(18.6f, 13.2f),
            },
        },
    };

    public static NpcDefinition Find(string id) =>
        All.FirstOrDefault(npc => string.Equals(npc.Id, id, StringComparison.OrdinalIgnoreCase));

    public static bool Validate(out string error)
    {
        error = "";
        if (All.Count != 6)
        {
            error = $"Expected six named villagers, found {All.Count}.";
            return false;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var npc in All)
        {
            if (string.IsNullOrWhiteSpace(npc.Id) || !ids.Add(npc.Id))
            {
                error = "NPC ids must be present and unique.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(npc.Name) || !names.Add(npc.Name))
            {
                error = "NPC names must be present and unique.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(npc.Role) || string.IsNullOrWhiteSpace(npc.Intro))
            {
                error = $"NPC '{npc.Id}' is missing a role or greeting.";
                return false;
            }
            if (!npc.PortraitFile.StartsWith("ui/npc_portraits/", StringComparison.Ordinal))
            {
                error = $"NPC '{npc.Id}' has an unexpected portrait path.";
                return false;
            }
            if (npc.PatrolPoints.Count < 2)
            {
                error = $"NPC '{npc.Id}' needs at least two patrol points.";
                return false;
            }
            if (!MissionCatalogIdsAreKnown(npc, out error))
                return false;
            foreach (var point in npc.PatrolPoints)
            {
                if (point.X < -52f || point.X > 92f || point.Z < -50f || point.Z > 50f)
                {
                    error = $"NPC '{npc.Id}' patrol point is outside the island.";
                    return false;
                }
                if (point.X > 40.5f && point.X < 49.5f)
                {
                    error = $"NPC '{npc.Id}' patrol point is in the river.";
                    return false;
                }
                foreach (var building in WorldAnchors.Buildings)
                {
                    if (MathF.Abs(point.X - building.X) < building.HalfX &&
                        MathF.Abs(point.Z - building.Z) < building.HalfZ)
                    {
                        error = $"NPC '{npc.Id}' patrol point is inside a building.";
                        return false;
                    }
                }
                if (WorldAnchors.DistanceToNearestStation(point.X, point.Z) < 3.35f)
                {
                    error = $"NPC '{npc.Id}' patrol point competes with a job station.";
                    return false;
                }
                if (MathF.Sqrt((point.X - npc.Home.X) * (point.X - npc.Home.X) +
                               (point.Z - npc.Home.Z) * (point.Z - npc.Home.Z)) > 8f)
                {
                    error = $"NPC '{npc.Id}' patrols too far from home.";
                    return false;
                }
            }
        }
        return true;
    }

    private static bool MissionCatalogIdsAreKnown(NpcDefinition npc, out string error)
    {
        error = "";
        foreach (string id in npc.MissionIds)
        {
            if (id.Length != 3 || id[0] != 'M' || !char.IsDigit(id[1]) || !char.IsDigit(id[2]))
            {
                error = $"NPC '{npc.Id}' references malformed mission '{id}'.";
                return false;
            }
        }
        return true;
    }
}
