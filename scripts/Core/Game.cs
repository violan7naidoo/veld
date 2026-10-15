using Godot;
using Veld.Characters;
using Veld.Gameplay;
using Veld.World;

namespace Veld.Core;

/// <summary>
/// Autoload singleton, being retired by build-plan item 18. Loads the data, owns the purse, standing,
/// notifier, settings, UI state and save file and the mission, dialogue and wanted systems, and still
/// answers the old call sites through forwarders until 18b to 18e move them onto the objects themselves.
/// </summary>
public partial class Game : Node
{
    public static Game Instance { get; private set; } = null!;

    public GameData Data { get; private set; } = new();
    public Purse Purse { get; private set; } = null!;
    public Reputation Reputation { get; } = new();
    public Notifier Notifier { get; } = new(line => GD.Print(line));
    public Settings Settings { get; } = new();
    public UiState Ui { get; } = new();
    public SaveGame SaveGame { get; private set; } = null!;

    public MissionManager Missions { get; private set; } = null!;
    public DialogueSystem Dialogue { get; private set; } = null!;
    public WantedSystem Wanted { get; private set; } = null!;

    public Main? Main { get; private set; }
    public Player? Player => Main?.Player;
    public WorldBuilder? World => Main?.World;
    public DayNight? DayNight => Main?.DayNight;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Settings.Load();
        Data = DataLoader.LoadAll();
        Factions.Modern = Data.World.IsModern;
        Purse = new Purse(Data.World.StartMoney);
        var world = new LiveWorld(this);
        Missions = new MissionManager(Data, world, Purse, Notifier);
        Wanted = new WantedSystem(world, Notifier);
        Dialogue = new DialogueSystem(Data, Missions, Wanted, world, Purse, Notifier, new LiveClock(this), new LiveDialogueView(this));
        SaveGame = new SaveGame(Purse, Reputation, Missions, Wanted, Notifier);
        Notifier.CoinsDropped += at => Main?.Sfx.Coins(at);
        GD.Print($"Veld data: {Data.Towns.Count} towns, {Data.Npcs.Count} npcs, {Data.Missions.Count} missions, {Data.Weapons.Count} weapons");
    }

    public void Bind(Main main)
    {
        Main = main;
        SaveGame.Main = main;
    }

    public override void _Process(double delta)
    {
        if (Main == null || GetTree().Paused) return;
        Missions.Update((float)delta);
        Wanted.Update((float)delta);
        // grass bends away from the player and the horse
        var grass = World?.Vegetation?.GrassMaterial;
        if (grass != null && Player != null)
        {
            grass.SetShaderParameter("bend_a", Player.GlobalPosition);
            grass.SetShaderParameter("bend_b", World!.Horse != null ? World.Horse.GlobalPosition : new Vector3(0, -1000, 0));
        }
    }

    // Forwarders for the call sites 18b to 18e move onto the objects above; each goes with its last caller.
    public int Pence => Purse.Pence;
    public void AddMoney(int pence) => Purse.Add(pence);
    public void Notify(string message) => Notifier.Notify(message);
    public Quality GraphicsQuality { get => Settings.GraphicsQuality; set => Settings.GraphicsQuality = value; }
    public bool InputLocked { get => Ui.InputLocked; set => Ui.InputLocked = value; }
    public const string SavePath = SaveGame.Path;
    public bool HasSave => SaveGame.HasSave;
    public void Save() => SaveGame.Save();
    public bool Load() => SaveGame.Load();
}
