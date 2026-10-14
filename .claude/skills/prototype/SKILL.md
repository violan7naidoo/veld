---
name: prototype
description: Build a throwaway prototype that answers a design question. Three modes - Theme and screens (lock the shared visual identity and key routes before the build loop), UI variants (compare several structurally different layouts for one screen), and Logic and state (a self-contained demo that pushes a state model through real cases) - each plan-first and writing only to prototypes/. Use when the user runs /prototype, names screens to mock up, asks to prototype the layout/theme/look and feel, wants to compare UI options, or wants to sanity-check whether a state model or piece of logic feels right before building it for real.
---

# prototype - answer a design question with throwaway code

Where this sits in the workflow:

    plan or feature  ->  [prototype]  ->  captured decision  ->  build
    (a question           (throwaway,      (folded into the      (real
     needs an answer)      plan-first)       spec or theme)        code)

A prototype is throwaway code that answers a question. **The question decides
the mode** - pick one before writing anything:

- **"What should the whole app look like?"** - pre-build, before `/feature`
  starts. See [Mode 1 - Theme and screens](#mode-1---theme-and-screens). This
  is the original, most common use of this skill and stays the default when
  the user just runs `/prototype` with no more context.
- **"What should this one screen look like?"** - mid-project, when a specific
  page's layout is genuinely undecided. See
  [Mode 2 - UI variants](#mode-2---ui-variants).
- **"Does this logic or state model feel right?"** - mid-project, before or
  during `/feature`, when a state machine or data shape looks reasonable on
  paper but needs pushing through real cases. See
  [Mode 3 - Logic and state](#mode-3---logic-and-state).

If the question is genuinely ambiguous and the user isn't reachable, default
to whichever mode better matches the surrounding request (a whole new project
or an unsettled visual direction -> Mode 1; a specific existing page -> Mode 2;
a backend module, data shape, or workflow -> Mode 3) and state the assumption
at the top of the prototype.

**Every mode is plan-first.** State the question and the plan, get approval,
and only then write files. Never generate a prototype before the user
approves the plan.

## Rules that apply to every mode

1. **Throwaway from day one, clearly marked.** Everything lives under
   `prototypes/`, plain HTML/CSS (a little inline JS for switching views is
   fine), no framework, no build step, no server. Anyone opens it by
   double-clicking.
2. **No persistence by default.** State lives in memory. Persistence is what a
   prototype checks, never what it depends on.
3. **Skip the polish.** No tests, no error handling beyond what makes it
   runnable, no abstractions "for later." The point is to learn something
   fast.
4. **Commit `prototypes/`, do not ignore it.** It is short-lived in git (born
   here, discarded once its answer is captured and `/complete` runs on the
   feature that consumed it), not throwaway that never lands. Don't add it to
   `.gitignore`.
5. **Capture the decision, then let it go.** Once a prototype has answered its
   question, fold the validated answer into the real spec, theme, or code -
   whichever mode below describes - and note in the relevant feature spec
   which prototype file settled it. `/complete` removes the consumed
   `prototypes/` files once the feature that used them lands (see
   `complete/SKILL.md`).

## Mode 1 - Theme and screens

Lock the shared visual identity and a handful of key routes before the build
loop starts. This is a pre-build step, not a feature - fast, visual, and
throwaway. Its one durable output is the theme: a set of CSS theme variables
that port into the real app's `globals.css` `@theme` when you build the first
UI feature. Everything else here gets discarded.

### Step 1 - read what the plan already says

Pull the stated look and feel and the screen/route list from
`blueprint/context/project-overview.md` (its UI/UX section); fall back to the UI/UX section
of `blueprint/project-plan.md` if the overview isn't generated yet. Use this
as the starting point, so you're refining the user's intent, not asking from
scratch.

### Step 2 - ask about the look and the pages

Work in plan mode. Ask the user a short set of questions (use the current tool's
short user-input prompt for discrete choices when available), seeded with what
the plan already says:

- **Look and feel** - confirm or adjust the vibe (light/dark, minimal/rich,
  density, editor-like, and so on), and ask for any reference apps or sites they
  want it to feel like.
- **Color and type** - any accent color or font direction (for example,
  mono-forward for code).
- **Which pages** - which screens to draft now. Default to the key routes from the
  plan; let the user add, drop, or reorder. Lean to a few, not every screen.

Keep it short. The user's answers, plus the plan, are the brief for the mockups.

### Step 3 - propose the plan, then wait

Present a short plan and stop for approval:

- the theme direction in a sentence or two (the vibe, accent, fonts), and
- the list of screens you'll mock, one line each on what each will show (the real
  states that exercise the theme, not empty shells).

Write nothing until the user approves. Adjust the plan if they push back.

### Step 4 - lock one theme

Once approved, write a single shared `prototypes/theme.css` that defines the theme
as CSS variables, following `reference/theme-variables.css`: surfaces, text,
accent, any component-specific states, font stacks, and a small scale. Derive the
values from the agreed brief.

This file is the deliverable. Keep it the single source of the theme, so tweaking
it restyles every mockup at once.

### Step 5 - mock each screen

For each approved screen, write a self-contained `prototypes/<screen>.html` that
links `theme.css` and lays out that screen with realistic dummy content and the
states that matter (a typing page mid-type with correct/wrong/pending chars and a
caret; a dashboard with believable stats and history rows; and so on).

- Plain HTML + CSS only. No framework, no build step. A few lines of inline JS for
  a view toggle is fine; nothing more.
- Pull every color, font, and spacing value from the `theme.css` variables, never
  hard-coded. That's what keeps the look consistent and portable.
- Realistic placeholder content over lorem ipsum. Desktop-first is enough.

### Then stop

Tell the user to open the files in a browser and iterate on the look. Point them
at the concrete next step: run `/feature` on the first UI feature - it detects
`prototypes/`, links these mockups as the spec's Design reference, and makes
porting `theme.css` into the app's `@theme` its first build step. When the theme
feels right the tokens carry into the real stylesheet; the HTML mockups are
reference and get discarded at that feature's `/complete`. This mode locks the
look, it does not build the app.

## Mode 2 - UI variants

Compare several **structurally different** layouts for one screen or piece of
UI, so a day spent picking between vague mockups in your head becomes ten
minutes of flipping between real ones.

### Step 1 - state the question and pick N

Write down, in one line, which screen or component is in question and how many
variants you'll draft. Default to **3**; more than 5 stops being radically
different and starts being noise.

> "Three layout variants for the dashboard summary card, switchable in one
> file."

### Step 2 - propose the plan, then wait

Present the question, the screen in scope, and a one-line description of each
planned variant's core structural idea (not colors - structure: where the
primary action sits, what's the information hierarchy, list vs. cards vs.
timeline, and so on). Stop for approval. Write nothing until the user
confirms.

### Step 3 - build one switchable file

Write a single self-contained `prototypes/<screen>-variants.html`, linking
`theme.css` for shared tokens so every variant reads as the same app. Inside:

- One markup block per variant (`VariantA`, `VariantB`, `VariantC`), each
  built with real, believable content - the page's actual purpose and data
  shape, not lorem ipsum.
- Variants must be **structurally different**: different layout, different
  information hierarchy, different primary affordance - not just different
  colors or copy. Three tweaked card grids is wallpaper, not a prototype. If
  two drafts come out too similar, redo one with an explicit constraint (for
  example "no card grid").
- A small floating switcher bar (fixed position, bottom-center): a left
  arrow, the current variant's letter and one-line name (`B - Sidebar
  layout`), a right arrow. Plain inline JS toggles which block is visible;
  arrow keys cycle too (but not while an input/textarea is focused).

### Step 4 - hand it over

Tell the user to open the file and flip through the switcher. The useful
feedback is usually "I want the header from B with the sidebar from C" - note
that as the actual decision, not just a single winning letter.

### Then stop

Once a direction is chosen (a single variant or a hybrid), tell the user the
concrete next step: run `/feature` (or continue the active one) and describe
the chosen structure in the spec's Design reference, linking this file. The
winner gets rebuilt to production standards inside the real feature - the
prototype markup itself never ships. This file is discarded at that feature's
`/complete`.

## Mode 3 - Logic and state

A single, self-contained HTML file that lets anyone drive a state model by
clicking buttons - for when something looks reasonable on paper but only
feels wrong once you push it through real cases. Because it's one file with
nothing to install, it can be handed to a non-developer (a designer, a PM, a
domain expert) who can feel the model for themselves.

Use this mode for: "I'm not sure this state machine handles the edge case
where X then Y," "does this data model actually let me represent case Z,"
or anything where the real question is "press buttons and watch the state
change." If the question is actually about visual layout, that's Mode 2, not
this one.

### Step 1 - state the question

Before writing code, write down what state model and what question this
prototype answers - one paragraph, visible at the top of the demo itself, not
just a comment. A logic prototype that answers the wrong question is pure
waste.

### Step 2 - propose the plan, then wait

Present the question and the state model's shape in one or two sentences
(a reducer, a state machine, a small set of pure functions - whichever fits
the question). Stop for approval before writing the file.

### Step 3 - isolate the logic, then build the page around it

Once approved, write `prototypes/<name>-logic.html`. Put the actual logic - a
pure reducer, an explicit state machine, or a small set of pure functions - in
one `<script>` block with no DOM access, no `document`, no button handlers
reaching inside it. This is the part that could later be lifted into the real
module; keep it liftable by keeping it pure. The page around it is throwaway
and calls into it; nothing flows the other direction.

Lay the page out top to bottom:

1. **Title and one-line explanation** of what this demo lets you explore (the
   question from Step 1).
2. **Current state** - the full relevant state as a labelled panel, not a raw
   JSON dump, re-rendered after every action.
3. **Free-play buttons** - one per action, always available, so anyone can
   poke the model in any order.
4. **Guided walkthroughs** - one tab per scenario (the happy path, a tricky
   edge case, an attempt at something that should be illegal), each holding a
   short plain-language description and the ordered real buttons to press.
   Starting a walkthrough resets to a known initial state.

Every label is in domain language (see `blueprint/context/domain-language.md`
if it exists), not code - buttons and state read like the business, not the
reducer. Linking `theme.css` for surface/text colors is optional but keeps it
visually consistent; this mode cares about the model, not the polish.

### Step 4 - hand it over

Send or open the file. The interesting moments are "wait, that shouldn't be
possible" or "huh, I assumed X would be different" - those are bugs in the
*idea*, which is the point. Add actions or scenarios if the user wants to
probe further; prototypes evolve during the session.

### Then stop

Once the prototype has answered its question, tell the user the concrete next
step: the validated reducer/machine/function set lifts into the real module
during `/feature` or `/implement` - cite this file as the source of that
logic in the spec's Notes for the AI. The HTML shell itself never ships and is
discarded at that feature's `/complete`.

## Formatting

Format the output to match the project's conventions in
`blueprint/context/ai-interaction.md`: concise, scannable markdown, with lists for
enumerations and tables for matrices rather than dense paragraphs.
