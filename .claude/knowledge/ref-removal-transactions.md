---
name: ref-removal-transactions
applies_when: deleting or dropping a git ref (a branch, a recovery ref, a kept ref) on a proof that read other refs, such as "the line holds its commits" or "another branch holds this tip", or choosing which ref such a proof rests on
enforces: the removal and what its proof rested on go as one update-ref --stdin transaction, a verify for exactly each ref the proof read at the commit it read, never delete-then-look-again; a holder is a form of the line, else a branch of the person's before a session branch
---

# Ref removals that rest on other refs — one transaction, verifying exactly what the proof read

**A ref removed because other refs hold its work goes in one `git update-ref --stdin` transaction: a `verify <ref> <commit>`
for each ref the proof rested on, at the commit the proof read it at, then the `delete <ref> <judged>`. Verify exactly what the
proof returned, never what a later read finds. Where the proof chooses a holder, a form of the line comes first, then a branch of
the person's, and a session branch only after those.**

## Why

`update-ref -d <ref> <old>` compares the ref it deletes and nothing else. A proof that read the line, a holder or a landing's
branch is stale the moment it is read, and a delete that does not check them under git's locks removes a branch whose work is
no longer anywhere. FABRIC-END4a took four reviews to close this, each finding a gap the round before left (D173's FABRIC-END4a
note, *After review* to *After the fourth review*):

- **Delete, then look again** read the line's forms after the branch went and put it back where they had moved. Between the two,
  every page, look and session saw the branch absent, and a put-back can itself be refused. The second review replaced it with
  the transaction: a refused one deletes nothing, so nobody sees the branch gone.
- **Verifying what is found afterwards** (round 3's `ProofRefsAsync`) read the holder again after the judgement and fell back to
  the line where none was found. A person's branch deleted in between let a line that never held the tip authorize the delete.
- **Verifying only some of the proof** left a content holder, a landing's holder and a squashing landing's branch unchecked,
  and a landing's own rests (the line it was proved on) were not carried to the session branches it held, so a line rewound
  after the landing let them go (the fourth review's `LineLanding.Rests`).
- **A session branch as holder** can go in the same pass as the branch it proves, so it holds nothing by the time the second
  delete runs.

The trap succeeds wrongly: every one of these deleted without an error.

## How to apply

- **The proof returns what it rested on.** Each judgement hands back the refs it read, each with the commit it read it at
  (`(Ref, Commit)` pairs), and the removal verifies those and nothing else. No helper looks for them afterwards. Handed nothing,
  the removal refuses: an empty list is no proof (`ContentHolds.DeleteJudgedAsync`).
- **Compare at the commit read, never by the live name.** Read each form of the line once with its commit and make every
  ancestry or content comparison against that id; a `refs/heads/main` re-resolved later is a different read.
- **What the proof rests on travels.** A branch proved through another (a landing, a branch inside another) carries that one's
  rests too, down the chain, into its own transaction.
- **Choosing a holder by ancestry:** one `for-each-ref --contains <tip>` read, with no symbolic ref, no `HEAD` and none of
  Daoris's own recovery refs (git-ref-absence's last point). Take a form of the line first, its branch then origin's copy; else a
  branch of the person's (D88: local outside `daoris/`, remote-tracking outside `*/daoris/`); a session branch only after those,
  and only where the caller admits one (the tidy, the sweep and a tree's removal admit the person's alone). None found is a
  refusal, never a fallback to the line (`ContentHolds.HolderOf`, `PersonsRef`).
- **The same for a recovery or kept ref.** It goes only in a transaction that verifies the branch, or the holder, at a commit
  shown to hold what the ref keeps (`ContentHolds.DropKeptAsync`, `DropKeptVerifiedAsync`), so it outlives any put-back that did
  not happen.
- **A ref that must be absent is `verify <ref>` with no id**, beside the `create` that rests on its absence.
- **A refused transaction is read, not assumed.** After one, the branch is gone only on git's own "missing"; any other answer is
  unread and keeps the recovery ref (`.claude/knowledge/git-ref-absence.md`, `ContentHolds.RefusedAsync`).
- **What no ref says stays a separate check.** `update-ref` asks no working tree, so a checkout made after the trees were read is
  outside the transaction: read the trees before, and again after, and put the branch back create-only where one has it out. A
  line moved forward in that moment refuses too; it is judged again at the next pass, which is the cost taken for the guarantee.
- **Proof:** a Git test per rest, red without its `verify`: the holder vanished, the content holder moved, the landing's branch
  moved, the line rewound between proof and act (`JudgedDeleteGitTests`, `TidyEffectGitTests`).
