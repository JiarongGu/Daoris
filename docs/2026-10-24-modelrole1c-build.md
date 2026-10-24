# MODELROLE1c build record

Quest `#afa5e1ff13cf`, D205 row c (*Build runs its choices*), on main after MODELROLE1b. The row is large, so it lands in parts on
one branch, each committed green, as the quest allows. This record says which parts are built and which remain.

## Parts

1. **The stored choices and their compatibility readers and writers** (built). `driver.json`'s `actors` is read and written
   (`ActorChoices.Read`/`Write`). An entry that does not read is kept as its error and holds the starts that read it
   (`ActorUnreadableException`). `ActorChoices.Judge` is the one judge, and `ActorChoices.Effective` is the one reading (as before,
   off, set; a pick, a handed agent and a fence's agents with `rule-default`). `ActorChoices.With` writes through `adapter`,
   `intakeAdapter` and `helperAdapter`. Every write that changes `adapter` first makes Chat and Brainstorm explicit where they are
   as before. Chat's agent (`START_CHAT`, `SESSION_START_FROM`, `sessions start-from`), Brainstorm's fallback voices and Intake read
   `Effective`. The old intake and helper doors take their actor back to as before. The drain turns a set Intake off.
   `ACTORS` (`Open`) and `SET_ACTOR` (`Person`) are in the census, its wording, the person card's words, the page's publishing
   twin and the README. The page has `useActors` and `useSetActor`. `daoris driver actor` reads and sets orders, and `driver adapter`
   now goes through `SET_ACTOR`. `setting_propose`'s `adapter` door writes through the same write, and its card names the actors it
   keeps.
2. **Build's choice-major walk and the carriers on every run** (built). A new quest's first start walks Build's effective order
   (`Driver.WalkBuildAsync`), entry by entry, each entry's accounts by the existing walk. It passes `unavailable`, `no-posture` (the
   start's own `DrivenAccess`) and `no-connector` (the pipe door, for a repository that has not adopted Daoris) before asking any
   account, and passes a choice whose walk holds with the walk's own reason. With nothing that can run, the start holds on
   *Build waits: none of its choices can run now.*, each choice named, the soonest cool-off beside it. The machine log says
   `actor.chose` and `actor.passed`. An order of one is today's walk exactly. `Door()` is the protocol door where any entry rides it.
   A handed quest takes Build's entry for its agent (`handed`). A carry-on and a resume keep their record's asked choice. The target
   carries `Asked` and `Choice`, and the record keeps the choice taken. The native carrier is row b's `NativeArguments`, now fed
   from the entry. The protocol carrier (`AcpSession.Carry`) sets the model, then the effort, by category, after `session/new` or a
   resume and before the posture and the first prompt. It sets the posture again after a change, since a model change can move it.
   A value the agent offers no option for, refuses, or answers with another value it offers is kept as asked and not carried
   (`Carried`), so the record never says a choice was taken that was not. Until part 3, a refused model runs at account default,
   said in *Details*, rather than ending the record `choice-not-offered`.
3. **Per-account offers and refusals** (built by MODELROLE1c2, below) (§3.3, §3.5 (a), (c), (d)): `agent-offers` in the three
   home twins; `OpeningOptions`; `choice-not-offered`; `Release`; the resume's put-back; `account.refused-model`;
   `SessionStarted.NotOffered`; no *Ended* notice.
4. **The screen.** 4a is built: AI features' *Actors* (`ActorsCard`), each order folded to one line, with Build's editor. It
   holds one row each for agent, model and effort, *Move up*, *Move down*, *Remove*, *Add a choice* up to four, *Save* and
   *Clear*. A refusal is said under *Save*. Saving a change of Build's agent says that Chat and Brainstorm keep theirs. The
   wait's sentence is the driver's, said where a held quest is said. The room's doors name `daoris driver actor`, and its
   proposal door is owed to MODELROLE1h. Remaining in MODELROLE1c2: the head's *choice n* chip, the opening note's actor line
   and the Agents fold's line (built by MODELROLE1c2, below).

## MODELROLE1c2: the rest of row c

Quest `#0c52196a3b0f`, in three parts on one branch, each committed green.

1. **`agent-offers`** (built). `data/agent-offers.json` keeps, per agent, version and account, the model and effort values an
   open listed and the models refused there, with when the last refusal was read (`AgentOffers`). Every process writes it merged
   per key under its keep lock; a reading from an older version never replaces a newer one. `AgentOffers.Judge` passes an account
   for a model only on that account's own refusal, at the start's version, under a day old. The entry is in D172's table in the
   driver, service and CLI twins, and in D195's inventory (the cleanup design's *Settings and small records*).
