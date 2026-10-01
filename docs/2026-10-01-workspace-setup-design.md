# Setting a workspace up — every repository adopted, declared and documented by its own session

> The owner's ask, 2026-10-01 (WSSETUP1): a driven session in the owner's work workspace stopped to ask the
> person a question its own repository's notes and code could answer. The owner's diagnosis is that the
> workspace lacks knowledge: *"the repo should be registered and apply the doctrine and also initialize the
> knowledge"*. This is the contract for the WSSETUP rows, and its decision is **D124**. Status: **designed;
> WSSETUP9, WSSETUP11 and LAYOUT7 as amended built** (§6.1, §7.3, §2), **and WSSETUP2 and WSSETUP4** (the
> install's doctrine tool, §1.2, and the version guard, §1.4), **and WSSETUP3** (`daoris` on every child's `PATH`,
> §1.3), **and WSSETUP5** (registration follows the line, §3); D124's notes say what each build settled, and the rest
> is not built. It
> builds on the set-up quest of `docs/2026-10-01-agent-layout-design.md` §6 (D117: LAYOUT7, LAYOUT8, LAYOUT10),
> the standard of `docs/2026-10-01-development-documents-design.md` (D122) and the tools' environment of
> `docs/2026-10-01-tools-design.md` (D121, TOOLS5), and it amends D117 §6 where §11 says. Read with **D13**,
> **D32**, **D45**–**D47**, **D58**, **D70**, **D77**, **D79**, **D83**, **D87**, **D105**, **D107**, **D109**,
> **D110** and **D112**.

The workspace, its repositories, its company and its tickets are private. They are named here only by what
they are: *the owner's work workspace*, *29 repositories*, *a report repository*.

- §0 is what is true today, measured on the install or read from the code at `21787b8`.
- §1–§6 are the design: the doctrine tool, the set-up quest, registration, the workspace, a baseline, and a
  session that looks before it asks.
- §7 is what it costs. §8 is the build, as rows. §9–§13 are what only a real run proves, what was rejected,
  what this amends, the twins, and what this document's gate does not cover.

## 0. What is true today

### 0.1 The workspace, as the parent measured it on the install (2026-10-01)

| What | Measured |
|---|---|
| Repositories | 29, each a registry row with a root on this machine, from an import that named the workspace (D77) |
| Drivable | all 29: opted in on this machine and driven over the protocol door (D70) |
| Adopted | none: no `daoris.json` in any |
| Registered, in the code's word | none. `Registration.Registered` is *adopted, and declares something* (`Registry.cs`). `connect` refuses a manifest that declares no `domain` (`connect.ts`), and none of these has a manifest |
| Addressable | all 29: a row with a root is (`Registration.Addressable`, D70) |
| Indexed knowledge | 23 with no entry at all. 6 with 16 to 195, every one from the scanner's candidate files: a decisions log, a fix log, a task archive, or a `.claude/` or `.agents/` tier |
| The intake's room | *None of these declares what it owns* (`Intake.cs`). Every ask is routed by each repository's README and stack (D77), and nothing else |
| How work lands | a local branch per quest (D87's `branch` form). The owner pushes it, opens the pull request and merges |

### 0.2 The stop that started this

A driven session in a report repository, working one of the owner's tickets, ended its turn holding its quest
to ask the person which report the ticket's words named. (PARK1 found that the session's card lost that
question.) The owner's reading is that the repository's own notes and code held the answer. Two things failed,
and they are different:

1. **The session asked what it could have looked up.** Its instruction (`TargetPrompt.Asking`, `Adapters.cs`)
   offers three reasons to stop for the person: *a sign-in, a go-ahead for an act outside this repository, a
   choice between options that is theirs*. Which report a ticket means reads as the third. Nothing in the
   instruction says that a choice the repository's documents, code or history settle is not the person's.
2. **The workspace holds nothing to look up.** No repository says what it owns, what it promises, or how the
   figures others read from it are computed. A neighbour's session that needed any of it would read up to 28
   other checkouts (D107 lets it), or ask.

### 0.3 What exists to build on

- **The set-up quest** (D117 §6, LAYOUT7 and LAYOUT8, designed and not built). One quest per repository,
  published as the person's, carried by the repository's own session on its branch, and landed by the
  workspace's rule. Its steps take up the doctrine in the agents layout and *fill in `domain`*. D122 §2.9 adds
  the brief, the documents and the safe work. Its refusals: *not registered here; no checkout here; no git
  history; already on the agents layout and clean; a set-up already open*.
- 🔴 **The doctrine command comes from npm** (D105, D117 §6.6), and nothing is published. The registry answered
  404 for `daoris` on 2026-09-30. LAYOUT10, the first real set-ups, *waits on the first publish*. D117 §9
  rejected *the install carrying the CLI onto a session's path before the first publish*, because *a second
  channel is a second version of the doctrine tool on one machine*.
- **The install does not carry the CLI.** `tools/desktop-publish.mjs` lays out the launcher, `app/` (the
  application, the host, the offered plugins, `resources.json`) and `data/`. A terminal's `daoris` is whatever
  the person installed.
- **One environment for every child is designed, and not on main.** TOOLS5 (D121 §2.4, building on TOOLS4) puts
  the managed or named tools' folders first on the `PATH` of every process the driver and the modules start.
  TOOLS2's resolution is built, in `tools.ts` and `Tools.cs`.
- **Registration is `connect`, run in the repository.** It reads `daoris.json` from the folder it runs in,
  refuses a linked worktree and a manifest that declares nothing, and sends the declaration, the packs, the
  remote's two flags, the lanes and, to a local service, the root. The desktop's *add* and the service's import
  read a manifest off the checkout's disk (`RegistryModule`, `RegistryImport`). D117 §6.5 has the registration
  *refreshed* from the line once a set-up lands, and says neither who does it nor when.
- **The scanner, for a repository with no lock** (`RepositoryScanner`, D117 §5.5), reads the rules, knowledge
  and skills at both roots, and the three logs by their candidate names. It never reads a README. The intake
  does: a README's title and first describing paragraph, labelled as the repository's word (D77,
  `SelfDescription`). DOC5's note under D122 (merging with integrate-h) gave the router no candidates, since a
  `docs/README.md` in a repository that declared nothing may be a site's front page.
- **The driver's pace** (`DriverConfig`, `Planner`, D58): at most `cap` sessions at once across all
  repositories, 2 by default. The planner starts the oldest open quest first, in the service's order. A session
  runs at most `timeout` minutes, 30 by default, and three failures park its quest.
