using System.Collections.Generic;
using Veld.Core;

namespace Veld.Tests.Fakes;

public sealed class FakeEconomy : IEconomy
{
    public int Pence { get; set; }
    public List<int> Added { get; } = new();

    public void Add(int pence)
    {
        Pence += pence;
        Added.Add(pence);
    }

    public bool TrySpend(int pence)
    {
        if (Pence < pence) return false;
        Add(-pence);
        return true;
    }
}
