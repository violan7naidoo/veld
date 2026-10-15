# Domain Language

> **Maintained file.** The project's glossary: canonical terms for this
> project's own domain, updated inline the moment a term is coined,
> sharpened, or found to conflict with how the code or the user actually uses
> it. `feature`, `fix`, `diagnose`, and `architecture` consult and update this
> file when a task touches project terminology - see the Domain language
> pointer in `AGENTS.md`. Keep it free of implementation detail: it is a
> glossary, not a spec, and `/overview` never writes to it.

## Simulation

**Simulation**:
The engine-free model of the game: the purse, standing, sim clock, missions, wanted state and dialogue, owned by one object in the `Veld.Sim` library and advanced by a tick. Godot nodes render it and forward input to it.
_Avoid_: game state, the model, the services

**Tick**:
One advance of the simulation by a fixed slice of time, driven once per physics frame. The simulation never reads wall-clock time.
_Avoid_: update, frame, step (a mission step is something else)

**Sim clock**:
Game time as hour of day and day number. A game day lasts the map's `dayLengthSeconds` of ticks; the sky, lamps and the HUD clock render it, and sleeping advances it.
_Avoid_: day-night (that is the renderer), world time, time of day

## Characters

**Brain**:
The decisions of one character inside the simulation: hostility, its state (idle, wander, patrol, chase, attack, flee, dead), targets, when to shoot and what a search of the body yields. It is created by the simulation and never touches the engine.
_Avoid_: AI, controller, behaviour tree

**Body**:
The character's node in the scene: physics, animation, the raycast for a shot and the effects. It reports its senses to the brain every physics frame and carries out the brain's intent.
_Avoid_: actor, entity, the NPC (ambiguous between body and brain)

**Senses**:
What a body reports to its brain in one tick: where it and the player are, whether the player is in sight, how fast it is actually moving, and whether its weapon can fire.
_Avoid_: perception, observation, input

**Intent**:
What a brain asks its body to do in one tick: which way to face, how fast to move, whether to fire or reload, whether to rest or show its name.
_Avoid_: command, action, output

## Rules

- **Be opinionated.** When multiple words exist for the same concept, pick the
  best one and list the others under `_Avoid_`.
- **Keep definitions tight.** One or two sentences max. Define what it IS, not
  what it does.
- **Only include terms specific to this project.** General programming
  concepts (timeouts, error types, utility patterns) don't belong even if the
  project uses them extensively. Before adding a term, ask: is this unique to
  this project's domain, or a general programming concept? Only the former
  belongs.
- **Group terms under subheadings** when natural clusters emerge. A flat list
  is fine if everything belongs to one cohesive area.
- **Update inline, not in batches.** The moment a term is resolved or a
  conflict is caught, write it here - don't queue it up for later.

## Format

    **{Term}**:
    {A one or two sentence description of the term.}
    _Avoid_: {other words people might reach for instead}

## When a term conflicts

If the user uses a term that conflicts with what's recorded here, say so
before proceeding: "This glossary defines '{term}' as X, but you seem to mean
Y here - which is it?" Resolve it, then update the entry in the same pass.
