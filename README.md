# Daoris (道衍)

**Cross-repo engineering doctrine.** One canonical set of agent-facing rules and knowledge,
materialized into each repository, kept from drifting — and improved from wherever the improvement was
discovered.

道衍 is *propagation and unfolding*: doctrine flows outward into the repositories, and refinements found
in a repository flow back and evolve the canon. Both directions ship in the first release, because a
one-way push would be distribution, not cultivation.

## Ecosystem

Three public projects, deliberately independent:

| | |
|---|---|
| [Lyntai](https://github.com/JiarongGu/Lyntai) | LLM cognition — providers, routing, memory, evaluation |
| [Shenora](https://github.com/JiarongGu/Shenora) | Desktop runtime — the shell an application is built in |
| **Daoris** | Engineering doctrine — how the work itself is done |

The CLI takes no dependency on either — it must run in repositories that have nothing installed. The
knowledge service consumes Lyntai as a library, at released versions only (D22). Daoris is the only one
of the three that installs *into* the others.

## The problem

The same doctrine gets independently re-derived in every repository, and the copies diverge. A rule that
turns out to be wrong stays wrong in five places, because nothing knows the copies exist. Measured on
this repository's own seeding: a doctrine set copied from a sibling **three days earlier** already
differed in 12 of 19 files.

## Install

Nothing is published yet (DIST1); once it is, every command runs through `npx` against a pinned
reference:

```sh
npx github:JiarongGu/Daoris#v0.0.1 init     # write daoris.json, report available packs
npx github:JiarongGu/Daoris#v0.0.1 sync     # materialize the doctrine, write daoris.lock
npx github:JiarongGu/Daoris#v0.0.1 check    # the gate — offline, exit 1 on drift
```

The canon ships **inside the package**, so the pinned reference is itself the version pin — no command
ever fetches anything, and `check` therefore works with no network at all.

## Commands

| Command | What it does |
|---|---|
| `analyze` | **What adopting would do here** — collisions, duplicates, projected budget. Writes nothing |
| `init` | Detects what the repository already has, writes `daoris.json`, reports available packs |
| `sync` | Materializes the manifest's packs — the rules region into `AGENTS.md`, knowledge and skills into `.claude/` — writes `daoris.lock`, regenerates the index |
| `check` | Drift, staleness, index freshness. **Offline.** Exit 1 on any failure; the core budget is reported, never enforced |
| `upstream <file>` | Promotes a locally-improved canonical file back into the canon |
| `index` | Says where the roster went: the `AGENTS.md` region, which `sync` regenerates (D59) |
| `status` | Packs, versions, drift, local files, and what a pending update would change; `--json` for an agent |
| `doctor` | Reports local documents that look like canonical ones under a different name. **Advisory — never fails** |
| `connect` | Registers this repo with a knowledge service: what it owns and accepts, and (`--workspace`) its circle |
| `retire` | Takes a repository off this machine's registry; **no file, history or doctrine is touched** |
| `import` | Registers a folder's subdirectories in one go, safe to re-run; `--workspace` names their circle |
| `remote` | This machine's remotes, one per workspace: `list`, `add <workspace> --url … [--key …]`, `remove <workspace>`. **Edits one file under the home; talks to nothing** |
| `agent` | Agents (Claude Code, Codex, dsh) and the accounts they run as: `list`, `install`/`update`/`login <agent>`, `profile list\|add\|remove\|default …`. **Spawns each agent's own tooling; keeps no sign-in** |
| `driver` | What this machine drives: `list`, `drive`/`undrive`, `hold`/`resume`, `cap <n>`, `adapter <name>`. **Edits one file under the Daoris home** |
| `plugin` | This machine's plugins: `list`, `add <folder>`, `remove <id>`, `enable`/`disable <id>`. **Edits under the home's `plugins/`; loads no code** |
| `browser` | Daoris's browser: `favorite list\|add\|remove` (a Daoris folder on its bar) and `extensions offer\|refuse`. **Edits files under the home** |


`sync` accepts `--dry-run` (print the plan, write nothing) and `--force`. `upstream` accepts `--all` to
promote every locally-edited canonical file at once. `connect` accepts `--workspace <name>`,
`status` accepts `--machine` to report this machine's wiring beside the repository's own declaration,
and `agent login` accepts `--profile <name>`.

**A workspace is the unit of sharing**: knowledge, quests and session records cross between repositories
within one and never across one. Membership is **wiring, like a git remote** — `connect --workspace
aurora` records it in this machine's registry and writes nothing into the repository, so a fork and a
second machine may each wire it differently. Omitting the flag leaves existing wiring alone; a
repository nobody ever wired is in `default`.

**A workspace's sharing has two halves, and they live apart.** Whether a repository's material *may*
leave the machine is its own `daoris.json` — tracked, reviewed, and silent by default. *Where* it would
go is this machine's map, the home's `remotes.json`: one deployment per workspace, because a shared
deployment serves exactly one circle and refuses a registration declaring another. `daoris remote`
edits that map, `status --machine` reports it, and a workspace with no entry syncs nowhere — which is
what every machine does until someone says otherwise. A key is never printed back, only its audit
prefix.

**One agent, many accounts.** An agent holds one login per configuration home, so `daoris agent`
makes accounts **named profiles**: isolated configuration directories whose *location* Daoris owns
(`harnesses/<agent>/<profile>/` under the home), selected at spawn through the variable that agent
already has. Logging in runs the agent's own flow inside one, so **Daoris never
sees, stores or copies a sign-in**; `agent key` keeps an API key, and `agent rules` what a
session may do (D72). Pick one per machine, per
workspace, or for a single conversation; the session record then names the account and tool version
it ran as. A spawn onto a missing agent, a profile nobody signed into, or a workspace the agent
has never been trusted in refuses **naming the action that fixes it**.
`docs/2026-09-22-toolchain-design.md` is the contract.

**`connect`, `retire` and `import` are the management commands** — opt-in, they talk to a service, and
no gate ever runs them. `remote`, `agent`, `driver` and `plugin` are management too; they edit files under
**the Daoris home** (`DAORIS_HOME` — the installed application's own `data/`, set once for your
account, never your profile; D63), refusing with none set. `agent` runs each agent's own installer,
updater or login flow, `agent pin` fetches from a maker's verified channel, and `agent trust --yes`
sets one flag in the agent's own file. Every other
command above is offline by construction. **The machine's registry
is the authority** on who is in the family: being in a folder is not being a member, so a repository
joins by connecting and leaves by retiring. `import` is safe to re-run: unstated wiring is
preserved. A never-managed store imports its
configured root **once**, and says so. A registered checkout that is no longer
where the registry says it is gets **named** by the next refresh rather than silently skipped.

**`--force` is the only way to lose work here**, so it names every file it overwrites or discards.
Daoris otherwise refuses in all three destructive cases — a file you edited, a file you wrote before
adopting, and a file being retired upstream that you had improved.

`analyze` answers the question a repository has *before* it adopts: what already exists here, what
would collide, what already says the same thing under another name, and what the always-loaded budget
becomes. It writes nothing, and `--json` gives an agent the exact facts to act on.

The division of labour is deliberate. **Daoris supplies what must be exact** — which paths collide,
which documents duplicate, what it costs — because an agent guessing at a collision is wrong in a way
that destroys files. **The agent supplies judgement** — which packs fit this repository, whether a
suspected twin really is one, how to resolve each collision — because a regex guessing at "is this a
.NET library" is wrong in a way that costs a sentence. Then a person selects.

It is also the only command that compares the working tree against the **canon** rather than the lock,
which is what lets it find a renamed twin *before* adoption rather than after — and, on this
repository, what caught a set of stale provenance headers that every lock-based check agreed was fine.

`doctor` exists because of the one thing the lock cannot catch: a repository's own rule that says the same
thing as a canonical one under a different name is *local*, and local is invisible by design. Word overlap
is a crude signal, so it only ever reports — a false positive that failed a build would be worse than the
duplication it warns about.

Its threshold is set from measurement against real sibling documents, not taste: near-verbatim copies
score ~73%, twins that were *rewritten* rather than copied land at 34–43%, and unrelated documents at
7–16%. It finds **restatement, not convergence** — a document that reaches the same principle through an
entirely different vocabulary scores like an unrelated one, and no threshold separates those. Adoption
still wants a read-through by hand; `doctor` shortens that job rather than replacing it.

## The manifest

```json
{
  "source": "github:JiarongGu/Daoris#v0.0.1",
  "packs": ["dotnet-library"],
  "target": ".claude",
  "coreBudgetBytes": 30000,
  "domain": {
    "summary": "One line: what this repository is, for someone who has never opened it.",
    "owns": ["the areas where a change belongs here rather than anywhere else"],
    "accepts": ["the kinds of work worth asking of it"]
  },
  "remote": { "join": true, "knowledge": false }
}
```

`daoris.lock` sits beside it, generated: one entry per materialized document, recording its pack,
canonical path, version, and content hash — plus, for the always-loaded ones, the file whose region
holds it, since those are a span inside `AGENTS.md` rather than files of their own. Both are tracked, so
a reviewer sees exactly what changed.

`domain` is the repository's declaration to the family — it is what makes a quest addressable rather
than a guess, and `daoris connect` refuses to register without one. `remote` is the one **disclosure**
control: whether this repository joins a team's remote deployment (its registration, quests and
session records become visible there), and — a second, separate declaration — whether its indexed
knowledge content feeds too. It lives in the manifest, tracked and reviewed, because disclosure is the
repository's call, not one person's local toggle; **absence means local, silently**, and `knowledge`
without `join` is refused. `daoris status` reports the declaration.

## Three layers

- **Core** — universal workflow rules and discovery skills. Every repository gets these, unless a selected
  pack offers to switch one off and the manifest confirms it under `switchedOff` (D71); `sync`, `check`
  and `status` all say so.
- **Packs** — stack-specific sets, named in the manifest.
- **Local** — the repository's own documents: never synced or touched, and its knowledge and skills
  are indexed as `(local)`.

The rule that makes this safe: **anything not in the lock is invisible to the tool.** Daoris only ever
writes files it put there. A repository that already owns a file at a canonical path gets a refusal, not
a silent overwrite.

## Two things worth knowing

**The tier is the location.** The always-loaded rules land in a region of `AGENTS.md` that Daoris owns —
the one file every harness this family drives actually reads — with `CLAUDE.md` carrying a one-line
`@AGENTS.md` import; files in `knowledge/` are read on demand; `skills/<name>/SKILL.md` is invoked by
name. The agent harness decides that by location, so Daoris does not carry a redundant `tier` field — and
because a region has a byte count exactly as a directory did, `check` still reports the
always-loaded footprint against the budget the manifest declares. It reports and never fails on it
(D54): drift is a fact, size is a judgement, and a build stopped by a judgement teaches people to
raise the number rather than read it.

**Canonical skills are parameter-free.** A skill states only the procedure that holds in every
repository and sends the reader to the generated index for anything local — there is no substitution map
in the manifest. Surveying twelve repositories showed why: copies of the same skill ranged over a 6.6×
size spread, and the shared part was ~15 lines. The rest was each repository's own routing content, which
no placeholder could have supplied.

**Every vendored file carries a one-line provenance header.** Not decoration: an agent that opens a rule
needing a tweak will otherwise simply edit it, which is exactly how the copies diverged. The header says
where the file came from and to use `daoris upstream`; the lock's hash catches the edit either way.

## Extending it

Daoris loads **no code into any host**, and has four seams, each a *declaration* or a *wire*, so none
can break the tool that reads it:

| To add | Write |
|---|---|
| **Doctrine** for a stack | a pack — `canon/packs/<name>/pack.json` + its tiers |
| **A gate** | a row in `daoris.gates.json` |
| **A harness** to drive sessions on | nothing — speak the **Agent Client Protocol** |
| **A plugin** on one machine | a folder under the home's `plugins/` with a `plugin.json` (D64): it *declares* ACP-door configurations and the servers sessions are handed, and may *speak* from its own process |

A pack and a plugin each declare the API they need (`"apiVersion"`), read first. The harness seam is
a protocol, not a registry; a plugin a folder, not a catalogue. `docs/2026-09-23-plugin-design.md`
is the contract.

## Beyond the CLI: the service, the platform, the driver

The CLI is the doctrine half. **`Daoris.Service`** indexes what every adopting repository has learned,
finds where two repositories reached the same conclusion in different words, holds the **registry** —
what each repository owns and accepts — and carries **quests**: how one repository asks another for
work instead of reaching in. **`Daoris.Web`** is the platform over it — knowledge, quests and projects
in one window, with doctrine read-only everywhere. **`Daoris.Desktop`** is the driver (D45): the
desktop shell hosts the local service, carries the same platform bundle, and runs the loop that turns
the quest queue into an execution queue — a fresh agent session per open quest, one per repository,
onto a clean tree, claiming its own quest so driven and outside work are indistinguishable. The same
HTTP host in **shared mode** is the team
remote (D47): every route gated by minted per-person per-machine keys, fed by each desktop's sync
loop, never required by anything local. Each artefact's own README carries the rest.

**Worked example:** [`examples/`](examples/README.md) is a two-project family — an engine and a game —
and `npm run rehearse:family` drives the whole arrangement through the real artefacts — adoption,
registration, a quest's full life, a driven session, a two-machine remote — with no model in the gate.

## Developing Daoris

```sh
npm run verify          # tests, then daoris check against its own doctrine
npm run rehearse        # pack, install into a clean repo, drive the full lifecycle
npm run rehearse:family # the router: two example projects, quests, the service, a restart
node --test             # tests only
```

`rehearse` is the release gate. The test suite exercises the source tree; the rehearsal exercises the
**artefact** — the tarball npm would publish, resolved through the `bin` entry the way a consumer runs
it. That is where install stories break: a file missing from `files`, a path that only resolves in a
source checkout, a skill directory that does not survive packing.

`DAORIS_CANON` overrides the canon root, which is how the tests drive a fixture canon.

Daoris carries its own `daoris.json` and syncs core into its own `.claude/`. A tool that cannot hold its
own doctrine cannot hold anyone else's.
