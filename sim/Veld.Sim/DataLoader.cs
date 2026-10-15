using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Veld.Sim;

/// <summary>Reads the JSON under res://data through whatever IDataFiles it is given; paths stay res:// strings and the port owns what they mean.</summary>
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

    public static GameData LoadAll(IDataFiles files, Action<string> info, Action<string> error, string? mapOverride = null)
    {
        var config = Load<ConfigData>(files, "res://data/config.json", error);
        if (mapOverride != null) config.Map = mapOverride;
        string folder = $"res://data/maps/{config.Map}/";
        var data = new GameData
        {
            MapId = config.Map,
            World = Load<WorldData>(files, folder + "world.json", error),
            Towns = LoadOptional<List<TownData>>(files, folder + "towns.json", error),
            Npcs = LoadOptional<List<NpcData>>(files, folder + "npcs.json", error),
            Dialogues = LoadOptional<Dictionary<string, DialogueData>>(files, folder + "dialogue.json", error),
            Missions = LoadOptional<List<MissionData>>(files, folder + "missions.json", error),
            Weapons = LoadOptional<Dictionary<string, WeaponData>>(files, folder + "weapons.json", error),
        };
        if (!string.IsNullOrEmpty(data.World.Heightmap))
            data.Heightmap = Load<HeightmapData>(files, folder + data.World.Heightmap, error);
        data.MapFolder = folder;
        // per-building overrides
        if (files.Exists(folder + "overrides.json"))
        {
            var over = Load<OverridesData>(files, folder + "overrides.json", error);
            int applied = 0;
            foreach (var b in data.World.Buildings)
            {
                if (b.Id == 0 || !over.Buildings.TryGetValue(b.Id.ToString(), out var o)) continue;
                if (o.Levels.HasValue) b.Levels = o.Levels.Value;
                if (o.Kind != null) b.Kind = o.Kind;
                if (o.Number != null) b.Number = o.Number;
                if (o.Street != null) b.Street = o.Street;
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
            info($"overrides.json: {applied} buildings customised");
        }
        Money.Modern = data.World.IsModern;
        return data;
    }

    public static T LoadOptional<T>(IDataFiles files, string path, Action<string> error) where T : new() =>
        files.Exists(path) ? Load<T>(files, path, error) : new T();

    public static T Load<T>(IDataFiles files, string path, Action<string> error) where T : new()
    {
        var text = files.ReadText(path);
        if (text == null)
        {
            error($"Data file missing: {path}");
            return new T();
        }
        try
        {
            return JsonSerializer.Deserialize<T>(text, Json) ?? new T();
        }
        catch (JsonException ex)
        {
            error($"Bad JSON in {path}: {ex.Message}");
            return new T();
        }
    }

    public static Vector2 XZ(float[]? a) => a == null || a.Length < 2 ? Vector2.Zero : new Vector2(a[0], a[1]);
}
