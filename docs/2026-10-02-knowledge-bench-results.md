# KNOW3: how each knowledge design lets a real session find its document (bench results)

**Carried by:** KNOW3, for D128 (`2026-10-02-setup-pilot-lessons-design.md` §2, the index read on demand) and for
KNOW2's review (D129). It is a record of one bench run, not a contract: a harness, a model or a corpus that moves is
measured again.

> Run 2026-10-02 by `tools/knowledge-bench.mjs`: 72 real headless sessions (6 designs × 12 tasks × 1) and 6 canaries,
> serially, on **Claude Code 2.1.287** with the harness's default model for the account, **`claude-opus-5-5`** (read
> from every run's `init` event). **Codex is not installed on this machine, so nothing here says how codex behaves.**
> No account or spend limit was reached. The raw streams and scores are in `local/scratch/knowledge-bench/full/`
> (gitignored); this document quotes them.

## The answer

- **Every design found every document, the control included: 72 of 72.** At 73 documents with descriptive file
  names, this model did not need an index to *find* the governing document. What the designs changed is the cost of
  finding it: calls, tokens, wall time, and how many bytes every request carries whether it needs a document or not.
- **A, today's table in the always-loaded region, is the fastest and the most expensive to carry.** The first call
  went to the right document in 12 of 12 runs. But the table cost **9,447 tokens on every request** at 73 documents
  (measured), about 21,800 at 169 (arithmetic), in every session, including the ones that never open a document.
- **B, D128's generated `INDEX.md`, costs one more call and nothing until it is opened.** Median 2 calls to the read
  against A's 1, and about a second more. Every B session opened the index first and **read it whole** (12 of 12,
  23 KB), so a session that does open it carries it from then on: B spent 17% more tokens per hit than A in these
  short discovery sessions. Its saving is in every session that never opens it, and in the rules sitting higher in
  `AGENTS.md`.
- **G, a prompt hook pushing a local BM25 top 5, was the cheapest per hit** (47,075 tokens, $0.148), but only as good as
  its ranking. The ranking held the truth in its top 5 for 5 of 12 questions. Then the read came at call 1. Otherwise
  the list could mislead: one session searched inside the hook's first suggestion and read the truth at call 5.
- **C, knowledge as skills, was the fastest by wall time** (median 14 s) and second cheapest in tokens per hit. Its
  listing is always loaded too, and grows with every document like A's table, at about 46% of A's bytes per document.
- **E, search-first, and the control took the most calls** (median 3), and 38% and 45% more median wall time than A,
  at no always-loaded cost. With a model writing the searches, synonymy did not defeat keyword search. The model wrote its
  own synonyms into each pattern. BM25 alone, with no model, put the truth in its top 5 for only 5 of the 12 questions.

## 1. Method

### 1.1 The corpus

This repository's own `docs/*.md`, 67 documents, plus the 6 in `canon/core/knowledge/`: **73 documents, 1.76 MB**,
median 16 KB each. `docs/README.md` is left out because it is itself an index, and so are the three append-only
records (`DECISIONS.md`, `FIX-LOG.md`, `task-archive.md`), which are read by lookup. Every document is public in this
repository, and nothing came from outside it.

Each `docs/` document's frontmatter is generated from its row in `docs/README.md`, the way a set-up session would
write it from the router (`frontmatterFor`):

- **A contract or method.** `applies_when` is *working on or deciding about* + the *For* column. `enforces` is the kind
  and the *For* column, then *Where it stands*, cut at 240 bytes.
- **A study or evidence.** `applies_when` is *revisiting the reasoning behind* + *Carried by* + the document's title,
  because that row names only decisions. `enforces` gives the kind's meaning.
- **A record.** `applies_when` is *looking up* + *What it holds*.

The canon's six keep their own frontmatter. A body's references to other corpus documents are pointed at where the
fixture keeps them.

### 1.2 The tasks

Twelve questions, each with one ground-truth document (T10 has two, either counts). Each is phrased in different words
from its target, because synonymy is what keyword discovery fails on. *BM25 rank* is where the push design's ranker,
with no model, put the truth among the 73.

