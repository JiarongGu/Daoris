# Canon changelog

**Why each version of the doctrine changed.** `daoris status` prints the entries between a repository's
locked version and the version shipping in the package, so a consumer sees not only *which* documents
moved but whether the change matters to them.

Write each entry under `## Unreleased`; the release workflow stamps that heading with the version,
and nobody stamps it by hand. Newest version first. Say what changed and what an
adopting repository should do about it — a line that only repeats the filename adds nothing that the
`changed` list did not already say. This file ships inside the package, so nothing here touches the
network.

## Unreleased

- **`development-documents`, `doc-loader` and `set-up-documents` teach an index of where things are.**
  One folder, generated and committed: by what a session looks for (an entry point, a command, a key, a
  fixture), each row naming a file and its lines; an outline of each file too long to read whole; and a
  digest of the decisions record. `development-documents` gains the `index` role and a section saying
  how it is kept: a generator in the repository's own toolchain, its check in the command that means
  done, a conflict resolved by writing it again, and every index the repository already keeps left
  where it is. `doc-loader` gains a step: where the brief names an index, every search of the code
  starts at its row and reads the lines it names. `set-up-documents` gains a step to make one, a clause
  in its close, `templates/index.md`, and an `index` row in the brief template's *Where things are*.
  None of it needs a doctrine tool, to write or to read. Drawn from one repository where a third of each
  session's calls came before its first edit, spent finding its way, and from a second that met the same
  need with indexes kept by hand. **Adopting repositories:** run `daoris sync`; the always-loaded region
  does not change. To keep an index, run `set-up-documents`' new step; one you already keep stays as it
  is.
- **`file-tool-discipline` says what a harness with no dedicated file tools does.** Its shell's
  read-only commands are its read tools, since a read changes nothing; the rest of the rule still
  binds: no edit scripted through another language's escaping, no deletion by computed offsets, no side
  channel past an approval. Drawn from an agent harness that offers only a shell: asked for a read-only
  review, it read the rule as forbidding every read it could make, and stopped before opening a file.
  **Adopting repositories:** run `daoris sync`; nothing else. The always-loaded region grows by 364
  bytes, the rule's three new lines and its longer row in the region's table.
- **`autonomous-development` says how often to verify.** Run the checks a change can reach while working,
  the whole set once where the work leaves the run, and after a fix only what failed; when the full set
  grows slow, measure the slowest check and make it faster rather than skipping it. An agent that re-ran
  every suite each round spent time and account allowance on checks the change could not fail. Nothing to
  do but read it: if your repository's own documents tell an agent to run everything on every step, this
  is the line to point them at.
- **The doctrine region no longer lists knowledge and skills.** They are in `<target>/INDEX.md`, which
  `sync` writes and `check` keeps true, and the region points to it. The always-loaded region no longer
  grows with your own documents, and the shared rules sit thousands of bytes higher in `AGENTS.md`.
  Drawn from the first real set-up, whose 169 knowledge documents made the region 53 KB and put the
  first rule past the 32,768 bytes one widely used agent reads of that file. Three canon documents
  change with it:
  - **`doc-loader`**, step 2, reads the index the region names: whole when it is a few dozen rows, and
    when it is longer, searched more than once (the task's words, their synonyms, and the folders and
    parts the task touches). Where a search over the repository's knowledge is connected it is asked
    too, since it matches by meaning, and where none is, the index searches are the whole step. A
    search that finds nothing has not shown that nothing applies.
  - **`skills-workflow`** sends a session to its agent's own skill list first, or to the generated
    index, since every agent lists its skills and the region no longer does.
  - **`development-documents`**: an on-demand document is named by something always read, or by the
    index that lists it, since a list that grows with the repository is read on demand too. And a
    knowledge document is named by its subject and says when it applies in its first lines, which is
    how a search finds it where no index is read.

  **Adopting repositories:** run `daoris sync`. A file of your own at `<target>/INDEX.md` is named as a
  collision; rename it. Until you sync, `check` names the index as absent and the region's pointer as
  out of date. The region drops by the size of the two tables, less the 24 bytes `skills-workflow`
  gains: 4,455 bytes in all in the repository this changelog ships from.
