# ACP3 — what the three harnesses actually expose (evidence note)

**Carried by:** D53 and ACP3 (in the archive). Each posture it established is the adapter's, in that
harness's own words; a record, not a contract.

> Written 2026-09-22, building **ACP3** (`docs/decisions/D53.md`; the build order is
> `docs/2026-09-21-dsh-evaluation.md` §5). **Driven, not read about**, and **keylessly**: every claim
> below was observed against an installed artefact at an exact version, with **no model call, no
> account and no credential spent**. Where a fact could only be read from a shipped bundle rather than
> from a live wire, this note says so — that is weaker evidence than a handshake and is labelled as
> such rather than rounded up.
>
> It exists because the backlog says the D37 boundary in another harness's vocabulary is
> **established, never guessed** (HARNESS2), and because `claims-need-checks` applies hardest to
> claims about somebody else's program.

## 0. What was probed, at which version

| | |
|---|---|
| Client | `tools/dsh-probes/acp-client.mjs` — DSH1's own JSON-RPC client, run with `--no-prompt` so the handshake and `session/new` happen and **no turn is ever taken** |
| dsh | `@deepseek-ai/dsh@0.1.6-alpha.2`, the DSH1 scratch prefix, `DSH_HOME` pointed at the hermetic probe home, `DSH_TELEMETRY_DISABLED=1` |
| codex-acp | `@agentclientprotocol/codex-acp@1.12.0`, installed exact into the same gitignored prefix for this note; `codex-cli 0.155.1` on the machine |
| claude-agent-acp | `@agentclientprotocol/claude-agent-acp@0.79.0` — unchanged from §1a, not re-probed |
| Frames | `_fixtures/acp3/*.jsonl`, every frame in both directions (gitignored; the findings are here) |

**Both adapter pins are still current** a day after DSH1 recorded them — `codex-acp` 1.12.0 and
`claude-agent-acp` 0.79.0 — which is the first evidence that the ACP project's release cadence is not
dsh's 1,687-commit week.

## 1. The finding that matters: the posture lives in three different places

D37's boundary — edits inside the tree proceed, nothing outward is ever auto-approved — has to be
**stated** per harness. It is not one mechanism with three spellings:

| Harness | Where the posture lives | The D37 value | How it was established |
|---|---|---|---|
| `claude-code-acp` | an ACP **mode** on the wire | `acceptEdits` | live `session/new`, DSH1 §1a |
| `codex-acp` | an ACP **mode** on the wire | `agent` | the adapter's shipped bundle (below) |
| `dsh` | **the environment**, `DSH_PERMISSION_MODE` | `workspace-write` | `dsh-base/cordis.patch.yml`, and a live `session/new` that carries **no modes at all** |

🔴 **dsh's ACP server exposes no `modes` key.** Its `session/new` result carries `sessionId` and
`configOptions` and nothing else — and the one config option is the **model catalogue**, which D24
forbids Daoris to touch. So on dsh the wire offers exactly one knob and it is the one Daoris must not
turn. An adapter written on the assumption that "the door carries the posture" would have set nothing
and reported success.

## 2. codex-acp — three modes, read from the bundle

`session/new` against an **empty** `CODEX_HOME` answers `-32000 Authentication required`, so the live
mode list cannot be had keylessly. The modes are therefore read from the shipped bundle
(`dist/index.js`, `class _AgentMode` at 1.12.0) — **artefact evidence, not a handshake**:

| id | name | approval policy | sandbox | network |
|---|---|---|---|---|
| `read-only` | Ask for approval | `on-request`, reviewed by the user | `workspaceWrite` | **off** |
| `agent` | Approve for me | `on-request`, reviewed by `auto_review` | `workspaceWrite` | **off** |
| `agent-full-access` | Full access | **`never`** | `dangerFullAccess` | on |

**`agent` is the D37 posture**, and three things follow.

- `read-only` would stall a driven session on its first edit; `agent-full-access` sets approvals to
  `never` over `dangerFullAccess`, which is precisely what D37 forbids. There is no third reading.
- `agent` is also the adapter's own `DEFAULT_AGENT_MODE`. Daoris names it **anyway**: a posture that
  happens to match a default is not stated, and a default is somebody else's to change.
- 🔴 **It is stricter than `acceptEdits`, not equivalent.** `agent` runs with `networkAccess: false`,
  which Claude Code's `acceptEdits` does not. A session that cannot reach the network fails in ways
  that look like something else entirely, so this is recorded rather than smoothed over.

