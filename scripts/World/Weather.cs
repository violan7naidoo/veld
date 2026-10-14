using Godot;
using Veld.Core;

namespace Veld.World;

/// <summary>
/// Dynamic weather: clear, partly cloudy, overcast, rain and storm, drifting from one to the next over
/// a minute or two every few game hours, with odds that depend on the biome. Drives the cloud cover,
/// sun strength, fog, wind (grass sway), rain particles, wet ground and thunder. DayNight reads the
/// blended values every frame.
/// </summary>
public partial class Weather : Node3D
{
    public enum State { Clear, PartlyCloudy, Overcast, Rain, Storm }

    public State Current { get; private set; } = State.PartlyCloudy;
    public State Target { get; private set; } = State.PartlyCloudy;
    /// <summary>Cloud shader coverage threshold: high = few clouds.</summary>
    public float Coverage { get; private set; } = 0.56f;
    public float Rain { get; private set; }
    public float FogMul { get; private set; } = 1f;
    public float Wind { get; private set; } = 0.8f;
    public float Sun { get; private set; } = 1f;
    public float Wetness { get; private set; }
    /// <summary>Short-lived lightning boost for the sun light.</summary>
    public float Flash { get; private set; }

    private readonly struct Preset
    {
        public readonly float Coverage, Rain, Fog, Wind, Sun;
        public Preset(float c, float r, float f, float w, float s) { Coverage = c; Rain = r; Fog = f; Wind = w; Sun = s; }
    }

    private static readonly Preset[] Presets =
    {
        new(0.8f, 0f, 1f, 0.6f, 1f),      // Clear
        new(0.56f, 0f, 1f, 0.9f, 1f),     // PartlyCloudy
        new(0.3f, 0f, 1.7f, 1.1f, 0.5f),  // Overcast
        new(0.24f, 0.7f, 2.4f, 1.5f, 0.38f), // Rain
        new(0.18f, 1f, 2.8f, 2.4f, 0.28f),   // Storm
    };

    private float[] _odds = { 0.5f, 0.3f, 0.1f, 0.07f, 0.03f };
    private float _blend = 1f;
    private float _hoursUntilChange = 3f;
    private float _thunderIn = 10f;
    private float _flashTimer;
    private float _rumbleIn = -1f;
    private readonly RandomNumberGenerator _rng = new();
    private GpuParticles3D _rain = null!;
    private bool _subtropical;

    public void Configure(string biome, int seed)
    {
        _subtropical = biome == "subtropical";
        _odds = _subtropical ? new[] { 0.3f, 0.3f, 0.15f, 0.18f, 0.07f } : new[] { 0.5f, 0.3f, 0.1f, 0.07f, 0.03f };
        _rng.Seed = (ulong)seed;
    }

    public override void _Ready()
    {
        // rain: a box of falling streaks that follows the camera
        var mat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(28f, 1f, 28f),
            Direction = new Vector3(0, -1, 0), Spread = 3f,
            InitialVelocityMin = 13f, InitialVelocityMax = 17f,
            Gravity = new Vector3(0.8f, -2f, 0.3f),
        };
        var streak = new QuadMesh { Size = new Vector2(0.025f, 0.55f) };
        streak.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.75f, 0.82f, 0.95f, 0.45f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _rain = new GpuParticles3D
        {
            Name = "Rain", Amount = 5000, Lifetime = 2.4f, ProcessMaterial = mat, DrawPass1 = streak, Emitting = false,
            VisibilityAabb = new Aabb(new Vector3(-40, -40, -40), new Vector3(80, 80, 80)), AmountRatio = 0f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_rain);
    }

    public override void _Process(double delta)
    {
        if (GetTree().Paused) return;
        float dt = (float)delta;
        var dn = Game.Instance.DayNight;
        float gameHours = dn == null ? dt / 30f : dt * 24f / dn.DayLengthSeconds;

        // pick the next weather every few game hours
        _hoursUntilChange -= gameHours;
        if (_hoursUntilChange <= 0f)
        {
            _hoursUntilChange = _rng.RandfRange(3f, 10f);
            float roll = _rng.Randf(), acc = 0f;
            int pick = 0;
            for (int i = 0; i < _odds.Length; i++) { acc += _odds[i]; if (roll <= acc) { pick = i; break; } }
            // storms only follow cloud, never a clear sky
            if (pick >= 3 && Target == State.Clear) pick = 2;
            if ((State)pick != Target) { Current = Target; Target = (State)pick; _blend = 0f; }
        }
        _blend = Mathf.Min(1f, _blend + dt / 90f);
        var a = Presets[(int)Current]; var b = Presets[(int)Target];
        float t = Mathf.SmoothStep(0f, 1f, _blend);
        Coverage = Mathf.Lerp(a.Coverage, b.Coverage, t);
        Rain = Mathf.Lerp(a.Rain, b.Rain, t);
        FogMul = Mathf.Lerp(a.Fog, b.Fog, t);
        Wind = Mathf.Lerp(a.Wind, b.Wind, t);
        Sun = Mathf.Lerp(a.Sun, b.Sun, t);
        Wetness = Mathf.MoveToward(Wetness, Rain > 0.05f ? 1f : 0f, dt * (Rain > 0.05f ? 0.05f : 0.01f));

        // rain follows the camera
        var cam = GetViewport().GetCamera3D();
        if (cam != null) _rain.GlobalPosition = cam.GlobalPosition + new Vector3(0, 22f, 0) + (-cam.GlobalTransform.Basis.Z) * new Vector3(1, 0, 1) * 8f;
        _rain.AmountRatio = Rain;
        _rain.Emitting = Rain > 0.03f;
        Game.Instance.Main?.Sfx.RainLevel(Rain);

        // thunder
        Flash = Mathf.Max(0f, Flash - dt * 9f);
        if (Target == State.Storm && _blend > 0.5f)
        {
            _thunderIn -= dt;
            if (_thunderIn <= 0f)
            {
                _thunderIn = _rng.RandfRange(6f, 22f);
                Flash = 1f;
                _flashTimer = 0.12f;
                _rumbleIn = _rng.RandfRange(0.8f, 3.5f);
            }
        }
        if (_rumbleIn >= 0f)
        {
            _rumbleIn -= dt;
            if (_rumbleIn < 0f) Game.Instance.Main?.Sfx.Thunder();
        }
        if (_flashTimer > 0f) { _flashTimer -= dt; Flash = 1f; }
    }

    public string Describe() => Target switch
    {
        State.Clear => "clear",
        State.PartlyCloudy => "partly cloudy",
        State.Overcast => "overcast",
        State.Rain => "rain",
        _ => "thunderstorm",
    };

    /// <summary>For tests and the console: jump straight to a state.</summary>
    public void Force(State s)
    {
        Current = s; Target = s; _blend = 1f; _hoursUntilChange = 6f;
    }
}
