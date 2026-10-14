using System;
using System.Collections.Generic;
using Godot;

namespace Veld.World;

/// <summary>
/// Procedural textures, generated once at startup so the project needs no image files. Every
/// texture here tiles seamlessly. To use real photos instead, replace a method body with
/// GD.Load&lt;Texture2D&gt;("res://textures/...") and keep the name.
/// </summary>
public static class Tex
{
    private static readonly Dictionary<string, Texture2D> _cache = new();

    private static Texture2D Cached(string key, Func<Texture2D> make)
    {
        if (_cache.TryGetValue(key, out var t)) return t;
        t = make();
        _cache[key] = t;
        return t;
    }

    // ------------------------------------------------------------------ building blocks

    /// <summary>Seamless fractal noise as a 0..1 float field.</summary>
    private static float[] NoiseField(int size, int seed, float frequency, int octaves = 4, FastNoiseLite.NoiseTypeEnum type = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, FastNoiseLite.FractalTypeEnum fractal = FastNoiseLite.FractalTypeEnum.Fbm)
    {
        var n = new FastNoiseLite { NoiseType = type, Seed = seed, Frequency = frequency, FractalOctaves = octaves, FractalType = fractal };
        if (type == FastNoiseLite.NoiseTypeEnum.Cellular)
        {
            n.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance2Sub;
            n.CellularJitter = 1.0f;
        }
        var img = n.GetSeamlessImage(size, size, false, false, 0.15f, true);
        var data = img.GetData();
        var f = new float[size * size];
        for (int i = 0; i < f.Length; i++) f[i] = data[i] / 255f;
        return f;
    }

