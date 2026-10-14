namespace Veld.Gameplay.Dialogue;

/// <summary>mission_step:id:n holds while that mission is active and on step n.</summary>
public sealed class MissionStepCondition : IDialogueCondition
{
    public string Name => "mission_step";

    public bool Holds(string[] args, DialogueContext context) =>
        args.Length > 1 && context.Missions.IsActive(args[0]) && int.TryParse(args[1], out var n) && context.Missions.StepIndex == n;
}
