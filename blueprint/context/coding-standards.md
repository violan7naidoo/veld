# Coding Standards

> The conventions this codebase actually follows, written by `/adopt` on
> 2026-10-14 from the code as it is. Where a line describes where we are going
> rather than where we are, it says so. Edit to taste; `/implement` and
> `/audit` hold work to this file.

## Stack

- Godot 4.7.2 .NET, C# on .NET 8. `Veld.csproj` uses `Godot.NET.Sdk/4.7.2`
  with `Nullable` enabled and `LangVersion` latest. Keep both.
- Everything in the scene is built in code from JSON data. The four `.tscn`
  files are stubs that attach a script to a root node; do not grow them.
- Python 3 tools under `tools/` use the standard library only.

## Project layout

- `scripts/Core/` - entry point and composition root, settings, save file, input map, physics layers, the live adapters over the sim ports, the smoke and stress harnesses
- `scripts/World/` - terrain, roads, buildings, dressing, vegetation, sky, weather, map image, textures, prop helpers
- `scripts/Characters/` - player, NPCs, horse, humanoid model, camera
- `scripts/Gameplay/` - weapons, vehicles, interactables
- `scripts/UI/` - HUD, minimap, world map, dialogue box, pause menu
- `scripts/Audio/` - generated sounds
- `data/maps/<map-id>/` - one folder per map; generated files next to hand-written ones
- `sim/Veld.Sim/` - the simulation library: the `Simulation` and the sim clock, the data model and loader, money, factions, the ports (world, notifier, dialogue view, clock, dice), purse, standing, notifier, missions, dialogue, wanted system, and under `Characters/` the brain, senses and intent of every NPC and the `PlayerState` with its `WeaponState`s; no engine access
- `tests/Veld.Tests/` - unit tests over `sim/Veld.Sim/`; the test project references the library, never the game

One system per file. A file is named after the type it holds.

## C# conventions

- File-scoped namespaces: `namespace Veld.World;`
- Godot node types are `public partial class X : Node3D` (or the relevant node).
  Plain data and services are `public sealed class`.
- Late-bound node fields use `= null!`; everything else is nullable-correct.
- Naming: PascalCase for types, methods, properties and constants;
  `_camelCase` for private fields; camelCase for locals and parameters.
- Allman braces for types and members; a short body may sit on one line.
- Data classes mirror the JSON exactly and carry no behaviour. JSON is camelCase,
  comments and trailing commas allowed, read through `DataLoader.Json`.
- Cross-system notifications use C# events (`event Action<int>? MoneyChanged`),
  not Godot signals, unless a Godot node needs the signal.
- Static helper classes (`Props`, `Tex`, `Money`, `Factions`, `Layers`) hold
  pure functions and constants only.
- Physics layers come from `Layers`; never hard-code a layer bit.
- Positions in data are `[x, z]` metres from the map centre, x east, z south;
  height always comes from the terrain.

## Dependencies and seams (where we are going)

- `Main` is the composition root: it loads the data, creates the `Simulation`
  and the game-side services (notifier, settings, UI state, save file), and
  hands each node what it needs through `Init` or `Configure` before the node
  enters the tree. **There is no static access to game state.** A new node
  declares its dependencies as `Init` parameters and receives one system each
  (`Sim.Purse`, `Sim.Missions`), not the whole simulation. The one exception
  is a node that composes other nodes: `WorldBuilder` holds the `Simulation`
  as the factory for the brains and systems it hands down. A new game-side
  service is created in `Main._Ready` and passed down. The old `Game` autoload
  was retired by build plan item 18.
- Behaviour keyed on a string (`step.Type`, dialogue `action` and `condition`,
  building `kind`) belongs in a registry of small classes looked up by name,
  not in a growing `switch`. `MissionStepRegistry` and `DialogueRegistry`
  (items 16 and 17) are those registries; add new kinds there.
- Simulation logic lives in `sim/Veld.Sim/`, a plain class library (item 19a):
  the data model and loader, money, factions, the ports, purse, standing,
  notifier, missions, dialogue and the wanted system, under `Veld.Sim`,
  `Veld.Sim.Missions` and `Veld.Sim.Dialogue`. It references `GodotSharp`
  only for `Vector2`, `Vector3`, `Color` and `Mathf`, which run without an
  engine. It may not use `Node`, `GD`, `FileAccess`, `OS`, `ResourceLoader`,
  `ProjectSettings` or `Input`; the Sim fence command in `AGENTS.md` must
  print nothing. Engine access goes through a port (`IDataFiles`,
  `IWorldQuery`, `IDialogueView`...) with the live adapter in `scripts/Core/`.
  One `Simulation` owns the sim clock, purse, standing, missions, wanted and
  dialogue systems and builds them over the ports; `Main` creates it once and
  calls `Sim.Tick` from `_PhysicsProcess`, so the physics step is the fixed
  tick and the simulation never reads an engine clock (item 19b). Brains
  decide, bodies act: an `NpcBrain` (item 19c) owns a character's hostility,
  state machine, targets, shots and loot, and the `Npc` node feeds it
  `NpcSenses` every physics frame and carries out its `NpcIntent` (steering,
  raycasts, effects, animation). Dice roll through the `IRandom` port. The
  `PlayerState` (item 19d) owns health, stamina, Dead Eye, the weapons as
  `WeaponState`s, the mount and death; the `Player` node feeds it each
  frame's input and renders death, respawn and Dead Eye through its events,
  and both bodies fire through one `Shooting.Fire`. New simulation code
  goes in the library and is driven from `Tick`; item 19e makes the save a
  snapshot of the simulation.