| Task | The question, in short | Ground truth | Words avoided | BM25 rank |
|---|---|---|---|---|
| T1 | an add-on supplies its own coding-assistant program: what may it state; is its code loaded? | `2026-09-23-plugin-design` | plugin, harness, declare, manifest | 24 |
| T2 | a subscription ran out of its weekly quota: which sign-in takes the next job, and when does the first return? | `2026-10-01-account-rotation-design` | account, limit, cool-off, rotation | 1 |
| T3 | the words on a settings screen in English and Chinese: just translation? a length cap? | `2026-10-01-naming-design` | name, label, glossary, budget | 1 |
| T4 | what the app records locally about my use, and what it may never keep | `2026-09-30-machine-log-design` | log, machine, event | 9 |
| T5 | I pushed a commit with an access token: is deleting the line enough? | `leak-repair` (canon) | credential, leak, history, scrub | 2 |
| T6 | the readme promises the program never opens a network connection: what should stand behind that? | `claims-need-checks` (canon) | claim, check, guarantee, verify | 7 |
| T7 | can I type my own commands in the bottom panel where an agent's output scrolls? | `2026-09-30-terminal-design` | terminal, console, shell | 9 |
| T8 | a front-end agent needs facts another team's codebase owns: how does it pause, get them and carry on? | `2026-09-27-ask-and-wait-design` | quest, repository, resume, block | 7 |
| T9 | which git and Node the app uses beside its agents, and can I use my own? | `2026-10-01-tools-design` | tool, resource, managed | 1 |
| T10 | an agent clicks through a page and signs in: whose browser, and how does it get it? | `2026-09-27-in-app-browser-design`, `2026-09-28-chromium-host-design` | browser MCP, CDP, Chromium | 1 |
| T11 | "find similar notes" with an embeddings vendor: which rules govern the vendor, and none configured? | `model-decoupling` (canon) | model, provider, tier, AI | 48 |
| T12 | the first time the product ran as a real copy on someone's computer: what broke? | `2026-09-22-first-deployment-case-study` | deploy, install, case study | 12 |

The full wording is `TASKS` in the tool. Every run is asked the same way around its question: *This is a discovery
task. Find and read the documents in this repository that govern the question below, then say in two lines what they
require, and name the documents you used. Change nothing.*

### 1.3 The designs

One fixture each, a fresh `git init` folder with one commit, so the harness sees a clean tree. Every fixture has
`CLAUDE.md` holding `@AGENTS.md`, and the same three-line brief in `AGENTS.md`, which names the repository and says
nothing about knowledge. Only each design's own part differs. The knowledge is at `knowledge/<name>.md`, except in C.

| Design | Its own part | Loaded before the first prompt | At 169 documents |
|---|---|---|---|
| **0** control | nothing | 208 bytes | 208 bytes |
| **A** index in the region | `## Read on demand`, one sentence (*scan the Applies when column and read every document that matches*), and the table, one row per document as Daoris renders it today | 25,971 bytes; **+9,447 tokens** a request over the control, measured | 59,626 bytes; ≈21,800 tokens a request |
| **B** generated index | D128 §2.2's pointer, adapted: *the knowledge … is listed in `INDEX.md`, generated from the files: read it before a non-trivial task, and search it when it is long.* `INDEX.md` is D128 §2.3's table, paths as code spans | 396 bytes; +63 tokens. `INDEX.md` 23,462 bytes when opened | 396 bytes; `INDEX.md` ≈54,100 bytes when opened |
| **C** skills | one sentence; each document is `.claude/skills/<name>/SKILL.md` with `description` from `applies_when` | 409 bytes plus the harness's skill listing, ≈11,700 bytes estimated; **+4,330 tokens** measured | ≈27,600 bytes; ≈9,900 tokens |
| **E** search-first | *the knowledge lives in `knowledge/` … each opening with frontmatter … There is no index: search `knowledge/` for the task's words* | 482 bytes; +95 tokens | 482 bytes |
| **G** push | one sentence, and a project `UserPromptSubmit` hook in `.claude/settings.json` running `.claude/hooks/rank.mjs`, BM25 over frontmatter (counted thrice) and body, printing the top 5 titles and paths | 360 bytes, and the hook's list with each prompt: +268 to +365 tokens, measured | unchanged: always five lines |

