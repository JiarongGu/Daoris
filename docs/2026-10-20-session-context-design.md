# A session's context: the agent's own conversation where it holds, Daoris's record wherever the session moves (accepted, 2026-10-20)

**Standing:** accepted under D189 (SESSCTX1), settled under D191 on 2026-10-20 after two other makers' reads, Codex's and
Fable's, each finding checked against the code by the steward (§14). The design builds nothing; its rows (§11) do.

Read for it: D131 points 1-4 and its rejected shapes; D137 and the session messages design §1, §2.2 and §4; D163 and the
mixed-agents design §1, §2 and §6, with its MIXAGENT1a-i, 1a-ii, 1a-iii and 2a-i notes; D167 and the co-working design §1.1,
§1.3, §1.5 and §5; D183 points 2-4 and its HELPMODE1a2, 1a4, 1a5, HELPWIRED1, HELPMODE1c2a and ACCESSSWAP1 notes; D184 and its
AUTONOMY1b and 1c notes; D188 points 1, 5 and 6; D191; D125 point 4; D165 point 3; D143; D160's rejected compaction; D24; the
session economy design §1.6-§1.7; the subagent load evidence §2-§3; `session-record-fields.md`, `model-decoupling.md` and
`claims-need-checks.md`. Every file reference was read again at `d0e51b54b` as the design was settled (first at
`4e12716b9`). A statement about what a harness does says where it comes from: **read** (from the maker's package or source,
read by an earlier design, or from Daoris's code that speaks to the harness), **recorded** (from this repository's own
measured records), or **not measured**. This design ran no turn.

## 0. The direction, and what is true today

The owner set the direction on 2026-10-20: Daoris manages a session's context, so that a conversation can change its
permissions, and even its agent, mid-session; holding the context is also what later work builds on. ACCESSSWAP1 took the
first step for Ask Daoris, merged the same day (D183's ACCESSSWAP1 note): an access change ends the agent's process at its
turn's end, and the same conversation resumes under the new access at its next turn. ACCESSSWAP1b did the same for a driven
session's permission prompts, on both doors (D183's ACCESSSWAP1b note). This design decides the rest.

**Where a conversation's context lives.**

| What | Where | Who reads it | Source |
|---|---|---|---|
| The agent's own conversation: every turn, tool results whole, its thinking, its compactions | the harness's store, in the configuration home of the account it ran on (Claude Code's transcript, Codex's thread) | the harness, on a resume | D131 point 2; D125 point 4; `Continuation.cs:139-143` |
| Its id | `<home>/sessions/<id>.harness.json`, one per record, replaced when the wire names another | Daoris | `HarnessConversations.cs:20-43` |
| Daoris's record of it | the session record; its events, `<id>.events.jsonl`, machine-local, a text cut at 64 KiB and a tool call's input and output at 2,000 characters; the verbatim transcript; `said`; the kept attachments | Daoris, the page, the terminal | `SessionEventLog.cs:9-24`; `SessionEvents.cs:358-362`, `:423-435` |

**What moves a conversation, and what the agent knows after.**

| The change | Today | What the agent knows after |
|---|---|---|
| Access, the same agent and account | the same conversation resumed under the new access at its next turn: Ask Daoris's (ACCESSSWAP1) and a driven session's (ACCESSSWAP1b, D184's AUTONOMY1b note) | its own conversation |
| Another account of the same agent: D165's move, a pick, a rotation after a limit | a new part: a carry-on or a hand-over (D125 point 4, D165 point 3) | the request as first written, the last plan, the last words, what is uncommitted |
| Another agent (D163) | a new part: the hand-over | the same four facts, with *None of its own context carries over to you* (`HandOverRecords.cs:111-112`) |
| Its resume refused: `unkept`, `unable`, `offered`, `gone`, `refused`, `elsewhere` | a driven record: a carry-on handed the four facts; a conversation or a closed quest's session: the words wait, marked, and *Start a conversation with these words* is the person's press (`ChatGoOn.cs:60-62`) | the four facts, or nothing |

Two of those codes say more than one thing today. `gone` is the protocol door's `resource_not_found` (`Acp.cs:2390-2396`), and
also the native door's harness ending on a failed exit before it named its conversation (`ChatRunner.cs:1840-1846`,
`DriverContinuations.cs:427-432`), which a crash, a failed sign-in or a flag the harness rejects ends the same way.
`refused` is the agent's own refusal on the wire (`Acp.cs:2395`), an adapter no longer installed (`ChatGoOn.cs:283-301`,
`DriverContinuations.cs:113`, `:124-126`), and a resumed run that could not start (`DriverContinuations.cs:294-299`,
`:346-352`).

So a session changes agent or account only by losing every word the person said between the first and those still waiting,
every answer but the last, and what each turn did. An access change keeps everything while the resume holds, and loses everything where it does not.

## 1. Two contexts

- **The working context is the agent's own conversation.** It is exact: the harness keeps every tool result whole, the
  agent's thinking and its own compactions, and it is the only place they are. It is resumed wherever D131's conditions hold.
  One record holds one, and never more (D131 point 4, D167 point 1).
- **The portable context is Daoris's record.** It is what Daoris saw of every turn it ran: the words the session took and
  who said them, the agent's messages, the plan, each tool call's kind, title, files and status, Daoris's notes, the kept
  attachments, and the tree. It goes to any agent, any account and any access as the brief (§3). Daoris already keeps all
  of it: this design adds no store of the conversation.

