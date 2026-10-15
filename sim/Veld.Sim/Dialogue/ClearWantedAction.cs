
namespace Veld.Sim.Dialogue;

/// <summary>clear_wanted:fee withdraws the complaint for a fee the player must be able to pay; without a fee it is free.</summary>
public sealed class ClearWantedAction : IDialogueAction
{
    public string Name => "clear_wanted";

    public bool Apply(string[] args, DialogueContext context)
    {
        if (args.Length > 0 && int.TryParse(args[0], out var fee))
        {
            if (context.Economy.TrySpend(fee))
            {
                context.Wanted.ClearWanted();
                context.Notifier.Notify("Paid " + Money.Format(fee) + ". The complaint is withdrawn.");
            }
            else context.Notifier.Notify("You cannot afford the fine.");
        }
        else context.Wanted.ClearWanted();
        return false;
    }
}
