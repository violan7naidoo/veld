---
name: diagnose
description: Diagnosis loop for hard, intermittent, or performance bugs. Builds a tight, red-capable feedback loop, reproduces and minimises the failure, ranks falsifiable hypotheses, and instruments until the root cause is confirmed, then stops with an evidence-backed diagnosis for /fix. Never applies the product fix itself. Use when the user says "diagnose" or "debug this", reports something broken/throwing/failing/slow with no obvious cause, or when /fix routes here for a bug that isn't small and obvious.
---

# diagnose - find the root cause before /fix writes the spec

Where this sits in the workflow:

    bug report  ->  [diagnose]  ->  /fix  ->  /implement  ->  /complete
    (symptom)       (evidence-backed   (spec written        (build it,     (commit +
                     root cause)        from the diagnosis)  reviewed)      merge + log)

`/fix` writes a repair spec directly for small, obvious bugs. For anything hard,
intermittent, performance-related, or unclear-root-cause, `/fix` routes here
first. This skill is read-mostly: it may create a repro harness or a failing
regression test by approval, but it stops before the product fix. Applying the
fix stays `/fix` + `/implement`'s job, so the fix still goes through Blueprint's
normal diff-by-diff review gate.

Adapted from the `diagnosing-bugs` skill in Matt Pocock's Skills For Real
Engineers (MIT License) - see `blueprint/context/NOTICE.md`.

## Input

A description of the bug or performance regression, or a finding ID from
`blueprint/context/findings.md`. If `/fix` routed here, use the problem
statement it was given.

When exploring the codebase, read `blueprint/context/domain-language.md` (if it
exists) for a clear mental model of the relevant terms, and check
`blueprint/decisions/` for ADRs in the area you're touching - this command
should not re-litigate a recorded trade-off.

## Redact

This skill has you show commands, outputs, and captured artifacts. **Redact
every secret first** - write `<REDACTED>` in its place. Build loops against env
vars, so the credential stays in the environment rather than in what you show.
Captured artifacts carry auth headers: quote only the lines that carry the
signal.

If the redacted output is not enough to diagnose the bug, say so and ask the
user.

## Phase 1 - Build a feedback loop

**This is the skill.** Everything else is mechanical. If you have a **tight**
pass/fail signal for the bug - one that goes red on _this_ bug - you will find
the cause; bisection, hypothesis-testing, and instrumentation all just consume
it. If you don't have one, no amount of staring at code will save you.

Spend disproportionate effort here. Be aggressive. Be creative. Refuse to give
up.

Ways to construct one, tried in roughly this order: a failing test at whatever
seam reaches the bug; a curl/HTTP script against a running dev server; a CLI
invocation diffing output against a known-good snapshot; a headless-browser
script asserting on DOM/console/network; replaying a captured trace; a
throwaway harness exercising the bug path directly; a property/fuzz loop for
"sometimes wrong output"; a bisection harness (`git bisect run`) if the bug
appeared between two known states; a differential loop diffing old vs new
output for the same input. As an absolute last resort, a human-in-the-loop
script when a person must click something.

Once you have *a* loop, tighten it: make it faster (skip unrelated init,
narrow scope), make the signal sharper (assert the specific symptom, not
"didn't crash"), make it more deterministic (pin time, seed RNG, isolate
filesystem, freeze network). A 30-second flaky loop is barely better than no
loop; a 2-second deterministic one is a debugging superpower.

For non-deterministic bugs, the goal is a higher reproduction rate, not a clean
repro: loop the trigger 100x, parallelise, add stress, narrow timing windows.
A 50%-flake bug is debuggable; 1% is not - keep raising the rate.

**When you genuinely cannot build a loop:** stop and say so explicitly. List
what you tried. Ask the user for access to the environment that reproduces it,
a redacted captured artifact (HAR file, log dump, core dump, timestamped
recording), or permission to add temporary production instrumentation. Do not
proceed to hypothesise without a loop.

Phase 1 is done when you can name **one command** - already run at least once,
output shown and redacted - that is red-capable (drives the actual bug path and
asserts the user's exact symptom), deterministic, fast, and agent-runnable. If
you catch yourself reading code to build a theory before this command exists,
stop - jumping straight to a hypothesis is the exact failure this skill
prevents.

## Phase 2 - Reproduce + minimise

Run the loop. Confirm the failure matches the symptom the user described (wrong
bug = wrong fix), and that it reproduces across multiple runs (or at a high
enough rate for non-deterministic bugs). Capture the exact symptom so later
phases can verify the fix actually addresses it.

Shrink the repro to the smallest scenario that still goes red - cut inputs,
callers, config, data, and steps one at a time, re-running the loop after each
cut. Done when every remaining element is load-bearing: removing any one of
them makes the loop go green. This minimal repro becomes the regression test in
Phase 5.

## Phase 3 - Hypothesise

Generate 3-5 ranked hypotheses before testing any of them. Each must be
falsifiable - state the prediction it makes ("if X is the cause, then changing
Y will make the bug disappear"). If you cannot state the prediction, the
hypothesis is a vibe - discard or sharpen it.

Show the ranked list to the user before testing. They often have domain
knowledge that re-ranks instantly, or already-ruled-out hypotheses. Don't block
on it if the user is unavailable - proceed with your own ranking.

## Phase 4 - Instrument

Each probe maps to a specific prediction from Phase 3. Change one variable at a
time. Prefer a debugger/REPL breakpoint over logs when the environment supports
it; otherwise targeted logs at the boundaries that distinguish hypotheses -
never "log everything and grep." Tag every debug log with a unique prefix
(`[DEBUG-a4f2]`) so cleanup is one grep.

For performance regressions, establish a baseline measurement first (timing
harness, profiler, query plan), then bisect against it - logs are usually wrong
for perf.

## Phase 5 - Confirm, then hand off (stop before the fix)

Once a probe confirms a hypothesis, stop. Do not apply the product fix here.

If a correct test seam exists for the bug pattern (the seam exercises the real
bug as it occurs at the call site, not a shallower stand-in), you may - with the
user's approval - turn the minimised repro into a failing regression test at
that seam, and leave it failing. If no correct seam exists, that itself is a
finding: note it, and flag it for `/architecture` afterward rather than forcing
a shallow test.

Report the diagnosis:

- **Root cause** - the confirmed hypothesis, in plain language.
- **Evidence** - the probe(s) that confirmed it, redacted.
- **Minimal repro** - the command from Phase 1/2.
- **Recommended fix** - the approach, and anything it must not break.
- **Regression seam** - where the fix's test belongs, or why none exists yet.
- **What would have prevented this** - only if the answer points at
  architecture (no good seam, tangled callers, hidden coupling); suggest
  `/architecture` with the specifics, made after the diagnosis, not before.

Then tell the user to run `/fix` (it will use this diagnosis to write the spec)
or, if `/fix` already routed here, return this report to that flow so it can
finish writing `blueprint/context/current-feature.md`.

## Rules

- Read-mostly. This skill may create a repro harness or a failing test file by
  approval; it never applies the product fix, and it never commits.
- Redact every secret before showing it, including in captured artifacts.
- No hypothesis without a falsifiable prediction; no instrumentation without a
  hypothesis it's testing; no fix recommendation without a confirming probe.
- If no correct test seam exists, say so - do not write a shallow test for
  false confidence.
- Never skip straight from symptom to fix. That shortcut is what this skill
  exists to prevent.

## Formatting

Format the output to match the project's conventions in
`blueprint/context/ai-interaction.md`: concise, scannable markdown, with lists
for enumerations and tables for matrices rather than dense paragraphs.
