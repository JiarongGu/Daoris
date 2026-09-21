# The dsh evaluation — evidence note (DSH1)

> Written 2026-09-21, executing `docs/2026-09-21-dsh-direction.md`. **Driven, not read about**: every
> probe below was run against an installed `dsh` over a scratch repository, and what is quoted is what
> was observed. Where a probe could not be run to its pass shape, this note says so and why — a
> half-evaluated option is recorded as exactly that. The decision this feeds is **D53** in
> `docs/DECISIONS.md` (proposed here; the owner's call). The option space, the boundary inventory and
> the probe list are the plan's and are not restated.

## 0. What ran, at which tier, on what

| | |
|---|---|
| dsh probed | `@deepseek-ai/dsh@0.1.6-alpha.2` from npm (dist-tag `alpha`; `latest` was `0.1.5-rc.2`), pinned exact into a gitignored scratch prefix: **260 packages, 561 MB**. The read-only local checkout stands at the same version (`ddefc45`, tag `dsh-v0.1.6-alpha.2`) and was never built — building it writes into another repository |
| Hermetic state | `DSH_HOME` pointed at a scratch home for every run; telemetry disabled by environment; a scratch git repository with its own commits; nothing under `~/.dsh`, no real profile, no real repository touched |
| The model | **No key was supplied for the session**, so every loop probe ran on a **scripted provider** — a small OpenAI-compatible server written for this evaluation that answers a fixed plan of tool calls and text, declared to dsh as a custom route in dsh's own `settings.yaml` (which is also D24's seam: the harness owns the model, Daoris names none). That tier proves the harness's *mechanics* — tool dispatch, sandbox, hooks, approvals, the durable record, exit codes, the wire — and dsh's own testing policy draws the same line ("a no-key test proves plumbing"). What it cannot prove is that a real model completes a quest-shaped task under dsh; DRV4's shape for dsh waits for a key, and every verdict below is stated within that limit |
| Machine | Windows 11; Node 24.15; **no PowerShell 7** — dsh's executor fell back to Windows PowerShell 5.1, its documented last resort; `claude` 2.1.278 (logged in); `codex` 0.155.1; pnpm 12.5.1 through corepack; the session ran elevated, which matters once below |

**Two corrections to the plan's survey, from the tree, before any probe ran:**

- **The Claude Code delegation is the real product but not the machine's copy.** `subagent-claude-code`
  runs the *pinned Agent SDK with its own bundled CLI payload* — "never falls back to the host `claude`
  executable" — so a dsh delegation runs a *different* Claude Code binary than the one `daoris harness`
  manages, reading the host's native settings and login.
- **The hook bridge is a compatibility subset and says so**: 7 of Claude Code's 30 hook events, each
  partial; `PreToolUse allow` does not pre-approve; `transcript_path` is always empty; one config path
  per process, no layered discovery; `continue: false` is recorded and not honoured.

**Two default-on rows in the base bundle that belong in the boundary reading**
(`packages/bundle/base/cordis.patch.yml`): `session-telemetry-otel` (`FEEDBACK_ONLY`, to a
DeepSeek-hosted collector; off only by environment) and `session-log-deepseek`, which **uploads the
canonical session log as a `dsh_session_log` field on every official-route model request**
(`enabled: true` in shipped profiles). A first run also writes `$DSH_HOME/.anonymous-user-id`. None of
it is hidden — each is documented — but D47 §4 says transcript-class material never leaves the machine,
so a Daoris deployment on dsh's official route would patch both rows off or accept them out loud. On the
custom route used here, **the request carried nothing beyond `store`** (the scripted server logged every
field the harness sent).

## 1. The probes

### Probe 1 — the headless run · tier: scripted · **pass shape met**

*Pass: DRV4's shape — a commit, a readable durable record.*

Five runs, each teaching something:

1. **The shipped default route speaks an Anthropic-style Messages protocol** (`POST /v1/messages`), not
   chat-completions: `HTTP_404` in 2.2 s, exit 1, and the JSON stream still closed properly (`turn_end`
   carrying the error, then `final` with empty text). The exit-code contract held.
