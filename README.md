# ![logo](https://raw.githubusercontent.com/JiarongGu/Daoris/main/docs/assets/daoris-logo.svg) Daoris (道衍)

[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](https://github.com/JiarongGu/Daoris/blob/main/LICENSE)
[![Node ≥22](https://img.shields.io/badge/node-%E2%89%A522-339933.svg)](https://nodejs.org/)

**Engineering doctrine for coding agents, kept the same in every repository, and a desktop that runs
the agents.** The `daoris` CLI writes one shared canon of rules, knowledge and skills into each
repository, where Claude Code, Codex and the other agents read it, and keeps the copies from drifting.
The desktop drives the agents' sessions one repository at a time, and lets one repository ask another
for work instead of reaching into it.

> 道衍 (dàoyǎn), *propagation and unfolding*: doctrine flows into repositories, and their refinements
> evolve the canon.

## Why Daoris

- **Copied doctrine diverges.** Every repository that writes its own agent rules writes a slightly
  different version of the same ones. Daoris tracks where each copy came from, reports a local edit as
  drift, and carries a reviewed improvement back to the canon with `daoris upstream`.
- **The tier is the location.** The rules every task needs land in a region of `AGENTS.md`, the one
  file every agent harness reads; knowledge is read on demand; a skill is invoked by name. Nothing
  loads what a task does not need.
- **Offline and dependency-free.** The canon ships inside the package, so pinning the package pins the
  doctrine, and `daoris check` is a gate that needs no network and no service.
- **Ownership stays local.** A session changes its own repository. Work another repository owns is a
  quest that repository takes, with its own review, never an edit from outside.
- **One desktop for the work.** Sessions, quests, repository setup and agent accounts in one window
  that hosts the local service, on this machine first, with a team remote only when you add one.

## Quickstart

In a git repository, with Node 22 or later:

```sh
npx daoris@0.0.1 init     # write daoris.json (and a .gitattributes if none), report the packs
npx daoris@0.0.1 sync     # write the doctrine into AGENTS.md and .claude/, record daoris.lock
npx daoris@0.0.1 check    # the gate: offline, exit 1 on drift
```

`init` writes the manifest with the core doctrine only: add a pack to its `packs` deliberately, fill in
`domain` (what the repository is, owns and accepts), and run `sync` again. Commit `daoris.json`,
`daoris.lock`, `AGENTS.md`, `CLAUDE.md` and `.claude/` together, and put `daoris check` in your gates.
Nothing is published until the first release (D105); from a checkout of this repository, run
`node src/Daoris.Cli/bin/daoris.mjs` in place of `npx daoris@0.0.1`.

## What's in the box

| You want | Reach for | Read |
|---|---|---|
| Shared agent rules, knowledge and skills in a repository | `daoris init`, `sync`, `check` | [the guide](https://github.com/JiarongGu/Daoris/blob/main/docs/guide/README.md#commands) |
| Stack-specific doctrine (.NET libraries, desktop apps, SQL, web views, …) | a pack in the manifest's `packs` | [three layers](https://github.com/JiarongGu/Daoris/blob/main/docs/guide/README.md#three-layers) |
| To know what adopting would change before it does | `daoris analyze`, `doctor` | [the guide](https://github.com/JiarongGu/Daoris/blob/main/docs/guide/README.md#commands) |
| A local improvement carried back to the canon | `daoris upstream <file>` | [the guide](https://github.com/JiarongGu/Daoris/blob/main/docs/guide/README.md#commands) |
| Gates every repository needs: links, docs freshness, sensitive data | `daoris-devkit verify` | [the devkit](https://github.com/JiarongGu/Daoris/blob/main/src/Daoris.Devkit/README.md) |
| Knowledge searched across repositories, and quests between them | the knowledge service and its connector | [the service](https://github.com/JiarongGu/Daoris/blob/main/src/Daoris.Service/README.md) |
| Agent sessions driven per repository, with accounts and review | the desktop | [the desktop](https://github.com/JiarongGu/Daoris/blob/main/src/Daoris.Desktop/README.md) |
| A worked two-repository family | `examples/` | [the examples](https://github.com/JiarongGu/Daoris/blob/main/examples/README.md) |

## Packages

| Package | Where | What it is |
|---|---|---|
| `daoris` | npm | The CLI: the doctrine commands, offline and with no runtime dependency, and the management commands for the desktop's home. Node 22 or later |
| `daoris-devkit-<rid>` | GitHub release | The universal gates as one native binary per platform, each with its `.sha256` |
| `daoris-knowledge-<rid>`, `daoris-knowledge-http-<rid>` | GitHub release | The knowledge service: the connector agents are handed, and the HTTP host that serves the platform and, in shared mode, a team's remote |
| Daoris desktop | built from source | The Windows application (`npm run publish:desktop`): the window, the driver, the local service and the CLI it lays out |

The CLI depends on no other project. The service consumes released [Lyntai](https://github.com/JiarongGu/Lyntai)
libraries, and the desktop is built on [Shenora](https://github.com/JiarongGu/Shenora); the three are
deliberately independent (D22).

## Status

Development is unpublished `0.0.x`: nothing is on npm or a release page until the first release, and
the install lines above describe the release interface. The doctrine CLI, the devkit, the service and
the desktop are built and exercised end to end by the family rehearsal; the desktop is in daily use on
Windows, where it develops this repository. [The changelog](https://github.com/JiarongGu/Daoris/blob/main/CHANGELOG.md)
says what changed, and [the roadmap](https://github.com/JiarongGu/Daoris/blob/main/ROADMAP.md) what comes next.

## Rules for coding agents

What an agent working in a repository that adopted Daoris most often gets wrong:

<!-- agent-rules:begin — llms.txt and context7.json repeat this block; tools/doc-links.mjs holds the three equal (D211) -->
- Never edit a synced copy: a file under `.claude/` with a provenance header, or the `daoris:rules` region of `AGENTS.md`. `daoris check` reports the edit as drift; promote an improvement with `daoris upstream <file>`.
- Write the repository's own text outside the `daoris:rules` region of `AGENTS.md`, which `sync` rewrites whole. `CLAUDE.md` loads it through its `@AGENTS.md` import.
- `daoris check` compares the disk with `daoris.lock`, never with the newest canon, and needs no network. A pack added to `daoris.json` fails it until `daoris sync` runs.
- After `daoris sync`, commit `daoris.json`, `daoris.lock`, `AGENTS.md`, `CLAUDE.md` and `.claude/` together: the lock is what `check` judges, and the diff is what a reviewer reads.
- Never hand-edit `.claude/INDEX.md`: `sync` regenerates it from the files, and `check` fails while it is behind.
- Put the repository's own knowledge and skills under `.claude/knowledge/` and `.claude/skills/` at names the canon does not use; `sync` never touches them and indexes them as `(local)`.
- Never pass `sync --force` to get past a refusal: it overwrites a file you edited, one written before adopting, or one you improved that the canon retires. Read the files it names.
- Pin the package (`npx daoris@<version>`): the canon ships inside it, so the pin is the doctrine's version, and the manifest's `source` records it.
- A change another repository owns is a quest to it, never an edit from here. Fill in `domain` in `daoris.json` first: `daoris connect` refuses a repository without one.
<!-- agent-rules:end -->

[`llms.txt`](https://github.com/JiarongGu/Daoris/blob/main/llms.txt) indexes the adopter documents for an agent, and
[`context7.json`](https://github.com/JiarongGu/Daoris/blob/main/context7.json) configures the Context7 index; both
carry these rules.

## Learn more

- [The guide](https://github.com/JiarongGu/Daoris/blob/main/docs/guide/README.md): every command, the manifest, the layers of doctrine, the seams, and the service, platform and desktop beyond the CLI.
- [The desktop](https://github.com/JiarongGu/Daoris/blob/main/src/Daoris.Desktop/README.md): installing it, the driver, and what the window does.
- [The examples](https://github.com/JiarongGu/Daoris/blob/main/examples/README.md): an engine and a game adopting, registering and exchanging a quest, driven by `npm run rehearse:family` with no model in the gate.

## Developing Daoris

Use Node 24 and the .NET 10 SDK. [The contributor guide](https://github.com/JiarongGu/Daoris/blob/main/docs/development.md)
gives the setup, the focused checks, the rehearsals and integration; [AGENTS.md](https://github.com/JiarongGu/Daoris/blob/main/AGENTS.md)
is the brief every agent reads, [the document router](https://github.com/JiarongGu/Daoris/blob/main/docs/README.md) locates every
contract, and [TASKS.md](https://github.com/JiarongGu/Daoris/blob/main/TASKS.md) holds the open work. `npm run verify` checks the
CLI, the doctrine and the documentation; `daoris.gates.json` declares the component checks.

Daoris carries its own `daoris.json` and syncs the core into its own `.claude/`. A tool that cannot hold
its own doctrine cannot hold anyone else's.

[MIT](https://github.com/JiarongGu/Daoris/blob/main/LICENSE) © 2026 Jiarong Gu
