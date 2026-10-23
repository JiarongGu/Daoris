# Actors: Daoris chooses the model for each kind of work it starts, from the choices the person put in order (MODELROLE1, proposed, round 1, 2026-10-23)

**Standing:** proposed with D205, round 1: the synthesis of the three independent proposals in
`docs/2026-10-23-actors-proposals.md` (Claude's, Codex's, and an independent Opus reader's in Fable's place under D191's
amendment of 2026-10-23), written from the brief `docs/2026-10-23-actors-brief.md`. Each difference is settled in §1 by
choosing one answer, or composing two, and saying what the others lose. It changes a foundation (D24's and D98's rule, and
every start's protocol), so under D191 Codex reads it and an independent Opus reader reads it as a foundation; D205 stays
proposed until both have read it and the steward accepts. Nothing here is built, spends an account, signs in or publishes.

Read for it: the brief and the three proposals whole; D24, D98, D155, D163, D174, D183, D191, D197, D199 and D201 at the points
this cites; `docs/model-roles.md`; `.claude/knowledge/model-decoupling.md`, `session-record-fields.md` and
`translation-parity.md`; the glossary. Every claim about the code was checked at this tree (`656acf6de`, `aad5dd409` with the
proposals merged); §0.2 lists where the brief or a proposal was wrong. `Dnn` is `docs/decisions/Dnn.md`; paths use the brief's
abbreviations (`Driver/`, `Modules/`, `Core/`, `Mcp/`, `Web/`).

## 0. What this rests on

### 0.1 The direction

The owner's direction of 2026-10-23, as the brief §1 carries it: for token use, Daoris chooses the model for each kind of work
it starts. Every kind of agent Daoris starts is an **actor**; the person configures each as **a range of agent plus model (and
effort), in order of preference**; **Ask Daoris helps** configure it; it is **a foundational change**; the **UI stays simple**.
And, through D191's amendment and `docs/model-roles.md`: Opus 5.5 does this development's reasoning work for now. That last
line governs who writes and reads this design and seeds the suggestion of §6.2; it does not make Opus a default anyone gets
without a press.

### 0.2 Corrections to the brief and the proposals, checked

| Claim | At this tree |
|---|---|
| Brief §3.1: the adapters' "No model is named (D24)" at `Adapters.cs:1870`, `:1962`, `:2074` | At `:1733` (`claude-agent-acp`), `:1870` (dsh), `:1962` (`codex-acp`) and `:2164` (the pipe door, "here either"); none at `:2074` |
| Brief §3.4: `WalkAsync` at `HarnessRoster.cs:1047-1099` | It begins at `:1047` and runs to `:1283` |
| Brief §4, Opus §1.4: `DriverMeetings.cs:26-27` is `MeetingTurns ?? Unavailable` | `:26-27` is the class's remark; the code is `:249`, `Unavailable` at `:320` |
| Opus §6.3: `DriverChoicesCommand.cs:108-109` holds `intake` and `helper` | The verb list is `:154-155`; `case "helper"` `:540` (`WithHelper` `:545`), `case "intake"` `:556` (`WithIntake` `:562`) |
| Opus §6.1: `driver.json` is written under the keep lock at `DriverConfig.cs:866` | `:866` is the parse error; `Save` is `:914`, the lock `:918`, `JsonKeeping.Kept` `:923` |
| Opus §2.8, §5.3: `PipeCensus.cs` | It is `Modules/Platform/PipeCensus.cs`. Every `SET_*` is `Person` (`:234-245`) except `SET_STANDING_CONFIRMATION`, which is `Window` (`:269`) |
| Claude C3: the ACP door's `_meta.claudeCode.options.settings` carries the rules, so it can carry `model` | It carries a **file path** (`Adapters.cs:1794-1795`, `:1809`), the same file the pipe door's `--settings` names (`:2222-2226`), written by `SpawnSettings.Write` (`Agents/Permissions/Permissions.cs:507-588`: `permissions`, `autoMode.hard_deny`, `hooks`; no model). A `model` there is the tool's settings tier, which `ANTHROPIC_MODEL` outranks (§3.2) |
| Claude C3, Codex §6: a partner's admission digest covers "the settings' digest", so a model in the settings changes or must join it | `PartnerConfiguration.Digest` (`Conversations/Domain/PartnerConfiguration.cs:56-72`) hashes `posture=`, `guard=` and the sorted `allow`, `ask`, `deny` and `hard` lines only. It never reads the settings file's content, so neither carrier of §3 changes it |
| Brief §3.2: Codex and dsh are refused by name | `AgentSettings.cs:242-245` refuses any agent whose toolchain declares no `SettingsFile`; neither is named |
| Opus §2.4: Claude's usage answer carries model-scoped weeks Daoris does not read | True: the recorded answer at `ClaudeUsage.cs:41` holds `seven_day_opus` and `seven_day_sonnet`; `:78` reads only `five_hour` and `seven_day` |
| Opus §2.1: `max` on the pipe door waits for a flag | Claude Code's model configuration page (read 2026-10-23, code.claude.com/docs/en/model-config) documents `--effort <level>`, `max` included, for one session, and the order *`/model`, then `--model`, then `ANTHROPIC_MODEL`, then the settings' `model`*. None of the three proposals used `--effort`; row a measures both |

The rest of what the proposals cite holds, with line drift of one to three lines: `Acp.cs:82` (`ThePersons`), `:516-517` (an
effort's levels follow the model), `:527-539`, `:918-923`, `:940-944`; `Driver.cs:1671-1672`, `:2583`, `:2674-2735`,
`:3161-3180`, `:3775-3778` (the driven `AcpSession`, no `onOptions`); `ChatRunner.cs:2185-2190`; `HarnessRoster.cs:883-1007`;
`ReviewerChoice.cs:175-237`; `HelperChoice.cs:15-37`; `PartnerChoice.cs:17-31`; `DriverBrainstorms.cs:345-359`, `:418`,
`:603-609`; `TurnModels.cs:9-25`; `InstancePipe.cs:557-562` (a `Person` route waits for the window's card, whoever calls);
`HelpSettingProposals.cs:31-38`; `HelpRoomDoors.cs:160-161`; `Sessions.cs:63`, `:95-99`, `:158`, `:165`, `:190-193`; the
three home twins (`home.ts:99`, `Driver/Platform/DaorisHome.cs:326`, `Core/Platform/DaorisHome.cs:319`) and their agreement
test (`src/Daoris.Cli/test/platform/home.test.ts:323-328`).

## 1. The three proposals, question by question

*C* is Claude's proposal, *X* Codex's, *O* the Opus reader's. The last column is this design's choice; the section after the
table gives each reason and the code it rests on.

| Question | C | X | O | Chosen |
|---|---|---|---|---|
| **The actors, and their names** | Eight: Build 开发, Conversation 对话, Ask Daoris 问道衍, Intake 受理, Second opinion 第二意见, Helper 助手, Partner 搭档, Brainstorm 头脑风暴. *actor* / 角色. Meeting, orient, a *small* build later | Eleven: adds Exact work 明确工作, Orientation 定位 (a new start), Brainstorm idea 方案生成 and Synthesis 方案汇总 apart; Development 开发, Chat 聊天. *actor* / 角色 beside *agent* / 智能体 | Eight, as C but Chat 聊天 for the person's conversation, id `ask-daoris`. *actor* / 角色; *choice* / 选择 and *account default* / 账户默认 as terms | **O's eight** (§2). Exact work and orientation wait for a signal and a start; the synthesis keeps D176's first-answered |
| **The range, and the account walk beneath it** | Up to 3 *choices* `agent[/model[/effort]]`; choice-major: all accounts of a choice before the next; a per-model cool-off read now | A list of *options* `{agent, model, effort}` with *Account default* explicit; choice-major; a missing entitlement does not cool an account | 1 to 4 *choices* `{agent, model?, effort?}`, *account default* shown; choice-major; model-scoped cool-offs a later row | **O's shape and cap, X's explicit account default, all three's choice-major walk; model-scoped cool-offs later** (§2.2, §4) |
| **How the model is carried to each door** | Claude Code: `model`/`effortLevel` in the spawn's settings file on both doors; others: `set_config_option` | Protocol door: `set_config_option`, model then effort, before work; pipe: a model flag and effort in the settings; a conflicting `commands[agent]` refused | Protocol door: `set_config_option` for every agent, model then effort, before the first prompt; pipe: `--model`, effort in the settings file; a person's command naming `--model` wins | **O's protocol door; the pipe door by `--model` and `--effort`**, measured first (§3) |
| **Fallback, and what is said** | Next choice when every account of one is held; mid-line: same agent other model moves silently, another agent by D163; a note and a chip | Option by option, reasons structured; never replay after a refusal; *No option ready*; a selection frozen per run | Choice by choice with coded passes; a running session never moves; a cooling agent's hand-over names the next choice; a note, a chip, the log; the wait names the soonest | **O, with X's rule that a refusal after dispatch is never a fallback** (§4) |
| **Who may versus which model** | The `helpers`, `partners`, `reviewers` and brainstorm lists *become* the range, mirrored back | Lists stay fences; the range orders within them | Rules say who may; choices say with which model; an empty rule reads the actor's choices | **O and X: the rules stay** (§4.4) |
| **Defaults and migration** | Empty seat = as today; one suggested set (`suggested.json`), applied by a press | A compatibility projection, *Current setup*; a dated suggestion with concrete acceptance words; old keys kept as fences | Read from today's keys, *as before*; a suggestion table held to `model-roles.md` by a census, *Use these* / *Not now*; old scalar keys written through | **O's reading and write-through, O's census-held table, X's acceptance words** (§6) |
| **Where it lives, and the UI kept simple** | Settings → AI features → *Actors*, a row each, an inline editor; head chip | The same place, short summaries, one editor open; a details fold on the head | The same place; first choice and *+n*; a use line; *Details* rows; a wait sentence | **O's screens, X's one-editor-open rule** (§7) |
| **Ask Daoris, restricted and full access** | `actors_propose`; full access acts itself, with a notice and *Put back* | `actor_ranges_propose`, a revision-checked patch; full access applies only what was asked, never invents spending | `actor_propose`; in both modes the change is the person's press, since `SET_ACTOR` is a `Person` route; no `settings_*` grant | **O's tool and rule, X's revision check and several actors in one card** (§8) |
| **The records and the terminal verb** | `actors` in `driver.json`; `actor` and `choice` on the record; `daoris driver actor` | `actors` versioned in `driver.json`; a new `data/actor-runs/` record; `daoris driver actors set …` | `actors` in `driver.json`; four machine-local fields on the record; `data/agent-offers.json`; `daoris driver actor` | **O** (§9) |
| **The tier that ran** | Read back from `TurnModels`; *asked sonnet; ran opus* judged and said | Requested, acknowledged and reported kept apart; a reported model outside the choice holds the next dispatch | *asked* and *ran* both shown, never judged against each other, since that needs a model list | **O, with X's three-way distinction in *Details*** (§9.3) |
| **Cost shown** | Tokens per actor this week, split by model on hover; per session by model | Measured consumption, never money; absent never zero; no unsplit turn apportioned | A 7-day use line per actor (starts, tokens, later choices); a model per part | **O's line, X's rules for what is never shown** (§10) |
| **What it amends and rejects** | D98, D174 p4; notes on D199, D155 | D24, D98, D57/TOOL3, D179, D89/D110, D155, D163/D189, D174, D176, D199, D172/D195, D116 | D98, D24, D57 (narrow), D179, D174, D155, D163, D65/D89, D183, D176, D130, D203, D116 | **O's list, D199 left unchanged with a test, D195's inventory added** (§12) |
| **The rows** | a–g: the seat first, the door measured inside row b | a–h: provenance first; doors measured inside row b | a–h, then i–k filed: **measure each door first**, then the record, then actor by actor | **O's order**, as the quest asks: the first row measures what each door accepts (§13) |

### 1.1 What each proposal got that the others missed

**Claude's.**
- **A census that every start resolves through one seam**, so a new start cannot bypass the actor unseen (C10). Kept, made
  stronger as O's required parameter (row b).
- **The light build measured before it is offered** (C2's *small*): a mark set wrongly costs a failed build, not a saving. Kept
  as row i, behind a measurement.
- **A per-model cool-off where the usage read gives one.** Right in aim, early in time: Daoris reads no model-scoped window yet
  (`ClaudeUsage.cs:78`). Kept as row k.
- **Keeping old builds sensible** by writing the old keys in step. Kept for the scalar keys only (§6.3).

**Codex's.**
- **A refusal after work is dispatched is not an availability signal**: no replay on another model after a content refusal, an
  uncertain send or a half-done turn (X §2). Taken whole (§4.3).
- **Requested, acknowledged and reported are three facts**, and a successful `set_config_option` is not proof the model ran
  (X §4). Taken into *Details* (§9.3).
- **A stale card never overwrites a newer choice**: Ask Daoris's patch carries the revision it read (X §5). Taken (§8).
- **Full access does not invent spending** (X §5). Taken, and the code already enforces it (§8.2).
- **Rollback disclosed**: an older build reads only the old keys, and says so (X §3). Taken (§6.3).
- **Never show missing as zero, never apportion an unsplit turn** (X §4). Taken (§10).
- **The brainstorm's synthesis deserves its own choice** and **exact work its own actor** (X §1). Both right in aim, both
  deferred: D176 point 9 keeps the first-answered synthesizer until a measurement says otherwise, and no start can yet tell
  exact work from judgement (§2.3).
- **A new home entry goes into the cleanup inventory** (X §6, D195). Taken for `agent-offers` (§9.2).

**The Opus reader's.**
- **The rules say who may; the choices say with what model** (O §2.8). The composition that keeps a partner's writing
  permission out of a machine-wide list. Taken (§4.4).
- **`choice`, not `option`**, since *option* already names an agent's config option and a workflow node's `options`. Taken (§2.1).
- **`--fallback-model` rejected**: it switches models inside a turn, where nothing can say why (O §2.2). Taken (§3.4).
- **An unoffered effort runs at the account's default and says so**, rather than passing a whole choice for a dial (O §2.3).
  Taken (§3.3).
- **What each agent last offered, kept in a home entry**, so the editor and the walk can judge a choice before a spawn (O §6.2).
  Taken (§9.2).
- **dsh has no full posture**, so a full-access Ask Daoris passes it (`no-full-access`). Checked: `DshAdapter` declares no
  `FullPosture` (`Adapters.cs:1275` defaults null; Claude's two doors `:1757`, `:2192`, Codex `:1985`). Taken (§4.1).
- **Measure each door first.** The only proposal whose first row measures what the doors accept before anything rests on them.
  Taken as row a.

## 2. The actors

### 2.1 The words

| Term | en | zh | Means | Avoid |
|---|---|---|---|---|
| actor | actor | 角色 | A kind of work Daoris starts an agent for, with the person's choices of agent, model and effort for it, in order | en *role* (D203's co-worker role and D174's development roles), *profile*; zh 演员, 岗位, 人设, 执行者 |
| choice | choice | 选择 | One entry of an actor's order: an agent, and its model and effort, or the account's default for each | en *option* (an agent's config option; a workflow node's `options`), *range*, *fallback*; zh 选项, 备选 |
| account default | account default | 账户默认 | What an agent runs when a choice names no model or effort: the account's own settings and today's cascade | en *inherit*, *its own*; zh 默认模型 |

**Why 角色.** It is unused in the Chinese chrome (checked: no match under `Web/locales/zh/`), and it reads as the part one plays,
the owner's "proper role". 演员 is a stage performer; 岗位 a post one hires a person for; 人设 a chatbot's persona; 执行者 sits
beside the workflow's executor; 代理 is banned for *agent* (`glossary.json:581`). **Why not *role* in English:** two things named
*role* would meet in one place, a helper's seat and a helper's D203 role. *Range* stays a design word and never reaches a page.

### 2.2 The list

| Id (wire, terminal) | en | zh | Starts (brief §4) | Chosen today by |
|---|---|---|---|---|
| `build` | Build | 开发 | 1 (and workflow *work* and *look* nodes, `WorkflowGraphRun.cs:314-316`); 2 and 3 keep it | `handedQuest?.Agent ?? config.Adapter` (`Driver.cs:1671-1672`) |
| `intake` | Intake | 受理 | 5 | `config.IntakeAdapter` (`DriverIntakes.cs:111`) |
| `opinion` | Second opinion | 第二意见 | 6, its recheck, and a workflow *opinion* node | the walk over `reviewers` (`ReviewerChoice.cs:175-237`) |
| `helper` | Helper | 助手 | 7 | `HelperChoice.Candidates` (`HelperChoice.cs:15-29`) |
| `partner` | Partner | 搭档 | 8 | named by the ask, admitted by a measured row (`PartnerChoice.cs:17-31`) |
| `brainstorm` | Brainstorm | 头脑风暴 | 9: every generator and the synthesizer | request, policy, then `config.Adapter` (`DriverBrainstorms.cs:348`) |
| `ask-daoris` | Ask Daoris | 问道衍 | 11, and 12 and 13 for its conversations | `config.HelperAdapter` (`HelpRoutes.cs:52`) |
| `chat` | Chat | 聊天 | 10, and 4, 12 and 13 for chats | the request's agent, else `config.Adapter` (`ConversationRoutes.cs:48`) |

- **A continuation keeps its record's actor** (starts 2, 3, 4, 12, 13): a quest handed over is still *Build*.
- **Every name but *Build* is a glossary term already**: *intake* `:482`, *second opinion* `:535`, *partner* `:537`, *helper*
  `:539`, *Ask Daoris* `:628`, *chat* `:477`; *brainstorm* / 头脑风暴 is the notifications' word (`notifications.json:55`) and
  enters the glossary. *Build* / 开发 names the work, and avoids 构建, which the chrome keeps for build output (`note.json:53`).
- **Chat, not Conversation.** The glossary's *chat* / 聊天 is "a session the person started to talk with its agent", and it bans
  对话 for it (`glossary.json:477-478`); *conversation* / 对话 is what every session keeps. C's *Conversation* / 对话 breaks that.
- **`ask-daoris`, not `help`**, so the id is not read as *helper*, which `helperAdapter` already confuses in the code.

### 2.3 What is not an actor, and the named kinds without a start

- **The usage readers, login, install, update and version probes**, and a **native subagent** (its model is the tool's
  frontmatter, and its tokens already arrive in `TurnModels`' split, `TurnModels.cs:19-20`).
- **A workflow node** takes its kind's actor. C's node naming another actor is left out: a node's actor is fixed by its kind,
  and a graph that names one carries a machine's choice in a portable record.
- **A set-up** starts no agent (`SetupWorld.cs:79`); its asks go through Intake and Build.
- **A meeting**: no real turn yet (`DriverMeetings.cs:249`). The row that gives meetings real turns names its actor.
- **Orientation**: no start exists. X's *Orientation* actor and O's row j agree it arrives with the start that hands a pack
  (row j). It is not offered before something sits in it.
- **Exact work**: nothing at a start says a quest is exact. C's *small* mark, X's *Exact work*, O's *weight* are one idea; it is
  row i, after a batch of row b's records measures the share D160 targets.
- **The brainstorm's synthesis**: D176 point 9's first-answered synthesizer stands (`DriverBrainstorms.cs:416-418`). X's own
  range for it is filed in row i's note as a measurement to take, not a setting to show.

## 3. A choice, and how a start carries it

### 3.1 The shape

A choice is `{ agent, model?, effort? }` in `driver.json`'s `actors` (§9.1). An absent `model` or `effort` is **account
default**: nothing is passed, today's cascade decides, and the page says *account default* (X's point that it is a visible
choice, not an omission). The values are the tool's own words:

- **The pipe door** (`claude-code`): the SDK's aliases (`AgentSettings.cs:59`) or a full id in the free field; efforts
  `low, medium, high, xhigh` (`:65`), and `max` once row a measures `--effort max`, since it is one start's, not a file default
  (D98 refuses `max` only as a default, `:185-199`).
- **A protocol door** (`claude-code-acp`, `codex-acp`, dsh): what that agent and version last offered in `model` and
  `thought_level` (§9.2), or the free field.

**One to four choices.** Four is O's cap and it matches the most voices a brainstorm takes (`BrainstormDesk.MostAgents = 4`,
`BrainstormDesk.cs:63`). C's three would cap a brainstorm's voices below what it runs today. No two choices are the same. The
rules are judged in one place for every door (`ActorChoices.Judge`), so the screen, the terminal and Ask Daoris refuse alike.
*Off* (an empty list) only for Intake and Ask Daoris, as today's *absent means off* (`DriverConfig.cs:411-434`, `:1452-1455`).

### 3.2 The carriers

**A protocol door, every agent alike** (O, X): after `session/new` and the posture (`Acp.cs:940-944`; a resume `:2620`), before
the first prompt, the driver sends `session/set_config_option` for `model`, reads the answer (an effort's levels follow the
model, `:516-517`), then sends it for `thought_level`. Only values that answer offered, only in `ThePersons`' two categories
(`:82`). The driven `AcpSession` gains the asked choice (`Driver.cs:3775-3778` has no `onOptions` today), as the chat's already
gains an options callback (`ChatRunner.cs:2189`). `SetConfigOptionAsync` (`Acp.cs:527-539`) is reused as it is.

- *Why not C's settings file on the ACP door.* The file `_meta.claudeCode.options.settings` names is the settings tier
  (§0.2), which `ANTHROPIC_MODEL` and a session's own `/model` outrank, and it is Claude-only. `set_config_option` is the tool's
  own session-level door, the same on every protocol agent, and the one the composer already uses.
- *How Codex and dsh are honoured without writing their homes.* Through the same offered options. D98 recorded that dsh's one
  config option is its model catalogue (D98, *Boundaries*); `codex-acp`'s categories are measured in row a. Where a door offers
  no `model`, its only choice is *account default*, and the editor says so.

**The pipe door** (`claude-code`): `--model <value>` and `--effort <value>`, appended after the person's command as the other
flags are (`Adapters.cs:2088-2096`). The documented order (§0.2) puts `--model` above `ANTHROPIC_MODEL` and every settings file,
and `--effort` among the explicit choices above every saved effort. So a choice that **names** a model holds for that start
against the account's `settings.json`, a repository's own `.claude/settings.json` and the person's environment; a choice that
names none passes nothing and leaves the cascade exactly as today.

- *Why `--effort` over O's `effortLevel` in the spawn file.* A settings file's `effortLevel` ranks below a model's saved effort
  (`modelSettings.<model>.effortLevel`, `AgentSettings.cs:21-51`), so the choice could quietly not hold, and `max` cannot ride a
  file at all. Row a measures `--effort` on the installed version; if it is absent there, `effortLevel` in the spawn's settings
  file (`Permissions.cs:507-588`) is the fallback, and `max` is not offered on that version.
- *A person's own command.* Where `commands[claude-code]` (`Driver.cs:2583`) already names `--model` or `--effort`, nothing is
  appended for that flag: the person's command is more specific (O). The record's *asked* says *your command*. X's refusal is
  rejected: it would stop a start the person deliberately configured.

**Why the session level, given D98's cascade.** The actor is the person's deployment choice for that kind of work, D98's own
"for an agent a person runs, the deployment is the person". A model the person named for Build should hold for every Build,
whichever repository it runs in. *Account default* is still there for the person who wants the repository's or the account's
own setting to decide.

### 3.3 When the agent does not offer the choice

- **Judged before a spawn**: a choice whose model the agent's last reading lacks is passed with `not-offered` (§4.1).
- **Caught at the open** (no reading yet, or a new version): the open reads `session/new`'s options; a model not among them,
  or a `set_config_option` the agent refuses, stops the process **before the first prompt**, the reading is kept, and the walk
  continues at the next choice within the same start (C). Nothing is spent beyond the spawn, and no strike is counted (D125: a
  limit is not a strike; nor is this). O's *end the part failed, pass it next time* reaches the same place a look later and
  costs a conversation its first press; C's in-start move is chosen.
- **The pipe door has no offer list.** An unknown `--model`'s words are measured in row a and read as `refused`, like the
  toolchain's `Refused` words (`Adapters.cs:2300`), with no strike.
- **An effort not offered** runs at account default effort, and *Details* says so (O). A model decides what runs and what it
  costs; an effort is a dial within it, and running without it is today's behaviour. X's *the option is unavailable* is
  rejected for effort alone.

### 3.4 Rejected carriers

| Carrier | Why |
|---|---|
| The account's `settings.json` | Shared by every actor on the account; two starts race on the tool's own file; a repository's settings outrank it |
| `ANTHROPIC_MODEL` at spawn | D98's rejection stands; Claude-only; inherited by every child the session runs |
| `model` in the spawn's settings file (C) | The settings tier: `ANTHROPIC_MODEL` outranks it, and it is Claude-only |
| `_meta.claudeCode.options.model` | Claude-only, precedence unmeasured; `set_config_option` does it for every protocol agent |
| The tool's `--fallback-model` | Switches models inside a turn, where Daoris cannot say why; the turn reads `mixed` |
| Writing `CODEX_HOME` or `DSH_HOME` | D98's boundary |

## 4. The walk, fallback and what is said

### 4.1 At a start: choice by choice, each choice's accounts before the next

All three agree, and so does the reason: the person's order is about who does the work, and an agent's accounts are
interchangeable seats of one set (D197 point 1). Taking a later choice while a seat of an earlier one is free spends against
that order.

```
for each choice, in order:
    pass it, coded, if it cannot run here:
        unavailable (not installed) · not-offered (§3.3) · no-full-access (Ask Daoris in full access, an agent with
        no FullPosture: dsh, Adapters.cs:1275) · not-admitted (a partner, D199) · not-listed (a rule's fence, §4.4)
    else that agent's account walk, unchanged (HarnessRoster.SelectAsync :883-886, WalkAsync :1047-1283; the order,
        cool-offs, sign-ins, keep, curve, the cap, D197):
        allowed → this choice, this account
        held    → pass it with the walk's own reason
no choice allowed → the wait (§4.5)
```

- **A passed choice takes no slot under the cap.** The walk already tells a panel's question from a start (`ChooseAsync`'s
  `counts`, `HarnessRoster.cs:1003-1007`; a panel's at `:1786`). Only the choice taken counts (X).
- **Driven work never takes an account kept for conversations**, nor the person's own sign-in (D197 point 3).
- **A missing model entitlement does not cool an account** (X): it passes the choice for this start, coded `refused`.
- **Model-scoped cool-offs are row k** (O): read `seven_day_opus` and `seven_day_sonnet` (`ClaudeUsage.cs:41`, unread at `:78`),
  then cool a limit naming a model's window as account plus model, so a later choice on the same account may run. Until then a
  cool-off is the account's, as `AccountCooling.Cool` stores it.

### 4.2 Mid-session: nothing moves silently

- **A running session never moves** to another choice (D163).
- **A part that ends on a limit** carries on as today, on another account of the same agent (`CarriedOnAsync`,
  `HarnessRoster.cs:991-995`), with **the same choice re-applied** at the open.
- **When every account of that agent is cooling**, D163's offer names the actor's next choice on another agent: *Hand over to
  Codex · account default, Build's choice 2?* Under `handOver: ask` (the default) it asks; `on` takes it; `off` waits.
- **C's silent move to the same agent on another model is rejected**: the cool-off is the account's (§4.1), so that model on
  that account is cooling too, and a move nobody was told of is what the brief §5 forbids.
- **A resume re-applies the record's asked choice** (`ResumeAsync`, `HarnessRoster.cs:924`), closing the brief §3.2's gap where a
  conversation going on lost an earlier choice. A composer change beside the composer wins over the actor for that
  conversation, and its last note is what a go-on re-applies (`ChatGoOn.cs:307`).
- **The next start of the same line** begins again from choice 1: the order is a preference, not a ratchet (C).

### 4.3 A refusal after dispatch is not a fallback (X)

After a prompt is sent, a content refusal, an uncertain send or a half-done turn is never retried on another model or agent. A
limit follows the cooling and carry-on rules above. The brainstorm keeps its one bounded restart on the next account of the same
agent (`DriverBrainstorms.cs:603-609`) and gains no range retry.

### 4.4 Who may, and with what model

`opinions.reviewers`, `helpers` and `partners`' agents and the brainstorm policy's agents are a repository's or workspace's say
over **who may** do a job, classed as the person's re-wiring (`PipeCensus.cs:77-78`). They stay. The actor says **with which
model**:

- **Where a rule names agents**, its order stands; each agent takes its model and effort from the actor's choices for that agent,
  in their order, else account default.
- **Where a rule names none**, the actor's choices are the order.
- *Second opinion*: `rule.Reviewers` (`ReviewerChoice.cs:211-214`) expands each named agent into its choices; the passes stand
  (another maker first, then an undeclared maker, then the same agent only where listed, `:175-180`). The same-agent label names
  the model: *the same agent, fresh: opus*. A model never makes a reviewer independent (D155 point 4).
- *Helper*: an agent the ask names (where the rule allows it) takes its model from the choices; with no rule list, the actor's
  choices replace *every agent of another maker* (`HelperChoice.cs:15-29`), another maker's first.
- *Partner*: still named by the ask (D199); the actor supplies only its model and effort. Admission is unchanged (§11).
- *Brainstorm*: voices come from the request, then the policy, then the actor's choices in place of `config.Adapter`
  (`DriverBrainstorms.cs:348`), up to `MostAgents`. A passed choice frees its voice to the next unused choice; a fallback never
  adds voices beyond the list (X).

**C's mirror is rejected.** The rules are scoped (a repository's, or the workspace's, brief §3.6), the actor machine-wide; one
cannot mirror the other. And a partner list is a permission: an actor that wrote it would widen who may write.

### 4.5 When no choice can run

- **Driven starts** hold on today's hold path; the quest's row line says: *Build waits: none of its choices can run now. Claude
  Code's accounts cool until 14:05; Codex is not signed in.* The wait is judged again at every look, and the first choice that
  can run **in order** is taken.
- **Chat and Ask Daoris** refuse the press where it was made, in the same sentence (D185 R3).
- **No new kind in *What needs you*.** A cool-off asks nothing of the person (X's caution, `.claude/knowledge/person-waits.md`):
  a sign-in or a changed order is the person's act, reached from the sentence's doors (*Accounts*, *Edit*).

### 4.6 What is said when a later choice ran

1. **The opening note** gains an actor line beside *Opened on `account`: why* (`note.opening.why.*`): *Ran as Build's choice 2,
   Claude Code · sonnet: choice 1, Claude Code · opus · high, was refused by account-1 and account-2.*
2. **The head**: a `status` chip *choice 2* / *第 2 选择* only when not choice 1, or *your pick* / *你的选择*.
3. **The machine log**: `actor.chose` (the choice, and each passed one with its code), `actor.passed`, `actor.ran`.
4. **The terminal loop**: *started #412 on claude-code (account-2) as build's choice 2: …*

Nothing goes to Notifications: a fallback asks nothing. A pattern shows on the actor's use line (§10).

## 5. The tier that ran

D24's "say the tier that ran", for agents: each session says its actor, the choice taken, what was asked and what the agent
reported.

- **What was asked** is the choice (or *your pick*, *as before*, *your command*).
- **What was acknowledged** is the agent's answer to `set_config_option`, on the protocol door.
- **What ran** is always the agent's own word: the turn's `_meta["_claude/model"]` and `_meta.quota.model_usage` on ACP
  (`Acp.cs:2216-2220`, `:1379-1394`), `message.model` on the pipe (`StructuredOutput.cs:212`, `:332`). Absent: *not reported* /
  *未报告*. More than one: each, in order of first use (`opusplan` runs two).
- **Never judged against each other** (O). Telling `opus` from `claude-opus-5-5` needs a model list (D57). Both are shown; a
  person reads the difference. X's *a reported model outside the choice holds the next dispatch* is rejected for that reason.

## 6. Defaults, the suggestion and migration

### 6.1 The first start after the upgrade is today's

With no `actors` in `driver.json`, each actor's choices are **read from today's keys**, at account default, and shown *as
before* / *沿用原设置*:

| Actor | Read from |
|---|---|
| Build, Chat | `[adapter]` (`Driver.cs:1672`; `ConversationRoutes.cs:48`) |
| Intake | `[intakeAdapter]`, or off where absent (`DriverConfig.cs:1452-1455`) |
| Ask Daoris | `[helperAdapter]`, or off where absent |
| Second opinion, Helper, Partner, Brainstorm | empty: their rules and today's fallbacks decide (§4.4) |

Nothing spends differently, so the upgrade itself reaches nothing D191 point 3 keeps for the owner.

### 6.2 The suggestion

**What it is** (O): one dated code table in the driver, `ActorSuggestion`, from actor to choices in Claude Code's own aliases and
*account default*, held to `docs/model-roles.md`'s roles table by a census that fails both ways. It is not a registry: no
versions, capabilities or prices. C's `suggested.json` is the same table without the census, so it could drift from the
reference silently.

**Its first content**, from the reference reviewed 2026-10-23, with Opus 5.5 leading the reasoning work as the owner directed:

| Actor | Suggested | The role it reads |
|---|---|---|
| Build | Claude Code · `opus` · high, then Claude Code · `sonnet` · account default | build-judgement (Opus 5.5, high). No start can tell exact from judgement yet (row i), so the stronger leads; `sonnet` is there for an account that refuses `opus` |
| Second opinion | Codex · account default, then Claude Code · `opus` · high | review: another maker, else Opus high (labelled *the same agent, fresh*) |
| Helper | Codex · account default, then Claude Code · `opus` · high | decide-support: another maker, high |
| Partner | Codex · account default, then Claude Code · `opus` · high | supplies only the named agent's model (§4.4) |
| Brainstorm | Claude Code · `opus` · high, Codex · account default | decide-support: different makers' voices |
| Intake, Ask Daoris, Chat | unchanged | no role in the reference reads intake's routing, and D174 rejects choosing by price alone (X); help is the person's (D89, D98) |

- *Claude Code* means the Claude door this machine runs (`adapter`). Another maker's choices appear only where that agent is
  installed with an account signed in for Daoris; otherwise the card says it left them out.
- **A new version behind an alias** needs nothing: *ran* shows the new id. **A different alias for a role** moves only through
  D174: the reference promotes it with its date and batch, the census fails until the table follows, and the next release offers
  the changed actors on the card, never applied.

### 6.3 Who accepts it, and in which words

The **person's press**, never anything else. On the owner's install, that press is D191 point 3's yes for the spending it
changes. The card lists each actor that would change, before → after, with X's concrete words:

> **en:** *Use these choices for new work on this machine. Daoris tries a later choice, and other Daoris accounts, only when an
> earlier one cannot start. This changes which models your accounts spend on, and can spend more. Work already running keeps its
> choices.* **[Use these]** **[Not now]**
>
> **zh:** *在这台机器上的新工作使用这些选择。只有前面的选择无法启动时，Daoris 才会尝试后面的选择和其他 Daoris 账户。这会改变你的账户把用量花在哪些模型上，用量也可能更多。正在运行的工作保持原来的选择。* **［采用这些］** **［暂不］**

- *Use these* copies the suggestion into the person's own choices, unlinked: a later suggestion never follows into them.
- *Not now* records the suggestion's date (`actorsSuggestion`); the card stays away until the table changes.
- Its terminal twin is `daoris driver actor suggested [--use]`.

### 6.4 Migration

- **An account's `model` and `effortLevel`** (D98's first level, `AgentSettings.cs:96-168`) stay, as what *account default*
  means, set where they are today. The *Model and effort* fold gains one line: *Used where an actor runs this agent on its account
  default; a model an actor names outranks it.* / *角色以账户默认运行此智能体时使用；角色指定的模型优先于它。*
- **`adapter`, `intakeAdapter`, `helperAdapter`** are read as §6.1. Once the person sets Build, Intake or Ask Daoris, the key is
  **written through** (O): the first choice's agent, or the key absent while off. An older build therefore starts the same agent
  at account default; a rollback loses only the model, and the *Actors* section says so in its footnote (X).
  `daoris driver intake|helper <agent>|off` remain and write a one-choice list at account default, saying what they replaced.
- **`opinions.reviewers`, `helpers`, `partners` and the brainstorm policy's agents** are unchanged (§4.4); `setting_propose`'s
  `opinion` and `helpers` doors stay theirs (`HelpSettingProposals.cs:31-38`).
- **The composer's picker** is unchanged; it opens showing what the actor set, since the agent answers with its current option.
- **An old `driver.json`** reads as today. An older build reading a new file ignores `actors`, which `JsonKeeping` keeps on save
  (`DriverConfig.cs:923`). A malformed `actors` entry holds the affected start with its error, never silently reverting (X).

## 7. Where it lives, and the UI kept simple

**Settings → AI features** (`Web/settings/AiDomain.tsx`; the glossary's *AI features*, "the jobs Daoris gives a model or an agent
of its own", `glossary.json:651`), **machine-wide** as D197's one set and D183 point 2's machine-wide settings. *Search and
convergence* stays above; *Ask Daoris's access* below. The *Intake* and *Ask Daoris* agent rows (`settings.ai.json:7-37`) fold
into their actor rows.

```
Actors
Each kind of work Daoris starts runs on the first of its choices that can run, in your
order, and says so when it took a later one.                       [Use these…] ← only while a suggestion is unanswered

Build            Claude Code · opus · high  +1                                     Edit
                 14 starts this week · 2.1M tokens · 2 on a later choice
Intake           Off                                                               Edit
Second opinion   Codex · account default  +1   · another maker first               Edit
Helper           as before                                                         Edit
Partner          as before                                                         Edit
Brainstorm       as before                                                         Edit
Ask Daoris       Claude Code · account default   as before                         Edit
Chat             Claude Code · account default   as before                         Edit
Also `daoris driver actor`. An older Daoris reads only each first agent.
```

**One editor open at a time** (X), inline where *Edit* was pressed (D185 R3), on `SettingList`'s label track (D185 R1):

```
Build: your order
  1  [Claude Code ▾]  [opus ▾]       [high ▾]              ↑ ↓ ✕
  2  [Claude Code ▾]  [sonnet ▾]     [account default ▾]   ↑ ↓ ✕
  + Add a choice                                            (at most 4)
  Choice 2 runs only when no account of choice 1 can: cooling, signed out, or refusing its model.
  A model named here outranks the account's own and a repository's own setting.
                                                                      Cancel   Save
```

- **The model menu**: *account default*; the agent's own words (the aliases on the pipe door, what a protocol agent last
  offered); *Other…* for a full id. Before Daoris has read an agent's models: only *account default* and *Other…*, with *Codex
  has not said its models yet; its next session here reads them.* The effort menu follows the same rule. Intake and Ask Daoris
  add *Off*.
- **Keyboard order**, never drag-only (X).

**The session's head**: a chip only when not choice 1; *Details* (folded, D152 §7) gains *Actor*, *Model* (*asked* ·
*acknowledged* · *ran*) and *Effort* rows; the account stays in *Details* (`headFacts.ts:172`); the parts fold gains each part's
model as it ran.

**At a start**: the person's pick of agent and account (D130 point 4) overrides both; the model is the actor's first choice for
that agent, else account default, and the head says *your pick*. **A quest, a node or an ask names no actor**: a quest travels
and must name no model (D24), an actor is this machine's, and a node's actor is its kind's. Row i's *weight* will be a fact about
the work that travels, never a model.

**Left out, to keep it simple** (all three agree on most): choices per workspace or repository; an *Auto* or intent router (it
needs a model to judge the work); per-error lists; a price or catalogue; a picker on a running driven session; a separate
synthesizer setting; a *wait N minutes for choice 1* setting; a notification per fallback; more than four choices; a new
navigation place; account selectors inside a choice.

### 7.1 The words

| What | Kind | en | zh |
|---|---|---|---|
| Section | `section` 32/12 | Actors | 角色 |
| Section body | sentence | Each kind of work Daoris starts runs on the first of its choices that can run, in your order, and says so when it took a later one. | Daoris 启动的每种工作，按你排的顺序，在第一个能运行的选择上运行；用了后面的选择时会说明。 |
| Actor names | `field` | §2.2 | §2.2 |
| account default | `choice` | account default | 账户默认 |
| Off | `choice` | Off | 关闭 |
| as before | `status` 16/5 | as before | 沿用原设置 |
| choice n | `status` 16/5 | choice {{n}} | 第 {{n}} 选择 |
| your pick | `status` | your pick | 你的选择 |
| not reported | fragment | not reported | 未报告 |
| Buttons | `button` 20/8 | Edit · Add a choice · Use these · Not now | 编辑 · 添加选择 · 采用这些 · 暂不 |
| The wait | sentence | {{actor}} waits: none of its choices can run now. {{soonest}} | {{actor}}在等待：它的选择现在都不能运行。{{soonest}} |
| The use line | sentence | {{starts}} starts this week · {{tokens}} tokens · {{later}} on a later choice | 本周启动 {{starts}} 次 · {{tokens}} 个 token · {{later}} 次用了后面的选择 |

The pass reasons (`unavailable`, `not-offered`, `refused`, `cooling`, `signed-out`, `no-full-access`, `not-admitted`,
`not-listed`) are coded sentences beside `note.opening.why.*`. 沿用原设置 is five units, at the `status` budget's edge; row b's
`names:check -- --strict` judges it, and 原设置 is the shorter fallback.

## 8. Ask Daoris

### 8.1 The tool

**`actor_propose`** (`Mcp/HelpActorTool.cs`, beside `agent_settings_propose`, `Mcp/HelpAccountTool.cs:10-31`), the driver's kind
`HelpActorProposals` after `HelpAccountProposals.cs:7-20`. One proposal may carry several actors (X's coordinated set), each
`choices`, `off`, `clear` (back to *as before*) or `suggested`, with `why`, and **the revision of `actors` it read**: at *Apply*
the driver re-reads, and a changed actor needs a fresh card rather than being overwritten (X). Every proposal is judged by
`ActorChoices.Judge`, so it can propose nothing the screen would refuse. The card shows before → after per actor, in the
screen's row words, and the terminal twin.

It does not propose a rule's agents (*who may*: `setting_propose`'s doors) or its own access mode (D183 point 2).

### 8.2 Restricted and full access

- **Restricted**: the card's *Apply* (D89).
- **Full access**: Ask Daoris may run `daoris driver actor …` itself, and the running Daoris's pipe asks the person in the
  window, because `SET_ACTOR` is a `Person` route (`PipeCensus.cs:234-245`) and a `Person` route waits for the window's card
  whoever calls (`InstancePipe.cs:557-562`). D201's standing confirmation covers no standing choice (D201 point 3) and no agent
  Daoris runs (point 4). So in both modes the change is the person's press: full access changes who types the command, not who
  says yes (O). C's *acts itself, with Put back* would need the pipe to except one route, and X's *apply what was asked* the pipe
  to know what was asked; neither is worth weakening the census for a choice that spends the owner's accounts.
- **No `settings_*` grant covers actors.** HELPMODE1c3's grants write `driver.json` with no Apply (D183's note); a list of choices
  spends accounts, and only the person's press moves that.
- **Changing Ask Daoris's own actor** affects its next new conversation, never the running one (X).

### 8.3 What it reads

The room's facts gain an *Actors* section: each actor's choices and where they came from (*set*, *as before*, *suggested*); each
agent's offered models as last read (§9.2); per actor over 7 days, starts, tokens by model, and the starts on a later choice with
each code. It can answer *why did my builds run on Sonnet this week?* from what Daoris holds, and propose from it. The doors table
gains *choose who does each kind of work* → Settings → AI features → `daoris driver actor` (`HelpRoomDoors.cs:160-161`).

## 9. The records and the terminal

### 9.1 `driver.json`'s `actors`

```json
"actors": {
  "build":   [{ "agent": "claude-code", "model": "opus", "effort": "high" }, { "agent": "claude-code", "model": "sonnet" }],
  "intake":  [],
  "opinion": [{ "agent": "codex-acp" }, { "agent": "claude-code", "model": "opus", "effort": "high" }]
},
"actorsSuggestion": "2026-10-23"
```

An actor absent is read from today's keys; an empty list is off (Intake, Ask Daoris) or no preference (the rule-listed actors).
Objects, not C's `agent/model/effort` string, since a free-field model id may hold a `/`. The writer is `DriverConfig.WithActor`
under the keep lock (`DriverConfig.cs:914-923`), judged by `ActorChoices.Judge`, writing through `adapter`, `intakeAdapter` and
`helperAdapter` (§6.4). `HomeWatch` already wakes the loop on its change. It is machine-local, as everything in that file; a
shared host carries no choice and no model.

### 9.2 `data/agent-offers.json`: what each agent last offered

What each agent and version last offered in `model` and `thought_level`, with the date, written by the driver alone at every
protocol `session/new` (`KeepOptions`, `Acp.cs:940-942`), read by the walk (§3.3) and the editor (§7). A reading, not a setting,
so not `harnesses.json` (D197's account file). It is a new home entry in the three twins (`home.ts`, `Driver/` and `Core/`
`DaorisHome.cs`, held equal by `home.test.ts:323-328`), with D195's cleanup inventory row (taken by a clear of the home, never
synced). X's re-discovery at every open alone would leave the editor nothing to offer before a session.

### 9.3 On the session record: four machine-local fields

| Field | Holds | Set |
|---|---|---|
| `actor` | the actor's id | at the open |
| `choice` | 1 to 4; `pick`; `before` (read from today's keys); `command` (the person's own flags) | at the open |
| `asked` | `{ agent, model \| null, effort \| null }` | at the open |
| `ran` | `{ models: [up to 3, first use order], effort \| null, acknowledged \| null }`, or null for *not reported* | when the first turn reports, and again only if a later turn adds a model |

They go through every row of `.claude/knowledge/session-record-fields.md`'s chain, kept off the wire by having no field there,
answered on loopback only, written with no revision, never in the mirror's insert, exactly as `profile`, `serves` and `help_mode`
(`Sessions.cs:95-99`, `:158`, `:190-193`). **Why on the record** and not only in the usage history (C) or a new `actor-runs/`
store (X): the head and the history row read records; the usage history is per account and expires at 90 days
(`UsageHistory.KeptDays`, `UsageHistory.cs:134`); `TurnModels` keeps the model in memory alone (`TurnModels.cs:9-11`). One record, not a second store
with its own retention. What it keeps is the agent's word, never a model name of Daoris's own.

**The usage history's turn line** (`UsageHistory.cs:606-640`) gains `actor`, bound where a start already binds agent, account and
door, so a turn's tokens count per actor.

### 9.4 The terminal

```
daoris driver actor                                          every actor's choices, and where each came from
daoris driver actor <actor>                                  one actor, and its last 7 days
daoris driver actor <actor> "<agent> [<model>|-] [<effort>|-]" …   set its order (- is account default)
daoris driver actor intake|ask-daoris off
daoris driver actor <actor> --clear                          back to as before
daoris driver actor suggested [--use]
```

Handed to the driver's program as its family is (D194, `cli/driver.ts` → `runDriverProgram`), in `DriverChoicesCommand` beside
`intake` and `helper` (`:154-155`, `:540-575`): one judge, no TypeScript twin. On the bridge, `ACTORS` (`Open`: choices, sources,
offers, the suggestion, the use) and `SET_ACTOR` (`Person`), both classed in `PipeCensus`.

## 10. Cost shown

**Tokens, never money** (D57). **Per session**: the parts strip's tokens, as today (`PartsStrip.tsx:18`), and the model that ran
per part, in the agent's word. **Per actor**: one quiet line over 7 days from the usage history and `actor.chose`: starts,
tokens, starts on a later choice. **Never** (X): a price; context occupancy as spend; token categories summed into one cost; an
unsplit `mixed` turn apportioned (`TurnModels` marks a split that does not cover the whole, `:19-25`); a missing measurement shown
as zero. The Agents page's *Usage* fold stays per account. MIXAGENT3a-iii's cost per row is the same per-session reading, and
this design leaves its row to it.

## 11. Containment and admission

- **Containment (D201).** Every choice starts through the same gate (`Driver.HoldAsync`, `AgentStarts.StartAsync`,
  `Driver.cs:3161-3180`); a fallback is the same start with other arguments, never another spawning path. A start the launcher
  cannot contain is still logged `agent.contained: false`. A choice never names a command: `commands[adapter]` stays the person's.
- **Admission (D199).** Unchanged. The model rides `set_config_option` or `--model`, the effort `--effort` (or the spawn file);
  the admission digest hashes the posture, the guard and the rule lists only (`PartnerConfiguration.cs:56-72`), never the
  settings file. Confinement belongs to the copy and the launcher, and a model cannot widen it. X's amendment (model, effort and
  mechanism in the digest) is rejected: it would require a measured row per model with no difference in confinement to measure.
  A test holds it: two choices, one digest. D199 point 9 already asks for the model on each partner's answer, which §5 supplies.

