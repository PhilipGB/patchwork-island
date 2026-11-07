using Godot;

namespace PatchworkIsland;

/// <summary>Shared contract for click-to-place construction modes.</summary>
public interface IBuildMode
{
    bool Active { get; }
    bool HandleWorldClick(Vector2 screenPosition);
    void CloseBuildMode();
    void RotateSelected();
}
