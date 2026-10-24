# CHATFLOW1: the reads of round 1 (D191)

The independent Opus 5.5 reader's foundation read of `docs/2026-10-24-chat-flow-design.md` and its D157 and D155 notes at `94c9065f4`, verbatim but for its headings set one level down. The steward ran it from outside the install, read-only, on main. Codex's read stopped when its credits ran out, so this round had one reader; Codex's read is owed on the final text. The Opus foundation reader asks another round (24 findings, 8 blocking): rows a and b could go ahead after findings 1, 2, 12, 13 and 21, and §4.3 and §5 to §7 want rewriting around a gate that refuses the press with its reason and asks the opinion at that press (finding 23). Round 2 answers it.

## Opus (foundation), round 1

The read, verbatim.

### 1. Claims about the code

I read this at HEAD (`ffcf42926`). Since `8cc3936b0`, `src/` changed only in `TreeLands.cs`, `ReplayHolds.cs` and `ContentHolds.cs` (HOOKMERGE1), and no §1 citation reads those lines. Most of §1 holds as written. I checked `ChatRunner.cs:363-365`, `:557-612`, `:1819-1830` and `:1964-1972`; `SessionTrees.cs:354-372` and `:471`; `SessionLedger.cs:646-700` and `:669`; `WorkflowRun.Read.cs:80-119`; `WorkflowRoutes.cs:217-243`; `WorkflowRunView.tsx:301-304`; `TreesRoutes.cs:532` and `:551`; `TreeGates.cs:66`, `:83-87` and `:184-185`; `ReviewGate.cs:440-459`; `OpinionGate.cs:344-345`; `OpinionPresses.cs:170-173`; `OpinionDeliverer.cs:127-145`; `ChatTurns.cs:574-608`; `ChatQueue.cs:49-65`; `SessionGoAheads.cs:75-84`; `WorkflowNamed.cs:58` and `:94-99`; `WorkflowGraphs.cs:24` and `:82-95`; `NodeWord.tsx:8-16`; `WorkflowRunBindings.cs:41-53`; `identity.ts:159-180`; `waits.ts:81-96`; and `locales/en/workflow.run.json:148`. These claims are wrong or incomplete:

- **§4.3: "a chat under a `light` pipeline is refused `run-heavy` from its tree, as a driven session is."** This is false.
  - `run-heavy` refuses only a tree whose git lock names a session (`Sessions/Heavy/HeavyRun.cs:180-189`, through `SessionLocks.HolderOf`).
  - A chat's tree is opened under a lock that names the opening process, and `OpenAsync` lets that lock go once the tree is open (`Trees/SessionTrees/SessionTrees.cs:369-372`, `:404`). §1.1 leaves out that release.
  - Only a driven start takes a session lock (`Loop/Driver.cs:2442`, `Loop/DriverContinuations.cs:304`).
  - The checks a start keeps are let go when its run ends (`Driver.cs:2551-2552`, a `Disposer`).
  - The harness's deny rules travel in the driven start's settings (`HeavyTold.Hand` into `HandRules`, `DriverContinuations.cs:418-421`). A chat's rules are composed once, at its spawn (`ChatRunner.cs:1349-1360`).
  - So a chat told `light` would hear `LightWords`' "`run-heavy` refuses this tree, and your harness refuses each one's declared command" (`HeavyTold.cs:72-80`), and neither part would be true.
