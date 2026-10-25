# FOUNDATION1: the reads of rounds 1 and 2 (D191)

The independent Opus 5.5 reader's foundation reads and Codex's reads of `docs/2026-10-25-foundation-design.md` and D212, round 1 at `c8eccf30e` and round 2 at `29a066182`, each run by the steward from outside the install, read-only, neither seeing the other's, verbatim but for their headings set one level down. Round 2 answers every finding of round 1 by number in the design's §12, and round 3 every finding of round 2 in its §15.

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

## Opus (foundation), round 2

The independent Opus 5.5 reader's foundation read of round 2 at `29a066182`: 14 findings, 6 blocking; accept after named changes.

### 0. Both reads of round 1

Read at HEAD `29a066182`. Its `src/` differs from `c8eccf30e`'s only by LIMITSWITCH1 (merged at `a85004bd0`) and KEEP1a (`git diff --stat c8eccf30e HEAD -- src`: 21 files, among them `HarnessRoster.cs`, `ResumeAccount.cs`, `Driver.cs` and `LimitResume.cs`). So every line the design cites from `c8eccf30e` holds, give or take the drift it names (`ResumeAccount.cs:87`, `HarnessRoster.cs:1014`, `Driver.cs:2368`, all confirmed). Line numbers below are HEAD's. *design:n* is a line of `docs/2026-10-25-foundation-design.md`. The paths use the design's aliases. I read the code but ran nothing.

#### The Opus reader's foundation read (20 findings, 3 blocking)

| # | Round 1 | Round 2 | Why |
|---|---|---|---|
| 1 | blocking | **Taken in part** | Two holds now go to the owner as §8's questions, and that reason holds: Ask Daoris outside a limit (`HandOver.cs:52-57`, `CurveMove.cs:70-71`), and a conversation whose account is refused, signed out or gone (`ChatGoOn.cs:566-581`). *Kept* is a hold of driven records only (`ResumeAccount.cs:31-36`). The closed quest's half stays in row a-i on the steward's reading, as a dated note on D137 point 4. Round 1 allowed exactly that route. But its stated reason does not hold (finding 7), and its executor cannot carry the case before a-iii (finding 5). |
| 2 | blocking | Taken | A standing move now makes the old part `Never` (§3.1). Take-up runs before every other come-up step (`ChatGoOn.cs:195`, `:213-257`, `:260`). `TakeWaits` reads the move record (`SessionWords.cs:544-545`). Row o lands after h and i, and its proof carries HANDWORDS1-i's case. |
| 3 | blocking | Taken | Each process reads the journal from its come-up, as `ServiceConfirmations.RunAsync` already does (`DriverLoop.cs:787`, `:841`). Row l carries the Process case. |
| 4 | — | Taken | Triggers are guarded by OLD/NEW, journal only raw transitions, write nothing on a mirror pass and never raise. The codes are judged in .NET. |
| 5 | — | Taken | `work` is additive beside the pushed `kind` (`SessionsStateSlice.cs:41`). The page falls back when a host sends no `work`. D203 point 10 is in the amend list. |
| 6 | — | Taken | `Lines.HeadOf` serves the say door, `PartnerDesk` and `HelperDesk`. A new `Continued` refusal comes with its readers. An older driver reads a `!ok` refusal as a failed say (`ServiceClient.cs:511`). |
| 7 | — | Taken, wider | Row d's census covers the briefed move, start-from and a refused open. |
| 8 | — | Taken | The ended move's double walk is row a-iii. |
| 9 | — | **Answered, and the reason holds** | The person's named account on a press is now a recorded pick. Splitting the other doors was withdrawn, rightly: today every door opens its named account as a pick (`ChatHandOver.cs:156-157`, `request.Account`; `CurveMove.cs:114-115`). |
| 10 | — | Taken, as named | Row a is split into a-i, a-ii and a-iii. But a-i gained a case that needs a-iii (finding 5). |
| 11 | — | Taken | Row p moves before row f, and row f's proof is scoped. |
| 12 | — | Taken | Each teller has its own cursor, and news is keyed by a durable id. |
| 13 | — | Taken | The words' state is a precondition. |
| 14 | — | Taken | Each door keeps its own gate, and h and i add a census case per door. |
| 15 | — | Taken | The wording is now *the tools its handed rules refuse*. |
| 16 | — | Taken | Rows l and m are split into l-i to l-v and m-i to m-v. |
| 17 | — | Taken; **reopened by §2.7** | The "one list" of ids now conflicts with configured role ids (finding 3). |
| 18 | — | Taken | The reservation is counted at the selection, and only probes are uncounted. |
| 19 | — | Taken | Moved words go ahead of later words, with a test. |
| 20 | — | Taken | All three text fixes landed. The quest line's reason was corrected (`Channel.cs:106-116`, `:1121-1133`). |

