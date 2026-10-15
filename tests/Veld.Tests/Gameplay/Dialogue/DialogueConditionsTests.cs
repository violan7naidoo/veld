using System.Collections.Generic;
using Godot;
using Veld.Sim;
using Veld.Sim.Dialogue;
using Veld.Sim.Missions;
using Veld.Tests.Fakes;
using Xunit;

namespace Veld.Tests.Gameplay.Dialogue;

public class DialogueConditionsTests
{
    private readonly FakeWorld _world = new();
    private readonly FakeEconomy _economy = new();
    private readonly FakeNotifier _notifier = new();
    private readonly FakeClock _clock = new();
    private readonly FakeWanted _wanted = new();
    private readonly MissionManager _missions;
    private readonly DialogueContext _context;
    private readonly DialogueRegistry _registry = DialogueRegistry.Builtin();

    public DialogueConditionsTests()
    {
        var parcel = new MissionData
        {
            Id = "parcel", Title = "Aunty Devi's Parcel", Giver = "aunty_devi",
            Steps =
            {
                new MissionStep { Type = "goto", Objective = "Take the parcel to the school", Position = new[] { 445f, 905f }, Radius = 45f },
                new MissionStep { Type = "talk", Objective = "Hand the parcel to Mr. Govender", Target = "mr_govender" },
            },
        };
        _missions = new MissionManager(new GameData { Missions = { parcel } }, _world, _economy, _notifier);
        var devi = new FakeNpc { Id = "aunty_devi", Name = "Aunty Devi" };
        _context = new DialogueContext(_missions, _wanted, _world, _economy, _notifier, _clock, devi);
    }

    /// <summary>Evaluates one term exactly as the dialogue system will: parse it, look the name up, ask.</summary>
    private bool Holds(string term)
    {
        var parsed = Assert.Single(DialogueTerm.Parse(term));
        IDialogueCondition condition = _registry.FindCondition(parsed.Name)!;
        return condition.Holds(parsed.Args, _context);
    }

    [Fact]
    public void Mission_available_holds_before_the_mission_starts_and_mission_active_after()
    {
        Assert.True(Holds("mission_available:parcel"));
        Assert.False(Holds("mission_active:parcel"));

        _missions.Start("parcel");

        Assert.False(Holds("mission_available:parcel"));
        Assert.True(Holds("mission_active:parcel"));
    }

    [Fact]
    public void Mission_done_holds_once_the_mission_is_on_the_completed_list()
    {
        Assert.False(Holds("mission_done:parcel"));

        _missions.Restore(new List<string> { "parcel" }, null, 0);

        Assert.True(Holds("mission_done:parcel"));
    }

    [Theory]
    [InlineData("mission_available")]
    [InlineData("mission_active")]
    [InlineData("mission_done")]
    public void A_mission_condition_without_a_mission_name_is_false(string name)
    {
        _missions.Start("parcel");

        Assert.False(Holds(name));
    }

    [Fact]
    public void Mission_step_holds_only_while_the_mission_is_on_that_step()
    {
        Assert.False(Holds("mission_step:parcel:0"));

        _missions.Start("parcel");
        Assert.True(Holds("mission_step:parcel:0"));
        Assert.False(Holds("mission_step:parcel:1"));

        _world.FakePlayerView!.Position = new Vector3(445f, 0f, 905f);
        _missions.Update(0.016f);
        Assert.True(Holds("mission_step:parcel:1"));
        Assert.False(Holds("mission_step:parcel:0"));

        Assert.False(Holds("mission_step:parcel:one"));
        Assert.False(Holds("mission_step:parcel"));
    }

    [Fact]
    public void Wanted_and_not_wanted_follow_the_level()
    {
        _wanted.Level = 1;
        Assert.True(Holds("wanted"));
        Assert.False(Holds("not_wanted"));

        _wanted.Level = 0;
        Assert.False(Holds("wanted"));
        Assert.True(Holds("not_wanted"));
    }

    [Fact]
    public void Money_holds_at_the_amount_and_not_one_below()
    {
        _economy.Pence = 4500;
        Assert.True(Holds("money:4500"));

        _economy.Pence = 4499;
        Assert.False(Holds("money:4500"));

        Assert.False(Holds("money:lots"));
        Assert.False(Holds("money"));
    }
}
