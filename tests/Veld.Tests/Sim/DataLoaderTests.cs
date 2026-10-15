using System.Collections.Generic;
using Veld.Sim;
using Xunit;

namespace Veld.Tests.Sim;

/// <summary>In the "era flags" collection: LoadAll sets Money.Modern from the map's era.</summary>
[Collection("era flags")]
public class DataLoaderTests
{
    private sealed class FakeFiles : IDataFiles
    {
        public Dictionary<string, string> Files { get; } = new();
        public bool Exists(string path) => Files.ContainsKey(path);
        public string? ReadText(string path) => Files.TryGetValue(path, out var text) ? text : null;
    }

    private readonly FakeFiles _files = new();
    private readonly List<string> _info = new();
    private readonly List<string> _errors = new();

    private GameData LoadAll(string? mapOverride = null) => DataLoader.LoadAll(_files, _info.Add, _errors.Add, mapOverride);

    [Fact]
    public void Loads_the_configured_map_from_the_files_it_is_given()
    {
        _files.Files["res://data/config.json"] = "{ \"map\": \"montford\" }";
        _files.Files["res://data/maps/montford/world.json"] = "{ \"name\": \"Montford\", \"era\": \"modern\" }";

        var data = LoadAll();

        Assert.Equal("montford", data.MapId);
        Assert.Equal("res://data/maps/montford/", data.MapFolder);
        Assert.Equal("Montford", data.World.Name);
        Assert.True(data.World.IsModern);
        Assert.Empty(data.Npcs);
        Assert.Empty(_errors);
    }

    [Fact]
    public void The_map_argument_overrides_the_config()
    {
        _files.Files["res://data/config.json"] = "{ \"map\": \"montford\" }";
        _files.Files["res://data/maps/other/world.json"] = "{ \"name\": \"Other\" }";

        var data = LoadAll("other");

        Assert.Equal("other", data.MapId);
        Assert.Equal("Other", data.World.Name);
        Assert.Empty(_errors);
    }

    [Fact]
    public void A_missing_required_file_gives_an_empty_object_and_one_error_naming_it()
    {
        _files.Files["res://data/config.json"] = "{ \"map\": \"montford\" }";

        var data = LoadAll();

        Assert.Equal("", data.World.Name);
        Assert.Equal(new[] { "Data file missing: res://data/maps/montford/world.json" }, _errors);
    }

    [Fact]
    public void Bad_json_gives_an_empty_object_and_one_error_naming_the_file()
    {
        _files.Files["res://data/config.json"] = "{ \"map\": \"montford\" }";
        _files.Files["res://data/maps/montford/world.json"] = "{ not json";

        var data = LoadAll();

        Assert.Equal("", data.World.Name);
        var error = Assert.Single(_errors);
        Assert.StartsWith("Bad JSON in res://data/maps/montford/world.json: ", error);
    }
}
