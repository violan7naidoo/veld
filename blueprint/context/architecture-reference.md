# Architecture Reference - Deep Modules

> **Disclosed reference.** Shared vocabulary for module design, consulted by
> `architecture`, by `feature` when a step's interface shape is itself in
> question, and by `audit`'s Standards lens. This is a reference to read when
> the shape of an interface matters, not a session to run on its own.

## Glossary

Use these terms exactly - don't substitute "component," "service," "API," or
"boundary." Consistent language is the whole point.

- **Module** - anything with an interface and an implementation. Scale-agnostic:
  a function, class, package, or tier-spanning slice.
- **Interface** - everything a caller must know to use the module correctly:
  the type signature, but also invariants, ordering constraints, error modes,
  required configuration, and performance characteristics.
- **Implementation** - what's inside a module, its body of code. Distinct from
  **Adapter**: a thing can be a small adapter with a large implementation (a
  Postgres repo), or a large adapter with a small implementation (an in-memory
  fake).
- **Depth** - leverage at the interface: how much behavior a caller (or test)
  can exercise per unit of interface they have to learn. A module is **deep**
  when a lot of behavior sits behind a small interface, **shallow** when the
  interface is nearly as complex as the implementation.
- **Seam** (Michael Feathers) - a place where you can alter behavior without
  editing in that place; the *location* a module's interface lives at.
- **Adapter** - a concrete thing that satisfies an interface at a seam.
- **Leverage** - what callers get from depth: more capability per unit of
  interface they learn.
- **Locality** - what maintainers get from depth: change, bugs, and knowledge
  concentrate in one place instead of spreading across callers.

## Deep vs. shallow

    Deep module    = small interface + lots of implementation (behavior hidden)
    Shallow module  = large interface + thin implementation (mostly pass-through - avoid)

When designing an interface, ask: can I reduce the number of methods? Can I
simplify the parameters? Can I hide more complexity inside?

## Principles

- **The deletion test.** Imagine deleting the module. If complexity vanishes,
  it was a pass-through. If complexity reappears across N callers, it was
  earning its keep.
- **The interface is the test surface.** Callers and tests cross the same
  seam. Wanting to test *past* the interface is a sign the module is the
  wrong shape.
- **One adapter means a hypothetical seam. Two adapters means a real one.**
  Don't introduce a seam unless something actually varies across it.
- **Depth is a property of the interface, not the implementation.** A deep
  module can be internally composed of small, swappable parts - they just
  aren't part of the interface.

## Dependency categories

Classify what a deepened module depends on; the category decides how it's
tested across its seam:

1. **In-process** - pure computation, no I/O. Always deepenable; merge and
   test through the new interface directly, no adapter needed.
2. **Local-substitutable** - a dependency with a local test stand-in (an
   in-memory database, a local filesystem fake). Deepenable if the stand-in
   exists; the seam stays internal.
3. **Remote but owned** (Ports & Adapters) - your own services across a
   network boundary. Define a port at the seam; production gets a network
   adapter, tests get an in-memory one.
4. **True external** (Mock) - third-party services you don't control. Inject
   as a port; tests provide a mock adapter.

## Comparing alternative interfaces

For a major design choice, don't settle for the first idea. Frame the
constraints and dependency category, sketch the shape, then draft at least
two genuinely different interfaces before picking - for example one
minimizing entry points, one maximizing flexibility, one optimizing for the
most common caller. Compare by depth, locality, and seam placement, then
recommend one (or a hybrid) with a clear reason.

## Rejected framings

- **Depth as a ratio of implementation-lines to interface-lines** - rewards
  padding the implementation. Use depth-as-leverage instead.
- **"Interface" as only a language keyword or a class's public methods** -
  too narrow; interface here includes every fact a caller must know.
- **"Boundary"** - overloaded with domain-driven design's bounded context.
  Say **seam** or **interface**.
