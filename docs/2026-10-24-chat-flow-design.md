# A conversation's work in its own tree runs its repository's workflow: a run keyed by the conversation's line, a gate that refuses the person's Accept with its reason and asks the second opinion at that press, a send-back only between turns, and a no-run answer the page words in both languages (proposed, round 2, 2026-10-24)

**Standing:** proposed, round 2, a note amending D157 (the workflow) and D155 (the second opinion) for CHATFLOW1, under D191. Round 1
was written by Claude Opus 5.5 from the code on main at `8cc3936b0` and read at `94c9065f4` by the independent Opus foundation reader
alone, its read verbatim in `docs/2026-10-24-chat-flow-reads.md`: 24 findings, 8 blocking, another round; Codex's read is owed, its
credits out. Round 2 is written by Claude Opus 5.5 from the code on main at `ca046e7fe`. Since the read's `ffcf42926` only tests changed
under `src/` (GITHUBDOCS1's and CI's), so every line the read cites stands. It answers every finding in §15, each checked against that
code. Nothing here is built.

The row CHATFLOW1 in `TASKS.md` carries the case. A chat working in a tree of its own serves no quest, so the session's *Workflow*
view shows only the driver's English sentence, in zh too, and its work reaches the review list with none of its repository's
workflow: no checks, no second opinion and no landing gate. The workflow design left *a conversation's work under a workflow* out,
not rejected (`docs/2026-10-09-workflow-design.md` §12.2), and the second-agent design did the same for *opinions on a conversation's
work* (`docs/2026-10-08-second-agent-design.md` §12.2). §2 says what changed since and why it now belongs in.

**What changed in round 2.** The read's finding 23 is taken: a chat's *Accept…* is gated as a quest's is today. The gate reads the
chat's run, refuses the press with its reason where a part holds, and asks the second opinion at that press. The person presses
again once it settles. No press is kept to land later, so `AutoLander`, a deferred merge and a landing on a live tree are gone from
the design (§5). Rows a and b are settled after findings 1, 2, 12, 13 and 21, and can go ahead (§11).

**What this is not.** It is not a quest. A chat stays the person's conversation: it takes no ask, publishes nothing and closes nothing.
Ask Daoris, an intake, a helper, a partner and a second opinion's reviewer are conversations too, and each is left as it is (§9).
A chat in the person's checkout is left as it is too. FABRIC-PIPELINE1d performs the `check` step, which exists only as a graph node
(§4.2). This design adds no executor.

Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, module paths under `src/Daoris.Desktop/Daoris.Desktop.Modules/`,
service paths under `src/Daoris.Service/Daoris.Service.Core/`, page paths under `src/Daoris.Web/src/`.

## 1. Today, read from the code

### 1.1 How a chat's tree is known

- **Chosen at the start.** `ChatRunner.Isolates` is the conversation's own choice, else the repository's standing one:
  `ownTree ?? config.OpensOwnTree(repository)` (`Conversations/Chat/ChatRunner.cs:363-365`; `Platform/DriverConfig.cs:1423-1424`).
- **Grown at the start.** Where it isolates, `StartAsync` opens a tree (`ChatRunner.cs:557-567`): `SessionTrees.OpenAsync` names it
  `s-<8 hex>` on the branch `daoris/s-<8 hex>` (`Trees/SessionTrees/SessionTrees.cs:354-356`), made by `git worktree add --lock -b`
  under a lock naming the opening process (`:369-372`), and **lets that lock go once the tree is open** (`:404`). No session lock is
  taken for a chat: only a driven start takes one (`Loop/Driver.cs:2442`, `Loop/DriverContinuations.cs:304`). The record opens on that
  tree with its `HEAD` as the base commit (`ChatRunner.cs:575-586`, SURF6). A tree that grew is told its process with the first message
  (`:612`, PROCESS1d-ii), which names no checks; a checkout chat is told nothing (`:608-611`).
