# Evidence: jj workspaces as an isolation kind (PLACEWORK1i's measurement, 2026-11-11)

The measurement the design's row 1i asks for before `JjIsolation` is built ([place-work design](2026-11-10-place-work-design.md) §4.4,
§4.5, §9): whether `jj` is installed or installable here, what a workspace per session costs on this machine, and how its changes come
back. Nothing in the product changed. Everything below was run on 2026-11-11 with jj 0.46.0 and git 2.53 (Windows 11, NTFS, the
repository's own volume) in a gitignored scratch folder, with `JJ_CONFIG`, `JJ_USER` and `JJ_EMAIL` set per command so no user
configuration was read or written.

## Read this first

- **The machine was busy.** Other sessions' gates and merge runs held the CPU at 86 to 91 % (`\Processor(_Total)\% Processor Time`,
  read before each repetition) for the whole run, and the folder's files are slow to create (a plain `cp -r` of 1,000 small files took
  9 s). Absolute times are upper bounds, and the same machine's `git worktree add` is the yardstick: the ratios are the finding. Where
  a figure has repetitions, all are shown and the best is the one to read.
- **Files are synthetic**: 1,000 and 10,000 text files of about 1 KB in 100-file folders. The design's 100,000-file bench folder was
  not run: generating it took 250 s and its first jj import was stopped before it finished (the 10,000 import took 181 s on the
  loaded machine), so the one 100,000-file figure below is an extrapolation, said as such.
- **Not measured**: symlinks, CRLF conversion (`working-copy.eol-conversion`), paths past 260 characters, a repository with
  `.gitignore`d dependency folders, jj on a network volume, two Daoris homes sharing a repository.

## 1. Installed, or installable here

| Question | Answer |
|---|---|
| On `PATH` here | **No**: `jj` is not found. `cargo` and `scoop` are not present either. |
| Installable | **Yes**, three ways: `winget install jj-vcs.jj` (0.46.0, Apache-2.0); the release zip winget downloads from the project's GitHub releases; `cargo install`. |
| What the zip is | `jj-v0.46.0-x86_64-pc-windows-msvc.zip`, 10,233,241 bytes, SHA-256 `c42316b10df04611bc65029a18bcb2d92636edddb6871d511f7bb4233e999b83` (winget verified it against its manifest), holding `jj.exe` (30 MB), `LICENSE`, `README.md`. A portable binary: no installer, no registry, no service. |
| Fits the built-in resource list | Yes. `Daoris.Desktop.Driver/resources.json` pins MinGit as `url`, `sha256`, `size`, `archive: zip`, `exe`, `paths` per platform; jj's zip has the same shape (`exe: jj.exe`), so a pinned `jj` entry downloaded into the home is the same mechanism and needs no installer. |
| What jj needs beside it | Git **2.42 or later** for a workspace beside the first (0.46's release notes: `jj workspace add` uses `git worktree add --orphan`). The machine's git is 2.53 and the pinned MinGit 2.56, both past it. |
| Identity | jj commits under `user.name` and `user.email`; both can come from `JJ_USER` and `JJ_EMAIL` in the environment of a start, so a session's workspace needs no config file. |

This run fetched the zip into the gitignored scratch folder and ran it from there; nothing was installed machine-wide and the
person's jj configuration was not touched.

## 2. What a workspace costs

`jj workspace add <folder>` makes a working copy of the repository's present commit beside the first, sharing the repository's store
and operation log. It is a checkout of every tracked file; it carries no ignored or untracked file (no `node_modules`, no build
output), exactly as a git worktree does, so the work Daoris does today to prepare a tree's dependencies is the same work here.

**Repositories made by jj 0.46 are colocated by default.** `jj git init` with no flag left a `.git` beside `.jj`, and
`jj workspace add` in it printed *Created Git worktree for the new workspace* and left a `.git` file (`gitdir: …/worktrees/…`) with
git's HEAD detached at the base commit, so `git status` and `git log` work inside the workspace. `jj git init --no-colocate` leaves
`.jj` alone, and a workspace made from it holds `.jj` and the files and nothing else: `git` there finds no repository.

### Wall times (ms), best of the repetitions in bold

| Files | Operation | jj, colocated | jj, not colocated | `git worktree` |
|---|---|---|---|---|
| 1,000 | add | 10,908; 17,020; **5,088**; 8,373; 7,955 | 63,050 (a first run, outlier); **3,986**; 4,096; 5,951 | **1,747**; 2,889; 2,446 |
| 1,000 | forget / remove | **700**; 2,179; 1,453; 1,542 | **642**; 883; 763 | **1,532** (remove); 1,685; 2,578 |
| 1,000 | delete the folder (`rm -rf`) | **1,064**; 1,536; 1,138; 2,768 | **1,072**; 1,616; 1,264 | (in remove) |
| 10,000 | add | 49,270; 83,696; 62,709; 91,921; **52,938** | **45,987**; 53,562; 73,796; 80,246 | **13,732**; 15,072; 16,145 |
| 10,000 | forget / remove | **2,018**; 2,418; 2,962; 7,590 | **1,022**; 1,375; 3,625 | **14,194** (remove); 19,801; 28,199 |
| 10,000 | delete the folder | **16,862**; 17,296; 23,328; 32,152 | **14,918**; 19,305; 19,471 | (in remove) |
| 10,000 | `jj status` in the new workspace | 12,822 | 6,840 | n/a |

- **About 4.6 to 4.9 ms per file to add, three times a git worktree's** (13.7 s against 46 to 53 s at 10,000; 1.7 s against 4.0 s
  at 1,000), colocated or not. The colocated add is the slower of the two where the run was quiet, since it also makes git's worktree.
  At 100,000 files that is 8 minutes or more on this machine under this load, **extrapolated, not measured**.
