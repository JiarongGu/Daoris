# Evidence: from a start to a full Sessions and Quests view (STARTPERF1's measurement, 2026-11-11)

**Standing:** evidence. It reads the installed window's machine log for 2026-11-11 and its data folder's layout, both read only, and
the service's and the desktop's stores from a copy in a gitignored scratch folder. The fixes it names are on STARTPERF1's branches;
the [database design](2026-11-11-start-load-database-design.md) beside it is proposed. Times are from the process's start unless a
line says otherwise.

## 1. What the data folder holds

| Record | Where | Count and size | A whole read |
|---|---|---|---|
| Session records | the service's store, `sessions` | 1,261 rows, 1.5 MB | 7 ms (SQLite, every column) |
| Quests | the service's store, `quests` (+ `quest_log` 2,868) | 950 rows, 4.0 MB: 917 Done, 32 Declined, 1 Taken | 12 ms |
| Asks, journal, work records | the service's store | 575, 5,596 and 450 rows | under 12 ms each |
| Knowledge index | the service's store, `index_entries_6` | 24,408 entries, 33.6 MB, 47 readings | 73 ms |
| Session events | `data/sessions/*.events.jsonl` | 1,230 files, 517.5 MB; median 314 KB, a tenth over 822 KB, largest 6.47 MB (24,782 lines) | 47 ms for the largest in Node; see §5 |
| Session folders and consoles | `data/sessions/` | 1,077 folders (1.5 MB of `.jsonl`), 1,258 `.log` (32.7 MB) | not read at a start |
| Workflow runs and units | `data/workflows/runs`, `units` | 941 and 922 JSON files, 6.1 MB | once per file stamp (`NodeRecordFiles.Settled`) |
| Harness transcripts | `data/harnesses/` | 1,354 `.jsonl`, 2.1 GB | not read at a start |
| Session trees | the install's `work/trees/` | 14 | a walk: under a second idle, 2-48 s at a start |
| The desktop's store | `desktop.db` | 3.2 MB (bindings 1,110, integration steps 1,302) | under 2 ms a table |

## 2. Each start, stage by stage

`start.stages`, ms from the process's start:

| Start | main | home | driver | shown | host | browser | pipe | splash | page |
|---|---|---|---|---|---|---|---|---|---|
| 00:32Z | 5,026 | 5,077 | 9,138 | 11,401 | 13,485 | 13,589 | 13,935 | 23,606 (bound) | 64,554 |
| 02:38Z | 1,345 | 1,389 | 3,762 | 4,456 | 10,211 | 10,441 | 10,591 | 20,459 (bound) | 75,899 |
| 02:42Z | 477 | 513 | 2,048 | 2,562 | 5,176 | 5,279 | 5,633 | 15,297 (bound) | none in 60 s |
| 05:28Z | 949 | 966 | 1,908 | 2,330 | 8,571 | 8,678 | 9,313 | 18,714 (bound) | none in 60 s |
| 07:00Z | 1,231 | 1,253 | 2,570 | 3,657 | 9,114 | 9,262 | 9,690 | 19,275 (bound) | 67,656 |

The window is up by 10 s. The splash lifts at its 10 s bound at every start: the page never said its first screen had its data within
it, and said so 54-66 s after its browser was made, or not at all.

The first look (`watch.looked by=start`), its larger stages (`~` beside the look, `.queue` the waits for a process start's place):

| Start | Look | Stages over a second |
|---|---|---|
| 00:32Z | 23.7 s | withdraw 4.1, paused 2.1, sweep 1.2, moves 1.2, ~nodes 4.1 ×2 |
| 02:38Z | 70.9 s | withdraw 4.9, sweep 1.4, moves 1.8, ~start 22.3 (+1.4), ~limits 11.7, ~nodes 6.7 ×2 (+4.9), ~supervise 1.7 (+4.6) |
| 07:00Z | 60.1 s | withdraw 4.6, sweep 1.4, moves 1.5, ~start 18.3, ~limits 6.6, ~nodes 5.3 ×2 (+8.4), landing 1.1 (+2.0) |

`look.slow` names the look's line-following and tidy beside it at 41.9-46.4 s at every start: they wait for the page's word, and began at
its 30 s bound. Of a slow stage's first 5 s, 0.2-4.6 s were queued for a process start (nodes 3.4, tidy 3.8, landing 4.6).

## 3. Where the time goes (07:00Z)

| s | What |
|---|---|
| 9.3-9.7 | the page's browser is made, the pipe served; the first look begins and asks for the page's word |
| ~10-13 | the sessions slice's first read: `GET /api/sessions/changes`, 3.1 s |
| ~10-36 | `GET /api/repositories`, 25.9 s: the service reads every checkout's index at its first use |
| 19.3 | the splash lifts at its bound |
| ~40 | the word's 30 s bound passes: the machine's facts start, and the look's line-following and tidy |
| 40-65 | the facts' parts walk the session trees: 17.9, 19.1, 2.4 and 6.3 s (`help.room.look`'s `slowTrees`) |
| 67.7 | the page's word: *What needs you* judged, over the facts' last part |
| 69.9 | the first look ends |

The other starts run the same chain: at 02:38Z the parts began at 41-42 s and the last ended at 71.2 s, the word at 75.9 s; at 05:28Z
they began at 42 s and took 46-48 s each, and no word came.

## 4. The causes, ranked by time