## 3. Two seams that behave differently from the Claude one

- 🔴 **`CODEX_HOME` must already exist.** Pointed at a path that is not there, the adapter exits 1
  before `initialize` completes: *"CODEX_HOME points to … but that path does not exist"*. The Claude
  adapter **creates** `CLAUDE_CONFIG_DIR` (§1a watched an empty scratch directory receive
  `.claude.json` and a `sessions/` record). Same idea, opposite requirement — so whatever creates a
  credential profile for codex must create the directory, and `daoris harness profile add` already
  does exactly that.
- **codex-acp declares real `authMethods`** — `api-key` (openai) and `chat-gpt` — where the Claude
  adapter declared `[]` and dsh declares `[]`. And its logged-out refusal arrives from **the wire**
  (`session/new` → `Authentication required`), not from a subcommand Daoris asks. That is a fourth
  shape for SES3's login question, and it fits the rule rather than bending it: a definite *out* is
  the only thing that refuses, and this is about as definite as an out gets.

## 4. dsh — the posture, and the two rows

Read from `@deepseek-ai/dsh-base/cordis.patch.yml` at 0.1.6-alpha.2:

- **`DSH_PERMISSION_MODE`**, defaulting to `workspace-write`, feeds both the `sandbox-policy` row's
  `mode` and the `approval` row's policy — which derives to `ask` for every value except
  `danger-full-access`, where it becomes `never`. So `workspace-write` **is** the D37 posture: writes
  inside the workspace are sandbox-legal and proceed without asking (DSH1 probe 1 watched a `write`
  and a `git commit` land with no approval), and anything escalating past the sandbox asks — which
  over ACP arrives as `session/request_permission` and is refused by construction (D52/ACP1). Failing
  closed on escalation is the behaviour, not a limitation.
- **The disable syntax is documented in dsh's own bundle** and is an id-targeted row, not a config
  key — *"config cannot disable a row"* is stated in the comment beside the telemetry row:

  ```yaml
  - id: session-telemetry-otel
    disabled: true
  ```

- **The file to write is `$DSH_HOME/cordis.patch.yml`** — dsh names it itself as "the home-level user
  patch layer, applied over every profile's own layer". It is inside the directory the credential
  profile already **is**, so Daoris owns its location without owning a profile's contents, and it
  reaches every profile in that home rather than one.
- Both outbound rows are present and on: `session-log-deepseek` in the base bundle's row list, and
  `session-telemetry-otel` exporting to `https://harness-telemetry.deepseeksvc.com/v1/logs` under
  `FEEDBACK_ONLY`. `DSH_TELEMETRY_DISABLED` (any non-empty value) opts the process out of the second
  one only; the first needs the patch.

## 5. HELP2 — `customSkillDirs` works, and it resolves at the wrong moment

`@deepseek-ai/dsh-skill-filesystem` takes `customSkillDirs: string[]` at rank 300, between the project
roots and the user roots, and its bundle format is `<name>/SKILL.md` — **exactly** Daoris's layout, so
nothing on an adopter's disk changes. But:

🔴 **The default project roots are resolved per session `cwd`; `customSkillDirs` is resolved once, at
construction, against the process's own cwd** (`.map((root) => resolve(root))`). So a relative
`.claude/skills` becomes one fixed absolute path for the life of the process.

For Daoris that is correct rather than merely tolerable, because the driver spawns **one process per
tree** with `WorkingDirectory` set to the tree (D51 — the tree is the unit). It stops being correct
the moment one dsh process serves two trees, which its own `session/list` shows is possible: that call
answered with **every session in the home**, across cwds. The constraint is therefore written down
here rather than discovered later by a skill that silently never fires.

## 6. What this note does not establish

- **No turn was taken against any of the three.** Every probe used `--no-prompt`. A real driven run
  per harness is ACP3's own proof and the owner's to authorise, exactly as ACP2's is.
- **codex's mode list is bundle evidence, not wire evidence.** It is confirmed on the wire by the
  first `session/new` under a logged-in `CODEX_HOME`, which is the same step as the driven run.
- **dsh was not driven to a commit over ACP here.** DSH1 probe 1 did that headlessly on a scripted
  provider; this note only establishes the door's shape and the profile's contents.
