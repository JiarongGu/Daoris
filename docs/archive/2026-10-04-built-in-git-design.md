# Daoris's own git, built in — branches, history and blame, organised by where work is

> GIT1, decided as D147 (2026-10-04). GIT1a–c are built (the branch list, the reads behind a page, the terminal's
> read door); their notes are under D147. The owner: *"since Daoris will need to use git anyway we will
> need a self-managed git, also since we will have self-managed git then we probably need to do a built-in git more
> like VS Code with GitLens"*. `Dnn` is `docs/decisions/Dnn.md`. LAND2 is D145, being designed beside this: it lands
> done work onto `feature/` branches and opens their pull requests through a plugin. This design places those branches
> and that press; what the plugin is told and how it answers are LAND2's.

## 1. What is there, and what was measured

**One git, already.** Every git call Daoris makes starts through `WorkingTree.GitStart`, which runs the file Tools
resolves (D121, TOOLS5): the system's git from `PATH` unless the person chose managed or a file. Managed Git can be
downloaded and used (TOOLS4, TOOLS7), and nothing switches on its own, so an install runs the system's git.

**Slices of git, each on its own screen**: the review (a session's range, or its landed branch, D113), the preview (a
file in a tree or on a branch, D111), a repository's page (its line, how many branches are unlanded), the clean-up and
*Bring up to date* (Daoris's branches, D88, D102, D109, D112), and the trace (the session behind a commit, D143).
Nothing shows a repository's branches together, its history, a commit, or a file's history.

**What costs time is starting git.** Measured on this repository on the development machine, Git 2.53, 2026-10-04:

| Read | Time |
|---|---|
| Twenty starts of `git rev-parse HEAD`, from a shell | 2.0 s, about 100 ms each |
| One `git diff -M --numstat --patch` over a range of 1,179 files | 2.2 s |
| One `git diff -M <range> -- <path>` per file, for 30 of those files | 6.5 s, about 215 ms each |
| `git log` of 500 commits across every ref, with parents | 0.2 s |
| `git blame --porcelain` of a 630-line file | 0.14 s |
| `git log --follow` of one file, over 1,757 commits | 0.75 s |
| `git for-each-ref` over 51 refs, with each one's ahead and behind the line and the tree holding it | 0.45 s |

A file at a time, the 1,179 files would take about four minutes. That is REVIEW3's lesson (a 71-file review took
about 55 s), and this design is built on it: **one git process per view.**

## 2. What it shows

### 2.1 Organised by where work is (D55)

Sessions stays organised by the session. Here the organising object is the **branch**: where a session's work is,
where it landed, what the line holds. Then the **commit**, which is what was done. A file is reached through a commit,
a review or a preview, and never browsed: there is no file tree and no editor.

### 2.2 The list: lines and branches, by repository

Each repository is a group. Its head says the line (D86, with what set it), how it stands to origin's copy of it, and
when the repository was last fetched: git's own `FETCH_HEAD` time, or *never fetched here*, never a guess. Under it,
in this order:

- **Sessions'**: a `daoris/…` branch, named by its session's title and state (the rail's rule, by the tree that holds
  it), with how far it is ahead of and behind its line.
- **Landed**: a branch `landings.json` records (D102). Whether it is pushed: its own name on origin is in step, ahead,
  behind, or gone from origin. Its pull request: the link the landing kept, and its state where a plugin answered,
  with when.
- **Yours**: every other local branch, folded after five.
- **Origin's**, with no local branch: shown from the list's ⋯.

