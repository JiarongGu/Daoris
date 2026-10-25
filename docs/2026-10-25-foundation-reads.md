# FOUNDATION1: the reads of rounds 1 to 4 (D191)

The independent Opus 5.5 reader's foundation reads and Codex's reads of `docs/2026-10-25-foundation-design.md` and D212, round 1 at `c8eccf30e`, round 2 at `29a066182`, round 3 at `68a396c65`, and round 4 at `6bac9a0be` by the Opus reader and at `86f32f40c`, with the Opus reader's named changes taken, by Codex, each run by the steward from outside the install, read-only, neither seeing the other's, verbatim but for their headings set one level down. Round 2 answers every finding of round 1 by number in the design's §12, round 3 every finding of round 2 in its §15, and round 4 every finding of round 3 in its §16; the Opus reader's round-4 findings are taken in the design's §17 and Codex's in its §18.

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

## Opus (foundation), round 3

The independent Opus 5.5 reader's foundation read of round 3 at `68a396c65`: 16 findings, 2 blocking; accept after named changes.

### 0. Both reads of round 2

I read at HEAD `68a396c65`. Its `src/` is `2a08bde4a`'s plus LIMITSWITCH2 (`git diff --stat 2a08bde4a HEAD -- src`: 11 files), and I read LIMITSWITCH2's code as well as its note. Lines below are HEAD's. The design's lines are `2a08bde4a`'s, and they drift by a few lines in `Driver.cs`, `LimitMove.cs`, `ChatGoOn.cs` and `ChatRunner.cs`. Paths use the design's aliases. I read the code; I ran nothing.

#### The Opus reader's foundation read (14 findings, 6 blocking)

| # | Round 2 | Round 3 | Why |
|---|---|---|---|
| 1 | blocking | Taken | An act is admitted only within three things: the code's act table, the appointment's `Acts` (`StandingConfirmation.cs:187`, checked at `:1276`), and the role as it stands. Increases wait for the next *Appoint*, and authority is edited only through `Window`-class routes (§2.7; rows t and y-ii). One gap: today the desk's decline is admitted outside every grant (`:1275`), and "all three" drops that (finding 7). |
| 2 | blocking | Taken | Two exclusive shapes, each act with its scope kind and offered flag. Those flags and the act tables are in the shell's library, which the driver cannot name (finding 7). |
| 3 | blocking | Taken, its field placed better | `actor` keeps the seeded id (`ActorRun.cs:176-177`). The added role goes on the binding in the desktop store, which no older build reads and which needs no chain through the host. The reason holds. Binding writes must keep the role's columns (finding 16). |
| 4 | blocking | Taken | Step 5 waits on the own account's reset wherever step 1 may resume there, and the proof case is split. |
| 5 | blocking | Taken | Moved to a-iii. LIMITSWITCH2 built it through the ended move, which walks (`ChatRunner.GoOnLostAsync` → `LimitMove.MoveEndedAsync(…, switching: true)`), never through `BriefedMove`. |
| 6 | blocking | Taken | Each id is absent, set, removed or unreadable. A work that may not be off keeps a machine-wide role. |
| 7 | — | Answered, and the reason holds as a reading | The claim is now the steward's evidence and reading, recorded as one the owner may overrule (§8; `D137.md:856-863`; `FIX-LOG.md:89-90`). The evidence is the steward's account of the install, which no tracked record lets me check. It stands on that account. |
| 8 | — | Taken | A role carries no doors. |
| 9 | — | Taken | Census C admits `design` within `build`, and `weight` is listed in §5.2. |
| 10 | — | Taken | §7 row 18 and D212's amend list. |
| 11 | — | Taken | y-i is ⚖, and its predecessors are named. |
| 12 | — | Taken, superseded | Store transactions replace the file claim. |
| 13 | — | Taken | a-iii adds `ElsewhereAsync`. |
| 14 | — | Taken | The lanes and the box mechanism are corrected. |

#### Codex's read (14 findings, 10 blocking)

| # | Round 2 | Round 3 | Why |
|---|---|---|---|
| 1.1 | blocking | Taken | No box is written beside the move record, and §13.5 gives the phase table. When the boxes are imported contradicts §13.5's own rule (finding 4). |
| 2.1 | blocking | Taken, by removal | A role carries no doors, so there is nothing to enforce. The revision is kept on the binding. |
| 2.2 | blocking | Taken | The appointment's grant is a ceiling for its life, intersected at each act. Increases wait for the next *Appoint*, narrowings apply at once, and removing a role revokes its appointments. |
| 2.3 | blocking | Taken | Per-id states, with first edit, partial edit and last deletion proven in row w. |
| 2.4 | — | Taken | The judge's census and its closed codes. |
| 3.1 | blocking | **Open** | The fence has the right shape: a store row, and a mark under a claim check before each effect. But the takeover rests on "the holder of the executor's role", which neither the code nor §13.3's schema establishes. The design misreads `--share` as a role passing, and "one shell per home" is a per-install guard (finding 1). |
| 3.2 | blocking | Taken | Dispositions are per attempt, and `LimitHeld.cs:35-41`'s reason is kept. |
| 3.3 | — | Taken | Receipts are keyed by line and producer key, and a keyless word by its id. The desks' keyed hands fall outside the rule's census (finding 5). |
| 3.4 | — | Taken | Step 6's one exception. |
| 4.1 | blocking | Taken | Each row carries an allow-listed snapshot, and row k proves the sequences. |
| 4.2 | blocking | Taken | At most 1 KiB a row and 50,000 rows, an overflow row, and *never raising* qualified. |
| 4.3 | blocking | Taken | News is keyed by occurrence, with one horizon and m-iv's cases. |
| 5.1 | blocking | Taken | Row d needs no receipt, p comes first, and row f names its three outcomes. Row d now needs f-i's `opens` (finding 12). |
| 5.2 | — | Taken | Row f is split, and row x's older-build case comes first. |

### 1. Claims about the code

**What holds**, from what I checked:
- §13.1's inventory: no `Microsoft.Data.Sqlite` under `src/Daoris.Desktop`; the service's pin (`src/Daoris.Service/Directory.Packages.props:30`); `knowledge.db` (`Core/Platform/DaorisHome.cs:338`); `KeepLock` and `JsonKeeping.Save` (`JsonKeeping.cs:72`); `DriverConfig.Save` (`:1009-1028`); `GitIdentityUse.Read` over `driver.json` (`:80-86`); the config's watch (`HomeWatch.cs:200`).
- The lease (`HomeLease.cs:55-75`, `:165`, `:337-343`).
- The GitDoor's fourteen compiled files.
- The driver's 784 files, all in one namespace.
- `StructureTests`' fifteen modules (`StructureTests.cs:191`), and §14.1's Loop rules at `2a08bde4a`.
- The catalogue's citations: `ActorChoices.cs:37`, `:43`, `:51-72`, `:171-177` and `:320`; `ActorRun.cs:176-177`; `DriverChoicesCommand.cs:2059-2061`; `HelpRoomOwed.cs:94-101`; `StandingConfirmation.cs:187`, `:330`, `:336`, `:831-835` and `:1276`.
- LIMITSWITCH2 as §5.1 describes it: `switches` read for every kind, with `walkCounts`; `ChatGoOn.SwitchableAsync`; the ended move's `switching` and its one walk; `GoOnLostAsync` for `ContinueWhy.Account`; `Driver.cs:1993` and `:2384`.

**What is wrong:**

1. **§13.4: "a role passes while its old holder lives (a lease given up or shared, `Host/Program.cs:868-885`)".** Sharing passes nothing. `--share` runs a second loop beside the instance, holding no lease, and "the two loops race for the same quests, and the take decides" (`Host/Program.cs:869-875`, `:890-895`).
2. **§13.4: "one shell per home".** The kit's guard is "One instance per install" (`App/Program.cs:368`). `HostStartClaim` is keyed by the service's address and refuses no second launch: "A launch that cannot claim it … begins nothing, and its loop brings the host up as before" (`Modules/Loop/HostSupervisor.cs:478-486`). Nothing excludes a second shell on the home before its conversations run, and they run before and without the lease (`DriverLoop.cs:777-800`).
3. **§13.2 and §13.4: the GitDoor opens the store "through the same class compiled into it as `GitIdentityUse` is".** The GitDoor is "Framework-dependent and referencing nothing … each using the base library alone" (`Daoris.Desktop.GitDoor.csproj:3-7`), and "The driver itself has ZERO package dependencies" (`src/Daoris.Desktop/Directory.Packages.props:6-9`). `DesktopStore` needs `Microsoft.Data.Sqlite` and its native bundle.
4. **§13.6 row z-iii: "left in place (a record's conversation id never changes)".** `HarnessConversations.Keep` replaces what was kept whenever the wire names the conversation (`Conversations/GoOn/HarnessConversations.cs:28-37`), and every start and resume is handed it as `Keeping` (`:51`).
5. **§3.4 rule 5: "Every keyed producer reaches the say door … (the HTTP host's say route is the door's one caller)".** Two keyed producers never pass the say door:
   - a helper's answer, keyed `helper:<id>` (`Core/Sessions/HelperDesk.cs:186-189`);
   - a partner's piece, keyed `partner:<piece>` (`PartnerDesk.cs:136-147`).

   Each is kept by its own desk's `KeepSaidAsync` through its own route (`Http/Program.cs:1739-1748`), and each is answered as a repeat only while its word waits (`HelperDesk.cs:180`; `PartnerDesk.cs:137`).
