using Godot;

namespace PatchworkIsland;

/// <summary>Narrow boundary from activity controllers to scene/session orchestration.</summary>
internal interface IGameplayHost
{
    bool Started { get; }
    bool Driving { get; }
    void RefreshMission();
    void RefreshMusic();
    void CompleteVillageMission(MissionCompletionResult result);
    void SetObjective(Vector3 position, string label, string icon, Color color);
    SaveResult Save(SaveReason reason);
    void NotifyRecovery(string message);
}