A session, on the window, is its entry (D167). A move to another conversation is a new part of the same entry, so the person
still sees one session, with a part for each conversation in its *Parts* strip.

## 2. The rule: resume where the conversation holds; a new part where it is proven lost; otherwise the person decides

The conversation resumes where its record has the same agent and account, its tree stands, its id was kept, and the door
can resume (D131 point 2, `Continuation.cs:169-201`). The access is not among the conditions: the protocol door asks for the
posture on every resume (read: `Acp.cs:2407-2408`), and the native door names `--permission-mode` on every run (read:
`Adapters.cs:1987-1995`, `:2045-2046`). Asking is not having: a posture the agent does not offer is refused where it is
required, as full access is, and left as it was where it is not (read: `Acp.cs:1920-1944`), and neither door's resume under
another access has run here against a real harness (ACCESSSWAP1b's proof ran the stub, D183's note; §12). §2.1 says what a
resumed start keeps of it.

Where the agent or the account changed, the conversation cannot go with it. Where neither changed, the resume is tried, and
what its refusal proves decides:

| The change, or what the resume met | Resumes? | Then |
|---|---|---|
| Access | yes, where the rest holds (ACCESSSWAP1; ACCESSSWAP1b for a driven session) | the same part, under the new access asked for (§2.1) |
| Account | no: the conversation is in the other account's home (D131 point 2) | a new part, `from` with the move's why |
| Agent | no | a new part, `from` with the move's why |
| Its loss proven: the protocol door's `resource_not_found` (`gone`), `unkept`, `unable`, `offered` | no | a new part by itself, handed the brief: `from` why `resume` for a conversation or a closed quest's session, its access changed or not; `retry` for a driven record's fallback (D131 point 3), whatever moved it |
| The native door's harness ended on a failed exit before it named its conversation (`gone` today) | tried once more, as a resume | where that ends the same way, held for the person, said why, the brief ready |
| The agent refused it on the wire (`refused`), or another client holds it (`elsewhere`) | no | held for the person, said why, the brief ready |
| The adapter it ran on is no longer installed (`refused` today) | no | held for the person with the hand-over's offer: they choose the agent |
| The resumed run could not start (`refused` today) | no run began | a start failure, counted as a failed start is today; never a move |
| Its tree gone; a teammate's record, an intake, a stand-down | no | as today (D131, D137 §2.2) |

- **One rule for every record whose resume is judged**: a driven record, a conversation and a closed quest's session. What
  differs is its new part's why: `retry` where a driven record falls back, `resume` where a conversation goes on by itself.
- **Of the refusals, only a proven loss moves a session by itself.** The agent's `resource_not_found` says it no longer has the conversation;
  `unkept`, `unable` and `offered` say no conversation was kept or none can be resumed here. Nothing else proves the
  conversation gone, so nothing else ends its line without the person.
- **The native door's failed exit proves nothing of loss.** Both of its sites mark `gone` on any non-zero exit before the
  conversation was named. It is tried once more as the same resume (the same id, account and access), and where that ends
  the same way it waits for the person. SESSCTX1c gives it a code of its own, or a detail on `gone` as `account` carries one
  (`ContinueWhy.AccountHeld`, `Continuation.cs:122-130`); either way a note already written `gone` reads as it did.
- **`elsewhere` is Codex's alone.** It is `codex-acp`'s `thread_active_writer`, read from the refusal's structured data
  (`Continuation.cs:73-77`, `Acp.cs:2390-2396`): another client holds the conversation, so its context is not lost and a
  second writer in the same tree could redo its work. On Claude a second writer is not detected, so no resume there is held
  for it.
- **Held for the person** means the words wait on the record, marked, the record says why, and the brief is ready if the
  person goes on in a new part. An adapter no longer installed offers the hand-over (D163 point 1), since only the person
  can choose the agent the session goes on with.
- **A resumed run that could not start is a start failure.** Its record goes back with its words (`Continuations.PutBack`),
  as a failed spawn's does, and is counted as a failed start is today; no part is opened for it.
