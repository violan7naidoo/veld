using Godot;
using Veld.Core;
using Veld.Gameplay;

namespace Veld.Characters;

/// <summary>
/// Everyone who is not the player: townsfolk, burghers, British soldiers and outlaws. A small state
/// machine (idle, wander, patrol, chase, attack, flee, dead) driven by faction hostility. Armed NPCs
/// shoot back with the same Weapon class the player uses. Talkable NPCs implement IInteractable and
/// open their dialogue from data/dialogue.json.
/// </summary>
public partial class Npc : CharacterBody3D, IInteractable, INpcView
{
    public NpcData Data { get; set; } = new();
    public Faction Faction { get; private set; }
    public int Health { get; private set; } = 100;
    public bool IsDead { get; private set; }
    public Weapon? Weapon { get; private set; }
    /// <summary>Set when the player attacks this NPC (or its group); armed NPCs then fight regardless of faction.</summary>
    public bool Provoked { get; set; }

    // the gameplay systems see this character through INpcView; explicit so Node.Name stays Godot's
    string INpcView.Id => Data.Id;
    string INpcView.Name => Data.Name;
    string INpcView.Group => Data.Group;
    Vector3 INpcView.Position => GlobalPosition;

    private enum State { Idle, Wander, Patrol, Chase, Attack, Flee, Dead }
    private State _state = State.Idle;

    private HumanoidModel _model = null!;
    private Label3D _label = null!;
    private Vector3 _home;
    private Vector3 _target;
    private float _wait;
    private int _patrolIndex;
    private float _stuck;
    private float _lostTimer;
    private float _reactTimer;
    private float _fleeTimer;
    private bool _looted;
    private bool _pendingDead;
    private const float Gravity = 9.8f;
    private const float WalkSpeed = 1.6f, RunSpeed = 5.2f;

