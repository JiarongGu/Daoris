# The map — how the parts connect (MAP1–MAP3, D67 §3)

> *"wiring means few things, how daoris agent loop chain workflow, and how repos wired in the
> workspace (so a topo map might need to be introduced and this map will also show for the repo
> itself for code we can setup this in roadmap and this will help for development)"* — the owner.

What each map draws, from which data, and where it lives. The owner settled the two open choices
the same day (§1 and §3). The order is MAP2, then MAP1, then MAP3: MAP2 needs no new
data, MAP1 needs the machine's wiring, and MAP3 needs a feed that does not exist yet. **MAP2 is
built** (2026-09-23), and §1 describes it as it is.

## 1. MAP2 — the workspace topology

**What it answers.** Which repositories are in this circle, what each is for, and how work and
knowledge move between them. That is the thing a person otherwise reconstructs from five views.

**Drawn from data that exists, with no model:**

| On the map | From | Shows |
|---|---|---|
| A **node** per repository | the registry (`/api/registry`, scoped by workspace) | name; how many quests are open to it, wherever they came from; whether a session is working there (ringed, and said in words) |
| A **quest edge**, directed, from → to | the quest store | how many quests went that way. Solid while any is open, faint once all are closed. `then` chains are drawn as the same edge, one step on |
| A **knowledge edge**, undirected and dotted | the convergence detector | two repositories that learned the same thing in different words. This is the one edge no text comparison finds (D17) |
| **Declarations** (`owns`, `accepts`) | the registry | in the node's detail, not as edges: they are what each repository says of itself, and the edges are what actually happened |

**A quest with an end off the map is counted, not drawn.** An ask's sender (`ask #…`) or a
repository outside the circle is not a node; the map says how many such quests there are, and they
still count toward the receiver's open number.

**Layout.** Two pure functions in `src/Daoris.Web/src/map/`: `buildTopology` (the data above into
nodes and edges) and `layoutRing` (positions, deterministic and ordered by name, so the picture does
not move between two looks at the same data). A ring from the top, one node alone at the centre.
Layers by quest flow are for a circle too big for a ring to read, and wait until one exists. Drawn
as SVG on the design tokens (D41): status never by colour alone, and both themes. **No graph
library**: a family is a handful to a few dozen repositories, and a layout engine would cost more in
bundle and in look than it buys. MAP3 may need one; that is MAP3's decision.

**Three things only the window showed** (2026-09-23), each now held by the code or a test:
- **A name goes on the side facing away from the centre** (`placeLabel`). Every line runs inward, so
  the outside is where no line arrives. With names always below, the arrow into the top node ran
  through its name.
- **A line is pressed through a wide invisible stroke**, and its count sits on the line at the
  curve's midpoint. A two-pixel curve was a line nobody could press: in the browser gate the map
  itself took the click.
- **Arrowheads are sized in the map's units**, not the line's. A head that grew with the line's
  width made the chosen line's arrow twice the size of the others.

**What a person does with it.** Hovering a node lights its edges and dims the rest. Choosing a node
(pointer or keyboard; every node and edge is a button) opens its detail: summary, declaration, and
the quests into and out of it with their states in words. Choosing a quest edge lists that
direction's quests. Choosing a knowledge edge says how many findings the pair shares and opens
Convergence. The map adds no actions of its own: every act stays where it already lives (the quest
drawer, the session view).

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

**The strip is built (MAP1a, 2026-09-23).** `buildChain` in `src/Daoris.Web/src/map/chain.ts` walks
up through `parent` and down through the quest that names the last as its parent. The steps still
to come are taken from the last published quest, because the quest before it still lists the step
that has already become one. A parent loop stops the walk, and a parent the page does not hold is
named rather than dropped. `ChainStrip` shows every session under its quest, oldest attempt first,
with the agent, version and account from the record. It renders in the quest drawer, where it
replaced the *follows* row and the *then* list, and under the attended session's head in Sessions.
Both only when there is a chain. The drawer's own session section still shows where things stand
now. The ask is shown by its handle: which tier answered is on the ask's record, which INT4c puts
on screen.

**The wiring is built (MAP1b, 2026-09-23).** `HarnessRoster.WiringAsync` answers for one workspace:
the adapter, whose accounts it runs as, the account and which rung chose it, the version and which
rung pinned it (or that `driver.json` names the command), and the refusal. The account comes from
`HarnessSettings.ResolveFrom`, which is what `Resolve` now is. Whether the start happens, and at
which version, is `SelectAsync` itself, and a test holds the two to the same answer for every shape
of the wiring file. The bridge's `STARTS` route answers for the workspaces the page names, as
names only: no home, no binary path and no key. Settings shows it as *What a start runs on*, after
the agents. The one job is the work: a driven session, which is also what a conversation started
without a pick takes. The intake joins with INT4b.

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
