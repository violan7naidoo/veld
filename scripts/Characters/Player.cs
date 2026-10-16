using System.Collections.Generic;
using Godot;
using Veld.Core;
using Veld.Gameplay;
using Veld.Sim;
using Veld.Sim.Characters;

namespace Veld.Characters;

/// <summary>
/// Body and view of the player: third-person movement relative to the camera, sprint and jump, aiming
/// and the hitscan shot, interaction with anything that implements IInteractable, riding (the body is
/// parked on the horse's saddle and input is forwarded to the horse) and driving. Health, stamina,
/// Dead Eye, the weapons and death live in its PlayerState; the body feeds it each frame's input and
/// renders death, respawn and Dead Eye through the state's events.
/// </summary>
public partial class Player : CharacterBody3D, IPlayerView
{
    public CameraRig Camera { get; set; } = null!;
    public PlayerState State { get; private set; } = null!;
    Vector3 IPlayerView.Position => GlobalPosition;
    public void Heal() => State.Heal();
    public void GiveAmmo(string weaponId, int count) => State.GiveAmmo(weaponId, count);
    public int Health { get => State.Health; set => State.Health = value; }
    public float Stamina => State.Stamina;
    public bool IsDead => State.IsDead;
    public float DeadEye => State.DeadEye;
    public bool DeadEyeActive => State.DeadEyeActive;
    public IReadOnlyList<WeaponState> Weapons => State.Weapons;
    public string? CurrentWeaponId => State.CurrentWeaponId;
    public WeaponState? CurrentWeapon => State.CurrentWeapon;
    public Horse? Mounted { get; private set; }
    public bool IsMounted => Mounted != null;
    public Vehicle? Driving { get; private set; }
    public bool Aiming { get; private set; }
    public IInteractable? Focus { get; private set; }
    public float Speed { get; private set; }

    private HumanoidModel _model = null!;
    private CollisionShape3D _shape = null!;
    private float _footstep;
    private bool _restoreCollision;
    private const float WalkSpeed = 3.4f;
    private const float SprintSpeed = 6.6f;
    private const float JumpSpeed = 4.6f;
    private const float Gravity = 9.8f;
    private Main _main = null!;
    private GameData _data = null!;
    private UiState _ui = null!;

    /// <summary>Call between Instantiate and AddChild; the state already carries the map's weapons.</summary>
    public void Init(Main main, GameData data, PlayerState state, UiState ui)
    {
        _main = main;
        _data = data;
        State = state;
        _ui = ui;
    }

