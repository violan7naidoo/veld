using System;
using System.Collections.Generic;
using Godot;
using Veld.Core;

namespace Veld.World;

/// <summary>
/// The ground. Either procedural (noise: a rolling plateau with flat-topped koppies) or a real
/// heightmap (SRTM elevation converted by tools/osm_to_map.py). Flattened under towns and along roads,
/// marked per cell with what sits on it (road, grass field, paving, building, water...) so the shader,
/// the vegetation and the builders all agree. One mesh plus a HeightMapShape3D for collision.
/// GetHeight() is what everything else uses to stand on the ground.
/// </summary>
public partial class Terrain : Node3D
{
    public enum Surface : byte { Veld = 0, Road = 1, Rail = 2, Town = 3, Grass = 4, Paved = 5, Built = 6, Water = 7, Wood = 8 }

    public float Size { get; private set; }
    public float CellSize { get; private set; }
    public int N { get; private set; }
    private float[] _heights = Array.Empty<float>();
    private Color[] _colors = Array.Empty<Color>();
    private Surface[] _surface = Array.Empty<Surface>();
    private FastNoiseLite _base = null!;
    private FastNoiseLite _koppie = null!;
    private FastNoiseLite _detail = null!;
    private float _heightScale;
    private HeightmapData? _hm;
    private WorldData _world = null!;
    private bool _subtropical;
    private List<Flatten> _flats = new();
    private List<Strip> _strips = new();

    public readonly struct Flatten
    {
        public readonly Vector2 Center; public readonly float Radius; public readonly float Blend;
        public Flatten(Vector2 c, float r, float b) { Center = c; Radius = r; Blend = b; }
    }

    public readonly struct Strip
    {
        public readonly List<Vector2> Points; public readonly float HalfWidth; public readonly float Blend; public readonly Surface Kind; public readonly bool Loop; public readonly float Depth;
        public Strip(List<Vector2> pts, float halfWidth, float blend, Surface kind, bool loop, float depth = 0f) { Points = pts; HalfWidth = halfWidth; Blend = blend; Kind = kind; Loop = loop; Depth = depth; }
    }

    public readonly struct AreaFill
    {
        public readonly List<Vector2> Polygon; public readonly Surface Kind; public readonly bool Level; public readonly float Blend;
        public AreaFill(List<Vector2> polygon, Surface kind, bool level, float blend) { Polygon = polygon; Kind = kind; Level = level; Blend = blend; }
    }

    private string _mapFolder = "";

