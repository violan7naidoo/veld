using System.Collections.Generic;
using Godot;
using Veld.Core;
using Veld.Gameplay;

namespace Veld.Characters;

/// <summary>
/// The player: third-person movement relative to the camera, sprint and jump, aiming and hitscan
/// shooting with two weapons, interaction with anything that implements IInteractable, and riding
/// (the body is parked on the horse's saddle and input is forwarded to the horse).
/// </summary>
public partial class Player : CharacterBody3D, IPlayerView
{
    public CameraRig Camera { get; set; } = null!;
    Vector3 IPlayerView.Position => GlobalPosition;
    public void Heal() => Health = MaxHealth;
    public void GiveAmmo(string weaponId, int count)
    {
        if (Weapons.TryGetValue(weaponId, out var weapon)) weapon.Reserve += count;
    }
    public int Health { get; set; } = 100;
    public int MaxHealth { get; } = 100;
    public float Stamina { get; private set; } = 100f;
    public bool IsDead { get; private set; }
    public Horse? Mounted { get; private set; }
    public bool IsMounted => Mounted != null;
    public Vehicle? Driving { get; private set; }

    public Dictionary<string, Weapon> Weapons { get; } = new();
    public List<string> WeaponOrder { get; } = new();
    public string? CurrentWeaponId { get; private set; }
    public Weapon? CurrentWeapon => CurrentWeaponId != null && Weapons.TryGetValue(CurrentWeaponId, out var w) ? w : null;
    public bool Aiming { get; private set; }
    public IInteractable? Focus { get; private set; }
    public float Speed { get; private set; }
    /// <summary>Slow-motion aiming meter, 0..100.</summary>
    public float DeadEye { get; private set; } = 100f;
    public bool DeadEyeActive { get; private set; }

    private HumanoidModel _model = null!;
    private CollisionShape3D _shape = null!;
    private float _sinceHit;
    private float _deadTimer;
    private float _footstep;
    private bool _restoreCollision;
    private const float WalkSpeed = 3.4f;
    private const float SprintSpeed = 6.6f;
    private const float JumpSpeed = 4.6f;
    private const float Gravity = 9.8f;

