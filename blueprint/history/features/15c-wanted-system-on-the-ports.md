# Feature: Game services seam, part c: the wanted system on the ports

**From build-plan:** feature 15c
**Status:** complete (2026-10-14)
**Branch:** `feature/services-seam-wanted`

## Goal

`WantedSystem` stops reaching into the `Game` singleton. It takes the world
query (for the police, the player and the era) and the notifier, and its heat,
witness, decay and provocation rules get behaviour tests with fakes. With this
the three gameplay systems of item 15 all run without the engine. The game plays
identically: the smoke run stays 46 of 46, including "attacking the law raised
wanted level to 1" and "they are now hostile".

## In scope

- `WantedSystem` constructed as `(IWorldQuery world, INotifier notifier)`; `ReportAttack(INpcView victim, IPlayerView attacker, bool killed)` and `OnGunfire(Vector3 position, INpcView? shooter)`.
- The three callers that pass node types: the NPC passes itself (it is an `INpcView`), the player passes `null` as the shooter since it is not an NPC.
- Composition in `Game._Ready` with the shared `LiveWorld`.
- Tests: `tests/Veld.Tests/Gameplay/WantedSystemTests.cs` on the existing fakes.

## Out of scope

- No new ports; everything needed exists from 15a and 15b.
- The HUD, minimap, map and NPC code that read `Game.Instance.Wanted.Level` stay as they are (feature 18).
- The numbers themselves (heat per offence, the 90 m sight radius, the 110 m witness radius, the 6 s grace, the 1.5 per second decay) are kept exactly; the tests pin them.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - Wanted system on the ports** - write the first test, `Attacking_the_police_raises_the_level_and_provokes_them`, against the new constructor and see it fail to compile (the red); then change the constructor and the two method signatures, replace every `_game.` use with a port, update the three callers, and compose it in `Game._Ready`. *Done when:* that test is green, `dotnet build` is clean, and the smoke run passes 46 of 46 including the two wanted checks.
- [x] **Step 2 - Behaviour tests** - the cases under Testing, one first written with a wrong literal to see the suite go red, then corrected. *Done when:* `dotnet test tests/Veld.Tests` passes with the new cases and the whole suite is green.

## Files / areas

- `scripts/Gameplay/WantedSystem.cs`, `scripts/Core/Game.cs`
- `scripts/Characters/Npc.cs` (two call sites), `scripts/Characters/Player.cs` (one call site)
- `tests/Veld.Tests/Gameplay/WantedSystemTests.cs` (new)

## Data / contracts

No new shapes. The public surface others read stays: `Level`, `Heat`, `Label`,
`SetLevel`, `ClearWanted`, `Update`, and the `IWantedState` port from 15b.

## Testing

The test gate is on. Everything is in-process on `FakeWorld`, `FakeNpc`,
`FakePlayer` and `FakeNotifier`. Distances are built from literal positions on
flat ground. Modern-era wording is used (the fake world defaults to modern) with
one case for the 1901 wording.

Cases for `WantedSystemTests`:

- `Label` is "" at level 0, "WANTED" at 1, "WANTED: patrols alerted" at 2, "WANTED: shoot on sight" at 3; `SetLevel` clamps to 0 to 3 and sets heat to 30, 70, 110 for levels 1 to 3 and 0 for level 0.
- Hurting a police officer adds 30 heat (level 1); killing one adds 60 (level 2); from a clear record the message is "The police have been called. Armed officers will shoot on sight." once, and every living officer within 150 m of the attacker is provoked while one at 200 m is not. On a 1901 map the message is "The garrison has been alerted. Soldiers will shoot on sight."
- Attacking an outlaw changes nothing.
- Hurting a civilian with no officer within 110 m of the victim changes nothing; with one at 100 m it adds 15, and killing adds 45.
- Heat caps at 120 and the level at 3 after repeated attacks.
- `Update` never decays while an officer is within 90 m of the player; with none in sight, heat holds for 6 s then falls 1.5 per second, and when it reaches zero the level drops to 0 with "The police have lost interest in you." said once.
- `OnGunfire` alerts every character within 35 m of the shot except the shooter, with the shot's position.
- `ClearWanted` zeroes level and heat and un-provokes the police.

**Test seams**

- Public interface under test: `Veld.Gameplay.WantedSystem` through its constructor, `ReportAttack`, `OnGunfire`, `Update`, `SetLevel`, `ClearWanted`, `Level`, `Heat`, `Label`, observed through the fakes.
- Behaviour the tests prove: the police respond to what they see, keep looking for a while, and then lose interest, exactly as the numbers in the code say.
- External boundary, local substitute, or mock: none external; the fakes are the second adapters. Never mock the system's internals.
- Red command and expected failing signal: `dotnet test tests/Veld.Tests`; first CS1729 on the new constructor, later an `Assert` failure showing expected versus actual.
- Full Verify command after green: none declared; `dotnet build`, `dotnet test tests/Veld.Tests`, then the smoke run from `AGENTS.md`.

## Notes for the AI

- No new `Game.Instance` call sites. The player's gunfire call passes `null` for the shooter; the exclusion only ever mattered for NPC shooters because the player is not in the NPC list.
- Keep `using Veld.Characters` for the `Faction` enum; drop nothing else that is still used.
- `Update` with no player bound must keep behaving as today (no officer counts as seen).
- Pin the numbers with literals in the tests; do not recompute them from the code's constants.
- No AI attribution in commit messages; ask before committing.
