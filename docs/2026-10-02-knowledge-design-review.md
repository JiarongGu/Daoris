# The knowledge design, reviewed — how a session finds what a repository knows

> The owner, 2026-10-02, on D128's index: *"i don't know if this index is a good design or not check if we can use any
> better way for this entire knowledge design"*, and later the same day: *"since lyntai has filesystem mode check if
> this is a better solution"*. This is the review (KNOW2), and its decision is **D129**, pending KNOW3's measurement.
> Status: **reviewed; nothing built.** It keeps D128's split and amends its §2.5, the canon words it drafts, and the
> rows WSSETUP14a and WSSETUP14d. WSSETUP14a was paused for this answer. Read with **D7** as amended by **D59**,
> **D14**, **D24**, **D48** §2a, **D54**, **D117**, **D122**, **D127** and **D128**.

- §0 is the question and the evidence. §1 is what each agent does today, from its maker. §2 weighs eight
  candidates, A to H. §3 compares them for the 169-document repository.
- §4 is the recommendation and the canon's words; §4.7 states it as predictions KNOW3's bench can test. §5 is the
  build. §6–§9 are what only a real run proves, what was rejected, what this amends, and what this document's gate
  does not cover. §10 lists the sources.

## 0. The question, and what was measured

### 0.1 What is being decided

Today every adopter's always-loaded region carries the rules in full, plus one row per knowledge document and per
skill (`renderRoster`, `tierrender.ts`). Sessions are told to read that index and load what matches (`skills-workflow`,
`doc-loader`). On the first real set-up's repository, 169 knowledge documents made the region 53,398 bytes. The rules
fell past the 32,768 bytes one agent reads, and 166 rows read *needs frontmatter* (D128 design §0). D128 moves the
knowledge and skill tables into a generated `<target>/INDEX.md`, read on demand.

The question is wider than that file: given doctrine shared across many repositories and agents, adopters with 10 to
200 knowledge documents, and D48 §2a (doctrine never hard-requires the service), what is the best way for a session to
find the document that applies?

### 0.2 What sessions on the owner's install did

The parent counted these from session records. Only these numbers are quoted.

| Session | Tool calls | File reads | Of code | Of knowledge | Of hand-written index files | Of `AGENTS.md`/`CLAUDE.md` | Searches |
|---|---|---|---|---|---|---|---|
| An ordinary driven session | 267 | 89 | 54 | 25 | 2 (the repository keeps three `RULES_INDEX_*` files) | 0 | 41 |
| A second session | — | 40 | 24 | 7 | 3 | — | 24, 3 of them into the knowledge folder |
| The set-up session | 116 shell commands | 22 | — | 7 | 2 | — | — |

Three readings, and one limit:

- **Agents read knowledge.** A quarter or more of the file reads in the first two sessions were knowledge documents.
- **Agents open an index file when one exists**, unprompted by Daoris: the repository's own hand-written ones, two or
  three times a session. An index read on demand is a behaviour sessions already show.
- **Agents also search the knowledge folder directly.** Three of the second session's 24 searches went there.
- **The records count reads by kind, not what led to each.** Whether a knowledge read followed an index row, a search
  hit or a path in another document is not in these numbers. DOC7 (D127 §6.1) is the line that would say.

### 0.3 The measurements this builds on

D128 §0.3, on a fixture shaped like the pilot (169 knowledge documents, 18 skills, a brief of 7,398 bytes):

| | Region | Root `AGENTS.md` | First rule at byte | Knowledge table |
|---|---|---|---|---|
| Today, 166 undescribed | 46,279 | 53,762 | 37,046 | 23,333 |
| Today, all described | 63,527 | 71,010 | 54,294 | 40,581 |
| D128 | 19,010 | 26,493 | 9,777 | in `INDEX.md`: about 27,700, or 45,000 described |

So a described knowledge row costs about 240 bytes, and a skill row 238 (4,281 bytes for 18). Here, measured on this
tree: the knowledge table is 3,304 bytes for 10 rows (322 bytes a row, with longer prose), and the skill table 1,338
bytes for 8. D127 §1.2 measured the eight skill descriptions the harness itself lists at 2,474 bytes, so the region's
skill table is a truncated second copy of a list every agent already holds.

## 1. What each agent does today, from its maker

Read on 2026-10-02 from each maker's documentation (**doc**), or from LAYOUT2's reading of shipped code at the
versions it names (**source**, `docs/2026-10-01-entry-point-evidence.md`). Daoris drives Claude Code (both doors),
codex (over ACP) and dsh. Gemini CLI, Cursor and GitHub Copilot are not driven, and are here because the study D122
weighed them and because they show where the ecosystem converges.

### 1.1 Instructions

