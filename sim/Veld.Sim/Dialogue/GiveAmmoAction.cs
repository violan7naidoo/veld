namespace Veld.Sim.Dialogue;

/// <summary>give_ammo:weapon:count tops up one of the player's weapons.</summary>
public sealed class GiveAmmoAction : IDialogueAction
{
    public string Name => "give_ammo";

    public bool Apply(string[] args, DialogueContext context)
    {
        if (args.Length > 1 && int.TryParse(args[1], out var count)) context.World.Player?.GiveAmmo(args[0], count);
        return false;
    }
}
