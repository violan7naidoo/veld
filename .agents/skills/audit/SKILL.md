---
name: audit
description: Code quality audit for a Blueprint project, read-only except the findings ledger it maintains at blueprint/context/findings.md. Reviews the diff through two separate lenses - Spec (does the code faithfully implement what current-feature.md asked for) and Standards (duplication, dead code, DRY violations, inconsistent patterns, overgrown files, unused exports, missing tests, deep-module design, and obvious security or performance risks) - and reports them without merging the two rankings. Use when the user runs /audit, invokes $audit, asks to audit the code, review code quality, check for dead code, check for duplicates, clean up a vibe-coded project, or make sure the code still meets project standards.
---

# audit - review code quality against the project standards

Where this sits in the workflow:

    /implement or /autopilot  ->  [audit]  ->  fixes or /complete
    (code exists)                 (two lenses,  (repair quality issues
                                   one ledger)   or close the feature)

`/check` proves behavior against the spec at runtime. `/doctor` checks
Blueprint setup and workflow health. This skill checks the code itself, through
two separate lenses that are never merged into one ranking:

- **Spec** - does the diff faithfully implement what `current-feature.md`
  asked for? Missing requirements, scope creep, and requirements that look
  implemented but aren't, quite.
- **Standards** - maintainability, duplication, dead code, consistency, test
  coverage for logic, deep-module design (see
  `blueprint/context/architecture-reference.md`), and drift from
  `coding-standards.md`.

A change can pass one lens and fail the other - code that does exactly what
the spec asked but breaks project conventions, or code that follows every
convention but implements the wrong thing. Reporting them separately stops one
lens from masking the other.

It reviews code without changing it: it never edits source files, installs
dependencies, commits, merges, pushes, or starts product work. Its one write is
the findings ledger at `blueprint/context/findings.md` (Step 4), the durable
record of findings and their status - unchanged by the two-lens split; every
finding still gets one durable ID, one severity, and one status.

## Input

Optional scope:

- no argument: use `current` when an active feature exists, otherwise use
  `changed` when local changes exist, otherwise use `full`
- `current`: audit the active `current-feature.md`, every committed feature-branch
  change from its merge base through `HEAD`, staged and unstaged changes,
  untracked source files, and nearby code affected by the feature
- `changed`: audit staged, unstaged, and untracked source files plus nearby code
- `full`: audit all project-owned source, tests, and configuration while excluding
  dependencies, generated files, build output, coverage output, caches, vendored
  code, and minified assets unless the user explicitly includes them
- path or directory: audit that area and the tests or callers needed to understand it

If the requested scope is unclear, pick the smallest useful scope and state it.

## Step 1 - gather context

Read:

- `AGENTS.md`
- `blueprint/context/project-overview.md`
- `blueprint/context/coding-standards.md`
- `blueprint/context/current-feature.md`
- `blueprint/context/findings.md`, for existing IDs and statuses
- `blueprint/context/ai-interaction.md`
- `blueprint/build-plan.md`, when feature order matters
- git branch and working tree status
- relevant source files, tests, and configs for the chosen scope

For `current`, resolve the comparison base without network access:

1. Use a base branch declared by the active spec or project instructions.
2. Otherwise use the locally recorded remote default branch when available.
3. Otherwise use an existing local `main`, then `master`.
4. Find the merge base and inspect the committed delta through `HEAD`, then add
   staged, unstaged, and untracked work.
5. If no reliable base exists, say so and use the active spec plus local changes.
   Never claim that committed feature work was fully covered in that case.

Do not fetch or pull to discover the base. For `full`, state the excluded paths
before reviewing so generated or third-party code does not consume the audit.

Prefer `rg` and targeted file reads. Do not dump large files into the response.

## Step 2 - run available signals

Use existing commands only. Do not install tools.

Run or inspect as appropriate:

- lint command, when declared in `AGENTS.md`
- typecheck command, when declared
- test command, when declared and relevant
- build command, when the audit needs to know if the current code compiles
- existing security or performance commands when declared and locally runnable
- lightweight searches for unused exports, duplicate names, TODO/FIXME, ignored
  errors, unsafe suppressions, empty catch blocks, skipped or focused tests,
  copied logic, and oversized files

