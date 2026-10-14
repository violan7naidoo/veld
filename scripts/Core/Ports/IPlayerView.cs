using Godot;

namespace Veld.Core;

/// <summary>What the gameplay systems may know about the player, and the two things dialogue can do to them.</summary>
public interface IPlayerView
{
    Vector3 Position { get; }
    void Heal();
    void GiveAmmo(string weaponId, int count);
}
