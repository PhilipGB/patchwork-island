using System;
using System.Collections.Generic;

namespace PatchworkIsland;

/// <summary>One child-friendly outfit in the village clothes shop.</summary>
public sealed class OutfitDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int Price { get; init; }
    public string MainHex { get; init; } = "FFFFFF";
    public string SecondaryHex { get; init; } = "FFFFFF";
    public string AccentHex { get; init; } = "FFFFFF";
    public string ShoeHex { get; init; } = "27364B";

    public bool IsStarting => Price == 0;
}

/// <summary>The colours the player model and the UI paper-doll use together.</summary>
public sealed class CharacterStyle
{
    public string MainHex { get; init; } = "FFFFFF";
    public string SecondaryHex { get; init; } = "FFFFFF";
    public string AccentHex { get; init; } = "FFFFFF";
    public string ShoeHex { get; init; } = "27364B";
}

/// <summary>
/// Pure character-preset and outfit rules. Kept free of Godot so purchases,
/// ownership and save defaults can be tested by the normal .NET runner.
/// The default outfit is always owned; only purchased outfits are stored.
/// </summary>
public static class CharacterCustomisation
{
    public const string Boy = "boy";
    public const string Girl = "girl";
    public const string DefaultPreset = Boy;
    public const string DefaultOutfit = "default";

    public static readonly string[] Presets = { Boy, Girl };

    // Four purchasable outfits plus the free starting outfit. Shared by both
    // presets so the shop stays small and every purchase is useful.
    private static readonly OutfitDefinition[] Catalogue =
    {
        new()
        {
            Id = DefaultOutfit,
            Name = "Everyday",
            Description = "The comfy outfit you start with.",
            Price = 0,
            MainHex = "2674EC",
            SecondaryHex = "27364B",
            AccentHex = "FFD23F",
            ShoeHex = "2674EC",
        },
        new()
        {
            Id = "sunny",
            Name = "Sunny Day",
            Description = "Warm yellow with a bright orange trim.",
            Price = 8,
            MainHex = "FFD23F",
            SecondaryHex = "F48B43",
            AccentHex = "FFF8EC",
            ShoeHex = "2674EC",
        },
        new()
        {
            Id = "meadow",
            Name = "Meadow Green",
            Description = "Fresh green, like the village green.",
            Price = 10,
            MainHex = "369B47",
            SecondaryHex = "86CF55",
            AccentHex = "FFF8EC",
            ShoeHex = "88582F",
        },
        new()
        {
            Id = "berry",
            Name = "Berry Pink",
            Description = "Pink and lavender with rosy shoes.",
            Price = 12,
            MainHex = "F49ABD",
            SecondaryHex = "946FE8",
            AccentHex = "FFF8EC",
            ShoeHex = "F45A54",
        },
        new()
        {
            Id = "starry",
            Name = "Starry Night",
            Description = "Deep navy with a golden sparkle.",
            Price = 15,
            MainHex = "27364B",
            SecondaryHex = "946FE8",
            AccentHex = "FFD23F",
            ShoeHex = "FFD23F",
        },
    };

    public static IReadOnlyList<OutfitDefinition> Outfits => Catalogue;

    public static bool IsValidPreset(string preset) =>
        preset == Boy || preset == Girl;

    public static string NormalisePreset(string preset) =>
        string.IsNullOrWhiteSpace(preset) ? DefaultPreset : preset;

    public static string NormaliseOutfit(string outfitId) =>
        string.IsNullOrWhiteSpace(outfitId) ? DefaultOutfit : outfitId;

    public static string PresetLabel(string preset) =>
        preset == Girl ? "Girl" : "Boy";

    public static OutfitDefinition Find(string outfitId)
    {
        if (string.IsNullOrWhiteSpace(outfitId)) return null;
        foreach (var outfit in Catalogue)
            if (outfit.Id == outfitId)
                return outfit;
        return null;
    }

    public static bool Owns(GameData data, string outfitId)
    {
        if (outfitId == DefaultOutfit) return true;
        if (data?.OwnedOutfits == null) return false;
        return data.OwnedOutfits.Contains(outfitId);
    }

    public static OutfitDefinition Equipped(GameData data) =>
        Find(data?.EquippedOutfit) ?? Find(DefaultOutfit);

    public static CharacterStyle StyleFor(string preset, string outfitId)
    {
        var outfit = Find(outfitId) ?? Find(DefaultOutfit);
        if (outfit.Id == DefaultOutfit)
        {
            // The starting outfit is the one place where the boy and girl
            // silhouettes get their own friendly signature colours.
            return preset == Girl
                ? new CharacterStyle
                {
                    MainHex = "F49ABD",
                    SecondaryHex = "946FE8",
                    AccentHex = "FFD23F",
                    ShoeHex = "F45A54",
                }
                : new CharacterStyle
                {
                    MainHex = "2674EC",
                    SecondaryHex = "27364B",
                    AccentHex = "FFD23F",
                    ShoeHex = "2674EC",
                };
        }

        return new CharacterStyle
        {
            MainHex = outfit.MainHex,
            SecondaryHex = outfit.SecondaryHex,
            AccentHex = outfit.AccentHex,
            ShoeHex = outfit.ShoeHex,
        };
    }

    public static ActionOutcome SetPreset(GameData data, string preset)
    {
        if (data == null)
            return Failure("No character data is available.");
        if (!IsValidPreset(preset))
            return Failure("Choose Boy or Girl.");

        data.CharacterPreset = preset;
        return Success($"Now playing as {PresetLabel(preset)}.");
    }

    public static ActionOutcome BuyOutfit(GameData data, string outfitId)
    {
        if (data == null)
            return Failure("No character data is available.");

        var outfit = Find(outfitId);
        if (outfit == null)
            return Failure("That outfit is not in the shop.");

        data.OwnedOutfits ??= new List<string>();
        if (outfit.Id == DefaultOutfit || Owns(data, outfit.Id))
            return Failure($"{outfit.Name} is already in your wardrobe.");

        if (data.Coins < outfit.Price)
            return Failure(
                $"Not enough coins. {outfit.Name} costs {outfit.Price}; you have {data.Coins}. " +
                "Finish jobs to earn more.");

        data.Coins -= outfit.Price;
        data.OwnedOutfits.Add(outfit.Id);
        // Buying and wearing happen together, so a child gets immediate,
        // visible feedback from one clear action.
        data.EquippedOutfit = outfit.Id;
        return Success($"Bought {outfit.Name} for {outfit.Price} coins! It is now worn.");
    }

    public static ActionOutcome EquipOutfit(GameData data, string outfitId)
    {
        if (data == null)
            return Failure("No character data is available.");

        var outfit = Find(outfitId);
        if (outfit == null)
            return Failure("That outfit is not in the shop.");
        if (!Owns(data, outfit.Id))
            return Failure($"You do not own {outfit.Name} yet. Buy it first.");

        data.EquippedOutfit = outfit.Id;
        return Success($"Wearing {outfit.Name}.");
    }

    private static ActionOutcome Failure(string message) =>
        new() { Success = false, Message = message };

    private static ActionOutcome Success(string message) =>
        new() { Success = true, Message = message };
}