Token figures are the difference between each design's first-request context and the control's for the same task.
That difference was the same in all twelve tasks to the token, except G's, whose list changes with the question. A's
table measured 2.73 bytes a token. C's listing measured about 2.75 against the estimate of its bytes. The 169 column
is arithmetic: the fixed part, plus the mean bytes per document times 169. It was not run.

### 1.4 The runs and the measures

Each run is `claude -p "<task>"` with the brief's flags: `--output-format stream-json --verbose --setting-sources
project --strict-mcp-config --mcp-config {"mcpServers":{}} --allowedTools Read,Grep,Glob,Skill --disallowedTools
Edit,Write,Bash,WebFetch,WebSearch --max-turns 20`. Every flag was checked against `claude --help` at 2.1.287.
`--max-turns` is not in the help text, but it is accepted: a probe run with it exited 0. Three flags were added, each
for a reason the canaries showed:

- **`--tools Read,Grep,Glob,Skill`.** Without it the `init` event listed 23 tools, among them `Task`, a subagent whose
  reads would be nested out of the session's own calls.
- **`--include-hook-events`.** It puts any hook that fires into the stream, which is how isolation is proven.
- **`--no-session-persistence`.** Nothing is written into the person's session history.

The child's environment drops every `CLAUDE*` variable, so a run is not a child of the session that started it and
does not inherit its effort setting. Runs are serial. The order of the designs turns with each task, so no design always
runs first.

From each stream:

| Measure | What it counts |
|---|---|
| hit | the run read a ground-truth document: a `Read` of its file, or the `Skill` tool on it |
| calls to the read | the 1-based index, among the session's own tool calls, of the first hit |
| calls to the target | the first call aimed at the truth: a read, a search confined to its file or skill folder, or a `Glob` that spells its path out. A session searching inside the right document has already chosen it |
| searches before it | `Grep` and `Glob` calls before the hit |
| wrong reads | distinct corpus documents read that are not the truth |
| tokens | input + cache read + cache write + output, from the `result` event; *tokens per hit* is the design's total over its hits, which caching does not change |
| list price | the harness's own `total_cost_usd`. It depends on what was cached by earlier runs, so the order matters to it and not to the tokens |
| wall | from spawn to exit |

**The pilot.** It ran first, six designs by T1, T5 and T12, and was looked at before the rest: isolation held in all six
canaries, and the scoring matched each stream read by hand. Nothing about the fixtures, the prompt or the flags
changed after it, so its 18 runs are 18 of the 72. Two measures were added to the scorer afterwards, *calls to the
target* and the list price per hit. Every record is scored again from its kept stream.

## 2. Isolation, proven

The harness loads instruction files from every folder above the one it runs in, and this machine has user-level
configuration (a memory MCP server among it) that would otherwise reach every run.

- **The root.** The fixtures sit under a folder in the OS temp folder. The tool refuses a `--root` with `CLAUDE.md`,
  `CLAUDE.local.md`, `AGENTS.md` or `.claude/CLAUDE.md` in it or in any folder above it, and has a test for that.
- **The canaries.** One turn per design asks the session to list, without calling a tool, every instruction file with
  its first heading, every tool, MCP server and skill, and any text a hook added. The stream's `init` event and hook
  events are checked against what the fixture holds. The bench stops at the first difference.

| Design | Tools | MCP servers | Plugins | Skills | Hook events |
|---|---|---|---|---|---|
| 0, A, B, E | Glob, Grep, Read, Skill | none | 4, all `builtin` | 19, the harness's own | none |
| C | the same | none | 4, all `builtin` | 92: the harness's 19 and the 73 documents | none |
| G | the same | none | 4, all `builtin` | 19 | `UserPromptSubmit` only, its output the ranker's list |

The four plugins are the harness's own (`cc-plugin-sec-default`, `cc-plugin-agents-md`, `cc-plugin-telemetry` and
`cc-plugin-plugin-authoring`, each with path `builtin`). No user-level hook fired, no MCP server connected, and no
plugin or skill came from the person's configuration. Each fixture's auto-memory folder holds no `MEMORY.md`, which
each canary checks. Every canary answered the same way: `CLAUDE.md` holding `@AGENTS.md`, `AGENTS.md` headed
`# Brief`, no memory loaded, no MCP server, and hook text *NONE*, except G's, which quoted the ranker's five lines. The
`init` and hook checks also ran on **every one of the 72 runs' streams**, not only the canaries, and found nothing.

