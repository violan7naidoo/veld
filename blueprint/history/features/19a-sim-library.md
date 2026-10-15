# Feature: Simulation and presentation split, part a: the Veld.Sim library

**From build-plan:** feature 19a
**Status:** complete (2026-10-15)
**Branch:** `feature/sim-library`

## Goal

The code that already has no node dependency moves into its own assembly,
`Veld.Sim`, under `sim/Veld.Sim/`, so the compiler and a grep enforce the
line the standards only describe: simulation logic never touches the engine.
The data model and its loader (behind a file port, because an exported build
reads its data from the pack), money, factions, the eight ports, the purse,
standing and notifier, the mission system with its step kinds, the dialogue
system with its registry, and the wanted system all move, under `Veld.Sim`
namespaces. The game references the library; the tests reference only the
library and no longer build the Godot project. The game plays identically:
smoke run 46 of 46, 107 tests green with only their `using` lines changed.

## In scope

- `sim/Veld.Sim/Veld.Sim.csproj`: a plain `Microsoft.NET.Sdk` class library, net8.0, nullable, latest language, with one package reference, `GodotSharp` 4.7.2, for `Vector2`, `Vector3`, `Color` and `Mathf`, which are managed code that runs without the engine. A `sim/.gdignore` keeps the editor out of the folder, as `tests/.gdignore` does.
- `Veld.csproj` gains `<Compile Remove="sim/**" />` and a project reference to the library; `tests/Veld.Tests/Veld.Tests.csproj` swaps its game reference for the library.
- Moves, with namespaces: `Data.cs` model classes, `Money` and `Faction` with `Factions` to `Veld.Sim`; `DataLoader` to `Veld.Sim` with an `IDataFiles` port and two log delegates in place of `FileAccess`, `GD` and `OS`; the ports, `Purse`, `Reputation`, `Notifier` to `Veld.Sim`; `MissionManager` and the step kinds to `Veld.Sim.Missions`; `DialogueSystem` and its kinds to `Veld.Sim.Dialogue`; `WantedSystem` to `Veld.Sim`.
- `scripts/Core/GodotDataFiles.cs`, the game's `IDataFiles` over `FileAccess`; `Main` passes it with `GD.Print`, `GD.PushError` and the `--map=` argument.
- Every `using` in the game and the tests updated; the `.uid` sidecars of moved files deleted.
- `coding-standards.md` and `AGENTS.md` updated: the layout gains `sim/Veld.Sim/`, the testing section says engine APIs rather than all `Godot.*` types, and the seams bullet about `Veld.Sim` becomes present tense.

## Out of scope

- A `Simulation` object, a sim clock or a fixed tick: 19b. `Main` keeps composing the systems one by one and ticking them in `_Process`.
- `Weapon`, which raycasts through nodes; `SaveGame`, which uses `FileAccess` and reads nodes; `Settings`, `UiState`, `Layers` and the live adapters: all stay in the game.
- Any behaviour change. Not one line of logic moves except the loader's file and log calls, which go through the port.
- Converting `Vector3` and friends to a non-Godot math type; the structs are managed code and a headless server can load `GodotSharp.dll` without an engine.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - The library and the data** - the two project files, `sim/.gdignore`, and the game's `Compile Remove` and project reference; `Data.cs` split into `sim/Veld.Sim/Data.cs` (the model, namespace `Veld.Sim`), `sim/Veld.Sim/DataLoader.cs` (with `IDataFiles` and the two log delegates), `sim/Veld.Sim/Money.cs`; `scripts/Characters/Faction.cs` to `sim/Veld.Sim/Faction.cs`; `scripts/Core/GodotDataFiles.cs` added; `Main` loads through it; `using Veld.Sim;` added wherever the compiler asks; sidecars removed. *Done when:* `dotnet build` is clean, `dotnet test tests/Veld.Tests` is green at 107, `grep -rE "GD\.|FileAccess|ResourceLoader|\bOS\.|\bNode\b|Node3D" sim/` finds nothing, `Veld.Sim.dll` sits in `.godot/mono/temp/bin/Debug/`, and the smoke run passes 46 of 46 including "addresses:" and "overrides:" so the loader ran through the port.
- [x] **Step 2 - Ports, purse, standing, notifier** - the eight port files, `Purse`, `Reputation` and `Notifier` move to `sim/Veld.Sim/` under `Veld.Sim`; usings follow. *Done when:* build clean, 107 green, the grep finds nothing, smoke 46 of 46 including "save restored money and health".
- [x] **Step 3 - Missions** - `MissionManager` and the `Steps` folder move to `sim/Veld.Sim/Missions/` under `Veld.Sim.Missions`; usings follow, including the three mission test files. *Done when:* build clean, 107 green, smoke 46 of 46 including "mission 'parcel' completed by walking its steps" and "HUD has an objective marker".
- [x] **Step 4 - Dialogue and wanted** - `DialogueSystem` and the `Dialogue` folder move to `sim/Veld.Sim/Dialogue/` under `Veld.Sim.Dialogue`; `WantedSystem` moves to `sim/Veld.Sim/` under `Veld.Sim`; usings follow, including the five dialogue and wanted test files. *Done when:* build clean, 107 green, `scripts/Gameplay/` holds only `Interactable.cs`, `Vehicle.cs` and `Weapon.cs`, smoke 46 of 46 including "dialogue opened and input locked" and the police toast.
- [x] **Step 5 - Tests on the library, docs** - the test project references `sim/Veld.Sim/Veld.Sim.csproj` instead of `Veld.csproj`; the standards and `AGENTS.md` paragraphs update. *Done when:* `dotnet test tests/Veld.Tests` is green at 107 and its build output names `Veld.Sim.dll` but not `Veld.dll`, `dotnet build` at the root is clean, the smoke run passes 46 of 46, and `grep -n "Veld.Sim" blueprint/context/coding-standards.md AGENTS.md` shows the layout line, the testing rule and the seams bullet.

