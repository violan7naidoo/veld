using System.Collections.Generic;
using Godot;
using Veld.Core;

namespace Veld.Tests.Fakes;

public sealed class FakePlayer : IPlayerView
{
    public Vector3 Position { get; set; }
    public int Heals { get; private set; }
    public List<(string WeaponId, int Count)> AmmoGiven { get; } = new();

    public void Heal() => Heals++;
    public void GiveAmmo(string weaponId, int count) => AmmoGiven.Add((weaponId, count));
}
