# Daoris (道衍) — Active Task Backlog

> **This file holds OPEN tasks only.** A finished task is **removed from here and appended to
> [`docs/task-archive.md`](docs/task-archive.md)** with its date and outcome — never ticked in place.
> `CHANGELOG.md` is the release-facing log; the archive is the per-task record. The contract is
> `docs/2026-08-04-daoris-design.md`; the forward sequence is [`ROADMAP.md`](ROADMAP.md).

**Goal:** one canonical set of agent-facing rules and knowledge, materialized into every repository in
the family, kept from drifting, and improved from wherever the improvement was found.

## State

All four artefacts exist; only `Daoris.Desktop` is a brief. Nine commands, 121 CLI tests, 81 service,
57 devkit, 52/52 release rehearsal (eight runs 2026-09-18), **22/22 family rehearsal** (2026-09-19),
8/8 devkit gates. Canon: 8 core rules, 5 knowledge documents, 5 skills, 6 packs. Always-loaded core is
**23,988 of 24,000 bytes** — 12 bytes of headroom, so the next canon addition fails the gate even as
an index row, and the answer is splitting, not raising (D28).

The service is **deployable** (D36): local sessions all spawn the MCP host over one persistent store
(`.mcp.json` here registers it as `daoris-knowledge`); the HTTP host carries registrations and quests
for a remote deployment, key-gated, **no model required**. `Daoris.Web` is **the platform** (D38,
D40): five views landing on **Overview** — is anything sitting, the family's health, quests grouped by
state with sitting time, projects as scannable declarations — with doctrine unwritable from every
view. The **example family** under `examples/` is the router's proof and the setup story (D39) — a
canon change must re-sync it in the same commit, and `npm run rehearse:family` enforces that.
Development is **automation-first** (D37): the person sets the target and verifies the final diff;
agents execute and gates verify the middle. `docs/FIX-LOG.md` carries fix root causes, indexed by the
service like every sibling's.

Nothing is published; development runs at `0.0.x`. **Adoption by other repositories is the owner's call
and happens when Daoris is ready** — it is not tracked here, and no repository is asked to adopt until
Daoris is finished. Lyntai stepped off the tool 2026-08-17 (owner-requested; the synced files remain
there as local forks), so the live consumer count is zero and Lyntai is not quest-addressable until it
re-adopts. The Shenora rehearsal keeps and does not expire: 6 collisions, 2 twins to retire, local
mechanics drafted at `docs/adoption/shenora-repo-mechanics.md`, budget 40,000, `check` clean at
38,782 bytes, with `web-webview` and `durable-jobs` ready for it.

## Backlog

- [ ] **REH1 — the release rehearsal intermittently reports 45/52.** Seen twice, **always exactly 7
  failures** — precisely the canon-upgrade phase's 7 checks, so a whole phase fails on a broken
  precondition rather than a flaky assertion. Both times it ran straight after canon files were edited
  and synced. **Every run now writes a transcript to `_fixtures/rehearsal-logs/` by construction**
  (2026-09-18), so the next failure is captured without anyone remembering to. Eight runs that day —
  several straight after canon edits and syncs, the suspected trigger — all passed 52/52. Stays
  open until a captured failure explains it. Do not tag a release while this is open.

- [ ] **LYN1 — the Lyntai pin, and the one-sweep migration.** `Daoris.Service` deliberately pins the
  cognition sibling at released 2.1.0 (D22): upstream has a renaming major sitting unreleased (its
  `Llm*` call types become `Text*`, and `Providers.Default` is already retired from its 3.x package
  roster), so the coordinated move is **one** migration when that major ships, not two. Lyntai is not
  quest-addressable (de-adopted), and no repository is asked to adopt until Daoris is finished — so
  this is held here, not delivered anywhere.

- [ ] **SVC2 — remote hardening, deferred until a deployment leaves a trusted network.** Per-person
  expiring keys and OIDC per the service design §5, and an MCP relay from the local stdio host to a
  remote service when a second machine actually exists. D36 records why neither is built now.

- [ ] **CANON2 — `desktop-winforms`, the last pack candidate.** One 11 KB source, one repository —
  below the two-repository bar, which is the whole reason the canon is trustworthy. Leave it local until
  a second repository needs the same thing.

- [ ] **HARNESS1 — a second harness layout.** `src/harness.ts` holds the signals and contract checks; a
  second implementation slots in beside the Claude one. Do not start until a repository actually wants
  it — the layout, always-loaded semantics and trigger mechanism all differ, and guessing produces
  doctrine nobody chose in a format nobody verified.

## How to work a task

- **TDD:** failing test → run it fail → minimal implementation → run it pass → commit.
- **Commit per task, automatically, once gates are green** (D37 as amended). Push, publish, release
  and history rewrites stay the owner's call.
- **`npm run verify` before claiming done.**
- **A canon file is project-agnostic** — the principle and the reason, never the mechanism. See
  `.claude/knowledge/canon-authoring.md`.
- **When a task completes, move it to `docs/task-archive.md`** with the date and outcome.
