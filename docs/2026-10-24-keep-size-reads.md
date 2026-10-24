# KEEPSIZE1: the reads of rounds 2 and 3 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of round 2 of `docs/2026-10-24-keep-size-design.md` and its note in D195 at `a2993a3bb`, each verbatim but for its headings set one level down; round 1 was rewritten to the owner's directions before any read. The steward ran both from outside the install, read-only, on the design's own tree. Codex asks another round (14 findings, 11 blocking); the Opus foundation reader would accept after named changes (22 findings, 4 blocking). Round 3 answers both. Round 3 was read by the Opus foundation reader alone, its read following theirs (11 findings, none blocking, accept after named changes); Codex's read is owed on the text the named changes leave.

## Codex

Codex's read of round 2.

### 1. Claims about the code

Round 2 correctly identifies the old-place defect, the carried branch’s missing recovery ref, and the repository feed’s replacement of indexed entries. Several other claims need correction:

- **A squashing landing does not necessarily hold the original session commits.** The table at `docs/2026-10-24-keep-size-design.md:80` is wrong. `TreeSquashes.cs:26` creates a new commit with the source tree and a different parent; `TreeSweeps.cs:337` accepts the squash’s recorded content hold. A recovery ref subsequently preserving the landing branch preserves that squash history, not necessarily the original session history. Finding 1.
- **The purge does not reject every internal link.** The claims at design lines 92 and 193 omit its deliberate exception: `TreeSetAside.cs:585` permits links under build folders, deleting their entries without following them. Finding 14.
- **Fifty later landings do not necessarily erase the attribution records.** Design lines 102–107 oversimplify retention. `LandedBranches.cs:572` bounds retired traces, ordered by retirement; `SessionBranches.cs:241` additionally protects open units and traces following live trees. Finding 14.
- **The floor does not bound the retained whole trees.** Design line 225’s “held short only by the floor” is unsupported. The existing floor slims eligible package folders; it does not purge the remaining `bin/`, tracked files and scratch early. Finding 14.
- **The stated log bound is wrong.** Design line 266 overlooks separate source files: `Platform/MachineLog.cs:349` names files by day **and source**. Finding 14.
- **Not every proposed default part exceeds the measured usage.** Design line 272 says this, but its 8 GB set-aside part is below the reported 27.9 GB. Finding 14.

Code filenames below are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless a service or web path is stated.

### 2. The mechanism’s holes

The central missing contract is a deletion eligibility judgement shared by the hour purge, ordinary purge, allowance and manual cleanup. A saved-record marker and an old folder timestamp cannot alone establish that the folder contains only disposable material.

The existing move has meaningful protections: `TreeSweeps.cs:302` excludes trees in use; `Trees/Branches/BranchTidying.cs:143` includes active sessions, waiting words, helper waits and reviews; the tidy reads use before and inside its repository hold. Those protections should remain explicit prerequisites. Matching `Session.Tree` identifies an association; it does not independently prove a session finished or that its complete record was captured.

Manual cleanup may remove regenerable caches, eligible unused downloads, proven redundant trees and explicitly disclosed set-aside contents the person chooses to discard. It must preserve live workplaces, unresolved uncommitted work, unheld commits, required unsaved records, protected downloads, history through this door, recovery refs, credentials and unrelated paths. The design’s “may never” list points in that direction, but neither the current early remover nor the proposed allowance enforces all those distinctions.

The allowance must stop when no eligible unit remains—even where the obstruction is a running browser, `never`, a settle period, unread data or an unrecorded folder rather than an inherently permanent kind. That condition needs a visible explanation and another judgement when the obstruction changes. It cannot truthfully promise a hard ceiling while protected writers may continue growing.

KEEPSIZE1a remains necessary. Reaching both places must also preserve which physical place each listed unit belongs to, not merely enumerate two roots.

### 3. The rows

KEEPSIZE1b targets the largest **remaining reported** source of reclaimable space on the owner’s install. That is the right target, but the claimed recovery of all 27.9 GB is conditional on attribution, record and commit-hold proofs that the backfill currently assumes.

| Row | Buildable alone, in this order? | Does its proof establish its claim? |
|---|---|---|
| **1b: record and hour purge** | Not safely as specified. It needs the deletion eligibility and failure protocol settled first. | Missing squash-source history, pre-move edits, refused branch deletion, ambiguous session attribution and irrecoverable backfill cases. |
| **1a: every place** | Yes, with place-aware identities and the shared safety rules. | The two-place proof also needs identical relative folder names and manifests at both places. |
| **1c: searchable records** | Yes after 1b’s durable record contract. | Search hits alone do not prove that the complete record can be retrieved, that old 1b records are indexed, or that clearing removes both forms. |
| **1d: allowance** | Its browser part presently depends on work assigned to 1e. | Missing arithmetic edge cases, stale counts, blocked parts below the whole allowance, and migration of folders without byte manifests. |
| **1e: browser start check** | Yes if 1d already supplies the kind and stopped-browser removal rule. | The inherited proof is useful; the division of responsibility between 1d and 1e needs correction. |
| **1f: warning-only floor** | Yes after the replacement allowance acts work. | Add preservation of warnings for every affected volume, including an external trees volume. |
| **KEEP1a2** | Remains a separate evidence-led change. | A measured size share establishes potential benefit, not that arbitrary `obj/` contents can be regenerated. |

### 4. Findings

1. **Blocking — the squash path can lose original commits.**  
   **Where:** design `:80`, `:188`; `Trees/SessionTrees/TreeSquashes.cs:26`, `TreeSweeps.cs:337`, `LandedCleanups.cs:330`.  
   A landing branch can hold the squash without holding the session commits the record promises to recover. Adding a ref only for `Carried` leaves this case open. **Smallest change:** require ancestry protection of the exact source tip; create a recovery ref whenever existing durable holders do not provide it. Prove a multi-commit source survives removal of both session and squash-landing branches.

2. **Blocking — the hour does not cover every late uncommitted write.**  
   **Where:** design `:193`, `:415`; `TreeSweeps.cs:308`, `TreeSetAside.cs:203`, `:273`, `:586`.  
   A write after the clean judgement but before the destination timestamp is chosen predates the purge’s threshold. Waiting an hour does not make `WrittenSince` detect it. The new service post can lengthen this interval. **Smallest change:** specify a post-move validation while git metadata remains usable, preserving the branch and folder on newly found edits or an unread validation. Add that exact race to 1b’s proof.

3. **Blocking — the manifest authorizes deletion before the removal outcome is settled.**  
   **Where:** design `:201`; `TreeSweeps.cs:196`, `:243`; `TreeSetAside.cs:218`.  
   The tree moves before `DeleteJudgedAsync`. That transaction can refuse, and its failure path can drop the temporary recovery ref. A marker written merely after the record POST would nevertheless authorize the hour purge. The later purge also checks neither the record nor the current commit holder. **Smallest change:** define a recoverable transition that arms deletion only after successful removal, bound to the exact recorded tip and hold. Revalidate eligibility before destructive deletion; prove refusal and restart boundaries.

4. **Blocking — fallback and allowance paths bypass the record prerequisite.**  
   **Where:** design `:170`, `:218`, `:240`, `:344`; `SetAsideKeeping.cs:36`, `:81`; `TreeSetAside.cs:401`.  
   “No answer, no tree goes” becomes deletion after its days—or after one day under the allowance. “Any other” set-aside folder also includes material for which neither a finished record nor a commit hold has been established. Current purge and early-removal guards do not establish those facts. **Smallest change:** classify folders explicitly and apply shared eligibility to every removal door. A finished session awaiting its record must stay until that record is durable; unknown folders must not become copies merely through age.

5. **Blocking — naming scratch does not preserve needed evidence.**  
   **Where:** design `:146`, `:209`, `:248`; `TreeSetAside.cs:104`.  
   The design deliberately destroys unique scratch, including potentially a local database or ignored nested repository, while assuming working rules have transferred everything needed. No mechanism verifies that assumption. It also excludes producer folders from the inventory while promising to name the database in `bin/`. **Smallest change:** require completion of the durable text/evidence record before automatic cleanup; unknown required material blocks it. Explicit manual disposal must disclose the irreversible contents. Define the inventory’s producer-folder exception consistently.

6. **Blocking — retry, backfill and session attribution lack sufficient identity.**  
   **Where:** design `:168`, `:178`, `:382`; `Trees/Branches/SessionBranches.cs:70`; `SlimManifest.cs:21`.  
   Several session rows can name a reused tree. Its branch-wide commit range is not automatically each session’s range. After branch removal and trace expiry, the session’s base plus today’s line cannot reconstruct the missing final tip. A readable folder is not proof that those facts remain recoverable. **Smallest change:** capture exact session associations, source boundaries and tip before removal. Keep new branches/trees when capture fails. Backfill only what surviving evidence proves, recording gaps without inventing attribution; test continuations and expired traces.

7. **Blocking — the allowance’s bounds and division are inconsistent.**  
   **Where:** design `:258`, `:263`, `:269`, `:328`; `Platform/KeepChoices.cs:174`.  
   Three independently settable parts may each equal N, leaving a negative “rest.” At N = 8 GB the browser default is 0.4 GB, below its 1 GB minimum. The 32 GB default can exceed half a small volume. Fractional examples also exceed today’s integer `Gb` representation. **Smallest change:** define allocation-sum validation, effective defaults on small volumes, fractional-byte conversion and the result of lowering N. Prove those cases through every setting door.

8. **Blocking — cached sizes can authorize excessive deletion or miss pressure.**  
   **Where:** design `:277`; `Platform/SystemSpace.cs:143`, `:199`; `Trees/Branches/BranchTidying.cs:279`.  
   Counts can precede the purge/slim earlier in the same pass. Reusing that total can delete additional folders after enough space has already gone; repeated passes may reuse it while remeasurement waits. Conversely, daily counts miss intervening growth. Legacy, `whole` and failed-slim folders also need a defined route to trustworthy byte counts. **Smallest change:** define count invalidation and subtraction across each act, reject stale destructive calculations, and trigger bounded recounts on relevant changes. Prove that a preceding purge prevents further unnecessary deletion.

9. **Not blocking — record bytes have no physical accounting rule.**  
   **Where:** design `:174`, `:233`, `:308`; `Platform/SystemSpace.cs:81`, `:90`.  
   Today `history` walks session/workflow files; `knowledge.db` falls under `rest`. Adding logical record bytes to history while retaining the physical database in rest breaks the advertised total. **Smallest change:** count the database physically once and distinguish any logical record-size detail from additive disk usage.

10. **Blocking — the old-place extension needs place-aware unit identities.**  
    **Where:** design `:388`; `SetAsideKeeping.cs:309` (`SetAsideListed.Id`); `Platform/SpaceFree.cs:291`, `:296`.  
    A unit currently identifies only workspace/repository/folder, and removal resolves it under one root. The same relative name can exist at both places. Enumerating both roots without changing that contract can select or remove the wrong unit. **Smallest change:** carry a validated place discriminator through listing, selection, removal, sizing and reporting. Prove two identical relative names, selecting either independently.

11. **Blocking — a searchable hit has no specified complete-record read.**  
    **Where:** design `:157`, `:163`, `:392`; service `Knowledge/KnowledgeService.cs:248`, `Knowledge/Store/SqliteKnowledgeStore.cs:222`; MCP `KnowledgeSearchTools.cs:149`.  
    `knowledge_get` resolves entries from `entries`, while the proposed `record:<session>` lives elsewhere. A session-page link does not specify how it displays the new landing, commits, ref and scratch record. The capped FTS text is not that complete record. **Smallest change:** specify scoped retrieval through the knowledge API and session page, with a search→read→clear proof and indexing of records created before 1c.

