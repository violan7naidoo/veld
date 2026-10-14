using System.Collections.Generic;
using Godot;
using Veld.Characters;
using Veld.Core;

namespace Veld.Gameplay;

/// <summary>
/// Runs one mission at a time from data/missions.json. Steps are checked every frame:
///   goto        - player within Radius of Position
///   kill_group  - every NPC in Group is dead
///   talk        - the player finished a dialogue with Target (dialogue action talk_done)
///   return      - like talk, and completing it pays RewardPence
/// Starting a mission spawns its Spawns (NPCs). The HUD reads ObjectiveText and MarkerPosition.
/// </summary>
public sealed class MissionManager
{
    private readonly Game _game;
    public HashSet<string> Completed { get; } = new();
    public MissionData? Active { get; private set; }
    public int StepIndex { get; private set; }
    public MissionStep? CurrentStep => Active != null && StepIndex < Active.Steps.Count ? Active.Steps[StepIndex] : null;

    public MissionManager(Game game) => _game = game;

    public bool IsDone(string id) => Completed.Contains(id);
    public bool IsActive(string id) => Active?.Id == id;

    public bool IsAvailable(string id)
    {
        var m = _game.Data.Mission(id);
        if (m == null || Active != null || Completed.Contains(id)) return false;
        foreach (var r in m.Requires) if (!Completed.Contains(r)) return false;
        return true;
    }

    public bool Start(string id)
    {
        if (!IsAvailable(id)) return false;
        var m = _game.Data.Mission(id)!;
        Active = m;
        StepIndex = 0;
        SpawnFor(m);
        _game.Notify("New task: " + m.Title);
        return true;
    }

    private void SpawnFor(MissionData m)
    {
        var world = _game.World;
        if (world == null) return;
        foreach (var s in m.Spawns)
            if (world.FindNpc(s.Id) == null) world.SpawnNpc(s);
    }

    public void Restore(List<string> completed, string? activeId, int step)
    {
        Completed.Clear();
        foreach (var c in completed) Completed.Add(c);
        Active = activeId != null ? _game.Data.Mission(activeId) : null;
        StepIndex = Active == null ? 0 : Mathf.Clamp(step, 0, Active.Steps.Count - 1);
        if (Active != null) SpawnFor(Active);
    }

    public string ObjectiveText => Active == null ? "" : (CurrentStep?.Objective ?? Active.Title);

    public Vector3? MarkerPosition
    {
        get
        {
            var step = CurrentStep;
            var world = _game.World;
            if (step == null || world == null) return null;
            switch (step.Type)
            {
                case "goto": return world.Ground(DataLoader.XZ(step.Position));
                case "kill_group":
                {
                    Vector3? best = null; float bestD = float.MaxValue;
                    var p = _game.Player?.GlobalPosition ?? Vector3.Zero;
                    foreach (var n in world.Group(step.Group))
                    {
                        if (n.IsDead) continue;
                        float d = n.GlobalPosition.DistanceTo(p);
                        if (d < bestD) { bestD = d; best = n.GlobalPosition; }
                    }
                    return best;
                }
                default:
                {
                    var npc = world.FindNpc(string.IsNullOrEmpty(step.Target) ? Active!.Giver : step.Target);
                    return npc?.GlobalPosition;
                }
            }
        }
    }

    public void Update(float dt)
    {
        var step = CurrentStep;
        var player = _game.Player;
        var world = _game.World;
        if (step == null || player == null || world == null) return;
        switch (step.Type)
        {
            case "goto":
            {
                var p = DataLoader.XZ(step.Position);
                if (new Vector2(player.GlobalPosition.X, player.GlobalPosition.Z).DistanceTo(p) <= step.Radius) Advance();
                break;
            }
            case "kill_group":
            {
                bool any = false;
                foreach (var n in world.Group(step.Group)) if (!n.IsDead) { any = true; break; }
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
        string target = string.IsNullOrEmpty(step.Target) ? Active!.Giver : step.Target;
        if ((step.Type == "talk" || step.Type == "return") && target == npcId) Advance();
    }

    public void OnNpcKilled(Npc npc)
    {
        // kill_group steps are polled in Update; this hook exists for custom reactions
        if (CurrentStep?.Type == "kill_group" && npc.Data.Group == CurrentStep.Group)
        {
            int left = 0;
            foreach (var n in _game.World!.Group(CurrentStep.Group)) if (!n.IsDead) left++;
            if (left > 0) _game.Notify($"{npc.Data.Name} is down. {left} left.");
        }
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
        _game.Notify(CurrentStep!.Objective);
    }

    private void Complete()
    {
        var m = Active!;
        Completed.Add(m.Id);
        Active = null;
        StepIndex = 0;
        if (m.RewardPence > 0)
        {
            _game.AddMoney(m.RewardPence);
            _game.Main?.Sfx.Coins(_game.Player?.GlobalPosition ?? Vector3.Zero);
        }
        _game.Notify($"Task complete: {m.Title}" + (m.RewardPence > 0 ? $" (+{Money.Format(m.RewardPence)})" : ""));
    }
}
