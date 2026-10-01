# One repository, every agent — the agents layout, and setting a repository up

> The owner's ask, 2026-10-01 (LAYOUT1 in `TASKS.md`, *one repository, every agent*): take the agent
> file and folder layout of dsh's own repository (`github.com/deepseek-ai/deepseek-harness`, public;
> D53 adopted dsh as a protocol), apply it to this repository and to every repository Daoris manages,
> and give Daoris a way to set one up. This is the contract for the LAYOUT rows, and its decision is
> **D117**. Status: **designed; the CLI's half (LAYOUT3: §2, §3, §5.1–§5.4), the service's (LAYOUT4: §5.5) and the set-up quest
with the layout read from a line (LAYOUT7: §6.1–§6.3 as D124 amends them; notes under D117 and D124) are built.** It extends
> `docs/2026-09-22-instruction-file-design.md` (D59), which moved only the always-loaded tier into
> `AGENTS.md` and left knowledge and skills under `.claude/`, and it enumerates D19's state space
> again for a move. Read with **D3** (no links), **D5**, **D13**, **D14**, **D18**, **D23**, **D32**,
> **D70**, **D110** and **D115**.

## 0. What is true today

### 0.1 This repository, read from its files

| What | Where | Read by |
|---|---|---|
| The project brief | `CLAUDE.md`: 24,405 bytes, 3,749 words against a ceiling of 3,750 | Claude Code and dsh (measured); **not codex** |
| The always-loaded doctrine | the `daoris:rules` region, which is all of `AGENTS.md` (22,758 bytes; the region is 22,672 of 26,000) | all three (measured) |
| The import | `CLAUDE.md`'s `daoris:import` region, `@AGENTS.md` | Claude Code |
| Knowledge | `.claude/knowledge/`: nine documents, six canonical and three local (`adoption`, `canon-authoring`, `twins`) | whoever the roster sends there, by path |
| Skills | `.claude/skills/`: seven, five canonical and two local (`add-pack`, `dispatch-subagent`) | Claude Code; dsh only through the profile Daoris writes (`customSkillDirs`, HELP2) |
| Claude Code's own | `.claude/settings.json` (tracked), `.claude/settings.local.json` and `.claude/worktrees/` (ignored) | Claude Code |
| Nested instruction files | none | — |

Three things found by reading, which the build must carry:

- 🔴 **`.gitattributes` names `.claude/knowledge/twins.md merge=union`** (D106). The attribute is by
  path. Moving the file without the line switches union merge off for the twins table, silently, and
  the next two branches that each add a row conflict.
- 🔴 **The service's `RepositoryScanner` reads `.claude/` whatever the manifest says**
  (`var target = ".claude"`), while `DaorisLock` reads the manifest's `target` to resolve the lock.
  A repository with any other target would have its knowledge and skills unread. Nothing has another
  target today, so nothing has shown it. (LAYOUT4 fixed it, §5.5.)
- **57 tracked files name `.claude/knowledge/` or `.claude/skills/`**: documents, code comments that
  point at `twins.md`, the examples' regions, and tests that use the paths as fixtures of today's
  layout. The fixtures stay, since that layout stays supported; the rest follow the move.

### 0.2 The reference, as the parent measured it on the owner's machine

This design read nothing outside its own tree; these are the parent's facts from a local checkout.

- `AGENTS.md` is the one instruction file, at the root and nested per package, about twenty of them
  (`packages/AGENTS.md`, `docs/AGENTS.md`, `scripts/AGENTS.md` and so on).
- `CLAUDE.md` is a git link (mode `120000`) to `AGENTS.md`, at the root and in a few folders.
- Skills live in `.agents/skills/<name>/SKILL.md`, with `references/` and `templates/` beside, and
  `.claude/skills` is a link to `../.agents/skills`. `.agents/skills/.gitignore` ignores
  `*/agents/openai.yaml`, per-agent skill metadata an agent generates.
- Decision notes live in
  `.agents/notes/{proposed,implemented,rejected,archived}/{feature,bug-fix,simplification,architecture,process,testing}/yyyy-mm-dd-topic.md`,
  bilingual (a `.zh.md` and an i18n sidecar), each folder with an `AGENTS.md` of its own.
- 🔴 **On a checkout with `core.symlinks=false`, the owner's, every link is a text file holding its
  target.** `CLAUDE.md` is 9 bytes reading `AGENTS.md`. Claude Code loads the path, not the
  instructions, and nothing says so.

The last fact is why §3 exists. The reference's layout is right, and its mechanism fails on exactly
the machine Daoris is developed and deployed on.

## 1. What each agent reads

The layout is built on what each agent reads, measured, and on nothing that is not. The dsh
evaluation (§6.5) measured the root files, ACP3's evidence note measured dsh's skill roots, and this
design's own session observed three more cells in Claude Code. Every other cell is **LAYOUT2**'s to
measure, and the layout is shaped so that no unmeasured cell can leave an agent without its
instructions.

| Agent (toolchain entry) | Instruction files | `@path` imports | A nested instruction file | Skill roots |
|---|---|---|---|---|
| Claude Code (`claude-code`) | `CLAUDE.md` at the root, **measured**; each `CLAUDE.md` in the folders above its working directory, **observed** | followed, **measured** | not measured; its maker documents one being read when a file in that folder is | `.claude/skills/`, its contract in `harness.ts` and **observed**; a nested `<folder>/.claude/skills/` as skills scoped to that folder, **observed** |
| Claude Code over ACP (`claude-code-acp`) | 🔴 **not measured**: whether the adapter loads project instructions at all | not measured | not measured | not measured |
| codex (`codex`) | `AGENTS.md`, **measured** (the binary's own strings) | not interpreted, **measured** | not measured | not measured. The reference ignores `*/agents/openai.yaml` inside `.agents/skills/`, per-agent skill metadata in a file named for codex's maker: evidence, not a measurement, that codex reads there |
| codex over ACP (`codex-acp`) | not measured | not measured | not measured | not measured |
| dsh (`dsh`) | `AGENTS.md`, `CLAUDE.md` and their `.local` overlays, **measured** | not interpreted, **measured** | not measured; the reference nests twenty, its makers' own convention | `<project>/.dsh/skills` and `<project>/.agents/skills` per session working directory, and `customSkillDirs` resolved once: **measured** from the shipped bundle (dsh evaluation §6.3, ACP3 §5) |
| a harness a plugin declares (D64) | unknown, per harness | unknown | unknown | unknown |

*Observed* means seen once, in the context this design's session ran in (Claude Code, at the build
that session ran on). The session was handed the worktree's `CLAUDE.md`, the
`AGENTS.md` it imports, and also the `CLAUDE.md` of the checkout the worktree sits inside, one folder
chain up. Its skill list carried the example family's skills as skills *scoped to `examples/game/`*,
from `examples/game/.claude/skills/`, and carried skills from another worktree's copy of the
examples under the ignored `.claude/worktrees/`. One observation at one version is weaker than a
probe and stronger than a guess, and it is labelled as what it is.

