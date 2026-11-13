using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// M12 bakery counter: three visible customer order cards, large pictogram
/// counts and plus/minus steppers. Serving the wrong quantity leaves the order
/// untouched; serving the right one fills the bakery display permanently.
/// </summary>
public static class BakeryUi
{
    private sealed class Row
    {
        public ChapterTwoRules.BakeryOrder Order;
        public Label BreadCount;
        public Label CakeCount;
        public Button BreadAdd;
        public Button BreadRemove;
        public Button CakeAdd;
        public Button CakeRemove;
        public Button Serve;
        public Label State;
    }

    public static void Show(Hud hud, CommunityMissionTasks service, Action<MissionCompletionResult> onComplete,
        Action onChanged)
    {
        var body = hud.OpenModal("Bakery Busy Day", new Vector2(900, 670), Palette.Orange);
        var intro = UiKit.HBox(body, 12);
        UiKit.Icon(intro, "bakery", 40);
        var introText = UiKit.Label(intro,
            "Read each order card, count the bread and cakes, then press Serve. " +
            "There is no time limit and a wrong count costs nothing.",
            19, Palette.Navy);
        introText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var status = UiKit.Label(body, "Three orders are waiting.", 19, Palette.Blue);
        status.Name = "BakeryStatus";

        var rows = new List<Row>();
        var bread = new Dictionary<string, int>();
        var cakes = new Dictionary<string, int>();
        foreach (var order in ChapterTwoRules.BakeryOrders)
        {
            bread[order.CustomerId] = 0;
            cakes[order.CustomerId] = 0;
            var panel = UiKit.Panel(body, Palette.Cream, Palette.Lavender, 14, 3);
            var row = UiKit.HBox(panel, 14);
            var portraitPanel = UiKit.Panel(row, new Color("F7E9C9"), Palette.Lavender, 12, 3);
            var portrait = new TextureRect
            {
                Texture = NpcPortrait.Load(order.CustomerId),
                CustomMinimumSize = new Vector2(92, 92),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            portraitPanel.AddChild(portrait);
            var info = UiKit.VBox(row, 3);
            info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            UiKit.Label(info, $"{order.CustomerName}'s order", 21, Palette.Navy,
                HorizontalAlignment.Left, false);
            UiKit.Label(info, $"{order.Bread} bread  +  {order.Cakes} cakes", 18,
                new Color("42526B"), HorizontalAlignment.Left, false);
            var state = UiKit.Label(info, "", 17, Palette.Success, HorizontalAlignment.Left, false);
            var steppers = UiKit.HBox(info, 12);
            var breadBox = UiKit.HBox(steppers, 4);
            UiKit.Label(breadBox, "Bread", 17, Palette.Navy, HorizontalAlignment.Left, false);
            var breadRemove = UiKit.Button(breadBox, "−", 22, new Vector2(52, 42), Palette.Cream, Palette.Stone);
            breadRemove.Name = $"BreadRemove_{order.CustomerId}";
            var breadCount = UiKit.Label(breadBox, "0", 22, Palette.Navy, HorizontalAlignment.Center, false);
            breadCount.Name = $"BreadCount_{order.CustomerId}";
            breadCount.CustomMinimumSize = new Vector2(36, 0);
            var breadAdd = UiKit.Button(breadBox, "+", 22, new Vector2(52, 42), new Color("FFF3C9"), Palette.Orange);
            breadAdd.Name = $"BreadAdd_{order.CustomerId}";
            var cakeBox = UiKit.HBox(steppers, 4);
            UiKit.Label(cakeBox, "Cakes", 17, Palette.Navy, HorizontalAlignment.Left, false);
            var cakeRemove = UiKit.Button(cakeBox, "−", 22, new Vector2(52, 42), Palette.Cream, Palette.Stone);
            cakeRemove.Name = $"CakeRemove_{order.CustomerId}";
            var cakeCount = UiKit.Label(cakeBox, "0", 22, Palette.Navy, HorizontalAlignment.Center, false);
            cakeCount.Name = $"CakeCount_{order.CustomerId}";
            cakeCount.CustomMinimumSize = new Vector2(36, 0);
            var cakeAdd = UiKit.Button(cakeBox, "+", 22, new Vector2(52, 42), new Color("FFF3C9"), Palette.Orange);
            cakeAdd.Name = $"CakeAdd_{order.CustomerId}";
            var serve = UiKit.Button(row, "Serve", 19, new Vector2(120, 48),
                new Color("DFF6E3"), Palette.Success);
            serve.Name = $"Serve_{order.CustomerId}";

            var entry = new Row
            {
                Order = order,
                BreadCount = breadCount,
                CakeCount = cakeCount,
                BreadAdd = breadAdd,
                BreadRemove = breadRemove,
                CakeAdd = cakeAdd,
                CakeRemove = cakeRemove,
                Serve = serve,
                State = state,
            };
            rows.Add(entry);
            string customer = order.CustomerId;
            breadAdd.Pressed += () =>
            {
                if (service.Data.GetMissionCounter("M12", $"order_{customer}") >= 1) return;
                bread[customer] = Math.Min(9, bread[customer] + 1);
                Refresh(entry, bread[customer], cakes[customer], service);
            };
            breadRemove.Pressed += () =>
            {
                if (service.Data.GetMissionCounter("M12", $"order_{customer}") >= 1) return;
                bread[customer] = Math.Max(0, bread[customer] - 1);
                Refresh(entry, bread[customer], cakes[customer], service);
            };
            cakeAdd.Pressed += () =>
            {
                if (service.Data.GetMissionCounter("M12", $"order_{customer}") >= 1) return;
                cakes[customer] = Math.Min(9, cakes[customer] + 1);
                Refresh(entry, bread[customer], cakes[customer], service);
            };
            cakeRemove.Pressed += () =>
            {
                if (service.Data.GetMissionCounter("M12", $"order_{customer}") >= 1) return;
                cakes[customer] = Math.Max(0, cakes[customer] - 1);
                Refresh(entry, bread[customer], cakes[customer], service);
            };
            serve.Pressed += () =>
            {
                var result = service.ServeBakeryOrder(customer, bread[customer], cakes[customer]);
                if (!result.Success)
                {
                    status.Text = result.Message;
                    status.AddThemeColorOverride("font_color", Palette.Red);
                    AudioManager.PlayUi("ui_error");
                    return;
                }
                if (result.Completion != null)
                {
                    hud.CloseModal();
                    onComplete?.Invoke(result.Completion);
                    return;
                }
                status.Text = result.Message;
                status.AddThemeColorOverride("font_color", Palette.Success);
                AudioManager.PlayWorld("coin_collect", -8f);
                Refresh(entry, bread[customer], cakes[customer], service);
                onChanged?.Invoke();
            };
            Refresh(entry, 0, 0, service);
        }

        var note = UiKit.Label(body,
            "The bakery uses its own stock, never your coins. " +
            "The order card is a count to read, not a number to type.",
            17, new Color("5C6B7A"));
        note.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    private static void Refresh(Row row, int bread, int cakes, CommunityMissionTasks service)
    {
        bool done = service.Data.GetMissionCounter("M12", $"order_{row.Order.CustomerId}") >= 1;
        row.BreadCount.Text = done ? row.Order.Bread.ToString() : bread.ToString();
        row.CakeCount.Text = done ? row.Order.Cakes.ToString() : cakes.ToString();
        row.BreadAdd.Disabled = done;
        row.BreadRemove.Disabled = done;
        row.CakeAdd.Disabled = done;
        row.CakeRemove.Disabled = done;
        row.Serve.Disabled = done;
        if (done)
        {
            row.State.Text = "Served ✓ display filled";
            row.State.AddThemeColorOverride("font_color", Palette.Success);
        }
        else
        {
            row.State.Text = $"Needs {row.Order.Bread} bread and {row.Order.Cakes} cakes";
            row.State.AddThemeColorOverride("font_color", new Color("5C6B7A"));
        }
    }
}
