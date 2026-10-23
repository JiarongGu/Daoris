# Work cut off by an account's limit resumes by itself (LIMITRESUME1, 2026-10-23)

**Standing:** proposed (D206). An Opus foundation reader reads it before it is built, because it extends D205's walk to a
driven line at a limit. Nothing is built here. Every line below was read at `9950b24cb`. LIMITGOON1's code is cited where it
is built: on a session branch (`5a2cd2a1f`, `5abcb801f`, `d67be99f1`) that is not merged at that commit. Paths are under
`src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they say otherwise.

The direction (the owner, 2026-10-23, through the quest that asked for this design): work cut off by a token limit must resume by
itself. The steward set the shape, which §3 designs:

1. a run ended by an account's limit is never failed and counts no strike;
2. where another account or another actor choice can take it now, it carries on there;
3. where none can, the line waits, shows the earliest reset, and resumes by itself at that reset. That is a full resume where
   the account that reset is the one its last part ran on; otherwise it is a briefed resume. The person's held words go with it;
4. conversations follow the same rule;
5. the page says it waits and when it resumes, with a press to go on now with another choice, and the stop.

## 1. The evidence, read again from the install's log

The machine log (`logs/2026-10-23.desktop.jsonl` on the install) for quest `1b78581ff053`:

| UTC | Event | What it says |
|---|---|---|
| 08:57:31 | `session.started b71795bc`, `account.rotated … to=account A carries=null` | the first part, on account A (a fourth was refused) |
| 09:07:14 | `account.limited account=account A hit=session until=12:22Z stated=true` | a session limit; its reset, 23:20 in the machine's zone, plus D125's 2-minute margin |
| 09:07:14 | `session.ended b71795bc state=failed` | |
| 09:07:31 | `account.rotated … to=account B carries=b71795bc`, `session.moved why=limit briefed=true` | a briefed carry-on on a second account |
| 09:07:46 | `account.limited account=account B hit=weekly until=2026-10-27T11:02Z`, `session.ended 98e6d7d3 state=failed` | ended at its first turn, 15 s in |
| 09:07:56 | `account.rotated … to=account C carries=98e6d7d3`, `session.moved why=limit briefed=true` | a third account |
| 09:11:59 | `account.limited account=account C hit=weekly until=11:02Z`, `session.ended dbf9a1ea state=failed` | |
| 09:11:57, 09:17:26 | `starts.waiting account=account C until=11:02Z` | the next start held: the look's wait, said once |
| 11:02:06 | `session.started 9967271d`, `account.rotated … to=account C carries=dbf9a1ea`, `session.moved continues=dbf9a1ea from=account C to=account C why=carry-on briefed=true` | at the earliest reset, a **briefed carry-on on the account the last part ran on** |
| 11:21:39 | `session.ended 9967271d state=completed` | |

So the quest's account of the evidence holds in its effect but needs three corrections, which the design rests on:

- **The line already came back by itself, at the earliest reset.** Two hours waited, held at start by the look, and 9967271d
  began 6 s after account C's cool-off ended. It was a **carry-on of the same line** (`continues=dbf9a1ea`), not a fresh
  retry. What it lost was the conversation. It ran on the **same account** as its last part, where the agent's own conversation
  still lay, yet was handed a brief (`briefed=true`). That is the full resume the shape's point 3 asks for, and the one the code
  never takes after a limit (§2.1).
- **Each part ended `failed` and counted no strike.** The ledger's state is `failed`, carrying the `limited` flag
  (`Sessions.cs:281-292`), and `ReadStrikes` leaves such a record out (`Service/ServiceClient.cs:2033-2034`). What the person saw
  as *failed* is the state word (`sessionState.failed`, *failed*). The trace page adds only *by an account's limit*
  (`Web/work/head/HowItCameToBe.tsx:651-662`, `work.trace.session.limit`). Each part was also announced as an *Ended* notice
  (`Quests/Domain/Attention.cs:248-254`, every conclusion the person did not ask for).
- **The wait had no reset on the line.** The look said the wait once, by account, in the log and the attention band
  (`work.attention.accounts.cooling`, *{{account}} cools until {{when}}*). The quest's page showed *waits for an account*
  (`work.waitsForAccount`, `Web/quests/QuestPage.tsx`) with no time and no press.

## 2. Today, by the code

### 2.1 A driven line

- **The limit is read from the door's failure only.** `AccountLimits.Read` (`Accounts/Usage/AccountLimits.cs:108-143`) reads the
  sentence and the reset: a time of day, a weekday and time, or a date (`When`, `:233-275`). It reads the zone only where it is
  an IANA name (`:187-191`) and adds a 2-minute margin (`:136`). The default is 60 minutes where nothing is read. It believes
  nothing past 8 days (`:79-94`).
- **The cool-off is per account, on disk.** `HarnessRoster.Limited` (`Accounts/Harnesses/HarnessRoster.cs:257-285`) writes a
  `CoolingEntry(Agent, Account, Until, Stated, Window, Seen, Session, …)` to `cooling.json` (`Accounts/AccountCooling.cs:22-27`,
  `:43-44`). `AccountWindows.Told` keeps a stated weekly reset (`HarnessRoster.cs:272-277`). An entry ends when its `Until`
  passes (`AccountCooling.cs:55-58`), by *Try now* (`End`, `:93-104`) or by a sign-in to that account (`SignedIn`, `:131-135`).
  D197 point 8 keeps it machine-wide.
- **The end.** `Observation.Conclude` makes any refused turn `failed` (`Sessions/Domain/Observation.cs:121-126`).
  `Driver.AccountLimited` (`Loop/Driver.cs:2815-2837`) replaces the agent's sentence with `account.cooling-window` or
  `account.cooling-no-window` (`AccountCooling.cs:280-284`) and returns `Limit`. `AdvanceAsync(…, limit:)` stores it
  (`Driver.cs:2453-2455`; `Daoris.Service.Core/Sessions/Sessions.cs:860`, `:875`, the `limited` column at `:545`).
- **Two gaps make a limit a strike.** First, only the protocol door's `turnFailed` is read (`Driver.cs:2813`, `:2819`), so a
  pipe-door harness's limit is a plain failure. Second, where `cooling.json` cannot be written the end is a plain failure
  (`:2826-2829`), deliberately: *the strikes bound it*.
- **The next start.** `Planner.CarriesOn` takes any `failed` last part (`Quests/Domain/Planner.cs:1935-1944`). A `Taken` quest is
  carried on (`:1329-1333`). An `Open` quest is started fresh (`:1311-1322`), or carried on in its tree where the cut-off left
  commits (RETRYWORK1, `:1307-1310`). A carry-on **walks** like a first start: *no context stays with an account*
  (`Driver.cs:1731-1735`). It is handed `ContextBriefs`' brief (`:2192-2196`, `:2309`), told its account changed only where the
  last part's account is still cooling (`:2313`).
- **The agent's own resume** is taken only where the person's words wait on the record or a park was answered
  (`DriverContinuations.ResumesOwn`, `Loop/DriverContinuations.cs:40-45`; `Driver.cs:1987-1995`). It is judged the same account,
  tree, kept id and door (`Conversations/Domain/Continuation.cs:170-185`; `Accounts/Rotation/ResumeAccount.cs`). Where that
  account cools, the words **wait for its reset** (`ResumeChoice.Waits`, `ResumeAccount.cs:72`; `Driver.cs:1743-1751`) and are
  not carried elsewhere, with *Go on in a new session* offered (D137 point 7). **A limit's end never takes this path**, which is
  why 9967271d was briefed.
- **No account left.** The start is held before any record opens (`Driver.cs:1753-1758`), its sentence `held.wait-every` or
  `held.wait-first`, or `held.cooling-account` for one account (`Accounts/Rotation/AccountRotation.cs:717-752`;
  `AccountCooling.cs:232-245`). The look classes it `Wait` (`Driver.cs:936`) and says it once (`Waits`, `:1087-1116`).
- **The wake is the look.** No timer is set at a cool-off's `Until`. Each look plans and walks again, and the account reads
  ready once `Until` has passed. The look runs every `PollSeconds`, 15 by default (`Platform/DriverConfig.cs:73`), paced in
  `Loop/DriverWatch.cs:789-796` (`Due`, `:836-837`). 15 s is inside the 2-minute margin.

### 2.2 A conversation

- **A turn refused by a limit is a failed turn**, read on the protocol door only. `ChatRunner` passes `Limited`
  (`Conversations/Chat/ChatRunner.cs:1257`), and the refused turn writes `DoorNotes.TurnNotTaken`, the cooling note and a turn
  with `StopReason = "error"` (`:2489-2521`). The conversation's process stays (`:1879-1880`), and every later word is refused
  the same way.
- **LIMITGOON1, built on its branch.** `LimitMove` moves the conversation at its turn's end to the walk's next account as a new
  part. It hands the conversation `ContextBriefs`' brief and the person's words from the limited turns (`LimitHeld.Of`), on both
  a repository's chat and Ask Daoris. Where the walk gives no other account, *the conversation stays* and *the next refused turn
  tries again*. Nothing is written for that case. LIMITGOON2 (another agent) is open (`TASKS.md:344`), designed in D205
  §3.5(e).
- **An ended conversation's words wait for its own account's reset** (`Conversations/Chat/ChatGoOn.cs:405-430`;
  `waits.account`, *… so your words wait to go on in it then*). **Nothing makes them go on at that reset.** A held chat goes on
  in four cases:
  - a word said to it (`ChatGoOn.cs:235-270`);
  - the person's *Go on* (`:282-290`);
  - the orphan sweep's end (`:119-150`);
  - the shell's come-up, once (`TakeUpAsync`, `:174`, called at `Daoris.Desktop.Modules/Loop/DriverLoop.cs:1164`).

  The page's held box says *it goes on with this then* (`work.conversation.held.cooling`), but draws it only for a driven record
  (`Web/work/conversation/ConversationView.tsx:191`; `Web/work/WorkFrame.tsx:1441-1445`).
- **A press with no account left** is refused in the wait's sentence and opens nothing (`ChatRunner.cs:606-607`).

### 2.3 Actors

D205 is accepted and none of its rows is built: no `ActorChoices` in the code. Until MODELROLE1's rows land, a line's *actor
choices* are one, its record's agent, and every rule below reads them so.

## 3. The design

### 3.1 The limited end: the ledger's flag, worded as its own end

**Where it lives: the ledger's existing `limited` flag on a `failed` record, with its `account.cooling-*` note; no new state.**
The state set is shared on the wire and a teammate's mirror carries it (D205 §3.5(c) kept `choice-not-offered` as `failed` for
the same reason). An older build or a teammate reads a new state as nothing it knows (`compatible-builds.md`). The flag already
names no account, travels with the record (`Sessions.cs:286-290`) and reaches the page (`Web/work/head/trace.ts:40`). What
changes is how each reader words and counts it:

- **Words.** Wherever a record's state is said (the session list, the head, the trace, a quest's sessions), a `failed` record with
  `limit` says *limited* / *额度用尽* in a quiet tone, never the failure's red. The trace keeps its note.
- **Notifications.** `AttentionWatch` leaves a limited end out of *Ended* (`Attention.cs:248-254`), as it leaves out one its park
  says. The line's own news says it once: *went on* where another account or choice took it, *waits until* where none did, and
  *resumed* at the reset.
- **Strikes.** `ReadStrikes` already passes it. Point 1's *never failed* is closed at its two gaps:
  - **The pipe door** gains its limit read: the native door's failed `result` read by the same table. D125 point 1 named that door
    *once recorded*, and an entry grows only with a recorded sentence held both ways. This is LIMITRESUME1f, after a recorded
    sentence.
  - **An unwritten cool-off stays a strike**, as today: with no `Until` the walk would offer the same account again at once. The
    strikes are what bound that spin, and D125 rejected a separate count.

### 3.2 The reset, kept per account

Unchanged in its store: `cooling.json`'s entry per agent and account (D125 point 2, D197 point 8). Its `Until` is the agent's
stated reset plus the margin, or 60 minutes with `stated: false`. The record keeps the reset the agent said in its note's
`until` value. **A line's wait is computed, never stored.** It is the earliest moment any account of any of its choices is ready:

- each choice's agent's accounts in the set (`AccountSet.Of`), each read by `HarnessRoster.Before` as the walk reads them;
- a signed-out account passed, as today;
- the earliest `Until` taken.

Today that is the hold's `Cooling` (`Driver.cs:1756`). Under D205 it is the minimum over the actor's effective order. An
unstated reset is said as one: *no reset was said; it tries again at {{time}}*.

### 3.3 The planner: what a limited line does at each look

A quest whose last part here ended limited is planned as **the line's resume**, `Taken` or `Open`. An `Open` quest's first part
held a conversation too, so it is no longer started fresh in a new tree (amending `Planner.cs:1311-1322` for this case alone,
RETRYWORK1's tree kept). At each look, in order:

1. **Its own account is ready, and its conversation resumes there → a full resume.** The judge is today's
   (`Continuations.Judge`, D189 point 2): the same agent, account and tree, a kept id and a door that resumes. Since D189 point 5
   moves nothing across, nothing is copied. The record goes back to `working` (D137 point 5's move out of an ended state). It is
   handed **Daoris's word** `limit.reset`, kept on its `said` through `DrivenInbox.KeepOrHoldAsync` (`session-words.md`) under
   the reset's key so a retry keeps one. Its words: *Your account's limit has reset. Go on with the work where you stopped.*
   It is said in Daoris's one English, then the person's held words, as theirs. It runs through the path the person's words take
   today (`DriverContinuations.ContinueAsync`). It is never briefed, because the conversation is whole. A refused resume falls to
   step 2, said `account`, as today. **Own first:** where its own account and another are both ready at the same look, its own
   is taken.
2. **Another account of its choice, or (§3.6) another choice, is ready → a briefed resume.** That is today's carry-on (LIMITGOON1
   and D205 §3.5(a) for a conversation): a new part, `from` the last, why `limit`, handed the brief. **The person's held words go
   with it**, after the brief and as theirs, taken off the old record by their ids (`BriefedMove`'s `SayHandedOver` order). This
   changes `ResumeChoice.Waits` for a limited record. Today the person's words pin a line to its cooling account (D137 point 7);
   now they go where the line goes.
3. **None → the line waits**, held at start as today, with a sentence of its own, `held.limit-waits`. It names the earliest
   reset (§3.2), the account and, under D205, the choice that resets. At the first look past that moment, step 1 or step 2
   takes it, as the account that freed is its own or not.

**Strikes** (point 1): none of the three counts one. A full resume or a briefed resume refused again by a limit ends limited
again. A limit with no stated reset tries again every 60 minutes (the `cooloff` setting); it never parks.

### 3.4 The wake: the look, and nothing beside it

- **A driven line** needs nothing new. The held start carries its `Until`, and the look's pace (15 s) plans it again. The
  install's 9967271d began 6 s after its reset this way.
- **A conversation** has no look today. **The look gains a stage beside its plan, `limits`** (`beside: true`, as `tidy` and
  `follow` run, `DriverWatch.cs:965`, `:992`). It calls the chat runner only where a cool-off of some account **ended since the
  last look**, a comparison of two reads of `cooling.json` and nothing more. The runner then takes each conversation of this
  machine held by a limit (§3.5) whose wait has come, in the record's order. It is not a second loop and not a timer. At the
  come-up, `TakeUpAsync` already reads them.

### 3.5 Conversations (point 4)

A conversation, a repository's chat or Ask Daoris, whose turn met a limit:

- **Goes on now where it can**: LIMITGOON1's move to another account of its agent, then D205's (e) to its actor's next choice
  on another agent (Ask Daoris included, as (e)'s named exception). Both are unchanged.
- **Else waits, held.** LIMITGOON1's *stays, and the next refused turn tries again* becomes a written wait. The person's words
  from the limited turns (`LimitHeld.Of`) and every word said while it waits are kept on the record's `said` through `ChatTurns`
  (`KeepAsync`). They are **not** dispatched to a process whose account is cooling. The conversation shows `held.limit-waits`
  with its reset. A word said to it is answered *held until {{time}}*, never refused turn by turn.
- **At the wake**, as §3.3: its own account reset first → **the same conversation goes on**. That is the next turn on the same
  process where it still runs, else the agent's own resume on that account (`ChatGoOn`'s path, D137 §2.2). Each is handed
  Daoris's word, then the held words. Otherwise another account or choice frees first → LIMITGOON1's or (e)'s briefed move with
  the held words.
- A conversation on the person's **pick** waits for its own account alone, as D205 §3.5(d) has it, and §3.6's press offers the
  rest.
- Ended conversations whose words wait for their account (`ChatGoOn.cs:419-423`) are woken the same way. That makes true what
  `waits.account` already says.

### 3.6 Choices: D205's walk reaches a driven line at a limit

D205 §3.5(a) holds a driven line on its asked choice and offers the next only by the person's press. Its round-4 note kept (e)
to conversations. The owner's direction of 2026-10-23, recorded in D205's acceptance note, was that *a driven line's next start
walks its choices*. This design takes that: **a driven line whose last part ended limited walks its actor's effective order at
step 2**, choice-major, every account of the out choice passed, as (e) does for a conversation. It carries the brief and the
held words, said once (*Went on with Codex · account default, Build's choice 2: Claude Code's accounts are at their limit until
14:05*). A **pick**, a **handed quest's named agent** and an actor *as before* (one entry) do not walk: they wait. The extension
is limited to an end by a limit. A refusal after the prompt, a content refusal or an uncertain send is the failed turn it is
(D205 §4.3). This is the one change to D205's walk, so the foundation read is owed.

### 3.7 The page (point 5)

The line's head and its foot, the session's row, and the quest's page all say the same sentence where the line waits:

| Key | en | zh |
|---|---|---|
| `work.limit.waits` | Waiting for a limit to reset: it resumes by itself at {{time}}, on {{account}}. | 正在等待额度重置：将于 {{time}} 在 {{account}} 上自动继续。 |
| `work.limit.waitsChoice` | Every choice of {{actor}} is at its limit: it resumes by itself at {{time}}, with {{agent}} on {{account}}. | {{actor}} 的所有选择都已达额度上限：将于 {{time}} 由 {{agent}} 在 {{account}} 上自动继续。 |
| `work.limit.noReset` | No reset was said: it tries again at {{time}}. | 未告知重置时间：将于 {{time}} 再试。 |
| `work.limit.goOnNow` | Go on now with… | 现在换个继续… |
| `work.limit.stop` | Stop | 停止 |
| `sessionState.limited` (the word for a `failed` record with `limit`) | limited | 额度用尽 |

- **The notes**, coded in `NoteCodes` with `noteLines.ts` values:
  - `limit.resumed`: *Its account's limit reset at {{until}}; it went on in its own conversation.* / *账户额度已于 {{until}} 重置，它在原对话中继续。*
  - `limit.waits`: the hold, in `held.limit-waits`'s words.
  - The moves keep `moved.on` / `moved.from`.
- ***Go on now with…*** opens D163's hand-over offer, listing what can run now: another agent or account the person may choose,
  the actor's own choices first. It is the person's press, so it may name a maker outside the actor (D205 §4.2's offer). It is
  the only way a pick moves.
- ***Stop*** is the existing stop of the line. Its words wait as a stopped line's do.
- **Not a row in *What needs you*.** The line settles by itself, so `person-waits.md`'s *listed only with an act* keeps it on
  its own page. The attention band's per-account line stays as it is.
- **The glossary** gains *limited* / 额度用尽, beside *cooling*, with *failed* in its avoid list for this state. It keeps 上限 for
  a window's limit, as `note.account.cooling-window` has it.
- `i18n:check`, `names:check --strict` and `HelpCoverageTests` (the two presses' hooks) hold the words and the hooks.

## 4. What it amends, each by a dated note on acceptance

- **D125** point 3: a limit's end is worded *limited*, never *failed*, and announces no *Ended*. Point 6: a held start shows its
  reset on the line and resumes at it by the look.
- **D197** point 8: the cool-offs also say when a limited line resumes, read across the set. The store and its rules are
  unchanged.
- **D205** §3.5(a) and its round-4 note: a driven line at a limit walks the actor's choices (§3.6). (e)'s *nothing left* becomes
  §3.5's written wait and wake.
- **D130** §16.3's *a driven start drops the kept account* and the goal's point 6 (*a carry-on carries no conversation*): a
  limited line's own account, once ready, is preferred and resumed (§3.3 step 1).
- **D189** point 2: a limited part whose own account resets resumes its own conversation, not a new part. Point 3: *a carry-on
  after a limit* is a new part only where another account or choice takes it.
- **D137** point 7, which the quest did not name and this design reaches: words held for a cool-off go with a briefed resume
  where another account or choice can take them now, and go on by themselves at the reset.

## 5. Rejected

- **A new ledger state `limited`**: the state set is shared with teammates and older builds, and the flag already exists and
  travels.
- **A timer per reset, or a loop for conversations**: the look already runs inside the margin. A stage that asks only when a
  cool-off ended costs one file read.
- **Keeping a briefed carry-on on the same account** (today): it throws away the exact conversation the account still holds.
- **Copying the transcript to another account's home**: D189 point 5, D125's rejection.
- **Holding the line on the person's words** (D137 point 7 as written): it keeps work waiting for hours while another account
  could take it.
- **A *What needs you* row**: nothing waits for the person.
- **Moving a pick or a handed agent by itself**: the person named it.
- **Counting limits toward a park**: D125.

## 6. Rows, in build order, with their proofs

| Row | What | After | Proof |
|---|---|---|---|
| LIMITRESUME1a | §3.1: a limited end is worded *limited*, quiet, in every list and head; no *Ended* notice; the line's own news | — | `AttentionTests`: a limited conclusion raises no *Ended*; the page's state-word tests in en and zh; `i18n:check`, `names:check --strict` |
| LIMITRESUME1b | §3.3 for a driven line: an `Open` or `Taken` quest's limited last part planned as the line's resume; own account first with Daoris's word and the held words; else a briefed carry-on with the held words; else `held.limit-waits` with the earliest reset | 1a | `PlannerTests` (a limited `Open` quest is not started fresh; own ready before another); the driver's stand-in agent: limited on account A, B free → a briefed part on B with the held words; all cooling → held, then at A's reset a resume of the same conversation id with `limit.reset` and no brief (the install's case); a resume refused → briefed, said `account`; no strike in any (`ReadStrikes`) |
| LIMITRESUME1c | §3.4-§3.5 for conversations: the look's `limits` stage; LIMITGOON1's stay written as a wait; words kept, not dispatched; the wake's own-account resume or move; ended chats' waiting words woken | LIMITGOON1 merged | `ChatGoOnTests`/`LimitMove` tests: a chat and Ask Daoris all cooling, the stage waking each once at the reset, the same process taking the held words with Daoris's word; another account freeing first → the briefed move; a word said while waiting kept once; the stage asking nothing where no cool-off ended (held clock, `held-clocks.md`) |
| LIMITRESUME1d | §3.7: the sentence, *Go on now with…* and *Stop* in the head, foot, row and quest page; notes `limit.resumed`, `held.limit-waits`; glossary | 1b, 1c | page tests and stories in en and zh; `HelpCoverageTests`' rows for both presses; `note.test.ts` |
| LIMITRESUME1e | §3.6: a driven line at a limit walks its actor's choices; the wait's earliest reset over the effective order | MODELROLE1 rows c and e | stand-in agents: every account of choice 1 cooling → choice 2 taken with the brief, said once; a pick and an *as before* actor wait; the reset shown is choice 2's where it is earlier |
| LIMITRESUME1f | §3.1's pipe-door read of a limit | a recorded native-door sentence | `AccountLimits` table rows held both ways; a pipe-door limit concludes limited, no strike |
| LIMITRESUME1g | the install: a line held across a real reset resumes its own conversation, seen in the window | 1a-1d | the parent's install proof, the machine log's `session.moved` absent and `limit.resumed` said |

## 7. What the checks do not cover

This is a document; nothing ran but the census and the documentation checks. The log lines in §1 were read from the install's
machine log. The code was read at `9950b24cb`, and LIMITGOON1 was read on its branch. Two claims were not run, and their rows
must prove them: that a Claude Code ACP conversation's process, held across its reset, accepts the next prompt; and that a resume
after a weekly limit finds its conversation in the account's home.
