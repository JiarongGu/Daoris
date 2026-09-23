# The working surface — design

> Written 2026-09-21 (SURF1), from the owner's direction set in the backlog 2026-09-20: **the desktop
> half of Daoris is becoming a user-driven application, and it should hold code sessions the way a
> terminal agent CLI does — but across different agents, repositories and concurrent sessions, designed
> with real UI/UX.** The research is `docs/2026-09-20-working-surface-research.md` and was written
> first, deliberately; this is the contract it feeds. Two decisions carry it: **D51** settles the
> isolation model, **D52** settles the surface. Read with D46 (the driver) and D49 (the interactive
> surface) — this extends both without weakening either — and with D41's design language, which does
> not move.
>
> **Amended 2026-09-21 by D55.** The owner reset the positioning mid-build — *"the desktop is becoming
> more a dev ide (but code gen driven)"* — with the method note *"you should reference more existing
> application"*. A second study answered it (`docs/2026-09-21-ide-reference-study.md`: IDEs, not
> session managers) and **D55** carries the result. Everything below stands **except the container**:
> Work is a **second frame** beside Manage, not a sixth nav item, and it brings a status bar, a
> growable output panel and a mode switch with it. §3's rail, attended session, stream and timeline
> are unchanged in content — see §3b for the three places the study adds to them.

## 1. What it is for

The platform today is an **operations console**: five views for *scanning state*, built for one person
"using this daily **beside** a terminal" (D41). The direction moves the terminal's job inside. A
working surface optimises for something else entirely — *holding attention on one thing while several
others run* — and that sentence is where every choice below comes from.

Three things a person cannot do today, and they are the whole design:

- **Talk to an agent in a repository the driver is working in** — one session per repository, so the
  surface's central act is refused exactly when the driver is doing its job.
- **Have the driver start anything in the repository they are editing** — the clean-tree rule holds it,
  correctly and permanently, which under a working surface means the repository the person cares about
  most is the one Daoris can never help with.
- **See what a session did.** Evidence is a commit list rendered as a string. Review — the third of the
  field's four concerns (research §2) — has no surface at all.

The first two are one problem wearing two hats, and §2 settles it. The third is §5.

## 2. The isolation model: the tree is the unit of exclusion, and a repository may have more than one

**Settled first, because it is not a layout question** (research §4). Recorded as **D51**.

**What was always true, and what was only incidentally true.** "One active session per repository" is
two claims welded together. The first — *two agents in one working tree corrupt each other's git
state* — is the reason, it is permanent, and nothing here touches it. The second — *a repository has
one working tree* — is not a fact about git at all. It is a fact about how the registry was built:
`Registration.Root` holds one path because `connect` runs in one checkout. Git has had linked
worktrees as a first-class feature for a decade.

So the lock keys on the **tree**, and a repository may have more than one. The refusal keeps its
sentence and gains precision: what holds you is a tree, named, not a repository.

**What this does not license.** D46 §9's argument survives untouched: *wanting parallelism within one
domain is a reason to split the domain, not the tree.* The planner keeps its rule — **one driven
session per repository, oldest open quest first** — because pacing a domain and preventing corruption
are different jobs with different reasons, and a mechanism shared between them would silently make one
answer the other. What a second tree buys is **the person and the driver coexisting in one
repository**, and a person is not a second workstream; they are the operator. If real use later wants
several driven sessions in one repository, that is a change to the planner's rule, argued on evidence,
and the ledger will not need to move.

**The rules, each with its reason.**

1. **A repository has exactly one registered root — its main tree.** It is what `connect` registered,
   what the index reads, what the driver stamps provenance from, and the only thing that ever feeds
   knowledge (WSP4/D48 §6). **A session tree feeds nothing and is never registered**: it is not a
   repository, it is a place to work. This keeps WSP4 whole rather than teaching it about trees — a
   session branch is not the canonical line, and WSP4 already refuses to feed from one.
2. **Daoris owns the location; git owns the contents** — `~/.daoris/trees/<workspace>/<repository>/`,
   the same arrangement as a credential profile (D49 §4), for the same reason: a directory Daoris
   created is a directory Daoris may reason about, and one it scattered beside somebody's checkout is
   not. The branch is named for the session, never reused, and based on the repository's canonical
   line as WSP4 already resolves it — falling back to the root's `HEAD` where no line is declared, and
   **saying which it used**.
