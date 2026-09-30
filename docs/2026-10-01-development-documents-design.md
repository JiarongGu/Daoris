# The development documents, and fewer asks — a standard for code generation

> The owner's ask, 2026-10-01 (DOC1 and UNBLOCK1 in `TASKS.md`, *Doctrine, plugins and tools*):
> *"remember daoris still have role for doctrine (and we do need to research a good development doc
> pattern for code generation and use it as standard for all repo setup so its good for sessions, and
> the goal is to unblock the repo as far as possible so less ask human permission during
> development)"*. This is the study and the contract for both, and its decision is **D122**. Status:
> **designed; nothing is built.** It builds on D117's layout (`docs/2026-10-01-agent-layout-design.md`)
> and LAYOUT2's measurements (`docs/2026-10-01-entry-point-evidence.md`, in the integration merging
> next), and on the permission scopes (D72–D74, `docs/2026-09-24-permission-scopes-design.md`). Read
> with **D7** as amended by **D59**, **D23**, **D26**, **D28**, **D48** §2a, **D54**, **D70**, **D81**,
> **D94**, **D106**, **D115** and **D117**.

## 0. What is true today

### 0.1 The canon names the records and never says where

The core canon already depends on a repository keeping records, and names them by their job:

| Canon file | The records it names |
|---|---|
| `task-lifecycle` (rule) | the backlog, the archive, the changelog |
| `persist-working-state` (rule) | the decisions record, the knowledge or conventions record, the plan or backlog |
| `no-global-memory` (rule) | a table of homes: the always-loaded rules, the on-demand knowledge, the decisions record, a skill, the changelog |
| `skills-workflow` (rule) | the generated index, the discovery skills |
| `doc-loader` (skill) | *the repository's own documentation router* |
| `fix-log` (skill) | *the repository's fix log* |
| `post-feature` (skill) | the status or roadmap entry, the user-facing log, *the document a newcomer would read* |

None says where any of them is. That is deliberate (canon-authoring: no layouts specific to one
repository), and it costs every session in every adopter the same search: find the backlog, find the
decisions, find the router, before the work can start. The generated index is the one place the
canon already sends a reader for repository specifics (D14), and it lists rules, knowledge and
skills, but not records.

### 0.2 This repository's documents

| Document | Role | Size today | How it is read |
|---|---|---|---|
| `CLAUDE.md` | the brief | 24,405 bytes, 3,749 words of a 3,750 ceiling | always, by Claude Code and dsh; not codex |
| `AGENTS.md` | the doctrine region | 22,758 bytes; the region is 22,672 of 26,000 | always, by all three |
| `docs/README.md` | the router: each document, its kind and its standing | 11,762 bytes | at a task's start, by `doc-loader` |
| `docs/DECISIONS.md` | decisions, each with *Rejected* from D51 on (a dogfood test) | 459,794 bytes | by lookup; the service splits it at headings |
| `TASKS.md` | the backlog: open rows only, the state, the handover | ceiling 6,600 words | whole, when picking work |
| `docs/task-archive.md`, `docs/FIX-LOG.md`, `CHANGELOG.md` | the archive, the fix log, the changelog | append-only, union-merged (D106) | by lookup |
| `.claude/knowledge/` | nine on-demand documents | — | by path, from the roster |
| `daoris.gates.json` | the gates, the devkit's configuration | eleven gates | by the devkit, the merge tool, and (DEV5) the queue |

Five documents carry word ceilings in `tools/doc-budgets.json`, reported and never failed (D54,
DOCS2). The append-only records carry none, by that tool's own reasoning: they are read by lookup
and grow by design.

### 0.3 What a driven session is handed

A quest session's target (`TargetPrompt.Claiming`, `Adapters.cs`) says *do the work inside this
repository under its own doctrine and gates* and does not say where either is. The harness loads the
repository's instruction files by its own rules (LAYOUT2): Claude Code, on both doors, `CLAUDE.md`
and its `@AGENTS.md` import; codex, the `AGENTS.md` chain from the project root, cut at 32,768 bytes;
dsh, `AGENTS.md` and `CLAUDE.md`. An unadopted repository's session gets none of the canon (D70); it
gets the repository's own files and the quest.

### 0.4 What asks today, door by door

"Asks" means every point at which a harness would stop for a person. Nothing Daoris starts has a
person at the prompt, so on every door an ask is a refusal (D52, D72 §1):

| Door | Posture | What it is handed | What a request becomes |
|---|---|---|---|
| Claude Code, pipe (`claude-code`) | `--permission-mode acceptEdits` | Daoris's composed rules as `--settings` (D72): `connector`, `commit`, `no-push`, `tree-guard`, the machine's, the workspace's and the repository's rules, the kept-files read (INT4j), the across rules (D107) | a denial; the harness has nobody to ask |
| Claude Code, protocol (`claude-code-acp`) | `auto`, else `acceptEdits` (D81) | the same file, as `_meta.claudeCode.options.settings` | `session/request_permission`, refused by its option's kind (`Acp.cs`, `AnswerPermissionAsync`) |
| codex, protocol (`codex-acp`) | `agent`: workspace-write, approvals by codex's own review, **network off** (ACP3) | nothing (D72: a rule is Claude Code's only) | the same refusal |
| dsh, protocol | `DSH_PERMISSION_MODE=workspace-write` | nothing | the same refusal, for anything escalating past its sandbox |

Two measured failures shaped the rules as they are. ACP2's session made its edit and was refused
the commit in a folder nobody had trusted, which gave `commit` (D72 as amended). FG5's sessions did
the work and then stopped at every command not on a short list (`npm` scripts, a generator, `git
merge`), each refusal becoming a proposal waiting for the person, which gave `auto` (D81). D81 also
rejected *growing the handed allow-list command by command*: it never catches up with a real
repository's tooling.

**Where a refusal goes today.** On the protocol door it is a console line, a `Note` event in the
conversation record, and the refused call's status `refused` (`Acp.cs`, `Refused`). `SessionLog`
writes no machine-log line for either, and the log's own `refused` event is a different thing: a
refusal a person met on the screen (REFUSE1). On the pipe door the harness reports its denials in the
stream (§1.2) and `ClaudeStreamJson` does not read them. So **nothing counts asks per session
today**.

## 1. The study

### 1.1 What a code-generating session needs, unasked

A session that must stop to ask, or that guesses, costs the person twice: once for the stall, and
again when the guess is reviewed. What removes each stop:

| The session needs | Because without it it | Answered here today by |
|---|---|---|
| **The brief**: what this is, the one constraint everything serves, what it must never do | reads the code to reconstruct a purpose, and misses the constraint that is not in the code | `CLAUDE.md` (Claude Code and dsh only) |
| **How to build, test and verify**, as exact commands, and which one means *done* | guesses a command, runs the whole suite, or stops at *looks done* | `CLAUDE.md`'s dev loop; `daoris.gates.json` |
| **What it may run without asking**, and what it must not | is refused, then proposes, declines or parks (§0.4) | the rules Daoris hands (D72), in no file the repository owns |
| **The layout and the rooms**: where things live, what is generated, what not to edit | edits a generated file, or a mirror, or the wrong twin | `CLAUDE.md`'s layout; D117's rooms, designed |
| **The conventions and traps** that differ from the language's defaults | writes the language's default, which is wrong here | `CLAUDE.md`; `.claude/knowledge/` |
| **The decisions**, with what each rejected | re-litigates a settled question, or reverses it | `docs/DECISIONS.md` |
| **The open work**, and the contract each row names | builds from the row's words alone | `TASKS.md` |
| **The glossary**: the words people and code use for this repository's things | names a thing twice, in two words | the web's glossary (D116), for the interface only |
| **Where each of these is** | searches, every session | nowhere generated (§0.1) |

