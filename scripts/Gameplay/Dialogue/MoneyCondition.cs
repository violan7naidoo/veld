namespace Veld.Gameplay.Dialogue;

/// <summary>money:n holds when the purse has at least n pence (cents on a modern map).</summary>
public sealed class MoneyCondition : IDialogueCondition
{
    public string Name => "money";

    public bool Holds(string[] args, DialogueContext context) => args.Length > 0 && int.TryParse(args[0], out var p) && context.Economy.Pence >= p;
}
