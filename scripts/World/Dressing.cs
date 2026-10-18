using System.Collections.Generic;
using Godot;
using Veld.Sim;
using Veld.Sim.Geometry;

namespace Veld.World;

/// <summary>
/// Everything on a real-world map that is not terrain, roads or buildings: sports fields (goal posts,
/// cricket strips), fences and walls, streams and rivers, power lines, and signs for shops, schools and
/// other places of interest. Shop signs use generic trade names, not the real business names.
/// </summary>
public partial class Dressing : Node3D
{
    private Terrain _terrain = null!;
    private readonly List<Transform3D> _posts = new();
    private readonly List<Transform3D> _wires = new();

    public void Build(WorldData world, Terrain terrain)
    {
        _terrain = terrain;
        int goals = 0, fences = 0, walls = 0, water = 0, signs = 0;

        foreach (var a in world.Areas)
        {
            var poly = new List<Vector2>();
            foreach (var p in a.Polygon) poly.Add(DataLoader.XZ(p));
            if (poly.Count < 3) continue;
            var c = Polygons.Centroid(poly);
            if (!terrain.InBounds(c.X, c.Y, 5f)) continue;
            if (a.Kind == "pitch")
            {
                var rect = Polygons.OrientedRect(poly);
                if (rect.HasValue && rect.Value.HalfLong > 20f && (a.Sport is "" or "soccer" or "football" or "multi" or "rugby")) { Goals(rect.Value); goals++; }
                else if (rect.HasValue && a.Sport == "cricket") CricketStrip(rect.Value);
                else if (rect.HasValue && a.Sport == "basketball") Hoops(rect.Value);
            }
            if (!string.IsNullOrEmpty(a.Name) && a.Kind is "pitch" or "park" or "stadium" or "school" or "education" or "sports_centre" or "track" && signs < 60)
            {
                Label(a.Name, new Vector3(c.X, terrain.GetHeight(c.X, c.Y) + 9f, c.Y), 0.022f);
                signs++;
            }
        }

        foreach (var l in world.Lines)
        {
            var pts = new List<Vector2>();
            foreach (var p in l.Points) pts.Add(DataLoader.XZ(p));
            if (pts.Count < 2) continue;
            switch (l.Kind)
            {
                case "fence": Fence(pts); fences++; break;
                case "wall": Wall(pts); walls++; break;
                case "stream": case "ditch": Water(pts, 1.6f); water++; break;
                case "river": Water(pts, 4.5f); water++; break;
                case "power_line": PowerLine(pts); break;
            }
        }

        foreach (var poi in world.Pois)
        {
            var p = DataLoader.XZ(poi.Position);
            if (!terrain.InBounds(p.X, p.Y, 5f)) continue;
            string text = PoiLabel(poi);
            if (text == "") continue;
            Label(text, new Vector3(p.X, terrain.GetHeight(p.X, p.Y) + 4.5f, p.Y), 0.014f);
            signs++;
        }
        Batch("FencePosts", new BoxMesh { Size = new Vector3(0.08f, 1.8f, 0.08f), Material = Props.Material(new Color(0.35f, 0.35f, 0.37f), 0.6f, 0.5f) }, _posts);
        Batch("FenceWires", new BoxMesh { Size = Vector3.One, Material = Props.Material(new Color(0.55f, 0.55f, 0.58f), 0.5f, 0.6f) }, _wires);
        GD.Print($"dressing: {goals} pitches with goals, {fences} fences ({_posts.Count} posts), {walls} walls, {water} watercourses, {signs} signs");
    }

