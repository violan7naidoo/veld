namespace Veld.Sim.Dialogue;

/// <summary>end closes the conversation; anything after it in the same list is skipped.</summary>
public sealed class EndAction : IDialogueAction
{
    public string Name => "end";

    public bool Apply(string[] args, DialogueContext context) => true;
}
