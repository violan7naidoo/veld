using Veld.Core;
using Xunit;

namespace Veld.Tests.Core;

public class ReputationTests
{
    [Fact]
    public void Residents_turn_hostile_only_below_minus_two()
    {
        var reputation = new Reputation();
        Assert.Equal(0, reputation.Value);
        Assert.False(reputation.ResidentsHostile);

        reputation.Lose(1);
        Assert.Equal(-1, reputation.Value);
        Assert.False(reputation.ResidentsHostile);

        reputation.Lose(1);
        Assert.Equal(-2, reputation.Value);
        Assert.False(reputation.ResidentsHostile);

        reputation.Lose(2);
        Assert.Equal(-4, reputation.Value);
        Assert.True(reputation.ResidentsHostile);
    }

    [Fact]
    public void Restoring_sets_the_standing_outright()
    {
        var reputation = new Reputation();

        reputation.Restore(-5);
        Assert.True(reputation.ResidentsHostile);

        reputation.Restore(0);
        Assert.False(reputation.ResidentsHostile);
    }
}