- **§4.2: in a steps version, a step this build cannot run "reads `cannot-start` with its limit's words."** A steps version that names a `check` does not read at all: `ReadSteps` refuses the whole version (`Workflows/Domain/WorkflowNamed.cs:343-348`). D157's revised FABRIC-CUTOVER1 note runs `check` only as a graph node (`D157.md:485`). A `check` step therefore exists only under a graph version, which is CHATFLOW1f's case.
- **§9, §10 and D157's note (`D157.md:527`): "an intake … runs none."** An intake's session already reads its ask's runs (`Workflows/WorkflowRun.Read.cs:86`). D157's WORKFLOW1c note records that behaviour: "an intake's through its ask" (`D157.md:241`).
- **§1.5's table and §9 on a partner.** A partner does not work in "its lead's" folder. It writes one piece in "a clone of its own with no remote", which Daoris keeps as one commit for its lead to merge (`D199.md:16-29`). So a partner does change something of its own, and `reads-for-another`'s words are false for it.
- **§4.2: `RunAsync` reads a chat "with three changes."** Every fact `RunAsync` reads is selected by `mine`, the sessions whose `Quest` is in the chain (`WorkflowRun.Read.cs:129`). That covers the standing tree (`:153-156`), the opinion read with `standing.Quest` (`:166-167`), and the landings, acceptances and due list (`:185-187`). `Derive` selects the work's sessions by quest again (`Workflows/Domain/WorkflowRun.cs:346-351`). A chat branch has to re-key all of these.
- **§4.1: "The trace needs `From` … CHATFLOW1a adds it."**
  - Row c, not row a, lists `TracedSession.From`.
  - The trace reads none of `opinion`, `serves` or `piece` either (`Sessions/Trace/Trace.cs:267-286`), and §4.2's "none of §1.5's" needs all three.
  - The service already answers all four to this machine (`Daoris.Service.Http/ApiContracts.cs:427-454`).
- **§4.1: the walk of "at most 16 links, as the partner's door bounds its line (PARTNERLINE1)."**
  - PARTNERLINE1 is an open defect (`TASKS.md:387`). The FIX-LOG's HELPERWAIT1 entry says why: stopping at the bound took a record the line may have left, and the fix refuses a line that goes past it (`docs/FIX-LOG.md:758`, `:764`).
  - The line's first record is already stored. SESSONE1a's entry, `session:<first>/<repository>`, is written when the chat opens (`SessionLedger.cs:692`; `Sessions/SessionEntries.cs:71-93`) and is on the wire as `entry` (`ApiContracts.cs:436-439`).
- **§4.3: a run is "cleared with its work, as a quest's run is (D153) … or its landing's record is kept."**
  - A quest's binding is cleared by the history clear, together with its quest (`D157.md:417-423`; `Sessions/History/HistoryHome.cs:29-31`). It is never cleared at its landing.
  - The workspace clear's leftover rule removes every binding that no quest names (`HistoryHome.cs:158-167`), and every `chat-<session>.json` is such a binding.
- **§5 points 2 and 3: the press is "kept on the due list (`AutoLandings()`) … by `AutoLander`'s path, which judges again."**
  - `AutoLander` closes an entry `off` whenever the rule is not a branch with `autoAccept` (`Trees/Landing/AutoLander.cs:184-191`).
  - It closes `undone` where its quest does not judge (`:227-228`), and `superseded` where a newer record sits on the tree (`:218-222`).
  - It tries nothing while a live record holds the tree (`:211-217`).
  - `AutoLanding.Quest` and `OpinionDue.Quest` cannot be null (`Trees/Landing/AutoLanding.cs:154`; `Review/Domain/OpinionDue.cs:10-11`).
- **§6: the send-back goes "through `ChatRunner.Tell`'s path … always ordinary."** `Tell` builds a `ChatMessage` at its default level, which is `Urgent` (`ChatRunner.cs:1821`; `Conversations/Domain/ChatMessage.cs:27`). It marks the words as Daoris's (`DaorisWord`) and carries no `by`. On `Tell`'s path as it stands, a send-back would be handed into a turn in flight and read as Daoris's words, not as another agent's claims.
- **§6: "`opinion_answer` is handed to a chat carrying findings, as `DriverContinuations.AnswersFindings` hands it to a driven run."** A driven run gets it as a rule when its resume spawns (`DriverContinuations.cs:418-420`, `:555`). A live chat's rules are fixed at its spawn (`ChatRunner.cs:1349-1360`), so there is no way to hand the rule for one turn.
- **§8: a chat run's go-ahead "sits on the run alone, and is answered on the Workflow view."** No door answers a go-ahead on a run. A run raising one belongs to WORKFLOW1l, which is not built (`Workflows/Domain/WorkflowCurrent.cs:86-87`).
- **§13: "Point 13's go-ahead raised by a run."** D157 point 13 is Ask Daoris's proposals (`D157.md:56-59`). The go-ahead a run raises is D157's amendment of D135 (`D157.md:106-107`).
- **§7: "The working family is the chat's agent's, and a hand-over's adds the next agent's."** D155 point 4 counts every family that wrote commits in the candidate (`D155.md:31`). Its COAGENT1 note adds every partner whose piece the candidate holds (`D155.md:488-492`).
- **Paths and line numbers.**
  - `WorkflowRun.cs` and `WorkflowGraphRun.cs` are under `Workflows/Domain/`, so the bare name is easily confused with `Workflows/WorkflowRun.Read.cs`.
  - `TracedRecords.cs:63-64` is `Interrupted`; `Teammate` is at `:69-70`.