    public override void _Ready()
    {
        _shape = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.32f, Height = 1.8f }, Position = new Vector3(0, 0.9f, 0) };
        AddChild(_shape);
        _model = new HumanoidModel { Name = "Model" };
        _model.Configure(Faction.Civilian, 1, new Color(0.82f, 0.64f, 0.5f));
        AddChild(_model);
        FloorMaxAngle = Mathf.DegToRad(50f);
        FloorSnapLength = 0.4f;

        var data = Game.Instance.Data;
        foreach (var id in data.World.PlayerWeapons)
            if (data.Weapons.TryGetValue(id, out var wd))
            {
                Weapons[id] = new Weapon(wd);
                WeaponOrder.Add(id);
            }
        if (WeaponOrder.Count > 0) Equip(WeaponOrder[0]);
    }

    // ------------------------------------------------------------------ equipment

    public void Equip(string? id)
    {
        if (id != null && !Weapons.ContainsKey(id)) return;
        CurrentWeaponId = id;
        _model.ShowWeapon(id == null ? null : Weapons[id].Data.Kind);
    }

    public Dictionary<string, int> AmmoSnapshot()
    {
        var d = new Dictionary<string, int>();
        foreach (var (id, w) in Weapons) { d[id + ".mag"] = w.InMag; d[id + ".reserve"] = w.Reserve; }
        return d;
    }

    public void RestoreAmmo(Dictionary<string, int> ammo, string? weapon)
    {
        foreach (var (id, w) in Weapons)
        {
            if (ammo.TryGetValue(id + ".mag", out var m)) w.InMag = m;
            if (ammo.TryGetValue(id + ".reserve", out var r)) w.Reserve = r;
        }
        Equip(weapon ?? (WeaponOrder.Count > 0 ? WeaponOrder[0] : null));
    }

    // ------------------------------------------------------------------ riding

    public void Mount(Horse horse)
    {
        if (IsMounted || IsDead) return;
        Mounted = horse;
        horse.SetRider(this);
        CollisionLayer = 0;
        CollisionMask = 0;
        Velocity = Vector3.Zero;
        Camera.TargetOffset = new Vector3(0, 2.1f, 0);
        Game.Instance.Hud?.SetPrompt("");
    }

    public void Dismount()
    {
        if (Mounted == null) return;
        var horse = Mounted;
        Mounted = null;
        horse.SetRider(null);
        CollisionLayer = Layers.Characters;
        CollisionMask = Layers.Solid | Layers.Horses;
        GlobalPosition = horse.GlobalPosition + horse.GlobalTransform.Basis.X * 1.3f + new Vector3(0, 0.3f, 0);
        Rotation = new Vector3(0, horse.Rotation.Y, 0);
        Camera.TargetOffset = new Vector3(0, 1.6f, 0);
    }

    // ------------------------------------------------------------------ driving

    public void EnterVehicle(Vehicle v)
    {
        if (Driving != null || IsDead || v.Occupied) return;
        if (IsMounted) Dismount();
        Driving = v;
        v.SetDriver(this);
        CollisionLayer = 0;
        CollisionMask = 0;
        Velocity = Vector3.Zero;
        _model.Visible = false;
        Aiming = false;
        Camera.Aiming = false;
        Camera.Target = v;
        Camera.TargetOffset = new Vector3(0, 1.9f, 0);
        Camera.BaseLength = 7f;
        Camera.FollowYaw = true;
        Camera.Exclude(v.GetRid());
        Game.Instance.Hud?.SetPrompt("");
    }

    public void ExitVehicle()
    {
        if (Driving == null) return;
        var v = Driving;
        Driving = null;
        v.SetDriver(null);
        // the collision shape comes back next physics frame: restoring it now, while the physics
        // server still has this body at the seat inside the car, resolves that overlap with a kick
        // that sends the car flying or through the road
        _restoreCollision = true;
        GlobalPosition = v.ExitPosition();
        Rotation = new Vector3(0, v.Rotation.Y, 0);
        Velocity = Vector3.Zero;
        _model.Visible = true;
        Camera.Target = this;
        Camera.TargetOffset = new Vector3(0, 1.6f, 0);
        Camera.BaseLength = 4.5f;
        Camera.FollowYaw = false;
    }

    // ------------------------------------------------------------------ damage

    public void TakeDamage(int amount, Node? attacker)
    {
        if (IsDead) return;
        Health -= amount;
        _sinceHit = 0f;
        Game.Instance.Main?.Sfx.Hit(GlobalPosition);
        Game.Instance.Hud?.FlashDamage();
        if (Health <= 0) Die();
    }

    public void SetDeadEye(bool on)
    {
        if (DeadEyeActive == on) return;
        DeadEyeActive = on;
        Engine.TimeScale = on ? 0.3 : 1.0;
        if (Game.Instance.DayNight != null) Game.Instance.DayNight.DeadEye = on;
        Game.Instance.Main?.Sfx.Click(GlobalPosition);
    }

    private void Die()
    {
        SetDeadEye(false);
        IsDead = true;
        Health = 0;
        Dismount();
        ExitVehicle();
        Aiming = false;
        Camera.Aiming = false;
        _model.SetDead(true);
        _deadTimer = 4f;
        Game.Instance.Notify("You are dead.");
    }

    private void Respawn()
    {
        IsDead = false;
        Health = MaxHealth;
        Stamina = 100f;
        _model.SetDead(false);
        Game.Instance.Wanted.SetLevel(0);
        int loss = Mathf.Min(Game.Instance.Pence, 2 * Money.Shilling);
        if (loss > 0) Game.Instance.AddMoney(-loss);
        var world = Game.Instance.Data.World;
        var spawn = DataLoader.XZ(world.PlayerSpawn);
        var terrain = Game.Instance.World!.Terrain;
        GlobalPosition = new Vector3(spawn.X, terrain.GetHeight(spawn.X, spawn.Y) + 1f, spawn.Y);
        Velocity = Vector3.Zero;
        foreach (var w in Weapons.Values) if (w.InMag == 0 && w.Reserve == 0) w.Reserve = w.Data.MagSize * 2;
        Game.Instance.Notify("You come to at the station. The doctor took " + Money.Format(loss) + " for his trouble.");
    }

    // ------------------------------------------------------------------ main loop

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var game = Game.Instance;
        foreach (var w in Weapons.Values) w.Tick(dt);
        if (_restoreCollision)
        {
            _restoreCollision = false;
            CollisionLayer = Layers.Characters;
            CollisionMask = Layers.Solid | Layers.Horses;
        }

        if (IsDead)
        {
            _deadTimer -= dt;
            Velocity = new Vector3(0, Velocity.Y - Gravity * dt, 0);
            MoveAndSlide();
            if (_deadTimer <= 0f) Respawn();
            return;
        }

        _sinceHit += dt;
        if (_sinceHit > 6f && Health < MaxHealth) { _regen += dt * 4f; if (_regen >= 1f) { Health = Mathf.Min(MaxHealth, Health + (int)_regen); _regen -= (int)_regen; } }

        bool locked = game.InputLocked;
        var move = locked ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        bool sprint = !locked && Input.IsActionPressed("sprint");
        Aiming = !locked && Input.IsActionPressed("aim") && CurrentWeapon != null;
        Camera.Aiming = Aiming;
        _model.SetAiming(Aiming);

        // Dead Eye: Q while aiming slows the world while the meter lasts
        if (!locked && Input.IsActionJustPressed("deadeye") && Aiming && DeadEye > 5f) SetDeadEye(!DeadEyeActive);
        if (DeadEyeActive)
        {
            DeadEye -= 15f * dt / (float)Engine.TimeScale;
            if (!Aiming || DeadEye <= 0f) SetDeadEye(false);
        }
        else DeadEye = Mathf.Min(100f, DeadEye + 2.5f * dt);

        if (Driving != null)
        {
            Driving.SetDriverInput(move, !locked && Input.IsActionPressed("jump"), !locked && Input.IsActionPressed("whistle"));
            GlobalTransform = Driving.SeatTransform;
            Velocity = Vector3.Zero;
            Speed = Mathf.Abs(Driving.ForwardSpeed);
            Camera.FollowYawValue = Driving.Rotation.Y;
            if (!locked && Input.IsActionJustPressed("interact")) ExitVehicle();
            return;
        }

        if (!locked) HandleWeaponKeys();

        if (IsMounted)
        {
            Mounted!.SetRiderInput(move, sprint, !locked && Input.IsActionJustPressed("jump"), Camera);
            GlobalTransform = Mounted.SaddleTransform;
            Speed = Mounted.Velocity.Length();
            _model.Animate(Speed, dt, riding: true);
            if (!locked && Input.IsActionJustPressed("interact")) Dismount();
        }
        else
        {
            Walk(move, sprint, !locked && Input.IsActionJustPressed("jump"), dt);
            UpdateFocus();
            if (!locked && Input.IsActionJustPressed("interact") && Focus != null && Focus.CanInteract(this)) Focus.Interact(this);
        }

        if (!locked && Input.IsActionJustPressed("whistle"))
        {
            if (game.World?.Horse != null) game.World.Horse.Whistle(this);
            else if (game.World?.PlayerCar != null && Driving == null) game.World.PlayerCar.RecallTo(this);
        }
        if (!locked && Input.IsActionPressed("shoot")) TryShoot();
    }

    private float _regen;

    private void HandleWeaponKeys()
    {
        if (Input.IsActionJustPressed("weapon_1") && WeaponOrder.Count > 0) Equip(WeaponOrder[0]);
        if (Input.IsActionJustPressed("weapon_2") && WeaponOrder.Count > 1) Equip(WeaponOrder[1]);
        if (Input.IsActionJustPressed("holster")) Equip(null);
        if (Input.IsActionJustPressed("reload") && CurrentWeapon != null && CurrentWeapon.StartReload())
            Game.Instance.Main?.Sfx.Click(GlobalPosition);
    }

    private void Walk(Vector2 move, bool sprint, bool jump, float dt)
    {
        var dir = (Camera.FlatForward * -move.Y + Camera.FlatRight * move.X);
        if (dir.LengthSquared() > 1f) dir = dir.Normalized();
        bool wantSprint = sprint && move.LengthSquared() > 0.01f && Stamina > 1f && !Aiming;
        float speed = Aiming ? WalkSpeed * 0.6f : wantSprint ? SprintSpeed : WalkSpeed;
        if (wantSprint) Stamina = Mathf.Max(0f, Stamina - 14f * dt);
        else Stamina = Mathf.Min(100f, Stamina + 10f * dt);

        var v = Velocity;
        var target = dir * speed;
        float accel = IsOnFloor() ? 12f : 3f;
        v.X = Mathf.Lerp(v.X, target.X, 1f - Mathf.Exp(-accel * dt));
        v.Z = Mathf.Lerp(v.Z, target.Z, 1f - Mathf.Exp(-accel * dt));
        if (IsOnFloor())
        {
            if (jump) v.Y = JumpSpeed;
            else v.Y = -0.5f;
        }
        else v.Y -= Gravity * dt;
        Velocity = v;
        MoveAndSlide();

        Speed = new Vector2(Velocity.X, Velocity.Z).Length();
        // face the camera when aiming, otherwise face where we walk
        float wantYaw = Aiming ? Camera.Yaw : (dir.LengthSquared() > 0.01f ? Mathf.Atan2(-dir.X, -dir.Z) : Rotation.Y);
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, wantYaw, 1f - Mathf.Exp(-(Aiming ? 25f : 12f) * dt)), 0);
        _model.Animate(Speed, dt);

        _footstep += Speed * dt;
        if (_footstep > 2.2f && IsOnFloor()) { _footstep = 0f; Game.Instance.Main?.Sfx.Step(GlobalPosition); }
    }

    private void UpdateFocus()
    {
        IInteractable? best = null;
        float bestScore = float.MaxValue;
        var world = Game.Instance.World;
        if (world == null) return;
        var eye = GlobalPosition + new Vector3(0, 1.4f, 0);
        var fwd = Camera.FlatForward;
        void Consider(Node3D node, IInteractable it, float reach = 3.2f)
        {
            var to = node.GlobalPosition - eye;
            float d = new Vector2(to.X, to.Z).Length();
            if (d > reach) return;
            float facing = new Vector2(to.X, to.Z).Normalized().Dot(new Vector2(fwd.X, fwd.Z));
            if (facing < 0.2f && d > 1.2f) return;
            float score = d - facing;
            if (score < bestScore && it.CanInteract(this)) { bestScore = score; best = it; }
        }
        foreach (var n in world.Npcs) Consider(n, n);
        if (world.Horse != null) Consider(world.Horse, world.Horse);
        foreach (var v in world.Vehicles) if (v.GlobalPosition.DistanceTo(GlobalPosition) < 8f) Consider(v, v, 4.8f);
        Focus = best;
        Game.Instance.Hud?.SetPrompt(best == null ? "" : "[E] " + best.Prompt);
    }

    private void TryShoot()
    {
        var w = CurrentWeapon;
        if (w == null) return;
        if (!w.CanFire)
        {
            if (w.InMag == 0 && !w.Reloading && Input.IsActionJustPressed("shoot"))
            {
                if (!w.StartReload()) Game.Instance.Notify("Out of ammunition for the " + w.Data.Name + ".");
                else Game.Instance.Main?.Sfx.Click(GlobalPosition);
            }
            return;
        }
        if (w.Data.Kind == "rifle" && !Input.IsActionJustPressed("shoot")) return; // bolt action: one shot per click

        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        if (Mounted != null) exclude.Add(Mounted.GetRid());
        // aim from the camera so the crosshair is honest; start a little in front of the lens
        var origin = Camera.AimOrigin + Camera.AimDirection * 0.5f;
        var shot = w.Fire(this, origin, Camera.AimDirection, Weapon.HitSpread(w.Data, Aiming), exclude);
        ShotEffects.Show(GetParent(), shot, _model.MuzzleWorld);
        Game.Instance.Main?.Sfx.Gunshot(w.Data.Kind, GlobalPosition);
        Game.Instance.Wanted.OnGunfire(GlobalPosition, this);
        if (shot.Hit && shot.Collider is Npc npc) npc.TakeDamage(w.Data.Damage, this);
        else if (shot.Hit && shot.Collider is Horse horse) horse.Spook();
        Camera.Pitch += 0.012f * (w.Data.Kind == "rifle" ? 1.6f : 1f);
    }
}
