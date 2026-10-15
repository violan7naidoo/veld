namespace Veld.Sim.Dialogue;

/// <summary>heal restores the player's health.</summary>
public sealed class HealAction : IDialogueAction
{
    public string Name => "heal";

    public bool Apply(string[] args, DialogueContext context)
    {
        context.World.Player?.Heal();
        return false;
    }
}
