using System.Collections.Generic;
using Godot;
using Veld.Core;

namespace Veld.World;

/// <summary>A text placed on the map: a road name, a place, or a town.</summary>
public sealed class MapLabel
{
    public string Text = "";
    public Vector2 World;
    /// <summary>0 road, 1 place of interest, 2 town/suburb</summary>
    public int Kind;
}

/// <summary>
/// Draws the whole world top-down into one texture at startup (ground tint with hill shading, roads,
/// railway, water, buildings) and collects the labels. The minimap and the full map both read it.
/// </summary>
public static class MapImage
{
    public const int Size = 1024;
    public static ImageTexture? Texture { get; private set; }
    public static List<MapLabel> Labels { get; } = new();
    public static float MetresPerPixel { get; private set; } = 1f;
    public static float MapSize { get; private set; } = 1f;

    private static byte[] _buf = System.Array.Empty<byte>();

    public static Vector2 ToPx(Vector2 world) => new((world.X + MapSize / 2f) / MetresPerPixel, (world.Y + MapSize / 2f) / MetresPerPixel);
    public static Vector2 ToWorld(Vector2 px) => new(px.X * MetresPerPixel - MapSize / 2f, px.Y * MetresPerPixel - MapSize / 2f);

    public static void Build(WorldBuilder world, GameData data)
    {
        var terrain = world.Terrain;
        MapSize = terrain.Size;
        MetresPerPixel = MapSize / Size;
        _buf = new byte[Size * Size * 4];
        Labels.Clear();
        var light = new Vector3(-0.5f, 0.8f, -0.45f).Normalized();

        // ground: tint with hill shading
        for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                float x = (px + 0.5f) * MetresPerPixel - MapSize / 2f;
                float z = (py + 0.5f) * MetresPerPixel - MapSize / 2f;
                var c = terrain.TintAt(x, z);
                var n = terrain.GetNormal(x, z);
                float shade = 0.6f + 0.5f * Mathf.Clamp(n.Dot(light), 0f, 1f);
                Put(px, py, new Color(c.R * shade, c.G * shade, c.B * shade));
            }

        var w = data.World;
        // water
        foreach (var l in w.Lines)
            if (l.Kind is "stream" or "river" or "ditch")
                Polyline(l.Points, l.Kind == "river" ? 2.2f : 1.2f, new Color(0.3f, 0.5f, 0.7f));
        // railway
        if (w.Railway.Points.Count > 1)
        {
            var pts = new List<float[]>(w.Railway.Points);
            if (w.Railway.Loop) pts.Add(w.Railway.Points[0]);
            Polyline(pts, 2.4f, new Color(0.25f, 0.22f, 0.2f));
            Polyline(pts, 0.8f, new Color(0.75f, 0.7f, 0.6f));
        }
        // roads: minor first so main roads draw on top
        foreach (var r in w.Roads) if (r.Kind is "footway" or "path" or "steps" or "track" or "cycleway") Polyline(r.Points, 0.9f, new Color(0.72f, 0.66f, 0.55f));
        foreach (var r in w.Roads) if (r.Kind == "service") Polyline(r.Points, 1.2f, new Color(0.5f, 0.5f, 0.5f));
        foreach (var r in w.Roads) if (string.IsNullOrEmpty(r.Kind)) Polyline(r.Points, Mathf.Max(1.5f, r.Width / MetresPerPixel), new Color(0.55f, 0.42f, 0.28f));
        foreach (var r in w.Roads) if (r.Kind is "residential" or "unclassified" or "living_street") { Polyline(r.Points, 2.6f, new Color(0.15f, 0.15f, 0.16f)); Polyline(r.Points, 1.8f, new Color(0.42f, 0.42f, 0.44f)); }
        foreach (var r in w.Roads) if (r.Kind is "tertiary" or "tertiary_link" or "secondary" or "primary" or "trunk") { Polyline(r.Points, 3.4f, new Color(0.15f, 0.15f, 0.16f)); Polyline(r.Points, 2.4f, new Color(0.85f, 0.75f, 0.45f)); }
        // buildings
        var fill = new Color(0.82f, 0.78f, 0.7f);
        var edge = new Color(0.45f, 0.4f, 0.35f);
        foreach (var b in w.Buildings)
        {
            var poly = new List<Vector2>();
            foreach (var p in b.Polygon) poly.Add(ToPx(DataLoader.XZ(p)));
            FillPolygon(poly, fill);
            for (int i = 0; i < poly.Count; i++) Line(poly[i], poly[(i + 1) % poly.Count], 0.6f, edge);
        }
        // authored towns: each building as a rotated rectangle
        foreach (var t in data.Towns)
        {
            var centre = DataLoader.XZ(t.Position);
            float ta = Mathf.DegToRad(t.Rotation);
            foreach (var b in t.Buildings)
            {
                if (b.Type is "fence" or "hitchrail" or "well" or "barrels" or "crate" or "trough") continue;
                float ba = ta + Mathf.DegToRad(b.Rotation);
                var off = new Vector2(b.Offset[0], b.Offset[1]).Rotated(ta);
                var c = centre + off;
                var hx = b.Size[0] / 2f; var hz = b.Size[2] / 2f;
                var poly = new List<Vector2>();
                foreach (var corner in new[] { new Vector2(-hx, -hz), new Vector2(hx, -hz), new Vector2(hx, hz), new Vector2(-hx, hz) })
                    poly.Add(ToPx(c + corner.Rotated(ba)));
                FillPolygon(poly, b.Type == "barracks" ? new Color(0.75f, 0.7f, 0.55f) : fill);
                for (int i = 0; i < 4; i++) Line(poly[i], poly[(i + 1) % 4], 0.6f, edge);
            }
            Labels.Add(new MapLabel { Text = t.Name, World = centre, Kind = 2 });
        }

