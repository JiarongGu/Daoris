# Account rotation: a limit read from the agent's own words, a cool-off, and the work carried on by the next account (TOOL4)

> The owner, 2026-10-01, continuing after their own assistant account hit its weekly limit and they signed in to
> another: *"this is also good to test for account switch"*. TOOL4 was held by **D57 §b** *"until TOOL3 has run long
> enough to answer three questions: what a harness's exhaustion actually looks like in its output, how long a cool-off
> should be, and whether a rotated session stays reproducible"*, with exhaustion *observed, never read*. The install
> now holds five observations (§0.2), and they answer all three. This is the contract for the TOOL4 rows, and its
> decision is **D125**. Status: **designed; TOOL4a, TOOL4c and TOOL4d built** (notes under D125; TOOL4d's says how
> the protocol stub reads a limit, and why the native door's failure is not yet handed over). It replaces the
> toolchain design's §2 part 4 and §6 (`docs/2026-09-22-toolchain-design.md`, noted at its head). Read with
> **D49 §4**, **D57**, **D58**, **D66 §3**, **D67 §1**, **D73**, **D76**, **D80**, **D94**, **D104**, **D110** and
> **D116**.

Accounts are named here as Daoris names their directories (`account-1`, `account-2`: D66 §3). Nobody's account, the
owner's time zone and the machine's paths are not named: a zone in a quoted sentence is written `<zone>`.

- §0 is what is true today, read from the code at `4f65cd3`, and the evidence.
- §1–§4 are the design: the signal, the cool-off, rotation, and every account cooling.
- §5 is cost and safety, §6 the doors. §7 is the build, as rows. §8–§12 are what only a real run proves, what was
  rejected, what this amends, the twins, and what this document's gate does not cover.

## 0. What is true today

### 0.1 The code

| What | Today |
|---|---|
| A refused turn on the protocol door | `session/prompt` answered with a JSON-RPC error. `AcpSession.Complete` throws *the ACP agent refused the call: {message}*; `CaptureAcpAsync` hands that to the conclusion, and `Observation.Conclude`'s `turnFailed` makes the session `failed` in the agent's words whether its quest is taken or open (ACPEND1). The adapter then exits 0 |
| A failed turn on the native door | `ClaudeStreamJson.Result` writes *— the turn failed ({subtype}): {words}* to the transcript and the console. Nothing reaches the conclusion: `HoldAsync` passes `turnFailed` only from the protocol door. A `rate_limit_event` frame is read as nothing |
| A refused credential (AGT3b) | `HarnessToolchain.Refused`, one phrase per toolchain (`API Error: 401` for Claude Code and the stub), matched against the **transcript's last lines** when a session concluded `failed`. `HarnessRoster.Refuse` then holds the account **in memory** until a person looks again (any account action, or the roster's refresh), and `SelectAsync` refuses it before anything else |
| Which account a start runs as | `HarnessSettings.Resolve`: the person's pick, the workspace's default, the machine's, then none, and none is the tool's own configuration home (D49 §4). Driven starts and intakes pass no pick; a conversation passes the person's (`START_CHAT` sends `profile` only when one is set); Ask Daoris passes none. A door runs as its owner's accounts (AGT7) |
| A session with no account named | The tool's own configuration home: no profile variable is set (`HarnessProbe.Apply` sets one only for a profile), so the session signs in as whoever the person last signed in as, at their own terminal, with their own use of the tool. Its record names no account (`Profile` null). 🔴 On the install, a sign-in to another account at the person's terminal moved Daoris's next session to it, unseen (§0.2, after observation 3) |
| The wiring file | `harnesses.json`, read and written by twins: `toolchain.ts` and `Harnesses.cs`. 🔴 The TypeScript writer keeps every key it does not know (`rest`); `HarnessSettings.Save` writes only the four sections it knows, so a key only the CLI wrote is gone after any screen edit |
| After a refused turn | A taken quest whose last session here failed is carried on in its tree at the next tick (D80), on the account the resolution names, which is the same account. Every `failed` record is a strike (`ServiceClient.ReadStrikes`), and the third parks the quest (D58) with the planner's `Exhausted` verdict, which only the person's Retry releases (RETRY1). So a spent account is started on again until the quest parks: on the install that took seconds (observation 4) |
| What a carry-on is told | That the quest is already its own, what cut the last session off, and which changes it left uncommitted (D80). Not its plan, not its last words |
| Usage | `usage.json`, one entry per session at its context high-water, totals per account derived (TOOL3). Nothing anywhere says what an account has left |
| The record | `Session.Profile` is served only over loopback. The note is served to every reader, and to another machine through `SessionNote.ForAnotherMachine`, which elides the record's **own** profile name, its tree and its transcript: no other account's name |
| The machine log | `turn.ended`, `session.ended` and the rest (D94 §4). Nothing about an account's limit |
| Rotating by hand | The person makes another account the default (Settings → Agents, or `daoris agent profile default claude-code account-2`), and the next carry-on runs on it. Nothing else moves work to another account |

### 0.2 The evidence

Five observations, on two channels. Each sentence is quoted as recorded, the zone elided.

