# Orientation everywhere — every repository Daoris drives starts from an index, and needs nothing of Daoris to read it

> The owner, 2026-10-04, after ORIENT1: *"so this should also apply to the repositories Daoris drives too, and an even
> better knowledge system since Daoris is there"*, and *"it should not break the general workflow if the system running
> the code does not have Daoris (this is for sharing repositories)"*. This is ORIENT2's design; its decision is
> **D151**. Status: **designed; nothing built.** Read with **D24**, **D32**, **D48** §2a, **D54**, **D122**, **D124**,
> **D128**, **D129**, **D134**'s ORIENT1b note, **D135**, and the map design's ORIENT1a note.

- §0 is what ORIENT1 measured and built, and what the repositories Daoris drives keep today. §1 is the floor: the
  committed files, how they stay true, and the canon's words. §2 is the set-up's part. §3 is what Daoris adds where it
  runs. §4 walks a fresh clone on a machine with no Daoris. §5 is the measure. §6 is the build.
- §7–§9 are what was rejected, what this amends, and what this document does not cover.

## 0. What ORIENT1 found, and what this generalises

### 0.1 The cost, measured here

ORIENT1 read ten of this repository's branches from 2026-10-04: 60 to 100 tool calls and 370 to 600 KB read before the
first edit, about a third of each branch; 278 shell greps and 117 shell dumps between them; decision files read whole
21 times (299 KB); files of 80 to 120 KB read whole for a few lines. `orient-report`, run over 26 of that day's
branches when ORIENT1 merged, put the median at 75 calls and 423 KB before the first edit. The code map named projects
and nothing in them, nothing pointed to it, and the knowledge server, started by `dotnet run` at each session, did not
connect. (It did not connect for the session that wrote this document either.)

### 0.2 What ORIENT1 built, and which half was this repository's alone

`tools/orient-index.mjs` writes `docs/index/` from the tree: a README naming what each file answers and every file over
40 KB (94 of them today); the bridge routes with their handlers and senders; the terminal verbs; the catalogue areas;
the test fixtures; an outline of each large file with line ranges; and a digest of every decision and dated note with
the lines each spans. `--check` fails naming each stale file; it is a declared gate and runs at every merge. The merge
tool writes the folder into each merge, and `.gitattributes` marks it `-merge`. `tools/orient-report.mjs` reads a
harness transcript and says what a session spent before its first edit.

Three parts of that are any repository's: **the README** (what each file answers, and the large files), **the
outlines**, and **the decisions digest**. The rest (routes, verbs, catalogues, fixtures) is this repository's answer to
*what do sessions here look for*, which every repository answers differently. The map design's ORIENT1a note said so:
no service reads it, the page draws nothing from it, and no other repository is asked to keep one. This design changes
the first and the last of those three.

### 0.3 What the repositories Daoris drives keep today

The install drives 29 repositories over two workspaces (D150's reading). None keeps a generated index of where things
are. The one whose sessions were read closely (KNOWUSE1, `docs/2026-10-03-knowledge-use-evidence.md` §2.1, §3) keeps
three hand-written indexes, one cross-cutting and one per part of the code, and a 129-line routing skill; its own
blocking gate opens them, and 20 of 22 working sessions ran it. Those sessions called `knowledge_search` 5 times in
4 sessions, and every answer said *matched on words only*. D129 saw the same install's sessions open hand-written index
files unprompted. So a second repository met the same need, by hand, and its sessions use what it built.

### 0.4 The two constraints

- **Every repository Daoris drives** gets the same start, written by that repository's own session and reviewed by its
  owner (D32, D124: Daoris publishes a set-up, never performs one).
- **A clone on a machine with no Daoris works exactly as well.** That is D48's coexistence clause (a Daoris-adopted
  repository stays fully workable, agents included, for contributors who do not run Daoris) and canon-authoring's test:
  *a file or a service*. A committed file costs a non-user nothing; a service is a dead end. So the floor is files,
  and their freshness must not need Daoris either.

## 1. The floor: committed files, kept true by the repository's own check

### 1.1 What every repository keeps

One folder, generated and committed, never edited by hand. Its name is the repository's; Daoris's is `docs/index/`.