- **The old part ends at its turn's end**, never mid-turn (D188 point 1), and before the new starts, since a tree is held by
  one session at a time (D51; D163's MIXAGENT1a-i note, point 3). A quest's session, whose turn is its whole quest, is
  stopped first by the person's press, as D163's MIXAGENT1a-ii note has it.
- **The new part starts in the same tree** on the agent, account and access chosen. Its instruction is composed afresh from
  the setup as it stands (D184's AUTONOMY1c note), and its room and servers are handed, and recorded, as at any start (D183
  point 4). It is handed the brief (§3.4 says where it stands in each kind of part), then the words waiting in `said` (D188
  points 5-6), which leave the old record by MSG1d's went note.
- **A conversation, or a closed quest's session, whose own conversation is proven lost goes on by itself**, as a new
  conversation in its tree and a part of its entry, said why. D137 §2.2 made it the person's press because a new
  conversation had none of the old one's context; the brief is that context. An access change whose resume finds its
  conversation lost goes the same way, so no access change ends a conversation's line. *Start a conversation with these
  words* stays, as the person's own press for a fresh start.
- **A driven record's fallback (D131 point 3), a carry-on after a limit (D125 point 4) and a hand-over (D163)** stay what
  they are, a new part each, handed the brief in place of the four facts.
- **A left conversation is not resumed again.** Once its line has moved on, it lacks what came after. It stays in its
  account's home, never read (D125 point 4), and never copied or removed by Daoris. Going back to that agent or account
  opens another new part.

### 2.1 What a resumed start says of its access

A resumed start keeps the access it asked for, and the mode the agent reports as current where the protocol reports one:
`currentModeId` on the resume's answer, or a `current_mode_update` after it. Where none is reported, it says the mode is not
confirmed. Today neither is kept: `SetPostureAsync` reads `currentModeId` only to skip asking for a mode already current and
discards `session/set_mode`'s answer (read: `Acp.cs:1946-1954`), a `current_mode_update` is dropped (`:2134`), and the
receipt keeps what was asked (D183's HELPWIRED1 note). The native door reports no mode; whether its `--permission-mode` holds
on a `--resume` is SESSCTX1a's to measure, and until it is, a native resumed start says its mode is not confirmed. So an
access change that did not take is said, never assumed.

### 2.2 The outgoing agent's closing note

- **Where the account forces the move** (`near`, `limit`, `curve`), the outgoing agent is asked for nothing: the account
  being left is near or past its limit, and the agent's last message of each turn is already kept.
- **Where the account has room and the move is decided while the outgoing agent can still take a turn** (the person's
  press, a change of agent), it is asked at its turn's end for one closing note, at most 4,000 characters, kept as its claim
  and carried as §3.1 item 6. A move never waits more than that one turn; a note not written in it is said absent.
- **An access change whose resume is refused asks none.** The refusal is met only at the resume, after the part already
  ended at its turn's end (D183's ACCESSSWAP1 and ACCESSSWAP1b notes), so no turn is left to ask, and a lost conversation
  cannot be asked at all.
- **A quest's session stopped for a move asks none.** Its turn is its quest (D163's MIXAGENT1a-ii note), so it has no
  turn's end to ask at; its outcome is its tool lines (§3.1 item 4).

## 3. The brief

### 3.1 What it carries

A pure function, `ContextBrief.Compose`, which replaces `HandOverBriefing.Compose` (`HandOverRecords.cs:92-138`), composes it
from the records of the line the new part continues: the part it moves from and the parts it continued, one chain back,
never the entry's other parts running beside it. Today `from` links only a hand-over and a move onto another account (read:
`Driver.ContinuesFrom`, `Driver.cs:3371-3393`); a carry-on on the same account and a driven fallback write none. So SESSCTX1b
first gives every continuation its link to the part it continued, with a why of its own (§7), and the brief follows that
chain. No model writes any of it.

1. **Its opening**, in Daoris's one English: which session it continues, on which agent it ran, why it moved, and that what
   follows is Daoris's record of the conversation, not the agent's memory.
2. **The request** as first written, at most 4,000 characters, as today; a conversation's is its first word, not repeated in 3.
3. **Every word the session took**, in order, marked by who said it as its record marks it: the person's, verbatim; another
   agent's answer or findings (its `Opinion`, `SessionEvents.cs:174-180`) and a channel's message, as that agent's claim
   (D155 point 7, D167 point 7). A channel's message is told by its own mark on its `user` event, which SESSCTX1b adds (§7),
   never by the English Daoris wraps it in. Daoris's own composed prompts are not repeated, since the new part's instruction
   is composed afresh. One word is one id, as its latest event says: a word shown when it was said and again when it was
   taken, or told again for its level, counts once (read: `SessionEvents.cs:182-196`), and a word taken back before it was
   sent is left out (`:60-64`, `:85-90`). A word the session already answered is a past exchange, kept with the turn that
   answered it, never an open instruction. Words still waiting in `said` are not in the brief; they follow it (§2).
4. **Each turn's outcome**: the agent's last message of the turn, its end kept, at most 4,000 characters; and one line of what
   the turn did (the files it edited, the commands that failed), from the tool calls' kind, title, locations and status as
   the events keep them (`SessionEvents.cs:246-249`, `:266-267`). A driven part stopped for a move has no last message for its
   interrupted turn: its outcome is its tool lines. **The last check each part ran** is said with its title and status. A
   check is a tool call whose command, as its kept input has it, matches a gate the repository declares (its
   `daoris.gates.json`, read as `HeavyGates.Read` reads it, `HeavyGates.cs:30-47`), or this machine's heavy-gate mapping for
   it (`DriverConfig.cs:351-357`, `HeavyGates.Resolve`, `HeavyGates.cs:105`); never a guess from a title. The last one that
   failed also carries its kept output, up to 2,000 characters (`RawLimit`, `SessionEvents.cs:361-362`), so a fresh agent
   need not run it again to learn what failed.
5. **The work's state**: the tree's head, read as the brief is composed, since a part's end commit is not recorded (only its
   start's, `BaseCommit`, `TracedRecords.cs:41-42`, which `trace` says at `Trace.Words.cs:342`); the plan as last kept (50
   steps at most), with the turn it was written in, since the newest plan may predate later corrections (`LastPlan` returns
   the newest, however old); the questions the session asked that no word answered (a go-ahead or a helper's ask still
   open); what the tree holds uncommitted and the commits on it since the line's first part began (50 each at most); the
   files the person attached, by name and where they lie, as a resumed run is handed them (`ChatFiles.cs:105-110`). A
   conversation's rules allow a read of its own kept folder alone (`ChatRunner.cs:494-506`, the folder
   `ChatFiles.cs:34-38`; D52 refuses every other ask), so a new part's rules also allow a read of the kept folders of the
   parts its line continues, which the brief names.
