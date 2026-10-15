# Feature: Simulation and presentation split, part b: the Simulation object and the fixed tick

**From build-plan:** feature 19b
**Status:** complete (2026-10-15)
**Branch:** `feature/simulation-tick`

## Goal

One `Simulation` in the `Veld.Sim` library owns the purse, standing, sim
clock, missions, wanted state and dialogue. `Main` creates it from the data
and the three live ports and ticks it once per physics frame, so the
simulation advances at a fixed step and never reads the engine's clock. Game
time leaves `DayNight`, which becomes a renderer of the sim clock; the time
skip key, the pause-menu slider, the HUD clock, the weather and the save file
all read and write the one clock. The game plays identically: smoke run 46 of
46, with the sunset check tightened to prove the tick ran, and 111 tests plus
the new ones green.

## In scope

- `SimClock` in `sim/Veld.Sim/`, under `Veld.Sim`: hour, day, day length, `TimeText`, `IsNight`, `Advance` and `Tick`; implements `IClock`. The midnight wrap and the two formatting rules move here from `DayNight`, with tests.
- `Simulation` in `sim/Veld.Sim/`, under `Veld.Sim`: one constructor over `GameData`, `IWorldQuery`, `INotifier` and `IDialogueView`; it creates the clock, purse, standing, mission, wanted and dialogue systems itself and exposes them; `Tick(dt)` advances the clock, polls the missions and cools the wanted level, in that order.
- `Main`: one `Sim` property replaces `Purse`, `Reputation`, `Missions`, `Dialogue` and `Wanted`; the five `new` lines become one; `_PhysicsProcess` calls `Sim.Tick`; the grass bend stays in `_Process`; the time skip key calls `Sim.Clock.Advance(2f)`.
- `DayNight`: `Configure(SimClock clock, Quality quality)`; it renders `Clock.Hour` and keeps `NightFactor`, `DeadEye`, `Quality`, `Weather` and `World`; its own `Hour`, `Day`, `Advance`, `Paused`, `TimeText`, `IsNight`, the `IClock` implementation and the tick in `_Process` go.
- `LiveClock` deleted; `Hud`, `PauseMenu`, `Weather`, `SaveGame` and `DevSmoke` read and write `Sim.Clock`; `SaveGame` takes the `Simulation`.
- The smoke harness's sunset check becomes a tick check: the hour set to 17.55 at case 652 must read at least 17.57 at case 700, 48 physics frames later.
- `coding-standards.md`: the layout line and the seams bullet name the `Simulation` and the tick. The glossary already carries Simulation, Tick and Sim clock.

## Out of scope

- NPC decisions inside the simulation: 19c. `Npc` keeps its state machine and still receives the systems through `Init`.
- Player and world state in the simulation and the save file as a snapshot: 19d. `SaveGame` keeps reading the player node and the world's dead list; only its sources for money, standing, time, missions and wanted move to `Sim`.
- A sim-owned fixed-step accumulator independent of the engine's physics rate. The physics step is the fixed tick (60 per second unless `Engine.PhysicsTicksPerSecond` changes, which nothing does); item 26 revisits this when a server ticks the simulation.
- Weather as simulation. It stays a node and reads the clock's day length as it does today.
- Any change to what `DayNight` draws, to `Notifier`, `Settings`, `UiState` or the `Init` signatures of the nodes: they keep receiving `Sim.Purse`, `Sim.Missions` and so on, one system each, not the whole simulation.
- Any behaviour change but one, called out in the notes: skipping time past midnight now advances the day, as sleeping already does.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - The sim clock** - `sim/Veld.Sim/SimClock.cs` and `tests/Veld.Tests/Sim/SimClockTests.cs`, written red first. Nothing in the game changes. *Done when:* `dotnet build` is clean, `dotnet test tests/Veld.Tests` is green at 116 (111 plus the five clock tests), the Sim fence prints nothing, and `git status` shows no change under `scripts/`.
- [x] **Step 2 - The Simulation** - `sim/Veld.Sim/Simulation.cs` and `tests/Veld.Tests/Sim/SimulationTests.cs`, written red first, over the existing fakes. Nothing in the game uses it yet. *Done when:* build clean, tests green at 120 (four simulation tests), the fence prints nothing, no change under `scripts/`.
- [x] **Step 3 - Main composes and ticks it; DayNight renders it** - `Main` creates `Sim`, passes its members to the `Init` calls, ticks it in `_PhysicsProcess`, skips time through `Sim.Clock.Advance`; `DayNight.Configure(Sim.Clock, quality)` and its tick removed; `LiveClock.cs` deleted; `Hud`, `PauseMenu`, `Weather`, `SaveGame` and `DevSmoke` moved to `Sim` and `Sim.Clock`; the sunset check tightened. *Done when:* build clean, 120 green, the fence prints nothing; `scripts/Core/LiveClock.cs` is gone; `grep -rnE "new (Purse|Reputation|MissionManager|WantedSystem|DialogueSystem|SimClock)\(" scripts/` finds nothing; `grep -rnE "DayNight\.(Hour|Day\b|Advance|Paused|TimeText|IsNight)" scripts/` finds nothing; `grep -n "Tick(" scripts/Core/Main.cs` shows one call inside `_PhysicsProcess`; the smoke run passes 46 of 46 including "clock ran to 17:34 in 48 physics frames" and "save restored money and health".
- [x] **Step 4 - Standards** - the layout line for `sim/Veld.Sim/` gains "the Simulation and the sim clock"; the seams bullet says the `Simulation` owns the systems and `Main` ticks it once per physics frame, present tense, and names 19c and 19d as what is still outside it. *Done when:* `grep -n "Simulation" blueprint/context/coding-standards.md` shows both lines, and build, tests and smoke are unchanged from step 3 (no code in this step).

