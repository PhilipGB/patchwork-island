using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// M17 market checkout. The 20-coin purse is mission stock and never touches
/// the player's wallet. Add/remove is reversible until the exact list is paid.
/// </summary>
public static class MarketMathsUi
{
    private sealed class Row
    {
        public string Item = "";
        public Label Count = null;
        public Button Add = null;
        public Button Remove = null;
    }

    public static void Show(Hud hud, FinaleMissionTasks service, Action<MissionCompletionResult> onComplete,
        Action onChanged)
    {
        if (service.Data.ActiveMission != "M17")
        {
            hud.ShowInfo("MARKET", "Start Market Day Maths at the JOBS board to use the 20 job coins.");
            return;
        }

        var body = hud.OpenModal("Market Day Maths", new Vector2(900, 650), Palette.Blue);
        var purse = UiKit.HBox(body, 10);
        UiKit.Icon(purse, "coin", 28);
        UiKit.Label(purse, $"Job purse: {FinaleRules.MarketBudget} coins", 22, Palette.Navy,
            HorizontalAlignment.Left, false);
        UiKit.Spacer(purse);
        UiKit.Label(purse, "These are not your own coins.", 17, new Color("5C6B7A"),
            HorizontalAlignment.Left, false);

        var request = UiKit.Panel(body, new Color("FFF3C9"), Palette.Orange, 14, 3);
        var requestRow = UiKit.HBox(request, 10);
        UiKit.Icon(requestRow, "basket", 30);
        UiKit.Label(requestRow, "FESTIVAL LIST:  2 bread  +  1 milk  +  2 apples", 21,
            Palette.Navy, HorizontalAlignment.Left, false);

        var totalPanel = UiKit.Panel(body, new Color("E7F2FF"), Palette.Blue, 14, 3);
        var totalRow = UiKit.HBox(totalPanel, 22);
        var total = UiKit.Label(totalRow, "Total 0 / 20", 23, Palette.Navy, HorizontalAlignment.Left, false);
        total.Name = "MarketTotal";
        var change = UiKit.Label(totalRow, "Change 20", 23, Palette.Navy, HorizontalAlignment.Left, false);
        change.Name = "MarketChange";
        UiKit.Spacer(totalRow);

        var status = UiKit.Label(body, "", 19, Palette.Navy);
        status.Name = "MarketStatus";
        status.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var rows = new List<Row>();
        foreach (string item in FinaleRules.MarketItems)
        {
            var panel = UiKit.Panel(body, Palette.Cream, Palette.Stone, 14, 3);
            var row = UiKit.HBox(panel, 10);
            UiKit.Icon(row, item, 34);
            var info = UiKit.VBox(row, 0);
            info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            UiKit.Label(info,
                $"{char.ToUpperInvariant(item[0]) + item[1..]}  ·  {FinaleRules.MarketPrice(item)} job coins each",
                20, Palette.Navy, HorizontalAlignment.Left, false);

            var count = UiKit.Label(row, "0", 26, Palette.Navy, HorizontalAlignment.Center, false);
            count.Name = $"MarketCount_{item}";
            count.CustomMinimumSize = new Vector2(46, 0);
            var remove = UiKit.Button(row, "Remove", 18, new Vector2(108, 44), Palette.Cream, Palette.Stone);
            remove.Name = $"MarketRemove_{item}";
            var add = UiKit.Button(row, $"+{FinaleRules.MarketPrice(item)}", 18, new Vector2(110, 44),
                new Color("FFF3C9"), Palette.Orange);
            add.Name = $"MarketAdd_{item}";
            var entry = new Row { Item = item, Count = count, Add = add, Remove = remove };
            rows.Add(entry);
            add.Pressed += () => Change(service, hud, item, true,
                () => Refresh(service, rows, total, change, status), onChanged);
            remove.Pressed += () => Change(service, hud, item, false,
                () => Refresh(service, rows, total, change, status), onChanged);
        }

        var actions = UiKit.HBox(body, 10);
        var finalise = UiKit.Button(actions, "Pay with job coins", 21, new Vector2(0, 54),
            new Color("DFF6E3"), Palette.Success);
        finalise.Name = "MarketFinalise";
        finalise.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        finalise.Pressed += () =>
        {
            var result = service.FinaliseMarket();
            if (!result.Success)
            {
                status.Text = result.Message;
                status.AddThemeColorOverride("font_color", Palette.Red);
                AudioManager.PlayUi("ui_error");
                return;
            }
            hud.CloseModal();
            onComplete?.Invoke(result.Completion);
        };
        var close = UiKit.Button(actions, "Step away", 19, new Vector2(0, 54),
            Palette.Cream, Palette.Stone);
        close.Name = "MarketClose";
        close.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        close.Pressed += () => hud.CloseModal();

        Refresh(service, rows, total, change, status);
    }

    private static void Change(FinaleMissionTasks service, Hud hud, string item, bool add,
        Action refresh, Action onChanged)
    {
        var result = add ? service.AddMarketItem(item) : service.RemoveMarketItem(item);
        if (!result.Success)
        {
            AudioManager.PlayUi("ui_error");
            hud.ShowHint(result.Message);
            return;
        }
        AudioManager.PlayWorld(add ? "coin_spend" : "ui_cancel", -7f);
        onChanged?.Invoke();
        refresh();
    }

    private static void Refresh(FinaleMissionTasks service, List<Row> rows, Label total, Label change,
        Label status)
    {
        int spent = FinaleRules.MarketSpent(service.Data);
        foreach (var row in rows)
        {
            int count = FinaleRules.MarketCount(service.Data, row.Item);
            row.Count.Text = count.ToString();
            row.Add.Disabled = !FinaleRules.CanAddMarketItem(
                FinaleRules.MarketCount(service.Data, "bread"),
                FinaleRules.MarketCount(service.Data, "milk"),
                FinaleRules.MarketCount(service.Data, "apple"), row.Item);
            row.Remove.Disabled = count <= 0;
        }

        if (total != null)
            total.Text = $"Total {spent} / {FinaleRules.MarketBudget}";
        if (change != null)
            change.Text = $"Change {FinaleRules.MarketChange(service.Data)}";

        if (status == null) return;
        if (FinaleRules.MarketListExact(
                FinaleRules.MarketCount(service.Data, "bread"),
                FinaleRules.MarketCount(service.Data, "milk"),
                FinaleRules.MarketCount(service.Data, "apple")))
        {
            status.Text = "Perfect list! Total 14, change 6. Press Pay with job coins.";
            status.AddThemeColorOverride("font_color", Palette.Success);
        }
        else
        {
            status.Text = "The list needs 2 bread, 1 milk and 2 apples.";
            status.AddThemeColorOverride("font_color", Palette.Navy);
        }
    }
}
