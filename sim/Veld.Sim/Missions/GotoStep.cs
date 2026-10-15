using Godot;

namespace Veld.Sim.Missions;

/// <summary>Done when the player stands within Radius metres of Position.</summary>
public sealed class GotoStep : IMissionStepKind
{
    public string Type => "goto";

    public bool Poll(MissionStep step, IWorldQuery world)
    {
        var player = world.Player;
        if (player == null) return false;
        var target = DataLoader.XZ(step.Position);
        return new Vector2(player.Position.X, player.Position.Z).DistanceTo(target) <= step.Radius;
    }

    public Vector3? Marker(MissionStep step, IWorldQuery world, string giver) => world.Ground(DataLoader.XZ(step.Position));
}