    public void Generate(WorldData world, HeightmapData? heightmap, List<Flatten> flats, List<Strip> strips, List<AreaFill> areas, List<List<Vector2>> built, string mapFolder)
    {
        _mapFolder = mapFolder;
        Size = world.SizeMeters;
        CellSize = world.CellSize;
        _heightScale = world.HeightScale;
        _world = world;
        _hm = heightmap != null && heightmap.Heights.Length == heightmap.Cols * heightmap.Rows && heightmap.Cols > 1 ? heightmap : null;
        _subtropical = world.Biome == "subtropical";
        N = (int)MathF.Round(Size / CellSize) + 1;
        _flats = flats;
        _strips = strips;
        _heights = new float[N * N];
        _surface = new Surface[N * N];

        _base = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Seed = world.Seed, Frequency = 0.0009f, FractalOctaves = 4, FractalLacunarity = 2.1f, FractalGain = 0.45f };
        _koppie = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = world.Seed + 7, Frequency = 0.0035f, FractalOctaves = 3, FractalType = FastNoiseLite.FractalTypeEnum.Ridged };
        _detail = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Seed = world.Seed + 13, Frequency = 0.02f, FractalOctaves = 2 };

        // 1. raw heights
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                var p = CellToWorld(x, z);
                _heights[z * N + x] = RawHeight(p.X, p.Y);
            }

        // 2. flatten under towns and farms
        foreach (var f in flats)
        {
            float target = RawHeight(f.Center.X, f.Center.Y);
            int r = (int)MathF.Ceiling((f.Radius + f.Blend) / CellSize) + 1;
            var (cx, cz) = WorldToCell(f.Center);
            for (int z = Math.Max(0, cz - r); z <= Math.Min(N - 1, cz + r); z++)
                for (int x = Math.Max(0, cx - r); x <= Math.Min(N - 1, cx + r); x++)
                {
                    float d = CellToWorld(x, z).DistanceTo(f.Center);
                    if (d > f.Radius + f.Blend) continue;
                    float t = d <= f.Radius ? 1f : 1f - (d - f.Radius) / f.Blend;
                    int i = z * N + x;
                    _heights[i] = Mathf.Lerp(_heights[i], target, Smooth(t));
                    if (d <= f.Radius) _surface[i] = Surface.Town;
                }
        }

        // 3. roads, railway and watercourses: level across their width, follow the land along their length
        foreach (var s in strips)
        {
            int segs = s.Loop ? s.Points.Count : s.Points.Count - 1;
            for (int k = 0; k < segs; k++)
            {
                var a = s.Points[k];
                var b = s.Points[(k + 1) % s.Points.Count];
                float reach = s.HalfWidth + s.Blend;
                var min = new Vector2(MathF.Min(a.X, b.X) - reach, MathF.Min(a.Y, b.Y) - reach);
                var max = new Vector2(MathF.Max(a.X, b.X) + reach, MathF.Max(a.Y, b.Y) + reach);
                var (x0, z0) = WorldToCell(min);
                var (x1, z1) = WorldToCell(max);
                for (int z = Math.Max(0, z0); z <= Math.Min(N - 1, z1); z++)
                    for (int x = Math.Max(0, x0); x <= Math.Min(N - 1, x1); x++)
                    {
                        var p = CellToWorld(x, z);
                        var q = ClosestOnSegment(p, a, b);
                        float d = p.DistanceTo(q);
                        if (d > reach) continue;
                        float t = d <= s.HalfWidth ? 1f : 1f - (d - s.HalfWidth) / s.Blend;
                        int i = z * N + x;
                        float target = (_surface[i] == Surface.Town ? _heights[i] : SmoothedRaw(q, a, b)) - s.Depth;
                        _heights[i] = Mathf.Lerp(_heights[i], target, Smooth(t));
                        if (d <= s.HalfWidth && (_surface[i] != Surface.Town || s.Kind == Surface.Rail || s.Kind == Surface.Water)) _surface[i] = s.Kind;
                    }
            }
        }

        // 4. land-use polygons: sports fields are levelled, everything else just changes the surface
        foreach (var area in areas) FillPolygon(area);

        // 5. building footprints: mark the ground so no plants grow through floors
        foreach (var poly in built) MarkPolygon(poly, Surface.Built);

        BuildMesh();
        BuildCollision();
    }

    private float RawHeight(float x, float z)
    {
        if (_hm != null)
            return SampleHeightmap(x, z) + _detail.GetNoise2D(x, z) * 0.35f;

        float plateau = _base.GetNoise2D(x, z);                       // -1..1 gentle undulation
        float ridged = Mathf.Max(0f, _koppie.GetNoise2D(x, z));      // 0..1, ridges are the crests
        float mesa = Mathf.SmoothStep(0.42f, 0.72f, ridged);         // flat-topped koppies with steep sides
        float koppie = mesa * KoppieMask(x, z);                       // no koppies on top of towns, roads or the line
        float detail = _detail.GetNoise2D(x, z) * 0.05f;
        float h = plateau * 0.35f + koppie * 0.85f + detail;
        h += (z / Size) * -0.15f;
        return h * _heightScale;
    }

    /// <summary>Catmull-Rom (bicubic) sample of the elevation grid, clamped at the edges.</summary>
    private float SampleHeightmap(float x, float z)
    {
        var hm = _hm!;
        float fx = (x - hm.OriginX) / hm.StepM;
        float fz = (z - hm.OriginZ) / hm.StepM;
        int ix = (int)MathF.Floor(fx), iz = (int)MathF.Floor(fz);
        float tx = fx - ix, tz = fz - iz;
        float G(int cx, int cz) => hm.Heights[Math.Clamp(cz, 0, hm.Rows - 1) * hm.Cols + Math.Clamp(cx, 0, hm.Cols - 1)];
        float Cub(float p0, float p1, float p2, float p3, float t) =>
            0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
        Span<float> rows = stackalloc float[4];
        for (int j = -1; j <= 2; j++)
            rows[j + 1] = Cub(G(ix - 1, iz + j), G(ix, iz + j), G(ix + 1, iz + j), G(ix + 2, iz + j), tx);
        return Cub(rows[0], rows[1], rows[2], rows[3], tz);
    }

    /// <summary>0 inside and near towns, roads and the railway, 1 out on the open veld. Keeps hills away from the places people build.</summary>
    private float KoppieMask(float x, float z)
    {
        float m = 1f;
        var p = new Vector2(x, z);
        foreach (var f in _flats)
        {
            float d = p.DistanceTo(f.Center) - f.Radius;
            m *= Smooth(Mathf.Clamp(d / 280f, 0f, 1f));
            if (m <= 0f) return 0f;
        }
        foreach (var s in _strips)
        {
            int segs = s.Loop ? s.Points.Count : s.Points.Count - 1;
            float best = float.MaxValue;
            for (int k = 0; k < segs; k++)
            {
                var q = ClosestOnSegment(p, s.Points[k], s.Points[(k + 1) % s.Points.Count]);
                best = Mathf.Min(best, p.DistanceSquaredTo(q));
            }
            float d = Mathf.Sqrt(best) - s.HalfWidth - 30f;
            m *= Smooth(Mathf.Clamp(d / 160f, 0f, 1f));
            if (m <= 0f) return 0f;
        }
        return m;
    }

    /// <summary>Height along a road centreline, averaged over the segment so the road does not bump.</summary>
    private float SmoothedRaw(Vector2 q, Vector2 a, Vector2 b)
    {
        var dir = (b - a);
        float len = dir.Length();
        if (len < 1f) return RawHeight(q.X, q.Y);
        dir /= len;
        float sum = 0; int n = 0;
        for (float o = -12f; o <= 12f; o += 4f)
        {
            var p = q + dir * o;
            sum += RawHeight(p.X, p.Y); n++;
        }
        return sum / n;
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float l2 = ab.LengthSquared();
        if (l2 < 0.0001f) return a;
        float t = Mathf.Clamp((p - a).Dot(ab) / l2, 0f, 1f);
        return a + ab * t;
    }

    public static bool PointInPolygon(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    private static float DistanceToEdges(Vector2 p, List<Vector2> poly)
    {
        float best = float.MaxValue;
        for (int i = 0; i < poly.Count; i++)
            best = Mathf.Min(best, p.DistanceSquaredTo(ClosestOnSegment(p, poly[i], poly[(i + 1) % poly.Count])));
        return Mathf.Sqrt(best);
    }

    private void FillPolygon(AreaFill area)
    {
        var poly = area.Polygon;
        if (poly.Count < 3) return;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        float sum = 0f;
        foreach (var p in poly)
        {
            minX = MathF.Min(minX, p.X); maxX = MathF.Max(maxX, p.X); minZ = MathF.Min(minZ, p.Y); maxZ = MathF.Max(maxZ, p.Y);
            var (cx, cz) = WorldToCell(p);
            sum += _heights[Math.Clamp(cz, 0, N - 1) * N + Math.Clamp(cx, 0, N - 1)];
        }
        float target = sum / poly.Count;
        float reach = area.Level ? area.Blend : 0f;
        var (x0, z0) = WorldToCell(new Vector2(minX - reach, minZ - reach));
        var (x1, z1) = WorldToCell(new Vector2(maxX + reach, maxZ + reach));
        for (int z = Math.Max(0, z0); z <= Math.Min(N - 1, z1); z++)
            for (int x = Math.Max(0, x0); x <= Math.Min(N - 1, x1); x++)
            {
                var p = CellToWorld(x, z);
                int i = z * N + x;
                bool inside = PointInPolygon(p, poly);
                if (inside && area.Kind != Surface.Veld && _surface[i] != Surface.Road) _surface[i] = area.Kind;
                if (!area.Level) continue;
                if (inside) _heights[i] = target;
                else
                {
                    float d = DistanceToEdges(p, poly);
                    if (d < reach) _heights[i] = Mathf.Lerp(_heights[i], target, Smooth(1f - d / reach));
                }
            }
    }

    private void MarkPolygon(List<Vector2> poly, Surface kind)
    {
        if (poly.Count < 3) return;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var p in poly) { minX = MathF.Min(minX, p.X); maxX = MathF.Max(maxX, p.X); minZ = MathF.Min(minZ, p.Y); maxZ = MathF.Max(maxZ, p.Y); }
        var (x0, z0) = WorldToCell(new Vector2(minX, minZ));
        var (x1, z1) = WorldToCell(new Vector2(maxX, maxZ));
        for (int z = Math.Max(0, z0); z <= Math.Min(N - 1, z1); z++)
            for (int x = Math.Max(0, x0); x <= Math.Min(N - 1, x1); x++)
            {
                int i = z * N + x;
                if (_surface[i] == Surface.Road) continue;
                if (PointInPolygon(CellToWorld(x, z), poly)) _surface[i] = kind;
            }
    }

    public Vector2 CellToWorld(int x, int z) => new((x - (N - 1) * 0.5f) * CellSize, (z - (N - 1) * 0.5f) * CellSize);

    public (int x, int z) WorldToCell(Vector2 p) =>
        ((int)MathF.Round(p.X / CellSize + (N - 1) * 0.5f), (int)MathF.Round(p.Y / CellSize + (N - 1) * 0.5f));

    private float H(int x, int z) => _heights[Math.Clamp(z, 0, N - 1) * N + Math.Clamp(x, 0, N - 1)];

    /// <summary>Bilinear ground height at world x,z. Outside the map the edge height is used.</summary>
    public float GetHeight(float x, float z)
    {
        float fx = x / CellSize + (N - 1) * 0.5f;
        float fz = z / CellSize + (N - 1) * 0.5f;
        int x0 = (int)MathF.Floor(fx), z0 = (int)MathF.Floor(fz);
        float tx = fx - x0, tz = fz - z0;
        float h00 = H(x0, z0), h10 = H(x0 + 1, z0), h01 = H(x0, z0 + 1), h11 = H(x0 + 1, z0 + 1);
        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
    }

    public Vector3 GetNormal(float x, float z)
    {
        float e = CellSize;
        float hl = GetHeight(x - e, z), hr = GetHeight(x + e, z), hd = GetHeight(x, z - e), hu = GetHeight(x, z + e);
        return new Vector3(hl - hr, 2f * e, hd - hu).Normalized();
    }

    public Surface SurfaceAt(float x, float z)
    {
        var (cx, cz) = WorldToCell(new Vector2(x, z));
        if (cx < 0 || cz < 0 || cx >= N || cz >= N) return Surface.Veld;
        return _surface[cz * N + cx];
    }

    public float Slope(float x, float z) => 1f - GetNormal(x, z).Y;

    /// <summary>The ground tint at a point (what the terrain shader multiplies its detail with). Used to colour grass.</summary>
    public Color TintAt(float x, float z)
    {
        var (cx, cz) = WorldToCell(new Vector2(x, z));
        if (_colors.Length == 0 || cx < 0 || cz < 0 || cx >= N || cz >= N) return new Color(0.6f, 0.52f, 0.27f);
        return _colors[cz * N + cx];
    }

    public bool InBounds(float x, float z, float margin = 20f) =>
        MathF.Abs(x) < Size * 0.5f - margin && MathF.Abs(z) < Size * 0.5f - margin;

    private void BuildMesh()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        _colors = new Color[N * N];
        var veldLow = _subtropical ? new Color(0.34f, 0.5f, 0.2f) : new Color(0.6f, 0.52f, 0.27f);
        var veldHigh = _subtropical ? new Color(0.42f, 0.56f, 0.24f) : new Color(0.7f, 0.6f, 0.33f);
        var rock = _subtropical ? new Color(0.48f, 0.42f, 0.34f) : new Color(0.5f, 0.4f, 0.3f);
        var road = new Color(0.56f, 0.44f, 0.3f);
        var rail = new Color(0.42f, 0.38f, 0.34f);
        var town = new Color(0.64f, 0.54f, 0.4f);
        var grass = new Color(0.36f, 0.52f, 0.22f);
        var paved = new Color(0.52f, 0.52f, 0.52f);
        var built = _subtropical ? new Color(0.5f, 0.47f, 0.42f) : town;
        var water = new Color(0.2f, 0.3f, 0.32f);
        var wood = new Color(0.33f, 0.42f, 0.2f);
        float minH = float.MaxValue, maxH = float.MinValue;
        foreach (var h in _heights) { minH = MathF.Min(minH, h); maxH = MathF.Max(maxH, h); }

        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                var p = CellToWorld(x, z);
                float h = _heights[z * N + x];
                var n = GetNormal(p.X, p.Y);
                float slope = 1f - n.Y;
                float t = Mathf.Clamp((h - minH) / Mathf.Max(1f, maxH - minH), 0f, 1f);
                var color = veldLow.Lerp(veldHigh, t);
                color = color.Lerp(rock, Mathf.Clamp((slope - 0.12f) * 4f, 0f, 1f));
                color = _surface[z * N + x] switch
                {
                    Surface.Road => road,
                    Surface.Rail => rail,
                    Surface.Town => town,
                    Surface.Grass => grass,
                    Surface.Paved => paved,
                    Surface.Built => built,
                    Surface.Water => water,
                    Surface.Wood => wood,
                    _ => color,
                };
                float v = _detail.GetNoise2D(p.X * 3f, p.Y * 3f) * 0.05f;
                color = new Color(color.R + v, color.G + v, color.B + v * 0.5f);
                _colors[z * N + x] = color;
                st.SetColor(color);
                st.SetNormal(n);
                st.SetUV(new Vector2(x / 8f, z / 8f));
                st.AddVertex(new Vector3(p.X, h, p.Y));
            }

        for (int z = 0; z < N - 1; z++)
            for (int x = 0; x < N - 1; x++)
            {
                int i = z * N + x;
                // Godot front faces are clockwise when seen from the outside (here: from above)
                st.AddIndex(i); st.AddIndex(i + 1); st.AddIndex(i + N);
                st.AddIndex(i + 1); st.AddIndex(i + N + 1); st.AddIndex(i + N);
            }

        var mesh = st.Commit();
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/terrain.gdshader") };
        material.SetShaderParameter("soil_albedo", Tex.SoilAlbedo());
        material.SetShaderParameter("soil_normal", Tex.SoilNormal());
        material.SetShaderParameter("rock_albedo", Tex.RockAlbedo());
        material.SetShaderParameter("rock_normal", Tex.RockNormal());
        material.SetShaderParameter("macro_noise", Tex.Macro());
        if (_subtropical) material.SetShaderParameter("rock_slope_start", 0.3f);
        ApplyOrtho(material, _world, _mapFolder);
        var mi = new MeshInstance3D
        {
            Name = "TerrainMesh",
            Mesh = mesh,
            MaterialOverride = material,
            GIMode = GeometryInstance3D.GIModeEnum.Static,
        };
        AddChild(mi);
    }

    /// <summary>Drape the map's aerial photo (if any) over the ground.</summary>
    private static void ApplyOrtho(ShaderMaterial material, WorldData world, string mapFolder)
    {
        if (string.IsNullOrEmpty(world.Ortho) || !world.OrthoBounds.IsSet || world.OrthoMix <= 0f) return;
        string path = world.Ortho.StartsWith("res://") ? world.Ortho : mapFolder + world.Ortho;
        var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        if (img == null || img.IsEmpty()) { GD.PushWarning("aerial photo not found or unreadable: " + path); return; }
        img.GenerateMipmaps();
        var tex = ImageTexture.CreateFromImage(img);
        var o = world.Origin; var b = world.OrthoBounds;
        float x0 = (float)((b.West - o.Lon) * o.MPerDegLon), x1 = (float)((b.East - o.Lon) * o.MPerDegLon);
        float z0 = (float)(-(b.North - o.Lat) * o.MPerDegLat), z1 = (float)(-(b.South - o.Lat) * o.MPerDegLat);
        material.SetShaderParameter("ortho", tex);
        material.SetShaderParameter("ortho_rect", new Vector4(x0, z0, x1, z1));
        material.SetShaderParameter("ortho_mix", Mathf.Clamp(world.OrthoMix, 0f, 1f));
        GD.Print($"aerial photo draped: {img.GetWidth()}x{img.GetHeight()} over x {x0:0}..{x1:0}, z {z0:0}..{z1:0}");
    }

    private void BuildCollision()
    {
        var body = new StaticBody3D { Name = "TerrainBody", CollisionLayer = Layers.Terrain, CollisionMask = 0 };
        var shape = new HeightMapShape3D { MapWidth = N, MapDepth = N, MapData = _heights };
        body.AddChild(new CollisionShape3D { Shape = shape, Scale = new Vector3(CellSize, 1f, CellSize) });
        AddChild(body);
    }
}
