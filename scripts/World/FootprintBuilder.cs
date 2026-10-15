using System;
using System.Collections.Generic;
using Godot;
using Veld.Core;
using Veld.Sim;

namespace Veld.World;

/// <summary>
/// Buildings from real footprints (OpenStreetMap polygons), dressed to look like the housing stock
/// of a Durban township suburb: face brick or pastel plaster, concrete tile / terracotta / corrugated
/// iron hipped roofs, flat-roofed double-storey extensions, a front door on the road side, burglar bars,
/// precast boundary walls with gate pillars, satellite dishes. Geometry is merged per 150 m chunk
/// (one surface per material) with one trimesh collider per chunk.
/// </summary>
public partial class FootprintBuilder : Node3D
{
    private const float ChunkSize = 150f;
    private const float RoadCell = 40f;
    private Terrain _terrain = null!;
    private int _labels, _walls, _doors, _dishCount;
    public static int ModelsPlaced { get; private set; }
    public static int FacadesPlaced { get; private set; }
    private readonly Dictionary<string, PackedScene?> _models = new();
    private readonly List<(Vector2 a, Vector2 b, float width)> _roads = new();
    private readonly Dictionary<(int, int), List<int>> _roadGrid = new();
    private readonly List<(Vector2 c, Vector2 dir)> _placedWalls = new();
    private readonly List<Transform3D> _dishes = new();
    private AddressPlates? _plates;