#### Codex's read (18 findings, 16 blocking)

| # | Round 1 | Round 2 | Why |
|---|---|---|---|
| 1.1 | blocking | Taken | Containment is D201's best effort (`AgentStarts.cs:13`). |
| 1.2 | — | Taken | A held call is told once while its process lives. |
| 2.1 | blocking | Taken | The new field is `work`, additive. |
| 2.2 | blocking | Taken | A voice is placed through `BrainstormScope` first (`BrainstormBox.cs:13`, `:29`), and each voice is served its own result tool (`:71-74`). |
| 3.1 | blocking | Taken | The answers-alone hold and the boundary are course facts. |
| 3.2 | blocking | Taken | `Start` has no `from` and its own intent key (`ChatRunner.cs:339`). |
| 3.3 | blocking | Taken; **over-reaches** | `Hold(reason, repair)` was added, but its account set leaves out the record's own account (finding 4). |
| 3.4 | blocking | Taken | One head across kinds. |
| 3.5 | blocking | Taken in substance | The claim is fenced and the open is atomic under `opens`. The file's compare-and-replace names no lock across processes (finding 12). |
| 3.6 | blocking | Taken | The producer key travels with the transfer id, in one transaction. |
| 3.7 | blocking | Taken | Dispositions are kept, and the client reads the unmarked list (`SessionLedger.cs:1234-1246`; `ServiceClient.cs:569`). |
| 4.1 | blocking | Taken | OLD/NEW guards, word-by-word diffs, and a `journal` family. |
| 4.2 | blocking | Taken | 50,000 rows, the floor, *incomplete since*, and row k's landing limits. |
| 4.3 | blocking | Taken | Told at most once, the claim made before the balloon. |
| 4.4 | blocking | Taken | The claim is limited to the retained journal. |
| 5.1 | blocking | Taken | Row p before row f, and row f's guarantee scoped. |
| 5.2 | blocking | **Deferred, with a reason that holds** | The principle is stated: one recovery authority per phase, and the newer build reconciles to an older build's evidence. The per-phase table moves to row f, whose proof is a downgrade and re-upgrade at each phase. |
| 6.1 | — | Taken in part; the reason holds | The rejection is reworded. Files stay, with Codex's fencing. |

### 1. Claims about the code

1. **The compatibility case §11 and row x mark *unverified* is answered by the code, and it fails** (design:354, :946). Every continuation passes the record's `actor` into its run: `Driver.cs:3666` (`actor = prior?.Actor ?? actor`), `DriverContinuations.cs:432`, `ChatGoOn.cs:645`, `:722`, `ChatRunner.cs:1398` and `LimitMove.cs:433`. `ActorRun`'s constructor throws `unknown actor` for any id outside the eight (`ActorRun.cs:176-178`). On a driven or one-turn run it throws after `AgentStarts.StartAsync` has already started the harness (`Driver.cs:3651`, `:3673`). So an older build cannot continue a record whose `actor` names an added role. Nor can the newer build, unless row x changes that guard.
2. **§3.2 step 2, §8 and §12's answer to O-1 say the owner's reported case "was a closed quest's"** (design:439, :788, :962). Nothing the design cites supports this:
   - D137's note says only "a finished record's words" (`D137.md:791-799`), and says it amends point 7.
   - LIMITSWITCH1's build note leaves a closed quest's session as it was, "since nothing carries its words to a new part by itself" (`D137.md:814-816`).
   - The FIX-LOG entry records LIMITSWITCH1's open-or-taken switch as the fix of that very symptom (`docs/FIX-LOG.md:54-66`).
3. **§3.2 step 2 and row a-i say a closed quest's session goes on "through the path a lost conversation takes"** (design:435-437, :854-855). That path is `GoesOnLost` → `ChatRunner.GoOnLostAsync` → `BriefedMove.MoveAsync` (`DriverLoop.cs:926-927`; `ChatRunner.cs:243-246`). It opens on `record.Profile`, as a pick (`BriefedMove.cs:133`). A picked account that cools is refused, so this path fails exactly in the cooling case. The briefed move takes the walk only from row a-iii.
4. **§3.3 says a standing limit box's "words are delivered under the box's own key (`limit-move/<session>`)"** (design:507). Only the take-up's re-delivery is a keyed `said` word (`LimitMove.cs:378`). The first delivery is a live turn taken by `ChatHandOver.SayHandedOver` (`ChatHandOver.cs:238-250`, called at `LimitMove.cs:277`). What stops a second delivery across builds is the take-up's `User`-event check (`LimitMove.cs:366`), plus the key while the word waits. Since one home runs one build at a time, the guarantee holds; the design names only half of how.
5. **§2.7 models `role_propose` on "D205 point 9's `actor_propose`", and offers the Designer "on D205 point 7's suggestion card"** (design:339-340, :358). Neither exists in code. `actor_propose` is owed to MODELROLE1h (`HelpRoomOwed.cs:94-101`), and the card waits on MODELROLE1g's `ActorSuggestion` (`D205.md:389`), which does not appear in `src/`. Neither row is in `TASKS.md`. These are decided shapes, not exemplars in code, and row y-i depends on both.
6. **§2.5, §2.7 and row t describe today's act check as "the act checked against the appointed role's authority"** (design:291-292, :349, :918). The code checks more. It refuses an act outside the appointment's own `acts` (`StandingConfirmation.cs:1276`), which the owner ticked at the window's ask and which is held in memory. An appointment found at a start stays dormant until *Appoint again*, never re-made from the file (D209 points 1, 2 and 4). The design names that check nowhere.

