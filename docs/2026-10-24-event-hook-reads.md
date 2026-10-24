# EVENTHOOK1: the reads (D191): the Opus foundation reader's of rounds 1, 2 and 3, and Codex's of rounds 2 and 3

The independent Opus 5.5 reader's foundation read of `docs/2026-10-24-event-hook-design.md` and its D173, D119 and D120 notes at `71e08c3c0`, verbatim but for its headings set one level down. The steward ran it from outside the install, read-only, on main. Codex's read stopped when its credits ran out and is owed; under D191's note of 2026-10-24 this design, which sends Daoris's events outside it, is not accepted on one maker's read. The Opus foundation reader asks another round (16 findings, 7 blocking). Round 2 answers it.

## Opus (foundation), round 1

The read, verbatim.

## EVENTHOOK1, round 1: the Opus foundation reader's read

Read at `d1b7d37d6`. No file the design cites has changed since `5e9a563bd` (an empty `git diff --stat` over `Driver/Plugins`,
`Driver/Loop/Driver.cs`, `Modules/` and `Mcp/`).

### 1. Claims about the code

Most of §1 holds at the lines it names: `HookPoints` and `HookFrames`, the hook set's reconcile, `Tell`, its teller and
`ChangesWaiting`, `HooksAtOnce`, `HookProcess`'s start, grace and stderr bound, `PluginCycles`, the state publisher, the notices
record, `Chain.cs`'s link 3, `WorkBounds.EventsWaiting`, the wiring census, and the MCP probe fallback. These claims are wrong:

- **A park never reaches `session/ended`.** The design says it does in §1.7 (design :123-125) and §4.2's mapping (:233). D173's
  note says the same at point 2 (D173.md:5398-5399). In the code, every conclusion that parks returns no `SessionEnded` at all:
  `Driver.cs:2855-2865` ("A park is not an ending (STANDDOWN2)"), `DriverIntakes.cs:414-416` and `DriverContinuations.cs:786-788`.
  The lines the design cites (`Observation.cs:174`, `:289`) are where the `awaiting-person` conclusion is made. Nothing there tells
  a plugin. The only thing that sees a park is `AttentionWatch.Observe` (`Quests/Domain/Attention.cs:230-244`), which both loops call
  after each look (`Modules/Loop/DriverLoop.cs:916`, `Driver.Host/Program.cs:972`). (F1)
- **"Landed" is not `work.ended`.** §4.2 maps *landed* to `work.ended` (:229, :233). In the end judgement, *ended* means the line
  holds the work (`Trees/SessionTrees/EndJudgement.cs:241-258`). A branch that the install landed and nobody has merged yet is
  `Handed` (`:263`), and no chain event names `Handed`. Link 1 also emits only `work.held` today (`Trees/RefWatch.cs:373`), and no
  open row makes it emit `work.ended`. The rows that D173's FABRIC-CHAIN2 note gave link 1 to (D173.md:399-401) were archived as
  built without it. (F2)
- **The plugin's environment is not only what Daoris tells it.** §1.3 (:71-72) and §3 (:201-204) read as though it were.
  `StartInfo` sets five names on top of the environment the process inherits (`HookProcess.cs:85-106`). Every child has seven names
  removed (`Platform/GitWiring.cs:40-41`), and the token variables are removed only from a session's start (`:43-47`). So a plugin
  inherits whatever Daoris inherited. (F3)
- **§6.2's knowledge server is not the process a hand-run session starts** (:343). `.mcp.json` starts
  `node tools/knowledge-server.mjs`. That is a relay: it serves a build's home under `local/knowledge-server/home/`, never the
  install's home, and it moves a running session onto a new build (`tools/knowledge-server.mjs:18-29`). (F12)
- **Two frame fields are not what §4.3 says** (:254-255). An intake's ending names its `repository` as `ask #<id>`
  (`DriverIntakes.cs:385`, `:416`, `:454`). `SessionEnded` carries no workspace (`Sessions/Domain/SessionEnded.cs:14-16`). (F13)
- **Smaller errors:**
  - §1.6's list of wakes leaves out `nodes` (`Chain.cs:30`).
  - §1.7's `work/land` row leaves out `title`, `quest.title` and `acceptedBy` (`HookPoints.cs:139-144`).
  - §4.4 and §4.5 take `state/changed`'s teller as their model. That teller starts at the first change whether or not any plugin
    listens (`Hooks.cs:1096-1100`), so "never started" (:273) is a change the builder must make, not something copied from it.
  - Row 1b's lane is given as the driver alone. The list of places a new point costs (`HookPoints.cs:20-22`) also names the
    modules' `DriverModulePluginsTests` and the page's gloss in `plugins/PluginKit.tsx`. (F14)

### 2. The mechanism's holes

**What an event carries.** The driver's frame is well bounded: a closed list of fields, no `note`, no path, and a census over the
builder. The weak point is the far end. The relay builds its words from whatever arrives at an endpoint it found through a file
that any process of the same user can rewrite. That includes every agent Daoris runs. The relay then posts those words into the
inbox as the session's own, verified child. §8's promise that no instruction rides an event (:385-386) rests on a census that never
sees the relay's input. (F5)

**The inbox token.** The design keeps the token out of Daoris by having the session's child post (§3, §10 :403-406). Suppose Daoris
was started from that session's shell. Then Daoris, its plugin and every agent it starts hold `CLAUDE_CODE_MESSAGING_SOCKET` and
`CLAUDE_CODE_MESSAGING_TOKEN`, because nothing removes them (F3). On native Windows the token is the only verification (§2.1
:150-151), so any of those processes could then post as the session's own child. No measurement covers this.

**A subscriber acting as the person.** D156 holds: no process in the chain holds the person's key. D201 does not hold for the relay:

- `relay.mjs` is the plugin's own code, shipped in its installed folder (§5.1, §6.1 :322; D120's note).
- The outside session runs it outside every Daoris job. D201 point 4 (D201.md:31) treats a process in no job as one whose covered
  presses run with no card.
- The relay inherits that terminal's environment: the inbox token, and on a role holder's terminal `DAORIS_SUPERVISOR_KEY`
  (`Sessions/Questions/SupervisorProof.cs:19-20`). Daoris itself never lets a child inherit that key (`GitWiring.cs:38-41`).
- The folder can be rewritten by any agent Daoris runs, and is replaced by an update or a package (EVENTHOOK1f).

So code that a contained agent can write would run uncontained, in a process the standing confirmation covers. (F4)

**Ordering, loss and duplicates across a restart.**

- *Ordering.* Within one run, the order is sound: one teller, events in `seq` order.
- *Loss.* A freshly started plugin knows neither `run` nor `seq` until its first event, because the handshake carries neither
  (`Hooks.cs:147-150`). Yet §5.2 writes `run` at the handshake and answers the last `seq` in the hello (:290, :294-295). Events told
  while no plugin process listens are neither told nor counted. So the `seq` gap shows only drops from the queue, never a plugin
  restart. (F8)
- *Duplicates and a second writer.*
  - A *Try* on the installed plugin starts a second process in the same data folder (`PluginTrials.cs:58-78`; the page's route
    `PluginsRoutes.cs:399`). It overwrites `subscribe.json`, then removes it when it stops.
  - A `--share` loop starts a loop-point plugin beside the lease's loop (`Hooks.cs:705-708`; `Driver.Host/Program.cs:20-27`), with
    the same result.

  Either way the live plugin goes silent, and a relay may post the kit's sample event as if it were real. (F7)

**A slow or gone subscriber, or a slow plugin.** On the subscriber side the bounds are right: 64 waiting per subscriber, 5 s to a
slow mark, and an answer at once. On the plugin side, a plugin that is alive but never answers is never restarted, because
reconcile replaces dead channels only (`Hooks.cs:725-729`). That plugin then costs 10 s per event, overflows the queue, and holds
every other listening plugin behind it (`Hooks.cs:1109`). (F9)

**Flood and cost.**

- The relay's default filter takes every state, including the steward's own stops (`byPerson`), and every workspace.
- Each delivered message is a typed prompt, and an idle session starts a turn. A run of failing endings can therefore start a turn
  every 10 s.
- The attention watch already applies the right default (`Attention.cs:252`), which the design does not reuse.
- The reconnect back-off rereads `subscribe.json` every 60 s for as long as Daoris is closed. That is a file poll, which §10 rejects
  (:407). (F10, F11)

**Measured before depending.** M1-M7 are the right gate, and §6.2 waits on M6 and M7. Four things are missing from them:

- whether a Daoris started from the session's shell, and its children, inherit the token (F3);
- whether the endpoint can be opened from both elevations (F6);
- the sustained per-sender rate at the relay's own pace (F10);
- the launcher relay of the knowledge server (F12).

M3, whether an idle session starts a turn, decides whether the first adapter is worth anything at all. Nothing that consumes the
point should be built before M3 answers (F15).

**The plugin system's own limits.** Several limits come from the plugin system itself:

- Node's `net` gives the pipe Windows' default descriptor. The install has run elevated (FIX-LOG 2026-10-21 PIPEOWNER1; terminal
  trust design :253-255). A relay that is not elevated is then refused, the same failure `InstancePipe` was fixed for (F6).
- A trial and a `--share` loop each start a second process of the plugin (F7).
- A plugin that hangs is never replaced (F9).
- The manifest does not say that the plugin serves an endpoint, though a manifest is what the person reads before switching a plugin
  on (`Hooks.cs:180-182`) (F16).

### 3. The rows

- **EVENTHOOK1a** is rightly first, and it can be built alone. Two changes: add the probes F3 and F6 name, and add a decision rule.
  If M3 and M6 both come back *no*, 1b-1e wait, because nothing would consume the point.
- **EVENTHOOK1b** can be built alone, but as written it does not give the owner what was asked. Its only kind is `session.ended`,
  which carries no park. So after 1b and 1c the steward still has to poll for *waiting on the person*, and for *landed*, which 1d
  cannot deliver either (F1, F2). With F1's change, 1b would carry the attention watch's kinds: ended, parked and quest-parked. 1b
  plus 1c would then let the steward stop polling, and 1b would be the row that gives the owner the most. Its proofs are good:
  unchanged with no subscriber, contiguous `seq`, the drop counted, and the census seen failing. Add *a session that parks is told
  once*, the test that would have caught F1. It also needs the lanes from F14.
- **EVENTHOOK1c** depends on 1a's M2 and on 1b, as stated. Its proof misses F4-F7: a hostile endpoint, an elevated plugin, a *Try*
  beside the live plugin, and a relay started with only what it needs. Its stand-in loop is a script that imitates `HookPeer`, but
  the Process-lane class through the real `HookProcess` covers the real wire.
