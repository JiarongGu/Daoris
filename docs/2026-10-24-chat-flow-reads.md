# CHATFLOW1: the reads of rounds 1 and 2 (D191)

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

## Opus (foundation), round 2

The independent Opus 5.5 reader's foundation read of round 2 at `a8573c142`, the only read of the round: 11 findings, 2 blocking; accept after named changes the steward can check without another read. Codex's read is owed on the final text.

### 0. Round 1's findings, checked against the code

I read at HEAD `3928dd5a9`. The design and its D157 and D155 notes have not changed since `a8573c142`.

| # | Round 1 | Now | Checked against |
|---|---|---|---|
| 1 | Own-tree chat read as a checkout chat | **Taken** | `WorkflowRun.Read.cs:87` still answers every chat. §4.2 and §10 give `chat-checkout` only where `Holds` fails, and `chat-own-tree` before row c. Rows a and b each prove it. |
| 2 | `intake` code | **Taken** | No code. `:86` is kept. |
| 3 | Row c arms graph runs | **Taken** | `Plan` returns a graph's digest (`WorkflowRunBindings.cs:129-132`), so the chat bind must test for it before it writes. The press's bind-first needs the same carve-out (finding 8). |
| 4 | D154's look has no home | **Taken** | `ReviewStates.CannotStart` and its skip floor exist (`ReviewGate.cs:74`, `:339`, `:411-412`), so d2's part can be built. The skip is re-homed, bound to a tip, on the run's look file. |
| 5 | Kept Land on the due list | **Taken** | No press is kept and no `AutoLander` path is left. |
| 6 | Send-back voids the Land | **Taken** for the voiding. Its replacement is open (finding 2). | The recheck "at the next press" has no mechanism (`OpinionGate.cs:428-438`; `OpinionDeliverer.cs:174-184`). |
| 7 | `light` told, not enforced | **Taken** | The chat is told nothing until 1h, which names the five steps. `Driver.cs:2442` and `:2551-2552` are now `:2447` and `:2557`. |
| 8 | Binding cleared wrongly | **Taken in part; open** (finding 3) | It is not cleared at a landing, and the leftover rule learns `chat-<id>`. But the history clear never removes a chat's record (`HistoryPlan.cs:23-30`; `HistoryHome.cs:64-66`). A chat goes by the session delete (`SessionDeletion.cs:50-86`). |
| 9 | No steps version holds a `check` | **Taken** | |
| 10 | Opinion promised before row d | **Taken** | |
| 11 | Key reinvents the entry | **Taken** (no walk, no bound). The fallback case is misdescribed and open (finding 4). | `SessionEntries.cs:71-94`; `BriefedMove.cs:26-27`, `:128-134` |
| 12 | Trace fields | **Taken** | `Trace.cs:267-286` still reads none of them. Row a adds six. |
| 13 | Partner misstated | **Taken** | |
| 14 | CHATTAKE1 | **Taken** for the view. **Deferred** to row i for the gate, with a reason that holds. But `took_quest` is per record, so a hand-over splits the run (finding 4). | `Service.Core/Sessions/Sessions.cs:1733-1740` |
| 15 | Later landing gets no opinion | **Answered.** The reason holds as policy but is overstated (finding 6). The opening is **taken**. | `OpinionDesk.cs:395-426`, `:417-418` |
| 16 | Derivation understated | **Taken** | |
| 17 | Send-back delivery unspecified | **Taken in words.** It cannot be built as rowed (finding 1). | `OpinionDesk.cs:480-492`; `OpinionDeliverer.cs:105-114` |
| 18 | Failed bind lowers the person's part | **Taken.** The graph case needs carving out (finding 8). | |
| 19 | Run's go-ahead has no door | **Taken** | `WorkflowRun.cs:395-401`; `WorkflowCurrent.cs:86-87` |
| 20 | Doors and census unnamed | **Taken**, with two gaps (finding 5) | |
| 21 | Words | **Taken** for the zh and the twin table. *Try again* is **answered** with a reason that holds. | `help.json:117` and `signin.json:6` say 重试 for a failed read. `opinion.json:97`'s *Ask again* is the opinion's. |
| 22 | Citations | **Taken** | |
| 23 | Refuse-and-ask not weighed | **Taken** | |
| 24 | Two backlog rows | **Taken** | One row, now at `TASKS.md:384`. |

### 1. Claims about the code

Round 2 was written at `ca046e7fe`. SUPERVISOR1b merged before it landed, so `Driver.cs` (+5 lines after `:917`) and `SessionLedger.cs` (+6 after `:164`) have moved since then. "Only tests changed" since `ffcf42926` also misses `Platform/WiringVariables.cs` and the CLI's `package.json`. No claim turns on these. I re-checked §1's citations and those of §4–§10. These are wrong or incomplete:

- **§4.3, §13, D157's note and row c: "cleared with its records, by the history clear only".**
  - The history clear never removes a chat. Its units are quests, asks and failed sessions (`Sessions/Domain/HistoryPlan.cs:23-30`). A workspace's reading only counts "conversations that served no quest" (`Sessions/History/HistoryHome.cs:64-66`, `:86-87`).
  - A chat's record goes by the session delete (`SESSION_DELETE`, `sessions delete`; `Sessions/History/SessionDeletion.cs:50-86`). That delete refuses while the chat's tree stands or a landing names it (`:127-141`), and it removes only `SessionHomeFiles`' names (`:75`).
  - The leftover rule sweeps `workflows/runs/` but not `opinions/gates/` (`HistoryHome.cs:103-167`). A chat run's gate file and look file would therefore stay forever.
- **§4.1: "Otherwise it is `chat-<own id>`: a line that moved to another tree (a retry or a move) starts a run there."**
  - No chat's `From` link changes tree. A hand-over opens on `prior.Tree` (`ChatHandOver.cs:157`). A limit's move and a briefed go-on pass `tree: tree` (`Conversations/Chat/LimitMove.cs:141-143`, `:255-257`; `BriefedMove.cs:128-134`). *Start a conversation with these words* passes no `from` (`ChatRunner.cs:326-330`).
  - What the fallback actually catches is a line whose origin is no session: a closed quest's session gone on as a conversation in the quest's own tree (SESSCTX1c part B, `BriefedMove.cs:26-27`). Its stored origin is its quest's or its ask's (`Service.Core/Sessions/SessionEntries.cs:84-94`).
- **§4.2's `tookQuest` row.**
  - The field belongs to the taking record alone: `MarkTookAsync` writes it by that record's id (`Service.Core/Sessions/Sessions.cs:1733-1740`).
  - `OpenChatAsync` takes no `tookQuest` (`SessionLedger.cs:652-706` at HEAD). A hand-over's next record therefore reads as a chat run, while the record before it reads the quest's run.