### 2. The mechanism's holes

**A run keyed by a session, over records keyed by quests.**

| Record | Where it stands for a chat run |
|---|---|
| Binding | The key works: `chat-1a2b3c4d` passes `IsPlainId`. But the node runtime arms every binding to a graph version in `workflows/runs/` (`Trees/Nodes/GraphRuns.cs:59-110`), and the gate's graph part judges by it (`TreeGates.cs:64-77`). So CHATFLOW1c's binding at the tree's open makes a graph-bound chat run live before CHATFLOW1f has said what its `claimed`, `handed` or `quest.close` subjects are. |
| Second opinion | The gate file's name works. Everything else is keyed by the quest: <ul><li>`OpinionGate.JudgeAsync` and `TreeGates.OpinionAsync` answer `None` without a quest (`OpinionGate.cs:345`; `TreeGates.cs:141-144`).</li><li>`OpinionLook.Concluded` takes a `QuestView` and owes the opinion at the quest's done (`Loop/OpinionLook.cs:19-25`).</li><li>A due closes `undone` on its quest (`OpinionDue.cs:28-29`).</li><li>The pass reads the tree's `HEAD` at the look, not the tip the Land named (`Loop/OpinionGateLook.cs:353`).</li><li>The service refuses a second automatic first pass for the same working session (`Daoris.Service.Core/Review/OpinionDesk.cs:395-406`). A chat that lands, goes on and lands again is therefore owed no opinion on its second candidate. That is a service change, and no row lists the service.</li></ul> |
| D154's look | The set-up is a chain step, a quest with `SetUpIn`. Its verdicts and its skip are kept on the quest (`ReviewGate.cs:271`, `:345`) and written through quest routes (`/api/quests/{id}/set-up`, `/review` and `/set-up-step`; `PersonDoorTableTests.cs:66`, `:103-104`). A chat run has no home for a set-up, *Reviewed*, *Not yet* or a skip. Yet §5 point 2 and §8 keep "D154's look waiting for *Reviewed*". |
| The due list | Built for automatic landings: it needs a quest, closes `off` without `autoAccept`, and skips live trees (see §1). |
| Go-aheads | On the ask (refused for a chat) or on the run (not built). In a steps version the `go-ahead` step is partial: a session asks on its ask, which a chat cannot do. |
| History | The leftover rule removes a chat's binding (see §1). |
| What seals the work | A graph's submission and `ready` (FABRIC-WORKFLOW2d) belong to CHATFLOW1f. |

**A step that starts while the conversation is still open.**

- **The kept Land never lands while the chat is open.** `AutoLander` tries nothing while a live record holds the tree, and a chat between turns stays `working`. So a kept Land lands only after the conversation has ended.
- **If it did land at a look, it would race a turn writing in the tree.** `TreeLands` refuses uncommitted work (`TreeLands.cs:223-226`), so a dirty tree mid-turn refuses the landing. The design has to say whether a kept Land lands only between turns, holding the turns as a hand-over does (`chat.Turns.Hold()`, `ChatHandOver.cs:142`), or only at the conversation's end.
- **A merge-form rule needs a ruling.** Under it, a deferred press merges at a moment the person is not pressing. D145 point 1 and AUTONOMY1c2's `merges` case keep a merge from landing by itself (`AutoLanding.cs:183-187`, `:205-206`). The design does not say whether a kept Land under a merge is refused or merges.

**The send-back.**