6. **The outgoing agent's own note**, as its claim, at most 4,000 characters. One the agent writes by itself rests on
   MIXAGENT1c's tool, `handover_propose`; until that is built, the note is the closing note §2.2 asks for, where one is asked.
7. **The entry's other parts**, one line each (its quest, how it began, how it ended), as D167 §1.5 hands them today.
8. **What was left out**, by count, that the record holds it, and the tool that reads it (§3.5).

### 3.2 Its bound

**At most 40,000 characters, a provisional ceiling** until SESSCTX1a counts briefs on each harness. A Han character counts as
three toward it, provisionally, until those counts say how each harness counts one. Every item is clipped on its own and
each cut said. The sections fill under their own caps, with room kept for item 8's notice:

| Section | Its cap, in characters |
|---|---|
| The opening and the request (items 1-2) | 4,000 |
| The outgoing agent's note (item 6) | 4,000 |
| The work's state (item 5): a plan step at most 200, each list counted past 50 | 4,000 |
| The entry's other parts (item 7), counted past it | 1,000 |
| The person's words (item 3), the oldest after the request left out first, counted | up to 12,000 |
| The turns' outcomes (item 4), newest first | at least 12,000, and what the others leave |

The caps choose what is kept, not where it stands: the brief reads in the conversation's order.

Why 40,000: about 11,000 tokens at the estimate of 3.6 characters a token (recorded: the subagent load evidence §2; an
estimate made on English, since the tokenizer is the harness's, economy design §1.7). Chinese and code count differently, so
the estimate holds for neither until measured, and the weight on a Han character is a guess on the safe side. At that
estimate it is under a quarter of the 50,000 tokens a general-purpose subagent started at (recorded, the same §2). SESSCTX1a
counts briefs in both languages as each harness counted them, and the bound and the weight move on those counts.

### 3.3 What it never carries

- **The agent's thoughts, or a tool call's output but one.** The first is the agent's working, not the conversation's. The
  second is the tool's, not the conversation's: its raw input and output are kept cut at 2,000 characters and its text
  content up to 64 KiB (read: `SessionEvents.cs:358-362`, `:423-435`), and the tree holds the files themselves. The one
  carried is the last failed check's kept output (§3.1 item 4), since what failed is the work's state and a fresh agent would
  otherwise run the check again to learn it. So the brief is never the transcript whole, which D163 rejected and §9 keeps
  rejecting.
- **A summary written by a model**, Daoris's or any other (D163; D24). It would be a model call Daoris makes and pays for, a
  tier to name, and a claim nobody can check against the conversation. The agent's own words (its last message of each turn,
  its note) are kept as its claims.
- **An account's name, a key or a machine path** beyond where the kept attachments lie, which a resumed run is handed already.
- **Anything read or written in a harness's own store** (D131; D137 §4.2).

### 3.4 Kept as handed

- **In a conversation's new part**, the brief is its first prompt's event, with origin `target` and its CONTEXT1 account (D143
  point 1; `InstructionAccount.cs:6-28`): each section's characters and source, and what the bound left out and why. Today's
  briefing has one caller, the conversation's hand-over (`HandOver.cs:238-240`).
- **In a driven part**, the brief is a section of its composed instruction, in place of the four facts the `carry-on` section
  holds today (`Adapters.cs:459-467`), and is accounted within that instruction's `InstructionAccount`, whose sections sum to
  the instruction's length by construction (`InstructionAccount.cs:17-19`; the carry-on's account today,
  `InstructionAccounting.cs:96-100`, `:394-409`). A cut with no code of its own gains one in `HandedCuts`
  (`InstructionAccount.cs:222-280`), with the page's twin (`HandedCodesTests`, `handed.test.ts`).

`daoris-driver trace` and the page read both as they read an instruction's.

### 3.5 The line's history, as a tool

A read-only tool on the session's connector pages the events of the line its session continues, so what the bound left out
(item 8) is one call away rather than lost. The events are transcript-class and never cross HTTP (D76 §2): the tool reads the
records' event files on this machine (`SessionEventLog.cs:9-24`), for the parts of its own session's line alone, and refuses
any other record. The connector references the service alone (D183's HELPMODE1c2a note), so its reader shares no code with
the driver's and holds the same shapes, twinned as `HelperBox` and `HelperFiles` are (D163's MIXAGENT2a-i note), held by a
test on each side. SESSCTX1f builds it right after SESSCTX1b: a brief that counts what it left out, with no way to read it,
hands the agent a gap it can only guess at.

## 4. What the agent's own conversation still does

- It is the working context of its part, and the only exact one.
- It is resumed under D131's conditions, which is the cheapest way on and the only way an agent keeps its exact memory
  across processes. So the rule tries it first.
- It takes an access change at its resume (§2, §2.1).
- It compacts itself, as the harness decides (§6).
- After its part ends it stays in its account's home. The person may still open it in their own terminal (D137 §4.3); turns
  run there are not in the record, and a later move does not carry them.
- Daoris never claims what it holds (D137 §4.4): a resume hands the agent its own history, which it may have compacted.