3. **A session tree exists only on request, per repository.** Silence means what happens today, byte
   for byte: sessions run in the registered root. The same rule as the profiles (D49 §4) — an additive
   feature or a trap, and there is no third option.
4. **A fresh tree holds nothing git does not track** — no installed dependencies, no build outputs, no
   untracked local configuration. That is the real price of this decision, it is paid per tree by
   whatever the repository's own setup costs, and it is stated **in the sentence that creates the
   tree** rather than discovered by a session whose gates cannot run.
5. **The clean-tree rule stays** on the registered root, and is vacuous in a fresh tree. That is the
   point: the person's work in flight stops holding the driver without a session ever being entangled
   with it — which is the lesson `reaching-in` was written from, honoured rather than traded away.
6. **Nothing merges itself.** A session tree's commits reach the canonical line when the person merges
   them (§5). A merge is local and reversible, so D37 would permit automating it; it stays a press
   because it is *where the person's verification lands*, and D37 keeps that human on purpose.
7. **Nothing deletes itself.** A tree with uncommitted changes or unmerged commits is never removed —
   the removal refuses, naming what would be lost, and the person may say it again meaning it.
   Destroying work is never a side effect of tidying.
8. **`connect` from a linked worktree is refused, naming the main one.** A registration re-pointed at
   an ephemeral tree is the worst failure available here: everything keeps working until the tree is
   removed, and then the repository's root is a path that does not exist. Git can answer the question
   directly (`--git-common-dir` against `--git-dir`), so this is a check, not a heuristic.
9. **A tree path is machine-local material** and inherits the transcript's guards exactly (D47 §4):
   stripped for a non-loopback caller, absent from the feed's shape, written as a literal NULL by the
   store's mirror. Three guards for one rule, because a path leaks through whichever half somebody
   forgot — the reasoning SES3 already paid for with the profile name.

**Rejected: keeping the repository as the unit.** It is coherent, it is free, and the refusal it
produces is already honest. It was rejected because of what it permanently caps: the working surface's
central act refuses whenever the driver is working; the repository being actively edited is
undrivable forever; and "concurrent sessions" collapses into "concurrent repositories", which D46
shipped a year's worth of design ago. Nothing is deployed (the standing redesign grant), and the unit
of exclusion is the single worst thing in this system to retrofit once records exist — the same "draw
it deliberately rather than around data" argument D48 made for the sharing boundary.

**Rejected: a container per session.** The field's other answer. It isolates the toolchain as well as
the filesystem, which is more than this problem needs and more than this family can carry: every
harness, every credential profile (D49 §4) and every repository's own gates would have to exist inside
an image somebody maintains. Git's mechanism composes with what is already here; an image replaces it.

**Rejected: one directory, switching branches.** Stash-and-checkout serialises precisely what needs to
run at once, and it destroys the person's working state on every switch. It is the problem restated.

## 3. The shape: Work is a view, not a second application

**One app, one language.** The console's five views are not a lesser product to be subsumed — they
answer questions a working surface does not ("who owns this", "where have we learned this twice"). So
**Work joins them as a sixth item**, and the shell, the tokens and the validated status palette stay
exactly as D41 settled them. A second visual language inside one app is the thing D41 exists to
prevent, and inverting the shell around sessions would produce one within a week.

**The layout.** Work is the one view that breaks the 72rem content cap: that cap is for *reading*, and
this surface is for *watching*. A sessions rail on the left (~18rem, the sidebar's sibling rather than
a second sidebar), and the attended session filling everything else.

*Amended 2026-09-21 (owner): the layout takes deepseek-harness's structure* (MIT; components doc §3a
carries the whole adoption). The frame is **three columns** — the rail, the attended session with a
protected 400px floor, and a **right dock keyed to the attended session** where the timeline and the
diff live, opening at 45% and conceding before the center does, closing deterministically rather than
reopening on resize. The stream keeps their tail rule: follow until the person scrolls up. The rail
keeps its ~18rem width and gains their collapse-to-rail behaviour. Nothing about D41's language moves
with it — structure and geometry transfer, pixels do not.

