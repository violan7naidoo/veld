# Feature: Unit test runner

**From build-plan:** feature 14
**Status:** complete (2026-10-14)
**Branch:** `feature/unit-tests`

## Goal

Give the project a unit test runner so that every logic-bearing step from here
on ships with a test, which is the precondition for refactoring the game
without breaking it. After this feature, `dotnet test tests/Veld.Tests` runs
xUnit tests against the game's pure logic, the command is recorded in
`AGENTS.md`, and the test gate in `coding-standards.md` is live.

## In scope

- `tests/Veld.Tests`, an xUnit project on .NET 8 referencing the game project.
- Keeping the test sources out of the game assembly and out of the Godot editor's import scan.
- First tests at two pure seams that already exist: `Money.Format` and `Factions.Parse` / `Factions.Label`.
- The test command in `AGENTS.md`, the solution file, the README's "Checking that nothing broke" section, and the Testing section of `coding-standards.md` reflecting that the gate is now on.

## Out of scope

- Tests for anything that needs a Godot scene or node (rendering, physics, input). Those stay with the smoke run.
- Making `MissionManager`, `DialogueSystem` or `WantedSystem` testable; that is feature 15's seam.
- A combined Verify command or GitHub Actions; `/ci` owns those.
- Replacing the static `Money.Modern` flag; noted below as a smell for feature 15.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - Test project** - add `tests/Veld.Tests/Veld.Tests.csproj` (`Microsoft.NET.Sdk`, `net8.0`, `RollForward LatestMajor`, nullable, packages `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, a `ProjectReference` to `../../Veld.csproj`), add it to `Veld.sln`, and fence it off: `<Compile Remove="tests/**" />` in `Veld.csproj` so the game assembly stops globbing the test sources, and an empty `tests/.gdignore` so the Godot editor does not scan the folder. *Done when:* `dotnet build` at the repo root builds both projects with no warnings in the touched files, `dotnet test tests/Veld.Tests` reports zero tests found, and the smoke run still passes.
- [x] **Step 2 - Money tests** - `tests/Veld.Tests/Core/MoneyTests.cs`, one class so the shared `Money.Modern` flag is never toggled by two tests at once: 1901 formatting (`522` is `£2 3s 6d`, `12` is `1s`, `0` is `0d`, `-30` is `-2s 6d`), modern formatting (`15000` is `R150.00`, `5` is `R0.05`, `-2000` is `-R20.00`). Each test sets `Money.Modern` explicitly. Expected strings are literals. *Done when:* `dotnet test tests/Veld.Tests` reports the Money tests passing, after a deliberately wrong expectation was seen failing once and corrected.
- [x] **Step 3 - Factions tests** - `tests/Veld.Tests/Characters/FactionsTests.cs`: `Parse` is case-insensitive and unknown or null input is `Civilian`; `Label` gives "Police" for British when `Factions.Modern` is true and "British Army" when false, and "Gang" / "Outlaw" likewise. Same one-class rule for the static flag. *Done when:* the suite passes with both classes and `dotnet build` is clean.
- [x] **Step 4 - Commands and docs** - `AGENTS.md` Commands gains `Test: dotnet test tests/Veld.Tests`; `README.md` "Checking that nothing broke" lists the test command before the smoke run; `coding-standards.md` Testing section says the runner exists and the gate is on. *Done when:* the three files mention the command, and `dotnet build`, `dotnet test tests/Veld.Tests` and the smoke run all pass.

## Files / areas

- `tests/Veld.Tests/Veld.Tests.csproj` (new), `tests/.gdignore` (new)
- `tests/Veld.Tests/Core/MoneyTests.cs`, `tests/Veld.Tests/Characters/FactionsTests.cs` (new)
- `Veld.csproj` (compile exclusion), `Veld.sln` (project entry)
- `AGENTS.md`, `README.md`, `blueprint/context/coding-standards.md`

## Data / contracts

- Test project layout mirrors `scripts/`: `tests/Veld.Tests/<Area>/<Type>Tests.cs`.
- Test command: `dotnet test tests/Veld.Tests`. Load-bearing: `/implement` and `/complete` run whatever `AGENTS.md` declares.
- No game data shapes change.

## Testing

- Step 1 is proven by the build, by `dotnet test` finding the project, and by the smoke run.
- Steps 2 and 3 are the tests themselves; each must be seen red once.
- Step 4 is proven by the three commands passing on the final tree.

**Test seams**

- Public interface under test: `Veld.Core.Money.Format(int) -> string` with the `Money.Modern` flag as its one piece of configuration; `Veld.Characters.Factions.Parse(string) -> Faction` and `Factions.Label(Faction) -> string` with the `Factions.Modern` flag.
- Behaviour the tests prove: an amount in pence or cents renders as the player sees it on the HUD; a faction name from JSON maps to the right faction and label for the era.
- External boundary, local substitute, or mock: none. Both modules are in-process pure computation; the only shared state is the two static era flags, handled by keeping each module's tests in one class.
- Red command and expected failing signal: `dotnet test tests/Veld.Tests` with one wrong literal; the failure names the test and shows expected versus actual strings.
- Full Verify command after green: none declared yet; run `dotnet build`, `dotnet test tests/Veld.Tests`, then the smoke run from `AGENTS.md`.

## Notes for the AI

- The game project is `Godot.NET.Sdk`, which globs every `.cs` under the project folder. Without the `Compile Remove` the test files and the test project's generated `obj` sources would be compiled into the game and break the editor build. Do this in Step 1, not later.
- The tests reference the game assembly, which drags in `GodotSharp`. That is fine as long as the code under test never calls into the engine; `Money` and `Factions` use only `System` and Godot's pure `Color` struct. Do not add tests for `Terrain.PointInPolygon` or `AddressIndex` here; those sit on node types and belong after feature 15 moves them.
- xUnit runs test classes in parallel. Both modules keep era state in a static flag, so all tests for a module live in one class and set the flag at the start of every test. Record `Money.Modern` and `Factions.Modern` as a smell for feature 15 (era should be a parameter or an injected setting).
- First `dotnet restore` of the test packages needs the network; say so if it fails.
- No AI attribution in commit messages; ask before committing; one commit per step only if the user wants checkpoints.