6. **§14.2 row 11 and §2.7.** Row 11 says Roles takes "`Sessions/Questions/`'s roles, appointments …", and §2.7 puts the act vocabulary "in the driver library". But the appointments, the role list, the act tables, the offered flags and the act judge all live in the shell's library: `Modules/Platform/StandingConfirmation.cs:121-133`, `:267-297`, `:330`, `:336` and `:1245-1290`; `Modules/Platform/RoleActs.cs:44-65`; `Modules/Bridge/StewardActRows.cs:145-161`. `Sessions/Questions/` holds the proof, the act form, the decisions and two verbs. The driver references no project (`Daoris.Desktop.Driver.csproj`), so it cannot name any of them.
7. **§2.5, §2.7 and row t: an act admitted "only where it is in all three".** Today the steward's desk decline is admitted on every steward appointment, outside its `acts` (`StandingConfirmation.cs:1275-1279`; `RoleActs.cs:56-60`).
8. **§14.1 and §14.3: `DaorisHome` "names six folders'" types.** It compiles into the GitDoor with the base library and thirteen other files (`GitDoor.csproj:20-35`), so it names none. The count read `HomeEntry` members (`HomeEntry.StandingConfirmation`, `.Notices`, …) as types.
9. **Row a-i's installed proof, `session.moved … why=account`.** The conversation's switch continues the old part by `retry`: `LimitMove.cs:220` and `:317-318`, logged at `:296` through `Continuations.Moved` (`Conversations/Domain/Continuation.cs:588-594`). D137's LIMITSWITCH2 note says the same (`D137.md:836-837`). `account` is the box's reason and the note's (`ContinueWhy.Account`, `Continuation.cs:60`).
10. **Row u.** Retiring 角色 at `zh/settings.daoris.json:108` is meant to retire it for an appointed role, but `:139` says it too (你任命的角色).
11. **D137's LIMITSWITCH2 note, written beside this round.** It says row 1a "keeps `accountPicked` on the record" (`D137.md:848`). Round 3's a-ii keeps it on the binding in the store instead.

### 2. Roles

- **Widening authority through a role.** None:
  - A working role holds no authority; its admission is its work's, in code.
  - An appointed role's authority is edited only on a `Window`-class route and admitted only within the appointment's grant, so an edit narrows a live holder and never widens one.
  - An older build reads no catalogue and finds every appointment dormant (`StandingConfirmation.cs:285-290`).
  - The export carries "the appointments' grants". An imported appointment is dormant by construction, since the holder key's hash is the run's memory alone. The design should still say that an import never sets or raises a grant, which only *Appoint* does (D208 point 1; finding 16).

  Authority does widen across a downgrade outside the catalogue (finding 2).
- **Owner-only acts.** None: the vocabulary names none, the judge refuses `owner-act`, and the door refuses it again.
- **Escaping containment.** No. Containment is keyed by work in code (`AgentStarts.cs:13`; `AgentJobs.cs:119`), and no role field names it.
- **Granted without the person's own press in the window.** No:
  - A working role changes only by the person's press at a door, or by Ask Daoris's proposal once pressed.
  - Appointed authority changes only in the window, and takes effect at *Appoint*.

  This holds, given finding 16's line on the import.
- **Two exclusive shapes.** On write, yes (`mixed-shape`). On read the design says nothing. A row written by an older store-aware build, or by hand, is read by `RoleCatalog.Read`, which must judge its shape too and hold a mixed record as unreadable (finding 16). The judge's `not-offered` and `scope-kind` read flags and tables the driver cannot reach (§1 item 6; finding 7).
- **The seed read per id.** Right, and it reproduces today. It does not say which starts an unreadable added record holds when its `takes` or `scope` is the part that does not read (finding 16).
- **Continuations across builds.** Safe by construction. Every build's continuation reads a seeded `actor` that `ActorRun` knows (`ActorRun.cs:176-177`; `Driver.cs:3673`), and a newer build reads the binding's role first. That stays true only while writers update a binding's columns by name. An older store-aware build that replaced the row would drop the role (finding 16).

### 3. The store (§13)

- **Owner.** The driver library, through `DesktopStore`, with each family owned by one subdomain. But the appointments' writer is the shell's library, not a driver subdomain (finding 7).
- **Processes.** The shell; `daoris-driver`, as the headless loop and as each verb; and the GitDoor, read-only. The driver and the GitDoor take their first package and native library, against constraints recorded for both (finding 3).
- **Transactions and fences.** These are right: `BEGIN IMMEDIATE`, one connection per process behind a gate, no transaction across an effect, and mark → effect → outcome. Step 4's order also loses and doubles nothing: the disposition and the receipt commit first, then the take, and a crash between is finished from the receipt.

  What fails is who may take a move over. The claim's holder is "the holder of the executor's role", and the code has no such holder for either kind (§1 items 1 and 2); §13.3 records none either. So a `--share` loop's driven moves either cannot be claimed, or the two live loops take each other's over. And a second install's shell on the same home takes over the first's live conversation moves (finding 1).
- **Migrations and the rollback window.** Forward-only steps, one transaction each, are sound. Two timings conflict with D139's window:
  - §3.3 imports an older build's boxes at the come-up, before the take-up, while §13.5 imports a family an older build acts on only after the build confirms.
  - A migration that raises the oldest-writer stamp at a first start leaves a rolled-back build refusing every write (finding 4).
- **What stays JSON.** The line is coherent, and the list matches the code: the formats other tools own, the tracked records, the manifests, the files read before the store opens, the streams, the credentials, the held words until row o, and `pipe-waiting` (one writer).
- **Lost, doubled or replayed.**
  - **Across a crash or a restart,** once finding 1 is fixed:
    - a move's effect is begun at most once, and an uncertain one is held and said;
    - a word is delivered once, or held at a conclusive limit and carried once, or left uncertain and said;
    - news is told at most once.

    One gap: a helper's or a partner's keyed hand retried after its take is kept again, because rule 5's receipt read covers only the say door (finding 5).
  - **Across a downgrade,** the phase table holds for moves. But:
    - a revoke made in the store is undone by an older build reading the backup left in place (finding 2);
    - an older build resumes a binding's stale conversation id (finding 6);
    - a hand edit of `driver.json` does nothing until a newer build returns (finding 11).

### 4. The module map (§14)

