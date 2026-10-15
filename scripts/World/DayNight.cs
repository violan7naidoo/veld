using System.Collections.Generic;
using Godot;
using Veld.Core;
using Veld.Sim;

namespace Veld.World;

public enum Quality { Low, Medium, High }

/// <summary>
/// Sky, sun, moon, clouds, fog and post-processing, driven by a 24-hour clock. Also keeps the list of
/// lamps and lit windows that come on at night. Graphics quality presets live in ApplyQuality.
/// </summary>
public partial class DayNight : Node3D, IClock
{
    public float Hour { get; set; } = 7.5f;
    public int Day { get; set; } = 1;

    /// <summary>Move the clock forward, rolling past midnight into the next day.</summary>
    public void Advance(float hours)
    {
        Hour += hours;
        while (Hour >= 24f) { Hour -= 24f; Day++; }
    }
    public float DayLengthSeconds { get; set; } = 720f;
    public bool Paused { get; set; }
    public Quality Quality { get; private set; } = Quality.High;
    /// <summary>0 in full daylight, 1 at night.</summary>
    public float NightFactor { get; private set; }
    public Weather? Weather { get; set; }
    /// <summary>Set by Main once the world is built; the ground and grass shaders read the weather through it.</summary>
    public WorldBuilder? World { get; set; }
    /// <summary>Desaturated slow-motion look while Dead Eye is active.</summary>
    public bool DeadEye { get; set; }

    private DirectionalLight3D _sun = null!;
    private DirectionalLight3D _moon = null!;
    private WorldEnvironment _env = null!;
    private PhysicalSkyMaterial _sky = null!;
    private MeshInstance3D _clouds = null!;
    private ShaderMaterial _cloudMat = null!;
    private MeshInstance3D _stars = null!;
    private StandardMaterial3D _starMat = null!;
    private MeshInstance3D _moonDisc = null!;

    private static readonly List<(Light3D light, float energy)> _nightLights = new();
    private static readonly List<(StandardMaterial3D mat, float energy)> _nightEmissives = new();

    /// <summary>A lamp that is off by day and on at night.</summary>
    public static void RegisterNightLight(Light3D light, float nightEnergy)
    {
        _nightLights.Add((light, nightEnergy));
        light.LightEnergy = 0f;
        light.Visible = false;
    }

    /// <summary>A material (window glass, lantern) that glows at night.</summary>
    public static void RegisterNightEmissive(StandardMaterial3D mat, float nightEnergy)
    {
        _nightEmissives.Add((mat, nightEnergy));
        mat.EmissionEnabled = true;
        mat.EmissionEnergyMultiplier = 0f;
    }

    public void Configure(float startHour, float dayLength, Quality quality)
    {
        Hour = startHour;
        DayLengthSeconds = dayLength;
        Quality = quality;
    }

    public string TimeText => $"{(int)Hour:00}:{(int)((Hour % 1f) * 60f):00}";
    public bool IsNight => Hour < 5.5f || Hour > 19.5f;

    public override void _Ready()
    {
        _nightLights.Clear();
        _nightEmissives.Clear();

        _sun = new DirectionalLight3D
        {
            Name = "Sun", ShadowEnabled = true,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            DirectionalShadowMaxDistance = 380f, DirectionalShadowSplit1 = 0.05f, DirectionalShadowSplit2 = 0.14f, DirectionalShadowSplit3 = 0.4f,
            DirectionalShadowFadeStart = 0.85f, ShadowBlur = 1.3f, ShadowNormalBias = 2.0f, ShadowBias = 0.03f,
            LightAngularDistance = 0.6f, LightEnergy = 1.2f, LightVolumetricFogEnergy = 1.4f,
        };
        _moon = new DirectionalLight3D
        {
            Name = "Moon", ShadowEnabled = true, DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits, DirectionalShadowMaxDistance = 120f,
            LightEnergy = 0.0f, LightColor = new Color(0.6f, 0.7f, 1.0f), SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly, LightVolumetricFogEnergy = 0.4f,
        };
        AddChild(_sun);
        AddChild(_moon);

        _sky = new PhysicalSkyMaterial
        {
            RayleighCoefficient = 2.2f, RayleighColor = new Color(0.26f, 0.41f, 0.58f),
            MieCoefficient = 0.006f, MieEccentricity = 0.8f, MieColor = new Color(0.69f, 0.73f, 0.8f),
            Turbidity = 8f, SunDiskScale = 1.6f, GroundColor = new Color(0.34f, 0.28f, 0.2f),
            EnergyMultiplier = 1.0f, UseDebanding = true,
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = _sky, RadianceSize = Sky.RadianceSizeEnum.Size256, ProcessMode = Sky.ProcessModeEnum.Automatic },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightSkyContribution = 1f,
            AmbientLightEnergy = 1f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Aces,
            TonemapExposure = 1.0f,
            TonemapWhite = 1.0f,
            SsaoEnabled = true, SsaoRadius = 2.0f, SsaoIntensity = 2.2f, SsaoPower = 1.6f, SsaoDetail = 0.4f, SsaoHorizon = 0.1f,
            SsilEnabled = false, SsilRadius = 4f, SsilIntensity = 0.8f,
            SdfgiEnabled = false, SdfgiCascades = 6, SdfgiMinCellSize = 0.5f,
            SdfgiUseOcclusion = true, SdfgiBounceFeedback = 0.55f, SdfgiEnergy = 1.1f, SdfgiReadSkyLight = true, SdfgiNormalBias = 1.3f, SdfgiProbeBias = 1.2f,
            GlowEnabled = true, GlowIntensity = 0.5f, GlowStrength = 0.9f, GlowBloom = 0.06f, GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight, GlowHdrThreshold = 1.1f,
            FogEnabled = true, FogMode = Godot.Environment.FogModeEnum.Exponential, FogDensity = 0.0007f, FogAerialPerspective = 0.55f, FogSkyAffect = 0.08f, FogSunScatter = 0.15f, FogLightEnergy = 1.0f,
            VolumetricFogEnabled = false, VolumetricFogDensity = 0.003f, VolumetricFogLength = 320f, VolumetricFogDetailSpread = 2.5f,
            VolumetricFogAlbedo = new Color(0.95f, 0.9f, 0.82f), VolumetricFogAmbientInject = 0.15f, VolumetricFogSkyAffect = 0.1f, VolumetricFogAnisotropy = 0.5f,
            AdjustmentEnabled = true, AdjustmentBrightness = 1.0f, AdjustmentContrast = 1.05f, AdjustmentSaturation = 1.1f,
        };
        _env = new WorldEnvironment { Name = "Environment", Environment = env };
        AddChild(_env);