- **§6: "The deliverer offers it only where `TurnEnded` is true … so `ChatTurns.Take` cannot hand it into a turn in flight."** The design names none of the following, and each is in the way:
  - **The service's hand refuses a live record.** `CannotTake` refuses every `Active` session, `working` included (`Review/OpinionDesk.cs:488-490`; `Sessions.cs:321-322`), and a live chat between turns is `working` (D164). The hand goes only to `opinion.Working` (`OpinionDesk.cs:248-253`), and only the record handed to may answer (`:285-294`).
  - **The deliverer reads a live chat as answering forever.** `Running` is true for `working` (`Quests/Domain/Planner.cs:361`), so `DeliverAsync` answers `with-session` (`Loop/OpinionDeliverer.cs:105-114`). Answers are read only at a driven resume's end (`:252-271`, whose one caller is `DriverContinuations.cs:658`) or after the record stops running.
  - **The loop reaches the shell's runner only through seams.** The modules wire them (`Loop/LoopContext.cs:57-71`; `Modules/Loop/DriverLoop.cs:890-895`), and the headless host holds none.
  - **Checking `TurnEnded` and then calling `Take` is a race.** `TurnEnded` is read outside `ChatTurns`' lock (`ChatRunner.cs:1969-1972`), and `Take` queues an ordinary word as `TurnEnd` once a turn is pumping (`ChatTurns.cs:594-608`). A word the person sends between the check and the take puts the send-back behind it, which §12 rejects.
- **§5.2 point 4, §6, §13's D155 amendment and the D155 note: "the recheck … is asked at the person's next press".**
  - Nothing asks a recheck at a press. Once answers are kept and no recheck has been asked, the gate reads `read-again` and says another agent is reading (`Review/Domain/OpinionGate.cs:428-438`).
  - The only thing that starts a recheck is `AnsweredAsync`, at the look, right after it reads the answers (`OpinionDeliverer.cs:174-184`).
  - `OpinionRequest` and `OpinionDue` ask first passes only (`OpinionGateLook.cs:213-229`).
- **§5.2 point 7: "by the one recheck where it is unspent".**
  - A recheck reads only commits made in answer to findings that were handed (`OpinionDesk.cs:417-418`).
  - Where the first pass raised nothing, or its findings went to the person, no agent reads a later landing. Until row e, every chat's findings go to the person (`OpinionDeliverer.cs:134`).
- **§9 and §10's `ask-daoris`: "Ask Daoris reads and advises (`ChatRunner.cs:688-691`) … changes no repository's code".** That remark is D89's. D183 point 1 gives Ask Daoris a full-access mode with shell and file edits as its own acts, held back only by the room's words (point 5).
- **§4.3: "a failure is a home that cannot be written or a version that cannot be read".**
  - An unreadable version does not fail the bind. `Plan` keeps it as a binding with its `Problem` (`WorkflowRunBindings.cs:120-132`), and the process holds the work by that problem (`WorkflowProcesses.cs:65-70`).
  - So only an unwritable home fails the bind.

### 2. The mechanism's holes

**A run keyed by a session, over records keyed by quests.** Row d names `OpinionGates`, `JudgeAsync`, `OpinionDue.Run` and `WorkAsync`. Still keyed by the quest, and not named:
- **`OpinionPresses.PressedAsync`** returns the gate unanswered for a null quest (`Review/OpinionPresses.cs:64`), and the route calls it with `questId` (`TreesRoutes.cs:586`).
  - A chat's press can then never answer `disputed` or `commits-since`.
  - Until row e every chat's findings reach the person as disputes (`OpinionGate.cs:423-427`), so only *Go on anyway…* would land them.
- **`OpinionGateLook.JudgeAsync`** reads the chain, the process (`RunOf(chain) ?? due.Quest`) and the gate by `due.Quest` (`Loop/OpinionGateLook.cs:185-206`).
- **`AskPassAsync`** takes its words from the chain's ask (`:381`). The opening has to come in there.
- **`AskAsync`** writes its due at `rule.On[0]` (`OpinionPresses.cs:118`). Under a rule whose first occasion is `steps`, a chat's due would close `past` at once (`OpinionLook.cs:80-84`).

**A step on a conversation still open, and a send-back while the person types or a turn runs.**
- The refusing gate removes round 1's race with the landing: the press is the person's, between turns, and uncommitted work refuses it.
- A pass reads the tree's `HEAD` at the look. That is the press's tip only if no turn ran in between, but the gate's ancestry check covers it.
- The send-back is where the mechanism breaks (finding 1).
- The loop's end is undecided. As the code stands, the answers would be read and the recheck started at the look after the answering turn, which contradicts §6 (finding 2).

**A tree landed, discarded or handed over mid-run.**
- **Landed:** the due closes `landed`, and nothing is delivered after that (`OpinionGateLook.cs:191-195`).
- **Discarded:** the due closes `gone`. For a bound run whose tree was discarded with unlanded commits, the work step names no state.
- **Handed over:** the gate stays with the line's key. But findings already handed to the record that pressed stay on that record, and only that record may answer them (`OpinionDesk.cs:285-294`).
  - A record handed over never takes another turn (`WordsNever.Continued`, `BriefedMove.cs:78-83`).
  - Those findings can then only go to the person, unless the service re-hands them to the newest record. The design should say which.

**Ask Daoris, chats with no tree, intakes and reviewers.**
- The classification order holds: a reviewer's `o-…` clone, a helper and a partner are classified before the tree test, and checkout chats and intakes are right.
- Ask Daoris's words are false under full access (§1).
- Two lines are misread (finding 4):
  - **A closed quest's session gone on as a conversation** becomes a fresh chat run on the quest's own tree. It is bound to the repository's default rather than the quest's choice, and asks a new opinion over the quest's own gate.
  - **A CHATTAKE1 chat's hand-over** becomes a chat run beside the taken quest's.

**A workflow whose steps need a quest's fields.**
- **The asker:** the opening, named as the opening, stands in for it.
- **The chain:** a chat has no later step and no set-up, and d2 answers the look.
- **The go-aheads:** a chat run's `go-ahead` step reads `cannot-start` until WORKFLOW1l. All three hold.

**What stays the person's press.** This holds: every landing happens at the person's press, and *Accept without the look…* is theirs alone. The press now also asks a pass that spends another maker's account. That is the rule's own ask, made at the person's word, so it is right. But `trees land --plan` and the page's `LANDING` read (`TreesRoutes.cs:561`) must ask nothing, and the design does not say so.

**The no-run answer's words.** §10 is sound except for two codes:
- **`ask-daoris`** is wrong under full access (§1).
- **`helper`'s zh** "它协助的会话有进展" reads as "the session it helps is making progress". 进展在它协助的会话那里 says that the run is there.

**The census and the person's doors.** `--without-look` and its `Person` route, the `HelpCoverageTests` exemptions and `WORKFLOW_RUN.code` are all named. Gaps:
- **The plan must ask nothing** (above).
- **Nothing says when to press again.** The refusal tells the person to press again once the opinion settles, but no door tells them it has settled for a chat run. The review row and the foot show it only to a person who looks; a notice would tell them.
- **The told landing differs from the real one.** `ProcessToldAsync` plans the landing without the bound process (`ChatRunner.cs:2244`), while the press lands by `gate.Process` (`TreesRoutes.cs:561-566`). From row c, a chat under a steps version would be told one landing and get another.
- **The view's checks are not enforced.** From row c the view shows `light` checks that nothing enforces until row h, and it does not say so.

