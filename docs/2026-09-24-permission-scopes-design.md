# Permission scopes — what an agent Daoris starts may do (PERM1)

> Written 2026-09-24 at the owner's direction, answering whether Daoris may set a safety guard (HELP3)
> and pre-approve its own connector's tools (INT3b) when it starts a session: *"so we should be able to
> do just like how claude code scopes configured by rules in daoris (which daoris can also use llm to
> update those too or modified by user)"*. The decision is **D72**. Phase 1 is built; phase 2 (an agent
> updating the rules) is designed here and filed as **PERM2**.

## 1. The model it mirrors

Claude Code decides what a session may do from **permission rules** in **scopes**. A rule is the
tool's own name, optionally with a specifier: `Bash(npm run test:*)`, `Edit(/src/**)`,
`WebFetch(domain:example.com)`, `mcp__<server>__<tool>`. Each sits in one of three lists: `allow` runs
without asking, `ask` asks, and `deny` refuses. Rules come from several scopes: enterprise-managed,
the command line (`--settings`), the repository's local and shared project settings, and the user's
own. The scopes **merge** rather than override: every scope's rules apply, and when two disagree
`deny` beats `ask`, which beats `allow`. A tool no rule covers falls to the session's mode
(`acceptEdits` approves edits inside the working directories and asks for the rest).

Two facts measured on this machine shape what Daoris can add:

- **An ask is a refusal for anything Daoris starts.** Over the protocol door every permission request
  is refused by construction (D52), and a pipe-door session (`-p`) has nobody to ask. So a tool an
  agent needs has to be **allowed before the session starts**, or it is refused.
- **A repository's own allow-list is ignored until the person trusts the folder**, on both doors
  (`docs/2026-09-24-deploy1-acp-trust-evidence.md`). An unadopted repository has no allow-list at all
  (INT3b). The repository's scope therefore cannot carry what Daoris itself needs.

## 2. Daoris's scopes

Daoris holds rules in **three scopes of its own**, in one file under the home:
`<home>/permissions.json`. It is never in a repository (D32).

| Scope | Reaches | Set with |
|---|---|---|
| **machine** | every session this machine's driver starts: quest sessions, intakes and conversations | `daoris agent rules allow <rule>` |
| **workspace** `<circle>` | sessions for repositories in that circle, and that circle's intake | `… --workspace <circle>` |
| **repository** `<name>` | sessions in that repository | `… --repository <name>` |

Above those sit **Daoris's defaults**: rules Daoris ships, each with an id and the reason it exists.
The person can switch a default off for the machine by id, and nothing else can remove it.

**Precedence is Claude Code's, not a second system.** Daoris does not rank its scopes against each
other or against the harness's. It unions defaults, machine, the session's workspace and the session's
repository, and hands the result to the harness as one more scope, the command-line tier. The harness
then merges it with the person's own settings and the repository's, with `deny` beating `ask` beating
`allow`. Two consequences follow, and both are the point:

- **Daoris can add a refusal nobody else can lift.** A default `deny` survives a repository's `allow`.
- **Daoris cannot lift a refusal the repository or the person made.** A repository's `deny` still
  wins over anything Daoris allows. A repository keeps its own say over itself.

A file that cannot be read is reported and treated as empty: wiring never stops a spawn (D21). An
unknown key written by a newer build is kept on every write.

## 3. The defaults

