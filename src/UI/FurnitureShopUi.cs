using System;
using System.Linq;
using Godot;

namespace PatchworkIsland;

/// <summary>Furniture catalogue: core-wallet purchases, free starter fallback, placement entry.</summary>
public partial class FurnitureShopUi : Node
{
    private readonly Hud _hud;
    private readonly GameData _data;
    private readonly Action _onPlace;
    private readonly Action _onChanged;
    private Label _walletLabel;
    private Label _statusLabel;
    private Label _totalLabel;
    private Button _freeButton;
    private Button _placeButton;
    private readonly System.Collections.Generic.List<(string Item, Label Count, Button Buy)> _rows = new();

    private FurnitureShopUi(Hud hud, GameData data, Action onPlace, Action onChanged)
    {
        _hud = hud;
        _data = data;
        _onPlace = onPlace;
        _onChanged = onChanged;
    }

    public static void Show(Hud hud, GameData data, Action onPlace, Action onChanged)
    {
        var ui = new FurnitureShopUi(hud, data, onPlace, onChanged);
        ui.Build();
        ui.Refresh();
    }

    private void Build()
    {
        var body = _hud.OpenModal("Home Sweet Home", new Vector2(790, 580), Palette.Lavender);

        var header = UiKit.HBox(body, 12);
        UiKit.Icon(header, "house", 32);
        UiKit.Label(header, "Furniture catalogue", 26, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Spacer(header);
        _walletLabel = UiKit.Label(header, "", 21, Palette.Navy, HorizontalAlignment.Left, false);

        var list = UiKit.Panel(body, new Color("FFF3C9"), Palette.Orange, 14, 3);
        var listRow = UiKit.HBox(list, 14);
        UiKit.Icon(listRow, "table", 28);
        UiKit.Icon(listRow, "chair", 28);
        UiKit.Icon(listRow, "plant", 28);
        var listText = UiKit.Label(listRow,
            "House list: 1 table (8)  +  2 chairs (5 each)  +  1 plant (3)  =  21 coins",
            20, Palette.Navy);
        listText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        foreach (var item in FurnitureRules.Items)
        {
            AddItemRow(body, item);
        }

        _totalLabel = UiKit.Label(body, "", 19, Palette.Navy);
        _totalLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        _statusLabel = UiKit.Label(body, "", 18, Palette.Navy);
        _statusLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var hint = UiKit.Label(body,
            "Buy the list with your own coins. If you are short, a free basic starter set keeps the job possible. " +
            "Furniture can be moved any time.",
            16, new Color("5C6B7A"));
        hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        _freeButton = UiKit.Button(body, "Free starter furniture", 19,
            new Vector2(340, 46), new Color("DFF6E3"), Palette.Success);
        _freeButton.Visible = false;
        _freeButton.Pressed += () =>
        {
            var outcome = FurnitureRules.GrantFreeStarterSet(_data);
            _onChanged?.Invoke();
            Refresh();
            _statusLabel.Text = outcome.Message;
            _statusLabel.AddThemeColorOverride("font_color", Palette.Success);
        };

        _placeButton = UiKit.Button(body, "Place furniture", 22, new Vector2(0, 52), new Color("E8E1FF"), Palette.Lavender);
        _placeButton.Pressed += () =>
        {
            _hud.CloseModal();
            _onPlace?.Invoke();
        };
    }

    private void AddItemRow(VBoxContainer body, string item)
    {
        int price = BudgetRules.FurniturePrice(item);
        var panel = UiKit.Panel(body, Palette.Cream, Palette.Stone, 12, 3);
        var row = UiKit.HBox(panel, 10);
        UiKit.Icon(row, item, 32);
        var info = UiKit.VBox(row, 0);
        info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UiKit.Label(info, FurnitureRules.Label(item), 21, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Label(info, $"{price} coins", 16, new Color("5C6B7A"), HorizontalAlignment.Left, false);
        var countLabel = UiKit.Label(row, "0 / 0", 22, Palette.Navy, HorizontalAlignment.Center, false);
        countLabel.CustomMinimumSize = new Vector2(110, 0);
        var buy = UiKit.Button(row, $"Buy  +{price}", 18, new Vector2(130, 44),
            new Color("FFF3C9"), Palette.Orange);
        buy.Pressed += () =>
        {
            var outcome = FurnitureRules.Buy(_data, item);
            if (!outcome.Success)
            {
                _statusLabel.Text = outcome.Message;
                _statusLabel.AddThemeColorOverride("font_color", Palette.Red);
                return;
            }
            _onChanged?.Invoke();
            Refresh();
        };
        _rows.Add((item, countLabel, buy));
    }

    private void Refresh()
    {
        _walletLabel.Text = $"{_data.Coins} coins";
        bool allOwned = true;
        foreach (var row in _rows)
        {
            int required = FurnitureRules.Required[row.Item];
            int owned = FurnitureRules.Owned(_data, row.Item);
            int placed = FurnitureRules.Placed(_data, row.Item);
            row.Count.Text = $"{owned} / {required}\nowned · {placed} placed";
            int price = BudgetRules.FurniturePrice(row.Item);
            row.Buy.Disabled = owned >= required || _data.Coins < price;
            if (owned < required) allOwned = false;
        }

        int missingCost = FurnitureRules.MissingCost(_data);
        _totalLabel.Text = $"Wallet {_data.Coins} · missing house pieces cost {missingCost} · full set 21";
        _freeButton.Visible = !allOwned && missingCost > _data.Coins;
        bool anyToPlace = _data.FurniturePlacements.Count > 0 ||
                          FurnitureRules.Items.Any(item => FurnitureRules.Available(_data, item) > 0);
        _placeButton.Disabled = !anyToPlace;

        if (allOwned)
        {
            _statusLabel.Text = "Everything is bought. Choose Place furniture to put it inside.";
            _statusLabel.AddThemeColorOverride("font_color", Palette.Success);
            return;
        }

        _statusLabel.Text = "Buy the missing pieces, or use the free starter set if coins are short.";
        _statusLabel.AddThemeColorOverride("font_color", Palette.Navy);
    }
}
