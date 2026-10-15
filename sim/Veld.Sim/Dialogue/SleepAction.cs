namespace Veld.Sim.Dialogue;

/// <summary>sleep:hours moves the clock forward.</summary>
public sealed class SleepAction : IDialogueAction
{
    public string Name => "sleep";

    public bool Apply(string[] args, DialogueContext context)
    {
        if (args.Length > 0 && float.TryParse(args[0], out var hours)) context.Clock.Advance(hours);
        return false;
    }
}