- **The one long driven turn measured** (COST1 and METER1 in the backlog): 48 minutes and about a thousand tool
  calls. Its context grew from 48K to 679K tokens on a 1M window. It read 663K tokens anew and 68.5M from its
  cache.
- 🔴 **Nothing stops an older doctrine tool from rewriting what a newer one wrote.** `sync` measures against the
  lock (D13). To an older canon, a file the newer canon improved and nobody touched here reads as *the canon
  changed upstream*, and `sync` rewrites it to the older text. `status` reports the versions differ
  (`commands.ts`), and `sync` does not refuse. This is so with npm alone: `npx daoris@0.0.1 sync` in a repository
  locked at 0.0.2.

## 1. The doctrine tool on a session's path

### 1.1 The choice

| | Before the first publish | After it | The rule a session needs | Network | What a committed file ends up saying |
|---|---|---|---|---|---|
| **Wait for npm** (D105 as D117 §6.6 reads it) | no set-up anywhere outside this workspace | `npx daoris@<version> …` | a runner with arguments, which D122's judge refuses and auto mode drops as a wildcard (D122 §1.6, finding 4) | every first run fetches; codex's `agent` mode has none (ACP3) | `npx daoris@<version> check` |
| **A path to the install's copy**, written into the quest (`node <install>/app/cli/bin/daoris.mjs …`) | works | works | exact, and holds a machine path | none | 🔴 the machine path, wherever a session copies the command: a brief, a room, a commit message (`sensitive-info`) |
| **`daoris` on every child's `PATH`**, from the install's packed CLI | works | works; npm stays the channel outside Daoris | exact, by verb: `Bash(daoris check)` | none: every doctrine command is offline (D8) | `daoris check`, the name npm's `bin` gives too |

**Chosen: the install carries its packed CLI, and every child Daoris starts finds `daoris` on its `PATH`.**
The first way holds the whole arc on a press that is the owner's and has its own unknowns (the name, the
account), and even after it, its rule is one no judge passes and its fetch is one codex cannot make. The second
works, and leaks the install's path into every repository a session writes it into. The third is the bare name
npm itself would put on a `PATH`, so a committed file says the same thing before the publish and after it.

### 1.2 What the install carries

- **The package, as the release publishes it.** `tools/desktop-publish.mjs` runs `npm pack` in `src/Daoris.Cli`,
  whose `prepack` builds `dist/` and stages the canon, the licence and the readme. This is the artefact the
  release rehearsal installs and the release workflow publishes. It is unpacked into `app/cli/` by the tar
  reader the CLI already carries (`tarball.ts`).
- **Two launchers in `app/bin/`**: `daoris`, a shell script, and `daoris.cmd`, each running `node` on
  `../cli/bin/daoris.mjs` with the arguments it was given. These are the shapes npm writes for a global `bin`,
  so Git Bash and Command Prompt each find `daoris` by its bare name. npm's third shape, `daoris.ps1`, is left
  out: a machine's execution policy may refuse it, and PowerShell then finds the `.cmd` by `PATHEXT`. That is
  the reading of each shell's lookup rules, **not measured** here, and the deployment rehearsal measures it
  (WSSETUP2).
- **Its version is the canon's.** `release-prep --check` already holds the package's version, the canon's and
  every pin together.
- **Nothing outside the install.** No global npm install, nothing under the user profile (D63), and the
  account's `PATH` untouched. A republish replaces `app/` whole, the CLI with it.

*As built (WSSETUP2): the package lands in `app/cli/node_modules/daoris/`, as npm lays a package out under a prefix,
and the launchers run `../cli/node_modules/daoris/bin/daoris.mjs`. Unpacked straight into `app/cli/`, the CLI reads
`<install>/canon`, since it reads the canon it ships only from under `node_modules` (`resolveCanonRoot`). The shells'
lookup was measured on a laid-out pack: Command Prompt and PowerShell run `daoris.cmd`, Git Bash the script. D124's
note has the rest.*

### 1.3 Found on every child's `PATH`: TOOLS5's one environment

- **TOOLS5's builder puts `app/bin/` first**, before the tools' folders, on every child it builds: a driven
  session on either door, a conversation, an intake, a hook, a landing plugin, and each of the terminal's shells.
  Ask Daoris's helper has no shell (HELP4), so nothing reaches it. The CLI's twin (`tools.ts`) does the same for
  the CLI's own children, when an install sits beside the home.
- **It is Daoris's own program, not a tool** (D121 §2.1). There is no way to choose for it, no download and no
  row in Settings → Tools: the install decides, like the hosts.
- **It runs on the `node` the child's `PATH` finds**, which is the one Tools resolves: the system's, managed, or
  a file the person named. The package needs Node 22 or later (`engines`). The press asks Tools for `node` and
  its version, and refuses with none, or one older (§2.1).
- **A person's own terminal outside Daoris is unchanged.** Inside Daoris's terminal panel (CONSOLE4), `daoris` is
  the install's, as it is for a session.

*As built (WSSETUP3): "where it exists" is read as the home's sibling `app/bin/` being a folder: in an install the home
is `data/`, so it is the install's own. Nothing else names it, so no file can point a child at another `daoris`. The
press's facts were LAYOUT7's already (`SetupTools`: `node` and its version, `daoris` and what it answered), read on the
same `PATH`, so they now find the install's. D124's note has the rest.*

### 1.4 The version pin, and two versions on one machine

- **D105 stands.** `init` writes `"source": "daoris@<version>"`, npm's spelling, and after the first publish
  `npx daoris@<version>` is how anyone outside Daoris runs the same tool. The press asks the `daoris` a child
  would find for its version, which in an install is the install's, and the quest names it. The quest's first
  step checks that `daoris --version` still prints it (§2.3).
- **D117's objection is real, and it is not the channel's.** npm serves every version to anyone who names one,
  and the hazard is the one §0.3 measured: an older tool rewriting a newer lock. So the tool refuses it. **`sync`
  and `upstream` refuse a lock whose canon version is newer than their own**, naming both versions and the
  command at the lock's (WSSETUP4). `check` and `status` already read the versions offline and say so.
- 🔴 **During `0.0.x` the version does not identify the canon.** Every commit says `0.0.1` until a release moves
  it, so two installs from different commits both answer `0.0.1`, and the guard cannot tell them apart. On one
  machine a republish moves forward with main, so a later `sync` moves a repository forward too. Between two
  machines on different builds it is open, and the first release closes it. It is recorded in §13.