| Id | List | Rules | Why |
|---|---|---|---|
| `connector` | allow | the Daoris connector's own tools: `registry`, `knowledge_search`, `knowledge_get`, `knowledge_repositories`, `knowledge_convergence`, `quest_list`, `quest_respond`, `quest_publish` (each as `mcp__daoris-knowledge__<tool>`) | A driven session must be able to take and close its own quest and publish the requests it finds (INT3b). `knowledge_refresh` is not in it: rebuilding the index is the machine's job, not a session's. |
| `no-push` | deny | `Bash(git push)`, `Bash(git push:*)` | A push leaves the machine, and it stays the person's (D37). Held as a structural refusal, never as a script's exit code (HELP3's probe 4: PowerShell 5.1 collapses a native exit). |

**What a rule cannot say, and who says it instead.** HELP3 also asked for *refuse a write outside
the session's tree*. A rule cannot express "outside": there is no negation, and `deny` beats `allow`,
so no pair of rules carves a tree out of everything else. That boundary is the **harness's own**:
Claude Code's working directory is the session's tree (D51), an edit outside it asks, and an ask is a
refusal here (§1). What that leaves open is a shell command that writes elsewhere, and only if a
repository or the person allowed that command broadly. A structural hook that checks the path would
close it. It is filed as **PERM3** rather than guessed at, because a hook is a program Daoris would
ship and run inside every session.

## 4. How the rules reach a session

At each spawn the driver composes the union for that session's machine, workspace and repository, and
writes it as a settings file under the home: `<home>/spawn/<session>.settings.json`, holding
`{"permissions": {"allow": […], "deny": […], "ask": […]}}`. It sits beside the session's server file
(`SpawnServers`) and goes when the session does.

- **Pipe door** (`claude-code`): `--settings <file>`, the flag Claude Code documents as *"load
  additional settings from"* (`claude --help`, 2.1.280, HELP3's evidence). A conversation gets it too.
- **Protocol door** (`claude-code-acp`): `session/new` carries
  `_meta: {claudeCode: {options: {settings: "<file>"}}}`. Read from the adapter's own source at
  0.79.0, `dist/acp-agent.js`. Its `_meta.claudeCode.options` is spread into the Agent SDK's options
  (l. 5934–5945, 6043–6045). A `settings` string is read as a file resolved against the session's
  `cwd` (l. 6003–6010), and an absolute path resolves to itself. It is the SDK's programmatic tier, the
  same tier `--settings` fills on the command line. The adapter also forwards `allowedTools` and
  `disallowedTools` (l. 6087), which were not chosen: one file on both doors is one thing to check.
- **Every other harness is handed nothing.** Codex and dsh have permission models of their own
  (Codex's approval and sandbox policy, dsh's `DSH_PERMISSION_MODE`), and a Claude Code rule means
  nothing to either. Translating one into the other is a claim about somebody else's program, made
  when a person asks for it and measured when it is made (the TOOL5 bar). Until then a rule is
  **Claude Code's only**, and the surfaces say so.

**Proven keylessly**: the composed file, the flag on the pipe door's arguments, and the `_meta` on
the protocol door's `session/new`. **Unproven until a real session runs**: that the harness honours
a rule from this tier, and above all that it honours one **in a folder nobody trusted**. The first
is Claude Code's documented behaviour. The second is exactly what DEPLOY1's measurement found false
for the repository's own tier, and nobody has measured the command-line tier. `tools/acp-trust-probe.mjs`
gains the room that measures it. If this tier is honoured untrusted, the connector default answers
INT3b without anyone granting trust. If it is not, trust stays DEPLOY1(b)'s to grant, and PERM1
still carries the guard.

## 5. Two doors to edit them (D50)

- **Terminal**: `daoris agent rules` lists the defaults (on or off) and each scope's rules.
  `daoris agent rules allow|ask|deny <rule> [--workspace <circle> | --repository <name>]` adds a rule,
  `remove <rule>` takes it out of every list in that scope, and `default <id> on|off` switches a
  default. A rule that is not a tool name with an optional parenthesised specifier is refused, naming
  the shape.
- **Screen**: Settings → *What agents may do*, desktop only (the file is machine-local, like every row
  under *This machine*). It shows the defaults with a switch each, the rules by scope with a remove
  each, and an *add a rule* form one press away.

Both doors write the same file, and a hand edit keeps working because **the file is the contract**:
the CLI's `permissions.ts` and the driver's `PermissionRules.cs` read it by the same rules, with a
test on each side and a test that holds the two defaults tables together.

## 6. An agent updating the rules — phase 2, PERM2

The owner's words include *"which daoris can also use llm to update those"*. An "LLM" here is a
**session**: the harness carries the model and Daoris calls none (D24). The design:

- **A connector tool**, `permission_propose {list, rule, scope, why}`. A session that was refused
  something, or that finds a rule too wide, proposes the change in its own words. It is recorded by the
  service beside the session, like an ask.
- **Narrowing applies at once.** A new `deny` or `ask`, or an `allow` removed, is written at the next
  tick. It can only make agents do less, and the record says which session made it.
- **Widening waits for the person.** A new `allow`, a `deny` or `ask` removed, or a default switched
  off is shown on the screen as a proposal with the session's reason, and applies on the person's yes,
  from either door. This is the line D37 and D52 draw: a better approval surface must not widen
  autonomy, and what an agent may do is exactly that boundary. Letting agents widen their own
  permissions would make the rules decoration.
- **Every change is recorded with who made it**: the person, or the session by id.

**Question for the owner, not decided against their words:** may an agent's widening ever apply
without the person, for example within one repository's scope, or for a tool that only reads? The
default above says no.

## 7. What was not chosen

- **Writing into the repository's `.claude/settings.json`.** It is the repository's file (D32). It
  is ignored until trusted (§1). And a region in a file whose owner also edits it is D59's problem
  with no comment markers to hold one.
- **The credential profile's own `settings.json`** (HELP3's second proposal). It reaches every session
  under that profile, but a profile is an account, not a scope a person thinks in. It would also make
  a rule's reach depend on which account a session happened to run as.
- **`allowedTools`/`disallowedTools` on the protocol door.** They exist, but they would be a second
  shape for the same rules on one door only.
- **Daoris ranking its own scopes** (a repository `allow` overriding a machine `deny`). That is a
  second precedence system beside the harness's, and it would disagree with it exactly when it
  mattered.
