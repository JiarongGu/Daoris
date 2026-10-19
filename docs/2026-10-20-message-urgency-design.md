# How a message reaches an agent that is working: its urgency, one delivery rule, a durable queue and four receipts (proposed, 2026-10-20)

**Standing:** proposed under D188 (COWORK1). Pending the owner's review: no row builds before the owner's yes. The design
builds nothing; its rows (§14) do, each one feature end to end as D187 shapes a row. It is the foundation COWORK1 asked
for: how a message reaches an agent that is working, whoever sends it.

Read for it: D136 and its STEER2, STEER2a, STEER2c and STEER2d notes; the session messages design §0-§3 and §5; D167
points 6-8, its SESSONE2a, SESSONE2b, SESSONE2e and SESSONE2f notes and its AGENTROOM1 note; the co-working design §2.3;
D79; D155 points 7-8 and its XAGENT1e note; D163 points 2-3 and its MIXAGENT2a-i, 2a-ii and 2a-iii notes; D177 point 4
and its AGENTROOM1a note; D173's ADR-D and the collaboration proposal §6; the fabric contract audit's rows 5 and 6;
`person-waits.md` and `session-record-fields.md`. Every file reference below was read at `b5ff8dfb9`.

**Revised 2026-10-20 (DESIGNREAD2)** after a second maker's read, before the owner's review. The references the revision added
were read at `3e88bcc79`. D188's DESIGNREAD2 note says what the read changed and what it did not.

## 0. The case

### 0.1 The rule decided

The owner decided the rule on 2026-10-20: **a message to an agent never forces it to stop; whether and how it interrupts
depends on the message's urgency.** This design says what the urgencies are, who may send each, and what each does on
every door and in every state. It then gives every path one delivery rule, one durable queue and one set of receipts.

### 0.2 What happens today, path by path

The doors first, since what a word does depends on the door and on the kind of session, not on the door's capability alone.

| Door | Its agents | A word said while a turn runs | *Send now* while a turn runs | An interrupt that keeps the session | Where |
|---|---|---|---|---|---|
| **Steers** | a protocol agent saying `promptQueueing: true` and `_meta.steering.supported: true` (`claude-agent-acp`), driven or a conversation | at its next step, once the first prompt is on the wire and nothing waits before it; else it waits behind them | steered: the step in flight cut, the turn going on | yes | `Acp.cs:814-834`; D136's STEER2a note |
| **Next step** | a protocol agent saying `promptQueueing: true` only | the same | at its next step, said `no-interrupt` or `behind` | yes | `DrivenInbox.cs:195-207`, `:225-265`; `ChatTurns.cs:213-245`, `:289-335` |
| **Protocol, turn's end** | `codex-acp` (its steering declaration alone is not enough), dsh, the stub, a plugin's | held, then the next prompt in the same session (D90) | held, and **the turn is stopped** so they go next | yes (`session/cancel`) | the same |
| **Native conversation** | Claude Code's stream-json conversation | the same | held, and **the turn is stopped** by the `interrupt` control request | yes | `Adapters.cs:2007-2017`; `ChatRunner.cs:1153-1165` |
| **Native driven run** | `claude -p`, no stdin | the same | held, said `no-interrupt`: nothing stops | **no** | `DrivenInbox.cs:78-84`, `:252-259` |
| **Text-only conversation** | an agent whose wire marks no turn | **sent at once** to its stdin, whatever runs, and kept in no record | sent at once, said `no-interrupt` | **no**: it has no turns, and `CANCEL_TURN` refuses | `ChatRunner.cs:1320-1366`, `:1422-1427` |

Then each path, on those doors.

