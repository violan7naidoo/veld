# Feature: Game services seam, part b: dialogue on the ports

**From build-plan:** feature 15b
**Status:** complete (2026-10-14)
**Branch:** `feature/services-seam-dialogue`

## Goal

`DialogueSystem` stops reaching into the `Game` singleton. It receives the
dialogue data, the mission manager, and six ports (wanted state, world query,
economy, notifier, clock, dialogue view), composed in `Game` with live adapters,
and its conditions and actions get behaviour tests with fakes. The game plays
identically: the smoke run stays 46 of 46, including "dialogue opened and input
locked" and "dialogue closed, input unlocked".

## In scope

- New ports under `scripts/Core/Ports/`: `IClock`, `IDialogueView`, `IWantedState`. `IPlayerView` gains `Heal()` and `GiveAmmo(string weaponId, int count)`.
- Live adapters: `DayNight : IClock` (with `Advance` owning the midnight wrap that the dialogue code does today); `WantedSystem : IWantedState` over its existing `Level` and `ClearWanted`; `Player` implements the two new actions; new `LiveClock` and `LiveDialogueView` in `scripts/Core/` that read `Game.DayNight`, `Game.Main.DialogueBox`, `Game.InputLocked`, the pause menu and the mouse mode lazily, because the autoload wakes before the scene is bound.
- `DialogueSystem` constructed as `(GameData data, MissionManager missions, IWantedState wanted, IWorldQuery world, IEconomy economy, INotifier notifier, IClock clock, IDialogueView view)`, with `Start(string dialogueId, INpcView npc)`.
- Tests: `tests/Veld.Tests/Gameplay/DialogueSystemTests.cs`, new fakes `FakeClock`, `FakeDialogueView`, `FakeWanted`, and `FakePlayer` recording heals and ammo.

## Out of scope

