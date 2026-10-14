namespace Veld.Gameplay.Dialogue;

/// <summary>Runs when a choice is picked. True means the conversation must end now. Args are the colon-separated parts after the name.</summary>
public interface IDialogueAction
{
    string Name { get; }
    bool Apply(string[] args, DialogueContext context);
}
