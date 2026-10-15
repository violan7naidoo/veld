# Feature: Simulation and presentation split, part c: NPC decisions in the simulation

**From build-plan:** feature 19c
**Status:** complete (2026-10-15)
**Branch:** `feature/npc-brain`

## Goal

Every decision the `Npc` node makes today moves into a `NpcBrain` in the
`Veld.Sim` library, created by the `Simulation` and tested without a scene:
who is hostile and why, what a hit does (provoke, wanted, standing, flee or
fight, death and the mission report), when an alert makes someone run, what
searching a body yields, the idle, wander, patrol, chase, attack and flee
state machine with its timers and targets, and when to shoot and how
accurately. The node becomes body and view: it feeds the brain its senses
each physics frame (positions, sight, actual speed, the weapon's state) and
carries out the brain's intent (face, move, fire, reload, rest, show the
name), keeping the physics, the raycasts, the effects and the animation.
Randomness becomes a port so the dice are testable. The game plays
identically: smoke run 46 of 46, 120 tests plus the new ones green.

## In scope

- `IRandom` port in `sim/Veld.Sim/Ports/`, `GodotRandom` adapter in `scripts/Core/`, `FakeRandom` in the test fakes; the `Simulation` takes an `IRandom` and hands it to the brains it creates.
- `sim/Veld.Sim/Characters/`, namespace `Veld.Sim.Characters`: `NpcState`, `NpcSenses`, `NpcIntent`, `NpcBrain`. `Simulation.CreateBrain(NpcData, INpcView self)` builds one over the simulation's own systems, the world port and the random port; the weapon comes from the data's weapon id.
- The brain owns: faction, health, dead, provoked, looted, the state, home, target, patrol index and every timer; `IsHostile`; `Settle`, `TakeDamage`, `MarkDeadSilently`, `Alert`, `Loot`, `Think`. Every rule and constant moves verbatim from `Npc.cs`; the only change is that dice roll through `IRandom`.
- The body keeps: the capsule, the model and label, gravity, yaw and velocity steering, `MoveAndSlide`, the sight raycast (handed to the brain as a lazy delegate), the shot raycast with its effects, sound and damage application, the `Weapon` instance with its ammo, `DeadNpcs` bookkeeping and the collision change on death, dialogue opening, `IInteractable` and `INpcView`.
- `Npc.Init(Main, GameData, NpcBrain, DialogueSystem)`; `WorldBuilder.Init(Main, Simulation, Notifier)`, since it is the factory that creates brains for the bodies it spawns; `Main` adapts.
- `coding-standards.md`: the layout line gains `Characters/`, the seams bullet says brains decide and bodies act, and that a node which composes other nodes may hold the `Simulation` as a factory. The glossary already carries Brain, Body, Senses and Intent.

## Out of scope

