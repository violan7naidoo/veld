using Godot;
using Veld.Characters;
using Veld.Core;

namespace Veld.Gameplay;

/// <summary>
/// Martial-law heat. Attacking British soldiers, or being seen shooting townsfolk near the garrison,
/// raises Heat; Level (0..3) is derived from it and every British NPC becomes hostile while Level > 0.
/// Heat decays when no soldier has the player in view. The magistrate can clear it for a fee
/// (dialogue action clear_wanted).
/// </summary>
public sealed class WantedSystem : IWantedState
{
    private readonly Game _game;
    public int Level { get; private set; }
    public float Heat { get; private set; }
    private float _sinceSeen;

    public WantedSystem(Game game) => _game = game;

    public string Label => Level switch { 0 => "", 1 => "WANTED", 2 => "WANTED: patrols alerted", _ => "WANTED: shoot on sight" };

    public void SetLevel(int level)
    {
        Level = Mathf.Clamp(level, 0, 3);
        Heat = Level == 0 ? 0f : Level * 40f - 10f;
    }

    public void ClearWanted()
    {
        Level = 0;
        Heat = 0f;
        var world = _game.World;
        if (world == null) return;
        foreach (var n in world.Npcs) if (n.Faction == Faction.British) n.Provoked = false;
    }

    public void Update(float dt)
    {
        if (Heat <= 0f) { Level = 0; return; }
        var player = _game.Player;
        var world = _game.World;
        bool seen = false;
        if (player != null && world != null)
            foreach (var n in world.Npcs)
                if (!n.IsDead && n.Faction == Faction.British && n.GlobalPosition.DistanceTo(player.GlobalPosition) < 90f) { seen = true; break; }
        _sinceSeen = seen ? 0f : _sinceSeen + dt;
        if (_sinceSeen > 6f) Heat = Mathf.Max(0f, Heat - dt * 1.5f);
        int level = Heat <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(Heat / 40f), 1, 3);
        if (level != Level)
        {
            if (level == 0) _game.Notify(_game.Data.World.IsModern ? "The police have lost interest in you." : "The soldiers have lost interest in you.");
            Level = level;
        }
    }

    /// <summary>A shot was fired. Townsfolk nearby scatter.</summary>
    public void OnGunfire(Vector3 position, Node shooter)
    {
        var world = _game.World;
        if (world == null) return;
        foreach (var n in world.Npcs)
            if (n != shooter && n.GlobalPosition.DistanceTo(position) < 35f) n.Alert(position);
    }

    public void ReportAttack(Npc victim, Player attacker, bool killed)
    {
        var world = _game.World;
        if (world == null) return;
        float add;
        if (victim.Faction == Faction.British) add = killed ? 60f : 30f;
        else if (victim.Faction == Faction.Outlaw) add = 0f;
        else
        {
            // civilians and burghers: only a crime if a soldier is close enough to notice
            bool witnessed = false;
            foreach (var n in world.Npcs)
                if (!n.IsDead && n.Faction == Faction.British && n.GlobalPosition.DistanceTo(victim.GlobalPosition) < 110f) { witnessed = true; break; }
            add = witnessed ? (killed ? 45f : 15f) : 0f;
        }
        if (add <= 0f) return;
        bool wasClear = Level == 0;
        Heat = Mathf.Min(120f, Heat + add);
        Level = Mathf.Clamp(Mathf.CeilToInt(Heat / 40f), 1, 3);
        _sinceSeen = 0f;
        if (wasClear) _game.Notify(_game.Data.World.IsModern ? "The police have been called. Armed officers will shoot on sight." : "The garrison has been alerted. Soldiers will shoot on sight.");
        // nearby soldiers join in at once
        foreach (var n in world.Npcs)
            if (!n.IsDead && n.Faction == Faction.British && n.GlobalPosition.DistanceTo(attacker.GlobalPosition) < 150f) n.Provoked = true;
    }
}
