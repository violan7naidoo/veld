using System.Collections.Generic;
using Godot;
using Veld.Characters;
using Veld.Core;
using Veld.Gameplay;
using Veld.Sim;

namespace Veld.World;

/// <summary>Assembles terrain, roads, towns, railway, vegetation and NPCs from GameData.</summary>
public partial class WorldBuilder : Node3D
{
    public Terrain Terrain { get; private set; } = null!;
    public Railway? Railway { get; private set; }
    public Vegetation? Vegetation { get; private set; }
    public AddressPlates? Plates { get; private set; }
    public List<Vehicle> Vehicles { get; } = new();
    public Vehicle? PlayerCar { get; private set; }
    public Horse? Horse { get; private set; }
    public List<Npc> Npcs { get; } = new();
    /// <summary>Ids of characters who have died; respected on spawn and on load.</summary>
    public HashSet<string> DeadNpcs { get; } = new();
    public Dictionary<string, TownData> Towns { get; } = new();
    public Dictionary<string, Vector3> TownCenters { get; } = new();

    private GameData _data = null!;
    private Main _main = null!;
    private Simulation _sim = null!;
    private Notifier _notifier = null!;
    private readonly PackedScene _npcScene = GD.Load<PackedScene>("res://scenes/Npc.tscn");
    private readonly PackedScene _horseScene = GD.Load<PackedScene>("res://scenes/Horse.tscn");

    /// <summary>Call before Build; every character and vehicle this builder spawns is given these.</summary>
    public void Init(Main main, Simulation sim, Notifier notifier)
    {
        _main = main;
        _sim = sim;
        _notifier = notifier;
    }

    public void Build(GameData data)
    {
        _data = data;
        var world = data.World;

        var flats = new List<Terrain.Flatten>();
        foreach (var t in data.Towns)
        {
            Towns[t.Id] = t;
            flats.Add(new Terrain.Flatten(DataLoader.XZ(t.Position), t.ClearRadius, 45f));
        }
        var strips = new List<Terrain.Strip>();
        foreach (var r in world.Roads)
        {
            var pts = new List<Vector2>();
            foreach (var p in r.Points) pts.Add(DataLoader.XZ(p));
            if (pts.Count < 2) continue;
            bool real = !string.IsNullOrEmpty(r.Kind);
            bool foot = r.Kind is "footway" or "path" or "steps" or "cycleway";
            float half = real ? r.Width / 2f + (foot ? 0.3f : 0.8f) : r.Width / 2f;
            float blend = real ? (foot ? 2.5f : r.Kind == "service" ? 3f : 6f) : 10f;
            strips.Add(new Terrain.Strip(pts, half, blend, real && foot ? Terrain.Surface.Paved : Terrain.Surface.Road, false));
        }
        if (world.Railway.Points.Count > 1)
        {
            var pts = new List<Vector2>();
            foreach (var p in world.Railway.Points) pts.Add(DataLoader.XZ(p));
            strips.Add(new Terrain.Strip(pts, 3.2f, 14f, Terrain.Surface.Rail, world.Railway.Loop));
        }
        foreach (var l in world.Lines)
        {
            if (l.Kind is not ("stream" or "river" or "ditch")) continue;
            var pts = new List<Vector2>();
            foreach (var p in l.Points) pts.Add(DataLoader.XZ(p));
            if (pts.Count < 2) continue;
            bool river = l.Kind == "river";
            strips.Add(new Terrain.Strip(pts, river ? 2.5f : 1.0f, river ? 5f : 3f, Terrain.Surface.Water, false, river ? 1.5f : 0.8f));
        }
        var areas = new List<Terrain.AreaFill>();
        foreach (var a in world.Areas)
        {
            var poly = new List<Vector2>();
            foreach (var p in a.Polygon) poly.Add(DataLoader.XZ(p));
            if (poly.Count < 3) continue;
            switch (a.Kind)
            {
                case "pitch": case "stadium": case "track": case "sports_centre": areas.Add(new Terrain.AreaFill(poly, Terrain.Surface.Grass, true, 8f)); break;
                case "park": case "grass": case "meadow": case "grassland": case "garden": case "playground": case "recreation_ground": case "cemetery": areas.Add(new Terrain.AreaFill(poly, Terrain.Surface.Grass, false, 0f)); break;
                case "parking": areas.Add(new Terrain.AreaFill(poly, Terrain.Surface.Paved, true, 4f)); break;
                case "wood": case "forest": areas.Add(new Terrain.AreaFill(poly, Terrain.Surface.Wood, false, 0f)); break;
                case "water": case "swimming_pool": case "reservoir": areas.Add(new Terrain.AreaFill(poly, Terrain.Surface.Water, true, 3f)); break;
            }
        }
        var built = new List<List<Vector2>>();
        foreach (var b in world.Buildings)
        {
            var poly = new List<Vector2>();
            foreach (var p in b.Polygon) poly.Add(DataLoader.XZ(p));
            if (poly.Count >= 3) built.Add(poly);
        }

        Terrain = new Terrain { Name = "Terrain" };
        AddChild(Terrain);
        Terrain.Generate(world, data.Heightmap, flats, strips, areas, built, data.MapFolder);

        foreach (var t in data.Towns)
        {
            var node = TownBuilder.Build(t, Terrain);
            AddChild(node);
            TownCenters[t.Id] = node.GlobalPosition;
        }

        if (world.Railway.Points.Count > 1)
        {
            Railway = new Railway { Name = "Railway" };
            AddChild(Railway);
            Railway.Build(world.Railway, Terrain);
        }

        if (world.Roads.Exists(r => !string.IsNullOrEmpty(r.Kind)))
        {
            var roads = new RoadBuilder { Name = "Roads" };
            AddChild(roads);
            roads.Build(world, Terrain);
        }
        if (world.Buildings.Count > 0)
        {
            Plates = new AddressPlates { Name = "AddressPlates" };
            AddChild(Plates);
            var footprints = new FootprintBuilder { Name = "Buildings" };
            AddChild(footprints);
            footprints.Build(world, Terrain, data.MapFolder, Plates);
        }
        if (world.Areas.Count > 0 || world.Lines.Count > 0 || world.Pois.Count > 0)
        {
            var dressing = new Dressing { Name = "Dressing" };
            AddChild(dressing);
            dressing.Build(world, Terrain);
        }

        var veg = new Vegetation { Name = "Vegetation" };
        AddChild(veg);
        veg.Scatter(Terrain, world.Vegetation, world.Seed, world.Biome == "subtropical");
        Vegetation = veg;
    }

