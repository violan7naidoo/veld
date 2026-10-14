# Architecture Decisions (ADRs)

> **Lazy folder.** Real ADRs live beside this file as `0001-slug.md`,
> `0002-slug.md`, and so on - created the moment the first one is needed, not
> before. `feature`, `fix`, `diagnose`, and `architecture` offer to record one
> inline when a qualifying decision crystallizes - see the Domain language
> pointer in `AGENTS.md`.

## When to offer an ADR

Only when **all three** hold:

1. **Hard to reverse** - the cost of changing your mind later is meaningful.
2. **Surprising without context** - a future reader will look at the code and
   wonder why it's done this way.
3. **The result of a real trade-off** - there were genuine alternatives, and
   one was picked for specific reasons.

If any of the three is missing, skip it. Easy to reverse means you'll just
reverse it; not surprising means nobody will wonder; no real alternative
means there's nothing to record beyond "we did the obvious thing."

## What qualifies

- **Architectural shape.** "The write model is event-sourced, the read model
  is projected."
- **Integration patterns** between parts of the system.
- **Technology choices that carry lock-in** - database, message bus, auth
  provider, deployment target. Not every library, just the ones that would
  take real effort to swap out.
- **Boundary and scope decisions.** The explicit no's are as valuable as the
  yes's.
- **Deliberate deviations from the obvious path**, so nobody "fixes" something
  that was deliberate.
- **Constraints not visible in the code** - a compliance requirement, a
  partner API's latency contract.
- **Rejected alternatives when the rejection is non-obvious**, so the same
  suggestion doesn't resurface every few months.

## Numbering

Scan this folder for the highest existing number and increment by one.

## Template

    # {Short title of the decision}

    {1-3 sentences: what's the context, what did we decide, and why.}

That's it. An ADR can be a single paragraph. The value is in recording *that*
a decision was made and *why* - not in filling out sections.

## Optional sections

Only include these when they add genuine value. Most ADRs won't need them.

- **Status** frontmatter (`proposed | accepted | deprecated | superseded by ADR-NNNN`) - useful when decisions are revisited.
- **Considered Options** - only when the rejected alternatives are worth remembering.
- **Consequences** - only when non-obvious downstream effects need to be called out.
