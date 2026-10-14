using System;
using System.Collections.Generic;
using Godot;
using Veld.Core;
using Veld.Gameplay;

namespace Veld.World;

/// <summary>
/// Real roads from map data: tar and gravel ribbons draped over the terrain, street lights along the
/// residential streets, and parked cars. Driefontein-style roads (no "kind") are left to the terrain
/// colouring, so this only runs for maps generated from OpenStreetMap.
/// </summary>
public partial class RoadBuilder : Node3D
{
    private Terrain _terrain = null!;
    private RandomNumberGenerator _rng = null!;

    public void Build(WorldData world, Terrain terrain)
    {
        _terrain = terrain;
        _rng = new RandomNumberGenerator { Seed = (ulong)world.Seed + 11 };

        var tar = new SurfaceTool(); tar.Begin(Mesh.PrimitiveType.Triangles);
        var gravel = new SurfaceTool(); gravel.Begin(Mesh.PrimitiveType.Triangles);
        var paving = new SurfaceTool(); paving.Begin(Mesh.PrimitiveType.Triangles);
        var kerb = new SurfaceTool(); kerb.Begin(Mesh.PrimitiveType.Triangles);
        int nTar = 0, nGravel = 0, nPaving = 0, nKerb = 0;
        var lightSpots = new List<(Vector3 pos, float yaw)>();
        var carSpots = new List<(Vector3 pos, float yaw)>();

        foreach (var r in world.Roads)
        {
            if (string.IsNullOrEmpty(r.Kind)) continue;
            var pts = new List<Vector2>();
            foreach (var p in r.Points) pts.Add(DataLoader.XZ(p));
            if (pts.Count < 2) continue;
            bool foot = r.Kind is "footway" or "path" or "steps" or "cycleway" or "pedestrian";
            bool dirt = r.Kind is "track" || r.Surface is "ground" or "dirt" or "gravel" or "unpaved" or "compacted" or "sand";
            float lift = foot ? 0.05f : 0.07f;
            if (foot) { nPaving += Ribbon(paving, pts, r.Width, lift, 2f, false, null, _terrain); }
            else if (dirt) { nGravel += Ribbon(gravel, pts, r.Width, lift, 4f, false, null, _terrain); }
            else { nTar += Ribbon(tar, pts, r.Width, lift, 6f, true, null, _terrain); }

            bool street = r.Kind is "residential" or "tertiary" or "tertiary_link" or "unclassified" or "secondary" or "primary" or "living_street";
            if (street)
            {
                // a pavement and a kerb on both sides
                for (int side = -1; side <= 1; side += 2)
                {
                    nPaving += Ribbon(paving, pts, 1.7f, 0.13f, 2f, false, null, _terrain, side * (r.Width / 2 + 0.85f));
                    nKerb += Ribbon(kerb, pts, 0.22f, 0.15f, 1f, false, null, _terrain, side * (r.Width / 2 + 0.05f));
                }
            }
            if (street && world.StreetLightSpacing > 0) Along(pts, world.StreetLightSpacing, r.Width / 2 + 1.2f, lightSpots, 20f);
            if (r.Kind is "residential" or "unclassified") Along(pts, 28f, r.Width / 2 - 1.0f, carSpots, 10f);
        }

        if (nTar > 0) AddMesh("Tar", tar, Props.Asphalt(), true);
        if (nGravel > 0) AddMesh("Gravel", gravel, Props.Soil(new Color(0.78f, 0.7f, 0.58f)), false);
        if (nPaving > 0) AddMesh("Paving", paving, Props.Concrete(new Color(0.8f, 0.78f, 0.74f)), false);
        if (nKerb > 0) AddMesh("Kerbs", kerb, Props.Concrete(new Color(0.62f, 0.62f, 0.6f)), false);

        StreetLights(lightSpots);
        Cars(carSpots, world.ParkedCars);
        GD.Print($"roads: tar {nTar / 2} quads, gravel {nGravel / 2}, paving {nPaving / 2}; street lights {lightSpots.Count}; parked cars {Mathf.Min(world.ParkedCars, carSpots.Count)}");
    }