    private void Batch(string name, Mesh mesh, List<Transform3D> transforms)
    {
        if (transforms.Count == 0) return;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh };
        mm.InstanceCount = transforms.Count;
        for (int i = 0; i < transforms.Count; i++) mm.SetInstanceTransform(i, transforms[i]);
        AddChild(new MultiMeshInstance3D { Name = name, Multimesh = mm, GIMode = GeometryInstance3D.GIModeEnum.Disabled, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private void Label(string text, Vector3 pos, float pixelSize)
    {
        var l = Props.Sign(text, pos, pixelSize, new Color(1f, 0.96f, 0.85f), billboard: true);
        l.FontSize = 44;
        l.OutlineSize = 8;
        l.VisibilityRangeEnd = 220f;
        l.VisibilityRangeEndMargin = 30f;
        l.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
        AddChild(l);
    }

    /// <summary>Public institutions keep their names; private shops get a generic trade sign.</summary>
    private static string PoiLabel(PoiData poi)
    {
        if (poi.Kind != "fuel" && MapImage.NamedPlaceKinds.Contains(poi.Kind)) return poi.Name;
        return poi.Kind switch
        {
            "doctors" => "DOCTOR",
            "shop:convenience" => "SUPERETTE",
            "shop:supermarket" => "SUPERMARKET",
            "shop:hairdresser" => "HAIR SALON",
            "shop:beauty" => "BEAUTY SALON",
            "shop:clothes" => "CLOTHING",
            "shop:car_parts" => "MOTOR SPARES",
            "shop:car" => "CAR SALES",
            "shop:electronics" => "ELECTRONICS",
            "shop:bakery" => "BAKERY",
            "shop:butcher" => "BUTCHERY",
            "fuel" => "FILLING STATION",
            "pharmacy" => "PHARMACY",
            "veterinary" => "ANIMAL CLINIC",
            "restaurant" or "fast_food" or "cafe" => "TAKEAWAYS",
            "bank" or "atm" => "BANK",
            _ => poi.Kind.StartsWith("shop:") ? "SHOP" : "",
        };
    }

    // ------------------------------------------------------------------ sports fields

    private void Goals(OrientedRect r)
    {
        var white = Props.Material(new Color(0.95f, 0.95f, 0.95f), 0.6f);
        for (int s = -1; s <= 1; s += 2)
        {
            var line = r.Center + r.Axis * (s * (r.HalfLong - 3f));
            var v = new Vector2(-r.Axis.Y, r.Axis.X);
            var l = line + v * 3.66f; var rr = line - v * 3.66f;
            float y = _terrain.GetHeight(line.X, line.Y);
            var a = new Vector3(l.X, y, l.Y); var b = new Vector3(rr.X, y, rr.Y);
            TownBuilder.Beam(this, a, a + new Vector3(0, 2.44f, 0), 0.1f, white);
            TownBuilder.Beam(this, b, b + new Vector3(0, 2.44f, 0), 0.1f, white);
            TownBuilder.Beam(this, a + new Vector3(0, 2.44f, 0), b + new Vector3(0, 2.44f, 0), 0.1f, white);
        }
    }

    private void CricketStrip(OrientedRect r)
    {
        var c = r.Center;
        float y = _terrain.GetHeight(c.X, c.Y);
        var strip = Props.Box(new Vector3(3.05f, 0.03f, 22f), Props.Material(new Color(0.78f, 0.72f, 0.55f), 0.95f), new Vector3(c.X, y + 0.03f, c.Y));
        strip.Rotation = new Vector3(0, Mathf.Atan2(-r.Axis.X, -r.Axis.Y), 0);
        AddChild(strip);
        var white = Props.Material(new Color(0.95f, 0.95f, 0.95f), 0.6f);
        for (int s = -1; s <= 1; s += 2)
        {
            var w = c + r.Axis * (s * 10f);
            for (int k = -1; k <= 1; k++)
            {
                var v = new Vector2(-r.Axis.Y, r.Axis.X) * (k * 0.11f);
                var p = new Vector3(w.X + v.X, y, w.Y + v.Y);
                TownBuilder.Beam(this, p, p + new Vector3(0, 0.71f, 0), 0.035f, white);
            }
        }
    }

    private void Hoops(OrientedRect r)
    {
        var iron = Props.Material(new Color(0.3f, 0.3f, 0.32f), 0.5f, 0.6f);
        for (int s = -1; s <= 1; s += 2)
        {
            var p2 = r.Center + r.Axis * (s * (r.HalfLong - 1.2f));
            float y = _terrain.GetHeight(p2.X, p2.Y);
            var p = new Vector3(p2.X, y, p2.Y);
            TownBuilder.Beam(this, p, p + new Vector3(0, 3.6f, 0), 0.1f, iron);
            var board = Props.Box(new Vector3(1.8f, 1.05f, 0.05f), Props.Material(Colors.White), p + new Vector3(0, 3.3f, 0));
            board.Rotation = new Vector3(0, Mathf.Atan2(-r.Axis.X, -r.Axis.Y), 0);
            AddChild(board);
        }
    }

    // ------------------------------------------------------------------ lines

    private void Fence(List<Vector2> pts)
    {
        Vector3? prev = null;
        float carry = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            float len = a.DistanceTo(b);
            if (len < 0.1f) continue;
            float d = carry;
            while (d <= len)
            {
                var p2 = a.Lerp(b, d / len);
                var p = new Vector3(p2.X, _terrain.GetHeight(p2.X, p2.Y), p2.Y);
                _posts.Add(new Transform3D(Basis.Identity, p + new Vector3(0, 0.9f, 0)));
                if (prev.HasValue && prev.Value.DistanceTo(p) < 8f)
                    for (int k = 0; k < 3; k++)
                    {
                        var wa = prev.Value + new Vector3(0, 0.5f + k * 0.6f, 0);
                        var wb = p + new Vector3(0, 0.5f + k * 0.6f, 0);
                        var wd = wb - wa;
                        float wlen = wd.Length();
                        if (wlen < 0.01f) continue;
                        _wires.Add(new Transform3D(Basis.LookingAt(wd / wlen, Vector3.Up) * Basis.FromScale(new Vector3(0.02f, 0.02f, wlen)), (wa + wb) * 0.5f));
                    }
                prev = p;
                d += 3f;
            }
            carry = d - len;
        }
    }

    private void Wall(List<Vector2> pts)
    {
        var brick = Props.Brick(new Color(0.8f, 0.72f, 0.65f));
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            float len = a.DistanceTo(b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 6f));
            for (int k = 0; k < n; k++)
            {
                var p = a.Lerp(b, k / (float)n); var q = a.Lerp(b, (k + 1) / (float)n);
                var m = (p + q) * 0.5f;
                float y = _terrain.GetHeight(m.X, m.Y);
                var mi = Props.Box(new Vector3(0.24f, 2.2f, p.DistanceTo(q) + 0.05f), brick);
                var dir = new Vector3(q.X - p.X, 0, q.Y - p.Y).Normalized();
                mi.Transform = new Transform3D(Basis.LookingAt(dir, Vector3.Up), new Vector3(m.X, y + 0.85f, m.Y));
                AddChild(mi);
            }
        }
    }

    private void Water(List<Vector2> pts, float width)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        int tris = RoadBuilder.Ribbon(st, pts, width, -0.25f, 8f, false, null, _terrain);
        if (tris == 0) return;
        st.GenerateNormals();
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, Props.WaterMaterial());
        AddChild(new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private void PowerLine(List<Vector2> pts)
    {
        var steel = Props.Material(new Color(0.45f, 0.47f, 0.5f), 0.5f, 0.7f);
        var wire = Props.Material(new Color(0.2f, 0.2f, 0.22f), 0.6f, 0.3f);
        Vector3? prevTop = null;
        foreach (var p2 in pts)
        {
            float y = _terrain.GetHeight(p2.X, p2.Y);
            var p = new Vector3(p2.X, y, p2.Y);
            AddChild(Props.Cylinder(0.25f, 18f, steel, p + new Vector3(0, 9f, 0), 0.12f, 8));
            AddChild(Props.Box(new Vector3(6f, 0.25f, 0.25f), steel, p + new Vector3(0, 17f, 0)));
            var top = p + new Vector3(0, 17f, 0);
            if (prevTop.HasValue)
                for (int k = -1; k <= 1; k += 2)
                    TownBuilder.Beam(this, prevTop.Value + new Vector3(k * 2.8f, 0, 0), top + new Vector3(k * 2.8f, 0, 0), 0.05f, wire);
            prevTop = top;
        }
    }
}
