using System.Collections.Generic;
using Godot;
using Veld.Sim;
using Veld.Sim.Missions;
using Veld.Tests.Fakes;
using Xunit;

namespace Veld.Tests.Gameplay;

/// <summary>
/// In the "era flags" collection with the Money and Factions tests: the completion message goes
/// through Money.Format, whose era is a static flag, so these classes must not run in parallel.
/// </summary>
[Collection("era flags")]
public class MissionManagerTests
{
    private readonly FakeWorld _world = new();
    private readonly FakeEconomy _economy = new();
    private readonly FakeNotifier _notifier = new();

    private MissionManager Manager(params MissionData[] missions) =>
        new(new GameData { Missions = new List<MissionData>(missions) }, _world, _economy, _notifier);

    /// <summary>The parcel run from the Chatsworth map: go to the school, hand over, report back.</summary>
    private static MissionData Parcel() => new()
    {
        Id = "parcel", Title = "Aunty Devi's Parcel", Giver = "aunty_devi", RewardPence = 15000,
        Steps =
        {
            new MissionStep { Type = "goto", Objective = "Take the parcel to the school", Position = new[] { 445f, 905f }, Radius = 45f },
            new MissionStep { Type = "talk", Objective = "Hand the parcel to Mr. Govender", Target = "mr_govender" },
            new MissionStep { Type = "return", Objective = "Go back to Aunty Devi" },
        },
    };

    /// <summary>The stolen kit: needs the parcel done first, spawns two thieves, ends when both are down.</summary>
    private static MissionData StolenKit() => new()
    {
        Id = "stolen_kit", Title = "The Stolen Kit", Giver = "coach_naidoo", RewardPence = 80000, Requires = { "parcel" },
        Steps =
        {
            new MissionStep { Type = "goto", Objective = "Go to the scrapyard", Position = new[] { 900f, -386f }, Radius = 60f },
            new MissionStep { Type = "kill_group", Objective = "Deal with the thieves", Group = "thieves" },
            new MissionStep { Type = "return", Objective = "Tell Coach Naidoo" },
        },
        Spawns =
        {
            new NpcData { Id = "thief_1", Name = "Thief", Group = "thieves", Faction = "outlaw", Position = new[] { 905f, -392f } },
            new NpcData { Id = "thief_2", Name = "Thief", Group = "thieves", Faction = "outlaw", Position = new[] { 888f, -372f } },
        },
    };

    private void PlayerAt(float x, float z) => _world.FakePlayerView!.Position = new Vector3(x, 0f, z);

    [Fact]
    public void Start_pays_nothing_and_notifies_the_title()
    {
        var missions = Manager(Parcel());

        Assert.True(missions.Start("parcel"));

        Assert.Equal("parcel", missions.Active?.Id);
        Assert.Equal(0, missions.StepIndex);
        Assert.Empty(_economy.Added);
        Assert.Equal(new[] { "New task: Aunty Devi's Parcel" }, _notifier.Messages);
    }

    [Fact]
    public void Availability_needs_a_known_idle_uncompleted_mission_with_its_requirements_met()
    {
        var missions = Manager(Parcel(), StolenKit());

        Assert.False(missions.IsAvailable("no_such_mission"));
        Assert.False(missions.IsAvailable("stolen_kit"), "requires the parcel first");
        Assert.True(missions.IsAvailable("parcel"));

        missions.Start("parcel");
        Assert.False(missions.IsAvailable("parcel"), "already active");

        missions.Restore(new List<string> { "parcel" }, null, 0);
        Assert.False(missions.IsAvailable("parcel"), "already completed");
        Assert.True(missions.IsAvailable("stolen_kit"));
    }

    [Fact]
    public void Start_refuses_an_unavailable_mission_and_changes_nothing()
    {
        var missions = Manager(Parcel(), StolenKit());

        Assert.False(missions.Start("stolen_kit"));

        Assert.Null(missions.Active);
        Assert.Empty(_notifier.Messages);
        Assert.Empty(_world.SpawnedIds);
    }

    [Fact]
    public void Start_spawns_only_the_characters_the_world_lacks()
    {
        _world.Characters.Add(new FakeNpc { Id = "thief_1", Group = "thieves" });
        var missions = Manager(Parcel(), StolenKit());
        missions.Restore(new List<string> { "parcel" }, null, 0);

        missions.Start("stolen_kit");

        Assert.Equal(new[] { "thief_2" }, _world.SpawnedIds);
    }

    [Fact]
    public void A_goto_step_advances_only_inside_its_radius()
    {
        var missions = Manager(Parcel());
        missions.Start("parcel");

        PlayerAt(445f, 955f);
        missions.Update(0.016f);
        Assert.Equal(0, missions.StepIndex);

        PlayerAt(445f, 940f);
        missions.Update(0.016f);
        Assert.Equal(1, missions.StepIndex);
        Assert.Equal("Hand the parcel to Mr. Govender", _notifier.Messages[^1]);
    }

