using System.Collections.Generic;
using Godot;
using Veld.Sim;
using Veld.Tests.Fakes;
using Xunit;

namespace Veld.Tests.Sim;

/// <summary>In the "era flags" collection: completing a mission formats its reward through Money, whose era is a static flag.</summary>
[Collection("era flags")]
public class SimulationTests
{
    private readonly FakeWorld _world = new();
    private readonly FakeNotifier _notifier = new();
    private readonly FakeDialogueView _view = new();

    /// <summary>A modern map with 5.00 to start, a 7:30 start and one errand: walk to the corner for 1.00.</summary>
    private static GameData Data() => new()
    {
        World = new WorldData { Era = "modern", StartMoney = 500, StartHour = 7.5f, DayLengthSeconds = 720f, PlayerWeapons = new List<string> { "pistol" } },
        Weapons = { ["pistol"] = new WeaponData { Id = "pistol" } },
        Missions =
        {
            new MissionData
            {
                Id = "errand", Title = "An errand", Giver = "aunty_devi", RewardPence = 100,
                Steps = { new MissionStep { Type = "goto", Objective = "Go to the corner", Position = new[] { 10f, 10f }, Radius = 3f } },
            },
        },
    };

    private Simulation Sim() => new(Data(), _world, _notifier, _view, new FakeRandom());

    [Fact]
    public void Starts_from_the_map_data()
    {
        var sim = Sim();

        Assert.Equal(500, sim.Purse.Pence);
        Assert.Equal(7.5f, sim.Clock.Hour, 3);
        Assert.Equal(1, sim.Clock.Day);
    }

    [Fact]
    public void The_player_carries_the_maps_weapons()
    {
        var sim = Sim();

        Assert.Single(sim.Player.Weapons);
        Assert.Equal("pistol", sim.Player.CurrentWeaponId);
    }

    [Fact]
    public void A_tick_moves_game_time()
    {
        var sim = Sim();

        sim.Tick(30f);

        Assert.Equal(8.5f, sim.Clock.Hour, 3);
    }

    [Fact]
    public void A_tick_polls_the_mission_and_pays_the_reward()
    {
        var sim = Sim();
        _world.FakePlayerView!.Position = new Vector3(10f, 0f, 10f);
        Assert.True(sim.Missions.Start("errand"));

        sim.Tick(1f / 60f);

        Assert.Contains("errand", sim.Missions.Completed);
        Assert.Equal(600, sim.Purse.Pence);
    }

    [Fact]
    public void Ticks_cool_the_wanted_level_when_no_police_are_near()
    {
        var sim = Sim();
        sim.Wanted.SetLevel(1);

        for (int i = 0; i < 40; i++) sim.Tick(1f);

        Assert.Equal(0, sim.Wanted.Level);
        Assert.Contains("The police have lost interest in you.", _notifier.Messages);
    }
}
