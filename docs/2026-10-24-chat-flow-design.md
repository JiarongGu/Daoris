# A conversation's work in its own tree runs its repository's workflow: a run keyed by the session, its steps started by the person's Land, its send-back only between turns, and a no-run answer the page words in both languages (proposed, round 1, 2026-10-24)

**Standing:** proposed, round 1, a note amending D157 (the workflow) and D155 (the second opinion) for CHATFLOW1, under D191:
two reads follow before anything is built. Written by Claude Opus 5.5 from the code on main at `8cc3936b0`. Nothing here is built.

The row CHATFLOW1 in `TASKS.md` carries the case. A chat working in a tree of its own serves no quest, so the session's *Workflow*
view shows only the driver's English sentence, in zh too, and its work reaches the review list with none of its repository's
workflow: no checks, no second opinion and no landing gate. The workflow design left *a conversation's work under a workflow* out,
not rejected (`docs/2026-10-09-workflow-design.md` §12.2), and the second-agent design did the same for *opinions on a conversation's
work* (`docs/2026-10-08-second-agent-design.md` §12.2). §2 says what changed since and why it now belongs in.

**What this is not.** It is not a quest. A chat stays the person's conversation: it takes no ask, publishes nothing and closes nothing.
Ask Daoris, an intake, a helper, a partner and a second opinion's reviewer are conversations too, and each is left as it is (§9).
A chat in the person's checkout is left as it is too. FABRIC-PIPELINE1d performs the `check` step. This design says where a chat's
run reaches that step and what the step reads, and adds no executor.

Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, module paths under `src/Daoris.Desktop/Daoris.Desktop.Modules/`,
service paths under `src/Daoris.Service/Daoris.Service.Core/`, page paths under `src/Daoris.Web/src/`.

## 1. Today, read from the code

### 1.1 How a chat's tree is known

- **Chosen at the start.** `ChatRunner.Isolates` is the conversation's own choice, else the repository's standing one:
  `ownTree ?? config.OpensOwnTree(repository)` (`Conversations/Chat/ChatRunner.cs:363-365`; `Platform/DriverConfig.cs:1423-1424`).
- **Grown at the start.** Where it isolates, `StartAsync` opens a tree (`ChatRunner.cs:557-567`): `SessionTrees.OpenAsync` names it
  `s-<8 hex>` on the branch `daoris/s-<8 hex>` (`Trees/SessionTrees/SessionTrees.cs:354-356`), made by `git worktree add --lock -b`
  (`:371-372`). The record opens on that tree with its `HEAD` as the base commit (`ChatRunner.cs:575-586`, SURF6). A tree that grew is
  told its process with the first message (`:612`, PROCESS1d-ii); a checkout chat is told nothing (`:608-611`).
- **The session's record.** The service's `OpenChatAsync` creates a `Chat` with no quest (`SessionLedger.cs:646-700`, the create at
  `:688-692`; `Sessions.cs:58-61`, `Quest` *null for a chat*). An unstated tree is recorded as the checkout's root (`SessionLedger.cs:669`).
  So *has a tree* is not *has its own*: the page's `ownTree` compares it with the registration (`work/identity.ts:159-180`), and the
  driver asks `SessionTrees.Holds` (`SessionTrees.cs:471`). The driver's trace reads `Kind` (`driven` or `chat`), `Tree` and
  `BaseCommit` (`Sessions/Domain/TracedRecords.cs:28-42`).
- **Its branch.** The record keeps no branch. It is `daoris/` plus the tree's folder name (`Trees/SessionTrees/TreeFollows.cs:19`,
  `Trees/SessionGroups.cs:291`), or the branch the agent moved its tree to (TREEFOLLOW1, `SessionGroups.cs:254`).
- **A hand-over keeps the tree.** `ChatHandOver` opens the new record on `prior.Tree` (`Conversations/Chat/ChatHandOver.cs:157`). The
  new record links to the old one by `From` (`Sessions.cs:814-816`, MIXAGENT1a-iii).

### 1.2 The run asked for a chat

- The page asks `WORKFLOW_RUN` with `{ session }` while the *Workflow* view shows (`work/WorkFrame.tsx:1118-1123`).
- `WorkflowRunReader.ChainsOf` turns a session into its quest or its ask. A chat has neither, so it answers *session `<id>` serves no quest,
  so no workflow runs for it.* (`Workflows/WorkflowRun.Read.cs:80-87`). A run's id is its chain's first quest in the repository
  (`Workflows/WorkflowRunBindings.cs:52-53`; `Workflows/Domain/WorkflowRunBinding.cs:9`).
