# MODELROLE1b build record

Quest `#eb9269d33cff`, D205 row b. Reporting only: every choice remains `before`.

## Plan and checkpoints

- MODELROLE1a merged first; the branch was then replayed onto `main`, which already held MODELROLE1a, keeping only this row's
  commits. The generated index was taken from `main` and regenerated. MAKERTRAIL1's `SessionTarget.CommitTrailer` and this row's
  `SessionTarget.Asked` sit side by side.
- Storage/HTTP: the five machine-local record fields and their loopback-only later door. Old and mirrored records read them as
  absent. `actor` is written at the open, the other fields through `POST /api/sessions/{id}/actor`, with no revision and no
  fields on the feed.
- Driver: `actor` is the first, required parameter of `HoldAsync` and `ChatRunner.RunAsync`, the two spawn seams, so a new
  start without one does not compile. Public chat presses keep their signatures and name Chat or Ask Daoris at that seam. The
  first native start, the resume and the chat carry the same tagged `Asked` (`ActorAsked.NativeArguments`). `TurnModels.Reported`
  keeps the door's names in report order before token attribution sorts or collapses them, and quota names survive counts that do
  not reconcile. `ActorRun` watches a run's turn events, keeps `ran` bounded (8 models, 8 entries, each with a count of more),
  writes in order and drains at the run's end, and logs `actor.ran`; a failed write is logged and changes nothing.
- The composer's provenance reads only the list the setter returned (`AcpSession.LastAcknowledgedOptions`). Its existing local
  projection is kept for the menu but is never taken as an acknowledgement.
- Usage history: each turn line carries the start's `actor`; older lines have none.
- Brainstorm: each pass record carries `actor`, `choice`, `asked` and `ran`, and a restart reads them back and goes on adding to
  `ran`. The result document names the models that ran.
- Web: *Details* shows actor, choice, asked model and effort, acknowledged model, model that ran, acknowledged effort and the
  changes reported, in both languages. The parts fold's newest line names the model that ran. The sessions push owner
  allow-lists the five fields and hears the client's provenance write after it succeeds, so a reported turn updates *Details*
  without a state move or a sync revision.

## Verification

Focused classes only, as the standing test rule for this repository says:

- Driver: `ActorRunTests` (the 13-start table and the required seams; the record cases; explicit `default`, absent model and
  uncarried effort through serialization and each native prepare), `AcpTests` (Claude's measured quota names; `codex-acp`'s
  answer-only report; *not reported* where the answer names none; a setter with no returned list is no acknowledgement),
  `ClaudeStreamJsonTests` (the pipe's reported names, init excluded; the refusal's `<synthetic>` names nothing),
  `UsageTurnsTests`, `BrainstormFilesTwinTests` (a pass and its restart), `ChatGoOnTests`, `HelpPlaceWorkspaceTests`,
  `HelpWiredTests`, `AskAndWaitTickTests`, `GatedStartsTests`.
- Process classes, each alone with `src/Daoris.Desktop/process.runsettings`: `ChatGoesOnProcessTests` (13) and
  `BrainstormProcessTests` (2, a real brainstorm pass recording actor, choice, asked agent and the stub's reported model).
- Modules: `SessionsStateSliceTests`. Service: `SessionStoreTests`, `SessionSyncTests` (no sync carries the fields). HTTP:
  `ContinuesHostTests` and `PersonDoorTableTests` (loopback only).
- Web: the `work/head` tests and `actors.test.ts`, the typecheck, `i18n:check` and `names:check -- --strict` (no finding of this
  row's keys).
- Censuses: `read-shares`, `optional-locks`, `live-logins`, `partials`, `one-word` and `home-census`.

## What the installed window should show

After the install carries this build: open a driven session started after it, and open *Details* on its head. It names
**Build** as the actor and **as before** as the choice, with the asked model and effort both **account default**. Once its first
turn ends, **Model that ran** names the model Claude reported (for example `claude-sonnet-5-5`, and the helper model beside it
where the turn used one), never the model the session was configured with. A codex-acp session names the model Codex reported.
A session that started before the install took this build, and one from another machine, reads *not recorded*. The parts fold's newest line ends with the same model. None of this is proven by the stories or the stubs above;
it needs the installed window.

## Readings

- D205 design §14 row b fixes this change to reporting; selection and model carriers belong to later rows.
- The MODELROLE1a evidence says ACP quota names may include a side model; names are provenance independently of token
  reconciliation. Neither a configured model nor an init frame proves what ran.
- The pipe door's `modelUsage` may be the process's running total (MODELROLE1a left it open). `ran` is a set over the
  session, so a name repeated from an earlier turn adds nothing.
