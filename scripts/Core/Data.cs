using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Veld.Core;

// Everything the world is made of is described by JSON in res://data/maps/<map>/. These classes mirror
// those files. Positions are [x, z] in metres from the map centre (x east, z south); height comes from the terrain.
// A map is either procedural (Driefontein: noise terrain + authored towns) or real (Chatsworth: a heightmap
// plus roads, areas, building footprints, lines and points of interest converted from OpenStreetMap).

public sealed class WorldData
{
    public string Name { get; set; } = "";
    /// <summary>"1901" or "modern": changes outfits, money, the clock and some wording.</summary>
    public string Era { get; set; } = "1901";
    /// <summary>"karoo" or "subtropical": ground and plant colours.</summary>
    public string Biome { get; set; } = "karoo";
    public string Intro { get; set; } = "";
    public string Attribution { get; set; } = "";
    public int Seed { get; set; } = 1900;
    public float SizeMeters { get; set; } = 1600;
    public float CellSize { get; set; } = 4;
    public float HeightScale { get; set; } = 55;
    /// <summary>Heightmap file in the map folder; empty for procedural terrain.</summary>
    public string Heightmap { get; set; } = "";
    /// <summary>Aerial photo (PNG/JPG in the map folder) draped over the ground; empty for none.</summary>
    public string Ortho { get; set; } = "";
    public LatLonBounds OrthoBounds { get; set; } = new();
    /// <summary>How much of the aerial photo shows through the procedural ground, 0..1.</summary>
    public float OrthoMix { get; set; } = 0.85f;
    public OriginData Origin { get; set; } = new();
    public float[] PlayerSpawn { get; set; } = { 0, 0 };
    public float PlayerSpawnFacing { get; set; } = 0;
    public float StartHour { get; set; } = 7.5f;
    public float DayLengthSeconds { get; set; } = 720;
    public List<RoadData> Roads { get; set; } = new();
    public RailwayData Railway { get; set; } = new();
    public VegetationData Vegetation { get; set; } = new();
    public bool HorseEnabled { get; set; } = true;
    public string HorseSpawnTown { get; set; } = "";
    public float[] HorseSpawnOffset { get; set; } = { 0, 0 };
    public string HorseName { get; set; } = "Vos";
    /// <summary>Weapon ids the player starts with, in hotkey order (1, 2).</summary>
    public List<string> PlayerWeapons { get; set; } = new() { "mauser", "webley" };
    /// <summary>Metres between street lights on residential roads; 0 for none.</summary>
    /// <summary>Starting money in pence (1901) or cents (modern).</summary>
    public int StartMoney { get; set; } = 720;
    public float StreetLightSpacing { get; set; } = 0;
    public int ParkedCars { get; set; } = 0;
    public List<AreaData> Areas { get; set; } = new();
    public List<FootprintData> Buildings { get; set; } = new();
    public List<LineData> Lines { get; set; } = new();
    public List<PoiData> Pois { get; set; } = new();

    public bool IsModern => Era == "modern";
}

public sealed class RoadData
{
    public string Name { get; set; } = "";
    /// <summary>OpenStreetMap highway kind (residential, tertiary, service, footway...). Empty for authored dirt roads.</summary>
    public string Kind { get; set; } = "";
    public float Width { get; set; } = 6;
    public string Surface { get; set; } = "";
    public bool Lit { get; set; } = false;
    public List<float[]> Points { get; set; } = new();
}

public sealed class RailwayData
{
    public List<float[]> Points { get; set; } = new();
    public bool Loop { get; set; } = true;
    public float BlockhouseSpacing { get; set; } = 420;
    public float TrainSpeed { get; set; } = 14;
    public int Wagons { get; set; } = 3;
}

public sealed class VegetationData
{
    public int Trees { get; set; } = 2600;
    public int Rocks { get; set; } = 2400;
    public int Grass { get; set; } = 140000;
    public int Shrubs { get; set; } = 7000;
    public int Mounds { get; set; } = 300;
}

/// <summary>A polygon of land use: pitch, park, grass, wood, parking, water, school...</summary>
public sealed class AreaData
{
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public string Sport { get; set; } = "";
    public List<float[]> Polygon { get; set; } = new();
}

public sealed class LatLonBounds
{
    public double South { get; set; }
    public double West { get; set; }
    public double North { get; set; }
    public double East { get; set; }
    public bool IsSet => North > South && East > West;
}

public sealed class OriginData
{
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double MPerDegLat { get; set; } = 111320.0;
    public double MPerDegLon { get; set; } = 96500.0;
}

