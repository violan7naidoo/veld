using Godot;

namespace Veld.Sim;

/// <summary>
/// Martial-law heat (the police, on a modern map). Attacking officers, or being seen attacking
/// townsfolk near one, raises Heat; Level (0..3) is derived from it and every officer becomes hostile
/// while Level > 0. Heat decays when no officer has the player in view. A fine clears it (dialogue
/// action clear_wanted). Talks to the scene only through the ports it is given.
/// </summary>
public sealed class WantedSystem : IWantedState
{
    private readonly IWorldQuery _world;
    private readonly INotifier _notifier;
    public int Level { get; private set; }
    public float Heat { get; private set; }
    private float _sinceSeen;

    public WantedSystem(IWorldQuery world, INotifier notifier)
    {
        _world = world;
        _notifier = notifier;
    }

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
        foreach (var n in _world.Npcs) if (n.Faction == Faction.British) n.Provoked = false;
    }

    public void Update(float dt)
    {
        if (Heat <= 0f) { Level = 0; return; }
        var player = _world.Player;
        bool seen = false;
        if (player != null)
            foreach (var n in _world.Npcs)
                if (!n.IsDead && n.Faction == Faction.British && n.Position.DistanceTo(player.Position) < 90f) { seen = true; break; }
        _sinceSeen = seen ? 0f : _sinceSeen + dt;
        if (_sinceSeen > 6f) Heat = Mathf.Max(0f, Heat - dt * 1.5f);
        int level = Heat <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(Heat / 40f), 1, 3);
        if (level != Level)
        {
            if (level == 0) _notifier.Notify(_world.IsModern ? "The police have lost interest in you." : "The soldiers have lost interest in you.");
            Level = level;
        }
    }

    /// <summary>A shot was fired. Townsfolk nearby scatter; the shooter, if a character, is not told about its own shot.</summary>
    public void OnGunfire(Vector3 position, INpcView? shooter)
    {
        foreach (var n in _world.Npcs)
            if (n != shooter && n.Position.DistanceTo(position) < 35f) n.Alert(position);
    }

    public void ReportAttack(INpcView victim, IPlayerView attacker, bool killed)
    {
        float add;
        if (victim.Faction == Faction.British) add = killed ? 60f : 30f;
        else if (victim.Faction == Faction.Outlaw) add = 0f;
        else
        {
            // civilians and burghers: only a crime if an officer is close enough to notice
            bool witnessed = false;
            foreach (var n in _world.Npcs)
                if (!n.IsDead && n.Faction == Faction.British && n.Position.DistanceTo(victim.Position) < 110f) { witnessed = true; break; }
            add = witnessed ? (killed ? 45f : 15f) : 0f;
        }
        if (add <= 0f) return;
        bool wasClear = Level == 0;
        Heat = Mathf.Min(120f, Heat + add);
        Level = Mathf.Clamp(Mathf.CeilToInt(Heat / 40f), 1, 3);
        _sinceSeen = 0f;
        if (wasClear) _notifier.Notify(_world.IsModern ? "The police have been called. Armed officers will shoot on sight." : "The garrison has been alerted. Soldiers will shoot on sight.");
        // nearby officers join in at once
        foreach (var n in _world.Npcs)
            if (!n.IsDead && n.Faction == Faction.British && n.Position.DistanceTo(attacker.Position) < 150f) n.Provoked = true;
    }
}