- Ticking brains from `Simulation.Tick`. The senses come from the body (a raycast, the physics velocity, the weapon), so the body calls `Think` from its physics frame and the brain stays a pure function of what it is told. Moving the call into the simulation needs the sight query and the weapon state behind ports, which arrive with 19d (weapons and ammo) and item 26.
- The `Weapon` class, its ammo and reload timing: 19d. The brain reads `MagEmpty` and `CanFire` from the senses and asks for `Fire` or `Reload`.
- `DeadNpcs` and `MarkDeadSilently` on load staying in the game: 19d makes the save a snapshot of the simulation.
- The player's own decisions, the horse, vehicles, `HumanoidModel`, `Factions.Outfit` colours.
- Any behaviour change. Dice through a port roll the same ranges; the react, flee, lost, stuck and wait timers keep their values; speeds, distances and spreads are the same numbers.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - The random port** - `IRandom`, `GodotRandom` over `GD.RandRange`, `FakeRandom` (a fraction between the bounds, default 0.5); `Simulation` gains `IRandom random` as its last constructor parameter and keeps it; `Main` passes `new GodotRandom()`; `SimulationTests` pass a `FakeRandom`. *Done when:* `dotnet build` clean, `dotnet test tests/Veld.Tests` green at 120, the Sim fence prints nothing, the smoke run passes 46 of 46.
- [x] **Step 2 - The brain, part one: hostility, hits, death, alerts, loot** - `NpcState`, `NpcBrain` with `Faction`, `Armed`, `Health`, `IsDead`, `Provoked`, `Looted`, `State`, `IsHostile`, `Settle`, `TakeDamage`, `MarkDeadSilently`, `Alert`, `Loot`; `Simulation.CreateBrain`; `tests/Veld.Tests/Sim/Characters/NpcBrainTests.cs` written red first. Nothing in the game changes. *Done when:* build clean, tests green at 120 plus the twelve brain tests, the fence prints nothing, `git status` shows no change under `scripts/`.
- [x] **Step 3 - The body on the brain, part one** - `Npc.Init(Main, GameData, NpcBrain, DialogueSystem)`; `Faction`, `Health`, `IsDead`, `Provoked`, `IsHostileTo`, `TakeDamage`, `Alert`, `MarkDeadSilently`, `CanInteract` and `Interact` delegate to the brain, with the body keeping the visuals, the collision change and `DeadNpcs`; `WorldBuilder.Init(Main, Simulation, Notifier)` creates each brain in `SpawnNpc`; `Main` adapts. *Done when:* build clean, tests green, the fence prints nothing, `grep -nE "_purse|_reputation|_missions|_wanted|Reputation|WantedSystem|MissionManager" scripts/Characters/Npc.cs` finds nothing, the smoke run passes 46 of 46 including "attacking the law raised wanted level to 1", "they are now hostile" and "dialogue opened and input locked".
- [x] **Step 4 - The brain, part two: the state machine and the shot** - `NpcSenses`, `NpcIntent`, `NpcBrain.Think`; `tests/Veld.Tests/Sim/Characters/NpcBrainThinkTests.cs` written red first. Nothing in the game changes. *Done when:* build clean, tests green at the step 2 count plus the thirteen think tests, the fence prints nothing, no change under `scripts/`.
- [x] **Step 5 - The body on Think** - `Npc._PhysicsProcess` builds the senses, calls `Brain.Think`, and applies the intent: yaw towards `FaceTo`, velocity along forward at `Speed`, gravity, `MoveAndSlide`, the label, aiming and animation; `Fire` runs the raycast, effects, sound and damage with the intent's spread; `Reload` starts the weapon's reload; `Rest` applies gravity only. The state enum, timers, targets, patrol index and the shoot gating leave the body. *Done when:* build clean, tests green, the fence prints nothing, `grep -cE "State\.|_wait|_lostTimer|_reactTimer|_fleeTimer|_patrolIndex|_stuck|RandRange|RandomNumberGenerator" scripts/Characters/Npc.cs` is 0, `Npc.cs` is under 220 lines, and the smoke run passes 46 of 46 including "npcs spawned", "someone to talk to exists" and the two police lines.
- [x] **Step 6 - Standards** - the layout line for `sim/Veld.Sim/` gains `Characters/` and the random port; the seams bullet says brains decide, bodies act, the body feeds senses and carries out intent, and a node that composes other nodes (`WorldBuilder`) may hold the `Simulation` as a factory. *Done when:* `grep -n "Brain" blueprint/context/coding-standards.md` shows the layout line and the seams bullet; build, tests and smoke unchanged from step 5 (no code in this step).

## Files / areas

- `sim/Veld.Sim/Ports/IRandom.cs`, `sim/Veld.Sim/Characters/NpcState.cs`, `NpcSenses.cs`, `NpcIntent.cs`, `NpcBrain.cs` (new); `sim/Veld.Sim/Simulation.cs`
- `scripts/Core/GodotRandom.cs` (new); `scripts/Core/Main.cs`; `scripts/World/WorldBuilder.cs`; `scripts/Characters/Npc.cs`
- `tests/Veld.Tests/Fakes/FakeRandom.cs`, `tests/Veld.Tests/Sim/Characters/NpcBrainTests.cs`, `NpcBrainThinkTests.cs` (new); `tests/Veld.Tests/Sim/SimulationTests.cs`
- `blueprint/context/coding-standards.md`

## Data / contracts

Load-bearing for 19d, which snapshots the brains' state and moves the weapon
behind a port, and for item 26, where brains run on a server.

```csharp
namespace Veld.Sim;

/// <summary>Dice as a port: the game rolls the engine's generator, tests choose the outcome.</summary>
public interface IRandom
{
    int Range(int minInclusive, int maxInclusive);
    float Range(float min, float max);
}

public sealed class Simulation
{
    public Simulation(GameData data, IWorldQuery world, INotifier notifier, IDialogueView view, IRandom random);
    public NpcBrain CreateBrain(NpcData data, INpcView self);   // weapon looked up by data.Weapon; null when none
}
```

