# Actors: Daoris chooses the model for each kind of work it starts, from the choices the person put in order (MODELROLE1, proposed, round 3, 2026-10-23)

**Round 3** (2026-10-23) answers round 2's two reads (`docs/2026-10-23-actors-reads.md`: Codex's, 11 findings, 10 blocking;
the Opus foundation read, R2-B1 to R2-B6 blocking and 13 not). Every finding was checked against the code at `af642e750` with
the reads merged, and every blocking one held; §16.2 tables each. What moved: the reservation in the count's own terms, one
count per start (§4.1); the paths after the open for every start that is not a driven first start, each named, and a
conversation whose agent has no account left going on with its actor's next choice, by the owner's direction of 2026-10-23
(§3.5, LIMITGOON2); a same-session retry that restores the opening's values (§3.3); `asked` as tagged values (§9.3); a model
change clearing the effort override (§4.2); the confirmed copy **dropped** (§8.2); inheritance kept on every change of `adapter`
(§4.0); row c carrying Chat's and Brainstorm's compatibility readers (§14); `no-posture` from each start's own access judge and a
`no-connector` pass (§4.1); the update drain turning Intake *off* (§6.4); the acceptance words true for Brainstorm (§6.3).
Round 3 settles R2-B1 with one addition beyond its named change, so by the Opus read's own condition the walk's contract wants
another foundation read (§16.3). D205 stays proposed.

**Round 2** (2026-10-23) answers round 1's two reads (`docs/2026-10-23-actors-reads.md`: Codex's, 15 findings, 12 blocking;
the independent Opus reader's foundation read, B1-B8 blocking and 12 not). Every finding was checked against the code at
`59a2db074` with the reads merged and held; §16 tables each, taken or answered, and where. What moved: one effective-choice
model for every actor (§4.0); the walk's per-start passes and its reservation (§4.1); refusal once the record is open (§3.3); the
carriers on every run (§3.2); the tier-that-ran record (§5, §9.3); the person's press on every write path (§8.2); Brainstorm's
choices as voices (§4.4); `handOver` untouched (§4.2). D205 stays proposed.

**Standing:** proposed with D205, round 3 (round 1 below as written, amended in place where §16 says): the synthesis of the three independent proposals in
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
| Brief §3.1: the adapters' "No model is named (D24)" at `Adapters.cs:1870`, `:1962`, `:2074` | At `:1733` (`claude-agent-acp`), `:1870` (dsh), `:1962` (`codex-acp`), `:2074` (the pipe door's class remark, "No model is ever named") and `:2164` (its chat, "here either"). *Round 2: round 1 said none at `:2074`, which was wrong (Codex 1, Opus §1)* |
| D205: "every session runs its account's own model" | Round 2: today's starts leave the model to the tool's own cascade, which a person's `commands` entry (`Adapters.cs:2090-2093`, `:2180-2183`), a repository's `.claude/settings.json` and `ANTHROPIC_MODEL` (`AgentsCommand.cs:821-822`) all reach, not the account's settings alone (Codex 3) |
| Brief §3.4: `WalkAsync` at `HarnessRoster.cs:1047-1099` | It begins at `:1047` and runs to `:1283` |
| Brief §4, Opus §1.4: `DriverMeetings.cs:26-27` is `MeetingTurns ?? Unavailable` | `:26-27` is the class's remark; the code is `:249`, `Unavailable` at `:320` |
| Opus §6.3: `DriverChoicesCommand.cs:108-109` holds `intake` and `helper` | The verb list is `:154-155`; `case "helper"` `:540` (`WithHelper` `:545`), `case "intake"` `:556` (`WithIntake` `:562`) |
| Opus §6.1: `driver.json` is written under the keep lock at `DriverConfig.cs:866` | `:866` is the parse error; `Save` is `:914`, the lock `:918`, `JsonKeeping.Kept` `:923` |
| Opus §2.8, §5.3: `PipeCensus.cs` | It is `Modules/Platform/PipeCensus.cs`. Every `SET_*` is `Person` (`:234-245`) except `SET_STANDING_CONFIRMATION`, which is `Window` (`:269`) |
| Claude C3: the ACP door's `_meta.claudeCode.options.settings` carries the rules, so it can carry `model` | It carries a **file path** (`Adapters.cs:1794-1795`, `:1809`), the same file the pipe door's `--settings` names (`:2222-2226`), written by `SpawnSettings.Write` (`Agents/Permissions/Permissions.cs:507-588`: `permissions`, `autoMode.hard_deny`, `hooks`; no model). A `model` there is the tool's settings tier, which `ANTHROPIC_MODEL` outranks (§3.2) |
| Claude C3, Codex §6: a partner's admission digest covers "the settings' digest", so a model in the settings changes or must join it | `PartnerConfiguration.Digest` (`Conversations/Domain/PartnerConfiguration.cs:56-72`) hashes `posture=`, `guard=` and the sorted `allow`, `ask`, `deny` and `hard` lines only. It never reads the settings file's content, so neither carrier of §3 changes it |
| Brief §3.2: Codex and dsh are refused by name | `AgentSettings.cs:242-245` refuses any agent whose toolchain declares no `SettingsFile`; neither is named |
| Opus §2.4: Claude's usage answer carries model-scoped weeks Daoris does not read | The recorded answer at `ClaudeUsage.cs:41` holds `seven_day_opus` and `seven_day_sonnet`; they are **recorded in the usage history** (`Seen`, `:106`, `:113-129`, every window with its scope, use and reset) and **unused by the walk and cooling**, whose projection at `:78` reads only `five_hour` and `seven_day`. *Round 2: round 1 said unread (Codex 2)*; in the one recorded answer both are `null` (Opus §1) |
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
- **A per-model cool-off where the usage read gives one.** Right in aim, early in time: the model-scoped windows are recorded in
  the usage history but unused by the walk and cooling (`ClaudeUsage.cs:78`, `:106`). Kept as row k.
- **Keeping old builds sensible** by writing the old keys in step. Kept for the scalar keys only (§6.3).

**Codex's.**
- **A refusal after work is dispatched is not an availability signal**: no replay on another model after a content refusal, an
  uncertain send or a half-done turn (X §2). Taken whole (§4.3).
- **Requested, acknowledged and reported are three facts**, and a successful `set_config_option` is not proof the model ran
  (X §4). Taken into *Details* (§9.3).
- **A stale card never overwrites a newer choice**: Ask Daoris's patch carries the revision it read (X §5). Taken (§8).
- **Full access does not invent spending** (X §5). Taken for every door of Daoris's own, each the person's press (§8.2); a raw
  edit of `driver.json` by a full-access shell is D183's unenforced boundary (round 3, Opus N11).
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
- **dsh has no full posture**, so a full-access Ask Daoris passes it (`no-full-access`, round 2's `no-posture`). Checked: `DshAdapter` declares no
  `FullPosture` (`Adapters.cs:1275` defaults null; Claude's two doors `:1757`, `:2192`, Codex `:1985`). Taken (§4.1).
- **Measure each door first.** The only proposal whose first row measures what the doors accept before anything rests on them.
  Taken as row a.

## 2. The actors

### 2.1 The words

| Term | en | zh | Means | Avoid |
|---|---|---|---|---|
| actor | actor | 角色 | A kind of work Daoris starts an agent for, with the person's choices of agent, model and effort for it, in order | en *role* (D203's co-worker role and D174's development roles), *profile*; zh 演员, 岗位, 人设, 执行者 |
| choice | choice | 选择 | One entry of an actor's order: an agent, and its model and effort, or the account's default for each | en *fallback*; zh 选项, 备选 |
| account default | account default | 账户默认 | What an agent runs when a choice names no model or effort: the account's own settings and today's cascade | en *inherit*, *its own*; zh 默认模型 |

**Round 2, the glossary entries as they will be written (Opus §4).**
- *choice*: `avoid.en` holds *fallback* alone. *option* and *range* go in its `why` (*option* is an agent's config option and a
  workflow node's `options`; *range* is this design's word), not in `avoid`: `names-check.mjs:199-201` holds `avoid.en` against
  every label, and *History options*, *change a conversation's option* and *No range to read* use them rightly. Its `match` is
  the actor editor's and the chip's keys only (or `pair: false`), since 27 English strings say *choice* in its ordinary sense.
- *actor*: its entry says a co-worker's role (D203 point 7) is named by its actor (第二意见, 助手, 搭档), so 角色 names one thing.
- *Build* / 开发 enters as an actor's name only, matched on its own label key, never `\bbuild\b`: the chrome says *build* 45
  times, mostly 构建's build output, and a word match would hold each to 开发.
- *your pick* is 你指定的, not 你的选择, which a reader cannot tell from a choice in the order; *choice {{n}}* is 第 {{n}} 个选择,
  the measure word kept (five units, the `status` edge; 选择 {{n}} if `names:check -- --strict` refuses it).
- Intake's and Ask Daoris's *Off* keep their own words on the folded rows: *Off — declarations only* / 关闭——仅按声明, *Off —
  starters only* / 关闭——仅入门提示 (`settings.ai.json:11`, `:31`).

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
gains an options callback (`ChatRunner.cs:2189`). `SetConfigOptionAsync` (`Acp.cs:527-539`) is reused with the one change
below (*What the agent acknowledged*).

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
  (`modelSettings.<model>.effortLevel`, `AgentSettings.cs:7`, `:25-30`; `AgentsCommand.cs:815-818`), so the choice could quietly
  not hold, and `max` cannot ride a file at all. **Round 2 (Codex 11): no file fallback.** A named effort is carried only by a
  mechanism row a measured to hold against a conflicting saved per-model effort. Where the installed version has no `--effort`,
  the pipe door offers effort as *account default* only, and a choice naming one runs at account default effort, said before
  dispatch in *Details* (*asked high · not carried*), never presented as taken.
- *The environment's effort* (Opus N1). Claude Code's `CLAUDE_CODE_EFFORT_LEVEL` may rank above `--effort`; row a measures it.
  Where it does and the start's environment sets it, nothing is appended for effort and *asked* says *your environment*, as a
  person's own command does.
- *A person's own command.* Where `commands[claude-code]` (`Driver.cs:2583`) already names `--model` or `--effort`, nothing is
  appended for that flag: the person's command is more specific (O). The record's *asked* says *your command*. X's refusal is
  rejected: it would stop a start the person deliberately configured. A command that sets `--model` inside a wrapper script
  cannot be seen; Daoris's appended flag then wins, and the editor's footnote says so (Opus N11).
- *A plugin-declared pipe harness* has no carrier: it offers *account default* only, without the free field (Opus N10).

**On every run, not the first spawn alone (round 2, Opus B5).** A native session's later runs start outside `HoldAsync`:
the go-on through `GoOnStart` (`DriverContinuations.cs:899-906`), which re-hands the settings and servers by hand, and the second
try through `AgainAsync` (`:1028-1040`), which reuses the run's start. So the asked choice is not a caller's flag: it rides the
target, `SessionTarget` and `ChatTarget` gaining `Asked { model?, effort? }`, and each native prepare, `Prepare` (`Adapters.cs:2088`),
`PrepareResume` (`:2123`) and `PrepareChat` (`:2172`), appends it itself; `AgainAsync` inherits it from the run it repeats. Every
protocol open and resume sets it the same way (`Acp.cs:940-944`, the resume's posture near `:2620`). The census holds the target's
field and the three prepares; the pipe stub proves a go-on run carries `--model`.

**What the agent acknowledged (round 2, Codex 10).** `SetConfigOptionAsync` (`Acp.cs:527-539`) assumes the requested value
locally when the answer carries no option list (`:533-536`), and does not compare a returned list's current value with the
request. So the actor's open uses it with one change: it reports whether the list was **returned** or **assumed**. A returned
list whose current value is the requested one is *acknowledged*; a returned list with another current value is a refusal of that
choice on that account (§3.3); no list is *acknowledged: not reported*, and the work goes on, *ran* being the agent's word that
settles it (§5). Holding on a silent answer would make an agent that never returns its list unusable; passing on a contradiction
keeps a choice from being shown as taken when the agent said otherwise. The composer's own use is unchanged. **Round 3 (Opus
N5):** row a measures what an accepted change returns as the current value. Until it has, a returned value other than the one
asked counts as a refusal only where it is another value the list offers, so an agent that answers a normalised id (`opus` asked,
`claude-opus-5-5` returned) does not pass an account.

**Why the session level, given D98's cascade.** The actor is the person's deployment choice for that kind of work, D98's own
"for an agent a person runs, the deployment is the person". A model the person named for Build should hold for every Build,
whichever repository it runs in. *Account default* is still there for the person who wants the repository's or the account's
own setting to decide.

### 3.3 When the agent does not offer the choice

**Round 2 (Opus B1, B2; Codex 8, 9).** Round 1's in-start move rested on two things the code does not do: the options are
first seen at `session/new` (`Acp.cs:940-942`), **after** the record opens naming its agent, version and profile
(`Driver.cs:2108`, before `Prepare` at `:2320`), with the account already counted (`HarnessRoster.cs:1411-1415`); and what an
agent offers and accepts is its **account's** (both Claude doors run on `claude-code`'s accounts, `CLAUDE_CONFIG_DIR`,
`Adapters.cs:1837`; each account its own home, `HarnessRoster.cs:1244-1254`). So offers and refusals are judged per account, and a
refusal after the record is open takes one of two named paths.

- **Judged before a spawn, per account.** `agent-offers` (§9.2) is keyed by agent, version **and account**. Where an account's
  reading lacks the choice's model, that **account** is passed for that choice in this start (`not-offered`), not the choice. The
  choice is passed only when every account of it is passed. A reading passes an account only while its version matches and it is
  under a day old; an older or other-version reading is a hint the editor shows, and the account is tried, its open refreshing
  the reading. So a negative reading never prevents the opening that would correct it for longer than a day, and an entitlement
  that changes without a new version is read again within a day.
- **Caught at the open, the record already open.** A model not among `session/new`'s options, a `set_config_option` the agent
  refuses, or one whose returned list names another value (§3.2) is that account's `not-offered` or `refused` reading, kept. No
  prompt has been sent, no strike is counted (D125). Then the walk is asked again with that account passed for that choice, and
  **its answer decides**:
  - the **same agent on the same account** (a later choice of that agent, where no other account of the refused choice remains):
    `set_config_option` again on the open session, no respawn; the record's `choice` and `asked` move through their later door
    (§9.3), and the opening note says both. **Round 3 (Codex 2): the session is reused only where the later choice establishes
    its whole configuration.** The refused attempt may have changed the session (a returned list naming another value is a
    refusal that moved it). So each category the attempt touched and the later choice leaves at *account default* is first set
    back to the value `session/new` returned for it (kept by `KeepOptions`, `Acp.cs:940-942`: the measured opening default);
    a category the later choice names is set to that value. Where the agent does not return the value asked for, either way,
    that account is passed for the later choice too and the next bullet applies. Test: `session/new` on `m0`, `m1` answered
    with `m2`, the later choice at account default: the session is set back to `m0`, and an agent that refuses that ends the
    record;
  - **anything else** (the same choice on another account of the agent, or another agent): for a **driven first start**, the
    opened record **ends** with `choice-not-offered` (no strike, the reading kept, the count released, §4.1), and the next look
    starts what the walk now answers. Every other start after the open takes §3.5's path for its kind.
  The readings are written before the refused record ends, so the next look judges them (round 3, Opus N4); a `refused`
  reading is keyed by the model refused, and a door that rejects `set_config_option` as a method is a reading of the agent
  and version, not of each account, so no account opens and ends a record once a day to learn it again.
  This is Opus's B1(a) and (b) with one narrowing: B1(a) moved the open session to the agent's next choice whenever one existed,
  which would take choice 2 on account-1 while choice 1 could still run on account-2, against B2's own rule and §4.1's order. The
  one rule, *the walk answers as if the refusal had been known before the spawn*, keeps both.
- **The pipe door has no offer list, and its prompt is in the arguments** (`-p`, `Adapters.cs:2092`), so a refusal can only
  arrive after the prompt was handed. A model refusal there is recognised only by row a's measured words, on a turn with no
  output and no tool use; it is **the one named exception to §4.3**, kept as that account's `refused` reading (so the next start
  passes the account without spawning), ending the part with no strike, the next look taking what the walk answers. A
  pipe chat's first turn and a pipe go-on run take §3.5's paths.
