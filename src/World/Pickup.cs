using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>A large, readable collectable that proves quantities through play.</summary>
public partial class Pickup : Interactable
{
    public string PickupId = "";
    public string ItemType = "apple";
    public string MissionId = "M01";
    public int Required = 1;
    public Func<string> ItemPromptProvider;
    public Func<bool> HiddenProvider;

    private Node3D _visual;
    private float _time;
    private float _baseY;
    private readonly float _seed;

    public Pickup()
    {
        _seed = (float)GD.RandRange(0.0, 6.28);
    }

    public override void _Ready()
    {
        Range = 3.9f;
        ShowHighlightRing = true;
        BuildVisual();
        base._Ready();
    }

    public override string GetPrompt()
    {
        if (ItemPromptProvider != null) return ItemPromptProvider();
        return $"E: Pick up {MissionService.ItemName(ItemType).ToLowerInvariant()}";
    }

    public override bool IsAvailable()
    {
        if (HiddenProvider != null && HiddenProvider()) return false;
        return base.IsAvailable();
    }

    public override Vector3 FocusPosition => GlobalPosition + Vector3.Up * 0.65f;

    private void BuildVisual()
    {
        _visual = new Node3D { Name = "PickupVisual" };
        AddChild(_visual);
        switch (ItemType)
        {
            case "apple":
                Geo.Sphere(_visual, new Vector3(0, 0.62f, 0), 0.3f, Palette.Red, 9, 5);
                Geo.Box(_visual, new Vector3(0, 0.83f, 0), new Vector3(0.06f, 0.18f, 0.06f), Palette.WoodDark);
                Geo.Box(_visual, new Vector3(0.12f, 0.91f, 0), new Vector3(0.25f, 0.06f, 0.14f), Palette.GrassDark,
                    new Vector3(0, 0, -25));
                Geo.Sphere(_visual, new Vector3(-0.11f, 0.72f, -0.22f), 0.06f, Palette.White, 6, 3);
                break;
            case "wood":
                Geo.Cylinder(_visual, new Vector3(0, 0.42f, 0), 0.23f, 0.23f, 1.5f, Palette.Wood, 7,
                    new Vector3(0, 0, 90));
                Geo.Cylinder(_visual, new Vector3(0.05f, 0.72f, 0.18f), 0.2f, 0.2f, 1.35f, Palette.WoodDark, 7,
                    new Vector3(0, 15, 90));
                Geo.Cylinder(_visual, new Vector3(0.0f, 0.3f, 0.22f), 0.06f, 0.06f, 1.45f, Color.FromHtml("E7C08A"), 5,
                    new Vector3(0, 0, 90));
                break;
            case "stone":
                Geo.Sphere(_visual, new Vector3(0, 0.42f, 0), 0.42f, Palette.Stone, 6, 3,
                    new Vector3(1.1f, 0.8f, 1.0f));
                Geo.Sphere(_visual, new Vector3(0.3f, 0.3f, 0.18f), 0.26f, Palette.StoneDark, 5, 2);
                Geo.Sphere(_visual, new Vector3(-0.26f, 0.26f, -0.12f), 0.22f, Palette.Stone, 5, 2);
                break;
            case "book_sun":
            case "book_moon":
            case "book_tree":
            case "book_star":
            {
                string word = ItemType.Substring(5).ToUpperInvariant();
                Color cover = ItemType switch
                {
                    "book_sun" => Palette.Yellow,
                    "book_moon" => Palette.Blue,
                    "book_tree" => Palette.Grass,
                    _ => Palette.Pink,
                };
                Geo.Box(_visual, new Vector3(0, 0.55f, 0), new Vector3(0.55f, 0.8f, 0.18f), cover);
                Geo.Box(_visual, new Vector3(0, 0.55f, 0.1f), new Vector3(0.44f, 0.68f, 0.04f), Palette.Cream);
                Geo.Box(_visual, new Vector3(0, 0.55f, 0.13f), new Vector3(0.36f, 0.08f, 0.02f), Palette.Navy);
                Geo.Label(_visual, new Vector3(0, 1.18f, 0), word, 64, Palette.Navy, true, 0.006f);
                break;
            }
            case "blue_parcel":
            case "orange_parcel":
                Color parcelColor = ItemType == "blue_parcel" ? Palette.Blue : Palette.Orange;
                Geo.Box(_visual, new Vector3(0, 0.5f, 0), new Vector3(0.72f, 0.72f, 0.72f), parcelColor);
                Geo.Box(_visual, new Vector3(0, 0.5f, -0.37f), new Vector3(0.5f, 0.5f, 0.03f), Palette.Cream);
                Geo.Box(_visual, new Vector3(0, 0.5f, -0.39f), new Vector3(0.3f, 0.22f, 0.03f), Palette.Navy);
                Geo.Box(_visual, new Vector3(0, 0.5f, -0.39f), new Vector3(0.3f, 0.05f, 0.04f), Palette.Navy);
                break;
            default:
                Geo.Box(_visual, new Vector3(0, 0.5f, 0), Vector3.One * 0.7f, Palette.Yellow);
                break;
        }
        string iconName = ItemType switch
        {
            "apple" => "apple",
            "wood" => "wood",
            "stone" => "stone",
            "blue_parcel" or "orange_parcel" => "parcel",
            "book_sun" or "book_moon" or "book_tree" or "book_star" => "library",
            _ => null,
        };
        // Books carry their own large cover word. A floating generic library
        // icon would overlap that word at reading distance.
        if (iconName != null && !ItemType.StartsWith("book_", StringComparison.Ordinal))
            Geo.IconSprite(_visual, new Vector3(0, 1.15f, 0), iconName, 0.015f, true);
        _baseY = _visual.Position.Y;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (_visual == null) return;
        _time += (float)delta;
        _visual.Position = new Vector3(_visual.Position.X,
            _baseY + Mathf.Sin(_time * 2.3f + _seed) * 0.1f, _visual.Position.Z);
        if (ItemType is "apple" or "blue_parcel" or "orange_parcel" or "stone")
            _visual.RotateY((float)delta * 0.7f);
    }

    public void CollectVisual()
    {
        SetProcess(false);
        Visible = false;
        QueueFree();
    }

    public static Pickup Create(Node parent, Vector3 position, string pickupId, string itemType,
        string missionId, int required, Action<Pickup> onInteract, Func<string> prompt, Func<bool> hidden = null)
    {
        var pickup = new Pickup
        {
            Name = $"Pickup_{pickupId}",
            Position = position,
            PickupId = pickupId,
            ItemType = itemType,
            MissionId = missionId,
            Required = required,
            ItemPromptProvider = prompt,
            HiddenProvider = hidden,
        };
        pickup.InteractAction = () => onInteract?.Invoke(pickup);
        parent.AddChild(pickup);
        return pickup;
    }
}