- **`task-lifecycle`**, **`development-documents`** and **`set-up-documents`** say what a session pays
  for: what it reads, on every step after it reads it, so what is read whole stays short by how each
  entry is written, and each fact has one home that every other place names in a line. The rule's
  backlog row is what and why in two sentences, its contract and its proof; its archive outcome is a
  line or three, saying what changed and where the detail lives, where it had said one line while the
  skill's template asked for a paragraph. The knowledge document gains the failure (a record written
  twice, and paid for on every step), the shapes of a backlog row, an archive entry, a router row and a
  decision's amendment, *look up by identifier*, and the shapes' sizes beside the ceilings: 60 words
  for a row and for an outcome, reported like every ceiling. The template asks for the short outcome,
  and the skill's step 4 moves a long row's excess to the record that holds it. Drawn from a repository
  whose backlog was trimmed twice and grew back both times, and whose archive outcomes retold, in other
  words, three quarters of the facts already in the decisions they cited.
  **Adopting repositories:** nothing to rewrite. Old entries stand as written; new ones take the shape.
  The always-loaded region grows by about 460 bytes, the rule's new sentences and the knowledge
  document's longer index row.
- **`autonomous-development`** gains *Look before you ask*: a question the task's own material, the
  repository's documents, code and history, or a neighbour's documents can settle is the agent's to
  settle, with its evidence kept for the hand-over; a reading the evidence leans to is taken and stated at
  the checkpoint; only what no source holds and only the person can give (a sign-in, a go-ahead for an act
  outside the repository or on a live system, a preference nothing records) reaches them mid-run. Drawn
  from a driven session that stopped to ask what its own repository's notes and code could answer. Nothing
  to do in an adopting repository beyond `sync`; the always-loaded region is unchanged.
- **`development-documents`** (new core knowledge) and **`set-up-documents`** (new core skill) — a
  standard for the documents a repository keeps so a code-generating session can work without stopping
  to ask or search. Every document has a **role** (brief, room, knowledge, skill, router, decisions,
  backlog, archive, fixes, changelog, glossary, gates) with one job and one way it is read: **always**
  (the brief, beside the doctrine in the root instruction file), **on demand** (rooms, knowledge,
  skills, the router), or **by lookup** (the records). A fact goes in the brief only if nearly every
  task needs it and the code cannot tell a reader. A document read whole has a ceiling, reported and
  never failed: bytes for the root file as a whole, because at least one widely used agent cuts that
  file at 32,768 bytes and the tail it loses is the doctrine; words for attention. The brief names
  where each record is. What a session may run without asking is a declaration a tool reads and a
  person accepts once, never a sentence, because an instruction file shapes what an agent tries and
  not what its harness allows. The skill carries the procedure and templates for the brief, a room, a
  knowledge document, the router, a decision, a backlog row and an archive entry. Knowledge and a skill
  rather than a rule: the part every task needs is where the records are, which the brief carries.
  Drawn from the agents' makers' own guidance, which converges on the same file, the same content test
  and the same split between what is read whole and what is looked up.
  **Adopting repositories:** nothing changes on its own. The always-loaded cost is two index rows. Run
  the skill when you set the repository up for agents, or when a session could not find where
  something is written. When you copy a template, leave its provenance line behind.
- **`localized-ui`** (new pack) — for a repository whose interface ships in more than one language.
  One on-demand document, `translation-parity`: every catalogue holds the same keys **both ways**,
  keys are structural rather than the default language's text, and the three kinds of text that reach
  a person — chrome, stored content, and a message whose exact wording *is* the contract — belong in
  a catalogue in exactly one case. Derived independently by two repositories in this family, both of
  which found their gaps by counting keys rather than by looking.
  **Adopting repositories:** take this pack only if you ship a second language. It is deliberately
  **not** core — a rule every repository loads on every task, to govern a concern only some of them
  have, is what the pack tier exists to prevent (`docs/DECISIONS.md` D61).
- **`claims-need-checks`** (core knowledge, extended) — the *"runner quietly saw fewer inputs"* shape
  gains its harder half: **the count that never rose.** A file-matching pattern written for a flat
  layout stops reaching once the code grows a subdirectory, and the files it now excludes have no
  earlier count to fall from — so the suite is green, the count is unchanged, and nothing reports
  that a whole directory is unchecked. Found in this repository: two globs, one sabotage-tested for
  reach and one not, and the untested one had silently stopped covering the new code.
  **Adopting repositories:** proving one pattern's reach proves nothing about the next one. Each
  asserts its own scope, so each earns a real file placed where it is supposed to look.
- **`claims-need-checks`** (core knowledge, extended) — a **fifth** way a check passes without
  checking: *nothing runs it*. A configuration file declared gates that no build step, hook or
  workflow ever invoked, so the declaration read as coverage while nothing executed it — and the
  first hand-run failed immediately. Where a check lives matters as much as what it asserts: a
  correct check in a tool nobody calls is indistinguishable from no check, and worse, because the
  declaration looks like one. Found in this repository, by going to add a gate and discovering the
  gate runner was not wired to anything.
  **Adopting repositories:** before believing a new check, follow the path from the command people
  actually run to the code you just wrote.
