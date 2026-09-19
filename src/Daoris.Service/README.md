# Daoris.Service — the cross-repository knowledge service

**Status: deployable.** An MCP server over stdio exposes the index to any agent session, and an HTTP
host carries the same service for a browser or a remote deployment. The core reads a repository's
knowledge into addressable entries, classifies each as canonical or local, stores them in SQLite,
answers ranked queries over FTS5, and finds where repositories learned the same lesson independently.
Quests and pushed registrations persist in the same database, so what one session publishes another
session — or another machine's `connect` — finds waiting. **81 tests**, two of which run against the
real sibling repositories rather than fixtures.

## The registry — who is out there, and what they own

Each repository declares a `domain` in its manifest: a one-line summary, the areas it **owns**, and the
kinds of quest it **accepts**. The service reads those while indexing and serves them together.

**Search answers "has anyone solved this"; the registry answers "whose problem is this."** Those are
different questions, and only the second tells you where a change belongs — which is what makes a quest
addressable rather than a guess.

Declared in the manifest rather than configured here, so it sits next to the thing it describes and is
reviewed by the people it describes. A central list would drift the moment a repository changed and
nobody remembered to update the server.

Adoption gates addressing; declaring does not. A repository that has adopted but said nothing is still
reachable — the asker is simply warned it may not be that repository's problem. Repositories that have
not adopted are **listed and marked**, because "who cannot be asked yet" is the same question as "who
can", and a silent omission reads as the repository not existing.

| Tool | What it answers |
|---|---|
| `registry` | Who is in the family, what each owns, what each accepts, who is not addressable |

## Quests — work one repository asks of another

Repositories in this family are not developed across (`docs/DECISIONS.md` D32). A change one needs from
another is a **quest**: published here, and *pulled* by the repository it is addressed to.

**It is held by the service, not written into anyone's files.** The first version of this wrote the
quest straight into the receiving repository's backlog, which is the same trespass in a smaller form —
an outside edit is still an outside edit when it is one file and uncommitted, and it still arrives from
whoever knows that codebase least. It was also incompatible with D8: reaching a central store means the
network, and nothing in the CLI may open a socket. So the CLI has no quest command, and this does.

| Tool | What it does |
|---|---|
| `quest_publish` | Ask another repository for something. Refuses a repository that has not adopted |
| `quest_list` | What has been asked of whom, and what is still outstanding |
| `quest_respond` | `take`, `done` or `decline` — declining needs a reason |

The judgement behind those — who may be addressed, what a refusal says, what declining requires —
lives in one place, `QuestExchange`, shared by the MCP and HTTP hosts. Written per host it would
drift, and the same ask would be deliverable through one door and refused at the other, which for a
quest system is the worst available bug: it looks like the sibling ignoring you.

Four states, because anything finer is status for its own sake. A quest is **taken**, not assigned,
which is the property that keeps declining a real answer. **Only an adopted repository can be
addressed**, because one without the client cannot see the quest — and an unread quest looks exactly
like an ignored one.

Stored beside the index in the same database: quests are service state as the index is, and two files
would be two things to back up and two that can disagree about which repositories exist.

**The semantic pass has been proven on a real pair.** Two repositories derived the same principle
independently and wrote it in different vocabulary; word overlap scores them at **25%**, below the
duplicate threshold, so the CLI's `doctor` structurally cannot see them. Indexed here with a local
embedding endpoint, convergence detection reports exactly that pair at **0.785** — and discriminates,
returning nothing at a 0.82 threshold and pulling in an unrelated document at 0.60. That is the whole
argument for this artefact existing, measured rather than asserted (`docs/DECISIONS.md` D17, D24).

Indexing the whole family takes **~500 ms for 408 entries** into a 7 MB database; queries answer in
**3–10 ms**.

## Running it

**Local — the default, and no daemon.** The MCP host is spawned by each agent session and exits with
it; the **database** is what persists. Every session in every repository on this machine spawns over
the same file (`~/.daoris/knowledge.db`), which is how a quest published from one repository's session
is waiting when another repository's session starts. This repository's own `.mcp.json` registers it as
`daoris-knowledge`; a sibling adds the same entry to its own `.mcp.json` — that file is the sibling's
to write — with an absolute `--project` path:

```json
{ "mcpServers": { "daoris-knowledge": {
    "command": "dotnet",
    "args": ["run", "--project", "<path-to-daoris>/src/Daoris.Service/Daoris.Service.Mcp"] } } }
```

`dotnet run` re-checks the build on each session start. The production shape is the **published
executable** (D43):

```sh
npm run publish:service -- --install    # both hosts → ~/.daoris/bin, self-contained single-file
```

