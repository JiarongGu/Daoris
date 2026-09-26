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
Scoped to every workspace of several, the nodes are ordered by circle first, so each circle's
repositories sit together and its quests stay within its arc, and a node's detail names its circle
(UX5 U49).

**The drawing is drawn at its own size** (UX5 U44): one unit a pixel, so a name is the type
scale's, and centred in its card. It is framed on what it draws (`frameMap`: every node and ring,
every name with its width estimated per character, every line's count), never a fixed square, which
left a three-node ring's lower third blank and cut a side node's long name at its edge. A card too
narrow for the full ring gets a smaller one (`fitRadius`) rather than smaller names, never closer
than two rings and a gap; past that, the drawing shrinks as the last resort. The code map keeps the
same rule by its width (`codeWidth`).
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

**What a person does with it.** Hovering a node, or reaching it by keyboard, lights its edges and
dims the rest. Choosing a node (pointer or keyboard; every node and edge is a button) opens its
detail: summary, declaration, and the quests into and out of it with their states in words.
Choosing a quest edge lists that direction's quests, and stands it forward while the other lines
step back. Choosing a knowledge edge says how many findings the pair shares and opens Convergence. A
choice is a toggle: a second press releases it, and so does Escape (UX5 U47). The map adds no
actions of its own: every act stays where it already lives, so a quest in a detail opens its drawer
(UX5 U46), and a session there wears the status hue the rail gives it (U45).

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
without a pick takes.

**The intake joined as a second job (AGT6, 2026-09-24).** With an agent named in `intakeAdapter`,
`STARTS` answers an `intake` row after each circle's `work` row, from the same `WiringAsync`, which is
the `SelectAsync` an intake takes for an ask in that circle. An agent this build has no adapter for is
a held row carrying the driver's own sentence, not a refusal of the whole answer. The list names each
row's job once there are two. Settings' *Daoris's own AI* reads the same rows for its one-line
"runs as" per circle.

## 3. MAP3 — a repository's code (after MAP2)

**What it answers.** Inside one node, what the repository is made of: its modules and how they
depend on each other. This is the roadmap's *repository intelligence* given a first consumer.

**The rule it keeps.** Daoris never writes into another repository (D32). The code map is **fed by
the repository**, the way its knowledge is, and stamped with the commit it speaks for (WSP4).

**The contract (settled 2026-09-23).** One committed JSON file, found by convention like the
decisions log: `docs/code-map.json`, else `code-map.json` at the root, first match wins. The scanner
takes candidates rather than configuration, for the reason it already gives: a reader that needs
setting up gets set up for one repository and never for the rest.

```json
{
  "version": 1,
  "modules": [{ "id": "service", "path": "src/Service", "summary": "indexes the family's knowledge" }],
  "dependencies": [{ "from": "web", "to": "service", "kind": "http" }]
}
```

- `id` is unique in the file and is what a dependency names. `path` is repository-relative, with
  no leading slash, no drive and no `..`: a map is read by machines that are not this one (D47 §4).
  `summary` is one line. `kind` is a short word the producer chooses (`imports`, `project`,
  `http`, …) and is drawn as the producer wrote it.
- **Judged whole.** A file that breaks any rule above is refused with the sentence naming the
  first break, and nothing of it is shown. A half-drawn map reads as a whole one.
- **Bounded** at 500 modules and 5,000 dependencies, and at 1 MB before it is parsed. A map that
  size is no longer a picture, and the bound keeps a mistaken producer from filling the store.
- No field says which producer wrote it, so the service and the page never know.