*As built (WSSETUP4): `sync` refuses in every mode, a dry run and `--force` included, and `upstream` one file or
`--all`. The sentence above was not so of either reader: `check` never reads the canon (D8), so it cannot know, and
`status` offered the older canon as an update and said to run `daoris sync`. It now prints a `newer lock` line and
the command at the lock's version. D124's note has the rest.*

### 1.5 What else it answers

- **codex**: `agent` mode has no network (ACP3), so `npx` could never have run there. The install's CLI needs
  none. Whether codex's sandbox runs a program that lives outside the workspace is **not measured**.
- **The press's rule** becomes exact verbs (§2.4), which a judge passes and auto mode keeps.
- **LAYOUT10 stops waiting on the first publish** (§11).

## 2. The set-up quest, for a repository nobody adopted

### 2.1 Who may be set up: the refusals

The set-up's subject is a row that is addressable and neither adopted nor declared: every repository in the
owner's workspace. D117 §6.1's *not registered here* meant *no row on this machine*, while the code's word
*registered* means adopted and declared, which is the set-up's goal. So the refusals are said by what they find,
each with the door that answers it:

| The press finds | Answer |
|---|---|
| addressable, not adopted, declaring nothing | **the set-up's own case** |
| adopted, declaring nothing | not refused: the quest carries the knowledge step alone, titled *Declare and document what this repository owns* |
| no row for it on this machine | refused: *not on this machine's registry*, naming Repositories → *Add a repository* and `daoris import <folder> --workspace <name>` |
| a row with no root here | refused: *no checkout here*. It is a teammate's registration, and its own machine's driver can be asked (unchanged) |
| not drivable here, or this machine's starts ride the pipe door | refused: *not driven here*. An unadopted repository is carried only by a driven session on the protocol door (D70). Names Settings → Driver and `daoris driver drive <repository>` |
| its sessions run in the checkout, not a tree of their own | refused: a set-up rewrites what every later session reads, so it lands on a branch of its own (D51, D87). Names `daoris driver trees <repository> on` |
| no git history | refused (unchanged) |
| no `node` on the children's `PATH`, or one older than 22 | refused: *the doctrine tool cannot run here*. Names Settings → Tools and `daoris tool use node …` |
| no `daoris` on the children's `PATH`, or one that does not answer its version | refused: *the doctrine tool cannot run here*, saying what was found. An install from before WSSETUP2 carries none, and a republish does |
| adopted, on the agents layout, declaring something, `check` clean | refused: *already set up* |
| a set-up already open | refused, naming it (unchanged) |

### 2.2 What the quest carries

The title, *Set up this repository for every agent (2026-10-01)*, and a body in the canon's words, with no
decision numbers and no path of Daoris's own beyond the layout's file names (D117 §6.2):

1. **What is asked, and whose it is** (D117 §6.2, unchanged).
2. **What was read there**, from the line at a named commit: D117 §6.2's list; the README's title and what the
   repository is built with, in D77's words; how many entries the workspace's index holds for it, and from which
   files; and **the names this workspace knows its other repositories by**, names only, for `uses` (§2.5).
3. **The steps** (§2.3).
4. **What it writes, and what it never does** (§2.6).
5. **How to close it** (§2.7).

### 2.3 The steps

In order. They are the adoption playbook's, with the knowledge step new and the tool's check first:

1. **The tool.** `daoris --version` prints `<version>`. If it prints anything else, or `daoris` is not found,
   decline: *the doctrine command could not run here*, with what it printed.
2. **Take up the doctrine.** `daoris init --harness agents`, and read what it prints: the packs, the
   repository's own documents, and the records it seems to keep by role. `daoris sync --dry-run`, and read every
   `COLLIDES`, `DRIFTED`, `LEFT BEHIND` and `LINK` line. Keep a collision's mechanism in a document of the
   repository's own before taking the canonical principle (the playbook's steps 2 to 4). `daoris sync`. The core
   skill `set-up-documents` and the knowledge `development-documents` are now in this tree; read both.
3. **Initialise the knowledge** (§2.5).
4. **Write the brief** from the skill's template (D122 §2.9, step 1).
5. **Declare the documents and the rooms** (step 2). The knowledge documents of step 3 need no declaring: the
   index lists the knowledge tier from the files.
6. **Declare the safe work** (step 3).
7. **Verify.** `daoris sync`, `daoris check`, and the repository's own build and tests where they are a session's
   to run.
8. **Commit** on this branch, as each step lands.

### 2.4 The rule the press adds

D117 §6.3's press adds a rule for the doctrine commands, as the person's say-so, since every permission request
on the protocol door is refused (D52). With `daoris` on the `PATH`, the rule is exact verbs, never a runner:

`daoris --version`, `daoris init --harness agents`, `daoris analyze --json`, `daoris sync --dry-run`,
`daoris sync`, `daoris sync --force`, `daoris check`, `daoris status --json`, `daoris doctor`.

- **As `Bash(…)`**, and as `PowerShell(…)` once D122 §3.5's canary says a driven session on Windows uses that
  tool.
- **Never `upstream`**, which writes into the canon, and the canon of the install's CLI is inside the install's
  folder. Never `connect`, `import`, `retire` or any management verb: a set-up registers nothing (§3.5).
- **`sync --force` is in.** On a set-up's branch, in a tree grown from the line, the text it replaces is committed
  on the line and can be taken back. The judge's *discard* row (D122 §3.3) is about work that cannot be.
- **Its scope**: the repository's, for the single press, and the workspace's, once, for the workspace press
  (§4.3). Said before it is pressed, and taken back in Settings → Permissions.
- `git mv` is no longer the press's: UNBLOCK4 made it every repository's default (D122 §3.6).

### 2.5 Initialising the knowledge

This is the owner's *initialize the knowledge*. It writes what a neighbour's session needs from this repository,
and would otherwise look for in 28 checkouts or ask the person.

1. **The domain**, in `daoris.json`:
   - `summary`: one line, for someone who has never opened the repository;
   - `owns`: the areas where a change belongs here rather than anywhere else;
   - `accepts`: the kinds of work worth asking of it;
   - `uses` (D91): the workspace's repositories its code depends on, by the names the quest lists, where the code
     shows it (a client, a package reference, an address).