Four consequences decide the layout:

1. **`AGENTS.md` at the root is the one file all three read.** It carries Daoris's region already
   (D59). The repository's brief moves into it too (§4).
2. **`.agents/skills/` is the skill root most agents read.** dsh reads it natively, per working
   directory, so a skill there reaches a dsh session in any home, the person's own included, where
   Daoris writes no profile. The reference ignores per-agent metadata there in a file named for
   codex's maker. Claude Code does not read it: the reference would not link `.claude/skills` to it if it did. So the source lives in
   `.agents/skills/`, and Claude Code gets a copy (§3).
3. **No harness is measured reading a nested instruction file**, and a driven session starts at its
   tree's root (D51), where a rule that walks up from the working directory reaches nothing nested.
   So a nested `AGENTS.md` is never relied on to be loaded. The always-loaded roster names every
   room (§2.2), and a lane session's prompt names its own (§2.5). Where a harness loads one by itself,
   the probe records a bonus.
4. 🔴 **The most consequential open cell is the ACP adapters'.** DEPLOY1 could not tell whether
   `claude-agent-acp` ignores an untrusted room's settings or never reads project settings at all.
   If it never reads them, every Claude Code session driven over the protocol door runs without
   `CLAUDE.md`, and so without the doctrine, today, whatever the layout. LAYOUT2 measures that cell
   first. If it is empty, the fix is the door's, handing the instructions over at `session/new` as
   the connector already is, and it gets a row of its own.

A fifth question is a byte limit. Codex's maker documents a limit on the bytes of project
instructions it reads (32 KiB by default in its documentation; not measured here). A root `AGENTS.md`
over that limit loses its tail, and the tail is the region and its last rule. LAYOUT2 measures the
limit. Until it does, the root file is kept under the documented figure (§4.2), and `check` reports
the file against the smallest limit measured, never failing on it (D54).

## 2. The layout

```
repo/
  AGENTS.md                  the repository's brief, for every agent
                             + <!-- daoris:rules --> roster, rooms, core rules <!-- /daoris:rules -->
  CLAUDE.md                  <!-- daoris:import --> @AGENTS.md <!-- /daoris:import -->
                             (and anything only that agent needs)
  <room>/AGENTS.md           a folder's own instructions: the repository's, never Daoris's
  <room>/CLAUDE.md           the same import, one per room
  .agents/
    knowledge/<name>.md      read on demand: canonical, and the repository's own
    skills/<name>/SKILL.md   invoked by name, with its references/, templates/ and scripts beside
  .claude/
    skills/<name>/...        a MIRROR of .agents/skills/<name>/, written by sync, for Claude Code
    settings.json, hooks, worktrees/ ...   Claude Code's own, untouched
  daoris.json                "harness": "agents", "target": ".agents", "rooms": [ ... ]
  daoris.lock                what sync wrote and where, every mirror, every room's pointer
```

### 2.1 `.agents/` holds the two on-demand tiers

`knowledge/` and `skills/` move under `.agents/`. They are the only tiers that are directories (D7 as
amended by D59). The always-loaded tier stays a region in `AGENTS.md`, unchanged. The canon's
vocabulary does not move, since a document is still a rule, knowledge or a skill, and no canon file
changes: the canon names no layout, which is what makes a new layout a descriptor (D23) rather than
an edit to the doctrine.

The descriptor is **`agents`**, a second implementation beside `claude-code`, selected by the
manifest's existing `harness` field. It is the first descriptor that serves several harnesses: one
target, `.agents`, plus what each harness that does not read it needs, a pointer for Claude Code's
instruction file and a mirror for its skills. `claude-code` stays supported for every repository
that has not moved. A repository moves by its own manifest change (§5.4).

### 2.2 Rooms: nested `AGENTS.md`

A **room** is a folder whose `AGENTS.md` tells an agent where it is. The word is the one the driver
already uses for the help and intake folders it writes. The reference keeps one per package. A
room's text is the repository's own: its package's conventions, traps and gates, which Daoris cannot
know and does not write.

- **Declared, in `daoris.json`'s `rooms`**: a list of folders. Nouns, like everything in the manifest
  (D26). Declared rather than found: the CLI never runs git, and a walk of the tree meets build
  output and worktrees, as §1's observation of skills from an ignored worktree shows. Lanes and gates
  are declared for the same reason (D115).
- **`sync` keeps a pointer beside each room's `AGENTS.md`**: `<room>/CLAUDE.md` holding the import
  region, exactly as at the root (D59 §4). The lock records the rooms it wrote pointers for, so a
  room taken out of the list loses its pointer.
- **The roster lists them.** The always-loaded region gains a *Rooms* table, each room's path and
  its `AGENTS.md`'s first heading, and one sentence: *read a folder's room before changing anything
  in it.* That is telling rather than loading, which D59 rejected for the rules. A room is on-demand
  material, like knowledge, and telling is how the on-demand tiers have always reached an agent.
- **A declared room with no `AGENTS.md` is a fact**, and `check` fails on it, as it does on a stale
  index.

### 2.3 What stays under `.claude/`

What is Claude Code's own and nobody else's: `settings.json`, `settings.local.json`, hooks,
`worktrees/`, and any skill a repository keeps for that one agent. `.claude/skills/` then holds
Daoris's mirrors beside such a skill, and the lock tells them apart. `.claude/knowledge/` goes, and so
does a pre-D59 `.claude/rules/` once the repository moves its own rules. Nothing is mirrored into
`.claude/knowledge/`: no harness auto-reads knowledge, the roster sends an agent to it by path, and
any agent can follow a path.

### 2.4 Decision notes stay in `docs/`

Weighed against the reference's notes, and not moved:

- **The reference's notes do what Daoris's records already do.** A note's folder is its state:
  proposed, implemented, rejected, archived. Here the backlog holds what is proposed, `DECISIONS.md`
  what was decided and what it rejected, and the task archive what was built with its outcome; an
  amendment lands in the entry it amends. The dsh evaluation compared the two (its §3), found them
  converged in substance, and Daoris took the one piece it lacked, the mechanical *Rejected* line.
- **`.agents/` is what an agent is told or invokes.** The records are the project's history, read by
  people as much as by agents, and by the service's scanner, which splits `docs/DECISIONS.md` at its
  headings. Under a hidden folder they would be hidden from the people who review them.
- **The numbers are cited everywhere**: in code comments, tests, documents and the dispatch skill's
  reserved numbers. Per-note files would need a name per note and a map from every number.
- **The one real advantage is recorded.** A file per note never collides at merge, where one log
  needs union merge and a duplicate check (D106). That is the reason to reopen this if union merge
  fails again. It has not.
- **Bilingual records** stay the owner's call. The dsh evaluation left doctrine in two languages to
  the owner, and nothing here asks for it.

A repository that keeps notes the reference's way keeps them: they are its own files, invisible to
`sync` (D5). The service's scanner reading a notes tree is held until a repository in a workspace
keeps one (§7).

