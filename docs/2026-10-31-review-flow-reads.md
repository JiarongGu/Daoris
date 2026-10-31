# REVIEWFLOW1 and LINESTOGETHER1: both makers' reads of the synthesis (2026-10-31)

The synthesis is `2026-10-31-review-flow-design.md`. Both reads follow as written.

## Codex's read

1. **Verdict: another round**, focused on the combination protocol. The acceptance relocation and authoritative-head read are sound, separable changes. Section 4 does not yet establish that one acceptance preserves every member’s gates and the head’s conflict resolutions.

2. **Findings**

**BLOCKING — Combining content does not combine gate authority.** `src/Daoris.Desktop/Daoris.Desktop.Driver/Review/EntryAccepts.cs:114` reads each part’s gate using its own quest and session; `Workplace/EntryLands.cs:279` preflights those gates, and `:312` lands each part with its own gate and result checks. Merging a separate quest into a chat head does not make the chat’s acceptance perform those checks. `Workplace/IntegrationRequests.cs:10` explicitly requires a unit’s own landing provenance. **Change:** define the combined acceptance’s member gate checks, pinned submissions, inclusion records and workflow settlement. Explain how included parts of the head’s own entry avoid being landed again by `EntryAccepts.PartsOf` (`:26`). Test a clear head with a member blocked by review, opinion or workflow.

**BLOCKING — The existing landing replay does not preserve merge resolutions as assumed.** `src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/LineHolds.cs:296` enumerates commits with `--no-merges`; `BranchReplays.cs:517` uses ordinary `rebase --onto`. When the line advances, `TreeLands.cs:574` selects replay. A conflict resolved in a merge commit can therefore encounter the original conflict again or fail the replay’s content proof. Proving member-tip ancestry before acceptance does not prove the accepted result survives this transport. **Change:** specify a supported representation or replay that preserves the resolved combined tree. Add an advanced-line fixture, including a conflict resolution recorded only in the merge commit, through branch, merge and squash landings.

