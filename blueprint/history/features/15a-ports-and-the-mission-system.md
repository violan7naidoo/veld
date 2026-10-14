# Feature: Game services seam, part a: ports and the mission system

**From build-plan:** feature 15a
**Status:** complete (2026-10-14)
**Branch:** `feature/services-seam-missions`

## Goal

`MissionManager` stops reaching into the `Game` singleton. It receives what it
needs through small ports (mission data, a world query, an economy, a
notifier), composed in `Game` with live adapters over the running scene, and
gets its first behaviour tests with in-memory fakes. The game plays identically:
the smoke run stays 46 of 46. This is the seam the dialogue system (15b) and the
wanted system (15c) then join.

## In scope

- Ports under `scripts/Core/Ports/`, one interface per file: `IEconomy`, `INotifier`, `IWorldQuery`, `INpcView`, `IPlayerView`.
- Live adapters: `Game` implements `IEconomy` and `INotifier`; `Npc` implements `INpcView`; `Player` implements `IPlayerView`; a new `LiveWorld : IWorldQuery` in `scripts/Core/LiveWorld.cs` that reads the current world, player and map era through `Game` lazily, because `Game._Ready` runs before `Main` binds the scene.
- `MissionManager` constructed as `new MissionManager(GameData data, IWorldQuery world, IEconomy economy, INotifier notifier)`, with no reference to `Game`.
- Tests: `tests/Veld.Tests/Gameplay/MissionManagerTests.cs` with fakes under `tests/Veld.Tests/Fakes/`.

## Out of scope

- `DialogueSystem` and `WantedSystem` stay on `Game` until 15b and 15c.
- Callers that read `Game.Instance.Missions` (HUD, minimap, map, smoke test, NPCs) stay as they are; the singleton still owns the instance. Removing those call sites is feature 18.
- A clock port; nothing in the mission system needs the time of day. 15b adds `IClock`.
- Replacing Godot's `Vector2` and `Vector3` in the ports; they are pure structs and fine in tests. Feature 19 decides on engine-free types.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - Ports** - the five interfaces under `scripts/Core/Ports/`, exactly as in Data / contracts below, with a one-line summary each. No consumer yet. *Done when:* `dotnet build` is clean and `dotnet test tests/Veld.Tests` still passes 21.
- [x] **Step 2 - Live adapters** - `Game : IEconomy, INotifier` (its `Pence`, `AddMoney`, `TrySpend`, `Notify` already exist; `Add` forwards to `AddMoney`, `Coins` forwards to `Main?.Sfx.Coins`); `Npc : INpcView` and `Player : IPlayerView` as thin members over what they already expose; `LiveWorld` wrapping `Game.World`, `Game.Player` and `Game.Data.World.IsModern`, returning empty or no-op results while the scene is not bound. *Done when:* `dotnet build` is clean and the smoke run passes 46 of 46 with no behaviour change.
- [x] **Step 3 - Mission system on the ports** - write the first test, `Start_pays_nothing_and_notifies_the_title`, against the new constructor and see it fail to compile against the old one (the seam does not exist yet: that is the red); then change the constructor, replace every `_game.` use with a port, compose it in `Game._Ready` as `new MissionManager(Data, new LiveWorld(this), this, this)`, and see the test pass. *Done when:* that test is green, `dotnet build` is clean, and the smoke run passes 46 of 46 including "mission 'parcel' started", "completed by walking its steps" and "reward paid".
- [x] **Step 4 - Behaviour tests** - the remaining cases listed under Testing, each with literal expectations; one is first written with a wrong literal to see the suite go red, then corrected. *Done when:* `dotnet test tests/Veld.Tests` passes with the new cases and the whole suite is green.

## Files / areas

- `scripts/Core/Ports/IEconomy.cs`, `INotifier.cs`, `IWorldQuery.cs`, `INpcView.cs`, `IPlayerView.cs` (new)
- `scripts/Core/LiveWorld.cs` (new), `scripts/Core/Game.cs`, `scripts/Characters/Npc.cs`, `scripts/Characters/Player.cs`
- `scripts/Gameplay/MissionManager.cs`
- `tests/Veld.Tests/Fakes/FakeWorld.cs`, `FakeNpc.cs`, `FakePlayer.cs`, `FakeEconomy.cs`, `FakeNotifier.cs` (new), `tests/Veld.Tests/Gameplay/MissionManagerTests.cs` (new)

