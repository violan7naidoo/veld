using Godot;
using Veld.Sim.Missions;

namespace Veld.Sim.Characters;

/// <summary>
/// The decisions of one character: who it is hostile to, what a hit does to it, when it runs, what a
/// search of its body yields, and each tick the idle, wander, patrol, chase, attack and flee state
/// machine with the shot decision. Created by Simulation.CreateBrain for a body that reports through
/// INpcView and feeds Think its senses.
/// </summary>
public sealed class NpcBrain
{
    private const float WalkSpeed = 1.6f, RunSpeed = 5.2f;

    private readonly INpcView _self;
    private readonly IWorldQuery _world;
    private readonly WantedSystem _wanted;
    private readonly Reputation _reputation;
    private readonly MissionManager _missions;
    private readonly IEconomy _purse;
    private readonly INotifier _notifier;
    private readonly IRandom _random;
    private readonly WeaponData? _weapon;

    private Vector3 _home;
    private Vector3 _target;
    private float _wait;
    private int _patrolIndex;
    private float _stuck;
    private float _lostTimer;
    private float _reactTimer;
    private float _fleeTimer;

    internal NpcBrain(NpcData data, WeaponData? weapon, INpcView self, IWorldQuery world, WantedSystem wanted,
        Reputation reputation, MissionManager missions, IEconomy purse, INotifier notifier, IRandom random)
    {
        Data = data;
        Faction = Factions.Parse(data.Faction);
        _weapon = weapon;
        _self = self;
        _world = world;
        _wanted = wanted;
        _reputation = reputation;
        _missions = missions;
        _purse = purse;
        _notifier = notifier;
        _random = random;
    }

    public NpcData Data { get; }
    public Faction Faction { get; }
    public bool Armed => _weapon != null;
    public NpcState State { get; private set; } = NpcState.Idle;
    public int Health { get; private set; } = 100;
    public bool IsDead { get; private set; }
    /// <summary>Set when the player attacks this character or its group; the armed then fight regardless of faction.</summary>
    public bool Provoked { get; set; }
    public bool Looted { get; private set; }

    public bool IsHostile
    {
        get
        {
            if (IsDead) return false;
            if (Data.Hostile || Faction == Faction.Outlaw) return true;
            if (Provoked && Armed) return true;
            return Faction switch
            {
                Faction.British => _wanted.Level > 0,
                Faction.Boer => _reputation.ResidentsHostile,
                _ => false,
            };
        }
    }

    /// <summary>Where the body stands when it enters the world: a patrol route means patrolling, otherwise idling for a while.</summary>
    public void Settle(Vector3 home)
    {
        _home = home;
        _target = home;
        State = Data.Patrol.Count > 1 ? NpcState.Patrol : NpcState.Idle;
        _wait = _random.Range(0.5f, 4f);
    }

    /// <summary>Returns true when this blow killed. A player's hit provokes, is reported, costs standing and makes the unarmed run.</summary>
    public bool TakeDamage(int amount, IPlayerView? byPlayer, Vector3 from)
    {
        if (IsDead) return false;
        Health -= amount;
        bool killed = Health <= 0;
        if (byPlayer != null)
        {
            Provoked = true;
            if (!string.IsNullOrEmpty(Data.Group))
                foreach (var n in _world.Group(Data.Group)) n.Provoked = true;
            _wanted.ReportAttack(_self, byPlayer, killed);
            if (Faction is Faction.Civilian or Faction.Boer) _reputation.Lose(killed ? 2 : 1);
            if (!Armed) StartFlee(from);
            else _reactTimer = 0.2f;
        }
        if (killed) Die();
        return killed;
    }

    private void Die()
    {
        IsDead = true;
        Health = 0;
        State = NpcState.Dead;
        _missions.OnNpcKilled(_self);
    }

    /// <summary>A save says this character is dead: no effects, no mission event, nothing left to find.</summary>
    public void MarkDeadSilently()
    {
        IsDead = true;
        Health = 0;
        State = NpcState.Dead;
        Looted = true;
    }

    /// <summary>Gunfire nearby: the unarmed run, outlaws excepted.</summary>
    public void Alert(Vector3 from)
    {
        if (IsDead) return;
        if (!Armed && Faction != Faction.Outlaw) StartFlee(from);
    }

    private void StartFlee(Vector3 from)
    {
        State = NpcState.Flee;
        _fleeTimer = 8f;
        var away = _self.Position - from;
        away.Y = 0;
        if (away.LengthSquared() < 0.01f) away = Vector3.Forward;
        _target = _self.Position + away.Normalized() * 40f;
    }

    /// <summary>The body fired a round: the wanted system hears it and nearby townsfolk scatter.</summary>
    public void ReportGunfire() => _wanted.OnGunfire(_self.Position, _self);

