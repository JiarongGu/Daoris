# How a message reaches an agent that is working: its urgency, one delivery rule, a durable queue and four receipts (proposed, 2026-10-20)

**Standing:** proposed under D188 (COWORK1). Pending the owner's review: no row builds before the owner's yes. The design
builds nothing; its rows (§14) do, each one feature end to end as D187 shapes a row. It is the foundation COWORK1 asked
for: how a message reaches an agent that is working, whoever sends it.

Read for it: D136 and its STEER2, STEER2a, STEER2c and STEER2d notes; the session messages design §0-§3 and §5; D167
points 6-8, its SESSONE2a, SESSONE2b, SESSONE2e and SESSONE2f notes and its AGENTROOM1 note; the co-working design §2.3;
D79; D155 points 7-8 and its XAGENT1e note; D163 points 2-3 and its MIXAGENT2a-i, 2a-ii and 2a-iii notes; D177 point 4
and its AGENTROOM1a note; D173's ADR-D and the collaboration proposal §6; the fabric contract audit's rows 5 and 6;
`person-waits.md` and `session-record-fields.md`. Every file reference below was read at `b5ff8dfb9`.

## 0. The case

### 0.1 The rule decided

The owner decided the rule on 2026-10-20: **a message to an agent never forces it to stop; whether and how it interrupts
depends on the message's urgency.** This design says what the urgencies are, who may send each, and what each does on
every door and in every state. It then gives every path one delivery rule, one durable queue and one set of receipts.

### 0.2 What happens today, path by path

