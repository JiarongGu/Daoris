# A repository's integration is a fabric workflow Daoris runs (INTEGRATE1, 2026-10-28: round 1, proposed)

**Standing (round 1, proposed under D191, D215):** written by Claude Opus 5.5, maker 1 of 3, from main at `7ebeba1ba`, reading the three
in-flight branches of §1.4 by `git show` alone. Claude Fable and Codex read it next, each without the other's read; the steward settles it.
It changes a foundation (how work reaches a repository's line, who may move a line and when, a protocol between Daoris and a repository's own
runner, and decided rules D87, D145, D192, D207 and D209 rest on), so by D191's note of 2026-10-24 no part of it is accepted on one maker's
read. Nothing here is built, spends an account, signs in, pushes, publishes or deletes. FOUNDATION1 (D212) stays the building priority: §11
marks the rows that can run beside it without touching its files, and every other row waits for the subdomain row it names. The decision is
[D215](decisions/D215.md).

**In one paragraph.** A repository's integration is declared in two halves: the repository's own facts in its `daoris.gates.json`
(`integration`: the gates that run at integration, its runner, its disposable scratch, the files whose rows or lines merge by a rule, the merge
message), accepted by the owner by its blob as the gates already are; and the install's policy on the landing rule (`integrate`: who requests,
how each member lands on the line, the batch, the order, what a failure that does not reproduce does, where a conflict of meaning goes). Daoris
runs it as one integration run per line, whose fixed steps (request, form, replay, resolve, conflict, gates, judge, promote, settle, tidy, close)
are each a fabric event the page shows, and each unit of work waits on it through an `integrate` node between `handed` and `line`. What is
mechanical is settled by declared rules; only a conflict of meaning reaches the member's session, or a landing agent where declared. The gates
run in the candidate's own job under the machine's one admission, in an environment Daoris composes and the steward's shell never reaches. A
failure is confirmed before it is blamed: the failing run is repeated once, as composed, holding the whole admission, and one that does not
recur in a unit no member reaches does not send a batch back. A green candidate fast-forwards the line. Later commits are settled as contained,
superseded or new, never as waiting work. `tools/merge-branch.mjs` keeps this repository's gate plan as its declared runner; its queue retires
once Daoris integrates this repository, the switch one committed line of the declaration so the two never both promote. Beside it, the owner's
second direction of the day is this design's principle: the procedures the development's day depends on are product, not scripts (§3, §7).

## 0. What this rests on

### 0.1 The direction

Two directions of the owner's, 2026-10-28, in this design's words:

1. **Integration is Daoris's to complete.** Merging should not need a queue the parent runs by hand: Daoris gets a proper merge step and
   carries it to its end, with the deeper git-workflow logic configured in the fabric. A merge task becomes a landing task that includes what a
   person would otherwise do around it, rebasing when the line moved among it.
2. **The foundation moves away from unmanageable scripting.** What the development's day depends on is built as product: fabric workflows with
   steps, events and tests, shown on the page and operable by the appointed steward. Contributor tools stay for the checks and generators that
   read the repository.

FOUNDATION1 (`docs/2026-10-25-foundation-design.md`, D212) stays the priority for building; this design may be written now and is built after
or beside it as the steward sequences.

### 0.2 What was read

The brief and `docs/development.md`; the discovery skills' matches (`autonomous-development`, `claims-need-checks`, `development-documents`,
`held-clocks`, `ref-removal-transactions`); D87 and its notes (LANDSYNC0 to LANDREBASE2, GENREGEN1, LANDBACKLOG1), D145, D164, D173's fabric
events design parts B, C and E (`docs/2026-10-16-fabric-events-design.md`), D190's cutover, D192's pipelines, D207's CI scope, D209's steward,
D191, D212's §4 and §14; `tools/merge-branch.mjs` (its header and the queue's sections), `tools/merge-queue.mjs` whole, and the driver's
landing in `Workplace/` and `Settings/` at `7ebeba1ba`; the queue's own records of 2026-10-26 to 28 under the main checkout's ignored `local/`
(read, never written); the backlog rows QUEUEDRAIN1, HEAVYQUEUE1, TIDYOUTPUT1, RUNENDS1 and DOGFOOD1. The integration reviews of 2026-10-05 and
2026-10-10 were not read past the router's rows (§13). Every `file:line` below was read at `7ebeba1ba` unless it names a branch.

## 1. Today's paths

### 1.1 This repository: a branch the install lands, a queue the parent runs

- **The install lands, never merges.** Its rule for this repository is `branch quest/{slug}-{quest}` (TASKS.md:602, DOGFOOD1). The parent then
  integrates every branch, the install's and its own subagents', with `tools/merge-branch.mjs` (`docs/development.md:139-286`).
- **Preview and freeze.** `--preview` merges with `git merge-tree` against main's tip and prints the diff, any conflict the resolvers cannot
  take, and the scoped run (`merge-branch.mjs:6679-6712`, the blocking set leaving out union records, generated folders and the backlog at
  `:6691`). `--message <file>` freezes the tip and the message's bytes, refusing a tip other than the last preview's
  (`merge-queue.mjs:131-145`; `merge-branch.mjs:6715-6740`).
- **A runner the parent's shell starts.** The first enqueue starts `--queue-run` detached (`merge-branch.mjs:6651-6673`), spawned with no
  environment of its own, so it inherits the parent's (`:6657`); it adds only the build-server isolation (`:6636`, `BESIDE_ENVIRONMENT` at
  `:5111`), and every gate's shell inherits that again (`runStep`, `:3568`).
- **The candidate.** Up to eight queued requests in arrival order (`merge-queue.mjs:72`, `:182-185`), each merged `--no-ff` on the last with
  its frozen message in `local/scope-tree` (`mergeMember`, `merge-branch.mjs:6239-6258`). The resolvers: the backlog by its merge base
  (`resolveBacklog`, `:1522-1544`; `mergeByBase`, `:1485-1510`), the declared generated folders written again (`:1360-1389`), union records by
  their attribute (`.gitattributes:39-43`) with what union glued set apart (`setApartJoins`, `:1624-1644`), the router by its row driver
  (`.gitattributes:50`). Any other conflict, and a merge commit git will not make (an empty merge among them, `:6252-6256`), sets the member
  aside and hands it back (`formCandidate`, `merge-queue.mjs:192-209`).
- **The scoped set.** The cheap tier always, each reached gate, the Process halves by class, the git half within eight minutes, browser specs
  and rehearsals deferred (`scopedSet`, `merge-queue.mjs:441-473`; `gitBudget`, `:418-432`; `runCandidate`, `merge-branch.mjs:6357-6406`). The
  admission record notes only the scoped tier's state (`:6400-6406`); the background run does not read it and gates run in no job object yet
  (`docs/development.md:283-286`).
- **The judge** (`judgeFailure`, `merge-queue.mjs:293-333`; D207 point 3). A death of memory holds the candidate (`merge-branch.mjs:6420-6424`).
  A known red last seen on the base is inherited. Otherwise the failing selection runs on the base first: a gate naming no test that fails there
  stops the queue, a test is inherited (`merge-queue.mjs:303-312`). Then the whole candidate and every prefix (`:313-316`), the selection being
  the failing test's class alone (`prefixCommand`, `merge-branch.mjs:6308-6317`), the last transition pinned and its neighbours run again
  (`merge-queue.mjs:319-325`). A whole candidate that does not fail again, a prefix that cannot run, or neighbours that disagree leave the
  failure unresolved (`placeOnPrefixes`, `:271-281`): a candidate of several then runs its members one at a time, one of one is handed back as
  unresolved (`candidateOutcome`, `:363-394`; `judged`, `merge-branch.mjs:6609-6613`), and by D207 "a passing re-run" never makes a candidate
  green (`docs/decisions/D207.md:63-65`), nor may it be re-formed until a run passes (`:163`).
