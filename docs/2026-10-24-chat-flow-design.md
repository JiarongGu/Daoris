# A conversation's work in its own tree runs its repository's workflow: a run keyed by the conversation's line, a gate that refuses the person's Accept with its reason and asks the second opinion at that press, a send-back only between turns, and a no-run answer the page words in both languages (accepted, 2026-10-24; amended by Codex's read, 2026-10-24)

**Standing:** accepted 2026-10-24 by the steward under D191 (D157's CHATFLOW1 acceptance note; amended 2026-10-24 by Codex's owed read of it, D157's amendment note), round 2 with its named changes, a note amending D157 (the workflow) and D155 (the second opinion) for CHATFLOW1. Round 1
was written by Claude Opus 5.5 from the code on main at `8cc3936b0` and read at `94c9065f4` by the independent Opus foundation reader
alone, its read verbatim in `docs/2026-10-24-chat-flow-reads.md`: 24 findings, 8 blocking, another round; Codex's read is owed, its
credits out. Round 2 is written by Claude Opus 5.5 from the code on main at `ca046e7fe`. Since the read's `ffcf42926`, `src/` changed in
tests (GITHUBDOCS1's and CI's), in `Platform/WiringVariables.cs` and in the CLI's `package.json`, and SUPERVISOR1b moved `Driver.cs` and
`SessionLedger.cs` by a few lines; no claim turns on these, and the citations are re-pinned. It answers every finding in §15, each checked
against that code. Round 2 was read at `a8573c142` by the Opus foundation reader alone (11 findings, 2 blocking): accept after named
changes the steward checks without another read. Those changes are taken by Claude Opus 5.5 from the code on main at `507b1925d`, and
§16 says where each went. Codex's owed read of the accepted text, read at `16e0950e2` against main `7cda79f01` after rows a and b were
built (12 findings, 8 blocking), reopened it on the boundaries below, keeping the refusing gate, the local run and the ordinary
send-back direction; D157's note of 2026-10-24 reopens it on those points. Its findings are taken by Claude Opus 5.5 from the code on
main at `5d19f6b3a`, and §17 says where each went; the notes stand again on the amended text (D157's amendment note).

**What is built.** Rows a and b have landed (D157's CHATFLOW1a and CHATFLOW1b notes): the trace reads the six fields
(`Sessions/Trace/Trace.cs:286-291`), `SessionResponse` carries `TookQuest` (`Daoris.Service.Http/ApiContracts.cs:457`), the route answers
the code and its values (`Bridge/Routes/WorkflowRoutes.cs:243`), and the page words the codes it knows (`workflow/WorkflowRunView.tsx:303-309`).
Row a's classifier is corrected by finding 6 (§4.1), in the change that carries this amendment. Rows c to i are not built. §1 is the
baseline read before rows a and b, kept as it was read; the citations the remaining rows build from are re-pinned at `5d19f6b3a` in §17.

The row CHATFLOW1 in `TASKS.md` carries the case. A chat working in a tree of its own serves no quest, so the session's *Workflow*
view shows only the driver's English sentence, in zh too, and its work reaches the review list with none of its repository's
workflow: no checks, no second opinion and no landing gate. The workflow design left *a conversation's work under a workflow* out,
not rejected (`docs/2026-10-09-workflow-design.md` §12.2), and the second-agent design did the same for *opinions on a conversation's
work* (`docs/2026-10-08-second-agent-design.md` §12.2). §2 says what changed since and why it now belongs in.

**What changed in round 2.** The read's finding 23 is taken: a chat's *Accept…* is gated as a quest's is today. The gate reads the
chat's run, refuses the press with its reason where a part holds, and asks the second opinion at that press. The person presses
again once it settles. No press is kept to land later, so `AutoLander`, a deferred merge and a landing on a live tree are gone from
the design (§5). Rows a and b are settled after findings 1, 2, 12, 13 and 21, and can go ahead (§11).

**What changed after round 2's read.** The send-back is built across the service, the driver and the modules, with an atomic take
under the turns' lock (§6), and D155's automatic recheck is kept after the answering turn, which Daoris started (§5.2, §6). A chat run's
files go with the session delete, not the history clear (§4.3). A line whose stored origin is a quest's or an ask's reads that quest's run,
and `tookQuest` is read across the line (§4.1, §4.2). Row d names every quest-keyed site it re-keys, the plan asks nothing, and the person
is told when the opinion settled (§5.2, §11).

**What changed after Codex's read.** One reservation of the turns no longer stands for several boundaries; each has its own. The press
reserves the conversation against another turn, judges every part at one captured tip and lands that tip by the pinned path, refusing a
moved candidate, on every door (§5.2, finding 1). The send-back's reservation is serialized with the keeping of the person's words and
adopts the findings' word in the same act (§6.1, finding 2); the answering turn has a completion boundary of its own, its ending tip and
answers frozen before another turn runs, and the recheck reads that reading (finding 3); the press records the person's turn count, and a
turn of theirs completed since sends the findings to them (finding 4); an ended chat takes the findings by a path of its own, resumed
through its owning runner (finding 5). A record's run is the concrete work its line continues, resolved before its origin, by one
resolver the view and every door share (§4.1, finding 6). A chat's candidate readiness is apart from its displayed work state (§4.2,
finding 7). A graph-bound chat run's subject and lifecycle are said before row f binds one (§4.5, finding 8).

**What this is not.** It is not a quest. A chat stays the person's conversation: it takes no ask, publishes nothing and closes nothing.
Ask Daoris, an intake, a helper, a partner and a second opinion's reviewer are conversations too, and each is left as it is (§9).
A chat in the person's checkout is left as it is too. FABRIC-PIPELINE1d performs the `check` step, which exists only as a graph node
(§4.2). This design adds no executor.

Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, module paths under `src/Daoris.Desktop/Daoris.Desktop.Modules/`,
service paths under `src/Daoris.Service/Daoris.Service.Core/`, page paths under `src/Daoris.Web/src/`.

## 1. The baseline, read from the code before rows a and b

This section is the code as read at `ca046e7fe` and re-pinned at `507b1925d`, before rows a and b were built, and is kept as read. Where
rows a and b changed it, the standing's *What is built* says so: the trace now reads the fields §1.1 says it does not, the service answers
`tookQuest`, and the no-run answer carries a code the page words. The citations rows c to i build from are re-pinned in §17.

### 1.1 How a chat's tree is known

- **Chosen at the start.** `ChatRunner.Isolates` is the conversation's own choice, else the repository's standing one:
  `ownTree ?? config.OpensOwnTree(repository)` (`Conversations/Chat/ChatRunner.cs:363-365`; `Platform/DriverConfig.cs:1423-1424`).
- **Grown at the start.** Where it isolates, `StartAsync` opens a tree (`ChatRunner.cs:557-567`): `SessionTrees.OpenAsync` names it
  `s-<8 hex>` on the branch `daoris/s-<8 hex>` (`Trees/SessionTrees/SessionTrees.cs:354-356`), made by `git worktree add --lock -b`
  under a lock naming the opening process (`:369-372`), and **lets that lock go once the tree is open** (`:404`). No session lock is
  taken for a chat: only a driven start takes one (`Loop/Driver.cs:2447`, `Loop/DriverContinuations.cs:304`). The record opens on that
  tree with its `HEAD` as the base commit (`ChatRunner.cs:575-586`, SURF6). A tree that grew is told its process with the first message
  (`:612`, PROCESS1d-ii), which names no checks; a checkout chat is told nothing (`:608-611`).
- **The session's record.** The service's `OpenChatAsync` creates a `Chat` with no quest (`SessionLedger.cs:652-706`, the create at
  `:694-698`; `Sessions.cs:58-61`, `Quest` *null for a chat*). An unstated tree is recorded as the checkout's root (`SessionLedger.cs:675`).
  So *has a tree* is not *has its own*: the page's `ownTree` compares it with the registration (`work/identity.ts:159-180`), and the
  driver asks `SessionTrees.Holds` (`SessionTrees.cs:471`), which is true for any folder under the trees root, a reviewer's clone
  `o-…` included (`Review/OpinionTree.cs:18-20`; `Trees/SessionTrees/TreeContext.cs:67`).
- **Its line.** The service stores each record's line as `entry`, `<origin>/<repository>` (SESSONE1a): a chat's origin is
  `session:<first>`, the first record its `From` links reach (`Sessions/SessionEntries.cs:71-93`; written at the open,
  `SessionLedger.cs:698`), and the wire carries it (`Daoris.Service.Http/ApiContracts.cs:436-439`). A helper and a reviewer keep an
  origin of their own (`SessionEntries.cs:76-77`). A line's origin can also be a quest's or an ask's (`SessionEntries.cs:84-94`): a closed
  quest's session gone on as a conversation in the quest's own tree (SESSCTX1c part B, `Conversations/Chat/BriefedMove.cs:26-27`) is
  such a line. No chat's `From` link changes tree: a hand-over opens on `prior.Tree` (`ChatHandOver.cs:157`), a limit's move and a
  briefed go-on pass `tree: tree` (`Conversations/Chat/LimitMove.cs:141-143`, `:255-257`; `BriefedMove.cs:128-134`), and *Start a
  conversation with these words* passes no `from` (`ChatRunner.cs:326-330`).
- **What the trace reads.** `Trace.ParseSessions` reads `Kind`, `Tree`, `BaseCommit`, `Ask` and the rest (`Sessions/Trace/Trace.cs:267-286`;
  `Sessions/Domain/TracedRecords.cs:28-80`), and none of `opinion`, `serves`, `piece`, `from` or `entry`, which the service answers this
  machine (`ApiContracts.cs:425-454`). A teammate's record is told by `/` in its id (`TracedRecords.cs:69-70`).
- **A chat that took a quest.** The record keeps `quest` null and the quest in `took_quest` (CHATTAKE1; `Sessions/Sessions.cs:250`), which
  joins that quest to the chat's line (`SessionEntries.cs:102-106`). The field is the taking record's alone: `MarkTookAsync` writes it by
  that record's id (`Sessions.cs:1733-1740`), and `OpenChatAsync` takes none (`SessionLedger.cs:652-706`), so a hand-over's next record
  carries no `took_quest` of its own. `/api/sessions` does not answer it to this machine (`ApiContracts.cs`
  has no such field), and `ServiceClient.SessionQuestAsync` reads only `quest` (`Service/ServiceClient.cs:1981-1992`).
- **Its branch.** The record keeps no branch. It is `daoris/` plus the tree's folder name (`Trees/SessionTrees/TreeFollows.cs:19`,
  `Trees/SessionGroups.cs:291`), or the branch the agent moved its tree to (TREEFOLLOW1, `SessionGroups.cs:254`).
- **A hand-over keeps the tree.** `ChatHandOver` opens the new record on `prior.Tree` (`Conversations/Chat/ChatHandOver.cs:157`), holding
  the old record's turns while it does (`:142`). The new record links to the old one by `From` (`Sessions.cs:814-816`, MIXAGENT1a-iii), and
  is told only the commit trailers, not the process (`ChatRunner.cs:613-619`).

### 1.2 The run asked for a chat

- The page asks `WORKFLOW_RUN` with `{ session }` while the *Workflow* view shows (`work/WorkFrame.tsx:1118-1123`).
- `WorkflowRunReader.ChainsOf` turns a session into its quest or its ask. **An intake reads its ask's runs** (`Workflows/WorkflowRun.Read.cs:86`,
  D157's WORKFLOW1c note, `D157.md:241`). A chat has neither, so it answers *session `<id>` serves no quest, so no workflow runs for it.*
  (`:87`). A run's id is its chain's first quest in the repository (`Workflows/WorkflowRunBindings.cs:52-53`; `Workflows/Domain/WorkflowRunBinding.cs:9`).
- Every fact `RunAsync` reads is selected by `mine`, the sessions whose `Quest` is in the chain (`WorkflowRun.Read.cs:129`): the standing tree
  (`:153-156`), the opinion (`:166-167`), the landings, acceptances and due list (`:185-187`). `Derive` selects them by quest again
  (`Workflows/Domain/WorkflowRun.cs:346-351`).
- The route answers `{ runs, problem }` and no code (`Bridge/Routes/WorkflowRoutes.cs:222-243`, the shape at `:242`). Its comment
  says so: *What names nothing that runs (a chat, a quest not here) is answered as no run with the driver's sentence* (`:217-221`).
- The page shows `problem` as written, and `workflow.run.none` only when no sentence came
  (`workflow/WorkflowRunView.tsx:301-304`). A person reading in 中文 reads the driver's English. The terminal prints the same sentence
  (`Workflows/WorkflowRunCommand.cs:72`), and the run's English is held on both doors by the twin table
  (`Daoris.Desktop.Driver.Tests/fixtures/workflow-run-said.json`, WORKFLOW1c5).
- The no-run sentences today: no session recorded (`WorkflowRun.Read.cs:84`), the chat's (`:87`), no quest here (`:94`), nothing named
  (`:119`), and the service not answering (`:53-56`, `Unanswered`, which the route does not send). An ask with no quest yet gives no
  sentence, and the page's fallback says *no quest of this work is published* (`locales/en/workflow.run.json:148`).

### 1.3 The gate a chat's Accept passes

- **Where it is offered.** A live chat between turns with commits beyond the line is in the review list with `Lands`
  (`Trees/SessionGroups.cs:898-908`, `:1100-1114`; `LandOffer.Of`, `:287-292`). The conversation's foot offers the same once the driver
  says its turn ended (`work/waits/waits.ts:81-96`, ACCEPTFOOT1, D164's note).
- **The press.** `LAND_SESSION_TREE` reads the session's quest, which is null for a chat (`Bridge/Routes/TreesRoutes.cs:532`), and asks
  the gate with it (`:551`). Where a part holds, the press is refused with its reason, said to the log (`:600`). Accept refuses uncommitted
  work (D164 point 4; the plan says so before the press).
- **The gate reads the rules live, and nothing holds.** `TreeGates.ProcessAsync` reads Current for a quest-less tree and never a binding
  (`Trees/SessionTrees/TreeGates.cs:83-88`). D154's look answers `None` for no quest (`Review/Domain/ReviewGate.cs:459`). The opinion's
  part answers `None` (`TreeGates.cs:141-144`; `Review/Domain/OpinionGate.cs:345`), and the graph's part reads only a bound graph run
  (`TreeGates.cs:64-77`). So the repository's look, its second opinion and its run's own gate never hold a chat's work.
- **D154's look lives on quests.** A look is set up by a set-up step, a quest with `SetUpIn` (`ReviewGate.cs:271`), and its verdicts and
  skip are kept on the quest (`:345`) through the person's quest routes `/api/quests/{id}/review` and `/set-up-step`
  (`Daoris.Service.Http.Tests/PersonDoorTableTests.cs:103-104`).
- **No opinion is owed or asked.** An opinion is owed when a driven record concludes `Done` (`Loop/OpinionLook.cs:19-30`). Its due names a
  quest (`Review/Domain/OpinionDue.cs:10-11`) and closes `undone` on it (`:28-29`), and its gate's answers are kept per chain (`TreeGates.cs:185`;
  `Review/OpinionGates.cs:21`). The pass reads the tree's `HEAD` at the look (`Loop/OpinionGateLook.cs:353`). The service starts one
  automatic first pass per working session; the person's ask is never capped (`Review/OpinionDesk.cs:395-406`). The person's own presses
  refuse a chat with the sentence the row quotes (`Review/OpinionPresses.cs:170-173`).
- **No go-ahead can be asked.** A go-ahead lives on an ask, and a chat's session is refused: *is a conversation on no ask*
  (`SessionGoAheads.cs:75-84`). A run raising one is WORKFLOW1l's, not built (`Workflows/Domain/WorkflowCurrent.cs:86-87`).

### 1.4 Checks a session is told

`run-heavy` refuses a tree only where git's lock on it names a session whose kept checks are `light` (`Sessions/Heavy/HeavyRun.cs:180-189`,
through `SessionLocks.HolderOf`). A driven start keeps its checks for its run and lets them go when the run ends (`Driver.cs:2548-2557`,
a `Disposer`), and hands the harness's deny rules in that start's settings (`HeavyTold.Hand` into `HandRules`, `DriverContinuations.cs:418-421`).
A chat's rules are composed once, at its spawn (`ChatRunner.cs:1349-1360`). So no part of that enforcement reaches a chat today, and
`HeavyTold.LightWords` (`Sessions/Heavy/HeavyTold.cs:72-80`) would be false said to one.