If a useful command is missing, report that as a gap. Do not invent a pass.

## Step 3 - review the code through both lenses

Review the same fixed change set twice, through two independent lenses. Keep
them separate in your notes as you go - do not let a Standards observation
bleed into the Spec pass or vice versa.

### Spec lens

Compare the diff against `blueprint/context/current-feature.md` (its Goal,
In scope, Out of scope, and Build steps). Look for:

- requirements the spec asked for that are missing or partially built
- behavior in the diff that wasn't asked for (scope creep beyond the spec's
  In scope section)
- requirements that look implemented but where the implementation looks wrong
  against the spec's stated done-when

If there is no active spec (a `full` or `changed` scope with nothing in
progress), skip the Spec lens and say so in the report - Standards still runs.

### Standards lens

Look for issues that affect long-term maintainability:

- duplicated logic, duplicated components, duplicated styles, or repeated data
  shaping that should share one helper
- dead code, unused files, unused exports, stale comments, unreachable branches,
  and abandoned feature paths
- functions, components, routes, or modules that are too large to review safely
- shallow modules - an interface nearly as complex as its implementation; see
  the deletion test in `blueprint/context/architecture-reference.md`
- clever abstractions that do not pay for themselves
- missing abstractions where duplication is already causing risk
- inconsistent project patterns, naming, validation, error handling, or data access
- logic-bearing code without tests when the project has a declared test command,
  or a test that mocks its own module's internals rather than a true external
  boundary (see the Test design section of `coding-standards.md`)
- skipped, focused, or placeholder tests; swallowed failures; and weak assertions
  around important logic
- UI or integration code without real browser evidence when behavior matters
- obvious security issues such as missing auth checks, unsanitized input, trusting
  client-supplied ownership fields, or leaking sensitive data
- obvious performance issues such as N+1 queries, unnecessary client rendering,
  unbounded loops, avoidable repeated network calls, or expensive work in render
- drift from `coding-standards.md` or `project-overview.md`

Do not nitpick harmless style differences unless they signal drift from the local
patterns. Prefer a short list of real findings over a broad list of guesses.

If a possible secret is found, never quote its value, paste the matching source
line, or include raw command output containing it. Report only the redacted secret
category, file, line, risk, and remediation. Redact sensitive values from all
audit evidence before responding.

## Step 4 - update the findings ledger

`blueprint/context/findings.md` is the durable record of findings. Chat reports
do not survive a context clear; the ledger does. It is the only file this skill
writes. If it is missing (an older install), create it with a `# Findings`
heading first.

**The ledger never scopes the review.** Review the code fresh in Step 3, then
record what the review found. Working from the open findings as a checklist and
verifying only those is the exact failure this file exists to prevent: a repair
can introduce a new defect that no existing entry points at.

One block per finding. The header line is the machine-readable contract and must
keep this exact shape; the prose below it is for humans and may vary:

    ### F-03 [P0] open - Retained auth volumes carry the run label

    **File:** ops/agent-proof/compose.yaml:86
    **Found:** 2026-07-21 by /audit (scope: current)
    **Lens:** Standards
    **Why it matters:** ...
    **Suggested fix:** ...
    **Resolution:**

**Lens** is `Spec` or `Standards`, naming which pass in Step 3 raised the
finding. It's a body field, not part of the header contract above - a finding
recorded before this edition existed simply has no Lens line, and that's fine.

