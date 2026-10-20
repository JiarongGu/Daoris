# A session's context: the agent's own conversation where it holds, Daoris's record wherever the session moves (proposed, 2026-10-20)

**Standing:** proposed under D189 (SESSCTX1). Pending the owner's review: no row builds before the owner's yes, and another
maker's read comes before that review. The design builds nothing; its rows (§11) do.

Read for it: D131 points 1-4 and its rejected shapes; D137 and the session messages design §1, §2.2 and §4; D163 and the
mixed-agents design §1, §2 and §6, with its MIXAGENT1a-i, 1a-ii and 1a-iii notes; D167 and the co-working design §1.1, §1.3,
§1.5 and §5; D183 points 2-4 and its HELPMODE1a2, 1a4 and 1a5 notes; D184 and its AUTONOMY1b and 1c notes; D188 points 1, 5 and 6;
D125 point 4; D165 point 3; D143; D160's rejected compaction; D24; the session economy design §1.6-§1.7; the subagent load
evidence §2-§3; `session-record-fields.md`, `model-decoupling.md` and `claims-need-checks.md`. Every file reference was read
at `4e12716b9`. A statement about what a harness does says where it comes from: **read** (from the maker's package or source,
read by an earlier design, or from Daoris's code that speaks to the harness), **recorded** (from this repository's own
measured records), or **not measured**. This design ran no turn.

## 0. The direction, and what is true today

The owner set the direction on 2026-10-20: Daoris manages a session's context, so that a conversation can change its
permissions, and even its agent, mid-session; holding the context is also what later work builds on. ACCESSSWAP1, in flight
and not assumed merged here, takes the first step: an access change ends the agent's process at its turn's end, and the same
conversation resumes under the new access at its next turn. This design decides the rest.

**Where a conversation's context lives.**

| What | Where | Who reads it | Source |
|---|---|---|---|
| The agent's own conversation: every turn, tool results whole, its thinking, its compactions | the harness's store, in the configuration home of the account it ran on (Claude Code's transcript, Codex's thread) | the harness, on a resume | D131 point 2; D125 point 4; `Continuation.cs:139-143` |
| Its id | `<home>/sessions/<id>.harness.json`, one per record, replaced when the wire names another | Daoris | `HarnessConversations.cs:20-43` |
| Daoris's record of it | the session record; its events, `<id>.events.jsonl`, machine-local, a text cut at 64 KiB and a tool call's input and output at 2,000 characters; the verbatim transcript; `said`; the kept attachments | Daoris, the page, the terminal | `SessionEventLog.cs:9-24`; `SessionEvents.cs:343-347`, `:407-418` |

**What moves a conversation, and what the agent knows after.**

| The change | Today | What the agent knows after |
|---|---|---|
| Access, the same agent and account | the same conversation resumed under the new access at its next start (D183 point 3, D184's AUTONOMY1b note), at its turn's end for a narrowing of Ask Daoris's mode (D183's HELPMODE1a5 note), and at the next turn for every change once ACCESSSWAP1 lands | its own conversation |
| Another account of the same agent: D165's move, a pick, a rotation after a limit | a new part: a carry-on or a hand-over (D125 point 4, D165 point 3) | the request as first written, the last plan, the last words, what is uncommitted |
| Another agent (D163) | a new part: the hand-over | the same four facts, with *None of its own context carries over to you* (`HandOverRecords.cs:111-112`) |
| Its resume refused: `unkept`, `unable`, `offered`, `gone`, `refused`, `elsewhere` | a driven record: a carry-on handed the four facts; a conversation or a closed quest's session: the words wait, marked, and *Start a conversation with these words* is the person's press (`ChatGoOn.cs:60-62`) | the four facts, or nothing |

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

## 2. The rule: resume where the conversation holds; otherwise a new part, handed the brief

The conversation resumes where its record has the same agent and account, its tree stands, its id was kept, and the door
can resume (D131 point 2, `Continuation.cs:169-201`). The access is not among the conditions: the protocol door sets the
posture on every resume (read: `Acp.cs:2373-2374`), and the native door names `--permission-mode` on every run (read:
`Adapters.cs:1987-1995`, `:2045-2046`).

| The change | Resumes? | Then |
|---|---|---|
| Access | yes, where the rest holds (ACCESSSWAP1) | the same part, under the new access |
| Account | no: the conversation is in the other account's home (D131 point 2) | a new part, `from` with the move's why |
| Agent | no | a new part, `from` with the move's why |
| None, or access alone, and the resume refused (`unkept`, `unable`, `offered`, `gone`, `refused`, `elsewhere`) | no | a new part, `from` why `resume`, the code said |
| Its tree gone; a teammate's record, an intake, a stand-down | no | as today (D131, D137 §2.2) |

