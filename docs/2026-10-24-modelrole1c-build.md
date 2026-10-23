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