## 12. What it amends, once accepted

Each a dated note appended to the decision, never rewritten into it.

| Decision | The note |
|---|---|
| D98 | *"Daoris names no model and chooses none"* becomes: Daoris keeps no model list and names no model of its own; the person chooses per actor in the tool's own words; Daoris carries that choice to each start at the tool's session level (`set_config_option`; `--model`, `--effort`); its only choosing is to take the first choice that can run, saying so. The second level now opens every protocol start; the first is what *account default* means. Still: the tool's home never written, `ANTHROPIC_MODEL` rejected, `max` never a file default, a tool whose words Daoris has not read offered only *account default* and the free field. Its *Boundaries* line that Daoris never turns dsh's catalogue itself becomes: only to a choice the person made |
| D24 | A note: the tier that ran includes the model the agent reported, shown on the session; every actor works at *account default* |
| D57 | Narrow: one dated table from actor to alias, suggested and never applied, held to `docs/model-roles.md` by a census; no versions, capabilities or prices |
| D179 / TOOL3 | The record keeps the agent's reported model, machine-local; the head and the actor's use line show it |
| D174 point 4 | Realized: the product's suggestion reads the reference through a census |
| D155 point 3 | Each named reviewer takes its model from *Second opinion*'s choices; an empty `reviewers` reads the actor's choices; point 4's label names the model |
| D163 point 3 | The hand-over offer, when an agent's accounts are all out, names the actor's next choice on another agent |
| D65 §1b, D89 | Intake's and Ask Daoris's agents become their actors; *absent means off* stands |
| D183 | Under full access a choice with no full posture is passed; `actor_propose` joins the proposal kinds; no `settings_*` grant covers actors |
| D176 point 9 | A brainstorm with no named agents takes *Brainstorm*'s choices instead of the machine's agent; the synthesizer is unchanged |
| D130 point 4 | The pick overrides agent and account; the model comes from the actor's choices for that agent |
| D195 | `agent-offers` joins the inventory |
| D116 | *actor*, *choice*, *account default*, *brainstorm*, and *Build* as an actor's name |