**Found while proving it.** Claude Code 2.1.287 reads `AGENTS.md` itself, through its built-in `cc-plugin-agents-md`. A
scratch probe of four one-turn sessions, outside the bench, used a 10,619-byte `AGENTS.md`:

| The folder held | Context of the first request |
|---|---|
| nothing | 7,867 tokens |
| `AGENTS.md` alone | 12,244 tokens |
| the same text as `CLAUDE.md` alone | 12,245 tokens |
| both, `CLAUDE.md` holding `@AGENTS.md` | 12,299 tokens |

So D59's `@AGENTS.md` import is loaded once, not twice. This bears on the entry-point evidence
(`2026-10-01-entry-point-evidence.md`), which read 2.1.284/2.1.285, and it is not this bench's question.

## 3. Results

| Design | Hits | Median calls to the target | Median calls to the read | Median searches before it | Wrong reads per run | Tokens per hit | List price per hit | Median turns | Median wall | Loaded before the prompt | At 169 documents |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0: control | 12/12 | 2 | 3 | 1.5 | 0.5 | 69,951 | $0.167 | 6 | 24 s | 0.2 KB | 0.2 KB |
| A: index in the region | 12/12 | 1 | 1 | 0 | 0.4 | 63,100 | $0.208 | 3 | 16 s | 26.0 KB | 59.6 KB |
| B: generated `INDEX.md` (D128) | 12/12 | 2 | 2 | 0 | 0.3 | 73,885 | $0.204 | 4 | 18 s | 0.4 KB | 0.4 KB |
| C: skills | 12/12 | 1 | 1 | 0 | 0.5 | 51,075 | $0.169 | 4 | 14 s | 12.1 KB | 27.6 KB |
| E: search-first | 12/12 | 3 | 3 | 2 | 0.6 | 65,620 | $0.178 | 6 | 23 s | 0.5 KB | 0.5 KB |
| G: push, BM25 top 5 | 12/12 | 2 | 2 | 1 | 0.2 | 47,075 | $0.148 | 4.5 | 17 s | 0.9 KB | 0.9 KB |

How each design spent its calls, over its 12 runs:

| Design | Calls | Searches | First call aimed at the truth | Other documents read |
|---|---|---|---|---|
| 0 | 58 | 37 | 0 of 12 | 6 |
| A | 27 | 8 | **12 of 12** | 5 |
| B | 39 | 12 | 0 of 12: the index came first, 12 of 12 | 3 |
| C | 31 | 11 | 11 of 12 | 6 |
| E | 58 | 37 | 0 of 12 | 7 |
| G | 40 | 24 | 5 of 12 | 2 |

Calls to the read, per task:

| Task | 0 | A | B | C | E | G |
|---|---|---|---|---|---|---|
| T1 | 5 | 2 | 3 | 2 | 3 | 4 |
| T2 | 2 | 1 | 2 | 1 | 2 | 1 |
| T3 | 5 | 2 | 2 | 1 | 4 | 1 |
| T4 | 4 | 1 | 2 | 1 | 3 | 2 |
| T5 | 2 | 1 | 2 | 1 | 2 | 1 |
| T6 | 2 | 1 | 2 | 1 | 3 | 2 |
| T7 | 4 | 1 | 2 | 1 | 2 | 2 |
| T8 | 2 | 1 | 2 | 3 | 3 | 3 |
| T9 | 4 | 1 | 2 | 1 | 3 | 1 |
| T10 | 2 | 1 | 2 | 2 | 4 | 1 |
| T11 | 5 | 1 | 2 | 3 | 4 | 2 |
| T12 | 2 | 1 | 2 | 1 | 2 | 5 |

