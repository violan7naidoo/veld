using Veld.Sim.Dialogue;
using Xunit;

namespace Veld.Tests.Gameplay.Dialogue;

public class DialogueRegistryTests
{
    private sealed class Never : IDialogueCondition
    {
        public string Name => "never";
        public bool Holds(string[] args, DialogueContext context) => false;
    }

    private sealed class OtherNever : IDialogueCondition
    {
        public string Name => "never";
        public bool Holds(string[] args, DialogueContext context) => true;
    }

    private sealed class Dance : IDialogueAction
    {
        public string Name => "dance";
        public bool Apply(string[] args, DialogueContext context) => false;
    }

    [Fact]
    public void Adding_makes_a_condition_and_an_action_findable_and_replaces_a_namesake()
    {
        var registry = new DialogueRegistry().Add(new Never()).Add(new Dance());

        Assert.IsType<Never>(registry.FindCondition("never"));
        Assert.IsType<Dance>(registry.FindAction("dance"));
        Assert.Null(registry.FindCondition("dance"));
        Assert.Null(registry.FindAction("never"));

        registry.Add(new OtherNever());
        Assert.IsType<OtherNever>(registry.FindCondition("never"));
    }

    [Fact]
    public void Builtin_knows_the_seven_conditions_and_nine_actions_and_nothing_else()
    {
        var registry = DialogueRegistry.Builtin();
        var conditions = new[] { "mission_available", "mission_active", "mission_step", "mission_done", "wanted", "not_wanted", "money" };
        var actions = new[] { "start_mission", "talk_done", "give_money", "take_money", "clear_wanted", "give_ammo", "heal", "sleep", "end" };

        Assert.DoesNotContain(conditions, name => registry.FindCondition(name) == null);
        Assert.DoesNotContain(actions, name => registry.FindAction(name) == null);
        Assert.Null(registry.FindCondition("weather"));
        Assert.Null(registry.FindAction("dance"));
    }
}