    /// <summary>All the poles, arms and lamp heads in four MultiMeshes; only the lights themselves are nodes (and they are off by day).</summary>
    private void StreetLights(List<(Vector3 pos, float yaw)> spots)
    {
        if (spots.Count == 0) return;
        var steel = Props.Material(new Color(0.42f, 0.44f, 0.46f), 0.55f, 0.6f);
        var lampMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.88f, 0.8f), Emission = new Color(1f, 0.9f, 0.7f), Roughness = 0.3f };
        DayNight.RegisterNightEmissive(lampMat, 4f);
        var parts = new (Mesh mesh, Transform3D local)[]
        {
            (new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.07f, Height = 7.5f, RadialSegments = 8, Rings = 1, Material = steel }, new Transform3D(Basis.Identity, new Vector3(0, 3.75f, 0))),
            (new BoxMesh { Size = new Vector3(0.08f, 0.08f, 1.7f), Material = steel }, new Transform3D(Basis.FromEuler(new Vector3(-0.12f, 0, 0)), new Vector3(0, 7.45f, -0.8f))),
            (new BoxMesh { Size = new Vector3(0.28f, 0.14f, 0.6f), Material = steel }, new Transform3D(Basis.Identity, new Vector3(0, 7.3f, -1.6f))),
            (new BoxMesh { Size = new Vector3(0.24f, 0.04f, 0.5f), Material = lampMat }, new Transform3D(Basis.Identity, new Vector3(0, 7.22f, -1.6f))),
        };
        foreach (var (mesh, local) in parts)
        {
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh };
            mm.InstanceCount = spots.Count;
            for (int i = 0; i < spots.Count; i++)
            {
                var (pos, yaw) = spots[i];
                mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Rotated(Vector3.Up, yaw), pos) * local);
            }
            AddChild(new MultiMeshInstance3D { Name = "StreetLightParts", Multimesh = mm, GIMode = GeometryInstance3D.GIModeEnum.Disabled, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        foreach (var (pos, yaw) in spots)
        {
            var basis = Basis.Identity.Rotated(Vector3.Up, yaw);
            var light = new OmniLight3D { OmniRange = 26f, LightColor = new Color(1f, 0.9f, 0.72f), ShadowEnabled = false, Position = pos + basis * new Vector3(0, 7.0f, -1.6f), OmniAttenuation = 1.3f };
            DayNight.RegisterNightLight(light, 3.5f);
            AddChild(light);
        }
    }

    private void AddMesh(string name, SurfaceTool st, Godot.Material material, bool receivesCars)
    {
        st.GenerateNormals();
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, material);
        AddChild(new MeshInstance3D { Name = name, Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, GIMode = GeometryInstance3D.GIModeEnum.Static });
    }

    /// <summary>
    /// A strip of quads of the given width along the polyline, following the ground. UV u runs across
    /// the width (0..1), v along the length in units of `vMetres`. Returns the number of triangles added.
    /// Pass a height function or a terrain; the one you give is used.
    /// </summary>
    public static int Ribbon(SurfaceTool st, List<Vector2> pts, float width, float lift, float vMetres, bool markings, Func<float, float, float>? height = null, Terrain? terrain = null, float offset = 0f)
    {
        // subdivide long segments so the ribbon hugs the terrain
        var dense = new List<Vector2> { pts[0] };
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            float len = a.DistanceTo(b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 6f));
            for (int k = 1; k <= n; k++) dense.Add(a.Lerp(b, k / (float)n));
        }
        if (dense.Count < 2) return 0;
        float H(Vector2 p) => height != null ? height(p.X, p.Y) : terrain!.GetHeight(p.X, p.Y);

        float hw = width / 2f;
        var left = new Vector3[dense.Count];
        var right = new Vector3[dense.Count];
        var v = new float[dense.Count];
        float dist = 0f;
        for (int i = 0; i < dense.Count; i++)
        {
            var prev = dense[Mathf.Max(0, i - 1)];
            var next = dense[Mathf.Min(dense.Count - 1, i + 1)];
            var dir = (next - prev).Normalized();
            var nrm = new Vector2(-dir.Y, dir.X);
            if (i > 0) dist += dense[i].DistanceTo(dense[i - 1]);
            var l2 = dense[i] + nrm * (hw + offset);
            var r2 = dense[i] - nrm * (hw - offset);
            left[i] = new Vector3(l2.X, H(l2) + lift, l2.Y);
            right[i] = new Vector3(r2.X, H(r2) + lift, r2.Y);
            v[i] = dist / vMetres;
        }
        int tris = 0;
        for (int i = 1; i < dense.Count; i++)
        {
            // two triangles, wound so they face up (Godot front faces are clockwise seen from the front)
            Tri(st, left[i - 1], new Vector2(0, v[i - 1]), right[i - 1], new Vector2(1, v[i - 1]), right[i], new Vector2(1, v[i]));
            Tri(st, left[i - 1], new Vector2(0, v[i - 1]), right[i], new Vector2(1, v[i]), left[i], new Vector2(0, v[i]));
            tris += 2;
        }
        return tris;
    }

    private static void Tri(SurfaceTool st, Vector3 a, Vector2 ua, Vector3 b, Vector2 ub, Vector3 c, Vector2 uc)
    {
        var n = (b - a).Cross(c - a);
        if (n.Y > 0f) { (b, c) = (c, b); (ub, uc) = (uc, ub); }
        st.SetUV(ua); st.AddVertex(a);
        st.SetUV(ub); st.AddVertex(b);
        st.SetUV(uc); st.AddVertex(c);
    }

    /// <summary>Spots every `spacing` metres along the polyline, offset to the right by `side` metres (negative for left).</summary>
    private void Along(List<Vector2> pts, float spacing, float side, List<(Vector3, float)> spots, float startOffset)
    {
        float next = startOffset + _rng.RandfRange(0f, spacing * 0.5f);
        float dist = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            float len = a.DistanceTo(b);
            if (len < 0.01f) continue;
            var dir = (b - a) / len;
            var nrm = new Vector2(-dir.Y, dir.X);
            while (next <= dist + len)
            {
                float t = (next - dist) / len;
                float s = (_rng.Randf() < 0.5f ? 1f : -1f) * side;
                var p = a.Lerp(b, t) + nrm * s;
                if (_terrain.InBounds(p.X, p.Y) && _terrain.Slope(p.X, p.Y) < 0.35f)
                    spots.Add((new Vector3(p.X, _terrain.GetHeight(p.X, p.Y), p.Y), Mathf.Atan2(-dir.X, -dir.Y)));
                next += spacing;
            }
            dist += len;
        }
    }

    // ------------------------------------------------------------------ parked cars

    private void Cars(List<(Vector3 pos, float yaw)> spots, int count)
    {
        if (count <= 0 || spots.Count == 0) return;
        count = Mathf.Min(count, spots.Count);
        // shuffle, take `count`
        for (int i = spots.Count - 1; i > 0; i--) { int j = _rng.RandiRange(0, i); (spots[i], spots[j]) = (spots[j], spots[i]); }

        var paint = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Metallic = 0.55f, Roughness = 0.3f };
        var glass = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.15f, 0.18f), Metallic = 0.6f, Roughness = 0.15f };
        var rubber = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.08f, 0.08f), Roughness = 0.9f };
        var body = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = new BoxMesh { Size = new Vector3(1.76f, 0.6f, 4.3f), Material = paint } };
        var cabin = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.55f, 2.2f), Material = glass } };
        var wheel = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new CylinderMesh { TopRadius = 0.33f, BottomRadius = 0.33f, Height = 0.22f, RadialSegments = 10, Material = rubber } };
        body.InstanceCount = count; cabin.InstanceCount = count; wheel.InstanceCount = count * 4;
        Color[] paints = { Colors.White, Colors.White, new(0.75f, 0.76f, 0.78f), new(0.1f, 0.1f, 0.12f), new(0.55f, 0.08f, 0.08f), new(0.1f, 0.2f, 0.5f), new(0.45f, 0.47f, 0.5f), new(0.85f, 0.75f, 0.2f), new(0.2f, 0.4f, 0.3f) };
        int drivable = 0;
        var world = GetParent() as WorldBuilder;
        for (int i = 0; i < count; i++)
        {
            var (pos, yaw) = spots[i];
            var basis = Basis.Identity.Rotated(Vector3.Up, yaw);
            // every fifth parked car is a real vehicle you can take; one in four of those is a minibus taxi
            if (world != null && i % 5 == 0 && drivable < 90)
            {
                bool taxi = i % 20 == 0;
                world.AddVehicle(taxi ? "taxi" : "car", taxi ? Colors.White : paints[_rng.RandiRange(0, paints.Length - 1)], pos + new Vector3(0, 0.5f, 0), yaw);
                drivable++;
                body.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.Zero), new Vector3(0, -500, 0)));
                cabin.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.Zero), new Vector3(0, -500, 0)));
                for (int w = 0; w < 4; w++) wheel.SetInstanceTransform(i * 4 + w, new Transform3D(Basis.Identity.Scaled(Vector3.Zero), new Vector3(0, -500, 0)));
                continue;
            }
            var t = new Transform3D(basis, pos);
            body.SetInstanceTransform(i, new Transform3D(basis, pos + basis * new Vector3(0, 0.62f, 0)));
            body.SetInstanceColor(i, paints[_rng.RandiRange(0, paints.Length - 1)]);
            cabin.SetInstanceTransform(i, new Transform3D(basis, pos + basis * new Vector3(0, 1.18f, -0.15f)));
            cabin.SetInstanceColor(i, Colors.White);
            for (int w = 0; w < 4; w++)
            {
                var off = new Vector3(w % 2 == 0 ? -0.82f : 0.82f, 0.33f, w < 2 ? -1.35f : 1.4f);
                wheel.SetInstanceTransform(i * 4 + w, new Transform3D(basis * Basis.FromEuler(new Vector3(0, 0, Mathf.Pi / 2)), pos + basis * off));
            }
            var col = new StaticBody3D { CollisionLayer = Layers.Props, CollisionMask = 0, Transform = new Transform3D(basis, pos + new Vector3(0, 0.8f, 0)) };
            col.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.8f, 1.5f, 4.4f) } });
            AddChild(col);
        }
        AddChild(new MultiMeshInstance3D { Name = "CarBodies", Multimesh = body, GIMode = GeometryInstance3D.GIModeEnum.Disabled });
        AddChild(new MultiMeshInstance3D { Name = "CarCabins", Multimesh = cabin, GIMode = GeometryInstance3D.GIModeEnum.Disabled });
        AddChild(new MultiMeshInstance3D { Name = "CarWheels", Multimesh = wheel, GIMode = GeometryInstance3D.GIModeEnum.Disabled });
    }
}
