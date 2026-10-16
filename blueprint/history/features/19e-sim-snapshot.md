# Feature: Simulation and presentation split, part e: the save as a snapshot of the simulation

**From build-plan:** feature 19e
**Status:** complete (2026-10-16)
**Branch:** `feature/sim-snapshot`

## Goal

The save file becomes a snapshot of the simulation. `Simulation.Snapshot()`
returns one `SimSnapshot` carrying the purse, standing, sim clock, missions,
wanted state, the player's state and who is dead, and `Restore()` puts it all
back. The dead characters move into the simulation, which already creates
every brain, so `WorldBuilder.DeadNpcs` retires and a mission NPC spawned
after a load is born dead the same way. `SaveGame` shrinks to a serialiser
that writes the snapshot, reads it back, and moves the player's body. The
file keeps exactly its fields and names, so saves made before this feature
still load. This closes item 19: the simulation is the whole game state,
which is what item 26 needs to run it on a server.

## In scope

- `SimSnapshot` in `sim/Veld.Sim/`: a plain class with the save file's fourteen fields under their current names, `version` 1 kept.
- `Simulation.Snapshot()` and `Simulation.Restore(SimSnapshot)`; the simulation keeps the brains it creates and a set of dead ids, so `Snapshot` lists every dead character and `Restore` marks the existing brains dead and makes later-created brains with those ids dead at birth; `Restore` also keeps the health floor of 10 the load applies today.
- `SaveGame`: `Save` serialises `_sim.Snapshot()` to `user://save.json`; `Load` dismounts and leaves the car, calls `_sim.Restore`, has the world show its dead, moves the body to the saved position, and shows the equipped weapon on the model. Its private `SaveFile` class goes.
- `WorldBuilder.DeadNpcs` and its two checks go; `ApplyDeadNpcs` becomes `ShowDead`, which asks every body whose brain is dead to show it; `Npc.MarkDeadSilently` and `_pendingDead` become `ShowDead`, body-only and idempotent, called from `_Ready` when the brain is already dead and from the world after a load; `Npc.Die` no longer writes the world's set.
- `coding-standards.md`: the seams section loses "(where we are going)" from its heading now that item 19 is done, the seams bullet says the save is a snapshot, the layout line names it. The glossary already carries Snapshot.

## Out of scope

