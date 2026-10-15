# Feature: Retire the Game singleton, part e: NPCs, horse and vehicles off the singleton

**From build-plan:** feature 18e
**Status:** complete (2026-10-15)
**Branch:** `feature/characters-off-singleton`

## Goal

The three spawned node types stop reaching for `Game.Instance`. `WorldBuilder`
is the only thing that creates them, at build time and again at runtime when a
mission spawns a character, so it receives the services once through an
`Init` from `Main` and hands each node what it needs through the node's own
`Init` before it enters the tree. The 36 call sites in `Npc.cs`, `Horse.cs`
and `Vehicle.cs` go to zero, the project count drops from 48 to 12, and the
`TrySpend` forwarder leaves `Game`. (Corrected during `/implement`: `AddMoney`
was listed too, but `DevSmoke` calls it through its `g` local, which the
`Game.Instance` grep never counted; it stays until 18f.) What remains after this feature is exactly `Main`, `DevSmoke` and
`DevCarStress`, which 18f takes with the autoload itself. The game plays
identically: smoke run 46 of 46, car stress clean, 107 tests untouched.

## In scope

- `WorldBuilder.Init(Main, Purse, Reputation, Notifier, MissionManager, DialogueSystem, WantedSystem)`, stored in fields, called by `Main` before `Build` because parked cars are created inside `Build`.
- `Horse.Init(Main, GameData, Notifier)` called in `SpawnHorse`; `Vehicle.Init(Main, Purse, Notifier)` called in `AddVehicle`, which every vehicle passes through; `Npc.Init(Main, GameData, Purse, Reputation, Notifier, MissionManager, DialogueSystem, WantedSystem)` called in `SpawnNpc`, which also serves mission spawns through `LiveWorld`.
- The 36 reads become field reads. Dead scene-binding guards go with them: `Player == null`, `World?.Terrain`, `World != null`, `Main?.Sfx`, `DayNight?.NightFactor`; the one guard that is not about the scene, `if (_engine != null)` after the engine loop is made, stays as it is.
- Forwarder `TrySpend` deleted from `Game` in step 2. `AddMoney` stays: `DevSmoke` still calls it.

## Out of scope

