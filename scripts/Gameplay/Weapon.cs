using Godot;
using Veld.Characters;
using Veld.Core;
using Veld.World;

namespace Veld.Gameplay;

/// <summary>A held firearm: ammunition state plus the hitscan shot. Shared by the player and NPCs.</summary>
public sealed class Weapon
{
    public WeaponData Data { get; }
    public int InMag { get; set; }
    public int Reserve { get; set; }
    public float Cooldown { get; set; }
    public float ReloadLeft { get; set; }
    public bool Reloading => ReloadLeft > 0f;

    public Weapon(WeaponData data)
    {
        Data = data;
        InMag = data.MagSize;
        Reserve = data.ReserveAmmo;
    }

    public void Tick(float delta)
    {
        if (Cooldown > 0) Cooldown -= delta;
        if (ReloadLeft > 0)
        {
            ReloadLeft -= delta;
            if (ReloadLeft <= 0)
            {
                int need = Data.MagSize - InMag;
                int take = Mathf.Min(need, Reserve);
                InMag += take;
                Reserve -= take;
            }
        }
    }

    public bool CanFire => Cooldown <= 0f && !Reloading && InMag > 0;

    public bool StartReload()
    {
        if (Reloading || InMag == Data.MagSize || Reserve <= 0) return false;
        ReloadLeft = Data.ReloadTime;
        return true;
    }

    /// <summary>Fires one round along `direction` from `origin`, with spread. Returns the hit (if any).</summary>
    public ShotResult Fire(Node3D shooter, Vector3 origin, Vector3 direction, float spreadDegrees, Godot.Collections.Array<Rid> exclude)
    {
        InMag--;
        Cooldown = 1f / Data.RateOfFire;
        var spread = Mathf.DegToRad(spreadDegrees);
        var dir = direction.Normalized();
        if (spread > 0f)
        {
            var rnd = new RandomNumberGenerator();
            var axis = dir.Cross(Vector3.Up).Normalized();
            if (axis.LengthSquared() < 0.001f) axis = Vector3.Right;
            dir = dir.Rotated(axis, rnd.RandfRange(-spread, spread)).Rotated(dir, rnd.RandfRange(0, Mathf.Tau));
        }
        var to = origin + dir * Data.Range;
        var space = shooter.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(origin, to, Layers.Shootable, exclude);
        var hit = space.IntersectRay(query);
        var result = new ShotResult { Origin = origin, End = to };
        if (hit.Count > 0)
        {
            result.End = (Vector3)hit["position"];
            result.Normal = (Vector3)hit["normal"];
            result.Collider = hit["collider"].AsGodotObject() as Node;
            result.Hit = true;
        }
        return result;
    }

    public static float HitSpread(WeaponData data, bool aimed) => aimed ? data.SpreadDegrees : data.HipSpreadDegrees;
}

public struct ShotResult
{
    public bool Hit;
    public Vector3 Origin;
    public Vector3 End;
    public Vector3 Normal;
    public Node? Collider;
}

/// <summary>Visual side of a shot: muzzle flash, tracer line and an impact puff. Fire and forget.</summary>
public static class ShotEffects
{
    public static void Show(Node parent, ShotResult shot, Vector3 muzzle)
    {
        var tracer = new MeshInstance3D();
        var im = new ImmediateMesh();
        tracer.Mesh = im;
        im.SurfaceBegin(Mesh.PrimitiveType.Lines);
        im.SurfaceSetColor(new Color(1f, 0.9f, 0.6f));
        im.SurfaceAddVertex(muzzle);
        im.SurfaceAddVertex(shot.End);
        im.SurfaceEnd();
        tracer.MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        tracer.TopLevel = true;
        parent.AddChild(tracer);
        var flash = new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.5f), LightEnergy = 6f, OmniRange = 6f, Position = muzzle, TopLevel = true };
        parent.AddChild(flash);
        parent.GetTree().CreateTimer(0.06).Timeout += () => { tracer.QueueFree(); flash.QueueFree(); };

        if (shot.Hit)
        {
            var puff = new CpuParticles3D
            {
                Position = shot.End, TopLevel = true, Emitting = true, OneShot = true, Amount = 14, Lifetime = 0.45f, Explosiveness = 1f,
                Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.08f, 0.08f), Material = Props.ParticleMaterial() },
                Direction = shot.Normal, Spread = 50f, InitialVelocityMin = 1.5f, InitialVelocityMax = 4f, Gravity = new Vector3(0, -6f, 0),
                Color = shot.Collider is Npc ? new Color(0.5f, 0.05f, 0.05f) : new Color(0.6f, 0.5f, 0.35f),
            };
            parent.AddChild(puff);
            parent.GetTree().CreateTimer(1.0).Timeout += () => puff.QueueFree();
        }
    }
}