**Stands unchanged:** D197 (the set, the order, the walk), D199 (admission, with its test), D201, D125, D47 §4, D203 (its
*Bring in another agent…*, once built, orders its agents by the actor's choices without amending it).

**Rejected**, beyond those each section names: a model routing every start, or an *Auto* pool (needs a model to judge the work,
and nobody can approve its members); cheapest-first routing or automatic escalation (no price, and D174 judges finished work);
trying a later choice before another account of an earlier one; per-workspace choices; a suggestion applied by itself, or one
that follows into accepted choices; replacing the rules' agent lists; flagging *ran* against *asked*; a notification per
fallback; the model as a travelling fact; folding the account's *Model and effort* away (it is what *account default* means).

## 13. The test plan

- **Unit**: `ActorChoices.Judge`'s table; the reading from today's keys; write-through and an older build's reading; the walk's
  table (cooling, signed out, refused, not offered, no full access, not admitted, not listed, kept for conversations, the pick, a
  handed account, all out → the wait, the first that frees in order); the second opinion's passes over choices and the label;
  `HelperChoice`, `PartnerChoice` and the brainstorm's voices; the suggestion's census both ways.
- **Census**: every start names its actor through a required parameter of `HoldAsync` and the chat runner's start, so a new start
  without one does not compile, plus a table test of the brief's 13 starts (C, O); `PipeCensus` (`ACTORS` `Open`, `SET_ACTOR`
  `Person`); `HelpCoverageTests` and the room's goldens; the home twins.