- The route answers `{ runs, problem }` and no code (`Bridge/Routes/WorkflowRoutes.cs:222-243`, the shape at `:242`). Its comment
  says so: *What names nothing that runs (a chat, a quest not here) is answered as no run with the driver's sentence* (`:217-221`).
- The page shows `problem` as written, and `workflow.run.none` only when no sentence came
  (`workflow/WorkflowRunView.tsx:301-304`). A person reading in 中文 reads the driver's English. The terminal prints the same sentence
  (`Workflows/WorkflowRunCommand.cs:72`).
- The no-run sentences today: no session recorded (`WorkflowRun.Read.cs:84`), the chat's (`:87`), no quest here (`:94`), nothing named
  (`:119`), and the service not answering (`:53-56`, `Unanswered`, which the route does not send). An ask with no quest yet gives no
  sentence, and the page's fallback says *no quest of this work is published* (`locales/en/workflow.run.json:148`).

### 1.3 The gate a chat's Accept passes

- **Where it is offered.** A live chat between turns with commits beyond the line is in the review list with `Lands`
  (`Trees/SessionGroups.cs:898-908`, `:1100-1114`; `LandOffer.Of`, `:287-292`). The conversation's foot offers the same once the driver
  says its turn ended (`work/waits/waits.ts:81-96`, ACCEPTFOOT1, D164's note).
- **The press.** `LAND_SESSION_TREE` reads the session's quest, which is null for a chat (`Bridge/Routes/TreesRoutes.cs:532`), and asks
  the gate with it (`:551`).
- **The gate reads the rules live, and nothing holds.** `TreeGates.ProcessAsync` reads Current for a quest-less tree and never a binding
  (`Trees/SessionTrees/TreeGates.cs:84-92`). D154's look answers `None` for no quest (`Review/Domain/ReviewGate.cs:442-443`, `:459`). The
  opinion's part answers `None` (`Review/Domain/OpinionGate.cs:344-345`), and the graph's part reads only a bound graph run
  (`TreeGates.cs:66`). So the repository's look, its second opinion and its run's own gate never hold a chat's work.
- **No opinion is owed or asked.** An opinion is owed when a driven record concludes `Done` (`Loop/OpinionLook.cs:19-30`). Its due names a
  quest (`Review/Domain/OpinionDue.cs:10-11`), and its gate's answers are kept per chain (`TreeGates.cs:184-185`; `Review/OpinionGates.cs:21`).
  The person's own presses refuse a chat with the sentence the row quotes (`Review/OpinionPresses.cs:170-173`).
- **No go-ahead can be asked.** A go-ahead lives on an ask, and a chat's session is refused: *is a conversation on no ask*
  (`SessionGoAheads.cs:75-84`).

### 1.4 Words into a live conversation

- **A chat's words.** `ChatTurns.Take` lets an ordinary word into a turn in flight never. Only an urgent one reaches the running turn at its
  next step, and only on a door that steers. Otherwise the word waits for the turn's end, or is the next turn at once
  (`Conversations/Chat/ChatTurns.cs:574-608`; the reaches `Now`, `NextStep`, `TurnEnd`, `Steered`, `Conversations/Domain/ChatQueue.cs:49-65`).
  This is D137 point 2's door.
- **Between turns.** `ChatRunner.TurnEnded` is true when no turn is taking or opening, nothing waits, and one has ended
  (`ChatRunner.cs:1966-1972`). The record stays `working` between turns (D164's ACCEPTFOOT1 note).
- **Another agent's findings never reach a chat.** `OpinionDeliverer.HandAsync` sends a chat's to the person (`Loop/OpinionDeliverer.cs:127-137`,
  `{ Kind: "chat" } => OpinionToPerson.Conversation`; the reason's words at `Review/Domain/OpinionDelivery.cs:119`, `:129`). A running or
  parked record waits, *never into a step in flight* (`OpinionDeliverer.cs:140-145`).
- **No send-back runs yet.** `failed: send-back` is a step's field (`Workflows/Domain/WorkflowNamed.cs:58`), and a graph decodes it as a
  `Human` node, *the adapter's one return, then the person's task* (`Trees/Nodes/WorkflowGraphs.cs:24`, `:82-95`). The one thing that
  sends work back today is the opinion's delivery above.

### 1.5 Conversations that are not a repository's chat

Each is `Kind = Chat` with no quest (`Sessions.cs:44-61`):

| Conversation | How it is told apart | Its folder |
|---|---|---|
| Ask Daoris | repository `daoris:help` (`SessionLedger.cs:426`, `IsHelp` `:432`; `TracedRecords.cs:22`), opened by `OpenHelpAsync` (`:576-628`) | its workspace's room, no base commit (`:616-620`) |
| An intake | repository `ask #<id>`, `Ask` set (`SessionLedger.cs:354-418`, `:408`) | a room |
| A reviewer | `Opinion` set (`Sessions.cs:148`) | a clone of its own, `o-<8 hex>` (`Review/OpinionTree.cs:6-25`) |
| A helper, a partner | `Serves`, `Piece` set (`Sessions.cs:158`, `:165`) | its lead's |
| A teammate's record | its id holds `/` (`TracedRecords.cs:63-64`) | another machine |

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
   tree, base, branch, commits, landing record and review exist on the same record and the same files as a quest session's (§1.1, §1.3).
   What a chat lacks is a key, which §4 gives.
4. **The person asked for it.** The row's direction is that a conversation's work in its own tree runs its repository's workflow.

What stays out is unchanged. A chat in the checkout works on the person's own files, and Daoris lands nothing from it. Ask Daoris changes
no repository's code (§9).

## 3. The rule

1. **A chat in a tree of its own runs its repository's workflow.** It is a run like a quest's: one repository's work, bound once,
   read from its records, drawn in the *Workflow* view. It is gated at the landing by the same `GateAsync` in the same fixed order
   (D155 §7), with every step in its version. A chat in the checkout, Ask Daoris, an intake, a reviewer, a helper, a partner and a
   teammate's record run none, and the view says which in the person's language (§9, §10).
2. **The run is keyed by the session, where no quest is.** Its id is `chat-<session>`, the first record of the conversation's line in that
   tree, so a hand-over keeps one run (§4).
3. **It binds when its tree opens.** It is bound to the workflow §4.1 of the workflow design chooses with no task level and no kind:
   the repository's, then the workspace's, then Current. A bind that fails holds nothing, since the conversation is the person's: the
   run reads Current live and says so once (§4.3).
4. **Its steps start at the person's Land, never at a turn's end alone.** A turn that ends with commits refreshes what the run shows: the
   candidate, the checks the workflow names and the steps it would take. Nothing is spent. The person's *Accept…* then names the tip,
   starts every step before the landing, and lands once they let it go, unless the person went on working first (§5).
5. **A send-back reaches the conversation only between turns.** It goes at the turn's end, as its own turn, and never into a turn in flight,
   at a next step or by *Send now*. If the person speaks first, their words go first and the send-back goes to the person instead (§6).
6. **A second opinion reads the chat's candidate, not its conversation.** That is from where its branch leaves the line to the tip the
   person's Land named, with the person's opening words as what was asked. Its findings are handed once to the chat, between turns, and kept
   with the run's gate (§7).
7. **What stays the person's press**: the Land itself, D154's *Reviewed*, an answer to a dispute, a stage's start where its step says
   `you`, and every production act and go-ahead (§8).
8. **No run is answered with a code** the page words in en and zh, with the driver's sentence kept for a code it does not know (§10).

## 4. The run's key and its binding

### 4.1 The key

- **`chat-<session>`.** A quest's id is 12 hex digits and a session's 8 (`Sessions.cs:799`). A prefix keeps the two apart by their
  spelling, so neither depends on a length, and `chat-1a2b3c4d` passes `WorkflowRunBindings.IsRunId` (`WorkflowRunBindings.cs:41-42`,
  `[A-Za-z0-9_-]{1,64}`). A teammate's id holds `/` and never passes, which keeps a run machine-local, as D47 §4 has it.
- **The line's first record.** A hand-over and a limit's go-on open a new record on the same tree with `From` (§1.1). The key follows
  `From` back to the first record whose tree is the same, at most 16 links, as the partner's door bounds its line (PARTNERLINE1). So one
  tree's work is one run, and its binding, its node record (`run:chat-<session>`, `Trees/Nodes/GraphRuns.cs:7-14`) and its gate's file
  (`<home>/opinions/gates/chat-<session>.json`) stay one. The trace needs `From`. Today `TracedSession` reads no `From`
  (`TracedRecords.cs:22-80`), and CHATFLOW1a adds it.
- **A run is one tree's.** A chat whose tree was discarded and that opened another (a second *New conversation*) is a new run.

### 4.2 What the run reads

`WorkflowRunReader.ChainsOf` gains one branch before the sentence at `:87`. A chat whose record names a tree `SessionTrees.Holds`, and
which is none of §1.5's, is a **chat run**. It has no chain: the run's quests are empty, its `session` is the line's newest record, and its
`repository` is the tree's owner. `RunAsync` reads it as it reads a quest's (`WorkflowRun.Read.cs:131-189`) with three changes:

- **Its process** is `WorkflowProcesses.Read(home, config, repository, workspace, "chat-<session>")`, the binding where one is kept
  (`WorkflowProcesses.cs:33-45`). `TreeGates.ProcessAsync` reads the same for a chat's tree, in place of Current live (`TreeGates.cs:84-92`),
  so the view and the gate decide by one process, as WORKFLOW1f has it.
- **Its work step** is the conversation's, read from the record and the runner's word, with no quest's states:
  - `working`, `agent` while a turn runs or words wait;
  - `waiting-on-you`, `talk` between turns with nothing beyond the line;
  - `waiting-on-you`, `land` between turns with commits beyond the line (the review's `offer`, LAND4);
  - `done` once the person's Land is kept for a tip (§5), and back to `waiting-on-you` if the tree moved past it;
  - `stopped` once the conversation is ended with nothing beyond the line.
- **Its later steps** are derived as a quest's are, from the gate, the opinion, the landing record and the due list (`WorkflowRun.cs:344-411`).
  A graph-bound run's are read from its node record, as `GraphRunAsync` reads one (`WorkflowRun.Read.cs:197-223`). Before the person's
  Land they read `not-reached`, and each says what it will do: *a second opinion from `<reviewer>` once you land it*, *the check
  `<gate>` once you land it*.

The proof of the row's first point is this view. A chat's tree with commits shows its run, Current's or its bound workflow's steps, and
**its checks** in two places:

- **The checks its session runs**: the run's `checks` (`light` or `heavy-at-end`), Current's `sessionChecks` or its version's field
  (`Sessions/Heavy/SessionChecks.cs:3-24`).
- **The `check` steps its workflow names.** Current draws none. In a steps version, a step this build cannot run reads `cannot-start`
  with its limit's words (`WorkflowRun.cs:396-401`; *checks remain outside this runtime*, `:329`). In a graph version, the node reads its
  record's state (`WorkflowGraphRun.cs:270-285`), which FABRIC-PIPELINE1d's executor moves. Until that row is built a check is drawn and
  never performed, and it never reads `done`.

### 4.3 The binding

- **When.** A start's run binds after its tree opens and before its take (D157's FABRIC-CUTOVER1 third-round note). A chat has no take, so
  its run binds when `StartAsync` has its record (`ChatRunner.cs:585-592`), beside where PROCESS1d-ii tells it its process. A chat opened
  before this build binds at its first turn's end, read by the same code.
- **What chooses.** `WorkflowRunBindings.Plan` with no ask and no task choice. No kind is set, since a chat has no ask to carry one, so the
  workflow design §4.1's rows from *the repository's default* down choose. A chat cannot choose a workflow that asks less of the person:
  no agent sets a chat's workflow (D157 point 10).
- **Its session's checks.** Once bound, the chat's process told at the tree's open carries the run's `checks`, as a driven start's words
  do (`SessionChecks.Of` and `Hold`, `Loop/Driver.cs:2543-2552`). So a chat under a `light` pipeline is refused `run-heavy` from its
  tree, as a driven session is (FABRIC-PIPELINE1b).
- **A bind that fails** costs the conversation nothing. A start's would hold (`WorkflowRunStart.cs:140-141`). A chat's says once in the
  conversation, *its repository's workflow could not be bound, so its work follows Current, read live*, and the run reads Current. The
  next turn's end tries again. The person is never stopped from talking.
- **Said.** Under a named workflow the conversation's first Daoris line says which and why (`WorkflowRunBinding.Why`,
  `WorkflowRunBinding.cs:50-60`), as the look says a start's. Under Current nothing is said, as a start's binding says nothing
  (`WorkflowRunStart.cs:197`).
- **Cleared** with its work, as a quest's run is (D153): when its tree is discarded with nothing landed, or its landing's record is kept.

## 5. When its steps start: the person's Land

**The reading taken.** Of the three the row offers (a turn ending with commits, the person's Land, or both), the steps start at the
person's Land. A turn's end only refreshes what the run shows. Three reasons, each from the code:

- **A turn's tip is not a candidate.** A second opinion is bound to a base and a tip (D155 point 1) and is rechecked at most once (D155
  point 7). A chat's tip moves every turn the person keeps talking, so a pass per turn would read work the person has not finished. It
  would spend another maker's account each time, and use up the recheck on a draft.
- **The checks are long.** A declared gate on this repository takes hours (the standing test rule this machine hands every session). A
  `check` step per turn would queue gates behind every reply.
- **The person already says when.** *Accept…* is the press that says *this is the work*, and the look of a quest's work comes after its
  done (D154). For a chat that press is the only moment the work is declared finished.

**What the Land does.**

1. **Names the tip.** The press is `LAND_SESSION_TREE` as today, and the gate reads the chat's run (§4.2). Where every step before the
   landing already lets go (none is named, or each settled at this tip), it lands at once, as today.
2. **Kept, not refused, where a step holds.** Today a holding gate refuses the press (`opinion`, `unreviewed`). For a chat the press is
   kept on the due list (`AutoLandings()`, the landing step's list, `WorkflowRunRecords.cs:14`) as the person's own landing, bound to the
   tip it named. It is never marked automatic, and the steps it needs start: the opinion asked at the next look (an `OpinionDue` naming
   the chat run in place of a quest, `OpinionDue.cs:10-11`), the `check` steps handed to their executor, and D154's look waiting for
   *Reviewed*.
3. **Lands when they let it go**, at the tip the person named (FABRIC-CLOSE1h16's pinned `tip`, `TreeGates.cs:34-41`), by
   `AutoLander`'s path, which judges again. The landing record says the person pressed it and when, and what each step said.
4. **Voided if the person goes on working.** A commit after the named tip leaves the entry `moved`, a new `AutoLandingCode`: *you went
   on working after you landed it, so it waits for your Land again.* The steps' results stay on the run, bound to the tip they read. A
   recheck counts the new commits as D155's does.
5. **Held for the person** where a step ends anywhere but settled: a `must` disputed (D155 point 8), a check failed after its one
   send-back, a look sent back, or a step that cannot start where it is required. The entry waits under *What needs you* and the
   conversation's foot with the reason's code, and lands nothing until the person presses again.
6. **Withdrawn** by the person at any time from the same foot (*Don't land it*). A withdrawn press lands nothing, and the steps already
   started finish and stay on the run.

Under a graph version, the person's Land plays the part a quest's `ready` plays. It reaches the run's `claimed` node at that tip, and the
runtime goes on as for a quest (FABRIC-CUTOVER1e). The `handed` node's landing is the due entry above. A graph-bound chat run is its own
row (CHATFLOW1f), after the steps run is proven.

## 6. A send-back into a live conversation

A send-back is a step's return of the work to its agent: a second opinion's findings (D155 point 7) or a failed check's one send-back
(D157 point 9, FABRIC-PIPELINE1f). For a chat:

- **Only between turns, as a turn of its own.** It is offered to the chat's turns only where `TurnEnded` is true (`ChatRunner.cs:1966-1972`),
  through `ChatRunner.Tell`'s path, a Daoris word marked as the step's (`ChatRunner.cs:1819-1830`). It never takes `NextStep` or
  `Steered` (`ChatQueue.cs:49-65`). It is always ordinary in D188's terms, whatever the step's level, so `ChatTurns.Take` cannot hand it into a turn in
  flight (`ChatTurns.cs:581-587`). D137's door holds, and the conversation shows it as *Another agent's findings*, never as the person's,
  with `by` as D155 point 7 has it.
- **Only after the person's Land.** The steps start only then (§5), so a send-back can only follow the person's own word that the work
  was done.
- **The person's words go first.** If the person sends words while a send-back waits, theirs are the next turn. The send-back is not
  queued behind them: the person chose to go on, so it goes to the person, kept with the reason `person-went-on`, and the due entry is
  `moved` once that turn commits. A send-back is never handed to a conversation that has moved past it.
- **Once.** A step sends back at most once (D157 point 9). Its answers are read as that turn ends: `opinion_answer` is handed to a chat
  carrying findings, as `DriverContinuations.AnswersFindings` hands it to a driven run (`Loop/DriverContinuations.cs:420`, `:555`). Then the
  one recheck, or the check's next attempt, reads the commits that turn made, and anything still standing goes to the person.
- **Never a new session.** A chat that has ended takes a send-back by D137 point 3, its own conversation resumed, never a new one. A chat
  that cannot resume sends it to the person, as `OpinionDeliverer` does today.

`OpinionDeliverer.HandAsync`'s `{ Kind: "chat" }` row (`:134`) narrows to a chat with no chat run: one in the checkout, or one whose
tree is gone.

## 7. The second opinion on a chat's work

- **When.** It is owed where the run's process names an opinion at the landing (`on` includes `landing`, D155 point 2), at the person's Land
  (§5). The `steps` occasion has no meaning for a chat, which has no chain. The person's own ask (`OpinionPresses.AskAsync`) is offered on a
  chat run at any time, as on a quest's: `WorkAsync` no longer refuses it (`OpinionPresses.cs:170-173`), and names the chat run's gate.
- **What it reads.** The packet is the second-agent design's §4, with three parts read from a chat's record:
  - the **candidate** is from the fork point with the line (`OpinionGateLook.cs:357-359`, `ForkPointAsync`) to the tip the Land named,
    read in its own clone as every pass is (`Review/OpinionPackets.cs:87-128`);
  - **what was asked** is the conversation's opening, the person's own first words, which D164's ACCEPTFOOT2 note already takes as a
    conversation's identity, marked as the person's;
  - **what the working session claims** is its commits' messages. A chat has no closing note, and nothing stands in for one.
- **Not the conversation.** D155 point 6 stands: the reviewer never reads the worker's conversation, so it does not anchor on its reasoning.
  The person's later words in the chat are not handed either, since they would be the conversation by another name.
- **Who reads.** The rule's `reviewers`, walked outside the working families (D155 point 4). The working family is the chat's agent's,
  and a hand-over's adds the next agent's.
- **Where its findings go.**
  - The opinion is kept as every one is, on the local host (`/api/opinions`) and in `<home>/opinions/<id>/`.
  - The gate's answers go to `<home>/opinions/gates/chat-<session>.json`, the chat run's key in place of a chain's (`TreeGates.cs:184-185`).
  - They are shown in the Review's opinion part and the run's opinion step, and handed once to the chat between turns (§6).
  - Disputes are the person's: the press that comes next answers them (D155 point 8), and the kept Land does not, since it was made
    before them (§5 point 5).

## 8. What stays the person's press

- **The landing.** A chat's work lands only by the person's *Accept…* (§5), kept or at once. *Accept automatically* says nothing for a
  chat, as PROCESS1d-ii already has it: a conversation has no quest whose done lands it. A workflow's landing step with `autoAccept`
  makes no chat's work land by itself.
- **D154's look**, *Reviewed*, where the run's process requires it. The Land is not a review (D154 point 8: the verdict is the person's
  alone, and its own press).
- **A dispute, a failed check after its send-back, a look sent back**: each waits for the person's next press.
- **After the landing**, the run's pull request, checks and stages go as a quest's run's do. A stage whose `start` is `you` waits for the
  person's press (`WorkflowNamed.cs:94-99`), and **production is only ever the person's press** (D157 point 9, D37).
- **A go-ahead the run raises.** A chat has no ask, and a go-ahead on a session is refused there (`SessionGoAheads.cs:75-84`). D157 lets a
  run raise a go-ahead *on its ask or on the run*. A chat run's sits on the run alone, and is answered on the Workflow view and under
  *What needs you* by the person. No agent, Ask Daoris included, answers one (`Help/Room/HelpRoomDoors.cs:377-380`).

## 9. Unchanged: Ask Daoris's conversations, and chats with no tree

- **Ask Daoris** reads and advises (`ChatRunner.cs:688-691`), in its workspace's room with no base commit, and changes no repository's
  code. It runs no workflow, binds nothing and is gated by nothing new. An intake is the same. Their *Workflow* view says why, with a
  code (§10).
- **A chat in the person's checkout** runs no workflow: its changes are the person's own files, the checkout is not a tree Daoris made, and
  Daoris lands nothing from it, as today. Its view says so in both languages and names the press that would give it a run: a new
  conversation in a tree of its own.
- **A reviewer, a helper, a partner** read for another session and change nothing of their own: no run.
- **A teammate's chat** ran on another machine, which keeps its run: no run here.

## 10. The no-run answer and its code

`WorkflowRunRead` gains `Code`, and `WORKFLOW_RUN` answers `{ runs, problem, code }` (`WorkflowRoutes.cs:242`). The page words a code
it knows and shows the driver's sentence for one it does not, as `NodeWord.tsx:8-16` does for a node's refusal (D182). The terminal
prints the sentence, as today (`WorkflowRunCommand.cs:72`). `workflow.run.none` stays for an answer with no code.

| Code | When | en | zh |
|---|---|---|---|
| `no-session` | no record by that id (`:84`) | No session `{{id}}` is recorded here. | 这里没有记录会话 `{{id}}`。 |
| `no-quest` | no quest by that id (`:94`) | No quest `#{{id}}` is here. | 这里没有委托 `#{{id}}`。 |
| `ask-no-quest` | an ask whose chains are empty (`:101-116`) | Nothing runs here yet: ask `#{{id}}` has published no quest. | 这里还没有进展：需求 `#{{id}}` 还没有发布委托。 |
| `chat-checkout` | a chat whose tree is the checkout | This chat works in your checkout, not in a tree of its own, so its work runs no workflow: what it changes is yours to commit. To give work a workflow, start a conversation in a tree of its own. | 这个聊天在你的检出中工作，没有自己的工作树，所以它的工作不走工作流：它改动的内容由你自己提交。想让工作走工作流，请在自己的工作树中开始一个对话。 |
| `chat-tree-gone` | a chat whose tree is gone, never bound | This chat's tree is gone, and its work was never bound to a workflow, so there is no run to show. | 这个聊天的工作树已经不在了，它的工作也从未绑定工作流，所以没有进展可显示。 |
| `ask-daoris` | Ask Daoris's record (`daoris:help`) | Ask Daoris advises and changes no repository's code, so no workflow runs for its conversations. | 问道衍只提供建议，不改动任何仓库的代码，所以它的对话不走工作流。 |
| `intake` | an intake's record | This conversation turns an ask into quests; each quest's work runs its own workflow. | 这个对话把需求整理成委托；每个委托的工作各自走自己的工作流。 |
| `reads-for-another` | a reviewer, a helper or a partner | This session reads for another and changes nothing of its own, so no workflow runs for it. | 这个会话为另一个会话阅读，自己不做任何改动，所以没有工作流为它运行。 |
| `teammate` | a teammate's record | This session ran on a teammate's machine, which keeps its run. | 这个会话在队友的机器上运行，它的进展保存在那里。 |
| `unanswered` | the service did not answer (`:53-56`) | The service did not answer, so where this work stands could not be read. Try again. | 服务没有回应，读不到这项工作的进展。请重试。 |

- The words use the glossary's: 委托 for quest, 需求 for ask, 重试 for try again, 聊天 for chat, 工作树 for tree, 检出 for checkout,
  工作流 for workflow, 进展 for run, 问道衍 for Ask Daoris (`locales/glossary.json:484-652`).
- `unanswered` draws a *Try again* (重试) press that reads again. The view already reads again at every tick under the driver's key
  (`WorkFrame.tsx:1118-1123`), and the press saves the person the wait.
- The chat's sentence at `:87` keeps its words for the terminal, with `chat-checkout` its code.
- Bounds: `{{id}}` is the record's own id, drawn in code as `Inline` draws it. No path is ever named, since the page is told none
  (D48 §7, `work/identity.ts:170-172`).

## 11. The rows, in build order, with their proofs

Each row ships both doors for its part (D50). None adds an Ask Daoris door: its room names the workflow's places already (WORKFLOW1c4),
and §9 keeps it out of the runs.

- [ ] **CHATFLOW1a — the no-run code** (driver, modules). `WorkflowRunRead.Code`, the route's `code` and §10's table's cases, the
  terminal's sentences unchanged. Proof: `WorkflowRunTests` (one row per code, the chat's sentence kept) and
  `DriverModuleWorkflowRunTests` (the answer's `code`, `:323-333` extended).
- [ ] **CHATFLOW1b — the page words it** (web; after a). The view's codes in `workflow.run.json`, en and zh, from the glossary's words,
  the driver's sentence for an unknown code, and *Try again* on `unanswered`. Proof: `WorkflowRunView.test.tsx` renders each code in en
  and in zh and an unknown one as the sentence (`:285-295` amended). **A chat with no tree says so in both languages**: `chat-checkout`
  in en and zh. Also stories per code, `npm --prefix src/Daoris.Web run i18n:check` and `names:check --strict`.
- [ ] **CHATFLOW1c — a chat's run, keyed and bound** (driver, modules; after a). `chat-<session>` by the line's first record
  (`TracedSession.From`), the binding when the tree opens, a failed bind said once, and `ChainsOf`'s chat branch with the work step's
  states (§4.2), and the process told carrying the run's `checks`. `TreeGates.ProcessAsync` reads the chat run's process. Proof:
  `WorkflowRunTests`: **a chat's tree with commits shows its run and its checks**. Under Current, a rule naming an opinion draws it as
  `not-reached` with what it will do, beside its `sessionChecks`. A named steps version naming a `check` draws it `cannot-start` with its
  limit's words. A hand-over's second record reads the same run. Also `WorkflowRunBindingTests` (a chat bound under the repository's
  default, never by a kind), `DriverModuleWorkflowRunTests` (a chat's `{ session }` answers a run), and `ChatRunnerTests` (the bind
  beside the process told, with its checks).
- [ ] **CHATFLOW1d — the person's Land starts its steps** (driver, modules, web; after c). The kept Land on the due list, the
  `OpinionDue` naming a chat run, `OpinionPresses.WorkAsync` reading one, the landing at the named tip, `moved`, *Don't land it*, and
  the held reasons' codes on the foot and *What needs you*, in en and zh. Proof: `AutoLandingPressTests` and `AutoLandingWorkflowTests`
  rows (kept, landed when settled, `moved` by a later commit, withdrawn), `OpinionGateTests` (a chat run's gate, its file by run key), `OpinionPacketTests` (the opening
  as what was asked, no conversation), and `WorkFrame.test.tsx` (the foot's kept and held states, en and zh).
- [ ] **CHATFLOW1e — its send-back only between turns** (driver; after d). `OpinionDeliverer` hands a chat run's findings by `Tell`'s
  path once `TurnEnded`, never `NextStep`, `Steered` or urgent. The person's words first, then `person-went-on`. `opinion_answer` is
  handed to the turn. Proof: `OpinionDeliveryTests`: **its send-back reaches the conversation only between turns**. Findings wait while
  a turn runs and while words are queued, and are taken as a turn of their own once it ends. A word the person sends first goes first,
  and the findings go to the person. A `ChatTurnsTests` row shows a Daoris word marked as a step's never takes `NextStep`. Also an
  `OpinionResumeTickTests` (`Process`) case on both doors, run by the CI.
- [ ] **CHATFLOW1f — a graph-bound chat run** (driver; after d, FABRIC-CUTOVER1e). The Land reaching `claimed` at its tip, and `handed`
  as the kept entry. Proof: `WorkflowGraphRunTests` and `GraphRunLandingTests` rows for a `run:chat-<session>` unit.
- [ ] **CHATFLOW1g — on the install** (the parent; after a republish carrying a-e). A chat in its own tree, under a workflow naming an
  opinion, is landed by the person's press, read by another maker and answered between turns. A checkout chat and Ask Daoris say why
  they run none. Both languages at 1546 and 680 px. Proof: dated shots and a ledger under D157.

## 12. Rejected

- **Steps started at every turn's end with commits** (§5): a draft read as a candidate, another maker's account spent per reply, the one
  recheck used up early, and hours of gates queued behind a conversation.
- **Making a chat publish a quest to itself** so it rides the quest's path: an outward record for local work (D47 §4), a quest nobody
  asked for on the service, and an intake's job done by a conversation.
- **Keying the run by the tree's name** (`s-<8 hex>`): a run is the work's, and the session is what the person, the records and the view
  name. The tree is reached from the session, and the reverse needs a scan.
- **Keying it by the session id bare**: it works by length alone, and a later id of another length would collide.
- **Words into a turn in flight**, at a next step, by *Send now*, or urgent: the person is typing into that conversation, and D137 point 2
  and D136 give a running turn only words its sender chose to send sooner.
- **Queueing a send-back behind the person's words**: it would answer work the person has already moved past.
- **Handing the reviewer the conversation, or the person's later words** (D155 point 6, and its rejection of the worker's conversation).
- **A kind or a task choice for a chat**: no ask carries one, and an agent choosing one could lower the person's part (D157 point 10).
- **Landing a chat's work automatically** under a landing step's `autoAccept`: no done says the work is finished, only the person's press.
- **A go-ahead on the chat's session**: the session door is built for an ask's sessions and refuses this one by design
  (`SessionGoAheads.cs:75-84`). The run is D157's own home for one.
- **A workflow for Ask Daoris, an intake or a checkout chat**: none changes code Daoris lands (§9).

## 13. What this amends, once accepted

- **D157.** The workflow design §12.2's *a conversation's work under a workflow* is lifted for a chat in its own tree. Point 11's run gains
  a second key, `chat-<session>`, bound when the tree opens and read with no chain. Point 10's choice has no task level or kind for it.
  Point 2's gate holds it at the person's Land. Point 9's send-back reaches a chat only between turns. Point 13's go-ahead raised by a
  run sits on a chat's run alone. The *Workflow* view's no-run answer carries a code.
- **D155.** The second-agent design §12.2's *opinions on a conversation's work* is lifted for a chat run's landing. Point 2's `landing`
  occasion is owed at the person's Land. Point 6's packet takes the opening as what was asked and the commits' messages as the claim.
  Point 7's findings go to a chat once, between turns, and to the person where the person spoke first. The gate's answers are kept by
  the run's key.
- **Standing.** D137 points 2 and 3 (the door), D136, D154 point 8, D155 points 4, 6 and 8, D157 points 9 and 10, D164 (Accept names
  the branch), D47 §4, D50, D37.

## 14. What the checks do not cover

Documents only, and nothing is built. Each statement in §1 was read at `8cc3936b0`. The other sections' citations name what the change
reads or narrows, and were checked to exist there. No chat was run on the install. Whether an agent in a chat calls `opinion_answer`
under the handed rule is unmeasured, as it was for a driven one (XAGENT1j). The `check` step's runtime is FABRIC-PIPELINE1d's. Until it
is built, a chat run's checks show as not started, so the first proof shows them drawn, not performed. The zh words follow the
glossary, and no translator read them. The bound of 16 links reuses PARTNERLINE1's and is a judgement, not a measurement. `verify`
checks the router's row, the decisions' notes and the links here, and none of the words.
