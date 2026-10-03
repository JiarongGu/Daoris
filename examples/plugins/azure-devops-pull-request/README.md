# `azure-devops-pull-request` — a plugin that lands work on Azure DevOps

A plugin is a folder with a `plugin.json` (`docs/2026-09-23-plugin-design.md`, D64). This one speaks
at two points. At `work/land` (D100): once a workspace's **branch** landing rule has put a session's
accepted work on a new branch, the landing hands that branch to this plugin, which pushes it and opens
the pull request in Azure Repos. At `work/state` (D148): where a look may remove a branch a landing
made, it says whether that branch's pull request completed, and with which commit. **Daoris itself
never pushes, never opens a pull request and never asks Azure DevOps** (D87) — this process does,
signed in as your `az` is.

## Nothing runs until you ask for it

Tracked here as an example, it does nothing on its own. It runs only when **both** are true:

1. you installed it: `daoris plugin add examples/plugins/azure-devops-pull-request` (or copied the
   folder under the Daoris home's `plugins/`), and it is not switched off;
2. a landing rule names it: `daoris driver landing --workspace <name> branch "feature/{quest}-{slug}"
   --plugin azure-devops-pull-request`, or Settings → Workspace → How work lands.

Installed and not named, it is never started: the driver loop keeps no process for a plugin that
speaks only at a landing and a query. Named, it is started for one accepted landing, told the branch,
and stopped; and a look that may remove a branch it pushed starts it once for that look's questions.

## What it runs

In the repository's own checkout, after Daoris has made the branch:

```sh
git push --quiet -u origin <branch>
az repos pr create --source-branch <branch> --title <title> --output json --target-branch <base> \
  --description <line> <line> …
```

The organization, project and repository are the ones `az` detects from the checkout's `origin`
(`--detect`, on by default), so nothing here names them. The title is the quest's (or what you first
said in a conversation); the description names the quest, the session, who accepted the work (the
person who reviewed it, or automatically when its quest was done, as the frame's `acceptedBy` says)
and the commits, one line per value, as `--description` takes them. `--target-branch` is the line the
work grew from; with none, Azure Repos uses the repository's default branch. The pull request's web
address is read from the JSON `az` prints and comes back to the review and the terminal.

## When a chain's branch moves on

A chain lands on one branch (D145 §3, D149): a later step's work, such as its verify step's, moves the
branch the first landing made on, and Daoris tells this plugin the pull request it opened then, as the
frame's `pullRequest`. Then it opens no second one. It reads that pull request's status, by the number
its address ends in, and pushes only while it is `active`:

```sh
az repos pr show --id <number> --output json
git push --quiet origin <branch>
```

A pull request that is completed or abandoned, or a status `az` cannot read, answers *not pushed*,
saying which: commits pushed to it now would ride no pull request. The branch stands, and a new pull
request for the rest is yours to open.

## When a look asks whether its pull request completed

A pull request completed by squash puts one new commit on the line, so git sees no ancestor and the
branches it carried look unmerged forever. Only Azure DevOps knows it completed, so where a look may
remove a branch a landing made (the landing's tidy, and the clean-up's list) and git cannot tell,
Daoris asks this plugin about it: at most 30 seconds a branch, and never on a timer.

```sh
az repos pr list --source-branch <branch> --status all --output json   # only where the record holds no pull request
az repos pr show --id <number> --output json
```

It finds the pull request by the number its address ends in, or, with none, among those from the
branch: into the line where there are any, a completed one, else an active one, else the newest
abandoned one. None found is *unknown*. It answers `active` as *open*, `completed` and `abandoned` as
themselves, and anything else as *unknown*. For a completed one it reads `lastMergeCommit` as the
merge commit, `lastMergeSourceCommit` as the commit it merged, `targetRefName` as the target,
`closedDate` as when, and `completionOptions.mergeStrategy` as how (`noFastForward` is *merge*,
`squash` and `rebase` keep their names, `rebaseMerge` is *rebase-merge*). A completed one with no
merge commit is answered *unknown*, saying so. An `az` that fails, not signed in or without the devops
extension, is the call's error with `az`'s last line.

**Daoris removes nothing on this word alone.** A branch goes only where git confirms the answer here:
the merge commit is on the line (local, or `origin/<line>`), and the branch stands at or under the
commit the pull request merged. A reading that is wrong keeps the branch.

## What it needs

- **node** on the PATH (it is a `node` script, as the other example plugins are).
- **git**, with `origin` pointing at the Azure Repos repository and push access for whoever `git`
  pushes as.
- **az**, the Azure CLI, signed in with `az login`, and its devops extension:
  `az extension add --name azure-devops`.

## When something fails

The branch Daoris made **always stands**. A push that fails answers *not pushed* with git's reason; an
`az` that fails after the push answers *pushed*, with `az`'s reason and no pull request. Either way the
review and `daoris-driver trees land` say the plugin's step did not finish and how to push the branch
yourself, and once the cause is fixed the review's *hand it to* or `daoris-driver trees hand <session>`
gives the branch to the plugin again. On Windows `az` is a `.cmd` script, so it is run through `cmd`: a title's or a line's double
quotes become single ones and a percent sign is spelled out, because cmd would read them.

`land.mjs` is the whole of it; `src/Daoris.Cli/test/landing-plugins.test.ts` drives it against a bare
repository and a fake `az`, never a network. It has not been run against a real Azure DevOps
organisation: the arguments are `az repos pr create`'s, `az repos pr list`'s and `az repos pr show`'s
documented ones, and whether `lastMergeCommit` names a squash's commit on the target was never
measured; git's confirmation is what keeps a wrong reading safe.
