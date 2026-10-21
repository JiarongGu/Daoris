# Keeping: each kind of thing Daoris writes kept by one rule the person sets and sees, a set-aside tree slimmed to what cannot be made again, and the rules run by the look, a free-space floor and a press (proposed, 2026-10-21)

**Standing:** proposed under D195 (CLEANUP1), for the reads D191 asks. Nothing here is built; its rows (§9) build it. Codex reads
it. Whether Fable reads is the steward's call under D191's note: the design adds one record (`data/keeping.json`) and one standing
choice (`driver.json`'s `keep`), and amends two decided rules (D88's fourteen days, for what git says can be made again, and the
history clearing design §7, by an age only the person sets). It changes no protocol between processes, no wire in place and no
graph. One choice waits for the owner under D191 point 3, since it lets the person's records go with no press at the time: §3.6,
an age for finished history. Nothing else publishes, spends an account, or removes anything that cannot be made again without the
person's rule or press.

Read for it: the row CLEANUP1 in `TASKS.md`; D88 and its notes (AUTOTIDY1 to CACHECLEAR1, lines 37-350); D102's TREEKEEP1 (278-302)
and CLEANUP1 (360-391); D173's CLEANUP1 note (1595-1684); D172 and the install layout design (§2 to §4); the This Daoris design (§3.1,
§6, §9); D185's UI/UX design (R1, R2, R3, §3.1) and D187; D153 and the history clearing design (§1.3, §2, §5, §7); D194 (the
`daoris driver` family handed to the driver's program); `.claude/knowledge/twins.md` (lines 37-52) and `setting-doors.md`. Every
statement about today was read from the code at `5e8caed81` and names its file and line; none was run. The install was measured
read-only on 2026-10-21 (§0.2).

## 0. The case, and what the design rests on

**The case.** On 2026-10-21 a session freeing disk space found the install's `trees/.tidied` holding 36.7 GB in 1.8 million files:
the folders the look's tidy moves aside and keeps fourteen days (AUTOTIDY1, D88), nearly all of it build output a rebuild makes
again. Each kind of thing Daoris keeps cleans itself by its own rule or by none; This Daoris shows the folders' sizes and no rule;
and nothing says the disk is filling until a build fails. The owner's direction (the row): one cleanup strategy built in, each kind
given a retention rule the person can set and see on This Daoris, run at the look's pace, below a free-space floor and by a press,
with a terminal twin; and the tidy asked whether it should drop a set-aside tree's regenerable outputs at once.

| Today | Read at |
|---|---|
| The look's tidy runs beside the watch, at most once each five minutes, and moves a landed tree's folder aside whole into `trees/.tidied/<workspace>/<repository>/<name>-<moment>` by one rename, then lets go of git's record of that tree | `BranchTidying.cs:29`, `DriverWatch.cs:330, 866`, `TreeSetAside.cs:32-38, 166-227` |
| Before it moves, the look's last look reads the tree's index (`ls-files -v -z`) and its folders; a tree holding uncommitted or untracked paths never moves | `TreeSetAside.cs:15-29, 55-111`; D88:157-161 |
| A later pass deletes a folder fourteen days after its move, only where nothing in it was written after the move and no link outside a build folder is in it; a folder it keeps is a blocked unit in *What needs you* | `BranchTidying.cs:111, 144-145`, `TreeSetAside.cs:254-283, 363-405`; D173's CLEANUP1 note, point 5 |
| The fourteen days is a constant; no setting reaches it | `BranchTidying.cs:145`; D88:84-86 |
| What a build or an install writes and re-makes is a fixed list of folder names; an ignored path under one that the registered checkout holds too goes with a tree at the press unasked; any other ignored file the tree wrote is its own | `TreeScratch.cs:9-26, 36-58`; D102's TREEKEEP1 |
| Downloaded versions nothing uses are listed and removed by the person's press on This Daoris, judged again, moved aside first; no rule by age and no terminal twin | `DownloadClearing.cs:62-87`; CACHECLEAR1's archive outcome |
| The machine log keeps 30 days and stops a file at 20 MiB; each process prunes at its open | `MachineLog.cs:37, 40, 80-86, 145-152` (driver), `MachineLog.cs:32` (service) |
| Finished history is cleared only by the person's press: *nothing clears by itself* | the history clearing design §7 (lines 451-463) |
| This Daoris's sizes are walked only when asked, in the background, kept for the process, bounded at 400,000 files and 60 s | `WiringSizes.cs:10-14, 47-51` |
| A recovery ref a branch or a tag of the person's holds goes after 30 days; one nothing holds stays until the person drops it | `RecoveryRetention.cs:22`; D102's CLEANUP1 note |
| Ask Daoris answers the downloads' press with an exemption, a discard that stays the person's | `HelpCoverageTests.cs:930-934` |

### 0.1 What it rests on

