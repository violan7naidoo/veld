using System.Collections.Generic;
using Veld.Sim.Missions;

namespace Veld.Sim.Dialogue;

/// <summary>
/// Plays a dialogue tree from data/dialogue.json through the dialogue view. A choice is hidden unless every
/// term in its Condition holds, and runs every term in its Action when picked; what each name means lives
/// in its class under Dialogue/, found by name in the registry. Talks to the scene only through the ports
/// it is given, so it runs the same against fakes.
/// </summary>
public sealed class DialogueSystem
{
    private readonly GameData _gameData;
    private readonly MissionManager _missions;
    private readonly IWantedState _wanted;
    private readonly IWorldQuery _world;
    private readonly IEconomy _economy;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly IDialogueView _view;
    private readonly DialogueRegistry _registry;

    private DialogueData? _data;
    private DialogueContext? _context;
    private readonly List<DialogueChoice> _visible = new();

    public bool IsActive => _data != null;

    public DialogueSystem(GameData data, MissionManager missions, IWantedState wanted, IWorldQuery world, IEconomy economy, INotifier notifier, IClock clock, IDialogueView view)
        : this(data, missions, wanted, world, economy, notifier, clock, view, DialogueRegistry.Builtin())
    {
    }

    public DialogueSystem(GameData data, MissionManager missions, IWantedState wanted, IWorldQuery world, IEconomy economy, INotifier notifier, IClock clock, IDialogueView view, DialogueRegistry registry)
    {
        _gameData = data;
        _missions = missions;
        _wanted = wanted;
        _world = world;
        _economy = economy;
        _notifier = notifier;
        _clock = clock;
        _view = view;
        _registry = registry;
    }

    public void Start(string dialogueId, INpcView npc)
    {
        if (!_gameData.Dialogues.TryGetValue(dialogueId, out var data))
        {
            _notifier.Notify($"{npc.Name} has nothing to say. (missing dialogue '{dialogueId}')");
            return;
        }
        _data = data;
        _context = new DialogueContext(_missions, _wanted, _world, _economy, _notifier, _clock, npc);
        ReportUnknownNames(dialogueId, data);
        Show(data.Start);
    }

    /// <summary>A misspelt name would otherwise show a choice it should hide, or do nothing when picked.</summary>
    private void ReportUnknownNames(string dialogueId, DialogueData data)
    {
        var conditions = new HashSet<string>();
        var actions = new HashSet<string>();
        foreach (var node in data.Nodes.Values)
        {
            foreach (var choice in node.Choices)
            {
                foreach (var term in DialogueTerm.Parse(choice.Condition))
                    if (_registry.FindCondition(term.Name) == null && conditions.Add(term.Name))
                        _notifier.Notify($"Dialogue '{dialogueId}' uses an unknown condition '{term.Name}'.");
                foreach (var term in DialogueTerm.Parse(choice.Action))
                    if (_registry.FindAction(term.Name) == null && actions.Add(term.Name))
                        _notifier.Notify($"Dialogue '{dialogueId}' uses an unknown action '{term.Name}'.");
            }
        }
    }

    private void Show(string nodeId)
    {
        if (_data == null || !_data.Nodes.TryGetValue(nodeId, out var node))
        {
            End();
            return;
        }
        _visible.Clear();
        var labels = new List<string>();
        foreach (var c in node.Choices)
            if (Holds(c.Condition)) { _visible.Add(c); labels.Add(c.Text); }
        if (labels.Count == 0) labels.Add("(Leave)");
        string speaker = string.IsNullOrEmpty(_data.Speaker) ? _context!.Npc.Name : _data.Speaker;
        _view.Open(speaker, node.Text, labels, Choose);
    }

    public void Choose(int index)
    {
        if (_data == null) return;
        if (index < 0 || index >= _visible.Count) { End(); return; }
        var c = _visible[index];
        if (Apply(c.Action)) return;
        if (string.IsNullOrEmpty(c.Next) || c.Next == "end") End();
        else Show(c.Next);
    }

    /// <summary>Runs each action in turn; true when one of them closed the dialogue, which skips the rest.</summary>
    private bool Apply(string action)
    {
        foreach (var term in DialogueTerm.Parse(action))
        {
            if (_registry.FindAction(term.Name)?.Apply(term.Args, _context!) == true)
            {
                End();
                return true;
            }
        }
        return false;
    }

    /// <summary>Every term must hold; a name nobody registered counts as holding.</summary>
    private bool Holds(string condition)
    {
        foreach (var term in DialogueTerm.Parse(condition))
        {
            var kind = _registry.FindCondition(term.Name);
            if (kind != null && !kind.Holds(term.Args, _context!)) return false;
        }
        return true;
    }

    public void End()
    {
        if (_data == null) return;
        _data = null;
        _context = null;
        _view.Close();
    }

    /// <summary>The box was closed from outside (Escape).</summary>
    public void OnBoxClosed()
    {
        if (_data != null) End();
    }
}
