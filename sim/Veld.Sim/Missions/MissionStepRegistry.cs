using System.Collections.Generic;

namespace Veld.Sim.Missions;

/// <summary>The step kinds a mission may use, by name. Add a new kind here, not in MissionManager.</summary>
public sealed class MissionStepRegistry
{
    private readonly Dictionary<string, IMissionStepKind> _kinds = new();

    /// <summary>The four kinds the shipped missions use.</summary>
    public static MissionStepRegistry Builtin() =>
        new MissionStepRegistry().Add(new GotoStep()).Add(new KillGroupStep()).Add(new TalkStep("talk")).Add(new TalkStep("return"));

    /// <summary>Registers a kind, replacing any with the same name. Returns this so calls chain.</summary>
    public MissionStepRegistry Add(IMissionStepKind kind)
    {
        _kinds[kind.Type] = kind;
        return this;
    }

    public IMissionStepKind? Find(string type) => _kinds.TryGetValue(type, out var kind) ? kind : null;
}
