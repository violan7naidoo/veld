# Feature: Simulation and presentation split, part d: player state in the simulation

**From build-plan:** feature 19d
**Status:** complete (2026-10-16)
**Branch:** `feature/player-state`

## Goal

What the player is, as opposed to where they are and what they press, moves
into the `Veld.Sim` library: health and its regeneration, stamina and the
sprint rule, the Dead Eye meter, the weapons carried with their ammunition,
reload and cooldown, whether they are on foot, riding or driving, and death
with the respawn that follows. A `PlayerState` owns it, created by the
`Simulation`, with a `WeaponState` per weapon that both the player and the
NPC bodies use; the shot raycast becomes one game-side helper. The `Player`
node becomes body and view: it reads input, moves, aims, rides, drives and
fires, tells its state what happened, and renders what the state says
through C# events. The game plays identically: smoke run 46 of 46, 147
tests plus the new ones green.

## In scope

- `WeaponState` in `sim/Veld.Sim/Characters/`: one weapon's magazine, reserve, cooldown and reload, moved verbatim from `Weapon.cs` minus the raycast, plus `Spend` (the two lines `Fire` did to the ammunition) and `HitSpread`.
- `Shooting.Fire` in `scripts/Gameplay/Shooting.cs`: the raycast from `Weapon.Fire`, over a `WeaponState` it spends a round from; `ShotResult` and `ShotEffects` move into that file unchanged; `Weapon.cs` goes. Both bodies fire through it.
- `PlayerState` in `sim/Veld.Sim/Characters/`: the rules above, created by `Simulation` from `world.playerWeapons`, over the purse, the wanted system and the notifier; `Simulation.Player` exposes it. Three events tell the body what to render: `DeadEyeChanged`, `Died`, `Respawned`.
- `Player.Init(Main, GameData, PlayerState, UiState)`: the purse, notifier and wanted system leave the body; `Health`, `Stamina`, `DeadEye`, `DeadEyeActive`, `IsDead`, `Weapons`, `CurrentWeapon`, `CurrentWeaponId` forward to the state for the HUD, the save and the harness; `Mount`, `Dismount`, `EnterVehicle`, `ExitVehicle` record the mount on the state.
- `Npc.cs` holds a `WeaponState` and fires through `Shooting`; `SaveGame` reads health, weapon and ammunition from `Sim.Player`; `DevSmoke` names weapons by id instead of `WeaponOrder`; `Main` adapts.
- `coding-standards.md`: the layout line and the seams bullet name `PlayerState` and `WeaponState`. The glossary already carries Standing and Dead Eye.

## Out of scope

- The save as a snapshot of the simulation, dead characters in the simulation, `WorldBuilder.DeadNpcs`: 19e. `SaveGame` keeps its shape and its file; only its sources for the player's health, weapon and ammunition move.
- Ticking the player's state from `Simulation.Tick`. The tick needs this frame's input (aiming, sprint held, moving, on foot) and the real time elapsed under slow motion, so the body calls `Tick` from its physics frame, as the NPC bodies do for their brains; item 26 revisits when input travels through a port.
- The player's position, velocity, yaw, camera, focus and interaction, the horse and vehicle nodes and their own state (the horse's stamina stays the horse's).
- The `IPlayerView` port: it stays, the body implements it and forwards `Heal` and `GiveAmmo` to the state.
- Any behaviour change. One oddity is moved as is and noted below: the respawn fee is two shillings (24 pence) on any map, which reads as R0.24 on the modern one.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - Weapon state and one raycast** - `WeaponState` with `tests/Veld.Tests/Sim/Characters/WeaponStateTests.cs` written red first; `Shooting.cs` takes the raycast, `ShotResult` and `ShotEffects`; `Weapon.cs` deleted; `Player` and `Npc` hold `WeaponState` and fire through `Shooting.Fire`. *Done when:* `dotnet build` clean, `dotnet test tests/Veld.Tests` green at 147 plus the six weapon tests, the Sim fence prints nothing, `scripts/Gameplay/Weapon.cs` is gone, and the smoke run passes 46 of 46 including "fired, … rounds left in magazine".
- [x] **Step 2 - The player's state** - `Mount`, `PlayerState` and `Simulation.Player`, with `tests/Veld.Tests/Sim/Characters/PlayerStateTests.cs` written red first and one more simulation test. Nothing in the game changes. *Done when:* build clean, tests green at the step 1 count plus eleven, the fence prints nothing, `git status` shows no change under `scripts/` beyond step 1.
- [x] **Step 3 - The body on the state** - `Player.Init(Main, GameData, PlayerState, UiState)`; the body forwards, records the mount, calls `Tick` each physics frame with this frame's input, and renders the three events; `Equip` shows the model's weapon after the state accepts it; `SaveGame` reads `_sim.Player`; `DevSmoke` and `Main` adapt. *Done when:* build clean, tests green, the fence prints nothing, `grep -nE "_purse|_notifier|_wanted|\bPurse\b|WantedSystem|\bNotifier\b|_sinceHit|_regen|_deadTimer|Stamina (=|-=|\+=)|DeadEye (=|-=|\+=)" scripts/Characters/Player.cs` finds nothing, and the smoke run passes 46 of 46 including "weapons:", "fired, … rounds left in magazine", "Dead Eye slows time", "Dead Eye ends when aiming stops", "switched to the second weapon" and "save restored money and health".
- [x] **Step 4 - Standards** - the layout line names the player and weapon state under `Characters/`; the seams bullet adds the player's state to "brains decide, bodies act". *Done when:* `grep -n "PlayerState" blueprint/context/coding-standards.md` shows both lines; build, tests and smoke unchanged from step 3 (no code in this step).