**Where it is read.** In local mode the service reads the file from the registered checkout on each
request. That is the person's machine showing the person's state, as the local index already does
(WSP4). A shared deployment holds what was fed, per repository, at the commit it speaks for, ordered
exactly as knowledge is (the sync design's §8, SYNC5a). The page opens it from a MAP2 node's detail and
lays it out in layers by dependency: a pure function, tested, and still no graph library.

**Who produces it — decided (owner, 2026-09-23): both.** A tool per stack where one exists (Roslyn
for C#, the TypeScript compiler for TS), run by the repository's own gates, because it is exact.
Elsewhere, the repository's agent keeps the file current as it works, asked by the driver's session
prompt (MAP3d, below): that works for any language, and is only as fresh as the last session. Both
write the same small file, so the service and the page never know which produced it. Rejected: the
agent alone (drifts where a tool could be exact) and a tool alone (nothing for a stack without one).

**How the agent is asked — decided (owner, 2026-09-24): the session prompt.** The driver's prompt
asks, for a repository whose code map exists, to keep it current, as wiring at spawn (HELP3's
shape). The file is still written by that repository's own agent, in its own tree (D32). Rejected:
**a canon skill**. It installs into every adopter, and the canon's bar is that two repositories
learned a thing before it is doctrine (DECISIONS: the bar "is what makes canonical content
trustworthy"). No repository has learned to keep a code map; it is a Daoris feature. **A pack**
was the middle road, and packs have the same bar.

**MAP3d is built (2026-09-24): the agent producer.**
- **When.** A driven quest's session is asked exactly when the tree it runs in keeps a map, found
  where the reader looks (`CodeMapFile.Find`, the reader's candidates restated and held to its
  source like the devkit's twin). A repository that keeps none is not asked to start one: a map
  nobody started is not a session's to invent, and a person or the tool starts one deliberately. A
  conversation carries no composed prompt, and an intake runs in a room under the home rather than
  in a repository, so neither is asked.
- **What.** One paragraph of the claiming instruction, after the take-work-close paragraph and
  before the boundary. It names the file. It asks the session to bring the map up to date in the same
  change when the work adds, removes, moves or rewires a module. It says how: with the repository's
  own tool where it has one, otherwise by hand in the reader's shape (a unique `id`, a
  repository-relative `path`, a one-line `summary`, dependencies naming modules by `id`). And it says
  why the shape matters: a map that breaks it is shown as nothing at all.
- **Not the tool's authority.** The prompt cannot tell which producer wrote the file, and the file
  does not say, so it defers to the repository's own tool rather than to `daoris-devkit map`, which
  a repository may not use. `map --check` stays a declared gate for exactly that reason.
- **Proof.** Driver tests hold the clause to exactly the case above, and hold the target a quest is
  handed (`SessionTarget.ForQuest`) to naming the map its tree keeps. The family rehearsal reads
  the stub session's own transcript both ways: the newcomer's first session, before it keeps a map,
  is not asked, and a session after it commits one is asked, by `docs/code-map.json`.

**MAP3a is built (2026-09-23).** `CodeMapReader` (service core) judges the file, and
`GET /api/code-map/{repository}` answers from the registered checkout. On the page, a MAP2 node's
detail offers *Open its code map*. The map is laid out by `layerModules`, longest path, so what uses
a module sits above it and a recorded cycle still places every module once. An arrow that skips a
layer bows out past the column: drawn straight, it ran behind the box between and vanished (seen on
the window). The example engine keeps a map and the game keeps none, and the browser gate opens both.

**MAP3b is built (2026-09-24)**, with SYNC5a. The desktop's sync reads the map from its own host and
feeds the file's text to `POST /api/feed/code-map`. The deployment judges it whole again and keeps it
in canonical form (`CodeMapReader.Write`) in `fed_code_maps`. A commit with no map keeps the row
with no body. The code-map door answers from that store for a repository with no checkout here.

**MAP3e is built (2026-09-24): a teammate's map comes down.** A machine's host brings it down on the
same pass as the quests and the records (`CodeMapSync`), for each repository of the circle it holds
only a teammate's copy of. Pulling a map needs no git, so the pass is the host's (D69's reasoning),
and the host is what answers the page. The remote's holding is the order: it already took each map
by ancestry, so this machine holds exactly what the remote holds, at the commit it holds it. A newer
commit replaces the map here, a commit that keeps none is held with none, and a map the remote no
longer holds is forgotten here. The commit is asked first (`GET /api/feed/held`), so a map that has
not moved is not sent again. A checkout here is the authority on its own map and is never asked for.
The door's answer is one shape, `CodeMapWire`, which the host writes and the pass reads. It carries
`fed` when no checkout was read, naming the commit, its line and the key that fed it, and the page
says so. Rejected: telling the page where the map lives instead of bringing it. A machine that has
to ask the remote to draw a teammate's repository stops answering for its circle when it is offline,
and the team's rows and quests already come down.

**MAP3c is built (2026-09-24): `daoris-devkit map`.** The devkit writes the file from the project
files it can read exactly, with no compiler, and `map --check` says whether the committed file is
still what they say.
- **Modules.** Every tracked `*.csproj` is a module, with the project's file name as its id and its
  directory as its path. Every tracked `package.json` below the root is a module too, with its package
  name as its id. The root `package.json` is the repository itself, not a module of it. Tracked files
  only, so a build output or an installed package never becomes a module.
- **Dependencies.** A `ProjectReference` is kind `project`, and a dependency in any section naming
  another of the repository's packages is kind `package`. A reference the repository does not track
  (outside it, or an MSBuild expression) is reported and not drawn, because the reader refuses a
  dependency that names no module.
- **What a person writes is kept.** The tool owns the modules and its two kinds. A summary is the
  project's own `Description` or `description`, folded to one line. Where the project declares none,
  the line already in the map is kept, so a person writes it once and the tool never erases it.
  Dependencies of any other kind (`http`, say) are a person's too, and stay while both ends are
  modules.
- **What it refuses.** It refuses to write anything the reader would refuse: two modules on one id,
  or more than the reader's bounds. It also refuses to rewrite a map it cannot read, which would lose
  what a person wrote in it.
- **The file.** It goes where the reader looks: an existing root `code-map.json` stays at the root,
  otherwise `docs/code-map.json`. It is written the same way every time (sorted, two-space indent,
  LF, a final newline, text as it reads), so a moved reference is a small diff.
- **The gate.** `map --check` is a fact, so it gates (D54). It is a **declared** gate, because a map an
  agent or a person wrote for another stack is not the devkit's to judge, and nothing in the file says
  which producer wrote it. Daoris declares it in `daoris.gates.json` and keeps its own
  `docs/code-map.json` this way.
- **The twin.** The devkit shares no code with the service, so it restates the reader's rules. A
  devkit test holds the candidates and bounds to `CodeMapReader`'s own source, and a service test
  judges Daoris's own committed map with the reader.

**Build order.** MAP3a: the contract, the local read, the page, and the example family carrying a
map. MAP3b: the feed to a shared deployment, with WSP4's provenance. MAP3c: a tool producer (the
devkit reads project references, which is exact for C# without Roslyn, and a package's
dependencies for TS). MAP3d: the agent producer, by the session prompt.

## 4. What does not move

D24: no map needs a model, and none names one. D32: a map of another repository is read, never
written. D47 §4: nothing machine-local reaches a browser. D50: the maps are views, not settings, so
a terminal twin is optional. `daoris map --json` would be the natural one for a script, and is left
until someone asks.
