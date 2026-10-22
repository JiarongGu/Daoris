# Daoris.Service — the cross-repository knowledge service

An MCP server over stdio exposes the index to any agent session, and an HTTP
host carries the same service for a browser or a remote deployment. The core reads a repository's
knowledge into addressable entries, classifies each as canonical or local, stores them in SQLite,
answers ranked queries over FTS5, and finds where repositories learned the same lesson independently.
Quests and pushed registrations persist in the same database, so what one session publishes another
session — or another machine's `connect` — finds waiting. Its tests are the `service` gate in
`daoris.gates.json`, which runs the solution: `Daoris.Service.Tests` for Core and the MCP door, and
`Daoris.Service.Http.Tests` for the HTTP host's own doors, started in-process with no port bound
(HTTP1). Gate logs carry the counts; dated integration reviews record the combined verification.

Start with [the contributor guide](../../docs/development.md) for checkout setup and
[the document router](../../docs/README.md) for the current contracts. This is a component reference:
read the registry, quest or host section needed. `docs/index/routes.md` locates route handlers;
the route table below explains their semantics rather than serving as the exhaustive route roster.

Since **D59** the always-loaded tier is a region of `AGENTS.md` rather than a directory, so the
scanner reads `DoctrineRegion` for a repository's rules and a gate holds that a canonical rule stays
searchable. The ledger side grew with it: a session record keys on the **working tree** it was held in
(D51), not on the repository that owns it, and carries the diff a review reads (SURF6a). A `stopped`
record says whether it was **interrupted** (D104): ended by the driver's orphan sweep or its shutdown,
not by the person. The ledger carries an interrupted take on as it does a failed one (D80); the column
arrives by `SchemaColumns`, so an older record reads as the person's. A person's stop holds its quest in
the driver until *Try again* releases it (SESSUX1b, D126), so the ledger carries it on too, but only a take
a session of this machine's made (`took`): a stop before the take leaves the quest to whoever took it. Every
carry-on asks whose the take is (CARRY2): one the quest's log says another machine made, a lost race
included, or one made here after this machine's last session on the quest ended, is refused as
`TakenElsewhere`, naming the teammate's session where one is on the quest. A `failed` record says whether an account's **limit** refused its turn (TOOL4c, D125 §5.2), as the driver
read it from the door's failure. It names no account, so it is answered to every caller and travels with
the record both ways; an older record, or a feed from a build before it, reads false.

Since **D117** (LAYOUT4) the scanner reads the layout a repository's lock was written under, never an
assumed `.claude`: the lock's root before the manifest's, and the root a layout moved from; both
`.agents` and `.claude` for a repository with no lock; the lock's mirrors skipped, so a skill is found
once, at its source; each declared room's `AGENTS.md` as the repository's own knowledge; and never a
link, a junction or a link held as text (`RepositoryLayout`, `RepositoryLinks`, twins of the CLI's
`layout.ts` and `links.ts`).