## Files / areas

- `sim/Veld.Sim/Veld.Sim.csproj`, `sim/.gdignore` (new)
- `sim/Veld.Sim/Data.cs`, `DataLoader.cs`, `Money.cs`, `Faction.cs`, `Purse.cs`, `Reputation.cs`, `Notifier.cs`, `WantedSystem.cs`, `Ports/*.cs`, `Missions/*.cs`, `Dialogue/*.cs` (moved)
- `scripts/Core/GodotDataFiles.cs` (new); `scripts/Core/Main.cs`; `Veld.csproj`; `tests/Veld.Tests/Veld.Tests.csproj`
- `using` lines across `scripts/` and `tests/`
- `blueprint/context/coding-standards.md`, `AGENTS.md`

## Data / contracts

Load-bearing for 19b to 19d, which build inside this assembly, and for item
26, which runs it on a server.

```csharp
namespace Veld.Sim;

/// <summary>Where the data files come from: the Godot pack in the game, a folder in tests and tools.</summary>
public interface IDataFiles
{
    bool Exists(string path);
    string? ReadText(string path);   // null when the file cannot be opened
}

public static class DataLoader
{
    public static readonly JsonSerializerOptions Json;
    public static GameData LoadAll(IDataFiles files, Action<string> info, Action<string> error, string? mapOverride = null);
    public static T Load<T>(IDataFiles files, string path, Action<string> error) where T : new();
    public static T LoadOptional<T>(IDataFiles files, string path, Action<string> error) where T : new();
    public static Vector2 XZ(float[]? a);
}
```

Paths stay `res://` strings; the port owns what they mean. Namespaces after
the move:

| Code | Namespace |
| --- | --- |
| data model, loader, money, factions, ports, purse, standing, notifier, wanted | `Veld.Sim` |
| mission manager, step registry and kinds | `Veld.Sim.Missions` |
| dialogue system, registry, terms, context, conditions and actions | `Veld.Sim.Dialogue` |

The library may use `Godot.Vector2`, `Vector3`, `Color` and `Mathf`. It may
not use `Node` or anything derived from it, `GD`, `FileAccess`,
`ResourceLoader`, `OS`, `ProjectSettings` or `Input`; the grep in every
done-when is the fence.

## Testing

The test gate is on. No step adds logic; the suite is the oracle for the
move and must stay at 107 with only `using` lines changed. One new piece of
code has a branch worth a test: the loader through the port.

- `DataLoaderTests` (`tests/Veld.Tests/Sim/`): with a fake `IDataFiles` holding `data/config.json` and one map folder, `LoadAll` returns the map's name and folder; a missing optional file yields an empty object and no error; a missing required file yields an empty object and one error message naming the path; bad JSON yields an empty object and one error message naming the path. Written red first in step 1 against the missing `IDataFiles`.

**Test seams**

- Public interface under test: `DataLoader` through `IDataFiles`.
- Behaviour the tests prove: the loader reads whatever the port gives it and reports a missing or broken file once, by path, without throwing.
- External boundary, local substitute, or mock: the file system is the boundary; a dictionary-backed fake replaces it.
- Red command and expected failing signal: `dotnet test tests/Veld.Tests`; CS0246 on `IDataFiles`.
- Full Verify command after green: none declared; `dotnet build`, `dotnet test tests/Veld.Tests`, then the smoke run from `AGENTS.md`.

Manual path: start the game; it loads the same map, the same addresses and
the same overrides as before, and the console still prints the data summary
line and the overrides line.

## Notes for the AI

- Move with `git mv` so history follows the files, then edit namespaces and usings; delete the `.uid` sidecar of every moved file.
- Change nothing inside a moved file except its namespace, its usings, and in the loader the five calls that go through the port and the delegates.
- Build after each move and let the compiler list the usings to add; `scripts/Core` keeps `Main`, `Settings`, `UiState`, `SaveGame`, `Layers`, the adapters and harnesses under `Veld.Core`.
- If `GodotSharp` 4.7.2 is not on the feed, reference the engine's own copy with a `HintPath` to `GodotSharp.dll` under the Godot folder's `GodotSharp/Api/Release/`, and say so in the step report.
- `OS.GetCmdlineUserArgs()` leaves the loader; `Main` reads `--map=` and passes it as `mapOverride`.
- Godot does not rebuild the C# project from the command line; build before every run.
- British spelling in player-facing text, no em dashes.
- No AI attribution in commit messages; ask before committing.
