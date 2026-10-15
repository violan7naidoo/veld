using System.Collections.Generic;
using Godot;
using Veld.Sim;
using Veld.Sim.Characters;

namespace Veld.Tests.Fakes;

/// <summary>A body for a brain in tests: what the world sees of the character is what its brain decided.</summary>
public sealed class FakeBody : INpcView
{
    public NpcBrain Brain { get; set; } = null!;
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Group { get; init; } = "";
    public Vector3 Position { get; set; }
    public List<Vector3> AlertedFrom { get; } = new();

    public Faction Faction => Brain.Faction;
    public bool IsDead => Brain.IsDead;
    public bool Provoked { get => Brain.Provoked; set => Brain.Provoked = value; }

    public void Alert(Vector3 from)
    {
        AlertedFrom.Add(from);
        Brain.Alert(from);
    }
}