- **Promotion.** Main fast-forwards to a green candidate only in a clean main checkout on `main`, held and tried each tick otherwise, re-formed
  when a direct commit moved main (`merge-branch.mjs:6516-6550`; `promotion`, `merge-queue.mjs:246-250`).
- **A drop.** `--drop-batch` takes a request off, or ends the candidate and hands its members back (`dropQueued`, `merge-queue.mjs:151-160`);
  nothing ends the gates already running (QUEUEDRAIN1, TASKS.md:22).
- **Its own flake rule.** For a merge in place, a failure outside FLAKE1's classes is never re-run, because a test that passes alone after
  failing in its suite may be polluted by another, which is a defect (`merge-branch.mjs:245-251`).
- **After it.** The background run gates main's tip (`docs/development.md:339-361`), and an install is staged only on its receipt (D207 point 5).

### 1.2 Other repositories: a merge on a press, a rebase by words, no gates

- **Two forms.** A rule merges into the line or makes a branch (`Settings/LandingKey.cs:11-18`), with its pattern, tidy, plugin, automatic
  acceptance, ticket key, squash and post-landing cycle (`:48-61`). A merge rule never accepts automatically: both doors refuse it
  (`LandingKey.cs:118-120`; D145 point 1, `docs/decisions/D145.md:16-28`), and the look closes such an entry as off (`Workplace/AutoLander.cs:188-192`).
  So a merge lands on a press, the person's: landing is no role's act (D209 point 3, `docs/decisions/D209.md:63-65`).
- **The press.** The plan says what would land (`Workplace/TreeLands.cs:52-84`, `:111-151`). The landing judges what the line holds
  (`:578`), replays the rest aside onto the line's tip where it moved (`:607-612`; D87's LANDREBASE1 note, `docs/decisions/D87.md:183-221`),
  and merges `--no-ff` in the repository's own checkout (`:287-289`), only while it is clean and on the line, read immediately before
  (`:264-282`), a failure aborted and the checkout left as it was (`:290-303`).
- **A conflict.** The replay refuses; the window offers the session rebase words, and the automatic and terminal landings say them as
  Daoris's words keyed by the tip (`Workplace/RebaseTelling.cs:10`; `docs/decisions/D87.md:284-295`). Generated files take the line's copy and
  are written again under the person's acceptance of the declaration's blob (`:232-255`); the backlog settles by its base (`:257-282`).
- **No gates.** The landing gate is the run's workflow part, the second opinion and the review (`Workplace/TreeGates.cs:40-57`); the session's
  own checks are trusted (D192 point 4).
- **The tidy.** A tree goes with the rule's tidy only with nothing in flight (`Workplace/TreeTidies.cs:43-50`); ignored files under thirteen
  build-folder names go with it, and any other ignored file written since the tree opened keeps it (`Workplace/TreeScratch.cs:15-19`, `:36-58`;
  TREEKEEP1). A repository cannot say that its own verify output is scratch (TIDYOUTPUT1, TASKS.md:31).
- **After landing.** `trees land --plan` lists commits the line lacks as work to land (`docs/development.md:235-241`), so a session that goes on
  after its landing makes commits that read as waiting.

### 1.3 What the fabric already holds

- **A rule carries what follows the landing.** `cycle` on the landing rule is a graph fragment (`Settings/LandingKey.cs:54-60`; the fabric events
  design §C2 and §E7, `docs/2026-10-16-fabric-events-design.md:529-611`, `:1784-1791`), inherited whole with the rule (D164).
- **Nodes and their authority.** `handed` and `line` are observed by git's proof (§E2's table and §E4, `:1464-1466`, `:1520-1534`); FOUNDATION1
  names `work.landed`, and `work.integrated` once the line holds the work (`docs/2026-10-25-foundation-design.md:1157`).
- **A performed check on the line.** D192's verification pipeline: a `check` Daoris performs, a run per batch on the line run to its verdict
  (D192 points 6 and 7), its gates the declaration the person accepted by its blob (point 2), with an automatic bisect rejected for landed work
  (`docs/decisions/D192.md:90-91`). FABRIC-PIPELINE1d to 1j are open (TASKS.md:176-181).
- **Twins already made.** The driver's `BacklogMerge` (`Workplace/BacklogMerge.cs:22`) and `GeneratedWriters` (`Workplace/GeneratedWriters.cs:21`)
  twin the tool's backlog and generated resolution for a landing's replay.
- **The environment.** A start drops the role variables it inherits and keeps `DAORIS_HOME` (`Agents/Adapters.cs:1661-1677`; ENVLEAK1,
  `docs/FIX-LOG.md:2930-2941`).
- **The map.** Workplace holds the trees, git and the landing (`docs/2026-10-25-foundation-design.md:2342`), Review the heavy gates, checks and a
  check's verdict (`:2348`), Workflows the node runtime (`:2350`), Events the vocabulary (`:2339`), Settings the keys (`:2335`), Roles the role
  catalogue (`:2343`).
- **Settings keep what a build does not own.** A write of `driver.json` keeps every key the writing build does not know
  (`Settings/DriverConfig.cs:938-944`).

### 1.4 In flight

- **HEAVYQUEUE1** (`quest/heavyqueue1-one-admission-3d2f1806497d`, QUEUEDRAIN1 and CIWATCHHOLD1 merged into it): one machine-wide admission in
  the Daoris home, `locks/admission/admission.json`, written by twins (`tools/machine-admission.mjs`, `Sessions/Heavy/MachineAdmission.cs`) under
  the keeping lock; capacity one per seven processors, two to six, first come first served; an entry counts while its process lives and beats.
  The merge tool's heavy gates and probes take a place (its `admitted`, `machineAdmission`), the tool finding the home by `DAORIS_HOME`, and
  nothing is shared without it.
- **QUEUEDRAIN1** (`quest/queuedrain1-drop-ends-its-gates-3f56befcd2b9`, `035059081`): a dropped candidate's gates and probes end.
- **CIWATCHHOLD1** (`quest/ciwatchhold1-ci-watch-test-7f249ff631aa`): a CI run's green status is written only after it lets the gate tree go.

### 1.5 What the survey finds

1. **Two integrators, two rules for one act.** The tool gates, batches, bisects and fast-forwards; the driver's merge form merges one press at a
   time into the checkout, ungated. Mechanical resolution lives twice and in part: the backlog and generated folders are twinned, union joins and
   the router only in the tool, a member already contained nowhere.
2. **Integration is a procedure, not product.** A shell, a detached process, files under `local/`, the parent's environment. The page shows none
   of it, and no role may operate it.
3. **A failure that does not reproduce is blamed on the batch.** It is the queue's commonest red (§2), and D207's rule makes it a hand-back.
4. **Cancellation is not owned.** Ending a candidate ended no process until QUEUEDRAIN1.
5. **After landing, nothing is settled.** Later commits read as waiting work, and a tree's own verify output keeps it.
6. **Nothing orders two rows that change the same files.** DOGFOOD1's note (TASKS.md:602) is a practice the parent remembers.

## 2. The evidence of 2026-10-28

