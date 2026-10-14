using Godot;
using Veld.Characters;

namespace Veld.Core;

/// <summary>What the gameplay systems may know and change about one character, live or faked.</summary>
public interface INpcView
{
    string Id { get; }
    string Name { get; }
    string Group { get; }
    Faction Faction { get; }
    bool IsDead { get; }
    Vector3 Position { get; }
    bool Provoked { get; set; }
    void Alert(Vector3 from);
}
