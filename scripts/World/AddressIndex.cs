using System;
using System.Collections.Generic;
using Godot;
using Veld.Sim;

namespace Veld.World;

/// <summary>
/// Every addressed building and every suburb boundary, prepared once at startup so the HUD, the map
/// and the smoke test can ask "which house is this" and "where is 88 Road 723" cheaply.
/// </summary>
public static class AddressIndex
{
    public sealed class Entry
    {
        public FootprintData Building = null!;
        public Vector2 Centre;
        public List<Vector2> Polygon = new();
    }

    private sealed class Suburb
    {
        public string Name = "";
        public List<Vector2> Polygon = new();
    }

    public static List<Entry> Entries { get; } = new();
    private static readonly List<Suburb> SuburbList = new();

    public static void Build(WorldData world)
    {
        Entries.Clear();
        SuburbList.Clear();
        foreach (var b in world.Buildings)
        {
            if (!b.HasAddress || b.Polygon.Count < 3) continue;
            var e = new Entry { Building = b };
            foreach (var p in b.Polygon) e.Polygon.Add(DataLoader.XZ(p));
            e.Centre = FootprintBuilder.Centroid(e.Polygon);
            Entries.Add(e);
        }
        foreach (var s in world.Suburbs)
        {
            if (s.Polygon.Count < 3) continue;
            var sub = new Suburb { Name = s.Name };
            foreach (var p in s.Polygon) sub.Polygon.Add(DataLoader.XZ(p));
            SuburbList.Add(sub);
        }
    }

    /// <summary>The house the point stands in or beside (within <paramref name="margin"/> metres of its walls), else null.</summary>
    public static Entry? At(Vector2 xz, float margin = 4f)
    {
        Entry? best = null;
        float bestD = margin;
        foreach (var e in Entries)
        {
            if (xz.DistanceSquaredTo(e.Centre) > 60f * 60f) continue;
            if (Terrain.PointInPolygon(xz, e.Polygon)) return e;
            float d = DistanceToPolygon(xz, e.Polygon);
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    /// <summary>Suburb name at a point from the municipal boundaries, or empty.</summary>
    public static string SuburbAt(Vector2 xz)
    {
        foreach (var s in SuburbList)
            if (Terrain.PointInPolygon(xz, s.Polygon)) return s.Name;
        return "";
    }

    /// <summary>
    /// "88 723", "88 road 723" or "19 rainbow circle": the first token that is a number is the house
    /// number, the rest must all appear in the street name. Returns null when nothing matches.
    /// </summary>
    public static Entry? Find(string query)
    {
        var tokens = query.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return null;
        string? number = null;
        var street = new List<string>();
        foreach (var t in tokens)
        {
            if (number == null && char.IsDigit(t[0])) number = t;
            else if (t != "road" && t != "rd") street.Add(t);
        }
        if (number == null) return null;
        Entry? loose = null;
        foreach (var e in Entries)
        {
            if (!string.Equals(e.Building.Number, number, StringComparison.OrdinalIgnoreCase)) continue;
            string s = e.Building.Street.ToLowerInvariant();
            bool all = true;
            foreach (var t in street) if (!s.Contains(t)) { all = false; break; }
            if (!all) continue;
            if (street.Count > 0 && s.Replace("road ", "") == string.Join(" ", street)) return e;
            loose ??= e;
        }
        return loose;
    }

    public static float DistanceToPolygon(Vector2 p, List<Vector2> poly)
    {
        float best = float.MaxValue;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            var ab = b - a;
            float l2 = ab.LengthSquared();
            float t = l2 < 0.0001f ? 0f : Mathf.Clamp((p - a).Dot(ab) / l2, 0f, 1f);
            best = Mathf.Min(best, p.DistanceTo(a + ab * t));
        }
        return best;
    }
}
