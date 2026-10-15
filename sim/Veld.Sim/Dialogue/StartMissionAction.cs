namespace Veld.Sim.Dialogue;

/// <summary>start_mission:id starts that mission if it is available.</summary>
public sealed class StartMissionAction : IDialogueAction
{
    public string Name => "start_mission";

    public bool Apply(string[] args, DialogueContext context)
    {
        if (args.Length > 0) context.Missions.Start(args[0]);
        return false;
    }
}
