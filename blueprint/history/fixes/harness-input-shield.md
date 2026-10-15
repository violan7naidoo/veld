# Fix: the smoke and car stress harnesses take real mouse and keyboard input

**Type:** Fix
**Status:** complete (2026-10-15)
**Branch:** `fix/harness-input-shield`

## The problem

Confirmed by `/diagnose` on 2026-10-15. The smoke run captures the mouse
(`Main._Ready` sets `Input.MouseMode = Captured`) and holds keyboard focus, so
while it runs every real click and key from the person at the machine lands in
the game and mixes with the harness's injected actions. The right mouse button
is "aim", which cuts walking speed to 60 percent; a right-click during the
70-tick walk leaves the player short of the car, which `DevSmoke` places a
fixed 7.5 m ahead of the spawn against a 4.8 m interaction reach, so "the car
is the interaction focus", "got into the car" and "got out of the car beside
it" fail together. Shift and E do the same to the sprint and enter checks.

Evidence: four isolated runs were bit-identical (key down at tick 31, up at
101, walk 3.7 m, car at 3.68 m, focus found); synthetic mouse motion changed
the camera's forward vector; holding the right button through the walk
reproduced the exact failing chain every time (walk 2.2 m, car at 5.24 m,
focus null, plus "sprinted 1.5 m" and "Dead Eye ends when aiming stops").
Frames per tick, injected-event latency and the car's transform were each
ruled out by probe. `DevCarStress` has the same exposure through the same
capture.

## The fix

Shield both harnesses from real input, and stop the one check that depends on
the walk length from depending on it.

- A `DevInputShield` node under `scripts/Core/`, added by each harness in `_Ready`. It releases the mouse (`Input.MouseMode = Visible`, so the camera ignores motion and the person's cursor is free to click elsewhere) and, in `_Input`, for any `InputEventKey` or `InputEventMouseButton` that is a press, calls `Input.ActionRelease` on every action that event matches. The harness's own events are `InputEventAction` and pass through untouched. The game's own `Input` state is what `Player` reads, so releasing there is what makes a real press inert.
- `DevSmoke` places the car relative to the player's position at frame 134 rather than the spawn: `playerXZ + ahead * 3.5f`, still along the road direction, so the focus check no longer cares how far the walk got.
- The aim probe used in the diagnosis becomes `tools/smoke_aim_probe.ps1`, committed, so the regression check can be run again: it holds the right button from 2 to 25 seconds while a smoke run goes.

Must not break: the smoke's screenshots (the window stays visible and
rendered), the `--quality` and `--shot` arguments, the car stress harness's
own enter and exit cycles, and normal play, which never adds the shield.

## Build steps

- [x] **Step 1 - Input shield** - `DevInputShield` as above; `DevSmoke._Ready` and `DevCarStress._Ready` add it as a child; `tools/smoke_aim_probe.ps1` added. *Done when:* `dotnet build` is clean, 107 tests still green, a smoke run with the probe held (red before this step: 39 of 46) passes 46 of 46, a smoke run without the probe passes 46 of 46, and the car stress run with the probe held ends with zero failures out of 40.
- [x] **Step 2 - Car placed relative to the player** - the frame 134 placement in `DevSmoke` uses the player's position plus 3.5 m along the road direction. *Done when:* a plain smoke run passes 46 of 46 and "the car is the interaction focus" passes, and a run with the probe held still passes.

## Verify

- Red first, on main before step 1: build, then run the smoke while the probe holds the right button; expect 39 of 46 with the car chain failing.
- Green after step 1: the same command passes 46 of 46; a plain run passes 46 of 46; `-- --carstress=40 --quality=0` with the probe held reports zero failures.
- After step 2: a plain run passes and the car line in the log shows the car within 4 m of the player at the focus check.
- Manual: start the smoke and move the mouse and type freely in another window while it runs; it still passes, and the cursor is never trapped.

The probe command, from the repo root:

    powershell -NoProfile -ExecutionPolicy Bypass -File tools/smoke_aim_probe.ps1 &
    "...\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --smoke --quality=0

## Notes for the AI

- Build before every Godot run; Godot does not rebuild the C# project from the command line, and a stale DLL cost the diagnosis one false lead.
- The shield is dev-only: nothing in `Main` adds it, only the two harnesses, so the game's input is untouched.
- Match every action with `InputMap.EventIsAction(@event, action)` over `InputMap.GetActions()`; do not hard-code action names.
- Keep `DevSmoke`'s road-centreline intent in the placement comment: the car must land on tar, not in a wall.
- No AI attribution in commit messages; ask before committing.
