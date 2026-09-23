# The map — how the parts connect (MAP1–MAP3, D67 §3)

> *"wiring means few things, how daoris agent loop chain workflow, and how repos wired in the
> workspace (so a topo map might need to be introduced and this map will also show for the repo
> itself for code we can setup this in roadmap and this will help for development)"* — the owner.

What each map draws, from which data, and where it lives. The owner settled the two open choices
the same day (§1 and §3). The order is MAP2, then MAP1, then MAP3: MAP2 needs no new
data, MAP1 needs the machine's wiring, and MAP3 needs a feed that does not exist yet.

## 1. MAP2 — the workspace topology (first)

**What it answers.** Which repositories are in this circle, what each is for, and how work and
knowledge move between them. That is the thing a person otherwise reconstructs from five views.

**Drawn from data that exists, with no model:**

| On the map | From | Shows |
|---|---|---|
| A **node** per repository | the registry (`/api/registry`, scoped by workspace) | name; summary on hover; how many quests are open to it; whether a session is working there |
| A **quest edge**, directed, from → to | the quest store | how many quests went that way. Solid while any is open, faint once all are closed. `then` chains are drawn as the same edge, one step on |
| A **knowledge edge**, undirected and dotted | the convergence detector | two repositories that learned the same thing in different words. This is the one edge no text comparison finds (D17) |
| **Declarations** (`owns`, `accepts`) | the registry | in the node's detail, not as edges: they are what each repository says of itself, and the edges are what actually happened |

**Layout.** A pure function, `layout(nodes, edges) → positions`, deterministic and ordered by name,
so the picture does not move between two looks at the same data. A ring for a small circle, and
layers by quest flow once it grows. Tested as a function, then drawn as SVG on the design tokens
(D41): status never by colour alone, and both themes. **No graph library**: a family is a handful
to a few dozen repositories, and a layout engine would cost more in bundle and in look than it
buys. MAP3 may need one; that is MAP3's decision.

**What a person does with it.** Hovering a node lights its edges. Clicking a node opens its detail:
the declaration, the quests to and from it, its sessions. Clicking an edge opens those quests. The
map adds no actions of its own: every act stays where it already lives (the quest drawer, the
session view).

**What it may show where.** Everything above is service-visible and carries no machine path, so the
map works in a browser and against a shared deployment (D47 §4). A session's account and tree are
machine-local and appear only on the desktop, where they already do.

**Where it lives — decided (owner, 2026-09-23): a view of its own, *Map*, on the activity bar.**
Rejected: the top of Overview (the map would be a summary of itself), and inside Projects (the
cards are what a node's detail already shows).

## 2. MAP1 — the workflow (second)

**What it answers.** How the higher loop (D67 §2) carries one piece of work, and on what. An ask
goes to the intake (which tier answered), then to its quests, their `then` steps, the sessions
that ran them and how each ended. Each step shows the agent, account and version it ran on, read
from the session record (D49 §4).

**And the wiring as the loop will resolve it next.** For each job — the intake, the work — which
agent, which account and which version a start would take in this workspace. That comes from
`driver.json`, `harnesses.json` and the plugins, resolved by the same functions the driver uses, so
the picture cannot disagree with the loop. It is machine-local, so it is desktop-only.

**Where.** A quest's drawer grows a chain strip: its ask, its parent, its `then`. The wiring gets a
panel beside the agents on Settings, drawn as the resolution runs — pick, then workspace default,
then machine default, then the tool's own home. The same strip reads the same way in the Sessions
view.

## 3. MAP3 — a repository's code (after MAP2)

**What it answers.** Inside one node, what the repository is made of: its modules and how they
depend on each other. This is the roadmap's *repository intelligence* given a first consumer.

**The rule it keeps.** Daoris never writes into another repository (D32). The code map is **fed by
the repository**, the way its knowledge is, and stamped with the commit it speaks for (WSP4).

**A contract to agree first.** The feed is a small file: modules (id, path, one-line summary) and
dependencies (from, to, kind). The service keeps it per repository per commit, and the page opens it
from MAP2's node.

**Who produces it — decided (owner, 2026-09-23): both.** A tool per stack where one exists (Roslyn
for C#, the TypeScript compiler for TS), run by the repository's own gates, because it is exact.
Elsewhere, a canon skill asks the repository's agent to keep the file current as it works: that
works for any language, and is only as fresh as the last session. Both write the same small file,
so the service and the page never know which produced it. Rejected: the agent alone (drifts where a
tool could be exact) and a tool alone (nothing for a stack without one).

## 4. What does not move

D24: no map needs a model, and none names one. D32: a map of another repository is read, never
written. D47 §4: nothing machine-local reaches a browser. D50: the maps are views, not settings, so
a terminal twin is optional. `daoris map --json` would be the natural one for a script, and is left
until someone asks.
