# Work cut off by an account's limit resumes by itself (LIMITRESUME1, 2026-10-23)

**Standing:** proposed (D206). An Opus foundation reader reads it before it is built, because it extends D205's walk to a
driven line at a limit. Nothing is built here. Every line below was read at `9950b24cb`. LIMITGOON1's code is cited where it
is built: on a session branch (`5a2cd2a1f`, `5abcb801f`, `d67be99f1`) that is not merged at that commit. Paths are under
`src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they say otherwise.

**Round 2** (2026-10-23) answers round 1's Opus foundation read (`docs/2026-10-23-limit-resume-reads.md`: 41 facts checked, 7
wrong as F1-F7; B1-B7 blocking, N1-N15 not; *another round*). The code is read again with LIMITGOON1 merged (`2d2aedca0`), so
its citations are now main's. The reader's line numbers held; where this round cites new ones, they are main's. Every finding is
taken or answered in §8's table, and where the answer changes the text, the section carries it marked *(round 2, Bn)*. New
sections: §3.8 (the other kinds of run, B5), §3.9 (an update's close, B6), §3.10 (a conversation that kept no id, B7).

**Round 3** (2026-10-24) answers round 2's two reads (`docs/2026-10-23-limit-resume-reads.md`): Codex's (12 findings, 8 blocking,
4-11; *another round*) and the Opus foundation read (37 facts, F8-F13 wrong; B8 and B9 blocking, N16-N29 not; *accept after B8
and B9*). The code is read again on main at `05328a5c9`. Since `2d2aedca0` only APIREFUSAL1 and MODELROLE1a touched the driver,
which shifted `Driver.cs` past `:2417` by two lines and `Observation.cs` past `:99`; this round cites main's lines. Where the two
reads agree (B8 with Codex 4, B9 with Codex 8, N16 with Codex 5, N22 with Codex 6), the agreed fix is taken. Every finding is
taken or answered in §9's table, and each section it changes carries it marked *(round 3, …)*.

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

*(Round 2, N14.)* The order at 09:11 is as the log has it. The wait said at 09:11:57 followed another session's limit on account C
at 09:11:56 (`account.limited session=40d9852f`), so account C was already cooling when this line's own limit came at 09:11:59.

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
  (`work.waitsForAccount`, `Web/quests/QuestPage.tsx`) with no time and no press. *(Round 3, Codex 1.)* That is what the
  install showed, not what the source always renders: where a held start's `waitsFor` reaches the page, the quest page already
  says the account and its time through `sittingSentence` (`Web/quests/QuestPage.tsx:697`, `Web/data/signals.ts:236-243`,
  `work.sitting.waitsFor`). What §3.7 adds is one sentence on the line itself, the same everywhere, saying which resume comes,
  with its presses.

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
- **The end.** `Observation.Conclude` makes a refused turn of an unfinished `Open` or `Taken` run `failed`
  (`Sessions/Domain/Observation.cs:125-130`); a wait or a submission the run established is read before it (`:104-119`,
  *round 3, Codex 2*).
  `Driver.AccountLimited` (`Loop/Driver.cs:2815-2837`) replaces the agent's sentence with `account.cooling-window` or
  `account.cooling-no-window` (`AccountCooling.cs:280-284`) and returns `Limit`. `AdvanceAsync(…, limit:)` stores it
  (`Driver.cs:2453-2455`; `Daoris.Service.Core/Sessions/Sessions.cs:860`, `:875`, the `limited` column at `:545`).
- **Two gaps make a limit a strike.** First, only the protocol door's `turnFailed` is read (`Driver.cs:2813`, `:2819`), so a
  pipe-door harness's limit is a plain failure. Second, where `cooling.json` cannot be written the end is a plain failure
  (`:2826-2829`; main's `:2828-2831`), deliberately: *the strikes bound it*.
- **The next start.** `Planner.CarriesOn` takes any `failed` last part (`Quests/Domain/Planner.cs:1935-1944`). A `Taken` quest is
  carried on (`:1329-1333`). An `Open` quest is started fresh (`:1311-1322`), or carried on in its tree where the cut-off left
  commits (RETRYWORK1, `:1307-1310`). A carry-on **walks** like a first start: *no context stays with an account*
  (`Driver.cs:1731-1735`). It is handed `ContextBriefs`' brief (`:2192-2196`, `:2309`), told its account changed only where the
  last part's account is still cooling (`:2313`).
- **The agent's own resume** is taken only where the person's words wait on the record or a park was answered
  (`DriverContinuations.ResumesOwn`, `Loop/DriverContinuations.cs:40-45`; `Driver.cs:1987-1995`). It is judged the same account,
  tree, kept id and door (`Conversations/Domain/Continuation.cs:170-185`; `Accounts/Rotation/ResumeAccount.cs`). Where that
  account cools, the words **wait for its reset** (`ResumeChoice.Waits`, `ResumeAccount.cs:72`; `Driver.cs:1743-1751`) and are
  not carried elsewhere, with *Go on in a new session* offered (D137 point 7). **A limit's end with no words waiting never takes
  this path**, which is why 9967271d was briefed. *(Round 3, Codex 2.)* `ResumesOwn` does not exclude a limited record: where the
  person's words wait on it, they take this path today.
- **No account left.** The start is held before any record opens (`Driver.cs:1753-1758`), its sentence `held.wait-every` or
  `held.wait-first`, or `held.cooling-account` for one account (`Accounts/Rotation/AccountRotation.cs:717-752`;
  `AccountCooling.cs:232-245`). The look classes it `Wait` (`Driver.cs:936`) and says it once (`Waits`, `:1087-1116`).
- **The wake is the look.** No timer is set at a cool-off's `Until`. Each look plans and walks again, and the account reads
  ready once `Until` has passed. The look runs every `PollSeconds`, 15 by default (`Platform/DriverConfig.cs:73`), paced in
  `Loop/DriverWatch.cs:789-796` (`Due`, `:836-837`). The default 15 s is inside the 2-minute margin. *(Round 2, F7.)*
  `pollSeconds` is the machine's setting, so the wake's latency is that setting, not 15 s. Nothing below depends on it being
  inside the margin: a later look still finds the account ready, and the line resumes then.

### 2.2 A conversation

- **A turn refused by a limit is a failed turn**, read on the protocol door only. `ChatRunner` passes `Limited`
  (`Conversations/Chat/ChatRunner.cs:1257`), and the refused turn writes `DoorNotes.TurnNotTaken`, the cooling note and a turn
  with `StopReason = "error"` (`:2489-2521`). The conversation's process stays (`:1879-1880`), and every later word is refused
  the same way.
- **LIMITGOON1, now merged** (`2d2aedca0`). `LimitMove` moves the conversation at its turn's end to the walk's next account as a new
  part. It hands the conversation `ContextBriefs`' brief and the person's words from the limited turns (`LimitHeld.Of`), on both
  a repository's chat and Ask Daoris. Where the walk gives no other account, *the conversation stays* and *the next refused turn
  tries again*. Nothing is written for that case. LIMITGOON2 (another agent) is open (`TASKS.md:344`), designed in D205
  §3.5(e).
- **An ended conversation's words wait for its own account's reset** (`Conversations/Chat/ChatGoOn.cs:405-430`;
  `waits.account`, *… so your words wait to go on in it then*). **Nothing makes them go on at that reset.** A held chat goes on
  in four cases:
  - a word said to it (`ChatGoOn.cs:235-270`);
  - the person's *Go on with this answer* (`:282-290`, `AnswerGoesOn`), the answers-alone bound's press, not a general *Go on*
    *(round 2, F6)*;
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
  `limit` says *limited* / *额度用尽* in a quiet tone, never the failure's red. The trace keeps its note. *(Round 2, N6.)* The
  readers beyond those four: the terminal's `daoris-driver sessions` (D50's parity), Ask Daoris's session tools, Overview's
  counts and the session list's filters, each reading the flag the same way. `SessionEnded` carries no limit flag
  (`Driver.cs:2494-2497`), so it gains one, as D205 N5 added `NotOffered`, for `AttentionWatch` to read. *The line's own news* is
  a `LookNews` line of the look's (`look.said`), not a notice.
- **Notifications.** `AttentionWatch` leaves a limited end out of *Ended* (`Attention.cs:248-254`), as it leaves out one its park
  says. The line's own news says it once: *went on* where another account or choice took it, *waits until* where none did, and
  *resumed* at the reset.
- **Strikes.** `ReadStrikes` already passes it. Point 1's *never failed* is closed at its two gaps:
  - **The pipe door** gains its limit read: the native door's failed `result` read by the same table. D125 point 1 named that door
    *once recorded*, and an entry grows only with a recorded sentence held both ways. This is LIMITRESUME1f, after a recorded
    sentence.
  - **An unwritten cool-off is still a limited end, never a strike** *(round 3, Codex 10; round 2 kept it a strike)*. Recognising
    the limit and keeping its retry are two things, and only the second failed. `AccountLimited` (`Loop/Driver.cs:2817-2838`)
    reads the sentence (`AccountLimits.Read`) before it writes, so on an `IOException` at the write (`:2828-2832`) it still
    returns `Limit = true` with the read `Until`. That entry is kept **in memory** by `AccountCooling` as an overlay the walk and
    §3.2's wait read as they read the file, and each look tries the write again until it lands or its `Until` passes. The spin is
    bounded by the overlay, not by strikes: the account is passed until its `Until`, as a written one is. The machine log says
    `account.cooling-unkept` once per entry, and the attention band says *Daoris could not keep this account's cool-off* beside the
    account, since it is the machine's problem, not the line's. A shell that ends with the overlay unwritten loses it, so the first
    attempt after the come-up meets the refusal again and is limited again, never a strike: one attempt per come-up, which D125
    point 3's bound allows (each attempt meets a cool-off that has ended, here by being lost).

### 3.2 The reset, kept per account

Unchanged in its store: `cooling.json`'s entry per agent and account (D125 point 2, D197 point 8). Its `Until` is the agent's
stated reset plus the margin, or 60 minutes with `stated: false`. The record's note keeps that `Until` in its `until` value: the
reset plus the margin, or the default, never the agent's bare reset (`AccountCooling.cs:280-284`, `AccountLimits.cs:119`, `:136`;
*round 2, F4*). The page therefore says *ready again at*, never *reset at* (§3.7). **A line's wait is computed, never stored.** It is the earliest moment any account of any of its choices is ready:

- each choice's agent's accounts in the set (`AccountSet.Of`), each read by `HarnessRoster.Before` as the walk reads them;
- a signed-out account passed, as today;
- the earliest `Until` taken.

Today that is the hold's `Cooling` (`Driver.cs:1756`). Under D205 it is the minimum over the actor's effective order. An
unstated reset is said as one: *no reset was said; it tries again at {{time}}*.

*(Round 2, N8, N9.)* The computed wait uses the walk's own filters: an account kept for conversations or outside the start's
scope (`ResumeAccount.cs:31-36`) is never a driven line's, so the page never names it. The own sign-in has no `{{account}}`, and
its sentence says *your own sign-in*. `AccountCooling.Cool` replaces an entry whole (`AccountCooling.cs:84-90`), so a later,
shorter reading on the same account (a session window, or the unstated default from a conversation still running there) could
shorten a stated weekly reset and waste an attempt. It keeps the later of a stated `Until` and the new one; an unstated reading
never shortens a stated one.

### 3.3 The planner: what a limited line does at each look

A quest whose last part here ended limited is planned as **the line's resume**, `Taken` or `Open`. An `Open` quest's first part
held a conversation too, so it is no longer started fresh in a new tree (amending `Planner.cs:1311-1322` for this case alone,
RETRYWORK1's tree kept). At each look, in order:

The planner plans it as it plans any cut-off today: a `CarryOn` whose `Resumes` is the limited record (`Planner.cs:1329-1333`,
`:1507-1540`). **Every step below is decided inside that carry-on's own start, never beside the look** *(round 2, B1)*. A word
said beside the look would be planned only at the next look, and the same look would already have planned the carry-on, whose
walk can land briefed on the own account just freed: the install's 9967271d again (`Planner.cs:1287-1291`, `Driver.cs:1731-1735`).

0. **A press of the person's since the limited end outranks the resume by itself** *(round 2, B4; round 3, N17, F13)*. Most of
   these are the planner's verdicts, which a start never sees, and the planner reads them in its own order
   (`Planner.cs:1278-1300`): a pause of its work first (PAUSE1b, `:1278-1282`), then the person's words written to the record,
   planned as `GoOn` (`:1287-1291`, which goes on in the own conversation, its own account first and, cooling, by step 2 below),
   then a stop. A stop cannot hold a limited line: a `failed` record is never `PersonStopped` (`Planner.cs:254`), and words
   release a stop anyway. In the start itself, step 0 is one thing: a hand-over made **from the limited record**
   (`HandedFrom`, `Planner.cs:1403-1404`), which names the agent and account the line goes on with. An older hand-over is the
   line's agent, not a press since the end. *Go on in a new session* is read where it is today, by `ResumeAsync`
   (`NewSessions.Covers`, `Driver.cs:1724-1726`), for a `GoOn`. Any of these decides the start, and step 1 is not tried.
1. **Its own account is ready, and its conversation resumes there → a full resume.** In `Driver.cs` at `:1716-1736`, beside
   `GoOns.ResumesOwn`, a start that is `!start.GoesOn && start.Resumes is { Limit: true }` *(round 3, N18: a `GoOn`'s `Resumes`
   is the same limited record, `Planner.cs:1459-1473`)* (a `PriorSession` already carries the flag, `Planner.cs:264`)
   asks `ResumeAsync` for its record's own account, as a resume does (`:1724-1726`), never the walk. It goes on only where:
   - that account is ready, its cool-off ended and, *(round 2, B2)*, its maker's reading asked after the cool-off as the walk asks a
     just-reset account (`ProbeWhy.Limit`, `HarnessRoster.cs:1895-1910`), a reading still out being treated as cooling. USAGEONE1's
     skip of the probe for a resume (`:1070-1077`) stays for the person's words; this start asks. *(Round 3, N21.)* There is a
     reading only for an agent whose door carries none of its windows (`:1070-1079`, the freshen's own condition). For one whose
     door carries them (Claude Code), there is nothing to ask, and the attempt is the probe: refused by the limit, it writes a new
     cool-off and the bound below holds;
   - and the judge passes (`Continuations.Judge`, D189 point 2: the same agent, account and tree, a kept id, a door that resumes),
     asked as if words waited, since the judge answers `ended` with none (`Continuation.cs:217`);
   - *(round 3, Codex 7)* and its door is one whose resume is known to fail closed: the protocol door, which refuses a missing id
     with `resource_not_found` before any prompt (`Agents/Acp/Acp.cs:2606-2612`). The native door takes step 1 only once row 1f
     has recorded what its `--resume` of a missing id does (§3.5); until then a native-door line goes by step 2, briefed, which is
     today's behaviour and never blind.

   Then it keeps **Daoris's word** on the ended record **through the say door** *(round 2, F1)*, as `RebaseTelling` does for an
   ended session's own conversation (`Trees/Landing/RebaseTelling.cs:16-28`): `service.SayAsync(session, text, from: daoris, key)`.
   The key is `DaorisKeys.LimitReset(session, until)`, `limit-reset/{session}/{until}` from the cool-off's `Until`, so a retry at a
   later look keeps one; `DaorisKeys.Read` names no quest fact for it, as for `Rebase`, so the word waits as it is. Its words:
   *Your account's limit has lifted. Go on with the work where you stopped.* Since D189 point 5 moves nothing across, nothing is
   copied, and the record goes back to `working` (D137 point 5). It is never briefed, because the conversation is whole. **Own
   first:** where its own account and another are both ready at the same look, its own is taken, since this is asked before the
   walk.

   **Where the word is kept** *(round 3, B8 item 4, Codex 4)*. The own account and the judge are asked at `:1716-1736`, and the
   start is marked a **limit resume**, an intent of the start's own (`LimitResume`), separate from any words waiting. The word is
   kept only **just before `ContinueAsync`** (`Driver.cs:1987`), after every hold between (access, tree, replay and trust,
   `:1764-1978`). A hold before it keeps nothing, so a held start leaves no word on the record. After the keep the record is
   re-read (`GoOns.HandedOnRecordAsync`), the start set going on with it, and `ContinueAsync` runs as for the person's words.

   **The order of what it is handed** *(round 2, B3; round 3, N19)*. Step 1 is planned only where no person's word waits: where
   one waits, the planner plans `GoOn` (`Planner.cs:1287-1291`) and step 1 is not reached. So the full resume comes two ways, and
   each carries the limit word:
   - **step 1** (no person's word waits): Daoris's word alone;
   - **a `GoOn` on a limited record whose own account resumes it**: the same keep, at the same place before `ContinueAsync`, under
     the same intent, so the person's words go first, oldest first, as theirs (`said` keeps keep-time order,
     `DriverContinuations.cs:254`), and Daoris's word last. That is the shape's *the person's held words go with it*.
   The word is written for either place: it tells the agent to go on, after whatever the person said.

   **On a refusal** *(round 2, B1, F2; round 3, B8 items 1-3, F8, F10, Codex 4)*. A start with the `LimitResume` intent whose
   resume is refused, before the wire (the judge on the account it landed on, `resume.Elsewhere`) or on it (`gone`, `unkept`,
   `exited`, `refused`, put back as it ended, `DriverContinuations.cs:494-511`):
   - **any code but `elsewhere`**: the word is taken off by id (N24 below), and the start goes on **in the same start** as the
     `CarryOn` that was planned: `start` restored to the planned carry-on (`GoesOn` false, `Resumes` the limited record), the walk
     through `CarriedOnAsync` (`Driver.cs:2051-2077`) and the brief, the person's words, where the `GoOn` way had any, after the
     brief as theirs. The intent is what passes the two guards that stop a wordless fallback today: the `TakenBackFirst` return
     (`:2008-2012`) and the Daoris-only `CannotGoOn` (`:2039-2043`). Both stay as they are for every start without it. The
     refusal's own code is said (`limit.resumeRefused`, §3.7), never `account`. Nothing is left on the record, so the next look
     plans nothing more for it: the line has a new part;
   - **`elsewhere`** (another client holds the conversation): the word is **kept and marked** `elsewhere`, as `CannotGoOn` marks
     today's words (`DriverContinuations.cs:767-782`, `GoOnMarks.Mark`), so `HeldForPerson` (`Planner.cs:1359-1361`) holds the
     line for the person and it is said once. Taking it off would leave no word to mark (`GoOnMarks.cs:57-58`) and the line would
     spin. The person's press (§3.7) takes the marked word off with the rest.

   **Every conclusion takes the word off** *(round 3, B8 item 3)*. A resumed run's conclusion takes Daoris's limit word, and §3.9's
   restart word, off by id along with the run's words, whatever the conclusion, limited included. The always-`Wait` rule below
   applies to the person's words alone. So a word kept by step 1 never reaches a later `GoOn`, a fallback, or a second key: the
   next attempt keeps its own.

   **The take-off door** *(round 3, N24, F9)*. It is `ServiceClient.TakenAsync(session, [id])` (`ServiceClient.cs:556-582`), whose
   `by` names the session that took the words, here the limited record's own. There is no *by Daoris*; the service keeps only the
   person's taken words on the ask (`Daoris.Service.Http/Program.cs:1511-1522`), so a Daoris word taken off leaves nothing there. On
   the record a Daoris word kept and taken off unsent shows nowhere as sent: the conversation shows the refusal's note
   (`limit.resumeRefused`) in its place.
2. **Another account of its choice, or (§3.6) another choice, is ready → a briefed resume.** That is today's carry-on: a new part,
   `from` the last, why `limit`, handed the brief, through the driver's fallback (`Driver.cs:2042-2047`, `CarriedOnAsync`)
   *(round 2, F3: not `BriefedMove`, which is the conversations' seam)*. **The person's held words go with it**, after the brief
   and as theirs, taken off the old record by their ids as the fallback takes them today. This changes `ResumeChoice.Waits` for a
   limited record. Today the person's words pin a line to its cooling account (D137 point 7); now they go where the line goes.
3. **None → the line waits**, held at start as today, with a sentence of its own, `held.limit-waits`. It names the earliest
   reset (§3.2), the account and, under D205, the choice that resets. At the first look past that moment, step 1 or step 2
   takes it, as the account that freed is its own or not.

**The bound, inside D125** *(round 2, B2)*. None of the three counts a strike, and nothing counts limits toward a park (D125
rejects a separate count, `docs/decisions/D125.md:62`). The bound is **one attempt per cool-off**: steps 1 and 2 are tried only
once the cool-off that held the line has ended, and an attempt refused by a limit writes a new cool-off (`AccountLimited`), so
the next attempt waits for that one. That is D125 point 3's *never carried on into the same refusal* (`D125.md:23-24`): each
attempt meets a cool-off that has ended. *(Round 3, Codex 10.)* A cool-off that could not be written is held in memory (§3.1),
which bounds a spin with no strike. *(Round 3, B8.)* A resume refused with no limit met (`gone`, `exited`, `refused`) writes no
cool-off, so it is not bounded by one: it is bounded by going on in the same start, briefed, which leaves nothing for the next
look to try again; `elsewhere` is held for the person.

- **Limited ends always wait.** TURNFAIL1 parks a resumed run's words on its second failed turn running (`FailedTwice`,
  `Sessions/Domain/Observation.cs:450-476`, applied at `DriverContinuations.cs:556-566`), and it reads a limit's failure too, so
  two limited full resumes in a row would hold the words for the person. `FailedTurnWords.Judge` takes the conclusion's `limit`
  flag (`AccountLimited`'s second value, `:554`): a limited conclusion is always `WordsAfter.Wait`, never `Held`. The words stay
  waiting, unmarked, and go with the next attempt. D137's TURNFAIL1 note is amended (§4). *(Round 3, N20.)* The limited wait's
  line has a code of its own, `ended.limit-words-wait` (*Your words wait for the account's limit to lift.*), never
  `ended.words-wait`: both `FailedTurnWords.Judge` (`Observation.cs:483`) and `Continuations.Unstarted` (`Continuation.cs:336-338`)
  read `ended.words-wait` as a first failure, so after one limited end a single ordinary failure would mark the words *failed
  twice*. Neither reads the new code. *(Round 3, B8 item 3.)* The rule is for the person's words; Daoris's word comes off at every
  conclusion (step 1).
- **A reset that does not lift the refusal is said on the page.** An admin's spend limit can outlast the session window's reset
  the sentence names (`AccountLimits.cs:304`, `:316`, `:320`). Where an attempt is refused at its first turn with the same `hit`
  within the margin after its account's cool-off ended, the line and the attention band say *its last reset did not lift the
  limit* (`limit.notLifted`, §3.7). It still waits for the next cool-off and never parks; *Go on now with…* is the way out.
- A limit with no stated reset tries again every 60 minutes (the `cooloff` setting), each attempt one per cool-off as above.

*(Round 2, N10.)* A further part can join the waiting line's tree (SESSONE1d's `Joining`, `Planner.cs:1588-1634`), since nothing
live holds it while it waits. The full resume then waits behind that part or resumes into a changed tree, as today's carry-on
does. The brief's and Daoris's word say nothing of it; the resumed agent reads its tree as it finds it.

### 3.4 The wake: the look, and nothing beside it

- **A driven line** needs nothing new. The held start carries its `Until`, and the look's pace (15 s) plans it again. The
  install's 9967271d began 6 s after its reset this way.
- **A conversation** has no look today. **The look gains a stage beside its plan, `limits`** (`beside: true`, as `tidy` and
  `follow` run, `DriverWatch.cs:965`, `:992`). *(Round 2, N2.)* It is keyed to **a held conversation whose computed wait (§3.2)
  has come**, the same computation the page shows, not to *a cool-off ended since the last look*: that missed a reset between
  the come-up's `TakeUpAsync` and the first look, an account signed in or added (which ends no cool-off), and, under D205,
  another agent's account freeing. The runner keeps the held conversations' earliest wait in memory, read again from the records
  at the come-up, so a look with none due costs one comparison, and one read of `cooling.json` where one is due. It then takes
  each conversation held by a limit (§3.5) whose wait has come, in the record's order. It is not a second loop and not a timer.
- **What drops the cached wait** *(round 3, Codex 5, N16; agreed)*. An account can be ready before the cached moment without
  any cool-off's `Until` passing: *Try now* (`AccountCooling.End`, `AccountCooling.cs:93-104`), a sign-in (`SignedIn`,
  `:131-135`), an added or newly eligible account (the set `AccountSet.Of` folds from the harness settings and each agent's
  profiles, `HarnessSettings.Profiles`), an actor's order edited under D205, and §3.1's in-memory overlay being written or
  passing. The cache is therefore keyed, beside its moment, by a **stamp** of what the wait was computed from: the modified times
  of `cooling.json`, the harness settings and the agents' profile folders, `driver.json`'s (where D205's actors live), and the
  overlay's version. Each look reads the stamp (a few file times, no parse), and a changed stamp recomputes the
  wait at that look. The driver's `HomeWatch` gains the same files as a topic, `Accounts` (`Platform/HomeWatch.cs:196-208` has no
  such topic today), so a change also wakes the look early rather than waiting for its pace.
- **No timed candidate** (every account signed out, none in the set, or each choice's agent gone): the wait has no moment. The
  conversation is held with the sentence that says why (`held.wait-every`'s sign-in form), and only a stamp change wakes it.
- **After a wake that did not resume it**: refused by the limit again, its new cool-off changes the stamp and the wait is
  recomputed from it; refused otherwise (`gone` and the rest), it leaves the held set by §3.5's refusal paths, and a look never
  tries it again by itself.
- **The closing boundary** (§3.9): the stage takes nothing once the shell is closing (`context.Closing`).

### 3.5 Conversations (point 4)

A conversation, a repository's chat or Ask Daoris, whose turn met a limit:

- **Goes on now where it can**: LIMITGOON1's move to another account of its agent, then D205's (e) to its actor's next choice
  on another agent (Ask Daoris included, as (e)'s named exception). Both are unchanged.
- **Else waits, held.** LIMITGOON1's *stays, and the next refused turn tries again* becomes a written wait. **One carrier per
  word** *(round 2, B3)*: the refused turns' words stay in the record's events, where they already are, and `LimitHeld.Of` stays
  their only reader (`LimitHeld.cs:17-56`); only the words said **while it waits** are kept on the record's `said` through
  `ChatTurns.KeepAsync`. Neither is dispatched to a process whose account is cooling. The conversation shows `held.limit-waits`
  with its reset. A word said to it is answered *held until {{time}}*, never refused turn by turn.
- **At the wake**, as §3.3: its own account ready first → **the same conversation goes on**. That is the next turn on the same
  process where it still runs, else the agent's own resume on that account (`ChatGoOn`'s path, D137 §2.2). The prompt holds, in
  this order and each once: the refused turns' words (from the events, as the person's), the words said while it waited (from
  `said`, taken off by their ids as the turn takes them), then Daoris's word last, as in §3.3 step 1. The refused turns' prompts
  did reach the agent before its account refused them (`ChatRunner.cs:2486-2487`), and whether its own transcript kept them is
  not known; they are sent again, since a repeat in its context costs a few lines and a loss costs the person's request. The page
  adds no event for them, so it shows each once. Row 1c's proof asserts the stand-in's prompt holds each once. *(Round 3, N26.)*
  Their one carrier lasts only while the last turn is limited (`LimitHeld.cs:35-55`). A wake turn that then ends on an error that
  is not a limit has sent them once, in that turn, so what is lost is a failed turn's, as in any conversation; they are not kept
  for another.
- **Otherwise another account frees first → a briefed move.** It hands the brief, then the refused turns' words, then the words
  said while waiting, each once, the waiting ones taken off the old record by their ids in the same move, so `BriefedMove`'s own
  pass of the record's waiting words finds none and nothing is delivered twice.
- **The seam that moves an ended conversation to another account** *(round 2, B3, F5)*. `LimitMove.MoveAsync` needs a live
  process (`LimitMove.cs:49`), and `BriefedMove` opens on the record's own account (`BriefedMove.cs:131`), so neither does it
  today. But only `LimitMove`'s hold of the live turn needs the process (`:92-104`): its start is the walk's
  (`runner.StartAsync(…, profile: null, from: new ContinuedFrom(session, HandOverWhy.Limit, HandOverBy.Daoris))`, `:109-111`),
  which passes the cooling account, and its hand-over is `SayHandedOver` (`:124`). The seam is `LimitMove`'s own body for an
  ended record: the hold and `EndForHandOverAsync` skipped, since the record has already ended, and the rest as written. It stays
  **within the conversation's agent**, so it needs neither D205's row e nor LIMITGOON2. Moving to another agent is (e)'s seam
  (MODELROLE1 row e, LIMITGOON2), and that part of the wake is row 1e's, after them.
- **Its own conversation lost, its own account the one ready** *(round 3, B9, F11, F12, Codex 4 and 8; agreed)*. At the wake the
  own account is ready but the judge or the wire proves the conversation lost (`reason.Lost`: `gone`, `unkept`, `unable`,
  `offered`, `Continuation.cs:46`). Today `GoOnAsync` hands that to `LostAsync` (`ChatGoOn.cs:392-396`), which moves only with
  the person's words on `said` (`:669`; `BriefedMove.cs:96-100` refuses `NoWords`) and otherwise reaches `CannotGoOn`
  (`ChatGoOn.cs:687`). For a conversation held by a limit, the refused turns' words are in the events alone, so that path loses
  the person's request. The fallback for a limit-held conversation proven lost is therefore **`LimitMove`'s ended-record body,
  with the record's own account allowed for that reason alone** (relaxing `LimitMove.cs:81-84` only where `reason.Lost`): the
  brief composed `before: held.Before`, then the refused turns' words, then the words said while waiting, each once.
- **Every `ChatGoOn` refusal path takes Daoris's word off first** *(round 3, B9)*: `LostAsync`, `CannotGoOn` and `WentBackAsync`,
  wherever a limit word or §3.9's restart word waits. Then: a lost conversation held by a limit goes by the bullet above; a lost
  conversation cut by an update (§3.9) goes by the same body with the brief alone, uncut by `before`, since the turn the update
  cut is the line's last and the brief carries it (Codex 4: that request is already off `said`); a refusal that proves no loss
  (`exited`, `refused`) waits for the person as today, with the person's words marked and no Daoris word among them. So *cannot
  go on* is never said over a word of Daoris's alone.
- **No words stranded between opening and delivery** *(round 3, Codex 8, B9; agreed)*. `LimitMove`'s body opens the replacement
  first (`LimitMove.cs:107-113`) and hands an in-memory message to `SayHandedOver` (`:124`, `ChatHandOver.cs:238-247`), which is
  `Turns.Take`, not a keep; a shell that ends between leaves the old record continued, which recovery then skips
  (`ChatGoOn.cs:206-207`), and the replacement empty. So the ended-record move has **one durable owner**, a box of its own beside
  D163's (`HandOverBox`, `Conversations/HandOver/HandOver.cs:13-44`, which the shell already takes as it comes up): written before
  the replacement opens, it names the reason, the refused turns' event bound (`held.Before` and the last limited turn), the ids of
  the words said while waiting, their kept files, and, once opened, the replacement's id. The message is composed from the box,
  never held only in memory. The old record's waiting words are taken off by their ids **only at delivery**: when the
  replacement's harness names its conversation for that message (`named`, as `WentOnAsync` takes a going-on's words,
  `ChatRunner.cs:2050-2054`), not at the queue's admission. The box is cleared at the same moment. At the come-up, before
  `TakeUpAsync` skips a continued record, a box with a replacement that took no turn is delivered to it again from the box; a box
  with no replacement is taken as a move not yet made. A replacement that took its turn is the box's end, whatever its words did.
- **A missing conversation on the native door** *(round 2, N1; round 3, Codex 7)*. The protocol door refuses a resume of an id it
  no longer has with `resource_not_found` → `gone` before any prompt (`Agents/Acp/Acp.cs:2606-2612`). The native door resumes by
  `--resume <id>` with the prompt already in its arguments (driven, `Agents/Adapters/Adapters.cs:2123-2128`) or written to stdin
  (a chat, `ChatRunner.cs:2633-2658`), and names its conversation in its `init` line only after (`:2049-2053`). Comparing that id
  with the kept one cannot prove that no work ran without context, so round 2's comparison is **not** the safety. The safety is a
  **measured contract**: row 1f records, on the real door, what `--resume` of a missing id does. If it fails closed (it exits
  before `init` and takes no turn), the native door's automatic resume is built on that, and an end before `init` keeps today's
  meaning: tried once more, then `exited` (`ChatRunner.cs:2064-2077`, D189's note), **never `gone`**, since a crash or a failed
  sign-in ends the same way. If it opens a fresh conversation, the native door never takes an automatic own-conversation resume:
  it goes by the brief (the move above, own account allowed), which is complete in context. Until 1f lands, the native door goes
  by the brief. The id comparison stays as evidence only: a mismatch is logged and said, so a record never claims a conversation
  it did not resume. A stand-in proves the code's reaction to each recorded shape; it does not prove the door's shape.
- A conversation on the person's **pick** waits for its own account alone, as D205 §3.5(d) has it, and §3.6's press offers the
  rest.
- **Ended conversations whose words wait for their account** (`ChatGoOn.cs:419-423`, D137 point 7) are woken the same way, **on
  their own account only** *(round 2, N5)*. They never met a limit: the person went back to that exact conversation, so they
  wait for its account and go on in it at its reset, never moved elsewhere by the wake. That makes true what `waits.account`
  already says. Only a conversation held by a limit (§3.5's wait) moves.

### 3.6 Choices: D205's walk reaches a driven line at a limit

D205 §3.5(a) holds a driven line on its asked choice and offers the next only by the person's press. Its round-4 note kept (e)
to conversations. The owner's direction of 2026-10-23, recorded in D205's acceptance note, was that *a driven line's next start
walks its choices*. This design takes that: **a driven line whose last part ended limited walks its actor's effective order at
step 2**, choice-major, every account of the out choice passed, as (e) does for a conversation. *(Round 3, Codex 11.)* D205 is
accepted, so this walk is an amendment to it in three places, each made by its dated note and §4: D205 §3.5(e)'s *why
conversations and not driven lines* (`docs/2026-10-23-actors-design.md:532-534`), the card's consent (§6.3, `:905-915`) and
the editors' words (§7, `:1025-1029`), all below. It carries the brief and the
held words, said once (*Went on with Codex · account default, Build's choice 2: Claude Code's accounts are at their limit until
14:05*). A **pick**, a **handed quest's named agent** and an actor *as before* (one entry) do not walk: they wait. The extension
is limited to an end by a limit. A refusal after the prompt, a content refusal or an uncertain send is the failed turn it is
(D205 §4.3). This is the one change to D205's walk, so the foundation read is owed.

*(Round 2, N3.)* Its dependencies, each before row 1e: MAKERTRAIL1, since the merge tool refuses another maker's commits without
their trailer; D155's maker passes, where an opinion's independence now meets a line with two makers and is judged against
every maker the line ran on (row 1j, *round 3, Codex 12*); and D163's *Rejected: automatic hand-overs by default* gains a note
for a driven line at a limit, as (e) gave it one for conversations (§4).

*(Round 3, Codex 11; round 2's `handOver: off` sentence withdrawn.)* Round 2 said `handOver: off` holds a driven line *as it holds
a conversation*. It does not hold a conversation: D205 keeps that **`off` does not stop (e)** (`actors-design.md:539-540`, Opus
N13), since the rule governs an agent's own ask (`HandOverRules.cs:7-10`) and the walk is no ask. A driven line takes the same
reading, so the two meanings are one and stated once for each:

- **a conversation**: `handOver` (any mode) governs the agent's own *Hand over* ask; it never stops (e)'s move at a limit. As
  D205 has it, unchanged;
- **a driven line**: the same. `handOver` governs the agent's ask (MIXAGENT1a-ii's quest hand-over); it never stops the walk
  across choices at a limit. What keeps a driven line on its agent is the person's order itself: an actor with one entry (*as
  before*), a pick, or a handed quest's named agent waits at step 3, as above.

The consent is the person's order, so the words they press must say it (D205 §6.3's R3-B3 reason). On acceptance:

| Where | Today (D205) | Becomes |
|---|---|---|
| The card's last clause (§6.3) | *… a conversation at its agent's limit goes on with the next, and a quest's session never does.* / *…对话的智能体用量到达上限时会改用下一个选择继续，委托的会话从不如此。* | *… a conversation or a quest's session at its agent's limit goes on with the next; one on your pick waits.* / *…对话或委托的会话在其智能体用量到达上限时会改用下一个选择继续；你指定的则等待。* |
| Build's editor (§7) | *Running work never moves to another choice.* / *正在运行的工作不会改用其他选择。* | *A quest's session whose agent has no account left goes on with your next choice, handed Daoris's brief and your waiting words; one on your pick or a handed agent waits.* / *委托的会话的智能体没有剩余账户时，会带着 Daoris 的简报和你待发的话改用你的下一个选择继续；你指定的或交接指定的智能体则等待。* |
| Every other actor's editor but Chat's and Ask Daoris's (§7) | *Running work never moves to another choice.* | Unchanged: intakes, opinions, helpers, partners and brainstorm voices never walk choices (§3.8) |

Proofs (row 1e): `handOver: off` with every account of choice 1 cooling, a conversation and a driven line each going on with
choice 2; a pick and a handed quest's agent each waiting; the card's and Build's editor's words in en and zh.

### 3.7 The page (point 5)

The line's head and its foot, the session's row, and the quest's page all say the same sentence where the line waits:

| Key | en | zh |
|---|---|---|
| `work.limit.waits` | Waiting for a limit to reset: it resumes by itself at {{time}}, on {{account}}. | 正在等待额度重置：将于 {{time}} 在 {{account}} 上自动继续。 |
| `work.limit.waitsChoice` | Every choice of {{actor}} is at its limit: it resumes by itself at {{time}}, with {{agent}} on {{account}}. | {{actor}} 的所有选择都已达上限：将于 {{time}} 由 {{agent}} 在 {{account}} 上自动继续。 |
| `work.limit.noReset` | No reset was said: it tries again at {{time}}. | 未告知重置时间：将于 {{time}} 再试。 |
| `work.limit.own` | It goes on in its own conversation. | 它将在原对话中继续。 |
| `work.limit.briefed` | It goes on in a new session, handed Daoris's brief. | 它将在新会话中继续，并附上 Daoris 的简报。 |
| `work.limit.notLifted` | Its last reset did not lift the limit: {{hit}}. | 上次重置未解除额度限制：{{hit}}。 |
| `work.limit.goOnNow` | Go on now with… | 改用其他选择继续… |
| `work.limit.pause` | Pause | 暂停 |
| `work.limit.word` (the word for a `failed` record with `limit`) | limited | 额度用尽 |
| `work.limit.stopWaiting` (round 3, N27) | Stop waiting | 停止等待 |

*(Round 2, N11.)* The state's word lives in the limit's own namespace, not `sessionState`, since *limited* is not a state of the
ledger. The zh press reads 改用其他选择继续…. 额度 names the account's allowance and 上限 a window's limit, as the glossary keeps
them. The sentence says which resume comes (`own` where its own account is the earliest, `briefed` otherwise). The account's
existing *Try now* (`AccountCooling.End`) sits beside it.

- **The notes**, coded in `NoteCodes` with `noteLines.ts` values:
  - `limit.resumed`: *Its account was ready again at {{until}}; it went on in its own conversation.* / *账户已于 {{until}} 恢复可用，它在原对话中继续。* (round 2, N11: the `until` is the margin-added `Until`, §3.2).
  - `limit.waits`: the hold, in `held.limit-waits`'s words.
  - `limit.notLifted`: §3.3's bound, said once per cool-off on the line and in the band.
  - The moves keep `moved.on` / `moved.from`.
- ***Go on now with…*** opens D163's hand-over offer, listing what can run now: another agent or account the person may choose,
  the actor's own choices first. It is the person's press, so it may name a maker outside the actor (D205 §4.2's offer). It is
  the only way a pick moves. *(Round 2, B4.)* It falls under D205 (e)'s Ask Daoris exception only for the automatic move. D163's
  offer refuses Ask Daoris's own conversation (`Conversations/HandOver/HandOver.cs:51-53`, `:188-190`), and D205 §3.5(e) keeps
  *the person's press … still never move it* (`docs/2026-10-23-actors-design.md:507-508`). So on Ask Daoris the press is its own
  door, `ResumeWords.HelpDoor` (*start a new conversation from this one*), never D163's offer.
- ***Pause*** for a driven line *(round 2, B4)*. A waiting driven line's last record has ended, and *nothing else leaves an ended
  state* (`src/Daoris.Service/Daoris.Service.Core/Sessions/SessionLedger.cs:1446-1451`), so there is no stop to make on it. Its
  press is the pause of its work (PAUSE1b), which the planner reads before anything else (`Planner.cs:1278-1282`). *Resume*
  lifts it, and the line goes on as §3.3 has it. *(Round 3, N17: a limited record is `failed`, never `PersonStopped`, so the pause
  is what holds it; the guard below stays for a stopped record of the line's.)* No `limit.reset` is said
  to a record whose line the person stopped or paused, as `ReopenTelling` guards its own words (`Loop/ReopenTelling.cs:238-244`).
  Since step 1 keeps the word only inside the start (§3.3), a paused or stopped line never reaches it.
- ***Stop*** for a conversation ends it as today; the wake (§3.4) skips a record the person stopped. *(Round 3, N27.)* A held
  conversation that has already ended (after a close or the sweep) has no stop to make (`SessionLedger.cs:1446-1451`), as B4
  found for a driven line. Its press is ***Stop waiting***: the waiting words are marked as `CannotGoOn` marks them, any Daoris
  word taken off by id, and the record leaves the wake's held set, so nothing goes on by itself. *Go on now with…* and *Try now*
  stay beside it, and the person's next word goes on as any word to an ended conversation does.
- **The notes added in round 3**: `limit.resumeRefused` (*Its own conversation could not be resumed ({{why}}), so it went on in a
  new session with Daoris's brief.* / *无法恢复它的原对话（{{why}}），因此它带着 Daoris 的简报在新会话中继续。*), the refusal's code
  as `why`; `ended.limit-words-wait` (§3.3); `account.cooling-unkept` (§3.1).
- *(Round 3, N29.)* `work.limit.waitsChoice`'s zh says 已达上限, the UI's own phrase (`harness.use.passed.refused`, `locales/zh/harness.json:116`), not 已达额度上限,
  which joins the two words the glossary keeps apart.

### 3.8 The other kinds of run (round 2, B5)

Every kind passes a cooling account at its start, since each goes through the walk: intakes call `SelectAsync`
(`Loop/DriverIntakes.cs:124`, held with the cool-off at `:134`); helpers and partners call `ReviewerChoice.ChooseAsync`
(`Review/Domain/ReviewerChoice.cs:327`); opinions are chosen by the same walk (`DriverOpinions.cs:36`); a brainstorm turn calls
`SelectHandedAsync` (`DriverBrainstorms.cs:660-661`). Every kind records the limit through `AccountLimited` or, for a brainstorm,
its own `Cooled` (`DriverBrainstorms.cs:723-727`). None counts a strike, since `ReadStrikes` counts only records with a quest
(`ServiceClient.cs:2037`). What differs is what happens after the end, and nothing resumes any of them after the reset:

| Kind | Today after a limited end | Round 2 |
|---|---|---|
| **Intake** | Ends `failed` with the flag (`DriverIntakes.cs:272-276`); its ask keeps its `Intake` binding and the look picks only asks with none (`:79`), so the ask is used up | **Covered, row 1h.** The ask is unbound by D205 §3.5(f)'s door (`EndRefusedIntakeAsync`, `docs/2026-10-23-actors-design.md:546-560`, not built; built by whichever of 1h and MODELROLE1's row f lands first), so the look picks the ask again and its start is held by the cool-off as any start's is, no strike |
| **Second opinion** | Gate `Unavailable` with `Code = Why ?? "ended"` and no `Until` (`Review/Domain/OpinionGate.cs:400`); the automatic re-ask needs `Cooling` with an `Until` passed (`OpinionGateLook.cs:221`), so only the person's *Try again* re-asks | **Covered, row 1h.** *(Round 3, Codex 3, N25.)* There is no `Cooling` gate state. A limited pass sets the gate to the exact shape the re-ask reads, `State: Unavailable, Code: ReviewerUnavailable.Cooling, Until: reset` (`OpinionGateLook.cs:220-221`), as a choice refused at start already returns it (`OpinionGate.cs:395`). The pass opened its opinion, so the gate reads `first.Why` with no `Until` (`:400`); row 1h reads the `Until` from the cool-off entry by its session (`CoolingEntry.Session`, `AccountCooling.cs:22-27`), and the opinion's `Why` is `limited` |
| **Helper** | `SettleHelperAsync` refuses it `HelperWhy.NoAnswer` (`DriverHelpers.cs:631-637`, `:539-548`), so the asker reads *no answer*, not a limit | **Covered where a lead waits on it; deferred where none does** *(round 3, Codex 9)*. HELPERWAIT1 lets a driven lead end its turn waiting on its own open helper (`DriverHelpers.cs:165-171`), and a helper refused with no answer brings that lead back to the person (`BackToPersonAsync`, `:363-386`), so a limit would still need the person. Row 1h: where the asker's last record waits on this helper (`HelperWaits.Waits`, its quest `Taken`), a limited helper run is **not refused**: the ask stays open in a state of its own, `cooling` with its `Until`, which `HelperWaits.Open` counts as open, so the lead keeps waiting. The ask is run again by the helpers' own pick (`ReviewerChoice.ChooseAsync`), which passes the cooling account: another eligible account or choice of the Helper actor at once, else at its `Until`. It is run again only while the lead still waits on it; a lead that has gone on, parked or ended lets it be refused as below. Where no lead waits (a live asker that has moved on), it is refused `limited`, naming when its account is ready again, and nothing resumes it: an answer hours later lands in a turn that no longer wants it |
| **Brainstorm voice** | Moves once to the walk's next ready account within its minutes (`DriverBrainstorms.cs:603-609`); a second failure ends it `Unavailable` with `limit` (`:598-600`) | **Deferred.** A brainstorm is a pass bounded by its minutes, and D205 keeps its voices frozen, a voice that cannot run being dropped and never replaced; waiting hours for a reset breaks the minutes it was asked within |
| **Partner** | The piece goes to `Ending` and the lead is told `partner.limited`, the work kept (`DriverPartners.cs:620-623`, `:328-331`, `PartnerNotes.cs:34`) | **Deferred.** The lead owns its partner and is told, with the work kept; a partner resuming by itself would act without its lead. The lead's wait on its partner is COAGENT1d's (COWORKUX1 design §12 row 12) |

### 3.9 An update's close (round 2, B6)

The install showed it: an update applied with no turn running, and the conversations it closed did not come back. The code
holds it this way today:

- **The drain.** An update applies once no driven session runs and no conversation's turn runs (`Daoris.Desktop.Modules/Update/InstallUpdater.cs:238-240`,
  `Setup/InstallUpdate.cs:74`). `Taking` counts only a tracked process whose turn runs (`ChatRunner.cs:1604`), and
  `DriverLoop.Work` reads nothing else (`Daoris.Desktop.Modules/Loop/DriverLoop.cs:334-340`), so a conversation still opening or
  going on (`ChatContext.GoingOn`, `ChatContext.cs:63`) does not hold it. *Update now* applies whatever runs (`InstallUpdater.cs:226`).
- **The close.** `StopAll` stops every conversation with `chat.closed` (`ChatRunner.cs:362-366`, `:1389-1415`), concluded `stopped`
  with no `interrupted` flag (`:2097-2103`, `:2713`). A driven session is recorded `stopped` and interrupted (`Driver.cs:2505-2514`),
  which `ReadStrikes` counts (`ServiceClient.cs:2034-2035`, D104), and it is carried on briefed, never in its own conversation.
- **The come-up.** `TakeUpAsync` takes only ended chats with words waiting (`ChatGoOn.cs:174-233`); the sweep only interrupted
  ones (`:119-150`). Nothing else brings a closed conversation back.

The design:

0. **The close is told it is an update, and which build** *(round 3, N22, Codex 6)*. Today the updater's close is
   `MainForm.CloseForGood()` (`Daoris.Desktop.App/Program.cs:180-182`, `InstallUpdater.cs:78-79`), the person's own close, and
   nothing tells `StopAll` (`ChatRunner.cs:1389-1415`) or the driver's close (`Driver.cs:2502-2516`) why. The updater sets the
   shell's closing reason first, `ShellClose.Update(build)` with the staged build's id, then closes; `StopAll` and the driver's
   close read it, and every other close reads `ShellClose.Person`. A person's close or restart (the install's 05:28) is unchanged:
   the turn in flight is lost as today, `chat.closed`, no word, and D104's strike stands for a driven session.
1. **A Daoris restart word on each conversation the close cuts, kept and never sent** *(round 3, Codex 6, N23; round 2 kept it
   through `ChatTurns.KeepAsync`, which takes it as the next turn, `ChatTurns.cs:189-195`)*. An update's close, per conversation
   with a turn running, a start or go-on pending, or words held, in this order:
   1. **the closing boundary**: the shell is marked closing (`context.Closing`), which `LostAsync`, `LimitMove` and §3.4's stage
      already read, and pending starts and go-ons read it before they open; the conversation's turns are **held**
      (`chat.Turns.Hold()`, as `LimitMove.cs:92` holds them), so nothing further is dispatched and a turn that ends meanwhile
      starts no other;
   2. **the stop**: its process stops, and the watch concludes the record `stopped` with `chat.closed` (`:2097-2103`); the
      driver's stop leaves the record's kept words on it (`ChatTurns.cs:746-761`);
   3. **the word**: only then is Daoris's word kept, through the say door on the ended record, as `RebaseTelling` keeps its own
      (`service.SayAsync(…, from: daoris, key)`), under `DaorisKeys.Restart(session, build)`, `restart/{session}/{build}`, so a
      second close for the same update keeps one. `DaorisKeys.Read` names no quest fact for it.
   An ended record has no process to send it to, so the word cannot be sent or counted attempted before the update; at the
   come-up `UncertainWords` (`ChatGoOn.cs:210-216`) finds nothing of it attempted. Its words: *Daoris restarted for an update
   while you were working. Go on where you stopped.* `TakeUpAsync` takes an ended chat with words waiting, so at the come-up it
   resumes it **by its kept id** on its own account, the cut turn going on in its own conversation. Its account cooling, it
   waits as §3.5 has it; no id kept or the conversation lost, §3.5's refusal paths (the brief alone, the restart word off first).
   The close's own sentence is unchanged. Proofs (row 1i): a turn that finishes while the close prepares starts no turn and
   leaves the restart word unsent on the record; a door that opens while the close prepares is held, and its record keeps one
   restart word.
2. **An idle conversation** (between turns, nothing pending, nothing held) is given no word and spends no turn: resuming it only
   to have it say it is ready costs a turn and changes nothing. Its record ends with the close's note under a code of its own,
   `chat.restarted`, which says *Daoris restarted for an update; this conversation goes on in its own conversation at your next
   word*. That is what happens: the person's next word goes on by its kept id (`ChatGoOn.cs:235-270`). The page says it as the
   close of an update, not as a stop.
3. **Opening counts as in flight for the drain.** `DriverLoop.Work` adds the conversations in `ChatContext.GoingOn` and the starts
   pending in the chat runner to `turns`, so the idle drain waits for them as it waits for a turn. *Update now* still applies at
   once, and its close gives each of them the restart word of point 1.
4. **A driven session stopped by *Update now* counts no strike.** Its stop is Daoris's own, not a decision about the quest, and
   not the quest's failure. The close records it as today, `stopped` and interrupted, with a note part coded `update.closed`, and
   `ReadStrikes` passes a record with that code as it passes an account's refusal (`AccountRefused`, `ServiceClient.cs:2412-2414`).
   No ledger column is added. Its carry-on is a carry-on whose cut-off is Daoris's own, so it takes §3.3's step 1 with the
   restart word in place of `limit.reset`: its own account and kept id first, the walk and the brief where that is refused. A
   person's close of the application is not an update and keeps D104's strike. *(Round 3, B8, Codex 4.)* The start carries the
   same kind of intent, `UpdateResume`, so its refusal goes on in the same start, briefed, with the word off, as §3.3's does;
   the word is kept just before `ContinueAsync`, and every conclusion takes it off.

### 3.10 A conversation that kept no id (round 2, B7)

**The install's four records, diagnosed** (the machine log and `data/sessions/` of the install, read 2026-10-23). The four
conversations the person resumed with `help resume` after the updates (`session.reopened … why=unkept door=terminal`, 12:18-12:19
UTC) were all Ask Daoris records on the native door (`claude-code`). None has a `harness.json`, and none was ever opened: each has
`session.started` and then `session.ended state=stopped` at a close, with no `session.opened`, no turn and no word of the person's
between. They were stopped by the closes at 03:18 (an update), 05:28 (a restart with no update) and 09:16 (an update). The native
door names its conversation in its `init` line, at the first prompt (`ChatRunner.cs:2049-2051`), so a record that never took a
prompt has no conversation to keep. **Nothing was lost, and the write did not fail: there was no conversation.** The other
conversations the 09:16 close stopped kept their ids (two on the protocol door, one Ask Daoris on the native door whose restart
words then waited for its own cooling account). The records stay as evidence; this design names no account.

So:

1. **A conversation that never took a turn and holds no words** is not a conversation to bring back. The close gives it no
   restart word (§3.9 point 1 reads *a turn running, pending or words held*), and at the come-up it stays as it was; an Ask
   Daoris opens afresh as it does today. A word the person says to it later starts its first turn. *(Round 3, N28.)* `help
   resume` on such a record says *it never took a turn* (a reason of its own, `never-spoken`, read where the record has no
   `session.opened`), not `unkept`, so the person is not told an id was lost when none existed.
2. **A conversation that took turns and kept no id goes by the brief**, its named fallback: step 2 for a driven line (§3.3), the
   briefed move for a conversation (`ChatGoOn.cs:392-396`, `LostAsync` `:663-688`). Never `limit.reset` or the restart word to a
   conversation that cannot hear it: those are taken off by id before the fallback, as §3.3's refusal path says.
3. **A lost keep is evidence.** The swallowed write (`Conversations/GoOn/HarnessConversations.cs:39-42`) writes a machine-log
   line, `conversation.unkept` with the session, adapter and the error's kind. A conversation's end with no id kept, after it
   took a turn, writes the same line whatever stopped it, and its record says `chat.not-kept` beside the stop's own note
   (today `NotKept` is added only where no stop reason stands, `ChatRunner.cs:2101-2102`, so the close never said it).
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

Added in round 2 (N4, B2, B6, N13):

- **D137's TURNFAIL1 note**: a limited conclusion's words always wait, never *failed twice* (§3.3's bound).
- **D163**'s *Rejected: automatic hand-overs by default*: a driven line at a limit is moved by itself across its actor's choices
  (§3.6), as (e) already is for a conversation.
- **D205 §3.5(a)'s round-4 bullet**, *(e) is reached only from a turn that met a limit, never from words said to a conversation
  already ended* (`docs/2026-10-23-actors-design.md:379-382`): an ended conversation held by a limit is moved at the wake within
  its agent (§3.5's seam); across agents it is still (e)'s, from a limit only.
- **D125 point 6**'s *What needs you*: a held limited line is not listed there; it says its wait on its own page (§3.7).
- **D104**: a driven session stopped by an update's close counts no strike (§3.9 point 4); the person's close keeps it.
- **D189 point 6** is kept, not amended, and cited: *a left conversation is not resumed again* is why *own* means the last part's
  account only (§3.3 step 1).

Added in round 3 (Codex 10, 11; B9):

- **D125 point 3**: a limit whose cool-off could not be written is still limited, never a strike; the cool-off is held in memory
  (§3.1). Round 2's *an unwritten cool-off stays a strike* is withdrawn.
- **D205 §3.5(e)**'s *why conversations and not driven lines*, **§6.3**'s card clause and **§7**'s editor words: a driven line at a
  limit walks its actor's choices too, `handOver: off` stopping neither, and the card's and Build editor's words say so (§3.6's
  table). The other actors' editors keep *Running work never moves to another choice.*
- **D163 MIXAGENT1a-i**'s own-account refusal in `LimitMove` (*the same account, or none, stays*): relaxed for a limit-held
  conversation proven lost, which goes on briefed on its own account (§3.5).
- **D189's note on an end before `init`** is kept: `exited`, never `gone` (§3.5).

## 5. Rejected

- **A new ledger state `limited`**: the state set is shared with teammates and older builds, and the flag already exists and
  travels.
- **A timer per reset, or a loop for conversations**: the look already runs at the machine's pace. A stage that asks only where
  a held conversation's wait has come costs one comparison (§3.4, round 2).
- **Keeping a briefed carry-on on the same account** (today): it throws away the exact conversation the account still holds.
- **Copying the transcript to another account's home**: D189 point 5, D125's rejection.
- **Holding the line on the person's words** (D137 point 7 as written): it keeps work waiting for hours while another account
  could take it.
- **A *What needs you* row**: nothing waits for the person.
- **Moving a pick or a handed agent by itself**: the person named it.
- **Counting limits toward a park**: D125.
- **Saying `limit.reset` beside the look** (round 2, B1): the same look plans the carry-on, whose walk lands briefed on the own
  account. Step 1 is decided inside the start.
- **Keeping the refused turns' words on `said` as well** (round 2, B3): two carriers deliver them twice.
- **Waking every idle conversation after an update** (round 2, B6): a turn spent to say it is ready; its next word goes on in it.
- **A new ledger column for an update's stop** (round 2, B6): a note part's code is read as an account's refusal already is.
- **Taking the limit word off and then using today's fallback** (round 2's text; round 3, B8, Codex 4): with no word left it
  returns `TakenBackFirst` and the next look tries again. The start's own intent carries it on in the same start.
- **Comparing the native `init` id as the safety** (round 2, N1; round 3, Codex 7): the prompt has already gone. A recorded
  contract, or the brief.
- **Keeping the restart word through `ChatTurns.KeepAsync`** (round 2; round 3, Codex 6, N23): it can be taken as a turn before
  the stop.
- **Quest strikes as the bound for an unwritten cool-off** (round 2; round 3, Codex 10): a healthy line could spend its strikes
  on a full disk. An in-memory cool-off bounds it.
- **A cached wait recomputed only at its moment** (round 2; round 3, Codex 5, N16): *Try now*, a sign-in and an added account
  free an account early.

## 6. Rows, in build order, with their proofs

| Row | What | After | Proof |
|---|---|---|---|
| LIMITRESUME1a | §3.1: a limited end is worded *limited*, quiet, in every list and head; no *Ended* notice; the line's own news | — | `AttentionTests`: a limited conclusion raises no *Ended*; the page's state-word tests in en and zh; `i18n:check`, `names:check --strict` |
| LIMITRESUME1b | §3.3 for a driven line: an `Open` or `Taken` quest's limited last part planned as the line's resume; step 0's presses first; step 1 decided in the carry-on's start (`Driver.cs:1716-1736`), its maker's reading asked, `limit.reset` kept through the say door and taken off on any refusal; else a briefed carry-on with the held words; else `held.limit-waits`; TURNFAIL1's limited words always `Wait`; `limit.notLifted` | 1a | Round 2, N7: in the driver's stand-in test, not `PlannerTests`, B1's case first: **the own account freeing in the same look as the carry-on is planned → a full resume of the same conversation id with `limit.reset` and no brief, never a briefed part on it** (the install's case); limited on A, B free → a briefed part on B with the held words and no `limit.reset`; a full resume refused `gone` → `limit.reset` taken off, the carry-on briefed, said `gone`; two limited full resumes running → the words still `Wait`, nothing marked `FailedTwice`; a refusal with the same `hit` just after the reset → `limit.notLifted` said once, no park; a paused or person-stopped line → no word kept; no strike in any (`ReadStrikes`); `PlannerTests` for a limited `Open` quest not started fresh. *Round 3 (B8, Codex 4, 10; N19, N20):* a resume refused `gone` → a briefed part on the walk's account **in the same start**, no word left, and the next look spawns nothing for that record; refused `elsewhere` → the word kept and marked, the line held for the person, no process spawned at the next look; two limited full resumes running, then another account free → no *cannot go on* line, at most one Daoris word on the record, none handed to the briefed part; a held start between the judge and `ContinueAsync` → no word kept; a `GoOn` on a limited record resumed on its own account → the person's words then Daoris's word; one limited end then one ordinary failure → the words not *failed twice*; a cool-off whose write throws → limited, no strike, the account passed until its `Until` |
| LIMITRESUME1c | §3.4-§3.5 for conversations: the look's `limits` stage keyed to the computed wait and its stamp; LIMITGOON1's stay written as a wait; one carrier per word; the wake's own-account resume or, within its agent, `LimitMove`'s body for an ended record, its own account allowed where the conversation is lost, through the move's durable box; every `ChatGoOn` refusal path taking Daoris's word off first; ended chats' waiting words woken on their own account only; *Stop waiting* | LIMITGOON1 merged (`2d2aedca0`); its native-door resume after 1f; the cross-agent part is 1e's | `ChatGoOnTests`/`LimitMove` tests: a chat and Ask Daoris all cooling, the stage waking each once at the reset, the same process's prompt holding the refused turns' words, the waiting words and Daoris's word, each once; an ended conversation with another account freeing first → moved by the walk with the brief and both kinds of words, none twice; a word said while waiting kept once; an ended chat's D137 words never moved; the stage asking nothing where no wait has come (held clock, `held-clocks.md`). *Round 3 (B9, Codex 5, 8):* own account ready and a protocol stand-in answering `resource_not_found` → a new part with the brief and the refused turns' words, never *cannot go on*; `exited` → waits for the person, no Daoris word left; with a wait cached for tomorrow, *Try now*, a sign-in and an added account each wake it at the next look; a shell ended after the replacement opens and before delivery → the come-up delivers the box to it, the old record's words still on it until then; a shell ended at the delivery → each word delivered once |
| LIMITRESUME1h | §3.8: an intake at a limit unbound by D205 (f)'s door; an opinion's limited pass gated `Unavailable`/`Cooling` with its `Until` from the cool-off entry; a helper a lead waits on kept open `cooling` and asked again; one no lead waits on refused as limited with its ready time | 1a | stand-in agents: a limited intake's ask picked again after the reset, held meanwhile, no strike; a limited opinion re-asked by `OpinionGateLook` after its `Until`; a lead waiting on a limited helper still waiting, the helper run again on another account or at the reset, never back to the person; a lead that went on → the helper refused as limited, not run again |
| LIMITRESUME1d | §3.7: the sentence (own or briefed), `notLifted`, *Go on now with…* (Ask Daoris's `HelpDoor`), *Pause* for a driven line, *Stop* and *Stop waiting* for a conversation, *Try now* beside, in the head, foot, row and quest page; notes `limit.resumed`, `held.limit-waits`, `limit.notLifted`, `limit.resumeRefused`, `ended.limit-words-wait`, `account.cooling-unkept`; glossary; §3.1's other readers | 1b, 1c | page tests and stories in en and zh; `HelpCoverageTests`' rows for the presses; `note.test.ts`; the terminal's `sessions` saying *limited* |
| LIMITRESUME1i | §3.9-§3.10: the close told it is an update and which build (`ShellClose`); the restart word kept on the ended record after the turns are held and the process stopped; an idle conversation's `chat.restarted`; opening counted in the drain; *Update now*'s driven stop coded `update.closed`, no strike, carried on by step 1's path; a never-spoken conversation given nothing and `help resume` saying `never-spoken`; `conversation.unkept` logged, `chat.not-kept` beside a stop | 1b, 1c *(round 3, Codex 12: its cooling and lost cases are 1c's)* | `ChatRunner` close tests: a turn running, a go-on pending and words held each keep one restart word, an idle one and a never-spoken one none; a turn finishing during the close's preparation starts no turn and leaves the word unsent; a door opening during it held, one word kept; a person's close keeps no word; `TakeUpAsync` resuming the first by its kept id; `InstallUpdate`/`DriverLoop.Work` counting `GoingOn`; `ReadStrikes` passing `update.closed`; the driven stand-in's update-closed take resumed in its own conversation; a failed keep writing the line |
| LIMITRESUME1g | the install: a line held across a real reset resumes its own conversation, seen in the window; an update with a conversation mid-turn and an idle one, the first going on by its kept id, the second saying it goes on at the next word | 1a-1d, 1i | the parent's install proof. *Round 3 (Codex 12):* the resumed part names **the same harness conversation id on the same account** as the limited part (its `session.resumed` and the kept id), not only `session.moved` absent; `limit.resumed` said |
| LIMITRESUME1f | the native door's two recordings and what rests on them: §3.1's limit read; *(round 3, Codex 7)* what its `--resume` of a missing id does, on the real door | a recorded native-door sentence and a recorded missing-id resume | `AccountLimits` table rows held both ways; a native-door limit concludes limited, no strike; the recorded resume shape, and the stand-in's reaction to it in 1b and 1c (a stand-in proves the code, the recording proves the door) |
| LIMITRESUME1j *(round 3, Codex 12)* | D155's maker passes judged against every maker the line ran on, so an opinion's independence holds on a line with two makers (§3.6) | MAKERTRAIL1 | an opinion asked on a line that ran on two makers refusing a reviewer of either |
| LIMITRESUME1e | §3.6: a driven line at a limit walks its actor's choices; the wait's earliest reset over the effective order; the conversation wake's move to another agent; D205's card and Build editor's words (§3.6's table) | MODELROLE1 rows c and e, LIMITGOON2, MAKERTRAIL1, 1j | stand-in agents: every account of choice 1 cooling → choice 2 taken with the brief, said once; a pick, a handed agent and an *as before* actor wait; `handOver: off` not stopping a conversation or a driven line; the reset shown is choice 2's where it is earlier; the card's and Build editor's words in en and zh |

## 7. What the checks do not cover

This is a document; nothing ran but the census and the documentation checks. The log lines in §1 were read from the install's
machine log. The code was read at `9950b24cb`, and LIMITGOON1 was read on its branch. Two claims were not run, and their rows
must prove them: that a Claude Code ACP conversation's process, held across its reset, accepts the next prompt; and that a resume
after a weekly limit finds its conversation in the account's home.

Round 2 ran nothing either. The code was read again on main with LIMITGOON1 merged (`2d2aedca0`). §3.10's diagnosis was read
from the install's machine log and its `data/sessions/` files, read-only. Three more claims are the rows' to prove: that the
refused turns' prompts sent again are harmless in a conversation that kept them (1c); that a native `--resume` of a missing id
names another id or ends before `init` (1c); and that a maker's reading after a cool-off tells a lifted limit from one that is
not (1b).

Round 3 ran nothing but the documentation checks. The code was read again on main at `05328a5c9`. Round 2's native claim is
withdrawn as a stand-in's to prove (Codex 7, 12): what the native door's `--resume` of a missing id does is row 1f's recording on
the real door, and the native door's automatic resume waits for it. The third claim covers only agents whose door carries none of
their windows (N21); for the others the attempt is the probe.

## 8. Round 2: round 1's reads answered

Every finding of the Opus foundation read (`docs/2026-10-23-limit-resume-reads.md`), against the code on main at `2d2aedca0`.
*Taken* means the change is made as the reader proposed, or narrower; *answered* means the design keeps its text and says why.

| Finding | Taken or answered | Where |
|---|---|---|
| F1. `limit.reset` kept through `DrivenInbox` on an ended record | Taken: through the say door under `DaorisKeys.LimitReset`, as `RebaseTelling` | §3.3 step 1 |
| F2. *A refused resume falls to step 2, said `account`, as today* | Taken: the word is taken off by id before the fallback; the refusal's own code is said; `CannotGoOn` is never reached for Daoris's word | §3.3 step 1 |
| F3. Step 2's words in `BriefedMove`'s order | Taken: the driver's fallback (`CarriedOnAsync`) for a driven line | §3.3 step 2 |
| F4. The note's `until` is the agent's reset | Taken: it is the margin-added `Until`; the page says *ready again at* | §3.2, §3.7 |
| F5. LIMITGOON1's move carries an ended conversation | Taken: it does not; the seam is `LimitMove`'s body for an ended record, within its agent | §3.5 |
| F6. *The person's Go on* | Taken: `AnswerGoesOn`, the answers-alone bound's press | §2.2 |
| F7. *15 s is inside the margin* | Taken: the default only; correctness does not rest on it | §2.1 |
| B1. Step 1 has no decision point | Taken: decided inside the carry-on's own start at `Driver.cs:1716-1736`, before the walk; judged as if words waited; `limit.reset` through the say door, taken off before any fallback; the refusal's code said; `CannotGoOn` avoided; 1b's first proof is the same-look case | §3.3 steps 0-1, §6 1b |
| B2. The loop's bound; TURNFAIL1 | Taken: one attempt per cool-off, inside D125 point 3; limited words always `Wait`; `limit.notLifted` said with no park; the own account's maker reading asked after its cool-off | §3.3 *The bound*, §4 |
| B3. Two carriers for the held words; the order; the ended conversation's move | Taken: the refused turns' words in the events only, the waiting words on `said` only, each delivered once; the person's words first and Daoris's word last; the ended conversation's move is `LimitMove`'s body within its agent, so D205's row e is not needed for it, and the cross-agent wake is 1e's | §3.3 step 1, §3.5, §6 |
| B4. Stop, Ask Daoris's press, precedence | Taken: a driven line's press is *Pause* (PAUSE1b), since an ended record has no stop; no word to a paused or stopped line; Ask Daoris's press is `HelpDoor`, D205 (e)'s exception being for the automatic move only; step 0, a person's press since the end outranks the resume | §3.3 step 0, §3.7 |
| B5. Scope beyond quests and conversations | Taken: intakes and second opinions covered (row 1h); a helper's refusal worded as a limit, its resume deferred; brainstorm voices and partners deferred, each with its reason | §3.8, §6 1h |
| B6. An update stopping conversations | Taken: a restart word keyed by build on each conversation the close cuts, resumed by its kept id at the come-up; an idle one given no word, `chat.restarted`; opening counted in the drain; *Update now*'s driven stop coded `update.closed`, no strike, carried on by step 1 | §3.9, §4, §6 1i |
| B7. No id kept | Taken: the four install records diagnosed (never opened, so nothing to keep); the brief named as the fallback for a conversation that took turns; `conversation.unkept` logged; `chat.not-kept` beside a stop | §3.10, §6 1i |
| N1. A missing conversation on the native door | Taken: the `init` line's id compared with the kept one; a mismatch is `gone` | §3.5 |
| N2. The conversation wake's trigger | Taken: keyed to a held conversation's computed wait | §3.4 |
| N3. §3.6's dependencies | Taken: MAKERTRAIL1, D155's passes, `handOver: off`, D163's note | §3.6, §4, §6 1e |
| N4. The amendments list | Taken: D137's TURNFAIL1 note, D163, D205 §3.5(a)'s round-4 bullet, D125 point 6, D104 | §4 |
| N5. D137's words moved at a wake | Taken: they wake on their own account only | §3.5 |
| N6. The readers of *limited* | Taken: the terminal, Ask Daoris's tools, Overview, filters; `SessionEnded` gains the flag | §3.1 |
| N7. 1b's proofs | Taken: in the driver's stand-in test, B1's case first | §6 1b |
| N8. The computed wait's filters | Taken | §3.2 |
| N9. `Cool` shortening a stated reset | Taken: the later stated `Until` kept | §3.2 |
| N10. A further part in the waiting tree | Answered: as today's carry-on; a line says so | §3.3 |
| N11. The page's words | Taken: *ready again at*, the word out of `sessionState`, 改用其他选择继续…, own or briefed said, *Try now* beside | §3.7 |
| N12. `pollSeconds` | Taken, with F7 | §2.1 |
| N13. D189 point 6 | Taken: cited | §4 |
| N14. §1's 09:11 order | Answered: the log holds it; another session's limit came first | §1 |
| N15. F6 | Taken, with F6 | §2.2 |

## 9. Round 3: round 2's reads answered

Every finding of Codex's read and of the Opus foundation read of round 2 (`docs/2026-10-23-limit-resume-reads.md`), against the
code on main at `05328a5c9`. *Taken* and *answered* as in §8. Where the two reads agree, one row says so and both are taken by it.

| Finding | Taken or answered | Where |
|---|---|---|
| **B8** (Opus) with **Codex 4**, agreed: step 1's refusal path carries nothing on or holds nothing | Taken: the start's own intent (`LimitResume`, `UpdateResume`); the word kept just before `ContinueAsync`, after every hold; a refusal but `elsewhere` takes the word off and goes on in the same start as the planned `CarryOn`, through `CarriedOnAsync` and the brief, past `TakenBackFirst` and the Daoris-only `CannotGoOn` for that intent alone; `elsewhere` keeps the word and marks it, so `HeldForPerson` holds the line; every conclusion takes the word off; 1b's three proofs | §3.3 step 1, *The bound*; §3.9 point 4; §6 1b |
| **B9** (Opus) with **Codex 8**, agreed: a lost limit-held conversation has no seam, and the ended move can strand words | Taken: the fallback is `LimitMove`'s ended-record body with its own account allowed where `reason.Lost`, handing the brief, the refused turns' words and the waiting words; every `ChatGoOn` refusal path takes Daoris's word off first; one durable box owns the move, the old words taken off only at delivery, and the come-up delivers an opened, undelivered box; 1c's proofs | §3.5; §6 1c |
| **Codex 5** with **N16**, agreed: the cached wait hides an account freed early | Taken: the cache is keyed by a stamp of the cooling store, the account set, `driver.json` and the overlay, read at each look; `HomeWatch` gains an `Accounts` topic; an untimed wait is woken only by a stamp change; a refused wake recomputes or leaves the set | §3.4; §6 1c |
| **Codex 6** with **N22** and **N23**: the update word can be sent before the update; the close is not told it is one | Taken: `ShellClose.Update(build)` set by the updater; the close holds the turns, stops the process, then keeps the word through the say door on the ended record, so it is never sent or counted attempted before the update; a person's close unchanged, the turn lost, no word | §3.9 points 0-1; §6 1i |
| **Codex 7**: the native id compared after the prompt; no `init` is not proven loss | Taken: the comparison is evidence only; the native door's automatic resume rests on row 1f's recording of `--resume` of a missing id on the real door, else goes by the brief, and goes by the brief until then; an end before `init` stays `exited` after one retry, never `gone`; driven resumes too | §3.3 step 1; §3.5; §6 1f; §7 |
| **Codex 9**: a lead waiting on its limited helper is brought to the person | Taken: where the asker's last record waits on the helper, the ask stays open `cooling` and is run again by the walk now or at its `Until`, only while the lead waits; where none waits, refused `limited`, deferred | §3.8; §6 1h |
| **Codex 10**: a strike where a cool-off cannot be written | Taken: limited and no strike; the read cool-off held in memory and written again at each look; `account.cooling-unkept`; round 2's *stays a strike* withdrawn | §3.1; §3.3 *The bound*; §4 |
| **Codex 11**: `handOver: off` and the consent words contradict D205 | Taken: `off` stops neither (e) nor the driven walk, stated once for each, since it governs an agent's ask; the card's last clause and Build's editor amended in en and zh; the other actors' editors unchanged; D205 §3.5(e), §6.3 and §7 in §4 | §3.6; §4; §6 1e |
| **Codex 1**: the quest page already shows a reset time | Taken: §1 distinguishes the install's observation from `sittingSentence`'s rendering; the new sentence is the line's own | §1 |
| **Codex 2**: three absolute statements | Taken: an unfinished `Open`/`Taken` run's refused turn; a limit with no words waiting; D206's *within the margin* is the default poll only (D206's round-3 note) | §2.1; D206 |
| **Codex 3** with **N25**: no `Cooling` gate state; no `Until` on an opened opinion | Taken: the shape `Unavailable` / `ReviewerUnavailable.Cooling` / `Until`; the `Until` read from the cool-off entry by its session | §3.8 |
| **Codex 12**: dependencies, order, and stub versus real proofs | Taken: 1i after 1b and 1c; the rows in topological order; row 1j for D155's all-makers independence before 1e; 1g observes the same harness conversation id on the same account; the native door's shape is 1f's recording, the stand-ins prove the code | §6; §7 |
| **F8**: `elsewhere` held nothing once the word came off | Taken, with B8: the word is kept and marked | §3.3 step 1 |
| **F9**: *by Daoris* on the taken door | Taken, with N24 | §3.3 step 1 |
| **F10**: the plain carry-on not reached with no word | Taken, with B8 | §3.3 step 1 |
| **F11**: `LimitMove` refuses the own account | Taken, with B9: relaxed where the conversation is lost | §3.5 |
| **F12**: `LostAsync` moves only with the person's words on `said` | Taken, with B9 | §3.5 |
| **F13**: step 0's order | Taken, with N17 | §3.3 step 0 |
| N17. Step 0's order and place | Taken: the planner's pause, words and stop, in its order, a `failed` record never `PersonStopped`; in the start, a hand-over from the limited record | §3.3 step 0 |
| N18. Step 1's guard | Taken: `!start.GoesOn && start.Resumes is { Limit: true }` | §3.3 step 1 |
| N19. The order paragraph unreachable | Taken: both ways carry the word, a `GoOn` on a limited record resumed on its own account keeping it after the person's words | §3.3 step 1 |
| N20. The limited wait's line code | Taken: `ended.limit-words-wait`, which neither reader counts | §3.3 *The bound* |
| N21. The maker's reading only for some agents | Taken: for the others the attempt is the probe | §3.3 step 1; §7 |
| N24. The take-off door | Taken: `TakenAsync(session, [id])`, `by` the record's own session; the page shows `limit.resumeRefused` in the word's place | §3.3 step 1; §3.7 |
| N26. The refused turns' one carrier | Answered: a wake turn that fails otherwise sent them once, a failed turn's loss as in any conversation | §3.5 |
| N27. A conversation's *Stop* on an ended record | Taken: *Stop waiting* marks the words and leaves the wake's set | §3.7 |
| N28. `help resume` on a never-spoken record | Taken: `never-spoken`, *it never took a turn* | §3.10 |
| N29. D206 point 4; 已达额度上限 | Taken: D206's round-3 note re-reads point 4; the zh says 已达上限 | §3.7; D206 |
