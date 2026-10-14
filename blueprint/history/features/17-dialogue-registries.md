# Feature: Dialogue action and condition registries

**From build-plan:** feature 17
**Status:** complete (2026-10-14)
**Branch:** `feature/dialogue-registries`

## Goal

A new dialogue condition or action becomes one small class registered by name
instead of a new `case` in `DialogueSystem`. The seven condition names and nine
action names the JSON uses today move into their own classes behind two
interfaces, the dialogue system looks them up by the term's name, and a
conversation that uses a name nobody registered says so once when it starts
instead of silently showing the choice or doing nothing. Existing JSON is
unchanged and the game plays identically: the smoke run stays 46 of 46 and the
sixteen 15b dialogue tests stay green untouched. Same shape as item 16.

## In scope

- `scripts/Gameplay/Dialogue/`: `DialogueTerm` (parses `"take_money:4500,heal"` into named terms), `DialogueContext` (what a condition or action may touch), `IDialogueCondition`, `IDialogueAction`, one `DialogueRegistry` holding both kinds by name, and the built-in kinds, one type per file.
- Conditions: `MissionCondition` (registered as `mission_available`, `mission_active` and `mission_done`), `MissionStepCondition`, `WantedCondition` (registered as `wanted` and `not_wanted`), `MoneyCondition`.
- Actions: `StartMissionAction`, `TalkDoneAction`, `GiveMoneyAction`, `TakeMoneyAction`, `ClearWantedAction`, `GiveAmmoAction`, `HealAction`, `SleepAction`, `EndAction`.
- `DialogueSystem` keeps its eight-argument constructor (which uses the built-in registry) and gains a nine-argument one that takes a registry; its two switches become lookups.
- A notification at `Start`, once per distinct unknown name, when a conversation uses a condition or action nobody registered.
- Tests for the parser, each kind, the registry, and the custom-kind tests that drive the conversion.

## Out of scope

- Any change in what a name does. An unknown condition still holds and an unknown action still does nothing, as today; the notification is the only new behaviour.
- Culture-invariant number parsing for `sleep:` and the money terms (lifted verbatim; already on the `/audit` list).
- New conditions or actions (weather, time of day, reputation); item 25 adds them through this registry.
- The `Game.Instance` call sites in `Npc`, `DialogueBox`, `PauseMenu` and `WorldMap`; that is item 18.
- The smoke test, which drives dialogue through `Choose` and names no term.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - Terms, context, interfaces, registry** - `DialogueTerm.Parse`, `DialogueContext`, `IDialogueCondition`, `IDialogueAction` and `DialogueRegistry` with both `Add` overloads, `FindCondition` and `FindAction`; no `Builtin()` and no kinds yet. Tests: `DialogueTermTests` written first against the missing type (the red is CS0246), and `DialogueRegistryTests` for add and replace using test-only kinds. *Done when:* `dotnet build` is clean and `dotnet test tests/Veld.Tests` is green with the new cases.
- [x] **Step 2 - Conditions** - the four condition classes, each body lifted from the matching arm of `Holds`, and `DialogueRegistry.Builtin()` registering the seven condition names. Tests: `DialogueConditionsTests`, one case first written with a wrong literal to see red. *Done when:* every condition case under Testing passes and the whole suite is green.
- [x] **Step 3 - Mission, player and clock actions** - `StartMissionAction`, `TalkDoneAction`, `GiveAmmoAction`, `HealAction`, `SleepAction` and `EndAction`, added to `Builtin()`. Tests: the first six cases of `DialogueActionsTests`. *Done when:* those cases pass and the suite is green.
- [x] **Step 4 - Money actions** - `GiveMoneyAction`, `TakeMoneyAction` and `ClearWantedAction` with their messages lifted verbatim, added to `Builtin()`, plus the registry inventory test (seven conditions, nine actions, null for `weather` and `dance`). *Done when:* the money cases and the inventory test pass and the suite is green.
- [x] **Step 5 - Dialogue system on the registry** - write `A_condition_and_an_action_added_to_the_registry_drive_a_choice` against the nine-argument constructor and see CS1729 (the red); then add the constructor, build the context in `Start`, replace `Holds` and `Apply` with lookups over `DialogueTerm.Parse`, add the unknown-name notification, replace the summary comment's name list with a pointer to `Dialogue/`, and add `An_unknown_condition_or_action_is_reported_when_the_conversation_starts`. *Done when:* both tests are green, the sixteen 15b tests are green without modification, `dotnet build` is clean, no condition or action name remains in `DialogueSystem.cs`, and the smoke run passes 46 of 46 including "dialogue opened and input locked" and "dialogue closed, input unlocked".