### 1.2 The patterns in use

Each read from its own source; the column says what each keeps and how big it lets it get.

| Pattern | The always-read file | Nested files | On-demand tier | Records | Size discipline | Verification | Permissions |
|---|---|---|---|---|---|---|---|
| **This canon** (D59, D117) | a region in `AGENTS.md`, `CLAUDE.md` importing it | rooms, designed (D117) | knowledge by path, skills by name, both listed in the generated index | named by job, placed by the repository (§0.1) | the region in bytes, reported (D28, D54) | the repository's own gates, *done means gates green* (`autonomous-development`) | not in the documents: the carve-outs are prose, the allowances are Daoris's rules |
| **This repository** | `CLAUDE.md` + the region | none | `.claude/knowledge/` | a router, a numbered decision log with *Rejected*, a backlog, an archive, a fix log, a changelog | five word ceilings, reported | `npm run verify` and the declared gates | none in the documents |
| **dsh's repository** (public) | root `AGENTS.md`, *"standing orders: rules an agent needs in context in every session, one to three lines each, linking its home"*, eleven sections under its title | an `AGENTS.md` per package and subtree, about twenty | subsystem references, cookbooks, `.agents/skills/` | `.agents/notes/` by lifecycle folder, postmortems | word ceilings, **gating**: root 1,960, subtree 600 (packages 750), architecture 2,400, with *relocate → condense → raise* | *"Run relevant checks locally"*, a pre-push skill; *"never default to the full suite"* | prose: its brief says what an agent may do, including a push after its checks |
| **The `AGENTS.md` convention** | `AGENTS.md`, *"a README for agents"* | the nearest file wins | — | — | none stated | *"Build and test commands"*, *"Testing instructions"* | *"Security considerations"*, prose |
| **Claude Code's maker** | `CLAUDE.md`, or `AGENTS.md` where no `CLAUDE.md` is in or above the project (2.1.277+) | loaded when a file below is read | skills (name and description at start, body on use); path-scoped rules | — | *"target under 200 lines per CLAUDE.md file"*; a file over 4 MiB skipped | *"Give Claude a way to verify its work"* | *"CLAUDE.md … not enforced configuration"*; allowances are settings, hooks and modes |
| **codex's maker** | `AGENTS.md` (and `AGENTS.override.md`), one per folder from the git root down | concatenated, closest last | `.agents/skills/` | — | **32 KiB for the whole chain** (`project_doc_max_bytes`), the tail cut | *"working agreements (testing, dependency management)"* | sandbox, approval policy, and `.rules` files (execpolicy) |
| **GitHub Copilot's maker** | `.github/copilot-instructions.md`; `AGENTS.md`, nearest wins; `CLAUDE.md` or `GEMINI.md` | `.github/instructions/*.instructions.md` by `applyTo` | — | — | *"no longer than 2 pages"* | *"document the sequence of steps … bootstrap, build, test, run, lint"*, *"validation steps"* | — |
| **Gemini CLI's maker** | `GEMINI.md`, configurable to `AGENTS.md` (`context.fileName`) | the hierarchy to the git root and below | `@file` imports | — | — | — | — |

The sources are listed in §9. dsh's repository is read from its public files at its default branch;
its notes lifecycle is as D117 §0.2 records it.

### 1.3 Where they converge

Two ecosystems arriving at one rule independently is the bar this project believes (D17). Six
things clear it:

1. **One short file every agent reads, and it is `AGENTS.md`.** The convention, codex, Copilot and
   dsh read it natively; Gemini reads it once configured; Claude Code reads it where there is no
   `CLAUDE.md`, and through an import where there is. D59 and D117 already chose it.
2. **The file holds what cannot be derived, and not what can.** Claude Code's maker: include *"Bash
   commands Claude can't guess"*, *"Common gotchas"*, *"Architectural decisions specific to your
   project"*; exclude *"Anything Claude can figure out by reading code"*, *"File-by-file descriptions
   of the codebase"*. dsh: *"one to three lines each, linking its home"*. This repository's
   `post-feature` prose pass hunts the same slop.
3. **Exact commands, and a check that means done.** Every maker says it. Claude Code's calls
   verification *"the difference between a session you watch and one you walk away from"*; Copilot's
   asks for *"explicit validation steps"*; dsh's brief sends the agent to a checks skill; this canon
   says *done means gates green*.
4. **Detail on demand.** Skills, path-scoped rules, subsystem references, cookbooks, the knowledge
   tier: each keeps the always-read file short by moving what one task needs out of what every task
   pays for.
5. **A ceiling on what is read whole, and none on what is read by lookup.** dsh ceilings its standing
   orders and not its notes; this repository ceilings five documents and not its append-only
   records; Claude Code's maker says 200 lines; codex cuts at 32 KiB; Copilot's says two pages.
6. **One home per fact, and a record of why.** dsh's notes carry *"the why, what-was-given-up, and
   required verification"*; this repository's decisions carry *Rejected*, gated from D51.

### 1.4 Where they differ, and what this takes

- **Notes by lifecycle folder, or one numbered log.** D117 §2.4 weighed it and kept the log; nothing
  here reopens it. The standard names a *decisions record* by its job, and a repository that keeps
  notes the other way declares its notes folder as that record (§2.7).
- **A ceiling that gates, or one that reports.** dsh's rejects; this canon's reports (D54: a
  judgement reports). Kept: the standard's ceilings report. What gates is a fact (§2.8).
- **Words or bytes.** People and models pay in words and lines; the harnesses cut in bytes. The
  standard counts both, each for its own reason (§2.3).
- **Permissions in prose.** dsh's brief tells the agent it may push after its checks; the
  convention suggests a *security considerations* section. Claude Code's maker is explicit that an
  instruction file shapes what the agent tries, not what the harness allows. A sentence cannot stop a
  command, and a command a sentence allows is still refused where the harness was not told. So the
  standard keeps permissions out of prose and in a declaration a tool reads (§3). The brief says only
  where the declaration is.
- **A layout section.** Claude Code's maker's `/doctor` cuts *"directory layouts … and architecture
  overviews"* as derivable; dsh keeps a 320-word layout. The difference is what the section holds.
  A tour of folders is derivable. *Which folder is generated, which is a mirror, which is a room with
  its own instructions, what must not be edited* is not. The standard's layout section holds only the
  second kind.

### 1.5 What sessions read: loaded, opened, and not yet measured

**Loaded at start** is measured, from each harness's shipped code (LAYOUT2): the root instruction
files per harness, an import's depth, the skill roots, and the byte limits. Nothing here re-measures
it.

