# Evidence: which instructions file each agent reads (AGENTSMD1)

Taken 2026-11-22/23, before any change to `sync`, on this machine's installed builds. The earlier note
(`archive/2026-10-01-entry-point-evidence.md`) read each harness's shipped code; this one runs each agent, in a
folder holding only the file named, and asks what its session was given. D117 §2 wrote `CLAUDE.md` holding
`@AGENTS.md` because Claude Code then read only `CLAUDE.md`; Claude Code 2.1.277 added `AGENTS.md` where a
project has no `CLAUDE.md`, and the cells below say whether that holds in the builds in use.

## The probe

A fixture folder is made outside any folder holding an instruction file (an instruction file above the folder is
loaded too). Its brief is two lines, `# Probe brief` and `PROBE-LINE: the agents-file token is AGT-4471-ONLY.`
(the token differs per file). Four fixtures: **only `AGENTS.md`**; **`AGENTS.md` and a `CLAUDE.md` holding
`@AGENTS.md`** (what `sync` writes today); **only `CLAUDE.md`** (its own token); **a root `AGENTS.md` and a room
`room/AGENTS.md`**, with no `CLAUDE.md` at all.

- **A real model** (Claude Code): `claude -p "Without using any tool: quote verbatim the line beginning PROBE-LINE
  from the project instructions you were given at the start of this session, or answer exactly NONE if there is
  none." --tools "" --no-session-persistence`, run in the fixture. With every tool off, a quoted line was handed to
  the model by the harness. The ACP door ran the same prompt through `tools/dsh-probes/acp-client.mjs` against the
  adapter in Daoris's toolchain cache. The model that answered (native): `claude-opus-5-5`.
- **A scripted echo** (codex, dsh): they need a model route, and a route to a person's account is theirs. Each ran
  against `tools/dsh-probes/echo-llm.mjs`, a local provider that answers each request by quoting the `PROBE-LINE`
  lines found in the request, or `NONE`, and logs the request field that held them. No model ran; the answer is
  read off what the harness sent, which is what the question asks. Codex ran with an empty `CODEX_HOME` and
  `-c model_providers.echo={…wire_api="responses"…}`, dsh with a scratch `DSH_HOME` whose `settings.yaml` declares
  the route (the setup `tools/dsh-probes/README.md` gives).

## Results

| Agent, build | only `AGENTS.md` | `AGENTS.md` + pointer | only `CLAUDE.md` | nested `room/AGENTS.md`, no `CLAUDE.md` |
|---|---|---|---|---|
| Claude Code native **2.1.329** | quoted `AGT-4471-ONLY` | quoted `AGT-4472-POINTER` | quoted `CLD-4473-ONLY` | root and room lines both quoted; the room's arrived when the Read tool read `room/x.txt` |
| Claude Code over ACP, adapter **0.106.0**, CLI **2.1.322** | quoted `AGT-4471-ONLY` | not run | not run | not run |
| Claude Code over ACP, adapter **0.84.0**, CLI **2.1.284** | quoted `AGT-4471-ONLY` | not run | not run | not run |
| codex **0.167.2** (echo) | quoted, in `input[2]` | quoted `AGT-4472-POINTER` | `NONE` | not run |
| codex **0.160.0** (echo) | quoted | not run | not run | not run |
| dsh **0.2.1-rc.2** (echo) | quoted, in `messages[2]` | quoted `AGT-4472-POINTER` | quoted `CLD-4473-ONLY` | not run |

Every agent in use quotes the line from `AGENTS.md` with no `CLAUDE.md` present. Claude Code's nested case
also works with none. Two cells say more than the proof asks:

- **codex reads no `CLAUDE.md`** (`NONE` in the third column), so the pointer was never for it. dsh reads both
  names, so with the pointer present it is handed `AGENTS.md` and a file holding the text `@AGENTS.md`, which it
  does not follow.
- **A `CLAUDE.md` of its own text wins over `AGENTS.md` in Claude Code.** A fifth fixture, `AGENTS.md` (token
  `AGT-4471-ONLY`) beside a `CLAUDE.md` of its own text (`CLD-4473-ONLY`), on native 2.1.329, quoted only the
  `CLAUDE.md` line. A repository whose `CLAUDE.md` holds anything but the import therefore gives Claude Code no
  brief unless that file imports it.

## What was not measured

- **Claude Code before 2.1.277** does not read `AGENTS.md` (the changelog the binary embeds, the earlier note
  §1); no such build is on this machine, so this is the maker's documentation, not a probe. It is the condition
  the retired pointer would have named: a person on a build older than 2.1.277 gets no brief from a repository
  without a `CLAUDE.md`. Every build in use on this machine and in Daoris's toolchain cache is newer (2.1.284 the
  oldest run here).
- **`codex-acp` (2.1.1, 2.6.0) and `dsh --profile acp`** run codex and dsh's own instruction loaders; each was
  not run separately.
- **The maker-side flag** `tengu_agents_md_mod` (the earlier note §1) is account-side: this ran on one account.
  Where it is off, the first column would read `NONE`; a probe is how an account shows it.

## Reproduce

`node tools/dsh-probes/echo-llm.mjs --port 8766 --log <log>`; codex: `CODEX_HOME=<empty> codex exec
--skip-git-repo-check -c 'model_providers.echo={name="echo",base_url="http://127.0.0.1:8766/v1",wire_api="responses",
requires_openai_auth=false}' -c model_provider=echo -c model=echo-model "<prompt>"`; dsh: the README's scratch home
with its `baseURL` at `http://127.0.0.1:8766/v1`, then `dsh headless --json "<prompt>"`. dsh's first run in a
fresh `DSH_HOME` imports its settings and fails once on a missing key; the second answers.