### 2.5 A lane's room (DEV1)

DEV1 tells a lane session its lane in the prompt (that design's §2.3). The room is where the lane's
own instructions live, so **a lane names its rooms**: `rooms`, a list of declared rooms, beside
`paths` in `daoris.lanes.json`. The prompt's lane section then says, in the canon's words: *this work
is for the `<id>` lane: <summary>. Its instructions are in `<room>/AGENTS.md`; read them, with the
repository's own `AGENTS.md`, before changing anything.* A quest across two lanes names each lane's
rooms.

- Two lanes may share a room: `web-shell` and `web-settings` share `src/Daoris.Web`.
- A lane may name none, and its session reads the root alone.
- A lane naming a room the manifest does not declare, or one whose `AGENTS.md` is not on the line,
  makes the lanes file unreadable, naming both. That is DEV1's own rule for a `gates` entry naming no
  declared gate.
- **A room's file belongs to the lane whose paths hold it** (`src/Daoris.Cli/AGENTS.md` is the `cli`
  lane's), so the lane that knows the folder keeps its instructions and the lane check needs nothing
  new. The pointer beside it is generated, and written by whichever lane runs `sync`, as the example
  family is re-synced today.

For Daoris: `web-shell` and `web-settings` name `src/Daoris.Web`; `driver` and `modules` name
`src/Daoris.Desktop`; `service` names `src/Daoris.Service`; `cli` names `src/Daoris.Cli`; `tools`
names `tools` and `src/Daoris.Devkit`; `records` names `docs`.

Starting a lane session inside its room's folder, so that a harness walking up from its working
directory finds the room by itself, was considered and rejected (§9).

## 3. No links: pointers and mirrors

D3 stands: materialization is always a real file. The reference is its measurement, since a link on a
checkout without links is a file holding a path. Each link the reference has becomes one of two
things Daoris writes, both committed, so a clone works with no tool installed (D48 §2a).

### 3.1 Pointers, for the instruction file

Where the reference links `CLAUDE.md` to `AGENTS.md`, Daoris writes the import region D59 already
writes at the root: `@AGENTS.md`, which Claude Code follows (measured) and the others ignore. The same
goes in each room. A `CLAUDE.md` with content of its own keeps every word and gains the region; one
that already imports needs nothing.

### 3.2 The skills mirror

Where the reference links `.claude/skills` to `.agents/skills`, `sync` writes **a copy of every skill
under `.agents/skills/` into `.claude/skills/`**, canonical and local alike, since Claude Code needs
the repository's own skills as much as the canon's.

- **Every file of the skill**, `SKILL.md` and whatever is beside it, except the skill's `agents/`
  folder. Per-agent metadata another agent writes there, which the reference ignores, belongs to that
  agent and not to the one the mirror serves.
- **`SKILL.md` carries a mirror header**, under the frontmatter (D14):
  `<!-- daoris: mirror of .agents/skills/<name>/SKILL.md for agents that read only .claude/skills —
  edit that file, not this; daoris sync rewrites this one -->`, and for a canonical skill it names
  `daoris upstream` as the way to promote an edit. Any other file is a byte copy: a script's first
  line may be a shebang, and a template may be in a format no comment fits.
- **The lock records each mirror**: its path, the source it copies and the hash of what was written,
  in a `mirrors` list beside `entries`. A mirror of a local skill is Daoris's file although its
  source is not. D5 is about what Daoris writes, and Daoris wrote the mirror.
- **Measured against the lock** (D13). A mirror that differs from what the lock recorded was edited
  here. One that matches the lock while its source changed is only behind, and `sync` renews it.
- **It retires when its harness stops needing it.** Which tier is mirrored, to which root, for which
  harness, is the descriptor's data. The day a probe shows Claude Code reading `.agents/skills/`, the
  entry goes, and `sync` retires every mirror by the cells of §5.4.

### 3.3 What a person who edits the mirror is told

Three times, each at a moment it can help:

1. **When they open it.** The header's one line names the source: D6's cheapest intervention at the
   only moment it matters.
2. **At `check` and `sync`.** `drifted .claude/skills/x/SKILL.md — a mirror of
   .agents/skills/x/SKILL.md, edited here`. `sync` refuses and says what to do: move the edit into
   the source and sync; or, for a canonical skill, `daoris upstream .claude/skills/x/SKILL.md`; or
   `sync --force` to discard it.
3. **At `upstream`.** A mirror's path is accepted and mapped to its canonical source. The edit, its
   header stripped, goes to the canon, and the next `sync` carries it to the source and back to the
   mirror by D13's *improved upstream, untouched here*. A mirror of the repository's own skill is
   refused: *nothing canonical to promote; the source is `.agents/skills/x/SKILL.md`*. A mirror whose
   source was edited too is refused naming both, because two edits are a merge only a person can
   make.

### 3.4 What it costs

The skills are in the repository twice, about 26 KB for Daoris's seven, and a skill change is a
two-file diff. A mirror of an unchanged skill costs nothing after its first write. The price buys a
layout that works the same on every checkout, which a link does not.

## 4. This repository first: the move, file by file

The move is Daoris's own change in Daoris's own repository. The example family moves as §4.4 says.

### 4.1 The files

| From | To | Moved by | Row |
|---|---|---|---|
| `.claude/knowledge/`: `autonomous-development`, `claims-need-checks`, `leak-repair`, `model-decoupling`, `reaching-in`, `translation-parity` (canonical) | `.agents/knowledge/` | `sync`, by the move cells (§5.4) | LAYOUT5 |
| `.claude/knowledge/`: `adoption`, `canon-authoring`, `twins` (local) | `.agents/knowledge/` | `git mv` before the sync: local files are the repository's to move (D5) | LAYOUT5 |
| `.claude/skills/`: `caveman`, `doc-loader`, `fix-log`, `pattern-finder`, `post-feature` (canonical) | `.agents/skills/`, mirrored back into `.claude/skills/` | `sync` | LAYOUT5 |
| `.claude/skills/`: `add-pack`, `dispatch-subagent` (local) | `.agents/skills/`, mirrored back | `git mv`, then `sync` writes the mirror | LAYOUT5 |
| `daoris.json` | `"harness": "agents"`, `"target": ".agents"`, `"rooms"` | by hand: the move's one decision | LAYOUT5 |
| `daoris.lock` | its `harness`, `target`, `mirrors` and `rooms` | `sync` | LAYOUT5 |
| `.gitattributes`: `.claude/knowledge/twins.md merge=union` | `.agents/knowledge/twins.md merge=union` | 🔴 the same commit as the file | LAYOUT5 |
| `CLAUDE.md`'s brief | the root `AGENTS.md`, above the region, and the rooms (§4.2) | written | LAYOUT6 |
| `CLAUDE.md` | the import region alone | written | LAYOUT6 |
| (new) | eight rooms: `canon/`, `docs/`, `src/Daoris.Cli/`, `src/Daoris.Desktop/`, `src/Daoris.Devkit/`, `src/Daoris.Service/`, `src/Daoris.Web/`, `tools/`; each `AGENTS.md` written, each `CLAUDE.md` by `sync` | written | LAYOUT6 |
| `.claude/settings.json`, `.claude/settings.local.json`, `.claude/worktrees/`, `.mcp.json` | stay | — | — |

### 4.2 The brief, split

`CLAUDE.md` is at its ceiling, one word to spare. Much of it is one artefact's detail that every
session in every other artefact pays for. The rooms are where that detail belongs, which is CANON6's
answer applied to the brief: split principle from detail, never raise the number.

| `CLAUDE.md` today | Goes to |
|---|---|
| *What this is*, four paragraphs | the root `AGENTS.md`, condensed |
| *Current state*: the counts, the three things to know, never editing the version by hand | the root |
| *Current state*: the code-gen IDE, the frame, the two desktop traps, the install and its deployment, the plugin kit, the conversation, the protocol door | `src/Daoris.Desktop/AGENTS.md` |
| *Current state*: the design language, and polishing the UI by looking | `src/Daoris.Web/AGENTS.md` |
| *Current state*: the toolchain | its CLI half to `src/Daoris.Cli/AGENTS.md`, its driver half to `src/Daoris.Desktop/AGENTS.md` |
| *The model, in three sentences* | the root |
| *Layout* | the root, with `.agents/` and the rooms |
| *Dev loop*: the gates, when each runs, no CI | the root, as one table |
| *Dev loop*: D19 before changing `sync`, `node --test`, `DAORIS_CANON` | `src/Daoris.Cli/AGENTS.md` |
| *Dev loop*: the desktop loop and the `Process` halves | `src/Daoris.Desktop/AGENTS.md` |
| *Conventions*: atomic LF writes, exit codes, plan and apply, comments, TDD and commits | the root |
| *Conventions*: TypeScript and ESM, zero runtime dependencies, the buildless loop, `bin/daoris.mjs`, the one network module | `src/Daoris.Cli/AGENTS.md` |
| *Conventions*: `isMain` | `tools/AGENTS.md` |
| *Writing canon files* | `canon/AGENTS.md` |
| The pointers to the README, the contract, the decisions, the roadmap, the backlog and the archive | `docs/AGENTS.md`, with the rules for writing each record; the root keeps one line to it |

**The root brief's ceiling is about 1,300 words**, so that the whole root file, brief and region, stays
under the limit codex documents (§1): about 8.5 KB of brief beside about 23.7 KB of region. LAYOUT2's
measurement may loosen or tighten that. Each room gets a ceiling with headroom over what it measures
when written. `doc-budgets` counts `AGENTS.md` **outside Daoris's regions**, since the region is the
canon budget's to measure, and `CLAUDE.md`'s ceiling goes with its text.

### 4.3 What else follows the move

- **The lane map.** `.agents/**` joins the laneless doctrine group (in `tools/lanes.json`, or
  `daoris.lanes.json` once DEV2 lands). `src/Daoris.Desktop/AGENTS.md` and its `CLAUDE.md` join the
  laneless documents group beside the desktop's `README.md`, since no lane's paths hold them. Every
  other room sits in the lane that owns its folder, or with `canon/` and `docs/` in the laneless
  groups. LEFT1's test refuses an unplaced file, so the row fails until each is placed.
- **The package.** `tools/stage-package.mjs` stages `canon/` whole into the npm package. It must leave
  out `canon/AGENTS.md` and `canon/CLAUDE.md`, which would otherwise ship inside every consumer's
  `node_modules`. The CLI's own room is outside the package's `files` and needs nothing.
- **References.** Each comment and document naming `.claude/knowledge/<x>` or a local
  `.claude/skills/<x>` follows the move, and a test in `dogfood.test.ts` holds that no tracked file
  outside the old layout's fixtures names one: a comment pointing at a file that moved fails
  silently. The CLI's tests of the `claude-code` descriptor keep their `.claude/` fixtures.
- **The records that describe the layout.** `README.md` (the consuming story), the CLI contract, the
  adoption playbook and the dispatch skill say the layout. The skill's *read `CLAUDE.md` and
  `AGENTS.md`* becomes *read `AGENTS.md` and your lane's room*.
- **Unchanged.** The driver's help and intake rooms already write an `AGENTS.md` and a `CLAUDE.md`
  import. The dsh profile's `customSkillDirs` stays while any repository on the machine is on the old
  layout (§7, held).

### 4.4 The example family

- **`examples/engine` moves** in LAYOUT5. Its local always-loaded rule in
  `.claude/rules/engine-mechanics.md` is the case `sync` reports and leaves to the repository, which
  moves it into its own part of `AGENTS.md`.
- **`examples/game` stays on `claude-code`.** The family then holds one repository on each layout,
  which is what a real workspace will hold for a long time, and game's local `world-streaming`
  document is the case a move refuses until the repository moves it. LAYOUT7's rehearsal phase moves
  a scratch copy of game through the set-up quest.

### 4.5 What stays Claude-only

Claude Code's settings, its hooks, its worktrees, `.mcp.json` at the root (codex's and dsh's sessions
get the connector on the protocol door, ACP4), and the skills mirror, which exists for it alone.