- **The session's record.** The service's `OpenChatAsync` creates a `Chat` with no quest (`SessionLedger.cs:646-700`, the create at
  `:688-692`; `Sessions.cs:58-61`, `Quest` *null for a chat*). An unstated tree is recorded as the checkout's root (`SessionLedger.cs:669`).
  So *has a tree* is not *has its own*: the page's `ownTree` compares it with the registration (`work/identity.ts:159-180`), and the
  driver asks `SessionTrees.Holds` (`SessionTrees.cs:471`), which is true for any folder under the trees root, a reviewer's clone
  `o-…` included (`Review/OpinionTree.cs:18-20`; `Trees/SessionTrees/TreeContext.cs:67`).
- **Its line.** The service stores each record's line as `entry`, `<origin>/<repository>` (SESSONE1a): a chat's origin is
  `session:<first>`, the first record its `From` links reach (`Sessions/SessionEntries.cs:71-93`; written at the open,
  `SessionLedger.cs:692`), and the wire carries it (`Daoris.Service.Http/ApiContracts.cs:436-439`). A helper and a reviewer keep an
  origin of their own (`SessionEntries.cs:76-77`).
- **What the trace reads.** `Trace.ParseSessions` reads `Kind`, `Tree`, `BaseCommit`, `Ask` and the rest (`Sessions/Trace/Trace.cs:267-286`;
  `Sessions/Domain/TracedRecords.cs:28-80`), and none of `opinion`, `serves`, `piece`, `from` or `entry`, which the service answers this
  machine (`ApiContracts.cs:425-454`). A teammate's record is told by `/` in its id (`TracedRecords.cs:69-70`).
- **A chat that took a quest.** The record keeps `quest` null and the quest in `took_quest` (CHATTAKE1; `Sessions/Sessions.cs:250`), which
  joins that quest to the chat's line (`SessionEntries.cs:102-106`). `/api/sessions` does not answer it to this machine (`ApiContracts.cs`
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
through `SessionLocks.HolderOf`). A driven start keeps its checks for its run and lets them go when the run ends (`Driver.cs:2543-2552`,
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
| Ask Daoris | repository `daoris:help` (`SessionLedger.cs:426`, `IsHelp` `:432`; `TracedRecords.cs:22`), opened by `OpenHelpAsync` (`:576-628`) | its workspace's room, no base commit (`:616-620`) |
| An intake | repository `ask #<id>`, `Ask` set (`SessionLedger.cs:354-418`, `:408`); its view reads its ask's runs (§1.2) | a room |
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

What stays out is unchanged. A chat in the checkout works on the person's own files, and Daoris lands nothing from it. Ask Daoris changes
no repository's code (§9).

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
4. **The gate refuses the person's press with its reason, and asks the second opinion at that press.** A turn's end starts nothing.
   Where a part holds, *Accept…* is refused as a quest's work is today; where the opinion's part holds for want of a pass, the press also
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

- **`chat-<first>`, from the stored line.** The record's `entry` names its origin, `session:<first>` (§1.1). Where the origin is a
  session whose record names the same tree, the key is `chat-<first>`. Otherwise it is `chat-<own id>`: a line that moved to another tree
  (a retry or a move) starts a run there. The service's walk is guarded against cycles, not bounded by a count, and so is this one: it
  reads one stored field and follows no link itself.
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
| `tookQuest` set (CHATTAKE1) | that quest's run: a chat that took a quest is that quest's work, never a chat run |
| `ask` set (an intake) | its ask's runs, as today; `ask-no-quest` before any quest is published |
| `daoris:help` | `ask-daoris` |
| `opinion` set | `reviewer` |
| `piece` set | `partner` |
| `serves` set | `helper` |
| a chat whose tree is gone, never bound | `chat-tree-gone` |
| a chat whose tree `SessionTrees.Holds` does not hold | `chat-checkout` |
| a chat in a tree of its own | its chat run (row c); `chat-own-tree` until row c is built; `chat-graph-later` where its choice is a graph version (§4.3) |

So the trace reads `opinion`, `serves`, `piece`, `from`, `entry` and `tookQuest` into `TracedSession` (row a), and the service answers
`tookQuest` to this machine as it answers `opinion` (an additive field, `ApiContracts.cs`).

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

**Its later steps** are derived as a quest's are, from the gate, the opinion, the landing record and the due list (`WorkflowRun.cs:344-411`).
Until row d, the opinion's step reads `not-reached` with no words of when, since the gate does not ask it yet. Row d's words say *a
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
  (`Trees/Nodes/GraphRuns.cs:59-110`), and the gate's graph part judges by it (`TreeGates.cs:64-77`). Until CHATFLOW1f says what a
  chat's `claimed`, `handed` and `quest.close` subjects are, a choice that gives a graph version binds nothing. The view answers
  `chat-graph-later`, and the gate reads as today: its process is Current's, and its look and opinion parts answer `None` until rows d and
  d2 give a chat run its own. Current and a steps version bind.
- **A bind that fails** costs the conversation nothing: it goes on, and the next turn's end tries again. The bind reads only local files,
  so a failure is a home that cannot be written or a version that cannot be read. It is said once in the conversation: *its repository's
  workflow could not be bound, so it cannot be landed yet: `<why>`*. **A press on an unbound chat run tries the bind first**, and one that
  still cannot be made refuses the press with that reason (`bind-failed`), since falling back to Current could skip what the repository's
  chosen workflow asks of the person (`D157.md:362`, `:393-394`). Only the landing waits.
- **Said.** Under a named workflow the conversation's first Daoris line says which and why (`WorkflowRunBinding.Why`,
  `WorkflowRunBinding.cs:50-60`), as the look says a start's. Under Current nothing is said, as a start's binding says nothing
  (`WorkflowRunStart.cs:197`).