### 3. The rows

- **CHATFLOW1a.**
  - It can be built alone, and its proof proves the codes and the kinds.
  - It should settle finding 4's two cases now, with a row each: a quest's session gone on as a conversation, and a CHATTAKE1 chat's hand-over.
- **CHATFLOW1b.** Fine.
- **CHATFLOW1c.** It can be built after a. Two of its proofs cannot be set up:
  - **"A line moved to another tree starting a run of its own":** no chat flow moves a line to another tree.
  - **`HistoryClearingTests`' "cleared with its record":** the history clear never clears a chat.
  - Replace them with `SessionDeletionTests` and a leftover-rule row (finding 3), and a quest-line chat (finding 4).
  - Add a row each for the press's bind-first leaving a graph choice as today (finding 8) and for the told landing (finding 7).
- **CHATFLOW1d and 1d2.**
  - They can be built after c once finding 5's sites are named.
  - The proof list is good. Add: the plan asks nothing; a chat run's press answers a dispute and `commits-since`; *Ask now*'s due is written at `landing`.
- **CHATFLOW1e.**
  - **It cannot be built in its lane (driver).** It needs the service and the modules (finding 1), and the recheck decided (finding 2).
  - Its proof misses four cases:
    - a word arriving between the check and the take;
    - the headless host;
    - findings handed before a hand-over;
    - the answers read at the send-back turn's end.
- **CHATFLOW1f, 1g, 1h, 1i.** In order. Row i needs only a.

**Is the first row the one that gives the owner the most?**
- **Rows a and b** fix one of the owner's three complaints (English in zh). They are cheap, settled and first by dependency, so they are right to start with, but they are not the most value.
- **Row d with d2** is the owner's gate. It depends only on c, and b stands apart, so c and d can follow a at once with b beside.
- **Unestablished:** which workflow the owner's own repositories follow on the install.
  - If it is a graph version (a FABRIC-PIPELINE1 development pipeline is one), rows c–e give those repositories only `chat-graph-later`.
  - The owner's case then waits for row f, behind FABRIC-CUTOVER1e and FABRIC-PIPELINE1d.

### 4. Findings

1. **Row e's send-back cannot be built in its lane, and §6's checks race** (`OpinionDesk.cs:248-253`, `:285-294`, `:488-490`; `Planner.cs:361`; `OpinionDeliverer.cs:105-114`, `:252-271`; `LoopContext.cs:57-71`; `ChatRunner.cs:1969-1972`; `ChatTurns.cs:594-608`). **Blocking.** As written, a live chat's findings wait as `with-session` until the conversation ends, and the press is refused all that time. Smallest change: row e's lanes become service, driver and modules, and §6 names five things:
   - (a) the service's hand admits a chat run's `working` record, names the line's newest record as its target, and lets that record answer;
   - (b) a `LoopContext` seam, wired by `DriverLoop` as `HandsOver` is, takes the word as an ordinary turn only if the runner is idle. The take is atomic under `ChatTurns`' lock, as `TakeHeld` is (`:552-567`). Otherwise the findings go to the person as `person-went-on`, and the headless host leaves them waiting;
   - (c) the answers are read when that turn ends, as `OpinionsAnsweredAsync` reads a resume's;
   - (d) `DeliverAsync` reads a chat run's record by the runner's turn state, not by `Running`;
   - (e) what happens to findings handed before a hand-over.

   Proof rows are needed for each, and for a word arriving between the check and the take.
2. **The recheck "at the person's next press" has no mechanism** (`OpinionGate.cs:428-438`; `OpinionDeliverer.cs:174-184`; `OpinionGateLook.cs:213-229`). **Blocking.** Built as written, either the recheck starts at the look after the answering turn, against §6 and §13, or the gate sits in `read-again` with nothing reading. Smallest change: keep D155 point 7's automatic recheck after the answering turn. That turn is one Daoris started, the recheck is bounded to one, and it reads only the commits made in answer. Say that rule 4 governs the person's turns, and drop the amendment from §5.2 point 4, §6, §13 and the D155 note. Otherwise, name a gate state for a recheck owed at the press, the press's write of it, and its row and proof.
3. **The history clear is the wrong door** (`HistoryPlan.cs:23-30`; `HistoryHome.cs:64-66`, `:103-167`; `SessionDeletion.cs:50-86`, `:127-141`). Not blocking. Smallest change, in §4.3, §13, the D157 note and row c's proof:
   - a chat run's binding, gate file and look file go with the session delete of the record its key names, by adding them to `SessionHomeFiles`' names;
   - the leftover rule takes `chat-<id>` files under `workflows/runs/`, `opinions/gates/` and `workflows/looks/` where no record `<id>` remains.
4. **The key's fallback misses the case it catches, and `tookQuest` is per record** (`SessionEntries.cs:84-94`; `BriefedMove.cs:26-27`, `:128-134`; `Sessions.cs:1733-1740`). Not blocking. Smallest change:
   - drop "a line that moved to another tree";
   - say that a line whose stored origin is a quest's or an ask's reads that quest's run in its repository and is gated as row i gates a `tookQuest` chat;
   - read `tookQuest` from any record of the line in this tree, or carry it at a hand-over's open;
   - row a proves both.
5. **Row d's quest-keyed sites are not all named, and the doors have two gaps** (`OpinionPresses.cs:64`, `:118`; `TreesRoutes.cs:561`, `:586`; `OpinionGateLook.cs:185-206`, `:381`; `OpinionLook.cs:80-84`). Not blocking. Smallest change: row d names these, and adds proof rows for a chat run's press answering a dispute and for the plan asking nothing:
   - `PressedAsync` keyed by the run;
   - `OpinionGateLook.JudgeAsync`'s chain, process and gate keyed by the run;
   - `AskPassAsync`'s words taken from the opening;
   - chat runs' dues written at `landing`, *Ask now*'s included;
   - the plan (`LANDING`, `trees land --plan`) asks nothing;
   - where the person learns that the opinion settled: the foot, the review row, and a notice.
6. **A later landing is rarely read by another agent** (`OpinionDesk.cs:395-426`, `:417-418`). Not blocking. For a long conversation, only its first landing gets an automatic opinion. Smallest change: §5.2 point 7 says plainly that a later landing is read only where the first pass's findings were handed and the recheck is unspent. Otherwise it shows `commits-since`, which the person's press answers or *Ask now* reads. Or give a chat run one automatic first pass per landing. That is the owner's call; the design should name it.
7. **What the conversation is told, and what the view shows, differ from what row c does** (`ChatRunner.cs:2244`; `TreesRoutes.cs:561-566`). Not blocking. Smallest change: row c plans the told landing with the bound process, and the view marks a chat run's checks as not yet enforced for a conversation until row h.
8. **The press's bind-first must leave a graph choice as today** (`WorkflowRunBindings.cs:120-132`; `WorkflowProcesses.cs:65-70`). Not blocking. Smallest change: §4.3 and row c say three things:
   - a choice that is a graph version is not refused `bind-failed`;
   - `bind-failed` means only an unwritable home;
   - an unreadable named workflow binds with its `Problem` and holds the press by it.
