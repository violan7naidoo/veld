# AGENTS.md

Instructions for AI coding agents working in this project. This is the cross-tool
entry point: Codex, Cursor, GitHub Copilot, Gemini CLI, Aider, Zed, Windsurf, and
others read `AGENTS.md`. Claude Code reads `CLAUDE.md`, which imports this file, so
there is a single source of truth.

> This is **AI_Blueprint-V3**: the standard AI Blueprint lifecycle below,
> strengthened with a small set of engineering disciplines (diagnosis before
> repair, TDD at agreed seams, a domain glossary, deep-module vocabulary,
> two-axis review, expanded prototyping, cited research) adapted from Matt
> Pocock's Skills For Real Engineers. The pipeline, the file map, and every
> approval gate are unchanged - see `blueprint/context/NOTICE.md` for what
> was added and where it came from.

## What this is

Veld is an open-world game set in Montford, Unit 7 of Chatsworth, Durban,
built from real OpenStreetMap and elevation data: 14,000 houses on their real
outlines, the real streets, driving, residents with dialogue, missions, police
and a wanted system. Godot 4.7.2 .NET, C# on .NET 8, everything built in code
from JSON under `data/maps/`. The root `README.md` describes the game; this
file describes how agents work on it.

This project is built with the **AI Blueprint**, a workflow layer, not an
app skeleton. To start a new project, scaffold the app first in an empty folder
(create-next-app, Vite, etc.), then overlay these files on top. Never run a
framework scaffolder inside a directory that already holds the blueprint files
(`AGENTS.md`, `CLAUDE.md`, `.agents/`, `.claude/`, `blueprint/`); it fails
because the directory isn't empty.

New here? `blueprint/README.md` explains the whole workflow.

## Read these for full context

- `blueprint/context/project-overview.md` - the project's source of truth
- `blueprint/context/coding-standards.md` - conventions to follow
- `blueprint/context/ai-interaction.md` - how to work with the user on this project
- `blueprint/context/current-feature.md` - the one feature, fix, or rollback being built right now

## Domain language

When a task changes project terminology or domain rules, read
`blueprint/context/domain-language.md` and relevant records under
`blueprint/decisions/`. Keep implementation detail in the active spec, not in
the glossary. Both are maintained lazily by `feature`, `fix`, `diagnose`, and
`architecture` - see each skill for when it updates them.

## Workflow

Build one feature, fix, or rollback at a time, behind review gates. Each step's instructions
are plain markdown skills any capable agent can read and follow. The workflow is
exposed through tool-specific adapters:

- Codex: `.agents/skills/<skill>/SKILL.md`
- Claude Code: `.claude/skills/<skill>/SKILL.md`

Unused adapters can be removed. Codex-only projects can delete `CLAUDE.md` and
`.claude/`. Claude Code-only projects can delete `.agents/`, but should keep
`AGENTS.md` because `CLAUDE.md` imports it.

When changing shared workflow behavior, update the matching skill in both
adapter folders so Codex and Claude Code stay aligned.

Core skills:

- `onboard` - tune commands, standards, visibility, ignore rules, and tool adapters after overlaying the Blueprint onto a freshly scaffolded or early project
- `doctor` - read-only Blueprint health check for setup, adapters, plans, overview freshness, and workflow drift
- `adopt` - bootstrap the Blueprint into an existing brownfield app with shipped features
- `overview` - distill the two planning docs into `blueprint/context/project-overview.md`
- `brief` - read-only briefing on an upcoming build-plan feature (scope, dependencies, size) before you spec it
- `feature` - turn a build-plan item into a spec, or propose a reviewed plan addition for a genuinely new feature
- `fix` - document an ad-hoc bug or change into `blueprint/context/current-feature.md`; routes hard, intermittent, or unclear-root-cause bugs through `diagnose` first
- `diagnose` - build a tight feedback loop and confirm a bug's root cause with evidence before `fix` writes the repair spec; never applies the fix itself
- `research` - investigate a question against primary sources and leave a cited note under `blueprint/research/` for the active spec
- `tests` - add or normalize unit testing and turn on the test gate
- `ci` - explicitly set up one project-specific Verify command and matching automatic GitHub checks
- `implement` - build the current spec one small, reviewed step at a time
- `check` - prove the current spec against the running app
- `try` - read-only manual review guide: where to go, what to click, what to expect
- `audit` - branch-aware or full-project review for code quality, security, performance, tests, and standards drift, reported through separate Spec and Standards lenses; records findings with durable IDs and statuses in `blueprint/context/findings.md`, where open or fixed P0/P1 findings block `complete`
- `architecture` - explicit-only, read-only scan for deep-module opportunities across the whole codebase; proposes a normal `feature` or `fix`, never refactors inline
- `rollback` - plan a safe reversal of a completed feature from its archive and exact git commit, with later-dependency review before code changes
- `complete` - run the final safety pass, log features, fixes, or rollbacks under `blueprint/history/`, then merge with approval
- `release` - optional Render or Vercel deployment readiness, local config, env review, and smoke-test planning
- `prototype` - optional, pre-build static mockups to lock the look
- `status` - read-only progress summary, workflow drift warning, and suggested next action

