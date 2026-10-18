using System.Collections.Generic;
using Godot;

namespace Veld.Sim.Geometry;

/// <summary>Pure polygon maths over [x, z] points, shared by the world builders.</summary>
public static class Polygons
{
    /// <summary>Positive when the ring runs counter-clockwise.</summary>
    public static float SignedArea(IReadOnlyList<Vector2> p)
    {
        float s = 0f;
        for (int i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; s += a.X * b.Y - b.X * a.Y; }
        return s * 0.5f;
    }

    /// <summary>The mean of the vertices.</summary>
    public static Vector2 Centroid(IReadOnlyList<Vector2> p)
    {
        var c = Vector2.Zero;
        foreach (var q in p) c += q;
        return c / p.Count;
    }

    /// <summary>Bounding rectangle aligned with the polygon's longest edge; null when that edge is under 0.1 m.</summary>
    public static Veld.Sim.Geometry.OrientedRect? OrientedRect(IReadOnlyList<Vector2> poly)
    {
        Vector2 best = Vector2.Zero; float bestLen = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            var e = poly[(i + 1) % poly.Count] - poly[i];
            if (e.Length() > bestLen) { bestLen = e.Length(); best = e; }
        }
        if (bestLen < 0.1f) return null;
        var u = best / bestLen; var v = new Vector2(-u.Y, u.X);
        float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
        foreach (var p in poly)
        {
            float pu = p.Dot(u), pv = p.Dot(v);
            minU = Mathf.Min(minU, pu); maxU = Mathf.Max(maxU, pu); minV = Mathf.Min(minV, pv); maxV = Mathf.Max(maxV, pv);
        }
        float hl = (maxU - minU) / 2f, hs = (maxV - minV) / 2f;
        var center = u * (minU + hl) + v * (minV + hs);
        if (hs > hl) { (hl, hs) = (hs, hl); u = v; }
        return new Veld.Sim.Geometry.OrientedRect(center, u, hl, hs);
    }
}
