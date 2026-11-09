using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// M13 shelf matcher. The player selects a carried labelled book, then chooses
/// one of four labelled shelves. A wrong shelf returns a friendly hint and keeps
/// the book in hand.
/// </summary>
public static class LibraryShelfUi
{
    public static void Show(Hud hud, MissionService service, Action<MissionCompletionResult> onComplete,
        Action onChanged)
    {
        var body = hud.OpenModal("Lost Library Books", new Vector2(820, 590), Palette.Blue);
        var header = UiKit.HBox(body, 12);
        UiKit.Icon(header, "library", 42);
        var intro = UiKit.Label(header,
            "Choose a book you are carrying, then choose the shelf with the same word. " +
            "A wrong shelf never loses the book.",
            19, Palette.Navy);
        intro.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var status = UiKit.Label(body, "", 19, Palette.Blue);
        status.Name = "LibraryStatus";

        var selected = "";
        foreach (string book in ChapterTwoRules.BookIds)
        {
            if (service.Data.GetMissionCounter("M13", ChapterTwoRules.HasBookKey(book)) >= 1)
            {
                selected = book;
                break;
            }
        }

        var bookRow = UiKit.HBox(body, 10);
        var bookButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        foreach (string book in ChapterTwoRules.BookIds)
        {
            var button = UiKit.Button(bookRow, ChapterTwoRules.BookLabel(book), 20, new Vector2(0, 52),
                Palette.Cream, Palette.Blue);
            button.Name = $"Book_{book.ToUpperInvariant()}";
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            bookButtons[book] = button;
            string captured = book;
            button.Pressed += () =>
            {
                if (service.Data.GetMissionCounter("M13", ChapterTwoRules.HasBookKey(captured)) < 1 &&
                    service.Data.GetMissionCounter("M13", ChapterTwoRules.ShelfKey(captured)) < 1)
                {
                    status.Text = $"You have not found the {ChapterTwoRules.BookLabel(captured)} book yet.";
                    status.AddThemeColorOverride("font_color", Palette.Red);
                    return;
                }
                if (service.Data.GetMissionCounter("M13", ChapterTwoRules.ShelfKey(captured)) >= 1)
                {
                    status.Text = $"{ChapterTwoRules.BookLabel(captured)} is already on its shelf.";
                    status.AddThemeColorOverride("font_color", Palette.Success);
                    return;
                }
                selected = captured;
                Refresh(bookButtons, service, selected);
                status.Text = $"Selected {ChapterTwoRules.BookLabel(captured)}. Choose its shelf.";
                status.AddThemeColorOverride("font_color", Palette.Blue);
            };
        }

        UiKit.Label(body, "Shelves", 21, Palette.Navy);
        var shelfRow = UiKit.HBox(body, 10);
        foreach (string shelf in ChapterTwoRules.BookIds)
        {
            var button = UiKit.Button(shelfRow, $"{ChapterTwoRules.BookLabel(shelf)} shelf", 19,
                new Vector2(0, 54), new Color("E8F4FF"), Palette.Blue);
            button.Name = $"Shelf_{shelf.ToUpperInvariant()}";
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            string capturedShelf = shelf;
            button.Pressed += () =>
            {
                if (string.IsNullOrEmpty(selected))
                {
                    status.Text = "Find and select a book first.";
                    status.AddThemeColorOverride("font_color", Palette.Red);
                    return;
                }
                var result = service.ShelveBook(selected, capturedShelf);
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
                AudioManager.PlayWorld("book_collect", -7f);
                selected = NextCarried(service);
                Refresh(bookButtons, service, selected);
                onChanged?.Invoke();
            };
        }

        UiKit.Label(body,
            "SUN, MOON, TREE and STAR. Read the cover, then read the shelf.",
            18, new Color("5C6B7A"));
        Refresh(bookButtons, service, selected);
        if (string.IsNullOrEmpty(selected))
        {
            status.Text = "No books in hand yet. Search near the shop, town hall, playground and green.";
            status.AddThemeColorOverride("font_color", Palette.Orange);
        }
    }

    private static void Refresh(Dictionary<string, Button> buttons, MissionService service, string selected)
    {
        foreach (var pair in buttons)
        {
            bool shelved = service.Data.GetMissionCounter("M13", ChapterTwoRules.ShelfKey(pair.Key)) >= 1;
            bool carried = service.Data.GetMissionCounter("M13", ChapterTwoRules.HasBookKey(pair.Key)) >= 1;
            pair.Value.Disabled = shelved || !carried;
            pair.Value.Text = shelved
                ? $"{ChapterTwoRules.BookLabel(pair.Key)} ✓"
                : pair.Key == selected
                    ? $"▶ {ChapterTwoRules.BookLabel(pair.Key)}"
                    : ChapterTwoRules.BookLabel(pair.Key);
        }
    }

    private static string NextCarried(MissionService service)
    {
        foreach (string book in ChapterTwoRules.BookIds)
            if (service.Data.GetMissionCounter("M13", ChapterTwoRules.HasBookKey(book)) >= 1)
                return book;
        return "";
    }
}