```csharp
namespace Veld.Sim.Characters;

public enum NpcState { Idle, Wander, Patrol, Chase, Attack, Flee, Dead }

/// <summary>What the body reports each tick. CanSeePlayer is a delegate so the raycast runs only when the brain asks.</summary>
public readonly record struct NpcSenses(
    Vector3 Position, Vector3 Right,
    Vector3 PlayerPosition, bool PlayerDead, bool PlayerMounted, float PlayerSpeed,
    Func<bool> CanSeePlayer,
    float Speed,                     // horizontal speed the body achieved last frame, for the stuck rule
    bool MagEmpty, bool CanFire);

/// <summary>What the brain asks of the body this tick.</summary>
public readonly record struct NpcIntent(
    NpcState State,
    bool Rest,                       // far from the player in a passive state: gravity only, no animation
    Vector3? FaceTo, float Speed,    // turn towards FaceTo; move along the body's forward at Speed, negative to back off
    bool Fire, float SpreadDegrees,  // fire one round now with this spread; the brain has already told the wanted system
    bool Reload,
    bool ShowLabel);

public sealed class NpcBrain
{
    public NpcData Data { get; }
    public Faction Faction { get; }
    public bool Armed { get; }
    public NpcState State { get; }
    public int Health { get; }
    public bool IsDead { get; }
    public bool Provoked { get; set; }
    public bool Looted { get; }
    public bool IsHostile { get; }                                   // data.hostile, outlaw, provoked and armed, police when wanted, residents when standing is below -2
    public void Settle(Vector3 home);                                // called by the body at _Ready: home, patrol or idle, the first wait
    public bool TakeDamage(int amount, IPlayerView? byPlayer, Vector3 from);   // true when this blow killed
    public void MarkDeadSilently();                                  // a save says this one is dead: no effects, no mission event
    public void Alert(Vector3 from);                                 // gunfire nearby: the unarmed, outlaws excepted, run
    public int Loot();                                               // pence found; pays the purse, drops the coins, notifies; 0 on a second search
    public NpcIntent Think(in NpcSenses senses, float dt);
}
```

Rules the brain carries, unchanged from `Npc.cs` today: hostility as above;
a hit by the player provokes the NPC and its group, reports the attack to
the wanted system, costs one standing (two when killed) for civilians and
residents, and sends the unarmed fleeing or arms a 0.2 s react timer; death
reports to the missions; an alert sends unarmed non-outlaws fleeing 40 m
for 8 s; loot is 18 to 60 on an outlaw, 1 to 9 on anyone else with one
standing lost. The loop: rest beyond 220 m in a passive state; the name
shows within 14 m when not hostile; sight is 90 m through the delegate;
hostile and unarmed flee inside 25 m; hostile and armed chase on sight or
inside 12 m, attack inside the engage range (the weapon's range times 0.4,
at most 55 m), keep chasing 8 s after losing sight, then return to patrol
or idle; wander within the wander radius after a 2 to 7 s idle wait; patrol
waits 1.5 s at each point; attack backs off inside 6 m; a shot needs a
ready weapon and an expired react timer, reloads an empty magazine, uses a
faction base spread (2.2 police, 1.6 residents, 2.6 others) plus 0.03 per
metre, plus 2.5 against a running target and 1.5 against a mounted one,
and re-arms the react timer at 0.4 to 1.4 s; stuck for 1.2 s means idle
when wandering or patrolling and a 4 m sidestep otherwise. Speeds are 1.6
walking and 5.2 running.

The body after the move: `Init(Main, GameData, NpcBrain, DialogueSystem)`,
`Brain`, `Data`, `Weapon`, the forwarding `Faction`, `Health`, `IsDead`,
`Provoked`, `IsHostileTo(Player)` (the parameter stays for its two callers;
it was never read), `TakeDamage(int, Node?)` translating the attacker node
into a player view and a position, `Alert`, `MarkDeadSilently`, `Prompt`,
`CanInteract`, `Interact`, `_PhysicsProcess`, `CanSee`, `Shoot(float
spread)`. `INpcView.Id`, `Name`, `Group`, `Position` stay explicit.

## Testing

The test gate is on. Steps 2 and 4 add logic and ship their tests red
first; steps 1, 3, 5 and 6 are wiring and docs and ride on the existing
suite and the smoke run. Every brain test builds a `Simulation` over
`FakeWorld`, `FakeNotifier`, `FakeDialogueView` and `FakeRandom`, then
`CreateBrain` over a `FakeNpc` standing in as the body, so effects are read
through the simulation: the wanted level, standing, the purse, the toasts,
a mission's completed list.

