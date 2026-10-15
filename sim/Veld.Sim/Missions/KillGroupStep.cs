using Godot;

namespace Veld.Sim.Missions;

/// <summary>Done when every character in Group is dead; the marker follows the nearest one still standing.</summary>
public sealed class KillGroupStep : IMissionStepKind
{
    public string Type => "kill_group";

    public bool Poll(MissionStep step, IWorldQuery world)
    {
        foreach (var n in world.Group(step.Group)) if (!n.IsDead) return false;
        return true;
    }

    public string? OnNpcKilled(MissionStep step, INpcView npc, IWorldQuery world)
    {
        if (npc.Group != step.Group) return null;
        int left = 0;
        foreach (var n in world.Group(step.Group)) if (!n.IsDead) left++;
        return left > 0 ? $"{npc.Name} is down. {left} left." : null;
    }

    public Vector3? Marker(MissionStep step, IWorldQuery world, string giver)
    {
        Vector3? best = null;
        float bestD = float.MaxValue;
        var p = world.Player?.Position ?? Vector3.Zero;
        foreach (var n in world.Group(step.Group))
        {
            if (n.IsDead) continue;
            float d = n.Position.DistanceTo(p);
            if (d < bestD) { bestD = d; best = n.Position; }
        }
        return best;
    }
}