12. **Blocking — 1d depends on a browser rule assigned to 1e.**  
    **Where:** design `:329`, `:396`, `:402`; `Platform/KeepKinds.cs:64`; `Daoris.Desktop.App/EngineBrowserHost.cs:113`.  
    Row 1d promises all three parts and their removals, but 1e introduces `KeepKinds.BrowserCache` and the daily/start checks. This does not establish that each row’s displayed rule already runs. **Smallest change:** put the browser kind, policy, stopped-browser allowance act and daily judgement in 1d; leave only the startup integration to 1e. Make each row’s proof assert its own promised behavior.

13. **Not blocking — blocked parts and policy exceptions are underspecified on the card.**  
    **Where:** design `:285`, `:298`, `:300`.  
    A kind can exceed its part while total usage remains below N, without triggering the described whole-allowance warning. The mockup also omits `whole`, record failures, protected-young folders and history’s configured age. **Smallest change:** show each blocked over-part condition, its reason and next opportunity; derive displayed retention and next cleanup from the effective policy. Preserve “at least” and unknown states in the total.

14. **Not blocking — correct the remaining factual prose and its repeated claims.**  
    **Where:** design `:92`, `:102`, `:225`, `:266`, `:272`; corresponding sources listed in section 1.  
    These are the link exception, trace-retention qualification, ineffective floor bound, per-source log files and default-versus-measurement comparison. **Smallest change:** correct those statements, qualify the projected space recovery, and carry the corrected wording into D195 where repeated.

**14 findings; 11 blocking.**

**Verdict: another round.** The record-first direction and the priority given to finished trees fit the owner’s frame. The next revision needs a coherent deletion eligibility and recovery protocol covering squash history, late edits, failed removal, missing records and legacy attribution, together with consistent allowance arithmetic and row dependencies. Those choices determine what may be irreversibly lost and need another read before implementation.

## Opus (foundation)

The independent Opus 5.5 reader's foundation read of round 2.

## KEEPSIZE1 round 2: the foundation read (D191)

**Reader:** Claude Opus 5.5, in Fable's place (D191's 2026-10-23 amendment). **Read:** `docs/2026-10-24-keep-size-design.md` whole,
D195 whole with its round-2 note, and `git diff HEAD~1 HEAD` on both (the tree at `1c1cee999`). **Against the code:** the trees'
set-aside, sweep, look and landing code (`TreeSetAside`, `TreeSweeps`, `AutoTidies`, `SetAsideKeeping`, `SlimManifest`,
`ContentHolds`, `TreeScratch`, `TreeStrays`, `PassLeftovers`, `LandedCleanups`, `LandedBranches`, `SessionBranches`,
`RecoveryRetention`, `RecoveryDropping`, `BranchTidying`), the keep rows' code (`KeepKinds`, `KeepChoices`, `SpaceFree`,
`SpaceFloor`, `SystemSpace`), and the service's knowledge store, search, sessions and history clear (`SqliteKnowledgeStore`,
`KnowledgeService`, `KnowledgeIndex`, `ServiceFactory`, `Sessions.cs`, `Asks/History.cs`). Nothing was run. Driver paths are under
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, service paths under `src/Daoris.Service/Daoris.Service.Core/`.

Most of §1's claims hold as cited: `GoesByItself` (`TreeSetAside.cs:26-29`), the five holds and their lines in `TreeSweeps.JudgeAsync`
(`:290-434`), the carried row's missing ref (`TreeSweeps.cs:181`), the move (`TreeSetAside.cs:190-235`), the purge and its guards
(`:366-424`, `:518-593`), the 50 traces (`LandedBranches.cs:320, 565-574`; `SessionBranches.cs:85`), the branch's name
(`SessionTrees.cs:354-355`), the sessions on the knowledge store's connection (`ServiceFactory.cs:296-298`), the `.tidied` readers
(`TreeSetAside.cs:38`, `SystemSpace.cs:64-96`), and `FloorPolicy` (`KeepKinds.cs:180-235`). What follows is what does not hold.

### 1. Claims about the code that are wrong

1. **§2.1, the scratch's source: "the last look's walk: each ignored path outside a producer's folder, by name and size".** The last
   look's walk (`TreeSetAside.NestedRepositories`, `TreeSetAside.cs:132-175`) names nested repositories and links. It reads no ignored
   path and no size. What a tree holds that no commit holds is `ContentHolds.HoldsAsync` at the judgement (`ContentHolds.cs:560-607`):
   `Ignored` (ignored paths only the tree has), `Written` and `TooLarge` (TREEKEEP1), and `Shared`. `--ignored=matching` names a folder
   ignored whole once, and no size is read. The look keeps only the shared count from it (`TreeSweeps.cs:310`). "Outside a producer's
   folder" also leaves out everything under `bin/` (`TreeScratch.cs:15-19`), so §3.2's own example, "a local database a program wrote
   in `bin/`", is never named. That contradicts "the record names each such path and its size".
2. **§4.3: "a set-aside folder's size is the manifest's `bytes`, written by the move and the slim (round 1's §2.2)".** No `bytes`
   field exists (`SlimManifest.cs:21-22`). Round 1's §2.2 had the slim write it, plus one walk for a folder without it. Round 2 drops
   the walk and makes the move write it, which would mean a walk inside the repository's hold. **§3.1's "the move writes, beside the
   slim's manifest, `record`"** hits the same gap: the move writes a manifest only where the tree has a droppable package folder and
   `whole` is off (`TreeSetAside.cs:204`; `AutoTidies.cs:271-277`). A stray or a leftover never gets one (`MoveFolderAside`,
   `TreeSetAside.cs:245-265`). So a tree without a `node_modules`, like the .NET service §4.2 sizes from, has nowhere to hold
   `record` or `bytes`.
3. **§5: "a finished session's tree within its hour, its record kept first (§3.1 steps 1 and 2 run at the press as at the pass)".**
   The press can already do this. Its early group lists every folder that is not past its date, at any age, the first hour
   included (`SpaceFree.cs:163`), and `RemoveEarly` has no age gate and needs no record (`SetAsideKeeping.cs:78-99`). Steps 1 and 2
   also cannot run at the press: the session branch is deleted in the same sweep as the move (`TreeSweeps.cs:239`), so no
   `git log` of the branch and no ref write is possible by then.
4. **§2.2: "a record the service did not take keeps the folder … and the next pass tries again".** No later pass meets that
   session again. Its branch went at the move (`TreeSweeps.cs:239`, `AutoTidies.cs:303`), and the look's candidates are the
   standing `daoris/*` branches with a kept fact (`AutoTidies.cs:218-254`). Nothing retries, and nothing would know what to retry
   from (finding 6).
5. **§4.1 step 1: "Set-aside folders (copies: the repository holds their commits)".** This is true only of a session tree that
   went by `GoesByItself`. The kind also holds strays, "a folder git lists no tree in: one a removed tree left, a half-removed
   tree's, or one a session wrote beside its tree", moved aside "so nothing in it is lost before the purge's fourteen days"
   (`TreeStrays.cs:12-13, 41-56`). It also holds a pass's leftovers (`PassLeftovers.cs:20-25`), and both are moved by
   `MoveFolderAside` with no judgement of their commits.
6. **§1.1's table: the line "by ancestry (`refs/heads/<line>` or `origin/<line>`)" counted as a hold that stays.** The code's own
   rule refuses a remote-tracking ref as a holder, since "a fetch moves or prunes" it (`RecoveryRetention.cs:12-14`). This changes
   nothing about today's removal of the branch, but §3.1 step 1 calls it "a ref that stays", and the record should say which form
   held.
7. **§1.3: "replaced whole at each feed (`ReplaceRepositoryAsync` … called at `KnowledgeService.cs:655`)".** That line is the fed
   (shared) path. The local refresh replaces at `KnowledgeIndex.cs:41`, and `:61` and `KnowledgeService.cs:357, 560` empty a gone
   or retired repository. The conclusion holds. Left out: the store calls its index "derived data", with "no migrations,
   deliberately", and drops `entries` and `entries_fts` on any schema bump (`SqliteKnowledgeStore.cs:13-21, 112-117`). That matters
   for the first store of non-derivable records written beside it (finding 13).
8. **Standing line and D195's note: "Round 1 (the same day, at `47c9b78a9`)".** The file does not exist at `47c9b78a9`. Round 1 is
   `d57b7f8d6`, merged at `16769207d`. Rows KEEPSIZE1a and 1e take "round 1's" proofs, and §4.3 takes round 1's §2.2, so the
   pointer is load-bearing.

### 2. The mechanism's holes

**What the folder's removal can lose.** A set-aside folder holds no git objects. Its `.git` is a file, and `git worktree remove`
drops the record it pointed to (`TreeSetAside.cs:215`). So whether a commit is lost is decided when the branch is deleted, which
already happens at the move today. Deleting the folder after an hour instead of fourteen days loses three things:

- the tip's checkout, which is a copy wherever a ref holds the commits;
- the ignored paths;
- a write that came after the move.

On the foundation's points:

- **Uncommitted work holds.** Tracked changes and untracked files make the tree `Dirty` (`TreeSweeps.cs:311`; the porcelain read with
  `--untracked-files=all`, `ContentHolds.cs:562-563, 601`), so no such tree is moved.
- **A tree a session is in holds.** It is `InUse` at the judgement (`TreeSweeps.cs:302`), the sessions are asked again inside the
  hold (`AutoTidies.cs:293`), and Windows refuses the rename of a folder with an open file (`busy`, `TreeSetAside.cs:260-264`). One
  edge is new at an hour: the purge deletes file by file and leaves a partial folder when a file is held open
  (`TreeSetAside.cs:596-640`). At fourteen days that rarely met a person reading the tree. At an hour it will (finding 19).
- **A commit no recovery ref holds holds**, once the carried tree's ref lands. The carried case is the one where the folder's
  checkout is today the only local copy of the tip's content. The new ref closes it (§3.1 step 1). It must cover the press's path
  too (finding 21).
- **A record not yet kept** holds at the pass: the mark is set only once the service answered. It does not hold under the
  allowance, which takes "any other past its first day", nor at the press, which lists "any other set-aside folder before its
  date" (findings 2 and 3). §8 rejects exactly this: "letting go of a tree before its record is kept".
- **What cannot be made again.** The hour removes the tree's own ignored scratch by a rule nobody presses. The code already tells it
  apart from build output: `TreeHolds.Ignored`, `Written` and `TooLarge` (`ContentHolds.cs:552-559, 620-622`). The person's own
  press refuses a tree that holds it (`TreeSweeps.cs:314`). The look takes such a tree today only "since the look moves its folder
  aside for fourteen days rather than deleting it" (`TreeSetAside.cs:16-21`, `TreeSweeps.cs:312-313`). The hour removes that
  ground, and it reverses D195 point 6 ("only what is known to be made again goes by itself before its time"). The owner's
  direction contrasts the record with "a copy of the whole code the repository already holds". It does not name scratch, which is
  not a copy. That makes it the owner's call or a narrower rule's, not the design's (finding 1).
- **The manual cleanup's bounds** contradict each other. §5 keeps "any other set-aside folder before its date … as today", and also
  never removes "a set-aside folder whose commits no ref that stays holds". For a folder whose branch is gone, the press has no tip
  (`SlimManifest.cs:21-22` holds `Branch` and no commit), so it cannot judge the second rule. Built one way the second rule does
  nothing. Built the other way the person can no longer clear strays or old folders by hand, against the owner's "clean up by hand
  at any time" (finding 3).
