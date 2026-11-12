# Evidence: a real session's abilities (AGENTFREE1)

Taken 2026-11-11 from inside a driven Claude Code session (the protocol door, `claude-code-acp` 0.84.0, harness
through the Agent SDK 0.3.284, account home under Daoris's data folder, tree `s-40bdca6b`, main `8aebffc592`).
D253 point 7 says a session keeps every ability its harness has; the census (`archive/2026-11-10-agent-free-census.md`)
lists what a start narrows. This document checks the lifts and the "narrows nothing" rows from inside the session
that the lifts are for. Paths are written `<account home>` (the session's `CLAUDE_CONFIG_DIR`), `<person's home>`
(their own `~/.claude` and `~/.claude.json`), `<spawn>` (Daoris's per-session spawn folder) and `<main checkout>`.
Each check names its command and its result; a result that proves less than the row says so.

## What the start composed (read from the running process)

`Get-CimInstance Win32_Process` on the harness's pid, arguments only, secrets omitted:

```
claude.exe --output-format stream-json --verbose --input-format stream-json --permission-prompt-tool stdio
  --disallowedTools AskUserQuestion --tools default
  --mcp-config {"mcpServers":{"daoris-knowledge":{...},"browser":{...}}}
  --setting-sources=user,project,local --permission-mode default --allow-dangerously-skip-permissions
  --plugin-dir <spawn>/805e2924.plugins/_own --chrome --settings <spawn>/805e2924.settings.json
```

`--tools default` and no `--allowedTools`/`--bare`: the harness's own tool set. `--plugin-dir …/_own` (the person's
home, row 23), `--chrome` (the Chrome switch) and `--settings` (the person's settings, merged) are the lifts' doors.
`CLAUDE_CODE_ENTRYPOINT=sdk-cli`, `ENABLE_TOOL_SEARCH=false` (row 20) and `CLAUDE_CONFIG_DIR=<account home>` are in the
environment; the only `DAORIS_*` variables are Daoris's own.

## Checks

### 1. The person's own skills reach the session (row 23, skills)

Commands: the session's own skill listing (what the harness offered at the start); `find <person's home>/skills -maxdepth 3`;
`find <spawn>/805e2924.plugins/_own/skills -mindepth 2 -maxdepth 2 -name SKILL.md | wc -l`.

- The person's own `skills/`, `agents/` and `commands/` hold **no skill**: `agents/` and `commands/` do not exist, and
  `skills/` holds only `synced/` and `.trash/`, the claude.ai sync's caches (bucket folders; `skills/<name>/SKILL.md`
  at the depth the harness reads: 0 files). By hand the harness loads none of it either.
- The synced skills reach the session through the harness's own sync (row 2 lifted): the listing carries
  `anthropic-skills:docs`, `:docx`, `:pdf`, `:pptx`, `:xlsx`, `:skill-creator`, `:google-workspace`, `:import-memory`,
  `:morning`, named by the harness (`<plugin>:<skill>`), and the repository's own skills (`change`, `add-pack`,
  `dispatch-subagent`, …) from `.claude/skills` unnamed.
- **Not proven:** that a person's skill is named `own:<name>`. The plugin is named `own` (`plugin.json` `name`; the
  server below shows as `plugin:own:kobo-memory`), and the harness prefixes a plugin's skills with its name as it does
  `anthropic-skills:`, but this person's home holds no skill to show it with, and a session writes no file in their home.
- Observed, narrowing nothing: the copy took the folder whole, **903 files, 18 MB**, for zero skills (the sync
  caches). It costs a copy per start. Filed as TASKS row AGENTFREE1 own-copy.

### 2. A subagent works, and writes in a worktree of its own (row 5)

Command: the Agent tool, `subagent_type: general-purpose`, `isolation: worktree`, asked to run
`pwd; git rev-parse --show-toplevel; git rev-parse --git-dir; git rev-parse --git-common-dir; git branch --show-current`,
write `agentfree-probe.txt` with the Write tool, read it back and run `git status --short`.

```
/d/<main checkout>/.claude/worktrees/agent-a4da6a995e78e9306
<main checkout>/.claude/worktrees/agent-a4da6a995e78e9306
<main checkout>/.git/worktrees/agent-a4da6a995e78e9306
<main checkout>/.git
worktree-agent-a4da6a995e78e9306
Write: File created successfully at: <main checkout>/.claude/worktrees/agent-a4da6a995e78e9306/agentfree-probe.txt
Read:  1	subagent wrote this
git status --short: ?? agentfree-probe.txt
```

No refusal. The subagent's worktree is under the main checkout's `.claude/worktrees/`, outside the session's tree, and
its write passed the tree guard (the lift of row 5). The probe worktree and its branch were removed afterwards
(`git worktree remove --force`, `git branch -D`). Observed, not Daoris's: 25 other `agent-*` worktrees stand there from
other sessions (a changed one is never removed by the harness); their prune is `docs/development.md`'s.

### 3. Background tasks run

Command: Bash with `run_in_background: true`, `echo bg-start; sleep 5; echo bg-end`.

```
bg-start 03:09:22
bg-end 03:09:28 pid=4111
[exited with code 0]
```

The harness returned at once, and told the session when it finished.

### 4. The browser server Daoris handed answers (row 16)