- **EVENTHOOK1d** cannot be built as written. Its `work.ended` waits on a link-1 emission that no row owns (F2). Its quest kinds come
  from "the look's quest snapshot", given at no line. That is `service.SnapshotAsync` (`Driver.cs:917`), and a quest leaving it is
  not the same as a quest closed. Its `person.asked` wakes the steward for a Confirm only the person presses. The steward's useful
  wake is the answer: CONFIRMWAIT1 says an asker that left finds the answer in the request's record (TASKS.md:384) (F15).
- **EVENTHOOK1e** must name the install's home and must be measured through the launcher's relay (F12).
- **EVENTHOOK1f** is correctly a quest to its owner. It carries F4's question of where the relay comes from.

### 4. Findings

1. **A park is no `session/ended`.** §1.7 :123-125, §4.2 :233 and D173.md:5398-5399 are wrong. See `Driver.cs:2855-2865`,
   `DriverIntakes.cs:414-416` and `DriverContinuations.cs:786-788`: *waiting on the person*, the steward's main reason to poll, is
   never told. **Blocking.**
   *Smallest change:* take the session kinds from `AttentionWatch.Observe`'s output where both loops already call it
   (`DriverLoop.cs:916`, `Program.cs:972`). Map `Ended`, `Parked` and `QuestParked` to kinds (`session.ended`, `session.parked`,
   `quest.parked`) and drop each event's `Note`. Those events are already said once, already leave out the person's own ends, limits
   and refused choices, and already treat the first look as a baseline. Then correct §1.7, §4.2 and D173's note point 2.
2. **"Landed" is mapped to the wrong event.** `work.ended` means the line holds the work (`EndJudgement.cs:241-258`). A landed,
   unmerged branch is `Handed` (`:263`). So the event would fire after the steward's own merge, and link 1 does not emit it at all
   (`RefWatch.cs:373`). **Blocking.**
   *Smallest change:* add a kind `work.landed`, told where the look's landing records its branch (`Driver.LandDueAsync`,
   `Driver.cs:1403`) with the landing's outcome code. Keep `work.ended` as a later kind, behind a row that owns link 1's emission.
3. **The inbox token can reach Daoris and everything it starts.** A child inherits the driver's environment
   (`HookProcess.cs:85-106`), and only seven names are removed (`GitWiring.cs:40-41`). A Daoris started from the outside session's
   shell carries `CLAUDE_CODE_MESSAGING_SOCKET` and `CLAUDE_CODE_MESSAGING_TOKEN` into its plugin and its agents. §3's and §10's
   trust argument (:201-204, :403-406) then fails. **Blocking.**
   *Smallest change:* add both names to `GitWiring.Removed`, so every child is stripped, with a test in 1b. Add M8: does a process
   started from the session's Bash, and its children, hold them? Say both in §3.
4. **The relay is plugin code running uncontained.** It runs in a process D201 point 4 covers, holding the inbox token and the
   terminal's keys (`SupervisorProof.cs:19-20`). It is read from a folder that agents and updates rewrite (§6.1 :322-326; D120's
   note). **Blocking.**
   *Smallest change:* the outside session runs a copy it pins: the tracked example at a reviewed commit, its hash checked before each
   start. Start it with only the two messaging variables and `PATH`. The relay opens only the plugin's endpoint and the inbox,
   which 1c's proof should show. The plugin's package ships no file the outside session runs, only that hash and a pointer.
5. **The relay trusts its input.** `subscribe.json` (§5.2 :290-292) can be replaced by any process of the same user, so the
   endpoint's frames reach the inbox unchecked, and §4.3's census never sees them (§8 :385-386). **Blocking.**
   *Smallest change:* the relay validates every field against the closed grammar before building a word. `kind` and `state` must
   come from closed sets, ids must match a pattern and be at most 64 characters, and a frame that fails is dropped and said on
   stderr. 1c adds a stand-in endpoint that sends an instruction-shaped `repository`, which the relay drops. Where Node can, the
   relay also checks the pipe server's process, as `InstanceClient` does.
6. **The endpoint names no ACL, owner or elevation.** With Node `net` a pipe gets Windows' default descriptor, and Node's only knobs
   widen it to every user. The install has run elevated (`docs/FIX-LOG.md:1506-1520`;
   `docs/2026-10-23-terminal-trust-design.md:253-255`), so a relay that is not elevated may be refused. `InstancePipe`'s rules are
   the exemplar: the user's SID alone, the network denied, owned by the user at either elevation
   (`docs/2026-10-16-terminal-rpc-design.md:145-152`). **Blocking.**
   *Smallest change:* add an M row that measures the plugin's endpoint from both elevations, and name the fallback. One option is the
   reverse direction: the relay serves a pipe of its own user, and the plugin connects ("the reverse works", terminal trust design
   :255). The other is a loopback socket gated by the key.
7. **A second process in the same data folder breaks delivery.** A *Try* on the installed plugin (`PluginTrials.cs:58-78`,
   `PluginsRoutes.cs:399`) and a `--share` loop (`Hooks.cs:705-708`) each start another process there. It rewrites
   `subscribe.json` and removes it when it stops. The live plugin goes unreachable until it restarts, and a relay may post the
   kit's sample event as real. **Blocking.**
   *Smallest change:* one address file per process (`subscribe/<pid>.json`), each writer removing only its own. Mark the kit's
   sample `work/event` frame with `run: "sample"`, which the relay drops. Start a plugin at `work/event` only in the lease's loop,
   as the work wire is started (`Hooks.cs:708`).
8. **A restart cannot be seen.** A new plugin process knows no `run` or `seq` (`Hooks.cs:147-150`), yet §5.2 writes `run` at the
   handshake (:290). Events with no listening process are neither told nor counted (`Hooks.cs:1196-1206`), so `seq` shows only
   queue drops. Not blocking.
   *Smallest change:* `initialize` gains `{ run, seq }` for a plugin at `work/event`, and §5.3 says a reconnect always reads the
   truth again.
9. **A hung plugin holds every event and every other plugin.** Reconcile replaces only dead channels (`Hooks.cs:725-729`), and the
   one teller awaits all plugins together (`Hooks.cs:1109`). Not blocking.
   *Smallest change:* after N late answers in a row at `work/event`, stop the plugin, as the work wire marks a plugin unsound
   (`WorkBounds.ExitsUnsound`). Or give each plugin its own queue and teller.
10. **The relay's default wakes too much, too often.** It takes every state, including `byPerson` stops, and every workspace
    (§6.1 :328). Each message is a typed prompt and may start a turn (§2.1 :148-149), as often as every 10 s. The attention watch's
    rule (`Attention.cs:252`) is not reused. Not blocking.
    *Smallest change:* the default is that rule plus the workspace the relay is started for, with a longer quiet window or a cap per
    hour. M4 adds the sustained per-sender rate at the relay's pace.
11. **The relay's lifetime is underspecified, and its reconnect is a poll.** "Its parent process has exited" (:336) names no
    process: a background start's parent may be a shell that exits at once. "Refuses twice" (:336) confuses a rate-limit refusal
    with a gone session. The 60 s reread of `subscribe.json` (:309-311) is a poll that §10 rejects (:407). Not blocking.
    *Smallest change:* pass the session's process id and watch that process. End only when the inbox is absent. Wait on a watch of
    the data folder instead of rereading.
12. **§6.2 and M7 skip the knowledge server's launcher.** The launcher's relay serves a build's home, not the install's, and swaps
    builds under a running session (`tools/knowledge-server.mjs:18-29`). The channel would read `subscribe.json` in the wrong home,
    and a swap would drop or duplicate it. Not blocking (1e is last and opt-in).
    *Smallest change:* `DAORIS_CHANNEL_EVENTS` names the install's home. M7 measures through `tools/knowledge-server.mjs` and across
    a build swap.
13. **Three frame details are unsaid.**
    - An intake's `repository` is `ask #<id>` (`DriverIntakes.cs:385`, `:416`, `:454`), not a registered name as §4.3 :254-255
      says.
    - `workspace` has no source (`SessionEnded.cs:14-16`). The attention watch resolves it (`Attention.cs:256`).
    - A conversation's ending (the chat runner's `onEnded`, `DriverLoop.cs:892-900`) never passes `EndingsAsync`.

    Not blocking.
    *Smallest change:* add a field `ask` beside `repository`, name the workspace's source, and say which kinds of session the point
    covers.
14. **Smaller claim errors** (see §1): `nodes` left out of §1.6's wakes (`Chain.cs:30`); the `work/land` row's `title`,
    `quest.title` and `acceptedBy` left out (`HookPoints.cs:139-144`); the teller exemplar starts whether or not anyone listens
    (`Hooks.cs:1096-1100`); and 1b's lanes (`HookPoints.cs:20-22`). Not blocking.
    *Smallest change:* correct the four lines.
15. **Order and sources in the rows.**
    - 1b and the rows after it do not wait on M3 or M6, so the point could be built with nothing to consume it.
    - 1d's quest kinds have no named source (`Driver.cs:917`), and a quest gone from the snapshot is not the same as a quest closed.
    - `person.asked` wakes the steward for a press only the person makes. The answer is the useful wake (TASKS.md:384).

    Not blocking.
    *Smallest change:* 1b waits until M3 or M6 says yes. Name the snapshot and its scope, and judge *closed* by the quest's state.
    Add `person.answered` beside `person.asked` or in its place.
