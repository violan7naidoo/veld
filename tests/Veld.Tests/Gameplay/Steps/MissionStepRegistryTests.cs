using Godot;
using Veld.Sim;
using Veld.Sim.Missions;
using Xunit;

namespace Veld.Tests.Gameplay.Steps;

public class MissionStepRegistryTests
{
    private sealed class DeliverStep : IMissionStepKind
    {
        public string Type => "deliver";
        public Vector3? Marker(MissionStep step, IWorldQuery world, string giver) => null;
    }

    private sealed class OtherGoto : IMissionStepKind
    {
        public string Type => "goto";
        public Vector3? Marker(MissionStep step, IWorldQuery world, string giver) => null;
    }

    [Fact]
    public void The_built_in_registry_knows_the_four_shipped_kinds_and_nothing_else()
    {
        var registry = MissionStepRegistry.Builtin();

        Assert.IsType<GotoStep>(registry.Find("goto"));
        Assert.IsType<KillGroupStep>(registry.Find("kill_group"));
        Assert.IsType<TalkStep>(registry.Find("talk"));
        Assert.IsType<TalkStep>(registry.Find("return"));
        Assert.Null(registry.Find("deliver"));
    }

    [Fact]
    public void Adding_a_kind_makes_it_findable_and_replaces_a_namesake()
    {
        var registry = MissionStepRegistry.Builtin().Add(new DeliverStep()).Add(new OtherGoto());

        Assert.IsType<DeliverStep>(registry.Find("deliver"));
        Assert.IsType<OtherGoto>(registry.Find("goto"));
    }
}
