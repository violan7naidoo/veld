using System.Collections.Generic;
using Veld.Sim;
using Xunit;

namespace Veld.Tests.Core;

public class PurseTests
{
    [Fact]
    public void Adding_raises_Changed_with_the_new_total()
    {
        var purse = new Purse(300);
        var changes = new List<int>();
        purse.Changed += total => changes.Add(total);
        IEconomy economy = purse;

        economy.Add(200);

        Assert.Equal(500, economy.Pence);
        Assert.Equal(new[] { 500 }, changes);
    }

    [Fact]
    public void Spending_refuses_when_short_and_charges_when_not()
    {
        var purse = new Purse(100);
        var changes = new List<int>();
        purse.Changed += total => changes.Add(total);

        Assert.False(purse.TrySpend(150));
        Assert.Equal(100, purse.Pence);
        Assert.Empty(changes);

        Assert.True(purse.TrySpend(60));
        Assert.Equal(40, purse.Pence);
        Assert.Equal(new[] { 40 }, changes);
    }

    [Fact]
    public void Restoring_sets_the_total_and_says_so()
    {
        var purse = new Purse(300);
        var changes = new List<int>();
        purse.Changed += total => changes.Add(total);

        purse.Restore(1200);

        Assert.Equal(1200, purse.Pence);
        Assert.Equal(new[] { 1200 }, changes);
    }
}