- Storing the mount in the save. The load dismounts and leaves the car before restoring, as today; the snapshot's `Mount` is not written. The player's position stays a body concern: the snapshot reads it through the world port and `SaveGame` applies it.
- Resurrecting a character who is dead now but alive in the save. Today's load never does that and neither does `Restore`.
- A new save-file version, migration, or extra fields (stamina, Dead Eye, the clock's day length). The shape is frozen at what loads today.
- Any behaviour change.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - The snapshot** - `SimSnapshot`, `Simulation.Snapshot`, `Simulation.Restore`, the kept brains and the dead ids, with `tests/Veld.Tests/Sim/SimulationSnapshotTests.cs` written red first. Nothing in the game changes. *Done when:* `dotnet build` clean, `dotnet test tests/Veld.Tests` green at 165 plus the six snapshot tests, the Sim fence prints nothing, `git status` shows no change under `scripts/`.
- [x] **Step 2 - The save as a serialiser, the dead in the simulation** - `SaveGame` over `Snapshot` and `Restore`; `WorldBuilder.DeadNpcs` retired and `ShowDead` in its place; `Npc.ShowDead` replaces `MarkDeadSilently` and `_pendingDead`; `Die` stops writing the world's set. *Done when:* build clean, tests green, the fence prints nothing, `grep -rn "DeadNpcs\|MarkDeadSilently\|_pendingDead" scripts/` finds nothing, `scripts/Core/SaveGame.cs` is under 60 lines, and the smoke run passes 46 of 46 including "saved to", "loaded save" and "save restored money and health".
- [x] **Step 3 - Standards** - the seams heading drops "(where we are going)", the seams bullet says the save is a snapshot and item 19 is done, the layout line names `SimSnapshot`. *Done when:* `grep -n "Snapshot" blueprint/context/coding-standards.md` shows both lines and `grep -c "where we are going" blueprint/context/coding-standards.md` is 0; build, tests and smoke unchanged from step 2 (no code in this step).

## Files / areas

- `sim/Veld.Sim/SimSnapshot.cs` (new); `sim/Veld.Sim/Simulation.cs`
- `scripts/Core/SaveGame.cs`, `scripts/World/WorldBuilder.cs`, `scripts/Characters/Npc.cs`
- `tests/Veld.Tests/Sim/SimulationSnapshotTests.cs` (new)
- `blueprint/context/coding-standards.md`

## Data / contracts

Load-bearing: the file on disk, and item 26.

```csharp
namespace Veld.Sim;

/// <summary>The whole simulation at one instant, in the save file's shape. Positions are x, y, z in metres.</summary>
public sealed class SimSnapshot
{
    public int Version { get; set; } = 1;
    public int Pence { get; set; }
    public int Reputation { get; set; }
    public float[] PlayerPosition { get; set; } = { 0, 0, 0 };   // read through the world port; the body applies it
    public float Hour { get; set; }
    public int Day { get; set; }
    public List<string> DeadNpcs { get; set; } = new();
    public List<string> CompletedMissions { get; set; } = new();
    public string? ActiveMission { get; set; }
    public int ActiveStep { get; set; }
    public int WantedLevel { get; set; }
    public int Health { get; set; }
    public string? Weapon { get; set; }
    public Dictionary<string, int> Ammo { get; set; } = new();
}

public sealed class Simulation
{
    public IReadOnlyList<NpcBrain> Characters { get; }   // every brain CreateBrain made, in creation order
    public SimSnapshot Snapshot();                       // DeadNpcs is the restored set plus every brain that has died since
    public void Restore(SimSnapshot s);                  // purse, standing, clock, wanted level, missions (which respawn the active mission's NPCs), the player's health (at least 10), ammunition and weapon, the dead set; existing brains in the set are marked dead silently
}
```

Serialised through `DataLoader.Json`, the keys are `version`, `pence`,
`reputation`, `playerPosition`, `hour`, `day`, `deadNpcs`,
`completedMissions`, `activeMission`, `activeStep`, `wantedLevel`, `health`,
`weapon`, `ammo`: the same fourteen the private `SaveFile` writes today. A
test pins them.

`CreateBrain` after the move: the brain is kept in `Characters`, and when
its id is in the dead set it is marked dead before it is returned, so the
body's `_Ready` finds `Brain.IsDead` and shows it. `WorldBuilder.ShowDead()`
runs after `Restore` for the bodies already in the world; `Npc.ShowDead()`
sets the model, the label, the collision and the velocity and may run twice.

`SaveGame` after the move, in full: the path constant, `HasSave`, `Save`
(snapshot, serialise, write, toast), `Load` (read, deserialise, dismount and
leave the car, restore, show the dead, place the body half a metre above
the saved position, show the weapon, toast).

## Testing

The test gate is on. Step 1 adds logic and ships its tests red first; steps
2 and 3 ride on the suite and the smoke run, whose save-and-load check
exercises the serialiser end to end.

- `SimulationSnapshotTests` (`tests/Veld.Tests/Sim/`), six facts over the usual fakes and the map from `SimulationTests` (500 to start, a pistol, the goto errand): a changed simulation round-trips into a fresh one (purse 650 after a paid errand, standing -2, 14:30 on day 3, wanted level 2, the errand completed, health 70, the pistol with 9 in the magazine and 20 in reserve); a killed character is listed in `DeadNpcs`; restoring a snapshot marks an existing character with that id dead silently, with no toast and no standing change; a character created after the restore with a listed id is born dead; health restores to at least 10; the snapshot carries the player's position from the world and serialises to exactly the fourteen keys above.

**Test seams**

- Public interface under test: `Simulation.Snapshot` and `Simulation.Restore` with `SimSnapshot` as the carried shape.
- Behaviour the tests prove: everything the simulation is can leave as one value and come back into another simulation unchanged, dead characters included, under the save file's existing keys.
- External boundary, local substitute, or mock: the world, notifier, dialogue view and dice are the ports; the existing fakes stand in. The file is the game's concern and is not tested here.
- Red command and expected failing signal: `dotnet test tests/Veld.Tests`; CS0246 on `SimSnapshot`.
- Full Verify command after green: none declared; `dotnet build`, `dotnet test tests/Veld.Tests`, the Sim fence from `AGENTS.md`, then the smoke run.

Manual path: play; kill an outlaw, take a hit, spend money, let the clock
run, save from the pause menu; change all of it, load; the money, time,
health and the body on the ground come back. A save from before this
feature loads the same way.

## Notes for the AI

- The private `SaveFile` class in `SaveGame.cs` is the shape to copy into `SimSnapshot`, field for field, names included; do not add or rename.
- `Restore` order: purse, standing, clock, wanted, missions, player, dead set and brains, matching today's `Load`. The health floor `Math.Max(10, s.Health)` moves with it.
- The dead set is a `HashSet<string>` on the simulation; `Snapshot` unions it with the ids of brains whose `IsDead` is true, so a character who died normally and one restored dead both appear.
- `Npc.ShowDead` is the body half of today's `MarkDeadSilently`; the brain half stays `NpcBrain.MarkDeadSilently`, called only by `Restore` and `CreateBrain`.
- `IWorldQuery.Player` can be null before the player exists; `Snapshot` then writes zeros for the position.
- No `Node`, `GD`, `FileAccess`, `Engine` or JSON in the library beyond the data classes it already has; the fence after every touch of `sim/`.
- Godot does not rebuild the C# project from the command line; build before every run.
- British spelling in player-facing text, no em dashes.
- No AI attribution in commit messages; ask before committing.
