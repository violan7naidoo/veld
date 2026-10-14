using Godot;
using Veld.Audio;
using Veld.Characters;
using Veld.UI;
using Veld.World;

namespace Veld.Core;

/// <summary>
/// Root of the main scene. Builds the world from data, spawns the player, the horse and the NPCs,
/// and creates the UI. Open scenes/Main.tscn in the editor to see it; everything underneath is
/// created at runtime from the JSON in res://data so you can edit the world without touching scenes.
/// </summary>
public partial class Main : Node3D
{
    public WorldBuilder World { get; private set; } = null!;
    public DayNight DayNight { get; private set; } = null!;
    public Player Player { get; private set; } = null!;
    public CameraRig Camera { get; private set; } = null!;
    public Hud Hud { get; private set; } = null!;
    public DialogueBox DialogueBox { get; private set; } = null!;
    public PauseMenu PauseMenu { get; private set; } = null!;
    public Sfx Sfx { get; private set; } = null!;
    public Weather Weather { get; private set; } = null!;
    public WorldMap WorldMap { get; private set; } = null!;

    public override void _Ready()
    {
        InputSetup.Configure();
        var game = Game.Instance;
        game.Bind(this);

        Sfx = new Sfx();
        AddChild(Sfx);

        DayNight = new DayNight { Name = "DayNight" };
        DayNight.Configure(game.Data.World.StartHour, game.Data.World.DayLengthSeconds);
        AddChild(DayNight);
        Weather = new Weather { Name = "Weather" };
        Weather.Configure(game.Data.World.Biome, game.Data.World.Seed);
        AddChild(Weather);
        DayNight.Weather = Weather;

        World = new WorldBuilder { Name = "World" };
        AddChild(World);
        World.Build(game.Data);
        AddressIndex.Build(game.Data.World);

        Camera = new CameraRig { Name = "CameraRig" };
        AddChild(Camera);

        Player = GD.Load<PackedScene>("res://scenes/Player.tscn").Instantiate<Player>();
        Player.Name = "Player";
        var spawn = DataLoader.XZ(game.Data.World.PlayerSpawn);
        Player.Position = new Vector3(spawn.X, World.Terrain.GetHeight(spawn.X, spawn.Y) + 1.0f, spawn.Y);
        Player.Rotation = new Vector3(0, Mathf.DegToRad(game.Data.World.PlayerSpawnFacing), 0);
        AddChild(Player);
        Camera.Target = Player;
        Camera.Yaw = Mathf.DegToRad(game.Data.World.PlayerSpawnFacing);
        Player.Camera = Camera;
        World.Plates?.Follow(Player);

        World.SpawnHorse(game.Data);
        World.SpawnPlayerCar(game.Data);
        World.SpawnNpcs(game.Data);
        MapImage.Build(World, game.Data);

        var ui = new CanvasLayer { Name = "UI", ProcessMode = ProcessModeEnum.Always };
        AddChild(ui);
        Hud = new Hud { Name = "Hud" };
        ui.AddChild(Hud);
        DialogueBox = new DialogueBox { Name = "DialogueBox" };
        ui.AddChild(DialogueBox);
        WorldMap = new WorldMap { Name = "WorldMap" };
        ui.AddChild(WorldMap);
        PauseMenu = new PauseMenu { Name = "PauseMenu" };
        ui.AddChild(PauseMenu);
        ui.AddChild(new UiInput { Name = "UiInput" });

        Input.MouseMode = Input.MouseModeEnum.Captured;
        game.Notify(string.IsNullOrEmpty(game.Data.World.Intro) ? "Driefontein, Cape Colony. April 1901. Press E to talk, H to whistle for your horse, Esc for the menu." : game.Data.World.Intro);

        // `godot --path . -- --smoke [--shot=C:/path/prefix]` runs the automated smoke test (see DevSmoke.cs)
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--smoke") >= 0) AddChild(new DevSmoke { Name = "DevSmoke" });
        // `-- --carstress[=N]` gets into N parked cars in turn and reports any that flip or sink (see DevCarStress.cs)
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--carstress")) { AddChild(new DevCarStress { Name = "DevCarStress" }); break; }
    }

    /// <summary>Menu-level keys. Called from UiInput, which keeps running while the game is paused.</summary>
    public void HandleUiInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pause"))
        {
            if (DialogueBox.IsOpen) DialogueBox.Close();
            else if (WorldMap.IsOpen) WorldMap.Toggle();
            else PauseMenu.Toggle();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("map") && !DialogueBox.IsOpen && !PauseMenu.IsOpen)
        {
            WorldMap.Toggle();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("map_time_skip") && !Game.Instance.InputLocked)
        {
            DayNight.Hour = (DayNight.Hour + 2f) % 24f;
            Game.Instance.Notify($"Time passes. It is now {DayNight.TimeText}.");
        }
        else if (@event.IsActionPressed("screenshot"))
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("user://screenshots"));
            var path = $"user://screenshots/veld-{Time.GetDatetimeStringFromSystem().Replace(':', '-')}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            Game.Instance.Notify("Screenshot saved to " + ProjectSettings.GlobalizePath(path));
        }
    }
}

/// <summary>Lives under the always-processing UI layer so Esc and M work while the game is paused.</summary>
public partial class UiInput : Node
{
    public override void _UnhandledInput(InputEvent @event) => Game.Instance.Main?.HandleUiInput(@event);
}