| # | When | Where | How it arrived | The words |
|---|---|---|---|---|
| 1 | 2026-09-27 | a driven session (FG5), Claude Code over its ACP adapter | the error answering `session/prompt`, 370k tokens into a twenty-minute turn; the adapter exited 0 | *Internal error: You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your session limit resets 7am (<zone>)* |
| 2 | 2026-09-29 | two driven sessions, the same door | the same, as the driver's note: *the ACP agent refused the call: Internal error: …* | *… · your session limit resets 7:50am (<zone>)*, in both |
| 3 | 2026-10-01 | the owner's own assistant: the same maker's CLI, not driven by Daoris | HTTP 429 `rate_limit` | *You've hit your weekly limit · resets Oct 6, 10pm (<zone>)* |
| 4 | 2026-10-01 | a driven session carrying a quest on, the protocol door, on the tool's own sign-in | refused mid-turn after 223 events; the driver carried the quest on twice more (D80), each refused at once, three events each; the third failure parked the quest on its strikes | *You've hit your individual spend limit · … · your weekly limit resets Oct 3, 4pm (<zone>)*, three times |
| 5 | 2026-10-02 | this repository's own subagents: the same maker's CLI, not driven by Daoris, after the owner had already switched once | HTTP 429 `rate_limit`, mid-run | *You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your weekly limit resets Oct 3, 4pm (<zone>)* |

Between observations 3 and 4 the owner signed in to another account at their own terminal. Daoris's sessions named no
account, so they read the same sign-in (§0.1), and **the person's sign-in moved Daoris's next session to the new
account without either saying so**. Observations 4 and 5 then name one reset, *Oct 3, 4pm*: one account, spent by
Daoris's sessions and the person's own agents alike. After observation 5 the owner added a third account, and **every
stopped agent was resumed with its context intact**: the work outlived the account it started on. That was done by
hand.

**What they answer of D57 §b's three questions:**

1. **What a limit looks like.** One sentence shape, five times. Clauses joined by ` · `. One says what was hit (*You've
   hit your individual spend limit*, *You've hit your weekly limit*); a later one says when it resets (*your session
   limit resets 7am*, *your weekly limit resets Oct 3, 4pm*, *resets Oct 6, 10pm*), with the zone in parentheses. The
   reset is a time of day when it is within a day, and a month, a day and a time when it is days away. On the protocol
   door the sentence is the error's message, behind *Internal error: *. The message is the only part of the error
   recorded.
2. **How long a cool-off is.** The agent says, every time it was seen. The cool-off is read, not chosen; a default is
   only for a sentence that names no time.
3. **Whether rotated work stays reproducible.** Observation 5's resumed agents carried on from their own record on a
   new account. Daoris's records name each session's account and tool version (D49 §4), but only when it ran on a
   named account: observation 4's sessions ran on the tool's own sign-in, and no record says which account that was.
   §3.6 and §3.7 say what makes a rotated piece of work readable end to end.

Three more things they show:

- **A spend limit is not one window.** Observations 1 and 2 reset with the *session* limit, 4 and 5 with the *weekly*
  one. So the reset clause decides the cool-off, and the *hit* clause is kept for the record only.
- **A start on a spent account is refused at once.** Observation 4's two carry-ons each lasted three events. Starting
  one costs seconds and no work, and three of them parked a quest whose only fault was its account.
- **The tool's own sign-in is shared with the person, and moves when they move it.** Daoris cannot choose it, is not
  told when it changes, and cannot say afterwards which account a session on it ran as.

## 1. The signal

### 1.1 What counts as a limit

A turn the harness refused, **on a door that carries the refusal apart from the agent's own words**, whose words an
entry in that agent's table recognises (§1.3). Nothing else: not a timeout, a crash, a stand-down or a decline, and not
anything the agent or its tools printed (§1.4).

### 1.2 On each door

| Door | Where a refusal arrives | What is read | Standing |
|---|---|---|---|
| The protocol door (ACP) | the JSON-RPC error answering `session/prompt` (§0.1) | the error's message, as the conclusion already receives it | observed: 1, 2 and 4 |
| The native door (`stream-json`) | a `result` frame with `is_error: true`, and its `result` text; `rate_limit_event` frames | the failed result's text, once the mapper hands it to the conclusion as the protocol door's failure is handed. A `rate_limit_event` only once its shape is recorded (TOOL4b) | **not observed**: no table entry, so a limit there is a failure as today |
| A door with text alone | nothing apart from the agent's output | nothing | never read |
| A maker's API (429) | no door of Daoris's. Daoris makes no model call (D24), and an account that is a key hands its key to the harness at spawn (D67 §1), which makes the calls and says the limit in its own words | the harness's words, on its door | 3 and 5 were the maker's CLI and its own API, outside Daoris. They inform the grammar, and the table says where each sentence came from |

**A field beats a sentence.** Where a door carries a limit as a field (the native door's `rate_limit_event` may; an ACP
error's `data` might), its adapter reads the field and the table is not consulted on that door. Until a frame with the
field is recorded, nothing is read from it.

### 1.3 The table

Each agent that has been seen hitting a limit declares an entry on its toolchain, `Limits`, beside AGT3b's `Refused`.
A door's limits are its owner's, as its accounts are (AGT7): `claude-code-acp` reads `claude-code`'s.

- **The marker**: a clause (the text between ` · ` separators) that ends *You've hit your <what> limit*, whatever a door
  put before it (the protocol door's *Internal error: *, the driver's *the ACP agent refused the call: *). `<what>`
  (*individual spend*, *weekly*) is kept as `hit`.
- **The reset**: a clause *[your <window> limit ]resets <when> (<zone>)*. `<window>` (*session*, *weekly*) is kept as
  `window` where it is said.
- **The recorded sentences**, each with its date, its door or channel, and the harness's version where known: the five
  of §0.2, with the zone replaced by one the test chooses, never the install's.

The rules:

1. **Compared case-insensitively**, the apostrophe in either form (`'`, `’`), a run of white space as one.
2. **One reader.** `AccountLimits.Read(entry, failure, seen, machineZone)`, pure, in the driver library, answers what
   §1.5 says. Nothing else in the driver matches a limit's words.
3. **An entry grows only with a recorded sentence.** A test refuses a marker that no recorded sentence matches, and a
   recorded sentence that no marker matches. A harness with no entry, codex's, dsh's and a plugin's today, reads every
   failure as today's failure.
4. **The stub's toolchain declares an entry too**, as it mirrors `Refused`, so the family rehearsal gates a limit with
   no account behind it.

### 1.4 What is never read

- **The transcript, the agent's messages, or what its tools printed.** A session working on this very feature prints
  these sentences: in a test, in a fixture, in this document. A failed session whose own output quoted one would cool an
  account that is not spent. Reading only the door's failure makes that impossible. (AGT3b reads the transcript's last
  lines; §12 names it.)
