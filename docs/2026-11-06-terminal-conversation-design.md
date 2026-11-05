# RPC1g: a terminal's conversation is the running Daoris's

Status: proposed 2026-11-06 for the owner's yes (D238), read on main at `014c286d7`. It finishes the terminal RPC design
(`docs/2026-10-16-terminal-rpc-design.md`, D171 point 3), whose §7.1 left `chat` and `help resume` alone "until RPC1g" and whose
§11 put a conversation's turn in the instance with the terminal a viewer. It adds area **T** to ONEWAY1's census (D228) and is
laid out as the ONEWAY1 design's §3: each job's one way, what is kept, what retires, the records and what the person sees. The
reading was read-only; no test was run. Paths: `Drv/` `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Host/` `…Driver.Host/`,
`Mod/` `src/Daoris.Desktop/Daoris.Desktop.Modules/`.

## 0. In short

- **A conversation runs in the running Daoris alone.** A terminal never runs one. It *holds* one over the pipe: it opens it with the
  route the window presses, reads its lines as they are written, says words, stops a turn and finishes it.
- **One Confirm per conversation.** The person confirms the terminal's start (or its hold of a conversation already open) in the
  window, once. From then the words, turn stops and the finish that this connection sends to that conversation run with no further
  card, until the terminal lets go, the conversation ends or the person takes it back in the window.
- **The hold is the subscription.** The pipe forwards the held conversation's lines, the person's words said elsewhere, and its end
  to the connection that holds it. The terminal RPC design's module-wide `SUBSCRIBE` is not built.
- **No instance, no conversation.** With no Daoris answering on the home, `chat` refuses and says how to open it. Nothing runs
  alone.
- **One verb holds a conversation:** `daoris chat`, with `--repository` to start one and `--session` to hold one already open.
  `session start-from` and `session hand-over` send their routes and hold what they open. `ask-daoris resume` goes.
- **Retired:** the terminal's own runner, its agents' job, `TerminalOwner`, `TurnMarks`, the request folder's stop for a terminal's
  conversation, and the window's branches for a conversation another process runs.

## 1. What is true today

`daoris chat --repository <name>` (`Host/ChatConsole.cs:99-139`) builds its own `ChatRunner` over its own `AdapterSet` and
`SessionProcesses` (`:146-198`) and runs the conversation in the terminal's process: stdin is the person, stdout the console, Ctrl+C
in a turn stops the turn, Ctrl+C at rest stops the conversation, end of input finishes it (`:176-289`). `session start-from` and
`session hand-over` open their new conversation the same way (`:42-61`, `:86-97`; `Host/SessionsConsole.cs:31-33`). The process takes
no home lease, so it runs beside a running desktop.

Because the conversation lives in another process, the window can only watch it from outside:

- `TerminalOwner` (`Drv/Loop/Chat/TerminalOwner.cs`) writes the turns' state to a file per conversation (`TurnMarks`,
  `Drv/Course/Requests/TurnMarks.cs`, `<home>/sessions/requests/<id>.turn`), and honours a stop asked through the request folder.
- The window reads those marks for the queue and refuses a turn stop of a conversation "run by another Daoris process … a
  terminal's" (`Mod/Bridge/Routes/ConversationRoutes.cs:364`, `:499-525`). Its words cannot reach the conversation either, and the
  loop's looks (a limit's wait, a move, a hand-over at a turn's end) do not run in the terminal's process.
- TERMTRUST1 makes a terminal's `chat` a Daoris process that runs agents, with an agents' job of its own
  (`docs/2026-10-23-terminal-trust-design.md` §3.3 point 3).

`ask-daoris resume <id> <words>` is already the say door (`Drv/Help/HelpCommand.cs:296-302`), a second verb for `session say`.

Over the pipe every conversation route is the person's: `START_CHAT`, `SESSION_INPUT`, `CANCEL_TURN` and `END_CHAT`
(`ConversationRoutes.cs:38`, `:87`, `:339`, `:317`) and `START_HELP` (`HelpRoutes.cs:35`), since "words said to a session, a
conversation or a channel are theirs" (`Mod/Platform/PipeCensus.cs:110-116`). A terminal that sent them one by one would put a card in
the window for every line it said. The reads are open: `TAIL_SESSION` (`ConsoleRoutes.cs:22`) and `SESSION_HISTORY`.

## 2. The census's area T: a terminal's conversation

- **T1, two runners of conversations on one home.** The terminal's `ChatRunner` (`ChatConsole.cs:146-198`) beside the instance's
  (`Mod/Loop/LoopRun.cs`), each with its own adapters, processes and looks. True second way: a conversation's course runs where its
  process is.
