namespace Veld.Core;

/// <summary>Physics layer bits. Keep in sync with [layer_names] in project.godot.</summary>
public static class Layers
{
    public const uint Terrain = 1u << 0;
    public const uint Buildings = 1u << 1;
    public const uint Characters = 1u << 2;
    public const uint Horses = 1u << 3;
    public const uint Props = 1u << 4;
    public const uint Train = 1u << 5;

    /// <summary>Everything a body walks into.</summary>
    public const uint Solid = Terrain | Buildings | Props | Train;
    /// <summary>Everything a bullet can hit.</summary>
    public const uint Shootable = Solid | Characters | Horses;
}