- **The allowance's arithmetic** when what may go cannot bring Daoris under N is unsettled. §4.1 ends "says so and removes nothing
  more", while §7's proof is "the whole over N going in order and stopping at what cannot go". At the default, N = 32 GB with parts
  of 8 + 1.6 + 4.8 = 14.4 GB, which leaves 17.6 GB for what cannot go. §4.2's own sizing case is about 30 GB of that (six live
  .NET trees and the rest). There the whole is over N for good:
  - each pass would empty the browser's caches and every unused download past its hour, with no gain;
  - `space.over` would stand as a permanent row;
  - the downloads would be fetched again on their next use (finding 4).

  Two smaller gaps sit beside it:
  - **Double counts.** A tree counted at the day's walk and set aside since is counted twice: once under live trees, once by its
    folder's bytes (finding 16).
  - **`whole` overridden.** Under `whole` the allowance still takes finished folders past their hour (finding 17).
- **The record's mechanism** has five gaps:
  - **The mark's timing.** The mark is written at the move, before the branch delete, which may be refused (finding 5).
  - **Retry inputs.** A retry has nothing to compose the record from (finding 6).
  - **Missing manifests.** Trees without a manifest never get the mark (finding 7).
  - **The key.** The record is keyed per session, though several sessions can name one tree (finding 10).
  - **The backfill's proof.** The backfill cannot prove step 1, or see the scratch, from a folder whose git record is gone
    (finding 11).

  The store, the search and the counting need stating too (findings 13 to 15).
- **The hold.** Composing the record (`git log --numstat`, a read of `events.jsonl`, a post to the service on loopback) inside
  `TreeLock.TryReplaying` (`AutoTidies.cs:291-297`) lengthens what every start in that repository waits on. KEEP1a kept that hold
  free of new git (Fable F1.5) (finding 18).
- **Two purges in one pass** (the days' purge and the hour's) overwrite `purge-failed.json` in turn (finding 8).

### 3. The rows

- **KEEPSIZE1b (the record, and the tree at its hour) gives the most space back, and is buildable alone.** It needs neither 1a nor
  1c: the hour's purge reads `.tidied` at the new place, where every move now lands. Its proof covers the happy path, the carried
  ref, a silent service, a write after the move, `whole`, the history clear and the backfill. It proves the claim only once these
  are added, each a case the text leaves open:
  - a tree with no package folder getting its mark (finding 7);
  - a branch delete refused after the move keeping the folder for its days (finding 5);
  - a tree with its own scratch waiting its days, if finding 1's narrower rule is taken;
  - a stray and a leftover never purged at the hour;
  - `purge-failed.json` keeping both purges' folders (finding 8);
  - the single-session delete taking the record (finding 13);
  - a second session in one tree (finding 10);
  - the carried ref on the press's path (finding 21).

  The backfill as written is not safe to prove (finding 11). Its gain is one-off: at most the 27.9 GB of folders now set aside,
  which the person's press can already free and whose days end by 11-06.
- **KEEPSIZE1a (every place) is buildable alone and placed right**: before the allowance counts the kind. Its proof lives only in
  round 1's text, at the wrong pointer (claim 8).
- **KEEPSIZE1c (search) comes after 1b and is buildable alone.** Its proof needs:
  - a stated merge of two FTS5 rankings, and the hybrid tier (finding 14);
  - a no-kinds search not crowded by 256 KB conversations;
  - records written by 1b before 1c indexed from `events.jsonl`;
  - the audience of a hit that holds the person's words.
- **KEEPSIZE1d (the allowance) comes after 1a.** Its proof list is good. It lacks:
  - the unreachable-N case (finding 4);
  - strays and unrecorded folders never taken (finding 2);
  - a tree moved since its count not counted twice (finding 16);
  - `whole` (finding 17);
  - a stored part above a lowered N (finding 22).

  It also names `KeepKinds.BrowserCache` (§4.6), which row 1e names too.
- **KEEPSIZE1e (the browser's caches)** comes after 1d, rightly, since it needs the part. Its proof is round 1's (claim 8).
- **KEEPSIZE1f (the floor as a warning)** comes after 1d, rightly. Its proof proves its claim.
- **Is the first row the one that gives the most space back safely?** Yes once finding 1 is settled: at 10-23's rate the hour
  turns about 380 GB of fourteen days into 1 to 2 GB. Without finding 1, it gives that space back by removing what cannot be made
  again, unpressed.

### 4. Findings

1. **Blocking. The hour removes a tree's own scratch, unpressed.**
   - **What is wrong.** §3.1 and §3.2 take a tree's own ignored paths at its hour. The code tells those apart
     (`ContentHolds.cs:552-559, 620-622`). The person's press refuses a tree that holds them (`TreeSweeps.cs:314`). The look takes
     one only because of the fourteen days (`TreeSetAside.cs:16-21`, `TreeSweeps.cs:312-313`). This reverses D195 point 6, and the
     owner's direction does not name scratch.
   - **The smallest change.** Either:
     - (a) a finished folder whose judgement found own paths (`TreeHolds.Ignored`, `Written` or `TooLarge`, carried out of
       `JudgeAsync` onto the item) waits its days, its paths named in the record, and every other finished folder goes at its hour;
     - or (b) the owner's explicit decision that own scratch goes at the hour, recorded in D195 before 1b builds.

     Either way, add D88's AUTOTIDY2a note to §9, since its ground ("moves the folder aside rather than deleting it") changes.
2. **Blocking. The allowance takes folders that are not copies.**
   - **What is wrong.** §4.1 step 1 and §3.3 let the allowance take "any other [folder] past its first day": strays, leftovers, a
     folder whose record was refused, one from before this build. Strays and leftovers are not copies (`TreeStrays.cs:12-13, 41-56`;
     `PassLeftovers.cs:20-25`). A refused record is what §8 rejects.
   - **The smallest change.** Step 1 takes only folders whose manifest carries the record mark. Delete §3.3's "the allowance (§4)
     may take it sooner, past its first day". Count every other folder under its kind and say it in §4.4 with the press as its door.
     Add a proof: a stray past its first day over the part is kept and said.
3. **Blocking. The press's bounds contradict each other.**
   - **What is wrong.** §5 removes "any other set-aside folder before its date … as today" and also never removes "a set-aside
     folder whose commits no ref that stays holds". The press cannot judge the second rule for a folder whose branch is gone
     (`SlimManifest.cs:21-22`; `TreeSweeps.cs:239`). Today's early group takes every folder at any age (`SpaceFree.cs:163`;
     `SetAsideKeeping.cs:78-99`). "§3.1 steps 1 and 2 run at the press" cannot run either (claim 3).
   - **The smallest change.** Keep today's early group as the person's word, each folder listed with whether it holds files of its
     own and, where the manifest says, what holds its commits. Make the "never" item a listed fact, not a bound. At the press, the
     record is composed first only from the manifest's stored inputs (finding 6).
4. **Blocking. The allowance has no settled stopping rule when N cannot be reached.**
   - **What is wrong.** §4.1's "removes nothing more" and §7's "going in order and stopping at what cannot go" disagree. At the
     default (32 GB, parts summing 14.4 GB), §4.2's own 30 GB case is over for good: each pass empties the caches and every unused
     download past its hour, with no gain, under a permanent `space.over` row.
   - **The smallest change.** The whole's step runs only where taking every unit it may take would bring the whole within N, judged
     from the counts. Otherwise the pass takes only each part's overage, and says so. Then either size the default N so that
     measured "cannot go" plus the parts fits (about 48 GB by §4.2's case), or say why 32 holds. Add a proof: what cannot go over N
     takes only the parts' overage.
5. **Not blocking. The mark is written before the branch goes.**
   - **What is wrong.** Where `DeleteJudgedAsync` refuses after the move (`TreeSweeps.cs:239-253`), the folder carries a record mark
     while its branch stands, and the record is wrong.
   - **The smallest change.** Write the `record` mark only after the delete succeeded. The manifest lies beside the folder, so
     writing it then is no write in it (`TreeSetAside.cs:518-525`).
6. **Not blocking. A retry has nothing to compose the record from (claim 4).**
   - **The smallest change.** At `judged`, the manifest carries the record's inputs: session, branch, grew-from, tip, hold kind, and
     the ref or line form. A later pass, the press and the backfill compose and post from it. `record` then means only "kept".
7. **Not blocking. Most trees get no manifest (claim 2).**
   - **What is wrong.** Without a manifest there is no place for the mark or the size, so a tree with no package folder never goes
     at its hour, and the allowance sees its size as unknown.
   - **The smallest change.** The move writes a manifest for every finished tree, its paths possibly empty; `whole` writes none, as
     today. Name the source of `bytes`: the card's per-folder walk (`SpaceFree.cs:153-156` already reads
     `Sizes.Of(home, "set-aside").Folders`), or round 1's one walk, never a walk inside the hold.
8. **Not blocking. Two purges in a pass overwrite each other's record of failures.**
   - **What is wrong.** Each `PurgeSetAside` call rewrites `purge-failed.json` from the folders it met. A folder inside the other
     call's wait is skipped before its entry is carried (`TreeSetAside.cs:377, 437-464`). So the day's wait after a repeated
     failure (`:386-392`) never engages, and the inventory loses the reason.
   - **The smallest change.** One purge call with a wait per folder (`after` as a function of the folder).
9. **Not blocking. The named scratch comes from the wrong source and has no bound (claim 1).**
   - **The smallest change.** Take the list from the judgement's `TreeHolds`, bounded as `ScratchWalkLimit` (5,000,
     `TreeScratch.cs:22`) bounds the walk. Either drop the `bin/` database from §3.2's example or name build-folder paths too. Read
     any size outside the hold.
10. **Not blocking. One record per session does not fit trees that several sessions share.**
    - **What is wrong.** Successive records of one quest run in one tree (`Trace.Chain.cs:1155-1157`), teammates and helpers share
      one, and a reopened session can finish a second tree.
    - **The smallest change.** Key the record by the branch's work unit (`GrownBranch`'s unit, FABRIC-END2) or by (session, tree),
      naming every session that ran in it. Decide this in 1b's schema.
11. **Not blocking. The backfill cannot prove what it needs.**
    - **What is wrong.** A folder already set aside has no git record (`TreeSetAside.cs:215`), so neither step 1 nor own scratch can
      be judged. The manifest names no tip (`SlimManifest.cs:21-22`). The Grown traces keep the tip only for the newest 50 per
      repository (`SessionBranches.cs:30, 85`). A carried folder from before this build has no ref.
    - **The smallest change.** Drop the backfill: the press frees these now, and their days end by 11-06. Or take only folders whose
      Grown trace gives the tip and which are proven held again at that tip, and never under finding 1(a).
12. **Not blocking. The record names a ref that may go.**
    - **What is wrong.** A recovery ref held by a person's branch goes 30 days on (`RecoveryRetention.cs:21-29`). The person may drop
      one (`RecoveryDropping.cs:19-23`). For a squash landing, the landing's ref is written later, when its branch goes
      (`LandedCleanups.cs:324-335`). So `git worktree add <path> <ref>` can fail.
    - **The smallest change.** Record the tip commit and the hold kind, and have the command use the commit. Look the ref up when
      the record is read.