**BLOCKING — The operation needs enforcement through acceptance, beyond page holds.** The cited `src/Daoris.Web/src/work/review/DiffPane.tsx:83` describes disabling a page control. The actual landing serialization is per tree (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/TreeLands.cs:405`). Neither establishes the proposed operation’s authority across member trees, terminal landings or automatic acceptance. The synthesis also specifies staleness before *checked*, without binding the subsequently accepted head tip to that check. **Change:** name the backend guards at every landing door, overlap-operation exclusion, and the acceptance-time recheck of the combined tip and member inclusion. Test another window, terminal acceptance, head reset after *checked*, cancellation and restart.

**BLOCKING — Moving a held word is an unrecoverable two-call gap as written.** `src/Daoris.Service/Daoris.Service.Core/Sessions/SessionLedger.cs:1512` removes the source word; `src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/ConversationRoutes.cs:405` returns only success or *taken*. A crash after removal but before saving the destination draft or saying it loses the durable word. **Change:** specify a recoverable, idempotent transfer with a stable identity and explicit uncertain-delivery handling. Test interruption between removal and destination delivery. LINESTOGETHER1b cannot simply assume the existing edit door supplies that protocol.

**Not BLOCKING — Commit-only overlap caching is insufficient.** `src/Daoris.Desktop/Daoris.Desktop.Driver/WorkOrders/Work/WorkOverlaps.cs:41` compares the base against the working tree, including tracked uncommitted changes. Those can change without changing the commit. **Change:** either compare committed tips explicitly and describe that limitation, or invalidate the cache on working-tree/index changes and include the base in its identity. Test edits without a commit and failed reads after a successful read.

**Not BLOCKING — The press inventory overstates available steward substitution.** `src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/StewardActRows.cs:154` registers settings, stop, finish and retry acts; hand-over remains a later row (`TASKS.md:85`). The inventory also omits existing queue editing, urgency and turn cancellation (`src/Daoris.Web/src/work/WorkFrame.tsx:2657`). **Change:** distinguish built, dark and future acts, and enumerate those conversation controls.

3. **Rejections of my proposal**

The placement rejection is adequate: D168 supports access at both reading positions through one owner.

The integration-door rejection is partly adequate. Its own-landing requirement is real, and my proposal explicitly needed a new acceptance spanning sessions. Choosing head consolidation can reduce reviews, but it exchanges that work for the unresolved gate and replay contracts above; it is not justified merely by calling these existing doors.

The attribution that I proposed a head re-derived on the page is incorrect: my proposal explicitly rejected reconstructing it in React. Correct that rejection. I accept dropping the duplicate Integration list.

4. **Rows and order**

The broad order works, but the rows are **not all buildable as written**.

Ready for separate acceptance now: **REVIEWFLOW1a, REVIEWFLOW1b and LINESTOGETHER1a**. No implementation is authorized by this read itself.

Then specify word-transfer recovery before **LINESTOGETHER1b**, fix cache identity before **1c**, and settle the gate, replay and backend operation contracts before **1d**. **1e** follows that protocol and steward activation; qualify its other promised acts by their actual dependencies. Keep **REVIEWFLOW1c** last, proving both successful delivery and recovery in the installed window.

Read-only review; nothing changed, and no repository `local/` directory was read.

## The Opus maker's read

**1. Verdict:** accept after named changes; three are BLOCKING. B1 holds REVIEWFLOW1a; B2 and B3 hold only LINESTOGETHER1d.

**2. Findings**
- **B1 (BLOCKING, REVIEWFLOW1a): *Leave part n out* on every part with a tree of its own lands the wrong thing and reports it falsely.** `EntryLands.cs:128` leaves a part out only when it does not share an earlier part's tree (`same is null && leaveOut.Contains`). A continued part keeps its recorded directory (D209 point 3), so a chat's parts share one tree, landed by its first part: *Leave part 1 out* on Lyntai's chat would leave out parts 1 to 6, and `Outcome` (`:430`) would report parts 2 to 6 `same-tree`, falsely. A part a later kept part grew from (`:246-247`) is landed anyway. The widening also changes D167 points 2 and 3 unnamed. Change: leave-out per tree, on its first part, naming every part it takes; none on a part a kept later part carries; `Sequence` marks a left-out shared tree's parts `left-out`; a D167 note; an `EntryLandingsTests` case.
- **B2 (BLOCKING, 1d): a merge in the head's tree does not survive the Accept's replay.** Kept commits are judged with `log --no-merges` (`LineHolds.cs:296`) and replayed with `rebase --onto` without `--rebase-merges` (`BranchReplays.cs:517`): on a moved line the head's resolutions, in its merge commits, are dropped. The *checked* ancestry test contradicts the integration design's §5.7 (patch and content, never ancestry). Change: the fixed words ask the head to replay each member's commits onto its branch, settling each conflict in the commit that meets it; *checked* reads containment by patch and content (`LineHolds`/`ContentHolds`); proof with the line moved after the head's turn.
- **B3 (BLOCKING, 1d): the door makes its own operation stale in the install's case.** An ended record keeps a word for its next part (`SessionLedger.cs:1103-1104`), so the press's words open a successor by `from`, the walk passes the bound head, and §4 ends the operation stale. Change: bind the operation to the head's line, carry it to a successor its own word opened, go stale only on a move it did not make.
- **N1:** a chain step has no `from` (`SessionEntryReader.cs:111`; `Lines.cs:49-55` follows only `from` and the same quest), so a chain's live step reads as beside an ended first step; an ask's sibling quests share an entry (`SessionEntries.cs:33-34`), so the first-created becomes the head arbitrarily; opening on the head would hide a working part. Change: state the head for a chain (its newest step) and for siblings; open on the head unless a part waits on the person or is working.
- **N2:** the `accept` wait stands only on the attended part's `lands` (`waits.ts:181-183`), and the entry's Accept needs more than one part offering work (`WorkFrame.tsx:1907`), so a head with no work beside a part with work shows no Accept. Change: the work wait stands on the session's offer (one or more parts).
- **N3:** *integrating* is already said (INTEGRATE1h's fact, `headFacts.ts:66-67`, `:163`); REVIEWFLOW1b reads it.
- **N4:** `git diff --name-only <base>` reads the working tree (`WorkOverlaps.cs:41`): key an ended clean tree by its commit, read a running tree at each look.
- **N5:** *brought into* and *head* need glossary rows; *contained* (已包含) is the line's.

**3. Rejections of my proposal:** adequate (*Now · next* folded into the facts line for HEADFIT1's budget; the head by the service's walk; no list on the Integration tab; *brought into*; `SESSION_QUEUE_EDIT` for held words). The synthesis kept my leave-out widening and my merge-then-ancestry check; both are wrong (B1, B2).

**4. Rows:** the order holds. REVIEWFLOW1a stays web-only (the placement and N2's wait) with the per-tree leave-out its own small row after it; REVIEWFLOW1a, 1b and LINESTOGETHER1a's page half touch the same files, so they run one after another. Can start now: REVIEWFLOW1a reduced, and LINESTOGETHER1a's service and driver half once N1's rule is stated. LINESTOGETHER1d only after B2 and B3 are written; 1e after STEWARDROLE1d and 1d; REVIEWFLOW1c last.

## The steward's settlement, so far

Codex asks another round on the door (1d) and the word's move (1b); the Opus maker accepts after named changes. Both reads find REVIEWFLOW1a sound once reduced to the web (*Accept all* where the changes are, one owner per D168, and the work wait standing on the session's offer, N2), so the steward accepts that row alone now and builds it. The rest is revised (every blocking finding of both reads) and read again.

## Round 2: the reads of the revision

### Codex's read

1. **Verdict: another round, focused on LINESTOGETHER1d.** LINESTOGETHER1b’s transfer contract is now acceptable as a design; it still needs its stated implementation proofs.

Code references below use **Driver** = `src/Daoris.Desktop/Daoris.Desktop.Driver`, **Service** = `src/Daoris.Service/Daoris.Service.Core`.

2. **My previous blocking findings**

- **Uncombined gates: substantially resolved; inclusion remains incomplete for merge landings.** The revision correctly requires each member’s own gate, pinned tip, submission and result checks, including preflight and before-push checks. These match `Driver/Review/EntryAccepts.cs:114`, `Driver/Workplace/EntryLands.cs:279`, `:312`, and `Driver/Workplace/PressedResults.cs:218`. Extending `Sequence` so included members never land separately also answers the double-landing finding. However, the proposed inclusion and workflow settlement depend on `LandedBranch.Work()` and `Names()` (`Driver/Knowledge/Domain/WorkplaceFacts.cs:166`, `:225`). Merge landings explicitly keep **no landing record** (`Driver/Workplace/TreeLands.cs:771`); workflow settlement instead uses `facts.Accepted` (`Driver/Workflows/Domain/WorkflowRun.cs:995`). See the concrete change below.

- **Replay: the representation fix resolves the original merge-resolution assumption; the content check remains blocking.** Ordinary replay still excludes merges (`Driver/Workplace/LineHolds.cs:296`) and uses plain rebase (`Driver/Workplace/BranchReplays.cs:517`). Recording resolutions in replayed commits is therefore sound. But “the head changed each conflicted file” does not prove containment. `TrialTreeAsync` stops at its first conflicting commit (`Driver/Workplace/LineHolds.cs:191`): later clean changes may never be examined. A head can touch the conflicting file, omit another member change, and satisfy the proposed exception. Also reject merges throughout the range the landing will replay, rather than only since the press.

- **Backend enforcement: correct location, incomplete admission synchronization.** Guarding beneath the tree hold and pinning the checked head answer the original page-only enforcement and reset findings (`Driver/Workplace/TreeLands.cs:404`, `:415`, `:592`, `:617`). However, operation creation uses the store lock while landing uses a separate tree hold. A landing can pass the operation guard immediately before the operation binds that tree. Checking “no landing” before insertion does not close that race.

- **Held-word move: resolved as a design contract.** One `ExclusiveAsync` can encompass both writes (`Service/Sessions/Sessions.cs:849`); its transaction commits only after the callback completes (`Service/Platform/ConnectionGate.cs:139`). The stable `move:<part>:<word>` key, waiting/settled lookup and uncertain-answer handling repair the two-call gap. Implement retry lookup before source-word absence or current-state refusals, as the existing say settlement precedes refusals (`Service/Sessions/SessionLedger.cs:1078`). Prove retries after destination consumption and successor creation, not only while waiting.

3. **Concrete blocking changes**

- Replace file-touch containment with complete accounting of every member change. Unproven conflicts need an explicit superseded/disposition record; do not call them contained. Test a conflicting first commit plus an omitted later clean commit.
- Serialize operation admission with landing/discard admission across processes, using a defined lock order. Test a landing paused after its guard while binding is attempted.
- Define durable member inclusion, gate provenance and workflow settlement for merge landings, including restart recovery; test branch, merge and squash forms.

4. **Rows that may start**

Continue **REVIEWFLOW1a**; start **REVIEWFLOW1d**, then **REVIEWFLOW1b**, respecting their shared-file order. **LINESTOGETHER1a’s service/driver half** and **1c** may start; **1b** may follow authoritative-head support. Hold **1d**, its dependent **1e**, and installed proof **REVIEWFLOW1c**.

Read only; nothing changed. No `local/` directory was read.

### The steward's settlement

Both makers accept every row but the door. The Opus maker's round-1 blocking findings are answered in the revision (B1 by REVIEWFLOW1d, B2 and B3 in LINESTOGETHER1d), and Codex now asks another round on LINESTOGETHER1d alone. So the design is accepted for REVIEWFLOW1a (built), REVIEWFLOW1d, REVIEWFLOW1b, LINESTOGETHER1a, 1b and 1c, built in that order where they share files; LINESTOGETHER1d (and 1e and REVIEWFLOW1c after it) is revised once more on Codex's three blocking changes and read again by both makers.