- **An effort not offered** runs at account default effort, and *Details* says so (O). A model decides what runs and what it
  costs; an effort is a dial within it, and running without it is today's behaviour. X's *the option is unavailable* is
  rejected for effort alone.
- **One option for model and effort together** (O §2.1, kept in round 2 for row a, Opus N9): where a door offers a single
  option whose values are model-and-effort pairs, the editor offers those values as models with no effort menu.

### 3.4 Rejected carriers

| Carrier | Why |
|---|---|
| The account's `settings.json` | Shared by every actor on the account; two starts race on the tool's own file; a repository's settings outrank it |
| `ANTHROPIC_MODEL` at spawn | D98's rejection stands; Claude-only; inherited by every child the session runs |
| `model` in the spawn's settings file (C) | The settings tier: `ANTHROPIC_MODEL` outranks it, and it is Claude-only |
| `_meta.claudeCode.options.model` | Claude-only, precedence unmeasured; `set_config_option` does it for every protocol agent |
| The tool's `--fallback-model` | Switches models inside a turn, where Daoris cannot say why; the turn reads `mixed` |
| Writing `CODEX_HOME` or `DSH_HOME` | D98's boundary |

### 3.5 After the open, for every start that is not a driven first start (round 3)

Round 2's paths after the open (§3.3) were written for a driven first start, whose quest is still `Open` and whose next look
plans it again. Five other kinds of open cannot take that path (Opus R2-B1; Codex 2-3): a resume lives in its own account's
home, a conversation has no next look, a pipe chat's first words go on stdin, a go-on run's words are handed back as a failed
turn's, and the curve move picks its account without the walk. Each is named here. **The one rule still holds for each:** the
start goes on as if the refusal had been known before its spawn; only *where* it goes on differs.

**(a) A continuation keeps its asked choice.** A resume, a go-on run, a carry-on, a briefed move, a hand-over to another account
of the same agent, the curve move and LIMITGOON1's move of a conversation to its agent's next account each take the record's
`asked` (then `override`, §4.2) and walk **only that choice's agent's accounts**, each judged by its reading for that model. A
running line never moves to another choice by this path (D163); the one exception is (e).

- **A resume** runs on its record's own account (`ResumeAsync`, `HarnessRoster.cs:918-990`, `ChooseAsync(chosen: named)` at
  `:968`). `ResumeAccount.Judge` gains the reading: an own account whose reading refuses the asked model is an account that
  cannot carry the words, so the resume goes through `ElsewhereAsync` (`:973-980`) as an own account signed out does today, its
  words carried to a **new** record on another account of the same choice (D175 point 3), the walk's `passed` holding the own
  account. **The old record is left as it was and never ended `choice-not-offered`**: it holds work and the person's words.
  Caught at the open instead (the reading stale or absent), the resumed process stops before its prompt, no strike, the reading
  kept, and the same start takes that `ElsewhereAsync` path. A conversation's resume (`walks: false`) is refused there in the
  wait's sentence, its words waiting, as today.
- **A pipe go-on run** whose silent turn carries row a's measured model refusal is TURNFAIL2's failed turn
  (`DriverContinuations.cs:934-940`): its words are handed back, no strike, the account's `refused` reading kept, and the next
  look's resume judges the reading before any spawn, taking the resume's path above.
- **A carry-on** (`CarriedOnAsync`, `:991-995`) walks the asked choice's accounts with their readings; a new record it opened
  that is refused at the open ends `choice-not-offered`, and (c) plans it again.
- **The curve move** (`CurveMove.cs:62-127`) never takes an account that does not offer the chat's model:
  `SwitchCandidates` (`HarnessRoster.cs:1974-1994`) judges each candidate's reading for the record's `asked` model (the
  `override`'s where one is kept); a candidate whose reading lacks it is not ready. Test: one candidate not offering the model is
  not moved to.
- **No account of the asked choice left**: a driven line waits in the wait's sentence (§4.5), and D163's offer names the actor's
  next choice, taken only by the person's press (§4.2). A conversation takes (e).

**(b) A conversation's first open re-asks within the same press.** `StartAsync` (`ChatRunner.cs:390`, record opened at `:463`)
and `PlaceHelpAsync` (`:588`, `:606-620`) own the start until it is dispatched or exhausted, carrying its effective order, its
`passed` set and the person's queued first words (Codex 3's pending start). Refused at the open, they end the refused record
`choice-not-offered`, re-ask the walk with the account passed, and open its answer **in the same call**: one press, the window
following the new record, its note saying both. Nothing left: the press is refused in the wait's sentence (§4.5) and the first
words wait on the refused record. A pipe chat's measured refusal on its silent first turn (`Adapters.cs:2181`) does the same,
carrying the first message.

- **Interrupted between the two records** (the app closed after the end, before the next open): the refused record has ended
  with its words waiting. **Its go-on is a fresh start of its actor**, not a resume: `ChatGoOn` (`ChatGoOn.cs:337-372`) reads the
  `choice-not-offered` end and starts the actor's walk with that record as `from` and its waiting words as the first message,
  rather than resuming the record's adapter, since no conversation began there. Every other ended chat goes on as today.
- Test: a Chat whose first entry is refused at the open: one press, one running conversation, two records; the same with the
  app stopped between them, its *Go on* starting the walk's answer with the words.

**(c) `choice-not-offered` is an end the planner reads.** It is `failed` with the end code `choice-not-offered`, so `Plans`
(`Planner.cs:1377-1389`) plans a `Taken` quest whose last record ended so, and strikes leave it out. `LastRun` skips such a record,
so `CarriedOnFrom` (`:1341-1343`) carries on from the record before it, never from the empty one. Its fresh tree is let go as a
session that never ran (`Driver.cs:2113-2117`). `Facts` leaves it out of `Running` and `LastStarted` (§4.1). Test: a carry-on
whose record ended `choice-not-offered` is planned again from the record before it.

**(d) A pick or a handed agent whose account refuses the model** (Opus N2). A pick names its agent and account, never moved
(`HarnessRoster.cs:1065`, `:1293`): its model is the actor's first entry for that agent, else account default; that entry
refused on the picked account, the next entry for the same agent on the same account is taken, as (a)'s same-account move; with
none, the pick is refused with the reason (*account-2 does not offer opus*), and nothing else runs in its place. A handed
quest's named agent walks its own accounts as (a).

**(e) A conversation whose agent has no account left goes on with its actor's next choice** (the owner's direction of
2026-10-23, LIMITGOON2; beside LIMITGOON1). Where every account of a Chat's or Ask Daoris's conversation's agent is out for its
asked choice (cooling, signed out, or refusing its model), and LIMITGOON1 therefore has no next account to move it to:

- **Its line goes on, without a press, as a new part**: a new record in the same line (`from` the last), on the answer of the
  actor's effective order walked **afresh, as at a start** (§4.1, choice-major), with every account of the out entry passed.
  Since every earlier entry was passed when the line last started, that answer is its next choice; one that has freed since is
  taken first, the order being a preference and not a ratchet (§4.2). An entry on the same agent with another model runs where
  its accounts are not the out ones' reasons (a cool-off is the account's, so a cooling agent's other entries pass too).
- **What it carries**: on another agent, Daoris's brief of the conversation (D163 point 1, never the transcript) and the words
  the failed turns held; on the same agent, LIMITGOON1's carrying. It is said once in the conversation, naming the agent and
  the choice it moved to (*Went on with Codex · account default, Chat's choice 2: Claude Code's accounts are all cooling until
  14:05*), and the page shows one conversation.
- **Never a maker the person did not allow**: only the actor's entries, each judged by the start's own access (`no-posture` by
  `HelpAccess.For` for Ask Daoris, §4.1). A conversation on the person's **pick** does not go on elsewhere: it waits in the wait's
  sentence, and D163's offer names the next choice for the person's press. An actor *as before* has one entry, so it waits.