    private sealed class Chunk
    {
        public readonly SurfaceTool Plaster = New(), Brick = New(), Tiles = New(), Iron = New(), Flat = New(), Precast = New(), Doors = New();
        public int NPlaster, NBrick, NTiles, NIron, NFlat, NPrecast, NDoors;
        public readonly List<Vector3> Faces = new();
        private static SurfaceTool New() { var st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles); return st; }
    }

    private string _mapFolder = "";

    public void Build(WorldData world, Terrain terrain, string mapFolder, AddressPlates? plates = null)
    {
        _terrain = terrain;
        _mapFolder = mapFolder;
        _plates = plates;
        ModelsPlaced = 0; FacadesPlaced = 0;
        IndexRoads(world);

        var chunks = new Dictionary<(int, int), Chunk>();
        int index = 0;
        foreach (var b in world.Buildings)
        {
            var poly = new List<Vector2>();
            foreach (var p in b.Polygon) poly.Add(DataLoader.XZ(p));
            if (poly.Count < 3) continue;
            var c = Centroid(poly);
            if (!terrain.InBounds(c.X, c.Y, 2f)) continue;
            var key = (Mathf.FloorToInt((c.X + terrain.Size / 2) / ChunkSize), Mathf.FloorToInt((c.Y + terrain.Size / 2) / ChunkSize));
            if (!chunks.TryGetValue(key, out var chunk)) { chunk = new Chunk(); chunks[key] = chunk; }
            AddBuilding(chunk, b, poly, c, index++);
        }

        var filter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
        var plasterMat = new StandardMaterial3D { AlbedoTexture = Tex.HouseWall(), VertexColorUseAsAlbedo = true, Roughness = 0.92f, TextureFilter = filter };
        var brickMat = new StandardMaterial3D { AlbedoTexture = Tex.BrickWall(), VertexColorUseAsAlbedo = true, Roughness = 0.95f, TextureFilter = filter };
        var tileMat = new StandardMaterial3D { AlbedoTexture = Tex.RoofTiles(), NormalEnabled = true, NormalTexture = Tex.RoofTilesNormal(), NormalScale = 0.8f, VertexColorUseAsAlbedo = true, Roughness = 0.85f, TextureFilter = filter };
        var ironMat = new StandardMaterial3D { AlbedoTexture = Tex.IronAlbedo(), NormalEnabled = true, NormalTexture = Tex.IronNormal(), NormalScale = 1.0f, VertexColorUseAsAlbedo = true, Roughness = 0.5f, Metallic = 0.45f, TextureFilter = filter };
        var flatMat = new StandardMaterial3D { AlbedoTexture = Tex.Concrete(), VertexColorUseAsAlbedo = true, Roughness = 0.95f, Uv1Triplanar = true, Uv1WorldTriplanar = true, Uv1Scale = new Vector3(0.4f, 0.4f, 0.4f), TextureFilter = filter };
        var precastMat = new StandardMaterial3D { AlbedoTexture = Tex.Precast(), VertexColorUseAsAlbedo = true, Roughness = 0.95f, TextureFilter = filter };
        var doorMat = new StandardMaterial3D { AlbedoTexture = Tex.Door(), Roughness = 0.7f, TextureFilter = filter };

        int nodes = 0;
        foreach (var (key, chunk) in chunks)
        {
            ArrayMesh? mesh = null;
            var mats = new List<Godot.Material>();
            void Add(SurfaceTool st, int n, Godot.Material m) { if (n > 0) { mesh = st.Commit(mesh); mats.Add(m); } }
            Add(chunk.Plaster, chunk.NPlaster, plasterMat);
            Add(chunk.Brick, chunk.NBrick, brickMat);
            Add(chunk.Tiles, chunk.NTiles, tileMat);
            Add(chunk.Iron, chunk.NIron, ironMat);
            Add(chunk.Flat, chunk.NFlat, flatMat);
            Add(chunk.Precast, chunk.NPrecast, precastMat);
            Add(chunk.Doors, chunk.NDoors, doorMat);
            if (mesh == null) continue;
            for (int i = 0; i < mats.Count; i++) mesh.SurfaceSetMaterial(i, mats[i]);
            AddChild(new MeshInstance3D { Name = $"Buildings_{key.Item1}_{key.Item2}", Mesh = mesh, GIMode = GeometryInstance3D.GIModeEnum.Static });
            var body = new StaticBody3D { CollisionLayer = Layers.Buildings, CollisionMask = 0 };
            body.AddChild(new CollisionShape3D { Shape = new ConcavePolygonShape3D { Data = chunk.Faces.ToArray() } });
            AddChild(body);
            nodes += 2;
        }

        if (_dishes.Count > 0)
        {
            var dishMesh = new CylinderMesh { TopRadius = 0.34f, BottomRadius = 0.3f, Height = 0.04f, RadialSegments = 12, Rings = 1, Material = Props.Material(new Color(0.8f, 0.8f, 0.78f), 0.5f, 0.3f) };
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = dishMesh };
            mm.InstanceCount = _dishes.Count;
            for (int i = 0; i < _dishes.Count; i++) mm.SetInstanceTransform(i, _dishes[i]);
            AddChild(new MultiMeshInstance3D { Name = "Dishes", Multimesh = mm, GIMode = GeometryInstance3D.GIModeEnum.Disabled, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        GD.Print($"buildings: {index} footprints in {chunks.Count} chunks ({nodes} nodes), {_walls} boundary walls, {_doors} doors, {_plates?.Count ?? 0} number plates, {_dishes.Count} dishes, {_labels} labels, {ModelsPlaced} real models, {FacadesPlaced} facade photos");
    }

    // ------------------------------------------------------------------ roads (to find each house's front)

    private void IndexRoads(WorldData world)
    {
        foreach (var r in world.Roads)
        {
            if (r.Kind is not ("residential" or "tertiary" or "tertiary_link" or "unclassified" or "secondary" or "primary" or "living_street")) continue;
            for (int i = 1; i < r.Points.Count; i++)
            {
                var a = DataLoader.XZ(r.Points[i - 1]); var b = DataLoader.XZ(r.Points[i]);
                int idx = _roads.Count;
                _roads.Add((a, b, r.Width));
                // register in every grid cell the segment's bounding box touches
                int x0 = Mathf.FloorToInt(Mathf.Min(a.X, b.X) / RoadCell), x1 = Mathf.FloorToInt(Mathf.Max(a.X, b.X) / RoadCell);
                int z0 = Mathf.FloorToInt(Mathf.Min(a.Y, b.Y) / RoadCell), z1 = Mathf.FloorToInt(Mathf.Max(a.Y, b.Y) / RoadCell);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        if (!_roadGrid.TryGetValue((x, z), out var list)) { list = new List<int>(); _roadGrid[(x, z)] = list; }
                        list.Add(idx);
                    }
            }
        }
    }

    private bool NearestRoad(Vector2 p, float maxDist, out Vector2 closest, out Vector2 dir, out float width)
    {
        closest = p; dir = Vector2.Right; width = 6f;
        float best = maxDist * maxDist;
        bool found = false;
        int cx = Mathf.FloorToInt(p.X / RoadCell), cz = Mathf.FloorToInt(p.Y / RoadCell);
        for (int x = cx - 1; x <= cx + 1; x++)
            for (int z = cz - 1; z <= cz + 1; z++)
            {
                if (!_roadGrid.TryGetValue((x, z), out var list)) continue;
                foreach (int i in list)
                {
                    var (a, b, w) = _roads[i];
                    var ab = b - a;
                    float l2 = ab.LengthSquared();
                    if (l2 < 0.01f) continue;
                    float t = Mathf.Clamp((p - a).Dot(ab) / l2, 0f, 1f);
                    var q = a + ab * t;
                    float d2 = p.DistanceSquaredTo(q);
                    if (d2 < best) { best = d2; closest = q; dir = ab / Mathf.Sqrt(l2); width = w; found = true; }
                }
            }
        return found;
    }

    // ------------------------------------------------------------------ one building

    private static readonly Color[] PlasterTints =
    {
        new(0.95f, 0.93f, 0.88f), new(0.93f, 0.88f, 0.76f), new(0.9f, 0.82f, 0.7f), new(0.85f, 0.88f, 0.9f), new(0.88f, 0.9f, 0.82f),
        new(0.93f, 0.8f, 0.72f), new(0.8f, 0.78f, 0.75f), new(0.96f, 0.9f, 0.6f), new(0.7f, 0.75f, 0.82f), new(0.95f, 0.75f, 0.8f), new(0.78f, 0.88f, 0.8f),
    };
    private static readonly Color[] BrickTints = { new(1f, 1f, 1f), new(0.9f, 0.85f, 0.8f), new(0.8f, 0.72f, 0.7f), new(1.05f, 0.95f, 0.9f) };
    private static readonly Color[] TileTints = { new(0.5f, 0.5f, 0.52f), new(0.42f, 0.42f, 0.45f), new(0.62f, 0.3f, 0.2f), new(0.55f, 0.28f, 0.2f), new(0.35f, 0.35f, 0.38f), new(0.6f, 0.45f, 0.35f) };
    private static readonly Color[] IronTints = { new(0.75f, 0.76f, 0.78f), new(0.3f, 0.42f, 0.32f), new(0.55f, 0.2f, 0.15f), new(0.6f, 0.6f, 0.62f), new(0.5f, 0.52f, 0.55f) };

    private void AddBuilding(Chunk chunk, FootprintData b, List<Vector2> poly, Vector2 centroid, int index)
    {
        if (SignedArea(poly) < 0) poly.Reverse();
        float area = Mathf.Abs(SignedArea(poly));
        if (area < 2.5f) return;

        float ground = float.MaxValue, groundMax = float.MinValue;
        foreach (var p in poly) { float h = _terrain.GetHeight(p.X, p.Y); ground = Mathf.Min(ground, h); groundMax = Mathf.Max(groundMax, h); }
        float hc = _terrain.GetHeight(centroid.X, centroid.Y);
        ground = Mathf.Min(ground, hc);
        float baseY = ground - 0.4f;
        float floorY = Mathf.Max(groundMax, hc) + 0.05f;

        uint hash = (uint)(index * 2654435761u);
        uint h1 = hash % 100, h2 = (hash >> 7) % 100, h3 = (hash >> 14) % 100, h4 = (hash >> 21) % 100;

        string kind = b.Kind;
        bool carport = kind is "roof" or "carport";
        bool ruin = kind is "ruins" or "construction";
        bool big = area >= 320f || kind is "school" or "commercial" or "industrial" or "retail" or "warehouse" or "stadium" or "public" or "civic" or "hospital" or "church" or "mosque" or "temple" or "place_of_worship";
        Color? wallOverride = null;
        if (b.Wall.StartsWith("#")) wallOverride = Color.FromHtml(b.Wall);
        bool brick = b.Wall == "brick" || (b.Wall == "" && !big && !carport && h1 < 35);
        if (wallOverride.HasValue || b.Wall == "plaster") brick = false;
        bool extension = !big && !carport && !ruin && b.Levels == 0 && area >= 110f && h2 < 22; // flat-roofed double storey
        float storeys = b.Levels > 0 ? b.Levels : big ? (area > 1500f ? 2f : 1.3f) : extension ? 2f : 1f;
        if (kind == "stadium") storeys = 2.5f;
        float wallH = carport ? 2.6f : ruin ? 1.8f : storeys * 3.0f;
        float topY = floorY + wallH;
        var wallTint = wallOverride ?? (brick ? BrickTints[h3 % (uint)BrickTints.Length] : PlasterTints[h3 % (uint)PlasterTints.Length]);
        if (big) wallTint = wallTint.Lerp(new Color(0.9f, 0.9f, 0.9f), 0.5f);
        if (ruin) wallTint = wallTint.Darkened(0.35f);

        // a real 3D model of this building replaces everything generated
        if (!string.IsNullOrEmpty(b.Model) && PlaceModel(b, centroid, floorY)) return;

        if (carport)
        {
            FlatRoof(chunk.Iron, ref chunk.NIron, poly, topY, IronTints[h3 % (uint)IronTints.Length], chunk.Faces, true);
            for (int i = 0; i < Mathf.Min(poly.Count, 6); i++) Post(chunk, poly[i], baseY, topY, 0.08f, new Color(0.4f, 0.4f, 0.42f));
            return;
        }

        // which wall faces the street
        bool hasRoad = NearestRoad(centroid, 45f, out var roadPt, out var roadDir, out var roadWidth);
        int frontEdge = -1;
        float bestFacing = -2f;
        var toRoad = hasRoad ? (roadPt - centroid).Normalized() : Vector2.Zero;

        // walls
        var wallSt = brick ? chunk.Brick : chunk.Plaster;
        float u = 0f;
        float parapet = extension || (big && !ruin) ? 0.45f : 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var bb = poly[(i + 1) % poly.Count];
            float len = a.DistanceTo(bb);
            if (len < 0.05f) continue;
            var mid = (a + bb) * 0.5f;
            var edge = (bb - a) / len;
            var outward = new Vector2(edge.Y, -edge.X);
            if (outward.Dot(mid - centroid) < 0f) outward = -outward;
            if (hasRoad && len > 2.4f) { float f = outward.Dot(toRoad); if (f > bestFacing) { bestFacing = f; frontEdge = i; } }
            var o3 = new Vector3(outward.X, 0, outward.Y);
            if (!string.IsNullOrEmpty(b.Facade) && hasRoad && len > 2.4f && i == FrontEdgeIndex(poly, centroid, toRoad)) { Facade(b, a, bb, baseY, topY + parapet, o3, chunk.Faces); u += len; continue; }
            float top = topY + parapet;
            var a0 = new Vector3(a.X, baseY, a.Y); var b0 = new Vector3(bb.X, baseY, bb.Y);
            var a1 = new Vector3(a.X, top, a.Y); var b1 = new Vector3(bb.X, top, bb.Y);
            float v0 = (baseY - floorY) / 3f, v1 = (top - floorY) / 3f;
            Quad(wallSt, a0, new Vector2(u / 4f, -v0), b0, new Vector2((u + len) / 4f, -v0), b1, new Vector2((u + len) / 4f, -v1), a1, new Vector2(u / 4f, -v1), o3, wallTint, chunk.Faces);
            if (brick) chunk.NBrick += 2; else chunk.NPlaster += 2;
            u += len;
        }
        if (ruin) return;

        // front door (and a boundary wall out by the street) on the road side
        if (frontEdge >= 0 && bestFacing > 0.3f)
        {
            var a = poly[frontEdge]; var bb = poly[(frontEdge + 1) % poly.Count];
            var edge = (bb - a).Normalized();
            var outward = new Vector2(edge.Y, -edge.X);
            var mid = (a + bb) * 0.5f;
            if (outward.Dot(mid - centroid) < 0f) outward = -outward;
            // the door sits a little off centre so it does not land in the middle of the texture's window
            var dpos = mid + edge * (h4 < 50 ? -1.0f : 1.0f) + outward * 0.04f;
            var o3 = new Vector3(outward.X, 0, outward.Y);
            var d0 = new Vector3(dpos.X - edge.X * 0.475f, floorY, dpos.Y - edge.Y * 0.475f);
            var d1 = new Vector3(dpos.X + edge.X * 0.475f, floorY, dpos.Y + edge.Y * 0.475f);
            Quad(chunk.Doors, d0, new Vector2(0, 1), d1, new Vector2(1, 1), d1 + new Vector3(0, 2.1f, 0), new Vector2(1, 0), d0 + new Vector3(0, 2.1f, 0), new Vector2(0, 0), o3, Colors.White);
            chunk.NDoors += 2;
            _doors++;
            // the number plate hangs beside the door, on the side away from the texture's window
            if (b.HasAddress && _plates != null)
            {
                var beside = dpos + edge * (h4 < 50 ? 0.95f : -0.95f);
                _plates.Add(new Vector3(beside.X, floorY + 1.75f, beside.Y) + o3 * 0.03f, o3, b.Number);
            }
            if (!big && hasRoad) BoundaryWall(chunk, poly, centroid, roadPt, roadDir, roadWidth, hash);
        }

        // roof
        var rect = OrientedRect(poly);
        bool rectangular = !big && !extension && rect.HasValue && area / (rect.Value.halfLong * 2f * rect.Value.halfShort * 2f) > 0.84f && rect.Value.halfShort > 1.5f;
        if (b.RoofKind == "flat") rectangular = false;
        if (rectangular)
        {
            bool ironRoof = b.RoofKind == "iron" || (b.RoofKind == "" && h2 >= 60 && h2 < 80);
            var roofTint = ironRoof ? IronTints[h4 % (uint)IronTints.Length] : TileTints[h4 % (uint)TileTints.Length];
            if (b.RoofColor.StartsWith("#")) { var rc = Color.FromHtml(b.RoofColor); roofTint = new Color(rc.R * 1.35f, rc.G * 1.35f, rc.B * 1.35f); }
            HipRoof(ironRoof ? chunk.Iron : chunk.Tiles, ref (ironRoof ? ref chunk.NIron : ref chunk.NTiles), chunk, rect!.Value, topY, roofTint);
            if (h1 >= 55 && h1 < 90) Dish(rect.Value, topY, hash);
        }
        else
        {
            var flatTint = big ? new Color(0.55f, 0.55f, 0.56f) : new Color(0.5f, 0.5f, 0.5f);
            if (b.RoofColor.StartsWith("#")) { var rc = Color.FromHtml(b.RoofColor); flatTint = new Color(rc.R * 1.3f, rc.G * 1.3f, rc.B * 1.3f); }
            FlatRoof(chunk.Flat, ref chunk.NFlat, poly, topY + 0.08f, flatTint, chunk.Faces, false);
            if (!big && h1 >= 55 && h1 < 80 && rect.HasValue) Dish(rect.Value, topY + 0.1f, hash);
        }

        if (!string.IsNullOrEmpty(b.Name) && (big || area > 400f) && _labels < 60)
        {
            var label = Props.Sign(b.Name, new Vector3(centroid.X, topY + 5f, centroid.Y), 0.02f, new Color(1f, 0.95f, 0.8f), billboard: true);
            label.FontSize = 48;
            AddChild(label);
            _labels++;
        }
    }

    private static int FrontEdgeIndex(List<Vector2> poly, Vector2 centroid, Vector2 toRoad)
    {
        int best = -1; float bestF = -2f;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var bb = poly[(i + 1) % poly.Count];
            float len = a.DistanceTo(bb);
            if (len < 2.4f) continue;
            var edge = (bb - a) / len;
            var outward = new Vector2(edge.Y, -edge.X);
            if (outward.Dot((a + bb) * 0.5f - centroid) < 0f) outward = -outward;
            float f = outward.Dot(toRoad);
            if (f > bestF) { bestF = f; best = i; }
        }
        return best;
    }

    private string MapPath(string file) => file.StartsWith("res://") ? file : _mapFolder + file;

    /// <summary>The street-facing wall as its own mesh with a photo of the real house front.</summary>
    private void Facade(FootprintData b, Vector2 a, Vector2 bb, float baseY, float topY, Vector3 outward, List<Vector3> faces)
    {
        var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(MapPath(b.Facade)));
        if (img == null || img.IsEmpty()) { GD.PushWarning("facade photo not found: " + b.Facade); return; }
        img.GenerateMipmaps();
        var mat = new StandardMaterial3D { AlbedoTexture = ImageTexture.CreateFromImage(img), Roughness = 0.9f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic };
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var a0 = new Vector3(a.X, baseY, a.Y); var b0 = new Vector3(bb.X, baseY, bb.Y);
        var a1 = new Vector3(a.X, topY, a.Y); var b1 = new Vector3(bb.X, topY, bb.Y);
        // the photo spans the wall from the ground line up; the buried part repeats the bottom row
        float vb = 1f + (0.4f / (topY - baseY));
        Quad(st, a0, new Vector2(0, vb), b0, new Vector2(1, vb), b1, new Vector2(1, 0), a1, new Vector2(0, 0), outward, Colors.White, faces);
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, mat);
        AddChild(new MeshInstance3D { Mesh = mesh, GIMode = GeometryInstance3D.GIModeEnum.Static });
        FacadesPlaced++;
    }

    /// <summary>A real model (.glb / .tscn) placed on the footprint; returns false if it could not be loaded.</summary>
    private bool PlaceModel(FootprintData b, Vector2 centroid, float floorY)
    {
        string path = MapPath(b.Model);
        if (!_models.TryGetValue(path, out var scene))
        {
            scene = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
            _models[path] = scene;
            if (scene == null) GD.PushWarning("building model not found (is it inside the project and imported?): " + path);
        }
        if (scene == null) return false;
        var node = scene.Instantiate<Node3D>();
        node.Position = new Vector3(centroid.X, floorY - 0.05f, centroid.Y);
        node.Rotation = new Vector3(0, Mathf.DegToRad(b.ModelYaw), 0);
        node.Scale = Vector3.One * Mathf.Max(0.01f, b.ModelScale);
        AddChild(node);
        // collision from every mesh in the model
        foreach (var mi in FindMeshes(node))
        {
            mi.CreateTrimeshCollision();
            foreach (var c in mi.GetChildren()) if (c is StaticBody3D sb) { sb.CollisionLayer = Layers.Buildings; sb.CollisionMask = 0; }
        }
        ModelsPlaced++;
        return true;
    }

    private static IEnumerable<MeshInstance3D> FindMeshes(Node n)
    {
        if (n is MeshInstance3D mi) yield return mi;
        foreach (var c in n.GetChildren()) foreach (var m in FindMeshes(c)) yield return m;
    }

    /// <summary>A precast boundary wall along the street side of a house, with a gate gap and two pillars.</summary>
    private void BoundaryWall(Chunk chunk, List<Vector2> poly, Vector2 centroid, Vector2 roadPt, Vector2 roadDir, float roadWidth, uint hash)
    {
        var toHouse = centroid - roadPt;
        var nrm = new Vector2(-roadDir.Y, roadDir.X);
        if (nrm.Dot(toHouse) < 0f) nrm = -nrm;
        float houseDist = toHouse.Dot(nrm);
        float wallDist = roadWidth / 2f + 2.1f; // behind the kerb and pavement
        if (houseDist < wallDist + 2.2f || houseDist > 30f) return;
        // extent of the house along the street
        float minT = float.MaxValue, maxT = float.MinValue;
        foreach (var p in poly) { float t = (p - roadPt).Dot(roadDir); minT = Mathf.Min(minT, t); maxT = Mathf.Max(maxT, t); }
        float half = Mathf.Min(14f, (maxT - minT) / 2f + 2.5f);
        var centre = roadPt + roadDir * ((minT + maxT) / 2f) + nrm * wallDist;
        foreach (var (c, d) in _placedWalls)
            if (c.DistanceSquaredTo(centre) < 9f && Mathf.Abs(d.Dot(roadDir)) > 0.9f) return;
        _placedWalls.Add((centre, roadDir));

        float gateHalf = 1.6f;
        float gateAt = ((hash >> 9) % 2 == 0 ? -1f : 1f) * Mathf.Max(0f, half - 2.6f);
        float y = _terrain.GetHeight(centre.X, centre.Y);
        var tint = (hash >> 11) % 3 == 0 ? new Color(0.9f, 0.86f, 0.8f) : Colors.White;
        void Panel(float t0, float t1)
        {
            if (t1 - t0 < 0.3f) return;
            var p0 = centre + roadDir * t0; var p1 = centre + roadDir * t1;
            float y0 = Mathf.Min(_terrain.GetHeight(p0.X, p0.Y), _terrain.GetHeight(p1.X, p1.Y)) - 0.3f;
            float top = Mathf.Max(_terrain.GetHeight(p0.X, p0.Y), _terrain.GetHeight(p1.X, p1.Y)) + 1.8f;
            var n3 = new Vector3(nrm.X, 0, nrm.Y);
            var thick = n3 * 0.07f;
            var a0 = new Vector3(p0.X, y0, p0.Y); var b0 = new Vector3(p1.X, y0, p1.Y);
            var a1 = new Vector3(p0.X, top, p0.Y); var b1 = new Vector3(p1.X, top, p1.Y);
            float u0 = t0 / 1.5f, u1 = t1 / 1.5f, v = (top - y0) / 1.8f;
            Quad(chunk.Precast, a0 - thick, new Vector2(u0, v), b0 - thick, new Vector2(u1, v), b1 - thick, new Vector2(u1, 0), a1 - thick, new Vector2(u0, 0), -n3, tint, chunk.Faces);
            Quad(chunk.Precast, a0 + thick, new Vector2(u0, v), b0 + thick, new Vector2(u1, v), b1 + thick, new Vector2(u1, 0), a1 + thick, new Vector2(u0, 0), n3, tint, chunk.Faces);
            Quad(chunk.Precast, a1 - thick, Vector2.Zero, b1 - thick, Vector2.Zero, b1 + thick, Vector2.Zero, a1 + thick, Vector2.Zero, Vector3.Up, tint.Darkened(0.1f));
            chunk.NPrecast += 6;
        }
        Panel(-half, gateAt - gateHalf);
        Panel(gateAt + gateHalf, half);
        Post(chunk, centre + roadDir * (gateAt - gateHalf - 0.2f), y - 0.3f, y + 2.05f, 0.2f, new Color(0.92f, 0.9f, 0.85f));
        Post(chunk, centre + roadDir * (gateAt + gateHalf + 0.2f), y - 0.3f, y + 2.05f, 0.2f, new Color(0.92f, 0.9f, 0.85f));
        _walls++;
    }

    private void Dish((Vector2 center, Vector2 axis, float halfLong, float halfShort) r, float roofY, uint hash)
    {
        if (_dishes.Count >= 6000) return;
        var v = new Vector2(-r.axis.Y, r.axis.X);
        float side = (hash >> 16) % 2 == 0 ? 1f : -1f;
        var p = r.center + r.axis * (r.halfLong * 0.45f * side) + v * (r.halfShort * 0.55f);
        float rise = Mathf.Clamp((r.halfShort + 0.45f) * 0.5f, 0.8f, 3.5f) * 0.5f;
        var basis = Basis.Identity.Rotated(Vector3.Up, (hash >> 3) % 628 / 100f).Rotated(Vector3.Right, -0.85f);
        _dishes.Add(new Transform3D(basis, new Vector3(p.X, roofY + rise + 0.35f, p.Y)));
    }

    private void FlatRoof(SurfaceTool st, ref int count, List<Vector2> poly, float y, Color tint, List<Vector3> faces, bool iron)
    {
        var pts = poly.ToArray();
        var idx = Geometry2D.TriangulatePolygon(pts);
        Vector2 UV(Vector2 p) => iron ? p : Vector2.Zero;
        if (idx.Length < 3)
        {
            var c = Centroid(poly);
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                Tri(st, new Vector3(c.X, y, c.Y), new Vector3(a.X, y, a.Y), new Vector3(b.X, y, b.Y), Vector3.Up, tint, faces, UV(c), UV(a), UV(b));
                count++;
            }
            return;
        }
        for (int i = 0; i + 2 < idx.Length; i += 3)
        {
            var a = pts[idx[i]]; var b = pts[idx[i + 1]]; var c = pts[idx[i + 2]];
            Tri(st, new Vector3(a.X, y, a.Y), new Vector3(b.X, y, b.Y), new Vector3(c.X, y, c.Y), Vector3.Up, tint, faces, UV(a), UV(b), UV(c));
            count++;
        }
    }

    private void HipRoof(SurfaceTool st, ref int count, Chunk chunk, (Vector2 center, Vector2 axis, float halfLong, float halfShort) r, float eaveY, Color tint)
    {
        var u = r.axis; var v = new Vector2(-u.Y, u.X);
        float o = 0.45f;
        float L = r.halfLong + o, S = r.halfShort + o;
        float rise = Mathf.Clamp(S * 0.5f, 0.8f, 3.5f);
        float ridgeHalf = Mathf.Max(0f, r.halfLong - r.halfShort);
        Vector3 P(Vector2 p, float y) => new(p.X, y, p.Y);
        var c = r.center;
        var e1 = P(c + u * L + v * S, eaveY); var e2 = P(c - u * L + v * S, eaveY);
        var e3 = P(c - u * L - v * S, eaveY); var e4 = P(c + u * L - v * S, eaveY);
        var r1 = P(c + u * ridgeHalf, eaveY + rise); var r2 = P(c - u * ridgeHalf, eaveY + rise);
        var side = new Vector3(v.X, 0.6f, v.Y).Normalized();
        var end = new Vector3(u.X, 0.6f, u.Y).Normalized();
        float tileV = 1f / 0.4f;
        Quad(st, e1, new Vector2(L, S * tileV), e2, new Vector2(-L, S * tileV), r2, new Vector2(-ridgeHalf, 0), r1, new Vector2(ridgeHalf, 0), side, tint, chunk.Faces);
        Quad(st, e3, new Vector2(L, S * tileV), e4, new Vector2(-L, S * tileV), r1, new Vector2(-ridgeHalf, 0), r2, new Vector2(ridgeHalf, 0), -side, tint, chunk.Faces);
        Tri(st, e4, e1, r1, end, tint, chunk.Faces, new Vector2(-S, S * tileV), new Vector2(S, S * tileV), new Vector2(0, 0));
        Tri(st, e2, e3, r2, -end, tint, chunk.Faces, new Vector2(-S, S * tileV), new Vector2(S, S * tileV), new Vector2(0, 0));
        count += 6;
        Quad(chunk.Flat, e1, Vector2.Zero, e2, Vector2.Zero, e3, Vector2.Zero, e4, Vector2.Zero, Vector3.Down, tint.Darkened(0.4f));
        chunk.NFlat += 2;
    }

    private void Post(Chunk chunk, Vector2 at, float y0, float y1, float r, Color tint)
    {
        var a = new Vector2(at.X - r, at.Y - r); var b = new Vector2(at.X + r, at.Y - r); var c = new Vector2(at.X + r, at.Y + r); var d = new Vector2(at.X - r, at.Y + r);
        Vector2[] ring = { a, b, c, d };
        for (int i = 0; i < 4; i++)
        {
            var p = ring[i]; var q = ring[(i + 1) % 4];
            var mid = (p + q) * 0.5f;
            var outward = (mid - at).Normalized();
            Quad(chunk.Plaster, new Vector3(p.X, y0, p.Y), Vector2.Zero, new Vector3(q.X, y0, q.Y), Vector2.Zero, new Vector3(q.X, y1, q.Y), Vector2.Zero, new Vector3(p.X, y1, p.Y), Vector2.Zero, new Vector3(outward.X, 0, outward.Y), tint, chunk.Faces);
            chunk.NPlaster += 2;
        }
        Quad(chunk.Plaster, new Vector3(a.X, y1, a.Y), Vector2.Zero, new Vector3(b.X, y1, b.Y), Vector2.Zero, new Vector3(c.X, y1, c.Y), Vector2.Zero, new Vector3(d.X, y1, d.Y), Vector2.Zero, Vector3.Up, tint);
        chunk.NPlaster += 2;
    }

    // ------------------------------------------------------------------ geometry helpers

    private static void Quad(SurfaceTool st, Vector3 a, Vector2 ua, Vector3 b, Vector2 ub, Vector3 c, Vector2 uc, Vector3 d, Vector2 ud, Vector3 outward, Color tint, List<Vector3>? faces = null)
    {
        Tri(st, a, b, c, outward, tint, faces, ua, ub, uc);
        Tri(st, a, c, d, outward, tint, faces, ua, uc, ud);
    }

    private static void Tri(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color tint, List<Vector3>? faces, Vector2? ua = null, Vector2? ub = null, Vector2? uc = null)
    {
        var n = (b - a).Cross(c - a);
        if (n.Dot(outward) > 0f) { (b, c) = (c, b); (ub, uc) = (uc, ub); }
        var normal = outward.Normalized();
        st.SetColor(tint); st.SetNormal(normal); st.SetUV(ua ?? Vector2.Zero); st.AddVertex(a);
        st.SetColor(tint); st.SetNormal(normal); st.SetUV(ub ?? Vector2.Zero); st.AddVertex(b);
        st.SetColor(tint); st.SetNormal(normal); st.SetUV(uc ?? Vector2.Zero); st.AddVertex(c);
        if (faces != null) { faces.Add(a); faces.Add(b); faces.Add(c); }
    }

    public static float SignedArea(List<Vector2> p)
    {
        float s = 0f;
        for (int i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; s += a.X * b.Y - b.X * a.Y; }
        return s * 0.5f;
    }

    public static Vector2 Centroid(List<Vector2> p)
    {
        var c = Vector2.Zero;
        foreach (var q in p) c += q;
        return c / p.Count;
    }

    /// <summary>Bounding rectangle aligned with the polygon's longest edge.</summary>
    public static (Vector2 center, Vector2 axis, float halfLong, float halfShort)? OrientedRect(List<Vector2> poly)
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
        return (center, u, hl, hs);
    }
}
