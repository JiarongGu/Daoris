# Survey: parallel work beyond git (PLACEWORK1a, D254)

How agent products and other multi-writer systems let several workers change one place in parallel, and bring the work back,
with and without git. Read on 2026-11-10 from each product's public documentation; the sources are inline, and what could not be
verified is listed at the end. This is the input to PLACEWORK1b's design: a session works in its department's own place by default,
one writer at a time, and isolation for parallel work is a workflow's step (D254).

## What the products do

No agent product isolates non-git work. Each isolates with a git worktree, a cloud machine or container with a branch, or not at all.

- **Claude Code**: `--worktree` and a subagent's `isolation: worktree` make `.claude/worktrees/<name>` on a branch of their own;
  inside one, edits aimed at the main checkout are refused, though the checks do not see what a shell command writes. Outside git
  its sessions "write to the working directory directly and aren't isolated from each other", and inside git `worktree.bgIsolation:
  "none"` turns isolation off. Its `WorktreeCreate`/`WorktreeRemove` hooks are the one designed seam for another mechanism: the hook
  prints the folder to use, with an SVN example (https://code.claude.com/docs/en/worktrees, https://code.claude.com/docs/en/agent-view).
  Agent teams share a task list under a file lock; for files the guidance is that each teammate owns a different set
  (https://code.claude.com/docs/en/agent-teams).
- **Codex**: Local, Worktree (a detached-head worktree; ignored files carried by `.worktreeinclude`) or Cloud, whose diff is applied
  locally (https://developers.openai.com/codex/app/worktrees).
- **Cursor, Windsurf, Conductor, Crystal**: a worktree per local agent, reviewed then applied or discarded; Windsurf at most 20 per
  workspace (https://docs.windsurf.com/windsurf/cascade/worktrees.md, https://www.conductor.build/docs/concepts/git-worktrees).
- **Copilot coding agent, Devin, Jules, cloud agents**: a machine per task, returned as a branch and pull request; Devin's coordinator
  merges its workers into one (https://docs.github.com/copilot/concepts/coding-agent/about-copilot-coding-agent,
  https://docs.devin.ai/product-guides/advanced-mode).
- **Container with a branch** (Dagger's container-use): reviewed with ordinary git (https://dagger.io/blog/agent-container-use).
- **No isolation**: Aider edits in place and commits; OpenHands mounts the folder read-write
  (https://docs.openhands.dev/openhands/usage/sandboxes/docker).
- **GitButler**: several sessions in one working folder, each session's edits attributed by hooks to a virtual branch of its own
  (https://docs.gitbutler.com/features/ai-integration/claude-code-hooks); one file edited by two sessions is undocumented.

## The approaches

| Approach | What isolates | How work comes back | Conflicts | Without git | Windows | Cost on a large folder |
|---|---|---|---|---|---|---|
| Git worktree | a checkout and a branch | merge, pull request or diff | three-way at the merge | no (Claude Code's hook seam aside) | yes | the tracked files and their set-up |
| Cloud machine per task | a remote machine | pull request or patch | at the merge | no | n/a | remote time |
| In place | nothing | already there | last writer wins, silently | yes | yes | none |
| Virtual branches (GitButler) | hunks per session | a commit per branch | one file: undocumented | no | yes | none |
| jj workspaces | a working copy each | rebase; conflicts kept in commits (http://docs.jj-vcs.dev/latest/conflicts/) | resolved later | no | yes | a checkout |
| Copy-on-write clone (Dev Drive's ReFS, APFS, btrfs/XFS reflink) | a cheap copy | diff against the fork's snapshot, applied | a three-way merge to build | **yes** | Dev Drive only (Windows 11 24H2, ReFS) | near none on disk; time grows with file count |
| Overlay (OverlayFS; Windows ProjFS is a provider API only) | an upper layer | the upper layer is the diff | to build | yes | **no** cheap overlay | low on Linux |
| Exclusive checkout (Perforce, Plastic, Git LFS locks, SharePoint) | a lock | check-in | prevented; a stale lock blocks | yes | yes | none; friction |
| Path claims with a lease (mcp_agent_mail) | declared globs, exclusive or shared, with a time to live | in place | a guard before writing | yes | yes | none |
| Optimistic check on a content hash | nothing | a write refused where its base moved | re-read and retry | yes | yes | none |
| CRDT, or last writer per property (Automerge, Yjs, Figma) | replicas | merged by itself | none by construction | yes | yes | needs its own format; none for existing files |
| Plan and apply (Terraform, change sets) | a reviewed plan | one apply | a stale plan refused | yes | yes | one apply pass |

Sources: https://learn.microsoft.com/windows/dev-drive and
https://devblogs.microsoft.com/engineering-at-microsoft/copy-on-write-performance-and-debugging/ (copy-on-write on by default for Dev
Drive in 24H2); https://kernel.org/doc/html/v5.11/filesystems/overlayfs.html;
https://learn.microsoft.com/en-us/windows/win32/projfs/projfs-programming-guide;
https://perforce.com/manuals/p4guide/Content/P4Guide/resolve.lock.exclusive.html; https://github.com/git-lfs/git-lfs/wiki/File-Locking;
https://github.com/Dicklesworthstone/mcp_agent_mail; https://automerge.org/blog/automerge-2/;
https://madebyevan.com/figma/how-figmas-multiplayer-technology-works/; https://docs.aws.amazon.com/AWSCloudFormation/latest/APIReference/API_CreateChangeSet.html.

**Files that are not text** are merged by no general tool: practice everywhere is a lock, or one side wins. Format-aware tools exist
for some (a spreadsheet merged by cell), and folder comparers merge text and structure only.

## Candidate designs, ranked for D254

1. **A writer's lease by default, isolation providers by kind of place, the return merged against a snapshot.** One writer per place,
   leased and renewed while the session runs, released at its end; the next waits. Each run records a manifest of the place at its
   start (path, hash, size, time), which both finds what the person or another tool changed meanwhile and is the return's base.
   Isolation, where a workflow asks for it, goes through one provider interface shaped like Claude Code's hooks (make a folder, remove
   it): a worktree for git, a jj workspace for jj, a copy-on-write clone where the volume allows it, else a full copy. The return,
   under the lease, merges file by file against the manifest: one side changed is applied, text changed on both sides is merged
   three ways, anything else changed on both is the person's with both versions kept, and every write is conditioned on the live
   file's hash. *Risk:* a full copy on NTFS, where most Windows places live, so a size bound that waits instead of copying; a
   conflict in a file that is not text always reaches the person.
2. **Path claims, no copies** (a tier that can sit inside 1): each session declares the paths it will write, exclusive or shared, with a
   time to live; a hook before each edit refuses one outside its claims, overlapping exclusive claims wait, and hash checks catch what
   slips. *Risk:* shells, builds, formatters and generated files write outside the claims, and an agent predicts its paths poorly.
3. **A git repository behind every folder**, kept outside it, then git everywhere. *Risk:* large files swell it, documents still do not
   merge, and a hidden repository under the person's documents confuses sync clients and the person.
4. **A sandbox with an overlay**, whose upper layer is the diff. *Risk:* no overlay exists on Windows without writing a ProjFS
   provider, and a machine per session is heavy.

**Places reached through a console** (cloud configuration, tickets) take plan, review and apply in every design: the session produces a
change set, never changes the system itself, and one applier runs it under the system's own precondition (a state serial, an etag, an
`If-Match`), a stale plan planned again. Many consoles have no plan and no etag, so each needs an adapter, and some can only be served
by the lease and a careful read-back.

## Not verified

Cursor's, Jules's, Amp's and Docker Sandboxes' details rest on secondary sources; Codex cloud's apply syntax differs between guides;
whether Copilot's agent runs on Windows runners (GitHub's pages disagree); the Windows container layer filter; that plain NTFS has no
block cloning; Notion's model; GitButler's behaviour for one file edited by two sessions.
