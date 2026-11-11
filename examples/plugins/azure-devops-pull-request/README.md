# `azure-devops-pull-request` — a pull request on Azure DevOps, merged and released, through its API

A plugin is a folder with a `plugin.json` (`docs/2026-09-23-plugin-design.md`, D64). This one performs two steps on the work wire's
`work/perform` (D241 point 5), `azure-devops-pull-request/pull-request` and `azure-devops-pull-request/release`, and answers
`work/state`. It runs **no git and no CLI**: Daoris's own `push` node pushes the branch through its doors first, and this process opens,
completes and follows the pull request and its release through Azure DevOps' REST API.

## Nothing runs until you ask for it

Tracked here as an example, it does nothing on its own. It runs only when **both** are true:

1. you installed it: `daoris plugin add examples/plugins/azure-devops-pull-request` (or copied the folder under the Daoris home's
   `plugins/`), and it is not switched off;
2. a place's workflow holds its steps: *land* (branch), *push*, *azure-devops-pull-request/pull-request*, *monitor*, and on the monitor's
   `when merged` way *tidy* and *azure-devops-pull-request/release*. No shipped preset names them (the *Hosted merge workflow* preset holds
   GitHub's step): add them to a workflow of your own.

A workflow that carries a change end to end sets the steps' options as its graph's nodes do:

```json
{ "id": "pull-request", "kind": "azure-devops-pull-request/pull-request", "version": 1, "reach": "performed",
  "options": { "autoComplete": true, "mergeStrategy": "squash", "deleteSourceBranch": true, "transitionWorkItems": true } },
{ "id": "release", "kind": "azure-devops-pull-request/release", "version": 1, "reach": "performed", "timeoutSeconds": 14400,
  "options": { "releaseDefinition": "app-release", "stages": ["Staging", "Production"], "deploy": "Production" } }
```

wired `push` → `pull-request` → `monitor`, `monitor` `when merged` → `tidy` → `release`, with `release` the graph's end.

## What it does

### `pull-request`

It finds the unit the attempt was made for (the unit `work/watch` told it, by the tip), reads `origin` from the checkout's git
configuration as a file (an `https://dev.azure.com/…/_git/…`, a `visualstudio.com` or an SSH `v3` address), and calls, with
`api-version=7.1`:

```
GET   {org}/{project}/_apis/git/repositories/{repo}/pullrequests?searchCriteria.sourceRefName=refs/heads/{branch}&searchCriteria.status=all
GET   {org}/{project}/_apis/git/repositories/{repo}                  the default branch, where the line names none
POST  {org}/{project}/_apis/git/repositories/{repo}/pullrequests     { sourceRefName, targetRefName, title, description }
```

An active pull request for the branch is reused, and a 409 (`TF401179`, one is active) is answered by finding that one. The step is
**idempotent**: the kit's `work.mjs` keeps each idempotency key's outcome, so a redelivered key never calls Azure DevOps twice. It reaches
with the pull request's address as evidence (`evidence.url`) and `pull-request: azure-devops-pull-request/pull-request@1:<id>` as its
output. It fails with `unauthorized` where Azure DevOps refuses the token (a 401, 403 or its sign-in page), `no-token`, `no-unit`,
`no-origin` or `rejected`; `unavailable` and `rate-limited` are transient, and Daoris tries them again.

**Auto-complete** is off unless the step's options say `autoComplete: true`. Then, once the pull request is opened or found, it reads the
token's own identity (`GET {org}/_apis/connectionData`, `api-version=7.1-preview`) and sets it as the one completing the pull request:

```
PATCH {org}/{project}/_apis/git/repositories/{repo}/pullrequests/{id} { autoCompleteSetBy: { id }, completionOptions }
```

`completionOptions` takes the options `mergeStrategy` (`squash`, `noFastForward`, `rebase` or `rebaseMerge`; `noFastForward` where
none is named), `deleteSourceBranch` and `transitionWorkItems` (each off unless `true`) and `mergeCommitMessage` (Azure DevOps' own where
none is named). Azure DevOps then merges the pull request once its branch policies pass: required reviewers, builds and checks still
decide, and nobody has to press *Complete*. The evidence's message ends saying which: *Auto-complete off*, *Auto-complete set (squash,
source branch deleted)*, or *Auto-complete not set* with Azure DevOps' words, where a policy or the token's permissions refuse it, or the
merge strategy is not one of the four; a refusal never fails the step. Azure DevOps not answering fails it as `unavailable`, and the
attempt Daoris makes again finds the same pull request and sets it.

### `work/state`

`work/state` (PLUGHOOK1a, D148) reads `GET …/pullrequests/{id}` (the id from the address Daoris holds, else the one from the branch,
among those into the line a completed one, else an active one, else the newest abandoned one) and answers `open`, `abandoned` or
`completed`, with `lastMergeCommit` as the merge commit, `lastMergeSourceCommit` as the commit it merged, `targetRefName` as the
target, `closedDate` as when, and `completionOptions.mergeStrategy` as how (`noFastForward` is *merge*, `squash` and `rebase` keep
their names, `rebaseMerge` is *rebase-merge*). A completed one with no merge commit is answered *unknown*, saying so. A merge
auto-complete made is answered as any other, so the workflow's `monitor` reaches `merged` on it.

**Daoris removes nothing on this word alone.** A branch goes only where git confirms the answer: the merge commit is on the line, and
the branch stands at or under the commit the pull request merged. A reading that is wrong keeps the branch.

### `release`

