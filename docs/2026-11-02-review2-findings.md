# REVIEW2 — the findings and where each went

Status: open, 2026-11-02. A full code review of `f0674b65a..` main (the merges of 2026-11-01 and 2026-11-02), by area, each
reviewer read-only and told to verify every finding against the code; every fixer verifies it again before changing anything and
says when one does not hold. A finding leaves this record when its fix is on main (the FIX-LOG holds the detail) or it is filed as
a backlog row. Until the routes' blocking findings are fixed, the install is not staged from main.

## The driver's landing, workflows and course

| # | Finding | Severity | Where | Taken by |
|---|---|---|---|---|
| L1 | The course reads seeded Build's policy, not the role that took the work | blocking | `Loop/StaffCourse.cs:39`, `:118`; `Loop/Driver.cs:2304`, `:4455` | STAFFPOLICY1e2 |
| L2 | A `land` node's `target` is accepted for every method and never used: the work merges into the tree's line while the record names another | blocking | `Workflows/Nodes/LandNodes.cs:86`; `Workplace/CurrentDeliveries.cs:47` | the landing fixer |
| L3 | A conversation's limit ignores the role's `ask`, `wait` and `most`, which the doors accept for chat roles | blocking | `Roles/StaffPolicyDoor.cs:24-33`; `Course/Chat/LimitMove.cs:876-886` | STAFFPOLICY1e2 |
| L4 | The 30-minute moved-off pass is never fed | non-blocking | `Roles/StartPlan.cs:187` | STAFFPOLICY1e2 |
| L5 | The single-entry fast path keeps its reservation when the start is refused | non-blocking | `Roles/StartPlan.cs:296-299` | STAFFPOLICY1e2 |
| L6 | `driver retry` with no instance writes no approval on a policy hold, yet says it did | non-blocking | `WorkOrders/RetryVerb.cs:103-105` | STAFFPOLICY1e2 |
| L7 | `MoveStore` names the step-18 columns on a store still at 15 | non-blocking | `Course/MoveStore.cs:72`, `:638`, `:698` | the landing fixer |
| L8 | `next` and `weighted` walk alike | non-blocking | `Roles/StaffJudge.cs:125-127` | STAFFPOLICY1e2 |
| L9 | The place's chart and the run's bind follow different versions when the newest does not read | non-blocking | `Workflows/WorkflowEffectives.cs:102`; `Workflows/WorkflowRunBindings.cs:126` | the landing fixer |
| L10 | A stale yes can release a later move; a failed store write is said as the move bound | non-blocking | `Loop/StaffCourse.cs:57-67`, `:101` | STAFFPOLICY1e2 |

## The routes and their authority

| # | Finding | Severity | Where | Taken by |
|---|---|---|---|---|
| R1 | `GIT_HISTORY_EDIT` runs as an Open read: the census matches a field ignoring case and lets `plan` decide while the handler reads `said` first, exact-case | blocking | `Modules/Platform/PipeCensus.cs:61-70`, `:455`; `Bridge/Routes/GitRoutes.cs:551` | the pipe fixer |
| R2 | The same hole opens `SYSTEM_SPACE_FREE`'s delete | blocking | `Bridge/Routes/SystemRoutes.cs:85-98`; `PipeCensus.cs:453` | the pipe fixer |
| R3 | A pasted HTTPS token sent over the pipe is kept on disk while it waits, and shown on the card | blocking | `Bridge/Routes/ToolsRoutes.cs:267-270`; `PipeCensus.cs:186`; `InstancePipe.cs:1227-1231` | GITAUTH1e/f |
| R4 | `GIT_USE` from the window remembers one repository's checkouts, so every other checkout loses its pointers | blocking | `Bridge/Routes/ToolsRoutes.cs:343-347` | GITAUTH1e/f |
| R5 | A repository's URL-scoped `http.<url>.extraheader` can still carry the person's own header past the identity's reset (traced, to prove with a real git) | blocking if it holds | `Workplace/GitHttps.cs:146`; `Workplace/GitOwnInput.cs:51` | GITAUTH1e/f |
| R6 | Removing an HTTPS identity drops it before GCM's erase, which may then fail or not run | non-blocking | `Workplace/GitIdentityHttps.cs:169-174`, `:78-86` | GITAUTH1e/f |
| R7 | `driver workflow new|edit|apply|import|use|kind` writes alone while the window runs | non-blocking | `Workflows/WorkflowVerb.cs:22-30`; `DriverChoicesCommand.cs:293` | the pipe fixer |
| R8 | `place … --apply` acting alone does not refuse a session's terminal | non-blocking | `Setup/PlaceCommand.cs:150-156` | the pipe fixer |
| R9 | `PLACE_APPLY` accepts a single `repository` its owner row and grounds never read | non-blocking | `Bridge/Routes/SetupRoutes.cs:174-177` | the pipe fixer |
| R10 | `GIT_BRANCHES {remotes}` answers a local remote's machine path | non-blocking | `Workplace/GitRemoteRows.cs:48` | GITAUTH1e/f |

## Still to read

The service and Ask Daoris; the page's flows; the tests and checks.
