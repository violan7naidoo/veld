using Veld.Sim.Characters;
using Veld.Sim.Dialogue;
using Veld.Sim.Missions;

namespace Veld.Sim;

/// <summary>The engine-free game: owns the systems, hands them their ports, and advances them one tick at a time.</summary>
public sealed class Simulation
{
    private readonly GameData _data;
    private readonly IWorldQuery _world;
    private readonly INotifier _notifier;

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

    /// <summary>A brain for one body: the character's weapon comes from the data's weapon id, its systems from here.</summary>
    public NpcBrain CreateBrain(NpcData data, INpcView self)
    {
        _data.Weapons.TryGetValue(data.Weapon, out var weapon);
        return new NpcBrain(data, weapon, self, _world, Wanted, Reputation, Missions, Purse, _notifier, Random);
    }

    /// <summary>One fixed slice of time: the clock first, then the mission poll, then the wanted cool-down.</summary>
    public void Tick(float dt)
    {
        Clock.Tick(dt);
        Missions.Update(dt);
        Wanted.Update(dt);
    }
}