9. **Two codes' words** (D183 points 1 and 5; §10). Not blocking. Smallest change:
   - `ask-daoris` becomes "Ask Daoris works in its workspace's room, not in a repository's tree, and a repository's work is asked for through a quest, so no workflow runs for its conversations." In zh: 问道衍在工作区的房间里工作，不在任何仓库的工作树中；仓库的工作要通过委托来请求，所以它的对话不走工作流。
   - §9 cites D183 instead of the D89 remark.
   - `helper`'s zh ends "……进展在它协助的会话那里。"
10. **The owner's own repositories' workflow is not stated** (§11). Not blocking. Smallest change: §11 names what they follow on the install. Where it is a graph version, §11 says that rows c–e give them only `chat-graph-later`, and weighs bringing row f forward.
11. **Citations drifted at HEAD.** Not blocking. Smallest change, at the final text:
    - re-pin `Driver.cs`: `:2442` is now `:2447`, and `:2543-2552` is now `:2548-2557`;
    - re-pin `SessionLedger.cs`: +6 from `:354` to `:700`;
    - correct "only tests changed";
    - `TASKS.md:386` is now `:384`.

11 findings, 2 blocking.

**Verdict: accept after named changes the steward can check without another read.** Round 2 took round 1's foundation: the refuse-and-ask gate, no kept press, no graph binding before CHATFLOW1f, nothing told that is not enforced, and the line's stored key. §1 and §4.2 read the code well. Both blocking findings are confined to the send-back loop. Row e's delivery needs the service and the modules and an atomic take (finding 1), and its recheck needs deciding (finding 2, best by keeping D155's automatic recheck). Each fix is a named change to §6, §13 and row e. Rows a and b may go ahead once finding 4's two classification rows are added. Rows c, d and d2 may go ahead once findings 3, 5, 7 and 8 are taken. Row e waits for findings 1 and 2. The steward can check all of these against this read. Codex's read stays owed under D191.

## Codex, the accepted text (owed)

Codex's read of the accepted design and its D157 and D155 notes at `16e0950e2`, owed since round 1 when its credits ran out, read after rows a and b were built: 12 findings, 8 blocking; reopen, keeping the refusing gate, the local run and the ordinary send-back direction.

### 1. Claims about the code

Read against **main `7cda79f01`**, including both earlier reads in full and D157’s and D155’s CHATFLOW1 notes and acceptance note.

Paths below use:

- `D/` = `src/Daoris.Desktop/Daoris.Desktop.Driver/`
- `M/` = `src/Daoris.Desktop/Daoris.Desktop.Modules/`
- `S/` = `src/Daoris.Service/Daoris.Service.Core/`
- `W/` = `src/Daoris.Web/src/`

These claims are wrong against main, or overstate what the cited mechanism establishes:

- **“Nothing here is built,” and §1’s account of the trace, API and untranslated no-run answer.** Rows a and b have landed. The trace reads all six fields (`D/Sessions/Trace/Trace.cs:286–291`), `SessionResponse` carries `TookQuest` (`src/Daoris.Service/Daoris.Service.Http/ApiContracts.cs:457`), the route answers code and interpolation values (`M/Bridge/Routes/WorkflowRoutes.cs:243`), and the page translates known codes (`W/workflow/WorkflowRunView.tsx:303–309`). These are historical baseline statements now, not today’s behaviour. Finding 12.
- **§4.1’s inference that an unchanged `From` tree makes every session-origin entry the first record in that tree.** An entry can inherit the origin of the session that *published a quest*, independently of its worker’s `From` line (`S/Sessions/SessionEntries.cs:108–112`). A closed quest’s worker continued as a chat can consequently have a session origin naming a publisher in another tree. Finding 6.
- **“An ask’s origin is an intake’s line.”** Quests sent by an ask also receive that origin (`SessionEntries.cs:95–97`). Their workers, and those workers continued as chats, can have `ask:<id>/<repository>`, not an intake’s entry. Finding 6.
- **§5.2’s “a landing never meets a turn writing in the tree,” because Accept is offered between turns and refuses dirty work.** Offering a press does not reserve the turns. The route reads the gate and later lands without holding `ChatTurns` (`M/Bridge/Routes/TreesRoutes.cs:551`, `:596`). A clean, newly committed turn can pass the dirty check. Finding 1.
- **§6.1(c)’s “the reservation’s task completes at the send-back turn’s end, as `TakeHeld`’s does.”** `TakeHeld` returns `_drained.Task` (`D/Conversations/Chat/ChatTurns.cs:560–566`). That task completes when the pump drains, not at one message’s turn boundary (`:859–870`, `:1008–1009`). Finding 3.
- **§6.2’s claim that the recheck reads only commits made in answer.** The existing answer reader captures current `HEAD` (`D/Review/OpinionAnswers.cs:87–92`); fix validation checks ancestry between the first opinion’s tip and that head (`:127–145`). It establishes neither the answering turn’s boundaries nor who caused every intervening commit. Finding 3.
- **§7’s assertion that `HEAD` at the look, “between turns,” is the press’s tip.** A whole intervening turn can finish before that look. `AskPassAsync` reads current `HEAD`, without a press tip or turn generation (`D/Loop/OpinionGateLook.cs:353–359`). Checking coverage at the *next landing* protects neither the earlier review request nor its send-back. Finding 4.

### 2. The mechanism’s holes

The refusing gate is the right replacement for a kept landing. The settled text also fixes the earlier service/module omissions. The remaining problem is that **one atomic turn reservation is being asked to establish several different boundaries**.

It does not establish that the person has no earlier message being persisted. `ChatTurns.KeepAsync` releases `_gate` while awaiting the service and has not yet put that message in the queue. It does not establish that the person has not already completed another turn since Accept. And the proposed completion task does not identify the end of the answering turn.

The landing has a separate boundary problem. For Current and steps versions, the gate’s opinion tip is not automatically `LandingGate.Tip`. Only the graph path pins that field. Reading a gate at A and subsequently landing the tree at B remains possible. Neither a disabled button nor a clean working tree is a substitute for binding the press to its candidate.

There is also more than one meaning of “line.” The entry groups work by its origin; `From` identifies continuation; a concrete quest identifies the workflow binding and gate. An ask origin and a publisher’s session origin cannot substitute for the concrete quest. The settled classification still conflates them.

Finally, the proposed chat work states do not fit the existing quest derivation unchanged. A chat waiting for Accept has not reached `done`, while the later-step readers require work to be done before showing the opinion and landing. The graph row has the corresponding unresolved problem at runtime: it needs a candidate’s beginning, end and next cycle without a quest.

These are additional findings beyond the earlier reads’ fixes. They affect whose words start the next turn, which candidate gets reviewed and landed, and whether the view exposes the gate that actually holds the work.

