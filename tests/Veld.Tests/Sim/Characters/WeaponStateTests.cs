using Veld.Sim;
using Veld.Sim.Characters;
using Xunit;

namespace Veld.Tests.Sim.Characters;

/// <summary>A revolver-sized weapon: five in the magazine, ten in reserve, two shots a second, a 2.5 s reload.</summary>
public class WeaponStateTests
{
    private static WeaponData Revolver(int reserve = 10) => new()
    {
        Id = "webley", Name = "Webley", Kind = "revolver", MagSize = 5, ReserveAmmo = reserve,
        RateOfFire = 2f, ReloadTime = 2.5f, SpreadDegrees = 0.6f, HipSpreadDegrees = 3.5f,
    };

    [Fact]
    public void Starts_with_a_full_magazine_and_the_data_reserve()
    {
        var weapon = new WeaponState(Revolver());

        Assert.Equal(5, weapon.InMag);
        Assert.Equal(10, weapon.Reserve);
        Assert.True(weapon.CanFire);
    }

    [Fact]
    public void Spend_takes_one_round_and_starts_a_half_second_cooldown()
    {
        var weapon = new WeaponState(Revolver());

        weapon.Spend();

        Assert.Equal(4, weapon.InMag);
        Assert.False(weapon.CanFire);
        weapon.Tick(0.4f);
        Assert.False(weapon.CanFire);
        weapon.Tick(0.2f);
        Assert.True(weapon.CanFire);
    }

    [Fact]
    public void StartReload_refuses_when_full_while_reloading_and_with_no_reserve()
    {
        var weapon = new WeaponState(Revolver());
        Assert.False(weapon.StartReload());

        weapon.Spend();
        Assert.True(weapon.StartReload());
        Assert.False(weapon.StartReload());

        var dry = new WeaponState(Revolver(reserve: 0));
        dry.Spend();
        Assert.False(dry.StartReload());
    }

    [Fact]
    public void A_reload_fills_the_magazine_from_the_reserve_after_the_reload_time()
    {
        var weapon = new WeaponState(Revolver());
        for (int i = 0; i < 4; i++) weapon.Spend();
        Assert.True(weapon.StartReload());

        weapon.Tick(2.4f);
        Assert.True(weapon.Reloading);
        Assert.Equal(1, weapon.InMag);

        weapon.Tick(0.2f);
        Assert.False(weapon.Reloading);
        Assert.Equal(5, weapon.InMag);
        Assert.Equal(6, weapon.Reserve);
    }

    [Fact]
    public void An_empty_magazine_cannot_fire()
    {
        var weapon = new WeaponState(Revolver());
        for (int i = 0; i < 5; i++) weapon.Spend();
        weapon.Tick(1f);

        Assert.Equal(0, weapon.InMag);
        Assert.False(weapon.CanFire);
    }

    [Fact]
    public void HitSpread_is_the_aimed_figure_when_aiming_and_the_hip_figure_otherwise()
    {
        var weapon = new WeaponState(Revolver());

        Assert.Equal(0.6f, weapon.HitSpread(aimed: true), 3);
        Assert.Equal(3.5f, weapon.HitSpread(aimed: false), 3);
    }
}
