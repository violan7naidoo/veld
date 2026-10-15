# Feature: Retire the Game singleton, part f: delete the autoload

**From build-plan:** feature 18f
**Status:** complete (2026-10-15)
**Branch:** `feature/delete-autoload`

## Goal

`Main` becomes the composition root: it loads the data and the settings,
creates the purse, standing, notifier, UI state and save file, composes the
mission, wanted and dialogue systems over live adapters that read `Main`, and
ticks the systems in its own `_Process`. The two dev harnesses and `Main`'s
own key handler read `Main` instead of the static. Then `Game.cs` and the
`[autoload]` entry are deleted. After this feature `Game.Instance` appears
nowhere, no static holds game state, and item 18 is complete. The game plays
identically: smoke run 46 of 46, car stress clean, 107 tests untouched.

## In scope

- `Main` gains public `Data`, `Settings`, `Purse`, `Reputation`, `Notifier`, `Ui`, `SaveGame`, `Missions`, `Dialogue` and `Wanted`, created at the top of `_Ready` in the order `Game._Ready` used, before any node is built; `Main._Process` takes over `Missions.Update`, `Wanted.Update` and the grass bend.
- `LiveWorld(Main, GameData)`, `LiveClock(Main)` and `LiveDialogueView(Main, UiState)` replace the `Game`-based adapters; they still read `Main`'s nodes on every call, but the "nothing until bound" fallbacks go because the only reason for them was the autoload waking first.
- `DevSmoke.Init(Main)`, `DevCarStress.Init(Main)` and `UiInput.Init(Main)`, called by `Main` before `AddChild`; every `g.` and `Game.` read in the harnesses becomes a read of `Main`, with `Pence`, `AddMoney`, `InputLocked`, `GraphicsQuality`, `Save`, `Load`, `HasSave` and `SavePath` reaching `Purse`, `Ui`, `Settings` and `SaveGame` directly.
- `Main.HandleUiInput` reads `Ui.InputLocked` and `Notifier.Notify`.
- `Game.cs` deleted, the `[autoload]` block removed from `project.godot`, and the "Dependencies and seams" paragraph of `blueprint/context/coding-standards.md` rewritten for the new rule: `Main` composes, nodes receive through `Init`, nothing static.

## Out of scope

- Moving the composition of the simulation (`Purse`, `Reputation`, `Missions`, `Wanted`, `Dialogue`) out of `Main` into a `Veld.Sim` object; item 19. `Main` is the Godot composition root until then.
- The harness conflict when a map has both a horse and a car; a separate `/fix`.
- Regenerating `project-overview.md`, which still describes the singleton in its problem statement; it is distilled from the plans and `/overview` refreshes it when the plans change.
- Unit tests: composition and process order live in Godot nodes; the smoke run, car stress run and the suite staying green are the evidence.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - Main composes everything** - `Main` gets the ten properties and creates them at the top of `_Ready` (`InputSetup`, `Settings.Load`, `DataLoader.LoadAll`, `Factions.Modern`, `Purse`, the three adapters and three systems, `SaveGame` with `Main = this`, `Notifier.CoinsDropped` wired to `Sfx`); every `game.X` in `_Ready` becomes `X`; `Main._Process` ticks the systems and bends the grass; the three adapters take `Main` (and `GameData`, `UiState`) and lose their null fallbacks; `Game` keeps `Instance`, `Bind` and `Main` and becomes one forwarder line per member the harnesses and `HandleUiInput` still reach, all reading `Main!`, with its `_Ready` reduced to `Instance = this` and no `_Process`. *Done when:* `dotnet build` is clean, 107 tests green, `grep -c "Game.Instance"` is still 12 across `scripts/`, `grep -c "new \|Update(" scripts/Core/Game.cs` is 0, and the smoke run passes 46 of 46 including "mission 'parcel' completed by walking its steps", "HUD has an objective marker", "dialogue opened and input locked", "pause menu opens and pauses the tree" and "save restored money and health".
- [x] **Step 2 - Harnesses and the key handler read Main** - `DevSmoke`, `DevCarStress` and `UiInput` gain `Init(Main)` and `Main` calls it; `var g = Game.Instance` becomes the field, the member reads map as listed under In scope, `Game.SavePath` becomes `SaveGame.Path`, the `DayNight != null` guard in `DevSmoke._Ready` goes; `HandleUiInput` reads `Ui` and `Notifier`; the forwarders those callers used leave `Game`. *Done when:* `dotnet build` is clean, the count is 1 across `scripts/` (the `Bind` line in `Main`), `Game.cs` holds only `Instance`, `Main`, `_Ready` and `Bind`, the smoke run passes 46 of 46, and the car stress run ends with zero failures out of 40.
- [x] **Step 3 - Delete the autoload** - the `Bind` lines go from `Main`, `Game.cs` is deleted, the `[autoload]` block leaves `project.godot`, and the coding-standards paragraph is rewritten. *Done when:* `dotnet build` is clean with 0 warnings, 107 tests green, `grep -rn "Game\.Instance\|\bGame\b" scripts/` finds only the two toast strings in `SaveGame.cs`, `project.godot` has no `[autoload]` section, the smoke run passes 46 of 46, the car stress run ends with zero failures out of 40, and `coding-standards.md` no longer mentions `Game.Instance`.