## Data / contracts

Load-bearing: 15b and 15c build on these shapes. Add members then; do not rename.

```csharp
namespace Veld.Core;

public interface IEconomy
{
    int Pence { get; }
    void Add(int pence);
    bool TrySpend(int pence);
}

public interface INotifier
{
    void Notify(string message);
    void Coins(Vector3 at);
}

public interface INpcView
{
    string Id { get; }
    string Name { get; }
    string Group { get; }
    Faction Faction { get; }
    bool IsDead { get; }
    Vector3 Position { get; }
    bool Provoked { get; set; }
    void Alert(Vector3 from);
}

public interface IPlayerView
{
    Vector3 Position { get; }
}

public interface IWorldQuery
{
    bool IsModern { get; }
    IPlayerView? Player { get; }
    IReadOnlyList<INpcView> Npcs { get; }
    INpcView? FindNpc(string id);
    IEnumerable<INpcView> Group(string group);
    void SpawnNpc(NpcData data);
    Vector3 Ground(Vector2 xz);
}
```

`INpcView` already carries `Provoked` and `Alert` for 15c because `Npc` has
them today. `IPlayerView` gains `Health` and `GiveAmmo` in 15b.

## Testing

The test gate is on. All logic here is in-process; the ports are owned modules
with in-memory stand-ins, so no mocks of internals.

Cases for `MissionManagerTests` (literal expectations throughout):

- `IsAvailable` is false for an unknown id, while another mission is active, when a required mission is not completed, and when the mission is already completed.
- `Start` returns false and changes nothing when unavailable; when available it sets `Active`, `StepIndex` 0, spawns each listed NPC that the world does not already have, and notifies "New task: <title>".
- `Update` advances a `goto` step only when the player's x,z is within the radius, and a `kill_group` step only when every NPC in the group is dead.
- `OnTalked` advances `talk` and `return` steps for the target id (the giver when the step names no target) and ignores other ids.
- Completing the last step marks the mission done, pays the reward through `IEconomy.Add` exactly once, calls `Coins` at the player's position, and notifies "Task complete: <title> (+R150.00)" for a 15000-cent reward on a modern map.
- `Restore` sets completed ids, re-activates the saved mission, clamps the saved step into range, and re-spawns missing NPCs.
- `MarkerPosition` is the ground point for `goto`, the nearest living group member for `kill_group`, the target NPC's position for `talk` and `return`, and null with no active mission.

**Test seams**

- Public interface under test: `Veld.Gameplay.MissionManager`, its constructor `(GameData, IWorldQuery, IEconomy, INotifier)` and the public members above. Each test builds a `GameData` with one or two `MissionData` entries in code.
- Behaviour the tests prove: a mission runs from acceptance to reward exactly as the JSON describes it, driven only by what the world reports.
- External boundary, local substitute, or mock: none external. `FakeWorld` (NPC list, player position, spawn log, flat ground), `FakeEconomy` (balance and a list of amounts added), `FakeNotifier` (messages and coin positions). Never mock `MissionManager`'s own internals.
- Red command and expected failing signal: `dotnet test tests/Veld.Tests`; first a compile error naming the missing constructor, later an `Assert.Equal` failure showing expected versus actual.
- Full Verify command after green: none declared; run `dotnet build`, `dotnet test tests/Veld.Tests`, then the smoke run from `AGENTS.md`.

## Notes for the AI

- No new `Game.Instance` call sites. `Game` itself becomes the adapter: it implements the ports and composes the systems in `_Ready`.
- `Money.Format` in the completion message is already pure and stays a static call; set `Money.Modern` in the test that checks the message.
- Keep `MissionManager` sealed and free of Godot node types; `Vector2`, `Vector3` and `Mathf` are the only Godot symbols it may use.
- `LiveWorld.Group` and `Npcs` return the live `Npc` nodes as `INpcView`; no copying.
- Use the vocabulary of `blueprint/context/architecture-reference.md`: these are ports at the seam between gameplay rules and the scene, with one live adapter and one test adapter each, which is what makes the seam real.
- One class per test file, test files mirror `scripts/` as `tests/Veld.Tests/Gameplay/MissionManagerTests.cs`.
- No AI attribution in commit messages; ask before committing.
