# What an agent is to Daoris today: every place it tells one agent or door from another — a survey

> AGENTPROVIDER1, phase 1 of a foundational co-design under D191 (2026-10-26). The factual ground the sealed proposals stand on; the
> direction, the constraints and the questions are `docs/2026-10-26-agent-provider-brief.md`. Read from the code at `f58f1be61`, every
> line number that commit's; nothing was run. Four readers each took one area (the protocol door and the streams; accounts and the
> toolchain; the loops and conversations that consume the adapters; the page, the CLI, the service and the tools), and the steward read
> the adapter seam itself and checked a sample of every reader's lines against the files. A fact inferred rather than seen says
> *unverified*. `Dnn` is `docs/decisions/Dnn.md`. **Paths:** a path with no prefix is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`;
> `Modules/` is `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Host/` is `src/Daoris.Desktop/Daoris.Desktop.Driver.Host/`, `Core/`, `Mcp/`
> and `Http/` are `src/Daoris.Service/Daoris.Service.{Core,Mcp,Http}/`, `Web/` is `src/Daoris.Web/src/`, `Cli/` is `src/Daoris.Cli/src/`.
> In the tables, **E** marks an essential difference (the agent or its door genuinely differs: its wire, its binary, its files, its
> vocabulary) and **A** an accident of where the code was written (a branch outside the agent, a copy, a hard-coded id, one agent's
> fact applied to all). It proposes nothing: §15 names the tangles, and the brief turns them into questions.

## 0. In one page

**What an agent is, in the code.** One interface, `ISessionAdapter` (`Agents/Adapters/Adapters.cs:1321-1513`), with twenty-one members
(its name, its door, three postures, its spawn for a session, a chat and a resume, whether it holds a conversation and resumes, how it
takes servers, rules and plugins, its `_meta`, its tools-up-front switch, its stream mapper, its message and interrupt frames, its
toolchain), and one record, `HarnessToolchain` (`Accounts/Harnesses/Harnesses.cs:176-265`), with thirty fields of facts about the
agent's program and accounts. `AdapterSet.Built` (`Adapters.cs:2548-2571`) holds six adapters and one agent with accounts and no
adapter; a plugin adds protocol-door harnesses (`Plugins/DeclaredAcpAdapter.cs:21-62`). Two doors: the **pipe** door
(`SessionWire.Pipe`, the harness's own command line and stdout) and the **protocol** door (`SessionWire.Acp`, the Agent Client Protocol,
`Agents/Acp/Acp.cs`'s `AcpSession`, 3,200 lines in one class).

| Id | Its accounts are | Door | Product · maker · mark | Binary | Home variable | Declared at |
|---|---|---|---|---|---|---|
| `claude-code` | its own | pipe | Claude Code · Anthropic · CC | `claude` | `CLAUDE_CONFIG_DIR` | `Adapters.cs:2176-2447` |
| `claude-code-acp` | `claude-code`'s | protocol | the same, declared again | `claude-agent-acp` | `CLAUDE_CONFIG_DIR` | `Adapters.cs:1831-1949` |
| `codex-acp` | `codex`'s | protocol | Codex · OpenAI · Cx | `codex-acp` | `CODEX_HOME` | `Adapters.cs:2063-2129` |
| `codex` | its own; **no door** | none | Codex · OpenAI · Cx | `codex` | `CODEX_HOME` | `Accounts/SignIns/CodexAccounts.cs:30-53` |
| `dsh` | its own (no sign-in) | protocol | DeepSeek Harness · DeepSeek · DH | `dsh` | `DSH_HOME` | `Adapters.cs:1970-2037` |
| `stub` | its own | pipe | — | the configured command | `DAORIS_STUB_CONFIG_DIR` | `Adapters.cs:1683-1749` |
| `acp-stub` | `stub`'s | protocol | — | the configured command | the stub's | `Adapters.cs:1763-1806` |
| a plugin's harness | its `accountOf`, or its own | protocol | its manifest's | its manifest's | its manifest's | `Plugins/DeclaredAcpAdapter.cs:21-62` |

**The sites, by what they are about** (§15 counts them):

| § | Group | Sites | E | A | Its sharpest tangle |
|---|---|---|---|---|---|
| 1 | Identity and naming | 37 | 8 | 29 | an agent is named by its door's id everywhere a record, a page or a choice names it, and three spellings of *door* |
| 2 | Toolchain, install and version | 38 | 26 | 12 | Codex's sign-in, limits and usage declared on a door of it or on a holder that is no adapter |
| 3 | Accounts, sign-in, usage and limit readings | 60 | 28 | 32 | four copies of *own, else owner*, each with its own rule; a Claude/Codex switch in the usage layer |
| 4 | Models, efforts and roles | 22 | 13 | 9 | Claude Code's settings schema and its effort words applied to every agent |
| 5 | A session's start, resume, going on and hand-over | 29 | 21 | 8 | resume and go-on written twice per door, once driven and once in a chat |
| 6 | The doors and what each supports | 28 | 17 | 11 | the machine's door decided in five places by four rules and threaded through the planner |
| 7 | The turn and its prompt | 14 | 12 | 2 | the composed instruction rides every spawn's environment, inside the adapter |
| 8 | The event stream | 35 | 22 | 13 | one agent's `_meta` read inline in the shared protocol door; Claude Code's stream read by four readers |
| 9 | Connectors | 16 | 6 | 10 | *the pipe door cannot drive an unadopted repository* decided in five places |
| 10 | Permissions and skills | 35 | 19 | 16 | Claude Code's rule grammar is the product's only rule language |
| 11 | Process, containment and the environment | 11 | 6 | 5 | a Claude seam set on every protocol door's spawn |
| 12 | Errors and their classes | 18 | 10 | 8 | a door's failure carried in two channels, so the pipe door's limit cools nothing |
| 13 | Anything else | 7 | 2 | 5 | twin comments naming a CLI table deleted four days ago |

**Where the code is already one place**, which a contract can stand on: the spawn's shell every adapter goes through
(`Spawning.Shell`, `Adapters.cs:1606-1653`: no window, UTF-8, the tools, the git wiring); the launcher every start goes through
(`Platform/AgentStarts.cs`, agent-neutral, D201); the one error reader (`AgentErrors.Read`, `Agents/Errors/AgentErrors.cs:133-205`,
own table then owner's); the one protocol session for every protocol agent (`AcpSession`), whose resume method is read from the wire's
capabilities (`Acp.cs:3457-3479`) and whose model carrier finds options by category with no agent branch (`Acp.cs:667-672`, `:728-761`);
the pipe door's per-agent reader seam (`IStreamMapper`, `Agents/Streams/StructuredOutput.cs:36-78`); one shape of a session's events
both doors write (`SessionEventKind`, `Sessions/Domain/SessionEvents.cs:13-64`); a running session's word capabilities attached at run
time rather than by the agent's name (`Conversations/DrivenInbox.cs:142-195`, `TurnDoor`, `Conversations/Domain/ChatQueue.cs:157`);
and the toolchain record itself, a declaration rather than code. Each is read beside a branch that does part of its job another way
(the rows below).

The prior maps this extends rather than repeats: COAGENT1's provider sheet (`docs/2026-10-22-coagent-design.md` §3.2, the capabilities
each door is known to have), COWORKVIEW1's survey of native children (`docs/2026-10-24-cowork-view-design.md` §1.1, §3.6), the model and
effort doors' measurement (`docs/2026-10-23-model-effort-doors-evidence.md`), AGENTERRORS1's survey of what each door hands on a
failure (`docs/2026-10-24-agent-errors-design.md` §1), and SWITCHCTX1's table of where each agent keeps its conversation
(`docs/2026-10-24-switch-context-design.md` §1.5). FOUNDATION1's map places all of this in one subdomain, Agents (D212 §14.2 row 9).

## 1. Identity and naming

**Today.** An agent is known by its adapter's id, which is a door's id: `claude-code` is Claude Code's pipe door and its accounts'
owner, `claude-code-acp` its protocol door, and Codex has a door (`codex-acp`) and an account holder (`codex`) that is no adapter.
Every record, the page's choices and the hand-over's roster name the door; the agent (the *tool* on the page) is rebuilt from
`AccountOf` wherever it is needed. *Door* means three things in the code: the wire (`SessionWire`), an adapter as a way onto an agent's
accounts (`HandOverDoor`, `TurnBinding.Door`), and a UI or say door; and *pipe* names both the native agent door and the terminal's named
pipe. Several places name an agent by a literal id or a type test.

| Site | What differs | Kind |
|---|---|---|
| `Adapters.cs:1685`, `:1765`, `:1833`, `:1972`, `:2065`, `:2178` | the six ids, each its adapter's `Name` | E |
| `Adapters.cs:2548-2571` | the built set: six adapters and the `codex` holder, a toolchain with no door (CODEXACCT1) | A — a holder exists because no `codex` door is built |
| `Adapters.cs:2475-2508` | `Agents` (owners and plugins' names), `Reserved`, `Holder` | E |
| `Adapters.cs:2511-2514` | a plugin's provenance read by a type test (`is DeclaredAcpAdapter`) | A — a type test, not a declared field |
| `Adapters.cs:2539-2546` | an unknown id is an error naming what exists (D23) | E |
| `Adapters.cs:1920-1922`, `:2105-2107`, `:2363-2365`, `:2024-2026` | product, mark and maker per toolchain; `claude-code-acp` declares `claude-code`'s again | A — a door repeats its owner's identity |
| `Accounts/Harnesses/Harnesses.cs:268-281` | `Owner` = `AccountOf` or the name; `StartsInOwnHome` | E |
| `Accounts/Harnesses/HarnessSettings.cs:346-350` | the agents known: the built set's and the plugins' | E |
| `Agents/AgentsCommand.cs:653-658`; `Platform/DriverChoicesCommand.cs:2195-2197` | a stand-in is told apart by an empty binary in one, by the names `stub` and `acp-stub` in the other | A — two rules for one fact |
| `Brainstorms/Domain/BrainstormSynthesis.cs:9-13` | `Stub => Adapter is "stub" or "acp-stub"` (read at `Brainstorms/Domain/BrainstormDocument.cs:64`, `:236-238`) | A — a hard-coded id where *runs no model* is meant |
| `Agents/Adapters/DshProfile.cs:123-142`; `Loop/Driver.cs:3012-3027` | dsh known by the literal `"dsh"` and by `adapter is DshAdapter` | A — an id and a type outside the adapter |
| `Setup/SetupCommand.cs:116-118` | `adapter.StartsWith("codex")` adds *sandbox not measured* | A — a prefix of an id |
| `Sessions/Domain/ChildBindings.cs:8-16`, `:81-82` | a child's harness is the constant `"claude-code"` or `"codex"` | A — ids in domain code |
| `Platform/DriverConfig.cs:70-75` | the empty configuration's adapter is `"claude-code"` | A — a default id outside the set |
| `Http/Program.cs:1291-1293`, `:1322-1324`, `:1966-1967` | opening an intake, Ask Daoris or a chat with no adapter takes `"claude-code"`, *the supported one* | A — the service names an agent |
| `Mcp/HelpAgentTool.cs:32` | the tool's description lists four ids | A |
| `Agents/AgentsCommand.cs:632`, `:683`; `Agents/AgentAccountsCommand.cs:141`; `Agents/AgentPinCommand.cs:41`; `Platform/DriverChoicesCommand.cs:553`, `:569`, `:1680`, `:1776`, `:2018`, `:2073`; `Brainstorms/Domain/BrainstormRules.cs:88`; `Web/locales/en/settings.helpers.json:25`, `Web/locales/en/settings.opinion.json:32` | ids as examples in refusals, help and placeholders | A — cosmetic |
| `Agents/AgentListCommand.cs:34-78`, `:82-101` | the built agents and the plugins' harnesses listed by two loops, a plugin's by its own name, its `AccountOf` ignored (`:99`) | A — two paths for one contract |
| `Agents/AgentsCommand.cs:624-642`; `Agents/AgentAccountsCommand.cs:668-674`, `:700-704`; `Agents/AgentListCommand.cs:110-111`; `Accounts/Harnesses/HarnessRoster.cs:1694-1715` | *a door, else a holder* resolved to its toolchain, four copies | A |
| `Conversations/Domain/HandOverAsks.cs:207-260`; `Loop/DriverHandOvers.cs:331-347` | `HandOverDoor(Harness, Present, AccountOf)`: *door* meaning an adapter | A — a second meaning of *door* |
| `Accounts/Usage/UsageTurns.cs:19-29` | `TurnBinding.Door` holds the adapter's name | A — a third |
| `Agents/Errors/AgentErrors.cs:41-52`; `Help/HelpWired.cs:178-182`; `Web/locales/en/harness.json:25-26` | the wire spelled `protocol`/`pipe` on a failure, `acp`/`pipe` on Ask Daoris's receipt, *protocol*/*direct* (协议/直连) on the page | A — three spellings of one fact |
| `Platform/DaorisHome.cs:432`; `Modules/Platform/InstancePipe.cs:108`; `Web/person/Confirmations.tsx:25` | *pipe* is also the terminal's named pipe (and *protocol* that pipe's version refusal) and a confirmation's source | A — one word, two things |
| `Loop/Driver.cs:2475-2476`; `Conversations/Chat/ChatRunner.cs:646-647`, `:882`; `Loop/DriverIntakes.cs:276`; `Conversations/Helpers/HelperDoors.cs:38`; `Conversations/Partners/PartnerDoors.cs:36` | every record is opened with its adapter's id (§14) | E |
| `Adapters.cs:383-431`; `Loop/Driver.cs:4578-4585` | the commit trailer: a switch on the maker (Anthropic, OpenAI, DeepSeek) choosing each maker's attribution identity, inside the prompt composer | A — a per-maker table outside the provider |
| `Review/Domain/ReviewerChoice.cs:24-33`; `Loop/DriverBrainstorms.cs:500-502`, `:540-541`; `Platform/DriverChoicesCommand.cs:1752-1756` | an agent's family (owner, product, maker) for D155's independent reader and a brainstorm's voices | E |
| `Web/data/api.ts:444-448` | a session record carries `adapter` (a door) and `harnessVersion`, never the agent | A — every reader rebuilds the agent |
| `Web/agents/tools.ts:236-243` | the door's label built in the page; any wire not `acp` worded *direct* | A |
| `Web/agents/agents.ts:499-536` | the declared mark, else initials the page makes, with collision rules | A |
| `Web/work/head/headFacts.ts:75-86` | the leading agent counted by door ids, so a move between two doors of one agent reads as two agents | A |
| `Web/locales/format.ts:222-241` | a session's tool printed as its raw adapter id | A |
| `Web/work/gates/opinion.ts:192-196`; `Review/Domain/OpinionView.cs:86` | a reviewer's name built twice, page and driver | A — a twin |
| `Web/work/gates/OpinionGate.tsx:242` | the page's default reviewer label `another-maker` | A |
| `Web/settings/AiDomain.tsx:82-86` | the agents an actor may name are doors, so an actor's choice holds a door's id | A |
| `Web/menus/commands.ts:40` | the palette's keywords name two products | A |
| `Web/locales/glossary.json:16` | product names kept untranslated | E |
| `Cli/cli/agent.ts:10`, `:68`, `:98-99` | the CLI's help lists the agents by name | A — the verbs run in the driver (`:104`) |

## 2. Toolchain, install and version

**Today.** `HarnessToolchain` is the de facto declaration of an agent's program: its binary and version question, its install (an
npm command), its update, its pin's source (a maker's signed channel for `claude` and `codex`, an npm package for the protocol adapters
and dsh), what a pinned copy runs with, its product, maker and mark. A protocol door is a separate package with its own pin from the
agent it drives (ACP2), so versions and commands are keyed by the door while accounts are keyed by the owner. Codex's own binary is
declared on the holder, which no door is; the stand-ins declare no binary and run whatever `driver.json`'s `commands` names.

| Site | What differs | Kind |
|---|---|---|
| `Accounts/Harnesses/Harnesses.cs:176-265` | the record: thirty fields, each a claim about one agent's program | E — today's contract |
| `Adapters.cs:2362-2423` | `claude-code`: `claude`, npm install, `update`, `auth login`, its JSON login question, its signed channel, trust file, settings file, pinned environment, key variable, refused words, limits, weekly reset, windows, sign-in words, usage question, turn report, error words | E |
| `Adapters.cs:1919-1945` | `claude-code-acp`: its own binary and package, `AccountOf: claude-code`, the trust file declared again, its own turn report and error words | E; the trust file's repeat A |
| `Adapters.cs:2104-2125` | `codex-acp`: its binary and package, `AccountOf: codex`, `ProfileMustExist`, and Codex's limit words and usage question *since no `codex` adapter exists* | A — the agent's facts on its door |
| `Accounts/SignIns/CodexAccounts.cs:30-53` | the `codex` holder: binary, `CODEX_HOME`, install, update, channel, login, status question, device code, ports; no key | A — declared outside any adapter |
| `Adapters.cs:2021-2033` | dsh: binary, `DSH_HOME`, npm package; no login | E |
| `Adapters.cs:1709-1741` | the stub's toolchain mirrors Claude Code's refused words, limits, windows, sign-in and error codes | A — the stand-in borrows one agent's vocabulary |
| `Adapters.cs:1792-1798` | `acp-stub`: `AccountOf: stub`, probed by presence | E |
| `Adapters.cs:2432-2433`; `Accounts/Harnesses/HarnessSelection.cs:535-551` | `DISABLE_UPDATES=1` on a pinned `claude` only | E |
| `Accounts/Harnesses/HarnessRoster.cs:863-867` | an adapter with no binary and no configured command is hidden (the stand-ins) | E |
| `Accounts/Harnesses/HarnessRoster.cs:1249-1268` | a pin resolved by the door's name | E |
| `Accounts/Harnesses/HarnessRoster.cs:1279-1301` | an absent binary's refusal names `daoris agent install` or *its maker's tooling* by `Install` | E |
| `Accounts/Harnesses/HarnessRoster.cs:2012-2041` | the wiring view: the account from the owner, the version and command from the door | A — two keys for one start |
| `Accounts/Harnesses/HarnessRoster.cs:2366-2374` | the usage read runs the owner's binary (`claude`, `codex`), not the door's | E |
| `Accounts/Harnesses/HarnessSelection.cs:273-276` | probed by presence (the protocol stub, a plugin's) rather than by its version | E |
| `Accounts/Harnesses/HarnessSelection.cs:403-416` | the command: configured, else the pin by door or holder, else the binary on the path | E |
| `Accounts/Harnesses/HarnessSelection.cs:553-560`; `Accounts/Harnesses/HarnessActions.cs:571-605` | npm's `.cmd` shims resolved, and a shim refuses shell metacharacters on its arguments, where the pipe door carries its prompt | E |
| `Accounts/Harnesses/HarnessActions.cs:79-86`, `:95-138`, `:181-210`, `:217-316` | install by npm; a pin from a channel or a package; update by pin or by the tool's own `update`; the newest version from a channel's pointer or `npm view` | E |
| `Agents/AgentPinCommand.cs:32-56` | a pin needs a package or a channel | E |
| `Agents/AgentPinCommand.cs:69-121`, `:196-221`, `:238-255`, `:286-325` | the pin, update and newest-version logic written again beside `HarnessActions` | A — a copy |
| `Accounts/Harnesses/HarnessSettings.cs:685-718`, `:1181-1199` | versions keyed by door, account sets and rotation by owner | E |
| `Accounts/Harnesses/HarnessSettings.cs:836-837`, `:855-875` | a managed binary's two layouts: a channel's `bin/` and npm's `node_modules/.bin` | E |
| `Agents/AgentsCommand.cs:318-366`; `Agents/AgentListCommand.cs:41-61`; `Agents/AgentLongCommand.cs:37-56` | `which`, the list's header, `install`, each from the toolchain | E |
| `Setup/ClaudeReleaseDownloads.cs:18-161`; `Setup/Domain/ReleaseChannel.cs:383-525` | Claude Code's signed release bucket and its key | E |
| `Setup/ClaudeReleaseDownloads.cs:130-132` | the verified binary's hash written by Claude's channel alone | A — a guarantee one channel gives |
| `Setup/ReleaseChannels.cs:25-53`, `:61-66`, `:78-80` | the channels a switch on their ids; any channel not Claude's staged as Codex's | A — a switch, not a channel object |
| `Platform/DriverConfig.cs:16-18`, `:33-35`, `:1170-1176`, `:1735-1758` | `commands` keyed by adapter name overrides the binary | E |
| `Plugins/DeclaredAcpAdapter.cs:46-59` | a plugin's toolchain from its manifest: no limits, sign-in, refused or error words | E |
| `Cli/platform/tarball.ts:1-3` | Codex's tar.gz unpacker, called only by tests since PUSH1f2c | A — leftover |
| `Web/agents/tools.ts:20-97` | the page's `ToolDoor` row per door: wire, owner, sign-in, device code, update, install, key, pin, structured, rules, settings, mark, product, maker | E — the shape a contract would own |
| `Web/agents/agents.ts:565`; `Web/agents/AgentPage.tsx:156-158`, `:340-356`; `Web/agents/AgentsView.tsx:170`, `:204-217` | the agent's first door (`doors[0]`) decides and carries every account act, install and key | A — the first door stands in for the agent |
| `Web/agents/AgentPage.tsx:669-752`, `:840-850`; `Web/agents/agents.ts:581-589` | the ways in, each door's version, install, update and pin | E |
| `Web/bridge/plugins.ts:30` | a plugin's harnesses are configurations of the protocol door | E |
| `Web/locales/en/projects.json:385`, `:393`, `:396`, `:416`; `Web/locales/en/settings.daoris.json:150`, `:174-175` | a role holder runs *Daoris's own pinned Claude Code* | A — D209's choice worded as one agent |
| `tools/family-rehearsal.mjs:757-782` | the stub answers `--version` and `--login-state` and reads its home variable | A — a twin of the stub's toolchain |
| `tools/proof-kit.mjs:53-55`, `:129-149` | the proof kit bound to `claude-code-acp` and its binary | A |
| `tools/live-logins.mjs:6-17` | tests may not sign in; the binaries read from the driver's toolchains | E |
| `tools/console2-probe.mjs:35`; `tools/console3-probe.mjs:2-6`, `:73` | dated probes of one adapter and of the pipe door's flags | E — measurement |

## 3. Accounts, sign-in, usage and limit readings

**Today.** Accounts are the owner's: `claude-code`'s for both Claude Code doors, `codex`'s for `codex-acp`, the stub's for both stand-ins;
a cool-off, a reading, a key and a sign-in are kept under the owner. Claude Code alone takes an API key (`ANTHROPIC_API_KEY`), Codex alone
signs in by a device code beside the browser and waits on local ports. Each agent words its limit in its own grammar, and the usage
layer asks Claude Code by a hidden stream-json conversation and Codex by its app server. A door reads a field it does not declare from
its owner, by four rules in four places.

| Site | What differs | Kind |
|---|---|---|
| `Accounts/Harnesses/HarnessRoster.cs:66-72` | a hold keyed by the door's owner, the mapping written inline | A |
| `Accounts/Harnesses/HarnessRoster.cs:114` | the cooling agent is the owner; an adapter with no toolchain has none | E |
| `Accounts/Harnesses/HarnessRoster.cs:120-144`, `:188-197` | limits, sign-in and windows read own, else an owner that is an adapter, never the `codex` holder | A — three copies, blind to the holder |
| `Accounts/Harnesses/HarnessRoster.cs:2082-2098` | the usage question: a fourth copy of the same fallback | A |
| `Accounts/Harnesses/HarnessRoster.cs:209-220` | a door's frame's windows kept only where the agent words windows (Claude Code's doors, the stand-ins) | E |
| `Accounts/Harnesses/HarnessRoster.cs:272-304` | a failure read for a limit with that agent's words, then by code | E |
| `Accounts/Harnesses/HarnessRoster.cs:427-462`, `:766-780` | a report and a login asked through the owner's or the holder's question, since doors declare none | A |
| `Accounts/Harnesses/HarnessRoster.cs:476-498` | pressing `codex-acp` probes its own binary, then the holder for its accounts | A — no `codex` door |
| `Accounts/Harnesses/HarnessRoster.cs:782-841` | toolchain, agent, sign-in command, reporting door: self, then owner adapter, then holder | A |
| `Accounts/Harnesses/HarnessRoster.cs:1987-1999` | readiness read from the owner's door where one is built | A |
| `Accounts/Harnesses/HarnessRoster.cs:519-561` | each agent's home variable reported | E |
| `Accounts/Harnesses/HarnessRoster.cs:720-741`, `:1317`, `:1393-1396`; `Accounts/Harnesses/HarnessSelection.cs:281-301` | a key account handed its key in the owner's key variable | E |
| `Agents/AgentLongCommand.cs:117-122` | the key variable spelled a third way | A |
| `Accounts/Harnesses/HarnessRoster.cs:1150-1170`, `:1204`, `:1658-1664`, `:1193` | a stand-in may run in its own home; every real agent walks Daoris's accounts; no toolchain, no walk | E |
| `Accounts/Harnesses/HarnessRoster.cs:1213-1216` | a start asks fresh windows only of an agent with a usage question | E |
| `Accounts/Harnesses/HarnessRoster.cs:1723-1755`, `:1962-1974` | set-up agents and a session's load counted under the owner | E |
| `Accounts/Harnesses/HarnessRoster.cs:2107-2134`, `:2263-2277` | a reading's floor: one minute for Claude Code, five for Codex | E |
| `Accounts/Harnesses/HarnessRoster.cs:2287-2360` | the usage read branches on `UsageKind`; Claude Code's read runs through Codex's helper; key accounts skipped | A — a closed switch in the roster |
| `Accounts/Usage/CodexUsage.cs:21-39`, `:295-357` | `UsageKind {Codex, Claude}`; both readers' process code in Codex's class | A |
| `Accounts/Usage/CodexUsage.cs:56-57`, `:91-285` | Codex's app server and its windows; a plan read only in Claude Code's word | E |
| `Accounts/Usage/ClaudeUsage.cs:21-46`, `:68-244` | Claude Code's hidden read: its floor, its windows, its `get_usage` body | E |
| `Accounts/Usage/AccountLimits.cs:121-125`, `:197-208`, `:296-328` | one reader holding two grammars (Claude Code's `·` clause and weekdays, Codex's clause and dates); Claude Code's sentences | E |
| `Accounts/Usage/AccountLimits.cs:330-361` | Codex's limit words declared for `codex-acp` | A — the right words in the wrong place |
| `Accounts/AccountReadings.cs:42-49`, `:107-183` | the *one* frame reader is Claude Code's `rate_limit_info` parser, its overage fields in the generic table | A |
| `Accounts/AccountReadings.cs:299-335` | Claude Code's window keys | E |
| `Accounts/Domain/UsageProjection.cs:93-97`, `:121-122` | Claude Code's token weights for every agent | A |
| `Accounts/Domain/SwitchDecision.cs:70-71` | Claude Code's one-minute floor for every agent's candidate | A |
| `Accounts/Domain/UsageCurves.cs:11-12` | window lengths, also at `ClaudeUsage.cs:30`, `CodexUsage.cs:97`, `AccountWindows.cs:55` | A — one fact in four places |
| `Accounts/AccountWindows.cs:67-81` | a weekly reset carried by `fixedWeek`, which the driver never reads from `WeekFixed`; only `Modules/Bridge/Routes/AccountsRoutes.cs:62` does | A |
| `Accounts/Usage.cs:69-76` | usage grouped by door, so one account on two doors is two rows | A |
| `Accounts/Harnesses/HarnessSelection.cs:33-36` | the key store's handle reasoned from Anthropic's key shape | A |
| `Accounts/Harnesses/HarnessSelection.cs:433-452` | no login question reads *unknown* (the doors, dsh) | E |
| `Accounts/Harnesses/HarnessSelection.cs:567-580` | a home variable required, its folder always made; `ProfileMustExist` read nowhere | A — a declared field nothing reads |
| `Accounts/Harnesses/HarnessActions.cs:378-447`; `Agents/AgentLoginCommand.cs:33-47`, `:79-82` | the sign-in by the tool's own flow, or its device code | E |
| `Accounts/SignIns/SignInPorts.cs:47-62`, `:127-142` | Codex's device code and its two ports | E |
| `Agents/AgentLoginCommand.cs:85-105` | the ports checked again inline | A — a copy |
| `Agents/AgentLoginCommand.cs:129-147`, `:187-201`, `:258-261` | the verb's name taken as the owner, safe only while doors declare no login | A |
| `Accounts/Harnesses/HarnessSettings.cs:919-926` | `*.lock` folders dropped for Claude Code's lockfile, for every agent | A |
| `Accounts/SignIns/AccountSignIns.cs:28-32`, `:52-67`; `Accounts/Rotation/RefusedAccounts.cs:17-34` | sign-in words: Claude Code's (declared on it and the stub), none for Codex or dsh | E |
| `Accounts/SignIns/ScratchSignIns.cs:21-48` | a *real* binary is one on the path; the stand-ins never are | E |
| `Accounts/Usage/UsageTurns.cs:23-29`; `Accounts/Usage/UsageHistory.cs:151-155` | a turn binds to the owner; its counts' meaning is the door's own, unknown for Codex | E |
| `Accounts/Rotation/LineAccount.cs:87-101`, `:262` | no toolchain, no account to decide | E |
| `Agents/AgentAccountsCommand.cs:147-156`; `Agents/AgentListCommand.cs:70-75`, `:118-146`, `:217-223` | a door lists its owner's accounts; the own sign-in line where the agent signs in itself | E |
| `Agents/AgentAccountsCommand.cs:711-721` | *speaks* recomputed with different holder handling | A |
| `Platform/ToolHomeLeftovers.cs:41-47` | `CLAUDE_CONFIG_DIR` hard-coded; Codex's home not listed | A |
| `Loop/Driver.cs:3285-3299`; `Conversations/Chat/ChatRunner.cs:1621`, `:1629` | a session's turns bound to its owner and account | E |
| `Loop/Driver.cs:3314-3340` | a refused credential read from the toolchain's words, under the owner | E |
| `Loop/DriverContinuations.cs:606-630` | an opening's account branched on the error reader's `By` suffix (`:words:refused`, `:words:sign-in`) | A — parses a spelling |
| `Conversations/Chat/CurveMove.cs:84-86` | a curve move needs an owner | E |
| `Modules/Bridge/Routes/AgentsRoutes.cs:206-216`; `Modules/Bridge/Routes/AccountsRoutes.cs:48-75`; `Modules/Bridge/AgentActions.cs:212-236` | account acts, grouping and sign-in through the owner (`codex-acp` signs in as `codex login`) | E |
| `Web/agents/tools.ts:226-229`, `:245-316` | the page rebuilds the agent from its doors by `AccountOf`, deduplicates accounts by home, takes the first definite login | A — the driver already groups |
| `Web/work/say.ts:211-222`; `Web/work/waits/limitWait.ts:63-64` | the door's owner resolved again to find its cooling | A |
| `Web/agents/agents.ts:120-121`; `Web/settings/accounts/accounts.ts:325-329`; `Web/settings/accounts/usageCurves.ts:19` | the two makers' windows (*session*, *weekly*) and their order hard-coded | A — twins of `UsageCurves` |
| `Web/settings/accounts/accounts.ts:306-318`; `Web/locales/en/harness.json:150-152` | the windows' names, Codex's *n-minute* among them | E |
| `Web/agents/agents.ts:546-556`; `Web/settings/accounts/AccountUse.tsx:327` | *own sign-in* or *installed*; an agent that says no windows | E |
| `Web/agents/AgentPage.tsx:813-838` | usage keyed and labelled by door id | A |
| `Web/agents/AgentsView.tsx:26-27` | Claude Code's one-minute floor applied to every agent's page | A |
| `Web/agents/SignInSteps.tsx:10-14`, `:148-177`, `:300-316` | the sign-in's progress scraped from each tool's text: Codex's local links and device code, Claude Code's *paste the code* | A — per-agent parsing in the page |
| `Web/agents/harnessRuns.tsx:108-112`, `:268-274`; `Web/locales/en/signin.json:33` | the device-code switch offered where the roster says | E |
| `tools/live-logins.mjs:34-35` | door to owner (`claude-code-acp` → `claude-code`, `codex-acp` → `codex`) | A — a twin of `AccountOf` |

## 4. Models, efforts and roles

**Today.** D205 carries a model and an effort at the session level: on the protocol door by `session/set_config_option`, found by
category (`model`, `thought_level`), refused before any prompt where not offered, the agent's own list kept as its offers; on the pipe
door by `--model` and `--effort`, a refusal read only after a silent turn's `model_not_found`. Claude Code's protocol door reports the
model a turn ran in its own `_meta`; Codex's reports none. The account settings (*account default*) are Claude Code's `settings.json`
schema behind a field that names only the file. The roles' choices hold door ids.

| Site | What differs | Kind |
|---|---|---|
| `Agents/Acp/Acp.cs:74-135`, `:596-614`, `:2572-2578` | options read from `configOptions`, set by `session/set_config_option`, replaced by the agent's update | E — the protocol door alone |
| `Agents/Acp/Acp.cs:165-173`, `:680-722`, `:829-836` | a model refused at the open, its re-ask | E |
| `Agents/Acp/Acp.cs:667-672`, `:728-761` | the carrier finds model and effort by category (Codex's effort `reasoning_effort`, Claude Code's `effort`) and accepts an alias back | E — no agent branch |
| `Agents/Acp/Acp.cs:2138-2160` | the per-model token split read from `result._meta.quota.model_usage` | A — one agent's `_meta` inline |
| `Agents/Acp/Acp.cs:3042-3046` | the turn's model read from `_meta["_claude/model"]`; Codex, dsh and the stub name none | A — one agent's key inline |
| `Agents/Streams/StructuredOutput.cs:118-127`, `:219-232` | the pipe door's model from the assistant message; `model_not_found` flags a refusal | E |
| `Accounts/Usage/TurnModels.cs:42-52`, `:131-156` | the model's provenance and token fields by wire | E |
| `Adapters.cs:2191`, `:2227`, `:2283`; `Sessions/Domain/ActorRun.cs:52-59` | `--model` and `--effort` on `claude-code` alone | E |
| `Sessions/Domain/ActorRun.cs:29-50` | what a spawn asked read from Claude Code's flags and `ANTHROPIC_MODEL`, `CLAUDE_CODE_EFFORT_LEVEL`, for any agent | A |
| `Sessions/Domain/ActorRun.cs:86-97` | an override reconciled by the protocol's categories | E |
| `Accounts/AgentSettings.cs:48-168`, `:234-262` | the generic account settings are Claude Code's: its aliases, its effort words, its keys; gated by the settings file's name alone | A |
| `Accounts/AgentOffers.cs:26-40`, `:83-97` | offers written at a protocol open, keyed by door; a list without a model is no pass, since Claude Code's lists aliases | E |
| `Accounts/Harnesses/HarnessRoster.cs:1010-1012`, `:1327-1331` | offers judged by door and version, accounts by owner | A |
| `Platform/ActorChoices.cs:137-144`, `:159-205` | *as before*: Build, Chat and Brainstorm from `adapter`, Intake from `intakeAdapter`, Ask Daoris from `helperAdapter`; the first agent written back to the old keys | A — one choice kept twice |
| `Platform/DriverConfig.cs:494-529`, `:578-580`, `:1081-1086`, `:1613-1637`; `Platform/DriverChoicesCommand.cs:534-588`, `:2130-2133` | the old keys read, written and set by their verbs beside the actors | A |
| `Loop/Driver.cs:2604`, `:2614` | the asked choice defaults to the adapter's own (`ActorAsked.Before`) | E |
| `Loop/Driver.cs:3356-3381` | the pipe door's refused model ends the part with no strike | E |
| `Loop/Driver.cs:3765-3799` | offers read and a refused model re-asked, the protocol capture alone | E |
| `Conversations/Chat/ChatRunner.cs:669-685`, `:900-904`, `:2463-2555` | a chat's pending replacement and its options only on the protocol door | E |
| `Modules/Bridge/Routes/ConversationRoutes.cs:326-343`, `:408-432`, `:521-535` | options and a turn's cancel by door | E |
| `Agents/AgentsCommand.cs:777-784`, `:822-823`; `Web/settings/accounts/AccountSettings.tsx:141`; `Web/locales/en/harness.json:72` | the settings' text names `.claude/settings.json` and `ANTHROPIC_MODEL` for every agent | A |
| `Web/settings/ai/Actors.tsx:26-27`, `:208-210` | the efforts offered for every agent are the pipe door's, `max` among them, which the driver refuses (`Accounts/AgentSettings.cs:65`, `:188`) | A — a twin that already disagrees |

## 5. A session's start, resume, going on and hand-over

**Today.** A protocol session opens with `initialize` and `session/new`, resumes by `session/resume` or `session/load` as the agent's
capabilities say, and takes later words in the same process; a pipe session is a process per run, resumed by Claude Code's
`--resume <id>` in a new process with the words as one argument, retried once where it exits before naming its conversation. Codex
refuses a resume while another client holds its thread (`thread_active_writer`). Each is written once for a driven session and once for
a conversation. A hand-over offers another agent's doors.

| Site | What differs | Kind |
|---|---|---|
| `Agents/Acp/Acp.cs:1639-1680` | the protocol open: `initialize`, `session/new {cwd, mcpServers, _meta?}`, options, carry, posture | E |
| `Agents/Acp/Acp.cs:826-827`, `:3396-3455`, `:3481-3485` | the protocol resume, its replay kept out | E |
| `Agents/Acp/Acp.cs:3457-3479` | the resume method from the wire's capabilities | E — no agent branch |
| `Agents/Acp/Acp.cs:3387-3394`, `:3433-3439` | a refused resume classed `Gone` by `-32002` and `Elsewhere` by Codex's `thread_active_writer` | A — one agent's reason in the shared door |
| `Agents/Acp/Acp.cs:2378-2404` | a polite `session/close`; the pipe door closes stdin | E |
| `Agents/Acp/AcpResumeRefused.cs:11-19` | a resume's refusal, protocol only | E |
| `Agents/Streams/HarnessCapture.cs:124-127`, `:164-176`; `Agents/Streams/StructuredOutput.cs:130-134`, `:179-184` | the pipe door's conversation id from its `init` line; the protocol door's from `session/new` (`Acp.cs:1666-1667`) | E |
| `Adapters.cs:2215`, `:2222-2231`, `:2282` | Claude Code's `--resume` for a driven session and a chat | E |
| `Loop/Driver.cs:1980-2011` | a resume of the same agent's own conversation, or a limit resume, protocol only | E |
| `Loop/Driver.cs:3864-3873`; `Loop/DriverContinuations.cs:645-659`; `Conversations/Chat/ChatRunner.cs:1808-1841`, `:2898-2918` | *a pipe run that exits before naming its conversation is tried once more*, three copies | A |
| `Loop/Driver.cs:3875-3883`; `Loop/DriverContinuations.cs:1184-1316` | the pipe door's go-on: held words resume its own conversation in a new process | E |
| `Loop/Driver.cs:4397-4441`; `Conversations/Chat/ChatRunner.cs:3045-3173` | the protocol resume written for a driven session and again for a chat | A — a copy |
| `Loop/DriverContinuations.cs:40-45`, `:185-220`, `:385-390` | the same agent's conversation; the protocol resumes on its wire, the pipe door prepares `PrepareResume` | E |
| `Loop/DriverContinuations.cs:54-63` | a limit resume refused unless `Wire == Acp`, since a pipe resume of a missing id is not known to fail closed | A — a door's fact as a wire test |
| `Conversations/Chat/ChatRunner.cs:1471-1482` | a chat's pipe door resumes at its spawn, its protocol door on the wire | E |
| `Conversations/Chat/ChatRunner.cs:2120-2133` | finishing: a structured door's finish, a text door's closed input | E |
| `Conversations/Chat/ChatGoOn.cs:8-52` | a go-on's words *sent*: on the wire for the protocol door, once named for the pipe door | E |
| `Conversations/Chat/ChatGoOn.cs:336-352`, `:566-592`, `:640-646`, `:741-747`; `Conversations/Domain/Continuation.cs:207-239` | `Continuations.Judge` asked the adapter's `Resumes` four times | E |
| `Conversations/Domain/Continuation.cs:30-46`, `:63-106` | the reasons a resume fails: `offered`, `gone` (protocol), `exited` (pipe), `elsewhere` (Codex) | E |
| `Conversations/Domain/ResumeAsk.cs:52-95` | a resume's words as one argument (pipe) or one block each (protocol) | E |
| `Conversations/GoOn/HarnessConversations.cs:31-55` | a conversation's id kept under the adapter that named it | E |
| `Help/HelpConversations.cs:320` | Ask Daoris resumable where its adapter resumes | E |
| `Host/HelpConsole.cs:47-58`; `Modules/Bridge/Routes/SessionsVerbs.cs:79-89`; `Modules/Bridge/Routes/HelpRoutes.cs:1361-1371` | *does this adapter resume* resolved three times, the terminal's without the plugins | A |
| `Trees/SessionGroups.cs:975-983` | a resume of the own conversation judged against the machine's adapter, where `DriverContinuations.ResumesOwn` judges the handed agent | A — two rules |
| `Loop/DriverHandOvers.cs:26`, `:306-315`; `Conversations/Chat/ChatHandOver.cs:138-150`; `Conversations/Chat/BriefedMove.cs:110-123`; `Conversations/Chat/LimitMove.cs:55`, `:513-514` | a hand-over's roster, its closing note on a structured door, a briefed move to an interactive agent, a limit move of a structured conversation | E |
| `Loop/DriverHandOvers.cs:353-365`; `Conversations/Chat/ChatHandOver.cs:203-217`; `Conversations/HandOver/HandOverQuest.cs:73-87` | an agent's accounts for a hand-over, three copies | A |
| `Web/work/start/handOverOffer.ts:38-86`, `:113-158` | the page decides whether to offer a hand-over and to which agent, a twin of `HandOverAsks.Steps` | A — a judgement on the page |
| `Platform/ToolHomeLeftovers.cs:54-55`, `:67-99` | Claude Code's conversation folders (`projects/`, its path flattening) under a generic name | E — Claude Code's layout |
| `Web/locales/en/note.json:167-168` | the pipe door's resume worded | E |

## 6. The doors and what each supports

**Today.** The protocol door supports, by the wire's own answers: model and effort options, per-call permission requests (refused, or
held for the person under the asking posture), postures by `session/set_mode`, mid-turn words and steering (Claude Code's adapter
alone), a cancel that keeps the session, stopping one background task, the transient retry inside the session, waiting on the
session's own background work and a turn the harness woke, servers and rules on the wire, children as their own sessions, a resume with
its replay kept out, and every plugin's harness. The pipe door supports only what the one agent's mapper reads (Claude Code's
`stream-json`; a pipe agent without a mapper is plain text), takes its settings, servers and plugins as files on the command line, keeps
the harness's own lines for a refused credential, reports its context by computing it, and ends its background work with the turn.
Which door the machine has is decided in five places.

| Site | What differs | Kind |
|---|---|---|
| `Adapters.cs:1293-1314`, `:1330` | `SessionWire {Pipe, Acp}`; an adapter that says nothing is a pipe | E |
| `Adapters.cs:1506` | `Resumes` defaults to *protocol* | E |
| `Adapters.cs:1694`, `:1773`, `:1864`, `:2069`, `:2184`, `:1460-1463` | `Interactive` declared by five adapters, never by dsh; a chat on one that is not refused | E |
| `Plugins/DeclaredAcpAdapter.cs:21-44`, `:61-62` | a plugin's harness: always protocol, its posture the manifest's, interactive | E |
| `Plugins/DeclaredAcpAdapter.cs:21` | every other member at its default: no rules, no skills, no full or asking mode, and `Resumes` true by the door | A — capabilities by omission |
| `Accounts/Harnesses/HarnessRoster.cs:392`, `:403-407` | the roster's door, and *structured* = protocol or a pipe with a mapper | E |
| `Agents/Streams/StructuredOutput.cs:36-78` | `IStreamMapper`, the pipe door's reader seam, implemented by Claude Code's alone | E |
| `Agents/Streams/HarnessCapture.cs:6-11`, `:46-78` | the plain pipe capture for an agent with no mapper: no events, usage or conversation | E |
| `Agents/Acp/Acp.cs:2671-2692` | only `session/request_permission` answered; no filesystem or terminal offered | E |
| `Agents/Streams/ClaudeStreams.cs:23-25` | nothing stops one task on the pipe door | E |
| `Loop/Driver.cs:662-690`, `:1035-1036`, `:1557` | the loop's door: protocol where any of Build's choices is, threaded into the planner and set-up | A |
| `Modules/Bridge/DriverContext.cs:155-172` | the window's door: the configured adapter's, read at fourteen places in the routes and slices | A — another rule |
| `Host/SessionsConsole.cs:37-48`; `Host/DriverChoicesConsole.cs:56-71` | the terminal's door: the configured adapter's, the first without the plugins | A — two more |
| `Setup/SetupWorld.cs:20-31` | the set-up's door: the configured adapter's, an unknown one a pipe | A — a fifth |
| `Quests/Domain/Planner.cs:1309-1324`; `Trees/SessionGroups.cs:837-843`, `:859-869`; `Trees/SessionTrees/EntryAccepts.cs:55-59`; `Sessions/SessionsCommand.cs:88`, `:710`, `:1409`, `:1739`, `:1751`; `Sessions/Work/WorkPausing.cs:13`, `:390`; `Sessions/Work/WorkAbandoning.cs:389` | one machine door threaded as a parameter | A |
| `Loop/Driver.cs:2738-2750`, `:3729-3748`, `:4366-4396` | a pipe run takes no words mid-run; its words wait for the run's end, the protocol's at the next step or steered | E |
| `Loop/Driver.cs:2754-2767`; `Loop/DriverContinuations.cs:440-462` | the protocol's background, offers and retry callbacks built for every start | A |
| `Loop/Driver.cs:3806-3825` | three captures: protocol, structured pipe, plain text | E — the core split |
| `Loop/DriverContinuations.cs:1145-1168` | a go-on only for a pipe door that resumes | E |
| `Conversations/Chat/ChatRunner.cs:561-570`, `:1641-1752`, `:2341-2357`, `:3202-3715`, `:3729-3832` | a chat's three doors: the protocol chat, the native chat, plain text; reach and stop per door | E |
| `Conversations/Chat/ChatRunner.cs:739-753`, `:1105-1133`, `:1296-1298` | a place carries door knobs: `ConnectorOnPipe`, a posture, tools up front | A |
| `Conversations/Chat/ChatContext.cs:44`, `:57`, `:132`; `Conversations/Domain/ChatQueue.cs:148-157`, `:187-196` | the live and the structured doors; `TurnDoor(Steers, KeepingStop)`; *send now* refused per door | E |
| `Conversations/DrivenInbox.cs:142-195`; `Sessions/SessionProcesses.cs:72-133`; `Modules/Loop/SessionWords.cs:53`, `:318-333` | capabilities attached at run time: steers, keeps a stop, reaches the next step; task stops on the protocol door | E — a seam already |
| `Modules/Bridge/Routes/AgentsRoutes.cs:123-139` | the page's roster: owner, product, maker, wire `pipe`/`acp`, structured, takes rules | E — the door's string leaves for the page |
| `Web/agents/tools.ts:215-218` | the page folds an adapter to `acp`, `pipe` or none, and reads rules back from it | A |
| `Web/work/say.ts:309-320`; `Web/work/WorkFrame.tsx:783-784` | 24,000 characters the cap on a pipe door's words, on the page alone | A — no driver twin |
| `Web/work/WorkFrame.tsx:2635-2636`; `Web/help/AskConversation.tsx:290-291`, `:923` | *can be stopped* inferred from *structured* | A |
| `tools/rehearsal-kit.mjs:444-480`, `:821-858`, `:922-938`; `tools/deployment-rehearsal.mjs:1036-1046`, `:1583-1588` | the stand-ins: a protocol stub declaring Claude Code's `promptQueueing`, a turn-end stub shaped on Codex, both doors driven | E |

## 7. The turn and its prompt

**Today.** Daoris composes one instruction and hands it through the adapter's spawn: in every spawn's environment as `DAORIS_TARGET`,
and on Claude Code's pipe door also as the `-p` argument. On the protocol door the target is `session/prompt`, a file as a
`resource_link`, a stop `session/cancel`; Claude Code's adapter queues words mid-turn and steers (`promptQueueing`, `_session/steering`),
Codex's resets its turn on a second prompt and so takes words only at a turn's end. On the pipe door a chat's message is a `stream-json`
line and its stop a `control_request`.

| Site | What differs | Kind |
|---|---|---|
| `Adapters.cs:1540`, `:1549`, `:1563`, `:1571` | `DAORIS_TARGET` composed (`TargetPrompt.Compose`) into every spawn's environment, inside the adapter seam | A — composition in the agent's spawn |
| `Adapters.cs:445-446`; `Loop/Driver.cs:3714-3715`, `:3724` | the instruction bound per start with the adapter's maker trailer | E |
| `Adapters.cs:2190` | Claude Code's pipe door takes the target on its command line | E |
| `Adapters.cs:2237-2253`; `Conversations/Chat/ChatRunner.cs:2165-2205`, `:2223-2235`, `:2281-2294`, `:2326-2331`, `:3729-3832` | a message framed as a `stream-json` line, a stop as a `control_request`; a text door sends raw text and cannot stop a turn | E |
| `Agents/Streams/StructuredOutput.cs:410-424` | the answer to the pipe door's interrupt | E |
| `Agents/Acp/Acp.cs:838-845`, `:1703-1719`, `:1837`, `:1845-1857` | words sent mid-turn only where `initialize` says `_meta.claudeCode.promptQueueing`; otherwise held to the turn's end | E; the `claudeCode` key read inline A |
| `Agents/Acp/Acp.cs:1721-1734`, `:1883`, `:1885-1981` | *send now* steers where queueing and `_meta.steering.supported` both hold, by `_session/steering` | E |
| `Agents/Acp/Acp.cs:1256-1258`, `:1359-1387`, `:2030-2033`, `:2232-2239` | queued prompts settled the way Claude Code's adapter answers them | E |
| `Agents/Acp/Acp.cs:1811-1818`, `:2162-2167` | a prompt's files as `resource_link` blocks | E |
| `Agents/Acp/Acp.cs:2340-2360` | a stop is `session/cancel`, then the held permissions released | E |
| `Loop/DriverContinuations.cs:1337-1377` | the pipe door's words joined into one argument, its opening note `resumed.native` | E |
| `Conversations/Chat/ChatHandOver.cs:238-266`; `Conversations/Chat/ChatHelperWords.cs:59-81` | a hand-over's note and a helper's answer framed for a text door | E |
| `Web/work/conversation/Composer.tsx:401`, `:640-642` | a text door has no levels, its words go at once | E |
| `tools/knowledge-bench.mjs:498-510` | the bench runs `claude -p` with its flags | A |

## 8. The event stream

**Today.** Both doors write one shape of a session's events (`user`, `message`, `thought`, `tool`, `plan`, `usage`, `turn`, `note`,
`transient`, `raw`, `receipt`; `Sessions/Domain/SessionEvents.cs:13-64`) in the protocol's tool kinds, and a child's words go to a console
stream beside the session, never into it. The protocol door reads them from the wire, where Claude Code's adapter adds its own
extensions (subagent sessions, async tasks, a woken turn, rate limits, the model) that the shared door reads by name; the pipe door
reads them through Claude Code's mapper. Children are announced three ways (the protocol door's `subagent_spawned`, the pipe door's
`task_started`, Claude Code's hook), and a Codex child exists only through its calls, which say no end.

| Site | What differs | Kind |
|---|---|---|
| `Sessions/Domain/SessionEvents.cs:10`, `:117`, `:327-353` | one shape of events in the protocol's vocabulary; a tool's name only from the pipe door's refusals | E |
| `Agents/Acp/Acp.cs:34` | `AcpUsage`, named for one door, carries both doors' context | A |
| `Agents/Acp/Acp.cs:141-158` | stop reasons, `refusal` among them | E |
| `Agents/Acp/Acp.cs:317`, `:1686`, `:1694`, `:2535-2569` | the protocol door's streams and its updates routed by session id | E |
| `Agents/Acp/Acp.cs:1765-1796` | `initialize` asks every protocol agent for `_meta.jetbrains.air {nativeSubagentSessions, asyncTasks}`, which only Claude Code's adapter reads | A — one adapter's extension sent to all |
| `Agents/Acp/Acp.cs:872-876`, `:995-1104` | a turn that ends with its own background work waits for it, then for a woken turn, else Daoris's go-on | E — Claude Code's adapter alone does this |
| `Agents/Acp/AcpWake.cs:27-38`, `:60-102`, `:216-230` | a woken turn read from `_meta["_claude/origin"]`, a subagent's step from `_meta.claudeCode.parentToolUseId`, a 60-second wait from Claude Code's measured wake | A — one agent's vocabulary and timing in the shared door |
| `Agents/Acp/AcpBackground.cs:10-19`, `:42-79`, `:147-160` | background work from Claude Code's async-task frames, a 250 ms settle for its *stopped then completed* | A |
| `Agents/Acp/Acp.cs:3098-3099`, `:3174-3176` | the async-task updates kept out of the console and the record | A |
| `Agents/Acp/Acp.cs:411-415`, `:1983-2028` | calls a steer cut, closed `cut` | E |
| `Agents/Acp/Acp.cs:2183-2197`; `Agents/Streams/StructuredOutput.cs:309-370` | a turn's end and tokens: camelCase on the protocol door; `result`'s subtypes and snake_case on the pipe door, mapped to the protocol's stop words | E |
| `Agents/Acp/Acp.cs:3048-3053`; `Agents/Streams/StructuredOutput.cs:329-334`, `:426-445` | context reported by the protocol door, computed on the pipe door, against `Acp.cs:26-29`'s *reported, never computed* | E; the claim A |
| `Agents/Acp/Acp.cs:3501-3642`; `Agents/Streams/StructuredOutput.cs:84-87`, `:186-209`, `:240-246` | words joined after a quiet on one door, streamed then whole on the other | E |
| `Agents/Acp/AcpStreams.cs:77-103`, `:152-204` | children from `subagent_spawned` and `subagent_state_update` by `subagentSessionId`; their words by session id | E |
| `Agents/Acp/AcpStreams.cs:36-37`, `:169-181`; `Agents/Streams/StructuredOutput.cs:491-500` | a child's card: kind `think` on the protocol door, the `Task` call's kind `other` on the pipe door | A — the doors' cards disagree |
| `Agents/Acp/AcpStreams.cs:85-92`, `:206-258`; `Agents/Streams/ClaudeStreams.cs:46-51`, `:169-187` | a task's output path on the wire, or read from the harness's words by a pattern | E |
| `Agents/Acp/AcpStreams.cs:309-323`; `Agents/Streams/ClaudeStreams.cs:262-275` | the endings' words twice, `killed` on the pipe door alone | A — twin code |
| `Agents/Streams/StructuredOutput.cs:6-34` | the mapped line's limits, outside lines, failure and refused model, shaped by Claude Code | A |
| `Agents/Streams/StructuredOutput.cs:80-177`, `:136-137`, `:447-538` | Claude Code's frames, its `Beside` streams, its tool names to the protocol's kinds | E |
| `Agents/Streams/ClaudeStreams.cs:27`, `:73-81`, `:95-167`, `:199-211` | children and tasks from `task_started` (`local_agent`, `local_bash`), lines by `parent_tool_use_id` | E |
| `Agents/Streams/HarnessCapture.cs:80-120`, `:145-162`, `:202` | the structured pipe capture: the mapper, the harness's own words kept apart | E |
| `Agents/Streams/HarnessCapture.cs:110-113`, `:123`, `:151-153`; `Sessions/ChildLineage.cs:141-152` | every structured pipe's stdout read by Claude Code's lineage reader; the protocol door writes no stream lineage | A |
| `Sessions/Domain/ChildBindings.cs:40-60`, `:128-266` | one fold with a branch per harness: Claude Code by hook, stream and joined calls, Codex by its calls' thread | E |
| `Sessions/Domain/ChildBindings.cs:293-323` | Claude Code's `task_started` parsed again for the lineage, a third reader beside the mapper and its streams | A |
| `Sessions/Domain/Observation.cs:564-676` | Claude Code's `stream-json` parsed again in Sessions (`HarnessWords`), a fourth reader | A |
| `Sessions/ChildLineage.cs:6-13`, `:37-41`, `:66-83`; `Agents/Permissions/Permissions.cs:561-581`; `Loop/Driver.cs:3134-3143` | the lineage hook handed in Claude Code's settings, so only an agent that takes settings has one | A |
| `Sessions/hooks/lineage-hook.mjs:1-46` | Claude Code's hook input | E |
| `Sessions/Domain/Observation.cs:148-157` | the pipe door ends background work with the turn | E |
| `Sessions/Heavy/LongCalls.cs:14-21`, `:155-160` | a background task's bound only from the protocol door | E |
| `Loop/Driver.cs:2800-2809`, `:2828-2831`, `:3853-3862`, `:4174-4202`, `:4347-4364` | each door's end signals, usage, background watch, limits and task stops at the run's conclusion | E |
| `Conversations/Chat/ChatRunner.cs:2829-2925`, `:2958-2965` | a chat's three captures and their usage | E |
| `Core/Sessions/CallLineage.cs:26-80`; `Core/Sessions/LineageFile.cs:73-90`; `Mcp/CallLineageFilter.cs:14-22` | the service reads each agent's `_meta` on a call: `claudecode/toolUseId`, or `x-codex-turn-metadata` | E; read in the service |
| `tools/fabric-probe/receipt.mjs:57-78` | the same keys read by a probe | A — a twin |
| `Web/work/WorkFrame.tsx:160-161`, `:2650-2653`; `Web/work/conversation/ConversationView.tsx:178-187`; `Web/work/conversation/ContextRing.tsx:28-34`; `Web/work/console/MonitorWindow.tsx:218-222`; `Web/bridge/agents/agents.ts:47-49`; `Web/locales/en/work.json:170` | a text door keeps no conversation, reports no context, shows a console | E |
| `Web/work/noteLines.ts:61-62`, `:255-260` | the pipe door's note codes worded (`ended.background-cut`, `resumed.native`) | E |

## 9. Connectors

**Today.** Daoris's connector is handed by the protocol door on the wire (`mcpServers` on `session/new`, the session's scope in the
entry's variables); a driven pipe session gets plugin servers in a Claude Code `.mcp.json`-shaped file by `--mcp-config` and relies on
the repository's own `.mcp.json` for the connector, so a pipe-door agent cannot drive a repository that has not adopted Daoris, while a
helper's, a reviewer's, a partner's and an intake's pipe spawn are handed the connector in that file. Claude Code
hands its servers its whole environment, Codex only each entry's own (CONNECTOR2), so the session's identity reaches Codex's connector
only through the entry. A Claude Code account's own connectors reach its sessions unseen (CONNECTORS1's row).

| Site | What differs | Kind |
|---|---|---|
| `Agents/Acp/Acp.cs:36-57`; `Loop/LoopContext.cs:84`, `:206` | `AcpMcpServer`, the protocol door's record, is both doors' server shape | A |
| `Agents/Acp/Acp.cs:1736-1763` | servers on the wire, their variables as `{name, value}` | E |
| `Adapters.cs:2309-2313` | `--mcp-config <file>` on Claude Code's pipe door | E |
| `Agents/Permissions/SpawnServers.cs:5-53` | the pipe door's file in Claude Code's `.mcp.json` layout for every pipe agent | A |
| `Agents/Permissions/SpawnServers.cs:55-68` | the door tested inside the helper | A |
| `Loop/DriverHelpers.cs:993-1000`; `Loop/DriverOpinions.cs:203-212`; `Loop/DriverPartners.cs:622-629`; `Loop/DriverIntakes.cs:324-336`; `Conversations/Chat/ChatRunner.cs:1544-1549` | the callers test the door again before the helper | A — five copies |
| `Loop/Driver.cs:2720-2726`; `Loop/DriverBrainstorms.cs:728-731`; `Loop/DriverContinuations.cs:399-421` | other callers rely on the helper's own test, or repeat the first start's handing | A |
| `Service/KnowledgeConnector.cs:3-18`; `Loop/Driver.cs:4318-4329`, `:4525-4526`; `Conversations/Chat/ChatRunner.cs:3010-3030` | the connector offered on the protocol door, or by a room that hands it on the pipe door | E |
| `Service/KnowledgeConnector.cs:80-106` | the home's variable always carried in the entry, since Codex hands a server only its entry | E — handled without a branch |
| `Agents/Permissions/Permissions.cs:105-106`, `:111-143`, `:204-213`; `Service/KnowledgeConnector.cs:26-32` | the connector's tools named in Claude Code's `mcp__daoris-knowledge__<tool>` | A |
| `Loop/Driver.cs:735-746`; `Conversations/HandOver/HandOverQuest.cs:199-205`; `Quests/Domain/Planner.cs:1923-1949`; `Setup/SetupPress.cs:76-82` | *a pipe agent cannot drive an unadopted repository* (`no-connector`, `NoDoor`, `NoConnector`), four places | A — one fact decided four times |
| `Web/projects/ProjectsView.tsx:280-282`, `:317` | and a fifth on the page | A |
| `Web/locales/en/work.json:358`; `tools/family-rehearsal.mjs:5019-5051`; `tools/testbed.mjs:127`, `:180-215` | that difference worded and rehearsed | E |
| `Web/help/access/WhatItHas.tsx:100` | a tool's name shown by Claude Code's `mcp__server__tool` split | A |
| `tools/service-publish.mjs:134-137` | the published snippet is a `.mcp.json` alone | A |
| `tools/fabric-probe/launch.mjs:54-58`, `:87-94`, `:156`, `:202`, `:284-290` | the probe's launchers and naming per agent | E — measurement |

## 10. Permissions and skills

**Today.** Daoris's permission rules are Claude Code's allow, ask and deny lists in its own grammar, handed as a settings file (`--settings`
on the pipe door, `_meta.claudeCode.options.settings` on its protocol door) with the hooks and the auto mode's hard deny; Codex and dsh
take none (`TakesSettings` false), and a plugin's harness none. Postures are each agent's own words: Claude Code's `auto`/`acceptEdits`,
`default`, `bypassPermissions`; Codex's `agent`, `read-only`, `agent-full-access`; dsh's an environment variable. Skills are composed as
Claude Code plugins (`--plugin-dir`, or the `_meta`'s `plugins`); dsh reads `.claude/skills`. Trust is Claude Code's `.claude.json` flag.

| Site | What differs | Kind |
|---|---|---|
| `Adapters.cs:1845`, `:1852`, `:1858`, `:2075`, `:2083`, `:2090`, `:2202`, `:2281`, `:2293`, `:2300` | the postures, each agent's words | E |
| `Adapters.cs:1981-2015` | dsh's posture as `DSH_PERMISSION_MODE=workspace-write` at its spawn | E |
| `Adapters.cs:1881`, `:2316`, `:1889-1906`, `:2323-2327`, `:2334-2341` | Claude Code takes rules and plugins: by its `_meta` on the protocol door, by `--settings` and `--plugin-dir` on the pipe door | E |
| `Agents/Acp/Acp.cs:266-281`, `:313-314`, `:1650-1652`, `:3422-3424` | the posture and the `_meta` handed to the open and the resume | E |
| `Agents/Acp/Acp.cs:326-336`, `:374-375`, `:2994-3009` | two marks (`!` required, `?` the person answers) packed into the posture string | A |
| `Agents/Acp/Acp.cs:2912-2992` | the first of the adapter's `|`-separated modes the agent offers, set by `session/set_mode`; none for dsh and the stub | E |
| `Agents/Acp/Acp.cs:2587-2598`, `:2694-2755`; `Agents/Streams/StructuredOutput.cs:95-100`, `:290-293`, `:319-327`, `:372-408` | a refused call: the protocol door's request refused or marked, the pipe door's own denials read | E |
| `Agents/Acp/Acp.cs:2757-2910`; `Sessions/Events/HeldPermissions.cs:73`; `Loop/Driver.cs:4333-4346` | a request held for the person, the protocol door alone | E |
| `Agents/Permissions/Permissions.cs:81-97`, `:144-170`, `:179-184`, `:250-255` | the rule model, its defaults in `Bash(...)` grammar and its shape are Claude Code's | A — one agent's grammar as the product's |
| `Agents/Permissions/Permissions.cs:42-48`, `:188-198`, `:215-248`, `:487-588` | Claude Code's hard deny, its absolute path forms, its settings file with hooks | E |
| `Agents/Permissions/Across.cs:107-155`; `Agents/Permissions/RuleProposals.cs:253-280` | reaching across and a proposal expressed in Claude Code's grammar | A |
| `Agents/Permissions/SpawnSkills.cs:6-31`, `:57-90` | skills as Claude Code plugins | E |
| `Agents/Permissions/SpawnSkills.cs:92-103` | the door tested inside; on the protocol door skills ride the rules' `_meta`, which only Claude Code's adapter reads | A |
| `Agents/Adapters/DshProfile.cs:48-56`, `:108-118` | dsh's skills from `.claude/skills` | E |
| `Sessions/Domain/DrivenAccess.cs:90-137`, `:145-148` | a driven session's mode per door: the pipe's `--permission-mode`, the protocol's posture with its marks; Codex's *keeps no rules* clause | E |
| `Sessions/Domain/AccessRules.cs:117-127` | the *questions* pause's one value is `claude-code` | A — an agent id as a setting's value |
| `Loop/Driver.cs:2068-2079`, `:3041-3044`, `:3097-3098`, `:3175-3186` | an unposed start held; rules and the connector's allowance only where the agent takes them | E |
| `Loop/Driver.cs:3147-3153`; `Conversations/Chat/ChatRunner.cs:1503-1526` | rules handed by flag or by `_meta`, the door branch written twice | A |
| `Loop/Driver.cs:2282-2303`; `Loop/DriverIntakes.cs:262-272`; `Modules/Bridge/Routes/DriverRoutes.cs:367-398` | the trust preflight and its grant: `ClaudeTrust` in the loop, an intake and a route | A |
| `Accounts/SignIns/ClaudeTrust.cs:79-141`, `:164-224` | Claude Code's trust flag in `.claude.json` | E |
| `Accounts/SignIns/ClaudeTrust.cs:235-237`, `:251-259`, `:269-273` | its sentences name `claude` and `daoris agent trust claude-code`; its own-home default is Claude Code's | A |
| `Agents/AgentsCommand.cs:677-691` | trust only where a trust file is declared | E |
| `Agents/AgentsCommand.cs:497`, `:694-697`, `:730-731`, `:755-759` | *Claude Code's own permission rules* and `claude` in the verbs' text; Claude Code's writer for any trust file | A |
| `Loop/DriverHelpers.cs:970`; `Loop/DriverOpinions.cs:121`, `:159`; `Review/Domain/OpinionPermissions.cs:115-134`; `Sessions/Heavy/HeavyTold.cs:57-80` | a reviewer's and a helper's posture: rules and a copy, or a copy alone | E |
| `Loop/DriverPartners.cs:544-550` | a partner admitted only by a measured row for its agent and version | E |
| `Conversations/Domain/PartnerConfiguration.cs:47-51` | a partner's posture read from `AcpPosture` whatever its door | A |
| `Conversations/Chat/ChatRunner.cs:837-849`, `:956-966`, `:1142-1172` | Ask Daoris's access per door | E |
| `Help/HelpMode.cs:82-120`; `Help/HelpRoom.cs:50-62` | the restricted mode's sentence names Claude Code's `acceptEdits`; `default` asked of every protocol agent | A |
| `Help/HelpRoom.cs:166-178`; `Quests/IntakeRoom.cs:49-57`, `:67-84`, `:231-240`; `Loop/DriverIntakes.cs:344-353` | Ask Daoris's and an intake's rooms write `CLAUDE.md` and `.claude/settings.json`, their allowances in Claude Code's tool names, for every agent | A |
| `Core/Help/RuleProposals.cs:54`, `:197-201`; `Mcp/RuleProposalTools.cs:16-21`, `:33` | a rule proposal checked against Claude Code's shape | A |
| `Web/bridge/agents/rules.ts:8-11`; `Web/settings/repository/AgentRules.tsx:325`; `Web/projects/setup/RepositoryRules.tsx:80`; `Web/locales/en/settings.rules.json:2`, `Web/locales/en/projects.json:227` | the rules typed, shown and titled as Claude Code's | A |
| `Web/settings/repository/Access.tsx:39-49`; `Web/work/AccessFact.tsx:46-48`; `Web/locales/en/settings.access.json:39`, `Web/locales/en/work.json:394` | the *questions* pause and its words, as Claude Code asks | A — a twin of `AccessRules` |
| `Web/settings/AiDomain.tsx:144-147`; `Web/help/access/grants.ts:13-18`, `:43`, `:62-64` | the page judges a grant available by *takes rules*, an unknown helper assumed to take them | A |
| `Web/agents/AgentsView.tsx:84-93`; `Web/work/waits/TrustAsk.tsx:7-31`; `Web/locales/en/help.json:210`, `:260` | the rules' link opens the agent that takes them; the trust question; *takes no rules file* | E |
| `tools/acp-trust-probe.mjs:45-60` | the trust probe reads Claude Code's file | E |

## 11. Process, containment and the environment

**Today.** Every adapter spawns through one shell (`Spawning.Shell`) and every start through the launcher's containment, which name no
agent. What differs is what the spawn sets: each agent's home variable and key, Claude Code's `DISABLE_UPDATES` on a pin and
`ENABLE_TOOL_SEARCH=false` for Ask Daoris, `CLAUDE_CODE_EXECUTABLE` pointing Claude Code's adapter at a managed `claude`, dsh's posture and
its telemetry patch; and how the process ends: a protocol agent is expected to exit once its input closes.

| Site | What differs | Kind |
|---|---|---|
| `Adapters.cs:1528-1604`, `:1659-1675` | the role variables by target kind and the roles dropped (ENVLEAK1); stdin open for protocol doors (`:1532`, `:1776`, `:1869`, `:2006`, `:2095`) | E |
| `Adapters.cs:1606-1653` | one shell for every adapter | E — one place already |
| `Adapters.cs:1743-1748`, `:1800-1805` | a stand-in requires a configured command; a real agent defaults to its binary | E |
| `Adapters.cs:2132-2153`; `Accounts/Harnesses/HarnessRoster.cs:1303-1308`; `Accounts/Harnesses/HarnessSelection.cs:562-565`, `:603-617`; `Loop/Driver.cs:2996-3010`; `Conversations/Chat/ChatRunner.cs:1483-1498` | `CLAUDE_CODE_EXECUTABLE` and `DISABLE_UPDATES` resolved from Claude Code's pin for every protocol door, Codex's and dsh's included, carried on the generic selection | A |
| `Adapters.cs:2442-2443`, `:1912`; `Conversations/Chat/ChatRunner.cs:1495-1497` | `ENABLE_TOOL_SEARCH=false` on both Claude Code doors for a room that asks | E |
| `Agents/Adapters/DshProfile.cs:20-121` | dsh's profile patch turning its telemetry off | E |
| `Agents/Acp/AcpEnding.cs:41-57` | a protocol agent that lingers after its input closes is ended and read as clean | E |
| `Agents/Acp/AcpEnded.cs:11-15` | a run's own end of its stream, protocol only | A — no pipe twin |
| `Modules/Platform/HolderTerminal.cs:4-11`, `:31-55`, `:216-240` | the role holder pinned to `claude-code`: its home variable, its argv, its binary's name | A — one agent's command line outside its adapter |
| `Web/settings/wiring/WiringToolHome.tsx:7-8`, `:22-35`; `Web/locales/en/settings.daoris.json:298-305` | the tool's home shown as Claude Code's alone | A |
| `tools/question-bench.mjs:395-401`; `tools/acp2-proof.mjs:17`, `:67-68`, `:112` | Claude Code's environment markers stripped; its home as the account seam | A; E |

## 12. Errors and their classes

**Today.** D210's reader is one place: a door hands a `DoorFailure`, `AgentErrors.Read` reads it into a limit, access, transient or fatal by
the adapter's own table and then its owner's. Claude Code has tables (its provider's kind words, statuses and recorded sentences; its
protocol adapter's `-32000`); Codex has none, so its transient errors read fatal, and `codexErrorInfo`, which D210 point 1 names for
Codex's limit, is read by no product code (a test fixture alone carries it, `Driver.Tests/Agents/AcpTests.cs:2946`). The transient retry
runs inside a protocol session alone. A failure travels in two channels: the protocol door's `turnFailed`, and `HarnessEnding.Failure`
for both doors; the conclusion's limit and sign-out readers take the first, so a pipe door's failed `result` cools no account and signs
out none (the three callers of the roster's `Limited`, `Loop/Driver.cs:3248`, `Loop/DriverBrainstorms.cs:778` and
`Conversations/Chat/ChatRunner.cs:2736`, each read the protocol channel).

| Site | What differs | Kind |
|---|---|---|
| `Agents/Errors/AgentErrors.cs:21-52` | where a failure is met (`open`, `prompt` on the protocol door; `result`, `harness` on the pipe door) and the door's name | E |
| `Agents/Errors/AgentErrors.cs:133-205`, `:249-261` | the order and the tables, own then owner | E |
| `Agents/Errors/AgentErrors.cs:144-152` | the pipe door's harness lines read for access alone | E |
| `Agents/Errors/AgentErrors.cs:84-88`, `:167-171`, `:325-344` | `AuthRequiredCode` from the door's own table: Claude Code's protocol adapter's `-32000` | E |
| `Agents/Errors/AgentErrors.cs:277-316` | Claude Code's provider words | E |
| `Agents/Errors/AgentErrors.cs:318-322` | the stub mirrors Claude Code's codes | A |
| `Agents/Errors/AgentErrors.cs:277-344` (absent) | no Codex or `codex-acp` table; `codexErrorInfo` read nowhere | A — a gap |
| `Agents/Acp/Acp.cs:175-239` | one refusal reader merges three agents' error shapes (`data.message`, `data.details`, `data.errorKind`, `data.reason`) | A |
| `Agents/Acp/Acp.cs:1106-1351`, `:1863-1880` | the transient retry inside the protocol session | E |
| `Agents/Acp/Acp.cs:1182-1183` | the open's retry skips Codex's `thread_active_writer` | A |
| `Agents/Acp/TransientRetry.cs:3-16`, `:30-31` | the shared policy typed on the protocol door's `AcpRefusal` | A |
| `Agents/Acp/TransientRetry.cs:231-246` | a word's turn failed, the retry exhausted | E |
| `Loop/TransientRetries.cs:16-68`; `Conversations/Chat/ChatRunner.cs:1700-1718` | the reader handed to a run, written twice | A — a copy |
| `Loop/Driver.cs:3800-3805` | the retry only for a protocol door's quest session | E |
| `Loop/Driver.cs:2842-2852`; `Loop/DriverContinuations.cs:755-762` | four account readers at the conclusion, two channels, written twice | A |
| `Loop/Driver.cs:3233-3275`, `:3383-3387`; `Loop/DriverBrainstorms.cs:750`, `:774-784`; `Conversations/Chat/ChatRunner.cs:2731-2776` | the limit and sign-out readers take the protocol channel alone (three copies of the limit reader) | A — the pipe door's limit cools nothing |
| `Loop/Driver.cs:3761`, `:3846-3848`, `:3894-3905`, `:4035-4038`, `:4442-4456`, `:4474-4481`, `:4504-4518` | the ending per door; a timeout told to the protocol door first; lingering background work ended | E |
| `Sessions/HarnessEnding.cs:19-80`; `Sessions/Domain/Observation.cs:479-484` | one ending with fields only one door fills (background cut, provider refused, model refused) | E |

## 13. Anything else

| Site | What differs | Kind |
|---|---|---|
| `Adapters.cs:2377`, `:2488`; `Accounts/Harnesses/Harnesses.cs:210`, `:214`, `:255`, `:259`; `Plugins/Domain/PluginCatalog.cs:122` | twin claims naming the CLI's `toolchain.ts` and `reservedHarnesses()`, deleted at `4cc4c0cb1` (PUSH1f2f, 2026-10-22) | A — stale claims |
| `Agents/Streams/ClaudeStreams.cs:119`, `:209`, `:266`; `Agents/Streams/StructuredOutput.cs:251`, `:396` | the pipe door's readers call a text helper on the protocol door's class (`AcpSession.OneLine`) | A |
| `Cli/layout/harness.ts:27-105`; `Cli/layout/layout.ts:15-22`; `Cli/layout/rooms.ts:1-19`, `:59`; `Cli/layout/links.ts:56`; `Cli/doctrine/region.ts:200-210`; `Cli/doctrine/drift.ts:331-335`; `Cli/doctrine/materialize.ts:853`; `Cli/cli/init.ts:11`; `Core/Registry/RepositoryLayout.cs:38-50`, `:108-111`; `Core/Registry/RepositoryLinks.cs:62` | the doctrine's layouts per agent (where it reads its instructions and skills, `CLAUDE.md` and `AGENTS.md`, the 32,768-byte cut), with the service's twin | E — the doctrine's, out of the contract's scope |
| `Cli/layout/harness.ts:108-117`; `Cli/layout/layout.ts:105-125`; `Cli/doctrine/config.ts:14-17`; `Core/Registry/DaorisLock.cs:39-43`, `:185-195`; `Service/Domain/LayoutFacts.cs:197` | a missing layout reads as `claude-code`'s, in four places | A |
| `Cli/platform/home.ts:147`, `:160-161`; `Core/Platform/DaorisHome.cs:368`, `:381-382` | the help rooms marked by `CLAUDE.md` | A |
| `.codex/config.toml:1-4`; `daoris.json:4`; `examples/engine/daoris.json:4` | the repository's own configuration: Codex given no knowledge server where `.mcp.json` gives Claude Code one; the doctrine on Claude Code's layout | A |
| `tools/merge-branch.mjs:221-229`; `tools/subagent-usage.mjs:3-15`, `:42`; `tools/dsh-probes/README.md:7-14` | development tools that read Claude Code's worktree lock and transcripts, and dsh's probes | E — development's own |

## 14. Records that name an agent or a door

| Record | Field and value | Travels | Written at |
|---|---|---|---|
| The session record (service) | `Adapter`, the door's id; `HarnessVersion`; `Profile`, the account's name | `Adapter` and `HarnessVersion` travel on the sync wire (`Core/Remote/SessionSync.cs:59`); `Profile` does not (`Core/Sessions/Sessions.cs:63`, `:80-99`) | `Loop/Driver.cs:2475-2476`; `Conversations/Chat/ChatRunner.cs:646-647`, `:882`; `Loop/DriverIntakes.cs:276`; `Conversations/Helpers/HelperDoors.cs:38`; `Conversations/Partners/PartnerDoors.cs:36` |
| The actor asked and ran | agent and model, this machine's alone (D205 point 10) | no | `Loop/Driver.cs:3677-3681`; `Conversations/Chat/ChatRunner.cs:1582` |
| `<session>.harness.json` | `adapter`, `conversation`, keyed by the adapter that named it | no | `Conversations/GoOn/GoOnFiles.cs:158-165`; `Conversations/GoOn/HarnessConversations.cs:31-37`; `Loop/Driver.cs:3724` |
| Ask Daoris's receipt `<id>.wired.json` | `harness`; `door` `pipe` or `acp`; `posture`; `permissionMode`; a skill `unverified` on the protocol door; a server `no-door` | no | `Help/HelpWired.cs:179`, `:182`, `:212`, `:233`, `:339`, `:350-352`, `:475-480` |
| A word's reach on a session event | `turn-end`, `next-step`, `now` | no | `Loop/Driver.cs:3739`, `:4389`; `Conversations/Chat/ChatRunner.cs:3396`, `:3422` |
| Note codes | `resumed.native`, `resumed.native-daoris`; a resume's reasons (`gone`, `exited`, `elsewhere`); a choice not offered; a refused model, each with the adapter or agent | with the record's note | `Platform/NoteCodes.cs:892`, `:894`; `Conversations/Domain/Continuation.cs:63-106`, `:157-158`, `:472-479`; `Loop/Driver.cs:2831`, `:3376-3380`, `:4439` |
| A session's end | `SessionEnded(… Adapter …)` | no | `Loop/DriverContinuations.cs:837`; `Loop/DriverIntakes.cs:386`, `:418` |
| The usage history | an entry's adapter; a turn's `TurnBinding(Agent, Account, Door, Meaning)`, `Door` the adapter's id | no | `Loop/Driver.cs:2772-2774`, `:3287`; `Conversations/Chat/ChatRunner.cs:1621`, `:1781-1782`; `Loop/DriverBrainstorms.cs:741` |
| The offers | per door's id | no | `Accounts/AgentOffers.cs:26-40` |
| The machine log | `adapter` on `agent.error`, `agent.transient`, `session.answered`, `conversation.unkept`, `session.quiet`; `agent` on `actor.*` and `partner.started` | no | `Loop/TransientRetries.cs:28-30`, `:53-56`; `Conversations/Domain/Continuation.cs:528-532`, `:579-580`; `Sessions/Watch/SessionWatch.cs:289`; `Loop/DriverPartners.cs:635-638` |
| An opinion's state | `adapter`, `maker`, `posture` (`rules-and-copy`, `copy-alone`) | no | `Review/Domain/OpinionAsk.cs:53-62` |
| A brainstorm's and a meeting's files | `adapter`, `maker`, `stubs` | no | `Brainstorms/BrainstormFiles.cs:219-220`; `Brainstorms/Domain/BrainstormDocument.cs:147`, `:329-330`; `Brainstorms/Domain/MeetingPacket.cs:110-111`, `:138-139` |
| A hand-over, a helper's, a partner's ask, a go-on mark | `agent` | no | `Conversations/HandOver/HandOverFiles.cs:27`, `:67`; `Conversations/HandOver/HandOverAskFiles.cs:77`; `Conversations/Helpers/HelperFiles.cs:84`, `:198`; `Conversations/Partners/PartnerFiles.cs:81`; `Conversations/GoOn/GoOnFiles.cs:32` |
| A trust hold | the trust file's path | no | `Loop/Driver.cs:2299`; `Loop/DriverIntakes.cs:270` |
| `driver.json` | `adapter`, `intakeAdapter`, `helperAdapter`, `commands` by id, `actors[].agent`, the *questions* pause's `claude-code` | no | `Platform/DriverConfig.cs:494-529`, `:1613-1637`, `:1735-1758`; `Sessions/Domain/AccessRules.cs:118`, `:127` |
| The pins and the account homes | versions by door's id (`harnesses.json`); accounts under `profiles/agents/<owner>/` | no | `Accounts/Harnesses/HarnessSettings.cs:685-718`; `Core/Platform/DaorisHome.cs:368` |

## 15. The count, and the worst tangles

**The count.** 350 sites (table rows; a row that groups copies of one branch counts once) in §1 to §13, 190 essential and
160 accidents of where the code was written, naming 234 files: 123 in the driver library, 14 in the modules
and the driver's host, 11 in the service, 53 in the page, 12 in the CLI, and 21 tools and
repository files. §14 lists sixteen records that name an agent or a door. Every count is this survey's reading at `f58f1be61`; a
reader who splits a grouped row counts more.

**The worst tangles**, each a place where one agent's fact or one door's test lives outside any provider and is decided more than once:

1. **One agent's wire read inline in the shared protocol door.** `AcpSession` and its helpers read Claude Code's adapter's extensions by
   name (`_claude/rateLimit`, `_claude/model`, `_claude/origin`, `claudeCode.promptQueueing`, `claudeCode.parentToolUseId`,
   `quota.model_usage`, the async-task and subagent frames, `jetbrains.air` sent to every agent: `Agents/Acp/Acp.cs:1714-1719`, `:1789`,
   `:2150`, `:3030`, `:3043`; `AcpWake.cs:219`, `:230`; `AcpBackground.cs:42-79`) and Codex's (`thread_active_writer`, `Acp.cs:1183`,
   `:3394`), and merges three agents' error shapes in one reader (`Acp.cs:175-239`). A new protocol agent with its own extensions has no
   place to put them but this class (§7, §8, §12).
2. **Claude Code's formats are the product's.** Its rule grammar (`Agents/Permissions/Permissions.cs:81-184`, the service's
   `Core/Help/RuleProposals.cs:54`, the page's rules), its settings file, hooks and plugin layout, its `.mcp.json` for every pipe agent
   (`SpawnServers.cs:5-53`), its MCP tool naming (`Permissions.cs:105-143`, `WhatItHas.tsx:100`), its trust file called from the loop and
   a route (`Loop/Driver.cs:2282-2303`, `DriverRoutes.cs:367-398`), its rooms' `CLAUDE.md` for every agent (`HelpRoom.cs:166-178`,
   `IntakeRoom.cs:67-84`), its settings schema and effort words (`Accounts/AgentSettings.cs:48-168`, `Actors.tsx:27`), its usage weights
   and floor (`UsageProjection.cs:121-122`, `SwitchDecision.cs:70-71`, `AgentsView.tsx:27`), and its `stream-json` read by four readers (the mapper,
   `StructuredOutput.cs:102-561`; its streams, `ClaudeStreams.cs:95-167`; the lineage, `ChildBindings.cs:293-323`; the ending's words,
   `Observation.cs:564-676`), the third wired to every pipe agent's stdout (`HarnessCapture.cs:123`). Codex, dsh and every plugin's harness are handed no rules, no skills and no lineage hook as a result
   (§8, §10).
3. **An agent is a door's id, and the agent is rebuilt everywhere.** Records, offers, pins, usage and the roles' choices hold door ids
   (§14); the owner is resolved by `AccountOf` in four driver copies with four rules (`HarnessRoster.cs:120-197`, `:782-841`,
   `:1694-1715`, `:2082-2098`), so Codex's limits and usage sit on its door and its sign-in on a holder no adapter is
   (`Adapters.cs:2122-2125`, `CodexAccounts.cs:30-53`); and the page rebuilds the agent from its doors (`Web/agents/tools.ts:226-316`) and
   acts through `doors[0]`, nine of its readers resolving door to agent again (§1, §3).
4. **The door decided by tests of `Wire` in the callers.** The machine's door is decided in five places by four rules
   (`Loop/Driver.cs:662-690`, `Modules/Bridge/DriverContext.cs:162-172`, `Host/SessionsConsole.cs:37-48`,
   `Host/DriverChoicesConsole.cs:56-71`, `Setup/SetupWorld.cs:20-31`) and threaded through the planner; *a pipe agent cannot drive an
   unadopted repository* is decided in five (§9); servers, rules and skills are handed by inline door tests in seven callers beside the
   helpers' own (§9, §10); and the page infers *can be stopped* and a 24,000-character cap from *structured* and *pipe* (§6).
5. **Resume, go-on and a failure written once per door and once per kind of session.** The protocol resume twice
   (`Loop/Driver.cs:4397-4441`, `ChatRunner.cs:3045-3173`), the pipe door's *retry once* three times, the accounts of a hand-over three
   times, the limit reader three times and the transient reader twice; and a failure carried in two channels, so the pipe door's limit
   cools no account and signs out none (`Loop/Driver.cs:3237`, `:3385`, `:3855`) (§5, §12).

Runners-up: a Claude Code seam (`CLAUDE_CODE_EXECUTABLE`, `DISABLE_UPDATES`) set on every protocol door's spawn
(`HarnessRoster.cs:1303-1308`, `HarnessSelection.cs:562-565`); declared fields nothing reads (`ProfileMustExist`; `WeekFixed` read only in
the modules); the stand-ins borrowing Claude Code's tables, so a rehearsal reads as Claude Code (`Adapters.cs:1709-1741`); the composed
instruction in every spawn's environment inside the adapter (`Adapters.cs:1540-1571`); and twin claims naming a CLI table that is gone
(§13).
