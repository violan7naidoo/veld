namespace Veld.Sim.Dialogue;

/// <summary>talk_done tells the mission manager the player has finished talking to this character.</summary>
public sealed class TalkDoneAction : IDialogueAction
{
    public string Name => "talk_done";

    public bool Apply(string[] args, DialogueContext context)
    {
        context.Missions.OnTalked(context.Npc.Id);
        return false;
    }
}
