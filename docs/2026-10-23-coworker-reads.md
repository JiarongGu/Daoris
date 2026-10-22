# COWORKUX1: the two reads of round 1 (D191)

Codex's read and Fable's foundation read of `docs/2026-10-23-coworker-design.md` and D203 at `2bdc8f5eb`, verbatim but for their headings set one level down; the parent ran both from outside the install. Round 2 answers each finding in the design's reads table.

## Codex

Read at `2bdc8f5eb`. Nothing changed; no builds, tests, Daoris processes, agent sessions or sign-ins ran. I did not read `local/` or seek another reader’s review.

Below, **design** means `docs/2026-10-23-coworker-design.md`. Code prefixes follow its abbreviations: `Core`, `Mcp`, `Driver`, and `Web` (`src/Daoris.Web/src`).

1. **Blocking — HELPERWAIT1 misses an ask the driver has not judged yet.** A lead can call `helper_ask` and immediately end its turn while the ask has no state file. Restricting “open” to `waiting`, `approved`, and `started` leaves that ordinary case parked.

   **Where:** design §5, lines 222–225; `Core/Sessions/HelperBox.cs:76` writes the ask alone; `Driver/Conversations/Helpers/HelperFiles.cs:48` reads a missing state as null; `Driver/Conversations/Domain/HelperJudge.cs:61` accepts such an ask for judging.

   **The smallest change:** Include an existing, unjudged ask in the open-ask predicate. Add a Process case that forces the lead to exit before the driver’s next helper look.

2. **Blocking — `Parks` does not already read questions to the person.** Its `awaitsBefore` and `awaitsAfter` describe a quest waiting on another repository’s quest. Moreover, a `question_ask` is bound to its park *after* the conclusion, so the proposed pre-conclusion test cannot simply reuse an already-bound question.

   **Where:** design §5, lines 223–224; `Driver/Sessions/Domain/Observation.cs:199`; `Driver/Sessions/Questions/QuestionFiles.cs:99`, `:114`; `Driver/Loop/Driver.cs:2424`. Go-aheads use another store, through `Core/Sessions/SessionGoAheads.cs:23`.

   **The smallest change:** Specify a new conclusion input that reads a current-turn question and outstanding go-aheads before settling either. Define its freshness rule, distinguish unread from absent, and test it through the real conclusion path.

3. **Blocking — `Reopens: false` does not keep a person’s park blocked.** Once the helper’s word is in `said`, the planner regards the parked record as having words waiting and may resume it without the person answering. `Reopens` describes how words were supplied; it is not a continuation prohibition.

   **Where:** design §5, lines 241–244; `Driver/Quests/Domain/Planner.cs:350`, `:1442`, `:1456`. `WordsWaiting` includes every waiting word, and `WordsOn` has no requirement for a person’s answer.

   **The smallest change:** Add an explicit planner guard: helper-only words on a genuine person-question park remain held until the person answers. Test the first and second helper answers, where the answers-alone bound would otherwise permit continuation.

4. **Blocking — the proposed refusal transition is rejected by the ledger.** An ended record may move only through the guarded go-on path to `working`; `completed → awaiting-person` currently returns “a finished session does not move.” Row 2 also omits the service from its build scope.

   **Where:** design §5, lines 237–240; §12, lines 424–426; D203 lines 35–37; `Core/Sessions/SessionLedger.cs:1388`, `:1726`.

   **The smallest change:** Include a narrowly guarded service transition for this refusal, its door and tests, and its state-flow twin. Specify what happens to waiting words: the ordinary move into `awaiting-person` clears them at `SessionLedger.cs:1410`.

5. **Blocking — completing the lead releases its tree ownership.** Holding its quest in the planner does not preserve the ledger’s tree exclusion or the git lock. Another part can select that tree while the helper reads it, and cleanup no longer sees the completed lead as active.

   **Where:** design §5, lines 230–233; D203 lines 33–34; `Core/Sessions/Sessions.cs:309`, `:1120`; `Driver/Loop/Driver.cs:2445`, `:2539`; `Driver/Quests/Domain/Planner.cs:1595`; `Driver/Trees/SessionTrees/SessionLocks.cs:103`.

   **The smallest change:** Define a durable tree-holding predicate for the helper wait, distinct from running-slot accounting, and apply it to starts, alongside-part selection, lock release and cleanup. Prove cap-one progress while a second writer and cleanup are refused.

6. **Blocking — adding `serves` does not enforce the full reviewer or helper role.** It blocks two quest tools, while other acting tools remain callable through the server. For example, `permission_propose` accepts the connector’s session id without checking its role and writes a machine/workspace/repository proposal. The claim that `helper_ask` already refuses Ask Daoris is also unsupported: its record judge checks `serves`, `opinion`, and `ask`, not the help repository.

   **Where:** design §0.2, lines 28–31; §2.1, lines 82–89; §4.1, lines 172–185; D203 lines 39–40; `Mcp/RoleFilter.cs:58`, `:68`; `Mcp/RuleProposalTools.cs:39`; `Core/Help/RuleProposals.cs:110`; `Core/Sessions/HelperBox.cs:138`; `Driver/Conversations/Domain/HelperJudge.cs:63`.

   **The smallest change:** Give helpers and reviewers server-enforced allow-lists with a tool census, retaining their own answer tools. Explicitly refuse Ask Daoris at both helper admission judges. Handed permission rules alone do not supply this boundary.

7. **Blocking — the once-only helper hand is left as a test expectation without a mechanism.** The design correctly identifies the crash window, but neither its record changes nor HELPERWAIT1 closes it. After the word leaves `said`, a restart before `Handed` is written can supply the same answer again, violating D188’s once-per-ask rule.

   **Where:** design §1, line 70; §4.4, lines 203–204; §11, line 401; `Core/Sessions/HelperDesk.cs:161`; `Driver/Loop/DriverHelpers.cs:418`, `:431`.

   **The smallest change:** Specify durable helper delivery identity and receipt handling before the hand, including uncertain attempts across restart. Reuse the partner delivery pattern where appropriate, and include the resulting uncertainty in the person-wait contract.

8. **Blocking — a helper answer can reopen a left conversation after a relay.** The team follows the line for display, but helper delivery still resolves the original `serves` record directly. Unlike the partner hand, it does not follow `from` successors.

   **Where:** design §2.2, lines 94–97; §5, lines 234–236; D203 lines 20–22; `Core/Sessions/HelperDesk.cs:140`; compare `Core/Sessions/PartnerDesk.cs:114`. D189 point 6 forbids resuming a left conversation.

   **The smallest change:** Resolve helper delivery to the eligible live successor along `from`, or retain the answer visibly for the person where none is eligible. Preserve the original commissioner and one delivery identity; test a hand-over during a helper’s run.

