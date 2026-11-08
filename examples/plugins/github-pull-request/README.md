# `github-pull-request` — a pull request on GitHub, through its API

A plugin is a folder with a `plugin.json` (`docs/2026-09-23-plugin-design.md`, D64). This one performs one step,
`github-pull-request/pull-request`, on the work wire's `work/perform` (D241 point 5), and answers `work/state`. It runs **no git and no
CLI**: Daoris's own `push` node pushes the branch through its doors first, and this process opens and reads the pull request through
GitHub's REST API.

## Nothing runs until you ask for it

Tracked here as an example, it does nothing on its own. It runs only when **both** are true:

1. you installed it: `daoris plugin add examples/plugins/github-pull-request` (or copied the folder under the Daoris home's
   `plugins/`), and it is not switched off;
2. a place's workflow holds its step, after a `push`: *land* (branch), *push*, *github-pull-request/pull-request*. The shipped
   *Hosted merge workflow* preset (`pull-request`) is that graph.

## What it does

`pull-request` finds the unit the attempt was made for (the unit `work/watch` told it, by the tip), reads `origin` from the
checkout's git configuration as a file, and calls:

```
GET  /repos/{owner}/{repo}/pulls?head={owner}:{branch}&state=all     an open one for the branch is reused
GET  /repos/{owner}/{repo}                                           the default branch, where the line names none
POST /repos/{owner}/{repo}/pulls                                     { title, head, base, body }
```

It is **idempotent**: the kit's `work.mjs` keeps each idempotency key's outcome, so a redelivered key never calls GitHub twice, and a
422 (one is open for the branch) is answered by finding that one. It reaches with the pull request's address as evidence
(`evidence.url`) and `pull-request: github-pull-request/pull-request@1:<number>` as its output. It fails with `unauthorized` where
GitHub refuses the token, `no-token`, `no-unit`, `no-origin` or `rejected`; `unavailable` and `rate-limited` are transient, and
Daoris tries them again.

`work/state` reads `GET /repos/{owner}/{repo}/pulls/{number}` (the number from the address Daoris holds, else the newest pull request
from the branch) and answers `open`, `completed` (merged, with `mergeCommit`, `sourceCommit` and `target`) or `abandoned` (closed
unmerged). **Daoris removes nothing on this word alone**: a branch goes only where git confirms the merge commit is on the line.

## The token

The API call carries `DAORIS_HOST_TOKEN`: the API token of the git identity serving `origin`, handed to this one process by the
driver and to no other. The plugin is not started where no identity serves `origin`, the identity is read only on its host, git
access denies Write, or the identity has no API token (set one in the identity's page, or `daoris git identity token <identity> --kind api --token-stdin`). The token is
never logged or written, and is sent only to the API. Where the host is a GitHub Enterprise one, the API is `https://<host>/api/v3`;
`DAORIS_HOST_API` names another base address, as the test's stand-in does.

## What it needs

- **Node.js**, as `plugin.json` declares under `tools`: the process, and its calls to the API.
- **An API token on the git identity serving `origin`**, with the scope to open pull requests in the repository.
- **`origin`** naming the GitHub repository.

## Why each version

- **Node.js 18.0.0**: the plugin calls the API with the global `fetch` and `AbortSignal.timeout`, which came with Node.js 18 and 17.3
  ([Node.js 18](https://nodejs.org/en/blog/announcements/v18-release-announce)); the kit's `work.mjs` needs 16.6.0 for `Array.prototype.at`.
  Read from the release notes, not run on 18.0.0.

## Proof

`node --test` in this folder runs the kit's wire and contract tests, and `pull-request.test.mjs`, which starts the plugin against a
stand-in of GitHub's API on the loopback: open, reuse, state, a refused token, no token, and that the source starts no program. Nothing
of GitHub's is reached, and it has not been run against github.com itself.
