# Clearing finished history from this machine (HIST1)

**Carried by:** HIST1 in `TASKS.md`. **Status:** design, written before the build, and **D153** records what it settles.
Nothing is built. §0 is what is true today, read from the code at `121503bf`; §1 onward is the design. Every name in it
follows `docs/2026-10-01-naming-design.md` (D116) and its glossary.

**Read with** D95 (a quest or an ask deleted while nobody started on it), D126
(`docs/2026-10-02-session-management-design.md`: §5 archive and delete, §7 the doors, §11), D132
(`docs/2026-10-02-pause-and-clean-up-design.md`, abandon), D88 (listed first, then pressed), D58 and D80 (strikes and
carry-ons are read from session records), D68 and the sync design's §8 (operations, cursors, tombstones), D47 §4
(records travel, processes never), D50 (two doors) and D37 (removing a record is a person's act).

**What it amends**, each marked where it lands:
- **D95**: a done or declined quest, and an ask whose work has closed, may now be cleared from this machine. D95 read
  the owner's *clear* as what the list already does, hiding a closed quest; *clear* now removes it here. D95's delete
  stays the act for a mistake, and stays the only removal that travels.
- **D126 §5.1, §5.4, §5.5 and §11**: a session that served a quest may be cleared once its quest is closed, with it or
  as one of its failed sessions; a landing's trace does not keep a cleared session; a teammate's record may be forgotten
  here with the quest it served.
- **The sync design's §8**: a forgotten quest is skipped by the fetch, and the store's numbers never go back (§3).
- **D94 §4**: one event, `history.cleared`. **D110**: a `clear` kind. **D116's glossary**: the term *clear*.

On 2026-10-07 the owner's install held 35 closed quests, 76 sessions that had served them and 24 MB of their
transcripts. Nothing in Daoris could clear any of it: a delete takes only an open quest nobody started on (D95) or a
conversation that served no quest (D126 §5.4), and archive only hides. It was purged by hand, with a backup under
`local/backups/`, and the owner asked for quest and session clean-up to be planned. This design gives a person one act,
**Clear**, for what finished work left on this machine: a closed quest with its ask, its sessions and its files; a
closed quest's failed sessions alone; and a workspace's finished history at once, listed first and then pressed. It
never touches work in progress, a tree, a branch, or the team's copy on a remote.

## 0. What is true today

### 0.1 What a person can remove

| Act | What it takes | What it leaves |
|---|---|---|
| *Delete…* a quest (D95; `QuestExchange.DeleteAsync`, `QuestExchange.cs:1288`) | an open quest no session names and no taken quest waits on: its row, and its log where it never left the machine, else a `deleted` operation that travels; its kept files | **every taken, done or declined quest**: refused (`KeptAsync`, `:1374`), *a closed quest already leaves the list* |
| *Delete…* an ask (D95; `AskDesk.DeleteAsync`, `Asks.cs:667`) | an ask with every quest it asked, each deletable; its kept files | an ask any of whose quests was started on |
| *Delete…* a session (D126 §5.4; `SessionLedger.DeleteAsync`, `SessionLedger.cs:986`; the driver's `SessionDeletion`) | a conversation that served no quest, ended, this machine's, named by nothing, with no tree here and never pushed; its record and what the home kept of it | **every session that served a quest** (`served-quest`), and every pushed record (`on-remote`) |
| *Archive*, *Archive what ended…* (D126 §5.2–§5.3) | a mark in `sessions/archived.json` | the record and every file, by design |
| *Abandon…* (D132) | declines open work, discards trees only Daoris holds, archives sessions | every record: archived, never deleted (D132 §3.5) |
| *Clean up…* (D88, D102) | landed trees and branches, and branches landings made once they read on the line | every record |

So a closed quest, its log, its ask, its sessions, their transcripts and conversations, and its kept files stay on the
machine for good, and every list that asks for closed records (`GET /api/sessions` asks for all of them, D126 §1.2) asks
for more each day.

### 0.2 What the hand purge removed

The purge of 2026-10-07 had to remove all of these, which is the inventory §2 covers, each verified against the code:
in `knowledge.db` the tables `sessions`, `quests`, `quest_log`, `asks`, `quest_cursor` and `quest_passes`; under the
home `sessions/<id>.log`, `.events.jsonl`, `.pid`, `.harness.json`, `.cannot.json` and `sessions/<id>/files/`; the kept
files under `quests/<id>/` and `asks/<id>/`; the intake rooms under `intake/<workspace>/`; `proposals/<id>.json` naming
a session in `by.session`; `landings.json`'s entries naming a session or a quest; ids in `sessions/archived.json`; and an
empty folder left under `trees/`. It kept `quest_machine`, the machine log, `usage.json`, the accounts, the registry,
the plugins and `salvage/`.

### 0.3 Hazards a removal meets, read from the code

A removal by hand, or a naive build, runs into each of these. None was seen on the window; each is read from the lines
named.

| # | Hazard | Where |
|---|---|---|
| H1 | **An operation's sequence can go back.** It is `MAX(sequence) + 1` over this machine's rows in `quest_log`, and nothing else keeps it. Remove the rows holding the highest one, and the next operation reuses a number the remote already holds; the remote answers it as a retried push, with the old operation's number, and the new move is lost without an error | `Quests.cs:657–659`; `Quests.cs:1507–1511` |
| H2 | **A session's revision can go back.** It is `MAX(revision) + 1` over `sessions`, and the push sends a workspace's own records past its cursor. Remove the newest record, and the next write can take a revision the cursor has passed, so it is never pushed. This already reaches D126's *Delete…* of a conversation that was the newest record | `Sessions.cs:397`; `Sessions.cs:782–802`; `SessionSync.cs:58–63` |
| H3 | **A row and its log go together.** A `quests` row with no log is given a fresh, pending history when the store opens, and pushed; a log with no row lets a publish append to it and write a row that disagrees | `Quests.cs:478–506`; `Quests.cs:614`, `:640` |
| H4 | **A fetch brings a removed quest back.** Every operation numbered past the cursor is kept, and a cursor at zero (a new store, or a workspace name never synced, `quest_cursor` keyed by name) fetches the whole history: a quest whose rows went comes back whole. Only a `deleted` operation replays to nothing | `Quests.cs:1089–1133`; `Quests.cs:353–356`, `:1119–1130`; `QuestLog.cs:216` |
| H5 | **The same words asked again stick.** Ids are derived from the words (`Quests.cs:576–583`). After a removal the same publish makes a fresh open quest here, pending; the remote, which still holds the closed one, refuses it on every pass, and it reads *ahead* for good | `Quests.cs:613–616`; `Quests.cs:1542–1552`; `Quests.cs:1403` |
| H6 | **An ask left with no quest reads as a proposal again**, and the default list shows it | `Asks.cs:652–657`, `:624` |
| H7 | **A taken quest waiting on a closed question is stranded** if the question goes: the driver plans its resume as answered, and the ledger refuses the open, every look | `Planner.cs:682–708`; `SessionLedger.cs:742–777` |
| H8 | **A chain's next step is never published** if its parent goes while held for acceptance | `Quests.cs:903–919` |

H1 and H4 are why removing `quest_cursor` by hand is dangerous on a wired workspace, and H1 why `quest_machine` alone is
not enough to keep: the machine id survives, and its sequence restarts below what the remote holds. §3 answers each.

## 1. What a person may clear

### 1.1 Four scopes

**Clear** (清除) removes from this machine what it kept of finished work: the records, the words in them, and the files
the home holds for them. It is offered in four scopes, each **listed first, then pressed** (§5).

| Scope | What it takes |
|---|---|
| **A closed quest's work** | the quest; every closed question its sessions asked (D79), applied again to what each adds, as `AskWork.Read` reads a quest's work (D132 §1); each one's log; every session record that served them, this machine's and its copies of a teammate's; and what the home kept of each (§2) |
| **An ask's work** | the ask, its intake session, and every quest of its work as above, chain steps included, **or not at all** (D95's rule, kept): one quest that may not go keeps the whole ask |
| **A closed quest's failed sessions** | this machine's sessions of a closed quest whose record ended `failed`, and what the home kept of each. The quest, its log and its other sessions stay |
| **A workspace's finished history** | every quest's and every ask's work in the workspace that may be cleared; the files the home holds of records no store has any more (§2.3); and the intake's room, where the workspace keeps no ask afterwards |

**A quest an ask asked is cleared with its ask, never alone.** An ask left with fewer quests is read from what is left,
and one left with none is a proposal again (H6). So the ask's page offers the clear, and a quest's page names its ask.

**Why a quest's work and not the quest alone.** A question a session asked another repository exists because that
work asked it, and its record carries the asker's session in `publishedBy`. Clearing the asker and keeping the question
would keep a record that names a session nothing holds. It is D132's reading of the work, so a pause, an abandon and a
clear reach the same pieces.

### 1.2 What it refuses

A unit is refused, whole, when any piece of it is one of these. Each refusal is a code in `Refusals`, an entry in both
catalogues and a throw site, as D126 §5.4's are. A sentence names the piece and the act that would free it.

| Code | When | Says |
|---|---|---|
| `HISTORY_UNKNOWN` | no such quest or ask here | *no quest `#q` on this machine* · *no ask `#a` on this machine* |
| `HISTORY_OPEN` | a quest of the work is open or taken | *`#q` is still open: its record is work in progress* · *`#q` is taken: its record is work in progress* |
| `HISTORY_OPEN` (`failed`) | failed sessions of an open or taken quest | *`#q` is still open or taken, and its strikes are counted from these sessions; archive them instead* |
| `HISTORY_ASKED` | a quest an ask asked, cleared on its own | *ask `#a` asked it; clear the ask, which takes every quest it became* |
| `HISTORY_LIVE` | a session of the work is live, here or on a teammate's machine, or its automatic landing still runs | *session `<id>` is still running; stop it first* · *session `<id>` still reads as running on `<machine>`* · *session `<id>`'s work is still being landed* |
| `HISTORY_NEEDS_YOU` | anything of it waits on the person | *session `<id>` waits on you* · *`#q` is done and waits for you to accept it* · *a conflict on `#q` waits on you* · *ask `#a` waits for you to publish or close it* · *a rule proposal from session `<id>` waits on you* |
| `HISTORY_AWAITED` | open work names it | *`#q2` waits on its answer* · *`#q2` was published by its session and is still open* · *`#q2`, its chain's next step, is still open and builds on its work* |
| `HISTORY_TREE_HERE` | a session's tree is still on this machine | *session `<id>`'s tree is still here; clean it up, or discard it, first* |
| `HISTORY_LANDING_STANDS` | a landing's branch for one of its sessions still stands | *the branch `<branch>` a landing made still stands in `<repository>`; clean it up once it has merged* |
| `HISTORY_UNPUSHED` | on a wired workspace, a move or a record of it has not reached the remote | *its last moves have not reached the remote for `<workspace>`; sync, then clear it* |
| `HISTORY_NOT_OURS` | a teammate's failed session, which a failed-sessions clear lists and keeps | *session `<id>` ran on `<machine>`; its record is theirs* |

**Nothing to clear is information, never a refusal** (D48 §6): a workspace with nothing closed, or a quest with no
failed session, says so and offers only *Close*.

**Why each.**
- **Open or taken work** is in progress, and its record is what the driver plans from (§4).
- **A live session** writes into its record. A teammate's record that still reads as running is refused too, since its
  end has not arrived yet.
- **What waits on the person** is never cleared under them: a parked session's question, a quest held for acceptance
  (its chain's next step is published on acceptance, H8), a conflict nobody dismissed, an ask proposed or open, and a
  rule proposal a session made. D126 §5.2's rule, *archive never hides what needs the person*, holds for clearing too.
- **Open work naming it**: a taken quest awaiting the question resumes with its outcome and closing words (D79), and
  would be stranded (H7). A chain's next step builds on its parent's last session here (CHAIN2, `Planner.cs:563–569`),
  so the parent's sessions stay until the step closes.
- **A tree here, or a landing's branch standing**, holds work or the record of where work went. The clear never touches
  a tree or a branch (§1.3), and the clean-up (D88, D102) is the press that removes those whose work is landed. A
  landing's trace, its branch already gone, does not refuse: it goes with its session (§2).
- **Unpushed** moves or records are the team's copy still to come. Clearing them first would be a decline, a done or a
  session the team never hears of.
- **A teammate's record on its own** is theirs (D126 §5.4): no scope takes one alone. Its copy goes here only with the
  quest it served (§3.2).

### 1.3 What is never touched

A tree, and a branch of any kind: the clean-up, *Discard tree* and abandon keep their proofs (D88, D132 §3.3). An open
or taken quest, and anything that waits on the person. The team's copy on a remote: nothing a clear does travels
(§3). The machine log, which keeps that a clear happened (§6.5). The usage counts. The accounts, the harness's own
conversation storage in an account's home included, which is the harness's and never Daoris's to read or remove (D125
TOOL4f). The registry, the plugins, `driver.json`'s settings, and any folder under the home Daoris did not write
(`salvage/`, §2.2). **And nothing clears by itself** (§7).

## 2. What the home holds

Each row was read from the code at `121503bf`, and names the class that writes it. **Clear** says what the clear does
with it; a row the hand purge removed and the clear keeps says why.

### 2.1 In `knowledge.db`

One SQLite store under the home (`HostComposition.cs:46`), every table opened on one connection (`ServiceFactory.cs:252`).

| Table | Written by | Holds | Clear |
|---|---|---|---|
| `quests` | `QuestStore` (`Quests.cs:374`) | a quest's row, a cache of replaying its log (`Quests.cs:237`) | removed, **with its log, in one transaction** (H3) |
| `quest_log` | `QuestStore`; replayed by `QuestLog` (`Quests.cs:337`) | every operation: a publish's title, body, links and quoted requirements; each note, decline reason and answer (`Quests.cs:1787–1840`) | removed for each cleared quest. A D95 tombstone keeps these words (`Quests.cs:1015`); a clear does not |
| `quest_forgotten` (new) | `QuestStore` | a forgotten quest's id and when | written for each quest a remote numbered (§3.2) |
| `quest_machine` | `QuestStore` (`Quests.cs:349`, `:457`) | this store's machine id; gains the sequence's high-water mark (§3.3) | **kept**: a new id is a new machine (the sync design's §2), and H1 |
| `quest_cursor` | `QuestStore` (`Quests.cs:353`) | per workspace, the last remote number fetched | **kept**: without it the next fetch brings the whole history back (H4) |
| `quest_passes` | `QuestStore.RecordPassAsync` (`Quests.cs:1373`) | per workspace, how its last pass ended, and the quests still behind | **kept**: a standing, not history; the next pass overwrites it |
| `sessions` | `SessionStore` (`Sessions.cs:261`), judged by `SessionLedger` | a session's record, with the person's words to it (`said`, `answer`) | removed for each cleared session, this machine's and a teammate's copy |
| `session_cursor` | `SessionStore` (`Sessions.cs:366`) | per workspace, the revision pushed and the remote revision fetched | **kept**, as `quest_cursor` |
| `session_revision` (new) | `SessionStore` | the highest revision issued (§3.3) | written as each revision is issued |
| `asks` | `AskStore` (`Asks.cs:154`), judged by `AskDesk` | the person's sentence, later words, go-aheads, the close note | removed for each cleared ask |
| `entries`, `registrations` and their kin, `api_keys` | `SqliteKnowledgeStore`, `RegistrationStore`, `ApiKeyStore` | knowledge, the registry, keys | **kept**: not history |

### 2.2 Under the home

**Per session**, for each cleared session. Today `SessionDeletion.RemoveFiles` (`SessionDeletion.cs:176`) removes the
first five; the clear and the delete take every row below through one helper.

| What | Written by | Clear |
|---|---|---|
| `sessions/<id>.events.jsonl`, the conversation | `SessionEvents` (`SessionEvents.cs:700`) | removed through `SessionEvents.Forget`, which also drops its numbering |
| `sessions/<id>.log`, the transcript | the four starts (`Driver.cs:1008`, `Driver.Intake.cs:177`, `ChatRunner.cs:397`, `Driver.Continue.cs:227`) | removed |
| `sessions/<id>/files/` | `ChatFiles` (`ChatFiles.cs:74`) | removed, with the whole `sessions/<id>/` |
| `sessions/<id>.harness.json` | `HarnessConversations` (`HarnessConversations.cs:26`) | removed. The harness's own conversation in the account's home is kept (§1.3) |
| `sessions/<id>.pid` | `SessionProcesses` (`SessionProcesses.cs:374`) | a leftover removed; a live process refuses (`HISTORY_LIVE`) |
| `sessions/<id>.cannot.json` | `GoOnMarks` (`GoOnMarks.cs:26`) | removed. **`SessionDeletion` leaves it today** |
| `sessions/<id>.new-session.json` | `NewSessionChoices` (`GoOnNew.cs:23`) | removed. **Left today** |
| its entry in `sessions/held-words.json` | `SessionWords` (`SessionWords.cs:79`, modules) | removed. **Left today** |
| its entry in `sessions/auto-landings.json` | `AutoLandings` (`AutoLanding.cs:213`) | a closed entry removed; one still landing refuses (`HISTORY_LIVE`). **Left today** |
| its mark in `sessions/archived.json` | `SessionArchive` (`SessionArchive.cs:53`) | removed (`Unarchive`) |
| `spawn/<id>.mcp.json`, `spawn/<id>.settings.json` | `SpawnServers` (`SpawnServers.cs:20`), `Permissions` (`Permissions.cs:467`) | removed at the session's end today; one a crash left is left over (§2.3) |
| `sessions/requests/<id>.json` | `SessionRequests` (`SessionRequests.cs:115`) | untouched: taken once, and dropped by any loop after a minute |

**Per quest and ask, and what names them.**

| What | Written by | Clear |
|---|---|---|
| `quests/<id>/`, a quest's kept files | the service's `QuestFiles` (`QuestFiles.cs:27`) | removed by the service after the records (`QuestFiles.Forget`), as D95's delete does |
| `asks/<id>/`, an ask's kept files | `AskDesk`'s keeper (`Asks.cs:577`, `:594`) | removed by the service, as above |
| `intake/<workspace>/`, the intake's room | `IntakeRoom` (`Intake.cs:97`), rendered again at every open (`Intake.cs:139`) | one room per workspace, shared by every ask. **Removed only by a workspace's clear that leaves the workspace no ask**; otherwise kept and named. It holds the rendered instructions and whatever intake sessions left there; the next open renders it again |
| `proposals/<id>.json`, a rule proposal (PERM2) | the service's `RuleProposalBox` (`RuleProposals.cs:38`); settled by the driver's `RuleProposals` and the CLI's `ruleproposals.ts` | one still pending that names a session or ask of the unit refuses (`HISTORY_NEEDS_YOU`); a settled one naming a cleared session or ask is removed by the service with the unit |
| `help/proposals/<id>.json`, Ask Daoris's | `HelpProposalBox` (`HelpProposalBox.cs:55`) | **kept**: its `by.session` is an Ask Daoris conversation, which a clear never takes |
| `landings.json` | `LandedBranches` (`LandedBranches.cs:119`) | a trace (`goneAt` set) whose session is cleared is removed; a standing entry naming a session of the unit, as its own or among its `advances`, refuses (`HISTORY_LANDING_STANDS`) |
| `abandoned.json` | `AbandonRecord` (`AbandonRecord.cs:96`) | an entry whose ask or quest is cleared is removed (`Write` with what is known, `:126`) |
| `driver.json`: `forgiven`, `released`, `pausedQuests`, `pausedAsks` | `DriverConfig` (`DriverConfig.cs:41`, `:87`, `:136`); twin `driverconfig.ts` | entries for a cleared quest or ask removed (`WithoutQuest`, `:113`), as abandon's step 7 does |
| `trees/<workspace>/<repository>/s-…` | `SessionTrees` (`SessionTrees.cs:149–152`) | a tree with anything in it refuses (`HISTORY_TREE_HERE`). An empty, unregistered folder a removal left (`SessionTrees.Leftovers.cs:3–14`) is removed, and so is an empty parent it leaves, which nothing removes today |

**Kept, and why.**

| What | Written by | Why it stays |
|---|---|---|
| `logs/`, the machine log | `MachineLog` (`MachineLog.cs:29`) | evidence about the machine, with no one's words, kept 30 days by its own rule (§7); it gains `history.cleared` |
| `usage.json` | `SessionUsage` (`Usage.cs:42`) | each entry names a session, and the accounts' totals are made of them (D126 §5.1); bounded to the newest 500 |
| `accounts.json`, `harnesses.json`, `keys.json`, `harnesses/<agent>/<profile>/`, `windows.json`, `cooling.json`, `reads.json` | `AccountNames`, `Harnesses`, `AccountWindows`, `AccountCooling`, `AccountReads` | the accounts and their readings; `windows.json` and `cooling.json` may name the session that read or cooled an account, which is a fact about the account |
| the registry, `remotes.json` | `RegistrationStore`; `RemoteConfig` (`RemoteConfig.cs:35`) | the workspace's wiring, not its history |
| `plugins/`, `plugins.json` | `Plugins` (`Plugins.cs:111`) | the plugins and their data |
| `salvage/` | **nothing in Daoris**: no file in the tree names it | a folder the person or a hand repair made; a clear never touches a folder Daoris did not write |

### 2.3 Left over

A file under `sessions/`, `spawn/`, `quests/` or `asks/` whose id no record, quest or ask holds, which no process here
runs, and which nothing touched for an hour, is **left over**: from a crash, a removal that failed, or a hand purge. It
names no workspace, so every workspace's clear lists the home's left-over files with its own (*and 3 MB left over from
records already gone*) and takes them. A quest's or a session's clear takes only its own.

### 2.4 The reading

`HISTORY_PLAN` answers, per workspace, what the home keeps of its finished work and what a clear would free, walking
the files of §2.2. It never names a path or a title.

- **Closed quests and asks**: how many, and how many a clear would take now.
- **Their sessions**: this machine's, and copies of a teammate's.
- **What they take on the disk**: conversations, transcripts, files, kept files and the intake's room, in bytes.
- **What is kept, by reason**: a count per refusal code, each with its door (§5).
- **Conversations that served no quest**: how many and how large, since only *Delete…* takes them (§11).
- **For the whole home**: what is left over, and the machine log's size.

*Kept on this machine: 35 closed quests and 2 asks, 76 sessions, 24 MB. A clear would take 33 quests and 23 MB; 2 are
kept, since a landing's branch still stands.* A workspace no page shows any more (renamed, or no longer registered) is
read and cleared at the terminal, which lists every workspace a record names.

## 3. Another machine, and the remote

### 3.1 A record that never left the machine simply goes

On a workspace with no remote, or for a quest whose receiver is not joined in its workspace and whose operations no
remote ever numbered, the clear removes the rows outright: the quest's row and its log together (H3), its sessions'
rows, its ask's row. This is D95's *a quest that never left the machine simply goes*, applied to a closed quest. Asks
never leave the machine (D65), so an ask always goes this way.

### 3.2 A record the remote holds is forgotten here

On a wired workspace, a closed quest whose operations a remote numbered is **forgotten**: this machine removes its
row, its log, its sessions' rows (its own, and its copies of a teammate's) and the files, and keeps one small fact,
that it forgot that quest. The team's copy is untouched.

- **It survives the cursor.** `quest_forgotten` in `knowledge.db` holds each forgotten quest's id and when. The fetch
  skips every operation for a forgotten quest, and the session fetch skips a teammate's record of one, while each
  cursor still moves past them. So a later conflict or dismissal on it (the only moves a closed quest still takes,
  `QuestLog.cs:209–221`) makes no half quest here, and a cursor back at zero (H4) brings nothing back.
- **It never re-pushes.** Only pending operations are pushed (`Quests.cs:1312–1336`), and a clear is refused while
  any is pending (`HISTORY_UNPUSHED`), so nothing of the quest is left to push. A session record is pushed by revision,
  and a removed row is never read again (`Sessions.cs:782–802`). This machine's own records never come back from a
  fetch, which leaves out the caller's origin (`Sessions.cs:809–836`).
- **It never travels.** No operation is written: no `deleted`, no `cleared`. The mark is not on the wire
  (`QuestWire`), not in a feed, and not read by a remote, which has no door for it (§6.3).
- **A new machine's first fetch brings the quest**, its operations and the team's records of it, because a new store
  is a new machine (the sync design's §2) and the team's history is its to hold. The same is true of this machine if
  its store is ever rebuilt. The clear's ask says so: *the remote for `<workspace>` keeps the team's copy*.
- **The same words asked again** (H5) make the same id. A publish whose id is forgotten here is refused in the
  exchange's words for a closed quest, *`#q` was cleared from this machine; the remote for `<workspace>` holds it
  closed. A new ask is a new title.*, as a done quest here answers today (`QuestExchange.cs:999–1000`). Asked on a
  workspace with no remote, where the quest simply went, the same words make the quest again, as after a D95 delete.

**Why forgetting, and not refusing a wired workspace.** A team's workspace grows fastest, by every teammate's quests,
and the remote already keeps the team's history: refusing would leave exactly the largest homes uncleanable, to
protect a copy that nothing here can harm. **Why a mark, and not an absence.** D95's reasoning stands: a fetch by
cursor cannot carry an absence. A removal with nothing kept would come back the first time a cursor is at zero (a
workspace renamed, or wired again under a new name), or half come back as a conflict's orphan operation. The mark is
the absence made a record, kept where it belongs: on the machine that chose it. **Why not a tombstone**: a `deleted`
operation removes the quest from every machine and the remote (D95). Clearing one's own history must not take the
team's.

### 3.3 The store's numbers never go back

H1 and H2 apply to any removal, D95's and D126's included. The store keeps **a high-water mark for each**:
`quest_machine` gains the highest sequence this machine has issued, and `session_cursor`'s neighbour, a one-row
`session_revision`, the highest revision issued. The next sequence is one past the larger of the log's maximum and the
mark; the next revision likewise. Each mark moves inside the write that issues its number, so SQLite's write lock keeps
it exact (`Sessions.cs:392–397`'s reason). **This is HIST1a, and it ships before anything removes a row**: it also
closes H2 for D126's *Delete…* as it stands.

### 3.4 What a teammate sees

Nothing. A clear writes no operation and pushes nothing, and a teammate's records, quests and remote are as they were.
A teammate's later moves on a forgotten quest reach the remote and their own machines, and are skipped here.

### 3.5 The shared deployment

A remote has no clear door, as it has no delete door (D95) and no abandon (D132 §5.4): a person clears on their own
machine. A remote's history is the team's, and trimming it would be a decision for the team, outside this design (§11).

## 4. Strikes and carry-ons

**A session of an open or taken quest is never cleared**, failed or not, alone or in bulk. The rule is D126 §5.4's
reason held to its letter: a quest's strikes are derived from its records (D58), RETRY1's mark counts from them
(`forgiven`), and a carry-on reads the last session here and its tree (D80). Clearing a failed session of an open quest
would lower the derived count, so a parked quest would start again without the person's *Try again*, and a mark set at
six failures would sit above a count of four. **Archive** is how such a session leaves the list (D126 §5.2).

**Once a quest is closed, nothing is counted from its sessions.** A closed quest never moves (`QuestTransitions.Allows`,
`QuestLog.cs:161–166`) and is never planned (D46 §3). So its failed sessions may be cleared, alone (§1.1's third scope)
or with it. `driver.json`'s entries for the quest (`forgiven`, `released`, `pausedQuests`, and `pausedAsks` for its
ask) go with it, as D132 §3.4 step 7 tidies them for a quest an abandon closed.

**Why not a tally kept beside the records**, so a cleared failure still counts: D58 rejected a second register, and the
property that made deriving worth doing is that the count cannot disagree with the records. **Why not let the clear
lower the count on purpose**, as a reset: that is *Try again* without its press and without its mark, which D58 and
RETRY1b keep honest.

## 5. Listed first, then pressed

As *Archive what ended…* (D126 §5.3), the clean-up (D88) and abandon (D132 §3.1):

1. **The first press lists** what the clear would take and what it would keep, each kept unit with its refusal's
   sentence and, where one exists, the door that frees it: the session's page for *Stop…*, the quest's page for a
   conflict or an acceptance, the workspace's Branches tab for *Clean up…*, *Sync now* for unpushed moves. It reads
   what the clear would free (§2.4). The moves are the clear's danger press and *Never mind*.
2. **The second press sends exactly the units the first listed**, held from when the list opened, and judges each
   again as it goes. One that changed since is kept and counted: *Cleared 33 of 35; 2 changed since the list and were
   kept.*

**The order of the second press**, per unit:
1. **Judge again**: the service's half (the records and the rule proposals), then the machine's (trees, landings,
   automatic landings, live processes).
2. **Clear the records** in the service, the unit in one transaction (§6.3): rows, logs, the mark where forgotten. The
   service then removes the unit's kept files (`QuestFiles.Forget`; the ask's files) and its settled rule proposals,
   as D95's delete removes kept files.
3. **Remove what the home kept** of each cleared session (§2.2), after the service's yes.
4. **Tidy what names it**: a landing's trace, an automatic landing's closed entry, held words, an abandon's entry,
   `driver.json`'s entries (§4), an archive mark, an empty folder under `trees/`; for a workspace, its left-over files
   (§2.3) and, where it keeps no ask, the intake's room.
5. **Write the log line** (§6.5), once, for the whole press.

**A failed step keeps what is left and says so.** The records' half is all or nothing per unit, so a unit is never
half cleared in the service. A file the home could not remove is left over (§2.3), and the next clear of the workspace
takes it.

## 6. The doors

### 6.1 The screen

Each act is offered where it applies and absent where it does not, never disabled (D119 §3.2). It asks once, under the
page's header, since nothing brings it back (the platform language's §4).

| Where | Act | Offered when |
|---|---|---|
| **A quest's page**, its header's ⋯ | *Clear from this machine…* | the quest is closed and no ask asked it; its plan says it may go |
| **A quest's page**, its header's ⋯ | *Clear failed sessions…* | the quest is closed and this machine has a failed session of it that may go |
| **An ask's page**, its header's ⋯ | *Clear from this machine…* | every quest of its work is closed; its plan says it may go |
| **A workspace's page**, Details, a section *Kept on this machine* | the reading (§2.4), then *Clear history…* | always the reading; the press while anything may go |

- **The asks**, by scope: *Clears `#q`, its 3 sessions and what this machine kept of them: their words, transcripts and
  files. Nothing brings it back.* On a wired workspace the last sentence is *The remote for `<workspace>` keeps the
  team's copy; this machine will not fetch it again.* A workspace's first press lists the counts by kind and every kept
  unit with its reason and door, as abandon's list does.
- **A quest an ask asked** offers no clear on its page; its header names the ask as today, and the ask's page clears it.
- **After a clear** of a quest or an ask, the page closes and the list it came from is shown, with a toast: *Cleared
  `#q` from this machine.*
- **Sessions offers none of these.** A session of a quest is that quest's record, cleared with it from its page; the
  list's own tidy is *Archive* (D126 §5.2). A conversation that served no quest keeps *Delete…* (D126 §5.4).
- **A browser** has no driver and no home, so none of this is offered there (D47 §4).
- **Below 560 px** the asks take their own line under the header, as D126 §3.2 says.

### 6.2 The terminal (D50)

`daoris-driver`, beside `quest delete`, `ask --delete` and `sessions delete`:

```
history [--workspace <name>] [--json]
    what this machine keeps of finished work, per workspace: quests, asks, sessions and bytes,
    what a clear would take, and what it keeps and why.
history clear --workspace <name> [--yes]
    list what clearing the workspace's finished history would take and keep; with --yes, clear it.
quest clear <id> [--failed] [--yes]  ·  ask --clear <id> [--yes]
    the same for one closed quest's work (or, with --failed, its failed sessions), or one ask's work.
```

- **Without `--yes` a clear is the first press**: it prints the list and changes nothing. With `--yes` it judges every
  unit again and clears what may go, as `trees clean --yes` does.
- **Exit codes** are the family's: 0 done or listed, 1 refused (a unit named was kept), 2 could not.
- **`--json`** prints `HISTORY_PLAN`'s answer field for field, as `sessions --json` prints `SESSION_GROUPS`' (a twin
  with one test of its fields).
- **Why `daoris-driver`'s**: the verbs read the service's records and remove the home's files. `daoris driver` edits
  `driver.json` and talks to nothing (D50).

### 6.3 The routes

**The service**, on a local host only (a shared deployment registers neither, as D95's delete; `SharedHostTests` holds
it):

| Route | Answers |
|---|---|
| `GET /api/history?workspace=` · `?quest=` · `?ask=` · `?quest=&failed=true` | per unit: what it holds (counts), whether it may be cleared, forgotten or removed outright, and the refusal's word with what it names; deletes nothing |
| `POST /api/history/clear` `{ units }` | per unit: cleared (removed or forgotten) or kept with its word; judges again inside each unit's transaction |

One judgement class, `HistoryDesk` in `Daoris.Service.Core`, judges the records' half for both routes (D36's one place):
it reads the quests, the log's pending operations, the sessions, the asks, the rule proposals and the workspace's
wiring. It clears through `QuestStore.ForgetAsync` (row and log together, H3, and the mark where numbered),
`SessionStore.ForgetAsync` and `AskStore.DeleteAsync`. A refusal travels as a word (`open`, `asked`, `live`,
`needs-you`, `awaited`, `unpushed`, `not-ours`, `unknown`) with what it names, as SESSUX1f's do; the driver reads the
word, never the sentence. The trees, the landings and the processes are the driver's to judge (`tree-here`,
`landing-stands`, `live`).

