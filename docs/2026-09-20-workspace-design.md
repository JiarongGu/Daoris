# Workspaces — design

> Written 2026-09-20, from the owner's direction set in the backlog the same day: repositories belong
> to **workspaces**, knowledge sharing is workspace-level, a machine can sync different workspaces to
> different Daoris servers, remote knowledge needs real add/update/delete semantics, and the desktop
> manages a workspace's repositories. Designed under the owner's standing redesign grant — nothing is
> deployed, so structure may change to fit rather than accrete. Read with
> `docs/2026-08-05-knowledge-service-design.md` (§4 disclosure, §5 identity — this narrows both),
> `docs/2026-09-20-remote-design.md` (the remote this multiplies), and D48 in `docs/DECISIONS.md`
> (the direction). The companion is `docs/2026-09-20-interactive-design.md`.

## 1. What it is for

Today the sharing boundary is an accident of layout: "the family" is whatever folder
`DAORIS_KNOWLEDGE_ROOT` points at, one flat registry, one optional remote per machine. That was right
for one person with one folder of checkouts. It is wrong the moment one machine holds repositories
that belong to different circles — a game family and a tools family, work and personal — because
knowledge, quests and registrations would cross a boundary nobody drew. The **workspace** is that
boundary, drawn deliberately: **the workspace is the unit of sharing.** Everything that crosses
repositories — search, convergence, the registry, quests, session records, and every remote — is
scoped to one workspace. Everything inside one repository is unchanged.

## 2. Where a workspace is declared: the manifest

`daoris.json` gains one field:

```json
{ "workspace": "aurora" }
```

- **Absence means the default workspace**, normalized to the name `default` at read — the same
  silence-is-the-safe-default rule as `remote` (D21), and what makes every existing manifest already
  correct. A machine that never declares workspaces behaves exactly as today: one family, one scope.
- **The manifest, not a machine-local mapping**, for the `no-global-memory` reason: membership is a
  fact about the repository — every clone of it, every teammate — and a fact in a tracked, reviewed
  file can be corrected by review and traced to the change that motivated it. A machine-local mapping
  would let two machines file the same repository under two workspaces and never notice.
- **Not a folder-level workspace file**, because the owner's own framing kills it: "some repo might
  belong to different workspace" — membership does not follow disk layout, and a design that assumes
  co-located checkouts breaks on the first repository that lives elsewhere.
- `connect` carries `workspace` in the registration exactly as it carries `domain` and the remote
  declarations. `status` reports it. Nouns only, like everything in the manifest (D26).

**A repository belongs to exactly one workspace.** A repository that genuinely serves two circles is a
repository whose domain wants splitting — the same answer the driver gives to parallel sessions in one
tree. (Cross-workspace *asking* is deliberately out of scope; see §10.)

## 3. The registry becomes managed; the scan becomes an import

Today the family is discovered by scanning a root folder, and the registry is partly derived from that
scan. The ghost-repository fix (FIX-LOG 2026-09-19) already showed scan-as-authority failing: what the
scan does not say governs as much as what it says. Workspaces finish the argument, because one root
folder cannot express "these repositories, in these workspaces, wherever they live."

- **The registry is the authority: an explicit list of repositories** — name, workspace, declaration,
  and (machine-locally) the checkout path — held in the store, edited through the service's doors.
- **The folder scan becomes `import`**: a bootstrap that proposes registrations from a folder's
  subdirectories, applied deliberately. `DAORIS_KNOWLEDGE_ROOT` survives as the import default and as
  back-compat for a store that has never been managed (first run imports it, once, and says so).
- **Adding** a repository = registering it (the desktop's add flow, or `daoris connect` from inside
  it, exactly as today). **Removing** = retiring the registration — the repository's files are never
  touched, and its knowledge leaves the index on the next refresh, by the ghost rule. **Updating** =
  a new registration upsert, as `connect` already behaves.
- The refresh/index pipeline reads the registry's paths instead of enumerating a folder. A registered
  path that no longer exists is reported as such — a named absence, never a silent ghost.

## 4. Scoping: what a workspace bounds

Every cross-repository entity carries its workspace: registrations, knowledge entries, quests, session
records. One local store still holds them all — the machine is the person's, and local mode trusts the
person (D21) — but **every cross-repository answer is scoped**:

