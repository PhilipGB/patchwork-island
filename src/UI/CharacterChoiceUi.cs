using System;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// The first step of a new game: one large, friendly Boy or Girl card with a
/// paper-doll preview. No sliders, no deep menus, and the choice is visible in
/// the world immediately.
/// </summary>
public static class CharacterChoiceUi
{
    public static void Show(Hud hud, Action<string> onChosen, Action onCancel)
    {
        var body = hud.OpenModal("Who would you like to play?", new Vector2(860, 640),
            Palette.Lavender, false, null, false);

        var intro = UiKit.HBox(body, 12);
        var instruction = UiKit.Label(intro,
            "Choose a character. You can change your look at the Clothes Shop in town.",
            20, Palette.Navy);
        instruction.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var cancel = UiKit.Button(intro, "Cancel", 19, new Vector2(130, 44), Palette.Cream, Palette.Stone);
        cancel.Pressed += () =>
        {
            hud.CloseModal();
            onCancel?.Invoke();
        };

        var cards = UiKit.HBox(body, 18);
        cards.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var firstChoice = AddCard(cards, hud, CharacterCustomisation.Boy, Palette.Blue, onChosen);
        AddCard(cards, hud, CharacterCustomisation.Girl, Palette.Pink, onChosen);
        // Opening focus lands on the first playable choice, not Cancel.
        Callable.From(firstChoice.GrabFocus).CallDeferred();
    }

    private static Button AddCard(HBoxContainer cards, Hud hud, string preset, Color border,
        Action<string> onChosen)
    {
        var card = UiKit.Panel(cards, Palette.Cream, border, 20, 4);
        card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var box = UiKit.VBox(card, 8);
        box.Alignment = BoxContainer.AlignmentMode.Center;

        var preview = new CenterContainer { CustomMinimumSize = new Vector2(0, 250) };
        box.AddChild(preview);
        var doll = new CharacterDoll();
        preview.AddChild(doll);
        doll.SetAppearance(preset, CharacterCustomisation.DefaultOutfit, false);

        UiKit.Label(box, CharacterCustomisation.PresetLabel(preset), 30, Palette.Navy,
            HorizontalAlignment.Center, false);
        UiKit.Label(box,
            preset == CharacterCustomisation.Girl
                ? "Long hair, bow and dress."
                : "Short hair, shirt and shorts.",
            17, new Color("5C6B7A"), HorizontalAlignment.Center, false);

        var choose = UiKit.Button(box, $"Play as {CharacterCustomisation.PresetLabel(preset)}", 21,
            new Vector2(0, 54), preset == CharacterCustomisation.Girl ? new Color("FFE4EE") : new Color("E7F2FF"),
            border);
        choose.Pressed += () =>
        {
            hud.CloseModal();
            onChosen?.Invoke(preset);
        };
        return choose;
    }
}