## Files / areas

- `scripts/Gameplay/Dialogue/DialogueTerm.cs`, `DialogueContext.cs`, `IDialogueCondition.cs`, `IDialogueAction.cs`, `DialogueRegistry.cs` (new)
- `scripts/Gameplay/Dialogue/MissionCondition.cs`, `MissionStepCondition.cs`, `WantedCondition.cs`, `MoneyCondition.cs` (new)
- `scripts/Gameplay/Dialogue/StartMissionAction.cs`, `TalkDoneAction.cs`, `GiveMoneyAction.cs`, `TakeMoneyAction.cs`, `ClearWantedAction.cs`, `GiveAmmoAction.cs`, `HealAction.cs`, `SleepAction.cs`, `EndAction.cs` (new)
- `scripts/Gameplay/DialogueSystem.cs`
- `tests/Veld.Tests/Gameplay/Dialogue/DialogueTermTests.cs`, `DialogueRegistryTests.cs`, `DialogueConditionsTests.cs`, `DialogueActionsTests.cs` (new); `tests/Veld.Tests/Gameplay/DialogueSystemTests.cs` (two added tests, existing tests untouched)

## Data / contracts

Load-bearing for item 25, which adds conditions and actions through this
registry, and for item 19, where the context is what moves into `Veld.Sim`.

```csharp
namespace Veld.Gameplay.Dialogue;

/// <summary>One name and its colon-separated arguments: "mission_step:parcel:1" is Name "mission_step", Args ["parcel", "1"].</summary>
public sealed class DialogueTerm
{
    public string Name { get; }
    public string[] Args { get; }
    /// <summary>Splits a comma-separated list. Terms are trimmed and blank ones skipped, so "" gives none.</summary>
    public static List<DialogueTerm> Parse(string text);
}

/// <summary>What a condition or action may read and change: the systems behind the conversation and the character in it.</summary>
public sealed class DialogueContext
{
    public DialogueContext(MissionManager missions, IWantedState wanted, IWorldQuery world, IEconomy economy, INotifier notifier, IClock clock, INpcView npc);
    public MissionManager Missions { get; }
    public IWantedState Wanted { get; }
    public IWorldQuery World { get; }
    public IEconomy Economy { get; }
    public INotifier Notifier { get; }
    public IClock Clock { get; }
    public INpcView Npc { get; }
}

/// <summary>Decides whether a choice is shown.</summary>
public interface IDialogueCondition
{
    string Name { get; }
    bool Holds(string[] args, DialogueContext context);
}

/// <summary>Runs when a choice is picked. True means the conversation must end now.</summary>
public interface IDialogueAction
{
    string Name { get; }
    bool Apply(string[] args, DialogueContext context);
}

public sealed class DialogueRegistry
{
    public static DialogueRegistry Builtin();            // the seven conditions and nine actions
    public DialogueRegistry Add(IDialogueCondition c);   // replaces a namesake; returns this
    public DialogueRegistry Add(IDialogueAction a);      // likewise
    public IDialogueCondition? FindCondition(string name);
    public IDialogueAction? FindAction(string name);
}
```

`DialogueSystem` builds one `DialogueContext` in `Start` and drops it in
`End`, so `Npc` is never null while a conversation is open. Unknown-name
notification text, once per distinct name per `Start`, walking the nodes in
order and reading a choice's condition before its action:
"Dialogue '<id>' uses an unknown condition '<name>'." and
"Dialogue '<id>' uses an unknown action '<name>'." They come before the first
node is shown.

## Testing

The test gate is on. All in-process, on the 15a fakes. Classes whose expected
text goes through `Money.Format` (the fine message) sit in the `era flags`
collection.

Parser (`DialogueTermTests`):

- `"take_money:4500,heal"` gives two terms: `take_money` with `["4500"]` and `heal` with no args.
- `" mission_step:parcel:1 "` gives one term with args `["parcel", "1"]`; `""` gives none; `"end,,"` gives one.

