# Census: what a Daoris session narrows (AGENTFREE1)

D253 point 7: a session's agent keeps every ability its harness has when a person runs it by hand. Daoris narrows only
through the person's own rules, a workflow's step and the floor that keeps work from being lost. This census lists every
flag, rule, deny, hook, environment strip and tool narrowing a start composes on the doors Daoris drives (Claude Code's
native pipe door, Claude Code's protocol door, Codex's protocol door), what each stops, why it exists, and its verdict. It was
taken at main `e3c6a88b87`; the places named are that commit's.

A row Daoris adds (a tool, a hook that decides nothing, a rule that allows) narrows nothing and is said so. A `lift` row
names the commit that lifts it, or the row that will where its code is another builder's.

## The rows

| # | Door | What a start composes | Where | What it stops | Why it exists | Verdict |
|---|---|---|---|---|---|---|
| 1 | Claude, both | `autoMemoryEnabled: false` in the session's settings file | `ClaudeRules.OwnContext` | the harness's own memory, read and written in its home | D230 (CONTEXTONE1e): a session's context the same whichever account runs it | **lift**: made on this branch; seen in a real session: the harness's memory section is present ([session evidence](2026-11-11-agent-free-session-evidence.md) §5) |
| 2 | Claude, both | `syncClaudeAiSkills: false` | same | the skills the person synced to the account | D230, as row 1 | **lift**: made; seen in a real session: the claude.ai-synced skills reach as `anthropic-skills:*` (§1) |
| 3 | Claude, both | `syncClaudeAiPlugins: false` | same | the plugins the person synced to the account | D230, as row 1 | **lift**: made; the key is absent from a real session's settings file, no account plugin enabled to show the effect (§5) |
| 4 | Claude, both | `disableClaudeAiConnectors: true` | same | the account's connectors | D230, as row 1 | **lift**: made; seen in a real session: the account's connectors are listed and callable (§5) |
| 5 | Claude, both | the tree guard's `PreToolUse` hook refusing a write outside the session's tree | `Workplace/hooks/tree-guard.mjs`, `TreeGuard.For` | a write outside the tree, and with it every write of the session's own subagent in the worktree the harness made for it: Claude Code places a subagent's worktree under the repository's main checkout (`<root>/.claude/worktrees/`), never under the session's linked tree | D72's `tree-guard` default: a write outside the tree could overwrite the person's work or another session's | **keep** as the floor, the person's to switch off; **lift** its refusal of the session's own subagent's worktree: made, and proven in a real session: a subagent in `isolation: worktree` wrote, read back and saw its file in `git status` (§2) |
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
| 16 | every door | the browser's server | `SessionServers.HandNotedAsync`, `ChatServers` | nothing: driven sessions and conversations are handed the plugin's browser server, and told so, wherever the person installed it and Daoris's browser can be reached | D170 | narrows nothing; a real session's `browser` tool listed its tabs (§4) |
| 17 | every door | the call table's tools per work; rule 54's kinds that may ask for a co-worker | `CallTable`, roles rules 53-54 | Daoris's own tools and co-workers beyond a session's work | D212, D230 | narrows nothing a hand run has: Daoris's own additions, served per work |
| 18 | Claude, protocol | `subagents` and AIR's `nativeSubagentSessions` asked only where streams route them | `AcpProtocol.Capabilities` | nothing: the harness runs its subagents either way; the capability asks for each subagent's own stream | BGWAIT1 | narrows nothing |
| 19 | every protocol door | `fs` and `terminal` declared off | `AcpProtocol.Capabilities` | nothing: the harness uses its own file tools and shell | D53 | narrows nothing |
| 20 | Claude, native | `ENABLE_TOOL_SEARCH=false` | the spawn shell (`Spawning.cs`) | nothing: every tool is listed at the start | D230 | narrows nothing; a real session lists every tool at the start (§1, §7) |
| 21 | every door | the role variables cleared | the spawn shell | the person's and the holder's keys reaching a session | D208, D253 | **keep**: the person's own keys stay the window's |
| 22 | every door | every other provider's registered credential and home dropped | the spawn shell | another agent's key or sign-in reached from a session's shell | D49, D172 point 3, agents rule 28 | **keep**, confirmed with row 23 made: a session runs on the account the walk chose, and another agent's key in its shell would start that agent on the person's own sign-in, which Daoris never starts work on and whose limits it never reads; the harness's every tool still runs, and another agent is reached as a co-worker or a quest on an account of its own set. The person's settings and Codex's home are handed with the same floor (no sign-in, provider or home variable) |
| 23 | every door | a configuration home Daoris owns, per account | the spawn shell, agents rule 17 | the person's own tool home: its user-level skills, subagent definitions, servers, settings (Claude in Chrome's switch among them), its memory file; Codex's own configuration and its servers | D175, D197: a start runs on the account the walk chose | **lift**: made (D262, D266): the account's home stays, and the person's skills, subagents, commands, servers, enabled plugins, settings, memory file and Chrome switch reach both of Claude Code's doors, and Codex's configuration, instructions, skills and prompts its door, each family the person's to switch off, and Codex's own servers ride the protocol door's open beside the plan's (`CodexOwnServers`); seen in a real Claude protocol-door session ([session evidence](2026-11-11-agent-free-session-evidence.md)): the person's user-scope server runs as `plugin:own:kobo-memory` and answers, their settings keys are in the session's file; **not shown**: a skill named `own:*` and the memory file (the person's home holds none), and Chrome (`--chrome` and the tools reach, no extension is connected, §7, TASKS AGENTFREE1) |
| 24 | Claude, both | a driven session's mode: `auto`, else `acceptEdits` (protocol), `acceptEdits` (pipe); `agent` on Codex | agents rule 21 | a tool call outside the mode's reach: refused on the pipe door and in the judged modes | D81, D184 | mode **keep** (the person's access setup chooses another); the refusal **lift**, held by ASKFULL1 (D253 points 5-6: held for the person, never refused) |
| 25 | Claude, Lyntai's layer | the three flow tools a headless turn cannot answer, always denied | `ClaudeDrivenLayer` | asking the person a question, entering and leaving plan mode | AGENTLYNTAI1s-ii | open: the owner's measurement decides (that row) |
| 26 | Codex | no rule language, so none of the person's denies | agents rule 19 | nothing; it holds fewer narrowings than Claude's doors | D72, D213 | narrows nothing |
| 27 | every door | full access and the asking mode only on the person's setting | agents rule 22 | nothing a hand run has by default | D183, D184 | **keep**: the person's rule |
| 28 | every door | writes to another checkout only where the person declared it | agents rule 25, `AcrossRules` | a write in another registered checkout | D107 | **keep**: the person's rule |
| 29 | Claude, native | the repository's `.mcp.json` servers, approved in the person's own home, not in an account's | agents rule 13, contradiction 12 | nothing on the native door: measured on Claude Code 2.1.322 signed out, with no model call, a `-p` run in a home holding no approval and a folder never trusted starts the repository's `.mcp.json` server, while `claude mcp list` there says it waits for approval | D72 | **keep**, nothing to lift: a by-hand approval is not needed under `-p`, and one the person gave in their user settings reaches the session with them (`own-settings`); the protocol door runs the same program through the SDK, unmeasured there until a signed-in open names a repository server; **measured on the protocol door** signed in, no approval, with no model call: a `.mcp.json` server in a folder never trusted started and connected ([session evidence](2026-11-11-agent-free-session-evidence.md) §6) |

## What the lifts change

- Rows 1-4: a Claude session's settings file says nothing of memory, synced skills, synced plugins or the account's
  connectors, so the account's own settings decide them as they do by hand. Daoris still writes no account's home.
- Row 5: the tree guard lets a write through when the call is a subagent's (`agent_id` present), its working directory is
  a worktree under the harness's own subagent folder (`.claude/worktrees/<name>`) of the session tree's main checkout, and
  that worktree's git directory is the session's repository's. The session's own agent still writes only in its tree.
- Row 23: a Claude or Codex session is handed what the person's own run reads from its tool home, family by family, as the design
  below says; nothing of it is written, and each family is a default of the person's rules.

## Rows for other builders

- Row 24's refusal is ASKFULL1's (D253 points 5-6).
- Row 25 is AGENTLYNTAI1s-ii's measurement.

## Row 23's design: the person's own tool home

A session runs in its account's home (agents rule 17) and Daoris writes no account home (rule 15), so what the person's own run
reads from its tool home never reaches it. The spawn reads the person's tool home where their own run would and hands what it
holds by the harness's session-only doors, writing only under the session's spawn folder, removed with it. Nothing is handed to a
session that runs in the person's own home (a conversation begun there), whose harness loads it all itself, nor under a scratch
home (a test's, a probe's).

**Claude Code** reads `CLAUDE_CONFIG_DIR`, else `.claude` in the profile, with its global file beside it (`<home>/.claude.json`
under the variable, else `.claude.json` in the profile).

| Family | A by-hand run loads | A session is handed (both doors) | The person's switch | The floor |
|---|---|---|---|---|
| skills, subagents, commands | `skills/`, `agents/`, `commands/` | copied into one harness plugin, `own`, beside Daoris's plugins, named by `--plugin-dir` or the open's meta; its pieces are named `own:<name>` | `own-skills` | copies only: the person's folder is read, never linked or written |
| servers | the global file's `mcpServers` (user scope) | the same plugin's `.mcp.json` | `own-servers` | a server named as Daoris's connector, or whose environment names a Daoris variable (its identity, its keys), is left out; a pass held to Daoris's connector alone (rule 13) is handed none |
| plugins | each plugin the user settings' `enabledPlugins` enables, at its user-scope install folder (`plugins/installed_plugins.json`) | the install folder by the same door, read in place as by hand | `own-plugins` | none beyond the plugin's own |
| the Chrome switch | `claudeInChromeDefaultEnabled` in the global file | `--chrome` on the native door; on the protocol door the SDK's `extraArgs` (`{ chrome: null }`) in the open's options | `own-chrome` | none: the browser is the person's own |
| settings | `settings.json` | merged into the session's rules file beneath what outranks it: a list joins Daoris's (the harness joins lists across tiers), a value comes in only where Daoris's file, the repository's `.claude/settings.json` and `settings.local.json` in the session's folder and the account's own `settings.json` set none | `own-settings` | nothing that signs in (`apiKeyHelper`, the login and cloud keys), sets the mode (`permissions.defaultMode`), names plugins or switches hooks off; no variable the session's process holds, a Daoris one, any provider's home, key or pin, or a sign-in one; a pass shaped for one job takes no allow, extra folder, hook or project-server approval |
| memory file | `CLAUDE.md` | appended to the system prompt: `--append-system-prompt-file` naming the file on the native door, its text on the SDK's `claude_code` preset (`systemPrompt.append`) on the protocol door | `own-memory` | read where it lies; an `@` import is handed as written |

Each switch is a default of the person's rules (agents rule 18), on until they switch it off. The global file is read for the
named keys alone: its sign-in and its project entries are never read, and no other account's home is read at all.

**Codex** reads `CODEX_HOME`, else `.codex` in the profile: `config.toml` (its servers, profiles, features), `AGENTS.md`,
`skills/`, `prompts/`. Its door has no plugin folder and no settings tier; its one session-only door is the adapter's `CODEX_CONFIG`,
a JSON configuration codex-acp (2.1.1) passes as each thread's configuration over the account's `config.toml`. The spawn sets it from
the person's home: `config.toml`'s keys where the account's sets none (`own-settings`), without the provider, the sign-in, the
approval policy, the sandbox or the trusted projects, in a profile either; and `developer_instructions` holding the account's own,
then the person's `AGENTS.md` (`own-memory`) and a sentence naming their `skills/` and `prompts/` folders (`own-skills`). Its servers
are not handed there: the adapter replaces the configuration's `mcp_servers` with the open's whenever the open names any, and
Daoris's connector always rides it, so they join the open's server list instead (`CodexOwnServers`), local programs only, under the
Claude servers' floor.
