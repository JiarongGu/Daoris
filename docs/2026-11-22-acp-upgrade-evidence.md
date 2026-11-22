# ACPUPGRADE1: the agents' adapters and programs against their newest releases (evidence note)

**Carried by:** the `ACPUPGRADE1` row of `TASKS.md`. A record of these versions on 2026-11-22, not a contract: a release that
moves is read again. Release notes were read from the makers' public release pages (`agentclientprotocol/claude-agent-acp`,
`agentclientprotocol/codex-acp`, `openai/codex`, `anthropics/claude-code` `CHANGELOG.md`); the newest versions from `npm view`
and the release lists, the same day.

## 1. Where each pin stands

Read with `daoris agent list` on this machine (the pins are the machine's, in the home's toolchain folder, not this repository's).

| Agent | Held now | Newest release | Gap read |
|---|---|---|---|
| `claude-code-acp` (`@agentclientprotocol/claude-agent-acp`) | pinned 0.106.0 | 0.109.1 (`latest`) | 0.107.0 to 0.109.1, §2 |
| `codex-acp` (`@agentclientprotocol/codex-acp`) | pinned 2.6.0 | 2.9.0 (`latest`) | 2.7.0 to 2.9.0, §3 |
| `codex` (the program) | pinned 0.167.2 | 0.171.0 | 0.168.0 to 0.171.0, §4 |
| `claude-code` (the program) | unpinned; the one on `PATH` is 2.1.329 | 2.1.330 (`latest`; `stable` is 2.1.323) | 2.1.323 to 2.1.330, §5 |

The row's "Codex pinned at 0.160.0" is older than the machine: the pin had already moved to 0.167.2. `codex-acp` carries its own
`@openai/codex` (2.6.0 asks `^0.166.0`, 2.9.0 asks `^0.171.0`), so moving `codex-acp` also moves the Codex its sessions run, apart
from the `codex` pin that Daoris's sign-in and usage reads use.

## 2. claude-code-acp 0.106.0 to 0.109.1: what the notes change for Daoris

- **Config options.** 0.109.0 orders them model, effort, fast, ultracode, mode, agent, and reports Fast in category `speed` (it was
  `model_config`). Measured below (§6): the same ids and values, the order and that one category differ. Daoris reads an option by
  its category (`AcpConfigOption.ThePersons`: `model` and `thought_level`) and never by position, so neither change is read.
- **Sign-in and limits.** 0.109.1 reports an unusable Claude subscription as unauthenticated; 0.107.0 puts the turn's rate limit in
  a rate-limit failure's error; 0.107.0 says why a v2 login failed. These reach `AgentErrors` and the sign-in readings: a driven
  session must show they still land as before (§7).
- **Turn ends and steering.** 0.108.0: an autonomous result naming the original prompt no longer ends a steered turn early;
  0.108.1: a v1 turn ends at its result for a client in the subagents form; 0.107.1/0.107.2: turn ends once and the stream is read
  after setup and restart. Daoris steers and asks for the subagent form (`AcpProtocol.Capabilities`): these are the ones to prove.
- **Servers and settings.** 0.108.0 sends MCP servers to Claude Code over stdin and offers MCP-over-ACP (`mcpCapabilities.acp`
  true); 0.109.0 sends a session's settings over stdin. Daoris hands its rules as `_meta.claudeCode.options.settings` and its servers
  on `session/new`: a driven session must show both still reach the session.
- **Models.** 0.107.3 passes a model id the picker does not list on to Claude Code; 0.108.1 rejects session setup for an
  incompatible model provider. Both touch `session/set_config_option` before the first prompt (rule 43/44).
- Pins and the SDK: `@agentclientprotocol/sdk` 2.7 to 2.9.1 and `@anthropic-ai/claude-agent-sdk` 0.3.323 to 0.3.330.

## 3. codex-acp 2.6.0 to 2.9.0

- 2.7.0: delegated tasks as session messages with children referenced from tool calls (the adapter's subagent reporting changes
  shape: `AcpSubagent` and the streams are what read it); structured plans as `items plan_update` only where the client supports it;
  priority mode mapped to fast; hooks that block Codex reported.
- 2.8.0: MCP-over-ACP, ACP v2 background work and per-terminal cancel, model settings first in the session's config options,
  Codex 0.170.0.
- 2.9.0: `auth/status` on ACP v1 and v2, alignment with SDK 2.9, Codex 0.171.0.
- Measured (§6): `agentCapabilities.auth.status` appears, `mcpCapabilities.acp` is now true, and
  `sessionCapabilities.subagents` is gone. Daoris reads only `loadSession` and `sessionCapabilities.resume`
  (`AcpProtocol.cs`, `AcpOpening.cs`), so the removed key is not read; it asks for subagents as a client capability.

## 4. Codex 0.167.2 to 0.171.0

Nothing in 0.168.0 to 0.171.0 changes a flag or a sign-in word Daoris reads: the notes add model providers in the TUI and the
gateway device code for the TUI, GPT-6.2 Sol and Astra to the catalogue with Sol the default, a TUI workspace by default, `codex exec
--remote`, `codex exec` (0.169.0) keeping project configuration and MCP servers off in an untrusted directory unless `--trust`, and
fixes to turn interrupt, approval and steering ordering (0.171.0: late completions no longer cancel a newer turn's approvals;
steering preserves earlier edits in the aggregate diff), permission profiles kept across a changed working directory, and Windows
sandbox repairs. The ones to prove on a session: approvals and steering, and a permission posture. Whether `codex login`'s words
(measured on 0.160.0, D125) are unchanged is read by a sign-in, not by the notes.

## 5. Claude Code 2.1.323 to 2.1.330

The unpinned program is run from `PATH`; Daoris starts nothing from a pin there. Read for the pipe door's flags, sign-in and
limits: 2.1.328 corrects the words of the error when usage credits run out mid-response (it named a rate limit); 2.1.327/2.1.328
fix `claude -p` hangs at a stalled settings fetch and change the wait of background subagents under `CLAUDE_CODE_FORK_SUBAGENT=0`;
2.1.330 adds sign-in to organisations managed in the Enterprise Admin Console after `claude auth login`, and
`CLAUDE_CODE_FULL_SIGN_IN_LINK=1`. No flag Daoris passes (`--model`, `--effort`, the stream-json formats, the permission and settings
flags) is named as changed.

## 6. Keyless handshakes

Both adapters from each side, `initialize` and `session/new` under a scratch config home, no prompt sent, so no model was called
and no account was used. The candidate adapters were installed under this repository's ignored scratch, not in the machine's
toolchain folder; the held ones were run from where they are pinned.

- claude-code-acp 0.106.0 and 0.109.1: both answer, both offer modes `default`, `acceptEdits`, `plan`, `auto`, `bypassPermissions`,
  and options `model`, `effort`, `fast`, `ultracode`, `mode`, `agent`. Order differs (0.106.0 puts `mode` first, 0.109.1 after
  `ultracode`); `fast` is `model_config` then `speed`. `mcpCapabilities.acp` false to true; `auth.status`, `loadSession` and the
  resume, fork and list capabilities are the same.
- codex-acp 2.6.0 and 2.9.0: both answer `initialize` with the `api-key` and `chat-gpt` methods and refuse `session/new`
  with "Authentication required" (no sign-in in a scratch home). Differences: §3.

## 7. The pins moved (2026-11-23)

The owner pressed Update on each: `claude-code-acp` is pinned 0.109.1, `codex-acp` 2.9.0 and `codex` 0.171.0, each managed in the
home's toolchain folder (`daoris agent list` reads them). `claude-code` stays unpinned: 2.1.330 changes no flag Daoris passes (§5),
the program runs from `PATH`, and the adapter carries its own Claude Code (SDK 0.3.330, program 2.1.330). Each pin's why: the notes
(§2 to §5) name no change to a word, flag or capability Daoris reads, and the measurement below shows the behaviour Daoris builds on
unchanged.

## 8. Measured again at the new pins

Keylessly against the stub model, each adapter at the version it replaced and at the new pin, in one script's order
(`docs/2026-10-03-steer-evidence.md` §9 holds the table): steering mid-tool, a second prompt into a running tool, steering with
no turn, a cancel mid-tool, a model switch. Both behave at the new pin as at the old. What the notes said would differ did and
nothing else: the order of the config options and Fast's category on Claude's adapter; `auth.status` and `mcpCapabilities.acp` now
offered by both; `sessionCapabilities.subagents` gone from `codex-acp`.

What Daoris changed: `WordsAtNextStep` and `Steering` of `claude-code-acp` are measured at agent 2.1.330, adapter 0.109.1 (they were
2.1.284, 0.84.0); `WordsAtNextStep` of `codex-acp` is measured at agent 0.171.0, adapter 2.9.0 (it was declared from the
transport's default). The offers readings kept per account at 0.106.0 and 2.6.0 are read again by each account's next
`session/new`; until then `daoris agent list` names them as read at another version.
