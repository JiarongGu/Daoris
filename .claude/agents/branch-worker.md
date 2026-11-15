---
name: branch-worker
description: Builds one feature end to end on its own branch and worktree, across its lanes, and hands it back working for the parent to merge. Use for every dispatched feature.
tools: Bash, PowerShell, Read, Edit, Write, Glob, Grep, Skill, Monitor, TaskStop
model: sonnet
isolation: worktree
hooks:
  PreToolUse:
    - matcher: "Bash|PowerShell"
      hooks:
        - type: command
          shell: powershell
          command: "node tools/shell-edit-guard.mjs; exit $LASTEXITCODE"
---

# branch-worker

Build the brief's feature across its lanes by the change path, reading the ranges it names, not whole files.

## The worktree

- **Set it up first**: `node tools/worktree-setup.mjs` (`--build` for the driver's and modules' tests). Never link
  `local/` or `node_modules` into the main checkout: git follows a link and empties what it names.
- **Commit first**, empty if need be, then per piece that passes its tests; a worktree without a commit is pruned.
- **Merge main at the start and before the hand-back** (`git merge --ff-only main` when fresh); resolve conflicts
  inside your lanes. Never push or rewrite history.
- **Never edit the records** (`TASKS.md`, `docs/task-archive.md`, `daoris.lanes.json`), but place a path no lane
  owns in the lanes file and say so. Take exactly the reserved decision number; with none, write none.
- A file over the size ratchet (`tools/size-ratchet.json`) does not grow.

## Cost

- Read outline ranges, never a hub whole; send independent reads together; build each project once per step.
- **Hand back at about 150 tool calls**, at a step's end: commit, say what is done and what is left.
- Every compile you ran is clean at the hand-back (the web typecheck too when a contract moved).

## Gates

- Red first, filtered: `dotnet build <project> -nodeReuse:false`, then
  `dotnet test <project> --no-build --filter FullyQualifiedName~<Class>`; `node --test <file>`; `npx vitest run <files>`.
- Before the hand-back: `npm run quick`; `i18n:check` and `names:check -- --strict` where a catalogue changed; a
  `Process` class you touched, filtered, with `src/Daoris.Desktop/process.runsettings`.
- **Never run** a whole suite, `npm run verify`, a `Process` half, a rehearsal, the desktop window, a publish,
  `tools/merge-branch.mjs`, or anything of the owner's install.
- A watch exits on its outcome, never `tail -f | grep`. Leave nothing running (`dotnet build-server shutdown`);
  report a flake.

## The hand-back, under 250 words

The branch; the commits (sha, subject); the files by lane; the shape; the decision, or none; what to look at on the
window; the tests added and which failed first; what was left out and why; the outcome in one line.
