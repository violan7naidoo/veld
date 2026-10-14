---
name: architecture
description: Read-only architecture health scan. Explicit invocation only.
disable-model-invocation: true
---

# architecture - find deepening opportunities, propose a feature, never refactor inline

Where this sits in the workflow:

    any time  ->  [architecture]  ->  candidate report  ->  /feature or /fix
                  (read-only scan)     (ranked, visual)      (the change, reviewed
                                                               through the normal loop)

Blueprint's `/audit` finds maintainability risk inside the feature that just got
built. This skill steps back from any one feature and asks a broader question:
is the codebase itself still healthy? It scans for **deepening opportunities** -
places where a shallow module (interface nearly as complex as its
implementation) could become a deep one (a small interface hiding real
behavior) - and ends by proposing a normal Blueprint feature or fix. It never
edits code itself. Explicit invocation only, because a scan like this can
broaden scope and consumes real review attention.

Adapted from `codebase-design` and `improve-codebase-architecture` in Matt
Pocock's Skills For Real Engineers (MIT License) - see
`blueprint/context/NOTICE.md`.

## Vocabulary

Use the terms in `blueprint/context/architecture-reference.md` exactly -
module, interface, depth, seam, adapter, leverage, locality. Don't drift into
"component," "service," "API," or "boundary." Read that file before scanning if
you haven't already; it also defines the deletion test and the one-adapter/
two-adapters rule this scan applies.

## Step 1 - scope, then explore

**Scope before you scan.** Deepening a module pays off by making future
changes to it easier, so weight the parts of the codebase that actually keep
changing:

- If the user named a direction (a module, a subsystem, a pain point), take it.
- Otherwise, walk back through recent history (`git log --oneline`) to find
  hot spots - the files and areas that keep coming up - and let those pull
  your attention first. If changes are scattered with no clear hot spot,
  widen the net.

Read `blueprint/context/domain-language.md` (if it exists) and any ADRs under
`blueprint/decisions/` in the area you're touching first - this scan should
propose new deepening, not re-litigate a recorded trade-off.

Then explore organically. Don't follow rigid heuristics; note where you
experience friction:

- Where does understanding one concept require bouncing between many small
  modules?
- Where are modules **shallow** - interface nearly as complex as the
  implementation?
- Where have pure functions been extracted just for testability, but the real
  bugs hide in how they're called (no **locality**)?
- Where do tightly-coupled modules leak across their seams?
- Which parts of the codebase are untested, or hard to test through their
  current interface?

Apply the **deletion test** to anything you suspect is shallow: would deleting
it concentrate complexity elsewhere, or just move it? A "yes, concentrates" is
the signal you want.

## Step 2 - present candidates as a report

Write a self-contained HTML report to the OS temp directory so nothing lands in
the repo (resolve via `$TMPDIR`, falling back to `/tmp` or `%TEMP%`; write to
`<tmpdir>/architecture-review-<timestamp>.html`). Open it for the user and tell
them the absolute path.

For each candidate, include: **Files** involved; **Problem** (why the current
shape causes friction, in domain-language.md terms where a term exists);
**Solution** (plain-English description of what would change, in the deep-
module vocabulary); **Benefits** (locality and leverage gained, how tests would
improve); a simple **before/after** illustration; and a **Recommendation
strength** badge (`Strong`, `Worth exploring`, or `Speculative`). End with a
**Top recommendation** - which candidate to tackle first and why.

**ADR conflicts:** if a candidate contradicts an existing ADR under
`blueprint/decisions/`, only surface it when the friction is real enough to
warrant revisiting that decision, and mark the card clearly (for example, "this
would revisit `0007-slug.md` - worth reopening because ..."). Don't list every
theoretical refactor an ADR already forbids.

Do not propose interface details yet. After the report is written, ask: "Which
of these would you like to explore?"

## Step 3 - explore the chosen candidate, then stop at a proposal

Once the user picks a candidate, use frontier questioning (see the Frontier
questioning section of `blueprint/context/ai-interaction.md`) to work through
its constraints, dependencies (see the dependency categories in
`blueprint/context/architecture-reference.md`), the shape of the deepened
module, and what tests would survive - only as deep as real ambiguity requires.

Side effects happen inline as decisions crystallize:

- Naming a deepened module after a concept not yet in
  `blueprint/context/domain-language.md`? Add the term there.
- The user rejects the candidate for a load-bearing reason a future scan
  shouldn't re-suggest? Offer an ADR under `blueprint/decisions/`, using the
  criteria in that folder's `README.md` - only when the reason would actually
  be needed by a future explorer, not for ephemeral ("not worth it right now")
  or self-evident reasons.

End by proposing the change as a normal Blueprint work item - a `build-plan.md`
line for `/feature` to spec, or a `/fix` target if it's small enough to be a
direct repair. **Never refactor inline during this skill.** The proposal is the
deliverable; `/feature` or `/fix` plus `/implement` do the actual work, one
small reviewed step at a time, same as any other change.

## Rules

- Read-only except for the temp-directory report file, `domain-language.md`
  term additions, and `blueprint/decisions/` ADRs - all side effects of Step 3,
  never source edits.
- Never refactor during this skill. It proposes; `/feature` or `/fix` deliver.
- Use the vocabulary in `blueprint/context/architecture-reference.md` exactly,
  and the domain's own terms from `domain-language.md` for what the module is
  *about*.
- Don't surface a candidate that only contradicts an ADR unless the friction
  genuinely justifies reopening it.
- Explicit invocation only - do not suggest running this as part of the normal
  build loop; suggest it as an occasional health check.

## Formatting

Format the output to match the project's conventions in
`blueprint/context/ai-interaction.md`: concise, scannable markdown, with lists
for enumerations and tables for matrices rather than dense paragraphs.