The rest of §0.2's corrections and §12's code citations hold, from the spot checks I made:
- `SessionLedger.cs:89-175` has no `Continued` refusal;
- `:945-962` refuses a word for an active record that is not running, and answers a retry by key;
- `ResumeAccount.cs:31-36`;
- the LIMITSWITCH1 lines;
- `Channel.cs:106-116`;
- `Notices.cs:12-35`;
- `ChatGoOn.cs:195`, `:260`;
- `DriverLoop.cs:777`, `:787`, `:841`;
- `ChatContext.cs:64`;
- `LimitMove.cs:191-213`.

### 2. The structure

#### Who acts

The participant read collapses today's parallel paths:
- the kind, read as `work` from the marks by one twinned rule;
- five rule shapes into one typed policy;
- admission into four tables keyed by capability;
- four walks, two pass-code sets and five re-asks into one start plan whose selection carries to the open.

Nothing a test or decision holds is lost, because `work` is additive beside `kind`, `actor` stays provenance, and containment stays D201's.

Still duplicated, by design:
- Staffing has two homes: `actors[id]` for the default eight, `roles[].choices` for an added role.
- Each work now has two scoped configurations: a scoped role's choices, and the scoped rules (`helpers.agents`, `opinions.reviewers`). D205 point 6's split survives, and the rules still fence. But row x's census should show which one a repository reads when a scoped role names an agent the repository's rule does not list.

What it gets wrong:
- `design` (finding 9).
- Door-narrowing has no reader (finding 8).

#### The company (§2.7)

- **A configured role widening authority without the person's press.** Yes, as written.
  - A role's `authority` lives in `driver.json`, a file with several writers (`DriverConfigKeepsTests`; the steward's own acts write it, `StewardActRows.cs`).
  - The act door then reads it live, without the appointment's in-memory `acts` (§1 item 6). So editing a role's authority widens a holder already appointed (finding 1).
  - "The person's own press in the window" (design:341) does not exclude `role_propose`'s card or the terminal's confirmed verb. D208 point 1 excludes both for an appointment.
- **An appointed role taking work.** The field table does not stop it (design:325-329). Such a role would seat the dedicated holder in work orders, against D208 point 2's session that "does only the role". It also bypasses the per-role offered flags (`StandingConfirmation.cs:330`, `:336`, `:828`) and each act's scope kind (`:831-835`) (finding 2).
- **Escaping containment.** No. Containment is keyed by work, in code (§2.3; `AgentStarts.cs:13`), and no field of a role names it.
- **Taking an owner-only act.** No. The vocabulary excludes D209 point 3's list, and both the judge and the door refuse such acts. Their safety still depends on finding 1's intersection.
- **The default company changing anything on upgrade.** Nothing is written, but three readings change it:
  - D212's "a seed read where `roles` names nothing" (`D212.md:98`; design:331), read literally, drops the whole seed at the person's first added role (finding 6).
  - Removing a role holds a work that D205's judge says may not be off (`ActorChoices.cs:43`, `:55`) (finding 6).
  - Making "one list" of the seed's ids puts `supervisor` and `steward` in `ActorChoices.Ids`. That list feeds *Actors* (`ActorChoices.cs:320`) and `daoris driver actor` (`DriverChoicesCommand.cs:2059-2061`) (finding 3).
- **An older build continuing a record.** It throws on an added role's id (finding 3).

#### How work moves

One pure `Course.Decide`, one `LineAccount` and one move record per executor is the right collapse. Round 2's additions fit the code:
- `Start` for start-from;
- the answers-alone hold as a course fact;
- `Never` for a standing move's old part;
- each door's gate;
- the fenced claim with the atomic `opens`.

What it loses:
- **The own account's reset.** §3.2 steps 4-5 leave it out of the wait. Read literally, a record whose account cools while every other account is signed out holds for the person instead of resuming at its own reset. That is a behaviour D206 and LIMITSWITCH1 hold (`HarnessRoster.cs:1027`) (finding 4).

