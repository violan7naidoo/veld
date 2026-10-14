using System.Collections.Generic;
using Godot;

namespace Veld.Core;

/// <summary>
/// The world as the gameplay systems question it: who is where, who is dead, where the ground is.
/// The live adapter reads the scene; tests use an in-memory one.
/// </summary>
public interface IWorldQuery
{
    bool IsModern { get; }
    IPlayerView? Player { get; }
    IReadOnlyList<INpcView> Npcs { get; }
    INpcView? FindNpc(string id);
    IEnumerable<INpcView> Group(string group);
    void SpawnNpc(NpcData data);
    Vector3 Ground(Vector2 xz);
}