- **One run spans every landing of its tree.** The binding is not cleared at a landing: the run's steps after the landing (§8) read it,
  and a chat that goes on and lands again lands under the same version.
- **Cleared with its records, by the history clear only.** A quest's binding goes with its quest (`D157.md:417-423`; `Sessions/History/HistoryHome.cs:29-31`).
  A chat run's goes with its line's records: the clear that removes the record named by its key removes `chat-<first>.json`, its gate's
  file and its look's file. The leftover rule (`HistoryHome.cs:158-167`) learns that a `chat-<id>` binding belongs to the record `<id>`,
  and removes it only where no record by that id remains.

### 4.4 What the conversation is told of its checks

The view shows a chat run's `checks` from row c. The conversation is told them only once they are enforced on its tree, and until then it
is told nothing of them, as today (§1.4). Enforcement is CHATFLOW1h: when a chat opens in its own tree, Daoris takes the session lock in
its id (`SessionLocks.HoldAsync`) and lets it go at the conversation's end; keeps its checks for the conversation's life rather than per
run; composes `HeavyTold.Hand`'s deny rules into the chat's rules at its spawn (`ChatRunner.cs:1349-1360`); and tells a hand-over's next
record the checks as the first was told. Only then is `HeavyTold.LightWords` said to it.

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

1. **Reads the gate at the tree's tip.** `LAND_SESSION_TREE` reads the chat run's process (§4.2) and every part of `GateAsync` in its fixed
   order, each keyed by the run where a quest's are keyed by the chain. Where every part lets go, it lands at once, as today, and the landing
   record says what each part said.
2. **Refuses with its reason where a part holds**, in the press's existing refusal (`LandingGateWords`, `TreesRoutes.cs:600`), on both doors.
   No press is kept and nothing lands later.
3. **Asks the opinion at that press** where the opinion's part holds as `not-asked`: an `OpinionDue` naming the chat run (§7), as the
   person's *Ask now* writes one (`OpinionPresses.cs:118`), at the rule's `landing` occasion. The refusal says so: *a second opinion is
   asked: `<reviewer>` reads it from the driver's next look; press Accept again once it settles.* The look starts it as every owed pass.
