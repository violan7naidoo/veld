using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Veld.Core;

/// <summary>
/// The running scene behind IWorldQuery. Reads through Game on every call because the autoload
/// wakes before Main binds the world and the player; until then it answers with nothing.
/// </summary>
public sealed class LiveWorld : IWorldQuery
{
    private readonly Game _game;

    public LiveWorld(Game game) => _game = game;

    public bool IsModern => _game.Data.World.IsModern;
    public IPlayerView? Player => _game.Player;
    public IReadOnlyList<INpcView> Npcs => _game.World?.Npcs ?? (IReadOnlyList<INpcView>)System.Array.Empty<INpcView>();
    public INpcView? FindNpc(string id) => _game.World?.FindNpc(id);
    public IEnumerable<INpcView> Group(string group) => _game.World?.Group(group) ?? Enumerable.Empty<INpcView>();
    public void SpawnNpc(NpcData data) => _game.World?.SpawnNpc(data);
    public Vector3 Ground(Vector2 xz) => _game.World?.Ground(xz) ?? new Vector3(xz.X, 0f, xz.Y);
}
