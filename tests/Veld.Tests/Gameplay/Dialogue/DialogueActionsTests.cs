using Godot;
using Veld.Sim;
using Veld.Sim.Dialogue;
using Veld.Sim.Missions;
using Veld.Tests.Fakes;
using Xunit;

namespace Veld.Tests.Gameplay.Dialogue;

/// <summary>In the "era flags" collection: the fine message goes through Money.Format.</summary>
[Collection("era flags")]
public class DialogueActionsTests
{
    private readonly FakeWorld _world = new();
    private readonly FakeEconomy _economy = new();
    private readonly FakeNotifier _notifier = new();
    private readonly FakeClock _clock = new();
    private readonly FakeWanted _wanted = new();
    private readonly MissionManager _missions;
    private readonly DialogueContext _context;
    private readonly DialogueRegistry _registry = DialogueRegistry.Builtin();

    public DialogueActionsTests()
    {
        var parcel = new MissionData
        {
            Id = "parcel", Title = "Aunty Devi's Parcel", Giver = "aunty_devi",
            Steps = { new MissionStep { Type = "talk", Objective = "Hand over the parcel", Target = "aunty_devi" } },
        };
        _missions = new MissionManager(new GameData { Missions = { parcel } }, _world, _economy, _notifier);
        var devi = new FakeNpc { Id = "aunty_devi", Name = "Aunty Devi", Position = new Vector3(28f, 0f, -700f) };
        _context = new DialogueContext(_missions, _wanted, _world, _economy, _notifier, _clock, devi);
    }

    /// <summary>Runs one term exactly as the dialogue system will: parse it, look the name up, apply. Returns whether it ends the conversation.</summary>
    private bool Apply(string term)
    {
        var parsed = Assert.Single(DialogueTerm.Parse(term));
        IDialogueAction action = _registry.FindAction(parsed.Name)!;
        return action.Apply(parsed.Args, _context);
    }

    [Fact]
    public void Start_mission_starts_the_named_mission_and_nothing_without_a_name()
    {
        Assert.False(Apply("start_mission"));
        Assert.Null(_missions.Active);
        Assert.Empty(_notifier.Messages);

        Assert.False(Apply("start_mission:parcel"));

        Assert.Equal("parcel", _missions.Active?.Id);
        Assert.Equal(new[] { "New task: Aunty Devi's Parcel" }, _notifier.Messages);
    }

    [Fact]
    public void Talk_done_reports_the_character_in_the_conversation_to_the_mission()
    {
        _missions.Start("parcel");

        Assert.False(Apply("talk_done"));

        Assert.True(_missions.IsDone("parcel"));
    }

    [Fact]
    public void Give_ammo_tops_up_the_named_weapon_and_needs_both_arguments()
    {
        Assert.False(Apply("give_ammo:pistol"));
        Assert.Empty(_world.FakePlayerView!.AmmoGiven);

        Assert.False(Apply("give_ammo:pistol:12"));

        Assert.Equal(new[] { ("pistol", 12) }, _world.FakePlayerView!.AmmoGiven);
    }

    [Fact]
    public void Heal_heals_the_player_once()
    {
        Assert.False(Apply("heal"));

        Assert.Equal(1, _world.FakePlayerView!.Heals);
    }

    [Fact]
    public void Sleep_advances_the_clock_by_the_stated_hours()
    {
        Assert.False(Apply("sleep:8"));

        Assert.Equal(new[] { 8f }, _clock.Advanced);
    }

    [Fact]
    public void End_asks_to_close_the_conversation_and_touches_nothing()
    {
        Assert.True(Apply("end"));

        Assert.Empty(_notifier.Messages);
        Assert.Empty(_economy.Added);
        Assert.Equal(0, _world.FakePlayerView!.Heals);
        Assert.Empty(_clock.Advanced);
    }

    [Fact]
    public void Give_money_pays_and_drops_coins_at_the_character()
    {
        Assert.False(Apply("give_money:200"));

        Assert.Equal(new[] { 200 }, _economy.Added);
        Assert.Equal(new[] { new Vector3(28f, 0f, -700f) }, _notifier.CoinsAt);
    }

    [Fact]
    public void Take_money_charges_only_when_the_player_can_pay()
    {
        _economy.Pence = 5000;
        Assert.False(Apply("take_money:4500"));
        Assert.Equal(500, _economy.Pence);

        _economy.Pence = 100;
        Assert.False(Apply("take_money:4500"));
        Assert.Equal(100, _economy.Pence);
        Assert.Empty(_notifier.Messages);
    }

    [Fact]
    public void Clear_wanted_with_a_fee_charges_clears_the_record_and_says_so()
    {
        Money.Modern = true;
        _economy.Pence = 60000;
        _wanted.Level = 2;

        Assert.False(Apply("clear_wanted:50000"));

        Assert.Equal(10000, _economy.Pence);
        Assert.Equal(0, _wanted.Level);
        Assert.Equal(new[] { "Paid R500.00. The complaint is withdrawn." }, _notifier.Messages);
    }

    [Fact]
    public void Clear_wanted_you_cannot_afford_leaves_the_record_alone()
    {
        _economy.Pence = 100;
        _wanted.Level = 2;

        Assert.False(Apply("clear_wanted:50000"));

        Assert.Equal(100, _economy.Pence);
        Assert.Equal(2, _wanted.Level);
        Assert.Equal(new[] { "You cannot afford the fine." }, _notifier.Messages);
    }

    [Fact]
    public void Clear_wanted_without_a_fee_is_free()
    {
        _wanted.Level = 2;

        Assert.False(Apply("clear_wanted"));

        Assert.Equal(0, _wanted.Level);
        Assert.Empty(_economy.Added);
        Assert.Empty(_notifier.Messages);
    }
}
