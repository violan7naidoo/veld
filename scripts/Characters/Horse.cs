using Godot;
using Veld.Core;
using Veld.Gameplay;
using Veld.Sim;
using Veld.World;

namespace Veld.Characters;

/// <summary>
/// The player's horse. Stands where it was left, wanders a little, comes when whistled (H), and
/// carries the rider when mounted: the rider's input is turned into "turn towards where the camera
/// points and go", with trot / gallop speeds and a stamina bar. Placeholder model built from capsules
/// with jointed legs; swap for a rigged GLB the same way as HumanoidModel, keeping the Saddle node.
/// </summary>
public partial class Horse : CharacterBody3D, IInteractable
{
    public Player? Rider { get; private set; }
    public bool IsRidden => Rider != null;
    public float Stamina { get; private set; } = 100f;
    public string HorseName { get; set; } = "Vos";
    public Transform3D SaddleTransform => _saddle.GlobalTransform;

    private enum State { Idle, Wander, Coming, Ridden, Spooked }
    private State _state = State.Idle;

    private Node3D _body = null!;
    private Node3D _saddle = null!;
    private Node3D _head = null!;
    private Node3D _neck = null!;
    private Node3D _tail = null!;
    private readonly Node3D[] _legs = new Node3D[4];
    private readonly Node3D[] _shins = new Node3D[4];
    private CpuParticles3D _dust = null!;
    private Vector3 _home;
    private Vector3 _target;
    private float _wait;
    private float _phase;
    private float _hoof;
    private float _stuck;
    private float _speed; // current forward speed
    private float _prevYaw, _yawRate, _lean;
    private const float Walk = 2.2f, Trot = 6.5f, Gallop = 12.5f, Gravity = 9.8f;
    private Main _main = null!;
    private GameData _data = null!;
    private Notifier _notifier = null!;

    /// <summary>Call between Instantiate and AddChild; _Ready reads the horse's name from the data.</summary>
    public void Init(Main main, GameData data, Notifier notifier)
    {
        _main = main;
        _data = data;
        _notifier = notifier;
    }

