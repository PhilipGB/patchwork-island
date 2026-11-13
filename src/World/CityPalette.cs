using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PatchworkIsland;

public static class CityPalette
{
    public static readonly Color[] Cars =
    {
        Palette.Red, Palette.Blue, Palette.Teal, Palette.Yellow,
        Palette.Lavender, Palette.Orange,
    };

    public static readonly Color[] Pedestrians =
    {
        Palette.Blue, Palette.Orange, Palette.Teal, Palette.Lavender,
        Palette.Red, Palette.Grass, Palette.Pink, Palette.Cream,
    };

    public static Color CarColour(int index) => Cars[Math.Abs(index) % Cars.Length];
    public static Color PedColour(int index) => Pedestrians[Math.Abs(index) % Pedestrians.Length];
}
