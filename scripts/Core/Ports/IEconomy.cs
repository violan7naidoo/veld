namespace Veld.Core;

/// <summary>The player's purse as the gameplay systems see it: pence on a 1901 map, cents on a modern one.</summary>
public interface IEconomy
{
    int Pence { get; }
    void Add(int pence);
    bool TrySpend(int pence);
}
