# Who is at work on a session: its parts, its subagents, its co-workers and the quests it asked, what each is on and what waits on what, read from the Fabric's records, and how a session hands its work out (COWORKVIEW1, proposed, 2026-10-24)

**Standing:** proposed, round 5, under D191, with dated notes amending D173 (the Collaboration Fabric), D203 (co-workers), D115
(lanes) and D167 (sessions and their parts). The design is two scopes with a standing each:
- **Row a, *a chat's subagents seen*: the scope round 5 settles.** Its whole contract is §7 row a's table, each rule with where it
  is specified and its proof. **Round 5** is written by Claude Opus 5.5 from the code on main at `9f48860ba`, whose `src/` is the
  same as `eced2688e`'s, the commit Codex read round 4 at. It answers Codex's read of round 4 (11 findings, 7 blocking, *another
  round* for row a; `docs/2026-10-24-cowork-view-reads.md`, *Codex, round 4*), every finding checked against that code, in §16;
  each place it changes is marked *(C4-n.m)*, Codex's §n finding m. The Opus foundation reader's read of round 4 (12 findings, 4
  blocking, *accept after named changes*) stays taken as §15 placed it. **Both makers read round 5 before row a is accepted**
  (D191), and nothing of it is built, filed or staged until then.
- **The routing half (rows e, b, c, d and f, and the sections marked *routing half*): proposed for a later round**, under its own
  reads, unchanged by round 5. Round 4 corrected what the reads found in it and settled none of it. Nothing of it is built or filed
  until that round accepts it, which FOUNDATION1's §5.1 has written after its rows c, d and f land.

The split is the steward's call after two rounds of Codex's reads each found more than ten blocking points across the whole
design: a row specified whole can be settled, while the width of both halves together kept every round open (§8).

**The rounds.** Round 1 was written by Claude Opus 5.5 from the code on main at `fd1d2727e`, and the independent Opus foundation
reader asked another round (19 findings, 10 blocking). Round 2, from `426ed2b73`, answers it in §11, and that reader's read of round
2 (13 findings, 3 blocking, *accept after named changes*) is placed in §12. Codex read round 2 at `972b17577` (20 findings, 11
blocking, *another round*), and round 3 answers it in §13. Both readers read round 3 at `308edb13d`: the Opus foundation reader
would accept after named changes (11 findings, 3 blocking), and Codex asks another round (19 findings, 13 blocking). **Round 4** is
written by Claude Opus 5.5 from the code on main at `b4f096cc0`, whose `src/` differs from `308edb13d`'s only in the supervisor's
and the steward's rows, which move lines this design cites in `DriverLoop.cs` and the driver host's `Program.cs` and change
nothing it reads, and answers every finding of both in §14; places it changes are marked *(O3-Fn)* for the Opus read's finding n and *(C3-n.m)* for Codex's §n finding m. Every read is
verbatim in `docs/2026-10-24-cowork-view-reads.md`. §1 to §13 keep their rounds' citations; a passage round 4 changes cites
`b4f096cc0`'s lines. The Opus foundation reader read round 4 at `953ae0c48` (12 findings, 4 blocking, *accept row a after named
changes*), placed in §15, and Codex read it with those changes at `eced2688e` (11 findings, 7 blocking, *another round* for row a).
**Round 5** answers Codex's read in §16; a passage it changes cites `eced2688e`'s lines, which are `9f48860ba`'s. Nothing here is
built, publishes, signs in or spends an account.

**What changed in round 5** (§16 places each):
- **Each process's lines carry its token** (C4-2.1). The driver issues a token, `proc`, before each spawn and writes it on that
  process's `start` and `end` and on every line its capture writes, bound into the process's writers when they are made, so a
  capture that outlives its process writes only as that process. The fold places a line with `proc` in its own process's
  generation wherever it sits, and the `start` line makes the file (§3.6).
- **History is read as it was recorded** (C4-1.1). Before row a no process wrote a `start`; a chat wrote one `end` per process,
  but a driven record one for all its processes. So a driven record's history may hold processes whose boundaries were never
  written, and a child no harness ended there reads *its end was not recorded*, never *ended*, *lost* or that `end`'s moment
  (§1.1, §3.6).
- **A root's calls wake nothing** (C4-2.2): a composition registers its part's root, and a call the fold reads as the root's wakes
  no read (§4.1).
- **A failure is a value, and every delivery is numbered** (C4-2.3, C4-3.1). This read's ask never throws: a failure within the
  patience and a held one are answered as the failed value, and the query is never retried. Every reply and event carries a
  number, and the page drops one older than what it holds (§4.1).
- **An input's failure is seen** (C4-2.4). Each slice the read takes its records from tells its health from memory and wakes the
  read when it fails or recovers. A slice never judged and failing reads *failed*; one judged and failing reads *stale* (§4.1,
  §5.2).
- **The bound says what it leaves out** (C4-4.1). Forty rows are filled open runs first, each child with its whole chain of
  ancestors or not at all, and every child not drawn is counted by its state (§4.1, §5.2).
- **Smaller** (C4-1.2, C4-3.2, C4-3.3). A part's state is its record's own word, not the strip's. Row a's code is placed on
  FOUNDATION1 Part A's module map, the `acp` writer behind a port (§4.3). `finished` has its words in both languages (§5.4).

**Round 4's standing**, as it was: proposed, round 4 with its read's named changes; row a the scope it settled, its Opus read's
named changes taken in §15 against `deb766f20`; Codex's read of round 4 owed, and nothing of row a built until it landed.

**What changed in round 4** (§14 places each):
- **Row a's runs keep every child its harness shows** (O3-F1, C3-2.1): a child no harness said the start of begins its run at its
  first call, *first seen*, and a late call never reopens a closed run (§3.6).
- **Every process is bracketed** (C3-2.2, C3-2.3): a `start` and an `end` line at each of the four places the driver starts a part's
  agent, and a last generation closes by the part's process being gone, never by whether the part is responsible for a quest
  (§3.6).
- **Row a reads no service and wakes once** (O3-F2, C3-2.4, O3-F5, C3-1.4): its parts and records come from the `groups` and
  `sessions` slices' snapshots in memory, every input it read is a dependency with the version it read, checked again as it
  lands, and a dependency that woke it leaves the watch until the read is asked again; a call line wakes it only where it begins a
  run (§4).
- **No state reads as nobody, as stale or as broken** (O3-F8, O3-F9, C3-4.1, C3-4.2, O3-F11e): a pending answer is a reading
  marker; the hook's unmatched agent ids stand in the empty sentence's place; a refresh that failed over a held answer is told and drawn as such;
  a child closed by Daoris's reading says when it was last seen (§3.6, §4, §5.2).
- **Row a draws no responsibility**: its parts are the strip's, the attended one first; the current executor, the waits, the lead
  rule and the fold by liveness are the routing half's, where round 4 corrects them (O3-F4, C3-1.1) (§3.1, §7).
- **The routing half's corrections, carried**: lanes in the registry's signature (O3-F3, C3-1.2); a burst's inputs owned past
  their flight (C3-1.4); reviewers refused only once COWORKUX1c lands (O3-F6, C3-1.3); helpers alone in COWORKUX1a's read (O3-F7);
  the standing told recorded (O3-F10); one-at-a-time worded once (C3-3.1); a meeting's request found by its folder's list
  (C3-5.6); rows b and d's proofs (O3-F11) (§4, §6, §7).

**Round 4's named changes** (§15 places each, from the Opus foundation reader's read of round 4): an open run whose harness says
no end is `unsaid`, never *working* (O4-F1); a pending composition registers the slices' not-judged status, so their first push
answers it (O4-F2); no failure makes the page ask again, and both carry their words (O4-F3); an `end` with no `start` after it
bounds a generation, and each `end` is written after the capture drains (O4-F4); and eight smaller ones, each a rule and a test
in §7's table (O4-F5 to F12).

**Round 3's standing**, as it was: proposed, round 3, written from `972b17577`; nothing built before both readers read it.

**What changed in round 3** (§13 places each):
- **Responsibility is current, never historical** (C2.1). A quest has one current executor, its newest record here; an earlier
  record of it is history whatever its end says, and a part waits only while its wait still stands (§3.1, §3.2).
- **A part's children live in execution generations** (C2.2, C2.3). The driver marks each process's start in the lineage; a
  generation closes at its `end`, at the next start, or at the record's own end, so a crash never leaves a child working, and a
  child started again is working again (§3.6).
- **The read names every dependency with the stamp it read** (C2.4), and what the tick and the driver's configuration move is read
  where they already push (C2.5): the untaken reason is the tick's own, and the hint's standing is told by the driver's slice (§4).
- **The smaller shape is adopted where the outcome holds** (C6.1): the read answers only what nothing else holds; co-workers come from
  D203's exchange read and open its thread, the channel from the panel's own read, a meeting by its link; the service's two reads
  are taken once per burst for every held read (§2, §3.9, §4, §5). What of it is not adopted is said under *Rejected* (§8).
- **Partial visibility is kept** (C4.1, C4.2): a hook's starts no announcement matched are a count of starts, never hidden; an
  unreadable lineage, a pending read and a failed one each say so, never *nobody* (§3.6, §5.2).
- **Routing eligibility is observable** (C3.1, C3.2): the driver tells a lead and a chat its repository's routing standing at each
  start, and where none was told the agent works its rows itself; serving roles hand nothing out (§6.2).
- **The rows** (C5.1-C5.5): row a's proofs gain the lifecycle and the watch; row e carries the eligibility line; row c follows
  COWORKUX1a and opens the thread where COWORKUX1b has landed; row d gains the ask's terminal twin; row f adds its files' wakes (§7).

**Round 2's named changes** (2026-10-24) take the Opus foundation reader's read of round 2, read at `eb11f2f5f` and kept verbatim in
`docs/2026-10-24-cowork-view-reads.md`: 13 findings, 3 blocking, *accept after named changes* the steward checks without another
read. Codex's read is owed, so under D191's note of 2026-10-24 only the first row is built on this until it lands, and nothing
carrying a row of it is staged. Each finding is taken as the read names it, checked against the code on main at `0d3af537b`, whose
`src/` is `eb11f2f5f`'s. Each place a finding changes is marked *(R2-On)*, `R2-O1` being the read's finding 1, and §12's table says
where each went. The three blocking ones:
- **The `atWork` wake names no session** (R2-O1). The hub asks every held `SESSION_AT_WORK` read again, so the reads that hold a
  part that moved, an ask's among them, are never missed (§4).
- **A lead waits on a lane part for its answer, never its code** (R2-O2). Work its next step builds on is not handed out, or it
  finishes its quest and the entry's one *Accept* joins the parts. It commits before it waits, and *alongside* holds only while the
  lead is live (§1.2, §6.1, §6.2).
- **A waiting part is live, never ended** (R2-O3). A part completed with its quest taken and a wait code is not folded, the lead
  does not move off it, and its wait is the *Waiting on* line (§3.1).

**What changed in round 2.**
- **A lane quest to the session's own repository is a further part of that session** (finding 1). It is drawn once, as a part, and
  it lands on the session's one branch under its one *Accept* (§1.3, §1.9, §3.1, §6.1).
- **The routing doctrine says what each kind of session does after publishing** (finding 2). It names the person's rule that bounds
  an agent's quests and the preconditions for them (finding 3) (§6.2, §6.5).
- **The read is woken by one named path into the held reads**: an `atWork` scope told on the bus (named `work` in round 2, R2-O9),
  and one fixed watch part over the lineage files (finding 4) (§4).
- **The protocol door's lineage line carries the parent and the wire's own end word**, and never an end the driver made (finding 5).
  The join to the hook uses the evidence already on record and never draws one child twice (finding 6) (§3.6).
- **No group names a boundary the lineage does not keep.** The subagents are grouped by the lead's agent process, not by a turn,
  whose end the event log keeps and the lineage does not (finding 7; R2-O4).
  *Last heard* comes from what the window heard, and gives way to the watcher's *quiet* (finding 8) (§3.1, §3.7).
- **The view is *At work* (分工), its read `SESSION_AT_WORK`, its verb `sessions at-work`.** D203's *team* and `sessions team`
  stay D203's (finding 9) (§5.4, §7).
- **The rows are features.** The first is *a chat's subagents seen*, built driver to page (finding 10) (§7).

Paths: `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Core/`
is `src/Daoris.Service/Daoris.Service.Core/`, `Mcp/` is `src/Daoris.Service/Daoris.Service.Mcp/`, `Web/` is `src/Daoris.Web/src/`.
`Dnn` is `docs/decisions/Dnn.md`. Every fact in §1 was read in the code its round names (the *Standing* above). A fact this design could not find is said as *not found*,
with what was searched. Backlog rows are cited by id, not by line.

## 0. The case

The row COWORKVIEW1 in `TASKS.md` carries it. On the install, a kit's chat was moved to another account into a fresh conversation
(`chat.moved-from`). It went on working its roadmap one native subagent at a time inside one long turn. Its page showed little but
*Next, RV16*, and its record's `updated` stood at the turn's start. Nothing said who was doing what, so the work read as stuck and as
no team at all. The repository declares no lanes, so it could not hand its own rows out as Daoris sessions (SELFASK1). A native
subagent is one tool card on the page, although FABRIC-C1a binds it to its part by lineage.

Two things went wrong, and this design answers both:

1. **Seen.** The Fabric already records most of who works beside a session, but no reader puts those records in one place. The one
   record that names native children is read only by tests (§1.1).
2. **Routed.** The session did all of its roadmap in one turn of one agent. Its repository had no lanes, so the one way to hand
   itself work did not exist. The publish tool's own description already names a repository's lanes as "the one way to ask your
   own repository" (`Mcp/QuestTools.cs:37-40`). The refusal it would have met, a quest to its own repository whole, named no way
   then; SELFASK1 has since made it name one (§1.9, C1.3). What the case lacked was lanes, not words.

## 1. Today, read from the code

### 1.1 Native children: recorded, never read

- **The record.** FABRIC-C1a writes `<home>/sessions/<id>/lineage.jsonl` for a session the driver started
  (`Driver/Sessions/ChildLineage.cs:35`, `:52`; `Core/Sessions/LineageFile.cs:13-22`; D173's FABRIC-C1a note, `D173.md:104-112`).
  It has four writers, each marked by `by`:
  - the connector, on every call, from the call's `_meta` (`Mcp/…/CallLineageFilter.cs:17-20`; fields `LineageFile.cs:72-92`);
  - Claude Code's hook on `PreToolUse` for the connector's tools, `SubagentStart` and `SubagentStop`
    (`Driver/Sessions/hooks/lineage-hook.mjs:7`, `:25`). It is handed in the session's settings
    (`Driver/Agents/Permissions/Permissions.cs:561-576`) on both the pipe and the protocol door: a chat hands it in `session/new`'s
    `_meta` (`Driver/Conversations/Chat/ChatRunner.cs:1349-1360`), a driven session at `Driver/Loop/Driver.cs:3087-3097`. The hook
    writes no parent (`lineage-hook.mjs:31-46`);
  - the driver reading Claude Code's stream on the pipe door, for each child's start, parent, depth and end, with the end's `status`
    (`Driver/Sessions/Domain/ChildBindings.cs:298-323`, hooked in at `Driver/Agents/Streams/HarnessCapture.cs:123`). The protocol
    door's capture runs no stream watcher (`ChatRunner.cs:2397-2419`);
  - the driver at the part's end, clean or not (`ChildBindings.cs:326-327`; `ChatRunner.cs:2497`, `Driver.cs:3825`).
- **What it keeps.** Ids only: the hook keeps a value only where it is shaped as an id (`lineage-hook.mjs:10-12`, `:24`), so no path
  reaches the file. `ChildBindings.Fold` folds the lines into each child's harness, native id, type, parent, depth, provenance,
  connectivity, state (`active`, `finished`, `ended`, `lost`) and call count (`ChildBindings.cs:13-37`, `:64-73`, `:128-266`), and
  the part's root, end and counts (`:90-111`).
  - It takes a parent only from a stream line or a Codex call (`:268-291`).
  - A part's `end` line closes every open child as `ended` where clean and `lost` where not (`:241-244`). A stop from the hook or
    the stream makes a child `finished`, whatever its end's `status` said (`:195`, `:211`, `:278`).
  - The lines carry their moments (`at`), but the fold keeps no start or end time.
  - No line keeps what the child was asked to do: the stream's line drops the task's description (`:298-323`).
  - Codex says no thread's end, so a Codex child ends with its part and is never `finished` (`D173.md:121-122`). A Codex child is
    bound only from the `thread` of its own calls (`ChildBindings.cs:223-235`).
  - *(C2.2, C2.3; read at `972b17577`.)* **The fold knows no process.** A child named again after an end clears only its `Closed`
    (`ChildBindings.cs:177`); its `Stopped` stays and wins (`:278`), so a finished child started again reads `finished`. Any line
    naming it reopens it, a call's included. The orphan sweep ends a record whose process went with the application, moving it to
    `stopped` (`Driver/Trees/Orphans.cs:81`), and writes no lineage `end`, so its open children stay `active` for good. And
    `ChildLineage.Read` answers a file it could not read as an empty fold (`ChildLineage.cs:125-139`, `:137`), the same as none.
  - *(C3-2.1, C3-2.2, C3-2.3, O3-F1; read at `b4f096cc0`.)* **A Codex child exists only through its calls** (`ChildBindings.cs:154-157`,
    `:223-233`): Codex says no thread's start or end. **A part's agent is started at four places**: a chat's
    (`Conversations/Chat/ChatRunner.cs:1441`), a driven session's first run (`Driver/Loop/Driver.cs:3643`), and, inside the same
    record, a continuation's run carrying words (`Driver/Loop/DriverContinuations.cs:1242`) and the one retry of a native resume
    (`:1299`). A driven record's lineage `end` is written once, after the record concludes (`Driver.cs:3876`, then `:3892`), so a
    crash between the two leaves a record `completed`, possibly with a wait code, and its generation open. A missing file is one of
    the `IOException`s `Read` folds as empty, and the first child's line is what creates it (`lineage-hook.mjs:49`;
    `ChildLineage.cs:92`). *(O4-F12e, corrected; read at `deb766f20`.)* Not only a child's: the connector writes a line for every
    call, the root's first call included, and makes the folder (`Core/Sessions/LineageFile.cs:59-70`, `:106`), and on Claude Code
    the hook writes the root's own `PreToolUse` line, with no agent (`lineage-hook.mjs:31-46`). A file may so exist with no child.
    On `deb766f20` the chat's spawn is `ChatRunner.cs:1546`, its browser line `:1487` and its `end` `:2854`, written in the run's
    `finally` after its conclusion; the driven run reads its exit at `Driver.cs:3827` and drains its capture at `:3833`, a
    continuation at `DriverContinuations.cs:1272` and `:1274`, a resume's retry at `:1312` and `:1314`. *(Round 5; read at
    `eced2688e`, whose `src/` is `9f48860ba`'s.)* The chat's spawn is now `ChatRunner.cs:1596`, its capture awaited whole at `:2896`
    and its `end` at `:2977`, still in the run's `finally`; the driven run's spawn is `Driver.cs:3658`, its exit `:3842`, its drain
    `:3848`, its conclusion `:3891` and its `end` `:3907`; the continuation's and the retry's lines have not moved.
  - *(O3-F5.)* Every call to Daoris's connector writes two lineage lines: the hook's on `PreToolUse` for the connector's tools
    (`ChildLineage.cs:41`) and the connector's own (`Core/Sessions/LineageFile.cs:59-70`). *(O4-F12e, corrected.)* Two on Claude
    Code, whose hook it is (`Agents/Permissions/Permissions.cs:573`); a Codex call writes one.
  - *(O4-F4; C4-1.1, corrected; read at `eced2688e`.)* **Every file written before row a holds no `start`, and one `end` per
    process only where its record is a chat.** A chat writes one as each process lets go, in its run's `finally` once its capture
    was awaited whole (`ChatRunner.cs:2977`, `:2896`), which on the pipe door is each turn. A driven record writes one for all its
    processes: a continuation's run and a native resume's retry start further processes inside the record and write no line
    (`DriverContinuations.cs:1242`, `:1299`), and the record's one `end` follows its conclusion (`Driver.cs:3891`, then `:3907`).
    Round 4 said every such file held one `end` per process, which is true of a chat's alone. Today's fold reads an `end` as the
    boundary between runs: a child named again after an end "runs again, in the part's next run" (`ChildBindings.cs:176-177`).
  - *(C4-2.1; read at `eced2688e`.)* **A driven run's capture may outlive its process.** Past the capture's bound the run's job is
    ended and the capture waited for again; one still open then is abandoned, still observed, and may go on writing after the
    record went on (`CaptureWithinAsync`, `Driver.cs:4004-4026`, `:4023`; `AbandonedAsync`, `:3965-3985`). Its lineage writer
    names only the session (`ChildLineage.Watch`, `ChildLineage.cs:145-152`, made for each capture at `HarnessCapture.cs:123`), and
    `AcpStreams` is made once per protocol connection (`Acp.cs:1668`). A chat awaits its capture whole before its `end`
    (`ChatRunner.cs:2896`). The hook and the connector are the agent's own children: the hook is handed its file as an argument
    (`lineage-hook.mjs:62`), and the connector reads its session from the environment it inherits (`LineageFile.FromEnvironment`,
    `Core/Sessions/LineageFile.cs:52-53`), where the start sets `DAORIS_SESSION_ID` (`Driver/Agents/Adapters/Adapters.cs:1574`).
- **Nobody reads it.** `ChildLineage.Read` (`ChildLineage.cs:125-139`) is called only by tests (`ChildLineageTests`,
  `LineageHookProcessTests`). No module route and no page code reads the lineage (searched `ChildBindings`, `ChildLineage` and
  `lineage` across `Modules/` and `Web/`). FABRIC-C1b, *a part's native children on its record*, is open and placed after C2a.
- **What the page shows of a child comes from elsewhere**, and is gone when the window is:
  - *the protocol door* carries every session on the install (D173's FABRIC-ACP1 note, `D173.md:5173-5180`).
    - The adapter announces a child on its parent's wire session (`from`), which is the root's or another child's, with its name,
      its task and its `subagentSessionId`. It announces the child's end with a state (`Driver/Agents/Acp/AcpStreams.cs:152-204`).
    - The driver opens a console stream for the child. It keeps one tool card on the session's events, titled with the child's name
      and carrying its task as text, updated in place at the child's end, for a child the root spawned only (`:158`, `:169-181`,
      `:202`). A nested child's name and task are held only in memory.
    - When the session ends, `EndAllAsync` closes every open child with the driver's own `session-ended` (`:110-148`).
    - A child's own words and tools go to its stream, never to the session's record or transcript (`:16-21`, `:95-102`).
  - *the pipe door*: the `Task` call is a card titled `Agent: {description}` (`Driver/…/StructuredOutput.cs:461`). The child's
    lines, tagged `parent_tool_use_id`, go to a stream named by the task's description (`Driver/Agents/Streams/ClaudeStreams.cs:73-80`,
    `:99-108`, `:137-149`);
  - the streams are held in memory only, each line a sequence number and its text with no moment (`Driver/…/SessionOutput.cs:9`,
    `:69`, `:92`, `:175`). They are served by `SESSION_STREAMS` (`Modules/Bridge/Routes/ConsoleRoutes.cs:44-55`) and drawn as the
    output panel's tabs, *subagent · live* or its end word from `work.panel.stream.state.*` (`Web/bridge/sessions/console.ts:152-199`,
    `Web/work/console/streams.ts:23-44`), with no parent, depth or moments;
  - a conversation's blocks have no parent or nesting (`Web/work/conversation/conversation.ts:231-261`), so a child is one
    `ToolCard` (`Web/work/conversation/ToolCard.tsx:82`).
- **The ids.** The CONSOLE2 evidence records that `subagentSessionId` is "the harness's agent id, seventeen hex characters", and that
  a re-run of the same task gets `<id>:generation:<n>` (`docs/2026-09-28-console2-streams-evidence.md:61-64`). The adapter's end
  states are `completed`, `failed`, `cancelled` and `disconnected` (`:69-70`). The hook's `agent_id` is Claude Code's agent id, the
  same id as the stream's `task_id` (`D173.md:100-102`). Whether the hook reaches Daoris over the adapter at all (`SubagentStart` on
  the protocol door) is unmeasured: FABRIC-ACP1 saw no native child (`D173.md:5177-5180`, `:131-133`).

So a person can see that a child started and ended, one card at a time, in the conversation and the panel. Nothing else is shown:
not the children together, not which one runs now, not one lost when its root was killed, and nothing at all once the window
restarts.

### 1.2 Parts and co-workers

- **Parts** (D167). A session is an entry, `<origin>/<repository>`, and its parts are every record under that key
  (`Core/Sessions/SessionEntries.cs:25-37`). The origin of a quest's session is the ask that asked it; else the conversation that
  took it (CHATTAKE1); else the origin of the session that published it; else its chain parent's; else the quest itself
  (`:96-113`). A conversation handed over takes the one it continues, through `from` (`:86-90`). So an entry's parts are:
  - its `from` chain, each a record continuing the last with its reason (`from_session`, `from_why`, written at
    `Core/Sessions/Sessions.cs:827`), shown in the head's facts as *continues…* (`Web/work/head/headFacts.ts:95-98`);
  - **every session opened on a quest whose origin is the entry's own**: a quest the line published to its own repository's lanes,
    a sibling request of the same ask to the same repository, and a quest a chat took (CHATTAKE1, `Session.TookQuest`,
    `Sessions.cs:246-250`). The ledger opens such a session under its quest's origin (`Core/Sessions/SessionLedger.cs:1599-1616`).
  - They are shown in the *Parts* strip and as the session's one rail row (D167 points 2, 4 and 5; FLOWCHART1b, archived 2026-10-20).
- **How a further part runs** (D167 point 4 as amended 2026-10-14; SESSONE1d). A new quest of an entry goes into the newest part's
  tree when nothing live holds it. Otherwise it starts at once alongside, in a tree of its own, as a further part
  (`Driver/Quests/Domain/Planner.cs:1716-1759`). This holds only where the repository opens its own trees here
  (`config.OpensOwnTree`, `:1723`). Where it does not, the repository runs one session at a time and the entry changes only the
  grouping (`:2010-2021`).
  - *(R2-O2)* **A lead waiting on a quest holds no tree.** A D79 wait ends `completed` with `ended.awaits` (`Observation.cs:104-111`),
    and only a wait on one's own helper keeps its tree (`WaitingInTree`, `Planner.cs:2195-2199`; `HelperWaits.HoldsTree`,
    `HelperWaits.cs:36-38`). So a lane quest the planner looks at after the lead's turn ended goes *into* the lead's tree
    (`Planner.cs:1762-1767`), with whatever the lead left uncommitted, and does not run alongside. Which of the two happens is a race
    between the lead's end and the planner's look. *Alongside* holds only while the lead is live.
  - *(R2-O2)* **The lead's resume does not bring a part's code.** The resume after the quest it waited on is answered starts the lead
    in its own earlier tree (`Planner.cs:1773-1801`, D79's door) with the answer in its instruction (`QuestExchange.cs:1503-1505`).
    That start syncs the tree with the line alone (`Driver.cs:533-547`, LANDSYNC2); only a chain step going on is synced to its
    source's submission (`GoOnInSyncAsync`, `:549-575`). A part that ran alongside lands on the entry's branch, never in the lead's
    tree.
  - *(C2.1; read at `972b17577`.)* **A resume is another record.** D79's resume opens a new record for the same quest
    (`Core/Sessions/SessionLedger.cs:1612-1616`), and the waiting one stays `completed` with `ended.awaits`. The quest keeps naming
    its question after the answer and after the resume (`QuestView.Awaits`, `Planner.cs:38-50`, SAYHELD1): whether it still waits is
    the planner's `ResumedOnAnswer` (`:1577-1581`), read from the question being closed and the quest's last session here. A helper's
    answer goes on in the waiting record itself (D203 point 5).
- **How it lands** (D167's LANDSYNC0 amendment, ACCEPTALL1, `D167.md:286-336`). An entry's parts land on the entry's one branch
  under the entry's one *Accept*, a part that ran alongside replayed onto that branch's tip. A part's own *Accept* lands it on the
  same branch (ACCEPTALL1b, ACCEPTALL1f).
- **Co-workers** (D203, accepted 2026-10-23): a record of this machine's whose `serves` names a part of another session's line. Its
  role is read as reviewer (`opinion`), partner (`serves` with `piece`) or helper (`serves` alone) (`D203.md` point 1; the design
  `docs/2026-10-23-coworker-design.md` §2.1). A helper and a reviewer keep their own entry and are no part of the line they serve
  (`SessionEntries.cs:76-77`).
  - A helper's ask is three files under `<home>/data/helpers` (`Core/Platform/DaorisHome.cs:340`): the ask (`id`, `asker`,
    `question`, `agent`, `askedBy`, `urgent`, `at`), its state (`waiting`, `approved`, `started`, `answered`, `refused`, `cooling`)
    and its answer (`Core/Sessions/HelperBox.cs:33`, `:44`, `:73-83`, `:117-123`, `:185`). A lead waiting on its helper ends
    `completed` with `ended.awaits-helper` (`Driver/Sessions/Domain/Observation.cs:164-165`), a note with no values
    (`Web/…/noteLines.ts:50`), so which helper it waits on is a join through the box's `asker`.
  - A partner's piece is a box under `<home>/data/partners` (`Driver/…/PartnerFiles.cs:6-10`, ask fields `:76-85`, state
    `:121-135`). COAGENT1a and 1b are built; `partner_ask`, the lead's own door, is not (COAGENT1d).
  - The page shows a helper's open asks in the head's *Helpers* and a partner's *Pieces* (`Web/work/head/SessionHead.tsx:440-457`);
    answered and refused asks are filtered out (`Web/work/waits/HelperAsks.tsx:61-66`). None of COWORKUX1a-i is built: the key
    `work.helper.heading` still reads *Helpers* (`Web/locales/en/work.helper.json:9`), and no `work.team.*` key exists.
- **Not co-workers** (D203 point 1): a part, another repository's session, a meeting's participant, a native subagent, Ask Daoris,
  an intake and a brainstorm's pass.

### 1.3 Quests a line published, lanes, and waits

- **Lanes** (D115 §2.2, built by DEV4). `daoris.lanes.json` declares a repository's lanes. `connect` sends their words, never their
  globs, and the registration keeps them (`docs/2026-10-01-self-development-design.md:129-162`). A quest is addressed
  `repository:lane`. The exchange splits it once (`Core/…/QuestExchange.cs:528-531`), judges the lanes (`:707-743`) and keeps
  `Lanes` beside `To` (`Core/…/Quests.cs:117`). A repository may ask its own lane, "work for another lane is work for another
  session"; to itself whole it is refused (`QuestExchange.cs:533-540`), naming its lanes or the file that would give it some since
  SELFASK1 (`:539-552` at `972b17577`, C1.3).
- **Under D167, that other session is a further part of the publisher's own.** A quest's origin is its publisher's (§1.2), so a
  session publishing to `itsrepository:lane` opens a session in its own entry. That session runs alongside in a tree of its own,
  where the repository opens its own trees, and lands on the entry's one branch. A lane quest to *another* repository opens a session
  in that repository, a different entry of the same origin.