        // labels: road names (longest run per name), places, suburbs
        var byName = new Dictionary<string, (float len, Vector2 mid)>();
        foreach (var r in w.Roads)
        {
            if (string.IsNullOrEmpty(r.Name) || r.Points.Count < 2) continue;
            float len = 0f;
            for (int i = 1; i < r.Points.Count; i++) len += DataLoader.XZ(r.Points[i - 1]).DistanceTo(DataLoader.XZ(r.Points[i]));
            var mid = DataLoader.XZ(r.Points[r.Points.Count / 2]);
            if (!byName.TryGetValue(r.Name, out var best) || len > best.len) byName[r.Name] = (len, mid);
        }
        foreach (var (name, v) in byName) if (v.len > 60f) Labels.Add(new MapLabel { Text = name, World = v.mid, Kind = 0 });
        foreach (var a in w.Areas)
            if (!string.IsNullOrEmpty(a.Name) && a.Polygon.Count > 2)
            {
                var c = Vector2.Zero;
                foreach (var p in a.Polygon) c += DataLoader.XZ(p);
                Labels.Add(new MapLabel { Text = a.Name, World = c / a.Polygon.Count, Kind = 1 });
            }
        foreach (var b in w.Buildings)
            if (!string.IsNullOrEmpty(b.Name) && b.Kind is "school" or "stadium" or "commercial" or "retail" && b.Polygon.Count > 2)
            {
                var c = Vector2.Zero;
                foreach (var p in b.Polygon) c += DataLoader.XZ(p);
                Labels.Add(new MapLabel { Text = b.Name, World = c / b.Polygon.Count, Kind = 1 });
            }
        foreach (var p in w.Pois)
        {
            if (p.Kind.StartsWith("place:")) Labels.Add(new MapLabel { Text = p.Name, World = DataLoader.XZ(p.Position), Kind = 2 });
            else if (p.Kind is "police" or "fire_station" or "post_office" or "school" or "fuel" && !string.IsNullOrEmpty(p.Name)) Labels.Add(new MapLabel { Text = p.Name, World = DataLoader.XZ(p.Position), Kind = 1 });
        }