        // cloud layer
        _cloudMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/clouds.gdshader") };
        _cloudMat.SetShaderParameter("noise", Tex.CloudNoise());
        _clouds = new MeshInstance3D
        {
            Name = "Clouds", Mesh = new PlaneMesh { Size = new Vector2(7000f, 7000f), SubdivideWidth = 8, SubdivideDepth = 8 },
            Position = new Vector3(0, 480f, 0), MaterialOverride = _cloudMat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, GIMode = GeometryInstance3D.GIModeEnum.Disabled,
            ExtraCullMargin = 4000f,
        };
        AddChild(_clouds);
        _cloudMat.SetShaderParameter("scale", 3.2f);
        _cloudMat.SetShaderParameter("coverage", 0.56f);
        _cloudMat.SetShaderParameter("softness", 0.28f);

        // stars: a dome that follows the camera and fades in at night; the moon is a small glowing disc placed along the moon light
        _starMat = new StandardMaterial3D
        {
            AlbedoTexture = Tex.Stars(), AlbedoColor = new Color(1, 1, 1, 0), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, CullMode = BaseMaterial3D.CullModeEnum.Front,
            DisableFog = true, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps, RenderPriority = -10,
        };
        _stars = new MeshInstance3D
        {
            Name = "Stars", Mesh = new SphereMesh { Radius = 2200f, Height = 4400f, RadialSegments = 32, Rings = 16 }, MaterialOverride = _starMat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, GIMode = GeometryInstance3D.GIModeEnum.Disabled, ExtraCullMargin = 5000f,
        };
        AddChild(_stars);
        var moonMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.95f, 0.9f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true,
            Emission = new Color(1f, 0.97f, 0.85f), EmissionEnergyMultiplier = 2.5f, DisableFog = true,
        };
        _moonDisc = new MeshInstance3D
        {
            Name = "MoonDisc", Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 16, Rings = 8 }, MaterialOverride = moonMat,
            Scale = new Vector3(22f, 22f, 22f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, GIMode = GeometryInstance3D.GIModeEnum.Disabled, ExtraCullMargin = 5000f,
        };
        AddChild(_moonDisc);

        ApplyQuality(Quality);
        Apply();
    }

    public void ApplyQuality(Quality q)
    {
        Quality = q;
        var env = _env.Environment;
        env.SsaoEnabled = q >= Quality.Medium;
        env.SsilEnabled = q == Quality.High;
        env.SdfgiEnabled = q >= Quality.Medium;
        env.VolumetricFogEnabled = q == Quality.High;
        _sun.DirectionalShadowMaxDistance = q == Quality.Low ? 180f : 380f;
        _sun.DirectionalShadowMode = q == Quality.Low ? DirectionalLight3D.ShadowMode.Parallel2Splits : DirectionalLight3D.ShadowMode.Parallel4Splits;
        _moon.ShadowEnabled = q == Quality.High;
        RenderingServer.DirectionalShadowAtlasSetSize(q == Quality.Low ? 2048 : 4096, true);
        var vp = GetViewport();
        vp.Msaa3D = q == Quality.Low ? Viewport.Msaa.Disabled : Viewport.Msaa.Msaa4X;
        vp.ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Fxaa;
        vp.UseTaa = q == Quality.High;
        vp.UseDebanding = true;
        _clouds.Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!Paused && !GetTree().Paused)
        {
            Hour += (float)delta * 24f / DayLengthSeconds;
            if (Hour >= 24f) { Hour -= 24f; Day++; }
        }
        Apply();
    }

    private void Apply()
    {
        // sun travels east to west; elevation peaks at 13:00 (southern hemisphere: the sun sits to the north)
        float t = (Hour - 6f) / 12f; // 0 at sunrise 06:00, 1 at sunset 18:00
        float elevation = Mathf.Sin(Mathf.Clamp(t, -0.25f, 1.25f) * Mathf.Pi) * 72f;
        float azimuth = Mathf.Lerp(-95f, 95f, Mathf.Clamp(t, -0.25f, 1.25f));
        _sun.RotationDegrees = new Vector3(-elevation, azimuth, 0);
        _moon.RotationDegrees = new Vector3(-Mathf.Max(-elevation, 8f), azimuth + 180f, 0);

        float sunMul = Weather?.Sun ?? 1f;
        float flash = Weather?.Flash ?? 0f;
        float daylight = Mathf.Clamp(elevation / 18f, 0f, 1f);
        float dusk = Mathf.Clamp(1f - Mathf.Abs(elevation) / 14f, 0f, 1f);
        NightFactor = 1f - Mathf.Clamp((elevation + 5f) / 11f, 0f, 1f);

        _sun.LightEnergy = 1.25f * Mathf.Clamp((elevation + 2f) / 16f, 0f, 1f) * sunMul + flash * 5f;
        _sun.LightColor = new Color(1f, 0.97f, 0.9f).Lerp(new Color(1f, 0.6f, 0.32f), dusk);
        _sun.Visible = elevation > -3f || flash > 0.01f;
        _moon.LightEnergy = 0.38f * NightFactor;
        _moon.Visible = NightFactor > 0.01f;

        _sky.Turbidity = Mathf.Lerp(6f, 12f, dusk) + (1f - sunMul) * 6f;
        _sky.EnergyMultiplier = Mathf.Lerp(0.6f, 1.0f, daylight);

        var env = _env.Environment;
        env.AmbientLightEnergy = Mathf.Lerp(0.55f, 1.0f, daylight) * Mathf.Lerp(0.7f, 1f, sunMul) + flash * 0.6f;
        env.FogDensity = Mathf.Lerp(0.0012f, 0.0006f, daylight) * (Weather?.FogMul ?? 1f);
        env.FogLightColor = new Color(0.55f, 0.6f, 0.72f).Lerp(new Color(0.85f, 0.8f, 0.7f), daylight).Lerp(new Color(0.95f, 0.55f, 0.35f), dusk * 0.6f);
        env.VolumetricFogDensity = Mathf.Lerp(0.0055f, 0.0022f, daylight);
        env.VolumetricFogEmission = new Color(0.02f, 0.02f, 0.03f) * NightFactor;
        env.AdjustmentSaturation = DeadEye ? 0.18f : Mathf.Lerp(0.85f, 1.1f, 1f - NightFactor) * Mathf.Lerp(0.8f, 1f, sunMul);
        env.AdjustmentBrightness = DeadEye ? 0.92f : 1f;

        // clouds pick up the sun's colour, go grey-blue at night
        var sunCol = _sun.LightColor;
        var lit = sunCol * Mathf.Lerp(1.0f, 1.3f, daylight) * Mathf.Lerp(0.5f, 1f, sunMul);
        var night = new Color(0.12f, 0.14f, 0.2f);
        _cloudMat.SetShaderParameter("lit_color", night.Lerp(lit, 1f - NightFactor));
        _cloudMat.SetShaderParameter("coverage", Weather?.Coverage ?? 0.56f);
        _cloudMat.SetShaderParameter("shade_color", (night * 0.7f).Lerp(new Color(0.62f, 0.64f, 0.72f).Lerp(new Color(0.75f, 0.45f, 0.38f), dusk), 1f - NightFactor));

        var cam = GetViewport().GetCamera3D();
        if (cam != null)
        {
            _stars.GlobalPosition = cam.GlobalPosition;
            _moonDisc.GlobalPosition = cam.GlobalPosition + _moon.GlobalTransform.Basis.Z * 1900f;
        }
        _starMat.AlbedoColor = new Color(1, 1, 1, NightFactor * 0.95f);
        _moonDisc.Visible = NightFactor > 0.02f && _moon.GlobalTransform.Basis.Z.Y > 0.02f;

        if (World != null)
        {
            if (World.Terrain.GetNodeOrNull<MeshInstance3D>("TerrainMesh")?.MaterialOverride is ShaderMaterial tm) tm.SetShaderParameter("wetness", Weather?.Wetness ?? 0f);
            World.Vegetation?.GrassMaterial?.SetShaderParameter("wind_strength", 0.14f * (Weather?.Wind ?? 0.8f));
        }

        foreach (var (light, energy) in _nightLights)
        {
            light.LightEnergy = energy * NightFactor;
            light.Visible = NightFactor > 0.03f;
        }
        foreach (var (mat, energy) in _nightEmissives)
            mat.EmissionEnergyMultiplier = energy * NightFactor;
    }
}
