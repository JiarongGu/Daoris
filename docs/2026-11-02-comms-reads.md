# COMMS1 — the reads of the design

Status: reads of `docs/2026-11-02-comms-design.md` (D225), kept as they came; the steward's settlement is the design's §13.
Codex's read stopped part-way when the workspace's spend cap was reached and is owed; Opus's read ran in its place.

## Opus's read of the design

Status: I read revision `82a941a137ac5ae549a864cb12649b9f04fe8229` (main, D225's acceptance). None of the code files the design
names changed between its evidence revision `16cc8107b` and this one. Read only, nothing written. Paths use the design's prefixes:
`Svc/` `Mcp/` `Drv/` `Mod/` `Web/`, plus `Http/` = `src/Daoris.Service/Daoris.Service.Http/`.

### 1. Blocking findings

**B1. Row b: the "answered park" disagreement is in the driver too, and the row only fixes the desks.** Two driver pre-checks never
call the door for a park: the opinion deliverer waits on any park of `view.Working` (`Drv/Loop/OpinionDeliverer.cs:174-179`); the
partner look returns on Running or Parked of the *named* asker, not the line's head (`Drv/Loop/DriverPartners.cs:471-472`). So with
only `PartnerDesk`/`OpinionDesk` changed, an answered park still takes no piece and no findings, and the proof (a table test
"through each desk") still passes. Under the new door, these pre-checks should read the head and whether its park was answered, as
the helper look already does (`Drv/Loop/DriverHelpers.cs:766`). The proof needs to go through the looks (`OpinionDeliveryTests`, the
partner look), not only the desks.

**B2. Row b: an opinion across a hand-over can no longer be answered, or is never handed again.** Only the session named in
`handed_to` may call `opinion_answer` (`Svc/Review/OpinionDesk.cs:379-387`), written only by `HandAsync` (`:280-282`). A keyed open
moves a waiting findings word (`By`) to the new part (`Drv/Course/Chat/MovedWords.cs:83-94`) but leaves `handed_to` on the old
record: the new part takes the findings and its `opinion_answer` is refused; the deliverer then reads answers from the record that
left (`OpinionDeliverer.cs:127-140`). CHATFLOW1e deliberately hands the findings a second time, once, to the newest record when the
first ended before answering (`OpinionDesk.cs:346-356`, `OpinionDeliverer.cs:101-112`); under one key `opinion:<id>` the settled read
(`Svc/Sessions/SaidKeys.cs:68-73`) answers that second hand "settled", and nothing reaches the part that has to answer. Needed:
`handed_to` follows the word when an open moves it, or `opinion_answer` is judged over the handed record's line, said as an
authority change; the second hand its own key (or the door exempts it); proof: a hand-over after the keep and before the take, for
every case.

**B3. Row b: retrying a lost answer can deliver it twice after the 7-day settlement window, or after a rollback.** The settlement
answers a retry for 7 days only; "the attempt has no horizon" (`SaidKeys.cs:20-24`). Today the no-horizon guards are the driver's
own files: the helper state's `Attempted`/`Handed` (`DriverHelpers.cs:776-792`, uncertain going to the person) and the partner
receipt's `Attempted` (`DriverPartners.cs:459-462`). `SolicitedHand` should retry only while its attempt is inside
`SaidKeys.Horizon` and the line reads whole; past that it settles uncertain for the person. It should also keep writing those old
fields (§4.2 and §8 only say they are "still read"); otherwise a driver rolled back before confirmation
(`Drv/Store/DesktopStore.cs:51-53`) hands again, and so does a host that drops its mark in the middle of a hand.

**B4. Row b: "already waiting" has to finish the dispatch that `HandedBefore` does today.** When `HandedBefore` finds the word
already waiting it dispatches it idempotently on the record that holds it before marking the ask handed (`DriverHelpers.cs:773-786`,
HELPERWAIT1b2); nothing else tells a chat asker between turns. The door's "already waiting" answer must name the holder and the word.
Today the helper desk refuses a word already waiting (`NotHanded`, `Svc/Sessions/HelperDesk.cs:221-224`) while the partner desk
answers it (`PartnerDesk.cs:173-180`); the one door needs one answer.

**B5. Row b: once new reviewers write `serves`, they are read as helpers.** The page reads `serves` as helper: `isHelper`
(`Web/work/identity.ts:82`), "helps …" on the rail (`Web/work/rail/SessionRow.tsx:217`), "helping …" in the head
(`Web/work/head/headFacts.ts:140`). The helper desk's hand guard accepts any record with `serves` (`HelperDesk.cs:144`), and so does
the first guard in `HelperBox.Answer` (`HelperBox.cs:101`). Not a widening in practice (the hand routes are Driver-class,
`Http/PersonDoors.cs:285-289`; both twins of `ParticipantWork.Of` read the opinion mark first, `Svc/Sessions/ParticipantWork.cs:59-60`,
`Drv/Participants/Domain/ParticipantWork.cs:76-77`), but row b is service and driver only: either it carries the web lane and those
guards check the opinion mark before `serves`, or `serves` moves to a row with the web lane.

**B6. Row c: the "one origin rule" isn't one if only the service changes.** These still derive the ask from the quest's sender: the
start's ask words and go-aheads (`Drv/WorkOrders/Domain/AskWords.cs:68-73`); the helper-wait check for an open go-ahead
(`DriverHelpers.cs:193`, `:201-207`); the workflow go-ahead node (`Drv/Workflows/Nodes/NodeRuntime.cs:618`); `KeepOnAskAsync`
(`Svc/Sessions/SessionLedger.cs:1855`); the supervisor's open-go-ahead guard (`SessionLedger.cs:1378-1391`). With `SessionGoAheads`
alone moved, a session on a sibling-published quest asks a go-ahead whose answer is never handed beneath its next start, and a turn
that asked both a go-ahead and a helper ends `ended.awaits-helper` instead of parking. Row c should be service and driver and name
these readers; proof: the answer handed at the next start, the helper wait parking on it, the supervisor refusing while it is open.
Authority, unsaid: a go-ahead the person already approved now also covers the same act from a newly covered session, possibly in
another repository (`Svc/Sessions/SessionGoAheads.cs:113-118`, `:146-151`); today that session is refused NoAsk.

**B7. Row e: the stale-press refusal already exists, outside the service step.** A press already names its question and is refused
`question-changed` in the modules before the say (`Mod/Bridge/Routes/ConversationRoutes.cs:118-132`; `Web/work/WorkFrame.tsx:1048`,
`:1094-1109`). A word held by `SessionWords` keeps `choice` but no question id (`Mod/Loop/SessionWords.cs:194-196`, `:1143-1146`), so a
press held across a move is never checked again. The say door refuses a question from anyone but the supervisor
(`SessionLedger.cs:1060-1066`), and the service never receives the person's press (`Drv/Service/ServiceClient.cs:397-406`). The row
needs: that refusal lifted for a person's press; the press checked inside the step against the head's question, as
`SupervisedParkAsync` does (`SessionLedger.cs:1362`); the question id on the held word and its file; a host mark (an older host refuses
the whole press as `from`); the modules' pre-check kept for older hosts. Proof: a press held across a hand-over refused when forwarded.

**B8. Row g: the admission has no line key, changes the reset rule, and has no first-start seeding.** Nothing in the desktop store
identifies a line (moves are keyed by `record_left`, `DesktopStore.cs:150-152`); a D79 resume opens a record with no `from`
(`docs/decisions/D188.md:369-372`), yet `Lines.WalkAsync` reaches it through the quest hop (`Svc/Sessions/Lines.cs:50-55`): say which
walk keys the admission. Today Daoris's own word (`from: daoris`) resets the count (`Drv/Course/Domain/AnswersAlone.cs:14`,
`:100-103`; `D188.md:352-353`); §5.3 resets only on the person's words or an act the person authorised, which changes D188 and is
missing from §8. The steward's choice 2 counts a line with no events as zero (`Drv/Course/GoOn/AnswersAloneHold.cs:16-17`;
`D188.md:354-355`); §5.3 makes it unknown and holds: seed every line from the legacy count on first read, else the first solicited
answer on every line after the upgrade holds. `supervisor-bound` (`AnswersAloneHold.cs:85-90`) must read the same count. Say whether
the store step raises the writer stamp (a new table no older build reads leaves it at 3, as at `DesktopStore.cs:500-511`), and what
the new build does with continuations a rolled-back build admitted.

