# Feature: Retire the Game singleton, part d: Player and camera off the singleton

**From build-plan:** feature 18d
**Status:** complete (2026-10-15)
**Branch:** `feature/player-off-singleton`

## Goal

The player and the camera stop reaching for `Game.Instance`. Each gains an
`Init` that `Main` calls before the node enters the tree, with exactly the
objects it reads: the carved-out services, the wanted system, and `Main` for
the scene siblings. The 26 call sites in `Player.cs` and `CameraRig.cs` go to
zero, the project count drops from 74 to 48, and three more members leave
`Game`: the `MouseSensitivity` and `InvertY` forwarders and the `Hud`
shortcut. The game plays identically: smoke run 46 of 46, car stress clean,
107 tests untouched.

## In scope

- `CameraRig.Init(Settings, UiState)`: the look sensitivity, the invert flag and the input lock.
- `Player.Init(Main, GameData, Purse, Notifier, UiState, WantedSystem)`: weapons from the data, death and respawn through the purse, notifier and wanted system, the input lock, and `Main` for the HUD prompt, the sound node, the clock's Dead Eye flag and the world.
- `Main` calls both before `AddChild`; `Player` is instantiated from its scene, so `Init` runs between `Instantiate` and `AddChild`. `Main`'s own `Game.Instance` uses stay (18f).
- Forwarders `MouseSensitivity` and `InvertY` deleted from `Game` (step 1); the `Hud` shortcut deleted from `Game` (step 3).
- The dead guards go with the static: `Hud?.`, `Main?.Sfx`, `DayNight != null` and the `world == null` return in `UpdateFocus` all protected against the scene not being bound, which cannot happen once the player exists.

## Out of scope

- `Npc`, `Horse`, `Vehicle` (18e); `Main`, `DevSmoke`, `DevCarStress`, the live adapters and `Game._Process` (18f).
- `Pence`, `AddMoney`, `Notify`, `InputLocked`, `GraphicsQuality`, `DayNight`, `World` and `Player` on `Game`: each still has a caller outside this feature.
- Narrowing `Player`'s view of the world to `IWorldQuery`; item 19.
- Unit tests: every change swaps a static read for a field read inside a Godot node; the smoke run, the car stress run and a screenshot are the evidence.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - CameraRig** - `Init(Settings settings, UiState ui)`; the three reads in `_UnhandledInput` become field reads; `Main` calls `Init` before `AddChild(Camera)`; `MouseSensitivity` and `InvertY` forwarders deleted from `Game`. *Done when:* `dotnet build` is clean, `grep -c "Game.Instance"` is 0 in `CameraRig.cs` and 71 across `scripts/`, neither name appears in `Game.cs`, and the smoke run passes 46 of 46.
- [x] **Step 2 - Player: services and systems** - `Init` with its six parameters and the fields behind them; `Main` calls it right after `Instantiate`; these lines become field reads: 62 (`Data`), 199 and 217 and 375 (`Notify`), 208 and 389 (`Wanted`), 209 and 210 (`Pence`, `AddMoney`), 211 (`Data.World`), and 225 (`var game`) with its three dependants 246 (`InputLocked`), 292 and 293 (`World.Horse`, `World.PlayerCar`, which keep their own null checks because a map may have neither). *Done when:* `dotnet build` is clean, the count is 61 across `scripts/`, and the smoke run passes 46 of 46 including "Dead Eye slows time", "save restored money and health" and "mission 'parcel' completed by walking its steps".
- [x] **Step 3 - Player: scene siblings** - the remaining thirteen lines read through `_main`: 109, 144, 175 and 364 (`Hud.SetPrompt`, `Hud.FlashDamage`), 174, 185, 306, 339, 376 and 388 (`Sfx`), 184 (`DayNight.DeadEye`), 213 (`World.Terrain`), and 346 (`World`, with the `world == null` return on 347 dropped); the `Hud` shortcut deleted from `Game`. *Done when:* `dotnet build` is clean, the count is 0 in `Player.cs` and 48 across `scripts/`, `Hud? Hud` no longer appears in `Game.cs`, the smoke run passes 46 of 46 including "Dead Eye slows time", "dialogue opened and input locked" and "HUD has an objective marker", the `2-driving` screenshot from a `--shot` run shows the player in the car, and the car stress run (`-- --carstress=40 --quality=0`) ends with zero failures out of 40.