1. **The first screen's word waited on its own consequence: 45-65 s at every start.** The page says its first screen has its data
   once its first-screen queries answered (`isFirstScreen`), and *What needs you* (`attention`) was one of them. The attention slice is
   judged only once the machine's facts hold their machine whole and every work part, and the facts start on the page's word, or at
   `DriverLoop.FirstScreenBound` (30 s) after the first look asks for it. So every start waited out the bound, then the facts' first
   judgement under the first look's git: the word at 64-76 s or never, the facts and the look's line-following and tidy idle for the
   first 30 s, the first look 60-71 s long.
2. **The service's knowledge index at its first use: 22.5-28.8 s at every start**, and 59 answers of the day over 2 s (16 s on average).
   `/api/repositories` was a first-screen root too. Its summary reads every entry (24,408, 33.6 MB) to count them, at each look's
   index feed as well.
3. **Process starts.** Every git of a start's work waits for one of three background places, `CreateProcess` being serial in the
   process and slow under the machine's scanner: 40-90 % of a slow stage's time is that queue, and a tree's walk that takes under a
   second idle took 2-48 s. It is what makes causes 1 and 4 cost seconds rather than milliseconds.
4. **The Sessions list's groups walked their trees one after another.** At a start the facts have not begun, so every tree the list
   asks (six or seven here) is walked by the groups slice itself, each in turn and each under cause 3; the list's groups, a
   first-screen query, wait for the sum.
5. **Every reader lists every quest, closed included.** 950 quests, 4 MB, 917 of them Done; about sixty readers each list them whole,
   one quest's lookup (`FindQuestAsync`) among them. 482 answers of the day took over 2 s (3.8 s on average, 30 s at most). A steady
   load more than a start's; 2-4 s for each reader at a start.
6. **Whole event records read where their end or a kept sum would do.** `SESSION_SPENT`, on the window's thread, parsed each asked
   part's whole record at each ask: `ui.stalled` 775 ms and 519 ms after the 02:38Z start. A record's first append in a process parsed it
   whole for its last number. Node reads the ten largest records whole in 305 ms (47 ms for 6.47 MB) and their ends in 3.4 ms; the
   typed .NET read is several times slower, as the stall says.
7. **The file system itself is not the start's cost.** The session records, the quests and the asks are rows of the service's SQLite
   store, each table read whole in 7-12 ms; what costs at a start is causes 1-4, waits and process starts. The files that cost later
   are the event records (517 MB, read whole by six readers) and the run records, read once per stamp.

## 5. Fixed, before and after

| Cause | Fix | Before | After |
|---|---|---|---|
| 1 | The first screen's roots are the status, registry, quests, asks, sessions (their groups among them) and notices; *What needs you* and the repositories draw as loading until they land (`0366058c36`) | the word at 64.6-75.9 s or never; the facts and the deferred look work at the 30 s bound, about 40 s | the word at the groups' first judgement; the facts and the deferred work start then, not at the bound; *What needs you* is the facts' first judgement after it |
| 4 | A door walks the trees no part judged `HelpTreeFacts.AtOnce` (4) at once (`f69d52f951`) | six trees of 50 ms: one at a time, 300 ms | four at once, 100 ms; at a start the sum of the walks becomes about a quarter of it, each walk still under cause 3 |
| 6 | What a part spent reads on from its last read (`SessionSpentKept`), whole again only where the record no longer stands as read; an append's number is read from the end (`651c69a51b`) | each ask parsed every asked part whole: 17-47 ms a large record in Node, the 775 ms stall in .NET | the first ask reads each record once; each later ask reads the lines written since; the number reads 4 KB from the end (0.2-0.9 ms) |
| 2 | A start takes the readings an earlier process left, stamped by this build (`KeptReadings`), and reads only the roots that moved; the summary counts in the store (`979440b9fc`) | every root read again at the first use: 22.5-28.8 s at the install; 36.7 s over four copies of this repository's records (89,744 entries); the summary's count 1,010 ms | nothing moved: 334 ms; one root moved: 9.3 s; the count 300 ms |
| 5 | The service keeps its quest list by the store's mark and answers a change since a mark (`GET /api/quests/changes`); every desktop reader of every quest reads the list kept by it (`QuestListKept`) (`80b4c960a5`) | 950 quests: each read made the list again, 613 ms and 3.9 MB | answered again unmoved 70 ms; a reader asking since its mark with nothing moved 4 ms and 14 KB of ids; one quest moved 204 ms and 18 KB |
| PUSH1d2b | The sessions and quests slices judge a kept list once while it is the same object (`JudgedOnce`), and a slice handed its last judgement compares nothing (`73972c604a`) | with no window, each 15 s period judged and compared every record: 148.7 ms over 1,261 | 0.003 ms a period |

Causes 2 and 5 are measured in-process without the window: `StartReadingProcessTests` (four committed copies of this repository's
records under `_fixtures/`, a store kept between composed hosts) and `QuestChangesProcessTests` (950 quests of about 4 KB on the
local host); PUSH1d2b by `SessionsSliceIdleProcessTests`. The slice already read the sessions on their change door (`SessionListKept`),
and the door answers from its kept answer while the store's mark holds; a write anywhere in the store's file moves that mark and
makes the whole session list again, `deletable` judged for every record (about 520 ms at 3,000 records, the route budget's reading).

The window's own after-measurement is the install's: once this branch is merged and the install updated, five starts' `start.stages`
(`page`, `splash`), the first `help.room.look` with trees, the first `watch.looked by=start` and the first `/api/repositories` read
against §2. This branch ran no installed window.

Not fixed here: cause 3 is the machine's protection, and fewer starts in the first look is a change of its own; the session door's
mark is the store file's, which any write moves (a mark of the sessions' own needs the quests' and the asks' too, since a record's
answer reads them); the session events' heads and offsets are the design's §2, for D275.