What it leaves duplicated:
- `LineAccount` beside the course's account facts until h and i;
- the boxes until v;
- `ContinuedIn` as a fallback;
- the ended conversation path, which walks one agent (`LimitMove.cs:235-241`; no `ElsewhereAsync`), where step 4 promises the role's later choices (finding 13).

What it gets wrong:
- the closed quest's path and its order (finding 5);
- its evidence (finding 7).

Authority: the only new automatic start is a closed quest's session going on as a conversation. It rests on the steward's reading of D137 point 4.

Words: rules 1 to 6 compose. With p before f and o after h and i, I found no word lost, doubled or replayed across a move, a crash or a restart, beyond the honest *uncertain* disposition. The one store question is the move file's cross-process write (finding 12).

#### What is said

Round 2's changes hold on the code:
- guarded raw triggers, which journal nothing for a mirror pass, an attempt mark or a note-only move;
- a `journal` family that wakes on every append;
- the journal read from each process's come-up;
- a cursor per teller;
- at most once, with the claim made under `Notices`' `KeepLock` before the balloon.

I found no event lost or told twice beyond the said crash window. Neither the journal nor `data/notices.json` has two authorities: the triggers are its one minter, and `Notices` is the notices' one owner.

### 3. The choices between the proposals

The round-1 choices I endorsed still stand on the code: Q1, Q2, Q3, Q6, Q7's provenance, Q8's files, Q9, Q11's trigger, Q13 and Q14. Of round 2's choices:

- **Q4, one configured record (the owner's direction).** This is right as one concept, but the record should be **two exclusive shapes**:
  - a working role: `takes`, `choices`, `scope`, `filled: always`;
  - an appointed role: `authority`, `scope`, `filled: appointed`.

  The judge should refuse a mixed record. A flat record invites finding 2.
- **Q1/Q15, `actor` naming the role.** I would keep `actor` to the seed's eight always-on ids. An added role's id goes in a new machine-local `role` field that no older build reads, as `compatible-builds.md` asks of new meaning (finding 3).
- **Q7, the closed quest's session.** I would not build it inside the first row on the steward's reading. Its executor needs a-iii's walk, and it reverses a *never by itself* rule the owner's note does not name. Put it in §8 beside the two questions, or land it in a-iii, saying it is the steward's reading.
- **Q16, 角色 for both.** Right for one concept. The two English labels (*Actors*, *Roles*) stay two words for it until y-i, which is said.
- **Q17, a-i first.** Right, for the owner's live defect class, provided a-i is cut back to the cooling rule for driven records and conversations, its earliest reset and its hold.

### 4. The rows

- **a-i** cannot land alone as written: its closed quest case fails until a-iii (finding 5). Its proof's "every account signed out" case contradicts, or encodes, finding 4. Cut back to the cooling rule for driven records and conversations, it lands alone on LIMITSWITCH1, and it is still the least change with the most defects removed (survey §2.4 rows 1, 11; LIMITSWITCH2).
- **b, c, a-ii, a-iii, d, p, e, f, g, h, i, o, j** land alone in their order.
  - c's lanes miss modules: `SessionsStateSlice` lives in `Modules/Loop/` (finding 14).
  - f's proof now proves what it claims, on p's receipts.
  - o's proof (an older shell's own sweep) proves the shape claim.
- **w** is read by nothing. Its proof should add the refusals of findings 2 and 6, and a catalogue with one added role that reproduces all eight seeded roles' starts.
- **x**'s proof "an older build's continuation of a record whose actor is an added role" will fail by construction (finding 3). Change where the id goes first.
- **y-i** refuses starts (removing a role holds its work) and adds an Ask Daoris door. By §10's own definition it is ⚖, and it depends on the unbuilt MODELROLE1g and 1h (finding 11).
- **k, l-i to l-v, m-i to m-v, n, s and u** land alone, and their proofs prove their claims.
- **q, r, t, v and y-ii** are marked ⚖.
  - t's text must keep the appointment's `acts` (finding 1).
  - y-ii's proof must show a live appointment not widened by a later edit to its role (finding 1).

### 5. Findings

1. **Blocking.** A role's authority can widen a holder already appointed, without the person's press (design:291-292, :327, :340-341, :349, :918-919; D212 point 16).
   - **What is wrong.** The act door checks only "the appointed role's authority", read from `driver.json`. Today it also refuses an act outside the appointment's own `acts`, taken from the window's ask and held in memory (`StandingConfirmation.cs:1273-1279`; D209 points 1, 2 and 4). An edit to `roles[].authority`, by any of `driver.json`'s writers, would therefore widen a live holder. And "the person's own press in the window" does not exclude `role_propose`'s card or a confirmed `daoris driver role`, which D208 point 1 excludes for an appointment.
   - **Smallest change.** Admit an act only where it is in all three: the code's act table, the role's configured authority and the appointment's recorded `acts`. Authority a role gains after an appointment takes effect only at the next *Appoint*. A role gains authority only through a `Window`-class route, as `SET_APPOINTMENT` is, never through `role_propose` or the terminal. Row y-ii's proof gains a live appointment that a later edit to its role does not widen.