## Files / areas

- `sim/Veld.Sim/Characters/WeaponState.cs`, `Mount.cs`, `PlayerState.cs` (new); `sim/Veld.Sim/Simulation.cs`
- `scripts/Gameplay/Shooting.cs` (new); `scripts/Gameplay/Weapon.cs` and its `.uid` (deleted)
- `scripts/Characters/Player.cs`, `scripts/Characters/Npc.cs`, `scripts/Core/SaveGame.cs`, `scripts/Core/DevSmoke.cs`, `scripts/Core/Main.cs`
- `tests/Veld.Tests/Sim/Characters/WeaponStateTests.cs`, `PlayerStateTests.cs` (new); `tests/Veld.Tests/Sim/SimulationTests.cs`
- `blueprint/context/coding-standards.md`

## Data / contracts

Load-bearing for 19e, whose snapshot is this state, and for item 26.

```csharp
namespace Veld.Sim.Characters;

/// <summary>One weapon's ammunition and timing; the raycast lives in the game.</summary>
public sealed class WeaponState
{
    public WeaponState(WeaponData data);      // full magazine, the data's reserve
    public WeaponData Data { get; }
    public int InMag { get; set; }             // settable for the save and the NPC bodies' bottomless reserve
    public int Reserve { get; set; }
    public float Cooldown { get; }
    public float ReloadLeft { get; }
    public bool Reloading { get; }
    public bool CanFire { get; }               // no cooldown, not reloading, a round in the magazine
    public void Tick(float dt);                // cooldown and reload; the magazine fills from the reserve when the reload ends
    public bool StartReload();                 // false when reloading, full, or the reserve is empty
    public void Spend();                       // one round out, cooldown at 1 / rateOfFire
    public float HitSpread(bool aimed);        // spreadDegrees aimed, hipSpreadDegrees otherwise
}

public enum Mount { OnFoot, Riding, Driving }

/// <summary>What the player is: health, stamina, Dead Eye, the weapons carried, the mount, and death and respawn.</summary>
public sealed class PlayerState
{
    public int Health { get; set; }            // settable for the save and the harness
    public int MaxHealth { get; }              // 100
    public float Stamina { get; }
    public bool IsDead { get; }
    public float DeadEye { get; }              // 0 to 100
    public bool DeadEyeActive { get; }
    public IReadOnlyList<WeaponState> Weapons { get; }   // the data's order
    public string? CurrentWeaponId { get; }
    public WeaponState? CurrentWeapon { get; }
    public Mount Mount { get; set; }           // the body records what it did
    public bool Sprinting { get; }             // decided by the last tick

    public event Action<bool>? DeadEyeChanged; // the body sets the engine's time scale, the sky's look and the click
    public event Action? Died;                 // the body dismounts, leaves the car, drops the model
    public event Action? Respawned;            // the body stands the model up at the spawn point

    public void Tick(float dt, float realDt, bool aiming, bool wantSprint, bool moving, bool onFoot);
    public bool Equip(string? id);             // false when that weapon is not carried; null holsters
    public bool ToggleDeadEye();               // only with the meter above 5
    public bool Reload();                      // starts the current weapon's reload; toasts "Out of ammunition for the X." when it cannot
    public bool TakeDamage(int amount);        // true when it killed
    public void ReportGunfire(Vector3 at);     // the wanted system hears the player's shot
    public void Heal();
    public void GiveAmmo(string weaponId, int count);
    public Dictionary<string, int> AmmoSnapshot();
    public void RestoreAmmo(Dictionary<string, int> ammo, string? weapon);
}

public sealed class Simulation
{
    public PlayerState Player { get; }         // from world.playerWeapons, looked up in the weapon data; unknown ids skipped
}
```