/// <summary>A building outline from map data.</summary>
public sealed class FootprintData
{
    public long Id { get; set; }
    public string Kind { get; set; } = "yes";
    public string Name { get; set; } = "";
    public int Levels { get; set; } = 0;
    public List<float[]> Polygon { get; set; } = new();
    public string Roof { get; set; } = "";
    public string Use { get; set; } = "";
    // ---- per-house overrides (overrides.json or hand-edited here)
    /// <summary>"brick", "plaster", or a plaster colour like "#f2d9c8".</summary>
    public string Wall { get; set; } = "";
    /// <summary>"tile", "iron" or "flat".</summary>
    public string RoofKind { get; set; } = "";
    /// <summary>Roof colour like "#8a3a2a".</summary>
    public string RoofColor { get; set; } = "";
    /// <summary>A photo of the street-facing wall (path in the map folder); it replaces the generated front wall.</summary>
    public string Facade { get; set; } = "";
    /// <summary>A 3D model (.glb or .tscn, path in the map folder) used instead of the generated building.</summary>
    public string Model { get; set; } = "";
    public float ModelYaw { get; set; } = 0f;
    public float ModelScale { get; set; } = 1f;
}

/// <summary>overrides.json: hand or tool edits for specific buildings, keyed by OpenStreetMap id.</summary>
public sealed class OverridesData
{
    public Dictionary<string, FootprintOverride> Buildings { get; set; } = new();
}

public sealed class FootprintOverride
{
    public int? Levels { get; set; }
    public string? Kind { get; set; }
    public string? Wall { get; set; }
    public string? RoofKind { get; set; }
    public string? RoofColor { get; set; }
    public string? Facade { get; set; }
    public string? Model { get; set; }
    public float? ModelYaw { get; set; }
    public float? ModelScale { get; set; }
}

/// <summary>Fences, walls, streams, rivers, power lines.</summary>
public sealed class LineData
{
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public List<float[]> Points { get; set; } = new();
}

public sealed class PoiData
{
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public float[] Position { get; set; } = { 0, 0 };
}

public sealed class HeightmapData
{
    public int Cols { get; set; }
    public int Rows { get; set; }
    public float StepM { get; set; } = 10;
    public float OriginX { get; set; }
    public float OriginZ { get; set; }
    public float SeaLevelOffset { get; set; }
    public float[] Heights { get; set; } = System.Array.Empty<float>();
}

public sealed class TownData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float[] Position { get; set; } = { 0, 0 };
    public float Rotation { get; set; } = 0;
    public float ClearRadius { get; set; } = 110;
    public List<BuildingData> Buildings { get; set; } = new();
}

public sealed class BuildingData
{
    /// <summary>house, shop, hotel, church, courthouse, smithy, station, barracks, tent, farmhouse, kraal, well, hitchrail, fence, barrels, crate, trough, wagon</summary>
    public string Type { get; set; } = "house";
    public string Sign { get; set; } = "";
    public float[] Offset { get; set; } = { 0, 0 };
    public float Rotation { get; set; } = 0;
    public float[] Size { get; set; } = { 8, 4, 7 };
    public string Color { get; set; } = "";
}

public sealed class NpcData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>civilian, british (police in the modern era), boer, outlaw</summary>
    public string Faction { get; set; } = "civilian";
    public string Role { get; set; } = "";
    public float[]? Position { get; set; }
    public string Town { get; set; } = "";
    public float[] Offset { get; set; } = { 0, 0 };
    public string Dialogue { get; set; } = "";
    public List<float[]> Patrol { get; set; } = new();
    public string Weapon { get; set; } = "";
    public bool Hostile { get; set; } = false;
    public string Group { get; set; } = "";
    public float WanderRadius { get; set; } = 12;
}

public sealed class DialogueData
{
    public string Speaker { get; set; } = "";
    public string Start { get; set; } = "start";
    public Dictionary<string, DialogueNode> Nodes { get; set; } = new();
}

public sealed class DialogueNode
{
    public string Text { get; set; } = "";
    public List<DialogueChoice> Choices { get; set; } = new();
}

public sealed class DialogueChoice
{
    public string Text { get; set; } = "";
    public string Next { get; set; } = "";
    /// <summary>start_mission:&lt;id&gt;, talk_done, give_money:&lt;n&gt;, take_money:&lt;n&gt;, give_ammo:&lt;weapon&gt;:&lt;count&gt;, heal, sleep:&lt;hours&gt;, clear_wanted:&lt;n&gt;, end</summary>
    public string Action { get; set; } = "";
    /// <summary>mission_available:&lt;id&gt;, mission_active:&lt;id&gt;, mission_step:&lt;id&gt;:&lt;n&gt;, mission_done:&lt;id&gt;, wanted, not_wanted, money:&lt;n&gt;</summary>
    public string Condition { get; set; } = "";
}

public sealed class MissionData
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Giver { get; set; } = "";
    public int RewardPence { get; set; } = 0;
    public List<MissionStep> Steps { get; set; } = new();
    public List<NpcData> Spawns { get; set; } = new();
    public List<string> Requires { get; set; } = new();
}

public sealed class MissionStep
{
    /// <summary>talk, goto, kill_group, return</summary>
    public string Type { get; set; } = "goto";
    public string Objective { get; set; } = "";
    public string Target { get; set; } = "";
    public float[] Position { get; set; } = { 0, 0 };
    public float Radius { get; set; } = 25;
    public string Group { get; set; } = "";
}

