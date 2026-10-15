using Godot;
using Veld.Sim;
using Veld.Tests.Fakes;
using Xunit;

namespace Veld.Tests.Gameplay;

public class WantedSystemTests
{
    private readonly FakeWorld _world = new();
    private readonly FakeNotifier _notifier = new();

    private WantedSystem Wanted() => new(_world, _notifier);

    private FakeNpc Add(string id, Faction faction, float x, float z)
    {
        var npc = new FakeNpc { Id = id, Name = id, Faction = faction, Position = new Vector3(x, 0f, z) };
        _world.Characters.Add(npc);
        return npc;
    }

    private FakeNpc Officer(string id, float x, float z) => Add(id, Faction.British, x, z);

    private FakePlayer Player => _world.FakePlayerView!;

    private static void Tick(WantedSystem wanted, int seconds)
    {
        for (int i = 0; i < seconds; i++) wanted.Update(1f);
    }

    [Fact]
    public void Attacking_the_police_raises_the_level_and_provokes_them()
    {
        Player.Position = Vector3.Zero;
        var near = Officer("near", 10f, 0f);
        var far = Officer("far", 200f, 0f);
        var wanted = Wanted();

        wanted.ReportAttack(near, Player, killed: false);

        Assert.Equal(1, wanted.Level);
        Assert.Equal(30f, wanted.Heat);
        Assert.True(near.Provoked);
        Assert.False(far.Provoked);
        Assert.Equal(new[] { "The police have been called. Armed officers will shoot on sight." }, _notifier.Messages);
    }

    [Theory]
    [InlineData(0, 0f, "")]
    [InlineData(1, 30f, "WANTED")]
    [InlineData(2, 70f, "WANTED: patrols alerted")]
    [InlineData(3, 110f, "WANTED: shoot on sight")]
    public void Setting_a_level_sets_its_heat_and_label(int level, float heat, string label)
    {
        var wanted = Wanted();

        wanted.SetLevel(level);

        Assert.Equal(level, wanted.Level);
        Assert.Equal(heat, wanted.Heat);
        Assert.Equal(label, wanted.Label);
    }

    [Fact]
    public void Levels_outside_zero_to_three_are_clamped()
    {
        var wanted = Wanted();
        wanted.SetLevel(7);
        Assert.Equal(3, wanted.Level);
        wanted.SetLevel(-2);
        Assert.Equal(0, wanted.Level);
    }

    [Fact]
    public void Killing_an_officer_counts_double_and_the_alarm_is_raised_once()
    {
        var officer = Officer("c", 5f, 0f);
        var wanted = Wanted();

        wanted.ReportAttack(officer, Player, killed: true);
        Assert.Equal(60f, wanted.Heat);
        Assert.Equal(2, wanted.Level);

        wanted.ReportAttack(officer, Player, killed: false);
        Assert.Equal(90f, wanted.Heat);
        Assert.Equal(3, wanted.Level);
        Assert.Single(_notifier.Messages);
    }

    [Fact]
    public void On_a_1901_map_the_garrison_is_alerted_instead()
    {
        _world.IsModern = false;
        var wanted = Wanted();

        wanted.ReportAttack(Officer("s", 5f, 0f), Player, killed: false);

        Assert.Equal(new[] { "The garrison has been alerted. Soldiers will shoot on sight." }, _notifier.Messages);
    }

    [Fact]
    public void Attacking_an_outlaw_is_nobodys_business()
    {
        Officer("c", 5f, 0f);
        var wanted = Wanted();

        wanted.ReportAttack(Add("thief", Faction.Outlaw, 0f, 0f), Player, killed: true);

        Assert.Equal(0, wanted.Level);
        Assert.Equal(0f, wanted.Heat);
        Assert.Empty(_notifier.Messages);
    }

    [Fact]
    public void Attacking_a_civilian_counts_only_when_an_officer_is_within_110_m_of_the_victim()
    {
        var resident = Add("resident", Faction.Civilian, 0f, 0f);
        var officer = Officer("c", 120f, 0f);
        var wanted = Wanted();

        wanted.ReportAttack(resident, Player, killed: false);
        Assert.Equal(0f, wanted.Heat);

        officer.Position = new Vector3(100f, 0f, 0f);
        wanted.ReportAttack(resident, Player, killed: false);
        Assert.Equal(15f, wanted.Heat);

        wanted.ReportAttack(resident, Player, killed: true);
        Assert.Equal(60f, wanted.Heat);
    }

    [Fact]
    public void Heat_caps_at_120_and_the_level_at_3()
    {
        var officer = Officer("c", 5f, 0f);
        var wanted = Wanted();

        for (int i = 0; i < 4; i++) wanted.ReportAttack(officer, Player, killed: true);

        Assert.Equal(120f, wanted.Heat);
        Assert.Equal(3, wanted.Level);
    }

    [Fact]
    public void Heat_never_decays_while_an_officer_is_within_90_m_of_the_player()
    {
        Player.Position = Vector3.Zero;
        Officer("c", 50f, 0f);
        var wanted = Wanted();
        wanted.SetLevel(1);

        Tick(wanted, 30);

        Assert.Equal(30f, wanted.Heat);
        Assert.Equal(1, wanted.Level);
    }

    [Fact]
    public void Out_of_sight_heat_holds_for_6_s_then_falls_1_5_a_second_until_the_police_lose_interest()
    {
        Player.Position = Vector3.Zero;
        Officer("c", 200f, 0f);
        var wanted = Wanted();
        wanted.SetLevel(1);

        Tick(wanted, 6);
        Assert.Equal(30f, wanted.Heat);

        Tick(wanted, 1);
        Assert.Equal(28.5f, wanted.Heat);

        Tick(wanted, 18);
        Assert.Equal(1.5f, wanted.Heat, 3);
        Assert.Equal(1, wanted.Level);

        Tick(wanted, 1);
        Assert.Equal(0f, wanted.Heat);
        Assert.Equal(0, wanted.Level);
        Assert.Equal(new[] { "The police have lost interest in you." }, _notifier.Messages);

        Tick(wanted, 5);
        Assert.Single(_notifier.Messages);
    }

    [Fact]
    public void Gunfire_alerts_everyone_within_35_m_except_the_shooter()
    {
        var shooter = Add("shooter", Faction.Outlaw, 0f, 0f);
        var bystander = Add("bystander", Faction.Civilian, 20f, 0f);
        var far = Add("far", Faction.Civilian, 50f, 0f);
        var wanted = Wanted();

        wanted.OnGunfire(Vector3.Zero, shooter);

        Assert.Empty(shooter.AlertedFrom);
        Assert.Equal(new[] { Vector3.Zero }, bystander.AlertedFrom);
        Assert.Empty(far.AlertedFrom);

        wanted.OnGunfire(Vector3.Zero, null);
        Assert.Single(shooter.AlertedFrom);
    }

    [Fact]
    public void Clearing_the_record_calms_the_police_down()
    {
        var officer = Officer("c", 5f, 0f);
        var wanted = Wanted();
        wanted.ReportAttack(officer, Player, killed: true);
        Assert.True(officer.Provoked);

        wanted.ClearWanted();

        Assert.Equal(0, wanted.Level);
        Assert.Equal(0f, wanted.Heat);
        Assert.False(officer.Provoked);
    }
}
