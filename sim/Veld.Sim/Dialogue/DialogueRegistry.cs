using System.Collections.Generic;

namespace Veld.Sim.Dialogue;

/// <summary>The conditions and actions a conversation may use, by name. Add a new one here, not in DialogueSystem.</summary>
public sealed class DialogueRegistry
{
    private readonly Dictionary<string, IDialogueCondition> _conditions = new();
    private readonly Dictionary<string, IDialogueAction> _actions = new();

    /// <summary>The conditions and actions the shipped conversations use.</summary>
    public static DialogueRegistry Builtin() =>
        new DialogueRegistry()
            .Add(new MissionCondition("mission_available", (missions, id) => missions.IsAvailable(id)))
            .Add(new MissionCondition("mission_active", (missions, id) => missions.IsActive(id)))
            .Add(new MissionCondition("mission_done", (missions, id) => missions.IsDone(id)))
            .Add(new MissionStepCondition())
            .Add(new WantedCondition("wanted", wanted: true))
            .Add(new WantedCondition("not_wanted", wanted: false))
            .Add(new MoneyCondition())
            .Add(new StartMissionAction())
            .Add(new TalkDoneAction())
            .Add(new GiveAmmoAction())
            .Add(new HealAction())
            .Add(new SleepAction())
            .Add(new GiveMoneyAction())
            .Add(new TakeMoneyAction())
            .Add(new ClearWantedAction())
            .Add(new EndAction());

    /// <summary>Registers a condition, replacing any with the same name. Returns this so calls chain.</summary>
    public DialogueRegistry Add(IDialogueCondition condition)
    {
        _conditions[condition.Name] = condition;
        return this;
    }

    /// <summary>Registers an action, replacing any with the same name. Returns this so calls chain.</summary>
    public DialogueRegistry Add(IDialogueAction action)
    {
        _actions[action.Name] = action;
        return this;
    }

    public IDialogueCondition? FindCondition(string name) => _conditions.TryGetValue(name, out var condition) ? condition : null;

    public IDialogueAction? FindAction(string name) => _actions.TryGetValue(name, out var action) ? action : null;
}
