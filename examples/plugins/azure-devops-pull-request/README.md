# `azure-devops-pull-request` — a plugin that lands work on Azure DevOps

A plugin is a folder with a `plugin.json` (`docs/2026-09-23-plugin-design.md`, D64). This one speaks
at one point, `work/land` (D100): once a workspace's **branch** landing rule has put a session's
accepted work on a new branch, the landing hands that branch to this plugin, which pushes it and opens
the pull request in Azure Repos. **Daoris itself never pushes and never opens a pull request** (D87) —
this process does, signed in as your `az` is.

## Nothing runs until you ask for it

Tracked here as an example, it does nothing on its own. It runs only when **both** are true:

1. you installed it: `daoris plugin add examples/plugins/azure-devops-pull-request` (or copied the
   folder under the Daoris home's `plugins/`), and it is not switched off;
2. a landing rule names it: `daoris driver landing --workspace <name> branch "feature/{quest}-{slug}"
   --plugin azure-devops-pull-request`, or Settings → Workspace → How work lands.

Installed and not named, it is never started: the driver loop keeps no process for a plugin that
speaks only at a landing. Named, it is started for one accepted landing, told the branch, and stopped.

## What it runs

In the repository's own checkout, after Daoris has made the branch:

```sh
git push --quiet -u origin <branch>
az repos pr create --source-branch <branch> --title <title> --output json --target-branch <base> \
  --description <line> <line> …
```

The organization, project and repository are the ones `az` detects from the checkout's `origin`
(`--detect`, on by default), so nothing here names them. The title is the quest's (or what you first
said in a conversation); the description names the quest, the session and the commits, one line per
value, as `--description` takes them. `--target-branch` is the line the work grew from; with none,
Azure Repos uses the repository's default branch. The pull request's web address is read from the
JSON `az` prints and comes back to the review and the terminal.

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
organisation: the arguments are `az repos pr create`'s documented ones.
