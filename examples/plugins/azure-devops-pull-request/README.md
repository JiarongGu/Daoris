# `azure-devops-pull-request` — a pull request on Azure DevOps, through its API

A plugin is a folder with a `plugin.json` (`docs/2026-09-23-plugin-design.md`, D64). This one performs one step,
`azure-devops-pull-request/pull-request`, on the work wire's `work/perform` (D241 point 5), and answers `work/state`. It runs **no git
and no CLI**: Daoris's own `push` node pushes the branch through its doors first, and this process opens and reads the pull request
through Azure DevOps' REST API.

## Nothing runs until you ask for it

Tracked here as an example, it does nothing on its own. It runs only when **both** are true:

1. you installed it: `daoris plugin add examples/plugins/azure-devops-pull-request` (or copied the folder under the Daoris home's
   `plugins/`), and it is not switched off;
2. a place's workflow holds its step, after a `push`: *land* (branch), *push*, *azure-devops-pull-request/pull-request*. No shipped preset names it (the *Hosted merge workflow* preset holds
   GitHub's step): add the step to a workflow of your own after its *push*.

## What it does

`pull-request` finds the unit the attempt was made for (the unit `work/watch` told it, by the tip), reads `origin` from the
checkout's git configuration as a file (an `https://dev.azure.com/…/_git/…`, a `visualstudio.com` or an SSH `v3` address), and calls,
with `api-version=7.1`:

```
GET  {org}/{project}/_apis/git/repositories/{repo}/pullrequests?searchCriteria.sourceRefName=refs/heads/{branch}&searchCriteria.status=all
GET  {org}/{project}/_apis/git/repositories/{repo}                  the default branch, where the line names none
POST {org}/{project}/_apis/git/repositories/{repo}/pullrequests     { sourceRefName, targetRefName, title, description }
```

An active pull request for the branch is reused, and a 409 (`TF401179`, one is active) is answered by finding that one. The step is
**idempotent**: the kit's `work.mjs` keeps each idempotency key's outcome, so a redelivered key never calls Azure DevOps twice. It reaches
with the pull request's address as evidence (`evidence.url`) and `pull-request: azure-devops-pull-request/pull-request@1:<id>` as its
output. It fails with `unauthorized` where Azure DevOps refuses the token (a 401, 403 or its sign-in page), `no-token`, `no-unit`,
`no-origin` or `rejected`; `unavailable` and `rate-limited` are transient, and Daoris tries them again.

`work/state` (PLUGHOOK1a, D148) reads `GET …/pullrequests/{id}` (the id from the address Daoris holds, else the one from the branch,
among those into the line a completed one, else an active one, else the newest abandoned one) and answers `open`, `abandoned` or
`completed`, with `lastMergeCommit` as the merge commit, `lastMergeSourceCommit` as the commit it merged, `targetRefName` as the
target, `closedDate` as when, and `completionOptions.mergeStrategy` as how (`noFastForward` is *merge*, `squash` and `rebase` keep
their names, `rebaseMerge` is *rebase-merge*). A completed one with no merge commit is answered *unknown*, saying so.

**Daoris removes nothing on this word alone.** A branch goes only where git confirms the answer: the merge commit is on the line, and
the branch stands at or under the commit the pull request merged. A reading that is wrong keeps the branch.

## The token

The API call carries `DAORIS_HOST_TOKEN`: the API token (a personal access token, sent as Basic's password) of the git identity
serving `origin`, handed to this one process by the driver and to no other. The plugin is not started where no identity serves
`origin`, the identity is read only on its host, git access denies Write, or the identity has no API token (set one in the identity's
page, or `daoris git identity token <identity> --kind api --token-stdin`). The token is never logged or written, and is sent only to the API. `DAORIS_HOST_API` names another
base address in place of `https://dev.azure.com`, as the test's stand-in does.

## What it needs

- **Node.js**, as `plugin.json` declares under `tools`: the process, and its calls to the API.
- **An API token on the git identity serving `origin`**, with the Code (read and write) scope.
- **`origin`** naming the Azure Repos repository.

## Why each version

- **Node.js 18.0.0**: the plugin calls the API with the global `fetch` and `AbortSignal.timeout`, which came with Node.js 18 and 17.3
  ([Node.js 18](https://nodejs.org/en/blog/announcements/v18-release-announce)); the kit's `work.mjs` needs 16.6.0 for `Array.prototype.at`.
  Read from the release notes, not run on 18.0.0.

## Proof

`node --test` in this folder runs the kit's wire and contract tests, and `pull-request.test.mjs`, which starts the plugin against a
stand-in of Azure DevOps' API on the loopback: open, reuse, state with the merge commit, a refused token, no token, and that the source
starts no program. It has not been run against a real Azure DevOps organisation: whether `lastMergeCommit` names a squash's commit on
the target was never measured, and git's confirmation is what keeps a wrong reading safe.
