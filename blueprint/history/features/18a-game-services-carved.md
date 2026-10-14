# Feature: Retire the Game singleton, part a: services carved out of Game

**From build-plan:** feature 18a
**Status:** complete (2026-10-14)
**Branch:** `feature/game-services-carved`

## Goal

`Game` stops being the place where money, standing, notifications, settings,
UI state and the save file all live. Each becomes a small sealed class that
`Game` owns and, for now, forwards to, so the 119 `Game.Instance` call sites
keep compiling unchanged while the gameplay systems start receiving the real
`Purse` and `Notifier` instead of `Game` itself. This is the strangler step:
18b to 18e then hand those objects to the nodes that need them and delete the
forwarders as their last callers go, and 18f deletes `Game`. The game plays
identically: smoke run 46 of 46, all 100 tests green untouched.

## In scope

- Six new sealed classes under `scripts/Core/`: `Purse` (money, implements `IEconomy`), `Reputation` (standing with the residents), `Notifier` (toasts and the coin sound, implements `INotifier`), `Settings` (the four player settings and `settings.cfg`), `UiState` (`InputLocked`, `Waypoint`), `SaveGame` (the save file and its DTO).
- `Game` owns one of each, composes `MissionManager`, `WantedSystem` and `DialogueSystem` with the real `Purse` and `Notifier`, and no longer implements `IEconomy` or `INotifier` itself.
- Forwarders on `Game` for every member a call site still reaches (`Pence`, `AddMoney`, `TrySpend`, `MoneyChanged`, `Notify`, `Notified`, `Coins`, the four settings, `SaveSettings`, `LoadSettings`, `InputLocked`, `Waypoint`, `HasSave`, `Save`, `Load`, `SavePath`), each one line with no logic.
- The one call-site shape change: `Game.Instance.Reputation` becomes the `Reputation` object, so the three lines in `Npc.cs` that read or lower it use `Lose` and `ResidentsHostile`. The threshold `< -2` moves with them.
- Unit tests for `Purse`, `Reputation` and `Notifier`, the three with logic a wrong answer could hide in.

## Out of scope

- Removing any `Game.Instance` call site; the count stays at 119. That is 18b to 18e.
- Moving `DeadNpcs` into `WorldBuilder` (18c), re-pointing `LiveWorld`, `LiveClock` and `LiveDialogueView` at `Main` (18f), the grass-bend loop in `Game._Process` (18f), deleting the autoload (18f).
- Unit tests for `Settings` and `SaveGame`; both are Godot file access with no branching worth a test, and the smoke run already saves, loads and applies the graphics quality.
- The no-op `MoneyChanged += _ => { }` subscription in `Hud`; a note for `/audit`.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - Purse and Reputation** - write `PurseTests` and `ReputationTests` first against the missing types (the red is CS0246), then the two classes. `Game` owns `Purse` (started at `Data.World.StartMoney` in `_Ready`) and `Reputation`, passes `Purse` to the mission and dialogue systems, forwards `Pence`, `AddMoney`, `TrySpend` and `MoneyChanged` (custom add and remove accessors) to it, drops `IEconomy` from its own declaration, and `Save` and `Load` read and restore through both objects. The three `Npc.cs` lines become `Game.Instance.Reputation.Lose(1)`, `Game.Instance.Reputation.Lose(killed ? 2 : 1)` and `Game.Instance.Reputation.ResidentsHostile`. *Done when:* the new tests are green with the whole suite, `dotnet build` is clean, `grep -c "Game.Instance"` over `scripts/` is still 119, `Game.cs` no longer names `IEconomy`, and the smoke run passes 46 of 46 including "save restored money and health".
- [x] **Step 2 - Notifier** - write `NotifierTests` first (red CS0246), then the class. `Game` owns one built with `GD.Print` as its log, passes it to all three gameplay systems, subscribes `CoinsDropped` to `Main?.Sfx.Coins`, forwards `Notify`, `Notified` and `Coins`, and drops `INotifier` from its declaration. *Done when:* the tests are green with the suite, `Game.cs` names neither port interface, and the smoke run passes 46 of 46 including "dialogue opened and input locked" and "mission 'parcel' completed by walking its steps".
- [x] **Step 3 - Settings and UiState** - the two classes; `Game` owns one of each and forwards the four settings, `LoadSettings`, `SaveSettings`, `InputLocked` and `Waypoint`. The settings file path constant moves to `Settings.Path`. No unit test: Godot `ConfigFile` and two fields. *Done when:* `dotnet build` is clean, the smoke run passes 46 of 46 including "fps 60 at quality High", and the pause menu's settings still save and reload across a restart (manual path below).
- [x] **Step 4 - SaveGame** - the class and its private `SaveFile` DTO lifted from `Game`, built with the purse, reputation, dead-NPC set, missions, wanted system and notifier, with `Main` bound from `Game.Bind`. `Game` forwards `HasSave`, `Save`, `Load` and `SavePath`. *Done when:* `dotnet build` is clean, the smoke run passes 46 of 46 including "saved to", "loaded save" and "save restored money and health", and `Game.cs` is under 110 lines.

## Files / areas

- `scripts/Core/Purse.cs`, `Reputation.cs`, `Notifier.cs`, `Settings.cs`, `UiState.cs`, `SaveGame.cs` (new)
- `scripts/Core/Game.cs` (hollowed to ownership, composition, forwarders, `Bind` and `_Process`)
- `scripts/Characters/Npc.cs` (three lines)
- `tests/Veld.Tests/Core/PurseTests.cs`, `ReputationTests.cs`, `NotifierTests.cs` (new)

