using System.Collections.Generic;
using Godot;
using Veld.Characters;
using Veld.Core;

namespace Veld.Tests.Fakes;

public sealed class FakeNpc : INpcView
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Group { get; set; } = "";
    public Faction Faction { get; set; } = Faction.Civilian;
    public bool IsDead { get; set; }
    public Vector3 Position { get; set; }
    public bool Provoked { get; set; }
    public List<Vector3> AlertedFrom { get; } = new();

    public void Alert(Vector3 from) => AlertedFrom.Add(from);
}
