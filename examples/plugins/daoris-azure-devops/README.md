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
  "options": { "action": "observe" } }
```

wired `push` → `pull-request` → `monitor`, `monitor` `when merged` → `tidy` → `release`, with `release` the graph's end. The release
target is each repository's own, set as its values over that workflow in `driver.json`'s `workflowValues`, by repository, workflow id
and node id:

```json
"workflowValues": { "app": { "my-workflow": { "release": { "pipeline": 7, "releaseDefinition": 42, "stage": 402, "action": "start-stage" } } } }
```

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
GET   {vsrm}/{project}/_apis/release/definitions/{releaseDefinition}             the declared definition: its artifacts, stages and triggers
GET   {org}/{project}/_apis/build/builds?repositoryId=…&repositoryType=TfsGit&branchName={target}&definitions={pipeline}
GET   {org}/{project}/_apis/build/definitions?repositoryId=…&repositoryType=TfsGit&includeAllProperties=true   where no build ran
GET   {org}/{project}/_apis/git/repositories/{repo}/commits/{commit}/changes                                   where no build ran
GET   {org}/{project}/_apis/git/repositories/{repo}/items?path={yaml}&versionDescriptor.version={commit}…      a YAML pipeline's trigger
GET   {vsrm}/{project}/_apis/release/definitions?artifactType=Build&artifactSourceId={projectId}:{pipeline}&$expand=…   a hold's candidates
GET   {vsrm}/{project}/_apis/release/releases?definitionId=…&artifactTypeId=Build&sourceId=…&artifactVersionId={build}
GET   {vsrm}/{project}/_apis/release/releases/{id}                             the declared stage's status and approvals, every so often
POST  {vsrm}/{project}/_apis/release/releases                                  { definitionId, description, artifacts: [{ alias, instanceReference }] }, for create-and-start alone
PATCH {vsrm}/{project}/_apis/release/releases/{id}/environments/{stage}         { status: inProgress }, for start-stage and create-and-start alone
GET   {org}/{project}/_apis/build/builds/{build}/timeline                       a pipeline run's own stage and its approvals
```

