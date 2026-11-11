# `daoris-azure-devops` — Daoris's work on Azure DevOps, through its API

Daoris's own Azure DevOps plugin, in Daoris's `daoris-` family of plugins (a plugin's id is `<family>-<name>`; yours takes your own
family). It covers the pull request (opened, set to auto-complete, its merge followed) and the release after it, and is where Azure
DevOps' pipelines and work items will go. It was `azure-devops-pull-request`: its manifest says so (`formerly`), and Daoris moves that
id's sign-in, switches and kept data to this one once, and runs a workflow still naming `azure-devops-pull-request/pull-request` on it.
Its schemas keep their names (`azure-devops-pull-request/receipt@1` and the others), since evidence already kept names them.

A plugin is a folder with a `plugin.json` (`docs/2026-09-23-plugin-design.md`, D64). This one performs two steps on the work wire's
`work/perform` (D241 point 5), `daoris-azure-devops/pull-request` and `daoris-azure-devops/release`, and answers
`work/state`. It runs **no git and no CLI**: Daoris's own `push` node pushes the branch through its doors first, and this process opens,
completes and follows the pull request and its release through Azure DevOps' REST API.

## Nothing runs until you ask for it

Tracked here as an example, it does nothing on its own. It runs only when **both** are true:

1. you installed it: `daoris plugin add examples/plugins/daoris-azure-devops` (or copied the folder under the Daoris home's
   `plugins/`), and it is not switched off;
