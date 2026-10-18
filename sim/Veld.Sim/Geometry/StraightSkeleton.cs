using System;
using System.Collections.Generic;
using Godot;

namespace Veld.Sim.Geometry;

/// <summary>
/// The straight skeleton of a simple counter-clockwise polygon, as roof faces. Every edge moves inward at
/// unit speed and each vertex slides along the mitre of its two edges; an edge that shrinks to nothing
/// merges its two vertices, and a reflex vertex that reaches an opposite edge splits the polygon in two.
/// The path of every vertex is an arc of the skeleton; the faces are read off the arcs at the end.
/// </summary>
public static class StraightSkeleton
{
    private const float Eps = 1e-3f;
    private const float TimeEps = 5e-4f;

    private sealed class Vertex
    {
        public Vector2 Start;
        public float Height;
        public Vector2 Velocity;
        public int LeftEdge, RightEdge;
        public Vector2 At(float t) => Start + Velocity * (t - Height);
    }

    private readonly record struct Arc(Vector2 A, float HeightA, Vector2 B, float HeightB, int Face1, int Face2);

    /// <summary>Empty when the polygon has fewer than three points, no area, is clockwise, or the wavefront cannot be followed.</summary>
    public static IReadOnlyList<RoofFace> Compute(IReadOnlyList<Vector2> polygon)
    {
        if (polygon.Count < 3) return Array.Empty<RoofFace>();
        // work around the outline's own centre: a float a kilometre from the origin cannot hold a millimetre
        var origin = Polygons.Centroid(polygon);
        var local = new Vector2[polygon.Count];
        for (int i = 0; i < local.Length; i++) local[i] = polygon[i] - origin;
        var faces = ComputeLocal(local);
        var placed = new List<RoofFace>(faces.Count);
        foreach (var f in faces)
        {
            var ring = new List<RoofVertex>(f.Ring.Count);
            foreach (var v in f.Ring) ring.Add(new RoofVertex(v.Point + origin, v.Height));
            placed.Add(new RoofFace(f.Edge, ring));
        }
        return placed;
    }