**The rail: sessions, grouped by repository.** Session identity is the highest-leverage detail in the
research and tab overload is the named anti-pattern, so identity is carried by **what a session is
for**, derived and never invented: `repository · kind · the quest's title or the conversation's first
line · state · age`. Grouping is by repository because that is the axis a person actually switches on,
and the group header is where the repository's own facts live — drivable, held, which tree is busy.
**Naming a session by hand is deliberately not added** until two real sessions cannot be told apart;
the derived title is free and true, and a rename is a store column plus a surface that has to earn it.

**The attended session has three parts and a composer.**

- **The head** — the record: state pill, repository, tree, the quest it serves, the tool and the
  account it ran as (the record already carries both, D49 §4), and its age. When a session is parked,
  the head is where its analysis sits (§4).
- **The stream** — the console (SES1), promoted out of the drawer to first-class, verbatim in
  monospace, with its drop counter unchanged: a window that silently skipped the middle of a build log
  would be a worse lie than one that showed nothing.
- **The timeline** — the observed audit layer beside the stream, never inside it: state transitions
  with their times, quest transitions, and commits as they land.
- **The composer**, for a chat: the input box SES2 built, with its two endings kept distinct —
  *finish* closes stdin and lets the harness wind up (`completed`), *stop* is the person's interrupt
  (`stopped`).

**Rejected: parsing the stream into steps.** The research's activity panel — "3 of 7 steps", with
progressive disclosure — assumes structured progress events. Daoris has none, and the only way to get
them is to parse another program's stdout, which turns the adapter seam into a screen-scraper that
breaks the first time a harness rewords a line. D23 and D24 exist to prevent exactly that coupling.
What Daoris *does* have is observed and true: process lifetime, quest transitions, the tool and the
account, and what landed in git. That is the audit layer, and it is honest.

**One home for the stream.** The console is in the quest drawer today and the chat is started from
Projects — two views holding pieces of one thing. Quests keeps the record summary and gains a door
into Work; Projects keeps the registry's own controls. Starting a session — repository, harness,
profile, and whether it opens its own tree — belongs in Work, where the result appears.

**The shell remembers the last view**, because a working surface is a place someone returns to, and
re-landing on a management screen every launch is a toll. Attention is carried by the sidebar's counts
instead (§4), which is what makes "what needs me" answerable from wherever the person actually is.

## 3b. What the IDE study adds to §3 (D55, 2026-09-21)

§3's title — *"Work is a view, not a second application"* — was half right and is now precise: Work
is not a second application, and it is **not a view either**. It is the second **frame** of one
application. Three additions, each from a named reference in
`docs/2026-09-21-ide-reference-study.md`:

- **The frame gains a status bar and a panel.** Ambient state (`driver: running · 2 sessions ·
  workspace: default`) has nowhere to live today and must be true without being looked at — VS Code's
  status bar. The stream stops being a fixed-height well inside a box and becomes a **panel you can
  grow, shrink and hide** — the one arrangement a well inside a card can never offer, and the reason
  output lives in a panel in every workbench ever shipped.
- **A session row carries two more facts, both already in the record.** **Where it runs** — Daoris is
  multi-machine by construction (D47) and a row that says nothing about which machine holds a session
  is a gap the remote opened and nobody filled — and **elapsed**, because "moved 4m ago" reads the
  same for a session three minutes old and one three hours deep.