16. **The manifest does not say the plugin serves an endpoint.** A manifest is what the person reads before switching a plugin on
    (`Hooks.cs:180-182`), and the page names no subscriber (D119's note). Not blocking.
    *Smallest change:* the gloss for `work/event` on the plugin's page, and the plugin's description, say that its events may be
    served to processes on this machine.

16 findings, 7 blocking (1-7).

**Verdict: another round.** The reading of §1 is mostly sound, and so is the overall approach: an observation point told off every
answering path, a plugin outside the driver, the session's own child posting, and measurement before anything depends on it. But
the two kinds the owner most needs are built from the wrong sources. A park is never a `session/ended` (F1), and "landed" is not
`work.ended` (F2). So the rows as ordered would not let the steward stop polling. The trust model also has four holes the steward
cannot close with a line each:

- the token may already be inside Daoris (F3);
- the relay is uncontained plugin code (F4);
- the relay accepts any input from an endpoint any same-user process can redirect (F5);
- the transport may be refused at the install's elevation (F6).

On top of those, a second plugin process breaks delivery (F7). Fixing F1, F4 and F6 changes where events come from, where the relay
comes from, and possibly which side serves the pipe. A round 2 that answers F1-F7 can be read narrowly, against §3-§6 and the rows
alone.

## Opus (foundation), round 2

The independent Opus 5.5 reader's foundation read of round 2 at `f360ab375`: 12 findings, 2 blocking; accept after named changes the steward can check without another read. Codex's read is owed, and by D191's note of 2026-10-24 this design is not accepted on one maker's read.

## EVENTHOOK1, round 2: the Opus foundation reader's read

Read at `d0e50cf24`, which holds `f360ab375`. Nothing the design cites has changed since `5d19f6b3a`. An empty
`git diff --stat 5d19f6b3a HEAD` over `src/Daoris.Desktop`, `src/Daoris.Service`, `tools/knowledge-server.mjs` and `examples/plugins`
shows only `Workflows/WorkflowRun.Read.cs` and its test. Design lines are `docs/2026-10-24-event-hook-design.md`. In the last column of
§0, a number is a finding of this round (§4).

### 0. Round 1's findings, checked against the code

| # | Round 1 | Round 2's answer | Checked against | Standing |
|---|---|---|---|---|
| 1 | A park is no `session/ended` | The session kinds come from `AttentionWatch.Observe` | `Attention.cs:230-258` says `Parked`, `QuestParked` and `Ended` once each, and `:224` makes the first look a baseline. Both loops call it: `DriverLoop.cs:916`, and `Host/Program.cs:972` in watch mode only. | **Taken**, and it holds. The frame's `quest` and `adapter` are not on the watch's events (→ 6). |
| 2 | "Landed" mapped to `work.ended` | `work.landed` at `AutoLander.Tried` | `AutoLander.cs:648-672` keeps every try with its code. `landed` and `advanced` are at `AutoLanding.cs:39-46`. It is reached only past `HeldHere` (`Driver.cs:1405`), and `Context.Hooks` is in scope there (`:1424`). | **Taken**, and it holds |
| 3 | The inbox token reaches Daoris | Join `GitWiring.Removed`, and strip at each executable's entry | `GitWiring.Hand` runs in every `Tools.Hand` (`Tools.cs:679`, `:693`) and in a tree's git (`WorkingTree.cs:973`). `App/Program.cs:34` and `Host/Program.cs:345` are the entry seams. | **Taken**, and it holds |
| 4 | The relay is plugin code, uncontained | The relay becomes Daoris's own verb | No plugin folder or package carries it, which holds. But a verb `VerbLeases` does not class holds the home's lease for its run, and `daoris-driver` runs from `app/`. | **Taken**, but the relay's new home opens new holes (→ 2) |
| 5 | The relay trusts its input | No address file, a check of the client's ancestry, and the grammar | Step 3 trusts `run/home.lease`, a file it did not write. It also admits every agent's harness and the plugin's agent-rewritable code. The id pattern admits words. | **Open** (→ 1) |
| 6 | The endpoint names no ACL, owner or elevation | Reverse the direction, use `InstancePipe.Security`, measure with M9 and M10 | `InstancePipe.cs:400-411` is the right rule, but it lives in the modules, which `daoris-driver` does not build against | **Taken**; a lane to correct (→ 7) |
| 7 | A second process breaks delivery | A file per pid, a `sample` run, the lease's loop only | The `wanted` rule at `Hooks.cs:705-708` needs the special case the design names, and the trial's handshake can carry `sample` | **Taken**. `Tell` must check the lease too (→ 8). |
| 8 | A restart cannot be seen | `initialize` gains `events: { run, seq }` | The handshake at `Hooks.cs:147-150` can carry it | **Taken** at the plugin. Nothing passes a gap or a new run on to the session (→ 3). |
| 9 | A hung plugin holds everything | A queue and teller per plugin; three late answers replace it | `Hooks.cs:1109` is the shared await this removes | **Taken**. The replacement has no back-off (→ 9). |
| 10 | The default wakes too much | The watch's rule, four kinds, one workspace, 30 s batches, 20 messages an hour | `Attention.cs:252` leaves out `ByPerson`, `Limit` and `NotOffered` | **Taken**, and it holds |
| 11 | The lifetime is loose, and the reconnect is a poll | `--session-pid`, and a folder watch | No reread on a clock is left | **Taken**. A closed connection is never reopened (→ 4). |
| 12 | §6.2 skips the launcher | `kinds@home`; M7 through the launcher and across a build swap | `tools/knowledge-server.mjs:18-29` | **Taken** for M7. 1e cannot reach the grammar or the checks (→ 7). |
| 13 | Three frame details are unsaid | An `ask` field, the workspace's source, conversations left out | `DriverIntakes.cs:385`, `:416`, `:454`; `Attention.cs:256`; `DriverLoop.cs:892-900` | **Taken**, and it holds |
| 14 | Smaller claim errors | Corrected | `nodes` is in §1.8. §1.9's land row matches `HookPoints.cs:132-147`. §1.2 says the state teller starts whatever listens (`Hooks.cs:1099`). The lanes are named. | **Taken**, and it holds |
| 15 | Order and sources in the rows | 1b waits on M3 or M6; the snapshot is named; `person.answered` | `/api/quests` is the open list (`Quests.cs:3990`) | 1b's gate and `person.answered` are taken. `quest.closed` has no source as named (→ 10). |
| 16 | The manifest does not say the plugin serves | The description and the gloss | D119's note | **Taken**, and it holds |

### 1. Claims about the code

Most of §1 holds at the lines it names:

- **The plugin system:** `HookPoints` and `HookFrames`; `HookSet`'s reconcile, its `wanted` rule, `Tell`, the teller and `Listening`; `HookPeer`'s patience and its `late` mark (`Hooks.cs:412-415`).
- **A plugin's process:** `HookProcess`'s start, its environment and its start through `AgentStarts`; `GitWiring.Removed` and `SessionTokens`; `HolderKey.Strip` and `HolderKey.Take`.
- **A second process:** the trial in the same data folder.
- **Where the events come from:** the park conclusions in `Driver.cs`, `DriverIntakes.cs` and `DriverContinuations.cs`; the attention watch and both of its callers; `AutoLander.Tried`; `EndJudgement`'s `Handed`; `RefWatch`'s `work.held`; `ChainEvents`.
- **Pipes and the lease:** `InstancePipe.Security`'s rule; `InstanceClient`'s owner and server checks; `HomeLease`'s holder and its start.
- **The window's pipeline and the knowledge server:** `StatePublisher`, NOBLOCK1's `ByEvent`, the notices record, and the MCP probe fallback.

These claims are wrong:

- **The launcher in §6.1 step 3 (:421-422) is the wrong program.** `Daoris.Desktop.Launcher` is the install root's executable. It starts the application from `app/`
  and exits (`Launcher/Program.cs:8-12`), so it is never a plugin's parent. A plugin's launcher is `daoris-piece-start.exe`
  (`PieceLauncher.Program`, `Driver/Conversations/Partners/PieceLauncher.cs:29`; project `Daoris.Desktop.PieceStart`).
- **"A process an agent starts is a grandchild of the launcher at least" (:423) misses the agent itself.** The sentence is true of what an agent spawns, but
  the agent's harness is a direct child of the same launcher under the same lease holder: `AgentStarts` is "the one route every agent,
  plugin process and piece starts by" (`Platform/AgentStarts.cs:5-11`). Step 3 therefore admits every agent Daoris runs, as well as the plugin.
- **"The relay reads no address and trusts no file it did not write" (:445-446) is wrong twice.**
  - Step 3 judges the client against the holder that
    `run/home.lease` names (`HomeLease.cs:53-64`, `:120`), a file in the home that any process of the user can rewrite.
  - The plugin's code
    lives in a folder the design itself says any agent can rewrite (§3 :246-249). The relay is then fed through the process step 3 trusts.
- **`InstancePipe.Security` (:416) is out of the verb's reach.** It is in the modules: namespace `Daoris.Desktop` (`Modules/Platform/InstancePipe.cs:13`, `:400`).
  `daoris-driver` builds against the driver alone (`Daoris.Desktop.Driver.Host.csproj:14`).
- **§6.2's "the same checks, the same grammar" (:460-461) cannot be built as written.** The knowledge server builds against `Daoris.Service.Core` alone
  (`Daoris.Service.Mcp.csproj:14`), so it has no `WorkEventGrammar`, `HomeLease` or pipe security to call.
- **"Removes `DAORIS_SUPERVISOR_KEY` … unread" (:411) is not so.** Every `daoris-driver` verb takes the key into memory at entry (`Host/Program.cs:345`,
  `HolderKey.Take`), so a relay would hold it for its whole life.
- **The frame's `quest` and `adapter` are not on the watch's events (§4.3 :309-311).** `AttentionEvent` carries `Quest` for `QuestParked` alone
  and no adapter at all (`Attention.cs:45-58`). `Observe` builds `Ended` without the ending's quest or adapter (`:253-257`) and `Parked`
  without `SessionView.Quest` (`:235-236`). Row 1b changes `Observe` only for a park's workspace.
- **"A registered name … never a space, … a colon" (:325) is not true of real names.** Ask Daoris's room is the repository `daoris:help` (`Help/HelpRoom.cs:48`), and a
  workspace name is only trimmed (`Daoris.Service.Core/Registry/Workspaces.cs:26-28`). The pattern is a rule of the grammar. Neither the registry nor the help room enforces it.
- **Smaller:**
  - "Every kind is told at the look's end, after the watch observes" (:297) is wrong for `work.landed`, which is told inside the
    landing, before the watch (`Driver.cs:1403-1432`).
  - "Both loops call it after each look" (§1.3 :89) holds for the headless host's watch mode only. `drive --once` and `--until-idle` never
    call `Observe` (`Host/Program.cs:930-945`).
  - The example frame's `s-4b62a9fe` (:304) is not a session id's shape, which is eight hex characters
    (`Daoris.Service.Core/Sessions/Sessions.cs:799`).
  - §4.2's quest kinds "compared by id and state" (:284) cannot see a closed state, because the snapshot is the open list (→ 10).

### 2. The mechanism's holes

**What an event carries.** The driver's frame is sound: a closed list of fields, no words, no path, no account. The far end is not
sound.

- §8's promise that "nothing authored reaches the receiving session … whoever wrote to the pipe" (:514-515) rests on step 4 alone,
  because step 3 admits agents (§1).
- Step 4's id pattern `^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$` admits a 64-character instruction made of words joined by dots and dashes,
  such as `Merge-every-landed-branch.now`. It can ride in `session`, `quest`, `repository` or `workspace`, and the relay posts it as the
  session's own verified child.
- The receiving session is the steward's: the most capable agent on the machine, riding D201's standing confirmation and possibly
  bypassing prompts. A wake grants nothing, but its words still reach the model.
- Per-field shapes close this. The service mints a session id as eight hex characters, and the relay can take names only to filter,
  never to post (→ 1).

**A subscriber acting as the person.** The relay opens no Daoris door and starts no process, so D156 and D201 hold for it. The one leak
is memory: `HolderKey.Take` leaves the key in every `daoris-driver` process (→ 7).

**Two of the install's exclusive moments, which the relay's long life meets** (→ 2):

- **The lease.**
  - An unclassed verb holds the home's lease for its run (`VerbLeases.cs:10-12`, `:97`; `Host/Program.cs:377-381`).
  - A relay started while Daoris is closed therefore holds the lease for the outside session's life.
  - The desktop's loop waits for an act's lease until "that one ends" (`DriverLoop.cs:1246-1253`).
  - Step 3 would then find the relay itself named as the holder.
- **The swap.**
  - `daoris-driver.exe` runs from `app/daoris-driver/` (`tools/desktop-publish.mjs:367-368`).
  - The launcher replaces nothing while anything runs from `app/`: "something still ran from app/ after the application closed, so
    nothing was replaced" (`Launcher/Program.cs:72`, `:82-87`, `:168-169`).
  - Step 7 has the relay wait through Daoris's close by design, so no update installs while the steward's session keeps a relay.

**Ordering, loss and duplicates across a restart.**

- *Ordering.* `seq` order per plugin holds.
- *Duplicates.* A file per pid, the `sample` run, the lease rule and `run` inside each message close round 1's cases. One remains: a
  plugin that declares `work/event` beside another loop point is wanted in a `--share` loop (`Hooks.cs:705-708`). The table in §4.2 has
  both loops tell, so that plugin would hear every event twice unless `Tell` checks the lease (→ 8).
- *Loss.*
  - The watch's first look is a baseline (`Attention.cs:224`), so a park made while Daoris was closed is never a `session.parked`. Only
    the new `run` shows it.
  - §4.4 asks "a subscriber that sees a gap, a new run, or reconnects" to read the truth again (:342-343). The subscriber here is the
    relay, which reads nothing (step 1), and step 5 posts only the four kinds. So nothing tells the steward to look again (→ 3).
  - A connection the relay refuses or closes is never reopened. Three cases end it:
    - a second client;
    - three bad frames;
    - a slow mark.

    The plugin reopens a connection only "when its file is next changed" (:381), the relay writes its file once (:413), and a pipe that
    did not answer is "left" (:370) (→ 4).
  - Real values outside the pattern (`daoris:help`, a workspace with a space) make frames fail the grammar at run time. The design does
    not say what the driver does with one. The relay's rule "three in a row close the connection" (:427) then turns them into lasting
    deafness (→ 5).

**A slow or gone subscriber, or a slow plugin.**

- The driver is never held: the plugin answers at once, and each plugin has its own teller.
- A plugin late at every look is stopped and started again at every look. The work wire's back-off and its *unsound* rule
  (`WorkBounds.cs:21-27`) are not reused (→ 9).

**A plugin that floods.** Bounded at every hop: 256 waiting per plugin, 64 per subscriber, frames of at most 4 KB, and at most
20 messages an hour, with the overflow counted, never kept.

**Measured before depending.**

- M1-M10 are the right list, and M8-M10 answer round 1.
- 1c's gate leaves out M5 (the relay's lifetime and the session's pid, which `--session-pid` rests on). It also leaves out M3 for its
  inbox adapter: with M3 *no* and M6 *yes*, 1c builds an adapter nothing uses.
- 1e reuses 1c's pipe, checks and grammar (§6.2 :460) but is gated on neither (→ 11).
- One mechanism is left unconsidered: a background command whose end wakes the session. The row's own steward re-arms its shell loop
  every half hour (`TASKS.md:459`), so this machine already relies on that wake. A relay that ends on its first batch would let 1b and
  1c stop the poll without the inbox's undocumented line, its own-child class or its rates (M2-M4) (→ 11).

**The plugin system's own limits.**

- **Its process.** A start that cannot be contained is made all the same: with no launcher laid out or a job refused, the plugin is
  the driver's direct child (`AgentJobs.cs:46-49`, `:119-123`). Step 3 then refuses the real plugin, and nothing says where. A test's
  home is such a case (`PieceStarts.cs:16-20`) (→ 7, → 12).
- **Its permissions.** The manifest's gloss is right.
- **Its hooks' timing.** A late plugin is restarted at a look, not between looks. `work.landed` is told inside the look.

### 3. The rows

- **EVENTHOOK1a** is rightly first and can be built alone. Add M5 to 1c's gate. Name the background-command wake beside the inbox and
  the channel in §2.3, as already in use (→ 11).
- **EVENTHOOK1b** can be built alone, in its lanes (the host's `Program.cs` among them). Its proofs prove the point. It needs three more:
  - `Observe` carrying quest and adapter, with a proof that checks them (→ 6);
  - `Tell` at the point telling nothing where the set does not hold the lease, with proof 3 extended to a plugin that also declares a
    loop point (→ 8);
  - a value outside the grammar never sent (→ 5).

  With 1c, 1b gives the owner the most.
- **EVENTHOOK1c** has three problems:
  - **What it builds.** Its lanes need the modules (or `InstancePipe.Security` moved into the driver). The verb must be classed in
    `VerbLeases`, and it must not run from `app/` across a swap (→ 2, → 7).
  - **What its proof proves.** "A client that is not the launcher's child is refused" proves that strangers are refused, not what the
    design claims of agents. It should show what step 3 does not refuse, and that step 4 drops an allowed-character word-id (→ 1). Its
    Process-lane class must lay out the built launcher and take the home's lease, as `AgentStartsProcessTests` and
    `PartnerProcessTests` lay the launcher out. Otherwise the real step 3 refuses the real plugin, or a seam skips step 3 (→ 12).
  - **What it misses.** A reconnect, and the line posted on a gap (→ 3, → 4).
- **EVENTHOOK1d** cannot build `quest.closed` from the named source (→ 10). `person.answered` and the census extension stand.
- **EVENTHOOK1e** must be gated on 1c, and its lane needs a shared home for the grammar and the checks, or a twin with a census (→ 7).
  It also writes into the install's home from a workspace build, which `tools/knowledge-server.mjs:19-21` keeps away from that home's
  store. Say why a plugin's data folder is not that store.
- **EVENTHOOK1f** stands.

### 4. Findings

1. **Step 3 admits agents, and step 4's ids carry words.** §6.1 step 3 (:420-425), "What Windows does not draw" (:442-446), the
   grammar's bounds (§4.3 :324-326) and §8 (:514-515) are wrong. Every agent's harness is a launcher's direct child under the lease
   holder (`AgentStarts.cs:5-11`). The plugin's code is rewritable by agents (§3 :246-249). The holder is read from a file
   (`HomeLease.cs:120`). An id of up to 64 characters of `[A-Za-z0-9._-]` is a sentence. So an agent can put its words, as the
   steward's own verified child, into the most capable session on the machine. That is the very hole F5 named. **Blocking.**
   *Smallest change:*
   - Give each generated field its minted shape: a session is eight hex characters (`Sessions.cs:799`); the quest, ask, confirmation
     and run are as minted.
   - The relay uses `repository` and `workspace` only to filter, and never posts them. The steward reads names through its own doors.
   - §6.1 says that step 3 refuses strangers (another user's process, a trial, a Daoris that does not hold the home), never an agent,
     and drops "trusts no file it did not write".
   - 1c's proof adds a frame with a word-shaped id of allowed characters, which is dropped.
2. **The relay's long life holds the home's lease and stops every update.** The verb is unclassed, so it holds the lease for its run
   (`VerbLeases.cs:10-12`, `:97`). Started while Daoris is closed, it keeps the desktop's loop waiting (`DriverLoop.cs:1246-1253`). It
   runs from `app/daoris-driver/` (`tools/desktop-publish.mjs:367-368`), and the launcher swaps nothing while anything runs from `app/`
   (`Launcher/Program.cs:72`, `:82-87`, `:168-169`). Step 7's wait through Daoris's close is exactly when the swap needs it gone.
   **Blocking.**
   *Smallest change:*
   - Class `events relay` with the verbs that hold nothing (beside `chat` and `run-heavy`), with its reason: its one write is its own
     file in a plugin's data folder.
   - Run it from a copy of its build outside `app/`, named by the build. The knowledge connector's home copy is the precedent
     (`KnowledgeConnector.cs:153`), and CONNECTOR1 is its warning (`:127-133`).
   - Or end the relay when the application closes for an update, with one fixed line posted, and say how it starts again without a poll.
   - 1c proves that a staged update swaps while a relay runs, and that the desktop takes the lease from a relay started first.
3. **Nothing tells the steward to read the truth again.** §4.4 (:341-343) gives the duty to "a subscriber". The relay reads nothing
   (§6.1 step 1), and step 5 posts only four kinds. After a restart the watch's baseline (`Attention.cs:224`) swallows what happened
   meanwhile. Not blocking.
   *Smallest change:* the relay posts one fixed line, inside its rate bound, for a new `run`, a `missed` above 0 or a `seq` gap, for
   example `Daoris: events may have been missed; read the sessions again [r-… #n]`. 1c proves it.
4. **A closed or refused connection is never reopened.** §5.2 leaves a pipe that did not answer (:369-371). §5.3 reopens only on a
   changed file (:380-381). The relay writes its file once (:413) and closes after three bad frames (:427). A slow mark or a refusal
   while an old connection drains leaves the subscriber deaf until the plugin restarts. Not blocking.
   *Smallest change:* the relay rewrites its file each time a connection ends, which the plugin's watch sees. Or the plugin retries a
   left file at its next event. The plugin takes only a `pipe` of the relay's name shape.
5. **The grammar meets real values it rejects.** `daoris:help` (`HelpRoom.cs:48`) and workspace names, which are only trimmed
   (`Workspaces.cs:26-28`), fall outside :325. The design says nothing of a run-time frame that fails, and the relay's three-strikes
   rule turns three such frames into lost delivery. Not blocking.
   *Smallest change:* `HookFrames.Event` leaves out a field whose value fails, or the event itself, counted in `missed`, and never
   sends it. Say whether a help-room park is a `session.parked`. 1b's census adds a real `daoris:help` value.
6. **The frame's fields are not on the watch's events.** `quest` and `adapter` for `session.ended`, and `quest` for `session.parked`,
   are missing from `AttentionEvent` (`Attention.cs:45-58`, `:235-236`, `:253-257`). The `--once` and `--until-idle` runs never observe
   (`Host/Program.cs:930-945`). The example's session id is not the minted shape. Not blocking.
   *Smallest change:* `Observe` carries the ending's quest and adapter and the park's `SessionView.Quest`, and 1b's proof 2 checks
   them. Say that a headless one-look run tells `work.landed` alone. Correct the example.
7. **Claims and lanes in §6.** Not blocking.
   - The launcher is `daoris-piece-start.exe`, not `Daoris.Desktop.Launcher`.
   - `InstancePipe.Security` moves into the driver beside `InstanceClient`, and 1c's lanes add the modules.
   - The knowledge server cannot reach the grammar, the lease or the pipe security (`Daoris.Service.Mcp.csproj:14`), so 1e needs a
     shared home or a twin held by a census.
   - The relay drops what `HolderKey.Take` took (`Host/Program.cs:345`), or the host takes it only for `sessions supervise`.
   - §4.2's latency line excepts `work.landed`.
   - An uncontained plugin (`AgentJobs.cs:46-49`, `:119-123`) is refused by step 3, and the design should say where that is said.

   *Smallest change:* correct these lines and lanes.
8. **The lease rule is only a rule of which plugins are wanted.** §4.1 (:264-267) puts it there. A plugin that declares `work/event`
   beside a loop point is wanted in a `--share` loop (`Hooks.cs:705-708`), and both loops tell after `Observe` (§4.2 :280). Not blocking.
   *Smallest change:* `Tell` at `work/event` tells nothing where `_holdsHome()` is false. 1b's proof 3 includes such a plugin.
9. **A plugin late at every look is restarted at every look** (§4.4 :338-340). Not blocking.
   *Smallest change:* count a late replacement as an unasked exit under the work wire's back-off and *unsound* rule
   (`WorkBounds.cs:21-27`), logged as `plugin.stopped` with its reason, beside the per-call `late` line that already exists.
10. **`quest.closed` has no source in the snapshot.** `SnapshotAsync` reads `/api/quests` (`ServiceClient.cs:139`), which lists only
    quests that are Open, Taken or held (`Quests.cs:3990`). A quest that became Done or Declined leaves it without the state §4.2 (:284)
    judges it by. Not blocking (1d).
    *Smallest change:* in a look where a quest left, read `/api/quests?includeClosed=true` for those alone, as the snapshot already
    does where words wait (`ServiceClient.cs:154-158`), and judge by that state.
11. **The gates, and the cheaper first wake.** 1c is not gated on M5, nor its inbox adapter on M3. 1e reuses 1c without depending on
    it. The background-command wake the steward already relies on (`TASKS.md:459`) is not considered. Not blocking.
    *Smallest change:* gate 1c on M5 and its inbox adapter on M3, and 1e on 1c. Add `events relay --once`, which prints the fixed lines
    and exits on its first batch, as 1c's first adapter. It depends on none of M2-M4, and the inbox stays the upgrade. Say it in §2.3
    and §9.
12. **1c's Process-lane proof cannot pass honestly as written.** In a test's home `HookProcess` starts the plugin uncontained
    (`PieceStarts.cs:16-20`; `AgentJobs.cs:119-123`), and the real step 3 refuses that. "Opens nothing but its pipe and a stand-in
    inbox" also omits the lease it reads and the file it writes. Not blocking.
    *Smallest change:* the class lays out the built launcher and takes the home's lease, as `AgentStartsProcessTests` and
    `PartnerProcessTests` lay the launcher out, and the claim names the lease read and the file write.

12 findings, 2 blocking (1 and 2).

**Verdict: accept after named changes the steward can check without another read.** Round 2 answers round 1 well. The events now come
from where parks and landings are actually seen. The token is stripped at every seam. The relay is out of the plugin's folder. The pipe
is served the right way round. A second process can no longer silence the first. Two blocking holes remain, and each has a narrow,
checkable change. First, step 3 admits every agent and step 4's ids can carry words, so §8's promise needs minted per-field shapes and
names kept out of the posted words, with §6.1 saying plainly what step 3 does not refuse (finding 1). Second, the relay's long life
must neither hold the home's lease nor stop the swap: class it in `VerbLeases`, run it from outside `app/` or end it at an update, and
prove both in 1c (finding 2). Findings 3-12 are corrections the steward can make line by line. Of them, 3, 4 and 5 decide whether the
steward actually stops polling, and 11's `--once` adapter is the shortest way to give the owner that.

## Codex, round 2

Codex's read of round 2 with its named changes at `972b17577`: 21 findings, 16 blocking; another round. The steward ran it read-only on main from outside the install, and its links are made repository-relative.

Read-only review; nothing changed and no probes run. References use the design’s `Driver/`, `Host/`, `Modules/` and `Mcp/` prefixes defined at `docs/2026-10-24-event-hook-design.md:36`.

### 1. Wrong claims about existing code

1. **Blocking — the steward’s terminal cannot start the proposed verb.** `Host/Program.cs:368` runs the role-terminal gate before dispatch, and `Driver/Sessions/Questions/SupervisorProof.cs:97–105` allows neither `events relay` nor `--where`. Dropping the captured key inside the relay comes too late; EVENTHOOK1c must explicitly allow these exact read-only operations while preserving the terminal’s role restriction.

2. **Non-blocking — two descriptions overstate their sources.** The proposed closed-quest request retrieves the whole closed-inclusive list, not “those quests alone” (`Driver/Service/ServiceClient.cs:157`; `Daoris.Service.Core/Quests/Quests.cs:3987`); filtering would happen afterward. Also, a landing runs beside the look (`Driver/Loop/Driver.cs:1428–1438`), so the design’s `work.landed` “before the watch” statement at design:361 is not a guaranteed ordering.

### 2. What leaves Daoris, and what can act

1. **Blocking — names already leave before the relay removes them.** Design:378–405 includes repository/workspace names and quest `to`/`from`, permits arbitrary non-control characters, and sends frames unchanged to subscribers (design:475); a private name, path, credential or person’s words stored as a name therefore reaches subscriber code. Subscriber-selected filters are no authorization boundary (design:455–471): define who may subscribe to which scope, and enforce the outbound projection before handing it to that subscriber.

2. **Blocking — the same-user threat claim remains too strong.** Design:568–573 says validation protects against every same-user agent, although design:513 places the executable copy in the same writable home and acknowledges agents can rewrite files. Minted hex shapes constrain presentation; they authenticate neither an event nor its referenced record, so narrow the promise to untrusted frames reaching an intact, trusted relay and require independent authorization for every resulting action.

3. **Blocking — filesystem custody is unspecified.** Design:455 and :513–516 prescribe writes and recursive copy cleanup without addressing redirected directories, atomic copy publication, concurrent `--where` calls or partial deletion of an active build. Require containment against junction/symlink redirection and explicit build-use coordination; an executable being held open does not establish that every dependency and directory survives cleanup.

4. **Non-blocking — §8 correctly adds no direct answer-to-action path.** The existing plugin wire ignores plugin-originated requests (`Driver/Plugins/Hooks.cs:532–534`), and the proposed relay opens no mutation door (design:524–525). The receiving agent can subsequently act through its existing permissions, so a wake must never count as confirmation, approval or evidence that the advertised fact is true.

### 3. Delivery

1. **Blocking — the default workspace filter cannot handle its own events.** `quest.parked` deliberately lacks a workspace (design:379, :392), yet the relay defaults to one workspace (design:550–551); `quest.published`, `quest.closed` and `person.answered` also lack it. `QuestPark` really has no workspace (`Driver/Trees/SessionGroups.cs:563–569`), so specify a source-side scope for every kind and fail closed when it is unknown.

2. **Blocking — restart recovery still depends on another event arriving.** The watch suppresses its first look (`Driver/Quests/Domain/Attention.cs:224`), batches disappear if the relay dies, and refused inbox batches are discarded (design:563). Specify an independently deliverable reconciliation marker on startup/reconnection and after terminal loss, including when no session was running; for `--once --since`, a hello watermark greater than the last delivered watermark must trigger reconciliation even when it is exactly the next sequence.

3. **Blocking — filtered sequences are indistinguishable from loss.** The sequence is global (design:416), filtering happens afterward (:470), and the relay treats skipped sequences as missed events (:558), so ordinary filtering repeatedly triggers broad rereads. Define separate source progress and subscriber delivery progress, suppression of repeated/older frames, and concurrent enqueue ordering; test two subscribers with different filters and a reconnect while events are in flight.

4. **Blocking — discovery and resource bounds are incomplete.** Reading the folder and *then* watching it leaves an unwatched interval (design:459), while watcher failure, initial connection failure and repeated slow reconnects lack a complete recovery rule. Establish the watch before scanning, reconcile on watcher uncertainty, bound reconnect attempts, and cap subscriber count, handshake time, batch bytes and batch entries—the 64-entry transport queue does not bound the relay’s 30-second accumulation.

### 4. Decided rules

1. **Blocking — participant visibility has no corresponding subscription rule.** D173:11–19 requires explicit visibility and participant-scoped permissions, but its EVENTHOOK1 amendment at D173:5409–5417 asserts safe export without defining subscriber grants. D182:18–23 explicitly preserves invalidations for plugins, so using wakes here is compatible with D182; that exemption does not resolve disclosure across scopes.

2. **Non-blocking — logging and home-layout work is missing from the rows.** D119:250 adds `event.unsent`, but the machine-log catalogue at `docs/2026-09-30-machine-log-design.md:128–135` and `Driver/Plugins/Domain/PluginEvents.cs:42–78` do not yet describe the new event and stop reason. Assign their catalogue/check updates explicitly, and use the existing home entry resolver (`Driver/Platform/DaorisHome.cs:119`, :400) rather than prescribing a second physical `bin/` layout.

### 5. EVENTHOOK1a–f and their order

1. **Blocking — 1a’s decision rule contradicts the dependency graph.** Design:646–649 permits progress when the inbox or channel works but M5 fails, while 1c requires M5 and 1e requires 1c (design:678, :680). Define adapter-specific prerequisites so a successful channel or inbox measurement has a buildable path without a successful background-command wake.

2. **Blocking — 1b lacks a complete frame contract.** Design:397–405 does not classify `adapter`, `to` and `from`, and requires an ending’s `adapter` although its source is nullable (`Driver/Sessions/Domain/SessionEnded.cs:16`). Settle every field’s type, scope, optionality and rejection behavior, then add concurrent-producer, listener-attachment and missing-final-event proofs to design:677.

3. **Blocking — 1c needs a complete host-entry and copied-build contract.** Besides the role gate, an ordinary dispatch passes `MachineLog.Open`, which prunes files (`Host/Program.cs:445`; `Driver/Platform/MachineLog.cs:80–85`), contradicting the claimed subscriber-file-only write surface. Route it explicitly before that machinery, specify how the copied executable retains its home/install identity—automatic discovery requires ancestry under `app/` (`Driver/Platform/DaorisHome.cs:62–80`)—and prove copying, cleanup and swapping concurrently.

4. **Blocking — 1d still misses legitimate closures.** A closed quest can remain in the “open” list because the query includes `held = 1` (`Daoris.Service.Core/Quests/Quests.cs:3990`), so watching only departures misses its transition to `Done`. A publication and closure between looks are also invisible, while a reopened quest can look newly published; define these as snapshot observations or derive the named lifecycle events from recorded transitions.

5. **Blocking for 1e — delivery does not establish usable reconciliation.** The workspace launcher deliberately substitutes its own home (`tools/knowledge-server.mjs:134–143`), so its connector tools cannot automatically reread the install whose channel supplied the wake. Specify the install-scoped read door and notification handoff during build replacement; the existing launcher drops output from a non-serving server (`tools/knowledge-server.mjs:322–327`).

6. **Non-blocking — 1f has the correct ownership boundary.** A quest after 1c respects D120:24–31 and the proposed amendment at D120:148–156. Include the accepted protocol version and contract fixtures in that request so the packaged implementation remains compatible with the example.

### 6. Smaller shape with the same outcome

1. **Non-blocking — ship only the measured `--once` path initially.** Keep one observation point, the four steward kinds, explicit scope, the plugin and a minimal read-only waiter that prints fixed lines plus a reconciliation watermark. Defer inbox posting, channels and the additional kinds; this stops the steward’s polling while removing token custody and substantially reducing lifetime, batching and compatibility work (design:578–588).

### Comparison with the earlier reads

1. **Blocking disagreement — the driver frame was not already safe.** The round-1 read at `docs/2026-10-24-event-hook-reads.md:50` and round-2 read at :332 call it bounded and path-free, but current design:402 allows path-shaped names and forwards them to subscriber code. Removing names only from the final model-facing sentence closes a different boundary.

2. **Blocking disagreement — delivery corrections need another design read.** The round-2 conclusion at reads:515–522 treats the remaining recovery changes as line-by-line fixes, and :389 calls every hop bounded. Filtering, final-loss recovery, workspace-less kinds and unbounded accumulation require protocol choices and adversarial lifecycle proofs, not merely wording corrections.

**Verdict: another round.**

## Opus (foundation), round 3

The independent Opus 5.5 reader's foundation read of round 3 at `c685459f4`: 12 findings, 4 blocking; accept after named changes.

### 0. Codex's round-2 findings, checked against the code

Read at `c685459f4`. Between `972b17577` and `c685459f4` only documents and `TASKS.md` changed, so the code is what Codex read. Design lines are `docs/2026-10-24-event-hook-design.md`, and the path prefixes are the design's.

| # | Codex's finding | Round 3's answer | Checked against | Standing |
|---|---|---|---|---|
| **C1.1** | **The role's terminal cannot start the verb** | `RoleTerminal.Runs` gains `events relay`, `--where` and `--once` alike | The gate is `SupervisorProof.cs:97-106`, at `Host/Program.cs:368`. The holder's harness also has its own allowlist: `Modules/Platform/HolderTerminal.cs:42-49` (D209 §2.2, "stated whole"). `HolderTerminalTests.cs:88-95` and the walk at `HolderWalk.cs:65` hold that list. D209.md:77-78 says "every alone path refuses `role-terminal`". | **Open** (→ 1). Only Daoris's half of the gate is opened. `--where` is an alone path, and D209 is not amended. |
| C1.2 | Two descriptions overstate their sources | §1.7b and §4.2 corrected | `ServiceClient.cs:157`; `Quests.cs:3990`; `Driver.cs:1424-1438` | **Taken**, and it holds |
| **C2.1** | **Names leave before the relay removes them** | No name in any frame. The projection is made in `HookFrames.Event`. | `Workspaces.cs:26-28`; design §4.3 | **Taken**, and it holds for frames. The plugin's first line is another matter (→ 2). |
| **C2.2** | **The same-user claim is too strong** | The promise is narrowed to "an intact relay" | design :672-679, :755-760 | **Taken in part**. It still says a false wake is the worst a process can do. A same-user process can also silence the relay, and the first line and stderr carry unchecked words (→ 2, 3, 4). |
| **C2.3** | **Filesystem custody is unspecified** | A staged copy moved in with no overwrite, an `in-use` file, no reparse point followed, `--where` under the lease | `DaorisHome.cs:119-127`, `:400`; `VerbLeases.cs:97`; `Host/Program.cs:388-412` | **Taken**, and it holds. It rests on Windows semantics that 1c proves. |
| C2.4 | No path from an answer to an action | A wake is never a confirmation | `Hooks.cs:532-534` | **Taken**, and it holds |
| **C3.1** | **The workspace filter cannot handle its own events** | No filter. One scope, the home. | `SessionGroups.cs:563-570`; `Attention.cs:256` | **Taken**, by another way, and it holds |
| **C3.2** | **Restart recovery waits on another event** | The plugin's first line settles a loss at once | `Attention.cs:224` | **Taken** where a plugin connects. **Open** where none ever does (→ 4). |
| **C3.3** | **Filtered sequences look like loss** | The plugin passes everything and the relay filters. `Tell` mints under one lock. | design §4.4, §5.2, step 5 | **Taken** within one relay. Across relays, `--since` loses the filtered watermark (→ 7). |
| **C3.4** | **Discovery and resource bounds are incomplete** | The watch is set before the read, a failed watch is set again, 8 subscribers, a 5 s first line, 4 KB, 50/52 lines | design §5.2, §5.3, steps 2, 4, 5 | **Taken**, and it holds |
| **C4.1** | **No subscription rule for participant visibility** | One scope, granted by the person's switch, at ADR-B's opaque level | D173:11-19; `PipeCensus.cs:237`, `:286` (`PLUGIN_ACTION` and `PLUGIN_ADD` are `Person`); `VerbLeases.cs:92` | **Taken**. The "grant" is the person's only while Daoris runs (→ 11). |
| C4.2 | Logging and home-layout work is missing | `event.unsent` and `late` added to the catalogue; `DaorisHome.Entry` used | `machine-log-design.md:128-135`; `PluginEvents.cs:71` (`late` exists) | **Taken**, and it holds |
| **C5.1** | **1a's decision rule contradicts the graph** | Each adapter has its own prerequisites | design :729-737 | **Taken**, and it holds. §6.3 is still caught behind M5 (→ 9). |
| **C5.2** | **1b lacks a frame contract** | §4.3's table and three concurrency proofs | `SessionEnded.cs:14-16`; `Attention.cs:45-58`, `:253-257` | **Taken**, and it holds |
| **C5.3** | **1c needs a host-entry and copied-build contract** | Routed before the machine log; `--home`; copy, removal and swap proved together | `Host/Program.cs:386`, `:396-445`; `MachineLog.cs:80-85`; `DaorisHome.cs:62-103` | **Taken**, and it holds, apart from the role's terminal (→ 1) and the process reader's lane (→ 8) |
| **C5.4** | **1d misses legitimate closures** | The quest kinds wait for a round that reads recorded operations | `Quests.cs:3990` (`held = 1`) | **Deferred**, with a reason that holds |
| **C5.5** | **1e has no usable reconciliation** | Carried as 1e's requirement | `tools/knowledge-server.mjs:134-143`, `:322-327` | **Deferred**, with a reason that holds |
| C5.6 | 1f's boundary is right; include versions and fixtures | `protocolVersion`, `WorkEventGrammar.Version` and fixtures in the request | D120's note | **Taken**, and it holds |
| C6.1 | Ship only the measured `--once` path first | Adopted, except a waiter that writes nothing and dropping §6.3 | PIPEOWNER1 (`FIX-LOG.md:1810-1828`); §1.5's leak | **Taken**. Both exceptions are answered with reasons that hold. |
| **C7.1** | **The driver's frame was not already safe** | Decided for Codex | `Workspaces.cs:26-28` | **Taken**, and it holds |
| **C7.2** | **The delivery corrections need another read** | Decided for Codex | — | **Taken**. This is that read. |

### 1. Claims about the code

Most of §1 holds at its lines:

- **Hooks:** the hook points, the hook set's `wanted` rule (`Hooks.cs:705-710`), `_holdsHome` (`:640`), `Tell`, `ChangedAsync` and `Listening` (`:1096-1206`), and the reader's one notification (`:524-534`).
- **Plugin process and verb entry:** `HookProcess.StartInfo` and its start through `AgentStarts` (`HookProcess.cs:68-146`). `GitWiring.Removed` and `SessionTokens` (`GitWiring.cs:40-47`). `HolderKey.Take` and `RoleTerminal` (`SupervisorProof.cs:54-120`). The host's order of gate, adoption, lease, early routes and log (`Host/Program.cs:353-445`). `VerbLeases` (`:56-98`) and `DaorisHome.InstallOf`, `Adopt` and `Entry` (`:62-127`).
- **Where the kinds come from:** the attention watch (`Attention.cs:210-261`) and both of its callers (`DriverLoop.cs:927`, `Host/Program.cs:999`). The park conclusions (`Driver.cs:2884-2889`, `DriverIntakes.cs:412-416`, `DriverContinuations.cs:830-836`). `AutoLander.Tried` (`AutoLander.cs:648-672`) and `LandDueAsync` (`Driver.cs:1405-1438`).
- **Launcher and pipes:** the launcher's swap (`Launcher/Program.cs:67-108`, `:168-169`; `StagedBuild.cs:519-539`) and `InstancePipe.Security` (`InstancePipe.cs:744-762`).
- **Minted ids:** session, quest and ask (`Sessions.cs:799`, `Quests.cs:704`, `Asks.cs:403-408`).

These claims are wrong:

- **"A role's terminal may start it" (:606-608) reads as if `RoleTerminal.Runs` were the only gate.** The holder runs Daoris's pinned harness with a fixed `--allowedTools` list (`HolderTerminal.cs:42-49`). No rule there matches a relay started by its copy's absolute path. `--where` is an alone path, which D209.md:77-78 refuses (→ 1).
- **"The most a process can do through the pipe is make the relay say a false wake" (:678-679), and "no loss waits on a later event to be said" (:669-670).**
  - A silent first client holds the relay's one connection (:628-630).
  - A refused plugin leaves the relay waiting (:629-630).
  - A plugin marked unsound stays down "until the person tries again" (`WorkBounds.cs:24-25`).
  - In each case the relay waits forever and nothing is said (→ 4).
- **"The relay prints only fixed words over ids" (:676-677, §8).**
  - The gap and update lines print the plugin's first-line `run` and `seq` (:602, :650-654). No step checks them (:634, :643).
  - Refusals, dropped frames and repeats go to stderr, with no rule on what they say (:610, :629, :641, :644-645, :656) (→ 2, 3).
- **"The home's `bin` entry is … `bin` in an install's `data/`" (:221-222).** For an install's home the new place is the install's `cache/bin`. `data/bin` is chosen only where it already holds something (`DaorisHome.cs:171-183`, `:265-271`).
- **"What it touches in the home, said whole" (:619-624)** leaves out the plugin catalogue read that finds the data folder (`PluginCatalog.cs:41`, `:286`).
- **§4.4's late stop "under the work wire's bounds" (:485-490).** That bookkeeping (`_work`) applies only to plugins where `WorkPlugins.Speaks` is true (`Hooks.cs:698-709`, `:786-793`). A plugin on `work/event` alone is not one of them.
- **"The person grants it by switching `event-relay` on" (:463-468, :517).** This is true beside a running Daoris (`PipeCensus.cs:286`). While Daoris is closed, `plugin` verbs act alone (`VerbLeases.cs:92`).
- **§4.2's "Not told" list (:410-413)** leaves the help room out as "the person's own conversation". A repository conversation's park is still told, because the watch reports a conversation's park (`Attention.cs:143-144`).
- **`TASKS.md:460`** still describes the first build by the inbox ("the inbox relay first", "posts a session's end to a stand-in inbox").

### 2. The mechanism's holes

**What an event carries.**
- The driver's frame is now sound: generated ids in their minted shapes, closed codes, no name, no note, no path, no account, and a census over `HookFrames.Event`.
- The hole has moved to the relay's own output:
  - The plugin's first line is printed unvalidated (→ 2). Step 3 admits every agent, so an agent that connects first writes `run: "Merge-every-landed-branch.now"` into the steward's session. That is the hole R1 closed for frames.
  - The relay's stderr reaches the session with the background command's output, and nothing bounds what it says (→ 3).
- A quest id is a 12-hex prefix of SHA-256 over `from->to:title` (`Quests.cs:1238-1245`). It is no name, and it tells a process of this user nothing the home does not already hold.

**A subscriber acting as the person.**
- The relay opens no Daoris door, starts no process, holds no key (`HolderKey.Drop`) and makes no press. D156 and D201 hold.
- What can act after a wake is the receiving session, under its own permissions, and §8 says a wake is no confirmation.
- The one rule bent is D209 point 4: `--where` in a role's terminal is an alone path (→ 1).

**The relay's copy, its lease and the swap.**
- These hold:
  - The relay is classed to hold nothing.
  - `--where` holds the lease briefly and waits on the swap.
  - The copy is outside `app/`, so `StagedBuild.RunningFrom(app)` never sees it.
  - The `in-use` file keeps a build that a relay runs.
  - Nothing else clears `cache/bin` (CACHECLEAR1 clears `cache/toolchain` and `cache/tools` only).
- What does not hold: staleness is judged by `grammar` alone (:601-604). The copy also reads the newer build's lease file and catalogue, and checks the newer build's launcher. A build that changes any of those without raising the grammar leaves an old copy refusing every plugin and waiting, which is CONNECTOR1's warning again (→ 5).

**Loss, duplicates and order across a restart and a relay that ends.**
- Order holds: one connection, `seq` order, and `Tell` under one lock.
- There are no duplicates while `--since` equals the first line.
- Loss is said whenever a plugin connects: a new `run`, a passed watermark, `missed`, and the gap at the end of a connection.
- Loss goes unsaid in three cases:
  - **No plugin ever connects** (unsound, off, unstartable, a stale copy, a silent agent). The relay waits with no wake, and the steward, having stopped polling, hears nothing (→ 4).
  - **A busy lease read.** `Tell` "mints no `seq`" when `_holdsHome()` is false (:375-378), and `_holdsHome()` reads the lease file on every call (`Hooks.cs:640`; `HomeLease.cs:195-202`, `:303-304`). A busy read drops the event and leaves no gap (→ 6).
  - **A false gap, the opposite error.** With a narrower `--kinds`, `--since` taken from the last printed line misses a later filtered frame, so the next relay reports a loss that is not one (→ 7).

**A slow or gone subscriber, or a slow plugin.**
- Nothing holds the loop: a per-plugin queue and teller, 256 waiting, and the plugin answering at once.
- Each subscriber has 64 waiting and a 5 s mark, and is closed with its gap said.
- A late plugin is stopped after three late answers in a row. Its back-off needs the work wire's bookkeeping extended to it (→ 11).

**Measured before depending.**
- 1b and 1c wait on M5 (the wake), M9 and M10 (elevation and ancestry), which is right.
- Not measured, though the design depends on it:
  - stderr reaching the session (→ 3);
  - an end by a kill, or with nothing printed, which step 6 relies on to wake (:669-670);
  - a wait that crosses the session's compaction or `/clear`;
  - the session that would actually subscribe through a role's terminal: the managed pinned build, `--setting-sources user`, `disableAllHooks` and its allowlist (→ 1, 12).

### 3. The rows

- **EVENTHOOK1a** can be built alone, and its record proves what it measures. M5 must be run in the session that will subscribe, and must add the cases in §2 (→ 1, 3, 12).
- **EVENTHOOK1b** can be built alone after 1a, and its ten proofs prove its claims.
  - Its list should name extending the late back-off to a plugin that does not speak the work wire (→ 11).
  - Its list should mint `seq` regardless of the lease read (→ 6).
  - §6.3, the strip of the messaging variables, should not wait on M5 (→ 9).
- **EVENTHOOK1c** can be built after 1b. Its proofs need:
  - a word-shaped first line, and both output streams checked (→ 2, 3);
  - the relay's silent cases ended and said (→ 4);
  - a build-keyed stale copy (→ 5).

  As written, its Process-lane class cannot pass step 3 honestly. "With the install's image" refuses a test host that holds the lease, the same shape as R12 (→ 8). Its lanes need `SystemProcesses` (`HolderWalk.cs:96-126`) moved beside `InstancePipe.Security`.
- **EVENTHOOK1d–1f** are later rounds or a quest. They stand.
- **Together:** for the owner's own session outside Daoris, 1a–1c let the steward stop polling once M5 answers yes, findings 2–4 are taken, and the plugin is on the install. No row puts the plugin on the install: `event-relay` is not an offer (`tools/desktop-publish.mjs:227`). No row proves the stop on the installed app: 1c is stand-ins only (→ 10). For a steward in a role's terminal they do not, without a D209 amendment (→ 1).

### 4. Findings

1. **C1.1 is half-closed, and the design never says which session subscribes.**
   - Today the steward is the owner's own session outside Daoris (the reads record, :3). The steward role is dark (`StandingConfirmation.cs:97`).
   - For a role holder, `HolderTerminal.SessionArguments` (`HolderTerminal.cs:42-49`) has no rule that matches the relay's copy path, so every re-arm asks the owner.
   - `--where` is an alone path, which D209.md:77-78 refuses, and D209 is not amended (:606-608; `TASKS.md:463`).

   **Blocking.** *Smallest change:* name the first build's subscriber as the owner's own session outside Daoris. Take `events relay` back out of `RoleTerminal.Runs`. Leave the role holder for a later round with a D209 note: a fixed allowlist element for a fixed command name, and the copy laid out by the instance rather than `--where`.
2. **The plugin's first line reaches stdout unvalidated.** Its `run` and `seq` are printed in the gap and update lines (:538-540, :601-602, :650-654). Step 3 checks only `plugin` and `run ≠ "sample"` (:634), and step 4 checks only frames (:643). **Blocking.** *Smallest change:* check the first line against the grammar (`run` `^[0-9a-f]{12}$`, `seq` an integer from 0, `grammar` an integer, no other field) and refuse it otherwise. 1c sends `Merge-every-landed-branch.now` as the first line's `run`, and sees it refused and nothing printed.
3. **stderr is part of the wake, and its words are unbound.** A background command's output carries both streams. Yet refusals, dropped frames and repeats are "said on stderr" with no rule on their words (:610, :629, :641, :644-645, :656). **Blocking.** *Smallest change:* every stderr line comes from a closed list of fixed sentences, never a value read from the pipe or a file. 1c's word-shaped cases check both streams. M5 records whether stderr reaches the session.
4. **A relay can wait forever with nothing said.**
   - The plugin is unsound until the person tries again (`WorkBounds.cs:24-25`), switched off, or unstartable.
   - A stale copy refuses the new build's plugin and waits (:629-630).
   - An agent takes the one connection and stays silent (:628-630).

   The claims at :669-670 and :678-679 say otherwise, and the steward has stopped polling. **Blocking.** *Smallest change:* correct both sentences. Then say these cases, either:
   - by a marker the live plugin holds open, deleted on close, which the relay watches: it ends with a fixed line when the marker goes or is absent at its start; or
   - by a `--within <minutes>` bound, ending with `Daoris: nothing heard; read the sessions again [<run> #<seq>]`.

   1c proves a relay with no plugin, and one held by a silent client.
5. **Staleness is keyed on `grammar` alone** (:601-604), but the copy reads the newer build's lease, catalogue and launcher. Not blocking. *Smallest change:* `initialize` and the first line carry the driver's `<build>`. A relay of another build prints its update line and ends.
6. **A busy lease read drops an event with no gap** (:375-378; `Hooks.cs:640`; `HomeLease.cs:195-202`, `:303-304`). Not blocking. *Smallest change:* mint the `seq` whatever the lease says, and tell only where it is held. Take the holding from the look's reconcile, not a file read per event.
7. **A narrower `--kinds` makes a false gap across relays.** `--since` is the last printed line (:648), not the relay's final watermark. Not blocking. *Smallest change:* the batch's last line names the watermark at the relay's end, and 1c's two-relay proof runs across a restart.
8. **Step 3 and 1c's Process-lane proof.**
   - "With the install's image" (:633-634) refuses a test host that holds the lease.
   - "A direct child of the launcher" refuses a `node` that resolves to a version manager's shim (`HookProcess.cs:74-83`, `:111-112`).
   - The parent, start and image reader exists only in the modules (`HolderWalk.cs:96-126`).

   Not blocking. *Smallest change:* the class runs the built host's `drive --once` from a laid-out install as the holder. Check that the client is in the launcher's own job (`PieceLauncher.OwnJobOf`) rather than its direct child. Move `SystemProcesses` in 1c's lanes.
9. **§6.3 waits on M5**, though §10 calls it "a fix of its own … a leak today" (:828-829; :731-733). Not blocking. *Smallest change:* its own row, not gated.
10. **No row installs the plugin or proves the stop on the install** (`tools/desktop-publish.mjs:227`; §9's 1c is stand-ins only). Not blocking. *Smallest change:*
    - 1c names *Install from a folder*, a `Person` route (`PipeCensus.cs:286`), as how the owner adds it.
    - Add the parent's installed proof: a real park wakes the steward's real session, recorded in the measurements record.
11. **Claims and records to correct.** Not blocking.
    - The grant is the person's only while Daoris runs (`VerbLeases.cs:92`). Rest C4.1 on the frame holding nothing the home's files do not.
    - The `bin` entry's place in an install (`DaorisHome.cs:171-183`).
    - The catalogue read missing from "said whole" (:619-624) and from 1c's list.
    - The late back-off needs `WorkPlugins`-style bookkeeping for `work/event` (`Hooks.cs:698-709`). Name it in 1b.
    - A repository conversation's park is told while the help room's is not (`Attention.cs:143-144`). Say which is meant.
    - `TASKS.md:460` still names the inbox as the first adapter and its proof.
12. **M5 leaves out what step 6 rests on.** An end by a kill or with nothing printed, and a wait across compaction or `/clear` (:669-670, :722). Not blocking. *Smallest change:* add both to M5's row.

12 findings, 4 blocking (1-4).

**Verdict: accept after named changes the steward can check without another read.** Round 3 takes Codex's read well. The frame now carries no name. Filtering has moved to the relay, so a gap means a loss. The first line settles a restart at once. The copy's custody is specified. The smaller first build ends the poll without the inbox's token. Each of the four blocking findings has a narrow change that can be checked line by line:
- name the owner's own outside session as the subscriber, and withdraw the role-terminal widening, which needs a D209 amendment (1);
- validate the plugin's first line by the grammar (2);
- bind stderr to fixed sentences (3);
- correct the two "nothing goes unsaid" claims and give the relay a way to say a plugin that never connects (4).

If the steward instead wants the D209 role holder as the first subscriber, that is a change to D209's stated-whole start line and its walk, and it needs another round.

## Codex, round 3

Codex's read of round 3 at `c685459f4`: 12 findings, 7 blocking; another round. The steward ran it read-only on main from outside the install, and its links are made repository-relative.

Read-only review at `c685459f4`; nothing changed, no probes run, and `local/` was not read. The code under `src/`, `tools/` and `examples/plugins/` is unchanged from `972b17577`.

References below use **Design** for `docs/2026-10-24-event-hook-design.md`, and its `Driver/`, `Host/` and `Modules/` prefixes defined at lines 49–51.

### 0. Round-2 findings

Severity is the original finding’s severity; status judges round 3.

| Finding | Severity | Status and reason |
|---|---|---|
| C1.1 — role-terminal gate | **blocking** | **Taken.** Exact-prefix admission and role-terminal proofs are assigned to 1c; Design:606, :768. |
| C1.2 — closed-list and landing claims | **non-blocking** | **Taken.** Whole-list filtering and concurrent landing order are described correctly; Design:224, :414. |
| C2.1 — names leave the driver | **blocking** | **Taken.** Names leave the frame altogether, before plugin delivery; Design:454. |
| C2.2 — same-user threat promise | **blocking** | **Answered with a reason that holds.** The promise now assumes intact relay files and authenticates no event; Design:672. First-line validation remains a separate hole below. |
| C2.3 — filesystem custody | **blocking** | **Taken.** Staging, usage handles, reparse checks and removal rules are specified; Design:585. |
| C2.4 — wake versus confirmation | **non-blocking** | **Taken.** Independent reads and authorization are explicit; Design:747. |
| C3.1 — workspace-less events | **blocking** | **Answered with a reason that holds.** One explicitly granted home scope replaces the workspace filter; Design:407, :463. |
| C3.2 — final-loss recovery | **blocking** | **Open.** Hello-watermark recovery fixes reconnection, but a final rejected source event can remain silent; Design:446, :650. |
| C3.3 — filtering and ordering | **blocking** | **Taken.** Filtering moves to the relay; source enqueueing is serialized and repeats are suppressed; Design:476, :655. |
| C3.4 — discovery and bounds | **blocking** | **Open.** Bounds improve, but failed admission can strand a relay and watcher reconciliation is missing; Design:527, :548. |
| C4.1 — subscription visibility | **blocking** | **Answered with a reason that holds.** The person grants opaque events for the whole home; subsequent reads retain their own permissions; Design:463. |
| C4.2 — logging and layout work | **non-blocking** | **Taken.** Catalogue updates and the existing home resolver are assigned; Design:588, :767. |
| C5.1 — measurement dependencies | **blocking** | **Taken.** Adapter-specific prerequisites replace the contradictory graph; Design:729. |
| C5.2 — event-frame contract | **blocking** | **Taken.** Optionality, rejection and concurrency proofs are specified; Design:431, :767. |
| C5.3 — host entry and copied build | **blocking** | **Open.** Entry routing and home discovery are settled; the copied bundle’s identity remains incomplete; Design:589. |
| C5.4 — quest lifecycle source | **blocking** | **Deferred with a reason that holds.** Recorded operations replace snapshots in a later design round; Design:397, :769. |
| C5.5 — channel reconciliation | **blocking** | **Deferred with a reason that holds.** Install-scoped reads and swap handoff are explicit prerequisites; Design:690. |
| C5.6 — package ownership and fixtures | **non-blocking** | **Taken.** The request carries versions and contract fixtures; Design:771; D120:158. |
| C6.1 — smaller first build | **non-blocking** | **Taken.** Only 1a–1c ship first; the remaining writes have stated purposes; Design:774, :825. |
| C7.1 — disagreement over frame safety | **blocking** | **Taken.** The unsafe names were removed; Design:916. |
| C7.2 — another independent read | **blocking** | **Taken.** Both readers must read round 3 before acceptance; Design:917. |

### 1. Wrong claims about existing code

1. **Non-blocking — the holder key has another reader.** Design:200–201 says only `sessions supervise` reads the captured key, but `Host/Program.cs:656` also supplies `HolderKey.Taken` to `StewardCommand`. Correct that inventory; clearing the captured key before the relay waits remains the right proposed change.

2. **Non-blocking — the installed `bin` location is conditional.** Design:221–223 describes an install’s location as `data/bin`, but `Driver/Platform/DaorisHome.cs:171` resolves an installed home to its install root, and :265 chooses between new and legacy locations. `HomeEntry.Bin` at :400 therefore permits the install’s `cache/bin`; the proposed use of `Entry` is correct, but the explanation is not.

### 2. What leaves Daoris, and what a subscriber can do

1. **Blocking — the first line bypasses the stated validation contract.** Design:539 defines `{plugin, run, seq, grammar}`, while :643 validates event frames against a grammar whose closed fields exclude `plugin` and `grammar` (:431). Yet :602 and :650 interpolate the first line’s `run` and `seq` into model-facing output: an admitted agent can supply authored strings there without the design explicitly rejecting them. Define a bounded hello schema, validate it before either output path, and require fixed diagnostics without echoed input.

2. **Blocking — one assembly hash does not identify the copied build.** Design:589 keys the copy by version and one assembly’s hash, and checks the bundle only when that directory is absent. The host has a separate driver-library dependency (`Daoris.Desktop.Driver.Host.csproj:14`), so changing an un-hashed companion DLL can reuse an older copied bundle even after `--where`. Identify the complete copied bundle and prove that changing only a dependency selects the new copy.

3. **Non-blocking — the remaining authority boundary is substantially clearer.** Design:454–468 removes names, paths and authored words from event frames and explicitly grants home-wide opaque visibility; the existing plugin handshake still receives home/data paths (`Driver/Plugins/Hooks.cs:149`), so “path-free” applies to the exported event, not all plugin traffic. Design:606–618 explicitly handles the role terminal, captured key and log-free entry; :747–759 grants no action or confirmation from a wake. Running the waiter outside `app/` addresses the launcher’s actual swap condition (`Daoris.Desktop.Launcher/Program.cs:72`), subject to the bundle-identity correction above.

### 3. Delivery

1. **Blocking — a final unexportable event still disappears silently.** Design:446–448 mints a sequence for a required field that fails validation but reports the omission only in the *next* event’s `missed`. If the connected plugin has heard sequence 10 and rejected sequence 11 is the last event, neither its hello watermark nor the waiting relay advances; :670’s “no loss waits on a later event” does not hold. Specify an independent progress/loss signal and prove this case without another event, disconnect or restart.

2. **Blocking — a subscriber can wait forever before obtaining a connection.** Design:527 leaves a failed connection until its file changes, :548 does the same to a ninth subscriber, and :629 leaves a relay waiting after a refused connection; its file is otherwise written only once (:521). Freeing a slot or recovering from a transient failure therefore need not wake or admit that relay. Define reconsideration on capacity changes and an observable failure path; test both without rewriting the subscriber file.

3. **Non-blocking — recovery after a successful connection is otherwise coherent.** A changed run or hello watermark triggers reconciliation without another work event (Design:650), which correctly handles the attention watch’s startup baseline (`Driver/Quests/Domain/Attention.cs:224`). Filtering advances source progress, duplicates are suppressed, and the source lock orders emission rather than historical occurrence (Design:476, :655). An absent outside session receives no replay; its next successful subscription requests a fresh read, which is consistent with wakes rather than durable messages.

### 4. Places it breaks a decided rule

1. **Blocking — subscriber discovery trusts a watcher without reconciliation.** D173:196–198 requires a slow reconciliation behind a watcher, and the accepted events contract repeats that requirement (`docs/2026-10-16-fabric-events-design.md:52`); Design:525–534 instead rescans only after callbacks or reported errors and explicitly rejects a clock. A missed creation callback without an error can leave a subscriber undiscovered indefinitely, and the desktop’s own plugin watcher excludes `.data` (`Driver/Platform/HomeWatch.cs:131–149`). Add a bounded internal reconciliation and prove recovery with a callback deliberately suppressed; this does not require the steward to poll sessions.

### 5. Rows 1a, 1b and 1c

1. **Non-blocking — 1a has a usable gate.** Design:731–741 now tells its builder which measurements permit progress and what a failed measurement blocks. Extend M5’s explicit cases to termination before stdout, since Design:669 relies on that termination waking the session too; the Process-lane stand-ins cannot establish Claude Code’s behavior.

2. **Blocking — 1b still needs the final-rejection rule.** Its event sources, nullable fields, lease check and concurrent enqueue order are sufficiently assigned at Design:767. Its builder remains stuck choosing how to signal the final minted-but-unsent sequence identified in §3.1; add that behavior and its no-later-event proof to this row.

3. **Blocking — 1c needs the remaining protocol choices before implementation.** Design:768 does not settle hello validation, failed/pending admission, silent watcher loss or complete bundle identity; its current proofs omit those cases. The **1a → 1b → 1c order holds**, but together they justify stopping the steward’s poll only after those failure paths produce recovery or an explicit wake.

**Verdict: another round.**
