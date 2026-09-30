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
session and the commits, and is written to the plugin's data folder (`plugins/.data/github-pull-request/`)
before `gh` reads it. `--base` is the line the work grew from; with none, `gh` uses the repository's
default branch. The pull request's address `gh` prints comes back to the review and the terminal.

## What it needs

- **node** on the PATH (it is a `node` script, as the other example plugins are).
- **git**, with `origin` pointing at the GitHub repository and push access for whoever `git` pushes as.
- **gh**, the GitHub CLI, installed and signed in: `gh auth login` (check with `gh auth status`).

## When something fails

The branch Daoris made **always stands**. A push that fails answers *not pushed* with git's reason; a
`gh` that fails after the push answers *pushed*, with `gh`'s reason and no pull request. Either way
the review and `daoris-driver trees land` say the plugin's step did not finish and how to push the
branch yourself. On Windows, a `gh` that is a `.cmd` script is run through `cmd`, so a title's double
quotes become single ones and a percent sign is spelled out.

No remote, organisation or repository is named here: `gh` takes them from the checkout's `origin`.
`land.mjs` is the whole of it; `src/Daoris.Cli/test/landing-plugins.test.ts` drives it against a bare
repository and a fake `gh`, never a network. It has not been run against a real GitHub repository: the
arguments are `gh pr create`'s documented ones.
