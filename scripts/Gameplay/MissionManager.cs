using System.Collections.Generic;
using Godot;
using Veld.Core;

namespace Veld.Gameplay;

/// <summary>
/// Runs one mission at a time from data/missions.json. Steps are checked every frame:
///   goto        - player within Radius of Position
///   kill_group  - every NPC in Group is dead
///   talk        - the player finished a dialogue with Target (dialogue action talk_done)
///   return      - like talk, and completing it pays RewardPence
/// Starting a mission spawns its Spawns (NPCs). The HUD reads ObjectiveText and MarkerPosition.
/// Talks to the scene only through the ports it is given, so it runs the same against fakes.
/// </summary>
public sealed class MissionManager
{
    private readonly GameData _data;
    private readonly IWorldQuery _world;
    private readonly IEconomy _economy;
    private readonly INotifier _notifier;

    public HashSet<string> Completed { get; } = new();
    public MissionData? Active { get; private set; }
    public int StepIndex { get; private set; }
    public MissionStep? CurrentStep => Active != null && StepIndex < Active.Steps.Count ? Active.Steps[StepIndex] : null;

    public MissionManager(GameData data, IWorldQuery world, IEconomy economy, INotifier notifier)
    {
        _data = data;
        _world = world;
        _economy = economy;
        _notifier = notifier;
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
        return true;
    }

    private void SpawnFor(MissionData m)
    {
        foreach (var s in m.Spawns)
            if (_world.FindNpc(s.Id) == null) _world.SpawnNpc(s);
    }

    public void Restore(List<string> completed, string? activeId, int step)
    {
        Completed.Clear();
        foreach (var c in completed) Completed.Add(c);
        Active = activeId != null ? _data.Mission(activeId) : null;
        StepIndex = Active == null ? 0 : Mathf.Clamp(step, 0, Active.Steps.Count - 1);
        if (Active != null) SpawnFor(Active);
    }

    public string ObjectiveText => Active == null ? "" : (CurrentStep?.Objective ?? Active.Title);

    public Vector3? MarkerPosition
    {
        get
        {
            var step = CurrentStep;
            if (step == null) return null;
            switch (step.Type)
            {
                case "goto": return _world.Ground(DataLoader.XZ(step.Position));
                case "kill_group":
                {
                    Vector3? best = null; float bestD = float.MaxValue;
                    var p = _world.Player?.Position ?? Vector3.Zero;
                    foreach (var n in _world.Group(step.Group))
                    {
                        if (n.IsDead) continue;
                        float d = n.Position.DistanceTo(p);
                        if (d < bestD) { bestD = d; best = n.Position; }
                    }
                    return best;
                }
                default:
                    return _world.FindNpc(TargetOf(step))?.Position;
            }
        }
    }

    public void Update(float dt)
    {
        var step = CurrentStep;
        var player = _world.Player;
        if (step == null || player == null) return;
        switch (step.Type)
        {
            case "goto":
            {
                var p = DataLoader.XZ(step.Position);
                if (new Vector2(player.Position.X, player.Position.Z).DistanceTo(p) <= step.Radius) Advance();
                break;
            }
            case "kill_group":
            {
                bool any = false;
                foreach (var n in _world.Group(step.Group)) if (!n.IsDead) { any = true; break; }
                if (!any) Advance();
                break;
            }
        }
    }

    /// <summary>Called by the dialogue system when a conversation ends with the talk_done action.</summary>
    public void OnTalked(string npcId)
    {
        var step = CurrentStep;
        if (step == null) return;
        if ((step.Type == "talk" || step.Type == "return") && TargetOf(step) == npcId) Advance();
    }

    public void OnNpcKilled(INpcView npc)
    {
        // kill_group steps are polled in Update; this hook exists for custom reactions
        if (CurrentStep?.Type == "kill_group" && npc.Group == CurrentStep.Group)
        {
            int left = 0;
            foreach (var n in _world.Group(CurrentStep.Group)) if (!n.IsDead) left++;
            if (left > 0) _notifier.Notify($"{npc.Name} is down. {left} left.");
        }
    }

    /// <summary>A talk or return step without a target means the mission giver.</summary>
    private string TargetOf(MissionStep step) => string.IsNullOrEmpty(step.Target) ? Active!.Giver : step.Target;

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
