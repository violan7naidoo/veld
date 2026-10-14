# Feature: Retire the Game singleton, part b: UI off the singleton

**From build-plan:** feature 18b
**Status:** complete (2026-10-14)
**Branch:** `feature/ui-off-singleton`

## Goal

The five UI nodes stop reaching for `Game.Instance`. Each gains an `Init`
method that `Main` calls with exactly the objects it needs, before the node is
added to the tree, so `_Ready` and every later frame read fields instead of a
static. The 32 call sites in `scripts/UI/` go to zero, the project count drops
from 119 to 87, and the four forwarders on `Game` whose only callers were these
nodes go with them. The game plays identically: smoke run 46 of 46, 107 tests
untouched.

## In scope

- `Init` on `DialogueBox`, `PauseMenu`, `WorldMap`, `Hud` and `Minimap`, each taking its real dependencies: the services carved out in 18a (`GameData`, `Purse`, `Notifier`, `Settings`, `UiState`, `SaveGame`), the gameplay systems (`MissionManager`, `WantedSystem`, `DialogueSystem`), and `Main` for the scene siblings (`Player`, `DayNight`, `Weather`, `World`, `PauseMenu`).
- `Main` calls each `Init` right after construction and before `AddChild`, passing what `Game` owns. `Main`'s own `Game.Instance` uses stay; they are 18f.
- `Hud` passes what `Minimap` needs when it constructs it.
- Forwarders deleted from `Game` as their last caller goes: `SaveSettings` (step 1), `Waypoint`, `Notified` and `MoneyChanged` (step 3).
- `Hud`'s no-op `MoneyChanged += _ => { }` subscription is dropped rather than re-pointed; it subscribed an empty lambda and nothing observes it.

## Out of scope

- `Main.cs`, `UiInput`, `DevSmoke` and `DevCarStress` call sites (18f), and `LiveDialogueView`'s writes to `InputLocked` through `Game` (18f).
- Narrowing the UI to read-only views of the simulation (`IWorldQuery` for the player and NPC positions); that is item 19. Here the nodes take `Main` for scene siblings.
- Any change to what the HUD, map or menus show.
- Unit tests: these are Godot nodes with no logic outside the scene; the smoke run and screenshots are the evidence.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - DialogueBox and PauseMenu** - `DialogueBox.Init(DialogueSystem)` and `PauseMenu.Init(Main, GameData, Settings, SaveGame, UiState, DialogueSystem)`; `Main` calls both; the 17 sites become field reads; `SaveSettings` forwarder deleted from `Game`. *Done when:* `dotnet build` is clean, `grep -c "Game.Instance"` is 0 in both files and 102 across `scripts/`, `SaveSettings` no longer appears in `Game.cs`, and the smoke run passes 46 of 46 including "pause menu opens and pauses the game", "pause menu closes and resumes", "dialogue opened and input locked" and "dialogue closed, input unlocked".
- [x] **Step 2 - WorldMap** - `WorldMap.Init(Main, GameData, Notifier, UiState, MissionManager, WantedSystem, DialogueSystem)`; `Main` calls it; the 11 sites become field reads. *Done when:* `dotnet build` is clean, the count is 0 in the file and 91 across `scripts/`, and the smoke run passes 46 of 46 including "world map opens and pauses", "world map closes and resumes", "address search finds 42 Road 721" and "address search rejects an unknown address"; the `6-map` screenshot from a `--shot` run shows the map with the player dot and labels.
- [x] **Step 3 - Hud and Minimap** - `Hud.Init(Main, GameData, Purse, Notifier, UiState, MissionManager, WantedSystem)` and `Minimap.Init(Main, GameData, UiState, MissionManager, WantedSystem)`, the latter called from `Hud` when it builds the minimap; `Hud._ExitTree` unsubscribes from the notifier it was given; `Waypoint`, `Notified` and `MoneyChanged` forwarders deleted from `Game`. *Done when:* `dotnet build` is clean, the count is 0 across `scripts/UI/` and 87 across `scripts/`, none of the three names appears in `Game.cs`, the smoke run passes 46 of 46 including "HUD has an objective marker", and the `1-spawn` screenshot from a `--shot` run shows the HUD's money, clock, objective line and minimap.

## Files / areas

