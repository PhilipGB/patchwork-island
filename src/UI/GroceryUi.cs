using System;
using Godot;

namespace PatchworkIsland;

/// <summary>The M04 shop. Uses a dedicated 12-coin job purse, never the wallet.</summary>
public partial class GroceryUi : Node
{
    private readonly Hud _hud;
    private readonly MissionService _missions;
    private readonly Action _onChanged;
    private Label _breadCount;
    private Label _milkCount;
    private Label _totalLabel;
    private Label _changeLabel;
    private Label _statusLabel;
    private Button _breadAdd;
    private Button _breadRemove;
    private Button _milkAdd;
    private Button _milkRemove;
    private PanelContainer _totalPanel;

    private GroceryUi(Hud hud, MissionService missions, Action onChanged)
    {
        _hud = hud;
        _missions = missions;
        _onChanged = onChanged;
    }

    public static void Show(Hud hud, MissionService missions, Action onChanged)
    {
        if (missions.Data.ActiveMission != "M04")
        {
            hud.ShowInfo("SHOP", "Start Market Basket at the JOBS board, then come back with the shopping list.");
            return;
        }

        var ui = new GroceryUi(hud, missions, onChanged);
        ui.Build();
        ui.Refresh();
    }

    private void Build()
    {
        var body = _hud.OpenModal("Market Basket", new Vector2(780, 560), Palette.Blue);

        var purseRow = UiKit.HBox(body, 10);
        UiKit.Icon(purseRow, "coin", 28);
        UiKit.Label(purseRow, "Job purse: 12 coins", 22, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Spacer(purseRow);
        UiKit.Label(purseRow, "These are not your own coins.", 17, new Color("5C6B7A"), HorizontalAlignment.Left, false);

        var request = UiKit.Panel(body, new Color("FFF3C9"), Palette.Orange, 14, 3);
        var requestRow = UiKit.HBox(request, 10);
        UiKit.Icon(requestRow, "basket", 30);
        var requestText = UiKit.Label(requestRow, "TOWN HALL wants  2 bread  +  1 milk", 22, Palette.Navy,
            HorizontalAlignment.Left, false);
        requestText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        AddItemRow(body, "bread", "Bread", 3, out _breadCount, out _breadAdd, out _breadRemove);
        AddItemRow(body, "milk", "Milk", 4, out _milkCount, out _milkAdd, out _milkRemove);

        _totalPanel = UiKit.Panel(body, new Color("E7F2FF"), Palette.Blue, 14, 3);
        var totalBox = UiKit.HBox(_totalPanel, 22);
        _totalLabel = UiKit.Label(totalBox, "Total 0", 23, Palette.Navy, HorizontalAlignment.Left, false);
        _changeLabel = UiKit.Label(totalBox, "Change 12", 23, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Spacer(totalBox);

        _statusLabel = UiKit.Label(body, "", 19, Palette.Navy);
        _statusLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var hint = UiKit.Label(body,
            "Add what the list asks for. Take something back for free if you change your mind. " +
            "You cannot spend more than the 12 job coins.",
            17, new Color("5C6B7A"));
        hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    private void AddItemRow(VBoxContainer body, string key, string label, int price, out Label countLabel,
        out Button addButton, out Button removeButton)
    {
        var panel = UiKit.Panel(body, Palette.Cream, Palette.Stone, 14, 3);
        var row = UiKit.HBox(panel, 10);
        UiKit.Icon(row, key, 34);
        var info = UiKit.VBox(row, 0);
        info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UiKit.Label(info, label, 22, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Label(info, $"{price} coins each", 17, new Color("5C6B7A"), HorizontalAlignment.Left, false);

        countLabel = UiKit.Label(row, "0", 26, Palette.Navy, HorizontalAlignment.Center, false);
        countLabel.CustomMinimumSize = new Vector2(46, 0);

        removeButton = UiKit.Button(row, "Remove", 18, new Vector2(108, 44), Palette.Cream, Palette.Stone);
        removeButton.Pressed += () => Change(key, false);

        addButton = UiKit.Button(row, $"Add  +{price}", 18, new Vector2(120, 44), new Color("FFF3C9"), Palette.Orange);
        addButton.Pressed += () => Change(key, true);
    }

    private void Change(string key, bool add)
    {
        var outcome = add ? _missions.AddGrocery(key) : _missions.RemoveGrocery(key);
        if (!outcome.Success)
        {
            AudioManager.PlayUi("ui_error");
            _statusLabel.Text = outcome.Message;
            _statusLabel.AddThemeColorOverride("font_color", Palette.Red);
            return;
        }
        AudioManager.PlayWorld(add ? "coin_spend" : "ui_cancel", -7f);
        Refresh();
        _onChanged?.Invoke();
    }

    private void Refresh()
    {
        int bread = _missions.GroceryBread;
        int milk = _missions.GroceryMilk;
        int total = _missions.GrocerySpent;
        int change = _missions.GroceryChange;

        _breadCount.Text = bread.ToString();
        _milkCount.Text = milk.ToString();
        _breadAdd.Disabled = !BudgetRules.CanAddGrocery(bread, milk, "bread");
        _milkAdd.Disabled = !BudgetRules.CanAddGrocery(bread, milk, "milk");
        _breadRemove.Disabled = bread <= 0;
        _milkRemove.Disabled = milk <= 0;

        _totalLabel.Text = $"Total  {total} / {BudgetRules.GroceryBudget}";
        _changeLabel.Text = $"Change  {change}";
        _totalPanel.AddThemeStyleboxOverride("panel",
            UiKit.Style(BudgetRules.IsGroceryCorrect(bread, milk) ? new Color("DFF6E3") : new Color("E7F2FF"),
                BudgetRules.IsGroceryCorrect(bread, milk) ? Palette.Success : Palette.Blue, 14, 3));

        if (BudgetRules.IsGroceryCorrect(bread, milk))
        {
            _statusLabel.Text = "Perfect basket! Total 10, change 2. Take it to TOWN HALL.";
            _statusLabel.AddThemeColorOverride("font_color", Palette.Success);
        }
        else if (BudgetRules.IsGroceryListComplete(bread, milk))
        {
            _statusLabel.Text = "That is enough for the list. Check the total, then go to TOWN HALL.";
            _statusLabel.AddThemeColorOverride("font_color", Palette.Success);
        }
        else
        {
            _statusLabel.Text = $"The list needs 2 bread and 1 milk. You have {bread} bread and {milk} milk.";
            _statusLabel.AddThemeColorOverride("font_color", Palette.Navy);
        }
    }
}