`--install` prints the ready `.mcp.json` snippet with the family root already filled in — a published
binary has no workspace above it to walk to, so the root must be **named**; run without it, the host
says so on stderr rather than silently indexing whatever directory spawned it. From a source checkout
the walk-up still lands on the right folder wherever the client spawned it. The release workflow ships
the same binaries per platform, each with a sha256 beside it (D27's shape): `daoris-knowledge-<rid>`
bare, and `daoris-knowledge-http-<rid>.tar.gz` carrying its web bundle beside the executable.

| Tool | Answers |
|---|---|
| `knowledge_search` | What has this family already learned about X? |
| `knowledge_get` | The full text of one entry |
| `knowledge_repositories` | What is searchable, and how much each repository contributes |
| `knowledge_convergence` | Which repositories learned the same lesson independently? |
| `knowledge_refresh` | Re-read every repository from disk — and retire what is no longer there: a repository renamed or removed leaves the index instead of being served forever (guarded: a scan that saw nothing retires nothing, because that is a mis-set root, not an emptied family) |

**Remote — transfer of request and task, opt-in.** The HTTP host is the deployable half. It runs with
**no model at all** (D24) and still carries what a remote deployment exists to carry: registrations
pushed by `daoris connect` — persisted, because for a remote service the pushed registrations *are*
the family — and quests, published and answered over the same `QuestExchange` the MCP host uses. A
repository's knowledge travels only if that repository opts in (D21); moving work never required
moving knowledge.

```sh
dotnet run --project src/Daoris.Service/Daoris.Service.Http     # http://localhost:5177
```

| Endpoint | |
|---|---|
| `GET /api/status` · `/api/search` · `/api/entry` · `/api/entries` · `/api/convergence` · `/api/repositories` | the read surface, same as the UI's |
| `GET /api/registry` · `POST /api/registry` | who is out there; where `daoris connect` lands |
| `GET /api/quests` · `POST /api/quests` · `POST /api/quests/{id}/respond` | the pull side; publish; take / done / decline |
| `GET /api/sessions` · `POST /api/sessions` · `POST /api/sessions/{id}/state` | the driver's session records (D46) |
| `POST /api/refresh` | local mode only: re-scan whatever repositories the host can see |
| `POST /api/feed/sessions` · `/api/feed/entries` | shared mode only: what a desktop's sync feeds up (D47) |
| `POST /api/feed/quests` | local mode only: the quest mirror a sync loop pulls down (D47) |

There are exactly two trust shapes (D47 §7, as amended). **Local** — the default — trusts the
loopback: the OS account is the boundary (D21), and the host refuses to start bound anywhere else.
**Shared** (`DAORIS_MODE=shared`) is the team deployment: every route under `/api` needs a minted
per-person per-machine key as a bearer token, no page is served, no machine path is ever answered, and
keys are administered on the binary itself — `keys mint --name <person@machine> [--days N]`,
`keys list`, `keys revoke <prefix>`. The key is shown once and stored hashed; the prefix is the
non-secret audit handle. A machine names its remote in `~/.daoris/remote.json`
(`{ "url": ..., "key": ... }`, `DAORIS_REMOTE_URL`/`DAORIS_REMOTE_KEY` overriding), and the desktop's
sync loop does the rest.

`ConvergenceDetector` answers a different question: **which repositories learned the same thing
independently?** It automates the survey that produced this project's own canon — reading twelve
repositories by hand to notice which documents said the same thing in different words. It proposes
candidates; a person decides, through `upstream`, under review.

Configuration is by environment, and every variable is optional — the defaults are the local mode:

| | |
|---|---|
| `DAORIS_KNOWLEDGE_ROOT` | Where the repositories are. Default: the folder containing this workspace |
| `DAORIS_KNOWLEDGE_DB` | Where the index lives. Default: `~/.daoris/knowledge.db` |
| `DAORIS_EMBED_MODEL` | Names an embedding model to **enable semantic search**. Unset = lexical only |
| `DAORIS_EMBED_URL` | Embedding endpoint. Default: `http://localhost:11434` (Ollama) |
| `DAORIS_MODE` | HTTP host only: `local` (default) or `shared` — the team deployment (D47) |
| `DAORIS_REMOTE_URL` / `DAORIS_REMOTE_KEY` | this machine's remote, overriding `~/.daoris/remote.json` |

Verified end to end against the real family with `nomic-embed-text`: **409 entries embedded in 34 s**,
and a query whose words appear in none of the matching documents — *"stop the console from stealing
focus during a capture"* — returned three desktop-capture documents from three different repositories.

Semantic recall is opt-in and never required. Naming a model turns it on and hybrid fuses it with the
lexical index; leaving it unset keeps the service lexical-only rather than half-configured, because an
index that will not start without an embedding endpoint is not local-first. If the endpoint is
unreachable or misconfigured, the refresh still completes and reports the reason — verified against a
local server started without `--embeddings`, which is what the failure actually looks like.

```sh
cd src/Daoris.Service && dotnet test
```

## What it finds today

Scanned across the family, 2026-08-05:

| Kind | Local | Canonical |
|---|---:|---:|
| Rule | 101 | 15 |
| Skill | 96 | 5 |
| Knowledge | 62 | 2 |
| Decision | 58 | 0 |
| Task outcome | 53 | 0 |
| Fix | 13 | 0 |
| **Total** | **383** | **22** |

**405 entries across 11 repositories, and 94% of them are local** — which is the premise of the whole
index, measured rather than assumed. Canonical content is identical in every repository that installs
it, so indexing it per repository would produce a dozen copies of one rule and call that a corpus. The
local material is what varies, and 124 of those entries are decisions, fixes and task outcomes that no
sibling repository can currently reach at all.

## The seams

Four extension points, each with one job, so the pieces that are still undecided can be swapped
without touching the ones that are not.

| Seam | Today | Later |
|---|---|---|
| `IKnowledgeSource` | The local filesystem | A git remote, or a devkit gate that pushes |
| `IKnowledgeStore` | SQLite file, or in memory for tests | A hosted store only if volume ever demands one |
| `IKnowledgeSearch` | FTS5 + BM25, semantic, and hybrid fusing both | Provider routing, so a deployment picks its own model |
| `IDisclosurePolicy` | `LocalOnly` — nothing leaves | `Sharing(repositories)` — opt-in per repository |
| `IVectorProvider` (the sibling's) | Any OpenAI-compatible or Ollama endpoint | Chosen by deployment, never by the feature (D24) |

Two choices worth knowing about:

- **Search returns scored hits, not a list.** Scores are what let two searches be merged, so hybrid
  is a composition rather than a third implementation.
- **Hybrid fuses on rank, not on score.** BM25 returns an unbounded figure and cosine similarity a
  number in [-1, 1]; adding them compares quantities that mean different things, and whichever has the
  larger range silently wins. Reciprocal rank fusion uses only each result's position in its own list.
- **Semantic search is optional and degrades.** The embedder is app-provided, so with none configured
  the service is lexical-only and local mode still works with nothing installed. If either half fails
  the other still answers — an index that returns nothing because an endpoint is down is worse than one
  that returns half of what it knows.
- **The disclosure policy is applied at ingest, not at query.** Withheld-at-query means the material
  is in the store and one forgotten filter discloses it; withheld-at-ingest means it was never there
  to leak. It is a *type* rather than a paragraph so that shared mode cannot be built without
  answering it.

## What it is for

A session in any repository can read that repository's doctrine, because `sync` put it on disk. It
cannot read what the *other* repositories learned. Every decision record, fix log and task outcome in
the family is invisible from anywhere but the repository that holds it — which is how the same problem
gets solved twice by the same person in two directories.

The service is the query layer over all of it: doctrine, decisions, and past task outcomes, across every
adopting repository.

## Why it comes after the canon, not before

Indexing content that is still divergent indexes the divergence. Six copies of a rule that disagree
produce six answers with no way to tell which is current — so the canon has to exist first, which it now
does. This is also why a generated wiki is a complement rather than a competitor (D16): a wiki is
derived from code and fails by going stale; doctrine is authored because something went wrong and fails
by diverging. The service indexes the second kind.

## Shape

- **ASP.NET Core**, so it can compose the family's existing LLM work rather than rebuild it — semantic
  memory, the embedder seam, the vector store and MCP hosting all already ship in the cognition sibling.
  That dependency becomes correct here precisely because this is a separate deployable; the CLI keeps
  its zero dependencies and never learns about this.
- **The canon is an input, not a copy.** `canon/` at the workspace root is the same tree the CLI
  materializes; the service reads it rather than holding its own.
- **Two clients, one UI** — see `Daoris.Web` and `Daoris.Desktop`.

## The design is written

**`docs/2026-08-05-knowledge-service-design.md`** — read it before writing code. It settles:

- **Local-first, sharing as configuration** (D21). One service, two modes, one binary; local needs no
  server, no account and no network, and must stay fully useful alone.
- **The disclosure boundary** — what may leave a machine at all, which is the question this project has
  to answer before "who may read it". Indexing is opt-in per repository, silence means keep it local,
  and the untracked local directory is a hard exclusion rather than a permission.
- **Authorization mirrors repository access** rather than inventing a second model that would eventually
  disagree with the first, silently.
- **A git repository as the shared store**, before a database: free, versioned, reviewable, and its
  access control already *is* the rule above rather than a copy of it.
- **LLM-assisted merge proposes; a person disposes.** Doctrine that appeared without anyone choosing it
  is the failure this whole project exists to prevent.
- **Built by composition** (D22) — the cognition sibling supplies embeddings, the vector store, routing
  and MCP hosting; the desktop sibling supplies the shell. Released versions only, never working trees.

The sharpest open question is still the first one: **does shared mode need hosting at all?** If the store
is a git repository and the client is local, "shared" may be a sync rather than a server.
