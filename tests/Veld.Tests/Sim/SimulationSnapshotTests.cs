using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using Veld.Sim;
using Veld.Sim.Characters;
using Veld.Tests.Fakes;
using Xunit;

namespace Veld.Tests.Sim;

/// <summary>
/// In the "era flags" collection: a completed errand formats its reward through the static money flag.
/// The map: 5.00 to start, a fifteen-round pistol with thirty spare, and the errand to the corner.
/// </summary>
[Collection("era flags")]
public class SimulationSnapshotTests
{
    private readonly FakeWorld _world = new();
    private readonly FakeNotifier _notifier = new();

    private static GameData Data() => new()
    {
        World = new WorldData { Era = "modern", StartMoney = 500, StartHour = 7.5f, DayLengthSeconds = 720f, PlayerWeapons = new List<string> { "pistol" } },
        Weapons = { ["pistol"] = new WeaponData { Id = "pistol", Name = "Pistol", MagSize = 15, ReserveAmmo = 30 } },
        Missions =
        {
            new MissionData
            {
                Id = "errand", Title = "An errand", Giver = "aunty_devi", RewardPence = 100,
                Steps = { new MissionStep { Type = "goto", Objective = "Go to the corner", Position = new[] { 10f, 10f }, Radius = 3f } },
            },
        },
    };

    private Simulation Sim() => new(Data(), _world, _notifier, new FakeDialogueView(), new FakeRandom());

    private NpcBrain Character(Simulation sim, string id, string faction = "civilian")
    {
        var body = new FakeBody { Id = id, Name = "Someone", Position = new Vector3(5f, 0f, 5f) };
        body.Brain = sim.CreateBrain(new NpcData { Id = id, Name = "Someone", Faction = faction }, body);
        _world.Bodies.Add(body);
        return body.Brain;
    }

    [Fact]
    public void A_changed_simulation_round_trips_into_a_fresh_one()
    {
        var before = Sim();
        _world.FakePlayerView!.Position = new Vector3(10f, 0f, 10f);
        before.Missions.Start("errand");
        before.Tick(1f / 60f);
        before.Purse.Add(50);
        before.Reputation.Lose(2);
        before.Clock.Hour = 14.5f;
        before.Clock.Day = 3;
        before.Wanted.SetLevel(2);
        before.Player.TakeDamage(30);
        for (int i = 0; i < 6; i++) before.Player.Weapons[0].Spend();
        before.Player.Weapons[0].Reserve = 20;

        var after = Sim();
        after.Restore(before.Snapshot());

        Assert.Equal(650, after.Purse.Pence);
        Assert.Equal(-2, after.Reputation.Value);
        Assert.Equal(14.5f, after.Clock.Hour, 3);
        Assert.Equal(3, after.Clock.Day);
        Assert.Equal(2, after.Wanted.Level);
        Assert.Contains("errand", after.Missions.Completed);
        Assert.Equal(70, after.Player.Health);
        Assert.Equal("pistol", after.Player.CurrentWeaponId);
        Assert.Equal(9, after.Player.Weapons[0].InMag);
        Assert.Equal(20, after.Player.Weapons[0].Reserve);
    }

    [Fact]
    public void A_killed_character_is_listed_among_the_dead()
    {
        var sim = Sim();
        var thug = Character(sim, "thug", "outlaw");
        thug.TakeDamage(100, null, Vector3.Zero);

        Assert.Contains("thug", sim.Snapshot().DeadNpcs);
    }

    [Fact]
    public void Restoring_marks_an_existing_character_dead_silently()
    {
        var sim = Sim();
        var resident = Character(sim, "resident");

        sim.Restore(new SimSnapshot { Health = 100, DeadNpcs = { "resident" } });

        Assert.True(resident.IsDead);
        Assert.True(resident.Looted);
        Assert.Empty(_notifier.Messages);
        Assert.Equal(0, sim.Reputation.Value);
    }

    [Fact]
    public void A_character_created_after_the_restore_with_a_listed_id_is_born_dead()
    {
        var sim = Sim();
        sim.Restore(new SimSnapshot { Health = 100, DeadNpcs = { "thief_1" } });

        var thief = Character(sim, "thief_1", "outlaw");

        Assert.True(thief.IsDead);
        Assert.True(thief.Looted);
        Assert.Contains("thief_1", sim.Snapshot().DeadNpcs);
    }

    [Fact]
    public void Health_restores_to_at_least_ten()
    {
        var sim = Sim();

        sim.Restore(new SimSnapshot { Health = 0 });
        Assert.Equal(10, sim.Player.Health);

        sim.Restore(new SimSnapshot { Health = 55 });
        Assert.Equal(55, sim.Player.Health);
    }

    [Fact]
    public void The_snapshot_carries_the_players_position_under_the_save_files_keys()
    {
        var sim = Sim();
        _world.FakePlayerView!.Position = new Vector3(3f, 1f, 4f);
        sim.Missions.Start("errand");

        var snapshot = sim.Snapshot();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot, DataLoader.Json));
        var keys = json.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToArray();

        Assert.Equal(new[] { 3f, 1f, 4f }, snapshot.PlayerPosition);
        Assert.Equal(new[]
        {
            "activeMission", "activeStep", "ammo", "completedMissions", "day", "deadNpcs", "health",
            "hour", "pence", "playerPosition", "reputation", "version", "wantedLevel", "weapon",
        }, keys);
    }
}