2. **Knowledge documents**, the repository's own, from the knowledge template beside `set-up-documents`, one per
   area a neighbour would ask about:
   - **what it owns, and where**: each area of `owns`, and the folders and entry points that hold it;
   - **its contracts and data**: what it exposes (interfaces, endpoints, messages, files it writes, tables it
     owns), what it takes from whom, their shapes, and which of them are promised and which incidental;
   - **the computations others depend on**: each figure, status or rule that another repository or a person reads
     from it, how it is derived, from which inputs, and where in the code;
   - in each, **where every fact lives**, a path and a symbol, so a reader can check it and a later session can
     keep it true.

   Each document's `applies_when` names the question a neighbour would be asking, since that is the line the
   index and the search show.
3. **What the knowledge is not**: a tour of the folders, anything a reader sees at a glance, the build commands
   (the brief and the gates file hold them), or the repository's history (its decisions record, if it keeps one).
4. **Truth before coverage.** A fact confirmed in the code is stated, with its place. One that could not be
   confirmed is written as not confirmed, with where it would be settled. One that only a neighbour knows names
   that neighbour. A set-up publishes no quest of its own: with twenty-nine set-ups, questions to each other would
   multiply sessions for answers the next set-ups write anyway (§7).
5. **Its own documents first.** A repository that already keeps knowledge (six in the workspace hold 16 to 195
   entries) is read before anything is written. A document that already answers is named in the close and not
   rewritten, and a new one says where the older one is.

**In the canon's words**, for the quest body and the playbook alike: *Write what a session in another repository
would need from this one and could not find: what it owns and where, what it promises and the shape of its data,
and how the figures others rely on are computed, each fact with the place in the code that holds it. Say which
facts the code did not confirm.*

### 2.6 What a set-up writes, and what it never does

**It writes**, in its own tree, on its own branch: `daoris.json` and `daoris.lock`; `AGENTS.md` and `CLAUDE.md`
(the region, the brief, the import); `.agents/`, with the canon's documents and its own knowledge; the
`.claude/skills/` mirrors `sync` writes; each room's `AGENTS.md`; the `safe` section of `daoris.gates.json`, and
the file where there is none; and a document that keeps a collision's mechanism.

**It never**:
- pushes, merges or opens a pull request (D87);
- writes outside its tree: the checkouts it may read are read only (D107);
- publishes a quest (§2.5, point 4);
- runs `connect` or `upstream` (§2.4, §3.5);
- sets `remote.join` or `remote.knowledge`: what may leave the machine is the person's call, and silence is local
  (D47 §4);
- changes a source, build or CI file. Anything it finds that needs one is named in the close, as work for a quest
  the person may publish.

### 2.7 How it closes

`done`, saying: the tool's version; each collision and how it was resolved; each twin retired and where its lines
went; the domain, in its words; each knowledge document, the question it answers, and each fact marked not
confirmed; what the brief took in and where each line it let go now lives; the documents, rooms and safe work
declared, and that the safe work waits for the person's yes (D122 §3.4); the budget and the root file's bytes;
what was chosen about links; and what it found that needs a quest. Or `decline`, with the reason.

### 2.8 The playbook says the same

`.claude/knowledge/adoption.md` gains *Initialise the knowledge* after the sync and before the brief, in §2.5's
words. LAYOUT7's composer test (D117 §10) holds that the body names each of the playbook's steps, and so it holds
the new one.

## 3. Registration follows the line

### 3.1 Who registers, and when

**The driver registers a repository from its line**, through the registry door the desktop's *add* already uses,
whenever the declaration on the line differs from what the row holds. It looks at three moments, and not at every
tick:

- **After Daoris moves a repository's line**: a landing under `merge` (D87), and the fast-forward of *Bring up
  to date* (D109). Under the owner's `branch` rule, that fast-forward is when a merged set-up first reaches this
  machine, and D112's default look already takes the repository, since it holds the set-up's landed branch.
