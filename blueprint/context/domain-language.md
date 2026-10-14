# Domain Language

> **Maintained file.** The project's glossary: canonical terms for this
> project's own domain, updated inline the moment a term is coined,
> sharpened, or found to conflict with how the code or the user actually uses
> it. `feature`, `fix`, `diagnose`, and `architecture` consult and update this
> file when a task touches project terminology - see the Domain language
> pointer in `AGENTS.md`. Keep it free of implementation detail: it is a
> glossary, not a spec, and `/overview` never writes to it.

_No terms recorded yet. Add the first one the moment a term is resolved._

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