        var img = Image.CreateFromData(Size, Size, false, Image.Format.Rgba8, _buf);
        img.GenerateMipmaps();
        Texture = ImageTexture.CreateFromImage(img);
        _buf = System.Array.Empty<byte>();
        GD.Print($"map image: {Size}x{Size} px, {MetresPerPixel:0.00} m/px, {Labels.Count} labels");
    }

    /// <summary>"721 Road, Montford" or "Welgevonden Road" or the nearest named place: what the HUD shows under the minimap.</summary>
    public static string LocationName(GameData data, Vector2 xz)
    {
        string road = "";
        float bestRoad = 40f;
        foreach (var r in data.World.Roads)
        {
            if (string.IsNullOrEmpty(r.Name)) continue;
            for (int i = 1; i < r.Points.Count; i++)
            {
                var a = DataLoader.XZ(r.Points[i - 1]); var b = DataLoader.XZ(r.Points[i]);
                var ab = b - a;
                float l2 = ab.LengthSquared();
                float t = l2 < 0.001f ? 0f : Mathf.Clamp((xz - a).Dot(ab) / l2, 0f, 1f);
                float d = xz.DistanceTo(a + ab * t);
                if (d < bestRoad) { bestRoad = d; road = r.Name; }
            }
        }
        string place = "";
        float bestPlace = 900f;
        foreach (var p in data.World.Pois)
            if (p.Kind.StartsWith("place:"))
            {
                float d = xz.DistanceTo(DataLoader.XZ(p.Position));
                if (d < bestPlace) { bestPlace = d; place = p.Name; }
            }
        foreach (var t in data.Towns)
        {
            float d = xz.DistanceTo(DataLoader.XZ(t.Position));
            if (d < t.ClearRadius + 60f && d < bestPlace) { bestPlace = d; place = t.Name; }
        }
        if (System.Text.RegularExpressions.Regex.IsMatch(road, "^7[0-9][0-9] Road$")) place = "Montford";
        if (road != "" && place != "") return road + ", " + place;
        if (road != "") return road;
        if (place != "") return place;
        return data.World.IsModern ? data.World.Name : "The veld";
    }

    // ------------------------------------------------------------------ raster helpers (texture pixel space)

    private static void Put(int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size) return;
        int i = (y * Size + x) * 4;
        _buf[i] = (byte)Mathf.Clamp(c.R * 255f, 0, 255); _buf[i + 1] = (byte)Mathf.Clamp(c.G * 255f, 0, 255); _buf[i + 2] = (byte)Mathf.Clamp(c.B * 255f, 0, 255); _buf[i + 3] = 255;
    }

    private static void Disc(Vector2 c, float r, Color col)
    {
        int x0 = Mathf.FloorToInt(c.X - r), x1 = Mathf.CeilToInt(c.X + r), y0 = Mathf.FloorToInt(c.Y - r), y1 = Mathf.CeilToInt(c.Y + r);
        float r2 = r * r;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if ((x + 0.5f - c.X) * (x + 0.5f - c.X) + (y + 0.5f - c.Y) * (y + 0.5f - c.Y) <= r2) Put(x, y, col);
    }

    private static void Line(Vector2 a, Vector2 b, float width, Color col)
    {
        float len = a.DistanceTo(b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.5f));
        for (int i = 0; i <= steps; i++) Disc(a.Lerp(b, i / (float)steps), Mathf.Max(0.5f, width / 2f), col);
    }

    private static void Polyline(List<float[]> pts, float widthPx, Color col)
    {
        for (int i = 1; i < pts.Count; i++) Line(ToPx(DataLoader.XZ(pts[i - 1])), ToPx(DataLoader.XZ(pts[i])), widthPx, col);
    }

    private static void FillPolygon(List<Vector2> poly, Color col)
    {
        if (poly.Count < 3) return;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in poly) { minX = Mathf.Min(minX, p.X); maxX = Mathf.Max(maxX, p.X); minY = Mathf.Min(minY, p.Y); maxY = Mathf.Max(maxY, p.Y); }
        for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
            for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
                if (Terrain.PointInPolygon(new Vector2(x + 0.5f, y + 0.5f), poly)) Put(x, y, col);
        // tiny footprints still get a pixel
        if (maxX - minX < 1f && maxY - minY < 1f) Put(Mathf.RoundToInt(minX), Mathf.RoundToInt(minY), col);
    }
}