The 72 runs took 25 minutes of wall time and $12.88 at list price, as the harness computes it, plus $0.44 for the
canaries. The account is a subscription, so this is the harness's figure and not a bill. Every run ended `success`,
none reached `--max-turns`, and the five-hour window stayed between 29% and 44%, shared with the session that ran
the bench. The bench stops at 85%.

## 4. What the misses looked like

There were none, so this section is where each design spent its extra calls, with the session's own search terms.

- **0, the control.** Half its runs (6 of 12) began by listing every file (`Glob **/*.md` or `**/*`). The dated,
  descriptive file names then did an index's work. In T12 the session listed the files and read the truth next. Its searches carried
  their own synonyms: T1 `(?i)(plugin|add-on).{0,80}(agent|driver)|(agent|driver).{0,80}plugin`, T3
  `(?i)chinese|translat|localiz|zh-|i18n|character limit|length`, T11 `(?i)embedding|vendor|similar`.
- **E, search-first.** It never listed the folder. It searched the bodies with alternations: T5
  `secret|token|credential|leak|history|rotat|revok`, T11
  `(?i)vendor|embedding|provider|third.party|not configured|unconfigured`. Twice it turned the frontmatter into an
  index of its own, grepping `^(name|applies_when|enforces):` over a few candidate names (T3, T10).
