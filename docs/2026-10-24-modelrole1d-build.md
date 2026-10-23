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
2. **Chat's and Ask Daoris's starts take their actor** (built). A new conversation (`START_CHAT`, `SESSION_START_FROM`) walks
   Chat's effective order and a new Ask Daoris (`START_HELP`, `HELP_START_FROM`) Ask Daoris's (`ChatRunner.WalkAsync`): an order of
   one is today's start on its agent, asking its model; more are walked choice by choice, each choice's accounts by the existing
   walk judged for its model. An entry that holds no conversation is passed `unavailable`, and for Ask Daoris one its access refuses
   under the mode standing now `no-posture` (`HelpAccess.For`), before any account is asked; with none, the press is refused in
   *Chat waits: none of its choices can run now.* / *Ask Daoris waits: …*. An agent the person named on the new conversation's chip
   is a pick, its model Chat's first entry for that agent. The record keeps the entry's `choice` and `asked`, and the opening note's
   actor line says a later choice. The chat's protocol open carries its asked choice (`AcpSession.Carry`) and keeps what its open
   listed as its agent's offer reading, so the composer opens on the actor's model; the native door carries it through
   `NativeArguments` as before. The composer's change is kept as `override`, reconciled from the returned configuration
   (`ActorOverride.Reconciled`: an effort-only change keeps the model, a model change clears the effort), and a conversation going on
   re-applies it over its asked choice (`ActorAsked.Overlaid`), its record's asked left as its open set it. Continuations (a hand-over,
   a limit move, a briefed move) keep running on the agent they name, as before.
3. **Intake's start takes its actor** (built). An intake walks Intake's effective order, read once for the start
   (`DriverIntakes.WalkAsync`): an order of one is today's start asking its model; more are walked choice by choice on accounts not
   kept for conversations, an agent this machine lacks passed `unavailable`; with none, the ask holds on *Intake waits: none of its
   choices can run now.*, the soonest cool-off beside it. The target carries the entry's `Asked` and `Choice`, so both doors carry its
   model and effort and the record keeps its choice; the opening note's actor line says a later choice.
4. **§3.5(b)'s pending start** (remaining): `ChatPending` owning the attempt across replacements, the queued words moved, the go-on
   after an interruption, a pick never walked afresh, a refused continuation's brief recomposed.
5. **§3.5(f)'s Intake re-ask** (remaining): `EndRefusedIntakeAsync` and the Intake's walk within its own start.

## Checks run

Part 1: driver `ActorChoicesTests`, `DriverChoicesCommandTests`, `DriverChoicesTextTests` (its six intake and helper rows changed,
with why), `HelpCoverageTests`, `HelpSettingProposalsTests`, `HelpRoom*` (the goldens written again for the two door rows),
`ConfigTests`, `DriverConfigKeepsTests`, `IntakeTests`, `InstallUpdateTests`; modules `DriverModuleDriverTests`,
`DriverModuleHelpTests`, `DriverModuleActorTests`, `PipeCensusTests`, `PipeWordingTests`, `StatePublisherTests`,
`DriverModuleRoutesTests`, `StartWiringRouteTests`; the web's `src/settings`, `src/bridge`, `src/locales`, the stories and
presentational tests, the typecheck, `i18n:check`, `names-check.mjs --strict` and `check:web-rules`. No Process class was changed.

Part 2: driver `ActorRunTests` (the override's reconciling and overlay), `HelpPlaceWorkspaceTests` (Ask Daoris's walk, `no-posture`,
as before restricted and full, the wait's sentence, Chat's walk and a pick), `AcpTests`, `ChatGoOnTests`, `HandOverRunnerTests`,
`BuildWalkTests`; the changed Process class `ProtocolChatTests` with `process.runsettings` (a new conversation opening on its Chat
entry's model and effort; its stand-in now keeps the effort it is set to), and beside it `ChatRunnerTests`, `HelpChatTests`,
`ChatGoesOnProcessTests`, `LimitGoesOnProcessTests`, `HandOverProcessTests`, `CurveMoveProcessTests`, `ChatWordsRestartProcessTests`;
modules `DriverModuleConversationTests`, `DriverModuleStartFromTests`, `DriverModuleHelpTests`, `DriverModuleActorTests`.

Part 3: the changed Process class `IntakeTests` with `process.runsettings` (an intake walking past an agent this machine lacks, and
waiting on the wait's sentence with none), its fast half, `InstallUpdateTests` and `ActorRunTests`.

## Readings

- *Off* on Intake's and Ask Daoris's editors is a press beside *Save* (`settings.ai.actors.off`, *Off* / 关闭), not a value in the
  model menu: §7 says the two editors "add *Off*", and an order that is off has no entries to hold a model. *Turn off* was refused
  by the glossary, which keeps *turn off* for a plugin (停用).
- The Intake card's per-circle *Runs as* lines and Ask Daoris's tier sentence stay, under their rows, since §7 folds the cards into
  the rows and says nothing is lost; their ⓘ sentences are the cards' own.
- A conversation going on with a kept `override` does not write its record's `asked` again: §9.3 sets `asked` at the open and again only
  by the same-account move, and the override re-applied is the person's change, not the open's ask.
- A route that names an agent (the new conversation's chip) is a pick even where it names Chat's own first agent: the person chose it,
  and §3.5(d) keeps a pick on its agent.