## Files / areas

- `scripts/Characters/Player.cs`, `scripts/Characters/CameraRig.cs`
- `scripts/Core/Main.cs` (two `Init` calls; its own `Game.Instance` uses untouched)
- `scripts/Core/Game.cs` (two forwarders and one shortcut removed)

## Data / contracts

Load-bearing for 18f, which calls these from `Main` with objects `Main` then
owns, and for item 19, which replaces the `Main` parameter with views.

```csharp
namespace Veld.Characters;

public partial class CameraRig : Node3D
{
    public void Init(Settings settings, UiState ui);
}

public partial class Player : CharacterBody3D, IPlayerView
{
    public void Init(Main main, GameData data, Purse purse, Notifier notifier, UiState ui, WantedSystem wanted);
}
```

Each `Init` stores its arguments in `= null!` fields and must run before the
node enters the tree: `Player._Ready` reads the weapon list from the data.

Each node's reads, for the record:

| Node | Through `Main` | Services and systems |
| --- | --- | --- |
| `CameraRig` | none | `Settings.MouseSensitivity`, `Settings.InvertY`, `Ui.InputLocked` |
| `Player` | `Hud` (prompt, damage flash), `Sfx` (hit, click, step, gunshot), `DayNight.DeadEye`, `World` (terrain, horse, car, characters, vehicles) | `Data` (weapons, spawn), `Purse` (`Pence`, `Add`), `Notifier.Notify`, `Ui.InputLocked`, `Wanted` (`SetLevel`, `OnGunfire`) |

## Testing

The test gate is on, but no step here carries logic a unit test could reach.
The suite must stay at 107 green. Evidence per step is the smoke run, plus
for step 3 the screenshot and the car stress harness from `AGENTS.md`, which
the standards require after any change to the player's enter and exit code:

    "...\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --smoke --shot=<scratch dir>/veld
    "...\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --carstress=40 --quality=0

Two paths are not driven by the smoke run and ride on the build: the mouse
look in `CameraRig._UnhandledInput` (the harness sets `Camera.Yaw` directly)
and `Player.Respawn` (nothing kills the player). Both are one-token
substitutions; the manual path covers them.

Manual path: move the mouse and the view turns; flip "Invert mouse Y" in the
pause menu and the pitch reverses; aim and press Q for Dead Eye; walk up to a
car and the "[E] Get in the car" prompt appears, E gets in, E gets out; fire a
shot near a resident and the wanted stars appear; take enough damage to die
and you wake at the spawn with less money and the doctor's message.

## Notes for the AI

- Change nothing but the source of each value. Lift each line as it is; `Game.Instance.X` becomes `_x` or `_main.X`, nothing else moves.
- Fields are `private Main _main = null!;` and so on, assigned in `Init`, after the existing private fields.
- `Main.Hud`, `Main.Sfx`, `Main.DayNight` and `Main.World` are non-nullable on `Main` and exist before any `Player` method other than `_Ready` runs; `_Ready` reads only `_data`. Keep a null check only on `World.Horse` and `World.PlayerCar`, which are genuinely optional.
- The car stress run is part of step 3's gate because `EnterVehicle` changes; run it and quote the result line.
- Delete a member from `Game` only when `grep` shows no caller left; the done-whens name which step deletes which.
- Count with `grep -rc "Game.Instance" scripts/ | awk -F: '{s+=$2} END {print s}'` and quote the number.
- British spelling in player-facing text, no em dashes, no new `Game.Instance` call sites anywhere.
- No AI attribution in commit messages; ask before committing.