- `Main`, `DevSmoke`, `DevCarStress`, the live adapters, `Game._Process` and the autoload entry: 18f.
- The `Player`, `World` and `DayNight` shortcuts on `Game`: `Game._Process` and the dev harnesses still read them until 18f.
- Narrowing what `Npc` sees of the game; item 19 moves its decisions into the simulation and the node becomes a view. The eight-parameter `Init` is the honest coupling of today's node, written down.
- Unit tests: every change swaps a static read for a field read inside a Godot node; the smoke run, the car stress run and one horse-enabled run are the evidence.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - WorldBuilder as the factory, and the horse** - `WorldBuilder.Init` with its seven fields; `Main` calls it right after `new WorldBuilder` and before `AddChild`; `Horse.Init(Main, GameData, Notifier)` with three fields, called in `SpawnHorse` between `Instantiate` and `AddChild`; the seven `Horse` lines (45 name from the data, 157 and 318 sound, 159 and 161 notify, 225 player with its null arm dropped, 257 terrain with its null check dropped) become field reads. *Done when:* `dotnet build` is clean, `grep -c "Game.Instance"` is 0 in `Horse.cs` and 41 across `scripts/`, the plain smoke run passes 46 of 46, and a smoke run with `"horseEnabled": true` set temporarily in `world.json` passes "horse spawned" and "horse is the interaction focus", after which the edit is reverted and `git status` shows `world.json` clean. (Corrected during `/implement`: the harness places the horse and the car on the same spot in front of the player when a map has both, so "mounted the horse" and "horse stamina drained" fail identically on main's code; that is a harness conflict for a later `/fix`, not evidence about this step.)
- [x] **Step 2 - Vehicle** - `Vehicle.Init(Main, Purse, Notifier)` with three fields, called in `AddVehicle` before `AddChild`; the eleven lines (142 engine loop, 161 and 175 notify, 162 purse, 167 and 183 terrain with their null checks dropped, 174 and 225 horn, 260 night factor, 274 and 277 hit sound) become field reads; `TrySpend` forwarder deleted from `Game`. *Done when:* `dotnet build` is clean, the count is 0 in `Vehicle.cs` and 30 across `scripts/`, `TrySpend` no longer appears in `Game.cs`, the smoke run passes 46 of 46 including "got into the car", "got out of the car beside it", "H brings the car back for R20" and "parked car stayed put", and the car stress run ends with zero failures out of 40.
- [x] **Step 3 - Npc** - `Npc.Init` with its eight fields, called in `SpawnNpc` right after `npc.Data = data`; the eighteen lines become field reads, with the `World != null` guard on 129 and the `player == null` return on 195 dropped. *Done when:* `dotnet build` is clean, the count is 0 in `Npc.cs` and 12 across `scripts/` (5 in `Main.cs`, 5 in `DevSmoke.cs`, 2 in `DevCarStress.cs`), and the smoke run passes 46 of 46 including "npcs spawned", "dialogue opened and input locked", "mission 'parcel' completed by walking its steps", "save restored money and health" and the police check.

## Files / areas

- `scripts/World/WorldBuilder.cs` (the `Init`, three spawn sites)
- `scripts/Characters/Horse.cs`, `scripts/Characters/Npc.cs`, `scripts/Gameplay/Vehicle.cs`
- `scripts/Core/Main.cs` (one `Init` call; its own `Game.Instance` uses untouched)
- `scripts/Core/Game.cs` (two forwarders removed)

## Data / contracts

Load-bearing for 18f, which calls `WorldBuilder.Init` from `Main` with
objects `Main` then owns, and for item 19, which replaces the `Main`
parameter with views.

```csharp
namespace Veld.World;

public partial class WorldBuilder : Node3D
{
    /// <summary>Call before Build; every character and vehicle this builder spawns is given these.</summary>
    public void Init(Main main, Purse purse, Reputation reputation, Notifier notifier, MissionManager missions, DialogueSystem dialogue, WantedSystem wanted);
}

namespace Veld.Characters;

public partial class Horse : CharacterBody3D, IInteractable
{
    public void Init(Main main, GameData data, Notifier notifier);
}

public partial class Npc : CharacterBody3D, IInteractable, INpcView
{
    public void Init(Main main, GameData data, Purse purse, Reputation reputation, Notifier notifier, MissionManager missions, DialogueSystem dialogue, WantedSystem wanted);
}

namespace Veld.Gameplay;

public partial class Vehicle : VehicleBody3D, IInteractable
{
    public void Init(Main main, Purse purse, Notifier notifier);
}
```

Each `Init` stores its arguments in `= null!` fields and must run before
the node enters the tree: `Npc._Ready` reads its weapon from the data and
`Horse._Ready` reads its name.

Each node's reads, for the record:

| Node | Through `Main` | Services and systems |
| --- | --- | --- |
| `Horse` | `Sfx` (whistle, hoof), `Player`, `World.Terrain` | `Data.World.HorseName`, `Notifier.Notify` |
| `Vehicle` | `Sfx` (engine, horn, hit), `World.Terrain`, `DayNight.NightFactor` | `Purse.TrySpend`, `Notifier.Notify` |
| `Npc` | `Sfx` (coins, gunshot), `Player`, `World` (group, dead set, ground) | `Data.Weapons`, `Purse.Add`, `Reputation` (lose, hostile), `Notifier.Notify`, `Missions.OnNpcKilled`, `Dialogue.Start`, `Wanted` (level, report, gunfire) |

## Testing

The test gate is on, but no step here carries logic a unit test could reach.
The suite must stay at 107 green. Evidence is the smoke run after each step,
the car stress run after step 2 because vehicles change, and for step 1 one
run with the horse enabled, since this map ships with `"horseEnabled": false`
and the plain run never touches `Horse`:

    "...\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --smoke
    "...\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --carstress=40 --quality=0

Manual path: whistle for the car with H and it is brought round for R20;
get in, drive at night and the headlights come on; run into a resident and
the wanted stars appear; talk to a resident and the dialogue opens; shoot a
thief, loot the body and the money and reputation change; save, quit, load,
and the thief stays dead.

## Notes for the AI

- Change nothing but the source of each value. Lift each line as it is; `Game.Instance.X` becomes `_x` or `_main.X`, nothing else moves.
- Order matters in `Main`: `World.Init(...)` goes between `new WorldBuilder` and `AddChild(World)`, because `Build` creates the parked cars through `AddVehicle` and each needs its `Init`.
- `WorldBuilder` must keep the services in fields, not parameters, because `SpawnNpc` is called again at runtime for mission spawns through `LiveWorld`.
- `Main.Player`, `Main.Sfx`, `Main.World` and `Main.DayNight` all exist before any of these nodes is spawned (`Main` creates the player and the world before `SpawnHorse`, `SpawnPlayerCar` and `SpawnNpcs`, and parked cars only read them at runtime), so the scene guards go. Keep `if (_engine != null)` in `Vehicle`: it guards a return value, not the scene.
- Build before every Godot run; Godot does not rebuild the C# project from the command line.
- Revert the temporary `world.json` edit before the step is presented; the done-when checks it.
- Count with `grep -rc "Game.Instance" scripts/ | awk -F: '{s+=$2} END {print s}'` and quote the number.
- British spelling in player-facing text, no em dashes, no new `Game.Instance` call sites anywhere.
- No AI attribution in commit messages; ask before committing.