In Codex, invoke these as skills (`$onboard`, `$overview`, `$feature`,
`$implement`, and so on) or ask naturally, such as "run the overview." In Claude
Code, use the slash commands (`/onboard`, `/overview`, `/feature`, and so on). In
tools without native skills, follow the matching `SKILL.md` manually. The
conventions in `blueprint/context/` apply however a step is invoked.

Optional explicit-only skill: `autopilot` can run one bounded spec/build/check
and targeted-audit pass when directly invoked. It may create checkpoint commits
on the feature or fix branch after passing steps, repair confirmed P0/P1 findings
within scope, and rerun affected checks. It stops before `/complete`, merge, push,
deploy, or destructive actions.

Deployment is also explicit. `/release` can prepare local Render or Vercel config
and run readiness checks, but it must stop before deploy, remote service changes,
push, or publish unless the user gives a separate yes in the current chat.

## Automatic verification

Automatic GitHub checks are a separate explicit setup. `/onboard` and `/adopt`
only report existing checks and point to `/ci` or `$ci` when none exist. Running
`/ci` inspects the real project and defines one `Verify` command from checks that
already exist. Use this order when available: typecheck, tests, then build. Never
invent a test runner or another check just to fill the command.

For JavaScript and TypeScript projects, prefer a package script such as `verify`
and use the detected package manager. For other stacks, use the native task
runner or exact combined command. Record the exact command under Commands below.

The optional `.github/workflows/verify.yml` must run that same command for pull
requests and pushes to the default branch. Preserve existing workflows, use the
project's real runtime and install command, and grant only `contents: read` by
default. This setup does not add local git hooks, coverage, browser tests,
security scans, or version matrices. Those remain later project choices.

GitHub branch protection or a ruleset can require the check after the repository
is pushed, but that is a separate remote setting. Missing automatic GitHub
checks do not make the Blueprint unusable.

## Commands

Windows. `GODOT` below stands for
`C:\Users\Damian work\Downloads\Godot_v4.7.2-stable_mono_win64\`. The console
variant prints to the terminal; the plain one does not.

- Build: `dotnet build`
- Run (no editor): `play.cmd`, or `"GODOT\Godot_v4.7.2-stable_mono_win64.exe" --path .`
- Editor: `open-editor.cmd`
- Smoke test: `"GODOT\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --smoke`
  (optional `--shot=C:/temp/veld` for screenshots, `--quality=0|1|2` for the
  graphics tier). Prints `PASS`/`FAIL` lines and exits non-zero on failure.
  This is the regression gate until a unit test command exists.
- Car stress: `"GODOT\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --carstress=40 --quality=0`
  gets into 40 parked cars and fails any that flip, lift, sink or bolt. Run it after any change to
  `Vehicle.cs` or the player's enter and exit code.
- Regenerate the map: `python tools/osm_to_map.py tools/source/montford_osm.json tools/source/montford_elevation.json data/maps/chatsworth-unit7`
- Lint: none configured.
- Test: `dotnet test tests/Veld.Tests` (xUnit over the `sim/Veld.Sim` library, pure logic only;
  builds the library and the tests, never the Godot project; see the Testing section of
  `blueprint/context/coding-standards.md`). Watch: `dotnet watch test --project tests/Veld.Tests`.
- Sim fence: `grep -rnE "GD\.|FileAccess|ResourceLoader|\bOS\.|\bNode\b|Node3D|ProjectSettings|\bInput\b" sim/ --include=*.cs`
  must print nothing. Run it after any change under `sim/`; the library may use the `GodotSharp`
  math structs and nothing else from the engine.
- Verify: not declared. Run `/ci` when you want one combined command and
  automatic GitHub checks; note the smoke test needs a Godot download on the
  runner.
