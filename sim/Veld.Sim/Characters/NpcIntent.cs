using Godot;

namespace Veld.Sim.Characters;

/// <summary>
/// What the brain asks of its body in one tick: rest (gravity only), or turn towards FaceTo and move along
/// the body's forward at Speed (negative to back off), fire one round with SpreadDegrees, start a reload,
/// and whether to show the name.
/// </summary>
public readonly record struct NpcIntent(
    NpcState State,
    bool Rest,
    Vector3? FaceTo,
    float Speed,
    bool Fire,
    float SpreadDegrees,
    bool Reload,
    bool ShowLabel);
