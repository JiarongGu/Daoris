# Keeping by size: a limit per kind beside its days, the set-aside folders at every place, and the floor as a share (proposed, 2026-10-24)

**Standing:** proposed, a note amending D195 (KEEPSIZE1). It adds removal nobody presses (a kind over its limit), amends D195's
§3.4 (*nothing that cannot be made again goes before its rule's time*: the time becomes its days or its limit, whichever comes
first) and changes the floor's default, so D191's reads come next: Codex and Fable read it, and the steward checks each finding
against the code and settles it. Nothing here is built. The rows (§6) build it, and the first one (KEEPSIZE1a) is a defect in
the current build that needs no new rule.

Read for it: the row KEEPSIZE1 in `TASKS.md`; D195 and the cleanup design (`docs/2026-10-21-cleanup-design.md`: §1, §2, §3, §4.1,
§5, §7, §9); D172's layout and `DaorisHome.Entry` and `Places` (`DaorisHome.cs:119-164, 265-271`); D51's TREEDEPS1. Every statement
about today was read from the code at `f8e11625b` and names its file and line. None was run.

## 0. The case, measured

**The owner's report** (the row, 2026-10-23): the disk keeps filling, and chat apps bound their caches by size. On 2026-10-24 the
drive held 49 GB free of 1 TB (96% used). That is above D195's floor (20 GB on a 1 TB volume), so the floor never fired. The
install's work folder alone held about 32 GB.

**Measured read-only on 2026-10-24**, under the install's home alone, by a walk that follows no link (Node's `readdir` and `lstat`,
40 minutes over 4.3 million files). Sizes are binary, as Windows shows them. The volume: 1,024 GB, 44.4 GB free; `daoris-driver space
--json` said the floor was 20 GB and the volume was not below it. Daoris's home held about 114 GB, 105 GB of it set-aside trees.

