using Godot;
using Veld.Sim;
using Veld.Sim.Characters;
using Veld.Tests.Fakes;
using Xunit;

namespace Veld.Tests.Sim.Characters;

/// <summary>
/// In the "era flags" collection: loot and mission completion format money through a static era flag.
/// Each brain gets a FakeBody standing in the fake world, so the systems see what the brain decided.
/// </summary>
[Collection("era flags")]
public class NpcBrainTests
{
    private readonly FakeWorld _world = new();
    private readonly FakeNotifier _notifier = new();
    private readonly FakeRandom _random = new();
    private readonly Simulation _sim;

    public NpcBrainTests() => _sim = new Simulation(Data(), _world, _notifier, new FakeDialogueView(), _random);

    /// <summary>A modern map with 5.00 to start, one pistol, and a clean-up job: deal with the thieves.</summary>
    private static GameData Data() => new()
    {
        World = new WorldData { Era = "modern", StartMoney = 500 },
        Weapons = { ["pistol"] = new WeaponData { Id = "pistol", Range = 60 } },
        Missions =
        {
            new MissionData
            {
                Id = "cleanup", Title = "Clean-up", Giver = "coach_naidoo",
                Steps = { new MissionStep { Type = "kill_group", Objective = "Deal with the thieves", Group = "thieves" } },
            },
        },
    };

    private NpcBrain Brain(string faction, string weapon = "", bool hostile = false, string group = "", string id = "n1")
    {
        var data = new NpcData { Id = id, Name = "Someone", Faction = faction, Weapon = weapon, Hostile = hostile, Group = group };
        var body = new FakeBody { Id = id, Name = data.Name, Group = group, Position = new Vector3(5f, 0f, 5f) };
        body.Brain = _sim.CreateBrain(data, body);
        _world.Bodies.Add(body);
        return body.Brain;
    }

    private IPlayerView Player => _world.FakePlayerView!;

    [Fact]
    public void Outlaws_are_hostile_and_civilians_are_not()
    {
        Assert.True(Brain("outlaw").IsHostile);
        Assert.False(Brain("civilian", id: "n2").IsHostile);
    }

    [Fact]
    public void Anyone_flagged_hostile_in_the_data_is_hostile()
    {
        Assert.True(Brain("civilian", hostile: true).IsHostile);
    }

    [Fact]
    public void Police_turn_hostile_when_the_player_is_wanted()
    {
        var officer = Brain("british", "pistol");
        Assert.False(officer.IsHostile);

        _sim.Wanted.SetLevel(1);

        Assert.True(officer.IsHostile);
    }

    [Fact]
    public void Residents_turn_hostile_when_standing_falls_below_minus_two()
    {
        var resident = Brain("boer");
        _sim.Reputation.Lose(2);
        Assert.False(resident.IsHostile);

        _sim.Reputation.Lose(1);

        Assert.True(resident.IsHostile);
    }

    [Fact]
    public void A_provoked_armed_npc_is_hostile_and_an_unarmed_one_is_not()
    {
        var armed = Brain("civilian", "pistol");
        var unarmed = Brain("civilian", id: "n2");

        armed.Provoked = true;
        unarmed.Provoked = true;

        Assert.True(armed.IsHostile);
        Assert.False(unarmed.IsHostile);
    }

    [Fact]
    public void A_hit_by_the_player_provokes_the_whole_group_and_costs_standing()
    {
        var stallholder = Brain("civilian", group: "market");
        var neighbour = Brain("civilian", group: "market", id: "n2");

        stallholder.TakeDamage(10, Player, Vector3.Zero);

        Assert.True(stallholder.Provoked);
        Assert.True(neighbour.Provoked);
        Assert.Equal(-1, _sim.Reputation.Value);

        neighbour.TakeDamage(100, Player, Vector3.Zero);

        Assert.Equal(-3, _sim.Reputation.Value);
    }

    [Fact]
    public void A_hit_sends_the_unarmed_fleeing_while_the_armed_stand_and_the_attack_is_reported()
    {
        var resident = Brain("boer");
        var officer = Brain("british", "pistol", id: "n2");
        resident.Settle(Vector3.Zero);
        officer.Settle(Vector3.Zero);

        officer.TakeDamage(10, Player, Vector3.Zero);

        Assert.Equal(NpcState.Idle, officer.State);
        Assert.Equal(1, _sim.Wanted.Level);

        resident.TakeDamage(10, Player, Vector3.Zero);

        Assert.Equal(NpcState.Flee, resident.State);
        Assert.Equal(2, _sim.Wanted.Level);   // the officer, 110 m or closer, witnessed it
    }