    public override void _Ready()
    {
        _shape = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.32f, Height = 1.8f }, Position = new Vector3(0, 0.9f, 0) };
        AddChild(_shape);
        _model = new HumanoidModel { Name = "Model" };
        _model.Configure(Faction.Civilian, 1, new Color(0.82f, 0.64f, 0.5f));
        AddChild(_model);
        FloorMaxAngle = Mathf.DegToRad(50f);
        FloorSnapLength = 0.4f;

        State.DeadEyeChanged += ShowDeadEye;
        State.Died += ShowDeath;
        State.Respawned += ShowRespawn;
        ShowWeapon();
    }

    // ------------------------------------------------------------------ equipment

    public void Equip(string? id)
    {
        if (State.Equip(id)) ShowWeapon();
    }

    private void ShowWeapon() => _model.ShowWeapon(State.CurrentWeapon?.Data.Kind);

    // ------------------------------------------------------------------ riding

    public void Mount(Horse horse)
    {
        if (IsMounted || IsDead) return;
        Mounted = horse;
        State.Mount = Veld.Sim.Characters.Mount.Riding;
        horse.SetRider(this);
        CollisionLayer = 0;
        CollisionMask = 0;
        Velocity = Vector3.Zero;
        Camera.TargetOffset = new Vector3(0, 2.1f, 0);
        _main.Hud.SetPrompt("");
    }

    public void Dismount()
    {
        if (Mounted == null) return;
        var horse = Mounted;
        Mounted = null;
        State.Mount = Veld.Sim.Characters.Mount.OnFoot;
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
        State.Mount = Veld.Sim.Characters.Mount.Driving;
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
        _main.Hud.SetPrompt("");
    }

    public void ExitVehicle()
    {
        if (Driving == null) return;
        var v = Driving;
        Driving = null;
        State.Mount = Veld.Sim.Characters.Mount.OnFoot;
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
        State.TakeDamage(amount);
        _main.Sfx.Hit(GlobalPosition);
        _main.Hud.FlashDamage();
    }

    private void ShowDeadEye(bool on)
    {
        Engine.TimeScale = on ? 0.3 : 1.0;
        _main.DayNight.DeadEye = on;
        _main.Sfx.Click(GlobalPosition);
    }

    private void ShowDeath()
    {
        Dismount();
        ExitVehicle();
        Aiming = false;
        Camera.Aiming = false;
        _model.SetDead(true);
    }

    private void ShowRespawn()
    {
        _model.SetDead(false);
        var spawn = DataLoader.XZ(_data.World.PlayerSpawn);
        var terrain = _main.World.Terrain;
        GlobalPosition = new Vector3(spawn.X, terrain.GetHeight(spawn.X, spawn.Y) + 1f, spawn.Y);
        Velocity = Vector3.Zero;
    }

    // ------------------------------------------------------------------ main loop

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        float realDt = dt / (float)Engine.TimeScale;
        if (_restoreCollision)
        {
            _restoreCollision = false;
            CollisionLayer = Layers.Characters;
            CollisionMask = Layers.Solid | Layers.Horses;
        }

        if (IsDead)
        {
            State.Tick(dt, realDt, aiming: false, wantSprint: false, moving: false, onFoot: false);
            Velocity = new Vector3(0, Velocity.Y - Gravity * dt, 0);
            MoveAndSlide();
            return;
        }

        bool locked = _ui.InputLocked;
        var move = locked ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        bool sprint = !locked && Input.IsActionPressed("sprint");
        Aiming = !locked && Input.IsActionPressed("aim") && CurrentWeapon != null;
        Camera.Aiming = Aiming;
        _model.SetAiming(Aiming);

        // Dead Eye: Q while aiming slows the world while the meter lasts
        if (!locked && Input.IsActionJustPressed("deadeye") && Aiming) State.ToggleDeadEye();
        State.Tick(dt, realDt, Aiming, sprint, move.LengthSquared() > 0.01f, onFoot: Driving == null && !IsMounted);

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
            Walk(move, !locked && Input.IsActionJustPressed("jump"), dt);
            UpdateFocus();
            if (!locked && Input.IsActionJustPressed("interact") && Focus != null && Focus.CanInteract(this)) Focus.Interact(this);
        }

        if (!locked && Input.IsActionJustPressed("whistle"))
        {
            if (_main.World.Horse != null) _main.World.Horse.Whistle(this);
            else if (_main.World.PlayerCar != null && Driving == null) _main.World.PlayerCar.RecallTo(this);
        }
        if (!locked && Input.IsActionPressed("shoot")) TryShoot();
    }

    private void HandleWeaponKeys()
    {
        if (Input.IsActionJustPressed("weapon_1") && Weapons.Count > 0) Equip(Weapons[0].Data.Id);
        if (Input.IsActionJustPressed("weapon_2") && Weapons.Count > 1) Equip(Weapons[1].Data.Id);
        if (Input.IsActionJustPressed("holster")) Equip(null);
        if (Input.IsActionJustPressed("reload") && State.Reload()) _main.Sfx.Click(GlobalPosition);
    }

    private void Walk(Vector2 move, bool jump, float dt)
    {
        var dir = (Camera.FlatForward * -move.Y + Camera.FlatRight * move.X);
        if (dir.LengthSquared() > 1f) dir = dir.Normalized();
        float speed = Aiming ? WalkSpeed * 0.6f : State.Sprinting ? SprintSpeed : WalkSpeed;

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
        if (_footstep > 2.2f && IsOnFloor()) { _footstep = 0f; _main.Sfx.Step(GlobalPosition); }
    }

    private void UpdateFocus()
    {
        IInteractable? best = null;
        float bestScore = float.MaxValue;
        var world = _main.World;
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
        _main.Hud.SetPrompt(best == null ? "" : "[E] " + best.Prompt);
    }

    private void TryShoot()
    {
        var w = CurrentWeapon;
        if (w == null) return;
        if (!w.CanFire)
        {
            if (w.InMag == 0 && !w.Reloading && Input.IsActionJustPressed("shoot") && State.Reload()) _main.Sfx.Click(GlobalPosition);
            return;
        }
        if (w.Data.Kind == "rifle" && !Input.IsActionJustPressed("shoot")) return; // bolt action: one shot per click

        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        if (Mounted != null) exclude.Add(Mounted.GetRid());
        // aim from the camera so the crosshair is honest; start a little in front of the lens
        var origin = Camera.AimOrigin + Camera.AimDirection * 0.5f;
        var shot = Shooting.Fire(this, w, origin, Camera.AimDirection, w.HitSpread(Aiming), exclude);
        ShotEffects.Show(GetParent(), shot, _model.MuzzleWorld);
        _main.Sfx.Gunshot(w.Data.Kind, GlobalPosition);
        State.ReportGunfire(GlobalPosition);
        if (shot.Hit && shot.Collider is Npc npc) npc.TakeDamage(w.Data.Damage, this);
        else if (shot.Hit && shot.Collider is Horse horse) horse.Spook();
        Camera.Pitch += 0.012f * (w.Data.Kind == "rifle" ? 1.6f : 1f);
    }
}