The kind is read from facts: the `daoris/` namespace (D88), the landing record, the line. A branch the record no longer
matches (D113's *not-ours*) reads as yours. **Which repositories** follows D112: those holding a branch of Daoris's
first, then every other with a checkout, listed apart and folded, each included by the person.

### 2.3 A branch's page

The header names the branch and its kind, says in one line where it stands (against its line, against origin, its
pull request) and carries its acts (§3). Beneath is its **history as a graph**: the commits on it since it left its
line, beside the line's since then, in lanes, 200 to a page. *All branches* draws the repository's graph across its
local branches and origin's. The line's own page is the repository's history.

The lanes are drawn by the page from each commit's parents (a pure helper). They are never read from
`git log --graph`'s drawing, which is parsing another program's screen (the working surface design §3).

### 2.4 A commit

Its message, its author and committer with their times, its parents as doors, and the branches that hold it. Then
**who made it**: the session and quest the trace finds for that commit (D143), or *not kept* where nothing keeps it.
Then its changes against its first parent, drawn as the review draws them, with `DiffFileRow` and `PatchView` taken
as they are (molecules, which import no hook), the review's bound and the bound's sentence. A merge commit says that
its changes are read against its first parent. Each file's row offers *History* and *Blame*.

### 2.5 A file's history and its blame

GitLens's core, on a page for one path at one commit:

- **History**: the commits that changed the path, following renames, 100 to a page, each a door to its commit.
- **Blame**: the file as that commit holds it, each line with its commit, author and age in a gutter, and a run of
  lines from one commit grouped. A line's commit opens its page. *Before this change* blames the commit's parent, which
  is how a person walks back past a reformat. D111's bound and binary test hold: a binary file has a history and no
  blame, and a long file says where the blame stopped.

The ways in are a commit's file row, a review's file row, and the session's preview (D111), which gains *History and
blame* for its file. A path can also be typed, completed from the commit's tree. That is a field, not a tree to browse.

### 2.6 Compare two refs

From a branch's or a commit's *Compare with…*. Both refs are resolved to commits once and the page names both. It
shows the commits only on each side, and what the right side changed since the two parted (`A...B`); *Everything
that differs* shows `A..B`. The same molecules draw it.

### 2.7 From a session

The review stays the session's: its range, its acts and its landed note (D113). Its head gains one line: the
session's branch against its line, and where its work landed, pushed or not, with its pull request. Each is a door into
Git with that branch chosen. The session's page header gains *Show in Git*. Nothing of Git goes to the side bar
(D118 §3c).

### 2.8 What is not built, and why

- **An editor, or a file tree.** D55: the person sets targets and verifies outcomes, and the frame holds no editor.
- **A merge-conflict editor.** A conflict is resolved where the work is: by a session in its tree, or by the person in
  their own tools. D109's replay aborts a conflict and names its files.
- **Staging hunks, or composing a commit.** The agents commit. The person's own commits are made where they edit.
- **Checking out, stash, reset, an interactive rebase, a cherry-pick, a revert.** Each writes a working tree, and here
  that is a session's, which is busy (D51), or the person's, which is theirs (D113 §5).
- **Tags, deleting a branch on origin, a force push.** The first two are outward acts nobody asked for. A force push
  rewrites what others may have.
- **Line comments and review threads.** Daoris is not a code-review product (the working surface design §5).
- **Fetching on a timer.** Daoris reaches the network as the person only on their press (D109).

## 3. What it does

### 3.1 The acts

**No act reads or writes a working tree or an index.** Each writes a ref or talks to origin, so a dirty checkout
refuses none of them. The one door that touches the checkout, moving the line forward, stays D109's, with its own
refusals. Plan and apply are separate: a press shows what will move and the command that will move it, and is judged
again right before it acts, at the commit the person was shown.

| Act | What runs | What refuses it |
|---|---|---|
| **Fetch** | `git fetch origin --prune`, for one repository or each the list shows, as the person: their credentials and helper, `GIT_TERMINAL_PROMPT=0`, two minutes (D109). It moves only origin's refs | No `origin`. A fetch that fails is named in git's words. `--prune` is why a branch the platform deleted after its pull request merged reads as gone from origin, not as still pushed |
| **Create a branch** | `git branch <name> <commit>`: a ref, and nothing checked out | A name that exists, which is never moved (D87). A name git refuses (D86's rule). A name under `daoris/`, the namespace Daoris's proofs read as its own sessions' (D88) |
| **Push** | `git push origin <commit>:refs/heads/<name>`: exactly the commit shown, and the person's pre-push hooks run | The line, which takes work through its platform's review (D87). A session branch: its work is not accepted until it lands, D109's replay never moves a branch on its remote, and the clean-up removes only local branches, so the copy would stay on the team's remote after the session's tree and branch are gone. A branch a running or waiting session's tree holds. A branch tracking another name or remote. Anything that is not a fast-forward of origin's copy: never a force |
| **Open its pull request** | The hand-off (D102, `trees hand`) to the repository's landing plugin, which pushes and opens it (D100) | D100's refusals of the plugin. A branch no landing recorded, until LAND2 says otherwise |
| **Delete a branch** | Locally, `update-ref -d` at the commit judged | The line. A branch any working tree has checked out (the list's `worktreepath`): `git branch -d` would refuse it, and `update-ref` would not. A session branch: the tree's *Discard* or the clean-up does that, and the sentence names it. A landed branch goes by the clean-up's removal, which marks its record (D113 §4). A branch whose commits no other branch and nothing on origin holds is refused, naming them; a second press, meaning it, deletes it and says the commit that brings it back while git keeps it (D51 rule 7) |

**Checking out is not offered.** The checkout is the person's, and their editor may have its files open. A session's
tree is busy. Nothing on this page needs a checkout: history, blame, a file and a compare all read git's objects, as
D113 reads a landed branch. Where the person wants the branch on disk, the page offers `git switch <name>` to copy, for the
person's own terminal or the console's (D96).

### 3.2 A push is the person's say-so

D37 keeps a push the person's. A press of *Push* is the person's call, made where they can see the commits it sends,
for one branch at one commit. What D87 rejected was a built-in push and pull request *for one host*: a durable
authorisation written into core, and a platform made the default. Neither applies. A push to `origin` is every
platform's, nothing pushes without a press, and no landing rule can name Daoris's push. Opening the pull request stays
a plugin's, because that is the platform's own API (D100). Git's output, hooks included, follows in the console under
`git:<repository>`, as a tool's download does (TOOLS7). Pushing a landed branch records it as pushed by the person,
with the commit, in its landing entry, so D109's replay keeps it from then on.

### 3.3 Both doors (D50)

`daoris-driver git branches [--repository <name>] [--all]` prints the list. `fetch`, `branch`, `push` and `delete`
print their plan and act with `--yes`; `delete --anyway` is the second press. Exit 1 on a refusal. One judge in the
driver library serves both doors, so the refusals cannot differ.

A read's terminal door is git itself. Each view says the git command that answers it, ready to copy, and the person
runs it in the checkout. Ask Daoris (D110): the reads are exempt, as `trace` is, and `fetch`, `branch`, `push` and
`delete` are owed to a `git` kind, whose push is a card the person presses.

### 3.4 What it keeps (D143)

The reads keep nothing. Each is derived from git and the records when it is asked, and says which git answered, its
way and version, as D24 says which tier answered. The acts keep their facts:
- a push of a landed branch: in its landing entry, with who, the commit and when;
- a deleted landed branch: its trace marked as removed by the person;
- a pull request's state, where a plugin answered: beside the landing's entry, with when and which plugin, in the field
  LAND2 names;
- each act: one machine log line by its code, naming no branch and no path (D94).

The time of the last fetch is git's own.

## 4. How it stays fast

### 4.1 One git process per view

| View | The call |
|---|---|
| The list | Per repository, one `for-each-ref` over `refs/heads` and `refs/remotes/origin` whose format names each ref's commit, time, subject, the tree holding it (`%(worktreepath)`) and its distance from the line (`%(ahead-behind:<line>)`). Then one `rev-list --left-right --count` for each branch whose copy on origin differs |
| A graph page | One `log --parents --date-order -n 200` over the tips shown |
| A commit | REVIEW3's one-call reader, over `<parent>..<commit>` |
| A file's history | One `log --follow -n 100 <commit> -- <path>` |
| A blame | One `blame --porcelain <commit> -- <path>` |
| A compare | One `log --left-right A...B`, then the one-call reader: two |

Every call asks for a machine format (NUL-separated fields, `-z`, `--porcelain`), never a human one, and each format has
a pure parser with its own tests. **A git without an atom is still answered**: without `ahead-behind`, one
`rev-list --count` per branch, said in the list's head as *this Git counts each branch on its own*. Both atoms answered
on Git 2.53 here; the floor each needs is GIT1a's to read and record.

### 4.2 Cached by commit id

An answer named only by full commit ids cannot change: a commit's changes, a blame or a history from a commit, a
compare of two commits, a graph page from a set of tips. The host keeps those in memory, least recently used first out,
bounded by bytes, keyed by the repository's git directory, the ids, the path and the options. **A ref is never a key.**
It is resolved first, by the list or one `rev-parse`, and the answer is cached under the commit it named. The page's
queries carry the ids and never go stale.

Nothing is cached on disk: git's object store already is that cache, and a copy would be a second record. **Daoris
writes nothing into a repository's `.git` to make it faster**, no `commit-graph` and no `maintenance`: the person's own
git configuration decides. The list is read again when the view opens, after an act, on *Refresh*, and for a repository
where a tick's news says a session moved.

### 4.3 The git binary, not a library

LibGit2Sharp (libgit2, in process) was weighed, and the git binary Tools resolves is chosen:

- **The start it saves is not the cost.** One start a view is about 100 ms. The measured cost was a start per file.
- **Credentials and SSH are git's.** The credential helper and `core.sshCommand` (D121's first key, after WSR7) are what
  reach the owner's platform. LibGit2Sharp asks for its own credential callback, and its official native binaries carry
  no SSH transport; only third-party packages add one. Fetch and push would stay on git, and Daoris would keep two
  implementations of one repository.
- **Hooks.** libgit2 runs none (its issue 964), so a push would skip the person's pre-push hook without a word.
- **Repositories git opens and libgit2 does not**: a partial clone (libgit2 issue 5564), and the reftable format. The
  page would go blank on a repository the agents work in.
- **Worktrees.** libgit2 has a worktree API. Whether it answers as git does for every tree Daoris reads (a detached
  session tree, `worktreepath`) is not measured, and Daoris's trees are linked worktrees (D51).
- **No surprise.** The page reads the git the agents run, resolved one way for both (D121 §3), so its answer is the
  answer they get.
- **Size.** libgit2's native library would join the install for each platform. Git is the system's, or the managed one
  Tools already downloads.

The library facts are from libgit2's and LibGit2Sharp's own trackers and packages, read 2026-10-04, not measured. A
library is reconsidered only if a view is measured that cannot be one call.

## 5. Managed Git: offered, never switched

D121 rejected managed by default, and that stands. Switching changes the git every session runs, and a minimal git
ships its maker's system configuration, not the one a Git for Windows installer wrote. The keys TOOLS7 compares before a
switch (`core.autocrlf`, `core.eol`, `core.symlinks`, `core.longpaths`) decide how a checkout reads, and line endings that
differ would show every file changed; TOOLS10 adds `credential.helper`. So the switch is offered where the person will
meet it, and only their press makes it:

- **The setup guide (D97) gains a first step, *Git*.** It is done when a git answers at or above D121's floors (2.29
  to fetch, 2.32 to carry a setting), and it says which git and its version. It is to do when none answers. Its doors
  are Settings → Tools at Git, which asks before it switches (TOOLS7), and `daoris tool use git managed <version>`.
  With no git at all, the step also says that MinGit carries no bash (the tools evidence), so an agent that wants Git
  Bash still needs Git for Windows.
- **The Git place names the git that answered** in its head (*Git 2.53.0 · the system's*), and its ⋯ offers *Use
  Daoris's own Git…*, which opens Settings → Tools at Git. Where that git lacks an atom §4.1 uses, or is below a floor,
  the head says so and the offer leads.
- **Never a different git for the page than for the agents** (D121 §3).
- **The offer to leave a system git that works waits on TOOLS6** (the floors) **and TOOLS10** (the probe before a
  managed git meets an agent).

## 6. Where it lives in the frame

- **The activity bar gains a place, *Git*, after Projects.** Git is a proper noun in the glossary, within the nav
  budget in both languages; D116's check settles the name, and *history*, *graph*, *blame* and *compare* join the
  glossary. It is shell-only, as Plugins is: a repository's commits and code are machine-local (D47 §4, D55), so a
  browser draws no such place.
- **The list pane** is §2.2. Its `＋` is *Create a branch…*, a drawer holding the name and the commit it starts at
  (D118 §3d). Its ⋯: every repository, origin's branches, *Fetch all*, *Use Daoris's own Git…*. Closed, it is a strip
  of its controls.
- **The main area** holds the branch, commit, file and compare pages, each a record (D118 §3d), laid out by its own
  width: a commit opens beside the graph when both fit above the main area's floor, and in its place, with a way back,
  when they do not.
- **The side bar and the panel do not change** (D118 §3c). A fetch's or a push's output is a console stream under
  `git:<repository>`.
- **The review and Git each own their verbs** (D56). The review acts on a session's work (accept, send back, discard),
  and Git on refs (fetch, branch, push, delete). A commit's *who made it* is the trace's chain for it, the section
  TRACE1b puts on a session's and a quest's page. LAND2's branches are the *Landed* group, and its press is the
  hand-off. Projects keeps its page and gains a door to Git with that repository chosen.

## 7. Considered and rejected

- **LibGit2Sharp in process** (§4.3).
- **Git inside Projects, as a part of a repository's page.** Projects' list is repositories, and Git's is branches across
  them. A graph beside a commit's changes needs the main area whole, and a repository page with history in it would be
  two pages in one.
- **The side bar**: it holds only what is the same on every view (D118 §3c).
- **Checking out in the person's checkout** (§3.1). **A tree per branch to check one out**: a branch checked out in a
  tree is one D109's replay keeps, and the person's terminal already switches a branch. It waits until someone asks.
- **Keeping "Daoris never pushes" by moving `git push` into a plugin Daoris ships.** The authority is the same press,
  behind a process and a handshake.
- **Pushing the line.** It takes work through its platform's review (D87). A person who pushes their own line does it
  in their own tools, and that refusal is the one to revisit if they ask.
- **`push -u`.** It writes the branch's configuration in the checkout. *Pushed* is read against origin's copy of the
  same name, which the push itself updates (GIT1g measures that it does).
- **Fetching on a timer**, **reading `git log --graph`'s drawing**, **a cache on disk**, and **writing a
  `commit-graph`** (§2.8, §2.3, §4.2).

## 8. The build

GIT1a reads the list in the driver and GIT1b the rest of the reads, with the cache. GIT1c is the terminal's read door
and GIT1d the routes. GIT1e and GIT1f are the place and its pages. GIT1g and GIT1h are the acts, push last. GIT1i
reaches it from a session. GIT1j is the managed Git offer, after TOOLS6 and TOOLS10, and GIT1k is Ask Daoris's kind.
Each row names its section here and its proof.
