using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using Veld.Gameplay;

namespace Veld.Core;

/// <summary>
/// The save file at user://save.json: what the player owns, where they are and what they have done.
/// The player, clock and world come from Main; Save and Load do nothing until it is bound.
/// </summary>
public sealed class SaveGame
{
    public const string Path = "user://save.json";

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

    private readonly Purse _purse;
    private readonly Reputation _reputation;
    private readonly MissionManager _missions;
    private readonly WantedSystem _wanted;
    private readonly INotifier _notifier;

    public SaveGame(Purse purse, Reputation reputation, MissionManager missions, WantedSystem wanted, INotifier notifier)
    {
        _purse = purse;
        _reputation = reputation;
        _missions = missions;
        _wanted = wanted;
        _notifier = notifier;
    }

    public Main? Main { get; set; }

    public bool HasSave => FileAccess.FileExists(Path);

    public void Save()
    {
        if (Main == null || Main.Player == null || Main.DayNight == null) return;
        var player = Main.Player;
        var s = new SaveFile
        {
            Pence = _purse.Pence,
            Reputation = _reputation.Value,
            PlayerPosition = new[] { player.GlobalPosition.X, player.GlobalPosition.Y, player.GlobalPosition.Z },
            Hour = Main.DayNight.Hour,
            Day = Main.DayNight.Day,
            DeadNpcs = new List<string>(Main.World.DeadNpcs),
            CompletedMissions = new List<string>(_missions.Completed),
            ActiveMission = _missions.Active?.Id,
            ActiveStep = _missions.StepIndex,
            WantedLevel = _wanted.Level,
            Health = player.Health,
            Weapon = player.CurrentWeaponId,
            Ammo = player.AmmoSnapshot(),
        };
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        f.StoreString(JsonSerializer.Serialize(s, DataLoader.Json));
        _notifier.Notify("Game saved.");
    }

    public bool Load()
    {
        if (!HasSave || Main == null || Main.Player == null || Main.DayNight == null || Main.World == null) return false;
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        var s = JsonSerializer.Deserialize<SaveFile>(f.GetAsText(), DataLoader.Json);
        if (s == null) return false;

        var player = Main.Player;
        _purse.Restore(s.Pence);
        _reputation.Restore(s.Reputation);
        Main.World.DeadNpcs.Clear();
        foreach (var id in s.DeadNpcs) Main.World.DeadNpcs.Add(id);
        Main.World.ApplyDeadNpcs();
        Main.DayNight.Hour = s.Hour;
        Main.DayNight.Day = s.Day;
        _wanted.SetLevel(s.WantedLevel);
        _missions.Restore(s.CompletedMissions, s.ActiveMission, s.ActiveStep);
        player.Dismount();
        player.ExitVehicle();
        player.GlobalPosition = new Vector3(s.PlayerPosition[0], s.PlayerPosition[1] + 0.5f, s.PlayerPosition[2]);
        player.Health = Math.Max(10, s.Health);
        player.RestoreAmmo(s.Ammo, s.Weapon);
        _notifier.Notify("Game loaded.");
        return true;
    }
}