**Opened during the work** is not measured anywhere. The parent's account in this row's dispatch is
that the sessions that worked the owner's ticket AR-2201 read the repository's `.claude/knowledge/`
documents. No tracked document records it, and the transcripts are the owner's, not this design's to
read. On D77's account none of that workspace's 29 repositories had adopted, so what those sessions
opened was the repository's own tier, found with no roster pointing at it. That supports one
claim: **a session opens an on-demand tier by itself when it finds one.** It cannot say which
documents, how often, whether before the first edit or after a failure, on which harness, or whether
it helped.

What would need measuring, and how (DOC7):

- **Per session, which roles were opened**: the brief, a room, a knowledge document, a skill, the
  router, the decisions, the backlog, the archive, the fix log. The conversation record already keeps
  each tool call and the files it touched (`SessionEvent.ToolKind`, its locations). A machine-log line
  `session.read {session, adapter, role, beforeEdit}` names the **role** a read resolved to, from the
  repository's declared documents (§2.7). It never names a path or a word (D94 §5). A read that
  resolves to no role writes nothing.
- **Per session, which skills were invoked**, by name, which is an identifier (D94 §4's rule for a
  field).
- **Once, over the sessions already recorded**: the same count run by the owner over the conversation
  records on their machine. That is the only *before* there is.
- **The questions it answers**: whether the router is opened before the first edit; whether knowledge
  is opened because the roster named it or found by search; whether a room's instructions are read
  before its files are changed; whether sessions on different harnesses read differently. The
  standard's ceilings and the roster's wording are then revised from the counts.

### 1.6 Five findings the study turned up

Each is recorded here because it bears on the standard or on fewer asks. None is fixed in this
branch.

1. **An adopter's default core budget and codex's cut disagree.** The CLI's default always-loaded
   budget is 30,000 bytes (`DEFAULT_CORE_BUDGET_BYTES`, `config.ts`). codex reads the root
   `AGENTS.md` chain up to 32,768 bytes and cuts the tail (LAYOUT2 §2). A region at its default
   budget leaves at most 2,768 bytes for the repository's own brief before codex loses the region's
   last rules, silently. BUDGET1, the owner's open call on what the budget caps, is where that is decided.
   The standard's report (§2.8) makes the sum visible meanwhile.
2. **A push can pass the no-push default in auto mode.** `no-push` denies `Bash(git push)` and
   `Bash(git push:*)`. Claude Code's maker documents that such a rule *"doesn't stop"* `git -C . push`
   or `git -c push.default=current push`, and that auto mode's classifier allows by default *"Pushing
   to any branch of the repository you're working in"*. D81 put the protocol door in auto mode. So a
   push written in another form reaches the classifier, which allows it. This is read from the
   maker's documentation, not from a turn. UNBLOCK4 answers it (§3.7).
3. **A repository can already widen a session without anyone's yes.** A project skill's
   `allowed-tools` pre-approves its tools for the turn that invokes it, and the maker says workspace
   trust *"never gates a skill's `allowed-tools` in any session"*, `-p` in an untrusted folder
   included. A canonical skill carrying the field would widen every session in every adopter, and
   would arrive by `sync`. None does today. DOC3 holds that none will (§2.8).
4. **Package-manager rules with a wildcard are dropped in auto mode.** Entering auto mode, Claude
   Code drops *"broad allow rules that grant arbitrary code execution"*, *"package-manager run
   commands"* among them, and keeps *"narrow rules like `Bash(npm test)`"*. A declaration translated
   into `Bash(npm run *)` would hold on the pipe door and vanish on the protocol door. Hence exact
   rules (§3.5).
5. **Not every declared gate is a session's to run.** This repository declares eleven gates. The
   dispatch skill forbids a subagent four of them: the two real-process halves, `test:web`, and the
   deployment rehearsal. They are heavy, need the machine quiet, or start windows.
   DEV5 gives a gate `kind` and `quiet` for the queue's sake. The same two fields say which gates a
   session may run (§3.1).

## 2. The standard (DOC1)

### 2.1 The roles

A **role** is a document's job. The canon speaks in roles; the repository binds each to a path (§2.7);
the index says where (§2.7). Every role but the brief is optional, and a repository that keeps one
declares it.

| Role | Its one job | Read | Ceiling |
|---|---|---|---|
| **brief** | what this is, the constraint everything serves, the non-negotiables, the layout's non-obvious half, the command that means done, where everything else is | always: the repository's part of the root instruction file | yes (§2.3) |
| **room** | one folder's conventions, traps and checks (D117) | on demand, when working there; loaded by some harnesses when a file there is read, never by codex | yes |
| **knowledge** | a deep dive one area needs, with `applies_when` and `enforces` | on demand, listed in the index | no; its index row is the cost |
| **skill** | a procedure, invoked by name | its description always; its body on use | its description (the harness caps it) |
| **router** | each document, its kind (contract, method, study, evidence, record) and its standing (current, amended by, superseded by) | whole, at a task's start | yes |
| **decisions** | numbered decisions, each with why, what it rejected, and what the gates do not cover | by lookup | no |
| **backlog** | open work only, each row naming its contract and its proof | whole, when picking work | yes |
| **archive** | finished work, each with its date and outcome | by lookup | no |
| **fixes** | root cause, fix and verification per non-trivial defect | by lookup | no |
| **changelog** | what a user of a release sees changed | by lookup | no |
| **glossary** | the names people and code use for this repository's things | by lookup | no |
| **gates** | the checks, their commands, and the work a session may do unasked (§3.1) | by tools | — |

What the table leaves out is also a decision. **Contracts** (design documents) are listed by the
router rather than declared one by one; they come and go too often for a manifest. **Notes kept by
lifecycle folder** are a repository's own shape of the decisions role, declared as its folder. A
**roadmap** is a backlog's sequence, and a repository that keeps one lists it in the router.

### 2.2 Always read, on demand, by lookup

- **Always read**: the brief and the doctrine region, in the one root file every agent reads. It is
  the only file measured to reach every harness: codex never loads a nested file, and loads the root
  chain only up to its byte limit (LAYOUT2).
- **On demand**: rooms, knowledge, skill bodies, the router, the contracts. The index names each, and
  telling is how the on-demand tiers reach an agent (D117 §2.2).
- **By lookup**: decisions, archive, fixes, changelog, glossary. Read for one entry, never whole. The
  service already splits the first three at their headings (`RepositoryScanner`).

A fact goes in the brief only if **nearly every task in the repository needs it**. That is the
canon's own test for its always-loaded tier (canon-authoring), applied to the repository's own part.

### 2.3 Ceilings