Every kind with a rule today has one act that judges and removes it (the tidy's purge, the clean-up, CACHECLEAR1, the clear, the
log's prune), and each judges again before it acts. What is missing is: one place where each kind's rule is set and seen; a reason
to run the removals of what can be made again before a disk fills; and a judgement, at the moment git can still give it, of what in
a tree git or a build makes again. The design adds those and no second remover. Every removal it runs is an existing act, or a new
act built in that shape: listed, judged again right before it goes, moved aside first where a program could hold it.

### 0.2 The owner's install, measured

Read-only, 2026-10-21, with .NET's directory enumeration following no link, the walk Daoris's own code uses. Git Bash's `du` was
dropped after it passed 280 s on one 22-thousand-file tree. The install's entries are at their old places under `data/`: its
machine log says HOMELAYOUT3's move was held at its last start (*the last update has not settled yet*), so each kind below is named by
its entry, which reads either place (D172 point 5). Sizes are binary, as Windows shows them.

| What | Size | Files | Notes |
|---|---|---|---|
| Set-aside trees | 37.4 GB | 1,982,441 | 73 folders. 62 are this repository's sessions', all set aside on 2026-10-20 (27.5 GB, 1.44 million files); 11 are the work workspace's, in three repositories (9.9 GB, 0.55 million) |
| … under a folder of `TreeScratch`'s list | 33.5 GB (90%) | 1,678,426 (85%) | |
| … the rest | 3.9 GB | 301,815 | about 61 MB a folder for this repository, its tracked size: a fresh checkout is 60 MB in 4,501 files |
| One such folder | 484 MB | 22,198 | the web's `node_modules` 229 MB in 17,069 files, the root's 25 MB, `bin/` and `obj/` about 170 MB |
| Live trees | 2.0 to 2.5 GB | 115 to 137 thousand | 7 trees, two walks minutes apart while sessions built; 83% of the bytes under build folders |
| Agents' versions (`toolchain/`) | 1.4 GB | 14,307 | four versions of three agents |
| Daoris's browser's profile (`browser/engine/`) | 845 MB | 7,762 | its HTTP cache 364 MB, code cache 301 MB, component cache 61 MB |
| An earlier build's (`browser/profile/`, `webview2/`) | 199 MB, 83 MB | 1,915, 371 | the WebView2 window's profile and engine data from before D92 |
| Agents' account homes | 380 MB | 3,454 | the agent's own conversations (`projects/`) 302 MB over four accounts |
| The service's installed copy (`bin/`) | 369 MB | 126 | |
| The window's profile (`chromium/`, `config/`) | 164 MB | 639 | its component cache 61 MB |
| Session records (`sessions/`) | 62 MB | 623, and 102 folders | conversations (`.events.jsonl`) 56.9 MB in 209 files, the largest 2.3 MB; transcripts (`.log`) 4.1 MB in 242; the oldest 2026-09-30 |
| Plugins | 50 MB | 1,393 | |
| The knowledge store | 47.8 MB | 1 | no `-wal` at the read |
| The machine log | 11 MB | 80 | 2026-09-29 to 2026-10-21, so the prune has had nothing to take; the largest day 4.2 MB |
| Usage history, records, rooms, locks, the rest | under 1 MB each | | |
| The program (`app/`) | 574 MB | 326 | |
| The volume | 1,024 GB, 143.5 GB free (14%) | | NTFS; one free-space read took 0.3 ms |

The home's `data/` held about 43 GB, 87% of it set aside. **Were a day like 2026-10-20 repeated** (62 trees of 454 MB set aside),
fourteen days of it would hold about 385 GB. With the build folders dropped at the move, about 52 GB; with git's own files dropped
too, what the trees wrote of their own, which in these folders is a few KB. That estimate is why §5 drops both. The home held about
2.2 million files, past `WiringSizes`' 400,000-file budget, so This Daoris's sizes on this install end partial; one walk of
`.tidied` took 36 to 100 s by .NET's enumeration. Neither was looked at on the window.

## 1. Everything Daoris writes, and the rule each kind has

A **kind** is what a person decides about together: one rule, one row. Each row names its entries in D172's table
(`DaorisHome.cs:326-394`), who writes it and what reads it back, its rule today read from the code, and what may remove it. **No
rule** marks a kind nothing removes by age, size or event today. Sizes are §0.2's.

### 1.1 `work/`: where agents work

| Kind | Entries | Written by → read by | Rule today | What may remove it |
|---|---|---|---|---|
| Live trees | `trees` (`trees/<ws>/<repo>/<tree>`) | `SessionTrees.OpenAsync` (`TreeContext.cs:42`) → sessions, git, the clean-up | moved aside by the look once its branch's work is on the line (D88's AUTOTIDY2a), never by age; one a session runs or waits in, words wait on or a review shows from stays | the look (moves aside), the clean-up's press (`trees clean`), *Discard*, abandon (D132) |
| … their build outputs | ignored folders of `TreeScratch`'s list | the session's build or install; TREEDEPS1's copy at the open | **no rule**: they live and go with their tree | with their tree |
| Set-aside trees | `trees/.tidied/…` | the look's move (`TreeSetAside.MoveFolderAside`, `:206`), a folder that is no tree and a cut-off opening (`TreeStrays`), an opinion's copy and a replay's tree (`PassLeftovers`) → the purge, the work inventory | 14 days after the move, where nothing was written since and no link outside a build folder is in it (`TreeSetAside.cs:254-283`); one the purge keeps is a blocked unit | the purge only; nothing early |
| Rooms | `help-room`, `help-rooms`, `intake`, `brainstorm-rooms`, `opinion-packets`, `usage-read` | Ask Daoris's room (`HelpRoom.cs:79`), intake's (`IntakeRoom.cs:62`), a brainstorm pass's (`BrainstormFiles.cs:57`), a reviewer's packet (`OpinionPackets.cs:44`) | a brainstorm's rooms by `RemoveRooms` and with its records at its clear (`BrainstormFiles.cs:250-262`), a turn's room made afresh (`:242`); an intake room by a workspace's clear that leaves it no ask (D153 §2.2); Ask Daoris's rooms and an opinion's packets: **no rule** (no delete in `OpinionPackets.cs`) | the clear; the brainstorm's own |
| Kept files | `quest-files`, `ask-files`, `session-files`, `proof` | the service's `QuestFiles` and `Asks`, the driver's `ChatFiles`; `proof` has no writer | with their record's clear or delete (D153, D95, D126) | the clear, the delete |

### 1.2 `data/`: records and choices

| Kind | Entries | Written by → read by | Rule today | What may remove it |
|---|---|---|---|---|
| Conversations and transcripts | `sessions` (`<id>.events.jsonl`, `.log`, `.harness.json`, marks) | `SessionEvents`, the four starts, `HarnessConversations` → the page, the trace, a go-on | **none, by design** (the history clearing design §7, D126 §11) | the person's *Clear history…* and a conversation's *Delete…*; Ask Daoris's by `help delete` |
| The service's records and index | `knowledge` | the service (`HostComposition.cs:46`) | its records by the clear; its index rewritten by each index; the file is never compacted (no `VACUUM` in the service's code) | the clear, rows only |
| Bounded records | `usage`, `landings`, `session-branches`, `workflows` (units, closures), `usage-history`, `opinion-dues`, the automatic landings | the driver | each its own: usage's newest 500 (`Usage.cs:48`); the landings' and branches' traces, the newest 50 per repository (`LandedBranches.cs:320`, `SessionBranches.cs:85`); a unit's settled 50 and history 200 (`NodeRecord.cs:166-168`); the closed automatic landings and opinion dues, 200 (`AutoLanding.cs:286`, `OpinionDues.cs:16`); usage history, 90 days and 64 MiB (`UsageHistory.cs:105-109, 134-137`) | their writers |
| Plugins and their data | `plugins` (with `.data/` and `.trials/`), `plugin-state` | `PluginInstall`, the plugin | **no rule** but the plugin's removal (D119) | removing the plugin |
| Settings and small records | `driver`, `harnesses`, `accounts`, the proposals, `brainstorms`, `home-id` and the rest of §3.1 there | their writers | never by age; a settled rule proposal with its unit's clear; a brainstorm's record by `brainstorm clear` | the person |

### 1.3 `profiles/`: sign-ins

