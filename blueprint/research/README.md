# Research Notes

> **Lazy folder.** Real notes live beside this file as `<slug>.md` - created
> the moment `/research` (or `$research`) has a finding to record, not
> before. See `.claude/skills/research/SKILL.md` (or the matching file under
> `.agents/skills/`) for how these get written.

## Format

Each note answers one question and has four sections:

- **Question** - exactly what this note answers.
- **Findings** - each claim followed by its source (a URL, a file path and
  line, a spec section). No claim without a citation.
- **Answer** - the direct, checkable answer the spec needs, distilled from the
  findings.
- **Open gaps** - anything the primary sources didn't settle.

## Rules

- One file per question. Don't let a note sprawl into covering several
  unrelated questions.
- Primary sources only - official docs, specs, source code, first-party APIs.
  A secondary write-up of a primary source is not itself a source.
- These notes inform the active spec; they are not a second work queue. Link
  a note from `blueprint/context/current-feature.md` when it directly informs
  the work in progress, or from the relevant line in `blueprint/project-plan.md`
  when it's about the stack itself.
