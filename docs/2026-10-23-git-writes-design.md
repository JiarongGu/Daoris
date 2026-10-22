# The person's git acts and writes: the design (GITAPP2, 2026-10-23)

**Status: proposed (D204), not yet read.** Split out of GITAPP1's design (`docs/2026-10-23-git-app-design.md`, D202) in its round 3,
so Review and the reader can be accepted and built first. The steward narrowed D202 to the reads; everything the person *does*
through Daoris's git (the checkout lock, the ref and remote acts, history edits, stash, staging, the Changes tab, the switch, and
D198's prerequisites for a push) moved here whole. **This document states what GITAPP1's round 2 said, unchanged** (at `a8b741993`,
design §4.4's act rows, §7.2, §7.3, §8, §9's writing verbs, §10.2's acts, §11's act surfaces, §12 rows 10-15 and §13's write
amendments), and lists what Codex's round-2 read and the open round-1 findings ask of it (§7). It is read later under D191, not in
GITAPP1's round 3. `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`; "Codex n" names Codex's round-1 findings and "Codex R2-n"
its round-2 findings, both in `docs/2026-10-23-git-app-reads.md`; GITAPP1's design is "the read design".

## 1. What it rests on

The read design's reader (§4 there) and its observation (§5 there): every act reads through the same `GitReader`, its plan names the
ids it read, and its re-judgement reads them again under its lock. The brief (`docs/2026-10-23-git-app-brief.md`) and the three
proposals (`docs/2026-10-23-git-app-proposals.md`) are this design's too; the comparison of the three on the acts is the read design's
§2 rows *Working-tree acts*, *Ref-only history edits* and *Conflict resolution*.

## 2. Where the working-tree acts live, and the checkout lock (round 2's §7.2)

The three proposals answered differently. **Chosen: the person's checkout, behind the switch** *Daoris may change this checkout's
files* (允许 Daoris 修改此检出中的文件), off by default, on the repository's Setup under **Reach** (边界, D150 point 3: the section about
what writes into the repository; Fable N11), its state said on every act's plan. A git management app works where the person's
changes are, beside their editor, and the switch is the person's say-so, made once and visible. **Codex's manual tree is rejected**:
the person's edits are in their checkout, so a separate tree has nothing of theirs to stage. **Fable's idle session tree is
rejected**: the session's work is the session's until it lands (D51, D164), and its agent would find commits its transcript and trace
do not explain. Both reads keep this.

**The checkout lock** (Codex 20, Fable N6). One `CheckoutLock` file per registered checkout, under the home's `locks/`, by
`TreeLock`'s pattern (`Driver/Trees/TreeLock.cs`: the file system's sharing, every Daoris process honouring it, a dead process
letting go with its handles, never waited for). Taken:

- **alone** by every Changes-tab act, for its re-judgement and its effects, and by `TreeLands.MergeAsync`, the merge landing that
  writes the checkout (`TreeLands.cs:162-168`), around its clean-and-on-the-line checks and its merge;
- **shared** by a conversation starting in the checkout (`ChatRunner`, `ChatGoOn`), from choosing the checkout until its record is
  open, as `TreeLock` holds a tree's start today; the open record then names it.

**Under the lock, immediately before any effect**, an act re-reads: the switch; the checkout's identity (its top, its common
directory, that it is the registered checkout and not a session tree, which is refused **independently of the switch**); the ledger's
running or waiting conversations naming it; an operation in progress (`MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD`, `rebase-merge/`,
`rebase-apply/`, `BISECT_LOG`); `HEAD` and the index entries the act touches; and the bytes the plan showed. Any difference refuses,
naming it. A landing that finds the lock held is held for this look, as a start that finds a replay is.

**Refused in the checkout**, with the reason: a hard reset, `clean`, a force anything (they lose work with no recovery); a recursive
submodule write; a merge or rebase of the checkout's branch (D109's and the landing's door); any act while a conversation runs there
or a landing holds it.

**The Changes tab reads three observations** (Codex 25), each by REVIEWLOCK1's rule: **HEAD → index** (`diff --cached h`), **index →
worktree** (`diff`, unrefreshed, stat-only rows dropped), and status's untracked and conflicted entries (`u` lines of
`--porcelain=v2`), with each index entry's blob id from status. A file with staged additions and matching unstaged reversals appears in
both comparisons, with both controls. A stage press applies a patch Daoris composes from the index → worktree hunk or line, with
`apply --cached`; an unstage press, from the HEAD → index hunk, with `apply --cached --reverse`; never through an editor, and never
from the read design's §6.2 display addition. At the press, the index entry's blob id and the worktree file's bytes must equal those
the hunk was composed from.