13. **Not blocking. The records sit in a store built for derived data.**
    - **What is wrong.** `knowledge.db`'s index is derived and is dropped on a schema bump (`SqliteKnowledgeStore.cs:13-21,
      112-117`). And the history clear is not the only door that deletes a session's row: there is also the single delete
      (`SessionLedger.cs:1996-2003`, `Sessions.cs:1522-1528`).
    - **The smallest change.** State that `work_records` migrates and is never dropped, and that `work_records_fts` has its own
      version, rebuilt from `work_records` and `events.jsonl`. State that every door that deletes a session's row deletes its record
      in the same transaction (`Asks/History.cs:303-323` for the clear).
14. **Not blocking. The search (1c) leaves its ranking, its default and its audience unsaid.**
    - **What is wrong.** Two FTS5 tables' bm25 scores are not one ranking, and the hybrid tier's vectors cover `entries` alone
      (`KnowledgeService.cs:180`). A search naming no kinds would include every 256 KB conversation (`AllButIndex`,
      `KnowledgeService.cs:188-190`). A record holds the person's words, which D94 keeps out even of the machine log
      (`MachineLog.cs:30-32`).
    - **The smallest change.** In 1c, state how the two rankings merge, cap records in a search naming no kinds (as
      `IndexEntriesUnasked` caps the index's entries), and name who may receive a `record` hit. Add a proof line for each.
15. **Not blocking. The records' bytes are counted twice.**
    - **What is wrong.** `knowledge.db` counts under `rest` (`SystemSpace.cs:90-92`), and `history` is the sessions and workflows
      folders (`:81-82`). Yet §2.2 and §4.5 count the records under `history`, so they would be in both.
    - **The smallest change.** Say how a table's bytes are measured and take them out of `rest`, or leave them in `rest` and word the
      card to match.
16. **Not blocking. The whole can be judged on stale and doubled counts.**
    - **What is wrong.** A tree counted under live trees at the day's walk and set aside since is counted again by its folder's
      bytes, and §4.3's counts may be a day old.
    - **The smallest change.** Judge the whole only from counts newer than the kind's last move, or count the trees kind again after
      a pass that moved one. Add a proof line.
17. **Not blocking. The allowance overrides `whole`.**
    - **What is wrong.** Under `whole`, §4.1 step 1 still takes finished folders "past their hour". At the default 8 GB part and
      about 27 GB a day of moves, the person's opt-out means nothing.
    - **The smallest change.** Say that the allowance takes a `whole` folder only past its first day, or never, and say it on the card.
18. **Not blocking. The record is composed inside the repository's hold.**
    - **What is wrong.** The commits' log, the read of `events.jsonl` and the post all happen under `TreeLock.TryReplaying`
      (`AutoTidies.cs:291-297`), which every start in that repository waits on.
    - **The smallest change.** Compose before the hold, from the plan's judgement. Post inside the hold with a bound, as
      `BusyAsync` is bounded, or after the move. Inside the hold, check only that the tip is unchanged.
19. **Not blocking. The hour's purge can leave a half-deleted folder.**
    - **What is wrong.** Where a person has a file in the folder open, `DeleteNeverFollowing` stops at that file and leaves a partial
      folder (`TreeSetAside.cs:596-640`).
    - **The smallest change.** Rename the folder first, as the press moves an earlier build's folders aside first
      (`SpaceFree.cs:127`). A refused rename keeps the folder whole, `busy`.
20. **Not blocking. Pointers and citations.**
    - **What is wrong.** See claims 7 and 8.
    - **The smallest change.** Change round 1's commit to `d57b7f8d6` in the standing line and in D195's note. Make rows 1a and 1e
      say where their proofs are. Cite `KnowledgeIndex.cs:41` for the local refresh.
21. **Not blocking. The carried ref, on both paths.**
    - **What is wrong.** `SweepAsync` is the press's path as well as the look's (`TreeSweeps.cs:181` serves both). Without the ref
      there, the person's *Clean up…* still drops a carried branch's commits. And no person's ref ever holds such a ref, so it is
      kept until the person drops it (`RecoveryRetention.cs:7-10`).
    - **The smallest change.** Write the ref for `Carried` on both paths, and say on the inventory that these refs are never
      retired.
22. **Not blocking. §4.6's shape.**
    - **What is wrong.** The parts are stored in decimal GB (4.8, 1.6), while `KeepChoices` keeps whole GB (`FloorGb`,
      `KeepChoices.cs:40`). "A part above N reads as N" contradicts KEEP1d's rule that a value out of its bounds in the file is not
      read. And `KeepKinds.BrowserCache` is named in both 1d and 1e.
    - **The smallest change.** Store parts in MB, or say they are decimal. Choose one rule for a part above a lowered N. Place
      `KeepKinds.BrowserCache` in 1d alone.

**22 findings, 4 of them blocking.**

**Verdict.** Accept after named changes. The steward can check each against the revised text and the code above, so no further
read is needed:

- **The four blocking findings:**
  - own scratch at the hour, settled by the owner's recorded word or by finding 1's narrower rule;
  - the allowance taking only folders that carry the record mark;
  - one consistent set of bounds for the press;
  - a stopping rule for an N that cannot be reached, with the default N reasoned against its parts.
- **Findings 5, 6, 7 and 10**, which fix the shape of the durable record before 1b builds it: the mark written after the branch
  goes, the record's inputs in the manifest, a manifest for every finished tree, and the record's key.

The other findings can be taken in their rows. The foundation holds where it matters most. No uncommitted path, no tree a session
is in, and no commit a ref does not hold is put at risk once the carried ref lands. What the hour and the allowance remove is
otherwise a copy, except the scratch and the strays the blocking findings name. If the steward takes finding 1(a), §3.3's
estimate of the space given back should be restated with how many finished trees hold their own scratch: the install's next look
can count that from the judgement it already makes.

## Opus (foundation), round 3

The independent Opus 5.5 reader's foundation read of round 3 at `6d3d3aec9`: 11 findings, none blocking; accept after named changes the steward can check, seven before KEEPSIZE1b is built, three before KEEPSIZE1d, and finding 9 at the merge with KEEPSIZE1a as built on main. Codex's read of round 3 stopped when its workspace ran out of credits and wrote nothing; under D191 it is owed on the text the named changes leave.

## KEEPSIZE1 round 3: the foundation read (D191)

**Reader:** Claude Opus 5.5, in Fable's place (D191's 2026-10-23 amendment). **Read:** `docs/2026-10-24-keep-size-design.md` whole at
`6d3d3aec9`, D195's round-3 note, `git diff HEAD~1 HEAD` on both, round 2's reads (`docs/2026-10-24-keep-size-reads.md`), and main's
KEEPSIZE1a commits (`0f6f76b79`, `936df1b01`, merged at `485c68121`), which landed beside this round from the same base `49cd7bdbc`.
**Against the code** at `49cd7bdbc` (round 3 changes only documents): `TreeSetAside`, `TreeSweeps`, `AutoTidies`, `ContentHolds`,
`SlimManifest`, `SetAsideKeeping`, `RecoveryRetention`, `KeepKinds`, `SpaceFree`, `SystemSpace`, `BranchTidying`'s Keeping pass, and the
service's `Sessions`, `SessionLedger`, `Asks/History`, `SqliteKnowledgeStore`, `KnowledgeService`, `ServiceFactory`. Nothing was run.
Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, service paths under `src/Daoris.Service/Daoris.Service.Core/`;
design lines are round 3's.

**What holds.** The foundation now holds where it matters most. A finished folder goes at its hour only at `recorded`, after its
branch's delete succeeded and the service took its record, and only while a holding check finds its exact tip on its ref or on
`refs/heads/<line>` (§3.4, §3.5). The tip ref is written wherever the line's own branch does not hold the tip, which closes the squash,
carried, landing-branch and `origin`-only cases (§3.1; `KeepDiscardedAsync` returns the existing ref at the same commit,
`ContentHolds.cs:297-301`, so a content hold's ref and the tip's are one). Uncommitted and untracked paths still keep the tree
(`TreeSweeps.cs:311`). A write after the judgement began keeps the folder (the `read` threshold): the judgement's status is
`--no-optional-locks` (`ContentHolds.cs:562-563`), a worktree's index and HEAD live under the repository's `.git/worktrees/`, the rename
and `worktree remove` write nothing inside the folder, and the purge reads every entry's creation and write times
(`TreeSetAside.cs:586`). Strays, leftovers and folders from before this build keep their days and are never the allowance's. The press
is one set of bounds. The allowance's stopping rule is sound. What the hour removes beyond copies is the session's own scratch, which
the owner decided goes (§3.2, D195 lines 491-493). What follows are gaps in the record's completeness, in two arithmetic rules and in
wording, none of which removes anything that cannot be made again.

### 1. Round 2's findings

#### 1.1 The Opus foundation read

| # | Round 3's answer | Verdict | Checked |
|---|---|---|---|
| claim 1 | the named scratch from `TreeHolds`, build folders' contents unnamed (§2.1) | **taken as named; holds** | `ContentHolds.cs:560-607`; a build folder the checkout lacks is named once as a path by `--ignored=matching` (`:578-584`), its contents never, as §2.1 says |
| claim 2 | a manifest for every finished tree; sizes from the set-aside walk (§3.3, §4.3) | **taken; holds** | `TreeSetAside.cs:204`, `SpaceFree.cs:153-156` |
| claim 3 | today's early group kept; the record posted from the manifest at the press (§5) | **taken; holds** | `SpaceFree.cs:163`, `SetAsideKeeping.cs:78-99` |
| claim 4 | the record's inputs at `judged`; the pass, press and restart post from them | **taken; holds for `deleted`**, not for `refused` or a stopped `judged` | finding 1 |
| claim 5 | the classes (§3.4) | **taken; holds** | `TreeStrays.cs:41-56`, `PassLeftovers.cs:20-25` |
| claim 6 | `origin/<line>` its own row; the tip ref; `hold.form` | **taken; holds** | `RecoveryRetention.cs:12-14`, `TreeSweeps.cs:420` |
| claim 7 | §1.3 corrected | **taken; holds** | `KnowledgeIndex.cs:41`, `SqliteKnowledgeStore.cs:13-21, 112-117` |
| claim 8 | round 1 is `d57b7f8d6` | **taken; holds** | `d57b7f8d6` adds the design; `16769207d` merges it; its §6 holds rows 1a and 1e |
| 1 (blocking) | the owner's decision (b), §3.2; D195's note; AUTOTIDY2a in §9 | **taken as named; holds** | `TreeSetAside.cs:16-21`, `TreeSweeps.cs:312-314` |
| 2 (blocking) | the allowance takes `recorded` alone (§3.4, §4.1 step 1); 1d's proof | **taken; holds**; the set-aside part now takes nothing the hour has not | finding 5 |
| 3 (blocking) | the early group as the person's word, each folder's class listed; "never" a listed fact (§5) | **taken; holds**; a stopped folder pressed before its resume loses its record | finding 1 |
| 4 (blocking) | the whole's step only where every eligible unit reaches N (§4.3 step 2); 48 GB reasoned (§4.2) | **taken; the rule holds**; the reasoning leans on an 8 GB set-aside part the allowance cannot hold | finding 5 |
| 5 | `recorded` after `deleted`; a refused delete marks `refused` | **taken; holds as a mark**; a refused folder never gets its record | finding 1 |
| 6 | the inputs in the manifest at `judged` | **taken; holds for `deleted`** | finding 1 |
| 7 | a manifest for every finished tree, `whole` included | **taken otherwise; holds**: a `whole` tree's record needs the inputs, and `whole` writes empty paths, so no slim, as today | §3.3 line 270 |
| 8 | one purge call, `after` per class | **taken; holds**; with main's KEEPSIZE1a each place keeps its own `purge-failed.json`, so "one call" is one pass over both places | finding 9 |
| 9 | `TreeHolds`, 5,000 names, the `bin/` example dropped | **taken; holds** | `TreeScratch.cs:15-22` |
| 10 | one record per tree, keyed by its set-aside identity, every session linked | **taken; holds** | `Trace.Chain.cs:1150-1158` |
| 11 | no backfill | **taken; holds** | `TreeSetAside.cs:215`, `SlimManifest.cs:21-22` |
| 12 | the tip kept, the ref looked up on read, a dropped ref unmarks | **taken; holds** | `RecoveryRetention.cs:21-29` |
| 13 | tables that migrate; `Sessions.DeleteAsync` takes the links | **taken; holds**: it is the one `DELETE FROM sessions` (`Sessions.cs:1525`), called inside `ExclusiveAsync` by both doors (`SessionLedger.cs:1996-2004`, `Asks/History.cs:303-313`); a record with no session is left over | finding 11 |
| 14 | 1c's ranking, cap and audience | **taken in 1c; holds** | `KnowledgeService.cs:180, 186-190` |
| 15 | `knowledge.db` once, under the rest; records by count | **taken; holds** | `SystemSpace.cs:81-82, 90-92` |
| 16 | fresh counts, subtraction, recount | **taken in part**: a second pass before the recount lands takes the overage again | finding 3 |
| 17 | never under `whole` | **taken; holds** | §3.4 line 304 |
| 18 | composed and posted after the hold, from the manifest | **taken otherwise; holds**; the Keeping pass and the press now read git, which §9 does not amend | finding 7 |
| 19 | renamed into `.tidied/.going/` first | **taken; holds**; every reader of `.tidied` (purge, slim, list, card walk) must pass `.going/` over, which §3.4 leaves unsaid | finding 7 |
| 20 | pointers | **taken; holds** | |
| 21 | the ref on both paths, never retired, said | **taken, widened to every tip the line's own branch does not hold; holds** | `TreeSweeps.cs:181`, `RecoveryRetention.cs:6-10` |
| 22 | whole GB; refused at the door, not read from the file; `BrowserCache` in 1d | **taken; holds** for the GB and the row; the lowered-N check sums differently from the part's | finding 4 |

#### 1.2 Codex's blocking findings

| # | Round 3's answer | Verdict | Checked |
|---|---|---|---|
| 1 | the tip ref wherever `refs/heads/<line>` does not hold it; 1b removes both branches | **taken as named; holds** | `TreeSquashes.cs:20-27`, `TreeSweeps.cs:337-356` |
| 2 | `read`, before the judgement's read, is a finished folder's threshold | **taken otherwise; holds** (see *What holds*): stronger than a post-move git check, since it reads every entry's creation time | `TreeSweeps.cs:308`, `TreeSetAside.cs:203, 273, 586` |
| 3 | armed only at `recorded`; the holding check before each removal; stops resumed | **taken; holds**, but `refused` and a stopped `judged` are left without a way to their record | finding 1 |
| 4 | one judgement for every door; awaiting-record held past its days; unmarked keep D195's days | **taken; answered with a reason that holds** (`TreeStrays.cs:41-56`) | |
| 5 | decided by the owner; the inventory from `TreeHolds` | **decided; holds** | |
| 6 | inputs captured before the delete; per-tree record; attribution only where bases bound it; no backfill | **taken; holds** (but see finding 10 on the range) | |
| 7 | whole GB, sum at most N on every door, scaled defaults, file read part by part | **taken in part**: the N door and the part door check different sums; tiny volumes break the minimums | finding 4 |
| 8 | fresh counts, subtraction within a pass, recount | **taken in part** | finding 3 |
| 10 | taken in 1a's row (place-aware units, the same-name proof) | **open as stated**: main built 1a otherwise, with a reason round 3 can give | finding 9 |
| 11 | `knowledge_get` resolves `record:…`; the session page; older records indexed; search → read → clear | **taken in 1c; holds** | `KnowledgeService.cs:248`, `SqliteKnowledgeStore.cs:222` |
| 12 | the browser's kind and daily clearing in 1d, the start's check alone in 1e | **taken; holds** | |

### 2. What round 3 introduced

- **Wrong claims.** §3.3's downgrade sentence is backwards for awaiting-record folders (finding 8). §3.5's `moved` resume cites
  `TreeSweeps.cs:304-305` (a registration whose folder is gone), which is the stopped `judged` case; at `moved` git has let go, and the
  next look meets a branch with no tree (finding 1). §4.3's "a destructive step reads only fresh counts" is contradicted by its next
  sentence (finding 3). §1.4, the standing line and row 1a describe 1a as unbuilt (finding 9). "Why 48" counts the install's own live
  trees inside its 7 GB beside six more, which only makes 48 more generous (finding 5).
- **New holes.** A refused delete or a stop at `judged` leaves finished work with no record, though its manifest holds every input, and
  the next look deletes its tree-less branch (finding 1). The person's *Clean up…* keeps no record (finding 2). The record's commits
  range takes commits merged in from the line (finding 10). A record with no session has no clearing door, and a newer `work_records`
  schema meeting an older build is unsaid (finding 11). The purge and the press gain git outside the hold, unamended (finding 7).
- **Rows and proofs.** 1b's *"a branch delete refused after the move: `refused`, never armed, kept its days"* proves the mark, not what
  becomes of the record when the next look deletes the branch (finding 1). 1d's arithmetic proof passes while the parts' sum exceeds N
  (finding 4). 1a is built on main without its round-3 proof (finding 9). 1d's *"past their first day"* is a remnant of round 2's
  allowance rule, harmless.
- **D195's note.** Line 502 *"a stop between steps is resumed"* where §3.5 leaves `judged` unresumed (finding 1); lines 510-513
  *"destructive steps read only fresh counts, less what the pass removed"* (finding 3); lines 516-517 *"1a (every place, place-aware
  units)"* (finding 9). Line 504 *"goes at its hour"* reads with §3.4 but not with §9's *"an hour after its record is kept"*
  (finding 6).

### 3. Findings

1. **Not blocking. `refused` and a stopped `judged` never reach their record, and the next look deletes their branch.**
   - **What is wrong.** At `refused` (design lines 307, 333-336) git has already let go of the tree, so the next look meets the same
     branch at the same tip with no tree: its kept fact still names it (`AutoTidies.cs:233-254`), `GoesByItself` takes a branch with no
     tree (`TreeSetAside.cs:26-29`), and the last look finds no folder at the tree's path (`TreeSetAside.cs:61-67`), so it is deleted
     within minutes, on §3.1's path with its ref. The folder labelled *its branch stands* is then a copy of a branch that is gone; it
     waits its days and goes with no record. A refusal is not rare in a busy repository: the delete verifies the line at the commit read
     (`ContentHolds.cs:472, 519-524`), so a landing between the judgement and the delete refuses it. A stop between the rename and
     `moved` leaves `judged` with the folder present and its worktree record standing. The next look takes that branch the same way
     (`TreeSetAside.cs:78`, `:215-219`). Today's slim drops a `judged` manifest at the next pass (`SetAsideKeeping.cs:120-125`), its
     inputs with it. §3.5 gives `moved` the way back (*"its delete marks this manifest `deleted` by its `branch` and `tip`"*, lines
     355-357) but not these two. Nothing goes early; what is lost is the record the design exists to keep.
   - **The smallest change.** One resume rule for every finished folder at `judged` (its folder present), `moved` or `refused`: where
     the manifest's `branch` is absent and the holding check passes, mark `deleted` and go on at step 6; the delete of a branch with no
     tree marks a manifest of that `branch` and `tip` `deleted`. A schema-2 `judged` manifest is not dropped by the slim. The press runs
     the resume before it lists. Correct the `:304-305` citation and D195 line 502. Add to 1b's proof: a refused delete, then the next
     look's delete, gives the record.

2. **Not blocking. The person's *Clean up…* keeps no record.**
   - **What is wrong.** §2.3 (lines 220-222) gives a record only to a tree the look sets aside. The press's path deletes the tree with
     `git worktree remove` (`TreeSweeps.cs:210-235`), with no manifest. §3.1 (line 232) writes the tip ref on it, but no record. §4.4
     (line 463) names *Clean up…* as a door when Daoris is over N. So finished work the person cleans up leaves no record of its landing
     or commits, which goes against the owner's *what is kept of finished work is its text record*. The press takes the work landed on a
     branch of the person's, which the look leaves (`TreeSetAside.cs:28`). Its own scratch keeps the tree there
     (`TreeSweeps.cs:314`), so nothing else is lost.
   - **The smallest change.** Either say in §2.3, and in the press's list, that *Clean up…* and *Discard* keep no record, or have the
     press's path write the same inputs before the tree goes (a manifest under `.tidied` with no folder) and post after the hold, as the
     look's path does.

3. **Not blocking. A stale count can authorize the same removal on each pass until the recount lands.**
   - **What is wrong.** §4.3 (lines 449-452) says *"a destructive step reads only fresh counts"*. The next sentence lets a stale kind
     take *"its part's overage as last measured less what this pass removed"*. The Keeping pass runs at every look
     (`BranchTidying.cs:237-283`). The recount is a background walk (`SystemSpace.cs:199-205`). So a second pass before the walk ends
     subtracts nothing of the first pass's removal and takes the overage again, which is Codex 8's *repeated passes may reuse it*. What
     goes is downloads and caches, which can be made again, so this costs a fetch.
   - **The smallest change.** Subtract every removal since the kind's last count (each run's bytes are in `keeping.json`'s `last`), or
     let a stale kind take nothing until its recount lands. Reword D195 lines 512-513 to match. Add the second pass to 1d's proof.

4. **Not blocking. The parts' sum is checked two ways, and the minimums exceed N on a tiny volume.**
   - **What is wrong.** §4.2 (lines 418-422) has two rules:
     - a part's door checks the parts the person set plus the others' defaults at that N;
     - the N door checks only the parts the person set.

     Example: set-aside 40 at N = 48 (40 + 5 + 2 = 47). N = 41 then passes the N door (40 ≤ 41), but the defaults at 41 are
     ⌊5·41/48⌋ = 4 and max(1, ⌊2·41/48⌋) = 1, so the sum is 45 > 41. The file rule, *"a part that would bring the sum above N is not read
     and keeps its default"*, cannot repair a sum whose overage is the defaults'. Where half the volume is under 3 GB, the three 1 GB
     minimum defaults exceed N. 1d's proof (*"an N refused below the set parts' sum"*, *"the last as its default"*) passes in both cases
     while *"the parts' sum is at most N"* fails. Nothing removes by the rest of N, so nothing is lost.
   - **The smallest change.** Use one sum on every door and in the file: the set parts plus the unset parts' defaults at the N in
     question. The file read drops set parts in reverse table order until that sum fits. Where half the volume is under 3 GB, the parts
     are not offered. Prove the lowered-N case through each door.

5. **Not blocking. The set-aside part takes nothing the hour's purge has not.**
   - **What is wrong.** §4.1 step 1 (lines 388-390) lets the allowance take recorded folders past their hour, not under `whole`, under
     every guard. That is exactly what the hour's purge takes earlier in the same pass, under the same guards and budget (§3.4 line 303;
     §4.3 line 436). So the 8 GB part (line 411) removes nothing. What the kind holds after the purge is only what the allowance may not
     take, and the card's *"1.1 GB of 8 GB"* (line 477) reads as a bound that does not hold. "Why 48" (lines 424-431) counts that 8 GB as
     a reserve the parts keep. The non-recorded folders are in fact part of what cannot go, and their number is unknown until 1b counts
     the classes (line 374). The sizing case also counts the install's own 4.1 GB of live trees inside its 7 GB beside six more, which
     only makes 48 more generous.
   - **The smallest change.** Say that the set-aside part is only said (its overage names the classes, as §4.4 line 469 does). In "Why
     48", count the folders that are not recorded among what cannot go, with 1b's class count as the measure that will confirm the
     default.

6. **Not blocking. The hour's clock is named two ways.**
   - **What is wrong.** §3.4 (line 303) and step 7 (line 344) say *at its hour*. The purge measures that from the moment in the folder's
     name (`TreeSetAside.cs:377`), so a folder recorded late goes at once. But the card (line 478), §9 (line 675) and the router's row say
     *an hour after its record is kept*.
   - **The smallest change.** Name one clock, either the move's as the purge reads it or `recorded`'s as the manifest writes it. Use it
     in §3.4, §4.5, §9, the router and D195 line 504.

7. **Not blocking. The purge and the press now read git, and §9 does not say so.**
   - **What is wrong.** The holding check (lines 297-299), step 6 and the resume (lines 340-343, 353-358) all read git from the Keeping
     pass and the press: `show-ref`, `merge-base --is-ancestor` and `git log`. Both of those are *"outside every repository's hold: it
     touches no git and no live tree"* (`SetAsideKeeping.cs:6-9`; the Keeping pass, `BranchTidying.cs:237-238`). Each must resolve a
     manifest's workspace and repository to a root, and the rows leave that unsaid. §3.1 (line 237) relaxes KEEP1a's hold-free-of-new-git
     rule (Fable F1.5). The `.going/` folder lies where every reader of `.tidied` enumerates workspaces. §9 lists none of these.
   - **The smallest change.** Say three things in §3.4:
     - the judgement reads git read-only, outside the hold, from the registry's root;
     - a repository it cannot resolve or read makes the folder unmarked;
     - every reader passes over `.going/`.

     Add the Keeping pass's contract and KEEP1a's hold note to §9.

8. **Not blocking. §3.3's downgrade sentence is backwards for one class.**
   - **What is wrong.** Lines 257-258 say an older build *"purges the folder by its days, which keeps more of a folder awaiting its
     record than this build would, never less"*. But this build keeps an awaiting-record folder past its days (line 305), while an older
     build reads no schema-2 manifest (`SlimManifest.cs:58`) and purges it at its days, its record never kept. It keeps more of a
     recorded folder (days, not an hour) and less of an awaiting one.
   - **The smallest change.** Say both, and that a downgrade loses the records still awaited.

9. **Not blocking. KEEPSIZE1a was built on main from the same base, differently.**
   - **What is wrong.** Main's `0f6f76b79` (merged at `485c68121`) reads every place through `SetAsideRoots`, and each place keeps its
     own `purge-failed.json`. A press id names its folder *"at whichever place of the trees holds it, the new first"* (main's
     `SpaceFree.cs:296`), not the place-aware unit that row 1a (lines 594-600), §10.1's Codex 10 answer (line 710) and D195 lines 516-517
     promise, and main has no same-name proof. Round 3 stands on `49cd7bdbc`, so these are stale against main:
     - §1.4 (lines 159-162);
     - the standing line (line 34, *"Nothing here is built"*);
     - §7's order (*"1b first, then 1a"*);
     - the TASKS row.

     The built resolution holds for a reason round 3 can give. A folder moves into the new place alone, named with its millisecond
     moment (`TreeSetAside.cs:271-273`), so one relative id at both places cannot arise from Daoris's own moves.
   - **The smallest change.** At the merge:
     - restate §1.4 and row 1a as built at `0f6f76b79`;
     - answer Codex 10 by that reason, or file the place-aware id as a row of its own;
     - reconcile the TASKS rows and D195's two notes.

10. **Not blocking. The record's commit range takes commits merged in from the line.**
    - **What is wrong.** §2.1 (line 176) and step 6 (line 340) read `git log --numstat <grewFrom>..<tip>`, which is every commit the tip
      reaches and `grewFrom` does not. A session branch that merged the line in, for example to settle a conflict, would then name other
      sessions' commits as its tree's. The line at the judgement cannot be excluded instead, since it already holds the tip.
    - **The smallest change.** Use `--first-parent`, so a merge from the line is one commit of the tree's, with its first-parent
      changes. Prove it with a branch that merged its line.

11. **Not blocking. A record with no session has no clearing door; the post's identity and a newer schema are unsaid.**
    - **What is wrong.** §2.2 (lines 195-196, 202-204, 214-215) clears a record with its last session. A record posted when no session
      row names the tree is never cleared, whether its sessions were cleared while it awaited its record or no row ever named the tree.
      Neither *Clear history…* nor the age reaches it. The pass and the press may both post one folder's record (§3.5 lines 341-342),
      and §2.2 does not say the post is idempotent. Nor does it say what an older build does with a newer `work_records` schema; the
      index refuses a newer schema (KSCHEMA1, `SqliteKnowledgeStore.cs:13-21`).
    - **The smallest change.** Say four things:
      - the post is idempotent on `(workspace, repository, folder)`;
      - a record with no session either ages by its own `at` under history's age or is not kept, the folder then going as recorded;
      - the history clear takes such records;
      - a newer schema is refused as KSCHEMA1 refuses the index's.

**11 findings, 0 of them blocking.**

**Verdict.** Accept after named changes; the steward can check each against the revised text and the code cited above, so no further
read is needed. The foundation holds. Nothing goes at the hour or by the allowance unless its exact tip is held, its branch is gone and
its record is kept. No uncommitted path, no tree a session is in, and no stray, leftover or older folder is taken before its days. The
press is one consistent set of bounds. The allowance stops where N cannot be reached. Only the session's own scratch, by the owner's
recorded decision, goes beyond copies. The changes before KEEPSIZE1b builds are:
- the one resume rule for `judged`, `moved` and `refused` (finding 1);
- a decision on the press's record, at least said in §2.3 (finding 2);
- one clock for the hour (finding 6);
- git outside the hold and `.going/`, said in §3.4 and §9 (finding 7);
- the downgrade sentence (finding 8);
- `--first-parent` (finding 10);
- the record's identity, its orphans and a newer schema (finding 11).

Findings 3, 4 and 5 settle the counts, the parts' one sum and the set-aside part before KEEPSIZE1d. Finding 9 is the merge with main's
built KEEPSIZE1a. Each is a sentence or a rule, not a new mechanism.

## Codex, the accepted text (2026-10-24)

Codex's read of the accepted design, owed since its credits ran out, verbatim. The amendment it names is the design's §12.

### 1. Claims about the code

Read the design, every earlier read in the named reads file, and D195’s KEEPSIZE1 notes and acceptance note. Code references below are against main at `16e0950e`. Driver paths are relative to `src/Daoris.Desktop/Daoris.Desktop.Driver/`; service paths are relative to `src/Daoris.Service/Daoris.Service.Core/`.

The corrected account of squash history, carried branches, repository-feed replacement and KEEPSIZE1a holds. These claims still need correction:

| Design location | Wrong claim | Code |
|---|---|---|
| `docs/2026-10-24-keep-size-design.md:388` | The `read` timestamp makes every later uncommitted change detectable. The walk examines surviving descendants; it does not detect a deleted top-level tracked file or examine the root directory’s own timestamp. | `Trees/SessionTrees/TreeSetAside.cs:608`, `:611`, `:617` |
| Design `:370` | A refused worktree removal puts the folder back “as today.” Today rollback can fail, leaving the folder aside after its manifest was forgotten. | `TreeSetAside.cs:233`, `:238`, `:241` |
| Design `:243` | *Clean up…* can take a tree holding its own scratch. Its sweep explicitly keeps that tree as dirty. | `Trees/SessionTrees/TreeSweeps.cs:314` |
| Design `:94`, `:667` | Having a file open establishes that the rename is refused. The implementation establishes only whether `Directory.Move` fails; an open handle’s sharing permissions matter. | `TreeSetAside.cs:268`, `:271`; `Platform/MachineLog.cs:351` demonstrates a writer permitting deletion |
| Design `:162`, `:468` | Logs have one file per day per process. The filename uses the supplied source, such as `driver`, rather than a process identity. | `Platform/MachineLog.cs:68`, `:72`, `:350` |
| Design `:255` | A recovery ref goes thirty days after the line subsequently holds it. Retention runs from the ref’s recorded creation date, with the commit-date fallback; becoming held does not restart that clock. | `Trees/SessionTrees/RecoveryRetention.cs:25`, `:29`, `:110` |

The proposed landing-record source also lacks information its table promises; finding 6 addresses that.

### 2. The mechanism’s holes

The owner’s decision settles disposal of a finished session’s own scratch. The remaining question is whether the mechanism reliably establishes that a folder belongs to that rule, preserves the promised record, and retains the exact commits.

The earlier reads concentrated on the successful move, refused branch deletion and interrupted manifest states. The remaining holes are chiefly at their boundaries:

- The new exact-tip protection is not explicitly included in the branch deletion’s existing ref transaction.
- Timestamp inspection is treated as equivalent to checking uncommitted state.
- An unread repository or manifest becomes a disposable legacy folder.
- A failed rollback can erase the only recovery protocol inputs.
- Moving into `.going/` loses the original identity and bypasses the judgement on restart.
- The record promises landing facts absent from its captured inputs.
- Clearing one session from a shared record leaves its indexed words unspecified.
- A background walk can finish after a mutation while retaining measurements made before it.

Recovery by `git worktree add <path> <tip>` remains conditional: some surviving ref must retain that tip. The text record cannot restore uncommitted changes or scratch. That makes the exact-tip transaction, the late-change guard and the distinction between unread and disposable load-bearing requirements.

The legacy days rule and the person’s explicitly listed disposal remain deliberate exceptions. They should not become fallback permissions for failures in the new protocol.

### 3. The rows

| Row | Buildable alone, in this order? | Does its proof establish its claim? |
|---|---|---|
| **KEEPSIZE1b** | After findings 1–6 are amended. Its durable session ownership also needs finding 7 settled. | The present cases miss a disappearing tip holder, a top-level tracked deletion, unread eligibility inputs, failed rollback, restart after `.going/`, and landing-trace expiry. |
| **KEEPSIZE1a** | Already built and available to 1b. | Its declared two-place proof establishes the normal generated-name contract. The settled treatment of manually duplicated names is explicit. |
| **KEEPSIZE1c** | After 1b, with finding 7’s clearing rule. | Search → read → clear is insufficient when clearing only one of several linked sessions. Prove that session’s words disappear while the other session and record remain. |
| **KEEPSIZE1d** | After finding 8; browser residue handling should accompany its browser kind. | Add a mutation during a walk, a stale different kind contributing to the whole, and an interrupted cache deletion. The arithmetic cases otherwise cover the stated allocation rule. |
| **KEEPSIZE1e** | Yes after 1d supplies the kind, policy and removal. | The inherited stopped-walk, no-shell and start-gate cases remain necessary alongside the two shortened cases written here. |
| **KEEPSIZE1f** | Yes after 1d. | The declared proof covers warning-only behavior and the separate trees volume. |
| **KEEP1a2** | Remains separate. | Its evidence establishes possible savings, not disposability of arbitrary producer output. |

### 4. Findings

1. **Blocking — the new exact-tip hold is not bound to branch deletion.**

   **Where:** design `:250–260`, `:372`; `TreeSweeps.cs:341`, `:363`, `:375`, `:415`; `ContentHolds.cs:472`.

   `DeleteJudgedAsync` verifies exactly the supplied proof rests. Those currently establish the landing/content judgement, which can preserve content without preserving the session’s original history. Merely creating a tip ref first does not make that ref a verified rest. Likewise, a separate local-line ancestry check can become stale while the original landing proof remains valid.

   **Smallest change:** carry the exact ancestry holder and commit into the deletion transaction: either the local line snapshot that justified omitting a recovery ref, or the recovery ref at the source tip. Preserve the existing content-proof rests too. Prove removal or movement of that new holder between protection and deletion refuses the delete.

2. **Blocking — `read` does not close the uncommitted-change race.**

   **Where:** design `:317–319`, `:386–391`; `TreeSetAside.cs:608–619`; `TreeSweeps.cs:308–314`.

   Delete a top-level tracked file after status reads it but before the move. No surviving entry necessarily has a later timestamp; the changed root directory is not itself inspected. The folder can pass the hour’s guard despite containing an uncommitted deletion. Preserved timestamps are another limitation of this proof.

   **Smallest change:** validate tracked and untracked state after the move while the worktree metadata remains usable, retaining or restoring the tree on dirty or unread results. Keep a late-change guard that also accounts for root-directory changes. Add the tracked-deletion race to 1b’s proof; a newly written file alone does not prove it.

3. **Blocking — unread inputs become permission to purge an unrecorded folder.**

   **Where:** design `:315`, `:323`, `:328–329`, `:342`; `SlimManifest.cs:40–63`; `ContentHolds.cs:363–366`.

   An unread schema-2 manifest is classified as unmarked. An unresolved or unreadable repository also makes the folder unmarked. That class goes at its days. Thus a folder awaiting its required record can lose that protection because its inputs temporarily cannot be read.

   **Smallest change:** distinguish known legacy/unmanifested folders from unread, unsupported-schema or unresolved finished folders. Preserve the latter’s protection and retry; do not permanently downgrade them. Establish branch absence only with git’s explicit missing result. Prove each failure past the folder’s days, followed by recovery and successful recording.

4. **Blocking — failed rollback destroys the resume inputs.**

   **Where:** design `:370–371`; `TreeSetAside.cs:233–244`.

   The prescribed “as today” failure path forgets the manifest before attempting rollback. If rollback also fails, the folder remains aside without its inputs. A later look can remove its tree-less branch, but there is then no manifest to mark `deleted` or post. The folder eventually goes as unmarked.

   **Smallest change:** forget the manifest only after rollback is confirmed. If the folder remains aside, retain its original path, destination and facts in a resumable failure state. Prove worktree-removal refusal followed by rollback refusal and restart, ending with a kept record.

5. **Blocking — `.going/` has no complete restart protocol.**

   **Where:** design `:355–360`, `:656–657`; `SlimManifest.cs:32–37`; `SetAsideKeeping.cs:113–116`; `TreeSetAside.cs:638–670`.

   The design moves the folder but does not say that its adjacent manifest moves with it or how the destination preserves workspace, repository and original identity. The slim presently drops a manifest whose original folder is absent. “Deleted by the next pass” then supplies neither the holding check nor the late-write checks required of every removal. A stop immediately after rename exposes this before any partial deletion complicates recovery.

   **Smallest change:** move a durable removal record with the folder, preserving its identity, threshold and authorization. Resume through the applicable eligibility checks, with explicit handling of partial deletion and subsequent writes. Prove rename → stop → late write or lost holder → restart keeps the remaining contents and says why.

6. **Blocking — the manifest cannot produce the promised landing record.**

   **Where:** design `:183`, `:202–203`, `:284–296`, `:380`; `Trees/LandedBranches.cs:14–16`; `TreeSweeps.cs:356`, `:376`, `:433`.

   The record promises the landing branch and when it landed. The manifest contains the cleanup judgement’s hold and times, not a landing snapshot or `LandedAt`. Several judgements return the line or content holder rather than the actual landing. Composing later from this manifest alone cannot recover those facts after trace retirement.

   **Smallest change:** capture the associated landing facts before removal, using the work unit/session association where established. Store an explicit absent/unknown result where no landing can be established; never substitute judgement or move time for landing time. Prove composition after the original landing trace has expired.

7. **Blocking — clearing one linked session does not clear its contribution to the record’s index.**

   **Where:** design `:204–213`, `:680–692`; service `Sessions/Sessions.cs:1525`, `Asks/History.cs:310–312`.

   The full-text row contains conversations from every linked session. The deletion contract removes a session’s links and deletes the record only with its last session. It does not remove the deleted session’s words while another session still links the record. Those words can remain searchable after their history was cleared.

   **Smallest change:** define conversation and note contributions per session. Deleting a session must remove its contribution and update the surviving record’s index in the same transaction. Settle that ownership in 1b; implement and prove the search behavior in 1c. Use two linked sessions with distinct searchable words.

8. **Blocking — an end timestamp does not establish a fresh measurement.**

   **Where:** design `:499–521`; `Platform/SystemSpace.cs:219–233`; `Platform/WiringSizes.cs:234–269`.

   Counts are stamped when their walks finish. A walk can count a live tree, the tree can move, and the walk can finish afterward. Its stamp passes the proposed freshness test although its bytes precede the move. A subsequent set-aside walk can count the same bytes again. Also, keeping a stale kind from removing its own units does not prevent its stale total from authorizing removal of fresh downloads or caches under the whole allowance.

   **Smallest change:** validate a mutation generation across each walk, invalidating a walk interrupted by a relevant change. Require trustworthy inputs for the whole’s total and reachability calculation, including kinds it cannot remove and incomplete readings. Prove a move during a walk and stale tree bytes authorizing removal of another kind.

9. **Not blocking — interrupted browser clearing leaves bytes outside the browser-cache kind.**

   **Where:** design `:441–442`, `:694–695`, `:711–713`; `Platform/BrowserCaches.cs:67–85`; `Platform/SystemSpace.cs:50–54`, `:87–88`; `Platform/SpaceFree.cs:485`.

   Cache clearing renames a directory to a random suffixed sibling. If deletion fails, subsequent clearing and listing visit only the original allow-listed paths. The residue is counted with the surrounding profile rather than browser caches, so neither its part nor the next ordinary clear reaches it.

   **Smallest change:** keep exact pending-removal identities and retry them through the same guarded remover, counting their bytes under browser caches. Add a failed-delete → next-pass proof to 1d.

10. **Not blocking — the shared guards contradict the person’s disposal permission.**

    **Where:** design `:317–319`, `:339`, `:591–609`; `SetAsideKeeping.cs:79–90`.

    Section 3.4 says every door keeps late-written contents and internal links outside build folders. Section 5 permits a person to remove those contents when disclosed. Those are different outcomes from the purported single judgement.

    **Smallest change:** make the judgement explicitly door-dependent. Path traversal and unreadability remain absolute refusals; disclosed late contents may be removed by the person’s selection. Define what observation is compared between listing and pressing so a changed folder is actually detected.

11. **Not blocking — correct the remaining behavioral prose.**

    **Where:** the design locations and code cited in section 1.

    *Clean up…* keeps own-scratch trees; an open file must specifically prevent the rename to establish `busy`; logs are grouped by source; recovery retention dates from ref creation rather than when a holder appears.

    **Smallest change:** correct those sentences and qualify the open-file proof accordingly. These corrections require no new product decision.

**11 findings; 8 blocking.**

**Verdict: stands after named amendments.** Findings 1–6 must amend KEEPSIZE1b before it is built; finding 7’s ownership rule belongs before 1b’s schema, with its indexing proof before 1c; finding 8 must amend 1d. The record-first direction, the owner’s scratch decision, the hour measured from `recorded`, and the allowance’s stopping rule stand. The deletion protocol needs the named amendments before its proofs can support D195’s acceptance claim.

## Codex, KEEPSIZE1b's branch at `427fb6cda` (2026-10-24)

Codex's read of the branch, verbatim. What it named is taken on the branch (D195's KEEPSIZE1b notes).

### 1. What could delete what it must not

Read branch `daoris/s-6ec2ba85` through `427fb6cda`: six commits, compared with `main`, against design §§2–3, §7 row 1b, §§10–11 and D195’s acceptance note. This was static inspection; I ran no build, test or Daoris process, made no network call and opened nothing under `local/`.

The normal sequence is substantially present: judge the tree, keep its tip where needed, write the manifest before moving, remove the branch at the judged commit, then post the record outside the repository hold. A failed manifest write keeps the tree. Ref cleanup after a refused removal verifies that the branch still protects the commit. Active-session checks remain on the sweep’s path, under the repository hold.

The remaining deletion risks are:

| Door or condition | Read |
|---|---|
| **The hour** | The purge accepts a persisted holding check up to an hour old. A recovery ref can disappear, the line can rewind or the branch can reappear after that check, and the purge still removes the folder. A registry outage does not invalidate an existing stamp. Findings **1, 5**. |
| **The branch’s removal** | The newly required recovery ref is created before removal, but is not added to the refs verified by the branch-deletion transaction. Findings **2**. |
| **The days** | Ordinary unmarked folders retain their name-based days; `deleted` folders normally have no removal date. However, a 404 changes an awaiting-record folder permanently to `unmarked`, allowing removal without its record. Finding **3**. |
| **The press** | It posts before removing, but drops the class supplied by the list. A folder offered as recorded can become unheld and still be removed under the earlier confirmation. Finding **4**. |
| **Missing or wrong manifest** | Missing, unreadable and unsupported manifests fall back to the days, never the hour. A syntactically valid manifest’s identity and facts are trusted; no new proof exercises a manifest belonging to another folder. |
| **Links** | Source and destination ancestry checks, recursive reparse-point checks and `.going` checks are present. Links inside build folders are removed as links. The documented external replacement race between checking a directory and reading it remains; these checks are not an atomic filesystem guarantee. |
| **Long paths** | Git retains `core.longpaths=true`; filesystem removal retains the .NET implementation. The existing long-path process test is affected by the new output, however. Finding **7**. |
| **Unresolvable repository** | A recorded folder becomes unmarked when the resolver runs and cannot resolve it. An unread registry runs no record step. The stale-stamp case remains unsafe. Finding **1**. |
| **Strays, leftovers and older folders** | Their ordinary route remains the days or the person’s explicit early removal. They acquire no hour merely by their age. The listed leftover proof is absent. |
| **`.going`** | Removal renames first, then deletes. A subsequent purge deletes leftovers there without another eligibility judgement, as the design specifies. The new test does not establish every claimed reader’s exclusion. |
| **Records** | Session deletion removes links and the last linked record transactionally. Orphan records lack an independent ageing/clearing path. Finding **8**. |

The `read` threshold addresses the judgement-to-move write interval in later filesystem checks. It does not make the later scan-to-rename interval atomic. I found no new unconditional path that deliberately takes a session’s live tree, but the required end-to-end refusal and interruption proofs are incomplete.

### 2. What the proofs prove

`KeepSizeGitTests` is marked `Process`/`Git`, but its record writer is generally a successful delegate, not the service store. `WorkRecordStoreTests` uses SQLite with synthetic commits and paths; `WorkRecordHostTests` exercises HTTP with synthetic record facts.

“Fails without” below means the relevant behaviour is asserted. Where mutation evidence exists, I distinguish it from inference. None was rerun during this read.

| Row 1b’s listed case | What exists | Would fail without the change? |
|---|---|---|
| Landed record, commits, tip, ref, scratch; hour; rename first | `KeepSizeGitTests:55` uses real Git and checks the posted facts and hour. It uses a fake post; an empty `.going` afterwards does not prove rename-before-delete. | Hour regression: yes, reported seen failing. Rename ordering: not established. |
| Multi-commit squash; both branches removed | `:102` manually squashes and reaches the existing **content-hold** path. It reads subjects from the recovery ref. | The archive explicitly says it passed without the new ref change. It misses the recorded squash-landing path. |
| Carried tree, both paths; origin-only line | `:361` covers origin-only on the look’s path. | Origin/look: reported seen failing. Carried/look, carried/press and origin/press: **no test**. |
| No package folder, manifest and hour | `:180` checks the empty paths and current `whole`; `:55` supplies ordinary hour coverage. | Manifest/hour assertions cover the new behaviour collectively. |
| Write between judgement and move | `:151` creates the file afterwards and backdates its timestamps into that interval. | Yes for the threshold comparison; it does not produce the actual interleaving. |
| Real branch deletion refused; next look resumes | `:231` writes `refused` by hand, recreates the branch, then calls `CleanAsync`. | Tests resume machinery, not production of `refused` or the specified next-look sequence. |
| Stop at `judged`; slim preserves manifest; press resumes | `:231` writes `judged` after a completed removal. | Manifest preservation is asserted. Actual stop, outstanding registration and press resume: **unproved**. |
| Branch merged its line; first-parent record | `:130`, real Git. | Yes: the expected subjects exclude the line’s separate commit. |
| Pass and press post twice; one record | Store and host tests post twice using synthetic facts. | Idempotency is asserted; pass-plus-press integration is **absent**. |
| Record posted after sessions cleared; age and clear | Store test covers a later clear whose other sessions supply a sufficiently recent cutoff. | That selected clear is asserted. Independent ageing by the orphan’s `at`: **no test or complete path**. |
| Newer store refused; folder remains `deleted` | Store test reopens after changing the stamp. | Reopened-store refusal is asserted. HTTP 409 and the driver preserving the folder: **unproved**. |
| Hour measured from a late record | `:203`, real Git with a fake record writer. | Yes for move-time versus supplied record-time. Delayed acknowledgement is untested. |
| `.going` excluded by readers and cleared; unresolved registry | `:308` checks two lists and later deletion; `:288` covers unresolved registry. | Registry behaviour is asserted. The `.going` fixture’s ordinary depth would already evade readers expecting a timestamped folder one level higher; slim exclusion is not demonstrated. |
| Service unavailable; retry from manifest; press posts first | Modified `KeepingSlimTests` exercises the look with a 503 stand-in and subsequent retry. | Look/retry behaviour is asserted. Press ordering: **no test**. |
| Stop after every step; manifest write refused | `:342` blocks manifest creation through the filesystem. Other interruption coverage rewrites states. | Manifest failure is meaningfully asserted. Stops after every step: **absent**. |
| Recovery ref dropped by hand | `:264`, real Git. | Yes for the next record pass unmarking it; not for a drop after that pass. |
| Stray and leftover avoid hour; both purge failures retained | `:324` constructs one unmarked stray directly. | Stray timing is asserted. Actual leftover and both classes’ failure retention: **absent**. |
| Two sessions, one tree, bounded attribution; elsewhere base | SQLite tests with invented commit IDs and tree paths. | Attribution logic is asserted. Attribution through an actual moved tree’s post: **absent**. |
| `whole` keeps days, record retained | `:180` covers `whole` set at purge time. | Yes for current policy. `whole` persisted at the move is not separately proved. |
| History clear and single delete remove last-linked record | SQLite tests exercise the actual ledger/history classes. | Yes; the archive reports the removal cases seen failing without cleanup. No real-Git integration. |
| Open file keeps folder whole, `busy` | `:380`, real Git, Windows-only. | Whole-folder survival is asserted. No reported mutation establishes that this catches removal of the rename-first step. |

The archive reports mutation evidence for the landed/hour, origin-only, session-delete, fresh-check and `.going` link guards. It expressly reports weaker evidence for most other cases. Moving the missing cases to KEEPSIZE1b2 records the debt; it does not supply row 1b’s accepted deletion proof.

### 3. Everything else that is wrong

The store’s basic separation is sound: its tables are independent of the derived knowledge entries, the version stamp is never lowered, posting is transactional and idempotent, and linked records are removed with their last session. Version 1 introduces the tables; there is no older record schema migration to demonstrate yet.

Three implementation details still need correction: orphan lifecycle depends on unrelated later sessions, the newer-version refusal is cached at open, and the Git log parser stores presentation-escaped filenames rather than reliably preserving names. The record pass also consumes one budget before the purge receives another full budget.

English and Chinese received matching keys and placeholders, and the glossary escape is corrected. The presentation remains incomplete: early removal shows aggregate classes without the individual holders, the card omits its promised waiting-class information, and upcoming recorded folders disappear from *next* until a holding stamp exists at or after their hour.

The records acknowledge several omissions, but they also overstate completion. In particular, `docs/task-archive.md:20320` still says posting happens before the press lists; the final implementation deliberately keeps that list read-only and posts during execution. D195’s later note describes that change correctly. Its statements that a press without service takes no recorded folder and that one budget bounds the pass are broader than the code.

### 4. Findings

1. **An old holding check authorizes a new deletion — blocks the merge.**  
   `src/Daoris.Desktop/Daoris.Desktop.Driver/Trees/SessionTrees/SetAsideEligibility.cs:73` accepts `Held` for an hour. Check successfully, drop the recovery ref or recreate the branch, then purge a minute later: the folder remains eligible. An unread registry likewise leaves that authorization intact. **Smallest change:** obtain a holding check for each removal immediately before its rename; do not reuse a persisted stamp across passes. Add the post-check ref-drop, branch-recreation and registry-outage cases.

2. **The branch-deletion transaction does not protect its new exact-tip holder — blocks the merge.**  
   `src/Daoris.Desktop/Daoris.Desktop.Driver/Trees/SessionTrees/TreeSweeps.cs:189` creates the recovery ref, but `:251` passes only the earlier `rests` to deletion. A ref removed between those operations does not refuse the branch delete; a squash/content/carried proof can still succeed without preserving the original commits. **Smallest change:** include the chosen exact-tip holder, at the commit read, in that transaction’s verifies. Apply the same discipline when the separate local-line read supplies the holder.

3. **A 404 abandons an awaited record and enables the days’ delete — blocks the merge.**  
   `src/Daoris.Desktop/Daoris.Desktop.Driver/Trees/SessionTrees/FinishedRecords.cs:104` changes `deleted` to `unmarked`. It then stops retrying even if the service is upgraded, and the folder can disappear without its record. This exception is absent from the acceptance rule and contradicts the awaiting-record row. **Smallest change:** retain `deleted`, retain the refusal reason and retry on 404.

4. **The press never enforces its listed-class guard — blocks the merge.**  
   `src/Daoris.Desktop/Daoris.Desktop.Driver/Platform/SpaceFree.cs:391` sends only `Own`; `SpaceAsked` also discards the class. Consequently `SetAsideKeeping.cs:96` never receives a class from a production door. A recorded folder can become unheld during the run and still go under its earlier description. **Smallest change:** carry class and holder through the request and compare consistently before removal, returning `changed`. Keep this requirement in 1b.

5. **The record’s hour starts before acknowledgement — blocks the merge.**  
   `src/Daoris.Desktop/Daoris.Desktop.Driver/Trees/SessionTrees/FinishedRecords.cs:116` writes the pass’s previously captured `now` after awaiting Git and the service. A slow post, or later folder in a slow pass, receives less than an hour after acknowledgement. **Smallest change:** stamp successful completion using an injected clock read after the acknowledgement; prove a delayed post leaves the full hour.

6. **Required destructive-path proofs are deferred — blocks the merge.**  
   `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Trees/KeepSizeGitTests.cs:102` exercises the already-protected content path; `:231` manufactures interruption/refusal states. D195 `:609` defers these and the other missing cases detailed in §2. **Smallest change:** complete row 1b’s missing real-Git cases, including actual squash/carried paths, refusal, interruptions and press ordering, and record the relevant failing mutations before calling 1b finished.

7. **Existing Git process assertions conflict with the new output — blocks the merge.**  
   `src/Daoris.Desktop/Daoris.Desktop.Driver/Trees/Branches/BranchTidying.cs:607` adds a record-kept line to a successful look. Unchanged process tests still require exactly one line, including `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Trees/BranchTidyingFolderTests.cs:268` and several neighbouring cases. With the new successful stand-in record door, these assertions receive both removal and record progress. **Smallest change:** update their expected semantic output and run the affected existing Git process family. This failure is identified statically, not reproduced here.

8. **An orphan record has no independent ageing or clearing route — does not block the merge.**  
   `src/Daoris.Service/Daoris.Service.Core/Asks/History.cs:310` derives the cutoff exclusively from selected sessions. With only orphan records remaining, no selected session supplies a cutoff or history unit, so those records cannot age out by their own `at`. **Smallest change:** make orphan records independently enumerable and clearable, using the explicit history cutoff rather than an unrelated session’s update time.

9. **A live store handle ignores a subsequently newer version — does not block this first-schema merge.**  
   `src/Daoris.Service/Daoris.Service.Core/Sessions/WorkRecords.cs:156` bases refusal on the version cached at open. Change the stamp after opening and the original handle still reads and posts, although deletion helpers reread it. Schema-specific setup also precedes the version check at `:98`. **Smallest change:** check the live stamp inside the operation’s gate/transaction and before schema-specific migration work; test the already-open handle.

10. **Commit filenames are parsed as display text — does not block the merge.**  
    `src/Daoris.Desktop/Daoris.Desktop.Driver/Trees/SessionTrees/FinishedRecords.cs:152` requests non-NUL numstat output; `:157` trims lines and `:163` stores Git’s escaped path unchanged. Chinese names, quotes, whitespace and rename notation need not survive as actual filenames. **Smallest change:** parse NUL-delimited numstat and preserve filenames exactly, with representative real-Git cases.

11. **The record pass does not share the purge’s remaining budget — does not block the merge.**  
    `src/Daoris.Desktop/Daoris.Desktop.Driver/Trees/Branches/BranchTidying.cs:280` spends the pass budget on records, then `:620` hands the purge the full `Budget` again. `ClearGoing` also runs before the purge’s budget checks. **Smallest change:** pass the remaining budget through and check it between `.going` folders.

12. **The card, press details and archive overstate what is shown or scheduled — does not block the merge.**  
    `src/Daoris.Desktop/Daoris.Desktop.Driver/Platform/SystemSpace.cs:585` uses eligibility requiring a post-hour stamp to calculate *next*, so a newly recorded folder has no upcoming hour shown. `src/Daoris.Web/src/settings/wiring/WiringSpaceFree.tsx:105` renders class totals without individual holders; the card lacks its waiting-class information. `docs/task-archive.md:20320` retains the obsolete “before the press lists” claim. **Smallest change:** calculate the scheduled hour separately from removal authorization, show the promised class/holder facts in both languages, and amend the archive’s outcome.

**12 findings; 7 block the merge.**

**Another build.** Close findings 1–7, retain the acceptance note’s awaiting-record rule, and return with the missing destructive-path proofs and the affected existing Git process checks. The current branch still permits deletion on stale evidence and deliberately abandons some records before their folders go; those are changes to the accepted safety contract, beyond a steward’s documentary completion check.