## 5. The canon's materialization

### 5.1 The manifest and the lock

**The manifest:**

- `harness`: `agents` for the new layout; absent, or `claude-code`, for today's.
- `target`: `.agents`. A target equal to a mirror root is refused.
- `rooms`: folders, relative to the repository. One that escapes the repository, sits inside the
  target or a mirror root, or is the repository's root, is refused (D18).

**The lock**, additively, as D71 added `switchedOff` (its `version` stays 1):

- `harness` and `target`: the descriptor and root its entries were written under. Absent means
  `claude-code` and `.claude`, the only layout written before. 🔴 **The lock, not the manifest, says
  where the files are.** Between a manifest's flip and the sync that moves the files, the manifest
  names the new root and the files are still at the old.
- `mirrors`: `{ path, of, sha256 }`, relative to the repository.
- `rooms`: the rooms whose pointers `sync` wrote.

An older build reading a manifest that says `agents` refuses the unknown harness and names what it
knows (D23). That is loud, which is right.

### 5.2 The commands

- **`init`** writes `"harness": "agents"`, `"target": ".agents"` and `"rooms": []`, and lists the
  repository's own documents under both roots. One under `.claude/knowledge/` or `.claude/skills/` is
  named with the `git mv` that moves it, since the index lists `.agents/` only. It reports a
  `CLAUDE.md` or `AGENTS.md` that is a link, or a link held as text (§5.4).
- **`sync`** plans and applies the cells of §5.4: the move, the mirrors, the pointers, the rooms in
  the roster, and the refusals. `--dry-run` prints `moved`, `mirror`, `pointer`, `LEFT BEHIND` and
  `LINK` lines beside today's. A folder the move empties is removed, and only when empty.
- **`upstream`** takes a source's path, a mirror's path (§3.3), or an old path after a move, which it
  answers with where the document went.
