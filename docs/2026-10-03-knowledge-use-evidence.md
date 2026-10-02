# KNOWUSE1: why the owner's work repository's sessions ask the owner what they ask (evidence note)

**Carried by:** D135 and the KNOWUSE1 row. It records every question the driven sessions of one repository put to the
owner over five days, classed by what could have answered each, against what each session read and was told. It is a
record of what happened, not a contract.

> Written 2026-10-03, **read only**: the install's session records, event logs and machine log, its service's quests
> and asks, the session trees, and the repository's own documents on its line and in its history. Nothing there was
> written, no session was re-run, and no process was touched; one session was still running when read. This note is
> de-identified: the workspace, the repository, its neighbours, the customers, the sites, the tickets and the people
> are not named, and quoted words are the owner's with only their spelling corrected. The raw notes, with every name,
> id and quotation as written, are untracked.

The owner, 2026-10-03: *"I found [the repository] itself is kind of ignoring the repo's knowledge docs? since a lot
asks back to me should not be there? and if this is the case we need to check the process properly or the initialize
of daoris system still not 100% working and that will need LLM/AI to involve"*.

## The answer

1. **The sessions do not ignore the repository's knowledge.** The repository keeps its own discovery gate (four skills
   as the first calls, every routed document read, its three hand-written indexes scanned), and 20 of the 22 driven
   sessions that did work ran it. They read 1 to 25 knowledge documents each (median 4.5). For every question a
   document answered, or a document told the session to ask, the session (or the one whose fix it verified) had read
   that document (§3, §4).
2. **46 items reached the owner: 15 in 6 parks, 31 in closing notes.** 25 needed the owner. 13 of those were asks for
   a go-ahead on a production write, made for **three** acts. 10 were answerable from the ticket, the code or a
   read-only look; 6 came from drift or Daoris's own mechanics; 3 were asked because the repository's own documents say
   to ask; and 2 were answered by a document the session had read (§4).
3. **Daoris's set-up was not the missing piece.** The repository's own gate is stronger than the canon's discovery
   skill, and the unmerged set-up branch replaced the richer one (§2.1). Recall pushed into the prompt would have
   added nothing: every governing document was already read (§6).
4. **WSSETUP9 changed where questions go.** Before it, five parks carried 13 items, four of them answerable from the
   sources. After it, one park carried two, both the owner's to give; the readings moved into closing notes, where the
   owner still meets them as questions (§5.3).
5. **A drifted reading is now knowledge.** The second attempt at one ticket found, in the first attempt's closing
   note, an answer that attempt had misread, and wrote it into the repository's knowledge as the owner's decision
   (§5.4). A check that answers from knowledge would answer from it.

## 1. What was read

Every driven session on the repository from 28 September to 2 October: 33 sessions on three tickets and the set-up,
plus the workspace's three intakes and two sessions a neighbour repository ran for one of them. The ticket labels:

| Label | The work | Sessions |
|---|---|---|
| T1 | a dashboard figure that read zero, its verification and a follow-up | 10, three of them ended by a spend limit |
| T2 | a speed figure, then the daily table's comparisons, over three follow-ups | 8, one stood down, one killed by a stray driver loop |
| T3 | a comparison report: the first attempt (DRIFT1's S1–S9) and a fresh one | 14, six of them cut off by a limit; one still running |
| U | the repository's set-up (D124, the pilot of D128) | 1 |

Not read: the ten sessions of an earlier ticket (26–27 September).

## 2. What the sessions were told

### 2.1 By the repository

- **Its always-read instructions open with a blocking gate**: invoke four discovery skills together, read every
  document the routing skill names, *"Scan the rule indices (Applies When column) → Read every matched rule"*, and only
  then explore code: *"a matched rule you don't Read is a contract you don't have."* An always-loaded rule repeats it,
  and an always-loaded router sends every task to the cross-cutting index, *"no exceptions"*.
- **Knowledge lives in one folder**, reached through three hand-written indexes, one cross-cutting and one per part of
  the code. The instructions name them.
- **The instructions also say to ask**: *"NEVER commit without explicit user approval. Always ask "Ready to commit?""*.
  No session asked it; every one committed on its branch as Daoris's prompt told it to. The prompt overrides a
  repository's instruction to ask when it says so plainly.
- **The unmerged set-up branch** replaced the repository's own routing skill (129 lines of routing by part of the code)
  with the canon's 34-line one, moved the routing into a knowledge document, and retired the repository's own
  discovery rule as a duplicate of the canon's.

### 2.2 By the driver (`TargetPrompt.Asking`, `Adapters.cs`)

Sessions up to 1 October were handed the old paragraph:

> If it needs something only the person can give — a sign-in, a go-ahead for an act outside this repository, a choice
> between options that is theirs — say exactly what and why in your last message, commit what you have, and end your
> turn with the quest still taken, rather than declining.

From 2 October, WSSETUP9's, as the code reads today:

> Look before you ask. The quest, its links and its files; this repository's own documents, code and history (its log,
> and the commits that last changed what you are changing); the workspace's knowledge, through your connector's
> `knowledge_search`; and the other checkouts listed above. A question one of these settles is not a question: decide
> it, and keep what settled it for your closing note. Where the evidence leans one way without settling it, take that
> reading, carry on, and say in your closing note which reading you took and on what evidence, so the person can
> correct it in review rather than be stopped by it.
>
> Stop only for what no source holds and only the person can give — a sign-in, a go-ahead for an act outside this
> repository or on a production system, a preference nothing records.

