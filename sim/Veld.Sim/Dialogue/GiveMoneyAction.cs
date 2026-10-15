namespace Veld.Sim.Dialogue;

/// <summary>give_money:pence pays the player and drops the coins at the character they are talking to.</summary>
public sealed class GiveMoneyAction : IDialogueAction
{
    public string Name => "give_money";

    public bool Apply(string[] args, DialogueContext context)
    {
        if (args.Length > 0 && int.TryParse(args[0], out var pence))
        {
            context.Economy.Add(pence);
            context.Notifier.Coins(context.Npc.Position);
        }
        return false;
    }
}
