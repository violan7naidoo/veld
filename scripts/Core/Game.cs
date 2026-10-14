using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using Veld.Characters;
using Veld.Gameplay;
using Veld.UI;
using Veld.World;

namespace Veld.Core;

/// <summary>
/// Autoload singleton: the one place that knows about everything. Holds the player's money and
/// reputation, the mission, dialogue and wanted systems, and saves/loads the game to user://save.json.
/// </summary>
public partial class Game : Node, IEconomy, INotifier
{
    public static Game Instance { get; private set; } = null!;

    public GameData Data { get; private set; } = new();
    public int Pence { get; private set; } = 3 * Money.Pound;
    /// <summary>Negative when the player has harmed Boers or civilians. Boers turn hostile below -2.</summary>
    public int Reputation { get; set; } = 0;
    public HashSet<string> DeadNpcs { get; } = new();

    public MissionManager Missions { get; private set; } = null!;
    public DialogueSystem Dialogue { get; private set; } = null!;
    public WantedSystem Wanted { get; private set; } = null!;

    public Main? Main { get; private set; }
    public Player? Player => Main?.Player;
    public Hud? Hud => Main?.Hud;
    public WorldBuilder? World => Main?.World;
    public DayNight? DayNight => Main?.DayNight;

    /// <summary>A spot the player marked on the map; shown on the compass and minimap.</summary>
    public Vector3? Waypoint { get; set; }

    /// <summary>True while a dialogue or menu owns the input.</summary>
    public bool InputLocked { get; set; }
    public float MouseSensitivity { get; set; } = 0.0025f;
    public bool InvertY { get; set; } = false;
    public float Volume { get; set; } = 0.8f;
    public Quality GraphicsQuality { get; set; } = Quality.High;

    public const string SettingsPath = "user://settings.cfg";

    public void LoadSettings()
    {
        var cf = new ConfigFile();
        if (cf.Load(SettingsPath) != Error.Ok) return;
        MouseSensitivity = cf.GetValue("input", "sensitivity", MouseSensitivity).AsSingle();
        InvertY = cf.GetValue("input", "invert_y", InvertY).AsBool();
        Volume = cf.GetValue("audio", "volume", Volume).AsSingle();
        GraphicsQuality = (Quality)Mathf.Clamp(cf.GetValue("graphics", "quality", (int)GraphicsQuality).AsInt32(), 0, 2);
    }

    public void SaveSettings()
    {
        var cf = new ConfigFile();
        cf.SetValue("input", "sensitivity", MouseSensitivity);
        cf.SetValue("input", "invert_y", InvertY);
        cf.SetValue("audio", "volume", Volume);
        cf.SetValue("graphics", "quality", (int)GraphicsQuality);
        cf.Save(SettingsPath);
    }

    public event Action<int>? MoneyChanged;
    public event Action<string>? Notified;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        LoadSettings();
        Data = DataLoader.LoadAll();
        Factions.Modern = Data.World.IsModern;
        Pence = Data.World.StartMoney;
        var world = new LiveWorld(this);
        Missions = new MissionManager(Data, world, this, this);
        Wanted = new WantedSystem(world, this);
        Dialogue = new DialogueSystem(Data, Missions, Wanted, world, this, this, new LiveClock(this), new LiveDialogueView(this));
        GD.Print($"Veld data: {Data.Towns.Count} towns, {Data.Npcs.Count} npcs, {Data.Missions.Count} missions, {Data.Weapons.Count} weapons");
    }

    public void Bind(Main main) => Main = main;

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

    public void AddMoney(int pence)
    {
        Pence += pence;
        MoneyChanged?.Invoke(Pence);
    }

    public bool TrySpend(int pence)
    {
        if (Pence < pence) return false;
        AddMoney(-pence);
        return true;
    }

    void IEconomy.Add(int pence) => AddMoney(pence);

    public void Notify(string message)
    {
        GD.Print("[notify] " + message);
        Notified?.Invoke(message);
    }

    public void Coins(Vector3 at) => Main?.Sfx.Coins(at);

    // ---------------------------------------------------------------- save / load

    private sealed class SaveFile
    {
        public int Version { get; set; } = 1;
        public int Pence { get; set; }
        public int Reputation { get; set; }
        public float[] PlayerPosition { get; set; } = { 0, 0, 0 };
        public float Hour { get; set; }
        public int Day { get; set; }
        public List<string> DeadNpcs { get; set; } = new();
        public List<string> CompletedMissions { get; set; } = new();
        public string? ActiveMission { get; set; }
        public int ActiveStep { get; set; }
        public int WantedLevel { get; set; }
        public int Health { get; set; }
        public string? Weapon { get; set; }
        public Dictionary<string, int> Ammo { get; set; } = new();
    }

    public const string SavePath = "user://save.json";

    public bool HasSave => FileAccess.FileExists(SavePath);

    public void Save()
    {
        if (Player == null || DayNight == null) return;
        var s = new SaveFile
        {
            Pence = Pence,
            Reputation = Reputation,
            PlayerPosition = new[] { Player.GlobalPosition.X, Player.GlobalPosition.Y, Player.GlobalPosition.Z },
            Hour = DayNight.Hour,
            Day = DayNight.Day,
            DeadNpcs = new List<string>(DeadNpcs),
            CompletedMissions = new List<string>(Missions.Completed),
            ActiveMission = Missions.Active?.Id,
            ActiveStep = Missions.StepIndex,
            WantedLevel = Wanted.Level,
            Health = Player.Health,
            Weapon = Player.CurrentWeaponId,
            Ammo = Player.AmmoSnapshot(),
        };
        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        f.StoreString(JsonSerializer.Serialize(s, DataLoader.Json));
        Notify("Game saved.");
    }

    public bool Load()
    {
        if (!HasSave || Player == null || DayNight == null || World == null) return false;
        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        var s = JsonSerializer.Deserialize<SaveFile>(f.GetAsText(), DataLoader.Json);
        if (s == null) return false;

        Pence = s.Pence;
        Reputation = s.Reputation;
        DeadNpcs.Clear();
        foreach (var id in s.DeadNpcs) DeadNpcs.Add(id);
        World.ApplyDeadNpcs();
        DayNight.Hour = s.Hour;
        DayNight.Day = s.Day;
        Wanted.SetLevel(s.WantedLevel);
        Missions.Restore(s.CompletedMissions, s.ActiveMission, s.ActiveStep);
        Player.Dismount();
        Player.ExitVehicle();
        Player.GlobalPosition = new Vector3(s.PlayerPosition[0], s.PlayerPosition[1] + 0.5f, s.PlayerPosition[2]);
        Player.Health = Math.Max(10, s.Health);
        Player.RestoreAmmo(s.Ammo, s.Weapon);
        MoneyChanged?.Invoke(Pence);
        Notify("Game loaded.");
        return true;
    }
}
