using System.Collections.Generic;
using Godot;
using Veld.Core;
using Veld.Gameplay.Steps;

namespace Veld.Gameplay;

/// <summary>
/// Runs one mission at a time from data/missions.json. What each step means lives in its kind
/// (see Steps/): goto, kill_group, talk and return ship with the game, and a new kind is one class
/// added to the registry. Starting a mission spawns its Spawns (NPCs). The HUD reads ObjectiveText
/// and MarkerPosition. Talks to the scene only through the ports it is given.
/// </summary>
public sealed class MissionManager
{
    private readonly GameData _data;
    private readonly IWorldQuery _world;
    private readonly IEconomy _economy;
    private readonly INotifier _notifier;
    private readonly MissionStepRegistry _kinds;

    public HashSet<string> Completed { get; } = new();
    public MissionData? Active { get; private set; }
    public int StepIndex { get; private set; }
    public MissionStep? CurrentStep => Active != null && StepIndex < Active.Steps.Count ? Active.Steps[StepIndex] : null;

    public MissionManager(GameData data, IWorldQuery world, IEconomy economy, INotifier notifier)
        : this(data, world, economy, notifier, MissionStepRegistry.Builtin())
    {
    }

    public MissionManager(GameData data, IWorldQuery world, IEconomy economy, INotifier notifier, MissionStepRegistry kinds)
    {
        _data = data;
        _world = world;
        _economy = economy;
        _notifier = notifier;
        _kinds = kinds;
    }

    public bool IsDone(string id) => Completed.Contains(id);
    public bool IsActive(string id) => Active?.Id == id;

    public bool IsAvailable(string id)
    {
        var m = _data.Mission(id);
        if (m == null || Active != null || Completed.Contains(id)) return false;
        foreach (var r in m.Requires) if (!Completed.Contains(r)) return false;
        return true;
    }

    public bool Start(string id)
    {
        if (!IsAvailable(id)) return false;
        var m = _data.Mission(id)!;
        Active = m;
        StepIndex = 0;
        SpawnFor(m);
        _notifier.Notify("New task: " + m.Title);
        ReportUnknownKinds(m);
        return true;
    }

    private void SpawnFor(MissionData m)
    {
        foreach (var s in m.Spawns)
            if (_world.FindNpc(s.Id) == null) _world.SpawnNpc(s);
    }

    /// <summary>A misspelt step kind would otherwise leave the mission silently stuck.</summary>
    private void ReportUnknownKinds(MissionData m)
    {
        foreach (var s in m.Steps)
            if (_kinds.Find(s.Type) == null) _notifier.Notify($"Mission '{m.Title}' has a step of unknown kind '{s.Type}'.");
    }

    public void Restore(List<string> completed, string? activeId, int step)
    {
        Completed.Clear();
        foreach (var c in completed) Completed.Add(c);
        Active = activeId != null ? _data.Mission(activeId) : null;
        StepIndex = Active == null ? 0 : Mathf.Clamp(step, 0, Active.Steps.Count - 1);
        if (Active != null)
        {
            SpawnFor(Active);
            ReportUnknownKinds(Active);
        }
    }

    public string ObjectiveText => Active == null ? "" : (CurrentStep?.Objective ?? Active.Title);

    public Vector3? MarkerPosition
    {
        get
        {
            var step = CurrentStep;
            if (step == null) return null;
            return _kinds.Find(step.Type)?.Marker(step, _world, Active!.Giver);
        }
    }

    public void Update(float dt)
    {
        var step = CurrentStep;
        if (step == null || _world.Player == null) return;
        if (_kinds.Find(step.Type)?.Poll(step, _world) == true) Advance();
    }

    /// <summary>Called by the dialogue system when a conversation ends with the talk_done action.</summary>
    public void OnTalked(string npcId)
    {
        var step = CurrentStep;
        if (step == null) return;
        if (_kinds.Find(step.Type)?.OnTalked(step, npcId, Active!.Giver) == true) Advance();
    }

    public void OnNpcKilled(INpcView npc)
    {
        var step = CurrentStep;
        if (step == null) return;
        var message = _kinds.Find(step.Type)?.OnNpcKilled(step, npc, _world);
        if (message != null) _notifier.Notify(message);
    }

    private void Advance()
    {
        if (Active == null) return;
        StepIndex++;
        if (StepIndex >= Active.Steps.Count)
        {
            Complete();
            return;
        }
        _notifier.Notify(CurrentStep!.Objective);
    }

    private void Complete()
    {
        var m = Active!;
        Completed.Add(m.Id);
        Active = null;
        StepIndex = 0;
        if (m.RewardPence > 0)
        {
            _economy.Add(m.RewardPence);
            _notifier.Coins(_world.Player?.Position ?? Vector3.Zero);
        }
        _notifier.Notify($"Task complete: {m.Title}" + (m.RewardPence > 0 ? $" (+{Money.Format(m.RewardPence)})" : ""));
    }
}