| File | Holds | Every repository? |
|---|---|---|
| `README.md` | What each file in the folder answers; the command that writes it and the one that checks it; the indexes the repository keeps by hand, first, with what each answers; every file over the outline threshold, with its size and lines | Yes |
| `outlines/<path>.md` | For each file over the threshold: its declarations (code) or headings (prose), each with its line range | Yes, where any file is over it |
| `decisions.md` | Each decision's title and the lines of its entry, then each dated amendment's label and lines | Where the repository keeps a decisions record |
| One file per thing sessions look for | Rows by name, each naming a file and a line: entry points (commands, routes, handlers, jobs), names that cross files (configuration keys, message types, catalogue keys), test fixtures and helpers | The repository's choice, up to four to start (§2.2) |

What it does not hold:

- **The knowledge listing.** It already has a home: the doctrine's generated index (`<target>/INDEX.md`, D128) where
  the repository has taken up the doctrine, or the repository's own hand-kept index. The README links it and copies none
  of it. A knowledge document too long to read whole gets an outline like any other file. That is the "digest of
  knowledge" the row asks for: headlines where they already live, and outlines for what is long.
- **Status, prose or a tour.** Every row is a place to open. Anything else belongs to the brief or a document.

The threshold is the repository's. Daoris's is 40 KB, the size past which reading a file whole was the cost measured.

### 1.2 How it stays true

- **A generator in the repository's own toolchain**: its language, beside its other tools, needing nothing the
  repository does not already use. It reads the files git would stage (tracked, and untracked but not ignored), with LF
  endings, sorts by code unit and never by locale, makes no network call, and finishes in seconds, so the same tree
  writes the same bytes on every machine and the check can run often.
- **A check mode, inside the command that means done.** It builds the folder in memory and fails, naming each missing,
  stale or stray file and the command that writes it again. A stale index is a fact, so it fails (D54); a ceiling is a
  judgement, so it reports. Where the repository's test runner discovers tests by itself, the check is a test file it
  already picks up, and no build or CI file changes.
- **A merge regenerates.** The folder is marked so git never merges it as text (`-merge`, as Daoris marks it), and the
  README's first line says to run the generator on a conflict. Where the code host folds generated files in a review
  (an attribute such as `linguist-generated`), the folder is marked that way too, so a pull request's reader is not
  asked to read moved line numbers.

**Why the repository's own toolchain, and not Daoris's.** The digest goes stale with every decision note and the
outlines with nearly every change to a large file. A check that needed Daoris to put that right would stop every
contributor who does not run Daoris, on most branches, which is exactly what D48 forbids: nothing Daoris adds may sit on
a non-user's critical path. A contributor already builds with the repository's toolchain, so a generator in it costs
them one command they can already run. It also lets each repository index what its own code is reached through, which
no generic tool can know.

### 1.3 What the canon says

Three on-demand files change, and no always-loaded one. The words below are drafts for ORIENT2a; the build may reword
them, and they stay project-agnostic: no product, no path, no command.

**`development-documents`** (core knowledge, read on demand):

- Its roles table gains a row: **index** · *where things are in the code and the records, by what a session looks for,
  each with its file and lines; generated, never edited by hand* · *by lookup, before any search of the code*. The
  *by lookup* list under *Three ways a document is read* names it.
- *Why*'s bullet **The search before the work** gains the measurement: in one repository a third of each session's
  calls came before its first edit, spent finding its way, and a search names a file, not the line in it.
- A new section:

  > ### An index of where things are
  >
  > A session that knows what it needs but not where it is will search, and the search is its largest cost before its
  > first change. A search names a file, not a line, so the read that follows is the whole file; and a long record is
  > read whole to find one amendment in it.
  >
  > - **Generate it and commit it.** A tool in the repository's own toolchain writes it from the files, and nobody
  >   edits it by hand. Committed, because a file is read by every agent on every machine with nothing installed and
  >   nothing running. A hand-kept list is wrong the moment the code moves.
  > - **By what is sought, each row a place.** Group it by what sessions look for here (an entry point, a command, a
  >   key, a fixture), each row naming a file and a line range, so the read that follows is a range.
  > - **Outline what is too long to read whole**: its declarations or headings, each with its lines.
  > - **Digest the decisions**: each decision's title and lines, and each dated amendment's label and lines.
  > - **The repository's own check keeps it true**, failing on a stale index and naming the command that writes it
  >   again. A conflict in it is resolved by writing it again, never by hand, and it is marked as generated so a
  >   review folds it.
  > - **The brief names it in a line**, and says to open it before searching.
  > - **An index the repository already keeps stays.** The generated one links it and restates none of its rows.

