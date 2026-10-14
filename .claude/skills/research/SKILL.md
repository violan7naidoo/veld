---
name: research
description: Investigate a question against primary sources (official docs, specs, source code, first-party APIs) and write one cited Markdown note under blueprint/research/, linked from the active spec. Use when a feature depends on unstable or unfamiliar external facts, official API behavior, or a library/tool choice that needs verifying before it's locked into project-plan.md or a feature spec.
---

# research - primary-source evidence for a spec

Where this sits in the workflow:

    project-plan.md or a feature spec  ->  [research]  ->  blueprint/research/<slug>.md  ->  informs the spec
    (an open question about              (cited findings,                                    (never a second
     an external fact)                    one file)                                            work queue)

Blueprint can read docs while it works, but has no standing place for durable,
cited research. This skill fills that gap: it investigates one question against
primary sources and leaves a permanent, citeable note behind - so the next
session (or the next person reading the spec) doesn't have to re-derive the
same facts from a stale memory of a doc page.

Adapted from the `research` skill in Matt Pocock's Skills For Real Engineers
(MIT License) - see `blueprint/context/NOTICE.md`.

## Input

A question that needs an answer from outside the codebase - a library's real
behavior, an API's actual contract, a spec's exact wording, a tool's
documented limits. If the question comes from the active spec's Open
questions or Notes for the AI, use that framing directly.

## Step 1 - investigate against primary sources

Where the current tool supports it, delegate the investigation to a background
agent or subagent so the research runs without blocking other work; otherwise
perform it directly in this session.

Follow every claim back to the source that owns it: official documentation,
the specification, the actual source code, or a first-party API response - not
a blog post, a forum answer, or a secondary write-up of the primary source.
Where the codebase already answers the question cheaply (a config file, a
`--help` output, a one-file lookup), that counts as the primary source too -
don't go further afield than the environment already provides.

## Step 2 - write the cited note

Create `blueprint/research/` if it doesn't exist yet (see
`blueprint/research/README.md` for the format). Write findings to
`blueprint/research/<slug>.md`, one file per question, with:

- **Question** - exactly what this note answers.
- **Findings** - each claim followed by its source (a URL, a file path and
  line, a spec section). No claim without a citation.
- **Answer** - the direct, checkable answer the spec needs, distilled from the
  findings above.
- **Open gaps** - anything the primary sources didn't settle; say so rather
  than guessing past it.

## Step 3 - link it from the spec

If a feature or fix is active, add a line to `blueprint/context/current-feature.md`
(its Notes for the AI section is a good home) pointing at the new note. If the
question was about the stack itself, point to it from the relevant line in
`blueprint/project-plan.md` instead.

The note informs the spec. It does not become a second work queue, a second
source of truth, or a substitute for the plans - `/overview` still only reads
`project-plan.md` and `build-plan.md`.

## Rules

- Every claim is cited. No claim, no line.
- Primary sources only. A secondary write-up of a primary source is not itself
  a source.
- One file per question. Don't let a research note sprawl into covering
  several unrelated questions.
- Don't cache what's cheap to look up. If the codebase already answers it in
  one file or one command, point at that instead of restating it.
- If the primary sources don't settle the question, say that plainly under
  Open gaps rather than presenting a guess as a finding.

## Formatting

Format the output to match the project's conventions in
`blueprint/context/ai-interaction.md`: concise, scannable markdown, with lists
for enumerations and tables for matrices rather than dense paragraphs.