2. **Blocking.** The two kinds of role are not exclusive (design:321-329).
   - **What is wrong.** The judge does not refuse a role `filled: appointed` that `takes` work. That seats D208 point 2's dedicated holder in work orders. An added appointed role also has no rule for the per-role offered flags (`StandingConfirmation.cs:330`, `:336`, `:828`) or for each act's scope kind. The supervisor is admitted only for a repository or a workspace (`:831-835`).
   - **Smallest change.** The judge refuses `takes`/`choices` on an appointed role and `authority` on an always-on role. Each act in the vocabulary carries its admissible scope kind and its offered flag in code. An added appointed role is offered only where every act it holds is. Row w's proof gains these refusals.
3. **Blocking.** Writing an added role's id into `actor` breaks continuation, and the "one list" changes the page on upgrade (design:336, :350, :909; D212 point 2).
   - **What is wrong.** Continuations read `actor` from the record (`Driver.cs:3666`; `DriverContinuations.cs:432`; `ChatGoOn.cs:645`, `:722`; `ChatRunner.cs:1398`; `LimitMove.cs:433`), and `ActorRun` throws on any id outside the eight (`ActorRun.cs:176-178`), after the harness started (`Driver.cs:3651`). Every build in the rollback window would fail every continuation of a Designer record. And making `ActorChoices.Ids` the seed's ten adds Supervisor and Steward to *Actors* (`ActorChoices.cs:320`) and to `daoris driver actor` (`DriverChoicesCommand.cs:2059-2061`).
   - **Smallest change.** `actor` keeps the seed's always-on id for the role's work (`build` for a Designer start). An added role's id goes in a new machine-local `role` field, through `session-record-fields.md`'s chain. The run reads `role ?? actor`. The "one list" is the eight always-on roles. Row x's proof runs an older build's continuation of a Designer record.
4. **Blocking.** The own account's reset is left out of the wait (design:449-462, :863).
   - **What is wrong.** Step 4's set is "the asked choice's other accounts", and step 5 waits for "exactly the accounts step 4 allows", holding where none of them has a reset. So a record whose own account cools while every other account is signed out holds for the person. LIMITSWITCH1 as built counts the own reset (`HarnessRoster.cs:1027`) and resumes there by itself, as D206 has it. Row a-i's proof case "with every account signed out it holds naming the sign-in", listed among a conversation's cases beside "signed out is `CannotGoOn`", encodes the regression or contradicts itself.
   - **Smallest change.** Step 5's set is step 4's plus the own account wherever step 1 may resume there. Split the proof case in two:
     - own account cooling, every other signed out: it waits for the own reset;
     - a driven record whose own account and every other are signed out: it holds, naming the sign-in.
5. **Blocking.** Row a-i's closed quest case cannot land in its order (design:435-439, :854-855).
   - **What is wrong.** `GoesOnLost` reaches `BriefedMove.MoveAsync` (`ChatRunner.cs:243-246`), which opens on the cooling own account as a pick (`BriefedMove.cs:133`), and that pick is refused. The walk arrives only in a-iii.
   - **Smallest change.** Move the closed quest's case (§7 row 1b and its proof) into row a-iii, after the briefed move takes the walk.
6. **Blocking.** The seed and removal semantics are unsafe as worded (`D212.md:98`; design:331, :335, :343-345).
   - **What is wrong.** "A seed read where `roles` names nothing", read literally, drops all eight seeded roles at the person's first added role, so every build and conversation holds. And "removing the role that takes a work ... its starts hold" makes Build or Chat off, which D205's judge refuses (`ActorChoices.cs:43`, `:55`; design:314 lists only Intake, Ask Daoris and Design as works that may be off).
   - **Smallest change.**
     - Read the seed per id: a seeded role is the seed's until `roles` holds a record of that id, and a removal is written as such a record.
     - The judge refuses removing a role of a work that may not be off, and refuses scoping a seeded role away from the machine.
     - Row w's proof adds a catalogue with one added role.
7. **Not blocking.** The closed quest extension rests on an unsupported claim (design:439, :788, :962; §1 item 2).
   - **What is wrong.** D137's note and the FIX-LOG say nothing of a closed quest; the FIX-LOG records LIMITSWITCH1's fix as the fix of the reported case. The note amends point 7, not point 4.
   - **Smallest change.** Drop "the case the owner reported was a closed quest's". Say the point-4 amendment is the steward's reading of "an ended record". List it in §8 as a reading the owner may overrule, beside the two questions.