- **Search, convergence, the registry list**: scoped to one workspace per query. The platform gains a
  workspace switcher (one more filter, not a new view); the MCP tools gain an optional `workspace`
  argument. The default scope is **the workspace of the repository the session runs in**, resolved
  from the working directory's manifest — an agent session asking "has anyone solved this" means its
  own circle, not every circle the machine can see. With no ambient repository and no argument, the
  answer states the workspaces it spans rather than silently mixing them (the D24 shape: report the
  scope that ran).
- **Quests are intra-workspace.** Publishing checks that `from` and `to` share a workspace, and the
  refusal names both sides' workspaces. Addressability already gates on adoption (D33/D34); this adds
  one more clause to the same judgement in `QuestExchange`, where both hosts share it.
- **The driver is per machine, across workspaces.** Drivable/holds stay per repository in
  `driver.json` (D46 §2 — the person's machine-local call, not workspace doctrine). One loop plans
  over everything drivable on the machine; the workspace decides where its quests and records
  *travel*, not which machine may work them.

## 5. Multi-server: one shared deployment serves one workspace

**A shared host is a workspace's host.** It is configured with its identity —
`DAORIS_WORKSPACE=aurora` — and refuses a registration or feed that declares any other workspace,
plainly, naming both. Rejected: one multi-tenant server holding many workspaces — it puts the sharing
boundary *inside* one store and one key space, which is exactly where a scoping bug becomes a
disclosure; a second workspace is a second process over a second SQLite file, which the self-contained
host (D43) makes as cheap as a config file. If a real team ever runs ten workspaces on one box, that
is a reverse-proxy problem, not a store problem.

**The machine's remotes become a map.** `~/.daoris/remote.json` (one remote per machine) becomes
`~/.daoris/remotes.json`:

```json
{ "aurora": { "url": "https://…", "key": "dk_…" }, "tools": { "url": "…", "key": "dk_…" } }
```

- Machine-local and untracked, exactly as today, because the key is per person per machine (D47 §7).
  The workspace **name** keys the map; whether a given repository may feed remains its own manifest's
  `remote` declaration — the two layers stay separate: *the manifest says may, the machine says where.*
- The env trio (`DAORIS_REMOTE_URL`/`_KEY`/`_CONFIG`) survives for the one-workspace case and the
  rehearsal's hermetic guard, plus `DAORIS_REMOTE_WORKSPACE` naming which workspace the pair serves
  (absent: `default`). The whole-pair-or-nothing rule is unchanged, and both twins (`RemoteConfig`,
  `RemoteTarget`) move together with their test tables.
- **The sync loop runs per workspace**: for each workspace with a remote, feed that workspace's joined
  repositories up and mirror that workspace's quests and foreign registrations down. The quest relay
  resolves its remote by the quest's workspace. A workspace with no remote entry syncs nowhere,
  silently — absence is the default (D21).

## 6. Remote knowledge sync: add, update, delete — and who is right

Today's feed is wholesale replacement per repository, last writer wins. Between two machines that is a
flapping generator: each tick, whichever machine feeds last overwrites the other's view, and a stale
checkout can clobber a fresh one. The owner's framing — knowledge differs by commit, PR, timing — is
the real problem: **two checkouts of one repository are two points in its history**, and the remote
must decide which one speaks.

The rule, in three parts, all enforced at the remote's door (in Core, where the entries door already
judges):

1. **Only the canonical line feeds knowledge.** The feed carries provenance the driver stamps from
   git — `{ commit, committedAt, branch }` (`WorkingTree` already reads HEAD) — and the remote takes
   knowledge only from a checkout on the repository's **default branch**. A feature-branch checkout is
   work in flight: its session records and quests still travel (they are records of activity, not
   claims of truth), but its knowledge does not — unmerged lessons are not yet the family's. The
   default branch name rides the registration (the checkout knows it; the remote cannot ask git).
2. **Replacement is monotonic by commit time.** The remote stores `{ commit, committedAt, origin }`
   per repository beside its entries and refuses a feed whose `committedAt` is older than what it
   holds — answered plainly ("`aurora-engine` is already fed from a newer commit"), reported by the
   sync as information, not a problem. Same commit re-feeds are idempotent, as today. This keeps
   *update* and *delete* correct for free: the newest canonical checkout's view replaces wholesale,
   and an entry absent from it is deleted — a repository that deleted its knowledge means the deletion
   (the existing rule, now protected from stale writers).
3. **Provenance is served, not implied.** `GET /api/repositories` on a shared host answers each
   repository's fed commit, time and origin, and the platform shows it — staleness a person can see
   beats freshness they must assume. This is `claims-need-checks` applied to the index itself: the
   remote's copy is a *claim about a commit*, so it names the commit.