    /// <summary>Searching the body: pence found, paid into the purse with the coins dropped here; 0 when there is nothing left.</summary>
    public int Loot()
    {
        if (!IsDead || Looted) return 0;
        Looted = true;
        int pence = Faction == Faction.Outlaw ? _random.Range(18, 60) : _random.Range(1, 9);
        if (Faction != Faction.Outlaw) _reputation.Lose(1);
        _purse.Add(pence);
        _notifier.Coins(_self.Position);
        _notifier.Notify("You find " + Money.Format(pence) + " on " + Data.Name + ".");
        return pence;
    }

    /// <summary>One tick of decisions from what the body senses: the state machine, where to face and how fast, whether to shoot.</summary>
    public NpcIntent Think(in NpcSenses s, float dt)
    {
        if (IsDead) return new NpcIntent(NpcState.Dead, true, null, 0f, false, 0f, false, false);
        var toPlayer = s.PlayerPosition - s.Position;
        float dist = toPlayer.Length();
        bool showLabel = dist < 14f && !IsHostile;
        if (dist > 220f && State is NpcState.Idle or NpcState.Wander or NpcState.Patrol)
            return new NpcIntent(State, true, null, 0f, false, 0f, false, false);

        bool hostile = IsHostile && !s.PlayerDead;
        bool canSee = hostile && dist < 90f && s.CanSeePlayer();

        if (State == NpcState.Flee)
        {
            _fleeTimer -= dt;
            if (_fleeTimer <= 0f || s.Position.DistanceTo(_target) < 2f) { State = PassiveState(); _wait = 2f; }
        }
        else if (hostile && !Armed)
        {
            if (dist < 25f) StartFlee(s.PlayerPosition);
        }
        else if (hostile && (canSee || dist < 12f || (State is NpcState.Chase or NpcState.Attack && _lostTimer < 8f)))
        {
            if (canSee || dist < 12f) _lostTimer = 0f; else _lostTimer += dt;
            float engage = Mathf.Min(_weapon!.Range * 0.4f, 55f);
            State = canSee && dist < engage ? NpcState.Attack : NpcState.Chase;
            _target = s.PlayerPosition;
        }
        else if (State is NpcState.Chase or NpcState.Attack)
        {
            State = PassiveState();
            _wait = 1f;
        }

        float speed = 0f;
        Vector3? faceTo = null;
        bool fire = false, reload = false;
        float spread = 0f;
        switch (State)
        {
            case NpcState.Idle:
                _wait -= dt;
                if (_wait <= 0f && Data.WanderRadius > 0.5f)
                {
                    float r = Data.WanderRadius;
                    _target = _home + new Vector3(_random.Range(-r, r), 0, _random.Range(-r, r));
                    State = NpcState.Wander;
                }
                else if (_wait <= 0f) _wait = 3f;
                break;
            case NpcState.Wander:
                if (Flat(_target - s.Position).Length() < 1f) { State = NpcState.Idle; _wait = _random.Range(2f, 7f); break; }
                speed = WalkSpeed; faceTo = _target;
                break;
            case NpcState.Patrol:
            {
                var p = _world.Ground(DataLoader.XZ(Data.Patrol[_patrolIndex]));
                if (Flat(p - s.Position).Length() < 1.2f)
                {
                    _wait -= dt;
                    if (_wait <= 0f) { _patrolIndex = (_patrolIndex + 1) % Data.Patrol.Count; _wait = 1.5f; }
                }
                else { _target = p; speed = WalkSpeed; faceTo = p; }
                break;
            }
            case NpcState.Chase:
                speed = RunSpeed; faceTo = _target;
                break;
            case NpcState.Attack:
                faceTo = s.PlayerPosition;
                if (dist < 6f) speed = -WalkSpeed;
                _reactTimer -= dt;
                if (s.MagEmpty) reload = true;
                else if (s.CanFire && _reactTimer <= 0f)
                {
                    // soldiers are trained; outlaws and burghers are good shots at range, everyone misses more when the target runs
                    float baseSpread = Faction switch { Faction.British => 2.2f, Faction.Boer => 1.6f, _ => 2.6f };
                    spread = baseSpread + dist * 0.03f + (s.PlayerSpeed > 3f ? 2.5f : 0f) + (s.PlayerMounted ? 1.5f : 0f);
                    fire = true;
                    ReportGunfire();
                    _reactTimer = _random.Range(0.4f, 1.4f);
                }
                break;
            case NpcState.Flee:
                speed = RunSpeed; faceTo = _target;
                break;
        }

        if (Mathf.Abs(speed) > 0.5f && s.Speed < 0.3f)
        {
            _stuck += dt;
            if (_stuck > 1.2f)
            {
                _stuck = 0f;
                // sidestep: pick a point off to one side and go there first
                var side = s.Right * (_random.Range(0f, 1f) > 0.5f ? 4f : -4f);
                if (State is NpcState.Wander or NpcState.Patrol) { State = NpcState.Idle; _wait = 1f; }
                else _target = s.Position + side;
            }
        }
        else _stuck = 0f;

        return new NpcIntent(State, false, faceTo, speed, fire, spread, reload, showLabel);
    }

    private NpcState PassiveState() => Data.Patrol.Count > 1 ? NpcState.Patrol : NpcState.Idle;

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
