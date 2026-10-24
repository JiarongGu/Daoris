# KEEPSIZE1: the reads of round 2 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of round 2 of `docs/2026-10-24-keep-size-design.md` and its note in D195 at `a2993a3bb`, each verbatim but for its headings set one level down; round 1 was rewritten to the owner's directions before any read. The steward ran both from outside the install, read-only, on the design's own tree. Codex asks another round (14 findings, 11 blocking); the Opus foundation reader would accept after named changes (22 findings, 4 blocking). Round 3 answers both.

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
