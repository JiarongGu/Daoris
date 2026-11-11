# `daoris-github` — Daoris's work on GitHub, through its API

Daoris's own GitHub plugin, in Daoris's `daoris-` family of plugins (a plugin's id is `<family>-<name>`; yours takes your own
family). Today it opens and reads the pull request. It was `github-pull-request`: its manifest says so (`formerly`), and Daoris moves
that id's sign-in, switches and kept data to this one once, and runs a workflow still naming `github-pull-request/pull-request` on it.
Its schemas keep their names (`github-pull-request/receipt@1` and `github-pull-request/pull-request@1`), since evidence already kept
names them.

A plugin is a folder with a `plugin.json` (`docs/2026-09-23-plugin-design.md`, D64). This one performs one step,
`daoris-github/pull-request`, on the work wire's `work/perform` (D241 point 5), and answers `work/state`. It runs **no git and no
CLI**: Daoris's own `push` node pushes the branch through its doors first, and this process opens and reads the pull request through
GitHub's REST API.

## Nothing runs until you ask for it

Tracked here as an example, it does nothing on its own. It runs only when **both** are true:

1. you installed it: `daoris plugin add examples/plugins/daoris-github` (or copied the folder under the Daoris home's
   `plugins/`), and it is not switched off;
2. a place's workflow holds its step, after a `push`: *land* (branch), *push*, *daoris-github/pull-request*. The shipped
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

The plugin signs in itself: `daoris daoris-github login` asks for a token, hidden as you type it (or `--stdin`), and keeps it in
the plugin's own private storage (`DAORIS_PLUGIN_PRIVATE`), handed to this plugin's processes alone; `status` says whether one is kept
and whether GitHub last refused it; `logout` forgets it. A git identity serving github.com that already held an API token hands it to
`login` once. The token is never logged, said back or put in evidence, and is sent only to the API. The plugin is not started where
`origin` is served by a read-only identity or git access denies Write. Where the host is a GitHub Enterprise one, the API is
`https://<host>/api/v3`; `DAORIS_HOST_API` names another base address, as the test's stand-in does.

## Tools for an agent

Four commands are marked `tool`, so a session at a place where the plugin's tools are on calls them through its `daoris-plugins`
server: `status`, and the three an agent working a pull-request step needs, each naming the repository by its origin's address:
`create-pull-request` (a pushed branch, its base, a title and a body; the open one found again), `pull-request-status` (by its
address or number, or from its branch) and `complete-pull-request` (by `merge`, `squash` or `rebase`, the branch deleted where asked,
branch protection still deciding). Each answers its pull request's number, address, state, branches and merge commit as `data`
(`pull-request-commands.mjs`, over the API helpers in `github.mjs` the step shares). A merge needs the token's Contents (Read and
write) on the repository.

## What it needs

- **Node.js**, as `plugin.json` declares under `tools`: the process, and its calls to the API.
- **A token kept by `login`**, with the scope to open pull requests in the repository.
- **`origin`** naming the GitHub repository.

## Why each version

- **Node.js 18.0.0**: the plugin calls the API with the global `fetch` and `AbortSignal.timeout`, which came with Node.js 18 and 17.3
  ([Node.js 18](https://nodejs.org/en/blog/announcements/v18-release-announce)); the kit's `work.mjs` needs 16.6.0 for `Array.prototype.at`.
  Read from the release notes, not run on 18.0.0.

## Proof

`node --test` in this folder runs the kit's wire and contract tests, and `pull-request.test.mjs`, which starts the plugin against a
stand-in of GitHub's API on the loopback: open, reuse, state, a refused token, no token, and that the source starts no program. Nothing
of GitHub's is reached, and it has not been run against github.com itself.
