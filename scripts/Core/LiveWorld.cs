using System.Collections.Generic;
using Godot;
using Veld.Sim;

namespace Veld.Core;

/// <summary>
/// The running scene behind IWorldQuery, read through Main on every call so the systems can be
/// composed before the world and the player exist; nothing asks until they do.
/// </summary>
public sealed class LiveWorld : IWorldQuery
{
    private readonly Main _main;
    private readonly GameData _data;

    public LiveWorld(Main main, GameData data)
    {
        _main = main;
        _data = data;
    }

    public bool IsModern => _data.World.IsModern;
    public IPlayerView? Player => _main.Player;
    public IReadOnlyList<INpcView> Npcs => _main.World.Npcs;
    public INpcView? FindNpc(string id) => _main.World.FindNpc(id);
    public IEnumerable<INpcView> Group(string group) => _main.World.Group(group);
    public void SpawnNpc(NpcData data) => _main.World.SpawnNpc(data);
    public Vector3 Ground(Vector2 xz) => _main.World.Ground(xz);
}
