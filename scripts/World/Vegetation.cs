using System.Collections.Generic;
using Godot;
using Veld.Core;

namespace Veld.World;

/// <summary>
/// Everything that grows or lies on the veld: thorn trees (two kinds), Karoo shrubs, grass tufts,
/// rocks and termite mounds. One MultiMesh per kind, so the whole lot is a handful of draw calls.
/// Placement avoids roads, towns, the railway and steep rock, and follows a noise field so there are
/// thickets along the drainage lines and open plains elsewhere.
/// </summary>
public partial class Vegetation : Node3D
{
    private RandomNumberGenerator _rng = null!;
    private Terrain _terrain = null!;
    private FastNoiseLite _density = null!;
    private bool _subtropical;
    public ShaderMaterial? GrassMaterial { get; private set; }
    private readonly Queue<Vector2> _woodQueue = new();

    public void Scatter(Terrain terrain, VegetationData data, int seed, bool subtropical = false)
    {
        _terrain = terrain;
        _subtropical = subtropical;
        _rng = new RandomNumberGenerator { Seed = (ulong)seed };
        _density = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Seed = seed + 99, Frequency = 0.004f };

        var leafShader = GD.Load<Shader>("res://shaders/leaf.gdshader");
        var grassShader = GD.Load<Shader>("res://shaders/grass.gdshader");

