# Keeping: each kind of thing Daoris writes kept by one rule the person sets and sees, a set-aside tree's package folders dropped once it has settled, and the rules run by the look, a free-space floor and a press (accepted, 2026-10-21)

**Standing:** accepted under D195 (CLEANUP1), settled under D191 on 2026-10-21 after three rounds, each finding checked against the
code by the steward (§11). Codex read the first round (fourteen blocking findings and eight others, all taken in round 2). Since the
design meets D191's foundation threshold (durable records, `data/keeping.json` and a manifest beside each set-aside folder it slims;
removal nobody presses; a lock between processes; decided rules other designs rest on, D88's fourteen days and CACHECLEAR1's
judgement), Fable read §3 and §5 of round 2 (thirteen findings, two blocking, all taken in round 3). Nothing here is built; its rows
(§9) build it. The owner decided §3.6 (D195's note of 2026-10-21): finished history may also go by an age the person sets, the press
alone by default. Nothing else publishes, spends an account, or removes anything that cannot be made again without the person's
rule or press.

Read for it: the row CLEANUP1 in `TASKS.md`; D88 and its notes (AUTOTIDY1 to CACHECLEAR1, lines 37-350); D102's TREEKEEP1 (278-302)
and CLEANUP1 (360-391); D173's CLEANUP1 note (1595-1684); D172 and the install layout design (§2 to §4); the This Daoris design (§3.1,
§6, §9); D185's UI/UX design (R1, R2, R3, §3.1) and D187; D153 and the history clearing design (§1.3, §2, §5, §7); D194 (the
`daoris driver` family handed to the driver's program); `.claude/knowledge/twins.md` (lines 37-52), `setting-doors.md`,
`bridge-presses.md` (row 11, the pipe's census), `ref-removal-transactions.md` and `git-ref-absence.md`. Every statement about today
was read from the code at `5e8caed81`, and read again at `1657bd997` for round 2 (the trees' files unchanged between) and, where
Fable's read touched it, at `2cdc2e8c2` for round 3, and names its file and line; none was run. The install was measured read-only
on 2026-10-21 (§0.2).

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
| Downloaded versions nothing uses are listed and removed by the person's press on This Daoris, judged again, moved aside first; no rule by age and no terminal twin. Tools also deletes one version at the person's press, in place, and may leave part of it on a failure | `DownloadClearing.cs:62-87`; CACHECLEAR1's archive outcome; `ToolsRoutes.cs:189-199`, `ToolInstall.cs:315-349` |
| The machine log keeps 30 days and stops a file at 20 MiB; each process prunes at its open | `MachineLog.cs:37, 40, 80-86, 145-152` (driver), `MachineLog.cs:32` (service) |
| Finished history is cleared only by the person's press: *nothing clears by itself* | the history clearing design §7 (lines 451-463) |
| This Daoris's sizes are walked only when asked, in the background, kept for the process, bounded at 400,000 files and 60 s | `WiringSizes.cs:10-14, 47-51` |
| A recovery ref a branch or a tag of the person's holds goes after 30 days; one nothing holds stays until the person drops it | `RecoveryRetention.cs:22`; D102's CLEANUP1 note |
| Ask Daoris answers the downloads' press with an exemption, a discard that stays the person's | `HelpCoverageTests.cs:930-934` |

### 0.1 What it rests on

Every kind with a rule today has an act that judges and removes it (the tidy's purge, the clean-up, CACHECLEAR1, the clear, the
log's prune), and each judges again before it acts; the downloads alone have two, CACHECLEAR1's and Tools' delete of one version,
which deletes in place (`ToolInstall.cs:338-346`). What is missing is: one place where each kind's rule is set and seen; a reason to
run the removals of what can be made again before a disk fills; and a judgement, at the moment git can still give it, of which of a
tree's folders a producer makes again whole. The design adds those, and makes the downloads' two acts one. Every removal it runs is
an existing act, or a new act built in that shape: listed, judged again right before it goes by what is known of the thing's use,
moved aside first where a program could hold it. A rename's success is never taken as proof that nothing uses a thing (§6).

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

The home held about 2.2 million files, past `WiringSizes`' 400,000-file budget, so This Daoris's sizes on this install end partial;
one walk of `.tidied` took 36 to 100 s by .NET's enumeration. Neither was looked at on the window.

**What each part of a set-aside folder weighs** (round 2, from the measured folder above, which is this repository's): its
`node_modules` folders 254 MB (52% of its bytes) and at least 17,069 of its 22,198 files (77%); `bin/` and `obj/` about 170 MB (35%),
in few files; git's tracked files about 60 MB (12%) in 4,501; what the session wrote of its own, a few KB. The 73 folders' split
between these was not measured apart, only their 90% under a build folder's name. The home's `data/` held about 43 GB, 87% of it set
aside. **Were a day like 2026-10-20 repeated** (62 trees of 454 MB set aside), fourteen days would hold about 385 GB. With the
`node_modules` folders dropped, about 170 GB; with every build folder dropped, about 52 GB; with git's files too, a few KB. §5 drops
the first alone: it is the only part whose producer discards it whole, so the only part known to hold nothing a person keeps, and
the rest is held by the person's days, the press and the floor's word (§3, §7).

## 1. Everything Daoris writes, and the rule each kind has

A **kind** is what a person decides about together: one rule, one row. Each row names its entries in D172's table
(`DaorisHome.cs:326-394`), who writes it and what reads it back, its rule today read from the code, and what may remove it. **No
rule** marks a kind nothing removes by age, size or event today. Sizes are §0.2's.

### 1.1 `work/`: where agents work

| Kind | Entries | Written by → read by | Rule today | What may remove it |
|---|---|---|---|---|
| Live trees | `trees` (`trees/<ws>/<repo>/<tree>`) | `SessionTrees.OpenAsync` (`TreeContext.cs:42`) → sessions, git, the clean-up | moved aside by the look once its branch's work is on the line (D88's AUTOTIDY2a), never by age; one a session runs or waits in, words wait on or a review shows from stays | the look (moves aside), the clean-up's press (`trees clean`), *Discard*, abandon (D132) |
| … their build outputs | ignored folders of `TreeScratch`'s list | the session's build or install; TREEDEPS1's copy at the open | **no rule**: they live and go with their tree | with their tree |
| Set-aside trees | `trees/.tidied/…` | the look's move (`TreeSetAside.MoveFolderAside`, `:206`), a folder that is no tree and a cut-off opening (`TreeStrays`), an opinion's copy and a replay's tree (`PassLeftovers`) → the purge, the work inventory | 14 days after the move, where nothing was written since and no link outside a build folder is in it (`TreeSetAside.cs:254-283`); one the purge keeps is a blocked unit | the purge only; nothing early (§5 adds the slim of its package folders) |
| Rooms | `help-room`, `help-rooms`, `intake`, `brainstorm-rooms`, `opinion-packets`, `usage-read` | Ask Daoris's room (`HelpRoom.cs:79`), intake's (`IntakeRoom.cs:62`), a brainstorm pass's (`BrainstormFiles.cs:57`), a reviewer's packet (`OpinionPackets.cs:44`) | a brainstorm's rooms by `RemoveRooms` and with its records at its clear (`BrainstormFiles.cs:250-262`), a turn's room made afresh (`:242`); an intake room by a workspace's clear that leaves it no ask (D153 §2.2); Ask Daoris's rooms and an opinion's packets: **no rule** (no delete in `OpinionPackets.cs`) | the clear; the brainstorm's own |
| Kept files | `quest-files`, `ask-files`, `session-files`, `proof` | the service's `QuestFiles` and `Asks`, the driver's `ChatFiles`; `proof` has no writer | with their record's clear or delete (D153, D95, D126) | the clear, the delete |

### 1.2 `data/`: records and choices

| Kind | Entries | Written by → read by | Rule today | What may remove it |
|---|---|---|---|---|
| Conversations and transcripts | `sessions` (`<id>.events.jsonl`, `.log`, `.harness.json`, marks) | `SessionEvents`, the four starts, `HarnessConversations` → the page, the trace, a go-on | **none, by design** (the history clearing design §7, D126 §11) | the person's *Clear history…* and a conversation's *Delete…*; Ask Daoris's by `help delete` |
| The service's records and index | `knowledge` | the service (`HostComposition.cs:46`) | its records by the clear; its index rewritten by each index; the file is never compacted (no `VACUUM` in the service's code) | the clear, rows only |
| Bounded records | `usage`, `landings`, `session-branches`, `workflows` (units, closures), `usage-history`, `opinion-dues`, the automatic landings, `notices` (NOTIFY2f, D196) | the driver; `notices` by any process on the home under its keep lock, read by each process's `notices` slice | each its own: usage's newest 500 (`Usage.cs:48`); the notices, seven days and at most 200, pruned at each write (`Notices.cs`); the landings' and branches' traces, the newest 50 per repository (`LandedBranches.cs:320`, `SessionBranches.cs:85`); a unit's settled 50 and history 200 (`NodeRecord.cs:166-168`); the closed automatic landings and opinion dues, 200 (`AutoLanding.cs:286`, `OpinionDues.cs:16`); usage history, 90 days and 64 MiB (`UsageHistory.cs:105-109, 134-137`) | their writers |
| Installed plugins | `plugins` (with `.trials/`) | `PluginInstall` | **no rule** but the plugin's removal (D119): its hook stopped, its folder moved aside whole, then removed (`PluginsRoutes.cs:308-320`) | removing the plugin |
| What a plugin kept | `plugins`' `.data/<id>`, `plugin-state` | the plugin | **no rule**: the plugin's removal keeps it and names it, the person's to throw away (`PluginsRoutes.cs:310-311`) | the person |
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
| Agents' and tools' versions | `toolchain`, `tool-versions` | **no rule**: the person's *Clear unused downloads…*, and Tools' delete of one tool's version (`TOOLS_DELETE`, `ToolsRoutes.cs:189-199`), which calls `ToolInstall.Delete` and deletes in place, refusing the version Tools runs (`ToolInstall.cs:315-349`). A download's staging (`<version>.part`) a crash left stays until that version is downloaded again (`ToolInstall.cs:57`; the alone path design §0) | CACHECLEAR1's press; Tools' delete |
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
| An earlier build's | `webview2/`, `browser/profile/` | **no rule**. `webview2/` is shown as an earlier build's (`WiringSizes.cs:68-73`, `SystemWiring.cs:446-454`, the This Daoris design §3.1); `browser/profile/` lies inside the `browser` folder, whose other entries are this build's (`DaorisHome.cs:355-365`), so it is counted under that folder and has no row of its own. Nothing offers to remove either | the person, by hand |
| The person's | `salvage/`, and any name the table does not hold | never touched (the history clearing design §1.3) | the person |

### 1.7 Outside the home

| Kind | Where | Rule today | What may remove it |
|---|---|---|---|
| Daoris's own refs and records in a registered repository | `refs/heads/daoris/*`, the landings' branches, `refs/daoris/discarded/*`, `.git/worktrees/<tree>` | the clean-up's proof (D88, D102); a recovery ref a branch or a tag holds after 30 days, one nothing holds until the person drops it (`RecoveryRetention.cs:22`, `work recovery drop`); a tree's record with its folder | the look, the clean-up, the person |
| What Daoris's sessions left in the person's own tool home | Claude Code's `projects/<flattened folder>` (OWNSPACE2) | listed, never removed (`ToolHomeLeftovers.cs:24-27`) | the person, by hand (OWNSPACE2e) |

The registered checkouts, their own build outputs and their own refs are the person's and appear nowhere in this design.

## 2. Each rule in one place

### 2.1 The kinds: one table in the driver library

`KeepKinds`, in `Daoris.Driver.Platform`, **describes** each kind and runs nothing: its id, the D172 entries it covers by name,
whether it can be made again, its default rule, what the person may set and within which bounds, the catalogue key of its words and
whose rule it is where it is not Daoris's. **Each kind's removal is a typed operation in the driver library** (D194 point 4): typed
input, the facts it judges by, the removal, and its result's fields, called by the pass, by the route and by the terminal alone
alike, so the three share one implementation and one set of guards. The table names each operation; it never calls one. The page's
catalogue holds each id's words, and a census test holds the two to each other, as WIRING1d's does for every setting. A kind is
added to the table in the change that adds the entry. **One effective policy** (the table's defaults under the person's `keep`,
§2.2) is the only reader of a kind's days: every place that says or acts on the set-aside folders' days reads it, today's six
readers of the constant `BranchTidying.SetAsideFor` among them (`BranchTidying.cs:111, 248, 317`, `WorkInventories.cs:59`,
`WorkInventory.cs:610`, `TreesConsole.cs:108`).

| Kind | Made again? | Default | The person may set | Its remover |
|---|---|---|---|---|
| `set-aside` | its package folders, a `node_modules` beside a `package.json` git tracks, yes (§5); the rest, git's own files, `bin/` and `obj/` included, no | 14 days; its package folders an hour after the move, where nothing was written in it since | 1 to 90 days; `whole`, which keeps every folder whole as today | the slim (§5), the purge |
| `downloads` | yes | an unused version goes 7 days after a pass first finds it unused, counted from the start of its current unused stretch (§2.3) | 0 to 365 days, or `never`: the press alone, below the floor too | CACHECLEAR1's act, which Tools' delete of one version takes too (§3.5) |
| `browser-cache` | yes | the engine's own bound; cleared below the floor while the browser does not run | — | new: the cache paths of `browser-engine` in §3.2's table, alone |
| `logs` | no | 30 days | 7 to 30 days: the processes' own 30 stays the ceiling, since the service would otherwise read the driver's choices | the log's prune; the pass's daily prune at the person's days |
| `history` | no | `never`: the person's clear alone | `never`, or 30 to 3,650 days (§3.6, decided by the owner) | the clear |
| `trees`, `records`, `rooms`, `plugins`, `sign-ins`, `program`, `service`, `running` | — | their own, §1 | nothing: shown, with whose rule it is | their own |
| `left-over` | — | never by itself | nothing | new: the press alone (§3.3), `webview2/` and `browser/profile/` each by its exact path |
| the floor | — | the smaller of 10% of the volume and 20 GB | a size from 1 GB to half the volume, a share from 1% to 50%, or `off` | — |

**What it leaves as it is.** A download's staging a crash left is PUSH1f2c's, which names each staging by its process and sweeps it
once that process is gone (the alone path design §5); this design adds nothing there. An opinion's packet keeps no rule (§7: it is
an opinion's, not a quest's, and the rooms together are under 1 MB on the install).

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
- **Each choice arrives with the act it governs** (§9): the set-aside folders', the downloads' and the logs' in KEEP1d, after their
  rules already run at their defaults; the floor's in KEEP1f with the floor; history's in KEEP1g with its daily clear. No build shows
  a rule it does not run.

### 2.3 What is kept durable, and nothing more

Codex's smaller shape (§11, 6.1): only what cannot be read again from the disk is kept. A size is not (§4.3).

**`data/keeping.json`.** A new home entry, `keeping`, in `data/` (D172's test 7: a record of the rules' runs), added to the three home
twins' tables at once. It is a machine record: never synced, no route answers it raw.

```
{ schema: 1,
  last:   { <kind>: { at, door: look|floor|press|terminal, run, removed: { count, bytes }, kept: { <why>: count } } },
  unused: { <agent|tool>/<name>/<version>: <first found unused> } }
```

- **One way to write it**: `JsonKeeping.Save` (`JsonKeeping.cs:53-71`), the file's keep lock held from its reread to its rename, each
  writer's edit the key paths it changed, so the pass, a press's run and a terminal acting alone each keep the others' entries. Beside
  a desktop, a terminal's run holds no lease that would order them (D194 point 3), so the lock is what does; an atomic rename alone
  lost the other writer's change (CONFIGLOCK1).
- **A file that does not read is never written over** (`JsonKeeping.Save` refuses it): the pass says so and writes no `last`, and an
  `unused` it cannot read is no first-unused time, so no download goes by its days until the file reads again (§3.5).
- **First unused is the start of the current unused stretch** (Fable F2.1). A pass that finds a version pinned, used or named clears
  its `unused` entry, so the next pass that finds it unused records a new time. Kept once and never reset, a version found unused at
  T0, pinned at T1, started on at T2 and unpinned at T3 would read at T4 as unused since T0, before the session's start, and be
  renamed under it; an agent run through npm's shims does not refuse that (`HarnessSettings.cs:509-516`). A pin and an unpin
  between two passes leave no trace here, which is why a running session's own resolved binary is the guard and its start the rule
  only for a record without one (§3.5).
- Every field is added, never renamed (a stored shape). A reader meeting none reads every kind as never cleaned.

**A manifest beside each folder the slim takes** (§5): `trees/.tidied/<ws>/<repo>/<name>-<moment>.slim.json`, the exact paths
judged before the move, each one's state and when its removal ended, written by the look before the move and by the slim after. It
lives and goes with its folder.

## 3. When it runs

### 3.1 The look's pace

A pass, `Keeping`, runs in the tidy's own task in the watch (`DriverWatch.cs:866`), after the tidy's repositories, in the place the
purge runs today (`BranchTidying.cs:111`), so it holds no repository's hold (`AutoTidies.cs:286`) and a start never waits on it. It
runs only where the tidy runs: the shell's watch and the headless `drive`'s, never a one-look run or a gate's driver (D88's AUTOTIDY1
judgement). Each pass:

1. reads the free space (§3.2);
2. tries the run lock (below); held elsewhere, it reads and removes nothing this pass;
3. runs the purge at the person's days, its read knowing what the slim removed (§5);
4. runs the slims due (§5), within the pass's budget;
5. once a day (the first pass 24 hours after the last daily one, and the first after a rule changed): unused downloads past their
   days, log files past their days, finished history past its days where set (§3.6);
6. at most one bounded measure per kind a day (§4.3).

**One run at a time, bounded** (Codex 3.2). A removal of any kind, the pass's, a press's or a terminal's alone, runs under the home's
run lock, `locks/keeping`, in `TreeLock.TryOpen`'s shape (`TreeLock.cs:78`): opened at once or not at all, never `RecordLock`'s
ten-second wait (`RecordLock.cs:23`; Fable F3.1). Its handle is the lock, let go with its process, so nothing stale blocks. Its
holder writes `{ pid, since, door }` to a sibling file it owns, `locks/keeping.holder`, as the keep lock writes its own
(`KeepLock.cs:78`), and removes it at the run's end, so a refusal names the holder and the card can say a terminal's run is under
way; a holder's file whose process is gone is a run a crash cut short (§3.3). It is tried, never waited for: the pass skips; a
press while a run is under way in the same process joins it, and from another process is refused, naming the holder. A pass
removes for at most 60 seconds, hearing a stop between paths and never inside one path's delete; a path whose removal failed for the same reason as at its last try is tried again the next day, not
each pass, and said once. The daily downloads' rule lists the versions without measuring each (`DownloadClearing.cs:143-147, 160`
measure every one) and measures only those it removes.

### 3.2 The free-space floor

**Measured** by the operating system's free-space read, the bytes this account may use, for the Daoris folder's own path and for
each entry the floor's acts free from: the trees home (`trees`), the downloads (`toolchain`, `tool-versions`) and the browser's
profile (`browser-engine`). The trees home may sit on another volume through a junction, so the home's read alone could judge the
wrong disk (Fable F4.3). On Windows `GetDiskFreeSpaceExW` on the folder answers for the volume that holds it, a mounted folder
included (a drive letter's read would not); `statvfs` elsewhere. Each volume's floor is computed from its own size, each act runs
where its own entry's volume is below it, and a volume two entries share is said once, compared by the identity the operating
system gives it. Read at each pass; one read took 0.3 ms on the install. Never a walk.

**Default: the smaller of 10% of the volume and 20 GB.** On the install's 1,024 GB volume that is 20 GB; on a 128 GB one, 12.8 GB.
**It is a trigger, not a reserve**: the free space at which Daoris starts freeing early what it can make again, and tells the person
what it could not. It is not sized to hold a day like 2026-10-20 (27.5 GB set aside, about 12 GB with the package folders dropped),
which no fixed margin on a small disk could; the first round's claim that it did was wrong (Codex 3.3). The person sets a size from
1 GB to half the volume, a share from 1% to 50%, or `off`; a value outside is refused (`KEEP_OUT_OF_RANGE`).

**Below it**, the pass runs at once, at most once an hour while below, these and only these, each through its own act and with
every guard of its own (§3.5):

1. the slims due (§5): the hour's settle and the read of nothing written since the move still hold;
2. unused downloads past the hour's `Settling`, their days ignored, **but never one whose rule is `never`**: the person's `never` is
   the press alone, below the floor too (Codex 3.1); every use guard of §3.5 holds;
3. the browser's caches, **once in each stretch below the floor**, from `space.low` to `space.ok`, not each hour (Fable F4.1): caches the
   browser fills again as it runs would otherwise be cleared each hour the disk stays low, for little beyond what the first clear
   freed. **Only where the
   shell's browser host says its browser is not running**, holding that host's start gate
   through the clear so no browser starts meanwhile (`EngineBrowserHost.cs:41-42, 107-113`). The browser closes with its shell
   (`:25-27`), but whether one outlived a shell that stopped cannot be asked with no shell, so with no Daoris running the caches are
   kept and said. Only these paths, relative to `browser-engine`'s root, its active profile being `Default`
   (`EngineBrowser.ProfileDirectory`, `EngineBrowser.cs:57`): in the profile, `Default/Cache`, `Default/Code Cache`,
   `Default/GPUCache`, `Default/DawnGraphiteCache`, `Default/DawnWebGPUCache` and `Default/Shared Dictionary/cache`; at the root,
   `component_crx_cache`, `GrShaderCache` and `ShaderCache` (round 1's nine names, each now placed). Each is checked to lie under the root with no link on
   the way, moved aside by one rename, then removed; a path not in the table is never touched, nor its cookies, storage or
   preferences, nor the window's profile, which is the UI kit's. The table is named from Chromium's layout; KEEP1e holds it to the
   install's profile, read without writing, before the act exists (§10).

Still below after these, it is **said, never taken**: a row in *What needs you* names the free space, the floor, what Daoris freed,
and what the person's press could free and how much (§3.3), with the press as its door (`NeedsAPerson`, listed only with an act:
`person-waits.md`); the machine log writes `space.low`. Said once each time the free space falls below, and cleared once it is
above the floor by the smaller of 5 GB and 5% of the volume (`space.ok`). With the floor's bounds, that line is at most 55% of the
volume, so it can always clear.

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
   list and was kept*). A set-aside folder before its date goes whole, with its manifest; the browser's caches only as §3.2 says.
