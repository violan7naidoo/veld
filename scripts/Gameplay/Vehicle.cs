using Godot;
using Veld.Characters;
using Veld.Core;
using Veld.World;

namespace Veld.Gameplay;

/// <summary>
/// A drivable car or minibus taxi on Godot's VehicleBody3D (real suspension, steering and traction
/// per wheel). The player gets in with E; input then goes to SetDriverInput and the player node
/// rides along on the Seat. Headlights come on at night while driven, brake lights when braking,
/// and running into people counts as an attack.
/// </summary>
public partial class Vehicle : VehicleBody3D, IInteractable
{
    public string Kind { get; private set; } = "car";
    public Color Paint { get; private set; } = Colors.White;
    public bool Occupied => Driver != null;
    public Player? Driver { get; private set; }
    public Transform3D SeatTransform => _seat.GlobalTransform;
    /// <summary>Forward speed in m/s (negative when reversing).</summary>
    public float ForwardSpeed => LinearVelocity.Dot(-GlobalTransform.Basis.Z);

    private Node3D _seat = null!;
    private readonly SpotLight3D[] _headlights = new SpotLight3D[2];
    private StandardMaterial3D _tailMat = null!;
    private StandardMaterial3D _headMat = null!;
    private AudioStreamPlayer3D? _engine;
    private float _throttle, _steerTarget;
    private bool _braking, _handbrake;
    private float _hornCooldown;
    private float _parkedFor;

    private float Power => Kind == "taxi" ? 5200f : 4200f;
    private float MaxSpeed => Kind == "taxi" ? 27f : 33f;

    public static Vehicle Create(string kind, Color paint)
    {
        var v = new Vehicle { Kind = kind, Paint = paint, Name = kind };
        return v;
    }