- **The driver plans by repository, never by lane.** Its only readers of lanes are the intake's room, the registration and the
  remote sync (`Driver/Quests/IntakeRoom.cs:133-138`, `Driver/Service/Domain/LineRegistration.cs:199`,
  `Driver/Service/RegistrationFollow.cs:155-166`, `Driver/Service/Domain/RemoteSyncPayloads.cs:169`). A lane's quest is started as
  any quest to its repository, and only where that repository is driven here (`Planner.cs:1889-1891`). PAR1 already runs a
  repository's sessions side by side where it opens trees of its own (`Planner.cs:1964-1966`). DEV6 adds lane reservations and a
  cap; DEV9 adds the steward that splits a whole-repository quest. Both are open.
- **The links.** A quest names the session whose connector published it, `PublishedBy`. It is null both for the person's own
  publish and for a chain's step (`Quests.cs:104-109`), and an intake publishes as its ask (`From` is `ask #id`). A chain's step
  names its `Parent`. A session names the quest it serves, `Session.Quest` (`Sessions.cs:103`), or the quest a chat took,
  `Session.TookQuest` (`:246-250`). No session record names a lane (no `Lane` in `Sessions.cs`).
- **The wait.** A taken quest waiting on a question names it in `Awaits`, one id (`Quests.cs:96-102`), set by `quest_respond`
  `wait` with one `on` (`Mcp/QuestTools.cs:366-374`). The exchange refuses a wait on the quest itself or on a closed one, and on
  nothing else (`QuestExchange.cs:1454-1505`), so a lead may wait on a quest it published to its own lane.
- **Units.** Quest units already carry `awaits`, `follows`, `produced-by`, their executors and their end (`WorkUnit.cs:53-73`,
  `:140-152`), built from the quests and sessions by `QuestUnits.Read` with no other read (`Driver/Quests/QuestUnits.cs:20`).
- **Why a quest sits.** The driver's tick says why each open quest does not start (D46 §3): not drivable, the repository busy, the
  cap spent, waiting for an account, and the rest (`Planner.cs:1023-1048`, `:2048`), read by the page through `sittingBecause`
  (`Web/data/signals.ts:5-7`, `:145-158`). *(C2.5; read at `972b17577`.)* The loop keeps them at each look
  (`Modules/Loop/DriverLoop.cs:984`) and hands them to the page on `DRIVER_TICK` (`:1027`), held as `useConsidered`
  (`Web/bridge/driver.ts:337`), as an ask's work reads them (`Web/asks/AskWork.tsx:56`). No ledger write moves them, and none of the
  driver's configuration: a drive or a trees switch is read by the driver's slice (`:987`), a registry's lanes by the tick
  (`:1019`, `RegistryHeard`).
- **Who took it** *(C1.2; read at `972b17577`)*. A quest's answer names the machine whose take holds it, `Holder`, beside this
  machine's `Here` (`Planner.cs:33-37`). The planner tells a take made here after a session ended, *by a chat or by work outside
  Daoris*, from another machine's (`:1746-1765`). A taken quest with no executor here is therefore not always another machine's.
- *(Routing half; O3-F3, O3-F4, C3-1.1, C3-1.2; read at `b4f096cc0`.)* **The planner's last session of a quest** (`ReadLastRun`,
  `Driver/Service/ServiceClient.cs:2134-2150`) reads `quest` only, never `tookQuest`, and skips a teammate's record, a stood-down
  one and one its agent refused the choice of before any prompt (D205 §3.5(c)); on equal `created` it keeps list order. **The
  registry's signature**, which the tick compares (`DriverLoop.cs:1027`, `RegisteredAsync` `:1343`), is each repository's name,
  adoption, workspace and root (`Planner.cs:722-736`): no lane moves it, and the change stream has no registry family.
- **Shown:** a quest's lanes on its card and page (`Web/quests/QuestList.tsx:214-218`, `Web/quests/QuestPage.tsx:656`); its wait on the
  quest page and list (`QuestPage.tsx:665-682`, `Web/quests/QuestsView.tsx:563-567`) and on the rail as *awaits #q of repo*
  (`Web/work/rail/SessionRow.tsx:329-332`); *Who it worked with* (`Web/work/…/SessionRelations.tsx:105-111`). **Not shown:** a
  lane anywhere under `Web/work/`, in an ask's work (`Web/asks/AskWork.tsx`) or on the chain strip.

### 1.4 The ask's channel

The channel is a table of messages (`Core/Asks/Channel.cs:243-258`). Each message has its ask, moment, posting session (null for the
person), repository, agent, entry, addressee, reply, question, text and urgency (`:87-104`). Daoris's own lines are read from the
quests and never stored (`:116`). The channel is drawn on the ask's page (`Web/asks/AskPage.tsx:533-545`) and in a session
(`ChannelPanel`, `Web/work/AttendedSession.tsx:323-324`). Each message shows *repository · agent* or *You*, its moment, a question or
urgent pill and its receipts (`Web/asks/ChannelSection.tsx:32-34`, `:288-291`). A message's session and entry are carried and not
drawn.

### 1.5 Brainstorms and meetings

A brainstorm is `<home>/brainstorms/<id>/`, with its request `(Id, Question, Repository, Agents, At)` and its state
(`Driver/…/BrainstormFiles.cs:8`, `:13`, `:35-40`). A meeting is `<home>/brainstorms/meetings/<id>/`, its request the same shape and
its state with a round (`Driver/…/MeetingFiles.cs:9`, `:15`). **Neither request names the session, ask or quest that started it.**
Neither has a place on the page: *no place in the frame, no state on any screen* (`docs/2026-10-20-ui-ux-design.md:110`). Their page
is MEETUX1a-d's, and a caller from a session is BRAINSTORM1d's and AGENTROOM1b's.

### 1.6 Work units, the running view and the flow

- **Units** (FABRIC-UNIT1a): kinds `branch`, `quest`, `ask`, `run` (`Driver/Trees/Inventory/WorkUnit.cs:4-20`), each with an owner,
  subject, end and links `produced-by`, `produced`, `awaits`, `follows` (`:53-73`, `:129`, `:140-152`). `WorkUnits.ReadAsync`
  (`Driver/…/WorkUnits.cs:54`) reads every unit, the branches' inventories with it, and is read only for node evaluation
  (`Driver/Loop/DriverWatch.cs:900`). FABRIC-UNITDOOR1, the door that would answer every unit (`WORK_UNITS`, `work units`), is open
  (no such route or verb under `src/`). `WORK_STATE` answers branch milestones, not units (`Modules/Bridge/Routes/LinesRoutes.cs:558-612`),
  pushed as slice `work`.
- **The running view.** `Flow` is built (`Web/design/Flow.tsx:147`) and draws a quest, a session's line and states, a landing and a
  workflow's run (FLOWCHART1a1-1d, archived; `Web/workflow/WorkflowChart.tsx:363`). FABRIC-GRAPHVIEW1 is open: each node's §E2
  state, each wire's condition and what waits on what, drawn from the units in the side bar's *Workflow* view
  (`docs/2026-10-20-flowchart-design.md` §2.5, §10). That design rejects *a second running view beside the Workflow view* and *a path
  inferred from a conversation, or a state the record does not keep* (§8).
- **No fabric event names a child.** The chain's events are wakes naming a subject (`Driver/Platform/Chain.cs:5-59`). The session's
  event log has user, message, thought, tool, plan, usage, turn, note, transient, raw and receipt
  (`Driver/Sessions/Domain/SessionEvents.cs:13-54`). The design's typed envelope is not built
  (`docs/2026-10-16-fabric-events-design.md:1204-1213`).

### 1.7 The session's record and page while its agent works

- **`updated` stays at the turn's start.** It is *when the record last moved* (`Driver/Trees/SessionGroups.cs:36`). It is written at
  the record's creation and at each ledger move (`Core/Sessions/Sessions.cs:827-828`, `:890-912`), and for a note kept on a said word
  (`:1562`); every other write leaves it (`:1413-1797`). The ledger has no move from `working` to `working`
  (`Core/…/SessionLedger.cs:2181-2192`). A chat moves to `working` once when its process starts and again only at its end
  (`ChatRunner.cs:2423`, `:3103`), and tool calls go to the event log, never the record (`AcpStreams.cs:173`). So an hour of work,
  children included, leaves `updated` where it began. Its readers rely on that meaning: the head's *ran* and *waiting* facts
  (`headFacts.ts:169`, `:175`, `:207`).
- **The event log keeps each turn's end; the lineage keeps no turn** *(R2-O4; round 2's "no record keeps a turn" corrected)*.
  - The session's events keep each turn's end as a `turn` event with its stop reason (`SessionEvents.cs:33-34`), emitted at each
    prompt's response on the protocol door (`Driver/Agents/Acp/Acp.cs:1999-2005`).
  - On that door the adapter holds the parent's turn open until the children it spawned end: CONSOLE2 measured it, and the
    adapter's source defers settling a turn for its subagents (`docs/2026-09-28-console2-streams-evidence.md:74-77`).
  - A chat on the protocol door keeps one agent process across its turns, taking each new message into that process
    (`ChatRunner.cs:1804`, `:1826`, `:2397-2399`). Its lineage gets an end line only when the process lets go (`:2486-2497`), and
    no lineage line names a turn. A child whose stop the hook missed stays `active` through later turns. Grouping children by turn
    would join two files by time, which §3.6 refuses, so the view groups them by the process, which the lineage holds.
- **The head** (`Web/work/head/SessionPageHead.tsx:14-39`) shows the state, the title and a facts line (`headFacts.ts:143-209`):
  repository, the leading agent, *continues*/*helps*/partner links, then by state its age, attempt, retrying, the first answer's
  wait, the running call, a heavy run, *quiet*. Then the primary act, *Stop…* and ⋯. The running call is the `calls` slice's, pushed
  as a call starts and ends (`Modules/Loop/CallsStateSlice.cs:9-27`). The watcher's *quiet · N min* is its second partition
  (`:37-41`, `headFacts.ts:199`), from the watcher's own hearing of a driven session's output, stamped in memory
  (`Driver/Sessions/Watch/SessionWatch.cs:64-71`, `:183-189`).
- **The side bar's views** are notifications, timeline, review, workflow, ask, console and terminal (`Web/work/frame/placements.ts:9`,
  `:22`), on the right but the console and terminal (`:28-30`). None says who works beside the session. The side bar is never under
  300 px (`DOCK.floor`, `Web/work/frame/layout.ts:44`), and its views are storied at 300 and 430 px
  (`Web/help/AskPanel.stories.tsx:335-341`; `Web/work/review/FilePreview.stories.tsx:5`).
- **`chat.moved-from`** is the new conversation's first note (`Driver/…/HandOver.cs:266-271`, `ChatRunner.cs:2125-2127`). It is
  drawn by the note table (`Web/…/noteLines.ts:295`), with the record's `from_session` and `from_why` as *continues…*
  (`headFacts.ts:95-98`). A moved chat's earlier part ran on another account.
- **The ask's page** lists its quests (`AskWork`, each with its state, route and sessions, `Web/asks/AskWork.tsx:23-38`), its lines
  of work (`AskLines`, `AskPage.tsx:526-529`) and its channel, in separate sections.

### 1.8 What reaches the page unasked

- **The service's change stream** has five families, `sessions`, `quests`, `asks`, `channel` and `words`
  (`Core/Platform/ChangeStream.cs:19-51`). The driver hears them in one wait (`Driver/Service/LedgerChanges.cs:26`). The window's loop
  tells each move it heard to `DriverLoop.MoveHeard` (`Modules/Loop/DriverLoop.cs:140-154`), and a slice hears a ledger family there
  (`QuestsStateSlice.HearLedger`, `Modules/Loop/QuestsStateSlice.cs:130-144`). *(R2-O1)* A ledger wake's subject is a list of
  families (`:132-137`), never a session.
- **The modules push fourteen slices** (`Modules/Loop/*StateSlice.cs`; `Modules/Platform/StateSlices.cs:82-85`;
  `.claude/knowledge/pushed-slices.md`).
- **A change with no press** is told as `DAORIS.STATE_CHANGED`, naming its scopes and, where it is one session's, that session
  (`StatePublisher.Publish`, `Modules/Platform/StatePublisher.cs:258-264`; the scopes `:15-87`). The page's one hub asks again the
  queries each scope names (`Web/bridge/state/stateHub.ts:27-68`, `:93-100`).
