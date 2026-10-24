# Model roles — which model does what, now

**A living reference (D174).** Which harness, model, version and effort does which kind of work, with the evidence for
each choice, what is on trial, and when an agent of another maker is asked for a different opinion. This repository's
dispatch reads it in place of fixed names; Daoris's own sessions take the same roles through MODELROLE1. Change it when a
batch's measurements or a new model version say so, and date the change.

Reviewed **2026-10-25**.

**Settled-design builds on Sonnet (the owner's decision, 2026-10-25).** Building a row whose design is settled, UI and more than
one lane included, is Claude Sonnet 5.5's work at high effort, with Opus 5.5 as the fallback where Sonnet cannot start; writing a
design, answering its reads and deciding stay with Opus 5.5 at high. The install's *Build* actor was set to that order by the
owner's press the same day (D205 point 8). Daoris has no actor yet that tells a design or decision quest from a build (MODELROLE1l),
so until it does, design rounds and their reads run on the steward's own Opus agents and another maker's, and the install is
handed settled-design rows only. The trial below is promoted by this decision, and its measure goes on from MODELROLE1e onward.

**The tier rule (the owner's direction, 2026-10-24).** A lower tier takes a role wherever a measurement shows the role's result
holds on it, as D160's split did for orientation and exact builds; a role whose result has not been measured on a lower tier keeps
the stronger model. A harness's own background calls stay as the harness sets them: Claude Code calls Claude Haiku 4.5 for its own
small tasks beside the session's model (seen on the install, 2026-10-24, in the records' models next to Opus 5.5), and that is the
harness's optimization, not a role here. Should a floor ever be wanted, Claude Code's documentation (read 2026-10-24) names
`ANTHROPIC_DEFAULT_HAIKU_MODEL` as the background calls' model, a managed `availableModels` list as the allow-list for every model a
session may select (whether it binds those background calls is not said), and `CLAUDE_CODE_SUBAGENT_MODEL` for subagents; the 4.5
seen where its alias now means Haiku 5.5 points at the protocol adapter's own bundled harness.

## The roles

| Role | The work | Current | Effort | Why, and the evidence |
|---|---|---|---|---|
| **steward** | plans, reviews, merges, keeps the records | Claude Opus 5.5 (the session's) | high | Holds the backlog and every merge; judgement throughout |
| **orient** | reads and locates, hands back a pack of exact ranges; changes nothing | Claude Sonnet 5.5 | default | D160: scouts ran a median 23 turns, 2% of the subagents' cost |
| **build-exact** | one lane, the pack names the change (catalogue words, a rename, a refusal, a small fix, a test's wait) | Claude Sonnet 5.5 | default | D160: a median 33 turns, 5% of cost for 19 rows (2026-10-15 to 17) |
| **build-settled** | a row whose design is accepted: UI, more than one lane, deletion or concurrency code the design already decided | Claude Sonnet 5.5 | high | The owner's decision of 2026-10-25, after the trial below (HELPAPPLY1 with MAINWINDOWS2b and 2c merged at 111 turns); Opus 5.5 where Sonnet cannot start |
| **build-judgement** | a design, a decision to write, a row whose design is the work | Claude Opus 5.5 | high | Judgement is the work; capped near a hundred turns (D160: 145 median uncapped cost three to seven times per row) |
| **review** | reads a finished branch or a design against the code, read-only | another maker (below), else Claude Opus 5.5 | high | An independent reader finds what the author's model shares (D155) |
| **decide-support** | a different opinion on a decision, a design, a choice between shapes, the backlog's order | another maker (below) | high | The owner, 2026-10-16: used on one's own initiative, not as a fixed step |
| **help** | Ask Daoris's room | the person's choice (D89, D98) | the person's | The person's account and taste |

## Another maker

| Harness | Version | Model | How it runs here |
|---|---|---|---|
| Codex | 0.160.0 | gpt-6.1-sol, reasoning effort high | read-only, in its own detached worktree, never reading `local/` (the private notes name the binary) |

**Ask it, on your own initiative, when:** a decision or design is about to be built on; a classification decides what
can act without the person (a census, a permission, a grant); two shapes are both plausible; a defect's cause has not
shown after one honest attempt; the backlog's order is in question; a merge touches deletion, history or secrets. **Do
not** ask it as a step every batch passes through. Its words are findings: verify each against the code, then route it
as a fix, a row or a decision note, and say which were dropped and why. It runs remotely, so it costs nothing beside the
gates.

## On trial

| Candidate | For role | What would promote it | Status |
|---|---|---|---|
| Claude Haiku 5.5 | orient | packs as exact as Sonnet's at lower cost, over two batches | not yet tried |
| Claude Fable 5.1 | build-judgement | a judgement row's merge as clean as Opus's at lower cost | 2026-10-20: UIFIT1 (a settled UI row, two lanes) merged clean, 286 tool calls (first sample, D160's note of that date); the second is a row whose design is the work. **Paused 2026-10-23:** the owner directed Opus 5.5 for this development's reasoning work for now, a design's third proposal and foundation read included (D191's amendment of that date) |
| Claude Sonnet 5.5 | build-judgement on a row whose design is settled before dispatch | a scouted UI row merged green near a hundred turns | 2026-10-16: HELPAPPLY1 with MAINWINDOWS2b and 2c merged at 111 turns (first sample); STEER2b and WIRING1c-i dispatched as the second and third (D160's note of that date). **Promoted 2026-10-25** to **build-settled** by the owner's decision (above) |

A candidate is tried on one or two rows of its role in a batch, measured with `tools/subagent-usage.mjs`, and its merge
judged (gates on the first run, review findings, the window). Promote it here with the date and the batch; retire a
choice the same way.

## The targets each batch is measured against (D160)

The median worker branch at or under a hundred turns; orientation under a fifth of a judgement worker's cost; at least
half the rows built by **build-exact**; the share of cost spent in **build-judgement** falling batch on batch.
