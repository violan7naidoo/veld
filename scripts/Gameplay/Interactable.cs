using Veld.Characters;

namespace Veld.Gameplay;

/// <summary>Anything the player can press E on. The HUD shows Prompt when the player is near and looking at it.</summary>
public interface IInteractable
{
    string Prompt { get; }
    bool CanInteract(Player player);
    void Interact(Player player);
}
