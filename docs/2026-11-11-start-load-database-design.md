# Proposed design: an index over the session events, and a change mark for the quests (STARTPERF1, D275)

**Standing:** proposed. D275 is reserved for the choice in §6, taken after Codex's read. Nothing in STARTPERF1's branch moves storage;
the [measurement](archive/2026-11-11-start-load-measurements.md) is the evidence it reads.

## 1. What the measurement says of the storage

- **The session records and the quests are already in a database.** The service's SQLite store holds 1,261 session records and 950
  quests, each table read whole in 7-12 ms. Moving them changes nothing at a start: the start's time went to a wait on itself, the index
  read at first use, process starts and trees walked in turn (the measurement's causes 1-4), none of them a file read.
- **What is files and is read whole** is the session events: 1,230 records, 517 MB, the largest 6.47 MB. Opening a session reads its
  record whole for its newest page (`Page`), and `After`, `TurnsEnded`, `LastPlan` and `ChoicesOf` read it whole for one answer; what a
  part spent did until STARTPERF1 kept it (`SessionSpentKept`).
- **What has no change mark** is the quest list: about sixty desktop readers each ask the service for every quest, closed included
  (4 MB, 917 of 950 Done), because nothing says whether the list moved since the last read. The session list has one
  (`session_revision`, `GET /api/sessions/changes`, `SessionListKept`).
- **The run records** (941 runs and 922 units, JSON files) are read once per file stamp; the nodes stage's time at a start is its git
  and its queue, not its reads. They stay files.

So a database is called for narrowly: an index over the event records, and a mark for the quests. The session records stay where they are.

## 2. The session events: heads and offsets in the desktop's store

- **`session_event_heads`** in `desktop.db`, a row per session: the file's length as last indexed, the last number, when it was last
  written, the turn's standing (ended or under way), the opening, the last plan's number, the first failed call's number, and the four
  counts spent with the turns and those reported. Written by `SessionEvents.Append` under the lock that writes the line, after the line.
- **`session_event_offsets`**, every 256th event's byte offset by session and number: a page before a number seeks to it rather than
  parsing the record from its start.
- **The file stays the record**: append-only JSONL, machine-local (D76), read by a person or a tool as it is. The store is an index over
  it, and a row whose length differs from the file's (another writer, a crash between the line and the row, a salvage's move) is
  rebuilt from the file at its next read.
- **Readers**: `Spent`, `TurnsEnded`, `LastAt`, `LastHandedAt`, `TurnEnded`, `Openings`, `LastPlan` and `ChoicesOf` read the head;
  `Page` and `After` read from the nearest offset; `SessionSpentKept` goes, its job the head's.
- **Rejected: the events as rows.** 517 MB of text in a store that is 3 MB, every line written twice or the file given up, and the
  file's readers (a terminal's tail, a person) given a second way to read a conversation. The head answers the reads for a few hundred
  bytes a session.
- **Rejected: the service's store.** The events are this machine's (D76) and the service's store is what a shared host exchanges; the
  desktop's store is the machine's own.

## 3. The quests: a change mark

Built on STARTPERF1's second branch, as the sessions' change door is rather than by a `quest_revision` on each row: a quest's answer
moves with records beside it (its delete with a session that names it, its takes with its log), so the service keeps the list's answer
by the store's mark (`QuestListAnswers`) and answers each quest whose written answer moved since a mark (`GET /api/quests/changes`,
`ListChanges<T>`). The desktop keeps the list by its change (`QuestListKept`, as `SessionListKept`): every reader of every quest reads
the kept list, and one quest's lookup reads one record of it. A host without the door answers it not found and the desktop reads the
whole list, as it does for an older host's session list. No store column moved.

## 4. Migration

- **Heads**: built from each file at its first read where no row stands, then kept by the writes; a home migration step builds every
  row at the first start after the update, beside the looks and after the page's word (1,230 files, about half a gigabyte of reads).
  Dropping the tables reads the files as now: nothing is lost, the files being the record.
- **Quests' mark**: the service's store migration adds the column and the mark; a quest written before it reads as moved once.
- **The file records** are kept for every reader; none is retired by this design.

## 5. What it changes

Opening a long session reads a page instead of its whole record; the first ask of what a part spent reads a row instead of a parse; the
quest readers at a look share one read of what moved instead of each reading 4 MB. None of the start's four largest causes: those are
fixed or named in the measurement.

## 6. The choice D275 takes

Whether the session events get the heads and offsets of §2 in the desktop's store, or stay read from their files with kept reads like
`SessionSpentKept` for each reader; and whether the service keeps the quests' change mark of §3.