**It acts only on the target its options declare**, by stable id: the build pipeline, the release definition (`none` for a pipeline
run's own stages), the exact stage, the action and what counts as success. Everything is resolved before any write. With no target
declared, or one declared in part, the step fails `no-target`, and Daoris holds it for the person; where the declared definition is gone,
takes no build of the declared pipeline, or holds no such stage, it fails `target-changed`. Either lists what it found for the pipeline,
even one candidate, each by id with its stages and its trigger (*#42 Portal (401 Dev, 402 Prod; no CD)*), and links the project's
releases. What it finds is only ever proposed: a definition's label, its trigger or the releases already made never choose the target.

The build is the newest one of the merged commit on the target branch of the declared pipeline. It waits for that build to succeed, then
looks for the declared definition's release made from exactly that build (its artifact under the definition's alias for the pipeline is
that build). With `observe` or `start-stage`, a release none made within `findSeconds` fails `no-release` (*No release of Portal (#42) was
made from build 20261110.41 (1a2b3c4d) within 600 s; the step's action does not create one.*). With `create-and-start`, it waits for a
release the definition's CD trigger makes where one takes the build's branch, and otherwise makes the release from that build, saying first
which stages its creation starts by itself; the release's description names the attempt, and a retry after an interrupted creation finds
the release it made rather than making a second. Several releases of the definition from one build fail `ambiguous`, listing them. Only the
declared stage is watched, until it ends: a pending approval is said on the plugin's console and waited on, never given here, and the stage
is started (once) only by `start-stage` and `create-and-start`. The step reaches when that stage ended as `success` asks, with the
release's or the run's address as evidence and the stage's outcome in its message (*Release-12 of app-release from build 20261110.41
(1a2b3c4d): Production succeeded after an approval by Release Approvers.*). A pipeline run's own stage is only observed.

**A merge the declared pipeline does not build** is reached at the first look, saying *Nothing to release*: its CI trigger is off, or its
branch filters leave out the target branch, or its path filters leave out every file the merge commit changed (a classic pipeline's
filters, a YAML pipeline's `trigger:` read from its file at the merge commit; `trigger: none`, a list of branches, or `branches` and
`paths` with `include` and `exclude`). Anything it cannot read (a trigger in another shape, a file it cannot fetch, a long list of changes)
is not taken for that: it waits `findSeconds` and fails `no-release`.

It fails with:

- `no-target`, `target-changed`: the target is not declared whole, or no longer resolves, with what was found;
- `stage-failed`: the declared stage rejected, failed or canceled, or ended in a result `success` does not take, in its words;
- `build-failed`: the declared pipeline's build of the merged commit did not succeed;
- `no-release`: no build of the merged commit in the declared pipeline within `findSeconds` (and the merge not shown to build nothing),
  or no release of the declared definition made from it within `findSeconds` where the action makes none;
- `ambiguous`: several releases of the declared definition made from the one build;
- `rejected`: Azure DevOps refused to make the release, or to start the stage;
- `timeout`: the attempt's deadline (the node's `timeoutSeconds`, else the step's 3600) came first, saying where the stage stood;
- `not-merged`, `no-unit`, `no-origin`, `no-token`, `unauthorized`; `unavailable` and `rate-limited` are transient, and while it
  watches it asks again rather than failing. A creation whose answer was lost fails `unavailable`, and the retry finds what it made.

Its options, where a repository's values over a shared workflow set them:

| Option | What it says |
|---|---|
| `pipeline` | the build pipeline's id; required |
| `releaseDefinition` | the classic release definition's id, or `none` for the pipeline run's own stages; required |
| `stage` | the stage: the definition's stage id, or with `none` the run's stage identifier; required |
| `action` | `observe` (never writes), `start-stage` (starts the declared stage of the release made from the build) or `create-and-start` (makes that release where none comes, then starts the stage); with `none`, `observe` alone; required |
| `success` | `succeeded`, or `partially-succeeded` to take a stage that partly succeeded; `succeeded` where not set |
| `findSeconds` | how long to look for the build and the release before saying none was found; 600 |
| `everySeconds` | how often to ask Azure DevOps while it watches; 30 |

An attempt a process before this one began is taken up from its start: it reads the release and the stage before it makes or starts
either, so nothing is made or started twice.

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

## Without a token: the person's signed-in browser

Where no token is kept, or Azure DevOps refuses the kept one (a 401, a 403 or its sign-in page), every call above (the pull request, its
auto-complete, `work/state`, the release watch and the agent's tools) is made from the person's signed-in session in Daoris's own browser
instead of failing `no-token`. A kept token Azure DevOps accepts is always the first way, and a refused one is not asked again by the
process until a new one is kept. Daoris hands the plugin the browser's DevTools address as `DAORIS_BROWSER` (the manifest's
`hooks.browser`; a host with no browser hands none, and the plugin then fails `no-token` or `unauthorized` as before).

- **A tab of its own** (`browser.mjs`). The plugin opens one tab per origin it asks (`dev.azure.com`, `vsrm.dev.azure.com`) in the
  browser's signed-in profile, holds it by its own target, never selects or navigates another, and closes it when the step's work ends.
  A fresh browser context was measured to carry no sign-in, so it is a tab, not a context; another session driving the browser moves
  its own tabs, never this one.
- **Only from the page.** Each call is a `fetch(url, { credentials: 'include', headers: { 'X-TFS-FedAuthRedirect': 'Suppress' } })` run
  inside that page. Nothing reads a cookie, a storage, a header the page sends or the browser's cookie store, and no token or cookie
  reaches the plugin's data, logs, evidence or the agent.
- **A sign-in that lapses** (a 401, a 203, or HTML where JSON was asked) is renewed once, by navigating the page to the organization
  (`{org}/_apis/connectionData`), which renews the single sign-on silently, and the call is made again. A second lapse fails the step as
  `signed-out`, naming the lapsed sign-in, with no second renewal.
- **The evidence names the way.** Each answer's evidence message (and `work/state`'s, and each tool's last line, with `data.way`) ends
  `Way: the token.`, `Way: the browser.` or `Way: the browser (the kept token was refused).`; the outputs and the other fields are the
  same by either way.

## Tools for an agent

Four commands are marked `tool`, so a session at a place where the plugin's tools are on calls them through its `daoris-plugins`
server: `status`, and the three an agent working a pull-request step needs, each naming the repository by its origin's address:
`create-pull-request` (a pushed branch, its target, a title and a description, and the step's auto-complete options as arguments; the active one found again), `pull-request-status` (by
its address or id, or from its branch) and `complete-pull-request` (by `merge`, `squash`, `rebase` or `rebase-merge`, the source
branch deleted where asked, the repository's policies still deciding). Each answers its pull request's id, address, state, branches
and merge commit as `data` (`pull-request-commands.mjs`).

## What it needs

- **Node.js**, as `plugin.json` declares under `tools`: the process, its calls to the API, and its connection to Daoris's browser.
- **A personal access token, kept by `login`**, or the person's sign-in to Azure DevOps in Daoris's browser (the token is the first way), with the scopes Code (read and write) for the pull request and its auto-complete,
  Build (read) for the release step, and Release (read, write and execute) where its action is `start-stage` or `create-and-start`
  (read alone for `observe`).
  The token's user needs the repository's *Contribute to pull requests* permission to set auto-complete.
- **`origin`** naming the Azure Repos repository.

## Why each version

- **Node.js 22.0.0**: the plugin calls the API with the global `fetch` and `AbortSignal.timeout`, and reaches the browser over the global
  `WebSocket`, which Node.js 22 enables by default ([Node.js 22](https://nodejs.org/en/blog/announcements/v22-release-announce)); the kit's
  `work.mjs` needs 16.6.0 for `Array.prototype.at`. Read from the release notes.

## Proof

`node --test` in this folder runs the kit's wire and contract tests, and the plugin's own against `stand-in.mjs`, a stand-in of Azure
DevOps' API on the loopback: `pull-request.test.mjs` (open, reuse, state with the merge commit, a refused token, no token, and that the
source starts no program), `auto-complete.test.mjs` (set, off by default, refused by a policy, refused for want of permission, an unknown
merge strategy) and `release.test.mjs` (the declared stage watched to success, a failed stage, a partial success, a pending approval
waited on, the stage started by `start-stage` alone, no build found, a merge the pipeline does not build, a pipeline run's own stage, and
the deadline), `release-target.test.mjs` (no target declared or one no longer resolving held with its candidates, even one; a line feeding
three release definitions never started on a found one; a release made by `create-and-start` alone, from the exact build, its self-starting
stages said first; a wrong build refused; an interrupted creation never made twice) and `triggers.test.mjs` (the YAML trigger's shapes,
and the stages a creation starts) and `browser.test.mjs`, against `browser-stand-in.mjs` as well, a stand-in of Daoris's
browser's DevTools endpoint whose pages run the call with a `fetch` that sends the session (the token way left alone, the browser
way with no token or a refused one, the same outputs and evidence by either, a lapsed sign-in renewed once and then said, a tab of its own,
and no cookie or storage asked for). None of it has been run against a
real Azure DevOps organisation: whether `lastMergeCommit` names a squash's commit on the target, and how each API answers a refusal by
policy, were never measured, and git's confirmation is what keeps a wrong reading safe.
