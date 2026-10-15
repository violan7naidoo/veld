using System.Collections.Generic;
using Godot;
using Veld.Sim;
using Veld.Sim.Dialogue;
using Veld.Sim.Missions;
using Veld.Tests.Fakes;
using Xunit;

namespace Veld.Tests.Gameplay;

/// <summary>In the "era flags" collection: the fine message goes through Money.Format.</summary>
[Collection("era flags")]
public class DialogueSystemTests
{
    private readonly FakeWorld _world = new();
    private readonly FakeEconomy _economy = new();
    private readonly FakeNotifier _notifier = new();
    private readonly FakeClock _clock = new();
    private readonly FakeWanted _wanted = new();
    private readonly FakeDialogueView _view = new();

    private static readonly FakeNpc Devi = new() { Id = "aunty_devi", Name = "Aunty Devi", Position = new Vector3(28f, 0f, -700f) };

    private static MissionData Parcel() => new()
    {
        Id = "parcel", Title = "Aunty Devi's Parcel", Giver = "aunty_devi", RewardPence = 15000,
        Steps = { new MissionStep { Type = "talk", Objective = "Hand over the parcel", Target = "aunty_devi" } },
    };

    private static DialogueChoice Choice(string text, string condition = "", string action = "", string next = "") =>
        new() { Text = text, Condition = condition, Action = action, Next = next };

    /// <summary>A cut-down superette conversation: food for money, a job while the parcel is open, a fine while wanted.</summary>
    private static DialogueData Superette() => new()
    {
        Speaker = "Aunty Devi",
        Start = "start",
        Nodes =
        {
            ["start"] = new DialogueNode
            {
                Text = "Hello my dear, what can I get you?",
                Choices =
                {
                    Choice("A quarter bunny (R45).", condition: "money:4500", action: "take_money:4500,heal", next: "ate"),
                    Choice("I need a job, Aunty.", condition: "mission_available:parcel", action: "start_mission:parcel", next: "job"),
                    Choice("I'd like to sort out my record.", condition: "wanted", next: "fine"),
                    Choice("Just looking, thanks.", action: "end"),
                },
            },
            ["ate"] = new DialogueNode { Text = "Eat, eat.", Choices = { Choice("Thanks, Aunty.", action: "end") } },
            ["job"] = new DialogueNode { Text = "Take this parcel.", Choices = { Choice("On my way.", action: "end") } },
            ["fine"] = new DialogueNode { Text = "R500 and it goes away.", Choices = { Choice("Pay R500.", condition: "money:50000", action: "clear_wanted:50000", next: "fined"), Choice("No.", action: "end") } },
            ["fined"] = new DialogueNode { Text = "Paid.", Choices = { Choice("Yes, officer.", action: "end") } },
        },
    };

    /// <summary>One node, one choice, so a single action or condition can be exercised on its own.</summary>
    private static DialogueData Single(string action = "", string condition = "", string next = "", string speaker = "Aunty Devi") => new()
    {
        Speaker = speaker,
        Start = "start",
        Nodes =
        {
            ["start"] = new DialogueNode { Text = "Well?", Choices = { Choice("Do it.", condition, action, next) } },
            ["after"] = new DialogueNode { Text = "Done.", Choices = { Choice("Bye.", action: "end") } },
        },
    };

    private DialogueSystem System(DialogueData dialogue, params MissionData[] missions)
    {
        var data = new GameData { Missions = new List<MissionData>(missions), Dialogues = { ["superette"] = dialogue } };
        var missionManager = new MissionManager(data, _world, _economy, _notifier);
        return new DialogueSystem(data, missionManager, _wanted, _world, _economy, _notifier, _clock, _view);
    }

    private DialogueSystem Started(DialogueData dialogue, params MissionData[] missions)
    {
        var dialogue_ = System(dialogue, missions);
        dialogue_.Start("superette", Devi);
        return dialogue_;
    }

