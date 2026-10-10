# SUBLOAD1: what a dispatched subagent's context cost (evidence note)

**Carried by:** D160. It records the dispatches written from 2026-10-01 to 2026-10-09, before D160's scout
and worker definitions existed. It is a record of those runs, not a contract. `tools/subagent-usage.mjs`
reads the same transcripts and prints the same columns, so a later batch is measured the same way.

> Written 2026-10-10 from the coding harness's own transcripts: one JSON line per message, a usage block on
> each assistant message, and a meta file beside each subagent's transcript naming its type. Nothing was
> run to produce them. No account is named. The transcripts folder is the machine's, so it is not given here.

## §1 What was measured

359 subagent transcripts. 356 were branches dispatched under the `dispatch-subagent` skill, as
`general-purpose` agents in worktrees. The others were two read-only searches and one documentation
lookup. Every branch ran on the same model, with a 1M-token window. Nothing compacted below roughly
900K tokens.

- **A turn** is one assistant message. A streamed message repeats its usage on every line, so it is
  counted once, by its id. Messages the harness wrote itself (a run stopped by a usage limit) are not turns.
- **Context** at a turn is its input plus its cache write plus its cache read.
- **The first edit** is the first edit or write of a file outside `local/` and `_fixtures/`. Scratch probes do
  not count as the work.
- **Weighted** counts a cache write at 1.25 of an uncached input token, a cache read at 0.1 and an output token
  at 5. It compares runs on one model. It is not a price (D57).

## §2 The numbers

| Branches (median, largest) | |
|---|---|
| Turns | 127 (369) |
| Turns before the first edit | 57 |
| Context at the first turn | 50K |
| Context at the first edit | 256K |
| Context at the end | 375K (923K) |

**Where the weighted went.** The fixed startup re-read by every turn: 13.7%. Context added while orienting,
re-read during the orientation: 15.7%. That orientation load carried through every turn after the first
edit: 35.7%. The work's own added context, re-read: 16.2%. Cache writes: 12.1%. Output: 6.6%. **The
startup and the orientation together are 65% of the cost**; the reading the work itself needed is 16%.

**Cost grows with the square of the turns**, because each turn re-reads a context that grows:

| Turns | Branches | Weighted per branch | Share |
|---|---|---|---|
| under 100 | 107 | 1.7M | 10% |
| 100 to 200 | 196 | 5.0M | 54% |
| 200 to 300 | 48 | 11.1M | 29% |
| 300 and over | 7 | 18.2M | 7% |

**What the orientation read.** About 104K tokens of tool output per branch came back before its first edit,
estimated from characters at 3.6 a token. The largest part was shell range prints and greps: `sed -n`
(13K per branch, 298 branches), `grep -n` (6.6K), the search tool (4.7K) and `grep -rn` (2.9K). Then came
the same large files, read again by branch after branch: `.claude/knowledge/twins.md` read whole (11K, 58
branches), ranges of the driver's `Harnesses.cs` (13K, 38) and `Driver.cs` (8K, 68), and the platform UX
contract (6K by range for 46 branches, 13K whole for 21).

**The startup.** A `general-purpose` subagent started at 50K tokens. A read-only search agent, with a
smaller tool set and no brief, started at 21K. The difference is mostly tool schemas, and the brief is
about 7K of it.

## §3 What each lever would have saved

Each branch's per-turn contexts were replayed under a change and priced with the same ratios. The replay
reproduces the recorded total to within 2%. Re-reading after a compaction or a hand-over is not modelled,
so each figure is a lower bound on the remaining cost.

| Change | Remaining cost |
|---|---|
| Compact at 200K | 56% |
| Compact at 300K | 67% |
| Split every branch at 80 turns | 64% |
| Split every branch at 120 turns | 76% |
| A quarter fewer turns, by batching independent calls | 77% |
| A startup 10K smaller | 97% |
| A startup 15K smaller | 95.5% |

The owner chose to cut the initial load instead of compacting (D160). A smaller startup alone barely
moves the cost. Startup and orientation do, together, because the
orientation's 200K is carried by every later turn. D160 targets them.

## §4 What D160 is judged by

The trial's batch is measured by `tools/subagent-usage.mjs`, grouped by agent type and model, against §2.

- A worker's context at its first edit, and its turns before it, against 256K and 57.
- The share of its cost that is orientation carried, against 35.7%.
- The scout's cost, counted in the same batch. Orienting is paid once either way; the saving is the load the
  worker no longer carries.
- On a second model, the columns, not the weighted total.

A pack is only as good as its pointers: each worker's hand-back names the pack's gaps.
