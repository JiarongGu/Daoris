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

## Second round, the afternoon: UI/UX

Against a snapshot holding UXFIX1–5, UXFIX1b, HIST1k, UXFIX3 and UX6d2, the last of them merged and not yet committed,
read from source as before. It judged the first round's fixes: the menus' roles, `Segmented`'s focus, the account rows,
`BranchRow` and the danger ink landed well; UXFIX2's foundation and HIST1k's sentence each left a gap. Every finding
below was checked against the source.

| Finding | Verified | Row |
|---|---|---|
| A clear that took nothing closes as success: `useHistoryActs` calls `done()` on every answer and `clearSaid` says a wholly-kept workspace clear in an `ok` tone (`historyActs.ts:31`, `history.ts:395`) | ✓ | HIST1n |
| On success the focus returns only to the opener node, which `KeptHistory` removed while asking, so it falls to the body; cancel already finds the press drawn again (`InlineConfirm.tsx:163`) | ✓ | UXFIX2c |
| Pending is two disabled presses with no status or `aria-busy`, so nothing says the act started (`InlineConfirm.tsx:175`) | ✓ | UXFIX2c |
| The shared `Refused` has no `min-w-0` or anywhere-wrapping, so an unbroken token can widen the 400 px main area (`InlineConfirm.tsx:16`) | ✓ | UXFIX2c |
| *records only, no files here* is said whenever bytes are 0, beside left-over files of 0 B (`history.ts:317`, both catalogues) | ✓ | HIST1n |

**Cross-cutting, adopted:** judge an operation's outcome before closing its ask, as the branch discard already does
(`Sweep.tsx:41`); keep long raw identifiers in the shared refusal's stories.

## The account management view (the owner asked, 2026-10-07)

Asked of Codex after the owner asked for support on the account management view's design, the Claude Code account view
included, with a new fact to take in: Codex's app-server answers an account's usage windows without spending usage
(CODEXUSE1). Read from source against D125, UX6, UX7 and the account designs; nothing rendered. Its one-line verdict:
the structure is worth keeping; identity, sign-in, reported allowance and the next start are not clearly separated.

| Finding | Verified | Row |
|---|---|---|
| A refused key reads as merely unchecked: `accountState` makes a signed-out account with a key `keyed`, so attention and the badge omit it (`agents.ts:46`, `accountAttention.ts:270,371`) | ✓ | ACCTUX1 |
| A cool-off reads *resets …* even where Daoris chose the wait (`agents.ts:114`) | ✓ | ACCTUX1 |
| The usage sentence wears 11 px faint ink, 3.38:1 in light, 4.04:1 in dark (`AccountRow.tsx:150`) | ✓ | ACCTUX1 |
| Two hints mislead: *from its row below*, *Left empty, it is called {{id}}* (`harness.next.json:10`, `agents.json:53`) | | ACCTUX1 |
| Claude Code and Codex collapse to the same `C` mark (`AgentList.tsx:133`, `ui.tsx:462`) | ✓ | ACCTUX2 |
| The email sits in a tooltip no key reaches; column heads hidden without per-row labels; the terms toggle and reorder buttons under 28 px | | ACCTUX2 |
| Adding a key and saving model/effort drop their forms before the act lands; an added key skips UX7's naming step (`AgentPage.tsx:240,747`) | | ACCTUX3 |
| *consider:* the row's *first* is the list's head, not `scope.next`; usage as one sentence compares badly across windows and makers | | ACCTUX4 |
| *consider:* UX6's recorded measure on the install is 16 concepts against a budget of 14 | | ACCTUX4 |

**Its proposal, adopted as ACCTUX4's starting point.** One page per agent, one row per account, the same windows for
both makers: the chosen name first, the reported identity beneath when it differs; sign-in and its read time apart from
allowance; each window in a stable cell with its exact percent, a thin bar of that window's own allowance, its reset and
its own reading's age; one *Next start* sentence for the chosen workspace above the accounts; an unknown window said in
words with no bar and never 0%; plan, ordinary usage, spend control and reset credits in details, and a credit never
redeemed by Daoris until redeeming is supported and confirmed through `InlineConfirm`. Its mock-ups, the readings
illustrative:

```text
**About 1000px — list open, two window cells side by side**

```text
+------------------+------------------------------------------------------------------------+
| Agents           | Codex  OpenAI · installed                     [Add an account...]  ... |
|                  |                                                                        |
| CC Claude Code   | Accounts for [work v]                                      [Read again] |
|    3 accounts    | Next start: team — it is the only account here that is ready.           |
|    1 signed out  | [How accounts are used >]                                              |
|                  |                                                                        |
| Cx Codex         | Account / state           Runs for          Now          Next start    |
|    3 accounts    | ---------------------------------------------------------------------- |
|                  | team · signed in 10:40    work, forge        1 session       next    ... |
| Not installed    | five-hour [          ] 1% used       weekly [==        ] 15% used       |
|                  | resets today 15:00 · read 10:42       resets 14 Oct 17:00 · read 10:42   |
|                  | ---------------------------------------------------------------------- |
|                  | reserve · cooling        work                              [Try now] ...|
|                  | five-hour [==========] 100% used     weekly [========= ] 94% used       |
|                  | resets today 15:00 · read 10:38       resets 10 Oct 17:00 · read 10:38   |
|                  | Cooling until today 15:02                                               |
|                  | ---------------------------------------------------------------------- |
|                  | spare · signed out 10:35 work                              [Sign in] ...|
|                  | Usage unknown · never read                                             |
|                  | ---------------------------------------------------------------------- |
|                  | Your own sign-in · unknown · never read   no workspace        [Read] ...|
|                  |                                                                        |
|                  | > Each account's own plan and terms                                    |
|                  | > How accounts are used   work: Make the most of them                  |
|                  | > Workspaces              forge: this machine's accounts               |
|                  | > Ways in                                                              |
|                  | > Usage                   Daoris's sessions only                       |
+------------------+------------------------------------------------------------------------+
```

The bars are schematic: a real 1% fill stays 1%, without a minimum-width blob. The text carries the precise value.

**About 680px — distinct agent marks, stacked account facts**

```text
+----+-------------------------------------------------------------+
| CC | Codex  OpenAI · installed                                   |
| Cx | [Add an account...]                                     ... |
|    |                                                             |
|    | Accounts for [work v]                          [Read again] |
|    | Next start: team                                            |
|    | It is the only account here that is ready.                  |
|    | [How accounts are used >]                                   |
|    | ----------------------------------------------------------- |
|    | team · signed in · read 10:40                           ... |
|    | Runs for work, forge · Now 1 session · Next start            |
|    | five-hour  1% used          weekly  15% used                 |
|    | resets today 15:00          resets 14 Oct 17:00              |
|    | read 10:42                  read 10:42                      |
|    | ----------------------------------------------------------- |
|    | reserve · cooling                            [Try now] ... |
|    | Runs for work · Cooling until today 15:02                    |
|    | five-hour  100% used        weekly  94% used                 |
|    | resets today 15:00          resets 10 Oct 17:00              |
|    | read 10:38                  read 10:38                      |
|    | ----------------------------------------------------------- |
|    | spare · signed out                            [Sign in] ...|
|    | Runs for work · Sign-in read 10:35                           |
|    | Usage unknown · never read                                  |
|    | ----------------------------------------------------------- |
|    | Your own sign-in                                 [Read] ...|
|    | unknown · never read · no workspace                          |
|    |                                                             |
|    | > Each account's own plan and terms                         |
|    | > How accounts are used                                     |
|    | > Workspaces                                                |
|    | > Ways in                                                   |
|    | > Usage · Daoris's sessions only                            |
+----+-------------------------------------------------------------+
```
```

**It said to keep:** one roster per agent across its ways in; chosen names, *no workspace* and the separate *Your own
sign-in* row; one action by state; the add flow's offer to sign an existing account back in; no probe because a page
opened; the inline removal and the retained drafts; What needs you's deduplication of accounts a waiting start names.
