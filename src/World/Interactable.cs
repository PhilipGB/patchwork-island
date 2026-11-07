using System;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Base for everything the player can use with E. A single generous-range
/// interaction contract keeps pickups, doors, counters, the van and job board
/// consistent. The bright ground ring is the target highlight.
/// </summary>
public partial class Interactable : Node3D
{
    public Func<string> PromptProvider;
    public Func<bool> AvailableProvider;
    public Action InteractAction;
    public string FallbackPrompt = "E: Use";
    public float Range = 3.6f;
    public bool ShowHighlightRing = true;

    private MeshInstance3D _ring;
    private float _age;
    private bool _highlighted;

    public override void _Ready()
    {
        AddToGroup("interactable");
        if (ShowHighlightRing)
        {
            AddChild(new Node3D { Name = "HighlightAnchor" });
            _ring = Geo.Disc(this, new Vector3(0, 0.04f, 0), 1.05f, Palette.Yellow, 0.68f, true);
            _ring.Visible = false;
        }
        SetProcess(true);
    }

    public virtual string GetPrompt() => PromptProvider?.Invoke() ?? FallbackPrompt;

    public virtual bool IsAvailable() => AvailableProvider?.Invoke() ?? true;

    public virtual void Interact() => InteractAction?.Invoke();

    public virtual Vector3 FocusPosition => GlobalPosition;

    public void TargetHighlight(bool on)
    {
        _highlighted = on;
        if (_ring != null)
            _ring.Visible = on && IsAvailable();
    }

    public override void _Process(double delta)
    {
        if (!_highlighted || _ring == null) return;
        _age += (float)delta;
        float pulse = 1.0f + Mathf.Sin(_age * 4f) * 0.08f;
        _ring.Scale = new Vector3(pulse, 1, pulse);
        _ring.RotateY((float)delta * 0.8f);
    }

    public void SetPrompt(Func<string> prompt) => PromptProvider = prompt;

    public static Interactable Create(Node parent, Vector3 worldPosition, string prompt, Action action,
        Func<bool> available = null, bool ring = true, float range = 3.6f, string name = "Interactable")
    {
        var node = new Interactable
        {
            Name = name,
            Position = worldPosition,
            FallbackPrompt = prompt,
            InteractAction = action,
            AvailableProvider = available,
            Range = range,
            ShowHighlightRing = ring,
        };
        parent.AddChild(node);
        return node;
    }
}