    [Fact]
    public void A_killing_blow_kills_and_the_next_tick_completes_a_kill_group_mission()
    {
        var first = Brain("outlaw", group: "thieves", id: "thief_1");
        var second = Brain("outlaw", group: "thieves", id: "thief_2");
        Assert.True(_sim.Missions.Start("cleanup"));

        Assert.True(first.TakeDamage(100, Player, Vector3.Zero));
        _sim.Tick(1f / 60f);

        Assert.True(first.IsDead);
        Assert.Equal(0, first.Health);
        Assert.Equal(NpcState.Dead, first.State);
        Assert.DoesNotContain("cleanup", _sim.Missions.Completed);

        Assert.True(second.TakeDamage(100, Player, Vector3.Zero));
        _sim.Tick(1f / 60f);

        Assert.Contains("cleanup", _sim.Missions.Completed);
    }

    [Fact]
    public void The_dead_are_not_hostile_and_take_no_more_damage()
    {
        var outlaw = Brain("outlaw");
        outlaw.TakeDamage(100, Player, Vector3.Zero);

        Assert.False(outlaw.IsHostile);
        Assert.False(outlaw.TakeDamage(10, Player, Vector3.Zero));
        Assert.Equal(0, outlaw.Health);
    }

    [Fact]
    public void An_alert_sends_unarmed_residents_running_and_leaves_officers_and_outlaws()
    {
        var resident = Brain("boer");
        var officer = Brain("british", "pistol", id: "n2");
        var outlaw = Brain("outlaw", id: "n3");
        foreach (var b in new[] { resident, officer, outlaw }) b.Settle(Vector3.Zero);

        foreach (var b in new[] { resident, officer, outlaw }) b.Alert(new Vector3(20f, 0f, 0f));

        Assert.Equal(NpcState.Flee, resident.State);
        Assert.Equal(NpcState.Idle, officer.State);
        Assert.Equal(NpcState.Idle, outlaw.State);
    }

    [Fact]
    public void Loot_pays_by_faction_costs_standing_for_residents_and_only_once()
    {
        _random.Fraction = 0f;
        var outlaw = Brain("outlaw");
        var resident = Brain("civilian", id: "n2");
        outlaw.TakeDamage(100, null, Vector3.Zero);
        resident.TakeDamage(100, null, Vector3.Zero);

        Assert.Equal(18, outlaw.Loot());
        Assert.Equal(518, _sim.Purse.Pence);
        Assert.Equal(0, _sim.Reputation.Value);
        Assert.Contains(new Vector3(5f, 0f, 5f), _notifier.CoinsAt);
        Assert.Contains(_notifier.Messages, m => m.StartsWith("You find ") && m.EndsWith(" on Someone."));

        Assert.Equal(1, resident.Loot());
        Assert.Equal(519, _sim.Purse.Pence);
        Assert.Equal(-1, _sim.Reputation.Value);

        Assert.Equal(0, resident.Loot());
        Assert.Equal(519, _sim.Purse.Pence);
    }

    [Fact]
    public void Gunfire_is_reported_so_nearby_townsfolk_scatter()
    {
        var officer = Brain("british", "pistol");
        var bystander = new FakeNpc { Id = "b1", Position = new Vector3(10f, 0f, 5f) };
        _world.Characters.Add(bystander);

        officer.ReportGunfire();

        Assert.Contains(new Vector3(5f, 0f, 5f), bystander.AlertedFrom);
    }
    [Fact]
    public void MarkDeadSilently_leaves_the_npc_dead_and_looted_with_no_effects()
    {
        var resident = Brain("civilian");

        resident.MarkDeadSilently();

        Assert.True(resident.IsDead);
        Assert.Equal(NpcState.Dead, resident.State);
        Assert.True(resident.Looted);
        Assert.Equal(0, resident.Loot());
        Assert.Empty(_notifier.Messages);
        Assert.Equal(0, _sim.Reputation.Value);
    }
}
