using System.Collections.Generic;

namespace Veld.Sim;

/// <summary>
/// The whole simulation at one instant, in the save file's shape: the fourteen fields and names the file
/// has always had, so older saves still load. Positions are x, y, z in metres; the body applies the one here.
/// </summary>
public sealed class SimSnapshot
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
