# PROFILEFIX1: a profile for a repository's own facts — the Opus maker's proposal

**Standing:** a maker's proposal under D191, not a decision. The steward reads it with another maker and decides; nothing is
published to Lyntai. Read at Daoris `9c7cda2c6`, Lyntai `1fd45734`, Daoris.Plugins `00ec0cf`.

**Read.** Lyntai's `docs/vnext-modular-capabilities.md` §6 to §8, its `TASKS.md` Part 427 (VP3), `docs/task-archive.md` Part 428
(VP1, VP2), D221 in `docs/DECISIONS.md`, and the format's code under `tests/Lyntai.Tests/Memory/Prototype/` (`ProfileFixture.cs`,
`ProfileFixtureReplay.cs`, `ProfileOverCurated.cs`, `ProfileModel.cs`, both `Fixtures/*.profile.json`). Daoris's D220 with its notes,
`docs/2026-10-31-memory-coordinator-design.md`, `daoris.json`, `daoris.gates.json`, `daoris.lanes.json`, the registry's
`Registration`, `DriverConfig` (`Lines`, `Landings`, `IntegrationGates`), `CanonicalLine`, the memory under `Knowledge/Memory/`, and
the git history of the three repositories' declarations.

## The answer in short

**No, for these facts, today.** A repository's stack, gates, lanes, documents and domain already have one home each, a declaration
file whose history is git's; its line, landing and accepted gates are the person's settings on the machine, ordered by a rule in
code; its settled choices are decisions. The coordinator cites records and never ingests them, so a profile of these facts would be
a second home for values a check already keeps true. Their real history holds two of the six event kinds a fixture needs. Daoris
is not VP3's first application; the condition that would make it one is in §7.

## 1. The facts, field by field

A field id is Daoris's (Lyntai's D217). "Owner" maps to the format's `user` and "inferred" to `hypothesis`; a declaration is
committed by a session and stands once landed under the owner's grant.

| Field id | Scope | Who states it | Home today | As a field |
|---|---|---|---|---|
| `domain.summary` | none | owner | `daoris.json` | single: fits |
| `domain.owns`, `domain.accepts`, `domain.uses`, `packs` | none | owner | `daoris.json` | list |
| `doctrine.target`, `doctrine.source` | none | owner | `daoris.json` | single: fits |
| `budget.words` | document role | owner | `daoris.json` `documents` | single per role: fits |
| `document.path` | document role | owner | `daoris.json` `documents` | single per role: fits |
| `gate.run`, `gate.clock` | gate name | owner, accepted whole by blob | `daoris.gates.json` | single per gate; a gate dropped has no verb |
| `lane.title`, `lane.summary` | lane id | owner | `daoris.lanes.json` | single per lane; a lane renamed is a scope rename |
| `lane.paths` | lane id | owner | `daoris.lanes.json` | list |
| `stack.dotnet`, `stack.node` | component lane | owner; a setup session infers | project files, `package.json` `engines`, the brief | single per lane: fits |
| `line` | none | person; workspace's; checkout's guess (inferred) | `DriverConfig.Lines`, `WorkspaceLines`, `origin/HEAD` | three sources, two subjects |
| `landing` | none | person; workspace's; the merge door | `DriverConfig.Landings`, `WorkspaceLandings` | three sources, two subjects |
| `integration.accepted` | none | person | `DriverConfig.IntegrationGates`, a blob id | a digest of a whole file |
| a settled choice | none | owner | a decision, a backlog direction | prose: curated with an end |

## 2. What stays curated, and why

- **Settled choices** are prose whose replacement is a later decision, ended by its `Superseded by D<n>` mark (KNOWFRESH1a). Lyntai's
  own §8 calls this curated memory with an end (its D215). The coordinator cites them (design §5).
- **What sessions learn** (a trap, a flake, a cost) is free text with citations: the coordinator's entries, as built.
- **The declared facts are neither.** The coordinator design cites a record and never ingests it (§0; §5, "Rejected. Ingesting
  records."), and D220 gives a present fact one home, preferring one a check keeps true. A claim holding `gate.run@universal` is
  a copy of a line in `daoris.gates.json`.

## 3. The reads, and what answers them now

- **Current.** A session's brief reads its tree's files: `ContextBriefs` takes the calls `daoris.gates.json` declares as checks;
  `CanonicalLine` answers the line and says which source answered.
- **At a time.** `git show <commit>:daoris.gates.json`; a verify result keeps the blob it ran under (`VerifyResults`).
- **History.** `git log -p` over the declaration.
- **Rivals.** The line's committed declaration against the blob the person accepted (`IntegrationGates`, INTEGRATE1h's declaration
  owed acceptance); the person's line against the checkout's guess, in `CanonicalLine`'s order.
