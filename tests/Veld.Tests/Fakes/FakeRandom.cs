using System;
using Veld.Sim;

namespace Veld.Tests.Fakes;

/// <summary>Rolls land at Fraction of the way through the range: 0 is the low bound, 1 the high, 0.5 the middle.</summary>
public sealed class FakeRandom : IRandom
{
    public float Fraction { get; set; } = 0.5f;

    public int Range(int minInclusive, int maxInclusive) => minInclusive + (int)MathF.Round((maxInclusive - minInclusive) * Fraction);

    public float Range(float min, float max) => min + (max - min) * Fraction;
}