| Agent | Always loaded, and its limit | Nested, lazily | `@` imports | Path-scoped rules |
|---|---|---|---|---|
| Claude Code (both doors) | `CLAUDE.md` in the working folder and every folder above; `AGENTS.md` only where no `CLAUDE.md` is (2.1.277+). A file over 4 MiB is skipped, never cut; *"target under 200 lines per CLAUDE.md file"* (doc, source) | a subfolder's `CLAUDE.md` and `.claude/rules/` load *"when Claude reads files in those subdirectories"* (doc, source) | followed, four hops (doc; source reads depth 5) | **yes**: `.claude/rules/*.md` with `paths:` globs, which *"trigger when Claude reads files matching the pattern"*; rules without `paths` load at start (doc) |
| codex (native and ACP) | `AGENTS.md` per folder from the git root down to the working folder; **32 KiB for the whole chain**, the tail cut (doc, source). codex-acp marks the session's root trusted (source) | never below the working folder (doc, source) | not interpreted (source) | none |
| dsh (0.1.6-alpha.2) | `AGENTS.md` and `CLAUDE.md` per folder from the project root; 65,536 bytes for the whole baseline (source) | a folder's files load when a `read`, `write` or `edit` touches a file there (source) | not interpreted (doc) | none: `.claude/rules/` is not interpreted (doc) |
| Gemini CLI | `GEMINI.md`, or `AGENTS.md` by `context.fileName`, from the workspace and its parents (doc) | **just in time**: *"When a tool accesses a file or directory, the CLI automatically scans for GEMINI.md files in that directory and its ancestors"* (doc) | `@file.md` (doc) | none; the folder's file is the scope |
| Cursor | Always-apply rules; `AGENTS.md` at the root and in subfolders, combined with parents (doc) | nested `AGENTS.md` and `.cursor/rules` (doc) | `@filename` inside a rule (doc) | **yes**: *"Apply to Specific Files"* by glob, and *"Apply Intelligently"* by description (doc) |
| GitHub Copilot | `.github/copilot-instructions.md`, *"no longer than 2 pages"*; the nearest `AGENTS.md` (doc) | the nearest `AGENTS.md` wins (doc) | — | **yes**: `.github/instructions/*.instructions.md` with `applyTo` globs (doc) |

### 1.2 Skills, hooks and search

| Agent | Skill roots | In context always, and its budget | A prompt hook that adds context | How it searches |
|---|---|---|---|---|
| Claude Code | `.claude/skills/` up to the repository root; a subfolder's loads *"the first time Claude reads or edits a file in that subdirectory"* (doc) | name and description; each capped at 1,536 characters; the listing's budget *"scales at 1% of the model's context window"*, and past it Claude Code *"drops descriptions starting with the skills you invoke least"*. A skill may carry `paths` (doc) | `UserPromptSubmit` and `PreToolUse` take `additionalContext`, capped at 10,000 characters (doc) | ripgrep (`Grep`), or `grep` and `find` in its shell on macOS and Linux; no embedding index (doc) |
| codex | `.agents/skills/` from the working folder up to the repository root; user, admin, system (doc, source) | name and description; the list *"uses at most 2% of the model's context window, or 8,000 characters"*; past it codex *"shortens skill descriptions first"*, then may omit skills with a warning (doc) | `UserPromptSubmit` adds *"extra developer context"*; a hook runs only after a person reviews and trusts its exact definition (doc). Over codex-acp: not measured | its shell; no index Daoris knows of |
| dsh | `.dsh/skills` and `.agents/skills` at the project root, and `customSkillDirs` (source) | not measured | not measured; its plugin hooks are tool pre and post (first-goal study §2) | not measured |
| Gemini CLI | `.gemini/skills/` or `.agents/skills/`, workspace and user (doc) | name and description; the body through `activate_skill`, after a consent prompt (doc) | `BeforeAgent`, *"After user submits prompt, before planning"*, may add context (doc) | `glob` and `grep_search` (doc) |
| Cursor | `.agents/skills/`, `.cursor/skills/`, and for compatibility `.claude/skills/` and `.codex/skills/` (doc) | progressive; no stated limit (doc) | `beforeSubmitPrompt` *"Can prevent submission"* and cannot add context; `sessionStart` and `postToolUse` can (doc) | *"Instant Grep"*, a local index; it *"does not store embeddings of your codebase for search"* (doc) |
| The Agent Skills format | `.agents/skills/`, *"a widely-adopted convention for cross-client skill sharing"* (doc) | name and description, *"~50-100 tokens per skill"*; the body under 5,000 tokens and 500 lines (doc) | — | — |

The Agent Skills format was *"originally developed by Anthropic, released as an open standard"*. Its showcase lists
Claude Code, codex, Gemini CLI, Cursor, GitHub Copilot and VS Code among 46 clients on the day it was read (doc).

### 1.3 Where the makers converge

Three makers, and the owner's own library, reach one shape for material too large to load: **a headline per entry
always loaded, the body on demand, and a cap on the headlines with a rule for overflow.**

- Skills: the name and description always, the body on use. The format is shared, and each budget is the maker's
  own: Claude Code caps the listing at 1% of the window and drops the least-used descriptions; codex caps it at 2% and
  shortens descriptions.
- Claude Code's own memory: `MEMORY.md` is *"Index, one line per memory, loaded into every session"*, read to its
  first 200 lines or 25 KB, while topic files are not loaded at start and *"Claude reads them on demand using its
  standard file tools"*.