## Files / areas

- `scripts/Core/Main.cs` (composition, `_Process`, three `Init` calls, key handler, `UiInput`)
- `scripts/Core/LiveWorld.cs`, `LiveClock.cs`, `LiveDialogueView.cs`
- `scripts/Core/DevSmoke.cs`, `scripts/Core/DevCarStress.cs`
- `scripts/Core/Game.cs` (hollowed in steps 1 and 2, deleted in step 3)
- `project.godot` (the `[autoload]` block)
- `blueprint/context/coding-standards.md` (one paragraph)

## Data / contracts

Load-bearing for item 19, which lifts the simulation half of this composition
into `Veld.Sim` and leaves `Main` as the Godot half.

```csharp
namespace Veld.Core;

public partial class Main : Node3D
{
    public GameData Data { get; private set; }
    public Settings Settings { get; }
    public Purse Purse { get; private set; }
    public Reputation Reputation { get; }
    public Notifier Notifier { get; }
    public UiState Ui { get; }
    public SaveGame SaveGame { get; private set; }
    public MissionManager Missions { get; private set; }
    public DialogueSystem Dialogue { get; private set; }
    public WantedSystem Wanted { get; private set; }
    // the scene nodes it already exposes: World, DayNight, Player, Camera, Hud, DialogueBox, PauseMenu, Sfx, Weather, WorldMap
}

public sealed class LiveWorld : IWorldQuery       { public LiveWorld(Main main, GameData data); }
public sealed class LiveClock : IClock            { public LiveClock(Main main); }
public sealed class LiveDialogueView : IDialogueView { public LiveDialogueView(Main main, UiState ui); }

public partial class DevSmoke : Node     { public void Init(Main main); }
public partial class DevCarStress : Node { public void Init(Main main); }
public partial class UiInput : Node      { public void Init(Main main); }
```

Order inside `Main._Ready`, fixed: input map; settings and data; purse; the
adapters and the three systems; the save file; the coin sound subscription;
then the scene exactly as today. The systems never touch a node before
`_Ready` returns: missions tick in `_Process`, dialogue starts from an NPC,
and a restore comes from the pause menu.

## Testing

The test gate is on, but no step here carries logic a unit test could reach;
the `LiveWorld` adapter is the one piece with branches and they are gone
after step 1. The suite must stay at 107 green. Evidence is the smoke run
after every step and the car stress run after steps 2 and 3:

    "...\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --smoke
    "...\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --carstress=40 --quality=0

The smoke run covers the process order directly: the mission marker and the
parcel mission depend on `Missions.Update` running every frame, the pause
check depends on it stopping while paused, and save and load depend on
`SaveGame` seeing `Main`.

Manual path: play for a minute; everything behaves as before, including T
to skip time and the screenshot key, which are the two lines `Main` itself
used to route through the static.

## Notes for the AI

- Change nothing but who owns and who reads. Lift `Game._Ready`'s composition and `Game._Process`'s body as they are; the only rewording is `this` for `Main` where `Game` passed itself.
- `Main` has no `ProcessMode` override, so `_Process` stops while the tree is paused, which is what `Game._Process`'s early return did; do not add `ProcessMode = Always`.
- The adapters read `Main`'s node properties at call time, never cache them, because the systems are created before the nodes exist.
- In step 1, `Game`'s forwarders read `Main!`; `Bind` runs first thing in `Main._Ready`, before any harness or key handler can run, so the bang is safe for the one step it lives.
- Step 3's standards paragraph replaces the one that begins "`Game.Instance` is a service locator reached from 111 call sites today"; keep the two bullets after it that describe registries and `Veld.Sim`.
- Count with `grep -rc "Game.Instance" scripts/ | awk -F: '{s+=$2} END {print s}'` and quote the number; the final check uses `\bGame\b` so a stray reference to the type cannot hide.
- Build before every Godot run; Godot does not rebuild the C# project from the command line.
- British spelling in player-facing text, no em dashes, no new static access anywhere.
- No AI attribution in commit messages; ask before committing.