- `WantedSystem`'s own conversion (15c); here it only gains the small interface it already satisfies.
- Replacing the condition and action `switch` statements with registries (item 17). The strings and their parsing stay exactly as they are.
- `DialogueBox` itself and the callers that read `Game.Instance.Dialogue` (NPC, pause menu, map, smoke test) stay as they are.
- Making the era a parameter instead of the static `Money.Modern` flag; the tests join the "era flags" collection as before.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - Ports and node adapters** - the three new interfaces and the two `IPlayerView` members; `DayNight.Advance(float hours)` wrapping past midnight and bumping `Day`; `WantedSystem : IWantedState`; `Player.Heal()` and `Player.GiveAmmo()` doing what the dialogue code does inline today. No consumer changes. *Done when:* `dotnet build` is clean, `dotnet test tests/Veld.Tests` still passes 32, and `FakePlayer` compiles against the widened view.
- [x] **Step 2 - Live adapters** - `LiveClock` forwarding to `Game.DayNight` (no-op and zero while unbound) and `LiveDialogueView` whose `Open` locks input, shows the mouse and calls `DialogueBox.Show`, and whose `Close` closes the box, unlocks input and recaptures the mouse unless the pause menu is open. *Done when:* `dotnet build` is clean and the smoke run passes 46 of 46 with no behaviour change (nothing uses them yet).
- [x] **Step 3 - Dialogue system on the ports** - write the first test, `Start_opens_the_start_node_with_the_choices_whose_conditions_hold`, against the new constructor and see it fail to compile (the seam does not exist: the red); then change the constructor, replace every `_game.` and `Input.` use with a port, change `Start` to take `INpcView`, and compose it in `Game._Ready`. *Done when:* that test is green, `dotnet build` is clean, and the smoke run passes 46 of 46 including the two dialogue checks and "mission 'parcel' started" (the smoke test starts it through the dialogue's `talk_done` path).
- [x] **Step 4 - Behaviour tests** - the cases under Testing, one first written with a wrong literal to see the suite go red, then corrected. *Done when:* `dotnet test tests/Veld.Tests` passes with the new cases and the whole suite is green.

## Files / areas

- `scripts/Core/Ports/IClock.cs`, `IDialogueView.cs`, `IWantedState.cs` (new), `IPlayerView.cs`
- `scripts/Core/LiveClock.cs`, `scripts/Core/LiveDialogueView.cs` (new), `scripts/Core/Game.cs`
- `scripts/World/DayNight.cs`, `scripts/Gameplay/WantedSystem.cs`, `scripts/Characters/Player.cs`
- `scripts/Gameplay/DialogueSystem.cs`
- `tests/Veld.Tests/Fakes/FakeClock.cs`, `FakeDialogueView.cs`, `FakeWanted.cs` (new), `FakePlayer.cs`, `tests/Veld.Tests/Gameplay/DialogueSystemTests.cs` (new)

## Data / contracts

Load-bearing for 15c and 17. Add members later; do not rename.

```csharp
namespace Veld.Core;

public interface IClock
{
    float Hour { get; }
    int Day { get; }
    void Advance(float hours);   // wraps past 24 and increments Day
}

public interface IDialogueView
{
    void Open(string speaker, string text, IReadOnlyList<string> choices, Action<int> onChoose);
    void Close();
}

public interface IWantedState
{
    int Level { get; }
    void ClearWanted();
}

public interface IPlayerView
{
    Vector3 Position { get; }
    void Heal();
    void GiveAmmo(string weaponId, int count);
}
```

`Open` is called once per node shown, so locking input inside it must be
idempotent. `Close` may be called when nothing is open; it must be harmless.

## Testing

The test gate is on. Everything here is in-process; the ports are owned modules
with in-memory stand-ins. The mission manager is real, built on the same fakes
as 15a. Dialogue data is built in code per test.

Cases for `DialogueSystemTests`:

- `Start` with an unknown dialogue id notifies "<npc name> has nothing to say. (missing dialogue '<id>')" and opens nothing.
- `Start` opens the start node with the speaker from the data, or the NPC's name when the data has none, and only the choices whose conditions hold: `money:4500` against a purse of 4000 and 5000; `mission_available:parcel`; `wanted` and `not_wanted` against levels 0 and 1; two conditions joined by a comma must both hold.
- When no choice holds, the single label is "(Leave)" and choosing it closes the view.
- Choosing a choice with `next` opens that node; with no `next`, `next: end` or an unknown node id, the view closes.
- Actions: `take_money:4500,heal` charges 4500 and heals; `give_money:200` adds 200 and plays coins at the NPC's position; `give_ammo:pistol:12` adds 12 to that weapon; `start_mission:parcel` starts it and the next node shows; `talk_done` advances a `talk` step whose target is this NPC; `clear_wanted:50000` charges and clears with the message "Paid R500.00. The complaint is withdrawn." and, short of money, notifies "You cannot afford the fine." and leaves the level; `sleep:8` at 22:00 on day 1 gives 06:00 on day 2; `end` closes.
- `OnBoxClosed` ends an active dialogue and is harmless when none is open.
- An out-of-range choice index closes the dialogue.

**Test seams**

- Public interface under test: `Veld.Gameplay.DialogueSystem` through its constructor, `Start`, `Choose`, `End`, `OnBoxClosed`, `IsActive`, observed through `FakeDialogueView` (opened nodes, closed count), `FakeEconomy`, `FakeNotifier`, `FakeClock`, `FakeWanted`, `FakePlayer`, and the real `MissionManager`.
- Behaviour the tests prove: a conversation shows exactly the choices the player has earned and each choice does to the world what the JSON says.
- External boundary, local substitute, or mock: none external. The fakes are the second adapters at each port. Never mock `DialogueSystem`'s internals or the mission manager.
- Red command and expected failing signal: `dotnet test tests/Veld.Tests`; first CS1729 on the new constructor, later an `Assert` failure showing expected versus actual.
- Full Verify command after green: none declared; `dotnet build`, `dotnet test tests/Veld.Tests`, then the smoke run from `AGENTS.md`.

## Notes for the AI

- No new `Game.Instance` call sites; `Game` is the composition root and composes `Dialogue` after `Missions` and `Wanted`, since it takes both.
- The `Input.MouseMode` and `InputLocked` handling moves into `LiveDialogueView` verbatim; the dialogue system must not touch Godot input.
- `DialogueSystem` may use Godot's `Vector3` and nothing else from the engine. Remove the `using Godot` for `Input` once the view owns it.
- Keep `Money.Format` in the fine message; set `Money.Modern = true` in that test and put the test class in the "era flags" collection.
- `DayNight.Advance` replaces the sleep loop inline in the dialogue code; `Main`'s "T skips two hours" keeps its own modulo and is untouched.
- One class per test file, mirrors `scripts/`.
- No AI attribution in commit messages; ask before committing.
