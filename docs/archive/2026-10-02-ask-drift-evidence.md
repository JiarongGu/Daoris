# DRIFT1: how an ask's two requirements were lost while Daoris drove it (evidence note)

**Carried by:** D133 and the DRIFT1 row. It records one ask on the owner's install, traced through its intake, its
two quests, ten sessions and two branches. It is a record of what happened, not a contract.

> Written 2026-10-02, **read only**: the install's session records, transcripts and event logs, its service's ask and
> quest records, the two work trees' history, and the report repository's own knowledge documents. Nothing there was
> written, no session was re-run, and no process was touched. This note is de-identified: the owner's work
> workspace, the report repository, the site, the ticket and the people are not named, and quoted words are the
> owner's with only their spelling corrected (the owner's request, 2026-10-02). The raw notes, with every name, id
> and quotation as written, are untracked.

## The answer

1. **The ask named one requirement and implied the second; the person stated the second twice, and both times it
   reached exactly one session.** The ask said the work "will need v3 bridge". In the report repository's own
   documents, the bridge is the door through which the older shell shows a report built with the common-report
   module, and the only kind ever exercised through it. The person then said "common-report" twice: once as a
   message added to a running session, once as an answer to a parked one. Neither statement outlived the session
   it was given to.
2. **Four Daoris mechanisms dropped them, in order:** the intake's paraphrase (§3.1), a park record that kept an
   answer's question away from the next session (§4.2, already fixed by PARK1), a carry-on that hands only the last
   session's answer (§4.3), and an account's limit that made that last session one that had said nothing (§4.4).
3. **A closing note became the requirement.** The implementing session closed with its own reading of an answer,
   attributed to the person ("Per your answer"). Every later session quoted that reading back to the person as
   their decision (§4.5).
4. **The correction landed on the verification quest**, whose title and criteria never changed, so the sessions that
   carried it either re-planned the build or wrote code under a "Verify" title (§4.6).
5. **Nothing in Daoris holds what the person said after the ask.** The ask record holds its sentence, its quests and
   its intake; the quests hold the intake's text. The person's four later sentences exist only in session records
   and transcripts (§4.7).

## 1. The ask, and what its words mean where the work lives