**B9. Rows d and f break the driver's structure gate as written.** A driver file may name types only from its own subdomain and the
ones listed before it (`Drv.Tests/StructureTests.cs:5-11`; `Drv.Tests/fixtures/structure.json:9-17`); work orders comes after Events,
Participants and Context, so `Observation`, `HelperWaits` and `ContextBrief` cannot read `Drv/WorkOrders/Domain/Exchanges.cs` without a
census edge. Say which fix applies: Loop passes the values down, or the `Exchange` value is declared low.

**B10. Row f: the hand-over brief already lists open asks.** `BriefLine.Open` (`Drv/Context/Domain/ContextBrief.cs:59`) is drawn as
"Asked and not answered yet: …" (`:569`), filled from the helper box (`Drv/Context/Events/ContextBriefs.cs:93`, `:256-274`), held by
`ContextBriefTests.cs:224-234`. Row f should widen `Open` to every case (owner, where the answer goes, delivery) rather than add a
piece, else helper asks are told twice. The filter treats an answered-but-not-handed helper ask as settled (`ContextBriefs.cs:266`)
while `HelperWaits.Open` treats it as open: the new read should settle which.

**B11. Row a: on events, a partner's word cannot be told from a helper's by key.** `SessionEvent` carries no `Key`
(`Drv/Events/Domain/SessionEvents.cs:211-250`), and a partner's event is written with `Helper = partner` (`DriverPartners.cs:508-511`),
so "a `partner:` key → a partner" works only in the service twin; the driver and page twins read the named record's piece/work. The
fixture must say which shape each twin reads: `SaidWord` has no origin; the "Channel with a target origin" row exists only on events
(`Drv/Course/Domain/ChatMessage.cs:219-232`).

