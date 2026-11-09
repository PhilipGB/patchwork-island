using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// The town clothes shop and changing room. Purchases come from the normal
/// coin wallet, every owned outfit can be worn for free, and the same screen
/// lets the player revisit the Boy/Girl presentation choice.
/// </summary>
public sealed class ClothesShopUi
{
    private readonly Hud _hud;
    private readonly GameData _data;
    private readonly Func<SaveResult> _onChanged;
    private Label _walletLabel;
    private Label _statusLabel;
    private Label _previewName;
    private Label _wearingLabel;
    private CharacterDoll _preview;
    private Button _boyButton;
    private Button _girlButton;
    private readonly List<OutfitRow> _rows = new();

    private sealed class OutfitRow
    {
        public OutfitDefinition Outfit;
        public Label State;
        public Button Action;
    }

    private ClothesShopUi(Hud hud, GameData data, Func<SaveResult> onChanged)
    {
        _hud = hud;
        _data = data;
        _onChanged = onChanged;
    }

    public static void Show(Hud hud, GameData data, Func<SaveResult> onChanged)
    {
        var ui = new ClothesShopUi(hud, data, onChanged);
        ui.Build();
        ui.Refresh();
    }

    private void Build()
    {
        var body = _hud.OpenModal("Clothes Shop", new Vector2(960, 680), Palette.Orange);

        var header = UiKit.HBox(body, 12);
        UiKit.Icon(header, "shirt", 36);
        var heading = UiKit.Label(header, "Dress up for a happy day in the village", 24,
            Palette.Navy, HorizontalAlignment.Left, false);
        heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UiKit.Icon(header, "coin", 28);
        _walletLabel = UiKit.Label(header, "", 24, Palette.Navy, HorizontalAlignment.Left, false);

        var main = UiKit.HBox(body, 16);
        main.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        BuildPreview(main);

        var list = UiKit.VBox(main, 8);
        list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UiKit.Label(list, "Point at an outfit to try it on in the mirror.", 18,
            new Color("5C6B7A"));

        // Keep purchase/equip feedback above the list so it stays visible when
        // the five outfit rows fill the modal.
        _statusLabel = UiKit.Label(list, "", 18, Palette.Navy);
        _statusLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        foreach (var outfit in CharacterCustomisation.Outfits)
            AddOutfitRow(list, outfit);

        var hint = UiKit.Label(list,
            "Buying spends your own coins. Wearing an outfit you already own is always free. " +
            "You can come back any time.",
            16, new Color("5C6B7A"));
        hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    private void BuildPreview(HBoxContainer main)
    {
        var panel = UiKit.Panel(main, new Color("FFF3C9"), Palette.Pink, 18, 4);
        panel.CustomMinimumSize = new Vector2(260, 0);
        var box = UiKit.VBox(panel, 6);

        UiKit.Label(box, "Changing room", 21, Palette.Navy, HorizontalAlignment.Center, false);

        var mirror = new CenterContainer { CustomMinimumSize = new Vector2(0, 260) };
        box.AddChild(mirror);
        _preview = new CharacterDoll();
        mirror.AddChild(_preview);

        _previewName = UiKit.Label(box, "", 21, Palette.Navy, HorizontalAlignment.Center, false);
        _wearingLabel = UiKit.Label(box, "", 16, new Color("5C6B7A"), HorizontalAlignment.Center);

        UiKit.Separator(box);
        UiKit.Label(box, "Character", 18, Palette.Navy, HorizontalAlignment.Center, false);
        var presets = UiKit.HBox(box, 8);
        _boyButton = UiKit.Button(presets, "Boy", 19, new Vector2(0, 44), Palette.Cream, Palette.Blue);
        _boyButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _boyButton.Pressed += () => ChoosePreset(CharacterCustomisation.Boy);
        _girlButton = UiKit.Button(presets, "Girl", 19, new Vector2(0, 44), Palette.Cream, Palette.Pink);
        _girlButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _girlButton.Pressed += () => ChoosePreset(CharacterCustomisation.Girl);
    }

    private void AddOutfitRow(VBoxContainer list, OutfitDefinition outfit)
    {
        var panel = UiKit.Panel(list, Palette.Cream, Palette.Stone, 12, 3);
        var row = UiKit.HBox(panel, 10);

        var swatches = UiKit.HBox(row, 3);
        UiKit.Swatch(swatches, Color.FromHtml(outfit.MainHex), 20);
        UiKit.Swatch(swatches, Color.FromHtml(outfit.SecondaryHex), 20);
        UiKit.Swatch(swatches, Color.FromHtml(outfit.AccentHex), 20);

        var info = UiKit.VBox(row, 0);
        info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UiKit.Label(info, outfit.Name, 21, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Label(info, outfit.Description, 16, new Color("5C6B7A"), HorizontalAlignment.Left, false);

        var state = UiKit.Label(row, "", 17, Palette.Navy, HorizontalAlignment.Right, false);
        state.CustomMinimumSize = new Vector2(150, 0);

        var action = UiKit.Button(row, "", 18, new Vector2(130, 44), new Color("FFF3C9"), Palette.Orange);
        action.Name = "Outfit_" + outfit.Id;
        action.Pressed += () => Activate(outfit);
        action.MouseEntered += () => PreviewOutfit(outfit.Id);
        action.FocusEntered += () => PreviewOutfit(outfit.Id);

        _rows.Add(new OutfitRow { Outfit = outfit, State = state, Action = action });
    }

    private void Activate(OutfitDefinition outfit)
    {
        bool owned = CharacterCustomisation.Owns(_data, outfit.Id);
        var outcome = owned
            ? CharacterCustomisation.EquipOutfit(_data, outfit.Id)
            : CharacterCustomisation.BuyOutfit(_data, outfit.Id);

        if (!outcome.Success)
        {
            ShowStatus(outcome.Message, true);
            Refresh();
            return;
        }

        var save = _onChanged?.Invoke();
        Refresh();
        PreviewOutfit(outfit.Id);
        if (save != null && !save.Success)
        {
            ShowStatus(outcome.Message + " It could not be saved yet; use Save now from the pause menu.",
                true);
            _hud.SetModalStatus(
                "Your new look is applied for this session, but it could not be saved yet. " +
                "Your previous save is unchanged.", true);
        }
        else
        {
            ShowStatus(outcome.Message, false);
        }
    }

    private void ChoosePreset(string preset)
    {
        var outcome = CharacterCustomisation.SetPreset(_data, preset);
        if (!outcome.Success)
        {
            ShowStatus(outcome.Message, true);
            return;
        }

        var save = _onChanged?.Invoke();
        Refresh();
        PreviewOutfit(_data.EquippedOutfit);
        if (save != null && !save.Success)
        {
            ShowStatus(outcome.Message + " It could not be saved yet.", true);
            _hud.SetModalStatus(
                "The character change is applied for this session, but it could not be saved yet.", true);
        }
        else
        {
            ShowStatus(outcome.Message, false);
        }
    }

    private void Refresh()
    {
        _walletLabel.Text = $"{_data.Coins} coins";
        _boyButton.Text = _data.CharacterPreset == CharacterCustomisation.Boy ? "Boy  ✓" : "Boy";
        _girlButton.Text = _data.CharacterPreset == CharacterCustomisation.Girl ? "Girl  ✓" : "Girl";

        foreach (var row in _rows)
        {
            bool owned = CharacterCustomisation.Owns(_data, row.Outfit.Id);
            bool worn = _data.EquippedOutfit == row.Outfit.Id;
            if (worn)
            {
                row.State.Text = "Owned · Wearing";
                row.State.AddThemeColorOverride("font_color", Palette.Success);
                row.Action.Text = "Wearing";
                row.Action.Disabled = true;
            }
            else if (owned)
            {
                row.State.Text = "Owned";
                row.State.AddThemeColorOverride("font_color", Palette.Success);
                row.Action.Text = "Wear";
                row.Action.Disabled = false;
            }
            else if (_data.Coins >= row.Outfit.Price)
            {
                row.State.Text = $"{row.Outfit.Price} coins";
                row.State.AddThemeColorOverride("font_color", Palette.Navy);
                row.Action.Text = $"Buy  {row.Outfit.Price}";
                row.Action.Disabled = false;
            }
            else
            {
                row.State.Text = $"Not enough coins ({_data.Coins}/{row.Outfit.Price})";
                row.State.AddThemeColorOverride("font_color", Palette.Red);
                row.Action.Text = $"Need  {row.Outfit.Price}";
                row.Action.Disabled = true;
            }
        }

        var equipped = CharacterCustomisation.Equipped(_data);
        _wearingLabel.Text = $"Wearing: {equipped.Name}";
        PreviewOutfit(_data.EquippedOutfit);
    }

    private void PreviewOutfit(string outfitId)
    {
        var outfit = CharacterCustomisation.Find(outfitId) ?? CharacterCustomisation.Find(CharacterCustomisation.DefaultOutfit);
        _previewName.Text = outfit.Name;
        _preview.SetAppearance(_data.CharacterPreset, outfit.Id,
            _data.UnlockedCosmetics.Contains("red_cap"));
    }

    private void ShowStatus(string message, bool error)
    {
        _statusLabel.Text = message;
        _statusLabel.AddThemeColorOverride("font_color", error ? Palette.Red : Palette.Success);
    }
}