- **The send-back's own commits void the Land.** §5 point 4 makes any commit after the named tip `moved`. The turn that answers the findings commits, so it voids the Land. §6 then has the recheck read that turn's commits, and D155 point 7 runs the recheck by itself (`D155.md:50-53`). That is a step started at a turn's end, which §3 rule 4 forbids. As written, the loop cannot end in a landing.
- **The person may be typing when it arrives.** The deliverer acts at a look. `TurnEnded` is true while a draft sits in the composer, and the driver cannot see a draft. So the send-back's turn starts (`Take` → `Now`), and the person's next word, `Urgent` by default (`ChatMessage.cs:27`, D136), is handed into it. "The person's words go first" holds only for words sent before the deliverer acts. The design should say that.
- **A hand-over between the Land and the findings.**
  - The findings name the pressing record (`view.Working`). The hand-over ended that record, and resuming it under D137 point 3 finds its tree held by the new record. The findings have to go to the line's newest record.
  - The kept entry closes as `superseded` (`AutoLander.cs:218-222`).
  - The new record is told only the commit trailers, not the process (`ChatRunner.cs:613-619`), so the `checks` told when the tree opened never reach the next agent.
- **An ended chat that goes on rewrites its commits.** It first brings its tree up to date (`ChatGoOn.cs:535-541`, LANDSYNC2b), which rewrites them. So `moved` has to be read by ancestry, and a rebase voids the Land too.

**A chat's tree that the person lands, discards or hands over mid-run.**

- **Lands, then goes on.** The tree can land again by LAND2c's advance. But the design clears the binding at the first landing, and the next turn's end binds again under the same key, possibly to a newer version, in the middle of the run. The second landing's opinion is then refused by the service's bound.
- **Discards.** This is covered by the existing `gone` codes.
- **Hands over.** See the send-back above.

**Ask Daoris, chats with no tree, intakes, reviewers and their kin.**

- **Ask Daoris and checkout chats** are fine with their codes.
- **Intakes:** see §1.
- **Reviewers and helpers.** A reviewer's clone `o-…` lies under the trees root (`Review/OpinionTree.cs:18-20`), so `SessionTrees.Holds` is true for it (`TreeContext.cs:67`). A helper's tree is its lead's. Unless the trace reads `opinion`, `serves` and `piece`, both would read as chat runs.
- **Partners:** see §1.
- **A chat that took a quest.** Its record keeps `quest` null and the taken quest in `took_quest` (CHATTAKE1; `SessionEntries.cs:102-106`; `ServiceClient.cs:1981-1988`). It would become a chat run bound to the repository's default. Meanwhile its quest's own task choice (the ask's, or WORKFLOW1e2's) and its quest's run go unread. That is two runs for one piece of work, with the person's choice bypassed.

**A workflow whose steps need a quest's fields.**

- **The asker.** D155 point 6 hands the reviewer "the requirements with the person's quoted words". The opening replaces them, but by a second landing it may no longer describe the candidate. Name it as the conversation's opening, not as what was asked.
- **The chain.** D154's set-up step is a chain step, and so are the opinion's `Later` and its `steps` occasion.
- **The go-aheads:** see the table above.

**What stays the person's press.** §8's intent is right. But a kept Land is a press that the look carries out later, so D51 rule 6's merge case needs a ruling. A failed bind that falls back to Current can also lower what the repository's chosen workflow asks of the person (finding 18).

**The no-run answer's words in en and zh.**

- An own-tree chat reads as a checkout chat until row c (finding 1).
- The partner's and the intake's words are false (see §1).
- *Try again* (重试) is the glossary's word for starting a quest's work again (`locales/glossary.json:652`). The workflow design's word for a read that failed is *Ask again* (`docs/2026-10-09-workflow-design.md:553-554`).
- `chat-checkout`'s zh, "请在自己的工作树中开始一个对话", reads as the reader's own tree.
- The terminal keeps "serves no quest" for a checkout chat while the page says something else. D50's two doors and WORKFLOW1c5's twin table (`fixtures/workflow-run-said.json`) should hold each code's English on both.

**The census and the person's doors.**

- **The kept Land** is a third answer of `LAND_SESSION_TREE`, beside landed and refused. `trees land` needs its words and exit code.
- **The other presses:**
  - *Don't land it* has no terminal door named (D50).
  - Opening *Ask now* to a chat (`OpinionPresses`) changes a press's reach.
  - The run's go-ahead has no door at all (WORKFLOW1l).
- **Ask Daoris coverage.** `HelpCoverageTests` (D110) needs each new door proposed or exempted. §11's "none adds an Ask Daoris door" points to WORKFLOW1c4's reads, which are not these presses.
- **Records to update:** the route rows for `WORKFLOW_RUN`'s `code` and for the press's kept answer, and the run's English twin table.
- **Quest operations:** none are added, so `quest-operations.md` is untouched, which is right.