Conditions (`DialogueConditionsTests`), each fetched from `DialogueRegistry.Builtin()` and held as `IDialogueCondition`:

- `mission_available:parcel` holds before the mission starts and not after; `mission_active:parcel` the reverse; `mission_done:parcel` only once `Restore` lists it; each is false with no argument.
- `mission_step:parcel:1` holds only while the mission is active at step 1; false at step 0, false with a non-numeric step, false with one argument.
- `wanted` holds at level 1 and not at 0; `not_wanted` the reverse.
- `money:4500` holds at 4500 and not at 4499; false with a non-numeric amount.

Actions (`DialogueActionsTests`), each held as `IDialogueAction`; every one returns false except `end`:

- `start_mission:parcel` starts it (the notifier shows "New task: Aunty Devi's Parcel"); with no argument nothing happens.
- `talk_done` reports the context's character to the mission manager: a `talk` step targeting that character completes.
- `give_ammo:pistol:12` gives `("pistol", 12)`; with one argument nothing happens.
- `heal` heals once; `sleep:8` advances the clock by 8; `end` returns true and touches nothing.
- `give_money:200` adds 200 and drops coins at the character's position.
- `take_money:4500` with 5000 leaves 500; with 100 leaves 100 and says nothing.
- `clear_wanted:50000` with 60000 leaves 10000, clears the level and says "Paid R500.00. The complaint is withdrawn." (`Money.Modern = true`); with 100 says "You cannot afford the fine." and leaves the level alone; with no argument clears for free.

Registry (`DialogueRegistryTests`):

- `Add` makes a test-only condition and a test-only action findable and replaces a namesake (step 1).
- `Builtin()` finds all seven condition names and nine action names and returns null for condition `weather` and action `dance` (step 4).

Dialogue system (`DialogueSystemTests`, two new tests):

- `A_condition_and_an_action_added_to_the_registry_drive_a_choice`: a test-only `never` condition hides a choice, and a test-only `count` action runs when its choice is picked.
- `An_unknown_condition_or_action_is_reported_when_the_conversation_starts`: a node whose only choice has condition `weather:rain` and action `dance` reports exactly `"Dialogue 'superette' uses an unknown condition 'weather'."` then `"Dialogue 'superette' uses an unknown action 'dance'."`, still shows the choice, and picking it closes the conversation without error.

**Test seams**

- Public interface under test: `DialogueTerm.Parse`, each kind through its interface, `DialogueRegistry`, and `DialogueSystem` through its public members as in 15b.
- Behaviour the tests prove: a condition decides on its own whether a choice shows, an action does its own work and says whether the conversation ends, and the dialogue system only parses and looks up.
- External boundary, local substitute, or mock: none external; the 15a fakes (`FakeWorld`, `FakeEconomy`, `FakeNotifier`, `FakeClock`, `FakeWanted`, `FakeDialogueView`, `FakeNpc`) as before, with a real `MissionManager`.
- Red command and expected failing signal: `dotnet test tests/Veld.Tests`; CS0246 on `DialogueTerm` in step 1, CS1729 on the nine-argument constructor in step 5, otherwise an `Assert` failure showing expected versus actual.
- Full Verify command after green: none declared; `dotnet build`, `dotnet test tests/Veld.Tests`, then the smoke run from `AGENTS.md`.

## Notes for the AI

- Lift each `case` and switch arm verbatim into its kind; the behaviour is pinned by the 15b tests, which must not be edited.
- `DialogueSystem` must not know any condition or action name after this; the only strings it keeps are the notification texts, "(Leave)" and the missing-dialogue message.
- The `end` action returns true; `DialogueSystem` then calls `End()` and skips the rest of the comma list, as today.
- The namespace is `Veld.Gameplay.Dialogue`. `Game.Dialogue` is a property and `NpcData.Dialogue` a field, both reached by member access, so there is no clash; if the compiler ever reports an ambiguous `Dialogue`, qualify that use rather than renaming the namespace.
- One type per file, Allman braces, file-scoped namespaces, British spelling in player-facing text, no em dashes.
- No new `Game.Instance` call sites; `Game` keeps calling the eight-argument constructor.
- No AI attribution in commit messages; ask before committing.
