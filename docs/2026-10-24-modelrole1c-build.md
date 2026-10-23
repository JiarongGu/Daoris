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
3. **Per-account offers and refusals** (remaining, filed as MODELROLE1c2) (§3.3, §3.5 (a), (c), (d)): `agent-offers` in the three
   home twins; `OpeningOptions`; `choice-not-offered`; `Release`; the resume's put-back; `account.refused-model`;
   `SessionStarted.NotOffered`; no *Ended* notice.
4. **The screen.** 4a is built: AI features' *Actors* (`ActorsCard`), each order folded to one line, with Build's editor. It
   holds one row each for agent, model and effort, *Move up*, *Move down*, *Remove*, *Add a choice* up to four, *Save* and
   *Clear*. A refusal is said under *Save*. Saving a change of Build's agent says that Chat and Brainstorm keep theirs. The
   wait's sentence is the driver's, said where a held quest is said. The room's doors name `daoris driver actor`, and its
   proposal door is owed to MODELROLE1h. Remaining in MODELROLE1c2: the head's *choice n* chip, the opening note's actor line
   and the Agents fold's line.

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
