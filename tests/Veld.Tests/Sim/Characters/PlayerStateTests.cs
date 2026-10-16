using System.Collections.Generic;
using Godot;
using Veld.Sim;
using Veld.Sim.Characters;
using Veld.Tests.Fakes;
using Xunit;

namespace Veld.Tests.Sim.Characters;

/// <summary>
/// In the "era flags" collection: the doctor's fee is formatted through the static money flag. The player
/// carries a pistol and a shotgun with no spare shells; a quiet second is a tick of one second on foot.
/// </summary>
[Collection("era flags")]
public class PlayerStateTests
{
    private readonly FakeWorld _world = new();
    private readonly FakeNotifier _notifier = new();
    private readonly Simulation _sim;
    private readonly PlayerState _player;

    public PlayerStateTests()
    {
        _sim = new Simulation(Data(), _world, _notifier, new FakeDialogueView(), new FakeRandom());
        _player = _sim.Player;
    }

    private static GameData Data() => new()
    {
        World = new WorldData { Era = "modern", StartMoney = 500, PlayerWeapons = new List<string> { "pistol", "shotgun", "unknown" } },
        Weapons =
        {
            ["pistol"] = new WeaponData { Id = "pistol", Name = "Pistol", MagSize = 15, ReserveAmmo = 30, RateOfFire = 4f, ReloadTime = 1.5f },
            ["shotgun"] = new WeaponData { Id = "shotgun", Name = "Shotgun", MagSize = 2, ReserveAmmo = 0, RateOfFire = 1f, ReloadTime = 2f },
        },
    };

    private void Quiet(float seconds = 1f) => _player.Tick(seconds, seconds, aiming: false, wantSprint: false, moving: false, onFoot: true);
    private void Sprint(float seconds = 1f) => _player.Tick(seconds, seconds, aiming: false, wantSprint: true, moving: true, onFoot: true);

    [Fact]
    public void Carries_the_maps_weapons_in_order_with_the_first_equipped_and_unknown_ids_skipped()
    {
        Assert.Equal(2, _player.Weapons.Count);
        Assert.Equal("pistol", _player.Weapons[0].Data.Id);
        Assert.Equal("shotgun", _player.Weapons[1].Data.Id);
        Assert.Equal("pistol", _player.CurrentWeaponId);
    }

    [Fact]
    public void Equip_refuses_a_weapon_not_carried_and_holsters_on_null()
    {
        Assert.False(_player.Equip("rifle"));
        Assert.Equal("pistol", _player.CurrentWeaponId);

        Assert.True(_player.Equip("shotgun"));
        Assert.Equal("Shotgun", _player.CurrentWeapon!.Data.Name);

        Assert.True(_player.Equip(null));
        Assert.Null(_player.CurrentWeapon);
    }

    [Fact]
    public void Health_regenerates_four_a_second_once_six_quiet_seconds_have_passed()
    {
        _player.TakeDamage(50);

        for (int i = 0; i < 6; i++) Quiet();
        Assert.Equal(50, _player.Health);

        Quiet();
        Assert.Equal(54, _player.Health);
    }

    [Fact]
    public void Sprinting_drains_fourteen_a_second_and_refuses_once_stamina_is_spent()
    {
        Sprint();
        Assert.True(_player.Sprinting);
        Assert.Equal(86f, _player.Stamina, 2);

        for (int i = 0; i < 7; i++) Sprint();
        Assert.Equal(0f, _player.Stamina, 2);

        Sprint();
        Assert.False(_player.Sprinting);
        Assert.Equal(10f, _player.Stamina, 2);

        Quiet();
        Assert.Equal(20f, _player.Stamina, 2);
    }

    [Fact]
    public void Stamina_does_not_change_off_foot()
    {
        Sprint();

        _player.Tick(1f, 1f, aiming: false, wantSprint: true, moving: true, onFoot: false);

        Assert.False(_player.Sprinting);
        Assert.Equal(86f, _player.Stamina, 2);
    }