Placed after the merge, it reads the unit's pull request (by the address Daoris holds, else by its branch) and takes its
`lastMergeCommit` and `targetRefName`; a pull request not completed fails it as `not-merged`. Then, with `api-version=7.1` on the
project's API and `api-version=7.1-preview` on release management's (`vsrm.dev.azure.com`):

```
GET   {org}/{project}/_apis/build/builds?repositoryId=…&repositoryType=TfsGit&branchName={target}[&definitions={pipeline}]
GET   {vsrm}/{project}/_apis/release/definitions?artifactType=Build&artifactSourceId={projectId}:{pipeline}&$expand=artifacts
GET   {vsrm}/{project}/_apis/release/releases?definitionId=…&artifactTypeId=Build&sourceId=…&artifactVersionId={build}
GET   {vsrm}/{project}/_apis/release/releases/{id}                             each stage's status and approvals, every so often
PATCH {vsrm}/{project}/_apis/release/releases/{id}/environments/{stage}         { status: inProgress }, for `deploy` alone
GET   {org}/{project}/_apis/build/builds/{build}/timeline                       a pipeline run's own stages and approvals
```

The build is the newest one of the merged commit on the target branch, of the pipeline named, else of the one pipeline that built it,
else of the one pipeline a release definition takes. Where a release definition takes that build's artifact, it waits for the build to
succeed, finds the classic release made from it, and watches that release's stages; where none does, it watches the pipeline run's own
stages (a YAML pipeline's). Each stage awaited is watched until it ends: a pending approval is said on the plugin's console and waited
on, never given here. The step reaches when every awaited stage succeeded, with the release's or the run's address as evidence and each
stage's outcome in its message (*Release-12 of app-release from build 20261110.41 (1a2b3c4d): Staging succeeded; Production succeeded
after an approval by Release Approvers.*). It fails with:

- `stage-failed`: a stage rejected, failed or canceled (a stage of a pipeline run skipped too, where `stages` names it), in its words;
- `build-failed`: the build a classic release takes did not succeed;
- `no-release`: no build of the merged commit within `findSeconds`, no release made from it within `findSeconds`, a build with no
  stages that no release definition takes, or a pipeline, release definition or stage named that is not there;
- `ambiguous`: builds of several pipelines, or several release definitions, and no option naming one;
- `timeout`: the attempt's deadline (the node's `timeoutSeconds`, else the step's 3600) came first, saying where each stage stood, and of
  a stage nothing started that `deploy` would start it;
- `not-merged`, `no-unit`, `no-origin`, `no-token`, `unauthorized`; `unavailable` and `rate-limited` are transient, and while it
  watches it asks again rather than failing.

Its options, each optional:

| Option | What it says |
|---|---|
| `pipeline` | the build pipeline, by name or id; else the one that built the merged commit |
| `releaseDefinition` | the classic release definition, by name or id; else the one whose artifact is that build |
| `stages` | the stage or stages to wait for, by name; else every stage |
| `deploy` | the stage or stages of a classic release to start where nothing started them; Azure DevOps' approvals and gates still decide |
| `findSeconds` | how long to look for the build and the release before saying none was found; 600 |
| `everySeconds` | how often to ask Azure DevOps while it watches; 30 |

A pipeline run's stages are started by their own triggers: `deploy` starts a classic release's stage alone. An attempt a process before
this one began is taken up from its start: it reads each stage before it starts one, so nothing is started twice.

## The token

The API call carries `DAORIS_HOST_TOKEN`: the API token (a personal access token, sent as Basic's password) of the git identity
serving `origin`, handed to this one process by the driver and to no other. The plugin is not started where no identity serves
`origin`, the identity is read only on its host, git access denies Write, or the identity has no API token (set one in the identity's
page, or `daoris git identity token <identity> --kind api --token-stdin`). The token is never logged or written, and is sent only to the API. `DAORIS_HOST_API` names another
base address in place of `https://dev.azure.com` and release management's, as the test's stand-in does.

## What it needs

- **Node.js**, as `plugin.json` declares under `tools`: the process, and its calls to the API.
- **An API token on the git identity serving `origin`**, with the scopes Code (read and write) for the pull request and its auto-complete,
  Build (read) for the release step, and Release (read, write and execute) where it starts a stage by `deploy` (read alone otherwise).
  The token's identity needs the repository's *Contribute to pull requests* permission to set auto-complete.
- **`origin`** naming the Azure Repos repository.

## Why each version

- **Node.js 18.0.0**: the plugin calls the API with the global `fetch` and `AbortSignal.timeout`, which came with Node.js 18 and 17.3
  ([Node.js 18](https://nodejs.org/en/blog/announcements/v18-release-announce)); the kit's `work.mjs` needs 16.6.0 for `Array.prototype.at`.
  Read from the release notes, not run on 18.0.0.

## Proof

`node --test` in this folder runs the kit's wire and contract tests, and the plugin's own against `stand-in.mjs`, a stand-in of Azure
DevOps' API on the loopback: `pull-request.test.mjs` (open, reuse, state with the merge commit, a refused token, no token, and that the
source starts no program), `auto-complete.test.mjs` (set, off by default, refused by a policy, refused for want of permission, an unknown
merge strategy) and `release.test.mjs` (a classic release watched to success, a failed stage, a pending approval waited on, a stage
started by `deploy`, no build and no release found, a pipeline run's own stages, and the deadline). None of it has been run against a
real Azure DevOps organisation: whether `lastMergeCommit` names a squash's commit on the target, and how each API answers a refusal by
policy, were never measured, and git's confirmation is what keeps a wrong reading safe.