- **The ask** (W1, 1 October 02:26 UTC, tier intake), with a link to the ticket: *"complete [the ticket] I already
  logged in [the site's system], and this will need v3 bridge and also we need to fix the v3 bridge view that powered
  by [the company's] AI should from v3 page instead from v2 currently it become fixed footer because of this"*.
- **The ticket**, as the sessions quoted it from a signed-in browser: a daily report comparing six production lines,
  with four cards, two charts, a sortable table, a drill-down and an export. Its calculation rule: *use each
  location's existing daily calculation service; do not introduce a different calculation method*, and its
  acceptance requires each line's figures to *match its individual daily report*. The ticket's text, as read twice,
  contains neither "common" nor "bridge".
- **"The v3 bridge"**, in the report repository's own knowledge document about it, exists because *a report authored
  with the v3 common-report editor could only be read in the v3 shell*. Its v3 row is the common-report route, and
  the document's *when this rule does not apply* says a report type other than common-report *has not been
  exercised*, since only common-report is fully configuration-driven.
- **"Common-report"** is that module: reports as configuration, edited in an editor, with features added to the
  module rather than per report. The other customer's dashboard the person later pointed to (W3) is one of those
  configurations.
- **What was built** is a new, dedicated report type with its own page, its own query and its own navigation entry,
  listed beside the repository's other dedicated types, and authored by a script because the editor does not know it.

## 2. The timeline

All times UTC. Every implementing and verifying session ran on the protocol door. S0–S6 ran on the default account
(their records name none); S7 on account 1; S8 and S9 on account 2, after rotation.

| Session | Quest | Window | Handed | Decided, built | Ended, last words in short |
|---|---|---|---|---|---|
| S0 intake | the ask | 1 Oct 02:26–02:29 | W1, the ticket | Read the ticket and the bridge document; published Q1 (build the report, reach it through the bridge, fix the footer) with a `then` step Q2 (verify in the browser), written from the ticket | published; flagged the lines' naming |
| S1 | Q1 | 02:29–03:20 | Q1's text, W1 at its foot | 02:42: a dedicated page over the older report's per-line endpoint, the calculation moved to a shared module; footer moved into the v3 page; two work-in-progress commits; prod run refused by the harness | **03:18 the person added W2**; S1 argued against it and asked which report is "the individual daily report": the older one (keep going) or a common-report dashboard (switch). Parked |
| S2 | Q1 | 03:20–04:08 | Q1, **W3**, S1's record: one closing sentence | Read W3 as a formula: re-implemented the other customer's formula in its own stored query, kept the dedicated type, reverted the shared module, squashed into one commit | Q1 done, "Per your answer"; prod entries scripted, not created |
| S3 | Q2 | 04:08–04:12 | Q2 (from the ticket), "read Q1 for the work" | Read only: nothing in prod; measured each line's figure against its daily report (one line 81.0 % against 89.1 %), "your 1 Oct decision" | parked: two permissions asked |
| S4 | Q2 | 08:14–08:20 | Q2, **W4**, S3's record | Loaded the common-report skills and documents, read the ticket again, sent four explorers to map what common-report lacks | **cut off: the account's spend limit**; no plan kept, no commit |
| S5, S6 | Q2 | 08:20, 11 s and 16 s | Q2, "cut off: spend limit" | nothing | the same limit (the third cut-off parks) |
| S7 | Q2 | 2 Oct 06:12, 18 s | Q2, the limit, last words: the limit | nothing | the same limit; account 1 cooling |
| S8 | Q2 | 06:12–06:15 | Q2, the limit, "nothing of its own conversation carries over" | Read only, the same blockers S3 found; "the formula you chose" | parked: prod entries, and where to test |
| S9 | Q2 | 09:13–09:49 | Q2, **W5**, S8's record | A mirror of the site's report in the development environment, the new report's entries created there, one commit; found the older shell's menu does not route a bridge entry; began fixing that code, uncommitted | **stopped by the person** |

The person's later words, spelling corrected: W2, *"I think we should be using the v3 common-report so there is no
need for new backend api?"*; W3, *"I think the existing calculation means using the data with the [other customer's]
logic? you can see the design is far different"*; W4, *"the new oee has its calculation in the ticket [link] so you
need to check this properly and we should be using v3 common-report if any feature is missing we should be add it
into common-report module instead"*; W5, *"you can run at local and set up in dev since that's what local is pointing
to, test this properly then we release it to prod"*. On 2 October, to the parent: *"what I asked is to use v3 bridge +
common-report but it's not doing that at all"*, and *"we need to investigate why the decision drifted"*.

## 3. Where each requirement went

### 3.1 The v3 bridge

- **Stated** in W1, twice: the report needs it, and its footer must come from the v3 page.
- **Kept by name, emptied of its meaning, at intake.** S0 read the bridge document whole and wrote that it
  *documents a v3 report hosted in the v2 shell*; part 2 of the quest became *make it reachable through the v3
  bridge*. The document's other half, that the bridge carries a common-report report and nothing else has been
  tried, did not reach the quest.
- **Built around, not through.** S1 read the same document, modelled the page on a dedicated report type, and
  decided its shape within a quarter of an hour without weighing common-report. S2 kept that shape. The bridge entry then points
  at a report type the bridge document says was never exercised.
- **The footer half was built** (S1, S2): the one part of the ask plainly done in code.
- **Never seen working.** Nothing reached prod: the entries needed a go-ahead S3 and S8 asked for, and the code was
  never merged. In development, S9 found the older shell's menu sends a bridge entry to its catalogue route, and read
  the same gap in prod's menu data.

### 3.2 The common report

- **First stated mid-turn** (W2, 03:18), to S1. S1's own decision was to push back: common-report would break the
  ticket's *match the daily report* rule, and the module lacks four features, each *a change to shared engine
  components*. It asked a two-way question whose second option was *a common-report dashboard like* the other
  customer's.
- **The question did not travel.** S1's record kept only *"The quest is still taken while I wait for your answer."*:
  the console transcript was read back from its end and stopped at the first indented line, which the question's own
  list had. PARK1 fixed that reading the same day.
- **The answer travelled alone.** W3 names the other customer's logic. Read with the question it picks the second
  option, since that dashboard is a common-report configuration. S2 was handed W3 and the one closing sentence, and
  neither W2 nor the question, and read *logic* as *formula*. It closed Q1 done saying *per your answer*.
- **Stated again, on the wrong quest** (W4, 08:14), as the answer to S3, which was verifying; Q1 was already done.
  W4 adds the rule the repository works by: a missing feature goes *into the common-report module*.
- **Lost at the limit.** S4 acted on W4 and was cut off before it kept a plan or a commit. S5–S8 were each handed the
  previous session's record, which was now the limit, and S8 ran on another account. W4 was gone from every prompt
  after S4. S8 and S9 went back to verifying the dedicated build "per the formula you chose".

## 4. The mechanisms, on Daoris's side

1. **An intake paraphrase can drop what the receiving repository's own definition implies.** The ask's sentence is
   appended verbatim to the quest the intake publishes (`DraftBodyOf`, `src/Daoris.Service/Daoris.Service.Core/Asks.cs`),
   but the quest's parts are the intake's words, and nothing asks that each part keep the person's words.
2. **A park record kept an answer away from its question.** Pre-PARK1, the record's words came from the transcript's
   last plain lines (`Driver.LastWords`). Fixed: `ParkedWords` now prefers the record's last message whole.
3. **A carry-on is handed one hop.** `PersonSaid` is the previous session's answer only
   (`src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs`, `Before` in `Adapters.cs`). A message the person added
   mid-turn (W2) is in no prompt at all.
4. **A cut-off replaces the answer with the cut-off.** After S4, each carry-on quoted the previous session's failure;
   TOOL4f's last words were the limit's own sentence. After an account change nothing of the conversation carries
   (D125 §3.5), so resuming the conversation, which ANSWER1 designs, would not have reached S8.
5. **A closing note is read as the requirement.** A follow-up's prompt says *read that quest for the work this one
   builds on*; Q1's note carried the build's reading, attributed to the person, and S3 and S8 quoted it back as the
   person's decision. Q2's body was written from the ticket and carries none of the asker's words (a `then` step is
   published with only `{parent}` expanded, `Quests.cs`).
6. **A correction has nowhere to go but the next prompt.** W4 changed what to build, on a verification quest. The
   quest's title and criteria stayed *verify*; S4 re-planned the build under it, and S9 edited code under it.
7. **The ask holds none of it.** The ask's record and the quests' records show none of W2–W5.

## 5. What is fixed, in flight, and uncovered

| Mechanism | Fixed or in flight | Left uncovered |
|---|---|---|
| §4.2 park record | PARK1 (built 1 October) | none for that case |
| §4.3 one hop | ANSWER1 (D131, in flight): an answer resumes the agent's own conversation | a carry-on on another account, a fresh start after the strikes, a follow-up quest |
| §4.4 cut-off | TOOL4f (D125): last plan and last words | the person's words, when the cut-off session had none of its own |
| §4.1, §4.5, §4.6, §4.7 | none | all |

## 6. What Daoris should hold

D133 decides it: the person's words are the ask's record, every session on the ask is handed them whole, a quest's
requirements quote them, and a done answers each one. The rows it proposes, in the backlog's shape:

- **DRIFT1a — the person's words are the ask's record** (service). Every sentence the person gives on an ask (the ask,
  each answer, each message added to a running session) is kept on the ask verbatim, with when and to which session
  and quest. Today W2–W5 live only in session records. Contract: D133 §1. Proof: service tests, an answer and an added
  message read back from the ask, seen failing first.
- **DRIFT1b — every session on an ask is handed them** (driver). A first start, a resume, a carry-on on any account
  and a follow-up step's session are handed every one of the ask's words, newest last, replacing the one-hop answer.
  S8 lost W4 two hops after it was said. Contract: D133 §2. Proof: a driver test on this chain's shape (answer, cut
  off, cut off, rotated carry-on still quotes the answer), seen failing first.
- **DRIFT1c — a quest's requirements quote the person** (service, MCP, intake). `quest_publish` takes requirements,
  each the person's own words and the check that proves it; the service refuses words that are not in the ask or a
  recorded answer, naming them; a follow-up inherits them. S0's paraphrase kept *bridge* and lost what it meant.
  Contract: D133 §3. Proof: refusal tests; the intake prompt's golden.
- **DRIFT1d — a done answers each requirement** (driver, service, web). Closing `done` says, per requirement, met or
  departed with the reason and the person's words relied on, quoted; a departure holds the follow-up for the person's
  yes. S2 closed *per your answer* on a reading nobody checked. Contract: D133 §4. Proof: a held follow-up in the
  service tests; the look.
- **DRIFT1e — a follow-up checks the ask, and a correction goes back to the work** (driver, service; design first). A
  verifying session is told the parent's note is the build's account and the ask's requirements are the measure; a
  correction to what was built becomes a requirement of the parent quest, which reopens, rather than work under a
  *verify* title. W4 was given to a verifier. Contract: D133 §5. Proof: the design, then its rows.

## 7. What a clean-up of this ask must know

For PAUSE1 (D132), which owns the clean-up: Q2 is still taken; two local branches hold the work, unpushed, and one
tree has uncommitted changes. **S9 wrote four records outside the repository**, in the development environment: a
mirrored report entry and its shift group, and the new report's two entries, all visible to administrators only.
Discarding the trees does not remove them, so a clean-up that lists only trees and branches would leave them. Nothing
was written to prod, and no process from either tree is running.

## 8. What this note does not cover

- **What W3 meant** is read from the question and the answer together, and the owner's later sentence agrees; nobody
  asked the person at the time.
- **The ticket** is as two sessions quoted it; its attachments, comments and history were not read here.
- **The prompts** are the ones the install recorded on 1 and 2 October; the install was updated between them (TOOL4f's
  last words appear only on 2 October). No session was re-run with the words D133 would hand it, so whether they would
  have changed S2's reading is not measured.
- **Cost** is not counted here.
