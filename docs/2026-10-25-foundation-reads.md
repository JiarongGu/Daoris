# FOUNDATION1: the reads of round 1 (D191)

The independent Opus 5.5 reader's foundation read and Codex's read of `docs/2026-10-25-foundation-design.md` and D212 at `c8eccf30e`, each run by the steward from outside the install, read-only, neither seeing the other's, verbatim but for their headings set one level down; round 2 answers every finding by number in the design's §12.

## Opus (foundation), round 1

The independent Opus 5.5 reader's foundation read of round 1 at `c8eccf30e`: 20 findings, 3 blocking; accept after named changes.

## FOUNDATION1 round 1: the independent Opus reader's foundation read

Read at `c8eccf30e`, whose `src/` is `92ac2bd7e`'s unchanged (`git diff 92ac2bd7e HEAD -- src` is empty), and LIMITSWITCH1's build at `3fdc6bbeb` (not an ancestor of HEAD). Paths use the design's aliases (`Driver/`, `Modules/`, `Core/`, `Mcp/`, `Web/`, `Chat/` for `Driver/Conversations/Chat/`). Read, not run.

### 1. Claims about the code

**What holds.** Every row of §0.2 I checked holds as written: `ChatRetry` at `ChatRunner.cs:1666`; the rule at `ResumeAccount.cs:78`, `HarnessRoster.cs:1008` and `LimitWaits.cs:186`, the chat's `walks: false` at `ChatGoOn.cs:571`, `:672` turning `Elsewhere` into `CannotGoOn` at `:581`, `:682`, the ended move's guard at `LimitMove.cs:213`, and `switches` at `3fdc6bbeb` read only for an open or taken quest; `agent = handedQuest?.Agent ?? config.Adapter` (`Driver.cs:1917`) with `GoOns.ResumesOwn` (`DriverContinuations.cs:40-45`) and the write-through (`ActorChoices.cs:171-177`); both earliest resets over one agent (`HarnessRoster.cs:1728`, `LimitWaits.cs:199`); `notPushed` (`Web/bridge/state/slices.ts:309`); the fail-open mark (`SaidDoor.cs:50-62`, `:84-90`) and the key answered only while the word waits (`Core/Sessions/SessionLedger.cs:955-962`); the held word's losses (`Modules/Loop/SessionWords.cs:179`, `:464`, `:920-929`, `:701`, `:782`; `SessionSay.cs:197`); `Actor` as provenance (`Core/Sessions/Sessions.cs:195-204`) with D203 accepted 2026-10-23 and its Ask Daoris refusal at point 6, not 10; the shell's come-up before the lease (`Modules/Loop/DriverLoop.cs:777`, `:841`, `:1224`); no recorded account pick (`ResumeAccount.cs:15-17`) and the one pick rule (`LimitMove.cs:447`); `BriefedMove.cs:133` opening on its own account; 角色 on both screens (`zh/settings.daoris.json:108`, `glossary.json:475`, `zh/settings.ai.json:28`); *line* and *session* (`glossary.json:530`, `:482`); the lineage folding unreadable as none (`ChildLineage.cs:134-137`). The synthesis is also right that Codex's pick reading is the decided one: the actors design §3.5(d) (`docs/2026-10-23-actors-design.md:469-475`) says *a pick names its agent and account, never moved* and that Chat's agent-only pick *walks that agent's accounts*.

**What is wrong or short.**