| Path | Running, its door takes words at its next step | Running, its door takes words only at its turn's end | Parked or ended | Where |
|---|---|---|---|---|
| The person's words | read at its next step (D136), on the conditions above | held, then the next prompt in the same session (D90); a text-only conversation sends them at once | kept on the record's `said`; the record reopens (D137) | `DrivenInbox.cs:195-207`; `ChatTurns.cs:213-245`; `SessionWords.cs:320-394` |
| The person's *Send now* (`SESSION_INPUT` `now`, `sessions say --now`) | steered where the agent steers; else at its next step, said `no-interrupt` or `behind` | held, and the turn stopped, where the door has an interrupt; the native driven run and a text-only conversation say `no-interrupt` | goes on with them, as without it | `DrivenInbox.cs:225-265`; `ChatTurns.cs:289-335`; `ChatRunner.cs:1331` |
| Daoris's own words: a source's reopen told, a step's go-on | not said: `ReopenTelling` returns on `record.Running` before it asks the say door (`ReopenTelling.cs:152`), and the say door refuses a running record besides, so they are told at the first look that finds it parked or ended | the same | kept on `said` **as the person's words**: on a park they are its answer, on an ended record they reopen it | `ReopenTelling.cs:35-44`, `:152-181`; FABRIC-CLOSE1c17 |
| Another session's message on the ask's channel | at its next step, as Daoris's words around that agent's claim, to the entry's newest part here | not held: it waits on the channel for the entry's next part, or for the agent's own `channel_read`, which reads it with no new part | the entry's next part, handed it in its instruction; or the agent's own `channel_read` | `ChannelReach.cs:74-92`, `:182-211`; `DrivenInbox.cs:289-295` |
| The person's message on the ask's channel | as their words, at its next step | as their words, at its turn's end | to its repository: reopens; to everyone: the next part | `ChannelReach.cs:189-209` |
| A helper's answer (solicited) | a driven asker: waits until the record ends, then reopens it; a conversation: a turn of its own, queued behind the turn in flight and never handed in at its next step; a text-only conversation: sent at once, whatever runs | the same | an ended asker reopens; a parked one waits for the person's answer | `DriverHelpers.cs:355-360`; `ChatTurns.cs:222-223`; `ChatHelperWords.cs:47-62` |
| A second opinion's findings (solicited) | wait, then reopen the ended record | the same | an ended record reopens; a parked one waits; a conversation's go to the person | `OpinionDeliverer.cs:124-143` |
| A quest | never a word into a turn: a request of the entry starts a further part alongside (D167's amendment), and an asker waiting on it (`quest_respond wait`) is resumed with its outcome (D79) | the same | the same | D79; D167 |
| A meeting's message | never into a turn: each turn reads its round's packet alone, and the next round's packet, sent by the person's press, carries the round's messages. The barrier is built, but a real agent's turn is refused `no-message-tool` until a tool carries a meeting's message (AGENTROOM1b): only a stand-in takes a turn today | the same | the same | `DriverMeetings.cs:19-28`, `:305-311`; D177 |

**How the channel's mark moves today.** One moment per entry. The look moves it through the messages that reached a part, and
stops before the first one held (`ChannelReach.cs:162-179`). A part's start moves it to the newest item its read held, though
its instruction carries only the newest twenty, each cut at 1,500 characters, and counts the older in one line
(`ChannelReach.cs:101-133`, `ChannelWords.cs:211-251`). The agent's own `channel_read` moves it to the newest item before its
answer reaches the agent (`Channel.cs:544-569`). A message is past the mark only where its moment is later than the mark's
(`ChannelWords.cs:80-83`), so one mirrored late from a teammate, or one at the mark's very moment, is never handed.

**How a word is identified and recorded today.** The say door gives each word a new id (`SessionLedger.cs:711`), so a producer
that says again after a crash keeps a second copy (`ReopenTelling` says, then marks: its own remarks name the gap). The queues
give a word their own ids (`said-n` in the driven inbox, a fresh one in a conversation's steer). On a protocol session with no
prompt open, the words are recorded as taken before the prompt goes on the wire (`Acp.cs:1110-1119`).

### 0.3 What disagrees

1. **No message carries a level.** Whether it interrupts is decided by who sent it and by the door, never by the sender's
   word on how much it matters.
2. **A message stops a turn.** *Send now* on a turn-end door stops the turn (D136 point 4, STEER2 point 4), and with
   nothing typed it is a stop alone (`work.steer.nowTip`).
3. **Agents' messages are treated in opposite ways.** A channel claim, which nobody asked for, enters the next step. A
   helper's answer, which the asker asked for, never does, and waits for the turn's end or the record's.
4. **Daoris's own words are recorded as the person's**, and reach a running session only after it parks or ends, though
   the fabric events design §E5 says its session is told at its next step.
5. **A claim with no next part is never pushed.** It stays on the channel past the entry's mark. Unless the agent reads the
   channel itself, it never reaches anyone, and nobody is told.
6. **Two queues are lost at a restart.** The record may show a waiting word, but a conversation's waiting words are
   delivered from `ChatTurns`' memory alone (MSG1d2), and a driven session's held words from `DrivenInbox`'s. The words held
   while a session winds up are kept in a file and read back (`SessionWords.cs:869-898`), and a helper's answer waits on
   `said` until its turn is on the wire (`ChatHelperWords.cs:15-45`). The channel's mark moves when a message is held, not when
   it is taken (`ChannelReach.cs:169-178`), so a restart between the two loses that message for the part.
7. **ADR-D's four states are not told apart.** The record tells *said* from *taken*, and writes *taken* before the wire for a
   prompt to an idle protocol session. The channel tells posted from read. Nothing tells a word sent to the harness from one
   it took, or one it took from one it answered (audit row 6).
8. **Nothing is delivered by identity.** A producer's retry makes a second word. The channel's mark is a moment, moved by
   three hands, and passes messages it never handed whole.

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

| Level | What it asks for on a running turn | What it cuts | en / zh |
|---|---|---|---|
| **ordinary** | a natural point: never inside a turn in flight | nothing | ordinary / 普通 |
| **urgent** | the agent's next step, where its door takes words then: after the tool calls in flight finish and before its next model call | nothing: it folds into the running turn | urgent / 紧急 |
| **now** | at once, where the agent steers: the step in flight is cut and the turn goes on with the words | the step in flight, never the turn | now / 立即 |

**A level is a request, not a guarantee.** Each door gives what it can, and the answer says what it gave and why (§3.0). On
a door that takes words only at its turn's end, urgent and now get what ordinary gets, marked and said so. On a text-only
conversation no level is honoured, since the driver cannot tell its turns apart.

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

**The race at a turn's end, decided: the rule is weakened to what Daoris does, and the race is narrowed and named.** A claim is
judged under the inbox's lock (`HoldDuringTurn`), which guards Daoris's own closing, not the harness's turn. A claim that passes
after the harness ended its turn, and before Daoris closes the inbox, goes as `session/prompt`, and that starts another turn
(D167's SESSONE2f note: not measured). No door offers a queueing prompt that refuses an idle session. The steer's
`promptRequired` does refuse one, but it cuts the step, so it is *now*'s door and never a claim's. So:
- **The rule reads: Daoris never prompts a claim at a turn's end, and never holds one for it.** It does not say that no claim
  ever lands in a turn of its own.
- **The window is narrowed.** The inbox stops taking claims on the reader's thread the moment the answer to its last open prompt
  is read (`Acp.cs:1124-1127`'s `answered`), before the run's next step is judged.
- **What is left is named.** The harness's answer may still be in the pipe when the claim is written. Such a claim may start one
  turn. The run ends after it unless something else holds it, and the poster's three urgent posts bound how often it can happen.
- **STEER3 measures** whether a door answers a queueing prompt to an idle session apart. Where one does, the claim is refused
  there instead, and the race closes for that door.

The race costs the person's and Daoris's words nothing, since either may make a turn happen (§4.1). A solicited urgent answer
that lands so counts as its one go-on.

**Amended: a solicited answer may extend or reopen its asker's run, once per ask.** The asker asked under the person's
rule (`helpers` `ask|on|off`, the opinion's level), and the answerer cannot ask back: a helper cannot ask for a helper
(D163 point 2), and an opinion's recheck goes to the person (D155 point 7). So the run that goes on is the asker's own,
finishing what it asked. A quest's outcome resuming an asker that waits on it (D79) is this case, built long ago.

**A new bound, for the one loop left.** Under `helpers: on`, an asker could ask, be reopened by the answer, and ask again,
with no person between. A line of work that goes on by solicited answers alone, with no word of the person's or Daoris's
beside them, goes on at most **twice in a row**. The third such answer waits for the person, as a wait they answer
(*Go on with this answer*, §8.3). Under `ask` the person's yes is already between each ask. Two is a first value,
a judgement that the row's tests hold and the owner may move.

- **It is counted from the records, over the whole line of work.** A go-on is a turn whose prompt took only solicited answers.
  The count is read back over the line: the entry's parts for a quest's work, and the `from` chain for a conversation. It runs
  across hand-overs and retries, which never reset it, and across both paths that go on, the driver's go-on and a conversation's
  turn pump. Only a word of the person's or Daoris's on the line resets it. Being read from the records, it survives a restart
  and needs no counter of its own.
- **It is judged by the session's one dispatcher** (§6), so two answers handed under separate asks' claims
  (`DriverHelpers.cs:137-159`) never both pass as the second.
- **An urgent answer taken inside a running turn is no go-on, and counts nothing.** An asker that asks again and again within
  its own turn is not this loop: no run goes on past its own turn. D163's two helpers at work (`too-many`) and the run's own
  minutes bound it, under the `helpers: on` the person chose.

## 3. The doors, until STEER3

### 3.0 What each level gets, door by door

The doors are §0.2's. A cell says what a word of the person's gets while a turn runs. Another sender's word gets the same, with
§4.1's limits on making a turn happen.

| Door | ordinary | urgent | now | The keeping stop | The receipts it can observe (§7) |
|---|---|---|---|---|---|
| Steers | the turn's end | the next step | the step cut, the turn going on | not needed: words reach sooner | held, sent, taken, answered |
| Next step | the turn's end | the next step | the next step, said `no-interrupt` or `behind` | not needed | held, sent, taken, answered |
| Protocol, turn's end | the turn's end | the turn's end, marked urgent, said so | the turn's end, said `no-interrupt` | offered | held, sent, taken, answered |
| Native conversation | the turn's end | the same | the same | offered: the `interrupt` control request | held, sent, taken, answered |
| Native driven run | the turn's end | the same | the same | **not offered**: it has no interrupt that keeps the session, and *Stop* ends the session | held, sent, taken, answered |
| Text-only conversation | **at once**: no level is honoured, and the composer offers no level, saying why | at once | at once | **not offered**: it has no turns | the person's words: none, since they stay in the console as today; another sender's: held and sent |

### 3.1 An urgent word on a door that takes words only at its turn's end

Until STEER3 measures Codex's `turn/steer` (whether its `preempt` cuts a step, session messages design §1.3) and Claude
Code's stream-json input on a driven run, **an urgent word on a turn-end door is held for the turn's end, marked urgent
and said so.** It is never a stop, and never a steer whose effect is not measured.

- **The person's urgent or *now* words** go into the next prompt when the turn ends. Their line says *its door takes words
  only when its turn ends* (`work.steer.notNowTurnEnd`, already worded). Where the door has an interrupt that keeps the
  session, it offers the keeping stop (§3.2) as the person's own press. On the native driven run it offers none.
- **Daoris's urgent words** are held the same way and go into the next prompt, since Daoris's words may go on with a run
  (§4.1).
- **An agent's urgent claim** waits as an ordinary claim does (§4.2). It rides a prompt the run sends anyway at the turn's end,
  or waits for the entry's next part or the agent's own read, since a prompt of its own would extend the run (§2.3). **Here its
  level earns no earlier delivery at all**: it is a label, and a line to the person. The session's channel panel shows it as
  urgent and not yet read. The person may pass it on (§4.2), which goes as the next prompt on the person's press, marked as the
  claim it is and as passed on by them. Where the door has an interrupt, the panel also offers the keeping stop, so the passed-on
  claim goes next.

After STEER3, MSG1h and MSG1i decide each door again under this rule. A step drained at the top of each loop with no cut is
a next-step door for *urgent*. A measured cut of the step in flight is a steering door for *now*. Neither door is used
before its turn is measured.

### 3.2 What the person's *Send now* becomes

*Send now* is the level **now**, and it never stops a turn.

- **Where the agent steers**: as STEER2a built it, steered past whatever waits, the step in flight cut and the turn going
  on. The record keeps the words with `reaches: "now"`.
- **On a next-step door whose agent does not steer**: at the next step, said `no-interrupt` or `behind`, as built.
- **On a turn-end door**: held for the turn's end, said `no-interrupt` (today's `turn-end` answer that stopped the turn
  retires). Where the door has an interrupt that keeps the session (a protocol door, the native conversation), the line
  beside the held words offers **the keeping stop**, *Stop its turn so this goes next…*, which is `CANCEL_TURN` with
  `keep: true` for a conversation and a driven session alike (STEER2a point 5). It asks once, as a stop: it names the step it
  cuts, and says that the work done stays and what waits goes next. The native driven run has no such interrupt
  (`DrivenInbox.cs:78-84`), so the line offers nothing there: a stop could not make the words go sooner.
- **On a text-only conversation**: sent at once, said `no-interrupt`, as today. No keeping stop.
- **With nothing typed**, *Send now* is not offered: the stop it was is the keeping stop's press, where there is one.
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

1. **It waits on the channel** until the entry acknowledges it by its id (§6), once a part has taken it or the agent read it.
   A part that ends with the claim untaken leaves it for the entry's next part. At a turn's end, it rides a prompt the run
   sends anyway (the person's, Daoris's or an answer's words), since that turn comes for its own reasons.
2. **The agent can read it at its own natural point.** The knowledge connector's next answer to a session whose entry
   holds unread messages ends with one line saying how many wait and that `channel_read` reads them (the collaboration
   proposal's pull-on-tool-call). This line is said once per new message, never on `channel_read` itself, and it is not
   delivery: the agent reads when it chooses.
3. **It expires when its ask has no quest open and no part of its entry runs here.** Both are checked, since a running part
   is still reached with no quest open (`ChannelReach.cs:84-89`). Then the ask's work is over, and no part will come. Its
   receipt for that entry becomes *not reached*, which the ask's channel shows the person beside the message (§8.4).
4. **The person may pass it on.** On a message not reached, or held on a turn-end door, *Send to its session* delivers it
   to the entry's newest part on this machine as the agent's claim, marked as passed on by the person. Because the person
   pressed, it may reopen that part: the person is between. The word kept is still the agent's (§9: its author is the
   poster, its transport the channel), so nothing reads it as the person's.

### 4.3 Expiry, by sender

| Sender | Waits until |
|---|---|
| The person | taken, taken back, or the record can never go on; then *It ended before reading this* or `words.unreached`, as today |
| Daoris | taken, or the fact it states no longer holds, judged at delivery: a later submission answers the reopen (`ReopenTelling`'s own rule). Then it is dropped with a coded note, and is never said late. The fact is judged by the key the word carries (the quest and the reopen's machine and sequence, or the step and the submission it grew from), never by its sentence: `BringsToSource` compares the sentence's text today (`ReopenTelling.cs:206-210`) |
| A solicited answer | taken, or its asker can never take it: the person, with the refusal's code, as `OpinionDeliveries.ToPerson` and `HelperWhy` already do |
| An unsolicited claim | taken by a part of the entry, or its ask has no quest open (§4.2) |
| A meeting's message | the meeting's own end (D177) |

**Urgency has no clock.** It matters only while a turn runs. A message that finds no turn running waits as ordinary,
because there is nothing to interrupt.

## 5. One delivery rule

**The rule.** Every message is kept in its path's store with its level, its sender and its producer's id before its sender
is told it was sent (§6). Then it reaches the agent at the earliest point its level asks for and the session's door allows
(§3.0). It stops nothing. Where it cannot go sooner, it waits as ordinary, and says why. At a natural point, it goes into the
next prompt where its sender may make a turn happen (§4.1). Elsewhere, it rides a prompt the run sends anyway, or waits for
the entry's next part, or for the agent's own read. A text-only conversation is outside the rule's levels: everything goes at
once, said so (§3.0).

| Path · level | Running, its door steers | Running, its door takes words at its next step | Running, turn's end only (until STEER3) | Between turns (a conversation open) | Parked | Ended | No part here |
|---|---|---|---|---|---|---|---|
| Person · now | steered at once; the step cut, the turn goes on | next step, `no-interrupt` or `behind` | held for the turn's end, `no-interrupt`; the keeping stop offered where the door has an interrupt | the next turn, at once | its answer (D131) | reopens (D137) | a teammate's: refused, as today |
| Person · urgent | next step | next step | held for the turn's end, said urgent; the keeping stop offered where the door has an interrupt | the next turn, at once | its answer | reopens | as above |
| Person · ordinary | held for the turn's end, then its next prompt | the same | the same | the next turn, at once | its answer | reopens | as above |
| Daoris · urgent | next step, as Daoris's words | next step | held for the turn's end, then its next prompt | the next turn | its answer where the person's own press made the fact (a reopen, a go-on), as today; otherwise told after the person's answer | reopens where the fact asks it to go on | told nothing (`ReopenTelling`'s rule) |
| Daoris · ordinary | held for the turn's end, then its next prompt | the same | the same | the next turn | as above | as above | as above |
| Solicited answer · urgent | next step, marked as the answerer's claim | next step | held for the turn's end, then its next prompt (once, §2.3) | the next turn | goes on beside the person's answer | reopens once | to the person, with its code |
| Solicited answer · ordinary (the opinion and a quest's outcome always) | held for the turn's end, then its next prompt (once) | the same | the same | the next turn (a conversation's opinion: the person, as today) | as above | reopens once | as above |
| Unsolicited claim · urgent | next step, as Daoris's words around the claim; the race at a turn's end narrowed and named (§2.3) | next step | as ordinary, marked urgent: no earlier delivery; the person may pass it on (§3.1) | never a turn of its own: the connector's next answer says it waits | the entry's next part, or the agent's own read | the same | the poster's machine's driver, by the same rule |
| Unsolicited claim · ordinary | never into the turn: it rides a prompt the run sends anyway at the turn's end; else the connector's next answer says it waits, and the entry's next part or the agent's own read takes it | the same | the same | the same | the same | the same | as above |
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
- A word survives a restart, or is shown as uncertain, and is never sent twice by Daoris (§6).
- The receipts are told apart where the door can observe them (§7).

## 6. Kept before told: the durable queue

**One store per concept, and one queue per session.** Each message stays in the store its path already owns (D173's guard:
never two authoritative stores for one concept). A word that may make a turn happen is delivered through the session's
own record: its `said` list, on the local host, machine-only (D47 §4).

| Path | The message's store | Its queue to one session | Read back after a restart by |
|---|---|---|---|
| The person's words | the record's `said` | `said`, which now takes words for a **running** record too, not only for a parked or ended one | the session's one dispatcher, at the shell's come-up: a word never attempted goes in its order, and one attempted and not taken is uncertain (below; MSG1d2) |
| Daoris's words | `said`, with `from: daoris` (FABRIC-CLOSE1c17) | `said` | the same |
| A helper's answer | `helpers/<id>.answer.json` and `.state.json` | `said` with `helper` (built) | built: `ChatHelperWords`' come-up, the planner's go-on |
| An opinion's findings | the opinion's store and `delivered.json` | `said` with `by` (built) | built |
| A channel message | the `channel` table | the channel itself: what its entry has not acknowledged by the message's id | the entry's acknowledgments, kept by message id beside its mark (§9) |
| A quest | the quest store | a part, or D79's resume | built |
| A meeting's message | `transcript.json` and the packets | the next round's packet | built (D177's restart trap) |

- **A word is kept once, by its producer's key.** The say door takes a `key` from whoever produces the word. The page's press
  gives one key per press. Daoris's fact gives its own: the quest and the reopen's machine and sequence, or the step and the
  submission it grew from. A channel message gives its id, a helper's answer its ask's id, an opinion's findings the opinion's
  id. A word whose key the record already holds, waiting or taken, is answered as the word kept, and no second copy is made.
  So a producer's retry is safe: `ReopenTelling` says, then marks, and a crash between the two keeps one word, not two.
- **`said` is kept before anything holds it, and one dispatcher drains it.** The driver keeps the word on the record first,
  appended in one statement as today, and only then tells the page. Each session has one dispatcher. It reads `said` in its
  order, and holds, sends or steers each word by its level and the door. No caller holds or sends a word itself once its own
  write returns: two writes that return out of order would reorder the queue. The in-memory queues (`DrivenInbox._held`,
  `ChatTurns._waiting`) become the dispatcher's views of `said`. The ledger's exclusive take (`SessionLedger.cs:727-765`)
  stays, so a take never rewrites the list under an append.
- **Each attempt is marked before the wire.** The dispatcher marks the word's `attempt` on `said` in one statement, writes
  the word to the harness, and records *sent* when the write returns. *Sent* is the write's return, never the prompt's answer:
  `SendPromptAsync` completes only with the answer. The word leaves `said` by id once the door confirms it *taken*, or once
  *sent* where the door cannot confirm (§7). Its take-off is awaited, in order, never fired and forgotten as a helper's answer's
  is today (`ChatHelperWords.cs:54`).
- **Across a restart: at most once without the person, and never silently.** The wire's write and the receipt after it are not
  one atomic step, and no harness drops a repeated prompt by its id. So at the come-up, a word with no attempt is held and goes
  in its order. A word with an attempt and no *taken* is **uncertain**, and Daoris never sends it again, since the harness may
  hold it. It leaves `said` with the receipt *uncertain* and reads *It may have read this; its answer was not kept*
  (`work.conversation.lost`). The person's own word offers *Say it again*, which keeps a new word under a new key. Daoris's
  words and a solicited answer show the same line on the person's page, and nothing says them again unasked.
- **A running record with words on `said` does not conclude** while the person's or Daoris's words wait (D137 §2). A
  solicited answer extends it once. Nothing else holds it.
- **The words held while a session winds up, or while a take runs**, keep the held-words file (`SessionWords.cs:93-96`,
  HANDWORDS1), since they must not land on a record that is ending. The rest of what it holds moves to `said`.
- **The channel delivers by identity, and its mark is for display.** Each entry's acknowledgments are kept by message id
  (§9): `taking` (a part claimed it), `sent`, `taken`, `read` (the agent's own `channel_read` returned it whole) and `partly`
  (a start's packet carried it cut, or counted it among the older ones left out). An entry is handed a message it has neither
  taken nor read, whatever its moment, so a message mirrored late, or at the mark's very moment, is handed all the same. An
  ordinary message waiting while a later urgent one is taken keeps its place: each has its own row, and neither is skipped. The
  in-memory `_reached` map gives way to these rows.
- **Every hand that moved the mark writes acknowledgments instead.** The look writes them for what a part claimed and took. A
  part's start writes `sent` for each message its packet carried whole, then `taken` with the start's first prompt, and
  `partly` for each it cut or left out: those are handed again to a new part, and the agent reads them whole with
  `channel_read`. `channel_read` writes `read` for each message it returns whole. That is the agent's own call, so an answer
  lost on its way is a failed call the agent makes again with `since`. The mark is still moved, to the oldest message the
  entry has not acknowledged, for the page's unread counts and for a host from before.
- **One part takes a message.** D167's amendment lets an entry hold several live parts. They are tried newest first, as the
  look's selector picks the newest today (`ChannelReach.cs:74-92`), and the first whose door takes the message at its level
  claims it. The claim is one statement that succeeds only where the entry holds no acknowledgment of the message, or only
  `partly`. A second part's claim finds the row and takes nothing. An alongside part reads it with `channel_read` at its own
  point. A part is never handed a message twice. A claim left at `taking` by a restart is handed to the entry's next part, as a
  new conversation, never again to the same one.

## 7. Four receipts, each recorded where

ADR-D's states, as Daoris can observe them, and only where it can. **Acted upon means answered, never obeyed**: Daoris
cannot know whether an agent did what a message asked, and it does not claim to. **A state is shown only where it was
observed**: a door that cannot confirm a word taken stops at *sent* (§3.0), and the page never shows a *taken* it did not see.

| State | en / zh | Means | Recorded, for a word to a session | Recorded, for a channel message |
|---|---|---|---|---|
| **queued** | held / 已暂存 | kept, and waiting for its point | on `said`, and the record's `user` event with `reaches` and the new `level`, shown at once (D136 §3) | the `channel` row (posted) |
| **acknowledged** | sent / 已发出 | the write to the harness returned: the prompt, the steer, the start or the resume's opening | `sent` under the word's id, when the write returns, never when the prompt is answered | the entry's acknowledgment `sent` |
| **delivered** | taken / 已收下 | the door confirmed the agent took it | written only on the door's word: a steer answered `injected`; a queued prompt the harness handed its turn to (STEER1's answer); a prompt to an idle session, at the first update of its turn; the native conversation, at the turn's first event after the line; the native run, at its first event. Today's *taken*, written before the wire for a prompt to an idle protocol session (`Acp.cs:1110-1119`), moves after it | the entry's acknowledgment `taken` |
| **acted upon** | answered / 已回应 | the turn that took it ended with an answer, or a correlated reply came | a receipt event, `answered`, when the turn that took it ended `end_turn`; and the reply's id where one is correlated: a channel reply's `re`, `helper_answer`, `opinion_answer`, `quest_respond` | the reply's `re`, and the entry's acknowledgment |

The channel has two more states of its own, neither of them *taken*: **read** (已读取), where the agent's own `channel_read`
returned it whole, and **partly handed** (部分送达), where a start's packet carried it cut, or counted it among the older
ones left out (§6).

Three outcomes close a word without the last state. *Uncertain* (可能已读) means it was attempted before a restart and never
confirmed taken (§6). *Not reached* (未送达) means it expired, or the record can never go on (§4.3). *Taken back* means the
person withdrew it, the existing `work.conversation.withdrawn` (*Taken back before it was sent.* / 发送前已收回。).

**Who sees them.** The page shows them on the conversation and on the ask's channel (§8). `channel_read` answers a poster
the receipts of its own messages, per addressed repository: *waiting*, *read*, *taken* or *not reached*. The machine log keeps
none: these are the record's facts, not measurements.

## 8. How it shows

### 8.1 The composer while a turn runs

- **One press, *Send*,** at the person's default, urgent, with a tip that says what the door will do: *it reads this at
  its next step*, or *its door takes words only when its turn ends*.
- **A level menu beside it**, with three items. *When its turn ends* (ordinary). *At its next step* (urgent, the default).
  *Now, cutting its step* (now), offered only where the agent steers; elsewhere it is shown disabled, with the reason.
- **Enter sends at the default**, as Enter queues today.
- **No level is remembered**: each word starts at the default.
- **On a text-only conversation there is no menu.** The tip says its words go at once, whatever their level
  (`work.composer.levelNoTurns`).

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
- **Receipts fold under a taken message**: *sent 14:02 · taken 14:03 · answered 14:09*, each only where it was observed
  (§7). A message that never reached its agent says so at its place. An uncertain word says *It may have read this* at its
  place, with *Say it again* where the words are the person's own.

### 8.3 A wait for the person

The third answer of §2.3's bound is a wait the person answers. It is one new kind in `work/waits.ts`, with its places as
`person-waits.md` lists them: the head, the foot, *What needs you*, the badge, the hook named in Ask Daoris's coverage, and
the words. Its acts are *Go on with this answer* and *Not now*. Not now hands the answer to the person, as an answer
nobody takes is handed today.

### 8.4 The ask's channel and a session's channel panel

- Each message names its level where it is urgent.
- For each repository it addresses, the message shows its receipt: *waiting*, *partly handed to `repository`*, *read by
  `repository`*, *taken by `repository`*, *answered* or *not reached*.
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
| `work.conversation.held.claimNextPart` | sentence | Held: it takes another agent's words during a turn, beside words it gets anyway, at a new part, or when it reads the channel. | 已暂存：它只在一轮进行中、随它本就会收到的话、新的一段开始时，或自己读取频道时，接收其他智能体的话。 |
| `work.composer.levelNoTurns` | sentence | This agent's door carries only text, so your words go at once, whatever their level. | 这个智能体的接入方式只传文本，所以你的话会立即发出，不论紧急程度。 |
| `work.conversation.sayAgain` | button | Say it again | 再说一次 |
| `work.steer.stopKeeping` | button | Stop its turn so this goes next… | 停止本轮，让这条接着送达… |
| `work.steer.stopKeepingAsk` | sentence | This stops the step it is on and its turn. What it has done stays, and what waits goes next. | 这会停止它正在进行的这一步和本轮。已完成的工作保留，等待中的内容接着送达。 |
| `work.queue.makeUrgent` / `makeOrdinary` | menu item | Make urgent / Make ordinary | 改为紧急 / 改为普通 |
| `work.receipt.held` / `sent` / `taken` / `answered` / `notReached` | state | held / sent / taken / answered / not reached | 已暂存 / 已发出 / 已收下 / 已回应 / 未送达 |
| `work.receipt.read` / `partly` / `uncertain` | state | read / partly handed / may have read | 已读取 / 部分送达 / 可能已读 |
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
| `SaidWord` (`Sessions.cs:329-330`) and the driver's `SaidWordView` | `Level` (`ordinary` / `urgent` / `now`); `Key`, the producer's (§6); `Attempt`, the moment the dispatcher first wrote it to a harness; `From`, its author where that is not the person: `daoris` (FABRIC-CLOSE1c17), or `agent` for a claim the person passed on (§4.2), whose `Channel` names the message | no level: the person's word as urgent, every other word as ordinary, which is today's reach for each; no key: each word its own, as today; no attempt: never attempted; no `From`: as before, by `By` and `Helper`. **Authorship is never read from `Channel`**, which says only how the word came: the person's own channel message is kept with it (`ChannelReach.cs:202-210`) and is the person's |
| The record's `user` event (`SessionEvents.cs:166-172`) | `level`; the receipt events `sent`, `taken` (written on the door's word, §7), `answered` and `uncertain` under the word's id | no level: as before; no receipts: shown as today, said and taken only |
| `channel` row and `ChannelWire` (travels) | `urgent`, **said on every message kept from now**: true, or an explicit false, decided in the insert's own statement, so a part's fourth urgent post is kept false | absent only on a message from before, or from an older teammate's machine: it keeps today's reach, next step where the door takes words then, and shows no level. Absent never means ordinary, so a new ordinary post is never mistaken for one from before. An older machine reading a new message ignores the field and keeps its own reach, which this machine cannot change |
| The channel's acknowledgments (new, in the channel's store beside the entry's mark) | one row per ask, entry and message: `taking`, `sent`, `taken`, `read` or `partly`, the part's session and the moment (§6) | an entry with a mark and no rows counts every message at or before its mark as taken, as today |
| `helpers/<id>.ask.json` (twins: `HelperBox` and `HelperFiles`) | `urgent` | ordinary, today's delivery |
| `SESSION_INPUT`, `SESSION_QUEUE_EDIT` | `level` | `now: true` is read as `level: now`; nothing is the default |
| `sessions say` request file (`SessionRequest`) | `level` | `now` as before |

`said` taking words for a running record is the say door's widened judgement (`POST /api/sessions/{id}/say`), not a new
field. The record's `said` stays machine-only and never travels (`session-record-fields.md`).

## 10. The traps, each a test

1. **A message stops a turn.** No delivery path calls a door's interrupt. Only the stop routes do: `CANCEL_TURN`, the
   session's stop, the keeping stop. *Test:* `DrivenInboxTests` and `ChatTurnsTests`: *now* on a turn-end door sends no
   interrupt, the turn runs on, and the answer is `no-interrupt`; the native driven run's line offers no keeping stop. Also a
   census of the interrupt's callers.
2. ***Send now* with nothing typed stops a turn.** It is not offered. *Test:* the composer's test, en and zh.
3. **An unsolicited claim extends a run.** At a turn's end with only claims held, the run ends and the claim waits for the
   next part. A claim that arrives once the answer to the inbox's last open prompt is read is refused (§2.3). *Test:*
   `ChannelNextStepTickTests`' stub that declares nothing, kept, and an urgent claim on it; `DrivenInboxTests`: a claim held
   after the last prompt's answer is read is refused, while a person's word is held.
4. **An ordinary claim enters a running turn.** *Test:* `ChannelReachTests`: an ordinary claim to a next-step part is not
   held, and the connector's next answer says it waits; held at a turn's end beside the person's word, it rides that prompt.
5. **An agent's words read as the person's, or the person's as an agent's.** *Test:* `SaidWord.Persons` excludes `helper`,
   `by` and `from`, and reads nothing from `channel`: the person's channel message, kept with `channel`, is the person's, and a
   claim they passed on, kept with `from: agent`, is not. The intake's DRIFT1 never takes an agent's word as the person's.
6. **Daoris's words read as the person's** (FABRIC-CLOSE1c17). *Test:* the person's words equal to Daoris's sentence are
   the person's; Daoris's carry `from` and sync as Daoris's; an older host's word reads as the person's.
7. **A restart loses a word.** *Test (Process):* a conversation's waiting word and a driven session's held word, never
   attempted, survive a shell restart and go in their order.
8. **A restart sends a word twice.** A word attempted and not confirmed taken before a crash is not sent again. *Test:* the
   come-up reads `said`'s attempts and the receipts; the word reads *it may have read this*, and the person's offers *Say it
   again*.
9. **The channel passes a message held but not taken.** *Test:* a restart between hold and take hands the message to the
   entry's next part, never twice to the same one; an ordinary message waiting while a later urgent one is taken is handed
   after it; a message whose moment equals the mark's, and one mirrored late with an older moment, are handed.
10. **Urgent flooding.** A part's fourth urgent post is kept as ordinary, and the tool's answer says so. *Test:*
    `ChannelTests`' bound, in the insert's own statement as the ten-post bound is, writing an explicit false; a new
    ordinary post reads as ordinary, never as a message from before.
11. **Answers alone loop.** Under `helpers: on`, the third answer in a row with nobody between waits for the person.
    *Test:* the count over a stand-in, through the driver's go-on and a conversation's turn pump alike, across a hand-over,
    and with two answers handed at once under separate asks; an urgent answer taken inside a running turn counts nothing;
    the wait's places (`person-waits.md`).
12. **A level from before.** *Test:* a `SaidWord`, a channel row and a helper ask with no level each read as their old
    reach; an older page's `now: true` reads as *now*.
13. **Urgent past ordinary is out of order.** *Test:* an urgent word goes at the next step past a held ordinary one; at the
    turn's end, the rest go in the order said, and the record shows each where it was taken.
14. **A steer answered `promptRequired`** (STEER2d) at *now* still goes first. *Test:* `AcpSteerTests`' case, kept.
15. **A claim vanishes, or is closed while a part could still take it.** *Test:* an ask whose quests all close, with no part
    of the entry running, marks each untaken message *not reached* for its entry; with a part still running, it does not.
    `channel_read` and the page say so.
16. **A meeting's barrier pierced.** No level reaches a turn in a round. *Test:* `MeetingTests`: a message posted at
    urgent during a round reaches no turn of it.
17. **Daoris's word said late, or not said to a running session.** A reopen answered by a later submission is dropped, not
    told, judged by the key the word carries. A running session is told at its next step. *Test:* `ReopenTelling`'s tests at
    urgent, a running record among them.
18. **Words cut or clashing.** *Test:* stories in long 中文; `i18n:check`, `names:check -- --strict`.
19. **A producer's retry keeps a second word.** *Test:* the say door's tests: a second say with a key the record holds,
    waiting or taken, keeps nothing new and answers the word kept; `ReopenTelling` failing between its say and its mark tells
    one word.
20. **Two senders reorder the queue.** *Test:* two words kept at once to one running session reach it in `said`'s order,
    through its one dispatcher.
21. **Two parts take one message.** *Test:* `ChannelReachTests` with two live parts of one entry: one claims the message, the
    other takes nothing.
22. **A mark mover passes a message.** *Test:* `channel_read` acknowledges only what it returned whole; a start's packet
    acknowledges its cut and left-out messages as `partly`, which a new part is handed again.
23. **A text-only conversation claims a level.** *Test:* its composer offers no menu and says why; its words go at once, and
    nothing on its record says *taken*.

## 11. What only a measured turn can settle

The parent runs these with the owner, on accounts the owner names. They are not this design's rows.

- **STEER3.** It measures Codex's `turn/steer` (does `preempt` cut a step?), Claude Code's `--input-format stream-json` on a
  driven run, and whether an empty steer at `now` folds words already queued. Until then, the turn-end doors are §3.1's. It
  also measures whether a next-step door answers a queueing prompt to an idle session apart from one it folds in, which
  would close §2.3's race on that door.
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
- **Sending an attempted word again after a restart** (DESIGNREAD2). No harness drops a repeated prompt by its id, so a
  replay could make the agent act twice. An uncertain word is shown, and only the person says it again.
- **An urgent bound per entry** (DESIGNREAD2). The ten-post cap it sits inside is a part's. Counted over an entry's whole
  life, a part started days later would find the bound spent by parts long ended. So it bounds a part, and an entry with
  several live parts may send three from each: the person's alongside requests made those parts.
- **An absent `urgent` read as ordinary** (DESIGNREAD2). It cannot be told from a message from before.

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
  words then. An ordinary one waits for a natural point. The entry acknowledges each message by its id, and its mark is kept
  for display. One part of an entry takes a message. A message never taken expires visibly, and the person may pass it on.
  The co-working design §2.3's table is read with §5's.
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

**D173's ADR-D** is this design's for delivery: durable, correlated by id, kept once by the producer's key, at most once
across a restart with an uncertain outcome said, and the four states told apart where a door can observe them (§7). D173's
note points here.

## 14. The rows

**The rows follow D187.** Each is one feature a person can see working, built end to end across the lanes it needs by one
builder, and handed back working. The rule is built in sub-features, each working on its own: the person's words first,
then Daoris's, then the durable queue, then agents' messages. STEER3 and BGWAIT2b stay the parent's, with the owner.
Each row names its Ask Daoris door or its reason (D110).

- [ ] **COWORK1a — the person's words reach a working agent at the level they choose, and no word stops a turn** (driver, modules, web-shell; D188 points 1-3; design §2, §3, §5, §8.1-§8.2, §8.5-§8.6, traps 1, 2, 12-14, 23). `SESSION_INPUT` and `sessions say` take `level` (`now: true` read as now); `DrivenInbox` and `ChatTurns` hold ordinary for the turn's end, send urgent at the next step, steer *now* where the agent steers and otherwise say why, never interrupting, door by door as §3.0 has it; the keeping stop as its own press and `sessions stop --turn`, offered only where the door has an interrupt that keeps the session, never on the native driven run; a text-only conversation's composer offering no level and saying why; the composer's level menu, the held line's level and `SESSION_QUEUE_EDIT`'s `level`; the glossary's *urgency*; Ask Daoris rows for any new hook. **Trap:** `DrivenInbox.Flow()` drains every held word onto the wire once the first prompt leaves (`DrivenInbox.cs:110-121`), ordinary ones with them, and the turn-end fallbacks of `HoldNowAsync` and `TakeNowAsync` still call the interrupt (`DrivenInbox.cs:252-256`, `ChatTurns.cs:331-335`); a builder who changes the default reach and leaves these, or offers the keeping stop on a door with no interrupt, ships a stop or a press that does nothing. Test each path apart. Opus. Proof: `DrivenInboxTests`, `ChatTurnsTests`, `AcpSteerTests`, `SessionsSayCommandTests`, `DriverModuleConversationTests`, the web's tests en and zh, `i18n:check`, `names:check --strict`; seen on the window: a word at each level on Claude Code's door and on the stub's, and *Send now* on the stub holding without a stop.
- [ ] **COWORK1b — Daoris's own words marked as Daoris's, and its urgent words reach a running session** (service, driver, web-shell; after 1a; absorbs FABRIC-CLOSE1c17; design §2.2, §4.1, §4.3, §5, §6's producer key, traps 6, 17, 19). `SaidWord.From` (`daoris`) through the say door and the record's chain (`session-record-fields.md`, a word's field); `SaidWord.Key` and the say door keeping a word once by its producer's key, first used here; `ReopenTelling` says the reopen and the step's go-on at urgent, each carrying its fact's key, into a running session at its next step and held elsewhere, its own return on a running record removed, dropped when a later submission answers it, and on a park its answer as today; the gate reads the mark, not the sentence; the conversation shows them as Daoris's. **Trap:** widening the say door and leaving `ReopenTelling`'s own `if (record.Running) return;` (`ReopenTelling.cs:152`), which still tells a running session nothing; or judging whether a reopen still stands, or whether words are Daoris's go-on, by the sentence's text as `BringsToSource` does (`ReopenTelling.cs:206-210`), instead of by the key and `from` the word carries. Opus. Proof: the say door's tests (a repeated key keeps one word), `ReopenTelling`'s (a running record; a failure between its say and its mark), CLOSE1c17's three cases, the page's en and zh; seen on the window: a reopen told to a running Claude Code session.
- [ ] **COWORK1c — every word to a session waits on its record, goes once or is shown uncertain across a restart, and shows the receipts its door can observe** (service, driver, modules, web-shell; after 1a and 1b; absorbs MSG1d2; design §6, §7, §8.2, §9, traps 7, 8, 20). The say door keeps words for a running record, once by key, the page's press giving one key per press; one dispatcher per session drains `said` in its order, and the driven inbox and the conversation's turns are its views; `attempt` marked on the word before the wire, *sent* at the write's return, *taken* only on the door's word (today's early *taken* on an idle protocol prompt moved after the wire), the word taken off by id, awaited; at the come-up a word never attempted goes and one attempted is *uncertain*, with *Say it again* on the person's own; the receipt events; the page folds them under a word, each only where observed. **Trap:** taking `SendPromptAsync`'s completion for *sent*, though it completes with the prompt's answer (`Acp.cs:2238-2258`); keeping the take-off fired and forgotten (`ChatHelperWords.cs:54`); giving words the queues' own ids (`said-n` at `DrivenInbox.cs:200`, a fresh one at `ChatRunner.cs:2102`) instead of `said`'s; or letting a caller hold a word itself once its own write returns, which reorders two senders. Opus. Proof: the service store's tests (a running record's `said`, an elder record, a key kept once, an attempt), `DrivenInboxTests`, `ChatTurnsTests` (two words kept at once go in `said`'s order), a `Process` test of a shell restart with a conversation's and a driven session's waiting word and one attempted word, the page's en and zh; seen on the window: a word waiting across a restart, and an uncertain one.
- [ ] **COWORK1d — agents' messages by level: an ordinary claim waits for a natural point, an urgent one reaches the next step, a helper's answer at its ask's level, and nothing undelivered vanishes** (service, driver, web-shell; after 1c; design §2.2-§2.3, §4, §5, §6's channel, §8.3-§8.4, §9, traps 3-5, 9-11, 15, 16, 21, 22). `channel_post`'s `urgent` with its bound, kept true or an explicit false in the insert's own statement, and `ChannelWire`'s field said on every message; the entry's acknowledgments by message id, written by every hand that moved the mark (the look, a start's packet, `channel_read`), with the mark kept for display; one part of an entry claiming a message; an ordinary claim riding a prompt the run sends anyway; the inbox refusing claims once its last prompt's answer is read (§2.3's race); the connector's answers naming what waits; expiry when the ask has no quest open and no part of the entry runs here; `channel_read`'s receipts; a claim passed on kept with `from: agent`; `helper_ask`'s `urgent` (both twins) and its answer at the next step; the answers-alone count read from the line's records, through the driver's go-on and a conversation's turn pump, across hand-overs, judged by the session's dispatcher, its third answer a person's wait (`person-waits.md`'s places); the ask's channel and the session's panel with receipts and *Send to its session*. **Trap:** fixing `LookAtAsync` and leaving the other two hands that move the mark by moment, `channel_read` (`Channel.cs:544-569`) and the start's bounded packet (`ChannelReach.cs:101-133`); writing a new ordinary post with `urgent` absent, which reads as a message from before and so reaches the next step; or counting answers alone in the driver's go-on only, which a conversation's turn pump (`ChatHelperWords.cs:47-62`) and a second ask's own claim (`DriverHelpers.cs:137-159`) pass by. Opus. Proof: `ChannelTests` (the explicit false, the acknowledgments, one claim per entry), `ChannelReachTests` (two live parts, an ordinary message behind an urgent one, equal and late moments), `ChannelSyncTests`, `ChannelNextStepTickTests` (`Process`), the MCP tools' tests, `HelperBoxShapeTests` and `HelperFilesTests`, `MeetingTests`' barrier, `HelpCoverageTests`' rows, the page's en and zh; seen on the window: two sessions of one ask, an ordinary and an urgent post, a message not reached.

**Rows that change** (the parent's to write into TASKS):
- **MSG1d2** is COWORK1c's.
- **FABRIC-CLOSE1c17** is COWORK1b's.
- **MSG1h and MSG1i**, after STEER3: each door is decided again under §3.1. A measured next step carries *urgent*, and a
  measured cut carries *now*.
- **FABRIC-EVENT3c** wakes the delivery of §5 on the channel's family, in place of the look's read.
- **SESSONE2i** carries a post's level to a teammate's ask.
- **AGENTROOM1b**'s `meeting_say` is a meeting's message: no level, the barrier's.