### 3. The rows

| Row | Can it be built independently in this order? Does its proof suffice? |
|---|---|
| **a** | Already built. Its classification needs finding 6’s correction. The existing examples prove selected origins, not the general identity rule. Add an ask-published quest continued as a chat, a quest inheriting another tree’s publisher origin, and a taken-quest chat contributing actual run facts. |
| **b** | Already built and stands for its stated translation scope. |
| **c** | After a’s correction and finding 7. Add proof that the chat’s opinion and landing remain visible before landing, including while findings are answered. Its visible checks mark also requires the web lane. |
| **d** | After c and finding 1. The existing sequential proofs do not establish candidate identity under a new turn, a hand-over or a wait for the landing hold. Exercise the actual mutation on both terminal paths and the bridge. |
| **d2** | Must ship with d, as stated. Its skip needs finding 1’s candidate boundary too: the tip skipped must be the tip landed. |
| **e** | Cannot be built safely from the present contract. Findings 2–5 amend reservation, completion, stale delivery and ended-chat resumption. The proof currently asks both to wait behind activity and to send findings to the person when reservation fails; those outcomes need distinct conditions. |
| **f** | Needs finding 8’s subject and lifecycle contract before binding any graph. A `run:chat-…` fixture alone does not prove closure, another landing, or a failed check’s send-back. |
| **g** | Correctly remains installed proof. One uninterrupted successful loop cannot prove the races; its ledger should distinguish that demonstration from the boundary proofs required above. |
| **h** | Can follow c, but needs the resumption proof in finding 10 and the web work in finding 11. |
| **i** | Can follow corrected a independently of the chat-run rows. Finding 6 must supply one concrete work resolver shared by its view and every landing/opinion door. |

### 4. Findings

1. **The landing is not bound to the candidate the gate judged. — Blocking; before d and d2.**

   The claim is at design `:344–365`. `TreesRoutes.cs:551` reads the gate before `:596` lands. `D/Review/Domain/OpinionGate.cs:675–679` explicitly permits an unpinned landing to use the tree as it stands; `D/Trees/SessionTrees/TreeGates.cs:64–76` pins only graph landings. The landing hold serializes landings, not conversation turns (`D/Trees/SessionTrees/TreeLands.cs:420–438`). A person can start and commit another turn after the gate read, and those commits can land under the earlier opinion or look skip.

   **Smallest change:** reserve the chat against another turn for the press, judge every part at one captured tip, and pass that tip through the existing pinned landing path. Refuse a moved candidate. Apply this to both doors, including a terminal addressing a conversation another process holds. Prove movement after gate judgement and while waiting for the landing hold.

2. **The reservation can overtake a person’s earlier message. — Blocking; before e.**

   Design `:434–440` reserves under `_gate` alone. But `D/Conversations/Chat/ChatTurns.cs:159–172` serializes persistence with `_keeping`, checks `_gate`, then awaits `door.KeepAsync`. The message enters `Take` only at `:197`. During that await the queue can look idle, so the send-back reserves first although the person already sent—and may already have persisted—the earlier word.

   **Smallest change:** serialize reservation with the dispatcher’s keep operation before testing idle state. Make adoption of the service-kept findings word part of that dispatcher operation. Add a test holding the person’s service write between the initial check and `Take`; the findings must not overtake it.

3. **The proposed completion task does not delimit the answering turn. — Blocking; before e.**

   Design `:442–443` relies on `TakeHeld`’s task. That task is the whole pump’s drain (`ChatTurns.cs:560–566`, `:865–870`). A person’s ordinary message arriving during the send-back can run before it completes. Reading answers then captures that later turn’s `HEAD`, and the automatic recheck can include the person’s subsequent work (`D/Review/OpinionAnswers.cs:87–92`; `D/Loop/OpinionDeliverer.cs:216–222`).

   **Smallest change:** give this particular turn a completion boundary. Capture its ending tip and answers before another queued turn runs, and make the recheck use that frozen reading. Do not describe ancestry alone as proof that a commit was made by that turn. Prove a person’s queued turn changing `HEAD` immediately after the answering turn.

4. **A completed intervening person’s turn is invisible to the send-back decision. — Blocking; before e.**

   Design `:410–413` tests waiting words; `:435–440` tests present idleness. Neither detects: Accept at A; the opinion reads A; the person sends and finishes work B; findings arrive while the chat is idle. The send-back then starts automatically on work the person has moved past. The rejection at design `:679` gives exactly that reason for forbidding queueing behind their words.

   The existing state stores a last turn end (`ChatTurns.cs:987–990`), but the opinion request records no corresponding person-turn generation (`D/Review/Domain/OpinionGate.cs:96`, `:129`).

   **Smallest change:** associate delivery eligibility with the person-turn generation at the initiating press, and compare it atomically at reservation. An intervening person’s turn sends the findings to the person even if it has already ended. Distinguish that from temporary activity which merely waits. Add both cases to row e’s proof.

5. **An ended chat can wait forever without receiving the word that would resume it. — Blocking; before e.**

   Design `:440–441` leaves findings waiting whenever no runner holds the chat. Yet `:463–464` requires an ended chat to resume in its own conversation. The service hand occurs only *after* reservation (`:437–438`), so the ended record never receives the findings word. Its normal go-on path requires `WordsWaiting` (`D/Conversations/Chat/ChatGoOn.cs:440–443`) and consumes those words before `RunAsync` (`:558–586`).

   **Smallest change:** name a separate ended-chat path: hand the word to the ended record, then request its existing go-on through the owning runner, preserving attribution and its turn completion boundary. Keep headless waiting as an explicit separate outcome; route a definitive inability to resume to the person. Prove an ended resumable chat with no person’s words already waiting.

6. **The origin classifier does not identify the work whose workflow must govern it. — Blocking; correction to a before i.**

   Design `:208–216`, `:234–235` and row i at `:653–658` leave three related gaps:

   - An ask origin can belong to a repository worker, not an intake (`S/Sessions/SessionEntries.cs:95–97`). Row i names quest origins and `tookQuest`, leaving this resumed worker without its concrete quest gate.
   - A quest can inherit its publisher’s session origin (`:108–112`); its continued worker’s tree need not be that publisher’s tree. The stated key rules cover neither that mismatch nor the concrete quest behind it.
   - Reading a taken quest’s run does not make the chat one of its work records. Main still selects `mine` exclusively by `session.Quest` (`D/Workflows/WorkflowRun.Read.cs:185–187`), then derives its tree, gate and landings from that set (`:209–245`). These chats keep `Quest` null.

   **Smallest change:** resolve the concrete work from the continuation’s quest-bearing records or its taken quest before treating an origin as an intake or an own chat run. Use that resolution consistently for run facts and gates. Preserve the genuine intake’s ask-wide view. Add all three cases to a/i’s proofs, asserting worker, tree, opinion and landing facts as well as run identity.