## Files / areas

- `sim/Veld.Sim/SimClock.cs`, `sim/Veld.Sim/Simulation.cs` (new)
- `tests/Veld.Tests/Sim/SimClockTests.cs`, `tests/Veld.Tests/Sim/SimulationTests.cs` (new)
- `scripts/Core/Main.cs`, `scripts/Core/SaveGame.cs`, `scripts/Core/DevSmoke.cs`; `scripts/Core/LiveClock.cs` (deleted)
- `scripts/World/DayNight.cs`, `scripts/World/Weather.cs`
- `scripts/UI/Hud.cs`, `scripts/UI/PauseMenu.cs`
- `blueprint/context/coding-standards.md`

## Data / contracts

Load-bearing for 19c, whose character brains tick inside `Simulation.Tick`;
for 19d, whose snapshot is these members; and for item 26, where a server
calls `Tick` with no engine at all.

```csharp
namespace Veld.Sim;

/// <summary>Game time: hour of day and day number, advanced by the tick at the map's day length.</summary>
public sealed class SimClock : IClock
{
    public SimClock(float startHour, float dayLengthSeconds);
    public float Hour { get; set; }           // 0 <= Hour < 24; set directly by the pause-menu slider, the harness and Load
    public int Day { get; set; }              // starts at 1
    public float DayLengthSeconds { get; }    // real seconds per game day, from world.json
    public string TimeText { get; }           // "17:33"
    public bool IsNight { get; }              // before 05:30 or after 19:30
    public void Advance(float hours);         // wraps past midnight into the next day
    public void Tick(float dt);               // dt real seconds, as dt * 24 / DayLengthSeconds hours, wrapping the same way
}

/// <summary>The engine-free game: owns the systems, hands them their ports, and advances them one tick at a time.</summary>
public sealed class Simulation
{
    public Simulation(GameData data, IWorldQuery world, INotifier notifier, IDialogueView view);
    public SimClock Clock { get; }            // from world.startHour and world.dayLengthSeconds
    public Purse Purse { get; }               // from world.startMoney
    public Reputation Reputation { get; }
    public MissionManager Missions { get; }
    public WantedSystem Wanted { get; }
    public DialogueSystem Dialogue { get; }   // built over Clock, so sleeping advances the one clock
    public void Tick(float dt);               // Clock.Tick, Missions.Update, Wanted.Update; dialogue is event-driven
}
```

The tick: `Main._PhysicsProcess(delta)` calls `Sim.Tick((float)delta)` and
nothing else does. The physics step is fixed, so the simulation steps at a
fixed `dt` and stops when the tree pauses, as the systems did under
`_Process`. The simulation reads no engine clock, no `Time`, no `Engine`.

