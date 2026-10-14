using System.Collections.Generic;
using Godot;
using Veld.Core;

namespace Veld.World;

/// <summary>
/// Small factory for the placeholder visuals used everywhere: textured materials (procedural, see Tex)
/// and simple meshes with them. Every model in the prototype is built from these so it runs with
/// zero binary assets. Replace a method here (for example Box -> load a GLB) and the whole game picks
/// it up.
/// </summary>
public static class Props
{
    private static readonly Dictionary<Color, StandardMaterial3D> Materials = new();
    private static readonly Dictionary<string, StandardMaterial3D> Textured = new();

    // ------------------------------------------------------------------ materials

    public static StandardMaterial3D Material(Color color, float roughness = 0.9f, float metallic = 0f)
    {
        if (Materials.TryGetValue(color, out var m)) return m;
        m = new StandardMaterial3D { AlbedoColor = color, Roughness = roughness, Metallic = metallic };
        Materials[color] = m;
        return m;
    }

    /// <summary>World-space triplanar textured material, so boxes of any size get the same texel density.</summary>
    public static StandardMaterial3D TexturedMaterial(string key, Texture2D albedo, Texture2D? normal, Color tint, float metresPerTile, float roughness = 0.9f, float metallic = 0f, float normalScale = 1f)
    {
        string k = $"{key}|{tint.ToHtml()}|{metresPerTile}";
        if (Textured.TryGetValue(k, out var m)) return m;
        m = new StandardMaterial3D
        {
            AlbedoColor = tint, AlbedoTexture = albedo, Roughness = roughness, Metallic = metallic,
            Uv1Triplanar = true, Uv1WorldTriplanar = true, Uv1TriplanarSharpness = 8f,
            Uv1Scale = new Vector3(1f / metresPerTile, 1f / metresPerTile, 1f / metresPerTile),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        if (normal != null) { m.NormalEnabled = true; m.NormalTexture = normal; m.NormalScale = normalScale; }
        Textured[k] = m;
        return m;
    }

    public static StandardMaterial3D Plaster(Color tint) => TexturedMaterial("plaster", Tex.PlasterAlbedo(), Tex.PlasterNormal(), tint, 2.5f, 0.95f, 0f, 0.6f);
    public static StandardMaterial3D Corrugated(Color tint) => TexturedMaterial("iron", Tex.IronAlbedo(), Tex.IronNormal(), tint, 1.0f, 0.5f, 0.45f, 1.0f);
    public static StandardMaterial3D Planks(Color tint) => TexturedMaterial("planks", Tex.PlankAlbedo(), Tex.PlankNormal(), tint, 1.6f, 0.85f, 0f, 0.8f);
    public static StandardMaterial3D StoneBlocks(Color tint) => TexturedMaterial("stone", Tex.StoneAlbedo(), Tex.StoneNormal(), tint, 1.1f, 0.92f, 0f, 0.8f);
    public static StandardMaterial3D Soil(Color tint) => TexturedMaterial("soil", Tex.SoilAlbedo(), Tex.SoilNormal(), tint, 1.5f, 0.95f, 0f, 0.8f);
    public static StandardMaterial3D Rock(Color tint) => TexturedMaterial("rock", Tex.RockAlbedo(), Tex.RockNormal(), tint, 2.5f, 0.85f, 0f, 1.2f);
    public static StandardMaterial3D Cloth(Color tint) => TexturedMaterial("cloth", Tex.PlasterAlbedo(), Tex.PlasterNormal(), tint, 0.6f, 1.0f, 0f, 0.4f);

    private static StandardMaterial3D? _particleMat;
    /// <summary>Soft translucent material for CpuParticles3D meshes (smoke, dust); the particle colour sets tint and alpha.</summary>
    public static StandardMaterial3D ParticleMaterial() => _particleMat ??= new StandardMaterial3D
    {
        VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
        Roughness = 1f, DisableReceiveShadows = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    public static StandardMaterial3D Asphalt() => Cached("asphalt", () => new StandardMaterial3D
    {
        AlbedoTexture = Tex.Asphalt(), Roughness = 0.92f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
    });
    public static StandardMaterial3D Concrete(Color tint) => TexturedMaterial("concrete", Tex.Concrete(), null, tint, 2.0f, 0.95f);
    public static StandardMaterial3D Brick(Color tint) => TexturedMaterial("brick", Tex.BrickAlbedo(), Tex.BrickNormal(), tint, 1.0f, 0.9f, 0f, 0.8f);
    public static StandardMaterial3D WaterMaterial() => Cached("water", () => new StandardMaterial3D
    {
        AlbedoColor = new Color(0.2f, 0.3f, 0.3f, 0.75f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, Metallic = 0.3f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    });

    private static readonly Dictionary<string, StandardMaterial3D> Named = new();
    private static StandardMaterial3D Cached(string key, System.Func<StandardMaterial3D> make)
    {
        if (Named.TryGetValue(key, out var m)) return m;
        m = make(); Named[key] = m; return m;
    }

    /// <summary>A modern street light: steel pole, arm, lamp head, and a warm-white light that comes on at night.</summary>
    public static Node3D StreetLight(float energy = 3.5f, float range = 26f)
    {
        var n = new Node3D();
        var steel = Material(new Color(0.42f, 0.44f, 0.46f), 0.55f, 0.6f);
        n.AddChild(Cylinder(0.07f, 7.5f, steel, new Vector3(0, 3.75f, 0), 0.05f, 8));
        var arm = Box(new Vector3(0.08f, 0.08f, 1.7f), steel, new Vector3(0, 7.45f, -0.8f));
        arm.Rotation = new Vector3(-0.12f, 0, 0);
        n.AddChild(arm);
        n.AddChild(Box(new Vector3(0.28f, 0.14f, 0.6f), steel, new Vector3(0, 7.3f, -1.6f)));
        var lamp = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.88f, 0.8f), Emission = new Color(1f, 0.9f, 0.7f), Roughness = 0.3f };
        DayNight.RegisterNightEmissive(lamp, 4f);
        n.AddChild(Box(new Vector3(0.24f, 0.04f, 0.5f), lamp, new Vector3(0, 7.22f, -1.6f)));
        var light = new OmniLight3D { OmniRange = range, LightColor = new Color(1f, 0.9f, 0.72f), ShadowEnabled = false, Position = new Vector3(0, 7.0f, -1.6f), OmniAttenuation = 1.3f };
        DayNight.RegisterNightLight(light, energy);
        n.AddChild(light);
        return n;
    }

    /// <summary>Window glass that glows warmly at night. A new material each time, because each one is registered with DayNight.</summary>
    public static StandardMaterial3D Glass(float nightGlow = 1.4f)
    {
        var m = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.45f, 0.55f, 0.62f), AlbedoTexture = Tex.GlassAlbedo(), Roughness = 0.12f, Metallic = 0.3f,
            Emission = new Color(1f, 0.72f, 0.38f), Uv1Triplanar = true, Uv1WorldTriplanar = true, Uv1Scale = new Vector3(0.8f, 0.8f, 0.8f),
        };
        DayNight.RegisterNightEmissive(m, nightGlow);
        return m;
    }

    // ------------------------------------------------------------------ meshes

    public static MeshInstance3D Box(Vector3 size, Color color, Vector3? position = null) => Box(size, Material(color), position);

    public static MeshInstance3D Box(Vector3 size, Godot.Material material, Vector3? position = null)
    {
        var mi = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = material };
        if (position.HasValue) mi.Position = position.Value;
        return mi;
    }

    public static MeshInstance3D Cylinder(float radius, float height, Color color, Vector3? position = null, float topRadius = -1) => Cylinder(radius, height, Material(color), position, topRadius);

    public static MeshInstance3D Cylinder(float radius, float height, Godot.Material material, Vector3? position = null, float topRadius = -1, int segments = 14)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = topRadius < 0 ? radius : topRadius, BottomRadius = radius, Height = height, RadialSegments = segments },
            MaterialOverride = material,
        };
        if (position.HasValue) mi.Position = position.Value;
        return mi;
    }

    public static MeshInstance3D Sphere(float radius, Color color, Vector3? position = null, float heightScale = 1f) => Sphere(radius, Material(color), position, heightScale);

    public static MeshInstance3D Sphere(float radius, Godot.Material material, Vector3? position = null, float heightScale = 1f)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = radius, Height = radius * 2 * heightScale, RadialSegments = 14, Rings = 8 },
            MaterialOverride = material,
        };
        if (position.HasValue) mi.Position = position.Value;
        return mi;
    }

    /// <summary>A capsule standing on its Y axis; `height` is the total height including the rounded ends.</summary>
    public static MeshInstance3D Capsule(float radius, float height, Godot.Material material, Vector3? position = null)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = radius, Height = Mathf.Max(height, radius * 2f), RadialSegments = 12, Rings = 6 },
            MaterialOverride = material,
        };
        if (position.HasValue) mi.Position = position.Value;
        return mi;
    }

    public static MeshInstance3D Prism(Vector3 size, Color color, Vector3? position = null) => Prism(size, Material(color), position);

    public static MeshInstance3D Prism(Vector3 size, Godot.Material material, Vector3? position = null)
    {
        var mi = new MeshInstance3D { Mesh = new PrismMesh { Size = size }, MaterialOverride = material };
        if (position.HasValue) mi.Position = position.Value;
        return mi;
    }

    /// <summary>A solid box: visual plus collision, on the buildings/props layer.</summary>
    public static StaticBody3D SolidBox(Vector3 size, Color color, uint layer = Layers.Buildings, Vector3? position = null) => SolidBox(size, Material(color), layer, position);

    public static StaticBody3D SolidBox(Vector3 size, Godot.Material material, uint layer = Layers.Buildings, Vector3? position = null)
    {
        var body = new StaticBody3D { CollisionLayer = layer, CollisionMask = 0 };
        body.AddChild(Box(size, material));
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        if (position.HasValue) body.Position = position.Value;
        return body;
    }

    /// <summary>A wall-mounted paraffin lantern: iron bracket, glass, and a warm light that comes on at night.</summary>
    public static Node3D Lantern(Vector3 position, float energy = 1.4f, float range = 9f)
    {
        var n = new Node3D { Position = position };
        n.AddChild(Box(new Vector3(0.06f, 0.06f, 0.35f), Iron, new Vector3(0, 0.2f, -0.17f)));
        n.AddChild(Box(new Vector3(0.16f, 0.04f, 0.16f), Iron, new Vector3(0, 0.3f, 0)));
        n.AddChild(Box(new Vector3(0.16f, 0.04f, 0.16f), Iron, new Vector3(0, -0.02f, 0)));
        var glass = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.8f, 0.6f, 0.8f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Emission = new Color(1f, 0.7f, 0.35f), Roughness = 0.2f };
        DayNight.RegisterNightEmissive(glass, 3.5f);
        n.AddChild(Box(new Vector3(0.12f, 0.28f, 0.12f), glass, new Vector3(0, 0.14f, 0)));
        var light = new OmniLight3D { OmniRange = range, LightColor = new Color(1f, 0.75f, 0.45f), ShadowEnabled = false, Position = new Vector3(0, 0.14f, 0.05f), OmniAttenuation = 1.4f };
        DayNight.RegisterNightLight(light, energy);
        n.AddChild(light);
        return n;
    }

    public static Label3D Sign(string text, Vector3 position, float pixelSize = 0.012f, Color? color = null, bool billboard = false)
    {
        return new Label3D
        {
            Text = text,
            Position = position,
            PixelSize = pixelSize,
            FontSize = 48,
            OutlineSize = 10,
            Modulate = color ?? new Color(0.98f, 0.93f, 0.8f),
            Billboard = billboard ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled,
            NoDepthTest = false,
        };
    }

    /// <summary>A painted wooden signboard with lettering, for shop fronts.</summary>
    public static Node3D SignBoard(string text, Vector3 position, float width, Color board, Color lettering)
    {
        var n = new Node3D { Position = position };
        n.AddChild(Box(new Vector3(width, 0.7f, 0.08f), Planks(board)));
        var label = Sign(text, new Vector3(0, 0, 0.05f), 0.009f, lettering);
        label.FontSize = 56;
        label.OutlineSize = 4;
        label.Width = width / 0.009f;
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        n.AddChild(label);
        return n;
    }

    public static Color Parse(string html, Color fallback) => string.IsNullOrEmpty(html) ? fallback : Color.FromHtml(html);

    public static MeshInstance3D WithRotation(this MeshInstance3D mi, Vector3 rotation)
    {
        mi.Rotation = rotation;
        return mi;
    }

    // Era palette
    public static readonly Color Whitewash = new(0.93f, 0.9f, 0.82f);
    public static readonly Color Sandstone = new(0.8f, 0.72f, 0.56f);
    public static readonly Color CorrugatedRed = new(0.55f, 0.2f, 0.15f);
    public static readonly Color CorrugatedGrey = new(0.6f, 0.62f, 0.64f);
    public static readonly Color DarkWood = new(0.3f, 0.2f, 0.12f);
    public static readonly Color Wood = new(0.5f, 0.35f, 0.2f);
    public static readonly Color Stone = new(0.5f, 0.47f, 0.42f);
    public static readonly Color Khaki = new(0.6f, 0.55f, 0.38f);
    public static readonly Color Canvas = new(0.85f, 0.8f, 0.68f);
    public static readonly Color Iron = new(0.25f, 0.25f, 0.27f);
    public static readonly Color GreenPaint = new(0.2f, 0.32f, 0.25f);
}