The prompt names no index: *this repository's own documents* is the whole pointer.

## 3. What the sessions read

| Work | Working sessions | Ran the repository's gate | Knowledge documents read | Index files opened | `knowledge_search` calls |
|---|---|---|---|---|---|
| T1 | 7 | 5 (the two others resumed the same scope) | 1–5 | 0–4 | 0 |
| T2 | 5 | 5 | 5–16 | 2–4 | 0 |
| T3 | 10 | 10 | 1–25 | 1–4 | 5, in 4 sessions |

Every `knowledge_search` answer ended *"Matched on words only"*: this install's service has no meaning tier. Several
sessions also searched the owner's personal memory store, handed to every session by the owner's own configuration,
and got entries about other projects.

## 4. Every question, classed

**K** answered by a knowledge document · **R** the repository's own documents require asking · **B** answerable from
the ticket, the code, the history or a read-only look · **O** only the owner · **D** drift, lost context or Daoris's
own mechanics. *Read* is whether the session (or, for 14, the session whose fix it verified) had read the document.

| # | Work | Where | The question, in short | Class | What answers it | Read |
|---|---|---|---|---|---|---|
| 1 | T1 | park | a production sign-in so a probe can run | O | — | |
| 2 | T1 | park | the production configuration write | O | — | |
| 3 | T1 | park | what the headline count means once its two parts differ | B | the tile's layout, which a later session read | yes; the document said both, written before the parts differed |
| 4–5 | T1 | park | the write, and the count, again | O, B | as 2, 3 | |
| 6 | T1 | park | hourly labels on a joined query sit off the hour | B | a later session's probe: an artefact of the probe | |
| 7 | T1 | park | go-ahead for the write | O | the owner: "run the put" | |
| 8 | T1 | close | which branch the pull request carries | O | the push is the owner's | |
| 9–10 | T1 | close | two views disagree on one day: its own ticket? | O | — | |
| 11 | T1 | close | a clean deep link fails; the bridge form works | K | the front-door document: deep links *"404 at the origin"* | yes |
| 12–13 | T2 | close | a display-only production write; a sign-in to verify | O | — | |
| 14 | T2 | close | a comparison is now taken between rounded values; shared code left alone | R | *"If a shared component genuinely is wrong, say so and get agreement BEFORE changing it"* | yes (its chain) |
| 15 | T2 | close | the display write, "your call" | O | — | |
| 16 | T2 | close | a dead session holds the quest; release it | D | a stray driver loop | |
| 17–18 | T2 | close | the display write again; the push | O | — | |
| 19 | T2 | close | a total compared with a week's total: which average? | B | the column's own label, "vs Last 7 Days Avg" | |
| 20 | T2 | close | a trailing week would match the label; that reopens a decision | R | *"re-open it with the user, not in code"*; the owner did | yes |
| 21 | T2 | close | a day-boundary setting does nothing in production | O | design intent nothing recorded; the document said it worked | |
| 22 | T2 | close | a week with no data: the line idle, or the feed? | O | the plant, or the ingesting repository | |
| 23–24 | T2 | close | a subtitle in the next write; the branch base | O | — | |
| 25 | T3 | park | which report is "the individual daily report": keep the build, or switch to the configurable module? | R | the shared-component rule above; the bridge document (bridging any other report type: *"the others have not been exercised"*); the owner had said the module that turn | yes, both |
| 26 | T3 | park | that dashboard's id, or that it does not exist | B | a read-only list of the site's reports, which two later sessions made | |
| 27 | T3 | park | a production menu entry | O | — | |
| 28–29 | T3 | close | the availability definition; a drill-down that will not match | D | the ticket's calculation; both rest on a misread answer | |
| 30 | T3 | close | the target line's value | B | the ticket: *a configured target* | |
| 31 | T3 | close | the catalog entry a bridge row names | K | the bridge document: *"any catalog `reportId`"* | yes; later found incomplete |
| 32–33 | T3 | close, park | the production entries | O | — | |
| 34 | T3 | park | run the branch locally against production data | O | the harness had refused it; see §5.1 | |
| 35 | T3 | park | "that reverses your decision" | D | DRIFT1 §4.5 | |
| 36–37 | T3 | park | the production entries; where to run the check | O | the owner: *"you can run at local and set up in dev since that's what local is pointing to, test this properly then we release it to prod"* | |
| 38 | T3 | close | availability per the owner's "earlier answer" | D | §5.4 | |
| 39–42 | T3 | close | idle lines not ranked at 0 %; no product picker; the target; drill-down to the old report | B | the ticket and the site's data, each | |
| 43–44 | T3 | close | the production entries; the release | O | — | |
| 45 | U | close | accept the declared safe commands | O | — | |
| 46 | U | close | the repository's checks fail until two scripts change | D | D128 fault 1 | |

