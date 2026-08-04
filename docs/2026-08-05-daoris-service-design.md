# Daoris.Service — design

**Status: design, nothing built.** Written before code so the shape is argued rather than discovered.
The contract for the CLI is `2026-08-04-daoris-design.md`; this is its knowledge layer.

---

## 1. What it is for

`sync` makes doctrine consistent across repositories. It does nothing about what each repository
*learned*: its decisions, its fix log, its task outcomes. Those are visible only from inside the
repository that holds them, which is how the same problem gets solved twice by the same person in two
directories — and how a rule gets rediscovered rather than reused.

The service is the query layer over all of it. It is **not** a place to author doctrine: improvements
still flow back through `upstream`, in the repository that found them, under review.

## 1a. Built by composition — which is the whole point of the family

Almost nothing here is a new primitive. The knowledge layer needs semantic memory, an embedder seam, a
vector store, provider routing and MCP hosting; it needs a desktop shell with a web surface and an IPC
bridge. **All of it already ships**, in two siblings built to be consumed.

So the build is mostly wiring:

| Need | Where it already exists |
|---|---|
| Embeddings, vector store, semantic recall | the cognition sibling |
| Provider routing, evaluation, MCP hosting | the cognition sibling |
| Desktop shell, WebView2 surface, IPC bridge, window state | the desktop sibling |
| Doctrine, canon, drift, the lock | this repository, already built |

**This is what makes the schedule plausible.** The expensive parts are done and versioned; what remains
is composition, and composition is where the family's design was supposed to pay off.

**And it runs in both directions.** Daoris is the first external consumer either sibling has had. A
library with no consumer is unvalidated — the same argument this project already makes about a pack
nobody installs, and about doctrine nobody runs. Building the knowledge layer therefore *tests* the two
siblings as much as it uses them, and every seam that turns out to be awkward is a finding for them
rather than a workaround here.

Two constraints survive that convenience:

- **The CLI composes nothing.** It stays Node, zero-dependency and offline (D1, D8, D10) — it must keep
  working in a repository that has no dependencies of its own. Only the service and its clients compose.
- **Consume released versions**, not sibling working trees. A dependency on an unreleased checkout is
  how three repositories become one repository with extra steps.

## 2. One service, two modes, chosen by configuration

**Local and shared are not two products.** They are one service with a storage seam and an auth seam,
and a consumer picks per deployment — the same shape the business-manager sibling already runs, where
the database provider is configuration and the default needs no database at all.

| | **Local** (default) | **Shared** (opt-in) |
|---|---|---|
| Runs | On the developer's machine | Somewhere the team can reach |
| Store | Embedded, single file | See §6 |
| Auth | None — the OS account is the boundary | Required. See §5 |
| Network | Never | Yes |
| Purpose | Recall across *your* repositories | Recall across *the team's* |

**Local is the default and must stay fully useful alone.** Most of the value — cross-repository recall
for one person working across a dozen checkouts — needs no server, no account, and no network. Anything
that only works in shared mode is a feature of sharing, not of the service.

**The mode is a setting, not a build.** One binary, one codebase; `Daoris:Mode` selects. A consumer who
starts local and later wants sharing changes configuration, not tooling.

## 3. What must not become true

Three properties the CLI has today, which the service must not quietly cost:

- **`check` never touches the network** (D8). The service is a separate process; the CLI must never
  gain a dependency on it, in either mode. A repository with no service configured behaves exactly as it
  does now.
- **Private stays private.** Several repositories in this family are private, and `sensitive-info`
  keeps machine paths and private project names out of *tracked* files. A service that indexes those
  repositories centralises precisely the material that rule exists to contain. See §4.
- **The canon has one source of truth.** The service reads the same `canon/` tree the CLI ships; it does
  not hold its own copy, and it never writes doctrine.

## 4. The disclosure boundary — the part that is specific to this project

Ordinary applications ask *who may read this*. This one must first ask **what may leave the machine at
all**, because the content is engineering knowledge from repositories with different visibility.

Three classes, and they are already how this family works:

| Class | Example | Local | Shared |
|---|---|---|---|
| **Canonical** | A core rule, a pack, the canon changelog | yes | yes — it is already public |
| **Repository** | A decision record, a fix log entry, a task outcome | yes | **only if that repository is shared with the reader** |
| **Private** | Anything under the untracked local notes — real paths, private project names, personal context | yes | **never** |

Two consequences worth deciding early rather than retrofitting:

- **Indexing is opt-in per repository**, not per service. A repository declares in its manifest whether
  its knowledge may be shared, and the default is no. Silence must mean "keep it local", because the
  cost of the wrong default is asymmetric: over-sharing is a disclosure and under-sharing is an
  inconvenience.
- **The untracked local directory is never indexed in shared mode, at any setting.** Not a permission —
  a hard exclusion, because it exists precisely to hold what must not travel.

## 5. Security model

Nothing here is needed for local mode, which is why local ships first. This is what shared mode requires
before it can exist.

**Follow the sibling's pattern, including its fail-safe.** Its `Auth:Mode` selects between an offline
development scheme and real OIDC, and the development scheme is honoured **only in the Development
environment** — a committed `Auth:Mode=Dev` cannot activate in a deployment. That inversion is the part
worth copying: the insecure mode is not merely discouraged, it is unreachable where it would matter.

- **Identity is delegated, never invented.** OIDC against whatever the team already uses. This project
  should not own passwords, and an IdP-agnostic validator means changing provider is configuration.
- **Authorization mirrors repository access.** The question "may this person read this repository's
  knowledge" already has an answer — the source host's permissions. Deriving from it means there is no
  second access-control model to drift out of step with the first. **Inventing a separate one is the
  mistake to avoid**, because it will disagree, and it will disagree silently.
- **Writes are narrow.** The service ingests and answers. It never edits doctrine, so a compromised
  instance leaks rather than corrupts — and the repositories remain the source of truth.
- **Transport is TLS; secrets live in the environment**, never in a tracked file (`sensitive-info`).

## 6. Storage

**Local: an embedded single-file store** with vectors alongside. No server, no container, no setup —
the same reasoning that makes the sibling's default need no database.

**Shared: prefer a git repository as the backing store**, before reaching for a database or a bucket.
It sounds unusual and it fits this project unusually well:

- It is **free**, and it is the substrate the family already runs on.
- It is **versioned and reviewable** — which is the argument `no-global-memory` already makes: a fact in
  a tracked file can be corrected by review and traced to the change that motivated it.
- **Access control already exists** and already matches §5's rule, because it *is* the repository
  permission model rather than a copy of it.
- It **survives the tool**. A knowledge store that outlives the service that built it is worth more than
  one that does not.

A hosted database earns its place only when query volume or vector search outgrows that — which is a
real possibility and a good problem, not a starting assumption.

## 7. LLM-assisted analysis and merge

The hard part of cross-repository knowledge is not storage or search; it is that **two repositories
describe the same lesson differently**, and neither knows about the other. That is measured, not
assumed: canonizing five skills from twelve repositories meant reading copies that shared a procedure
and almost no vocabulary, and `doctor`'s word-overlap detector provably cannot see convergence — same
principle, different words (D17). That is exactly the gap a model closes.

So the service proposes, and a person disposes:

- **Analyze** — surface that several repositories are circling the same lesson, including when the
  wording shares nothing. This is the job `doctor` structurally cannot do.
- **Merge** — draft a single statement of what they share, and name what is genuinely local to each.
  That is precisely the method used to canonize the existing skills, done by hand.
- **Never auto-promote.** A merge proposal is a draft for `upstream`, reviewed in a repository, by a
  person. Doctrine that appeared without anyone choosing it is the failure this whole project exists to
  prevent — and a rule nobody chose is one nobody will defend when it is inconvenient.

This is where a dependency on the cognition sibling becomes correct rather than premature: providers,
routing, semantic memory, the embedder seam and the vector store already ship there. D1 rejected that
dependency **for the CLI**, because a build gate must not depend on a release cadence it does not
control. A separate deployable has no such constraint.

**The model sees whatever is indexed**, so §4 governs it too — and more strictly, since a hosted model
is a third party. Local mode should be able to run against a local model for exactly this reason.

## 8. Open questions

1. **Does shared mode need hosting at all?** If the store is a git repository (§6) and the client is
   local, "shared" may be a sync rather than a server — no deployment, no account, no privacy boundary
   beyond the one that already exists. **Price this before building a host.**
2. **How does knowledge reach the index?** Pushed by a devkit gate, pulled from remotes, or read from
   local checkouts. Each has a different answer to §4, and the third needs no network at all.
3. **What is the query surface?** An MCP server a session can call directly is more useful to an agent
   than a web UI, and it is less to build. The UI may be the second client rather than the first.
4. **Where does the desktop shell sit** — a client of a remote service, or the host of the local one?
   If local-first wins, the shell *is* the product and there is no deployment.