## 5. The cost in tokens

**Read and recorded.** A harness sends the context it holds with every request; one recorded turn read 103 tokens from its
cache for each token it read anew (economy design §1.6, from COST1's and METER1's records). Daoris counts a turn's input,
output, cache reads and cache writes where its door reports them (read: `TurnModels.cs:114-139`, `SessionEvents.cs:110`,
`:298-299`), and never a price (`Usage.cs:37-40`, D24). The protocol door reports context as a turn runs, and the native door
when it ends (read: `Usage.cs:25-28`).

Let **H** be the part's context at the move, its high-water (`AccountRotation.ContextOf`, `AccountRotation.cs:662-678`); **S**
what a fresh start of the harness carries before any words (its system text and tools); **B** the brief.

| The way on | Its first request | Each later request |
|---|---|---|
| A resume, the cache holding the same prefix | reads H | reads H and what grew |
| A resume, the prefix changed or the cache gone | writes H | reads H and what grew |
| A new part | writes S and B | reads S, B and what grew |

**Not measured:** whether a resumed run's first request reads H from the cache or writes it again. That depends on whether
the harness sends the same prefix after a restart and under another access (a different tool set or posture text would change
it), and on how long its provider keeps a cache. Neither was read. SESSCTX1a measures it on both doors before any row relies
on it.

**What follows, and what does not.** A move writes S and B where a resume reads or writes H, and its later requests carry B
and what grows where a resume's carry H and what grows. But H here is the part's high-water (`ContextOf`), not the context a
conversation sends after its harness compacted it, so H against B bounds no saving: a resumed conversation may carry far less
than H, and a new part may spend turns reading back what the brief left out. So no way on is claimed cheaper; SESSCTX1a
measures the first and the later requests of both. The rule resumes first for continuity, not for cost: the direction asks
for a change of access or agent, and the agent's own conversation is the context it worked in, whatever its harness kept of
it after a compaction (D137 promises nothing of what an agent remembers).

**Kept.** Each move records H and B's characters on its line (§7). Each part keeps its own usage, with its cache reads and
writes as its door reports them, and *not measured* where the door says none (TOOL3).

## 6. Compaction

- **Inside a part, the harness compacts** as it decides. Daoris neither starts nor stops it, and cannot see what it kept.
  Read: the protocol adapter sets a turn's context reading at a compaction (limit-signals evidence §3), and a harness's
  compaction is a model call of its own (the dsh evaluation's boundary inventory, its D1/D24 row).
- **Daoris's own compaction is a new part handed the brief.** It happens at a move. The person may also choose it on the
  same agent and account, *Go on fresh*, held until SESSCTX1a's numbers show what it saves (SESSCTX1e).
- **No automatic threshold.** D160's reason holds: compaction saves only on the turns after it (replayed, compacting at 200K
  still left 56% of the cost, subagent load evidence §3), and a move also loses the exact memory. A threshold is proposed
  again only from measured moves.

## 7. How it is said

- **On the record.** Each move writes a coded line on both parts: from which agent and account to which, why, whether it
  resumed or was briefed, H and B. The new part's first line says it continues the session (D163's `chat.taken-over`,
  widened to the new whys).