| Path | Running, its door takes words at its next step | Running, its door takes words only at its turn's end | Parked or ended | Where |
|---|---|---|---|---|
| The person's words | sent at once, read at its next step (D136) | held, then the next prompt in the same session (D90) | kept on the record's `said`; the record reopens (D137) | `DrivenInbox.cs:195-207`; `ChatTurns.cs:213-245`; `SessionWords.cs:320-394` |
| The person's *Send now* (`SESSION_INPUT` `now`, `sessions say --now`) | steered where the agent steers (`AcpSession.SteersNow`): the step in flight is cut and the turn goes on; else at its next step, said `no-interrupt` or `behind` | held, and **the turn is stopped** so they go next, on a protocol door with an interrupt (codex-acp, dsh, the stub); the native run and a text door say `no-interrupt` | goes on with them, as without it | `DrivenInbox.cs:225-265`, `:434-445`; `ChatRunner.cs:1309` |
| Daoris's own words: a source's reopen told, a step's go-on | not said: the say door refuses a running record, so they are told at the first look that finds it parked or ended | the same | kept on `said` **as the person's words**, and the record reopens | `ReopenTelling.cs:16-53`; FABRIC-CLOSE1c17 |
| Another session's message on the ask's channel | at its next step, as Daoris's words around that agent's claim | not held; it waits on the channel for the entry's next part, and if none comes it is never delivered | waits for the entry's next part; never reopens | `ChannelReach.cs:182-211`; `DrivenInbox.cs:289-295` |
| The person's message on the ask's channel | as their words, at its next step | as their words, at its turn's end | to its repository: reopens; to everyone: the next part | `ChannelReach.cs:189-209` |
| A helper's answer (solicited) | a driven asker: waits until the record ends, then reopens it; a conversation: a turn of its own, queued behind the turn in flight and never handed in at its next step | the same | an ended asker reopens; a parked one waits for the person's answer | `DriverHelpers.cs:355-360`; `ChatTurns.cs:222-223`; `ChatHelperWords.cs:52-62` |
| A second opinion's findings (solicited) | wait, then reopen the ended record | the same | an ended record reopens; a parked one waits; a conversation's go to the person | `OpinionDeliverer.cs:124-143` |
| A quest | never a word into a turn: a request of the entry starts a further part alongside (D167's amendment), and an asker waiting on it (`quest_respond wait`) is resumed with its outcome (D79) | the same | the same | D79; D167 |
| A meeting's message | never into a turn: each turn reads its round's packet alone, and the next round's packet, sent by the person's press, carries the round's messages | the same | the same | `DriverMeetings.cs:19-21`; D177 |

The doors are the ones D136 and its STEER2a note name. **Takes words at its next step**: a protocol agent whose
`initialize` says `promptQueueing: true` (`claude-agent-acp`), for a driven session (STEER1) and a conversation (MSG1c).
**Steers**: the same agent where it also says `_meta.steering.supported: true`. **Only at its turn's end**: every other
protocol agent (`codex-acp`, dsh, the stub, a plugin's), the native driven run (`claude -p`, no stdin) and a native
conversation's stream-json, until STEER3 measures Codex's `turn/steer` and Claude Code's stream-json input.

### 0.3 What disagrees

1. **No message carries a level.** Whether it interrupts is decided by who sent it and by the door, never by the sender's
   word on how much it matters.
2. **A message stops a turn.** *Send now* on a turn-end door stops the turn (D136 point 4, STEER2 point 4), and with
   nothing typed it is a stop alone (`work.steer.nowTip`).
3. **Agents' messages are treated in opposite ways.** A channel claim, which nobody asked for, enters the next step. A
   helper's answer, which the asker asked for, never does, and waits for the turn's end or the record's.
4. **Daoris's own words are recorded as the person's**, and reach a running session only after it parks or ends, though
   the fabric events design §E5 says its session is told at its next step.
5. **A claim with no next part is lost silently.** It stays on the channel past the entry's mark, and nobody is told it
   never reached anyone.
6. **Queues are lost at a restart.** A conversation's waiting words live in `ChatTurns` alone (MSG1d2), and a driven
   session's held words in `DrivenInbox` alone. The channel's mark moves when a message is held, not when it is taken
   (`ChannelReach.cs:169-178`), so a restart between the two loses that message for the part.
7. **ADR-D's four states are not told apart.** The record tells *said* from *taken*. The channel tells posted from read.
   Nothing tells a word sent to the harness from one it took, or one it took from one it answered (audit row 6).

## 1. Words

- **A message** is anything said to a session while it may be working: the person's words, Daoris's own words, another
  agent's claim, a solicited answer (a helper's, an opinion's, a quest's outcome), a meeting's message.
- **Its urgency** is the sender's statement of when it should reach the agent: *ordinary*, *urgent* or *now*. It is a
  stated fact, never judged by a model (`model-decoupling`).
- **Its reach** is what the door did: `now`, `next-step`, `turn-end` or `resume`, the record's word since D136 and D137.
  The urgency is what was asked, and the reach is what happened. Both are kept.
- **A natural point** is a point the agent reaches by its own work, where words can go in and nothing is cut: the end of
  its turn, the space between two turns, the start of its next part, or its own call to read.
- **A stop** ends a turn or a session. It is never a message.
- **Solicited** means correlated to an ask the receiving session made, or one the person made for it, under the person's
  rule: a helper's answer to `helper_ask`, findings to the opinion the landing asked for, a quest's outcome to the asker
  waiting on it. **Unsolicited** is everything an agent says that nobody asked it for: a channel post.

## 2. The levels

### 2.1 Three levels, and a stop that is not one

| Level | Where it reaches a running turn | What it cuts | en / zh |
|---|---|---|---|
| **ordinary** | at a natural point: never inside a turn in flight | nothing | ordinary / 普通 |
| **urgent** | at the agent's next step, where its door takes words then: after the tool calls in flight finish and before its next model call | nothing: it folds into the running turn | urgent / 紧急 |
| **now** | at once, where the agent steers: the step in flight is cut and the turn goes on with the words | the step in flight, never the turn | now / 立即 |

**No level stops a turn.** A turn or a session is stopped only by a stop. The person's stops are *Stop* (the session),
*Stop turn* (a conversation's turn, CONV4a) and the keeping stop (§3.2); each is its own press with its own words. Daoris's
own stops are not messages either: a run's timeout, a pause or hold the person set (D132), the account's limit (D125), a
hand-over the person pressed (D163). Each stays as its decision has it. **No agent ever stops another's turn**, by any
level or any door.

**Urgency is when, not order.** Words held for the same point go in the order said. An urgent word goes at the next step
past ordinary words held for the turn's end, because the sender chose sooner. When the turn ends, everything still held
goes in the order said.

### 2.2 Who may send each

| Sender | ordinary | urgent | now | Its default |
|---|---|---|---|---|
| **The person** | yes | yes | yes, their own words only | urgent: D136's reason stands, since the person's words are meant to change the work in progress, and an agent that reads them at its turn's end reads them after doing that work |
| **Daoris** | yes | for a fact that makes the work in flight wrong: a source reopened under the step a session builds on (FABRIC-CLOSE1c8), and a step gone on with (FABRIC-CLOSE1c13) | never | ordinary |
| **A solicited answer** | yes | where its ask asked for it: `helper_ask` with `urgent`, or the person's helper ask with the same choice. An opinion's findings and a quest's outcome are always ordinary | never | ordinary |
| **An unsolicited claim** (`channel_post`) | yes | yes, within a bound: three of the ten posts a part may make (§10, trap 10) | never | ordinary |
| **A meeting's message** | the protocol's barrier, at any level (§5) | — | never | — |

The person's composer says the default as its press (*Send*, which reaches at the next step where the door allows) and
offers the other two beside it (§8.1). A message from before this design carries no level, and is read as its source's old
reach (§9).

### 2.3 Agents do not talk each other into turns, and a solicited answer may go on once

**Kept: an unsolicited message never starts, reopens or extends a run** (D155's rejection, D163's, D167 point 7, D177
point 4). Two agents answering each other with no person between is a loop with nobody in it. It spends accounts, and
the agreement it reaches establishes nothing (D155 point 8). An urgent claim at the next step creates no turn, since it
folds into a turn that already runs. So it is allowed, as the channel's claim at the next step is today.

**Amended: a solicited answer may extend or reopen its asker's run, once per ask.** The asker asked under the person's
rule (`helpers` `ask|on|off`, the opinion's level), and the answerer cannot ask back: a helper cannot ask for a helper
(D163 point 2), and an opinion's recheck goes to the person (D155 point 7). So the run that goes on is the asker's own,
finishing what it asked. A quest's outcome resuming an asker that waits on it (D79) is this case, built long ago.

**A new bound, for the one loop left.** Under `helpers: on`, an asker could ask, be reopened by the answer, and ask again,
with no person between. A line of work that goes on by solicited answers alone, with no word of the person's or Daoris's
beside them, goes on at most **twice in a row**. The third such answer waits for the person, as a wait they answer
(*Go on with this answer*, §8.3). Under `ask` the person's yes is already between each ask. Two is a first value,
a judgement that the row's tests hold and the owner may move.

## 3. The doors, until STEER3

### 3.1 An urgent word on a door that takes words only at its turn's end

Until STEER3 measures Codex's `turn/steer` (whether its `preempt` cuts a step, session messages design §1.3) and Claude
Code's stream-json input on a driven run, **an urgent word on a turn-end door is held for the turn's end, marked urgent
and said so.** It is never a stop, and never a steer whose effect is not measured.

- **The person's urgent or *now* words** go into the next prompt when the turn ends. Their line says *its door takes words
  only when its turn ends* (`work.steer.notNowTurnEnd`, already worded) and offers the keeping stop (§3.2) as the
  person's own press.
- **Daoris's urgent words** are held the same way and go into the next prompt, since Daoris's words may go on with a run
  (§4.1).
- **An agent's urgent claim** is held for the entry's next part, since at the turn's end it would extend the run (§2.3).
  The session's channel panel shows it to the person as urgent and not yet read, with the keeping stop. Where the person
  presses it, the claim goes as the next prompt on the person's press, marked as the claim it is and as passed on by the
  person.

After STEER3, MSG1h and MSG1i decide each door again under this rule. A step drained at the top of each loop with no cut is
a next-step door for *urgent*. A measured cut of the step in flight is a steering door for *now*. Neither door is used
before its turn is measured.

### 3.2 What the person's *Send now* becomes

*Send now* is the level **now**, and it never stops a turn.

- **Where the agent steers**: as STEER2a built it, steered past whatever waits, the step in flight cut and the turn going
  on. The record keeps the words with `reaches: "now"`.
- **On a next-step door whose agent does not steer**: at the next step, said `no-interrupt` or `behind`, as built.
- **On a turn-end door**: held for the turn's end, said `no-interrupt` (today's `turn-end` answer that stopped the turn
  retires). The line beside the held words offers **the keeping stop**, *Stop its turn so this goes next…*, which is
  `CANCEL_TURN` with `keep: true` for a conversation and a driven session alike (STEER2a point 5). It asks once, as a stop:
  it names the step it cuts, and says that the work done stays and what waits goes next.
- **With nothing typed**, *Send now* is not offered: the stop it was is the keeping stop's press.
- **The terminal**: `sessions say --now` is `--level now` and prints the same answers. *sent now: its turn is stopping, so
  this goes next* retires, and `sessions stop <id> --turn` is the keeping stop's twin.
- **A conversation's *Stop turn*** stays the withdrawal (CONV4a): it hands waiting words back to the box.

## 4. Solicited answers and unsolicited claims

### 4.1 Who may make a turn happen

| Sender | Extend a run (its next prompt at the turn's end) | Reopen an ended record | Start a new session |
|---|---|---|---|
| The person | yes (D90) | yes (D137) | only by their own press (*Start a conversation with these words*, D137 §2.2) |
| Daoris | yes, where the fact asks the work to go on: a reopen told, a step's go-on | the same | never by a message (Daoris starts work by its planner's own rules) |
| A solicited answer | yes, once per ask, under §2.3's bound | the same | never: an answer is carried to no new session, as D155 and D163 already hold |
| An unsolicited claim | never | never | never |

A parked record keeps D131's rule: it waits for the person's answer, and a solicited answer for it goes on beside that
answer, as both paths do today.

### 4.2 An unsolicited claim with no next part

A claim is never lost silently.

1. **It waits on the channel**, and the entry's mark moves through it only once a part has *taken* it (§6). A part that
   ends with the claim untaken leaves it for the entry's next part.
2. **The agent can read it at its own natural point.** The knowledge connector's next answer to a session whose entry
   holds unread messages ends with one line saying how many wait and that `channel_read` reads them (the collaboration
   proposal's pull-on-tool-call). This line is said once per new message, never on `channel_read` itself, and it is not
   delivery: the agent reads when it chooses.
3. **It expires when its ask has no quest open.** The ask's work is over, and no part will come. Its receipt for that
   entry becomes *not reached*, which the ask's channel shows the person beside the message (§8.4).
4. **The person may pass it on.** On a message not reached, or held on a turn-end door, *Send to its session* delivers it
   to the entry's newest part on this machine as the agent's claim, marked as passed on by the person. Because the person
   pressed, it may reopen that part: the person is between.

### 4.3 Expiry, by sender

| Sender | Waits until |
|---|---|
| The person | taken, taken back, or the record can never go on; then *It ended before reading this* or `words.unreached`, as today |
| Daoris | taken, or the fact it states no longer holds, judged at delivery: a later submission answers the reopen (`ReopenTelling`'s own rule). Then it is dropped with a coded note, and is never said late |
| A solicited answer | taken, or its asker can never take it: the person, with the refusal's code, as `OpinionDeliveries.ToPerson` and `HelperWhy` already do |
| An unsolicited claim | taken by a part of the entry, or its ask has no quest open (§4.2) |
| A meeting's message | the meeting's own end (D177) |

**Urgency has no clock.** It matters only while a turn runs. A message that finds no turn running waits as ordinary,
because there is nothing to interrupt.

## 5. One delivery rule

**The rule.** Every message is kept in its path's store with its level, its sender and its id before its sender is told it
was sent (§6). Then it reaches the agent at the earliest point its level allows on the door the session has. It stops
nothing. Where it cannot go sooner, it waits as ordinary, and says why. At a natural point, it goes into the next prompt
where its sender may make a turn happen (§4.1). Elsewhere, it waits for a turn that comes anyway, or for the agent's own
read.

| Path · level | Running, its door steers | Running, its door takes words at its next step | Running, turn's end only (until STEER3) | Between turns (a conversation open) | Parked | Ended | No part here |
|---|---|---|---|---|---|---|---|
| Person · now | steered at once; the step cut, the turn goes on | next step, `no-interrupt` or `behind` | held for the turn's end, `no-interrupt`; the keeping stop offered | the next turn, at once | its answer (D131) | reopens (D137) | a teammate's: refused, as today |
| Person · urgent | next step | next step | held for the turn's end, said urgent; the keeping stop offered | the next turn, at once | its answer | reopens | as above |
| Person · ordinary | held for the turn's end, then its next prompt | the same | the same | the next turn, at once | its answer | reopens | as above |
| Daoris · urgent | next step, as Daoris's words | next step | held for the turn's end, then its next prompt | the next turn | told after the person's answer | reopens where the fact asks it to go on | told nothing (`ReopenTelling`'s rule) |
| Daoris · ordinary | held for the turn's end, then its next prompt | the same | the same | the next turn | as above | as above | as above |
| Solicited answer · urgent | next step, marked as the answerer's claim | next step | held for the turn's end, then its next prompt (once, §2.3) | the next turn | goes on beside the person's answer | reopens once | to the person, with its code |
| Solicited answer · ordinary (the opinion and a quest's outcome always) | held for the turn's end, then its next prompt (once) | the same | the same | the next turn (a conversation's opinion: the person, as today) | as above | reopens once | as above |
| Unsolicited claim · urgent | next step, as Daoris's words around the claim | next step | held for the entry's next part; the person may pass it on (§3.1) | never a turn: the connector's next answer says it waits | the entry's next part | the entry's next part | the poster's machine's driver, by the same rule |
| Unsolicited claim · ordinary | never into the turn: the connector's next answer says it waits; the entry's next part | the same | the same | the same | the same | the same | as above |
| The person's channel message | as the person's words at their level, to the entry's running part | the same | the same | the same | to its repository: its answer | to its repository: reopens; to everyone: the next part | as above |
| A quest | never a message: a further part alongside (D167), or D79's resume of an asker waiting on it | | | | | | |
| A meeting's message | the protocol's barrier: the next round's packet, at whatever level; no level pierces a round | | | | | | |

**What the rule keeps from today.** The person's default is today's reach. Opinions keep their delivery, now named the
ordinary case. Helpers keep their ordinary delivery, and gain the urgent one. The channel's next-step reach becomes the
urgent level's, so an ordinary post no longer enters a running turn. The meeting's barrier and the quest's doors are
unchanged.

**What changes.**
- No path stops a turn.
- Daoris's words are marked as Daoris's, and its urgent words reach a running session.
- A helper's urgent answer reaches its asker's next step.
- An ordinary channel post waits for a natural point.
- Nothing undelivered vanishes.
- Every queue survives a restart (§6).
- The four receipts are told apart (§7).

## 6. Kept before told: the durable queue

**One store per concept, and one queue per session.** Each message stays in the store its path already owns (D173's guard:
never two authoritative stores for one concept). A word that may make a turn happen is delivered through the session's
own record: its `said` list, on the local host, machine-only (D47 §4).

| Path | The message's store | Its queue to one session | Read back after a restart by |
|---|---|---|---|
| The person's words | the record's `said` | `said`, which now takes words for a **running** record too, not only for a parked or ended one | the shell, at its come-up: the driven session's inbox and the conversation's turns are filled again from `said`, in order, by id (MSG1d2) |
| Daoris's words | `said`, with `from: daoris` (FABRIC-CLOSE1c17) | `said` | the same |
| A helper's answer | `helpers/<id>.answer.json` and `.state.json` | `said` with `helper` (built) | built: `ChatHelperWords`' come-up, the planner's go-on |
| An opinion's findings | the opinion's store and `delivered.json` | `said` with `by` (built) | built |
| A channel message | the `channel` table | the channel itself, past the entry's mark, which moves only once a part took the message | the mark, and the part's record events (§7), which say whether this part already took it |
| A quest | the quest store | a part, or D79's resume | built |
| A meeting's message | `transcript.json` and the packets | the next round's packet | built (D177's restart trap) |

- **`said` is kept before the inbox holds it.** The driver keeps the word on the record first (one write to the local
  host). Only then does it hold or send the word, and tell the page. The in-memory queues (`DrivenInbox._held`,
  `ChatTurns._waiting`) become views of `said`.
- **A word leaves `said` by id once it is on the wire**, through the taken door as a helper's answer does today
  (`ChatHelperWords.TakeOffAsync`). A word sent and not yet taken is not sent again after a restart. It reads *It may
  have read this; its answer was not kept* (`work.conversation.lost`).
- **A running record with words on `said` does not conclude** while the person's or Daoris's words wait (D137 §2). A
  solicited answer extends it once. Nothing else holds it.
- **The words held while a session winds up, or while a take runs**, keep the held-words file (`SessionWords.cs:93-96`,
  HANDWORDS1), since they must not land on a record that is ending. The rest of what it holds moves to `said`.
- **The channel's mark moves on taking**, not on holding (`ChannelReach.cs:169-178` today). The in-memory `_reached` map
  gives way to the part's record events, which the look reads once per part. A message taken by this part is not handed
  to it again, and a new part is handed what its entry has not taken.

## 7. Four receipts, each recorded where

ADR-D's states, as Daoris can observe them. **Acted upon means answered, never obeyed**: Daoris cannot know whether an
agent did what a message asked, and it does not claim to.

| State | en / zh | Means | Recorded, for a word to a session | Recorded, for a channel message |
|---|---|---|---|---|
| **queued** | held / 已暂存 | kept, and waiting for its point | on `said`, and the record's `user` event with `reaches` and the new `level`, shown at once (D136 §3) | the `channel` row (posted) |
| **acknowledged** | sent / 已发出 | it left Daoris: the prompt, the steer or the resume's opening is on the wire | a receipt event under the word's id, `sent`, written when the door's send returns; the word leaves `said` | the part's receipt event, under the message's id |
| **delivered** | taken / 已收下 | the agent took it into its conversation | the word again under its id as that turn's ask (D136 §3); for a steer, where it was answered `injected` | the same event on the part's record; the entry's mark moves through it |
| **acted upon** | answered / 已回应 | the turn that took it ended with an answer, or a correlated reply came | a receipt event, `answered`, when the turn that took it ended `end_turn`; and the reply's id where one is correlated: a channel reply's `re`, `helper_answer`, `opinion_answer`, `quest_respond` | the reply's `re`, and the part's receipt |

Two more outcomes close a word without the last state. *Not reached* (未送达) means it expired, or the record can never go
on (§4.3). *Taken back* means the person withdrew it, the existing `work.conversation.withdrawn` (*Taken back before it
was sent.* / 发送前已收回。).

**Who sees them.** The page shows them on the conversation and on the ask's channel (§8). `channel_read` answers a poster
the receipts of its own messages, per addressed repository: *waiting*, *taken* or *not reached*. The machine log keeps
none: these are the record's facts, not measurements.

## 8. How it shows

### 8.1 The composer while a turn runs

- **One press, *Send*,** at the person's default, urgent, with a tip that says what the door will do: *it reads this at
  its next step*, or *its door takes words only when its turn ends*.
- **A level menu beside it**, with three items. *When its turn ends* (ordinary). *At its next step* (urgent, the default).
  *Now, cutting its step* (now), offered only where the agent steers; elsewhere it is shown disabled, with the reason.
- **Enter sends at the default**, as Enter queues today.
- **No level is remembered**: each word starts at the default.

### 8.2 The conversation and the held-words line

- **Each waiting word's line says when it reaches**, which is what its level means, in the sentences already worded
  (`work.conversation.held.*`), plus one new one: *Urgent, held: its door takes words only when its turn ends.*
- **A chip names the level only where it is not quiet.** A word from anyone but the person shows *urgent* (紧急) where it
  is urgent, and the person's own word shows *now* (立即) where it was sent now. *Ordinary* is never marked. The chip uses
  a word and a neutral emphasis, never colour alone, and never the hue of a wait on the person.
- **The held-words line** (`SESSION_QUEUED`) lists each waiting word with its sender, its level and a menu: *Make urgent*,
  *Make ordinary* and *Send now* for the person's own words; *Make urgent* and *Make ordinary* for Daoris's words and a
  solicited answer; *Edit* and *Take back* for the person's own words only.
- **A level is changed only while the word waits.** It is `SESSION_QUEUE_EDIT` with `level`. Raising a word to *now* steers
  it past what waits, as STEER2a allows. A word already on its way belongs to the agent and is changed by nothing (STEER2
  point 3).
- **Receipts fold under a taken message**: *sent 14:02 · taken 14:03 · answered 14:09*. A message that never reached its
  agent says so at its place.

### 8.3 A wait for the person

The third answer of §2.3's bound is a wait the person answers. It is one new kind in `work/waits.ts`, with its places as
`person-waits.md` lists them: the head, the foot, *What needs you*, the badge, the hook named in Ask Daoris's coverage, and
the words. Its acts are *Go on with this answer* and *Not now*. Not now hands the answer to the person, as an answer
nobody takes is handed today.

### 8.4 The ask's channel and a session's channel panel

- Each message names its level where it is urgent.
- For each repository it addresses, the message shows its receipt: *waiting*, *taken by `repository`*, *answered* or *not
  reached*.
- A message not reached, or held on a turn-end door, offers *Send to its session* (§4.2), the person's press.
- A teammate's message shows the receipts of this machine's own parts only.

### 8.5 Words in both languages

| Key | Kind | en | zh |
|---|---|---|---|
| `work.level.ordinary` | state | ordinary | 普通 |
| `work.level.urgent` | state | urgent | 紧急 |
| `work.level.now` | state | now | 立即 |
| `work.composer.level` | label | When it reads this | 何时读到 |
| `work.composer.levelOrdinary` | menu item | When its turn ends | 本轮结束时 |
| `work.composer.levelUrgent` | menu item | At its next step | 下一步时 |
| `work.composer.levelNow` | menu item | Now, cutting its step | 立即，打断当前这一步 |
| `work.conversation.held.urgentTurnEnd` | sentence | Urgent, held: its door takes words only when its turn ends. | 紧急，已暂存：它的接入方式只在本轮结束时接收消息。 |
| `work.conversation.held.claimNextPart` | sentence | Held for its next part: it takes another agent's words only during a turn or at a new part. | 留待它的下一段：它只在一轮进行中或新的一段开始时接收其他智能体的话。 |
| `work.steer.stopKeeping` | button | Stop its turn so this goes next… | 停止本轮，让这条接着送达… |
| `work.steer.stopKeepingAsk` | sentence | This stops the step it is on and its turn. What it has done stays, and what waits goes next. | 这会停止它正在进行的这一步和本轮。已完成的工作保留，等待中的内容接着送达。 |
| `work.queue.makeUrgent` / `makeOrdinary` | menu item | Make urgent / Make ordinary | 改为紧急 / 改为普通 |
| `work.receipt.held` / `sent` / `taken` / `answered` / `notReached` | state | held / sent / taken / answered / not reached | 已暂存 / 已发出 / 已收下 / 已回应 / 未送达 |
| `work.wait.answerAlone` | sentence | {{agent}}'s answer would take this work on a third time with nobody between. | {{agent}} 的回答会让这项工作在无人参与的情况下第三次继续。 |
| `work.wait.goOnAnswer` | button | Go on with this answer | 带着这个回答继续 |
| `asks.channel.forward` | button | Send to its session | 发给它的会话 |
| `asks.channel.notReached` | sentence | It never reached {{repository}}'s session: the ask's work ended first. | 它没有送达 {{repository}} 的会话：需求的工作先结束了。 |

- **The glossary gains one term, *urgency*** (紧急程度): the sender's statement of when a message reaches a working agent,
  ordinary, urgent or now, with the stop not among them. It avoids en *priority* and zh 优先级, because urgency decides
  when a message goes, not its order. The three level words join its kinds as states.
- **`work.steer.sending`** (*Stopping its turn: what you added goes next.*) retires, and `work.steer.nowTip` loses its
  second clause. The build owns the budgets; `names:check -- --strict` holds them.

### 8.6 The terminal

- `sessions say <id> "…" [--level ordinary|urgent|now]`, where `--now` is read as `--level now`. It prints the reach, and
  the level where the door could not honour it, with the reason.
- `sessions stop <id> --turn` is the keeping stop.
- `ask channel <id> [--urgent] "…"` posts the person's message at urgent.

## 9. Stored shapes

Each is a field added beside the others. A record from before reads as it did (the brief's rule on stored shapes).

| Shape | Field | A value from before reads as |
|---|---|---|
| `SaidWord` (`Sessions.cs:329-330`) and the driver's `SaidWordView` | `Level` (`ordinary` / `urgent` / `now`); `From` (`daoris`, FABRIC-CLOSE1c17) | no level: the person's word as urgent, every other word as ordinary, which is today's reach for each; no `From`: as before, by `By`, `Helper` and `Channel` |
| The record's `user` event (`SessionEvents.cs:166-172`) | `level`; the receipt events `sent` and `answered` under the word's id | no level: as before; no receipts: shown as today, said and taken only |
| `channel` row and `ChannelWire` (travels) | `urgent` (true only; said only where true) | a message from before, or from an older teammate's machine, keeps today's reach: next step where the door takes words then. It shows no level |
| `helpers/<id>.ask.json` (twins: `HelperBox` and `HelperFiles`) | `urgent` | ordinary, today's delivery |
| `SESSION_INPUT`, `SESSION_QUEUE_EDIT` | `level` | `now: true` is read as `level: now`; nothing is the default |
| `sessions say` request file (`SessionRequest`) | `level` | `now` as before |

`said` taking words for a running record is the say door's widened judgement (`POST /api/sessions/{id}/say`), not a new
field. The record's `said` stays machine-only and never travels (`session-record-fields.md`).

## 10. The traps, each a test

1. **A message stops a turn.** No delivery path calls a door's interrupt. Only the stop routes do: `CANCEL_TURN`, the
   session's stop, the keeping stop. *Test:* `DrivenInboxTests` and `ChatTurnsTests`: *now* on a turn-end door sends no
   interrupt, the turn runs on, and the answer is `no-interrupt`. Also a census of the interrupt's callers.
2. ***Send now* with nothing typed stops a turn.** It is not offered. *Test:* the composer's test, en and zh.
3. **An unsolicited claim extends a run.** At a turn's end with only claims held, the run ends and the claim waits for the
   next part. *Test:* `ChannelNextStepTickTests`' stub that declares nothing, kept, and an urgent claim on it.
4. **An ordinary claim enters a running turn.** *Test:* `ChannelReachTests`: an ordinary claim to a next-step part is not
   held, and the connector's next answer says it waits.
5. **An agent's words read as the person's.** *Test:* `SaidWord.Persons` excludes `helper`, `by`, `channel` and `from`;
   the intake's DRIFT1 never takes them as the person's.
6. **Daoris's words read as the person's** (FABRIC-CLOSE1c17). *Test:* the person's words equal to Daoris's sentence are
   the person's; Daoris's carry `from` and sync as Daoris's; an older host's word reads as the person's.
7. **A restart loses a word.** *Test (Process):* a conversation's waiting word and a driven session's held word survive a
   shell restart, and go once.
8. **A restart sends a word twice.** A word sent and not taken before a crash is not sent again. *Test:* the come-up reads
   `said` and the receipts, and the word reads *it may have read this*.
9. **The channel's mark passes a message held but not taken.** *Test:* a restart between hold and take hands the
   message again to the same part, once.
10. **Urgent flooding.** A part's fourth urgent post is kept as ordinary, and the tool's answer says so. *Test:*
    `ChannelTests`' bound, in the insert's own statement as the ten-post bound is.
11. **Answers alone loop.** Under `helpers: on`, the third answer in a row with nobody between waits for the person.
    *Test:* the driver's go-on over a stand-in; the wait's places (`person-waits.md`).
12. **A level from before.** *Test:* a `SaidWord`, a channel row and a helper ask with no level each read as their old
    reach; an older page's `now: true` reads as *now*.
13. **Urgent past ordinary is out of order.** *Test:* an urgent word goes at the next step past a held ordinary one; at the
    turn's end, the rest go in the order said, and the record shows each where it was taken.
14. **A steer answered `promptRequired`** (STEER2d) at *now* still goes first. *Test:* `AcpSteerTests`' case, kept.
15. **A claim vanishes.** *Test:* an ask whose quests all close marks each untaken message *not reached* for its entry;
    `channel_read` and the page say so.
16. **A meeting's barrier pierced.** No level reaches a turn in a round. *Test:* `MeetingTests`: a message posted at
    urgent during a round reaches no turn of it.
17. **Daoris's word said late.** A reopen answered by a later submission is dropped, not told. *Test:* `ReopenTelling`'s
    tests at urgent.
18. **Words cut or clashing.** *Test:* stories in long 中文; `i18n:check`, `names:check -- --strict`.

## 11. What only a measured turn can settle

The parent runs these with the owner, on accounts the owner names. They are not this design's rows.

- **STEER3.** It measures Codex's `turn/steer` (does `preempt` cut a step?), Claude Code's `--input-format stream-json` on a
  driven run, and whether an empty steer at `now` folds words already queued. Until then, the turn-end doors are §3.1's.
- **BGWAIT2b.** It probes words said into a harness's own woken turn, which may be taken and never answered. Until its
  probe, an urgent word sent into a woken turn is a known risk: the run would wait on it until its timeout. The rule
  does not change. BGWAIT2b's case decides whether such a word is held for the woken turn's end.

## 12. Not built

**Rejected (D188):**
- **A level that stops a turn.** It is the rule this design serves.
- **Codex's `turn/steer` as an urgent or *now* door before STEER3.** Its cut is not measured.
- **An unsolicited message that starts, reopens or extends a run.** D155 and D167 stand.
- ***now* for anyone but the person.** It cuts a step the agent chose. An agent's or Daoris's word can always wait one step.
- **Urgency judged by a model.** It is the sender's statement, and a model's judgement would make delivery depend on
  which model ran.
- **A priority queue** that reorders waiting words at a turn's end.
- **A clock that decays urgency.** A message that finds no running turn already waits as ordinary.
- **One store for every message.** Each path keeps its store, and `said` is the one queue to a session.
- **The level on the travelling session record.** `said` stays machine-only.

**Left out, not rejected:**
- A person's urgency for a meeting, where the person already grants every round.
- A post to a teammate's ask (SESSONE2i), which carries its level when it is built.
- Receipts in the machine log.
- An agent changing the level of its own message once posted: a channel message is never changed once kept (SESSONE2d).

## 13. Against the decisions

**This amends:**
- **D136**: point 4 and its STEER2 note's point 4, and its STEER2a and STEER2c notes. *Send now* is the level *now*. On a
  turn-end door it holds the words and stops nothing; the keeping stop is the person's separate press, and the terminal's
  *its turn is stopping* line retires.
- **D167 point 7.** A channel message reaches a running session at its next step where it is urgent and the door takes
  words then. An ordinary one waits for a natural point. The entry's mark moves on taking. A message never taken expires
  visibly, and the person may pass it on. The co-working design §2.3's table is read with §5's.
- **D163 point 2.** A helper's answer reaches its asker at its ask's level: as its next turn, or, where the ask was
  urgent, at the asker's next step. A run goes on by answers alone at most twice in a row.
- **D155's XAGENT1e delivery rule.** Findings are a solicited answer at ordinary. Their delivery is unchanged, now one rule
  with the helper's, with the four receipts. A reopen by findings alone counts toward §2.3's bound.
- **The session messages design §2 and §3.1.** A conversation's waiting words are record events kept on `said`, as §3.1
  asked (MSG1d2).
- **The co-working design §2.3** and **the fabric events design §E5**'s *its session is told at its next step*, which this
  builds as Daoris's urgent words.

**These stand:**
- D90's turn-end door, D137's reaches and reopen, D131's park.
- D52: no permission is answered for an agent.
- D47 §4: `said` never travels.
- D155's and D163's *never to a new session*.
- D177's barrier.
- D79's wait and resume.
- D167's amendment: a request runs alongside.

**D173's ADR-D** is this design's for delivery: durable, correlated by id, deduplicated by the record's events, and the
four states told apart (§7). D173's note points here.

## 14. The rows

**The rows follow D187.** Each is one feature a person can see working, built end to end across the lanes it needs by one
builder, and handed back working. The rule is built in sub-features, each working on its own: the person's words first,
then Daoris's, then the durable queue, then agents' messages. STEER3 and BGWAIT2b stay the parent's, with the owner.
Each row names its Ask Daoris door or its reason (D110).

- [ ] **COWORK1a — the person's words reach a working agent at the level they choose, and no word stops a turn** (driver, modules, web-shell; D188 points 1-3; design §2, §3, §5, §8.1-§8.2, §8.5-§8.6, traps 1, 2, 12-14). `SESSION_INPUT` and `sessions say` take `level` (`now: true` read as now); `DrivenInbox` and `ChatTurns` hold ordinary for the turn's end, send urgent at the next step, steer *now* where the agent steers and otherwise say why, never interrupting; the keeping stop as its own press and `sessions stop --turn`; the composer's level menu, the held line's level and `SESSION_QUEUE_EDIT`'s `level`; the glossary's *urgency*; Ask Daoris rows for any new hook. Opus. Proof: `DrivenInboxTests`, `ChatTurnsTests`, `AcpSteerTests`, `SessionsSayCommandTests`, `DriverModuleConversationTests`, the web's tests en and zh, `i18n:check`, `names:check --strict`; seen on the window: a word at each level on Claude Code's door and on the stub's, and *Send now* on the stub holding without a stop.
- [ ] **COWORK1b — Daoris's own words marked as Daoris's, and its urgent words reach a running session** (service, driver, web-shell; after 1a; absorbs FABRIC-CLOSE1c17; design §2.2, §4.1, §4.3, §5, traps 6, 17). `SaidWord.From` (`daoris`) through the say door and the record's chain (`session-record-fields.md`, a word's field); `ReopenTelling` says the reopen and the step's go-on at urgent, into a running session at its next step and held elsewhere, dropped when a later submission answers it; the gate reads the mark, not the sentence; the conversation shows them as Daoris's. Opus. Proof: the say door's tests, `ReopenTelling`'s, CLOSE1c17's three cases, the page's en and zh; seen on the window: a reopen told to a running Claude Code session.
- [ ] **COWORK1c — every word to a session waits on its record, survives a restart, and shows its four receipts** (service, driver, modules, web-shell; after 1a; absorbs MSG1d2; design §6, §7, §8.2, §9, traps 7-9). The say door keeps words for a running record; the driven inbox and the conversation's turns are views of `said`, filled at the come-up and taken off by id once on the wire; the receipt events `sent` and `answered`; the page folds them under a taken word. Opus. Proof: the service store's tests (a running record's `said`, an elder record), `DrivenInboxTests`, `ChatTurnsTests`, a `Process` test of a shell restart with a conversation's and a driven session's waiting word, the page's en and zh; seen on the window: a word waiting across a restart.
- [ ] **COWORK1d — agents' messages by level: an ordinary claim waits for a natural point, an urgent one reaches the next step, a helper's answer at its ask's level, and nothing undelivered vanishes** (service, driver, web-shell; after 1c; design §2.2-§2.3, §4, §5, §8.3-§8.4, traps 3-5, 10, 11, 15, 16). `channel_post`'s `urgent` with its bound, `ChannelWire`'s field, the connector's answers naming what waits, the mark moving on taking, expiry when the ask has no quest open, `channel_read`'s receipts; `helper_ask`'s `urgent` (both twins) and its answer at the next step; the answers-alone bound as a person's wait (`person-waits.md`'s places); the ask's channel and the session's panel with receipts and *Send to its session*. Opus. Proof: `ChannelTests`, `ChannelReachTests`, `ChannelSyncTests`, `ChannelNextStepTickTests` (`Process`), the MCP tools' tests, `HelperBoxShapeTests` and `HelperFilesTests`, `MeetingTests`' barrier, `HelpCoverageTests`' rows, the page's en and zh; seen on the window: two sessions of one ask, an ordinary and an urgent post, a message not reached.

**Rows that change** (the parent's to write into TASKS):
- **MSG1d2** is COWORK1c's.
- **FABRIC-CLOSE1c17** is COWORK1b's.
- **MSG1h and MSG1i**, after STEER3: each door is decided again under §3.1. A measured next step carries *urgent*, and a
  measured cut carries *now*.
- **FABRIC-EVENT3c** wakes the delivery of §5 on the channel's family, in place of the look's read.
- **SESSONE2i** carries a post's level to a teammate's ask.
- **AGENTROOM1b**'s `meeting_say` is a meeting's message: no level, the barrier's.
