using Godot;
using Veld.Core;
using Veld.Sim;

namespace Veld.World;

/// <summary>
/// Turns a TownData entry into buildings. Each building type is a small recipe of textured boxes,
/// prisms, cylinders and signs with a collision box. Add a new type by adding a case to BuildOne().
/// Positions are the town's local frame: offset [x, z] rotated by the town rotation. Houses face +z.
/// </summary>
public static class TownBuilder
{
    private static readonly Color PlinthStone = new(0.72f, 0.66f, 0.58f);
    private static readonly Color FrameWhite = new(0.92f, 0.9f, 0.84f);
    private static readonly Color RoofIronDark = new(0.32f, 0.3f, 0.3f);

    public static Node3D Build(TownData town, Terrain terrain)
    {
        var root = new Node3D { Name = "Town_" + town.Id };
        var center = DataLoader.XZ(town.Position);
        float baseY = terrain.GetHeight(center.X, center.Y);
        root.Position = new Vector3(center.X, baseY, center.Y);
        root.Rotation = new Vector3(0, Mathf.DegToRad(town.Rotation), 0);

        foreach (var b in town.Buildings)
        {
            var node = BuildOne(b);
            node.Position = new Vector3(b.Offset[0], 0, b.Offset[1]);
            node.Rotation = new Vector3(0, Mathf.DegToRad(b.Rotation), 0);
            root.AddChild(node);
        }

        var nameSign = Props.Sign(town.Name.ToUpperInvariant(), new Vector3(0, 9f, 0), 0.03f, new Color(1f, 0.92f, 0.7f), billboard: true);
        nameSign.FontSize = 64;
        root.AddChild(nameSign);
        return root;
    }

    private static Node3D BuildOne(BuildingData b)
    {
        var n = new Node3D { Name = b.Type + (string.IsNullOrEmpty(b.Sign) ? "" : "_" + b.Sign.Replace(' ', '_')) };
        var size = new Vector3(b.Size[0], b.Size[1], b.Size[2]);
        switch (b.Type)
        {
            case "church": Church(n, size, b); break;
            case "station": Station(n, size, b); break;
            case "tent": Tent(n, size); break;
            case "barracks": Barracks(n, size, b); break;
            case "kraal": Kraal(n, size); break;
            case "well": Well(n); break;
            case "hitchrail": HitchRail(n, size); break;
            case "blockhouse": Blockhouse(n, size); break;
            case "fence": Fence(n, size); break;
            case "barrels": Barrels(n); break;
            case "crate": Crate(n, size); break;
            case "trough": Trough(n); break;
            case "wagon": Wagon(n); break;
            default: House(n, size, b); break;
        }
        return n;
    }

    // ------------------------------------------------------------------ shared pieces

    /// <summary>A box running from a to b (fence rails, wires, beams).</summary>
    public static MeshInstance3D Beam(Node3D n, Vector3 a, Vector3 b, float thickness, Godot.Material material)
    {
        var mid = (a + b) * 0.5f;
        float len = a.DistanceTo(b);
        var mi = Props.Box(new Vector3(thickness, thickness, len), material);
        var dir = (b - a).Normalized();
        var up = Mathf.Abs(dir.Y) > 0.95f ? Vector3.Right : Vector3.Up;
        mi.Transform = new Transform3D(Basis.LookingAt(dir, up), mid);
        n.AddChild(mi);
        return mi;
    }

    /// <summary>A sash window: white frame, glass, mullions and a sill. z is the wall's outer face.</summary>
    private static void Window(Node3D n, Vector3 pos, float w, float h, float yaw = 0f)
    {
        var win = new Node3D { Position = pos, Rotation = new Vector3(0, yaw, 0) };
        win.AddChild(Props.Box(new Vector3(w + 0.22f, h + 0.22f, 0.1f), Props.Planks(FrameWhite), new Vector3(0, 0, 0.0f)));
        win.AddChild(Props.Box(new Vector3(w, h, 0.06f), Props.Glass(), new Vector3(0, 0, 0.03f)));
        win.AddChild(Props.Box(new Vector3(0.05f, h, 0.12f), Props.Planks(FrameWhite), new Vector3(0, 0, 0.02f)));
        win.AddChild(Props.Box(new Vector3(w, 0.05f, 0.12f), Props.Planks(FrameWhite), new Vector3(0, 0, 0.02f)));
        win.AddChild(Props.Box(new Vector3(w + 0.34f, 0.08f, 0.22f), Props.Planks(FrameWhite), new Vector3(0, -h / 2 - 0.15f, 0.06f)));
        n.AddChild(win);
    }