| Kind | Entries | Rule today | What may remove it |
|---|---|---|---|
| Agents' account homes | `agent-homes` | the agent's own. Claude Code removes its own conversations after its `cleanupPeriodDays` (30 days by its documentation, not read here); Daoris neither sets nor reads it, since the home is the tool's (D49 §4, the history clearing design §1.3) | removing the account (D125) |
| Daoris's browser's profiles | `browser-engine`, `browser-edge`, `edge-cookies` | the engine's own cache bound: Daoris starts it with no cache switch (`EngineBrowser.cs:113-132`); **no rule of Daoris's** | nothing of Daoris's |
| The window's profile | `window` (`chromium/`, `config/`) | the UI kit's engine's own | nothing of Daoris's |
| Keys and remotes | `keys`, `remotes` | never | the person |

### 1.4 `cache/`: what can be fetched again

| Kind | Entries | Rule today | What may remove it |
|---|---|---|---|
| Agents' and tools' versions | `toolchain`, `tool-versions` | **no rule**: the person's *Clear unused downloads…* only. A download's staging (`<version>.part`) a crash left stays until that version is downloaded again (`ToolInstall.cs:57`; the alone path design §0) | CACHECLEAR1's press |
| The service's installed copy | `bin` | never: a repository's `.mcp.json` names it (the layout §3.4) | `publish:service --install` replaces it |

### 1.5 `run/`, `logs/`, `app/` and `update/`

| Kind | Entries | Rule today | What may remove it |
|---|---|---|---|
| What a running Daoris holds | `home-lease`, `running`, `locks`, `spawn`, `starts`, `hooks`, `probing`, `session-pids`, `requests`, `edge-record`, `pipe`, `home-move` | each its own: a start's request 24 hours (`StartRequests.cs:16, 118`); a session's request a minute (`SessionRequests.cs:325`); a session's spawn files at its end (`SpawnServers.cs:76`, `SpawnSkills.cs:111`, `SessionChecks.cs:121`); the pipe's journal a day (`PipeJournal.cs:228-233`) and its sent presses 24 hours (`SentPresses.cs:21`); a lock nobody holds is taken over | their writers |
| The machine log | `logs` | 30 days, 20 MiB a file, pruned at each process's open | the processes |
| The engine's log | `window`'s `chromium/cef.log` | **no rule** | nothing |
| The program | `app/` | replaced whole by an update (D139) | the update |
| An update's folders | `update/previous/`, `update/staged/`, `update/failed/` | the build before, once the swap is confirmed (`StagedBuild.cs:498-501`, `InstallSwap.cs:204-207`); a staged build when unstaged (`StagedBuild.cs:512-515`); a failed one **kept, no rule**, for the person to read (`StagedBuild.cs:192`) | the update |

### 1.6 What is not Daoris's now

| Kind | Names | Rule today | What may remove it |
|---|---|---|---|
| An earlier build's | `webview2/`, `browser/profile/` | **no rule**; shown as an earlier build's, nothing offers to remove it (`WiringSizes.cs:68-73`, the This Daoris design §3.1) | the person, by hand |
| The person's | `salvage/`, and any name the table does not hold | never touched (the history clearing design §1.3) | the person |

### 1.7 Outside the home

| Kind | Where | Rule today | What may remove it |
|---|---|---|---|
| Daoris's own refs and records in a registered repository | `refs/heads/daoris/*`, the landings' branches, `refs/daoris/discarded/*`, `.git/worktrees/<tree>` | the clean-up's proof (D88, D102); a recovery ref a branch or a tag holds after 30 days, one nothing holds until the person drops it (`RecoveryRetention.cs:22`, `work recovery drop`); a tree's record with its folder | the look, the clean-up, the person |
| What Daoris's sessions left in the person's own tool home | Claude Code's `projects/<flattened folder>` (OWNSPACE2) | listed, never removed (`ToolHomeLeftovers.cs:24-27`) | the person, by hand (OWNSPACE2e) |

The registered checkouts, their own build outputs and their own refs are the person's and appear nowhere in this design.

## 2. Each rule in one place

### 2.1 The kinds: one table in the driver library

`KeepKinds`, in `Daoris.Driver.Platform`, holds each kind: its id, the D172 entries it covers by name, whether it can be made
again, its default rule, what the person may set and within which bounds, its remover, and whether the floor may run it early
(§3.2). The route, the terminal and the pass read it; the page's catalogue holds each id's words, and a census test holds the two to
each other, as WIRING1d's does for every setting. A kind is added to the table in the change that adds the entry.

| Kind | Made again? | Default | The person may set | Its remover |
|---|---|---|---|---|
| `set-aside` | git's own files and the shared build outputs in it, yes (§5); the rest, no | 14 days; what git says can be made again goes at the move | 1 to 90 days; `whole`, which keeps a folder whole as today | the move's slim (§5), the purge |
| `downloads` | yes | an unused version goes 7 days after a pass first finds it unused | 0 to 365 days, or `never` (the press alone) | CACHECLEAR1's |
| `browser-cache` | yes | the engine's own bound, cleared below the floor | — | new: the cache folders of `browser-engine` alone (§3.2) |
| `logs` | no | 30 days | 7 to 30 days: the processes' own 30 stays the ceiling, since the service would otherwise read the driver's choices | the log's prune |
| `history` | no | `never`: the person's clear alone | `never`, or 30 to 3,650 days (§3.6, the owner's) | the clear |
| `trees`, `records`, `rooms`, `plugins`, `sign-ins`, `program`, `service`, `running` | — | their own, §1 | nothing: shown, with whose rule it is | their own |
| `left-over` | — | never by itself | nothing | new: the press alone (§3.3) |
| the floor | — | the smaller of 10% of the volume and 20 GB | a size, a share of the volume, or `off` | — |

**A gap it closes without a new setting.** An opinion's packet goes with its quest's clear (the history clearing design §2.2 gains
its row). A download's staging a crash left is PUSH1f2c's, which names each staging by its process and sweeps it once that process is
gone (the alone path design §5); this design adds nothing there.

### 2.2 The person's choice: `driver.json`'s `keep`

The overrides are standing choices (`DriverConfig`), in one object; a key that is absent is its kind's default.

```
"keep": {
  "setAside":  { "days": 14, "whole": false },
  "downloads": { "days": 7 },          // null: only the press
  "logs":      { "days": 30 },
  "history":   { "days": null },       // §3.6
  "floor":     { "gb": 20 }            // or { "percent": 10 }, or { "off": true }
}
```

- **Judged in the library**: `StandingChoices` refuses a value out of its bounds with a coded refusal (`KEEP_OUT_OF_RANGE`), the
  same on both paths (D194 point 4), said under the row's editor with the editor kept open (R3).
