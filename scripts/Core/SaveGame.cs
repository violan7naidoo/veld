using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using Veld.Sim;

namespace Veld.Core;

/// <summary>
/// The save file at user://save.json: what the player owns, where they are and what they have done.
/// The player and the world come from Main; money, standing, time, missions and wanted come from the
/// simulation. Save and Load do nothing until Main is bound.
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

    private readonly Simulation _sim;
    private readonly INotifier _notifier;

    public SaveGame(Simulation sim, INotifier notifier)
    {
        _sim = sim;
        _notifier = notifier;
    }

    public Main? Main { get; set; }

    public bool HasSave => FileAccess.FileExists(Path);

    public void Save()
    {
        if (Main == null || Main.Player == null) return;
        var player = Main.Player;
        var s = new SaveFile
        {
            Pence = _sim.Purse.Pence,
            Reputation = _sim.Reputation.Value,
            PlayerPosition = new[] { player.GlobalPosition.X, player.GlobalPosition.Y, player.GlobalPosition.Z },
            Hour = _sim.Clock.Hour,
            Day = _sim.Clock.Day,
            DeadNpcs = new List<string>(Main.World.DeadNpcs),
            CompletedMissions = new List<string>(_sim.Missions.Completed),
            ActiveMission = _sim.Missions.Active?.Id,
            ActiveStep = _sim.Missions.StepIndex,
            WantedLevel = _sim.Wanted.Level,
            Health = _sim.Player.Health,
            Weapon = _sim.Player.CurrentWeaponId,
            Ammo = _sim.Player.AmmoSnapshot(),
        };
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        f.StoreString(JsonSerializer.Serialize(s, DataLoader.Json));
        _notifier.Notify("Game saved.");
    }

    public bool Load()
    {
        if (!HasSave || Main == null || Main.Player == null || Main.World == null) return false;
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        var s = JsonSerializer.Deserialize<SaveFile>(f.GetAsText(), DataLoader.Json);
        if (s == null) return false;

        var player = Main.Player;
        _sim.Purse.Restore(s.Pence);
        _sim.Reputation.Restore(s.Reputation);
        Main.World.DeadNpcs.Clear();
        foreach (var id in s.DeadNpcs) Main.World.DeadNpcs.Add(id);
        Main.World.ApplyDeadNpcs();
        _sim.Clock.Hour = s.Hour;
        _sim.Clock.Day = s.Day;
        _sim.Wanted.SetLevel(s.WantedLevel);
        _sim.Missions.Restore(s.CompletedMissions, s.ActiveMission, s.ActiveStep);
        player.Dismount();
        player.ExitVehicle();
        player.GlobalPosition = new Vector3(s.PlayerPosition[0], s.PlayerPosition[1] + 0.5f, s.PlayerPosition[2]);
        _sim.Player.Health = Math.Max(10, s.Health);
        _sim.Player.RestoreAmmo(s.Ammo, s.Weapon);
        player.Equip(_sim.Player.CurrentWeaponId);
        _notifier.Notify("Game loaded.");
        return true;
    }
}