### 1.5 Words into a live conversation

- **A chat's words.** `ChatTurns.Take` lets an ordinary word into a turn in flight never. Only an urgent one reaches the running turn at its
  next step, and only on a door that steers. Otherwise the word waits for the turn's end, or is the next turn at once
  (`Conversations/Chat/ChatTurns.cs:574-608`; the reaches `Now`, `NextStep`, `TurnEnd`, `Steered`, `Conversations/Domain/ChatQueue.cs:49-65`).
  A word is `Urgent` unless said (`Conversations/Domain/ChatMessage.cs:27`, D136, D188). This is D137 point 2's door.
- **Between turns.** `ChatRunner.TurnEnded` is true when no turn is taking or opening, nothing waits, and one has ended
  (`ChatRunner.cs:1966-1972`). Where no runner here holds the chat, `SessionGroups.Between` reads `SessionEvents` (`SessionGroups.cs:898`).
  The driver cannot see a draft in the composer. The record stays `working` between turns (D164's ACCEPTFOOT1 note).
- **`Tell`** builds a Daoris word (`DaorisWord`) at the default level, `Urgent`, with no `by` (`ChatRunner.cs:1819-1830`).
- **Another agent's findings never reach a chat.** `OpinionDeliverer.HandAsync` sends a chat's to the person (`Loop/OpinionDeliverer.cs:127-137`,
  `{ Kind: "chat" } => OpinionToPerson.Conversation` at `:134`). A running or parked record waits, *never into a step in flight* (`:140-145`).
  A driven run is handed `opinion_answer` as a rule when its resume spawns (`DriverContinuations.cs:418-420`, `:555`).
- **No send-back runs yet.** `failed: send-back` is a step's field (`Workflows/Domain/WorkflowNamed.cs:58`), and a graph decodes it as a
  `Human` node (`Trees/Nodes/WorkflowGraphs.cs:24`, `:82-95`). The one thing that sends work back today is the opinion's delivery above.

### 1.6 Conversations that are not a repository's chat

Each is `Kind = Chat` with no quest (`Sessions.cs:44-61`):

| Conversation | How it is told apart | Its folder |
|---|---|---|
| Ask Daoris | repository `daoris:help` (`SessionLedger.cs:432`, `IsHelp` `:438`; `TracedRecords.cs:22`), opened by `OpenHelpAsync` (`:582-634`) | its workspace's room, no base commit (`:622-626`) |
| An intake | repository `ask #<id>`, `Ask` set (`SessionLedger.cs:360-424`, `:414`); its view reads its ask's runs (§1.2) | a room |
| A reviewer | `Opinion` set (`Sessions.cs:148`) | a clone of its own under the trees root, `o-<8 hex>` (`Review/OpinionTree.cs:6-25`) |
| A helper | `Serves` set (`Sessions.cs:158`) | its lead's |
| A partner | `Serves` and `Piece` set (`Sessions.cs:165`) | a clone of its own with no remote; its one piece is kept as a commit its lead merges (`D199.md:16-29`) |
| A teammate's record | its id holds `/` (`TracedRecords.cs:69-70`) | another machine |

## 2. What changed, and why it now belongs in

Both designs left a chat out for one reason: *A chat lands nothing by the driver's gate today* (the second-agent design §12.2), and *A
chat lands by the person's press as today* (the workflow design §12.2). Neither weighed a chat's work and found it should go ungated.

That reason no longer describes the product:

1. **A chat in its own tree now commits and lands as a quest's work does.** D164 (2026-10-12) tells a conversation that grew a tree to
   commit on its branch as it goes (PROCESS1d-ii). Its Accept names the branch by the repository's rule, from the work's own commit subject
   (ACCEPTFOOT2), and the conversation's foot and head offer *Accept…* once a turn ends with work beyond the line (ACCEPTFOOT1). The
   same press, `LAND_SESSION_TREE`, lands both.
2. **So the gate is skipped, not absent.** The press runs the same `GateAsync` (§1.3), and each part answers *nothing to wait for* because
   the quest is null. A repository whose workflow requires a look, or whose rule names a second opinion at the landing, has that
   requirement met by a quest's work and silently skipped by a chat's. The work is the same kind, and so are the branch and the press.
3. **The run reads the records a chat already has.** A run is *mostly derived from the records that exist* (D157 point 11). A chat's
   tree, base, branch, commits, landing record and review exist on the same record and the same files as a quest session's (§1.1, §1.3),
   and its line is already stored (§1.1). What a chat lacks is a run key, which §4 derives from that line.
4. **The person asked for it.** The row's direction is that a conversation's work in its own tree runs its repository's workflow.

What stays out is unchanged. A chat in the checkout works on the person's own files, and Daoris lands nothing from it. Ask Daoris works in its
workspace's room, not in a repository's tree, and Daoris lands nothing from it either (§9).

## 3. The rule

1. **A chat in a tree of its own runs its repository's workflow.** It is a run like a quest's: one repository's work, bound once,
   read from its records, drawn in the *Workflow* view. It is gated at the person's *Accept…* by the same `GateAsync` in the same fixed
   order (D155 §7). A chat in the checkout, Ask Daoris, a reviewer, a helper, a partner and a teammate's record run none, and the view
   says which in the person's language (§9, §10). An intake reads its ask's runs as today, and a chat that took a quest reads that
   quest's (§4.2).
2. **The run is keyed by the conversation's line** where no quest is: `chat-<first>`, the line's first record in that tree (§4.1).
3. **It binds when its tree opens, and never to a graph version before CHATFLOW1f.** It is bound to the workflow the workflow design §4.1
   chooses with no task level and no kind: the repository's, then the workspace's, then Current. Where that choice is a graph version,
   nothing is bound and the view says so until CHATFLOW1f (§4.3). A press on a run that could not be bound tries the bind first, and
   one that still cannot be made refuses the press with its reason (§4.3).
4. **The gate refuses the person's press with its reason, and asks the second opinion at that press.** The end of a turn the person
   started starts nothing. The one exception is D155 point 7's recheck after a turn Daoris started to answer findings (§6). Where a part holds, *Accept…* is refused as a quest's work is today; where the opinion's part holds for want of a pass, the press also
   asks one. The person presses again once it settles, and the work lands then, at that press (§5).
5. **A send-back reaches the conversation only between turns.** It is an ordinary word carrying `by`, never into a turn in flight, at a
   next step or by *Send now*. A word the person sent first goes first, and the send-back goes to the person (§6).
6. **A second opinion reads the chat's candidate, not its conversation.** That is from where its branch leaves the line to the tip the
   pass reads, with the conversation's opening beside it. Its findings are handed once to the chat, between turns, and kept with the run's
   gate (§7).
7. **What stays the person's press**: every landing, D154's look or the person's way past it, an answer to a dispute, a stage's start
   where its step says `you`, and every production act and go-ahead (§8).
8. **The conversation is told only what is enforced.** The view shows the run's checks. The chat is told them once Daoris enforces them on
   its tree, which a row of its own builds (§4.4).
9. **No run is answered with a code** the page words in en and zh, and the terminal says each code's English from the same table (§10).

## 4. The run's key, its reading and its binding

### 4.1 The key

- **The concrete work first** (Codex's finding 6). A line's origin groups work; it does not name the work whose workflow governs it. An
  entry can inherit the origin of the session that published a quest, whatever its worker's tree (`Sessions/SessionEntries.cs:108-112`),
  and a quest an ask sent takes the ask's origin, so its worker and that worker gone on as a conversation read `ask:<id>/<repository>`,
  not an intake's entry (`:95-97`). So before any origin is read, **one resolver, `WorkflowRunReader.WorkOf`, names the quest whose
  workflow governs a record**: its own `quest`; else the nearest record its `From` links reach that names a quest or took one (CHATTAKE1);
  else a quest a chat of its line in its tree took (§4.2); else a stored origin `quest:<id>`. An intake, a reviewer, a helper and a
  teammate's record continue no work here. The walk follows the records read, guarded against a cycle. A record it resolves is that
  quest's work: it reads that quest's run in its repository, under the quest's own binding and gate, is one of that run's work records
  (§4.2), and its press is gated by that quest's chain (row i). It never binds the repository's default over the quest's choice, nor asks
  a new opinion beside the quest's gate. Row i's doors, `LAND_SESSION_TREE`, the terminal's land and `OpinionPresses.WorkAsync`, ask the
  same resolver, so the view and every door decide by one answer.
- **`chat-<first>`, from the stored line**, for a record the resolver names no work for. The record's `entry` names its origin,
  `session:<first>` (§1.1). Where the origin is a session whose record names the same tree, the key is `chat-<first>`. The service's walk is
  guarded against cycles, not bounded by a count, and so is this one: it reads one stored field.
- **An ask's origin** with no work resolved reads that ask's runs, as an intake's does: a genuine intake keeps its ask-wide view.
- **`chat-<own id>`** is kept only for a record whose stored origin names no session, quest or ask, which no chat writes today.
- **Its spelling.** A quest's id is 12 hex digits and a session's 8 (`Sessions.cs:799`). The prefix keeps the two apart by their
  spelling, and `chat-1a2b3c4d` passes `WorkflowRunBindings.IsRunId` (`WorkflowRunBindings.cs:41-42`, `[A-Za-z0-9_-]{1,64}`). A teammate's
  id holds `/` and never passes, which keeps a run machine-local, as D47 §4 has it.
- **One run per tree, across its landings.** A hand-over and a limit's go-on open a new record on the same tree in the same line, so they
  read the same key. Its binding, its gate's file (`<home>/opinions/gates/chat-<first>.json`) and its look's file (§5.3) stay one. A tree
  that lands and goes on is the same run: LAND2c's advance lands it again under the same binding (§4.3).
- **A chat whose tree was discarded and that opened another** (a second *New conversation*) is a new run.

### 4.2 What the view reads

`WorkflowRunReader.ChainsOf` reads a session in this order (row a's classification, then row c's run):

| The record | Answered |
|---|---|
| none by that id | `no-session` |
| a teammate's | `teammate` |
| `quest` set | its quest's run, as today |
| a concrete work `WorkOf` resolves (§4.1): a quest a record its `From` links reach names or took, a `tookQuest` on any record of the line in this tree (CHATTAKE1), or a stored origin `quest:<id>` | that quest's run, the record one of its work records: a chat that took a quest, or a quest's worker gone on as a conversation, is that quest's work, never a chat run, before a hand-over and after it, whatever origin its line inherited |
| `ask` set (an intake) | its ask's runs, as today; `ask-no-quest` before any quest is published |
| a stored origin `ask:<id>` with no work resolved | its ask's runs, as an intake's |
| `daoris:help` | `ask-daoris` |
| `opinion` set | `reviewer` |
| `piece` set | `partner` |
| `serves` set | `helper` |
| a chat whose tree is gone, never bound | `chat-tree-gone` |
| a chat whose tree `SessionTrees.Holds` does not hold | `chat-checkout` |
| a chat in a tree of its own | its chat run (row c); `chat-own-tree` until row c is built; `chat-graph-later` where its choice is a graph version (§4.3) |

So the trace reads `opinion`, `serves`, `piece`, `from`, `entry` and `tookQuest` into `TracedSession` (row a), and the service answers
`tookQuest` to this machine as it answers `opinion` (an additive field, `ApiContracts.cs`). Since `took_quest` is the taking record's
alone (§1.1), the reader takes it from any record of the line whose tree is this one, so a hand-over's next record reads the taken quest's
run as the record before it did; carrying it at the hand-over's open would rewrite the service's own record, which this needs no more than
the line's read does.

**A resolved record is one of its quest's work records** (finding 6). Reading a quest's run does not by itself make a chat one of its
records: `mine` was selected by `session.Quest` alone, and the tree, the gate and the landings were derived from it
(`Workflows/WorkflowRun.Read.cs:187`, `:209-246` at `5d19f6b3a`, before row a's correction). So `RunAsync` selects its work records by the same
resolver: a quest's own sessions, and every record `WorkOf` resolves to a quest of the run's part, each read there as that quest's though
its own record keeps `quest` null. The conversation then contributes its worker, its standing tree, the opinion read on that tree, and its
landings and acceptances, as the worker it continues does. This ships with row a's correction; the doors' gate is row i's.

**A chat run** has no chain. Its quests are empty, its `session` is the line's newest record, and its `repository` is the tree's owner.
`RunAsync` reads it as a quest's, **with every fact selected by the line** rather than by `mine`: the records whose key is this run's, the
standing tree, the opinion read under the run's gate file, the landings, the acceptances and the due list by those records, and
`Derive`'s work sessions the same. Its process is `WorkflowProcesses.Read(home, config, repository, workspace, "chat-<first>")`, the
binding where one is kept (`WorkflowProcesses.cs:33-45`), and `TreeGates.ProcessAsync` reads the same for a chat's tree in place of
Current live (`TreeGates.cs:83-88`), so the view and the gate decide by one process (WORKFLOW1f).

**Its work step** is the conversation's, read from the record and the runner's word, or `SessionEvents` where no runner holds the chat
(as `SessionGroups.Between` reads it), so the terminal's view matches the page's:

- `working`, `agent` while a turn runs or words wait;
- `waiting-on-you`, `talk` between turns with nothing beyond the line;
- `waiting-on-you`, `land` between turns, or ended, with commits beyond the line (the review's `offer`, LAND4);
- `done` once a landing's record is kept for the tip the tree stands at, and `waiting-on-you`, `land` again once the tree moves past it;
- `stopped` once the conversation has ended with nothing beyond the line.

**Its later steps** are derived from the gate, the opinion, the landing record and the due list (`WorkflowRun.cs:344-411`), **by the
candidate's readiness, never by the displayed work step** (Codex's finding 7). A quest's derivation reaches the later steps only once the
work step is `done` (`workDone`, `Workflows/Domain/WorkflowRun.cs:354-363`): before it, the opinion (`:583`), the look (`:678`) and the
landing (`:735-737`) read `not-reached`. A chat run's work step stays `waiting-on-you`, `land` until it lands and reads `working` again
while a findings-answering turn runs, so that rule would hide its opinion and its landing exactly while they hold the work. So a chat
run's facts carry **`Candidate`**: true once commits stand beyond the line, between turns or ended, or once the person's press has asked an
opinion on a tip of it (§5.2). `Derive` reads `workDone` as `Candidate` for a chat run, and reads a quest's as today. While an answering
turn runs, the opinion keeps its own state (`with-session`, then `read-again`), never `not-reached`; the work step alone says the turn
runs. Until row d, the opinion's step reads `not-reached` with no words of when, since the gate does not ask it yet. Row d's words say *a
second opinion from `<reviewer>` is asked when you press Accept*.

**Its checks**, in the view: the run's `checks` (`light` or `heavy-at-end`), Current's `sessionChecks` or its version's field
(`Sessions/Heavy/SessionChecks.cs:3-24`). A `check` step exists only under a graph version: a steps version naming one does not read
(`Workflows/Domain/WorkflowNamed.cs:343-348`; D157's FABRIC-CUTOVER1 note, `D157.md:485`). So a chat run's `check` steps are
CHATFLOW1f's, and FABRIC-PIPELINE1d's executor moves them.

### 4.3 The binding

- **When.** A start's run binds after its tree opens and before its take (D157's FABRIC-CUTOVER1 third-round note). A chat has no take, so
  its run binds when `StartAsync` has its record (`ChatRunner.cs:585-592`), beside where PROCESS1d-ii tells it its process. A chat opened
  before this build binds at its first turn's end, read by the same code.
- **What chooses.** `WorkflowRunBindings.Plan` with no ask and no task choice. No kind is set, since a chat has no ask to carry one, so the
  workflow design §4.1's rows from *the repository's default* down choose. No agent sets a chat's workflow (D157 point 10).
- **Never a graph version before CHATFLOW1f.** The node runtime arms every binding to a graph version under `workflows/runs/`
  (`Trees/Nodes/GraphRuns.cs:59-110`), and the gate's graph part judges by it (`TreeGates.cs:64-77`). Until CHATFLOW1f builds what
  §4.5 says a chat's `claimed`, `handed` and `quest.close` subjects and its next candidate are, a choice that gives a graph version binds nothing. The view answers
  `chat-graph-later`, and the gate reads as today: its process is Current's, and its look and opinion parts answer `None` until rows d and
  d2 give a chat run its own. Current and a steps version bind. `Plan` returns a graph version's digest (`WorkflowRunBindings.cs:129-132`),
  so the chat's bind tests the chosen version for a graph before it writes, and the press's bind-first below tests it the same way.
- **A bind that fails** costs the conversation nothing: it goes on, and the next turn's end tries again. The bind reads only local files,
  and only **a home that cannot be written** fails it. A named workflow that cannot be read does not: `Plan` keeps it as a binding with
  its `Problem` (`WorkflowRunBindings.cs:120-132`), and the process holds the work by that problem (`WorkflowProcesses.cs:65-70`), so the
  press is refused with the problem's words as a quest's work under it is. A failed bind is said once in the conversation: *its
  repository's workflow could not be bound, so it cannot be landed yet: `<why>`*. **A press on an unbound chat run tries the bind first**,
  and one that still cannot be made refuses the press with that reason (`bind-failed`), since falling back to Current could skip what the
  repository's chosen workflow asks of the person (`D157.md:362`, `:393-394`). Only the landing waits. **A choice that is a graph version
  is never refused `bind-failed`**: the press's bind-first binds nothing for it, and the press is gated as today (the paragraph above).
- **Said.** Under a named workflow the conversation's first Daoris line says which and why (`WorkflowRunBinding.Why`,
  `WorkflowRunBinding.cs:50-60`), as the look says a start's. Under Current nothing is said, as a start's binding says nothing
  (`WorkflowRunStart.cs:197`).
- **One run spans every landing of its tree.** The binding is not cleared at a landing: the run's steps after the landing (§8) read it,
  and a chat that goes on and lands again lands under the same version.
- **Cleared with the record its key names, by the session delete.** A quest's binding goes with its quest through the history clear
  (`D157.md:417-423`; `Sessions/History/HistoryHome.cs:29-31`). The history clear never removes a chat: its units are quests, asks and
  failed sessions (`Sessions/Domain/HistoryPlan.cs:23-30`), and a workspace's reading only counts the conversations that served no quest
  (`HistoryHome.cs:64-66`, `:86-87`). A chat's record goes by the session delete (`SESSION_DELETE`, `sessions delete`;
  `Sessions/History/SessionDeletion.cs:50-86`), which refuses while its tree stands or a landing names it (`:127-141`) and removes the
  names `SessionHomeFiles` lists (`:75`). So `SessionHomeFiles` gains a chat run's three files, `workflows/runs/chat-<id>.json`,
  `opinions/gates/chat-<id>.json` and `workflows/looks/chat-<id>.json`, and the delete of the record `<id>` takes them; a later record of
  the line deleted first takes none. The leftover rule (`HistoryHome.cs:103-167`), which sweeps `workflows/runs/` but neither
  `opinions/gates/` nor a look's folder today, takes a `chat-<id>` file under each of the three folders where no record `<id>` remains.

### 4.4 What the conversation is told of its checks

The view shows a chat run's `checks` from row c. The conversation is told them only once they are enforced on its tree, and until then it
is told nothing of them, as today (§1.4). Enforcement is CHATFLOW1h: when a chat opens in its own tree, Daoris takes the session lock in
its id (`SessionLocks.HoldAsync`) and lets it go at the conversation's end; keeps its checks for the conversation's life rather than per
run; composes `HeavyTold.Hand`'s deny rules into the chat's rules at its spawn (`ChatRunner.cs:1349-1360`); and tells a hand-over's next
record the checks as the first was told. Only then is `HeavyTold.LightWords` said to it.

**Every execution acquires them, not only the open** (Codex's finding 10). A chat that goes on in its own conversation bypasses
`StartAsync` and calls `RunAsync` directly (`Conversations/Chat/ChatGoOn.cs:576-586`), and `run-heavy` reads both the session lock and the
kept checks (`Sessions/Heavy/HeavyRun.cs:183`). So row h acquires the lock and keeps the checks at every execution of the conversation:
its open, a hand-over's next record, a go-on in its own conversation, a limit's go-on and the driver's come-up restoring a live chat; and
lets them go at each one's end, a start that fails included. **The checks kept are the ones handed.** Current's `sessionChecks` is read
live, so a rule changed during a conversation's life would let the view show checks the running harness was never handed: the kept
checks are those composed into that execution's spawn, and the view shows them, the live rule beside them where they differ, until the
next execution takes the new one.

**The view says so too.** From row c until row h, the view marks a chat run's checks as *not yet enforced for a conversation*, so it never
shows `light` as a limit the tree holds. Row h takes the mark off.

**What the conversation is told of its landing matches the press.** `ProcessToldAsync` plans the landing it tells without a process
(`ChatRunner.cs:2244`), while the press lands by the gate's process (`TreesRoutes.cs:561-566`). From row c the told landing is planned with
the chat run's bound process, so a chat under a steps version is told the landing its press makes.

### 4.5 A graph-bound chat run's subject and lifecycle (CHATFLOW1f)

Codex's finding 8. Today `claimed` and `quest.close` observe a quest (`RunSubject.Quest`, `Trees/Nodes/GraphRuns.cs:226-230`), the
runtime picks a run's branch through its chain's newest quest (`Trees/Nodes/NodeRuntime.cs:487-501`), and an ended run is judged again
only once a different quest comes back to it (`:492-493`). A chat has none of those facts. So binding a graph version stays disabled
(§4.3, `chat-graph-later`) until row f builds this contract, and row f builds no more than it:

- **The subject is the candidate.** A chat run's unit is `run:chat-<first>`, and its candidate is a `RunSubject.Candidate`: the press's
  captured tip on the line's branch (§5.2), numbered from 1 per run. `claimed` observes the candidate's submission, `handed` its landing,
  and `opinion`, `look` and `check` read the branch at the candidate's tip, as they read a quest's branch.
- **Submitted at the person's press.** A press that captures a tip beyond the run's last landed candidate submits candidate *n* + 1 and
  reaches `claimed` on it; the same press then reads the gate as §5.2 says. A turn's end submits nothing, so an ordinary turn starts no
  step (§3 rule 4). A press on the tip already submitted submits nothing again.
- **`quest.close` is refused for a chat run.** A chat closes nothing (§2's *What this is not*), so a graph whose path from `claimed` to
  `handed` holds a `quest.close` node is refused at the bind with its reason, and the run reads `chat-graph-later` for it, with that reason
  as its words. A graph whose `quest.close` lies only after `handed` is refused the same way: no chat subject answers it.
- **A later candidate reopens the same run.** Once `handed` lands candidate *n*, the run ends as a quest's does, and the next press that
  submits candidate *n* + 1 judges it again, in place of a different quest coming back (`NodeRuntime.cs:492-493`). Nothing reopens it at a
  turn's end.
- **Its send-back.** A failed `check` sends back once per candidate (D157 point 9), by §6's path, and what still stands after it goes to
  the person. A hand-over keeps the run and the candidate, as §4.1 keeps the key.

Row f's proof covers two successive landings of one run, a hand-over between submission and landing, a `quest.close` graph refused at
the bind, and a failed check's single send-back on a candidate.

## 5. The person's Accept, gated

### 5.1 The reading taken, and the one rejected

Round 1 started the steps at the person's press and kept that press on the due list to land later. The read showed that the list cannot
carry it: `AutoLander` closes an entry `off` without `autoAccept`, `undone` without a quest, and `superseded` after a hand-over, tries
nothing while a live record holds the tree, and requires a quest (`Trees/Landing/AutoLander.cs:184-191`, `:211-228`;
`Trees/Landing/AutoLanding.cs:154`). A kept press would also merge at a moment the person is not pressing, which D145 point 1 keeps from
happening by itself (`AutoLanding.cs:183-187`, `:205-206`), and would race a turn writing in the tree.

So a chat's press is gated as a quest's is today. Steps never start at a turn's end, for the reasons round 1 gave: a turn's tip is a draft
while the person keeps talking, a pass per turn spends another maker's account and the one recheck (D155 points 1 and 7), and a `check`
per turn queues hours of gates behind each reply. The person's *Accept…* is the only moment the work is declared finished, and that press
is where the gate reads it.

### 5.2 What the press does

1. **Reserves the conversation, captures one tip, and judges every part at it** (Codex's finding 1). Offering the press between turns
   reserves nothing: the route reads the gate and later lands without holding the conversation's turns (`Bridge/Routes/TreesRoutes.cs:551`,
   `:596`), a clean, newly committed turn passes the dirty check, the landing hold serializes landings and not turns
   (`Trees/SessionTrees/TreeLands.cs:420-438`), and an unpinned gate lands the tree as it stands (`Review/Domain/OpinionGate.cs:675-679`,
   pinned today for a graph landing alone, `Trees/SessionTrees/TreeGates.cs:64-76`). So the press first **reserves the chat against
   another turn**, under `ChatTurns`' lock as §6.1's reservation does, and refuses with *a turn is running; press Accept once it ends*
   where one is taking, opening or waiting. Reserved, it captures the tree's `HEAD` once, reads the chat run's process (§4.2) and every
   part of `GateAsync` in its fixed order **at that tip**, each keyed by the run where a quest's are keyed by the chain, and sets
   `LandingGate.Tip` to it, so the landing goes by the existing pinned path, which lands exactly that commit or refuses where the tree moved
   past it. A moved candidate is refused with *the work moved while it was judged; press Accept again*, never landed under the judgement
   of an earlier tip. A word the person sends while the press holds the reservation waits behind it, as behind a turn, and the reservation
   is let go when the press answers, landed or refused. Where every part lets go, it lands at once, as today, and the landing record says
   what each part said.
   - **On every door, by one driver operation** (with finding 11). The reservation, the capture, the judgement, the ask and the pinned
     landing are one driver operation, which the bridge's `LAND_SESSION_TREE`, the connected terminal and the standalone terminal
     (`Daoris.Desktop.Driver.Host/TreesConsole.cs:390`, `:417`, which today goes from the gate's read straight to the landing) each call.
     Where another process holds the conversation, the operation asks that process's runner for the reservation through the pipe, and a
     door that cannot reach the holder refuses with *the conversation is held by another window; press Accept there*. The plans of all
     three doors stay free of writes (point 5).
2. **Refuses with its reason where a part holds**, in the press's existing refusal (`LandingGateWords`, `TreesRoutes.cs:600`), on both doors.
   No press is kept and nothing lands later.
3. **Asks the opinion at that press** where the opinion's part holds as `not-asked`: an `OpinionDue` naming the chat run (§7), as the
   person's *Ask now* writes one (`OpinionPresses.cs:118`). A chat run's due is written at the `landing` occasion, never at `rule.On[0]`
   as *Ask now* writes a quest's, since under a rule whose first occasion is `steps` it would close `past` at once (`OpinionLook.cs:80-84`);
   *Ask now*'s due on a chat run is written at `landing` too. The refusal says so: *a second opinion is asked: `<reviewer>` reads it from
   the driver's next look; press Accept again once it settles.* The look starts it as every owed pass. The due carries the press's
   captured tip and **the person's turn count at the press** (§6.1, finding 4), so the pass reads that candidate and its findings are
   delivered only to the turns they were asked on.
4. **Asks nothing twice.** A press while a pass is `reading`, its findings are `with-session`, or its recheck reads (`read-again`) is
   refused with that state's words and asks nothing. The recheck is not the press's: D155 point 7's runs at the look after the answering
   turn ends (§6).
5. **Only the press asks.** The plan reads the gate and asks nothing: `LANDING` (`TreesRoutes.cs:561`), which the page's review reads, and
   `trees land --plan` show the opinion's part as it stands and write no due. Only `LAND_SESSION_TREE`'s press writes one.
6. **The person is told when it settled.** The refusal asks them to press again once the opinion settles, so a chat run's settling is
   said where they look and where they do not: the conversation's foot offers *Accept…* again with the opinion's word, the review row's
   opinion part moves, and a notice (`Platform/Notices.cs`) says the second opinion on the conversation's work has settled and names the
   conversation.
7. **Lands at the press that finds every part settled**: the person's own press, in time, under the rule's form. A merge-form rule merges at
   that press, as it does for a quest's work. Uncommitted work refuses the press, as D164 point 4 has it. A landing never meets a turn
   writing in the tree because the press holds the turns' reservation from the capture to the landing and lands the captured tip by the
   pinned path (point 1), not because the press is offered between turns or the tree is clean.
8. **After a hand-over** the run, its gate and its due are the line's (§4.1), so the next record's press reads the same gate, and nothing
   is closed `superseded`.
9. **A later landing of the same tree is rarely read by another agent.** The service starts one automatic first pass per working session
   (`OpinionDesk.cs:395-406`), and a recheck is asked only where findings were handed (`:417-418`), reading the answering turn's frozen
   reading (§6.1 (c)). So a later
   landing is read by an agent only where the first pass's findings were handed and the one recheck is still unspent; since the recheck
   runs after the answering turn (§6), that is seldom. Otherwise the gate shows `commits-since`, which the person's press over it answers
   (the second-agent design §8.3) or their *Ask now* reads, never capped. For a long conversation, only its first landing gets an automatic
   opinion. Giving a chat run one automatic first pass per landing would change the service's bound for it; that is the owner's call, and
   this design does not make it (§12).

### 5.3 D154's look on a chat run

A look is set up by a set-up step, a quest the work's done publishes (§1.3). A chat publishes nothing, so no look can be set up for its work
in this build. Where the run's process requires one (the rule's level, or its named workflow's look step, `ReviewGate.Decide`):

- **Its part reads `cannot-start`**, with its limit's words: *a conversation publishes no set-up step, so no look can be set up for its work.*
  The press is refused with that reason.
- **The person's way past it** is *Accept without the look…*, a press of their own with their words, kept at the tip it was pressed at on the
  run's look file, `<home>/workflows/looks/chat-<first>.json` (machine-local, D47 §4, as the opinion gate keeps the person's words). It is
  D154 row 1's skip, re-homed for a run that has no quest. It covers that tip and the commits before it; a later commit asks it again, and the
  landing record says it and its words. **The tip skipped is the tip landed** (finding 1): the press is §5.2 point 1's operation, so the
  skip is kept at the captured tip, the landing is pinned to it, and a tree that moved past it is refused, never landed under the skip. No agent and no Ask Daoris card reaches it (D110), as the opinion's *Go on anyway…* is not reached.
- **Its doors**: the review's look part on the page, and `daoris-driver trees land <id> --without-look "<words>"` on the terminal (D50),
  both through one driver press, a `Person` route in the pipe's census.
- A look for a conversation's work set up in an environment is not designed here; it would need a set-up with no quest (§12).

This ships as row d2, beside row d's gate (§11). Until it lands, a look-requiring rule leaves a chat's press as today, since the look's part
still answers `None` for it (§4.3).

### 5.4 A go-ahead on a chat run

A chat has no ask, and a session's go-ahead is refused for one (`SessionGoAheads.cs:75-84`). A run raising one is WORKFLOW1l's, not built
(`WorkflowCurrent.cs:86-87`). Until it is, a chat run's `go-ahead` step reads `cannot-start` with that limit, as any step that cannot start
reads (`WorkflowRun.cs:396-401`), and once WORKFLOW1l gives a run its own go-ahead, a chat run's is that.

## 6. A send-back into a live conversation

A send-back is a step's return of the work to its agent: a second opinion's findings (D155 point 7) or, under a graph version, a failed
check's one send-back (D157 point 9, FABRIC-PIPELINE1f, CHATFLOW1f's). For a chat:

- **Only after the person's press.** A pass is asked only by an *Accept…* (§5.2), so a send-back follows the person's own word that the work
  was done.
- **An ordinary word of its own, between turns.** It is not `Tell` as it stands, which is urgent and Daoris's (§1.5). It is a word at
  `Ordinary` level carrying `by`, shown as *Another agent's findings*, never as the person's or Daoris's, with `by` as D155 point 7 has it.
  It never takes `NextStep` or `Steered` (`ChatQueue.cs:49-65`), so it never enters a turn in flight. D137's door holds.
- **The person's words go first, where they were sent first.** If the person has sent a word since the press that asked the opinion,
  whether it waits, is being kept, or has already been answered by a turn that ended, theirs come first and the send-back goes to the
  person, kept with the reason `person-went-on` (§6.1 (b), findings 2 and 4). The driver cannot see a draft: where the person is
  typing when the send-back is taken, its turn starts, and their word then reaches it as any of their words reach a running turn, at its
  end, or at its next step where they send it sooner (D136). That is accepted and said in the design, not hidden.
- **Its answer tool.** A chat run whose process names an opinion carries `opinion_answer` in its rules from its spawn, since a live chat's
  rules are fixed then (`ChatRunner.cs:1349-1360`), as `DriverContinuations.AnswersFindings` hands it to a driven run at its resume.

### 6.1 How it is delivered: the service, the driver and the modules

Today none of the delivery reaches a live chat. The service's hand refuses every `Active` record, a live chat's `working` included
(`CannotTake`, `Review/OpinionDesk.cs:480-492`; `Sessions.cs:321-322`), hands only to `opinion.Working` (`:248-253`), and lets only the
record handed to answer (`:285-294`). The deliverer reads a `working` record as answering for ever (`Running`, `Quests/Domain/Planner.cs:361`;
`Loop/OpinionDeliverer.cs:105-114`), and reads answers only at a driven resume's end (`:252-271`, called once, from
`DriverContinuations.cs:658`) or once the record stops running. The loop reaches the shell's chat runner only through seams the modules
wire (`Loop/LoopContext.cs:57-71`; `Modules/Loop/DriverLoop.cs:890-895`), and the headless host wires none. And reading `TurnEnded`
(`ChatRunner.cs:1969-1972`) outside `ChatTurns`' lock and then calling `Take` is a race: `Take` queues an ordinary word as `TurnEnd` once a
turn pumps (`ChatTurns.cs:594-608`), so a word the person sends between the two would put the send-back behind it, which §12 rejects.
Codex's read found that one reservation cannot carry every boundary this needs (findings 2 to 5): `ChatTurns.KeepAsync` serializes the
keeping of a word with `_keeping`, tests `_gate`, and awaits the service's `door.KeepAsync` before the word reaches `Take`
(`Conversations/Chat/ChatTurns.cs:159-172`, `:197`), so the queue can look idle while the person's earlier word is being kept; `TakeHeld`'s
task is the whole pump's drain (`:552-567`, `:860-870`), not one turn's end; the state keeps a last turn's end (`:987-990`) and the
opinion's request no count of the person's turns (`Review/Domain/OpinionGate.cs:96`, `:129`), so a turn the person finished since the press
is invisible at the take; and an ended record's go-on requires words waiting (`Conversations/Chat/ChatGoOn.cs:440-443`) and consumes them
before `RunAsync` (`:558-586`). So each boundary is its own. Row e is built in three lanes, and six things are named:

- **(a) The service's hand admits a chat run's live record** (service). `HandAsync` takes the record to hand to, and admits a `working`
  chat (no quest, no ask, no opinion, no `serves`) whose line and tree are those of `opinion.Working`; it records that record as
  `Handed.Session`, so `AnswerAsync`'s own rule, only the record handed to answers (`:285-294`), lets it answer. Every other record is
  refused as today. The driver names the line's newest record in this tree as the target, never the record that pressed where a hand-over
  ended it.
- **(b) An atomic take, through a seam, in the dispatcher's keep** (driver, modules; findings 2 and 4). `LoopContext` gains `SendsBack`,
  wired by `DriverLoop` to the shell's runner as `HandsOver` is. **The reservation is one operation of the dispatcher's keep:** it takes
  `_keeping` first, so a word of the person's whose keeping is in flight finishes being kept and reaches `Take` before the reservation tests
  anything, and only then tests, under `ChatTurns`' lock, that nothing is taking, opening, held or waiting and a turn has ended
  (`TurnEnded`'s test read inside the lock). **The person's turn count** is the number of the person's words kept on the line's records in
  this tree; the press that asked the opinion records it on the due (§5.2 point 3), and the reservation compares it atomically, under the
  same two locks. The outcomes are distinct:
  - **the person moved past it**: the count has risen since the press, whether their word waits, was being kept, or was already answered
    by a turn that has ended. The findings go to the person as `person-went-on`, nothing is handed, and nothing waits;
  - **activity that is not the person's**: a turn running, opening or held that the count does not show as theirs (a Daoris word's, a
    hand-over's), or a record a hand-over is still opening. The findings wait as `waiting`, and the next look tries again;
  - **reserved**: the turns pump, so a word the person sends from then on waits behind the send-back's turn as behind any turn. The
    dispatcher then asks the service's hand (a) and **adopts the word the hand kept as the reserved turn's word, inside the same operation**,
    so no other word can take that turn; where the hand is refused, the reservation is let go and a word that arrived meanwhile starts at
    once.

  Where the chat lives in another process, the seam asks that process's runner. Where no runner holds a live chat, or the host is
  headless (`SendsBack` null), the findings wait as `waiting`, as a hand-over's box waits for a runner. An ended chat is (f)'s.
- **(c) The answering turn has a boundary of its own** (finding 3). The reservation returns that turn's own completion, not the pump's
  drain: when the send-back's turn ends, and before the pump takes another queued word, the runner captures the tree's `HEAD` as the turn's
  **ending tip** and the answers the record kept, as one **frozen reading** (`OpinionReading`), and completes the turn's task with it. The
  seam's caller keeps that reading, as `OpinionsAnsweredAsync` keeps a resume's, and the recheck reads it (§6.2), never the tree's `HEAD`
  at a later look (`Review/OpinionAnswers.cs:87-92`; `Loop/OpinionDeliverer.cs:216-222`). A person's word queued behind the turn runs after
  the capture, so its commits are never read as answers.
- **(d) `DeliverAsync` reads a chat run's record by the runner's turn state**, not by `Running`: `with-session` while the send-back's turn
  runs, then its answers read. A chat no runner holds reads by its record as today.
- **(e) Findings handed before a hand-over.** A hand-over ends the record they were handed to, and that record never takes another turn
  (`WordsNever.Continued`, `BriefedMove.cs:78-83`). Where its answers are not kept, the service re-hands the same opinion, once, to the
  line's newest record in the same tree (a), the delivery moving `Handed.Session` to it, and from then only that record answers. Where the
  newest record cannot take them, they go to the person as today.
- **(f) An ended chat takes them by a path of its own** (finding 5). Waiting for a runner never ends for a conversation that has ended,
  since nothing starts it, and its go-on needs words waiting that the hand would only keep after a reservation. So where the line's newest
  record has ended and is resumable, the deliverer asks the owning runner whether it can go on (its tree stands, the repository is
  registered, the record is a chat of this machine's); where it can, the service's hand keeps the findings word on that ended record, with
  its `by`, and the deliverer requests the record's existing go-on (`ChatGoOn`) through the owning runner, which consumes it as *another
  agent's answer* (`ChatGoOn.cs:583`) in its own conversation, with (c)'s completion boundary. A headless host leaves the findings
  `waiting`, an explicit outcome the next look retries once a runner is wired. A runner's definitive refusal to resume sends the findings to
  the person, never a new session.

### 6.2 The answering turn and the recheck

- **Its commits void nothing.** No press is kept (§5), so the answering turn's commits move the tip and nothing else.
- **D155's automatic recheck is kept.** At the look after the answering turn ends, `AnsweredAsync` asks the one recheck where it is due
  (`OpinionDeliverer.cs:174-184`), as for a quest's work. This is not a step started at the end of the person's turn, which rule 4 forbids:
  the answering turn is one Daoris started, and the recheck is bounded to one (`OpinionDesk.cs:417-418`). **It reads the answering turn's
  frozen reading** (§6.1 (c)): the commits from the first opinion's tip to the turn's ending tip, captured before another turn ran.
  Ancestry alone does not say who made a commit (`Review/OpinionAnswers.cs:127-145` checks only that a fix lies between the two tips); the
  boundary is what bounds it, since no other turn ran in the tree between the take and the capture. A commit made in the tree outside any
  turn, by the person's own git, is not told apart, and the design says so (§14). **Who rechecks is independent of the answer too**
  (finding 9): the writers the recheck's reviewer is chosen outside are seeded with `first.Working` and follow that record's ancestry
  (`Loop/OpinionDeliverer.cs:211-214`; `Review/Domain/OpinionWriters.cs:23-39`), which misses a successor that answered after a re-hand
  (§6.1 (e)). So the recheck's choice also walks the line of the record that answered, while the ask keeps the first opinion's identity
  the service requires; where that successor is of the first reviewer's family, that reviewer is no longer independent of the fixes, and
  the recheck goes to another of the rule's reviewers, or to the person where none is left. While it reads, the gate is `read-again` (`Review/Domain/OpinionGate.cs:428-438`) and the press is refused with
  those words; once it is given, what still stands goes to the person, and the person's next press lands or answers it. No gate state
  waits for a press to ask a recheck, so the gate never sits in `read-again` with nothing reading.
- **Ancestry decides what an opinion covers** (`OpinionGate.JudgeAsync`, through `ReviewGit.HoldsAsync`), so a go-on that rewrites the
  commits (`ChatGoOn.cs:535-541`, LANDSYNC2b) leaves the earlier pass covering nothing that would land.
- **Once.** A step sends back at most once (D157 point 9). What still stands after the recheck goes to the person.
- **Never a new session.** A chat that has ended takes a send-back by D137 point 3, its own conversation resumed through §6.1 (f)'s path,
  never a new one. A chat whose runner definitively cannot resume it sends it to the person, as `OpinionDeliverer` does today.

`OpinionDeliverer.HandAsync`'s `{ Kind: "chat" }` row (`:134`) narrows to a chat with no chat run: one in the checkout, or one whose tree
is gone.

## 7. The second opinion on a chat's work

- **When.** It is owed where the run's process names an opinion at the landing (`on` includes `landing`, D155 point 2), and asked at the
  person's press (§5.2). The `steps` occasion has no meaning for a chat, which has no chain. The person's own *Ask now*
  (`OpinionPresses.AskAsync`) is offered on a chat run at any time, as on a quest's: `WorkAsync` reads a chat run instead of refusing it
  (`OpinionPresses.cs:170-173`), and names its gate.
- **Its due.** `OpinionDue` gains `Run`, and `Quest` may be null where `Run` names a chat run. Such a due closes `landed`, `gone` or `off`,
  never `undone`. **It carries the press's tip and the person's turn count** (Codex's finding 4). `HEAD` at the look is not the press's
  tip: a whole turn of the person's can finish before the look, and `AskPassAsync` reads the tree's `HEAD` with no press tip or turn count
  (`Loop/OpinionGateLook.cs:353-359`). So for a due naming a run, the pass reads the candidate at the due's tip, in its own clone as every
  pass is, and its findings are delivered by §6.1 (b), which compares the turn count; a later landing's gate judges what would land against
  the tip the pass read and says it.
- **The sites still keyed by the quest**, each re-keyed by the run in row d:
  - `OpinionPresses.PressedAsync` returns the gate unanswered for a null quest (`Review/OpinionPresses.cs:64`), and the route calls it with
    `questId` (`TreesRoutes.cs:586`). Keyed by the run, a chat run's press answers a dispute and `commits-since` as a quest's does; until
    row e every chat's findings reach the person as disputes (`OpinionGate.cs:423-427`), so without it only *Go on anyway…* would land them.
  - `OpinionGateLook.JudgeAsync` reads the chain, the process (`RunOf(chain) ?? due.Quest`) and the gate by `due.Quest`
    (`Loop/OpinionGateLook.cs:185-206`); for a due naming a run it reads the run's process and gate and no chain.
  - `AskPassAsync` takes its words from the chain's ask (`:381`); for a chat run it takes the conversation's opening, and its tip from
    the due rather than from the tree's `HEAD` (`:353`).
  - `OpinionPresses.AskAsync` writes its due at `rule.On[0]` (`OpinionPresses.cs:118`); a chat run's is written at `landing` (§5.2 point 3).
- **What it reads.** The packet is the second-agent design's §4, with three parts read from a chat's record:
  - the **candidate** is from the fork point with the line (`ForkPointAsync`) to the tip the pass reads, read in its own clone as every
    pass is (`Review/OpinionPackets.cs:87-128`);
  - **the conversation's opening**, the person's own first words, which D164's ACCEPTFOOT2 note already takes as a conversation's identity.
    It is named as the opening, not as what was asked, since by a second landing it may no longer describe the candidate;
  - **what the working session claims** is its commits' messages. A chat has no closing note, and nothing stands in for one.
- **Not the conversation.** D155 point 6 stands: the reviewer never reads the worker's conversation, so it does not anchor on its reasoning.
  The person's later words in the chat are not handed either, since they would be the conversation by another name.
- **Who reads.** The rule's `reviewers`, walked outside the working families (D155 point 4): every family that wrote commits in the
  candidate, which a hand-over's and a limit's go-on's makers join, and every partner whose piece the candidate holds (D155's COAGENT1
  note, `D155.md:488-492`).
- **Where its findings go.**
  - The opinion is kept as every one is, on the local host (`/api/opinions`) and in `<home>/opinions/<id>/`.
  - The gate's answers go to `<home>/opinions/gates/chat-<first>.json`, the run's key in place of a chain's (`TreeGates.cs:185`).
  - They are shown in the Review's opinion part and the run's opinion step, and handed once to the chat between turns (§6).
  - Disputes are the person's: the press that comes next answers them (D155 point 8).

## 8. What stays the person's press

- **The landing.** A chat's work lands only by the person's *Accept…*, at that press (§5). *Accept automatically* says nothing for a chat,
  as PROCESS1d-ii already has it, and a landing step's `autoAccept` makes no chat's work land by itself.
- **D154's look**, *Reviewed* where one can be set up, and, for a chat run, *Accept without the look…* (§5.3). The Accept itself is not a
  review (D154 point 8: the verdict is the person's alone, and its own press).
- **A dispute, and what stands after the recheck**: each waits for the person's next press.
- **After the landing**, the run's pull request and stages go as a quest's run's do. A stage whose `start` is `you` waits for the
  person's press (`WorkflowNamed.cs:94-99`), and **production is only ever the person's press** (D157 point 9, D37).
- **A go-ahead.** None can be raised on a chat run until WORKFLOW1l (§5.4). No agent, Ask Daoris included, answers one
  (`Help/Room/HelpRoomDoors.cs:377-380`).

## 9. Unchanged: Ask Daoris's conversations, intakes, and chats with no tree

- **Ask Daoris** works in its workspace's room with no base commit, not in a repository's tree. D183 point 1 gives it a full-access mode
  whose shell and file edits are its own acts, held back only by the room's words (point 5); a repository's work is asked for through a
  quest, which runs that repository's workflow. So Ask Daoris runs no workflow, binds nothing and is gated by nothing new. Its *Workflow* view says why, with a code (§10).
- **An intake** reads its ask's runs, as WORKFLOW1c built it (§1.2), and is untouched.
- **A chat in the person's checkout** runs no workflow: its changes are the person's own files, the checkout is not a tree Daoris made, and
  Daoris lands nothing from it, as today. Its view says so in both languages and names how to have a run: a conversation with a tree of
  its own.
- **A reviewer and a helper** read for another session and change nothing of their own: no run.
- **A partner** writes one piece its lead lands: its lead's run is the run.
- **A teammate's chat** ran on another machine, which keeps its run: no run here.

## 10. The no-run answer and its code

`WorkflowRunRead` gains `Code`, and `WORKFLOW_RUN` answers `{ runs, problem, code }` (`WorkflowRoutes.cs:242`). The page words a code
it knows and shows the driver's sentence for one it does not, as `NodeWord.tsx:8-16` does for a node's refusal (D182).
`workflow.run.none` stays for an answer with no code. **The terminal says each code's English sentence, the same words as the page's en**,
held on both doors by the run's twin table (`fixtures/workflow-run-said.json`, D50), so a checkout chat no longer reads *serves no quest*
there while the page says otherwise.

| Code | When | en | zh |
|---|---|---|---|
| `no-session` | no record by that id (`:84`) | No session `{{id}}` is recorded here. | 这里没有记录会话 `{{id}}`。 |
| `no-quest` | no quest by that id (`:94`) | No quest `#{{id}}` is here. | 这里没有委托 `#{{id}}`。 |
| `ask-no-quest` | an ask whose chains are empty (`:101-116`), an intake's included | Nothing runs here yet: ask `#{{id}}` has published no quest. | 这里还没有进展：需求 `#{{id}}` 还没有发布委托。 |
| `chat-checkout` | a chat whose tree `SessionTrees.Holds` does not hold | This chat works in your checkout, not in a tree of its own, so its work runs no workflow: what it changes is yours to commit. To give work a workflow, start a conversation with a tree of its own. | 这个聊天在你的检出中工作，没有自己的工作树，所以它的工作不走工作流：它改动的内容由你自己提交。想让工作走工作流，请开始一个有自己工作树的对话。 |
| `chat-own-tree` | a chat in a tree of its own, before row c | This chat works in a tree of its own, but this build reads no workflow for a conversation's work yet. | 这个聊天在自己的工作树中工作，但这个版本还不为对话的工作读取工作流。 |
| `chat-graph-later` | a chat in a tree of its own whose repository's workflow is a graph version, before CHATFLOW1f | This chat's repository follows the workflow `{{workflow}}`, which this build does not run for a conversation's work yet, so its work is landed as before. | 这个聊天所在仓库遵循工作流 `{{workflow}}`，这个版本还不为对话的工作运行它，所以它的工作仍按以前的方式落地。 |
| `chat-tree-gone` | a chat whose tree is gone, never bound | This chat's tree is gone, and its work was never bound to a workflow, so there is no run to show. | 这个聊天的工作树已经不在了，它的工作也从未绑定工作流，所以没有进展可显示。 |
| `ask-daoris` | Ask Daoris's record (`daoris:help`) | Ask Daoris works in its workspace's room, not in a repository's tree, and a repository's work is asked for through a quest, so no workflow runs for its conversations. | 问道衍在工作区的房间里工作，不在任何仓库的工作树中；仓库的工作要通过委托来请求，所以它的对话不走工作流。 |
| `reviewer` | a reviewer's record | This session reads another's work for a second opinion and changes nothing of its own, so no workflow runs for it. | 这个会话为第二意见阅读另一个会话的工作，自己不做任何改动，所以没有工作流为它运行。 |
| `helper` | a helper's record | This session helps another and changes nothing of its own, so no workflow runs for it: the session it helps has the run. | 这个会话协助另一个会话，自己不做任何改动，所以没有工作流为它运行：进展在它协助的会话那里。 |
| `partner` | a partner's record | This session writes one piece that the session that asked it lands, so that session's run is the run. | 这个会话写一个分项，由请它来的会话落地，所以那个会话的进展就是这项工作的进展。 |
| `teammate` | a teammate's record | This session ran on a teammate's machine, which keeps its run. | 这个会话在队友的机器上运行，它的进展保存在那里。 |
| `unanswered` | the service did not answer (`:53-56`) | The service did not answer, so where this work stands could not be read. Try again. | 服务没有回应，读不到这项工作的进展。请重试。 |

- The words use the glossary's: 委托 for quest, 需求 for ask, 重试 for try again, 聊天 for chat, 工作树 for tree, 检出 for checkout,
  工作流 for workflow, 进展 for run, 问道衍 for Ask Daoris, 第二意见 for second opinion, 分项 for piece, 落地 for landing
  (`locales/glossary.json`). The glossary has no term for a partner's lead, so the words name it as *the session that asked it*, as
  the partner's notes do (`locales/en/note.json:246`).
- `unanswered` draws a *Try again* (重试) press that reads again. The page already says *Try again* (重试) for a read that failed
  (`help.history.retry`, `signin.retry`), and *Ask again* (再征询一次) is the opinion's press for asking a reviewer again
  (`opinion.act.askAgain`), which a read is not. The view already reads again at every tick (`WorkFrame.tsx:1118-1123`), and the press
  saves the person the wait.
- An intake has no code of its own: it reads its ask's runs.
- Bounds: `{{id}}` is the record's own id, drawn in code as `Inline` draws it; `{{workflow}}` is the workflow's id, drawn the same. No path
  is ever named, since the page is told none (D48 §7, `work/identity.ts:170-172`).

## 11. The rows, in build order, with their proofs

Each row ships both doors for its part (D50). By round 2's read's own measure, rows a and b may go ahead with finding 4's two
classification rows; rows c, d and d2 once findings 3, 5, 7 and 8 are taken; and row e once findings 1 and 2 are. This text takes them
all (§16), for the steward to check against the read without another; Codex's read stays owed under D191.

**After Codex's read** (§17), each blocking finding is taken before the row it names: finding 1 before d and d2; findings 2 to 5 before
e; finding 6 as a correction to the built row a, with its test, before i; finding 7 before c; finding 8 before f. Each row below carries
its amendment. Rows a and b are built; a's correction lands with this amendment.

**What the owner's own repositories follow on the install, and row f.** On the install every run binding reads `current`: the home keeps
no named workflow, and each of its run bindings, as of 2026-10-24, names `current` with two steps, the agent's work and a landing the
person accepts as a branch (`quest/{slug}-{quest}` for this repository, DOGFOOD1). No graph version is chosen anywhere, so rows c to e give
those repositories a chat run under Current, never `chat-graph-later`, and row f brings them nothing yet. Bringing row f forward was
weighed and is not done: it waits behind FABRIC-CUTOVER1e and FABRIC-PIPELINE1d and would serve no repository the owner runs today. The
same reading says what row d will gate there: Current names no second opinion and no look for those repositories, so until the owner's
rules name one, a chat run's gate reads the same rules a quest's work reads and holds nothing more. The owner's *no checks, no gate* is then
met by rows c and d together with a rule naming an opinion (CHATFLOW1g sets one for its proof).

- [ ] **CHATFLOW1a — the no-run code and the record's kinds** (service, driver, modules). `/api/sessions` answers `tookQuest` to this
  machine; `TracedSession` reads `opinion`, `serves`, `piece`, `from`, `entry` and `tookQuest`; `ChainsOf` classifies in §4.2's order, a chat
  that took a quest reading its quest's run; `WorkflowRunRead.Code`, the route's `code`, §10's codes with `chat-own-tree` for every chat a
  tree of its own holds, and the terminal saying each code's English. Proof: `WorkflowRunTests`, one row per code and a row each for a
  reviewer's `o-…` clone, a helper and a partner (trees `Holds` would take), an own-tree chat reading `chat-own-tree` and never
  `chat-checkout`, an intake still reading its ask's runs, a chat that took a quest reading that quest's run, **a closed quest's session
  gone on as a conversation in the quest's tree reading that quest's run, never a chat run** (its stored origin `quest:<id>`, §4.1), and
  **a CHATTAKE1 chat's hand-over, whose next record carries no `took_quest`, still reading the taken quest's run** (§4.2); `TraceTests` (the six
  fields); `DriverModuleWorkflowRunTests` (the answer's `code`, `:323-333` extended); the twin table's rows for each code's English.
  *Built* (D157's CHATFLOW1a note). **Corrected by Codex's finding 6, before row i** (§4.1, §4.2): `WorkflowRunReader.WorkOf`, the one
  resolver of the concrete work, read before an origin by `ChainsOf` and selecting `RunAsync`'s work records. Proof, seen failing first:
  `WorkflowRunTests` rows for **a quest an ask sent, its worker gone on as a conversation, reading that quest's run and not every run of
  the ask**, **a quest inheriting its publisher's origin from another tree, its worker's continuation reading that quest's run and never
  `chat-own-tree`**, a genuine intake keeping its ask-wide view, and **a continuing conversation contributing its worker, its standing
  tree and its landing to the run** (`ReadAsync` over a stand-in service). The opinion read on that tree goes through the same work record;
  the doors' use of the resolver, and its opinion and landing gate, are row i's proof.
- [ ] **CHATFLOW1b — the page words it** (web; after a). The view's codes in `workflow.run.json`, en and zh, the driver's sentence for an
  unknown code, and *Try again* on `unanswered`. Proof: `WorkflowRunView.test.tsx` renders each code in en and in zh and an unknown one as
  the sentence (`:285-295` amended): **a chat with no tree says so in both languages** (`chat-checkout`), and an own-tree chat reads
  `chat-own-tree`. Also stories per code, `npm --prefix src/Daoris.Web run i18n:check` and `names:check --strict`. *Built* (D157's
  CHATFLOW1b note), and stands for its translation scope.
- [ ] **CHATFLOW1c — a chat's run, keyed and bound** (driver, modules, web; after a's correction and finding 7). `chat-<first>` from the stored line (§4.1); the binding
  when the tree opens, never to a graph version (`chat-graph-later`); a failed bind said once, and a press on an unbound run binding first
  and refused `bind-failed` only for an unwritable home, a graph choice never refused and an unreadable named workflow bound with its
  `Problem` (§4.3); `ChainsOf`'s chat run read by the line, with the work step's states; `TreeGates.ProcessAsync` reading the chat run's
  process; the told landing planned with the bound process (§4.4); the view's checks marked not yet enforced until row h;
  `SessionHomeFiles` taking a chat run's three files and the leftover rule sweeping them (§4.3). The conversation is told nothing of its
  checks. Proof: `WorkflowRunTests`: **a chat's tree with commits shows its run and its checks** (Current's `sessionChecks`, and a steps
  version's `checks`), marked not yet enforced, its opinion `not-reached` with no words of when, a hand-over's second record reading the
  same run, and an ended chat with commits reading `land`. `WorkflowRunBindingTests` (bound under the repository's default, never by a
  kind; a graph version bound nothing); `OpinionLandingTests` or `ReviewLandingTests` rows for the press's bind-first: a graph choice
  landing as today, never `bind-failed`; an unwritable home refused `bind-failed`; an unreadable named workflow held by its problem.
  `SessionDeletionTests` (a chat's delete taking its binding, gate file and look file); a `HistoryClearingTests` leftover-rule row (a
  `chat-<id>` file under each of the three folders kept while the record `<id>` stands and taken once it is gone);
  `DriverModuleWorkflowRunTests` (a chat's `{ session }` answers a run); `ChatRunnerTests` (the bind beside the process told, the told
  landing planned with the bound process, and no checks told). **Amended by Codex's finding 7** (§4.2): the chat run's facts carry
  `Candidate`, and `Derive` reads the later steps by it rather than by the displayed work step; the opinion keeps its own state while an
  answering turn runs. Its proof adds `WorkflowRunTests` rows for the view **before the first Accept, during review, during the answer's
  delivery, after the recheck and after the landing**, the opinion and the landing visible before the landing throughout. **The web lane**
  (finding 11): the view's *not yet enforced* mark on the checks, en and zh, in `WorkflowRunView.test.tsx`, a story, `i18n:check` and
  `names:check --strict`.
- [ ] **CHATFLOW1d — the gate refuses the press and asks the opinion** (driver, modules, web; after c). The opinion's part keyed by the
  run (`OpinionGates` under `chat-<first>`, `OpinionGate.JudgeAsync` with a run and no quest); `OpinionDue.Run`; the press asking at
  `not-asked` and nothing twice; `OpinionPresses.WorkAsync` reading a chat run; and the quest-keyed sites §7 names, each keyed by the run:
  `OpinionPresses.PressedAsync` (`:64`, called with `questId` at `TreesRoutes.cs:586`), `OpinionGateLook.JudgeAsync`'s chain, process and
  gate (`:185-206`), `AskPassAsync`'s words from the conversation's opening (`:381`), and a chat run's dues written at `landing`, *Ask
  now*'s included (`OpinionPresses.cs:118`). The plan (`LANDING`, `trees land --plan`) asks nothing (§5.2 point 5); the person learns the
  opinion settled at the foot, on the review row and by a notice (§5.2 point 6); the refusal's words on both doors; the opinion's step's
  words. Proof: `OpinionLandingTests` rows: a chat's press refused and the opinion asked; pressed again while it reads, refused and nothing
  asked; settled, landed at the press; a merge-form rule merging at the press; uncommitted work refused; a hand-over's next record pressing
  the same gate; a later landing showing `commits-since` where the recheck is spent; **a chat run's press answering a dispute and
  `commits-since`**; **the plan asking nothing**, by the route and by `trees land --plan`; *Ask now*'s due on a chat run written at
  `landing` under a rule whose first occasion is `steps`. A `NoticesTests` row (the settled notice). `OpinionGateTests` (a chat run's gate, its file by run key),
  `OpinionPacketTests` (the opening named as the opening, no conversation), `TreesWordsTests` (the refusal's words, exit 1), and
  `WorkFrame.test.tsx` (the foot's refusal, en and zh). The new presses' reach: `HelpCoverageTests` exempts them as the person's (D110).
  **Amended by Codex's findings 1 and 11, before d** (§5.2 point 1): the press is one driver operation that reserves the conversation
  against another turn, captures one tip, judges every part at it, writes the due with that tip and the person's turn count, and lands by
  the pinned path (`LandingGate.Tip`), refusing a moved candidate; the bridge, the connected terminal and the standalone terminal
  (`TreesConsole.cs:390`, `:417`) each call it, and their plans write nothing. Its proof adds `OpinionLandingTests` rows that **move the
  candidate after the gate's judgement** (a commit made between the judgement and the landing is refused, never landed), **while the
  press waits for the landing hold**, and **across a hand-over**; a press refused while a turn runs; a press on a conversation another
  process holds, through the pipe; each on both terminal paths (merge and branch forms) and through the bridge, the connected terminal and
  the standalone terminal, with `TreesWordsTests` for each door's refusal words and exit code.
- [ ] **CHATFLOW1d2 — the look on a chat run, and the person's way past it** (driver, modules, web; with d). The look's part `cannot-start`
  for a chat run under a rule or version that requires one; *Accept without the look…*, its file, the page's press and
  `trees land <id> --without-look`, a `Person` route. Proof: `ReviewLandingTests` (refused with the limit's words; the person's press
  lands at that tip; a later commit asks again; the landing record names it), `DriverModuleTreesWordsTests` (the terminal door's words and
  exit code), `PipeCensus`'s census row, `HelpCoverageTests` (exempt), and `WorkFrame.test.tsx` in en and zh. **Amended by Codex's
  finding 1, before d2** (§5.3): the skip is kept at the press's captured tip and the landing pinned to it; its proof adds a
  `ReviewLandingTests` row where the tree moves after the skip is judged, refused and never landed under the skip.
- [ ] **CHATFLOW1e — its send-back only between turns** (service, driver, modules; after d). §6.1's five: (a) the service's hand admitting
  a chat run's `working` record of the line, naming the line's newest record and letting it answer; (b) `LoopContext.SendsBack`, wired by
  `DriverLoop` as `HandsOver` is, reserving the turns atomically under `ChatTurns`' lock as `TakeHeld` does, the findings an `Ordinary`
  word with `by`, `person-went-on` where a word of the person's was first, and waiting where no runner holds the chat; (c) the answers read
  at the send-back turn's end; (d) `DeliverAsync` reading a chat run's record by the runner's turn state; (e) findings handed before a
  hand-over re-handed once to the newest record. D155's automatic recheck at the look after the answering turn (§6.2). `opinion_answer` in
  a chat run's rules from its spawn. Proof: `OpinionDeliveryTests`: **its send-back reaches the conversation only between turns**.
  Findings wait while a turn runs and while words are queued, and are taken as a turn of their own once it ends; a word the person sent
  first goes first and the findings go to the person; **a word arriving between the check and the take** goes first and the findings go
  to the person, never behind it; **the headless host** (`SendsBack` null) leaves them waiting; **findings handed before a hand-over**
  re-handed to the newest record, which answers them; **the answers read at the send-back turn's end**, and the recheck started at the
  next look, the gate `read-again` while it reads and never with nothing reading. A service row (`Daoris.Service.Tests`, beside
  `McpOpinionToolTests`) for the hand admitting a chat run's `working` record and refusing every other active one; a `ChatTurnsTests` row
  for the reservation (refused while a word waits or a turn pumps; a word sent after it waits behind the send-back; never `NextStep`); a
  `DriverLoop` modules row for the seam's wiring; a `ChatRunnerTests` row for the rule handed at spawn. Also an `OpinionResumeTickTests`
  (`Process`) case on both doors, run by the CI. **Amended by Codex's findings 2 to 5 and 9, before e** (§6.1, §6.2): six things, not five;
  the reservation taken inside the dispatcher's keep, adopting the hand's word in the same operation (2); the answering turn's own
  completion and frozen reading, read by the recheck (3); the person's turn count recorded at the press and compared at the reservation
  (4); an ended chat's path through its owning runner's go-on (5, (f)); the recheck's reviewer chosen outside the answering successor's
  line too (9). The proof's outcomes are now distinct: **waiting** only for activity that is not the person's (a Daoris word's turn, a
  hand-over opening) and for a live chat no runner here holds or a headless host; **to the person** only where the person's turn count
  rose since the press. Its proof adds `ChatTurnsTests` rows **holding the person's service write between the reservation's first check
  and `Take`** (the findings never overtake it, and go to the person), and **a person's queued turn changing `HEAD` immediately after the
  answering turn** (the frozen reading holds the answering turn's tip, and the recheck reads that); `OpinionDeliveryTests` rows for **a
  person's turn completed between the press and the findings, the chat idle when they arrive** (to the person, never started), for
  **temporary activity that is not the person's** (waiting, then delivered), and for **an ended resumable chat with no person's words
  waiting** (handed to the ended record, gone on through its runner, answered in its own conversation), a runner's definitive refusal
  sending them to the person; and an `OpinionDeliveryTests` recheck row where **the answering successor is of the first reviewer's
  family**, so that reviewer is not presented as independent of the fixes.
- [ ] **CHATFLOW1f — a graph-bound chat run** (driver; after d, e, FABRIC-CUTOVER1e). The bind of a graph version; the press reaching
  `claimed` at its tip and `handed` landing at that press; its `check` nodes and their send-back. Proof: `WorkflowGraphRunTests` and
  `GraphRunLandingTests` rows for a `run:chat-<first>` unit, and `chat-graph-later` no longer answered. **Amended by Codex's finding 8,
  before f** (§4.5): the candidate as the subject, submitted at the press; `quest.close` refused at the bind; a later candidate reopening
  the same run at a press, never at a turn's end; the bind of a graph version stays disabled until this lands. Its proof covers **two
  successive landings of one run**, **a hand-over between submission and landing**, **a `quest.close` graph refused**, and **a failed
  check's single send-back** on a candidate; a `run:chat-…` fixture alone does not.
- [ ] **CHATFLOW1g — on the install** (the parent; after a republish carrying a to e and d2). A chat in its own tree, under a workflow
  naming an opinion, refused at its first press with the opinion asked, read by another maker, answered between turns, and landed by the
  person's next press. A checkout chat and Ask Daoris say why they run none. Both languages at 1546 and 680 px. Proof: dated shots and a
  ledger under D157. **Its ledger keeps the demonstration apart** (Codex's read, row g): one uninterrupted loop on the install shows the
  path working and proves none of the races, which rows d and e's boundary proofs carry; the ledger says which it shows.
- [ ] **CHATFLOW1h — a chat's checks enforced, then told** (driver, web; after c). The session lock in the chat's id for its life, its checks
  kept for its life, `HeavyTold.Hand`'s deny rules in its spawn's rules, a hand-over's next record told, and only then the process told
  with its checks (§4.4). Proof: a `HeavyRunCommandTests` row refusing `run-heavy` on a `light` chat's tree; `ChatRunnerTests` (the rules
  and the words); a `HandOverProcessTests` row (the next record told). **Amended by Codex's findings 10 and 11** (§4.4): the lock and the
  checks acquired at every execution, a go-on in its own conversation and the come-up included, and released on a failed start; the
  checks kept being those handed, the live rule shown beside them where it changed. Its proof adds **an end-and-go-on row for a light
  chat** (`run-heavy` still refused on its tree after the go-on), a failed start releasing the lock, and a rule changed mid-life; the web
  lane takes the *not yet enforced* mark off, en and zh, in `WorkflowRunView.test.tsx`.
- [ ] **CHATFLOW1i — a chat that took a quest is gated by that quest** (driver, modules; after a). `LAND_SESSION_TREE` and
  `OpinionPresses.WorkAsync` read a record's `tookQuest` where its `quest` is null, from any record of the line in this tree (§4.2), so a
  CHATTAKE1 chat's press, and its hand-over's next record's, is gated by its taken quest's chain, as its view already reads that quest's
  run; a line whose stored origin is a quest's (§4.1) is gated by that quest's chain the same way. Proof: `OpinionLandingTests` and
  `ReviewLandingTests` rows for a chat that took a quest, its hand-over's next record, and a closed quest's session gone on as a
  conversation, and a chat that took none unchanged. **Amended by Codex's finding 6** (§4.1): every door, `LAND_SESSION_TREE`, the
  terminal's land and `OpinionPresses.WorkAsync`, asks `WorkflowRunReader.WorkOf`, the resolver row a's correction built, so the view and
  every landing and opinion door decide by one answer. Its proof adds rows for **a quest an ask sent, its worker gone on as a
  conversation**, and **a quest inheriting its publisher's origin from another tree**, each gated by its quest's chain, asserting the
  worker, the tree, the opinion and the landing the door reads.

## 12. Rejected

- **Steps started at every turn's end with commits**: a draft read as a candidate, another maker's account spent per reply, the one
  recheck used up early, and hours of gates queued behind a conversation.
- **The press kept on the due list to land later** (round 1's §5): `AutoLander` cannot carry it without a branch of its own, it would
  merge at a moment the person is not pressing, and it would race a turn in the tree (§5.1). The refusing gate needs none of that, and
  leaves the landing the person's press in time. A kept press may be weighed again as a later row if it is still wanted.
- **A chat falling back to Current when its bind fails**: it could skip what the repository's chosen workflow asks of the person (§4.3).
- **Binding a graph version before CHATFLOW1f**: it would arm a runtime whose chat subjects nobody has said.
- **Telling a chat checks nothing enforces**: the words would be false (§1.4, §4.4).
- **Clearing a chat's binding at its landing**: the steps after the landing read it, and a chat that goes on would bind again mid-run.
- **Making a chat publish a quest to itself** so it rides the quest's path: an outward record for local work (D47 §4), a quest nobody
  asked for on the service, and an intake's job done by a conversation. A look's set-up for a conversation's work would need the same,
  so §5.3 gives the person's way past instead.
- **Keying the run by the tree's name** (`s-<8 hex>`): a run is the work's, and the session is what the person, the records and the view
  name. **Keying it by the session id bare**: it works by length alone. **Walking `From` with a bound of its own**: the line is already
  stored, and a bound copies PARTNERLINE1's open defect (`TASKS.md:387`; the FIX-LOG's HELPERWAIT1 entry).
- **Words into a turn in flight**, at a next step, by *Send now*, or urgent: the person is typing into that conversation, and D137 point 2
  and D136 give a running turn only words its sender chose to send sooner. **`Tell` for the send-back**: it is urgent and Daoris's.
- **Queueing a send-back behind the person's words**: it would answer work the person has already moved past.
- **Handing the reviewer the conversation, or the person's later words** (D155 point 6, and its rejection of the worker's conversation).
- **A kind or a task choice for a chat**: no ask carries one, and an agent choosing one could lower the person's part (D157 point 10).
- **A go-ahead on the chat's session**: the session door is built for an ask's sessions and refuses this one by design
  (`SessionGoAheads.cs:75-84`). WORKFLOW1l's run go-ahead is the home.
- **A code for an intake**: it already reads its ask's runs.
- **A per-landing first pass in the service, decided here**: it is the owner's call, not this design's. As built, a later landing is
  seldom read by an agent (§5.2 point 9); giving a chat run one automatic first pass per landing would change the service's bound for it,
  and is left to the owner as a row of its own if wanted.
- **The recheck asked at the person's next press** (round 2): nothing asks a recheck at a press, and the gate would sit in `read-again`
  with nothing reading (`OpinionGate.cs:428-438`), unless a gate state, a press's write and their proof were added. D155 point 7's
  automatic recheck after the answering turn needs none of that (§6.2).
- **Checking `TurnEnded` and then calling `Take`** (round 2): a word the person sends between the two puts the send-back behind it. The
  take is one act under the turns' lock (§6.1).
- **The history clear as a chat run's clear** (round 2): it never removes a chat (§4.3).
- **A workflow for Ask Daoris or a checkout chat**: neither changes code Daoris lands (§9).
- **A disabled button or a clean tree as the press's boundary** (Codex's read): offering the press between turns reserves nothing, and a
  clean, newly committed turn passes the dirty check; the press reserves the turns and pins its tip (§5.2 point 1).
- **`TakeHeld`'s task as the answering turn's end** (Codex's read): it is the pump's drain, which a person's queued word extends; the
  answering turn has a completion of its own (§6.1 (c)).
- **Present idleness as the send-back's test** (Codex's read): a turn the person finished since the press leaves the chat idle; the person's
  turn count decides (§6.1 (b)).
- **An origin read as the work** (Codex's read): an ask's or a publisher's origin groups work, and a concrete quest governs it (§4.1).

## 13. What this amends, once accepted

- **D157.** The workflow design §12.2's *a conversation's work under a workflow* is lifted for a chat in its own tree. Point 11's run gains
  a second key, `chat-<first>` from the record's stored line, bound when the tree opens and read by the line with no chain; a graph version
  is not bound until CHATFLOW1f, and the binding, with the run's gate and look files, goes with the session delete of the record its key
  names, the leftover rule taking what no record holds. A line whose stored origin is a quest's reads that quest's run. Point 10's choice
  has no task level or kind for it, and a press on a run that cannot be bound, for an unwritable home only, is refused; a graph choice is
  never refused for it. Point 2's gate refuses a chat's press with its reason and asks the opinion at that press, and the plan asks
  nothing. Point 9's send-back reaches a chat only between turns. D157's amendment of D135 (`D157.md:106-107`): a chat run's go-ahead waits
  for WORKFLOW1l's. The *Workflow* view's no-run answer carries a code, said in English on both doors.
- **D155.** The second-agent design §12.2's *opinions on a conversation's work* is lifted for a chat run's landing. Point 2's `landing`
  occasion is asked at the person's press, its due written at `landing`. Point 6's packet takes the conversation's opening, named as such,
  and the commits' messages as the claim. Point 7's findings go to a chat once, between turns, as an ordinary word carrying `by`, taken by
  one atomic act under the conversation's turns and handed by the service to the line's newest record; to the person where the person
  spoke first. Point 7's automatic recheck stands, at the look after the answering turn, which Daoris started. A later landing is read by
  another agent only where that recheck is unspent. The gate's answers and the due are kept by the run's key.
- **D154**, by reference: a look that cannot be set up for a chat run reads `cannot-start`, and its row 1 skip is re-homed as the person's
  *Accept without the look…* on the run.
- **After Codex's read** (§17), these amend the two notes above. D157: a record's run is the concrete work its line continues, resolved
  before its origin by one resolver the view and every door share; a chat run's later steps are derived by its candidate's readiness; the
  press reserves the conversation, judges at one captured tip and lands it pinned, on every door by one driver operation; a graph-bound
  chat run's subject is its candidate, submitted at the press, `quest.close` refused, and a later candidate reopens the run at a press;
  a chat's checks are acquired at every execution. D155: the due carries the press's tip and the person's turn count; the send-back's
  reservation is part of the dispatcher's keep, the person's turn count deciding whether the findings go to the person and activity
  that is not theirs whether they wait; the answering turn's frozen reading is what the recheck reads; an ended chat takes the findings
  through its own runner's go-on; and the recheck's reviewer is chosen outside the answering successor's line too.
- **Standing.** D137 points 2 and 3 (the door), D136, D145 point 1, D154 point 8, D155 points 4, 6 and 8, D157 points 9 and 10, D164
  (Accept names the branch), D47 §4, D50, D110, D37.

## 14. What the checks do not cover

Rows a and b are built, and row a's correction for finding 6 is built with this amendment (`WorkflowRunTests`, seen failing first); the
rest is documents. Each statement in §1 was read at `ca046e7fe` and re-pinned at `507b1925d`, where §16's citations were read too; §17's
were read at `5d19f6b3a`. The answering turn's boundary bounds what other turns made, not a commit made in the tree by the person's own
git outside any turn, which nothing tells apart (§6.2). The other sections' citations name what the change reads or narrows, and were checked to exist there. No chat was run on
the install; what its run bindings name (§11) was read from the install's home, not from a run. The names `SendsBack` and the reservation
are this design's, for row e to keep or rename. Whether an agent in a chat calls `opinion_answer`
under the handed rule is unmeasured, as it was for a driven one (XAGENT1j). The `check` step's runtime is FABRIC-PIPELINE1d's, reached by
a chat only through CHATFLOW1f. The zh words follow the glossary, and no translator read them. `verify` checks the router's row, the decisions' notes and the links here, and none of the words.

## 15. Round 2: the read's findings and their answers

Every finding of the Opus foundation read of round 1 (`docs/2026-10-24-chat-flow-reads.md` §4), each checked against the code on main at
`ca046e7fe`. *Taken* means the design now says it; *answered* means it is not taken, for the reason given; *deferred* means a later row
carries it. The read's §1 to §3 corrections are findings below, and the rest are taken where they are named. This table is kept as
round 2 answered; where round 2's read changed an answer (rows 6, 8, 11, 14, 15 and 17), §16 says what the text now holds.

| # | Finding (blocking in bold) | Checked against | Answer |
|---|---|---|---|
| **1** | **An own-tree chat reads as a checkout chat in rows a and b** | `WorkflowRun.Read.cs:87` answers every chat; `SessionTrees.cs:471` | **Taken** as named: `chat-checkout` only where `SessionTrees.Holds` does not hold the tree; an own-tree chat reads `chat-own-tree` until row c's run (§4.2, §10). Rows a and b each prove it. |
| **2** | **The `intake` code contradicts built behaviour** | `WorkflowRun.Read.cs:86`; `D157.md:241` | **Taken** as named: no `intake` code; an intake reads its ask's runs and `ask-no-quest` before any quest (§4.2, §9, §10). Row a proves an intake unchanged; the D157 note no longer lists it. |
| **3** | **Row c would arm graph-bound chat runs before CHATFLOW1f** | `GraphRuns.cs:59-110`; `TreeGates.cs:64-77` | **Taken** as named: row c binds only Current or a steps version; a graph version binds nothing, answers `chat-graph-later`, and the gate reads as today (§4.3). CHATFLOW1f binds it. |
| **4** | **D154's look has no home for a chat run** | `ReviewGate.cs:271`, `:345`; `PersonDoorTableTests.cs:103-104` | **Taken**, the second way: a required look on a chat run reads `cannot-start` and refuses the press; the person's way past is *Accept without the look…*, kept on the run's machine-local look file with its doors and census row (§5.3, row d2, shipped with d). A set-up for a conversation's work is rejected (§12). |
| **5** | **The kept Land cannot ride the automatic due list** | `AutoLander.cs:184-191`, `:211-228`; `AutoLanding.cs:154`, `:183-187`, `:205-206` | **Taken**, the first way: the refusing gate (finding 23). No press is kept; the merge-form case merges at the person's press; uncommitted work refuses as today; a hand-over shares the line's gate (§5.1, §5.2). |
| **6** | **The send-back's own commits void the Land it serves** | round 1 §5 point 4, §6; `D155.md:50-53` | **Taken**: with no kept press nothing is voided; the recheck is asked at the person's next press, reading the new tip and counting against D155's one recheck, never started at a turn's end (§5.2 point 4, §6). |
| **7** | **A `light` chat is told enforcement it does not have** | `HeavyRun.cs:180-189`; `SessionTrees.cs:404`; `Driver.cs:2442`, `:2551-2552`; `DriverContinuations.cs:418-421`; `ChatRunner.cs:613-619`, `:1349-1360`; `HeavyTold.cs:72-80` | **Taken**: the view shows the checks from row c, and the chat is told nothing of them until CHATFLOW1h does the read's five steps (the session lock for the conversation's life, the checks kept for it, the deny rules in its spawn, a hand-over told, a `HeavyRunCommandTests` row); only then is it told (§4.4). |
| **8** | **The binding is cleared at the wrong time, and the history clear deletes it** | `HistoryHome.cs:29-31`, `:158-167`; `D157.md:417-423` | **Taken** as named: cleared with its records by the history clear only; the leftover rule reads `chat-<id>` as the record `<id>`'s; one run spans every landing of its tree (§4.1, §4.3). Proved by `HistoryClearingTests` in row c. |
| 9 | No steps version can hold a `check` | `WorkflowNamed.cs:343-348`; `D157.md:485` | **Taken**: `check` steps exist only under a graph version; row c's `cannot-start` case is dropped and their proof is CHATFLOW1f's (§4.2, §11). |
| 10 | Row c's run promises an opinion that row d delivers | round 1 §4.2 | **Taken**: until row d the opinion's step reads `not-reached` with no words of when; row d ships its words (§4.2). |
| 11 | The key reinvents the stored entry and copies an open defect | `SessionEntries.cs:71-93`; `SessionLedger.cs:692`; `TASKS.md:387`; `FIX-LOG.md:758`, `:764` | **Taken**, the first way: the key is the entry's origin `session:<first>` where that record's tree is this tree, else the record's own id; no bound of 16 (§4.1). Where the design's *same tree* rule differs from the entry's (a line moved to another tree), §4.1 says so. |
| 12 | The trace lacks the fields that tell a reviewer, a helper and a partner apart | `Trace.cs:267-286`; `OpinionTree.cs:18-20`; `TreeContext.cs:67`; `ApiContracts.cs:425-454` | **Taken**: row a reads `opinion`, `serves`, `piece`, `from`, `entry` (and `tookQuest`) into `TracedSession`; its proof covers each kind, and §4.1 is aligned with the rows (§4.2, §11). |
| 13 | The design misstates what a partner does | `D199.md:16-29`; `D155.md:488-492` | **Taken**: §1.6's folder corrected; `partner` has its own words; partners' agents join §7's working families. |
| 14 | A chat that took a quest is not addressed | `SessionEntries.cs:102-106`; `Sessions.cs:250`; `ServiceClient.cs:1981-1992` | **Taken** for the view: its run is the taken quest's, never a chat run; the service answers `tookQuest` and row a reads it (§4.2). **Deferred** for its gate: today's press reads only `quest` (§1.1), and gating a CHATTAKE1 chat by its taken quest changes that row's landing, so it is a row of its own, CHATFLOW1i; until then such a chat is never a chat run and lands as today. |
| 15 | A later landing gets no owed opinion, and the pass is not pinned to the Land's tip | `OpinionDesk.cs:395-406`; `OpinionGateLook.cs:353` | **Answered**: the service's bound limits only the automatic first pass; a second landing is read by the one recheck or `commits-since`, which the person answers or meets with *Ask now*, never capped (§5.2 point 7). With no kept press there is no Land tip to pin; the gate judges what would land against the tip the pass read (§7). **Taken**: the packet names the conversation's opening as the opening. |
| 16 | The run's derivation is understated | `WorkflowRun.Read.cs:129-187`; `WorkflowRun.cs:346-351`; `SessionGroups.cs:898` | **Taken**: every fact selected by the line; the work step reads `SessionEvents` where no runner holds the chat; an ended chat with commits reads `land` (§4.2). |
| 17 | The send-back's delivery is unspecified | `ChatRunner.cs:1821`; `ChatMessage.cs:27`; `ChatRunner.cs:1349-1360`; `ChatGoOn.cs:535-541` | **Taken**: an `Ordinary` word with `by`, not `Tell`; `opinion_answer` in the chat run's rules from spawn; findings to the line's newest record; a turn started while the person drafts said and accepted; ancestry decides what a pass covers, so a rebase leaves it covering nothing (§6). |
| 18 | A failed bind can lower the person's part | round 1 §4.3; `D157.md:362`, `:393-394` | **Taken** as named: a press on an unbound chat run tries the bind first; one that cannot be made refuses with `bind-failed`; the conversation goes on (§4.3). |
| 19 | A run's go-ahead has no door | `WorkflowCurrent.cs:86-87` | **Taken**: until WORKFLOW1l a chat run's `go-ahead` step reads `cannot-start` with that limit; §8 corrected (§5.4). |
| 20 | The doors and the census are unnamed | D50, D110 | **Taken**: with no kept press, `LAND_SESSION_TREE` keeps its two answers and its refusal's words and exit code (row d's `TreesWordsTests`); *Don't land it* is gone; *Accept without the look…* has its terminal door and census row (row d2); *Ask now* opened to a chat run and the new press are exempted in `HelpCoverageTests` as the person's; `WORKFLOW_RUN`'s `code` and the twin table's rows are in row a. |
| 21 | Some words are wrong | `glossary.json:652`; workflow design `:553-554`; round 1 §10's zh | **Taken**: `chat-checkout`'s zh is "请开始一个有自己工作树的对话"; each code's English is held on both doors by the twin table (§10). **Answered** for *Ask again*: the quest asks 重试 for *try again*, the page already says *Try again* (重试) for a failed read (`help.history.retry`, `signin.retry`), and *Ask again* (再征询一次) is the opinion's press to ask a reviewer again, which a read is not. |
| 22 | Citations | round 1 §13, §4.2, §1.5 | **Taken**: D157's amendment of D135 named in place of point 13; `Workflows/Domain/` on both paths; `TracedRecords.cs:69-70` cited (§1, §13). |
| 23 | The deferred press is not weighed against the refuse-and-ask gate | round 1 §5 point 2 | **Taken**: §5 and §6 rewritten around the refusing gate, §4.3 and §7 with them; the kept press is rejected with its reasons and left as a later row if wanted (§5.1, §12). |
| 24 | The backlog has two open CHATFLOW1 rows | `TASKS.md:236`, `:386` | **Taken**: folded into the one at `:386`, which names the design's round. |

**What stays owed.** Codex's read, on this text (D191, its 2026-10-23 amendment). Rows a and b are settled by the read's own measure and
may go ahead; the rest wait until both reads' blocking findings are taken. The design stays proposed.

## 16. Round 2's read: where each named change went

Every finding of the Opus foundation read of round 2 (`docs/2026-10-24-chat-flow-reads.md`, round 2 §4), taken as the read names it.
Each place was checked against the code on main at `507b1925d`.

| # | Finding (blocking in bold) | Where it went |
|---|---|---|
| **1** | **Row e's send-back cannot be built in its lane, and §6's checks race** | §6.1: the service's hand admits a chat run's `working` record and names the line's newest record (`OpinionDesk.cs:248-253`, `:285-294`, `:480-492`); `LoopContext.SendsBack`, wired by `DriverLoop` as `HandsOver` is (`LoopContext.cs:57-71`, `DriverLoop.cs:890-895`), reserves the turns under `ChatTurns`' lock as `TakeHeld` does (`ChatTurns.cs:552-567`); the answers are read at that turn's end; `DeliverAsync` reads the turn state, not `Running` (`OpinionDeliverer.cs:105-114`); findings handed before a hand-over are re-handed once. Row e's lanes are service, driver and modules, with proof rows for each and for a word between the check and the take (§11). |
| **2** | **The recheck "at the person's next press" has no mechanism** | §6.2: D155 point 7's automatic recheck is kept, at the look after the answering turn (`OpinionDeliverer.cs:174-184`), so `read-again` (`OpinionGate.cs:428-438`) always has a recheck reading. §3 rule 4 now governs the person's turns; the amendment is dropped from §5.2 point 4, §6, §13 and the D155 note, and §12 rejects it. |
| 3 | The history clear is the wrong door | §4.3: a chat run's binding, gate file and look file join `SessionHomeFiles`' names, so the session delete of the record its key names takes them (`SessionDeletion.cs:50-86`, `:75`); the leftover rule takes `chat-<id>` files under the three folders where no record remains (`HistoryHome.cs:103-167`). Row c's proof is `SessionDeletionTests` and a leftover-rule row; §13 and the D157 note follow. |
| 4 | The key's fallback misses the case it catches, and `tookQuest` is per record | §4.1: "a line that moved to another tree" is dropped (no chat's `From` changes tree, §1.1); a line whose stored origin is a quest's or an ask's reads that quest's run and is gated as row i gates (`SessionEntries.cs:84-94`, `BriefedMove.cs:26-27`). §4.2: `tookQuest` is read from any record of the line in this tree (`Sessions.cs:1733-1740`). Row a proves both, and row i gates both. |
| 5 | Row d's quest-keyed sites and two door gaps | §7 names `PressedAsync` (`OpinionPresses.cs:64`, `TreesRoutes.cs:586`), `OpinionGateLook.JudgeAsync` (`:185-206`), `AskPassAsync`'s words (`:381`) and the dues at `landing` (`OpinionPresses.cs:118`, `OpinionLook.cs:80-84`). §5.2 points 5 and 6: the plan asks nothing (`TreesRoutes.cs:561`), and the person learns it settled at the foot, the review row and a notice. Row d lists each, with proof rows for a press answering a dispute and `commits-since` and for the plan asking nothing. |
| 6 | A later landing is rarely read by another agent | §5.2 point 9 says it plainly (`OpinionDesk.cs:395-426`, `:417-418`), and names a per-landing first pass as the owner's call, which §12 leaves to them. |
| 7 | What the conversation is told, and what the view shows, differ from row c | §4.4: row c plans the told landing with the bound process (`ChatRunner.cs:2244`, `TreesRoutes.cs:561-566`), and the view marks a chat run's checks not yet enforced until row h. Row c lists both with their proof. |
| 8 | The press's bind-first must leave a graph choice as today | §4.3: a graph choice is never refused `bind-failed`; `bind-failed` means only an unwritable home; an unreadable named workflow binds with its `Problem` and holds the press by it (`WorkflowRunBindings.cs:120-132`, `WorkflowProcesses.cs:65-70`). Row c proves the three. |
| 9 | Two codes' words | §10: `ask-daoris`'s en and zh as the read words them; §9 and §2 cite D183 points 1 and 5 for the room in place of D89's remark; `helper`'s zh ends 进展在它协助的会话那里。 |
| 10 | The owner's own repositories' workflow is not stated | §11: on the install every run binding names `current` (work, then a landing the person accepts as a branch) and no named workflow is saved, so rows c to e give those repositories a chat run under Current, never `chat-graph-later`. Bringing row f forward is weighed and not done. |
| 11 | Citations drifted at HEAD | The standing corrects "only tests changed"; §1 re-pins `Driver.cs` (`:2447`, `:2548-2557`) and `SessionLedger.cs` (+6 from `:354` to `:700`); the backlog row stands at `TASKS.md:384`. |

**What stays owed.** Codex's read, on the final text (D191). The steward checks these changes against round 2's read without another
read. The design and its D157 and D155 notes stay proposed.

## 17. Codex's read of the accepted text: where each went

Every finding of Codex's owed read of the accepted text (`docs/2026-10-24-chat-flow-reads.md`, *Codex, the accepted text*, §4), taken
as the read names it, each blocking one before the row it names. Each place was checked against the code on main at `5d19f6b3a`, whose
`src/` is the read's `7cda79f01` plus rows a and b's later commits; the read's citations hold there to the line, and those below are
re-pinned where they moved. §15 and §16 are kept as answered.

| # | Finding (blocking in bold) | Checked against | Where it went |
|---|---|---|---|
| **1** | **The landing is not bound to the candidate the gate judged** (before d and d2) | `Bridge/Routes/TreesRoutes.cs:551`, `:596`; `Review/Domain/OpinionGate.cs:675-679`; `Trees/SessionTrees/TreeGates.cs:64-76`; `Trees/SessionTrees/TreeLands.cs:420-438`; `Daoris.Desktop.Driver.Host/TreesConsole.cs:390`, `:417` | §5.2 point 1: the press reserves the conversation, captures one tip, judges every part at it and lands it through `LandingGate.Tip`'s pinned path, refusing a moved candidate, on every door by one driver operation, a conversation another process holds asked through the pipe; point 7 no longer rests on the press being offered between turns. §5.3: the tip skipped is the tip landed. Rows d and d2 carry the proofs: movement after the judgement and while waiting for the landing hold, a hand-over, both terminal paths and the bridge. §12 rejects the button or a clean tree as the boundary. |
| **2** | **The reservation can overtake a person's earlier message** (before e) | `Conversations/Chat/ChatTurns.cs:159-172`, `:197` | §6.1 (b): the reservation is one operation of the dispatcher's keep, taking `_keeping` before it tests idleness, and adopting the hand's word as the reserved turn's in the same operation. Row e's proof holds the person's service write between the check and `Take`. |
| **3** | **The completion task does not delimit the answering turn** (before e) | `ChatTurns.cs:552-567`, `:860-870`; `Review/OpinionAnswers.cs:87-92`, `:127-145`; `Loop/OpinionDeliverer.cs:216-222` | §6.1 (c): the turn's own completion, its ending tip and answers frozen before another queued word runs; §6.2: the recheck reads that frozen reading, and ancestry is not said to prove authorship; §5.2 point 9 and §14 follow. Row e's proof has a person's queued turn move `HEAD` right after the answering turn. §12 rejects `TakeHeld`'s task as the turn's end. |
| **4** | **A completed intervening person's turn is invisible to the send-back** (before e) | `ChatTurns.cs:987-990`; `OpinionGate.cs:96`, `:129`; `Loop/OpinionGateLook.cs:353-359` | §5.2 point 3 and §7: the due carries the press's tip and the person's turn count; §6.1 (b): the count compared at the reservation, a rise sending the findings to the person even where that turn has ended, activity not the person's waiting; §6's bullet on the person's words follows. Row e's proof covers both. §12 rejects present idleness as the test. |
| **5** | **An ended chat can wait forever without the word that would resume it** (before e) | `Conversations/Chat/ChatGoOn.cs:440-443`, `:558-586` | §6.1 (f): an ended resumable chat's path, the findings word handed to the ended record and its existing go-on requested through the owning runner, with (c)'s boundary; headless waiting an explicit outcome; a definitive refusal to the person. §6.2's *never a new session* points to it. Row e's proof has an ended chat with no person's words waiting. |
| **6** | **The origin classifier does not identify the governing work** (a correction to a, before i) | `Sessions/SessionEntries.cs:95-97`, `:108-112` (service); `Workflows/WorkflowRun.Read.cs:187`, `:209-246` | §4.1: the concrete work first, by one resolver, `WorkflowRunReader.WorkOf`; §4.2's table and its work records; row a's correction **built in this change** (`WorkflowRun.Read.cs`, `ChainsOf` and `RunAsync`), with `WorkflowRunTests` rows for an ask-sent quest's worker gone on, a publisher's origin from another tree, a genuine intake, and a continuing conversation's worker, tree and landing, each seen failing first; row i's doors ask the same resolver, with its proof. §12 rejects an origin read as the work. |
| **7** | **The chat's displayed work state prevents its later steps** (before c) | `Workflows/Domain/WorkflowRun.cs:354-363`, `:583`, `:678`, `:735-737` | §4.2: `Candidate` on a chat run's facts, `Derive` reading the later steps by it, the opinion's own state kept while an answering turn runs. Row c's proof covers the view before the first Accept, in review, in delivery, after the recheck and after the landing. |
| **8** | **Row f lacks the graph subject and next-candidate lifecycle** (before f) | `Trees/Nodes/GraphRuns.cs:226-230`; `Trees/Nodes/NodeRuntime.cs:487-501`, `:492-493` | §4.5: the candidate as the subject, submitted at the press; `quest.close` refused at the bind; a later candidate reopening the same run at a press; graph binding disabled until row f builds it (§4.3). Row f's proof: two landings, a hand-over, the refusal, a failed check's single send-back. |
| 9 | Reviewer independence across the re-hand | `Loop/OpinionDeliverer.cs:211-214`; `Review/Domain/OpinionWriters.cs:23-39` | §6.2: the recheck's choice also walks the answering record's line, keeping the first opinion's identity; row e's proof has a successor of the first reviewer's family. |
| 10 | Row h stops before same-record resumption | `ChatGoOn.cs:576-586`; `Sessions/Heavy/HeavyRun.cs:183` | §4.4: the lock and the checks acquired at every execution, come-up included, released on a failed start; the checks kept are those handed. Row h's proof adds an end-and-go-on row for a light chat. |
| 11 | Row ownership and proof outside the lanes | `TreesConsole.cs:390`, `:417` | Rows c and h list web for the checks' mark; row d's press is one driver operation called by the bridge, the connected terminal and the standalone terminal, each proved, their plans free of writes (§5.2 point 1). |
| 12 | The document's standing over its baseline | design `:11`, `:719` before this amendment; `D157.md:550-598` | The standing says what is built and labels §1 the dated baseline; §14 no longer says nothing is built; the citations rows c to i build from are re-pinned here at `5d19f6b3a`. The reads and §15 and §16 are unchanged. |

**What it keeps.** The refusing gate, the local run and the ordinary send-back direction, as the read keeps them. **What stands now.**
D157's and D155's CHATFLOW1 notes stand again on this amended text (D157's amendment note of 2026-10-24). Row a's correction is built;
rows c to i may be built in their order, each with its amendment above.
