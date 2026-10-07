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

Read from source, catalogues and stories; nothing rendered. Five of its eight *must* findings were checked against the
source before routing (marked ✓); the rest are plausible from the cited lines and go with their bundle's proof.

| Finding | Verified | Row |
|---|---|---|
| Knowledge's mode switch: `Segmented` moves the value on arrows but not the focus, so Space reselects the old mode and a screen reader stays on the unchecked radio (`ui.tsx:1002`) | ✓ | UXFIX1 |
| The folded menu's trigger is about 23 px tall, under the 28 px target floor (`AppMenu.tsx:119`, `py-1`, no minimum) | ✓ | UXFIX1 |
| Menu checkmarks are visual only: the current workspace, place and region are not announced (`AppMenu.tsx:103`, `ui.tsx:1121`) | | UXFIX1 |
| Destructive confirmations differ after the press: Clear history stays open while pending, but discard branch and delete close at once and refuse in a toast (`Sweep.tsx:218` ✓, `SessionHead.tsx:335`, `QuestPage.tsx:396`, `AskPage.tsx:260`) | ✓ | UXFIX2 |
| Opening a confirmation neither focuses its explanation nor ties it to the confirming button, so a keyboard user reaches *Clear* without hearing what goes (`ClearAsk.tsx:101`, `Sweep.tsx:47`, `SessionPageHead.tsx:156`) | | UXFIX2 |
| *Let … run …* may offer an account whose state is unknown: `outsideOf` falls back to `candidates[0]` (`accountAttention.ts:240`) | ✓ | UXFIX3 |
| A signed-out row's age is its last reading, shown as *waiting*, so reading an account again makes an old blocker look new (`accountAttention.ts:347`) | ✓ | UXFIX3 |
| Branch rows keep three columns down to the main area's 400 px floor, and a truncated branch name cannot be read whole (`Sweep.tsx:188,244`) | | UXFIX4 |
| *consider:* dark danger text `#c74534` on `#0f0f12` is about 3.95:1 for control labels; a danger-text token (`ui.tsx:161`, `tokens.css:120`) | | UXFIX5 |
| *consider:* history's English is harder to scan than its 中文 ("An ask asked it, and clears it with its own") (`en/history.json:44`) | | UXFIX5 |

**Cross-cutting, adopted:** one inline-confirmation foundation for `ClearAsk`, `DiscardBranchAsk`, the delete and stop
asks and the inline copies in the quest, ask and agent pages: focus on opening, the explanation describing the button,
pending kept open, refusal inside, close on success or cancel; ACCTEDIT1's `answered.done/refused` is its exemplar.
Measure assembled windows, not only component stories (UX6a2's counter on the install).

**It said to keep:** Knowledge's shared place with separate mode memory; Settings alone at the bar's foot and the
seven domains; the attention rows' control budget; history's held plan, exact listed-unit press and stated kept
reasons; the account refusals' retained drafts; and the folded menu's reuse of the full menu's rows.

## Second round, the afternoon: code shape

Asked again once the owner's Codex usage returned, against a detached snapshot at `fca36ad7` so the reviewer never read
a checkout mid-merge, over what landed since `1da961d6` (REFAC1–3, EVID1b, HIST1l, SESSDEL1, ORIENT2e/2h, WAITCLAIM1,
UX6d1, MOD9b, ORIENT2a2). It found no regression in REFAC1–3 and no defect in WAITCLAIM1's distinction.

| Finding | Verified | Row |
|---|---|---|
| The scanner counts titles but never reserves the anchors it makes: `A`, `A`, `A (2)` give `A`, `A (2)`, `A (2)`, and the duplicate id fails the store's insert and the whole refresh (`RepositoryScanner.cs:508`, the same rule at `:457`, `:616`, `IndexRows.cs:45`) | ✓ | ORIENT2h3 |
| Every reader toggles a fence on any triple-backtick line, so a four-backtick fence closes on a quoted example and its table becomes index rows (`IndexSections.cs:66`, `IndexRows.cs:57`, `MarkdownSections.cs:98`) | ✓ | ORIENT2h3; the digest's twin is ORIENT2h4 |
| A failed `ls-tree` reads as an empty folder and an unreadable folder as absent, so evidence nobody read posts `missing` (`EvidenceReader.cs:225-230,263`) | ✓ | EVID1b3 |
| The two evidence test files serialize `QuestView` by hand and already differ | | EVID1b3 |
| *consider:* `RebaseAsync` (`Quests.cs:1395`) replays, judges claims, rewrites and amends in one method; a pure planner would make WAITCLAIM1's ordering testable | | QUESTREBASE1 |

**It said to leave alone:** REFAC1–3's centralizations; the twins across artifacts, C#-to-C# included; REACH's
dependency rows beside lane ownership; a deployment's index rows having no source lines, where a declared index's
rows keep theirs; and WAITCLAIM1's line between a wait on this machine's losing take and one made without a take.