**The driver**, every route `DAORIS.DRIVER`'s in a new `DriverModule.History.cs`, with MOD5's three things (the
handler, the Desktop README's row, a call from a new `bridge/history.ts`):

| Route | Payload | Answers |
|---|---|---|
| `HISTORY_PLAN` | `{ workspace }` · `{ quest, failed? }` · `{ ask }` | the units, what each would take and keep with its code, and the reading (§2.4) |
| `HISTORY_CLEAR` | the same scope, and `units` the first press listed | what went (counts and bytes), what stayed with its code, and how many changed since the list |

`HistoryClearing`, in the driver library, plans and clears for both doors, as `WorkAbandoning` does for abandon. It
asks the service's judgement, judges the machine's half (§1.2's trees, landings, automatic landings and live
processes), calls the service's clear, then removes the home's files and tidies what names them (§5). The home's files
of one session are removed by one helper that `SessionDeletion` (D126 §5.4) also calls, so a delete and a clear take the
same files, and the four a delete leaves today (§2.2) go with both.

### 6.4 Ask Daoris (D110)

**A door, proposal only.** A new `clear` kind (`clear_propose`), its doors `quest`, `ask` and `workspace`, naming one.
Its card is the first press: what would go, what stays and why, and the ask's sentence. **Apply is the second press**
and sends exactly what the card listed (D89). Ask Daoris never clears, and no MCP tool removes a record (D95, D37):
`clear_propose` writes a proposal, as `delete_propose` does. *Clear what finished in that workspace* is a thing a
person asks, and Apply is still their press, so it is a door rather than an exemption, as D126 §7.3 made the
session's *Delete…* one. `HelpCoverageTests` reads the three presses on the screen as answered by the kind's doors. The
room's doors table gains *clear finished history*, and *The machine now* says what each workspace keeps (§2.4).

### 6.5 The machine log

One event joins D94's catalogue, names and counts only (D94 §5): **`history.cleared`**: `scope` (`quest`, `ask`,
`failed`, `workspace`), `quests`, `asks`, `sessions`, `forgotten` (how many of the quests a remote keeps), `bytes`,
`kept`, `door` (`screen`, `terminal`). Never an id, a title or a word: the log keeps that it happened, as
`session.deleted` does, and a clear that took nothing writes nothing.

