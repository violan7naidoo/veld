namespace Veld.Core;

/// <summary>Standing with the residents: negative after harming them; they turn hostile below -2.</summary>
public sealed class Reputation
{
    public int Value { get; private set; }

    public bool ResidentsHostile => Value < -2;

    public void Lose(int points) => Value -= points;

    public void Restore(int value) => Value = value;
}