- **Under NOBLOCK1 a computed read is marked `[ByEvent]`.** It answers at once from what is held (`Modules/Bridge/HeldAnswers.cs`).
  Its work runs on a task of its own, but only when the page asks (`:20-22`, `:74-106`). Where the answer moved, it is told as
  `DAORIS.ANSWERED`, naming the page's query, to the pages that asked (`:170-177`; `Web/bridge/state/held.ts`; D56's NOBLOCK1 note,
  `D56.md:177-190`). The page asks again only when a query is invalidated (`held.ts:24-34`), and D169's one place for that is the
  hub. `WindowCensusTests` refuses a computed route that is neither.
  - *(C2.6, C4.2; read at `972b17577`.)* `HeldAnswers` serialises a landing answer and compares it with the one held under the same
    gate an ask takes (`HeldAnswers.cs:78`, `:145-160`), so a large answer's landing holds an ask on the window's thread. A work that
    fails where an answer is held keeps it and tells nothing (`:164-165`). The page reads a first pending answer through the query's
    own reader, which reads it as empty (`held.ts:20-34`).
  - The shared reads join one flight per key (`RouteFlights`) and are forgotten with the held works at every press and every
    `STATE_CHANGED` (`DriverModule.cs:106-113`, `:245-249`). The bus does not wait for that forgetting, so an ask can still join a
    work begun before the change it answers (`:106-108`).
  - *(Read at `b4f096cc0`; O3-F2, O3-F8, C3-1.4, C3-4.2.)* `HeldAnswers` keeps 128 answers and lets one go only to make room
    (`HeldAnswers.cs:40`, `:181-188`); the hub's invalidation asks again only the queries a view has mounted (`stateHub.ts:80`). A
    flight leaves `RouteFlights` as it completes (`RouteFlights.cs:92`, `:101`), so two reads that do not overlap share nothing. The
    page's query library, TanStack Query 5 (`src/Daoris.Web/package.json:30`), fails a query whose function answers `undefined`, and the
    app retries once (`Web/main.tsx:31`).
  - *(O3-F2.)* `HomeWatch`'s judge moves its baseline at each judgement (`HomeWatch.cs:337`), and judges every topic at a 30 s
    deadline whatever it heard (`:172`). `ChangeWatch` coalesces a subject's hints over 250 ms, at most 2 s (`ChangeWatch.cs:177-180`).
  - *(Row a's inputs.)* The `groups` slice holds each line of work's entry, its parts in the strip's order, as `SESSION_GROUPS`
    answers it (`Modules/Loop/GroupsStateSlice.cs:20-24`, `:173-179`); the `sessions` slice holds every record's allow-listed fields
    (`SessionsStateSlice.cs:38-46`); the driver's slice holds `running`, the sessions with a live process (`DriverStateSlice.cs:44`).
    Each answers its snapshot from memory (`StateSlice.Snapshot`, `Modules/Platform/StateSlices.cs:170-176`) and tells `Pushed`
    after a judgement that pushed (`:378`). *(O4-F6, O4-F12c, corrected.)* `running` is this window's own processes
    (`Modules/Bridge/Routes/DriverRoutes.cs:642`, `SessionProcesses.Running`, `Driver/Sessions/SessionProcesses.cs:183-189`), not
    the machine's: `AliveOnThisMachine` also reads the markers other drivers on this machine keep (`:455-459`). And an ended intake
    whose ask became quests is in no entry (`Driver/Trees/SessionEntries.cs:69-70`, `:96-97`). Before the service answers, both
    slices are not judged (`SessionsStateSlice.cs:71`, `GroupsStateSlice.cs:111`). Held reads are tested in
    `Modules.Tests/Bridge/DriverModuleHeldTests.cs`; there is no `HeldAnswersTests`.
  - *(C4-2.3, C4-2.4, C4-3.1; read at `eced2688e`.)* **Where a held read's failure and its delivery go today.** A first ask whose
    work fails within `Patience` is refused at once, the flight's fault thrown (`HeldAnswers.cs:104-105`), and a fault that landed
    after it is thrown at the next ask, once (`:96-100`); `askHeld` lets the refusal through (`held.ts:30`), so the query is in
    error and the app retries it once (`main.tsx:31`). `Land` decides under the gate and tells after letting it go
    (`HeldAnswers.cs:145-172`, `:174-177`), so two landings of one key may be told in the reverse of their order, and the page
    writes an event's answer with no version (`held.ts:49-51`). A slice whose judgement fails says so once in the machine log and
    keeps what it judged (`SessionsStateSlice.cs:77`, `GroupsStateSlice.cs:117`; `StateSlices.cs:207-214`): its failing flag is
    private (`:107`), its snapshot says only *ready* or *not judged* (`:170-180`), and only a judgement that pushed tells `Pushed`
    (`:248`, `:378`), so nothing a reader holds moves when an input fails or recovers.
- **The home watch.** `HomeWatch` registers its topics once and offers no way to remove one (`Driver/Platform/HomeWatch.cs:270-280`).
  *(R2-O13c)* That is `HomeWatch`'s surface, not the watch's: the `ChangeWatch` beneath it has `Unwatch` (`ChangeWatch.cs:316-339`).
  A topic's wake carries its fixed subject, never which file moved (`_wake(topic, known.Subject)`, `HomeWatch.cs:342`) (R2-O1).
  `ForDriver` wakes the driver's look, not a route (`:196-213`). `ForProposals` and `ForNotices` are the window's own process's,
  whoever holds the home's lease (`:234-258`; `DriverLoop.cs:802-816`). A part may hear a whole folder tree while its judge reads
  only the files named to it (`HomePart.ByName`; `HomePart.Workflows`, `:13-33`). *(C2.4; read at `972b17577`.)* A name newly read
  by name takes its first stamp as read and wakes nothing (`HomeWatch.cs:331`): a file named after it moved is never a move.
- **No family, slice, scope or held read carries the lineage or a native child.** The `calls` slice moves only when a call starts or
  ends (`CallsStateSlice.cs:23-24`).

### 1.9 How work is handed out today

| Way | Who starts it | Where it runs | What the record says | Seen |
|---|---|---|---|---|
| a lane's quest to the session's own repository (D115, D167) | a session's `quest_publish` to `itsrepository:lane`; the person's composer offers no lane (DEV4's *left out*) | **a further part of the publisher's own session**: alongside in a tree of its own while a part holds the session's tree, else in the newest part's tree, a lead waiting on a quest holding none (§1.2, R2-O2); only where the repository is driven here, and side by side only where it opens its own trees | the quest's `Lanes`, `PublishedBy`; the part's `Session.Quest` and its `entry` | the *Parts* strip and the rail row; the quest's page; lands on the session's one branch under its one *Accept* |
| a lane's quest to another repository | the same, to `other:lane` | that repository's session, an entry of the same origin | as above | the quest's page; *Who it worked with* |
| a helper (D163) | the lead's `helper_ask` under `helpers`, or the person | its own one-turn record reading a copy | the helper box, `serves` | open asks in the head |
| a partner (D199) | the person only, until COAGENT1d | its own clone, landed by the lead's merge | the partner box, `serves`, `piece` | *Pieces* |
| a reviewer (D155) | a workflow's occasion, or the person | its own record, reading | `opinion` | the gate |
| a native child | the lead's agent, by its harness's own tool | inside the lead's agent process, its tree and its account | the lineage (§1.1) | one card for a child of the root, and a panel tab |
| another repository whole | `quest_publish` to it | its own repository's session | the quest | *Who it worked with*, the quest |

**A repository asking itself whole** was refused, when the case happened, with *That is the repository you are in — a quest is work
for someone else. Use its own backlog.* (`QuestExchange.cs:535-540`), whether or not it declared lanes, a sentence that did not say
a lane is how a repository asks itself, although the publish tool's description does (`QuestTools.cs:37-40`). *(C1.3; read at
`972b17577`.)* **SELFASK1 has landed** (archived 2026-10-24): the refusal now names the sender's lanes and how to address one where it
declares some, and where it declares none says that a `daoris.lanes.json` with a steward lane is what lets it dispatch its own work
(`QuestExchange.cs:539-552`, `NoLanesWay` `:759-761`). The `registry` tool's row for a repository with no lanes says the same
(`Mcp/KnowledgeSearchTools.cs:326-330`). A lane named on a repository with none is refused naming the repository
(`QuestExchange.cs:713-716`).

**A chat can take a quest** (CHATTAKE1): `quest_respond take` from a chat records `Session.TookQuest`
(`QuestTools.cs:395-403`), and the quest joins the chat's line (`SessionEntries.cs:102-106`; `D167.md:176`).

### 1.10 The case replayed

The kit's chat ran on the protocol door. Each child its root started became one card titled with the child's name and one panel tab
(§1.1). Whether the hook recorded each child's start and stop in its lineage file is not known. `SubagentStart` over the adapter is
unmeasured, and row a reads the install's files first (§7). Nothing read the file either way. The chat's record moved once, to
`working`, at the start (§1.7). The repository declares no lanes, so it could not hand a row to a further part of its own session.
Had it addressed its own repository whole, it would have been refused with a sentence that then named no way (§1.9). The person needed to
know which child runs, which ended, since when and how many are left. Those facts existed only as cards scrolled past, or did not
exist.

## 2. The rule

1. **The view is a read, never a store** (D173's first guard; D203 point 3). Nothing records a team or a structure. The read
   composes the records that already link a part of the work to the session: the entry and its parts, `from`, the lineage,
   `PublishedBy`, `Parent`, `Lanes`, `Session.Quest`, `Session.TookQuest`, `Awaits` and the quest units. *(C6.1)* What another read
   already holds, the view reads from that read, beside this one (§3.9): the co-workers (`serves`, `piece`, `opinion`, the boxes)
   from D203's exchange read, the channel from the panel's own read, why a quest sits from the tick, and *last heard* from the
   `calls` slice.
2. **From the records, never the conversation's text.** No member, state, wait or moment is parsed from what an agent said. What a
   member is on is the record's field: a quest's title, a helper's question, a piece, a lane's title. For a native child it is the
   harness's own announcement of it (§3.6), a structured field of its wire. Each is drawn as the agent's words, never Daoris's
   (D137).
3. **Partial visibility is said, not filled** (ADR-B, canary 9). A child whose parent no harness said sits under the lead with
   *parent not said*. A Codex child is *ended with its part*, never *finished*. A child of a killed root is *lost*. A harness whose
   children are seen only when they call Daoris says so in place of an empty group. A member the read cannot place is not drawn as
   anything else. *(C4.1, C4.2)* Evidence the read cannot place is counted as what it is, never hidden: an agent id the hook named
   that no announcement matched is an id, not a child (round 4). A source not read (a lineage unreadable, the read still pending,
   failed or stale) is said as not read, and *Nobody else is working on this* is drawn only where every source was read and holds
   nobody, no unmatched id included (O3-F9, C3-4.1).
4. **One home for each record.** A record is drawn once, in the one group its record places it in (§3.1). A part is a part, however
   it joined the session, and the view and the *Parts* strip draw the same parts, each once. *(R2-O7)* Their orders differ: the
   strip draws parts as lineage, oldest first (`SessionEntries.Read`; `Web/work/head/partsLife.test.ts:11`, `:88`), and the view
   orders them lead, live, ended (§3.1).
5. **No model** (D24). Every word in the view is Daoris's fixed words or a record's as written.
6. **D203's words stand.** Point 1's co-worker and point 2's team are unchanged. A native child, another repository's session and
   the quest this session serves are not co-workers and gain no `serves`. The view shows them beside the team, each under its own
   heading, because the person asked *who is doing what*, and the answer includes them. Their rules are their own.
7. **One reader for each fact, one place to see them** *(C6.1)*. The view is one place, because the case was work spread over places
   nobody looked at together. Each block in it is an existing presentation over its existing reader where one exists: the *Parts*
   strip's rows read-only, D203's *Co-workers* rows and thread, the ask's quest rows with the tick's reason, the channel's lines, a
   meeting's link. The read this design adds answers only what no reader holds today, over the same parts and quest adapters the
   strip and the quest pages read (`SessionEntries`, `QuestUnits`): each part's current responsibility and wait, how it joined, its
   subagents' tree, and the quests a part asked with their takers.

## 3. The read

### 3.1 Who is at work on a session

**Row a's part of this section** *(round 4)*: the read is asked for a session; the part the page attends is the one whose subagents
are drawn; the entry's parts are listed as the *Parts* strip lists them, the attended one first, each with its record's own state
and its subagents' count; and the *Subagents* group. *(C4-1.2)* The state is the record's `state` in its own word, `sessionState.*`,
as an ask's work draws a session (`Web/asks/AskWork.tsx:111`), and deliberately not the strip's word, which is the reader's derived
one where it said one (`awaiting-helper`, `parked`, `awaiting-reply`, `answered`, `going-on`; `PartsStrip.tsx:56` through
`shownOf`, `Web/work/groups.ts:148`, `:159`), a wait row a does not read. Row a reads no quest, no wait and no responsibility, and folds
no part by whether it is live. **Everything else in §3.1 to §3.5 is the routing half's**, proposed for a later round: the current
executor, *live*, *waiting*, *answered*, the lead rule, the fold of ended parts, and the groups *Co-workers*, *Asked through quests*,
*Asked by* and *Gathered*.

The read is asked for a session: its entry (D167), whose parts the *Parts* strip draws. For an ask, it is the union of its entries'
reads and its quests (§3.5; routing half).

**Current responsibility** *(C2.1; routing half)*. A quest has at most one **current executor** here: the newest record, by `created` then id,
whose `Quest ?? TookQuest` names it, the record the planner reads as the quest's last session here (`LastRun`,
`Planner.cs:1577-1581` at `972b17577`). Every earlier record of the same quest is history, whatever its end says: a D79 resume opens
a new record and leaves the waiting one `completed` with `ended.awaits` (§1.2), so an end code alone never says who is responsible
now. *(O3-F4, C3-1.1: corrected, for the routing half's round.)* "The newest record" is not `LastRun`'s rule. The selector is
one shared function, `LastRun`'s own (`ServiceClient.cs:2134-2150` at `b4f096cc0`): a teammate's record, a stood-down one and
one refused before any prompt are skipped, and a tie keeps list order; then a chat's `TookQuest` names the executor where no such
record of the quest is newer. A part is read as waiting or answered only where its quest's take is this machine's (`Holder` is
`Here`, where the host answers both). The routing half's rows test a refused resume and a stood-down replacement leaving the
earlier responsibility in place, and a chat's take.

**Live** *(R2-O3, made current by C2.1)* is a part that is active (`queued`, `starting`, `working` or `awaiting-person`, the page's
`SESSION_ACTIVE`, `Web/data/api.ts:423-424`), or one that is its quest's current executor, ended `completed` with a wait code
(`ended.awaits` or `ended.awaits-helper`, `Observation.cs:104-111`, `:164-165`) with its quest still taken. Such a part is:
- **waiting** while its wait still stands: for `ended.awaits`, the question its quest names is not closed (neither `Done` nor
  `Declined`; a done held for the person's yes is not closed); for `ended.awaits-helper`, until its record moves, which the helper's
  answer or its unanswered end does (D203 point 5);
- **answered** where its question has closed and its resume has not begun: still live, its *Waiting on* line gone, its row saying
  *its question is answered: it resumes next*, and the tick's reason beside it where the resume sits (§3.9).

Once a later record of its quest exists, the waiting record is an ended part, folded with the others, and the new record is live in
its own right. A waiting lead is `completed`, which the page's active set leaves out, so without this the lead rule would move the
lead to the very lane part it waits on, and the fold would hide the wait among the earlier parts. A waiting part is read as waiting,
never as ended: it is never folded, and its own wait is the *Waiting on* line (§3.2).

**The lead** is the part the page attends where it is live. Otherwise it is the entry's newest live part that no other live part
waits on, by `created` and then id, the order `SessionEntries.Read` lists and `EntryParts.Earlier` finds the newest by; so a lead
waiting on its lane part stays the lead. Where no part is live, the lead is the newest part. Other live parts are drawn *working
alongside* where they run in a tree of their own. *(R2-O9)* The view's lead is not the code's `EntryAccepts.EntryLead`, an entry's
first part, the one its *Accept* is named for (`EntryAccepts.cs:32-44`); the type names it `leadPart` (§3.8).

| Group (en / zh) | Members | Read from | Who | On what | State | Since |
|---|---|---|---|---|---|---|
| **This session** / 本会话 | every part of the entry, once: the lead first, then other live parts (a waiting one among them), then ended parts folded behind a count | the entry's records, as the *Parts* strip reads them | agent, account's mark | its quest's title and its lanes, or *chat* | the record's | *last heard* for a live part (§3.7); the record's end for an ended one |
| **Subagents** / 子智能体 | the lead's own native children, as a tree by `Parent` and `Depth` | the lead's lineage fold, with §3.6's label, moments and end word | the child's type | the harness's label for it, the agent's words | *working*; the wire's end word; *ended with its part*; *lost* | its start; its end |
| **Co-workers** / 同事 | helpers, partners and reviewers of any part | *(C6.1)* D203's exchange read (COWORKUX1a, the coworker design §6.3), read beside this one (§3.9), never the boxes again | agent | the question; the piece; the candidate | the exchange read's | the ask's `at` |
| **Asked through quests** / 通过委托请求 | each quest a part of the entry published that opened no part of it: to another repository (its lanes or whole), or to its own lanes and not yet taken | the quest units (`QuestUnits.Read`) of quests whose `PublishedBy` is a part, and of their chain steps by `follows` | the taking session's agent; *not taken yet* only where the quest is `Open`, with the tick's reason beside it (§3.9, C2.5); a taken quest with no executor here *taken by {conversation}* through `TookQuest`, *taken on another machine* only where its `Holder` names a machine other than `Here`, else *taken, by no session recorded here* (R2-O6, C1.2) | the quest's title; its lanes | the unit's end, its current executor's wait (§3.2), and that session's state where taken | the quest's or the session's moves |
| **Asked by** / 由谁请求 | the quest the lead serves or took, its publisher and its ask | `Session.Quest ?? Session.TookQuest`; the quest's `PublishedBy`, `Parent` and `From` | the publishing session, the step before, the ask, or the person | the quest | — | — |
| **Gathered** / 召集 | a brainstorm's voices and a meeting's participants, once their requests name who started them (§3.6, row f) | the request and its state, each a dependency of the read (§4, C5.5) | each agent | the question | the state's | the request's `at` |

- **A part carries how it joined** (§3.4): *handed on*, *retried*, *published to the {lane} lane by part n*, *took #q*, or *the
  ask's quest #q* (R2-O7). Where it runs alongside, it says *alongside, in a tree of its own*. *(R2-O2)* That is read from the
  part's own tree, a tree no earlier part of the entry worked in, never from its quest's lanes: a lane part the planner sent into a
  waiting lead's tree runs after, not alongside. Each part other than the lead carries its own subagents as a count,
  *2 subagents · 1 working*, which opens that part's view. Only the lead's subagents are drawn as rows. An earlier part of a moved
  chat ran on another account, and the subagents' note (§5.4) is true only of the lead.
- **A quest to the session's own lanes moves from *Asked through quests* to *This session* when it is taken.** Untaken, it has no
  record but the quest. Taken, its session is a part, and the quest is that part's *on what*. It is never drawn in both.
- **A chain's step** (a quest with a `Parent`) is *the next step of #parent* and is placed with that parent, followed from it by
  the unit's `follows` (`WorkUnit.cs:64-65`). A quest whose `From` is `ask #id` belongs to that ask. Only a quest with neither, and
  no `PublishedBy`, is *by you*.
- **A Codex lead's subagents** are bound only from the threads of their own calls (§1.1). For such a lead the group says that its
  subagents appear here only when they call Daoris, in place of *Nobody else is working on this*.

Each member row says **how it got there** (§3.4). It opens what it is: a part's page, a quest's page, a co-worker's thread (the
coworker design §6.4, R2-O13a) once COWORKUX1b has built it and the member's own session page until then (C5.3), a meeting's page
by its link (C6.1), or, for a native child, its console tab while the window holds it and its lineage facts after.

### 3.2 What waits on what (routing half)

Waits are edges, each from a record, never inferred from moments:

| Edge | Read from |
|---|---|
| the lead waits on its helper | `ended.awaits-helper` on the quest's current executor (§3.1); which helper is the exchange read's open ask whose asker is that part, read beside this one (§3.9, C6.1) |
| a taken quest waits on a quest it published | the quest unit's `awaits`, which is `Quest.Awaits`: **one quest at most** (`Quests.cs:96-102`), and **only while it stands** *(C2.1)*: its current executor is waiting (§3.1) and the question is not closed. The unit keeps the link after the answer and after the resume (`Planner.cs:38-50` at `972b17577`, SAYHELD1), so the link alone is never a wait |
| a part or a quest's session waits on the person, on a helper, on its own quest | that session's own state and its unit's `awaits`, the same edges one level down, under the same rule |
| a quest waits to be taken | its status `Open` (R2-O6: a unit's executors miss a chat's take and another machine's, `QuestUnits.cs:36-37`); *(C2.5)* the driver's tick's verdict and sentence for it (`sittingBecause`: not driven here, the repository busy, the cap spent, waiting for an account) are read on the page from the tick's own `considered`, beside the answer, never composed into it (§3.9) |
| a child runs under its parent | the lineage's `Parent`; an edge *inside*, not a wait |

The read answers the edges, and the view draws them as words on the waiting row: *waits on Codex · helper*, *waits on #a1b2 in
driver*, *not taken yet: the concurrency cap (3) is spent*. The session's own wait is drawn once at the top, first: **Waiting on** /
在等 *#a1b2 (driver lane)*. A session that published quests and waits on none says so: *published 3, waits on none*. That is the
truth for a chat that took no quest, which has no `Awaits`, and for a driven lead that handed rows on and finished (§6.2). The
subagents' group says how many run, *2 working*, and states no cause. *(R2-O4)* On the protocol door, where a child of the lead is
working, the heading adds *its turn waits for them*: the adapter holds the turn until the children it spawned end (§1.7). A waiting
part is live (§3.1), so a lead waiting on its own lane part is drawn as the lead with *Waiting on #q (core lane)*, and that part
below it. *(C2.1)* Once the question is answered the line goes; once the resume has begun, the resumed record is the lead and the
record that waited is folded among the earlier parts.

### 3.3 The channel's last words (routing half)

The view draws the ask's newest three channel messages, each with its moment and its author as the channel draws it, and places a
message on the member that said it by its session, which the page already carries and does not draw (`Channel.cs:87-104`).
*(C6.1)* The read carries no message: the lines are the ones the session's page already holds for its `ChannelPanel`
(`Web/work/AttendedSession.tsx:323-324`), and the ask's page for its channel section, so the view adds no reader and no wake for the
channel. *All messages* opens that panel. A session with no ask has no channel, and the block is not drawn.

### 3.4 The routing that put each member there (routing half)

Each member's row says the door that started it, read from its record:

| Member | Its routing, as the record says |
|---|---|
| a part opened by a lane quest | *published to the {lane} lane by part {n}* (`PublishedBy`, `Lanes`); *by you* only where the quest has no `PublishedBy`, `Parent` or ask |
| a part a chat took | *took #{quest}* (`TookQuest`) |
| a part opened by a sibling request of the same ask (`SessionEntries.cs:97`; D167 point 2) | *the ask's quest #{quest}* (R2-O7) |
| an earlier part | *handed on* with its `from_why`, or *retried*, as the parts strip says it |
| another repository's session | *asked of {repository} by part {n}*; *by ask #{id}* where `From` is an ask |
| a chain's step | *the next step of #{parent}* |
| a helper | *asked by this session* or *asked by you* (`askedBy`), under the rule `helpers` |
| a partner | *asked by you* (and by the lead once COAGENT1d lands) |
| a reviewer | *the workflow's second opinion* or *asked by you* (the opinion's occasion) |
| a native child | *started by {agent}*: the harness's own door, which Daoris neither starts nor confines (C2a held) |
| a brainstorm or meeting | *called by this session* or *by you*, once the request names it |

### 3.5 An ask's view (routing half)

An ask's page draws the same component over the ask. It shows the ask's quests, each with its lanes and its taking session, and each
of its entries with its parts, subagents and co-workers folded to a count that opens the entry's own view. It also shows the channel's
last words. It replaces nothing on the ask's page: `AskWork` and `AskLines` keep their presses. The section sits above them and links
into them.

### 3.6 What the records gain

Only two stored changes, both additive and both desktop-only:

1. **The lineage keeps a child's label, its parent, its end word and its moments, each process's start, and on each line the
   process that wrote it** (C2.2; C4-2.1).
   - **The protocol door.** The driver appends a line `by: "acp"` at the adapter's announcement, *(C4-3.2)* through a port
     `AcpStreams` is handed, so `AcpStreams` names no lineage type (§4.3), with:
     - the `subagentSessionId`;
     - the announced name and task, bounded: the name to 120 characters, the task's first line to 200;
     - **its place**: `parent`, the announcing wire session's id where that is a child's, or `depth: 1` where the root announced it.
       This is the one place on that door where a parent is said (`AcpStreams.cs:152-158`).
   - **The end.** It appends another `acp` line, with the wire's `state` word, **only from `subagent_state_update`**
     (`AcpStreams.cs:186-204`). `EndAllAsync` writes no lineage line. A child still open when the process lets go is closed by the
     part's own `end` line, as `ended` where clean and `lost` where not. A killed root's child therefore reads *lost*, never
     *finished*.
   - **The pipe door.** The stream's start line keeps the task's `description`, bounded the same way. `ChildBindings.StreamLine`
     drops it today. Its end line already keeps the `status` (`ChildBindings.cs:313-318`).
   - **Every process is bracketed** *(C2.2, C2.3; made whole by C3-2.2, C3-2.3)*. Round 3 named two places; the driver starts a
     part's agent at four (§1.1): a chat's (`ChatRunner.cs:1441` at `b4f096cc0`), a driven session's first run (`Driver.cs:3643`), a
     continuation's run inside the same record (`DriverContinuations.cs:1242`) and a native resume's one retry (`:1299`). At each,
     the driver appends `{at, by: "start", proc}` before the process is spawned, so it precedes every line that process's hook,
     stream or calls can write, and `{at, by: "end", proc, clean}` once that process's exit is read **and its capture has drained**
     *(O4-F4)*, clean as `End` judges it today (its exit code and no stop made). Draining is `CaptureWithinAsync` after the exit (`Driver.cs:3827`,
     then `:3833`; `DriverContinuations.cs:1272`, `:1274`; `:1312`, `:1314` at `deb766f20`), so a stream's `ended` line is written
     before the `end`, not after it; a chat's `end` is already written last, in its run's `finally` (`ChatRunner.cs:2854`). Where
     the drain's bound passes with the capture still open, the `end` is written at the bound, and *(C4-2.1)* a line the capture
     writes after it carries its own process's `proc`, which places it (below). The record's own conclusion writes no second `end`
     for a process already ended: `Driver.cs:3892`'s line (`:3907` at `eced2688e`) becomes the first run's own. *(C4-2.1)* **The
     `start` line makes the file** where there is none, so every line a process can write follows its `start`, the first
     process's included; its `end` goes to the file its `start` made, and `End` still writes to no file that does not exist
     (`ChildLineage.cs:118-122`). Round 4 wrote both only to a file that existed, which left a file's first process with no `start`.
     A part whose process started before row a has no file until a line of it creates one, as today.
   - **Each line names its process** *(C4-2.1)*. Before each of the four spawns the driver issues a token, sixteen hex characters,
     shaped as an id (`ChildBindings.cs:85`; `lineage-hook.mjs:24`), and writes it as `proc` on that process's `start` and `end`
     and on every line the driver writes for it, the stream watcher's and the `acp` writer's. **The token is bound into that
     process's writers when they are made**: the watcher made for its capture (`HarnessCapture.cs:123`) and the sink handed to the
     `AcpStreams` of its connection (`Acp.cs:1668`) each hold it from the spawn, and none reads which process runs now. So a capture
     that outlives its process (§1.1) writes only as that process: that is the writer's fence. The token is also set on the spawn's
     environment as `DAORIS_LINEAGE_PROC`, beside `DAORIS_SESSION_ID` (`Adapters.cs:1574`), set anew before each of the four
     spawns, since a continuation and a retry reuse the first run's start; and it joins the role variables a start drops when it
     inherits them (`Adapters.Roles`, `Adapters.cs:1659-1665`, ENVLEAK1), so a driver run inside a session never writes as that
     session's process. The hook and the connector, the agent's own children, copy it as `proc` where their environment holds it
     and it is shaped as an id. Whether it reaches them on each door, the hook under the adapter and the connector under each
     harness's own start of it, is rule 1's measure; a line that comes without one is placed by its position, as every line
     written before row a is.
   - **Execution generations** *(C2.2, C2.3, C3-2.2, C3-2.3)*. The fold splits the lines into generations, one per process where
     the file recorded its processes. **Which generation a line is in** *(C4-2.1)*:
     - **a line with a `proc` is in the generation whose `start` names it**, wherever it sits in the file: a line its capture
       wrote late, after its process's `end` or after the next process's `start`, is still its own process's. A `proc` no `start`
       names (its `start` lost, which `Append` never says, `ChildLineage.cs:108-111`) is placed as a line with none;
     - **a line with no `proc`** (every line written before row a, and the hook's and the connector's where the token did not reach
       them) is placed by its position: in the generation open where it sits. *(O4-F4)* **Where none is open, after an `end`, it
       begins a generation**, as today's fold reads an `end` as the boundary between runs (`ChildBindings.cs:176-177`), and the
       owner's case is such a file. One exception: a line that only ends a run of the generation that `end` closed (a stream's
       `ended`, an `acp` end, a hook's `stopped`, naming an id with a run there), coming before any line of a later generation,
       stays in that generation, its end word taken as the end-word rule below has it, and begins nothing.

     **Recorded and unrecorded boundaries** *(C4-1.1)*. A generation a `start` began is *recorded*: its process's boundaries are
     its `start` and its `end`. A generation begun without one is *history*: before row a no process wrote a `start`, a chat wrote
     one `end` per process, and a driven record one for all of its processes (§1.1). So a history generation's own `end` is its one
     process's only where the record is a chat, by its `kind`, which the `sessions` slice already holds (`SessionsStateSlice.cs:41`;
     `kind?: 'driven' | 'chat'`, `Web/data/api.ts:445`). In a driven record's history generation the boundaries between its
     processes were never written, and the fold claims none: a run there that no harness ended may have ended at any of them, so
     it reads **`unrecorded`**, *its end was not recorded · last seen {time}*, never `ended`, `lost` or the `end`'s moment, which may
     be a later process's. In a file row a's build writes, a history generation is begun only by a line with no `proc` sitting after
     an `end` and before the next `start`: a late hook or connector line the token did not reach, or a process an older build
     started after a rollback; it is read as history too. A generation closes at the first of:
     1. its own `end` line, `ended` where clean and `lost` where not, as today (`ChildBindings.cs:241-244`), at the line's moment:
        a recorded generation, and a chat's history one; *(C4-1.1)* a driven record's history generation closes here too, its runs
        no harness ended reading `unrecorded`;
     2. the next `start` line: `lost` for a recorded generation, whose process went without its `end`, which only a crash of the
        application or the driver between the process's exit and its `end` line leaves, since every process now writes one;
        *(C4-1.1)* `unrecorded` for a history one, whose process went before anything wrote its end (the first `start` after an
        upgrade closes a record's history so);
     3. for the last generation only, where no `end` line came, **the part's process being gone**: no process of the part runs in
        this window (the driver slice's `running`) and its record is neither `queued`, `starting` nor `working`. *(O4-F6)*
        `running` is this window's own processes, not the machine's (§1.8): a part another Daoris process on this machine runs is
        `working` on its record and is closed only by its record's move. *(C3-2.3)* This reads the process, never responsibility: a
        record `completed` with a wait code is closed here as any other, so the crash between `Driver.cs:3876`'s conclusion and the
        `end` line leaves no child working. It reads `lost` where the record is `stopped` or `failed`, and `ended` where it is
        `completed`, `awaiting-person`, `declined` or `stood-down`, each a clean end (O4-F7; the nine states, `Web/data/api.ts:413-415`).
        *(C4-1.1)* A driven record's history generation closed here reads its runs no harness ended `unrecorded`, whatever the
        record's state. A `working` record whose process went is not closed by the reading: the orphan sweep moves it `stopped` as the driver's
        watch comes up (`Orphans.cs:74-81`, once per watch start, `DriverWatch.cs:715-717`), and that move wakes the read (§4.1), so
        a process being spawned, `working` before it is tracked, is never read as gone.

     **Rules 2 and 3 are Daoris's reading, not an end anyone said** *(O3-F11e)*. A child they close carries no end moment: it reads
     *lost* or *ended with its part* with *last seen {time}*, its own last line's moment in that generation, never the sweep's
     `updated` or the next start's. *(C4-1.1)* So is `unrecorded`, wherever it is read: *its end was not recorded · last seen
     {time}*. The read passes the record's state, its `kind` and the running set to the fold; nothing is written.

     A child's state and moments belong to **its run** *(C2.3; made whole by O3-F1, C3-2.1)*:
     - a run begins at a start the harness said (the hook's `started`, the stream's `started`, an `acp` announcement), from that
       moment;
     - **or, for a native id with no run in this generation, at its first line of any kind, a call's included**: a child no harness
       said the start of, as every Codex child is (§1.1), is kept, its start unknown, drawn *first seen {time}* from that line's
       moment;
     - it ends at a stop its harness said (the hook's `stopped`, the stream's `ended`, an `acp` end) or at its generation's close;
     - a start the harness said after a stop, in this generation or a later one, begins a new run: *working*, from its new start;
     - **a call or a stop line naming a native id whose run in this generation already ended begins nothing**, so a late call
       never reopens a finished child; a call naming it in a later generation, with no said start there, begins that
       generation's run as above.
     - *(O4-F1)* **A run whose harness says no end is never *working*.** Codex says no thread's start or end (§1.1), so its run
       stays open until its generation closes, which on `codex-acp`, one process across a chat's turns (`Adapters.cs:2063-2067`),
       is the chat's whole process. While open, such a run's state is `unsaid`: *first seen {time} · its end is not said*. It is
       counted as working nowhere: not in *n working*, not in a part's subagents' working count, not in the head's *working with
       it*. Its generation's close ends it as any other (*ended with its part*, *lost*).
     - *(O4-F8)* **The end word.** Where a line of the run says a word, the wire's (`acp`) or the stream's, that word is the run's
       end, whichever line comes first; otherwise the hook's stop makes it `finished`. A hook `stopped` and a wire's
       `subagent_state_update` thus end the same run with the wire's word, never the order of their lines.
     - *(C4-2.1)* **A line placed in a generation already closed** (a line its capture wrote late, carrying its process's `proc`)
       changes that generation alone. An end it says ends or words its run there, as the end-word rule has it. A start it says
       begins a run there that the generation's close has already ended: the run reads as that close has it, with *last seen* at
       its own line, never *working*. It begins no generation and touches no run of a later one. So a late start never makes a
       generation, and an old stop never ends the later run of a reused id.

     The earlier runs of a native id stay in the fold as history and are never drawn as second rows. This replaces `Of`'s clearing
     of `Closed` alone (`ChildBindings.cs:177`), under which a stopped child's `Stopped` won for good (`:278`). *(O4-F4)* That
     clearing was also how today's fold reads an `end` as a boundary between runs; the generation rule above keeps that boundary.
   - **Unreadable is not empty** *(C4.1)*. `ChildLineage.Read` answers three outcomes: no file (nothing recorded), read, and
     unreadable, with the error's kind, where today every `IOException`, a missing file's included, folds as empty
     (`ChildLineage.cs:135-138`). An unreadable lineage is drawn as *its subagents could not be read* on that part, never as none,
     and keeps the view's empty sentence away. A missing file is a dependency of the read stamped absent (§4), since the first
     child's line is what creates it, or the root's first call (O4-F12e), or from row a on its first process's `start` (C4-2.1).
   - **The hook** stays ids only. A label is not an id, and the hook's rule (`lineage-hook.mjs:10-12`) is that no value a path may
     hide in reaches the file through it.
   - **The fold** keeps, per run:
     - each child's first and last `at`, and whether its start was said or only first seen (O3-F1); the read's answer carries the
       first, the end the harness said with its moment, and, for a run rules 2 or 3 closed, the last as *last seen*, never a
       working run's last, which only call lines would move (O3-F5, §4);
     - its label, where a line gave one;
     - its parent and depth from an `acp` line, beside the stream's and Codex's;
     - **the end word as the wire or the stream said it**: `completed`, `failed`, `cancelled` or `disconnected`. A stop with no word
       (the hook's) stays `finished`. The view draws each with the output panel's own words (`work.panel.stream.state.*`), adding
       `disconnected`, and *(C4-3.3)* every other state, `finished` and `unrecorded` among them, with the key §5.4's mapping names.
   - **Why a label may enter a file the rule kept to ids.**
     - The file is the session's, under the home, never synced and never on a shared host (D47).
     - For a child of the root, the label is already on the session's events as the card's title and text (`AcpStreams.cs:169-181`).
     - For a nested child, this is the first time its name and task reach disk (they were held only in memory, §1.1). It is the same
       class of text, from the same structured field of the harness's wire, kept in the same session's own folder. It is never parsed
       from the conversation, and it is bounded.
     - *(R2-O13f)* A task's first line may name a path. The lineage's twin row (`.claude/knowledge/twins.md:180`) says a value is
       kept only where it is shaped as an id, *so no path is written*, which the label ends. Row a amends that row, adding the
       driver's `acp` and `start` lines beside its `stream` and `end` (C2.2), and *(C4-2.1)* `proc` on each line, the hook's and
       the connector's where their environment holds it, and D173's note says the file becomes
       transcript-class, kept on this machine (D47).
   - **The join.** The CONSOLE2 evidence says what the ids are: `subagentSessionId` is the harness's agent id, and a re-run is
     `<id>:generation:<n>` (§1.1). The hook's `agent_id` is the same harness's agent id.
     - The fold joins an `acp` child to a hook child where the ids are equal, or where the `acp` id is the hook's id followed by
       `:generation:<n>`. That is the same child run again: a new run of it, as above.
     - **An `acp` child that joins nothing stays the child's one row.** Hook starts that join no `acp` child on a part that has
       `acp` lines are not drawn as rows. *(C4.1, replacing R2-O8's two halves; counted by id since round 4.)* They are kept as
       what they are, **agent ids the hook named that no announcement matched**, and said as a count beside the group: *its hook
       also named 2 agent ids it could not match*. They are never counted as children: not in *n working*, not in a part's
       subagents' count. *(O3-F9, C3-4.1;
       round 3's "never a reason for the empty sentence to be withheld" withdrawn, since it contradicted the next point.)* **Where a
       part's subagents hold only such ids, the empty sentence is not drawn: the count stands in its place**, with its story, since
       evidence whose end nobody said establishes no absence. The generation suffix is the adapter source's word, not a measurement
       (CONSOLE2 evidence `:61-62`), and whether a re-run's hook `agent_id` is the same id or a new one is unknown, so the count says
       neither that the agent did not announce a child nor that these are other children.
     - *(Round 4, with O3-F1.)* **What is counted is agent ids, not starts.** On a part with `acp` lines, every hook line of an
       agent id no announcement matched, its start, its stop or its calls, is evidence of that one id: counted once, never a row,
       and never a run (the first-line rule above draws rows only from the harness's children the part's rows come from: Codex's
       threads, and on a part with no `acp` lines, Claude Code's agents). Whether `SubagentStart` reaches the hook over the adapter
       at all is row a's first measure (§1.1), so an id the hook names only by a call must be counted too. The words say ids:
       *its hook also named 2 agent ids it could not match*.
     - *(O4-F1)* **A Codex part draws its rows from one source.** The driver asks every streamed protocol session for
       `nativeSubagentSessions` (`Acp.cs:1756-1770`), so `codex-acp` may announce children as `acp` lines as well as its calls
       naming threads, and nothing joins the two today. Row a's measure (rule 1) records whether `codex-acp` announces children and
       whether its announced ids equal the threads its calls carry. Until D173's note records it, a Codex part with `acp` lines draws
       its rows from them, with their ends, and counts each thread no announcement matched once, as the hook's unmatched ids are
       counted; a Codex part with none draws its rows from its threads. Where the measure finds the ids equal, they join as the
       hook's do.
     - **Where row a's measure finds that a door's ids never join the hook's**, the count stands as worded, since it claims no
       child, and D173's note records the measure. Round 2 hid them there; a door whose evidence the view hid could draw *Nobody
       else is working on this* over a hook that saw starts, which D173's ADR-B forbids (`D173.md:11-12`).
     - On a part with no `acp` lines (the pipe door), the hook's and the stream's children are the rows, as they join today by
       `task_id`.
     - Nothing joins by time or by order.
     - Row a measures the join on the install's lineage files and on a stand-in's, on a first run and on a re-run, before the fold
       relies on it. Its counts go in D173's note.
2. **A brainstorm's and a meeting's request name who called it**: `session` and `ask`, null where the person did. This is additive,
   written by the door that makes the request. It is row f's, after BRAINSTORM1d and AGENTROOM1b give a session a door to call one.

Nothing else is stored: no team record, no structure, no lane on the session record (a part's lane is its quest's), no stored wait,
no turn. One fact is kept in memory and not stored: each console stream's newest moment, for *last heard* (§3.7).

### 3.7 The record's `updated`, and *last heard*

`updated` keeps its meaning, *when the record last moved*. Advancing it on every event would make *ran* and *waiting* wrong
(`headFacts.ts:169`, `:175`, `:207`), and would write the service's store at the rate of the agent's events.

On the protocol door, a child's words and tools never reach the session's events, and its lineage gets a line only at its start, its
end and its connector calls (§1.1). A moment taken from the events and the lineage would read *38 minutes ago* for a child that has
worked for 38 minutes. So each working part has a **last heard**, the first of these that holds:

1. **What the window heard**: the newest line of the part's own console stream and its children's streams. *(R2-O13d)*
   `SessionOutput` keeps no moment today (`ConsoleLine(Sequence, Text)`, `SessionOutput.cs:9`; `Buffer`, `:98-112`). It gains each
   stream's newest moment beside its lines, in memory, with no stored change: §3.6's one new memory.
2. **After the window restarted** with the part still working, nothing was heard before the start. The view says *nothing heard
   since the window opened at 14:02*, never a moment it did not hear.
3. **The watcher's hearing** of a driven session (`SessionWatch.cs:183-189`), where the window holds no stream for the part.
4. ~~Its lineage's lines, newest `at`.~~ *(Withdrawn in round 4, O3-F5.)* Every connector call writes two lineage lines (§1.1), so
   the newest `at` would move the held answer at the call rate, the cost §4 rejects for *last heard*; the three sources above are
   what the window heard, and where none holds the view says *nothing heard since the window opened*. Round 2 also read the part's
   events here; round 3 left them out (C2.4).

*(R2-O5)* **How it reaches the page.** The three are facts of the window's memory, so they are pushed as a keyed partition of
the `calls` slice, `heard`, keyed by session and judged from memory as its `quiet` partition is (`CallsStateSlice.cs:37-41`), with
no `STATE_CHANGED`. A stream's new line wakes the slice, coalesced to one judgement per session per ten seconds, since the page draws
*1 min ago* by its own clock. The held answer carries no *last heard*. The head and the view read the slice's `heard` beside it.

The head's facts line for a working session gains one fragment where it has subagents or other parts working: *3 working with it ·
last heard 1 min ago* / *3 个在一起工作 · 1 分钟前有动静*, which opens the view. **The fragment gives way to *quiet*** wherever the
watcher has marked the session (`CallsStateSlice.cs:37-41`). The head never says two things about one session's hearing, and
D200's watcher is unchanged.

### 3.8 The data types

The read answers one type, fed whole to a presentational component (`Web/design/presentational.test.ts` covers `work/` and `asks/`).
*(C6.1, C2.5, C2.6)* Round 2's `coworkers` group, `channel`, `sitting` and `at` are gone from it: the first three are read beside it
(§3.9), and a composition moment would make every answer differ from the one held, so an unchanged answer would be told as moved.

**Row a's type** *(round 4: the scope this round settles)*. It carries nothing a call line alone moves (O3-F5), and nothing of
responsibility, waits, quests or co-workers:

```ts
type AtWorkHeld =                                 // what the page's query holds (O3-F8, C3-4.2, O4-F3)
  | { reading: true }                             // the pending answer, never `undefined`, which the query library fails
  | { failed: { why: string } }                   // a work failed with nothing held, or (C4-2.4) an input slice never judged
                                                  // and failing; never a query error (C4-2.3); the page asks nothing again
  | { answer: AtWorkA; stale?: { why: string } }; // stale: a refresh failed over this answer, or (C4-2.4) an input slice
                                                  // judged once is failing now (§4.1)
type AtWorkA = {
  of: { session: string };                        // row a asks for a session; an ask's view is the routing half's
  attended: string;                               // the part whose subagents are drawn: the one the page attends
  parts: AtWorkPart[];                            // the strip's parts, the attended one first, then the strip's order; at most 40;
                                                  // an attended record in no entry (an ended intake) is the one part (O4-F12c)
  moreParts?: Record<string, number>;             // parts past the bound, counted by the record's state
  subagents: AtWorkSubagents;                     // the attended part's
};
type AtWorkPart = {
  key: string;                                    // the record's id; opens its page
  agent?: string; mark?: string;                  // the maker's mark, never a hue alone
  state: string;                                  // (C4-1.2) the record's own `state`, worded `sessionState.*`; never the
                                                  // strip's shown word, which carries the reader's derived waits; no liveness
  children: { count: number; working: number; observed?: number; unreadable?: boolean }; // its subagents, as for the attended part
};
type AtWorkSubagents = {
  read: 'read' | 'none' | 'unreadable';           // §3.6: no file, read, or could not be read
  unreadable?: string;                            // the error's kind
  harness?: 'claude-code' | 'codex';
  seenByCalls?: boolean;                          // a harness whose children appear only when they call Daoris (Codex)
  working: number;                                // never an `unsaid` run (O4-F1)
  turnWaits?: boolean;                            // `claude-code-acp` alone, a child working: its turn waits for them (O4-F1)
  observed?: number;                              // ids the hook, or a Codex part's calls, named that no announcement matched; never children
  children: AtWorkChild[];                        // at most 40, filled as §4.1's bound has it (C4-4.1): open runs, then lost,
                                                  // then the newest three of the rest, each only with its whole chain of ancestors
  folded?: number;                                // the rest past the newest three, none drawn as an ancestor: *n ended*
  more?: Record<string, number>;                  // every child not drawn for want of room, whatever its state, counted by state;
                                                  // rows, `folded` and `more` together count every child once (C4-4.1)
};
type AtWorkChild = {
  key: string;                                    // its native id
  type?: string;
  label?: { text: string; claim: true };          // the harness's announcement, the agent's words
  parentKey?: string; depth?: number;             // absent where no harness said: *parent not said*
  state: 'working' | 'unsaid' | 'completed' | 'failed' | 'cancelled' | 'disconnected' | 'finished' | 'ended' | 'lost'
    | 'unrecorded';                               // unsaid: open, its harness saying no end (Codex): never working (O4-F1);
                                                  // unrecorded: closed in a driven record's history, its end never written (C4-1.1)
  since: string; startSaid: boolean;              // its run's start, or its first line where no start was said: *first seen* (O3-F1)
  ended?: string;                                 // the end its harness or its process's `end` line said, with its moment
  lastSeen?: string;                              // only for a run rules 2 or 3 closed, an `unrecorded` one, or one begun in a
                                                  // generation already closed (O3-F11e, C4-1.1, C4-2.1)
  opens: { stream?: string };                     // its console tab while the window holds it
};
```

**The routing half's type**, as round 3 wrote it, which the later round extends row a's with:

```ts
type AtWork = {
  of: { session: string } | { ask: string };
  leadPart?: string;                     // the lead's record id, §3.1; never EntryAccepts.EntryLead (R2-O9)
  waitingOn: AtWorkEdge[];               // the lead's current wait, §3.2; one quest at most; none once answered (C2.1)
  groups: AtWorkGroup[];                 // in §3.1's order, an empty group absent; co-workers are read beside it (§3.9)
  hint?: 'not-driven' | 'no-lanes' | 'one-at-a-time'; // §6.4, judged in .NET by the routing standing (C2.5, R2-O11)
};                                       // no composition moment (C2.6)
type AtWorkGroup = {
  kind: 'parts' | 'subagents' | 'quests' | 'askedBy' | 'gathered';
  members: AtWorkMember[];               // at most 40 (C2.6): the lead, the live and working, the lost, then the newest
  folded?: number;                       // ended members past the newest three
  more?: Record<string, number>;         // members past the bound, counted by state (C2.6)
  observed?: number;                     // subagents: starts the hook saw that no announcement matched; never children (C4.1)
  unreadable?: string;                   // subagents: the lineage could not be read, and the error's kind (C4.1)
  seenByCalls?: boolean;                 // subagents: a harness whose children appear only when they call Daoris
};
type AtWorkMember = {
  key: string;                           // the record's own id: a session, a quest, a request, a child's native id
  kind: 'part' | 'child' | 'quest' | 'asker' | 'voice';
  agent?: string; mark?: string;         // the maker's mark, never a hue alone
  on?: { text: string; claim: boolean }; // claim: the agent's words (a child's label), drawn as such
  lanes?: string[];                      // a part's or a quest's lanes
  state: string;                         // the record's own state word; a child's end word as the wire said it
  waiting?: boolean;                     // its quest's current executor, its wait still standing: live, never folded (R2-O3, C2.1)
  answered?: boolean;                    // its quest's current executor, its question closed and its resume not begun (C2.1)
  taken?: 'conversation' | 'elsewhere' | 'unrecorded'; // a quest taken with no executor here (R2-O6, C1.2)
  since?: string; ended?: string;        // a child's: its newest run's (C2.3)
  heard?: { at: string };                // §3.7's fourth: the lineage's newest; the calls slice's `heard` wins (R2-O5)
  alongside?: boolean;                   // a part in a tree of its own, read from its tree (R2-O2)
  children?: { count: number; working: number; unreadable?: boolean }; // a part other than the lead
  routedBy: { door: string; by: 'session' | 'person' | 'ask' | 'chain' | 'harness' | 'workflow'; detail?: string };
  waitsOn?: AtWorkEdge[];
  depth?: number; parentKey?: string;    // a child's place in the tree; absent where no harness said
  opens: { session?: string; quest?: string; stream?: string; meeting?: string };
};
type AtWorkEdge = {
  to: string;
  kind: 'helper' | 'quest' | 'person' | 'untaken' | 'partner';
  label: string;                         // untaken: why it sits is the tick's, read beside (§3.9, C2.5)
};
```

Beside its answer each composition keeps, never sent, **its dependencies**: every input it read with the version it read (§4, C2.4,
O3-F2, C3-2.4).

### 3.9 What the view reads beside the read (routing half)

*(C6.1, C2.5.)* Each of these already has a reader, a wake and a presentation, and the view composes them with the read's answer
rather than reading the same records again:

| Block | Its reader, as it stands | What the view adds |
|---|---|---|
| *Co-workers* | D203's exchange read of the line, COWORKUX1a's (the coworker design §6.3), with its rows (§6.2) | nothing; its rows drawn read-only, opening the member's thread (COWORKUX1b) or, until it is built, the member's own session page (C5.3). Which helper a lead waits on is that read's open ask whose asker is the lead |
| why a quest sits | the tick's `considered`, held as `useConsidered` (`Web/bridge/driver.ts:337`) and told by `DRIVER_TICK` (`DriverLoop.cs:1027`), as the ask's work reads it (`AskWork.tsx:56`) | the sentence on an untaken quest's row, and on an answered part whose resume sits |
| the channel | the session page's `ChannelPanel` read (`AttendedSession.tsx:323-324`), the ask page's channel section | its newest three lines, placed on the member that posted each |
| *last heard*, *quiet* | the `calls` slice's `heard` and `quiet` partitions (§3.7) | nothing |
| a meeting or a brainstorm | its page (MEETUX1b, MEETUX1c), by link | its row's state, from the read (row f) |

Each verdict among them is judged in .NET once, as D182 point 6 has it: the tick's sentence is the planner's, the hint's standing
the routing judge's (§6.4). The page renders them.

## 4. How it updates without the window asking (NOBLOCK1)

### 4.1 Row a's read, whole (round 4; round 5's changes marked)

*(O3-F2, O3-F5, O3-F8, C3-1.4, C3-2.4, C3-4.2; C4-2.2 to C4-2.4, C4-3.1, C4-4.1.)* This is the whole of row a's update contract.
§4.2 is round 3's text, kept as the routing half's draft; where they differ, this holds for row a. §4.3 says where its code sits.

- **One read, `SESSION_AT_WORK`, `[ByEvent]`, keyed by a session.** `WindowCensusTests` gains it as told by event.
- **Its inputs are in memory or on this machine's disk; none is a service read** (C3-1.4). They are:
  - the entry's parts, from the `groups` slice's snapshot, its `entries` partition, which is the strip's own (§1.8); an attended
    record in no entry, an ended intake whose ask became quests, is its own one part (O4-F12c);
  - each part's record, from the `sessions` slice's snapshot: only the fields the answer reads, its state, adapter, `created`,
    *(C4-1.1)* `kind` and maker's mark, never its `note`, `ran` or `updated`, which move often (O4-F12d);
  - which parts have a process running in this window, from the driver slice's `running` (O4-F6: this window's, never the
    machine's, §1.8);
  - *(C4-2.4)* each of those three slices' health, beside its snapshot (below);
  - each part's lineage file, read by name.

  So row a needs no burst owner for service reads: the slices judged them once for every window. Until the `groups` and `sessions`
  slices have judged, the read answers pending, never an empty answer (D182's PUSHUX1), unless the one not judged is failing
  (C4-2.4).
- **An input's health** *(C4-2.4)*. A slice that fails only logs and keeps what it judged (§1.8), so the read could not tell a
  failed input from a pending or a current one. Each slice gains its health, read from memory under its gate beside `Snapshot`
  (`StateSlices.cs:170`): whether its last judgement failed, and that failure's words. It tells `HealthMoved`, outside the gate
  and after its snapshot holds what the judgement left, whenever a judgement's failure begins or ends (its failing flag flipping,
  `:207-220`), even where the judgement pushed nothing. The read takes each input slice's health with its snapshot, and no
  service call:
  - not judged, not failing: pending, as above;
  - not judged and failing: `{ failed: { why } }`, the slice's own words (*Who is at work could not be read: {{why}}*);
  - judged and failing: the answer composed from the snapshot the slice keeps, with `stale: { why }` (*Could not read again:
    {{why}}*, its rows kept, dimmed);
  - healthy again: the next composition carries no `stale` and lands as moved.

  Each slice's health is a dependency, so its `HealthMoved` wakes the read on a first failure, on a failure over a kept snapshot
  and on the recovery alike, with no ask of the page's own.
- **Its dependencies.** Each composition keeps, beside its answer and never sent, every input with the version it read:
  - each lineage file: its stamp, taken before the read (`HomeWrites.Stamp`), or *absent* where there is no file, since the first
    child's line, the root's first call or *(C4-2.1)* a process's `start` creates it; the length read **up to its last full
    line** (O4-F12b: the hook and the connector append from other processes, `lineage-hook.mjs:48-58`, `LineageFile.cs:104-120`,
    and the fold skips a half line, `ChildBindings.cs:335-346`, so the judge reads on from that line's start); the native ids its
    runs hold in each generation; the ids it counted as unmatched; and *(C4-2.2)* the ids the fold read as its part's root
    (Claude Code's session, Codex's threads named with no parent and the root its metadata names, `ChildBindings.cs:139-163`);
  - the entry as the `groups` slice's partition held it, or *absent* where the attended session is in no entry; each part's read
    fields as the `sessions` slice's partition held them; whether each part was in `running`; and *(C4-2.4)* each slice's health
    as it read it;
  - *(O4-F2)* **a pending composition registers too**: where the `groups` or `sessions` slice has not judged, the dependency is
    that slice's not-judged status, so its first judgement's `Pushed` wakes the read and the answer follows with no other ask. A
    view restored open as the window starts so leaves *reading* as soon as the slices judge, not at a focus re-fetch.
- **Checked as it lands: the source fence** (C3-2.4). A composition about to land compares every dependency with its current
  version: a file's stamp, a slice's partition in its snapshot, a slice's health (C4-2.4). Where one moved, it composes again
  before landing, at most three times; the last one lands and asks the judge at once. Its registration is told by `Land` (`HeldAnswers.cs:147-149`), which alone
  knows whether a newer composition superseded it, since the work cannot; a superseded one registers nothing. So an ask that joined
  a work begun before the move it answers, which the bus allows (`DriverModule.cs:106-108`), lands nothing stale, whether the move
  was a file's or a record's.
- **One judge, and each read wakes once** (O3-F2). The judge holds the union of the registered dependencies, and is asked:
  - by the home watch's one topic (`HomeWatch.ForWork`, the window's own process, whoever holds the home's lease): one fixed part,
    the `sessions` folder and its subfolders hearing only `*/lineage.jsonl`, its judge reading by name only the union's files, a
    file registered absent among them. `HomeWatch`'s topic judge is fixed today: it compares with its own `Last` and passes over
    this process's marked writes (`HomeWatch.cs:316-344`, `:332`). So `ForWork` adds a topic whose judge is the read's own, given
    the topic's heard paths and asked at its deadline, which compares with the registered versions;
  - by the `groups`, `sessions` and driver slices' `Pushed` (`StateSlices.cs:378`), whose hearers compare only the partitions
    and the `running` entries the union names, and *(C4-2.4)* by their `HealthMoved`, which compares the health the union names;
  - at once, when a composition registers.

  It compares each dependency with **the version its composition registered**, never with a baseline of the watch's own
  (`HomeWatch.cs:331`, `:337`). Where any of one read's dependencies moved, it tells `atWork` once, and **that read's
  dependencies leave the union**. They come back only when that read is asked again and a composition of it lands. So:
  - a held answer no page asks again (`HeldAnswers` keeps 128, `:40`; the hub asks only mounted queries, `stateHub.ts:80`) wakes
    once and then never again, however its files move;
  - a mounted view whose files take three appends during one composition is asked once more, not three times.
- **What a lineage line wakes for** (O3-F5). Where a file's stamp moved, the judge reads the bytes past the registered length:
  - any line but a call wakes: a start, a stop, an `acp` line, a process's `start` or `end`;
  - a call line, the connector's or the hook's `event: call`, wakes only where it names a native id the composition holds neither
    as a run in that generation, nor among the ids it counted as unmatched (O4-F5), *(C4-2.2)* nor as its part's root, since only
    then does it begin a run or a new count (§3.6). Otherwise it moves nothing the answer carries (§3.8 carries no call count and
    no working run's last moment), and the judge registers the new length and stamp as read, waking nothing;
  - *(C4-2.2)* **a root's call names no child.** The fold reads a Claude Code call as the root's where the hook's line for it
    names no agent (`ChildBindings.cs:190`, `:217-219`), and a Codex call as the root's where it names a thread with no parent or
    the root its metadata names (`:154-162`, `:223-227`); neither ever begins a run. So a call the fold would read as the root's
    wakes nothing, however often it comes, a root thread the composition has not seen included, since a thread named with no
    parent is a root by that rule. The one call naming a root that wakes is a Codex call naming, with no parent, a thread the
    composition holds as a child run, since the fold then moves that thread to the roots;
  - a file shorter than its registered length wakes.

  So the lines a call writes (two on Claude Code, one on Codex, §1.1) never tell `STATE_CHANGED`, the cost R2-O5 kept *last
  heard* from, unmatched ids' calls included.
- **The path to the page** *(O4-F10: copied here, so row a borrows nothing from §4.2)*. `HeldAnswers` runs a read's work only when
  the page asks, so every wake goes through the page's one hub:
  1. A wake raises `DAORIS.STATE_CHANGED` with a new scope, **`atWork`** (`work` is already the pushed slice's name,
     `WorkStateSlice.cs:45`), **naming no session** (`StatePublisher.Publish(bus, StateScopes.AtWork)`, a new bit after
     `Notices`): no wake carries the session it is about (R2-O1).
  2. The hub's `atWork` entry asks every mounted `SESSION_AT_WORK` read again, whatever it is keyed by (`stateHub.ts:27`,
     `:93-100`).
  3. The query asks again; `HeldAnswers` answers what it holds at once, runs the work on its own task (or joins the one under
     way), and tells `DAORIS.ANSWERED` where the answer moved, *(C4-3.1)* the reply and the event each numbered (below).

  In row a the hub's `sessions` entry does not name `SESSION_AT_WORK`: a press that moves a session moves the `sessions` slice,
  whose `Pushed` reaches the judge, so a second invalidation would only ask twice. `quests` and `asks` are no input of row a's.
- **The terminal's twin** *(O4-F11)*. `daoris-driver sessions at-work <session> [--json]` asks the running Daoris over its pipe,
  which answers a held read by asking until it is not pending (`Modules/Platform/InstancePipe.cs:1061-1080`). With no window
  running, it composes the same answer with the same .NET function (D182 point 6), its inputs read under the home's lease: the
  service's records and `SessionEntries.Read` for the entry, `SessionProcesses.AliveOnThisMachine` for a part's process, and the
  lineage files. Its tests cover both.
- **No other wake.** Row a reads no quest, ask, channel, registry, standing or meeting, so none of them wakes it; the routing half
  adds its own (§4.2).
- **Off the window's thread** (C2.6). A composition is a fold of the entry's few lineage files, on `HeldAnswers`' task. Each list
  carries at most 40, counted past that (the bound, below). `HeldAnswers` serialises a work's answer on the work's own task before
  it takes its gate, comparing under the gate only the text already made (today it serialises under the gate,
  `HeldAnswers.cs:145-160`).
- **The bound** *(C4-4.1, replacing O3-F11c's "every working and lost child, then the newest", which forty-one working children
  could not keep)*. A list carries at most 40 rows. The subagents' rows are filled in this order, each child taking a row only
  together with every ancestor it has among the part's children that is not drawn yet, and only where that whole chain fits in
  the rows left; a child whose chain does not fit is passed over, and the filling goes on with the next:
  1. the open runs, `working` then `unsaid`, the newest start first;
  2. the `lost`, the newest last seen first;
  3. of the rest (ended by a word, `finished`, `ended`, `unrecorded`), the newest three.

  The rest past those three that no drawn child needs as an ancestor are `folded`, drawn as *{{count}} ended*. Every child not
  drawn for want of room, whatever its state, is counted by its state in `more`, drawn as *{{count}} more not drawn* or, where any
  is working, *{{count}} more not drawn, {{working}} of them working*. So a drawn child's ancestors are always drawn and a chain
  longer than 40 never is; the heading's *n working* counts every working child, drawn or not; and the rows, `folded` and `more`
  together count every child once. The parts list is bounded the same way, without chains: the attended part first, then the
  strip's order, the parts past 40 counted by their record's state in `moreParts`, drawn as *{{count}} more parts*.
- **Pending, failed and stale** (O3-F8, C3-4.2; made whole by O4-F3 and O4-F12a):
  - the page's reader maps the pending answer to `{ reading: true }`, never `undefined`, which TanStack Query 5 fails
    (`src/Daoris.Web/package.json:30`) and the app retries once (`Web/main.tsx:31`); the view draws it as reading;
  - *(O4-F3; round 4's "answered once as its refusal" corrected)* today a failure is never answered once: `answered` asks a
    failed query again (`held.ts:45-47`), `Land` has cleared the flight (`HeldAnswers.cs:147`), so the next ask begins a new work
    (`:88-91`) and throws the fault (`:96-100`), and a work that always fails is begun again at every failure. And the event is
    `{ held, failed }` (`:176`), carrying no words. So **both failure events carry the refusal's words**: `Land` tells `ANSWERED`
    with `failed`, `why` (the fault's message, the host's words), and `kept: true` where an answer is held. This read's reader
    writes `{ failed: { why } }` where nothing is held and `stale: { why }` beside the held answer where one is, and **asks
    nothing on either**. *(C4-2.3; round 4's "*Try again* is the only re-ask" corrected)* No failure makes the page ask again;
    the asks after one are those after any answer, a wake, the query's re-fetch on focus past its `staleTime` (`main.tsx:30`),
    and *Try again*. So a work that always fails is begun once per ask, never once per failure. Every other held read keeps
    today's handling, which this changes for none of them;
  - *(O4-F12a; round 4's "the next answer clears `stale`" corrected)* an ask answered from the held answer (`held.ts:30-31`)
    keeps `stale`, and `Land` tells the first landing after a kept failure even where it equals the held answer
    (`HeldAnswers.cs:155-160` tells nothing for an equal one today), which clears it. So *stale* never flickers off at a wake
    during a lasting failure. The view keeps its rows, dimmed, with *Could not read again: {{why}}* and *Try again*. *(C4-3.1)*
    The numbers below make this hold whatever order the deliveries come in.
- **A numbered read** *(C4-2.3, C4-3.1)*. Round 4 changed the events alone, so a refusal answered directly still reached the page
  as a route's error and was retried, and an older landing told after a newer one still won (§1.8). `SESSION_AT_WORK` is held as
  a *numbered* read, the only one; every change in this point, and each of O4-F3's and O4-F12a's above, applies to numbered reads
  alone, and every other held read keeps today's handling.
  - **Never a refusal** (C4-2.3). Its ask never throws. A work that fails within `Patience` is answered `{ failed: { why } }` with
    its number (`HeldAnswers.cs:104-105` throws it today), and a fault that landed with nothing held is answered so at every ask
    until a landing replaces it, never thrown once (`:96-100`). Where an answer is held and the newest landing failed over it, the
    reply is the answer with `stale: { why }`, so a window that asks first, never told the event, reads it stale too. A pending
    reply carries no number and keeps what the query holds. The query function so never rejects for a work's failure, and the
    query sets `retry: false`, so a refusal of the bridge itself (`call`'s own failure, a window closing) is not asked again
    either: the view draws a query in error as *failed*, with its error's words.
  - **Numbered** (C4-3.1). `HeldAnswers` keeps an epoch of its own, as a slice does (`StateSlice.Epoch`, `StateSlices.cs:128`),
    and a landing's number is its work's `begun` (`HeldAnswers.cs:90`), drawn from one counter that only grows. Every reply and
    every `ANSWERED` of a numbered read carries `{ epoch, n }`: an event its landing's number, a reply the number of the held state
    it answers from (the newest landing, `Landed`, `:150`), or of the work it waited for within `Patience`. For each numbered
    query the page keeps the newest number it took; a reply or an event of the same epoch with a lower number is dropped, the
    query keeping what it holds, an equal one is the same landing, and one of another epoch replaces the number kept. So a landing
    told after a newer one, which `Land`'s telling outside its gate allows (`:172-177`), and a direct reply arriving after a newer
    event regress nothing: no old rows come back over new ones, and no old failure over a recovery (D182 point 3, `D182.md:24`,
    as the slices have it).

### 4.2 Round 3's text: the routing half's draft (a later round)

Kept as round 3 wrote it, for the routing half to start from, with round 4's corrections marked. It settles nothing for row a.

- **One read, `SESSION_AT_WORK`, marked `[ByEvent]`**, keyed by its payload (a session or an ask), as NOBLOCK1 settles for reads
  asked per payload rather than one owner's snapshot (`D56.md:186-189`). It answers at once from what is held, or `{pending: true}`
  while nothing is held. *(C4.2)* Its page reader reads the pending answer as not answered yet (`undefined`), never as an empty
  answer, as `held.ts`'s default readers do (`held.ts:20-34`), so a first ask draws §5.2's reading state and never *nobody*.
  *(Corrected in round 4, O3-F8: `undefined` fails the query; the marker is `{ reading: true }`, §4.1.)*
  `WindowCensusTests` gains the route as told by event.
- **The path from a wake to a held read.** `HeldAnswers` runs a read's work only when the page asks, so every wake goes through the
  page's one hub:
  1. A wake raises `DAORIS.STATE_CHANGED` with a new scope, **`atWork`** (R2-O9: `work` is already the pushed slice's name,
     `WorkStateSlice.cs:45`), **naming no session** (`StatePublisher.Publish(bus, StateScopes.AtWork)`, a new bit after `Notices`).
  2. The hub's `atWork` entry asks every held `SESSION_AT_WORK` read again, whatever it is keyed by (`stateHub.ts:27`, `:93-100`).
     *(R2-O1; round 2's narrowing by session withdrawn.)* No wake can carry the session it is about: a ledger wake's subject is a
     list of families (`QuestsStateSlice.cs:132-137`), and a home topic's wake carries its fixed subject, never the file that moved
     (`HomeWatch.cs:342`). And a narrowing would miss reads that hold the part that moved: part X's read draws part Y's state, its
     children's count and its *last heard*, and an ask's read is keyed by the ask. These reads are few (the attended views and the
     head), and the hub's `askAgain` and `HeldAnswers` already coalesce them.
  3. The query asks again. `HeldAnswers` answers what it holds at once, runs the work on its own task (or joins the one under way),
     and tells `DAORIS.ANSWERED` where the answer moved.

  The hub's `sessions`, `quests` and `asks` entries also invalidate it, since a press that moves any of those moves a member.
- **The wakes**, each telling `atWork`:
  - **The ledger's families**: `sessions`, `quests` and `asks`, heard on `DriverLoop.MoveHeard` by the existing
    `QuestsStateSlice.HearLedger` seam (`QuestsStateSlice.cs:130-144`). A connector's write (a quest published, a take, a wait) is
    told with no press. *(C6.1)* `channel` is no longer one: the read carries no message (§3.3).
  - **The files the compositions read** *(C2.4)*, heard by one watch of the window's own process, as `ForProposals` and `ForNotices`
    are, and whoever holds the home's lease (`HomeWatch.ForWork`). It has one topic with fixed parts, none added or dropped: the
    `sessions` folder with its subfolders, hearing only `*/lineage.jsonl`, and, from row f, the brainstorms' folder with its
    subfolders, hearing only `*/request.json` and `*/state.json` (`MeetingFiles.cs:29-38`, C5.5). *(C6.1)* The helper and partner
    boxes are no longer parts of it: the co-workers are read beside (§3.9). A move tells `atWork`, naming nothing (R2-O1).
    *(C3-5.6, for the routing half's round.)* A request made after the view is held changes no stamp it read: meetings are found by
    listing their folder (`MeetingFiles.cs:76`). So row f's read also depends on the listing of `brainstorms/` and
    `brainstorms/meetings/`, their names as it read them, and a new request's folder wakes it; its proof creates a request after
    the view is held, with no ledger write.
  - **Its dependencies, each at the stamp it was read** *(C2.4)*. A composition reads files by name only: the lineage of **every
    part it folds**, the lead's for its rows and each other part's for its count, every entry's parts' for an ask's read, and row f's
    requests and states. It keeps each file's path with the stamp it took **before** reading it (`HomeWrites.Stamp`), as its
    dependencies (§3.8). The topic's judge reads by name the union of what the held compositions keep, and compares each file's
    stamp **with the stamp its composition read**, never with the first stamp the watch itself takes, which `HomeWatch` takes as
    read and wakes nothing for (`HomeWatch.cs:331`). A composition that lands asks the topic to judge at once, so a line appended
    between its read and its registration wakes it then, not at the deadline. Two more cases fall out of the same rule:
    - an ask that joined a work begun before the change it answers, since the bus does not wait for `Forget`
      (`DriverModule.cs:106-108`), lands stamps older than the files', and is woken again;
    - a held read let go (`HeldAnswers.Kept`, the least recently asked first) drops its files from the union.

    The watch is bounded by what the held reads depend on, and its listing never walks every session's folder. *(Round 4,
    O3-F2, C3-2.4: as written, a dependency compared with its composition's stamp wakes again at every judgement until that read
    is composed again, and for good where no page asks it again; and records were no dependency, so a ledger-only stale landing
    went unseen. §4.1's registration check, one judge and wake-once replace this for row a, and the routing half takes them for
    its own inputs.)*
  - **The routing standing** *(C2.5)*. The hint's standing is judged in .NET (§6.4) from the driver's drive and trees, read from the
    driver slice's snapshot in memory (`DriverStateSlice`, whose `drivable` and `trees` a `driver.json` edit moves at the look,
    `DriverLoop.cs:987`), and from the registry's lanes, read with the burst's service reads as the intake reads a repository's
    declaration (`DeclarationView.Lanes`, `Driver/Quests/Domain/Intake.cs:90-102`). The driver slice's `Pushed`
    (`Modules/Platform/StateSlices.cs:378`), filtered to `drivable` and `trees` moving, tells `atWork`, as the pushed slices'
    pattern for a fact another slice holds has it (`.claude/knowledge/pushed-slices.md`); so does a registry the tick heard move
    (`RegistryHeard`, `DriverLoop.cs:1019`, `:1321-1326`). The tick's considerations need no wake of the read: the view reads them
    beside it (§3.9). *(O3-F3, C3-1.2, for the routing half's round: as written this cannot hear lanes.)* The registry's signature
    is name, adoption, workspace and root (`Planner.cs:722-736` at `b4f096cc0`), and no change family carries the registry. So
    `Repositories.Signature` gains each repository's declared lane ids, read with the tick's registry read
    (`DriverLoop.RegisteredAsync`), and the hint's read keeps the lanes it read as a dependency compared at each tick; a lanes-only
    registration then moves the hint with no other write, which row e proves.
  - *(R2-O5)* **Not what the window hears.** A console stream's newest line is no wake of the read: *last heard* is pushed by the
    `calls` slice's `heard` partition (§3.7). Told through `STATE_CHANGED` at up to once per ten seconds per working session, it
    would make `DriverModule` forget every shared flight and held work at each telling (`DriverModule.cs:102-107`), and every
    window would re-ask, so the joining NOBLOCK1's heavy reads rely on would weaken at the agents' rate.
- **Coalesced.** Wakes within one judgement compose once: the hub's `askAgain` and `HeldAnswers` joining the work under way each
  hold it to one read per burst, and the answer is told only where it moved.
- **The clock is the page's.** *for 4 min* and *1 min ago* are drawn from `since` and `heard` by the page's clock, never re-sent.
- **Off the window's thread.** The lineage read is a file read of one session's lines, folded in memory. The quest units are built
  by `QuestUnits.Read` (`QuestUnits.cs:20`), never by `WorkUnits.ReadAsync`, which reads every branch's inventory, and never by a
  service call per member. *(R2-O6; round 2's "the quests and sessions the driver holds" corrected.)* No route holds its inputs:
  the loop's snapshot keeps open and taken quests and active sessions only (`Planner.cs:888-891`, `:1770-1771`), not the closed
  quests and ended parts the view draws. So a composition needs **two whole-store service reads**, every session record and
  every quest (`SessionRecordsAsync`, `EveryQuestAsync`, as `HelpRoutes.cs:293-294` and `WorkUnits.cs:66-71` read them), on the held
  work's task. *(C6.1)* **They are taken once per burst for every held read**: the compositions of one burst join one shared flight
  of each (`RouteFlights`, forgotten at every `STATE_CHANGED` with the held works, `DriverModule.cs:106-113`), and each payload
  composes its own answer from them, the registry's declarations read once beside them for the hint (C2.5). So a wake costs those
  reads once, however many `SESSION_AT_WORK` reads are held. *(C3-1.4, for the routing half's round: `RouteFlights` drops a flight
  as it completes, `RouteFlights.cs:92`, `:101`, so reads that do not overlap share nothing.)* The routing half names an owner that
  keeps a burst's inputs: a snapshot of the two reads, taken once per `atWork` wake's generation and kept until that
  generation's last composition lands, and tests a burst of compositions that each complete before the next begins. Row a needs
  none of it: it reads no service (§4.1).
- **Bounded, and never serialised under the ask's gate** *(C2.6)*. Each group carries at most 40 members, the rest counted by state
  (§3.8), so an answer's size does not grow with a long process's children or an ask's quests. And `HeldAnswers` serialises a work's
  answer on the work's own task before it takes its gate, comparing under the gate only the text already made, so an ask on the
  window's thread never waits on a serialisation (`HeldAnswers.cs:145-160` serialises under it today). Row a makes that change,
  which every held read shares.

### 4.3 Where row a's code sits (round 5)

*(C4-3.2.)* FOUNDATION1 Part A, accepted (D212), maps the driver's subdomains bottom to top, each naming only its own types and
those listed before it; where a lower one needs a higher one, the lower declares a port, the higher implements it, and Loop wires
it (`docs/2026-10-25-foundation-design.md` §14.2, §14.3). Its §5.1 has row a wait for nothing there, and row a needs nothing of its
Part B: no journal, move record, desktop store or routing contract. Row a's pieces sit where the map puts what each extends:

| Piece | Subdomain (its place in the map) | Why there |
|---|---|---|
| the lineage writer's port: what an announcement and an end say, a type of its own, handed to `AcpStreams` | Agents (9) | `AcpStreams` is under `Agents/`; it names the port, never `ChildLineage` |
| the same port handed to the stream watcher in place of `lineageHome` (`HarnessCapture.cs:111-123`) | Agents (9) | `HarnessCapture` calls `ChildLineage.Watch` today, an edge up into Participants, which this cuts |
| the port's implementation, writing each line with the session and the process's token bound, and `start` and `end` | Participants (12) | `Sessions/ChildLineage`, `Domain/ChildBindings` and the hook are Participants' |
| the fold's generations and runs, and the composition of the answer, the one .NET function the read and the verb share | Participants (12) | beside the fold it reads; it takes the records, the running set and the health as plain values |
| issuing the token, setting it on the spawn, and handing each process its writers, at the four spawns | Loop (21) | the look's runs and the chat runner's core are Loop's, and Loop wires a port a lower subdomain declares |
| the `ForWork` topic, its judge a function it is handed | Events (7) | `HomeWatch` is Events'; it names no type of the read's |
| each stream's newest moment (§3.7) | Context (13) | `Domain/SessionOutput` is Context's; the `calls` slice in the modules reads it |
| `sessions at-work` | Work orders (15) | `Sessions/SessionsCommand` is Work orders', above Participants, so it calls the composition |
| the held read, its numbering, the slices' health, the hub's scope and the page | the modules and the web | outside the map (its §14.2's last paragraph) |

So row a adds no edge up the map, and the one it touches, the stream watcher's, it cuts. Where a Part A move has landed before row
a, row a's files are in their new folders; where none has, they are where today's code has them, and the move carries them.

## 5. The view

### 5.1 Where it is

*(Round 4.)* Row a builds the side bar view in a session, the head's fragment (§3.7) and the *Parts* strip's place beside it; the
ask's section, the co-workers' rows and the *Workflow* link are the routing half's.

- **In a session**: a side bar view **At work** / **分工**, `atWork` in `ViewId` and `VIEW_IDS` after *workflow* (`placements.ts:9`,
  `:22`). It sits on the right by default, and is movable to the panel as every view is (D118's frame). The head's new fragment
  (§3.7) and the waiting row's *waits on* open it.
- **In an ask**: the same component as a section of the ask's page above *Quests*, titled the same.
- **D203's *Co-workers* section in the head stays** the place of the presses on a co-worker (*Let it ask*, *Not now*, *Stop…*), with
  `data-wait-section` (`.claude/knowledge/person-waits.md`). The view shows co-workers as that section's own rows, read-only, from
  the same exchange read (§3.9, C6.1), opening each member's thread once COWORKUX1b has built it and its session page until then
  (C5.3), and carries no second set of those presses.
- **The *Parts* strip stays** the place of each part's presses. The view's part rows open the part and carry no press. The strip
  and the view list the same parts, each once; the strip orders them as lineage, oldest first, and the view lead, live, ended
  (R2-O7, §2 point 4).
- **The graph's half is FABRIC-GRAPHVIEW1's running view**, in the *Workflow* view. A session's run's nodes are not redrawn here, and
  the flowchart design's rejection of a second running view stands. The view links to it where the session has a run (*Its
  workflow →*). Its quest rows read the same units, so the two views agree on a quest's state.
- **Help**: Ask Daoris's room learns the view's name in its prose list of views (`placements.ts`'s remark; `Help.cs`'s *The window*).

### 5.2 What it draws

**Before an answer, and when none came** *(C4.2)*, following D182's PUSHUX1 note (`D182.md:559-575`: not answered is not none):
- **Reading.** The first ask's pending answer, held as `{ reading: true }` (§4.1, O3-F8), draws the view's blocks as a skeleton,
  `aria-busy`, with *Reading who is at work…*; never *Nobody else is working on this*, never a zero, and never the query's error.
- **Failed** *(O4-F3; C4-2.3, C4-2.4)*. A work that failed with nothing held, whether within the patience or after it, an input
  slice never judged and failing, and a query in error are each told with their words, held as `{ failed: { why } }` (§4.1): the
  view says *Who is at work could not be read: {{why}}*, the host's words passed through, with the shared *Try again*. No failure
  asks again by itself.
- **Held.** An answer held is drawn as it is while the next is composed.
- **Stale** *(C3-4.2, O4-F3, O4-F12a; C4-2.4)*. A refresh that failed over a held answer, and an input slice failing since it was
  judged, are told with their words (§4.1): the view keeps its rows, dimmed (`opacity-60`, `aria-busy` off), with *Could not read
  again: {{why}}* and *Try again*. An ask answered from the held answer keeps it; the first landing after the failure clears it,
  even an unchanged one; *(C4-3.1)* a delivery older than the one drawn changes nothing.
- **In part.** A part whose lineage could not be read says so in its place (§3.6), and the rest is drawn.
- **Nobody** *(O3-F9, C3-4.1; O4-F9)*. *Nobody else is working on this* is drawn only where the read answered, *(C4-2.4)* not
  stale, every part's lineage was read or recorded none, no part but the attended one is listed, no child and no unmatched id is
  held, **and the
  attended part's harness shows its children other than by its calls**. It is withheld, and the *appear only when they call
  Daoris* line drawn in its place, for a harness seen only by its calls, read from the record's `adapter`
  (`SessionsStateSlice.cs:41`): a Codex lead with no lineage yet meets every other condition. The same holds for any door rule 1's
  measure finds shows no child, such as a protocol session started without streams (`Acp.cs:1756-1770`). Where the unmatched ids
  are all there is, their count stands in the sentence's place.

**Row a draws, from top to bottom** *(round 4)*, each block absent where it has nothing to say:
1. **This session**: the entry's parts as the strip lists them, the attended part first: mark · agent · *(C4-1.2)* its record's
   own state (`sessionState.*`; where the strip shows a derived word, the view shows the record's) · *last heard* from the `calls`
   slice where it has one · its subagents' count (*2 subagents · 1 working*), each row opening its part, and past 40 *{{count}}
   more parts* (C4-4.1). No part is folded or called live, waiting or the lead here: that is the routing half's.
2. **Subagents** of the attended part, as item 3 below has them, with *first seen {time}* for a child no harness said the start of
   (O3-F1) and *last seen {time}* beside *lost* or *ended with its part* where Daoris's reading closed it (O3-F11e). *(O4-F1)* An
   open run whose harness says no end reads *first seen {time} · its end is not said*, in the status palette's neutral word, never
   *working*, and is counted in no *working*. *(C4-1.1)* A child closed in a driven record's history reads *its end was not
   recorded · last seen {time}*, neutral too. *(C4-3.3)* A child whose only end is its hook's stop reads *finished {time}*, never
   *completed*. *(C4-4.1)* Under the rows, *{{count}} ended* for the folded and *{{count}} more not drawn, {{working}} of them
   working* for the children the bound left out.

**The routing half's view**, as round 3 wrote it, from top to bottom (each block absent where it has nothing to say):

1. **Waiting on** (§3.2): one line, the lead's current wait; *nothing* is not drawn, nor a wait already answered (C2.1).
2. **This session**: the lead's row (mark · agent · *working* · *last heard 1 min ago* · its quest and lane, or *chat*), then each
   other live part (*alongside, in a tree of its own* where it is, its lane, its subagents' count), a waiting or answered part among
   them and never folded (R2-O3, C2.1), then ended parts behind *{{count}} earlier parts*, a record its quest's resume replaced
   among them.
3. **Subagents**: one row per child of the lead as a tree, indented by depth: *type · label · working · for 6 min*, or its end word
   and time (*completed 14:02*, *failed 14:05*), or *ended with its part*, or *lost*, each its newest run's (C2.3). Finished
   children fold behind *{{count}} ended* past the newest three; a working or lost child never folds. The heading carries
   *{{count}} working*. A note under the heading says what the person is looking at: *Subagents work inside this session's own agent
   and on its account: they stop when it stops.* Beside it is the count of agent ids the hook named that no announcement matched,
   where there are any (§3.6, C4.1), or, for a Codex lead, the line that its subagents appear only when they call Daoris; and *its
   subagents could not be read* where its lineage was unreadable. On the protocol door, with a child working, the heading adds
   *its turn waits for them* (R2-O4); *(O4-F1)* on `claude-code-acp` alone, the one adapter CONSOLE2 measured holding the turn
   (`docs/2026-09-28-console2-streams-evidence.md:74-77`), and never for an `unsaid` run, which is not working.
4. **Co-workers**: D203's rows from its exchange read, read-only here (§3.9).
5. **Asked through quests**: one row per quest: *#a1b2 · core lane · {title}*, then its session (*Claude Code · working · 12 min*,
   or *not taken yet: {the tick's reason}*, or *taken, by no session recorded here*), then its own *waits on*, then its routing in
   the faint line.
6. **Asked by**: one row.
7. **Gathered**: once row f lands, each row a link to its meeting's or brainstorm's page (C6.1).
8. **On the channel**: three lines from the panel's own read (§3.3), *All messages* →.
9. **The hint** (§6.4), where it holds.

Every state word is a status from the glossary's kinds, with the status palette and a word or icon beside every hue, never a hue
alone (D41). Every row is one button reaching the member, in the order drawn, so the keyboard and a reader meet them as the eye does.

### 5.3 At each width, theme and language

The side bar is 45% of the window, never under 300 px, and the whole frame under 768 px (`Web/work/frame/layout.ts:44`). A region
lays out by its own width (`docs/2026-09-19-platform-ux.md:304-311`).

| The view's width | Where | How it lays out |
|---|---|---|
| **about 696 px** | the install's 1546 px window, at 45% | the wide row: mark, who, on what, state and since on one line; routing on a second faint line; the channel's lines whole to two lines |
| **430 px** | a roomier side bar (the 888 px window gives about 400) | the narrow row: who and state on the first line, on what on the second (two lines, then *…* with the whole in the tip), routing behind the row's tip; the tree's indent capped at two steps, deeper children said by *depth 3* |
| **300 px** | the side bar's floor (`DOCK.floor`), where zh is narrowest | the narrow row with the state word under the name where the two do not fit on one line; the tree's indent capped at one step; the channel's lines to one line, whole in the tip |
| **680 px** | the whole frame (under 768 px) | the narrow row at the frame's width; the side bar's view replaces the conversation while open, its close returning to it, as every view does there |

- **Themes**: tokens only, both themes' status colours, checked by the tokens gate. A lost child is drawn in `--warn` with its word,
  never red alone.
- **Languages**: every label is in both catalogues. zh lines are shorter by count but wider per glyph, so the narrow row wraps on the
  measured box, never by character count (FLOWCHART1e's measured-box check is the gate's precedent).
- **Stories** in the side bar at 300 and 430 px, as the side bar's views are storied (`AskPanel.stories.tsx:335-341`), and as the whole
  frame at 680 px, in both themes and both languages. One story per state; **row a's are the first fourteen** (round 5 adds two
  and widens three, marked):
  - a chat with three subagents (one working, one completed, one failed);
  - a nested child with *parent not said*;
  - a Codex lead whose subagents appear only when they call Daoris, one of them *first seen 14:02 · its end is not said* and
    counted in no *working* (O3-F1, O4-F1); and a Codex lead with no lineage yet, its *appear only when they call Daoris* line
    standing where *Nobody* would be (O4-F9);
  - a lost child;
  - a child beside two agent ids its hook named that no announcement matched (C4.1);
  - *(O3-F9)* a part whose subagents hold only unmatched ids: their count, never *Nobody else is working on this*;
  - *(C2.2, C2.3, O3-F11e)* a child the application's crash left open, *lost · last seen 14:05*; and a child started again,
    *working*;
  - *(C4.2, C3-4.2)* reading, failed, stale over a held answer, and a part whose lineage could not be read; *(C4-2.4)* and failed
    and stale from an input slice's failure, with its words;
  - an entry of three parts, the attended one first, each with its subagents' count; *(C4-1.2)* one of them a record the strip
    words with a derived word, drawn with its record's own state;
  - *(C4-4.1)* the bound: forty-one working children, forty rows and *1 more not drawn, 1 of them working*, the heading *41
    working*; a drawn child under its whole chain of ancestors; and *{{count}} ended* for the folded;
  - the empty view, which says *Nobody else is working on this* / *没有其他成员参与*;
  - the head's fragment, *3 working with it · last heard 1 min ago*, and *quiet* in its place;
  - *(C4-3.3)* a child whose only end is its hook's stop, *finished 14:02* / *已结束 14:02*, beside one the wire ended *completed*;
  - *(C4-1.1)* a driven record's history of a first run and a continuation, one child *its end was not recorded · last seen 14:05*
    beside one its harness ended.

  The routing half's, for its round:
  - a session with two lane parts alongside and one untaken lane quest with its reason;
  - a lead waiting on its own lane part, drawn as the lead and never folded (R2-O3);
  - *(C2.1)* a lead whose question was answered and whose resume sits, *answered*; and the resumed record as the lead, the one
    that waited folded among the earlier parts;
  - a quest taken by a conversation, one taken on another machine, and one taken by no session recorded here (R2-O6, C1.2);
  - a quest to another repository waiting on a question;
  - a lead waiting on a helper;
  - a chat that took a quest;
  - an ask with two entries;
  - each hint.

### 5.4 Words

Keys under `work.atWork.*` (the coworker design §8.3 reserves `work.team.*` for D203's *Co-workers* section, R2-O13a), each a label
with its kind's budget (D116 §3), checked by `names:check -- --strict` and `i18n:check`. A child's end words are the output panel's
own (`work.panel.stream.state.completed`, `failed`, `cancelled`, and a new `disconnected` / 已断开), so **an end the wire said**
reads the same in the panel and the view. *(R2-O10c)* The sameness holds for the wire's words only. A child the driver closed when
its session ended is *ended with its session* in the panel and on the conversation's card (`AcpStreams.cs:134-139`;
`work.panel.stream.state.session-ended`), while the view reads that child from the lineage, as *ended with its part* or *lost when
its session stopped*. The panel's tab keeps its word: it is told by the driver at the session's end, and the view by the part's
`end` line.

| Key | en | zh |
|---|---|---|
| `view` | At work | 分工 |
| `group.parts` | This session | 本会话 |
| `group.subagents` | Subagents | 子智能体 |
| `group.coworkers` | Co-workers | 同事 |
| `group.quests` | Asked through quests | 通过委托请求 |
| `group.askedBy` | Asked by | 由谁请求 |
| `group.gathered` | Gathered | 召集 |
| `group.channel` | On the channel | 频道上 |
| `waitingOn` | Waiting on {{what}} | 在等{{what}} |
| `subagentsNote` | Subagents work inside this session's own agent and on its account: they stop when it stops. | 子智能体在本会话自己的智能体里、用它的账户工作：它停止时它们也停止。 |
| `subagentsWorking` | {{count}} working | {{count}} 个在工作 |
| `subagentsByCalls` | Its agent's subagents appear here only when they call Daoris. | 它的智能体的子智能体只有在调用 Daoris 时才会出现在这里。 |
| `subagentsTurnWaits` | its turn waits for them | 它这一轮在等它们 |
| `observed` | its hook also named {{count}} agent ids it could not match | 钩子另外记下 {{count}} 个无法对应的智能体 id |
| `unreadable` | its subagents could not be read | 读不到它的子智能体 |
| `child.ended` | ended with its part | 随所属段结束 |
| `child.lost` | lost when its session stopped | 会话停止时丢失 |
| `child.noParent` | parent not said | 未说明上级 |
| `child.firstSeen` | first seen {{time}} | {{time}} 首次出现 |
| `child.lastSeen` | last seen {{time}} | 最后出现于 {{time}} |
| `child.unsaid` | its end is not said | 未说明结束 |
| `child.finished` | finished | 已结束 |
| `child.unrecorded` | its end was not recorded | 未记录结束 |
| `child.folded` | {{count}} ended | 另有 {{count}} 个已结束 |
| `more` | {{count}} more not drawn | 另有 {{count}} 个未显示 |
| `moreWorking` | {{count}} more not drawn, {{working}} of them working | 另有 {{count}} 个未显示，其中 {{working}} 个在工作 |
| `part.more` | {{count}} more parts | 另有 {{count}} 段 |
| `part.alongside` | alongside, in a tree of its own | 并行，在自己的工作树中 |
| `part.children` | {{count}} subagents · {{working}} working | {{count}} 个子智能体 · {{working}} 个在工作 |
| `part.earlier` | {{count}} earlier parts | 更早的 {{count}} 段 |
| `part.answered` | its question is answered: it resumes next | 它的问题已答复：接下来继续 |
| `quest.untaken` | not taken yet: {{why}} | 尚未接手：{{why}} |
| `quest.tookBy` | taken by {{conversation}} | 由{{conversation}}接手 |
| `quest.elsewhere` | taken on another machine | 已在另一台机器上接手 |
| `quest.unrecorded` | taken, by no session recorded here | 已接手，本机没有记录接手的会话 |
| `quest.nextStep` | the next step of #{{quest}} | #{{quest}} 的下一步 |
| `routed.lane` | published to the {{lane}} lane by {{by}} | 由{{by}}发布到{{lane}}泳道 |
| `routed.took` | took #{{quest}} | 接手了 #{{quest}} |
| `routed.askQuest` | the ask's quest #{{quest}} | 需求的委托 #{{quest}} |
| `routed.repository` | asked of {{repository}} by {{by}} | 由{{by}}向{{repository}}提出 |
| `routed.ask` | by ask #{{ask}} | 由需求 #{{ask}} 提出 |
| `routed.harness` | started by {{agent}} | 由{{agent}}启动 |
| `heard` | last heard {{ago}} | {{ago}}有动静 |
| `heardSinceStart` | nothing heard since the window opened at {{time}} | 窗口 {{time}} 打开以来没有动静 |
| `withIt` | {{count}} working with it | {{count}} 个在一起工作 |
| `published` | published {{count}}, waits on none | 已发布 {{count}} 个，未等待 |
| `empty` | Nobody else is working on this | 没有其他成员参与 |
| `reading` | Reading who is at work… | 正在读取谁在工作… |
| `failed` | Who is at work could not be read: {{why}} | 读不到谁在工作：{{why}} |
| `stale` | Could not read again: {{why}} | 无法重新读取：{{why}} |
| `hint.notDriven` | This repository is not driven here, so no quest to it starts on this machine, and its work runs inside this conversation. | 这个仓库不在本机驱动，所以发给它的委托不会在本机开始，它的工作都在这个对话里进行。 |
| `hint.noLanes` | This repository declares no lanes, so its work runs inside this conversation. A `daoris.lanes.json` lets it hand rows to further parts of this session. | 这个仓库没有声明泳道，所以它的工作都在这个对话里进行。有了 `daoris.lanes.json`，它就能把条目交给本会话的后续段。 |
| `hint.oneAtATime` | This repository's sessions run one at a time here, so a quest to its lanes would wait for this one. | 这个仓库的会话在本机一次只运行一个，所以发给它泳道的委托会等这一个结束。 |

*(C4.1, C4.2; round 4)* `observed` counts agent ids, never subagents, so neither language says how many children there were;
`firstSeen` says a start nobody said, and `lastSeen` an end nobody said (O3-F1, O3-F11e). The `{{why}}` of `stale` and `failed`
is the host's refusal as it said it, passed through and never re-worded (`.claude/knowledge/translation-parity.md`).
*(O4-F1)* `child.unsaid` follows `child.firstSeen` on one row (*first seen 14:02 · its end is not said*) and is a state word in
the status palette's neutral, never *working*. *(C4-1.1)* `child.unrecorded` is read the same way, *its end was not recorded ·
last seen 14:05*, and says only that no line recorded the child's end, never how it ended.

**Row a's words** *(O4-F10)*. Row a's keys are `view`, `group.parts`, `group.subagents`, `subagentsNote`, `subagentsWorking`,
`subagentsByCalls`, `subagentsTurnWaits`, `observed`, `unreadable`, every `child.*`, `part.children`, `heard`, `heardSinceStart`,
`withIt`, `empty`, `reading`, `failed` and `stale`, *(C4-3.3, C4-4.1)* `more`, `moreWorking` and `part.more`, beside the panel's
end words with the new `disconnected`. Every other key above is the routing half's, added with its rows. Of the new glossary
terms below, row a's are *at work* / 分工 and `disconnected` / 已断开; the rest are the routing half's.

**Each state's words** *(C4-3.3, C4-1.2)*. Every value a row a state can hold is drawn by one named key, so a key missing from
both catalogues, which `i18n:check`'s equal key sets would pass, fails a test of the mapping instead:

| A child's `state` | Its key |
|---|---|
| `working` | `sessionState.working` |
| `unsaid` | `work.atWork.child.unsaid`, after `child.firstSeen` |
| `completed`, `failed`, `cancelled` | `work.panel.stream.state.*`, the panel's own (`Web/locales/en/work.panel.json:16-20`, zh the same lines) |
| `disconnected` | `work.panel.stream.state.disconnected`, new |
| `finished` | `work.atWork.child.finished`, new: the hook's stop, which says no outcome, so never *completed* |
| `ended` | `work.atWork.child.ended` |
| `lost` | `work.atWork.child.lost` |
| `unrecorded` | `work.atWork.child.unrecorded`, new, before `child.lastSeen` |

A part's state is its record's `state` worded by `sessionState.*` (`Web/locales/en/sessionState.json`), the nine states and
`idle`, as an ask's work draws a session (`Web/asks/AskWork.tsx:111`), never the strip's shown word.

**The glossary's words are kept**: 委托 for quest, 需求 for ask (*the ask's channel* is 需求的频道), 会话 session, 段 part, 泳道 lane,
频道 channel, 子智能体 subagent, 智能体 agent, 账户 account, 工作树 tree, 助手 helper, 搭档 partner, 阅读者 reviewer. *(R2-O10a)*
Round 2 also called co-worker 同事, lead 主导, alongside 并行 and *not driven here* 不在本机驱动 the glossary's; `glossary.json` holds
none of them and 同事 is in no zh catalogue, so `names:check -- --strict` holds nothing for them. They are new terms below.
*Hook* is 钩子 and *turn* 轮, as the zh catalogues already say them (`settings.rules.hook`; `help.mode.toFull`).

**Words kept off the window.**
- *Team* / 团队 stays D203's word for the structure in prose, and *team* already names the people sharing a remote (D203 point 9),
  so neither the view's title nor a group says it.
- *Turn*: the event log keeps each turn's end, but the lineage keeps no turn (§1.7, R2-O4), so no group or label says *this
  turn*. The one heading word *its turn waits for them* says what the adapter does, never a group.
- *Run* is the glossary's workflow run (进展, matched as *this run*), so no label says *this run* either. The subagents are the lead's
  agent's, which is what the note says.

**New glossary terms**: *at work* / 分工 (a view); `disconnected` / 已断开 as a child's end word; and *(R2-O10a)* *co-worker* / 同事
and *lead* / 主导 as D203 point 9 names them, *alongside* / 并行 as point 9 names the structure, and *not driven here* / 不在本机驱动.

## 6. The routing half (a later round)

*(Round 4.)* All of §6 is the routing half's, proposed for a later round under its own reads. Round 4 corrects in it what the
reads of round 3 found, and settles none of it.

### 6.1 The ways, and what each is for

| Way | Use it for | It costs | Its bound |
|---|---|---|---|
| a lane's quest to the session's own repository | a row of the roadmap that can be built and checked on its own, which the lead's next step does not build on: **a further part of this session**, in a tree of its own where the repository opens trees and while the lead is live (R2-O2), landing on the session's one branch under its one *Accept* | one part's turns on an account; a tree | the person's drive here and the machine's cap, whether it starts; trees here, whether it runs alongside or after (R2-O13e); and the person's account rules (§6.5) |
| a lane's quest to another repository | that repository's owned work | its session | D45: never write across |
| a helper | a question about the work in progress, answered once | one turn, reading | `helpers`; two at once; D163 |
| a partner | a piece of code written apart, taken in by the lead's merge | one turn in a clone | `partners`; D199 |
| a native child | a short search or read the lead needs before it goes on | the lead's own account and process | the harness's; Daoris confines it only after C2a |

**The difference that matters to the person.** A lane part is a record Daoris starts, shows, stops, resumes and lands with the
session. A native child lives inside the lead's agent process, which a limit, a crash or a stop ends whole, with no record of its own.
A roadmap of rows, each of which can be checked on its own, belongs in lane parts. A process that runs a roadmap through children
risks every row at once. The parts still land together, on the session's one branch (D167's LANDSYNC0 amendment), so the person
accepts the session's work once. A superseded part is named and never dropped unsaid.

### 6.2 How an agent is told (the doctrine)

The co-working skill (`canon/core/skills/co-working/SKILL.md`, synced to `.claude/skills/co-working/SKILL.md` and both examples)
gains a section, *handing work out*. It is project-agnostic, written under the canon-authoring rules, and says the table above in
the agent's words. It decides nothing: the agent chooses within the person's rules, and Daoris never splits a session's work itself
(D193's rejection of agents directing agents; D24).

- **Who it is for** *(C3.2)*: a **lead**, a driven session serving the quest it was started for, and a **chat**, a conversation the
  person started in a repository. Not a helper, a partner or a reviewer, nor Ask Daoris, an intake or a brainstorm's pass, each of
  which has its own room's rules. The section says so in its first line. *(O3-F6, C3-1.3: round 3 said the connector refuses all
  three roles.)* It refuses `quest_publish` and `quest_respond` to a record with `serves` (`Mcp/RoleFilter.cs:58`, `:77`, read at
  `:114`), so to helpers and partners today; a reviewer's record carries `opinion` and no `serves` (`Core/Review/OpinionDesk.cs:128-131`)
  until COWORKUX1c gives it one. Until then a reviewer is excluded by the section's words alone, and is told no standing.
- **When it applies** (§6.5's preconditions), *(C3.1)* **read from what Daoris told the agent, never assumed.** The repository's
  routing standing is desktop-owned: the service's `registry` answers its lanes but never this machine's drive or trees
  (`Mcp/KnowledgeSearchTools.cs:288-330`). So the driver tells each lead and each chat it starts, in one line of Daoris's fixed
  words, its repository's **routing standing** here, judged once in .NET (§6.4): *open* (driven here, lanes declared, trees of its
  own, naming the lanes), *one at a time* (driven, lanes, no trees of its own), *no lanes*, or *not driven here*. The line goes
  where BRWHINT1 already puts the browser's line: with a new conversation's first message (`ChatRunner.cs:1380-1382` at
  `972b17577`), in a driven session's instruction (`Adapters.Browser`'s place, `Agents/Adapters/Adapters.cs:334-339`), and, unlike
  the browser's, again at each later start of the conversation's process (a go-on, a hand-over's briefing), since a drive may
  have moved since. The way is open where the agent was told *open*, and for a chat only where its person asked it to hand its
  work out (R2-O11b). Told *one at a time*, a lead may still hand rows on and finish its quest naming them, since they start once
  it ends; a chat works its rows itself, since a working chat holds its repository (§6.4). *(C3-3.1)* This one-at-a-time rule for a
  lead is said the same way in D115's note, and tested apart from the chat's. *(C3-3.2)* *Open* says the way is available, never
  that an account or an agent can run the quest at once: the planner and the account walk still decide when it starts.
  *(O3-F10)* The start that tells the line keeps a coded note on the record naming the standing told, so the person can see what
  the agent was told; where it differs from the hint's present standing, the view says *told: {standing}* beside the hint.
  **Where no line was told** (an older
  build, a session this rule does not cover), **the standing is unknown and the agent works its rows itself**: the conservative
  reading, which costs a slower roadmap, never a quest that sits unseen. A standing that moves while the process runs is told at
  its next start; meanwhile a quest that does not start says why on the view (§3.9).
- **What goes where.**
  - Rows that can be built and checked on their own go to the repository's own lanes, as quests (`quest_publish` to
    `repository:lane`), one quest per row with its proof. Each becomes a further part of the session and lands with it.
  - *(R2-O2)* Work the agent's own next step builds on is not handed out: a part's commits land on the entry's branch, never in
    the lead's tree, so the lead could not build on them. The agent does that work itself, or finishes its quest and lets the
    entry's one *Accept* join the parts.
  - A question goes to a helper.
  - A piece goes to a partner, where the door exists.
  - Subagents are for reads and searches the agent needs before it goes on.
- **After publishing, by kind of session.**
  - **A chat** says what it published and ends its turn. Its view shows each part and quest and its end. A quest's outcome is an
    ordinary message and never starts a turn (D188 point 2), so the chat is told at its next turn.
  - **A driven session** never ends a turn while it still holds its quest with nothing named: that parks it on the person
    (`Observation.cs:167-174`), the case HELPERWAIT1 fixed for helpers (D203 point 5). It does one of two things:
    - it **finishes its own quest** (`done` or `ready`), naming the quests it handed on in its reason, where its own work is done;
    - or it **waits on one of them** (`quest_respond wait`, one `on`) and ends its turn, where its own work cannot go on without that
      one's **answer**. A lead waits on at most one quest (§3.2).
  - *(R2-O2)* **A lead waits on a lane part for its answer, never for its code.** The resume starts it in its own earlier tree with
    the answer in its instruction, synced with the line alone (§1.2), so the part's commits are not there.
  - *(R2-O2)* **A lead commits before it waits.** A D79 wait holds no tree, so its tree may go to the next part the planner looks at
    (§1.2), with whatever it left uncommitted.
  - *(R2-O2)* **The resume after a lane part's `done` is D79's quest door**, which D188 point 5 leaves unchanged, so it is no turn
    D188 forbids. The person's drive, the machine's cap and the strikes bound the chain (§6.5).
- **What it reads afterwards**: `quest_list`, and the channel.

### 6.3 A chat

A chat that took no quest has no `Awaits` and cannot `wait`. A chat that took one (CHATTAKE1) is read through `Session.TookQuest`:
the quest is its *Asked by*, and its wait is that quest's (§3.1). Either may publish to its own repository's lanes: `PublishedBy`
names it, and each quest opens a further part of its session (§1.3). D188 settles how a chat hears its quests' ends: a quest's
outcome is ordinary and never starts a run (`D188.md:18`, `:29-30`). This design shows the ends in the view and starts nothing.

### 6.4 Where the way is not open (SELFASK1)

Three places say how, each in Daoris's fixed words:

1. **The refusal** (SELFASK1, built, C1.3: `QuestExchange.cs:539-552` at `972b17577`). Where lanes are declared, it names them by
   their addresses. Where none are, it says that a `daoris.lanes.json` with a steward lane is what lets the repository dispatch its
   own work.
2. **The registry's row** for a repository with no lanes says the same (SELFASK1, `KnowledgeSearchTools.cs:326-330`).
3. **The view's hint** is drawn where the lead's agent process started at least two subagents, counted from the lead's lineage in
   its current generation (§3.6, C2.2). It is said where the case happened and nowhere else, as the first of these that holds, in
   this order *(R2-O11a; round 2 gave no order, and its conditions overlapped and left a case unworded)*, each worded so that it is
   true of a repository with no lanes. *(C2.5, C3.1)* The three are the **routing standing**, judged once in .NET from the drive,
   the trees and the registry's lanes, the same judge whose words the driver tells an agent at its start (§6.2). *(O3-F10)* They
   share a judge, not a moment: a standing that moved since the start differs from what was told, which the view says (§6.2). The
   read takes its inputs from the driver slice and the registry and is told when they move (§4.2):
   1. `not-driven`: it is not driven on this machine (`config.Drivable`), whether or not it declares lanes, since no quest to it
      would start here;
   2. `no-lanes`: it is driven here and declares no lanes, whether or not it opens trees of its own;
   3. `one-at-a-time`: it is driven, declares lanes, and does not open its own trees (`config.OpensOwnTree`), so a quest to its lanes
      would wait for this session. *(R2-O13b)* This is true of a chat too: a repository's holder is drawn from every active session,
      conversations included (`Planner.cs:1290-1292`; CAPCOUNT1's remark at `:1300`), so a working chat holds a one-at-a-time
      repository.

   The hint links to the guide's lanes section. It is a fact about the repository and this machine, never a judgement of the
   agent's choice.

### 6.5 What is the person's

- **The rule that bounds an agent's quests to its own repository** is the person's choice to drive that repository here (D46 §2,
  `config.Drivable`, `Planner.cs:1889-1891`), together with the machine's concurrency cap (`config.Cap`, *the concurrency cap is
  spent*, `Planner.cs:2048`), which holds today. DEV6's lane cap adds to it once built. *(R2-O13e; round 2 counted trees among the
  bounds.)* Drive and cap bound whether a quest starts. Trees of its own (`config.OpensOwnTree`) decide only whether it runs
  alongside or after: a lane quest to a driven repository without them still starts, one at a time (`Planner.cs:2010-2021`).
  - *(R2-O2)* **The chain of resumes.** A lead resumed after its lane part's `done` comes through D79's quest door, unchanged by
    D188 point 5; drive, cap and strikes bound how far such a chain goes.
  - **Why no rule shaped like `helpers`.** A quest to a driven repository already starts under exactly these conditions, whoever
    publishes it: an ask, the person, or another repository's session. A session publishing to its own lanes gains no power that
    another repository's session lacks. Driving is the person's opt-in to sessions they did not start, and the cap bounds their
    count.
  - **What `helpers` covers that this does not** *(C1.1; round 2 said a helper runs outside the cap, which the code does not)*. A
    helper takes a slot under the machine's cap as a quest's session does: its admission subtracts the slots held before it starts
    one (`Driver/Loop/DriverHelpers.cs:130` at `972b17577`), and the helpers are judged before the quests are planned
    (`Driver.cs:1006`). What the `helpers` rule adds is its own admission outside the quest plan: a helper is no quest, so no drive,
    strike or lane bounds it, and the rule's yes, the agent it names and two at once per asker are what decide whether it starts
    (`HelperJudge.Judge`, `DriverHelpers.cs:131`). A quest to one's own lanes goes through the plan, where the drive and the cap
    already decide. If the person wants a separate rule for self-published quests, it is a later row. It is named in §9 and not
    designed here.
- **Whether a repository has lanes.** Writing `daoris.lanes.json` is a change to that repository, made by its own sessions or its
  owner, through its own landing (D45). Daoris never writes one.
- **Which accounts they spend** (D197's walk, the actors of D205).
- **`helpers` and `partners`**, the rules already in Settings. A native child's tool falls under the person's permission rules (D72),
  which can deny the harness's agent tool as any other.
- **Accepting the session's work**, with its parts on one branch, by the entry's one *Accept* (ACCEPTALL1). Nothing here lands
  anything.
- Not the person's to press each time: the agent's choice within those rules (§6.2), as the coworker design §3.3 has it for
  co-workers (R2-O13a).

## 7. The rows, in build order

Each row is one feature a person can see working, built driver to page by one builder (D187), in en and zh wherever words are drawn.
They are filed in `TASKS.md` when this design is accepted; until then COWORKVIEW1 points here.

1. **COWORKVIEW1a — a chat's subagents seen** (driver, modules, web-shell; replaces FABRIC-C1b's read and display and lifts its
   *after C2a*, since showing restricts nothing). **The scope round 5 settles.** *(C3-5.1, O3-F1, O3-F2, round 4; C4-4.2, round
   5.)* Its whole contract is the table below: every rule of its lifecycle, its watch and its empty states, where each is
   specified, and the test that proves it. Nothing outside the table is row a's. Its first step is the measure (rule 1). Round 5
   widens rules 1, 4 to 7, 9 to 17, 19 and 21 and adds rule 23, each change marked with its finding.

   | # | Rule | Specified in | Proved by |
   |---|---|---|---|
   | 1 | **The measure first**: the install's lineage files and a stand-in's, on each door, first run and re-run; whether `SubagentStart` reaches the hook over the adapter; how many `acp` ids join a hook id, equal and by the suffix; *(O4-F1)* whether `codex-acp` announces children, whether its announced ids equal the threads its calls carry, and so which lines a Codex part draws its rows from; which doors show no child at all (a protocol session started without streams); *(C4-2.1)* whether `proc` reaches the hook's and the connector's lines on each door, and how many lines with none sit after a `start` their own moment precedes, the one placement position cannot make | §1.1, §3.6 *the join*, *each line names its process* | the counts, the Codex answer and the `proc` answer per door in D173's note before the fold joins |
   | 2 | **The `acp` lines**: the announcement bounded (name 120, task's first line 200) with its parent or `depth: 1`; an end only from `subagent_state_update`; `EndAllAsync` writes none | §3.6 | a new `AcpStreamsTests` class, each of the three |
   | 3 | **The pipe's description** kept on the stream's start line, bounded | §3.6 | `ChildBindingsTests` on `StreamLine` |
   | 4 | **Every process bracketed**: a `start` before each of the four spawns, an `end` once that process's exit is read and *(O4-F4)* its capture has drained (or its bound passed), none again at the record's conclusion; *(C4-2.1)* each naming the process's `proc`, issued before its spawn, the `start` making the file where there is none and the `end` written only to a file that exists; `proc` on every line the driver writes for the process, bound into its writers at the spawn; `DAORIS_LINEAGE_PROC` set on the spawn's environment anew at each of the four, and among `Adapters.Roles` | §3.6 | per place: a chat, a driven first run, a continuation's run and a resume's retry each write `start` then `end` around their process, each with its own `proc` (stand-ins); a stream `ended` line the capture carries lands before the `end` (O4-F4); a record of two processes folds as two generations; *(C4-2.1)* a part with no file writes its `start` first; a continuation's lines carry its own `proc`, never the first run's; a capture held past its bound and abandoned, writing after the next `start`, writes with its own `proc`; a start inheriting `DAORIS_LINEAGE_PROC` drops it (ENVLEAK1's table); `ChildLineageTests`: an `end` with no file writes none |
   | 5 | **A generation begins** at a `start`, or, with none, at the first line after an `end` (O4-F4), a line that only ends a run of the closed generation staying in it; **it closes** at its `end`; at the next `start` (`lost`); or, the last, where no process of the part runs in this window and the record is neither `queued`, `starting` nor `working` (`lost` for `stopped` and `failed`; `ended` for `completed`, `awaiting-person`, `declined` and `stood-down`, O4-F7); never by responsibility; a part another Daoris process here runs closes only by its record's move (O4-F6); *(C4-1.1)* **recorded** where a `start` began it, **history** where none did; a history generation's `end` its one process's only where the record's `kind` is a chat; in a driven record's history, and in any history generation the next `start` closes, a run no harness ended reads `unrecorded`, *last seen*, never `ended`, `lost` or an `end`'s moment | §3.6 | `ChildBindingsTests`, a case each; *(O4-F4)* a chat's file written before row a with three processes, each closed by its own `end`, a later process's runs never in an earlier generation, and a late stream `ended` after an `end` ending its run there; *(C4-1.1)* **the existing driven case**: a file written before row a by a driven record's first run and its continuation, no `start` and one `end`: a child of the first run its harness never ended reads `unrecorded` with *last seen* at its own last line, never that `end`'s word or moment, and one its harness ended reads its word; the same lines under a chat's record read the `end`'s word; a history generation the first `start` after an upgrade closes reads `unrecorded`, never `lost`; the crash between `Driver.cs:3876`'s conclusion and the `end` line, record `completed` with `ended.awaits`, child `ended`; each of the nine record states mapped (O4-F7); a `working` record with no process stays `working` until the sweep's `stopped`, then `lost`; a *Process* case: a stand-in chat orphaned with a child working, the sweep run, the view reading *lost* |
   | 6 | **Runs**: begun by a start the harness said, or, for an id with no run in the generation, by its first line, a call's included (*first seen*); ended by a said stop or the generation's close; a said start after a stop is a new run; a call or stop after a run ended begins nothing; *(O4-F1)* a run whose harness says no end is `unsaid` while open, *first seen {time} · its end is not said*, working nowhere; *(C4-2.1)* a line with `proc` in its own process's generation wherever it sits, one with none by its position, and a line placed in a generation already closed changing that generation alone | §3.6 | *(C4-2.1)* `ChildBindingsTests`, across a later spawn: a late `started` with the first process's `proc` after the second's `start` begins no generation and draws its child closed with the first, *last seen*, never *working*; a late `ended` naming id X with the first's `proc` after the second's `start`, while the second runs X, leaves the second's X *working* and words the first's; a line with no `proc` after the second's `start` is placed in the second; a `proc` no `start` names is placed by position; `ChildBindingsTests`: the Codex fixture (`ChildBindingsTests.cs:125`) given its lines' `at`, which *first seen* needs (O4-F1: they carry none today, `:127-136`), keeps its children, each `ended` with its part and *first seen*; *(O4-F1)* an open Codex generation reads each child `unsaid`, counted in no *working* and no `turnWaits`; a finished id started again, in the same generation and a later one, reads `working`; a late call reopens nothing; a Claude Code agent seen only by calls on the pipe door is a row |
   | 7 | **End words and moments**: *(O4-F8)* the wire's or the stream's word wins wherever a line of the run says one, whatever the order of the lines; otherwise the hook's stop makes it `finished`; a close by rule 5's second or third way, *(C4-1.1)* and every `unrecorded` one, carries no end moment and reads *last seen* at the child's own last line | §3.6, §3.8 | `ChildBindingsTests`: a hook `stopped` before a wire's `failed`, and after it, both read `failed` (O4-F8); the answer's test: no working run's last moment, no call count |
   | 8 | **The join**: equal id or the `:generation:<n>` suffix; on a part with `acp` lines, the hook's unmatched agent ids counted once, never rows or runs, on a door that joins and one that never does; *(O4-F1)* a Codex part with `acp` lines draws its rows from them and counts each thread no announcement matched, until the measure says the ids join; nothing by time or order | §3.6 | `ChildBindingsTests`, a Codex part with `acp` lines and threads among them: each child one row |
   | 9 | **Three outcomes of a read**: no file, read, unreadable with its kind; no file is a dependency stamped absent, *(C4-2.1)* which a process's `start` may end | §3.6, §4.1 | `ChildLineageTests`: an unreadable file is never an empty fold; *(C4-2.1)* a read holding a part registered absent wakes when that part's next process writes its `start` |
   | 10 | **Inputs in memory**: the entry from the `groups` slice (an attended record in no entry its own one part, O4-F12c), the read fields of the records from the `sessions` slice (O4-F12d), this window's `running` from the driver slice (O4-F6), the lineage files by name; pending until both slices have judged, *(O4-F2)* a pending composition registering each slice's not-judged status; *(C4-1.1)* the record's `kind` among the read fields; *(C4-2.4)* each slice's health, read from memory: not judged and failing *failed*, judged and failing *stale*, each with the slice's words | §4.1 | the read's tests: no service call at the client's seam; *(O4-F2)* pending before the slices judge, then the answer once they judge, with no other ask; an ended intake drawn as its own part; a `note` or `updated` moving alone wakes nothing; *(C4-2.4)* a `sessions` slice whose first judgement fails draws *failed* with its words, never *reading* for good, with no ask of the page's; one that fails over a judged snapshot draws *stale* over the kept rows; its recovery, by a judgement that pushes nothing, draws the answer without *stale*; the same for the `groups` slice; none of them makes a service call at the client's seam; `StateSlicesTests`: `HealthMoved` told as a failure begins and as it ends, never at a second failure in a row |
   | 11 | **The source fence**: dependencies with versions, compared as it lands, *(C4-2.4)* each slice's health among them; a moved one composes again, at most three times; registration told by `Land`, so a superseded composition registers nothing | §4.1 | the read's tests: a `sessions` push and a lineage append, each during a composition, compose again before it lands; *(C4-2.4)* so does a slice's failure beginning during one; an ask joined to a work begun before a record moved lands the fresh answer; a superseded composition registers nothing (`DriverModuleHeldTests`) |
   | 12 | **One judge, waking each read once**: compared with the registered versions, by a `ForWork` topic whose judge is the read's own, never `HomeWatch`'s fixed judge; a read's dependencies leave the union when they wake it, back when it is asked again and lands; judged at registration; a length registered up to the last full line (O4-F12b); *(C4-2.4)* asked by each slice's `HealthMoved` too | §4.1 | the watch's tests: *(C4-2.4)* a slice's `HealthMoved` wakes a read registered on its health, once; a held read no page asks again wakes once and never again across appends and 30 s deadlines; three appends during one composition ask it once more; an append between a composition's read and its registration wakes it; a sibling part's append wakes the attended part's read; a lineage file created after the read wakes it; *(O4-F12b)* a half line at composition, completed after, wakes the read |
   | 13 | **What a line wakes for**: any line but a call; a call only where it names an id the composition holds neither as a run nor among its unmatched ids (O4-F5), *(C4-2.2)* nor as its part's root, its root ids registered; a call the fold reads as the root's wakes nothing, but a Codex call naming with no parent a thread held as a child; a shorter file | §4.1 | the watch's tests: a call naming a held id wakes nothing and is registered; *(O4-F5)* a call by an unmatched id already counted wakes nothing; a call naming a new Codex thread with a parent wakes; *(C4-2.2)* a hundred root calls, a Codex root thread's, a parentless thread's never seen before, and a Claude Code root's hook and connector lines, cause zero `atWork` wakes; a parentless Codex call naming a held child thread wakes once; a `start` line wakes; a truncated file wakes |
   | 14 | **Off the window's thread**: *(C4-4.1)* at most 40 rows a list, the subagents filled open runs, then lost, then the newest three of the rest, each child only with its whole chain of ancestors, a chain that does not fit passed over; the rest folded; every child not drawn for want of room counted by its state in `more`; the parts past 40 counted by state; `HeldAnswers` serialising before its gate; no composition moment | §3.8, §4.1 *the bound* | `DriverModuleHeldTests` (O4-F12e: there is no `HeldAnswersTests`): the text made before the gate; a timed case under a slow stand-in: an ask answered within the window's bound while a 40-member answer lands and three other held reads compose; an unchanged composition tells nothing; *(C4-4.1)* the fold's tests: forty-one working children make 40 rows and `more` of one working, the heading *41 working*; a working child under 40 ancestors is never drawn and is counted in `more`, its ancestors drawn by their own place in the order; a chain of 12 drawn whole after 25 others, and one of 20 passed over for a shorter one after it; in each, the rows, `folded` and `more` together count every child once and no drawn child lacks its parent; 45 parts make 40 rows and `moreParts` |
   | 15 | **Reading, failed, stale**: pending held as `{ reading: true }`; *(O4-F3)* both failure events carrying the refusal's words, a failure with nothing held written as `{ failed: { why } }` and one over a held answer as `stale: { why }`, neither asking again; *(O4-F12a)* `stale` kept through an ask answered from the held answer and cleared by the first landing after the failure, even an unchanged one; *(C4-2.3)* the read **numbered**: its ask never throws, a failure within `Patience` and a held fault answered as the failed value until a landing replaces it, a held answer with a kept failure answered stale, the query's `retry: false`, a query in error drawn *failed*; *(C4-3.1)* every reply and event carrying `{ epoch, n }`, the page dropping one older than the newest it took, another epoch replacing it; every other held read unchanged | §4.1, §5.2 | `DriverModuleHeldTests`: both failures told with `why`, the held answer kept; *(O4-F3)* a work that always fails is begun once per ask, never once per failure; *(O4-F12a)* an unchanged landing after a kept failure is told; *(C4-2.3)* a work failing within `Patience` and one failing after it are each answered as the failed value, the ask never throwing, and a later ask with the fault held reads it again; `held.test.ts`: neither failure asks again, another held read keeps its handling; *(C4-2.3)* the query is never in error for a work's failure and is not retried when the bridge refuses; *(C4-3.1)* events told newest first then older keep the newest; an old direct reply after a newer event is dropped; an old failure after a recovery and an old answer after a failure each regress nothing; an equal number changes nothing; another epoch's delivery replaces the number kept; an unnumbered read's event is handled as today; the page's: reading is no query error, failed draws its sentence and *Try again*, stale stays through a re-ask answered from the held answer and keeps its rows dimmed |
   | 16 | **Nobody** only where every source was read and holds nobody; unmatched ids stand in its place; an unreadable lineage withholds it; *(O4-F9)* a harness seen only by its calls, read from the record's `adapter`, or a door rule 1 finds shows no child, withholds it, its *appear only when they call Daoris* line in its place; *(C4-2.4)* an input slice failing, the answer *failed* or *stale*, withholds it | §5.2 | the page's tests, a case each; a Codex lead with no lineage draws the line, never *Nobody*; *(C4-2.4)* an answer *stale* for a failing `groups` slice, holding no child, never draws *Nobody* |
   | 17 | **The parts are the strip's set and order**: the attended part first, *(C4-1.2)* each with its record's own state, `sessionState.*`, deliberately not the strip's shown word, which carries the reader's derived waits; its subagents' count; no part folded, called live, waiting or the lead | §3.1, §3.8, §5.2, §5.4 | the read's tests: the strip's set, one row each; a `completed` record with `ended.awaits` drawn with its own state, neither folded nor called waiting; *(C4-1.2)* the page's: a `completed` part the reader's grouping words `awaiting-helper` shows that derived word in the strip (`PartsStrip.tsx:56`) and *completed* in the view, the difference held on purpose |
   | 18 | ***Last heard*** from the `calls` slice's `heard` partition, none in the answer; the head's fragment, giving way to *quiet* | §3.7 | `SessionOutput`'s newest moment; `CallsStateSliceTests`: judged from memory, one judgement per session per ten seconds, no `STATE_CHANGED`; the head's tests |
   | 19 | **The view**: the side bar's `atWork` after *Workflow*, at 300, 430 and 680 px, both themes and languages, its words, *(O4-F10)* row a's keys and its two terms *at work* and `disconnected` marked as its own; *(O4-F1)* `turnWaits` on `claude-code-acp` alone; *(C4-3.3)* every state a child or a part can hold drawn by the key §5.4's mapping names, `finished` and `unrecorded` among them | §5.1, §5.3, §5.4 | §5.3's first fourteen stories; `presentational.test.ts`, `i18n:check`, `names:check -- --strict` with row a's keys added and no routing-half key, the tokens gate; `turnWaits` false on any other adapter; *(C4-3.3)* a table test naming, for every value of `AtWorkChild.state` and every record state, the key it draws, and finding that key in both the en and the zh catalogue |
   | 20 | **Its doors**: `sessions at-work <session> [--json]`, *(O4-F11)* asking the running Daoris over its pipe, and with none running composing the same answer by the same .NET function under the home's lease; told by event, *(O4-F10)* through §4.1's own three steps, the hub's `sessions` entry not naming the read; no press; Ask Daoris's room learns the view's name | §4.1, §5.1 | the verb's words and JSON tests, with a window running and with none; `WindowCensusTests`; `PipeCensusTests`; `HelpCoverageTests`; `stateHub.test.tsx`: `atWork` asks the read again, `sessions` does not |
   | 21 | **The records**: the lineage's twin row gains `acp` and `start` and the label (R2-O13f), *(C4-2.1)* and `proc` on every line, the hook's and the connector's where their environment holds it; D173's note keeps the measure | §3.6 | `twins.md`'s row; D173's note; *(C4-2.1)* the connector's `LineageFile.Line` and the hook's `lineageLine` tests: `proc` written where the environment holds an id-shaped one, absent where it holds none or another shape |
   | 22 | **The person's first**, on the install | below | below |
   | 23 | *(C4-3.2)* **Where its code sits**, on FOUNDATION1 Part A's map: the lineage writer's port declared in Agents, handed to `AcpStreams` and to the stream watcher in place of `lineageHome`, implemented in Participants, wired by Loop at the spawns; no new edge up the map | §4.3 | a new `AcpStreamsTests` class with a recording port, naming no lineage type; `HarnessCapture` taking the port; the structure census (FOUNDATION1's row mod-0) where it has landed, its exceptions gaining none and losing the watcher's |

   - *Proof*, beyond the table's: round 3's test list moved into the table's rows, less what left row a. The waiting, answered and
     lead tests and the ask-keyed read are the routing half's (rows b and d); the burst's service reads are none of row a's (§4.1).
     - **The person's first:** a chat working through native subagents shows each one, its state and its end, in **two runs**.
       In run 1, a stand-in chat on the protocol door starts three children: one completes, then the root is killed, and the other
       two read *lost*. In run 2, the chat is resumed and starts one child, which reads *working* with *last heard* moving. The view
       draws *completed 14:02*, *lost*, *lost* and *working* in en and zh, at 300, 430 and 680 px and in both themes, seen on the
       install's window. *(R2-O12b)* Neither run draws *ended with its part*, a clean end with a child still open: on the protocol
       door the adapter holds the turn until its children end (§1.7), so a stand-in chat does not produce it on demand. Only
       `ChildBindingsTests` holds it, and its story draws it. *(C2.2)* Nor do they draw a child an application's crash left open:
       rule 5's *Process* case holds it.
   - *Trap:* joining an `acp` child to a hook line by order or time; writing an end the driver made on a child's behalf; closing a
     generation by whether the part is responsible for a quest rather than whether its process runs (C3-2.3); a call reopening a
     run; a dependency compared with anything but the version its composition registered (O3-F2); *(C4-2.1, C4-1.1)* placing a
     line with `proc` by its position, or one with none by its moment; reading a driven record's history `end` as each of its
     processes'; *(C4-2.3, C4-3.1)* a work's failure reaching the page as a refusal, or a numbered delivery taken without its
     number.

**The routing half's rows, for a later round** *(round 4)*. Rows e, b, c, d and f stay proposed under their own standing (the
design's *Standing*): round 4 corrects in them what the reads found, marked below, and settles none of them. Their order holds:
e, then b after a, then c, d and f.
2. **COWORKVIEW1e — the routing told, from a standing the agent is told** (driver, canon, web-shell). *(R2-O12c: moved up, its
   letter kept so the notes' citations hold.)* *(C5.2)* Round 2 had its skill section land on its own, while the drive it reads was
   told to no chat; this row now builds that source first, so it depends on no view row for its first half. SELFASK1 has landed
   (C1.3), so it waits on nothing else. Its half after row a: §6.4's three hints, which read the lead's lineage.
   - **The routing standing** (§6.4): one judge in .NET from the drive, the trees and the registry's lanes, and the driver's line
     told to each lead and chat at each start of its process, in BRWHINT1's places (§6.2, C3.1). No line for a helper, partner,
     reviewer, Ask Daoris, an intake or a brainstorm's pass (C3.2).
   - **The skill section**, through canon and then this repository and both examples synced, under the canon-authoring rules: who
     it is for (C3.2), the way read from the line told and unknown without one (C3.1), and R2-O2's answer (a lead waits on a lane
     part for its answer, never its code, and commits before it waits).
   - *Proof:* the judge's table (*open*, *one at a time*, *no lanes*, *not driven*, in R2-O11's order); the line in a new chat's
     first message, in a driven session's instruction, and at a go-on after the drive moved, with the moved standing; none for each
     role above; `daoris check` and the canon's tests. The hints' stories, and their rule's tests: each hint drawn at two subagents
     in one generation, as the first of the three that holds; `no-lanes` drawn for a driven repository with no lanes and no trees of
     its own; none drawn with lanes declared and driven with trees, or at one subagent; *(C2.5)* the hint moving when `driver.json`
     undrives the repository, and when the registry gains its lanes, with no session or quest written.
   - *(Round 4, for its round.)* The lanes-only proof passes only once `Repositories.Signature` carries the declared lane ids
     (O3-F3, C3-1.2, C3-5.2; §4.2). The one-at-a-time rule for a lead is tested apart from a chat's (C3-3.1). The start that tells
     the line keeps its coded note, and a standing moved since is drawn as *told: {standing}* (O3-F10).
3. **COWORKVIEW1b — a session's lane parts and the quests it asked, with their waits** (driver's read, web-shell; after a).
   - **What it builds.** The `parts` group's lane, *took* and *the ask's quest* routing (R2-O7), *alongside* read from the part's
     tree (R2-O2) and each part's subagents' count. The *Asked through quests* group from the quest units, with the takers (a chat's
     take, another machine's by `Holder`, and one by no session recorded here, R2-O6, C1.2), their current waits (C2.1) and, for a
     quest still `Open`, the tick's reason read beside the answer (§3.9, C2.5). The *Waiting on* line, one quest at most, gone once
     answered. *Asked by* (`Quest ?? TookQuest`; `Parent`; `From`). The `published` line.
   - *Proof (the person's second):* **a session that handed rows to its lanes shows each part and what it waits on.**
     - *(R2-O12a; round 2's run corrected.)* Under a cap of 1 a driven publisher holds the one slot itself (`Planner.cs:904`,
       `:1301`), so no lane part could run alongside it, and the other repository's session needs that slot to have started. So the
       publisher is a chat, which holds no slot (CAPCOUNT1). An example repository with two lanes (family phase 4b's engine, `core`
       and `media`), driven with trees of its own, under a cap of 1. In this order: the chat publishes to another repository, whose
       session takes the quest and waits on a question; then the chat publishes to each lane. One lane quest is taken and runs
       alongside; the other is untaken while the cap is spent.
     - The view draws the taken lane's session **once, as a part**: the set of parts is the *Parts* strip's, one row each (R2-O7).
       The untaken one is under *Asked through quests* with *not taken yet: the concurrency cap (1) is spent*, and moves to *This
       session* when taken. The other repository's session shows *waits on #q of repo*. Each is drawn in en and zh.
     - *(R2-O3)* **A lead waiting on its lane part**, under a cap of 2: a driven lead publishes one lane quest, commits, waits on it
       and ends its turn. The view draws the lead as the lead, live and never folded, with *Waiting on #q (core lane)*, and the lane
       part once below it, *alongside* only where its tree is its own (R2-O2). *(C2.1)* Then the lane part finishes: the line goes,
       and once the resume starts, the resumed record is the lead and the record that waited is folded among the earlier parts.
     - The read's tests: a chain's step under its parent, never *by you*; an ask's quest under its ask; a chat's taken quest as
       its *Asked by*; a taken quest with no executor here read by its `Holder` (C1.2). The page's: *(C2.5)* a tick that changes an
       untaken quest's reason, with no session or quest written, changes the row's words.
   - *(Round 4, for its round.)* Row b now builds the parts' responsibility row a left out: the current executor, *live*,
     *waiting*, *answered*, the lead rule and the fold by liveness (§3.1). Its selector is `LastRun`'s own rule as one shared
     function, a chat's take after it, and a take this machine's (O3-F4, C3-1.1, C3-5.3); its tests add a resume its agent
     refused before any prompt and a stood-down replacement, each leaving the earlier responsibility in place, and a chat's take.
     The lane part *finishes* by closing its quest done, by an answer or its own *Accept* (ACCEPTALL1b); a done held for the
     person's yes keeps the lead waiting (O3-F11a). The cap-1 run's other repository is one not driven here, so its question does
     not take the one slot first (O3-F11b). Before the first tick, `useConsidered` is empty (`Web/bridge/driver.ts:333-340`), so an
     untaken row says *not taken yet* with no reason, never an empty `{{why}}` (O3-F11d).
4. **COWORKVIEW1c — who serves it, and what was said** (web-shell; after b and COWORKUX1a, whose prerequisite HELPERWAIT1c is
   built). *(C5.3, C6.1)* Round 2 promised a thread no row had supplied and offered to write the exchange reader here; both are
   D203's.
   - **What it builds.** The *Co-workers* block from COWORKUX1a's exchange read, its rows read-only (§3.9); each row opens the
     member's thread where COWORKUX1b has landed, and the member's own session page until then. The *Waiting on* helper's name from
     the same read. The channel's three lines from the panel's own read, each placed on the member that posted it. No box joins the
     `atWork` watch, and the read gains no co-worker or channel field.
   - *Proof:* a lead waiting on a helper shows *Waiting on Codex · helper* and the helper's row opening its session page, then, once
     COWORKUX1b is in, its thread; the view's co-worker rows and the head's section rendered from one query; a channel line placed
     on the part that posted it, with no second channel read; en and zh.
   - *(Round 4, O3-F7, C3-5.4.)* COWORKUX1a's exchange read holds the line's helper asks alone (the coworker design §6.3, §12 row
     1). Row c draws what that read holds: helpers first, reviewers once COWORKUX1c and partners once COWORKUX1e extend it, with
     no change here.
5. **COWORKVIEW1d — an ask's view, and its terminal twin** (driver host, web-shell; after c). §3.5 on the ask's page. *(C5.4)* Its
   twin, as D185's R5 asks (`D185.md:24-25`): `daoris-driver ask at-work <ask> [--json]`, routed before the ask's other words, as
   `ask channel` is (`src/Daoris.Desktop/Daoris.Desktop.Driver.Host/Program.cs:470-475` at `972b17577`, `:474-479` at
   `b4f096cc0`), printing the same groups and waits.
   - *Proof:* an ask with two entries and a channel: the section above *Quests*, its rows opening each entry; stories; the verb's
     words and its JSON for the same ask, and an unknown ask refused by name.
   - *(Round 4, O3-F11f, C3-5.5.)* The twin prints the read's answer, its groups and waits. The tick's reason, the co-workers and
     the channel are read beside the answer on the page, and the twin prints none of them; its output says so in one line, pointing
     to `daoris-driver ask channel <ask>` for the channel.
6. **COWORKVIEW1f — brainstorms and meetings on their caller's view** (driver, web-shell; after BRAINSTORM1d and AGENTROOM1b; its
   links after MEETUX1b). §3.6 point 2. *(C5.5)* Its requests and states are dependencies of the composition, under the watch's
   brainstorms part (§4).
   - *Proof:* the request's fields written by each door; the *Gathered* group's rows, each linking to its page; a meeting's state
     alone moving (a round from frozen to running, no ledger write) moves the row.
   - *(Round 4, C3-5.6.)* A request made after the view is held changes none of the files it read, since meetings are found by
     listing their folder (`MeetingFiles.cs:76`). The read also depends on the listings of `brainstorms/` and
     `brainstorms/meetings/` as it read them (§4.2), and its proof creates a request naming the session after the view is held,
     with no ledger write, and sees it drawn.

SELFASK1 is built (archived 2026-10-24), and this design changes nothing of it (C1.3). FABRIC-C1b's row is rewritten to point here
when this is accepted, since row a meets both its halves.

## 8. Rejected

- **A team or structure record**, or a lane on the session record: two stores for one fact (D173's guard, the coworker design §3.4,
  R2-O13a).
- **Who does what read from the conversation**: an agent's prose, a plan's text or a tool card's words, parsed. A record's field or
  nothing.
- **A model's summary of who is at work** (D24).
- **`updated` advanced on every event**: it would break *ran* and *waiting* and write the store at the agent's rate (§3.7).
- **Native children as co-workers**: they have no record, no confinement, and nothing Daoris can stop by itself (D203 point 1 stands).
- **The child's prompt kept whole**, or the hook writing labels: a bounded label from the harness's announcement only, and the hook
  ids only (§3.6).
- **An end written at the session's end for an open child** (round 2): it would fold as the harness's own stop and read a killed
  root's child as *finished* (§3.6).
- **A lane part drawn twice**, as a part and as a quest's member (round 2): one record, one home (§2 point 4).
- **A group named for a turn, and a stored turn line** (round 2; its reason corrected by R2-O4). The read offered a driver line at
  each turn's end as the alternative. The event log already keeps each turn's end (§1.7), so a stored line would be a second store
  of it, and a group by turn would join the events and the lineage by time, which §3.6 refuses. The subagents are the lead's
  agent's either way.
- **A `helpers`-shaped rule for self-published quests, now** (round 2): the person's drive and the cap already bound them as they
  bind every quest, trees deciding only alongside or after (§6.5, R2-O13e). It is left as a later row if wanted.
- **A watch part per attended line, added and dropped** (round 1's): `HomeWatch` offers no way to remove a topic (§1.8). That is
  its surface, not the watch's, since `ChangeWatch` has `Unwatch` (R2-O13c); one fixed part, its judge reading by name, is still the
  simpler choice and replaces it.
- **The `atWork` wake narrowed by session** (round 2's, R2-O1): no wake carries the session it is about, and an ask's read and a
  read drawing another part's state would be missed. Every held read is asked again (§4).
- ***Last heard* told through `STATE_CHANGED`** (round 2's, R2-O5): each telling forgets every read under way (§4). It is pushed
  by the `calls` slice.
- **A waiting end code read as a wait on its own** (round 2's live predicate, R2-O3 as written; C2.1): a resume leaves the record
  that waited `completed` with its code and the quest naming its question, so the predicate drew two live parts and a wait already
  answered (§3.1).
- **The hook's starts hidden where a door's ids never join** (round 2's, R2-O8's second half; C4.1): the view could then say
  *Nobody else is working on this* over a hook that saw starts. They are a count of starts (§3.6).
- **A lineage `end` written by the orphan sweep** (C2.2's other way): the record's own end, which the read already holds, covers
  every way a record ends without its process, and a second writer of the part's end would race a go-on's `start` (§3.6).
- **One `Closed` flag reopened by any line** (today's fold; C2.3): a stop won for good, and a late call reopened a finished child.
  A run begins only at a start the harness said (§3.6).
- **The routing standing read by the service or the connector from the driver's configuration** (C3.1): this machine's drive is
  the desktop's, and the service answers no machine's facts; the driver tells it (§6.2).
- **The tick's reason and the driver's configuration composed into the held answer** (round 2's `sitting`; C2.5): each tick would
  then have to wake every held read, though the page already holds the tick's sentence (§3.9). The hint's standing is the one
  configuration fact the answer keeps, told by the driver slice's push (§4).
- **The co-workers, the boxes and the channel read again by this read** (round 2's; C6.1): a second reader of what D203's exchange
  read and the channel's panel already hold, with its own wakes.
- **The smaller shape as links alone** (C6.1, the part not adopted): the person's case was work spread over places nobody looked at
  together, so the parts, the subagents, the quests and their waits stay drawn in one place, each by its existing presentation,
  rather than reached only from their own pages.
- **A composition moment in the answer** (round 2's `at`; C2.6): every composition would differ from the one held, so each would
  be told as moved.
- **Settling both halves in one round** (the steward's call, round 4). Two rounds of Codex's reads each found more than ten
  blocking points, most in the routing half's responsibility, wakes and doctrine, and each round's answers opened new ones there.
  A design that wide is never accepted, and the owner's case, a chat's subagents seen, waits behind it. So row a is specified
  whole and settled alone, and the routing half keeps its own standing and reads. Rejected with it: dropping the routing half,
  since the owner's outcome includes routing better, and settling it by a vote of the readers.
- **A run opened only by a start the harness said** (round 3's; O3-F1, C3-2.1): Codex says no start, so every Codex child was
  erased (§3.6).
- **A generation closed by the next `start` alone where processes write no `end`** (round 3's; C3-2.2): a clean process's open
  child would read *lost*. Every process is bracketed (§3.6).
- **A last generation closed by whether the part is live** (round 3's; C3-2.3): a crash after a waiting record's conclusion left
  its child working. The process decides (§3.6).
- **A dependency compared with its composition's stamp at every judgement** (round 3's; O3-F2): it woke again until recomposed,
  and for good where no page asked again. Each read wakes once (§4.1).
- **Records left out of the dependencies** (round 3's; C3-2.4): a ledger-only stale landing went unseen. The fence checks every
  input (§4.1).
- **Row a reading the service per burst through `RouteFlights`** (round 3's; C3-1.4): flights that do not overlap share nothing,
  and the slices already hold the records (§4.1).
- **The lineage's newest moment as *last heard*, and call lines waking the read** (round 3's; O3-F5): the connector writes two
  lines a call, the rate R2-O5 kept *last heard* from (§3.7, §4.1).
- **`undefined` as the pending answer** (round 3's; O3-F8): the query library fails it.
- **A refresh's failure kept silently over a held answer** (round 3's; C3-4.2): the view would show a stale answer as current
  (§4.1, §5.2).
- **Unmatched hook evidence counted as starts, or beside an empty sentence** (round 3's; O3-F9, C3-4.1): an id seen only by its
  calls went uncounted, and starts nobody ended were drawn beside *nobody*. Ids are counted, and the count stands in its place.
- **An open run whose harness says no end drawn *working*** (round 4's; O4-F1): a Codex child would read working for the whole of
  a chat's process. It is `unsaid` (§3.6).
- **A file's lines all one generation until a `start`** (round 4's; O4-F4): every file written before row a, the owner's case
  among them, has no `start`. An `end` bounds a generation (§3.6).
- **A failure that makes the page ask again** (NOBLOCK1's path today, which round 4 took as written; O4-F3): a work that always
  fails was begun again at every failure. *Try again* is the only re-ask for this read (§4.1).
- **`running` read as the machine's** (round 4's; O4-F6): it is this window's; another process's part closes by its record.
- **Every file written before row a read as one `end` per process** (round 4's; C4-1.1): a driven record's continuation and
  retry wrote none, so that `end`'s word and moment were another process's. A driven record's history is read as unrecorded.
- **A late capture line dropped at the writer** (the other fence C4-2.1 offers): it would hide a child its harness announced,
  which D173's ADR-B forbids. The line keeps its process's token and is placed in its own generation.
- **A line with no token placed by its moment** against the `start` it follows: the fold joins nothing by time (§3.6), and a
  clock read twice is no order. Rule 1's measure counts such lines instead, so the residue is a number, not a guess.
- **Telling `ANSWERED` under `HeldAnswers`' gate** (C4-3.1's other way): every landing would hold the gate across the bus's emit,
  the stall NOBLOCK1 removed. Numbers make any order of delivery safe, as D182 point 3 has the slices.
- **Every held read made a value and numbered at once** (C4-2.3, C4-3.1): today's reads are tested on a refusal and a failure's
  re-ask; this read is the first numbered one, and another is numbered by a row of its own.
- **Every working child kept past the bound** (round 4's, from O3-F11c; C4-4.1): forty-one cannot fit forty, and a chain deeper
  than the bound cannot fit at all. The bound fills by chain and counts what it leaves.
- ***Team at work* and `sessions team` widened** (round 1's): D203's word and verb (§5.4, §7).
- **A second running view or a free graph of who is at work**: the running view is GRAPHVIEW1's, in *Workflow*. This view is a list,
  which reads at every width (the flowchart design §8).
- **Daoris splitting a chat's roadmap into quests by itself**, or a coordinator agent dispatching the parts: D193's rejection, D203
  §3.4.
- **A poll** of the lineage or the boxes from the page: NOBLOCK1's rule.
- **The hint shown everywhere a way is not open**: most chats need none. It is said where subagents ran a roadmap (§6.4).

## 9. Not built, and unmeasured

- **Unmeasured:**
  - whether `SubagentStart` reaches the hook over the adapter, and how many `acp` ids join a hook id on a first run and a re-run
    (row a's first step);
  - the view's budgets in zh at 300 px;
  - how many lineage lines a long process writes, and so the fold's cost per wake.
  - *(R2-O13b)* Round 2's last item, whether a chat counts as its repository's active session for `one-at-a-time`, is answered by
    the code: it does (§6.4).
  - *(C2.6)* whether 40 members a group is the right bound, and how long a 40-member answer takes to serialise on the install;
  - *(C3.1)* whether agents told their routing standing hand rows out as the skill says, which only the install's sessions show.
  - *(Round 4)* how often a held read wakes on the install once call lines wake only for a new run and each read wakes once;
    row a's first week of the install's machine log says it, beside the measure in D173's note.
  - *(C4-2.1)* whether `DAORIS_LINEAGE_PROC` reaches the hook and the connector on each door, and so how many of their lines the
    fold places by position, and how many of those sit after a `start` their own moment precedes (rule 1).
- **Not designed here:**
  - DEV6's lane reservations and cap, and DEV9's steward;
  - a separate rule for self-published quests (§6.5);
  - *(R2-O2, the read's alternative)* a resumed lead's tree synced with the lane part it waited on, as `GoOnInSyncAsync` syncs a
    chain step with its source (`Driver.cs:549-575`). The doctrine's answer (§6.2) makes it unneeded; if it is wanted, it is a
    later row;
  - a child's own transcript after the window restarts (the console streams stay in memory);
  - *(C3.1; round 2 left it here)* a chat told whether its repository is driven here before it publishes is now designed: the
    routing standing's line (§6.2), built by row e. What stays undesigned is a standing told the moment it moves, mid-process: it is
    told at the next start (§6.2).

## 10. What it amends, once accepted

*(Round 4.)* Row a's acceptance amends D173 alone, and D118's frame by its side bar view. The D203, D115, D167, D176 and D177
amendments below come with the routing half's acceptance.

- **D173**: the FABRIC-C1a note's *ids only* is kept for the hook and the connector. The driver's lines gain a bounded label, a
  parent and the wire's end word, and the fold its moments and the generation join (§3.6). The file becomes transcript-class, kept
  on this machine (D47), since a label may name a path (R2-O13f). FABRIC-C1b's read and display are COWORKVIEW1a's, no longer after
  C2a. GRAPHVIEW1 stays the graph's half. *(Round 3)* The driver also marks each process's start, and the fold reads generations
  and runs, a last generation closed by the record's own end; a hook's unmatched starts are counted as starts; an unreadable file
  is never an empty fold (C2.2, C2.3, C4.1). *(Round 4)* Every process the driver starts for a part is bracketed by `start` and
  `end`; a last generation closes when the part's process is gone; a run begins at a said start or, for an id with none, at its
  first line; the hook's unmatched agent ids are counted (C3-2.1 to C3-2.3, O3-F1). *(Round 5)* Every line names its process by a
  token the driver binds into the process's writers, the `start` making the file, and a line is placed by that token; a driven
  record's history is read as unrecorded; a root's call wakes nothing; the read's failures are values and its deliveries
  numbered; its inputs' health is read; the bound fills by chain and counts what it leaves (C4-1.1, C4-2.1 to C4-2.4, C4-3.1,
  C4-4.1). The lineage's twin row gains `proc`.
- **D203**: points 1 and 2 stand as written. The view draws the team (the entry's parts and its co-workers) and, beside it, records
  point 1 keeps out, each under its own heading. `sessions team` stays COWORKUX1a's. This design's verb is `sessions at-work`.
  *(Round 3)* Its co-workers are COWORKUX1a's exchange read and open COWORKUX1b's thread; the view never reads the boxes (C5.3,
  C6.1). The routing doctrine is for leads and chats alone (C3.2).
- **D115**: §2.2's *work for another session* is, under D167, a further part of the publisher's session where the lane is its own
  repository's. Its lanes are shown on that session's view, and a repository where the way is not open is told how (§6.4, with
  SELFASK1, built). *(Round 3)* An agent is told its repository's routing standing at each start, and hands rows to its lanes only
  where it was told the way is open (C3.1).
- **D167**: a lane quest to the session's own repository is a further part of the session that published it (point 2's origin
  rule), drawn once as a part, and landed by the entry's one *Accept*. *(Round 3)* A quest's current executor is its newest record
  here; a record its resume replaced is an earlier part, whatever its end code (C2.1). *(Round 4)* "Newest" is `LastRun`'s own rule,
  one shared function, then a chat's take, and only where the take is this machine's (O3-F4, C3-1.1).
- **D176, D177**: a request names who called it (row f).
- **D118's frame**: a side bar view, `atWork`, after *Workflow*.
- **These stand:** D24, D45, D46, D47, D56 (NOBLOCK1), D116, D137, D169, D186 and the flowchart design, D188, D193, D200.

## 11. Round 2: the read's findings and their answers

Every finding of the Opus foundation read of round 1 (`docs/2026-10-24-cowork-view-reads.md` §4), each checked against the code on main
at `426ed2b73`. This table is round 2's record and is kept as written; where round 2's read changed an answer (the `work` scope and its
narrowing, the turn, the strip's order, trees as a bound), §12 says so and the sections above carry the change. *Taken* means the design now says it. *Answered* means it is not taken as written, for the reason given. *Deferred*
means a later row carries it. The read's §1 to §3 corrections are findings below.

| # | Finding (blocking in bold) | Checked against | Answer |
|---|---|---|---|
| **1** | **A lane quest to a session's own repository makes a further part of that session's line** | `SessionEntries.cs:33-37`, `:96-113`; `SessionLedger.cs:1599-1616`; `Planner.cs:1716-1759`; `D167.md:22-31`, `:73-82`, `:286-336` | **Taken**, the second way: such a session is a part, drawn once under *This session* with its lane, its routing and *alongside*, in the *Parts* strip's order. An untaken quest to the session's own lanes is under *Asked through quests* until taken (§2 point 4, §3.1). The subagents are read only for the lead (§3.1, with finding 7). The lead is the attended live part, else the newest live part by `created` then id. The entry rule's effect is stated in §1.2, §1.3, §1.9, §6.1 and §6.5, and the landing on the entry's one branch. The hint says *further parts of this session*. Row b's proof asserts one row per part and the strip's order. A D167 note is added beside D115's. |
| **2** | **The routing doctrine parks a driven lead on the person** | `Observation.cs:159-174`; `Quests.cs:96-102`; `QuestTools.cs:366-374`; `QuestExchange.cs:1454-1505` | **Taken** as named: a chat ends its turn; a driven session finishes its quest naming the quests it handed on, or waits on one and ends its turn, and never ends a turn holding its quest with nothing named (§6.2). §3.2 says a lead waits on at most one quest. The exchange refuses no wait on a quest to one's own lane, so the wait is open. |
| **3** | **The routing names no rule of the person's and no precondition** | `QuestTools.cs:148-163`; `QuestExchange.cs:108-113`; `HelperBox.cs:86-88`; `Planner.cs:1889-1891`, `:1964-1966`, `:2010-2021`, `:2048`; `D203.md:31-33` | **Taken**, the second way: the rule is the person's drive with trees of its own and the machine's cap, which hold today. The reason is that a quest to a driven repository starts under exactly these, whoever publishes it, and a helper's rule covers a turn outside the plan (§6.5). The skill section applies only where lanes are listed and the session is driven, or the chat's person asked (§6.2). The hint is one of three, naming a missing drive or one-at-a-time where the way is not open (§6.4). A `helpers`-shaped rule is rejected for now and left as a later row (§8, §9). |
| **4** | **None of §4's wakes reaches a held read** | `HeldAnswers.cs:20-22`, `:74-106`, `:170-177`; `held.ts:24-34`; `stateHub.ts:27-68`, `:93-100`; `StatePublisher.cs:258-264`; `HomeWatch.cs:196-213`, `:234-258`, `:270-280`; `QuestsStateSlice.cs:130-144`; `CallsStateSlice.cs:23-24` | **Taken**, the first way: every wake tells a new `work` scope on `STATE_CHANGED`, and the hub invalidates `SESSION_AT_WORK`, whose next ask runs the work and is told `ANSWERED` (§4). The ledger's families come through `MoveHeard` by the existing `HearLedger` seam. The lineage files come through one fixed part over `sessions/` hearing `*/lineage.jsonl`, its judge reading by name only the held sessions', in the window's own process as `ForProposals` is. The boxes are fixed parts of the same watch. The `calls` slice is no longer a wake: *last heard* comes from the streams (finding 8). |
| **5** | **The protocol door's `acp` line omits the parent and can write a false end** | `AcpStreams.cs:110-148`, `:152-204`; `lineage-hook.mjs:31-46`; `ChatRunner.cs:2397-2419`; `ChildBindings.cs:195`, `:211`, `:241-244`, `:268-291`; CONSOLE2 evidence `:69-70` | **Taken** as named: the `acp` line carries `parent` (the announcing child's id) or `depth: 1` (the root's); the end line is written only from `subagent_state_update`, and `EndAllAsync` writes none, so the part's `end` line makes an open child `ended` or `lost`; the fold keeps the wire's end word and the view draws it with the panel's words (§3.6, §5.4). Row a's proof covers all three. |
| **6** | **The join's fallback draws one child twice; the evidence about the id is on record** | CONSOLE2 evidence `:61-64`; `D173.md:100-102`; `ChildBindings.cs:167-178` | **Taken** as named: the evidence is cited (§1.1). The join is by equal id or the `:generation:<n>` suffix, as the fold already reads a child run again. An unjoined announcement stays the child's one row, and the hook's unjoined children are a count beside the group, never a second row (§3.6). Row a measures a first run and a re-run, and its proof includes the unjoined case. |
| **7** | **No record keeps a turn, so *Inside this turn* names a boundary nothing holds** | `ChatRunner.cs:1804`, `:1826`, `:2397-2399`, `:2486-2497`; `glossary.json` (*run* 进展, matched as *this run*) | **Taken**, the first way, with one change of wording: the group is *Subagents* 子智能体, and its note says they work inside the session's own agent and stop when it stops. *In this run* is not used, since the glossary's *run* is a workflow's (进展) and `names:check` would read it so. The edge "the turn has not ended because they run" is dropped; the heading says *n working* (§3.2, §5.2). The hint counts per process, since its last `end` line (§6.4). The stored turn line is rejected (§8). |
| **8** | ***Last heard* stays still while a child works, and duplicates the watcher's** | `AcpStreams.cs:16-21`, `:95-102`; `SessionOutput.cs:9`, `:90-110`; `SessionWatch.cs:64-71`, `:183-189`; `CallsStateSlice.cs:37-41`; `headFacts.ts:199` | **Taken** as named: *last heard* is the newest line of the part's and its children's streams, kept in memory; after a restart, *nothing heard since the window opened at…*; then the watcher's hearing, then the events and the lineage. The head's fragment gives way to *quiet* wherever the watcher marked the session (§3.7). |
| **9** | ***Team at work* and `sessions team` take D203's word and verb** | `D203.md:22-24`, `:58-60`, `:83`, `:199-206`; coworker design `:282-283`, `:661-664` | **Taken**, the first way for each: the title is *At work* (分工); the read is `SESSION_AT_WORK` and the verb `sessions at-work`, beside an existing `sessions team` left exactly as COWORKUX1a defines it; the keys are `work.atWork.*`; the types are `AtWork…`. The D203 note says that points 1 and 2 stand, and that the view draws the team and other records beside it, widening neither (§2 point 6, §10). `SESSION_WORK` and `sessions work` were not taken, since `work` already names the units and `WORK_STATE`. |
| **10** | **Rows a and b are layers, and the first row shows the owner nothing** | `D187.md:13-16`; round 1 §7 | **Taken** as named: row a is *a chat's subagents seen*, built driver, modules and web-shell by one builder, with the join measured as its first step and finding 5's line in it. Each later row is a feature with its own proof (§7). Rows are renamed a-f. |
| 11 | A chat can take a quest | `QuestTools.cs:395-403`; `Sessions.cs:246-250`; `SessionEntries.cs:102-106`; `D167.md:176`; `D188.md:18`, `:29-30` | **Taken**: `Session.Quest ?? Session.TookQuest` everywhere a part's quest is read; §6.3 rewritten and cites D188 point 2; §1.9 says it. |
| 12 | A null `PublishedBy` does not mean "by you" | `Quests.cs:104-109`; `WorkUnit.cs:64-65` | **Taken** as named: a quest with a `Parent` is *the next step of #parent*, placed by `follows`; one whose `From` is `ask #id` is the ask's; only a quest with neither is *by you* (§3.1, §3.4). |
| 13 | An untaken quest is shown without the reason it sits | `signals.ts:5-7`, `:145-158`; `Planner.cs:1023-1048` | **Taken**: the edge carries the tick's verdict and its fact, worded by the page as the Overview words them, *not taken yet: {why}* (§3.2, §3.8, §5.4). |
| 14 | Smaller claims to correct | `QuestTools.cs:37-40`; `D173.md:131-133`, `:5177-5180`; `AcpStreams.cs:158`, `:169-181`, `:202`; `Planner.cs:1964-1966`; `DaorisHome.cs:340`; `TASKS.md` | **Taken**, each: (a) §0 and §1.9 say the tool's description names lanes, and the case lacked lanes; (b) §1.10 says the hook's record on the install is unmeasured, row a's to read; (c) §1.1 and §3.6 say only a root's child has a card, and that a nested child's label reaches disk for the first time, with the reason that holds; (d) §1.3 and §9 say PAR1 already runs sessions side by side with trees of their own, and DEV6 adds reservations and a cap; (e) `data/helpers`; (f) rows cited by id. |
| 15 | Three Chinese words go against the glossary | `glossary.json` (*part* 段, *account* 账户, *subagent*); `names-check.mjs:184-188`, `:197` | **Taken**: 随所属段结束, 账户, *Subagents* (§5.4). |
| 16 | The stories use the main area's widths, not the side bar's | `storyWidths.tsx:9-13`; `layout.ts:44`; `AskPanel.stories.tsx:335-341`; `FilePreview.stories.tsx:5` | **Taken**: §5.3 has a 300 px row and a 430 px row; the stories are at 300 and 430 px in the side bar and 680 px as the frame, in both languages and both themes. |
| 17 | Row c's proof cannot happen in one run | `ChildBindings.cs:241-244`; `ChatRunner.cs:2486-2497` | **Taken**: row a's install proof is two runs, a killed one and a resumed one, and names *completed 14:02*, *ended with its part* and *lost* apart (§7). |
| 18 | A Codex lead's children are seen only when they call Daoris | `ChildBindings.cs:223-235`; `D173.md:120-122`, `:5184-5188` | **Taken**: for such a lead the subagents group says so in place of the empty state (§2 point 3, §3.1, §5.4), with its story. |
| 19 | The quests group re-derives what the units already carry | `WorkUnit.cs:53-73`, `:140-152`; `QuestUnits.cs:20`; `WorkUnits.cs:54-70` | **Taken**: each quest member's state, wait and step are read from its unit, built by `QuestUnits.Read` over the quests and sessions held. That is the same adapter `WorkUnits.ReadAsync` uses, without the branches' inventories it also reads, so the view and GRAPHVIEW1 agree on a quest's state (§3.1, §4, §5.1). |

**What stays owed.** Codex's read, on this text (D191, its 2026-10-23 amendment). The read asked for a narrow next round over §1.3,
§1.9, §3.1-§3.2, §3.6-§3.7, §4, §6 and §7. The design stays proposed until both reads' blocking findings are taken.

## 12. Round 2's read: its named changes, and where each went

Every finding of the Opus foundation read of round 2 (`docs/2026-10-24-cowork-view-reads.md`, *Opus (foundation), round 2* §4),
taken as the read names it and checked against the code on main at `0d3af537b`, whose `src/` is `eb11f2f5f`'s (it differs from
round 2's `426ed2b73` in `WorkflowRun.Read.cs` and its tests only, which this design does not touch). Each place is marked *(R2-On)*
in the text.

| # | Finding (blocking in bold) | Checked against | Where it went |
|---|---|---|---|
| **1** | **The `work` wake's narrowing by session cannot be carried, and would miss the reads holding the part that moved** | `QuestsStateSlice.cs:132-137`; `HomeWatch.cs:316-344`, `:342`; `stateHub.ts:27-68`, `:93-100` | **Taken**, the read's first way: the `atWork` scope names no session, and the hub asks every held `SESSION_AT_WORK` again, an ask's among them; the hub and `HeldAnswers` coalesce them (§4 steps 1-2). The lineage and box wakes tell it naming nothing (§4); §1.8 says what each wake carries; row a's tests ask an ask-keyed read again; row c's boxes tell no asker; §8 rejects the narrowing. |
| **2** | **The doctrine's wait on a lane part resumes the lead without the part's work, and may give the part the lead's tree** | `Planner.cs:1716-1767`, `:1773-1801`, `:2195-2199`; `HelperWaits.cs:36-38`; `Driver.cs:533-575`; `QuestExchange.cs:1503-1505`; `Observation.cs:104-111` | **Taken**, the read's smallest change: §6.2 says a lead waits on a lane part for its answer, never its code; work its next step builds on is not handed out, or it finishes its quest and the entry's *Accept* joins the parts; it commits before it waits; the resume is D79's door, unchanged by D188 point 5, bounded by drive, cap and strikes (§6.2, §6.5). §1.2 states the facts; §1.9, §3.1, §6.1 and row b say *alongside* holds only while the lead is live, read from the part's own tree. The read's alternative, a resume-sync row, is named in §9 and not designed. |
| **3** | **A waiting part reads as ended, so the lead moves off it and the fold hides it** | `Observation.cs:104-111`, `:164-165`; `Web/data/api.ts:423-424` | **Taken** as named: *live* is active, or `completed` with its quest taken and `ended.awaits` or `ended.awaits-helper`; such a part is never folded and its wait is the *Waiting on* line (§3.1, §3.2, §5.2). The lead is the newest live part no other live part waits on, so a lead waiting on its lane part stays the lead. The type gains `waiting` (§3.8); row a's tests and row b's proof have a lead waiting on its lane part; a story draws it. |
| 4 | The turn claims are wrong | `SessionEvents.cs:33-34`; `Acp.cs:1999-2005`; CONSOLE2 evidence `:74-77` | **Taken**: §1.7 says the events keep each turn's end and the adapter holds the turn for its children, while the lineage keeps none and the view groups by what it holds; §3.2 drops *a background child holds no turn* and the heading may say *its turn waits for them* (new key); §5.4's *Turn* line and §8's reason corrected; the D173 note likewise. |
| 5 | The stream wake goes through `STATE_CHANGED`, which forgets every read under way | `DriverModule.cs:102-107`; `CallsStateSlice.cs:9-45` | **Taken** as named: *last heard* is a keyed `heard` partition of the `calls` slice, judged from memory as `quiet` is, with no `STATE_CHANGED`; the head and the view read it beside the held answer (§3.7, §4, §3.8); row a builds and tests it (`CallsStateSliceTests`); §8 rejects the telling. |
| 6 | The quests group's inputs are held by no route, and its units miss a chat's or a teammate's take | `QuestUnits.cs:20`, `:36-37`; `HelpRoutes.cs:293-294`; `WorkUnits.cs:66-71`; `Planner.cs:888-891`, `:1770-1771` | **Taken**: §4 names the two whole-store service reads per composition, on the held work's task, and their cost per wake; *not taken yet* only where the quest is `Open`; a taken quest with no executor here reads *taken by {conversation}* through `TookQuest`, or *taken on another machine* (§3.1, §3.2, §5.4, row b, a story). |
| 7 | The parts group's order contradicts the strip's, and one way a part joins has no words | `SessionEntries.cs:97`; `partsLife.test.ts:11`, `:88`; D167 point 2 | **Taken**: the view and the strip draw the same parts, each once, the view ordered lead, live, ended and the strip as lineage (§2 point 4, §5.1); row b asserts the set and one row each; *the ask's quest #{quest}* is added (§3.1, §3.4, §5.4). The read's wording *the ask's request* is said *quest*, since the glossary keeps *request* off the window (`glossary.json`, *request*). |
| 8 | The join's count says what no record holds | CONSOLE2 evidence `:61-62` | **Taken** as named: *{{count}} more seen only by its hook* (`hookOnly`), and where the measure finds no join on a door, that door's hook children are neither rows nor a count, recorded in D173's note (§3.6, §3.8, §5.2, §5.4, row a). |
| 9 | Two names collide | `WorkStateSlice.cs:45`; `EntryAccepts.cs:32-44` | **Taken**: the scope is `atWork` (§4); §3.1 says the view's lead is not `EntryLead`, and the type's field is `leadPart` (§3.8). |
| 10 | Words | `glossary.json`; `AcpStreams.cs:134-139`; `work.panel.json:20`; `zh/settings.rules.json` (`settings.rules.hook`) | **Taken**: (a) co-worker 同事, lead 主导, alongside 并行 and *not driven here* 不在本机驱动 move to *New glossary terms*; (b) the count's zh is 另有 {{count}} 个只被钩子看到, counting subagents; (c) §5.4 says the panel's sameness holds for the wire's words only, the panel keeping *ended with its session* for an end the driver made. |
| 11 | The hints overlap, and the chat's case has no drive precondition | `Planner.cs:1889-1893`, `:2010-2021` | **Taken**: the order is `not-driven`, `no-lanes`, `one-at-a-time`, each worded true of a repository with no lanes (§6.4, §5.4, §3.8, row e's tests); the chat's case adds *and its repository is driven here* (§6.2). |
| 12 | Two proofs need correcting, and one row can move | `Planner.cs:904`, `:1301`; `ChildBindings.cs:241-244` | **Taken**: (a) row b's run has a chat publisher (CAPCOUNT1) and the order, the other repository's quest taken and waiting, then the lane quests; (b) row a says only `ChildBindingsTests` holds *ended with its part*, with the reason; (c) row e is moved up to second, its skill section depending on no view row and carrying R2-O2's answer, still under D191's note. |
| 13 | Smaller claims | `ChangeWatch.cs:316-339`; `Planner.cs:1290-1292`, `:1300`, `:2010-2021`; `SessionOutput.cs:9`, `:98-112`; `twins.md:180` | **Taken**, each: (a) the coworker design's sections cited as its own (§3.1, §5.4, §6.5, §8) and the D203 note's; (b) §9's chat holder answered, and `one-at-a-time` said true of a chat (§6.4); (c) §1.8 and §8 give `HomeWatch`'s surface as the reason, `ChangeWatch` having `Unwatch`; (d) §3.7 says `SessionOutput` keeps no moment today; (e) §6.1, §6.5, §8 and the D115 note say drive and cap bound whether a quest starts, trees whether alongside or after; (f) §3.6 and §10 say row a amends the twin row and the file becomes transcript-class, with the D173 note. |

**What stays owed.** Codex's read, on this text. Under D191's note of 2026-10-24, only row a is built on this until it lands, and
nothing carrying a row of it is staged. *(It landed on 2026-10-25, asking another round; §13 answers it.)*

## 13. Round 3: Codex's read answered

Every finding of Codex's read of round 2 with its named changes (`docs/2026-10-24-cowork-view-reads.md`, *Codex, round 2*), each
checked against the code on main at `972b17577`, the commit it read. Codex numbers its findings by section, so `2.1` is its §2
finding 1, marked *(C2.1)* in the text above. *Taken* means the design now says it, and where; *disputed* would give the code line
that shows why, and no finding is disputed: each was confirmed in the code.

| # | Finding (blocking in bold) | Checked against | Answer |
|---|---|---|---|
| 1.1 | Helpers consume the machine cap | `DriverHelpers.cs:130-131`; `Driver.cs:1006`; `HelperJudge.cs:61-84` | **Taken**: a helper takes a slot under the cap; what `helpers` adds is its own admission outside the quest plan (the rule's yes, its agent, two at once per asker), so a self-published quest, already under the plan's drive and cap, needs no rule like it (§6.5). §8's rejection keeps that reason. |
| 1.2 | No local executor does not prove another machine took the quest | `Planner.cs:33-37`, `:1746-1765` | **Taken**: a taken quest with no executor here reads *taken by {conversation}* through `TookQuest`, *taken on another machine* only where `Holder` names a machine other than `Here`, else *taken, by no session recorded here* (§1.3, §3.1, §3.8, §5.2-§5.4, row b). |
| 1.3 | SELFASK1 has landed | `QuestExchange.cs:539-552`, `:759-761`; `KnowledgeSearchTools.cs:326-330`; the archive's SELFASK1 entry | **Taken**: §0, §1.3, §1.9 and §1.10 say the old refusal in the past and the new one as built; §6.4 cites it; row e no longer waits on it; §7's last line and §10's D115 line say it is built. |
| **2.1** | **Historical waiting parts remain live after replacement** | `SessionLedger.cs:1612-1616`; `Planner.cs:38-50`, `:1577-1581`; `QuestUnits.cs:29` | **Taken**: a quest has one current executor, its newest record here (the planner's `LastRun`), and *live*, *waiting* and *answered* are read from it alone; a wait stands only while its question is not closed, since the unit keeps the link after the answer and the resume; a replaced record is folded (§1.2, §3.1, §3.2, §3.8, §5.2). Rows a and b test wait, answer, replacement working, and answered before the resume; §8 rejects the end code read alone. |
| **2.2** | **An application crash leaves children falsely working** | `Orphans.cs:81`; `ChildBindings.cs:241-244`, `:278`; `ChildLineage.cs:118-123` | **Taken**: the driver writes a `start` line at each process's start on both doors, to an existing file only; a generation closes at its `end`, at the next `start` (`lost`), or, the last one, at the record's own end where it is not live (`lost` for `stopped` or `failed`, `ended` for `completed`), the record's state passed to the fold and nothing written (§1.1, §3.6). Row a's `ChildBindingsTests` and a *Process* case; §8 rejects the sweep writing an `end`. |
| **2.3** | **Reopening a child does not reset its terminal state** | `ChildBindings.cs:167-178`, `:195`, `:211`, `:278` | **Taken**: a child's state and moments belong to a run, begun by a start the harness said and ended by its stop or its generation's close; a native id started again is a new run, *working*; a call or stop line opens none (§1.1, §3.6, §5.2). Row a tests an id started again in a later generation and in the same one. |
| **2.4** | **The fixed watch misses dependencies and its first change** | `HomeWatch.cs:274-280`, `:316-331`; `DriverModule.cs:106-108` | **Taken**: a composition keeps every file it read by name with the stamp taken before reading (every part's lineage, an ask's entries', row f's files); the judge compares with those stamps, never its own first sight, and judges at once when a composition lands; a read let go drops its files (§1.8, §3.8, §4). §3.7's fourth source drops the event log, which no wake covers. Row a tests an append between composition and registration, a sibling's and an ask's. |
| **2.5** | **Scheduling and configuration changes have no complete wake path** | `DriverLoop.cs:984`, `:987`, `:1019`, `:1027`, `:1321-1326`; `StateSlices.cs:378`; `DriverStateSlice.cs:36`; `Web/bridge/driver.ts:337`; `AskWork.tsx:56` | **Taken**, each fact read where it already moves: the tick's reason on the page from `useConsidered`, beside the answer, as the ask's work reads it (§1.3, §3.2, §3.9); the hint's standing, the one configuration fact the answer keeps, judged in .NET from the driver slice's snapshot and the registry's declared lanes, the slice's `Pushed` (filtered to `drivable` and `trees`) and a registry the tick heard move telling `atWork` (§4, §6.4). Row b tests a tick's reason moving with no write; row e a `driver.json` edit and a registry's lanes moving the hint. |
| 2.6 | Background composition alone does not prove NOBLOCK1 | `HeldAnswers.cs:78`, `:140-160`, `:164-165` | **Taken**: each group is bounded at 40 members, the rest counted by state; `HeldAnswers` serialises off its gate (row a's change, shared by every held read); the answer drops `at`, so an unchanged composition tells nothing; row a's timed case adds a 40-member landing beside three other held reads (§1.8, §3.8, §4, §7). |
| **3.1** | **The chat's driving precondition has no specified source** | `KnowledgeSearchTools.cs:288-330`; `ChatRunner.cs:1380-1382`; `Adapters.cs:334-339` | **Taken**, both ways named: the driver tells each lead and chat its repository's routing standing, judged once in .NET, in BRWHINT1's places and at each later start; with none told, the standing is unknown and the agent works its rows itself (§6.2, §6.4). Row e builds the judge and the line before the skill section; §8 rejects the service reading the driver's configuration; §9 drops what round 2 left undesigned. |
| 3.2 | Explicitly exclude sessions holding restricted roles | `RoleFilter.cs:54-58` | **Taken**: the section is for leads and chats, never a helper, partner, reviewer, Ask Daoris, an intake or a brainstorm's pass, and says so first; none of them is told the standing (§6.2, row e). |
| **4.1** | **Hiding unmatched hook evidence breaks truthful partial visibility** | `ChildLineage.cs:125-139`, `:137`; `D173.md:11-12` | **Taken**: the hook's unmatched starts are counted as starts, never children, on every door (`observed`, replacing R2-O8's count and its suppression); `Read` answers none, read and unreadable apart, and an unreadable lineage is said and withholds the empty sentence (§2 point 3, §3.6, §3.8, §5.2, §5.4, row a). |
| 4.2 | Loading, failure and retained-stale states need designs | `held.ts:20-34`; `HeldAnswers.cs:96-100`, `:164-165`; `D182.md:559-575` | **Taken**: §5.2 draws reading (a skeleton, never *nobody*), failed (its sentence and *Try again*), held (NOBLOCK1's, unchanged) and in part (an unreadable lineage); the page reader takes pending as not answered (§4); a story each (§5.3); keys `reading`, `failed`, `unreadable` (§5.4). |
| 4.3 | The remaining boundaries hold; three disagreements with the Opus reads | the reads record, *Opus (foundation), round 2*, findings 3 and 8 and its verdict | **Taken**: the boundaries stand as checked. The disagreements are decided below. |
| **5.1** | **Row a needs the lifecycle and watch contracts** | §7 row a | **Taken**: row a builds and proves C2.1 to C2.4, C2.6, C4.1 and C4.2, and its trap names the end code read alone (§7). |
| **5.2** | **Row e cannot precede observable routing eligibility** | §6.2; `KnowledgeSearchTools.cs:288-330` | **Taken**: row e builds the standing's judge and its line first, then the skill section that reads it, and waits on no other row; it stays second, its hints after row a. Row b follows a with C2.1's predicate and C2.5's reads (§7). |
| **5.3** | **Row c promises a thread another feature has not supplied** | coworker design §6.4, §12 rows 1 and 3 (`docs/2026-10-23-coworker-design.md:661-673`) | **Taken**: row c follows COWORKUX1a and takes its co-workers from that row's exchange read; each row opens COWORKUX1b's thread where it has landed and the member's own session page until then; row c writes no reader of its own (§3.1, §3.9, §5.1, §7). |
| 5.4 | Row d needs an ask terminal twin | `D185.md:24-25`; `src/Daoris.Desktop/Daoris.Desktop.Driver.Host/Program.cs:470-475` | **Taken**: `daoris-driver ask at-work <ask> [--json]`, routed as `ask channel` is, its words and JSON tested (§7 row d). |
| **5.5** | **Row f adds records without adding their wakes** | `MeetingFiles.cs:29-38` | **Taken**: the watch's topic gains a fixed brainstorms part hearing `*/request.json` and `*/state.json`; row f's files are dependencies at their stamps; its proof moves a meeting's state alone (§3.1, §4, §7 row f). |
| 6.1 | A smaller shape with the same outcome | `AttendedSession.tsx:323-324`; coworker design `:661`; `RouteFlights.cs:22-34` | **Taken where the outcome holds**: the read answers only what no reader holds; the co-workers are D203's exchange read and thread, the channel the panel's read, why a quest sits the tick's, a meeting a link; the service's two reads are shared across a burst's held reads, each payload keeping its own answer and `calls.heard` kept (§2 points 1 and 7, §3.3, §3.8, §3.9, §4, §5). **Not taken:** links alone in place of the one place, under §8, since the case was work spread over places nobody looked at together. |

**Where Codex and the Opus reads disagree**, decided on the code at `972b17577`:
- **The Opus read's round-2 finding 3, a waiting part as live.** Codex is right that the predicate it named is not sufficient: a
  resume opens a new record and leaves the one that waited `completed` with its code (`SessionLedger.cs:1612-1616`), and the quest
  keeps naming its question after the resume (`Planner.cs:38-50`). The Opus finding stays right that a waiting lead must read as
  live; round 3 keeps that, read from the quest's current executor (§3.1).
- **The Opus read's round-2 finding 8, the hook's children hidden where a door never joins.** Codex is right on the suppression:
  hiding evidence lets the view say *nobody* over starts a hook saw, against D173's ADR-B (`D173.md:11-12`). The Opus finding stays
  right that they must never be counted as children twice. Round 3 keeps both: they are counted as starts, never as children, and
  never hidden (§3.6).
- **The verdict.** The Opus read would accept round 2 after named changes; Codex asks another round. Each of Codex's eleven blocking
  findings holds in the code it cites, several of them in the predicates round 2's named changes had just added, so the verdict is
  Codex's: another round, which this is.

**What stays owed.** Both readers' reads of round 3 (D191). The design stays proposed until both have read it and their blocking
findings are taken; until then nothing of it is built, and nothing carrying a row of it is staged. *(Both landed on 2026-10-25;
§14 answers them.)*

## 14. Round 4: the reads of round 3 answered

Every finding of the Opus foundation reader's read of round 3 and of Codex's (`docs/2026-10-24-cowork-view-reads.md`, *Opus
(foundation), round 3* and *Codex, round 3*), each checked against the code on main at `b4f096cc0`. Marked *(O3-Fn)* and *(C3-n.m)*
in the text. *Taken* means the design now says it, and where; *carried* means it is the routing half's, corrected in its text and
settled in its own round. No finding is disputed: each was confirmed in the code it cites.

**Where the two reads overlap, the stricter is taken** (the steward's direction):
- **Call-only children** (O3-F1, C3-2.1). The Opus read opens a run at the first call where the harness says no start; Codex's keeps
  any first-observed child with an unknown start and bars a late call from reopening a closed run. Codex's is the wider and is
  taken, for every harness the rows come from (§3.6).
- **The watch** (O3-F2, C3-2.4). The Opus read's wake-once and absent stamps, and Codex's fence over records as well as files: both
  are taken, as one judge over every input's registered version (§4.1).
- **The registry's lanes** (O3-F3, C3-1.2). Both of the Opus read's ways together, the signature carrying lane ids and the lanes a
  dependency compared at each tick, with Codex's proof of a lanes-only change; carried to the routing half, since row a reads no
  registry (§4.2, row e).

**The Opus foundation reader's read of round 3** (11 findings, 3 blocking; accept after named changes):

| # | Finding (blocking in bold) | Checked against | Answer |
|---|---|---|---|
| **F1** | **The run rule erases every Codex child** | `ChildBindings.cs:154-157`, `:223-233`; `ChildBindingsTests.cs:125`; `D173.md:121-122` | **Taken**, as Codex's wider C3-2.1: a native id with no run in its generation begins one at its first line, a call's included, drawn *first seen*; a call or stop after its run ended begins nothing (§3.6; row a rules 6 and 7). |
| **F2** | **The watch wakes again until a composition is redone, and for good for an answer no page asks again** | `HomeWatch.cs:172`, `:331`, `:337`; `HeldAnswers.cs:40`, `:88-92`, `:181-188`; `stateHub.ts:80`; `DriverModule.cs:106-111` | **Taken** with C3-2.4: one judge compares registered versions; a read's dependencies leave the union when they wake it, back when it is asked again and lands; a missing file is stamped absent; tests of a held read no page asks and of three appends in one composition (§4.1; rules 9, 12). |
| **F3** | **The registry wake cannot hear lanes** | `Planner.cs:722-736`; `DriverLoop.cs:1027`, `:1329-1334`, `:1343-1356`; `Core/Platform/ChangeStream.cs:12-48` | **Carried**: the signature gains lane ids and the hint's read keeps its lanes as a dependency compared at each tick; row e's proof then passes (§4.2, row e). Row a reads no registry. |
| F4 | The current executor is not `LastRun`, and ignores whose take it is | `ServiceClient.cs:2134-2150`; `Planner.cs:33-37`, `:1739-1765` | **Carried**: `LastRun`'s own rule as one shared function, then `TookQuest`, and only where the take is this machine's; row b's tests of a refused resume, a stood-down one and a chat's take (§1.3, §3.1, §10, row b). Row a draws no responsibility. |
| F5 | The lineage wake tells `STATE_CHANGED` at the call rate | `ChildLineage.cs:41`; `LineageFile.cs:59-70`; `ChangeWatch.cs:177-180` | **Taken**, stricter than either way named: a call line wakes only where it begins a run, and the answer carries nothing only call lines move, the lineage's newest moment withdrawn from *last heard* (§3.7, §3.8, §4.1; rule 13). |
| F6 | Reviewers are not refused the quest tools today | `RoleFilter.cs:58`, `:77`, `:114`; `OpinionDesk.cs:128-131` | **Taken**: helpers and partners refused now, reviewers once COWORKUX1c gives them `serves`, excluded by the section's words until then (§6.2; D203's note). |
| F7 | COWORKUX1a's exchange read holds helpers alone | coworker design §6.3 (`:490-497`), §12 (`:661-681`) | **Taken**: row c draws what that read holds, reviewers and partners as COWORKUX1c and 1e extend it (row c). |
| F8 | The `undefined` pending sentinel turns reading into a failure | `src/Daoris.Web/package.json:30`; `main.tsx:31` | **Taken**: `{ reading: true }`, and a test that the query is not in error (§3.8, §4.1, §5.2; rule 15). |
| F9 | The empty sentence beside unmatched starts contradicts itself | design §3.6 at `308edb13d` | **Taken**: where the hook's unmatched ids are all there is, their count stands in the empty sentence's place (§3.6, §5.2; rule 16; a story). |
| F10 | The standing told is not recorded, and "never disagree" overclaims | design §6.2, §6.4; `D115.md:588` | **Carried**: a coded note names the standing told; *told: {standing}* where it differs; "the same judge", never "never disagree" (§6.2, §6.4, row e; D115's note). |
| F11 | Smaller | as each names | **Taken**, each: (a) row b's lane quest closes done by an answer or its own *Accept*, a held done keeping the lead waiting; (b) row b's other repository is not driven here; (c) a drawn child's ancestors are kept past the bound (§3.8; rule 14); (d) before the first tick an untaken row says *not taken yet* with no reason (row b); (e) a child closed by Daoris's reading reads *last seen* at its own last line (§3.6; rule 7); (f) row d's twin prints the answer only, and says what it leaves out (row d). |

**Codex's read of round 3** (19 findings, 13 blocking; another round). Its §0 re-reads round 3's answers to its round-2 findings;
the open ones there are its findings below:

| # | Finding (blocking in bold) | Checked against | Answer |
|---|---|---|---|
| **1.1** | **"Newest record" is not the planner's `LastRun`** | `ServiceClient.cs:2134-2150` | **Carried**, as O3-F4, with a refused resume, a stood-down one and a chat's take in row b's tests. |
| **1.2** | **The registry wake cannot observe lanes changing** | `Planner.cs:733-736`; `DriverLoop.cs:1343-1356` | **Carried**, as O3-F3, the stricter of both. |
| 1.3 | Reviewers are not refused by the role filter today | `RoleFilter.cs:114`; `OpinionDesk.cs:128` | **Taken**, as O3-F6. |
| **1.4** | **`RouteFlights` shares overlapping requests, not a burst's inputs** | `RouteFlights.cs:92`, `:101` | **Taken** for row a by removing the need: it reads the `groups`, `sessions` and driver slices' snapshots, no service (§4.1; rule 10). **Carried** for the routing half: an owner keeping a snapshot of the two reads per wake's generation, with a test of compositions that complete one after another (§4.2). |
| **2.1** | **Start-only runs discard call-only children** | `ChildBindings.cs:223`; `ChildBindingsTests.cs:125` | **Taken**, this read's rule over O3-F1's (§3.6; rule 6). |
| **2.2** | **Process generations need process ends** | `DriverContinuations.cs:1242`, `:1299`; `Driver.cs:3643`, `:3892`; `ChatRunner.cs:1441` | **Taken**: a `start` and an `end` around each of the four spawns, none again at the conclusion (§1.1, §3.6; rule 4). |
| **2.3** | **Responsibility still prevents crash closure** | `Driver.cs:3876`, `:3892` | **Taken**: the last generation closes when the part's process is gone and its record is neither queued, starting nor working, whatever it is responsible for; a test of that exact crash (§3.6; rule 5). |
| **2.4** | **File stamps do not repair a stale ledger-only composition** | `DriverModule.cs:106-109`; `HeldAnswers.cs:88`, `:147-149` | **Taken**, with O3-F2: the records, the entry and the running set are dependencies too, compared as a composition lands, which composes again where one moved (§4.1; rule 11). |
| 3.1 | The one-at-a-time rule contradicts its decision note | design §6.2; `D115.md:586` | **Taken**: D115's note says the lead's exception as §6.2 does, tested apart from a chat's (§6.2, row e). |
| 3.2 | No bypass found; describe *open* as availability | `Planner.cs:1940`; `Driver.cs:693` | **Taken**: *open* says the way is available, never that an account or agent runs it at once (§6.2). |
| **4.1** | **Unmatched starts still do not establish an empty view** | `D173.md:11` | **Taken**, as O3-F9 (§3.6, §5.2; rule 16). |
| **4.2** | **A failed refresh masquerades as current state** | `HeldAnswers.cs:164-165`; `held.ts:41-52`; `D182.md:564` | **Taken**: a failure over a held answer is told as `failed` and `kept`, written as `stale` without asking again, and drawn dimmed with its words and *Try again*; every other held read keeps its data (§4.1, §5.2; rule 15; a story). |
| 4.3 | No further D203, D167, D185 or parity violation | as cited | **Taken** as a check. |
| **5.1** | **Row a needs the corrected lifecycle and delivery contracts** | design §7 at `308edb13d` | **Taken**: row a's table names each rule with its proof, call-only discovery, processes within one record, a crash after a wait's conclusion and a ledger-only stale landing among them (§7 row a). |
| **5.2** | **Row e's lane proof cannot pass; its one-at-a-time wording** | design §7 row e | **Carried**: the signature's lane ids make the proof passable; the wording is one (row e, §4.2; D115's note). |
| **5.3** | **Row b inherits the responsibility selector** | design §7 row b | **Carried**, as 1.1 (row b). |
| 5.4 | Row c's partner and reviewer coverage | coworker design `:661` | **Taken**, as O3-F7 (row c). |
| 5.5 | Row d: what "same groups and waits" includes | `Driver.Host/Program.cs:474-479` | **Taken**, as O3-F11f (row d). |
| **5.6** | **Row f cannot discover a new request** | `MeetingFiles.cs:76` | **Carried**: the listings of the brainstorms' and meetings' folders are dependencies, and its proof creates a request after the view is held (§4.2, row f). |

**The verdicts.** The Opus read would accept after three named changes, each now made (F1, F2, F3). Codex asks another round, and
each of its thirteen blocking findings holds in the code it cites. Seven of them fall in row a (2.1 to 2.4, 4.1, 4.2, 5.1), and
round 4 settles each in row a's table; the other six, with O3-F3, are the routing half's (1.1, 1.2, 1.4's second half, 5.2, 5.3,
5.6), corrected in its text and carried to its round. That is the split the *Standing* records.

**What stays owed.** Both readers' reads of round 4, on row a's scope (D191). Row a is accepted, or not, on what they find in
its table; the routing half is read in a round of its own. Until row a is accepted nothing of the design is built, and nothing
carrying a row of it is staged. *(The Opus foundation reader's read landed on 2026-10-25, its named changes placed in §15;
Codex's is owed.)*

## 15. Round 4's named changes, and where each went

The Opus foundation reader's read of round 4 (`docs/2026-10-24-cowork-view-reads.md`, *Opus (foundation), round 4*), read at
`953ae0c48`: 12 findings, 4 blocking; *accept row a after named changes the steward can check without another read*. Each is
taken as the read names it, checked against the code on main at `deb766f20`. That commit's `src/` differs from `b4f096cc0`'s,
among the files this design reads, only in `ChatRunner.cs` (MODELROLE1d2's pending starts), which moves its spawn to `:1546`, its
browser line to `:1487` and its lineage `end` to `:2854`, the `end` still written in the run's `finally` after its conclusion;
§1.1 cites the moved lines. Each place a finding changes is marked *(O4-Fn)*, and each of row a's rules it touches is in §7's
table with its test. The read's §0 re-reads round 3's answers and its §1 lists the claims below that it found wrong; each is a
finding here. Codex's read of round 4 is owed, its credits out, so under D191's note of 2026-10-24 row a stays proposed and
nothing of it is built until that read lands.

| # | Finding (blocking in bold) | Checked against | Where it went |
|---|---|---|---|
| **F1** | **A Codex child reads *working* for the life of its process** | `ChildBindings.cs:223-233`; `D173.md:121-122`; `Adapters.cs:2063-2067`; `Acp.cs:1756-1770`; CONSOLE2 evidence `:74-77`; `ChildBindingsTests.cs:127-136` | **Taken** as named: an open run whose harness says no end is `unsaid`, *first seen {time} · its end is not said*, counted as working nowhere (§3.6, §3.8, §5.2, §5.4's `child.unsaid`); `turnWaits` on `claude-code-acp` alone (§3.8, §5.2); rule 1 measures `codex-acp`'s announcements and says which lines a Codex part draws its rows from, a Codex part with `acp` lines drawing from them until then (§3.6 *the join*); rule 6 tests an open Codex generation, the fixture given its `at` (§7 rules 1, 6, 8, 19). |
| **F2** | **A pending composition registers nothing, so the view stays *reading*** | `SessionsStateSlice.cs:71`; `GroupsStateSlice.cs:111`; `StateSlices.cs:170-180`, `:378-393`; `main.tsx:30` | **Taken** as named: a pending composition registers each slice's not-judged status, and a session in no entry is registered absent, so the slices' first push wakes it; rule 10 tests pending, then the answer, with no other ask (§4.1; rule 10). |
| **F3** | **A failure with nothing held asks again at every failure, and no failure carries its words** | `held.ts:45-47`; `HeldAnswers.cs:88-91`, `:96-100`, `:147`, `:176` | **Taken** as named: both failure events carry the refusal's words; this read's reader writes `{ failed: { why } }` or `stale: { why }` and asks nothing; *Try again* is the only re-ask; rule 15 tests a work that always fails begun once per ask (§3.8, §4.1, §5.2; rule 15). Round 4's "answered once as its refusal" is corrected there. |
| **F4** | **Lines after an `end` with no `start` are left to no rule** | `ChatRunner.cs:2854`; `ChildBindings.cs:176-177`; `Driver.cs:3827`, `:3833`; `DriverContinuations.cs:1272-1274`, `:1312-1314`; `ChildLineage.cs:145-152` | **Taken** as named: an `end` with no `start` after it closes its generation and the next line begins the next, a line only ending a run of the closed generation staying in it; each `end` is written once the capture has drained; rule 5 tests a file written before row a with three processes (§1.1, §3.6; rules 4, 5). |
| F5 | A call by an unmatched agent id wakes the read at the call rate | `lineage-hook.mjs:40-44`; `DriverModule.cs:106-111` | **Taken**: a call naming an id the composition holds as a run or among its unmatched ids wakes nothing (§4.1; rule 13). |
| F6 | `running` is this window's processes, not the machine's | `DriverRoutes.cs:642`; `SessionProcesses.cs:183-189`, `:455-459`; `DriverWatch.cs:715-717` | **Taken**: the claim corrected (§1.8, §4.1), and a part another Daoris process here runs closes only by its record's move (§3.6; rule 5). |
| F7 | Rule 5 leaves `declined` and `stood-down` unread | `Web/data/api.ts:413-415` | **Taken**: both read `ended`, a clean end (§3.6; rule 5). |
| F8 | A joined child's end word depends on line order | `AcpStreams.cs:186-204` | **Taken**: the wire's or the stream's word wins wherever a line of the run says one, otherwise the hook's `finished` (§3.6; rule 7). |
| F9 | *Nobody* leaves out the Codex line | `SessionsStateSlice.cs:41`; `Acp.cs:1756-1770` | **Taken**: rule 16 withholds the sentence for a harness seen only by its calls, read from the record's `adapter`, and for a door rule 1 finds shows no child (§2 point 3, §5.2, §5.3's story; rule 16). |
| F10 | Row a's update path is borrowed from §4.2, and its words are not marked | design §4.1, §4.2, §5.4 at `953ae0c48` | **Taken**: §4.1 carries its own three steps, and the hub's `sessions` entry does not name `SESSION_AT_WORK` in row a; row a's keys and its two terms are marked (§4.1, §5.4; rules 19, 20). |
| F11 | The terminal verb's inputs are not said | `Driver.Host/Program.cs` `sessions`; `InstancePipe.cs:1061-1080` | **Taken**: the verb asks the running Daoris; with none running it composes the same answer by the same .NET function under the home's lease, its tests covering both (§4.1; rule 20). |
| F12 | Smaller | as each names | **Taken**, each: (a) `stale` kept through an ask answered from the held answer, and the first landing after a kept failure told even unchanged (§4.1, §5.2; rule 15); (b) a file's length registered up to its last full line (§4.1; rule 12); (c) an attended record in no entry is its own one part (§1.8, §3.8, §4.1; rule 10); (d) only the record fields the answer reads are dependencies (§4.1; rule 10); (e) §1's claims about the file's creation and the lines per call corrected, and `DriverModuleHeldTests` named for rules 14 and 15 (§1.1, §1.8, §3.6; rules 14, 15). |

**What stays owed.** Codex's read of round 4, its credits out (D191's note of 2026-10-24). Row a stays proposed, with these
named changes taken, until it lands; nothing of the design is built and nothing carrying a row of it is staged before then. The
routing half is unchanged by these changes and is carried to its own round. *(Codex's read landed on 2026-10-25 at `eced2688e`,
asking another round for row a; round 5 answers it in §16.)*

## 16. Round 5: Codex's read of round 4 answered

Codex's read of round 4, with §15's named changes, at `eced2688e` (`docs/2026-10-24-cowork-view-reads.md`, *Codex, round 4*): 11
findings, 7 of them blocking; *another round* for row a. Each is checked against the code on main at `9f48860ba`, the commit round
5 is written from. **Whether the cited lines moved**: `src/` is the same at both commits (`git diff eced2688e 9f48860ba -- src/` is
empty), so every line of code the read cites holds where it says, and so do its `Design:` lines, the design being unchanged
between them. Only its `Foundation:` lines moved, since FOUNDATION1's Part B round 5 landed after the read: its `:1013` is now
`:1049` (§5.1's COWORKVIEW1 row), `:1933`, `:1936` and `:1937` are `:2014`, `:2017` and `:2018` (§14.2's rows 9, 12 and 13), and
`:1955` is `:2036` (§14.3's *Downward only*), each saying what the read quotes. Its `groups.ts:152` is the doc comment above the
lines it means, `:148` and `:159`. Round 4's own citations, made at `deb766f20`, moved in four files before the read, and round 5
cites them anew where it changes a passage: `ChatRunner.cs` (the spawn `:1546` to `:1596`, the `end` `:2854` to `:2977`),
`Driver.cs` by fifteen lines at the places cited (the spawn `:3658`, the exit `:3842`, the drain `:3848`, the conclusion `:3891`,
the `end` `:3907`), `Acp.cs` by nine (the streams made at `:1668`) and `GroupsStateSlice.cs` by none.

The read's §0 re-reads round 3's answers and opens no finding of its own: the three gaps it names there (2.2's historical
boundaries, 4.2's input owners, 5.1's further omissions) are its findings 1.1, 2.4 and 4.2 below. *Taken* means the design now says
it, and where; no finding is disputed, each having held in the code it cites.

| # | Finding (blocking in bold) | Checked against | Answer |
|---|---|---|---|
| **1.1** | **Historical files do not hold one `end` per process** | `DriverContinuations.cs:1242`, `:1299` start processes and write no line; `Driver.cs:3891`, then `:3907`, the record's one `end`; a chat's `end` in each run's `finally`, `ChatRunner.cs:2977`, after `:2896` | **Taken.** §1.1's claim corrected: one `end` per process holds for a chat's file alone. A generation a `start` began is *recorded*, one begun without is *history*; a history `end` is its process's only where the record's `kind` is a chat, and in a driven record's history, or a history generation the next `start` closes, a run no harness ended reads `unrecorded`, *its end was not recorded · last seen {time}*, never `ended`, `lost` or the `end`'s moment. `kind` joins the read fields. Rule 5's fixture is now the existing driven case, a first run and a continuation under one `end`, beside a chat's (§1.1, §3.6, §3.8, §5.2 to §5.4; rules 5, 7, 10). |
| 1.2 | The strip does not word the record's raw state | `PartsStrip.tsx:56`; `groups.ts:148`, `:159` | **Taken.** Row a keeps the record's own state, now said as `sessionState.*` (as `AskWork.tsx:111` draws a session) and as deliberately unlike the strip's derived words; rule 17 tests a part the strip shows `awaiting-helper` drawn *completed* (§3.1, §3.8, §5.2, §5.4; rule 17). |
| **2.1** | **Delayed capture lines have no process attribution** | `Driver.cs:3965-3985`, `:4004-4026` (an abandoned capture still observed); `ChildLineage.cs:145-152`, made per capture at `HarnessCapture.cs:123`; `AcpStreams` made per connection, `Acp.cs:1668` | **Taken, the smallest of the read's two ways that keeps evidence.** A token, `proc`, issued before each of the four spawns, on that process's `start` and `end` and on every line the driver writes for it, bound into its writers when they are made, so a capture that outlives its process writes only as that process; the same token on the spawn's environment as `DAORIS_LINEAGE_PROC`, set anew at each spawn and among `Adapters.Roles` (`Adapters.cs:1574`, `:1659-1665`), for the hook and the connector to copy. The fold places a line with `proc` in its own process's generation wherever it sits: a late start begins no generation, and an old stop never ends a reused id's later run. The `start` makes the file, so a first process has its `start` too. A line with no `proc` is placed by position, as all history is; whether the token reaches the hook and the connector on each door, and how many of their lines sit after a boundary they precede, is rule 1's measure. Dropping late lines at the writer is rejected, since it hides children (§3.6, §8, §9; rules 1, 4, 6, 9, 21). |
| **2.2** | **Rule 13's id test wakes on root calls** | `ChildBindings.cs:154-162`, `:217-219`, `:223-227` | **Taken.** The composition registers its part's root ids beside its runs and unmatched ids, and a call the fold reads as the root's, Claude Code's naming no agent and Codex's naming a thread with no parent or the named root, wakes nothing; the one that wakes is a parentless Codex call naming a thread held as a child. Rule 13 proves a hundred root calls cause no wake (§4.1; rule 13). |
| **2.3** | **Immediate route failures are not covered** | `HeldAnswers.cs:96-100`, `:104-105`; `held.ts:30`; `main.tsx:31` | **Taken.** `SESSION_AT_WORK` is a *numbered* read, the only one: its ask never throws, a failure within `Patience` and a held fault answered as the failed value (the fault kept until a landing replaces it), a held answer with a kept failure answered stale; the query sets `retry: false`, and a query in error is drawn *failed*. Round 4's "*Try again* is the only re-ask" is corrected to "no failure asks again". Tests before and after `Patience` (§1.8, §4.1, §5.2; rule 15). |
| **2.4** | **Failed input slices read as pending or current** | `SessionsStateSlice.cs:77`; `GroupsStateSlice.cs:117`; `StateSlices.cs:107`, `:170-180`, `:207-214`, `:248` | **Taken.** Each slice tells its health from memory beside its snapshot, and `HealthMoved` when a judgement's failure begins or ends, even pushing nothing. Health is a dependency: never judged and failing reads *failed*, judged and failing *stale*, each with the slice's words, and the recovery lands as moved; the composer still calls no service. Tests of a first-read failure, a failure over a kept snapshot and the recovery, for both slices (§1.8, §4.1, §5.2; rules 10 to 12, 16). |
| **3.1** | **Late delivery can regress the page** | `HeldAnswers.cs:145-172` (decided under the gate), `:174-177` (told after it); `held.ts:49-51`; `D182.md:24` | **Taken.** Every reply and event of the numbered read carries `{ epoch, n }`, `n` its work's `begun`, which only grows; the page keeps the newest it took for each query and drops one older, another epoch replacing it. Tests of events told in reverse, an old direct reply after a newer event, and an old failure after a recovery (§4.1; rule 15). |
| 3.2 | Part A's placement needs stating; Part B is not required | Foundation §5.1 (`:1049`), §14.2 rows 9, 12, 13 (`:2014`, `:2017`, `:2018`), §14.3 (`:2036`) at `9f48860ba` | **Taken.** §4.3 places each piece of row a on the map: the lineage writer's port in Agents, handed to `AcpStreams` and to the stream watcher in place of `lineageHome`, implemented in Participants, wired by Loop at the spawns; so row a adds no edge up the map and cuts the watcher's. Part B is named as not needed (§3.6, §4.3; rule 23). |
| 3.3 | Hook-only `finished` has no words | `Web/locales/en/work.panel.json:16-20`; `Web/locales/zh/work.panel.json:16-20` | **Taken.** `work.atWork.child.finished`, *finished* / 已结束, in both catalogues; a mapping names the key for every state a child or a part can hold, `unrecorded` among them, and a table test finds each key in both languages, which equal key sets would not; a story of a hook-only finished child beside a completed one (§3.6, §5.2 to §5.4; rule 19). |
| **4.1** | **Rule 14's bound and retention cannot all hold** | design §3.8 (`:832`) and rule 14 (`:1490`) at `eced2688e` | **Taken.** Forty rows filled open runs, then lost, then the newest three of the rest, each child only with its whole chain of ancestors, a chain that does not fit passed over; the rest folded; every child not drawn for want of room counted by its state in `more`, so the heading counts every working child and rows, folds and `more` count every child once. Tests of forty-one working children, a chain deeper than the bound and one that fits (§3.8, §4.1, §5.2 to §5.4; rule 14). |
| 4.2 | Coverage is a plan; named gaps | as cited | **Taken**, each gap by the finding that names it: rules 4 to 6 gain the historical and delayed-capture cases (1.1, 2.1); 10 to 13 the input-health and root-call cases (2.2, 2.4); 15 and 16 the immediate and upstream failures (2.3, 2.4); 11 and 15 reversed delivery (3.1); 14 the overflow (4.1); 17 its wording (1.2). Rule 22 stays the installed run, apart from them. |

**The decisions' notes.** D173's COWORKVIEW1 notes gain one dated 2026-10-25 for round 5's changes to its points 2 and 4. D203's,
D115's and D167's notes speak of the routing half and of the parts' set and order, which round 5 leaves as they were, so none
gains a note; nor do D56's NOBLOCK1 note or D182, whose rules the numbered read and the slices' health extend for this read without
changing what they say.

**What stays owed.** Both makers' reads of round 5, on row a's table (D191): the Opus foundation reader's and Codex's. Row a is
accepted, or not, on what they find; until then nothing of the design is built, filed or staged. The routing half keeps its own
round.