- **"Own tree" becomes a control on the session**, not only the per-repository toggle the Machine
  view has. That is how the need arrives — you want a *second* session in a repository that already
  has one — and it is the shape every reference uses (Cursor's "move an agent into a worktree").

And one correction to §5: review is a **multibuffer**, one scrollable aggregation of every changed
file with a per-file *viewed* mark, not a file tree beside a diff pane. The verbs stay Daoris's —
**accept**, or **send it back as a quest** — because the session already committed and reaching in to
fix what you are reviewing is the thing D32 forbids.

## 4. Attention: nobody should have to watch

Continuous monitoring is the reported source of fatigue, and no-visibility sessions showed **3× the
abandonment at identical output quality** (research §2). So visibility is load-bearing and watching is
not the mechanism.

- **Overview keeps the landing** (D40 — "is anything sitting" is still the first question) and gains
  one band: **what needs you.** Parked sessions first, then finished-and-unreviewed work, then quests
  sitting that nobody can take. Every row is a door. *Amended 2026-09-24 (INT4d):* an ask waiting
  on a person comes after the parked sessions and before the quests (intake design §1h).
- **`AwaitingPerson` gets a surface at last.** It has meant "only the person can clear this" since
  D46 and has never been rendered anywhere. In Work it wears the warn treatment in the rail, its
  analysis sits at the top of the head — options, recommendation, reason, as `autonomous-development`
  requires, never a bare "may I?" — and the person's moves are exactly the three the ledger already
  allows from that state (`completed`, `declined`, `stopped`, each with a note). **No new states**: a
  surface that invented one would be a second lifecycle to keep in step with the first.
- **The sidebar carries two counts**: sessions live, and how many need a person. The second one is the
  only badge that wears a status hue, because it is the only one that is a status.
- **An OS notification on park and on end** — and never for an ending the person caused, because a
  toast telling you what you just pressed is how people learn to dismiss toasts unread. Per machine,
  off in one click, and off by default for nothing. This **closes driver design open question 5**: the
  runtime ships no toast API and deliberately never learns what an operation is, so this is the
  shell's own code over the window toolkit it already has.

## 5. Review: the diff is the missing piece

Evidence today is `commits landed:` plus `git log --oneline`, stored as a string. It says that work
happened and nothing about what it was.

- **The diff is computed where the tree is, and rides the bridge.** Desktop-only, structurally, for
  the same reason as the console (D47 §4): it is machine-local material, and a repository that never
  opted its knowledge into a remote has certainly not opted its source into one. A teammate sees the
  evidence string, exactly as today — the record is what syncs, and this is not the record.
- **Per session**: the files touched, the patch, and the base it is measured from — the driver already
  records `HEAD` before spawning, so the range is a fact rather than a guess. For a session running in
  its own tree, whether it is merged into the canonical line.
- **The bound is stated, never hidden** — the console's rule, applied again. A diff has no upper size,
  so the surface renders to a limit and says what it truncated; git on disk has the rest.
- **Two acts, both the person's**: *merge into the canonical line*, and *discard the tree*. Merge is a
  press (§2, rule 6). Discard is destructive: it confirms, it names what will be lost, and it refuses
  where work would vanish unasked (§2, rule 7).
- **Not in scope**: line comments, review threads, hunk-by-hunk approval. The reviewable record is the
  repository's own history (`autonomous-development`); this exists so the person can *see* it without
  leaving, not so Daoris can become a code-review product.

## 6. How much terminal is actually wanted

**A transcript with an input box — what SES2 built — and no PTY.** A real pseudo-terminal means
terminal emulation: ANSI sequences, resize protocols, the alternate screen, and a rendering surface
that must stay faithful to each harness's redraws. That is a large permanent commitment made to
recover an affordance that already has a better door: **the person's own terminal on the same
machine**, where `daoris-driver chat` hands the harness the real thing outright — precisely as
`daoris agent login` does, and for the same reason (D49 §4: capturing an interactive flow to
pretty-print it turns a working one into a hung one).

Held as an open question with a real trigger: a supported harness whose interactive output proves
unreadable over a pipe in actual use. Evidence decides, not the field's fashion.

## 7. What holds when the window is closed

The driver runs headless and must keep doing so — the surface is a **view over records plus live
streams**, never the place either lives.

- After a restart the records are the service's and the transcripts are on disk; the ring buffers are
  gone, and the window says so with the drop counter it already has.
- A machine running only `daoris-driver` notifies nobody, deliberately — there is no screen to notify.
  Its records are read when a surface next opens, and the terminal is the other door (D50): a
  `sessions` listing and the moves that clear a parked session belong to `daoris-driver`, on the
  machine that holds them, exactly as `chat` does. The `daoris` CLI's offline shape does not change.

## 8. Boundaries that do not move

- **D38 — one UI.** Work is one view whose *live* half is structurally desktop-only: the records and
  the session list are service state and render in a browser; the stream, the composer, the diff and
  the tree paths ride the bridge and are absent there. Same argument the console and chat already made.
- **D31 — doctrine stays unwritable** from every surface, including this one.
- **D37 — a better approval surface must not widen autonomy.** Everything here is *seeing* and *the
  person acting*. Nothing in it lets a session do something a session could not do before: no
  auto-merge, no auto-push, no progressive delegation that widens as approvals accumulate. The
  research names that last pattern; it is declined here for the reason D37 was written — approval
  fatigue trains the reviewer, and a surface that learns from a trained reviewer learns the wrong
  thing.
- **D24 — no model is named.** The harness carries the model and the conversation; the surface reports
  what the record says and configures nothing.
- **D41's design language** — the tokens, the validated status palette, the accessibility rules, the
  motion budget. The shape of one view is open; the language is not.

## 9. What lives where (the D49 §5 table, extended)

| The service (passive; model-free; spawn-free) | The driver / desktop |
|---|---|
| Session records — now naming the tree they ran in, stripped off-machine like the transcript | Session trees: creation, listing, removal, and the refusals that protect them |
| Quests and evidence, unchanged — a teammate reads the same record | The diff, computed by git where the tree is, over IPC only |
| Nothing about attention — a record's state is the fact | Notifications, and the surface that renders attention |

## 10. How it is verified

The same discipline: **driven, not asserted, no model in the gate.**

- **The family rehearsal** grows a trees phase: a session tree created and spawned into; the lock
  refusing a second session in the *same* tree while allowing one in the other; a driven session
  starting while the registered root is deliberately dirty; `connect` refused from a linked worktree
  naming the main one; a removal refused with work in the tree and the work still there afterwards;
  and the remote store scanned for tree paths, beside the profile names it already scans for.
- **The vitest inner loop** owns the Work view over a mocked bridge — the rail, the attended session,
  the stream's merge of backlog and live lines, the composer, the diff pane, and every attention
  state, including the ones real data rarely shows.
- **The Playwright outer loop** owns the record half and the negative guarantee: a browser sees
  sessions and their records, and never a stream, a diff, a tree path, or a notification setting.
- **Both locale catalogues and the parity gate** cover every new sentence; every refusal is a code in
  `Refusals`, an entry in both catalogues, and a throw site (the REV2 lesson).

## 11. The build order

Session-sized items, each TDD, each gate-green, each moved to the archive on completion.

| Item | What lands |
|---|---|
| **SURF2** | The lock keys on the tree. The session record names its tree; `ActiveForAsync` and the ledger's refusals move with it; the planner keeps its repository rule. Nothing creates a tree yet — every session runs in the registered root, so behaviour is identical and the rehearsal proves it. |
| **SURF3** | Session trees. `git worktree` under `~/.daoris/trees/`, opt-in per repository, with both editors (D50); `connect` refused from a linked worktree; removal that refuses to destroy work; the feed still reads only the registered root. |
| **SURF4a–d** | The Work view, **component by component** — the atoms and the two helpers, the rail, the attended session, then the view. |
| **SURF5** | Attention. Overview's *what needs you* band, `AwaitingPerson`'s surface and its three moves, the sidebar counts, the OS notification and its setting, and the terminal's half on `daoris-driver`. |
| **SURF6** | Review. The diff over the bridge, bounded and stated; merge and discard as the person's acts; the browser's absence gated. |

**How the screens are built is its own document** (owner's direction, 2026-09-21, recorded as D52's
amendment): `docs/2026-09-21-working-surface-components.md` carries the layers, the inventory and the
loops each part passes. It restructures the build and changes nothing above it — a rail, a head, a
live stream, a timeline, a composer and a diff assembled as one view would be a file in which the
first thing that renders is the last thing.

## 12. Deliberately not in this design

- **Several driven sessions in one repository.** The planner's rule stands until real use argues
  otherwise; the ledger will not need to change when it does (§2).
- **Hand-named sessions**, until two real ones cannot be told apart (§3).
- **A real PTY**, until a harness's interactive output proves unreadable over a pipe (§6).
- **A board.** Quests are already a better organisation primitive than the kanban every tool in the
  field invents (research §3); a second one would be this project's own pathology in a new place.
- **Anything cross-machine.** Records sync, processes never (D46/D47). A shared live console is a new
  disclosure argument, not a transport detail.
- **Progressive delegation** — autonomy that widens as approvals accumulate (§8).
- **Scheduled or recurring targets.** Still driver design open question 4: a new entity feeding
  quests, not a change to quest ids.