- **Nothing left**: the conversation waits in the wait's sentence with its words held, as LIMITGOON1's last account does.
- **Why conversations and not driven lines.** The owner's direction is for conversations, where the person is present and the
  line is the thread they are in. A driven line keeps round 2's rule: the offer, taken only by the person's press under every
  `handOver` mode (Opus B8). That rule is not reopened here.
- **What it changes**: D163's *Rejected: automatic hand-overs by default* gains a dated exception, a conversation at a limit
  with no account left going on to its actor's next choice, the person's order being their prior consent (§12). It is a limit,
  not a refusal after dispatch, so §4.3 holds.
- Test: a stand-in agent with every account cooling, the conversation going on with a second stand-in agent and the held words,
  said once; one with no other entry holding with its reason; a conversation on a pick waiting with the offer.

## 4. The walk, fallback and what is said

### 4.0 One effective order, for every actor (round 2)

Round 1 had three meanings of an actor's list (an order, a model per agent, voices) and two of *empty*, and let a rule's agent run
with no index (Codex 4-7; Opus B4, B6, B7). Round 2 has one function, `ActorChoices.Effective(actor, config, fence, pick)`, that
every start, the editor's preview, the opening note, the terminal and Ask Daoris's facts call. It answers the **effective
order**: entries `{ agent, model?, effort?, from }`, where `from` is what the record keeps as `choice` (§9.3).

**An actor's stored state is one of three, and nothing else** (`driver.json`'s `actors`, §9.1):

| State | Stored | Means |
|---|---|---|
| *as before* | the actor absent | read from today's key or rule, at account default (§6.1); `from` is `before` |
| *off* | `[]`, on Intake and Ask Daoris only | no start, as today's *absent means off* (`DriverConfig.cs:411-434`); `Judge` refuses `[]` elsewhere |
| *set* | one to four choices | the person's entries; `from` is the entry's number |

**The fence decides who; the actor decides the order only where no fence names agents, and the model always.** A *fence* is a
rule's or an ask's agents (§4.4). An agent the fence names takes, as its alternatives in order, the actor's entries for that agent;
where the actor has none, it runs at account default with `from` **`rule-default`**, and the editor's preview and the opening
note name it so (Codex 5's first fix: a scoped rule may supply an account-default entry; requiring an actor entry first would stop
today's rules on the upgrade).

| Actor | Who, in what order | Disabled |
|---|---|---|
| Build, Intake, Ask Daoris, Chat | the actor's entries | Intake and Ask Daoris *off* |
| Second opinion | always a rule's `reviewers` or the ask's agent; D155's passes; **an empty `reviewers` does not exist** (`OpinionRules.cs:179`, `NoReviewers` `:140`) | a rule's `false` (`OpinionRule.None`, `IsNone` is `Reviewers.Count == 0`, `:24-27`) or no rule: no opinion; the actor never enables one |
| Partner | the ask's named agent, admitted (D199) | as today |
| Helper | a `helpers` rule's agents where it names any; else the actor's entries where *set*; else `HelperChoice.Candidates`, *as before* | as today |
| Brainstorm | **voices**, not an order: the request's named agents, else the policy's, else the actor's entries, one voice each, else `[adapter]` *as before* | as today |

So for Second opinion and Partner the actor is **a model for each agent a rule or an ask names**: their rows, editors and the
suggestion's card say exactly that (*Models for the agents a rule or an ask names*), with no order and no *another maker first*
decoration (Opus B6). A `pick` (D130 point 4) overrides agent and account for its start, the model being the actor's first entry
for the picked agent, else account default (refused on that account: §3.5(d)); a handed quest's named agent is `from`
**`handed`**, its model taken the same way. **Helper's order is D155's maker passes** (round 3, Opus N1): `HelperChoice.ChooseAsync`
walks its candidates through `ReviewerChoice`'s passes (`HelperChoice.cs:30-37`, `ReviewerChoice.cs:175-180`), so the actor's
entries are the candidates and another maker than the asker's still comes first; the preview says *another maker than the
asker's first*, the asker being known only at the ask. **The order a start walks is read once for that start** (Opus N3): a
`SET_ACTOR` that lands between the walk and a re-ask at the open does not change the order the re-ask uses.

**Inheritance, kept on every change of `adapter` (Codex 7, Opus B4(a); round 3, Codex 7).** Build, Chat and Brainstorm's last
fallback read the one key `adapter` (`Driver.cs:1672`; `ConversationRoutes.cs:48`, `:164`, `SessionsVerbs.cs:33`;
`DriverBrainstorms.cs:348`). **Every write that changes `adapter`** (setting or clearing Build, `driver adapter`, the proposal
door) first writes each of Chat and Brainstorm still *as before* as its explicit one-entry list of `adapter`'s agent as it stood,
at account default, in the same write under the keep lock, and the Save's sentence names them (*Chat and Brainstorm keep
claude-code; written for them*). Round 2 did this on the first write only, so clearing Chat back to *as before* reopened its
live reading of `adapter`, and a later change of Build moved it (Codex's set → clear Chat → change Build). Now a cleared Chat
reads `adapter` until the next write that would change it, which freezes Chat where it stood. *As before* for Chat therefore
means *Build's agent as it stands now, until Build next changes*, and the editor's *Clear* says so. Test: set Build, clear Chat,
change Build: Chat stays on the agent it ran, written for it. So no actor follows `adapter` across a change of it. Its
other readers, which want *this machine's Build agent* for a record with none (`BriefedMove.cs:111`, `ChatGoOn.cs:372`,
`OpinionGateLook.cs:368`, `SessionGroups.cs:837`, `Driver.cs:622`, `SetupPress.cs:52`, `AgentsRoutes.cs:62`, `:287`,
`HelpRoomMachineNow.cs:15`), keep reading it, and write-through keeps it Build's first agent. A test holds that setting Build leaves
Chat's and Brainstorm's effective order unchanged unless the change names them. **Where Build runs on more than one agent**
(round 3, Opus N12), the opinion wire's *same agent* display, which judges against `config.Adapter` alone
(`LinesRoutes.cs:155`, `DriverRoutes.cs:620-622`, `HelpSettingProposals.cs:730`), judges against Build's agents in its effective
order, so a reviewer on Build's second agent is labelled the same agent too.

### 4.1 At a start: choice by choice, each choice's accounts before the next

All three agree, and so does the reason: the person's order is about who does the work, and an agent's accounts are
interchangeable seats of one set (D197 point 1). Taking a later choice while a seat of an earlier one is free spends against
that order.

```
passed = {}                                   # this start's (choice, account) passes: not a cool-off, not counted
for each entry of the effective order (§4.0):
    pass it, coded, if it cannot run here at all:
        unavailable (not installed) · no-posture (§4.1, B3) · no-connector (§4.1, round 3)
        · not-admitted (a partner, D199) · not-listed (a fence)
    else that agent's account walk (D197's order, cool-offs, sign-ins, keep, curve, the cap), with `passed` excluding
        the accounts already passed for this entry, and each account's offer reading judged first (§3.3):
        counted → open on this entry, this account (the start's one count, §4.1)
        an account not-offered or refused → add (entry, account) to passed; the walk goes on within the entry
        held    → pass the entry with the walk's own reason
no entry allowed → the wait (§4.5)
```

**The walk gains two things, and is otherwise unchanged (round 2, Opus B2, Codex 9).** Round 1 called it unchanged; it is not,
and these are its contract:

- **A per-start `passed` set** of (entry, account), handed to `WalkAsync` (`HarnessRoster.cs:1047`), which today takes no
  exclusion. It is the start's alone: never a cool-off, never stored, gone when the start ends. An entry is passed only when every
  account of it is. The readings are judged **inside the walk**, so every caller of `WalkAsync` takes them: `SelectHandedAsync`,
  `CarriedOnAsync` and `ResumeAsync`'s own-account pass (`HarnessRoster.cs:895-905`, `:968`, `:991-995`; round 3, Opus §1.3).