Command: `mcp__browser__browser_tabs {action: list}`. Result: four tabs listed (`chrome://new-tab-page/` twice and two
signed-in pages the person has open in Daoris's browser). The server is `browser` in `--mcp-config`, running as
`server-keeper.mjs … @playwright/mcp … --cdp-endpoint http://127.0.0.1:<port>`, a child of the harness.

### 5. The person's memory and settings are read (rows 1, 23)

- **Memory (harness):** the session's system prompt carries the harness's memory section naming its folder under
  `<account home>/projects/…/memory/`, and the session settings file holds no `autoMemoryEnabled` key (row 1 lifted).
  The folder is empty; a write to it was not made.
- **Memory file (person's `CLAUDE.md`):** `<person's home>/CLAUDE.md` does not exist. **Not proven**: there is nothing
  for `--append-system-prompt-file`/`systemPrompt.append` to carry. The protocol door's argument line above names neither,
  as it should with no file.
- **Memory (the person's own server):** `mcp__plugin_own_kobo-memory__mem_search` answered three entries. The server is
  the person's user-scope server from `<person's home>/.claude.json` `mcpServers`, handed in `<spawn>/…_own/.mcp.json`
  (type stdio, their command and environment) and running as a child of the harness
  (`Get-CimInstance Win32_Process | ? ParentProcessId -eq <harness pid>`: `kobo-memory.exe mcp`).
- **Settings:** `<spawn>/805e2924.settings.json` has the top-level keys `permissions`, `autoMode`, `hooks`, `effortLevel`,
  `modelSettings`, `autoUpdatesChannel`, `skipWorkflowUsageWarning`, `remoteControlAtStartup`, `feedbackDrafts`. The
  last six, and the `environment` lines of `autoMode`, are the person's `<person's home>/settings.json` verbatim;
  no `apiKeyHelper`, no `defaultMode`, no `enabledPlugins`, as the floor says. The account's own `settings.json` does
  not exist, the repository's sets `enabledPlugins` alone.
- Observed: `CLAUDE_EFFORT=high` in the session while the person's `effortLevel` is `xhigh`. Daoris's staff choices carry an effort of their own, which may outrank a settings value, and this model may not offer
  `xhigh`; whether the harness applied the person's value is not read here. Not a narrowing found; the effort a work
  runs on is the person's staffing.
- The account's connectors and plugins (rows 3, 4): the session settings file holds no `syncClaudeAiPlugins` or
  `disableClaudeAiConnectors`; the session lists `mcp__claude_ai_*` tools (Claude Docs, Claude Data), the account's
  connectors. The probe in check 6 listed 20 `claudeai`-scope servers on the account's home, `needs-auth` (and others connected).

### 6. The repository's `.mcp.json` servers start (row 29)

In the session itself the repository's one server is named `daoris-knowledge`, as Daoris's connector is, so one of
them is the session's and no second process starts; the session's is Daoris's (`daoris-knowledge.exe` from `--mcp-config`;
no process of `node tools/knowledge-server.mjs` has the harness as parent; the one on the machine is the person's own
run's). That cannot show the row. So the protocol door was opened the way the SDK opens it, by hand, in a scratch
folder under `_fixtures/agentfree1/` holding a `.mcp.json` with a server `repo-probe` that appends a line to a log when
it starts, with the account's `CLAUDE_CONFIG_DIR` (signed in, no approval of the folder, never trusted),
`--output-format stream-json --input-format stream-json --permission-prompt-tool stdio --setting-sources=user,project,local
--permission-mode default`, no `--bare`, no prompt sent (no model call), one `initialize` and then `mcp_status` control
requests:

```
init: success
{"name":"daoris-knowledge","status":"failed","scope":"project","error":"Connection closed"}
{"name":"repo-probe","status":"connected","scope":"project"}
claudeai connectors: 20 needs-auth; project/failed, project/connected, claudeai/connected, claudeai/needs-auth
started pid=70840 at 2026-11-11T03:10:20Z        <- the probe's own log
```

`repo-probe` was started and connected with no approval; the repository's root `.mcp.json` server (found up the folder
chain) was started too and failed only because the probe's working directory is not the repository root and its
environment was stripped. Row 29 holds on the protocol door as on the native: a `.mcp.json` server starts without a
by-hand approval. The probe ran a second harness on the account's home, which writes that home's project record for
the scratch folder (the account's home, never the person's).

### 7. The Chrome switch (row 23)

- `--chrome` is on the harness's argument line, and the session lists `mcp__claude-in-chrome__*` tools: the switch reached
  the protocol door.
- **Narrowed in practice, cause unproven:** `list_connected_browsers` → `[]` and `tabs_context_mcp` → "Browser extension is
  not connected". The person's Chrome has a native host running (`chrome-native-host.bat` of the person's home, started by
  Chrome), but the machine-wide native-messaging manifest
  (`com.anthropic.claude_code_browser_extension.json`, one per machine) now names the **account home's** wrapper, which runs
  the toolchain's harness, because a `--chrome` start under the account's home rewrote it (its file time is the session's
  start day). Two readings, neither measured: the extension signs in as another claude.ai account than the account home's;
  or the manifest's rewrite, a harness side effect on the person's browser, and the host Chrome started before it, pair the
  extension with no session. Filed as TASKS row AGENTFREE1 chrome.

## What it proves, per census row

| Row | Result |
|---|---|
| 1, 2, 4 | the session settings file names none of the three keys; harness memory section, `anthropic-skills:*` and `mcp__claude_ai_*` tools present: **proven** |
| 3 | the key is absent; no account plugin is enabled to show (`enabledPlugins` is the repository's): **key proven, effect not shown** |
| 5 | a subagent's worktree write passed: **proven** |
| 16 | the `browser` server answered: **proven** |
| 20 | `ENABLE_TOOL_SEARCH=false`, every tool listed at the start: **proven** |
| 23 | servers, settings and the plugin name `own`: **proven**; skills (as `own:*`) and the memory file: **not shown**, the person's home holds none; Chrome: **tools reach, no extension connected** |
| 29 | **proven** on the protocol door, signed in, no approval |

## Filed

See `TASKS.md` under AGENTFREE1: `own:*` unshown, the copy's size, the Chrome pairing, and the settings' effort.