- **T2, turns told through files.** `TurnMarks` and `TerminalOwner` beside the instance's queue read (`SESSION_QUEUE`), and the
  window's branches that read the marks. True second way, a second source of a conversation's state.
- **T3, a terminal conversation's stop through the request folder**, beside `STOP_SESSION`. True second way.
- **T4, three verbs that open a conversation in the terminal** (`chat`, `session start-from`, `session hand-over`) beside the routes
  the window presses (`START_CHAT`, `SESSION_START_FROM`, `SESSION_HAND_OVER`). Two ways to open.
- **T5, `ask-daoris resume` beside `session say`.** Two verbs for one word to a conversation.

## 3. The one way

### 3.1 The hold

A **hold** binds one pipe connection to one conversation. It is the pipe's, kept in `PipeState` beside the presses under way, never
on disk.

- **Made by the person's Confirm.** A request the terminal sends with `hold: true` beside its envelope (as `key` is sent today) asks
  for it: `START_CHAT`, `SESSION_START_FROM` or `SESSION_HAND_OVER`, which hold the conversation their answer names, or
  `DAORIS.PIPE` `HOLD { session }`, which holds one already open. Each is a person's press, so the window shows the card that every
  terminal press shows (RPC1c), with one more sentence: *Confirm also lets this terminal speak for you in this conversation until it
  lets go.* Refused or withdrawn, nothing is held.