    public override void _Ready()
    {
        bool taxi = Kind == "taxi";
        var size = taxi ? new Vector3(1.9f, 1.0f, 5.0f) : new Vector3(1.78f, 0.6f, 4.3f);
        Mass = taxi ? 1500f : 1100f;
        CenterOfMassMode = CenterOfMassModeEnum.Custom;
        CenterOfMass = new Vector3(0, 0.12f, 0);
        AngularDamp = 2.0f;
        LinearDamp = 0.05f;
        CollisionLayer = Layers.Props;
        CollisionMask = Layers.Solid | Layers.Characters | Layers.Horses;
        ContactMonitor = true;
        MaxContactsReported = 4;
        BodyEntered += OnBodyEntered;
        CanSleep = true;
        FreezeMode = FreezeModeEnum.Static;

        // collision: body and cabin
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = new Vector3(0, 0.45f + size.Y / 2, 0) });
        var cabinSize = taxi ? new Vector3(1.8f, 0.7f, 4.2f) : new Vector3(1.6f, 0.55f, 2.1f);
        var cabinPos = taxi ? new Vector3(0, 0.45f + size.Y + 0.35f, 0.1f) : new Vector3(0, 0.45f + size.Y + 0.27f, -0.1f);
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = cabinSize }, Position = cabinPos });

        // visuals
        var paint = new StandardMaterial3D { AlbedoColor = Paint, Metallic = 0.55f, Roughness = 0.3f };
        var glass = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.15f, 0.18f), Metallic = 0.6f, Roughness = 0.15f };
        var trim = Props.Material(new Color(0.1f, 0.1f, 0.1f), 0.7f);
        AddChild(Props.Box(size, paint, new Vector3(0, 0.45f + size.Y / 2, 0)));
        AddChild(Props.Box(cabinSize, glass, cabinPos));
        AddChild(Props.Box(new Vector3(cabinSize.X + 0.02f, 0.08f, cabinSize.Z + 0.02f), paint, cabinPos + new Vector3(0, cabinSize.Y / 2, 0)));
        for (int s = -1; s <= 1; s += 2) AddChild(Props.Box(new Vector3(0.06f, cabinSize.Y, cabinSize.Z), paint, cabinPos + new Vector3(s * (cabinSize.X / 2 + 0.02f), 0, 0)));
        AddChild(Props.Box(new Vector3(size.X + 0.1f, 0.22f, 0.3f), trim, new Vector3(0, 0.5f, -size.Z / 2 - 0.1f)));
        AddChild(Props.Box(new Vector3(size.X + 0.1f, 0.22f, 0.3f), trim, new Vector3(0, 0.5f, size.Z / 2 + 0.1f)));
        if (taxi)
        {
            // the stripe
            AddChild(Props.Box(new Vector3(size.X + 0.02f, 0.14f, size.Z), Props.Material(new Color(0.85f, 0.15f, 0.1f)), new Vector3(0, 0.45f + size.Y * 0.55f, 0)));
        }
        _headMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.9f, 0.8f), Emission = new Color(1f, 0.95f, 0.8f), EmissionEnergyMultiplier = 0f, EmissionEnabled = true };
        _tailMat = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.05f, 0.05f), Emission = new Color(1f, 0.1f, 0.05f), EmissionEnergyMultiplier = 0f, EmissionEnabled = true };
        for (int s = -1; s <= 1; s += 2)
        {
            AddChild(Props.Box(new Vector3(0.3f, 0.16f, 0.06f), _headMat, new Vector3(s * (size.X / 2 - 0.3f), 0.45f + size.Y * 0.6f, -size.Z / 2 - 0.02f)));
            AddChild(Props.Box(new Vector3(0.3f, 0.14f, 0.06f), _tailMat, new Vector3(s * (size.X / 2 - 0.3f), 0.45f + size.Y * 0.6f, size.Z / 2 + 0.02f)));
            var light = new SpotLight3D
            {
                Position = new Vector3(s * (size.X / 2 - 0.3f), 0.45f + size.Y * 0.6f, -size.Z / 2 - 0.05f), SpotRange = 38f, SpotAngle = 32f, LightEnergy = 3.5f,
                LightColor = new Color(1f, 0.95f, 0.8f), ShadowEnabled = false, Visible = false,
            };
            light.RotateY(Mathf.Pi); // spot lights point along -Z already; flip to shine forward of the car (-Z is forward: no flip needed)
            light.RotateY(Mathf.Pi);
            AddChild(light);
            _headlights[s < 0 ? 0 : 1] = light;
        }

        // wheels: front steer, rear drive
        float wx = size.X / 2 + 0.02f;
        float wzF = -size.Z / 2 + (taxi ? 0.9f : 0.75f), wzR = size.Z / 2 - (taxi ? 0.9f : 0.75f);
        var rubber = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.08f, 0.08f), Roughness = 0.9f };
        var rim = new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.7f, 0.72f), Metallic = 0.6f, Roughness = 0.35f };
        foreach (var (x, z, front) in new[] { (-wx, wzF, true), (wx, wzF, true), (-wx, wzR, false), (wx, wzR, false) })
        {
            var wheel = new VehicleWheel3D
            {
                Position = new Vector3(x, 0.62f, z), UseAsSteering = front, UseAsTraction = !front,
                WheelRadius = 0.34f, WheelRestLength = 0.3f, SuspensionTravel = 0.3f, SuspensionStiffness = 32f, SuspensionMaxForce = 60000f,
                DampingCompression = 0.6f, DampingRelaxation = 0.85f, WheelFrictionSlip = 2.2f, WheelRollInfluence = 0.02f,
            };
            var tyre = Props.Cylinder(0.34f, 0.24f, rubber, Vector3.Zero, -1, 12);
            tyre.Rotation = new Vector3(0, 0, Mathf.Pi / 2);
            wheel.AddChild(tyre);
            var hub = Props.Cylinder(0.2f, 0.26f, rim, Vector3.Zero, -1, 8);
            hub.Rotation = new Vector3(0, 0, Mathf.Pi / 2);
            wheel.AddChild(hub);
            AddChild(wheel);
        }

        _seat = new Node3D { Name = "Seat", Position = new Vector3(-0.42f, 0.45f + size.Y - 0.1f, taxi ? -1.3f : -0.3f) };
        AddChild(_seat);
    }

    // ------------------------------------------------------------------ interaction

    public string Prompt => "Get in the " + (Kind == "taxi" ? "taxi" : "car");
    public bool CanInteract(Player player) => !Occupied && !player.IsMounted && player.Driving == null && LinearVelocity.Length() < 3.5f;
    public void Interact(Player player) => player.EnterVehicle(this);

    public void SetDriver(Player? driver)
    {
        Driver = driver;
        if (driver != null)
        {
            Freeze = false;
            _parkedFor = 0f;
            Sleeping = false;
            if (_engine == null)
            {
                _engine = Game.Instance.Main?.Sfx.EngineLoop();
                if (_engine != null) AddChild(_engine);
            }
            _engine?.Play();
        }
        else
        {
            _throttle = 0f; _steerTarget = 0f; _handbrake = true;
            _engine?.Stop();
        }
    }

    /// <summary>The modern map's stand-in for whistling a horse: your car is brought to the road beside you (a friend drops it off).</summary>
    public void RecallTo(Player player)
    {
        if (Occupied) return;
        float d = GlobalPosition.DistanceTo(player.GlobalPosition);
        if (d < 25f) { Game.Instance.Notify("Your car is right here."); return; }
        if (!Game.Instance.TrySpend(2000)) { Game.Instance.Notify("Getting the car brought round costs R20 and you are short."); return; }
        var yaw = player.Camera.Yaw;
        var fwd = new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw));
        var right = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
        var at = player.GlobalPosition + fwd * 6f + right * 2.5f;
        var terrain = Game.Instance.World?.Terrain;
        if (terrain != null) at.Y = terrain.GetHeight(at.X, at.Z) + 0.6f;
        Freeze = false;
        GlobalTransform = new Transform3D(Basis.Identity.Rotated(Vector3.Up, yaw), at);
        LinearVelocity = Vector3.Zero; AngularVelocity = Vector3.Zero;
        _parkedFor = 0f;
        Game.Instance.Main?.Sfx.Horn(at);
        Game.Instance.Notify("A mate drops your car off beside you. R20 for petrol.");
    }

    /// <summary>Where the driver stands after getting out: beside the driver's door, on the ground.</summary>
    public Vector3 ExitPosition()
    {
        var side = GlobalTransform.Basis.X * -1.7f;
        var p = GlobalPosition + side + new Vector3(0, 0.3f, 0);
        var terrain = Game.Instance.World?.Terrain;
        if (terrain != null) p.Y = Mathf.Max(p.Y, terrain.GetHeight(p.X, p.Z) + 0.2f);
        return p;
    }

    private Vector2 _input;
    private bool _horn;

    public void SetDriverInput(Vector2 move, bool handbrake, bool horn)
    {
        _input = move;
        _handbrake = handbrake;
        _horn = horn;
    }

    // ------------------------------------------------------------------ physics

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        float speed = ForwardSpeed;
        if (Occupied)
        {
            float maxSteer = Mathf.Lerp(0.5f, 0.09f, Mathf.Clamp(Mathf.Abs(speed) / 24f, 0f, 1f));
            _steerTarget = -_input.X * maxSteer;
            Steering = Mathf.Lerp(Steering, _steerTarget, 1f - Mathf.Exp(-dt * 7f));
            _throttle = -_input.Y;
            _braking = false;
            if (_throttle > 0.01f)
            {
                // drive forward (this also stops a backward roll on a hill)
                EngineForce = -_throttle * Power * (1f - Mathf.Clamp(speed / MaxSpeed, 0f, 1f)); // Godot drives wheels along +Z for a positive force; our front is -Z
                Brake = 0f;
            }
            else if (_throttle < -0.01f)
            {
                if (speed > 1.5f) { Brake = 28f; EngineForce = 0f; _braking = true; }
                else { EngineForce = -_throttle * Power * 0.45f * (1f - Mathf.Clamp(-speed / 9f, 0f, 1f)); Brake = 0f; }
            }
            else { EngineForce = 0f; Brake = 2f; }
            if (_handbrake) { Brake = 45f; _braking = true; }
            _hornCooldown -= dt;
            if (_horn && _hornCooldown <= 0f) { _hornCooldown = 0.7f; Game.Instance.Main?.Sfx.Horn(GlobalPosition); }
            if (_engine != null) _engine.PitchScale = 0.55f + Mathf.Clamp(Mathf.Abs(speed) / MaxSpeed, 0f, 1f) * 1.3f + Mathf.Abs(_throttle) * 0.15f;
        }
        else
        {
            EngineForce = 0f;
            Brake = 80f;
            Steering = Mathf.Lerp(Steering, 0f, 1f - Mathf.Exp(-dt * 4f));
            // once it has come to rest, pin it: a parked car must not creep down Chatsworth's hills
            _parkedFor += dt;
            if (!Freeze && _parkedFor > 1.0f && LinearVelocity.Length() < 0.4f) Freeze = true;
        }

        // keep it on its wheels: a righting torque when the body rolls, downforce with speed
        var up = GlobalTransform.Basis.Y;
        float roll = Mathf.Acos(Mathf.Clamp(up.Dot(Vector3.Up), -1f, 1f));
        if (roll > 0.02f)
        {
            var axis = up.Cross(Vector3.Up);
            if (axis.LengthSquared() > 0.0001f) ApplyTorque(axis.Normalized() * roll * Mass * 9f - AngularVelocity * Mass * 0.9f);
        }
        ApplyCentralForce(-Vector3.Up * speed * speed * 6f);

        float night = Game.Instance.DayNight?.NightFactor ?? 0f;
        bool lightsOn = Occupied && night > 0.15f;
        foreach (var l in _headlights) l.Visible = lightsOn;
        _headMat.EmissionEnergyMultiplier = lightsOn ? 3f : 0f;
        _tailMat.EmissionEnergyMultiplier = _braking && Occupied ? 4f : lightsOn ? 1.2f : 0f;
    }

    private void OnBodyEntered(Node body)
    {
        float speed = LinearVelocity.Length();
        if (speed < 4f) return;
        if (body is Npc npc && !npc.IsDead)
        {
            npc.TakeDamage((int)(speed * 6f), Driver);
            Game.Instance.Main?.Sfx.Hit(npc.GlobalPosition);
        }
        else if (body is Horse horse) horse.Spook();
        else if (speed > 9f) Game.Instance.Main?.Sfx.Hit(GlobalPosition);
    }
}