7. **The chat’s displayed work state prevents its later steps from being derived as a quest’s. — Blocking; before c.**

   Design `:261–267` keeps the work step `waiting-on-you/land` until landing, while saying later steps derive as a quest’s. Existing derivation sets `workDone` only for `done` (`D/Workflows/Domain/WorkflowRun.cs:354–363`). Opinion returns `not-reached` before that (`:583`), as do look (`:678`) and landing (`:735–737`). During a findings-answering turn, the chat becomes `working` again, which would hide the opinion then too.

   **Smallest change:** define chat candidate readiness independently of the displayed work state and use it for later-step derivation. Preserve the opinion’s actual state while its answering turn runs. Prove the view before first Accept, during review, during answer delivery, after recheck and after landing.

8. **Row f still lacks the graph subject and next-candidate lifecycle it says it supplies. — Blocking; before f.**

   Design `:283–285` defers `claimed`, `handed` and `quest.close` subjects to f. Row f (`:642–644`) mentions the first two but settles neither `quest.close` nor another landing of the same run.

   Today `claimed` and `quest.close` observe a quest (`D/Trees/Nodes/GraphRuns.cs:226–230`); runtime branch selection depends on that quest (`D/Trees/Nodes/NodeRuntime.cs:487–501`). An ended run is reconsidered when a different quest returns (`:492–493`). A chat has none of those facts.

   **Smallest change:** specify the chat candidate’s subject, its submission at Accept, treatment or rejection of `quest.close`, and how a later candidate reopens the same bound run without starting steps at an ordinary turn’s end. Keep graph binding disabled until that contract exists. Prove two successive landings, hand-over, closure and a failed check’s single send-back.

9. **Reviewer independence needs proof across the newly permitted re-hand. — Not blocking.**

   Design `:446–449` permits the successor to answer, and `:494–496` requires independence from every contributing family. Existing recheck selection seeds writer discovery with `first.Working` (`D/Loop/OpinionDeliverer.cs:211–214`), while `OpinionWriters.Agents` follows that record’s ancestry (`D/Review/Domain/OpinionWriters.cs:23–39`). It does not discover a later successor merely because that successor answered.

   **Smallest change:** explicitly include the answering successor’s line when selecting the rechecker, while retaining the original opinion identity required by the service. Add a proof where the successor is of the original reviewer’s family; that reviewer must no longer be presented as independent of the fixes.

10. **Row h’s proof stops before same-record resumption. — Not blocking.**

    Design `:315–318` acquires checks and the lock when a chat opens and releases them when it ends. Going on in that same conversation bypasses `StartAsync` and calls `RunAsync` directly (`D/Conversations/Chat/ChatGoOn.cs:576–586`). `run-heavy` depends on both the session lock and kept checks (`D/Sessions/Heavy/HeavyRun.cs:183`). Testing a new chat and a hand-over does not prove this path.

    **Smallest change:** name acquisition and restoration for every resumed execution, including come-up, with release on failed starts. Add an end-and-go-on proof for a light chat. Also distinguish Current’s live requested checks from the checks actually handed to the running harness if the rule changes during its lifetime.

11. **Some row ownership and proof obligations remain outside the listed lanes. — Not blocking.**

    Row c changes a visible checks mark (`design:596`), and h removes it (`:320–321`), but neither lists web. Row d’s new mutation is described at `LAND_SESSION_TREE`; the standalone terminal currently goes directly from gate reading to landing (`src/Daoris.Desktop/Daoris.Desktop.Driver.Host/TreesConsole.cs:390`, `:417`). A refusal’s wording test does not prove that terminal press asks the opinion.

    **Smallest change:** add web to c/h’s ownership, place the refuse-and-ask mutation in a shared driver operation, and explicitly prove it through the bridge, connected terminal and standalone terminal. Keep all three plans free of writes.

12. **The settled document needs an updated standing over its preserved baseline. — Not blocking.**

    The main-code discrepancies listed in §1 are consequences of a/b landing; D157 records them at `docs/decisions/D157.md:550–593`. Design `:11` and `:719` still say nothing is built. Its code citations also retain the pre-a reader’s line numbers.

    **Smallest change:** add the a/b implementation boundary to the standing and label §1 as the dated baseline. Re-pin citations used by the remaining implementation rows. Preserve the earlier reads and historical answer tables unchanged.

**12 findings, 8 blocking.**

**Verdict: reopen.** The refusing gate, local run and ordinary send-back direction stand, but the settled mechanism still lacks a reliable boundary for the person’s earlier words, the answering turn and the candidate their press lands. Findings 1–8 require amendments before d/d2, e, corrected a/i, c and f respectively. Rows a and b’s completed work remains useful; their existence does not establish those remaining boundaries.

## Opus, the amendment checked

An independent Opus 5.5 reader's check of the amendment that took Codex's owed read (`e9bca8aa5`), standing in for Codex while its credits are out: 12 findings, 1 blocking row c; row c may be built after named changes the steward can check.

### 1. Codex's eight blocking findings

Paths are the design's: the driver's under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, the modules' and the service's as its standing names them. `design:` is `docs/2026-10-24-chat-flow-design.md` on main.

**1. The landing is bound to the judged candidate (before d and d2).** The text answers it: §5.2 point 1 (`design:422-440`), point 7 (`:459-462`), §5.3 (`:484-485`), rows d and d2 (`:778-792`) and §12 (`:888-889`). The answer holds against main. `TreeGates.GateAsync` already judges every part at a passed `tip` (`Trees/SessionTrees/TreeGates.cs:39-55`). `LandingGate.Tip` is the pin (`Review/Domain/OpinionGate.cs:673-679`), and the landing refuses a branch that moved off it in both forms (`Trees/SessionTrees/TreeLands.cs:420-431`, `:601-605`). The bridge and the standalone terminal read and land where the design says (`Bridge/Routes/TreesRoutes.cs:551`, `:596`; `Daoris.Desktop.Driver.Host/TreesConsole.cs:390`, `:417`). Three things are open:
- **The reservation has no named state.** `Hold()` is the only reservation `ChatTurns` has, and it makes `KeepAsync` and `Take` drop the person's word (`Conversations/Chat/ChatTurns.cs:166`, `:579`). So "a word the person sends while the press holds the reservation waits behind it" (`design:432`) needs a state the design never names.
- **The wait can be long.** The person's words would also wait through the landing hold, which lasts up to five minutes (`TreesRoutes.cs:594`).
- ***Ask now* has no tip or count.** Its due on a chat run carries neither the captured tip nor the person's turn count (finding 5).

**2. The reservation overtaking an earlier word (before e).** The text answers it in §6.1 (b) (`design:540-555`), and the answer holds. `KeepAsync` takes `_keeping` before the service's keep and releases it only after `Take` (`ChatTurns.cs:159-209`). A reservation that takes `_keeping` first therefore cannot overtake a keep in flight. The only thing left open comes through finding 4's count: a word the service did not keep still reaches `Take` (`:169-182`), but it never reaches the record the count reads.