    [Fact]
    public void Start_opens_the_start_node_with_the_choices_whose_conditions_hold()
    {
        _economy.Pence = 4000;
        _wanted.Level = 0;
        var dialogue = System(Superette(), Parcel());

        dialogue.Start("superette", Devi);

        var opened = Assert.Single(_view.Openings);
        Assert.Equal("Aunty Devi", opened.Speaker);
        Assert.Equal("Hello my dear, what can I get you?", opened.Text);
        Assert.Equal(new[] { "I need a job, Aunty.", "Just looking, thanks." }, opened.Choices);
        Assert.True(dialogue.IsActive);
    }

    [Fact]
    public void Enough_money_and_a_wanted_level_unlock_their_choices()
    {
        _economy.Pence = 5000;
        _wanted.Level = 1;
        Started(Superette(), Parcel());

        Assert.Equal(new[] { "A quarter bunny (R45).", "I need a job, Aunty.", "I'd like to sort out my record.", "Just looking, thanks." }, _view.Current!.Choices);
    }

    [Fact]
    public void Start_with_an_unknown_dialogue_says_so_and_opens_nothing()
    {
        var dialogue = System(Superette());

        dialogue.Start("hotel", Devi);

        Assert.Empty(_view.Openings);
        Assert.False(dialogue.IsActive);
        Assert.Equal(new[] { "Aunty Devi has nothing to say. (missing dialogue 'hotel')" }, _notifier.Messages);
    }

    [Fact]
    public void The_speaker_falls_back_to_the_character_name()
    {
        Started(Single(speaker: ""));

        Assert.Equal("Aunty Devi", _view.Current!.Speaker);
    }

    [Fact]
    public void Two_conditions_joined_by_a_comma_must_both_hold()
    {
        _economy.Pence = 100;
        _wanted.Level = 1;
        var dialogue = Started(Single(condition: "not_wanted,money:100"));

        Assert.Equal(new[] { "(Leave)" }, _view.Current!.Choices);
        dialogue.Choose(0);
        Assert.Equal(1, _view.Closes);
        Assert.False(dialogue.IsActive);
    }

    [Fact]
    public void A_choice_with_a_next_node_opens_it_and_without_one_closes()
    {
        var dialogue = Started(Single(next: "after"));
        dialogue.Choose(0);
        Assert.Equal("Done.", _view.Current!.Text);

        dialogue.Choose(0);
        Assert.Equal(1, _view.Closes);
        Assert.False(dialogue.IsActive);
    }

    [Fact]
    public void An_unknown_next_node_or_an_out_of_range_choice_closes()
    {
        var dialogue = Started(Single(next: "nowhere"));
        dialogue.Choose(0);
        Assert.Equal(1, _view.Closes);

        dialogue = Started(Single(next: "after"));
        dialogue.Choose(7);
        Assert.Equal(2, _view.Closes);
    }

    [Fact]
    public void Buying_food_charges_and_heals()
    {
        _economy.Pence = 5000;
        var dialogue = Started(Superette());

        dialogue.Choose(0);

        Assert.Equal(500, _economy.Pence);
        Assert.Equal(1, _world.FakePlayerView!.Heals);
        Assert.Equal("Eat, eat.", _view.Current!.Text);
    }

    [Fact]
    public void Giving_money_pays_and_drops_coins_at_the_character()
    {
        var dialogue = Started(Single(action: "give_money:200"));

        dialogue.Choose(0);

        Assert.Equal(new[] { 200 }, _economy.Added);
        Assert.Equal(new[] { new Vector3(28f, 0f, -700f) }, _notifier.CoinsAt);
    }

    [Fact]
    public void Giving_ammo_tops_up_the_named_weapon()
    {
        var dialogue = Started(Single(action: "give_ammo:pistol:12"));

        dialogue.Choose(0);

        Assert.Equal(new[] { ("pistol", 12) }, _world.FakePlayerView!.AmmoGiven);
    }

    [Fact]
    public void Taking_the_job_starts_the_mission_and_shows_the_next_node()
    {
        var dialogue = Started(Superette(), Parcel());

        dialogue.Choose(0);

        Assert.Equal("Take this parcel.", _view.Current!.Text);
        Assert.Contains("New task: Aunty Devi's Parcel", _notifier.Messages);
    }