2. **dsh makes model calls the agent loop did not ask for.** With the custom route, the loop ran and the
   `write` tool landed the file — but a *second* request arrived at the scripted server: the session-title
   generator ("Generate the session title from this JSON array of human messages…", its own 363-char
   system prompt, no tools), and it consumed the plan's commit step. The session log records it as
   `session/title` (a fallback title first) then `session/title-llm-request` and a provider title.
   Compaction is another such caller. **Anything that accounts for "which model ran, how often" must
   count these.** The server was taught to answer tool-less requests without consuming the plan.
3. **The Windows sandbox changes the process token in a way git notices.** `git commit` inside the
   `pwsh` tool failed with `fatal: detected dubious ownership … owned by BUILTIN/Administrators … but the
   current user is <user>`. The scratch directory had been created by this elevated session, so its owner
   was the Administrators group; under dsh's `WRITE_RESTRICTED` token the admin group no longer clears
   git's ownership check. Recorded as a fact about the sandbox's token, not as a dsh defect — a
   non-elevated driver would not create admin-owned checkouts.
4. **dsh's credential scrub drops any environment name that looks like a key.** Passing git a
   `GIT_CONFIG_COUNT/GIT_CONFIG_KEY_0/GIT_CONFIG_VALUE_0` triple through the harness environment
   reached the child as `GIT_CONFIG_COUNT` alone ("error: missing config key GIT_CONFIG_KEY_0"). dsh's
   own source says so: *"The subprocess credential scrub removes ambient `GIT_CONFIG_KEY_n` entries"*
   (`packages/deliverables/workspace-changes/src/git.ts`), and another package names the rule as
   `*KEY*`/`*SECRET*`. An adapter carrying `DAORIS_*` variables is unaffected; anything with KEY, TOKEN or
   SECRET in its name is not.
5. **With the directory owned by the user: exit 0 in 3.7 s.** The JSON stream, verbatim in order:
   `session` (id + cwd) · `turn_start` · `step_start` · `tool_call write` (the exact path and content) ·
   `tool_result completed` ("Created file") · `step_end` with usage · `step_start` · `tool_call pwsh`
   (`git add -A; git commit …; git rev-parse --short HEAD`) · `tool_result completed` (`896efaf`) ·
   `step_end` · `step_start` · `text` · `step_end` · `turn_end {kind: completed}` · `final`. `git log`
   shows `896efaf probe: dsh headless run landed probe.md`, one file, three insertions.

**What the harness sent, every time**: a 4,254-character system prompt; the user turn followed by a
runtime-context snapshot ("Current DSH file policy: workspace-write…") and a `<system-reminder>` with a
skills catalogue; **24 tools** (`create_goal edit exit_plan_mode get_goal glob grep interrupt_agent
job_kill job_list job_output list_agents pwsh read read_image send_message skill subagent
subagent_fork todo_write update_goal web_fetch web_search workflow write`); `User-Agent:
deepseek-harness/0.1.6-alpha.2`.

**The durable record**: `$DSH_HOME/sessions/<cwd-slug>/<session-id>/session.v3.jsonl.zstd` — JSONL,
**one Zstandard frame per flush** (Node's `zlib` decodes it once the frames are split on the magic), plus a
projection-cache JSON beside it. Thirty events for the run above, and the first three of every session are
the permission facts: `permission/preset {workspace-write}` · `sandbox/mode {workspace-write}` ·
`approval/policy {ask}`; then the inbox splice, `turn/start`, `step/start`, `system/message`, the user
messages, **`request/header`** (the exact provider, model, `maxTokens` and the full tool roster the
request carried), `request/context` (`contextWindow`), the title events, `assistant/message` (with the
embedded compact stream), `tool/call`, `tool/result` (with `isError`), `step/end`, `turn/end`. It is a
strictly richer record than anything Daoris observes from a pipe — and it is *self-reported*, which is
the D46 point.

### Probe 2 — the ACP session · tier: scripted · **pass shape met**

*Pass: the working surface's timeline could render from these events with no stdout parsing.*

**Read first** (`packages/acp/acp`): the server is **automation-only by decision** (their 2026-07-23 note
removed it as an editor UI); ACP v1 plus `session/list|resume|close`; **no authentication**
(`authenticate` "immediate success"); one prompt in flight per session; updates carry only **committed**
facts — never token deltas, plans, titles or terminal metadata; stop reasons map `completed → end_turn`,
`interrupted → cancelled`, and **`aborted | blocked | error → end_turn`** (a hook-aborted or errored turn
reads as an ordinary end on the wire; the session log keeps the truth). `session/request_permission` is a
machine policy channel — allow-once, reject-once, cancel — never a durable grant.

**Run**: a JSON-RPC client of this evaluation's own spawned `dsh --profile acp` over stdio (the ACP row
patched onto the scripted route) and logged every frame. Observed:

- `initialize` → `protocolVersion: 1`; `agentCapabilities`: MCP over http, prompt image/audio/embedded
  context all false, `sessionCapabilities: {close, list, resume}`.
- `session/new {cwd, mcpServers: []}` → a session id **and a `models` configuration option carrying the
  whole provider catalogue** — the current value `["mock","mock-model"]` beside the DeepSeek entries. The
  wire hands a client the model picker; D24 says Daoris must not use it.
- `session/prompt` → five updates in **276 ms**: `usage_update {used: 7783, size: 65536}` ·
  `tool_call {toolCallId, title: "read", kind: "other", status: "in_progress", rawInput}` ·
  `tool_call_update {completed, content}` · `agent_message_chunk` · `usage_update {7878}` → result
  `stopReason: end_turn`.
- `session/list {}` → **every session in the home, including the headless ones**, each with its `cwd`. The
  home, not the profile, is the unit of session visibility.
- `session/close` → `{}`; stdin EOF → the server exited 0 on its own.

**Mapped onto the driver's needs**: *spawn* is a process (unchanged); *observe* stays exit code + quest
state (unchanged, D46); *turn* is `session/prompt` — a real API instead of a line written to stdin;
*stop* is `session/cancel` then stdin EOF, and the exit is `completed`-shaped. **Mapped onto the
timeline's needs**: tool boundaries with ids, inputs and outcomes; the turn boundary (the prompt result);
thoughts; context pressure. **Not on the wire**: step boundaries (in the log only), and the honest end
reason of an aborted or blocked turn. The pass shape holds — no stdout was parsed — with that one
flattening named.

### Probe 3 — Claude Code over ACP · tier: binaries + field + handshake

*Decides whether B is "one protocol" or "one protocol plus the old pipes".*

- **`claude` speaks no ACP** (2.1.278: no such surface in its help). **`codex` has `app-server`** — its
  *own* JSON-RPC protocol with TypeScript and JSON-schema generators, not ACP; dsh's `subagent-codex`
  drives exactly that.
- **The adapters exist, are current, and are the ACP project's, not the vendors'.**
  `@agentclientprotocol/claude-agent-acp` **0.79.0** (published 2026-09-17, Apache-2.0; the renamed
  `@zed-industries/claude-code-acp`) implements ACP **over the Claude Agent SDK**; pre-built binaries
  exist for Windows; documented gaps "due to SDK constraints" (Plan Mode, `/compact`).
  `@agentclientprotocol/codex-acp` **1.12.0** (same day, Apache-2.0) does the same for Codex. dsh itself
  can sit on either side: its `acp` profile is a server, `subagent-acp` a client of any ACP agent.
- **Handshake, run here** (`claude-agent-acp@0.79.0` pinned into the scratch prefix; 87 packages;
  `@agentclientprotocol/sdk 1.4.0`, `@anthropic-ai/claude-agent-sdk 0.3.274`): `initialize` answered in
  0.6 s with a *richer* capability set than dsh's — `loadSession`, `fork`, `delete`, `list`, `resume`,
  `additionalDirectories`, `subagents`, image and embedded-context prompts, MCP over http and sse, plus
  `_meta.claudeCode.promptQueueing` and a JetBrains extension block; `authMethods: []`. `session/new`
  then **failed: "Claude native binary not found for win32-x64 … or set `CLAUDE_CODE_EXECUTABLE`"** — the
  SDK's optional platform payload had not landed. That error is the useful part: the adapter has an
  **executable seam**, so it can be pointed at the machine's own managed `claude` instead of carrying a
  second copy. *(The seam test — the machine's `claude` plus an empty `CLAUDE_CONFIG_DIR`, no prompt —
  is recorded in §1a.)*

