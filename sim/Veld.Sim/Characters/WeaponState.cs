using Godot;

namespace Veld.Sim.Characters;

/// <summary>One weapon's ammunition and timing: magazine, reserve, cooldown and reload. The raycast lives in the game.</summary>
public sealed class WeaponState
{
    public WeaponState(WeaponData data)
    {
        Data = data;
        InMag = data.MagSize;
        Reserve = data.ReserveAmmo;
    }

    public WeaponData Data { get; }
    public int InMag { get; set; }
    public int Reserve { get; set; }
    public float Cooldown { get; private set; }
    public float ReloadLeft { get; private set; }
    public bool Reloading => ReloadLeft > 0f;
    public bool CanFire => Cooldown <= 0f && !Reloading && InMag > 0;

    public void Tick(float dt)
    {
        if (Cooldown > 0) Cooldown -= dt;
        if (ReloadLeft > 0)
        {
            ReloadLeft -= dt;
            if (ReloadLeft <= 0)
            {
                int need = Data.MagSize - InMag;
                int take = Mathf.Min(need, Reserve);
                InMag += take;
                Reserve -= take;
            }
        }
    }

    public bool StartReload()
    {
        if (Reloading || InMag == Data.MagSize || Reserve <= 0) return false;
        ReloadLeft = Data.ReloadTime;
        return true;
    }

    /// <summary>One round out of the magazine; the next can leave after 1 / rateOfFire seconds.</summary>
    public void Spend()
    {
        InMag--;
        Cooldown = 1f / Data.RateOfFire;
    }

    public float HitSpread(bool aimed) => aimed ? Data.SpreadDegrees : Data.HipSpreadDegrees;
}