4. **Asks nothing twice.** A press while a pass is `reading` or its findings are `with-session` is refused with that state's words and asks
   nothing. Where the answering turn has committed, the next press asks the one recheck (§6), the tip it reads being the tree's then.
5. **Lands at the press that finds every part settled**: the person's own press, in time, under the rule's form. A merge-form rule merges at
   that press, as it does for a quest's work. Uncommitted work refuses the press, as D164 point 4 has it, so a landing never meets a turn
   writing in the tree: the press is offered only between turns (§1.3).
6. **After a hand-over** the run, its gate and its due are the line's (§4.1), so the next record's press reads the same gate, and nothing
   is closed `superseded`.
7. **A second landing of the same tree** is read as a quest's work after its first pass: by the one recheck where it is unspent, otherwise
   `commits-since`, which the person's press over it answers (the second-agent design §8.3) or their *Ask now*, which is never capped.
   The service's bound (`OpinionDesk.cs:395-406`) bounds only the automatic first pass, and is unchanged.

### 5.3 D154's look on a chat run

A look is set up by a set-up step, a quest the work's done publishes (§1.3). A chat publishes nothing, so no look can be set up for its work
in this build. Where the run's process requires one (the rule's level, or its named workflow's look step, `ReviewGate.Decide`):

- **Its part reads `cannot-start`**, with its limit's words: *a conversation publishes no set-up step, so no look can be set up for its work.*
  The press is refused with that reason.
- **The person's way past it** is *Accept without the look…*, a press of their own with their words, kept at the tip it was pressed at on the
  run's look file, `<home>/workflows/looks/chat-<first>.json` (machine-local, D47 §4, as the opinion gate keeps the person's words). It is
  D154 row 1's skip, re-homed for a run that has no quest. It covers that tip and the commits before it; a later commit asks it again, and the
  landing record says it and its words. No agent and no Ask Daoris card reaches it (D110), as the opinion's *Go on anyway…* is not reached.
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
  The deliverer offers it only where `TurnEnded` is true for the line's newest record (`ChatRunner.cs:1966-1972`), and it never takes
  `NextStep` or `Steered` (`ChatQueue.cs:49-65`), so `ChatTurns.Take` cannot hand it into a turn in flight (`ChatTurns.cs:581-587`). D137's
  door holds.
- **The person's words go first, where they were sent first.** If words the person sent wait when the deliverer acts, theirs are the next
  turn and the send-back goes to the person, kept with the reason `person-went-on`. The driver cannot see a draft: where the person is
  typing when the deliverer acts, the send-back's turn starts, and their word then reaches it as any of their words reach a running turn,
  at its end, or at its next step where they send it sooner (D136). That is accepted and said in the design, not hidden.
- **To the line's newest record.** The findings name the record that pressed. Where a hand-over came after it, they go to the line's newest
  record, which holds the tree (§4.1), never to the ended one.
- **Its answer tool.** A chat run whose process names an opinion carries `opinion_answer` in its rules from its spawn, since a live chat's
  rules are fixed then (`ChatRunner.cs:1349-1360`), as `DriverContinuations.AnswersFindings` hands it to a driven run at its resume.
- **Its commits void nothing.** No press is kept (§5), so the answering turn's commits move the tip and nothing else. The recheck is not
  started at that turn's end, which rule 4 forbids: it is asked at the person's next press, which reads the new tip and counts against
  D155's one recheck (§5.2 point 4). Ancestry decides what an opinion covers (`OpinionGate.JudgeAsync`, through `ReviewGit.HoldsAsync`),
  so a go-on that rewrites the commits (`ChatGoOn.cs:535-541`, LANDSYNC2b) leaves the earlier pass covering nothing that would land.
- **Once.** A step sends back at most once (D157 point 9). What still stands after the recheck goes to the person.
- **Never a new session.** A chat that has ended takes a send-back by D137 point 3, its own conversation resumed, never a new one. A chat
  that cannot resume sends it to the person, as `OpinionDeliverer` does today.