public sealed class WeaponData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>rifle (one shot per click) or revolver (fires while held; pistols too)</summary>
    public string Kind { get; set; } = "rifle";
    public int Damage { get; set; } = 50;
    public int MagSize { get; set; } = 5;
    public float RateOfFire { get; set; } = 1.0f;
    public float ReloadTime { get; set; } = 2.5f;
    public float SpreadDegrees { get; set; } = 0.6f;
    public float HipSpreadDegrees { get; set; } = 3.5f;
    public float Range { get; set; } = 300;
    public int ReserveAmmo { get; set; } = 40;
}

public sealed class GameData
{
    public string MapId { get; set; } = "";
    public string MapFolder { get; set; } = "";
    public int OverridesApplied { get; set; }
    public WorldData World { get; set; } = new();
    public HeightmapData? Heightmap { get; set; }
    public List<TownData> Towns { get; set; } = new();
    public List<NpcData> Npcs { get; set; } = new();
    public Dictionary<string, DialogueData> Dialogues { get; set; } = new();
    public List<MissionData> Missions { get; set; } = new();
    public Dictionary<string, WeaponData> Weapons { get; set; } = new();

    public TownData? Town(string id) => Towns.Find(t => t.Id == id);
    public MissionData? Mission(string id) => Missions.Find(m => m.Id == id);
}

public sealed class ConfigData
{
    public string Map { get; set; } = "driefontein-1901";
}

public static class DataLoader
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static GameData LoadAll()
    {
        var config = Load<ConfigData>("res://data/config.json");
        foreach (var a in OS.GetCmdlineUserArgs())
            if (a.StartsWith("--map=")) config.Map = a.Substring(6);
        string folder = $"res://data/maps/{config.Map}/";
        var data = new GameData
        {
            MapId = config.Map,
            World = Load<WorldData>(folder + "world.json"),
            Towns = LoadOptional<List<TownData>>(folder + "towns.json"),
            Npcs = LoadOptional<List<NpcData>>(folder + "npcs.json"),
            Dialogues = LoadOptional<Dictionary<string, DialogueData>>(folder + "dialogue.json"),
            Missions = LoadOptional<List<MissionData>>(folder + "missions.json"),
            Weapons = LoadOptional<Dictionary<string, WeaponData>>(folder + "weapons.json"),
        };
        if (!string.IsNullOrEmpty(data.World.Heightmap))
            data.Heightmap = Load<HeightmapData>(folder + data.World.Heightmap);
        data.MapFolder = folder;
        // per-building overrides
        if (FileAccess.FileExists(folder + "overrides.json"))
        {
            var over = Load<OverridesData>(folder + "overrides.json");
            int applied = 0;
            foreach (var b in data.World.Buildings)
            {
                if (b.Id == 0 || !over.Buildings.TryGetValue(b.Id.ToString(), out var o)) continue;
                if (o.Levels.HasValue) b.Levels = o.Levels.Value;
                if (o.Kind != null) b.Kind = o.Kind;
                if (o.Wall != null) b.Wall = o.Wall;
                if (o.RoofKind != null) b.RoofKind = o.RoofKind;
                if (o.RoofColor != null) b.RoofColor = o.RoofColor;
                if (o.Facade != null) b.Facade = o.Facade;
                if (o.Model != null) b.Model = o.Model;
                if (o.ModelYaw.HasValue) b.ModelYaw = o.ModelYaw.Value;
                if (o.ModelScale.HasValue) b.ModelScale = o.ModelScale.Value;
                applied++;
            }
            data.OverridesApplied = applied;
            GD.Print($"overrides.json: {applied} buildings customised");
        }
        Money.Modern = data.World.IsModern;
        return data;
    }

    public static T LoadOptional<T>(string path) where T : new() => FileAccess.FileExists(path) ? Load<T>(path) : new T();

    public static T Load<T>(string path) where T : new()
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"Data file missing: {path} ({FileAccess.GetOpenError()})");
            return new T();
        }
        var text = file.GetAsText();
        try
        {
            return JsonSerializer.Deserialize<T>(text, Json) ?? new T();
        }
        catch (JsonException ex)
        {
            GD.PushError($"Bad JSON in {path}: {ex.Message}");
            return new T();
        }
    }

    public static Vector2 XZ(float[]? a) => a == null || a.Length < 2 ? Vector2.Zero : new Vector2(a[0], a[1]);
}

/// <summary>
/// Money. In 1901 it is pre-decimal: 12 pence to a shilling, 20 shillings to a pound, stored as pence.
/// In the modern era the same integer is cents and prints as rands.
/// </summary>
public static class Money
{
    public const int Pound = 240;
    public const int Shilling = 12;
    public static bool Modern { get; set; }

    public static string Format(int amount)
    {
        bool neg = amount < 0;
        amount = System.Math.Abs(amount);
        if (Modern) return (neg ? "-" : "") + $"R{amount / 100}.{amount % 100:00}";
        int pounds = amount / Pound;
        int shillings = (amount % Pound) / Shilling;
        int d = amount % Shilling;
        var parts = new List<string>();
        if (pounds > 0) parts.Add($"£{pounds}");
        if (shillings > 0) parts.Add($"{shillings}s");
        if (d > 0 || parts.Count == 0) parts.Add($"{d}d");
        return (neg ? "-" : "") + string.Join(" ", parts);
    }
}
