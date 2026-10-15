using Godot;

namespace Veld.Sim;

public enum Faction { Civilian, British, Boer, Outlaw }

public static class Factions
{
    /// <summary>Set from the map's era: modern outfits and labels (police instead of the British Army, gangs instead of outlaws).</summary>
    public static bool Modern { get; set; }

    public static Faction Parse(string s) => s?.ToLowerInvariant() switch
    {
        "british" => Faction.British,
        "boer" => Faction.Boer,
        "outlaw" => Faction.Outlaw,
        _ => Faction.Civilian,
    };

    public static string Label(Faction f) => f switch
    {
        Faction.British => Modern ? "Police" : "British Army",
        Faction.Boer => Modern ? "Resident" : "Burgher",
        Faction.Outlaw => Modern ? "Gang" : "Outlaw",
        _ => Modern ? "Resident" : "Townsfolk",
    };

    private static readonly Color[] Tees = { new(0.85f, 0.2f, 0.2f), new(0.2f, 0.35f, 0.7f), new(0.95f, 0.95f, 0.9f), new(0.15f, 0.15f, 0.15f), new(0.9f, 0.7f, 0.2f), new(0.3f, 0.6f, 0.4f), new(0.6f, 0.3f, 0.6f), new(0.95f, 0.6f, 0.3f) };

    private static (Color coat, Color trousers, Color hat, bool helmet) ModernOutfit(Faction f, int variant) => f switch
    {
        Faction.British => (new Color(0.12f, 0.18f, 0.38f), new Color(0.1f, 0.12f, 0.22f), new Color(0.1f, 0.14f, 0.3f), false),
        Faction.Outlaw => (variant % 2 == 0 ? new Color(0.15f, 0.15f, 0.17f) : new Color(0.3f, 0.3f, 0.32f), new Color(0.12f, 0.12f, 0.14f), new Color(0.1f, 0.1f, 0.1f), false),
        _ => (Tees[variant % Tees.Length], variant % 3 == 0 ? new Color(0.12f, 0.12f, 0.14f) : new Color(0.2f, 0.28f, 0.45f), variant % 2 == 0 ? new Color(0.1f, 0.1f, 0.12f) : Tees[(variant + 3) % Tees.Length], false),
    };

    /// <summary>Clothing colours for the blocky models.</summary>
    public static (Color coat, Color trousers, Color hat, bool helmet) Outfit(Faction f, int variant) => Modern ? ModernOutfit(f, variant) : f switch
    {
        Faction.British => (new Color(0.55f, 0.5f, 0.33f), new Color(0.5f, 0.45f, 0.3f), new Color(0.7f, 0.65f, 0.5f), true),
        Faction.Boer => (variant % 2 == 0 ? new Color(0.35f, 0.25f, 0.15f) : new Color(0.25f, 0.28f, 0.3f), new Color(0.3f, 0.25f, 0.2f), new Color(0.25f, 0.18f, 0.1f), false),
        Faction.Outlaw => (new Color(0.2f, 0.15f, 0.12f), new Color(0.22f, 0.2f, 0.18f), new Color(0.12f, 0.1f, 0.08f), false),
        _ => (variant % 3) switch
        {
            0 => (new Color(0.6f, 0.4f, 0.3f), new Color(0.3f, 0.3f, 0.35f), new Color(0.5f, 0.4f, 0.25f), false),
            1 => (new Color(0.3f, 0.35f, 0.5f), new Color(0.25f, 0.22f, 0.2f), new Color(0.2f, 0.2f, 0.2f), false),
            _ => (new Color(0.55f, 0.5f, 0.45f), new Color(0.35f, 0.3f, 0.25f), new Color(0.45f, 0.35f, 0.2f), false),
        },
    };
}