| Class | Items | In parks | In closing notes |
|---|---|---|---|
| K | 2 | 0 | 2 |
| R | 3 | 1 | 2 |
| B | 10 | 4 | 6 |
| O | 25 | 9 | 16 |
| D | 6 | 1 | 5 |
| **All** | **46** | **15** | **31** |

The owner's 25 split as 13 production writes for three acts (one configuration write asked three times and answered
once; display writes asked four times and never answered; one report's production entries and release asked six times
and redirected to development), 2 sign-ins, 3 push or branch questions, 1 permission to run against production, and 6
preferences or facts nothing recorded. The owner answered each of the six parks in one message, and turned four
closing items into new asks; the rest stood.

## 5. Why each class happened

### 5.1 The owner's: work routed through production, and a go-ahead with nowhere to live

The repository's knowledge sends verification to production: *"A change here is delivered when it is merged, built,
released, DEPLOYED and then checked in a real browser against prod"*, and its testing document says *"Dev cloud is
frequently down … so testing against dev is unreliable. The team's existing pattern points the local dev app at PROD
APIs."* The prompt makes every production act the owner's. So each verification needed a yes, and each session
re-listed the yeses still open: three acts drew thirteen asks. The owner's preference, development first, was said
once on one quest, was recorded nowhere a later session reads, and contradicted the repository's documents. The code
held half of it: the local environment file points at development, and a plan document says *"already points at dev
cloud. Do not repoint it."* An earlier ticket's history held the rest: *"Dev only, never prod."*

### 5.2 The repository's documents tell the session to ask

Two documents require the owner's agreement: one before a shared component changes, one before a recorded product
decision reopens. Both were read, and the sessions did as they said. One of them stood against the owner's later
instruction (*"if any feature is missing we should be add it into common-report module instead"*): the first
attempt's push-back on the missing features, *"Each of these is a change to shared engine components that other reports
use"*, is that document's rule. Daoris cannot change those documents (D32); a conflict between one and the owner's
words is the owner's to settle, as a request to the repository.

### 5.3 A reading presented as a question

WSSETUP9 tells a session to take a reading the evidence leans to and say it in the closing note. Sessions did, under
headings such as *"Decisions for you"* and *"Decisions the owner may want to revisit"*, mixed with the production yeses.
Six of the ten B items sit in closing notes, five of them such readings; most rest on the ticket the quest linked, and
none quoted the ticket's line it rests on. The owner's own replies show which they thought unnecessary: *"the new OEE
has its calculation in the ticket … so you need to check this properly"*; *"since that's what local is pointing to"*.

### 5.4 Drift, and drift written into knowledge

The first attempt read the owner's answer as a formula (DRIFT1). The fresh attempt's ask left that answer out, but the
session found it in the first attempt's closed quest note, which said *"Per your answer"*, and in a document on the
first attempt's branch. It then wrote, in the repository's knowledge, that *"the owner also settled the calculation
earlier"*. Every later session that reads that document will meet the drifted reading as knowledge. The neighbour
repository's session asked the owner what the owner had said only to this repository, and got *"like I said we should
be using common-report"*.

### 5.5 Knowledge that answered, and was read

Both K items are findings listed for the owner rather than questions that stopped work. One document was right and
the session reported its symptom again; the other said *any* catalog entry would do, and a later session found that
the shell takes a bridge row's link from that entry, so the document was incomplete. A third, ambiguous one (item 3)
was written before the two counts it described could differ.

## 6. The fix, weighed