## Content rules

From `docs/design.md`. Every character is invented. Shops get generic trade
signs, never a real business name. Real public places keep their names. No
real resident is depicted or implied. Missions are small-town stories.

## Testing

The test runner is xUnit in `tests/Veld.Tests`, run with the `test` command in
the Commands section of `AGENTS.md` (`dotnet test tests/Veld.Tests`). The test
project references `sim/Veld.Sim/Veld.Sim.csproj`, not the game, so a test run
builds the library and the tests and never the Godot project. The game project
excludes `tests/**` and `sim/**` from its own compile and both folders carry a
`.gdignore`, so the editor never sees them. The smoke run stays the
regression gate for everything that needs a scene:

    "...\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --smoke

Both must pass after every change: no `FAIL` lines, exit 0, and `dotnet build`
warning-free for the files touched.

The test gate is on. Tests are required for logic-bearing steps:

- **What to test:** pure logic where a wrong answer is possible: money
  formatting, dialogue condition and action parsing, mission step progression,
  address numbering, geometry helpers such as `OrientedRect` and `SignedArea`.
- **What not to test:** anything that needs a running Godot scene (rendering,
  physics, input, nodes). Those ride on the smoke run and screenshots.
- **The gate:** a step that adds in-scope logic ships a passing test in the same
  diff. The test command must be green before the step is approved, before any
  checkpoint commit and before `/complete` merges.
- Tests live in `tests/Veld.Tests/`, named `<Type>Tests.cs`, in a folder
  named after the one that holds the type (`Sim/` for the library root;
  `Core/`, `Gameplay/` and `Characters/` still hold the tests written before
  the move). Run them via the project's test command, not a hard-coded
  tool name. An empty suite should fail, not pass.

Stack binding: xUnit on .NET 8. Logic under test must not reach an engine API
(`Node`, `GD`, `FileAccess`, `OS`, `Input`), which is the practical reason for
the seams above; the `GodotSharp` math structs (`Vector3`, `Color`, `Mathf`)
are plain managed code and are fine.

## Test design

**Confirm the seam first.** Name the public interface under test (the spec's
Test seams block, or ask) before writing a test. No test at an unconfirmed
seam. When the interface shape is itself in question, use the vocabulary in
`blueprint/context/architecture-reference.md`.

**Tests verify behaviour, not implementation.**

```csharp
// GOOD - observable behaviour through the public interface
[Fact]
public void Mission_completes_when_last_step_reports()
{
    var missions = new MissionManager(clock, economy, notifier, data);
    missions.Start("parcel");
    missions.OnReached("parcel", step: 0);
    missions.OnTalked("mr_govender");
    missions.OnTalked("aunty_devi");
    Assert.True(missions.IsDone("parcel"));
}

// BAD - coupled to internals, breaks on refactor with no behaviour change
Assert.Equal(2, missions.StepIndex);
```

Red flags: mocking internal collaborators, testing private methods, asserting
call counts, reading state through a side channel.

**Expected values must be independent.** `Assert.Equal("£2 3s 6d",
Money.Format(522))`, never a value recomputed the way the code computes it.

**Mock only true external boundaries.** The clock, randomness and the file
system. Never mock our own modules; pass real ones.

**One vertical slice at a time.** Red before green: one failing test at the
seam, the minimum code to pass it, repeat. Refactoring happens in `/audit`,
not mid-cycle.

## Code quality

- No commented-out code.
- No unused `using` directives, fields or variables.
- Keep methods under 50 lines where practical; the world builders are the
  known exception and are split by build plan items, not inline.
- Prefer a small sealed class over a tuple once a shape is used in more than
  one place.

## Comments

Write code that explains itself; comment only what the code cannot say.

- One `/// <summary>` line on each class saying what it owns is the existing
  convention. Keep it to a line or two.
- Comment the **why**, not the **what**. Delete any comment that restates the code.
- No banner blocks or section dividers in new code. (`// ----- name` dividers
  exist in some world files; do not add more.)
- When in doubt, leave the comment out.

## Writing

- No em dashes (U+2014) in generated content: docs, comments, commit messages,
  specs. Use a hyphen for `term - description` and rephrase prose with commas,
  parentheses or a colon. Avoid en dashes and the ellipsis character too.
- British spelling (colour, metre, neighbour) in prose and strings shown to
  the player, matching the existing text.
