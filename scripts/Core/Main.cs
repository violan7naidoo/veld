using Godot;
using Veld.Audio;
using Veld.Characters;
using Veld.Gameplay;
using Veld.UI;
using Veld.World;

namespace Veld.Core;

/// <summary>
/// Root of the main scene and the composition root. Loads the data and settings, creates the purse,
/// standing, notifier, UI state and save file, composes the mission, wanted and dialogue systems over
/// live adapters, then builds the world from data, spawns the player, the horse and the NPCs, and
/// creates the UI, handing each node what it needs. Open scenes/Main.tscn in the editor to see it;
/// everything underneath is created at runtime from the JSON in res://data.
/// </summary>
public partial class Main : Node3D
{
    public GameData Data { get; private set; } = new();
    public Settings Settings { get; } = new();
    public Purse Purse { get; private set; } = null!;
    public Reputation Reputation { get; } = new();
    public Notifier Notifier { get; } = new(line => GD.Print(line));
    public UiState Ui { get; } = new();
    public SaveGame SaveGame { get; private set; } = null!;
    public MissionManager Missions { get; private set; } = null!;
    public DialogueSystem Dialogue { get; private set; } = null!;
    public WantedSystem Wanted { get; private set; } = null!;

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

        Settings.Load();
        Data = DataLoader.LoadAll();
        Factions.Modern = Data.World.IsModern;
        Purse = new Purse(Data.World.StartMoney);
        var world = new LiveWorld(this, Data);
        Missions = new MissionManager(Data, world, Purse, Notifier);
        Wanted = new WantedSystem(world, Notifier);
        Dialogue = new DialogueSystem(Data, Missions, Wanted, world, Purse, Notifier, new LiveClock(this), new LiveDialogueView(this, Ui));
        SaveGame = new SaveGame(Purse, Reputation, Missions, Wanted, Notifier) { Main = this };
        Notifier.CoinsDropped += at => Sfx.Coins(at);
        GD.Print($"Veld data: {Data.Towns.Count} towns, {Data.Npcs.Count} npcs, {Data.Missions.Count} missions, {Data.Weapons.Count} weapons");

        Sfx = new Sfx();
        Sfx.Configure(Settings.Volume);
        AddChild(Sfx);

        DayNight = new DayNight { Name = "DayNight" };
        DayNight.Configure(Data.World.StartHour, Data.World.DayLengthSeconds, Settings.GraphicsQuality);
        AddChild(DayNight);
        Weather = new Weather { Name = "Weather" };
        Weather.Configure(Data.World.Biome, Data.World.Seed, DayNight, Sfx);
        AddChild(Weather);
        DayNight.Weather = Weather;

        World = new WorldBuilder { Name = "World" };
        World.Init(this, Purse, Reputation, Notifier, Missions, Dialogue, Wanted);
        AddChild(World);
        World.Build(Data);
        DayNight.World = World;
        AddressIndex.Build(Data.World);

        Camera = new CameraRig { Name = "CameraRig" };
        Camera.Init(Settings, Ui);
        AddChild(Camera);

        Player = GD.Load<PackedScene>("res://scenes/Player.tscn").Instantiate<Player>();
        Player.Name = "Player";
        Player.Init(this, Data, Purse, Notifier, Ui, Wanted);
        var spawn = DataLoader.XZ(Data.World.PlayerSpawn);
        Player.Position = new Vector3(spawn.X, World.Terrain.GetHeight(spawn.X, spawn.Y) + 1.0f, spawn.Y);
        Player.Rotation = new Vector3(0, Mathf.DegToRad(Data.World.PlayerSpawnFacing), 0);
        AddChild(Player);
        Camera.Target = Player;
        Camera.Yaw = Mathf.DegToRad(Data.World.PlayerSpawnFacing);
        Player.Camera = Camera;
        World.Plates?.Follow(Player);

        World.SpawnHorse(Data);
        World.SpawnPlayerCar(Data);
        World.SpawnNpcs(Data);
        MapImage.Build(World, Data);

        var ui = new CanvasLayer { Name = "UI", ProcessMode = ProcessModeEnum.Always };
        AddChild(ui);
        Hud = new Hud { Name = "Hud" };
        Hud.Init(this, Data, Purse, Notifier, Ui, Missions, Wanted);
        ui.AddChild(Hud);
        DialogueBox = new DialogueBox { Name = "DialogueBox" };
        DialogueBox.Init(Dialogue);
        ui.AddChild(DialogueBox);
        WorldMap = new WorldMap { Name = "WorldMap" };
        WorldMap.Init(this, Data, Notifier, Ui, Missions, Wanted, Dialogue);
        ui.AddChild(WorldMap);
        PauseMenu = new PauseMenu { Name = "PauseMenu" };
        PauseMenu.Init(this, Data, Settings, SaveGame, Ui, Dialogue);
        ui.AddChild(PauseMenu);
        var uiInput = new UiInput { Name = "UiInput" };
        uiInput.Init(this);
        ui.AddChild(uiInput);

        Input.MouseMode = Input.MouseModeEnum.Captured;
        Notifier.Notify(string.IsNullOrEmpty(Data.World.Intro) ? "Driefontein, Cape Colony. April 1901. Press E to talk, H to whistle for your horse, Esc for the menu." : Data.World.Intro);

        // `godot --path . -- --smoke [--shot=C:/path/prefix]` runs the automated smoke test (see DevSmoke.cs)
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--smoke") >= 0)
        {
            var smoke = new DevSmoke { Name = "DevSmoke" };
            smoke.Init(this);
            AddChild(smoke);
        }
        // `-- --carstress[=N]` gets into N parked cars in turn and reports any that flip or sink (see DevCarStress.cs)
        foreach (var a in OS.GetCmdlineUserArgs())
            if (a.StartsWith("--carstress"))
            {
                var stress = new DevCarStress { Name = "DevCarStress" };
                stress.Init(this);
                AddChild(stress);
                break;
            }
    }

    /// <summary>Ticks the gameplay systems; stops with the tree, since this node is pausable.</summary>
    public override void _Process(double delta)
    {
        Missions.Update((float)delta);
        Wanted.Update((float)delta);
        // grass bends away from the player and the horse
        var grass = World.Vegetation?.GrassMaterial;
        if (grass != null)
        {
            grass.SetShaderParameter("bend_a", Player.GlobalPosition);
            grass.SetShaderParameter("bend_b", World.Horse != null ? World.Horse.GlobalPosition : new Vector3(0, -1000, 0));
        }
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
        else if (@event.IsActionPressed("map_time_skip") && !Ui.InputLocked)
        {
            DayNight.Hour = (DayNight.Hour + 2f) % 24f;
            Notifier.Notify($"Time passes. It is now {DayNight.TimeText}.");
        }
        else if (@event.IsActionPressed("screenshot"))
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("user://screenshots"));
            var path = $"user://screenshots/veld-{Time.GetDatetimeStringFromSystem().Replace(':', '-')}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            Notifier.Notify("Screenshot saved to " + ProjectSettings.GlobalizePath(path));
        }
    }
}

/// <summary>Lives under the always-processing UI layer so Esc and M work while the game is paused.</summary>
public partial class UiInput : Node
{
    private Main _main = null!;

    public void Init(Main main) => _main = main;

    public override void _UnhandledInput(InputEvent @event) => _main.HandleUiInput(@event);
}