| Candidate | What it would have changed here | Verdict |
|---|---|---|
| Finish the set-up so the repository carries the doctrine | Nothing: the repository's own gate ran, and the pilot replaced its richer routing skill | Finish it for D128's reasons, not this one; WSSETUP14f's close shows the canon's discovery skill still reaches the repository's routing |
| Push the service's recall into the prompt (KNOW2b) | Nothing: every governing document was already read, and this install answers by words only | Stays held on D129's measurement |
| Check each question against the knowledge, a model judging | Knowledge alone: 2 of 46, neither blocking. With the ask's words and the ticket: up to 16 more (B and D) | Build as a review measured first (KNOWUSE2, KNOWUSE3); never answering in the owner's place until it classes as a person would |
| Name the repository's own index in "look before you ask" | No sign it would change any of the 46: sessions found the indexes through the repository's instructions | Folded into KNOWUSE1c's wording, at no extra cost |
| Make the session cite the source it checked | Each reading would carry the ticket's line or the document it rests on, and a question naming nothing checked shows itself | KNOWUSE1c |
| *From the evidence:* one go-ahead per act, held on the ask | Thirteen asks would have been three | KNOWUSE1a |
| *From the evidence:* standing answers per repository | The development-first answer would have reached every later session | KNOWUSE1b |
| *From the evidence:* an attributed reading is not a requirement | The fresh attempt would not have re-asked, or recorded, the misread answer | KNOWUSE1d, beside DRIFT1 |

**How a model is involved without naming one** (`model-decoupling`, D24). The review runs whatever passes it can. The
floor needs no model: a word search over the repository's knowledge and over the ask's words (D133's record) for each
item, shown beside it with the matched line and *words only*. The model tier is a short check session the driver
starts on the deployment's own harness and account, like an intake, handed the item, those hits and the ticket text
the session recorded. It returns, for each item, a source it quotes, *needs the owner*, or *a reading*, and the review
says which tier classed it. A document that quotes the owner counts only when the words are on the ask's record (§5.4).
Nothing is answered in the owner's place until a replay of recorded questions, these 46 first, shows the review classes
them as a person did.

The rows, in the backlog's shape:

- **KNOWUSE1a — a go-ahead is asked once and held on the ask** (service, driver, web). A session that needs the owner's
  yes for an act outside the repository records it as one pending go-ahead on the ask, named by the act, and later
  sessions are handed it and its answer instead of re-listing it. Three acts drew thirteen asks. Contract: D135 §2.
  Proof: service tests (a second request for one act joins the first; an answer reaches every later session); a driver
  test that a carry-on is handed it; the look.
- **KNOWUSE1b — a standing answer holds for the repository** (driver, modules; both doors, D50). The owner can keep an
  answer that outlives its quest (development writes allowed; test locally against development; production only on a
  yes) for that repository, and every later session there is handed it beneath its quest. The development-first answer
  was given once and read by no later session. Contract: D135 §3. Proof: driver tests (a claim, a resume, a carry-on and
  a follow-up handed it; gone once removed); the terminal door and the screen.
- **KNOWUSE1c — a closing note separates what needs the owner, and every item cites its source** (driver). The look
  names the indexes the repository's instructions name; a reading names the line it rests on (the ticket, a document, a
  code path) under its own heading, apart from what needs the owner. Two closing notes put nine readings to the owner
  as decisions, beside the production yeses, and none quoted the ticket's line. Contract: D135 §4. Proof:
  `AskAndWaitPromptTests`, each case failing first; a canary turn on the owner's install.
- **KNOWUSE1d — an attributed reading is not a requirement** (driver; beside DRIFT1). The person's words quoted in a
  document, a closed quest's note or a commit are a reading unless the ask's record holds them, and a session relying on
  one says so. The fresh attempt took a misread answer from a closed note and wrote it into knowledge. Contract: D135
  §5, D133 §1. Proof: prompt goldens, each failing first.
- **KNOWUSE2 — a question-review bench over recorded questions** (tools, docs). A script replays recorded questions
  through the no-model floor and a model tier on the deployment's harness, and scores each against the class a person
  gave it. A review shown to the owner must class as a person would first. Contract: D135 §6. Proof: the script's tests
  (fixture, scoring, tier line); an evidence note; the model tier the owner's run (D24).
- **KNOWUSE3 — the review beside the question** (driver, modules, web; held on KNOWUSE2). Beside each item a park or a
  closing note brings, the owner sees what may already answer it (a document's line, their own earlier words, the
  ticket's line) and which tier found it, and answers with one or in their own words. Contract: D135 §6. Proof: a silent
  service leaves the park as today; the tier line on every result; the look.

A request the owner may publish to the repository, which Daoris never writes into: correct the comparison report's
knowledge document where it records the misread answer as a decision, and reconcile the shared-component rule with the
instruction to add missing features to the shared module.

## 7. What this note does not cover

- **What a person would class each item as** is this note's reading of the session, the documents and the owner's later
  words; the owner did not class them. KNOWUSE2 is where a second reader is measured.
- **The ticket** is as the sessions quoted it; its attachments and history were not read here.
- **One session was still running**, and the earlier ticket's ten sessions were not read.
- **No session was re-run** with the wording the rows propose, so whether they lower the count is not measured.
- **Cost** is not counted here.
