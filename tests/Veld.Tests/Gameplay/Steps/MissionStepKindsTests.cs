using Godot;
using Veld.Sim;
using Veld.Sim.Missions;
using Veld.Tests.Fakes;
using Xunit;

namespace Veld.Tests.Gameplay.Steps;

public class MissionStepKindsTests
{
    private readonly FakeWorld _world = new();

    private FakeNpc Add(string id, string group, float x, float z, bool dead = false)
    {
        var npc = new FakeNpc { Id = id, Name = "Thief", Group = group, Faction = Faction.Outlaw, Position = new Vector3(x, 0f, z), IsDead = dead };
        _world.Characters.Add(npc);
        return npc;
    }

    private void PlayerAt(float x, float z) => _world.FakePlayerView!.Position = new Vector3(x, 0f, z);

    [Fact]
    public void Goto_is_done_within_its_radius_and_points_at_the_ground()
    {
        IMissionStepKind kind = new GotoStep();
        var step = new MissionStep { Type = "goto", Position = new[] { 445f, 905f }, Radius = 45f };

        PlayerAt(445f, 955f);
        Assert.False(kind.Poll(step, _world));
        PlayerAt(445f, 940f);
        Assert.True(kind.Poll(step, _world));

        Assert.Equal(new Vector3(445f, 0f, 905f), kind.Marker(step, _world, "giver"));
        Assert.False(kind.OnTalked(step, "giver", "giver"));
    }

    [Fact]
    public void Kill_group_is_done_when_the_whole_group_is_dead()
    {
        var kind = new KillGroupStep();
        var step = new MissionStep { Type = "kill_group", Group = "thieves" };
        var a = Add("a", "thieves", 10f, 0f);
        var b = Add("b", "thieves", 20f, 0f);

        Assert.False(kind.Poll(step, _world));
        a.IsDead = true;
        Assert.False(kind.Poll(step, _world));
        b.IsDead = true;
        Assert.True(kind.Poll(step, _world));
    }

    [Fact]
    public void Kill_group_points_at_the_nearest_living_member_or_nowhere()
    {
        var kind = new KillGroupStep();
        var step = new MissionStep { Type = "kill_group", Group = "thieves" };
        PlayerAt(0f, 0f);
        var near = Add("a", "thieves", 10f, 0f);
        var far = Add("b", "thieves", 30f, 0f);

        Assert.Equal(new Vector3(10f, 0f, 0f), kind.Marker(step, _world, "giver"));
        near.IsDead = true;
        Assert.Equal(new Vector3(30f, 0f, 0f), kind.Marker(step, _world, "giver"));
        far.IsDead = true;
        Assert.Null(kind.Marker(step, _world, "giver"));
    }

    [Fact]
    public void Kill_group_reports_a_death_only_while_members_of_the_group_remain()
    {
        var kind = new KillGroupStep();
        var step = new MissionStep { Type = "kill_group", Group = "thieves" };
        var a = Add("a", "thieves", 10f, 0f);
        var b = Add("b", "thieves", 20f, 0f);
        var bystander = Add("c", "residents", 5f, 0f);

        Assert.Null(kind.OnNpcKilled(step, bystander, _world));

        a.IsDead = true;
        Assert.Equal("Thief is down. 1 left.", kind.OnNpcKilled(step, a, _world));

        b.IsDead = true;
        Assert.Null(kind.OnNpcKilled(step, b, _world));
    }

    [Theory]
    [InlineData("talk")]
    [InlineData("return")]
    public void Talk_and_return_complete_on_the_target_or_the_giver(string type)
    {
        IMissionStepKind kind = new TalkStep(type);
        Assert.Equal(type, kind.Type);

        var targeted = new MissionStep { Type = type, Target = "mr_govender" };
        Assert.True(kind.OnTalked(targeted, "mr_govender", "aunty_devi"));
        Assert.False(kind.OnTalked(targeted, "aunty_devi", "aunty_devi"));

        var bare = new MissionStep { Type = type };
        Assert.True(kind.OnTalked(bare, "aunty_devi", "aunty_devi"));
        Assert.False(kind.OnTalked(bare, "kyle", "aunty_devi"));
        Assert.False(kind.Poll(bare, _world));
    }

    [Fact]
    public void Talk_points_at_the_target_character()
    {
        var kind = new TalkStep("talk");
        Add("mr_govender", "", 440f, 900f);

        Assert.Equal(new Vector3(440f, 0f, 900f), kind.Marker(new MissionStep { Type = "talk", Target = "mr_govender" }, _world, "aunty_devi"));
        Assert.Null(kind.Marker(new MissionStep { Type = "talk" }, _world, "aunty_devi"));
    }
}
