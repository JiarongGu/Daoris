---
name: session-words
applies_when: handing a word to a running session or conversation (a new producer of words, a new kind of word, a new door that writes words to a harness), or changing how a word leaves its record
enforces: a word is kept on the record's `said` through the session's one dispatcher before anything holds it, handed to its door to be attempted before the wire and taken off once written, and a word a shell before attempted is settled as uncertain at the come-up, never sent again
---

# Words to a session — kept first, attempted, taken off

**Every word that may make a turn happen goes through the session's one dispatcher, which keeps it on the record first.** COWORK1c
(D188's note of 2026-10-20) built it; the message-urgency design §6 is the contract. A producer that holds a word in a process
itself loses it at a restart, and two producers that each hold their own word once their write returns reorder the queue.

## The dispatcher

- **A driven session's** is its `DrivenInbox` (`SessionProcesses.InboxOf`); **a conversation's** is its `ChatTurns`. Keep a word
  with `DrivenInbox.KeepOrHoldAsync` (or `KeepAsync`) and `ChatRunner.SayKeptAsync` (`ChatTurns.KeepAsync`), through a `SaidDoor`.
  Never `Hold` a word that must survive a restart: `Hold` is the process-only fallback.
- **Give the word its producer's key** (`ChatMessage.Key`, or Daoris's fact's on `DaorisWord`): the say door answers a key a waiting
  word holds with that word, so a retry keeps one.
- **A record that is ending keeps nothing.** A dispatcher that stopped taking words answers `Closed` and takes off what it kept as it
  closed; the caller holds the word as words said while a session winds up are held (`SessionWords`' held file, HANDWORDS1).
- **A record that is not its line's head keeps nothing either** (FOUNDATION1d-ii): a host with the `head` mark refuses the word
  `continued`, naming the head. The dispatcher holds, queues and attempts nothing (`InboxKeep.Continued`, `TurnKept.Head`), never its
  fallback for a refusal, and its caller says the word at the head whole: `WordsHead.FollowAsync`, or the shell's and the headless door's
  own judgement there. A new say caller is named in `ContinuedReadersCensusTests`, or that census fails.
- **A keyed open moves the old part's words itself** (FOUNDATION1d-ii part 3, against a host whose `opens` mark is 2): a site that opens
  a part and hands it words sends `MoveWords` (the record they leave, the `Carries` ids, the ids it read) through
  `SessionOpenDoors.OpenAsync`, and delivers and takes each word the answer's `Moved` names by its `Id` on the new part, never by the
  old record's ids. Where the answer's `Moves` is false and `Moved` empty, the host moved nothing and the site moves them as today.
- **Channel messages are not `said`'s** (COWORK1d): their queue is the channel itself.

## The door

- The dispatcher hands a kept word to its door through `SaidDoor.Dispatched`, which sets `ChatMessage.Attempting` and `Went`.
- **A door awaits `Attempting` before it writes the word, and calls `Went` once written**, or once it settled the word without the
  wire (a word of Daoris's dropped). `AcpSession` does both, in the order the words were asked to go; a door that writes words
  another way (the native door's resumed run, `DriverContinuations`) must too.
- A door that observes a receipt records it as a `receipt` event under the word's id (`Receipts.Sent`, `Receipts.Answered`); *taken*
  is the word's own event as the turn's ask, written only once the agent did something for it.

## The come-up

- A word a shell before attempted is uncertain: `UncertainWords` takes it back and says so with the `lost` note, at the loop's first
  look after the orphan sweep, and before a conversation taken up at the come-up goes on. A new place that goes on with a record's
  words at a shell's start settles them first.