## Data / contracts

Load-bearing for 18b to 18f, which hand these objects to nodes, and for item
19, where `Purse`, `Reputation` and `Notifier` are simulation state that moves
into `Veld.Sim`.

```csharp
namespace Veld.Core;

/// <summary>The player's money. Changed fires with the new total after every change, including a restore.</summary>
public sealed class Purse : IEconomy
{
    public Purse(int pence);
    public int Pence { get; }
    public event Action<int>? Changed;
    public void Add(int pence);
    public bool TrySpend(int pence);     // false and no change when short
    public void Restore(int pence);      // load: set and raise Changed
}

/// <summary>Standing with the residents: negative after harming them; they turn hostile below -2.</summary>
public sealed class Reputation
{
    public int Value { get; }
    public bool ResidentsHostile => Value < -2;
    public void Lose(int points);
    public void Restore(int value);
}

/// <summary>Feedback to the player: a toast line, and the coin sound at a spot. The log is GD.Print in the game.</summary>
public sealed class Notifier : INotifier
{
    public Notifier(Action<string> log);
    public event Action<string>? Notified;
    public event Action<Vector3>? CoinsDropped;
    public void Notify(string message);  // logs "[notify] " + message, then raises Notified
    public void Coins(Vector3 at);       // raises CoinsDropped
}

public sealed class Settings
{
    public const string Path = "user://settings.cfg";
    public float MouseSensitivity { get; set; } = 0.0025f;
    public bool InvertY { get; set; }
    public float Volume { get; set; } = 0.8f;
    public Quality GraphicsQuality { get; set; } = Quality.High;
    public void Load();                  // keeps the defaults when the file is missing
    public void Save();
}

/// <summary>True while a dialogue or menu owns the input, and the spot the player marked on the map.</summary>
public sealed class UiState
{
    public bool InputLocked { get; set; }
    public Vector3? Waypoint { get; set; }
}

public sealed class SaveGame
{
    public const string Path = "user://save.json";
    public SaveGame(Purse purse, Reputation reputation, HashSet<string> deadNpcs, MissionManager missions, WantedSystem wanted, INotifier notifier);
    public Main? Main { get; set; }      // the player, clock and world come from here; Save and Load do nothing until it is bound
    public bool HasSave { get; }
    public void Save();
    public bool Load();
}
```

`Game` after this feature, in outline: `Instance`, `Data`, the six owned
objects (`Settings`, `Purse`, `Reputation`, `Notifier`, `Ui`, `SaveGame`),
`DeadNpcs`, the three gameplay systems, `Main` and its four sibling shortcuts,
`Bind`, `_Ready`, `_Process`, and one forwarder line per legacy member.

## Testing

The test gate is on. All in-process; no fakes needed, these classes have no
dependencies.

Purse (`PurseTests`):

- A purse started at 300 that adds 200 holds 500 and raised `Changed` once with 500.
- A purse at 100 refuses to spend 150 (false, still 100, `Changed` never raised) and then spends 60 (true, 40, `Changed` raised with 40).
- `Restore(1200)` sets 1200 and raises `Changed` with 1200.

Reputation (`ReputationTests`):

- Starts at 0 and not hostile; after `Lose(1)` it is -1 and not hostile; after `Lose(1)` again it is -2 and still not hostile; after `Lose(2)` it is -4 and hostile.
- `Restore(-5)` is hostile; `Restore(0)` is not.

Notifier (`NotifierTests`):

- `Notify("Game saved.")` logs exactly `"[notify] Game saved."` and raises `Notified` with `"Game saved."`.
- `Coins(new Vector3(28, 0, -700))` raises `CoinsDropped` with that point and logs nothing.

**Test seams**

- Public interface under test: `Purse`, `Reputation` and `Notifier` through their public members; `Purse` and `Notifier` also as `IEconomy` and `INotifier`.
- Behaviour the tests prove: money and standing change only the way the rules say and announce every change; a notification reaches both the log and the listener.
- External boundary, local substitute, or mock: the log is the only boundary, replaced by a list-appending lambda; nothing else.
- Red command and expected failing signal: `dotnet test tests/Veld.Tests`; CS0246 on the missing type in steps 1 and 2.
- Full Verify command after green: none declared; `dotnet build`, `dotnet test tests/Veld.Tests`, then the smoke run from `AGENTS.md`.

Manual path for step 3: open the pause menu, change the volume and graphics
quality, close the game, start it again, and the pause menu shows the changed
values.

## Notes for the AI

- Lift each body verbatim; the smoke run pins save, load, settings and the toasts. The only behavioural rewording is the reputation threshold moving into `ResidentsHostile`.
- Forwarders are one line each and carry no logic. Mark the block with one comment saying 18b to 18e delete them; do not add a forwarder for anything no call site uses.
- `Game` passes `Purse` and `Notifier` to the gameplay systems from step 1 and 2 on, never `this`.
- `Purse.Restore` and `Reputation.Restore` exist so `SaveGame.Load` never needs a setter; keep `Value` and `Pence` read-only outside their classes.
- `Notifier` takes its log as a delegate so the tests run without the engine; `Game` passes `GD.Print`.
- Tests mirror `scripts/Core/` under `tests/Veld.Tests/Core/`; no era flags are touched, so no collection attribute.
- File-scoped namespaces, one type per file, Allman braces, British spelling in player-facing text, no em dashes.
- No new `Game.Instance` call sites. The count after this feature is exactly the count before it.
- No AI attribution in commit messages; ask before committing.
