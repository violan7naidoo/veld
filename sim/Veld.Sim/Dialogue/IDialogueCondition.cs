namespace Veld.Sim.Dialogue;

/// <summary>Decides whether a choice is shown. Args are the colon-separated parts after the name.</summary>
public interface IDialogueCondition
{
    string Name { get; }
    bool Holds(string[] args, DialogueContext context);
}