### 2. Non-blocking findings

1. §4.2's door table dropped "a teammate's" from the refused row; `Lines.WalkAsync` returns a teammate's record as its own head
   (`Lines.cs:37-44`) and the desk refuses it (`HelperDesk.cs:200`). Keep the row.
2. CHATFLOW1e hands a chat run's findings only to the working line's record in its tree (`OpinionDesk.cs:312-316`), named by the
   driver's `to`; the door's head can be in another tree. Say whether the guard and `to` stay.
3. The door walks `Lines.WalkAsync` with no chain hop (`SessionLedger.cs:1102-1104`); the page's head is `HeadOfLineAsync`, chain hops
   included (`Http/Program.cs:2084`; `Lines.cs:65-71`). The exchange read's "where its answer will go" must use the door's walk.
4. A hand-over ask has its own id (`Svc/Sessions/HandOverAskBox.cs:80-83`): use `hand-over:<id>`. The question file is deleted when the
   next run opens (`Drv/WorkOrders/Questions/QuestionFiles.cs:105-108`): name where an answered question's outcome is read from.
5. D188 point 7's *Answered* receipt (`D188.md:60-61`) becomes *taken, turn ended* (§7): list it in §8's D188 row.
6. The `answers: 1` mark joins `/api/protocol`'s marks (`Http/Program.cs:481`; read in `Drv/Service/SessionOpenDoors.cs:81-105`):
   spell out what it promises so the driver knows which of its own paths stand aside.
7. Production partner starts are refused for everyone (`Drv/Participants/Domain/PartnerMeasurements.cs:44`, `Rows = []`); row b's
   partner proofs run on stand-ins only, and row n can show none.
8. Row k: a lane quest's take can be a teammate's (`docs/decisions/D167.md:406-410`; `Svc/Asks/Channel.cs:1068`), so refuse a part
   that lives elsewhere; a newer service with an older driver keeps an addressed row nothing delivers; rows on a non-ask origin kept
   in `channel` would travel by the channel sync (`D167.md:260-264`); a person's post is also kept on the ask (`Channel.cs:754-756`)
   and handed to every later session there (`Drv/WorkOrders/Domain/AskWords.cs:216-217`): say what happens to one addressed to a part.
9. Row l: the per-caller bound is keyed by process (`Mod/Platform/StewardDesk.cs:102-103`), so persisted requests carry dead callers.
10. Row i: *At work* composes from state slices only (`Mod/Loop/AtWorkRead.cs:52-56`); the helper, piece, question and hand-over files
    are not slices, so they need inputs with their health.
11. Citations: the partner's helper mark is at `PartnerDesk.cs:182`; the measurement rows at `PartnerMeasurements.cs:44`.

### 3. The §2 settlements against the code

Kinds, delivery apart from outcome, parts hearing each other, `partner_ask` out, the steward desk and three waves hold; one door
holds as the direction with B1 to B4; no new question record holds, its premise corrected by B7; the durable count holds as a need,
not buildable as written (B8); `serves` holds for the service and driver, not the page or the helper guards (B5).

**Verdict:** accept once the rows carry: b, the driver pre-checks on the head and its answered park, `handed_to` following a moved
word, CHATFLOW1e's second hand its own key, retries bounded by the window with the old fields still written, "already waiting"
dispatching, a web lane and opinion-before-`serves` guards; c, service and driver with every ask reader named and the go-ahead join
said as an authority change; e, the check moved into the say step, the supervisor-only refusal lifted, the question id on held
words, a host mark; g, a line key, D188's reset and no-events rules changed openly, seeding from the legacy count, the supervisor's
bound check, the writer stamp stated; d and f, the value placed where the structure gate allows and `BriefLine.Open` widened; a, the
event shape for partners in the driver and page twins.