    /// <summary>World position for a data entry: explicit [x,z], or a town plus local offset (rotated by the town).</summary>
    public Vector3 Resolve(float[]? position, string town, float[] offset)
    {
        Vector2 xz;
        if (!string.IsNullOrEmpty(town) && Towns.TryGetValue(town, out var t))
        {
            var o = DataLoader.XZ(offset);
            float a = Mathf.DegToRad(t.Rotation);
            var rotated = new Vector2(o.X * Mathf.Cos(a) - o.Y * Mathf.Sin(a), o.X * Mathf.Sin(a) + o.Y * Mathf.Cos(a));
            xz = DataLoader.XZ(t.Position) + rotated;
        }
        else xz = DataLoader.XZ(position);
        return new Vector3(xz.X, Terrain.GetHeight(xz.X, xz.Y), xz.Y);
    }

    public Vector3 Ground(Vector2 xz) => new(xz.X, Terrain.GetHeight(xz.X, xz.Y), xz.Y);

    public void SpawnHorse(GameData data)
    {
        if (!data.World.HorseEnabled) return;
        Horse = _horseScene.Instantiate<Horse>();
        Horse.Name = "Horse";
        Horse.Init(_main, data, _notifier);
        var pos = Resolve(null, data.World.HorseSpawnTown, data.World.HorseSpawnOffset);
        if (string.IsNullOrEmpty(data.World.HorseSpawnTown)) pos = Resolve(data.World.PlayerSpawn, "", new[] { 0f, 0f }) + new Vector3(4, 0, 2);
        Horse.Position = pos + new Vector3(0, 1.2f, 0);
        AddChild(Horse);
    }

    /// <summary>The player's own car, parked on the road a few metres from the spawn point (modern maps only).</summary>
    public void SpawnPlayerCar(GameData data)
    {
        if (!data.World.IsModern) return;
        var spawn = DataLoader.XZ(data.World.PlayerSpawn);
        float yaw = Mathf.DegToRad(data.World.PlayerSpawnFacing);
        var forward = new Vector2(-Mathf.Sin(yaw), -Mathf.Cos(yaw));
        var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
        var at = spawn + forward * 7f + right * 2.2f;
        PlayerCar = AddVehicle("car", new Color(0.75f, 0.78f, 0.8f), new Vector3(at.X, Terrain.GetHeight(at.X, at.Y) + 0.6f, at.Y), yaw);
        PlayerCar.Name = "PlayerCar";
    }

    public Vehicle AddVehicle(string kind, Color paint, Vector3 position, float yaw)
    {
        var v = Vehicle.Create(kind, paint);
        v.Init(_main, _sim.Purse, _notifier);
        v.Position = position;
        v.Rotation = new Vector3(0, yaw, 0);
        AddChild(v);
        Vehicles.Add(v);
        return v;
    }

    public void SpawnNpcs(GameData data)
    {
        foreach (var n in data.Npcs) SpawnNpc(n);
        // two sentries at every blockhouse
        if (Railway != null)
        {
            int k = 0;
            foreach (var bh in Railway.BlockhousePositions)
            {
                for (int i = 0; i < 2; i++)
                {
                    var p = bh + new Vector3(i == 0 ? 6f : -6f, 0, 3f);
                    SpawnNpc(new NpcData
                    {
                        Id = $"sentry_{k}_{i}", Name = "Sentry", Faction = "british", Role = "sentry", Weapon = "lee_enfield",
                        Position = new[] { p.X, p.Z }, WanderRadius = 8f,
                        Patrol = new List<float[]> { new[] { bh.X + 7f, bh.Z + 5f }, new[] { bh.X - 7f, bh.Z + 5f }, new[] { bh.X - 7f, bh.Z - 5f }, new[] { bh.X + 7f, bh.Z - 5f } },
                    });
                }
                k++;
            }
        }
    }

    public Npc SpawnNpc(NpcData data)
    {
        var npc = _npcScene.Instantiate<Npc>();
        npc.Name = "Npc_" + data.Id;
        npc.Data = data;
        npc.Init(_main, _data, _sim.CreateBrain(data, npc), _sim.Dialogue);
        npc.Position = Resolve(data.Position, data.Town, data.Offset) + new Vector3(0, 1.0f, 0);
        AddChild(npc);
        Npcs.Add(npc);
        if (DeadNpcs.Contains(data.Id)) npc.MarkDeadSilently();
        return npc;
    }

    public Npc? FindNpc(string id) => Npcs.Find(n => n.Data.Id == id);

    public IEnumerable<Npc> Group(string group)
    {
        foreach (var n in Npcs) if (n.Data.Group == group) yield return n;
    }

    public void ApplyDeadNpcs()
    {
        foreach (var n in Npcs)
            if (DeadNpcs.Contains(n.Data.Id) && !n.IsDead) n.MarkDeadSilently();
    }
}
