using Veld.Core;

namespace Veld.Gameplay.Dialogue;

/// <summary>What a condition or action may read and change: the systems behind the conversation and the character in it.</summary>
public sealed class DialogueContext
{
    public DialogueContext(MissionManager missions, IWantedState wanted, IWorldQuery world, IEconomy economy, INotifier notifier, IClock clock, INpcView npc)
    {
        Missions = missions;
        Wanted = wanted;
        World = world;
        Economy = economy;
        Notifier = notifier;
        Clock = clock;
        Npc = npc;
    }

    public MissionManager Missions { get; }
    public IWantedState Wanted { get; }
    public IWorldQuery World { get; }
    public IEconomy Economy { get; }
    public INotifier Notifier { get; }
    public IClock Clock { get; }
    public INpcView Npc { get; }
}