- *Without the tool* gains a sentence: *The index is the repository's own: its generator, its check and its files need
  no doctrine tool, and neither does reading it.*
- The frontmatter's `enforces` gains *where things are is a generated index the repository's own check keeps true*.
  That changes the knowledge's row in each adopter's `INDEX.md`, which is read on demand.

**`doc-loader`** (core skill; only its description is always loaded, and it does not change) gains a step after the
generated index:

> 3. **Where things are.** If the brief names an index of where things are, start every search of the code there:
>    open the row for what you need, then read the lines it names rather than the file. Search past it only for what
>    it does not list.

**`set-up-documents`** (core skill) gains a step after *Place the records*, a clause in the close, and a template:

> 5. **Give it an index of where things are**, from `templates/index.md`. First list every index the repository already
>    keeps, written by hand or generated, and keep each as and where it is. One that is generated and checked is the
>    index: name it, and add nothing. Otherwise write a generator in the repository's own toolchain, beside its other
>    tools, needing nothing the repository does not already use and writing the same files on every machine. Put its
>    check in the command that means done. Mark its folder as generated, so a review folds it and a merge leaves it to
>    be written again. The generated index lists the hand-kept ones first and restates none of their rows. The brief's
>    *Where things are* names it.

The close (the skill's last step) adds *each index the repository already kept, and how the generated one differs*,
which is D129 §5's sentence, now said where the index is made.

`templates/index.md`, the README's shape:

```
# Where things are

Generated by `<the command that writes it>`; never edit by hand. `<the command that checks it>` fails when it is
stale and runs in `<the command that means done>`. On a merge conflict here, run the first command again.

Open the row you need, then read the lines it names.

| File | Answers |
|---|---|
| `<file>` | <what sessions look for that it names, each with its file and lines> |
| `decisions.md` | each decision's title and lines, then each dated amendment's label and lines |
| `outlines/<path>.md` | the declarations or headings of a file over <the threshold>, each with its lines |

## Kept by hand

| Index | Answers |
|---|---|
| `<path>` | <what it answers; nothing here restates it> |

## Files over <the threshold>

| File | KB | Lines |
|---|---|---|
```

**Why changes to three files and not a new canon document.** `development-documents` is already the document a session
reads when it sets a repository up or could not find where something is written, and its *Why* already names the
search before the work. A second document for one role would be read at the same moment, say half of the same things,
and add a row to every adopter's index. `doc-loader` is where a session is told what to open before it explores, and
`set-up-documents` is where a set-up is told what to write.

**The budget.** CANON7: the always-loaded core is 20,064 of 26,000 bytes, measured by `verify` on this branch. None
of the three files is in it, and `skills-workflow` does not change. A repository that declares the index (§1.4) pays one
row in its *Where things are*: 119 bytes as drafted, so this repository's region would be about 20,183.

**The bar of two sources** (`development-documents`: *two sources reaching one rule is the bar for believing it*).
This repository measured the cost and built the generated form. A second repository met the same need by hand, with
three indexes by part of the code and a routing skill its sessions open in nearly every run (§0.3). The canon takes the
generated form, because a hand-kept list is wrong once the code moves (canon-authoring: *a roster is generated, never
written*), and keeps every hand-kept one where it stands.

### 1.4 The declaration

The CLI's roles (`documents.ts` `ROLES`, D122 §2.7) gain `index`, after `router`: a path, the README's, with the job
*where things are in the code and the records, generated: open it before searching*. `sync` renders its row in *Where
things are*, and `check` fails when a declared path is missing, as it does for every role. A repository that declares
nothing sees no change (`documents.ts`' own rule). Daoris declares `docs/index/README.md`. A repository with no doctrine
tool writes the same line in its brief by hand (`development-documents`, *Where the records are*).

## 2. The set-up's part

### 2.1 Its own quest, after the set-up

The index is its own quest, *Give this repository an index of where things are*, published by the set-up press as a
set-up is (D124 §3, D117 §6): to the repository, as the person's, so its own session writes it and its owner reviews
it. The press composes it for a repository it finds set up (adopted, its domain declared, any set-up's branch landed)
and declaring no index, and the workspace plan
(D124 §5) paces it with the set-ups, one open at a time by default. It is not a step of the set-up quest: the set-up is
already the longest quest Daoris publishes, its pilot left a branch that could not be merged (D128), and an index that
fails must never hold a set-up back. A fourth title joins `SetupQuests`, so the machine log marks its session and the
usage report says what it cost (WSSETUP11).

### 2.2 What the session writes

- **The inventory first**: every index the repository keeps, hand-written or generated (its router, files named as
  indexes, routing skills, a code map, an architecture page), each with what it answers. Then the repository's checks,
  run before the first change (D128 §2).
- **The generator**, at the path the quest names, in the repository's language, under §1.2's rules. The press names the
  path and the two commands (write, and check) before the ask, from the stack it reads (D77's `SelfDescription`: a
  Node, .NET or Python repository each has one obvious form), and grants exactly those two commands as it grants the
  set-up's verbs (LAYOUT7: the rule goes in before the ask, and comes out if the ask is refused). That widens little:
  the repository's own test command, which its declaration of safe work already lets a session run, executes the
  repository's code too.
  A stack the press does not know gets no quest; the press says so, and the person may publish the ask by hand.
- **The tables**: up to four, of what this repository is reached through, each row a file and line. Where the
  repository has driven sessions behind it, the quest's body carries the files they read most and the words they
  searched for, counted by the press from those sessions' typed events as repository-relative paths and words, never a
  machine path, so the tables answer what sessions actually looked for. Otherwise the session chooses from the code.
- **The check**, inside the command that means done, as a test the runner discovers where it can.
- **The marks**: the folder `-merge`, and generated for review where the host reads such a mark.
- **The declaration**: the brief's one line, and `documents.index` where the repository has taken up the doctrine.
- **The close**: the checks at the end, green where they were green before (D128 §2), the index's own check among them;
  each hand-kept index named with how the generated one differs; nothing deleted.

**The bounds** (D128 §3, amended): the quest writes only the generator, the index folder, the check's test or its one
entry in the command that means done, the attribute lines, the brief's line and the manifest's role. It moves nothing,
changes no other source, build or CI file, and adds no frontmatter.

### 2.3 A repository that already keeps an index

| It keeps | The quest |
|---|---|
| A generated index with a check | Names it in the brief and the manifest, and writes nothing else. What it lacks goes in the close, for the owner |
| A generated index with no check | Leaves its generator alone and says in the close that nothing keeps it true, so the owner can add the check. No second index |
| Hand-kept indexes (by part of the code, a cross-cutting rule index) | Keeps each where and as it is; the generated README lists them first under *Kept by hand*, and its tables restate none of their rows |
| A routing skill of its own | Keeps it whole. The canon's discovery skill never replaces it (KNOWUSE1 §2.1: the pilot replaced a 129-line routing skill with the canon's 34-line one) |
| A file or folder at the index's usual path | Picks another folder; never overwrites |

### 2.4 Daoris's own index

It already meets §1: generated, checked, merge-written, its README naming each file. ORIENT2b declares it; its README
gains the *Kept by hand* heading saying *none*, so a reader can tell an empty section from a forgotten one.

## 3. Where Daoris is present

### 3.1 What the knowledge service indexes from each repository's committed files

Today (`RepositoryScanner`): the rules, as files or the doctrine region; the knowledge, including a declared folder
(D128); the skills; each room's `AGENTS.md`; the declared router; the decisions, fixes and archive, split into one entry
per decision, fix or outcome, a folder of records titled by its first heading (DOC8c); the README until the repository
adopts (D124 §6). Never the source code (D124 rejected indexing code as a baseline).

ORIENT2e adds **the declared index**: every markdown file in the folder the declared README is in, split at headings as
a log is, as entries of a new kind, `index`, labelled by path, the repository's own provenance. A where-question then
finds a place across the workspace, and a session in one repository learns where another keeps something without a
checkout it may read. The index is small, reviewed, and written by the repository about itself, which is why it is
indexed and the code is not. Its entries feed a shared deployment the way all knowledge does: from the default branch,
stamped with the commit (D48).

`knowledge_search`'s `kinds` gains `index`. A search with no kinds includes index entries, at most two of them, so a
question about why something was decided is not crowded out by rows of identifiers.

### 3.2 A hit names its lines

ORIENT1 found decision files read whole 21 times because a search names the file, not the note. Every entry keeps the
lines it spans in its file, and every hit names `path:start-end` and the line its excerpt starts on. `knowledge_get`
takes an optional range, so a session without the repository's checkout reads the passage, not the entry whole. This
needs no digest: the service reads the lines from the file it indexes.

### 3.3 How a driven session is handed it

- **The search**: the `daoris-knowledge` connector, with `knowledge_search` and `knowledge_get`. On the protocol door
  the driver hands it on `session/new` and writes nothing into the repository (ACP4, `KnowledgeConnector`).
- **The look starts at the index.** The look before asking already names the indexes the session's tree keeps
  (KNOWUSE1c, `RepositoryIndexes.Find`: the declared router, then files named as indexes). ORIENT2d names the declared
  `documents.index` first, read from the tree, in a sentence of its own: *This repository keeps an index of where things
  are, `<path>`: open the row you need, then read only the lines it names, before searching the code or the knowledge.*
  Where none is declared, the instruction reads as it does today.
- **The prompt is Daoris's, composed per session and never committed**, so a clone without Daoris loses only the
  sentence, and the brief carries the same pointer. Recall pushed into the prompt stays held (D129 §6, D135 §1): this
  names a file the repository wrote, not a ranked guess.

### 3.4 The semantic tier, per deployment

The service reads three variables when it starts: `DAORIS_EMBED_MODEL` (a model's name, which turns the tier on),
`DAORIS_EMBED_URL` (its endpoint, a local Ollama address unless set) and `DAORIS_EMBED_WINDOW` (the most characters one
vector carries, 2,000 unless set; a longer entry is embedded in pieces, D123). Settings → AI features says so, and that
a change takes a restart; `knowledge_refresh` says which recall is active, or why the semantic half was skipped. On a
local install they are the environment its service host starts in, so the model can be a local runtime; on a team's
server they are its own, chosen for cost and throughput (D24's table). Nothing in the index or the canon names a model.

What it adds here: a where-question in other words. *Where is a failed payment tried again* can reach the outline row
of a class named for the queue that does it, though the two share no word. The two halves are fused by rank, never by
score.

### 3.5 The floors, in order

| What is present | What answers | What it says |
|---|---|---|
| The service, with a model | Words and meaning, fused by rank | Nothing more, or why a configured half did not answer (TIER1) |
| The service, no model | BM25 over the same entries, index rows included | *Matched on words only* on every result (TIER1) |
| No service | The committed files: the brief's line, the README, the row, the lines; the agent's own search over them | — |

An index row is made of names, and a name is a word, so the words-only tier already finds a question asked by name. The
last row is §4.

## 4. What breaks without Daoris: nothing

A contributor clones the repository onto a machine with git, the repository's own toolchain and an agent harness, and
nothing of Daoris: no CLI, no service, no driver, no connector, no model endpoint.

| Step | What the session reads or runs | Needs Daoris? |
|---|---|---|
| 1. Clone | The root instruction file (the brief, the doctrine region), the doctrine's `INDEX.md`, the skills in both agents' folders (D117's mirror), the index folder, the generator and its check | No: all committed |
| 2. Start | The harness reads the root instruction file (`CLAUDE.md` carries `@AGENTS.md`; codex and dsh read `AGENTS.md`), under the 32,768-byte cut (D128). The brief's line: open the index before searching; *Where things are* names it | No |
| 3. Discovery | `skills-workflow` sends it to its agent's skill list; `doc-loader` reads the router, then `INDEX.md`. Its step 2 asks a search *where a search over this repository's knowledge is connected*, and says *where none is, the index searches are the whole step*: none is | No |
| 4. Orientation | `doc-loader`'s new step: the index's README, the row, the outline, the lines | No |
| 5. The change | It edits; a large file's declarations move | No |
| 6. Done | The brief's command that means done runs the index's check. It fails, naming the stale outline and the command that writes it; the session runs that command, in the repository's toolchain; the check passes; both are committed | No |
| 7. A need elsewhere | `repository-owns-its-work`: *as a quest where a request system exists, and as a message to that repository's owner where none does* | No |
| 8. A merge | The index conflicts as `-merge` leaves it; the README's first line says to run the generator; it is resolved | No |
| 9. Review | The host folds the generated folder | No |

**What mentions Daoris** is vocabulary and files: the region's first line says it is generated, and `daoris.lock`
exists. Both cost a non-user nothing (canon-authoring's file test). `daoris check` runs only where a repository put it
in its own CI; ORIENT2 adds to it only that a declared index path exists.

**What could break it, and what holds it.**

- *A canon sentence that sends a session to a service with no alternative.* The D48 §2a scan in `dogfood.test.ts`
  holds every canon file to naming the tool-absent path; the words in §1.3 name only files, and ORIENT2a runs the scan.
- *A generator that needs something the clone lacks.* It uses only the repository's own toolchain (§1.2), which a
  contributor needs to build at all. ORIENT2f proves it on a clone with no `daoris` on `PATH`.
- *An index stale on the line*, where a contributor's CI does not run the command that means done. A stale row points
  a few lines off: the session sees the declaration is not there, searches as it would have without an index, and the
  next run of the check writes it again. It degrades to today; it never misleads silently for long.

## 5. The measure

### 5.1 Where the driven sessions' transcripts are

On the install the home is `data/` (D63, D93). Under it, per session:

| File | Holds | Usable for the report? |
|---|---|---|
| `sessions/<id>.events.jsonl` | The typed events (D76 §2): each tool call with its kind (ACP's: read, edit, delete, move, search, execute, think, fetch, other), title, locations, input (cut at 2,000 characters), output (cut at 2,000) and content text (cut at 64 KB), every cut saying the original length, `… (N chars)` | **Yes**: both doors write it |
| `sessions/<id>.log` | The rendered transcript, text a person reads, never the wire's JSON | No |
| `sessions/<id>.harness.json` | The harness's own conversation id (ANSWER1a) | Only to name the conversation |
| `logs/<day>.<source>.jsonl` | The machine log: `session.started` (session, kind, adapter, repository, workspace, `setup`), `turn.ended` (calls, tokens, context), `session.parked` | Yes, to name and group sessions |
| `trees/<workspace>/<repository>/<name>/` | Each session's working tree | Yes, to tell an edit of the work from any other write |

The harness's own transcript, the JSONL `orient-report` reads today, lives in the account's configuration home, which
Daoris never reads (D125, as `HarnessConversations` says). So ORIENT1e teaches the report the events file.

### 5.2 What ORIENT1e reads, and what it cannot

- `--home <dir>`: every quest's session the machine log's `session.started` names (by its kind), set-ups excluded, and
  each one's events file.
- **A call** is a tool event's id; later events with the same id are its updates. **Its kind** is the event's tool kind,
  and for a shell call its command, from the input's `command`, classed as the report classes one today. A
  `knowledge_search` is counted by its title.
- **What it read** is the larger of its content text's length and its output's stated length, in characters, since the
  events keep characters. The report says *characters* where it read events and *bytes* where it read a harness
  transcript, and compares neither with the other.
- **The first edit** is the first edit-kind call whose first location lies in the session's tree and outside `local/`.
- **Before and after** split each repository's sessions at the commit that first added its declared index to its line,
  read with a read-only `git log` in the registered checkout.
- **It reads only.** It touches no process, writes nothing under the home, and its output stays on the machine; a
  design that quotes it records counts, de-identified as KNOWUSE1 recorded its evidence.

It cannot see a call's input past 2,000 characters, which a read's path and range never reach. Whether each door's
events carry a read's text, and so its size, is to be confirmed on the install's own files by ORIENT1e's first run;
calls and kinds are on both.

DOC7's planned `session.read` line would be a second source for the same reads. ORIENT1e needs no new line, and neither
waits on the other.

### 5.3 Before and after

**Before** is every driven quest session on the install since events were kept, because no repository there has an
index yet. ORIENT1e's first run gives it, per repository and over the workspace. Its numbers are not in this document: the
install was not read for it. This repository's own before (§0.1: a median of 75 calls and 423 KB over 26 branches)
is the nearest reference, not a baseline: driven sessions run other work, in repositories they have not seen.

**After** is the same report once a repository's index has landed. The targets, per repository with an index and over
the workspace, on the median editing session:

| Measure | Target |
|---|---|
| Calls before the first edit | Half the before |
| Characters read before the first edit | Half the before |
| Shell searches and dumps before the first edit | Half the before |
| Whole reads of files over the outline threshold before the first edit | A quarter of the before |
| Sessions that open the index before their first edit | 8 in 10 (the before is 0) |

Halving is ORIENT1's own target, kept so the two read the same way.

### 5.4 Guards

- **Parks per ten sessions are not higher.** An index that sent sessions astray would show as questions to the person.
- **No session lands with its index stale**: the repository's check, run at the close, is the proof.
- **A clone with no Daoris reaches the same first edit**: ORIENT2f's rehearsal phase, run at every merge it reaches.
- **Few sessions per repository** makes a per-repository median noisy, so the workspace's figure decides and each
  repository's is shown beside it.

## 6. The build

In order: ORIENT1e first, so the before is read before any index lands; ORIENT2a and ORIENT2b together; ORIENT2c,
ORIENT2d and ORIENT2e in any order; ORIENT2f; and ORIENT2g at the owner's pace. Each row notes its amendment where it
lands.

- [ ] **ORIENT1e — the orientation report reads a Daoris home** (tools). Driven sessions are kept as typed events, not
  harness transcripts. `--home <dir>` reads each quest session's `.events.jsonl` (named by `session.started`), finds
  its first edit inside its tree, and splits each repository at the commit that landed its index. Contract: design §5,
  D151 §7. Proof: fixtures from both doors; the before.
- [ ] **ORIENT2a — the canon teaches the index** (canon, examples). Every adopter learns to keep a generated index its
  own tooling keeps true. `development-documents` gains the `index` role and a section, `doc-loader` and
  `set-up-documents` a step each, with `templates/index.md`, a changelog entry and both examples re-synced. Contract:
  design §1.3, D151 §3. Proof: canon tests, the D48 §2a scan; core bytes unchanged.
- [ ] **ORIENT2b — the index declared** (cli). The brief's line is what sends a session to the index first. `index`
  joins `ROLES` after `router`; `sync` renders its row in *Where things are*; `check` fails on a missing declared path;
  Daoris declares `docs/index/README.md`. Contract: design §1.4, D151 §4. Proof: documents tests; the region measured,
  about 120 bytes more.
- [ ] **ORIENT2c — the index quest** (driver). A driven repository gets its index from its own session, once set up.
  The press and plan publish *Give this repository an index of where things are*: inventory first, a generator at a
  named path with its two commands granted, its check, the declaration, nothing else. Contract: design §2, D151 §5.
  Proof: `SetupBriefTests`; the family rehearsal.
- [ ] **ORIENT2d — the driven look starts at the index** (driver). A session told where things are reads ranges, not
  files. `RepositoryIndexes.Find` names the declared `documents.index` first, and the look says to open its row, then
  its lines, before searching. With none declared the instruction reads as today. Contract: design §3.3, D151 §6.
  Proof: `AskAndWaitPromptTests` goldens, each failing first.
- [ ] **ORIENT2e — the service indexes the index; hits name lines** (service). With Daoris present, a where-question
  finds a place across the workspace. The scanner reads the declared index, split at headings, as
  kind `index`; entries keep their lines; a hit names `path:start-end`; `knowledge_get` takes a range. Contract: design
  §3.1–§3.2, D151 §6. Proof: scanner and tool tests; the tier line.
- [ ] **ORIENT2f — the fresh-clone proof** (examples, tools). An index must work where nothing of Daoris is installed.
  One example gains a generator, its check in its own test, and its declaration; the family rehearsal clones it with no
  `daoris` and no service, moves a line, sees the check fail, regenerates, passes. Contract: design §4, D151 §2. Proof:
  the new phase.
- [ ] **ORIENT2g — the after** (the owner's install, read only). Once index quests land at the plan's pace, ORIENT1e
  reads the same home again. Targets before the first edit: calls, characters, and shell searches and dumps halved;
  whole reads of outlined files a quarter; 8 in 10 open the index; parks no higher. Contract: design §5.3–§5.4, D151
  §7. Proof: an evidence note.

## 7. Rejected

- **Daoris writes the index** (`daoris sync`, or a `daoris index` verb, even for the generic half: outlines and the
  digest). Every decision note would stale it, so every contributor would need Daoris to make a branch pass: a non-user's
  critical path (D48). And no generic tool knows what a repository is reached through.
- **A reference generator shipped in the canon and copied in.** One script would put its language's toolchain on the
  path of every repository that does not use it; one per language would make the canon carry code in stacks it cannot
  test. The template carries the shape and the rules; the session writes the code in the repository's language.
- **Generated at a session's start and ignored by git.** It costs a call and a permission before the first read, leaves
  nothing on the line for the service, a code host or a reader, and a session that skips it searches as before, which
  is the failure measured.
- **The service as the index**: no committed files, a search instead. A dead end without Daoris (D48 §2a), and ORIENT1
  found the server not connected in practice.
- **The index in the always-loaded region.** It grows with the repository and pushes the rules past one agent's cut;
  D128 and D129 rejected the knowledge table in the region for the same reason.
- **A hand-kept index added by the set-up.** Wrong the moment the code moves. A repository's own stays (§2.3); none is
  added.
- **The index as a step of the set-up quest.** The set-up is the longest quest already, and an index that fails would
  hold it (§2.1).
- **One format the service parses**, a schema every repository's generator must write. Each generator is the
  repository's; the service reads headings and rows as text, which every generator writes anyway.
- **Indexing the source code in the service**, or embedding every file. D124 rejected it as a baseline; the index is
  the repository's own small, reviewed statement of where things are.
- **Pushing index rows or recall into the driven prompt.** D129 §6 holds the push for measurement; the prompt names one
  file.
- **A new canon document for the index** (§1.3): read at the same moment as `development-documents`, saying half the
  same things, and one more row in every adopter's index.
- **A merge driver that regenerates.** Its configuration is not tracked, so a clone falls back to conflict markers
  (D134's reason); `-merge` and a line in the README work everywhere.
- **Replacing a repository's hand-kept index or routing skill** with the generated one or the canon's: the pilot's
  mistake (KNOWUSE1 §2.1).

## 8. What this amends

Each when its row builds, noted where it lands:

- **The map design's ORIENT1a note**: *no service reads it* and *no other repository is asked to keep one* end. The
  service reads a declared index (ORIENT2e), and every repository Daoris drives is asked for one (ORIENT2c). The page
  still draws nothing from it.
- **D122 §2.7** (DOC3): a role, `index`. **D122's canon**: `development-documents`, `doc-loader` and
  `set-up-documents`, with a changelog entry for every adopter.
- **D124 §3 and §5**: the press publishes a fourth quest and the plan paces it. **D124 §7, through D135 §4**: the look
  names the declared index first.
- **D128 §3**: the bounds, for the index quest. **D129 §5**: the close names each hand-kept index where the index is made.
- **D134's ORIENT1b note**: a digest of the decisions, now recommended to every repository that keeps a decisions
  record. D134 point 7 stands: the canon still does not recommend one file per decision.
- **The service's search**: a kind, lines on every hit, a range on `knowledge_get`.

D24, D32, D48 §2a, D54, D76 §2 and D47 §4 (the events stay on the machine), D125 and D129 §6 stand.

## 9. What this document does not cover

- **Nothing is built.** The canon's words are drafts; the CLI, driver and service shapes are designs.
- **The install was not read.** The before is ORIENT1e's first run. The 29 repositories and two workspaces are D150's
  reading; the second repository's indexes and its sessions' use of them are KNOWUSE1's evidence, not re-read.
- **The events' sizes.** That the native door keeps a read's text in the events, and so its size, was read from the
  code (`StructuredOutput`, `SessionEvents.Bound`), not from an install's file. The protocol door keeps what its adapter
  sends as content or raw output (`Acp.cs`); whether each adapter sends a read's text is not known here.
- **Whether a session can write a sound generator in each stack**, in what time and at what cost, is the first index
  quest's to show, as WSSETUP12's pilot showed the set-up's.
- **Review noise.** How many index lines a typical change moves, and whether a host folds them, is not measured.
- **Which door hands the connector** on each of the install's adapters is ACP4's code, not re-read per adapter.
- **The second source** met the need with indexes of knowledge by part of the code, not of code by place; §1.3 counts
  it as the same need, which is a reading.
- `verify` checks this document's links and D151's shape, and none of their words.