1. **§4.1, the triggers are narrow, so the look's bookkeeping mints nothing.** `AFTER UPDATE OF` fires on the SET list, not on a change. `SetStateAsync`'s one statement sets `state`, `limited` and `said` on every move (`Core/Sessions/Sessions.cs:899-902`), and `MirrorAsync`'s upsert sets `state` and `limited` on every mirror pass (`:931-936`). As written, every note-only move and every teammate sync mints `part.*`. Only the existing words trigger guards with a `WHEN` (`:676-677`).
2. **§4.2, `class` with the driver's `SessionEnded` flags as its first source.** Those flags are the look's in-memory conclusions (`report.Concluded`, `Attention.cs:250-252`). A trigger in the service cannot read them. The same holds for `part.opened`'s `kind` (a .NET rule) and `part.parked`'s `for` (read from the question and go-ahead records).
3. **§2.1 and row c, the sessions slice's allow-list gains `kind`.** It already pushes `kind` (`Modules/Loop/SessionsStateSlice.cs:41`), meaning driven or chat. The page types it `kind?: 'driven' | 'chat'` (`Web/data/api.ts:445`) and reads it (`Web/work/identity.ts:65-66`).
4. **§3.4 rule 2, the say door answers today's `continued` refusal.** The service has no such refusal (`SessionSayRefusal`, `SessionLedger.cs:89-175`). `continued` is the desktop doors' code (`SessionSay.cs:35`, `:45`; `SessionWords.cs:555`), and `ContinuedIn` follows chats alone (`SessionSay.cs:54-58`). The service also already walks a head of its own, a `from` chain over every kind, for a partner's answer (`Core/Sessions/PartnerDesk.cs:114-120`, `ContinuedByAsync`). That is a third rule beside the design's *two*, and it can disagree with the quest's last run for a driven line.
5. **§3.4 rule 3 and row d, the three moves that deliver words today.** There is a fourth. The briefed move reads the person's words before its open (`BriefedMove.cs:99`) and takes them naming the new part (`:158`), and `NotOfferedAsync` does the same (`:179`, `:250`).
6. **§3.2, Ask Daoris's new part opened *as D205 §3.5(e) already opens it*.** (e) opens it only at a limit with no account of its agent left. The code names that as *the one exception* to *Ask Daoris's own conversations never go on elsewhere (D137 §2.2)* (`HandOver.cs:52-57`; `CurveMove.cs:70-71`; D205's R3-B2 row).
7. **§3.2, a closed quest's session going on as a conversation is *the path its lost conversation takes today*.** It is that path only for a conversation proven lost (`Driver.cs:2374-2381`, SESSCTX1c, *the steward's choice*). For everything else the code's rule is *Nothing carries the words on by itself from a closed quest's session (D137 §2.2)* (`Driver.cs:2367-2373`). LIMITSWITCH1's own `switches` documentation at `3fdc6bbeb` keeps that wait.
8. **§0.2's LIMITSWITCH2 row and §5.1.** The filed row (`3fdc6bbeb:TASKS.md:415`) also says *a pick and Ask Daoris's room keep the own account*. The design reports only the `BriefedMove` half, and it reverses the Ask Daoris half.
9. **§3.1, preconditions on the record's revision catch a change under the decision.** A word kept with no note moves no revision (`Sessions.cs:1565-1574`).
10. **§3.3, every move writes its record before the old part ends.** That cannot hold for a move from an ended record: the ended limit move has *no part to end* (`LimitMove.cs:191-196`), nor does any go-on of an ended record.
11. **§8, *an account the person named stays pinned*.** §3.2 step 3 sends a hand-over's named account to the walk. Today a conversation's hand-over opens on it as a pick (`Chat/ChatHandOver.cs:157`, `request.Account`), refused while it cools.
12. **§2.3, a reviewer's and a voice's mutations refused at the server.** Their answers are mutations: `opinion_give` (`OpinionPermissions.cs:29`) and `brainstorm_give`/`brainstorm_synthesize` (`BrainstormPermissions.cs:22-25`). The role filter classes those as refused to a partner (`Mcp/RoleFilter.cs:44-52`).
13. **Row a, *(driver, web)*.** `accountPicked` is a machine-local record field. Its chain (`.claude/knowledge/session-record-fields.md` rows 1-12) runs through the service's record, column, create, reader, the ledger's opens, the host's request, route and loopback response, the driver's write and read, three stand-ins and the web type. Row f's `opens` is the same.
14. **Row 1h cites §7 row 15.** That row is the notices' writers.

### 2. The structure

**Who acts.** The target collapses the readings of the kind into one rule, twinned with a fixture. It also collapses five rule shapes into one typed policy, admission into four tables, and four walks, two pass-code sets and five re-asks into one start plan. Nothing a test or decision holds is lost, provided the kind is read from the marks (D203 point 1) and `actor` stays provenance (D205), as chosen.