**The queue's own records** (`local/scope-runs.jsonl` and `local/merge-queue.log` under the main checkout, read for this design; times in UTC as
kept). From 2026-10-26 10:28 to 2026-10-28 04:40 the queue made 29 scoped runs: 14 green; 9 red only on failures its judge left unresolved (13
such judgements in them, beside one inherited); 3 with a member pinned, 4 pinnings, each a census test (`ParticipantWorkReadersTests` twice,
`SessionMarkerTests`, `GitSeedTests`' no-deletes census); 2 stops on a check failing on the base; 1 dropped. Across all 29 runs the judge left 22
failures unresolved and pinned 4. The longest gate of the last three runs took 25 to 29 minutes, against 5
to 12 before. Candidate `7ebeba1` (seven members) promoted at 23:28 on 2026-10-27; the next, `268477e` (six), was red at 04:40 on two unresolved
timing tests (a meeting's stalled turn cut at its minutes; a session another process runs stopped through its request) and split into one
member at a time. Set aside on 2026-10-28: a branch whose row met another's at one line of `docs/development.md`'s census table; a branch whose
merge commit could not be made, its work already in an earlier member; a branch conflicting in `package.json`.

**What the steward reported** (the brief; cited as evidence):
- The queue promoted one batch in more than five hours while about eleven changes waited; every candidate failed only on load-sensitive tests
  none of its members touched (short fixed waits, a web test's 20 s timeout, a structure gate's wall-clock bound, a held-file move), each sending
  the batch back. The steward dropped and requeued candidates by hand, ended runners because a drop did not end a candidate's gates
  (QUEUEDRAIN1), resolved set-asides (the two above), and once found a real census miss.
- The steward's shell environment, the install's home variable among it, leaked into the queue's gates (QUEUEENV1; no row of it stands in
  `TASKS.md` at `7ebeba1ba`, and this design's row a is its home). HEAVYQUEUE1 (TASKS.md:33) records the load beside: candidates at twice the
  fast Driver gate's time while a driven session's thousand-test run and a memory bench ran.
- In the library repository (Lyntai) five landings conflicted in a day, most on one status line every row edited, each needing a full session
  turn to rebase. One session committed after landing a change the line had already overtaken, leaving a review entry nobody could clear without
  a forced removal, since its tree held its own verify outputs (TIDYOUTPUT1, TASKS.md:31: two landings kept for 119 ignored files), and its
  closing note said its work waited for an Accept when nothing was left (beside RUNENDS1, TASKS.md:30).
- Two parallel library rows each added the same runner method; built from one base, they conflicted in meaning at landing. This repository met
  the same on 2026-10-22 with KEEP1d and KEEP1e (DOGFOOD1's note, TASKS.md:602).

**What the evidence says.** The judge did what D207 asks; the rule is what cost: a failure that does not reproduce is treated as a member's. The
steward's hand work is the queue's missing parts: cancellation, a clean environment, two more mechanical resolutions, and a way to decide a held
candidate. In the library, landing had no gates and no notion of a line's own rows, so a conflict of meaning reached landing and a status line
cost a session's turn.

## 3. The principle: what the day runs on is product

**The procedures the development's day depends on are product, not scripts.** Each is a fabric workflow (D157, D190, D192): steps of fixed
kinds with their own state, each step a fabric event (D212 §4), each with tests, shown on the page, and operable by the appointed steward through
its recorded acts (D209). A script is kept only where it is a check or a generator that reads the repository (a census, a document check, the
index and code-map generators, a git merge driver git itself runs), a harness that proves the product from outside it, or an instrument a person
starts. The test is the same as the doctrine's for memory: a procedure a fresh install would lack, or that only the parent's shell can run, is a
missing product part.

Why: integration fails in exactly the places a script cannot reach. The page cannot show what the script holds in its process; a role cannot be
granted an act that is a shell command; a crash of the parent's shell is the queue's; the parent's environment becomes the gates'. Each of the
day's hand repairs (§2) was a step the product did not own.

## 4. The declaration

### 4.1 Two homes, one for each kind of fact

| Fact | Home | Why there |
|---|---|---|
| What the repository is: its gates at integration, its runner, its scratch, its row tables and line rules, its merge message, who integrates it | its `daoris.gates.json`, key `integration`, read from the line's tip and **run only as the owner accepted it by its blob** (the acceptance FABRIC-PIPELINE1a built for `landed`'s gates, D192 point 2; GENREGEN1 point 2) | versioned and reviewed with the files it describes; a session cannot declare a command and have its own landing run it |
| How this install integrates it: who requests, how members land on the line, the batch, the order, what an unconfirmed failure does, where a conflict of meaning goes | the landing rule in `driver.json`, key `integrate` (`landings`, `workspaceLandings`), inherited whole with the rule (D164), set at Setup, by the terminal and by Ask Daoris's proposal at the person's press | the install's trust choices, which no repository file can lower |

**The repository's half** (`daoris.gates.json`):

```json
"integration": {
  "by": "daoris",
  "gates": "reached",
  "runner": "node tools/merge-branch.mjs --integrate-runner",
  "scratch": ["local/scratch/**", "_fixtures/**"],
  "merge": [
    { "paths": "docs/development.md", "by": "rows" },
    { "paths": "docs/STATUS.md", "line": "^Status: ", "by": "run", "run": "node tools/status.mjs" }
  ],
  "message": "Integrate {name}: {title}",
  "env": { "pass": [] }
}
```

| Key | Means | Absent |
|---|---|---|
| `by` | who integrates the line: `daoris`, or `tool` for a repository whose own tool still does (this one until row k) | `daoris` where the rule integrates |
| `gates` | the gates at integration: a list of declared gate names, each run whole, or `reached`, the runner's plan for the candidate's paths | every declared gate, whole |
| `runner` | the repository's runner (§5.9), which plans and runs its gates and probes a failure | Daoris runs each named gate's `run` itself |
| `scratch` | globs of ignored output a tidy may take with the tree, beside the thirteen build folders `TreeScratch` lists | none; today's rule |
| `merge` | files whose conflicts settle by a rule: `rows` (by base: a row a side added kept, one removed dropped, rows both add at one place both kept, one base row both changed a conflict, MERGETASKS1's rule widened from the backlog), or a `line` pattern settled by `run` (its writer recomputes it after the merge) or `line` (the line's copy kept, the member's change named superseded) | none; a conflict |
| `message` | the merge commit's subject, from `{name}`, `{title}`, `{quest}`, `{repository}`, `{branch}`; its body the unit's closing summary and the members' `Co-Authored-By:` trailers, deduplicated | `Merge branch '{branch}'`, today's merge form's (`TreeLands.cs:287-289`) |
| `env.pass` | exact variable names a gate needs from the install's environment beyond the composed base (§5.5); never a `DAORIS_` name | none |

**The install's half** (the landing rule):

```json
"landings": { "Daoris": { "form": "merge", "tidy": true,
  "integrate": { "request": "done", "commit": "merge", "batch": 8, "order": "arrival", "gates": "declared",
                 "unconfirmed": { "unreached": "land", "reached": "split" }, "conflicts": "session" } } }
```

| Key | Values | Absent |
|---|---|---|
| `request` | when a unit enters integration: `owner` (the person's Accept), `steward` (the steward's act, §8), `done` (the quest's done, as D145's switch does for a branch); `done` only where `gates` is `declared` and the declaration names at least one gate | `owner` |
| `commit` | how each member lands: `merge` (one `--no-ff` merge commit per member), `rebase` (rebase-and-merge: replayed and the line fast-forwarded, no merge commit), `squash` (one commit per member, PROCESS1b-ii's message) | `merge` |
| `batch` | members per candidate, 1 to 8 | 8 where gates run, 1 where none |
| `order` | `arrival`, or `apart` (a member changing a path an earlier member of the forming candidate changed waits for the next candidate) | `arrival` |
| `gates` | `declared` (the accepted blob's `integration.gates`), or `none` (today's trust of the session's own checks) | `none` |
| `unconfirmed` | §4.5: for a unit no member reaches and for one a member reaches, each `land`, `split` or `hold` | `{ "unreached": "land", "reached": "split" }` once row b's replay holds (§4.5); until then `split` for both, today's rule |
| `conflicts` | a conflict of meaning goes to the member's `session`, to a landing `agent` (§5.4), or is held (`hold`) for the steward or owner | `session` |
| `line` | the line by name | the repository's canonical line (`daoris driver line`) |

### 4.2 The landing shapes

| Shape | The rule | What integration does | The line reached |
|---|---|---|---|
| A branch for review | `branch <pattern>` | nothing: the branch is the result | observed when the person merges it (`line`, as today) |
| A pull request | `branch <pattern> --plugin <id>` | replay onto the line's tip; the declared gates, where the rule's `integrate.gates` is `declared`, before the plugin pushes; no batch, no promotion (a later row, §11) | `pull-request`, then `line`, observed (D148) |
| Merge | `merge` with `integrate`, `commit: merge` | every step of §5.2 | promoted |
| Rebase and merge | `commit: rebase` | every step; the line linear, each member's commits keeping their own trailers | promoted |
| Squash | `commit: squash` | every step; one commit per member | promoted |
| A merge rule with no `integrate` (every merge rule today) | `merge` | from row g, read as `{ request: owner, gates: none, batch: 1, commit: merge }` with today's message: the commits today's press makes, made in Daoris's integration tree, so the checkout is only ever fast-forwarded | promoted |

Integration never rewrites the line or a pushed ref: the line moves only forward to a gated commit (§5.2 step 8), and what is replayed is a
session's unpushed commits, into new commits in Daoris's own tree, as D87's replay already does (D109, D87 point 2). A push stays the owner's
(D37; D191 point 3), or the rule's plugin's under D145's standing say-so, unchanged.

### 4.3 The gates at integration

`gates: declared` runs the accepted blob's `integration.gates` on the candidate's tip; `reached` hands the choice to the runner's plan, which for
this repository is today's scoped set (D207 point 3) unchanged. The commands are read from the line's accepted blob at the candidate's base, never
from a member's copy. A member that changes the declaration integrates under the line's accepted one, and once promoted its blob is offered for the
owner's acceptance; integration goes on under the last accepted blob meanwhile, and a gate whose accepted command no longer runs on the line
stops the line's integration as a check failing on the base does, the acceptance named as the remedy. A declared runner runs the candidate's own
code, as every gate does: the acceptance binds which commands run, never the code they run (D192's reading of its runner).

### 4.4 Batching and ordering

A candidate takes up to `batch` requests in their frozen order. Under `apart`, a request whose changed paths meet an earlier member's in the
forming candidate waits for the next, so two overlapping members never share a bisection and the later meets the earlier's promoted work at its
own replay. The 2026-10-26 to 28 records show no pinning between overlapping members, so this repository keeps `arrival`; `apart` is for a
repository whose rows collide (§9).

### 4.5 A failure that does not reproduce

**The confirm run.** Before a failure is placed on any member, the failing gate's run is repeated once on the candidate's tip **as composed**
(the same units, settings and order, where the runner can run them so; the gate whole otherwise), **holding the whole admission**: an entry in
HEAVYQUEUE1's admission that takes every place, first come first served, so it starts once the heavy runs before it end and admits none beside
it; a session's heavy run asking meanwhile waits and is told, by HEAVYQUEUE1's words. One confirm run per signature per candidate, never more.

- **Fails again with its signature: confirmed.** It is judged as today (§5.3): the base, then the prefixes, the last transition pinned.
- **Passes: unconfirmed.** The gate's verdict on the candidate's tree is the confirm run's pass, and the first failure is filed as a **sighting**
  on its unit with the candidate's tip and the load the admission held at the time. What the candidate does is the rule's `unconfirmed`, by
  whether any member's paths reach the failing unit by the runner's reach (a unit the census cannot place counts as reached):
  - `land`: the candidate goes on to promotion;
  - `split`: today's rule, the members one at a time, one of one handed back as unresolved with the sighting;
  - `hold`: the candidate waits for the steward's or the owner's `decide` (§8), the evidence shown.
- **Fails another way:** the new failure is the candidate's, confirmed, since it failed with nothing heavy beside it.

**The default is `unreached: land`, `reached: split`.** Safe because: the confirm run is a stronger test than a re-run (a test polluted by
another fails again as composed, which a class run alone would hide, `merge-branch.mjs:245-251`; a load race does not, since nothing heavy runs
beside it); a unit no member reaches is one the members' changes do not touch by the census, and a real break the census missed reproduces and is
bisected whatever the reach; the install is never staged on a scoped green (D207 point 5), and the background run gates every promoted tip whole.
The exposure left is named (§13): a member that makes a unit outside its census reach flaky without making it fail. `reached: split` keeps
today's rule exactly where the member could be the cause. **The strict setting** is `split` for both, today's rule, and it stays the default
until row b replays the 29 recorded runs of §2 under the policy and the steward reads which would have promoted and whether any later run found a
pinned failure there.

**Sightings feed the fixes.** Three sightings of one unit within seven days are shown as a finding on the Integration view with the row its
fix needs (a held clock, `held-clocks.md`; TREECLEANFREE1, PLUGINSHUTDOWN1, CHATGOONWAKE1 and HOSTCLEAN1 are such rows today, TASKS.md:15-25). A
wider window is never the policy's answer.

This amends D207 point 3's *a passing re-run never making a candidate green*, by name, for the confirm run alone; its rejection of re-forming a
candidate until a run passes stands (`docs/decisions/D207.md:163`): one confirm run, bounded.

### 4.6 Who requests

`request: owner` is today's Accept. `request: steward` lets the steward's act request a unit, or any branch of a registered repository whose rule
integrates, the steward's own records branches among them (§8). `request: done` makes the quest's done the request, as D145's switch does for a
branch rule; it is allowed on a merge rule only with declared gates, since a merge with neither a press nor a check is what D145 point 1 guards.
This amends D145 point 1 and `LandingKey.Problem`'s refusal for a merge rule carrying `integrate`. `autoAccept` is not reused: an older build
skips a merge rule that carries it (`LandingKey.cs:118-120`, `:385-387`) and would fall back to the workspace's rule.

### 4.7 Defaults, inheritance and older builds

The repository's rule replaces its workspace's whole, `integrate` with it (D164). A build that does not know `integrate` keeps it on its writes
(`DriverConfig.cs:938-944`) and reads the rule as today's merge, landing it on the person's press with no gates; in the rollback window that is
said once on the staged update's note, and nothing else is lost. A build that does not know the store's integration tables (§5.6) never reads
them (`compatible-builds.md`).

## 5. The integration run

### 5.1 Two levels

- **The unit's graph.** Under a merge rule, an `integrate` node, `waited`, sits between `handed` and `line` in Current's graph, and a named
  workflow's version decodes its merge landing to the same nodes (the fabric events design §E7). `handed` is reached when the request is frozen
  (`work.landed`); `integrate` is reached when a promoted candidate holds the frozen tip, its evidence the candidate and the promoted commit, or
  when the unit is found contained (step 3); it fails when the unit is handed back, with the code (`work.integrated` on reach); `line` stays
  observed by git's proof.
- **The line's integration run.** One per line, built in: its steps fixed, its options the declaration's, candidates one at a time, each to its
  verdict unless dropped or stopped. It has a record of its own in the store (§5.6), as D192's landed run has, and it is no named workflow (§10).

### 5.2 The steps

| # | Step | What it does | Its event |
|---|---|---|---|
| 1 | **request** | freezes the unit's tip and its message, composed by the declaration's `message` | `integration.queued` |
| 2 | **form** | takes up to `batch` requests by `order` and makes a candidate on the line's tip in the line's integration tree, one per line, kept warm as `local/scope-tree` is | `integration.formed` |
| 3 | **replay** | for each member, onto the candidate so far: what the line or an earlier member holds is left out by D87 point 1's judgement (`LineHolds`, content then patch), and said; a member nothing is left of is **contained**, its node reached with *contained in `<commit>`*, never set aside; the rest replayed aside (D87 point 2, LANDREBASE1) and landed by `commit` | `integration.replayed` |
| 4 | **resolve** | each stop settled where every conflicted path is mechanical: a generated folder (written again by its accepted writer, GENREGEN1), a union record (git's own; the joins union glued set apart, MERGEJOIN1 and UNIONGLUE1, given a driver twin), the backlog (by base, LANDBACKLOG1), a declared row table or line rule (`integration.merge`), the router (its row driver, which git runs from the clone's own configuration) | `integration.resolved` |
| 5 | **conflict** | any other conflict is one of meaning: the member goes by `conflicts` (§5.4) and the candidate goes on without it | `integration.conflict` |
| 6 | **gates** | the declaration's gates on the candidate's tip, through its runner or directly (§5.9); each heavy gate takes a place in the machine's admission; every process in the candidate's job; the environment composed (§5.5) | `integration.gates` |
| 7 | **judge** | each failure by its signature (§5.3) | `integration.judged` |
| 8 | **promote** | the line to the candidate's tip: `merge --ff-only` in a clean checkout on the line, read immediately before (TreeLands' guards, `:264-282`), or, where no worktree has the line out, one `update-ref` verifying the line's old value; held and said otherwise, tried at each look; formed again where the line moved | `integration.promoted`, `integration.held` |
| 9 | **settle** | for each member: its node reached; its branch's commits past the frozen tip judged **contained** (said), **superseded** (asked, §5.7) or **new** (a new request where its session is done, else its session's) | `integration.settled` |
| 10 | **tidy** | each member's tree by its rule's tidy, the declared scratch disposable beside `TreeScratch`'s list, nothing forced; a tree that stays follows the landed tip (D87 point 7) | `integration.tidied` |
| 11 | **close** | the unit's integration note written from its record (a code and its facts), which the review, the session's head, the quest's page, the CLI and Ask Daoris read: *Accept* or *waits for you* is said only where `request` names a press still due | `work.integrated` |

A member handed back at step 5, 7 or 8 says why (`integration.handed-back`), to whom and with which words. A line whose base fails a gate that
names no test stops (`integration.stopped`), lifted by a commit that moves the line, a `recheck`, or the run's own recheck an hour on, as today.

### 5.3 The judge

In order, for each failure of a candidate's gates:
1. **A death of memory** is the machine's, held as D207 holds it, never run on the base or the prefixes.
2. **A known red last seen failing on the base** is inherited, as today.
3. **The confirm run** (§4.5). Unconfirmed: by the policy, and no base run is made. Confirmed: on.
4. **The base.** The failure's selection on the base: failing with its signature, a test is inherited and filed known red; a gate that names no
   test stops the line's integration.
5. **The prefixes.** The selection on every prefix, as composed where the runner can run it so; the last transition pinned and handed back to its
   member's session with the failure's log; neighbours that disagree, or a prefix that cannot run, leave it unresolved, and a candidate of several
   then runs its members one at a time. A one-member candidate's confirmed failure on a passing base is pinned to it.

Signatures are D207's (a test by its full name, case, exception type and assertion location; a gate naming no test by its step; a build by its
error codes), read by a twin of `tools/test-failures.mjs` in Review that holds its fixture table.

### 5.4 A conflict of meaning, and the landing agent

`session`, the default: the member's session is told through the say door, as Daoris's words keyed `rebase/<session>/<tip>` (LANDREBASE1 point 3,
`RebaseTelling`), with the conflicted files, the line's commits that met them, and the member it met where that was another member of the same
candidate; it resolves in its own tree, runs what its change reaches, commits, and its new tip is a new request. Nothing else waits on it.

Where the session cannot go on (its record, tree or conversation gone; held by an account's limit past its reset, D206), `conflicts` decides: `hold`
for the steward's or owner's act, or `agent`: **a landing agent**, a working role (D212 §2.7's catalogue, capability `build`) started by Daoris
in a tree of its own from the line's tip with the conflicted member's commits, handed git's conflict lines, both sides' commits and both quests'
titles and bodies, told to resolve only the conflicted change, run the checks it reaches and commit; its tip is a request in the member's name,
its resolver recorded. It counts against the cap as any start does, and resolves nothing in a file its brief does not name. Off by default.

### 5.5 The gate environment

A gate never inherits a person's or the steward's shell: Daoris starts it, and its environment is composed from the install's own, in one list
both the driver and the tool's twin hold, its names held by the wiring census (`SystemWiringTests` for every `DAORIS_` name):
- **removed:** every `DAORIS_*` variable (the role variables ENVLEAK1 already removes, and `DAORIS_HOME`, the role key and the supervisor key);
  git's repository-locating variables (`GIT_DIR`, `GIT_WORK_TREE`, `GIT_INDEX_FILE`, `GIT_OBJECT_DIRECTORY`, `GIT_ALTERNATE_OBJECT_DIRECTORIES`,
  `GIT_COMMON_DIR`, `GIT_NAMESPACE`, `GIT_CEILING_DIRECTORIES`, `GIT_CONFIG*`; `docs/development.md:459-462`); the merge tool's `MERGE_BRANCH_*`
  switches;
- **added:** the build-server isolation (`BESIDE_ENVIRONMENT`'s three), the admission's hold marker (`DAORIS_ADMISSION_HOLD`, so a heavy run a
  gate starts sees itself inside its hold), and the declaration's `env.pass` names.

The runner the tool starts today gets the same composition (row a), its admission found by the runner's own process, never handed to a gate.

### 5.6 State, recovery and cancellation

- **The store.** Workplace's family in the desktop store (D212 §13): `integration_requests`, `integration_candidates`, `integration_members`,
  `integration_steps`, `integration_sightings`, every column required, each step's start and end written in one `BEGIN IMMEDIATE` transaction
  before and after its effect. Known red moves from `local/ci-findings.json`'s shape into Review's own table with the findings' rules unchanged
  (D207 point 4), once FABRIC-PIPELINE1e runs the background check.
- **One integrator per line.** The run is the home's lease holder's, one per line; `integration.by` in the line's committed declaration names who
  integrates it, so the tool and Daoris never both promote one line (§6.3).
- **Recovery at the come-up.** A candidate whose tip the line holds is promoted, read by ancestry (`resumeCandidate`'s rule); a promotion is judged
  by ancestry before anything is retried; a step whose job is gone runs again from its start.
- **Cancellation is owned.** Every process a candidate starts runs in one job object named by the candidate (CISCOPE1e2's job, QUEUEDRAIN1's lesson):
  a drop, a stop, a supersession or the run's end closes the job, its descendants ending with it, before its admission places are let go.

### 5.7 After landing: contained, superseded, new

A commit made on a unit's branch after its request froze is judged at step 9 against the promoted line:
- **contained**: the line holds it by content or patch, or its replay is empty: said, never offered again;
- **superseded**: its replay conflicts, and in every conflicted hunk the line's side was changed after the frozen base by a commit outside this
  unit's work: said as superseded by that commit, kept on a recovery ref (D102's), and offered to the session (or, where it cannot go on, the
  steward) as *superseded* or *rebase it*; never said as waiting for an Accept;
- **new**: anything else, a new request at the unit's next revision when its session is done, else its session's to finish.

The review row of a unit whose `integrate` node is reached and which holds no new commit leaves the review (REVIEWLANDED1, D102), its tree going
by its rule without `--force`.

### 5.8 The page and the terminal

- **The Integration view** (集成) on the repository: the queue (each request's unit, its frozen tip, since when), the candidate with its members
  and each step's state as it moves, what holds it and the act that releases it with who may press, what was handed back, to whom and in which
  words, the sightings and known red by unit, and the promoted history. A pushed slice, `INTEGRATION`, partitioned by repository, its owner in
  .NET (`pushed-slices.md`).
- **The session's head** says its unit's integration state in one line from the same record; *What needs you* lists a candidate held for the
  owner and a declaration owed acceptance (a new wait kind, `person-waits.md`).
- **The terminal**: `daoris-driver integrate <repository>` shows; `--plan <branch>` is the preview (objects only); `--request <branch> [--message
  <file>]`, `--drop <request>`, `--recheck`, `--decide <candidate> land|split|hand-back`, `--order <request> first|last` are the doors of §8.
- **Words** in English and Chinese; the glossary gains *integration* (集成), *candidate* (候选), *handed back* (退回), *contained* (已包含) and
  *superseded* (已被取代), beside *landing* (落地), *line* (主线) and *merge* (合并).

### 5.9 The runner contract

A repository's declared runner answers three verbs, run in the integration tree as its working directory, with the composed environment, inside
the candidate's job, each writing a JSON report:

```
<runner> plan  --base <sha> --tip <sha> --json <file>                 # { run: [{gate, why}], kept: [...], deferred: [...] }
<runner> run   --base <sha> --tip <sha> --gates <names> --report <dir> # per gate { verdict, ms, log, failures: [{gate, unit, test, signature}] }
<runner> probe --at <sha> --failure <file> [--composed] --report <dir> # { result: pass | fail | cannot, signature }
```

An empty or unreadable report is never a pass (D192's rule). A repository with no runner has each named gate run whole as a step, its failure named
by the gate, a probe the gate run again. FABRIC-PIPELINE1d's runner contract for the `check` is the same contract with `run` alone: one contract,
two callers.

## 6. What becomes of `tools/merge-branch.mjs`

### 6.1 What Daoris takes, and where it lands (D212 §14.2)

| Logic | Today | Lands in |
|---|---|---|
| Requests, freezing, the preview | `merge-queue.mjs:122-145`; `merge-branch.mjs:6679-6740` | **Workplace** |
| The candidate, a member's merge, re-forming, promotion, recovery by ancestry | `merge-queue.mjs:182-256`; `merge-branch.mjs:6223-6258`, `:6494-6571` | **Workplace** |
| The mechanical resolvers | `merge-branch.mjs:1360-1644`, `:4657-4661`; the twins `BacklogMerge`, `GeneratedWriters` | **Workplace**, twinned with the tool while it lives |
| Contained, superseded, the declared scratch | none; `LineHolds` and `TreeScratch` reused | **Workplace** |
| The gates' run, the admission, the environment, the job | `merge-branch.mjs:3557-3580`, `:5111`, `:6400-6406`; HEAVYQUEUE1's `MachineAdmission` | **Review** (with `Sessions/Heavy/`, at mod-xvi) |
| The judge: signatures, the confirm run, base, prefixes, the outcome, memory | `merge-queue.mjs:261-394`; `tools/test-failures.mjs` | **Review**, its rules pure in `Review/Domain`, the failure reader a twin |
| Known red and sightings | `tools/ci-background.mjs`' findings | **Review** |
| The line's run, its executor off the pass, the `integrate` node | `merge-branch.mjs:6623-6673` (the detached runner) | **Workflows**, composed by **Loop** |
| The `integration.*` codes, `work.integrated` | the queue's log lines | **Events** |
| The rule's `integrate` | none | **Settings** |
| The steward's integration acts | none | **Roles**, with the shell's act tables |

### 6.2 What stays the repository's own

As this repository's declared runner (`--integrate-runner`, row j) and the parent's tools: the gate plan read from `daoris.gates.json` and the
release workflow, the lane table (`REACH`) and `daoris.lanes.json`, PROCREACH1's and the test map's censuses, the two lanes and their order
(GATELANES1, GATELANES2, GATEORDER1, GATEBUILD1, REHLANE1 to 2b), the FLAKE1 and LEFT1 re-runs, MEMGATE1's waits, class verdicts and standing
(GATE6), `--full`, `--full --beside`, `--ci` and its watch until FABRIC-PIPELINE1e runs the background check, the stage's evidence (`--passed`),
the commit check, and the prune of subagents' worktrees (GATE2). These read this repository's own layout, which the product must not carry.

The queue's verbs (`--preview`, `--message`, `--queue`, `--queue-run`, `--drop-batch <branch>`) retire at the flip (row k): each refuses where the
line's declaration says `by: daoris`, naming `daoris-driver integrate`.

### 6.3 The move, without a gap

1. **Rows a to c, in the tool** (today's queue, fixed in place): the composed environment, the confirm run and its policy, the two mechanical
   resolutions. The steward's day improves before any driver row lands.
2. **Rows d to h, in Daoris, dark**: the declaration read and accepted, the run over git, the gates and judge, the executor and node, the page.
   Nothing of this repository changes; the library and a test repository are their first users once row h lands.
3. **Row k's shadow**: for a day, Daoris plans every request the tool's queue takes (objects only: the merge tree, the resolution, the runner's
   plan), records what it would do beside the queue's outcome, and the steward reads where they differ.
4. **Row k's flip**: the tool's queue drained; one commit through it sets `integration.by: daoris` and the runner in `daoris.gates.json`; once it
   promotes, the tool refuses new requests (it reads the line's committed declaration), the owner accepts the blob and sets the install's rule
   for this repository to `merge` with `integrate` (row k), and the branches landed under the old rule are requested by the steward's act.
   Between the two, the tool integrates; after, Daoris; the committed line of the declaration is the one switch both read.

## 7. The scripts the development runs on

### 7.1 The day's scripts and their homes

| Script, or procedure | What it does today | Its home | When |
|---|---|---|---|
| `tools/merge-branch.mjs` (6,837 lines, 233 top-level functions by `grep -c '^(export )?(async )?function'`) with `tools/merge-queue.mjs` (638) | integration: the queue, candidates, gates, bisection, promotion; and this repository's gate plan, CI and stage evidence | **this design**: the queue into Daoris (§6.1), the plan as the declared runner (§6.2) | rows a to k |
| `tools/desktop-publish.mjs` (1,475) with `tools/service-publish.mjs` (155) | builds and publishes the desktop and service hosts; `--stage --from-gated` stages the install from the gate tree | **Daoris's own update step**: an `update` workflow of the install (build from a commit the background check passed, stage, install when idle, D139), the build the publisher's code run as its declared command | follow-up UPDATESTEP1 |
| `tools/family-rehearsal.mjs` (6,239) and `tools/deployment-rehearsal.mjs` (1,815), with `tools/release-rehearsal.mjs` (585), `tools/rehearsal-kit.mjs` (1,071) and `tools/setup-kit.mjs` (226) | prove the product end to end from outside it: the family's routing over real hosts; a published install driven | **stay harnesses**: a proof of the product must not run inside the product it proves, nor share its process, its home or its failure; *when* and *on which commit* they run, and their verdict, become the verification pipeline's `check` (D192, FABRIC-PIPELINE1d, 1g) | FABRIC-PIPELINE1g |
| `tools/usage-report.mjs` (1,483) | the machine log summarised for a session | **the product's usage view** (This Daoris), the machine log's readers in Events; the tool stays the terminal's read until then | follow-up USAGEVIEW1 |
| The steward polling the install's sessions in a loop | finds finished, failed and waiting sessions by asking again | EVENTHOOK1's push (`work/event`, the relay), EVENTHOOK1b, c and h | existing rows |
| The steward reading a session's handback out of its event log | finds what a session said it did | a **handback record** the session writes once, which the CLI and the MCP serve, and which RUNENDS1's end reads | follow-up HANDBACK1 |
| Hand-written briefs repeating the same proof clauses | each ask carries the same test rule, censuses and records steps | a workflow kind's standing instruction or a brief template per kind (D157's kinds), filled by the ask | follow-up BRIEFKIND1 |
| The steward coordinating its own subagents outside Daoris | its subagents work beside the install's sessions and meet only at the merge | an outside agent joining a line's channel (D188's channel) as a participant | follow-up CHANNELJOIN1 |
| Ending stuck runs and clearing its own environment by hand | ends a runner, unsets the home's variable | integration's owned cancellation (§5.6) and the composed gate environment (§5.5) | rows a, f |

UPDATESTEP1, USAGEVIEW1, HANDBACK1, BRIEFKIND1 and CHANNELJOIN1 are named follow-ups, each its own design or row after this one, none part of this
design's first rows.

### 7.2 Every other tool, by kind

Line counts by `wc -l` at `7ebeba1ba`; tests beside each are not listed.

| Kind | Tools (lines) | Keep or move |
|---|---|---|
| Integration and CI | `merge-branch.mjs` (6,837), `merge-queue.mjs` (638), `ci-background.mjs` (485), `test-failures.mjs` (354), `processes.mjs` (205), `as-merged.mjs` (176), `index-lock.mjs` (66) | the queue, the judge, the findings and the process-tree ending **move** (§6.1); `as-merged.mjs` stays (a gate's command), `index-lock.mjs` with the tool |
| Censuses and checks that read the repository | `process-reach.mjs` (906), `test-map.mjs` (701), `test-recall.mjs` (379), `doc-duplicates.mjs` (298), `doc-shapes.mjs` (265), `doc-links.mjs` (257), `optional-locks.mjs` (254), `live-logins.mjs` (166), `partials.mjs` (161), `doc-budgets.mjs` (142), `read-shares.mjs` (141), `doc-system.mjs` (139) | **keep** |
| Generators and git's drivers | `orient-index.mjs` (1,425), `merge-rows.mjs` (308), `move-files.mjs` (244), `release-prep.mjs` (181, the release workflow's), `stage-package.mjs` (55) | **keep** |
| Publishing and the install | `desktop-publish.mjs` (1,475), `service-publish.mjs` (155) | **move** to the update step (UPDATESTEP1) |
| Rehearsals and their kits | `family-rehearsal.mjs` (6,239), `deployment-rehearsal.mjs` (1,815), `rehearsal-kit.mjs` (1,071), `release-rehearsal.mjs` (585), `setup-kit.mjs` (226) | **keep as harnesses**; their scheduling moves (FABRIC-PIPELINE1g) |
| Instruments and measurements | `usage-report.mjs` (1,483), `desktop.mjs` (1,090), `knowledge-bench.mjs` (900), `orient-report.mjs` (715), `question-bench.mjs` (683), `ux-count.mjs` (665), `knowledge-server.mjs` (593), `testbed.mjs` (308), `subagent-usage.mjs` (211), `cdp.mjs` (205) | **keep**, but `usage-report.mjs` moves (USAGEVIEW1) |
| Probes and proofs (dated evidence, run by a person) | `chromium-probe.mjs` (390), `proof-kit.mjs` (321), `int4f-proof.mjs` (266), `acp2-proof.mjs` (260), `edge-probe.mjs` (237), `acp-trust-probe.mjs` (223), `perm2-proof.mjs` (221), `console2-probe.mjs` (213), `console3-probe.mjs` (137) | **keep** |
| Helpers | `fsx.mjs` (222), `listen-port.mjs` (47), `shell-edit-guard.mjs` (40) | **keep** |

## 8. Who acts

- **Daoris** (the home's lease holder) runs every step of §5.2 under the accepted declaration and the rule, and requests where `request: done`.
  It never pushes, publishes, rewrites the line or a pushed ref, removes by force, or runs a command its accepted blob does not name.
- **The steward** (D209), once STEWARDROLE1d activates the role: its new acts, each one route in one exact form with a record of its own naming the
  steward, overruled by the owner, refused for a repository whose landing pushes: `integrate request` (a unit, or a branch of a registered
  repository whose rule integrates, its own records branches among them), `drop` (a request, or a candidate, its job closed), `requeue` (a
  handed-back request at the same tip, once), `recheck` (a stopped line), `decide` (a candidate the policy held: `land`, `split` or `hand back`),
  `order` (a request first or last). Each narrows, is the development's own run, or is undone by a press of the owner's. This amends D209 point 3:
  integrating into a local line under an accepted declaration is no longer among *landings* there, which keep meaning a push or publication and
  every acceptance of what is delivered (closing a quest as done stays the owner's).
- **The owner** is still asked: to accept each repository's integration declaration by its blob, and every later blob, since it runs commands
  unasked (a widening, D209 point 3); to set a rule's `request: done` and `unconfirmed: land`, each lowering the owner's part (D192's lowering
  table gains both); for every push, publication and history rewrite (D191 point 3); for a removal by force; and for what a hold names as theirs.
  The owner is no longer asked for a press per integration where `request` is `done`, nor to unblock a batch an unconfirmed failure held.

## 9. Rows that touch the same files

**The rule, stated:** two rows that change the same files are never built from one base at once. Daoris holds it where it can see it, and says
it where it cannot:
1. **Declared paths.** An ask or a quest may carry `paths` (globs), as a backlog row names its `file:line`. At a start the planner compares a
   quest's paths with every open quest's of the repository, declared or already changed on its branch against the line; an overlap is said on
   both (`overlaps <quest>: <paths>`), and under the rule's `overlap: wait` (absent: `warn`) the later start waits on the earlier unit's
   `integrate` node through a `wait.unit` wire (the fabric events design §E6).
2. **Observed overlap.** Two running units whose branches change one path are each told once (an ordinary word), and under `order: apart` the
   later is formed after the earlier (§4.4), so it meets the earlier's work at its own replay rather than at a shared bisection.
3. **The steward's own publishing** keeps DOGFOOD1's practice, now the planner's where paths are declared: same-file rows are published one
   after the other.

No route reads a model's judgement of which files a row will touch (D192 point 1).

## 10. Rejected

- **Keeping the tool as the integrator, fixed in place.** Rows a to c do fix it, as stepping stones; kept as the end it leaves the procedure
  outside the product, unseen by the page and inoperable by the role (§3).
- **Daoris carrying this repository's gate plan.** Lanes, reach and the Process census are the repository's knowledge of itself; the runner
  contract keeps them its own.
- **A pipeline marked `integration`** (D192's mark). Its steps are fixed and ordered by safety (replay before gates before promotion), the
  person's choices are options, and a version per change of options would bind runs to graphs nobody edits. Reopened if a repository needs steps a
  plugin contributes inside integration.
- **A node per step on each unit.** A candidate is shared by several units; the steps are the line's run's, and the unit's node waits on it.
- **Promoting by a merge in the checkout** (today's merge form). It can half-apply in a checkout Daoris did not create; a fast-forward of a clean
  checkout, or an `update-ref` where none has the line out, moves only to a commit already gated.
- **Landing every unconfirmed failure, reached or not.** A member's own race would land with only a sighting; `reached` stays `split`.
- **Re-running a red candidate until it passes.** D207's rejection stands (`docs/decisions/D207.md:163`); one confirm run, bounded.
- **Confirming by the class alone** (today's prefix selection). A test polluted by another passes alone (`merge-branch.mjs:245-251`).
- **A wider window or a longer timeout.** No fix (`held-clocks.md`); the sightings feed the held-clock rows.
- **Daoris resolving a conflict of meaning, or a strategy option.** D87's rejection stands (`docs/decisions/D87.md:104-107`); a landing agent is a
  session that resolves and is gated like any member.
- **`request: done` with no gates.** A merge with neither a press nor a check is what D145 point 1 guards.
- **`autoAccept` as the merge rule's switch.** An older build skips a merge rule carrying it (§4.6).
- **The whole declaration in `driver.json`.** The repository's own facts belong with its files, reviewed there.
- **The whole declaration in the repository.** A session could lower the install's trust choices for its own landing; a declaration a session
  edits and has obeyed is an agent granting itself permission (`development-documents.md`).
- **Two integrators during the move.** Two promoters of one line; `integration.by` names one.
- **Reverting a promoted candidate the background run finds red.** D192's rejection of an automatic revert stands; it is filed and fixed forward.
- **Moving the rehearsals into the product** (§7.1).
- **Detecting same-file rows by a model's reading** (§9).

## 11. The rows, in dependency order

⚑ marks a row that can run beside FOUNDATION1 without touching its files. Each row is its own branch, lands through the integrator of its day, and
names its proof.

- **INTEGRATE1a ⚑ — the queue's gates start clean (QUEUEENV1).** Tools only, after HEAVYQUEUE1 lands (one file). The runner is started, and every
  gate, probe, re-run and rehearsal starts, with §5.5's composition; the runner finds the admission itself. Proof: a tool test that starts the
  runner with `DAORIS_HOME`, a role variable and `GIT_DIR` set and a stub gate printing its environment, red before, green after, the admission
  still found.
- **INTEGRATE1b ⚑ — a failure is confirmed before it is blamed.** Tools, and one additive field in HEAVYQUEUE1's two admission twins (an entry
  that takes every place) with their shared table. The confirm run, sightings, the policy read from `daoris.gates.json`'s `integration.unconfirmed`
  until row d moves it to the rule, its default `split` for both. Proof: tool tests (a stub gate failing once beside a held load and passing
  quietly promotes with its sighting under `land`; one failing twice is bisected; a polluted pair fails again as composed; a reached unit splits);
  then a replay of §2's 29 recorded runs under the policy, reporting which would have promoted, read by the steward before the default flips to
  `unreached: land`. Its driver half edits `Sessions/Heavy/MachineAdmission.cs`, which FOUNDATION1's mod-xvi moves to Review: beside FOUNDATION1
  only while no mod-xvi branch is open.
- **INTEGRATE1c ⚑ — mechanical resolution completed.** The tool and the driver's twins: a contained member recorded as contained; declared row
  tables and line rules (`integration.merge`) in `mergeByBase`'s and `BacklogMerge`'s twin, one fixture table; union joins set apart in the
  driver's replay. Proof: the table in both; §2's two set-asides as fixtures, landed. The driver half touches `Workplace/`, moved at mod-x:
  beside FOUNDATION1 while no open branch holds those files.
- **INTEGRATE1d ⚑ — the declaration, read and accepted, nothing acting on it.** `daoris.gates.json`'s `integration` (tool and driver twins), the
  rule's `integrate` (`LandingKey` and the CLI's `driverconfig.ts`, one table), its acceptance by blob, Setup's rows shown, refusals. Proof: the
  twins' table; an older build's write keeping the key; `request: done` refused without gates.
- **INTEGRATE1e — the integration run over git, unwired** (Workplace). The store family; request, form, replay, resolve, conflict as data,
  promote, settle, tidy with the declared scratch (TIDYOUTPUT1 folded in), recovery by ancestry; gates behind a verdict seam. Proof: a Git Process
  class per step, each crash point (after the line moved, before its record), each promotion path, the ref transactions
  (`ref-removal-transactions.md`), §2's library cases (a status line by `run`, a superseded commit, a tree whose verify output is declared scratch).
  After FOUNDATION1's mod-xii; new files in Workplace's folder alone.
- **INTEGRATE1f — the gates and the judge in the driver** (Review). The runner contract and the direct run; the admission's places and the whole
  admission; §5.5's environment; the candidate's job; the failure reader's twin; the judge pure in `Review/Domain`, twinned with the tool's table.
  Proof: the twins' tables; a job closed ending a grandchild; a confirm run waiting for a session's heavy run. After mod-xvi.
- **INTEGRATE1g — the executor and the unit's node** (Workflows, Loop). The line's run off the pass (FABRIC-PIPELINE1d's executor), `integrate`
  in Current's graph and in decoded versions, the events, the merge form's press and done as requests (every merge rule read through integration,
  §4.2), conflicts to the session or a landing agent. Proof: LANDREBASE1's and LANDBACKLOG1's Process classes green through the new path with their
  outcomes unchanged; a merge rule with `request: done` integrating a done quest. After FABRIC-PIPELINE1d and mod-xviii.
- **INTEGRATE1h — the page and the terminal.** The Integration view and its slice, the head's line, *What needs you*, `daoris-driver integrate`,
  Ask Daoris's coverage, words and glossary. Proof: the slice's tests, the page's tests in both languages, the installed window.
- **INTEGRATE1i — the steward's integration acts** (Roles and the shell's act tables), amending D209 point 3. After STEWARDROLE1d.
- **INTEGRATE1j ⚑ — this repository's runner.** `tools/merge-branch.mjs --integrate-runner` answering §5.9 over today's plan, shared with
  FABRIC-PIPELINE1g. Proof: the contract's fixture run against the tool; a probe as composed reproducing a polluted pair. After row d.
- **INTEGRATE1k — this repository moves.** The shadow day, then the flip (§6.3); `docs/development.md`'s integration section rewritten. Proof: a
  day's shadow with its differences read; the first promoted candidate on the install; the tool's queue refusing.
- **INTEGRATE1l — rows that touch the same files** (§9). Proof: an overlapping start held and said; two running units told once each.
- **INTEGRATE1m — another repository on integration.** A quest published to the library repository asking for its own `integration` declaration
  (its gates, scratch and line rules), never written from here; the install's rule for it set by the owner.

Follow-ups named in §7.1, outside these rows: UPDATESTEP1, USAGEVIEW1, HANDBACK1, BRIEFKIND1, CHANNELJOIN1.

## 12. Questions for the readers

1. **Is the confirm run enough** to tell load from a member's defect? A pollution fails again as composed; a race a member introduced may not.
   Should `reached: split` be `hold` instead, so the steward reads each?
2. **Should every merge rule go through integration** (one implementation, §4.2's last row), or stay today's press-merge until the owner sets
   `integrate` on it?
3. **The landing agent**: a working role worth having, or should a conflict whose session cannot go on always hold for the steward?
4. **The two homes** (§4.1): is the split right, and should `unconfirmed` be the repository's (it knows its flaky suites) rather than the install's?
5. **A line rule's `by: line`** (the line's copy kept): ever safe, or only `run`?
6. **The integration tree**: one per line kept warm (as `local/scope-tree`), or per candidate; how it meets KEEPSIZE1's allowance and TREEPOOL1.
7. **A built-in run** rather than a pipeline marked `integration` (§10): which do the readers take?
8. **Superseded** only asked, never applied by itself: enough for the library's case?
9. **One runner contract** for integration and D192's `check`: does the `probe` verb belong in D192's too?

## 13. What stays unmeasured, and what this design's checks do not cover

It is proposed and a document: the documentation gates prove its links, routing and shape, not its claims. Every `file:line` of §1 was read at
`7ebeba1ba`, and the in-flight branches by `git show` at their tips of 2026-10-28; nothing was built or run but those reads, the decision index's
generator and the document checks. §2's numbers were counted from the queue's records under `local/` by a reading script, never written there; the
steward's accounts in §2 (the five hours, the eleven waiting, the census miss, the library's day) are the brief's and were not read from a record.
Unmeasured, each the named row's to measure: how many of §2's unresolved failures a confirm run under the whole admission would pass (row b), how
long the whole admission waits beside driven sessions (row b), whether a member can make an out-of-reach unit flaky without its failure recurring
(row b's replay can show only what recurred later), the integration tree's disk (row e), and the landing agent's worth (row g). Not read: the
library repository's own files (another repository, described only from the brief and TIDYOUTPUT1), the install's live home, the steward's own
procedures (described from the brief), and the integration reviews of 2026-10-05 and 2026-10-10 past their router rows.

## 14. Reads

| Read | By | Findings | Blocking | The steward's verdict |
|---|---|---|---|---|
| Round 1 | Claude Fable | — | — | — |
| Round 1 | Codex | — | — | — |