- **`check`** (and `inspect`, which `status` and the dogfood test read) gains four facts that fail,
  offline: a mirror drifted, missing, or behind its source; a declared room with no `AGENTS.md`; a
  room's pointer missing; a path Daoris writes that is a link or a link held as text. It reports,
  never failing: the root `AGENTS.md`'s whole size against the smallest limit LAYOUT2 measured (D54);
  a document in an old tier that no index lists; a skill under `.claude/skills/` that is the
  repository's own, read by Claude Code alone.
- **`doctor`** skips every mirror, which would otherwise score as a perfect twin of its source.
- **`analyze` and `status`** name the layout and, per harness, what reaches it, from the measured
  cells of §1. An unmeasured cell says *not measured*, never a guess.
- **`verifyHarnessContract`** checks `.agents/skills/` as it checks skills today: a skill without
  frontmatter installs and never fires, on any of the three.

### 5.3 The region and the provenance header

The roster's links point at `.agents/knowledge/` and `.agents/skills/`, the same number of characters
as `.claude`. So the region grows only by the *Rooms* table and one sentence: *skills live in
`.agents/skills/`; `.claude/skills/` mirrors them for the agent that reads only there; edit the
source.* That is about 1,000 bytes for Daoris's eight rooms, from 22,672 to about 23,700 of 26,000,
an estimate the build measures and reports (D54).

The source's provenance header is unchanged (D14). A mirror's `SKILL.md` carries the mirror header of
§3.2 in the same place, under the frontmatter.

### 5.4 The state space: D19, for a move

A **move** is a repository whose manifest names a root or a descriptor other than the one its lock
was written under. D19's table still applies to every document at its new root; the tables below are
what the move adds. They are enumerated before implementation, as D19 says.

**A canonical document the lock holds under the old root:**