- **The whole subject.** The registry's row (summary, owns, accepts, packs, uses and lanes as `daoris connect` read them; the line
  the driver declared) and the files at the line's tip.

Each read is answered exactly by the home itself. A profile would answer the same reads from a copy, and the copy needs a writer.

## 4. What the history shows

From git: Daoris's declarations since 2026-08-04, Lyntai's since it adopted (2026-10-24, `df762879`, `9afece81`), Daoris.Plugins'
since its setup.

- **First statements**, in all three. Daoris.Plugins' manifest was written once (`9fb6069`, 2026-10-01) and has not changed.
- **Corrections**, in Daoris: `coreBudgetBytes` 24000 to 26000 (`3fc23ebb5`, 2026-09-21); `documents.decisions` from
  `docs/DECISIONS.md` to `docs/decisions` (`ee2654a94`, 2026-10-03); the `universal` gate's command wrapped in `as-merged.mjs`
  (`bc373b749`, 2026-10-07).
- **A field changed while the rest stand**: each of those, and the `web` gate narrowed to its end-to-end run beside a new
  `web-unit` (`e0a1bdd57`, 2026-10-18).
- **A renamed field**: none to replay. `documents.router`, a path, became `{path, words}` (`6ffc0e2f3`, 2026-10-10), and both
  shapes stand in the file today (`index` a path, `router` an object), each read as it is.
- **A disputed value**: none in a file, which holds one value per key at a commit. Daoris's disagreements are between homes,
  ordered in code: the line, and a declaration newer than the accepted one (Lyntai declared its integration on 2026-10-31,
  `f2dbacea`; its acceptance is the owner's, on this machine).
- **A forget**: none. No declaration was withdrawn in git; a retirement is the registry's (`Registry.Retire`), not an event of the
  facts.

An honest fixture holds `set` and `correct` steps only. The other four kinds would be invented, and a fixture of two verbs shows
the format, which `person-reference` already does.

## 5. Where it does not fit

- **Lists.** Owns, accepts, uses, packs and lane paths are one claim per member, but a member dropped has no verb: `forget` takes the
  whole subject.
- **Scope renames.** A lane renamed moves a scope, and `rename` moves a field.
- **Three sources and a second subject.** The line and the landing are read through the repository's setting, then the workspace's,
  then a default; the format holds two authorities and one subject per file (Lyntai's §8, what the format cannot say).
- **Whole acceptance.** The owner accepts a gate declaration by its blob (D215 point 2, the integration design §4.1), never field
  by field; a claim per gate would ask the person to accept each.
- **Readers with no store between.** The driver reads its line and gates at each start and merge, and the CLI's doctrine commands
  stay offline. A profile would put the service's memory under the driver's settings.

## 6. The cost of being first

- **A second memory per repository.** The profile projects over a revisioned curated store; Daoris's memory is Lyntai's graph
  engine, one per repository (the design's §1, KNOWCENTRAL2d).
- **A twin.** Field ids held to three declaration files and their readers (`connect.ts`, `lanes.ts`, `RegistryImport`,
  `HeavyGates`), every shape change made in both.
- **A writer at each commit point** projecting a landing's declaration diff into claims with the two-write recovery, and an import
  of each repository's history.
- **No contract to adopt.** The profile is test-only, Lyntai's D221 forbids publishing one to be tried, and the package waits on a second
  application of a different kind that nobody in the family has named. Daoris would build toward a contract that may not ship.
- **What it buys**: reads that git, the registry and `CanonicalLine` already answer.

## 7. Recommendation

**No.** The declared facts stay in their files and are cited; settled choices stay decisions; what sessions learn stays curated
entries. No fixture is written. VP3's answer, for the steward to send once decided: Daoris's repository facts are declarations
with one home and git's history; the coordinator cites them; the format fits them with two of its six kinds of event; no change
to the contract is asked.

**Re-opens** when a single-valued repository fact has no declared home, is stated by both the person and sessions, and is read
exactly by the coordinator: a repository that never adopts, whose line, stack and test command sessions infer at setup and the
person corrects; or KNOWCENTRAL2i's installed measurement finding sessions writing one named fact again and again in entries that
disagree. The subset is then those fields, the fixture is drawn from their real events, and Daoris counts as an application for
them.

**Not verified here**: the person's line and landing for each repository (the install's settings, not read), and whether sessions
write repository facts as memory entries (KNOWCENTRAL2i has not run).
