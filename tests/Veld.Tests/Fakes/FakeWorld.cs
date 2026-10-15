using System.Collections.Generic;
using System.Linq;
using Godot;
using Veld.Sim;

namespace Veld.Tests.Fakes;

/// <summary>An in-memory world: a list of characters, one player, flat ground at height zero.</summary>
public sealed class FakeWorld : IWorldQuery
{
    public bool IsModern { get; set; } = true;
    public FakePlayer? FakePlayerView { get; set; } = new();
    public List<FakeNpc> Characters { get; } = new();
    public List<string> SpawnedIds { get; } = new();

    public IPlayerView? Player => FakePlayerView;
    public IReadOnlyList<INpcView> Npcs => Characters;
    public INpcView? FindNpc(string id) => Characters.FirstOrDefault(n => n.Id == id);
    public IEnumerable<INpcView> Group(string group) => Characters.Where(n => n.Group == group);

    public void SpawnNpc(NpcData data)
    {
        SpawnedIds.Add(data.Id);
        Characters.Add(new FakeNpc
        {
            Id = data.Id, Name = data.Name, Group = data.Group, Faction = Factions.Parse(data.Faction),
            Position = data.Position == null ? Vector3.Zero : new Vector3(data.Position[0], 0f, data.Position[1]),
        });
    }

    public Vector3 Ground(Vector2 xz) => new(xz.X, 0f, xz.Y);
}