    private static ImageTexture FromRgb(int size, float[] r, float[] g, float[] b, float[]? a = null)
    {
        var bytes = new byte[size * size * 4];
        for (int i = 0; i < size * size; i++)
        {
            bytes[i * 4] = (byte)Mathf.Clamp(r[i] * 255f, 0, 255);
            bytes[i * 4 + 1] = (byte)Mathf.Clamp(g[i] * 255f, 0, 255);
            bytes[i * 4 + 2] = (byte)Mathf.Clamp(b[i] * 255f, 0, 255);
            bytes[i * 4 + 3] = a == null ? (byte)255 : (byte)Mathf.Clamp(a[i] * 255f, 0, 255);
        }
        var img = Image.CreateFromData(size, size, false, Image.Format.Rgba8, bytes);
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Normal map from a 0..1 height field (tiling).</summary>
    private static ImageTexture NormalFromHeight(int size, float[] h, float strength)
    {
        var bytes = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float l = h[y * size + (x - 1 + size) % size], r = h[y * size + (x + 1) % size];
                float u = h[((y - 1 + size) % size) * size + x], d = h[((y + 1) % size) * size + x];
                var n = new Vector3((l - r) * strength, (u - d) * strength, 1f).Normalized();
                int i = (y * size + x) * 4;
                bytes[i] = (byte)((n.X * 0.5f + 0.5f) * 255f);
                bytes[i + 1] = (byte)((n.Y * 0.5f + 0.5f) * 255f);
                bytes[i + 2] = (byte)((n.Z * 0.5f + 0.5f) * 255f);
                bytes[i + 3] = 255;
            }
        var img = Image.CreateFromData(size, size, false, Image.Format.Rgba8, bytes);
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffffff) / (float)0xffffff;
        }
    }

    private static (float[] r, float[] g, float[] b) Channels(int n) => (new float[n], new float[n], new float[n]);

    // ------------------------------------------------------------------ ground

    /// <summary>Dry Karoo soil: fine grit, pebbles and patches of lighter sand. Mid grey on average (it is multiplied by the terrain tint).</summary>
    public static Texture2D SoilAlbedo() => Cached("soil_a", () =>
    {
        const int S = 512;
        var fine = NoiseField(S, 11, 0.06f, 5);
        var patches = NoiseField(S, 12, 0.008f, 3);
        var pebbles = NoiseField(S, 13, 0.05f, 2, FastNoiseLite.NoiseTypeEnum.Cellular, FastNoiseLite.FractalTypeEnum.None);
        var (r, g, b) = Channels(S * S);
        for (int i = 0; i < S * S; i++)
        {
            float v = 0.42f + fine[i] * 0.22f + patches[i] * 0.14f;
            float stone = Mathf.SmoothStep(0.78f, 0.95f, pebbles[i]);
            v = Mathf.Lerp(v, 0.36f + fine[i] * 0.1f, stone);
            r[i] = v * 1.04f; g[i] = v * 0.98f; b[i] = v * 0.9f;
        }
        return FromRgb(S, r, g, b);
    });

    public static Texture2D SoilNormal() => Cached("soil_n", () =>
    {
        const int S = 512;
        var fine = NoiseField(S, 11, 0.06f, 5);
        var pebbles = NoiseField(S, 13, 0.05f, 2, FastNoiseLite.NoiseTypeEnum.Cellular, FastNoiseLite.FractalTypeEnum.None);
        var h = new float[S * S];
        for (int i = 0; i < h.Length; i++) h[i] = fine[i] * 0.5f + Mathf.SmoothStep(0.78f, 0.95f, pebbles[i]) * 0.6f;
        return NormalFromHeight(S, h, 2.2f);
    });

    /// <summary>Weathered dolerite: cellular cracks, layered strata, lichen specks.</summary>
    public static Texture2D RockAlbedo() => Cached("rock_a", () =>
    {
        const int S = 512;
        var cells = NoiseField(S, 21, 0.02f, 1, FastNoiseLite.NoiseTypeEnum.Cellular, FastNoiseLite.FractalTypeEnum.None);
        var grain = NoiseField(S, 22, 0.08f, 4);
        var strata = NoiseField(S, 23, 0.01f, 2);
        var (r, g, b) = Channels(S * S);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int i = y * S + x;
                float layer = Mathf.Sin((y / (float)S * 14f + strata[i] * 2f) * Mathf.Tau) * 0.5f + 0.5f;
                float v = 0.38f + grain[i] * 0.25f + layer * 0.08f + cells[i] * 0.18f;
                float crack = Mathf.SmoothStep(0.0f, 0.12f, cells[i]);
                v *= Mathf.Lerp(0.55f, 1f, crack);
                float lichen = Mathf.SmoothStep(0.72f, 0.8f, grain[i]) * 0.5f;
                r[i] = Mathf.Lerp(v * 1.02f, 0.55f, lichen * 0.3f);
                g[i] = Mathf.Lerp(v * 0.97f, 0.6f, lichen * 0.5f);
                b[i] = Mathf.Lerp(v * 0.9f, 0.35f, lichen * 0.3f);
            }
        return FromRgb(S, r, g, b);
    });

    public static Texture2D RockNormal() => Cached("rock_n", () =>
    {
        const int S = 512;
        var cells = NoiseField(S, 21, 0.02f, 1, FastNoiseLite.NoiseTypeEnum.Cellular, FastNoiseLite.FractalTypeEnum.None);
        var grain = NoiseField(S, 22, 0.08f, 4);
        var h = new float[S * S];
        for (int i = 0; i < h.Length; i++) h[i] = cells[i] * 0.8f + grain[i] * 0.25f;
        return NormalFromHeight(S, h, 4f);
    });

    /// <summary>Large-scale variation used by the terrain shader to break up tiling.</summary>
    public static Texture2D Macro() => Cached("macro", () =>
    {
        const int S = 256;
        var a = NoiseField(S, 31, 0.02f, 3);
        return FromRgb(S, a, a, a);
    });

    // ------------------------------------------------------------------ buildings

    public static Texture2D PlasterAlbedo() => Cached("plaster_a", () =>
    {
        const int S = 256;
        var n = NoiseField(S, 41, 0.08f, 4);
        var stains = NoiseField(S, 42, 0.012f, 2);
        var (r, g, b) = Channels(S * S);
        for (int i = 0; i < S * S; i++)
        {
            float v = 0.86f + n[i] * 0.12f - Mathf.SmoothStep(0.6f, 0.9f, stains[i]) * 0.1f;
            r[i] = v; g[i] = v * 0.99f; b[i] = v * 0.96f;
        }
        return FromRgb(S, r, g, b);
    });

    public static Texture2D PlasterNormal() => Cached("plaster_n", () =>
    {
        const int S = 256;
        var n = NoiseField(S, 41, 0.08f, 4);
        return NormalFromHeight(S, n, 1.1f);
    });

    /// <summary>Corrugated iron: vertical corrugations with rust streaks. One tile = 1 m.</summary>
    public static Texture2D IronAlbedo() => Cached("iron_a", () =>
    {
        const int S = 256;
        var rust = NoiseField(S, 51, 0.03f, 4);
        var streak = NoiseField(S, 52, 0.02f, 3);
        var (r, g, b) = Channels(S * S);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int i = y * S + x;
                float wave = Mathf.Sin(x / (float)S * 13f * Mathf.Tau) * 0.5f + 0.5f;
                float v = 0.78f + wave * 0.18f;
                float ru = Mathf.SmoothStep(0.55f, 0.85f, rust[i] * 0.7f + streak[(y / 3 * S + x) % (S * S)] * 0.3f);
                r[i] = Mathf.Lerp(v, 0.5f, ru); g[i] = Mathf.Lerp(v, 0.3f, ru); b[i] = Mathf.Lerp(v, 0.18f, ru);
            }
        return FromRgb(S, r, g, b);
    });

    public static Texture2D IronNormal() => Cached("iron_n", () =>
    {
        const int S = 256;
        var h = new float[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                h[y * S + x] = Mathf.Sin(x / (float)S * 13f * Mathf.Tau) * 0.5f + 0.5f;
        return NormalFromHeight(S, h, 6f);
    });

    /// <summary>Weathered planks running horizontally, 8 boards per tile.</summary>
    public static Texture2D PlankAlbedo() => Cached("plank_a", () =>
    {
        const int S = 256;
        var grain = NoiseField(S, 61, 0.1f, 3);
        var (r, g, b) = Channels(S * S);
        const int boards = 8;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int i = y * S + x;
                int board = y * boards / S;
                float within = (y * boards / (float)S) - board;
                float gap = within < 0.06f || within > 0.94f ? 0.45f : 1f;
                float tone = 0.72f + Hash(board, 0, 7) * 0.25f;
                float g1 = grain[(y * S + (x + board * 37) % S)] ;
                float v = tone * (0.8f + g1 * 0.3f) * gap;
                r[i] = v; g[i] = v * 0.82f; b[i] = v * 0.62f;
            }
        return FromRgb(S, r, g, b);
    });

    public static Texture2D PlankNormal() => Cached("plank_n", () =>
    {
        const int S = 256;
        var grain = NoiseField(S, 61, 0.1f, 3);
        var h = new float[S * S];
        const int boards = 8;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int board = y * boards / S;
                float within = (y * boards / (float)S) - board;
                float gap = within < 0.06f || within > 0.94f ? 0f : 1f;
                h[y * S + x] = gap * (0.7f + grain[y * S + x] * 0.3f);
            }
        return NormalFromHeight(S, h, 3f);
    });

    /// <summary>Dressed sandstone blocks with mortar lines, 4 courses per tile.</summary>
    public static Texture2D StoneAlbedo() => Cached("stone_a", () =>
    {
        const int S = 256;
        var grain = NoiseField(S, 71, 0.07f, 4);
        var (r, g, b) = Channels(S * S);
        const int courses = 4, perRow = 2;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int i = y * S + x;
                int row = y * courses / S;
                float wy = (y * courses / (float)S) - row;
                float xo = (x / (float)S + (row % 2) * 0.5f / perRow) * perRow;
                int col = (int)Mathf.Floor(xo);
                float wx = xo - col;
                bool mortar = wy < 0.07f || wy > 0.93f || wx < 0.04f || wx > 0.96f;
                float tone = 0.78f + Hash(col, row, 3) * 0.2f;
                float v = mortar ? 0.7f : tone * (0.85f + grain[i] * 0.25f);
                r[i] = v * (mortar ? 0.95f : 1.0f); g[i] = v * (mortar ? 0.93f : 0.92f); b[i] = v * (mortar ? 0.9f : 0.78f);
            }
        return FromRgb(S, r, g, b);
    });

    public static Texture2D StoneNormal() => Cached("stone_n", () =>
    {
        const int S = 256;
        var grain = NoiseField(S, 71, 0.07f, 4);
        var h = new float[S * S];
        const int courses = 4, perRow = 2;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int row = y * courses / S;
                float wy = (y * courses / (float)S) - row;
                float xo = (x / (float)S + (row % 2) * 0.5f / perRow) * perRow;
                int col = (int)Mathf.Floor(xo);
                float wx = xo - col;
                bool mortar = wy < 0.07f || wy > 0.93f || wx < 0.04f || wx > 0.96f;
                h[y * S + x] = mortar ? 0f : 0.8f + grain[y * S + x] * 0.2f;
            }
        return NormalFromHeight(S, h, 3.5f);
    });

    /// <summary>Window glass: faint reflections and dust. Used with a little emission at night.</summary>
    public static Texture2D GlassAlbedo() => Cached("glass_a", () =>
    {
        const int S = 64;
        var n = NoiseField(S, 81, 0.2f, 2);
        var (r, g, b) = Channels(S * S);
        for (int i = 0; i < S * S; i++) { float v = 0.35f + n[i] * 0.2f; r[i] = v * 0.85f; g[i] = v * 0.95f; b[i] = v; }
        return FromRgb(S, r, g, b);
    });

    // ------------------------------------------------------------------ vegetation

    /// <summary>A clump of dry grass blades with alpha. Drawn, not noise.</summary>
    public static Texture2D GrassBlade() => Cached("blade", () =>
    {
        const int S = 128;
        var (r, g, b) = Channels(S * S);
        var a = new float[S * S];
        var rng = new Random(5);
        for (int k = 0; k < 9; k++)
        {
            float baseX = 0.15f + (float)rng.NextDouble() * 0.7f;
            float lean = ((float)rng.NextDouble() - 0.5f) * 0.6f;
            float height = 0.55f + (float)rng.NextDouble() * 0.45f;
            float width = 0.035f + (float)rng.NextDouble() * 0.03f;
            float tone = 0.75f + (float)rng.NextDouble() * 0.4f;
            for (int y = 0; y < S; y++)
            {
                float t = 1f - y / (float)S; // 0 at the bottom, 1 at the top
                if (t > height) continue;
                float u = t / height;
                float cx = baseX + lean * u * u;
                float w = width * (1f - u * 0.9f);
                for (int x = 0; x < S; x++)
                {
                    float dx = Mathf.Abs(x / (float)S - cx);
                    if (dx > w) continue;
                    int i = y * S + x;
                    float shade = (0.55f + u * 0.5f) * tone;
                    r[i] = Mathf.Min(1f, 0.62f * shade); g[i] = Mathf.Min(1f, 0.55f * shade); b[i] = Mathf.Min(1f, 0.28f * shade);
                    a[i] = 1f;
                }
            }
        }
        return FromRgb(S, r, g, b, a);
    });

    /// <summary>Leaf mass with holes, for thorn tree canopies and shrubs (alpha scissored).</summary>
    public static Texture2D Leaf() => Cached("leaf", () =>
    {
        const int S = 256;
        var n = NoiseField(S, 91, 0.05f, 4);
        var fine = NoiseField(S, 92, 0.2f, 2);
        var (r, g, b) = Channels(S * S);
        var a = new float[S * S];
        for (int i = 0; i < S * S; i++)
        {
            float v = 0.7f + fine[i] * 0.5f;
            r[i] = 0.42f * v; g[i] = 0.5f * v; b[i] = 0.22f * v;
            a[i] = n[i] * 0.6f + fine[i] * 0.4f;
        }
        return FromRgb(S, r, g, b, a);
    });

    // ------------------------------------------------------------------ sky

    /// <summary>Equirectangular star field for the physical sky's night texture.</summary>
    public static Texture2D Stars() => Cached("stars", () =>
    {
        const int W = 2048, H = 1024;
        var bytes = new byte[W * H * 4];
        var rng = new Random(1900);
        var band = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Seed = 3, Frequency = 0.004f, FractalOctaves = 3 };
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                // faint milky way band
                float tilt = Mathf.Sin(x / (float)W * Mathf.Tau) * 0.18f;
                float d = Mathf.Abs(y / (float)H - 0.5f - tilt);
                float mw = Mathf.Max(0f, 1f - d / 0.12f) * (band.GetNoise2D(x, y) * 0.5f + 0.5f) * 0.12f;
                int i = (y * W + x) * 4;
                bytes[i] = (byte)(mw * 200); bytes[i + 1] = (byte)(mw * 210); bytes[i + 2] = (byte)(mw * 255); bytes[i + 3] = 255;
            }
        for (int k = 0; k < 6000; k++)
        {
            int x = rng.Next(W), y = rng.Next(H);
            float m = (float)rng.NextDouble();
            float bright = m < 0.03f ? 1f : m < 0.2f ? 0.6f : 0.3f;
            float warm = (float)rng.NextDouble();
            void Put(int px, int py, float f)
            {
                if (px < 0 || py < 0 || px >= W || py >= H) return;
                int i = (py * W + px) * 4;
                bytes[i] = (byte)Mathf.Min(255, bytes[i] + bright * f * 255 * (0.9f + warm * 0.1f));
                bytes[i + 1] = (byte)Mathf.Min(255, bytes[i + 1] + bright * f * 255 * 0.95f);
                bytes[i + 2] = (byte)Mathf.Min(255, bytes[i + 2] + bright * f * 255 * (1f - warm * 0.15f));
            }
            Put(x, y, 1f);
            if (bright > 0.5f) { Put(x + 1, y, 0.35f); Put(x - 1, y, 0.35f); Put(x, y + 1, 0.35f); Put(x, y - 1, 0.35f); }
        }
        var img = Image.CreateFromData(W, H, false, Image.Format.Rgba8, bytes);
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    });

    /// <summary>Seamless cloud noise for the cloud layer.</summary>
    public static Texture2D CloudNoise() => Cached("clouds", () =>
    {
        const int S = 512;
        var a = NoiseField(S, 101, 0.006f, 5);
        var b = NoiseField(S, 102, 0.02f, 3);
        var (r, g, bb) = Channels(S * S);
        for (int i = 0; i < S * S; i++) { r[i] = a[i]; g[i] = b[i]; bb[i] = 0; }
        return FromRgb(S, r, g, bb);
    });

    // ------------------------------------------------------------------ modern-era surfaces

    /// <summary>Asphalt across a road: u spans the full width (yellow edge lines, dashed white centre line), v tiles every 6 m.</summary>
    public static Texture2D Asphalt() => Cached("asphalt", () =>
    {
        const int S = 512;
        var grain = NoiseField(S, 111, 0.12f, 4);
        var patches = NoiseField(S, 112, 0.01f, 3);
        var (r, g, b) = Channels(S * S);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int i = y * S + x;
                float u = x / (float)S, v = y / (float)S;
                float tone = 0.22f + grain[i] * 0.1f + patches[i] * 0.08f;
                float cr = tone, cg = tone, cb = tone * 1.04f;
                bool edge = Mathf.Abs(u - 0.06f) < 0.012f || Mathf.Abs(u - 0.94f) < 0.012f;
                bool centre = Mathf.Abs(u - 0.5f) < 0.011f && v < 0.36f;
                if (edge) { cr = 0.75f; cg = 0.65f; cb = 0.18f; }
                else if (centre) { cr = cg = cb = 0.85f; }
                float wear = 0.65f + grain[i] * 0.35f;
                if (edge || centre) { cr *= wear; cg *= wear; cb *= wear; }
                r[i] = cr; g[i] = cg; b[i] = cb;
            }
        return FromRgb(S, r, g, b);
    });

    public static Texture2D Concrete() => Cached("concrete", () =>
    {
        const int S = 256;
        var n = NoiseField(S, 121, 0.1f, 4);
        var cracks = NoiseField(S, 122, 0.03f, 1, FastNoiseLite.NoiseTypeEnum.Cellular, FastNoiseLite.FractalTypeEnum.None);
        var (r, g, b) = Channels(S * S);
        for (int i = 0; i < S * S; i++)
        {
            float v = (0.7f + n[i] * 0.2f) * Mathf.Lerp(0.8f, 1f, Mathf.SmoothStep(0.0f, 0.06f, cracks[i]));
            r[i] = v; g[i] = v; b[i] = v * 0.98f;
        }
        return FromRgb(S, r, g, b);
    });

    /// <summary>One storey of a plastered house wall: 4 m wide x 3 m tall, with a window and a darker plinth band. Multiplied by the building tint.</summary>
    public static Texture2D HouseWall() => Cached("housewall", () =>
    {
        const int W = 256, H = 192; // 4 m x 3 m
        var n = NoiseField(256, 131, 0.08f, 4);
        var bytes = new byte[W * H * 4];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float mx = x / 64f, my = (H - 1 - y) / 64f; // metres; my is height above the floor
                float v = 0.9f + n[(y % 256) * 256 + (x % 256)] * 0.1f;
                float cr = v, cg = v, cb = v;
                if (my < 0.25f) { cr = 0.5f; cg = 0.47f; cb = 0.44f; }
                Window(mx, my, n, x, y, ref cr, ref cg, ref cb);
                int i = (y * W + x) * 4;
                bytes[i] = (byte)(Mathf.Clamp(cr, 0, 1) * 255); bytes[i + 1] = (byte)(Mathf.Clamp(cg, 0, 1) * 255); bytes[i + 2] = (byte)(Mathf.Clamp(cb, 0, 1) * 255); bytes[i + 3] = 255;
            }
        var img = Image.CreateFromData(W, H, false, Image.Format.Rgba8, bytes);
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    });

    /// <summary>Clay roof tiles, 1 m tile: 4 courses of 0.25 m, 3 tiles per course.</summary>
    public static Texture2D RoofTiles() => Cached("tiles", () =>
    {
        const int S = 256;
        var n = NoiseField(S, 141, 0.1f, 3);
        var (r, g, b) = Channels(S * S);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int i = y * S + x;
                float cy = (y / (float)S * 4f) % 1f;
                int course = (int)(y / (float)S * 4f);
                float cx = ((x / (float)S * 3f) + (course % 2) * 0.5f) % 1f;
                float curve = 0.75f + 0.25f * Mathf.Cos((cx - 0.5f) * Mathf.Pi);
                float lip = cy < 0.12f ? 0.55f : 1f;
                float v = (0.72f + n[i] * 0.28f) * curve * lip;
                r[i] = v; g[i] = v * 0.97f; b[i] = v * 0.95f;
            }
        return FromRgb(S, r, g, b);
    });

    public static Texture2D RoofTilesNormal() => Cached("tiles_n", () =>
    {
        const int S = 256;
        var h = new float[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float cy = (y / (float)S * 4f) % 1f;
                int course = (int)(y / (float)S * 4f);
                float cx = ((x / (float)S * 3f) + (course % 2) * 0.5f) % 1f;
                h[y * S + x] = 0.5f * Mathf.Cos((cx - 0.5f) * Mathf.Pi) + (cy < 0.12f ? 0f : 0.3f);
            }
        return NormalFromHeight(S, h, 3f);
    });

    /// <summary>Face brick, 1 m tile: 13 courses.</summary>
    public static Texture2D BrickAlbedo() => Cached("brick_a", () =>
    {
        const int S = 256;
        var n = NoiseField(S, 151, 0.12f, 3);
        var (r, g, b) = Channels(S * S);
        const int courses = 13, perRow = 4;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int i = y * S + x;
                int row = y * courses / S;
                float wy = (y * courses / (float)S) - row;
                float xo = (x / (float)S + (row % 2) * 0.5f / perRow) * perRow;
                int col = (int)Mathf.Floor(xo);
                float wx = xo - col;
                bool mortar = wy < 0.12f || wx < 0.05f;
                float tone = 0.8f + Hash(col, row, 9) * 0.3f;
                float v = mortar ? 0.72f : tone * (0.85f + n[i] * 0.2f);
                r[i] = mortar ? v * 0.95f : v * 0.78f; g[i] = mortar ? v * 0.93f : v * 0.5f; b[i] = mortar ? v * 0.9f : v * 0.4f;
            }
        return FromRgb(S, r, g, b);
    });

    public static Texture2D BrickNormal() => Cached("brick_n", () =>
    {
        const int S = 256;
        var h = new float[S * S];
        const int courses = 13, perRow = 4;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int row = y * courses / S;
                float wy = (y * courses / (float)S) - row;
                float xo = (x / (float)S + (row % 2) * 0.5f / perRow) * perRow;
                float wx = xo - Mathf.Floor(xo);
                h[y * S + x] = (wy < 0.12f || wx < 0.05f) ? 0f : 1f;
            }
        return NormalFromHeight(S, h, 2.5f);
    });

    /// <summary>Shared window painter for the house walls: frame, glass, mullions, sill and burglar bars.</summary>
    private static bool Window(float mx, float my, float[] fine, int x, int y, ref float cr, ref float cg, ref float cb)
    {
        bool inWin = Mathf.Abs(mx - 2f) < 0.6f && my > 1.0f && my < 2.1f;
        bool frame = Mathf.Abs(mx - 2f) < 0.68f && my > 0.94f && my < 2.17f && !inWin;
        bool mullion = inWin && (Mathf.Abs(mx - 2f) < 0.025f || Mathf.Abs(my - 1.55f) < 0.025f);
        bool bar = inWin && (Mathf.Abs(((mx - 1.4f) % 0.3f) - 0.15f) < 0.012f);
        if (frame || mullion) { cr = 0.9f; cg = 0.9f; cb = 0.88f; }
        else if (bar) { cr = cg = cb = 0.12f; }
        else if (inWin)
        {
            float gl = 0.3f + fine[((y * 3) % 256) * 256 + ((x * 3) % 256)] * 0.25f;
            cr = gl * 0.75f; cg = gl * 0.85f; cb = gl;
        }
        if (Mathf.Abs(mx - 2f) < 0.75f && my > 0.86f && my < 0.94f) { cr *= 0.75f; cg *= 0.75f; cb *= 0.75f; }
        return inWin || frame;
    }

    /// <summary>Face-brick house wall, 4 m x 3 m per tile, with a barred window. Multiplied by the building tint.</summary>
    public static Texture2D BrickWall() => Cached("brickwall", () =>
    {
        const int W = 256, H = 192;
        var fine = NoiseField(256, 161, 0.08f, 4);
        var bytes = new byte[W * H * 4];
        const float course = 0.085f, brick = 0.23f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float mx = x / 64f, my = (H - 1 - y) / 64f;
                int row = (int)(my / course);
                float wy = (my / course) - row;
                float xo = mx / brick + (row % 2) * 0.5f;
                int col = (int)Mathf.Floor(xo);
                float wx = xo - col;
                bool mortar = wy < 0.14f || wx < 0.06f;
                float tone = 0.78f + Hash(col, row, 5) * 0.3f;
                float n = fine[(y % 256) * 256 + (x % 256)];
                float cr, cg, cb;
                if (mortar) { cr = 0.68f; cg = 0.66f; cb = 0.62f; }
                else { cr = tone * (0.78f + n * 0.2f); cg = tone * (0.45f + n * 0.15f); cb = tone * (0.35f + n * 0.1f); }
                if (my < 0.25f) { cr *= 0.75f; cg *= 0.75f; cb *= 0.75f; }
                Window(mx, my, fine, x, y, ref cr, ref cg, ref cb);
                int i = (y * W + x) * 4;
                bytes[i] = (byte)(Mathf.Clamp(cr, 0, 1) * 255); bytes[i + 1] = (byte)(Mathf.Clamp(cg, 0, 1) * 255); bytes[i + 2] = (byte)(Mathf.Clamp(cb, 0, 1) * 255); bytes[i + 3] = 255;
            }
        var img = Image.CreateFromData(W, H, false, Image.Format.Rgba8, bytes);
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    });

    /// <summary>A front door with a security gate in front of it (1 m x 2.1 m).</summary>
    public static Texture2D Door() => Cached("door", () =>
    {
        const int W = 64, H = 136;
        var bytes = new byte[W * H * 4];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float mx = x / 64f, my = (H - 1 - y) / 64f;
                float cr = 0.32f, cg = 0.2f, cb = 0.12f; // dark varnished door
                bool frame = mx < 0.06f || mx > 0.94f || my > 2.02f;
                bool panel = (Mathf.Abs(mx - 0.5f) < 0.3f) && ((my > 0.25f && my < 0.9f) || (my > 1.1f && my < 1.85f)) && !(Mathf.Abs(mx - 0.5f) < 0.26f && ((my > 0.29f && my < 0.86f) || (my > 1.14f && my < 1.81f)));
                if (frame) { cr = 0.85f; cg = 0.84f; cb = 0.8f; }
                else if (panel) { cr *= 0.6f; cg *= 0.6f; cb *= 0.6f; }
                // security gate bars
                bool bar = Mathf.Abs(((mx + 0.05f) % 0.16f) - 0.08f) < 0.012f && my < 2.0f;
                bool rail = (Mathf.Abs(my - 0.3f) < 0.015f || Mathf.Abs(my - 1.0f) < 0.015f || Mathf.Abs(my - 1.9f) < 0.015f);
                if (bar || rail) { cr = 0.15f; cg = 0.15f; cb = 0.16f; }
                if (Mathf.Abs(mx - 0.82f) < 0.03f && Mathf.Abs(my - 1.0f) < 0.03f) { cr = 0.8f; cg = 0.75f; cb = 0.5f; }
                int i = (y * W + x) * 4;
                bytes[i] = (byte)(cr * 255); bytes[i + 1] = (byte)(cg * 255); bytes[i + 2] = (byte)(cb * 255); bytes[i + 3] = 255;
            }
        var img = Image.CreateFromData(W, H, false, Image.Format.Rgba8, bytes);
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    });

    /// <summary>Precast concrete boundary wall: 1.5 m panels of 0.3 m slabs between posts. One tile = 1.5 m x 1.8 m.</summary>
    public static Texture2D Precast() => Cached("precast", () =>
    {
        const int W = 192, H = 230;
        var n = NoiseField(256, 171, 0.1f, 3);
        var bytes = new byte[W * H * 4];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float mx = x / 128f, my = (H - 1 - y) / 128f; // metres
                float v = 0.68f + n[(y % 256) * 256 + (x % 256)] * 0.14f;
                bool post = mx < 0.07f || mx > 1.43f;
                bool joint = Mathf.Abs((my % 0.3f) - 0.0f) < 0.012f || Mathf.Abs((my % 0.3f) - 0.3f) < 0.012f;
                if (post) v *= 0.9f;
                else if (joint) v *= 0.7f;
                if (my < 0.12f) v *= 0.8f;
                int i = (y * W + x) * 4;
                bytes[i] = (byte)(v * 255); bytes[i + 1] = (byte)(v * 0.99f * 255); bytes[i + 2] = (byte)(v * 0.96f * 255); bytes[i + 3] = 255;
            }
        var img = Image.CreateFromData(W, H, false, Image.Format.Rgba8, bytes);
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    });
}
