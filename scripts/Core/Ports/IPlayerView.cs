using Godot;

namespace Veld.Core;

/// <summary>What the gameplay systems may know about the player.</summary>
public interface IPlayerView
{
    Vector3 Position { get; }
}
