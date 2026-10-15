using Godot;
using Veld.Sim;
using Veld.Sim.Characters;
using Veld.Tests.Fakes;
using Xunit;

namespace Veld.Tests.Sim.Characters;

/// <summary>
/// The state machine through Think alone. Bodies stand at (5, 0, 5); the player stands 100 m east unless a
/// test moves them, which is within simulation range and out of sight. One game hour is thirty seconds.
/// </summary>
[Collection("era flags")]
public class NpcBrainThinkTests
{
    private readonly FakeWorld _world = new();
    private readonly FakeRandom _random = new();
    private readonly Simulation _sim;

    public NpcBrainThinkTests() => _sim = new Simulation(Data(), _world, new FakeNotifier(), new FakeDialogueView(), _random);

    private static GameData Data() => new()
    {
        World = new WorldData { Era = "modern" },
        Weapons = { ["pistol"] = new WeaponData { Id = "pistol", Range = 60 } },
    };

    private static readonly Vector3 Here = new(5f, 0f, 5f);
    private static readonly Vector3 FarEast = new(100f, 0f, 0f);

    private NpcBrain Brain(string faction, string weapon = "", bool hostile = false, float wanderRadius = 12f, params float[][] patrol)
    {
        var data = new NpcData { Id = "n1", Name = "Someone", Faction = faction, Weapon = weapon, Hostile = hostile, WanderRadius = wanderRadius };
        foreach (var p in patrol) data.Patrol.Add(p);
        var body = new FakeBody { Id = "n1", Name = "Someone", Position = Here };
        body.Brain = _sim.CreateBrain(data, body);
        _world.Bodies.Add(body);
        body.Brain.Settle(Here);
        return body.Brain;
    }

    private static NpcSenses Senses(Vector3 player, Vector3? position = null, bool canSee = false, float speed = 1f,
        bool magEmpty = false, bool canFire = true, float playerSpeed = 0f, bool mounted = false) =>
        new(position ?? Here, Vector3.Right, player, false, mounted, playerSpeed, () => canSee, speed, magEmpty, canFire);

    [Fact]
    public void Settling_on_a_patrol_route_walks_to_the_first_point()
    {
        var officer = Brain("british", "pistol", patrol: new[] { new[] { 10f, 0f }, new[] { 20f, 0f } });

        var intent = officer.Think(Senses(FarEast), 1f / 60f);

        Assert.Equal(NpcState.Patrol, intent.State);
        Assert.Equal(new Vector3(10f, 0f, 0f), intent.FaceTo);
        Assert.Equal(1.6f, intent.Speed, 3);
    }

    [Fact]
    public void Patrol_moves_on_to_the_next_point_after_waiting_there()
    {
        _random.Fraction = 0f;
        var officer = Brain("british", "pistol", patrol: new[] { new[] { 10f, 0f }, new[] { 20f, 0f } });
        var atFirstPoint = new Vector3(10f, 0f, 0f);

        var waiting = officer.Think(Senses(FarEast, atFirstPoint), 1f / 60f);
        officer.Think(Senses(FarEast, atFirstPoint), 1f);
        var moving = officer.Think(Senses(FarEast, atFirstPoint), 1f / 60f);

        Assert.Equal(0f, waiting.Speed, 3);
        Assert.Equal(new Vector3(20f, 0f, 0f), moving.FaceTo);
        Assert.Equal(1.6f, moving.Speed, 3);
    }

    [Fact]
    public void An_idle_npc_wanders_to_the_corner_of_its_radius_and_idles_again_on_arrival()
    {
        _random.Fraction = 1f;
        var resident = Brain("civilian");

        resident.Think(Senses(FarEast), 4.1f);
        var walking = resident.Think(Senses(FarEast), 1f / 60f);
        var arrived = resident.Think(Senses(FarEast, new Vector3(17f, 0f, 17f)), 1f / 60f);

        Assert.Equal(NpcState.Wander, walking.State);
        Assert.Equal(new Vector3(17f, 0f, 17f), walking.FaceTo);
        Assert.Equal(1.6f, walking.Speed, 3);
        Assert.Equal(NpcState.Idle, arrived.State);
        Assert.Equal(0f, arrived.Speed, 3);
    }

    [Fact]
    public void Beyond_220_m_in_a_passive_state_it_rests()
    {
        var resident = Brain("civilian");

        var intent = resident.Think(Senses(new Vector3(300f, 0f, 0f)), 1f / 60f);

        Assert.True(intent.Rest);
        Assert.Equal(NpcState.Idle, intent.State);
    }

    [Fact]
    public void Hostile_and_armed_with_the_player_in_sight_beyond_engage_range_it_chases()
    {
        var outlaw = Brain("outlaw", "pistol");

        var intent = outlaw.Think(Senses(new Vector3(40f, 0f, 5f), canSee: true), 1f / 60f);

        Assert.Equal(NpcState.Chase, intent.State);
        Assert.Equal(new Vector3(40f, 0f, 5f), intent.FaceTo);
        Assert.Equal(5.2f, intent.Speed, 3);
    }