It leaves duplicated:
- the word's three author fields, with a partner's answer still written as a helper's (`PartnerDesk.cs:93-96`; survey §1.3 row 18), which rule 6's `Carries` and the events' `by` both read;
- the two hand-kept actor-id lists (`ActorChoices.cs:37`, `ActorRun.cs:176-177`; row 9);
- a third, SQL copy of the kind rule, if `part.opened` mints `kind` in a trigger.

It gets the pushed field's name wrong (finding 5) and over-words the reviewer and voice change (finding 15). Authority holds: the call layer fails closed only for a session Daoris names, the holder's proof is untouched until row t, and rows q, r and t wait.

**How work moves.** One pure `Course.Decide` with `LineAccount` and one move record is the right collapse. Executors stay, and the hand-over's two timings are kept as a difference of door. Four things break or are lost.

- **Authority.** Row a starts parts that today wait for the person's press, and claims LIMITSWITCH1 for them. Those are an Ask Daoris conversation going elsewhere outside a limit, a closed quest's session going on as a new conversation, and a conversation whose own account is signed out, refused, gone or kept (today `CannotGoOn`, *since nothing carries a chat's words on by itself*, `ChatGoOn.cs:566-569`). LIMITSWITCH1's text reaches only *an ended record whose own account is cooling* (finding 1). The hand-over's named account goes to the walk, against §8 (finding 9). The course's hand-over rows do not say that an agent's ask and the curve pass the hand-over rule's consent before `Move` (finding 14).
- **Words.** Rule 4 keeps a word on a winding-up or moving record. Today's come-up take-up and sweep resume such a word as an ended chat's (`ChatGoOn.cs:191-262`), which restarts the old part in the tree its successor holds. That is why the shell never keeps it there (`SessionWords.cs:455-458`). The course takes *a move record standing* as an input, but no answer reads it (finding 2). Three narrower gaps remain:
  - recovery in `delivering` re-keeps a word the new part already took, until receipts land (finding 11);
  - the briefed move still reads its words before its open (finding 7);
  - a word said to the new head can come before older moved words (finding 19).
- **Stores.** The move file has one writer per kind, which is right. But the cross-kind move row a adds (a driven record going on as a conversation) names no executor. The ended move, LIMITSWITCH2's path, keeps its double walk (finding 8).
- **Duplication left.** There are three head rules (the driver's `ContinuedIn`, its `Superseded`, and the service's `PartnerDesk` walk; finding 6). `LineAccount` and the course's account facts coexist until 1h and 1i. The boxes sit beside the move file until row v.

**What is said.** The envelope, the two minters, the three orders, one end with four declared filters, and a durable said-set in place of baselines are the right collapse. The rejection of diff-minting and of a page frame are right on the code. Three things go wrong.

- **An event lost.** Rows l and m move `SESSION_ENDED`, the usage wake and news onto the journal. A shell that does not hold the lease builds no watch and hears no change stream (`DriverLoop.cs:841`, `:895`). Today a conversation's end reaches it in-process from the routes' `onEnded` (`Modules/Bridge/Routes/ConversationRoutes.cs:55`, `DriverLoop.cs:1233`) whoever holds the lease. The design corrects exactly this for moves (§0.2) and not for events (finding 3).
- **An event told for nothing.** The triggers as described fire on bookkeeping and on every mirror pass (finding 4). A park may also be told as both `part.parked` and `person.waiting` unless the latter is kept to waits no record holds.
- **A store with two writers.** `data/notices.json` already takes writers in two processes under `KeepLock` and keeps keys it did not write (`Driver/Platform/Notices.cs:12-17`), so *one owner* holds as a class. But one journal cursor beside it serves two tellers (the shell for its conversations, the lease's loop for driven work), so one teller can step the cursor past the other's news. Machine-event news (a held call, a Confirm) has no key that survives a run for the said-set to hold (finding 12).

### 3. The choices between the proposals

On the code the synthesis chose well on most questions:
- Q1: the kind from the marks, since D203 point 1 is accepted and `Sessions.cs:195-204` makes `actor` provenance.
- Q2: the four layers with X's typed policy; the brainstorm's `off` given no reader.
- Q3: one settled selection, a choice the live and ended moves' double walks prove right.
- Q6: one pure decision, keeping the chat's closing turn (`ChatHandOver.cs:145-150`).
- Q7: X's provenance rule, which is D205 §3.5(d)'s own text, so O's amendment is not needed.
- Q8: a driver file with an open key over service tables, since conversations run without the lease.
- Q9: the head judged inside the say door's step.
- Q11: trigger over diff and over the client.
- Q13: no new frame.
- Q16: 职务.

I would choose differently in five places:
- **Row a's scope.** Keep LIMITSWITCH1's decision as written (an ended record, driven or conversation, whose own account cools). Leave Ask Daoris, a closed quest's session and a conversation's non-cooling holds where D137 §2.2 and D205's *one exception* put them until each is decided.
- **The journal's contents.** Triggers write only the raw transition (record, column, old and new value, `said` length delta), guarded by `WHEN`. `EventCodes`, `class`, `kind` and `for` are judged in .NET at projection. That keeps D182 point 6's one judgement and adds no SQL twin, and a trigger written never to raise cannot fail an older writer's statement.
- **The news cursor.** One per teller, and news keyed by its subject's durable id, never by position.
- **The person's named account in a hand-over press.** A recorded account pick, as `START_CHAT`'s profile is. Only an agent's or the curve's named account goes first-then-walk.
- **The pushed field's name.** Not `kind`; the prose word *kind* can stay.

### 4. The rows

**Landing alone.**
- 1b, 1c (once renamed), 1e, 1g, 1k and 1u can each land alone behind today's tests.
- 1a lands only after LIMITSWITCH1 merges onto a main that has moved under its branch (43 files differ, AGENTERRORS1b's `ChatRetry` among them).
- 1d lands alone once its service refusal and every say caller's handling are named.
- 1f and 1p are ordered wrong for 1f's proof: *each word once* across a crash in `delivering` needs 1p's receipts or a mandatory mark on moved words.
- 1o must wait for every go-on path to read a standing move record (1h, 1i) and for the course's answer that makes the old part `Never`.
- 1l and 1m each move four or five readers at once (`SESSION_ENDED`, `session/ended`, the log line, *Ended* and the usage slice; the watch, three notices writers, every baseline and the notify rule), against the design's own *one path or channel per row*. Both need the journal read from come-up, not behind the lease.

**Proofs.**
- 1a's proof misses its field chain: `SessionStoreTests`' mirrored and elder cases, `ContinuesHostTests`, the feed's absence.
- 1c's proof asserts a `kind` the slice already sends.
- 1d's proof misses the briefed move.
- 1f's proof claims more than it can before 1p.
- 1k's *the look's bookkeeping mints none* is the right test and will fail as the triggers are described, which is useful.
- 1l lacks a shell beside a headless lease holder.

**The first row.** 1a is not the least change. As written it is the largest of the first three: a new record field across four processes, the ended move widened, the briefed move re-pointed, a single selection, a closed quest converted, and Ask Daoris moved. Its defect value is LIMITSWITCH2, since LIMITSWITCH1 is already built. The first row should be 1a's core:
- `LineAccount` over `ResumeChoice`, the roster's inline twin and `switches`;
- the chat's and the wake's cooling go-on through the ended move, with its double walk fixed;
- the earliest reset.

The account-pick field and the briefed move's walk follow as their own rows.

**Authority marks.** Rows q, r, t and v are marked. Not marked are row a's starts beyond LIMITSWITCH1 (finding 1), and 1h and 1i moving the hand-over and the curve onto the course without the consent stated (finding 14).

### 5. Findings

1. **Blocking.**
   - **What is wrong.** Row a reverses three decided *never by itself* rules and records none of them in §7, §8 or D212's amend list:
     - an Ask Daoris conversation going on elsewhere outside a limit (D137 §2.2 as `HandOver.cs:52-57` and `CurveMove.cs:70-71` cite it; D205's R3-B2 *one exception*; D163's MIXAGENT1a-i and 1b-i notes; LIMITSWITCH2 as filed);
     - a closed quest's session carried on as a new conversation for a cooling account (D137 point 4 and its design §2.2, `Driver.cs:2367-2373`; SESSCTX1c only for a lost conversation);
     - a conversation carried on where its own account is signed out, refused, gone or kept (`ChatGoOn.cs:566-569`).

     §8 rests all three on LIMITSWITCH1's cooling text, and §3.2 misreads D205 §3.5(e).
   - **Smallest change.** Cut the three from row a, keeping LIMITSWITCH1's scope with Ask Daoris and a closed quest's session waiting as filed. Otherwise add a §7 row and an amend-list entry for each and put them to the owner in §8, marking the part ⚖.
2. **Blocking.**
   - **What is wrong.** Rule 4 (row o) keeps words on a winding-up or moving record. Today's come-up and sweep resume an ended chat's waiting words (`ChatGoOn.cs:191-262`), which restarts the old part in its successor's tree (HANDWORDS1-i; `SessionWords.cs:455-458`). The course lists the move record as an input, but no answer reads it.
   - **Smallest change.** Add a course rule: a standing move record before `delivered` makes the old part's own go-on `Never`, its words the move's. State that move records are taken up before the sweep. Gate row o on 1h and 1i. Add HANDWORDS1-i's case with the shell restarted mid-hand-over, and an older shell's sweep, to its proof.
3. **Blocking.**
   - **What is wrong.** Rows l and m project `SESSION_ENDED`, the usage wake and news from the journal. A shell not holding the lease reads no change stream (`DriverLoop.cs:841`, `:895`), where today a conversation's end reaches it in-process (`ConversationRoutes.cs:55`; `DriverLoop.cs:1233`). Events are lost or late while a headless loop drives.
   - **Smallest change.** Each process reads the journal from come-up, as `ServiceConfirmations.RunAsync` runs (`DriverLoop.cs:787`), or the shell's conversation ends stay its hub's machine events. Add a Process case with a headless lease holder in row l.
4. **Not blocking.**
   - **What is wrong.** `AFTER UPDATE OF` fires on every `SetStateAsync` and mirror upsert (`Sessions.cs:899-902`, `:931-936`), and the trigger cannot read `class`, `kind` or `for` (`Attention.cs:250-252`).
   - **Smallest change.** Guard each trigger with `WHEN OLD.x IS NOT NEW.x`, state the rule for mirrors, write triggers that never raise, journal only raw transitions, and judge codes in .NET.
5. **Not blocking.**
   - **What is wrong.** `kind` is already the pushed and typed driven/chat field (`SessionsStateSlice.cs:41`; `api.ts:445`; `identity.ts:65-66`). D203 point 10's planned `role` in `sessions --json` also collides with `ROLE_ACT`'s `role` (`RoleActForm.cs:71-92`).
   - **Smallest change.** Give the participant's kind another wire and code name, and add D203 point 10 to the amend list.
6. **Not blocking.**
   - **What is wrong.** The service has no `continued` refusal (`SessionLedger.cs:89-175`), and it already walks a head for partners (`PartnerDesk.cs:114-120`).
   - **Smallest change.** Name the new refusal value and its readers (`ServiceClient`, `SessionSay.cs:207-209`) and every say caller's handling (survey §2.5's list). Use one service head function for the say door and `PartnerDesk`.
7. **Not blocking.**
   - **What is wrong.** Row d omits the briefed move and `NotOfferedAsync`, which read words before their open (`BriefedMove.cs:99`, `:158`, `:179`, `:250`).
   - **Smallest change.** Take row d's set from the census of every site that opens `from` and hands words.
8. **Not blocking.**
   - **What is wrong.** The ended move, LIMITSWITCH2's path in row a, walks at `LimitMove.cs:237` and opens on `profile: null` at `:256`, walking again and counting a start never made. Row a fixes only the live move (`:86`, `:142`).
   - **Smallest change.** Carry the selection to that open too.
9. **Not blocking.**
   - **What is wrong.** A hand-over's named account goes to the walk (§3.2 step 3), against §8 and today's pick (`ChatHandOver.cs:157`).
   - **Smallest change.** Record the person's named account as an account pick. Only an agent's or the curve's goes first-then-walk. Add a §7 row.
10. **Not blocking.**
    - **What is wrong.** Row a is the largest first row. Its lanes omit the service, and its proof omits `accountPicked`'s field chain (`session-record-fields.md`).
    - **Smallest change.** Split row a: (i) `LineAccount` and the cooling go-on for driven records and conversations; (ii) account-pick provenance with its chain and tests; (iii) the briefed move's walk and the single selection.
11. **Not blocking.**
    - **What is wrong.** Row f claims each word once across a kill in `delivering`, but the producer key dedups only while the word waits (`SessionLedger.cs:955-962`) and the mark fails open (`SaidDoor.cs:50-62`).
    - **Smallest change.** Make the mark on moved words mandatory in row f, or scope its proof until row p lands.
12. **Not blocking.**
    - **What is wrong.** One cursor serves two tellers, and machine-event news has no durable key for the said-set.
    - **Smallest change.** Keep a cursor per teller and key news by the subject's durable id.
13. **Not blocking.**
    - **What is wrong.** The preconditions miss a word kept with no note (`Sessions.cs:1565-1574`).
    - **Smallest change.** Add the words' state to the preconditions, or the executor's re-read, as `DriverContinuations.cs:346-355` does.
14. **Not blocking.**
    - **What is wrong.** The course's hand-over rows do not state the hand-over rule's consent for an agent's ask or for the curve (`CurveMove.cs:114-117`).
    - **Smallest change.** Say that each reaches `Move` only once the rule admits it, else `Hold` for the person, with a census case per door in 1h and 1i.
15. **Not blocking.**
    - **What is wrong.** *A reviewer's and a voice's mutations refused* would refuse `opinion_give` and `brainstorm_give`/`brainstorm_synthesize`.
    - **Smallest change.** Reword it as *the tools its handed rules refuse*.
16. **Not blocking.**
    - **What is wrong.** Rows l and m each move several readers at once.
    - **Smallest change.** Move one reader per branch, as 1h and 1i are cut.
17. **Not blocking.**
    - **What is wrong.** Survey §1.3 rows 9 and 18 have no target (`ActorChoices.cs:37`, `ActorRun.cs:176-177`; `PartnerDesk.cs:93-96`).
    - **Smallest change.** Name one reader of a word's author and one list of actor ids, or say why they stay.
18. **Not blocking.**
    - **What is wrong.** *Evaluating counts nothing; the launcher consumes the reservation* would stop the spread of starts within one look (`HarnessRoster.cs:894-897`).
    - **Smallest change.** Take the reservation at the settled selection under the roster's gate. Only probes are uncounted (`counts: false`, `:911`).
19. **Not blocking.**
    - **What is wrong.** The head moves at the open before moved words are delivered, so a newer word can come before them.
    - **Smallest change.** Keep moved words ahead of words kept on the new part after its open, with a test.
20. **Not blocking.** Text fixes:
    - row 1h's *§7 row 15* is the wrong row;
    - *before the old part ends* fails for moves from an ended record (`LimitMove.cs:191-196`), so let such a move start at `old-ended`;
    - *the channel's quest lines take* the quest log's names must be a mapping at projection, since the channel's codes travel to teammates (`Core/Asks/Channel.cs:110-116`).

20 findings, 3 blocking.

**Verdict.** Accept after named changes the steward can check without another read. The shape is right on the code: a participant read with its kind from the marks, one pure course with one account rule and one move record per kind's executor, and one vocabulary minted at the commit with three orders kept apart. Its choices between the proposals are mostly the better ones. The three blocking findings are bounded edits checkable against the cited lines:
- cut row a's three extensions back to LIMITSWITCH1's cooling scope;
- add the course's rule that a standing move record makes the old part `Never`, and gate row o on it;
- read the journal from come-up in every process, with its Process case.

If the steward instead keeps finding 1's extensions in row a, those need their own decision (the owner's under D191 point 3, or dated notes on D137, D163, D205 and D189 that both makers read), and they are not covered by this acceptance.

## Codex, round 1

Codex's read of round 1 at `c8eccf30e`: 18 findings, 16 blocking; another round.

Reviewed HEAD `c8eccf30e`, read-only; no tests run. References use the synthesis’s path aliases; **F** means `docs/2026-10-25-foundation-design.md`.

#### 1. Claims about existing code

1. **Blocking — containment is best-effort.** F:208 says containment is unchanged and “nothing starts uncontained.” `Driver/Platform/AgentStarts.cs:13` and `Driver/Agents/Consoles/AgentJobs.cs:119` explicitly allow ordinary starts without containment; only pieces fail closed. Preserve that exception or propose a separately reviewed change to D201.

2. **Non-blocking — held calls do not survive their owning process’s restart.** F:598 and F:709 describe a held call standing across a restart and being announced again. `Modules/Loop/HeldCallAnnouncements.cs:22` explicitly says the opposite: these permissions exist only in that process’s memory. Replace this proof with projection reconstruction while the owning process remains alive.

#### 2. Who acts

1. **Blocking — `kind` already has a wire meaning.** F:167 and F:685 propose adding `kind` for participant classification, but it already carries `driven`/`chat` in `Modules/Loop/SessionsStateSlice.cs:41` and `Daoris.Service.Http/ApiContracts.cs:400`. Replacing its meaning breaks unmigrated readers such as `Web/work/say.ts:90`; introduce a separate additive field, with an older-host fallback.

2. **Blocking — the connector cannot classify every caller from the ledger alone.** F:151 promises that, while F:174 includes recordless voices and children. A voice’s positive placement comes from `BrainstormScope` (`Core/Sessions/BrainstormBox.cs:13`), including its generator/synthesizer distinction, whose allowed result tool differs (`Driver/Brainstorms/Domain/BrainstormPermissions.cs:28`). Specify that contextual lookup before the unknown-caller fallback, its refusal codes, and the permitted result mutations; a blanket mutation refusal would prevent today’s brainstorms from answering.

#### 3. How work moves

1. **Blocking — `CourseFacts` omits existing decision inputs.** F:274 lists the newest part and waiting authors, but the answers-alone guard reads events across preceding parts (`Driver/Conversations/GoOn/AnswersAloneHold.cs:30`, `:109`); the chat applies it before resumption (`Chat/ChatGoOn.cs:505`). Add the bound, release grounds and relevant execution boundary to the contract, or explicitly retain them as mandatory executor guards.

2. **Blocking — “start-from” is not an ordinary successor move.** F:303 puts it under `Move`, whose record is keyed by the source part. Today it calls `StartAsync` without `from` (`Chat/ChatRunner.cs:339`), producing an independent entry (`Core/Sessions/SessionLedger.cs:698`). Represent this distinction explicitly, including a fresh intent key, so repeated independent starts neither reuse one destination nor supersede the source.

3. **Blocking — the account rule has no answer when no reset exists.** F:331 and F:651 offer `Wait(earliest, account)` when nothing is ready, while excluding every signed-out, missing, refused or unoffered candidate from that minimum. Today `HarnessRoster.Unready` preserves a non-cooling refusal in precisely this case (`Driver/Accounts/Harnesses/HarnessRoster.cs:1732`). Add a non-timed hold with the required repair; do not manufacture a reset or promise automatic resumption.

4. **Blocking — the head rule misses driven-to-chat succession.** F:383 retains separate conversation-chain and quest-last-run rules, while row a adds more closed-quest transitions into chats. `WordsNever.ContinuedIn` only traverses from a chat (`Driver/Conversations/GoOn/SessionSay.cs:56`), and `LastOwnForQuestAsync` excludes successor chats with no quest (`Core/Sessions/Sessions.cs:1045`). Define one traversal across that boundary and prove a late word to the original driven part reaches the final chat.

5. **Blocking — executor kind is not an exclusive move claim.** F:359 names the recovering executor but gives no conditional claim, generation or ownership handoff; today’s `GoingOn` exclusion is process-local (`Chat/ChatContext.cs:68`). F:365 also restores the approximate `from`/time search that my proposal rejected, although successors are not unique by `from` (`Core/Sessions/Sessions.cs:998`). Require a fenced claim and atomic idempotent open; against an older host, an unresolved open must remain held rather than receive the “never twice” guarantee.

6. **Blocking — transfer keys break producer deduplication.** F:380 preserves the producer key, but F:387 keeps the destination word under `move/<old>/<word>`. Today deduplication searches only the addressed record’s `SaidWord.Key` (`Core/Sessions/SessionLedger.cs:956`), so retrying the original producer key at the new head can create another word. Keep logical producer identity separate from transfer identity, and commit its receipt atomically with removal; define the bounded receipt horizon.

7. **Blocking — “never attempted again” conflicts with carrying limit-refused turns.** F:395 prohibits every second attempt, while F:402 carries limit-refused words that already passed the attempt and wire (`Chat/ChatTurns.cs:1225`; `Chat/LimitWaits.cs:223`). State distinct dispositions for definite limit refusal, successful delivery and uncertainty, including the transient-then-limit case. Also require acknowledgement of each actual mark: today a successful attempt response can contain unmarked words (`Core/Sessions/SessionLedger.cs:1234`).

#### 4. What is said

1. **Blocking — column triggers alone neither identify transitions nor wake every reader.** F:430’s `AFTER UPDATE OF state` would also fire for same-state writes because `SetStateAsync` always names `state` and `said` (`Core/Sessions/Sessions.cs:899`). Conversely, taking words changes neither the session revision nor the words-family counter (`Sessions.cs:1605`, `:677`), so F:437’s existing family wake can miss a journal append. Specify OLD/NEW predicates, per-word differences and a journal wake that covers every append without waking on bookkeeping.

2. **Blocking — retention and loss are not yet a contract.** F:433 promises a bounded journal without a count, byte/payload bound or trimming algorithm; F:511 says nothing is lost and F:609 says records retain the trimmed facts. Session state and notes are overwritten (`Core/Sessions/Sessions.cs:899`), so a completed park/answer transition cannot necessarily be reconstructed after trimming. Define retention, monotonic positions, floor reporting and reconciliation’s explicitly incomplete history, then give row k measurable landing limits.

3. **Blocking — a durable said-set does not guarantee exactly-once announcements.** F:518 promises both an untold park announced after restart and no repeated balloon, but does not order the durable claim, cursor and external emission. Existing notices serialize file edits (`Driver/Platform/Notices.cs:187`), not notification delivery, and are pruned or dismissed (`:207`, `:174`). Specify an at-most-once announcement policy and its crash-loss window, with deduplication evidence retained independently of visible notices.

4. **Blocking — journal durability is being confused with relay delivery.** F:710 promises that a restart loses no session event, while EVENTHOOK1 explicitly provides no replay and loses queued events with loss reporting (`docs/2026-10-24-event-hook-design.md:584`). No persistent relay source cursor, acknowledgement or replay boundary is specified here. Either amend that delivery contract concretely or limit the claim to retained journal history; also declare which shell-local machine events remain unavailable to the lease-owned plugin stream.

#### 5. Migration

1. **Blocking — row f’s proof depends on later rows o and p.** F:702 requires killing the shell at every phase and delivering every word once, while durable move-time keeps arrive at F:711 and mandatory attempts/receipts at F:712. Today `ChatTurns.KeepAsync` refuses its held state (`Chat/ChatTurns.cs:192`), and `SaidDoor` deliberately sends after marking fails (`Driver/Conversations/SaidDoor.cs:50`). Move those prerequisites earlier, or narrow row f’s guarantee and identify the later row that establishes it.

2. **Blocking — executable rollback adapters create two recovery authorities.** F:367 keeps old boxes beside the new move file, but the old limit recovery actively reconstructs and queues delivery (`Chat/LimitMove.cs:352`), while hand-over recovery consumes and clears its box (`Conversations/HandOver/HandOver.cs:292`). Define downgrade and subsequent upgrade reconciliation at each phase, including completion evidence an older build leaves; additive files alone cannot prevent replay. Keep the correctly stated two-maker gates for q/r/t/v, and retain SWITCHCTX1’s own outstanding read before its carry rows.

#### 6. Disagreements with synthesis choices

1. **Non-blocking — the rejection of service-owned move persistence misstates my proposal.** F:624 rejects it because moves perform desktop actions, but my proposal explicitly leaves execution semantics and harness access in the driver (`docs/2026-10-25-foundation-proposal-codex.md:215`). I still prefer conditional local-service persistence for claims, destination allocation and word transfer; files are acceptable only after supplying equivalent fencing and recovery guarantees. I otherwise accept the mark-derived kind, separate loops, trigger capture, and omission of a move record for same-conversation resume once durable delivery owns that recovery.

**Verdict: another round.**