    private static IReadOnlyList<RoofFace> ComputeLocal(IReadOnlyList<Vector2> polygon)
    {
        int n = polygon.Count;
        if (Polygons.SignedArea(polygon) < Eps) return Array.Empty<RoofFace>();
        var dirs = new Vector2[n];
        var inward = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            var d = polygon[(i + 1) % n] - polygon[i];
            if (d.Length() < Eps) return Array.Empty<RoofFace>();
            dirs[i] = d.Normalized();
            inward[i] = new Vector2(-dirs[i].Y, dirs[i].X);
        }
        var ring = new List<Vertex>(n);
        for (int i = 0; i < n; i++) ring.Add(Make(polygon[i], 0f, (i - 1 + n) % n, i, dirs, inward));
        var arcs = new List<Arc>();
        return Collapse(ring, polygon, dirs, inward, arcs) ? Faces(polygon, arcs) : Array.Empty<RoofFace>();
    }

    /// <summary>A wavefront vertex between two edges; parallel edges leave it standing still, which happens where a ridge forms.</summary>
    private static Vertex Make(Vector2 at, float height, int left, int right, Vector2[] dirs, Vector2[] inward)
    {
        var n1 = inward[left];
        var n2 = inward[right];
        float dot = n1.Dot(n2);
        return new Vertex
        {
            Start = at, Height = height, LeftEdge = left, RightEdge = right,
            Velocity = 1f + dot > Eps ? (n1 + n2) / (1f + dot) : Vector2.Zero,
        };
    }

    private static bool Collapse(List<Vertex> ring, IReadOnlyList<Vector2> poly, Vector2[] dirs, Vector2[] inward, List<Arc> arcs)
    {
        int guard = ring.Count * 8 + 8;
        // the wavefront only moves forward: an event earlier than the ring's present is stale, however the ring has changed since
        float now = 0f;
        foreach (var v in ring) now = Mathf.Max(now, v.Height);
        while (ring.Count > 2 && guard-- > 0)
        {
            float bestT = float.MaxValue;
            int bestI = -1, splitEdge = -1;
            Vector2 bestP = default;
            for (int i = 0; i < ring.Count; i++)
                if (EdgeEvent(ring[i], ring[(i + 1) % ring.Count], now, out float t, out var p) && t >= now - TimeEps && t < bestT) { bestT = t; bestI = i; bestP = p; splitEdge = -1; }
            for (int i = 0; i < ring.Count; i++)
            {
                for (int k = 0; k < ring.Count; k++)
                {
                    if (k == i || (k + 1) % ring.Count == i) continue;
                    var ea = ring[k];
                    var eb = ring[(k + 1) % ring.Count];
                    if (SplitEvent(ring[i], ea, eb, ea.RightEdge, poly, dirs, inward, out float t, out var p) && t >= now - TimeEps && t < bestT - TimeEps)
                    { bestT = t; bestI = i; bestP = p; splitEdge = k; }
                }
            }
            bestT = Mathf.Max(bestT, now);
            now = bestT;
            if (bestI < 0) return false;

            if (splitEdge < 0)
            {
                int next = (bestI + 1) % ring.Count;
                var a = ring[bestI];
                var b = ring[next];
                End(a, bestP, bestT, arcs);
                End(b, bestP, bestT, arcs);
                ring[bestI] = Make(bestP, bestT, a.LeftEdge, b.RightEdge, dirs, inward);
                ring.RemoveAt(next);
                continue;
            }

            var v = ring[bestI];
            End(v, bestP, bestT, arcs);
            int e = ring[splitEdge].RightEdge;
            var one = new List<Vertex> { Make(bestP, bestT, v.LeftEdge, e, dirs, inward) };
            for (int j = (splitEdge + 1) % ring.Count; j != bestI; j = (j + 1) % ring.Count) one.Add(ring[j]);
            var two = new List<Vertex> { Make(bestP, bestT, e, v.RightEdge, dirs, inward) };
            for (int j = (bestI + 1) % ring.Count; j != (splitEdge + 1) % ring.Count; j = (j + 1) % ring.Count) two.Add(ring[j]);
            return Collapse(one, poly, dirs, inward, arcs) && Collapse(two, poly, dirs, inward, arcs);
        }
        if (ring.Count == 2)
        {
            // two vertices left: a sliver that closes along the ridge when the later of them arrives on it
            var a = ring[0];
            var b = ring[1];
            float t = Mathf.Max(a.Height, b.Height);
            var pa = a.At(t);
            var pb = b.At(t);
            End(a, pa, t, arcs);
            End(b, pb, t, arcs);
            AddArc(arcs, pa, t, pb, t, a.LeftEdge, a.RightEdge);
            return true;
        }
        return ring.Count < 2;
    }

    /// <summary>The time and place where two neighbouring vertices meet, if their paths do. Two already on the same point meet now.</summary>
    private static bool EdgeEvent(Vertex a, Vertex b, float now, out float t, out Vector2 p)
    {
        if (a.At(now).DistanceTo(b.At(now)) < Eps)
        {
            t = now;
            p = a.At(now);
            return true;
        }
        var c = a.Start - a.Velocity * a.Height - b.Start + b.Velocity * b.Height;
        var d = a.Velocity - b.Velocity;
        float start = Mathf.Max(a.Height, b.Height);
        p = default;
        if (d.LengthSquared() < Eps * Eps)
        {
            t = start;
            if (c.Length() > Eps) return false;
            p = a.At(t);
            return true;
        }
        t = -c.Dot(d) / d.LengthSquared();
        if (t < start - TimeEps || (c + d * t).Length() > Eps * 10f) return false;
        t = Mathf.Max(t, start);
        p = a.At(t);
        return true;
    }

    /// <summary>The time and place where a reflex vertex reaches the moving edge between ea and eb, if it does so within that edge.</summary>
    private static bool SplitEvent(Vertex v, Vertex ea, Vertex eb, int e, IReadOnlyList<Vector2> poly, Vector2[] dirs, Vector2[] inward, out float t, out Vector2 p)
    {
        var n = inward[e];
        float vn = v.Velocity.Dot(n);
        t = 0f;
        p = default;
        if (1f - vn < Eps) return false;
        t = ((v.Start - poly[e]).Dot(n) - v.Height * vn) / (1f - vn);
        if (t < v.Height - TimeEps) return false;
        p = v.At(t);
        var d = dirs[e];
        return (p - ea.At(t)).Dot(d) >= -Eps && (eb.At(t) - p).Dot(d) >= -Eps;
    }

    private static void End(Vertex v, Vector2 p, float t, List<Arc> arcs) => AddArc(arcs, v.Start, v.Height, p, t, v.LeftEdge, v.RightEdge);

    private static void AddArc(List<Arc> arcs, Vector2 a, float ha, Vector2 b, float hb, int f1, int f2)
    {
        if (a.DistanceTo(b) < Eps && Mathf.Abs(ha - hb) < TimeEps) return;
        arcs.Add(new Arc(a, ha, b, hb, f1, f2));
    }

    /// <summary>Each face: its edge, then the chain of arcs that border that face, walked from the edge's end back round to its start.</summary>
    private static List<RoofFace> Faces(IReadOnlyList<Vector2> poly, List<Arc> arcs)
    {
        int n = poly.Count;
        var faces = new List<RoofFace>(n);
        for (int e = 0; e < n; e++)
        {
            var a = poly[e];
            var b = poly[(e + 1) % n];
            var ring = new List<RoofVertex> { new(a, 0f), new(b, 0f) };
            var used = new bool[arcs.Count];
            var at = b;
            for (int step = 0; step < arcs.Count; step++)
            {
                int found = -1;
                Vector2 next = default;
                float h = 0f;
                for (int i = 0; i < arcs.Count && found < 0; i++)
                {
                    if (used[i] || (arcs[i].Face1 != e && arcs[i].Face2 != e)) continue;
                    if (arcs[i].A.DistanceTo(at) < Eps) { found = i; next = arcs[i].B; h = arcs[i].HeightB; }
                    else if (arcs[i].B.DistanceTo(at) < Eps) { found = i; next = arcs[i].A; h = arcs[i].HeightA; }
                }
                if (found < 0) break;
                used[found] = true;
                if (next.DistanceTo(a) < Eps) break;
                ring.Add(new RoofVertex(next, h));
                at = next;
            }
            faces.Add(new RoofFace(e, ring));
        }
        return faces;
    }
}