- Cursor's *"Apply Intelligently"* rules: the description decides, and the rule loads *"When Agent decides it's
  relevant"*.
- Lyntai's memory engine recalls *"a cheap index of one-line headlines that a caller can choose to expand"* (the
  parent's reading of Lyntai's decisions).
- Path-scoped rules (Claude Code, Cursor, Copilot) and lazily loaded folder files (Claude Code, dsh, Gemini CLI) are
  the second answer: load by **where** the session works, not by what it asks.

Two things in that shape matter for Daoris. **The headlines are always loaded, and capped**, by a budget the harness
sets and an overflow rule a committed file cannot follow: the skill listings rank by use, and `MEMORY.md` tells its
writer to shorten it. And **no agent documented here searches a repository by meaning**: ripgrep, a shell, Instant
Grep. The one search by meaning in this picture is Daoris's own service (`knowledge_search`, words and meaning
together, saying which answered, D24).

### 1.4 What could not be verified

dsh's skill budget, prompt hooks and search; dsh, codex and codex-acp at their newest. Whether Claude Code runs a hook
from the `--settings` file Daoris hands it, and over the ACP adapter's settings option, without workspace trust.
Whether codex runs hooks under codex-acp, and how a driven session would satisfy its trust review. Gemini CLI, Cursor
and Copilot cells are their documentation only: Daoris drives none of them. A budget stated in tokens is converted
here at about four bytes a token, an estimate no tokenizer checked.

## 2. The candidates

Each is weighed on what the brief names: the bytes every request carries, how reliably a session finds the right
document, how it degrades on an agent that lacks a feature, D48 §2a, drift detection, and whether a tool or a person
keeps it. The 169-document repository is D128's fixture: a 7.4 KB brief, 169 knowledge documents, 18 skills.

### A. The index in the region (today)

- **Always loaded**: 53,762 bytes of root file, 71,010 with every document described. **Steps**: none to see the list.
- **Discovery**: the model reads every row by meaning, but 166 rows say nothing, and on codex the rules themselves
  are cut, since the first one starts at byte 37,046.
- **Degrades**: worst on the agent with the smallest limit, and silently.
- **Verdict: replace.** D128 is right that the region must not grow with a repository's documents.

### B. A generated index read on demand (D128)