2. **The walk's model and the paths after the open** (built). `SelectAsync`, `SelectHandedAsync`, `ResumeAsync` and
   `CarriedOnAsync` take the model the readings are judged for, a per-start passed set and the start's own count to leave out.
   An account its reading refuses is passed, never cooled; an entry whose every ready account is passed holds with
   `HarnessSelection.NotOffered`, and Build's walk passes it `not-offered`. At a protocol open the entry's model is always set:
   a refusal (the set refused, or answered with another offered value) is kept as the account's reading, then the start's
   re-ask (`Driver.ReaskAsync`, the walk with no count) answers. A later choice of the same agent on the same account is set on
   the open session, after each category the refused attempt moved is set back to `OpeningOptions`. Anything else ends the open
   before its prompt (`AcpChoiceNotOffered`) and the record `failed` with `ended.choice-not-offered`; `Release` gives back the
   count and the look's running reading once the process stopped. Such a record is no strike, no last run, neither running nor
   last started (`SessionStarted.NotOffered`), and raises no *Ended*. A continuation and a handed quest keep their choice, so their
   refused record ends. A resume whose own account's reading refuses its model carries its words to another account of the same
   choice (`ResumeChoice.NotOffered`, `account.resume-not-offered`); caught at the open, it is put back as it ended.
   `account.refused-model` joins `NoteCodes.AccountsOwn`. The opening note's actor line (`opening.actor`) is kept in the local
   conversation where a later choice ran, at the start or by a same-account move.
3. **The screen** (built). The head's *choice n* / *your pick* chip (`ChoiceChip`, `choiceChip`), on the facts line before the
   access chip and beside the word with no header; the note lines' words in both languages; the Agents page's *Model and effort*
   fold's line.

**Remaining, filed as MODELROLE1c3:** the pipe door's measured model refusal (`model_not_found` on a silent turn) on a first start
and a go-on run, kept as the account's reading and ending with `account.refused-model`; the session list's and head's quiet
*Not started* word for a `choice-not-offered` record and a teammate mirror's sentence (today the state reads *failed*, its note's
line says *Not started*); the chip's stories at the frame's widths; the installed-window proof.

### Checks run (MODELROLE1c2)

Driver, focused: `AgentOffersTests`, `DaorisHomeTests` (driver and service), `BuildWalkTests`, `AcpTests`, `NoteCodesTests`,
`NoteSitesTests`, `ResumeAccountTests`, `AttentionTests`, `StrikeTests`, `AccountRotationGoalTests`, `StructureTests`; the new
Process class `ModelRefusalTickTests` with `process.runsettings` (both proofs). The driver's and the modules' fast halves: the
driver's one failure, `StructureTests` (the walk's records declared in `Loop/`), passes since they moved to `Platform/`; the modules'
one failure, `SweepStateSliceTests`' ref-move case under load, passes alone and is not touched by this change. The CLI's `home.test.ts` and `home-census.test.ts`. The web's typecheck, the note,
note-line, actor, access-fact and Agents tests, the head's folder, `i18n:check`, `names-check.mjs --strict` and `check:web-rules`.

### Readings (MODELROLE1c2)

- A list that lacks a model never passes an account by itself: MODELROLE1a measured Claude's door listing aliases and taking a full
  id it does not list. So the open always asks, and only the agent's refusal at the set is a reading; it is `refused` where the
  list named the value and `not-offered` where it did not (`docs/2026-10-23-model-effort-doors-evidence.md`, the ACP door).
- A refusal's day counts from the refusal, not from the last open, so opens that list the model again do not keep it refused past a
  day, and do not clear it before (§3.3, *an entitlement that changes without a new version is read again within a day*).
- The record's end names the agent and the model but no account, since its note travels; the account is the record's own, in
  *Details* (§3.5(c) words the local row with the account).
- A resume caught at the open is put back and its words go on at the next look, whose resume judges the kept reading and takes
  `ElsewhereAsync`, rather than in the same call (§3.5(a) round 4); the record and its words are untouched either way.
- The refused record's fresh tree is left to the existing tidying of an ended record rather than let go at once: the record already
  names it.

## Checks run