- **The `from` link on every continuation** (SESSCTX1b). `fromWhy` gains `resume` (its own conversation was lost and it went
  on by itself), `carry-on` (a carry-on on the same account, after a cut-off, a time-out, a crash, a refused turn or an
  interrupted take) and `retry` (a driven record's fallback, D131 point 3), beside `press`, `near`, `limit`, `agent` and
  `curve`; `fromBy` is `daoris`, or `person` where the person's press went on. `from`, `fromWhy` and `fromBy` stay together
  or absent (D163's MIXAGENT1a-iii note), a stored shape, so a record from before reads as it did. The three are reasons a
  record continues another by, which no hand-over's box takes, as `curve` was before SWITCH1c: the box's check
  (`HandOverWhy.Known`, `HandOverRecords.cs:47-48`, read at `HandOverFiles.cs:46` and `:88`) stays on the five. The twins
  move in one branch, in both lanes: the driver's `HandOverWhy` with its `Briefed` and `Noted` English
  (`HandOverRecords.cs:4-49`) and `NoteCodes.Handed` (`NoteCodes.cs:510-517`), held by `NoteCodesTests` (`:388-398`, which
  lists the whys exactly and holds each to the box's check, so it splits with it); the page's `HANDED_REASONS`
  (`noteLines.ts:366-377`), held to the driver's by `locales/note.test.ts`, and `note.handed.why.*` in both catalogues
  (`locales/{en,zh}/note.json:186-190`).
- **The *Parts* strip says how a part began by its why.** Its reader takes any `from` as a hand-over
  (`Trees/SessionEntries.cs:128`) and reads a part without one by the prior record's state (`:129-138`), so a link on every
  continuation would show every carry-on as handed over. It moves with the whys: the five a hand-over's, `carry-on` as
  carried on, `retry` as retried, and `resume` as a move, a `PartBegan` of its own (`SessionEntries.cs:4-23`) worded in both
  languages (`entries.ts:10`, `PartsStrip.tsx:21-37`); a part with no link, a record from before, as today. The head's facts
  read `fromWhy` by the same map (`headFacts.ts:51-55`).
- **A channel's message is marked as one** on its `user` event, by a field of its own as `Opinion` is
  (`SessionEvents.cs:161-180`), written where the driver hands it (`ChannelWords.Claim`, `ChannelWords.cs:253-262`) and read by
  the page in place of the prefix it matches today (`CHANNEL_CLAIM_OPENING`, `channel.ts:35-40`; `ConversationView.tsx:369-372`).
  A record from before is still read by the prefix; the brief reads the field, never Daoris's English.
- **The refusal codes** split as §2 says, the native door's failed exit apart from `gone`, worded in the page's twin
  (`REASONS`, `say.ts:247-267`, held by `locales/note.test.ts`; `work.say.why.*` in both catalogues).
- **The machine log** writes `session.moved` {session, from, to, why, briefed}, by profile name as D125 point 7's lines are.
- **On the page.** The *Parts* strip says how a part began from a move (*moved from Codex: its conversation could not go with
  it*). The head shows the leading part's agent, and each part's cost is on its own row. Both languages.
- **To the agent**, by the brief's opening; **on the console**, when the move happens, as ACCESSSWAP1's line does.
- **D143 point 5.** The choice is which way a session goes on. Its facts are kept on the line's notes and the brief's
  account, and `trace`, the *Parts* strip and *Details* read them.

## 8. What the decisions become

- **D131.** Points 1, 2 and 4 stand: the resume is the first way on, its conditions are unchanged, and an access change rides
  it. Point 3's fallback hands the brief, its `from` why `retry`, and is taken by itself only where the loss is proven or the
  account, adapter or tree changed (§2): the native door's failed exit is tried once more and then waits, a wire refusal,
  `elsewhere` and an adapter no longer installed wait for the person, and a run that could not start is a start failure.
- **D137.** §2.2's *a closed quest, or a chat*: where its conversation is proven lost, it goes on by itself in a new part
  handed the brief; where its resume is refused otherwise, it waits, the brief ready. *Start a conversation with these words*
  stays as the person's own press.
- **D163.** Point 1's hand-over stays a new part on the named agent, handed the brief in place of the four facts, its outgoing
  agent asked for one closing note where the person pressed it (§2.2). The MIXAGENT1a-iii note's `from` is written by every
  continuation (§7). Its rejection of carrying the transcript is narrowed: the brief carries the exchange as Daoris kept it,
  bounded, and never the transcript whole, a thought, a model's summary, or a tool's output but the last failed check's.
- **D167.** Point 1 stands: a part is one conversation. §1.5's *a new part is told it continues the session* is the brief.
- **D125 point 4 and D165 point 3.** A carry-on or a move onto another account is handed the brief.
- **D183.** An access change whose resume finds its conversation lost goes on in a new part (§2), and a resumed start says
  the mode the agent reports, or that it is not confirmed (§2.1). Its points 2-4 stand.
- **D184 and D188 stand.** A move runs under the setup as it stands, and words held in `said` reach the new part after the
  brief, by D188's one rule.

## 9. Rejected

- **Daoris as the only context: every new process re-sent the conversation by Daoris and no resume.** It loses the exact
  memory where nothing moved, writes the whole context again at every start, and gives up the cheapest way on.
- **Copying or writing a harness's own store** to move a conversation: a transcript copied between account homes, or a
  thread written for another agent. It acts inside an account's home (D131, D137 §4.2). It depends on a format the harness
  changes at will (the native CLI updates itself, D131), and it can never cross makers.
- **A summary written by a model** (D163's rejection stands; §3.3).
- **A closing turn asked of the outgoing agent where the account forces the move** (`near`, `limit`, `curve`). It spends a
  turn on the account being left, near or past its limit, and the agent's last message of each turn is already kept. Where
  the account has room, one bounded note is asked (§2.2).
- **Moving a session by itself on any refused resume.** A refusal that does not prove the conversation lost (the native
  door's failed exit, a wire refusal, `elsewhere`, an adapter gone) would end a line the agent could still go on, and a
  second writer could redo its work; the person decides those (§2).
- **A record that holds several conversations in turn.** D167 settled that a session is an entry and a part is one
  conversation. A record that changed agent and account mid-way would change every reader of its `adapter` and account and
  of its usage, for what the entry already shows as one session.
- **Handing every event as it was kept.** A tool's raw output is cut at 2,000 characters (its text at 64 KiB) and a thought is the agent's working,
  so such a replay is neither exact nor small; the tree holds the files.
- **Going back to a left conversation with what came after it**: two histories that disagree on what is newest. Held until
  moves are measured.
- **An automatic compaction threshold** (§6).
- **A fork to change access** (`session/fork`, `--fork-session`): a fork stays on the same agent and account, and gains
  nothing a resume does not where a resume holds.
- **Changing the mode inside a running conversation** (`session/set_mode`): D183's HELPMODE1a2 note found that a mode is
  five things handed at a start, and that holds.

## 10. The choices, settled

The four choices this section left for the owner's review were settled under D191 by the steward, on Codex's and Fable's
findings (§14), and none is open: which refusals move a session by itself (§2), the bound (§3.2), the closing note (§2.2,
§9), and the line's history as a tool, built right after the brief (§3.5, §11).

## 11. Build rows

Order: SESSCTX1a first, since the cost and the resume-first rule lean on what is not measured; SESSCTX1b beside it, being
pure but for the links it writes; SESSCTX1f right after SESSCTX1b; SESSCTX1c after SESSCTX1b, the driven door's access
change being ACCESSSWAP1b's, already merged; SESSCTX1d after SESSCTX1c; SESSCTX1e after SESSCTX1a and SESSCTX1c.

| Row | Lanes | After | What |
|---|---|---|---|
| SESSCTX1a | the parent, on this machine's accounts | first | the measurements of §12, one turn each, into an evidence document |
| SESSCTX1b | driver, web-shell | beside 1a | the `from` link on every continuation with its three whys and their twins, the *Parts* strip by the why, a channel message's mark, the line's kept folders readable, and `ContextBrief.Compose` over a line's records, with its bound and its account, handed by the hand-over and every carry-on in place of the four facts (§3.4's two shapes) |
| SESSCTX1f | service, driver | 1b | the line's history as a read-only connector tool, its event reader twinned with the driver's (§3.5) |
| SESSCTX1c | driver, web-shell | 1b (ACCESSSWAP1b merged) | the refusals judged as §2 says (a proven loss goes on by itself, `from` why `resume`; the native door's failed exit split from `gone` and tried once more; the rest held for the person), the closing note (§2.2), the mode a resumed start reports (§2.1), the coded lines and `session.moved` |
| SESSCTX1d | modules, web-shell | 1c | the *Parts* strip's line for a move, the head's leading agent, each part's cost, in both languages |
| SESSCTX1e | driver, modules, web-shell | 1a, 1c | *Go on fresh*: the person's press and `daoris-driver sessions fresh <id>`, Ask Daoris's exemption as the person's own |

## 12. Measurements owed, and what only a real run proves

- **Claude Code's native `--resume`** has never run on this machine (`Adapters.cs:1985`): of an ended conversation, once
  unchanged and once under another `--permission-mode`, and whether the mode it runs in is the one named (§2.1).
- **The native door's failed exit before it names its conversation**: what ends a `--resume` that way (a conversation the
  harness no longer has, a rejected flag, a failed sign-in), so the single further try (§2) is judged on what happens.
- **The protocol door's `session/resume` under another mode**, on `claude-agent-acp`, and its `session/load` fallback measured
  apart; on each, whether the answer carries `currentModeId` or a `current_mode_update` follows, and whether the mode it
  reports is the one asked for, since an optional one it does not offer is left as it was; whether it takes
  `bypassPermissions` at a load is HELPMODE1a3's, which SESSCTX1a may share.
- **Codex**: whether a thread resumes after its `codex-acp` process has ended, and whether `thread_active_writer` (read,
  session messages design §1.1) clears once that process is gone.
- **The cache** at each resume above: the first turn's input, cache reads and cache writes, against H.
- **Usage at a turn's end** on the protocol door: whether it reports there too, or only as the turn runs (read as the latter,
  `Usage.cs:25-28`), which decides whether H at a move is the turn's last reading or its end's.
- **A new part handed a 40,000-character brief**, on each agent, one in English and one in Chinese: its first turn's input
  and cache write as the harness counted them, against S, and how a Han character counted against the weight of three; and a
  resumed conversation's later requests beside a new part's, since H, a high-water, says nothing of what a compacted
  conversation sends.
- **Only a real run proves** that a new part does not redo what the brief says was done, and only the window shows the
  *Parts* strip's line, in both languages. ANSWER1d and MSG1j's canaries prove the resume itself.

## 13. What this document's gate does not cover

Documents only; nothing is built. Its statements about today were read from the code at `d0e51b54b`. Its statements about
the harnesses were read from earlier designs that read the makers' packages and source (the session messages design §1, the
limit-signals evidence §3), or from Daoris's code; none was measured by this design, and §12 lists what must be.
`verify` checks this document's links and D189's place and shape, and none of their words.

## 14. Two other makers' reads (2026-10-20)

Codex read the design first and Fable second, each on its own (D191 point 1). Every finding was checked against the code by
the steward before it was taken, and every one changed this document. Codex's eight reshaped it while it was proposed;
Fable's fifteen, and the steward's decisions on the four choices §10 had left open, settled it. The readers did not
disagree on a choice: both would build the line's history with the brief (C8, F13), and Fable split further the refusals
Codex had held for the person (C7, F2, F3).

| # | Finding | Checked | Verdict, and where it was taken |
|---|---|---|---|
| C1 | The brief followed `from`, which a carry-on on the same account never writes, so an earlier part's words could be lost | `Driver.cs:3371-3393` | taken: §3.1, SESSCTX1b gives every continuation its link first; the brief follows that chain, never parallel parts |
| C2 | "Every word taken" repeats a word told twice and revives one taken back | `SessionEvents.cs:60-64`, `:182-196` | taken: §3.1 item 3, one word per id, its latest event; withdrawn left out; answered words as past exchanges; waiting words after the brief |
| C3 | A resume asks for a posture; an optional one not offered is left as it was | `Acp.cs:1920-1944` | taken: §2 and §2.1, a resumed start says what it asked and what was confirmed; §12 measures both, `session/resume` and `session/load` apart |
| C4 | "Cheaper on every later request" and "exact memory" outran the evidence: H is a high-water, and a harness's compaction keeps what it keeps | `AccountRotation.cs:662-678`; D137 | taken: §5, no way on is claimed cheaper; resume first for continuity; §12 measures later requests |
| C5 | The bound's allocation left sections without caps and no room for the omission notice | the design's §3.2 | taken: §3.2, per-section caps, per-item clipping, the notice's room, the bound provisional and counted in both languages |
| C6 | The brief carried activity more than actionable state; the plan's age was unsaid; the 2,000-character claim was wrong for text content | `SessionEvents.cs:358-362`, `:423-435`; `LastPlan` | taken: §3.1 items 4-5, the last check, open questions, the plan's turn; §3.3 and §9 corrected |
| C7 | `elsewhere` is contention, not loss; a bare `refused` does not prove loss | `Acp.cs:2390-2396` | taken: §2, those wait for the person, the brief ready |
| C8 | An optional note in a running turn is worth carrying; the pull tool should come with the brief | the design's §3.1, §3.5, §9 | taken: §3.1 item 6; the tool built right after the brief (F13) |
| F1 | A `from` on every continuation breaks the *Parts* strip, which reads any `from` as a hand-over; the whys are twinned and a box refuses an unknown one | `Trees/SessionEntries.cs:128`; `HandOverFiles.cs:46`, `:88`; `NoteCodesTests.cs:395`; `locales/{en,zh}/note.json:186-190` | taken: §7, `resume`, `carry-on` and `retry` added in both lanes in one branch, the strip read by the why, the twins named; SESSCTX1b |
| F2 | The native door's `gone` is any failed exit before the conversation was named, not a proven loss; only the protocol door's `resource_not_found` proves it | `ChatRunner.cs:1840-1846`; `DriverContinuations.cs:427-432`; `Acp.cs:2393` | taken: §2, only a proven loss (`resource_not_found`, `unkept`, `unable`, `offered`) moves a session by itself; the native failed exit tried once more, then held, and split from `gone` in SESSCTX1c |
| F3 | `refused` is three things: a wire refusal, an adapter no longer installed, a run that could not start | `Acp.cs:2395`; `ChatGoOn.cs:298-299`; `DriverContinuations.cs:125`, `:298`, `:348` | taken: §2, a wire refusal and `elsewhere` held, the brief ready; an adapter gone held with the hand-over's offer; a run that could not start a start failure; `elsewhere` Codex's alone |
| F4 | The brief's channel claims have no field: the page tells one by a prefix of Daoris's English | `ConversationView.tsx:371`; `channel.ts:40` | taken: §3.1 item 3 and §7, its own mark on the `user` event, with the page's twin; SESSCTX1b |
| F5 | A new part cannot read the attachments the brief names: a conversation's rules allow its own kept folder alone | `ChatRunner.cs:494-506`; D52 | taken: §3.1 item 5, a new part's rules read the line's kept folders |
| F6 | A part's end commit is not recorded, and nothing classes a tool call as a check | `TracedRecords.cs:42`; `Trace.Words.cs:342` | taken: §3.1 items 4-5, the tree's head read at compose time, a check by a declared gate, the last failed check's kept output; §3.3 |
| F7 | A driven carry-on's facts are a section of its composed instruction, not a first event; the briefing has one caller | `HandOver.cs:240`; `InstructionAccount.cs:17-19` | taken: §3.4, a section accounted within the instruction for a driven part, the first prompt's event for a conversation |
| F8 | ACCESSSWAP1 covers Ask Daoris alone; a driven session's access change is ACCESSSWAP1b, open | D183's ACCESSSWAP1 note | taken: §11, SESSCTX1c after ACCESSSWAP1b, which merged while the design was settled (D183's ACCESSSWAP1b note) |
| F9 | The outgoing agent's note rests on MIXAGENT1c, whose tool does not exist | `handover_propose` found nowhere under `src/` | taken: §3.1 item 6, the closing note carries it until the tool exists |
| F10 | A driven part stopped for a move has no last message for its interrupted turn | D163's MIXAGENT1a-ii note | taken: §3.1 item 4 and §2.2, its outcome is its tool lines |
| F11 | The caps left at most 4,000 characters for every turn's outcome, filled last | the design's §3.2 | taken: §3.2 re-cut, the outcomes at least 12,000; a Han character counted as three until measured |
| F12 | "The posture the agent answered" is read nowhere: `currentModeId` only skips an identical call, `set_mode`'s answer is discarded, the receipt keeps what was asked | `Acp.cs:1946-1954`, `:2134`; D183's HELPWIRED1 note | taken: §2.1, the mode the agent reports where it reports one, else not confirmed; §12; D189 point 2's caveat |
| F13 | The open choices: refusals (F2-F3), the closing turn, the history tool | the design's §9-§10 | taken: §2.2, a closing note where the account has room, none where it forces the move, with one case narrowed below; §3.5, SESSCTX1f right after SESSCTX1b |
| F14 | The fork's rejection reason, two conversations, was weak | the design's §9 | taken: §9, a fork gains nothing a resume does not where a resume holds |
| F15 | §0 said ACCESSSWAP1 was in flight; it merged | D183's ACCESSSWAP1 note | taken: §0 |

**One decision narrowed on the code.** The steward's choice on the closing note named an access change whose resume is
refused among the moves where the account has room. That refusal is met only at the resume, after ACCESSSWAP1 or
ACCESSSWAP1b already ended the part at its turn's end, and a lost conversation cannot be asked; so §2.2 asks none there, and
says why.