8. **Not blocking.** Narrowing a role's doors has no reader (design:316, :327, :340-341, :348-349).
   - **What is wrong.** The admission tables "read the work, never the role", so a role's narrowed doors bind only through the handed rules, where the adapter takes settings. And "a door its work does not have" contradicts "doors only narrowing".
   - **Smallest change.** Drop door-narrowing from the role until a row gives the call layer the role, and strike "or a door its work does not have".
9. **Not blocking.** `design` fails its own census, and its quest mark is unrecorded (design:207, :213-216, :359, :888).
   - **What is wrong.** `WorkOf` reads design sessions as `build`, while census C requires the role named by `actor` to take the marks' work, so a Designer record fails it. The quest's design mark is a new field set by a publisher, on a record that travels, and §5.2 does not list it. MODELROLE1i's `weight: exact`, its stated model, is not built.
   - **Smallest change.** Either `WorkOf` answers `design` from the quest's mark, or census C admits `design` within `build`. Add the mark to §5.2 with its wire treatment and how an older build reads it.
10. **Not blocking.** D205's rejection of "per-workspace choices" (`D205.md`, Rejected) is reversed by a role's `scope` but missing from the amend list.
    - **Smallest change.** Add a dated note on it beside point 8's.
11. **Not blocking.** Row y-i refuses starts and adds an Ask Daoris tool, but is unmarked (design:910).
    - **What is wrong.** It depends on the unbuilt MODELROLE1g card and MODELROLE1h `actor_propose` (§1 item 5). D205 point 9's "no `settings_*` grant covers actors" is not carried over to roles.
    - **Smallest change.** Mark y-i ⚖, or say why removing a role is not refusing a start. Name MODELROLE1g and 1h as its predecessors, or say `role_propose` retires 1h's door. Extend point 9's sentence to roles.
12. **Not blocking.** The move record's fence has no lock (design:486-489, X-3.5).
    - **What is wrong.** "An atomic replace guarded by the revision the writer read" is a read-compare-write that two processes can interleave.
    - **Smallest change.** Name the lock: hold the file's lock from read to rename, as `Notices.KeepLock` does (`Notices.cs:15-16`).
13. **Not blocking.** A conversation's ended move has no step to the role's later choices.
    - **What is wrong.** Step 4 moves to "the role's later choices where ... every account of its agent cools", but the ended move walks one agent and has no `ElsewhereAsync` (`LimitMove.cs:235-241`; compare the live move, `:92`, `:444-469`).
    - **Smallest change.** Say that a-i's conversation path waits as today there, or add `ElsewhereAsync` to the ended path with a case.
14. **Not blocking.** Two text fixes:
    - Row c's lanes omit modules (`Modules/Loop/SessionsStateSlice.cs`).
    - §3.3's "delivered under the box's own key" (design:507) names half the mechanism. The first delivery is a live turn (`ChatHandOver.cs:238-250`); the keyed re-delivery and the `User` check are `LimitMove.cs:366-378`.

14 findings, 6 blocking.

**Verdict: accept after named changes the steward can check without another read.** The participant, the course with one account rule and a fenced move record per executor, and the journal with three orders now stand on the code. Round 1's three blocking findings are taken, or taken by the route round 1 allowed, and Codex's sixteen are taken or deferred with reasons that hold. The six blocking findings are bounded edits, each checkable against the lines cited: in §2.7 and row t, an act is admitted only within the appointment's `acts`; the two kinds of role are kept exclusive, with their flags and scopes; an added role's id stays out of `actor`; the seed is read per id, and no work that may not be off can be emptied; the own account's reset stays in step 5; and the closed quest's case moves to a-iii. Rows t and y-ii already wait for both makers' reads, so the authority half is read again before anything is built. This acceptance does not cover the closed quest extension: until the steward drops its evidence claim or the owner answers it, it stands on the steward's reading alone.

## Codex, round 2

Codex's read of round 2 at `29a066182`: 14 findings, 10 blocking; another round.

Reviewed HEAD `29a066182`, read-only; no tests run or files changed. References use the design’s path aliases. **F** means `docs/2026-10-25-foundation-design.md`; **D212** means `docs/decisions/D212.md`.

**0. Round-1 findings**

“Answered” means taken with a reason that holds. None of my original findings was wrong.

