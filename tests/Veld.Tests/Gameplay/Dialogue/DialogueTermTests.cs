using Veld.Sim.Dialogue;
using Xunit;

namespace Veld.Tests.Gameplay.Dialogue;

public class DialogueTermTests
{
    [Fact]
    public void A_comma_list_splits_into_named_terms_with_their_arguments()
    {
        var terms = DialogueTerm.Parse("take_money:4500,heal");

        Assert.Equal(2, terms.Count);
        Assert.Equal("take_money", terms[0].Name);
        Assert.Equal(new[] { "4500" }, terms[0].Args);
        Assert.Equal("heal", terms[1].Name);
        Assert.Empty(terms[1].Args);
    }

    [Fact]
    public void A_term_is_trimmed_and_keeps_every_argument()
    {
        var term = Assert.Single(DialogueTerm.Parse(" mission_step:parcel:1 "));

        Assert.Equal("mission_step", term.Name);
        Assert.Equal(new[] { "parcel", "1" }, term.Args);
    }

    [Fact]
    public void Blank_input_gives_no_terms_and_blank_entries_are_skipped()
    {
        Assert.Empty(DialogueTerm.Parse(""));
        Assert.Equal("end", Assert.Single(DialogueTerm.Parse("end,,")).Name);
    }
}
