# Fix: Car flips into the air or falls through the map when you get in

**Type:** Fix
**Status:** complete (2026-10-14)
**Branch:** `fix/car-unfreeze-launch`

## The problem

Confirmed by `/diagnose` on 2026-10-14 with the `--carstress` harness
(`scripts/Core/DevCarStress.cs`, 40 parked cars, deterministic).

**Root cause.** `Vehicle._PhysicsProcess` applies the righting torque (and the
speed-squared downforce) every physics frame whether or not the body is frozen.
A frozen `RigidBody3D` does not consume applied forces, so the torque
accumulates for as long as the car stands parked and discharges the instant the
driver gets in, when `SetDriver` sets `Freeze = false`.

Evidence:

- Baseline run: 20 of 40 cars failed. Roll up to 176 degrees, launches up to
  28 m at 35 m/s (the flip); two cars then fell through the heightmap to -10 m
  on the second entry (the fall off the map).
- Severity grew with how long each car had been parked before entry.
- The only two cars that stayed still were the two with roll under the 0.02 rad
  threshold at which the torque is applied.
- Probe: an early return while `Freeze` is true, one line, gave 0 of 40 on the
  same cars. Logs: `carstress1.log`, `carstress2.log` in the session scratchpad.

**Secondary cause.** Cars are frozen at a velocity trough while the suspension
is still over-compressed: every parked car sits 0.34 m below its origin's
resting height of about 0.1 m. Even with the main cause removed, each unfreeze
produces a uniform 0.4 m, 1.8 m/s hop. The freeze rules written today (freeze
instantly on exit, or after one second regardless of state) make this worse than
the original rule did.

**Third cause, the fall off the map on its own.** `Player.ExitVehicle` restores
the player's collision layer in the same frame it moves the player out of the
car. The physics server resolves that step with the capsule still at the seat,
inside the car's collision boxes, and the kick (30 to 85 m/s in one step,
independent of the parking brake value and still 20 to 40 m/s with the player
teleported 8 m away) punches the car into or through the heightmap. The instant
freeze on exit had been masking it. Evidence: with the player's collision kept
off for that one step, 0 of 12 cars moved at all (worst 0.3 m/s); with it on,
12 of 12 failed.

## The fix

1. **No forces on a frozen body.** Once the parked logic has run, return from
   the physics step while `Freeze` is true: no torque, no downforce, no engine or
   brake writes.
2. **Freeze only when settled.** Freeze after the car's speed has stayed below
   0.25 m/s for 15 consecutive physics frames, with a 3 s cap so a car on a steep
   slope still gets pinned. On exit, brake hard and let the same rule pin it
   instead of freezing instantly at speed.
3. **Restore the player's collision one physics frame after leaving the car.**
   `ExitVehicle` moves the player and sets a flag; the next `_PhysicsProcess`
   restores the layer and mask. One frame without collision at a spot 0.2 m above
   the ground is harmless.

Must not break: parked cars staying put on hills, getting out of a moving car
without it tunnelling, the R20 recall, and the smoke run's car checks.

## Build steps

- [x] **Step 1 - No forces while frozen** - the early return in `Vehicle._PhysicsProcess`. *Done when:* `--carstress=40 --quality=0` exits 0 with "0 of 40" and the harness's worst entry speed is still the 1.8 m/s hop (the baseline for step 2).
- [x] **Step 2 - Settled-only freeze** - replace the instant freeze on exit and the one-second cap with the 15-frame settle rule and 3 s cap; the exit path brakes hard first. *Done when:* `--carstress=40` reports every car with first-entry lift under 0.15 m and speed under 1.5 m/s (the exit kick of the third cause still shows on the second entry until step 3 lands).
- [x] **Step 3 - Deferred collision restore** - the flag in `Player.ExitVehicle` and its handling at the top of `_PhysicsProcess`. *Done when:* `--carstress=40` exits 0 with "0 of 40" and a worst entry under 1.5 m/s and 0.2 m lift, and the smoke run passes 46 of 46 including "parked car stayed put", "got out of the car beside it" and "H brings the car back".
- [x] **Step 4 - Keep the harness** - `DevCarStress` stays as the regression check: its summary line reports the worst entry speed and lift; `AGENTS.md` Commands and the README's "Checking that nothing broke" list the command. *Done when:* both files mention `--carstress` and the harness prints the worst-case line.

## Verify

- `dotnet build`, then `"GODOT\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --carstress=40 --quality=0` exits 0 with "0 of 40", then the smoke run exits 0.
- Manual: park on the hill outside 42 Road 721, get out, wait ten seconds, get back in: no jump, no roll. Drive to the stadium, get into a parked car there: same.

## Regression seam

No unit seam exists: the behaviour lives inside Godot's physics step, so the
`--carstress` harness is the regression test. For `/architecture` later:
`Vehicle` talks to the physics server directly from `_PhysicsProcess`; a
parked-or-settling state machine separated from the node would be testable
without the engine. That belongs with feature 19.
