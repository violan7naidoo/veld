# Feature: Mission step registry

**From build-plan:** feature 16
**Status:** complete (2026-10-14)
**Branch:** `feature/mission-step-registry`

## Goal

A new kind of mission step becomes one small class registered by name instead
of four edits to `MissionManager`. The four kinds the JSON uses today (`goto`,
`kill_group`, `talk`, `return`) move into their own classes behind one
interface, the mission manager looks them up by the step's `type` string, and
a mission that names an unknown kind says so instead of silently never
finishing. Existing JSON is unchanged and the game plays identically: the
smoke run stays 46 of 46 and the 15a mission tests stay green untouched.

## In scope

- `IMissionStepKind` and a `MissionStepRegistry` under `scripts/Gameplay/Steps/`, one type per file, with the four built-in kinds (`talk` and `return` are one class registered under two names).
- `MissionManager` keeps its four-argument constructor (which uses the built-in registry) and gains a five-argument one that takes a registry, so tests and future features can add kinds.
- The three switches and the two string comparisons in `MissionManager` replaced by registry lookups.
- A notification at `Start` and `Restore` when a mission contains a step of an unregistered kind.
- Tests for each kind and for the registry, plus the custom-kind test that drives the conversion.

## Out of scope

- The smoke test's own switch on step types in `DevSmoke.cs`; it drives the test and stays.
- New step kinds (deliver, escort, timed); item 25 adds them through this registry.
- The dialogue action and condition switches; that is item 17, which copies this shape.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - Kinds and registry** - `IMissionStepKind` exactly as in Data / contracts, `GotoStep`, `KillGroupStep`, `TalkStep` (constructed with its name, so `talk` and `return` share it), and `MissionStepRegistry` with `Builtin()`, `Add`, `Find`, each kind's body lifted from the matching branch of `MissionManager`. No consumer yet. *Done when:* `dotnet build` is clean and `dotnet test tests/Veld.Tests` still passes 63.
- [x] **Step 2 - Mission manager on the registry** - write `A_kind_added_to_the_registry_drives_a_step` (a test-only `AlwaysDoneStep` registered as `always_done` makes a mission advance on the first `Update`) against the five-argument constructor and see it fail to compile (the red); then add the constructor, replace the switches and string checks with lookups, add the unknown-kind notification, and see it pass. *Done when:* that test is green, the eleven 15a mission tests are green without modification, `dotnet build` is clean, and the smoke run passes 46 of 46 including "mission 'parcel' completed by walking its steps" and "HUD has an objective marker".
- [x] **Step 3 - Kind tests** - the cases under Testing, one first written with a wrong literal to see the suite go red, then corrected. *Done when:* `dotnet test tests/Veld.Tests` passes with the new cases and the whole suite is green.

## Files / areas

- `scripts/Gameplay/Steps/IMissionStepKind.cs`, `GotoStep.cs`, `KillGroupStep.cs`, `TalkStep.cs`, `MissionStepRegistry.cs` (new)
- `scripts/Gameplay/MissionManager.cs`
- `tests/Veld.Tests/Gameplay/Steps/MissionStepKindsTests.cs`, `MissionStepRegistryTests.cs` (new); `tests/Veld.Tests/Gameplay/MissionManagerTests.cs` (one added test, existing tests untouched)

## Data / contracts

Load-bearing for item 25, which adds kinds through this interface.

```csharp
namespace Veld.Gameplay.Steps;

/// <summary>One kind of mission step. Polled kinds answer Poll; event kinds answer OnTalked.</summary>
public interface IMissionStepKind
{
    string Type { get; }
    /// <summary>Checked every frame while the step is current; true when it is done.</summary>
    bool Poll(MissionStep step, IWorldQuery world) => false;
    /// <summary>The player has finished talking to npcId; true when that completes the step.</summary>
    bool OnTalked(MissionStep step, string npcId, string giver) => false;
    /// <summary>A message when a character dies during this step, or null when the kind does not care.</summary>
    string? OnNpcKilled(MissionStep step, INpcView npc, IWorldQuery world) => null;
    /// <summary>Where the HUD points for this step, or null.</summary>
    Vector3? Marker(MissionStep step, IWorldQuery world, string giver);
}

public sealed class MissionStepRegistry
{
    public static MissionStepRegistry Builtin();      // goto, kill_group, talk, return
    public MissionStepRegistry Add(IMissionStepKind kind);   // replaces a kind of the same name; returns this
    public IMissionStepKind? Find(string type);
}
```

`giver` is passed so `talk` and `return` can default their target to the
mission giver exactly as today. Unknown-kind notification text:
"Mission '<title>' has a step of unknown kind '<type>'." once per Start or
Restore, after the "New task" message.

## Testing

The test gate is on. All in-process, on the 15a fakes.

Kind tests (`MissionStepKindsTests`):

- `GotoStep`: `Poll` is true only when the player's x,z is within `Radius` of `Position`; `Marker` is the ground point; `OnTalked` is false.
- `KillGroupStep`: `Poll` is true only when every member of `Group` is dead; `Marker` is the nearest living member to the player and null when none live; `OnNpcKilled` returns "Thief is down. 1 left." when one of two remains and null when the dead character is in another group or none remain.
- `TalkStep("talk")` and `TalkStep("return")`: `OnTalked` is true for the step's `Target`, for the giver when `Target` is empty, and false otherwise; `Marker` is the target character's position; `Poll` is false.

Registry tests (`MissionStepRegistryTests`):

- `Builtin()` finds the four names and returns null for "deliver".
- `Add` makes a new kind findable and replaces an existing one of the same name.

Mission manager (`MissionManagerTests`, one new test): `A_kind_added_to_the_registry_drives_a_step`, and a second, `An_unknown_step_kind_is_reported_when_the_mission_starts`, asserting the exact message.

**Test seams**

- Public interface under test: each `IMissionStepKind` implementation through the interface, `MissionStepRegistry`, and `MissionManager` through its public members as in 15a.
- Behaviour the tests prove: a step kind decides on its own when it is done and where it points, and the mission manager only looks kinds up.
- External boundary, local substitute, or mock: none external; `FakeWorld`, `FakeNpc`, `FakePlayer` as before.
- Red command and expected failing signal: `dotnet test tests/Veld.Tests`; first CS1729 on the five-argument constructor, later an `Assert` failure showing expected versus actual.
- Full Verify command after green: none declared; `dotnet build`, `dotnet test tests/Veld.Tests`, then the smoke run from `AGENTS.md`.

## Notes for the AI

- Lift each branch verbatim into its kind; the behaviour is pinned by the 15a tests, which must not be edited.
- Default interface members keep the kinds to the one or two methods they care about.
- `MissionManager` must not know any kind's name after this; the only strings it keeps are the notification texts.
- The unknown-kind check runs over `m.Steps` once at `Start` and `Restore`; an unknown step is inert (never completes, no marker), not an exception.
- No new `Game.Instance` call sites; `Game` keeps calling the four-argument constructor.
- No AI attribution in commit messages; ask before committing.