- **One count per start, owned across reselection (round 3, Codex 1, Opus R2-B4, agreed).** Round 2's *reservation committed at
  the first prompt or released* named a mechanism the count does not have. The count, as it is: the walk counts the account as it
  returns it (`Number = counts ? Chose(...) : 0`, `:1411-1415`), an entry in `_chosen` until the next look whose mark covers it;
  from that look on, the count is the record itself (`Look`, `:1651-1659`), and a look begins only once the last look's starts have
  opened their records (`Driver.cs:891-897`). `Facts` counts both (`Running`, `:1688-1693`), and the rotation ranks by it
  (`AccountRotation.cs:541`) and by `LastStarted` (`:555-575`). So, in those terms:
  - **A start owns one count**, from the walk's answer until its record ends or the start comes to nothing. *Commit* is dropped:
    nothing exists for it to do.
  - **Before the record opens, the count is taken back by `Unchose` whenever the start ends**: a hold or a refusal after the walk
    included, which today keep it (`Driver.cs:1775`, the posture hold; `:1796`, the tree lock's), as well as a cancellation and a
    probe lock's failure (`WaitedOutAsync`, `:1424-1440`, as today).
  - **After the open, the count is the record, and the release is the record's end.** `Facts` leaves a record ended
    `choice-not-offered` out of both `Running` and `LastStarted`, so the refusing account is not read as just used.
  - **The re-ask at the open carries the start's own count in.** It judges the alternatives with the start's own load taken out of
    its account's facts (its `_chosen` number, or its record where a look has read it), so the refused account's count does not
    bias the goal order; and it takes no count of its own. **A same-account answer keeps the count.** **Any other answer** first
    stops the opened process, which holds its account until then, so a concurrent probe still finds it busy (`:729`); then ends the
    record, which releases the count; and the start that follows (the next look's, or §3.5(b)'s in the same call) takes its own
    one count as any start does. A look that lands during the negotiation changes nothing: before the end, the record is counted
    either way.
  - `counts: false` is not used for this: it also skips the start's usage refresh (`:1076-1079`, `:1191-1197`).
  - Tests: a refusal, a cancellation, a hold after the walk and three alternatives in a row leave no count behind; the re-ask
    ranks a goal account without its own load; a probe during the negotiation finds the account busy; a look during the
    negotiation leaves one count; a `choice-not-offered` record is neither running nor last started.
- **Driven work never takes an account kept for conversations**, nor the person's own sign-in (D197 point 3).
- **A missing model entitlement does not cool an account** (X): it is that account's `refused` reading (§3.3), for this entry.
- **`no-posture` (round 2, Opus B3)** replaces `no-full-access`. A driven start on a repository whose permission prompts are
  `asking` or `unrestricted`, on an agent with no `AskingPosture` / `FullPosture`, is refused by `DrivenAccess.For`
  (`DrivenAccess.cs:106-112`) and held (`Driver.cs:1775`) after an account was counted, so a Build whose first entry is dsh or a
  plugin harness would hold at every look and never reach its second. `no-posture` is judged per entry **before** the walk, from
  each start's own access judge and nothing else (round 3, Codex 9): `DrivenAccess.For(...).Refusal` for driven starts, and
  `HelpAccess.For(mode, adapter).Refusal` for Ask Daoris (`Help/HelpMode.cs:83-104`). Round 2 said restricted Ask Daoris needs
  the asking posture; it does not: `HelpAccess.For` lets a restricted start run on any adapter, asking `HelpRoom.Posture` where
  offered, and refuses only a full-access start on an adapter with no `FullPosture` (dsh, `Adapters.cs:1275`). So no restricted
  start that runs today stops. Test: an *as before* restricted Ask Daoris on a harness with no asking mapping starts as today.
- **`no-connector` (round 3, Opus R2-B5)**, judged per entry before the walk as `no-posture` is. Only the protocol door hands a
  session the connector it takes its quest with (`Driver.cs:3760-3771`; the pipe door gets the plugins' servers alone,
  `SpawnServers.cs:55-67`), so the planner admits an unadopted repository only on that door (`Planner.cs:1729-1753`), judged
  today from `adapter`'s one wire (`Door()`, `Driver.cs:618-628`). With entries on both doors, an entry on the pipe door for a
  repository that has not adopted Daoris is passed `no-connector`; the planner's gate asks whether **any** entry of Build's order
  can drive the repository, not `Door()` alone, and the workspace set-up's use of `Door()` (`Driver.cs:1312`) asks the same. Test:
  an unadopted repository, Build's first entry cooling on the protocol door and its second on the pipe door, waits rather than
  spawning on the pipe; the reverse is planned.
- **Model-scoped cool-offs are row k** (O): `seven_day_opus` and `seven_day_sonnet` are recorded in the usage history
  (`ClaudeUsage.cs:106`) and unused by the walk (`:78`); row k makes the walk and `AccountCooling.Cool` read a limit naming a
  model's window as account plus model, so a later entry on the same account may run. Until then a cool-off is the account's.

### 4.2 Mid-session: nothing moves silently

- **A running session never moves** to another choice (D163). A conversation's line may, as a new part, by §3.5(e) alone.
- **A part that ends on a limit** carries on as today, on another account of the same agent (`CarriedOnAsync`,
  `HarnessRoster.cs:991-995`), with **the same choice re-applied** at the open, each account judged by its reading (§3.5(a)).
- **When every account of a conversation's agent is out**, its line goes on with the actor's next choice (§3.5(e), LIMITGOON2).
- **When every account of a driven line's agent is cooling**, D163's offer names the actor's next choice on another agent: *Hand over to
  Codex · account default, Build's choice 2?* **Round 2 (Opus B8): it is taken only by the person's press, whatever the
  repository's `handOver` mode.** `handOver` governs an *agent's* ask to hand its session over (`HandOverRules.cs:57-58`), and
  Daoris's own offer is "taken only by the person's press" (`handOverOffer.ts:24`); round 1's *`on` takes it* would have let a
  mode the person set for their agents' asks move a running line to another maker with no press. The offer stands where today's
  offer stands and only names the next choice; D163 is not widened, and §12's D163 note says only that.
- **C's silent move to the same agent on another model is rejected**: the cool-off is the account's (§4.1), so that model on
  that account is cooling too, and a move nobody was told of is what the brief §5 forbids.
- **A resume re-applies the record's asked choice** (`ResumeAsync`, `HarnessRoster.cs:924`), closing the brief §3.2's gap where a
  conversation going on lost an earlier choice, on every run (§3.2, *On every run*).
- **A composer change is kept as values, not read back from its note (round 2, Codex 12).** Today's change note keeps the
  option's and the value's display names (`ChatRunner.cs:1721-1728`), which need not be unique or reversible, and an effort-only
  change says nothing of the model. So each successful change also writes the record's machine-local `override`
  `{ model?, effort? }` (§9.3) as the option's category and value id, apart from the opening's `asked`, which never changes after
  its later door. **Round 3 (Codex 5): reconciled from the returned configuration, never merged blindly.** An effort-only change
  keeps the override's model. A model change can change both the effort menu and its current value (`Acp.cs:516-517`), so a
  successful model change sets the override's model and **clears its effort**, leaving the effort to what the new model gives,
  unless the same change named one; an earlier `{model: A, effort: high}` followed by a change to `B` is kept as `{model: B}`,
  never `B/high`. A resume and a briefed continuation apply `asked` overlaid by `override`, where an override's model also drops
  `asked`'s effort unless the override names one, so a resume sets what the person last left, in that order. Tests: two values
  with one display name, an effort-only change, a model change whose answer resets the effort, a restart, and an actor edit after
  the change (the conversation keeps its override).
- **A continuation takes its record's asked choice, not the actor as it stands** (Opus N5): a carry-on, a retry, a second
  opinion's recheck (which walks `[first.Adapter]`, `OpinionDeliverer.cs:213`) and the brainstorm's synthesizer take the record's
  (or the generator's) `asked`, and say so. Only the hand-over offer's *next choice*, and §3.5(e)'s next part, are read from the
  actor as it stands now. Each path after the open is §3.5's.
- **The next start of the same line** begins again from choice 1: the order is a preference, not a ratchet (C).

### 4.3 A refusal after dispatch is not a fallback (X)

After a prompt is sent, a content refusal, an uncertain send or a half-done turn is never retried on another model or agent. A
limit follows the cooling and carry-on rules above. The one named exception is the pipe door's measured model refusal on a turn
with no output and no tool use (§3.3). The brainstorm keeps its one bounded restart on the next account of the same
agent (`DriverBrainstorms.cs:603-609`) and gains no range retry.

### 4.4 Who may, and with what model

`opinions.reviewers`, `helpers` and `partners`' agents and the brainstorm policy's agents are a repository's or workspace's say
over **who may** do a job, classed as the person's re-wiring (`PipeCensus.cs:77-78`). They stay. The actor says **with which
model**, by §4.0's one function:

- **Where a fence names agents**, its order stands; each agent takes as its alternatives the actor's entries for that agent, in
  their order, else account default (`rule-default`).
- **Where none does**, the actor's entries are the order (Build, Intake, Ask Daoris, Chat; Helper with no rule list).
- *Second opinion*: `rule.Reviewers` (`ReviewerChoice.cs:211-214`) or the ask's agent (`OpinionGateLook.cs:366-370`) expands each
  named agent into its alternatives; the passes stand (another maker first, then an undeclared maker, then the same agent only
  where listed, `:175-180`). The same-agent label names the model: *the same agent, fresh: opus*. A model never makes a reviewer
  independent (D155 point 4). **Round 2 (Codex 4, Opus B6, agreed): round 1's *an empty `reviewers` reads the actor's choices*
  is withdrawn.** No such rule exists (`OpinionRules.cs:179`), and `false` is *off*; the actor never orders or enables reviewers.
  Migration fixtures hold that `false`, an absent rule and a malformed rule each keep today's behaviour.
- *Helper*: an agent the ask names (where the rule allows it) takes its model from the actor; with no rule list, the actor's
  entries, where *set*, replace *every agent of another maker* (`HelperChoice.cs:15-29`). `Candidates` never offers the asker's
  own family by default (`:24-27`); an actor entry may, as the person's list, and it is labelled *the same agent, fresh* (Opus N6).
- *Partner*: still named by the ask (D199); the actor supplies only its model and effort. Admission is unchanged (§11).
- *Brainstorm*: **its entries are voices that run together, not an order** (round 2, Codex 6, Opus B7, agreed). Today one pass runs
  per distinct agent (`DriverBrainstorms.cs:345-355`, launched apart at `:385-394`), and with nothing named one voice, `adapter`
  (`:348`). So where the request and the policy name none, each of the actor's entries is one voice, at most `MostAgents`
  (`BrainstormDesk.cs:63`), and `Judge` refuses two entries of one agent on Brainstorm, since they would be one voice. The voices
  are planned once and frozen for the brainstorm. **A voice that cannot run is dropped, never replaced** by another entry or
  agent: a substitute would hand the packet to a maker D176 point 2's disclosure did not name. A named or policy voice takes its
  model from the actor's entry for that agent, else account default. A voice keeps its own one bounded restart on its agent's
  next account (`:603-609`), that account judged by its reading for the voice's model as the walk judges it (round 3, Opus N13).
  The row, the editor and the suggestion's card say so (*all at once*, §7.1), and the card's Brainstorm line counts the voices
  it adds (§6.3).

**C's mirror is rejected.** The rules are scoped (a repository's, or the workspace's, brief §3.6), the actor machine-wide; one
cannot mirror the other. And a partner list is a permission: an actor that wrote it would widen who may write.

### 4.5 When no choice can run

- **Driven starts** hold on today's hold path; the quest's row line says: *Build waits: none of its choices can run now. Claude
  Code's accounts cool until 14:05; Codex is not signed in.* The wait is judged again at every look, and the first choice that
  can run **in order** is taken.
- **Chat and Ask Daoris** refuse the press where it was made, in the same sentence (D185 R3); a conversation already going
  waits in it with its words held (§3.5(e)).
- **No new kind in *What needs you*.** A cool-off asks nothing of the person (X's caution, `.claude/knowledge/person-waits.md`):
  a sign-in or a changed order is the person's act, reached from the sentence's doors (*Accounts*, *Edit*).

### 4.6 What is said when a later choice ran

1. **The opening note** gains an actor line beside *Opened on `account`: why* (`note.opening.why.*`): *Ran as Build's choice 2,
   Claude Code · sonnet: choice 1, Claude Code · opus · high, was refused by account-1 and account-2.* It rides the local
   conversation's note only, never the record's `Note`/`NoteParts`, which `SessionSync` feeds cleaned only of paths and the
   record's profile (`Core/Remote/SessionSync.cs:61-67`, `Core/Sessions/SessionNote.cs:28-51`); a no-sync test holds it (Opus N4).
2. **The head**: a `status` chip *choice 2* / *第 2 个选择* only when not choice 1, or *your pick* / *你指定的*.
3. **The machine log**: `actor.chose` (the choice, and each passed one with its code), `actor.passed`, `actor.ran`.
4. **The terminal loop**: *started #412 on claude-code (account-2) as build's choice 2: …*

Nothing goes to Notifications: a fallback asks nothing. A pattern shows on the actor's use line (§10).

## 5. The tier that ran

D24's "say the tier that ran", for agents: each session says its actor, the choice taken, what was asked and what the agent
reported.

- **What was asked** is the entry (or *your pick*, *as before*, *a rule's agent at account default*, *your command*, *your
  environment*, *not carried*).
- **What was acknowledged** is the agent's **returned** answer to `set_config_option` on the protocol door, never a value the
  setter assumed (§3.2); an answer with no list is *not reported*.
- **What ran** is always the agent's own word: the turn's `_meta["_claude/model"]` and `_meta.quota.model_usage` on ACP
  (`Acp.cs:2216-2220`, `:1379-1394`), `message.model` on the pipe (`StructuredOutput.cs:212`, `:332`). Absent: *not reported* /
  *未报告*. More than one: each, in order of first use (`opusplan` runs two).
- **Round 2 (Codex 13): the names are kept before token attribution touches them.** `TurnModels` sorts the names and collapses an
  unsplit multi-model turn to `mixed` (`TurnModels.cs:69-80`), and sorts an accepted split too (`:181-185`). That is right for
  tokens and wrong for provenance. So the record's `ran` is fed from the turn's reported names as the door gave them, in first-use
  order, whether or not the token split reconciles; `mixed` stays a token fact only. `ran` keeps up to eight distinct names and a
  count of any more (`more: n`, shown *and n more*), never a silent cut. A change of effort or of what was acknowledged is kept as
  its own dated entry (§9.3), not only when a new model name appears. Row b's cases: an unsplit mixed turn, a fourth and a ninth
  model, and an effort change on the same model.
- **A brainstorm's passes say the same (round 2, Codex 14).** A brainstorm opens no service session record
  (`DriverBrainstorms.cs:634-639`); its local pass record holds role, agent and account, state, codes and turns
  (`Brainstorms/Domain/BrainstormDocument.cs:7-13`, read at `BrainstormFiles.cs:195-196`). Each pass record, a restart's included,
  gains `actor`, `choice`, `asked` and `ran` in the same shapes, carried through its reader, its display and the terminal's
  brainstorm answer. That is in row b, with its own proof.
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
| Second opinion, Partner | nothing: the rule's or the ask's agents at account default (§4.0) |
| Helper | `HelperChoice.Candidates`, as today |
| Brainstorm | the request's or the policy's voices, else `[adapter]` as one voice (`DriverBrainstorms.cs:348`) |

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
| Second opinion | models for the agents a rule names: Codex · account default; Claude Code · `opus` · high | review: Opus high where the rule names Claude Code (labelled *the same agent, fresh*); the rule still decides who and in what order (§4.0) |
| Helper | Codex · account default, then Claude Code · `opus` · high | decide-support: another maker, high |
| Partner | models for the agent an ask names: Codex · account default; Claude Code · `opus` · high | supplies only the named agent's model (§4.4) |
| Brainstorm | two voices: Claude Code · `opus` · high and Codex · account default | decide-support: different makers' voices; the card says it adds a voice, which spends more (§4.4) |
| Intake, Ask Daoris, Chat | unchanged | no role in the reference reads intake's routing, and D174 rejects choosing by price alone (X); help is the person's (D89, D98) |

- *Claude Code* means the Claude door this machine runs (`adapter`). Another maker's choices appear only where that agent is
  installed with an account signed in for Daoris; otherwise the card says it left them out. The card judges each suggested
  entry by `ActorChoices.Judge` against the offers read (§9.2) and leaves out what cannot run (if `claude-agent-acp` offers ids
  rather than the aliases, `opus` there is `not-offered`), so a press never leaves an actor with no entry that ever runs (Opus N2).
- **A new version behind an alias** needs nothing: *ran* shows the new id. **A different alias for a role** moves only through
  D174: the reference promotes it with its date and batch, the census fails until the table follows, and the next release offers
  the changed actors on the card, never applied.

### 6.3 Who accepts it, and in which words

The **person's press**, never anything else. On the owner's install, that press is D191 point 3's yes for the spending it
changes. The card lists each actor that would change, before → after, with X's concrete words:

> **en:** *Use these choices for new work on this machine. For every actor but Brainstorm, Daoris tries a later choice, and other
> Daoris accounts, only when an earlier one cannot start; Brainstorm runs all its choices at once, one voice each. This changes
> which models your accounts spend on, and can spend more. Work already running keeps its choices.* **[Use these]** **[Not now]**
>
> **zh:** *在这台机器上的新工作使用这些选择。除头脑风暴外，只有前面的选择无法启动时，Daoris 才会尝试后面的选择和其他 Daoris 账户；头脑风暴同时运行它的所有选择，每个一方。这会改变你的账户把用量花在哪些模型上，用量也可能更多。正在运行的工作保持原来的选择。* **［采用这些］** **［暂不］**

**Round 3 (Codex 10, Opus R2-B6, agreed): the sentence is true for Brainstorm.** Round 2 made Brainstorm's entries voices that run
together and kept a sentence saying every later choice runs only when an earlier one cannot start. The general clause is now
scoped to every actor but Brainstorm, and the card's before → after list gives Brainstorm's line its own words where it adds
voices: *Brainstorm: two voices at once, where one ran before; this spends more* / *头脑风暴：同时运行两方，原来是一方；用量更多*
(`{{after}}` and `{{before}}` counted, §7.1). Test: a card changing Build and Brainstorm together shows both, the Brainstorm line
counting its voices, in both languages.

- *Use these* copies the suggestion into the person's own choices, unlinked: a later suggestion never follows into them.
- *Not now* records the suggestion's date (`actorsSuggestion`); the card stays away until the table changes.
- Its terminal twin is `daoris driver actor suggested [--use]`.

### 6.4 Migration

- **An account's `model` and `effortLevel`** (D98's first level, `AgentSettings.cs:96-168`) stay, as what *account default*
  means, set where they are today. The *Model and effort* fold gains one line: *Used where an actor runs this agent on its account
  default; a model an actor names outranks it.* / *角色以账户默认运行此智能体时使用；角色指定的模型优先于它。*
- **`adapter`, `intakeAdapter`, `helperAdapter`** are read as §6.1. Once the person sets Build, Intake or Ask Daoris, the key is
  **written through** (O): the first choice's agent, or the key absent while off; every write that changes `adapter` makes Chat
  and Brainstorm explicit where they are *as before* (§4.0). **What an older build does (round 3, Codex 11, Opus N9).** It reads
  the legacy keys and the original rules only: `adapter` for Build, for Chat and for the brainstorm's fallback
  (`ConversationRoutes.cs:48`, `DriverBrainstorms.cs:348`), `intakeAdapter` and `helperAdapter`. So a rollback loses Chat's own
  agent where it differs from Build's, every later choice, Brainstorm's added voices, and every model and effort; it starts each
  actor's first agent at account default, Chat on Build's. The *Actors* footnote says that (*An older Daoris runs each actor's
  first agent, Chat on Build's, with no models*), and the older-build fixtures demonstrate that projection.
- **The update drain turns Intake off in what `Effective` reads** (round 3, Opus R2-B3). A draining look plans with
  `InstallUpdate.Drained(config)` (`DriverWatch.cs:604`), which stops intakes by nulling `IntakeAdapter` (`InstallUpdate.cs:49-53`,
  D139 point 2's *intakes too*; the gate is `DriverIntakes.cs:58`). An Intake actor that is *set* would escape it. So `Drained`
  also sets the Intake actor *off* (`[]`) in the config it returns, and `DriverIntakes` reads Intake through `Effective`. Test: a
  draining look with Intake *set* starts no intake.
- **Every old door, and its fate (round 2, Opus B4(b)(c), Codex 7, 15).** Each writer of these keys goes through the person's
  press (§8.2), so none can change an actor unseen:

  | Old door | Today | Round 2 |
  |---|---|---|
  | `daoris driver adapter <agent>` | writes `adapter` with **no route** (`Change(null, null, …)`, `DriverChoicesCommand.cs:528-532`) | Build's one-choice list at account default through `SET_ACTOR` (`Person`), saying what it replaced, §4.0's inheritance applied; its sentence says Chat stays on its agent (round 3, Opus N9), where it once moved both |
  | `daoris driver intake\|helper <agent>\|off` | `SET_INTAKE`, `SET_HELPER` (`:540-575`) | Intake's or Ask Daoris's one-choice list, or *off*, through `SET_ACTOR`; `SET_INTAKE` and `SET_HELPER` leave `PipeCensus` |
  | `setting_propose`'s `adapter`, `intake`, `helper` doors (`HelpSettingProposals.cs:37`) | a card per key | from row c, the `adapter` door writes through the same write as `SET_ACTOR` (§4.0's inheritance applied); row d does the same for `intake` and `helper`; row h retires all three into `actor_propose`; the room's doors table (`HelpRoomDoors.cs:159-163`) points to it; `HelpCoverageTests` and the goldens follow |
  | The *Intake* and *Ask Daoris* rows (`settings.ai.json:7-37`) | their own Save | folded into their actor rows, the same `SET_ACTOR` |
  | A hand edit of `driver.json` | read at the next look | **unchanged, read at the next look** (round 3: the confirmed copy is dropped, §8.2) |

  Their twins follow them in the same rows (round 3, Opus N10): the service's `HelpSettingProposalWriter.Doors`
  (`HelpSettingProposals.cs:27`), `PipeWording`'s `SET_INTAKE` and `SET_HELPER` rows (`PipeWording.cs:188-189`), the web's
  `useSetIntake` and `useSetHelper` (`Web/bridge/driver.ts:463`, `:486`), and the room's machine facts that read the three keys
  (`HelpRoomMachineNow.cs:15-17`), which read the actors through `Effective` instead.

- **`opinions.reviewers`, `helpers`, `partners` and the brainstorm policy's agents** are unchanged (§4.4); `setting_propose`'s
  `opinion` and `helpers` doors stay theirs (`HelpSettingProposals.cs:31-38`).
- **The composer's picker** is unchanged; it opens showing what the actor set, since the agent answers with its current option.
- **An old `driver.json`** reads as today. An older build reading a new file ignores `actors`, which `JsonKeeping` keeps on save
  (`DriverConfig.cs:923`); `actors` joins `driver-config-keeps.json` (Opus N7; round 3, Opus N11: the CLI writer's keep table
  left in PUSH1f2b, `jsonkeep.ts:8`, so there is no second table). A malformed
  `actors` entry holds the affected start with its error, never silently reverting (X).

## 7. Where it lives, and the UI kept simple

**Settings → AI features** (`Web/settings/AiDomain.tsx`; the glossary's *AI features*, "the jobs Daoris gives a model or an agent
of its own", `glossary.json:651`), **machine-wide** as D197's one set and D183 point 2's machine-wide settings. *Search and
convergence* stays above; *Ask Daoris's access* below. The *Intake* and *Ask Daoris* agent rows (`settings.ai.json:7-37`) fold
into their actor rows.

```
Actors
Each kind of work Daoris starts runs on the first of its choices that can run, in your
order, and says so when it took a later one; Brainstorm runs all its choices at once.
                                                                   [Use these…] ← only while a suggestion is unanswered

Build            Claude Code · opus · high  +1                                     Edit
                 14 starts this week · 2.1M tokens · 2 on a later choice
Intake           Off — declarations only                                           Edit
Second opinion   models for the agents a rule names: as before                     Edit
Helper           as before                                                         Edit
Partner          models for the agent an ask names: as before                      Edit
Brainstorm       all at once: as before                                            Edit
Ask Daoris       Claude Code · account default   as before                         Edit
Chat             Claude Code · account default   as before                         Edit
Also `daoris driver actor`. An older Daoris runs each actor's first agent, Chat on Build's, with no models.
```

**Rounds 2 and 3 keep the screen to this one short list**: eight rows, one line each and a use line where there is use, one
editor open. Their mechanisms add no row, fold, card or setting; what they add is said in rows already there (*Details* on the
head, the opening note, the wait sentence, a conversation's one line when it goes on with another choice). Round 3 drops round
2's hand-edit card with the confirmed copy (§8.2). Second opinion's and Partner's editors list a model per
agent rather than an order, and Brainstorm's says *voices*; Second opinion's row drops round 1's *another maker first*, which the
actor never decided.

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
| Section body | sentence | Each kind of work Daoris starts runs on the first of its choices that can run, in your order, and says so when it took a later one; Brainstorm runs all its choices at once. | Daoris 启动的每种工作，按你排的顺序，在第一个能运行的选择上运行；用了后面的选择时会说明。头脑风暴同时运行它的所有选择。 |
| Actor names | `field` | §2.2 | §2.2 |
| account default | `choice` | account default | 账户默认 |
| Off | `choice` | Off | 关闭 |
| as before | `status` 16/5 | as before | 沿用原设置 |
| choice n | `status` 16/5 | choice {{n}} | 第 {{n}} 个选择 |
| your pick | `status` | your pick | 你指定的 |
| not reported | fragment | not reported | 未报告 |
| Details rows | `field` | Actor · Model · Effort | 角色 · 模型 · 推理强度 |
| Details values | fragment | asked {{v}} · acknowledged {{v}} · ran {{v}} | 要求 {{v}} · 已确认 {{v}} · 实际 {{v}} |
| asked's sources | fragment | your command · your environment · not carried · a rule's agent at account default | 你的命令 · 你的环境 · 未传递 · 规则指定的智能体，账户默认 |
| more models | fragment | and {{n}} more | 另有 {{n}} 个 |
| Opening note's actor line | sentence | Ran as {{actor}}'s choice {{n}}, {{entry}}: {{passed}} | 以{{actor}}的第 {{n}} 个选择运行，{{entry}}：{{passed}} |
| Brainstorm's editor | sentence | Brainstorm runs all its choices at once, each one voice; at most 4. | 头脑风暴同时运行它的所有选择，每个一方；最多 4 个。 |
| Brainstorm's row | fragment | all at once | 同时运行 |
| Fenced actors | sentence | Models for the agents a rule or an ask names. | 为规则或请求指定的智能体选模型。 |
| The card's Brainstorm line (round 3) | sentence | Brainstorm: {{after}} voices at once, where {{before}} ran before; this spends more. | 头脑风暴：同时运行 {{after}} 方，原来是 {{before}} 方；用量更多。 |
| A conversation gone on with another choice (round 3) | sentence | Went on with {{entry}}, {{actor}}'s choice {{n}}: {{why}} | 已改用 {{entry}} 继续，即{{actor}}的第 {{n}} 个选择：{{why}} |
| The actors' footnote (round 3) | sentence | Also `daoris driver actor`. An older Daoris runs each actor's first agent, Chat on Build's, with no models. | 也可用 `daoris driver actor`。旧版 Daoris 只运行每个角色的第一个智能体，聊天用开发的智能体，且不带模型。 |
| Buttons | `button` 20/8 | Edit · Add a choice · Use these · Not now | 编辑 · 添加选择 · 采用这些 · 暂不 |
| The wait | sentence | {{actor}} waits: none of its choices can run now. {{soonest}} | {{actor}}在等待：它的选择现在都不能运行。{{soonest}} |
| The use line | sentence | {{starts}} starts this week · {{tokens}} tokens · {{later}} on a later choice | 本周启动 {{starts}} 次 · {{tokens}} 个 token · {{later}} 次用了后面的选择 |

The pass reasons (`unavailable`, `not-offered`, `refused`, `cooling`, `signed-out`, `no-posture`, `not-admitted`,
`not-listed`) are coded sentences beside `note.opening.why.*`. 沿用原设置 is five units, at the `status` budget's edge; row b's
`names:check -- --strict` judges it, and 原设置 is the shorter fallback.

## 8. Ask Daoris

### 8.1 The tool

**`actor_propose`** (`Mcp/HelpActorTool.cs`, beside `agent_settings_propose`, `Mcp/HelpAccountTool.cs:10-31`), the driver's kind
`HelpActorProposals` after `HelpAccountProposals.cs:7-20`. One proposal may carry several actors (X's coordinated set), each
`choices`, `off`, `clear` (back to *as before*) or `suggested`, with `why`, and **the revision of `actors` it read**: a digest of
each named actor's list as read, judged per actor at *Apply*, so a changed actor needs a fresh card rather than being overwritten
(X), and an unrelated actor's change does not stale the card (Opus N8). Every proposal is judged by
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
- **Every write path, not the managed door alone (round 2, Codex 15, Opus B4).** Round 1's guarantee held only for requests
  entering the pipe (`InstancePipe.cs:557-562`). Full-access Ask Daoris has unrestricted file access and no tree guard
  (`ChatRunner.cs:758-774`, D183 point 5), and `daoris driver adapter` wrote with no route at all. Round 2 closed the second and
  answered the first with a confirmed copy; **round 3 drops the copy** (Codex 6, Opus R2-B2: Opus's option (ii), Codex 15's first
  option):
  - **Daoris's own doors**: every one that writes `actors`, `adapter`, `intakeAdapter` or `helperAdapter` is the person's press,
    and each is named: `SET_ACTOR` (`Person`) through the running instance's pipe, whoever calls; the screen's own Save; a
    card's *Apply* (`actor_propose`'s, and until row h `setting_propose`'s three doors, §6.4); and the person's own terminal
    acting alone where no instance runs or a headless loop holds the home (`DriverChoicesCommand.cs:292-321`, D194 point 5),
    which refuses a session's terminal (`:294-298`). §6.4's table holds each to that, and its census proves no other writer.
  - **A raw edit of `driver.json`** is what it is for every other key in that file, `cap`, the holds and an access rule's
    `unrestricted` among them: read at the next look, and, from a full-access shell, D183's unenforced boundary, which D183 already
    says in its own terms. Round 3 does not make one family of that file's keys the only one guarded.
  - **Why dropped rather than given an exact contract.** The copy guarded only against an edit made with no intent to forge (one
    that rewrote both files defeated it), and to be exact it needed: a snapshot bound to each card and revision, a stale *Apply*
    and *Put back* refused, a Save confirming only its own patch over the confirmed state while `DriverConfig.Save` merges
    whatever is on disk (`DriverConfig.cs:914-923`), a shared lock and recovery across two files, a terminal twin for a headless
    loop that has no window to show the card, and one accessor that every one of the fifteen start-time readers Opus R2-B2(d)
    lists must take. That is a second source of start-time truth for a boundary that is not one; its cost falls on every start,
    its gain on none that a full-access shell cannot undo. Both reads name the drop as acceptable (Codex 15's first option; Opus
    R2-B2(ii), *smaller*).
  - Proof: the managed verb meets the window's card (`PipeConfirmations`); the census holds every writer of the four keys to the
    doors named; a session's terminal is refused the alone path.
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

An actor absent is read from today's keys; an empty list is off, for Intake and Ask Daoris only, and refused elsewhere (§4.0).
Objects, not C's `agent/model/effort` string, since a free-field model id may hold a `/`. The writer is `DriverConfig.WithActor`
under the keep lock (`DriverConfig.cs:914-923`), judged by `ActorChoices.Judge`, writing through `adapter`, `intakeAdapter` and
`helperAdapter` (§6.4). `HomeWatch` already wakes the loop on its change. It is machine-local, as everything in that file; a
shared host carries no choice and no model.

### 9.2 `data/agent-offers.json`: what each agent last offered

What each agent, version **and account** last offered in `model` and `thought_level`, and each account's `refused` readings (a
pipe door's measured refusal included), with the date, written at every protocol `session/new` (`KeepOptions`, `Acp.cs:940-942`)
and every refusal (§3.3), read by the walk (per account, under a day old, same version) and the editor (as hints). **Round 2
(Codex 8, Opus B2, N7):** keyed per account, since each account is its own home; written by more than one process (the loop,
conversations, the host's consoles), so atomically, merged per key under a lock, and a reading from an older version never
replaces a newer one. A reading, not a setting,
so not `harnesses.json` (D197's account file). It is a new home entry in the three twins (`home.ts`, `Driver/` and `Core/`
`DaorisHome.cs`, held equal by `home.test.ts:323-328`), with D195's cleanup inventory row (taken by a clear of the home, never
synced). X's re-discovery at every open alone would leave the editor nothing to offer before a session. Round 2's
`data/actors-confirmed.json` is withdrawn with the copy (round 3, §8.2); `agent-offers` is the one new home entry.

### 9.3 On the session record: five machine-local fields

| Field | Holds | Set |
|---|---|---|
| `actor` | the actor's id | at the open |
| `choice` | `1` to `4`; `rule-default`; `before`; `pick`; `handed` (§4.0) | at the open; again only by §3.3's same-account move |
| `asked` | `{ agent, model, effort }`, each of model and effort a tagged value `{ source, value?, carried }` (round 3, below) | at the open; again only by §3.3's same-account move |
| `override` | `{ model?, effort? }`: the composer's last change, category and value id, reconciled from the returned configuration (§4.2) | at each successful composer change |
| `ran` | `{ models: [up to 8, first-use order], more: n, entries: [the last 8, { at, acknowledged?, effort? }], entriesMore: n }`, or null for *not reported* | when a turn reports a new model, and when an acknowledgement or effort changes (§5) |

**Round 3 (Codex 4): `asked` is tagged, never a sentinel.** `AgentSettings.Models` includes the tool's own alias `"default"`
(`Accounts/AgentSettings.cs:58-59`), so round 2's string `default` for *account default* could not be told from an explicit request
for that alias, and replacing a requested effort with `not-carried` lost the `high` that *asked high · not carried* shows. So
each of `model` and `effort` is `{ source, value?, carried }`: `source` is `entry` (the choice named it; `value` is the tool's word,
`"default"` included), `account-default` (the choice named none; no `value`; nothing passed), `command` or `environment` (the
person's own command or environment named it; `value` where it can be read); `carried` says whether Daoris's carrier passed it
(`false` with `source: entry` is *asked high · not carried*, the value kept). Tests: an explicit `"default"` and an absent model
recorded and resumed apart, the first carried as `--model default` or its option and the second passing nothing; an unsupported
effort kept as asked and not carried. **`ran.entries` is bounded** as `models` is, with a count (Opus N6).

**Round 2 (Opus N3):** `choice`, `asked`, `override` and `ran` are set after the open, so each goes through
`session-record-fields.md`'s later door, as `help_mode` and `resolved` have: a service door of its own, a `PersonDoors.Table` row
and its stand-ins, loopback only. Round 1's *exactly as `profile`* held for `actor` alone. A brainstorm's pass record carries the
same shapes locally (§5).

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
tokens, starts on a later choice; where the machine log keeps fewer than seven days, the line says the span it read (Opus N12). **Never** (X): a price; context occupancy as spend; token categories summed into one cost; an
unsplit `mixed` turn apportioned (`TurnModels` marks a split that does not cover the whole, `:19-25`); a missing measurement shown
as zero. The Agents page's *Usage* fold stays per account. MIXAGENT3a-iii's cost per row is the same per-session reading, and
this design leaves its row to it.

## 11. Containment and admission

- **Containment (D201).** Every choice starts through the same gate (`Driver.HoldAsync`, `AgentStarts.StartAsync`,
  `Driver.cs:3161-3180`); a fallback is the same start with other arguments, never another spawning path. A start the launcher
  cannot contain is still logged `agent.contained: false`. A choice never names a command: `commands[adapter]` stays the person's.
- **Admission (D199).** Unchanged. The model rides `set_config_option` or `--model`, the effort `set_config_option` or `--effort`;
  the admission digest hashes the posture, the guard and the rule lists only (`PartnerConfiguration.cs:56-72`), never the
  settings file. Confinement belongs to the copy and the launcher, and a model cannot widen it. X's amendment (model, effort and
  mechanism in the digest) is rejected: it would require a measured row per model with no difference in confinement to measure.
  A test holds it: two choices, one digest. D199 point 9 already asks for the model on each partner's answer, which §5 supplies.

## 12. What it amends, once accepted

Each a dated note appended to the decision, never rewritten into it.

| Decision | The note |
|---|---|
| D98 | *"Daoris names no model and chooses none"* becomes: Daoris keeps no model list and names no model of its own; the person chooses per actor in the tool's own words; Daoris carries that choice to each start at the tool's session level (`set_config_option`; `--model`, `--effort`); its only choosing is to take the first choice that can run, saying so. The second level now opens every protocol start; the first is what *account default* means. Still: the tool's home never written, `ANTHROPIC_MODEL` rejected, `max` never a file default, a tool whose words Daoris has not read offered only *account default* and the free field. Its *Boundaries* line that Daoris never turns dsh's catalogue itself becomes: only to a choice the person made |
| D24 | A note: the tier that ran includes the model the agent reported, shown on the session, beside the choice asked; an actor *as before* works at *account default* |
| D57 | Narrow: one dated table from actor to alias, suggested and never applied, held to `docs/model-roles.md` by a census; no versions, capabilities or prices |
| D179 / TOOL3 | The record keeps the agent's reported model, machine-local; the head and the actor's use line show it |
| D174 point 4 | Realized: the product's suggestion reads the reference through a census |
| D155 point 3 | Each named reviewer takes its model from *Second opinion*'s entries for that agent, else account default; reviewers stay named by the rule, and `false` stays off; point 4's label names the model |
| D163 point 3 | The hand-over offer, when a driven line's agent's accounts are all out, names the actor's next choice on another agent; it is taken only by the person's press under every `handOver` mode |
| D163 *Rejected* (round 3) | *Automatic hand-overs by default* gains one exception, by the owner's direction of 2026-10-23 (LIMITGOON2): a conversation whose agent has no account left goes on, as a new part carrying Daoris's brief and its held words, with its actor's next choice, the person's order being their consent; never on a pick, never to an agent outside the actor's entries (§3.5(e)) |
| D175 point 3 | A resume whose own account refuses its asked model is an account that cannot carry its words: they go to a new record on another account of the same choice, the old record left as it was (§3.5(a)) |
| D65 §1b, D89 | Intake's and Ask Daoris's agents become their actors; *absent means off* stands |
| D183 | A choice with no posture for the start's access is passed (`no-posture`); `actor_propose` joins the proposal kinds and retires `setting_propose`'s `adapter`, `intake` and `helper` doors; no `settings_*` grant covers actors; every door of Daoris's own that writes the actors is the person's press, and a raw edit of `driver.json` stays the unenforced boundary it is for that file's other keys (round 3) |
| D176 point 9 | A brainstorm with no named agents takes *Brainstorm*'s entries as its voices instead of the machine's agent; a voice that cannot run is dropped, never replaced; the synthesizer is unchanged |
| D130 point 4 | The pick overrides agent and account; the model comes from the actor's choices for that agent |
| D195 | `agent-offers` joins the inventory (round 3: `actors-confirmed` withdrawn) |
| D139 point 2 | The drain's *intakes too* reaches a *set* Intake actor: `Drained` sets it *off* (§6.4) |
| D116 | *actor*, *choice*, *account default*, *brainstorm*, and *Build* as an actor's name |

**Stands unchanged:** D197 (the set, the order, the walk), D199 (admission, with its test), D201, D125, D47 §4, D203 (its
*Bring in another agent…*, once built, orders its agents by the actor's choices without amending it).

**Rejected**, beyond those each section names: a model routing every start, or an *Auto* pool (needs a model to judge the work,
and nobody can approve its members); cheapest-first routing or automatic escalation (no price, and D174 judges finished work);
trying a later choice before another account of an earlier one; per-workspace choices; a suggestion applied by itself, or one
that follows into accepted choices; replacing the rules' agent lists; flagging *ran* against *asked*; a notification per
fallback; the model as a travelling fact; folding the account's *Model and effort* away (it is what *account default* means).

## 13. The test plan

- **Unit**: `ActorChoices.Judge`'s table; `ActorChoices.Effective`'s table for every actor (as before, off, set; a fence's agent
  with and without an entry, `rule-default`; a pick; a handed quest; Brainstorm's voices, two entries of one agent refused);
  the reading from today's keys; write-through, the first write making Chat and Brainstorm explicit, setting Build leaving them
  unchanged, and an older build's reading; opinion migration fixtures (`false`, absent, malformed each as today); the walk's table
  (cooling, signed out, refused and not offered **per account** with two accounts offering differently, an entitlement change with
  no version change, a stale reading tried, no posture, not admitted, not listed, kept for conversations, the pick, a handed
  account, all out → the wait, the first that frees in order); the reservation (refusal, cancellation, three alternatives: none
  leaked, rotation undistorted); the second opinion's passes over entries and the label; `HelperChoice`, `PartnerChoice` and the
  brainstorm's voices (a dropped voice not replaced); the suggestion's census both ways.
- **Census**: every start names its actor through a required parameter of `HoldAsync` and the chat runner's start, so a new start
  without one does not compile, plus a table test of the brief's 13 starts (C, O); the target's `Asked` and the three native
  prepares; `PipeCensus` (`ACTORS` `Open`, `SET_ACTOR` `Person`, `SET_INTAKE` and `SET_HELPER` gone); every writer of `actors` and
  the three keys going through `SET_ACTOR` or the Save; `HelpCoverageTests` and the room's goldens; the home twins.
- **Stub**: ACP: model before effort, both before the first prompt; an unoffered model on one account passes that account and the
  walk continues; a returned list naming another value is a refusal; a list not returned is *not reported*; a same-account move
  sets again without a respawn; another account ends the record and releases its reservation; a resume re-applies `asked` then
  `override`. Pipe: `--model` and `--effort` exactly when an entry names them, none where the person's command or environment
  does; a go-on and a second try carry them; a measured model refusal on a silent turn kept as that account's reading.
- **Invariants**: two choices, one partner digest; the record's five fields answered on loopback only and absent from the feed,
  the mirror and a sync (the `profile` tests' twin); the opening note's actor line absent from a sync; the managed verb meeting
  the window's card, and a session's terminal refused the alone path.
- **Records**: `ran` keeping an unsplit mixed turn's names, a fourth and a ninth model (`more`), and an effort change on one
  model, its entries bounded; the composer's override across duplicate display names, an effort-only change, a model change
  that resets the effort, a restart and a later actor edit; `asked`'s explicit `"default"` and absent model apart, and an
  effort not carried keeping its value; a brainstorm pass record and its restart carrying the four shapes.
- **Round 3's paths and passes**: each test named in §3.3, §3.5 (a)-(e), §4.0 (set → clear Chat → change Build), §4.1 (the
  count; restricted Ask Daoris as before; `no-connector`), §6.3 (Build and Brainstorm on one card) and §6.4 (the drain with
  Intake *set*; the older-build projection).
- **Words**: `i18n:check`, `names:check -- --strict`, the glossary's new terms; stories at the frame's three widths in both
  languages (D185 R6).
- **Installed**, distinct from any story: the rows' own proofs below. Before a suggestion changes, the batch after the owner's
  press is compared with the week before it (row f's line, D174's outcomes: first-run gates, *must* findings, landed or sent
  back), never by price or benchmark alone.

## 14. The rows, in build order

Each lands alone, behind the one before, with its words in both languages and its proof (D187).

| Row | What lands | Proof |
|---|---|---|
| **MODELROLE1a — what each door accepts, measured** (tools, stubs) | On `claude-agent-acp`, `codex-acp` and dsh: `set_config_option` for `model` and `thought_level` at a driven-shaped open, before the first prompt, the values offered, combined or apart, and whether a turn reports its model; **round 2 (Opus N2):** whether the offers differ by account, whether `set_config_option` on `claude-agent-acp` writes the account's `settings.json` (which would break D98's boundary and move every *account default* on it), whether the answer returns the option list, and whether `claude-agent-acp` offers the SDK's aliases or ids; **round 3 (Opus N5):** whether an accepted change returns the requested id as the current value. On the pipe door: `claude -p --model <alias>`, `--effort` (with `max`) **against a conflicting saved per-model effort**, `CLAUDE_CODE_EFFORT_LEVEL`'s rank, whether `--model` holds on a `--resume` run, and when and in what words an unknown or unentitled model is refused (on a silent turn or not). No spawn-file effort is measured as a carrier (§3.2). An evidence document; the ACP and pipe stubs speak the measured shapes | The evidence on the owner's install; the stubs' tests |
| **MODELROLE1b — each session says its actor and what ran** (driver, service, web) | Every start names its actor (required parameter); the target's `Asked`, carried by the three native prepares; the record's five fields through their later doors; `ran` from the reported names before `TurnModels`; the brainstorm's pass records (§5); *Details*' rows and the parts fold's model; the usage history's `actor`; `actor.ran`. Behaviour unchanged: every choice `before` | The 13-start table; the loopback-only and no-sync tests; *ran* from each stub, *not reported* where a door says nothing; §13's record cases; a brainstorm pass and its restart naming actor and model; stories; on the install, a driven session's *Details* naming the model Claude reported |
| **MODELROLE1c — Build runs its choices** (driver, modules, web, terminal) | `actors` read and written through, every change of `adapter` making Chat and Brainstorm explicit; `ActorChoices.Judge` and `Effective`; **round 3 (Codex 8): every compatibility reader and writer of the shared scalar in this row's boundary**, so Chat's agent (`ConversationRoutes.cs:48`, `:164`, `SessionsVerbs.cs:33`) and Brainstorm's fallback voice (`DriverBrainstorms.cs:348`) read `Effective`'s agent here, at account default, and `setting_propose`'s `adapter` door writes through the same write; rows d and e add their models and editors; the choice-major walk with its per-start passes and its one count per start; the carriers, on every run; per-account offers and refusals, §3.3's paths and §3.5 (a), (c) and (d) for driven starts and their continuations; `no-posture`, `no-connector`; the drain's Intake *off*; `agent-offers` in the twins; `ACTORS`, `SET_ACTOR`; `daoris driver actor`, and `driver adapter` through it; AI features' *Actors* with Build's editor; the note, chip and wait; the Agents fold's line | The unit, walk, stub, census and invariant tests above; actual Chat and Brainstorm starts, and the legacy proposal door, after changing Build, each keeping its agent (not `Effective`'s table alone); on the install, a Build whose first choice names a model one account does not offer runs that choice on the other account, and with neither, runs its second and says so on the head and in the note |
| **MODELROLE1d — Intake, Ask Daoris and Chat run their choices** | Starts 5, 10-13 take their actor; the two cards fold into rows; `driver intake\|helper` write a one-choice list through `SET_ACTOR`, `SET_INTAKE` and `SET_HELPER` leave the census; `setting_propose`'s `intake`, `helper` and `adapter` doors write one-choice lists through `Judge` until row h retires them; `no-posture` from `HelpAccess.For`; the pick overrides; the composer's `override` kept, reconciled and re-applied; §3.5(b)'s re-ask within the press and its go-on after an interruption; the curve move judging each candidate's reading. §3.5(e) lands as LIMITGOON2, after LIMITGOON1 and row c | *Absent means off* kept; `no-posture`, restricted Ask Daoris as before; the composer opening on the actor's model; the override cases of §13; `HelpRoomDoorsTests`; on the install, a chat on *Chat* · sonnet shows sonnet beside its composer and in *Details*, and keeps it across a restart |
| **MODELROLE1e — second opinions, helpers, partners and brainstorms take their models** | §4.0's fences and §4.4's composition; the label naming the model; the brainstorm's frozen voices; the hand-over offer naming the next choice | `ReviewerChoice` over entries (another maker first; the same agent on a stronger model labelled *fresh*; a reviewer with no entry at `rule-default`; `false` still off); two choices one digest; a brainstorm whose voice cannot run dropping it with no substitute; a cooling Build offering the hand-over and taking it only on the person's press under `ask` and `on` alike; on the install, a second opinion's *Details* naming Codex's reported model |
| **MODELROLE1f — each actor's use shown** | The 7-day line from the usage history and `actor.chose`; absent stays absent | The sum's table; stories; the install's line against the usage history for a day (`tools/usage-report.mjs`) |
| **MODELROLE1g — Daoris suggests choices** | `ActorSuggestion` and its census; the card; `actor suggested [--use]`; agents not set up left out | The census both ways; the card's tests and stories; on the install, **the owner's press**, D191 point 3's yes, with row f's week before it as the baseline |
| **MODELROLE1h — Ask Daoris proposes choices** | `actor_propose`, `HelpActorProposals`, the revision check, the room's *Actors* facts and door | `HelpCoverageTests`; goldens; a stale card refused; a judged refusal at *Apply*; full access running the verb meeting the window's card (`PipeConfirmations`); on the install, Ask Daoris answering *why did builds fall back?* and its card applied |

**Filed with this design, each behind its own measurement or design:**
- **i. Exact build by a quest's weight**: a publisher (the person, the intake, an agent whose pack names the change) may declare
  `weight: exact`; the receiving machine starts it as an *Exact build* actor, suggested after D174's build-exact row. The quest
  carries no model. Filed after a batch of row b's records; the brainstorm synthesis's own choice is measured in the same batch.
- **j. Orientation as a start**: a scout before a Build, handing a pack from the knowledge store (the MODELROLE1 row); its actor
  named then, suggested after D174's orient row. No scout on a valid pack.
- **k. Cool-offs scoped to a model**: the walk and cooling read `seven_day_opus` and `seven_day_sonnet`, already in the usage
  history, and cool a model's limit as account plus model.

## 15. Not built, and not measured

Every door's acceptance (row a); whether `codex-acp` reports a turn's model; `--effort` on the installed Claude Code; the
documented precedence of `--model` over `ANTHROPIC_MODEL`, read and not observed; `CLAUDE_CODE_EFFORT_LEVEL`'s rank; whether
offers differ by account; when a pipe model refusal arrives; what an accepted `set_config_option` returns as the current value.
A full-access Ask Daoris editing `driver.json` with its shell changes what the next look runs, as for every key of that file
(§8.2, D183).

## 16. The reads

### 16.1 Round 1's reads, answered in round 2

Both reads are verbatim in `docs/2026-10-23-actors-reads.md`. Every finding was checked against the code at `59a2db074` and
held; none was answered by disagreeing with its facts. *Taken* means the read's named fix, or the fix both reads agree on;
*taken, narrowed* or *answered* says how round 2 differs.

**Codex (15 findings, 12 blocking; verdict another round).**

| # | Finding | Verdict | Where |
|---|---|---|---|
| 1 | `:2074` does carry "No model is ever named" | Taken | §0.2 |
| 2 | Model windows are recorded in the usage history, unused by the walk | Taken; row k scoped to the walk and cooling | §0.2, §1.1, §4.1, row k |
| 3 | D205 overstates what decides today's model | Taken: today's cascade includes custom commands, repository settings, `ANTHROPIC_MODEL` | §0.2, D205's note |
| 4 | No reviewers means off, not inheritance | Taken (agreed with Opus B6): the empty-reviewers branch withdrawn; `false` off; migration fixtures | §4.0, §4.4, §12 D155, §13, row e |
| 5 | A rule's agent absent from the actor runs with no provenance | Taken, first fix: `rule-default`, shown in preview and note | §4.0, §9.3 |
| 6 | Brainstorm conflates alternatives with participants | Taken (agreed with Opus B7): entries are voices, frozen, a voice dropped never replaced, D176 point 2 untouched | §4.0, §4.4, §6.2, §7, §12 D176 |
| 7 | Write-through moves Chat and the brainstorm fallback; `driver adapter` unmigrated | Taken (agreed with Opus B4): the first write makes the others explicit; every legacy writer named | §4.0, §6.4 |
| 8 | Cached offers and refusals skip an account that supports the model | Taken (agreed with Opus B2): per account, a day's validity and version, refreshed by the open | §3.3, §4.1, §9.2 |
| 9 | No release for a failed negotiation | Taken: a reservation committed at the first prompt or released; a per-start exclusion; `counts: false` not used | §4.1 |
| 10 | The setter does not establish an acknowledgement | Taken, with one answer: returned vs assumed; a contradiction is a refusal; no list proceeds as *not reported* rather than hold, since *ran* settles it and holding would strand an agent that never returns a list | §3.2, §5 |
| 11 | The spawn-file effort fallback restores the precedence problem | Taken: no file fallback; an unsupported effort is account default before dispatch, said | §3.2, row a |
| 12 | Composer changes cannot be restored from notes | Taken: the record's `override`, category and value id, merged | §4.2, §9.3, row d |
| 13 | The reported-tier record loses information | Taken: names before `TurnModels`, up to 8 with `more`, effort and acknowledgement entries | §5, §9.3, §13 |
| 14 | Brainstorm has no provenance home | Taken: the pass records carry the shapes, in row b | §5, row b |
| 15 | The person's press is guaranteed only on the managed door | Taken, both halves: every Daoris door through `SET_ACTOR`; a hand edit held for a card, starts on the confirmed actors; the limit against a forger said | §6.4, §8.2, §15 |

**Opus foundation read (B1-B8 blocking, 12 not; verdict accept after named changes).**

| # | Finding | Verdict | Where |
|---|---|---|---|
| B1 | The in-start move happens after the record opens; on the pipe after the prompt | **Taken, narrowed**: (b) and (c) as named; (a) moves the open session only where the walk, re-asked with the account passed, answers the same agent on the same account, so B2's order holds. Under the read's own condition this asks another foundation read of §3.3 | §3.3, §4.3 |
| B2 | Offers and refusals are per account | Taken as named: keyed per account, a per-start passed set, a choice passed only when every account is, the pipe refusal kept | §3.3, §4.1, §9.2 |
| B3 | Permission prompts can starve an actor | Taken: `no-posture`, judged before the walk | §4.1 |
| B4 | Write-through moves untouched actors; old doors unmigrated | Taken: (a) explicit on the first write, (b) `driver adapter` through `SET_ACTOR`, (c) each old door's fate | §4.0, §6.4, §8.2, rows c, d |
| B5 | Later native runs drop the carried model | Taken: `Asked` on the target, every prepare appends | §3.2, §13, row b |
| B6 | An empty `reviewers` does not exist | Taken (agreed with Codex 4); Second opinion and Partner are *models for the agents a rule or an ask names* | §4.0, §4.4, §6.2, §7 |
| B7 | Brainstorm's choices are voices | Taken, the second of its two options, as the reads agree with Codex 6; the card counts the voices it adds | §4.4, §6.2, §7.1 |
| B8 | `handOver: on` widened to take Daoris's offer | Taken, the first option: only the person's press, D163 not widened | §4.2, §12 D163, row e |
| N1 | An effort can be outranked unseen | Taken: the environment variable measured; *your environment*, *not carried* | §3.2, §5, row a |
| N2 | Row a's list should grow | Taken; the card judges suggested entries against the offers | row a, §6.2 |
| N3 | Fields set after the open need their door | Taken | §9.3 |
| N4 | The opening note's actor line could sync | Taken: local note only, a no-sync test | §4.6, §13 |
| N5 | Which choice a continuation takes | Taken: the record's `asked`; `handed` | §4.0, §4.2 |
| N6 | Helper with no rule list may take the asker's family | Taken, labelled | §4.4 |
| N7 | `agent-offers`' writer; the keep tables | Taken | §6.4, §9.2 |
| N8 | `actor_propose`'s revision undefined | Taken: a per-actor digest | §8.1 |
| N9 | The combined model-and-effort option | Taken | §3.3 |
| N10 | A plugin pipe harness has no carrier | Taken: account default only | §3.2 |
| N11 | A wrapper's `--model` cannot be seen | Taken: said beside *your command* | §3.2 |
| N12 | The use line's span | Taken | §10 |
| §4 | The names | Taken: *choice*'s avoid list and match, *your pick* 你指定的, 第 {{n}} 个选择, *Build* as a name only, the *Off* words, the role note, the missing words | §2.1, §4.6, §7, §7.1 |

Round 2 asked Codex to read it and an independent Opus reader to read §3.3 and §4.1 again as the walk's contract; both reads
are in §16.2.

### 16.2 Round 2's reads, answered in round 3

Both reads are verbatim in `docs/2026-10-23-actors-reads.md` (*Codex, round 2*; *Opus (foundation), round 2*). Every finding
was checked against the code at `af642e750`, and every blocking one held: the count's mechanism (`HarnessRoster.cs:1651-1693`,
the holds at `Driver.cs:1775` and `:1796` keeping their count), the setter's assumed and returned lists (`Acp.cs:527-539`), the
chat start opening its record before the walk could be re-asked (`ChatRunner.cs:424-463`), `ChatGoOn` resuming only its record's
adapter (`ChatGoOn.cs:372`), the curve move's model-blind candidates (`HarnessRoster.cs:1974-1994`, `CurveMove.cs:62-127`), the
`"default"` alias (`AgentSettings.cs:59`), the alone path (`DriverChoicesCommand.cs:292-321`), `HelpAccess.For`'s restricted
branch (`HelpMode.cs:83-95`), the drain's null (`InstallUpdate.cs:49-53`) and the planner's one-door gate (`Planner.cs:1729-1753`).
*Taken* is the read's named fix, or the one both reads agree on; *answered* says how round 3 differs.

**Codex, round 2 (11 findings, 10 blocking; verdict another round).**

| # | Finding | Verdict | Where |
|---|---|---|---|
| 1 | Reselection has no defined ownership of the reservation | Taken, agreed with Opus R2-B4: one count per start, owned across the re-ask, its own load out of the re-ask's facts, the old process stopped before the end releases it; `Unchose` on a hold or refusal before the open; the record's end after it; *Commit* dropped | §4.1 |
| 2 | Reusing an ACP session does not restore account default | Taken, its first option: a touched category the later choice leaves at account default is set back to the value `session/new` returned; an agent that will not ends the record | §3.3 |
| 3 | "The next look" is no conversation fallback | Taken, agreed with Opus R2-B1(b): the chat start owns the pending start in the same press; an interrupted one goes on as a fresh start of its actor, apart from ordinary go-on | §3.5(b) |
| 4 | `asked` uses a real model name as its sentinel | Taken: tagged `{ source, value?, carried }`; `"default"` an entry's value; a not-carried effort keeps its value | §9.3 |
| 5 | Merged overrides can resurrect an obsolete effort | Taken: a model change clears the effort override; an effort-only change keeps the model; resume overlays accordingly | §4.2 |
| 6 | The confirmed copy lacks an approval and commit contract | Taken by **dropping the copy** (Codex 15's first option, Opus R2-B2(ii)): every Daoris door named as the person's press; a raw edit is D183's boundary, as for every key of `driver.json`. Why not the contract: §8.2 | §8.2, §6.4, §7, §7.1, §9.2, §12, §13, §14 |
| 7 | Clearing an actor defeats first-write preservation | Taken, its first option: preservation on **every** change of `adapter`, a clear included | §4.0 |
| 8 | Row c's guarantee depends on rows d and e | Taken: Chat's and Brainstorm's compatibility readers and the `adapter` proposal door move into row c; its proof starts them | §6.4, row c |
| 9 | Restricted Ask Daoris is given a new refusal | Taken: `no-posture` from `HelpAccess.For(...).Refusal` and `DrivenAccess.For(...).Refusal` alone; an as-before restricted start tested | §4.1 |
| 10 | The acceptance paragraph still calls a voice a fallback | Taken, agreed with Opus R2-B6: the sentence scoped to every actor but Brainstorm, Brainstorm's line counting its voices, both languages, the section body too | §6.3, §7, §7.1 |
| 11 | Rollback loses more than the model (non-blocking) | Taken, agreed with Opus N9: the older build's projection disclosed and fixtured | §6.4, §7, §7.1 |

**Opus foundation read, round 2 (R2-B1 to R2-B6 blocking, 13 not; verdict accept after the six).**

| # | Finding | Verdict | Where |
|---|---|---|---|
| R2-B1 | The paths after the open cover a driven first start only | **Taken as named**, (a) to (c) and its four tests, **with one addition by the owner's direction**: (e), a conversation whose agent has no account left goes on with its actor's next choice (LIMITGOON2). (a)'s *never takes the actor's next entry* holds for every continuation but that one. By the read's own condition this wants another foundation read (§16.3) | §3.5, §4.2, §12 D163, D175 |
| R2-B2 | The confirmed copy misses four paths | Taken, option (ii): the copy, its card and its home entry dropped; the four paths are each Daoris's own door, named | §8.2 |
| R2-B3 | The update drain escapes a *set* Intake | Taken: `Drained` sets Intake *off*; tested | §6.4, §12 D139 |
| R2-B4 | The reservation names a mechanism the count lacks | Taken, agreed with Codex 1 | §4.1 |
| R2-B5 | A Build mixing doors can fall onto the pipe in an unadopted repository | Taken: `no-connector` per entry; the planner's gate and the set-up ask whether any entry can drive | §4.1 |
| R2-B6 | Brainstorm's card keeps B7's sentence | Taken, agreed with Codex 10 | §6.3, §7.1 |
| N1 | Helper's order is D155's maker passes | Taken | §4.0 |
| N2 | A pick or handed agent refused the model | Taken: the same agent's next entry on that account, else a refused pick with its reason | §3.5(d) |
| N3 | The order a start walks is read once | Taken | §4.0 |
| N4 | The readings: keyed by model; a method rejection per agent and version; written before the end | Taken | §3.3 |
| N5 | What an accepted change returns | Taken: measured in row a; meanwhile only another offered value is a refusal | §3.2, row a |
| N6 | `ran.entries` unbounded | Taken: the last eight and a count | §9.3 |
| N7 | If the copy stays | Moot: the copy is dropped | §8.2 |
| N8 | The hand-edit card's place | Moot: the card is dropped | §7 |
| N9 | Rollback and the old verb | Taken (with Codex 11): Chat may change agent on rollback; `driver adapter` says Chat stays | §6.4 |
| N10 | The retired doors' twins | Taken: each named beside its door | §6.4 |
| N11 | Stale sentences | Taken: *reused as it is*, *the code already enforces it*, the CLI keep table, D24's note | §1.1, §3.2, §6.4, §12 |
| N12 | The opinion wire's same-agent display | Taken: judged against Build's agents | §4.0 |
| N13 | A voice's restart judges its account's reading | Taken | §4.4 |

### 16.3 What round 3 asks next

Round 3 takes every blocking finding of both reads as named or as both agree, except one addition: §3.5(e), a conversation going
on with its actor's next choice when its agent has no account left, by the owner's direction of 2026-10-23. That is exactly the
case the Opus read names as the walk's contract again (*a continuation taking the actor's next entry*), so an independent Opus
reader reads §3.3, §3.5 and §4.1 to §4.2 again as a foundation, and Codex reads round 3, under D191. D205 stays proposed until
both have read and the steward accepts.
