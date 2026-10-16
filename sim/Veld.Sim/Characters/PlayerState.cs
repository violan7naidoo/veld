using System;
using System.Collections.Generic;
using Godot;

namespace Veld.Sim.Characters;

/// <summary>
/// What the player is: health and its regeneration, stamina and the sprint rule, the Dead Eye meter, the
/// weapons carried, the mount, and death with the respawn that follows. The body feeds Tick this frame's
/// input and renders death, respawn and Dead Eye through the three events.
/// </summary>
public sealed class PlayerState
{
    private readonly IEconomy _purse;
    private readonly WantedSystem _wanted;
    private readonly INotifier _notifier;
    private readonly List<WeaponState> _weapons = new();
    private float _sinceHit;
    private float _regen;
    private float _deadTimer;

    internal PlayerState(IEnumerable<string> weaponIds, IReadOnlyDictionary<string, WeaponData> weapons, IEconomy purse, WantedSystem wanted, INotifier notifier)
    {
        _purse = purse;
        _wanted = wanted;
        _notifier = notifier;
        foreach (var id in weaponIds)
            if (weapons.TryGetValue(id, out var data)) _weapons.Add(new WeaponState(data));
        if (_weapons.Count > 0) CurrentWeaponId = _weapons[0].Data.Id;
    }

    public int Health { get; set; } = 100;
    public int MaxHealth => 100;
    public float Stamina { get; private set; } = 100f;
    public bool IsDead { get; private set; }
    /// <summary>Slow-motion aiming meter, 0..100.</summary>
    public float DeadEye { get; private set; } = 100f;
    public bool DeadEyeActive { get; private set; }
    public IReadOnlyList<WeaponState> Weapons => _weapons;
    public string? CurrentWeaponId { get; private set; }
    public WeaponState? CurrentWeapon => CurrentWeaponId == null ? null : _weapons.Find(w => w.Data.Id == CurrentWeaponId);
    public Mount Mount { get; set; }
    /// <summary>Decided by the last tick: sprint was wanted, the player moved, stamina was above 1 and they were not aiming.</summary>
    public bool Sprinting { get; private set; }

    public event Action<bool>? DeadEyeChanged;
    public event Action? Died;
    public event Action? Respawned;

    /// <summary>One physics frame of what the player is. realDt is dt divided by the engine's time scale, so Dead Eye drains in real time.</summary>
    public void Tick(float dt, float realDt, bool aiming, bool wantSprint, bool moving, bool onFoot)
    {
        foreach (var w in _weapons) w.Tick(dt);
        if (IsDead)
        {
            _deadTimer -= dt;
            if (_deadTimer <= 0f) Respawn();
            return;
        }

        _sinceHit += dt;
        if (_sinceHit > 6f && Health < MaxHealth)
        {
            _regen += dt * 4f;
            if (_regen >= 1f) { Health = Math.Min(MaxHealth, Health + (int)_regen); _regen -= (int)_regen; }
        }

        if (DeadEyeActive)
        {
            DeadEye -= 15f * realDt;
            if (!aiming || DeadEye <= 0f) SetDeadEye(false);
        }
        else DeadEye = Math.Min(100f, DeadEye + 2.5f * dt);

        if (onFoot)
        {
            Sprinting = wantSprint && moving && Stamina > 1f && !aiming;
            Stamina = Sprinting ? Math.Max(0f, Stamina - 14f * dt) : Math.Min(100f, Stamina + 10f * dt);
        }
        else Sprinting = false;
    }

    /// <summary>False when that weapon is not carried; null holsters.</summary>
    public bool Equip(string? id)
    {
        if (id != null && !_weapons.Exists(w => w.Data.Id == id)) return false;
        CurrentWeaponId = id;
        return true;
    }

    /// <summary>Q while aiming: on or off, only with the meter above 5.</summary>
    public bool ToggleDeadEye()
    {
        if (DeadEye <= 5f) return false;
        SetDeadEye(!DeadEyeActive);
        return true;
    }

    private void SetDeadEye(bool on)
    {
        if (DeadEyeActive == on) return;
        DeadEyeActive = on;
        DeadEyeChanged?.Invoke(on);
    }

    /// <summary>Starts the current weapon's reload; says so when there is nothing left to load.</summary>
    public bool Reload()
    {
        var w = CurrentWeapon;
        if (w == null) return false;
        if (w.StartReload()) return true;
        if (w.InMag == 0 && !w.Reloading) _notifier.Notify("Out of ammunition for the " + w.Data.Name + ".");
        return false;
    }

    /// <summary>Returns true when this blow killed.</summary>
    public bool TakeDamage(int amount)
    {
        if (IsDead) return false;
        Health -= amount;
        _sinceHit = 0f;
        if (Health > 0) return false;
        Die();
        return true;
    }

    private void Die()
    {
        SetDeadEye(false);
        IsDead = true;
        Health = 0;
        Mount = Mount.OnFoot;
        _deadTimer = 4f;
        _notifier.Notify("You are dead.");
        Died?.Invoke();
    }

    private void Respawn()
    {
        IsDead = false;
        Health = MaxHealth;
        Stamina = 100f;
        _wanted.SetLevel(0);
        int loss = Math.Min(_purse.Pence, 2 * Money.Shilling);
        if (loss > 0) _purse.Add(-loss);
        foreach (var w in _weapons) if (w.InMag == 0 && w.Reserve == 0) w.Reserve = w.Data.MagSize * 2;
        _notifier.Notify("You come to at the station. The doctor took " + Money.Format(loss) + " for his trouble.");
        Respawned?.Invoke();
    }

    /// <summary>The player fired: the wanted system hears it and nearby townsfolk scatter.</summary>
    public void ReportGunfire(Vector3 at) => _wanted.OnGunfire(at, null);

    public void Heal() => Health = MaxHealth;

    public void GiveAmmo(string weaponId, int count)
    {
        var w = _weapons.Find(x => x.Data.Id == weaponId);
        if (w != null) w.Reserve += count;
    }

    public Dictionary<string, int> AmmoSnapshot()
    {
        var d = new Dictionary<string, int>();
        foreach (var w in _weapons) { d[w.Data.Id + ".mag"] = w.InMag; d[w.Data.Id + ".reserve"] = w.Reserve; }
        return d;
    }

    public void RestoreAmmo(Dictionary<string, int> ammo, string? weapon)
    {
        foreach (var w in _weapons)
        {
            if (ammo.TryGetValue(w.Data.Id + ".mag", out var m)) w.InMag = m;
            if (ammo.TryGetValue(w.Data.Id + ".reserve", out var r)) w.Reserve = r;
        }
        Equip(weapon ?? (_weapons.Count > 0 ? _weapons[0].Data.Id : null));
    }
}