    [Fact]
    public void Inside_engage_range_it_attacks_facing_the_player_and_fires_when_ready()
    {
        var outlaw = Brain("outlaw", "pistol");
        var player = new Vector3(15f, 0f, 5f);

        var intent = outlaw.Think(Senses(player, canSee: true), 1f / 60f);

        Assert.Equal(NpcState.Attack, intent.State);
        Assert.Equal(player, intent.FaceTo);
        Assert.Equal(0f, intent.Speed, 3);
        Assert.True(intent.Fire);
    }

    [Fact]
    public void With_an_empty_magazine_it_reloads_instead_of_firing()
    {
        var outlaw = Brain("outlaw", "pistol");

        var intent = outlaw.Think(Senses(new Vector3(15f, 0f, 5f), canSee: true, magEmpty: true, canFire: false), 1f / 60f);

        Assert.Equal(NpcState.Attack, intent.State);
        Assert.True(intent.Reload);
        Assert.False(intent.Fire);
    }

    [Fact]
    public void After_a_shot_it_holds_fire_until_the_react_timer_has_passed()
    {
        _random.Fraction = 0f;
        var outlaw = Brain("outlaw", "pistol");
        var senses = Senses(new Vector3(15f, 0f, 5f), canSee: true);

        Assert.True(outlaw.Think(senses, 1f / 60f).Fire);
        Assert.False(outlaw.Think(senses, 0.1f).Fire);
        Assert.False(outlaw.Think(senses, 0.1f).Fire);
        Assert.True(outlaw.Think(senses, 0.25f).Fire);
    }

    [Fact]
    public void Spread_grows_with_distance_a_running_target_and_a_mount()
    {
        _sim.Wanted.SetLevel(1);   // police only fight when the player is wanted
        var player = new Vector3(15f, 0f, 5f);

        var standing = Brain("british", "pistol").Think(Senses(player, canSee: true), 1f / 60f);
        var running = Brain("british", "pistol").Think(Senses(player, canSee: true, playerSpeed: 4f), 1f / 60f);
        var mounted = Brain("british", "pistol").Think(Senses(player, canSee: true, mounted: true), 1f / 60f);

        Assert.Equal(2.5f, standing.SpreadDegrees, 3);
        Assert.Equal(5.0f, running.SpreadDegrees, 3);
        Assert.Equal(4.0f, mounted.SpreadDegrees, 3);
    }

    [Fact]
    public void A_chaser_that_loses_sight_for_9_s_gives_up_but_keeps_chasing_after_2_s()
    {
        var outlaw = Brain("outlaw", "pistol");
        var player = new Vector3(40f, 0f, 5f);
        outlaw.Think(Senses(player, canSee: true), 1f / 60f);

        var stillChasing = outlaw.Think(Senses(player), 2f);
        outlaw.Think(Senses(player), 7.1f);
        var gaveUp = outlaw.Think(Senses(player), 1f / 60f);

        Assert.Equal(NpcState.Chase, stillChasing.State);
        Assert.Equal(NpcState.Idle, gaveUp.State);
    }

    [Fact]
    public void Flee_ends_after_8_s_with_a_two_second_idle_wait()
    {
        var resident = Brain("civilian", wanderRadius: 0f);
        resident.Alert(new Vector3(25f, 0f, 5f));

        var running = resident.Think(Senses(FarEast), 1f / 60f);
        var over = resident.Think(Senses(FarEast), 8.1f);

        Assert.Equal(NpcState.Flee, running.State);
        Assert.Equal(new Vector3(-35f, 0f, 5f), running.FaceTo);
        Assert.Equal(5.2f, running.Speed, 3);
        Assert.Equal(NpcState.Idle, over.State);
    }

    [Fact]
    public void An_unarmed_hostile_flees_when_the_player_comes_within_25_m()
    {
        var thug = Brain("civilian", hostile: true);

        var watching = thug.Think(Senses(new Vector3(35f, 0f, 5f)), 1f / 60f);
        var running = thug.Think(Senses(new Vector3(25f, 0f, 5f)), 1f / 60f);

        Assert.Equal(NpcState.Idle, watching.State);
        Assert.Equal(NpcState.Flee, running.State);
    }

    [Fact]
    public void Stuck_while_wandering_means_idle_and_stuck_while_fleeing_means_a_sidestep()
    {
        _random.Fraction = 1f;
        var wanderer = Brain("civilian");
        wanderer.Think(Senses(FarEast), 4.1f);
        var gaveUp = wanderer.Think(Senses(FarEast, speed: 0f), 1.3f);
        Assert.Equal(NpcState.Idle, gaveUp.State);

        var runner = Brain("civilian", wanderRadius: 0f);
        runner.Alert(new Vector3(25f, 0f, 5f));
        runner.Think(Senses(FarEast, speed: 0f), 1.3f);
        var sidestep = runner.Think(Senses(FarEast, speed: 0f), 1f / 60f);
        Assert.Equal(new Vector3(9f, 0f, 5f), sidestep.FaceTo);
    }

    [Fact]
    public void The_name_shows_within_14_m_only_when_not_hostile()
    {
        var resident = Brain("civilian");
        var outlaw = Brain("outlaw");

        Assert.True(resident.Think(Senses(new Vector3(15f, 0f, 5f)), 1f / 60f).ShowLabel);
        Assert.False(resident.Think(Senses(new Vector3(20f, 0f, 5f)), 1f / 60f).ShowLabel);
        Assert.False(outlaw.Think(Senses(new Vector3(15f, 0f, 5f)), 1f / 60f).ShowLabel);
    }
}