**Verdict.** B is *one protocol through two third-party adapters*. Neither Anthropic nor OpenAI ships
ACP; the Claude adapter is Apache-licensed, moves fast (0.79 the week of writing), and runs the Agent SDK
— either its own CLI payload or, through `CLAUDE_CODE_EXECUTABLE`, the `claude` the toolchain already
manages. Sources: [claude-agent-acp](https://github.com/agentclientprotocol/claude-agent-acp) ·
[Zed's ACP page](https://zed.dev/acp) · [codex-acp](https://github.com/agentclientprotocol/codex-acp).

### Probe 4 — the hook-config bridge · tier: scripted · **pass shape met, with a Windows finding**

*Decides whether a dsh session honours the repository's existing D37 wiring.*

The scratch repository carries `.claude/settings.json` with one `PreToolUse` command hook — a Node
script refusing any shell command that would `git push` — and the bridge is mounted by a `--patch`
overlay naming that file. Three runs:

1. **The hook fired at the right point and its evidence log crashed**: `hook/invoked` at `PreToolUse`,
   then `hook/result {decision: "pass", exitCode: 1, stderrSummary: "node:fs … writeFile…"}`. **Hooks run
   inside the sandbox**: the script tried to write beside itself, outside the workspace, and was denied.
   Exit 1 is "a non-blocking failure: the action proceeds" — so the push went through to git.
2. **Exit 2 did not block.** With the log moved inside the workspace, the record read
   `hook/result {decision: "pass", exitCode: 1, stderrSummary: "refused by the repository hook: a push
   leaves the repository…"}` — *the hook's own reason, under exit code 1*. Verified directly on this
   machine: `powershell -Command "node -e 'process.exit(2)'"` returns **1**; only an explicit trailing
   `exit $LASTEXITCODE` returns 2. **Windows PowerShell 5.1 collapses a native command's exit code, and
   dsh's Windows executor is 5.1 when PowerShell 7 is absent** — so the exit-2 form of a Claude Code
   hook cannot block through the bridge here. PowerShell 7's behaviour was not verified (not installed).
3. **The structured form blocks.** The same hook answering on a clean exit with
   `{"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "deny",
   "permissionDecisionReason": …}}` — the shape Claude Code itself accepts — produced
   `hook/result {decision: "deny", exitCode: 0}` and a tool result `status: error, "Error: refused by the
   repository hook: a push leaves the repository and is not the session's to do (D37 boundary)"`. The
   harmless command in the same run passed (`## main`). The payload the hook received:
   `session_id, transcript_path: "", cwd, hook_event_name: "PreToolUse", tool_name: "pwsh", tool_input,
   tool_use_id`; `CLAUDE_PROJECT_DIR` was set.

**Verdict.** A dsh session honours a repository's existing hooks for the events it supports, with the
subset caveats of §0 — and, on Windows without PowerShell 7, only hooks that block *structurally*. That
second point is a fact about hooks under a 5.1 executor, not about dsh alone, and it is worth carrying
into any adapter that promises "the repository's own configuration governs".

### Probe 5 — the subagent delegation · tier: composition · **held at the edge, deliberately**

*Establishes whether dsh-as-orchestrator composes with the products we already support.*

- **Install**: `dsh plugin --profile headless add @deepseek-ai/dsh-subagent-claude-code` (pnpm through a
  corepack shim, 177 s) resolved **`0.0.1-rc.1`** — the only version npm has, while the CLI is at
  `0.1.6-alpha.2` — and dsh warned: *"declares no `dsh.bundle` — installed as a plain dependency, not a
  profile layer."* It still pulled `@anthropic-ai/claude-agent-sdk-win32-x64@0.3.220` (85 MB): **262 MB**
  into the profile for a second Claude Code.
- **Composition**: with the provider and tool rows inserted by hand (as its README prescribes for a
  preset), the profile loaded and the scripted server saw **`subagent_claude_code` in the advertised
  roster** beside the 24 base tools. So the seam composes even from the stale package.
- **Not run: the delegation itself.** Completing it would spend the person's own Claude account through
  a login they did not hand to this session; the plan conditioned loop probes on a key supplied at
  session time, and none was. What a real run would add is small: the README already fixes that the
  parent sees only the child's final text, that permission prompts are denied unattended, and that the
  child reads the host's settings.

### Probe 6 — the approval map · tier: scripted · **pass shape met**

*Pass: a configuration exists in which nothing outward-facing is auto-approved.*

dsh's line is **two knobs and a preset**: `sandbox-policy` mode (`read-only` — the package's fail-safe
default — `workspace-write` — the shipped profiles' default, `DSH_PERMISSION_MODE` overrides — or
`danger-full-access`), `user-approval` policy (`ask`, the default, **fails closed to `unavailable` when no
answerer is composed**, which headless does not compose; `never` rejects deterministically — "the strict
headless stance"), and `permission-presets` bundling them, pinned per fresh session. A tool denied by the
sandbox may retry **once** with `sandbox_permissions: workspace-write | danger-full-access` and a
`justification`, through the approval seam. **Only one-shot grants exist.**

Observed: a `pwsh` write to `$env:USERPROFILE` under `workspace-write` → `Set-Content : Access to the
path '…' is denied` (the Windows ACL restricted token), `Test-Path` → `False`. The same command with
`sandbox_permissions: danger-full-access` → `approval/asked {reason: "escalate sandbox to
danger-full-access: …"}` · `approval/decided {outcome: "unavailable"}` · tool result
`status: error, "Error: sandbox escalation to \"danger-full-access\" requires approval, but no approval
channel is available"`. The file does not exist. Verified against the world, not the self-report.

**Where D37's line lands.** dsh's vocabulary is *file effects* and *escalation*. It has **no notion of
outward-facing**: a `git push` is a sandbox-legal shell command inside the workspace, and the network is
outside `SandboxMode`'s vocabulary by their own statement. So the D37 boundary Daoris needs — a push, a
publish, a release stay human — is expressible under dsh only through the repository's **hooks** (probe 4)
or the prompt, exactly as with `claude-code` today. The pass shape holds for what dsh governs; the rest
is the repository's, as it already is.

### Probe 7 — stability and pinning · tier: history

| Fact | Measured |
|---|---|
| Release cadence | **26 prerelease tags** 2026-08-10 → 09-17 (five and a half weeks) — one every one to two days; no stable release; npm `latest` lags `alpha` by a week |
| One week of churn (`0.1.5-rc.2` → `0.1.6-alpha.2`) | **1,687 commits**; 5,420 files; +878k / −64k lines whole-tree (vendored and generated material included); **28 `package.json` files added or removed** under `packages/` |
| Same week, on the surfaces Daoris would bind to (CLI args, ACP, headless, hooks, `subagent-acp`, `subagent-claude-code`) | **61 files, +2,488 / −695**; the ACP update/codec and headless JSON-stream files changed in 12 commits |
| Session format | writer **0 → 2 → 3** in five weeks; 25 archived transitions, 15 with changed roots; their own archive: "historical version 0 did contain structural changes without version increases" |
| The npm surface vs the tree | the CLI is published daily; **`subagent-claude-code` is not** — npm holds `0.0.1-rc.1` (August) against a tree at `0.1.6-alpha.2` |
| Stated posture | README: *THERE WILL BE COMPATIBILITY-BREAKING CHANGES*; root `AGENTS.md`: "Public APIs are pre-stable; update every consumer" |
| What a pin costs | `--save-exact` in a scratch prefix: 561 MB / 260 packages per machine, plus 262 MB per profile that delegates to Claude Code. Machine tooling in D49 §4's sense, never repository doctrine |

**Reading.** Bind to **the standard, not the product**. ACP v1 is a protocol dsh implements and the
probe-3 adapters implement too, and it does not move when dsh does; the wire seen in probe 2 is the
standard's, not dsh's. Binding to `dsh headless --json`'s event shape, to dsh CLI flags or to its
session-log format binds to surfaces that changed in 12 commits last week. Everything else about the
preview — the plugin API, Cordis, the profile layout — is only Daoris's problem under options C and D.

### Probe 8 — the plugin cost · **not run**

Only if C stayed live after 1–7, and it does not (§4). The tree still says what it would cost: a plugin is
a TypeScript module exporting `apply(ctx)`, mounted by absolute path in a patch overlay (their tutorial is
a dozen lines); a *Web UI* plugin is a client package inside their Cordis client runtime with locale-owned
copy and their slot system. The cost was never the plugin — it is living inside Cordis, inside a preview
that moved 1,687 commits last week, with two outbound rows to keep patched off.

## 1a. Addendum — the two seams of the Claude adapter, tested keylessly

Run: the same handshake with **`CLAUDE_CODE_EXECUTABLE` set to the machine's managed `claude`** (the
binary `daoris harness` already knows) and **`CLAUDE_CONFIG_DIR` set to an empty scratch directory**,
no prompt sent. Observed:

- `initialize` in 1.4 s; **`session/new` succeeded** (`sdk-initialize` 1,295 ms of it) and returned
  `modes` — **Claude Code's own permission modes on the ACP wire**: `default` ("Manual — always ask"),
  `acceptEdits`, `plan`, `auto`, `bypassPermissions` — plus `configOptions` (mode, model) and an
  `available_commands_update`. The driver's `acceptEdits` posture is therefore expressible over ACP
  as a mode, not as a command-line flag.
- **The config-dir seam passes through.** The empty profile directory received `.claude.json`, a
  `backups/` entry and a `sessions/` record from the CLI the adapter spawned — and nothing was written
  to the machine's real profile. The adapter's own log line: *"session account carries no identity
  signal"* — the profile is logged out, as it should be. `session/list` answered `[]` (profile-scoped,
  where dsh's answered the whole home).
- **No model was called.** The machine's real `claude auth status` still reads logged in and was not
  spent; the prompt step was withheld on purpose.

**What this settles for option B**: the Claude adapter can run *the* `claude` the toolchain manages,
under *the* credential profile the toolchain chose, and Daoris's permission posture is a mode on the
wire. What it leaves for ACP2: a real turn under a logged-in profile, and the permission answerer
against Claude Code's real requests.

## 2. The boundary inventory, observed

| Boundary | What was seen |
|---|---|
| **D1 / D24** — no model in Daoris, none named | Held under A and B by construction: which model answers is `$DSH_HOME/settings.yaml` (the custom route) or a profile patch, and dsh writes the exact route into every record's `request/header`. **dsh makes model calls of its own** (title generation, compaction), so "which tier answered" for a dsh session is plural and the record shows each. The ACP wire hands a client the model catalogue; Daoris must not use it |
| **D8 / D33** — the CLI | Untouched under every option |
| **D23** — adapters on proof | A is `ISessionAdapter` as written. **B re-cuts the seam**: a protocol door beside the pipe door — a design change with a decision, and D23's "on proof" then means a real driven run over ACP, which this session could not spend for |
| **D37** — the approval boundary | dsh: file-effect sandbox + fail-closed one-shot approval; **no outward-facing notion**. The boundary is the repository's hooks or the prompt — as today. On Windows without PowerShell 7, an exit-2 hook does not block; a structured deny does |
| **D46 / D47** — observed lifecycle; streams never leave | ACP updates and the session log are self-reports, richer than observation; they may *enrich* a record and must not move it. The record still moves on exit code + quest state. Two default-on rows send material off the machine **on the official route**; on a custom route the request carried nothing extra. The ACP server binds nothing but stdio; a `web` profile binds `127.0.0.1:3080`; `session/list` shows every session in the home |
| **D49 §3** — no Daoris chat loop | Unchanged under A/B/C: dsh or the adapter owns the loop |
| **D51** — the tree is the unit | Unchanged; `session/new` takes an absolute `cwd`, which is the tree |
| **Credential profiles (D49 §4)** | `DSH_HOME` isolates dsh's credentials (`.credentials.yaml`), settings and sessions as one directory — the same shape as `CLAUDE_CONFIG_DIR`; `subagent-acp` insists a nested dsh child gets its own. dsh has no login command and no "am I logged in" question — presence of a credential for a route is the answer, and the toolchain's login check would be `unknown`, which is permissive by SES3's rule |

## 3. DOCS1 — the documentation system, compared

dsh's documentation is code-generated and gated by ~60 `verify-*` scripts. What converges with Daoris
is D17-grade evidence (two ecosystems arriving at one doctrine independently); what does not is listed
with a take/leave and a reason. No code was ported, so no notice is owed.

| dsh mechanism | Daoris today | Verdict |
|---|---|---|
| **One home per fact — a tier taxonomy** (standing orders / architecture map / subsystem references / decision notes / postmortems / cookbooks / package contracts), placement rules per fact kind | D7 tier-is-the-directory; `CLAUDE.md` as the router; `docs/DECISIONS.md`, `FIX-LOG.md`, `task-archive.md`, design contracts | **Converged.** Same doctrine, different names |
| **Per-document word budgets in a manifest**, `verify-doc-budgets` (48 lines), a *relocate → condense → raise* discipline, "a too-low ceiling is a budget bug", 5% headroom at target | D28: a byte budget on the *always-loaded core* only, with the same split-not-raise discipline; no budget on `CLAUDE.md`, the backlog's handover, or the design documents — all of which have grown by accretion | **Adopt as a devkit gate** (DOCS2): the same principle Daoris already holds for the canon, extended to its own always-read prose. `CLAUDE.md` is this repository's root standing-orders file and is the first candidate |
| **Fenced `ts` blocks must compile** (`doc-typecheck`, 231 lines) and **`type-equiv` manifests** so a pasted declaration cannot drift (346 lines) | `claims-need-checks` (verify prose against the implementation, by hand) | **Leave for now, note the shape.** Daoris's docs carry few code fences; the day a design document pastes a C# or TS declaration, a type-equivalence check is the right gate and this is the model |
| **Notes lifecycle** `proposed/ → implemented/ → rejected/`, frozen `archived/`, a format gate (header, `## Problem`, **mandatory `## Alternatives considered`**), implemented notes kept current with what shipped | The numbered decision log with amendments, "Rejected:" lines by convention, the backlog as the proposed tier, the archive as history | **Converged in substance; one small take**: assert the "Rejected" line mechanically — a D entry without its alternatives invites re-litigation, which is exactly what both records exist to prevent. Cheap to gate |
| **Generated reference regions, freshness-gated** (`--check` variants of every generator) | `RULES_INDEX.md` generated by `daoris`; `release-prep --check` for version agreement | **Converged** |
| **Model Experience section per package** — what a package contributes to the model's context and its cache effect | The canon index rows carry `applies_when` / `enforces`; D28 budgets the bytes | **Converged in intent**; the cache-effect framing is theirs and worth a sentence in `canon-authoring` when the next rule is written |
| **The slop checklist** (duplicated rules, history outside its tier, status annotations, hand-restated catalogues, reasoning transcripts, emphasis inflation) | Nothing equivalent | **Adopt into `post-feature`** as a documentation pass — it is a review checklist, not a gate |
| **Markdown link and wrap gates** (`verify-md-links`, one-line paragraphs) | Nothing | Links: **cheap devkit gate** with DOCS2. Wrap: leave — Daoris hard-wraps at 100 on purpose |
| **Bilingual pairing with a terminology contract** (`.zh.md` twins, sidecar consistency records, a translation skill) | `Daoris.Web` speaks en + 简体中文 with a parity gate (D42); docs are English | **Leave.** The platform already holds the rule where it matters; doctrine in two languages is a decision for the owner, not a gate to port |

**Take-aways as one backlog item** (DOCS2): a doc-budget manifest and a link check as devkit gates, the
"Rejected" assertion on new decision entries, and the slop checklist folded into `post-feature`.

## 4. What the evidence says about the options

**A · dsh as a third harness — feasible today, worth little on its own.** Every mechanic an
`ISessionAdapter` needs was observed: `dsh headless` takes the target (positional or stdin), exits 0/1 by
turn outcome, streams JSON events, records durably, and its sandbox + approval posture fails closed with
no answerer composed. But the driven shape's *interactive* half is not a stdin/stdout pipe — one task per
run — so `Interactive` would be false, or would be ACP (which is option B). Its toolchain entry is honest
but thin: `npm i -g @deepseek-ai/dsh@<exact>` (561 MB), `dsh --version`, `DSH_HOME` as the profile seam,
and **no login question to ask**. A adds a multi-provider agent option and nothing structural.

**B · ACP as the driver's session protocol — the structural gain, proven on the wire.** Probe 2 is the
timeline's structured source without parsing anything: tool boundaries, turn boundaries, thoughts, usage,
by contract. Turn-taking becomes a real API. The same wire reaches dsh natively and Claude Code and
Codex through the ACP project's adapters (probe 3), with the Claude adapter pointing at the managed
`claude` through its executable seam. What B costs: **re-cutting the seam** (a protocol door beside the
pipe door — D23 evolves); a **permission answerer** in the driver for `session/request_permission`
(fail closed, the repository's checked-in posture — never wider, D52's rule); accepting that
`aborted | blocked | error` flatten to `end_turn` on the wire (the record still moves on exit + quest
state, so nothing is lost where it matters); and **two fast-moving Apache adapters** on the machine's
toolchain. What B does *not* change: observation (D46), no model named (D24), the tree as the unit (D51),
streams desktop-only (D47 §4). It keeps the pipe door: `claude -p … --permission-mode acceptEdits` stays
the supported path until an ACP driven run has proven itself — "one protocol plus the old pipes" for a
while, by design, not by accident.

**C · the working surface as a dsh web deployment — rejected by the boundary reading.** Their web profile
is a full product: a model picker (D24), a workspace picker (D48), plugin management, its own visual
identity (D41), a bound port with two default-on outbound rows, on a developer preview that moved 1,687
commits last week. D38's one UI would become two in fact; D31/D47 would have to be re-proven inside
someone else's host on every upgrade. Its *structure* is already adopted (D52 as amended) — that was
the part worth having.

**D · full re-platform — rejected; it reopens D1, and says so.** Daoris is process tooling, not an LLM
product; the family layer (canon, quests, registry, convergence, coexistence, the zero-dependency CLI) has
nothing to gain from Cordis, and the CLI stays untouchable regardless. The pre-stable plugin API and the
churn would put the fastest-moving part of the stack under the slowest-changing judgement.

**Recommendation to the owner** (D53, proposed): **B, with A folded into it** — dsh arrives as an *ACP
configuration* of the protocol door, not as a hand-built adapter, and HARNESS2's codex adapter arrives
the same way. The design work is the seam; the proof is a real driven run over ACP, which needs the
owner's login and is the first build item's closing step.

## 5. Proposed build order (conditional on D53 as proposed)

| Item | What lands | Proof |
|---|---|---|
| **ACP1 — the protocol door** | `ISessionProtocol` beside `ProcessStartInfo` on the adapter seam: a JSON-RPC client over the spawned process's stdio; `session/new` on the tree; the composed target as `session/prompt`; updates teed into a **structured** console stream beside the verbatim one (desktop-only, D47 §4); `session/request_permission` answered by the D37 posture; records still move on exit + quest state | A **stub ACP agent** (the mirror of this evaluation's client) in the family rehearsal — no model, D46 §8's shape |
| **ACP2 — `claude-code` over ACP** | `@agentclientprotocol/claude-agent-acp` pinned exact as a managed toolchain entry; `CLAUDE_CODE_EXECUTABLE` → the managed `claude`; `CLAUDE_CONFIG_DIR` → the profile; the permission answerer against Claude Code's real prompts | The **real driven run** (DRV4's shape) — the owner supplies the login. This is D23's "on proof" |
| **ACP3 — dsh and codex as configurations** | `dsh --profile acp` with `DSH_HOME` as the profile, its model in the profile's own settings (Daoris names none), the two outbound rows patched off in the profile Daoris owns the location of; `codex-acp` likewise. HARNESS2 closes into this | The stub proves the door; a driven run per harness proves the harness |
| **SURF4c gains its structured source** | The timeline renders ACP tool lifecycle, turn boundaries, thoughts and usage — the protocol D52 said stdout parsing was not | Existing component loops |
| **SURF4a–d resume** | On `Daoris.Web`, as designed; C is closed | As designed |
| **DOCS2** | §3's take-aways as gates | The gates watched failing first |
