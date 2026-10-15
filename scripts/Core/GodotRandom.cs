using Godot;
using Veld.Sim;

namespace Veld.Core;

/// <summary>The engine's global generator behind IRandom.</summary>
public sealed class GodotRandom : IRandom
{
    public int Range(int minInclusive, int maxInclusive) => GD.RandRange(minInclusive, maxInclusive);

    public float Range(float min, float max) => (float)GD.RandRange(min, max);
}