9. **Blocking — the reviewer follow-up sends a recheck to the wrong recipient.** The table calls it D155’s recheck but sends its findings to the lead. D155 deliberately sends recheck findings to the person, preventing another automatic exchange.

   **Where:** design §7, line 324; D203 lines 49–50; `docs/decisions/D155.md:48`; `Core/Review/OpinionDesk.cs:225`, `:237`.

   **The smallest change:** Say “the person and the opinion gate” for recheck findings. Keep automatic delivery to the working session limited to the first pass.

10. **Blocking — the existing helper read cannot supply the promised whole-line cards and receipts.** It filters asks by exactly one asker id and returns neither answer text nor delivery identity. After attending a successor—or the member itself—the promised historical questions, answers and delivery facts are unavailable from the stated read. A person’s pending ask also has no conversation event at which to place its card.

    **Where:** design §2.2, lines 94–96; §6.3, lines 283–289; §6.4, lines 299–306; §10, lines 389–390; `Daoris.Desktop.Modules/Bridge/Routes/SessionsRoutes.cs:194`; `Driver/Conversations/Domain/HelperNotes.cs:10`; `Driver/Conversations/Helpers/HelperFiles.cs:185`. The asked note carries the helper session id, not the ask id, and is produced only once a helper starts.

    **The smallest change:** Define the extended existing read as a whole-line exchange projection, retaining original asker, ask id/time, answer source/text and delivery word/receipt identity. Place cards from the box’s `at` when no commission event exists. Specify how a member resolves this projection.

11. **Blocking — the answers-alone reset has no specified writer on the lead’s line.** A person’s follow-up writes a new helper ask, but that file is not among the events `AnswersAlone.Resets` reads. Recording the question only in the new helper’s conversation would not reset the lead’s count either.

    **Where:** design §7, lines 328–329; §11, line 404; D203 line 50; `Driver/Conversations/Domain/AnswersAlone.cs:89`; existing person doors at `Driver/Conversations/Helpers/SessionsHelperCommand.cs:91` and `Daoris.Desktop.Modules/Bridge/Routes/SessionsRoutes.cs:175`.

    **The smallest change:** Name a durable person-action event on the commissioned lead’s line and teach `Resets` that event. Write it from both doors without manufacturing a taken prompt or relabelling the helper’s answer as the person’s words.

12. **Blocking — the rows and installed proof depend on later rows.** Row 3 takes §6.4 whole, including *Ask again* and the follow-up strip, but row 5 introduces that door and `after`. The shared installed proof also asks again and hands the answer, although row 1’s stated proof dependency is only rows 1 and 2.

    **Where:** design §6.4, lines 305–306; §11, lines 410–414; §12, lines 420–430.

    **The smallest change:** Limit row 3 to navigation, confinement and existing delivery facts; add follow-up controls and strip in row 5. Split the installed proof by landing row. Also state how partner follow-ups are linked: §4.3 currently adds `after` only to helper asks.

13. **Blocking — CHANPART1 needs a new acknowledgment contract.** Today one acknowledgment is keyed by ask, entry and message, specifically preventing two live parts from taking the same message. Changing delivery to every live part cannot be achieved by merely removing the same-entry exclusion.

    **Where:** design §3.2, lines 133–135; §11, line 407; §12, lines 434–435; `Core/Asks/Channel.cs:320`, `:390`, `:1048`; `Driver/Loop/ChannelReach.cs:66`. The existing protocol is recorded in D188’s COWORK1d note.

    **The smallest change:** Specify per-part acknowledgment identity, reads, claims, receipts, expiry and older-reader behavior, and amend the relevant D188 note as well as D167. Preserve ten posts and three urgent posts **per part**, and the rule that unsolicited posts create no turn. Alternatively, require a separate foundation design before this row builds.

14. **Blocking — several reviewers are named but not sufficiently specified to build.** No unchanged setting or earlier row chooses N, and the current gate selects one newest first pass. The design supplies neither the replacement selection/aggregation contract nor its proof; “written with their rows” postpones the design’s safety decisions.

    **Where:** design §3.2, line 125; §9, line 384; §11, line 408; §12, lines 436–439; `Driver/Review/Domain/OpinionGate.cs:384`; `Core/Review/OpinionDesk.cs:398`.

    **The smallest change:** Define who declares the reviewer set, its cap, per-reviewer recheck allowance, delivery batching and aggregate gate settlement. Test that one disputed opinion survives later opinions and that separate deliveries cannot bypass the third-answer hold. Defer rows 9–10 behind that design if these choices are intentionally later.

15. **Not blocking — the truncation correction is itself incorrect.** The rail already nests before truncating *Ended*, in both state and repository arrangements. Truncation can hide an ended parent with its children, but it does not first remove a child whose drawn lead is in *You*.

    **Where:** design §0.2, lines 32–35; §1, line 61; `Web/work/rail/SessionList.tsx:187`, `:238`, `:263`; `Web/work/groups.ts:198`.

    **The smallest change:** Remove the claimed existing ordering defect. Keep regression coverage for the ordering and treat the new child-fold behavior as the actual change.

16. **Not blocking — reused helper chrome promises automatic return for person-only answers.** The existing answered-state sentence says the answer is the session’s next turn; the ask explanation and success message make the same promise. Reusing them for `to: person` gives the wrong destination in both languages.

    **Where:** design §7, line 322; §8.3, lines 365–375; `Web/locales/en/work.helper.json:3`, `:8`, `:21`, and the corresponding zh keys; `Web/work/waits/HelperAsks.tsx:95`.

    **The smallest change:** Add destination-aware explanation, success and delivery sentences in both catalogues, and update the helper tool’s fixed response where necessary.

17. **Not blocking — “nobody but the person stops a member” contradicts its own bounds.** Helpers and partners have timed passes, and D188/D200 retain Daoris’s automatic ends for such passes.

    **Where:** design §7, line 333 versus §2.1, lines 82–84; `docs/decisions/D188.md:22`; `docs/decisions/D200.md`, point 7.

    **The smallest change:** Say that no agent or message level stops a member; the person’s stop and Daoris’s declared pass limits still do.

The basic shape is sound: one record per bounded commission, a role derived from its authority fields, and an unstored team projection. The structures are useful compositions of serving, continuation and channel relationships. Authority and delivery must nevertheless follow the explicit continuation lineage; membership in a displayed entry cannot authorize another writer, resurrect an old conversation or settle acceptance.