3. **A run, with its id** (Codex 5.3). The press answers at once with the run's id, taking the run lock (§3.1); one asked while a run is
   under way in the same process answers that run's id. The run's state, each kind waiting, running or done with what it removed and
   kept, is part of `SYSTEM_SPACE`'s answer (§4.4) for the current or last run of the process, and its end is the news under the
   `system` scope and each kind's `last` in `keeping.json`. The card shows the run with its count and its time, then its end (R2),
   and a page opened again reads the same run. The terminal reads it until it ends (§4.4). **A run a crash cut short** writes no
   `last`, so the card's `last` may be older than the folders show (Fable F3.3). Its holder's file, left naming a process that is
   gone (§3.1), is read as *a run stopped before its end*, with its start and its door, and said on the card until the next run
   ends; what it removed shows in the next walk's size, and a slim's paths in their manifest (§5).
4. **Whose press it is** (Codex 4.2; `bridge-presses.md` row 11). The list changes nothing and is `Open`; the removal is the person's
   delete, `Person`. A terminal's removal over the pipe waits for the person's Confirm in the window (RPC1c), worded by its act. With
   no Daoris running, from an environment carrying `DAORIS_SESSION_ID`, the removal is refused, exit 1, and nothing is done (D194
   point 5): `--yes` is the person's word at their own terminal and grants a session nothing, and the run lock orders runs without
   standing for anyone. The list runs anywhere.

### 3.4 What never runs by itself

- Anything that cannot be made again, or is not known to be, before its rule's time: a set-aside folder's own files, git's tracked
  files, `bin/`, `obj/` and every build folder but its package folders, before its days (§5); a log within its days; finished history
  without the person's age (§3.6).
- A live tree, or its build outputs (§7).
- Anything in `profiles/` but the browser's caches (§3.2); the agents' homes; the window's profile.
- `bin/`, `app/`, `update/failed/`, an earlier build's and the person's names (§1.6).
- Anything outside the home (§1.7): the person's checkouts, their refs beyond D88's and D102's rules, the person's tool home.

### 3.5 What is refused while it is used

**A rename's success is no proof that nothing uses a thing** (Codex 2.6). A file opened with delete sharing does not refuse it, as
the machine log opens its own (`MachineLog.cs:299-300`); an agent run through npm's shims holds no file of its version open
(`HarnessSettings.cs:509-512`), and a version's folder renames under it. So each kind says what it knows of its use, and a refused
rename only adds a keep:

| Kind | Kept while | Known by |
|---|---|---|
| Live trees | a session running or waiting names it, words wait on it, a review shows from it | `BranchTidying.InUseAsync` (`BranchTidying.cs:133-142`) |
| Set-aside trees | something in it was written after its move; a link outside a build folder; a delete refused. For the slim also: within the hour after the move; `whole`; a manifest not at `moved` (§5) | `TreeSetAside.Kept`, `purge-failed.json`, the manifest. A tree moves only once no session, review or words hold it, read at the move inside the repository's hold |
| Downloads | pinned, used, named, unreadable, within the hour; `never`, below the floor too; **a running session resolved a binary inside it**, or, for a session record older than that field, the session started before the version was first found unused; the rename refused (`in-use`) | `DownloadClearing.Judge` (`DownloadClearing.cs:211-237`), read again under the use files' keep locks (below); the home's live sessions (`ServiceClient.ActiveSessionsAsync`, `ServiceClient.cs:335`), each with the binaries its start resolved, from its record (below); `keeping.json`'s `unused` (§2.3) |
| The browser's caches | the browser runs, or no Daoris runs to say whether it does | the shell's browser host, its process and its start gate (§3.2) |
| Logs | today's file | the date in its name. Each process writes only today's file and opens it with delete sharing (`MachineLog.cs:296-300`), so a delete is never refused by its writer: the date is the guard, not the refusal |
| History | open work, a live session, what waits on the person, unpushed moves | the history clearing design §1.2 |
| An earlier build's, a failed update's | no entry of this build names it; an update not at rest | the table (`WiringSizes.EarlierBuild`, and `browser/profile/` by its path); the update's state; a refused rename keeps it too |

Each kept unit is said with its reason, in the list and in the log. **Tools' delete of one version** (`TOOLS_DELETE`) takes
CACHECLEAR1's act from KEEP1b: judged the same way, moved aside first, so one removal of a version has one set of guards.

**The downloads' use, judged under the use files' locks** (Fable F2.2 to F2.5):

- **Which files, in which order.** The keep locks of the harness file the roster reads, at `HarnessSettings.ResolvePath(home)`
  (`HarnessSettings.cs:104`), which `DAORIS_HARNESS_CONFIG` may name elsewhere, never `harnesses.json` by name (F2.4); `tools.json`;
  and `driver.json` at the path the loop resolves, as `DownloadUses.Read` takes it (`DownloadClearing.cs:45-51`). Taken in that
  order, so a pin or a use written meanwhile waits and is then seen, where today a pin can land between `:176` and `:186`.
- **Held per version, from its judgement to its rename only**, never across the removal that follows, so a person's pin or
  `SET_KEEP` waiting on one is never refused at the keep lock's ten seconds (`KeepLock.cs:35`; F2.3). A pin's writer looks again
  under its lock that the version's folder is there, refusing a pin of one gone meanwhile, where today the next start would fall
  back to `PATH` and say so (`HarnessSettings.cs:496-498`).
- **The guard binds only the writers that take the lock** (F2.2). Both writers of `driver.json` take it (`DriverConfig.cs:891`,
  `driverconfig.ts:882`). Both of `tools.json` took it with KEEPALL1c, and since PUSH1f2c (merged at `7f3d80593`) the file has one
  writer, the driver's, holding the lock from its read to its write (`Tools.cs:372, 392, 451-452`). The harness file's two take it on
  their keeping path, but each still writes wiring built in memory whole and unlocked (`HarnessSettings.cs:188-190`,
  `toolchain.ts:509`), the CLI's writer a second one until PUSH1f2e retires it. So KEEP1b comes after every writer of the three files
  is under the keep lock: after PUSH1f2e, whose `HarnessSettings.Change` takes every writer of the harness file through the lock (the
  alone-path design §1.4, §5). PUSH1f2c, the other row the steward's check named, is built; it reshaped `TOOLS_DELETE` and the
  download's staging and publication, which KEEP1b's act takes over as they now stand. A KEEP1b built before PUSH1f2e carries the
  whole-layout write under the lock itself, on both sides, with a proof that a whole-layout write waits.
- **What a running session may still run is the binary it resolved** (F2.5): at its start the roster resolves the agent's managed
  binary (`HarnessSelection`, `HarnessRoster.cs:1083, 1137-1143`), two for an ACP session, its adapter and the `claude` it is
  told to use, and the start records them on the session record, a machine-only field (`session-record-fields.md`). A version holding a binary a
  running session resolved is kept. A record older than that field keeps the start-time rule: kept while a session that started
  before the version was first found unused runs (§2.3).

### 3.6 History by an age (decided by the owner)

The history clearing design §7 (and D126 §11) decided that finished history never clears by itself: a record gone without a press is
one the person never saw go, and one install's 24 MB was met by one listed press. This design keeps that the default and offers an
age only the person sets: once a day, each workspace's finished units whose last move is older than the days are cleared through
the clear itself, every refusal holding (D153 §1.2), each said, the log's `history.cleared` with `door: rule`. Since it lets the
person's records go with no press at the time, it was asked of the owner (D191 point 3), who decided it on 2026-10-21 (D195's note):
the rule is the person's to set, its default the press alone with no age. KEEP1g builds it, its choice and its daily clear in one
row, and the history clearing design §7 is amended once D195 is accepted: *nothing clears finished history by itself unless the
person has set an age*.

## 4. This Daoris

### 4.1 The card

**Place:** Settings → This Daoris, a card *Disk space* after *Folders* (the This Daoris design §2). Its rows use R1's label track
(`SettingList`). Each kind is one `SettingRow`: the kind's name as the label; its terminal twin as the hint; the size with when it
was counted as the value; under it, the rule in words with *Change…* where the person may set it, and a line *next* and *last*. A
kind whose rule is not Daoris's says whose it is and offers no *Change…*. *Unused downloads* opens the Downloads row's list as it is
drawn today (`WiringDownloads`, `WiringDownloads.tsx:31`), each version with its date by the rule: the card draws no second list of
versions (Codex 6.1). Each *Change…* appears with the row that lets the person set that rule (§9), so a build never shows an editor
for a rule it does not run. A kind whose entry lies on another volume than the Daoris folder's says that volume's free space and
floor on its own row (§3.2).

