# `github-pull-request` — a plugin that lands work on GitHub

A plugin is a folder with a `plugin.json` (`docs/2026-09-23-plugin-design.md`, D64). This one speaks
at one point, `work/land` (D100): once a workspace's **branch** landing rule has put a session's
accepted work on a new branch, the landing hands that branch to this plugin, which pushes it and opens
the pull request. **Daoris itself never pushes and never opens a pull request** (D87) — this process
does, signed in as your `gh` is.

## Nothing runs until you ask for it

Tracked here as an example, it does nothing on its own. It runs only when **both** are true:

1. you installed it: `daoris plugin add examples/plugins/github-pull-request` (or copied the folder
   under the Daoris home's `plugins/`), and it is not switched off;
2. a landing rule names it: `daoris driver landing --workspace <name> branch "feature/{quest}-{slug}"
   --plugin github-pull-request`, or Settings → Workspace → How work lands.

Installed and not named, it is never started: the driver loop keeps no process for a plugin that
speaks only at a landing. Named, it is started for one accepted landing, told the branch, and stopped.

## What it runs

In the repository's own checkout, after Daoris has made the branch:

```sh
git push --quiet -u origin <branch>
gh pr create --head <branch> --title <title> --body-file <file> --base <base>
```

The title is the quest's (or what you first said in a conversation). The body names the quest, the
session, who accepted the work (the person who reviewed it, or automatically when its quest was done,
as the frame's `acceptedBy` says) and the commits, and is written to the plugin's data folder
(`plugins/.data/github-pull-request/`) before `gh` reads it. `--base` is the line the work grew from;
with none, `gh` uses the repository's default branch. The pull request's address `gh` prints comes
back to the review and the terminal.

## When a chain's branch moves on

A chain lands on one branch (D145 §3, D149): a later step's work, such as its verify step's, moves the
branch the first landing made on, and Daoris tells this plugin the pull request it opened then, as the
frame's `pullRequest`. Then it opens no second one. It reads that pull request's state and pushes only
while it is open:

```sh
gh pr view <pullRequest> --json state
git push --quiet origin <branch>
```

A pull request that is merged or closed, or a state `gh` cannot read, answers *not pushed*, saying which:
commits pushed to it now would ride no pull request. The branch stands, and a new pull request for the
rest is yours to open.

## What it needs

- **The tools its `plugin.json` declares under `tools`**, each at the version it names or newer, with GitHub
  CLI signed in: `gh auth login`. `daoris-driver plugins try github-pull-request` checks them on this machine.
- **`origin`** pointing at the GitHub repository, with push access for whoever `git` pushes as.

## Why each version

Each floor in `tools` is the oldest release that has everything this plugin runs with that tool: the
calls above, the check, and the version question Daoris asks.

- **GitHub CLI 1.9.0**: `gh pr view --json`, which reads a pull request's state at an advance, came in
  [1.9.0](https://github.com/cli/cli/releases/tag/v1.9.0). The rest is older: `gh pr create`'s body from a
  file in [1.7.0](https://github.com/cli/cli/releases/tag/v1.7.0) ([#3018](https://github.com/cli/cli/pull/3018),
  `--body-file`), its `--head` in [1.0.0](https://github.com/cli/cli/releases/tag/v1.0.0), `gh auth status`
  in [0.12.0](https://github.com/cli/cli/releases/tag/v0.12.0), and `--title`, `--base` and `--version` are
  in 0.12.0's source. Known from the release notes, and from that source for the last three.
- **Git 1.7.0**: `git push`'s `-u` and `--quiet` are both in 1.7.0's options
  ([`builtin-push.c` at v1.7.0](https://github.com/git/git/blob/v1.7.0/builtin-push.c)), and its
  [release notes](https://github.com/git/git/blob/master/Documentation/RelNotes/1.7.0.adoc) name
  `--set-upstream`. A reading of the source: no Git that old was run.
- **Node.js 16.6.0**: `land.mjs` takes a command's last line with `Array.prototype.at`, which came with V8
  9.2 in [Node.js 16.6.0](https://nodejs.org/en/blog/release/v16.6.0). That everything else it uses is
  older is a reading of the script, not a run on 16.6.0.

## When something fails

The branch Daoris made **always stands**. A push that fails answers *not pushed* with git's reason; a
`gh` that fails after the push answers *pushed*, with `gh`'s reason and no pull request. Either way
the review and `daoris-driver trees land` say the plugin's step did not finish and how to push the
branch yourself, and once the cause is fixed the review's *hand it to* or `daoris-driver trees hand
<session>` gives the branch to the plugin again. On Windows, a `gh` that is a `.cmd` script is run through `cmd`, so a title's double
quotes become single ones and a percent sign is spelled out.

No remote, organisation or repository is named here: `gh` takes them from the checkout's `origin`.
`land.mjs` is the whole of it; `src/Daoris.Cli/test/landing-plugins.test.ts` drives it against a bare
repository and a fake `gh`, never a network. It has not been run against a real GitHub repository: the
arguments are `gh pr create`'s and `gh pr view`'s documented ones.