`OpinionDeliverer.HandAsync`'s `{ Kind: "chat" }` row (`:134`) narrows to a chat with no chat run: one in the checkout, or one whose tree
is gone.

## 7. The second opinion on a chat's work

- **When.** It is owed where the run's process names an opinion at the landing (`on` includes `landing`, D155 point 2), and asked at the
  person's press (§5.2). The `steps` occasion has no meaning for a chat, which has no chain. The person's own *Ask now*
  (`OpinionPresses.AskAsync`) is offered on a chat run at any time, as on a quest's: `WorkAsync` reads a chat run instead of refusing it
  (`OpinionPresses.cs:170-173`), and names its gate.
- **Its due.** `OpinionDue` gains `Run`, and `Quest` may be null where `Run` names a chat run. Such a due closes `landed`, `gone` or `off`,
  never `undone`. It carries no tip: the pass reads the tree's `HEAD` at the look (`OpinionGateLook.cs:353`), which between turns is the
  press's tip, and the gate at the next press judges what would land against the tip the pass read and says it.
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

- **Ask Daoris** reads and advises (`ChatRunner.cs:688-691`), in its workspace's room with no base commit, and changes no repository's
  code. It runs no workflow, binds nothing and is gated by nothing new. Its *Workflow* view says why, with a code (§10).
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
| `ask-daoris` | Ask Daoris's record (`daoris:help`) | Ask Daoris advises and changes no repository's code, so no workflow runs for its conversations. | 问道衍只提供建议，不改动任何仓库的代码，所以它的对话不走工作流。 |
| `reviewer` | a reviewer's record | This session reads another's work for a second opinion and changes nothing of its own, so no workflow runs for it. | 这个会话为第二意见阅读另一个会话的工作，自己不做任何改动，所以没有工作流为它运行。 |
| `helper` | a helper's record | This session helps another and changes nothing of its own, so no workflow runs for it: the session it helps has the run. | 这个会话协助另一个会话，自己不做任何改动，所以没有工作流为它运行：它协助的会话有进展。 |
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

Each row ships both doors for its part (D50). Rows a and b are settled and can go ahead; c and the rows after it wait for Codex's read.

- [ ] **CHATFLOW1a — the no-run code and the record's kinds** (service, driver, modules). `/api/sessions` answers `tookQuest` to this
  machine; `TracedSession` reads `opinion`, `serves`, `piece`, `from`, `entry` and `tookQuest`; `ChainsOf` classifies in §4.2's order, a chat
  that took a quest reading its quest's run; `WorkflowRunRead.Code`, the route's `code`, §10's codes with `chat-own-tree` for every chat a
  tree of its own holds, and the terminal saying each code's English. Proof: `WorkflowRunTests`, one row per code and a row each for a
  reviewer's `o-…` clone, a helper and a partner (trees `Holds` would take), an own-tree chat reading `chat-own-tree` and never
  `chat-checkout`, an intake still reading its ask's runs, and a chat that took a quest reading that quest's run; `TraceTests` (the six
  fields); `DriverModuleWorkflowRunTests` (the answer's `code`, `:323-333` extended); the twin table's rows for each code's English.
- [ ] **CHATFLOW1b — the page words it** (web; after a). The view's codes in `workflow.run.json`, en and zh, the driver's sentence for an
  unknown code, and *Try again* on `unanswered`. Proof: `WorkflowRunView.test.tsx` renders each code in en and in zh and an unknown one as
  the sentence (`:285-295` amended): **a chat with no tree says so in both languages** (`chat-checkout`), and an own-tree chat reads
  `chat-own-tree`. Also stories per code, `npm --prefix src/Daoris.Web run i18n:check` and `names:check --strict`.
- [ ] **CHATFLOW1c — a chat's run, keyed and bound** (driver, modules; after a). `chat-<first>` from the stored line (§4.1); the binding
  when the tree opens, never to a graph version (`chat-graph-later`); a failed bind said once, and a press on an unbound run binding first
  and refused `bind-failed` where it cannot; `ChainsOf`'s chat run read by the line, with the work step's states; `TreeGates.ProcessAsync`
  reading the chat run's process; the history clear and the leftover rule. The conversation is told nothing of its checks. Proof:
  `WorkflowRunTests`: **a chat's tree with commits shows its run and its checks** (Current's `sessionChecks`, and a steps version's
  `checks`), its opinion `not-reached` with no words of when, a hand-over's second record reading the same run, a line moved to another
  tree starting a run of its own, and an ended chat with commits reading `land`. `WorkflowRunBindingTests` (bound under the repository's
  default, never by a kind; a graph version bound nothing); `HistoryClearingTests` (a live chat's binding kept by the leftover rule, cleared
  with its record); `DriverModuleWorkflowRunTests` (a chat's `{ session }` answers a run); `ChatRunnerTests` (the bind beside the process
  told, and no checks told).