### 3. The rows

- **CHATFLOW1a.**
  - It can be built alone once the trace reads `opinion`, `serves` and `piece`.
  - As written, the `:87` sentence becomes `chat-checkout` (§10's last bullets), so until row c a chat in a tree of its own, which is the owner's own case, says it works in the checkout.
  - The `intake` code regresses an intake's view.
  - The proof shows the codes, not the classification. It needs one row each for a reviewer, a helper and a partner, which are trees that `Holds` would take for a chat run.
- **CHATFLOW1b.**
  - It can be built alone after row a, and its proof shows that the words render.
  - It needs a row for an own-tree chat before row c.
  - The glossary's words are checked only by `names:check`.
- **CHATFLOW1c.**
  - It cannot be built as written. The graph-version binding would arm the runtime (finding 3), its `check`-step proof cannot be set up (finding 9), and the `light` checks it tells are not enforced (finding 7).
  - Its proof draws the opinion "once you land it" while the Land, ungated until row d, lands at once, so the words promise what the press will not do.
  - No proof holds "refused `run-heavy`". That needs a `HeavyRunTests` row on a chat's tree.
- **CHATFLOW1d.**
  - It cannot be built from the design. The look has no home, `AutoLander` cannot carry the kept press, the service's bound refuses a second pass, `OpinionDue` has no tip to pin, and the merge-form case has no ruling.
  - Its proof misses five cases:
    - an open chat's kept Land landing between turns;
    - a hand-over between the Land and the landing;
    - a second landing of the same tree;
    - uncommitted work at the landing;
    - a merge-form rule.
- **CHATFLOW1e.**
  - It depends on row d, and on settling the contradiction between the send-back's commits and the kept Land.
  - Its proof misses where findings go after a hand-over, and whether a live chat has `opinion_answer` at all.
- **CHATFLOW1f.** It is in the right place, provided row c binds no graph version before it.
- **CHATFLOW1g.** Fine.

**Is the first row the one that gives the owner the most?** No. The owner reported three things: English in zh, no checks, and no gate. Rows a and b fix the words, though not for the owner's own case until row c. Row d delivers the gate, which is the heart of "every piece of work runs its repository's workflow".

A simpler gate is available, and the design does not weigh it. §5 point 2 says only that today's gate refuses the press. The alternative is to gate a chat's press the way a quest's press is gated today:

- the press reads the chat run's process (row c's `TreeGates` change), and the opinion's part is keyed by the run;
- where something holds, the press is refused with its reason, and the opinion is asked at that press;
- once it settles, the person presses again.

That needs no `AutoLander` branch, no deferred merge and no landing on a live tree, and the landing stays the person's press in time. I would make that row d, and keep the deferred press as a later row if it is still wanted.

### 4. Findings