Since **D122** (DOC5) the scanner also reads where a repository says its records are, in its manifest's
`documents`. The declared decisions, fixes and archive are each the first candidate for their log, a file
or a folder of records, so a log at a name no candidate knows is found, and found once. A record in a
folder is titled by its first heading, or by its file name where it has none (D134, DOC8c). The declared
router is indexed as one document, and the declared index of where things are at its headings (ORIENT2e, below). A
declaration adds a path and is never required. One the CLI
refuses is read as none, so the scanner reads its candidates as it did before (`RepositoryDocuments`, a
twin of the CLI's `documents.ts`).

Since **ORIENT1g** (D134 §5 as amended) a decision in a folder is more than one entry: its text before its
first dated note, at the id it always had, then each dated note an entry of its own, titled by the decision and
the note's label (`D125 › Built 2026-10-04 (TOOL6g): …`) and anchored by that label. A note is found and
labelled as the decisions digest (`docs/index/decisions.md`) finds and labels it (`DecisionNotes`), and the
two are held to one table, `tools/orient-index-fixtures/decision-notes.json`, row for row (ORIENT1h):
`DecisionNotesTests` holds this side, `tools/orient-index.test.mjs` the digest's. Measured when it was built,
over this repository's 152 decisions the split gave the digest's 291 note rows, the same lines and labels in the
same order. A fix, an outcome and a decisions log in one file keep their notes inside.

Since **ORIENT2e** (D151 §6, `docs/2026-10-04-orientation-everywhere-design.md` §3.1–§3.2) the scanner reads a
repository's **declared index of where things are** (`documents.index`, the role ORIENT2b added; `RepositoryDocuments`
lists it after the router, as the CLI does): every markdown file in the folder the declared README is in, and below,
split at every heading (`IndexSections`), as entries of a kind of their own, `index`, the repository's own. Each is
titled by the headings above it (`Bridge routes › DAORIS.DRIVER (102)`), and text before any heading by the file's
path. Since **ORIENT2h** a table is read a row at a time: each row an entry of its own, titled by its section's
headings and its label, its first cell with text named by its column
(`Bridge routes › DAORIS.DRIVER (102) › Route: SESSION_GO_ON_NEW`), its body its line as written, so it names that
line. Read as part of its section, one matching row was weighed against a long section, and a route's row in a 102-row
table lost to short sections (D151's ORIENT2e note); the column's name is in the title because the header is no line of
the row, and without it *sync verb* missed the `sync` row (D151's ORIENT2h note). The header and its separator are no
entry. Since **ORIENT2h2** a list is read an item at a time, for the same reason: a fixture's item was answered by
its list's 61-line section. An item is its line and its continuation lines, as markdown reads them, titled by the
headings, its parents' labels and its own (`Outline of … › 300-2333 class QuestStore › 688-724 PublishAsync()`), so a
nested item read alone keeps its place; its label is its leading code span or link text, or its text where it opens
with neither. Since **ORIENT2h5** an item's id is its title without the lines each label leads with
(`… › class QuestStore › PublishAsync()`; a fixture's `Tests.cs:284` is `Tests.cs`), in a deployment's index too, so
an edit above the lines it names leaves its id as it was; the title keeps them, for the person who reads it. The prose around a table or a list stays a section, each run of it an entry. A README declared at the root is read alone, a declared folder is that folder, and an undeclared folder named
like an index is never guessed. A folder a deployment names as its index (`DAORIS_KNOWLEDGE_INDEX`) is read a row at a
time first, as that deployment chose, and those rows are of the kind `index` too. A search that names no kinds answers
with at most two index entries, at their ranks, and fills the rest from every other kind; the answer says when more
matched (`SearchAnswer.MoreIndex`). Convergence leaves the index out unless it is asked for by kind: two indexes
generated from one template have learned nothing together.

**Every entry keeps the lines of its file it is** (`KnowledgeEntry.Lines`): a whole document, a section, a decision and
each of its notes, a README's opening, a rule out of the region, an index section or row. Line `i` of the body is line
`first + i` of the file, counted from the file's first line whatever the read trimmed above it. A deployment's index
row is its cells labelled by their columns, not its file's line as written, so it names none, and neither does a fed
entry whose feed named none or named lines its body cannot be. A hit names `path:first-last` and the line its excerpt
starts on (`KnowledgeHit.ExcerptLine`, from `Text.ExcerptAt`), the frontmatter it skipped counted; `knowledge_get`
reads a range from the entry as indexed, never from the disk, so a deployment fed by another machine reads it too. The
store keeps them (`first_line`, `last_line`; schema 5, so an older index is rebuilt when it opens, and a newer one
is left as it is and refused, KSCHEMA1 below), `/api/search`
answers `firstLine`, `lastLine` and `excerptLine` beside every field it had, `/api/entry` and `/api/entries` the first
two, and `/api/feed/entries` takes them, digesting them only where an entry names them.

Since **D124** (WSSETUP8), until a repository adopts, the scanner reads its root `README.md`, in any case,
as the repository's own word. Each section split at level-two headings is one local knowledge entry carrying
the file's path, and the part before the first heading is titled *README*. It claims no role, and is never
read through a link, from `docs/README.md` or in another format. A file a declaration already names is read
by that role. Once the repository has a lock, the README is not read, and the next refresh drops its entries.

## The registry — who is out there, and what they own

Each repository declares a `domain` in its manifest: a one-line summary, the areas it **owns**, the
kinds of quest it **accepts**, and, optionally, the repositories it **uses** (D91), which the map draws
as dependencies. The service reads those while indexing and serves them together. `uses` is read by
one rule on every door (`Declared.Uses`, the CLI's `usesOf` its twin): trimmed, blanks and repeats in
any case dropped, the repository's own name dropped, absent as empty.

**Search answers "has anyone solved this"; the registry answers "whose problem is this."** Those are
different questions, and only the second tells you where a change belongs — which is what makes a quest
addressable rather than a guess.

Declared in the manifest rather than configured here, so it sits next to the thing it describes and is
reviewed by the people it describes. A central list would drift the moment a repository changed and
nobody remembered to update the server.

**Registered is addressable; adopted is disciplined** (D70). A repository that has adopted can be asked,
and one registered on this machine with a root can be asked too. The second has no connector in its
files, so only a session the driver starts in it over the protocol door answers, handed a connector on
the wire. Declaring gates nothing: a repository that has adopted but said nothing is still reachable, and
the asker is simply warned it may not be that repository's problem. Repositories that have not adopted
are **listed and marked** with who can answer them, because "who cannot be asked yet" is the same
question as "who can", and a silent omission reads as the repository not existing.

**A folder becomes a workspace in one statement** (D77). `POST /api/registry/import` with a
`workspace` wires every row it registers to that circle (`daoris import <folder> --workspace <name>`).
A statement re-points, and an import that names none moves nobody.

| Tool | What it answers |
|---|---|
| `registry` | Who is in the family, what each owns, what each accepts, the lanes each declares (by the `repository:lane` that asks one), who is not addressable |

## Quests — work one repository asks of another

Repositories in this family are not developed across (`docs/DECISIONS.md` D32). A change one needs from
another is a **quest**: published here, and *pulled* by the repository it is addressed to.

**It is held by the service, not written into anyone's files.** The first version of this wrote the
quest straight into the receiving repository's backlog, which is the same trespass in a smaller form —
an outside edit is still an outside edit when it is one file and uncommitted, and it still arrives from
whoever knows that codebase least. It was also incompatible with D8: reaching a central store means the
network, and nothing in the CLI may open a socket. So the CLI has no quest command, and this does.

| Tool | What it does |
|---|---|
| `quest_publish` | Ask another repository for something, with `links` and `attachments` (paths on this machine) if the ask needs them, `then`, the steps to ask next once it is done (a step's `setUpIn` makes it a set-up step), and, for a quest an ask asks, `requirements`, each the person's own words quoted with the check that proves it (DRIFT1c) and, optionally, its `evidence`, paths the done's commit must hold (EVID1a), `shortTitle`, at most 40 characters (SESSUX1j), and the chain's `review` on the person's quoted words, or an intake's `reviewProposal` with its reason (REVIEWENV1b). Refuses a repository nothing here could answer: unregistered, or unadopted with no root (D70). `to` may be `repository:lane` or `repository:lane+lane` for lanes the registry lists, your own repository's included; a lane nobody declared is refused, naming the lanes there are (D115) |
| `quest_list` | What has been asked of whom, and what is still outstanding: links, file names, what each follows and what follows it, each requirement's evidence, what was last read of it, and why a held done waits |
| `quest_respond` | `take`, `done` or `decline` — declining needs a reason. A `done` on a quest with requirements gives `answers`, one per requirement by its number: `met` with how its check was met, or `departed` with the reason and the person's words it turns on (`quote`), which must be theirs. One left unanswered is refused, naming it; a departure holds what follows the quest for the person's yes (DRIFT1d). A `met` answer on a requirement naming evidence holds it until Daoris reads that evidence, and the answer names what is read in the session's last commit (EVID1a) |
| `go_ahead_ask` | Ask the person for a go-ahead on an act outside the repository (`kind`: write, release, push, sign-in or run; `on`, where it lands; `act`, what it touches; `why`), held on the ask the session's quest was asked by, one per act. A request naming every word of an act already asked, its kind and place the same, joins it and is told its answer; one sharing only some of its words is asked once more, naming the near one. A session on no ask is told to say it in its last message instead (KNOWUSE1a). A set-up step's request whose place reads as production is refused, and nothing is asked (REVIEWENV1b) |
| `question_ask` | A driven session's agent records what only the person can decide before it ends its turn on it: the `question` (at most 1000 characters), the `choices` it offers (at most six, each its `label`, at most 80, and what it `does` on it, at most 300; labels unlike each other), the one it may `recommend` by number, and `by` when the work needs the answer (at most 200), each its own words. Written as `<session>.question.json` under the home's sessions folder (`QuestionBox`), a later question replacing it; the driver keeps it only for the park its run ends in. A conversation, a helper, a reviewer, an intake, Ask Daoris's own conversation and a record on no quest are refused, and a go-ahead stays `go_ahead_ask`'s (PARKVIEW1) |
| `review_serve` | A set-up step's own session names the `folder` its build wrote, in its tree with no link on the way, and the environment's `address`, an origin; kept on its record for the set-up it says next. This build does not serve it yet, and says so (REVIEWENV1b, REVIEWENV1d) |
| `review_ready` | A set-up step's own session says what it set up: `look`, where its tab is; `shows`, at most 300 characters; `again`, at most 600; and a review run's `run`, quoted from the procedure. Kept on its record until its driver posts it with the commit it read; the step's done is refused until one is said (REVIEWENV1b) |
| `opinion_give` | A second opinion's own reviewer says it, once, within the rule's minutes: at most 20 `findings` (`weight`, `where`, `claim`, `consequence`, `reproduce`, `sure`, a `proposal`), what it `read` and its `limits`, and a recheck's word on each first-pass finding (`rechecked`). Any other session, and a pass that ended or ran out of time, is refused (XAGENT1c) |
| `opinion_answer` | The working session a pass's findings were handed to answers each by its number: `fixed` with a commit, `rejected` with `evidence`, or `unresolved` with `why`. The latest answer to a finding stands, until a recheck has read them (XAGENT1c) |
| `helper_ask` | A working session asks another agent a `question` (and optionally an `agent`): written as an ask file under the home's `helpers` folder for the driver to take, and answered at once with a sentence. A helper, a reviewer and an intake are refused (a helper cannot ask for a helper); the person's `helpers` rule is the driver's to apply (MIXAGENT2a-i). `urgent` asks the answer at the asker's next step while it works, kept as `urgent: true` on the ask file; an ask with none is ordinary, its next turn (COWORK1d, D188) |
| `helper_answer` | The helper the driver started says its `answer`, once, for the ask the driver's state file names it the helper of; any other session is refused (MIXAGENT2a-i) |
| `partner_answer` | The partner the driver started for a piece says its `summary` (at most 20,000 characters) and what it `left` undone (at most 4,000), once, written as `<piece>.answer.json` under the home's `partners` folder (`PartnerBox`) while the driver's state names it the piece's partner and `started`; any other session is refused. Its files are its piece, which the driver reads itself (COAGENT1b) |
| `handover_propose` | A working session's agent asks to hand its session over to another agent, with its `note` for the next one (at most 4000 characters), kept as its claim: written as `<session>.hand-over-ask.json` under the home's sessions folder (`HandOverAskBox`), one ask at a time, and answered at once with what each of the person's `handOver` rules does with it. A helper, a reviewer, an intake and Ask Daoris's own conversation are refused; the rule, the next agent in the person's order and whether the session had a turn since it was itself handed over are the driver's, written in its `<session>.hand-over-state.json` (MIXAGENT1c). Not Ask Daoris's `hand_propose` |
| `line_read` | A session that continues another (a hand-over, an account move, a carry-on, a fallback) reads its line's history: the parts its record's `from` chain names, oldest first, and itself, each part's `<id>.events.jsonl` under the home's sessions folder (`LineEvents`), read on this machine and never served over HTTP (D76 §2). `part` (left out, the part it continues, else itself), `before`, a number an answer named, and `limit`, 1 to 200 (50): a page of entries oldest first (the person's and Daoris's words by their marks, the agent's messages joined, tool calls with their updates, plans, turns' ends, notes; never a thought), at most 40,000 characters with a Han character as three, one entry heavier than that cut and the cut said, and where to read on. A part off the line, an id that is no local id, no session, no record and no home are refused, nothing read (SESSCTX1f, D189's note) |
| `housekeeping_inspect`, `housekeeping_clean_up`, `housekeeping_discard_branch` | Ask Daoris's housekeeping (HELPMODE1c2a): a look at Daoris's own branches (optionally one `repository`), a clean-up of the `branches` named in one `repository`, a discard of one session `branch`. Each is a keyed request in the home's session requests for the conversation's own session, which the driver's loop running it takes, judges (the person's grant now and at this start, the conversation's workspace) and answers beside it; the tool waits ten seconds for the take, withdraws one nobody took, then waits 45 for the answer. A session that is not an Ask Daoris conversation is refused here; targets are names, never a path |
| `settings_notify`, `settings_language`, `settings_timeout`, `settings_cap`, `settings_cooloff` | Ask Daoris's chosen settings (HELPMODE1c3): notify on or off, a repository's or a workspace's session `language` (`en`, `zh` or `clear`, exactly one of `repository` and `workspace`), a session's `minutes`, the sessions at once, the cool-off's `minutes`. Each a keyed `set` request through the same exchange as housekeeping's (`HelpRequestExchange`), which the driver's loop judges (the person's grant now and at this start, the conversation's workspace, the setting's own checks) and applies with no Apply; a session that is not an Ask Daoris conversation is refused here |
| `checkout_read` | Ask Daoris's reading of the checkouts (HELPMODE1c4), for every agent: one checkout of the conversation's workspace by its `repository` and a `path` relative to its checkout, a file's text (at most 200 KB, said when cut) or a folder's entries, or `what` is `status` or `branches` for `git status` and `git branch --list`, which name no path. A keyed `read` request through the same exchange as the settings' (`CheckoutBox`), which the driver's loop judges (the checkout readable under D107, not turned off now and handed at this start; the path walked inside the checkout, out of git's own folder and of what git ignores) and answers with its words and the `text` read, cut here again past 200 KB; a session that is not an Ask Daoris conversation is refused here |
| `channel_post` | A session on an ask says its `text` (at most 4000 characters) on the ask's one channel, to everyone or `to` one of the ask's repositories, optionally `re` a message and as a `question`; at most ten a part. A message asks nothing and starts nothing; work stays a quest. Refused, in a sentence, for a session on no ask, a helper or a reviewer (SESSONE2a). `urgent` asks a working session's next step; at most three of a part's posts are kept urgent, judged in the insert's own statement, a fourth kept ordinary and said so; an ordinary post is kept `urgent: false`, never absent (COWORK1d, D188) |
| `channel_read` | A session on an ask reads the channel since its entry last read (or `since`, an ISO 8601 moment): messages and Daoris's own lines about the ask's quests, oldest first; the entry's read mark moves (SESSONE2a). Read from its last read, it also returns each message for its entry that the entry has not had, whatever its moment, acknowledges `read` each one for its entry it returned, marks an urgent message so, and answers the poster what each of its own messages reached per repository addressed (COWORK1d) |
| *every other tool's answer* | Ends with what waits for the session on its ask's channel, how many and how many urgent, once per new message (`WaitingFilter`, COWORK1d, D188's note): the pull an agent meets at its next call, whatever its door; `channel_read`'s answer never does |
| `brainstorm_give` | A brainstorm's generator turn gives its `proposals` (each `idea`, `rationale`, `effort`, `risk`, `reversible`, `undo`, `confidence`, `assumptions`, `sources`), its `needs`, or `none` with why: written whole into the one answer file the driver names on the turn's connector, and judged by the driver's rules against the packet's sources in its room. A refusal says its codes back, and the file holds the last answer given until a valid one is kept, once. Refused on every connector that names no generator turn (BRAINSTORM1a2) |
| `brainstorm_synthesize` | A brainstorm's synthesis turn gives its `groups` (each `title`, `idea`, `members` by anonymous id, `rank`, `why`, `flags`) and what it left out (`leftOut`, each `id` and `why`), judged against the ids its room was handed, as `brainstorm_give` is (BRAINSTORM1a2) |

**A tool the connector gains is named in the same change** wherever a session's rules list the connector's tools: the host's
list (`KnowledgeServer.ToolTypes`, held by `McpToolRegistrationTests`), the driver's `BrainstormPermissions.ConnectorTools` and its
two `golden/brainstorm-rules` files (a pass is denied every tool but its own), and, as its contract decides, the default allow list
(`Permissions.cs` with the CLI's twin `permissions.ts`, each held by the other's test) and a reviewer's and a helper's
(`OpinionPermissions`). `McpToolDescriptionTests` refuses a name that says `history`, `clear` (bar Ask Daoris's `clear_propose`)
or `forget`, since no tool clears a record, and one that says `evidence` or `verdict`.

**A quest carries links and files** (D65 §2). A link is an absolute http or https address, refused
otherwise, because a drawer shows it as a link. A file's **bytes stay on the machine that published
it**, kept under that machine's home at `quests/<id>/attachments/<first 12 of the sha256>-<name>`.
They are kept only after the record exists, so a refused ask leaves nothing on disk. The record names
each file by name, hash and size, which is all a remote ever learns: a shared deployment refuses
content outright, and the relay's signature has no field for it. A quest carries at most 10 files and
20 MB in total. A local host tells a caller on this machine where each kept file lies (the
transcript's rule), and the driver hands a session that directory as `DAORIS_QUEST_ATTACHMENTS`. With
no Daoris home, files are refused, and links still travel.

**A quest can name what comes next** (D65 §4). `then` is an ordered list of steps (`to`, `title`,
`body`, at most 5). Closing the quest `done` publishes the first step **in the same transaction**,
asked on behalf of the same asker, carrying the rest and naming its `parent`. `{parent}` in a step's
words becomes that id. A decline stops the chain. A step's id derives from its parent too, so a step
never collides with an older quest of the same words. The whole chain is judged when it is composed:
every step must be addressable from the asker, and every step must live in the same home as the
first, because each is published wherever the one before it closes.

**A quest's requirements quote the person** (DRIFT1c, D133 §3). `requirements` is a list of at most
20, each a `quote` and a `check` of at most 2,000 characters. The quote must stand in one of the
person's words on the ask that asks the quest: its sentence, an answer to a session, or a message added
to one. It is matched verbatim, whitespace and case aside. A quote found in none of them is refused,
and the refusal names each such quote. So is any requirement on a quest no ask asks, and on a host
that keeps no asks. A step of the chain inherits its parent's requirements. They travel with the
quest in the log and the sync, written only when there are some, and a remote checks only that each
has both halves, since it holds no asks.

**A requirement may name its evidence** (EVID1a, D144; `docs/2026-10-03-evidence-design.md` §2–§3,
§6). `evidence` is a list of at most 5 items, each exactly one of a `path` the done's commit must hold
or a `gate`. A path is judged at every door (`QuestEvidence.JudgePath`): repository-relative with
forward slashes, inside the tree and outside `.git`, at most 300 characters. So no machine's path is
ever kept, and a pushed quest or verdict naming one is not whole on the wire. A gate is refused,
naming the landing queue it waits for (EVID1d). A step to the same repository inherits each
requirement whole; a step to another inherits it without its evidence, and the step's publish in its
history says so.

A met answer on such a requirement closes the quest done and held, because the service cannot read a
commit and a step publishes in the done's own transaction. `Quest.Hold` names the cause: `departed`
first, then `evidence-unread`, then `evidence-missing`. The driver posts what it read to the evidence
door, which no connector tool reaches, and the exchange takes the verdict only on a done that waits
on its evidence (`AwaitsEvidence`) and only when it reads exactly what that done waits on. Its shape
has one judge, which the door and the wire both call (`QuestEvidenceVerdict.JudgeShape`, REFAC3): a
full commit id, a known way of reading, and per item a requirement numbered from 1, one path or gate
judged as a requirement's evidence is, a result its kind may have, a full object id, and `spelled`
only on a `case` read, naming the same path in another case. What it reads is judged apart, against
the quest, by the exchange and the replay alike (`Covers`). It is kept
as an `Evidenced` operation that travels like every verb. All found lifts the hold and publishes the
held step in the same transaction; anything else keeps it held until a later check finds it or the
person's yes (`accept`) takes the done as it stands.

**A chain's work may be reviewed where it runs** (REVIEWENV1b, D154;
`docs/2026-10-08-review-environment-design.md` §1.4–§1.5, §2.1, §2.6, §3.5). A chain carries a review
choice, `review` (`off`, `on` or an environment's name, with the person's `words`), inherited by each
step: the person's own at their door, an agent's only on the person's words, checked against the ask's
as a requirement's quote is (`quest_publish`'s `review`). Every connector is an agent's, whether or not
it names a session, and an agent's `off`, quoted or not, is kept as a proposal, never as the choice
(REVIEWENV1b3). An ask keeps the person's choices, the latest
standing, and its intake's proposals with their reasons (`reviewProposal`), which only the person's press
applies. The same ask keeps an intake's **kind proposal** (`quest_publish`'s `workflowProposal`, WORKFLOW1i-a): a kind's
id and a reason that is the person's own words, at most 300 characters, quoted as a review's words are (a reason they
never said is refused `NotQuoted`, a bad kind `BadWorkflow`). It is answered as `workflowProposals` beside
`workflowChoices` and applied only by the person's press at `POST /api/asks/{id}/workflow`; an agent never sets a kind
in this version. A quest published with **no ask** keeps the person's own kind and workflow (WORKFLOW1e2), set at the
local host's `POST /api/quests/{id}/workflow` while it is open, in a `chosen` operation that travels as every operation
does, and answered as the quest's `workflowChoices`; a quest an ask published is refused there, its ask's choice being
its work's. A **set-up step** is a chain step whose `setUpIn` names its environment: the exchange judges it
when the chain is composed (a name that never reads as production, the repository of the step before it
and none of that repository's steps after it, and no set-up step on a chain whose choice is `off`), and
the person's *Set it up* door publishes one following that repository's last done work in the whole
`follows` chain, one per repository per chain. Its done is refused until a set-up
was said, and closes held, `unreviewed`, after a departure and evidence. A set-up is a `SetUp`
operation (`look`, `shows`, `again`, `served`, `run`, the `commit` the driver read, the session or the
person, and `id`, its identity, made from all of that where it was said), and a local one crosses the
wire without its address, since its tab is on the machine that showed it; its `id` crosses whole, so
every machine dedupes it alike. The person's verdict is a `Verdict` operation: `reviewed` or `not-yet`
on the set-up they name, which must be the newest, or `skipped`,
with their words, kept on the ask as words of those kinds. Only a `reviewed` or a skip
lifts the review's hold; a yes accepts a departure or its evidence and never a review. What reads the
record, the gate at every landing door and the step's driving, is the driver's (REVIEWENV1c).

**A second opinion is kept on the machine whose work it read** (XAGENT1c, D155;
`docs/2026-10-08-second-agent-design.md` §5.4, §6.1–§6.4). One pass is one row of `opinions`, beside the
session records and never a quest operation: its occasion, the candidate (base, tip and the commits
between, by full id), the reviewer the driver's walk chose (adapter, label, product, maker, account),
the families that wrote the work, the posture that held, the rule's `minutes`, and its tier. Asking one
opens its reviewer's record with it: a chat that serves no quest and names it as `opinion`, in a clone
of its own, never the working session's tree. Where a pass stands is read from that record, never kept:
`reading`, `given`, or `failed` (`ended`, `out-of-time`). The rule starts one first pass per working
session; the person's asks are not capped; a recheck reads, once, the commits since a first pass whose
findings went. A first pass's findings go to its working session as one word in `said` whose `by` names
the opinion, in Daoris's fixed words, reopening the record: never in `answer` and never on the ask, which
every later session reads as the person's. A recheck's findings go to the person. The say door refuses a
reviewer's record (`opinion`), and a shared host keeps none: its desk refuses with one sentence
(`OpinionDesk`).

**A quest carries a short title** (SESSUX1j, D126 §9): `short`, the few words that tell it apart in
a list, given by whoever publishes it (`shortTitle` on `quest_publish`, `short` on the HTTP doors and
an intake's draft). It is at most 40 characters on one line, and the exchange refuses a longer one or
one with a line break; a remote refuses a pushed one the same way. It travels in the publish
operation and the sync only when given. Every quest answers `short` with what a list calls it: the
publisher's, else a name read from its own words (`QuestTitles.Derive`): whole words, an ellipsis
where words were left off, a bracketed note line skipped, and a ticket key the ask names leading it.
Where only one word would be kept, the next is cut by its characters, at its last `/`, `-` or `_`
that fits (SHORTFIT1), so a verb and a long branch name read as more than the verb. A derived name is
never written into the record, and `quest_publish`'s answer to a publish with no short title says
it; to an intake's publish with no requirements, it says nothing of the person's words will be
checked.

**A quest names the session that published it** (SESS1) as `publishedBy`, when a session's
connector did. The driver names every session on the connector it hands over and on its spawn
(`DAORIS_SESSION_ID`), and `quest_publish` records it, an intake's publish for its ask included. A
person's publish and a chain step name none. It travels with the quest in the log and the sync, so a
session's view can say what it asked of other repositories without guessing from the times.

**An ask is a sentence entered at a workspace, not at a repository** (D65 §1a). It keeps its words,
links and files (under `<home>/asks/<id>/`), who asked, and what became of it, and every record names
the tier that answered. With no intake harness, the **declarations tier** ranks the workspace's
adopted repositories by the words their summary, `owns` and `accepts` share with the sentence. A
repository of the circle that can be asked, adopted or not, whose exact name the sentence holds whole
comes before every overlap, in the sentence's order, with its name as the evidence (ASKNAME1):
`portal` is not named by `portal-ui`. It proposes, with the matched words as evidence, and
**publishes nothing**; a person turns a proposal into a quest. Naming the receiver publishes at once. An ask's quests are asked *by the ask*
(`ask #<id>`), in the ask's own circle. The same words in the same circle are the same ask. Asks
are machine-local: a local host's door, and `daoris-driver ask` from a terminal. An ask is **done**
(USE1c) once it became a quest and every quest asked by it, chain steps included, has closed. That is
worked out from the quests on every read and never stored, so a quest closed on another machine
counts when it syncs in. The default list hides a done ask as it hides a closed one. An ask also keeps
**the person's words after it** (DRIFT1a, D133 §1): each answer to a session on its work, each message
added to one, and each word said to one after it ended once a session took it (`reopened`, MSG1a),
verbatim, with when, the session and its quest, after its own sentence (`words`). They are
not back-filled, so an ask from before says from when they are kept (`wordsKeptFrom`).

The judgement behind those — who may be addressed, what a refusal says, what declining requires —
lives in one place, `QuestExchange`, shared by the MCP and HTTP hosts. Written per host it would
drift, and the same ask would be deliverable through one door and refused at the other, which for a
quest system is the worst available bug: it looks like the sibling ignoring you.

**A quest or an ask made by mistake can be deleted** (D95), from a person's doors only: a local host's
`DELETE /api/quests/{id}` and `DELETE /api/asks/{id}`, which the page and `daoris-driver quest delete`
/ `ask --delete` call. There is no MCP tool for it. Only a quest nobody has started on goes: it must
be open, no session record may name it, and no taken quest may wait on it. Anything else is refused
naming what to do instead, which is to decline it or leave it closed. An ask goes with every quest
asked by it, or not at all. A quest that may have left the machine is **tombstoned**, a `deleted`
operation the sync carries so no fetch brings it back, and on a shared quest the delete is pushed
before the answer returns. A quest that never left simply goes. The `deletable` field on the quests and
asks a local host lists (and on a quest a publish, a response or a dismissal answers with) is the same
judgement, so a page offers the verb only where the door would take it.

**A number the store has issued is never issued again** (HIST1a, D153 point 4). An operation's sequence
is one past the larger of the log's highest for this machine and `quest_machine.sequence`, and a session
record's revision one past the larger of the table's newest and the one-row `session_revision`. Triggers
move both marks in the statement that writes the number. Without them, removing the newest rows (a
delete, and the history clearing D153 designs) reissued a number a remote already held: the remote
answered the push as a retry, a fetch brought its own operation back under that number, and the new
move was lost with no error. A store from before the marks seeds them from what it holds, so numbers
may now have gaps, and nothing reads them as contiguous.

**Finished history is cleared from this machine, listed first and then pressed** (HIST1b, D153).
`HistoryDesk` judges the records' half of a unit for both of a local host's history doors. A unit is a
closed quest's work: the quest, each question its sessions published, applied again to what each adds,
and every session record that served them, a teammate's copies included. Or an ask's work: the ask, its
intake and every quest it asked, whole or not at all. Or a closed quest's failed sessions of this
machine's. A unit stays whole, its refusal naming the piece: `open` (work in progress, or an open quest's
failed sessions, its strikes; how its quest stands named beside the word by `stands`: `open` or `taken`,
HIST1m), `asked` (a quest an ask here asked, named alone), `live` (a session still running, or a
teammate's still reading as running), `needs-you` (a parked session, a done held for the person's yes, an
undismissed conflict, an ask proposed or open, an unsettled rule proposal one of its sessions made or one
for the ask, each named beside the word by `waits`: `parked`, `held`, `conflict`, `ask`, `proposal`,
HIST1l), `awaited` (an open question its session published, a taken quest waiting on its answer, a
chain's open next step, each named beside the word by `by`: `question`, `asker`, `step`, HIST1m),
`unpushed` (on a wired workspace, a move or a record of it the remote has not taken), `unknown`; a
teammate's failed session is listed and kept as `not-ours`. Every word the doors spell is held, with the
driver's constants, to one table, `Daoris.Service.Tests/fixtures/history-words.json` (HIST1m). Each unit
is judged again inside the one transaction that clears it. A record that never left the machine simply
goes, its row and its log together. **A quest a remote numbered is forgotten**: it goes the same way and `quest_forgotten` keeps its
id, so the quest fetch and the session fetch pass over it while their cursors move past; no later move and
no cursor at zero brings it back, and a new store, being a new machine, fetches it whole. Nothing is
pushed and nothing travels: no tombstone and no operation. The same words published again are refused,
409 at the HTTP door, in a closed quest's words. After the records, the service removes the quest's and
the ask's kept files and each settled rule proposal naming a cleared session or the ask; a session's own
files under the home are the driver's to remove. A kept file the disk will not let go of stays, and the
press names its quest or ask in `failed` (HIST1j), so the driver counts it as failed rather than freed; a
rule proposal that stays is not named, since no clear takes one as left over. No MCP tool clears anything.

**A decline may apply only while the quest is open** (PAUSE1c, D132 point 10): `whileOpen: true` on
`POST /api/quests/{id}/respond`, which an abandon sends, and no MCP tool does. It is refused, 409, on a
quest taken here. It travels with the flag, and one that reaches the remote after another machine's
take becomes a conflict on the quest, as a losing take does, so the take stands. A decline without it
is the plain one, which lands over a take as it always has.

Four states, because anything finer is status for its own sake. A quest is **taken**, not assigned,
which is the property that keeps declining a real answer. **An adopted repository, or one registered
here with a root, can be addressed** (D70): registered is addressable, adopted is disciplined. Anything
else is refused naming who can be, because an unread quest looks exactly like an ignored one.

Stored beside the index in the same database: quests are service state as the index is, and two files
would be two things to back up and two that can disagree about which repositories exist.

**A new kind of quest operation goes in every place `.claude/knowledge/quest-operations.md` names**
(QUESTOP1), from the enum to the MCP listing, and `QuestOperationKindsTests` fails for a kind that misses one.

## Proposing a change to what agents may do

A session that finds the rules it was handed wrong says so: `permission_propose` (PERM2, D74) proposes
adding a rule to `allow`, `ask` or `deny`, removing one, or switching one of Daoris's defaults, in one
of D72's scopes (the machine, a `workspace`, a `repository`), with the reason. **It proposes and never
applies.** Whether a change narrows depends on the rules as they stand, which only the driver reads:
it applies a narrowing at its next tick and holds a widening, which 🔴 **never applies without the
person** (`daoris agent rules accept|decline`, or the Settings card).

**A proposal is a file, not a row here.** One JSON file each under `<home>/proposals/`, where the
home is the one the driver names on the connector it hands a session (`DAORIS_RULES_HOME`), or else
`DAORIS_HOME`. The rules are machine-local, so a proposal to change them is too: it is never fed to a
remote and never served over HTTP. The file records the session that proposed it
(`DAORIS_SESSION_ID`, set by the driver on every connector it hands over: each protocol-door session,
and an intake on either door), the ask when that session is an intake, and the folder it ran in, which
the screen is never shown. A pipe-door quest session talks through its repository's own connector,
which names no session, so its proposal lands under `DAORIS_HOME` as one from *a session the driver
did not start*. A malformed rule, a scope with no name or an empty reason is refused with nothing
written. With no home at all, every proposal is refused.

**Ask Daoris proposes the same way, and never applies** (HELP1c, D89). Its conversation's connector
offers `setting_propose` (one of the `daoris driver` doors: every verb but `list`, since HELP9 and HELP10
`across`, `cap`, `adapter` and `retry` among them, and since DOORS1a `helpers` and `commits`, shape-checked by the flags
their verbs take, and a landing's `--key` and `--squash`; the tool says a repository's own instructions only seed a
proposal the person applies) and `ask_propose` (something to start, as
an ask at a workspace; since ENTRY1c with the review choice the person said and their words, as the
composer sends them, and never a kind or a workflow, which are the person's), and since HELP6 `agent_propose` (an agent's update, or a pin to one exact
version; since HELP10 also the account it runs as by default; since TOOL4l-a also an account's `use`, the walk's `order` and a cool-off's `ready`; since SWITCH1d the `use` door's `switch` and the person's `curve`, a window each as the terminal's `--curve` spells it, or `no_curve`, in place of `early` and `near`), `delete_propose` (a quest or an ask made by mistake), `agent_settings_propose` (an account's
own model and effort) and `go_propose` (a place to open, changing nothing: a view, a Settings domain, or a part
such as Knowledge's mode or a workspace's tab, as D150's UX6i2a and UX6g2b notes list them; since D161 the front door's
way to take a person to the place, and since ENTRY1f1 one quest or ask on Quests as its `item`, which the driver judges
against the machine's records, and since ENTRY1f2 one session on Sessions, judged against the machine's own; since ENTRY1d2a
Repositories' Add repository or Import a folder may carry the `workspace` the drawer opens with, a name and never a folder; since NEW1d-a Sessions' New conversation, the part `new`,
may carry the `repository` and `sentence` the composer opens with, written to the common fields, a repository never a folder), and since PLUG9
`plugin_propose` (a plugin that has landed, added from its folder in a repository's checkout, or one
installed here switched on or off; since D103 also one of the install's own plugins by its id in
`offer`, or an `update` of an installed one from where it came from; since HELPPLUG1c an `approve` of what a
plugin's server offers Ask Daoris, with its `server` and the `writes` the person accepts, or a `withdraw`), and since WSR5b `hand_propose` (a
branch a landing made, handed to a landing plugin that pushes it and opens the pull request), and since
HELP10 `browser_propose` (Daoris's browser's settings and favorites, as Settings → Browser sets them) and
`sync_propose` (bringing repositories up to date after a pull request merged; the driver's card looks first, which
fetches, and only then offers the press; since BRSCOPE1b an optional one-word `workspace` scopes the look and the press), and since DRIFT1d2 `accept_propose` (the person's yes to a quest a done's
departure holds, which the card shows with the words each departure relied on), and since ENTRY1d1
`repository_propose` (a registered repository moved to a workspace by name, as its Manage drawer moves it; never a
path, since a move needs none), and since SESSUX1h2a `session_propose` (sessions archived or unarchived by id, or every
ended one archived; and `delete_propose` takes a `session`, one bare id), and since PAUSE1f-a `pause_propose` (one ask's or quest's work paused or resumed, by id; abandon is
never proposed), and since HIST1f-a `clear_propose` (one quest's, ask's or workspace's finished history cleared; the card lists what is cleared and what is kept), and since WORKFLOW1h-a `workflow_propose` (a whole version of a workflow for a repository or workspace, new or on a base, and optionally its choice; its `steps` are written as nested JSON, and `changes`, `kind` and `import` are refused as not proposed yet; since WORKFLOW1h-name a new workflow carries an optional `name`, the person's words, at most 60 characters, and an edit's is refused because an edit keeps its name; since FABRIC-PIPELINE1i a verification pipeline's `graph` in place of `steps`, never both, and a `route` — `default`, `landed` or `kind:<id>` — which with neither chooses a saved workflow alone), and since LAYOUT8 `setup_propose` (a registered repository set up for every agent by name, as `daoris-driver setup <repository>` and its page's Set up for agents press it: one quest to its own session and the rule its session needs; never a path, and its `door` is `setup`, beside which WSSETUP7's `register` and workspace doors go). Each writes
one file under
`<home>/help/proposals/`, the same home as the rules proposals, checked here for its shape only:
whether the route would take it is the driver's, which judges it with the route's own rules before
the person sees a card, and settles it when they press Apply or Not now. Every connector carries the
tools, but only the helper's room allows them, and a proposal is shown only in the conversation that
made it.

**The semantic pass has been proven on a real pair.** Two repositories derived the same principle
independently and wrote it in different vocabulary; word overlap scores them at **25%**, below the
duplicate threshold, so the CLI's `doctor` structurally cannot see them. Indexed here with a local
embedding endpoint, convergence detection reports exactly that pair at **0.785** — and discriminates,
returning nothing at a 0.82 threshold and pulling in an unrelated document at 0.60. That is the whole
argument for this artefact existing, measured rather than asserted (`docs/DECISIONS.md` D17, D24).

Indexing the whole family takes **~500 ms for 408 entries** into a 7 MB database; queries answer in
**3–10 ms**.

## A call's lineage

**What a call's harness says of it is written down, and nothing is decided by it yet** (FABRIC-C1a, D173). A native
subagent calls through the connector its root was handed, with the root's `DAORIS_SESSION_ID`. So the connector reads
each `tools/call`'s `_meta` before the tool runs and appends what the harness wrote there to the session's lineage file,
`<home>/sessions/<session>/lineage.jsonl`, the home as for a proposal: Claude Code's id of the call, or Codex's thread,
its parent and the spawn kind. It keeps ids only. Codex's workspace path beside them is never read, a value not shaped as
an id is dropped, and the arguments, which the model writes, are not read for it. A connector that names no session
writes nothing. The driver binds each child to the session's part from these lines and its own (`ChildBindings`).

## Running it

**Local — the default, and no daemon.** The MCP host is spawned by each agent session and exits with
it; the **database** is what persists. Every session in every repository on this machine spawns over
the same file (`knowledge.db` under the Daoris home — `DAORIS_HOME`, which the installed desktop sets
for the account, D63), which is how a quest published from one repository's session is waiting when
another repository's session starts. A host the install carries (`app/daoris-knowledge/`,
`app/daoris-knowledge-http/`), started with no `DAORIS_HOME` — a person's own agent's `.mcp.json`, a terminal — takes the
install's own `data/` as its home, as the driver host does (SERVICEHOME2, D171 point 1). Elsewhere, with no home and no
`DAORIS_KNOWLEDGE_DB`, the host says so on stderr and exits 2 rather than opening a database under the user profile.
Hosts starting together wait for each other: each write waits while another host holds the file, up to the SQLite driver's
command timeout (30 s). A write that reads back what it wrote (`RETURNING`) runs in a write transaction of its own, since the
driver does not wait at such a write's commit when it runs alone (QUESTOPENLOCK1, D173's note).

**An older build never touches a newer index, and works with the rest of the store** (KSCHEMA1). The index's schema has
a version (`user_version`): an older one is rebuilt when it opens, as before. A newer one, which only a newer Daoris
writes, is left as it is: nothing is dropped, created or stamped, `Rebuilt` stays false and the fed commits are kept.
The store opens, and its quests, sessions, asks, keys and registry work; every operation on the index throws
`NewerIndexException`, whose sentence names both versions and no path. The HTTP host answers it on every route over the
index as 409 in the error shape, and the connector's index tools answer it as their text. The registry answers with no
entry counts, and no refresh is tried. Each host says it once at start, on stderr with the folder to update and in its
log as `index.refused`. The read and the stamp are one write transaction, so an older build opening beside a newer
one's stamp waits for it and leaves it. D36's KSCHEMA1 note says why the index alone is refused, and what an older build
can still do to the stores that add columns.

**This repository's own `.mcp.json` starts a built host, never a build** (ORIENT1c): `node
tools/knowledge-server.mjs`. It ran `dotnet run` before, which built at every session's start, once per
worktree, failed when two built at once, and refused to start in a terminal with no home. Now
`npm run knowledge:build` publishes the host into `local/knowledge-server/builds/` when its sources changed,
and the merge tool runs it once a merge's gates pass. A session started by hand, or a subagent's in a worktree,
gets that build over a home of its own under `local/knowledge-server/home/` (never the install's, whose store a
workspace build must not open), serving the main checkout alone (`DAORIS_KNOWLEDGE_REPOSITORY`), its `docs/`
a section each and its `docs/index/` a row each (`DAORIS_KNOWLEDGE_DOCUMENTS`, `DAORIS_KNOWLEDGE_INDEX`), by
words only, and re-read at the next search once the checkout moved (a watch of its git refs and knowledge folders marks it stale,
FABRIC-EVENT6) or its reading is five minutes old, a minute where nothing could be watched. Its instructions say so before the first search.
A folder of documents or records is walked without entering what git ignores, the watch is opened around it (KNOWWALK1), and
the host itself watches no file in the folder it starts in. A knowledge folder made after the server started, and a declared room's
`AGENTS.md`, are watched too: the folder from when it is made, the room through the layout's room list (FABRIC-EVENT6b).
A running session moves onto the build a merge made, its handshake replayed, within a minute (KNOWHOLD1).
A session a driver started keeps the environment it was handed and runs the machine's host first. With no
build the launcher answers the protocol itself: the handshake says why and lists no tool, and the session
works as before. Measured on the first build: the first search of a process, which reads the checkout, 1.2
to 1.7 s for 7,724 entries; each search after it, about 30 ms. A sibling adds an entry naming a published host
to its own `.mcp.json`, which is that sibling's file to write, as `--install` prints below.

The production shape is the **published executable** (D43):

```sh
npm run publish:service -- --install    # both hosts → $DAORIS_HOME/bin, self-contained single-file
```

`--install` lands them under the Daoris home — it refuses with no `DAORIS_HOME` set, since there is no
default under the profile — and prints the ready `.mcp.json` snippet with the home and the family root
already filled in: a published binary has no workspace above it to walk to, so the root must be
**named**; run without it, the host
says so on stderr rather than silently indexing whatever directory spawned it. From a source checkout
the walk-up still lands on the right folder wherever the client spawned it. The release workflow ships
the same binaries per platform, each with a sha256 beside it (D27's shape): `daoris-knowledge-<rid>`
bare, and `daoris-knowledge-http-<rid>.tar.gz` carrying its web bundle beside the executable.

| Tool | Answers |
|---|---|
| `knowledge_search` | What has this family already learned about X, and where is it? Each hit names `path:first-last` and the line its excerpt starts on; `kinds` takes `index`, and with no kinds an answer holds at most two index entries and says when more matched (ORIENT2e) |
| `knowledge_get` | The full text of one entry, or with `lines` (`12-30`, or `12`) only the lines a hit names: the part of the range inside the entry, read from what was indexed; a range outside it is refused naming the entry's lines, and an entry that names none is answered whole, saying so (ORIENT2e) |
| `knowledge_repositories` | What is searchable, and how much each repository contributes |
| `knowledge_convergence` | Which repositories learned the same lesson independently? |
| `knowledge_refresh` | Re-read every repository from disk — and retire what is no longer there: a repository renamed or removed leaves the index instead of being served forever (on a host that reads its registered roots, the registry decides — retiring the last repository empties the index, POLISH5; a fed host never refreshes) |

**Remote — transfer of request and task, opt-in.** The HTTP host is the deployable half. It runs with
**no model at all** (D24) and still carries what a remote deployment exists to carry: registrations
pushed by `daoris connect` — persisted, because for a remote service the pushed registrations *are*
the family — and quests, published and answered over the same `QuestExchange` the MCP host uses. A
repository's knowledge travels only if that repository opts in (D21); moving work never required
moving knowledge.

```sh
dotnet run --project src/Daoris.Service/Daoris.Service.Http     # http://localhost:5177
```

| Endpoint | |
|---|---|
| `GET /api/status` · `/api/search` · `/api/entry` · `/api/entries` · `/api/convergence` · `/api/repositories` | the read surface, same as the UI's; a local host's status also answers `home`, its home's id (`HomeIdentity`, HOMEID1), which a terminal checks before it takes the address |
| `GET /api/registry` · `POST /api/registry` | who is out there; where `daoris connect` lands, with the words of the lanes it declares (`lanes`: id, title, summary, steward; absent keeps the row's, D115). A shared deployment orders a checkout's declaration by the commit it names, as it orders knowledge: a declaration from an older commit, another line, or no commit where one is held is not taken, as information (SYNC5b) |
| `DELETE /api/registry/{repository}` | take a repository off the map; nothing on disk is touched. At a shared deployment it is how a machine's retire reaches the circle (SYNC5b) |
| `GET /api/registry/retired?workspace=` · `DELETE /api/registry/retired/{repository}?workspace=` | local mode only: the retires this machine's checkouts owe a circle, written by the store as a joined checkout's row leaves it (retired, re-wired, or re-registered unjoined), and cleared once the driver's pass has carried them (SYNC5b) |
| `GET /api/code-map/{repository}` | a repository's own code map (MAP3a), read from its committed `docs/code-map.json` and judged whole; a repository with a checkout here is read from it, and one without answers with what was fed at a shared deployment (MAP3b) or brought down by a machine's sync (MAP3e), with `fed` naming the commit, its line and the key that fed it, or with no file |
| `GET /api/quests` · `POST /api/quests` · `POST /api/quests/{id}/respond` | the pull side; publish; take / done / decline. A publish's `to` may name lanes (`repository:lane+lane`); a quest answers `to` as the repository and `lanes` beside it (D115). A publish's files arrive whole (base64) at a local host and by name only at a shared one; each door refuses the other shape. A publish may name `requirements` (`quote`, `check`), and every quest answers them, `[]` for none (DRIFT1c). A `done` answers each (`answers`: `requirement`, then `met`, or `departed` with `quote`), and every quest answers its `answers`, `held` and `accepted`; a held done stays on the outstanding list (DRIFT1d). A publish may name `short`, and every quest answers `short`, the publisher's or the name read from its words (SESSUX1j). A requirement may name `evidence` (`path` or `gate`), answered `[]` for none; every quest answers `hold` (`departed`, `evidence-unread`, `evidence-missing`, `unreviewed`, absent when not held), `awaitsEvidence` and `evidence`, the verdict last read (EVID1a). A publish may name the chain's `review` (`choice`, `words`) and a step's `setUpIn`; a quest answers `review`, `setUpIn`, `setUps` and `verdicts`, each absent for none (REVIEWENV1b). Read from its log, a quest answers a conflict's `lostToTheReopen` and `forward` (a bring-forward's take: `machine`, `sequence`, `at`, `standing` `held`, `unconfirmed` or `lost`), the conflicts `settled`, each with how it `ended` (`dropped`, `brought-forward`), the `holder` whose take holds the step, and, to a caller on a local host's machine, `here`, that machine's id; each absent for none (SYNCMERGE1b0) |
| `POST /api/quests/{id}/accept` | local mode only: the person's yes to a done held for them, a departure or its evidence unread or missing, which accepts it as it stands. The chain's next step it held is published, and a quest waiting on it resumes; 409 for a quest nothing holds (DRIFT1d, EVID1a) |
| `POST /api/quests/{id}/reopen` · `POST /api/quests/{id}/continue` | local mode only, the person's: `reopen` names the quest's current sealed submission (`submission`: `machine`, `sequence`; `note`, their words), which leaves it taken with none current, so a close naming it is stale; the step it published is withdrawn if untaken, and held (`hold` `source-reopened`) if taken. `continue` lets a held step go on, growing from its source's current unheld submission, with any submission of its own cleared with its answers, yes and verdict, so it submits again (FABRIC-CLOSE1c10), and the next step that submission published reopened with it, withdrawn or held, as a reopen of it would (FABRIC-CLOSE1c14), and is refused `not-sealed`, naming it, where that next step already closed on the submission the go-on would clear, writing nothing (FABRIC-CLOSE1c19); an untaken one is rebound by that submission's publication itself, judged while open, so it never lets go a step another machine took first (FABRIC-CLOSE1c15). A step answers `grewFrom` and `sourceReopened`, absent for none. 409 for a state that refuses it, 404 for no quest (FABRIC-CLOSE1c) |
| `POST /api/quests/{id}/done` | local mode only: the person marks an open or taken quest done (`note`, their words, optional). It answers none of their requirements, and its note says it was theirs: *The person marked this done*, then their words. The chain's next step is published and a quest waiting on it resumes, as any done's; 409 for a closed quest, 404 for no quest (QUESTCLOSE1) |
| `POST /api/quests/{id}/bind` · `POST /api/quests/{id}/close` · `POST /api/quests/{id}/close/outcome` | local mode only, the driver's (FABRIC-CLOSE1a, D173): `bind` names the graph that closes a taken quest (`graph`, its digest), once, before its first submission (refused, as at the take, for a quest whose circle has a remote, FABRIC-CLOSE1v), or with `atTake: true` an open quest's, for the `session` it names, which that session's take on this machine binds in the take's own write and answers saying `ready` (FABRIC-CLOSE1l; refused for a quest no longer open, one whose circle has a remote, or with no `session`). FABRIC-CLOSE1o: the take consumes the naming and binds only for that session, on the publication it was named on, within an hour (judged at the moment the take's write begins, however long it queued for it, FABRIC-CLOSE1w) and with no remote then; a naming by a build before FABRIC-CLOSE1w, kept where such builds keep theirs, owns nothing and never binds, and such a build's take never reads this build's; an expired, replaced or remote-since naming leaves the take unbound and its answer says to finish with `done`; the quest then answers `closure`, its agent's `done` is refused (400) naming `ready`, and its `respond` `ready` (a done's `reason` and `answers`) keeps it Taken with its `submission` (`machine`, `sequence`, `at`, `note`), held as a done would be. `close` (`key`, `token`, `quests`: `id`, `machine`, `sequence`) writes every quest named Done in one transaction, each on its named submission. Each outcome is kept under its `key`, a refusal as much as a close: the same `key`, `token` and `quests` again answer that first outcome and write nothing, and anything else sent with that key is refused, 409 (FABRIC-CLOSE1e). 400 for a call that is not one, 404 for no quest, 409 for a check that refused (first the close's scope, FABRIC-CLOSE1v: a quest of another repository than the one the close is at, of no chain that one grew from, or bound to another graph; then closed, unbound, not this machine's, stale, held, its step not published), naming the quest; nothing is written. Each refusal names its check in `code` (`malformed`, `not-found`, `other-repository`, `not-in-chain`, `other-graph`, `closed`, `not-bound`, `not-held`, `stale`, `held`, `unsealed`, `key-reused`, `withdrawn`), a replayed one the check it was refused on then, and every host of this build answers `closure: true` on `GET /api/status`, the handshake a driver asks before it binds (FABRIC-CLOSE1b). `close/outcome`, with the same body, asks what became of a close the driver holds no answer to, and is answered in the close's own shape from what is kept under its `key`, judging and writing no Done: its first outcome where the key was taken, else 409 `withdrawn`, the key kept withdrawn in the same transaction so that close never runs and a send of it arriving after is refused alike (FABRIC-CLOSE1u). A host before it has no such door, and the driver's close stays unknown |
| `POST /api/quests/{id}/evidence` | local mode only: the driver's verdict on a done's evidence (`commit`, `how`, `session`, `items`: `requirement`, `path` or `gate`, `result`, `object`, `changed`, `spelled`; and on a submission, `submission`: `machine`, `sequence`, the one it was read for, FABRIC-CLOSE1e). Found lifts the hold and publishes the held step; missing keeps it held. 400 for a verdict that is not one in shape or does not read exactly what the done waits on, naming why; 409 for a quest whose done waits on no evidence, or a verdict read for another submission than the current one, or naming one on a done; 404 for no quest (EVID1a). A verdict read for the current submission is that submission's commit: the quest's `submission` answers it as `commit`, absent before, after a later `ready`, and from a host before it (FABRIC-CLOSE1c5). A verdict with no `items`, on a current submission that waits on no evidence and whose commit is not known, is that submission's commit read alone at the end of the session that said its `ready`: kept once, holding and publishing nothing (FABRIC-CLOSE1c6); 409 for a second, or with no submission standing, and 400 where evidence is asked |
| `POST /api/quests/{id}/set-up` | local mode only: a set-up on a set-up step, with the `commit` Daoris read and the environment's `kind` (`local` or `deployed`), and either the `session` whose said set-ups the driver posts, each once, or the person's own `look`, `shows` and `again`. 400 for one that is not one in shape, 409 for a quest that is no set-up step or a take this machine lost, 404 for no quest or no such session of this machine's on it (REVIEWENV1b) |
| `POST /api/quests/{id}/review` | local mode only: the person's verdict, `verdict` `reviewed`, `not-yet` (with `words`) or `skipped`. A `reviewed` and a `not-yet` name the set-up the person looked at, `setUp` (`machine` and `sequence`, as the quest's `setUps` answer them), and a skip names none (REVIEWENV1b3). A `reviewed` or a skip lifts the review's hold and publishes the held step; their words are kept on the ask as their own kind. 400 for a verdict that is not one, or a `reviewed` or `not-yet` naming no set-up or half of one; 409 for one the review's state refuses (nothing shown, a newer set-up than the one named, already reviewed or skipped), 404 for no quest. No connector tool reaches it (REVIEWENV1b) |
| `POST /api/quests/{id}/set-up-step` | local mode only: the person's *Set it up*, an `environment`; publishes a set-up step following that done quest, in Daoris's words, with its requirements and its chain's choice. 400 for a name that is not one or reads as production, 409 for work not done, a set-up step, a quest a later step of the same repository adds to, or a chain that already composes or holds a set-up step for that repository, read over the whole `follows` chain (REVIEWENV1b3), 404 for no quest (REVIEWENV1b) |
| `POST /api/quests/{id}/workflow` | local mode only, the person's door (D156; WORKFLOW1e2): the person's kind and workflow for a quest published with no ask, before it starts (`kind`, `workflow` or `current`, either, both, or neither to clear them; and their `words`), kept as a `chosen` operation; the latest stands, and the quest answers `workflowChoices`, absent for none. 400 for a shape that is not one or words past 2,000 characters, 404 for no such quest, 409 for a quest an ask published (its ask's choice is its work's), one taken (its run was bound at its start) or closed. No connector tool reaches it |
| `POST /api/quests/{id}/conflicts/dismiss` | a person dismisses a conflict: `{ machine, sequence }` names one, and naming none dismisses every one the quest carries. It is an operation the next pass carries, so every machine drops it. It moves no status (SYNC6c) |
| `POST /api/quests/{id}/conflicts/forward` | local mode only: the person brings their machine's lost take forward (D178), `{ machine, sequence }` naming its conflict whole. In one write, a fresh take of the step as it stands and the conflict's dismissal marked `forward` with that take, which applies only where the take stands; on a shared quest the take is pushed and awaited as a take is (D69), answered confirmed, UNCONFIRMED offline, or lost again, the conflict then unresolved. 400 for a conflict not named whole, 404 for no quest, 409 for a conflict not this machine's lost take or settled already, a step held, closed or withdrawn, or a loss at the remote. No connector tool reaches it (SYNCMERGE1a) |
| `GET /api/asks` · `POST /api/asks` · `POST /api/asks/{id}/publish` · `POST /api/asks/{id}/close` | local mode only: an ask made at a workspace (D65 §1a), the quest a person turns it into, and closing it with a reason. Every ask answers `words`, the person's own, oldest first (DRIFT1a), and `goAheads`, what its sessions asked the person for, one per act (KNOWUSE1a). A publish's `requirements` quote them, and a quote they never said is refused, 409 (DRIFT1c) |
| `GET /api/asks/{id}/channel` · `POST /api/asks/{id}/channel` · `POST /api/asks/{id}/channel/read` | local mode only (SESSONE2a, D167): the ask's channel. The read answers `items` in time order, each a `message` (its poster, `to`, `re`, `question`, `open`, `text`) or one of Daoris's `line`s (`code` `published`, `taken`, `done`, `declined` or `awaiting`, from the ask's quests, never stored), with `unread` and `mark` for the `entry` named (the person's `person` by default); it moves no mark, and `since` limits it. The post is the person's (`text`, `to` a repository or `everyone`, `re`), kept on the channel and on the ask as a `channel` word; at most 4000 characters, refused with a code (`empty`, `too-long`, `bad-to`, `bad-re`; 404 `not-found`). The read POST moves an entry's mark to `through`, else to the newest item. Deleting the ask forgets its channel. COWORK1d (D188): each message answers `urgent`, true or false, absent only on one from before; the post takes `urgent` (false keeps the person's message ordinary; their default is urgent); read for an entry, each message answers that entry's acknowledgment `ack` (`taking`, `sent`, `taken`, `read`, `partly`) and its part's `ackSession`, a claim whose part no longer runs read as none; read for the person, `receipts`, one per repository it addresses (`waiting`, `held`, `sent`, `taken`, `read`, `partly`, `answered`, or `not-reached` once the ask has no quest open and no part of that entry is here, judged as read and never stored) |
| `POST /api/asks/{id}/channel/ack` | local mode only, the driver's (COWORK1d, D188 point 6; the message-urgency design §6): an `entry`'s acknowledgment of `messages` by id, for its part's `session`. `taking` claims each, one part's only, in one statement (where the entry holds none, a `partly` another part was handed, or a claim whose part no longer runs); `released` lets a part's claim go; `sent`, `taken`, `read` and `partly` raise what the entry holds, never lowering it. Answers `acknowledged`, the ids it changed; the entry's mark then moves for display, to before the oldest message for it not yet sent, taken or read. An unknown state is 400 `bad-state`, an unknown ask 404. A channel from before the table counts each message at or before an entry's mark as taken, written once as the table is made |
| `POST /api/asks/{id}/go-aheads/{number}` | local mode only: the person's answer to a go-ahead, `answer` `approved` or `refused` with their `words` if any; a later answer replaces the earlier. 400 for any other answer or words past 2,000 characters, 404 for an ask or a go-ahead not held. No connector tool answers one: the production acts stay the person's (KNOWUSE1a) |
| `POST /api/asks/{id}/review` | local mode only: the person's review choice for the ask (`choice`: `off`, `on` or an environment's name, and their `words`), also how they apply its intake's proposal; the latest stands. `POST /api/asks` takes the composer's as `review` and `reviewWords`, and an ask answers `reviewChoices` and `reviewProposals`, each absent for none. 400 for a choice that is not one, 409 for a closed ask, 404 for no ask (REVIEWENV1b) |
| `POST /api/asks/{id}/workflow` | local mode only, the person's door (D156): the person's kind and workflow for the ask's work (`kind`, a kind's id; `workflow`, a workflow's id or `current`; either, both, or neither to clear them; and their `words`); the latest stands, and reaches each chain it publishes whose work has not started. `POST /api/asks` takes the composer's as `kind`, `workflow` and `workflowWords`, and an ask answers `workflowChoices`, absent for none. Only the shape is judged here: which kinds a workspace declares and which workflows read are the driving machine's. 400 for a shape that is not one, 409 for a closed ask, 404 for no ask (WORKFLOW1e) |
| `GET /api/quests/{id}/moves` | both modes (FLOWCHART1a2, D186; the flowchart design §2.1): one quest's moves, `{ quest, moves }`, in the order its history replays (what a remote numbered, then this machine's pending ones), never by time. Each move by allow-list (Core's `QuestMoves`): `kind` as the wire spells it, `at`, `machine`, `sequence`, the remote's `number` (absent while pending), `applied` (whether the replay applied it, judged against the quest the moves before it replay to), `from` (the status it reached the quest at), `hold` (the hold it set, or `none` where it let one go), and its kind's own field: a conflict's `attempted`, a dismissal's `dismisses`, a wait's `waitsOn`, a submission's `session`, and the codes of an evidence read (`found`, `missing`, `commit`), a set-up (`local`, `deployed`) and a verdict. Never the publish's payload, a note's body, the answers or a path. An id with no history answers no moves, so a 404 is a host from before the door |
| `GET /api/quests/{id}/attachments/{sha256}` | local mode, loopback only: a kept file, served sandboxed (`Content-Security-Policy: sandbox`, `nosniff`), and anything but an image, a PDF or text as a download, so an attached page never runs on the platform's origin |
| `GET /api/sessions` · `POST /api/sessions` · `POST /api/sessions/{id}/state` | the driver's session records (D46); an open on a quest that is not open, on a take that is not this machine's (CARRY2), or in a busy tree is a 409 with the ledger's sentence, and a stand-down is carried on once, over the take the person's bring-forward made after it began (SYNCMERGE1b0); a move to `stopped` may say `interrupted: true` (D104), and a move to `failed` may say `limit: true` (TOOL4c); a move anywhere else that says either is refused. A finished record moves only to `working`, with the person's words waiting on a record of this machine's, never a stand-down, an intake or a teammate's, and in a tree no other session holds (MSG1a, D137 §2.3). Each record answers `said`, the words waiting, another agent's naming its second opinion as `by` (XAGENT1c); `answer`, the person's joined; a reviewer's `opinion`; and, for a record that continues another, `from` (the session it continues), `fromWhy` and `fromBy`, which the driver names at either open (MIXAGENT1a-iii, D163 point 4) and no wire carries, each to a caller on this machine only; and, for a driven record, `access`, the access setup its start resolved, each pause with its value, which the driver names at the quest's open, kept as a short map of words (anything else a 400), on no wire and to a caller on this machine only (AUTONOMY1a, D184 point 1) |
| `POST /api/sessions/{id}/answer` · `/api/sessions/{id}/added` | the person answers a session parked to ask them (STANDDOWN2), a second answer joining the first (MSG1a); and, local mode only, what they added to a running one, as its driver reports it. Each is kept on the ask the session's work is for (DRIFT1a); `added` answers `kept: false`, with the reason, for a session on no ask |
| `POST /api/sessions/{id}/take-back` | local mode only, the person's door (SAIDBACK1a, D137 §2.4): one word waiting on a parked or ended record of this machine's, by its id (`word`), taken off it before a session took it; with `text`, reworded in the same step, the old word out and the new words kept last under a new id with its files' names, answered as `said` to this machine only (order among several waiting words is not kept). Answers `{done: true, message, session, said?}`, or `{done: false, why: "taken", message}` where the word no longer waits or the record runs again, never an error; a reword with no words or no word named is 400 `no-words`, no record 404, and what never goes on 409 by the say door's word. Nothing is kept on the ask |
| `POST /api/sessions/{id}/say` · `/api/sessions/{id}/taken` | local mode only (MSG1a, D137 §5.3): the person's words to a parked or ended session of this machine's, kept on its record (`text`, `files` kept by name) and answered with the word's id; a park takes them as its answer, kept on its ask at once. A refusal names its word (`refusal`: `no-words`, `not-found`, `not-ours`, `intake`, `stood-down`, `running`, and `opinion` for a reviewer's record, XAGENT1c). And the words a session took, by their ids (`said`, `by` for a fallback's session): each of the person's said after the record ended is kept on the ask as `reopened`, and another agent's, whose word names its opinion as `by`, never is. Words that came from the ask's channel carry its message id (`channel`, SESSONE2e, kept on the said word): the ask already holds them as a `channel` word, so neither the park's answer nor `reopened` keeps them again. Daoris's own words (COWORK1b, D188) carry `from: "daoris"` and their producer's `key`, both kept on the word and answered back: never the person's, so `answer` joins none of them, the ask keeps none, and the went-on line says `ledger.went-on-daoris`. A say whose `key` a waiting word already holds keeps nothing and is answered with that word; an author the service does not know is refused `from`, 400. COWORK1d: `from: "agent"` is another agent's channel claim the person passed on (*Send to its session*), kept with its `channel` and never the person's; the person's own channel message is kept with its `channel` alone and is theirs |
| `POST /api/opinions` · `GET /api/opinions` · `GET /api/opinions/{id}` · `POST /api/opinions/{id}/hand` | local mode only, and to a caller on this machine only (XAGENT1c, D155 point 11): the driver asks a pass (`occasion`, `pass`, `rechecks`, `working`, `candidate`, `reviewer`, `families`, `posture`, `minutes`, the reviewer's `tree`), answered with the opinion and its reviewer's record; reads it back with where its pass stands, by `working`, `session` or `repository`; and hands a first pass's findings to its working session, answered with the word they wait in. 400 for a part out of shape, 404 for nothing by that name, 409 for a bound, a held tree or a session that cannot take them now |
| `GET /api/brainstorms` · `POST /api/brainstorms` · `GET /api/brainstorms/{id}` · `POST /api/brainstorms/{id}/send` · `DELETE /api/brainstorms/{id}` | local mode only, and to a caller on this machine only (BRAINSTORM1a3, D176 points 1, 2 and 8): the door `daoris brainstorm` reaches. The person's question (`question`, at most 4,000 characters; `repository`, whose knowledge the packet is frozen from; `agents`, at most four) written as the request the driver's look takes, under the home's `brainstorms/<id>/`; each brainstorm by where it stands (`state`: `asked`, then the driver's `frozen`, `running`, `ended`; `code`; `packet`, the frozen digest; `sent`, the one the person sent), oldest first; one with the packet as frozen (`frozen`, whole) and the document's Markdown (`document`), each absent until there is one; the person's send, naming the frozen digest, idempotent; and the clear of its records, its packet and its rooms. 400 for a question or agents out of bounds, 404 for no brainstorm by that id, 409 to send before a packet is frozen, to clear one running, or a clear the disk would not finish. The driver's `BrainstormFiles` reads the same files; both are held to `fixtures/brainstorm-files.json` among the driver's tests. A shared host maps none |
| `POST /api/sessions/chat` · `/api/sessions/intake` · `/api/sessions/help` | the record a conversation opens: a chat in a repository (D49 §3); and, local mode only, an intake for an ask (D65 §1b) and Ask Daoris's conversation in its room (HELP1a, D89), recorded in `daoris:help` in the `workspace` the body names (ASKSCOPE1a, D158's ASKSCOPE1 note; none is `default`, and one this machine does not hold is a 404 naming those it does), one running per workspace, going on included, and never pushed by the session sync whatever its workspace. Its `mode` (HELPMODE1a2, D183 point 3) is the access mode its start got, `restricted` or `full`, kept on the record and answered as `helpMode` to a caller on this machine only; none is `restricted`, as a record from before reads, and a mode the ledger does not know is a 400 |
| `POST /api/sessions/{id}/help-mode` | local mode only, the driver's (HELPMODE1a2): `{ mode }`, the access mode an Ask Daoris conversation going on runs under once the person's mode changed since it last ran, written on its record with no move and no revision. 400 for no mode or one the ledger does not know, 404 for no Ask Daoris record of this machine's by that id |
| `POST /api/sessions/{id}/access` | local mode only, the driver's (AUTONOMY1b2): `{ access: { pause: value, … } }`, the setup a resumed driven session now runs under once the person's setup changed since it started, written on its record with no move and no revision. 400 for no map or one that is no short map of words, 404 for no driven record of this machine's by that id (a mirror, a conversation and Ask Daoris's have none) |
| `POST /api/sessions/{id}/resolved` | local mode only, the driver's (KEEP1b, D195 point 6): `{ resolved: ["agent|tool/<name>/<version>", …] }`, the downloaded versions a start of the record was handed, added to its earlier starts' with no move and no revision; the record answers them as `resolved` on loopback alone, and no wire carries them. The downloads' rule keeps each while the session runs. 400 for none, more than 64, or one that is no version's key (a path never is), 404 for no record of this machine's by that id |
| `DELETE /api/sessions/{id}` · `GET /api/sessions/{id}/deletable` | local mode only: delete a conversation's record that served no quest (SESSUX1f, D126 §5.4), or judge it and delete nothing. A chat that took a quest through its own connector served it (CHATTAKE1), and its `served-quest` names no quest. Only an ended record of this machine's goes, named by no ask's intake and no quest's publisher, and never pushed to a remote; a refusal is a 409 with the ledger's sentence as `error` beside its word (`refusal`: `not-ours`, `live`, `served-quest`, `named`, `on-remote`) and the quest, ask, machine or workspace it names. `GET /api/sessions` answers `deletable` per record, false at a shared deployment |
| `GET /api/history?workspace=` · `?quest=` · `?quest=&failed=true` · `?ask=` | local mode only: what a clear of finished history would take, deleting nothing (HIST1b, D153). Exactly one scope, `failed` only beside a quest, or 400. A workspace lists each ask that was closed or whose work closed, then each closed quest no ask here asked, a question riding with the work that asked it. Each unit answers `kind` (`quest`, `ask`, `failed`), `id`, `workspace`, `clearable`, the ids it takes (`quests`, `asks`, `sessions`, `teammates`), `forgotten` (its quests a remote numbered, forgotten here rather than removed), `refusal` when it stays, and `kept`, pieces listed and kept; each refusal is its word (`refusal`) beside the desk's sentence (`error`) and the quest, ask, session, machine or workspace it names, and for `needs-you` only, `waits`: what waits on the person (`parked`, `held`, `conflict`, `ask`, `proposal`; HIST1l), for `open` only, `stands`: how the work in progress stands (`open`, `taken`; HIST1m), and for `awaited` only, `by`: the open work naming it (`question`, `asker`, `step`; HIST1m), each absent beside every other word |
| `POST /api/history/clear` | local mode only: `{ units: [{ kind, id }] }`, exactly the units the listing gave, each judged again and cleared in one transaction, or kept with its word. 400 for no unit, an unknown kind or a blank id; otherwise 200 with `units`, each `{ unit, cleared, message }`, and `failed: { quests, asks }` only where a cleared unit's kept files stayed because the disk would not let go of them (HIST1j), absent otherwise |
| `GET /api/confirmations` · `GET /api/confirmations/{id}` · `GET /api/confirmations/waiting` · `POST /api/confirmations` · `POST /api/confirmations/{id}/confirm` · `/refuse` | local mode only (PERSONDOOR1b, D156 point 4; `waiting` NOTIFY2j: what waits, `{ since, waiting }`, held after `since` until it moves, one past its time at its time, or `wait` seconds, 30 at most, a cursor not the current one answered at once, at most eight held): a terminal's ask for the person's confirmation of a request refused `person-only` or `driver-only`: its `method`, `path` (with the query) and `body` exactly as sent, and `secretSha256`, the hex SHA-256 of a secret the client made (32 random bytes in base64url). Held in memory only, and answered with the confirmation (`id`; `state`: `waiting`, `confirmed`, `refused`, `expired` or `used`; the door's `route` and its name, `act`; `values`, the address's by name; `fields`, every value of the body by its JSON pointer, each `{ name, type, value }`; `body`; `asked` and `expires`, two minutes on) and the sentence a client says while it waits. Only names the door binds may be asked for, each once: a body or query carrying another is refused, so the card shows only what binds. The list is what waits, oldest first. Confirm and refuse take the person key and answer only one that waits. Sent again with `Daoris-Person-Grant: <secret>`, the same method, path, query and body (labelled `application/json`, UTF-8) are answered once within the two minutes; anything else is refused 403 `grant`, a secret or address that grants nothing before the body is read. 400 for an ask out of shape, for a name the door does not bind, for a body over 64 KB or 256 values, or for a door that needs no key; 413 for an ask over 512 KB; 403 `confirmations-full` while five wait; 409 on a host handed no key; 404 for none held, 409 for one that waits no more |
| `GET /api/changes?since=&wait=` | local mode only, a read any caller may make (FABRIC-EVENT3b; the fabric events design §A4.2): which ledger families moved after `since` (`<epoch>.<n>`), answered as soon as one moves or at `wait` seconds (0 to 30, 0 by default) with none, as `{ since, moved }`. The families are `sessions` (`session_revision`), `quests` (the newest `quest_log` position), `asks` (`ask_revision`), `channel` (`channel_revision`) and `words` (`words_revision`, a word a connector keeps on a session's `said` without a note, a helper's or a second opinion's: it moves as `said` grows, never on a take, a push mark or a cursor, PUSH1d1b), the last three counts kept by triggers in the file, so a connector built before them moves them too (PUSH1d1). Each is read by its own revision, so a write that moves none, a sync pass's record, a read mark or a cursor among them, wakes nobody. One sampler reads `PRAGMA data_version` on an observer connection of its own, so a connector's commit from its own process is heard. A missing, malformed, stale or future cursor is answered at once with every family and the current cursor; the epoch is new at a start, after the observer reconnects or first opens after a cursor went out, and when the database at the path is another. At most 8 waits are held, a ninth answered at once without sampling; a wait is counted from its arrival, the host's stop answers a held wait, and a wait is never logged as slow |
| `POST /api/refresh` | local mode only: re-scan whatever repositories the host can see |
| `POST /api/feed/sessions` · `/api/feed/entries` · `/api/feed/code-map` | shared mode only: what a desktop's sync feeds up (D47, MAP3b) |
| `GET /api/feed/held?repository=` | shared mode only: the commits a repository's knowledge, code map and declaration stand on here — what a feeding machine asks git about (SYNC5a, SYNC5b) |
| `GET /api/quests/operations?since=N` · `POST /api/quests/operations` | shared mode only: what the remote accepted after number N, in its order, and a push of a machine's quest operations rebased on N, judged quest by quest (D68) |
| `POST /api/quests/machines/{machine}/retire` | shared mode only (FABRIC-CLOSE1i2): a person's retirement of a machine the handshake counts, asked through their own machine's host; answers `{ retired, closure }`, `retired: false` where it has heard nothing from the machine. A later push marking it counts it again |
| `POST /api/sync?workspace=` · `GET /api/quests/{id}/claim` | local mode only: one pass for a workspace — the quests' fetch, rebase and push, then the session records both ways, then the team's code maps down for the repositories with no checkout here (MAP3e) — answering its conflicts, refusals, record and map counts and wall; and where this machine's claim on a quest stands — held, unconfirmed, lost or none (D69), with `why` `source-reopened` where it lost to the person's reopen (FABRIC-CLOSE1c18) and `forward`, the take it holds the quest by where a bring-forward made it (SYNCMERGE1b0), and `taken`, when the take it stands taken by was made (CARRY3). A take on a shared quest runs the quest half before it answers |
| `GET /api/sync?workspace=` | local mode only: where a workspace stands — operations not yet pushed, the quests the last pass left behind, the quests carrying a conflict, when a pass last reached the remote and last tried, and the wall it hit, and `undeclared`, the machines its remote last said had not declared they know a bound quest, as the last pass heard them (absent where none heard it: FABRIC-CLOSE1i2). Read from the store, reaching no remote; a workspace with no remote here answers `wired: false` and nothing else (SYNC6a) |
| `POST /api/sync/machines/{machine}/retire?workspace=` | local mode only, the person's (FABRIC-CLOSE1i2): retire a machine the workspace's remote still counts, one whose store was deleted or that left the workspace, at the remote; what it answers is kept as the last pass keeps it, so a bind reads it at once. Answers `{ workspace, machine, retired, undeclared, message }`, `retired: false` where the remote heard nothing from it; 404 for a workspace with no remote here, 502 with the remote's words for a wall or a remote before the door. A push from the machine counts it again |
| `GET /api/sessions/since?since=N` | shared mode only: the team's session records held after revision N, in order — every origin but the caller's own (SYNC4) |

There are two host modes (D47 §7, as amended). **Local** — the default — binds to loopback: the OS
account is the base boundary (D21), and the host refuses to start bound anywhere else. A local host
started with a person key additionally gates person/driver writes as described below (D156).
**Shared** (`DAORIS_MODE=shared`) is the team deployment: every route under `/api` needs a minted
per-person per-machine key as a bearer token, no page is served, no machine path is ever answered, and
keys are administered on the binary itself — `keys mint --name <person@machine> [--days N]`,
`keys list`, `keys revoke <prefix>`. The key is shown once and stored hashed; the prefix is the
non-secret audit handle. **A shared deployment serves one workspace** (D48 §5), named by
`DAORIS_WORKSPACE`: every row it takes lands in that circle, and one declaring another is refused in a
sentence naming both. A machine names its remotes in the home's `remotes.json` — a map,
`{ "<workspace>": { "url": ..., "key": ... } }`, with the environment pair overriding it whole — and
the desktop's sync loop, which runs once per wired circle, does the rest.

**Neither shape takes a write from a web page it does not allow** (ORIGIN1, `BrowserOrigins`). CORS keeps a
page from reading an answer, not from sending a simple request (a `POST` with no body, a text body or a form
body), and a browser on this machine is on the loopback. So every `POST`, `PUT`, `PATCH` and `DELETE`, on any
path, whose `Origin` is not allowed, or that names none and whose `Sec-Fetch-Site` is `cross-site` or
`same-site`, is refused 403 before a route runs: `{ "error": <sentence>, "code": "cross-site" }`, and one
`origin.refused` warning in the machine log, by the route's pattern and which header refused it. Allowed are
the CORS policy's own origins (the shell's page on a local host, D92, and `DAORIS_WEB_ORIGIN` when set) and,
on a local host, its own origin under a loopback name. A client that sends neither header (the CLI, the
driver, a rehearsal) is answered as before. A read is CORS's, as it was.

**A local host answers only its loopback names** (ORIGIN2, `BrowserOrigins.IsLoopbackName`). A website whose DNS
name is pointed at 127.0.0.1 is its own origin here, so CORS and the origin gate both see a same-origin page, and its
reads would be answered: the index, the quests, the roots a loopback caller is given. So every request on a local
host, reads, writes and preflights alike, whose `Host` is not `localhost`, a 127/8 address or `[::1]` (any port, any
case) is refused 403 before anything else runs: `{ "error": <sentence>, "code": "host" }`, and one `host.refused`
warning in the machine log by the route's pattern, never the name. A request that names no host is no browser's and
is answered. Every caller already uses a loopback name: the shell and its page at `127.0.0.1`, the CLI, Playwright and
the rehearsals at `localhost`, the development proxy keeping its own `localhost`. A shared host judges no name: it is
reached by its deployment's own, and its key gate keeps a page off it (D47 §7).

**A local host its starter handed a person key opens the person's and the driver's doors to that key alone**
(PERSONDOOR1a–b, D156). The starter writes the key as the host's first input line (`DAORIS_PERSON_KEY_ON_INPUT=1`), and a
caller presents it as `Daoris-Person`. Reads answer anyone; an agent's doors (`respond`, a publish) answer a keyless call
and judge it as an agent's; the driver's own and the person's alone refuse one 403 with `driver-only` or `person-only`,
and a key from another start with `stale`, each with a sentence and one `person.refused` line in the machine log. A
terminal on a machine with a window holds no key, so its client asks the window instead (`/api/confirmations` above) and
sends the request again with the grant the person confirmed. A host handed no key gates nothing, as before.

**A shared deployment takes knowledge from a named commit on the canonical line** (D48 §6). Each feed
carries `{ commit, committedAt, branch }` stamped from git by the machine that holds the checkout; the
deployment refuses one from a branch that is not the repository's declared default. Wholesale
replacement then keeps deletion correct for free, and `/api/repositories` answers the commit each copy
stands on, so staleness is visible rather than assumed. A checkout git cannot answer for feeds no
knowledge, and neither does one with uncommitted changes; its session records and quests still travel.

**Which feed wins is decided by ancestry where git can say** (SYNC5a, `FeedOrder`). The deployment
cannot run git, so the feeding machine asks it: a commit that descends from the one held names it as
`base`, and the deployment takes the feed while it still holds that commit — or refuses it as *moved*.
Without a base, commit time decides, and an older feed is refused as *stale*. The same commit is
compared by a digest the deployment computes over what it would store: equal is already held, and
different keeps the first reading. Every one of those refusals is *information*: the machine behind
is simply behind. The code map (MAP3b) is fed and ordered the same way, at its own commit, and judged
whole again at the door.

`ConvergenceDetector` answers a different question: **which repositories learned the same thing
independently?** It automates the survey that produced this project's own canon — reading twelve
repositories by hand to notice which documents said the same thing in different words. It proposes
candidates; a person decides, through `upstream`, under review.

Configuration is by environment, and every variable is optional — the defaults are the local mode:

| | |
|---|---|
| `DAORIS_HOME` | The Daoris home (D63) — every default below that names a file derives from it. Unset, a host the install carries takes the install's own `data/` (SERVICEHOME2); elsewhere, with the file's own variable unset too, a host refuses rather than defaulting under the profile |
| `DAORIS_KNOWLEDGE_ROOT` | Where the repositories are. Default: the folder containing this workspace |
| `DAORIS_KNOWLEDGE_DB` | Where the index lives. Default: `$DAORIS_HOME/knowledge.db` |
| `DAORIS_KNOWLEDGE_REPOSITORY` | The one checkout a workspace's own server serves (ORIENT1c): the bootstrap registers it alone, and each process re-reads it at its first use and again once the checkout moved (watched) or its reading is five minutes old (a minute where nothing could be watched). Not a folder, and a host refuses to start. Unset: the folder of repositories, read once |
| `DAORIS_KNOWLEDGE_DOCUMENTS` | A repository-relative folder read in each registered checkout, each markdown file split at its headings (ORIENT1c). Unset reads none. One that leaves the repository is refused |
| `DAORIS_KNOWLEDGE_INDEX` | A repository-relative folder holding a generated index, each table row and list item one entry of the kind `index`, naming no lines (ORIENT1c; ORIENT2e). Read before a declared index, so a folder named here is read a row at a time even where the repository declares it. Unset reads none |
| `DAORIS_EMBED_MODEL` | Names an embedding model to **enable semantic search**. Unset = lexical only |
| `DAORIS_EMBED_URL` | Embedding endpoint. Default: `http://localhost:11434` (Ollama) |
| `DAORIS_EMBED_WINDOW` | The most characters one embedded text carries, the title included: the deployment's statement of its embedder's window (D123). A longer entry is embedded in pieces this long, each its own vector. Default: `2000`. Below `200`, or not a whole number, and a host refuses to start. Characters only approximate tokens, so leave margin for code and for 中文 |
| `DAORIS_MODE` | HTTP host only: `local` (default) or `shared` — the team deployment (D47) |
| `DAORIS_WORKSPACE` | HTTP host only: which circle a **shared** deployment serves (default: `default`). Refused on a local host, which holds every circle the machine wired |
| `DAORIS_REMOTE_URL` / `DAORIS_REMOTE_KEY` | one workspace's remote, overriding the home's `remotes.json` **whole**; `DAORIS_REMOTE_WORKSPACE` names which circle the pair serves |
| `DAORIS_STOP_ON_INPUT_END` | HTTP host only: `1` makes the host stop cleanly when its standard input ends, which is how the desktop stops a host it started (LOG2a), and waits 3 s for requests in flight rather than the framework's 30, under the 5 s the desktop waits before it kills the host (FLAKE2). Set by the desktop alone; unset, standard input is never opened |

Verified end to end against the real family with `nomic-embed-text`: **409 entries embedded in 34 s**,
and a query whose words appear in none of the matching documents — *"stop the console from stealing
focus during a capture"* — returned three desktop-capture documents from three different repositories.

Semantic recall is opt-in and never required. Naming a model turns it on and hybrid fuses it with the
lexical index; leaving it unset keeps the service lexical-only rather than half-configured, because an
index that will not start without an embedding endpoint is not local-first. If the endpoint is
unreachable or misconfigured, the refresh still completes and reports the reason — verified against a
local server started without `--embeddings`, which is what the failure actually looks like.

```sh
cd src/Daoris.Service && dotnet test
```

## What it finds today

Scanned across the family, 2026-08-05:

| Kind | Local | Canonical |
|---|---:|---:|
| Rule | 101 | 15 |
| Skill | 96 | 5 |
| Knowledge | 62 | 2 |
| Decision | 58 | 0 |
| Task outcome | 53 | 0 |
| Fix | 13 | 0 |
| **Total** | **383** | **22** |

**405 entries across 11 repositories, and 94% of them are local** — which is the premise of the whole
index, measured rather than assumed. Canonical content is identical in every repository that installs
it, so indexing it per repository would produce a dozen copies of one rule and call that a corpus. The
local material is what varies, and 124 of those entries are decisions, fixes and task outcomes that no
sibling repository can currently reach at all.

## The seams

Four extension points, each with one job, so the pieces that are still undecided can be swapped
without touching the ones that are not.

| Seam | Today | Later |
|---|---|---|
| `IKnowledgeSource` | The local filesystem | A git remote, or a devkit gate that pushes |
| `IKnowledgeStore` | SQLite file, or in memory for tests | A hosted store only if volume ever demands one |
| `IKnowledgeSearch` | FTS5 + BM25, semantic, and hybrid fusing both | Provider routing, so a deployment picks its own model |
| `IDisclosurePolicy` | `LocalOnly` — nothing leaves | `Sharing(repositories)` — opt-in per repository |
| `IVectorProvider` (the sibling's) | Any OpenAI-compatible or Ollama endpoint | Chosen by deployment, never by the feature (D24) |

Choices worth knowing about:

- **A long entry is embedded whole, in pieces** (D123). Each piece is at most `DAORIS_EMBED_WINDOW`
  characters, led by the entry's title, cut at a paragraph where one falls in the window's latter half,
  and reaching back a little into the piece before. Each is its own vector; a search names an entry
  once, at its best piece, and a refresh says how many entries were split. Before, the tier embedded
  the first 2,000 characters and dropped the rest without a word: a third of this repository's text.
- **A hit carries an excerpt, and the excerpt is the entry's prose.** It is a window of the body
  around the first matching term (or, for a semantic hit that shares none, the opening of the piece
  that matched), taken after
  the frontmatter and with a heading's hashes, `**`, `__` and backticks dropped, so it reads as a
  sentence and never as the file's machinery. A lone `_` or `*` stays, since it may be part of an
  identifier. The frontmatter is still searched; only the window skips it.
- **An identifier is found by its words, and a question asks its words joined too** (ORIENT1f). FTS5's
  `unicode61` keeps `ProbeLock` as one token, so a question in plain words never reached the entry that named
  it. Each index row spells an identifier's words beside it (`ProbeLock Probe Lock`, `probe_lock probelock`),
  cut at underscores and where the case turns, never at a digit alone, so `TOOL6g` stays one word; the stored
  body is unchanged and an excerpt reads as written. A question asks its words, then each two and three
  adjacent words joined as an identifier spells them, a join counted once for each word it joins. So
  `ProbeLock`, `probeLock`, `probe_lock` and *probe lock* each find the others, and the entry that names the
  identifier outranks one that only says its words. An index written before is rebuilt when it opens (schema
  4). Held by `TextTests`, `SearchTests`, `SqliteStoreTests` and `ProbeLockQuestionTests`, whose miniature of
  this repository's records answers *what decided the probe lock* with the decision note that names
  `ProbeLock` (ORIENT1g, above), then the fix, where a design section titled with *lock* and *decided*
  answered before.
- **Search returns scored hits, not a list.** Scores are what let two searches be merged, so hybrid
  is a composition rather than a third implementation.
- **Hybrid fuses on rank, not on score.** BM25 returns an unbounded figure and cosine similarity a
  number in [-1, 1]; adding them compares quantities that mean different things, and whichever has the
  larger range silently wins. Reciprocal rank fusion uses only each result's position in its own list.
- **Semantic search is optional and degrades.** The embedder is app-provided, so with none configured
  the service is lexical-only and local mode still works with nothing installed. If either half fails
  the other still answers — an index that returns nothing because an endpoint is down is worse than one
  that returns half of what it knows.
- **The disclosure policy is applied at ingest, not at query.** Withheld-at-query means the material
  is in the store and one forgotten filter discloses it; withheld-at-ingest means it was never there
  to leak. It is a *type* rather than a paragraph so that shared mode cannot be built without
  answering it.

## What it is for

A session in any repository can read that repository's doctrine, because `sync` put it on disk. It
cannot read what the *other* repositories learned. Every decision record, fix log and task outcome in
the family is invisible from anywhere but the repository that holds it — which is how the same problem
gets solved twice by the same person in two directories.

The service is the query layer over all of it: doctrine, decisions, and past task outcomes, across every
adopting repository.

## Why it comes after the canon, not before

Indexing content that is still divergent indexes the divergence. Six copies of a rule that disagree
produce six answers with no way to tell which is current — so the canon has to exist first, which it now
does. This is also why a generated wiki is a complement rather than a competitor (D16): a wiki is
derived from code and fails by going stale; doctrine is authored because something went wrong and fails
by diverging. The service indexes the second kind.

## Shape

- **ASP.NET Core**, so it can compose the family's existing LLM work rather than rebuild it — semantic
  memory, the embedder seam, the vector store and MCP hosting all already ship in the cognition sibling.
  That dependency becomes correct here precisely because this is a separate deployable; the CLI keeps
  its zero dependencies and never learns about this.
- **The canon is an input, not a copy.** `canon/` at the workspace root is the same tree the CLI
  materializes; the service reads it rather than holding its own.
- **One atomic file write** (`AtomicFile`, DEDUP9a). The core writes a file something else may read beside it under a
  name of its own, then renames it over the target, retrying a busy target and removing the beside file; an existing
  file is replaced. Quest attachments, helper asks and answers, and rule and help proposals all write through it. The
  driver keeps a helper of its own (D166): the two projects do not reference each other.
- **The core is ten module folders** (STRUCT5, D166), under one namespace, `Daoris.Knowledge`: `Platform` (the
  home, the atomic write, the machine log, the connection gate), `Composition` (`ServiceFactory`, the one place that
  wires them; no module names it), `Remote`, `Registry`, `Quests`, `Asks`, `Sessions`, `Review`, `Help` and `Knowledge`
  (its files in four scopes: `Search/`, `Store/`, `Ingest/`, `Feed/`). The tests mirror the modules, not the scopes, and the
  MCP door's tests sit in `Mcp/`. `Daoris.Service.Tests`' `StructureTests` holds the layout to `fixtures/structure.json`;
  find a core file by name under the core rather than by a flat path.
- **Two clients, one UI** — see `Daoris.Web` and `Daoris.Desktop`.

## The design is written

**`docs/2026-08-05-knowledge-service-design.md`** — read it before writing code. It settles:

- **Local-first, sharing as configuration** (D21). One service, two modes, one binary; local needs no
  server, no account and no network, and must stay fully useful alone.
- **The disclosure boundary** — what may leave a machine at all, which is the question this project has
  to answer before "who may read it". Indexing is opt-in per repository, silence means keep it local,
  and the untracked local directory is a hard exclusion rather than a permission.
- **Authorization mirrors repository access** rather than inventing a second model that would eventually
  disagree with the first, silently.
- **A git repository as the shared store**, before a database — a direction the driver later closed:
  D47 priced git-as-store and declined it, because a quest queue two machines race needs an arbiter
  that refuses the second take *before* work starts, and the serialization point a lock needs IS a
  host. Shared mode is a deployment of this host; the repositories stay the versioned, reviewable
  source of truth.
- **LLM-assisted merge proposes; a person disposes.** Doctrine that appeared without anyone choosing it
  is the failure this whole project exists to prevent.
- **Built by composition** (D22) — the cognition sibling supplies embeddings, the vector store, routing
  and MCP hosting; the desktop sibling supplies the shell. Released versions only, never working trees.

What was the sharpest open question — does shared mode need hosting at all? — is settled (D47 §3):
it does, it is this same binary in shared mode, and it is built, gate-proven by the family rehearsal's
two-machine phase. Its doors are held in-process as well (HTTP1): every route a shared host maps
refuses a caller without a key and answers a valid one; an expired or revoked key is named by its
prefix and never repeated; no page is served; no GET answers a machine path to any caller; and a local
host asked to bind beyond loopback does not start. The surviving ceiling is the SQLite file, held until
a real team outgrows it.