- **Twins**: the key is added at each place `twins.md:48-52` lists, the CLI's `driverconfig.ts` with its keeps table, unless PUSH1f2b
  has retired that reader by the time the row is built, when it is the driver's alone.
- **The terminal twin** is a standing choice, handed to the driver's program as D194 hands the family (`HANDED_VERBS`, held to
  `DriverChoicesCommand.Verbs` by `handed.test.ts`):

```
daoris driver keep                                  each kind's rule, and whether it is the default
daoris driver keep set-aside <days>|whole|slim|default
daoris driver keep downloads <days>|never|default
daoris driver keep logs <days>|default
daoris driver keep history <days>|never|default     (§3.6)
daoris driver keep floor <n>GB|<n>%|off|default
```

- **Ask Daoris** proposes a rule through the `setting` kind's door for `keep` (`setting-doors.md`), applied by the person's press.

### 2.3 What was kept and removed: `data/keeping.json`

A new home entry, `keeping`, in `data/` (D172's test 7: a record of the person's acts and the rules' runs), added to the three home
twins' tables at once. It is a machine record: never synced, no route answers it raw, written atomically by the process holding the
home's lease.

```
{ schema: 1,
  kinds:    { <kind>: { bytes, files, measured, how: ledger|walk, partial,
                        last: { at, door: look|floor|press|terminal, removed: { count, bytes }, kept: { <why>: count } } } },
  setAside: { <folder, from the trees home>: { repository, branch, commit, moved,
                                               dropped: { tracked: { files, bytes }, build: { files, bytes } },
                                               remains: { files, bytes } } },
  unused:   { <agent|tool>/<name>/<version>: <first found unused> } }
```

Every field is added, never renamed (a stored shape). A reader meeting none reads every kind as never measured and nothing removed.

## 3. When it runs

### 3.1 The look's pace

A pass, `Keeping`, runs beside the tidy in the watch (`DriverWatch.cs:866`), only where the tidy runs: the shell's watch and the
headless `drive`'s, never a one-look run or a gate's driver (D88's AUTOTIDY1 judgement). Each pass:

1. reads the free space (§3.2);
2. finishes any slim §5 began and could not end;
3. runs the purge at the person's days (`BranchTidying.cs:111`, handed the days in place of the constant);
4. once a day (the first pass 24 hours after the last daily one, and the first after a rule changed): unused downloads past their
   days, log files past their days, finished history past its days where set (§3.6);
5. at most one bounded measure per kind a day (§4.3).

The slim itself runs in the tidy's pass, right after each move (§5), outside the repository's hold, since it touches no git and no
tree of the repository's, so a start never waits on it.

### 3.2 The free-space floor

**Measured** by the operating system's free-space read for the Daoris folder's own path, the bytes this account may use: on
Windows `GetDiskFreeSpaceExW` on the folder, which answers for the volume that holds it, a mounted folder included (a drive
letter's read would not), `statvfs` elsewhere. Read at each pass; one read took 0.3 ms on the install. Never a walk.

**Default: the smaller of 10% of the volume and 20 GB.** On the install's 1,024 GB volume that is 20 GB; on a 128 GB one, 12.8 GB.
The margin holds a day of builds (2026-10-20 set aside 27.5 GB of one repository's trees) and an operating system's update, without
asking a small disk to keep most of itself free. The person sets a size, a share, or `off`.

**Below it**, the pass runs at once, at most once an hour while below, these and only these, each through its own act and guards:

1. a set-aside folder's slim not yet finished;
2. unused downloads, their days ignored: a version within `Settling` (an hour) or one a program runs from is still kept;
3. the browser's caches: in `browser-engine` alone, the folders `Cache`, `Code Cache`, `GPUCache`, `GrShaderCache`, `ShaderCache`,
   `DawnGraphiteCache`, `DawnWebGPUCache`, `component_crx_cache` and `Shared Dictionary/cache`, each moved aside by one rename
   first, which Windows refuses while the browser runs, so a running browser's caches are kept. Never its cookies, storage or
   preferences, and never the window's profile, which is the UI kit's.

Still below after these, it is **said, never taken**: a row in *What needs you* names the free space, the floor, what Daoris freed,
and what the person's press could free and how much (§3.3), with the press as its door; the machine log writes `space.low`. Said once
each time the free space falls below, and cleared once it is 5 GB above the floor (`space.ok`).

### 3.3 The person's press

*Free up space…* on This Daoris's *Disk space* card, and `daoris-driver space free`. **Listed first, then pressed** (D88's shape, as
CACHECLEAR1's):

1. The first press lists, per kind: **(a)** what its rule takes now; **(b)** what could go early, by the person's word: set-aside
   folders before their date, each with its repository, its date, its size and whether it holds files of its own; unused downloads
   within their days; the browser's caches; what an earlier build left (`webview2/`, and `browser/profile/` whose sign-ins go with
   it); a failed update's folder. **(c)** For a kind with a press of its own, its door: *Clean up…* on a workspace's Branches, *Clear
   history…* on its page, *Discard* on a tree.
2. One press takes (a). Each group of (b) has its own press, said first in its own words. Each sends exactly the units the list
   showed, each judged again right before it goes; one changed since is kept and counted (*Removed 11 of 12; 1 changed since the
   list and was kept*).
3. A press that removes many files answers at once that it started, and its card says what is being removed with its count and its
   time, then its end (R2); the terminal prints a line per kind as it ends.

### 3.4 What never runs by itself

- Anything that cannot be made again before its rule's time: a set-aside folder's own files before its days, a log within its days,
  finished history without the person's age (§3.6).
- A live tree, or its build outputs (§7).
- Anything in `profiles/` but the browser's caches (§3.2); the agents' homes; the window's profile.
- `bin/`, `app/`, `update/failed/`, an earlier build's and the person's names (§1.6).
- Anything outside the home (§1.7): the person's checkouts, their refs beyond D88's and D102's rules, the person's tool home.

### 3.5 What is refused while it is used

| Kind | Kept while | Known by |
|---|---|---|
| Live trees | a session running or waiting names it, words wait on it, a review shows from it | `BranchTidying.InUseAsync` (`BranchTidying.cs:133-142`) |
| Set-aside trees | something in it was written after its move; a link outside a build folder; a delete refused | `TreeSetAside.Kept`, `purge-failed.json` |
| Downloads | pinned, used, named, unreadable, within the hour, a program runs from it | `DownloadClearing` |
| The browser's caches | the browser runs | the rename refused |
| Logs | today's file; one a process holds | the date in its name; the delete refused |
| History | open work, a live session, what waits on the person, unpushed moves | the history clearing design §1.2 |
| An earlier build's, a failed update's | a file is held; an update not at rest | the rename refused; the update's state |

Each kept unit is said with its reason, in the list and in the log.

### 3.6 History by an age (waits for the owner)

The history clearing design §7 (and D126 §11) decided that finished history never clears by itself: a record gone without a press is
one the person never saw go, and one install's 24 MB was met by one listed press. This design keeps that the default and offers an
age only the person sets: once a day, each workspace's finished units whose last move is older than the days are cleared through
the clear itself, every refusal holding (D153 §1.2), each said, the log's `history.cleared` with `door: rule`. It amends §7 to
*nothing clears by itself unless the person set an age*. Since it lets the person's records go with no press at the time, it is
asked of the owner (D191 point 3): yes builds KEEP1h; no drops KEEP1h, and `history` stays shown with its press as a door.

## 4. This Daoris

### 4.1 The card

**Place:** Settings → This Daoris, a card *Disk space* after *Folders* (the This Daoris design §2). Its rows use R1's label track
(`SettingList`). Each kind is one `SettingRow`: the kind's name as the label; its terminal twin as the hint; the size with when it
was counted as the value; under it, the rule in words with *Change…* where the person may set it, and a line *next* and *last*. A
kind whose rule is not Daoris's says whose it is and offers no *Change…*.

```
 wide, Settings → This Daoris, Disk space
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Disk space ⓘ                                                                       [Free up space…]    │
│ Free space                     143.5 GB of 1,024 GB · keep at least 20 GB         [Change…]            │
│ daoris driver keep floor …                                                                             │
│ Set-aside trees ⓘ              37.4 GB · 73 folders · counted 10:42                                    │
│ daoris driver keep set-aside … kept 14 days; what can be made again goes at once  [Change…]            │
│                                next: 3 folders on 29 Oct, 0.6 GB · last: nothing removed yet           │
│ Unused downloads ⓘ             1 of 4 versions, 0.4 GB                                                 │
│ daoris driver keep downloads … an unused version goes 7 days after it was found unused [Change…]       │
│                                next: claude-code-acp 0.79.0 on 28 Oct · last: 18 Oct, 1 removed, 0.3 GB │
│ The browser's cache ⓘ          0.7 GB · the browser's own limit; cleared when space is low             │
│ Machine log ⓘ                  11 MB · kept 30 days                               [Change…]            │
│ Finished work's records ⓘ      62 MB · kept until you clear them                  [Open the workspace] │
│ Trees in use ⓘ                 2.1 GB in 7 trees · cleaned up once their work is on the line [Branches]│
│ Sign-ins ⓘ                     0.7 GB · yours, and each agent's own rule                               │
│ Left by an earlier build ⓘ     282 MB · removed only by your press                                     │
│ Everything else ⓘ              1.0 GB · each by its own rule                       ˅                   │
└────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

```
 narrow (the 400 px floor): the track goes, each value under its words
┌──────────────────────────────────────────────┐
│ Disk space ⓘ               [Free up space…]  │
│ Free space                                   │
│ 143.5 GB of 1,024 GB · keep at least 20 GB   │
│ [Change…]                                    │
│ ──────────────────────────────────────────── │
│ Set-aside trees ⓘ                            │
│ daoris driver keep set-aside …               │
│ 37.4 GB · 73 folders · counted 10:42         │
│ kept 14 days; what can be made again goes    │
│ at once                          [Change…]   │
│ next: 3 folders on 29 Oct, 0.6 GB            │
└──────────────────────────────────────────────┘
```

```
 the press, listed, drawn under the card's head as the Downloads row's list is drawn under its row
│ What goes now, by your rules                                                         4 items · 0.9 GB │
│   Unused downloads      claude-code-acp 0.79.0                                       0.3 GB           │
│   Set-aside trees       2 folders past 14 days                                       0.6 GB           │
│   Machine log           1 day past 30 days                                           4 MB             │
│                                                                    [Remove 4 now]  Never mind         │
│ What could go early, by your word                                                                     │
│   Set-aside trees       71 folders before their date · 3 hold files of their own     36.8 GB  [Remove…]│
│   The browser's cache   kept while the browser runs                                  0.7 GB   [Remove…]│
│   Left by an earlier build  webview2, browser/profile (its sign-ins go too)          282 MB   [Remove…]│
│ With their own press:   Clear history… (a workspace's page) · Clean up… (Branches)                    │
```

The *Change…* editor is inline under its row (`FormGrid`): a number of days (4.5rem) with the unit, and the choices the kind has
(*until I remove it*, *keep folders whole*, *the default*), *Save* and *Never mind*; a refusal is said under it (R3).

### 4.2 States

| State | What the card shows |
|---|---|
| First load | skeleton rows at the track's width |
| Ready | every row with its size, rule, next and last |
| A kind never measured | *not counted yet* and *Count* on its row; the rest as ready |
| Counting | that kind's *Counting…* (static text, FREEZE1); the rest as ready |
| A count that stopped short | the size *at least …* and why (*more files than one count reads*) |
| Below the floor | the free-space line in the warn ink, what Daoris freed, and *Free up space…* as the primary press |
| Floor off | *no floor: Daoris frees nothing early* |
| A rule being saved, or refused | the row's editor disabled with its status, or the driver's sentence under it, the editor open (R3) |
| The press listed, running, done | the list; R2's line with its count and time; what went and what was kept, by reason |
| A shell older than the page | *This window answers no disk space: it is older than this page*, and no card |
| A development run | the scratch root's volume, said as the development run's |
| Wide, half, narrow; en, 中文; light, dark | R1's track; stacked below 32rem of card |

### 4.3 Sizes without walking every file

1. **The free space is the operating system's read** (§3.2), at each pass and each open: never a walk.
2. **The big kinds keep a ledger** in `data/keeping.json`, kept by the acts that change them: a move aside records what remains after
   its slim, counted by the slim's own walk of a folder that is by then small; a purge or a press subtracts what it removed, counted
   as it removes; a download adds its version's size as it publishes; CACHECLEAR1's listing already walks each version and writes
   it. The page reads the ledger at once, saying *counted* with the time.
3. **A walk per kind**, never the whole home: bounded by `WiringBudget` per kind so one large kind never starves the rest, in the
   background, at most once a day per kind and on *Count* or *Measure again*, never on a page's open (`WiringSizes.cs:47-51`). Each
   kind's walk publishes as it ends, so the card fills in kind by kind: WIRING-SIZES-LIVE1's need, for the kinds this card shows.
4. **What drifts** (a late write into a set-aside folder, a tree built in) is corrected by the next walk of that kind; the card says
   the time of the count, so a stale size is never presented as now.

With §5's slim, a set-aside folder of this repository is a few KB rather than 22 thousand files, and the walks fit their budgets.

### 4.4 The read, the presses and the terminal

- **`SYSTEM_SPACE`**, a read in the `system` route domain beside `SYSTEM_WIRING` and `SYSTEM_DOWNLOADS` (`SystemRoutes.cs`,
  `bridge/system.ts`), its answer every field added and never renamed:

```
{ schema: 1,
  volume: { path, free, total, floor: { bytes, from: default|size|percent|off }, below, freedEarly: { bytes, at } | null },
  kinds:  [ { id, entries, bytes, files, measured, partial, partialWhy, counting,
              rule: { kind: days|never|own|proof|press, days, whole, default, whose },
              settable, next: { at, count, bytes } | null,
              last: { at, door, removed: { count, bytes }, kept: { <why>: count } } | null } ] }
```

- **`SET_KEEP`**, the standing choice's press in the driver's route domain, `{ kind, days | never | whole | default }` or
  `{ floor }`, answering the state as every standing choice does; its operation is `StandingChoices`' (D194 point 4).
- **`SYSTEM_SPACE_FREE`**: `{ plan: true }` answers the list of §3.3; `{ units, early }` removes exactly those, answering at once
  that it started where it removes many, then its end as news under the `system` scope.
- **The terminal**, D50's twin of each:

```
daoris-driver space [--json] [--measure]
    the free space and the floor; each kind's size, rule, next and last clean.
daoris-driver space free [--kind <id>]... [--early <group>]... [--yes]
    list what each rule takes now, and what could go early; with --yes, remove what the rules take,
    and with --early, that group too. Exit 0 done or listed, 1 something named was kept, 2 could not.
```

  With a Daoris running, `space free --yes` is its client (`InstanceDoor.PressAsync`, D194 point 2); alone, it acts under the
  home's lease. `--json`
  prints `SYSTEM_SPACE`'s answer field for field, so the twins share one test table.
- **Ask Daoris**: `SET_KEEP` through the `setting` kind's door; *Free up space…* `Exempt`, in CACHECLEAR1's words: a discard that
  stays the person's own press, listed first, nothing in use removed. *The machine now* says the free space and the floor.
- **The machine log** gains, names and counts only, folders relative to the trees home (the machine log design §4): `tidied.slimmed`
  (`folder`, `commit`, `tracked`, `build`, `bytes`, `remains`), `space.freed` (`kind`, `count`, `bytes`, `door`, `early`),
  `space.kept` (`kind`, `why`, `count`), `space.low` and `space.ok` (`free`, `floor`).

### 4.5 Both languages

Keys name the place (`translation-parity`). The glossary gains **free up space** (释放空间: run each kind's rule now, and remove what
the person chooses early; a record's *clear* and a tree's *clean up* keep their own presses) and **set aside** (另行存放, the word
`projects.tidied.aside` already says: a tree's folder the look moved out of the trees, kept for its days before it is deleted);
`names:check -- --strict` runs on both.

| Key | Kind | English | 中文 |
|---|---|---|---|
| `settings.daoris.card.space` | card | Disk space | 磁盘空间 |
| `settings.daoris.button.freeUp` | button | Free up space… | 释放空间… |
| `settings.daoris.button.change` | button | Change… | 更改… |
| `settings.daoris.space.free` | row | Free space | 可用空间 |
| `settings.daoris.space.ofVolume` | value | {{free}} of {{total}} | 共 {{total}}，可用 {{free}} |
| `settings.daoris.space.floor` | value | keep at least {{size}} | 至少保留 {{size}} |
| `settings.daoris.space.floorOff` | value | no floor: Daoris frees nothing early | 未设下限：道衍不会提前释放 |
| `settings.daoris.space.kind.setAside` | row | Set-aside trees | 另行存放的工作树 |
| `settings.daoris.space.kind.downloads` | row | Unused downloads | 未使用的下载内容 |
| `settings.daoris.space.kind.browserCache` | row | The browser's cache | 浏览器缓存 |
| `settings.daoris.space.kind.logs` | row | Machine log | 本机日志 |
| `settings.daoris.space.kind.history` | row | Finished work's records | 已完成工作的记录 |
| `settings.daoris.space.kind.trees` | row | Trees in use | 使用中的工作树 |
| `settings.daoris.space.kind.signIns` | row | Sign-ins | 登录信息 |
| `settings.daoris.space.kind.leftOver` | row | Left by an earlier build | 旧版本留下的 |
| `settings.daoris.space.kind.rest` | row | Everything else | 其他 |
| `settings.daoris.space.rule.days` | value | kept {{count}} days | 保留 {{count}} 天 |
| `settings.daoris.space.rule.never` | value | kept until you remove it | 保留到你移除为止 |
| `settings.daoris.space.rule.slim` | value | what can be made again goes at once | 可重新生成的内容立即移除 |
| `settings.daoris.space.next` | value | next: {{what}} on {{when}} | 下次：{{when}}，{{what}} |
| `settings.daoris.space.last` | value | last: {{when}}, {{count}} removed, {{size}} | 上次：{{when}}，移除 {{count}} 项，{{size}} |
| `settings.daoris.space.nothingYet` | value | nothing removed yet | 尚未移除任何内容 |
| `settings.daoris.space.now` | heading | What goes now, by your rules | 按你的规则现在移除的 |
| `settings.daoris.space.early` | heading | What could go early, by your word | 可由你提前移除的 |

Each rule's word, each *whose* (`settings.daoris.space.whose.*`: *the agent's own*, *the browser's own limit*, *the update*), each
kept reason (`settings.daoris.space.kept.*`) and each refusal (`settings.daoris.space.refused.*`) is a key in both catalogues. A size
is formatted by `format.ts`; a version, a repository and a folder are content and stay as written.

## 5. Slimming at the source

**Decided: yes.** At the move aside, a tree's folder keeps only what cannot be made again; what git says it holds and what a shared
build folder holds goes at once, and the rest waits its days.

**The judgement, by git's word and never a guess.** In the look's last look (`TreeSetAside.LastLookAsync`, `:55-111`), inside the
repository's hold, before the move, with the moment taken first (*judged at*):

1. **The commit**: `rev-parse --verify HEAD^{commit}` in the tree. Where it does not read, nothing is slimmed.
2. **Git's own files**: the index the last look already reads (`ls-files -v -z`, `:76`), whose hidden entries already keep the tree
   (`:79-87`). The look moves only a tree with no uncommitted and no untracked path (D88:160-161), so each listed path equals the
   commit. A submodule checked out in the tree holds a `.git`, which the last look reads as another repository and keeps the tree
   for (`:89-108`); one not checked out is an empty folder.
3. **Shared build outputs**: one `status --porcelain=v1 -z --ignored=traditional --untracked-files=normal` in the tree, which lists a
   folder holding only ignored files as `!! <folder>/`. Such a folder goes where a segment of its path is in `TreeScratch`'s list and
   the registered checkout holds a folder at that same path (TREEKEEP1's *shared*): the same rule that lets the press delete it
   unasked (`TreeScratch.cs:9-26`). A build folder git lists file by file (a tracked file inside it) stays.
4. **Everything else stays**: an ignored file outside the list (a `.env`, a local config, `local/`, `_fixtures/`, an editor's
   folder), a build folder the checkout does not hold (a `dist/` only this tree built), any path git did not name, and anything a
   read could not reach.

**The slim, after the move.** Only once `MoveAsideAsync` reports the folder moved and git's record let go (`:166-197`); a folder put
back is never slimmed. The rename succeeding is Windows' word that nothing held a file in the folder open. The slim removes exactly the
paths the judgement named, inside the moved folder, never what it did not name: each tracked file, each shared build folder through
`DeleteNeverFollowing` (`:413`), a link removed as a link with what it points at untouched (TIDIEDLINK1), a read-only file made
writable first. A path written after *judged at* stays, so a write that raced the move is kept for the folder's days; one written
after the move keeps the folder at the purge, as today. Empty folders left go. A failure leaves that path and is said; the next pass finishes a slim it began.
The ledger records the commit, the repository and the branch, what went and what remains, and the log writes `tidied.slimmed`.

**Bringing it back.** The commit is on the line, or on the recovery ref the look kept (D88's AUTOTIDY2a), so `git worktree add
<folder> <commit>` makes git's files again and a build the rest. The folder's row in the press's list and in `work state` says that
command with the commit from the ledger.

**What is not slimmed**: a folder moved aside that was no tree (`TreeStrays`), a cut-off opening, an opinion's copy and a replay's
tree (`PassLeftovers`), since git cannot vouch for them; a tree whose judgement did not read; `whole` set by the person; and the 73
folders already set aside, whose git records are gone, which go by their fourteen days (all by 2026-11-03) or by the person's early
press.

**What it saves on the install's day** (§0.2): of a 484 MB folder of this repository, about 424 MB of shared build output and 60 MB of
git's own files go; what remains is what the session wrote of its own. Of the 73 folders, 90% of the bytes are under a build
folder's name; how much of that the checkouts share was not measured, so the saving is between that share and the whole.

## 6. Safety

1. **Nothing a session uses is removed.** The live trees and the set-aside folders keep the look's guards (§3.5); every other removal
   is moved aside by one rename first wherever a program could hold it, which Windows refuses while a file is open, so a held thing
   is kept as *in use*, never half removed (CACHECLEAR1's rule).
2. **Nothing outside the home is touched.** Every path is the table's entry under the Daoris folder (`DaorisHome.Entry`), checked to
   lie below it with no link on the way (`TreeWorktrees.LinkBelowTreesRoot`'s rule, `TreeWorktrees.cs:89`), and every walk and
   delete follows no link. Daoris's
   refs in a registered repository keep D88's and D102's rules alone; the checkouts and the person's tool home are never written.
3. **Only what can be made again goes by itself before its time**: git's files, shared build outputs, unused downloads and the
   browser's caches. Everything else goes only by a rule the person can see and set, or by their press (D88's amendment of D51 rule 7).
4. **Every removal is said**: the machine log (§4.4), the ledger's *last* per kind, the card's *last*, the look's report line, and
   the list's kept units with their reasons.
5. **What a clean would remove is seen before it runs**: each kind's *next* on the card; the press listed first, its second press
   sending exactly the listed units, each judged again right before it goes; `daoris-driver space free` without `--yes` lists and
   changes nothing.
6. **One remover per kind.** The pass and the press call each kind's own act, so the guards a kind has are the ones it keeps; the slim
   and the browser's caches, the two new acts, are written in the purge's and CACHECLEAR1's shapes.
7. **A failure keeps.** A read that fails keeps what it would have judged (D88's *unread*); a delete that fails leaves its path and is
   said once; a ledger that does not read is measured again, never taken as empty.

## 7. Rejected

- **Deleting a tree's folder at the move, whole.** The fourteen days hold a session's own scratch and a write that raced the move
  (D88's AUTOTIDY1); slimming keeps both and drops the rest.
- **Taking every ignored path as made again.** A `.env`, a local config or a session's prepared requests (TREEKEEP1's case) are
  ignored and are the person's; only a shared build folder of the fixed list goes.
- **Slimming the folders already set aside** by reading their `.gitignore` files without git: a guess, since git's record of each is
  gone; they go by their date or the person's press.
- **Keeping git's own files in the folder** for the person to look at: about 61 MB a folder, 52 GB at the 2026-10-20 rate, for what
  `git worktree add` at the recorded commit makes again.
- **One size limit for the whole Daoris folder, oldest first**: it would remove what cannot be made again by size alone.
- **A floor that removes what cannot be made again**: a full disk is the person's to answer for their own records and scratch; Daoris
  frees what it can make again and says the rest.
- **Slimming a live tree's build outputs**, even an idle one's: it is a session's workplace (D51), words may go on in it (D137), and a
  session resumed there would rebuild in its turn; the clean-up and *Discard* stay its doors.
- **An age for finished history on by default**: the history clearing design §7's reason holds; it is the person's to set (§3.6).
- **Setting the agents' own retention** (Claude Code's `cleanupPeriodDays`) from Daoris: the account's home is the tool's (D49 §4).
- **Bounding the browser's caches by a switch at the engine's start**, not chosen yet: the browser is the application's executable,
  whose command line Chromium reads (`EngineBrowser.cs:106-112`), so a cache-size switch could be handed to it beside Daoris's own
  (`:121-132`). Which of its caches such a switch bounds was not measured (its code cache was 301 MB beside a 364 MB HTTP cache), and
  clearing below the floor and by the press needs none. Measured first, it may replace the floor's third act.
- **The machine log kept past 30 days**: each process prunes at its open by its own constant, and the service would have to read the
  driver's choices; 30 days stays the ceiling.
- **A walk of the whole home at each pass**: about 2.2 million files on the install, 36 to 100 s, for numbers the ledger keeps.
- **A file of its own for the rules** (`keep.json`): the standing choices already have one file, its twin, its terminal family and
  its Ask Daoris door.
- **A word of its own for each act**: *clean up* stays the trees' and branches', *clear* the records' and the downloads'; the
  umbrella press is *Free up space…* and calls them.
- **Holding new sessions while below the floor**: a scheduling choice for the cap, not cleanup; a session's own failure says the
  disk is full.

## 8. What this amends, once accepted

- **D88's AUTOTIDY1 note**: a folder the look moves aside drops at once what git says can be made again (§5), and the purge's
  fourteen days are the person's to set; its guards, the move, the purge's checks and *Said once* stand.
- **D102's TREEKEEP1**: its *shared build folder* rule is also the slim's at the move.
- **D88's CACHECLEAR1 note**: unused versions also go by a rule the person sets, and below the floor; its judgement, the rename first
  and the press stand; the terminal twin is `daoris-driver space free --kind downloads`.
- **The history clearing design §7**: only where the owner says yes to §3.6.
- **D172's layout §4**: the logs' row keeps 30 days *or fewer, as set*; `data/` gains `keeping.json`.
- **The This Daoris design**: §3.1's *nothing offers to delete it*, for `webview2/` and `browser/profile/` alone, which the press now
  offers (§3.3); §9's *editing settings on this page*, for the keep rules, which have no other place, so the page is their one door.
- **The machine log design §4**: the five events of §4.4.

## 9. Build rows

Each is a feature a person can see working (D187), end to end across its lanes. They are named KEEP1, not CLEANUP1a and on, since
CLEANUP1b and CLEANUP1c already name D173's and D102's notes and two fix-log entries of 2026-10-17, and CLEANUP1 is that id's third
use. Each runs its own tests and the quick checks; the merge and the CI beside run the rest (D180).

- **KEEP1a — a tree set aside keeps only what cannot be made again** (driver). §5: the judgement in the last look, the slim after
  the move, the ledger's `setAside` entries with the three twins' `keeping` entry, `tidied.slimmed`, the purge handed its days from
  the table, `work state`'s recovery naming the commit. Proof: a `Process` class over real git, seen failing first: a tree with a
  shared `node_modules` (and a junction in it to a folder outside), shared `bin/`, a `dist/` only it built, a `.env`, `local/notes`
  and tracked files, moved aside, leaves the `.env`, `local/notes` and `dist/` with git's file and nothing else, the junction's target
  untouched; a build folder holding a tracked file kept; a path written after *judged at* kept and the folder then kept by the purge;
  a folder git would not let go of put back unslimmed; a stray and a cut-off opening never slimmed; a slim stopped midway finished by
  the next pass.
- **KEEP1b — This Daoris shows each kind's size, rule, next and last clean** (driver, modules, web). §2.1's table, §2.3's ledger and
  §4.3's walk per kind, `SYSTEM_SPACE`, `daoris-driver space`, the *Disk space* card with every state of §4.2 but the rules' editor,
  the keys and the glossary's two terms, WIRING-SIZES-LIVE1's need for these kinds. Proof: the read's tests (a ledger's size answered
  at once, a kind never measured, a partial count, each kind's *next*), the terminal's text and `--json` table, the card's tests and
  stories at wide, half and narrow in both languages.
- **KEEP1c — the person sets each rule, on the page and at the terminal** (driver, modules, web, cli). §2.2: `driver.json`'s `keep`
  with its twins and keeps table, `SET_KEEP`, `daoris driver keep` handed to the host, the bounds' refusal, the row's editor (R3), Ask
  Daoris's `setting` door, the purge's and the log's days read from it. Proof: the both-ways modules test over every `keep` choice,
  the keeps table's row on a file neither writer wrote, the refusal under the editor, the coverage row.
- **KEEP1d — the press frees space, listed first** (driver, modules, web). §3.3: `SYSTEM_SPACE_FREE`'s plan and press,
  `daoris-driver space free`, the groups that go early (set-aside folders before their date, the browser's caches, an earlier build's,
  a failed update's), R2's line, `space.freed` and `space.kept`, the `Exempt` row. Proof: a plan that changes nothing; a press sending
  only the listed units, one changed since kept and counted; a held browser cache and a held earlier-build folder kept as in use; the
  terminal's exits.
- **KEEP1e — below the floor, Daoris frees what it can make again and says the rest** (driver, modules, web). §3.2: the free-space
  read for the folder's own path, the default and the person's floor, the pass's three early acts, the row in *What needs you* with
  the press as its door, `space.low` and `space.ok`. Proof: a stand-in free-space read below and above the floor; the three acts run
  in order and nothing else; said once, and cleared 5 GB above; a mounted folder's volume read, not its drive's.
- **KEEP1f — unused downloads go by their rule** (driver). §2.1's `downloads`: the `unused` record from the pass, the daily removal
  through CACHECLEAR1's judgement. Proof: `DownloadClearingTests` rows (first found unused, removed after its days, a pin written
  meanwhile keeps it and clears the record, `never` keeps it, a version a program runs from kept as in use).
- **KEEP1g — an opinion's packet goes with its quest's clear** (driver). §2.1's gap: the history clearing design §2.2's row. Proof: a
  clear of a quest with a packet removes it; a packet of an open quest stays.
- **KEEP1h — finished history by an age the person sets** (driver, modules, web; only if the owner says yes to §3.6). The daily clear
  through `HistoryClearing`, every refusal holding, `history.cleared`'s `door: rule`. Proof: a unit past its days cleared and one with
  a refusal kept and said; `never` clears nothing.

## 10. What this document's gate does not cover

Documents only; nothing is built. The statements about today were read from the code at `5e8caed81`, and none was run. The sizes are
one install's on one day, read with .NET's enumeration and not compared with Explorer's; the owner's 36.7 GB and this reading's 37.4 GB
differ by a day's moves and the counting. No test shows `status --ignored=traditional` naming each wholly ignored folder as the slim
relies on, `GetDiskFreeSpaceExW` answering a mounted folder's volume, a running browser refusing its caches' rename, or the share of
the set-aside build folders the checkouts hold; KEEP1a, KEEP1e and KEEP1d prove them. Claude Code's `cleanupPeriodDays` is read from
its documentation. The floor's 20 GB and 10%, the downloads' 7 days and the 5 GB margin are judgements, each a default the person
sets, to be read again on the install after a week of KEEP1a.

## 11. The reads (D191)

Not read yet. Codex reads every design; the steward checks each finding against the code before it is taken, keeps the table of the
reads and their verdicts here, and says whether Fable read and, if not, why the design changes no foundation.

| # | Reader | Finding | Checked at | Verdict | Taken in |
|---|---|---|---|---|---|