1. **An own-tree chat reads as a checkout chat in rows a and b** (design §10, lines 326 and 338). **Blocking.** Until row c, the `:87` branch answers every chat, so `chat-checkout` would tell the owner's own case "This chat works in your checkout, not in a tree of its own". Smallest change: answer `chat-checkout` only where `SessionTrees.Holds` does not hold the tree, and give an own-tree chat with no run yet a code of its own, worded in row b and replaced by row c's run.
2. **The `intake` code contradicts built behaviour** (`WorkflowRun.Read.cs:86`; `D157.md:241`). **Blocking**, because building it as written regresses WORKFLOW1c. Smallest change: drop `intake` from §9, §10 and D157's note. An intake keeps reading its ask's runs, and `ask-no-quest` covers it before any quest is published.
3. **Row c would arm graph-bound chat runs before CHATFLOW1f** (`GraphRuns.cs:59-110`; `TreeGates.cs:64-77`). **Blocking.** Smallest change: row c binds a chat only where the selection gives Current or a steps version. Under a graph version it binds nothing and answers a code until CHATFLOW1f binds it, and the gate meanwhile reads as today, which the design should say.
4. **D154's look has no home for a chat run** (`ReviewGate.cs:271`, `:345`; the quest routes in `PersonDoorTableTests.cs:66`, `:103-104`). **Blocking.** Smallest change: before row d, say where a chat run's set-up, *Reviewed*, *Not yet* and skip are kept, with their person's doors and census rows. One option is the run's machine-local record, as the opinion gate keeps the person's words. Otherwise, say that a required look on a chat run reads `cannot-start` and holds the Land, and name the person's way past it.
5. **The kept Land cannot ride the automatic due list** (`AutoLander.cs:184-191`, `:211-228`; `AutoLanding.cs:154`, `:183-187`, `:205-206`). **Blocking.** The list closes `off` without `autoAccept` and `undone` without a quest, closes `superseded` after a hand-over, never lands a tree a live record holds, requires a quest, and has no ruling for a merge-form rule. Smallest change: adopt the refuse-and-ask gate (finding 23). Otherwise specify the chat branch: no quest, never closed `off`, landed only between turns with the turns held, carried to the line's newest record after a hand-over, uncommitted work handled, and a merge-form rule refused (the person presses again).
6. **The send-back's own commits void the Land it serves** (design §5 point 4 against §6 "Once"; `D155.md:50-53`). **Blocking.** As written, the findings loop never ends in a landing, and D155's automatic recheck becomes a step started at a turn's end, which rule 4 forbids. Smallest change: the answering turn closes the kept Land with a code of its own, and the recheck and the landing wait for the person's next Land, which names the new tip and counts against D155's one recheck.
7. **A `light` chat is told enforcement it does not have** (`HeavyRun.cs:180-189`; `SessionTrees.cs:404`; `Driver.cs:2551-2552`; `ChatRunner.cs:613-619`, `:1349-1360`; `HeavyTold.cs:72-80`). **Blocking.** Smallest change: when a chat opens in its own tree, Daoris should:
   - take the session lock in its id (`SessionLocks.HoldAsync`) and let it go at the conversation's end;
   - keep its checks for the conversation's life rather than per run;
   - compose `HeavyTold.Hand`'s deny rules into `RulesFor`;
   - tell a hand-over's next record the checks;
   - add a `HeavyRunTests` row on a chat's tree.
