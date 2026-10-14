using Godot;

namespace Veld.Core;

/// <summary>Feedback to the player that the gameplay systems raise: a toast line, the coin sound at a spot.</summary>
public interface INotifier
{
    void Notify(string message);
    void Coins(Vector3 at);
}
