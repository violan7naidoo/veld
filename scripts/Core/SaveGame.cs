using System.Text.Json;
using Godot;
using Veld.Sim;

namespace Veld.Core;

/// <summary>
/// The save file at user://save.json: the simulation's snapshot, written out and read back. The body is
/// moved by hand on load, since the simulation does not own where the player stands. Save and Load do
/// nothing until Main is bound.
/// </summary>
public sealed class SaveGame
{
    public const string Path = "user://save.json";

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
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        f.StoreString(JsonSerializer.Serialize(_sim.Snapshot(), DataLoader.Json));
        _notifier.Notify("Game saved.");
    }

    public bool Load()
    {
        if (!HasSave || Main == null || Main.Player == null || Main.World == null) return false;
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        var s = JsonSerializer.Deserialize<SimSnapshot>(f.GetAsText(), DataLoader.Json);
        if (s == null) return false;

        var player = Main.Player;
        player.Dismount();
        player.ExitVehicle();
        _sim.Restore(s);
        Main.World.ShowDead();
        player.GlobalPosition = new Vector3(s.PlayerPosition[0], s.PlayerPosition[1] + 0.5f, s.PlayerPosition[2]);
        player.Equip(_sim.Player.CurrentWeaponId);
        _notifier.Notify("Game loaded.");
        return true;
    }
}