        Trees(data.Trees, leafShader);
        Shrubs(data.Shrubs, leafShader);
        Rocks(data.Rocks);
        Mounds(data.Mounds);
        Grass(data.Grass, grassShader);
    }

    // ------------------------------------------------------------------ helpers

    private static MultiMesh NewMultiMesh(Mesh mesh, int count, bool colors = false)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = colors, Mesh = mesh };
        mm.InstanceCount = count;
        return mm;
    }

    private MultiMeshInstance3D Add(string name, MultiMesh mm, bool shadows, float visibleTo = 0f, bool gi = true)
    {
        var mi = new MultiMeshInstance3D
        {
            Name = name, Multimesh = mm,
            CastShadow = shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            GIMode = gi ? GeometryInstance3D.GIModeEnum.Static : GeometryInstance3D.GIModeEnum.Disabled,
        };
        if (visibleTo > 0f) { mi.VisibilityRangeEnd = visibleTo; mi.VisibilityRangeEndMargin = 20f; mi.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self; }
        AddChild(mi);
        return mi;
    }

    /// <summary>A random spot on open veld. Returns false if none was found after a few tries.</summary>
    private bool Spot(out float x, out float z, float maxSlope, float densityBias, int tries = 12, float townChance = 0f, float grassChance = 0f)
    {
        for (int t = 0; t < tries; t++)
        {
            x = _rng.RandfRange(-_terrain.Size * 0.485f, _terrain.Size * 0.485f);
            z = _rng.RandfRange(-_terrain.Size * 0.485f, _terrain.Size * 0.485f);
            var surface = _terrain.SurfaceAt(x, z);
            if (surface == Terrain.Surface.Town) { if (_rng.Randf() > townChance) continue; }
            else if (surface == Terrain.Surface.Grass) { if (_rng.Randf() > grassChance) continue; }
            else if (surface != Terrain.Surface.Veld && surface != Terrain.Surface.Wood) continue;
            if (_terrain.Slope(x, z) > maxSlope) continue;
            if (densityBias > 0f)
            {
                float d = _density.GetNoise2D(x, z);
                if (_rng.Randf() > Mathf.Clamp(d * densityBias + 0.45f, 0.04f, 1f)) continue;
            }
            return true;
        }
        x = z = 0f;
        return false;
    }

    private static Basis Upright(float yaw, Vector3 scale) => Basis.Identity.Rotated(Vector3.Up, yaw).Scaled(scale);

    private static StandardMaterial3D Triplanar(Texture2D albedo, Texture2D normal, Color tint, float metresPerTile, float normalScale = 1f, float roughness = 0.95f)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = tint, AlbedoTexture = albedo, NormalEnabled = true, NormalTexture = normal, NormalScale = normalScale,
            Uv1Triplanar = true, Uv1WorldTriplanar = true, Uv1Scale = new Vector3(1f / metresPerTile, 1f / metresPerTile, 1f / metresPerTile),
            Roughness = roughness, VertexColorUseAsAlbedo = true, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
    }

    private static void Trim(MultiMesh mm, int used)
    {
        if (used < mm.InstanceCount) mm.VisibleInstanceCount = used;
    }

    // ------------------------------------------------------------------ trees

    private void Trees(int count, Shader leafShader)
    {
        if (count <= 0) return;
        var bark = Triplanar(Tex.PlankAlbedo(), Tex.PlankNormal(), new Color(0.55f, 0.42f, 0.3f), 0.8f, 0.8f);
        var trunkMesh = new CylinderMesh { TopRadius = 0.11f, BottomRadius = 0.26f, Height = 1f, RadialSegments = 7, Rings = 1, Material = bark };
        var branchMesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.12f, Height = 1f, RadialSegments = 5, Rings = 1, Material = bark };

        var leafMat = new ShaderMaterial { Shader = leafShader };
        leafMat.SetShaderParameter("leaf", Tex.Leaf());
        leafMat.SetShaderParameter("tile", 2.2f);
        var canopyMesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 10, Rings = 6, Material = leafMat };

        var trunks = NewMultiMesh(trunkMesh, count, true);
        var branches = NewMultiMesh(branchMesh, count * 3, true);
        var canopies = NewMultiMesh(canopyMesh, count * 2, true);

        int placed = 0;
        while (placed < count)
        {
            float x, z;
            if (_woodQueue.Count > 0)
            {
                var q = _woodQueue.Dequeue(); x = q.X; z = q.Y;
                if (!_terrain.InBounds(x, z) || _terrain.SurfaceAt(x, z) != Terrain.Surface.Wood) continue;
            }
            else if (!Spot(out x, out z, 0.3f, 0.9f, 40, 0f, 0.04f)) break;
            if (_terrain.SurfaceAt(x, z) == Terrain.Surface.Wood && _woodQueue.Count < 40)
                for (int e = 0; e < 2; e++) _woodQueue.Enqueue(new Vector2(x + _rng.RandfRange(-7f, 7f), z + _rng.RandfRange(-7f, 7f)));
            float h = _terrain.GetHeight(x, z);
            bool umbrella = _rng.Randf() < (_subtropical ? 0.25f : 0.6f); // flat-topped acacia, or a rounder tree
            float s = _rng.RandfRange(0.75f, 1.35f) * (_subtropical ? 1.25f : 1f);
            float yaw = _rng.Randf() * Mathf.Tau;
            float lean = _rng.RandfRange(-0.08f, 0.08f);
            var barkTint = new Color(_rng.RandfRange(0.85f, 1.1f), _rng.RandfRange(0.85f, 1.05f), _rng.RandfRange(0.85f, 1.0f));
            var leafTint = umbrella
                ? new Color(_rng.RandfRange(0.9f, 1.15f), _rng.RandfRange(0.95f, 1.2f), _rng.RandfRange(0.8f, 1.0f))
                : new Color(_rng.RandfRange(0.8f, 1.0f), _rng.RandfRange(0.95f, 1.15f), _rng.RandfRange(0.75f, 0.95f));
            if (_subtropical) leafTint = new Color(leafTint.R * 0.8f, leafTint.G * 1.05f, leafTint.B * 0.85f);

            float trunkH = (umbrella ? 3.6f : 2.6f) * s;
            var tb = Basis.Identity.Rotated(Vector3.Up, yaw).Rotated(Vector3.Right, lean).Scaled(new Vector3(s, trunkH, s));
            trunks.SetInstanceTransform(placed, new Transform3D(tb, new Vector3(x, h + trunkH * 0.5f - 0.1f, z)));
            trunks.SetInstanceColor(placed, barkTint);

            // three branches fanning out from the top of the trunk; the cylinder's axis is Y, so align Y with the branch direction
            for (int b = 0; b < 3; b++)
            {
                float a = yaw + b * 2.1f + _rng.RandfRange(-0.4f, 0.4f);
                float tilt = umbrella ? _rng.RandfRange(0.9f, 1.25f) : _rng.RandfRange(0.45f, 0.8f);
                float len = (umbrella ? 2.4f : 1.6f) * s;
                var dir = new Vector3(Mathf.Cos(a) * Mathf.Sin(tilt), Mathf.Cos(tilt), Mathf.Sin(a) * Mathf.Sin(tilt)).Normalized();
                var top = new Vector3(x, h + trunkH - 0.2f, z);
                var mid = top + dir * len * 0.5f;
                var side = dir.Cross(Vector3.Up).Normalized();
                if (side.LengthSquared() < 0.01f) side = Vector3.Right;
                var fwd = side.Cross(dir).Normalized();
                var bb = new Basis(side * s, dir * len, fwd * s);
                branches.SetInstanceTransform(placed * 3 + b, new Transform3D(bb, mid));
                branches.SetInstanceColor(placed * 3 + b, barkTint);
            }

            // canopy: one wide flat crown plus a smaller offset one, or two rounder ones
            for (int c = 0; c < 2; c++)
            {
                float rx = (umbrella ? (c == 0 ? 3.2f : 2.0f) : (c == 0 ? 2.1f : 1.6f)) * s * _rng.RandfRange(0.9f, 1.1f);
                float ry = (umbrella ? (c == 0 ? 0.9f : 0.7f) : (c == 0 ? 1.8f : 1.3f)) * s;
                var off = c == 0 ? Vector3.Zero : new Vector3(_rng.RandfRange(-1.2f, 1.2f), _rng.RandfRange(-0.3f, 0.5f), _rng.RandfRange(-1.2f, 1.2f)) * s;
                float cy = h + trunkH + (umbrella ? 1.3f : 1.4f) * s;
                var cb = Basis.Identity.Rotated(Vector3.Up, _rng.Randf() * Mathf.Tau).Scaled(new Vector3(rx, ry, rx * _rng.RandfRange(0.85f, 1.1f)));
                canopies.SetInstanceTransform(placed * 2 + c, new Transform3D(cb, new Vector3(x, cy, z) + off));
                canopies.SetInstanceColor(placed * 2 + c, leafTint);
            }
            placed++;
        }
        Trim(trunks, placed); Trim(branches, placed * 3); Trim(canopies, placed * 2);
        Add("Trunks", trunks, true);
        Add("Branches", branches, true);
        Add("Canopies", canopies, true);
    }

    // ------------------------------------------------------------------ shrubs

    private void Shrubs(int count, Shader leafShader)
    {
        if (count <= 0) return;
        var mat = new ShaderMaterial { Shader = leafShader };
        mat.SetShaderParameter("leaf", Tex.Leaf());
        mat.SetShaderParameter("tile", 3.5f);
        mat.SetShaderParameter("sway", 0.02f);
        mat.SetShaderParameter("cutoff", 0.5f);
        var mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 8, Rings = 5, Material = mat };
        var mm = NewMultiMesh(mesh, count, true);
        int placed = 0;
        while (placed < count)
        {
            if (!Spot(out float x, out float z, 0.35f, 0.5f, 20, 0f, 0.02f)) break;
            float h = _terrain.GetHeight(x, z);
            float r = _rng.RandfRange(0.5f, 1.3f);
            // Karoo bossies: grey-green, some silvery, some dry brown
            float kind = _rng.Randf();
            var tint = kind < 0.5f ? new Color(0.95f, 1.0f, 0.85f) : kind < 0.8f ? new Color(1.1f, 1.1f, 1.0f) : new Color(1.2f, 0.95f, 0.7f);
            if (_subtropical) tint = new Color(tint.R * 0.75f, tint.G * 1.05f, tint.B * 0.8f);
            mm.SetInstanceTransform(placed, new Transform3D(Upright(_rng.Randf() * Mathf.Tau, new Vector3(r, r * 0.7f, r * _rng.RandfRange(0.8f, 1.2f))), new Vector3(x, h + r * 0.35f, z)));
            mm.SetInstanceColor(placed, tint);
            placed++;
        }
        Trim(mm, placed);
        Add("Shrubs", mm, false, 420f, false);
    }

    // ------------------------------------------------------------------ rocks

    private void Rocks(int count)
    {
        if (count <= 0) return;
        var mat = Triplanar(Tex.RockAlbedo(), Tex.RockNormal(), new Color(0.72f, 0.62f, 0.5f), 2.5f, 1.2f, 0.85f);
        var mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 9, Rings = 5, Material = mat };
        var mm = NewMultiMesh(mesh, count, true);
        int placed = 0;
        while (placed < count)
        {
            if (!Spot(out float x, out float z, 0.6f, 0f, 20)) break;
            float slope = _terrain.Slope(x, z);
            // bigger boulders on the koppie sides
            float big = slope > 0.2f ? _rng.RandfRange(1.2f, 3.2f) : _rng.RandfRange(0.25f, 1.1f);
            var scale = new Vector3(big * _rng.RandfRange(0.8f, 1.4f), big * _rng.RandfRange(0.45f, 0.8f), big * _rng.RandfRange(0.8f, 1.3f));
            float h = _terrain.GetHeight(x, z);
            var basis = Basis.Identity.Rotated(Vector3.Up, _rng.Randf() * Mathf.Tau).Rotated(Vector3.Right, _rng.RandfRange(-0.25f, 0.25f)).Scaled(scale);
            mm.SetInstanceTransform(placed, new Transform3D(basis, new Vector3(x, h + scale.Y * 0.35f, z)));
            float g = _rng.RandfRange(0.8f, 1.1f);
            mm.SetInstanceColor(placed, new Color(g, g * _rng.RandfRange(0.92f, 1f), g * _rng.RandfRange(0.85f, 1f)));
            placed++;
        }
        Trim(mm, placed);
        Add("Rocks", mm, true);
    }

    // ------------------------------------------------------------------ termite mounds

    private void Mounds(int count)
    {
        if (count <= 0) return;
        var mat = Triplanar(Tex.SoilAlbedo(), Tex.SoilNormal(), new Color(0.62f, 0.45f, 0.3f), 1.5f, 0.8f);
        var mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.7f, Height = 1f, RadialSegments = 9, Rings = 2, Material = mat };
        var mm = NewMultiMesh(mesh, count, true);
        int placed = 0;
        while (placed < count)
        {
            if (!Spot(out float x, out float z, 0.12f, 0f, 20)) break;
            float h = _terrain.GetHeight(x, z);
            float s = _rng.RandfRange(0.6f, 1.8f);
            mm.SetInstanceTransform(placed, new Transform3D(Upright(_rng.Randf() * Mathf.Tau, new Vector3(s, s * 1.4f, s)), new Vector3(x, h + s * 0.6f, z)));
            mm.SetInstanceColor(placed, Colors.White);
            placed++;
        }
        Trim(mm, placed);
        Add("Mounds", mm, true, 500f);
    }

    // ------------------------------------------------------------------ grass

    private void Grass(int count, Shader grassShader)
    {
        if (count <= 0) return;
        var mat = new ShaderMaterial { Shader = grassShader };
        mat.SetShaderParameter("blade", Tex.GrassBlade());
        GrassMaterial = mat;

        // two crossed quads, 1 m wide and 0.75 m tall, pivot at the bottom
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            st.SetUV(new Vector2(0, 1)); st.AddVertex(a);
            st.SetUV(new Vector2(1, 1)); st.AddVertex(b);
            st.SetUV(new Vector2(1, 0)); st.AddVertex(c);
            st.SetUV(new Vector2(0, 1)); st.AddVertex(a);
            st.SetUV(new Vector2(1, 0)); st.AddVertex(c);
            st.SetUV(new Vector2(0, 0)); st.AddVertex(d);
        }
        Quad(new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0.5f, 0.75f, 0), new Vector3(-0.5f, 0.75f, 0));
        Quad(new Vector3(0, 0, -0.5f), new Vector3(0, 0, 0.5f), new Vector3(0, 0.75f, 0.5f), new Vector3(0, 0.75f, -0.5f));
        st.GenerateNormals();
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, mat);

        var mm = NewMultiMesh(mesh, count, true);
        int placed = 0;
        while (placed < count)
        {
            if (!Spot(out float x, out float z, 0.3f, 0.35f, 6, 0.1f, 0.6f)) break;
            float h = _terrain.GetHeight(x, z);
            float s = _rng.RandfRange(0.55f, 1.35f);
            mm.SetInstanceTransform(placed, new Transform3D(Upright(_rng.Randf() * Mathf.Tau, new Vector3(s, s * _rng.RandfRange(0.7f, 1.2f), s)), new Vector3(x, h - 0.02f, z)));
            var ground = _terrain.TintAt(x, z);
            float v = _rng.RandfRange(0.85f, 1.15f);
            mm.SetInstanceColor(placed, new Color(ground.R * 1.3f * v, ground.G * 1.3f * v, ground.B * 1.1f * v));
            placed++;
        }
        Trim(mm, placed);
        Add("Grass", mm, false, 200f, false);
    }
}
