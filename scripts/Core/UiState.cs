using Godot;

namespace Veld.Core;

/// <summary>True while a dialogue or menu owns the input, and the spot the player marked on the map.</summary>
public sealed class UiState
{
    public bool InputLocked { get; set; }
    public Vector3? Waypoint { get; set; }
}