**3. The answering turn's own boundary (before e).** The text answers it in §6.1 (c) (`design:558-563`), §6.2 (`:582-593`) and §12 (`:890-891`). The answer holds:
- `TakeHeld` returns the pump's drain (`ChatTurns.cs:552-566`, `:865-869`).
- The pump passes a point between a turn's end and its next take (`:975-997`, then `:862-876`), and a capture fits there.
- The recheck already reads the reading's tip, never `HEAD` (`Loop/OpinionDeliverer.cs:221`, `Tip = reading.Tip!`).

What is open is steering. The person's word defaults to `Urgent` (`Conversations/Domain/ChatMessage.cs`, `Level`). On a door that steers, an urgent word reaches the running answering turn at its next step (`ChatTurns.cs:586-591`). The person's work then falls inside the boundary, and the recheck reads it as answers (finding 6).

**4. A completed intervening turn (before e).** The text answers it in §5.2 point 3 (`design:447-449`), §6.1 (b) (`:544-550`) and §7 (`:611-616`). The answer does not hold as worded:
- The count is "the number of the person's words kept on the line's records" (`design:544-545`; D157's amendment note, *Readings taken*).
- In this code a kept word is one on the record's `said` list, and it leaves that list once its turn is on the wire (`ChatTurns.cs:93-96`, `:957-964`; `Conversations/SaidDoor.cs:15-16`; `Quests/Domain/Planner.cs:355`).
- Take a turn the person sent and finished after the press, which is finding 4's own case. Its word is off the record when the findings arrive, so the count never rises (finding 4).

**5. An ended chat waiting forever (before e).** The text answers it in §6.1 (f) (`design:570-577`) and §6.2 (`:599-600`). The answer holds against the go-on, which needs an ended chat with words waiting (`Conversations/Chat/ChatGoOn.cs:440-443`) and already treats such words as another agent's answer (`:583`). Today the service hands findings to an ended `opinion.Working` (`Review/OpinionDesk.cs:248-249`; `CannotTake`, `:480-492`). Two things are open:
- **(a) blocks (f).** (a) admits only a `working` record and refuses "every other record … as today" (`design:535-539`). After a hand-over, the line's newest record is not `opinion.Working`. So the hand to it that (f) needs is refused by (a)'s own words.
- **(f) compares no turn count** (finding 7).

**6. The work that governs a record, resolved before its origin (a correction to row a, before i).** The text answers it in §4.1 (`design:231-241`), §4.2 (`:264`, `:281-286`) and rows a and i (`:732-738`, `:847-851`). The answer holds against the code:
- `WorkOf` follows §4.1's order (`Workflows/WorkflowRun.Read.cs:165-176`).
- `ChainsOf` reads it before an intake's or an ask's origin (`:114-116`).
- `RunAsync` selects its work records by it (`:210-214`).

The new `WorkflowRunTests` cases (`Daoris.Desktop.Driver.Tests/Workflows/WorkflowRunTests.cs:641`, `:672`) cover a worker of a quest an ask sent, a publisher in another tree, a genuine intake and a continuation's landing. Open: the taken-quest case's run facts and the opinion fact (finding 12), and a chat run whose line takes a quest later (finding 3).

**7. Later steps derived by readiness (before c).** The text answers it in §4.2 (`design:304-313`) and row c (`:758-761`). The answer does not hold against `Derive`. `Candidate` replaces `workDone`, but two other parts of `Derive` are unchanged (findings 1 and 2):
- The landing step still reads only ended records, and finds a landing by the record's id.
- The opinion step reads a gate that answers `None` for a record with no quest.

**8. A graph-bound chat run's subject and lifecycle (before f).** The text answers it in §4.5 (`design:380-403`), §4.3 (`:327-332`) and row f (`:824-828`). The answer holds. `claimed` and `quest.close` observe a quest (`Trees/Nodes/GraphRuns.cs:226-228`), an ended run is judged again only when a different quest comes back (`Trees/Nodes/NodeRuntime.cs:492-493`), and binding a graph version stays disabled until row f. Open: a submitted candidate whose tip moves before it lands (finding 9).

### 2. What the amendment broke or left inconsistent

- **Row c's proof against its own mechanism.** Before row d, `Candidate` is true, so the opinion step reads the gate (`WorkflowRun.cs:583`). The gate answers `None` for a null quest (`TreeGates.cs:141`), which is drawn as `skipped` (`WorkflowRun.cs:596`). `design:312` and row c's proof (`:751`) say `not-reached`. The landing step has the same kind of gap (finding 1).
- **Row e's body keeps the rules its amendment replaces.** It still says:
  - "§6.1's five";
  - "as `TakeHeld` does";
  - "(c) the answers read at the send-back turn's end";
  - findings "wait … while words are queued" and also "go to the person" (`design:793-803`).

  Its amendment says "six things" and gives distinct outcomes (`:808-814`). Together these are the mixed outcomes Codex's row e cell named.
- **§6 now contradicts §6.1 and §7 in two places.**
  - "A pass is asked only by an *Accept…*" (`design:504`) sits against *Ask now* on a chat run at any time (`:608-610`). That matters now, because every run's due carries a tip and a count.
  - The next-step reach is "accepted and said" (`:511-513`). That sits against (c)'s "its commits are never read as answers" (`:562-563`) and §6.2's "no other turn ran" (`:587`).
- **Stale or wrong claims about the records.**
  - §11 still says "Codex's read stays owed under D191" (`design:708`).
  - §17 says `5d19f6b3a`'s `src/` is `7cda79f01` "plus rows a and b's later commits" (`:999`). In fact `git diff 7cda79f01 5d19f6b3a -- src/` is empty, because rows a and b are in both.
  - The standing says the citations the remaining rows build from are re-pinned in §17 (`:20`). Only §17's table is re-pinned: §6.1's `DriverContinuations.cs:658` is now `:660` (`design:523`).
- **The router lists the reads twice.** `docs/README.md:141` (updated) and `:143` (the old "Codex's read owed" row) both name `2026-10-24-chat-flow-reads.md`. The commit added a row instead of replacing one.

### 3. Findings

1. **A chat run's landing step does not follow `Candidate`. Blocking row c.**

   §4.2 reads `workDone` as `Candidate` (`design:309-310`) and leaves `Landing` as a quest's. But `Landing` reads only ended records with evidence, and finds a landing by the record's id (`WorkflowRun.cs:730-760`). That breaks in three places:
   1. **A live chat between turns.** Its record stays `working` (§1.5), so it is not `ended` (`:732`; `Live`, `:464`). With `Candidate` true, the landing reads `done`, `nothing` (`:740-743`). `Derive` never holds a landing that reads `done` behind an unsettled opinion (`:374-379`). So the view says there is nothing to land at exactly the moment the work waits on the press.
   2. **Every tip after the first landing.** Once a record has a landing or an acceptance (`:749`, `:758`), its landing reads `done` for every later tip, and the opinion shows the kept landing's opinion (`:573-580`). Yet §4.2's work step returns to `land` once the tree moves (`design:301`), and one run spans every landing (§4.1). The run's acceptances also carry no tip (`WorkflowRunRecords.cs:20-40`). So "a landing's record is kept for the tip the tree stands at" cannot be read for a merge landing.
   3. **After a merge landing under a rule that names no opinion.** This is the install's Current (§11). Nothing stands beyond the line and no opinion was asked, so `Candidate` is false. The landing then reads `not-reached` (`:735-738`) beside a work step that reads `done`.

   Row c's proof ("the landing visible … after the landing", `design:759-761`) cannot pass from this text.

   **Smallest change:** one paragraph in §4.2, saying four things:
   - A chat run's landing reads the line's newest record in the tree, live or ended.
   - Its commits are read from the tree beyond the line, as the review's offer reads them.
   - It reads `done` only where a landing or an acceptance is kept for the tip the tree stands at. The paragraph says where a merge acceptance's tip is read, and that `done` comes before `talk` and `stopped`.
   - `Candidate` is true from commits beyond the line, between turns or ended, or from an opinion a press asked, until that tip lands. It stays true while the landing reads `done`.

   Three proof rows go with it:
   - A live chat between turns reads `waiting-on-you`, `accept`.
   - A merge landing under a rule that names no opinion reads `done` after it lands.
   - A second candidate after a branch landing reads `accept` again, with its own opinion.

2. **Before row d, the opinion step reads `skipped`, not `not-reached`. Not blocking.** This is §2's first bullet. **Smallest change:** pick one:
   - say in §4.2 that until row d, a chat run whose process names an opinion reads `not-reached` by a rule of the run's own, so the null-quest gate's `None` is never drawn as a skip; or
   - change `:312` and row c's proof to `skipped`, with words saying that no opinion is asked for a conversation's work yet.

3. **A chat run whose line later takes a quest. Not blocking.**

   `WorkOf` reads a `tookQuest` from any record of the line in this tree, newest first (`WorkflowRun.Read.cs:175`, `:187-196`), and a chat can take a quest at any point in its life (CHATTAKE1). So a chat run already bound as `chat-<first>`, whether it has landed or not, then reads the taken quest's run:
   - its earlier landings are counted there;
   - its binding, gate and look files are left unread until the session delete.

   §4.1's "one run per tree, across its landings" (`design:250-252`) does not say this. **Smallest change:** one sentence in §4.1 saying which run governs from the take on and what becomes of the chat run's three files, plus one row in row c's proof.

4. **The person's turn count is read from words the record drops. Not blocking c; needed before d, which records it, and e.** This is §1's finding 4. **Smallest change:**
   - Define the count as something that only rises: the person's own words in the line's conversation history (their `user` events in `SessionEvents`, on each record of the line in the tree), plus the words waiting or being kept.
   - Read it the same way at the press and at the reservation.
   - Prove it with a turn the person sent and finished after the press, with nothing left on `said`.

5. **The press's reservation has no named state, and *Ask now* has no tip or count. Not blocking c; needed before d.** This is §1's finding 1 and §2's third bullet. **Smallest change:**
   - Name a reserved state in which a word is kept and waits but starts no turn, as `_limited` behaves (`ChatTurns.cs:594-601`), let go when the press answers.
   - Say whether it is held through the landing hold, or let go once the tip is pinned. The pin alone already refuses a moved candidate.
   - Say that *Ask now* on a chat run captures `HEAD` and the count at its press. Otherwise `:504` stands, and *Ask now*'s findings go to the person.

6. **An urgent word steered into the answering turn defeats finding 3's boundary. Not blocking c; needed before e.** This is §1's finding 3 and §2's third bullet. **Smallest change:** pick one, and add one row to row e's proof:
   - while a send-back's turn runs, the person's words reach it only at its end (`TurnEnd`), unless they press *Send now*; or
   - a reading whose turn took a word of the person's is marked as such, no recheck is asked automatically, and its answers go to the person.

7. **(a) and (f) disagree, and (f) ignores the count. Not blocking c; needed before e.** This is §1's finding 5. A person who went on after the press and then ended the chat again would have the findings resumed into work they have moved past. **Smallest change:**
   - (a) also admits an ended chat record that is the newest of `opinion.Working`'s line in its tree.
   - (f) compares the count first, and a rise sends the findings to the person.
   - Add a proof row for each.

8. **Row e's body states the old rules. Not blocking c; needed before e.** This is §2's second bullet. **Smallest change:** rewrite row e's body to §6.1's six things and their distinct outcomes, and drop "as `TakeHeld` does", "at the send-back turn's end" and "findings wait … while words are queued".

9. **A submitted graph candidate whose tip moves before it lands. Not blocking c; needed before f.**

   §4.5 covers two presses (`design:390-392`):
   - a press beyond the last landed candidate submits candidate *n* + 1;
   - a press on the tip already submitted submits nothing.

   It does not cover a press at a new tip while *n* + 1 stands submitted and unlanded: the press was refused because an opinion was asked, then the person committed again. Is that a second *n* + 1, an *n* + 2, or *n* + 1 superseded? And what becomes of the `opinion`, `look` and `check` already judged on the earlier tip? **Smallest change:** one sentence in §4.5 and one row in row f's proof.

10. **Stale and wrong statements. Not blocking.** This is §2's fourth bullet. **Smallest change:**
    - drop "Codex's read stays owed" (`:708`);
    - say at `:999` that `src/` is identical to `7cda79f01`'s;
    - narrow the re-pin claim at `:20` to §17's table, or re-pin §4 to §7 (for example `DriverContinuations.cs:660`).

11. **The router has a duplicate row. Not blocking.** Delete `docs/README.md:143`.

12. **Row a's correction proves less than Codex asked. Not blocking.**

    Codex asked for a chat that took a quest contributing run facts, with the worker, the tree, the opinion and the landing all asserted (reads `:427`, `:480`). The new cases cover a `From` continuation of a worker (`WorkflowRunTests.cs:641`, `:672`), and fall short in three ways:
    - no CHATTAKE1 chat's facts are tested;
    - no opinion fact is tested, though `RunAsync` reads it through the same record (`WorkflowRun.Read.cs:248-253`);
    - the tree asserted is also the worker's, so the test cannot tell that the continuation contributed it.

    The design defers the opinion to row i (`design:737-738`), but row i proves the doors, which are another reader. **Smallest change:** one `ReadAsync` case for a CHATTAKE1 chat under a rule naming an opinion, asserting its tree, opinion and landing, before row c builds on the same work records.

**12 findings, 1 blocking row c.**

**Verdict:** row c may be built after named changes the steward can check. The required change is finding 1: §4.2's paragraph on a chat run's landing, with its three proof rows. Findings 2 and 3 are row c's own text and should go in the same edit; none of the three needs another read. The amendment otherwise answers Codex's eight blocking findings in the text, not only in §17's table, and the answers hold against main, including the built correction for finding 6. The exception is finding 7, whose answer swaps the gate on later steps but not the landing step's own reading of live records and tips. Findings 4 to 8 must land before rows d and e and finding 9 before row f; 10 to 12 are record fixes the steward can make at any time.