| Canon | Old path vs lock | New path | Outcome |
|---|---|---|---|
| selects it | same | absent | **move**: write at the new root, delete the old, re-root the entry |
| selects it | same | same as the lock, or as the canon renders now | **finish the move**: delete the old. A move interrupted, or made by hand |
| selects it | same | differs | **collision at the new root**: the repository's own file there. Refuse (D12), naming both paths |
| selects it | differs, and its body is the canon's now | — | read as *same*, as D19 reads it: the edit was already promoted, and only the lock is stale |
| selects it | differs, and its body is not the canon's | any | **drift**: refuse. `upstream` the old path, or `--force` discards the edit. A move is a write, and drift means the repository changed the file (D59 §5's rule) |
| selects it | absent | absent | create at the new root and drop the old path (D19's *recreate*) |
| selects it | absent | same | adopt at the new root |
| selects it | absent | differs | collision at the new root: refuse |
| retires it | same | — | retire the old; nothing is written at the new |
| retires it | differs | — | **edited retirement**: refuse |
| retires it | absent | — | drop the entry |
| a confirmed switch turns it off (D71) | — | — | D71's rows, at the old root; nothing at the new |

**The repository's own documents**, which are not in the lock:

| Where | Outcome |
|---|---|
| in an old on-demand tier (`<old>/knowledge/`, `<old>/skills/`) | 🔴 **refuse the move**, naming each with the `git mv` that moves it. Left there, the index stops listing it and nothing else says so: the silent failure D23 names. Moving it is the repository's act (D5), and the command is one line |
| the same name under both roots | refuse: two copies, and which one is meant is the repository's call |
| an old `rules/` file (before D59) | reported, not refused. It is read by Claude Code alone; D19's retirement advice stands, and the report says where its text could go, the repository's own part of `AGENTS.md` |
| anything else under the old root: settings, hooks, worktrees | invisible (D5). The old root was never Daoris's, only its entries were |

**A mirror**, for each file of each skill under the new root's `skills/`:

| In the lock | Mirror on disk | Source | Outcome |
|---|---|---|---|
| no | absent | present | create |
| no | same as it would be written | present | adopt silently |
| no | differs | present | **collision**: the repository's own file at a mirror path, a skill it keeps for that one agent or one not yet moved. Refuse |
| yes | as the lock | present | renew when the source changed, else unchanged |
| yes | differs from the lock, same as it would be written | present | the lock only: the edit already reached the source |
| yes | differs from both | present, unchanged | **mirror edited**: refuse (§3.3) |
| yes | differs from both | present, a canonical source that drifted too | refuse, naming both; the source's drift refuses anyway |
| yes | absent | present | recreate; `check` reports it missing |
| yes | as the lock | gone | retire the mirror |
| yes | differs | gone | **edited mirror of a skill that went**: refuse. Nothing can receive the edit but a copy aside |
| yes | absent | gone | drop |

**A room's pointer:**

| In the manifest | In the lock | `<room>/AGENTS.md` | `<room>/CLAUDE.md` | Outcome |
|---|---|---|---|---|
| yes | — | absent | — | **refuse**: a room with no instructions |
| yes | — | present | absent | create it, the import alone |
| yes | — | present | imports it | nothing |
| yes | — | present | its own text | append the import region; every word kept |
| yes | — | present | damaged markers | refuse (D59 §4) |
| no | yes | — | holds the region | remove the region, and the file if the region was all of it |
| no | yes | — | no region | drop |
| no | no | — | — | invisible (D5) |

**A link**, for every path Daoris writes: `AGENTS.md`, `CLAUDE.md`, a room's pair, `.agents/`, the
mirror root and each mirror:

| The path is | Outcome |
|---|---|
| a link or a junction | 🔴 **refuse, never write through.** Writing beside and renaming replaces the link with a file holding a copy of what it pointed at: Claude Code then loads that text twice, and git records the link turned into a file. On a checkout without links, appending to the text is worse: git reads the edit as the link's new target, broken for every other contributor |
| a link held as text: a file whose only content is a relative path to something beside it, or a file where a folder must go | 🔴 **refuse**, saying what was seen: *this looks like a link checked out as text (`core.symlinks=false`); an agent that reads it reads a path.* Refusing on a probable is safe, and the sentence names both answers, a real file holding `@AGENTS.md` or a folder of mirrors, and that the choice is the repository's |

The link refusals are D59's *never guess at a boundary* in another shape. On the other side of the
guess is how the repository is laid out for its other contributors.

### 5.5 The service reads the same layout

`DaorisLock` and `RepositoryScanner` are the service's half of the twin (§10):

- **The root is the lock's `target`, else the manifest's, else `.claude`**, the order §5.1 gives. The
  scanner stops assuming `.claude` (§0.1).
- **Mirrors are skipped**, so a search finds each skill once per repository.
- **Each declared room's `AGENTS.md` is indexed** as a local knowledge entry. It is a folder's own
  reference, which is what the service's search exists to find.
- **A repository with no lock is read at both roots**, `.agents/` and `.claude/`, since an unadopted
  repository in the reference's shape keeps its skills in the first. A link held as text is skipped
  as a document.

### 5.6 How the rehearsals carry the upgrade

- **`npm run rehearse`**, the release rehearsal over the packed tarball, gains a *move the layout*
  phase. A consumer whose manifest names `claude-code` gets a local knowledge document and a local
  skill under `.claude/`. Its manifest is flipped. `sync --dry-run` lists the moves and the two left
  behind, and exits 1; `sync` refuses, naming each `git mv`; they are moved; `sync` moves the canonical
  files, writes the mirrors and re-roots the lock; `check` is clean. Then a mirror is edited: `check`
  fails naming the source, `sync` refuses, `upstream` from the mirror promotes it, and the next `sync`
  is clean. A room is declared and gains its pointer, then undeclared and loses it. A `CLAUDE.md`
  replaced by the 9 bytes `AGENTS.md` is refused with the link sentence, and where the runner can
  make a real link, a real one is refused too.
- **`npm run rehearse:family`** holds the family on both layouts, engine on `agents` and game on
  `claude-code`, both current and clean, and each document indexed once. LAYOUT7 adds the set-up
  phase (§6).
- **Unit tests** hold every cell of §5.4, each a failing test first.

## 6. Setting up a repository Daoris manages

> **Amended by D124** (`docs/2026-10-01-workspace-setup-design.md`, WSSETUP1), to be built with LAYOUT7:
> - **§6.1**: *not registered here* is said by what the press finds, each refusal with its door (that design's
>   §2.1). A repository that is addressable, not adopted and declaring nothing is the set-up's own case, never a
>   refusal. *Set up every repository in this workspace* is a plan of single quests, paced (its §4).
> - **§6.2**: the steps check the doctrine tool's version first, run `daoris` from the session's `PATH` in place of
>   `npx daoris@<version>`, and gain *Initialise the knowledge*: the domain and the knowledge documents a
>   neighbour's session would need (its §2.3–§2.7). The set-up writes only in its tree, and publishes no quest.
> - **§6.3**: the press's rule is exact `daoris` verbs, never `upstream` (its §2.4).
> - **§6.5**: the driver registers a repository from its line, after Daoris moves the line, at start and on the
>   person's press (its §3).
> - **§6.6 and §9**: the install carries its packed CLI and every child finds it on its `PATH` (its §1). The
>   rejection of *the install carrying the CLI onto a session's path* is reversed, and LAYOUT10 no longer waits on
>   the first publish.

Daoris never writes into a repository it manages (D32, `repository-owns-its-work`). A set-up is a
quest to that repository, carried out by its own session, which is the adopter's own agent the
adoption playbook asks for, on its branch, and landed by the workspace's rule (D87).

### 6.1 Three doors, one quest per repository

- **The screen**: *Set up for agents* on the repository's row in Projects and in its Manage dialog.
  For an adopter on `claude-code`, *Move to the agents layout*.
- **The terminal**: `daoris-driver setup <repository> [--plan]`. It lives on the headless binary
  because that binary already owns git, which the facts are read with (§6.5), and already publishes
  asks (`ask --to`). The CLI has no quest command (D32).
- **Ask Daoris**: a `setup` proposal kind with the door `setup`, judged against the same facts and
  applied as the screen's press (D89). `HelpCoverageTests` holds the verb to it, as it holds
  `trees sync` (D110).

Each press publishes **one ask to one repository**, through the ask door with `--to`, as the person's.
The ask names its receiver, so no intake session runs. Several repositories may be pressed in turn,
and a list may select several, but never one quest for several: each repository's session, branch
and review is its own. The title carries the day, so its content-derived id makes a second press on
the same day the same quest, and the press says so.

Each refusal says why: not registered here; no checkout here (a teammate's registration, which its
own machine's driver can be asked from); no git history; already on the agents layout and clean; a
set-up already open, named.

### 6.2 What the quest carries

A title, *Set up this repository for every agent (2026-10-01)*, or *Move this repository to the agents
layout (…)*, and a body in the canon's words: no decision numbers and no path of Daoris's own beyond
the layout's file names, since it is read in a repository that may know nothing of Daoris.

1. **What is asked, and whose it is.** Take up the shared doctrine in the layout every agent reads,
   or move to it. This is the repository's own change, made by its own session on its branch;
   nothing was written there from outside.
2. **What was read there**, from the repository's line at a named commit: `daoris.json` and its
   layout; `AGENTS.md` (absent, or how many lines of its own); `CLAUDE.md` (absent, its own lines, the
   import, or a link, and whether this machine's checkout holds links as text); what `.claude/rules/`,
   `.claude/skills/` and `.agents/skills/` hold; the folders with an `AGENTS.md` of their own.
3. **The steps**, from the adoption playbook, in order. `npx daoris@<version> init`, or for an
   adopter the manifest's two fields. Fill in `domain`. Move the repository's own documents out of
   `.claude/knowledge/` and `.claude/skills/` with `git mv`. Run `sync --dry-run` and read every
   `COLLIDES`, `DRIFTED`, `LEFT BEHIND` and `LINK` line: a collision is resolved by keeping the
   repository's mechanism in a document of its own before taking the canonical principle. Put
   instructions for every agent in `AGENTS.md`, above the region, and leave `CLAUDE.md` with the
   import and whatever only that agent needs. Declare the folders with their own `AGENTS.md` as
   rooms. Search the repository for anything that reads `.claude/skills/` or `.claude/knowledge/` by
   path: a CI step, a hook, a script. Run `sync`, `check`, and the repository's own build and tests.
   Commit.
4. **How to close it.** `done`, saying what was done: each collision and how it was resolved, each
   document moved, each twin retired and where its lines went, the budget, and what was chosen about
   links. Or declined, with the reason, including *the doctrine command could not run here*.

The version is the one the install's canon carries: D105's `daoris@<version>`, the manifest's own
`source`.

### 6.3 What the session is told, and what it is not

An unadopted repository's driven session is given the quest, the connector on the wire and the
repository's own instructions, and none of the canon (D70). So **the quest's body is its playbook.**
`.claude/knowledge/adoption.md` is Daoris's own document, local, and a session in another repository
cannot read it. The body is written from it, and the playbook gains the layout's steps in the same
row, so that the two say the same (§10).

The playbook's last step, leaving the diff uncommitted for the owner's review, becomes for a driven
set-up **the branch the workspace's rule lands**. Under `branch`, the owner reviews it where the pull
request is. Under `merge`, the landed history is the review (D113). The press says which before it is
pressed (§6.5).

**What the press adds, and says it adds**: a repository-scope rule letting that repository's sessions
run the doctrine commands at the pinned version, and `git mv` (Claude Code's own rules, D72). Over the
protocol door every permission request is refused by construction (D52), and a session that cannot
run the command cannot do the work. The press is the person's say-so, and the rule is taken back in
Settings → Rules. On another agent the posture is that agent's (ACP3): codex's `agent` mode runs
without the network `npx` needs, and the press says so when that agent would carry the quest.

### 6.4 A repository that already has its own instruction files

D59 rejected clobbering, and every case here keeps the repository's words:

- **An `AGENTS.md` of its own**: the region is appended, and nothing outside it is read, moved or
  rewritten.
- **A `CLAUDE.md` with content, and no `AGENTS.md`**: the import is appended and the brief stays.
  Codex reads only `AGENTS.md`, so the quest asks the session to move what every agent needs. That is
  the session's judgement, not a rewrite.
- **`CLAUDE.md` a link to `AGENTS.md`**, the reference's shape: `sync` refuses to write through it
  (§5.4). The link hands Claude Code the instructions where links are checked out, and a path where
  they are not. The quest says both, recommends a file holding `@AGENTS.md` because it works on every
  checkout, and leaves the choice to the session, which says what it chose.
- **`.claude/skills` a link to `.agents/skills`**: the same refusal. Removing the link lets `sync`
  write the mirror.
- **A `.claude/rules/` of its own**: read by Claude Code alone. The quest says so, and moving those
  rules into `AGENTS.md`'s own text is the repository's call.
- **An `.agents/skills/` of its own**: its skills are local, listed in the roster and mirrored. A
  same-named skill under `.claude/skills/` is a collision the session resolves.
- **Its own `.agents/notes/`, `docs/AGENTS.md` or anything else**: invisible to the tool (D5).

### 6.5 What the screen shows, per repository

The driver reads it from the repository's **line**, as git objects (D113's reads), since the line is
what the next session's tree grows from: the manifest, whether the region is there, `CLAUDE.md`'s
content and git mode (`120000` is a link), this checkout's `core.symlinks`, the skill roots, and the
rooms.

- **Adopted or not**, as today.
- **The layout**: *agents*; *.claude*, today's layout, where Claude Code reads the skills and the
  others the instructions only; *its own*, instruction files with no doctrine; *none*.
- **The agents it serves**: a row per agent the toolchain knows, and one per agent a plugin declares.
  Each reads *its instructions and its skills*, *its instructions only*, *a link's path, not the
  instructions*, *nothing of it*, or *not measured*, with its reason (*`CLAUDE.md` imports
  `AGENTS.md`*; *`CLAUDE.md` is a link, held here as the text `AGENTS.md`*). From measured cells only.
- **Rooms**: how many, and on the Manage dialog, which.
- **The press**, with the landing rule it will land by (*as a branch, for your review* or *merged into
  its line*), the agent that will carry it, the rule it adds, and the quest's text to read first.
- **A set-up waiting**: a session branch that holds the set-up and is not on the line yet, named.
- **Once the set-up is on the line**, the next read finds the manifest, and the registration's
  adoption and domain are refreshed from it as the dialog's *add* does (D70). The session runs no
  `connect`.

A registration with no checkout here shows *no checkout here*, and no press.

### 6.6 What the press cannot promise

- **The doctrine command comes from npm** (D105), and no release has run. Until the first publish, a
  session outside this workspace cannot run `npx daoris@<version>`, and declines saying so. The first
  publish is the owner's press. Inside this workspace, Daoris itself (LAYOUT5) and the example family
  run the CLI from source.
- **A protocol door's posture may not allow the command** (§6.3).
- **The session's judgement** over collisions, twins, the brief and a link is judgement, and only a
  real set-up shows how good it is.

## 7. The phased build

Each row is dispatchable as written, in the order listed. Lanes are DEV1's ids; *laneless* is the
doctrine and documents groups. D117 decides all of it, so no row needs a decision number of its own
unless building it finds something D117 did not decide. A row with a rehearsal check is proven by the
parent's serial run at merge, never in the worktree.

| Row | What lands | Lanes | What proves it |
|---|---|---|---|
| **LAYOUT2** | **The entry-point probe.** Every *not measured* cell of §1, per harness the toolchain knows, at its pinned version: instruction files at the root, above the working directory and below it; `@path` inside a nested file; the skill roots; whether a link held as text is read as text; the byte limit on instructions and what is cut over it; whether an ACP adapter loads project instructions at all; whether dsh lists a skill twice when two roots hold it. Keyless first (bundles, binary strings, `--no-prompt` handshakes), then one canary turn per harness, a word per file asked back, on the owner's login. An evidence document, each cell labelled by its tier | tools (the probe); laneless (the evidence) | The evidence. The turn half is the owner's to authorise, as ACP2's was |
| **LAYOUT3** | **The `agents` layout in the CLI** (§2, §3, §5.1–§5.4): the descriptor, its pointer and its mirror; `rooms`; the lock's `harness`, `target`, `mirrors` and `rooms`; `sync`'s move, mirror, room and link cells; `check`, `inspect`, `upstream`, `doctor`, `init` and `verifyHarnessContract`; D18 over the declared roots; the region's *Rooms* table and mirror sentence. The release rehearsal's *move the layout* phase | cli; tools (the rehearsal) | `node --test`: each cell of §5.4 a test, failing first. `npm run rehearse` (§5.6) |
| **LAYOUT4** | **The service reads the layout** (§5.5): the lock's root before the manifest's, mirrors skipped, rooms indexed, both roots for a repository with no lock, a link held as text skipped | service | Service tests for each rule, and a skill found once in a repository with a mirror |
| **LAYOUT5** | **This repository's doctrine moves** (§4.1, §4.3, §4.4): the local documents by `git mv`; the manifest; `sync`; the union line in `.gitattributes`, in the same commit; the lane map places `.agents/**` and the rooms' files; `examples/engine` moves; every reference follows, and a dogfood test holds that none names a moved path. **Run alone**: the reference sweep touches every lane | laneless; tools; cli (the dogfood test); each code lane, for its comments | `verify`, with `check` and LEFT1's lanes test. Both rehearsals, the family's with engine on `agents` and game on `claude-code` |
| **LAYOUT6** | **The brief moves** (§4.2): `CLAUDE.md` into the root `AGENTS.md` and eight rooms; `CLAUDE.md` the import alone; `doc-budgets` counts `AGENTS.md` outside Daoris's regions and budgets each room; `stage-package` leaves the canon's room out; `README.md`, the CLI contract, the adoption playbook and the dispatch skill say the layout | laneless; tools; each room's lane, for its own file | `verify`: the budgets, and a package test that the canon's room is not staged. A fresh session per harness in this repository names the brief's canary: LAYOUT2's turn, re-run on the moved repository, the owner's |
| **LAYOUT7** | **The layout facts and the set-up quest** (§6.1–§6.3, §6.5's facts): the driver reads a repository's layout from its line (git objects, modes, `core.symlinks`); the entry-point table, twinned with the CLI's; the quest's composer; `daoris-driver setup <repository> [--plan]`; the rule the press adds. The verb is listed as a door owed to LAYOUT8's kind (D110) | driver; cli (the table's twin); tools (the family rehearsal) | Driver tests: the table, the composer, and in the `Process` half a scratch repository with a mode-`120000` `CLAUDE.md` under `core.symlinks=false`. `HelpCoverageTests`. Family rehearsal: `setup game --plan` names the old layout and the document left behind; `setup game` publishes; a stub session in game's scratch copy runs the CLI and lands; the facts then read *agents* |
| **LAYOUT8** | **The layout on the screen, and its Ask Daoris door** (§6.1, §6.5): Projects' row and the Manage dialog's *Agents* section, the press with its landing rule, agent, rule and text; the refresh after a set-up lands; the modules' routes and doors; the `setup` kind (the service's box and tool, the driver's judge, the card); English and Chinese | modules; web-shell; service; driver (the judge) | Modules and service tests; `HelpProposalKindsTests` and `HelpCoverageTests`; vitest over a mocked bridge; translation parity; the look on the window in both themes and both languages, the parent's |
| **LAYOUT9** | **A lane names its rooms** (§2.5), once DEV2 and DEV6 have landed: `rooms` in `daoris.lanes.json`; the lanes reader's refusals; the prompt's lane section names each room; Daoris's lanes name theirs | driver; records | Lanes-reader and prompt tests. Family rehearsal: the laned example's session is told its room |
| **LAYOUT10** | **The first real set-ups**, the owner's run: one unadopted repository of the real workspace through the press, and one with instruction files of its own, a link held as text among them if one exists. Evidence: what each session did with the collisions, the brief and the links, and what it cost | none (a run) | The evidence, with the owner present. It waits on the first publish (§6.6) |

**Held, each with its trigger:**

- **dsh's `customSkillDirs`** (HELP2) stays while any repository on the machine is on the `.claude`
  layout. LAYOUT2 says whether dsh lists a skill twice when the mirror and `.agents/skills/` both
  hold it. If it does, a row is written then to keep that root only while such a repository exists.
- **The scanner reading a notes tree** (§2.4), once a repository in a workspace keeps one.
- **Retiring the `claude-code` descriptor**, once no registered repository is on it.
- **Retiring the mirror**, once a probe shows Claude Code reading `.agents/skills/`.
- **The protocol door handing instructions over at `session/new`**, if LAYOUT2 finds an ACP adapter
  that loads none (§1).

## 8. What a rehearsal can prove, and what only a real adopter can

**A rehearsal proves the mechanism**: every cell of §5.4 over real files; the packed tarball moving a
consumer; the service indexing each document once; the facts read from real git objects, a link's
mode among them; the quest published and carried by a stub session; the refusals. No model and no
account.

**Only a real run proves the rest:**

1. **What each harness loads**: LAYOUT2's turn half. Artefact evidence says what a program contains,
   not what a session is handed.
2. **Whether an ACP adapter loads project instructions at all**, and so whether driven Claude Code
   sessions have had the doctrine.
3. **Codex's limit against the real root file**, once the brief has moved.
4. **A real session's judgement in a set-up**: collisions, twins, a brief moved out of `CLAUDE.md`, a
   link replaced or kept.
5. **That the command resolves** on the adopter's machine after the first publish, and that the
   agent's posture lets it run.
6. **What the repository never told Daoris**: a CI step, a hook or a script that reads
   `.claude/skills/` or `.claude/knowledge/`. The quest asks the session to search for them; only its
   answer says whether it found them all.
7. **The reference itself as a workspace member**: links held as text, twenty rooms, notes of its own.

## 9. Considered and rejected

- **Links, the reference's own mechanism.** D3, and measured failing on the owner's machine (§0.2):
  a checkout without links holds a path, and nothing reports it.
- **`.agents/skills/` alone, with no mirror.** Claude Code does not read it, which is why the
  reference links `.claude/skills` to it. Every skill would vanish for the harness that drives most
  of this family's sessions.
- **The source in `.claude/`, mirrored into `.agents/`.** The source belongs where most agents read,
  and the copy with the one that looks elsewhere; and the ask was the reference's layout.
- **A mirror of knowledge.** No harness auto-reads knowledge, since the roster sends an agent to it by
  path. A mirror would double every search hit for nothing.
- **Leaving the brief in `CLAUDE.md`.** Codex does not read it, and the ask was every agent.
- **The region before the brief in `AGENTS.md`**, so that a limit that cuts the tail takes the brief
  rather than the rules. D59 appends the region after an adopter's own text, and reordering would
  move every adopter's words. The answer to a limit is a smaller file, which the rooms make possible,
  measured and reported.
- **Rooms found by walking the tree.** The CLI never runs git, and a walk meets build output and
  ignored worktrees. Declared, like lanes and gates.
- **Rooms in the lanes file only.** A repository with no lanes has rooms (the reference has twenty
  and no lanes), and the pointers are `sync`'s, which does not read the driver's file.
- **Daoris writing a room's text.** It is the repository's knowledge of its own package. Generated
  rooms would be doctrine nobody chose (D23).
- **Starting a lane session inside its room's folder.** A session's commands, gates and paths are the
  repository's, from its root (D51); a lane may span rooms; and walking up from a working directory
  is unmeasured for two of the three agents.
- **Decision notes under `.agents/notes/`** (§2.4).
- **Renaming the manifest's `harness` field to `layout`.** An alias kept forever, for a word.
- **Daoris replacing a repository's link with a file.** The link is the repository's tracked choice,
  seen by its contributors on every system. Daoris refuses and names it, and the repository's session
  chooses.
- **Setting a repository up by writing into it from the desktop**, as the Manage dialog writes
  `daoris.json`. A set-up rewrites what every future session reads, which is the owner's review and
  the repository's own agent's judgement over collisions and twins (D32).
- **One quest for a whole workspace.** Each repository's session, branch and review is its own.
- **Syncing from the driver or the service**, for instance as a connector tool. A second
  implementation of D19's state space, in another language, would twin the hardest table in the
  project.
- **The install carrying the CLI onto a session's path before the first publish.** D105 chose npm as
  the one channel, and a second channel is a second version of the doctrine tool on one machine.
- **Bilingual records**, as the reference keeps them: the owner's call, and not asked here.
- **Daoris writing `.agents/skills/.gitignore`.** That is repository mechanics, and the mirror leaves
  each skill's `agents/` folder out by itself.

## 10. The twins this creates

Each is added to `.claude/knowledge/twins.md` (`.agents/knowledge/twins.md` once LAYOUT5 lands) by the
row that builds it, with its test tables:

- **The layout**: the manifest's `harness`, `target` and `rooms`, and the lock's `harness`, `target`,
  `mirrors` and `rooms`. The CLI's `config.ts` and `materialize.ts`; the service's `DaorisLock` and
  `RepositoryScanner`; the driver's layout reader (LAYOUT7). The order *the lock, then the manifest,
  then `.claude`* is in all three tables.
- **What each harness reads**: §1's measured cells, in `harness.ts` and the driver's layout reader. An
  unmeasured cell is *not measured* in both, never a default.
- **A link held as text**: the CLI's refusal, which reads content, and the driver's reader, which
  reads the git mode first and then content. The same content cases are in both tables.
- **The set-up brief and the adoption playbook**: the driver's composer and the playbook, where a
  test holds that the brief names each of the playbook's steps.
