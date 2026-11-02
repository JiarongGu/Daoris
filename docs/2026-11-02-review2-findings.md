# REVIEW2 — the findings and where each went

Status: open, 2026-11-02. A full code review of `f0674b65a..` main (the merges of 2026-11-01 and 2026-11-02), by area, each
reviewer read-only and told to verify every finding against the code; every fixer verified it again before changing anything.
A finding leaves this record when its fix is on main (the FIX-LOG holds each one's root cause, fix and proof) or it is filed as a
backlog row.

## Read and fixed

Three areas are read and every finding in them is fixed on main, each fixer confirming the finding held before changing it:

- **The driver's landing, workflows and course** (L1–L10, three blocking): the course reads the role that took the work; a land's
  target is the tree's line or nothing lands; a conversation's role is refused what its limit does not read; the moved-off pass is
  fed; a refused single-choice start gives its reservation back; `driver retry` alone writes the approval; a held store takes moves
  without the step-18 columns; the met action names its walk's rule; the chart follows the bind's newest version; a yes closes at
  the start it released. Built by STAFFPOLICY1e2 and the landing fixer.
- **The routes and their authority** (R1–R10, five blocking): a field's casing or a second flag no longer opens a history edit or
  *Free up space* as a read; a pasted token is refused over the pipe; `GIT_USE` remembers every checkout; a repository's URL-scoped
  header or cookie holds the remote as own-input; an HTTPS identity is erased before it is removed; the workflow verbs press their
  routes while the window runs; `place --apply` alone refuses a session's terminal; `PLACE_APPLY` takes `repositories` alone; a
  local remote is named by its folder. Built by the pipe fixer and GITAUTH1e/f.
- **The service and Ask Daoris** (S1–S7, two blocking): every card says what its Apply writes (a merge accepted with no press; a
  policy cleared or re-keyed; an undriven repository's set-up started alone); a role's own scope fences its change; the room says
  off and unreadable roles and the machine's week as such; a draft told to an earlier part is found after a limit move; the opening
  note names the role whose policy chose. Built by the Ask Daoris fixer and STAFFPOLICY1e2.

## Left open from them

- The moved-off pass runs in the same loop as the not-answering one but after it; the design wants it first (L4's order).
- The page's set-up card does not draw the plan's `alone` (S6's words are on the card; the page's `SetupShown` and `SetUpForAgents`).
- `DrivenMoveDowngradeProcessTests` (cut `sent`, word false) failed once under load and was not seen passing alone: rerun on a quiet
  machine.

## The page's flows

| # | Finding | Severity | Where | Taken by |
|---|---|---|---|---|
| P1 | On the *Landed work* route, *Use here* writes the default route's choice | blocking | `workflow/place/PlaceWorkflowView.tsx:231-262`, `:298`; `workflow/place/ChangeWorkflow.tsx:175` | the page fixer |
| P2 | The confirmation for *Your own git sign-ins* on a remote can be skipped | blocking | `settings/git/GitRemotes.tsx:129`, `:131`, `:145` | the page fixer |
| P3 | *Clear* on a git access class loosens it without asking | blocking | `settings/repository/GitAccess.tsx:148` | the page fixer |
| P4 | A draft card's *Open in the editor* treats the told draft as the saved version | blocking | `PlaceWorkflowView.tsx:163-167`; `workflow/edit/WorkflowEditor.tsx:192` | the page fixer |
| P5 | The editor's draft is lost whenever the editor unmounts (layout, *Change…*, a fold, *Set up…*) | blocking | `WorkflowEditor.tsx:173`; `PlaceWorkflowView.tsx:327`, `:338-341` | the page fixer |
| P6 | The inspector is not keyed by step, so an open role editor and typed fields carry to the next step | blocking | `WorkflowEditor.tsx:789-791`; `workflow/edit/StepOptions.tsx:62-63`, `:122-126` | the page fixer |
| P7 | A role saved from *Taken by* is applied to the draft as it was at the press | non-blocking | `workflow/edit/StepRole.tsx:73-83` | the page fixer |
| P8 | *Change…* previews one version and *Use here* chooses another | non-blocking | `ChangeWorkflow.tsx:193-197`, `:290-295` | the page fixer |

## The tests and checks

| # | Finding | Severity | Where | Taken by |
|---|---|---|---|---|
| T1 | The SSH security proofs never run under a gate: every gate strips `DAORIS_*` (so `DAORIS_TEST_SSHD`), and `GitSshHoldsProcessTests` returns green having asserted nothing | blocking | `tools/merge-branch.mjs:7194-7199`; `Workplace/GitSshdProcessTests.cs:275-286`; `GitSshHoldsProcessTests.cs:40-41` | the checks fixer |
| T2 | The person's yes to a policy hold is written by two doors and neither is tested | blocking | `WorkOrders/RetryVerb.cs:110-116`; `Modules/Bridge/Routes/DriverRoutes.cs:694-702` | the checks fixer |
| T3 | The pipe's token refusal is tested as a function, never through the pipe | non-blocking | `PipeCensusTests.cs:594-602`; `InstancePipe.cs:1026` | the checks fixer |
| T4 | Merging with plain git skips every check merge-branch blocks on | non-blocking | the dispatch skill | the steward: merges go through the tool (skill amended) |
| T5 | The post-merge CI ends at its first red, so each red costs a cycle; `--keep-going` beside `--ci` is never read | non-blocking | `tools/merge-branch.mjs:838`, `:6859`, `:6870-6874` | the steward (plain `--ci` after a merge, skill amended); the flag to the checks fixer |
| T6 | The censuses have no gate of their own, and the post-merge list is hand-written prose | non-blocking | the censuses; the dispatch skill | the checks fixer |
| T7 | The help room's goldens pinned false statements about authority as text | non-blocking | `HelpRoomGoldenTests.cs:222-236` | the checks fixer |
| T8 | The course table can be satisfied by a *left out* row, and the readers fixture's `reads` is never checked | non-blocking | `LineCourseTests.cs:653`; `participant-work-readers.json` | the checks fixer |
| T9 | The scratch census reads only one shape of delete and only `_fixtures` roots | non-blocking | `GitSeedTests.cs:151-160`; `GitHttpsProcessTests.cs:24`, `:53` | the checks fixer |
| T10 | The new frame-time budgets are load-timed with no rerun | non-blocking | `measured/canvas.spec.ts:349`; `measured/large.spec.ts:84` | the checks fixer |
| T11 | Several text censuses read only one spelling of what they guard | non-blocking | `WorkflowGraphsTests.cs:314-322`; `NoConsoleWindowTests.cs:81`; `tools/console-windows.mjs:69` | the checks fixer |
| T12 | Goldens pin incidental text, or one fact twice | non-blocking | `cli-usage/help.txt` with `driver-choices-text.json`; `deployment-rehearsal.test.ts:158-160` | the checks fixer |

All five areas are read.