- `NpcBrainTests` (`tests/Veld.Tests/Sim/Characters/`), twelve facts: outlaws are hostile and civilians are not; `hostile: true` makes anyone hostile; police turn hostile at wanted level 1; residents turn hostile when standing falls to -3; a provoked armed NPC is hostile and a provoked unarmed one is not; a hit by the player provokes the NPC and its whole group and costs a civilian one standing (two when killed); a hit sends an unarmed NPC fleeing and leaves an armed one in place; a killing blow returns true, sets dead and health 0, and completes a `kill_group` mission when it was the group's last member; the dead are never hostile and take no further damage; an alert sends an unarmed resident fleeing and leaves an armed officer and an unarmed outlaw where they are; loot pays 18 on an outlaw at the low roll with no standing lost, pays 1 on a civilian with one standing lost and drops the coins at the body, and a second search pays 0; `MarkDeadSilently` leaves the NPC dead and looted with no toast and no standing change.
- `NpcBrainThinkTests`, thirteen facts: settling with a patrol route starts in Patrol facing the first point at walking speed; waiting 1.5 s at a point moves on to the next; an idle NPC wanders to the corner of its radius at the high roll and goes idle again on arrival; beyond 220 m a passive NPC rests; a hostile armed NPC in sight beyond engage range chases at running speed; inside engage range it attacks, facing the player, and fires when the weapon is ready; with an empty magazine it asks to reload and does not fire; after a shot it holds fire until the react timer (0.4 s at the low roll) has passed; spread at 10 m is 2.5 for police, 5.0 against a running target and 4.0 against a mounted one; a chaser that loses sight for 9 s returns to patrol, one that loses it for 2 s keeps chasing; flee ends after 8 s with a 2 s idle wait; an unarmed hostile flees when the player comes within 25 m; stuck for 1.2 s means idle when wandering and a 4 m sidestep when chasing; the name shows within 14 m only when not hostile.

**Test seams**

- Public interface under test: `NpcBrain` in `Veld.Sim.Characters`, reached through `Simulation.CreateBrain`; the seam is `Settle`, `TakeDamage`, `Alert`, `Loot` and `Think(senses, dt)`.
- Behaviour the tests prove: a character decides hostility, flight, fight, death, loot and every state change from what its body tells it, and nothing else in the game decides those things.
- External boundary, local substitute, or mock: the world, notifier, dialogue view and dice are the ports; the existing fakes plus `FakeRandom` stand in. The simulation's own systems are real.
- Red command and expected failing signal: `dotnet test tests/Veld.Tests`; CS0246 on `NpcBrain` in step 2 and on `NpcSenses` in step 4.
- Full Verify command after green: none declared; `dotnet build`, `dotnet test tests/Veld.Tests`, the Sim fence from `AGENTS.md`, then the smoke run.

The smoke run cannot prove a chase: it attacks whichever officer it finds
first, who may be resting 220 m away. The state machine is proven by the
think tests; the smoke proves the body still spawns, talks, turns hostile
when attacked and saves.

Manual path: play; residents wander and the officers patrol. Shoot near a
resident: they run. Shoot an officer: the wanted level rises, they chase,
stop at range and fire; stand still and their shots land more often than
when you sprint. Kill an outlaw and search the body for R18 or more; search
a resident's body and lose standing.

## Notes for the AI

- Move the rules verbatim; the constants in the Data / contracts section are the ones to copy. Do not tidy the state machine while moving it; `/audit` can do that on tested code.
- The brain calls `wanted.OnGunfire(self.Position, self)` when it emits `Fire`, where the body called it after the shot; same frame, same effect.
- `TakeDamage` from another NPC (the attacker is not the player) only costs health, as today.
- The body's `TakeDamage(int, Node?)` keeps its signature: the player and other bodies call it with nodes. `IsHostileTo(Player)` keeps its parameter for `DevSmoke` and `Player.Consider`.
- `Settle` is where `_Ready` set `_home`, chose Patrol or Idle and rolled the first wait; `_pendingDead` ordering (a save marking the NPC dead before `_Ready`) stays in the body.
- `FakeRandom.Fraction` picks where in a range the roll lands (0 is the low bound, 1 the high); a test that cares sets it.
- `Simulation.CreateBrain` needs the weapon data; look it up in `GameData.Weapons` by `NpcData.Weapon` and pass `WeaponData?`. `Armed` is whether it found one.
- `Right` in the senses is the body's basis X, used only for the sidestep.
- No `Node`, `GD`, `RandomNumberGenerator` or raycast in the library; the fence after every touch of `sim/`.
- Godot does not rebuild the C# project from the command line; build before every run.
- British spelling in player-facing text, no em dashes.
- No AI attribution in commit messages; ask before committing.
