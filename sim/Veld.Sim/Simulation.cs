using System;
using System.Collections.Generic;
using Godot;
using Veld.Sim.Characters;
using Veld.Sim.Dialogue;
using Veld.Sim.Missions;

namespace Veld.Sim;

/// <summary>
/// The engine-free game: owns the systems, hands them their ports, advances them one tick at a time, keeps
/// every brain it creates, and leaves or returns as one snapshot.
/// </summary>
public sealed class Simulation
{
    private readonly GameData _data;
    private readonly IWorldQuery _world;
    private readonly INotifier _notifier;
    private readonly List<NpcBrain> _characters = new();
    private readonly HashSet<string> _deadIds = new();

    public Simulation(GameData data, IWorldQuery world, INotifier notifier, IDialogueView view, IRandom random)
    {
        _data = data;
        _world = world;
        _notifier = notifier;
        Random = random;
        Clock = new SimClock(data.World.StartHour, data.World.DayLengthSeconds);
        Purse = new Purse(data.World.StartMoney);
        Reputation = new Reputation();
        Missions = new MissionManager(data, world, Purse, notifier);
        Wanted = new WantedSystem(world, notifier);
        Dialogue = new DialogueSystem(data, Missions, Wanted, world, Purse, notifier, Clock, view);
        Player = new PlayerState(data.World.PlayerWeapons, data.Weapons, Purse, Wanted, notifier);
    }

    public SimClock Clock { get; }
    public Purse Purse { get; }
    public Reputation Reputation { get; }
    public MissionManager Missions { get; }
    public WantedSystem Wanted { get; }
    public DialogueSystem Dialogue { get; }
    public PlayerState Player { get; }
    public IRandom Random { get; }
    /// <summary>Every brain CreateBrain made, in creation order.</summary>
    public IReadOnlyList<NpcBrain> Characters => _characters;

    /// <summary>A brain for one body: the character's weapon comes from the data's weapon id, its systems from here. A restored save may say it is already dead.</summary>
    public NpcBrain CreateBrain(NpcData data, INpcView self)
    {
        _data.Weapons.TryGetValue(data.Weapon, out var weapon);
        var brain = new NpcBrain(data, weapon, self, _world, Wanted, Reputation, Missions, Purse, _notifier, Random);
        if (_deadIds.Contains(data.Id)) brain.MarkDeadSilently();
        _characters.Add(brain);
        return brain;
    }

    /// <summary>Everything the simulation is, in the save file's shape; the dead are the restored set plus every brain that has died since.</summary>
    public SimSnapshot Snapshot()
    {
        var at = _world.Player?.Position ?? Vector3.Zero;
        var dead = new SortedSet<string>(_deadIds);
        foreach (var b in _characters) if (b.IsDead) dead.Add(b.Data.Id);
        return new SimSnapshot
        {
            Pence = Purse.Pence,
            Reputation = Reputation.Value,
            PlayerPosition = new[] { at.X, at.Y, at.Z },
            Hour = Clock.Hour,
            Day = Clock.Day,
            DeadNpcs = new List<string>(dead),
            CompletedMissions = new List<string>(Missions.Completed),
            ActiveMission = Missions.Active?.Id,
            ActiveStep = Missions.StepIndex,
            WantedLevel = Wanted.Level,
            Health = Player.Health,
            Weapon = Player.CurrentWeaponId,
            Ammo = Player.AmmoSnapshot(),
        };
    }

    /// <summary>Puts a snapshot back, the dead before the missions so a respawned mission character can be born dead; the body's position is the caller's.</summary>
    public void Restore(SimSnapshot s)
    {
        Purse.Restore(s.Pence);
        Reputation.Restore(s.Reputation);
        _deadIds.Clear();
        foreach (var id in s.DeadNpcs) _deadIds.Add(id);
        foreach (var b in _characters) if (_deadIds.Contains(b.Data.Id) && !b.IsDead) b.MarkDeadSilently();
        Clock.Hour = s.Hour;
        Clock.Day = s.Day;
        Wanted.SetLevel(s.WantedLevel);
        Missions.Restore(s.CompletedMissions, s.ActiveMission, s.ActiveStep);
        Player.Health = Math.Max(10, s.Health);
        Player.RestoreAmmo(s.Ammo, s.Weapon);
    }

    /// <summary>One fixed slice of time: the clock first, then the mission poll, then the wanted cool-down.</summary>
    public void Tick(float dt)
    {
        Clock.Tick(dt);
        Missions.Update(dt);
        Wanted.Update(dt);
    }
}