    public override void _Ready()
    {
        HorseName = _data.World.HorseName;
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.45f, Height = 2.0f }, Position = new Vector3(0, 0.95f, 0), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
        FloorMaxAngle = Mathf.DegToRad(50f);
        FloorSnapLength = 0.5f;
        BuildModel();
        _home = GlobalPosition;
        _target = _home;
    }

    private void BuildModel()
    {
        var coat = Props.Material(new Color(0.42f, 0.26f, 0.15f), 0.7f);
        var dark = Props.Material(new Color(0.16f, 0.1f, 0.06f), 0.8f);
        var leather = Props.Material(new Color(0.36f, 0.2f, 0.1f), 0.45f);
        var blanket = Props.Cloth(new Color(0.6f, 0.12f, 0.12f));
        var iron = Props.Material(Props.Iron, 0.4f, 0.7f);

        _body = new Node3D { Name = "Body" };
        AddChild(_body);
        var barrel = Props.Capsule(0.36f, 1.75f, coat, new Vector3(0, 1.15f, 0));
        barrel.Rotation = new Vector3(Mathf.Pi / 2, 0, 0);
        _body.AddChild(barrel);
        _body.AddChild(Props.Sphere(0.37f, coat, new Vector3(0, 1.17f, -0.72f), 0.95f));
        _body.AddChild(Props.Sphere(0.36f, coat, new Vector3(0, 1.2f, 0.7f), 0.95f));

        _neck = new Node3D { Name = "Neck", Position = new Vector3(0, 1.38f, -0.8f) };
        var neckDir = new Vector3(0, Mathf.Cos(0.95f), -Mathf.Sin(0.95f)); // 54 degrees forward of vertical
        var neck = Props.Capsule(0.19f, 0.95f, coat, neckDir * 0.42f);
        neck.Rotation = new Vector3(-0.95f, 0, 0);
        _neck.AddChild(neck);
        for (int i = 0; i < 6; i++)
            _neck.AddChild(Props.Box(new Vector3(0.08f, 0.16f, 0.14f), dark, neckDir * (0.1f + i * 0.13f) + new Vector3(0, 0.14f, 0.1f)));
        _body.AddChild(_neck);

        _head = new Node3D { Name = "Head", Position = neckDir * 0.9f + new Vector3(0, 0.04f, 0) };
        _head.AddChild(Props.Box(new Vector3(0.23f, 0.26f, 0.34f), coat, new Vector3(0, 0, -0.05f)));
        _head.AddChild(Props.Box(new Vector3(0.17f, 0.17f, 0.3f), coat, new Vector3(0, -0.07f, -0.36f)));
        _head.AddChild(Props.Box(new Vector3(0.16f, 0.06f, 0.08f), dark, new Vector3(0, -0.12f, -0.49f)));
        for (int s = -1; s <= 1; s += 2)
        {
            _head.AddChild(Props.Sphere(0.03f, Props.Material(new Color(0.05f, 0.04f, 0.03f)), new Vector3(s * 0.11f, 0.06f, -0.1f)));
            var ear = Props.Prism(new Vector3(0.07f, 0.16f, 0.05f), coat, new Vector3(s * 0.08f, 0.2f, 0.04f));
            ear.Rotation = new Vector3(0, 0, s * -0.2f);
            _head.AddChild(ear);
        }
        // halter straps
        _head.AddChild(Props.Box(new Vector3(0.2f, 0.025f, 0.32f), leather, new Vector3(0, 0.02f, -0.3f)));
        _head.AddChild(Props.Box(new Vector3(0.025f, 0.3f, 0.025f), leather, new Vector3(0.12f, -0.04f, -0.2f)));
        _head.AddChild(Props.Box(new Vector3(0.025f, 0.3f, 0.025f), leather, new Vector3(-0.12f, -0.04f, -0.2f)));
        _head.AddChild(Props.Box(new Vector3(0.1f, 0.12f, 0.06f), dark, new Vector3(0, 0.18f, -0.1f))); // forelock
        _neck.AddChild(_head);

        _tail = new Node3D { Name = "Tail", Position = new Vector3(0, 1.4f, 0.98f) };
        var tail = Props.Capsule(0.07f, 0.85f, dark, new Vector3(0, -0.38f, 0.1f));
        _tail.AddChild(tail);
        _body.AddChild(_tail);

        // saddle, blanket, stirrups
        _body.AddChild(Props.Box(new Vector3(0.78f, 0.06f, 0.82f), blanket, new Vector3(0, 1.5f, -0.05f)));
        _body.AddChild(Props.Box(new Vector3(0.5f, 0.1f, 0.56f), leather, new Vector3(0, 1.58f, -0.05f)));
        _body.AddChild(Props.Box(new Vector3(0.22f, 0.14f, 0.12f), leather, new Vector3(0, 1.66f, -0.3f)));
        _body.AddChild(Props.Box(new Vector3(0.34f, 0.16f, 0.1f), leather, new Vector3(0, 1.67f, 0.2f)));
        for (int s = -1; s <= 1; s += 2)
        {
            _body.AddChild(Props.Box(new Vector3(0.04f, 0.5f, 0.06f), leather, new Vector3(s * 0.38f, 1.32f, -0.05f)));
            _body.AddChild(Props.Box(new Vector3(0.13f, 0.11f, 0.03f), iron, new Vector3(s * 0.38f, 1.05f, -0.05f)));
        }
        _body.AddChild(Props.Box(new Vector3(0.8f, 0.04f, 0.08f), leather, new Vector3(0, 0.82f, -0.05f)));
        _saddle = new Node3D { Name = "Saddle", Position = new Vector3(0, 0.78f, -0.05f) };
        _body.AddChild(_saddle);

        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0 ? -0.22f : 0.22f);
            float z = (i < 2 ? -0.62f : 0.66f);
            var leg = new Node3D { Position = new Vector3(x, 0.95f, z) };
            leg.AddChild(Props.Capsule(0.1f, 0.52f, coat, new Vector3(0, -0.22f, 0)));
            var shin = new Node3D { Position = new Vector3(0, -0.46f, 0) };
            shin.AddChild(Props.Capsule(0.065f, 0.5f, coat, new Vector3(0, -0.22f, 0)));
            shin.AddChild(Props.Cylinder(0.08f, 0.09f, dark, new Vector3(0, -0.45f, 0), -1, 8));
            leg.AddChild(shin);
            AddChild(leg);
            _legs[i] = leg;
            _shins[i] = shin;
        }

        _dust = new CpuParticles3D
        {
            Position = new Vector3(0, 0.15f, 0.6f), Amount = 28, Lifetime = 1.4f, Emitting = false,
            Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f, RadialSegments = 5, Rings = 3, Material = Props.ParticleMaterial() },
            Direction = new Vector3(0, 0.6f, 1f), Spread = 35f, InitialVelocityMin = 1.0f, InitialVelocityMax = 2.6f,
            Gravity = new Vector3(0, -0.4f, 0), ScaleAmountMin = 0.6f, ScaleAmountMax = 2.6f, Color = new Color(0.72f, 0.6f, 0.44f, 0.4f),
        };
        AddChild(_dust);
    }

    // ------------------------------------------------------------------ interaction

    public string Prompt => "Mount " + HorseName;
    public bool CanInteract(Player player) => !IsRidden && !player.IsMounted && _speed < 1f;
    public void Interact(Player player) => player.Mount(this);

    public void SetRider(Player? rider)
    {
        Rider = rider;
        _state = rider != null ? State.Ridden : State.Idle;
        if (rider == null) { _home = GlobalPosition; _target = _home; _wait = 3f; }
    }

    public void Whistle(Player player)
    {
        if (IsRidden) return;
        _main.Sfx.Whistle(player.GlobalPosition);
        float d = GlobalPosition.DistanceTo(player.GlobalPosition);
        if (d > 450f) { _notifier.Notify(HorseName + " is too far away to hear you."); return; }
        _state = State.Coming;
        _notifier.Notify("You whistle for " + HorseName + ".");
    }

    /// <summary>Teleport the horse and make this its new resting place.</summary>
    public void PlaceAt(Vector3 position, float yaw)
    {
        GlobalPosition = position;
        Rotation = new Vector3(0, yaw, 0);
        _home = position;
        _target = position;
        _speed = 0f;
        Velocity = Vector3.Zero;
        if (!IsRidden) { _state = State.Idle; _wait = 6f; }
    }

    public void Spook()
    {
        if (IsRidden) { Stamina = Mathf.Max(0f, Stamina - 20f); return; }
        _state = State.Spooked;
        _wait = 2.5f;
        var rnd = new RandomNumberGenerator();
        _target = GlobalPosition + new Vector3(rnd.RandfRange(-1, 1), 0, rnd.RandfRange(-1, 1)).Normalized() * 25f;
    }

    private Vector2 _riderMove;
    private bool _riderSprint, _riderJump;
    private CameraRig? _riderCam;

    public void SetRiderInput(Vector2 move, bool sprint, bool jump, CameraRig cam)
    {
        _riderMove = move; _riderSprint = sprint; _riderJump |= jump; _riderCam = cam;
    }

    // ------------------------------------------------------------------ loop

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        float wantSpeed = 0f;
        float wantYaw = Rotation.Y;
        bool jump = false;

        switch (_state)
        {
            case State.Ridden:
            {
                var cam = _riderCam;
                if (cam != null && _riderMove.LengthSquared() > 0.01f)
                {
                    var dir = cam.FlatForward * -_riderMove.Y + cam.FlatRight * _riderMove.X;
                    wantYaw = Mathf.Atan2(-dir.X, -dir.Z);
                    bool gallop = _riderSprint && Stamina > 0.5f;
                    wantSpeed = gallop ? Gallop : Trot;
                    float turn = Mathf.Abs(Mathf.AngleDifference(Rotation.Y, wantYaw));
                    if (turn > 1.2f) wantSpeed = Mathf.Min(wantSpeed, Walk);
                    if (gallop) Stamina = Mathf.Max(0f, Stamina - 9f * dt);
                }
                if (wantSpeed < Gallop - 0.1f) Stamina = Mathf.Min(100f, Stamina + 7f * dt);
                jump = _riderJump && IsOnFloor();
                _riderJump = false;
                break;
            }
            case State.Coming:
            {
                var player = _main.Player;
                _target = player.GlobalPosition;
                float d = Flat(_target - GlobalPosition).Length();
                if (d < 2.6f) { _state = State.Idle; _home = GlobalPosition; _wait = 4f; break; }
                wantYaw = YawTo(_target);
                wantSpeed = d > 40f ? Gallop : d > 10f ? Trot : Walk;
                break;
            }
            case State.Spooked:
            {
                _wait -= dt;
                wantYaw = YawTo(_target);
                wantSpeed = Gallop;
                if (_wait <= 0f || Flat(_target - GlobalPosition).Length() < 2f) { _state = State.Idle; _home = GlobalPosition; _wait = 3f; }
                break;
            }
            case State.Wander:
            {
                float d = Flat(_target - GlobalPosition).Length();
                if (d < 1.5f) { _state = State.Idle; _wait = (float)GD.RandRange(3.0, 9.0); break; }
                wantYaw = YawTo(_target);
                wantSpeed = Walk;
                break;
            }
            default: // Idle: graze, then pick a nearby spot
            {
                _wait -= dt;
                if (_wait <= 0f)
                {
                    var rnd = new RandomNumberGenerator();
                    var p = _home + new Vector3(rnd.RandfRange(-10f, 10f), 0, rnd.RandfRange(-10f, 10f));
                    var terrain = _main.World.Terrain;
                    if (terrain.InBounds(p.X, p.Z) && terrain.Slope(p.X, p.Z) < 0.2f) { _target = p; _state = State.Wander; }
                    else _wait = 2f;
                }
                break;
            }
        }

        // turn, then move along our own forward; horses do not strafe
        float turnRate = _state == State.Ridden ? 2.6f : 1.8f;
        Rotation = new Vector3(0, Mathf.RotateToward(Rotation.Y, wantYaw, turnRate * dt), 0);
        _yawRate = dt > 0f ? Mathf.AngleDifference(_prevYaw, Rotation.Y) / dt : 0f;
        _prevYaw = Rotation.Y;
        float accel = wantSpeed > _speed ? 6f : 9f;
        _speed = Mathf.MoveToward(_speed, wantSpeed, accel * dt);
        var forward = -GlobalTransform.Basis.Z;
        var v = Velocity;
        v.X = forward.X * _speed;
        v.Z = forward.Z * _speed;
        if (IsOnFloor()) v.Y = jump ? 4.5f : -0.5f; else v.Y -= Gravity * dt;
        Velocity = v;
        MoveAndSlide();

        // if a wall stops us, give up on the target
        if (_state is State.Wander or State.Coming or State.Spooked && wantSpeed > 0.5f && Velocity.Length() < 0.3f)
        {
            _stuck += dt;
            if (_stuck > 1.5f) { _stuck = 0f; _state = State.Idle; _home = GlobalPosition; _wait = 2f; }
        }
        else _stuck = 0f;

        Animate(dt);
    }

    private void Animate(float dt)
    {
        float s = new Vector2(Velocity.X, Velocity.Z).Length();
        bool gallop = s > Trot + 1f;
        _phase += dt * Mathf.Clamp(s, 0f, 14f) * (gallop ? 1.05f : 1.5f);
        float amp = Mathf.Clamp(s / 4f, 0f, 1f) * 0.75f;
        // trot: diagonal pairs move together; gallop: fore pair and hind pair, slightly offset
        float[] ph = gallop
            ? new[] { _phase, _phase + 0.35f, _phase + Mathf.Pi, _phase + Mathf.Pi + 0.35f }
            : new[] { _phase, _phase + Mathf.Pi, _phase + Mathf.Pi, _phase };
        for (int i = 0; i < 4; i++)
        {
            float swing = Mathf.Sin(ph[i]) * amp;
            float fold = Mathf.Max(0f, -Mathf.Sin(ph[i])) * amp * 1.1f;
            _legs[i].Rotation = new Vector3(swing, 0, 0);
            _shins[i].Rotation = new Vector3(-fold, 0, 0);
        }
        bool grazing = s < 0.2f && _state == State.Idle && _wait > 1.5f;
        float bob = Mathf.Abs(Mathf.Sin(_phase)) * (gallop ? 0.07f : 0.03f) * amp;
        _body.Position = new Vector3(0, bob, 0);
        _lean = Mathf.Lerp(_lean, Mathf.Clamp(_yawRate * 0.11f * Mathf.Clamp(s / 8f, 0f, 1f), -0.26f, 0.26f), 1f - Mathf.Exp(-dt * 6f));
        _body.Rotation = new Vector3(gallop ? Mathf.Sin(_phase) * 0.05f : 0f, 0, _lean);
        _neck.Rotation = new Vector3(Mathf.Lerp(_neck.Rotation.X, grazing ? -0.8f : -Mathf.Abs(Mathf.Sin(_phase)) * 0.06f * amp - (gallop ? 0.2f : 0f), 1f - Mathf.Exp(-dt * 3f)), 0, 0);
        _head.Rotation = new Vector3(grazing ? 0.25f : 0.1f + Mathf.Sin(_phase + 0.5f) * 0.05f * amp, 0, 0);
        _tail.Rotation = new Vector3(-0.35f + Mathf.Sin(_phase * 0.5f) * 0.1f * amp, Mathf.Sin(_idle += dt * 1.3f) * 0.18f, 0);
        _dust.Emitting = gallop && IsOnFloor();
        _hoof += s * dt;
        if (_hoof > 2.4f && IsOnFloor() && s > 1.5f) { _hoof = 0f; _main.Sfx.Hoof(GlobalPosition, gallop); }
    }

    private float _idle;

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
    private float YawTo(Vector3 target)
    {
        var d = Flat(target - GlobalPosition);
        return d.LengthSquared() < 0.001f ? Rotation.Y : Mathf.Atan2(-d.X, -d.Z);
    }
}
