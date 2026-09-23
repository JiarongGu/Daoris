# Agents, their accounts and their wiring — the owner's asks (2026-09-23)

> *"so a few thing about the account or agents, so 1 machin claude-code, codex, managed claude-code
> (which claude does support for download standalone exe) not sure for codex, and I have no idea what
> harness is (so its for deepseek?) and we might also support for api driven too and also we should
> have proper wiring logic (maybe as a workflow setup? and more visually configuable?) and also whats
> the mean "ai" setup for daoris itself (so some task we can use ai to support)"* — the owner, after
> UX1 landed.

This note answers what can be answered from the code and the vendors' own documents, turns the rest
into backlog items (AGT1–AGT6 in `TASKS.md`), and names the decisions that are the owner's. It is a
direction, not a design: each item that needs one gets it before it is built.

## 1. What the words mean today

- **A harness is the agent program a session runs** — Claude Code (Anthropic), Codex (OpenAI), dsh
  (DeepSeek's agent CLI, `@deepseek-ai/dsh`, adopted as a protocol configuration by D53). The model
  belongs to the harness; Daoris starts it, hands it the work and pipes its text (D24). The word is
  the code's, and a person never needed it. Since AGT1 a person reads *agent*: `daoris agent`, the
  refusals, and each tool's card naming its product and maker. `driver.json`'s `adapter` and the
  door ids (`claude-code-acp`) stay, because a file and a terminal type them.
- **A door is how Daoris holds a session**: *direct* (a pipe to the tool's own CLI) or *protocol*
  (the Agent Client Protocol, D53). One tool can have both, which is why Settings groups doors under
  the tool.
- **An account is a directory the tool signs into** (D66 §3): made by signing in, listed by who
  signed in, removed with its sign-in. The tool's own home is the account a machine has before any.
- **Where the binary comes from** is the third axis (D57): the machine's `PATH`, or a version Daoris
  installed and pinned. So "machine claude-code" and "managed claude-code" are one tool, one set of
  accounts, and two sources — not two agents.

## 2. What was checked (2026-09-23)

From the vendors' own documents, not from memory:

- **Claude Code ships a standalone binary, installable at an exact version.** The native installer
  takes a version (`install.ps1 2.1.89`); each release publishes a `manifest.json` of per-platform
  SHA-256 checksums under `downloads.claude.ai/claude-code-releases/<version>/`, signed from 2.1.89
  on. **The npm package installs that same native binary** through a per-platform optional
  dependency, so TOOL2's `npm install --prefix` pin already runs the native build. Fetching from the
  release bucket instead would drop npm and verify against the signed manifest.
  ([setup](https://code.claude.com/docs/en/setup))
- 🔴 **Native and npm installs update themselves in the background** unless `DISABLE_AUTOUPDATER=1`
  is set (or `DISABLE_UPDATES=1`, which blocks every path). **Measured the same day (AGT2a):** a
  pinned 2.1.270 reported its own auto-updates enabled and called itself npm-global; every pinned
  spawn now runs with `DISABLE_UPDATES=1` (FIX-LOG).
- **Codex ships standalone too**: npm, Homebrew, per-platform archives on its GitHub releases (the
  README names macOS and Linux ones), and a PowerShell installer for Windows. Not yet checked: a
  Windows archive on the release page, and whether a version can be chosen.
  ([openai/codex](https://github.com/openai/codex))
- **Daoris's own AI today is one feature.** The service's semantic search and convergence use an
  embedding endpoint (`DAORIS_EMBED_MODEL`, `DAORIS_EMBED_URL`, Ollama by default); unset, it is
  lexical only, and the status bar says *lexical only*. Intake answers from declarations with no
  model (INT4a), and INT4b makes it a **session** on a harness rather than a model call. Every
  model-backed feature works with none and names the tier that answered (`model-decoupling`).

**Checked again for AGT2b (2026-09-24)**: `docs/2026-09-24-agt2b-channel-evidence.md` records each
channel's layout, how the signature works, what the official installers verify, and what is not
confirmed.

## 3. The asks, as items

- **AGT1 — one word a person reads: *agent*** (done). The CLI verb, its help and the refusals say
  *agent*; each toolchain declares its product and maker, and the card and `agent list` show them
  (dsh reads as DeepSeek's). `daoris harness` answers with where it went. `harness` and `adapter`
  stay the code's words.
- **AGT2 — a managed install from the vendor's own channel.** AGT2a measured whether a pinned spawn
  updates itself, and it could, so the tool's own switch is now set. AGT2b fetches Claude Code from
  its release bucket at the pinned version, checked against the signed manifest, and Codex from its
  releases once the Windows archive and version choice are confirmed. npm stays for what ships
  only there. **Built 2026-09-24**: `docs/2026-09-22-toolchain-design.md` §3a.
- **AGT3 — an account that is an API key.** Each tool has its own way: Claude Code reads
  `ANTHROPIC_API_KEY` or a cloud provider's credentials; Codex signs in with one read from stdin
  (`codex login --with-api-key`, [auth](https://developers.openai.com/codex/auth)) and stores it in
  its own home; dsh names a provider route in its profile. **Decided (D67 §1): Daoris keeps the
  key**, machine-local, never over HTTP, printed back only as an audit prefix, and handed to the
  agent at spawn through the tool's own variable.
- **AGT4 — an API-driven agent.** **Closed by D67 §2**: an agent keeps its own loop and reaches
  Daoris over MCP, so Daoris runs no model loop. An API-driven agent is an existing one on an
  API-key account (AGT3), or one a plugin declares.
- **AGT5 — the wiring.** **Became the MAP arc (D67 §3)**: the *workflow* (MAP1: how Daoris's loop
  chains an ask through the intake, quests, sessions and `then`, each step's agent, account and
  version), the *workspace topology* (MAP2: which repositories there are and how they are wired),
  and *a repository's code* on the same map (MAP3). Today's four files — `driver.json`,
  `harnesses.json`, `plugins/`, `remotes.json` — are what MAP1 reads.
- **AGT6 — Daoris's own AI, on the Settings page.** The jobs Daoris may use a model for, each with
  the tier that answers now and how to change it: semantic search and convergence (the embedding
  endpoint, today environment-only), and intake (which agent and account runs it, once INT4b lands).
  Candidates to add later, each specified without naming a model: a quest drafted from an ask, a
  session's summary for the record, a second look at a diff before *accept*.

## 4. Order

AGT1 and AGT2a landed first. Then AGT3 (the key, now decided), AGT2b, and the MAP designs; AGT6
meets MAP1, because intake is a job on the workflow. INT4b is not displaced: it is the first job
MAP1 will draw.
