using System.Collections.Generic;
using Godot;

namespace Veld.Sim.Geometry;

/// <summary>
/// An outline made fit to roof: short edges and straight-through vertices dropped, the eave overhang
/// added, and the gate that says which outlines get a hipped roof. Rings are counter-clockwise.
/// </summary>
public static class RoofOutline
{
    /// <summary>Merges each edge shorter than minEdge into its midpoint and drops vertices that turn less than straightDegrees, until nothing changes.</summary>
    public static List<Vector2> Simplify(IReadOnlyList<Vector2> p, float minEdge = 0.5f, float straightDegrees = 3f)
    {
        var r = new List<Vector2>(p);
        bool changed = true;
        while (changed && r.Count > 3)
        {
            changed = false;
            for (int i = 0; i < r.Count && r.Count > 3; i++)
            {
                int j = (i + 1) % r.Count;
                if (r[i].DistanceTo(r[j]) < minEdge)
                {
                    r[i] = (r[i] + r[j]) * 0.5f;
                    r.RemoveAt(j);
                    changed = true;
                    break;
                }
            }
            for (int i = 0; i < r.Count && r.Count > 3; i++)
            {
                if (Mathf.Abs(TurnDegrees(r, i)) < straightDegrees)
                {
                    r.RemoveAt(i);
                    changed = true;
                    break;
                }
            }
        }
        return r;
    }

    /// <summary>Every edge moved outward by distance and the neighbouring edge lines re-intersected; a spike too sharp to mitre moves along one normal.</summary>
    public static List<Vector2> Offset(IReadOnlyList<Vector2> p, float distance)
    {
        int n = p.Count;
        var r = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            var a = p[(i - 1 + n) % n]; var b = p[i]; var c = p[(i + 1) % n];
            var n1 = Outward(b - a);
            var n2 = Outward(c - b);
            float dot = n1.Dot(n2);
            r.Add(1f + dot < 1e-4f ? b + n1 * distance : b + (n1 + n2) * (distance / (1f + dot)));
        }
        return r;
    }

    /// <summary>Four to twelve vertices, every edge at least 1.2 m, and either convex or every corner within 12 degrees of square.</summary>
    public static bool IsRoofable(IReadOnlyList<Vector2> p)
    {
        if (p.Count < 4 || p.Count > 12) return false;
        bool convex = true, rectilinear = true;
        for (int i = 0; i < p.Count; i++)
        {
            if (p[i].DistanceTo(p[(i + 1) % p.Count]) < 1.2f) return false;
            float turn = TurnDegrees(p, i);
            if (turn < -0.01f) convex = false;
            if (Mathf.Abs(Mathf.Abs(turn) - 90f) > 12f) rectilinear = false;
        }
        return convex || rectilinear;
    }

    /// <summary>The signed turn at vertex i in degrees: positive for a left turn, which is a convex corner on a counter-clockwise ring.</summary>
    private static float TurnDegrees(IReadOnlyList<Vector2> p, int i)
    {
        int n = p.Count;
        var d1 = (p[i] - p[(i - 1 + n) % n]).Normalized();
        var d2 = (p[(i + 1) % n] - p[i]).Normalized();
        return Mathf.RadToDeg(Mathf.Atan2(d1.Cross(d2), d1.Dot(d2)));
    }

    /// <summary>The outward normal of an edge on a counter-clockwise ring: its right-hand side.</summary>
    private static Vector2 Outward(Vector2 edge)
    {
        var d = edge.Normalized();
        return new Vector2(d.Y, -d.X);
    }
}