```
 wide, Settings → This Daoris, Disk space
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Disk space ⓘ                                                                       [Free up space…]    │
│ Free space                     143.5 GB of 1,024 GB · keep at least 20 GB         [Change…]            │
│ daoris driver keep floor …                                                                             │
│ Set-aside trees ⓘ              37.4 GB · 73 folders · counted 10:42                                    │
│ daoris driver keep set-aside … kept 14 days; package folders go after an hour     [Change…]            │
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
│ kept 14 days; package folders go after       │
│ an hour                          [Change…]   │
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
| A run under way in another process | *A run from a terminal is under way, since …*, from the holder's file; *Free up space…* disabled with that reason (§3.1) |
| A run stopped before its end | *The last run stopped before its end, at …*, beside *last*, until the next run ends (§3.3) |
| A shell older than the page | *This window answers no disk space: it is older than this page*, and no card |
| A development run | the scratch root's volume, said as the development run's |
| Wide, half, narrow; en, 中文; light, dark | R1's track; stacked below 32rem of card |

### 4.3 Sizes without walking every file

1. **The free space is the operating system's read** (§3.2), at each pass and each open: never a walk.
2. **No ledger of sizes** (Codex 6.1). Round 1 had every remover, measure and download keep counters in `keeping.json`, a second
   accounting each writer had to keep true. Sizes are derived instead, through `WiringSizes`' own walk.
3. **A walk per kind**, never the whole home: `WiringSizes.MeasureFolder` over the kind's entries, bounded by `WiringBudget` per kind
   (400,000 files and 60 s, `WiringSizes.cs:10-13`) so one large kind never starves the rest, in the background, at most once a day
   per kind, on *Count* or *Measure again*, and at a run's end for the kinds it removed from, never on a page's open
   (`WiringSizes.cs:47-51`). Kept for the process, as `WiringSizes.Shared` keeps today's measure; after a start a kind reads *not
   counted yet* until its walk. Each kind's walk publishes as it ends, so the card fills in kind by kind: WIRING-SIZES-LIVE1's need,
   for the kinds this card shows. The set-aside kind's walk keeps each folder's size, which its *next* reads.
4. **What drifts** (a late write into a set-aside folder, a tree built in) is corrected by the next walk of that kind; the card says
   the time of the count, so a stale size is never presented as now. A run's own count of what it removed is said as the run's.

With §5's slim, a set-aside folder of this repository holds about 5,100 files rather than 22,000; the install's 73 would be about
0.37 million, within the kind's budget but near it, so a count that stops short says *at least*.

### 4.4 The read, the presses and the terminal

- **`SYSTEM_SPACE`**, a read in the `system` route domain beside `SYSTEM_WIRING` and `SYSTEM_DOWNLOADS` (`SystemRoutes.cs`,
  `bridge/system.ts`), its answer every field added and never renamed:

```
{ schema: 1,
  volume: { path, free, total, floor: { bytes, from: default|size|percent|off }, below, freedEarly: { bytes, at } | null },
  kinds:  [ { id, entries, bytes, files, measured, partial, partialWhy, counting,
              volume: { path, free, total, floor, below } | null,      // only where its entry's volume is not the folder's (§3.2)
              rule: { kind: days|never|own|proof|press, days, whole, default, whose },
              settable, next: { at, count, bytes } | null,
              last: { at, door, removed: { count, bytes }, kept: { <why>: count } } | null } ],
  run:    { id, door, started, ended, outcome: done|kept|stopped,
            kinds: [ { id, state: waiting|running|done, removed: { count, bytes }, kept: { <why>: count } } ] } | null,
  held:   { pid, since, door, gone } | null }                           // the run lock's holder elsewhere, or one a crash left (§3.1, §3.3)
```

- **`SET_KEEP`**, the standing choice's press in the driver's route domain, `{ kind, days | never | whole | default }` or
  `{ floor }`, answering the state as every standing choice does; its operation is `StandingChoices`' (D194 point 4).
- **`SYSTEM_SPACE_FREE`**: `{ plan: true }` answers the list of §3.3; `{ units, early }` starts a run removing exactly those and
  answers at once `{ run, joined }`, the run's progress then read in `SYSTEM_SPACE`'s `run` and its end the news under the `system`
  scope (§3.3).
- **The pipe's census** (`PipeCensus.Driver()`, `bridge-presses.md` row 11): `SYSTEM_SPACE` `Open`; `SYSTEM_SPACE_FREE` by a
  `PipeRule` on `plan` (`PipeCensus.cs:83-84`), the plan `Open` and the removal `Person`; `SET_KEEP` `Person`, a standing choice.
  `person.route.SYSTEM_SPACE_FREE` and `person.route.SET_KEEP` word the window's Confirm in both catalogues (`Confirmations.tsx`'s
  `WORDED`), and the verbs that send them are named in the driver library's `PersonPresses`.
- **The terminal**, D50's twin of each:

```
daoris-driver space [--json] [--measure]
    the free space and the floor; each kind's size, rule, next and last clean.
daoris-driver space free [--kind <id>]... [--early <group>]... [--yes]
    list what each rule takes now, and what could go early; with --yes, remove what the rules take,
    and with --early, that group too, then wait for the run's end, a line per kind as it ends.
    Exit 0 listed, or every unit removed; 1 something named was kept, or the person refused it in the
    window; 2 could not: a read failed, the run stopped, or Daoris stopped while it ran.
```

  With a Daoris running, `space free --yes` is its client (`InstanceDoor.PressAsync`, D194 point 2), confirmed in the window. Its
  answer is the run's id, not its end (`InstanceDoor.cs:232-234` hands the verb the answer as it arrives), so the verb then reads
  `SYSTEM_SPACE`, an `Open` read, each second until the run has ended, printing each kind as it turns done; where Daoris stops first it
  says how it ended is not known and that `daoris-driver space` says what remains, exit 2. Alone, it is a long verb: it holds no lease
  (D194 point 3), takes the run lock, which a desktop starting meanwhile finds held, and prints as each kind ends in its own process.
  `--json` prints `SYSTEM_SPACE`'s answer field for field, so the twins share one test table.
- **Ask Daoris**: `SET_KEEP` through the `setting` kind's door; *Free up space…* `Exempt`, in CACHECLEAR1's words: a discard that
  stays the person's own press, listed first, nothing in use removed. *The machine now* says the free space and the floor.
- **The machine log** gains, names and counts only, folders relative to the trees home (the machine log design §4): `tidied.slimmed`
  (`folder`, `removed`, `bytes`, `kept`), `space.freed` (`kind`, `count`, `bytes`, `door`, `early`, `run`), `space.kept` (`kind`,
  `why`, `count`), `space.low` and `space.ok` (`free`, `floor`).

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
| `settings.daoris.space.rule.slim` | value | its package folders go after an hour | 其依赖包文件夹一小时后移除 |
| `settings.daoris.space.next` | value | next: {{what}} on {{when}} | 下次：{{when}}，{{what}} |
| `settings.daoris.space.last` | value | last: {{when}}, {{count}} removed, {{size}} | 上次：{{when}}，移除 {{count}} 项，{{size}} |
| `settings.daoris.space.nothingYet` | value | nothing removed yet | 尚未移除任何内容 |
| `settings.daoris.space.now` | heading | What goes now, by your rules | 按你的规则现在移除的 |
| `settings.daoris.space.early` | heading | What could go early, by your word | 可由你提前移除的 |

Each rule's word, each *whose* (`settings.daoris.space.whose.*`: *the agent's own*, *the browser's own limit*, *the update*), each
kept reason (`settings.daoris.space.kept.*`) and each refusal (`settings.daoris.space.refused.*`) is a key in both catalogues. A size
is formatted by `format.ts`; a version, a repository and a folder are content and stay as written.

## 5. Dropping a set-aside tree's package folders

**Decided: yes, narrowed in round 2.** A tree's folder set aside drops its package folders, each a `node_modules` beside a
`package.json` git tracks, at the first pass an hour after its move, where nothing in the folder was written since. Everything else
waits the folder's days: git's tracked files, `bin/` and `obj/`, every other build folder and everything the session wrote. **Where
anything about a path is unknown, the path stays**: that is the rule each step below applies.

**Why this part and no other.** A removal nobody presses needs to know the thing holds nothing a person keeps. A build folder's name
does not say so: an ignored `build/` may hold a `.env`, a local database or a hand-made export (Codex 2.1), and a program run from
`bin/` writes beside itself. A shared name was TREEKEEP1's rule for the person's press (`TreeScratch.cs:9-19`, D102's TREEKEEP1),
which is the person's act; it is not proof enough without one. A package folder is the one whose producer discards it whole: npm's
clean install removes an existing `node_modules` before it installs, and another package manager in its place rebuilds it from the
same `package.json`, so nothing kept there outlives the next install. It is also most of the gain: 52% of the measured folder's
bytes and at least 77% of its files (§0.2). Git's tracked files (12%) stay because removing them needed three guards of their own
(§7); `bin/` and `obj/` (35%) stay because no producer of theirs discards them whole (§7, and §11's question for Fable).

**The judgement, before the hold, checked again inside it** (Fable F1.5). Made in `SweepPlanAsync` (`TreeSweeps.cs:30`), which the
look runs before it takes the repository's hold (`AutoTidies.cs:270`, the hold at `:286`), so the hold gains no `git status
--ignored` walk beside `NestedRepositories`' own (`TreeSetAside.cs:92, 118`). Inside the hold, the last look
(`TreeSetAside.LastLookAsync`, `:55-111`), after its guards have passed, checks it again by the reads it already makes: its index
read still lists each judged folder's `package.json`; a tree with a change to a tracked file, a `.gitignore` among them, never moves;
and its one walk of the tree's folders, which the judgement's nested and link checks share, finds no repository and no link on the
way to or inside a judged folder. A folder is droppable where all of these hold:

1. **Git ignores it whole**: one `status --porcelain=v1 -z --ignored=traditional --untracked-files=normal` in the tree names it
   `!! <path>/`. A folder git lists file by file, a tracked file inside it, is not one.
2. **It is a package folder**: its last segment is `node_modules`, and the folder above it holds a `package.json` the tree's index
   lists (the last look's `ls-files -v -z`, `:76`, already read). A `node_modules` with no tracked `package.json` above it stays.
3. **Nothing in it is another's**: no repository lies under it (the last look's `NestedRepositories`, `:89-108`, whose answer for
   paths under an ignored folder it now also reads), and no link lies on the way to it from the tree's top, it included.

Where git's status, the index or the folders could not be read, the tree moves aside whole with no manifest, as today.

**The manifest, written before the move** (Codex 2.4). The look chooses the folder's name in `.tidied` first (`<name>-<moment>`,
`TreeSetAside.cs:206-220`, which today chooses it inside the move), and writes `<name>-<moment>.slim.json` beside where the folder will
go, atomically:

```
{ schema: 1, state: judged|moved, judged, moved, repository, branch,
  paths: [ { path, state: pending|removing|removed|kept, why, tried, ended } ] }