    private static void Door(Node3D n, Vector3 pos, float w = 1.05f, float h = 2.15f, float yaw = 0f)
    {
        var door = new Node3D { Position = pos, Rotation = new Vector3(0, yaw, 0) };
        door.AddChild(Props.Box(new Vector3(w + 0.26f, h + 0.18f, 0.12f), Props.Planks(FrameWhite), new Vector3(0, 0.04f, 0f)));
        door.AddChild(Props.Box(new Vector3(w, h, 0.08f), Props.Planks(Props.DarkWood), new Vector3(0, 0, 0.04f)));
        door.AddChild(Props.Sphere(0.035f, Props.Material(Props.Iron, 0.4f, 0.8f), new Vector3(w * 0.35f, 0, 0.1f)));
        n.AddChild(door);
    }

    private static void Chimney(Node3D n, Vector3 pos)
    {
        n.AddChild(Props.Box(new Vector3(0.8f, 2.0f, 0.8f), Props.StoneBlocks(PlinthStone), pos));
        n.AddChild(Props.Box(new Vector3(1.0f, 0.14f, 1.0f), Props.Material(Props.Stone), pos + new Vector3(0, 1.0f, 0)));
        n.AddChild(new CpuParticles3D
        {
            Position = pos + new Vector3(0, 1.1f, 0), Amount = 10, Lifetime = 3.5f, Emitting = true,
            Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 6, Rings = 3, Material = Props.ParticleMaterial() },
            Direction = new Vector3(0.3f, 1, 0.1f), Spread = 14f, InitialVelocityMin = 0.35f, InitialVelocityMax = 0.7f,
            Gravity = new Vector3(0.25f, 0.12f, 0), ScaleAmountMin = 0.5f, ScaleAmountMax = 1.8f, Color = new Color(0.6f, 0.6f, 0.62f, 0.22f),
        });
    }

    // ------------------------------------------------------------------ buildings

    private static void House(Node3D n, Vector3 size, BuildingData b)
    {
        var wallColor = Props.Parse(b.Color, b.Type switch
        {
            "shop" => Props.Sandstone,
            "hotel" => new Color(0.88f, 0.82f, 0.68f),
            "smithy" => Props.Stone,
            _ => Props.Whitewash,
        });
        var wall = b.Type == "smithy" ? Props.StoneBlocks(wallColor) : Props.Plaster(wallColor);
        var roofColor = b.Type == "smithy" ? Props.CorrugatedGrey : Props.CorrugatedRed;
        var roof = Props.Corrugated(roofColor);
        float stoepDepth = 2.4f;

        // walls on a stone plinth
        n.AddChild(Props.SolidBox(size, wall, Layers.Buildings, new Vector3(0, size.Y / 2, 0)));
        n.AddChild(Props.Box(new Vector3(size.X + 0.16f, 0.6f, size.Z + 0.16f), Props.StoneBlocks(PlinthStone), new Vector3(0, 0.3f, 0)));
        // gable roof with ridge cap and fascia boards
        n.AddChild(Props.Prism(new Vector3(size.X + 1.4f, size.Y * 0.45f, size.Z + 1.4f), roof, new Vector3(0, size.Y + size.Y * 0.225f, 0)));
        n.AddChild(Props.Box(new Vector3(size.X + 1.5f, 0.12f, 0.32f), Props.Corrugated(RoofIronDark), new Vector3(0, size.Y + size.Y * 0.45f, 0)));
        for (int s = -1; s <= 1; s += 2)
            n.AddChild(Props.Box(new Vector3(size.X + 1.4f, 0.22f, 0.06f), Props.Planks(Props.DarkWood), new Vector3(0, size.Y + 0.05f, s * (size.Z / 2 + 0.7f))));
        // stoep (porch) on the front (+z): plank floor, step, posts, rail, iron roof
        var planks = Props.Planks(Props.Wood);
        n.AddChild(Props.Box(new Vector3(size.X, 0.28f, stoepDepth), planks, new Vector3(0, 0.14f, size.Z / 2 + stoepDepth / 2)));
        n.AddChild(Props.Box(new Vector3(1.6f, 0.14f, 0.5f), planks, new Vector3(0, 0.07f, size.Z / 2 + stoepDepth + 0.25f)));
        float stoepRoofY = size.Y * 0.82f;
        n.AddChild(Props.Box(new Vector3(size.X + 0.4f, 0.1f, stoepDepth + 0.3f), roof, new Vector3(0, stoepRoofY, size.Z / 2 + stoepDepth / 2 + 0.1f)));
        n.AddChild(Props.Box(new Vector3(size.X + 0.4f, 0.2f, 0.06f), Props.Planks(Props.DarkWood), new Vector3(0, stoepRoofY - 0.05f, size.Z / 2 + stoepDepth + 0.25f)));
        int posts = Mathf.Max(2, Mathf.RoundToInt(size.X / 3f) + 1);
        for (int i = 0; i < posts; i++)
        {
            float x = -size.X / 2 + 0.3f + i * (size.X - 0.6f) / (posts - 1);
            n.AddChild(Props.Cylinder(0.08f, stoepRoofY - 0.28f, Props.Planks(Props.DarkWood), new Vector3(x, 0.28f + (stoepRoofY - 0.28f) / 2, size.Z / 2 + stoepDepth - 0.2f), -1, 8));
            if (i > 0 && !(i == posts / 2 || (posts == 2 && i == 1)))
            {
                float x0 = -size.X / 2 + 0.3f + (i - 1) * (size.X - 0.6f) / (posts - 1);
                Beam(n, new Vector3(x0, 1.0f, size.Z / 2 + stoepDepth - 0.2f), new Vector3(x, 1.0f, size.Z / 2 + stoepDepth - 0.2f), 0.06f, Props.Planks(Props.DarkWood));
            }
        }
        // door, windows, chimney, lantern
        Door(n, new Vector3(0, 1.08f + 0.28f, size.Z / 2 + 0.02f));
        for (int i = -1; i <= 1; i += 2)
            Window(n, new Vector3(i * size.X * 0.3f, 1.95f, size.Z / 2 + 0.02f), 1.0f, 1.3f);
        if (size.Z >= 7f)
            for (int s = -1; s <= 1; s += 2)
                Window(n, new Vector3(s * (size.X / 2 + 0.02f), 1.95f, -size.Z * 0.1f), 0.9f, 1.2f, s * Mathf.Pi / 2);
        if (b.Type == "hotel" && size.Y >= 6f)
            for (int i = -2; i <= 2; i++)
                if (i != 0) Window(n, new Vector3(i * size.X * 0.19f, 4.9f, size.Z / 2 + 0.02f), 0.9f, 1.2f);
        Chimney(n, new Vector3(size.X * 0.35f, size.Y + size.Y * 0.35f, -size.Z * 0.15f));
        n.AddChild(Props.Lantern(new Vector3(0.95f, 2.45f, size.Z / 2 + 0.16f)));

        if (!string.IsNullOrEmpty(b.Sign))
            n.AddChild(Props.SignBoard(b.Sign, new Vector3(0, stoepRoofY + 0.55f, size.Z / 2 + stoepDepth + 0.2f), Mathf.Min(size.X + 0.4f, 0.38f * b.Sign.Length + 1f), Props.GreenPaint, new Color(0.95f, 0.9f, 0.75f)));
    }

    private static void Church(Node3D n, Vector3 size, BuildingData b)
    {
        var wall = Props.Plaster(Props.Whitewash);
        n.AddChild(Props.SolidBox(size, wall, Layers.Buildings, new Vector3(0, size.Y / 2, 0)));
        n.AddChild(Props.Box(new Vector3(size.X + 0.2f, 0.8f, size.Z + 0.2f), Props.StoneBlocks(PlinthStone), new Vector3(0, 0.4f, 0)));
        n.AddChild(Props.Prism(new Vector3(size.X + 1.2f, size.Y * 0.5f, size.Z + 1.2f), Props.Corrugated(Props.CorrugatedGrey), new Vector3(0, size.Y + size.Y * 0.25f, 0)));
        n.AddChild(Props.Box(new Vector3(size.X + 1.3f, 0.12f, 0.32f), Props.Corrugated(RoofIronDark), new Vector3(0, size.Y + size.Y * 0.5f, 0)));
        // steeple at the front
        float towerH = size.Y * 2.2f;
        float frontZ = size.Z / 2 + 1.6f;
        n.AddChild(Props.SolidBox(new Vector3(3.4f, towerH, 3.4f), wall, Layers.Buildings, new Vector3(0, towerH / 2, frontZ)));
        n.AddChild(Props.Box(new Vector3(3.6f, 0.8f, 3.6f), Props.StoneBlocks(PlinthStone), new Vector3(0, 0.4f, frontZ)));
        n.AddChild(Props.Cylinder(2.4f, 4.4f, Props.Corrugated(RoofIronDark), new Vector3(0, towerH + 2.2f, frontZ), 0.05f, 8));
        n.AddChild(Props.Box(new Vector3(0.14f, 1.6f, 0.14f), Props.Material(Props.Iron, 0.5f, 0.7f), new Vector3(0, towerH + 5.1f, frontZ)));
        n.AddChild(Props.Box(new Vector3(0.9f, 0.14f, 0.14f), Props.Material(Props.Iron, 0.5f, 0.7f), new Vector3(0, towerH + 5.5f, frontZ)));
        // tower windows (louvred belfry) and clock-like roundel
        for (int s = -1; s <= 1; s += 2)
            n.AddChild(Props.Box(new Vector3(0.7f, 1.6f, 0.1f), Props.Planks(Props.DarkWood), new Vector3(s * 0.0f, towerH - 1.6f, frontZ + s * 1.72f)));
        Door(n, new Vector3(0, 1.35f + 0.4f, frontZ + 1.7f + 0.02f), 1.5f, 2.6f);
        for (int i = 0; i < 3; i++)
            for (int s = -1; s <= 1; s += 2)
                Window(n, new Vector3(s * (size.X / 2 + 0.02f), size.Y * 0.55f, -size.Z * 0.3f + i * size.Z * 0.3f), 0.9f, 2.6f, s * Mathf.Pi / 2);
        n.AddChild(Props.Lantern(new Vector3(1.3f, 2.9f, frontZ + 1.7f + 0.16f)));
        n.AddChild(Props.Sign(string.IsNullOrEmpty(b.Sign) ? "N.G. KERK" : b.Sign, new Vector3(0, size.Y + 1.0f, frontZ + 1.8f), 0.012f, new Color(0.2f, 0.18f, 0.15f)));
    }

    private static void Station(Node3D n, Vector3 size, BuildingData b)
    {
        var stone = Props.StoneBlocks(PlinthStone);
        var sand = Props.StoneBlocks(Props.Sandstone);
        var ironGrey = Props.Corrugated(Props.CorrugatedGrey);
        var iron = Props.Material(Props.Iron, 0.45f, 0.7f);
        float platW = size.X + 10f, platD = size.Z + 6f;
        n.AddChild(Props.SolidBox(new Vector3(platW, 0.9f, platD), stone, Layers.Buildings, new Vector3(0, 0.45f, 0)));
        n.AddChild(Props.Box(new Vector3(platW, 0.1f, 0.4f), Props.Material(new Color(0.85f, 0.82f, 0.75f)), new Vector3(0, 0.92f, platD / 2 - 0.2f)));
        n.AddChild(Props.SolidBox(size, sand, Layers.Buildings, new Vector3(0, 0.9f + size.Y / 2, -1.5f)));
        n.AddChild(Props.Prism(new Vector3(size.X + 1.2f, size.Y * 0.4f, size.Z + 1.2f), ironGrey, new Vector3(0, 0.9f + size.Y + size.Y * 0.2f, -1.5f)));
        n.AddChild(Props.Box(new Vector3(size.X + 1.3f, 0.12f, 0.32f), Props.Corrugated(RoofIronDark), new Vector3(0, 0.9f + size.Y + size.Y * 0.4f, -1.5f)));
        // canopy over the platform edge (+z faces the track), on iron columns with brackets
        float canopyY = 0.9f + size.Y * 0.85f;
        float canopyZ = size.Z / 2 + 1.5f;
        n.AddChild(Props.Box(new Vector3(size.X + 8f, 0.12f, 4.2f), ironGrey, new Vector3(0, canopyY, canopyZ)));
        n.AddChild(Props.Box(new Vector3(size.X + 8f, 0.45f, 0.06f), Props.Planks(Props.GreenPaint), new Vector3(0, canopyY - 0.2f, canopyZ + 2.1f)));
        for (int i = -2; i <= 2; i++)
        {
            float x = i * (size.X + 8f) / 5f;
            n.AddChild(Props.Cylinder(0.09f, canopyY - 0.9f, iron, new Vector3(x, 0.9f + (canopyY - 0.9f) / 2, canopyZ + 1.7f), -1, 8));
            Beam(n, new Vector3(x, canopyY - 0.9f, canopyZ + 1.7f), new Vector3(x, canopyY - 0.1f, canopyZ + 0.9f), 0.06f, iron);
            Beam(n, new Vector3(x, canopyY - 0.9f, canopyZ + 1.7f), new Vector3(x, canopyY - 0.1f, canopyZ + 2.5f), 0.06f, iron);
        }
        Door(n, new Vector3(0, 0.9f + 1.08f, -1.5f + size.Z / 2 + 0.02f));
        for (int i = -1; i <= 1; i += 2)
            Window(n, new Vector3(i * size.X * 0.3f, 0.9f + 1.9f, -1.5f + size.Z / 2 + 0.02f), 1.1f, 1.3f);
        // benches and lanterns under the canopy
        for (int s = -1; s <= 1; s += 2)
        {
            var bench = new Node3D { Position = new Vector3(s * (size.X * 0.42f), 0.9f, canopyZ - 0.6f) };
            bench.AddChild(Props.Box(new Vector3(2.0f, 0.08f, 0.45f), Props.Planks(Props.GreenPaint), new Vector3(0, 0.45f, 0)));
            bench.AddChild(Props.Box(new Vector3(2.0f, 0.45f, 0.08f), Props.Planks(Props.GreenPaint), new Vector3(0, 0.75f, -0.2f)));
            for (int l = -1; l <= 1; l += 2) bench.AddChild(Props.Box(new Vector3(0.08f, 0.45f, 0.4f), iron, new Vector3(l * 0.9f, 0.22f, 0)));
            n.AddChild(bench);
            n.AddChild(Props.Lantern(new Vector3(s * 4f, canopyY - 0.55f, canopyZ), 1.6f, 11f));
        }
        // water tank for locomotives, on timber trestles
        n.AddChild(Props.Cylinder(1.7f, 2.4f, Props.Corrugated(new Color(0.35f, 0.33f, 0.32f)), new Vector3(size.X / 2 + 6f, 0.9f + 5.2f, -2f)));
        for (int i = -1; i <= 1; i += 2)
            for (int j = -1; j <= 1; j += 2)
                n.AddChild(Props.Box(new Vector3(0.3f, 4f, 0.3f), Props.Planks(Props.DarkWood), new Vector3(size.X / 2 + 6f + i * 1.1f, 0.9f + 2f, -2f + j * 1.1f)));
        n.AddChild(Props.Cylinder(0.08f, 1.8f, iron, new Vector3(size.X / 2 + 6f, 0.9f + 3.4f, -0.4f), -1, 6));
        n.AddChild(Props.SignBoard(string.IsNullOrEmpty(b.Sign) ? "STATION" : b.Sign, new Vector3(0, canopyY + 0.5f, canopyZ + 2.15f), Mathf.Max(5f, 0.4f * (b.Sign.Length + 2)), Props.GreenPaint, new Color(0.95f, 0.9f, 0.75f)));
    }

    private static void Tent(Node3D n, Vector3 size)
    {
        n.AddChild(Props.Prism(new Vector3(size.X, size.Y, size.Z), Props.Cloth(Props.Canvas), new Vector3(0, size.Y / 2, 0)));
        var body = new StaticBody3D { CollisionLayer = Layers.Buildings, CollisionMask = 0, Position = new Vector3(0, size.Y / 2, 0) };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(size.X, size.Y, size.Z) } });
        n.AddChild(body);
    }

    private static void Barracks(Node3D n, Vector3 size, BuildingData b)
    {
        // a garrison camp: rows of bell tents, a flagpole, a sandbag wall, crates and a lamp post
        var canvas = Props.Cloth(Props.Canvas);
        for (int r = 0; r < 2; r++)
            for (int i = 0; i < 4; i++)
            {
                var t = new Node3D { Position = new Vector3(-size.X / 2 + 4f + i * (size.X - 8f) / 3f, 0, -size.Z / 2 + 4f + r * (size.Z - 8f)) };
                t.AddChild(Props.Cylinder(2.4f, 3.2f, canvas, new Vector3(0, 1.6f, 0), 0.12f, 12));
                t.AddChild(Props.Cylinder(0.06f, 3.6f, Props.Planks(Props.Wood), new Vector3(0, 1.8f, 0), -1, 6));
                var body = new StaticBody3D { CollisionLayer = Layers.Buildings, CollisionMask = 0, Position = new Vector3(0, 1.4f, 0) };
                body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.2f, Height = 2.8f } });
                t.AddChild(body);
                n.AddChild(t);
            }
        n.AddChild(Props.Cylinder(0.08f, 9f, Props.Planks(Props.Wood), new Vector3(0, 4.5f, 0), -1, 8));
        n.AddChild(Props.Box(new Vector3(1.8f, 1.1f, 0.05f), Props.Cloth(new Color(0.75f, 0.1f, 0.15f)), new Vector3(0.9f, 8.2f, 0)));
        n.AddChild(Props.Box(new Vector3(0.6f, 1.1f, 0.06f), Props.Cloth(new Color(0.15f, 0.2f, 0.6f)), new Vector3(0.3f, 8.2f, 0)));
        var sandbag = Props.Cloth(Props.Khaki);
        for (int i = -3; i <= 3; i++)
            for (int row = 0; row < 3; row++)
                n.AddChild(Props.SolidBox(new Vector3(2.2f, 0.38f, 0.8f), sandbag, Layers.Buildings, new Vector3(i * 2.3f + (row % 2) * 0.5f, 0.19f + row * 0.36f, size.Z / 2 + 1f)));
        Crate(n, new Vector3(0.9f, 0.9f, 0.9f), new Vector3(-size.X / 2 + 2f, 0, size.Z / 2 - 1f));
        Crate(n, new Vector3(0.9f, 0.9f, 0.9f), new Vector3(-size.X / 2 + 3.1f, 0, size.Z / 2 - 1.2f));
        var post = new Node3D { Position = new Vector3(3f, 0, size.Z / 2 - 1f) };
        post.AddChild(Props.Cylinder(0.07f, 3.2f, Props.Planks(Props.Wood), new Vector3(0, 1.6f, 0), -1, 6));
        post.AddChild(Props.Lantern(new Vector3(0, 2.9f, 0.2f), 1.6f, 12f));
        n.AddChild(post);
        n.AddChild(Props.SignBoard(string.IsNullOrEmpty(b.Sign) ? "GARRISON" : b.Sign, new Vector3(0, 2.4f, size.Z / 2 + 1.8f), 5.5f, Props.Khaki, new Color(0.15f, 0.12f, 0.1f)));
    }

    private static void Kraal(Node3D n, Vector3 size)
    {
        // dry-stone enclosure for stock, gap for the gate
        float r = size.X / 2;
        int posts = 18;
        var stone = Props.Rock(new Color(0.75f, 0.7f, 0.62f));
        for (int i = 1; i < posts; i++)
        {
            float a = i / (float)posts * Mathf.Tau;
            var seg = Props.SolidBox(new Vector3(r * Mathf.Tau / posts + 0.2f, 1.2f, 0.6f), stone, Layers.Props, new Vector3(Mathf.Cos(a) * r, 0.6f, Mathf.Sin(a) * r));
            seg.Rotation = new Vector3(0, -a + Mathf.Pi / 2, 0);
            n.AddChild(seg);
        }
        for (int s = 0; s < 2; s++)
        {
            float a = (s == 0 ? 0.5f : (posts - 0.5f)) / posts * Mathf.Tau;
            n.AddChild(Props.Cylinder(0.12f, 1.8f, Props.Planks(Props.DarkWood), new Vector3(Mathf.Cos(a) * r, 0.9f, Mathf.Sin(a) * r), -1, 6));
        }
    }

    private static void Well(Node3D n)
    {
        var stone = Props.StoneBlocks(PlinthStone);
        n.AddChild(Props.Cylinder(1.2f, 1.0f, stone, new Vector3(0, 0.5f, 0), -1, 16));
        n.AddChild(Props.Cylinder(0.9f, 1.05f, Props.Material(new Color(0.08f, 0.12f, 0.16f)), new Vector3(0, 0.52f, 0), -1, 16));
        for (int i = -1; i <= 1; i += 2) n.AddChild(Props.Box(new Vector3(0.15f, 2.4f, 0.15f), Props.Planks(Props.Wood), new Vector3(i * 1.0f, 1.9f, 0)));
        n.AddChild(Props.Prism(new Vector3(2.8f, 0.8f, 1.8f), Props.Corrugated(Props.CorrugatedGrey), new Vector3(0, 3.3f, 0)));
        n.AddChild(Props.Cylinder(0.06f, 2.2f, Props.Planks(Props.DarkWood), new Vector3(0, 2.6f, 0), -1, 6).WithRotation(new Vector3(0, 0, Mathf.Pi / 2)));
        n.AddChild(Props.Box(new Vector3(0.02f, 1.3f, 0.02f), Props.Material(new Color(0.6f, 0.5f, 0.35f)), new Vector3(0, 1.95f, 0)));
        n.AddChild(Props.Cylinder(0.16f, 0.26f, Props.Material(Props.Iron, 0.5f, 0.6f), new Vector3(0, 1.3f, 0), 0.13f, 10));
        var body = new StaticBody3D { CollisionLayer = Layers.Props, CollisionMask = 0, Position = new Vector3(0, 0.5f, 0) };
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.2f, Height = 1f } });
        n.AddChild(body);
    }

    private static void HitchRail(Node3D n, Vector3 size)
    {
        var wood = Props.Planks(Props.DarkWood);
        n.AddChild(Props.Box(new Vector3(size.X, 0.12f, 0.12f), wood, new Vector3(0, 1.1f, 0)));
        for (int i = -1; i <= 1; i += 2) n.AddChild(Props.Box(new Vector3(0.15f, 1.1f, 0.15f), wood, new Vector3(i * size.X / 2, 0.55f, 0)));
        Trough(n, new Vector3(size.X / 2 + 1.4f, 0, 0));
    }

    public static void Blockhouse(Node3D n, Vector3 size)
    {
        // Rice-pattern corrugated-iron blockhouse on a stone base, the Boer War railway guard post
        float r = Mathf.Max(2.2f, size.X / 2);
        var iron = Props.Corrugated(Props.CorrugatedGrey);
        n.AddChild(Props.Cylinder(r + 0.4f, 1.4f, Props.StoneBlocks(PlinthStone), new Vector3(0, 0.7f, 0), -1, 16));
        n.AddChild(Props.Cylinder(r, 4.2f, iron, new Vector3(0, 1.4f + 2.1f, 0), -1, 16));
        n.AddChild(Props.Cylinder(r + 0.7f, 1.5f, Props.Corrugated(RoofIronDark), new Vector3(0, 1.4f + 4.2f + 0.75f, 0), 0.1f, 16));
        n.AddChild(Props.Cylinder(r + 0.45f, 0.16f, Props.Corrugated(RoofIronDark), new Vector3(0, 1.4f + 4.2f, 0), -1, 16));
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.Tau;
            var slit = Props.Box(new Vector3(0.6f, 0.25f, 0.1f), Props.Material(new Color(0.05f, 0.05f, 0.05f)), new Vector3(Mathf.Cos(a) * (r + 0.02f), 4.2f, Mathf.Sin(a) * (r + 0.02f)));
            slit.Rotation = new Vector3(0, -a + Mathf.Pi / 2, 0);
            n.AddChild(slit);
        }
        Door(n, new Vector3(0, 1.4f + 1.05f, r + 0.02f), 0.9f, 1.9f);
        n.AddChild(Props.Lantern(new Vector3(0.8f, 3.6f, r + 0.1f), 1.3f, 9f));
        var body = new StaticBody3D { CollisionLayer = Layers.Buildings, CollisionMask = 0, Position = new Vector3(0, 3.5f, 0) };
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = r + 0.4f, Height = 7f } });
        n.AddChild(body);
        // barbed wire on posts
        var wire = Props.Material(new Color(0.35f, 0.33f, 0.3f), 0.6f, 0.6f);
        var postMat = Props.Planks(Props.Wood);
        const int posts = 8;
        for (int i = 0; i < posts; i++)
        {
            float a0 = i / (float)posts * Mathf.Tau, a1 = (i + 1) / (float)posts * Mathf.Tau;
            var p0 = new Vector3(Mathf.Cos(a0) * (r + 5f), 0, Mathf.Sin(a0) * (r + 5f));
            var p1 = new Vector3(Mathf.Cos(a1) * (r + 5f), 0, Mathf.Sin(a1) * (r + 5f));
            n.AddChild(Props.Box(new Vector3(0.12f, 1.5f, 0.12f), postMat, p0 + new Vector3(0, 0.75f, 0)));
            if (i == posts - 1) continue; // gap for the path
            Beam(n, p0 + new Vector3(0, 0.55f, 0), p1 + new Vector3(0, 0.55f, 0), 0.025f, wire);
            Beam(n, p0 + new Vector3(0, 1.0f, 0), p1 + new Vector3(0, 1.0f, 0), 0.025f, wire);
            Beam(n, p0 + new Vector3(0, 1.4f, 0), p1 + new Vector3(0, 1.4f, 0), 0.025f, wire);
        }
    }

    // ------------------------------------------------------------------ small props

    /// <summary>A post-and-rail fence of Size.X metres along the local x axis.</summary>
    private static void Fence(Node3D n, Vector3 size)
    {
        var wood = Props.Planks(Props.DarkWood);
        int posts = Mathf.Max(2, Mathf.RoundToInt(size.X / 2.5f) + 1);
        for (int i = 0; i < posts; i++)
        {
            float x = -size.X / 2 + i * size.X / (posts - 1);
            n.AddChild(Props.Box(new Vector3(0.14f, 1.2f, 0.14f), wood, new Vector3(x, 0.6f, 0)));
            if (i > 0)
            {
                float x0 = -size.X / 2 + (i - 1) * size.X / (posts - 1);
                Beam(n, new Vector3(x0, 0.55f, 0), new Vector3(x, 0.55f, 0), 0.07f, wood);
                Beam(n, new Vector3(x0, 1.05f, 0), new Vector3(x, 1.05f, 0), 0.07f, wood);
            }
        }
        var body = new StaticBody3D { CollisionLayer = Layers.Props, CollisionMask = 0, Position = new Vector3(0, 0.5f, 0) };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(size.X, 1.0f, 0.2f) } });
        n.AddChild(body);
    }

    private static void Barrels(Node3D n)
    {
        var wood = Props.Planks(Props.Wood);
        var band = Props.Material(Props.Iron, 0.5f, 0.7f);
        Vector3[] spots = { new(0, 0, 0), new(0.8f, 0, 0.3f), new(0.35f, 0, -0.65f) };
        foreach (var p in spots)
        {
            n.AddChild(Props.Cylinder(0.34f, 0.9f, wood, p + new Vector3(0, 0.45f, 0), -1, 12));
            n.AddChild(Props.Cylinder(0.355f, 0.05f, band, p + new Vector3(0, 0.2f, 0), -1, 12));
            n.AddChild(Props.Cylinder(0.355f, 0.05f, band, p + new Vector3(0, 0.72f, 0), -1, 12));
            var body = new StaticBody3D { CollisionLayer = Layers.Props, CollisionMask = 0, Position = p + new Vector3(0, 0.45f, 0) };
            body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.36f, Height = 0.9f } });
            n.AddChild(body);
        }
    }

    private static void Crate(Node3D n, Vector3 size, Vector3? at = null)
    {
        var p = at ?? Vector3.Zero;
        n.AddChild(Props.SolidBox(size, Props.Planks(Props.Wood), Layers.Props, p + new Vector3(0, size.Y / 2, 0)));
        n.AddChild(Props.Box(new Vector3(size.X + 0.02f, 0.06f, size.Z + 0.02f), Props.Planks(Props.DarkWood), p + new Vector3(0, size.Y - 0.1f, 0)));
    }

    private static void Trough(Node3D n, Vector3? at = null)
    {
        var p = at ?? Vector3.Zero;
        n.AddChild(Props.SolidBox(new Vector3(2.0f, 0.6f, 0.8f), Props.Planks(Props.DarkWood), Layers.Props, p + new Vector3(0, 0.3f, 0)));
        var water = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.28f, 0.3f), Roughness = 0.05f, Metallic = 0.4f };
        n.AddChild(Props.Box(new Vector3(1.8f, 0.04f, 0.6f), water, p + new Vector3(0, 0.52f, 0)));
    }

    private static void Wagon(Node3D n)
    {
        var wood = Props.Planks(Props.Wood);
        var dark = Props.Planks(Props.DarkWood);
        var iron = Props.Material(Props.Iron, 0.5f, 0.7f);
        n.AddChild(Props.SolidBox(new Vector3(2.2f, 0.9f, 4.6f), wood, Layers.Props, new Vector3(0, 1.35f, 0)));
        n.AddChild(Props.Box(new Vector3(2.3f, 0.12f, 4.7f), dark, new Vector3(0, 0.95f, 0)));
        n.AddChild(Props.Prism(new Vector3(2.4f, 1.1f, 4.4f), Props.Cloth(Props.Canvas), new Vector3(0, 2.35f, 0)));
        for (int i = -1; i <= 1; i += 2)
        {
            n.AddChild(Props.Box(new Vector3(2.7f, 0.1f, 0.1f), iron, new Vector3(0, 0.85f, i * 1.6f)));
            for (int s = -1; s <= 1; s += 2)
            {
                var wheel = Props.Cylinder(i < 0 ? 0.65f : 0.85f, 0.12f, dark, new Vector3(s * 1.3f, i < 0 ? 0.65f : 0.85f, i * 1.6f), -1, 14);
                wheel.Rotation = new Vector3(0, 0, Mathf.Pi / 2);
                n.AddChild(wheel);
                n.AddChild(Props.Cylinder(0.12f, 0.16f, iron, new Vector3(s * 1.32f, i < 0 ? 0.65f : 0.85f, i * 1.6f), -1, 8).WithRotation(new Vector3(0, 0, Mathf.Pi / 2)));
            }
        }
        // disselboom (shaft) out the front
        Beam(n, new Vector3(0, 0.9f, -2.3f), new Vector3(0, 0.75f, -5.4f), 0.14f, dark);
        n.AddChild(Props.Box(new Vector3(1.6f, 0.1f, 0.1f), dark, new Vector3(0, 0.8f, -4.6f)));
    }
}