## 7. Nothing clears by itself

**Kept, as D126 §5.5 and §11 say it: no rule by age, no size cap, nothing on a tick.** The reasons hold harder here:
a clear removes words that nothing brings back, on a machine where the person may be the only holder. A record gone
without a press is one the person never saw go (D126 §11), and a quest's record can matter long after it closed: a
landing's review, a teammate's question, an evidence note (D144). **What changes is that the person can see it**: the
workspace's page reads what the home keeps (§2.4), and *The machine now* says it to Ask Daoris, so the size of the
history is a fact on the screen, not a discovery on the disk.

**The machine log is not history**, and keeps its own rule: 30 days, then the next writer deletes its files (the
machine log design's §2). It is evidence about the machine, holds no one's words (§5 there), and is bounded by
construction. **An age rule for clearing** was weighed and not chosen (§11): it would be the first thing in Daoris that
removes records on its own, and the measured need, one install's 24 MB, is met by one listed press.

## 8. Both languages

Every name follows D116: its kind decides its form and budget, and a concept takes the glossary's term. A count is
English characters and Chinese units (D116 §4). A button that names its object says it in the glossary's word, as
SESSUX1d settled for 会话.

| Key | Kind | English | 中文 | Measured / budget |
|---|---|---|---|---|
| `quests.detail.clear` | menu | Clear from this machine… | 从本机清除… | 24, 6 / 24, 10 |
| `quests.detail.clearMeanIt` | button | Clear quest | 确认清除委托 | 11, 6 / 20, 8 |
| `quests.detail.clearFailed` | menu | Clear failed sessions… | 清除失败的会话… | 22, 8 / 24, 10 |
| `quests.detail.clearFailedMeanIt` | button | Clear {{count}} | 清除 {{count}} 个 | 8, 5 / 20, 8 |
| `asks.record.clear` | menu | Clear from this machine… | 从本机清除… | 24, 6 / 24, 10 |
| `asks.record.clearMeanIt` | button | Clear ask | 确认清除需求 | 9, 6 / 20, 8 |
| `projects.workspace.kept` | section | Kept on this machine | 本机保留的记录 | 20, 7 / 32, 12 |
| `projects.workspace.clear` | button | Clear history… | 清除记录… | 14, 5 / 20, 8 |
| `projects.workspace.clearMeanIt` | button | Clear {{count}} | 清除 {{count}} 项 | 8, 5 / 20, 8 |
| `common.cancel` *(exists)* | button | Never mind | 取消 | — |

**The glossary gains one term**, `clear`: en *clear*, zh **清除**. *Remove from this machine what it kept of finished
work: its records, their words and the files they left. A team's remote keeps its copy.*
- **Why not *delete***. *Delete* (删除) removes a record for good, and a quest's delete travels to every machine as a
  tombstone (D95). A clear never travels, and on a wired workspace the team keeps the record. One name for both would
  promise either too much or too little.
- **Why not *clean up***. The glossary's *clean up* (清理) is D88's removal of landed trees and branches, and the clear
  touches neither (§1.3). D132 §8.2 kept the same distance for abandon.
- **Why not *archive***. Archive keeps everything and hides it (归档).
- **Avoid**: zh 清理 (clean up's), 删除 (delete's), 清空 (empties a container, which says nothing of what stays).
- **The existing *Clear* labels** (a setting cleared back to its default: `settings.landing.clear`,
  `agents.account.clearIn`) already say 清除, and the term's `match` is the build's to scope so that names-check reads
  them consistently.

**The sentences** are the build's to write in both languages: the asks (§6.1), the refusals (§1.2), the reading
(§2.4), the toasts and the terminal's lines. A toast keeps to two lines (110 and 55). A quest's title and a person's
words are content and are not translated (`translation-parity`).

## 9. The build

These rows replace the provisional HIST1a–d in `TASKS.md`. Their lanes are `daoris.lanes.json`'s ids. D153 decides all
of it, so no row takes a decision number unless building it finds something D153 did not decide.

**Every row's proof**, besides what each names: a failing test first; the fast half of each .NET suite it touches;
`npm run verify`, with parity and `names:check` clean for every new key; for a row that changes words a test reads, the
old words searched in what a worktree cannot run (the desktop suites' `Process` half, `tools/*rehearsal*.mjs`, the
web's `e2e/` specs); for a row that changes the window, the parent's look after the merge in both themes and both
languages at 1280, 888 and 680 px.

**Order.** HIST1a, then HIST1b, in the service lane. HIST1c after HIST1b, in the driver and modules lanes. HIST1d (the
driver lane) and HIST1e (the web shell lane) after HIST1c, side by side. HIST1f after HIST1e and SESSUX1h. HIST1g after
HIST1d. HIST1h last. **HIST1a alone closes H1 and H2**, which reach D95's and D126's deletes today, so it may go first
whatever else waits.

- [ ] **HIST1a — store numbers never go back** (service; first). Keep high-water marks for operation sequences and
  session revisions, so no removal reissues a number the remote or push cursor passed. This closes H1–H2, which D95's
  and D126's deletes already reach. Contract: history-clearing §0.3, §3.3. Proof: store tests removing the newest
  rows; sync tests over the real wire.
- [ ] **HIST1b — service clears closed records** (service; after a). `HistoryDesk` judges and clears quest, ask and
  failed-session units behind local-only `GET /api/history` and `POST /api/history/clear`; numbered quests are
  forgotten (`quest_forgotten`) and fetches skip them. Contract: §1, §2.1, §3, §6.3. Proof: desk tests; sync tests
  (skipped, never refetched or pushed, fetched by a second store); no shared door.
- [ ] **HIST1c — driver clears the home** (driver, modules, web-shell catalogues; after b). `HistoryClearing` judges
  trees, landings and processes, calls the service, removes §2.2's files through a helper `SessionDeletion` shares,
  tidies what names them, reads sizes, adds `HISTORY_PLAN`, `HISTORY_CLEAR`, `history.cleared`. Contract: §2, §4–§5,
  §6.3, §6.5. Proof: scratch-home clearing tests; deletion's missed files; route and refusal tests.
- [ ] **HIST1d — terminal history verbs** (driver; after c). `daoris-driver history`, `history clear --workspace`,
  `quest clear [--failed]` and `ask --clear`, each listing until `--yes`, so a machine with no screen clears as the
  window does (D50). Contract: §6.2. Proof: in-process command tests with exits, usage golden, and a `--json` field twin
  against `HISTORY_PLAN`.
- [ ] **HIST1e — clear on the screen** (web-shell; after c). The quest page's two clears, the ask page's, and the
  workspace page's *Kept on this machine* reading with *Clear history…*, each listed then pressed; names and the
  glossary's *clear*. Contract: §6.1, §8. Proof: stories, mocked-bridge vitest (the second press sends the list),
  parity, `names:check --strict`, bilingual look.
- [ ] **HIST1f — Ask Daoris proposes a clear** (driver, service, web-shell; after e and SESSUX1h). A `clear` kind
  whose card is the first press and whose Apply sends what it listed; the room's doors and *The machine now*. Contract:
  §6.4. Proof: clear-proposal, kinds and coverage tests, the room's golden files, `ProposalCard.test.tsx`.
- [ ] **HIST1g — family rehearsal clears** (tools; after d; the parent runs it). On the two-machine circle, a closed
  quest cleared on one machine stays on the other and the remote, does not return after a pass, and its words asked
  again are refused. Contract: §3, §12. Proof: the phase, run at the merge.
- [ ] **HIST1h — installed clear** (parent; after a republish carrying a–f). Clear a workspace's finished history on
  the install. Contract: §12. Proof: a dated ledger of counts and bytes before and after, `history` matching the page,
  both themes and languages at 1280, 888 and 680 px.

## 10. What does not change

- **D95**: a delete stays the act for a mistake, and the only removal that travels. A quest's record with work on it
  is still never deleted; it may be cleared from a machine once its work is closed.
- **D46 §3–§4**: four quest states, a session concluded from its exit and its quest. A clear adds no state.
- **D58**: the strikes are derived from the records, and nothing a clear does changes a count anything reads (§4).
- **D68's one lock**, the take, and the sync's operations. A clear writes none.
- **D47 §4**: processes never leave their machine, and the pages never print a path on it.
- **D51 rule 7 as D88 and D132 amended it**: no tree or branch is removed by anything here.
- **D126 §5.2 and §5.4**: archive keeps everything, and never hides what needs the person; *Delete…* of a conversation
  stays as it is.
- **D132**: abandon archives and never deletes, and its §3.5 holds: kept files stay with their records, so they go when
  a clear takes the record. A clear may follow an abandon once its quests are closed.
- **D41's palette** and **D116's names**.

## 11. Not chosen

- **Refusing a clear on a wired workspace.** The largest histories would be the ones that could never be cleared, to
  protect a copy nothing here can harm (§3.2).
- **A clear carried by absence**, removing the rows and keeping nothing. D95's reason: a fetch by cursor brings an
  absence back the first time a cursor is at zero, or half back as a conflict's orphan operation (H4).
- **A clear that travels**, as a `deleted` tombstone or a new `cleared` operation. One person's tidy would remove the
  team's record from every machine and the remote, which D95 allows only for a mistake nobody started on.
- **Trimming a remote's history.** The team's record is the team's decision; a shared deployment has no clear door.
- **Clearing a session of an open quest**, and a tally that keeps its strike (§4). D58 rejected the tally; the clear
  would be a *Try again* without its press.
- **Clearing a quest an ask asked on its own.** The ask would read from what is left, and as a proposal once none is
  (H6).
- **Refusing a quest a teammate's session served.** Its record is theirs, and stays theirs on their machine and the
  remote; this machine's copy carries no transcript, and refusing would keep a team workspace's history here for good.
  A teammate's record is still never cleared on its own.
- **The clear removing landed trees and branches itself**, so one press does both. Two removals with two proofs stay
  two presses (D88, D132 §6.3); the list names the clean-up as the door.
- **Clearing from Sessions.** A session of a quest is that quest's record (D126 §5.4); one owner, the quest's page and
  the workspace's, holds the act (D56).
- **Taking conversations that served no quest in the workspace's clear.** A conversation is the person's own words,
  and nothing but the person says it is finished: *Delete…* takes them one at a time (D126 §5.4). The reading counts
  them, so the person sees what they hold.
- **Clearing by age, or by size, on a tick** (§7).
- **Choosing units one by one in the list.** A second selection beside the page's (D126 §11's reason); a quest's page
  clears one, the workspace's clears all that may go.
- **An undo.** Nothing keeps a copy: a backup is the person's, as the owner's under `local/backups/` was.
- **Naming it *Delete*, *Clean up* or *Archive*** (§8).

## 12. What only the window and a real run can prove

- **The widths**: the quest's and the ask's ⋯ with *Clear from this machine…* at 24 characters, and the workspace's
  *Kept on this machine* with its reading at 680 px, in both languages.
- **A clear on the owner's install**, of a workspace's finished history: the counts and bytes before and after, and
  `history` reading the same as the page.
- **A forgotten quest over a real remote**: a teammate's later dismissal skipped here, a workspace renamed and fetched
  from zero without the quest coming back, and a second machine's first fetch bringing it.
- **How long `HISTORY_PLAN` takes** on a workspace of 29 repositories with a few hundred closed quests, since the
  reading walks the home's files.
- **Whether 清除 reads as the owner means it** beside 清理 and 删除.

## 13. What this document's gate does not cover

This change is documents only, and nothing is built.

- **Read from the code at `121503bf`**: the service's `Quests.cs`, `QuestLog.cs`, `QuestSync.cs`, `QuestWire.cs`,
  `QuestExchange.cs`, `QuestFiles.cs`, `Sessions.cs`, `SessionLedger.cs`, `SessionSync.cs`, `SessionFeed.cs`, `Asks.cs`,
  `RuleProposals.cs` and the HTTP host's `Program.cs`; the driver's files §2 names, `SessionDeletion.cs`,
  `ServiceClient.cs`'s strike readers and `Planner.cs`. The lines cited in §0.3 and §2 were found by two read-only
  research passes over that commit, and the hazards' own lines were read again by hand. None of the hazards was
  reproduced.
- **The owner's purge** is as `TASKS.md` and the dispatch record it; its backup and the install were not read, so
  whether its workspaces are wired, and so whether H1 or H4 reached it, is not known.
- **Not measured**: every item of §12.
- **Found while reading, and filed in a row rather than fixed here**:
  - H2 reaches D126's *Delete…* of a conversation that was the newest record (HIST1a).
  - `SessionDeletion` leaves a deleted conversation's `.cannot.json`, `.new-session.json`, held words and automatic
    landing entry (HIST1c's shared helper).
  - `SessionArchive.Unarchive` drops marks for gone records only when handed what is known, and no caller hands it, so
    stale marks wait for the next archive. Harmless; a clear removes its own.
- **`verify` checks** this document's links, the decision's shape, the router's row, the budgets and the duplicates, and
  none of these words.
