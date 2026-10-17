---
name: git-ref-absence
applies_when: reading whether a git ref exists before forgetting, retiring or deleting something on the answer, or reading a ref's commit where a failure would be taken as "no such ref"
enforces: only git's own "missing" says a ref is absent, show-ref --exists exiting 2; any other failed read is unread, and an unread read keeps what it would have forgotten and says so
---

# Git ref absence — only git's own "missing" says a ref is gone

**A record forgotten, a trace retired or a recovery ref dropped on "the branch is gone" must rest on git saying
the ref is missing. `git show-ref --exists <ref>` exiting 2 is that word. Every other failure is unread: keep
what the answer would have forgotten, and say git could not read it.**

## Why

The commands that read a ref quietly cannot tell absence from a ref they could not read, and two of them fail
the same way for both. FABRIC-END4a's fourth review found a refused delete reading a broken ref as "gone
already", and RECORDUNREAD1 found three more paths that forgot or retired a record the same way (D173).

| Command | Present | Missing | Broken ref, or another failure |
|---|---|---|---|
| `rev-parse --verify --quiet <ref>` | 0, its id | 1 | 1 (a warning names a broken ref) |
| `for-each-ref <pattern>` | listed | not listed, exit 0 | not listed, a warning, exit 0 |
| `show-ref --exists <ref>` (git 2.43+) | 0 | 2 | 1, with git's words |
| `merge-base --is-ancestor <a> <b>` | 0 is an ancestor | 1 is not | 128 or another code: no answer |
| `cat-file -e <full id>` (an object, not a ref) | 0 | 1, saying nothing | 128, a malformed name |

`for-each-ref --contains <id>` exits 129 for an id the repository does not have, as for any other refusal: ask `cat-file -e` before
reading that as "held by nothing" (CLEANUP1).

Read on git 2.53 with a loose ref written over with text that is no commit. A git older than 2.43 has no
`--exists` and refuses the option, so there a missing ref reads as unread: kept, never the other way. The
bundled MinGit is newer.

## How to apply

- **Read first, ask absence only on failure.** Keep the read the path already makes (`rev-parse`, a snapshot
  of `for-each-ref`), and where it found nothing ask `show-ref --exists`. The driver's helper is
  `ContentHolds.MissingAsync`: it answers missing only on exit 2, with git's words otherwise. One git start
  more, only where the ref read absent.
- **A list is not a census.** A name a `for-each-ref` list left out is absent only once `show-ref --exists`
  says so, since the list skips a broken ref and still exits 0.
- **An exit code is a word only where git defines it.** `merge-base --is-ancestor`'s 1 says "not an
  ancestor"; anything else is no answer, and is unread rather than "no longer holds it".
- **Missing may still stand in another case.** On a disk blind to case, `show-ref --exists refs/heads/feature/x` exits 0 for a
  loose `Feature/X` and 2 once it is packed (read on git 2.53, Windows): packed refs compare names exactly. Before forgetting on
  "missing", look for the name in another case in the branches' list; one there is a doubt, kept (CLEANUP1).
- **Unread keeps and says.** The record stays, the branch is kept, a recovery ref stays, and the answer says
  git could not read it, with git's first line.
