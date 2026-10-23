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
2. **Build's choice-major walk and the carriers on every run** (next): Build's first start walks its effective order, entry by entry,
   each entry's accounts by the existing walk; `no-posture` and `no-connector` passes; the wait's sentence; the native carrier from
   the entry (`--model`, `--effort`) and the protocol carrier (`set_config_option` for model then effort) on the first open, the
   resume and the go-on.
3. **Per-account offers and refusals** (§3.3, §3.5 (a), (c), (d)): `agent-offers` in the three home twins; `OpeningOptions`;
   `choice-not-offered`; `Release`; the resume's put-back; `account.refused-model`; `SessionStarted.NotOffered`; no *Ended* notice.
4. **The screen**: AI features' *Actors* with Build's editor; the note, the chip and the wait; the Agents fold's line.

## Checks run

Part 1, focused classes only (the standing test rule): driver `ActorChoicesTests`, `InstallUpdateTests`, `ConfigTests`,
`DriverConfigKeepsTests`, `DriverChoicesTextTests`, `DriverChoicesCommandTests`, `HelpCoverageTests`, `HelpSettingProposalsTests`,
`BrainstormLookTests`, `IntakeTests`, `HelpRoom*`; modules `DriverModuleActorTests`, `DriverModuleStartFromTests`,
`DriverModuleConversationTests`, `PipeCensusTests`, `PipeWordingTests`, `StatePublisherTests`, `DriverModuleRoutesTests`,
`VerbLeasesTests`, `DriverModuleDriverTests`; the CLI's `cli-usage.test.ts`; the web's `src/bridge` tests, the typecheck,
`i18n:check` and `names-check.mjs --strict`.

## Readings

- *Chat as before* reads `adapter` live until the next write that changes it (design §4.0, round 3), so a cleared Chat follows Build
  only until Build next changes; `ActorChoicesTests` holds set, clear Chat, change Build.
- `driver adapter <agent>` replaces Build's whole order with one choice at account default, and says what it replaced (design §6.4's
  table). It does not keep later choices, since the verb names one agent.
- Until row d, `SET_INTAKE` and `SET_HELPER` write their key and take their actor back to as before, so the key they write is what
  the next start reads.