- **`post-feature`** (skill, extended) — the audit gains a **prose pass**: after refreshing what the
  change made stale, read the writing it added against nine named shapes — the same rule in two
  places, history outside the record that holds it, status annotations that rot, a hand-restated
  catalogue, the path taken to the answer rather than the answer, rationale repeated beside each
  sibling, the paragraph carrying four rules, emphasis everywhere, and intent where the record should
  state fact. Documentation is the one surface with no compiler, so a wrong sentence outlives the
  behaviour it describes with every gate green — and is believed for exactly that long, because a
  confident sentence is what tells a reader not to go and look. A checklist rather than a principle
  because by the end of a long session the writing all looks necessary to whoever wrote it.
  Converged independently with another agent harness's documentation standard, which is the
  two-source bar for believing a rule rather than one project's taste.
  **Adopting repositories:** nothing to do — the skill's `description` is unchanged, so what is loaded
  unasked costs exactly what it did before, and the new section is read only when the skill is invoked.
- **`autonomous-development`** (new core knowledge) — development is automation-first: a person
  states the target and verifies the outcome; the steps between are executed by agents and verified
  by gates, not by per-step approval. Per-step interaction does not scale past a small system, and
  approval fatigue trains the reviewer to click through exactly when a real decision arrives. The
  carve-outs sit at the outward boundary: destructive or irreversible actions and anything that
  leaves the repository — push, publish, release, a write into a sibling — stay explicitly human,
  while a commit is part of the run, landing per task once gates are green so the history is the
  reviewable record. Knowledge rather than a rule for the same reason
  as `model-decoupling` — it applies when shaping how a task runs, not on every task — and set by the
  owner's direction for the whole family rather than derived from convergence, which is itself the
  model in action: the human set the target.
  **Adopting repositories:** read it once when wiring up an agent workflow; the operational half is
  that "done" means your own gates green plus a reviewable diff, and that mid-run questions batch to
  a checkpoint instead of interrupting.
- **`repository-owns-its-work`** (new core rule) — **never write into another repository.** Not its
  code, not its files, not its backlog. Publish the request and let whoever works there take it: a
  quest where a request system exists, a message to that repository's owner where none does. There is
  no remaining case where writing across is the answer, because the request is; a request to do it
  anyway is the user's call to make explicitly, not a judgement to reach alone.
  **Adopting repositories:** this rule names the only doctrine mechanism that needs a service running,
  and it names the alternative in the same breath on purpose — a contributor who does not use this
  tool, or whose agent does not, still has something to do when they read it.
- **`reaching-in`** (new core knowledge) — what happens when you do it anyway, kept because the failure
  was not the first edit but everything that followed. One file written into a sibling; a removal script
  that over-deleted 171 lines; a repair that restored from the last commit and destroyed an unstaged
  edit the sibling's own session was about to commit. Three writes, one piece of work lost, every step
  well-intentioned and each worse than the last.
  The lessons are about reasoning rather than tooling: **a tree-state observation expires the moment you
  look away** if anyone else is working there, **the repair is another outside edit** made with less
  information, and **never revert a file you do not own** — reverting is not an undo and silently
  discards uncommitted work you cannot see. If you have already written, stop and report it; the owner
  can undo it with context you do not have.
  The reason is not etiquette. An outside edit is made by whoever knows that codebase least — that is
  what being outside means — and it skips the review that repository would have applied. **The knowledge
  is not portable but the request is:** why a rule is worded as it is, what was tried and rejected, which
  constraint a file encodes, all of that stays with the repository, and a request carries the one thing
  that does travel — what is needed, and why.
  The mechanism is a **quest**, held by the knowledge service and *pulled* by the repository it is
  addressed to — never written into that repository's files, which would be the same trespass in a
  smaller form. Four states: open, taken, done, declined. Called a quest rather than a task or a request
  because every backlog here is already full of tasks, and a colliding word would be ambiguous in
  exactly the file where the distinction matters. It is *taken* rather than assigned, which is the
  property that keeps declining a real answer — and declining requires a reason, because a bare refusal
  gives the asker nothing to act on.
  **Only a repository that has adopted can be addressed**, because one without the client cannot see
  what was asked, and an unread quest looks exactly like an ignored one. *(Since widened: a
  repository registered with the service is addressable too, answered by a session that is handed
  its connector at start.)*
  Practice came before the rule: repositories here already kept "waiting on the sibling repository"
  sections, arrived at independently. What was missing was a name, a place, and a readable status.
  **Adopting repositories:** once adopted, siblings can address quests to you. Nothing is written into
  your tree — your own agent reads what is outstanding through the service and decides, including
  whether to copy it into your backlog. Declining is a real answer.
