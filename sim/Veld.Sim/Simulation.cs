using Veld.Sim.Dialogue;
using Veld.Sim.Missions;

namespace Veld.Sim;

/// <summary>The engine-free game: owns the systems, hands them their ports, and advances them one tick at a time.</summary>
public sealed class Simulation
{
    public Simulation(GameData data, IWorldQuery world, INotifier notifier, IDialogueView view)
    {
        Clock = new SimClock(data.World.StartHour, data.World.DayLengthSeconds);
        Purse = new Purse(data.World.StartMoney);
        Reputation = new Reputation();
        Missions = new MissionManager(data, world, Purse, notifier);
        Wanted = new WantedSystem(world, notifier);
        Dialogue = new DialogueSystem(data, Missions, Wanted, world, Purse, notifier, Clock, view);
    }

    public SimClock Clock { get; }
    public Purse Purse { get; }
    public Reputation Reputation { get; }
    public MissionManager Missions { get; }
    public WantedSystem Wanted { get; }
    public DialogueSystem Dialogue { get; }

    /// <summary>One fixed slice of time: the clock first, then the mission poll, then the wanted cool-down.</summary>
    public void Tick(float dt)
    {
        Clock.Tick(dt);
        Missions.Update(dt);
        Wanted.Update(dt);
    }
}
