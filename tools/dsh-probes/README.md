# dsh probes — the instruments behind `docs/2026-09-21-dsh-evaluation.md`

Four small Node scripts, zero dependencies, kept so the evaluation is reproducible and so ACP1 has a
seed: the ACP client here is the mirror of the stub ACP agent the family rehearsal will need. None of
them is a gate, none runs a model, and none is shipped.

| Script | What it is |
|---|---|
| `mock-llm.mjs` | A scripted OpenAI-compatible chat-completions server. Each agent-loop request consumes one step of a plan (a tool call by name pattern, or a final text); tool-less auxiliary requests (session titles, compaction) are answered without consuming a step; every request's model, tool roster, message tail and extra fields are logged. It also records, and refuses, requests on an Anthropic-style `/messages` route, which is what dsh's official DeepSeek adapter speaks |
| `acp-client.mjs` | A minimal Agent Client Protocol client over an agent's stdio: `initialize`, `session/new`, `session/prompt` (or `--no-prompt`), `session/list`, `session/close`; answers `session/request_permission` by policy (`--permission allow|reject`); logs every frame in both directions as JSONL |
| `read-dsh-session.mjs` | Decodes a dsh session log (`session.vN.jsonl[.zstd]` — one zstd frame per flush) into one line per event, using Node's own `zlib` |
| `hook-deny-push.mjs` | A Claude Code `PreToolUse` command hook that refuses a shell command containing `git push`, by the structured `permissionDecision: deny` form; `HOOK_BLOCK_BY_EXIT_CODE=1` switches to the exit-2 form, which the evaluation showed does **not** block under a Windows PowerShell 5.1 executor |

## Reproducing a probe

Everything runs under a scratch harness home and a scratch git repository — never a real one:

```sh
# a plan: one step per agent-loop request
# { "steps": [ { "tool": { "match": "^write", "args": { "file_path": "probe.md", "content": "…" } } },
#              { "tool": { "match": "^pwsh$|^bash$", "args": { "command": "git add -A; git commit -q -m probe" } } },
#              { "text": "Done." } ] }
node tools/dsh-probes/mock-llm.mjs --port 8765 --plan plan.json --log requests.jsonl

# dsh's own settings.yaml in the scratch home declares the route (the harness owns the model, D24):
#   llm-pi-ai: { providers: { mock: { apiKeyEnv: MOCK_API_KEY, api: openai-completions,
#                baseURL: http://127.0.0.1:8765/v1, models: [ { id: mock-model } ] } } }
#   agent-default-model: { provider: mock, model: mock-model }
DSH_HOME=<scratch home> MOCK_API_KEY=mock-key DSH_TELEMETRY_DISABLED=1 \
  dsh headless --json "Create probe.md and commit it."

# the ACP session, frames logged
node tools/dsh-probes/acp-client.mjs --cmd dsh --args '--profile|acp' --cwd <scratch repo> \
  --prompt "Read README.md." --log frames.jsonl --permission reject

# the durable record
node tools/dsh-probes/read-dsh-session.mjs "<scratch home>/sessions/<slug>/<id>/session.v3.jsonl.zstd"
```

Two things the evaluation learned the hard way, so you do not: the `write` tool refuses to overwrite a
file the session has not `read` (clean the scratch repository between runs), and a checkout owned by
the Administrators group fails git's ownership check under dsh's write-restricted Windows sandbox
(create the scratch repository from a non-elevated shell, or set its owner to the user).
