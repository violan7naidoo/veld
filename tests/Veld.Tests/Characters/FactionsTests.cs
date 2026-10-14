using Veld.Characters;
using Xunit;

namespace Veld.Tests.Characters;

/// <summary>One class for the same reason as MoneyTests: Factions.Modern is a static era flag.</summary>
public class FactionsTests
{
    [Theory]
    [InlineData("british", Faction.British)]
    [InlineData("BOER", Faction.Boer)]
    [InlineData("Outlaw", Faction.Outlaw)]
    [InlineData("civilian", Faction.Civilian)]
    [InlineData("gang", Faction.Civilian)]
    [InlineData("", Faction.Civilian)]
    public void Parses_faction_names_from_data_case_insensitively(string name, Faction expected)
    {
        Assert.Equal(expected, Factions.Parse(name));
    }

    [Fact]
    public void Parses_a_missing_name_as_civilian()
    {
        Assert.Equal(Faction.Civilian, Factions.Parse(null!));
    }

    [Theory]
    [InlineData(true, Faction.British, "Police")]
    [InlineData(false, Faction.British, "British Army")]
    [InlineData(true, Faction.Outlaw, "Gang")]
    [InlineData(false, Faction.Outlaw, "Outlaw")]
    [InlineData(true, Faction.Civilian, "Resident")]
    [InlineData(false, Faction.Boer, "Burgher")]
    public void Labels_a_faction_for_the_era(bool modern, Faction faction, string expected)
    {
        Factions.Modern = modern;
        Assert.Equal(expected, Factions.Label(faction));
    }
}
