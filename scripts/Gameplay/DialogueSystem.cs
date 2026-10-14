using System;
using System.Collections.Generic;
using Godot;
using Veld.Characters;
using Veld.Core;

namespace Veld.Gameplay;

/// <summary>
/// Plays a dialogue tree from data/dialogue.json through the DialogueBox. Choices can be hidden by a
/// Condition and can run an Action when picked:
///   conditions: mission_available:id, mission_active:id, mission_step:id:n, mission_done:id, wanted, not_wanted, money:pence
///               (several may be combined with commas; all must hold)
///   actions:    start_mission:id, talk_done, give_money:pence, take_money:pence, give_ammo:weapon:count, heal, sleep:hours, clear_wanted:pence, end
/// </summary>
public sealed class DialogueSystem
{
    private readonly Game _game;
    private DialogueData? _data;
    private Npc? _npc;
    private string _nodeId = "";
    private readonly List<DialogueChoice> _visible = new();

    public bool IsActive => _data != null;

    public DialogueSystem(Game game) => _game = game;

    public void Start(string dialogueId, Npc npc)
    {
        if (!_game.Data.Dialogues.TryGetValue(dialogueId, out var data))
        {
            _game.Notify($"{npc.Data.Name} has nothing to say. (missing dialogue '{dialogueId}')");
            return;
        }
        _data = data;
        _npc = npc;
        _game.InputLocked = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Show(data.Start);
    }

    private void Show(string nodeId)
    {
        if (_data == null || !_data.Nodes.TryGetValue(nodeId, out var node))
        {
            End();
            return;
        }
        _nodeId = nodeId;
        _visible.Clear();
        var labels = new List<string>();
        foreach (var c in node.Choices)
            if (Holds(c.Condition)) { _visible.Add(c); labels.Add(c.Text); }
        if (labels.Count == 0) labels.Add("(Leave)");
        string speaker = string.IsNullOrEmpty(_data.Speaker) ? _npc?.Data.Name ?? "" : _data.Speaker;
        _game.Main?.DialogueBox.Show(speaker, node.Text, labels, Choose);
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
                    if (parts.Length > 1) _game.Missions.Start(parts[1]);
                    break;
                case "talk_done":
                    if (_npc != null) _game.Missions.OnTalked(_npc.Data.Id);
                    break;
                case "give_money":
                    if (parts.Length > 1 && int.TryParse(parts[1], out var give)) { _game.AddMoney(give); _game.Main?.Sfx.Coins(_npc?.GlobalPosition ?? Vector3.Zero); }
                    break;
                case "take_money":
                    if (parts.Length > 1 && int.TryParse(parts[1], out var take)) _game.TrySpend(take);
                    break;
                case "clear_wanted":
                    if (parts.Length > 1 && int.TryParse(parts[1], out var fee))
                    {
                        if (_game.TrySpend(fee)) { _game.Wanted.ClearWanted(); _game.Notify("Paid " + Money.Format(fee) + ". The complaint is withdrawn."); }
                        else _game.Notify("You cannot afford the fine.");
                    }
                    else _game.Wanted.ClearWanted();
                    break;
                case "give_ammo":
                    if (parts.Length > 2 && int.TryParse(parts[2], out var count) && _game.Player != null && _game.Player.Weapons.TryGetValue(parts[1], out var weapon))
                        weapon.Reserve += count;
                    break;
                case "heal":
                    if (_game.Player != null) _game.Player.Health = _game.Player.MaxHealth;
                    break;
                case "sleep":
                    if (parts.Length > 1 && float.TryParse(parts[1], out var hours) && _game.DayNight != null)
                    {
                        _game.DayNight.Hour += hours;
                        while (_game.DayNight.Hour >= 24f) { _game.DayNight.Hour -= 24f; _game.DayNight.Day++; }
                    }
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
                "mission_available" => parts.Length > 1 && _game.Missions.IsAvailable(parts[1]),
                "mission_active" => parts.Length > 1 && _game.Missions.IsActive(parts[1]),
                "mission_step" => parts.Length > 2 && _game.Missions.IsActive(parts[1]) && int.TryParse(parts[2], out var n) && _game.Missions.StepIndex == n,
                "mission_done" => parts.Length > 1 && _game.Missions.IsDone(parts[1]),
                "wanted" => _game.Wanted.Level > 0,
                "not_wanted" => _game.Wanted.Level == 0,
                "money" => parts.Length > 1 && int.TryParse(parts[1], out var p) && _game.Pence >= p,
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
        _game.Main?.DialogueBox.Close();
        _game.InputLocked = false;
        if (_game.Main != null && !_game.Main.PauseMenu.IsOpen) Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    /// <summary>The box was closed from outside (Escape).</summary>
    public void OnBoxClosed()
    {
        if (_data != null) End();
    }
}