    [Fact]
    public void Talk_done_reports_this_character_to_the_mission()
    {
        var data = Single(action: "talk_done");
        var gameData = new GameData { Missions = { Parcel() }, Dialogues = { ["superette"] = data } };
        var missions = new MissionManager(gameData, _world, _economy, _notifier);
        var dialogue = new DialogueSystem(gameData, missions, _wanted, _world, _economy, _notifier, _clock, _view);
        missions.Start("parcel");
        dialogue.Start("superette", Devi);

        dialogue.Choose(0);

        Assert.True(missions.IsDone("parcel"));
    }

    [Fact]
    public void Paying_the_fine_clears_the_record_and_says_so()
    {
        Money.Modern = true;
        _economy.Pence = 60000;
        _wanted.Level = 2;
        var dialogue = Started(Single(action: "clear_wanted:50000", next: "after"));

        dialogue.Choose(0);

        Assert.Equal(10000, _economy.Pence);
        Assert.Equal(0, _wanted.Level);
        Assert.Equal(1, _wanted.Clears);
        Assert.Equal(new[] { "Paid R500.00. The complaint is withdrawn." }, _notifier.Messages);
    }

    [Fact]
    public void A_fine_you_cannot_afford_leaves_the_record_alone()
    {
        _economy.Pence = 100;
        _wanted.Level = 2;
        var dialogue = Started(Single(action: "clear_wanted:50000"));

        dialogue.Choose(0);

        Assert.Equal(100, _economy.Pence);
        Assert.Equal(2, _wanted.Level);
        Assert.Equal(new[] { "You cannot afford the fine." }, _notifier.Messages);
    }

    [Fact]
    public void Sleeping_advances_the_clock_by_the_stated_hours()
    {
        var dialogue = Started(Single(action: "sleep:8"));

        dialogue.Choose(0);

        Assert.Equal(new[] { 8f }, _clock.Advanced);
    }

    [Fact]
    public void Closing_the_box_from_outside_ends_the_dialogue_and_is_harmless_when_none_is_open()
    {
        var dialogue = Started(Single());
        dialogue.OnBoxClosed();
        Assert.False(dialogue.IsActive);
        Assert.Equal(1, _view.Closes);

        dialogue.OnBoxClosed();
        Assert.Equal(1, _view.Closes);
    }

    /// <summary>A condition no shipped conversation uses, registered by a test to prove the registry is the seam.</summary>
    private sealed class NeverCondition : IDialogueCondition
    {
        public string Name => "never";
        public bool Holds(string[] args, DialogueContext context) => false;
    }

    private sealed class CountingAction : IDialogueAction
    {
        public int Runs { get; private set; }
        public string Name => "count";
        public bool Apply(string[] args, DialogueContext context)
        {
            Runs++;
            return false;
        }
    }

    [Fact]
    public void A_condition_and_an_action_added_to_the_registry_drive_a_choice()
    {
        var counting = new CountingAction();
        var registry = DialogueRegistry.Builtin().Add(new NeverCondition()).Add(counting);
        var tree = new DialogueData
        {
            Speaker = "Aunty Devi", Start = "start",
            Nodes = { ["start"] = new DialogueNode { Text = "Well?", Choices = { Choice("Hidden.", condition: "never"), Choice("Count it.", action: "count") } } },
        };
        var data = new GameData { Dialogues = { ["superette"] = tree } };
        var missions = new MissionManager(data, _world, _economy, _notifier);
        var dialogue = new DialogueSystem(data, missions, _wanted, _world, _economy, _notifier, _clock, _view, registry);
        dialogue.Start("superette", Devi);

        Assert.Equal(new[] { "Count it." }, _view.Current!.Choices);
        dialogue.Choose(0);

        Assert.Equal(1, counting.Runs);
        Assert.False(dialogue.IsActive);
    }

    [Fact]
    public void An_unknown_condition_or_action_is_reported_when_the_conversation_starts()
    {
        var dialogue = Started(Single(condition: "weather:rain", action: "dance"));

        Assert.Equal(new[] { "Dialogue 'superette' uses an unknown condition 'weather'.", "Dialogue 'superette' uses an unknown action 'dance'." }, _notifier.Messages);
        Assert.Equal(new[] { "Do it." }, _view.Current!.Choices);

        dialogue.Choose(0);

        Assert.False(dialogue.IsActive);
        Assert.Equal(1, _view.Closes);
    }
}
