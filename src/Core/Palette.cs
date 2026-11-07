using Godot;

namespace PatchworkIsland;

/// <summary>Shared art-direction palette. Written-spec colours win over concept boards.</summary>
public static class Palette
{
    public static readonly Color Sky = Hex("75C9FF");
    public static readonly Color Grass = Hex("86CF55");
    public static readonly Color GrassDark = Hex("369B47");
    public static readonly Color Road = Hex("6A7180");
    public static readonly Color RoadMark = Hex("FFF3D6");
    public static readonly Color Water = Hex("24B5E4");
    public static readonly Color Yellow = Hex("FFD23F");
    public static readonly Color Red = Hex("F45A54");
    public static readonly Color Blue = Hex("2674EC");
    public static readonly Color Lavender = Hex("946FE8");
    public static readonly Color Wood = Hex("B98550");
    public static readonly Color WoodDark = Hex("88582F");
    public static readonly Color Stone = Hex("9CA3AD");
    public static readonly Color StoneDark = Hex("6F7885");
    public static readonly Color Cream = Hex("FFF8EC");
    public static readonly Color Navy = Hex("27364B");
    public static readonly Color Success = Hex("257B47");
    public static readonly Color Orange = Hex("F48B43");
    public static readonly Color Skin = Hex("F5C99B");
    public static readonly Color White = Hex("FFFFFF");
    public static readonly Color Sand = Hex("EFD9A6");
    public static readonly Color Pink = Hex("F49ABD");
    public static readonly Color Teal = Hex("2FAE9B");

    public static Color Hex(string hex) => Color.FromHtml(hex);
}