| Kind (D195's table) | Size | Files | Age, growth | Notes |
|---|---|---|---|---|
| Set-aside trees at the trees entry's new place (`work/trees/.tidied`) | 27.9 GB | 466,233 | 77 folders: 6 set aside on 10-22, 71 on 10-23 (26.9 GB) | 74 with a slim manifest. `bin/` 58%, other build folders 14% (an Angular build cache), tracked files and the sessions' own 18%, `obj/` 8%, `node_modules` 3% |
| … at the old place (`data/trees/.tidied`) | **76.7 GB** | 3,489,163 | 164 folders set aside from 10-15 to 10-22: 66 on 10-20 (27.9 GB), 63 on 10-21 (28.7 GB) | **No rule reaches them** (§1.1). 40 slimmed. `node_modules` 44%, `bin/` 30%, tracked and own 12%, `obj/` 8% |
| Live trees (`work/trees`) | 4.1 GB | 263,962 | 6 trees, all opened 10-23 | `node_modules` 55%, an Angular build cache 26%, tracked and own 10%, `bin/` 5%, `obj/` 3% |
| The browser's profile (`browser-engine`) | 872 MB | 8,770 | 360 MB written within the day | the table's caches 767 MB: `Default/Cache` 373 MB, `Default/Code Cache` 305 MB, `component_crx_cache` 62 MB, the rest under 15 MB each |
| Downloaded versions (`toolchain`) | 1.4 GB | 14,307 | written 09-21 to 10-07 | three agents; `keeping.json` names one version unused since 10-21, which goes on 10-28 |
| Agents' account homes (both places) | 1.3 GB | 5,516 | | the agents' own |
| The program (`app/`) | 578 MB | 252 | | replaced by an update |
| The pipe's journal (`run/pipe`) | 391 MB | 3 | about 200 MB a day | its own rule: a day kept after it ends (`PipeJournal.cs:225-240`) |
| The service's copy (`data/bin`) | 369 MB | 126 | | |
| Left by an earlier build (`browser/profile`, `webview2`) | 282 MB | 2,286 | written before 10-01 | the press alone (KEEP1e) |
| Sessions' records (`sessions`) | 182 MB | 1,751 | 09-29 to today; 152 MB of it within the week | history: the person's clear or age |
| The window's profile | 166 MB | 642 | | the UI kit's own |
| The knowledge store | 86 MB | 1 | | |
| Plugins | 54 MB | 1,615 | | |
| The machine log (`logs`) | 26 MB | 94 | 09-29 to today; 24 MB within the week | |
| The rest | under 5 MB each | | | |

**What one tree costs.** A tree of this repository, live, is about 325 to 460 MB: its `node_modules` 254 MB (TREEDEPS1's copy, 61% of
the files), `bin/` 70 to 80 MB and `obj/` 35 to 55 MB once it has built, and 70 MB tracked. Set aside and slimmed, it is about 205 MB:
`bin/` 91 MB, `obj/` 31 MB, tracked and its own 66 MB, and 12 MB of `node_modules` that has no tracked `package.json` above it. The
other workspace's repositories cost more. A .NET service's set-aside folder is 3.5 GB, 98% of it in ten `bin/` folders of about
430 MB each, which the slim never takes (D195 §7). An Angular app's folder is 0.7 to 0.8 GB, most of it its `.angular` build cache.

**What it says.**
- **The set-aside folders are the only kind that grows**, by about 27 GB a day. The slim did not change the rate: 27.9 GB on 10-20
  before KEEP1a and 26.9 GB on 10-23 after it. On 10-20 the bytes were `node_modules`; on 10-23 they were `bin/`, which no rule
  may take before its days. Fourteen days at that rate is about 380 GB, so the floor would fire within the week. Only a limit
  by size bounds this kind short of its days.
- **77 GB lies where no rule reaches** (§1.1): the folders set aside before the home's layout moved them on 10-23.
- **Every other kind is small, or bounded already.** The downloads are 1.4 GB, with a rule. The browser's caches are 0.77 GB.
  The logs are 26 MB, with a ceiling of thirty days of files of at most 20 MiB each (`MachineLog.cs:37, 40`). The sessions'
  records are 182 MB. The pipe's journal is about 400 MB, kept by its own day.
- **The live trees are bounded by the number of sessions**: 4.1 GB in six (§3).

## 1. Today, read from the code

### 1.1 The set-aside folders at the old place: no rule reaches them

The trees entry has two places, `work/trees/*/*/*` and the old `trees/*/*/*` (`DaorisHome.cs:379`). Under an install, the old place is
`data/trees` (`FolderOf`, `:171-183`). A tree is read where it is, at either place (`TreeContext.TreesRoots`, `TreeContext.cs:64`;
`SessionTrees.cs:528`; `TreeFollows.cs:94`). The set-aside root is one path: `TreeSetAside.SetAsideRoot`, which is
`TreesRoot/.tidied` (`TreeSetAside.cs:38`), and `TreesRoot` is `DaorisHome.Entry` asked for the folder (`TreeContext.cs:58`). Once
the new place holds anything, `Entry` answers it (`DaorisHome.cs:265-271`). Every reader of the set-aside folders reads that one
root:
- the purge (`TreeSetAside.cs:366-373`) and the slim (`SetAsideKeeping.cs:51-52, 104-105`);
- the floor's first act (`SpaceFloor.cs:439`) and the press's list (`SpaceFree.cs:291`);
- the look's report (`BranchTidying.cs:250`);
- the card's walk (`SpaceKinds.Places`, `SystemSpace.cs:66-73`) and its *next* (`:573`).

So the 164 folders in `data/trees/.tidied` are never purged, never listed, and never counted as set aside. The card counts them under
*Everything else* (`rest` is the folder less every other kind's places, `SystemSpace.cs:90-92`), where 3.5 million files stop the
walk short. `daoris-driver trees list` names the 77 at the new place and none at the old. The oldest were set aside on 10-15, so
the purge would have taken them from 10-29.

### 1.2 What each kind's rule is

| Kind | Its rule | Its limit today |
|---|---|---|
| `set-aside` | 1 to 90 days, 14 by default; `whole`; the slim an hour after the move (`KeepKinds.cs:17-23`) | none |
| `downloads` | 0 to 365 days into a version's unused stretch, 7 by default, or `never` (`KeepKinds.cs:31-37`) | none |
| `browser-cache` | the engine's own; Daoris starts it with no cache switch (the cleanup design §1.3); cleared by the press and once in each stretch below the floor, only while the shell's host says it is not running (`EngineBrowserHost.cs:113-126`, `BrowserCaches.cs:26-60`) | none: a card row of `own`, `browser` (`SystemSpace.cs:34`), no `KeepKind` |
| `logs` | 7 to 30 days (`KeepKinds.cs:44-47`) | none; 20 MiB per file |
| `history` | the press, or an age the person sets (`KeepKinds.cs:55-61`) | none |
| the floor | the smaller of 10% of the volume and 20 GB (`KeepKinds.cs:180-222`, `FloorPolicy.Of`) | — |

The person's choices are `driver.json`'s `keep` (`KeepChoices`, `KeepChoices.cs:15-60`), judged by `KeepRules` and set through
`SET_KEEP`, `daoris driver keep`, each row's *Change…* (`WiringSpace.tsx:80-140`) and Ask Daoris's `keep` door (D195's KEEP1d note).

## 2. A limit by size per kind, beside its days

**The rule.** A kind that can be made again or let go keeps at most its limit as well as its days, and whichever comes first
applies. Over the limit, the kind's oldest units go first, each through the kind's one removal with every guard it has today, until
the kind is under its limit. A unit that a guard keeps stays, and still counts toward the limit. While it does, the card says by how
much the kind is over and why. **Never one limit for the whole folder**: D195 rejected that, since it would remove what cannot be made
again by size alone (the cleanup design §7), and this design keeps that rejection. Each limit is the kind's own, set on its own row.

### 2.1 Which kinds, and their defaults

| Kind | Its unit, oldest first by | Default limit | The person may set | Why |
|---|---|---|---|---|
| `set-aside` | a folder, by its move (the moment in its name, `TreeSetAside.MovedAt`, `TreeSetAside.cs:333`) | **30 GB** | 1 GB to half the trees home's volume, or `none` | the only kind that grows: about 27 GB a day, so 30 GB keeps about a day of folders beside their days |
| `downloads` | an unused version, by the start of its current unused stretch (`keeping.json`'s `unused`) | none | 1 to 256 GB, or `none` | 1.4 GB, already kept by its days; the limit is for the person who sets long days |
| `browser-cache` | the table's paths, cleared together (§2.4) | **2 GB** | 1 to 64 GB, or `none` | 0.77 GB today, with no bound of Daoris's; a chat app's kind of limit |
| `logs` | — | — | — | not given one (§5): 26 MB, its days the ceiling |
| `history` | — | — | — | not given one (§5): it cannot be made again, and its age is the owner's decision (D195 point 7) |

**Why a default for the set-aside folders, when their days had one too.** A folder set aside is a tree whose work is on the line
and which held no uncommitted or untracked path at its last look (`TreeSetAside.GoesByItself`, `TreeSetAside.cs:13-27`). What is left
that cannot be made again is a session's ignored scratch and what a program wrote beside itself in `bin/`. Fourteen days already
remove both by a default nobody set, and the limit takes the same folders sooner, through the same removal and its guards. Without
a default, the kind holds about 380 GB at the measured rate: the disk the owner reported fills, and the floor fires every day. The
default is 30 GB, about one day's folders. That keeps the newest day whole, for a person reaching back to it.

**Why `none` and not 0.** `none` is the person's choice to keep by days alone, as before this design. A limit of 0 would remove
every folder past its first day, which the days already allow by `1`.

### 2.2 Set-aside folders over their limit

- **Judged at each Keeping pass**, after the purge and the slims (D195's §3.1 steps 3 and 4), under the run lock and within the
  pass's 60 s budget: the kind's total, then the oldest folders past the **settle of one day**, until the total is under the limit.
  A folder moved within the last day is never taken by the limit. It goes by its days, the person's press, or the next day's pass.
  So the kind can stand above its limit by up to a day's folders, and the card says so (*over its limit by 12 GB: 9 folders set aside
  today*).
- **Through the purge itself**: `TreeSetAside.PurgeSetAside(now, after: 1 day, spent, out stopped, only: the folders the limit
  names)` (`TreeSetAside.cs:366`), so every guard of the purge holds. A folder that something wrote into after its move is kept and
  said, `written`. A link outside a build folder keeps it, and so does a folder that does not read (`Kept`, `TreeSetAside.cs:518`). A failed
  delete waits a day (`SetAsideKeeping.Again`). The folder's manifest goes with it. No removal of its own is added.
- **Each folder's size without a walk at each pass.** A folder set aside does not grow: a write after the move keeps it from every
  removal. So its size is read once and kept beside it. The slim writes the bytes left after it into the manifest (`bytes`, a field
  added), and the limit's step walks a folder with no `bytes` once (a whole folder or one from before this build), within the pass's
  budget, keeping the size in the process's memory. The total is the sum. A folder not yet read counts as unread, and while any is
  unread the step takes nothing beyond what the read sizes already show over the limit. The unknown keeps (§6 point 7). This is not a ledger of sizes (the cleanup design §7): each
  size belongs to its own folder, and the folder's removal takes it.
- **Said**: `last.set-aside` as today, with what went by the limit counted under `limit` (`removed` and `kept` by their why);
  `tidied.purged` with `rule: limit`; and the card's *next* reads *over its limit: 3 folders at the next pass, 1.1 GB* where it is over,
  else the days' *next* as today.
- **Below the floor** (§4), the step runs at once rather than at the next pass. The one-day settle still holds, since the floor
  frees what can be made again (D195 §7) and the limit is the person's rule, not the floor's.

### 2.3 Downloads over their limit

The unused versions, by the start of their current unused stretch, oldest first, go through CACHECLEAR1's act (`DownloadClearing.ClearAsync`),
each judged again under the use files' locks as today, so a version that is pinned, used, named, recent or held by a running session
stays (D195 point 6). `never` is the press alone, and that holds against the limit as it holds below the floor (D195's rejection of
*the floor taking a `never` download*). The sizes are each version's folder, walked when the kind is over its limit by the card's
latest count, and only then. The limit is judged once a day with the days' rule (§3.1 step 5), and at once when it changes.

### 2.4 The browser's caches over their limit

The browser's caches are one unit: the table's nine paths (`SpaceKinds.BrowserCaches`). A cache's own index decides which of its
entries is oldest, so deleting the oldest files from outside it corrupts it. Over the limit, they are cleared together through
`BrowserCacheClearing.Clear`, only while the shell's host says the browser is not running (`WhileStoppedAsync`), and the profile's
`lockfile` still keeps them. The check is made at two moments:

- **At the browser's start**, inside the host's start gate, before it starts the browser. This is the one moment the browser is
  known not to run. A walk of the nine paths (about 8,800 files on the install) is bounded by the kind's budget. A walk that
  stops short clears nothing.
- **At the daily pass**, where the browser is not running.

With no shell, a terminal alone keeps them, as today. Bounding the HTTP cache by Chromium's `--disk-cache-size` switch is still not
chosen (the cleanup design §7). The HTTP cache is about half of the caches (373 MB of 767 MB), and the code cache, which the
switch is not known to bound, is the other half. It may come as its own row once that is measured.

### 2.5 The person's choice

`driver.json`'s `keep` gains a `gb` per kind that has a limit. An absent `gb` is the default, and `null` is `none`, read the way the
downloads' `days: null` reads as `never`:

```
"keep": {
  "setAside":     { "days": 14, "gb": 30 },
  "downloads":    { "days": 7,  "gb": null },
  "browserCache": { "gb": 2 },
  "floor":        { "percent": 10 }
}
```

- `KeepKind` gains `MinGb`, `MaxGb`, `DefaultGb` and `MaySetNoLimit`. `KeepKinds.BrowserCache` joins the table as a kind with a limit
  and no days, and its card row turns from `own` to `days` … `size`. `KeepChoices` gains `SetAsideGb`, `DownloadsGb` and `BrowserCacheGb`,
  and `KeepRules.Apply` judges each against its bounds: `KEEP_OUT_OF_RANGE` with `context: gb`, the context the floor's refusal
  already carries (D195's KEEP1f note). The set-aside maximum is half of the trees home's volume as the door reads it, as the floor's
  is.
- `SET_KEEP` takes `{ kind, gb }` and `{ kind, noLimit: true }`; `daoris driver keep set-aside <n>GB|no-limit` and
  `daoris driver keep browser-cache <n>GB|no-limit|default`; Ask Daoris's `keep` door proposes them.
- `SYSTEM_SPACE`'s kinds gain `rule.gb` and `bounds.gb { min, max, none }`, and the card's editor a size field beside the days
  (`WiringSpace.tsx:80-140`). These are added fields: an older page reads none, and offers days alone.
- `KeepPolicy.Unread` keeps no limit: a choice that does not read keeps the most.

### 2.6 The card

```
 Set-aside trees ⓘ              27.9 GB · 77 folders · counted 10:42
 daoris driver keep set-aside … kept 14 days or 30 GB, whichever comes first; package folders go after an hour   [Change…]
                                over its limit by 0 · next: 6 folders on 5 Nov, 1.1 GB · last: 23 Oct, 8 removed, 1.1 GB
 The browser's cache ⓘ          0.8 GB · kept to 2 GB; cleared when the browser starts over it          [Change…]
```

The words are new keys in both languages (*whichever comes first* / *以先到者为准*; *over its limit by* / *超出上限*), the glossary
unchanged.

## 3. A tree's build outputs and dependency folders

**What they cost** (§0): a live tree of this repository is 325 to 460 MB, 254 MB of it `node_modules` and up to 135 MB `bin/` and
`obj/`. The six live trees held 4.1 GB. Set aside and slimmed, a tree keeps about 120 MB of build outputs. That is 60% of what is
left of it, and three quarters of the bytes added each day.

**Whether a tree no session holds may keep them: yes, while it is live.** A live tree is bounded by the sessions. Six held 4.1 GB,
and the cap, the clean-up and the look's move keep their number small. D195 rejected slimming a live tree, even an idle one's: it is
a session's workplace (D51), words may go on in it (D137), and a session resumed there would rebuild in its turn (the cleanup design
§7). The measurement leaves that as it is. A tree waiting days on its review (REVIEWLANDED1's case) costs about 0.4 GB, and the
cure for it is that row. Dropping its `node_modules` would save 254 MB and cost TREEDEPS1's copy again at the go-on, a path the go-on
does not take today (D51's TREEDEPS1 copies when a tree opens). That is not worth a new removal in a live tree.

**Once set aside, they are what the limit takes.** `bin/` is never slimmed (D195 §7), and `obj/` waits for KEEP1a2's evidence. On the
install, `obj/` is 8% of a slimmed folder's bytes and `bin/` 58%. So KEEP1a2 alone would not bound the kind, and the limit does.
An Angular repository's `.angular` build cache is 14% of the new place: a producer's cache, discarded whole by `ng cache clean`. It is
evidence for KEEP1a2's *second producer rule* beside `obj/`, written into that row (§6), not a rule this design adds.

## 4. The floor, reconsidered as a share

The floor did not fire at 96% used, because 20 GB is 2% of the volume. Its default was the smaller of 10% and 20 GB, so that the
floor would not fire at hundreds of GB free on a large disk (the cleanup design §3.2). Read against the case, a cap that keeps a
1 TB disk at 98% before Daoris frees anything is too low. What the floor does below is cheap and harmless: the slims due, unused
downloads, the browser's caches once in a stretch, and from this design each kind's limit at once. Each of these can be made again,
and the row in *What needs you* is said once in each stretch.

- **The default becomes 10% of each volume**, with the 20 GB cap removed. A 1 TB volume's floor becomes 102 GB, so the install
  would have been below it on 10-23 (79 GB free), and on 10-21 it was above it (143.5 GB free, the cleanup design §0.2). A 128 GB volume's stays 12.8 GB. The margin that ends a stretch stays
  the smaller of 5 GB and 5% (`FloorPolicy.Margin`).
- The person's size (1 GB to half the volume), share (1% to 50%) and `off` stand as KEEP1f built them (`FloorPolicy`,
  `KeepChoices.cs:90-95`). A floor the person set is theirs, and the default's change does not touch it.
- **A large disk**: a 4 TB volume's default floor is 410 GB. A person whose own files keep it below that sees the row once and sets a
  size. That is a smaller harm than a 1 TB disk filling unannounced, and the press stays the person's in either case.
- `FloorPolicy.Said` and the card's words drop *the smaller of 10% of a volume and 20 GB* for *below 10% of a volume free*.

## 5. Rejected

- **One limit for the whole Daoris folder**, oldest first (D195 §7, kept): it removes what cannot be made again by size alone.
- **A limit for the machine log**: 26 MB on the install. Its worst is thirty days of files of at most 20 MiB each, per process, and
  its days (7 to 30) are already the person's lever.
- **A limit for finished history**: it cannot be made again, and the owner decided its rule is an age the person sets (D195 point 7).
- **Taking a set-aside folder's build folders first under the limit, then the folder**: a second removal with a manifest state of
  its own, to keep a folder a day longer, when the limit takes it soon anyway. The whole folder goes through the purge's one
  removal.
- **The limit taking a folder in its first day**: it is the day a person reaches back to most, and a write that raced the move shows
  then.
- **Clearing the browser's cache file by file, oldest first**: the cache's own index decides what is oldest, and an outside delete
  corrupts it.
- **Dropping an idle live tree's package folders** (§3): 254 MB a tree, against a copy at the go-on that the go-on does not make today.
- **A limit for the pipe's journal**: about 400 MB, kept a day after its day by its own rule. A bound per day belongs to its own
  writer, not to keeping.
- **Keeping the 20 GB cap on the floor's default**: the case shows it holds a 1 TB disk at 98% used before anything is freed.

## 6. The rows, in build order

Each is landable alone, with its proof seen failing first, and runs its own tests and the quick checks. The merge and the CI beside run
the rest.

- **KEEPSIZE1a — the set-aside folders at every place of the trees entry** (driver; a defect, buildable now under D195 as it
  stands). The purge, the slim, the floor's first act, the press's list, the look's report, `trees list`, the work inventory and the
  card's walk and *next* read `.tidied` under each of `TreeContext.TreesRoots`, not under `TreesRoot` alone. A folder moves into the
  new place's `.tidied` as today. A folder at the old place with no manifest is purged at its days and never slimmed (D195 §7:
  *slimming the folders already set aside without git*). Proof: a home holding a set-aside folder at each place, both past their
  days, both purged; both listed and counted under `set-aside`, and none under `rest`; one written into after its move at the old
  place kept and said; `trees list` naming both.
- **KEEPSIZE1b — the floor's default as a share** (driver, web words). `FloorPolicy.Of`'s default is 10% of the volume, without the
  cap; `Said`, the card's words and Ask Daoris's card follow. Proof: `FloorPolicy.Of` on 1,024 GB is about 102 GB and on 128 GB
  12.8 GB; a size or share the person set is unchanged; `SpaceFloorTests`' below and above cases at the new default; the card's below
  story in both languages.
- **KEEPSIZE1c — the set-aside folders' limit, with the limit's foundations** (driver, modules, web). §2.2 and §2.5: `KeepKind`'s
  limit fields, `KeepChoices.SetAsideGb`, `KeepRules`' bounds and refusal, `SET_KEEP`'s `gb` and `noLimit`, the terminal's
  `<n>GB|no-limit`, Ask Daoris's door, the manifest's `bytes`, the pass's limit step through `PurgeSetAside`'s `only`, the floor
  running it at once, `SYSTEM_SPACE`'s `rule.gb` and `bounds.gb`, the card's words and the editor's size field. Proof: folders over
  the limit going oldest first until under it; one in its first day kept; one written into after its move kept, said and counted;
  an unread size taking nothing beyond what is read; `none` taking nothing; a size out of bounds refused with `context: gb` on every
  door; the both-ways test over the new choices; the card's over-limit state in both languages.
- **KEEPSIZE1d — the downloads' limit** (driver, web). §2.3. Proof: unused versions over the limit going by their unused stretch,
  oldest first; a pinned, used or running version kept; `never` taking nothing; the days and the limit together, whichever comes first.
- **KEEPSIZE1e — the browser's cache limit** (driver, shell, web). §2.4: `KeepKinds.BrowserCache`, the check inside the host's start
  gate and at the daily pass, the row turning settable. Proof: caches over the limit cleared at a start, before the browser runs; a
  running browser's caches kept at the pass; a walk stopped short clearing nothing; no shell keeping them; the row's words in both
  languages.
- **KEEP1a2** (open, D195) gains the measurement above as its first week's evidence: `obj/` is 8% of a slimmed folder and `.angular` is
  a second producer's cache.

## 7. What this amends, once accepted

- **D195 point 4 and the cleanup design §3.2**: the floor's default is 10% of each volume; below it, each kind's limit is judged at
  once.
- **D195 point 2 and the cleanup design §2.1 and §2.2**: `keep` gains each kind's `gb`, and `browser-cache` becomes a kind with a
  rule of Daoris's.
- **The cleanup design §3.4**: *before its rule's time* reads *before its days or its limit, whichever comes first*.
- **The cleanup design §3.1**: the Keeping pass reads every place of the trees entry, and step 4 is followed by the limit's step.
- **The cleanup design §4.1**: the card's rows say a limit beside the days.
