using Godot;

namespace Veld.Sim.Missions;

/// <summary>
/// Done when the player finishes a conversation with Target, or with the mission giver when the step
/// names no target. Registered twice, as "talk" and as "return"; the names differ only in the JSON.
/// </summary>
public sealed class TalkStep : IMissionStepKind
{
    public TalkStep(string type) => Type = type;

    public string Type { get; }

    public bool OnTalked(MissionStep step, string npcId, string giver) => TargetOf(step, giver) == npcId;

    public Vector3? Marker(MissionStep step, IWorldQuery world, string giver) => world.FindNpc(TargetOf(step, giver))?.Position;

    private static string TargetOf(MissionStep step, string giver) => string.IsNullOrEmpty(step.Target) ? giver : step.Target;
}