Part 1, focused classes only (the standing test rule): driver `ActorChoicesTests`, `InstallUpdateTests`, `ConfigTests`,
`DriverConfigKeepsTests`, `DriverChoicesTextTests`, `DriverChoicesCommandTests`, `HelpCoverageTests`, `HelpSettingProposalsTests`,
`BrainstormLookTests`, `IntakeTests`, `HelpRoom*`; modules `DriverModuleActorTests`, `DriverModuleStartFromTests`,
`DriverModuleConversationTests`, `PipeCensusTests`, `PipeWordingTests`, `StatePublisherTests`, `DriverModuleRoutesTests`,
`VerbLeasesTests`, `DriverModuleDriverTests`; part 2: `AcpTests`, `BuildWalkTests` and the non-process look classes covering a
driven start (`AccountGoalTickTests`, `AccountLimitTickTests`, `AccountRotationTickTests`, `AcpTurnFailureTickTests`,
`AskAndWaitTickTests`, `CapCountTickTests`, `HandOverQuestTickTests`, `LimitResumeTickTests`, `NativeResumeTests`,
`RefusedByTheHarnessTests`, `SignedOutWaitTests`, `SignInRefusalTickTests`, `TrustHoldTickTests`, `StartGitCountTests`,
`ProviderRefusalTickTests`, `ActorRunTests`, `GatedStartsTests`, `AccessSwapTickTests`, `DoneEndTickTests`); part 4a:
`HelpCoverageTests`, `HelpRoom*` (the goldens written again for the two door rows and the owed door), the web's `src/settings`
tests (`AiDomain.test.tsx`'s Actors cases among them) and `src/design/stories.test.tsx`, `storyWidths.test.tsx`; the CLI's
`cli-usage.test.ts`; the web's `src/bridge` tests, the typecheck, `i18n:check` and `names-check.mjs --strict`. No Process class
was changed, so none was run.

## Readings

- *Chat as before* reads `adapter` live until the next write that changes it (design §4.0, round 3), so a cleared Chat follows Build
  only until Build next changes; `ActorChoicesTests` holds set, clear Chat, change Build.
- `driver adapter <agent>` replaces Build's whole order with one choice at account default, and says what it replaced (design §6.4's
  table). It does not keep later choices, since the verb names one agent.
- Until row d, `SET_INTAKE` and `SET_HELPER` write their key and take their actor back to as before, so the key they write is what
  the next start reads.

## MODELROLE1c3: what row c still owed

Quest `#ec4d9e828b6e`, in two parts on one branch, each committed green.

1. **The pipe door's model refusal** (built). `ClaudeStreamJson` reads row a's measured refusal by the frame's word: the
   `assistant` frame's `error: "model_not_found"` on a `<synthetic>` message, on a turn whose model wrote nothing and ran no tool,
   ended by a failed `result`. `HarnessWords` carries it to `HarnessEnding.ModelRefused`. `Driver.ModelRefused` keeps the account's
   `refused` reading for the model the actor's choice named (`ActorAsked.NamedModel`) and adds `account.refused-model` to the
   failure. A first start's conclusion and a go-on run's (`ConcludeResumedAsync`) both call it. A first start so ended raises no
   *Ended* (`SessionEnded.NotOffered`). A go-on run's words wait for the next look (`FailedTurnWords.Judge`), never for the person.
2. **The screen** (built). `limitedOr` and `shownState` read a `failed` record whose note carries `ended.choice-not-offered` as
   *not started* (`work.notStarted.word`), neutral and ended. `notStartedLine` gives the code's sentence: `work.notStarted.here`
   names the agent, the model and the record's account, and `work.notStarted.mirror` is used for a teammate's record, which has
   none. The session row's facts line and the head say it, the head in place of the note. `SessionPageHead`'s stories hold the
   chip at 80rem, 39rem and 25rem in en and zh, and a test composes them.

**Remaining, filed as MODELROLE1c4:** the installed proof.

### Checks run (MODELROLE1c3)

Driver: `PipeModelRefusalTests` (new) and `ModelRefusalTickTests` with `process.runsettings`, each new case seen failing first. The
focused classes covering the changed code all passed: `ClaudeStreamJsonTests`, `ContinuationTests`, `HandedRecordTests`,
`NativeResumeTests`, `NativeStreamsTests`, `RefusedByTheHarnessTests`, `TranscriptAsSaidTests`, `UsageTurnsTests`, `StrikeTests`,
`AttentionTests`, `NoteCodesTests`, `NoteSitesTests`, `AgentOffersTests`, `BuildWalkTests`, `StructureTests`, `ResumeAccountTests`.
The driver's fast half passed: 14446 tests. The web: `ui.test.tsx`, the rail, the head, `actors`, `groups`, the quests, the map
and the console's tests, `stories.test.tsx` and `storyWidths.test.tsx`; the typecheck, `check:web-rules`, `i18n:check` and
`names-check.mjs --strict`.

### Readings (MODELROLE1c3)

- A first start the pipe door refused ends `failed` with the account's line, not `choice-not-offered`. The row names
  `account.refused-model`, and on this door a prompt was handed, so the record did start a turn.
- Its *Ended* notice is left out, as a `choice-not-offered` end's is, since the next look goes on by itself (§4.6).
- The word is lower case, *not started*, by the glossary's `status` kind. The design's capital is the sentence's.
- The account in the sentence is the record's own id (`profile`), which is what this machine answers. The person's display name for
  it is not read on the list.