The tick, verbatim from `Player.cs`: every weapon ticks; dead counts down
4 s then respawns (health full, stamina 100, wanted cleared, the purse
docked the lesser of its pence and two shillings, any weapon at 0 and 0
given two magazines of reserve, the doctor's toast, `Respawned`); otherwise
health regenerates 4 per second once 6 s have passed since the last hit;
Dead Eye drains 15 per real second while active and switches off when
aiming stops or the meter empties, and refills 2.5 per second while off; on
foot, sprinting holds while it is wanted, the player moves, stamina is above
1 and they are not aiming, draining 14 per second, otherwise stamina refills
10 per second; off foot, stamina is untouched. `realDt` is `dt` divided by
the engine's time scale, which only the body knows. A killing blow switches
Dead Eye off, sets dead, health 0, the mount to on foot, the 4 s timer, the
toast "You are dead." and `Died`.

The body after the move keeps: the capsule, model and camera, `Walk`,
`UpdateFocus`, `TryShoot` (now `Shooting.Fire` plus `ReportGunfire`),
`HandleWeaponKeys` (now `State.Equip` and `State.Reload`), the four mount
and vehicle methods (now also setting `State.Mount`), `SetDeadEye` becomes
the handler of `DeadEyeChanged`, and the body parts of `Die` and `Respawn`
become the handlers of `Died` and `Respawned`. `Speed`, `Aiming`, `Focus`,
`Mounted` and `Driving` (the nodes) stay on the body.

## Testing

The test gate is on. Steps 1 and 2 add logic and ship their tests red
first; steps 3 and 4 move call sites and docs and ride on the suite and the
smoke run.

- `WeaponStateTests`, six facts, over a weapon with magazine 5, reserve 10, rate of fire 2 and reload 2.5 s: it starts full; `Spend` takes one round and sets a half-second cooldown during which `CanFire` is false; `StartReload` refuses when full, while reloading, and with an empty reserve; a reload fills the magazine from the reserve after 2.5 s (reserve 10 to 6 after four shots and a reload); the magazine empty means `CanFire` false; `HitSpread` is the aimed figure when aiming and the hip figure otherwise.
- `PlayerStateTests`, eleven facts, over a `Simulation` with `FakeWorld`, `FakeNotifier`, `FakeDialogueView`, `FakeRandom` and a map with 500 to start and two weapons: the weapons come in the data's order with the first equipped and an unknown id skipped; `Equip` refuses an id not carried and holsters on null; health regenerates by 4 after a quiet second once six seconds have passed, and not before; sprinting on foot drains 14 in a second and refuses once stamina is at or below 1, and stamina refills 10 per second when not sprinting; off foot, stamina does not change; Dead Eye toggles only above 5, drains 15 per real second while active, switches off when aiming stops and raises `DeadEyeChanged` both ways; Dead Eye refills 2.5 per second while off; a killing blow sets dead and health 0, raises `Died`, and toasts "You are dead."; four seconds later it respawns with health 100, stamina 100, wanted level 0, the purse at 476, an empty weapon given two magazines of reserve, `Respawned` raised and the doctor's toast; `Reload` toasts "Out of ammunition for the …" when the reserve is empty and starts the reload otherwise; `Heal` fills health and `GiveAmmo` adds to the reserve.
- `SimulationTests` gains one fact: `Player` carries the map's weapons.

**Test seams**

- Public interface under test: `WeaponState` and `PlayerState` in `Veld.Sim.Characters`, the latter reached through `Simulation.Player`; the seam is `Tick` with the four input flags and the real-time delta, plus the commands above.
- Behaviour the tests prove: what the player is changes only by these rules, and the body learns of death, respawn and Dead Eye only through the three events.
- External boundary, local substitute, or mock: the world, notifier, dialogue view and dice are the ports; the existing fakes stand in. The purse and the wanted system are real.
- Red command and expected failing signal: `dotnet test tests/Veld.Tests`; CS0246 on `WeaponState` in step 1 and on `PlayerState` in step 2.
- Full Verify command after green: none declared; `dotnet build`, `dotnet test tests/Veld.Tests`, the Sim fence from `AGENTS.md`, then the smoke run.

Manual path: play; sprint until the stamina bar empties and watch it
refuse; aim and press the Dead Eye key, watch the world slow and the meter
drain, release aim and watch it stop; fire a magazine empty and reload; take
hits, wait six seconds, watch health climb; die and come to at the station
four seconds later, two shillings lighter, with the wanted level cleared;
save, change health, load.

## Notes for the AI

- Move the rules verbatim; the constants in the Data / contracts section are the ones to copy. `Tick` order is weapons, dead or alive, hit timer and regeneration, Dead Eye, stamina, matching `_PhysicsProcess`.
- `Equip` on the state returns whether the id was accepted; the body shows the model's weapon only then, which is the one place the model follows the state outside the events.
- The body subscribes to the three events in `_Ready` and never calls `Engine.TimeScale`, `SetDead` or the spawn move from anywhere else.
- `DevSmoke` lines that read `WeaponOrder` become `Weapons[0].Data.Id` and `Weapons[1]`; the checks' text stays.
- `SaveGame.Save` reads `_sim.Player.Health`, `CurrentWeaponId` and `AmmoSnapshot()`; `Load` sets `_sim.Player.Health`, calls `_sim.Player.RestoreAmmo` then `player.Equip(_sim.Player.CurrentWeaponId)` so the model follows; it still dismounts, leaves the car and moves the body.
- `Shooting.Fire` keeps the engine's `RandomNumberGenerator` for the spread cone: it is a presentation detail of the raycast, not a simulation decision.
- Delete `scripts/Gameplay/Weapon.cs.uid` with the file.
- No `Node`, `GD`, `Engine` or raycast in the library; the fence after every touch of `sim/`.
- Godot does not rebuild the C# project from the command line; build before every run.
- British spelling in player-facing text, no em dashes.
- No AI attribution in commit messages; ask before committing.