- `scripts/UI/DialogueBox.cs`, `PauseMenu.cs`, `WorldMap.cs`, `Hud.cs`, `Minimap.cs`
- `scripts/Core/Main.cs` (five `Init` calls; its own `Game.Instance` uses untouched)
- `scripts/Core/Game.cs` (four forwarders removed)

## Data / contracts

Load-bearing for 18f, which calls these from `Main` with objects `Main` then
owns, and for item 19, which replaces the `Main` parameter with views.

```csharp
namespace Veld.UI;

public partial class DialogueBox : Control
{
    public void Init(DialogueSystem dialogue);
}

public partial class PauseMenu : Control
{
    public void Init(Main main, GameData data, Settings settings, SaveGame save, UiState ui, DialogueSystem dialogue);
}

public partial class WorldMap : Control
{
    public void Init(Main main, GameData data, Notifier notifier, UiState ui, MissionManager missions, WantedSystem wanted, DialogueSystem dialogue);
}

public partial class Hud : Control
{
    public void Init(Main main, GameData data, Purse purse, Notifier notifier, UiState ui, MissionManager missions, WantedSystem wanted);
}

public partial class Minimap : Control
{
    public void Init(Main main, GameData data, UiState ui, MissionManager missions, WantedSystem wanted);
}
```

Each `Init` stores its arguments in private readonly-style fields (`= null!`
until `Init`, per the late-bound convention) and must be called before the
node enters the tree. `Main` keeps the siblings it already exposes; nothing new
is added to it.

Each node's reads, for the record:

| Node | Through `Main` | Services and systems |
| --- | --- | --- |
| `DialogueBox` | none | `Dialogue.OnBoxClosed` |
| `PauseMenu` | `DayNight` (quality, hour) | `Data.World` name and attribution, `Settings` four values and `Save`, `SaveGame` `Save`, `Load`, `HasSave`, `Ui.InputLocked`, `Dialogue.IsActive` |
| `WorldMap` | `Player`, `World`, `PauseMenu.IsOpen` | `Data`, `Notifier.Notify`, `Ui.InputLocked` and `Waypoint`, `Missions.MarkerPosition`, `Wanted.Level`, `Dialogue.IsActive` |
| `Hud` | `Player`, `DayNight`, `Weather`, `World.Horse` | `Data`, `Purse.Pence`, `Notifier` (`Notified`, `Notify`), `Ui.Waypoint`, `Missions`, `Wanted.Label` |
| `Minimap` | `Player`, `World` | `Data.Missions`, `Ui.Waypoint`, `Missions`, `Wanted.Level` |

## Testing

The test gate is on, but no step here carries logic a unit test could reach:
every change swaps a static read for a field read inside a Godot node. The
suite must stay at 107 green. Evidence per step is the smoke run and, for
steps 2 and 3, the screenshots:

    "...\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --smoke --shot=<scratch dir>/veld

which writes `veld-1-spawn.png` (HUD in open play) and `veld-6-map.png`
(the world map). Both are viewed, not just produced.

Manual path: play for a minute. Money, clock, objective, wanted stars and the
minimap update; M opens the map, a click sets a waypoint, right-click clears
it, the search box finds `42 Road 721`; Esc opens the pause menu, the sliders
move and persist, save and load work; talk to a resident and the dialogue box
opens and closes. Run `/try latest` after `/complete` for the full list.

## Notes for the AI

- Change nothing but the source of each value. Lift the surrounding line as it is; `game.X` becomes `_x` or `_main.X`, nothing else moves.
- Fields are `private Main _main = null!;` and so on, assigned in `Init`; no constructor parameters on nodes, since Godot instantiates `Control` subclasses itself in other paths and the project's convention is `Init` or `Configure`.
- `Main.Player`, `Main.World` and `Main.DayNight` are non-nullable on `Main`, so a `game.Player` null check becomes a plain read; keep a null check only where the original guarded a genuinely optional thing (`World.Horse`, `World.PlayerCar`).
- `Hud._ExitTree` must unsubscribe from the same `Notifier` it subscribed to; keep both lines.
- Delete a forwarder only when `grep` shows no caller left; the done-whens name which step deletes which.
- Count with `grep -rc "Game.Instance" scripts/ | awk -F: '{s+=$2} END {print s}'` and quote the number.
- British spelling in player-facing text, no em dashes, no new `Game.Instance` call sites anywhere.
- No AI attribution in commit messages; ask before committing.