- **Forgetting is cheap; deleting the folder is the filesystem's cost**: 0.6 to 3.6 s to forget, 15 to 23 s to delete 10,000
  files, which is what `git worktree remove` costs too (14 to 28 s).
- **The first `jj status` in a new workspace is 7 to 13 s at 10,000 files** (it scans the tree once to settle its state). The
  session's first command pays it.
- **Disk**: a workspace is a full copy of the tracked files, 5 MB at 1,000 and 42 MB at 10,000 files of 1 KB (4 KB clusters), with no
  sharing between workspaces. The repository's store is shared and small (about 1 MB for either repository, since the files are
  identical text): the cost is the files, as a git worktree's is. A copy-on-write clone (row 1g) is not available on NTFS.
- **The first import of a folder is not jj's cost alone.** Taking an existing folder into jj (`jj commit` after `jj git init`) took
  13.7 s at 1,000 and 181 s at 10,000 files; `git add` and `git commit` of the same 10,000 files took 171 s here. It is paid once, by
  whoever makes the repository, never by a session.

Three workspaces added at once from one repository (1,000 files) all succeeded in 6.1 s, and three sessions describing (snapshotting)
at once all succeeded in 1.7 s. jj printed *Concurrent modification detected, resolving automatically* and recorded a
`reconcile divergent operations` entry in the operation log; no command failed and the three changes were all in the log.

## 3. How the changes come back

Run against a repository with a base commit, two sessions' workspaces (`w1`, `w2`), the person's working copy holding an uncommitted
edit and a new file, and then a commit of the person's own that changes the same line as session 1.