- **`claims-need-checks`** (new core knowledge) — verify behavioural prose against the implementation
  rather than the design; ship the check in the same change; say which claims the gate did not cover.
  **Two repositories in the family derived this independently and from opposite ends** — one auditing
  shipped API documentation against its own source, one finding a configuration field parsed and never
  read. Neither could have found the other by searching: at first draft the two shared **25% vocabulary**,
  well under the 30% duplicate threshold, which is D17's point demonstrated live. The canonical document
  merges both, and the merged version now matches the other at 49%.
  **Adopting repositories:** worth reading once, then grep your entry document and readme for *always,
  never, throws, cannot, defaults to, guaranteed, enforced, verified, ensures* and check each against the
  code. "Throws" is the costliest to get wrong, because it fails silently.
- **`leak-repair`** (new core knowledge) — how to repair a credential, machine path or private name that
  has already been committed. `sensitive-info` says a committed leak is a history problem and needs a
  rewrite; this is the deep dive that says how, and it is knowledge rather than a rule because it applies
  only when you actually have one. Assembled from **three** repositories' hard-won versions, including
  one written during a real purge. The traps that cost the most: **the backup bundle you take first is
  itself a complete copy of the leak**; the rewrite tool usually strips the remote, and tags need pushing
  separately; a clean scan deserves the same suspicion as a passing test, so plant a pattern you know is
  present and confirm it is found before trusting a clean result.
- **`web-webview`** (new pack) — hosting a web UI inside a native shell. `embedded-web-ui` carries the
  four invariants that fail *silently*: answer resource requests off the UI thread with a response object
  rather than materialized bytes, treat the browser object as thread-affine, publish anything served from
  disk atomically, and fail closed on initialization and health checks. `web-host-lifecycle` holds the
  surrounding detail — scheme registration that fails identically however you get it wrong, readiness
  gating on content rather than navigation, environment scoping, and the development inspector whose
  arguments are silently dropped once the host sets any of its own.
  Written from two applications that derived the same invariants from different symptoms — one profiling
  a frozen window, one debugging slow thumbnails. Validated by adoption: against a real repository's
  doctrine, `doctor` reports its own 21.6 KB hosting document as **64%** covered by the canonical pair.
- **`desktop-app`** (new pack) — verifying a real desktop application. `desktop-verification` carries
  the invariants: drive the running app rather than a mock, **synthetic input does not prove an
  interaction** (it bypasses hit testing, focus, z-order and pointer capture, which is where interaction
  bugs live), capture before and after, and confirm *which instance* you attached to — a stale bundle, an
  orphaned process or a second window with no debugging target each give a real answer about the wrong
  thing. `desktop-dev-loop` holds the loop: one long-lived instance, a randomized debugging port because
  a fixed one attaches to the wrong process rather than failing, and selectors that survive a rebuild.
  Three applications converged on this independently. **Not validated by adoption** — no repository has
  installed it yet, the same status as `durable-jobs`.
- **`durable-jobs`** (new pack) — long-running work that survives a restart. `durable-work` carries the
  invariants: dispatch rather than await, checkpoint so resume is cheap, bound capacity **per lane**
  rather than globally, and add a kind as a handler plus a registration. `job-system-design` holds the
  shape underneath — one consumer loop over a mailbox rather than a task per item, a container job that
  is bookkeeping and is never dispatched, and backing off from measured pressure rather than a guessed
  constant.
  Three applications in the family built one of these independently; **two use the same filename**, which
  is the strongest agreement the survey has found. The crash-loop guard is in it because one repository
  hit it and another had already recorded the same gap as an open risk before it happened to them.
  **Not yet validated by adoption** — no repository has installed it. Packs are opt-in, so an unused one
  costs nobody anything, but it is doctrine argued from evidence rather than proven in use, and the
  distinction is worth keeping.
- **`windows-machine`** (pack rule, extended) — two more traps that pass silently. PowerShell 5 unwraps a
  nested array of exactly one element, so a find-and-replace built from an array-of-pairs holding a single
  pair replaces one *letter* everywhere; two or more pairs behave, which is what hides it. And a working
  directory past the path-length limit fails as *corrupt input* — tools report they could not open a file,
  which sends you looking at the asset instead of the path.
- **`model-decoupling`** (new core knowledge) — specify a feature without naming a model; the deployment
  chooses one, every model-backed feature still does its useful part with no model at all, and the
  output says which tier answered. Knowledge rather than a rule because it applies when building an
  AI-backed feature, not on every task.
- The first canon: seven workflow rules, and five skills — `doc-loader` and `pattern-finder` to start a
  task, `post-feature` and `fix-log` to close one, `caveman` for terse output. Nothing to upgrade from
  yet.
