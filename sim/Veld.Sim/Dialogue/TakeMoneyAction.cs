namespace Veld.Sim.Dialogue;

/// <summary>take_money:pence charges the player if they can pay; otherwise nothing happens.</summary>
public sealed class TakeMoneyAction : IDialogueAction
{
    public string Name => "take_money";

    public bool Apply(string[] args, DialogueContext context)
    {
        if (args.Length > 0 && int.TryParse(args[0], out var pence)) context.Economy.TrySpend(pence);
        return false;
    }
}