    [Fact]
    public void Dead_Eye_toggles_only_above_five_drains_while_active_and_ends_when_aiming_stops()
    {
        var changes = new List<bool>();
        _player.DeadEyeChanged += on => changes.Add(on);

        Assert.True(_player.ToggleDeadEye());
        Assert.True(_player.DeadEyeActive);

        _player.Tick(0.3f, 1f, aiming: true, wantSprint: false, moving: false, onFoot: true);
        Assert.Equal(85f, _player.DeadEye, 2);

        _player.Tick(0.3f, 1f, aiming: false, wantSprint: false, moving: false, onFoot: true);
        Assert.False(_player.DeadEyeActive);
        Assert.Equal(new[] { true, false }, changes);

        Assert.True(_player.ToggleDeadEye());
        _player.Tick(0.01f, 4.4f, aiming: true, wantSprint: false, moving: false, onFoot: true);
        _player.Tick(0.01f, 0.01f, aiming: false, wantSprint: false, moving: false, onFoot: true);
        Assert.False(_player.DeadEyeActive);
        Assert.True(_player.DeadEye < 5f);
        Assert.False(_player.ToggleDeadEye());
    }

    [Fact]
    public void Dead_Eye_refills_two_and_a_half_a_second_while_off()
    {
        _player.ToggleDeadEye();
        _player.Tick(0.01f, 6f, aiming: true, wantSprint: false, moving: false, onFoot: true);
        _player.ToggleDeadEye();
        Assert.Equal(10f, _player.DeadEye, 2);

        Quiet();

        Assert.Equal(12.5f, _player.DeadEye, 2);
    }

    [Fact]
    public void A_killing_blow_sets_dead_and_on_foot_raises_Died_and_toasts()
    {
        bool died = false;
        _player.Died += () => died = true;
        _player.Mount = Mount.Riding;

        Assert.True(_player.TakeDamage(100));

        Assert.True(died);
        Assert.True(_player.IsDead);
        Assert.Equal(0, _player.Health);
        Assert.Equal(Mount.OnFoot, _player.Mount);
        Assert.Contains("You are dead.", _notifier.Messages);
        Assert.False(_player.TakeDamage(10));
    }

    [Fact]
    public void Four_seconds_dead_it_comes_to_at_the_station()
    {
        bool respawned = false;
        _player.Respawned += () => respawned = true;
        _sim.Wanted.SetLevel(2);
        Sprint();
        _player.Equip("shotgun");
        _player.CurrentWeapon!.Spend();
        _player.CurrentWeapon!.Spend();
        _player.TakeDamage(100);

        for (int i = 0; i < 3; i++) Quiet();
        Assert.True(_player.IsDead);

        Quiet(1.1f);

        Assert.True(respawned);
        Assert.False(_player.IsDead);
        Assert.Equal(100, _player.Health);
        Assert.Equal(100f, _player.Stamina, 2);
        Assert.Equal(0, _sim.Wanted.Level);
        Assert.Equal(476, _sim.Purse.Pence);
        Assert.Equal(4, _player.Weapons[1].Reserve);
        Assert.Contains(_notifier.Messages, m => m.StartsWith("You come to at the station."));
    }

    [Fact]
    public void Reload_toasts_when_out_of_ammunition_and_starts_the_reload_otherwise()
    {
        _player.Equip("shotgun");
        _player.CurrentWeapon!.Spend();
        _player.CurrentWeapon!.Spend();

        Assert.False(_player.Reload());
        Assert.Contains("Out of ammunition for the Shotgun.", _notifier.Messages);

        _player.Equip("pistol");
        _player.CurrentWeapon!.Spend();

        Assert.True(_player.Reload());
        Assert.True(_player.CurrentWeapon!.Reloading);
    }

    [Fact]
    public void Heal_fills_health_and_GiveAmmo_adds_to_the_reserve()
    {
        _player.TakeDamage(30);

        _player.Heal();
        _player.GiveAmmo("pistol", 5);
        _player.GiveAmmo("unknown", 5);

        Assert.Equal(100, _player.Health);
        Assert.Equal(35, _player.Weapons[0].Reserve);
    }
}
