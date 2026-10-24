# EVENTHOOK1: the reads of round 1 (D191)

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