- **Once when the driver starts**, for each repository with a checkout here.
- **When the person asks**: *Refresh* on the repository's row, and `daoris-driver register [--repository
  <name>]` (§4.5).

A git read per repository at every tick would be 29 processes every 15 seconds, for an answer that changes when a
line moves, and Daoris knows when it moves one. A line the person moves outside Daoris is read at the next start
or the next press.

### 3.2 From which root, read how

- **The row's root is the checkout, and stays.** A tree is never registered, by `connect`'s own refusal of a
  linked worktree, twinned here.
- **The declaration is read from the line, as git objects**: `daoris.json` and `daoris.lanes.json` at the line's
  commit, named. Never the checkout's working files, which may be on another branch or hold edits, and never a
  session's tree, which holds a declaration no review has reached (D122 §3.2's reason).
- **The line is D86's.**

### 3.3 What is sent

Exactly what `connect` would send from that manifest, its `registration()`: the packs, the domain with `uses` by
its rule, `join` and `shareKnowledge` as explicit booleans, the lanes as a list (`[]` for none), and the root, to a
local service only. Adoption is said as `connect` says it, by sending no `adopted: false` (D70). No workspace: silence preserves the wiring (D48). Registering does not
re-index, so the driver then asks for the refresh every door already has (`/api/refresh`), and the intake's room
reads the declaration from the next ask. The index reads the checkout's files, as it always has, so a checkout left
on another branch shows its older documents until it moves.

### 3.4 Refusals: on the row and in the log, never silent

| Found | Answer |
|---|---|
| no line known | nothing registered; *no line here*, with D86's doors |
| no `daoris.json` on the line | nothing registered; the row says *not set up on its line*, and names a set-up branch waiting for review where one is recorded (D102) |
| a manifest that does not read, or a field the CLI refuses | the row keeps what it held, and says why |
| a manifest that declares nothing | adoption recorded as it is (D70), the declaration left as the row held it, and the row says *adopted, declares nothing*. This is `connect`'s rule that an empty declaration is worse than none, twinned |
| a lanes file that does not read | refused whole, naming each problem, as `connect` refuses |
| a row with no root here | not this machine's: its own machine's driver registers it |
| a root that is a linked worktree | refused (an import never makes one; held anyway) |

Each outcome writes `registry.followed {repository, outcome}` to the machine log (D94): a name and a word from a
fixed list, never a path or a summary.

*As built (WSSETUP5): "after Daoris moves a line" is a record under the home, `lines-moved.json`, written where the
line moves (`SessionTrees`' merge and its fast-forward), whichever door pressed it; the next look follows each moved
line before it reads the registry, and a terminal's `trees land` or `trees sync` follows at once. "Once at start" is a
watch's (the shell's loop and `daoris-driver drive`), beside its first looks. A registration is sent only where the row
holds something else, and "the declaration left as the row held it" sends the row's own declaration back, since the
registry door replaces it whole. Each outcome's sentence is kept for the row in `registry-followed.json`. D124's note
has the rest.*

### 3.5 Why the driver, and not the session or `connect`

- **The session's tree is a linked worktree**, which `connect` refuses, and its declaration is not reviewed until
  it lands.
- **`connect` reads the folder it runs in**, and the checkout may be on another branch.
- **A set-up's rules do not reach the service**, which `connect` talks to.
- **Under `merge` the landing is the review** (D113), and Daoris moved the line itself. Under `branch` the merge is
  the owner's on the platform, and the fetch that brings it here is the person's press (D109).
- **It generalises.** Any landed change to a declaration, a domain reworded or a lane added, reaches the registry
  the same way. Today someone has to remember `connect`.

## 4. Setting up a whole workspace

### 4.1 The press writes a plan

*Set up every repository in this workspace* still publishes one quest per repository (D117 §9: never one quest
for several). It does not publish them at once. Under oldest-first and a cap of 2, twenty-nine set-ups published
together would hold both slots for hours, and every quest the person asked meanwhile would wait behind them. So
the press writes a plan, and the tick works it.

**The plan**, `<home>/setup/<workspace>.json`, holds the person's choices and the quests it published:

- `order`: the repositories, in §4.2's order, less the ones the person unticked;
- `atOnce`: how many of its set-ups are open at once. One by default, and never more than `cap − 1`, so other work
  always keeps a slot. At a cap of 1 it is one, and other work waits its turn;
- `pilot`: how many go first, two by default. Once they have all closed, the plan pauses itself and says so, until
  the person resumes it with what they cost in front of them (§7);
- `paused`;
- `published`: each repository's quest id.

**At each tick**, for each repository in order, its state is read from the facts: *set up* (registered from its
line), *open* (its quest open or taken), *waiting for review* (closed `done`, not on the line), *declined*,
*parked* (its strikes), or *to go*. While fewer than `atOnce` are open and the plan is not paused, the next *to go*
is published as the single press publishes it, judged again by §2.1 at that moment. A repository refused then is
skipped, and the plan says why. A declined set-up is never published again by the plan; the person presses for it
alone.

**Nothing in it is a count** (D58). The plan keeps choices and ids; progress is read from the quests and the
registry every time.

### 4.2 The order: what other work touches first

From the facts this machine keeps, highest first:

1. quests addressed to it from other repositories, or routed to it by the intake;
2. reads into its checkout by other repositories' sessions, from the conversation records' tool calls and the
   files they touched (D76);
3. sessions run in it;

and then by name. The press shows each repository with its counts. The person may untick any, or name some to go
first (`--first`). The pilot is the first two.

Value lands first that way, and the plan can be stopped at any point with what matters already set up. A
repository nothing touches may never need a set-up (§7).

### 4.3 The rule

The workspace press adds §2.4's rule once, at workspace scope, rather than one rule per repository. It says so
before it is pressed. The verbs are the same offline commands, each writing only in the tree it runs in.

### 4.4 What the person sees as each lands

- **On the Repositories view**, at the workspace's head, one line: *Setting up — 2 set up · 1 waiting for your
  review · 1 setting up · 22 to go · 1 declined — paused after the pilot*, with *Resume*, *Pause* and *Stop*.
- **On each repository's row**, its state, in the words of §4.1, and what each state links to: the session while
  it sets up; the branch while it waits for review, and that a *Bring up to date* after the merge registers it;
  the domain's summary once it is set up; the reason, when it declined or parked; what it cost, once it ended
  (§7.3).
- **In *What needs you***: the pilot's pause, a set-up that stopped to ask the person, and a declined one.
- **Notifications** as for any session that parks or ends unasked (SURF5b).
- **The review** of each set-up is its session's, on the work frame, as for any landed session (D52, D113).

### 4.5 The doors (D50, D110)

- **The screen**: Repositories → a workspace's *Set up every repository*. It opens the list (the order with its
  counts, a box each, *at once*, the pilot, the rule it adds, the landing rule it lands by, the agent that will
  carry it, and the pilot's cost once known), then the press.
- **The terminal**, on the headless binary beside LAYOUT7's `daoris-driver setup <repository> [--plan]`:
  - `daoris-driver setup --workspace <name> [--plan] [--at-once <n>] [--pilot <n>] [--first <repo>…] [--skip <repo>…]`.
    `--plan` prints the list and each refusal, and publishes nothing.
  - `daoris-driver setup --workspace <name> --pause | --resume | --stop`. *Stop* ends the plan. A quest it already
    published stays, and one nobody has started can be deleted (D95).
  - `daoris-driver register [--repository <name>]` (§3.1).
- **Ask Daoris**: LAYOUT8's `setup` kind gains the doors `workspace`, `pause`, `resume`, `stop` and `register`,
  judged against the same facts and applied as the press (D89). `HelpCoverageTests` holds each verb to its door.
  Each is a door and none is exempt. *Pause*, *resume*, *stop* and *register* change only what the person or the
  line already said; the workspace press widens, since it publishes quests and adds a rule, so it is a card the
  person applies.

## 5. A baseline that writes nothing: the README, as the repository's word

**Decided: yes.** Until a repository adopts, the service indexes its README as the repository's own word.

- **What.** For a repository with no lock (the scanner's own test for reading both roots), `README.md` at the
  root, in any case, is split at its headings as a log is (`MarkdownSections.Split`). Each section is an entry:
  kind *knowledge*, provenance *local*, path `README.md`, titled *README* for the part before the first heading
  and by its heading after. Never through a link or a link held as text (D117 §5.5). Not `docs/README.md`, and no
  other format, since only markdown has headings the splitter reads.
- **Until it adopts.** Once a lock exists, the README is not read. The repository has declared its documents by
  then (DOC3), and its README is its front page again.
- **Why yes.** Twenty-three repositories have nothing in the index. A search finds nothing in them, and a session
  then reads checkout after checkout to learn which one to read (§7), or asks. A README is what a repository's
  authors wrote for a newcomer (D77), and a session searching the index is one.
- **Why DOC5's objection does not reach it.** DOC5 refused to *guess a role*. A router is followed as the map of a
  repository's documents, and a site's front page taken for one sends a session the wrong way. A README entry
  claims no role: it carries its path, and it is one hit among others. A wrong hit costs a read; a missing one
  costs a question to the person.
- **What stays.** The intake keeps its own reading (D77), a title and a paragraph, for a different purpose. Code
  is never indexed: the index is of knowledge, and reading code is a session's own act (D107).

## 6. A session looks before it asks

### 6.1 The instruction

`TargetPrompt.Asking` is rewritten for every driven instruction: claiming, resuming and carrying on. Its middle
paragraph, asking another repository (D79, D107), is kept. A paragraph comes before it, and the last one narrows:

> Look before you ask. The quest, its links and its files; this repository's own documents, code and history (its
> log, and the commits that last changed what you are changing); the workspace's knowledge, through your
> connector's `knowledge_search`[; and the other checkouts listed above]. A question one of these settles is not
> a question: decide it, and keep what settled it for your closing note. Where the evidence leans one way without
> settling it, take that reading, carry on, and say in your closing note which reading you took and on what
> evidence, so the person can correct it in review rather than be stopped by it.
>
> *(the paragraph on asking another repository, unchanged)*
>
> Stop for the person only for what no source holds and only they can give — a sign-in, a go-ahead for an act
> outside this repository or on a production system, a preference nothing records. Then say exactly what and why,
> and what you looked at, in your last message, commit what you have, and end your turn with the quest still
> taken, rather than declining. The person answers, and you are started again here, in this tree, with their
> words.

- **The bracketed clause** is said only where the session may read across (D107). Where reading is off, the
  instruction names no checkout, as today.
- **What goes**: *a choice between options that is theirs*. A choice the sources settle is decided; one they lean
  on is taken and said; one nothing records is a preference, and stays the person's.
- **A reading taken is reversible by construction.** It is committed on the session's branch, said in the close,
  and reviewed with the diff, under `branch` by the person and under `merge` by the landed history (D113).
- **What stays the person's**: a sign-in (D89), and an act outside the repository or on a production system:
  `autonomous-development`'s carve-outs.

### 6.2 What it is held by

`AskAndWaitPromptTests` keeps its three phrases (*only the person can give*, *end your turn with the quest still
taken*, *rather than declining*) and gains: the look-first paragraph in each of the three instructions; the
sources named; the reading taken and said; the checkouts clause present only with reading across; and *a choice
between options* gone. No test in a desktop suite's `Process` half, no rehearsal (`tools/*rehearsal*.mjs`) and no
web `e2e/` spec names the old words; this design searched them.

### 6.3 The canon says it too

The prompt reaches every driven session, adopted or not, and nothing else. A session outside Daoris in an adopter
is first-class (D46), and reads the canon. So `autonomous-development` gains one bullet, before *A step that
genuinely needs a human choice surfaces as a decision, not a pause*:

> - **Look before you ask.** A question the task's own material, the repository's documents, code and history, or
>   a neighbouring repository's documents you may read can settle is not a question for the person: settle it,
>   and keep the evidence for the hand-over. Where the evidence leans one way without settling it, take that
>   reading and state it at the checkpoint, where it is reviewed with the rest of the outcome. Only what no source
>   holds and only the person can give reaches them mid-run: a sign-in, a go-ahead for an act outside the
>   repository or on a live system, a preference nothing records.

- **Project-agnostic** (canon-authoring): no product, no command, no path, and no service. *A neighbouring
  repository's documents you may read* is a file a session reads, and needs nothing running (D48 §2a).
- **The always-loaded region is unchanged.** The document is on-demand knowledge, and its frontmatter, the only
  part the index carries, does not change.
- An entry goes under `## Unreleased` in `canon/CHANGELOG.md`, and this repository and `examples/` are re-synced
  in the same commit (WSSETUP10).

## 7. What it costs

### 7.1 What is known

- **One set-up's cost is not measured**, because none has run. The pilot is the measurement (§4.1).
- **The one long driven turn measured** read 663K tokens anew and 68.5M from its cache, in 48 minutes and about a
  thousand tool calls, ending at 679K tokens of context (COST1, METER1). If every set-up cost that, the workspace's
  29 would read about 19M tokens anew and 2.0 billion from cache. That is the worst case the record implies, not a
  forecast.
- **What adoption adds to every later session** is the region at its start: 21,758 and 21,975 bytes in the two
  examples, measured by DOC2. Each session reads it once, and every later call reads it from the cache with the
  rest of the context.
- **The person's review** is the cost no record holds. Under `branch`, each set-up is a branch the owner pushes,
  opens and merges.

### 7.2 What bounds the spend, by construction

- **One at a time**, and never the last slot (§4.1).
- **The pilot's pause.** Two set-ups, then nothing more until the person has seen what they wrote and cost.
- **The order** puts the repositories other work touches first, so the plan can stop anywhere with the most useful
  part done.
- **The reading is bounded by the body**: confirm what you write; do not tour (§2.5). A set-up publishes no quest.
- **The driver's timeout** ends a session at 30 minutes by default. A session cut off is carried on in its tree
  (D80), committing as it goes so a carry-on loses little, and three cut-offs park the quest (D58).
- **COST1's levers** (a smaller window, the harness's own compaction) apply to a set-up as to any session once they
  are built. This design names no model: which agent and model carry a set-up is the person's (D98,
  `model-decoupling`).

### 7.3 What is measured, and where

- **Each set-up's usage**: the usage record's context high-water and window (D57 §4), its turns' tokens anew, from
  cache and out (METER1's split), its minutes and its tool calls. On its row once it ends, and in a set-up section
  of `tools/usage-report.mjs` (WSSETUP11). Absent is never zero, and no price is claimed.
- **What it saves**: the sessions in the workspace that ended holding their quest to ask the person (D83), per
  week, for a week before the plan runs and for the weeks after. That is the owner's complaint, counted. A
  `session.parked {session, repository}` line in the machine log makes it countable (WSSETUP11).

## 8. The build

Rows ready for `TASKS.md`. Lanes are `daoris.lanes.json`'s ids; *doctrine* and *docs* are the laneless groups.
D124 decides all of it, so no row takes a decision number unless building it finds something D124 did not decide.
A row with a rehearsal, a `Process`-half case or a look in its proof is proven by the parent at merge.

| Row | What lands | Lanes | What proves it |
|---|---|---|---|
| **WSSETUP9** | **A session looks before it asks** (§6.1, §6.2): `TargetPrompt.Asking` rewritten for the three instructions; the checkouts clause only with reading across | driver | `AskAndWaitPromptTests`, each new case failing first |
| **WSSETUP10** | **The canon says it** (§6.3): the bullet in `autonomous-development`; `canon/CHANGELOG.md` under `## Unreleased`; this repository and `examples/` re-synced in the same commit | doctrine | `verify`: the canon's scans, `check` clean, the region's bytes unchanged. The family rehearsal |
| **WSSETUP11** | **Set-ups and parks, counted** (§7.3): `session.parked` where the attention watch sees a park; the usage report's set-up section and parks per week by workspace. **Early**, so a week of *before* is logged | driver; tools | `SessionLog` and attention tests; the report's parse table |
| **WSSETUP4** | **An older tool never rewrites a newer lock** (§1.4): `sync` and `upstream` refuse, exit 1, a lock whose canon version is newer by number, naming both and `npx daoris@<the lock's version>`; the release rehearsal raises a consumer's lock and sees `sync` refused | cli; tools (the rehearsal) | `node --test` against a fixture canon at two versions, failing first. `npm run rehearse` |
| **WSSETUP8** | **The README as an unadopted repository's word** (§5) | service | Scanner tests: read with no lock, split at headings, path and titles, never through a link or held as text, never `docs/README.md`, gone once a lock exists; the golden test of an adopted repository unchanged |
| **WSSETUP2** | **The install carries its doctrine tool** (§1.2): `desktop-publish.mjs` packs `src/Daoris.Cli`, unpacks it into `app/cli/` and writes the two launchers into `app/bin/`; the install's readme says so | tools | `desktop-publish.test.ts` over the layout. The deployment rehearsal: the install's `daoris --version` prints the canon's version from Command Prompt and from Git Bash where the runner has one, and `check` runs clean in a scratch repository it syncs |
| **WSSETUP3** | **`daoris` on every child's `PATH`** (§1.3), after TOOLS5 and WSSETUP2: the tools' environment puts the install's `app/bin/` first where it exists; the press's facts gain Tools' `node` and its version, and the `daoris` a child would find and the version it answers | driver; modules; cli (the environment's twin) | The environment's table in both twins, byte for byte as before with no install. A `Process`-half case: a stub driven session's `daoris --version` |
| **LAYOUT7, amended** | **The set-up quest, as §2 says** (the row is open): the refusals of §2.1; the body of §2.2–§2.7, the tool's check and the knowledge step among the steps, `daoris` for `npx daoris@<version>`; the rule as §2.4's verbs; the playbook gains *Initialise the knowledge* | driver; cli (the table's twin); doctrine (the playbook); tools (the family rehearsal) | LAYOUT7's own proof, and the composer test holding the new step. The family rehearsal's stub session runs `daoris` from its `PATH`, with no network |
| **WSSETUP5** | **Registration follows the line** (§3), after LAYOUT7, whose line reader it shares: the three moments, the read as git objects, the composer, the refusals, `registry.followed`, `daoris-driver register`, and the row's *Refresh* route | driver; modules; cli (the twin of `registration()`) | The composer's table against `connect.ts`'s, line for line; a test for each refusal. A `Process`-half case: a scratch repository whose line gains a manifest by a `merge` landing is registered adopted and declared while its checkout is on another branch. The family rehearsal: `setup` under `merge`, the stub lands, and the row reads adopted and declared with no `connect` run |
| **WSSETUP6** | **The workspace plan** (§4.1–§4.3, §4.5's terminal), after LAYOUT7: the plan file, the tick that works it, the order, `atOnce` bound by `cap − 1`, the pilot's pause, `--pause`, `--resume` and `--stop`, a refusal at publish skipped and said, a declined set-up never re-published | driver | Plan tests for each, from fixtures. The family rehearsal: a workspace of two unadopted scratch repositories set up one at a time by the stub, pausing after a pilot of one |
| **WSSETUP7** | **The workspace on the screen and in Ask Daoris** (§4.4, §4.5), after LAYOUT8, FRAME1e and WSSETUP6: the head line, the rows' states, the press and its list, *What needs you*; the modules' routes; the `setup` kind's five doors; English and Chinese, named by D116 | modules; web-shell; service (the kind's box and tool); driver (the judge) | Modules tests (MOD5's three things per route); `HelpProposalKindsTests` and `HelpCoverageTests`; vitest over a mocked bridge; translation parity and the names check. The look on the window, both themes and both languages |
| **WSSETUP12** | **The pilot**, the owner's run, after a republish carrying WSSETUP2, WSSETUP3, WSSETUP5, WSSETUP6, WSSETUP9 and LAYOUT7: two repositories of the workspace, by the plan's pilot or named. An evidence document: what each wrote (the domain, the knowledge, the brief), its review, its cost, its registration after the merge, and a canary: a fresh driven session in a neighbour, asked something one set-up's knowledge answers, finds it by search and does not park | none (a run) | The evidence, with the owner present |
| **WSSETUP13** | **The rest**, the owner's run: the plan resumed with the pilot's numbers on the press. Evidence: each set-up's cost, and the workspace's parks per week, a week before (WSSETUP11) and after | none (a run) | The evidence |

**Order.** WSSETUP9 first: it acts on every driven session at once. WSSETUP11 early, for the week of *before*.
WSSETUP4, WSSETUP8, WSSETUP10 and WSSETUP2 any time, each in its own lane. WSSETUP3 after TOOLS5 and WSSETUP2. The
driver lane runs WSSETUP9 → WSSETUP11 → LAYOUT7 → WSSETUP5 → WSSETUP6 in sequence. WSSETUP7 after LAYOUT8, FRAME1e
and WSSETUP6. Then the owner's two runs.

**LAYOUT10**, amended: it no longer waits on the first publish. Its unadopted half is WSSETUP12. Its other half,
a repository with instruction files of its own and a link held as text among them, stays as written.

**Only WSSETUP10 changes the canon.** It re-syncs `examples/` in the same commit, and the family rehearsal holds it.

## 9. What a rehearsal can prove, and what only a real run can

**A rehearsal proves the mechanism**, with no model and no account: the install's CLI found on a child's `PATH`
and running offline; the version guard; the quest's refusals and body; a stub session running `daoris` and
landing; the registration read from a line, not a tree, after a merge landing; the plan's pacing and its pilot's
pause; a README indexed and gone after adoption; the instruction's words.

**Only a real run proves the rest:**

1. **Whether a real set-up writes knowledge that is true**: each fact at its place, and the unconfirmed ones said.
   Judgement, which only the pilot shows, and the owner's review of it.
2. **Whether a neighbour's session finds it and asks less**: the canary in WSSETUP12 once, and the parks per week
   over WSSETUP13.
3. **What one set-up costs**, and whether 30 minutes is enough for one.
4. **Whether each harness's shell finds `daoris` by its bare name** on the owner's machine: Claude Code's Bash and
   PowerShell tools on Windows, and codex's sandbox running a program outside the workspace.
5. **Whether the owner's review keeps pace** with one set-up at a time, and whether a pause between waves is
   wanted.
6. **That a real *Bring up to date* after a real merge** registers what the set-up declared.

## 10. Considered and rejected

- **Waiting for npm** (§1.1). It holds the arc on a press with its own unknowns, and even after it, the rule is a
  runner no judge passes and the fetch is one codex cannot make.
- **A machine path in the quest.** It works, and it lands in whatever the session commits: a brief, a room, a
  message (`sensitive-info`).
- **The tarball attached to the quest**, run with `npx` or `npm exec`. Still a runner with arguments, npm's cache
  written under the user profile, and a megabyte per quest.
- **A global npm install from the install** (`npm install -g`). It writes under the user profile (D63), and a
  global install is the machine's npm's to make (D121 §2.7).
- **The install's `app/bin/` on the account's `PATH`.** It changes every terminal on the account, which D105 refused
  for the home's variable for the same reason. Daoris's own children get it, and the person's terminals stay theirs.
- **A single-file executable of the CLI.** A second build of the CLI that no rehearsal tests. The tarball is the
  release's own artefact.
- **The driver or the service running the doctrine commands into the repository**, or the verbs as connector
  tools. Daoris writing into a repository (D32); the service starts no process (D46); and D117 §9's second
  implementation of D19's state space.
- **Registering from the set-up session** (`connect` in its tree). A linked worktree, a declaration nobody
  reviewed, and the network.
- **Registering at the set-up's `done`.** Under `branch`, the work is on no line yet.
- **Registering by re-running the service's import.** It reads the checkout's working files, not the line.
- **A read of every line at every tick.** Twenty-nine processes every 15 seconds, for an answer that changes when
  a line moves, and Daoris knows when it moves one.
- **Publishing every set-up at once.** Under oldest-first, other work waits behind all of them.
- **A chain of set-ups** (D65's `then`). A decline stops a chain, and a chain runs one at a time whatever the cap.
- **A priority for set-ups, or a quest status for *deferred*.** Oldest-first is one implementation, in the
  service's order (`Planner`), and a new status is a new transition in the lock (D79's reason).
- **The plan in `driver.json`.** Both twins would have to keep a field only the driver uses. The plan is the
  driver's alone, so it is the driver's file.
- **Ordering by name, or by size.** A name says nothing about use, and size is not value.
- **One quest for the workspace** (D117 §9).
- **Daoris drafting declarations into its registry** (D77): a claim nobody made, read as one.
- **Set-ups asking their neighbours.** Twenty-nine sessions asking each other, for facts the later set-ups write.
- **The README read after adoption too.** An adopter declares its documents (DOC3); declared, never guessed (DOC5).
- **Indexing code as a baseline.** The index is of knowledge. Reading code is a session's own act (D107).
- **The prompt alone, or the canon alone.** The canon reaches no unadopted repository (D70); the prompt reaches no
  session outside Daoris (D46).
- **No stop at all.** A sign-in and a go-ahead on a production system stay the person's
  (`autonomous-development`).
- **A model chosen for set-ups by Daoris.** `model-decoupling`; which agent and model run is the person's (D98).

## 11. What this amends

Each row that builds a piece notes the amendment where it lands.

- **D117 §6.1**: the refusals are §2.1's; the workspace press is a plan of single quests (§4).
- **D117 §6.2**: the steps are §2.3's: the tool's check, `daoris` in place of `npx daoris@<version>`, the knowledge
  step, the bounds (§2.6) and the close (§2.7).
- **D117 §6.3**: the press's rule is exact verbs (§2.4), at workspace scope for the workspace press.
- **D117 §6.5**: the driver registers a repository from its line, at three moments (§3).
- **D117 §6.6 and §9**: the doctrine command comes from the install; *the install carrying the CLI onto a session's
  path* is no longer rejected; LAYOUT10 no longer waits on the first publish.
- **D105 §2**: npm stays the channel outside Daoris and the manifest's `source`. Inside Daoris, the install's copy
  at the same version runs.
- **D121 §2.4**: the tools' environment carries Daoris's own `app/bin/` first.
- **D122 §3.9**: the press's rule is `daoris` verbs.
- **D79 and D83**: a session looks before it asks, and what reaches the person narrows to what no source holds.
- **The canon's `autonomous-development`**: one bullet (WSSETUP10).
- **The adoption playbook** (local): *Initialise the knowledge*.

## 12. The twins this creates

Each is added to `.claude/knowledge/twins.md` by the row that builds it, with its test tables:

- **A registration from a manifest**: `connect.ts`'s `registration()` and the driver's composer (WSSETUP5), with
  one table of manifests and the bodies they send.
- **The tools' environment with the install's `app/bin/`**: `tools.ts` and `Tools.cs` (WSSETUP3), TOOLS5's twin
  gaining its rows.
- **The set-up brief and the adoption playbook** (D117 §10), with the new step.
- **Not a twin**: the service's README reader (WSSETUP8) and the driver's `SelfDescription` read the same file for
  different answers, all of it by sections for search, a title and a paragraph for the intake. Named here so that
  nobody holds one to the other.

## 13. What this document's gate does not cover

This change is documents only, and nothing is built.

- **Read from the code at `21787b8`**: `Adapters.cs` (`TargetPrompt`), `Planner.cs`, `DriverConfig.cs`,
  `Intake.cs`, `connect.ts`, `manage.ts`, `commands.ts`, `cli.ts`, the CLI's `package.json`, `Registry.cs`,
  `RegistryImport.cs`, `RegistryModule.cs`, `RepositoryScanner.cs`, `tools/desktop-publish.mjs`, the MCP tools'
  names, and `AskAndWaitPromptTests.cs`.
- **The workspace's facts are the parent's measurement on the install** (§0.1), not read here, and the long turn's
  numbers are the backlog's (COST1, METER1). DOC5's note is on a branch merging next, read from it.
- **Not measured**: which shell each harness runs `daoris` from and whether it finds it by its bare name; whether
  codex's sandbox runs a program outside the workspace; what one set-up costs; whether a real set-up's knowledge is
  true.
- 🔴 **The version guard cannot tell two builds of `0.0.1` apart** (§1.4). On one machine a republish only moves
  forward; between machines on different builds, a `sync` can still move a repository back until the first release
  gives builds different versions.
- **`verify` checks** this document's links, the decision log's shape, the budgets and the duplicates, and none of
  these words.