```

Each path is relative to the tree's top in git's spelling; `ended` is when its removal ended, written with `removed` (step 4). Then
it moves the folder. Once `MoveAsideAsync` reports the folder moved
and git's record let go (`:166-197`), it writes `state: moved` with the moment. A folder put back, or a move refused, removes its
manifest. Only a manifest at `moved` is ever slimmed: one left at `judged`, where the process stopped between the move and that write,
is unknown, so its folder is kept whole for its days and the manifest dropped by the next pass. A manifest whose folder is gone is
dropped. The manifest is a file, so the folders' enumerations pass it over (the purge's, `TreeSetAside.cs:259-261`; the inventory's,
`SessionTrees.cs:531-535`), and it goes with its folder.

**The slim, later, in the Keeping pass** (§3.1), under the run lock and within the pass's budget, outside every repository's hold
(Codex 5.1: the move runs inside `SweepAsync`, under the hold, `AutoTidies.cs:286-292`; the slim touches no git and no live tree, so
nothing of it runs there). It takes each manifest at `moved` whose folder was moved an hour or more ago (`Settle`, the downloads'
`Settling`, `DownloadClearing.cs:87`), where the person's rule is not `whole`, read at the slim too:

1. **Nothing written since the move.** The purge's own read of the whole folder (`TreeSetAside.Kept`, `:363-405`), the package folders
   included: a write after the move, by an install or a program still running from the folder, keeps the folder whole for its days,
   said as the purge says it, and the manifest's paths are marked `kept: written`. This, and the move's own guards (no session,
   review or words held the tree, read inside the hold), are what is known of the folder's use; the move's rename succeeding is not
   (§3.5). **The residual, exactly** (Fable F1.3): a pure-JavaScript program that outlived its session, holds nothing in the folder
   open and writes nothing there fails at its next lazy `require`. A program with a native addon loaded from the tree, or its
   working folder in it, already refuses the move, which keeps the tree as `busy` (`TreeSetAside.cs:219-225`). The purge accepts the
   same program at its days; here it costs that program its packages, never work.
2. **Each path checked again right before it goes** (Codex 2.5). Its relative path holds no `..` and is not rooted; joined to the
   folder, its full path lies under the folder by whole segments, compared without case on Windows; no link lies on the way from the
   trees home to it, it included (`TreeWorktrees.LinkBelowTreesRoot`, `:89-110`, which checks links and not containment: a path
   outside the trees home gives it `..` segments to walk, so containment is checked apart); and it is still a folder named
   `node_modules`.
3. **Removed, never following a link.** The path is marked `removing` in the manifest, then removed by `DeleteNeverFollowing`
   (`TreeSetAside.cs:413-437`), which in this round checks its own argument, and each folder again right before it reads it, and refuses
   one that is a link: today it checks only the children it lists (`:416-418`), so handed a folder that is, or became, a junction it
   would read the junction's target (Codex 2.5). A link inside goes as a link, what it points at untouched (TIDIEDLINK1); a read-only
   file is made writable first. The path's end is written as it ends: `removed` with `ended`, or `kept` with why. A stop midway leaves it
   `removing`, and the next pass begins it again from step 1. A path that failed for the same reason as at its last try (`tried`)
   waits a day (§3.1).
4. **The purge knows what the slim removed.** Removing a folder changes the last-write time of the folder above it, and the purge
   reads every entry's last-write time against the move (`TreeSetAside.cs:390-398`): with nothing more, every folder slimmed would be
   kept at its date for ever as *written after it was moved aside*, each a blocked unit in *What needs you*. So the purge's read, and
   step 1's, pass over what a manifest marks `removing` or `removed` without reading into it, and read the folder directly above a
   `removed` path against when that removal ended (`ended`), not against the move; while a path below it is still `removing`, that
   folder's own time is passed over until the removal ends. Every file and every other entry of that folder is still read against the
   move. **The read checks each entry's creation time too** (Fable F1.1), as `TreeStrays.WrittenSince` does (`TreeStrays.cs:288`): a
   copy or a move keeps its source's last-write time, and the purge's read checks `LastWriteTimeUtc` alone (`TreeSetAside.cs:390-398`),
   so a file copied or moved into that folder after the move was seen by nothing once the folder's own time was passed over. A copy
   takes a new creation time; a move changes the time of the folder it lands in, read against the removal's end. A file another
   process writes, copies or moves there afterwards keeps the folder; a removal by another process there is not seen, and loses
   nothing a person keeps. The purge deletes the manifest with its folder.

The log writes `tidied.slimmed`; the folder's line in the work inventory and in `work state` names what was dropped.

**Bringing it back** (Codex 2.7). The folder keeps git's tracked files and everything of the session's own; only its package folders
are gone, and its producer makes them again in place (`npm ci` beside the `package.json`). No git command restores anything, since
git's files never left.

**What is not slimmed**: a folder moved aside that was no tree (`TreeStrays`), a cut-off opening, an opinion's copy and a replay's
tree (`PassLeftovers`), since git cannot vouch for them; a tree whose judgement did not read; a folder while the person's rule is
`whole`; and the 73 folders already set aside, which have no manifest and go by their fourteen days (all by 2026-11-03) or by the
person's early press.

**An older build** sharing the home (one put back by an update's rollback) reads no manifest: its purge keeps a slimmed folder as
written, a blocked unit the person can remove by the press. It keeps more than it should, never less (`compatible-builds.md`).

## 6. Safety

1. **Nothing a session uses is removed, by what is known of each thing's use** (§3.5). The live trees keep the look's guards; a
   set-aside folder's packages go only an hour after a move the look made of a tree no session, review or words held, and only where
   nothing in the folder was written since; a downloaded version is kept while a pin, a use or a setting names it, judged under those
   files' locks once every writer of them takes the lock, and while a running session resolved a binary in it (for a record older
   than that field, while a session that started before it was found unused runs); the browser's caches go only while the
   shell says its browser is not running, holding its start. A rename still goes first wherever a program could hold a thing, and its
   refusal still keeps it as *in use* (CACHECLEAR1's rule), but its success proves nothing (Codex 2.6).
2. **Nothing outside the home is touched.** Every path is the table's entry under the Daoris folder (`DaorisHome.Entry`), or a
   manifest's path checked to lie under its set-aside folder by whole segments, with no link on the way (`LinkBelowTreesRoot`,
   `TreeWorktrees.cs:89-110`, and containment checked apart, §5). Every walk and delete follows no link, and `DeleteNeverFollowing`
   checks its own argument as well as what it lists (§5). Daoris's refs in a registered repository keep D88's and D102's rules alone;
   the checkouts and the person's tool home are never written.
3. **Only what is known to be made again goes by itself before its time**: a set-aside tree's package folders, unused downloads and
   the browser's caches. Everything else, git's tracked files, `bin/` and `obj/` among them, goes only by a rule the person can see and
   set, or by their press (D88's amendment of D51 rule 7). Where anything about a path is unknown, it stays.
4. **Every removal is said**: the machine log (§4.4), `keeping.json`'s *last* per kind, the card's *last*, the look's report line, and
   the list's kept units with their reasons.
5. **What a clean would remove is seen before it runs**: each kind's *next* on the card; the press listed first, its second press
   sending exactly the listed units, each judged again right before it goes; `daoris-driver space free` without `--yes` lists and
   changes nothing.
6. **One remover per kind.** The pass, the route and the terminal alone call each kind's one typed operation (§2.1), so the guards a
   kind has are the ones it keeps; the downloads' two acts become one, Tools' delete taking CACHECLEAR1's (§3.5); the slim and the
   browser's caches, the two new acts, are written in the purge's and CACHECLEAR1's shapes.
7. **A failure keeps, and so does what is unknown.** A read that fails keeps what it would have judged (D88's *unread*); a delete that
   fails leaves its path and is said once; a manifest left at `judged` keeps its folder whole; a `keeping.json` that does not read
   lets no download go by its days; a free-space read that fails runs no floor act; a browser whose state no shell can say keeps its
   caches.
8. **One run at a time, bounded** (§3.1): the run lock between processes, tried and never waited for, its holder named; at most 60
   seconds of removal a pass, a stop heard between paths, an unchanged failure tried again the next day; the use files' locks held
   per version from judgement to rename, never across a removal (§3.5).

## 7. Rejected

- **Deleting a tree's folder at the move, whole.** The fourteen days hold a session's own scratch and a write that raced the move
  (D88's AUTOTIDY1); slimming keeps both and drops the rest.
- **Taking every ignored path as made again.** A `.env`, a local config or a session's prepared requests (TREEKEEP1's case) are
  ignored and are the person's; only a package folder goes.
- **Removing git's tracked files at the move** (round 1's choice). About 61 MB a folder, 12% of its bytes, 52 GB at the rate of
  2026-10-20 over fourteen days, for a removal that needed three guards of its own: the clean judgement fenced against an edit made
  after it, which today is read before the last look (`TreeSweeps.cs:142, 152`; Codex 2.2); the commit kept on a ref for every kind of
  removal, where a completed pull request's carrier and a squashing landing's hold set no `HeldBy` and so get no recovery ref
  (`:163, 319-338, 343-346`; Codex 2.3), and the slim waiting for the ref transaction (`:221`); and a tested restore into a fresh
  folder (Codex 2.7). Kept, the folder is the session's tree as it was but for its packages, and a reinstall makes it whole.
- **A build folder's shared name as proof it can be made again** (round 1, TREEKEEP1's rule). It is the rule for the person's press;
  for a removal nobody presses, the proof is a producer that discards the folder whole (§5; Codex 2.1).
- **`bin/` in the slim, ever** (Fable F5). A program run from it writes beside itself (a local database), so nothing short of the
  person's days or press knows it holds nothing a person keeps.
- **`obj/` at once** (round 2's question to Fable). `bin/` and `obj/` together are about 35% of the measured folder, in few files,
  never measured apart. No build discards `obj/` whole, and `dotnet clean` removes only the files the build recorded; taking exactly
  those (MSBuild's `*.FileListAbsolute.txt`, each configuration's, its paths absolute at the tree's old place) is precise but a
  guard of its own. `obj/`, from which no program runs, may join later on evidence: measured apart in §10's week, then a second
  producer rule in its own row, KEEP1a2 (§9). The person's days, the press and the floor's word hold it meanwhile.
- **A first-unused time kept once found** (round 2): a version found unused, then pinned, started on and unpinned, read as unused
  since before that session's start, and went under it (Fable F2.1). A pass that finds it used clears the time (§2.3).
- **A session's start as the whole fact of what it may run** (round 2): the binary each session resolved names the version exactly,
  both of an ACP session's (Fable F2.5); the start stays the rule only for a record older than that field (§3.5).
- **The use files' locks held across the removal**: a person's pin or `SET_KEEP` would be refused at the keep lock's ten seconds
  while a large version is deleted (Fable F2.3); they are held per version, from its judgement to its rename.
- **KEEP1b before every writer of the use files takes the keep lock** (round 2's order): the guard binds only writers that take it,
  and the harness file's whole-layout write takes none on either side (Fable F2.2, §3.5).
- **The run lock waited for** (round 2, `RecordLock`'s ten seconds): a run elsewhere is a long one, so a wait holds a pass or a press
  for nothing, and says nothing of who holds it; it is tried, its holder named (Fable F3.1).
- **The free space of the Daoris folder alone** (round 2): the trees home may sit on another volume through a junction (Fable F4.3).
- **The browser's caches cleared each hour below the floor** (round 2): once in each stretch below it (Fable F4.1).
- **Slimming the folders already set aside** by reading their `.gitignore` files without git: a guess, since git's record of each is
  gone; they go by their date or the person's press.
- **The slim at once, inside the tidy's pass** (round 1): it ran inside the repository's hold (`AutoTidies.cs:286-292`) and on a rename's
  word. Later, it runs on an hour's evidence, under the run lock, outside every hold (§5).
- **A rename's success as proof that nothing uses a thing** (round 1, §6): a file opened with delete sharing does not refuse it
  (`MachineLog.cs:299-300`), nor a version an agent runs through npm's shims (`HarnessSettings.cs:509-512`); each kind says what it
  knows of its use instead (§3.5; Codex 2.6).
- **A ledger of sizes kept by every remover** (round 1's `kinds` and `setAside` in `keeping.json`): counters every writer had to keep
  true, among several processes; sizes come from a bounded walk per kind, and a slimmed folder is small (§4.3; Codex 6.1).
- **An opinion's packet with its quest's clear** (round 1's KEEP1g). A packet is an opinion's, its folder named by the opinion's id
  (`OpinionPackets.cs:36-45`); it may cover several quests (`OpinionPacket.cs:40-41`), and its reviewer's session names no quest
  (`DriverOpinions.cs:157-158`), so one quest's clear cannot tell whether another quest or a live reviewer still needs it (Codex 5.5).
  The rooms together are under 1 MB on the install. A rule for packets belongs with the opinion's own record and its liveness.
- **The floor taking a `never` download** (round 1): the person's `never` is the press alone, below the floor too (Codex 3.1).
- **A floor sized to hold a day of builds** (round 1's justification): no fixed margin on a small disk could; the floor is a trigger
  (§3.2; Codex 3.3).
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
- **A walk of the whole home at each pass**: about 2.2 million files on the install, 36 to 100 s; a bounded walk per kind, at most once a
  day, says the same.
- **A file of its own for the rules** (`keep.json`): the standing choices already have one file, its twin, its terminal family and
  its Ask Daoris door.
- **A word of its own for each act**: *clean up* stays the trees' and branches', *clear* the records' and the downloads'; the
  umbrella press is *Free up space…* and calls them.
- **Holding new sessions while below the floor**: a scheduling choice for the cap, not cleanup; a session's own failure says the
  disk is full.

## 8. What this amends, once accepted

- **D88's AUTOTIDY1 note**: a folder the look moves aside drops its package folders an hour after the move, where nothing was written
  in it since (§5); the purge's fourteen days are the person's to set, read by every reader through one policy; the purge's read
  passes over what the slim removed, reads the folder above a removed path against the removal's end, and checks each entry's
  creation time as well as its last-write time; the purge runs in the Keeping pass under the run lock. Its guards, the move and
  *Said once* stand. `DeleteNeverFollowing` checks its own argument too.
- **D102's TREEKEEP1** stands as it is: its *shared build folder* rule stays the person's press's, and the slim does not use it.
- **D88's CACHECLEAR1 note**: unused versions also go by a rule the person sets, and below the floor but for `never`; the judgement
  gains the keep for a version a running session resolved (its start, for an older record) and runs under the use files' keep locks,
  per version from the judgement to the rename, and a pin's writer looks again under its lock; Tools' delete of one version takes
  the same act; the rename first and the press stand; the terminal twin is `daoris-driver
  space free --kind downloads`.
- **The history clearing design §7**: decided by the owner (§3.6): nothing clears finished history by itself unless the person has set
  an age.
- **D172's layout §4**: the logs' row keeps 30 days *or fewer, as set*; `data/` gains `keeping.json`.
- **The This Daoris design**: §3.1's *nothing offers to delete it*, for `webview2/` and `browser/profile/` alone, which the press now
  offers (§3.3); §9's *editing settings on this page*, for the keep rules, which have no other place, so the page is their one door.
- **The machine log design §4**: the five events of §4.4.

## 9. Build rows

Each is a feature a person can see working (D187), end to end across its lanes. They are named KEEP1, not CLEANUP1a and on, since
CLEANUP1b and CLEANUP1c already name D173's and D102's notes and two fix-log entries of 2026-10-17, and CLEANUP1 is that id's third
use. Each runs its own tests and the quick checks; the merge and the CI beside run the rest (D180).

**Their order** (Codex 5.1 and 5.4, round 2). The foundations go in the first row that removes anything: the table, `keeping.json`'s
locked writer, the manifest and the run lock are KEEP1a's. A rule runs before any build shows it or lets the person set it: the
slim and the downloads' rule (1a, 1b) come before the card (1c) and the person's rules (1d); the floor (1f) comes after every act it
runs (1a, 1b, and the browser's caches in 1e); history's age (1g) brings its choice and its clear in one row. Renumbered from round 1:
its 1a stays 1a, its 1f is 1b, 1b is 1c, 1c is 1d, 1d is 1e, 1e is 1f, 1h is 1g (the owner's note names it KEEP1h), and its 1g, an
opinion's packet with its quest's clear, is dropped (§7). **Round 3** (Fable F2.2, F5): KEEP1b comes after PUSH1f2e (PUSH1f2c, also
named, is merged), once every writer of the use files takes the keep lock, or carries the harness file's whole-layout write under it
itself (§3.5);
KEEP1a2, `obj/` on evidence, follows KEEP1a and a week of its measurement.

- **KEEP1a — a tree set aside drops its package folders once it has settled** (driver; the service's and the CLI's home tables).
  §2.1's `KeepKinds` with its set-aside kind,
  and the one effective policy every reader of the set-aside days reads, still fourteen days (`BranchTidying.cs:111, 248, 317`,
  `WorkInventories.cs:59`, `WorkInventory.cs:610`, `TreesConsole.cs:108`); §2.3's `keeping.json` through `JsonKeeping.Save`, with the
  home twins' `keeping` entry, its `last` for the set-aside kind; §3.1's Keeping pass in the tidy's task taking over the purge, the run
  lock tried in `TreeLock.TryOpen`'s shape with its holder's `{ pid, since, door }` file, the budget and the backoff; §5's judgement in
  `SweepPlanAsync` before the hold, checked again by the last look's own reads inside it, one walk shared; the manifest written before
  the move and marked `moved` after, each path's `ended`; the slim; the purge's read knowing the manifest, reading the folder above a
  removed path against its `ended` and every entry's creation time too; `DeleteNeverFollowing` checking its own argument and every
  folder before it reads it, containment checked apart; `tidied.slimmed`; the inventory's and `work state`'s line naming what was
  dropped. Proof: a `Process`
  class over real git, seen failing first: a tree holding a `node_modules` beside a tracked `package.json` (a junction inside it to a
  folder outside), a `node_modules` with no tracked `package.json` above it, `bin/`, `obj/`, `dist/`, a `.env`, `local/notes` and tracked
  files, moved aside: before the hour nothing goes; after it only the first `node_modules`, the junction's target untouched; the folder
  then purged at its days, not kept as written; a `node_modules` that is itself a junction kept, its target untouched;
  `DeleteNeverFollowing` handed a junction refusing it; a manifest path with `..`, or rooted, refused; a write in the folder after the
  move keeping it whole, its paths `kept: written`; a manifest left at `judged` keeping its folder whole and dropped; a slim stopped
  midway taken up by the next pass and never read as a write; a folder git would not let go of put back with no manifest; `whole`
  read at the slim; a stray and a cut-off opening never slimmed; a second pass meeting the held run lock at once, removing nothing
  and naming the holder; a holder's file whose process is gone read as a run cut short; a `keeping.json` written by another writer
  between this one's read and write keeping both; a file copied, and one moved, into the folder above a slimmed path after its
  removal ended keeping the folder at the purge; a `package.json` untracked between the judgement and the last look leaving its
  `node_modules` out of the manifest.
- **KEEP1a2 — `obj/` joins the slim, on evidence** (driver; after KEEP1a and a week of its measurement; Fable F5). First `obj/` is
  measured apart from `bin/` in the folders set aside over §10's week: its share of the bytes and files, and how often one was written
  after its move. Where that bears it out, a second producer rule beside §5's, written from what the week showed, with every guard of
  §5 (the judgement before the move, the manifest, the hour, nothing written since, containment, no link); `bin/` never (§7). Proof:
  the measurement in D195's note; then KEEP1a's `Process` class gaining an `obj/` the rule takes, one it keeps, and `bin/` kept.
- **KEEP1b — unused downloads go by their rule** (driver, the service's session record; the web's Downloads row; after PUSH1f2e,
  PUSH1f2c being merged, §3.5). §2.1's `downloads` at its default, seven days: the pass's `unused` in `keeping.json`, cleared by a
  pass that finds the version used (§2.3); the daily removal through CACHECLEAR1's act, listing without measuring every version;
  the act judging under the keep locks of the harness file the roster reads, `tools.json` and `driver.json` at the loop's path, held per version from the
  judgement to the rename; the binaries each session's start resolved, recorded on its record, keeping a version a running session
  resolved, and a record without them kept by its start; a pin's writer looking again under its lock; `TOOLS_DELETE` through the same
  act; the Downloads row (`WiringDownloads`) saying each version's date by the rule. Proof: `DownloadClearingTests` rows, each seen
  failing first: first found unused recorded once in an unused stretch; found unused at T0, pinned at T1 with a pass seeing it pinned,
  started on at T2 by a session whose record holds no binaries, unpinned at T3: at T4 its first-unused time is T4's pass, and the
  session keeps it; removed after its days; a pin written between the judgement and the
  rename waits on the lock and keeps the version; a pin waiting while another version is removed taken at once, the locks let go at
  the rename; the harness file named by `DAORIS_HARNESS_CONFIG` the one locked; a version holding a binary a running session resolved
  kept as `running`, both of an ACP session's; a record with no binaries kept by its start before the first-unused moment, one started
  after not; `never` and an unreadable `keeping.json` keep it; Tools' delete of a version a session runs refused the same way; the
  row's words in both languages; and, built before PUSH1f2e, a whole-layout write of the harness file waiting on the held lock.
- **KEEP1c — This Daoris shows each kind's size, rule, next and last clean** (driver, modules, web). §2.1's presentation, §4.3's walk
  per kind through `WiringSizes`, `SYSTEM_SPACE` (`Open`), `daoris-driver space`, the *Disk space* card with every state of §4.2 but the
  editors and the floor's, `last` from `keeping.json`, `held` from the run lock's holder file (a run under way elsewhere, or one a
  crash cut short, §3.3), `left-over` naming `webview2/` and `browser/profile/` by path, the keys and the glossary's two terms,
  WIRING-SIZES-LIVE1's need for these kinds. Every rule it shows already runs (1a, 1b, the log's thirty days,
  history's press). Proof: the read's tests (a kind never measured, a partial count, each kind's *next* from the walk and the policy,
  a `last` from `keeping.json`), the terminal's text and `--json` table, the census's row, the card's tests and stories at wide, half
  and narrow in both languages.
- **KEEP1d — the person sets each rule, on the page and at the terminal** (driver, modules, web; cli while its twin reads
  `driver.json`). §2.2: `driver.json`'s `keep` for the
  set-aside folders (days, `whole`), the downloads (days, `never`) and the logs (days), with its twins and keeps table; `SET_KEEP`
  (`Person`, `person.route.SET_KEEP`); `daoris driver keep` handed to the host; the bounds' refusal; each row's editor (R3); Ask
  Daoris's `setting` door; the pass's daily log prune at the person's days. Proof: the both-ways modules test over every `keep` choice
  this row adds, the keeps table's row on a file neither writer wrote, the refusal under the editor, the coverage row, and each of the
  six readers of the set-aside days saying a set value.
- **KEEP1e — the press frees space, listed first** (driver, modules, web). §3.3: `SYSTEM_SPACE_FREE`'s plan and run with its id,
  `SYSTEM_SPACE`'s `run`, the census's `PipeRule` (`Open` plan, `Person` removal) with `person.route.SYSTEM_SPACE_FREE` and
  `PersonPresses`, the session marker's refusal alone; `daoris-driver space free` reading the run to its end; the groups that go early
  (set-aside folders before their date with their manifests, downloads within their days, the browser's caches through the shell's
  browser host, a new seam the shell hands the driver as it hands the in-app browser, an earlier build's two folders by path, a
  failed update's folder); §3.2's cache table held to the install's profile, read without writing; R2's line; `space.freed` and
  `space.kept`; the `Exempt` row. Proof: a plan that changes nothing; a press removing only the listed units, one changed since kept
  and counted; a running browser's caches kept, and kept with no Daoris running; a held earlier-build folder kept as in use; a second
  press joining the run; the terminal's exits by the run's end, and Daoris stopping mid-run said as not known, exit 2; a confirmed
  and a refused press from a terminal; a marked environment refused alone, exit 1.
- **KEEP1f — below the floor, Daoris frees what it can make again and says the rest** (driver, modules, web). §3.2: the free-space
  read for the folder's own path and each entry the acts free from, each volume's floor from its own size and said once, a kind on
  another volume said on its row (`kinds[].volume`), the default, the floor's choice in `keep` with its bounds and its editor, the
  pass's three early acts at once, at most hourly, the browser's caches once in each stretch below, the row in *What needs you* with
  the press as its door (`NeedsAPerson`, `AttentionStateSlice`, the attention words), `space.low` and `space.ok` at the margin. Proof:
  a stand-in free-space read below and above the floor; the three acts run in order and nothing else, each with its own guards (a
  slim within its hour not run, a `never` download kept, a running browser's caches kept); the browser's caches cleared once in a
  stretch below, not an hour later, and again after `space.ok` and a new `space.low`; a trees home on another volume through a
  junction judged by its own volume's read, the folder's volume above its floor; said once, and cleared at the margin; a floor out
  of its bounds refused; a mounted folder's volume read, not its drive's; a failed read running nothing.
- **KEEP1g — finished history by an age the person sets** (driver, modules, web; decided by the owner, D195's note, which names it by
  round 1's id, KEEP1h). §3.6: history's choice in `keep` (`daoris driver keep history`, its row's editor on the card), the daily clear
  through `HistoryClearing`, every refusal holding, `history.cleared`'s `door: rule`; the history clearing design §7 amended. Proof: a
  unit past its days cleared and one with a refusal kept and said; `never`, the default, clears nothing; the both-ways test over the
  choice.

## 10. What this document's gate does not cover

Documents only; nothing is built. The statements about today were read from the code at `5e8caed81`, again at `1657bd997` and, where
Fable's read touched them, at `2cdc2e8c2`; none was run.

**What it rests on** (round 3). Two of Fable's findings were blocking, and the design is safe only with both taken. **F2.1**: a
version's first-unused time is the start of its current unused stretch, cleared by a pass that finds it used (§2.3); kept once and
never reset, the start-time keep for a running session did not hold. **F2.2**: the downloads' judgement under the use files' locks
binds only writers that take them, so KEEP1b waits for every writer of the three files to take the keep lock, PUSH1f2e (PUSH1f2c is
merged), or carries the harness file's whole-layout write under it itself (§3.5, §9). Neither is proved until KEEP1b's rows.

**The residuals, exactly.** At the slim: a pure-JavaScript program that outlived its session, holds nothing in the folder open and
writes nothing there fails at its next lazy `require`; a program with a native addon loaded or its working folder in the tree
already refuses the move as `busy` (§5 step 1, Fable F1.3). At the purge after a slim: a file moved, not copied, into the folder
above a package folder while that folder's removal runs is not seen, since that folder's own time is passed over until the removal
ends (§5 step 4). After a crash: the card's `last` may be older than the folders, said as a run stopped before its end (§3.3).

**Not shown.** The sizes are one install's on one day, read with .NET's enumeration and not compared with Explorer's; the owner's
36.7 GB and this reading's 37.4 GB differ by a day's moves and the counting; the split of a set-aside folder (§0.2) is the one
measured folder's, not the 73's, and `bin/` and `obj/` were not measured apart. No test shows `status --ignored=traditional` naming
each wholly ignored folder as the slim relies on; a folder's last-write time changing when an entry in it is removed, which §5's step
4 answers, and a copy taking a new creation time (NTFS's and POSIX's documented behaviour, not run here); `GetDiskFreeSpaceExW`
answering a mounted folder's volume, or a junction's target's; npm's clean install discarding `node_modules` whole (its
documentation); the cache paths of §3.2's table on the install's profile; or where a session's record keeps the binaries its start
resolved (KEEP1b names the field). KEEP1a, KEEP1b, KEEP1e and KEEP1f prove them. What still crosses, besides the residuals above: a
folder replaced by a junction between `DeleteNeverFollowing`'s check of it and its read is not closed by a check, only narrowed to
that moment, under a trees home only Daoris writes. Claude Code's `cleanupPeriodDays` is read from its documentation. The floor's
20 GB and 10%, its bounds, the downloads' 7 days, the slim's hour and the margin are judgements, each a default the person sets or a
bound, to be read again on the install after a week of KEEP1a; that week also measures `obj/` apart from `bin/`, for KEEP1a2 (§7).

## 11. The reads (D191)

**Codex, 2026-10-21, on round 1.** Every finding was checked against the code at `1657bd997` before it was taken (D160's step 9;
the trees' files are as round 1 read them at `5e8caed81`). All hold; each is taken, several by a smaller change than the one asked,
said in the verdict. (B) marks what the read called blocking, (N) what it did not. The steward's own findings from the check follow
(S).

| # | Finding | Checked at | Verdict | Taken in |
|---|---|---|---|---|
| 1.1 | (N) Removing a plugin keeps and names its data; the inventory grouped them | `PluginsRoutes.cs:308-320` | holds | §1.2: two rows, the installed folder and what a plugin kept |
| 1.2 | (N) Downloads have a second remover, Tools' delete, which deletes in place and may leave part; "one remover" and "rename first" were not so | `ToolsRoutes.cs:189-199`, `ToolInstall.cs:315-349` | holds | §0, §0.1, §1.4; §3.5 and §6.6: Tools' delete takes CACHECLEAR1's act (KEEP1b) |
| 1.3 | (N) The browser's active profile is `Default`; `browser/profile/` is not shown as an earlier build's | `EngineBrowser.cs:57`, `WiringSizes.cs:68-73`, `SystemWiring.cs:446-454`, `DaorisHome.cs:355-365` | holds: it lies inside the `browser` folder, counted under it | §1.6; §3.2's table by exact path; `left-over` names it by path (KEEP1c) |
| 2.1 | (B) A build folder's shared name does not prove it can be made again (`build/.env`, a local database, an export) | `TreeScratch.cs:15-19`, D102's TREEKEEP1 | holds | §5: only a package folder beside a tracked `package.json`, whose producer discards it whole; `bin/`, `obj/` and the rest wait their days (§7) |
| 2.2 | (B) The clean judgement is read before the last look, so an edit between them would be deleted with the tracked files | `TreeSweeps.cs:142, 152, 290-296` | holds | taken by the smaller change: the slim removes no tracked file and nothing outside a package folder git ignores whole, and a write after the move keeps the folder whole; nothing needs the fence (§5, §7) |
| 2.3 | (B) The recovery commit is not kept for a completed pull request's carrier, so the restore would fail once git collects it; the slim must wait for the ref transaction | `TreeSweeps.cs:163, 221, 343-346` | holds, and for a squashing landing's hold too (`:319-338`, no `HeldBy`) | taken by the same change: git's files stay in the folder, so no commit has to be recovered (§7) |
| 2.4 | (B) No durable manifest of what was judged before the move; after a crash the next pass cannot know what was authorized | round 1's §2.3 and §5 | holds | §5: a manifest beside the folder, written before the move, `judged` then `moved`, each path's state; `judged` alone keeps the folder whole |
| 2.5 | (B) `DeleteNeverFollowing` reads its argument before asking whether it is a link; containment, the root and its ancestors are unchecked | `TreeSetAside.cs:413-418` | holds; and `LinkBelowTreesRoot` checks links on the way but not containment (`TreeWorktrees.cs:91-96`) | §5 steps 2 and 3: the helper checks its own argument and each folder before it reads it; containment by whole segments checked apart; the moment between check and read said in §10 |
| 2.6 | (B) A rename's success is no proof that nothing uses a thing: delete sharing, agents run through npm's shims, the downloads' reread then rename | `MachineLog.cs:299-300`, `HarnessSettings.cs:509-512`, `DownloadClearing.cs:176, 186` | holds | §3.5: each kind's proof of use (the move's guards and an hour's quiet; the use files' locks to the rename and the running session's keep; the shell's browser host); §6.1 |
| 2.7 | (N) The restore command aimed at a folder that is not empty | round 1's §5 | holds | answered by 2.3's take: only package folders go, made again in place by a reinstall (§5) |
| 3.1 | (B) The floor overrode a person's `never` | round 1's §2.1, §2.2, §3.2 | holds | §3.2: `never` is the press alone, below the floor too; KEEP1f's proof |
| 3.2 | (N) No overall budget: unfinished slims each pass, an unbounded delete, the clearing relisting and measuring every version | `TreeSetAside.cs:413`, `DownloadClearing.cs:143-147, 160` | holds | §3.1: one run lock, 60 s a pass, a stop between paths, an unchanged failure tried the next day, the rule's listing without measures |
| 3.3 | (N) 20 GB cannot hold a 27.5 GB day; percentages unbounded; "floor plus 5 GB" may never clear | round 1's §3.2 | holds | §3.2: the floor a trigger, its bounds, the margin the smaller of 5 GB and 5%, so at most 55% of the volume |
| 4.1 | (B) Atomic replacement and the lease do not serialize `keeping.json`'s writers | `JsonKeeping.cs:53-71`, D194 point 3 | holds | §2.3: only `JsonKeeping.Save`, under the file's keep lock; and fewer fields to write (6.1) |
| 4.2 | (B) The routes are unclassed for the pipe's census; `--yes` and the lease are no authority | `PipeCensus.cs:83-84`, D194 point 5 | holds | §3.3 point 4 and §4.4: plan `Open` by a `PipeRule`, removal and `SET_KEEP` `Person`, the window's Confirm and its words, `PersonPresses`, the marker's refusal alone |
| 4.3 | (B) The design meets D191's foundation threshold | D191's amendment | holds, as the steward had already agreed (D195's note) | the standing; Fable reads §3 and §5 of this round |
| 5.1 | (B) KEEP1a used the table and ledger KEEP1b built; the slim's plan ran inside the repository's hold | round 1's §9, `AutoTidies.cs:286-292` | holds | §9: the foundations in KEEP1a; the slim in the Keeping pass, outside every hold (§3.1, §5) |
| 5.2 | (B) Every reader of the purge's days must read one policy; history's controls wait on the owner | `WorkInventory.cs:610`, `WorkInventories.cs:59` | holds, with four more readers: `BranchTidying.cs:111, 248, 317` and the terminal's `TreesConsole.cs:108` | §2.1's one effective policy, every reader in KEEP1a and a set value in KEEP1d's proof; the owner decided history, and its choice is KEEP1g's |
| 5.3 | (B) The press answered "started" while the terminal's door hands the verb the answer as it arrives, so the terminal could not print the end | `InstanceDoor.cs:186-241` | holds | §3.3 point 3 and §4.4: a run with an id, `SYSTEM_SPACE`'s `run`, the terminal reading it to its end |
| 5.4 | (N) The card showed the downloads' rule before any row ran it | round 1's §9 | holds | §9's order: enforcement (1a, 1b) before the card (1c) and the person's rules (1d); the floor after its acts; each editor with its row (§2.2, §4.1) |
| 5.5 | (B) An opinion's packet is the opinion's, may cover several quests, and its reviewer names no quest | `OpinionPackets.cs:36-45`, `OpinionPacket.cs:40-41`, `DriverOpinions.cs:157-158` | holds | taken by dropping the row: the rooms are under 1 MB, and a rule for packets belongs with the opinion's record (§7) |
| 6.1 | (N) A smaller shape: a descriptive table and typed removers shared by the watch, window and terminal; only manifests, first-unused times and last outcomes durable; sizes through `WiringSizes`; the Downloads row reused | D194 point 4, `WiringDownloads.tsx:31` | holds | taken whole: §2.1 (the table describes, one typed operation per kind), §2.3 (`keeping.json` holds `last` and `unused` alone, the manifest beside its folder), §4.3 (no ledger), §4.1 (the Downloads row's list) |
| S1 | Removing a folder changes the last-write time of the folder above it, which the purge reads as a write after the move: round 1's slim would have kept every folder it touched at its date for ever, a blocked unit each | `TreeSetAside.cs:383-405` | holds; NTFS's and POSIX's documented behaviour, not run here | §5 step 4: the purge passes over what a manifest marks removed, and over the last-write time of the folder directly above it; KEEP1a's proof |
| S2 | The logs' row said a held file refuses its delete; the log opens its file with delete sharing, and the date is the guard | `MachineLog.cs:296-300` | holds | §3.5's logs row |

**What this round took from Codex's smaller shape** (6.1): all of it. `KeepKinds` describes and runs nothing; each kind's removal is
one typed operation in the driver library, which the pass, the route and the terminal alone call (D194 point 4). What is durable is
what the disk cannot say again: the slim's manifest beside its folder, the first time a version was found unused, and each kind's last
run. Sizes are `WiringSizes`' walks, per kind and bounded, with no counters for writers to keep true. The Downloads row keeps its own
list. It also made the slim smaller than either reading asked: package folders only, later, by an hour's evidence.

**The choices this round made**, where two shapes remained: git's tracked files kept rather than guarded (2.2, 2.3, 2.7); package
folders alone rather than every build folder of the list (2.1); the slim an hour after the move in the Keeping pass rather than at once
in the tidy's (2.6, 5.1); a manifest per folder beside it rather than entries in `keeping.json`, so the slim and the record of it have
one writer each and go together (2.4, 4.1); the downloads' use read under the use files' locks with the running session's keep rather
than a lock of the clearing's own (2.6); the browser's caches only through the shell's browser host, and kept with no Daoris running
(2.6); an opinion's packet left with no rule rather than bound to its opinion here (5.5); and the history row kept as the owner decided
it, renumbered KEEP1g.

**Whether it changes a foundation, and who reads next.** It does, in D191's amendment's classes: durable records (`keeping.json`, the
slim's manifest), concurrency (the run lock between a desktop's pass and a terminal alone, `keeping.json`'s writers under one keep
lock, the downloads' judgement held under the use files' locks against a pin's writer), and decided rules other designs rest on (D88's
fourteen days and its purge's read, CACHECLEAR1's judgement, the history clearing design §7, decided by the owner). Fable reads §3 and
§5 of this round, not shown Codex's read first (D191 point 1). Asked hardest: whether §5's evidence is enough for a removal nobody
presses (the producer rule, the hour, the purge's read with step 4's exception, `DeleteNeverFollowing`'s check and the moment it
leaves), and whether `obj/` alone, from which no program runs, may join; whether §3.5's downloads guard closes the race with a pin
(the three locks taken in one order, the pin's writer looking again under its lock) and whether a session's start is the right fact
for a version it may run; and whether §3.1's run lock and §3.3's run id hold when the desktop and a terminal alone meet. Then the
steward checks Fable's findings and settles the design.

**Fable, 2026-10-21, on §3 and §5 of round 2** (D191's note: durable records, removal nobody presses, a lock between processes, and
decided rules others rest on), not shown Codex's read first. Every finding was checked against the code at `2cdc2e8c2` before it was
taken (D160's step 9). All thirteen hold, F2.2 in part, and each is taken in round 3. (B) marks what the read called blocking, (N)
what it did not.

| # | Finding | Checked at | Verdict | Taken in |
|---|---|---|---|---|
| F1.1 | (N) Step 4's exception for the parent's write time hides a file copied or moved into the parent after the move: a copy and a move keep the source's last-write time, and the purge's read checks `LastWriteTimeUtc` only. Record when each path's removal ended and read the parent against that, not the move; check `CreationTimeUtc` too, as `TreeStrays.WrittenSince` does | `TreeSetAside.cs:390-398`, `TreeStrays.cs:287-288` | holds, taken | §2.3 and §5: the manifest's `ended`, step 4; §8; KEEP1a's proof |
| F1.3 | (N) Name the exact residual: a pure-JavaScript program that outlived its session, holds nothing open and writes nothing, failing at its next lazy `require`; one with a loaded addon or its working folder in the tree already refuses the move | `TreeSetAside.cs:219-225` (`busy`) | holds, taken | §5 step 1; §10 *The residuals, exactly* |
| F1.5 | (N) The judgement adds a `git status --ignored` walk inside the repository's hold, beside `NestedRepositories`' own: judge before the hold in `SweepPlanAsync` and check again inside it, or at least share one walk | `AutoTidies.cs:270, 286`, `TreeSweeps.cs:30, 152`, `TreeSetAside.cs:92, 118` | holds, taken whole: git's status read before the hold, the check inside it by the last look's own reads, one walk shared | §5 *The judgement*; KEEP1a |
| F2.1 | (B) The downloads' first-unused time is recorded once and never reset: found unused at T0, pinned at T1, started on at T2 and unpinned at T3, the version reads at T4 as unused since T0 and is renamed under a live session, which an npm-layout agent does not refuse. A pass that finds it used clears its entry | `HarnessSettings.cs:509-516` | holds, taken | §2.1, §2.3, §7, §10; KEEP1b's proof gains the sequence |
| F2.2 | (B) The keep-lock guard binds only the writers that take the lock: `DriverConfig.Save` and `JsonKeeping.Save` take it; `HarnessSettings.Save` only on its keeping path, writing a whole layout unlocked otherwise; `tools.json`'s writer none, D194 deferring it to PUSH1f2c and 2e; the CLI's `writeHarnessSettings` a second writer until PUSH1f2e. KEEP1b after every writer of the three files is under the lock, naming PUSH1f2c and 2e, or carrying it itself with a proof that a whole-layout write waits | `DriverConfig.cs:891`, `JsonKeeping.cs:71`, `HarnessSettings.cs:188-190`, `toolchain.ts:508-510`, `Tools.cs:431-432`, `tools.ts:423-424` | holds, taken; in part for `tools.json`, whose two writers took the lock with KEEPALL1c after D194's read. The harness file's whole-layout write is unlocked on both sides. PUSH1f2c stays named: it reshapes `TOOLS_DELETE` and the download's staging, which KEEP1b's act takes over; merged since (`7f3d80593`), `tools.json`'s one writer holding the lock from its read to its write (`Tools.cs:372, 392, 451-452`) | §3.5 *The guard binds only the writers that take the lock*; §7; §9's order and KEEP1b; §10 |
| F2.3 | (N) Hold the three locks per version, from the judgement to the rename only, never across the removal, so a person's pin or `SET_KEEP` is not refused after the keep lock's ten seconds | `KeepLock.cs:35`, `RecordLock.cs:23` | holds, taken | §3.5, §6.8, §7; KEEP1b's proof |
| F2.4 | (N) Lock the harness file the roster reads, `HarnessSettings.ResolvePath(home)`, which `DAORIS_HARNESS_CONFIG` may name elsewhere, not `harnesses.json` by name | `HarnessSettings.cs:88, 104` | holds, taken; `driver.json` likewise at the path the loop resolves (`DownloadClearing.cs:45-51`) | §3.5; KEEP1b's proof |
| F2.5 | (N) Judge whether a session may still run a version by the binary it resolved, recorded on its session record (an ACP session resolves two); keep the start-time rule for older records | `HarnessRoster.cs:1083, 1134-1143` | holds, taken | §3.5, §6.1, §7, §8; KEEP1b, with the service's session record |
| F3.1 | (N) The run lock is a try, in `TreeLock.TryOpen`'s shape, not `RecordLock`'s ten-second wait; it writes `{ pid, since }` to a sibling file its holder owns, so a refusal names the holder and the card can say a terminal's run is under way | `TreeLock.cs:78`, `RecordLock.cs:23`, `KeepLock.cs:78` | holds, taken, the file naming the door too | §3.1, §4.2, §4.4's `held`, §6.8, §7; KEEP1a, KEEP1c |
| F3.3 | (N) After a crash, the card's `last` may be older than the folders: say so | round 2's §3.3 | holds, taken: the holder's file a dead process left is read as a run stopped before its end | §3.3, §4.2, §10; KEEP1a, KEEP1c |
| F4.1 | (N) The browser's caches below the floor once in each stretch below, reset at `space.ok`, not every hour | round 2's §3.2 | holds, taken | §3.2, §7; KEEP1f's proof |
| F4.3 | (N) Read free space for each kind's own entry path (the trees, the cache), since the trees home may sit on another volume through a junction | round 2's §3.2 | holds, taken | §3.2, §4.1, §4.4's `kinds[].volume`, §7; KEEP1f's proof |
| F5 | (N) `obj/` may join the slim later, on evidence: measured apart in §10's week, then a second producer rule in a row of its own after KEEP1a; `bin/` never, since a program run from it writes beside itself | round 2's §7 and §11 | holds, taken | §7; §9's KEEP1a2; §10 |

**The choices this round made**, where two shapes remained: the judgement both made before the hold and checked inside it, rather
than one of the two (F1.5); KEEP1b after PUSH1f2e (and PUSH1f2c, merged since) rather than carrying their locks, the harness file's
whole-layout write carried only by a KEEP1b built first (F2.2); a session's resolved binaries on its record rather than a lease on
the version, its start kept for older records (F2.5); and the run lock's holder file naming its door, so the card tells a terminal's run under way
from one a crash cut short (F3.1, F3.3).

**Settled.** The steward checked each take against this revision: first-unused reset by a pass that finds the version used; KEEP1b
after every writer of the use files takes the keep lock; the use files' locks held per version, on the path the roster and the loop
resolve; a running session's resolved binaries kept; the purge reading creation times and the parent against the removal's end; the
judgement before the hold; the run lock tried with its holder named; the floor per volume and the browser's caches once per stretch
below; `obj/` on evidence, `bin/` never. Every finding of both reads is taken, and the owner decided §3.6. The design
is accepted (D195's note of 2026-10-21); its rows are §9's, KEEP1a to 1g and KEEP1a2.