`DayNight` after the move: `Configure(SimClock clock, Quality quality)`,
`Clock` read-only, `NightFactor`, `DeadEye`, `Quality`, `Weather`, `World`,
`ApplyQuality`, the two static light registries. `Weather` reads
`dayNight.Clock.DayLengthSeconds`. The save file's shape does not change
(`hour` and `day` are read from and written to `Sim.Clock`).

`IClock` and `FakeClock` stay: the dialogue tests construct `DialogueSystem`
directly and need a clock they can read back.

## Testing

The test gate is on. Steps 1 and 2 add logic and ship their tests red
first; steps 3 and 4 move call sites and ride on the smoke run.

- `SimClockTests` (`tests/Veld.Tests/Sim/`), five facts, day length 720 so one game hour is 30 real seconds: `Tick(30)` from 07:30 reads 08:30; `Tick(30)` from 23:45 reads 00:15 on day 2; `Advance(8)` from 20:00 reads 04:00 on the next day; `TimeText` at 17.55 is "17:33"; `IsNight` is true at 21.5 and false at 12.
- `SimulationTests` (`tests/Veld.Tests/Sim/`), four facts over `FakeWorld`, `FakeNotifier` and `FakeDialogueView` with a `GameData` carrying `startMoney` 500, `startHour` 7.5, `dayLengthSeconds` 720 and one mission of a single `goto` step at `[10, 10]`, radius 3, reward 100: construction gives `Purse.Pence` 500, `Clock.Hour` 7.5 and `Clock.Day` 1; `Tick(30)` reads 08:30; with the fake player standing at the target, `Missions.Start` then one `Tick` credits the reward (`Purse.Pence` 600) and lists the mission in `Missions.Completed`; `Wanted.SetLevel(1)` then forty ticks of one second with no police in the world brings `Wanted.Level` to 0 and the notifier holds "The police have lost interest in you." (the world is modern).

**Test seams**

- Public interface under test: `Simulation` and `SimClock` in `Veld.Sim`; the seam is the four-port constructor plus `Tick`.
- Behaviour the tests prove: the simulation starts from the map's data, and one tick moves game time, polls the current mission step and cools the wanted level; callers never reach those systems any other way.
- External boundary, local substitute, or mock: the world, the notifier and the dialogue view are the ports; the existing fakes stand in. The clock is not faked: it is the module under test.
- Red command and expected failing signal: `dotnet test tests/Veld.Tests`; CS0246 on `SimClock` in step 1 and on `Simulation` in step 2.
- Full Verify command after green: none declared; `dotnet build`, `dotnet test tests/Veld.Tests`, the Sim fence from `AGENTS.md`, then the smoke run.

Manual path: play; the HUD clock advances and the sky follows it. Esc opens
the pause menu and its time slider still sets the hour. The time skip key
adds two hours and, past midnight, a day. Save, change the hour, load: the
saved hour and day come back. A dialogue with a `sleep` action advances the
same clock the sky renders.

## Notes for the AI

- `Main` stays the composition root. Nodes keep receiving one system each through `Init` (`Sim.Purse`, `Sim.Missions`...), not the `Simulation`; `SaveGame` reads most of it and takes `Sim`. Do not add forwarding properties on `Main` for the old names; change the readers (`DevSmoke` has about 25, all mechanical).
- The one deliberate behaviour change: the time skip key uses `Clock.Advance(2f)`, so a skip across midnight advances the day. Today it wraps the hour and leaves the day alone; sleeping through dialogue already advances the day. Say so in the step 3 report.
- `DayNight.Paused` has no writer; delete it with the clock. `Player` still writes `DayNight.DeadEye` and `Vehicle` still reads `NightFactor`: rendering, unchanged.
- Do not change `Engine.PhysicsTicksPerSecond`; the smoke harness counts physics frames, and the tightened sunset check assumes 48 of them between cases 652 and 700 at 60 per second (0.8 s is 0.0267 h at day length 720, so 17.55 reads 17.5767, which `TimeText` shows as 17:34).
- `Tick` order inside `Simulation` is clock, missions, wanted, matching what `Main._Process` did (missions then wanted) with the clock in front.
- Expected values in tests come from arithmetic on the day length (30 s per hour at 720) and from the strings the player sees, never from calling the code under test twice.
- Run the Sim fence after every touch of `sim/`. Godot does not rebuild the C# project from the command line; build before every run.
- British spelling in player-facing text, no em dashes.
- No AI attribution in commit messages; ask before committing.