IDs are sequential within the ledger (`F-01`, `F-02`, ...), never reused and
never renumbered while their entries live here, even after a finding closes.
Bare IDs are scoped to the live ledger: `/complete` archives resolved entries
under a work-item prefix (feature 12's `F-03` becomes `12/F-03`), and that
prefixed form is the permanent reference. A later ledger that has emptied and
reset starts at `F-01` again without colliding. Severity reuses the P0-P3
scheme from Step 5; only P0 and P1 block `/complete`. Status is one of:

| Status | Meaning | Blocks P0/P1 at /complete |
|---|---|---|
| `unverified` | Suspected, no confirming evidence yet | No |
| `open` | Confirmed, not yet repaired | Yes |
| `fixed` | Repaired, not yet re-reviewed | Yes |
| `closed` | Repaired and re-reviewed against the new code | No |
| `accepted` | Not fixing, by the user's explicit decision; reason recorded in Resolution | No |
| `invalid` | Re-examination proved the finding wrong; evidence recorded in Resolution | No |

After the review:

- Append each new confirmed finding as `open` with the next sequential ID, one
  past the highest ID present in the ledger (entries carried forward from
  earlier work count; a fresh ledger starts at `F-01`).
- Record an unverified risk worth tracking as `unverified`. It is a lead, not a
  defect, and never gates a merge.
- Update the entries this pass re-examined: correct the status or severity and
  note the evidence in **Resolution**.
- Move a `fixed` finding to `closed` only when all three hold: this pass's
  reviewed set included the finding's file, re-examining the repaired code
  confirmed the original defect is gone and the repair introduced no new one,
  and the report names the finding as closed. An unrelated new finding in the
  same file gets its own entry and does not keep the repaired one open. Never
  close a finding implicitly.
- Set `accepted` only on the user's explicit decision in the current session,
  and record their reason. Never accept a finding on their behalf.
- Set `invalid` only when re-examination shows the finding was wrong, and
  record that evidence in **Resolution**. It is a review verdict (or the
  user's explicit call), never a shortcut past the gate for blocked work.

`fixed` blocking `/complete` is deliberate: a repair is not done when the code
changes, it is done when a review has looked at the result. `/implement` marks
repairs `fixed`; only a review pass moves them to `closed`.

## Step 5 - report findings

Lead with findings, grouped under two headings - `## Spec` and `## Standards` -
each ordered by severity, using the IDs the ledger assigned:

    ## Spec

    F-04 [P1] Title
    File: path:line
    Why it matters: ...
    Suggested fix: ...

    ## Standards

    F-05 [P2] Title
    File: path:line
    Why it matters: ...
    Suggested fix: ...

Do not merge or rerank findings across the two headings - report them
verbatim (or lightly cleaned) under their own lens. End with a one-line
summary per lens: total findings and the worst issue *within that lens*, if
any. Don't pick one overall winner across both lenses; that reranking is
exactly what the split exists to prevent.

Severity:

- `P0` - data loss, security break, or code that cannot ship
- `P1` - likely bug, broken contract, missing guard, or high-risk duplication
- `P2` - maintainability issue worth fixing before the feature closes
- `P3` - small cleanup, consistency issue, or follow-up candidate

Use P0 or P1 only when a concrete code path, violated contract or security
boundary, failing command or test, or reproducible behavior confirms the risk. If
the evidence is incomplete, list it under `Unverified risks` with the missing
validation instead of presenting it as a confirmed high-severity finding.

If there are no findings, say that clearly and name any remaining risk or missing
signal, such as "no test command declared" or "browser flow not audited."

Then include:

- ledger changes: findings added, updated, or closed this pass, by ID
- commands run and results
- selected scope
- base branch, merge base, and commit range for `current`, when available
- files or directories reviewed
- generated, third-party, or otherwise excluded paths
- standards checked
- browser or runtime evidence inspected, when relevant
- skipped, focused, or placeholder tests found
- checks that were unavailable or could not run
- suggested repair order

For `full`, say whether coverage was complete or partial. Never label a partial
review as a full-project audit.

## Rules

- The findings ledger is the only file this skill writes. Never edit, format,
  install, commit, merge, push, or delete anything else.
- The ledger reports status; it never defines what the review looks at. Do not
  turn open findings into the review checklist.
- Never fetch, pull, or run network-backed audit tools without explicit approval.
- Never reproduce secrets or sensitive values in findings or command output.
- Findings first. Keep summaries short.
- Ground every finding in a file path and line number when possible.
- Avoid speculative rewrites. Recommend the smallest fix that removes the risk.
- Respect existing project patterns over generic advice.
- Do not require perfection. The goal is code that is understandable, consistent,
  testable where it matters, and safe to keep building on.

## Formatting

Format the output to match the project's conventions in
`blueprint/context/ai-interaction.md`: concise, scannable markdown, with lists for
enumerations and tables for matrices rather than dense paragraphs.
