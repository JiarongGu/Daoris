# Second-opinion review after the 2026-10-07 run

> **Standing:** evidence. A read-only second opinion from another agent harness (Codex), asked by the owner after a
> day of about forty merges: one review of the code's shape, one of the UI/UX. Its findings are suggestions; this
> document records which were verified and where each went. Nothing here is a decision.

## How it was asked

`codex exec` with a read-only sandbox, no approvals, the repository as its root, told not to read `local/`, and given
the project's own rules to judge by (CLAUDE.md, AGENTS.md, twins.md and the lane map for the code; the platform design
language, UX6 and the component method for the UI). The prompts and raw answers stayed on the machine that ran them.

**What it taught about our own doctrine.** The first UI/UX run read nothing: Codex read `file-tool-discipline` ("use
the dedicated read/search tools, not shell"), found its harness offers none, and stopped. The rule does not say what a
harness without such tools should do. That is CANONREAD1.

## Code shape

**Latent bugs, each verified against the source before it became a row:**

| Finding | Verified | Row |
|---|---|---|
| `HistoryCommand.Door` interpolates a workspace bare into the line it prints, so `my team` breaks the command ACCTQUOTE1b's spelling exists for | yes, `HistoryCommand.cs:480` | HIST1i |
| A clear's freed bytes are measured before removal and summed whatever failed, so a locked transcript counts as freed; the service's `QuestFiles` swallows its own deletion failures | yes, `HistoryClearing.cs:335,363` | HIST1j |
| The workspace reading says "takes nothing" from bytes alone, beside a live *Clear history…* when a closed quest has records but no files | yes, `history.ts:314` against `clearList` | HIST1k |

**Refactors, ranked by value over risk (its order), and where each went:**

1. Plugin-id validation has more than one owner per artifact (`driverconfig.ts` and `plugins.ts`; `Landing.cs` and
   `Plugins.cs`): CASEFOLD1e had to fix two regexes. → REFAC1.
2. `HistoryKept` repeats `HistoryCodes.Of`'s mapping in the modules. → REFAC1.
3. Evidence verdicts are validated twice in the service (`QuestEvidence.cs`, `QuestExchange.cs`), already differing
   on `spelled`. → REFAC3.
4. Tooling assembles write-beside-then-rename itself; `ux-count.mjs` still renames bare. → REFAC2.
5. `as-merged.mjs` and `merge-branch.mjs` each build the staged tree. → REFAC2.
6. `SessionHomeFiles`' suffixes and `Own` describe the same files twice, and clearing discards the removal result. →
   folded into HIST1j.
7. The driver reconstructs which *needs-you* the service meant from a second snapshot. → HIST1l.
8. Two driver tests walk to `daoris.json` the same way. → REFAC1.

**It said to leave alone**, and this review agrees: the twins across languages and their shared tables; REACH's
cross-lane fixture rows, which map test dependencies, not ownership; the selective rename retries (a held live plugin
must refuse at once); `SessionBranchDiscard`, `Planner.CarriesOn`, `HistoryAnswers` and `HistoryStandIn`; and the
counter's self-contained page function, which must serialize whole.

## UI/UX

Pending: the re-run with read access stated.