- **A provider's console, quota route or response headers.** Each needs a credential or an API Daoris has no business
  holding (D49 §4, D57 §6).
- **Token counts.** `usage.json` and the prompt response's `_meta.quota` say what was used, not what is left. A guess
  from them would cool an account that is not spent, or miss one that is.
- **An HTTP status.** No door of Daoris's sees one (§1.2).

### 1.5 What a recognised limit gives

`LimitSeen`: `hit`; `window`, or null; `until`, the instant the account is offered again (§2); `stated`, whether the
agent named that time; `assumedZone`, whether the machine's zone stood in for the one it named.

A failure the table does not recognise is **a failure, as today**: the note carries the agent's words, the strikes
bound it, and the wording becomes an entry by a row once it is recorded.

## 2. The cool-off

### 2.1 The reset, read

| The reset says | Read as |
|---|---|
| a time of day: *7am*, *7:50am*, *10pm* | the first such moment after `seen`, in the zone |
| a month, a day and a time: *Oct 6, 10pm*, *Oct 3, 4pm* | that moment in the zone, in the year that puts it first after `seen` |
| a moment up to 15 minutes before `seen`, read before the two rows above | `seen`: the two clocks disagree and the reset has not landed, so the account waits the margin and is tried again |
| a zone the platform resolves as an IANA identifier | that zone |
| any other zone (an abbreviation, a display name), or none | **the machine's own zone**, said as assumed. The harness printed the time for the machine it runs on, which is this one |
| no reset clause, or one this grammar does not read | the default (§2.2) |
| a moment more than 8 days ahead | **not believed**: the default, said so. The longest window any sentence named is a week, so a moment further off is a date already past, carried into next year (*Oct 6* read on Oct 20) |

A **margin of 2 minutes** is added to a stated reset, so the next start does not race the provider's own clock. Month
names are read in English, as the sentences print them; a sentence in another language matches no marker, and is a
failure as today.

Rows the test holds, `seen` in the sentence's zone:

| The reset clause | `seen` | `until` |
|---|---|---|
| *your session limit resets 7am* | 03:10 | 07:02 the same day, stated |
| *your session limit resets 7am* | 07:05 | 07:07: read as `seen`, plus the margin |
| *your session limit resets 7:50am* | 08:30 | 07:52 the next day |
| *resets Oct 6, 10pm* | Oct 1, 14:00 | Oct 6, 22:02 |
| *your weekly limit resets Oct 3, 4pm* | Oct 2, 09:00 | Oct 3, 16:02 |
| *resets Jan 2, 9am* | Dec 30 | Jan 2 of the next year, 09:02 |
| *resets Oct 6, 10pm* | Oct 20 | the default, *not believed* (the next Oct 6 is 351 days ahead) |
| *resets 7am (PST)* | — | 07:02 in the machine's zone, `assumedZone` |
| no reset clause | — | the default, not stated |

### 2.2 The default

**60 minutes**, for a limit whose sentence names no time this reads or one it does not believe. Long enough that a spent
account is not started on at every look; short enough that a wrong guess costs an hour of waiting, not an evening. A
guess too short costs little: a start on a spent account is refused at once (observation 4), and its refusal states a
time if it has one. Settable (`cooloff` in `driver.json`, at least 1; absent is 60), and measured: the usage report
counts limits met with a stated time against those met with the default (TOOL4h), which is how to learn whether an
hour is right.

### 2.3 Kept per account, on the machine

