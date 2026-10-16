# Model roles — which model does what, now

**A living reference (D174).** Which harness, model, version and effort does which kind of work, with the evidence for
each choice, what is on trial, and when an agent of another maker is asked for a different opinion. This repository's
dispatch reads it in place of fixed names; Daoris's own sessions take the same roles through MODELROLE1. Change it when a
batch's measurements or a new model version say so, and date the change.

Reviewed **2026-10-16**.

## The roles

| Role | The work | Current | Effort | Why, and the evidence |
|---|---|---|---|---|
| **steward** | plans, reviews, merges, keeps the records | Claude Opus 5.5 (the session's) | high | Holds the backlog and every merge; judgement throughout |
| **orient** | reads and locates, hands back a pack of exact ranges; changes nothing | Claude Sonnet 5.5 | default | D160: scouts ran a median 23 turns, 2% of the subagents' cost |
| **build-exact** | one lane, the pack names the change (catalogue words, a rename, a refusal, a small fix, a test's wait) | Claude Sonnet 5.5 | default | D160: a median 33 turns, 5% of cost for 19 rows (2026-10-15 to 17) |
| **build-judgement** | design, UI, more than one lane, a decision to write, deletion or concurrency code | Claude Opus 5.5 | high | Judgement is the work; capped near a hundred turns (D160: 145 median uncapped cost three to seven times per row) |
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
| Claude Fable 5.1 | build-judgement | a judgement row's merge as clean as Opus's at lower cost | not yet measured |
| Claude Sonnet 5.5 | build-judgement on a well-scouted UI row | a scouted UI row merged green near a hundred turns | per D160's earlier batches, promising |

A candidate is tried on one or two rows of its role in a batch, measured with `tools/subagent-usage.mjs`, and its merge
judged (gates on the first run, review findings, the window). Promote it here with the date and the batch; retire a
choice the same way.

## The targets each batch is measured against (D160)

The median worker branch at or under a hundred turns; orientation under a fifth of a judgement worker's cost; at least
half the rows built by **build-exact**; the share of cost spent in **build-judgement** falling batch on batch.