A document read whole has a ceiling; a record read by lookup has none (DOCS2's reasoning). Two units,
because two things are paid:

- **Bytes, for the root file as a whole**: brief and region together under the smallest limit among
  the agents the repository serves. Today that is codex's 32,768 bytes, which cuts the tail, and the
  tail is the region. Reported against the root file, as D117 §5.2 already designs, never failing.
- **Words and lines, for attention.** The standard's starting ceilings, each set with headroom over
  what a document measures when written and raised deliberately, never silently:

| Document | Starting ceiling | Where the number comes from |
|---|---|---|
| the brief | 1,500 words and 200 lines | Claude Code's maker's 200 lines; dsh's 1,960 words; D117's 1,300 words for this repository, which leaves room under 32 KiB beside a 23.7 KB region |
| a room | 600 words | dsh's subtree ceiling |
| the router | 2,500 words | this repository's measures 1,845 today |
| the backlog | 5,000 words | this repository's ceiling is 6,600 and it measures 6,492, its handover included |

They report (D54). The discipline when one goes over is DOCS2's: relocate to the tier that holds it,
condense, and only then raise.

### 2.4 The brief's template

The repository's part of the root instruction file, above the region. Each heading is kept, and a
section with nothing to say says *none*, so a reader can tell an empty section from a forgotten one.

```markdown
# <Repository> — the brief

## What this is
<Three to five sentences: what it is for, who uses it, and the one constraint every change serves.>

## Before changing anything
- <A non-negotiable, one line, and a link to where its reason lives.>
- <Three to seven of them. A rule with no home is a rule nobody can judge an edge case by.>

## Layout
<Only what reading the tree would not tell: which folders are generated, which are mirrors, which
are rooms with their own instructions, what must never be edited by hand, where each artefact's
contract is. Not a tour of the folders.>

## Build, test, verify
- Done means: `<the one command>`.
- <The few commands a session runs most, exact. The rest are in the gates declaration.>
- <What never to run, and why: a gate that needs the machine quiet, a command that publishes.>

## Conventions
<Only those that differ from the language's and the tools' defaults.>

## Where things are
<Generated by the doctrine tool from the manifest (§2.7). Without the tool, written by hand: the
router, the decisions, the backlog, the archive, the fixes, the changelog, the glossary, the gates.>
```

**What a brief never holds**: status (*built*, *not yet*), which rots in place; history, which
belongs to the decisions; counts, which belong to the one place that keeps them; a quotation of the
owner, which belongs to the decision it motivated; a permission, which belongs to the declaration.

### 2.5 A room's template, and the records' shapes

A room (D117 §2.2) holds `## What this folder is`, `## Conventions here`, `## Traps`, and `## Checks`
(the declared gates that cover it, by name). Each is shorter than the brief's, and none repeats a
repository-wide rule; it links to it.

The records' shapes are the ones this repository keeps, stated without its names:

- **A decision**: a number that only increases; `Decision`, `Why`, `Rejected` (the alternatives
  weighed, never invented after the fact), `What it amends`, `What the gates do not cover`. An
  amendment is appended to the entry it amends.
- **A backlog row**: an identifier; what and why in two sentences; the contract it builds against;
  its proof; where known, its lane and a `file:line`. A finished row moves to the archive
  (`task-lifecycle`).
- **An archive entry**: the row's words as they stood, the date, and one paragraph of outcome.
- **A fix entry**: the `fix-log` skill's shape.
- **The router's row**: the document, its kind, what it is for, and where it stands, naming the
  decision that amended it.

### 2.6 Where it lives in the canon, and why

Three homes were possible.

- **A pack.** Packs are opt-in by stack (localized UI, a desktop app, SQL storage). Six of the core
  rules and skills name the records (§0.1). A dependency of core cannot be optional.
- **The adoption playbook.** It is this repository's own local document. A session in another
  repository cannot read it, which is why D117 §6.3 made the set-up quest's body its playbook.
- **Core.** Chosen, as two files:
  - **Core knowledge, `development-documents`**: the roles, the three reading tiers, the ceilings'
    principle, the brief's content test, and *permissions are a declaration, not a sentence*. On
    demand, so it costs the always-loaded region one index row, not its body.
  - **Core skill, `set-up-documents`**, with `templates/` beside its `SKILL.md` (the brief, a room,
    a decision entry, a backlog row, the router): the procedure for setting up or repairing a
    repository's documents. The canon ships a skill's whole folder (`canon.ts`, `tierFiles`), so the
    templates arrive with it.
- **Not a rule.** The always-loaded core is at 22,672 of 26,000 bytes before D117's rooms table,
  which adds about 1,000. The part of the standard every task needs is *where the records are*, and
  the generated table (§2.7) carries that as data rather than prose.

**The cost**, estimated and to be measured by DOC2 and DOC3: two index rows of about 450 bytes, and
the *Where things are* table of about 400 bytes for a repository that declares its documents. Beside
D117's rooms, that brings this repository's region to about 24,500 of 26,000.

**Canon-authoring holds.** Neither file names a product, a build command or a path specific to one
repository. They say *the root instruction file*, *the gates declaration*, *the generated index*,
and leave the file names to the index and the descriptor, which already know them (D14, D23).

### 2.7 Roles bound to paths: `documents` in the manifest

The manifest gains `documents`, a map from role to path, with an optional ceiling:

```json
"documents": {
  "router":    { "path": "docs/README.md", "words": 2500 },
  "decisions": "docs/DECISIONS.md",
  "backlog":   { "path": "TASKS.md", "words": 6600 },
  "archive":   "docs/task-archive.md",
  "fixes":     "docs/FIX-LOG.md",
  "changelog": "CHANGELOG.md",
  "brief":     { "words": 1300 },
  "room":      { "words": 600 }
}
```

- **Nouns, so the manifest** (D26). A string is a path with no ceiling. `brief` and `room` take a
  ceiling and no path, since the root instruction file and the rooms are the descriptor's and D117's.
- **The roles are a closed set the CLI knows.** An unknown role is refused, naming the known ones, as
  an unknown harness is (D23). A path that escapes the repository, is the root, sits inside the
  doctrine target or a mirror root, or is a link or a link held as text is refused (D18, D117 §5.4).
- **`sync` renders a *Where things are* table into the region**, one row per declared role: the role,
  the path, and the role's job in the CLI's fixed words. A repository that declares nothing gets no
  table and no change, so the examples need no re-sync until they declare.
- **`init` and `analyze` name candidates by role** from conventional names (a changelog, a decisions
  file, an ADR folder, a fix log, a backlog by its usual names), and write none. Declaring is the
  repository's act, done by its session in a set-up (§2.9).

### 2.8 What `check` holds, and what it reports

D54: a fact gates, a judgement reports.

| `check`, offline | Kind |
|---|---|
| A declared document's path does not exist | **fact, fails** |
| A declared path escapes the repository, or is a link or a link held as text | **fact, fails** |
| The region's *Where things are* table differs from the manifest | **fact, fails**, as a stale roster does (`indexStale`) |
| A declared room has no instruction file (D117) | **fact, fails** |
| A declared document is over its ceiling in words | judgement, reported |
| The root instruction file, brief and region together, over the smallest measured harness limit | judgement, reported (D117 §5.2's report, one line) |
| No backlog or decisions role is declared | judgement, reported: *the canon's records have nowhere to point here* |

And one fact held by the canon's own tests rather than by `check`: **no canonical skill carries
`allowed-tools`** (finding 3). DOC3 carries that test, in the CLI lane beside the other canon scans.

### 2.9 The set-up quest carries it (LAYOUT7)

D117 §6.2 composes the set-up quest's body from the adoption playbook, because the session in the
other repository cannot read the playbook. The standard joins it as three steps, in the canon's
words:

1. **Write the brief** from the template, moving into it what every agent needs from an existing
   `CLAUDE.md`, and nothing a reader could derive.
2. **Declare the documents** the repository keeps, and the rooms.
3. **Declare the safe work** (§3.1): the gates a session may run, the build and test commands, the
   lockfile install.

The close lists each. The playbook gains the same steps in the same row, and LAYOUT7's composer test,
which holds that the body names each of the playbook's steps (D117 §10), holds these too.

### 2.10 Without the tool

The canon must not hard-require Daoris (D48 §2a). Every piece of the standard is a committed file: a
repository with no tool writes its *Where things are* section by hand, keeps its ceilings by
judgement, and its gates declaration is read by whatever reads it. The only service anywhere in the
standard is none. What the tool adds is the table kept true and the facts checked.

## 3. Fewer asks (UNBLOCK1)

The shape, in one sentence: **the repository declares what is safe, the person says yes once, and
Daoris hands each harness that yes in its own words.** The yes is per repository and per widening of
its declaration, not per command, which is what separates this from the command-by-command growth
D81 rejected.

### 3.1 The declaration: `safe` in the gates file

Commands that execute live in `daoris.gates.json`, apart from the manifest, so a reader of the
manifest can be certain nothing in it runs (D26, `GateDeclaration`'s remarks). The declaration goes
there:

```json
{
  "gates": [
    { "name": "cli",        "run": "npm run verify",          "kind": "suite" },
    { "name": "driver",     "run": "dotnet test src/…Tests --filter Category!=Process", "kind": "suite" },
    { "name": "deployment", "run": "npm run rehearse:deploy", "kind": "rehearsal", "quiet": true }
  ],
  "safe": {
    "run":     ["npm test", { "run": "node --test", "args": true }],
    "install": ["npm ci"]
  }
}
```

What a session is offered, from that file:

- **Every gate that is not the queue's.** A gate whose `kind` is `check` or `suite` (DEV5's
  vocabulary; absent is `suite`) and that is not `quiet` (absent is `false`). Rehearsals and quiet
  gates are the queue's, as the dispatch skill already makes them the parent's. A gate may say
  `"session": false` to withhold itself.
- **`safe.run`**: the build, test, lint and format commands the repository wants run without asking.
  A string is exact. `{ "run", "args": true }` also allows trailing arguments.
- **`safe.install`**: the install from the lockfile. It is a command like the others. It is kept
  apart so that the surfaces can say *installs from its lockfile*, and so that the judge (§3.3)
  refuses an install that adds a package.

**Git on the repository's own branch is not declared.** It is the same for every repository, so it
is Daoris's default (§3.6).

The devkit parses the gates file by the fields it knows and ignores the rest (`GateDeclaration.Read`),
so `safe` needs nothing of it but a test that it is kept. The driver's reader is new (UNBLOCK2).

### 3.2 Read from the line, never from the tree

The driver reads the declaration **from the repository's line**, as git objects, where LAYOUT7 reads
the layout facts (D113's reads). It never reads it from a session's tree. A session can edit its own
tree's gates file, and a declaration read from there would be an agent widening its own permissions,
which D74 forbids. A declaration reaches the line only by landing, and a landing is reviewed: the
person's review of the pull request under `branch`, or the landed history under `merge` (D113).

### 3.3 The judge: what a declaration can never allow

Before anything is proposed, each command is judged against the carve-outs in
`autonomous-development`. A refused command stays asking, and the proposal lists it with the reason.
The judge is conservative on purpose: a false refusal costs an ask, and a false allowance costs the
thing the carve-out protects.

| Refused | Why |
|---|---|
| A shell operator, redirect or substitution (`&&`, `\|\|`, `;`, `\|`, `&`, `>`, `<`, `` ` ``, `$(`, a newline) | one command per entry; the harness splits compounds and judges each part |
| A runner or interpreter that runs its argument, with `args` (`sh -c`, `bash -c`, `cmd /c`, `pwsh -c`, `node -e`, `python -c`, `npx`, `pnpm dlx`, `xargs`, `env`, `sudo`, `docker exec`) | a trailing wildcard on a runner allows anything (the maker's own warning); an exact one is judged as written |
| A publish, a release, a deploy or a push: `git push`, `npm publish`, `dotnet nuget push`, `gh release`, `cargo publish`, `twine upload`, `docker push`, and a script or subcommand named `publish`, `release` or `deploy` | it leaves the machine; the person's (D37) |
| A history rewrite or a discard: `git rebase`, `git reset --hard`, `git commit --amend`, `git filter-*`, `git clean`, `git checkout --`, `git restore`, `git stash drop` | it cannot be taken back |
| A recursive delete (`rm -r`, `rimraf`, `Remove-Item -Recurse`, `rd /s`) | destructive |
| A path outside the repository (absolute, or a `..` that escapes) | the tree is the boundary (D51, the tree guard) |
| An install that names a package (`npm install <name>`, `dotnet add package`) | it changes the lockfile; a lockfile install does not |

This repository's `npm run publish:desktop` would be refused by the third row, as it should be.

### 3.4 The person's one yes

D74's rule stands: **a widening never applies without the person**, in any scope. Claude Code's maker
reached the same rule from the other side: a repository's own `permissions.allow` waits for the
folder's trust, and `autoMode` is never read from project settings, *"so a checked-in repo or a build
step could otherwise inject its own allow rules"*. A declaration is exactly a checked-in repository's
allowances, so it waits for the person once.

- **A `declare` proposal.** When the tick finds a repository's declaration on its line differing from
  the one last accepted, it files one proposal under `<home>/proposals/`: the repository, the commit,
  every rule derived (§3.5), and every command the judge refused with its reason. Its author is *the
  repository's declaration at `<commit>`*, not a session.
- **Judged as D74 judges.** A declaration that only removes commands from the accepted set **narrows**,
  and the tick applies it at once. One that adds any **widens**, and waits, showing only what it
  adds. A newer declaration supersedes a waiting one, which the driver settles, naming the newer.
- **Accepted, it is a layer of its own.** `permissions.json` keeps each repository's accepted
  declaration beside the person's own rules, as `repositories.<name>.declared: { commit, allow }`,
  and the composition unions it like any other layer. Kept apart so that a declaration that narrows
  never removes a rule the person wrote by hand, and so that Settings can show which rules came from
  where.
- **Both doors** (D50): Settings → *What agents may do* → *Proposed by agents*, with the rule row in
  *What needs you*; and `daoris agent rules proposals | accept <id> | decline <id>`. Ask Daoris's door
  is owed by D110 (UNBLOCK6).

### 3.5 From the declaration to each harness

| Harness | What it is handed | Why, and what is measured |
|---|---|---|
| Claude Code, pipe door | each accepted command as an exact `Bash(<run>)`, or `Bash(<run> *)` where `args` is set, in the composed `--settings` file | acceptEdits refuses every command no rule allows, so on this door the declaration is everything. The command-line tier reaches an untrusted folder (DEPLOY1's measurement, D73). |
| Claude Code, protocol door (auto) | the same rules, in the same file on `_meta` | exact rules stay in effect in auto mode, where a package-manager wildcard is dropped (finding 4). Auto mode already allows *"installing dependencies declared in your lock files"*, so the install rule matters on the pipe door. |
| Both Claude Code doors, on Windows | the same commands as `PowerShell(<run>)` too | the harness has a PowerShell tool whose rules have the same shape; whether a driven session on this machine uses it is **not measured**, and UNBLOCK2's canary says |
| codex, protocol door (`agent`) | **nothing**, as today | its per-session channel for command rules is not measured (codex-acp passes a thread config; whether a client can add to it is unread); its `.rules` files are per account under `$CODEX_HOME` or per trusted project under `.codex/rules/`, and an account is not a scope (D72); an execpolicy `allow` runs its command **outside** the sandbox, a wider grant than a Claude Code allow; and `agent` has no network, so no install runs whatever is allowed. UNBLOCK7 measures before anything is handed (the TOOL5 bar). |
| dsh (`workspace-write`) | **nothing**, as today | commands inside the workspace already run without asking (DSH1 probe 1); there is nothing to widen |
| a plugin's harness (D64) | nothing | its manifest's posture is the harness's own (ACP3's rule: never guess a neighbouring mode) |

The surfaces say, per repository and per agent, what that agent is handed, in those words: *handed
the declaration*; *runs it inside its own sandbox; the declaration is not handed*; *nothing measured*.

### 3.6 `git` on its own branch

A session commits its own work in its own tree (D72's `commit` default: `cd`, `git add`, `git
commit`), and the read-only forms of `git` need no rule in the harness. What was missing is
**`git mv`**, which LAYOUT7 needed and gave the set-up press to add. A rename is reversible, stays in
the tree, and `git` itself refuses a path outside the repository. So `commit` gains `Bash(git mv:*)`,
for every repository, and the set-up press's rule loses it (§3.9).

What stays out, and why: `git merge` into the session's branch (D82 rejected a merge rule: a write to
a line the person owns); `git rm` with `-f`, `git restore`, `git stash drop` (they discard); switching
branches (a session works on the branch it was given, D51).

### 3.7 The carve-outs, held harder in auto mode

Finding 2 is a hole in an existing guarantee, and widening what sessions may run raises its stakes.
UNBLOCK4 composes, while `no-push` is on, an `autoMode.hard_deny` entry into the same spawn settings
file:

```json
"autoMode": { "hard_deny": ["$defaults", "Pushing to any remote in any form (for example git -C <dir> push, git -c <key>=<value> push), publishing a package, or creating a release: these stay the person's."] }
```

- **The classifier reads `autoMode` from the `--settings` tier and the SDK's options**, by its maker's
  reference, and never from project settings. Daoris's composed file is that tier on both doors (D72).
  That this file's `autoMode` reaches the classifier on the protocol door is **not measured**; the
  canary does it.
- 🔴 **`"$defaults"` is not optional.** A `hard_deny` list without it replaces the built-in list,
  whose entry is the data-exfiltration rule. The composed list always begins with it, and a test holds
  that.
- A PreToolUse hook reading the command for a push in any form was considered and held (§8): PERM3
  found that a command cannot be judged by reading it, and a hook that fails open on a form it cannot
  parse is the gap again. It is written if the canary shows the classifier letting a variant through.

### 3.8 What still asks

The carve-outs of `autonomous-development`, and what holds each on each harness once the rows land:

| Still asks (is refused) | Claude Code, pipe | Claude Code, protocol (auto) | codex (`agent`) | dsh |
|---|---|---|---|---|
| A push, a publish, a release | `no-push` deny; nothing else allowed | `no-push` deny, and the `hard_deny` entry for other forms | no network in `agent` | 🔴 **nothing**: a push is sandbox-legal and dsh has no notion of outward (DSH1 probe 6); held for UNBLOCK7 |
| A history rewrite, a discard | not allowed by any rule | the classifier's built-in soft blocks (*"git reset --hard … git clean -fd"*, *"git commit --amend"*) | its own review | inside its sandbox, not held |
| A write into another repository | the tree guard, and D107's across denies | the same | its workspace sandbox | its workspace sandbox |
| A destructive delete | not allowed | the classifier's blocks on files that existed before the session | its sandbox and review | inside its sandbox, not held |
| Anything the repository did not declare | refused | judged by the classifier | its sandbox and review | its sandbox |

The table is the honest part of the design. Fewer asks on Claude Code comes with a harder carve-out
there, and on dsh the carve-out for a push rests on the prompt and the repository's own hooks, as
DSH1 found. Daoris does not claim more.

### 3.9 The set-up press's rule

D117 §6.3's press adds a repository-scope rule for the doctrine commands at the pinned version, and
`git mv`, as the person's say-so. With §3.6, `git mv` is a default, and the press adds only the
doctrine commands. After the set-up lands, the repository's first declaration is on its line, and the
next tick files it as a `declare` proposal. The press tells the person so before it is pressed: *after
this lands, the repository's declaration will wait for your yes*.

### 3.10 Measuring asks, before and after

- **The line** (UNBLOCK5): `permission.refused {session, adapter, tool, kind}`, never the command
  (D94 §5). On the protocol door it comes from the refused call's event (`Status: "refused"`,
  `ToolKind`). On the pipe door it comes from the harness's own report: with `--output-format
  stream-json`, *"denials appear as `permission_denied` system messages, and the final result
  message lists them in `permission_denials`"* (the maker's reference, written beside
  `--permission-prompts none`, which needs 2.1.259). That is read from documentation, and whether
  the pipe door's runs emit it without that flag is not measured. The mapper is written to the
  documented shape and labelled so until a turn shows the frame. Auto
  mode's classifier blocks may not arrive as permission requests at all; what they look like on the
  protocol wire is **not measured**, and the canary says. If they are failed calls with a reason,
  they are counted as `by: classifier`.
- **The report**: `tools/usage-report.mjs` gains asks per session (mean, median, 90th percentile,
  the share with none) by adapter and by repository. It also gains the waiting rule proposals per
  week, counted by state from the proposals folder, which holds no words the report prints.
- **Before**: UNBLOCK5 lands first, and a week of the owner's use is logged before UNBLOCK3 lands.
  Separately, the owner can count refusals in the conversation records already on their machine,
  where each protocol-door refusal is kept as a note (and, since HELP4, as the call's `refused`
  status), with a script DOC7 adds. The records are theirs to run it over.
- **After**: the same count, per repository, from the day its declaration is accepted.
- **The target is not zero.** A session refused a push is working as designed. What should reach
  zero is a refusal of **declared** work, and what should fall is asks per session overall.

## 4. The canon text, outlined for DOC2

Frontmatter and the section plan, so DOC2 starts from the decisions rather than rediscovering them.
The words are DOC2's to write under canon-authoring.

```yaml
---
name: development-documents
applies_when: setting a repository up for agents, adding, moving or splitting one of its documents, or when a session had to search for where something is written
enforces: a short brief every agent reads, detail on demand, records in named places the index points to; a document read whole has a ceiling and a record read by lookup has none; what may run unasked is a declaration a tool reads, never a sentence
---
```

`# The development documents — what a repository keeps so a session works unasked`, then `## Why`
(the silent failures: a brief grown until an agent that cuts at a byte limit loses the rules at its
tail; a session re-deriving a rejected decision; a command allowed in prose and refused by the
harness), then `## How to apply`: the roles, the three reading tiers, the brief's content test, the
ceilings' principle, where the records are, and the declaration.

```yaml
---
name: set-up-documents
description: Set a repository's development documents up to the standard, or repair ones that drifted — the brief, the rooms, the records and the declaration of safe work — from the templates beside this skill. Use when a repository is set up for agents, when a request asks for it, or when a session could not find where something is written.
---
```

Its steps: inventory what exists, by role; write or trim the brief from `templates/brief.md`; declare
the documents and the rooms; declare the safe work; measure each document against its ceiling and
the root file against the smallest harness limit; leave the change for review with what moved where.
Its `templates/`: `brief.md`, `room.md`, `decision.md`, `backlog-row.md`, `router.md`. It carries no
`allowed-tools` (§2.8).

## 5. The twins this creates

Each is added to the twins table by the row that builds it, with its test tables:

- **`documents` in the manifest**: the CLI's `config.ts` reads it, and the service's `RepositoryScanner`
  reads the declared decisions, fixes and archive before its own candidates (DOC5). The scanner's rule
  that it needs no configuration stands: a declaration adds a path, it is never required.
- **`safe` in the gates file**: the devkit's `GateDeclaration` keeps it, and the driver's reader
  derives the offer. The merge tool reads `gates` and ignores `safe`.
- **The `declare` proposal and the `declared` layer**: the driver's `RuleProposals.cs` and
  `Permissions.cs`, and the CLI's `ruleproposals.ts` and `permissions.ts`, with the same reading and
  answering cases. The service's `RuleProposalBox` never writes a `declare` proposal, and its table
  says so.
- **The defaults**: `commit` gains `git mv` in both defaults tables (`Permissions.cs`, `permissions.ts`),
  held together by the test that already compares them.

## 6. The build

Each row is dispatchable as written. Lanes are DEV2's ids (`daoris.lanes.json`); *doctrine* and
*docs* are the laneless groups. D122 decides all of it; a row that finds something D122 did not
decide takes its own number. A row with a rehearsal in its proof is proven by the parent's serial run
at merge.

| Row | What lands | Lanes | What proves it | Canon |
|---|---|---|---|---|
| **UNBLOCK5** | **Asks, counted** (§3.10): `permission.refused` from the protocol door's refused calls and the pipe door's `permission_denied` messages; the usage report's asks per session and waiting proposals. **First**, so a week of *before* is logged | driver; tools | `SessionLog` tests; the stream mapper's table against a frame written from the maker's reference, labelled; the usage report's parse table | no |
| **DOC2** | **The standard as canon** (§2.1–§2.6, §4): core knowledge `development-documents`; core skill `set-up-documents` with `templates/`; an entry under `## Unreleased` in `canon/CHANGELOG.md`; this repository and `examples/` re-synced **in the same commit**; the adoption playbook (local) gains the three set-up steps | doctrine; docs | `verify`: frontmatter, the canon's scans, `check` clean, the budget reported. The family rehearsal, the parent's | **yes** |
| **DOC3** | **Roles bound to paths** (§2.7, §2.8): `documents` in the manifest, its refusals, `init` and `analyze` naming candidates, `sync`'s *Where things are* table, `check`'s facts and reports (the root file's bytes shared with LAYOUT3's report), and the canon test that no canonical skill carries `allowed-tools`; the release rehearsal declares, breaks and repairs a document | cli; tools (the rehearsal) | `node --test`, each fact and report a failing test first, and the `allowed-tools` test seen failing against a fixture skill; `npm run rehearse`, the parent's | no; a repository that declares nothing sees no change |
| **UNBLOCK2** | **The declaration and its judge** (§3.1–§3.3), after DEV5, whose gates-file fields it reads: `safe` in `daoris.gates.json`; the devkit keeps it; the driver reads the offer from the line; the judge's table; this repository's own `safe` section | tools (the devkit, the gates file); driver | the judge's table, each row a failing test first; the devkit's test that `safe` is kept; the reader over a scratch repository's line, not its tree | no |
| **UNBLOCK4** | **The carve-outs, held harder, and `git mv`** (§3.6, §3.7): the `hard_deny` entry with `"$defaults"` first, composed while `no-push` is on; `commit` gains `Bash(git mv:*)` in both defaults tables | driver; cli (the defaults twin) | composed-file tests, `"$defaults"` first asserted; the defaults twin. The canary, the owner's: a push written as `git -C . push` in auto mode is refused | no |
| **UNBLOCK3** | **The person's one yes** (§3.4, §3.5), after UNBLOCK2 and a week of UNBLOCK5: the `declare` proposal filed at the tick; narrowing applied, widening waiting, superseding; the `declared` layer composed; the pipe and protocol doors' exact rules, and `PowerShell(…)` once the canary says; both doors' answers | driver; cli (the twin) | twin tables on both sides; the family rehearsal: a declared command refused before the yes and allowed after, and a declaration that drops it narrowing at the next tick | no |
| **DOC4** | **This repository declares its documents** (§2.7): `documents` in `daoris.json` with today's ceilings; `tools/doc-budgets.mjs` reads them from the manifest, so there is one list, and `doc-budgets.json` retires | doctrine (`daoris.json`, the region by `sync`); tools | `verify`: `check` clean with the table in the region; `doc-budgets` reads the manifest | no |
| **DOC5** | **The service reads declared records**: the scanner reads the declared decisions, fixes and archive before its candidates, and indexes the router as a document | service | service tests: a declared decisions log at a path no candidate names is found, and found once | no |
| **DOC6** | **The example family keeps the standard**: `examples/engine` gets a brief from the template, declares its documents and a `safe` section; the family rehearsal checks `check` over it and that the region names its records | doctrine (`examples/`); tools (the rehearsal) | the family rehearsal, the parent's | re-syncs `examples/` |
| **UNBLOCK6** | **The screen and Ask Daoris** (§3.4): the proposal card shows a declaration, its commit, its rules, its refusals with their reasons, and what each agent is handed; the *What needs you* row; Ask Daoris's door, or a reasoned exemption (D110); both languages | web-settings; modules; driver (help) | vitest over a mocked bridge; modules tests; `HelpCoverageTests`; translation parity; the look in both themes and both languages, the parent's | no |
| **DOC7** | **What sessions read, measured** (§1.5): `session.read` and `session.skill` lines; the usage report's reading section; a script the owner runs once over the records already kept | driver; tools | `SessionLog` tests (a read resolving to a role, one resolving to none, no path in any line); the report's table. The owner's run is evidence | no |
| **UNBLOCK7** | **codex and dsh, measured before handed** (keyless first): whether codex-acp's `session/new` can carry a thread's rules or network; whether its trusted root loads a project's `.codex/rules/`; whether dsh's hook bridge, through `$DSH_HOME/cordis.patch.yml`, can hold a push. An evidence note; a row per finding | tools (the probe); docs (the evidence) | the evidence. Nothing is handed to codex or dsh until it says | no |
| **UNBLOCK8** | **The first real declaration**, the owner's run: one repository of the real workspace declares, by its set-up (LAYOUT10) or a quest; asks per session a week before and a week after | none (a run) | the evidence, with the owner present | no |

**Order.** UNBLOCK5 first. Then DOC2 (doctrine) beside DOC3 (cli) beside UNBLOCK2 (tools and driver,
after DEV5). The driver lane runs UNBLOCK5 → UNBLOCK2 → UNBLOCK4 → UNBLOCK3 → DOC7 in sequence. DOC4
follows DOC3; DOC5 any time after DOC3; DOC6 after DOC2, DOC3 and UNBLOCK2; UNBLOCK6 after UNBLOCK3.
UNBLOCK7 and UNBLOCK8 are runs.

**Only DOC2 changes the canon.** It re-syncs `examples/` in the same commit and passes the family
rehearsal. DOC6 changes the examples themselves and passes it too. DOC3 changes the region's generator
only for a repository that declares documents.

**Amended rows.** LAYOUT7's body gains the three set-up steps of §2.9, and its press's rule loses `git
mv` (§3.9); its composer test holds the new steps. LAYOUT3's root-file report and DOC3's are one line.

## 7. What a rehearsal can prove, and what only a real run can

**A rehearsal proves the mechanism**: every fact and report of §2.8 over real files; the region's
table kept true; a declaration read from a line and not a tree; the judge's refusals; a declared
command refused before the person's yes and allowed after, handed in the composed file; a narrowing
applied at the tick; a `permission.refused` line with no words in it. No model and no account.

**Only a real run proves the rest:**

1. **That the harness honours what it is handed**: the exact rules on both Claude Code doors, the
   `hard_deny` entry on the protocol door, and whether a driven session on Windows reaches for its
   PowerShell tool. The canary, the owner's.
2. **What a classifier block looks like on the protocol wire**, and so whether it is counted.
3. **Whether sessions read the documents the standard gives them**, and in which order (DOC7's
   counts).
4. **Whether asks per session fall**, on a real repository over real weeks (UNBLOCK8).
5. **Whether a set-up session writes a good brief** from the template: judgement, which only a real
   set-up shows (LAYOUT10).

## 8. Considered and rejected

- **Allowances applied from the declaration without the person.** D74's rule, and the harness maker's
  own: repository-supplied allow rules wait for trust, and `autoMode` is never read from project
  settings for that reason. A session can also edit the declaration, so an automatic reading would be
  an agent widening itself.
- **A skill with `allowed-tools` naming the repository's gates.** The harness honours it untrusted,
  for one turn, on one harness, with no review: every property the design exists to avoid. And the
  canon never ships one (§2.8).
- **Writing the repository's `.claude/settings.json`.** The repository's file (D32), ignored until
  trusted, and rejected by D72 for both reasons.
- **Translating the declaration into codex's `.rules` files.** Per account, where an account is not a
  scope (D72), and an execpolicy `allow` runs the command outside the sandbox, a wider grant than it
  looks. Held for UNBLOCK7's measurement.
- **One proposal per command.** D81 rejected growing the list command by command. The unit is the
  repository's reviewed declaration.
- **Prefix rules for runners** (`Bash(npm run *)`, `Bash(npx *)`). Dropped in auto mode (finding 4),
  and a runner runs whatever follows it.
- **The declaration in the manifest.** The manifest is nouns, and nothing in it runs (D26).
- **Reading the declaration from the session's tree.** A session could widen itself mid-run (§3.2).
- **Every declared gate offered to sessions.** The queue's gates need the machine quiet or start
  windows (finding 5).
- **The standard as an always-loaded rule.** The budget, and most tasks do not need it; the generated
  table carries the part every task does.
- **The standard as a pack, or in the adoption playbook.** A dependency of core cannot be opt-in, and
  the playbook cannot be read from another repository (§2.6).
- **Ceilings that fail.** D54. What fails is a fact: a declared document missing, a link, a stale
  table.
- **Notes by lifecycle folder as the standard's decisions record.** D117 §2.4; a repository that keeps
  them declares the folder as its decisions record.
- **A glossary required everywhere.** It pays where a repository names things to people, in one
  language or two; elsewhere it is a document nobody opens.
- **A hook that reads commands for a push.** Held (§3.7): the classifier's hard deny first, the hook
  if a turn shows a variant passing.
- **`dontAsk` or `bypassPermissions` postures.** D81 kept the harness's own judgement; `dontAsk` would
  turn every undeclared command into a denial even where the classifier would have allowed it.

## 9. Sources

Read 2026-10-01. Each claim above about a harness names whether it is the maker's documentation, or
LAYOUT2's reading of shipped code (`docs/2026-10-01-entry-point-evidence.md`), or this repository's
code, by file.

- The `AGENTS.md` convention: <https://agents.md/>
- Claude Code, how it remembers a project (`CLAUDE.md`, `AGENTS.md`, imports, the 200-line target):
  <https://code.claude.com/docs/en/memory>
- Claude Code, best practices (verification, the include/exclude table, permissions):
  <https://code.claude.com/docs/en/best-practices>
- Claude Code, permissions (rule syntax, compound commands, what a Bash rule does not match, trust,
  what runs before trust): <https://code.claude.com/docs/en/permissions>
- Claude Code, permission modes (auto mode's blocks and allowances, broad rules dropped, the fall-back
  thresholds): <https://code.claude.com/docs/en/permission-modes>
- Claude Code, configuring auto mode (`autoMode` scopes, `"$defaults"`, `hard_deny`):
  <https://code.claude.com/docs/en/auto-mode-config>
- Claude Code, running programmatically (`permission_denied`, `permission_denials`, `dontAsk`):
  <https://code.claude.com/docs/en/headless>
- Claude Code, skills (progressive loading, supporting files, `allowed-tools` and trust):
  <https://code.claude.com/docs/en/skills>
- Claude Code, hooks (`PermissionRequest`, `PermissionDenied`): <https://code.claude.com/docs/en/hooks>
- codex, `AGENTS.md` discovery and its 32 KiB: <https://learn.chatgpt.com/docs/agent-configuration/agents-md>
- codex, rules (`prefix_rule`, decisions, where they load): <https://learn.chatgpt.com/docs/agent-configuration/rules>
- codex, approvals and sandbox: <https://learn.chatgpt.com/codex/agent-approvals-security>
- GitHub Copilot, repository instructions: <https://docs.github.com/en/copilot/how-tos/configure-custom-instructions/add-repository-instructions>
- Gemini CLI, context files: <https://google-gemini.github.io/gemini-cli/docs/cli/gemini-md.html>
- dsh's repository, root and documentation standards:
  <https://github.com/deepseek-ai/deepseek-harness/blob/master/AGENTS.md> and
  <https://github.com/deepseek-ai/deepseek-harness/blob/master/docs/AGENTS.md>