| Finding | Status | Assessment and evidence |
|---|---|---|
| 1.1 Containment | Answered — **non-blocking** | F:238 preserves ordinary starts’ best effort and pieces’ fail-closed rule. |
| 1.2 Held-call lifetime | Answered — **non-blocking** | F:685 correctly ends the hold with its owning process. |
| 2.1 Wire `kind` collision | Answered — **non-blocking** | F:191 adds `work` beside `kind`, with fallback at F:193. |
| 2.2 Recordless callers | Answered — **non-blocking** | F:236 positively places brainstorm generators/synthesizers and preserves their result tools. Refusal-code precision remains below. |
| 3.1 Missing course inputs | Answered — **non-blocking** | F:371 includes answers-alone history; F:397 includes waiting-word state and execution preconditions. |
| 3.2 Start-from semantics | Answered — **non-blocking** | F:392 gives it an independent `Start` and press intent key, without `from`. |
| 3.3 No eligible reset | Answered — **non-blocking** | F:460 supplies a non-timed hold naming the repair. |
| 3.4 Driven-to-chat head | Answered — **non-blocking** | F:524 specifies one service traversal across kinds; F:899 requires the late-word regression. |
| 3.5 Exclusive move claim | Open — **blocking** | F:488 specifies guarded file updates, but not exclusion or fencing of external effects. |
| 3.6 Producer versus transfer identity | Answered — **non-blocking** | F:533 separates them and commits transfer/removal/receipt together; F:556 supplies a receipt horizon. Identity scope still needs precision. |
| 3.7 Attempts and limit refusals | Open — **blocking** | Word-by-word acknowledgement is fixed; F:555’s transient-then-limit rule remains unsafe as written. |
| 4.1 Transition guards and wakes | Answered — **non-blocking** | F:588 guards real changes; F:590 gives every append its own wake. |
| 4.2 Retention and loss | Open — **blocking** | F:592 fixes count, positions and floor; F:911’s measured 5 MB limit does not bound arbitrary payloads. |
| 4.3 Announcement guarantee | Open — **blocking** | F:687 fixes claim-before-show ordering, but the bounded said-set’s expiry/replay contract remains unspecified. |
| 4.4 Journal versus relay durability | Answered — **non-blocking** | F:695 preserves relay loss and no replay; F:1001 explicitly excludes shell machine events from that stream. |
| 5.1 Move-proof prerequisites | Answered — **non-blocking** | F:900 puts attempts before moves; F:906 defers moving keeps until the course holds the old part. The remaining proof wording is addressed below. |
| 5.2 Executable rollback adapters | Open — **blocking** | F:504 postpones the essential phase mapping; F:508’s claimed deduplication does not hold against today’s code. |
| 6.1 Persistence rejection | Answered — **non-blocking** | F:136 correctly distinguishes persistence ownership from execution ownership. Files remain a valid choice once their concurrency protocol is supplied. |

**1. Wrong claims about existing code**

1. **Blocking — the rollback deduplication argument is false.** F:507 says the legacy box key makes older and newer delivery deduplicate, but `Core/Sessions/SessionLedger.cs:956` searches only pending `said`; after removal, that key protects nothing, and an older service ignores the proposed receipts. Likewise, the successor guard cited at F:508 prevents another hand-over open, but `Driver/Conversations/HandOver/HandOver.cs:271` can leave it unbriefed and `:294` still clears its box. Supply the actual downgrade/re-upgrade phase table, including uncertain delivery and phases where no executable old box may exist, before claiming one recovery authority.

**2. Who acts and the company**

1. **Blocking — configured door restrictions have no enforcement path.** F:327 permits narrower role doors, while F:347 explicitly makes admission read work “never the role”; F:175 gives the connector only ledger/brainstorm classification. Specify the runtime intersection of capability permissions and the selected role’s restrictions, including how the connector obtains it and handles missing evidence. F:214 also requires the catalogue “as it stood at the start,” but neither the record chain at F:722 nor the catalogue contract retains that version.

2. **Blocking — appointment grants and scope changes need an explicit lifecycle.** F:328 requires an appointment inside its role’s scope, but does not specify checking each resolved target against both scopes or what changing/removing the role does to live and pending acts. Today `Modules/Platform/StandingConfirmation.cs:188` snapshots appointment acts, `:1276` checks them, and `:1304` rechecks execution authority. Preserve an immutable owner-approved grant ceiling, intersect it with current restrictions at execution, and require a fresh window press for every effective increase—including scope expansion and removal of a restriction—not merely the “act added” case at F:919.

3. **Blocking — the seed needs absence and deletion semantics.** F:331 falls back to the seed where `roles` “names nothing,” F:335 writes only the first changed role, and F:344 promises removed work stays unstaffed. Without an overlay/tombstone or complete-catalogue rule, the first edit can discard untouched defaults, or deleting the last explicit role can resurrect the seed. Define absent, empty, malformed and deleted states, and prove first edit, partial edit and last deletion preserve those distinctions.

