using System;
using Veld.Sim.Missions;

namespace Veld.Sim.Dialogue;

/// <summary>
/// Asks the mission manager one yes-or-no question about the mission named in the argument.
/// Registered three times, as mission_available, mission_active and mission_done.
/// </summary>
public sealed class MissionCondition : IDialogueCondition
{
    private readonly Func<MissionManager, string, bool> _test;

    public MissionCondition(string name, Func<MissionManager, string, bool> test)
    {
        Name = name;
        _test = test;
    }

    public string Name { get; }

    public bool Holds(string[] args, DialogueContext context) => args.Length > 0 && _test(context.Missions, args[0]);
}
