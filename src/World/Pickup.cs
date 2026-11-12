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
    public bool MissionAvailable { get; set; } = true;
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
        if (!MissionAvailable || (HiddenProvider != null && HiddenProvider())) return false;
        return base.IsAvailable();
    }

    public override Vector3 FocusPosition => GlobalPosition + Vector3.Up * 0.65f;

    public string VisualTemplatePath { get; private set; } = "";

    private void BuildVisual()
    {
        _visual = new Node3D { Name = "PickupVisual" };
        AddChild(_visual);
        string templatePath = VisualTemplate.PickupPath(ItemType);
        Node3D template = VisualTemplate.Instantiate(_visual, "Visual", templatePath);
        VisualTemplatePath = templatePath;
        Range = template.GetMeta("radius", Range).AsSingle();

        Color? cover = ItemType switch
        {
            "book_sun" => Palette.Yellow,
            "book_moon" => Palette.Blue,
            "book_tree" => Palette.Grass,
            "book_star" => Palette.Pink,
            "blue_parcel" => Palette.Blue,
            "orange_parcel" => Palette.Orange,
            _ => null,
        };
        if (cover.HasValue &&
            template.GetNodeOrNull<MeshInstance3D>("Collectible") is { } collectible)
        {
            collectible.MaterialOverride = VisualTemplate.FlatMaterial(cover.Value);
        }

        if (ItemType.StartsWith("book_", StringComparison.Ordinal))
        {
            string word = ItemType.Substring(5).ToUpperInvariant();
            Geo.Label(_visual, new Vector3(0, 1.0f, 0), word, 64, Palette.Navy, true, 0.006f);
        }
        else
        {
            string iconName = ItemType switch
            {
                "apple" => "apple",
                "wood" => "wood",
                "stone" => "stone",
                "blue_parcel" or "orange_parcel" => "parcel",
                _ => null,
            };
            if (iconName != null)
                Geo.IconSprite(_visual, new Vector3(0, 1.15f, 0), iconName, 0.015f, true);
        }
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