- [ ] **CHATFLOW1d — the gate refuses the press and asks the opinion** (driver, modules, web; after c). The opinion's part keyed by the
  run (`OpinionGates` under `chat-<first>`, `OpinionGate.JudgeAsync` with a run and no quest); `OpinionDue.Run`; the press asking at
  `not-asked` and nothing twice; `OpinionPresses.WorkAsync` reading a chat run; the refusal's words on both doors; the opinion's step's
  words. Proof: `OpinionLandingTests` rows: a chat's press refused and the opinion asked; pressed again while it reads, refused and nothing
  asked; settled, landed at the press; a merge-form rule merging at the press; uncommitted work refused; a hand-over's next record pressing
  the same gate; a second landing read by the recheck or `commits-since`. `OpinionGateTests` (a chat run's gate, its file by run key),
  `OpinionPacketTests` (the opening named as the opening, no conversation), `TreesWordsTests` (the refusal's words, exit 1), and
  `WorkFrame.test.tsx` (the foot's refusal, en and zh). The new presses' reach: `HelpCoverageTests` exempts them as the person's (D110).
- [ ] **CHATFLOW1d2 — the look on a chat run, and the person's way past it** (driver, modules, web; with d). The look's part `cannot-start`
  for a chat run under a rule or version that requires one; *Accept without the look…*, its file, the page's press and
  `trees land <id> --without-look`, a `Person` route. Proof: `ReviewLandingTests` (refused with the limit's words; the person's press
  lands at that tip; a later commit asks again; the landing record names it), `DriverModuleTreesWordsTests` (the terminal door's words and
  exit code), `PipeCensus`'s census row, `HelpCoverageTests` (exempt), and `WorkFrame.test.tsx` in en and zh.
- [ ] **CHATFLOW1e — its send-back only between turns** (driver; after d). The findings as an `Ordinary` word with `by`, offered only where
  `TurnEnded`, to the line's newest record; `person-went-on`; `opinion_answer` in a chat run's rules from its spawn. Proof:
  `OpinionDeliveryTests`: **its send-back reaches the conversation only between turns**. Findings wait while a turn runs and while words are
  queued, and are taken as a turn of their own once it ends; a word the person sent first goes first and the findings go to the person;
  after a hand-over they reach the newest record. A `ChatTurnsTests` row shows the word never takes `NextStep`; a `ChatRunnerTests` row
  shows the rule handed at spawn. Also an `OpinionResumeTickTests` (`Process`) case on both doors, run by the CI.
- [ ] **CHATFLOW1f — a graph-bound chat run** (driver; after d, e, FABRIC-CUTOVER1e). The bind of a graph version; the press reaching
  `claimed` at its tip and `handed` landing at that press; its `check` nodes and their send-back. Proof: `WorkflowGraphRunTests` and
  `GraphRunLandingTests` rows for a `run:chat-<first>` unit, and `chat-graph-later` no longer answered.
- [ ] **CHATFLOW1g — on the install** (the parent; after a republish carrying a to e and d2). A chat in its own tree, under a workflow
  naming an opinion, refused at its first press with the opinion asked, read by another maker, answered between turns, and landed by the
  person's next press. A checkout chat and Ask Daoris say why they run none. Both languages at 1546 and 680 px. Proof: dated shots and a
  ledger under D157.
- [ ] **CHATFLOW1h — a chat's checks enforced, then told** (driver; after c). The session lock in the chat's id for its life, its checks
  kept for its life, `HeavyTold.Hand`'s deny rules in its spawn's rules, a hand-over's next record told, and only then the process told
  with its checks (§4.4). Proof: a `HeavyRunCommandTests` row refusing `run-heavy` on a `light` chat's tree; `ChatRunnerTests` (the rules
  and the words); a `HandOverProcessTests` row (the next record told).
- [ ] **CHATFLOW1i — a chat that took a quest is gated by that quest** (driver, modules; after a). `LAND_SESSION_TREE` and
  `OpinionPresses.WorkAsync` read a record's `tookQuest` where its `quest` is null, so a CHATTAKE1 chat's press is gated by its taken
  quest's chain, as its view already reads that quest's run. Proof: `OpinionLandingTests` and `ReviewLandingTests` rows for a chat that
  took a quest, and a chat that took none unchanged.

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
- **A per-landing first pass in the service**: the recheck and the person's *Ask now* already read a second landing (§5.2 point 7), and no
  row then needs the service's bound changed.
- **A workflow for Ask Daoris or a checkout chat**: neither changes code Daoris lands (§9).

## 13. What this amends, once accepted

- **D157.** The workflow design §12.2's *a conversation's work under a workflow* is lifted for a chat in its own tree. Point 11's run gains
  a second key, `chat-<first>` from the record's stored line, bound when the tree opens and read by the line with no chain; a graph version
  is not bound until CHATFLOW1f, and the binding is cleared only by the history clear, with its records. Point 10's choice has no task level
  or kind for it, and a press on a run that cannot be bound is refused. Point 2's gate refuses a chat's press with its reason and asks the
  opinion at that press. Point 9's send-back reaches a chat only between turns. D157's amendment of D135 (`D157.md:106-107`): a chat run's
  go-ahead waits for WORKFLOW1l's. The *Workflow* view's no-run answer carries a code, said in English on both doors.
- **D155.** The second-agent design §12.2's *opinions on a conversation's work* is lifted for a chat run's landing. Point 2's `landing`
  occasion is asked at the person's press. Point 6's packet takes the conversation's opening, named as such, and the commits' messages as
  the claim. Point 7's findings go to a chat once, between turns, as an ordinary word carrying `by`, and to the person where the person
  spoke first; the recheck is asked at the person's next press. The gate's answers and the due are kept by the run's key.
- **D154**, by reference: a look that cannot be set up for a chat run reads `cannot-start`, and its row 1 skip is re-homed as the person's
  *Accept without the look…* on the run.
- **Standing.** D137 points 2 and 3 (the door), D136, D145 point 1, D154 point 8, D155 points 4, 6 and 8, D157 points 9 and 10, D164
  (Accept names the branch), D47 §4, D50, D110, D37.

## 14. What the checks do not cover

Documents only, and nothing is built. Each statement in §1 was read at `ca046e7fe`. The other sections' citations name what the change
reads or narrows, and were checked to exist there. No chat was run on the install. Whether an agent in a chat calls `opinion_answer`
under the handed rule is unmeasured, as it was for a driven one (XAGENT1j). The `check` step's runtime is FABRIC-PIPELINE1d's, reached by
a chat only through CHATFLOW1f. The zh words follow the glossary, and no translator read them. `verify` checks the router's row, the decisions' notes and the links here, and none of the words.

## 15. Round 2: the read's findings and their answers

Every finding of the Opus foundation read of round 1 (`docs/2026-10-24-chat-flow-reads.md` §4), each checked against the code on main at
`ca046e7fe`. *Taken* means the design now says it; *answered* means it is not taken, for the reason given; *deferred* means a later row
carries it. The read's §1 to §3 corrections are findings below, and the rest are taken where they are named.

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
