using System.Collections.Generic;
using Godot;
using Veld.Core;

namespace Veld.Gameplay;

/// <summary>
/// Plays a dialogue tree from data/dialogue.json through the dialogue view. Choices can be hidden by a
/// Condition and can run an Action when picked:
///   conditions: mission_available:id, mission_active:id, mission_step:id:n, mission_done:id, wanted, not_wanted, money:pence
///               (several may be combined with commas; all must hold)
///   actions:    start_mission:id, talk_done, give_money:pence, take_money:pence, give_ammo:weapon:count, heal, sleep:hours, clear_wanted:pence, end
/// Talks to the scene only through the ports it is given, so it runs the same against fakes.
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

    private DialogueData? _data;
    private INpcView? _npc;
    private readonly List<DialogueChoice> _visible = new();

    public bool IsActive => _data != null;

    public DialogueSystem(GameData data, MissionManager missions, IWantedState wanted, IWorldQuery world, IEconomy economy, INotifier notifier, IClock clock, IDialogueView view)
    {
        _gameData = data;
        _missions = missions;
        _wanted = wanted;
        _world = world;
        _economy = economy;
        _notifier = notifier;
        _clock = clock;
        _view = view;
    }

    public void Start(string dialogueId, INpcView npc)
    {
        if (!_gameData.Dialogues.TryGetValue(dialogueId, out var data))
        {
            _notifier.Notify($"{npc.Name} has nothing to say. (missing dialogue '{dialogueId}')");
            return;
        }
        _data = data;
        _npc = npc;
        Show(data.Start);
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
        string speaker = string.IsNullOrEmpty(_data.Speaker) ? _npc?.Name ?? "" : _data.Speaker;
        _view.Open(speaker, node.Text, labels, Choose);
    }

    public void Choose(int index)
    {
        if (_data == null) return;
        if (index < 0 || index >= _visible.Count) { End(); return; }
        var c = _visible[index];
        bool ended = Apply(c.Action);
        if (ended) return;
        if (string.IsNullOrEmpty(c.Next) || c.Next == "end") End();
        else Show(c.Next);
    }

    /// <summary>Returns true if the action closed the dialogue.</summary>
    private bool Apply(string action)
    {
        if (string.IsNullOrEmpty(action)) return false;
        foreach (var raw in action.Split(','))
        {
            var parts = raw.Trim().Split(':');
            switch (parts[0])
            {
                case "start_mission":
                    if (parts.Length > 1) _missions.Start(parts[1]);
                    break;
                case "talk_done":
                    if (_npc != null) _missions.OnTalked(_npc.Id);
                    break;
                case "give_money":
                    if (parts.Length > 1 && int.TryParse(parts[1], out var give)) { _economy.Add(give); _notifier.Coins(_npc?.Position ?? Vector3.Zero); }
                    break;
                case "take_money":
                    if (parts.Length > 1 && int.TryParse(parts[1], out var take)) _economy.TrySpend(take);
                    break;
                case "clear_wanted":
                    if (parts.Length > 1 && int.TryParse(parts[1], out var fee))
                    {
                        if (_economy.TrySpend(fee)) { _wanted.ClearWanted(); _notifier.Notify("Paid " + Money.Format(fee) + ". The complaint is withdrawn."); }
                        else _notifier.Notify("You cannot afford the fine.");
                    }
                    else _wanted.ClearWanted();
                    break;
                case "give_ammo":
                    if (parts.Length > 2 && int.TryParse(parts[2], out var count)) _world.Player?.GiveAmmo(parts[1], count);
                    break;
                case "heal":
                    _world.Player?.Heal();
                    break;
                case "sleep":
                    if (parts.Length > 1 && float.TryParse(parts[1], out var hours)) _clock.Advance(hours);
                    break;
                case "end":
                    End();
                    return true;
            }
        }
        return false;
    }

    private bool Holds(string condition)
    {
        if (string.IsNullOrEmpty(condition)) return true;
        foreach (var raw in condition.Split(','))
        {
            var parts = raw.Trim().Split(':');
            bool ok = parts[0] switch
            {
                "mission_available" => parts.Length > 1 && _missions.IsAvailable(parts[1]),
                "mission_active" => parts.Length > 1 && _missions.IsActive(parts[1]),
                "mission_step" => parts.Length > 2 && _missions.IsActive(parts[1]) && int.TryParse(parts[2], out var n) && _missions.StepIndex == n,
                "mission_done" => parts.Length > 1 && _missions.IsDone(parts[1]),
                "wanted" => _wanted.Level > 0,
                "not_wanted" => _wanted.Level == 0,
                "money" => parts.Length > 1 && int.TryParse(parts[1], out var p) && _economy.Pence >= p,
                _ => true,
            };
            if (!ok) return false;
        }
        return true;
    }

    public void End()
    {
        if (_data == null) return;
        _data = null;
        _npc = null;
        _view.Close();
    }

    /// <summary>The box was closed from outside (Escape).</summary>
    public void OnBoxClosed()
    {
        if (_data != null) End();
    }
}