- **G, push.** For the five questions whose truth was in the hook's top 5 (T2, T3, T5, T9, T10), the median read was
  call 1. For the other seven it was call 2, and the list could mislead. In T12 the session first searched inside the
  hook's first suggestion, `2026-09-27-first-goal-study`, with `(?i)(install|dev build|from source|repo checkout|
  publish|real machine|first run)`, then tried `Glob knowledge/*install*`, and read the truth at call 5. In T1 the
  truth was 24th, out of the list. The session's own `(?i)(plugin|extension|add-on|addon)` found the plugin design,
  and it also searched the hook's first suggestion, the driver design, before reading the plugin design at call 4.
- **A, the region's table.** Its first call went to the truth in all twelve runs. In ten that call was the read. In T1
  and T3 it was a search confined to the chosen file, such as T1's `(?i)harness|in-process|load.*assembl|no code` in
  the plugin design, and the read came second.
- **B, the index file.** Every run read `INDEX.md` whole as its first call, and none searched it. The truth followed at
  call 2 in 11 runs. In T1 a search inside the target came between them.
- **C, skills.** Eight truths came by the `Skill` tool at call 1. Three runs found the skill's folder by `Glob` and read
  `SKILL.md` directly (T8, T10, T11). C's slowest were T8, which also read `reaching-in`, and T11, which grepped
  `(?i)embedding` across every skill and read two neighbours.
- **Other documents read** were mostly defensible neighbours: `reaching-in` beside T8 (five designs), the knowledge
  service design beside T11 (all six; that service is where embeddings are used), the toolchain design beside T9
  (two), and `autonomous-development` beside T5 (two).

## 5. Threats to validity

- **One harness, one model.** Claude Code 2.1.287, `claude-opus-5-5`, at the harness's default effort. A smaller model
  may write poorer searches, and E, G and the control lean on the session's own searching. Codex is not installed and
  is not measured. That codex cuts project instructions at 32,768 bytes is the entry-point evidence's, so A's table at
  169 documents (59.6 KB) would be cut for codex: arithmetic, not run.
- **A corpus the model may half-know, and one that names itself well.** These are Daoris's own documents, which the
  model may have met in some form. More important, their file names are dated and descriptive
  (`2026-09-22-first-deployment-case-study.md`), and the control used them as an index in half its runs. A
  repository whose names say less would likely widen the gaps. Not measured.
- **Tasks written by the bench's author,** who knew the targets. The vocabulary was avoided by hand, and BM25 still
  ranked 4 of the 12 truths first.
- **One run per cell.** Twelve runs per design, no repeats, no variance. A difference of one call in a median is within
  what a second run could move.
- **The prompt asks for discovery outright.** Every design was told to find and read the governing documents. Real
  sessions are not always told, and the owner's real 267-call session read 25 knowledge documents and 2 index files and
  made 41 searches. The bench does not measure whether a session thinks to look at all.
- **Frontmatter from the router.** `enforces` for a contract carries its status text, not what it requires. It is
  thinner than hand-written frontmatter, and A, B and C read it.
- **73 documents, not 169.** The 169 figures are bytes by arithmetic. Two things were not measured at 169: whether a
  B session would still read a 54 KB index whole, and whether the harness would shorten C's listing.
- **The knowledge folder is `knowledge/` at the root,** not a dotted folder like `.claude/` or `.agents/`, which a
  search may skip by default. Not measured.
- **A hit is a read, not a correct answer.** The two-line answers are kept in the raw records and were not graded.

## 6. What the numbers say

### 6.1 For D128

- **D128's reason holds on bytes, and the bench adds its cost in requests.** A list in the region is paid on every
  request of every session: 9,447 tokens at 73 documents, measured, and about 21,800 at 169. B moves that to the
  sessions that open the index, once each, for one more call (median 2 against 1) and about a second.
- **"Read on demand" was read whole.** All twelve B sessions read the 23 KB index entire, and none searched it, though
  the pointer says *search it when it is long*. A session that opens the index carries it for the rest of its context,
  much as A does. So the saving is in the sessions that never open it, and in where the rules sit. Whether a 54 KB index
  would be searched instead is the first thing to measure once D128 is built.
- **The index is not what makes documents findable here.** The control found all twelve. At this size the index buys
  speed and fewer calls, and does not buy recall. D128 is a change in what every request costs, and the bench gives no
  reason to expect sessions to find less after it.

### 6.2 For KNOW2's candidates

- **Measured here.** The region's index (A), an index file (B), knowledge as skills (C), search-first (E) and per-prompt
  recall (G).
- **Not measured.** Rooms and path rules attach knowledge to where in a code tree a session works, and a discovery
  question in a documents-only repository cannot exercise that. Lyntai's file storage is not in this repository, and
  the bench copies nothing from outside it.
- **A list that grows with the repository costs the same way wherever it is always loaded.** A's table costs per
  request. So does C's skill listing, at about 46% of A's bytes per document. C was the quickest design and the second
  cheapest per hit, but it is the harness's own progressive disclosure, and how other agents list skills was not run.
- **Push is cheapest when its ranking is right, and the ranking is its weak part.** BM25 with no model put the truth in
  its top 5 for 5 of 12 questions. A better ranker is the model tier, and `model-decoupling` says it must still answer
  without one. Every design here has a no-model floor: the session's own searching, which found all twelve.
- **Not measured: combinations.** Such as an index on demand with a pushed top 5 (B with G), or skills with a push.

## 7. Reproducing, and what the gate does not cover

```
node --test tools/knowledge-bench.test.mjs
node tools/knowledge-bench.mjs run --root <a folder under the OS temp folder> --pilot --label pilot
node tools/knowledge-bench.mjs run --root <the same> --label full
node tools/knowledge-bench.mjs report --label full
node tools/knowledge-bench.mjs rank "<question>"
```

`run` skips any record already kept, so a stopped bench resumes. The test file runs outside `npm run verify`, whose
tests are the CLI package's.

**What its gate covers:**

- the frontmatter from the router rows
- each design's fixture and its shared brief
- the ranker and the hook it is copied into
- the root's refusal and the flags
- the scoring of recorded pilot streams, scrubbed of machine paths and trimmed of the documents' text
- the isolation check, on a recorded canary and on a stream altered to fail
- the limit check and the aggregate

**What it does not cover:**

- the numbers in §3, which are this run's
- the `AGENTS.md` probe in §2, which was a scratch script
- the token-to-byte ratios in §1.3, measured from this run's first requests
- every 169-document figure, which is arithmetic
