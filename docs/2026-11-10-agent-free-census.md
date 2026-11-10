# Census: what a Daoris session narrows (AGENTFREE1)

D253 point 7: a session's agent keeps every ability its harness has when a person runs it by hand. Daoris narrows only
through the person's own rules, a workflow's step and the floor that keeps work from being lost. This census lists every
flag, rule, deny, hook, environment strip and tool narrowing a start composes on the doors Daoris drives (Claude Code's
native pipe door, Claude Code's protocol door, Codex's protocol door), what each stops, why it exists, and its verdict.

A row Daoris adds (a tool, a hook that decides nothing, a rule that allows) narrows nothing and is said so. A `lift` row
names the commit that lifts it, or the row that will where its code is another builder's.

## The rows

| # | Door | What a start composes | Where | What it stops | Why it exists | Verdict |
|---|---|---|---|---|---|---|
| 1 | Claude, both | `autoMemoryEnabled: false` in the session's settings file | `ClaudeRules.OwnContext` | the harness's own memory, read and written in its home | D230 (CONTEXTONE1e): a session's context the same whichever account runs it | **lift**: made on this branch |
| 2 | Claude, both | `syncClaudeAiSkills: false` | same | the skills the person synced to the account | D230, as row 1 | **lift**: made |
| 3 | Claude, both | `syncClaudeAiPlugins: false` | same | the plugins the person synced to the account | D230, as row 1 | **lift**: made |
| 4 | Claude, both | `disableClaudeAiConnectors: true` | same | the account's connectors | D230, as row 1 | **lift**: made |
| 5 | Claude, both | the tree guard's `PreToolUse` hook refusing a write outside the session's tree | `Workplace/hooks/tree-guard.mjs`, `TreeGuard.For` | a write outside the tree, and with it every write of the session's own subagent in the worktree the harness made for it: Claude Code places a subagent's worktree under the repository's main checkout (`<root>/.claude/worktrees/`), never under the session's linked tree | D72's `tree-guard` default: a write outside the tree could overwrite the person's work or another session's | **keep** as the floor, the person's to switch off; **lift** its refusal of the session's own subagent's worktree: made |
| 6 | Claude, both | `no-push` deny forms and the classifier's push sentence | `Permissions.cs` defaults, `GitAccessPolicy` | a push in any form | D72, D198: a push leaves the machine | **keep**: the person's rule, opened by git access's Write |
| 7 | Claude, both | `no-publish`'s sentence | `Permissions.cs` | publishing a package or a release | D72 | **keep**: the person's rule, switched by the person |
| 8 | Claude, both | the person's `permissions.json` rules, three scopes | `PermissionRules.Compose` | what the person denied | D72, D184 | **keep**: the person's rule |
| 9 | Claude, both | the deny on Daoris's git profile | `GitSessionHolds.ProfileDeny` | reading Daoris's identity store | D241 | narrows nothing a hand run has: Daoris's own store |
| 10 | Claude, both | the lineage hook on the connector's calls and each subagent's start and stop | `ClaudeLineageHook` | nothing; it decides nothing | FABRIC-C1a | adds |
| 11 | Claude, both | the `commit` and `connector` defaults | `Permissions.cs` | nothing; both allow | D72 | adds |
| 12 | Claude, both | a reviewer's, helper's, partner's, brainstorm's and supervisor's shapes (`OpinionPermissions`, `PartnerPermissions`, `BrainstormPermissions`, `SupervisePermissions`) | `SessionRules.Hand` | writes, shells and pushes those passes do not make | D212, D233 | **keep**: a workflow's step |
| 13 | Claude, both | a heavy gate's declared command denied under a light pipeline | `HeavyTold`, `SessionRules.Hand` | running the verification's own gate inside the build | D233's checks | **keep**: a workflow's step |
| 14 | Claude, both | a supervisor's turn held to the person's tier and Daoris's servers (`--setting-sources user --strict-mcp-config`; `SourcesAloneMeta` on the protocol door) | `HandSourcesAlone` | the copy's settings, hooks and servers | D233 | **keep**: the person's appointment, a supervisor reads alone |
| 15 | every door | Daoris's connector alone for a reviewer, helper, partner or brainstorm pass | `ConnectorPlans.ConnectorOnly` | a plugin's server, which could write where the pass may not | D212 | **keep**: a workflow's step |
| 16 | every door | the browser's server | `SessionServers.HandNotedAsync`, `ChatServers` | nothing: driven sessions and conversations are handed the plugin's browser server, and told so, wherever the person installed it and Daoris's browser can be reached | D170 | narrows nothing |
| 17 | every door | the call table's tools per work; rule 54's kinds that may ask for a co-worker | `CallTable`, roles rules 53-54 | Daoris's own tools and co-workers beyond a session's work | D212, D230 | narrows nothing a hand run has: Daoris's own additions, served per work |
| 18 | Claude, protocol | `subagents` and AIR's `nativeSubagentSessions` asked only where streams route them | `AcpProtocol.Capabilities` | nothing: the harness runs its subagents either way; the capability asks for each subagent's own stream | BGWAIT1 | narrows nothing |
| 19 | every protocol door | `fs` and `terminal` declared off | `AcpProtocol.Capabilities` | nothing: the harness uses its own file tools and shell | D53 | narrows nothing |
| 20 | Claude, native | `ENABLE_TOOL_SEARCH=false` | the spawn shell (`Spawning.cs`) | nothing: every tool is listed at the start | D230 | narrows nothing |
| 21 | every door | the role variables cleared | the spawn shell | the person's and the holder's keys reaching a session | D208, D253 | **keep**: the person's own keys stay the window's |
| 22 | every door | every other provider's registered credential and home dropped | the spawn shell | another agent's key or sign-in reached from a session's shell | D49, D172 point 3, agents rule 28 | **keep**: the person's own sign-ins stay the person's; a session reaches another agent through a co-worker or a quest |
| 23 | every door | a configuration home Daoris owns, per account | the spawn shell, agents rule 17 | the person's own tool home: its user-level skills, subagent definitions, servers, settings (Claude in Chrome's switch among them), its memory file; Codex's own configuration and its servers | D175, D197: a start runs on the account the walk chose | **lift**, not made here: the account's home stays; what the person's own tool home holds is handed to the session (a design: which pieces, and how, on each door) |
| 24 | Claude, both | a driven session's mode: `auto`, else `acceptEdits` (protocol), `acceptEdits` (pipe); `agent` on Codex | agents rule 21 | a tool call outside the mode's reach: refused on the pipe door and in the judged modes | D81, D184 | mode **keep** (the person's access setup chooses another); the refusal **lift**, held by ASKFULL1 (D253 points 5-6: held for the person, never refused) |
| 25 | Claude, Lyntai's layer | the three flow tools a headless turn cannot answer, always denied | `ClaudeDrivenLayer` | asking the person a question, entering and leaving plan mode | AGENTLYNTAI1s-ii | open: the owner's measurement decides (that row) |
| 26 | Codex | no rule language, so none of the person's denies | agents rule 19 | nothing; it holds fewer narrowings than Claude's doors | D72, D213 | narrows nothing |
| 27 | every door | full access and the asking mode only on the person's setting | agents rule 22 | nothing a hand run has by default | D183, D184 | **keep**: the person's rule |
| 28 | every door | writes to another checkout only where the person declared it | agents rule 25, `AcrossRules` | a write in another registered checkout | D107 | **keep**: the person's rule |
| 29 | Claude, native | the repository's `.mcp.json` servers, approved in the person's own home, not in an account's | agents rule 13, contradiction 12 | unmeasured: whether an account's home holds the approval the person gave by hand | D72 | to measure under row 23's design |

## What the lifts change

- Rows 1-4: a Claude session's settings file says nothing of memory, synced skills, synced plugins or the account's
  connectors, so the account's own settings decide them as they do by hand. Daoris still writes no account's home.
- Row 5: the tree guard lets a write through when the call is a subagent's (`agent_id` present), its working directory is
  a worktree under the harness's own subagent folder (`.claude/worktrees/<name>`) of the session tree's main checkout, and
  that worktree's git directory is the session's repository's. The session's own agent still writes only in its tree.

## Rows for other builders

- Row 23 (the person's own tool home handed to a session) composes a spawn's home and environment, which the spawn shell
  owns; it needs a design before code.
- Row 24's refusal is ASKFULL1's (D253 points 5-6).
- Row 25 is AGENTLYNTAI1s-ii's measurement.
- Row 29 is measured with row 23.