- **What it covers.** On the connection that holds it and for that conversation only: `SESSION_INPUT` in the verb's form (`said:
  true`, the words, `paths` for attachments, no `question`, `draft` or `preface`), `CANCEL_TURN` in either form, and `END_CHAT`.
  Each runs as a confirmed press, marked `door: terminal` as every pipe press is. Every other press, about this conversation or any
  other, asks as today.
- **One holder at a time.** A conversation held by one connection refuses a second hold with `held`, naming the terminal's process.
  The window is never a holder: it says words to a held conversation as to any other.
- **How it ends.** The connection closes; the terminal sends `DAORIS.PIPE` `LET_GO`; the conversation ends; or the person presses
  *Let go of the terminal* on the conversation's head in the window (`SESSION_LET_GO`, a window route, `Window` in the census). Each
  end is told to the connection (`held: ended`, with its reason) and logged as `person.hold` with `state: ended`; the start is logged
  with `state: held`. A restart ends every hold, since every connection closes.
- **Under an update.** `SESSION_INPUT` waits for the drain (`PipeDrain.Waits`): a held word sent then is refused `stopping`, `why:
  update`, as the window's would be, and the hold stays. `CANCEL_TURN` and `END_CHAT` pass.

Why it is safe to cover these. A hold is bound to a pipe handle that only the terminal's process holds, so no other process speaks
through it; another process that wants a conversation asks for its own hold, which is a card. What the hold covers is what the
terminal did with no gate at all while it ran the conversation itself. It is never standing: it names one conversation and lives as
long as one connection.

### 3.2 What the terminal reads

The hold is also the subscription. While a connection holds a conversation, the pipe forwards to it, as the kit's event envelope, that
conversation's `SESSION_OUTPUT` batches (`Mod/Loop/OutputTold.cs`), its `SESSION_EVENTS` batches and its `SESSION_ENDED`, and
nothing of any other conversation. The terminal prints the console lines as it does today. From `SESSION_EVENTS` it prints only the
person's words said in the window, as `chat: said in Daoris: …`, so its transcript is whole.

- **No gap.** The hold starts forwarding before the terminal reads the backlog. The terminal then reads `TAIL_SESSION` and drops a
  forwarded line whose sequence it already printed.
- **A slow terminal.** The pipe keeps at most a bounded queue of forwarded lines per connection. Past it, the pipe drops the oldest
  and sends `gap` with the last sequence dropped, and the terminal reads `TAIL_SESSION` again from its last printed line. The bus never
  waits on a connection.
- **The terminal RPC design's `SUBSCRIBE { modules }`** (§6) is not built. It had no consumer but this one, and it would forward every
  conversation's lines to every subscriber.

### 3.3 The verbs

| Verb | Sends | Then |
|---|---|---|
| `daoris chat --repository <name> [--agent] [--account] [--own-tree]` | `START_CHAT` with `hold: true`, the flags as its payload's `adapter`, `profile` and `ownTree`; no `adapter` walks Chat's order in the instance | holds what it opened |
| `daoris chat --session <id>` | `HOLD { session }` | holds it; prints its last lines from `TAIL_SESSION` first |
| `daoris session start-from <id>`, `session hand-over <id> --agent …` | their routes, with `hold: true` | holds the conversation the answer names |
| `daoris ask-daoris resume` | — | retired; `session say <id>` says one line, `chat --session <id>` holds the conversation |

Once held, the conversation reads as today's `chat`: stdin is the person, stdout the console.

| In the terminal | Today | Then |
|---|---|---|
| A line | `runner.Say` in the terminal's process | `SESSION_INPUT`, held, at the level the window's box uses |
| `:attach <path>` | the file read into memory | the path, sent as the window sends a picked file's (`paths`), checked by the route's own ground (`PipeReads.AttachedFiles`) |
| Ctrl+C, or a line of ETX, during a turn | the turn stopped | `CANCEL_TURN`, held, its answer in the verb's own words |
| Ctrl+C at rest | the conversation stopped | `LET_GO`: *the conversation goes on in Daoris; `daoris chat --session <id>` holds it again*, exit 0 |
| End of input | the conversation finished | `END_CHAT`, held; the terminal waits for `SESSION_ENDED` and prints how it ended |
| The terminal closed, or the connection lost | the conversation killed with the process | the hold ends; the conversation goes on in Daoris |
| The window lets go | — | *Daoris's window took this conversation back*, exit 0 |
| Daoris closes or updates | — | *Daoris is closing; the conversation is kept; hold it again once it is back*, exit 2 |

**With no instance.** `chat`, `start-from` and `hand-over` find the instance as every verb does (`Drv/Instance/InstanceDoor.cs`, the
terminal RPC design §7.4). Where none answers on this home, they do nothing and exit 2: *Daoris is not running on this home; open
it, then run this again.* A headless `drive` loop serves no pipe and runs no conversation, so it is said the same way, naming it.
A role's terminal is refused before it connects, as for every press that would be the owner's (`Drv/Instance/TerminalRole.cs`).

### 3.4 The census's area, settled

**T1 and T4: one runner, one way to open** (`RPC1g2`, `RPC1g3`).
- *One way:* the instance's `ChatRunner`; the window's routes open every conversation, the terminal's included.
- *Kept:* nothing of the terminal's runner.
- *Retires:* `ChatConsole`'s runner path (`ConverseAsync` and its `SessionOutput`, `SessionProcesses`, `AdapterSet` and attachment
  reading), `StartFromAsync` and `HandOverAsync`'s own conversations; TERMTRUST1's case of a terminal's `chat` as a Daoris process
  that runs agents. `ChatConsole`'s and `HelpConsole`'s `AdapterSet.Built()` calls are two fewer readers for ONEWAY1ah.
- *Records:* none. A conversation's records are the same whoever opened it.
- *Seen:* a conversation started from a terminal appears in Sessions as it starts, its turns live in every window, and the window
  can say words to it, stop its turn, hand it over or go on with it as with any other.

**T2 and T3: one state, one stop** (`RPC1g4`).
- *One way:* the instance's queue read and `STOP_SESSION`.
- *Retires:* `TerminalOwner`, `TurnMarks` and their tests (`TurnMarksTests`, `TerminalOwnerProcessTests`,
  `DriverModuleTerminalTurnTests`), the request folder's watch for a terminal's conversation, the window's branches at
  `ConversationRoutes.cs:364` and `:499-525`, and the `[CommandGroup("chat", Whole = true)]` mark on `TerminalOwner`.
- *Records:* a `*.turn` mark left under `sessions/requests/` is removed at the instance's start. That removal is a register entry:
  it ends when the owner's install finds none, and `RPC1g4`'s follow-up deletes it.
- *Seen:* a held conversation's head says *Held by a terminal* with the terminal's process, and offers *Let go of the terminal*.

**T5: one verb for one word** (`RPC1g3`).
- *One way:* `session say` for a line; `chat --session` to hold.
- *Retires:* `ask-daoris resume`, its usage line, its Help coverage row and HelpConsole's remark that resume stays the terminal's
  (`Host/HelpConsole.cs:41`).

## 4. Rejected

- **A card for every word.** The person would confirm each line they had just typed, and a card pressed without reading is no gate.
- **The standing confirmation naming words.** D201 never covers words said as the person (`terminal-trust design` §2.2): a process
  outside an agent's job rides the grant, and words are the strongest press there is.
- **Words as an open route.** The pipe confers no person's authority (the terminal RPC design §4.2): every agent of the account could
  speak as the person into every conversation.
- **Acting alone when no instance runs.** It is today's second runner. A desktop opened beside it shows a conversation it cannot
  reach, which is what `TurnMarks` and the request folder's stop exist to patch.
- **The verb starting Daoris when none runs.** A `chat` typed in an agent's shell would start the person's loop and put a window in
  front of them on an agent's word, and the loop would drive quests no one asked of a conversation.
- **The terminal's process becoming the instance for the conversation's life.** It would hold the home's lease for hours, so the
  desktop could not open; or it would be a third host of the loop.
- **`SUBSCRIBE { modules }`.** It forwards every conversation to every subscriber and has no other consumer (§3.2).
- **Ctrl+C at rest stopping the conversation.** The conversation is Daoris's now. Closing the terminal lets go, so Ctrl+C does the
  same; a stop is the window's, or `session stop`.

The cost, said: a machine with no screen has no terminal conversation. ChatConsole's reading of D50 (a conversation over ssh to a
screenless machine) ends; D50's rule for configuration stands. The headless loop runs no conversation today, so nothing that worked
through an instance is lost there.

## 5. Proof

- **Modules, over a real pipe** (`InstancePipeTests`' fixture): a confirmed start with `hold: true`, then words, a turn stop and a
  finish with no card; the same words from a second connection carded; a second hold refused `held`; the hold ended by the
  connection's close, by `LET_GO`, by the conversation's end and by `SESSION_LET_GO`, each told and logged; forwarded lines in
  sequence, and a `gap` after an overflowed queue; nothing of another conversation forwarded; a held word during the drain refused
  `stopping` with the hold kept.
- **The host, against a stand-in server:** each row of §3.3's two tables, the text for the lines `chat` prints today unchanged, and
  no instance refused with nothing done.
- **One process test end to end:** the built `daoris chat` driving a test instance with the rehearsal's stub harness, scripted on
  stdin, its record read out. This replaces the family rehearsal's §14 headless step (`tools/family-rehearsal.mjs:3479-3550`).
- **The census:** `PipeCensusTests` classes `SESSION_LET_GO` as the window's, and no source file names `TurnMarks`.
- **The installed window:** `daoris chat --repository …` with Daoris open: the card, then words; the conversation live in the window;
  a word from the window printed in the terminal; Ctrl+C at rest leaves it running in Daoris.

## 6. Rows, in build order

- [ ] **RPC1g1 — the pipe holds a conversation for one connection** (modules; D238). `hold: true` on a confirmed start or
  `HOLD { session }` binds the connection to the conversation; held `SESSION_INPUT` (verb's form), `CANCEL_TURN` and `END_CHAT` run with
  no card; the pipe forwards that conversation's `SESSION_OUTPUT`, `SESSION_EVENTS` and `SESSION_ENDED`, bounded with a `gap`;
  `LET_GO` and the window's `SESSION_LET_GO` end it; `person.hold` logged; the head says *Held by a terminal*. Contract: §3.1, §3.2.
  Proof: §5's modules tests; the card's sentence and the head's words in en and zh.
- [ ] **RPC1g2 — `daoris chat` holds the running Daoris's conversation** (driver host; after RPC1g1). `--repository` starts through
  `START_CHAT`, `--session` holds; lines, `:attach`, Ctrl+C and end of input as §3.3's table; refused with no instance. Contract:
  §3.3. Proof: host tests per row; the end-to-end process test; the rehearsal's §14 step removed.
- [ ] **RPC1g3 — start-from and hand-over hold what they open; `ask-daoris resume` goes** (driver host; after RPC1g2). Contract: §3.3,
  §3.4 T4 and T5. Proof: host tests; the usage and Help coverage without `resume`.
- [ ] **RPC1g4 — the terminal's runner and its patches deleted** (driver, modules; after RPC1g3). `ChatConsole`'s runner path,
  `TerminalOwner`, `TurnMarks`, the request folder's watch for a terminal's conversation, the window's branches, the terminal's
  agents' job; leftover `*.turn` marks removed at the start as a register entry. Contract: §3.4 T1 to T3. Proof: the deleted tests
  gone, the census test, the register entry's gate.
- [ ] **RPC1g5 — seen on the install** (needs the install). §5's last item.
- Conditional: **a headless instance**, only once a machine with no screen is a use: the headless loop hosts the modules' dispatcher
  and serves the pipe, and the same client reaches it. Until then nothing is built for it.

## 7. Where a read should press

1. **The hold widens one Confirm into many presses.** Its bound is the connection's handle. An agent that can type into the person's
   terminal, or inject into its process, speaks through the hold; that is the account's reach (D156 point 7), but the card must say
   plainly what Confirm lets through, and the read should test the sentence.
2. **No conversation on a screenless machine.** This gives up a capability ChatConsole was written for. If the owner wants it, the
   conditional row is the only way that keeps one runner; the read should weigh whether it is wanted now.
3. **An attachment becomes a path the instance reads.** Today the terminal reads the file itself, as its own user. Held, it sends the
   path and the instance reads it, as for a file picked in the window, but on the terminal's word under one Confirm; an elevated
   Daoris reads what a terminal that is not elevated could not. The route's own ground (`PipeReads.AttachedFiles`) is the check, and
   the read should say whether it must also hold the path to what the terminal's process can open. Ctrl+C at rest letting go rather
   than stopping is the other change a script may meet.