**Claims checked:** 64 distinct assertions about today’s code, counting repeated assertions once: **61 hold; 3 do not**—the `Parks` person-question claim (**2**), Ask Daoris’s helper refusal (**6**), and truncation-before-nesting (**15**). The other findings concern the proposed behavior and missing build contracts. I could not independently verify the install anecdote, actual window appearance, external terminology survey or real-provider measurements from this checkout.

**Counts:** **17 findings: 14 blocking, 3 not blocking.**

**Verdict: revise and read again.** The model is viable, but the turn-end, tree ownership, confinement and delivery contracts need correction before these rows can be built safely as written.

## Fable

## COWORKUX1 round 1: Fable's foundation read (D191), 2026-10-23

Read: `docs/2026-10-23-coworker-design.md` and `docs/decisions/D203.md` in the locked worktree `coworkux1-reads`, the brief and the
three proposals whole, D51, D155, D163, D167, D188, D191, D199, D200, `person-waits.md`, `session-record-fields.md`, the glossary.
Every fact below was checked in the main checkout (`fe4809b75` with the working tree's test changes; line numbers are that tree's).
Nothing was run, no agent started, no install home touched. Findings are most severe first; "blocking" means the design cannot be
built as written without answering it, "not blocking" means a correction or a choice the steward can take in the same round.

### Blocking

#### F1. `completed → awaiting-person` is not a move the ledger makes (§5 point 5; D203 point 5)