- **The old part ends at its turn's end**, never mid-turn (D188 point 1), and before the new starts, since a tree is held by
  one session at a time (D51; D163's MIXAGENT1a-i note, point 3). A quest's session, whose turn is its whole quest, is
  stopped first by the person's press, as D163's MIXAGENT1a-ii note has it.
- **The new part starts in the same tree** on the agent, account and access chosen. Its instruction is composed afresh from
  the setup as it stands (D184's AUTONOMY1c note), and its room and servers are handed, and recorded, as at any start (D183
  point 4). Its
  first prompt is the brief, then the words waiting in `said` (D188 points 5-6), which leave the old record by MSG1d's
  went note.
- **A conversation, or a closed quest's session, whose own conversation cannot resume goes on by itself**, as a new
  conversation in its tree and a part of its entry, said why. D137 §2.2 made it the person's press because a new
  conversation had none of the old one's context; the brief is that context. An access change whose resume is refused goes
  the same way, so no access change ends a conversation's line. *Start a conversation with these words* stays, as the
  person's own press for a fresh start.
- **A driven record's fallback (D131 point 3), a carry-on after a limit (D125 point 4) and a hand-over (D163)** stay what
  they are, a new part each, handed the brief in place of the four facts.
- **A left conversation is not resumed again.** Once its line has moved on, it lacks what came after. It stays in its
  account's home, never read (D125 point 4), and never copied or removed by Daoris. Going back to that agent or account opens another
  new part.

## 3. The brief

### 3.1 What it carries

A pure function, `ContextBrief.Compose`, which replaces `HandOverBriefing.Compose` (`HandOverRecords.cs:92-138`), composes it
from the records of the line the new part continues: the part it moves from and, by their `from` links, the parts before it.
No model writes any of it.

1. **Its opening**, in Daoris's one English: which session it continues, on which agent it ran, why it moved, and that what
   follows is Daoris's record of the conversation, not the agent's memory.
2. **The request** as first written, at most 4,000 characters, as today; a conversation's is its first word, not repeated in 3.
3. **Every word the session took**, in order, marked by who said it as its record marks it: the person's, verbatim; another
   agent's answer or findings and a channel's message, as that agent's claim (D155 point 7, D167 point 7). Daoris's own
   composed prompts are not repeated, since the new part's instruction is composed afresh.
4. **Each turn's outcome**: the agent's last message of the turn, its end kept, at most 4,000 characters; and one line of what
   the turn did (the files it edited, the commands that failed), from the tool calls' kind, title, locations and status as
   the events keep them.
5. **The work's state**: the plan as last kept (50 steps at most); what the tree holds uncommitted and the commits on it since
   the line began (50 each at most); the files the person attached, by name and where they lie, as a resumed run is handed
   them (`ChatFiles.cs:105-109`).
6. **The outgoing agent's own note**, where it wrote one through its tool (MIXAGENT1c), as its claim, at most 4,000 characters.
7. **The entry's other parts**, one line each (its quest, how it began, how it ended), as D167 §1.5 hands them today.
8. **What was left out**, by count, and that the record holds it.

### 3.2 Its bound

**At most 40,000 characters.** Parts 1, 2 and 5 to 8 go first, each under its own cap. The person's words come before the
agent's: all of them are kept, unless they alone would pass half the bound; then the oldest after the request go first,
counted. The turns' outcomes fill what remains, newest first. The brief reads in the conversation's order.

Why 40,000: about 11,000 tokens at the estimate of 3.6 characters a token (recorded: the subagent load evidence §2; an
estimate, since the tokenizer is the harness's, economy design §1.7). That is under a quarter of the 50,000 tokens a
general-purpose subagent started at (recorded, the same §2), so the brief costs a new part less than the harness's own start.
SESSCTX1a reads its size as each harness counted it, and the bound moves on that number.

### 3.3 What it never carries

- **The agent's thoughts or a tool call's output.** The first is the agent's working, not the conversation's. The second is
  kept cut at 2,000 characters for the page, and the tree holds the files themselves. So the brief is never the transcript
  whole, which D163 rejected and §8 keeps rejecting.
- **A summary written by a model**, Daoris's or any other (D163; D24). It would be a model call Daoris makes and pays for, a
  tier to name, and a claim nobody can check against the conversation. The agent's own words (its last message of each turn,
  its note) are kept as its claims.
- **An account's name, a key or a machine path** beyond where the kept attachments lie, which a resumed run is handed already.
- **Anything read or written in a harness's own store** (D131; D137 §4.2).

### 3.4 Kept as handed

The brief is the new part's first event, with origin `target` and its CONTEXT1 account (D143 point 1;
`InstructionAccount.cs:6-28`): each section's characters and source, and what the bound left out and why. `daoris-driver
trace` and the page read it as they read an instruction's.

### 3.5 Pulled, later

A read-only tool on the session's connector that pages the line's events would let the brief carry only the newest turns,
the agent reading the rest when it needs them. The events are transcript-class and never cross HTTP (D76 §2), so the tool
reads them on the machine, for the session it serves alone. It is held (SESSCTX1f) until SESSCTX1a and the first moves show
that the bound costs too much or leaves out what a new part needed.

## 4. What the agent's own conversation still does

- It is the working context of its part, and the only exact one.
- It is resumed under D131's conditions, which is the cheapest way on and the only way an agent keeps its exact memory
  across processes. So the rule tries it first.
- It takes an access change at its resume (§2).
- It compacts itself, as the harness decides (§6).
- After its part ends it stays in its account's home. The person may still open it in their own terminal (D137 §4.3); turns
  run there are not in the record, and a later move does not carry them.
- Daoris never claims what it holds (D137 §4.4): a resume hands the agent its own history, which it may have compacted.

## 5. The cost in tokens

**Read and recorded.** A harness sends the context it holds with every request; one recorded turn read 103 tokens from its
cache for each token it read anew (economy design §1.6, from COST1's and METER1's records). Daoris counts a turn's input,
output, cache reads and cache writes where its door reports them (read: `TurnModels.cs:114-139`, `SessionEvents.cs:282-283`),
and never a price (`Usage.cs:37-40`, D24). The protocol door reports context as a turn runs, and the native door when it ends
(read: `Usage.cs:25-28`).

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

**What follows without a measurement.** A move costs one write of S and B in place of H, and every later request carries B
instead of H. B is bounded near 11,000 tokens, and H in a long conversation is far larger (the recorded turn grew to 679,000,
economy design §1.6). So a move is cheaper on every request after it, and it costs the agent its exact memory. That trade is
why the rule resumes first: the direction asks for a change of access or agent, not a cheaper conversation, and exact memory
is worth paying for wherever it can be kept.

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
  widened to the new why). `from` carries the why, gaining `resume` beside `press`, `near`, `limit`, `agent` and `curve`; a
  stored shape, so the old codes read as they did. The machine log writes `session.moved` {session, from, to, why,
  briefed}, by profile name as D125 point 7's lines are.
- **On the page.** The *Parts* strip says how a part began (*moved from Codex: its conversation could not go with it*). The
  head shows the leading part's agent, and each part's cost is on its own row. Both languages.
- **To the agent**, by the brief's opening; **on the console**, when the move happens, as ACCESSSWAP1's line does.
- **D143 point 5.** The choice is which way a session goes on. Its facts are kept on the line's notes and the brief's
  account, and `trace`, the *Parts* strip and *Details* read them.

## 8. What the decisions become

- **D131.** Points 1, 2 and 4 stand: the resume is the first way on, its conditions are unchanged, and an access change rides
  it. Point 3's fallback hands the brief.
- **D137.** §2.2's *a closed quest, or a chat*: where its conversation cannot resume, it goes on by itself in a new part
  handed the brief. *Start a conversation with these words* stays as the person's own press.
- **D163.** Point 1's hand-over stays a new part on the named agent, handed the brief in place of the four facts. Its
  rejection of carrying the transcript is narrowed: the brief carries the exchange as Daoris kept it, bounded, and never the
  transcript whole, a thought, a tool's output or a model's summary.
- **D167.** Point 1 stands: a part is one conversation. §1.5's *a new part is told it continues the session* is the brief.
- **D125 point 4 and D165 point 3.** A carry-on or a move onto another account is handed the brief.
- **D183.** An access change whose resume is refused goes on in a new part (§2). Its points 2-4 stand.
- **D184 and D188 stand.** A move runs under the setup as it stands, and words held in `said` reach the new part in its first
  prompt, after the brief, by D188's one rule.

## 9. Rejected

- **Daoris as the only context: every new process re-sent the conversation by Daoris and no resume.** It loses the exact
  memory where nothing moved, writes the whole context again at every start, and gives up the cheapest way on.
- **Copying or writing a harness's own store** to move a conversation: a transcript copied between account homes, or a
  thread written for another agent. It acts inside an account's home (D131, D137 §4.2). It depends on a format the harness
  changes at will (the native CLI updates itself, D131), and it can never cross makers.
- **A summary written by a model** (D163's rejection stands; §3.3).
- **A closing turn asked of the outgoing agent before every move.** It spends a turn on the account being left, often near or
  past its limit, and holds back the move the person pressed; the agent's last message of each turn is already kept.
- **A record that holds several conversations in turn.** D167 settled that a session is an entry and a part is one
  conversation. A record that changed agent and account mid-way would change every reader of its `adapter` and account and
  of its usage, for what the entry already shows as one session.
- **Handing every event as it was kept.** A tool's output is cut at 2,000 characters and a thought is the agent's working,
  so such a replay is neither exact nor small; the tree holds the files.
- **Going back to a left conversation with what came after it**: two histories that disagree on what is newest. Held until
  moves are measured.
- **An automatic compaction threshold** (§6).
- **A fork to change access** (`session/fork`, `--fork-session`): it stays on the same agent and account, leaves two
  conversations, and the posture is already set at every resume.
- **Changing the mode inside a running conversation** (`session/set_mode`): D183's HELPMODE1a2 note found that a mode is
  five things handed at a start, and that holds.

## 10. For the owner's review

Three choices the owner may turn:

1. **A conversation whose resume is refused goes on by itself** (§2). The alternative keeps the person's press and hands the
   brief to it.
2. **The bound, 40,000 characters** (§3.2).
3. **No closing turn from the outgoing agent** (§9). The alternative asks it for a note at its turn's end, where it can still
   take a turn.

## 11. Build rows

Order: SESSCTX1a first, since the cost and the resume-first rule lean on what is not measured; SESSCTX1b beside it, being
pure; SESSCTX1c after ACCESSSWAP1 and SESSCTX1b; SESSCTX1d after SESSCTX1c; SESSCTX1e after SESSCTX1a and SESSCTX1c;
SESSCTX1f held.

| Row | Lanes | What |
|---|---|---|
| SESSCTX1a | the parent, on this machine's accounts | the measurements of §12, one turn each, into an evidence document |
| SESSCTX1b | driver | `ContextBrief.Compose` over a line's records, with its bound and its CONTEXT1 account, handed by the hand-over and every carry-on in place of the four facts |
| SESSCTX1c | driver | a refused resume of a conversation, a closed quest's session or an access change goes on in a new part, handed the brief, `from` why `resume`, the coded lines and `session.moved` |
| SESSCTX1d | modules, web | the *Parts* strip's words for a move, the head's leading agent, each part's cost, in both languages |
| SESSCTX1e | driver, modules, web | *Go on fresh*: the person's press and `daoris-driver sessions fresh <id>`, Ask Daoris's exemption as the person's own |
| SESSCTX1f | service, driver | held: the line's history as a read-only connector tool (§3.5) |

## 12. Measurements owed, and what only a real run proves

- **Claude Code's native `--resume`** has never run on this machine (`Adapters.cs:1985`): of an ended conversation, once
  unchanged and once under another `--permission-mode`.
- **The protocol door's `session/resume` under another mode**, on `claude-agent-acp`; whether it takes
  `bypassPermissions` at a load is HELPMODE1a3's, which SESSCTX1a may share.
- **Codex**: whether a thread resumes after its `codex-acp` process has ended, and whether `thread_active_writer` (read,
  session messages design §1.1) clears once that process is gone.
- **The cache** at each resume above: the first turn's input, cache reads and cache writes, against H.
- **Usage at a turn's end** on the protocol door: whether it reports there too, or only as the turn runs (read as the latter,
  `Usage.cs:25-28`), which decides whether H at a move is the turn's last reading or its end's.
- **A new part handed a 40,000-character brief**, on each agent: its first turn's input and cache write as the harness
  counted them, against S.
- **Only a real run proves** that a new part does not redo what the brief says was done, and only the window shows the
  *Parts* strip's line, in both languages. ANSWER1d and MSG1j's canaries prove the resume itself.

## 13. What this document's gate does not cover

Documents only; nothing is built. Its statements about today were read from the code at `4e12716b9`. Its statements about
the harnesses were read from earlier designs that read the makers' packages and source (the session messages design §1, the
limit-signals evidence §3), or from Daoris's code; none was measured by this design, and §12 lists what must be.
`verify` checks this document's links and D189's place and shape, and none of their words.