- **Does it follow the code's seams?** Mostly; most of the map follows real folders. Four places do not:
  - Roles' appointments and act tables live in the shell's library (finding 7).
  - `ResumeChoice` and `ResumeWords` (`Accounts/Rotation/ResumeAccount.cs:66`) move to Course, yet the roster in Agents names `ResumeChoice` on ten lines (`HarnessRoster.cs`, `ResumeAsync`). So Agents names row a-i's `LineAccount` upward from its first commit.
  - `Trees/SessionGroups`, placed in Service (#4), reads `StartVerdict` (`Quests/Domain/Planner.cs`), `GoOnMarks`, `ResumeWords`, `AccountNames` and `SessionTrees`, all from higher subdomains.
  - `Quests/Domain/Attention`, placed in Events (#7), names `CurveMove` (`Attention.cs:338`), `StartVerdict` (`:272`) and `HelpRoom` (`:80`) (finding 8).
- **Acyclic and enforceable.** Acyclic by its order. Once namespaced, it is enforceable as a ratchet by `using` lines. The starting edges over-count (the `DaorisHome` edge is false, §1 item 8), so mod-0's exact census matters more than §14.1's figures.
- **No behaviour changed.** Moving folders changes none, provided the GitDoor's `Compile Include` paths and the embedded resources' `Include` paths move with the files; their `LogicalName`s are pinned. Moving namespaces is not quite behaviour-free: the machine log writes an exception's `GetType().FullName` (`MachineLog.cs:322`). D166 also rejected renaming namespaces as files move (finding 10).
- **Names.** *Roles* is the owner's name, and right. But `Course` and `Participants`, types the design introduces (`Course.Decide`, `Participants.Of`), are also their namespaces' names under `Daoris.Driver.<Name>`. From any `Daoris.Driver.*` file, the simple name binds to the namespace first, so every caller must qualify it (finding 9). *Work orders* needs a namespace spelling.

### 5. Names on the page

Round 2's single word is right, on the code and on the glossary.

- **The code.** The design makes the actor and the appointed role one record, `Role`: one catalogue, one judge, and one place to set it once row y-i lands. It also says the AI screen's actor rows *are* the catalogue's working roles (§2.7, *What reads it*). Round 3's 职务 for the role beside 角色 for the actor therefore shows one record under two Chinese words, on two screens.
- **Round 3's reason.** "So the page says the configured position and the staffing screen apart" separates two screen titles, not two concepts.
- **The catalogues.** Every shipped string already says 角色 for both: `zh/settings.ai.json:28`, `zh/work.json:2`, `zh/person.json:123`, `zh/agents.json:121`, `zh/note.json:10`, and `zh/settings.daoris.json:108` and `:139`.
- **The glossary.** It already gives the term 角色 the meaning the working role now carries (`glossary.json:475`). Its `avoid` list refuses 岗位, which is close to 职务 in meaning.
- **The appointed shape.** Where it must be said, 任命的角色 already says it (`settings.daoris.json:139`).

Keep 角色 for the role in both shapes, keep *actor* as a code and prose word for a working role's staffing, and withdraw 职务 (finding 15).

### 6. The rows

- **Does each land alone, in order, behind today's tests?**
  - These can: a-i, b, c, mod-0, a-ii, a-iii, p, e, g, h, i, o, j, w, x, y-i, k, l-i to l-v, m-i to m-v, n, q, r, s, t, y-ii and v.
  - d cannot land before f-i. It opens under the move's `opens` key, which §5.2 and f-i introduce (finding 12).
  - z-i needs the package decision (finding 3).
  - z-ii does not land behind today's tests unchanged. The family rehearsal and the proof kit seed and re-read a home's `driver.json` (`tools/family-rehearsal.mjs:981`, `:5603-5607`; `tools/proof-kit.mjs:285-290`), and importing once makes their later writes inert (finding 11).
  - f-i to f-vi wait on finding 1.
- **Does each proof prove its claim?** Most do. The exceptions:
  - a-i's installed line is `why=retry`, not `why=account` (finding 13).
  - f-i's "a paused executor resuming after a takeover" proves nothing until the takeover rule says who may take over. It also needs two shells on one home and a `--share` loop (finding 1).
  - z-ii, z-iv and z-xi lack the case of a revoke made in the store followed by an older build's start (finding 2).
  - u's list misses `:139` (finding 15).
- **Are the rows that grant authority marked to wait?** q, r, t, v, y-i and y-ii are marked ⚖. Four unmarked rows touch authority records or deletion:
  - z-ii adds an import door that changes `access`, `drivable`, `trees`, `standing` and a role's authority;
  - z-iv rehomes the appointments and the standing confirmation's grant;
  - z-xi rehomes the permissions, the configured access and the plugin approvals, among them the GitDoor's grants;
  - z-ix moves the records a removal follows (finding 14).

### 7. Findings

1. **Blocking. The move's takeover rests on an executor-role holder that nothing establishes** (§13.4; D212 point 8).
   - **What is wrong.** A move is taken over "only by its role's current holder, of a move whose run is not that holder's run", and a live holder is never taken over. But §13.3's schema records no holder, and the code has none to read:
     - `--share` runs a second driven loop beside the instance, holding nothing (`Host/Program.cs:869-875`, `:890-895`). Its moves are either unclaimable, or the instance takes them over while the loop still lives.
     - "One shell per home" rests on a per-install guard (`App/Program.cs:368`) and on an address mutex that refuses no second launch (`HostSupervisor.cs:478-486`). So a second install's shell on the home takes over the first's live conversation moves.
   - **Smallest change.**
     - Take a move over only where its claim's run is dead by the lease's own test: pid and start time, with "cannot tell" read as alive (`HomeLease.cs:66-68`, `:165`, `:446`). Never take one over because a role passed.
     - Each live loop, a `--share` loop included, claims and fences the moves of the lines it runs; a conversation's move belongs to the shell that runs it.
     - Name the per-process run (id, pid, start time) and the claim's columns in §13.3.
     - Row f-i proves three cases: two shells on one home, a `--share` loop beside the instance, and a paused executor whose process still lives.
2. **Blocking. An older build undoes a revoke made in the store** (§13.5; rows z-ii, z-iv, z-xi).
   - **What is wrong.** A family an older build only reads is left in place and never written through. So an older build run on the home (§13.5's own case) reads each authority as it stood at the import:
     - the standing confirmation's `presses` grant is live from its file at an instance's start (`StandingConfirmation.cs:267-271`);
     - `driver.json`'s `access`, `drivable`, `trees` and `standing` are §13.5's own authority keys;
     - `permissions.json`, `configured-access.json` and `plugin-approvals.json` are z-xi's, and the GitDoor reads the first at every remote act.

     Every revoke or narrowing the person makes after the import comes back, without any press.
   - **Smallest change.** Choose one per authority-bearing family:
     - a narrowing written to the store is also applied to the left-in-place file by that family's own writer, narrowing only; or
     - the family is set aside, where its older reader reads absence as off, as the standing confirmation's does (`:270-271`).

     Add a revoke followed by an older build's start to the proofs of z-ii, z-iv and z-xi.
3. **Not blocking. The store gives the driver and the GitDoor their first package, against constraints recorded for both** (§13.2).
   - **What is wrong.** `src/Daoris.Desktop/Directory.Packages.props:6-9` keeps the driver to the base library, and `GitDoor.csproj:3-7` keeps the door "referencing nothing … so a door starts small on every remote act". `DesktopStore` needs `Microsoft.Data.Sqlite` and the native bundle in both. D212 names neither constraint.
   - **Smallest change.** State that the driver takes the package, pinned as the service's is (`src/Daoris.Service/Directory.Packages.props:30-31`), with its reason, as an amendment of that recorded rule. For the GitDoor, choose one: it takes the package and row z-i measures its start on a remote act, or its four families stay readable to it without SQLite.
4. **Not blocking. The rollback window's timings conflict** (§3.3, §13.3, §13.5).
   - **What is wrong.** Two timings disagree with D139's window:
     - **Box imports.** §3.3 imports "any box an older build may have written" at the come-up, before the take-up. §13.5 imports a family an older build acts on, a box among them, only after the build confirms. But the app confirms asynchronously as it composes (`App/Program.cs:323`; `InstallUpdater.cs:135`), a swap can finish `confirmed: false` (the update design §3 step 5), and a headless start never confirms.
     - **Migrations.** A migration that raises the oldest-writer stamp at a first start leaves a rolled-back build refusing every write.
   - **Smallest change.**
     - The come-up's imports run once this start's confirmation is settled, or once the swap journal shows none pending.
     - Until a box is imported, its old part is the course's `Never`.
     - A migration that raises the stamp runs only after confirmation.
     - Row z-i proves a rollback after a first start.
5. **Not blocking. Keyed hands bypass the receipts** (§3.4 rule 5; row p).
   - **What is wrong.** A helper's and a partner's keyed words are kept by their desks, not the say door (`HelperDesk.cs:180-189`; `PartnerDesk.cs:136-147`; `Http/Program.cs:1739-1748`). Each is answered as a repeat only while its word waits. A hand retried after its take is kept twice.
   - **Smallest change.** Rule 5 and row p's census name both hands beside the say door, and the desktop client reads the receipt before a hand as well.
6. **Not blocking. An older build resumes a stale conversation id from the bindings left in place** (row z-iii).
   - **What is wrong.** The kept id is replaced whenever the wire names one (`HarnessConversations.cs:28-37`). The backup holds the id as it stood at the import, and §13.5 gives the bindings no merge rule for the files an older build writes while it runs.
   - **Smallest change.** Write a changed conversation id through to its file, which has one writer and is small; or class the bindings as set aside, where an older build reading none falls back to a carry-on (`:15-16`). At the return, an older build's newer file wins for its own session.
7. **Not blocking. Half of Roles lives in the shell's library** (§2.7, §13.3, §14.2 row 11).
   - **What is wrong.** The appointments, the role list, the act tables, the offered flags and the act judge are all in Modules (§1 item 6), and the driver cannot reference Modules. So neither `RoleCatalog.Judge` (`not-offered`, `scope-kind`) nor the Roles-owned `appointments` table can be written as the design says. The design's three-way intersection also drops the desk's acts, which are admitted outside every grant (`StandingConfirmation.cs:1275-1279`).
   - **Smallest change.** Name either a row that moves `Roles`, `StewardActs`, `StewardActRows` and the appointment's persistence down into the driver (proven by `StandingConfirmationTests` and `PipeCensusTests`), or a port that Modules implements. Say that the desk's acts stay outside the grant.
8. **Not blocking. Four placements in the map go against the code's dependencies** (§14.1, §14.2).
   - **What is wrong.** Four of the map's placements create edges against its own order:
     - the account rule's `ResumeChoice` and `ResumeWords` go to Course while the roster in Agents returns `ResumeChoice` (`ResumeAccount.cs:66`; `HarnessRoster.cs`);
     - `SessionGroups` in Service reads Agents, Workplace, Course and Work orders;
     - `Attention` in Events names Course and Work orders (`Attention.cs:272`, `:338`);
     - the `DaorisHome` edge is false (§1 item 8).
   - **Smallest change.** Place the resume's types with the roster, or the roster's resume in Course. Place `SessionGroups` and the attention policy above what they read (Work orders or Loop), with Events keeping the vocabulary. Correct §14.1's `DaorisHome` edge.
9. **Not blocking. Two of the design's types share their namespaces' names** (§3.1, §2.1, §14.2).
   - **What is wrong.** `Course` (`Course.Decide`) and `Participants` (`Participants.Of`, `.WorkOf`) would live in the namespaces `Daoris.Driver.Course` and `Daoris.Driver.Participants`. From any `Daoris.Driver.*` file the simple name binds to the namespace, so every call must be qualified. The same holds for the Modules' `Roles` class (`StandingConfirmation.cs:121`) once it moves to `Daoris.Driver.Roles`.
   - **Smallest change.** Give those namespaces distinct names, or rename the types.
10. **Not blocking. The map amends D166 and D176 without naming them** (§14; D212's amend list).
    - **What is wrong.** §14 replaces D166's modules (point 1; Brainstorms is D176 point 1's fifteenth, held at `StructureTests.cs:191`). It also reverses D166's rejection of "renaming namespaces as files move" and its point 4's "namespaces per module last". Neither decision is in §7 or in D212. A namespace move also changes the machine log's `type` field (`MachineLog.cs:322`).
    - **Smallest change.** Add D166 (points 1 and 4, and its rejection) and D176 point 1 to the amend list, with the reason. Either keep D166's order, folders first and namespaces last, or say why not. List the log field as a known change.
11. **Not blocking. D212 understates the change to D50, and z-ii's test seeding is missing** (§13.5; row z-ii).
    - **What is wrong.** D50 keeps hand-editing working because "the file, not the surface, is authoritative" (`D50.md:7-9`). After z-ii, nothing reads a hand edit of the left-in-place `driver.json` until a newer build returns and offers it, and today's live read ends (`HomeWatch.cs:200`; `DriverWatch.cs:1118`). D212 lists D50 only for "export and import at every door". Separately, the rehearsals and the proof kit seed `driver.json` (§6).
    - **Smallest change.** State D50's change plainly: the store is the truth, and a hand edit takes effect only by an import. Say what a changed `driver.json` does at each start. Give z-ii a seeding rule for its census: a fixture home's seed file is imported at its first start, and `DAORIS_DRIVER_CONFIG`'s file whenever it changes.
12. **Not blocking. Row d uses the `opens` key before row f-i introduces it** (§3.4 rule 3; §5.2; rows d, f-i).
    - **What is wrong.** Row d's open "moving the old part's waiting words … idempotent under its key" uses `opens`, which is "new, row f-i". Row d lands before f-i, while no move record yet keeps the key across a crash.
    - **Smallest change.** Move `opens` and its record-field chain into row d, proving its idempotency in-process there and across a crash in f-i.
13. **Not blocking. Row a-i's installed proof names the wrong log line, and D137's note is stale** (§10; `D137.md:848`).
    - **What is wrong.** The line will read `why=retry` (§1 item 9). D137's note keeps `accountPicked` on the record (§1 item 11).
    - **Smallest change.** Make the proof read `session.moved … why=retry`, with the box's `account`. Add a dated line to D137 pointing at row a-ii's binding.
14. **Not blocking. Rows that rehome authority, or move the records a removal follows, are not marked ⚖** (§10; §13.6).
    - **What is wrong.** z-ii, z-iv, z-xi and z-ix are unmarked (§6).
    - **Smallest change.** Mark z-ii, z-iv and z-xi ⚖, and z-ix as a deleting row.
15. **Not blocking. One record appears under two Chinese words** (§6; row u).
    - **What is wrong.** §5 gives the case. Row u also misses `zh/settings.daoris.json:139`.
    - **Smallest change.** Use 角色 for the role in both shapes and 任命的角色 for the appointed one, and withdraw 职务. If 职务 stands instead, row u must retire 角色 from every string listed in §5 and rewrite the glossary's *actor* entry (`glossary.json:475`).
16. **Not blocking. The catalogue's reads, its continuations and its import each leave one case unsaid** (§2.7; §13.3; §13.5).
    - **What is wrong.** Four cases:
      - shape is judged on write alone;
      - an unreadable added record's held starts are unsaid;
      - nothing says binding writers update named columns rather than replacing a row;
      - the import's carrying of "the appointments' grants" does not say it never sets one.
    - **Smallest change.** Four sentences, one per case:
      - `RoleCatalog.Read` judges shape, and holds a mixed record as unreadable.
      - An unreadable added record holds every start in its scope, until it reads or is deleted.
      - Binding writers update named columns and never replace a row.
      - An imported appointment is dormant, and its grant is set only by *Appoint* (D208 point 1).

      Row w's proof gains the first two.

16 findings, 2 blocking.

**Verdict: accept after named changes the steward can check without another read.** The participant read, the course with one account rule, the journal with its capture schema, and the role catalogue's shapes, per-id seed and grant lifecycle now stand on the code. Round 2's blocking findings are taken, apart from Codex's 3.1. Two changes are needed, and each is bounded and checkable against the lines cited:
- **Finding 1:** a move is taken over only from a run that is dead by the lease's own liveness test, never because a role passed, with the run and claim columns named and f-i's three cases added.
- **Finding 2:** every authority-bearing family keeps an older build from reading a revoked grant, either by writing narrowings through to its backup or by setting it aside where absence is off.

The rows the store and the map add should carry findings 3 to 16's edits, and z-ii, z-iv and z-xi should be marked ⚖ so the authority half is read again before it is built. This acceptance does not cover the reading that a closed quest's session falls inside LIMITSWITCH1's decision; that remains the owner's to overrule.

## Codex, round 3

Codex's read of round 3 at `68a396c65`: 17 findings, 11 blocking; another round.

Reviewed HEAD `68a396c65a72e455b260bc384bc3f112ac7a4b34`, read-only. No files changed or tests run; nothing under checkout-relative `local/` was read.

**F** = `docs/2026-10-25-foundation-design.md`; **D212** = `docs/decisions/D212.md`. Code paths use F’s aliases.

**0. My round-2 findings**

| Finding | Assessment | Evidence |
|---|---|---|
| 1.1 Rollback deduplication | **Open — blocking** | Removing the parallel boxes helps, but the replacement phase table misses an older build opening another successor; see §3 below. F:1355–1358. |
| 2.1 Configured door restrictions | **Taken — non-blocking** | Roles carry no doors; admission remains capability-based, and bindings retain the selected role revision. F:349, F:365. |
| 2.2 Appointment lifecycle | **Open — blocking** | The immutable grant is restored, but intersecting it with current configuration permits a restriction removed after narrowing to become effective again. F:390–399. |
| 2.3 Seed semantics | **Taken — non-blocking** | Per-id absent/set/removed/unreadable states distinguish first edits, deletion and reset. F:367–377. |
| 2.4 Judges and refusal codes | **Taken — non-blocking** | Legacy writers enter the census; validation follows work rather than actor id; refusal codes are named. F:266–268, F:409–412. |
| 3.1 Effect fencing | **Open — blocking** | A transaction before an effect still leaves a pause between committed mark and effect. F:1299–1316. |
| 3.2 Transient then limit | **Taken — non-blocking** | Dispositions remain per attempt, preserving `LimitHeld`’s uncertainty rule. F:649–655; `Driver/Conversations/Domain/LimitHeld.cs:35`. |
| 3.3 Receipt identity | **Taken — non-blocking** | Line plus producer key, and word id for keyless words, answer the identity question. F:661–663; the new storage protocol has separate problems below. |
| 3.4 Same-account exception | **Taken — non-blocking** | Automatic moves and deliberate fresh-part presses are distinguished. F:547–552. |
| 4.1 Immutable projection inputs | **Taken — non-blocking** | Capture now specifies snapshots and delayed-reader proofs rather than later record lookups. F:701–723. |
| 4.2 Bounds and storage failure | **Taken — non-blocking** | Payload/count limits, overflow reporting and atomic failure are specified; WAL is explicitly outside the payload bound. F:715–723. |
| 4.3 Announcement retention | **Open — blocking** | The horizon is specified, but journal and reconciliation paths assign different keys to the same occurrence. F:827–836. |
| 5.1 Word-move prerequisites | **Open — blocking** | Attempts now precede d, but d depends on the idempotent open introduced later in f-i. F:1004, F:1074, F:1076. |
| 5.2 Standalone move landings | **Taken — non-blocking** | f is split by move family; the older-build actor case and both-reader gates remain explicit. F:1076–1078, F:1087, F:968. |

None of those findings was wrong; none needs deferral.

**1. Wrong claims about existing code**

1. **Blocking — the cited guards do not establish one shell per home.** F:1307–1309 relies on that property, but `App/Program.cs:368` describes one instance **per install**, while `Modules/Loop/HostSupervisor.cs:480` guards early service startup by service address (`:495`), not conversation execution by home. Give conversation executors an explicit, home-scoped ownership mechanism rather than treating these guards as its proof.

2. **Blocking — not every keyed producer passes through the desktop client.** F:659–660 and F:1401 use that claim to justify desktop-only receipts, but `Core/Sessions/HelperDesk.cs:185–189` and `PartnerDesk.cs:136–147` create keyed words directly through `KeepSaidAsync`. Include those producers in the receipt protocol or narrow the guarantee explicitly.

3. **Non-blocking — today’s appointment does not offer individually ticked acts.** F:391 and D212:51 describe acts the owner ticked, whereas `Modules/Platform/StandingConfirmation.cs:247` has no requested acts list and `:860` snapshots `OffersActs(role)`; `Setup` is the separate tick. Describe individual act selection as a proposed change if that is intended.

**2. Roles**

1. **Non-blocking — the principal authority boundaries are now sound as requirements.** The exclusive shapes, closed act vocabulary, offered flags, scope checks, per-id seeds and absence of configurable containment fields prevent a role from inventing tools, owner-only acts or launcher privileges (F:343–377, F:405–407). Preserve the stated ordinary-start containment exception: configuration does not strengthen today’s best-effort containment into a guarantee.

2. **Blocking — live attenuation needs its own retained ceiling.** Appoint with `{answer,defer}`, narrow the role to `{answer}`, then restore `defer`: the three-way intersection at F:390–397 admits `defer` again without *Appoint again*, contrary to F:394 and row y-ii. Retain reductions for that appointment, and require execution-time checks for queued acts and resolved targets; today `StandingConfirmation.RunAct` checks owner marks and appointment liveness, not new catalogue restrictions (`Modules/Platform/StandingConfirmation.cs:1300–1311`).

**3. The store (§13)**

1. **Blocking — marking an effect does not fence its execution.** An executor can commit its mark, pause, lose ownership, then resume directly into the spawn or prompt without making the “next mark” that F:1313–1316 expects to fail. Specify ownership exclusion through the actual effect, or fencing enforced by its recipient, and test suspension **after mark commit but before effect**; no external effect should be placed inside a SQLite transaction to solve this.

2. **Blocking — the desktop receipt lookup races the service’s removal.** Under F:656–660, a retry can read “no receipt,” pause, then reach `SayAsync` after another dispatcher commits the receipt and removes the original word. `Core/Sessions/SessionLedger.cs:956` then finds no pending key and creates another word at `:965`; the two stores need a durable admission/settlement protocol, not a client-side precheck.

3. **Blocking — `opens` belongs in the service, but one field is insufficient for the promised repeated answer.** F:589–591 and F:1302–1303 promise that a repeated open returns both its record and the words originally transferred, including after a lost answer. Once those words leave `said` (`Core/Sessions/Sessions.cs:1602–1607`), the `opens` column alone retains no transfer result; specify durable replay evidence committed with the service open.

4. **Blocking — the downgrade table misses a successor of the destination.** At F:1357, suppose newer Daoris opened B, older Daoris finds no kept conversation and opens C from B, then crashes before transferring B’s words: today that opening precedes delivery and removal (`Driver/Conversations/Chat/BriefedMove.cs:130–158`). On return, the table’s “else it delivers” permits delivery to B despite C; reconcile the actual head and its attempts in every phase before delivering.

5. **Blocking — migration admission needs a check inside every writing transaction.** F:1282–1286 specifies checking a newer stamp when opening, but a process already holding its connection can survive another process’s migration and subsequently write. Require the oldest-writer check under the same write lock as each mutation, with a proof that pauses an older writer before migration and resumes it afterward.

6. **Blocking — reconciliation can repeat already announced news.** F:828 keys a journal occurrence by position, while F:830 keys reconciliation by `updated`; after trimming, another teller can claim the second key for the same end within the retained horizon. Moreover, `updated` changes on note-only writes (`Core/Sessions/Sessions.cs:886–890`), so retain a canonical occurrence identity or durable aliases across both paths.

7. **Non-blocking — ownership and most JSON exceptions are well placed, but the inventory is incomplete.** Driver ownership, service separation, forward migrations and the prohibition on transactions spanning external effects are appropriate (F:1238–1258, F:1282–1295); keeping credentials, external formats, repository records, bootstrap files and streams as files has stated reasons. Explicitly place one-use git grants: the driver writes them and GitDoor consumes them by rename/delete (`Driver/Platform/GitAccessRules.cs:153–183`), yet neither their migration nor an exception appears beside F:1387–1394’s retained files.

**4. The module map (§14)**

1. **Blocking — the proposed enforcement does not yet hold the promised graph.** The rank order is acyclic, but F:1498 and F:1517 treat namespace `using` lines as dependencies after migration; fully qualified references need no such line, and this checkout already uses that form (`Driver/Sessions/SessionsCommand.cs:185`). Keep resolved type-reference checking, or equivalent comprehensive scanning, and prove rejection of qualified, aliased and static references as well as ordinary imports.

2. **Non-blocking — the groupings are plausible targets, with real cuts still required.** For example, F:1472 puts `ChatGoOn` in Course while F:1479 puts its concrete `ChatRunner` and `ChatContext` dependencies in Loop (`Driver/Conversations/Chat/ChatGoOn.cs:87–105`). Name those port cuts in the census, and include linked consumers in each mechanical move: GitDoor’s explicit source paths (`Daoris.Desktop.GitDoor.csproj:20–37`) also need updating, so moving files and adding usings alone is insufficient.

**5. Names**

1. **Non-blocking — as defined here, this is one concept with two shapes.** F:307–309 and F:888 expressly make D205’s actor a working role; F:890 supplies no independently configured actor entity that would justify a second page concept. Use **role / 职务** consistently for configured positions, label their agent/model choices as staffing, and retain `actor`/角色 only for legacy compatibility surfaces until migrated; amend F:899–902 accordingly.

**6. The migration**

1. **Blocking — move the service-open prerequisite before d.** The order at F:1004 puts d before f-i, although d’s proof requires a retried atomic open that moves nothing twice (F:1074), and f-i introduces its unique `opens` key (F:1076). Give the service open and durable transfer-result contract an earlier standalone row, then keep p before every word-moving adoption.

2. **Non-blocking — finish making the module rows individually landable.** F:1531 groups Platform with Store and Service with Install and Instance, despite F:1528’s one-subdomain rule; split those groups and give each cut its concrete compatibility/build proof. The whole-design two-reader gate, the marked authority/deletion rows and SWITCHCTX1’s independent outstanding read remain correctly retained (F:968, F:1002, F:852).

**Verdict: another round.** The remaining authority, cross-store delivery, effect-fencing and downgrade gaps require protocol decisions; they are not bounded wording corrections.

## Opus (foundation), round 4

The independent Opus 5.5 reader's foundation read of round 4 at `6bac9a0be`: 12 findings, 3 blocking for Part A; Part A accept after named changes; Part B's agenda right as far as it goes, with six items added. Codex's read of round 4 was owed when this was recorded, its credits out; it follows, at `86f32f40c`.

### 0. Both reads of round 3

I read at HEAD `6bac9a0be`. Its `src/` is `e8f1aed4f`'s plus one test (`Trees/ReviewGraphCensusTests.cs`), so round 4's `e8f1aed4f` lines hold, and every line below is HEAD's. Paths use the design's aliases. I ran read-only git and text searches, and nothing else.

#### The Opus reader's round 3 (16 findings, 2 blocking)

| # | Round 3 | Round 4 | Does the answer hold? |
|---|---|---|---|
| 1 | blocking | Taken, in Part B (§3.3, §13.3, §13.4; f-i's three cases) | Deferred to Part B, and the reason holds. A move is taken over only from a run that `HomeLease`'s pid-and-start-time test finds dead, which closes the case. Two installs' shells and a hung executor are on §16.3. |
| 2 | blocking | Taken, in Part B (§13.5's two classes; z-ii, z-iv and z-xi prove a revoke) | Deferred, and the reason holds. z-xi's choice file by file is on §16.3. |
| 3 | — | Taken, in Part B (§13.2; z-i lands within its p95 limit) | Holds. |
| 4 | — | Taken, in Part B (§13.5's *settled*) | Holds. The headless start and the silent swap are on §16.3. |
| 5 | — | Taken, the stricter, in Part B (the settlement is the service's) | Holds. |
| 6 | — | Taken, in Part B (bindings set aside) | Holds. |
| 7 | — | Taken. w-0 is Part B's, and mod-xi moves only the driver's own files | Holds. |
| 8 | — | Taken, in Part A (§14.1, §14.2) | **Partly open.** Three edges remain: the account rule's own file names Course (`ResumeAccount.cs:99-101`); `SessionGroups.cs` takes `SessionRecord` up with it; and the gate's scan still counts DaorisHome's member names. See findings 5 and 6. |
| 9 | — | Taken | Holds. No driver type has a subdomain's name either (I checked all twenty-one). |
| 10 | — | Taken, in Part A (§7 row 21, §14.5, §14.6, D212's amend list) | Holds. |
| 11 | — | Taken, in Part B | Holds. |
| 12 | — | Taken, in Part B (d-i) | Holds. |
| 13 | — | Taken (row a-i's log line; `D137.md:865-868`) | Holds. The new dated line states a Part B proposal as decided (finding 11). |
| 14 | — | Taken, in Part B | Holds. |
| 15 | — | Taken, as the steward's decision | **The premise is wrong, and I made the same mistake in round 3.** Ten shipped zh strings say 职责 for an appointed role. The glossary also refuses two terms that share 角色. See finding 2. |
| 16 | — | Taken, in Part B | Holds. |

#### Codex's round 3 (17 findings, 11 blocking)

| # | Round 3 | Round 4 | Does the answer hold? |
|---|---|---|---|
| §0: round 2's 1.1, 2.2, 3.1, 4.3, 5.1 (open) | blocking | Answered by 3.4, 2.2, 3.1, 3.6 and 6.1 | In Part B, as those rows below. |
| 1.1 | blocking | Taken with O3-1, in Part B | Holds. |
| 1.2 | blocking | Taken with O3-5, in Part B | Holds. |
| 1.3 | — | Taken (§2.7, §7 row 17, D212 point 5) | Holds. |
| 2.1 | — | Taken (§2.3) | Holds. |
| 2.2 | blocking | Taken, in Part B (each narrowing lowers the grant; the act is checked again at its run) | Deferred, and the reason holds. The order across the two stores until z-iv is on §16.3. |
| 3.1 | blocking | Taken with O3-1, in Part B | Holds. |
| 3.2 | blocking | Taken, in Part B (`said_settled`) | Holds. |
| 3.3 | blocking | Taken, in Part B (`session_opens`) | Holds. |
| 3.4 | blocking | Taken, in Part B (the head is read before delivery; `Lines.Forward`) | Holds. The new service step is on §16.3. |
| 3.5 | blocking | Taken, in Part B | Holds. |
| 3.6 | blocking | Taken, in Part B (the transition number) | Holds. |
| 3.7 | — | Taken (§13.6's list) | Holds. |
| 4.1 | blocking | Taken, in mod-0 (Part A) | **Open in part.** The design says the gate counts every form *as its Loop rule reads type names today*. Today's scan skips every name that follows a `.` (`StructureCheck.cs:385`). So mod-0 needs a rule that does not exist yet for qualified, alias and static references (finding 5). |
| 4.2 | — | Taken in part, in Part A | **Open in part.** Only `ChatGoOn`'s port is named. Nine Course files and `Acp.cs` name the chat runner's core. The launcher, PieceStart and fourteen tests that read source by path are linked consumers left off the list (findings 1, 7). |
| 5.1 | — | Disputed; 角色 kept | The answer on the concept holds. Its reason, that *every shipped string already says 角色*, is wrong: ten strings say 职责 (finding 2). With either word, some shipped strings now change. |
| 6.1 | blocking | Taken, in Part B (d-i) | Holds. |
| 6.2 | — | Taken (mod-i to mod-xxi) | Holds. |

None of the 33 findings was wrong.

### 1. Part A, row by row

**a-i (one account rule).** The code stands on nothing in Part B. `LineAccount` is pure, sits beside `ResumeAccount`, and is read by today's callers: `HarnessRoster.ResumeAsync` (`:975`), `ChatGoOn.SwitchableAsync` (`:489-492`), Ask Daoris's resume (`:730-733`), `LimitWaits` (`:156-157`, `:194-220`) and the closed quest's hold (`Driver.cs:2381-2384`). The LIMITSWITCH2 lines it cites hold: `LimitMove.cs:220`, `:296`, `:317-318`; `Continuation.cs:60`, `:588-594`; `HarnessRoster.cs:1030-1048`, `:1762-1779`.

Its claim to change no behaviour breaks at two points:
- **The earliest reset.** Row a-i calls it "one computation", and §7 row 3 makes it a D206 point 2 note: one reset over exactly the accounts the line may take, with ineligible accounts adding none. Today three different sets are read:
  - the walk's states (`Unready`, `HarnessRoster.cs:1766`);
  - the switch's walk against the own reset (`:1032-1048`);
  - for a limited conversation's wake, every cooling account of the agent (`LimitWaits.cs:157`, `:220`, through `CoolingsOf`, `HarnessRoster.cs:251-256`).

  So a single computation is a change of behaviour. It also leans on step 4's set, which a-ii, a-iii and LIMITRESUME1e define (finding 3).
- **The walk's side effects.** `LineAccount.Decide` takes "the walk's answer". But the roster walks only where the own account cools under `switches`, or in `ElsewhereAsync`, and a walk counts the start it chooses (`HarnessRoster.cs:894-911`). If `Decide` is fed a walk up front, starts are walked and counted where none were before (finding 4).

The proof (equality with the answers today's code gives, case by case) is the right proof. It must also assert, per case, how many walks were made and how many starts counted. The row lands alone, before mod-0.

**b (a word keeps its identity).** It stands on nothing in Part B. The file grows additively and stays safe for an older shell, whose reader takes each field by name (`SessionWords.cs:981-996`). But the row changes behaviour.
- The record path's `SayAsync(id, word.Text, word.Files, ct)` (`:701`, `:782`) gains the producer's key, so a producer's retry after a shell restart keeps one word where today it keeps two. That is the decided guarantee, so it is a repair, not a reorganisation (finding 8).
- The record path must still pass no `choice`. The say door refuses a choice from any author but the supervisor and then keeps nothing (`SessionLedger.cs:901-907`). The proof's "never reads as the supervisor's" should assert that such a word is still kept.
- "Delivers once" across a restart holds only while the word still waits on `said` (`:956`). After a take, it is row p's.

**c (the participant's work, read once).** The rule matches `HoldsSlot` (`Planner.cs:872-885`). D203 point 1 decides the partner's reading, and `identity.ts:79` is today's gap. Four things are missing:
- There is no partner string to draw with: `en/work.json:27-29` holds conversation, session and helper only.
- The service answers `work` on loopback, which walks `session-record-fields.md`'s rows 8, 10, 11 and 12. The row names none of them.
- The marks the rule reads sit on `PriorSession` and `SessionView` (`Planner.cs:291-408`, `:759-817`, in Work orders), and the binding comes through `HarnessConversations.Read` (`GoOn/HarnessConversations.cs:54`, in Course). So `ParticipantWork.Of`, placed in Participants, names two subdomains above it from its first commit.
- The row does not say what happens to a census A disagreement other than the partner's.

See finding 9. The row lands alone, before mod-0.

**mod-0 (the structure gate).** The census, as specified, is not exact:
- It misses the qualified, alias and static forms until a new rule is written.
- It reads member names as type names. DaorisHome's `HomeEntry` fields make eleven false upward edges that no cut can remove.
- It does not place every file.
- It leaves today's test folders failing the mirror rule (`StructureCheck.cs:183-213`).

See finding 5. On the graph: the order is total, so the target is acyclic by construction. The code is not. I scanned the tree with the gate's own identifier rule over the map's placements. It is rough, and false positives included. It finds about 1,100 upward type names in 254 of the 765 files it places. §14.6 names four cut families. The gate keeps the Loop rule as absolute, which conflicts with the map (finding 1).

**u (the role's name).** It cannot land as written (finding 2):
- `names:check` refuses two terms that share a Chinese name and exits 2 (`names-check.mjs:118-121`, `:299-302`), so *role* 角色 cannot sit beside *actor* 角色.
- "No page string changes" is false: ten shipped strings say 职责 for the appointed role.

**mod-i to mod-xxi (the folder moves).** Most are moves that change no behaviour. The exceptions are blocked by the kept Loop rule (finding 1):
- the ten `LoopContext`-built `Loop/` classes the map sends down, which cannot move;
- the chat runner's core, which cannot move into Loop while nine Course files, `Acp.cs` and six Workplace files name it.

The proof also needs these builds and tests: the launcher, PieceStart, the modules' tests and the service's two Mcp tests, all of which compile or read moved files (finding 7).

### 2. The line

- **§7 row 3 and D212 point 7's earliest reset** ("of exactly the accounts it may take … an account that cannot run adding none") are Part B's. They read step 4's set, which needs a-ii's recorded pick, a-iii's `ElsewhereAsync` and LIMITRESUME1e, yet row a-i's notes carry them (finding 3).
- **D212 accepts points 1 and 2 "as reads",** and both carry Part B clauses:
  - point 1: the binding moved into the desktop store;
  - point 2: `actor` as the seeded id, the role and revision on the binding, and the census against the catalogue.

  Part A should name the clauses it accepts (finding 10).
- **Row b's proof** of "delivers once" across a restart, after the take, stands on row p's settlement. Scope it to a word that still waits.
- **D137's new dated line** (`D137.md:865-868`) states row a-ii's binding in the indicative (finding 11).
- **The cuts into Loop are Part A's, not Part B's,** but Part B's rows e, f and h would write the course's executors into Course. The executors take `ChatContext` (`Chat/ChatContext.cs:14`), so finding 1 must settle first.

### 3. Part B

§16.3 is the right core: each item is a protocol round 4 introduced. It is missing six items:

1. **Row o's shape.** The shape a word kept for a moving record takes so that an older shell's sweep does not resume it (§3.4 rule 4). This has been open since round 2, and the text still says "where no such shape exists the row waits for the rollback window". It is a protocol decision, not a measurement.
2. **The version matrix.** A table for each service protocol (`opens`, `Continued`, the attempt's answer, `said_settled`, the journal) giving: an older desktop against a newer service, and a newer desktop against an older service. Today each is said in a different section.
3. **The say door's head read.** `Lines.HeadOf` walks the `from` chain inside the exclusive step for every word, with no index on `from_session` (§11, *unverified*). This is the hottest path, and row d-ii's landing limit should be fixed in round 5.
4. **The settlement's horizon.** Seven days and 10,000 rows bound D137's and D188's *never sent again* for a producer's retry. That is a decision note, not only a bound row p measures.
5. **The `delivered` window.** Between `delivered` and the take (§13.4 step 4), across a downgrade the word still waits, marked attempted, and an older build settles it as uncertain. §13.5's `delivered` row should say so.
6. **The chat runner's boundary** (finding 1), before rows e, f-i and h.

### 4. Claims about the code

1. **§0.2, §1 Q16, §6, §16.1's 15 and 5.1, D212 point 14, the brief §8 note and row u:** "every shipped string already says 角色 … no page string changes". Ten strings say 职责 for an appointed role: `zh/projects.json:393`, `:398`, `:399`, `:402`, `:404`, `:413`, `:414`, `:415`, `:416`, and `zh/settings.daoris.json:179`.
2. **Row u:** "*actor*'s entry keeps 角色" beside *role* 角色. `src/Daoris.Web/scripts/names-check.mjs:118-121` records two terms sharing a Chinese name as malformed, and `:299-302` exits 2.
3. **§14.5 rule 2 and row mod-0:** the direction rule reads "every type reference … as `StructureCheck` reads Loop's today, so an ordinary, a fully qualified, an aliased and a static reference all count". Today's `Identifier()` skips every name that follows a `.` (`StructureCheck.cs:385`). So `Daoris.Driver.HolderKey.Taken` (`Sessions/SessionsCommand.cs:185`) names nothing to it, and neither does any `using static Daoris.Driver.X;` (for example `Accounts/AccountCooling.cs:5`, `Agents/Tools.cs:6`).
4. **§14.1 and §11:** DaorisHome "names none", and "row mod-0's census is the exact list". The gate's scan reads `HomeEntry` members as type names (`Platform/DaorisHome.cs:326`, `:352`, `:354`, `:362`, `:423-427`): DriverConfig, Notices, HelpProposals, OpinionGates, AgentOffers, GitIdentities, ConfiguredAccess, PluginState, UsageHistory, RuleProposals and SessionBranches, each a type of a higher subdomain.
5. **§14.6:** "source-scan tests find files by name … so a move breaks none". Fourteen sites read driver sources by folder path:
   - `Driver.Tests/Conversations/DrivenInboxTests.cs:576`, `:579`;
   - `Loop/DriverCommandTests.cs:138-139`, `:166`, `:184`, `:203`, `:284-285`;
   - `Platform/InstanceDoorTests.cs:831`;
   - `Trees/GitLaunchTests.cs:200`;
   - `Modules.Tests/Loop/DriverLoopSendsBackTests.cs:38`;
   - and in the service's tests, `Daoris.Service.Tests/Mcp/McpRoleFilterTests.cs:72` (`Conversations/Domain/PartnerPermissions.cs`) and `McpToolRegistrationTests.cs:61` (`Brainstorms/Domain/BrainstormPermissions.cs`).
6. **§14.6:** the launcher's linked consumer is "its compile of `StagedBuild.cs` by path".
   - The launcher compiles nine driver files (`Daoris.Desktop.Launcher.csproj:23-40`): StagedBuild, InstallSwap, HeldFiles, HomeWrites, HomeLease, HomeIdentity, DaorisHome, DriverException and AtomicFile.
   - PieceStart compiles two more: PieceLauncher and ProcessJob (`Daoris.Desktop.PieceStart.csproj:21-22`).
7. **§14.2 and §14.6:** "Loop's rules leave it … each a move with its tests, never a rewrite", and "its cuts after it". Ten `Loop/` classes the map moves down are built from `LoopContext`, most of them from `Driver` too:
   - `DriverContinuations.cs:24`, `DriverHandOvers.cs:18`, `DriverHelpers.cs:28`, `DriverPartners.cs:58`, `DriverIntakes.cs:25`, `DriverBrainstorms.cs:24`, `DriverMeetings.cs:29`, `DriverOpinions.cs:23`, `OpinionDeliverer.cs:22`, `OpinionGateLook.cs:11`.
   - `StructureCheck.cs:216-217` refuses any of them outside Loop.
8. **§14.3, the chat runner's one port** ("`ChatGoOn`'s use of … `ChatRunner` and `ChatContext`"):
   - Nine Course files name the chat runner's core: `BriefedMove`, `ChatGoOn`, `ChatHandOver`, `CurveMove`, `LimitMove`, `LimitWaits`, `StartWith`, `GoOn/UncertainWords` and `HandOver/HandOver`. The names are `ChatContext` (`Chat/ChatContext.cs:14`), `ChatStart` (`Chat/ChatRunner.cs:8`), `ChatTurns`, `ChatHelp` and `ChatPending`.
   - So does `Agents/Acp/Acp.cs` (`ChatTurns`).
   - Six Workplace files name the wiring view the map puts in Loop: `Platform/DownloadClearing.cs`, `SpaceCommand.cs`, `SpaceFree.cs`, `SystemSpace.cs`, `ToolHomeLeftovers.cs` and `Trees/Branches/BranchTidying.cs`.
9. **§14.2 row 9:** the account rule sits in Agents "since the roster returns it". But:
   - `ResumeChoice` names Course's `ContinueReason` and `ContinueWhy` (`Accounts/Rotation/ResumeAccount.cs:99-101`; declared at `Conversations/Domain/Continuation.cs:9`, `:54`);
   - the roster names `SwitchCandidate` (`HarnessRoster.cs:2193-2212`), declared in `Accounts/Domain/SwitchDecision.cs:7`, which row 14 sends to Course;
   - Acp names `ContinueWhy` (`Agents/Acp/Acp.cs:3401-3421`).
10. **§14.2 row 15:** `Trees/SessionGroups` goes to Work orders "as the service's session records as groups". Its file declares `SessionRecord` (`Trees/SessionGroups.cs:14`). The planner's file and the intake's hold the service's record views:
    - `Quests/Domain/Planner.cs:4` (QuestView), `:246` (PriorSession), `:639` (SaidWordView), `:728` (RepoView), `:752` (SessionView);
    - `Intake.cs:8` (AskView), `:106` (LaneView).

    These are the records `ServiceClient` (Service, rank 4) returns.
11. **§14.2 row 1 and row 12:** Platform's containment names the piece launcher, which the map places in Participants: `Platform/AgentStarts.cs:33`; `Agents/Consoles/AgentJobs.cs:77`, `:111-121`, `:139`.
12. **§14.2 row 7:** `MachineLog` and `HomeWrites` are placed in Events. They are named by:
    - Platform: `AgentStarts.cs:31`, `:41`, `:53`;
    - Service: `ServiceClient.cs`, `LedgerChanges.cs`;
    - Install: `Setup/UpdateCommand.cs`, `Agents/ToolInstall/`.

    Both name nothing above Platform, since the GitDoor and the launcher compile them with the base library (`GitDoor.csproj:26`, `Launcher.csproj:30`). Separately, `Notices.cs` (Events) names `AttentionEvent` and `AttentionKind`, which are declared in `Quests/Domain/Attention.cs:10`, `:45`, the file round 4 moves whole to Work orders.
13. **§14.2 does not place:**
    - sixteen files of `Sessions/Domain/`, among them `SessionEnded`, `SessionEvents` and `StartRun`;
    - `Sessions/Events/HeldPermissions.cs`, `Sessions/LookFacts.cs` and `Sessions/SessionsCommand.cs`;
    - `Platform/DriverChoicesCommand.cs`;
    - `Setup/HeldFiles.cs`, which the launcher compiles with InstallSwap.
14. **Row a-i, though written in round 4, cites lines older than `e8f1aed4f`:**
    - `HarnessRoster.cs:1014` for the inline twin, which is at `:1022`;
    - `ChatGoOn.cs:571-587` and `:672-688` for the conversation's and Ask Daoris's go-on, which are at `:614-645` and `:723-746`.
15. **§3.2 step 5 and row a-i:** "the earliest reset one computation" under "it changes no behaviour". Today there are three sets: `HarnessRoster.cs:1766`; `:1032-1048`; and `LimitWaits.cs:220` over `CoolingsOf`, which keeps every cooling account of the agent, the walk's or not.

### 5. Findings

1. **Blocking for Part A. The kept Loop rule contradicts the map's Loop boundary** (§14.2, §14.5 rule 4, §14.6; claims 7 and 8).
   - **What is wrong.** Under the absolute `loop` rule (`StructureCheck.cs:216-217`):
     - mod-xii, mod-xiv, mod-xv and mod-xvi cannot move the ten `LoopContext`-built classes out of Loop;
     - mod-xxi cannot move the chat runner's core or the wiring view into Loop while lower files name them.

     "Move, then cut" and "never a rewrite" cannot both hold.
   - **Smallest change.**
     - Keep the `LoopContext`-built `Driver*` and `Opinion*` classes in Loop, as the look's executors, and move out only the rules §14.1 lists.
     - Place `ChatContext` and `ChatStart` in Course, or name one port family covering all nine Course files and `Acp.cs`.
     - Place `WiringSizes` and `WiringBudget` with the space code in Workplace.
     - State whether an edge into Loop is a census exception until its cut, or whether every cut into Loop lands before the move.
2. **Blocking for Part A. Row u's premise and its glossary entry both fail** (§6, row u; claims 1 and 2).
   - **What is wrong.** Today's page names the appointed role with two Chinese words: 角色 at `settings.daoris.json:108` and `:139`, and 职责 in ten strings. And `names:check` exits 2 on *role* 角色 beside *actor* 角色.
   - **Smallest change.**
     - Row u replaces 职责 with 角色 in the ten strings (任命的角色 where the appointed shape must be said), under the UI and translation contracts, proven by `i18n:check` and `names:check -- --strict`.
     - *role* takes 角色, and its match covers the `Actor(s)` labels. *actor* becomes `use: "role"` (code and prose only).
     - Correct §0.2, §1 Q16, §6, §16.1's 15 and 5.1, D212 point 14 and the brief §8 note.
3. **Blocking for Part A. Row a-i asserts a rule change and a D206 note that lean on Part B** (§3.2 step 5, row a-i, §7 row 3; D212 point 7; claim 15).
   - **What is wrong.** "One computation … ineligible ones adding none", over step 4's set, is not today's answer, which reads three sets. Step 4's set is a-ii's, a-iii's and LIMITRESUME1e's.
   - **Smallest change.**
     - a-i reads each caller's set as today, behind one function that names which set it reads.
     - §7 row 3, with its D206 point 2 note, moves to Part B beside a-iii.
     - D212 point 7's sentence on the earliest reset is marked as Part B's.
4. **Not blocking. a-i's pure function against the walk's side effects, and its axis** (row a-i; claim 14).
   - **What is wrong.** `SelectAsync` counts a chosen start (`HarnessRoster.cs:894-911`), so feeding `Decide` "the walk's answer" up front walks where today nothing does. The "constraint (none, a pick)" axis names a pick that no record keeps until a-ii. The row's lines are older than `e8f1aed4f`.
   - **Smallest change.**
     - The walk is asked only where today's code asks it: `Decide` runs twice, or takes the walk as a lazy input. The proof asserts, per case, how many walks were made and how many starts counted.
     - The "pick" on the axis is a handed quest's named account, or the hand-over press's account opened as a pick (`:887`).
     - Use `e8f1aed4f`'s lines.
5. **Not blocking. The census of mod-0 is not exact as specified** (§14.5, §11; claims 3, 4 and 13).
   - **Smallest change.** mod-0's specification adds:
     - a rule for the namespace-qualified chain (`Daoris.Driver.<Name>`, `global::`), alias targets and `using static` targets;
     - a rule that a name the file declares as a member is no edge, with a planted member named like a higher type in the proof (or Roslyn's semantic model in the tests);
     - every file placed in `structure.json`;
     - today's test folders listed as not yet moved, each mod row moving its tests.

     In §11, replace "exact" with "every named reference".
6. **Not blocking. Several vocabularies sit above the files that name them** (§14.2; claims 9 to 12, and row c).
   - **What is wrong.** The upward names follow from where these types are placed, not from the code needing them there.
   - **Smallest change.** Move each down in §14.2:
     - `ContinueReason`, `ContinueWhy` and `SwitchDecision` with the account rule into Agents, or `ContinueWhy` into Events beside `NoteCodes`;
     - `SessionRecord` and the planner's and intake's record views into Service;
     - `PieceLauncher`, `PieceStarts` and `PieceJobs` into Platform, with `AgentStarts`;
     - `MachineLog` and `HomeWrites` into Platform;
     - `AttentionKind` and `AttentionEvent` split out into Events;
     - `HarnessConversations`, which stores the binding, into Participants.

     And mod-0 names each edge by its family and the subdomain row after which it is cut, not by a row id the design does not have yet.
7. **Not blocking. The list of linked consumers is short** (§14.6; claims 5 and 6).
   - **Smallest change.** Name in it the launcher's nine files, PieceStart's two and the fourteen tests that read source by path. Make those tests name-based first, as D166 point 4 orders. Each move's proof builds the launcher and PieceStart and runs the modules' and the service's Mcp tests.
8. **Not blocking. Row b repairs behaviour, though the Standing says it does not** (the Standing, row b).
   - **Smallest change.**
     - The Standing names b's repair: a producer's retry after a shell restart now keeps one word, under D137 and D188, so no new decision is asked.
     - b's Process case restarts before the take, and says the case after the take is row p's.
     - The proof asserts that the record path passes no `choice` (`SessionLedger.cs:901-907`).
9. **Not blocking. Row c leaves out its string, its field chain and its dependency** (row c).
   - **Smallest change.**
     - Add `work.identity.partner` (Partner / 搭档, D203 point 9) with `i18n:check` and `names:check`.
     - Name `session-record-fields.md`'s rows 8, 10, 11 and 12.
     - `ParticipantWork.Of` takes the marks as a value declared in Participants.
     - A census A disagreement other than the partner's moves no reader in c.
10. **Not blocking. D212's Part A covers Part B clauses** (D212, the status paragraph and points 1, 2 and 7).
    - **Smallest change.** Name the clauses Part A accepts, or those it leaves to Part B: point 1's store, point 2's binding and census, point 7's recorded pick, settled selection and earliest reset.
11. **Not blocking. A dated line on an accepted decision states a proposal as decided** (`D137.md:865-868`).
    - **Smallest change.** Change "is kept on the start's binding" to "is proposed to be kept on the start's binding (FOUNDATION1 Part B, row 1a-ii)".
12. **Not blocking. §16.3 is missing six items** (§3 items 1 to 6).
    - **Smallest change.** Add them to round 5's agenda.

12 findings, 3 blocking for Part A.

**Verdict on Part A: accept after named changes the steward can check without another read.** Findings 1 to 3 are each a bounded edit against the lines cited: the Loop boundary in §14.2 and §14.5, row u's strings and its glossary entry, and §7 row 3 moved to Part B.

**Verdict on Part B's agenda: §16.3 is right as far as it goes.** Add the six items in §3: row o's shape, the version matrix, the say door's head read, the settlement's horizon as a decision, the `delivered` window, and the chat runner's boundary.

## Codex, round 4

Codex's read of round 4 with the Opus reader's named changes taken, at `86f32f40c`, run by the steward read-only on main from outside the install: 31 findings, 4 blocking for Part A and 3 for Part B; Part A accept after named changes, checkable without another design read; Part B's agenda (the design's §16.3) right, with three additions.

Reviewed HEAD `86f32f40cc957fe20367d41d5674e67bf05ca969`, read-only. No files changed or tests run; nothing under checkout-relative `local/` was read.

**F** = `docs/2026-10-25-foundation-design.md`. Code paths use its aliases, including **Chat** = `Driver/Conversations/Chat/`. “Blocking” below means blocking Part A unless explicitly marked Part B.

### 0. My round-3 findings

| Finding | Assessment | Reason and evidence |
|---|---|---|
| 1.1 One shell per home | Deferred to Part B — non-blocking A | The false premise is withdrawn; process-run ownership replaces it. F:2075; `Driver/Platform/HomeLease.cs:427`. |
| 1.2 Keyed producers bypassing the client | Deferred to Part B — non-blocking A | Settlement now belongs inside every service keeping door. F:2076; `Core/Sessions/HelperDesk.cs:180`. |
| 1.3 Individually ticked acts | Taken — non-blocking | The grant is correctly described as `OffersActs`, with the separate setup tick. F:2077; `Modules/Platform/StandingConfirmation.cs:860`. |
| 2.1 Authority boundaries and containment | Answered — non-blocking | Configuration does not strengthen ordinary containment or weaken pieces. F:337; `Driver/Platform/AgentStarts.cs:13`. |
| 2.2 Retained attenuation | Deferred to Part B — non-blocking A | A retained, lowered ceiling answers the restore counterexample; its implementation remains a protocol question. F:2079; `StandingConfirmation.cs:1300`. |
| 3.1 Effect fencing | Deferred to Part B — non-blocking A | Dead-run-only takeover addresses suspension between mark and effect. F:2080; `HomeLease.cs:427`. |
| 3.2 Settlement race | Deferred to Part B — non-blocking A | Admission and settlement now meet in the service transaction. F:2081; `Core/Sessions/SessionLedger.cs:955`. |
| 3.3 Open replay evidence | Deferred to Part B — non-blocking A | `session_opens` retains the result after words leave `said`. F:2082; `Core/Sessions/Sessions.cs:1593`. |
| 3.4 Destination’s successor | Deferred to Part B — non-blocking A | Every delivery phase now reconciles the head. F:2083; `Chat/BriefedMove.cs:130`. |
| 3.5 Migration admission | Deferred to Part B — non-blocking A | The stamp is checked inside each writing transaction. F:2084. |
| 3.6 Announcement identity | Deferred to Part B — non-blocking A | The state-transition counter answers the repeated-end case; other occurrences need the addition below. F:2085; `Driver/Platform/Notices.cs:146`. |
| 3.7 One-use git grants | Taken — non-blocking | Their file exception and consumption mechanism are explicit. F:2086; `Driver/Platform/GitAccessRules.cs:153`. |
| 4.1 Comprehensive reference checking | Open — **blocking** | The revised scanner specification still permits missed declarations and references; §1.4 below. F:1905; `Driver.Tests/StructureCheck.cs:388`. |
| 4.2 Ports and linked consumers | Open — **blocking** | The consumer list is repaired, but the expanded chat boundary leaves concrete upward edges; §1.19 below. F:1812; `Chat/ChatPending.cs:218`. |
| 5.1 One concept, one page name | Answered — non-blocking | The steward’s choice of 角色 holds after correcting the shipped-string premise. F:1047; `Web/scripts/names-check.mjs:118`. |
| 6.1 Open before word-moving adoption | Deferred to Part B — non-blocking A | Standalone d-i now precedes its consumers. F:2090. |
| 6.2 Individually landable moves | Taken — non-blocking | The grouped moves are split, with linked-consumer proofs. F:2091, F:1943. |

None of the round-3 findings was wrong.

### 1. Part A, row by row

The unflagged folder rows have appropriate proofs **as requirements**, conditional on the corrected mod-0 census: unchanged namespaces, today’s gates, linked builds, and the modules’ and service’s affected tests. They have not yet demonstrated those proofs by running them (F:1320, F:1943).

1. **a-i — blocking wording contradiction.** F:1200–1202 permits the lazy walk only for cooling under `switches` or the live move’s `ElsewhereAsync`, “and nowhere else”; today `HarnessRoster.ResumeAsync` also walks through its own `ElsewhereAsync` (`HarnessRoster.cs:1052`, `:1063`) and its no-toolchain branch (`:984`). Name and preserve those branches, and include no-toolchain and both `walks` settings in the existing answer/walk/count comparison; no Part B contract is needed.

2. **b — non-blocking, with one missing route in the proof.** The additive storage and before-take boundary stand independently of settlement, but `Modules/Loop/SessionWords.cs:843` forwards a held word through another `SayAsync` without key, level or display choice. Explicitly carry those through `PassAsync`, and prove a restarted hold forwarded to a live successor retains its key and deduplicates a retry while pending there.

3. **c — non-blocking, provided “drawn as a partner” stays that narrow.** Changing `isHelper` to exclude partners also changes the composer and offered acts (`Web/work/WorkFrame.tsx:511`, `Web/work/acts.ts:139`), beyond changing their displayed identity. Preserve those restrictions for partners and add assertions for them; also distinguish “old record answered by a new host” from “old host omitting `work`” in the conflicting proof wording at F:1275–1278.

4. **mod-0 — blocking; the census is not sound as specified.** A file-wide exemption for member *names* can hide a real type reference elsewhere in that file, and today’s declaration regex consumes `record struct` as the name `struct`, which `Declarations` discards (`StructureCheck.cs:238`, `:388`; an existing declaration is `Driver/Plugins/BoundedLines.cs:9`). Choose the offered Roslyn semantic option, ignoring member occurrences rather than spellings, and add mixed member/type, record-struct, record-class, namespace-alias and nested-alias fixtures. Exceptions must enumerate exact file/type edges grouped by family, so an existing family cannot admit a new crossing.

5. **u — non-blocking.** The ten-string change and `actor → role` glossary pointer now match the code and avoid the duplicate-name rejection (`Web/scripts/names-check.mjs:110`, `:118`). Its parity, naming and screen proofs are appropriate, with no catalogue-authority dependency (F:1305).

6. **mod-i, Platform — non-blocking.** Its process primitives and linked launcher files can move without changing their behavior. The expanded linked builds cover the important path consumers (F:1842, F:1932).

7. **mod-ii, Store — non-blocking.** This moves today’s file primitives; it does not introduce `DesktopStore`. The distinction is explicit and independent of Part B (F:1843, F:1946).

8. **mod-iii, Settings — non-blocking.** The composed configuration can remain behind named exceptions while its readers move. Parser and command cuts must retain today’s answers, rather than introduce the proposed import semantics (F:1844, F:1876).

9. **mod-iv, Service — non-blocking.** Moving returned record views down beside their client is a behavior-preserving ownership cut. Their remaining upward vocabulary references belong in mod-0’s exact census (F:1824, F:1845).

10. **mod-v, Install — non-blocking.** Installation, tool and swap code can move with unchanged paths at runtime. The launcher’s linked compilation is correctly included (F:1846, F:1933).

11. **mod-vi, Instance — non-blocking.** This moves the existing pipe mechanism while domain verbs remain with their domains. Its client and pipe census are appropriate proof (F:1847).

12. **mod-vii, Events — non-blocking.** Splitting the attention vocabulary from its policy avoids moving that policy beneath its dependencies. Journal implementation remains Part B’s (F:1828, F:1848).

13. **mod-viii, Knowledge — non-blocking.** The existing connector wiring, room facts and trace reads can move unchanged. Upward dependencies remain explicit cut work, not permission to adopt Part B’s admission policy (F:1849).

14. **mod-ix, Agents — non-blocking.** Keeping the account rule, roster and continuation vocabulary together repairs the previously misplaced dependency. Its behavior remains the LIMITSWITCH implementations’ behavior (F:1821, F:1850).

15. **mod-x, Workplace — non-blocking.** Moving wiring sizes beside the space readers fixes that concrete placement. Git and removal behavior must remain behind the existing suites, without importing Part B’s store families (F:1819, F:1851).

16. **mod-xi, Roles — non-blocking.** Only the driver’s existing role files move. The shell’s act vocabulary and its authority-bearing relocation remain explicitly outside this row (F:1852).

17. **mod-xii, Participants — non-blocking.** `WorkMarks` and the binding-file reader provide usable lower seams without the proposed catalogue or database. Keeping the `LoopContext` executors in Loop removes the previous absolute-rule violation (F:1853).

18. **mod-xiii, Context — non-blocking.** Existing briefs, transcripts and instruction composition can move independently. SWITCHCTX1’s carry implementation is still its own prerequisite, rather than something this move silently builds (F:1854).

19. **mod-xiv, Course — blocking boundary omission.** F:1813 moves `ChatPendings` down but leaves `ChatPending` in Loop, although `ChatPendings` stores and returns it (`Chat/ChatPending.cs:209`, `:218`); `ChatPending.Next` also names `ConversationWalk` from `ChatRunner.cs:39`. Move the whole pending family and `ConversationWalk` into Course, and place the existing `ConfiguredAccess` data/read seam below Course: `ChatContext.cs:80` otherwise points up to Help. These are internal placement cuts, needing no Part B protocol.

20. **mod-xv, Work orders — non-blocking.** Its planner, groups and attention policy retain their executors in Loop. Moving the service record views separately makes this row’s ownership coherent (F:1856).

21. **mod-xvi, Review — non-blocking.** The pure opinion judgments and delivery values can leave Loop while its context-built executors stay. The revised split respects today’s absolute Loop rule (F:1857).

22. **mod-xvii, Plugins — non-blocking.** The folder move need not change hooks or their delivery contract. Pinned template resource names and existing plugin proofs are necessary and named (F:1858, F:1936).

23. **mod-xviii, Workflows — non-blocking.** Moving the node runtime with workflows is organizational. The workflow suites and family rehearsal are appropriate proof (F:1859).

24. **mod-xix, Setup — non-blocking.** Workspace setup remains separate from installation and update. Its existing plan and press behavior needs no Part B store (F:1860).

25. **mod-xx, Help — non-blocking after the seam correction in item 19.** Help can retain its judges while lower readers use the existing access data/read API. This must preserve grants exactly, rather than migrate or reinterpret them (F:1861; `Driver/Help/ConfiguredAccess.cs:26`, `:221`).

26. **mod-xxi, Loop — blocked by items 4 and 19.** The rank ordering is acyclic by construction, but that does not establish that the specified concrete placements are acyclic. Its proof must show zero remaining upward exceptions and zero incoming Loop edges after the named cuts, while retaining all ten context-built executors (F:1862, F:1913).

### 2. The line

1. **Non-blocking: no further row needs moving to Part B.** The recorded pick, unified reset, catalogue census, store imports and authority relocation are now excluded explicitly (`docs/decisions/D212.md:15`; F:39). The corrections above concern preserving existing behavior and completing internal seams; c must not use the naming repair to expand partner actions.

### 3. Part B: additions to the next agenda

1. **Blocking for Part B: independently old MCP writers.** The version matrix must include an older connector writing the migrated database directly, not just desktop/service HTTP pairings: `Mcp/Program.cs:104` composes its own stores, and `Core/Composition/ServiceFactory.cs:410` constructs its own helper desk. Today that desk checks only pending words before keeping another (`HelperDesk.cs:180`, `:189`), so a newer HTTP service’s settlement check cannot protect that admission path.

2. **Blocking for Part B: attenuation across crashes and live processes.** Specify the commit/recovery order between a store narrowing and its backup-file narrowing, including a crash between them (F:1648). Also prove that every live appointment is lowered across two shells: today the lock and appointment list are instance-local (`Modules/Platform/StandingConfirmation.cs:378`, `:390`), so “one mutation step” alone does not establish that property.

3. **Blocking for Part B: occurrence keys beyond state transitions.** A conversation can answer repeatedly without changing session state; today those answers are distinguished by `ChatQueue.LastTurnEnded` (`Driver/Quests/Domain/Attention.cs:333`, `:339`) and renew the notice (`Notices.cs:146`). F:972’s state-transition counter therefore needs a separate durable correlation rule for turn answers and other non-state occurrences, with repeated-answer and reconciliation proofs.

### 4. Wrong claims about existing code

1. **Blocking: a-i’s exhaustive walk description is false.** The “and nowhere else” at F:1202 omits existing `ResumeAsync` walks at `HarnessRoster.cs:984` and `:1065`. Correct it as §1.1 specifies; the broader promise to preserve every walk and counted start is right.

**Part A verdict: accept after the named changes above, checkable without another design read.** The blocking changes are a-i’s exhaustive walk description, mod-0’s semantic census and fixtures, and the pending/access seams before the final Loop move; b and c need the named preservation proofs.

**Part B verdict: §16.3 is the right agenda, with the three additions in §3.** It remains proposed; the new protocols have requirements and counterexamples to prove, not implementation evidence yet.
