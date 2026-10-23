# MODELROLE1d build record

Quest `#00b7114a6f5f`, D205 row d (*Intake, Ask Daoris and Chat run their choices*), on main after MODELROLE1b, 1c and 1c2. The row
is large, so it lands in parts on one branch, each committed green, as the quest allows. This record says which parts are built and
which remain.

## Parts

1. **The old doors write actors, and the two cards fold into rows** (built). `daoris driver intake|helper <agent>|off` write Intake's
   and Ask Daoris's one-choice order at account default, or off, through `SET_ACTOR`, saying the order they replaced.
   `SET_INTAKE` and `SET_HELPER` leave the driver module, `PipeCensus`, `PipeWording`, the person card's words, the page's publishing
   twin and the bridge (`useSetIntake`, `useSetHelper`). `setting_propose`'s `intake` and `helper` doors write the same one-choice
   order, judged by `ActorChoices.Judge`, until MODELROLE1h retires them. A census test holds that no production door calls the old
   setters. The room's machine facts read Intake's and Ask Daoris's first agent through `Effective`; its doors table names their
   rows on *Actors*. On AI features the *Intake* and *Ask Daoris* cards are folded into their actor rows: each row keeps its ⓘ and
   what it does now under its order (`IntakeAbout` with the per-circle *Runs as*, `AskDaorisAbout`), off in its own words
   (*Off — declarations only*, *Off — starters only*). Intake's, Ask Daoris's and Chat's editors are offered beside Build's;
   Intake's and Ask Daoris's add *Off*; Chat's and Ask Daoris's say what a running conversation does (§7, round 4), every other
   editor that running work never moves.
2. **Starts 5 and 10-13 take their actor** (remaining): the chat runner's start and Ask Daoris's placement walking Chat's and Ask
   Daoris's effective order, `no-posture` from `HelpAccess.For`, the pick overriding, the carriers on `ChatTarget`, the composer's
   `override` kept, reconciled and re-applied, and the composer opening on the actor's model.
3. **§3.5(b)'s pending start** (remaining): `ChatPending` owning the attempt across replacements, the queued words moved, the go-on
   after an interruption, a pick never walked afresh, a refused continuation's brief recomposed.
4. **§3.5(f)'s Intake re-ask** (remaining): `EndRefusedIntakeAsync` and the Intake's walk within its own start.

## Checks run

Part 1: driver `ActorChoicesTests`, `DriverChoicesCommandTests`, `DriverChoicesTextTests` (its six intake and helper rows changed,
with why), `HelpCoverageTests`, `HelpSettingProposalsTests`, `HelpRoom*` (the goldens written again for the two door rows),
`ConfigTests`, `DriverConfigKeepsTests`, `IntakeTests`, `InstallUpdateTests`; modules `DriverModuleDriverTests`,
`DriverModuleHelpTests`, `DriverModuleActorTests`, `PipeCensusTests`, `PipeWordingTests`, `StatePublisherTests`,
`DriverModuleRoutesTests`, `StartWiringRouteTests`; the web's `src/settings`, `src/bridge`, `src/locales`, the stories and
presentational tests, the typecheck, `i18n:check`, `names-check.mjs --strict` and `check:web-rules`. No Process class was changed.

## Readings

- *Off* on Intake's and Ask Daoris's editors is a press beside *Save* (`settings.ai.actors.off`, *Off* / 关闭), not a value in the
  model menu: §7 says the two editors "add *Off*", and an order that is off has no entries to hold a model. *Turn off* was refused
  by the glossary, which keeps *turn off* for a plugin (停用).
- The Intake card's per-circle *Runs as* lines and Ask Daoris's tier sentence stay, under their rows, since §7 folds the cards into
  the rows and says nothing is lost; their ⓘ sentences are the cards' own.