**What is wrong.** The service's ledger allows no move out of `Completed`: `SessionLedger.Allowed`
(`src/Daoris.Service/Daoris.Service.Core/Sessions/SessionLedger.cs:1726-1738`) lists successors for `Queued`, `Starting`, `Working`
and `AwaitingPerson`, and `_ => []` for every ended state. So "one ledger move `completed → awaiting-person` at the refusal" is a new
transition in the service (D46's rules), and row 2's lanes (`driver, modules, web`, §12 row 2) do not include the service. Two
consequences the design does not name: the move's `clearSaid: target == AwaitingPerson` (`:1408-1410`) clears any word waiting on
the record at that moment (a person-handed follow-up kept on the held lead, §7); and on a service from before the move the driver's
state door is refused, so the lead stays `completed` and held with nothing saying the person is needed.

**Smallest change.** Either (a) add `Completed → AwaitingPerson` to `Allowed`, taken only through the driver's state door for a driven
record of this machine's whose quest is still taken, with `clearSaid` false for this move, the service lane added to row 2, a dated note
on D46 and the older-service fallback said (the move refused → the look's line says so and the row falls back to a `NeedsAPerson` row
read from the box's refusal); or (b) keep the ledger as it is and reopen the held lead with Daoris's own keyed word at the refusal
(COWORK1b's `from: daoris`, a key kind `helper-unanswered/<ask>`, D188 point 2's "a fact that makes the work in flight wrong"), so the
agent itself asks again or parks with a real question. (b) changes no stored shape and no transition and spends one turn; (a) is what
D203 point 5 says. Pick one and write it; as written, neither exists.

#### F2. A helper's word kept on a parked lead is gone on at the next look, not held for the person (§5 point 6; D203 point 5)

**What is wrong.** Point 6 keeps the helper's answer on an `awaiting-person` record "with `Reopens: false`" so it waits for the
person's answer. No driver reader honours `Reopens`: the only mention is the record's definition (`Quests/Domain/Planner.cs:609`).
The planner reads `PriorSession.WordsWaiting => !Running && (Said is { } said ? said.Count > 0 : AnsweredPark)` (`Planner.cs:350`),
`WordsOn(quest)` returns the park whenever it has any word on `said` (`Planner.cs:1442-1449`), and the groups reader draws it
*going on* (`Trees/SessionGroups.cs:1107-1116`). The store's `KeepSaidAsync` appends to `said` whatever the state
(`Sessions.cs:1468-1496`). So the hand would make the park go on with the helper's claim alone, skipping the question it asked the
person, which is the leak Codex's X4 named ("keeping and continuation authority are separate"), and the answers-alone hold would be
the only thing between.

**Smallest change.** Do not keep the word on a park. `HandAsync` takes an `AwaitingPerson` asker only once it is answered
(`asker.Answer is not null || asker.Said.Any(w => w.Persons)`, `HelperDesk.cs:148-149`), keeping the word beside the person's
answer, which is exactly the `words-and-helper` case the design wants; otherwise today's behaviour stands (the driver retries the hand
at each look, `Loop/DriverHelpers.cs:411, 421`). The card and the thread read the answer from the box (`<ask>.answer.json`), which
§6.3 already says is the body's source, and say *waits until you answer {lead}*. Nothing is lost and no park goes on alone.

#### F3. The double hand after a restart is deterministic, not a race for a test to find (§1 row 15; §13; D203's delivery)

**What is wrong.** The box records `Handed` only after the keep (`DriverHelpers.cs:418-431`). `HandAsync` dedupes only against words
still waiting on `said` (`HelperDesk.cs:161-164`) and sets no producer `Key` on the helper's word (`:166-168`), so once the lead went
on and the word left `said`, the service has no memory of it. Any refusal but `NotFound`/`Shared` is non-ok at the host
(`Daoris.Service.Http/Program.cs:1724-1731`), which the driver reads as `handed: false` and retries without writing `Handed`
(`Conversations/Helpers/HelperDoors.cs:86-91`; `DriverHelpers.cs:421`). So after a crash (or a failed `WriteState`) between the keep and
`Handed`: look N re-hands, is refused `NotHanded` while the word waits, writes nothing; the plan at the same look goes on with the word
(`Driver.cs:799` helpers, `:821` plan); look N+1 re-hands, finds no waiting word, keeps a second one, and the lead goes on twice with the
same claim, the second a go-on by answers alone. The Process case §11 row 2 names would fail against the design as written, since the
design specifies no mechanism that makes it hand once.

**Smallest change.** Helper delivery takes the partner's receipt shape, already built: claim, mark `attempted` before the door,
`taken` after, and a look that finds `attempted` with no hand in flight settles it `uncertain` for the person, never handed again by
itself (`Conversations/Domain/PartnerPieces.cs:252-300`, `PartnerDelivery`; D199's part-six note). The helper's word carries a producer
key (`helper/<ask>`), as D188 point 6 has every word. Say it in §5 and move row 15 from "taken as a proof" to a mechanism.

#### F4. The guard "a bound question to the person wins" reads nothing as written (§5 point 1; D203 point 5)

**What is wrong.** Point 1 says the open `question_ask` or `go_ahead_ask` are "the awaits `Parks` already reads".
`Observation.Parks` reads `awaitsBefore`/`awaitsAfter` (`Sessions/Domain/Observation.cs:200-207`), which are D79's question to
another repository on the quest. `question_ask` writes PARKVIEW1's `QuestionBox` under the home (`Daoris.Service.Mcp/QuestionTools.cs:7-40`)
and `go_ahead_ask` is held on the ask's go-aheads (`SessionLedger.GoOnWithGoAheadsAsync`, `:1774-1800`); `Parks` reads neither. So a lead
that asked both the person and a helper would be read `awaits-helper`, its question hidden until the helper answered, then gone on with
the helper's claim and the person's question lost.

**Smallest change.** Name the two sources as a new input to `Conclude`, filled by the look beside `backgroundCut`: the session's open
question in the `QuestionBox`, and an open go-ahead on the quest's ask naming the session. Where either is open the park stands.

#### F5. The design says both that the person's ask resets the answers-alone count and that *Hand to the lead* counts (§7; D203 point 8)

**What is wrong.** §7's first bullet: "The person's ask is their word... `AnswersAlone.Resets` counts it". `Resets` reads a `user`
event with no origin or `person`, Daoris's `from`, or an `answers.passed` note (`Conversations/Domain/AnswersAlone.cs:89-91`). A
person's helper ask writes a box file and no event on the lead; the answer handed to the lead carries `helper` on its event, which
`Answers` reads (`:94-95`). So today a person-asked helper's answer already counts as a go-on by answers alone, and the design's fact
is wrong. §7's second bullet then says the press hands it "as one solicited answer under D188 point 4", which counts. D203 point 8 says
"The person's ask is their word for D188's answers-alone count". Both cannot hold.

**Smallest change.** The person's press (*Hand to the lead*, `sessions hand`) keeps an `answers.passed`-shaped note on the lead beside the
word (`AnswersAloneNotes.Passed`, `AnswersAlone.cs:110-112`), which `Resets` already reads as their word; the claim stays the helper's
(the word's `helper`, D137). Say so in §7 and D203 point 8, and correct the fact.

### Not blocking

#### F6. Nesting already precedes *Ended*'s truncation; what can still hide a member is its lead being cut (§0.2 point 2; §1 row 6; §6.1)

`SessionList` nests first (`nest(byState...)`, `Web/src/work/rail/SessionList.tsx:238`) and cuts *Ended* afterwards over the nested rows
(`ended(...)`, `:188-190, :246`; `cutEnded`, `groups.ts:339-346`), keeping the row the attended session is under. So "nest before *Ended*
is truncated" is the case today; row 1 builds nothing there. What can separate a lead from its member is a lead in *Ended* past the
first twelve, whose whole nested row goes behind *Show N more*. The row to file is "a lead with a working, waiting or failed member is
never cut" (as `keep` does for the attended one), which §6.1's fold rule needs anyway.

#### F7. `DriverHelpers.cs:443` writes `helper` on the event for a conversation asker only (§4.2)

The `User` event with `Helper` at `:439-445` is under `if (conversation && word is not null)`. A driven asker's event with `helper` is
written by the go-on's opening (COWORK1e, D188's note). The card's placement rule holds; the citation should name both writers.

#### F8. Neither `:411` nor `HandAsync` needs to change for the held lead; only F2's parked case touches `HandAsync` (§0.2 point 3; §5 point 4)

A `completed` record is neither `Running` nor `Parked`, so `DriverHelpers.cs:411` does not skip it and `HandAsync`'s `_ => null` arm
(`HelperDesk.cs:157`) already keeps the word with `Reopens: true`. The go-on path then holds: `WordsOn` finds the lead
(`Planner.cs:1442-1449`) and `Observation.Resumed` concludes a resumed run on a taken quest (`Observation.cs:231-238`). Say "no change"
here; it makes row 2 smaller. For the hold itself, the precedent is the `Unable` mark the answers-alone hold puts on the snapshot before
the plan (`Driver.cs:736-740`; `Planner.cs:1359-1361`, `HeldForPerson` keeps `CarriedOnFrom` off a marked record), which already stops
the carry-on and the strike; a `Snapshot.HelperAsks` of its own is a second way to say the same thing.

#### F9. A restart during the helper's turn brings the lead to the person (§5 point 5; §13)

The hold is derived from the box and the record, so it survives a restart; but the orphan sweep ends the helper's record interrupted,
the look reads the `started` ask with no answer as `refused no-answer`, and point 5 parks the lead on the person with "{agent} ended
with no answer". That is correct and should be said as the restart's reading, so row 2's Process case expects it rather than a re-ask.
A driver from before row 2 reads the held lead (`completed`, quest taken, no mark) as a plain carry-on (`Planner.cs:1329-1333`) and starts
a new part while the helper works; same-install builds move together, so say "unsupported across builds" rather than guard it.

#### F10. *Hand to the lead* / 交给主导 and `sessions hand` collide with *hand off* (§6.3, §4.5, §8.3)

The glossary's *hand off* (`glossary.json:542`: "Give a landed branch to a plugin") matches `\bhand(s|ed|ing)? (it )?(on|to)\b`, which
*Hand to the lead* matches, and its zh form is 交给, which 交给主导 uses. `names:check` would read the button as the hand-off term rather
than flag it, so the person reads the plugin's word on a helper's answer. `sessions hand` sits beside `sessions hand-over` and Ask
Daoris's `hand_propose` (brief §3.1). Smallest: *Pass to the lead* / 转给主导 (Codex's verb) and `sessions pass <helper>`; a `button`
(20/8) in both.

#### F11. State the destination rule in one sentence; and say what an older driver does with `to` (§4.3, §7; D203 point 8; D163's amendment)

The person's first ask from the lead's head answers the lead (today, kept); their *Ask again* from the member's thread answers the person
(`to: person` by default). The rule that makes this one amendment of D163 point 2 is "the answer goes where the person asked from: the
lead's page to the lead, the member's thread to the person; `--to-me` and *for the lead* turn either". Write that, or the asymmetry reads
as an accident. `HelperFiles` parses with `JsonDocument` (`Conversations/Helpers/HelperFiles.cs:205`), so a driver from before `to` ignores
it and hands to the lead; say so under compatible builds.

#### F12. Several reviewers with two makers on the machine (§3.2 row 4; §12 row 9)

D155 point 4 takes another maker first and the working family only when listed and nothing else can run; D199 point 9 adds a partner's
maker to the families. On an install with Claude Code and Codex, "N opinions of different makers" of a mixed-maker candidate has no
independent maker at all. Before row 9, say what the structure means then: a second pass of the same family labelled *the same agent,
fresh*, or a plugin's declared maker, and that agreement establishes nothing either way (point 8). Not this round's to build.

#### F13. The reviewer's `serves` is sound; two readers to name (§4.1; D203 point 6)

Checked: `OpinionDesk.AskAsync` opens with `opinion: id, ownEntry: true` and no `serves` (`Review/OpinionDesk.cs:128-131`); `RoleFilter`
reads `Serves`/`Piece` alone (`Mcp/RoleFilter.cs:13-19, 108-117`), refusing the two quest tools (`:58, :77-81`); `HandAsync` reads
`Opinion` before `Serves` (`HelperDesk.cs:145-146`); the say door refuses `opinion` before `helper` (`SessionLedger.cs:1054-1066`);
`HoldsSlot` counts a record once whichever mark it carries (`Planner.cs:831-836`); `OpinionDesk.AskAsync` reads a working session's
`Opinion` before its `Serves` (`:107-111`). D155 point 6 holds, since `serves` is a link. Add to §4.1's list: `DeliverHelperAsync`'s
`AskerUnfit` arm (`DriverHelpers.cs:396`) and `HandOverAsks.cs:118`, both already refusing by `opinion`; and say that a recheck's
reviewer and *the same agent, fresh* serve the same `working`, so a team read counts each pass.

#### F14. Facts that hold, for the record

§0.2 points 1, 3, 4, 5 (`errors.json:61`, `history.json:28`, `settings.tools.json:79` in both languages; `glossary.json:635` *panel*,
`:523` *line*, `:581` *subagent*); `D167.md:218`'s "not handed to the entry"; D167's 2026-10-14 alongside amendment (`D167.md:73-78`, so
the brief's "parts never run beside each other" is the stale sentence, not the design's); `HelperJudge.PerAsker = 2` (`:52`);
`identity.ts:79`; `conversation.ts:45-80` (no `helper`); `Sessions.cs:375-380`; `SessionGroups.cs:1094, 1116` (*Resumes later* with
`Holds`); `Observation.cs:150-158`; `SessionList.tsx:92-96, 164-167`; `groups.ts:309-316`; the look's order helpers → partners → plan
(`Driver.cs:799-821`), so a hold filled by the helpers stage reaches the plan in the same look.

### Names (§8; D203 point 9)

同事 over 协作者: the three reasons hold (git hosts' collaborator; 协作关系 already titles *Who it worked with*, `work.relations.title`;
the budget). 团队 in prose only, since *team* names the people sharing a remote in three strings: right. *Co-workers* / 同事 as the
section (32/12), *lead* 主导, the roles' words kept, 多位阅读者 for the owner's panel (面板 is the region): right. *subagent* narrowed:
right. The one collision is F10.

### Cost and the rows (§10, §12; D203 point 11)

No model, no new poll, one account turn per *Ask again*: holds. Row 1 changes no stored shape and shows today's helper as a
co-worker: right first row. Row 2 must gain the service lane (F1a) or take F1b, and the receipt (F3); row 1's install proof waiting on
row 2 is right. Rows 3 to 10 build in a safe order; row 5 is the only one that touches a box's shape (`after`, `to`), read leniently by
the driver's reader (F11).

### Verdict

**Accept after the blocking findings are answered.** The shape is right: one record per co-worker with its role read from the fields it
already carries, the team as a read over `serves` and the entry, the structures as compositions of `serves`, `from` and the channel
chosen by the door pressed, the reviewer joined by `serves`, and a lead that waits on its co-worker as a held `completed` record rather
than a park or a `working` record with no process. What is wrong is in §5's mechanisms and the delivery (F1 to F5), each fixable without
moving the shape; none asks for a rethink.

## Codex, round 2

Codex's read of round 2 at `c2bddc515`, read-only, with HELPERWAIT1's unfinished branch (`worktree-agent-ae0f5a97b787af377`, cited as H) beside it. Round 3 answers it.

### 1. Round-1 findings

References below use **design** for `docs/2026-10-23-coworker-design.md`; **Core** for `src/Daoris.Service/Daoris.Service.Core`; **Driver** for `src/Daoris.Desktop/Daoris.Desktop.Driver`; and **Tests** for `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests`. **H** identifies the committed, unfinished HELPERWAIT1 tip `d054392a6`; round 2 inspected its earlier commit `8b87af2be`.

| Finding | Verdict | Where in round 2 | Why |
|---|---|---|---|
| 1 — unjudged ask omitted | taken | §5.1, lines 294–304 | `HelperWaits.Open` includes an ask with no state, as required. |
| 2 — conclusion does not read person questions | open | §5.1, lines 298–304 | Sources and freshness are specified, but unread question files still mean absent; new finding 3. |
| 3 — `Reopens: false` does not hold a park | open | §5.5, lines 329–336 | The unsafe keep is withdrawn, but the replacement cannot deliver beside the person's answer with unchanged guards; new finding 1. |
| 4 — refused ledger transition and missing service lane | taken | §5.4; §12 row 2 | The guarded transition, retained words, service lane and D46 amendment are explicit. |
| 5 — completed lead releases its tree | taken | §5.3, lines 308–318 | The required predicate reaches exclusion, locks, alongside selection and cleanup, with cap-one proof. Its unfinished implementation is acknowledged. |
| 6 — role confinement and Ask Daoris admission | taken | §2.1; §4.1; §12 row 4 | Server allow-lists, tool censuses and both admission refusals replace the unsupported claims. |
| 7 — once-only hand lacks a mechanism | wrong | §1 row 15; §5.6 | The proposed evidence check assumes durable evidence the existing event path does not guarantee; new finding 2. |
| 8 — delivery can reopen a left conversation | taken | §2.2; §5.7 | Delivery follows explicit `from` successors and otherwise retains the answer for the person; replay evidence must follow that lineage too. |
| 9 — recheck findings sent to the lead | taken | §7, line 440; §14 | Recheck findings now go to the person and gate, preserving D155. |
| 10 — insufficient exchange projection | taken | §6.3, lines 389–398 | The extended read includes whole-entry asks, bodies, identities and delivery evidence, including member-side resolution. |
| 11 — missing answers-alone reset writer | taken | §7, lines 449–455 | Both person doors write `helper.person-asked`; passing writes the existing reset note without changing authorship. |
| 12 — build and proof dependencies | taken | §4.3; §6.4; §11–12 | Follow-up controls move to row 5, installed proofs are staged, and partner pieces may remain separate rows. |
| 13 — channel acknowledgment contract | deferred | §3.5, lines 202–208; §12 row 8 | The separate design must settle per-part identity, compatibility, receipts, expiry and bounds. This is a justified foundation boundary. |
| 14 — several-reviewer gate unspecified | deferred | §3.5, lines 210–217; §12 rows 9–10 | Selection, aggregation, rechecks and delivery bounds remain explicit requirements of a separate design. |
| 15 — nesting already precedes truncation | taken | §0.2.2; §6.1 | The incorrect ordering claim is withdrawn; retaining a lead with live members is the actual change. |
| 16 — person-only answers inherit wrong destination text | taken | §8.3, lines 503–508 | Both catalogues gain destination-aware explanations, success messages and answer sentences. |
| 17 — only the person stops a member | taken | §7, lines 458–459 | Declared pass bounds remain valid; agents and message levels cannot stop another member. |
| F1 — ledger move, words, lanes and older service | open | §5.4; §12 row 2 | Choice (a) is specified, but failed transitions have neither reliable recovery nor the requested person-facing fallback; new finding 5. |
| F2 — helper-only words resume a park | open | §5.5 | Holding outside `said` fixes premature continuation; unchanged guards still prevent the promised combined delivery; new finding 1. |
| F3 — deterministic duplicate after restart | wrong | §5.6 | `HandedBefore` covers two positive examples, not every way delivery evidence can disappear; new finding 2. |
| F4 — person-question guard reads nothing | open | §5.1 | It now reads the correct stores, but an unread question can still bypass the guard; new finding 3. |
| F5 — person ask both resets and counts | taken | §7, lines 442–455 | The current behavior is corrected and the two explicit reset writers are named. |
| F6 — a truncated lead hides its children | taken | §6.1, lines 359–364 | Leads with working, waiting or failed members remain above the cut. |
| F7 — two event writers | taken | §4.2 | Both writers are acknowledged; their events still need distinct receipt meanings, new finding 7. |
| F8 — completed delivery needs no changed park guard or planner mark | taken | §0.2.3; §5.3, §5.6 | Plain completed records are neither parked nor automatic carry-ons; `Planner.CarriesOn` supports this narrower claim. |
| F9 — restart outcome and older drivers | taken | §4.3; §5.4 | Unsupported mixed builds are stated; the restart sentence needs the saved-answer qualification in new finding 8. |
| F10 — hand/pass naming collision | taken | §4.5; §8.3 | *Pass to the lead* / 转给主导 and `sessions pass` avoid the existing hand-off vocabulary. |
| F11 — destination rule and older reader | taken | §1 row 10; §4.3; §7 | Origin determines the default destination, overrides are explicit, and older drivers' behavior is disclosed. |
| F12 — reviewer independence with two makers | deferred | §3.5, lines 215–217 | The several-reviewer design must settle the mixed-maker candidate case rather than promise unavailable independence. |
| F13 — reviewer link, extra readers and pass membership | taken | §4.1 | Both additional readers and separate membership for each review pass are named. |
| F14 — existing facts that hold | answered | §0.2; §4–5; §15.2 | The retained facts need no change; their acknowledgment does not establish the new delivery guarantees. |

### 2. Findings on round 2's mechanism

1. **Blocking — unchanged guards cannot deliver the helper beside the person's answer.**

   **Where:** design `:332–334`; `Core/Sessions/HelperDesk.cs:148`; `Core/Sessions/SessionLedger.cs:732`, `:754`; H `Driver/Loop/DriverHelpers.cs:596`. The earlier branch's `Tests/Loop/HelperLookTests.cs:685` explicitly expects no hand for an already-answered park.

   Answering keeps the record `awaiting-person` and appends the person's word. Both delivery guards reject that state unconditionally. The planner therefore starts with the person's answer alone; the helper waits until a later turn ends, potentially producing another turn or another park. “Unanswered” is a new condition the cited code does not contain.

   **Smallest fix:** Permit delivery to a parked record only when its own person-answer predicate holds, in both driver and service, before continuation selects its words. Prove that an unanswered park remains held and an answered park takes both words in one prompt.

2. **Blocking — `said` plus conversation events is not a complete, durable delivery receipt.**

   **Where:** design `:79`, `:337–345`; `Driver/Sessions/Events/SessionEventLog.cs:84`; `Driver/Conversations/DrivenInbox.cs:1126`; H `Driver/Loop/DriverHelpers.cs:234`, `:560`, `:602`; `8b87af2be` `Tests/Loop/HelperLookTests.cs:794`.

   `SessionEvents.Keep` deliberately swallows failed event writes, and the inbox allows delivery to continue when its recording callback fails. Consequently, a word can leave `said` without the promised durable `user` evidence. A crash before `Handed` then permits another hand. The restart test seeds either waiting words or a successfully appended event; it does not prove the asserted ordering invariant.

   Following successors introduces another gap: H checks only the resolved recipient's words and events. Evidence left on the commissioner or an earlier successor is missed after a relay. Adding a producer key to a waiting word, as H now does, does not preserve evidence after that word leaves.

   **Smallest fix:** Define an authoritative receipt keyed once per commission, retained across consumption and recipient changes, with an interrupted attempt held as uncertain. Alternatively, make the proposed record evidence mandatory and durable before delivery can progress. Prove failed event recording and a relay between delivery and recovery, as well as the two existing positive examples.

3. **Blocking — an unread person-question file still authorizes helper-only continuation.**

   **Where:** design `:299–304`; `Driver/Sessions/Questions/QuestionFiles.cs:43`, `:61`, `:99`; H `Driver/Loop/DriverHelpers.cs:150`, `:165`.

   Round 2 distinguishes unread from absent for go-aheads only. `QuestionFiles.Read` returns null for both absence and read failure. A temporarily unreadable current question therefore allows `awaits-helper`; settling that completed conclusion can subsequently remove the question. The same failure can authorize conversion of an existing park.

   **Smallest fix:** Give the conclusion and park-conversion guards a question read that distinguishes absent, present and unread. Unread must retain the person wait and its question. Add both conclusion-path and recovery cases.

4. **Blocking — the legacy-park conversion cannot establish that the park asked the person nothing.**

   **Where:** design `:335–336`; H `Driver/Conversations/Domain/HelperWaits.cs:50`; H `Driver/Loop/DriverHelpers.cs:581`; baseline `Driver/Sessions/Domain/Observation.cs:150`; `docs/2026-10-22-park-ask-design.md:22`, `:92`.

   `ParkedOn` establishes an agent ask, a clean-exit park, and relative timing. Neither those facts nor absence of a structured question proves why the old run parked. Existing parks can carry their real question only in closing prose; the structured tool is an instruction, not an enforced prerequisite. Such a park can satisfy this predicate and be reopened with a helper's claim over its outstanding question.

   **Smallest fix:** Keep ambiguous legacy parks parked. Offer the person an explicit continuation with the helper's answer, or require positive recorded evidence of a helper wait before converting automatically. Test a park made after the helper ask whose person question exists only in its closing message.

5. **Blocking — a failed return-to-person transition can leave the lead held indefinitely.**

   **Where:** design `:319–328`, `:428`, `:533`; H `Driver/Loop/DriverHelpers.cs:256`, `:264`, `:274`, `:289`.

   The branch suppresses subsequent attempts after any caught transition failure until the record's update changes. That includes transient service failures, not just an older service's unsupported transition. Nothing in this state necessarily changes the record: the helper has finished, the planner leaves the completed lead alone, and no park reaches *What needs you*. The loop's retry line does not supply F1's requested person-facing fallback.

   **Smallest fix:** Separate unsupported/permanent refusals from transient failures. Retry transient failures on service recovery without requiring the held record to change; expose an actionable existing wait when the transition cannot be supported. Prove one failed request followed by recovery, and a permanently unsupported transition.

### 3. Findings on rows, proof and presentation

6. **Blocking — row 6 assigns the partner wait without its service changes or sufficient proof.**

   **Where:** design `:305–307`, `:314–324`, `:537`, `:572–573`; H `Core/Sessions/SessionLedger.cs:1392`, `:1492–1496`; baseline `Core/Sessions/Sessions.cs:1115`.

   Row 6 adds `ended.awaits-partner` “on the same contract,” but names only web and driver lanes. The required service transition recognizes exactly `ended.awaits-helper`; the tree-holding contract likewise names helper waits. An observation test showing the new ending cannot prove tree ownership, refusal recovery, continuation or restart behavior for a partner.

   **Smallest fix:** Separate the presentation change from the partner-wait change. Give the latter the required service/modules scope and the corresponding tree, cap-one, no-answer, delivery and restart proofs, or defer it explicitly until that contract is specified.

7. **Not blocking — the conversation notification is not evidence that the answer was taken.**

   **Where:** design `:245–248`, `:395–398`; `Driver/Loop/DriverHelpers.cs:439–445`; `Driver/Conversations/Chat/ChatRunner.cs:1770`; `Driver/Conversations/Domain/AnswersAlone.cs:94`.

   The cited conversation writer emits `user` with `Reaches: "resume"` to notify the runner that words wait. The actual prompt event is separate. A card that treats any helper-marked event as taking can announce *taken as the next turn* before that turn occurs.

   **Smallest fix:** Specify that taking requires the corresponding event with `Reaches` absent; the resume notification represents waiting. Add a projection case where only the notification exists.

8. **Not blocking — restart does not always refuse the interrupted helper's ask.**

   **Where:** design `:327–328`; H `Driver/Loop/DriverHelpers.cs:507`, `:512`, `:534`.

   Settlement first accepts a saved `helper_answer`, regardless of the helper record's interrupted ending. A restart after that file was written but before the turn ended therefore delivers the saved answer rather than necessarily bringing the lead to the person.

   **Smallest fix:** Qualify the sentence with “without a saved answer,” and distinguish the saved-answer and no-answer restart cases in the proof.

**8 findings: 6 blocking, 2 not blocking.**

**Verdict: another round.** The co-worker model, naming corrections and explicit foundation deferrals hold. Findings 1–6 still require changes to the delivery, recovery and build contracts before acceptance; assigning those obligations to unfinished HELPERWAIT1 does not resolve them. The next read should check the revised contracts and their evidence requirements, including the corresponding claims in D203.

## Codex, round 3

Codex's read of round 3 at `3a928e71c`, read-only, with HELPERWAIT1 as built merged in. HELPERWAIT1b and round 4 answer it.

### 1. Round-2 findings

**References:** `design` = `docs/2026-10-23-coworker-design.md`; `Driver` = `src/Daoris.Desktop/Daoris.Desktop.Driver`; `Tests` = `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests`; `Core` = `src/Daoris.Service/Daoris.Service.Core`; `Http` = `src/Daoris.Service/Daoris.Service.Http`.

| Finding | Verdict | Where | Why |
|---|---|---|---|
| 1 — answered parks cannot take both answers | taken | design:364–379; `Core/Sessions/HelperDesk.cs:156`; `Driver/Loop/DriverHelpers.cs:634`; `Tests/Loop/HelperWaitTickTests.cs:135` | Both guards changed; the Process case checks both answers in one prompt. |
| 2 — no durable receipt; relay hides evidence | open | design:380–401; `Driver/Loop/DriverHelpers.cs:660`; `Driver/Conversations/Helpers/HelperDoors.cs:86` | The ask’s state file is a sound receipt location, and lineage evidence is now read, but ambiguous HTTP failures can erase the attempt mark; finding 2 below. |
| 3 — unread question authorizes continuation | open | design:307–316; `Driver/Sessions/Questions/QuestionFiles.cs:68`, `:115` | The guard now parks, but settlement can delete the unread question; finding 1 below. |
| 4 — legacy park conversion cannot prove absence of a question | taken | design:372–379; `Tests/Loop/HelperLookTests.cs:679` | Conversion is withdrawn, including the closing-prose-only question case; the person’s answer remains necessary. |
| 5 — failed return can hold indefinitely | open | design:345–355, `:649`; `Tests/Loop/HelperLookTests.cs:795` | Retrying every look is valid and recovery is tested; the person-facing fallback remains unfinished, without a prerequisite on the first showing; finding 6 below. |
| 6 — partner wait lacks service scope and proof | deferred | design:317–323, `:631–657` | Presentation is separated from the wait; row 12 waits for `partner_ask`, then a design with service scope and corresponding proofs. |
| 7 — resume notice is not taking | taken | design:249–251, `:458–459`; `Driver/Loop/DriverHelpers.cs:266`; `Tests/Loop/HelperLookTests.cs:923` | Recovery excludes resume notices, and row 1 explicitly requires the same distinction in the exchange projection. |
| 8 — restart may deliver a saved answer | taken | design:356–363; `Driver/Loop/DriverHelpers.cs:557`; `Tests/Loop/HelperLookTests.cs:615` | Saved-answer delivery and interrupted-without-answer refusal are distinguished and tested. |

### 2. Findings on round 3’s built mechanism

1. **Blocking — the unread question is still deleted during conclusion.**

   **Where:** design:307–312 claims that unread questions park “with its question kept”; `Driver/Sessions/Questions/QuestionFiles.cs:115` keeps only a successfully read, fresh question and otherwise calls `Delete`. Both conclusion paths invoke this settlement: `Driver/Loop/Driver.cs:2436` and `Driver/Loop/DriverContinuations.cs:581`.

   The malformed question planted by `Tests/Loop/HelperLookTests.cs:994` makes the new guard return false, but that test never runs settlement. Through the actual conclusion path, the record parks and its unread question disappears. The cited test therefore does not establish the retention claim.

   **Smallest fix:** Specify that a parked conclusion preserves an existing question whose contents or freshness cannot be read. Apply that rule in settlement and prove retention through first-run and resumed-run conclusions. Correct §15.3’s finding-3 answer accordingly.

2. **Blocking — an ambiguous hand response can remove the protection against duplicate delivery.**

   **Where:** design:382–395 and `docs/decisions/D203.md:130` claim once-only delivery through the attempt mark. `Driver/Conversations/Helpers/HelperDoors.cs:87–88` returns the same `Handed: false` for an unreadable response or any unsuccessful HTTP status as for a definite refusal. `Driver/Loop/DriverHelpers.cs:666–668` then clears the mark.

   An unsuccessful response does not prove that nothing was kept. `Core/Sessions/HelperDesk.cs:181` keeps the word before the HTTP response is constructed; `Http/Program.cs:1729` subsequently awaits `ToSessionAsync`, and the exception handler at `:271` can answer 500. Thus a committed hand can clear its attempt mark. If the word is consumed and its event write fails—the original round-2 failure—the next attempt has neither evidence nor a mark and can hand again.

   The mark’s location holds; this failure classification does not. The cited tests cover definite refusal and interrupted attempts, not a committed hand followed by an unsuccessful response.

   **Smallest fix:** Distinguish confirmed domain refusals from unknown outcomes. Clear the mark only for a confirmed refusal that guarantees nothing was kept; preserve uncertainty for unexpected statuses and unreadable success responses. Add a case that commits the word, fails the response, then consumes it without event evidence.

3. **Blocking — cleanup releases protection before a failed return to the person has recovered.**

   **Where:** design:324–337 claims the waiting lead keeps its tree across exclusion, locks, planning and cleanup. `Driver/Trees/Branches/BranchTidying.cs:164–165` additionally requires an open helper ask, unlike `Core/Sessions/SessionLedger.cs:1507–1510`, which protects the completed wait while its quest remains taken.

   Once the last ask is refused, a failed return leaves the lead completed with no waiting words and no open ask. It falls out of cleanup’s active, waiting-word and helper sets. Cleanup can then release its retained lock (`BranchTidying.cs:103`; `Driver/Trees/SessionTrees/SessionLocks.cs:108–110`) and tidy an otherwise eligible tree while the ledger still considers that tree held.

   The cited regression actually expects protection to disappear immediately upon refusal (`Tests/Trees/BranchTidyingTests.cs:715–716`), without establishing that the lead has parked. Ask Daoris’s housekeeping inherits the same helper predicate (`Driver/Help/HelpHousekeeping.cs:193`).

   **Smallest fix:** Keep cleanup protection until the lead has successfully parked, continued or ceased holding its quest. Align this with the service’s predicate and test a refused ask followed by a failed return, including lock retention.

4. **Blocking — the sixteen-link limit can deliver to a part the line has already left.**

   **Where:** design:402–405 says delivery reaches the newest part, with a sixteen-link bound. `Core/Sessions/HelperDesk.cs:143–147` and `Driver/Loop/DriverHelpers.cs:713–726` stop after sixteen links and use the resulting intermediate record without checking whether it has another successor.

   With seventeen successor links, both sides select a left part. A stopped intermediate record passes the service’s delivery switch (`HelperDesk.cs:149–168`), so the answer can be kept on precisely the conversation D189 forbids reopening. Neither cited test exercises exhaustion of the traversal bound.

   **Smallest fix:** Retain the bound, but refuse delivery when it is exhausted and another successor exists. Keep the answer available to the person, and test a chain beyond the bound in both resolutions.

### 3. Findings on build order and proof

5. **Not blocking — the cited Process case does not prove cap-one progress.**

   **Where:** design:335–337 calls `HelperWaitTickTests.cs:97` a cap-one proof. Its driver uses `DriverConfig.Empty with` without setting `Cap` (`Tests/Loop/HelperWaitTickTests.cs:59–71`); `Driver/Platform/DriverConfig.cs:59` defaults that cap to **two**.

   The case proves continuation with a helper answer, but cannot establish that freeing the lead’s sole slot permits the helper to start.

   **Smallest fix:** Set `Cap = 1` explicitly in the existing case and verify the claimed sequence before retaining the cap-one citation.

6. **Blocking — row 11 is acknowledged work but is not a prerequisite of the first live showing.**

   **Where:** design:353–355 acknowledges that a refused return has no actionable wait. Row 11 specifies the missing fallback at `:649–652`, but the build order and first installed proof require only rows 1 and 2 (`:604–606`, `:616–622`). `docs/decisions/D203.md:134–136` likewise leaves the fallback to row 11.

   Retrying each look is a reasonable recovery choice; it fixes the earlier dependence on an unchanged record. It does not make a refusal visible in *What needs you*. Filing HELPERWAIT1b leaves that half of round-2 finding 5 open, and the stated order permits the first co-worker showing to land with it still open.

   **Smallest fix:** Make HELPERWAIT1b’s fallback a prerequisite of row 1’s installed proof and landing, or incorporate it into that row. Keep its specified refused-versus-unreachable cases and both-language proof.

**6 findings: 5 blocking, 1 not blocking.**

**Verdict: another round.** The answered-park correction, conservative treatment of legacy parks, notice/taking distinction and partner-wait deferral hold. Keeping the receipt on the ask and retrying failed returns at each look are also reasonable choices. Acceptance still rests on guarantees the built paths do not keep: unread-question retention, preservation of uncertain delivery outcomes, tree protection during recovery and delivery beyond the lineage traversal bound. Correct those contracts and their proofs, and make the remaining person-facing fallback a prerequisite of the first showing.