2. a place's workflow holds its steps: *land* (branch), *push*, *daoris-azure-devops/pull-request*, *monitor*, and on the monitor's
   `when merged` way *tidy* and *daoris-azure-devops/release*. No shipped preset names them (the *Hosted merge workflow* preset holds
   GitHub's step): add them to a workflow of your own.

A workflow that carries a change end to end sets the steps' options as its graph's nodes do:

```json
{ "id": "pull-request", "kind": "daoris-azure-devops/pull-request", "version": 1, "reach": "performed",
  "options": { "autoComplete": true, "mergeStrategy": "squash", "deleteSourceBranch": true, "transitionWorkItems": true } },
{ "id": "release", "kind": "daoris-azure-devops/release", "version": 1, "reach": "performed", "timeoutSeconds": 14400,
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
GET   {org}/{project}/_apis/build/definitions?repositoryId=…&repositoryType=TfsGit&includeAllProperties=true   where no build ran
GET   {org}/{project}/_apis/git/repositories/{repo}/commits/{commit}/changes                                   where no build ran
GET   {org}/{project}/_apis/git/repositories/{repo}/items?path={yaml}&versionDescriptor.version={commit}…      a YAML pipeline's trigger
GET   {vsrm}/{project}/_apis/release/definitions?artifactType=Build&artifactSourceId={projectId}:{pipeline}&$expand=artifacts,triggers
GET   {vsrm}/{project}/_apis/release/releases?[definitionId=…&]artifactTypeId=Build&sourceId=…&artifactVersionId={build}
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
after an approval by Release Approvers.*).

**With no options**, a repository's pipeline is usually the artifact of several release definitions (dev, QA and prod; a disabled one).
The step then watches the releases actually made from the build, each by its own stages: it waits (within `findSeconds`) for a release of
every definition whose CD trigger takes the build's branch, and watches those together, a release whose every stage starts by hand left
alone and said. A definition with no trigger on the branch (QA, prod) is not watched unless a release was made from the build anyway. One
release failing fails the step.

**A merge that builds nothing** is reached at the first look, saying *Nothing to release*, where every pipeline of the repository leaves
it out: its CI trigger is off, or its branch filters leave out the target branch, or its path filters leave out every file the merge
commit changed (a classic pipeline's filters, a YAML pipeline's `trigger:` read from its file at the merge commit; `trigger: none`, a list of
branches, or `branches` and `paths` with `include` and `exclude`). Anything it cannot read (a trigger in another shape, a file it cannot
fetch, no pipeline listed, a long list of changes) is not taken for that: it waits `findSeconds` and fails `no-release` as before.

It fails with:

- `stage-failed`: a stage rejected, failed or canceled (a stage of a pipeline run skipped too, where `stages` names it), in its words;
- `build-failed`: the build a classic release takes did not succeed;
- `no-release`: no build of the merged commit within `findSeconds` (and the merge not shown to build nothing), no release made from it
  within `findSeconds` (of several definitions: none triggering on the branch, or every release made starting by hand), a build with no
  stages that no release definition takes, or a pipeline, release definition or stage named that is not there;
- `ambiguous`: builds of several pipelines, and no option naming one;
- `timeout`: the attempt's deadline (the node's `timeoutSeconds`, else the step's 3600) came first, saying where each stage stood, and of
  a stage nothing started that `deploy` would start it;
- `not-merged`, `no-unit`, `no-origin`, `no-token`, `unauthorized`; `unavailable` and `rate-limited` are transient, and while it
  watches it asks again rather than failing.

Its options, each optional:

| Option | What it says |
|---|---|
| `pipeline` | the build pipeline, by name or id; else the one that built the merged commit |
| `releaseDefinition` | the classic release definition, by name or id; else the releases made from that build by the definitions whose CD trigger takes its branch |
| `stages` | the stage or stages to wait for, by name; else every stage |
| `deploy` | the stage or stages of a classic release to start where nothing started them; Azure DevOps' approvals and gates still decide |
| `findSeconds` | how long to look for the build and the release before saying none was found; 600 |
| `everySeconds` | how often to ask Azure DevOps while it watches; 30 |

A pipeline run's stages are started by their own triggers: `deploy` starts a classic release's stage alone. An attempt a process before
this one began is taken up from its start: it reads each stage before it starts one, so nothing is started twice.

## The token

The plugin signs in itself, through the commands its manifest declares under `interfaces`:

- `daoris daoris-azure-devops login` asks for a personal access token, hidden as you type it (or reads it from standard input
  with `--stdin`), and keeps it in the plugin's own private storage (`DAORIS_PLUGIN_PRIVATE`, `credential.json`), a folder Daoris
  hands this plugin's processes alone. A git identity serving Azure DevOps that already held an API token hands it to `login` once.
- `daoris daoris-azure-devops status` says whether a token is kept, since when, and whether Azure DevOps last refused it.
- `daoris daoris-azure-devops logout` forgets it.

Each API call carries the kept token as Basic's password; it is never logged, said back or put in evidence, and is sent only to the
API. A refused one is noted for `status`. The plugin is not started where `origin` is served by a read-only identity or git access
denies Write. `DAORIS_HOST_API` names another base address in place of `https://dev.azure.com` and release management's, as the
test's stand-in does.

## Tools for an agent

Four commands are marked `tool`, so a session at a place where the plugin's tools are on calls them through its `daoris-plugins`
server: `status`, and the three an agent working a pull-request step needs, each naming the repository by its origin's address:
`create-pull-request` (a pushed branch, its target, a title and a description; the active one found again), `pull-request-status` (by
its address or id, or from its branch) and `complete-pull-request` (by `merge`, `squash`, `rebase` or `rebase-merge`, the source
branch deleted where asked, the repository's policies still deciding). Each answers its pull request's id, address, state, branches
and merge commit as `data` (`pull-request-commands.mjs`).

## What it needs

- **Node.js**, as `plugin.json` declares under `tools`: the process, and its calls to the API.
- **A personal access token, kept by `login`**, with the scopes Code (read and write) for the pull request and its auto-complete,
  Build (read) for the release step, and Release (read, write and execute) where it starts a stage by `deploy` (read alone otherwise).
  The token's user needs the repository's *Contribute to pull requests* permission to set auto-complete.
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
started by `deploy`, no build and no release found, a pipeline run's own stages, and the deadline), `release-places.test.mjs` (a build taken
by several release definitions as four repositories of one organisation shape them, and a merge no pipeline builds, by a path filter, a branch
filter or a trigger off) and `triggers.test.mjs` (the YAML trigger's shapes). None of it has been run against a
real Azure DevOps organisation: whether `lastMergeCommit` names a squash's commit on the target, and how each API answers a refusal by
policy, were never measured, and git's confirmation is what keeps a wrong reading safe.
