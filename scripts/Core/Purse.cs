using System;

namespace Veld.Core;

/// <summary>The player's money. Changed fires with the new total after every change, including a restore.</summary>
public sealed class Purse : IEconomy
{
    public Purse(int pence) => Pence = pence;

    public int Pence { get; private set; }

    public event Action<int>? Changed;

    public void Add(int pence)
    {
        Pence += pence;
        Changed?.Invoke(Pence);
    }

    public bool TrySpend(int pence)
    {
        if (Pence < pence) return false;
        Add(-pence);
        return true;
    }

    public void Restore(int pence)
    {
        Pence = pence;
        Changed?.Invoke(Pence);
    }
}
