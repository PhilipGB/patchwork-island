using Godot;

namespace PatchworkIsland;

/// <summary>
/// Small code-drawn paper doll used by the character chooser and clothes shop.
/// It mirrors the in-world silhouette (dress versus shirt/shorts, long versus
/// short hair) and updates its colours from the same pure outfit catalogue.
/// </summary>
public partial class CharacterDoll : Control
{
    private string _preset = CharacterCustomisation.DefaultPreset;
    private string _outfitId = CharacterCustomisation.DefaultOutfit;
    private bool _capRed;
    private bool _built;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(190, 240);
        if (!_built) Rebuild();
    }

    public void SetAppearance(string preset, string outfitId, bool capRed)
    {
        string normalisedPreset = CharacterCustomisation.IsValidPreset(preset)
            ? preset
            : CharacterCustomisation.DefaultPreset;
        string normalisedOutfit = CharacterCustomisation.Find(outfitId) != null
            ? outfitId
            : CharacterCustomisation.DefaultOutfit;

        if (_built && normalisedPreset == _preset && normalisedOutfit == _outfitId && capRed == _capRed)
            return;

        _preset = normalisedPreset;
        _outfitId = normalisedOutfit;
        _capRed = capRed;
        if (IsInsideTree()) Rebuild();
    }

    private void Rebuild()
    {
        _built = true;
        UiKit.ClearChildren(this);

        var style = CharacterCustomisation.StyleFor(_preset, _outfitId);
        Color main = Color.FromHtml(style.MainHex);
        Color secondary = Color.FromHtml(style.SecondaryHex);
        Color accent = Color.FromHtml(style.AccentHex);
        Color shoes = Color.FromHtml(style.ShoeHex);
        Color skin = Palette.Skin;
        Color hair = Color.FromHtml("6E462B");
        Color darkHair = hair.Darkened(0.14f);
        bool girl = _preset == CharacterCustomisation.Girl;

        // Soft ground shadow.
        Rect(new Vector2(58, 228), new Vector2(74, 8), new Color(0.15f, 0.2f, 0.24f, 0.18f));

        if (girl)
        {
            // Long hair sits behind the dress and shoulders.
            Rect(new Vector2(58, 30), new Vector2(74, 116), hair);
            Rect(new Vector2(56, 76), new Vector2(10, 72), darkHair);
            Rect(new Vector2(124, 76), new Vector2(10, 72), darkHair);
        }

        // Legs and shoes.
        float legTop = girl ? 176 : 166;
        Rect(new Vector2(76, legTop), new Vector2(14, 42), skin);
        Rect(new Vector2(100, legTop), new Vector2(14, 42), skin);
        Rect(new Vector2(70, 216), new Vector2(25, 12), shoes);
        Rect(new Vector2(95, 216), new Vector2(25, 12), shoes);
        Rect(new Vector2(70, 226), new Vector2(25, 4), shoes.Darkened(0.2f));
        Rect(new Vector2(95, 226), new Vector2(25, 4), shoes.Darkened(0.2f));

        if (girl)
        {
            Dress(main, secondary, accent);
        }
        else
        {
            // Shirt, accent collar, shorts and pocket.
            Rect(new Vector2(61, 88), new Vector2(68, 60), main);
            Rect(new Vector2(61, 88), new Vector2(68, 9), accent);
            Rect(new Vector2(63, 146), new Vector2(64, 26), secondary);
            Rect(new Vector2(104, 104), new Vector2(16, 14), accent);
            Rect(new Vector2(63, 170), new Vector2(64, 5), accent);
        }

        // Arms and hands.
        Rect(new Vector2(45, 92), new Vector2(15, 52), main);
        Rect(new Vector2(130, 92), new Vector2(15, 52), main);
        Rect(new Vector2(45, 142), new Vector2(15, 13), skin);
        Rect(new Vector2(130, 142), new Vector2(15, 13), skin);

        // Neck and friendly face.
        Rect(new Vector2(86, 74), new Vector2(14, 14), skin);
        Rect(new Vector2(62, 28), new Vector2(62, 52), skin);
        Rect(new Vector2(77, 48), new Vector2(6, 9), Palette.Navy);
        Rect(new Vector2(103, 48), new Vector2(6, 9), Palette.Navy);
        Rect(new Vector2(87, 63), new Vector2(12, 3), Palette.Navy);
        Rect(new Vector2(69, 57), new Vector2(7, 4), new Color("E99A86"));
        Rect(new Vector2(110, 57), new Vector2(7, 4), new Color("E99A86"));

        // Hair on top of the face.
        if (girl)
        {
            Rect(new Vector2(59, 22), new Vector2(68, 15), hair);
            Rect(new Vector2(62, 33), new Vector2(62, 7), hair);
            Rect(new Vector2(57, 34), new Vector2(8, 58), hair);
            Rect(new Vector2(121, 34), new Vector2(8, 58), hair);
            Bow(new Vector2(124, 31), accent);
        }
        else
        {
            Rect(new Vector2(62, 21), new Vector2(62, 14), hair);
            Rect(new Vector2(60, 31), new Vector2(66, 11), darkHair);
            Rect(new Vector2(66, 32), new Vector2(54, 6), hair);
        }

        // Blue cap, or the red cap unlocked by the first job.
        Color cap = _capRed ? Palette.Red : Palette.Blue;
        Rect(new Vector2(68, 15), new Vector2(50, 10), cap);
        Rect(new Vector2(65, 24), new Vector2(34, 6), cap.Darkened(0.08f));
        Rect(new Vector2(110, 16), new Vector2(10, 7), Palette.Cream);
    }

    private void Dress(Color main, Color secondary, Color accent)
    {
        var skirt = new Polygon2D
        {
            Color = main,
            Polygon = new[]
            {
                new Vector2(65, 127),
                new Vector2(125, 127),
                new Vector2(140, 178),
                new Vector2(50, 178),
            },
        };
        AddChild(skirt);

        Rect(new Vector2(68, 86), new Vector2(54, 46), main);
        Rect(new Vector2(68, 86), new Vector2(54, 8), accent);
        Rect(new Vector2(61, 121), new Vector2(68, 8), secondary);
        Rect(new Vector2(51, 169), new Vector2(88, 7), accent);
        Rect(new Vector2(88, 98), new Vector2(14, 10), accent);
    }

    private void Bow(Vector2 position, Color color)
    {
        AddChild(new Polygon2D
        {
            Color = color,
            Position = position,
            Polygon = new[]
            {
                Vector2.Zero,
                new Vector2(-13, -7),
                new Vector2(-13, 7),
            },
        });
        AddChild(new Polygon2D
        {
            Color = color,
            Position = position,
            Polygon = new[]
            {
                Vector2.Zero,
                new Vector2(13, -7),
                new Vector2(13, 7),
            },
        });
        Rect(position - new Vector2(4, 4), new Vector2(8, 8), color.Darkened(0.18f));
    }

    private void Rect(Vector2 position, Vector2 size, Color color)
    {
        var rect = new ColorRect
        {
            Position = position,
            Size = size,
            Color = color,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(rect);
    }
}
