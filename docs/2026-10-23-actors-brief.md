# Actors: Daoris chooses the model for each kind of work it starts, from a range the person sets: the brief

> MODELROLE1, phase 1 of a co-design under D191 (2026-10-23). This brief is the one document every proposal is written
> from. It holds the direction, the facts of today by `file:line`, every place Daoris starts an agent, the constraints a
> proposal keeps, how other tools let people pick a model per task, and the questions every proposal answers. The
> proposals are in `docs/2026-10-23-actors-proposals.md`, one section per maker, each written from this brief alone and
> sealed before its author read another. A synthesis follows in a later quest. `Dnn` is `docs/decisions/Dnn.md`. Paths
> are under `src/`: `Driver/` is `Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is
> `Daoris.Desktop/Daoris.Desktop.Modules/`, `Core/` is `Daoris.Service/Daoris.Service.Core/`, `Mcp/` is
> `Daoris.Service/Daoris.Service.Mcp/`, `Web/` is `Daoris.Web/src/`. Read at `aad5dd409`; line numbers are that commit's.

## 1. The direction

The owner's direction, 2026-10-23, as the quest carries it: **for token use, Daoris should choose the model for each kind
of work it starts.**

- **Actors.** Every kind of agent Daoris starts gets a proper role, called an **actor**. The quest names a driven build,
  orientation, review and second opinion, a helper, a partner piece, intake, a brainstorm turn, a workflow node, a set-up
  and Ask Daoris, "and any other the code shows" (§4 lists what the code shows).
- **A range, not one model.** The person configures each actor as **a range of options in an order of preference**: an
  agent plus a model, plus an effort where the agent has one.
- **Ask Daoris can help configure it.**
- **It is a foundational change.** It touches every start, D24's rule and D98's split between Daoris and the tool.
- **The UI stays simple, never overcomplicated.**

The roles this development already dispatches by (D174, `docs/model-roles.md`) are where the idea came from. D174 point 4
says MODELROLE1 makes those roles "Daoris's own per-role setting for the sessions it drives, in D98's terms"; the
MODELROLE1 row (`TASKS.md:386`) used to wait on SUBLOAD1c's trial, and the owner's direction of 2026-10-23 lifted that wait.

**The part of the company it makes work** (D193): the staff (part 4) at a cost the owner chooses (part 1's budget). Each
seat in the company is filled by the right hand for the job, not by whoever the machine's one default happens to be.

## 2. The process

D191 governs it, with its 2026-10-20 amendment. Claude's proposal comes first, sealed. Codex's and Fable's follow, each
from this brief alone, then one synthesis, then Codex's read, and Fable's read because this changes a foundation: a
decided rule other designs rest on (D24, D98) and every start's protocol. The steward settles. D191 point 3 keeps one
thing for the owner: "anything that spends the owner's accounts or money beyond the development's own runs". So whatever
the default spends, the owner's yes is asked for it (§7, question 4).

## 3. Today, by `file:line`

### 3.1 Each agent runs one model and effort, and Daoris passes none

**No start names a model or an effort.** No path passes `--model`, `-c model_reasoning_effort`, `ANTHROPIC_MODEL` or any
model variable, and none calls `session/set_config_option` at start.

- `session/new` carries `{ cwd, mcpServers, _meta? }` and nothing else (`Driver/Agents/Acp/Acp.cs:918-923`). Its `_meta`
  holds only the rules file and the plugins (`Driver/Agents/Adapters/Adapters.cs:1794-1811`).
- Each adapter says so in its own comment: "No model is named (D24)" (`Adapters.cs:1870`, `:1962`, `:2074`).
- Preparing the spawn (`Driver/Loop/Driver.cs:2580-2613`, `HarnessProbe.Apply` in `HarnessSelection.cs:529-579`) sets
  the account's environment, the pinned binary, and the profile variable pointing at the chosen account's folder
  (`CLAUDE_CONFIG_DIR`, `CODEX_HOME`, `DSH_HOME`). None of these is a model.
- The only thing set after `session/new` is the posture, through `session/set_mode` (`Acp.cs:2112-2166`).
- The arguments per adapter name no model either:
  - Claude Code's pipe door: `-p … --permission-mode …` and the stream flags (`Adapters.cs:2088-2096`; chat `:2172-2184`).
  - `claude-agent-acp`: the bare binary (`:1771-1775`).
  - dsh: `--profile acp` (`:1904-1917`).
  - `codex-acp`: the bare binary (`:1994-1998`).
- A person's `driver.json` `commands[adapter]` replaces the command whole (`Driver.cs:2583`, `ChatRunner.cs:1084`). That
  is the only way an extra flag reaches a start, and Daoris adds none itself.

**So the model is whatever the account's own configuration says.**

- **Claude Code** (both doors) reads `settings.json` in the account's folder: `model`, `effortLevel` and
  `modelSettings.<model>.effortLevel` (`Driver/Accounts/AgentSettings.cs:21-51`). `claude-code-acp` shares
  `claude-code`'s accounts (`Adapters.cs:1842`).
- **Codex and dsh** read their own configuration in `CODEX_HOME` / `DSH_HOME`, which Daoris never writes.

### 3.2 The one standing setting: an account's model and effort (D98)

D98 split the choice into two levels, each in the tool's own terms.

**An account's defaults.**

- *What is written.* `AgentSettings` writes the account folder's `settings.json` (`AgentSettings.cs:96-168`). Its
  writer `AccountSettings.Write` (`:228-263`) serves the window, Ask Daoris and the terminal alike.
- *The choices.* The aliases offered are the Agent SDK's list (`:58-59`): `default, sonnet, opus, haiku, fable, best,
  sonnet[1m], opus[1m], fable[1m], opusplan`, plus a free field. The efforts are `low, medium, high, xhigh` (`:65`). `max`
  is refused as a default because the tool never keeps it (`:185-199`).
- *Which agents.* Only Claude Code declares a settings file (`Adapters.cs:2288-2290`). Codex and dsh are refused with
  "set its model with the tool itself" (`AgentSettings.cs:242-245`), so no Codex model can be set from Daoris today.
- *What can outrank it.* A repository's own `.claude/settings.json` and `ANTHROPIC_MODEL` both outrank it. The surface
  says so (`Driver/Agents/AgentsCommand.cs:822`; `Web/locales/en/harness.json:72`).

The doors to it:

| Door | Where |
|---|---|
| Window | Agents → the agent's page → *Model and effort* fold, and *Model and effort…* in each account's ⋯ menu (`Web/agents/AgentPage.tsx:181`, `:380-381`, `:772-803`). The form is `Web/settings/accounts/AccountSettings.tsx:49` (model `:94-111`, effort `:115-120`, effort per model `:125-134`) |
| Terminal | `daoris agent settings <agent> [--account A] [model M] [effort E [--for M]]` (`Daoris.Cli/src/cli/agent.ts:81-85`), handed to `daoris-driver agent settings` (`AgentsCommand.cs:771-895`), route `SET_AGENT_SETTINGS` (`Modules/Bridge/Routes/AgentsRoutes.cs:513-518`). The CLI twin `agentsettings.ts` is gone (PUSH1f2d): `AgentSettings.cs` is the one writer (`:32-33`) |
| Ask Daoris | `agent_settings_propose` (`Mcp/HelpAccountTool.cs:10-31`), applied by `HelpAccountProposals.cs:10-20` |
| Words | en `Web/locales/en/harness.json:58-81` (`harness.settings.open` *Model & effort*), `agents.json:100` (`agents.section.settings`); zh the same keys in `locales/zh/` |

**One conversation's model and effort.**

- *The control.* The person changes a model or an effort beside the composer (`Web/work/conversation/SessionOptions.tsx:22-31`,
  rendered at `Composer.tsx:712`).
- *The wire.* `SESSION_OPTIONS`, `SESSION_OPTIONS_CHANGED` and `SET_SESSION_OPTION` (`Web/bridge/sessions/conversation.ts:251-282`;
  `Modules/Bridge/Routes/ConversationRoutes.cs:385-404`).
- *The driver's side.* `ChatRunner.SetOptionAsync` (`Driver/Conversations/Chat/ChatRunner.cs:1699-1721`) sends
  `session/set_config_option` (`Acp.cs:527-539`). It allows only the `model` and `thought_level` categories
  (`AcpConfigOption.ThePersons`, `Acp.cs:82`).
- *Where it works.* Only on a protocol-door **conversation** (a chat or Ask Daoris): only `ChatRunner.cs:2189` passes the
  options callback. A driven session's `AcpSession` is opened with none (`Driver.cs:3775`), so a driven, intake, review,
  helper, partner or brainstorm session offers no options.
- *What does not carry.* A conversation that goes on after it ended does not re-apply an earlier choice: no other caller
  of `SetConfigOptionAsync` exists.

### 3.3 D24, D98 and the role reference

- **D24: the model is a deployment choice.** A feature is specified without naming a model; the provider is chosen by
  where the deployment runs, at the composition root. A feature delivers what it can without a model and **reports which
  tier ran**.
- **D98's reading of D24 for agents:** "Daoris names no model and chooses none. Every alias, effort and option is the
  tool's own word … and a person picks one. *The deployment chooses*, and for an agent a person runs, the deployment is
  the person."
  - D98 rejected a model list Daoris maintains (D57's registry), and `ANTHROPIC_MODEL` at spawn, because "it would outrank
    the person's own settings … and it would be Daoris's setting rather than the tool's".
  - D98 never writes the tool's own configuration home.
- **The session record keeps no model.** `Session.Adapter` is "a name for the record, never a model (D24)"
  (`Core/Sessions/Sessions.cs:63`). The record does keep the harness's version and the account (`Profile`) (`:80-88`, `:101-117`).
- **D174 and `docs/model-roles.md`.** This development dispatches by role, not by model name:

  | Role | The work | Current (reviewed 2026-10-16) |
  |---|---|---|
  | steward | plans, reviews, merges, keeps the records | Claude Opus 5.5, high |
  | orient | reads and locates, hands back a pack of exact ranges | Claude Sonnet 5.5, default |
  | build-exact | one lane, the pack names the change | Claude Sonnet 5.5, default |
  | build-judgement | design, UI, more than one lane, deletion or concurrency | Claude Opus 5.5, high |
  | review | reads a finished branch or a design, read-only | another maker, else Claude Opus 5.5, high |
  | decide-support | a different opinion on a decision or a choice of shapes | another maker, high |
  | help | Ask Daoris's room | the person's choice (D89, D98) |

  Candidates on trial for a role sit beside it (Haiku for orient, Fable and Sonnet for build-judgement).

  The reference is living. A new version enters on trial for a role, is measured (`tools/subagent-usage.mjs`, D160's
  targets) and is promoted or retired with the date. D174 point 4 says Daoris takes the same logic through MODELROLE1, and
  that its reference "starts from this one and is measured the same way". D174 also rejected choosing "by price or by
  benchmark alone".

### 3.4 Which agent and which account: the pool and rotation (D197)

**One set per agent.**

- *Where it lives.* The accounts Daoris holds are one set for every workspace, kept in `harnesses.json`:
  `rotation[agent]` is the whole order, and `rotationUse[agent]` holds its `use` (goal, order), `keep`, `switch` and
  `curve` (`Driver/Accounts/Harnesses/HarnessSettings.cs:85`, `:137`, `:156-158`).
- *Who reads it.* Every reader goes through one projection, `AccountSet.Of` (`AccountSet.cs:29`).
- *What is outside it.* The person's own sign-in is outside the set and never used for driven work (D197 point 3).
- *The machine's agent.* `driver.json`'s `adapter`, `claude-code` by default (`Driver/Platform/DriverConfig.cs:72`,
  `:986`, `:1433`).

**A start picks an account by walking the set.**

- *The entry point.* `HarnessRoster.SelectAsync` (`HarnessRoster.cs:883-886`), with `SelectHandedAsync` (`:895`),
  `ResumeAsync` (`:924`) and `CarriedOnAsync` (`:991`) beside it.
- *The walk* (`WalkAsync`, `:1047-1099`):
  - it takes the agent's set (`:1064`) in `AccountRotation.Order` (`AccountRotation.cs:311-336`);
  - it holds cooling and refused accounts first, before any probe (`:1094-1099`);
  - it refreshes usage readings where the set has more than one account (`:1076-1079`);
  - driven work never takes an account kept for conversations (`StartKind`).
- *A limit is a cool-off.* `HarnessRoster.Limited` (`:253-263`) reads `AccountLimits.Read`, whose `DefaultCoolOff` is
  60 minutes (`Driver/Accounts/Usage/AccountLimits.cs:79`, `:107-119`). The cool-off is stored by `AccountCooling.Cool`
  (`AccountCooling.cs:84`), and `driver.json`'s `cooloff` overrides its length (`DriverConfig.cs:458-472`).
- *What is logged.* `account.limited` and `account.rotated` (`AccountCooling.cs:320-334`, `:380-383`; `RotatedStart` at
  `AccountRotation.cs:121-137`).

**Rotation moves between accounts of one agent, never between agents.** Changing agent is a hand-over (D163).

### 3.5 Hand-over and mixed makers (D163, D189)

- **A hand-over is a carry-on with its agent named.** A session goes on in a new session on another agent, in the same
  tree, handed Daoris's brief of it (D163 point 1). Daoris offers one when the account is near its limit, or cut by one
  (point 3).
- **The person's rule decides.** `handOver` (`ask` by default, `on`, `off`) says what an agent's own ask for a hand-over
  does (point 3).
- **The quest's door.** `DriverHandOvers.TakeHandOverAsync` (`DriverHandOvers.cs:77`) writes `HandedQuests` (`:133`). The
  next start then reads `handedQuest?.Agent ?? config.Adapter` (`Driver.cs:1672`), with the named account first
  (`HarnessRoster.cs:895-905`).
- **A conversation's door.** `ChatHandOver.HandOverAsync` (`ChatHandOver.cs:45`) starts the new conversation on the
  requested agent and account (`:156-157`).
- **Mixed makers on one line.** One entry's parts may run on different makers' agents (D167, D189); each part's record
  names its own `Adapter` and `Profile`.

### 3.6 Second opinions (D155) and helpers (D163)

**A second opinion.**

- *Its rule.* `opinions` in `driver.json` (a repository's rule, or the workspace's), with `reviewers`: "adapters in the
  person's order, named and never guessed" (D155 point 3).
- *Who reads.* The walk tries every entry of another maker's family first, then a maker not declared, then the same agent
  "fresh", only where the person listed it (`Driver/Review/Domain/ReviewerChoice.cs:175-237`; D155 point 4). The account
  is chosen by the unchanged account walk (`ReviewerChoice.cs:327`).
- *Independence is by maker, not by model.* D155 point 4: "Daoris names no model (D24), and says so."
- **`reviewers` is already an ordered range of agents, with no model in it.**

**A helper.**

- *Who answers.* The agent the ask names, if the rule's list allows it; else the rule's list; else every agent of another
  maker (`Driver/Conversations/Helpers/HelperChoice.cs:15-37`).
- *The rule.* `helpers` (`ask` | `on` | `off`, with its agents listed) decides what an agent's ask does.

### 3.7 Partners (D199)

- *Who writes.* A partner must be named by the ask, and is refused unless a measured row of its whole effective
  configuration admits it (`DriverPartners.cs:500-507`; `PartnerChoice.cs:17-31`).
- *The rule.* `partners` (`ask` | `on` | `off`, with its agents).
- *What the measured row covers* (D199 point 4): "versions, posture, writable roots, network and loopback, the operating
  system, the settings' digest". A model is not among them, but the settings' digest is.
- *The capability sheet* `AgentCapabilities` is designed (`docs/2026-10-22-coagent-design.md:293-330`) but not built
  (COAGENT1e), and it has no model or effort row.

### 3.8 The jobs Daoris's AI already names an agent for

Settings → *AI features* (`Web/settings/AiDomain.tsx:27`; `settings.domain.ai`, *AI features* / *AI 功能*) already holds
two per-job agent choices. They are the nearest thing to an actor today:

- **Intake**: `driver.json`'s `intakeAdapter` (`DriverConfig.cs`; read at `Driver/Loop/DriverIntakes.cs:111`). Its words
  are `settings.ai.intake.*` (`Web/locales/en/settings.ai.json:27-37`): "Every intake spends a real account."
- **Ask Daoris**: `driver.json`'s `helperAdapter` (`DriverConfig.cs:434`; `Modules/Bridge/Routes/HelpRoutes.cs:52`). Its words
  are `settings.ai.helper.*` (`:7-16`).

Both name an agent and no model. Both have a terminal twin: `daoris driver intake <agent>|off` and
`daoris driver helper <agent>|off`.

### 3.9 What the usage reads tell per model

| What is read | Where | Per model? |
|---|---|---|
| A turn's tokens: input, cache read, cache write, output, and the context high-water mark, as `turn.ended` | `Driver/Sessions/Events/SessionLog.cs:43-47`, `:359-376` | No |
| A session's totals, `SessionSpent` | `Driver/Sessions/Domain/SessionEvents.cs:156-159`; `SESSION_SPENT` (`Modules/Bridge/Routes/SessionsRoutes.cs:571-586`); shown in the parts strip and context ring (`Web/work/head/PartsStrip.tsx:18`, `Web/work/conversation/ContextRing.tsx:21`), naming no model and claiming no price | No |
| **The model a turn's tokens went to**: `TurnModels(Model, Split, Whole, From)`, as its door said it (`_meta.quota.model_usage` on ACP, `message.model` and `modelUsage` on the pipe) | `Driver/Accounts/Usage/TurnModels.cs:9-32`; the sources at `Acp.cs:1384-1385` and `Driver/Agents/Streams/StructuredOutput.cs:212`, `:332`; the turn line's `model` (a name, `mixed` or `unknown`) and `split[]` in the usage history (`UsageHistory.cs:606-640`) | **Yes**, in the machine-local usage history only: "never written to the session's record, which keeps no model's name" (`TurnModels.cs:10`) |
| An account's windows (how much is left, when it resets): Claude's `get_usage` control request, Codex's `app-server` rate limits | `ClaudeUsage.cs:6-40`; `CodexUsage.cs:114-118`; `HarnessRoster.cs:2034-2107` | No: per account |
| Usage history over 90 days | `UsageHistory.cs:128-180`; shown on the agent page's Usage fold (`Web/agents/AgentPage.tsx:814`, `Web/settings/accounts/UsageCurve.tsx`) | Per account; the per-turn model is in the file, but no screen reads it |
| A price | nowhere (D57; `ClaudeUsage.cs:16-17`) | — |
| Cost per row on the rail (MIXAGENT3a-iii) | not built (`TASKS.md:103`) | — |

**So Daoris already learns which model each turn ran on, after the fact, and shows it nowhere.** The model that runs is
the account's choice, and Daoris records it only in the usage history.

## 4. Every place Daoris starts an agent, and what each does

All of them prepare the spawn the same way (`Driver.Prepare`, `Driver.cs:2580-2613`) and start through
`AgentStarts.StartAsync` (`Driver.cs:3161`, spawn at `:3180`). **None passes a model or an effort.**

| # | Start | What it does | Decided at | Agent chosen by | Account |
|---|---|---|---|---|---|
| 1 | **Driven build** | A quest's session: builds in its tree, commits, closes. Also every workflow *work* and *look* node, which run as quests (`Driver/Workflows/Domain/WorkflowGraphRun.cs:314-316`) | `Driver.RunAsync` (`Driver.cs:1661`; agent at `:1671-1672`) | `handedQuest?.Agent ?? config.Adapter` | `SelectHandedAsync(…, StartKind.Driven)` (`:1733-1735`) |
| 2 | **Its retries, carry-ons and words to a parked or ended session** | The same work going on: resumed where its conversation holds, else a new part | `Driver.cs:1720-1728`, `DriverContinuations.ContinueAsync` (`DriverContinuations.cs:109`) | The record's agent | `ResumeAsync` on the record's account, else the walk (`CarriedOnAsync`, `Driver.cs:2056-2057`) |
| 3 | **A quest's hand-over** (D163) | The same work on another agent, briefed | `DriverHandOvers.cs:77`, then start 1 | The named agent | The named account, else the walk within that agent |
| 4 | **A conversation's hand-over** (D163), and the usage curve's move | A chat going on on another agent or account | `ChatHandOver.cs:45` | The requested agent | `SelectAsync(chosen: account, Conversation)` (`ChatRunner.cs:424-426`) |
| 5 | **Intake** | Reads an ask the declarations left, publishes quests, asks where it cannot tell | `DriverIntakes.RunIntakeAsync` (`DriverIntakes.cs:104`) | `config.IntakeAdapter` (`:111`) | `SelectAsync(…, Driven)` (`:124`) |
| 6 | **A second opinion** (D155), its recheck, and a workflow *opinion* node | Reads a candidate in a clone of its own, gives findings with `opinion_give` | `OpinionGateLook.cs:372`, recheck `OpinionDeliverer.cs:212-225`; pass `DriverOpinions.cs:34` | The walk over `reviewers` (`ReviewerChoice.cs:175-237`) | The account walk per entry (`ReviewerChoice.cs:327`) |
| 7 | **A helper** (D163) | Answers one question, read-only, in a copy carrying the work in progress | `DriverHelpers.cs:574`; pass `:609`, `:818` | `HelperChoice.Candidates` | As 6 |
| 8 | **A partner piece** (D199) | Writes one piece in a clone of its own, contained by the launcher | `DriverPartners.cs:168`, `:290`; pass `:319`, `:482` (launcher `PieceLauncher.cs:129`) | Named by the ask, admitted only by a measured row (`:500-507`) | As 6 |
| 9 | **A brainstorm turn**: generators, then a synthesizer | Each agent proposes, and the first that answered synthesizes | `DriverBrainstorms.Generators` (`DriverBrainstorms.cs:345-358`); synthesizer `:418`; turn `:641` | The request's agents, else the policy's, else `config.Adapter` (`:348`) | `SelectHandedAsync(turn.Agent, …, Driven)` (`:660-661`); a limit restarts once on the next account (`:603-609`) |
| 10 | **A repository conversation** (chat; *start with words*, *start from these words*) | The person's own talk with an agent in a repository | `ConversationRoutes.cs:40-77`; `ChatRunner.StartAsync` (`ChatRunner.cs:390`), `:276`, `:244` | The request's agent, else `config.Adapter` (`ConversationRoutes.cs:48`) | The person's pick as `chosen`, else the walk (`ChatRunner.cs:424-426`) |
| 11 | **Ask Daoris** | A conversation about Daoris itself, in a room of its own; proposes, or acts under full access (D183) | `HelpRoutes.cs:46-89`; `ChatRunner.cs:588`, `:635` | `config.HelperAdapter` (`HelpRoutes.cs:52`) | `SelectAsync(…, Conversation)` (`ChatRunner.cs:606`) |
| 12 | **A conversation going on after it ended** (chat or Ask Daoris) | The same conversation resumed | `ChatGoOn.cs:307`; account `:417`, `:499` | The record's agent | `ResumeAsync(…, record.Profile, walks: false)` |
| 13 | **A new part briefed from an earlier conversation** (D189) | A fresh conversation handed a brief composed without a model | `BriefedMove.cs:108-133` | `record.Adapter ?? config.Adapter` (`:111`) | The record's account, or the help room's walk |

**Spawned, but not as a working agent:** the usage readers (`ClaudeUsage.cs:38`, `CodexUsage.cs:115`), and the login,
install, update and version probes (`HarnessActions.cs:502-546`). None is an actor.

**Named by the quest but not found as a start of its own:**

- **Orientation.** No scout or pack start exists in the driver. MODELROLE1's row wants "a work's orientation handed in as
  a pack from the knowledge store rather than read afresh by the building session". Today the build session orients itself.
- **A workflow node.** It is not a start of its own. A node runs as a quest (start 1) or an opinion (start 6)
  (`WorkflowGraphRun.cs:314-316`), and `WorkPerformer` talks to plugin processes, not agents.
- **A set-up.** `SetupWorld` runs only `node` and `daoris --version` (`SetupWorld.cs:79`). It publishes asks
  (`:73-74`), which go through intake (start 5) and quests (start 1).
- **A meeting** (D177). No real agent takes a turn yet. The turns runner is `MeetingTurns ?? Unavailable`
  (`DriverMeetings.cs:26-27`, `:249`).
- **Knowledge drafting and convergence.** Nothing is spawned for them. The embedder is the service's environment
  (`settings.ai.search.hint`), outside this question.
- **A title or a commit message.** Nothing is spawned. The short title is judged from what the publishing agent sends
  (`QuestExchange.cs:606`).

## 5. The constraints a proposal keeps

Each one is decided and stands, unless a proposal argues to amend it. A proposal that amends one says which and why, and
the amendment is appended to the decision, never rewritten into it.

- **D24: no feature is welded to a model.**
  - The words a person sees and the code's rules name no model; models come from the tool's own vocabulary and the
    person's choice.
  - A feature works without a model and **says the tier that ran**.
  - The direction asks Daoris to *choose* per kind of work. A proposal says how that choice stays the person's
    deployment choice, and therefore how D98's "Daoris names no model and chooses none" is amended, if it is.
- **D98: the tool's own terms, and the tool's home never written.**
  - Every model, alias and effort offered is the tool's own word: its SDK's list, its protocol's offered options, or a
    free field.
  - No list Daoris maintains (D57's rejected registry).
  - No `ANTHROPIC_MODEL` at spawn.
  - `max` is never written as a default.
  - A tool whose settings Daoris has not read is offered nothing it cannot honour, and the surface says so.
- **D197: one set of accounts per agent.**
  - The order and the use settings stay as they are; the person's own sign-in stays outside.
  - An actor chooses an agent, a model and an effort. **Which account** stays the walk's (cool-offs, sign-ins, `keep`),
    and a proposal says how a range and the walk compose when an account cools.
- **D201: containment.** Every agent Daoris starts is contained from its first instruction through the launcher, and the
  standing confirmation's cover depends on it. Choosing another agent or model for a start never makes an uncontained
  start, and a start the launcher cannot contain is still logged `agent.contained: false`.
- **D199: admission.** A partner runs only where a measured row admits its whole effective configuration. If a model
  changes that configuration (the settings' digest), the proposal says how admission reads it.
- **D155: independence by maker.** A second opinion is independent by maker, not by model; the same agent on another model
  is still the same family. The person's `reviewers` order and the same-agent fallback's label stand.
- **D163: hand-over.** Changing agent mid-work is a hand-over, by the person's `handOver` rule (`ask` by default). An
  actor's range never moves a running session to another agent silently.
- **Nothing a person never chose runs silently** (the quest's constraint).
  - A model or an agent the person did not put in a range, or accept as a default, is never used without saying so.
  - A fallback down the range is said where it happens.
  - The account's own model (today's behaviour) counts as chosen only where the person sees it as a choice.
- **The tier that ran is said** (D24). Each session says which actor, agent, model and effort actually ran, as the agent
  reported it, not only as asked. Where the agent did not report its model, it says *not reported*.
- **Machine-local.**
  - Accounts, ranges and usage are this machine's (AGENTS.md; D47 §4).
  - A shared host carries no range and no model.
  - Today the session record keeps no model's name (`TurnModels.cs:10`, TOOL3); a proposal that records one says why
    that does not cross a sync.
- **Every door** (D50): the window, the terminal and Ask Daoris (D110, D183) each reach what the setting does.
- **Names** (D116; `docs/2026-10-01-naming-design.md`).
  - Every chrome word comes in English and Chinese, within its kind's budget (`button` 20/8, `status` 16/5,
    `section` 32/12, `tab` 10/4).
  - A new term enters through the glossary (`Web/locales/glossary.json`) with its reasons.
  - D193 rejected "a new vocabulary on the page". *actor* is the owner's word and is not yet a term. *角色* is used
    nowhere in the Chinese chrome today.
- **Simple.** The owner's word: "the UI must stay simple, never overcomplicated". A person who sets nothing gets work done
  as today.
- **The owner's money** (D191 point 3). A default that spends more, or on another account, is the owner's to accept.

## 6. How other tools let people pick a model per task

Read from each tool's documentation on 2026-10-23. Where a source was a forum, or two sources disagreed, the row says so.

| Tool | The unit of choice | One model or an ordered list | With no setup | Where it lives | Cost shown | What keeps it simple |
|---|---|---|---|---|---|---|
| **Cursor** | One picker. Subagents take `model: inherit \| <id>`. A forum request asks for a default per mode, so per-mode models seem absent (forum) | One. *Auto* routes over a pool; a subagent's blocked model is quietly substituted | *Auto* (the router), in its *Balance* intent | The picker; subagent frontmatter, which also takes effort | Billed at the routed model's list price | Auto offers three intents (Cost, Balance, Intelligence), not models; subagents inherit |
| **GitHub Copilot** | Chat picker per conversation; cloud-agent picker per task; custom agents (`.agent.md` `model:`); quiet settings for utility jobs (`chat.utilityModel` for titles and commit text, `inlineChat.defaultModel`) | `.agent.md` `model:` may be **an array tried in order until one is available**; elsewhere one, or Auto | *Auto* (Efficiency, Balance, Intelligence) | The chat dropdown; *Manage Models* chooses which appear | Token-based credits since 2026-06; Auto discounted; a hover says which model answered | An intent, not a model; the person picks which models show; background jobs kept off the picker |
| **Claude Code** | Session model (`/model`); subagent `model:` and `effort:` frontmatter; `opusplan` (Opus plans, Sonnet acts) | `fallbackModel` / `--fallback-model` is **an ordered list**, used only when the main model is overloaded, for that turn | `default` (the plan's best); effort a slider | `/model` (save, or this session only), `--model`, settings `model`, `modelSettings.<alias>.effortLevel`; subagent order: the call's value, then frontmatter (`inherit`), then `CLAUDE_CODE_SUBAGENT_MODEL`, then the main model | Not on these pages | Aliases never go stale (admins pin what each points to); a subagent inherits unless set |
| **OpenAI Codex CLI** | A profile (`--profile`), layered over the base config; `review_model`; `plan_mode_reasoning_effort`; spawned agents' `agents.default_subagent_model` and effort | One per layer; no fallback list found | The account's available models; no documented default found | `config.toml` (profiles as `[profiles.<name>]` or separate files: the sources disagree) | Not covered | A profile holds only what differs; review and plan get small targeted overrides |
| **Zed** | Per feature: `agent.default_model`, `inline_assistant_model`, `commit_message_model`, `thread_summary_model`, `subagent_model`, each provider, model and optionally effort | One per feature; an unset feature uses the default; a subagent's missing model falls back to its parent's | What first-run set up | The agent panel's selector, favourites cycled; `settings.json` | The thread's token count beside the selector | One default plus optional overrides; favourites |
| **Continue** | Roles on each model entry: `chat`, `autocomplete`, `edit`, `apply`, `embed`, `rerank` | One model may hold several roles; no fallback described | Omitted roles mean chat, edit, apply and summarize | `config.yaml` | Not covered | Roles are tags on a model, not separate settings |
| **Aider** | Main (architect), editor and weak models (weak for commit messages and summaries) | One per role | Each main model brings its own editor and weak pair | Flags and model settings | Says architect mode costs more | Choosing the main model chooses the helpers |
| **Cline** | Plan mode and Act mode | One per mode | One model for both until *Use different models for Plan and Act* is ticked (our reading of its docs) | A settings toggle | Not covered | One toggle |
| **Roo Code** | An API configuration profile (provider, model, thinking budget, rate limit) linked to a mode; a task keeps the profile it started with | One profile per mode or task | One profile | Settings → Providers; a dropdown in the chat | Not covered | A named bundle; sticky per mode and per task |
| **JetBrains Junie** | One main model; a hidden helper model does summaries and routing | One | *Default*, described as the best price and quality and updated as models ship | Settings → Tools → Junie → Models | Not covered | Helper work is not a setting at all |
| **OpenRouter** | A request (`models: [...]`, or `openrouter/auto`) | **An ordered array**, moving on for context length, moderation, rate limits or downtime | — | The request | Billed at the model that answered; the response's `model` names it | One ordered list; every answer says who answered |
| **LiteLLM** | A model group (an alias over deployments) | **Ordered lists per error kind** (`fallbacks`, `context_window_fallbacks`, `content_policy_fallbacks`) | `simple-shuffle` routing | `router_settings` | A cost-based routing strategy | Callers ask for a group; the chains are configured once |

Sources: cursor.com/docs/models, cursor.com/help/models-and-usage/cursor-router, cursor.com/docs/agent/subagents,
forum.cursor.com/t/default-model-selection-per-mode-agent-ask-plan-debug/150676;
docs.github.com/en/copilot/concepts/auto-model-selection,
docs.github.com/en/copilot/how-tos/use-copilot-agents/cloud-agent/changing-the-ai-model,
github.blog/changelog/2026/04/27/github-copilot-is-moving-to-usage-based-billing,
code.visualstudio.com/docs/copilot/language-models, code.visualstudio.com/docs/agents/reference/custom-agent-file-reference;
code.claude.com/docs/en/model-config, code.claude.com/docs/en/sub-agents;
learn.chatgpt.com/docs/config-file/config-reference, …/config-advanced, …/config-basic; zed.dev/docs/ai/agent-settings,
zed.dev/docs/ai/agent-panel; docs.continue.dev/reference; aider.chat/docs/usage/modes.html,
aider.chat/docs/config/adv-model-settings.html; docs.cline.bot/features/plan-and-act;
roocodeinc.github.io/Roo-Code/features/api-configuration-profiles; junie.jetbrains.com/docs/junie-plugin-models.html;
openrouter.ai/docs/guides/routing/model-fallbacks; docs.litellm.ai/docs/proxy/reliability, docs.litellm.ai/docs/routing.

**What keeps them simple:**

1. **One default, and an override only where it differs.** Unset means inherit: Zed's features, Claude Code's and Cursor's
   subagents, Codex's profiles, Continue's role defaults and Aider's pairs all work this way. Nobody configures every kind
   of task.
2. **An intent rather than a model where the tool can route.** Cursor and Copilot offer three intents. Junie offers a
   *Default* that changes as models ship. Claude Code's aliases name a tier that stays right when a version moves.
3. **The ordered list is for availability, and kept apart from the choice.** Claude Code's `fallbackModel` (for the turn
   only), Copilot's `.agent.md` arrays, OpenRouter and LiteLLM move down the list when the first cannot answer. The good
   ones say which model actually answered, and bill at that model's price.
4. **Background jobs get a quiet setting of their own.** Commit messages, titles and summaries go to a cheap model (Zed,
   VS Code's utility model, Aider's weak model, Junie's hidden helper), which keeps the visible picker to one or two choices.

## 7. The questions every proposal answers

A proposal answers each one, and says where it amends a decision.

1. **The actors.**
   - The list, with each actor's name in English and Chinese against the glossary and the budgets. A name for *actor*
     itself (*角色*, or better).
   - Which start of §4 each actor covers.
   - What becomes of the named kinds §4 does not find as starts: orientation, a workflow node, a set-up, a meeting.
   - Whether a person's own conversation (start 10) is an actor.
2. **The range.**
   - What an option is: an agent, a model in the tool's own words, and an effort where the agent has one. How a Codex or
     dsh model is honoured when Daoris writes neither tool's configuration.
   - How a start hands a model over: the protocol's `session/set_config_option` after `session/new`, a flag on the pipe
     door, or the account's settings. Why that way, given D98's cascade (`ANTHROPIC_MODEL` and a repository's own
     `.claude/settings.json` outranking the account).
   - What happens when the agent does not offer the option named.
3. **Fallback when an account cools.**
   - How the range and D197's account walk compose: all of an option's accounts before the next option, or the next
     option first.
   - What happens mid-session when an account cools (D163's hand-over and its rule).
   - What happens when every option is cooling or refused: the wait the person sees.
   - D155's independence in the second opinion's range.
4. **Defaults that need no setup.**
   - What a person who sets nothing gets on the first start after the upgrade. Is it exactly today's behaviour, or a
     suggested range, and if suggested, who accepts it (D191 point 3) and in which words?
   - How a default names a model without Daoris keeping a model list (D57, D98).
   - How a default follows a new model version (D174's trial and promotion).
5. **Where it lives.**
   - In Settings: which domain, and the layout of one screen.
   - On the session: what its head says about the actor and what ran.
   - On a start: whether the person's pick of agent and account at a start (D130 point 4) overrides the actor.
   - On a quest, a workflow node or an ask: whether any of them can name an actor or an option.
   - Machine-wide, or per workspace or repository.
6. **Ask Daoris.**
   - What it may set: one actor's range, all of them, a suggested set.
   - How it is confirmed: the card, the Apply, and what changes under full access (D183).
   - Which tool carries it (beside `agent_settings_propose`).
7. **Migration from today.**
   - What becomes of the account's `settings.json` model and effort (D98's first level).
   - What becomes of `intakeAdapter`, `helperAdapter`, `opinions.reviewers`, the `helpers` and `partners` agent lists,
     and the brainstorm policy's agents.
   - What becomes of the composer's per-conversation picker.
   - What a reader of an old `driver.json` sees.
8. **Cost shown.**
   - What the person sees per actor and per session, from what Daoris already reads (§3.9: tokens per turn, the model per
     turn in the usage history, the account's windows), with no price (D57).
   - Whether `TurnModels` reaches the session record or the screen, and what D24 and TOOL3 then say.
9. **Tests and proof.** The unit tests per rule, the census and stub tests, the installed-window proof, and what is
   measured on the install before a default changes.
10. **The rows**, in build order, each with its proof.