`cooling.json` under the home, keyed by the account's owner and profile name, `""` for the tool's own configuration
home (AGT3b's key):

```json
{
  "claude-code": {
    "account-1": {
      "until": "2026-09-29T21:52:00Z", "stated": true, "window": "session",
      "seen": "2026-09-29T14:02:11Z", "session": "3f9c2a71"
    }
  }
}
```

- **Written by the driver when a limit is read**, atomically. A later limit on the same account replaces its entry. An
  entry whose `until` has passed is ready, and the next write drops it.
- **Ended early only by what may have changed the account**: the person's *Try now* (§6), since they may know the limit
  was raised; a sign-in or a new key into that account, since the directory may now hold another account (AGT3b ends a
  refusal on the same acts); and, for the tool's own home alone, the roster's refresh, because a sign-in there happens
  at the person's own terminal, where Daoris does not see it (§3.7). A named account's refresh does not end its
  cool-off: its reset is a stated fact, and nothing outside Daoris changes which account its directory holds.
- **Never the agent's words, never a key, never who signed in.** The session id points at the record, which holds the
  words.
- **Missing or unreadable is no account cooling** (D21's reading): an observation lost costs at most one start.
- **No HTTP route** (D47 §4), like the usage it sits beside.
- **Not in `harnesses.json`.** That file is the person's wiring, written by two editors. The driver writing it at each
  refusal would race an edit from the screen or the terminal, and a cool-off is an observation, not a choice.
- **On disk, not in memory as AGT3b's refusals are.** A reset at 07:52 must survive a restart at 03:00. A refused key
  waits for a person; a limit waits for a time. Two states, two rules.

### 2.4 How it shows

- **Settings → Agents**, on the account's row: *Cooling until 07:52 (<zone>) · in 3 h 10 min · the agent said so*, or
  *· Daoris's default: the agent named no time*, or *· this machine's zone assumed*, with *Try now*.
- **The quest's reason**, wherever a consideration is shown: the hold's sentence (§4).
- **What needs you**, while every account a start may use is cooling (§4).
- **A conversation's opening**, when it opened on another account (§3.3).
- **The terminal**: `daoris agent list` prints each account's cool-off the same way, e.g. `account-1  signed in  cooling
  until 07:52 <zone> (in 3 h 10 min) — the agent named the time`. The headless driver's tick line says the hold, and its
  console the attention line once (§4).
- Times are shown in the machine's zone, with the zone named. The labels are translated; the agent's own sentence,
  where it is shown, is content and is not (`translation-parity`).

## 3. Rotation

### 3.1 The order

The person's list of the accounts rotation may use, per agent (the account's owner), **in `harnesses.json`**:

```json
{
  "rotation": { "claude-code": ["account-1", "account-2", "account-3"] },
  "workspaceRotation": { "work": { "claude-code": ["account-2", "account-3"] } }
}
```

- **Resolved as a default is**: the start's workspace's order for that agent, else the machine's, else none. A work
  circle rotates among its own accounts, and a personal account stays out of it.
- **None is today's behaviour, byte for byte**: a cooling account holds its starts and nothing moves (D48 §2a).
- **Do-not-rotate is absence from the order.** An account the applicable order does not list is never rotated into,
  and work resolved to it is never rotated away from it. The screen shows that as a switch on the account's row,
  *Rotate*, whose off is the absence. One list, not a list and a mark.
- An order naming an account that does not exist, or one account twice, is refused by both doors, as `profile default`
  refuses a name that does not exist.

### 3.2 When

**Only at a start**: a driven start, a carry-on (D80), a resume (D79), an intake, a new conversation, Ask Daoris's
opening. **Never inside a running session or conversation.** The account is the process's configuration home, set at
spawn (D49 §4), and a harness keeps its conversation where it keeps it, which Daoris does not read.

### 3.3 Which account

The account the resolution names (pick, workspace, machine, none) runs if it is **ready**: not cooling, not refused
(AGT3b), signed in or holding its key. **Rotation never moves work off a ready account.** If it is not ready:

| The account was named by | Then |
|---|---|
| the person's pick (a conversation) | refused: the cool-off's sentence, and the accounts that are ready, by name. The person chose it |
| nothing: the tool's own home | the start waits (§3.4) |
| a default, and the applicable order lists it | the next account after it in the order, wrapping, that is ready |
| a default the order does not list, or there is no order | the start waits |

- **Ready is what `SelectAsync` already asks**, with the cool-off read first: a file read, before any probe. Cooling,
  refused, signed out or keyless are walked past alike.
- **A trust hold on the chosen account** is said as any trust hold is (D73), naming the account and the folder.
  Rotation does not walk past it: trust is the person's grant, per account, and walking past would quietly move the
  work to a third.
- **Back when ready.** Once the resolved account's reset passes, new starts run on it again. Nothing sticks to the
  account a start rotated to.
- **A new conversation rotates like a start**, since it holds no context yet, and its first line says which account it
  opened on and why (*opened on account-2: account-1 is cooling until 07:52*).

### 3.4 What never rotates

- **A running session or conversation** (§3.2).
- **An account the person picked** for a conversation. The picker showing the default is not a pick.
- **The tool's own configuration home**: the person's own sign-in, the one they use at their own terminal. Daoris never
  spends it in place of a listed account, and never moves work off it (§3.7). A machine that named no accounts behaves
  as before, and its starts wait out the cool-off.
- **An account outside the order** (§3.1).

*A session holding a person's sign-in context*, which the ask names, is read here as the last three: a conversation
the person is in, an account they chose for it, and the tool's own home.

### 3.5 Carrying a stopped session on to the next account

**What was done by hand on 2 October**: the owner added an account and resumed every stopped agent, and each carried
on with its context. **What can be done by hand in Daoris today**: make another account the default, and the next
tick's carry-on runs on it (§0.1). **What happened by accident on 1 October**: the person's sign-in at their terminal
moved Daoris's sessions, which named no account, to another account, and no record says so (§0.2).

**What Daoris does, once built:**

1. The refused session concludes `failed` in the agent's words (ACPEND1), its record saying `limit` (§5.2), and its
   account cools (§2).
2. At the next look the carry-on is planned as D80 plans it, behind the person's hold, busy and the cap, in the same
   tree.
3. Its selection rotates (§3.3). If no account is ready, it waits (§4).
4. Its instruction says what D80's says (the quest is already its own, what cut the last session off, the changes it
   left uncommitted), and two things more, from **Daoris's own record** of the cut-off session (D76): its **last plan**
   (the harness's to-do list, as the conversation keeps it) and its **last words**, bounded. After a limit it also says
   the last session ran on another account that is now cooling. This applies to every carry-on, since a cut-off for any
   reason loses the same thread.
5. **What does not travel is the harness's own conversation.** It lives wherever the harness keeps it for the first
   account, which Daoris never reads or copies (D49 §4, D66 §3). So the carried-on agent starts a fresh conversation
   from the tree and Daoris's record, as every D80 carry-on does today. In the maker's CLI on 2 October the conversation
   outlived the account; Daoris's accounts are separate homes by design, and what crosses between them is Daoris's.

### 3.6 Reproducible: the records and the log

- **Each session record names its own account** (loopback only, D47 §4) and its tool's version (D49 §4), unchanged.
  The quest's sessions, read in order, show the account each ran on.
- **The carried-on session's conversation record opens with a note**: *carried on from session 3f9c2a71 on account-2;
  account-1 is cooling until 07:52 (<zone>), as the agent said; its turn 1 was refused with 370,104 tokens of context*.
  The turn is the refused session's own (the turns its record ended, plus one), and the context its usage high-water.
  The conversation record is the machine's (D76).
- **The machine log** writes `account.limited` and `account.rotated` (§5.4).
- 🔴 **The session's note, which travels, never names another account.** `SessionNote.ForAnotherMachine` elides only the
  record's own account, so account-1's name in the note of a session that ran on account-2 would reach a teammate. The
  note says *the account it ran on is cooling until 07:52* and *carried on from session 3f9c2a71 on another account*.

So a rotated piece of work reads end to end, from Daoris's records: which tool, which version, which account, which
tree, which turn, and why it moved. What no record holds is the harness's own context, which no carry-on has had.

### 3.7 Rotation needs accounts of Daoris's own

**Yes: every account rotation may use is a named account, one configuration home each.** Daoris chooses an account
only by choosing its directory (the profile variable, D49 §4). The tool's own home holds whichever account the person
last signed in to, at their own terminal: Daoris neither chooses it nor is told when it changes, and a session that ran
on it names no account in its record. So the own home can be cooled, as it was spent, and its starts wait; it is never
in an order, never rotated into and never rotated away from (§3.4). Rotation begins when the person signs Daoris in to
a second account (*Sign in to another account*, `daoris agent login claude-code --new`, D66 §3) and lists both.

**The own home's cool-off**, kept under `""` like any other, means *whoever was signed in there when the limit came*.
After the person signs in to another account at their terminal it no longer describes what is there, and Daoris cannot
tell. So the own home's cool-off also ends on the roster's refresh (§2.3), and its sentence says so.

**What the screen says while sessions share the person's own sign-in**, on Settings → Agents, under the agent, whenever
a start would run on the tool's own home (no workspace or machine default named):

> *Sessions run on your own sign-in*, *<who>*, *the account this tool uses at your own terminal. Signing in to another
> account there moves Daoris's sessions with it, and their records cannot say which account ran. Daoris cannot rotate
> it. Sign in to an account for Daoris to give its sessions accounts of their own.*

with *Sign in to another account* beside it. `<who>` is the tool's own answer, read fresh on the probe and kept nowhere
(D66 §3); where the tool gives none, the line says *your own sign-in* alone. While that account cools, the line ends:
*It is cooling until Oct 3, 16:02 (<zone>). If you have signed in to another account since, refresh.* A session that ran
there shows *your own sign-in* where its head shows an account. `daoris agent list` says the same in one line, naming
`daoris agent login <agent> --new`.

## 4. When every account is cooling

*Every account a start may use* is the resolved account and, where an order applies, the rest of the order. With no
order it is the one account, which is observation 4's case: the carry-on now waits for the stated reset rather than
starting into the same refusal twice and parking the quest.

- **The start is held at spawn**, with one sentence: *every Claude Code account this start may use is cooling; the
  first ready, account-2, at 07:52 (<zone>)*, or with one account, *account-1 is cooling until Oct 3, 16:02 (<zone>), as
  the agent said*. The quest's consideration becomes `Blocked` with it, as for any hold at spawn. (Not
  `StartVerdict.Exhausted`: that word is the quest's strikes, DRV6, and nothing about the quest is wrong.)
- **A waiting look starts no process.** The cool-off is read before any probe, so nothing is spawned and nothing is
  probed while every account is cooling. The loop keeps looking, because the person may press *Try now*, change the
  order or sign in to another account, and another workspace's order may have one ready.
- **Said once.** The tick report carries the wait (the agent, the order's scope, the first ready time, the quests
  held), and the attention watch says it when it first appears, as it says a park once: a toast on the desktop, a line
  on a headless console. *What needs you* holds a row while it lasts. The log writes `starts.waiting` once per wait.
- **The queue waits in its order.** Nothing is reordered, skipped or parked. At the first reset, the next look starts
  the oldest held quest on the account that became ready.
- **No spinning**: no retry between looks, no backoff, no question to any provider.

**What the person sees meanwhile:**

- **The refused session**, in the quest's sessions and on its own page: `failed`, the agent's words, and *its account
  is cooling until Oct 3, 16:02 (<zone>)*.
- **The quest**, wherever its reason is shown (the Overview, its page, the tick's line): *waits for an account*, with
  the hold's sentence. It is not parked and shows no Retry: nothing about the quest needs the person's judgement, and it
  starts by itself at the reset. Where an order could take it sooner, the sentence names the doors: add an account to
  the order, or sign in to another.
- **Settings → Agents**: the account's cool-off (§2.4), with *Try now*.
- **The attention and *What needs you***, once, while every account is cooling, as above.
- A quest already parked on its strikes before this lands stays parked behind Retry (RETRY1), as today.

## 5. Cost and safety

### 5.1 Nothing spends more

Rotation moves work that would have started anyway onto another account the person listed. It starts nothing new, and
it never spreads work across accounts to share a load. A cooling account is never started on, so a spent account is
spent **less** than today: today the carry-on starts the spent account again until three strikes park the quest.
Observation 2's two refusals named the same reset, and observation 4's carry-ons started twice into it and parked the
quest within seconds.

### 5.2 A limit is not a strike

A `failed` record that says `limit` (a field on the record, beside D104's `interrupted`, naming no account) is **not a
strike** (D58 amended). Observation 4 is the case: three refusals by one spent account, two of them three events long,
parked a quest behind the person's Retry. A strike says trying again spends an account without progress. A limit is
the account's state, with its own reset, and the cool-off bounds it: an account meets a limit at most once per window
it names, so a quest's limit cut-offs are bounded by the accounts the person listed and their windows. The quest waits
for the reset, or rotates (§3), and is never carried on into the same refusal. A timeout is still a strike, and so is a
limit the table does not recognise.

**The risk left**, named: a quest whose every session spends a whole window and lands nothing keeps spending the listed
accounts' windows. It is never silent (every limit is a note and a log line, every wait an attention), and the usage
report counts limit cut-offs per quest (TOOL4h). §8 lists it as a real run's question.

### 5.3 No credential leaves the machine

Nothing reads inside an account's directory (D49 §4, D66 §3). A key is read only to hand it to the harness at spawn, as
today (D67 §1). `cooling.json` and the order hold profile names only, and neither has an HTTP route. The record's
`limit` names no account.

### 5.4 The log

An account is recorded by its profile name: never a key, never the key's handle, never who signed in (D66 §3), and
never the agent's sentence (D94 §5). Null is the tool's own home.

| Event | Source | Data | Why it is kept |
|---|---|---|---|
| `account.limited` | desktop, driver | session, adapter, account, hit, window, until, stated, assumedZone, turn, used | a limit met: which account, which window, until when, said or defaulted, at which turn and context |
| `account.rotated` | desktop, driver | session, adapter, from, to, carries | a start that ran on another account because its own was cooling; `carries` is the cut-off session, or null |
| `starts.waiting` | desktop, driver | adapter, workspace, until, quests | every account a start may use was cooling, written once per wait |

## 6. The doors (D50, D110)

| What | The screen | The terminal | Ask Daoris |
|---|---|---|---|
| The machine's order | Settings → Agents: the agent's accounts in order, with up and down, and *Rotate* on each | `daoris agent profile order <agent> <account>…`, `--clear` | the agent kind's `order` door, a card the person applies |
| A workspace's order | where the screen sets that workspace's default account | the same with `--workspace <name>` | the same door, with the workspace |
| A cool-off ended early | *Try now* on the account's row | `daoris agent profile ready <agent> <account>` | a `ready` door, a card |
| The default cool-off | Settings → Driver | `daoris driver cooloff <minutes>` | the setting kind's `cooloff` |
| What is cooling | the account rows, the quest's reason, *What needs you* | `daoris agent list`, the headless tick line | the room's machine facts name each cooling account and its time |
| Sessions on the person's own sign-in | §3.7's line under the agent, with *Sign in to another account* | `daoris agent list`'s line, naming `daoris agent login <agent> --new` | the room's machine facts say that starts run on the tool's own sign-in |

- `ready` on an account that is not cooling says so and changes nothing. `cooloff` refuses less than 1: a zero cool-off
  is a spin.
- Each new door is held to its answer by `HelpCoverageTests` (D110). Adding an account to an order widens what Daoris
  may spend, and *Try now* spends sooner, so each is a card the person applies (D89).
- The new names (*Rotation*, *Rotate*, *Cooling until*, *Try now*) are designed in both languages and entered in the
  glossary, which the names check holds (D116).

## 7. The build

Rows ready for `TASKS.md`. Lanes are `daoris.lanes.json`'s ids; *docs* is the laneless group. D125 decides all of it,
so no row takes a decision number unless building it finds something D125 did not decide. A row with a rehearsal, a
`Process`-half case or a look in its proof is proven by the parent at merge.

| Row | What lands | Lanes | What proves it |
|---|---|---|---|
| **TOOL4a** | **The limit table and its reader** (§1.3, §1.5, §2.1): `HarnessToolchain.Limits` on `claude-code` and the stub; `AccountLimits.Read`, pure: the marker, the reset grammar, the zone rule, the grace, the margin, the 8-day bound; the five sentences as fixtures with their provenance, the zone replaced | driver | `AccountLimitsTests`: §2.1's rows, each failing first; the table test refusing a marker no recorded sentence matches and a sentence no marker matches |
| **TOOL4b** | **What the native door says** (§1.2): `rate_limit_event`'s shape and a limit's failed `result`, read keylessly from the maker's published SDK declarations and labelled unmeasured; then the first real limit on that door, whenever one happens, recorded verbatim | docs (evidence) | An evidence document. A table entry or a field reader follows only from a recorded frame |
| **TOOL4c** | **The record says a limit** (§5.2): `Session.Limit`, set with `failed`, stored, served off loopback and synced, since it names no account; a record from before reads false. As `Interrupted` (D104) was added | service | Store, ledger, HTTP and sync tests: the flag round-trips and travels; an older record reads false |
| **TOOL4d** | **The cool-off and the hold** (§1.2, §2, §4, §5.2, §5.4), after TOOL4a and TOOL4c: `cooling.json`, its writer and reader; a recognised limit cooling the account from a driven session, an intake and a conversation; the native door's failed result handed to the conclusion (an empty table: no change); `SelectAsync` holding a cooling account first, before any probe, with §4's sentences; `ReadStrikes` passing a `limit` record; the client sending `limit`; a cool-off ended by a sign-in or key into its account, and the own home's by the roster's refresh; the note never naming another account; the tick report's wait and the attention said once; `account.limited` and `starts.waiting`; the default as a constant until TOOL4e | driver | Fast-half tests for each, failing first. A `Process`-half tick, observation 4 replayed: the stub ACP agent answers with its sentence, the account cools until Oct 3, 16:02 in the test's zone, the carry-on is held with no strike counted, the next look spawns nothing, the quest is never `Exhausted`, the attention is said once |
| **TOOL4e** | **The twins and the terminal** (§2.2, §2.4, §3.1, §6), after TOOL4d: `harnesses.json`'s `rotation` and `workspaceRotation`, read and written by both twins (🔴 `HarnessSettings.Save` writes them, or a screen edit deletes the order); `daoris agent profile order` and `ready`; `cooling.json` in `daoris agent list`; `driver.json`'s `cooloff`, both twins, and `daoris driver cooloff`; the golden usage; `twins.md`'s rows | cli; driver | The twin tables on both sides; a file each twin writes, written again by the other, keeps every section; `node --test`; the golden usage |
| **TOOL4f** | **Rotation** (§3), after TOOL4e: `SelectAsync`'s walk; a pick and the tool's own home never rotated; a conversation rotated only at its opening, and its first line; the carry-on's instruction with the last plan and last words (§3.5); the carried-on session's opening note; `account.rotated` | driver | `SelectAsync` tables for §3.3's rows. A `Process`-half tick: account-1 cooling, the order `[account-1, account-2]`, the carry-on runs on account-2 in the same tree, its record names account-2, its note names the cut-off session and no other account, the log line; account-1 ready again takes the next start |
| **TOOL4g** | **The screen and Ask Daoris** (§2.4, §4, §6), after TOOL4f and FRAME1's Settings frame: the account rows (order, *Rotate*, cooling, *Try now*); §3.7's line while starts run on the person's own sign-in, and *your own sign-in* on a session's head; the quest's *waits for an account* with no Retry (§4); Settings → Driver's cool-off; the *What needs you* row and the toast; the modules' routes; Ask Daoris's `order`, `ready` and `cooloff` doors and the room's cooling facts; English and Chinese, named by D116 | modules; web-settings; web-shell; driver (the doors, the room) | Modules tests (MOD5's three per route); vitest over a mocked bridge; `HelpCoverageTests` and `HelpProposalKindsTests`; parity and the names check. The look on the window, both themes and both languages |
| **TOOL4h** | **The rehearsal and the report** (§8, §2.2), after TOOL4f: a family rehearsal phase (two stub accounts; the first refuses with a recorded sentence; it cools until the stated time; the carry-on completes on the second; both refusing holds starts and says so once); the usage report's limits: per account and week, stated against default, rotations, waits, limit cut-offs per quest | tools | The rehearsal phase; the report's parse table |
| **TOOL4i** | **A real rotation on the install**, the owner's run, after a republish carrying TOOL4a and TOOL4c–TOOL4f: two of the owner's accounts, each signed in as an account of Daoris's own (§3.7), not the tool's own home, in the order. Either the next limit that comes, or deliberately while one account is known to be at its limit with its reset stated (as on 1 and 2 October): that account first, a ready one second, a small quest. An evidence document: the refusal's words on the protocol door (is a weekly limit worded there as the CLI words it?), the cool-off to the stated time in the stated zone, the carry-on on the second account in the same tree, both records and the log naming both accounts and the turn, and at the reset new starts back on the first | none (a run) | The evidence, with the owner present |

**Order.** TOOL4a, TOOL4b and TOOL4c any time, each in its own lane. The driver lane runs TOOL4a → TOOL4d → TOOL4e →
TOOL4f. TOOL4g and TOOL4h after TOOL4f, in their own lanes. Then the owner's run. **TOOL4d alone already ends today's
waste**: observation 4's quest would have waited for its reset, unparked, instead of spending its strikes in seconds.
It is worth landing before rotation is, and it helps a machine with one account as much as one with three.

## 8. What a rehearsal can prove, and what only a real run can

**A rehearsal proves the mechanism**, with no model and no account: the recorded sentences recognised and their resets
read, the cool-off kept across a restart, a start held with nothing spawned, a carry-on rotated within its tree, the
records and the log naming both accounts, the attention said once, the order's two doors.

**Only a real run proves the rest:**

1. **The protocol door's words for a weekly limit with no spend limit beside it.** Observation 4 carried a weekly reset
   behind a spend limit on that door; observation 3's bare weekly limit was the maker's CLI outside Daoris.
2. **The native door's words**, and what a `rate_limit_event` carries (TOOL4b).
3. **Whether a reset lands at the stated minute**, and whether 2 minutes of margin is enough. (How fast a start on a
   spent account is refused is answered once: at once, three events, observation 4.)
4. **Whether a carried-on session on another account finishes the work** from the tree and Daoris's record, without the
   harness's own conversation.
5. **Whether an hour is a good default**, from the report's stated-against-default count.
6. **Whether a quest spends windows without landing anything** (§5.2), from the report's limit cut-offs per quest.

## 9. Considered and rejected

- **Reading the transcript, the agent's messages or its tools' output for the sentence.** A session that quotes a limit
  would cool an account that is not spent.
- **Asking the provider what is left**, by its console, a quota route or response headers: a credential and an API
  Daoris has no business holding (D49 §4, D57 §6).
- **Guessing from token counts** (`usage.json`, `_meta.quota`): what was used is not what is left.
- **A 429 reader.** No door of Daoris's sees one; Daoris makes no model call (D24).
- **One cool-off for every limit.** The sentence says when; five hours would be wrong for a weekly limit, and an hour
  wrong for both.
- **Exponential backoff.** D58's reason, and here the reset is stated.
- **Spreading work across accounts** to share a load: it spends windows the work did not need, and makes a quest's
  sessions harder to read.
- **Rotating inside a running session.** The account is set at spawn, and the harness's conversation is not Daoris's.
- **Copying the harness's conversation into the next account's home** to carry the context: a read inside an account's
  directory (D49 §4, D66 §3).
- **Handing the carry-on the path of the cut-off session's record.** A machine path lands in whatever a session commits
  (D124's reason). The plan and the last words go in the instruction.
- **A do-not-rotate list beside the order.** Two lists that can disagree, about one account.
- **Rotating into or away from the tool's own home**, or away from a person's pick. The own home is whichever account
  the person last signed in to; Daoris can neither choose it nor say afterwards which it was (§3.7).
- **Keying the own home's cool-off by who was signed in**, to tell when the person signed in elsewhere: it would keep
  who, which the probe reads fresh and keeps nowhere (D66 §3). The refresh ends it instead.
- **Giving the own home's sessions a profile silently**, to make them rotatable: a session pointed at a fresh home is
  signed out, which is why no account named means the tool's own home (D48 §2a). The person signs Daoris in to an
  account of its own.
- **The cool-offs in `harnesses.json`**: the driver writing the person's wiring at each refusal, racing the two editors.
- **The cool-offs in memory**, as AGT3b's refusals are: a reset outlives a restart.
- **A limit as one more `Refused` phrase.** A refused key waits for a person; a limit waits for a time.
- **A limit counted as a strike.** Rotation would park the long quests it exists to keep going, and the cool-off
  already bounds the spend.
- **A separate count of limit cut-offs that parks after some number**: a second register, and the number a guess.
- **Sleeping the loop until the first reset**: the person's actions, the intake and other workspaces' orders keep
  moving.
- **Parking or reordering the queue while it waits.**
- **`StartVerdict.Exhausted` for a cooling account**: that word is the quest's strikes (DRV6).
- **The other account's name in the session's note**: the note travels, and the scrubber knows only the record's own
  account.
- **Translating the agent's sentence**: it is content.

## 10. What this amends

Each row that builds a piece notes the amendment where it lands.

- **D57 §b**: the hold on rotation is lifted, because the evidence it waited for exists (§0.2). The toolchain design's
  §2 part 4 and §6 are this design now.
- **D58**: a `failed` record whose turn was refused for an account's limit is not a strike; its quest waits for the
  reset or rotates, and is never parked for it.
- **D80**: a carry-on's instruction carries the cut-off session's last plan and last words, and after a limit says the
  account changed.
- **D94 §4**: three events (§5.4).
- **D110**: the `order`, `ready` and `cooloff` doors.
- **D66 §3 / AGT3b**: an account action (a sign-in, a key) ends that account's cool-off as it ends a refusal, and the
  roster's refresh ends the tool's own home's.
- **Unchanged, and restated**: D49 §4 and D66 §3 (nothing read inside an account's directory; who signed in is kept
  nowhere), D48 §2a (no account named is the tool's own home, untouched), D67 §1 (a key handed at spawn), D73 (a trust
  hold is said, never walked past), AGT3b (a refused key is held in memory until a person looks).

## 11. The twins this creates

Each is added to `.claude/knowledge/twins.md` by the row that builds it (TOOL4e), with its test tables:

- **The order in `harnesses.json`**: `toolchain.ts` and `Harnesses.cs`. `rotation` and `workspaceRotation`; resolved
  workspace, then machine, then none; absent is no rotation; an unknown account or one named twice refused; each writer
  keeps the other's sections.
- **`cooling.json`**: written by the driver on a limit, read by both, an entry removed by both (`ready`, *Try now*). An
  entry's shape; a passed `until` is ready; missing or unreadable is none cooling.
- **`cooloff` in `driver.json`**: `driverconfig.ts` and `DriverConfig.cs`. Absent is 60; less than 1 refused.
- **Not a twin**: the limit table and its reader, which only the driver has. The CLI concludes no session.

## 12. What this document's gate does not cover

This change is documents only, and nothing is built.

- **Read from the code at `4f65cd3`**: `Observation.cs`, `Driver.cs` (`HoldAsync`, `CaptureAcpAsync`, `AccountRefused`,
  the conclusion), `Acp.cs` (`Complete`), `StructuredOutput.cs` (`ClaudeStreamJson`), `Harnesses.cs`
  (`HarnessToolchain`, `HarnessSettings`, `HarnessProbe.Apply`, `HarnessRoster.Refuse` and `SelectAsync`), `Adapters.cs`
  (the stub's toolchain), `Planner.cs`, `ParkedQuests.cs`, `ServiceClient.cs` (`ReadStrikes`), `Attention.cs`,
  `Usage.cs`, `ChatRunner.cs`, the service's `Sessions.cs`, `SessionNote.cs` and `ToSession`, the CLI's `toolchain.ts`
  and `cli/agent.ts`, the web's `bridge/conversation.ts`, and `daoris.lanes.json`.
- **The evidence is the install's and the owner's**, given to this branch by the parent in three messages; the
  transcripts and records were not read here. The zone in every sentence is elided. That observations 4 and 5 were one
  account rests on their naming one reset and on the sessions having named no account; no record could say more.
- **Not measured**: every item of §8.
- **Found while reading, and not filed by this branch**:
  1. AGT3b reads the transcript's last lines for `API Error: 401` (`Observation.Refused` over `LastLines`). A failed
     session whose own output ended quoting that phrase would hold its account until a person looks. §1.4's rule, the
     door's failure and nothing else, would answer it where the door carries one.
  2. The ACPEND1 note carries the agent's sentence to every reader, and the sentence names the machine's zone.
     `SessionNote.ForAnotherMachine` does not elide a zone.
- **`verify` checks** this document's links, the decision log's shape, the budgets and the duplicates, and none of
  these words.