- **Always loaded**: 26,493 bytes, whatever the count (D128's finding 3). **Steps**: one read of `INDEX.md`, whole
  (about 27,700 to 45,000 bytes, carried from that step on, D127 §1.6) or by search (about 150 bytes a row found,
  measured here: one matching frontmatter line is 146 bytes), then each matched document.
- **Discovery**: a whole read matches by meaning. A search matches by words, and that is where §2.G's measurement
  bites: the miss rate of a word search grows with the store. D128's `doc-loader` says *"by searching it for the
  task's words when it is longer"*, which at 169 rows is exactly that regime.
- **Degrades**: well. Every agent reads a file and searches text; nothing runs. It is committed, so D48 §2a holds.
- **Drift**: `check` rebuilds it offline and fails when it is behind (D128 §2.4). Kept by `sync`, never by hand.
- **Verdict: keep, as the floor.** Amend how it is searched when long (§4.2).

### C. Knowledge as skills

Each knowledge document becomes `<name>/SKILL.md`, and each agent's own progressive disclosure carries its
description.

- **Always loaded**: the region drops its pointer, but the harness's catalog now asks for 187 descriptions at about
  240 bytes each, about 45,000 bytes. Claude Code's budget at a 200,000-token window is about 8,000 bytes by the
  conversion above, and codex's is 2% of the window or 8,000 characters. Past the budget both shed descriptions, so
  most of the 169 would be names alone. **Steps**: none to see a name, one to activate.
- **Discovery**: by the harness's ranking of use, which is the right rule for procedures and the wrong one for a
  document read once a month whose one month matters.
- **It is the move D128 forbids.** Each document becomes a folder under a skill root, which is fault 1 again (218
  links and about 250 references), and under D117 each is mirrored for Claude Code: 169 more files.
- **Degrades**: by budget, per agent. codex warns; on Claude Code the cost shows in `/doctor`'s estimate of the
  listing, not in the session.
- **Verdict: reject** for knowledge. Skills stay skills, and the catalogs already carry them, which is why D128 takes
  the skill table out of the region.

### D. Knowledge beside the code, in rooms loaded lazily

Each folder's `AGENTS.md` (a room, D117 §2.2) carries the knowledge about that folder.

- **Always loaded**: the region plus a *Rooms* row per room, about 100 bytes each. A room loads on Claude Code, dsh,
  Gemini CLI and Cursor when the session touches a file there, and never on codex, which reads only the region's
  pointer to it.
- **Discovery**: perfect for a folder's own conventions, by where the session works. Nothing for cross-cutting
  knowledge (how twins agree, how a leak is repaired), which has no folder to sit in. A room's ceiling is 600 words
  (D122 §2.3), so it holds a folder's conventions, not 169 deep dives.
- **Degrades**: to *telling* on codex, which D117 already designed for.
- **Verdict: a complement, already built** (D117's rooms). Not the knowledge mechanism.

### E. Search first: no index, a convention

No generated list. A document's name and its first lines (`applies_when`, `enforces`, its heading) are the search
surface. The discovery skill greps them, with the service's `knowledge_search` as an accelerator, never required.

- **Always loaded**: about 26,450 bytes (D128's root file, its pointer replaced by a sentence naming the convention).
  **Steps**: one to three searches, then the documents.
- **Discovery**: lexical. A grep of every `applies_when:` line here returns 1,578 bytes in 11 lines for 10 documents,
  one of them an example inside a document's body: about 143 bytes a line, so about 24,000 for 169. That is an index
  read in another shape, with nothing to say it is complete. A document without frontmatter is found only by its name
  or heading.
- **Degrades**: as well as B. **Drift**: nothing generated, so nothing to fall behind, and nothing that proves the
  list whole.
- **Verdict: adopt as the convention under B, not instead of it.** When no index is read (an agent outside Daoris,
  a skipped step), a well-named document with its frontmatter first is what a search finds (§4.4).

### F. Path-scoped rules

Each knowledge document carries globs, and the harness loads it when a matching file is read.

- **Reach**: Claude Code (`paths`), Cursor (globs), Copilot (`applyTo`), in three formats. Not codex, not dsh. That is
  the shape D59 left: `.claude/rules/` reached one agent of three.
- **It needs the knowledge moved or copied** into each agent's rules folder: fault 1, or the knowledge mirror D117
  rejected, which doubles every search hit.
- **Verdict: reject.** What it does well, loading by where the session works, rooms already do on more agents.

### G. Recall pushed per prompt

This is the owner's own working pattern on this machine, described neutrally as the owner's own memory store. One
Markdown entry per file, with frontmatter; served over MCP, where a search returns ids and titles and a get returns
one entry in full; ranked by keyword and meaning together by a small .NET program that links Lyntai; pushed into the
context by the harness's hooks on each prompt, and before a mutating command only, so reads cost nothing. It fails
open: with no embedder or no program, the hook falls back to keywords alone.

**Measured on that store** with paraphrase probes worded to avoid the target's vocabulary (the parent's report):

| | Found |
|---|---|
| Keyword alone, 16 entries | 9 of 12 |
| Keyword alone, 112 entries, a 3-slot window | 4 of 13. The misses were synonymy, and the miss rate grows with the store |
| Keyword and meaning together | 11 of 13 (85%) |
| The remaining misses | ranking, not retrieval: the right entry was a candidate, below the cut |

Costs: about 500 ms a recall, mostly .NET start-up; a new entry is invisible to meaning until it is ingested; a
diagnostic once polluted the store.

- **Against the 169-document case.** An agent that searches a long index is matching keywords, which is the regime
  the store measured failing as it grew. An agent that reads the whole index matches by meaning, and pays for every
  row from that step on. Recall by meaning is the one channel that is both cheap per find and robust to wording.
- **Against D48 §2a.** A push needs the service or a local program. So it can only ever be an accelerator over files
  that work without it, and it must never appear in the canon or in a file committed to the repository.
- **Against the agents.** A prompt hook that adds context exists on Claude Code, codex (once a person trusts the
  hook), Gemini CLI and Copilot's SDK. Cursor's prompt hook cannot add context. dsh's is not measured. Where there is
  no hook, the same recall is *pulled*: every driven session already has the connector's `knowledge_search` on both
  doors, and its target prompt already says to look there (`Adapters.cs`, `Asking`, WSSETUP9). Outside Daoris there
  is grep over the files.
- **The push that reaches every agent is the prompt Daoris writes.** A driven session is started from the target
  prompt the driver composes. Headlines recalled for the quest and written into that prompt reach Claude Code, codex
  and dsh on both doors, with no hook and no trust review. A hook is needed only for a chat session's later prompts.
- **Verdict: adopt as an accelerator, held until it is measured on repository knowledge** (§4.6). The store's
  numbers are memory entries, not a repository's deep dives, and the probe is cheap.

### H. Lyntai's file storage as the knowledge store

`UseFileSystemStorage` in `Lyntai.Storage.Basic` (3.3; the package's page reads *"one Markdown record per file,
readable without a client"* across six domains, at 3.5.3, which `Daoris.Service` already references). From Lyntai's
changelog and decisions, as the parent read them: records are held in memory and written through, because a scan
measured 137 ms at 1,000 records and 1.9 s at 10,000 against a 100 ms budget. So one process owns a root through a lock
file, a second is refused, files may be hand-edited only while no process owns the root, and a file that does not
parse is skipped and never written over. It serves key-value, prompts, conversations, task and curated memory and the
memory engine's graph, and not vectors, jobs, the cache or traces.

The first-goal study §3 declined this format for repository knowledge, for three reasons. Re-tested against this
question:

1. **A second schema.** Holds, more strongly. Knowledge frontmatter is now the agents' own idiom: the Agent Skills
   format's `name` and `description`, Claude Code's `paths`. Lyntai's header is read by no agent.
2. **Single-owner rules.** Holds, and decides it. A repository's knowledge folder is written by sessions on branches,
   by people, and by every `git checkout` and merge. To Lyntai each of those is a hand edit under a running owner. The
   scan cost that forces the single owner is not the issue at this size: 169 records would scan in about 23 ms by
   linear extrapolation from 137 ms at 1,000, which is arithmetic, not a measurement. The ownership model is.
3. **Sequential ids collide across branches.** Holds. Daoris names a document by its file name (D7, D14), and git
   merges two new names.

Two more: it holds no vectors, so the search by meaning would still live in the service's own index; and the doctrine
command is zero-dependency Node and `check` runs offline (D8, D11), so the doctrine cannot read a store a .NET process
owns.

**What transfers is two ideas, not the store.** *Headlines, then expand* is already Daoris's shape: an index row, a
`knowledge_search` hit (title, path, excerpt) and `knowledge_get`. Building B confirms it. **Authoritative entries take
reserved slots**: Daoris's authoritative tier, the rules, never competes for a slot, since it is always loaded. The
idea that does transfer is the reserved slot itself, in a push's ranking: one slot for the best keyword match, one for
the best meaning match, the rest by the fused rank. That answers the store's last finding, a right entry ranked below
the cut. **Verdict: decline the store; take the reserved slots into G.**

## 3. Compared, for the 169-document repository

| | Always loaded (root file) | Per find | Steps to the document | Reach | Without Daoris | Drift held | Kept by |
|---|---|---|---|---|---|---|---|
| A | 53,762 to 71,010 | 0 | 0 + read | rules cut on codex | yes | `check` | `sync` |
| B | 26,493 | 150 a row searched, or 27,700 to 45,000 read whole | 1 + read | every agent | yes | `check` | `sync` |
| C | about 26,300, plus a catalog asking 45,000 of an 8,000 budget | 0 | 0–1 | per budget | yes | the harness | hand and `sync` (mirror) |
| D | 26,493 + 100 a room | 0 on lazy agents | 0, or 1 on codex | folder-scoped only | yes | rooms declared | hand |
| E | about 26,450 | about 150 a hit | 1–3 + read | every agent | yes | none | hand (frontmatter) |
| F | 26,493 | the rule, whole | 0 | Claude Code (and Cursor, Copilot) | yes | none | hand, three formats |
| G | B's floor | about 200 a headline pushed | 0 + read | where a hook or the prompt is Daoris's | falls back to B | ingest lag | the service |
| H | — | — | — | through a process | no | lock file | a process |

- **The best discovery per byte, where it is available, is G**: no standing bytes, a few hundred per headline, and it
  matches by meaning. It needs a running service and reaches only the sessions Daoris starts.
- **The best that needs nothing running is B**: bounded in the region, one read when short, a search when long, and
  `check` keeps it whole. E is cheaper per find and loses both the meaning and the completeness.
- **B and E degrade best**: every agent reads files and searches text. G degrades to them by failing open. C degrades
  by budget, D to telling, F to nothing on two agents of three.

### 3.1 The hybrid

None of the eight wins alone. The one that beats each is layered, with the files as the floor:

1. **The files are the store and the fallback.** Knowledge stays Markdown with its frontmatter where the repository
   keeps it (D128 §1), and `INDEX.md` lists all of it, generated and held by `check` (B).
2. **A convention makes a document findable without the index** (E): a name that says its subject, and when it applies
   in its first lines.
3. **Recall by meaning is pulled** through the service's `knowledge_search`, already handed to every driven session,
   and named where a knowledge search is connected, always beside its tool-absent path.
4. **Recall is pushed** only where Daoris writes the session's words: the driven target prompt first (every agent),
   a prompt hook in chat sessions second (where the agent has one Daoris can hand). Headlines only, with reserved
   slots, failing open (G, held for its measurement).

Layer 1 alone is D128. Layers 2 and 3 are words in canon documents read on demand, so they cost the region nothing;
the one always-loaded change is `skills-workflow`'s, 22 bytes (§4.3). Layer 4 is the driver's, and waits for a probe.

## 4. The recommendation: amend D128, keep its split

### 4.1 What stands

D128 §1 (the checks kept green, knowledge declared in place), §2.1–§2.4 (the region, the pointer, `INDEX.md` and its
cells), §2.6–§2.8, §3 (a document without frontmatter, by its first heading) and §4 (the pilot's follow-up) stand as
written. The review found nothing better for the floor than one generated, checked file.

### 4.2 `doc-loader`, step 2

D128 §2.5's draft says *"whole when it is a few dozen rows, by searching it for the task's words when it is longer"*.
The second half is a word search over a long list, the regime that misses by synonymy. In the canon's words:

> 2. **The generated index.** The always-loaded doctrine names the index of the on-demand tiers. Read it whole when it
>    is a few dozen rows. When it is longer, search it more than once: the task's words, their synonyms, and the names
>    of the folders and parts the task touches. Read every row the searches return. Where a search over this
>    repository's knowledge is connected, ask it too, since it matches by meaning, which a word search cannot; where
>    none is, the index searches are the whole step. Read every matched document. A search that finds nothing has not
>    shown that nothing applies: the index is generated from what is on disk, so it is the exhaustive list.

A skill's body loads on use, so this costs no request that does not run it. It names a service, so it carries the
tool-absent path in the same sentence (canon-authoring), and the D48 §2a canon scan, which matches only *quest* today
(`dogfood.test.ts`), widens to a connected search (§5).

### 4.3 `skills-workflow`

D128 §2.5 leaves it unchanged. With the skill table out of the region, its first line, *"invoke the discovery skills
the generated index lists"*, would send a session to open `INDEX.md` for names its agent already lists. Two clauses
change:

- *"invoke the discovery skills the generated index lists"* becomes *"invoke the discovery skills your agent lists"*;
- *"**Consult the generated index for the roster.** It is built from what is on disk, so it is right about…"* becomes
  *"**Find them in your agent's own skill list, or in the generated index.** Both are built from what is on disk, so
  they are right about…"*.

+22 bytes in every adopter's region, measured on these drafts. An agent whose skill roots hold nothing still finds the
skills by path in the index, which is the Agent Skills format's *file-read activation*.

### 4.4 `development-documents`

D128's sentence stands (*"Something always read names each one, or names the index that lists them"*). One more joins
the *Three ways a document is read* section, the convention of §2.E:

> Name a knowledge document by its subject, and say when it applies in its first lines. A search finds what a file's
> name and opening say, and that is how a document is found where no index is read.

It is on demand, so its index row is the only always-loaded cost, and the row does not change. It builds after
SESSOPT1a, which also changes this document (D127 §2.3).

### 4.5 The set-up quest

D128 §1.5–§1.6's quest gains two things:

- **The close names each hand-written index the repository keeps**, such as the report repository's three, and what
  each lists that the generated index does not, or the reverse. The set-up neither deletes nor rewrites one: it is the
  repository's own file, a check may read it, and `doc-loader` step 1 still reads it as the repository's router. Two
  lists of one tier drift apart (D14: *a shortcut table elsewhere is a convenience, not the registry*), so retiring
  one into the generated index is a request the person may publish.
- **A document the set-up writes is named by its subject**, with its frontmatter first and a first heading that says
  what it is about (§4.4), so it is found by a search as well as by the index.

### 4.6 Recall, as the driver's accelerator

Held for measurement (KNOW3's bench, §4.7; the rows of §5), and shaped now so the measurement asks the right
question:

1. **Into the driven target prompt first.** The driver asks the service for the quest's title and body, scoped to the
   session's repository, and writes up to five headlines under the look-before-you-ask paragraph: each document's path
   and its `applies_when`, or its heading, never its body. It reaches every agent on both doors.
2. **Reserved slots** (§2.H): the best keyword match and the best meaning match each keep a slot, and the rest go by
   the fused rank.
3. **Fails open.** No service, no answer within a second, or no semantic half: the prompt is written as today, and
   the search's own tier line (D24) says when only words answered.
4. **A prompt hook for chat sessions second**, on Claude Code through the settings Daoris already composes (D72), once
   a probe shows such a hook runs on both doors. codex's hook needs a person's trust per definition, and a driven
   session has nobody to give it, so it waits for that cell to be measured.
5. **Never** in the canon, in `INDEX.md`, or in any file committed to the repository (D32, D48 §2a). A pushed headline
   is a hint. The session still runs `doc-loader`.

### 4.7 The recommendation as predictions (KNOW3)

KNOW3 runs real headless Claude Code sessions over this repository's own documents, with a fixture per design (A, B,
C, E, G, and a control with no guidance), and scores whether the session read the right document, in how many calls
and tokens. Its results go to `docs/2026-10-02-knowledge-bench-results.md`, and D129 takes them. What this review
predicts, so each line can be confirmed or refuted:

| Design | Hit rate, against A | Tokens per session | Where it should miss | What would refute it |
|---|---|---|---|---|
| A, the index in the region | the reference: every row is read by meaning from the first step | the most: the table rides every request | rarely on this corpus; its failure (rules past codex's cut) cannot show on Claude Code, which never cuts | — |
| B, `INDEX.md` | within a few points of A, one call later | fewer than A, the more steps a session takes after its read | a session that never opens the index; a long index searched in the task's words when the document uses others | B well below A: sessions skip the pointer, and its wording failed |
| C, knowledge as skills | close to A while the catalog fits its budget, falling toward the control past it | about A's: the catalog rides every request too | the right skill never activated: a description written as a fact's subject is weaker as a trigger than an index row read on purpose | C at A's rate past the budget, or above B |
| E, search first | below B on tasks worded away from the documents, near B on tasks in their words | the fewest of the guided designs | synonymy: a search for one word misses the document that says another | E at B's rate on paraphrased tasks: then the index adds completeness, not discovery, at this size |
| G, pushed titles | the best on paraphrased tasks; at least B's elsewhere | near E's: a few hundred bytes a prompt | ranking, not retrieval: the right document a candidate below the cut; and nothing when the push has no meaning half | G no better than B on paraphrased tasks: then KNOW2b is not worth its service call |
| The control | the lowest: it finds documents whose names the task shares | many calls spent searching | everything not named by the task | the control near B: the guidance is noise on this corpus |

The bench's G pushes through a prompt hook. KNOW2b would write the same headlines into the driven prompt, so G's result
speaks for both, on the one turn a driven session starts from.

**The best discovery per byte should be G, then B.** If the bench ranks B above G, the push is held (KNOW2b) and the
floor is the design. If it ranks E with B, `doc-loader`'s many-wordings search (§4.2) carries the discovery and the
index carries completeness and `check`. What no single-agent bench can show is the degradation half of §3: the cut on
codex, a nested file never loaded, a hook an agent lacks.

### 4.8 What to tell the owner, in one line each

- **Is the index a good design?** Yes, as the floor: it is the shape the makers converged on, minus the cap, and the
  only one that works with nothing running. Its *search when long* needs more than one wording.
- **Is there a better way?** Recall by meaning, pushed where Daoris writes the prompt, beats it per byte. It sits on
  top of the index, never in its place, and its case on repository knowledge is not measured yet.
- **Is Lyntai's file storage better?** Not as the store: git is the owner of a repository's files, and Lyntai's store
  needs to be the only one. Its reserved slots belong in the push's ranking.

## 5. The build

Rows ready for `TASKS.md`, in D127's shape. Lanes are `daoris.lanes.json`'s ids. D129 decides all of it.

- [ ] **WSSETUP14a, amended** (the row is open). Its `doc-loader` step 2 takes §4.2's words, `skills-workflow` §4.3's,
  and `development-documents` §4.4's sentence beside D128's. A word search over a long index misses by synonymy, and a
  session would otherwise open the index for skill names its agent already lists. Contract: D128 design §2.2–§2.5, as
  §4.1–§4.4 here amend them. Proof: the row's own, plus the D48 §2a canon scan widened to a connected search, failing
  first on a draft without its tool-absent clause; the region within 100 bytes of D128 §2.6 plus 22.
- [ ] **WSSETUP14d, amended** (the row is open). The set-up's close names each hand-written index and how it differs
  from the generated one, and the documents it writes are named by their subject. Two hand-kept lists of one tier
  drift, and sessions there already read them. Contract: §4.5. Proof: `SetupBriefTests` (the close's new item, no
  deletion of a repository's index in the bounds) and the playbook twin.
- [ ] **KNOW2a — the service's own recall, probed** (only if KNOW3's G fixture ranks by something other than the
  service's `knowledge_search`). A tools script runs paraphrase probes over a fixture of knowledge documents, through
  searches of `INDEX.md` and through `knowledge_search`, and reports recall at 3 and 5 for each. KNOW2b would push the
  service's ranking, so that ranking is what must beat the word searches. Contract: §2.G, §4.6. Proof: an evidence
  note with the table and the probes; the script's tests. The word half runs keylessly; the meaning half needs the
  deployment's embedder, so it is the owner's run (D24). Lanes: tools; docs.
- [ ] **KNOW2b — headlines in the driven prompt** (held on KNOW3's G result, KNOW2a where it runs, and DOC7). The driver writes up to five recalled
  headlines into the target prompt, one slot reserved for each tier, and nothing when the service does not answer. It
  reaches every agent on both doors, because the driver writes the prompt. Contract: §4.6 items 1–3 and 5. Proof:
  driver tests (the headlines, the reserved slots, a silent service leaving the prompt as today, no body pasted);
  DOC7's knowledge reads before the first edit, before and after. Lanes: driver.
- [ ] **KNOW2c — a prompt hook for chat sessions** (held on KNOW2b and a probe). On Claude Code, the settings Daoris
  composes carry a `UserPromptSubmit` hook that adds the same headlines, failing open. A chat session's later prompts
  get what a driven session's first does. Contract: §4.6 item 4. Proof: the probe that a hook in those settings runs
  on both doors; the composed settings' tests; a canary turn on the owner's login. Lanes: driver; cli.

**Order.** WSSETUP14a as D128 orders it, after KNOW3's numbers land in D129; WSSETUP14d after WSSETUP14b. KNOW2a after
KNOW3, if it is needed. KNOW2b after KNOW3, KNOW2a and DOC7. KNOW2c after KNOW2b.

## 6. What a gate can prove, and what only a real run can

- **A gate proves** the canon's words and their scans, the region's bytes, `INDEX.md`'s cells, the set-up quest's
  words, the probe's arithmetic, and that a silent service leaves a prompt unchanged.
- **KNOW3's bench shows**, on one agent and this repository's documents, the predictions of §4.7: hit rates, calls
  and tokens per design.
- **Only use shows** whether sessions read the index whole or search it, and whether the searches find what a whole
  read would (DOC7); whether recall by meaning beats word searches on a repository's knowledge (KNOW2a, the owner's
  embedder); whether pushed headlines change what a session reads before its first edit (KNOW2b); whether a hook
  in Daoris's settings fires on both doors (KNOW2c's probe); and how each design degrades on codex and dsh, which a
  Claude Code bench cannot reach.

## 7. Considered and rejected

- **The index in the region** (A): it grows with every document, and the rules fall past codex's cut.
- **Knowledge as skills** (C): the catalogs' budgets shed descriptions at this count, and it is the move D128 forbids,
  mirrored.
- **Knowledge in rooms** (D) as the mechanism: cross-cutting knowledge has no folder, a room has a 600-word ceiling,
  and codex never loads one. Rooms stay for a folder's own conventions.
- **Search with no index** (E) on its own: nothing proves the list whole, and a document without frontmatter is
  invisible to the frontmatter search.
- **Path-scoped rules** (F): one format per agent, none on codex or dsh, and the knowledge moved or mirrored.
- **Lyntai's file storage as the store** (H): a single owner where git and every branch write, sequential ids, no
  vectors, and a .NET process between the doctrine and its offline `check`.
- **Push as doctrine, or a hook committed to the repository**: it needs a program running, which D48 §2a forbids
  doctrine to require, and a committed file is the repository's (D32).
- **A region that keeps the table while it is short and moves it past a threshold**: the region's shape would flip
  with the repository, every flip a diff, for about 3 KB on a small adopter. D128's finding 3, a region the same for
  every repository, is worth more.
- **Capping the index, as the makers cap their headline lists**: their overflow rules rank by use or tell the writer to
  shorten, neither of which a generated file can do, and D128 already rejected a cap by the alphabet.

## 8. What this amends

Each row that builds a piece notes the amendment where it lands.

- **D128 §2.5**: `doc-loader` step 2 as §4.2; `skills-workflow` changes as §4.3; `development-documents` gains §4.4's
  sentence. The rows WSSETUP14a and WSSETUP14d, as §5.
- **D124 §2.7**, through D128 §1.6: the close names each hand-written index (§4.5).
- **The first-goal study §3**: its three reasons for declining Lyntai's format for repository knowledge, re-tested and
  standing (§2.H).
- **The D48 §2a canon scan** (`dogfood.test.ts`): a connected search joins *quest* as a service-only mechanism.

## 9. What this document's gate does not cover

This change is documents only, and nothing is built. The install's session counts (§0.2), the owner's store and its
measurements (§2.G), and Lyntai's lock, scan and domain facts (§2.H) are the parent's reports: none was re-run here, and
the NuGet page confirms only the one-file-per-record and six-domain parts. The makers' cells are their documentation as
fetched on 2026-10-02, or LAYOUT2's reading of shipped code, as §1 labels each, and §1.4 lists what neither reached.
Byte figures are D128's fixture or this tree, measured with `node` and `grep` in the worktree; token budgets are
converted at about four bytes a token. The canon drafts' 22 bytes are computed, not rendered. §4.7 is predictions:
KNOW3's numbers were not in hand when this was written, and D129 takes them when they land. `verify` checks this
document's links and the decision's shape, and none of their words.

## 10. Sources

Read on 2026-10-02 unless marked.

- Claude Code, memory, rules, `AGENTS.md` and auto memory: <https://code.claude.com/docs/en/memory>
- Claude Code, skills: <https://code.claude.com/docs/en/skills>
- Claude Code, hooks: <https://code.claude.com/docs/en/hooks>
- Claude Code, tools (`Grep`, `Glob`): <https://code.claude.com/docs/en/tools-reference>
- codex, `AGENTS.md`: <https://learn.chatgpt.com/docs/agent-configuration/agents-md> (redirected from
  `developers.openai.com/codex/guides/agents-md`)
- codex, skills: <https://learn.chatgpt.com/docs/build-skills> (redirected from `developers.openai.com/codex/skills`)
- codex, hooks: <https://learn.chatgpt.com/docs/hooks> (redirected from `developers.openai.com/codex/hooks`)
- The Agent Skills format: <https://agentskills.io/>, <https://agentskills.io/specification>,
  <https://agentskills.io/client-implementation/adding-skills-support>
- Gemini CLI: <https://geminicli.com/docs/cli/gemini-md/>, <https://geminicli.com/docs/cli/skills/>,
  <https://geminicli.com/docs/hooks/>, <https://geminicli.com/docs/tools/file-system/>
- Cursor: <https://cursor.com/docs/context/rules>, <https://cursor.com/docs/context/skills>,
  <https://cursor.com/docs/agent/hooks>, <https://cursor.com/docs/context/semantic-search>
- GitHub Copilot: <https://docs.github.com/en/copilot/how-tos/configure-custom-instructions/add-repository-instructions>,
  <https://docs.github.com/en/copilot/how-tos/copilot-sdk/use-hooks/user-prompt-submitted>
- Lyntai's file storage: <https://www.nuget.org/packages/Lyntai.Storage.Basic> (3.5.3, published 2026-09-30)
- What Claude Code, codex, codex-acp and dsh read, from shipped code (2026-10-01): `docs/2026-10-01-entry-point-evidence.md`
- The service's search: `src/Daoris.Service/Daoris.Service.Mcp/KnowledgeTools.cs`; the driven prompt's look before
  asking: `src/Daoris.Desktop/Daoris.Desktop.Driver/Adapters.cs`