    [Fact]
    public void A_kill_group_step_advances_only_when_everyone_in_the_group_is_dead()
    {
        var missions = Manager(Parcel(), StolenKit());
        missions.Restore(new List<string> { "parcel" }, null, 0);
        missions.Start("stolen_kit");
        PlayerAt(900f, -386f);
        missions.Update(0.016f);
        Assert.Equal(1, missions.StepIndex);

        _world.Characters[0].IsDead = true;
        missions.Update(0.016f);
        Assert.Equal(1, missions.StepIndex);

        _world.Characters[1].IsDead = true;
        missions.Update(0.016f);
        Assert.Equal(2, missions.StepIndex);
    }

    [Fact]
    public void Talking_advances_only_for_the_right_character_and_a_bare_return_means_the_giver()
    {
        var missions = Manager(Parcel());
        missions.Start("parcel");
        PlayerAt(445f, 905f);
        missions.Update(0.016f);

        missions.OnTalked("aunty_devi");
        Assert.Equal(1, missions.StepIndex);

        missions.OnTalked("mr_govender");
        Assert.Equal(2, missions.StepIndex);

        missions.OnTalked("mr_govender");
        Assert.Equal(2, missions.StepIndex);

        missions.OnTalked("aunty_devi");
        Assert.True(missions.IsDone("parcel"));
    }

    [Fact]
    public void Completing_the_last_step_pays_the_reward_once_at_the_player_and_says_so()
    {
        Money.Modern = true;
        var missions = Manager(Parcel());
        missions.Start("parcel");
        PlayerAt(445f, 905f);
        missions.Update(0.016f);
        missions.OnTalked("mr_govender");

        missions.OnTalked("aunty_devi");

        Assert.Null(missions.Active);
        Assert.Equal(0, missions.StepIndex);
        Assert.True(missions.IsDone("parcel"));
        Assert.Equal(new[] { 15000 }, _economy.Added);
        Assert.Equal(new[] { new Vector3(445f, 0f, 905f) }, _notifier.CoinsAt);
        Assert.Equal("Task complete: Aunty Devi's Parcel (+R150.00)", _notifier.Messages[^1]);
    }

    [Fact]
    public void Restore_brings_back_progress_clamps_the_step_and_respawns_missing_characters()
    {
        var missions = Manager(Parcel(), StolenKit());

        missions.Restore(new List<string> { "parcel" }, "stolen_kit", 99);

        Assert.True(missions.IsDone("parcel"));
        Assert.Equal("stolen_kit", missions.Active?.Id);
        Assert.Equal(2, missions.StepIndex);
        Assert.Equal(new[] { "thief_1", "thief_2" }, _world.SpawnedIds);
    }

    [Fact]
    public void The_marker_follows_the_step_kind()
    {
        var missions = Manager(Parcel(), StolenKit());
        Assert.Null(missions.MarkerPosition);

        missions.Start("parcel");
        Assert.Equal(new Vector3(445f, 0f, 905f), missions.MarkerPosition);

        _world.Characters.Add(new FakeNpc { Id = "mr_govender", Position = new Vector3(440f, 0f, 900f) });
        PlayerAt(445f, 905f);
        missions.Update(0.016f);
        Assert.Equal(new Vector3(440f, 0f, 900f), missions.MarkerPosition);
    }

    /// <summary>A kind no shipped mission uses, registered by a test to prove the registry is the seam.</summary>
    private sealed class AlwaysDoneStep : IMissionStepKind
    {
        public string Type => "always_done";
        public bool Poll(MissionStep step, IWorldQuery world) => true;
        public Vector3? Marker(MissionStep step, IWorldQuery world, string giver) => null;
    }

    [Fact]
    public void A_kind_added_to_the_registry_drives_a_step()
    {
        var wait = new MissionData { Id = "wait", Title = "Wait", Giver = "aunty_devi", Steps = { new MissionStep { Type = "always_done", Objective = "Just wait" } } };
        var data = new GameData { Missions = { wait } };
        var missions = new MissionManager(data, _world, _economy, _notifier, MissionStepRegistry.Builtin().Add(new AlwaysDoneStep()));
        missions.Start("wait");

        missions.Update(0.016f);

        Assert.True(missions.IsDone("wait"));
    }

    [Fact]
    public void An_unknown_step_kind_is_reported_when_the_mission_starts()
    {
        var delivery = new MissionData { Id = "delivery", Title = "The Delivery", Giver = "aunty_devi", Steps = { new MissionStep { Type = "deliver", Objective = "Deliver it" } } };
        var missions = Manager(delivery);

        missions.Start("delivery");
        missions.Update(0.016f);

        Assert.Equal(new[] { "New task: The Delivery", "Mission 'The Delivery' has a step of unknown kind 'deliver'." }, _notifier.Messages);
        Assert.Equal(0, missions.StepIndex);
        Assert.Null(missions.MarkerPosition);
    }

    [Fact]
    public void The_kill_group_marker_points_at_the_nearest_living_member()
    {
        var missions = Manager(Parcel(), StolenKit());
        missions.Restore(new List<string> { "parcel" }, "stolen_kit", 1);
        PlayerAt(900f, -386f);

        Assert.Equal(new Vector3(905f, 0f, -392f), missions.MarkerPosition);

        _world.Characters[0].IsDead = true;
        Assert.Equal(new Vector3(888f, 0f, -372f), missions.MarkerPosition);
    }
}