Rejected: **per-entry merge** — entries are derived data (D-store: "the index is derived; the
repositories are the truth"), and merging two machines' derivations invents a second source of truth
that git already is. Rejected: **wall-clock last-writer-wins** — the failure mode is the exact one
being fixed. Rejected: **feeding branch knowledge under a branch label** — it doubles the store's
shape for material whose home (the branch, the PR) already displays it better.

Local mode is untouched: a local index still scans its own checkouts, whatever branch they sit on —
the person's machine shows the person's state.

## 7. The desktop manages the workspace's repositories

The management surface the owner asked for, landing in the platform's Projects view **where a shell's
driver is attached** — the same gate as every control, because managing repositories means touching
machine paths (D46/D47: paths never reach a browser).

- **Add**: pick a folder → the shell validates (a manifest? offer `init`'s proposal; a git repo?) →
  registers it with workspace + declaration, through the same door `connect` uses. Adoption itself —
  sync, collisions, the review — remains the repository's own agent's job (`adoption.md`); adding to
  the workspace is registration, not adoption.
- **Update**: edit the declaration (domain, workspace, remote flags) → the shell writes `daoris.json`
  **in the repository** and re-registers. This is the person editing their own tracked file through a
  form instead of a text editor; the diff lands uncommitted for the repository's own review flow, and
  doctrine (rules/knowledge/skills) stays unwritable from every surface (D31) — the manifest is inert
  data (D26), not doctrine.
- **Remove**: retire the registration, with the sentence saying what it does **not** do — no files
  deleted, no history touched; the repository simply stops being addressable and indexed here.
- A browser over a keyed remote sees the workspace's registry read-only, as it sees everything.

## 8. What changes where

| Layer | Change |
|---|---|
| CLI (`types.ts`, `config.ts`, `connect.ts`, `commands.ts`) | `workspace` manifest field, normalized at read; carried by `connect`; reported by `status`; `init` scaffolds it. Nothing else — every doctrine command is per-repository and untouched |
| Service Core | `Workspace` on registration/entry/quest/session; the registry becomes the managed list (+paths machine-local); scan → import; quest exchange gains the same-workspace clause; the feed doors gain provenance + monotonic + default-branch judgement; schema version bumps and rebuilds (no migrations, by the store's own rule — and nothing is deployed) |
| Hosts | `DAORIS_WORKSPACE` identity on the shared host, refusals naming workspaces; `/api/repositories` provenance; MCP tools gain `workspace` args with ambient default |
| Driver / Desktop | `RemoteTarget` → the remotes map (env pair kept for one workspace); sync loop per workspace; feed stamps git provenance; the shell's repo management (add/update/remove) over the loopback host |
| Web | Workspace switcher on the cross-repo views; provenance on Projects; management forms (desktop-attached only) |
| Rehearsal | A workspace phase: two workspaces on one machine — scoping asserted (a search and a quest refused across the boundary, with the sentence), two remotes fed disjointly; the knowledge-sync phase: a stale feed refused, a branch feed refused, a newer feed replacing — all with the decoy-style negative checks |

## 9. Build order (the backlog's WSP items)

1. **WSP1 — the workspace exists**: manifest field end to end, every entity carries it, scoping of
   search/registry/quests, the rehearsal's two-workspace assertions. Everything else stands on this.
2. **WSP2 — the managed registry**: registry-as-authority, `import`, the desktop's add/update/remove.
3. **WSP3 — remotes become a map**: per-workspace remotes, sync loop per workspace, host identity.
4. **WSP4 — knowledge sync semantics**: provenance, monotonic replace, default-branch-only, served
   provenance. (Lands after WSP3 because the refusals belong to a workspace's host, but the Core
   judgement can be built and unit-proven independently.)

## 10. Open questions, deliberately held

1. **Cross-workspace asking.** A repository that wants something from another workspace today has a
   person carry it. If real use wants a bridged quest, that is a new, explicit door with its own
   disclosure argument — not a relaxation of §4's clause.
2. **Workspace-level doctrine.** Packs per workspace (a workspace naming packs all members adopt) is
   attractive and unforced; the canon's two-repository bar applies to canonizing the idea itself.
3. **One person, one workspace, many machines** already works (D47). One *team* per workspace is the
   deployment this design serves. Anything finer (per-repository ACLs inside a workspace) waits for a
   team that exists, per the service design §5c's "authorization mirrors repository access".