## 3. History edits with no working tree (round 2's §7.3)

Cherry-pick, revert, rebase onto, and rename, applied by one ref transaction at the judged old id (§4.2), on a branch **no tree has
checked out** (the list's `worktreepath`, read before and after the effect), never the line, a session branch or a landed branch.
`git replay` is not used: git's own documentation marks it experimental.

**Supported, precisely** (Codex 23, Fable N9): **a linear range of non-merge commits, none of them a root commit, none published**:
reachable from no remote-tracking ref, as D109's replay refuses to rewrite published history (a force push being refused later does not
make the rewrite acceptable now). Merges in the range, a root commit, and cherry-picking or reverting a merge are refused, said.

- **Pick C onto T**: `merge-tree --write-tree --merge-base=C^ T C` (round 2 said git 2.40 and later; `--merge-base` is in git's
  `RelNotes/2.40.0.adoc:6`), then `commit-tree` with C's author name, email and date and C's message unchanged, the committer being the
  checkout's configured identity first (D198 point 5).
- **Revert C on T**: `merge-tree --write-tree --merge-base=C T C^`; the message git's own (`Revert "<subject>"`, a blank line, `This
  reverts commit <C>.`).
- **Rebase B..tip onto O**: pick each commit in order onto the last result.
- **A result whose tree equals its parent's** (an empty commit) refuses the act, naming the commit; it is not dropped silently.
- **Signed commits** in the range refuse: Daoris does not re-sign, and a rewrite would drop the signature.
- **Every id is pinned**: the source range, the target tip and the branch's old id are read once, and the transaction verifies them.
- **Cost said**: 2n serial starts for n commits; progress streams with the time; past 50 commits (provisional) the act refuses and
  names the command to copy. A conflict anywhere refuses the whole act, naming files and hunks; nothing has moved, since objects alone
  were written.

## 4. Writes (round 2's §8)

### 4.1 What every act does

Every act is planned, shown with the command it will run, **the hooks it will run** and the effects beyond its row, and judged again
at the ids shown under its lock (D147 §3.1). An act that reaches a remote passes D198's doors (§4.3). Daoris's own writes are
serialised per ref by git's locks and `update-ref`'s old id, and per checkout by §2's lock (Codex X6).

**Effects beyond the row, said in the plan** (Codex 21, Fable N8): a commit, amend, switch or stash runs the checkout's hooks
(`pre-commit`, `commit-msg`, `post-commit`, `post-checkout`, a formatter that rewrites files in place), named where the repository has
them, never skipped (`--no-verify` is not offered); and git's auto maintenance, as the person's own `git commit` would. Every
`update-ref` runs a `reference-transaction` hook where one exists. A rename moves the branch's configuration section (§4.2). A
**fetch Daoris runs** passes `-c maintenance.auto=false -c gc.auto=0 -c fetch.writeCommitGraph=false`, so nothing of Daoris's own
choosing is written beyond the remote refs and objects (D147 §4.2). Reflogs are git's and are written as git writes them.

**No `push -u`, no upstream tracking**: they write the checkout's configuration (D147's reason stands).

### 4.2 How each write recovers

**Ref removals and rewrites reuse the judged-delete transaction** (Codex 22, Fable N14; `.claude/knowledge/ref-removal-transactions.md`,
`ContentHolds.DeleteJudgedAsync`): one `update-ref --stdin` transaction carrying a `verify <ref> <commit>` for exactly each ref the
judgement read, at the commit it read it; a `create` of the recovery ref (D102's SQUASHTIDY1c note, CLEANUP1's pattern) in the same
transaction; then the act's own `update`, `create` or `delete` at the judged old id. A refused transaction changes nothing and is
**read, not assumed**: absence is only git's own "missing", any other answer is unread and keeps the recovery ref
(`.claude/knowledge/git-ref-absence.md`; `for-each-ref` can skip a broken ref, so a snapshot's omission is never proof of absence).
**Late checkout** (Codex 22): `update-ref` asks no working tree, so the trees are read before and again after; where a tree checked the
branch out in between, the branch is put back create-only at its old id and the act says so. This **occupancy rule extends to
rewrites and renames**, not deletes alone. *(Codex R2-12 and R2-13 find this paragraph unbuildable as written: §7.)*

| Act | Effect step | Recovery |
|---|---|---|
| Fetch | git's own fetch | none needed: remote refs only; prune's removals named before and after |
| Create a branch or a local tag | `update-ref --stdin`: `verify <name>` (absent), `create` | delete it |
| Delete a local branch or tag | the judged-delete transaction, with its recovery ref | the recovery ref |
| Rename a local branch | one transaction: `verify` the proof refs, `create` the new name and the recovery ref at the old id, `delete` the old name at it; then `config --rename-section branch.<old> branch.<new>`, its old contents kept in the act's record (the one configuration write, Codex 21); a new reflog, said | rename back; the section restored from the record |
| Pick, revert, rebase onto (§3) | objects first, then one transaction: `verify` source and target ids, `create` the recovery ref, `update` the branch from its old id | the recovery ref; nothing moved on a conflict |
| Push one branch | git's push of `<id>:refs/heads/<name>`, no `+` | none (outward), said (D37) |
| Stage, unstage | `apply --cached`, under the checkout lock | the opposite press |
| Commit, amend | `commit`, under the lock; amend first `create`s a recovery ref at the old tip | the recovery ref |
| Stash save | `stash push`, tracked files only unless *Include untracked* is chosen, said | apply it |
| Stash apply | refused unless the checkout is clean and no untracked file the stash writes exists; `stash apply` (never `pop`) | **retained conflicts are the supported outcome** (Codex 24): git has no "abort apply", so the plan says a conflict leaves the conflicted files for the Changes tab's resolve, with the stash kept |
| Stash drop | resolved by the stash's **commit id**, not a moving ordinal: the id at `stash@{n}` re-read under the lock, its commit `create`d as a recovery ref, then dropped; a stash whose message names a `daoris/` branch is a session's and refused, since `refs/stash` is shared across linked trees | the recovery ref |
| Switch branch | refused dirty, mid-operation, or where the branch is checked out elsewhere; `switch <branch>`, never `--force` | switch back |
| Resolve a conflict | the chosen side or both per hunk written to the file, then `add` | the file's replaced bytes kept beside the operation until it ends |

**Who owns an act's git process** (round 2's §4.4 act row): the act's transaction; started after the act's re-judgement under its
lock; ended at exit; cancelled **before its effect step only**, since once an effect step starts it runs to exit and its recovery
runs; at window exit or a verb's end waited for, never killed mid-effect, the window's close waiting for a running act and saying so.
`daoris-driver git …`'s writing verbs: an act already in its effect step finishes on Ctrl-C. *(Codex R2-15 finds this conflicts with
today's bounded runner: §7.)*

### 4.3 D198's prerequisites, corrected

- **Every remote act** (fetch, push) needs **the doors installed** on that install: their installation is still opt-in at publish
  (`tools/desktop-publish.mjs:1326-1340`; Codex 2, 26). Where they are not, the act is not offered, and the plan says why. GITAUTH1c's
  identities store is partly built (`GitIdentities.cs`, `GitIdentityCommand.cs`, `GitAuthor.cs`), its remaining proofs standing.
- **A fetch** is a Read at the door.
- **A person's push** is a Write, which the door allows only with a grant or `no-push` off (`GitAccessRules.cs:40-46`). It needs
  **GITAUTH1f** (the person sets Write) and **a grant of its own**: the press writes a `GitGrants` grant for origin's push address,
  handed to the push's process as the landing hands its own (Fable N7), never borrowing a landing's grant (Codex 26). The push's
  refspec carries no `+`, so git's client refuses a non-fast-forward against what the remote advertises; the door, judging Write
  only, cannot itself prove the update is a fast-forward, and the plan does not claim it does.
- **A tag push** is Dangerous, and the door can see a tag update only once **GITAUTH1g**'s transport judge reads each update
  (`docs/2026-10-22-git-auth-design.md` §6.1). It waits for GITAUTH1g and is not offered before it.
- **A terminal writing verb keeps the person's press** (Codex 27): each `daoris-driver git` writing verb is a `PersonPresses` route of
  `RouteClass.Person` (`InstanceRoutes.cs:9-21,156-170`): sent to the running window, shown there and run on the person's Confirm
  (RPC1c), the verb saying it waits; with no window answering, the standalone-terminal rule the other person's presses follow applies
  (`trees land --yes` today). `PipeCensusTests` and the verb census hold each. The `DAORIS_SESSION_ID` refusal stays, as an extra
  guard: an absent marker is never taken as proof of a person.

**The writing verbs** (round 2's §9): `daoris-driver git branches|fetch|branch|rename|delete|push|tag … [--plan|--yes]`,
`pick|revert|rebase … [--plan|--yes]`, `checkout <repository> stage|unstage|commit|amend|stash|switch|resolve … [--plan|--yes]`, each a
person's press. Ask Daoris's `git` kind proposes the acts as cards the person presses (GIT1k, widened), never applying them; the Setup
switch is proposed through the setting door, never set.

## 5. Proof, surfaces and rows (round 2's §10.2 acts, §11, §12 rows 10-15)

**Proof.** Bare-origin fixtures for push and fetch, the pre-push hook running; every refusal of §2-§4 on a real fixture; each
transaction red without each `verify` (`JudgedDeleteGitTests`' pattern); a late checkout put back; a conflict refusing a pick whole;
a merge, a root commit, a published commit refused; the switch off, a conversation starting, and a merge landing each refusing every
checkout act; a stash apply's conflict leaving the stash kept; a stash drop by id after another drop moved the ordinals.

| Surface | English | 中文 |
|---|---|---|
| Ref acts | Rename · Create tag · Delete tag | 重命名 · 创建标签 · 删除标签 |
| A remote act not offered | Needs Daoris's git doors on this install · Needs push allowed for this repository | 需要在此安装上启用 Daoris 的 git 门 · 需要允许推送到此仓库 |
| History edits | Pick onto… · Revert · Rebase onto… | 拣选到… · 还原 · 变基到… |
| A refusal | Conflicts in {files}; nothing was changed | {files} 中有冲突；未做任何更改 |
| An act's undo | {commit} is kept as {ref} until {date} | {commit} 保留为 {ref}，直至 {date} |
| An act's plan | Runs these hooks: {hooks} | 将运行以下钩子：{hooks} |
| Setup's switch | Daoris may change this checkout's files | 允许 Daoris 修改此检出中的文件 |
| The Changes tab | Changes · Staged · Not staged · Stage · Unstage · Commit · Amend | 更改 · 已暂存 · 未暂存 · 暂存 · 取消暂存 · 提交 · 修订 |
| A hunk and a line | Stage hunk · Stage line | 暂存块 · 暂存行 |
| Stash and switch | Stash · Apply · Drop · Include untracked · Switch | 贮藏 · 应用 · 丢弃 · 包括未跟踪文件 · 切换 |
| Resolving | Take ours · Take theirs · Take both · Open in your editor · Mark resolved | 采用我方 · 采用对方 · 两者都要 · 在你的编辑器中打开 · 标记为已解决 |

*Amend* (修订) and *hook* (钩子) pass D116's check first.

**Round 2's rows**, as it filed them under GITAPP1; renamed GITAPP2a-f when this design is accepted, each landing alone:

1. **The ref and remote acts** (round 2's GITAPP1e; after GITAPP1d-iii; takes GIT1g, GIT1h, GIT1k). Fetch, create, rename and delete a
   local branch, local tags, through §4.2's transactions; push one branch only once GITAUTH1f is built and the doors are installed
   (§4.3). Proof: as GIT1g and GIT1h name it; each transaction red without each `verify`; a late checkout put back; the rename's section
   restored; the person's push refused with no grant and allowed with the press's; the terminal twins as person's presses.
2. **History edits without a working tree** (round 2's 1f; after 1). §3. Proof: each over real fixtures; a conflict, a merge, a root, a
   published or signed commit, an empty result each refusing with nothing moved; a checked-out branch refused, and one checked out
   between judgement and effect put back.
3. **The switch, the lock and the Changes tab read** (round 2's 1g-i; after 1). The Setup switch under Reach, off by default;
   `CheckoutLock` taken by `MergeAsync` and a conversation's start in the checkout; the Changes tab's three observations and its watch;
   no act. Proof: the lock held by each taker refuses the others; the tab reads a staged-and-reverted file in both lists.
4. **Stage, unstage, commit, amend** (round 2's 1g-ii; after 3). By file, hunk and line; the plan naming hooks. Proof: the switch off, a
   conversation starting, a landing holding the lock, and changed index or file bytes each refusing; a formatting `pre-commit` hook's
   rewrite said.
5. **Switch branch and stash** (round 2's 1g-iii; after 4). Proof: a dirty switch refused; an apply's conflict retained with the stash
   kept; a drop by id; a session's stash refused.
6. **Resolve a conflict** (round 2's 1g-iv; after 5). A side per hunk, the person's editor, mark resolved. Proof: the replaced bytes kept
   until the operation ends; a file not in conflict refused.

## 6. Fit (round 2's §13, the write half)

- **D147** would be amended: point 2 and design §2.8 (tags locally, stash, staging, commit, cherry-pick, revert and a linear rebase
  offered in this design's forms; §2.8's "merge-conflict editor" exclusion amended, since *Take ours/theirs/both* and *Mark resolved*
  write a file, while a free three-way editor stays rejected; checkout's reset, clean, a force and a remote delete stay refused);
  point 3 and design §3.1 (acts may write the person's checkout behind its switch; a history edit writes objects and one ref
  transaction); the rejection "Checking out in the person's checkout" (`D147.md:58`) and design §3.1's "Checking out is not offered"
  (`docs/2026-10-04-built-in-git-design.md:136-139`), by the switch. D147 §4.2's named exceptions for acts: a recovery ref, a renamed
  branch's configuration section, and the effects git's hooks and auto maintenance make for an act the person pressed in their own
  checkout. Design §3.1's "D51 rule 7" reads D102's SQUASHTIDY1c note and CLEANUP1.
- **D150 point 3** would be amended: a repository's page gains a **Changes** tab beside Branches and History, shown only where the
  switch is on; the switch lives in Setup's **Reach** section.
- **D198 stands**, its prerequisites as §4.3 has them. A local act passes no door.
- **D51, D55, D109, D164 stand**: a session's tree is the session's, and no person's act writes it.

## 7. Open questions, for this design's first read

From **Codex's round-2 read** (`docs/2026-10-23-git-app-reads.md`, §3), each blocking unless said:

| # | Asks | Touches |
|---|---|---|
| R2-11 | D109's line pull (`LinePulls.PullAsync`, `LinePulls.cs:51-78`, from `TreeSync.cs:209`) runs `merge --ff-only` in the registered checkout and is a second checkout writer the lock omits: put it under `CheckoutLock` from re-judgement through effect and recovery, in the writer census and the exclusion proof | §2 |
| R2-12 | `verify <name>` then `create <name>`, and a verify of a ref the same transaction updates, are two operations on one ref, which git refuses: one operation per ref, expectations in `create`/`update … <old>`/`delete … <old>`, verifies only for untouched proof refs, contradictions refused; success and alias cases tested | §4.2 |
| R2-13 | Create-only restoration cannot undo a rewrite (the branch still exists at the new tip; `ContentHolds.cs:467-475`'s put-back is create-only because a delete left it absent): rollback per operation, conditional on the branch still equalling this act's result; a rename's reverse transaction conditional on both names; an uncertain outcome reported | §3, §4.2 |
| R2-14 | Stash drop is still ordinal-based: `refs/stash` and its reflog are shared across linked trees, and the checkout lock protects one checkout; specify identity-checked reflog removal under concurrent mutation, or defer Drop; delete only positively person-owned entries (a `daoris/` message is not ownership proof) | §4.2 |
| R2-15 | "Effects run to exit, never killed" conflicts with today's runner, which ends git at its bound (`WorkingTree.cs:1022-1059`, `GitBounds`, `ChildProcess.cs:76-87`) and with D181; exit failure does not prove no effect; a rename's ref-then-config window has no durable phase: caller cancellation apart from the backstop, postcondition reconciliation, an explicit unknown outcome, a durable act journal with restart reconciliation, interruption proved at each boundary | §4.2 |
| R2-16 (not blocking) | Row 3's stash apply leaves conflicts "for the Changes tab's resolve", which only row 6 builds: move conflicting apply after Resolve, or give and test the interim path | §5 rows 5-6 |

From **the round-1 findings Codex's round-2 table marks open or wrong** on the write half: **Codex 22** (wrong: the transaction
principle taken, its generalisation unbuildable as stated, R2-12 and R2-13); **Codex 24** (open: drop identity, R2-14); and **Codex
20** (taken, but D109's line pull outside the lock, R2-11).

Carried from GITAPP1's round 3, because the reads it settles feed the acts: an act's plan reads through the reader and its observation
(the read design §5.1-§5.2), so an act inherits the context rules (§5.3 there) and the conflict preview's merge-driver refusal
(§1.1 there) for its own `merge-tree`; history edits' `merge-tree --merge-base` needs git 2.40 (`RelNotes/2.40.0.adoc:6`), checked as
the read design checks its floors.