- **Stub**: ACP: model before effort, both before the first prompt; an unoffered model stops before any prompt and the walk
  continues; a resume re-applies. Pipe: `--model` and `--effort` exactly when a choice names them, none where the person's
  command does.
- **Invariants**: two choices, one partner digest; the record's four fields answered on loopback only and absent from the feed,
  the mirror and a sync (the `profile` tests' twin).
- **Words**: `i18n:check`, `names:check -- --strict`, the glossary's new terms; stories at the frame's three widths in both
  languages (D185 R6).
- **Installed**, distinct from any story: the rows' own proofs below. Before a suggestion changes, the batch after the owner's
  press is compared with the week before it (row f's line, D174's outcomes: first-run gates, *must* findings, landed or sent
  back), never by price or benchmark alone.

## 14. The rows, in build order

Each lands alone, behind the one before, with its words in both languages and its proof (D187).

| Row | What lands | Proof |
|---|---|---|
| **MODELROLE1a — what each door accepts, measured** (tools, stubs) | On `claude-agent-acp`, `codex-acp` and dsh: `set_config_option` for `model` and `thought_level` at a driven-shaped open, before the first prompt, the values offered, combined or apart, and whether a turn reports its model. On the pipe door: `claude -p --model <alias>`, `--effort` (with `max`), an unknown model's words, and `effortLevel` in the spawn file as the fallback. An evidence document; the ACP and pipe stubs speak the measured shapes | The evidence on the owner's install; the stubs' tests |
| **MODELROLE1b — each session says its actor and what ran** (driver, service, web) | Every start names its actor (required parameter); the record's four fields; *Details*' rows and the parts fold's model; the usage history's `actor`; `actor.ran`. Behaviour unchanged: every choice `before` | The 13-start table; the loopback-only and no-sync tests; *ran* from each stub, *not reported* where a door says nothing; stories; on the install, a driven session's *Details* naming the model Claude reported |
| **MODELROLE1c — Build runs its choices** (driver, modules, web, terminal) | `actors` read and written through; `ActorChoices.Judge`; the choice-major walk; the carriers; passes and the in-start `not-offered` move; `agent-offers` in the twins; `ACTORS`, `SET_ACTOR`; `daoris driver actor`; AI features' *Actors* with Build's editor; the note, chip and wait; the Agents fold's line | The unit, walk, stub and census tests above; on the install, a Build whose first choice names a model the agent does not offer runs its second and says so on the head and in the note |
| **MODELROLE1d — Intake, Ask Daoris and Chat run their choices** | Starts 5, 10-13 take their actor; the two cards fold into rows; `driver intake|helper` write a one-choice list; full access passes dsh; the pick overrides; a go-on re-applies the last composer change | *Absent means off* kept; `no-full-access`; the composer opening on the actor's model; `HelpRoomDoorsTests`; on the install, a chat on *Chat* · sonnet shows sonnet beside its composer and in *Details* |
| **MODELROLE1e — second opinions, helpers, partners and brainstorms take their models** | §4.4's composition; the label naming the model; the brainstorm's voices; the hand-over offer naming the next choice | `ReviewerChoice` over choices (another maker first; the same agent on a stronger model labelled *fresh*; empty `reviewers` reading the actor); two choices one digest; a cooling Build offering the hand-over under `ask`, taking it under `on`, waiting under `off`; on the install, a second opinion's *Details* naming Codex's reported model |
| **MODELROLE1f — each actor's use shown** | The 7-day line from the usage history and `actor.chose`; absent stays absent | The sum's table; stories; the install's line against the usage history for a day (`tools/usage-report.mjs`) |
| **MODELROLE1g — Daoris suggests choices** | `ActorSuggestion` and its census; the card; `actor suggested [--use]`; agents not set up left out | The census both ways; the card's tests and stories; on the install, **the owner's press**, D191 point 3's yes, with row f's week before it as the baseline |
| **MODELROLE1h — Ask Daoris proposes choices** | `actor_propose`, `HelpActorProposals`, the revision check, the room's *Actors* facts and door | `HelpCoverageTests`; goldens; a stale card refused; a judged refusal at *Apply*; full access running the verb meeting the window's card (`PipeConfirmations`); on the install, Ask Daoris answering *why did builds fall back?* and its card applied |

**Filed with this design, each behind its own measurement or design:**
- **i. Exact build by a quest's weight**: a publisher (the person, the intake, an agent whose pack names the change) may declare
  `weight: exact`; the receiving machine starts it as an *Exact build* actor, suggested after D174's build-exact row. The quest
  carries no model. Filed after a batch of row b's records; the brainstorm synthesis's own choice is measured in the same batch.
- **j. Orientation as a start**: a scout before a Build, handing a pack from the knowledge store (the MODELROLE1 row); its actor
  named then, suggested after D174's orient row. No scout on a valid pack.
- **k. Cool-offs scoped to a model**: read `seven_day_opus` and `seven_day_sonnet`, and cool a model's limit as account plus model.

## 15. Not built, and not measured

Every door's acceptance (row a); whether `codex-acp` reports a turn's model; `--effort` on the installed Claude Code; the
documented precedence of `--model` over `ANTHROPIC_MODEL`, read and not observed; whether a full-access Ask Daoris uses the verb
rather than editing `driver.json` with its shell (the room tells it to; nothing prevents it, as for every setting today).

## 16. The reads

None yet. Codex reads this round under D191, and an independent Opus reader reads it as a foundation (D191's amendment of
2026-10-23). The steward checks each finding against the code and records the verdicts here.