    public override void _Ready()
    {
        Faction = Factions.Parse(Data.Faction);
        int variant = 0;
        foreach (char c in Data.Id) variant += c;
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.32f, Height = 1.8f }, Position = new Vector3(0, 0.9f, 0) });
        FloorMaxAngle = Mathf.DegToRad(50f);
        FloorSnapLength = 0.4f;

        _model = new HumanoidModel { Name = "Model" };
        _model.Configure(Faction, variant);
        AddChild(_model);

        if (!string.IsNullOrEmpty(Data.Weapon) && Game.Instance.Data.Weapons.TryGetValue(Data.Weapon, out var wd))
        {
            Weapon = new Weapon(wd) { Reserve = 999 };
            _model.ShowWeapon(wd.Kind);
        }

        _label = new Label3D
        {
            Text = Data.Name, Position = new Vector3(0, 2.15f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 36, PixelSize = 0.008f, Modulate = new Color(1f, 0.95f, 0.85f), OutlineSize = 8, Visible = false,
            NoDepthTest = true,
        };
        AddChild(_label);

        _home = GlobalPosition;
        _target = _home;
        _state = Data.Patrol.Count > 1 ? State.Patrol : State.Idle;
        _wait = (float)GD.RandRange(0.5, 4.0);
        if (_pendingDead) MarkDeadSilently();
    }

    // ------------------------------------------------------------------ interaction

    public string Prompt => IsDead ? "Search body" : "Talk to " + Data.Name;

    public bool CanInteract(Player player)
    {
        if (IsDead) return !_looted;
        return !string.IsNullOrEmpty(Data.Dialogue) && !IsHostileTo(player) && _state != State.Flee;
    }

    public void Interact(Player player)
    {
        if (IsDead)
        {
            _looted = true;
            int pence = Faction == Faction.Outlaw ? GD.RandRange(18, 60) : GD.RandRange(1, 9);
            if (Faction != Faction.Outlaw) Game.Instance.Reputation -= 1;
            Game.Instance.AddMoney(pence);
            Game.Instance.Main?.Sfx.Coins(GlobalPosition);
            Game.Instance.Notify("You find " + Money.Format(pence) + " on " + Data.Name + ".");
            return;
        }
        Game.Instance.Dialogue.Start(Data.Dialogue, this);
    }

    // ------------------------------------------------------------------ hostility

    public bool IsHostileTo(Player player)
    {
        if (IsDead) return false;
        if (Data.Hostile || Faction == Faction.Outlaw) return true;
        if (Provoked && Weapon != null) return true;
        return Faction switch
        {
            Faction.British => Game.Instance.Wanted.Level > 0,
            Faction.Boer => Game.Instance.Reputation < -2,
            _ => false,
        };
    }

    public void TakeDamage(int amount, Node? attacker)
    {
        if (IsDead) return;
        Health -= amount;
        bool killed = Health <= 0;
        if (attacker is Player p)
        {
            Provoked = true;
            if (Game.Instance.World != null && !string.IsNullOrEmpty(Data.Group))
                foreach (var n in Game.Instance.World.Group(Data.Group)) n.Provoked = true;
            Game.Instance.Wanted.ReportAttack(this, p, killed);
            if (Faction is Faction.Civilian or Faction.Boer) Game.Instance.Reputation -= killed ? 2 : 1;
            if (Weapon == null) StartFlee(p.GlobalPosition);
            else _reactTimer = 0.2f;
        }
        if (killed) Die();
    }

    private void Die()
    {
        IsDead = true;
        Health = 0;
        _state = State.Dead;
        _model.SetDead(true);
        _label.Visible = false;
        CollisionLayer = 0;
        CollisionMask = Layers.Terrain;
        Velocity = Vector3.Zero;
        Game.Instance.DeadNpcs.Add(Data.Id);
        Game.Instance.Missions.OnNpcKilled(this);
    }

    /// <summary>Used when loading a save: the NPC is already dead, no effects, no mission events.</summary>
    public void MarkDeadSilently()
    {
        if (_model == null) { _pendingDead = true; return; }
        IsDead = true;
        Health = 0;
        _state = State.Dead;
        _looted = true;
        _model.SetDead(true);
        _label.Visible = false;
        CollisionLayer = 0;
        CollisionMask = Layers.Terrain;
    }

    public void Alert(Vector3 from)
    {
        if (IsDead) return;
        if (Weapon == null && Faction != Faction.Outlaw) StartFlee(from);
    }

    private void StartFlee(Vector3 from)
    {
        _state = State.Flee;
        _fleeTimer = 8f;
        var away = (GlobalPosition - from); away.Y = 0;
        if (away.LengthSquared() < 0.01f) away = Vector3.Forward;
        _target = GlobalPosition + away.Normalized() * 40f;
    }

    // ------------------------------------------------------------------ loop

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (IsDead)
        {
            Velocity = new Vector3(0, IsOnFloor() ? 0 : Velocity.Y - Gravity * dt, 0);
            MoveAndSlide();
            return;
        }
        Weapon?.Tick(dt);
        var player = Game.Instance.Player;
        if (player == null) return;
        var toPlayer = player.GlobalPosition - GlobalPosition;
        float dist = toPlayer.Length();
        _label.Visible = dist < 14f && !IsHostileTo(player);
        if (dist > 220f && _state is State.Idle or State.Wander or State.Patrol)
        {
            // far away: do not simulate, just rest on the ground
            Velocity = new Vector3(0, IsOnFloor() ? 0 : Velocity.Y - Gravity * dt, 0);
            MoveAndSlide();
            return;
        }

        bool hostile = IsHostileTo(player) && !player.IsDead;
        bool canSee = hostile && dist < 90f && CanSee(player);

        if (_state == State.Flee)
        {
            _fleeTimer -= dt;
            if (_fleeTimer <= 0f || GlobalPosition.DistanceTo(_target) < 2f) { _state = Data.Patrol.Count > 1 ? State.Patrol : State.Idle; _wait = 2f; }
        }
        else if (hostile && Weapon == null)
        {
            if (dist < 25f) StartFlee(player.GlobalPosition);
        }
        else if (hostile && (canSee || dist < 12f || (_state is State.Chase or State.Attack && _lostTimer < 8f)))
        {
            if (canSee || dist < 12f) _lostTimer = 0f; else _lostTimer += dt;
            float engage = Mathf.Min(Weapon!.Data.Range * 0.4f, 55f);
            _state = canSee && dist < engage ? State.Attack : State.Chase;
            _target = player.GlobalPosition;
        }
        else if (_state is State.Chase or State.Attack)
        {
            _state = Data.Patrol.Count > 1 ? State.Patrol : State.Idle;
            _wait = 1f;
        }

        float wantSpeed = 0f;
        float wantYaw = Rotation.Y;
        switch (_state)
        {
            case State.Idle:
                _wait -= dt;
                if (_wait <= 0f && Data.WanderRadius > 0.5f)
                {
                    var rnd = new RandomNumberGenerator();
                    var p = _home + new Vector3(rnd.RandfRange(-Data.WanderRadius, Data.WanderRadius), 0, rnd.RandfRange(-Data.WanderRadius, Data.WanderRadius));
                    _target = p; _state = State.Wander;
                }
                else if (_wait <= 0f) _wait = 3f;
                break;
            case State.Wander:
                if (Flat(_target - GlobalPosition).Length() < 1f) { _state = State.Idle; _wait = (float)GD.RandRange(2.0, 7.0); break; }
                wantSpeed = WalkSpeed; wantYaw = YawTo(_target);
                break;
            case State.Patrol:
            {
                var p = Game.Instance.World!.Ground(DataLoader.XZ(Data.Patrol[_patrolIndex]));
                if (Flat(p - GlobalPosition).Length() < 1.2f)
                {
                    _wait -= dt;
                    if (_wait <= 0f) { _patrolIndex = (_patrolIndex + 1) % Data.Patrol.Count; _wait = 1.5f; }
                }
                else { _target = p; wantSpeed = WalkSpeed; wantYaw = YawTo(p); }
                break;
            }
            case State.Chase:
                wantSpeed = RunSpeed; wantYaw = YawTo(_target);
                break;
            case State.Attack:
                wantYaw = YawTo(player.GlobalPosition);
                if (dist < 6f) { wantSpeed = -WalkSpeed; } // back off
                Shoot(player, dist, dt);
                break;
            case State.Flee:
                wantSpeed = RunSpeed; wantYaw = YawTo(_target);
                break;
        }

        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, wantYaw, 1f - Mathf.Exp(-8f * dt)), 0);
        var forward = -GlobalTransform.Basis.Z;
        var v = Velocity;
        v.X = Mathf.Lerp(v.X, forward.X * wantSpeed, 1f - Mathf.Exp(-10f * dt));
        v.Z = Mathf.Lerp(v.Z, forward.Z * wantSpeed, 1f - Mathf.Exp(-10f * dt));
        v.Y = IsOnFloor() ? -0.5f : v.Y - Gravity * dt;
        Velocity = v;
        MoveAndSlide();

        if (Mathf.Abs(wantSpeed) > 0.5f && new Vector2(Velocity.X, Velocity.Z).Length() < 0.3f)
        {
            _stuck += dt;
            if (_stuck > 1.2f)
            {
                _stuck = 0f;
                // sidestep: pick a point off to one side and go there first
                var side = GlobalTransform.Basis.X * (GD.Randf() > 0.5f ? 4f : -4f);
                if (_state is State.Wander or State.Patrol) { _state = State.Idle; _wait = 1f; }
                else _target = GlobalPosition + side;
            }
        }
        else _stuck = 0f;

        _model.SetAiming(_state == State.Attack);
        _model.Animate(new Vector2(Velocity.X, Velocity.Z).Length(), dt);
    }

    private bool CanSee(Player player)
    {
        var from = GlobalPosition + new Vector3(0, 1.6f, 0);
        var to = player.GlobalPosition + new Vector3(0, 1.2f, 0);
        var q = PhysicsRayQueryParameters3D.Create(from, to, Layers.Solid, new Godot.Collections.Array<Rid> { GetRid() });
        return GetWorld3D().DirectSpaceState.IntersectRay(q).Count == 0;
    }

    private void Shoot(Player player, float dist, float dt)
    {
        var w = Weapon!;
        _reactTimer -= dt;
        if (w.InMag == 0) { w.StartReload(); return; }
        if (!w.CanFire || _reactTimer > 0f) return;
        // soldiers are trained; outlaws and burghers are good shots at range, everyone misses more when the target runs
        float baseSpread = Faction switch { Faction.British => 2.2f, Faction.Boer => 1.6f, _ => 2.6f };
        float spread = baseSpread + dist * 0.03f + (player.Speed > 3f ? 2.5f : 0f) + (player.IsMounted ? 1.5f : 0f);
        var muzzle = _model.MuzzleWorld;
        var aimAt = player.GlobalPosition + new Vector3(0, player.IsMounted ? 1.5f : 1.1f, 0);
        var shot = w.Fire(this, muzzle, aimAt - muzzle, spread, new Godot.Collections.Array<Rid> { GetRid() });
        ShotEffects.Show(GetParent(), shot, muzzle);
        Game.Instance.Main?.Sfx.Gunshot(w.Data.Kind, GlobalPosition);
        Game.Instance.Wanted.OnGunfire(GlobalPosition, this);
        if (shot.Hit && shot.Collider is Player p) p.TakeDamage(Mathf.Max(8, w.Data.Damage / 3), this);
        else if (shot.Hit && shot.Collider is Npc other && other != this) other.TakeDamage(w.Data.Damage / 2, this);
        _reactTimer = (float)GD.RandRange(0.4, 1.4);
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
    private float YawTo(Vector3 target)
    {
        var d = Flat(target - GlobalPosition);
        return d.LengthSquared() < 0.001f ? Rotation.Y : Mathf.Atan2(-d.X, -d.Z);
    }
}