8. **The binding is cleared at the wrong time, and the history clear deletes it** (design §4.3, line 205; `HistoryHome.cs:158-167`; `D157.md:417-423`). **Blocking.** Clearing at the landing breaks §8's after-landing steps and makes a chat that goes on bind again under the same key. The leftover rule deletes a live chat's binding at a workspace clear. Smallest change: clear a chat run's binding with its records through the history clear only, teach the leftover rule that `chat-<session>` belongs to that session, and say that one run spans every landing of its tree.
9. **No steps version can hold a `check`** (`WorkflowNamed.cs:343-348`; `D157.md:485`). Not blocking. §4.2's claim and row c's `cannot-start` proof are impossible. Smallest change: drop that case from row c, say that `check` steps exist only under a graph version, and move their proof to CHATFLOW1f.
10. **Row c's run promises an opinion that row d delivers** (design §4.2, lines 174-176). Not blocking. Smallest change: until row d, draw the opinion `not-reached` with no "once you land it", or ship those words with row d.
11. **The run's key reinvents the stored entry and copies an open defect** (`SessionEntries.cs:71-93`; `SessionLedger.cs:692`; `TASKS.md:387`; `FIX-LOG.md:758`, `:764`). Not blocking. Smallest change: key the run by the entry's origin, `session:<first>`, where the first record's tree is this tree, and by the record's own id otherwise. Or follow `From` and refuse past the bound, as HELPERWAIT1b does. Either way, say where the design's "same tree" rule differs from the entry's.
12. **The trace lacks the fields that tell a reviewer, a helper and a partner apart** (`Trace.cs:267-286`; `OpinionTree.cs:18-20`; `TreeContext.cs:67`). Not blocking. Smallest change: row a reads `opinion`, `serves`, `piece` and `from` into `TracedSession`, its proof covers each kind, and §4.1 is aligned with the rows.
13. **The design misstates what a partner does** (`D199.md:16-29`; `D155.md:488-492`). Not blocking. Smallest change: fix §1.5's folder, give a partner words of its own ("writes a piece its lead lands; its lead's run is the run"), and add partners' agents to §7's working families.
14. **A chat that took a quest is not addressed** (CHATTAKE1; `SessionEntries.cs:102-106`; `ServiceClient.cs:1981-1988`). Not blocking. Smallest change: say which run its work is, and whether the quest's own task choice binds it. The simplest reading is that its run is the taken quest's.
15. **A chat's later landing gets no owed opinion, and the pass is not pinned to the Land's tip** (`OpinionDesk.cs:395-406`; `OpinionGateLook.cs:353`). Not blocking. Smallest change: row d's lanes include the service, making the automatic pass's bound one per landing for a chat run; `OpinionDue` carries the named tip; and the packet calls the opening "the conversation's opening", not what was asked.
16. **The run's derivation is understated** (`WorkflowRun.Read.cs:129-187`; `WorkflowRun.cs:346-351`; `SessionGroups.cs:892-905`). Not blocking. Smallest change: say that every fact is selected by the chat's line, that the work step reads `SessionEvents.TurnEnded` where no runner holds the chat (as `SessionGroups.Between` does), so the terminal's view matches the page's, and add the missing state for a chat that ended with commits beyond the line.
17. **The send-back's delivery is unspecified** (`ChatRunner.cs:1821`; `ChatMessage.cs:27`; `ChatRunner.cs:1349-1360`; `ChatGoOn.cs:535-541`). Not blocking. Smallest change: the send-back is a word of its own at `Ordinary` level carrying `by`, not `Tell` as it stands. The chat run's rules carry `opinion_answer` from its spawn. Findings go to the line's newest record. A turn the send-back starts while the person drafts is said to be accepted. `moved` is read by ancestry, so a go-on's rebase voids the Land.
18. **A failed bind can lower the person's part** (design §4.3, lines 199-201; `D157.md:362`, `:393-394`). Not blocking. A chat's bind reads only local files, yet falling back to Current skips whatever the repository's named workflow asks. Smallest change: a Land on an unbound chat run tries the bind first, and a bind that cannot be made refuses the Land with its reason. The conversation goes on; only the landing waits.
19. **A run's go-ahead has no door** (`WorkflowCurrent.cs:86-87`). Not blocking. Smallest change: say that until WORKFLOW1l a chat run's `go-ahead` step reads `cannot-start` with its limit, and correct §8's sentence.
20. **The doors and the census are unnamed** (D50, D110). Not blocking. Smallest change: row d names the kept press's answer shape and `trees land`'s words and exit code, the terminal twin of *Don't land it*, the `HelpCoverageTests` exemptions or proposals for the new presses, and the route rows.
21. **Some words are wrong** (`glossary.json:652`; workflow design `:553-554`; design §10's zh). Not blocking. Smallest change: use *Ask again* for `unanswered`; write `chat-checkout`'s zh as "请开始一个有自己工作树的对话"; hold each code's English on both doors through the run's twin table.
22. **Citations** (design §13; §4.2; §1.5). Not blocking. Smallest change: replace "Point 13's go-ahead" with D157's amendment of D135, put `Workflows/Domain/` on the two paths, and cite `TracedRecords.cs:69-70`.
23. **The deferred press is not weighed against the refuse-and-ask gate** (design §5 point 2). Not blocking alone, but it is the root of finding 5. Smallest change: weigh the alternative in §5 and §12. My recommendation is to build it as row d (§3) and make the deferred press a later row if wanted.
24. **The backlog has two open CHATFLOW1 rows** (`TASKS.md:236`, `:386`). Not blocking. Smallest change: fold them into one row.

24 findings, 8 blocking.

**Verdict: another round.** The direction is sound. Giving a conversation in its own tree its repository's workflow, keying the run by the conversation where no quest is, starting steps only at the person's word, and sending findings back only between turns all hold up, and §1 reads the code accurately with few exceptions. But §5–§7's mechanism rests on records the design does not re-home: D154's look, the automatic due list and the service's opinion bound are all keyed by quests. Its send-back loop also contradicts its own voiding rule, so as written the loop never lands. And row c would arm graph runs, tell a chat checks nobody enforces, and lose its binding to the history clear (findings 3–8). Rows a and b could go ahead now after findings 1, 2, 12, 13 and 21, which the steward can check without another read. §4.3 and §5–§7 need a second round, best rewritten around the refuse-and-ask gate (finding 23).