4. **Non-blocking — finish the single-judge and refusal-code contracts.** F:319 names `Company.Judge`, but its census should explicitly include legacy `SET_ACTOR` (`Modules/Bridge/Routes/DriverRoutes.cs:319`) and terminal/Help writers, alongside the new role doors. Reusing D205’s validation also needs capability-based checks: `Driver/Platform/ActorChoices.cs:51` rejects unfamiliar ids and `:65` applies brainstorm uniqueness by the literal actor id. Name the closed unplaced/unread/unknown-tool/forbidden-work refusal codes promised at F:231 and require identical cases at every relevant door.

The owner-only act exclusion and fixed launcher containment are stated correctly at F:315–317 and F:342. Staffing alone grants no appointment authority under F:283; the gaps above concern making those rules effective for configurable roles.

**3. How work moves**

1. **Blocking — a fenced file update does not fence a spawn or prompt.** F:488 guards phase replacement, but a stalled executor can act after another process takes a newer claim and only discover its stale claim when saving afterward. `Driver/Platform/AtomicFile.cs:156` supplies unconditional replacement; `RecordLock.cs:42` supplies an available cross-process exclusion primitive, but the design chooses no complete claim/takeover protocol. Specify atomic claim acquisition, live-owner takeover rules and the execution cutoff for every external effect; test a paused old executor resuming after takeover, not only two competing file writes.

2. **Blocking — eventual limit failure must not reclassify earlier transient attempts.** F:555 says a transient try followed by a limit refusal “is the limit’s,” and F:900 repeats that proof without qualifying which words were definitely refused. Today `Driver/Conversations/Domain/LimitHeld.cs:35` deliberately clears words already sent before a transient event because the agent may have read them. Keep dispositions per actual word attempt: only words conclusively refused before acceptance may carry; earlier uncertain attempts remain uncertain.

3. **Non-blocking — define receipt identity beyond a bare producer key.** F:534 and F:556 do not say whether receipt uniqueness is per record, original line, producer or home, nor how legacy words without keys transfer idempotently. Today keys are record-scoped and null means each word is independent (`Core/Sessions/Sessions.cs:374`). Specify the logical identity, a stable transfer identity for keyless words, and tests for equal keys on unrelated lines and retries through multiple successors.

4. **Non-blocking — narrow the account rule’s same-account prohibition.** F:463 forbids a new part on its own account where the conversation could resume, then F:464 gives the explicit briefed move that account first when ready. State which triggers the prohibition covers and which deliberate fresh-part presses override it. Otherwise two conforming implementations can choose different accounts from the same facts.

**4. What is said**

1. **Blocking — raw transitions need immutable projection inputs.** F:588 retains column old/new values and word ids, while F:619–623 require historical reason, class, park kind, author and level. A later lookup cannot recover these reliably: `Core/Sessions/Sessions.cs:886` overwrites notes and `:1605` removes words. Define a bounded, allow-listed snapshot of the necessary OLD/NEW facts at capture time, then classify it in .NET; prove end/reopen/end and park/answer/repark occurring before any reader runs.

2. **Blocking — row count is not the promised payload/size bound.** F:592 bounds 50,000 rows, but neither raw values nor per-transition word-id collections have a maximum; F:911 measures 5 MB without an enforcement rule. Specify payload limits, overflow/loss reporting and whether the size limit includes database/index/WAL overhead. Also qualify “never raising” at F:588: trigger storage failure cannot simultaneously be ignored and guarantee atomic capture.

3. **Blocking — the said-set’s expiry can invalidate “never repeated.”** F:681 gives it an unspecified horizon, while F:694 bounds catch-up by visible notices’ week/count; neither says when a deduplication key becomes safe to forget. Define retention together with cursor advancement and reconciliation eligibility, and key recurring facts by their occurrence—a later answer/end of the same resumed record must remain distinct (`Driver/Platform/Notices.cs:146` already renews answers). Test more than 200 eligible announcements, horizon expiry, a new teller and a floor reconciliation without replaying previously claimed news.

**5. The migration**

1. **Blocking — row d still depends on an undeclared receipt prerequisite.** F:899 adopts §3.4 rule 3’s transactional transfer receipt, but producer receipts are introduced afterward in row p (`F:900`, order at F:842). Either introduce the required receipt store/contract in d or place p first. Also replace F:902’s unconditional “delivers … exactly once” proof with explicit delivered, definitely refused and uncertain outcomes consistent with F:495 and F:554.

2. **Non-blocking — split row f into actual standalone landings.** F:902 introduces claims, idempotent opens, recovery, three initial move families and two legacy-box adapters in one row, despite F:839’s one-path rule. Give each adoption its own subrow and compatibility proof; make the unknown-actor older-build case mandatory before x, since today `Driver/Sessions/Domain/ActorRun.cs:176` throws for an added role. The whole-design two-reader gate at F:809 and SWITCHCTX1’s outstanding-read gate at F:710 should remain.

**Verdict: another round.** The role authorization lifecycle, move fencing/rollback protocol and journal capture schema still require foundational choices, rather than bounded editorial corrections.