| Step | Command | Result |
|---|---|---|
| A session works | edits in its workspace; `jj describe -m <text>` | the edits are snapshotted into the workspace's change (1.8 s; this repository held about 200 files). `jj new` leaves an empty change to work on. |
| The person moves on | the person's `jj commit` in the place | the place's `@-` is now the person's commit; the sessions' changes still sit on the base. |
| The return | `jj rebase -s <session change> -d '@-'` run in the place | *Rebased 2 commits to destination* in 5.7 s (about 200 files, a loaded machine); session 1's change touched the line the person changed, and jj said **New conflicts appeared in 1 commits** and kept the conflict **in the change**: the rebase succeeded. Session 2's change rebased clean in 0.9 s. |
| Find the conflicts | `jj resolve --list -r <change>` | `shared.txt    2-sided conflict`; the log marks the change `(conflict)`. |
| The person's files | `ls`, `cat`, `jj status` in the place | **untouched**: the person's working copy was the empty change on their own commit, the files as they left them. The returned changes are siblings of the person's `@`, not applied to it. |
| Review's diff | `jj diff -r <change> --summary` (1.0 s) and `--git` | `M d0000/f1.txt`, `A s1.txt`, `M shared.txt` and a unified diff. For the conflicted file the diff **shows jj's conflict markers as the file's content** (`<<<<<<< conflict 1 of 1`, `%%%%%%%`, `+++++++`), not git's. |
| Make it findable | `jj bookmark create session-1 -r <change>` | the change has a name a jj reader sees. Whether a git reader of a colocated repository sees it was not measured. |
| Forget a workspace | `jj workspace forget <name>`, then remove the folder | the non-empty changes **remain** in the repository as commits; the empty, undescribed working-copy changes were abandoned. |
| A folder removed without forgetting | `rm -rf` of a workspace, then `jj workspace list` | the workspace is **still listed**: only `forget` ends it. |
| Two sessions' changes against each other | `jj new <change A> <change B>` on two changes that wrote the same new file | a merge change with **`clash.txt    2-sided conflict`**; the clash is the person's, found when they combine the changes, never at the return. |
| Resolving | `jj new <conflicted change>`, edit the markers, `jj squash` (the person's way) | jj's own flow, in jj. |

What this settles against §4.5:

- **The return as written works**: rebase the session's change onto the parent of the place's working-copy commit, as a change of its
  own, with the conflict kept in it, and nothing in the person's files changed. "Daoris never squashes into the person's working
  copy" holds by construction, since a rebase of a sibling does not touch `@`.
- **It is not read-only in the place.** jj snapshots the working copy on every command that does not say `--ignore-working-copy`
  (documented in `jj --help`), so a rebase run in the place turns the person's uncommitted edits into jj's working-copy commit and
  writes an operation to the place's operation log. The files stay; the repository's history moves. The return should say
  `--ignore-working-copy` where it only needs the change graph (the rebase, `resolve --list`, `diff -r`), which is exactly when the
  person's files are not its business. (`--ignore-working-copy` is in the help; the run above did not use it.)
- **Two ends, not one.** The node needs the conflict state read back (`resolve --list -r`), and the end of a copy needs
  `workspace forget` by the name the start recorded; a run cut off after the folder went and before the forget leaves a listed
  workspace, so the sweep (§4.7) must forget by name, not infer from the folder.
- **In a workspace that is not colocated, `git` does not exist**: a session's own git commands fail there, and so would any git-based
  door of Daoris's pointed at the copy (not run here). The session's brief must give jj words (`jj status`, `jj diff`,
  `jj describe`), and the git-based doors must not be asked about a jj copy.

## 4. What this says about the row

1. **Buildable, with the cost known.** `JjIsolation` is one pinned download (§1), `workspace add` and `forget`, and the §4.5 return;
   every step ran. Its price is a checkout: about three times a git worktree's, and minutes where a place has tens of thousands of
   files.
2. **The kind reaches fewer repositories than §4.3's table suggests.** Since a 0.46 `jj git init` colocates, most jj repositories made
   now have a git working tree and read `git` (D254 point 4); a workspace there is a git worktree and the git return applies. The `jj`
   kind (`.jj` and no git working tree) is the repository made with `--no-colocate`, or by an older jj. The proof should say which of
   the two its jj repository is.
3. **The `copy` bound applies in kind.** A workspace of 20,000 files (the design's proposed `copyBound`) costs about 1.5 to 3 minutes
   here, so a bound on the count of tracked files, read at the start, is the same decision for `workspace` as for `copy`: over it the
   session waits and works in place. The design lists no such bound for `workspace` (§4.4).
4. **What the proof needs.** A process test that finds `jj` (the pinned zip, or `PATH`) and says itself skipped where it does not,
   as the row says; it can use a 50-file repository, since the figures above are the measurement and not the test: the conflict kept
   in the change (§3's third row) and a workspace forgotten after its folder is gone.

Raw figures were taken from the scratch run's output on 2026-11-11 (a gitignored folder under the repository's `_fixtures/`, removed
after this record was written).
