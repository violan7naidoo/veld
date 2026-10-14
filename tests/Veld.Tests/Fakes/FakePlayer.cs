using Godot;
using Veld.Core;

namespace Veld.Tests.Fakes;

public sealed class FakePlayer : IPlayerView
{
    public Vector3 Position { get; set; }
}
