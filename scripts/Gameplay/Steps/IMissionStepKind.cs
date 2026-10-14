using Godot;
using Veld.Core;

namespace Veld.Gameplay.Steps;

/// <summary>
/// One kind of mission step, looked up by the step's "type" string. Polled kinds answer Poll, event
/// kinds answer OnTalked; the default members let a kind implement only what it cares about.
/// </summary>
public interface IMissionStepKind
{
    string Type { get; }

    /// <summary>Checked every frame while the step is current; true when it is done.</summary>
    bool Poll(MissionStep step, IWorldQuery world) => false;

    /// <summary>The player has finished talking to npcId; true when that completes the step.</summary>
    bool OnTalked(MissionStep step, string npcId, string giver) => false;

    /// <summary>A message when a character dies during this step, or null when the kind does not care.</summary>
    string? OnNpcKilled(MissionStep step, INpcView npc, IWorldQuery world) => null;

    /// <summary>Where the HUD points for this step, or null.</summary>
    Vector3? Marker(MissionStep step, IWorldQuery world, string giver);
}
