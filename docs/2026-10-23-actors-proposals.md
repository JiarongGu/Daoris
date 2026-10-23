# Actors: Daoris chooses the model for each kind of work it starts: the proposals

> MODELROLE1, phase 1 of a co-design under D191 (2026-10-23). Each section is one maker's complete proposal, written from
> `docs/2026-10-23-actors-brief.md` alone and sealed before its author read another. Sections are added verbatim and never
> edited afterwards. The synthesis, in a later quest, is written apart. Claude's was written first. Codex's and Fable's
> come separately.

## Claude (Opus 5.5)

*Sealed 2026-10-23, before any other proposal was read.*

### C1. The shape in one paragraph

**An actor is a seat in the company.** Each kind of work Daoris starts sits in one seat, and the person fills each seat
with a short ordered list of up to three **choices**. A choice is an agent, then optionally a model in that agent's own
words, then optionally an effort.

- **Daoris walks the list at every start.** For each choice it runs D197's account walk unchanged, and it never runs
  anything that is not on the list.
- **An empty seat means *as today*.** It resolves to exactly the agent and the account's own model that start uses now.
  Upgrading therefore changes nothing until the person, or Ask Daoris with the person's Apply, fills a seat.
- **Daoris ships one suggested set** in the tools' own aliases, offered as a single proposal and never applied by itself.
- **Each session says which seat it sat in and what actually ran**, read from the agent's own report of the turn.
- **The setting lives in one place**: Settings → *AI features*, which already names an agent per job for intake and Ask
  Daoris (brief §3.8). Each actor is one row there.

### C2. The actors

| Actor | en | zh | Starts it covers (brief §4) | What a person reads under its name |
|---|---|---|---|---|
| `build` | Build | 开发 | 1 driven build (and a workflow *work* or *look* node), 2 its retries, carry-ons and resumes, 3 a quest's hand-over | Works a quest in its repository |
| `conversation` | Conversation | 对话 | 10 a repository conversation, 12 its going on, 13 a briefed new part, 4 a conversation's hand-over | Your own talk with an agent; you can still pick at the start |
| `help` | Ask Daoris | 问道衍 | 11, and 12 and 13 in the help room | Answers about Daoris itself |
| `intake` | Intake | 受理 | 5 | Turns an ask into quests |
| `opinion` | Second opinion | 第二意见 | 6 (with its recheck and a workflow *opinion* node) | Reads work before it lands; another maker first |
| `helper` | Helper | 助手 | 7 | Answers one question for a session |
| `partner` | Partner | 搭档 | 8 | Writes one piece in its own copy |
| `brainstorm` | Brainstorm | 头脑风暴 | 9 (every generator, and the synthesizer) | Proposes ideas side by side |

**Every label reuses an existing glossary term.** *session*, *conversation*, *Ask Daoris*, *intake*, *second opinion*,
*helper* and *partner* are already terms. *Brainstorm* / 头脑风暴 is already the window's word
(`notifications.done.look.brainstorm-*`). *Build* / 开发 is the one new label, and it names the work, not a person. So the
only new term is the umbrella word, and D193's "no new vocabulary on the page" is kept for the rest.

**The umbrella word.**

- **English: *actor*.** It is the owner's word. *Role* is taken in the code (`RoleFilter`, the server scope a co-worker
  is bound to) and in D174's development roles. Two things named *role* would be confused exactly where they meet:
  a helper's seat versus a helper's scope.
- **Chinese: 角色** (the part one plays). The owner glossed actors as "proper roles". It is unused in the Chinese chrome
  today. It is rejected as names: *演员* (a stage actor), *执行者* (an executor: mechanical, and close to the workflow's
  executor) and *参与者* (the workflow's participation).
- **The glossary entry**: *actor* / 角色, "a kind of work Daoris starts an agent for, filled by the choices the person put
  in order", avoiding 演员 and 执行者.

**Not actors, and why:**

| Kind | Why | What happens instead |
|---|---|---|
| A set-up | It starts no agent (`SetupWorld.cs:79`); its asks go through intake and quests | It sits in Intake, then Build |
| A workflow node | It runs as a quest or an opinion (`WorkflowGraphRun.cs:314-316`) | It takes its kind's actor. A graph node may name **another actor** in a new optional field `actor` (C5), never a model, so a graph stays model-free and portable (D24) |
| A meeting (D177) | No agent takes a turn yet | The actor `meeting` / 会议 is added in the row that gives meetings real turns |
| Orientation | No orientation start exists | The actor `orient` / 摸底 is added with the row that builds the orientation pack (C10, row g). A seat is shown only when something sits in it |
| Usage readers, login and probes | Not working agents | — |

**Deferred: a light build.** D160's largest saving was splitting build into *exact* and *judgement*. Daoris cannot tell
the two apart today, because no quest says which it is. A mark set wrongly costs a failed build, not a saving. So row g
measures first:

- a quest's publisher (the person, the intake, an asking agent) may mark a quest *small*;
- a `small` actor / 小改 takes it, falling back to Build where its seat is empty;
- it is promoted to the screen only after a batch shows small quests merging clean on the cheaper choice.

### C3. A choice, and how a start hands it to the agent

A **choice** is `{ agent, model?, effort? }`, stored as `agent[/model[/effort]]` in `driver.json`'s new `actors` object,
for example `"build": ["claude-code/opus/high", "claude-code/sonnet", "codex-acp"]`.

- **No model means the account's own**, which is D98's first level and today's behaviour.
- **The values are the tool's own words**, read from where D98 reads them:
  - for Claude Code, its SDK's aliases and efforts (`AgentSettings.Models`, `AgentSettings.Efforts`) plus the free field;
  - for any agent on the protocol door, the values its `session/new` offered in the `model` and `thought_level`
    categories (`Acp.cs:80-82`). The driver keeps them per agent and version as it last saw them, which is machine-local
    and refreshed at every start. That is an observation of the wire, not a list Daoris maintains, so D57 stays rejected.
- **A choice for an agent whose model Daoris cannot hand over** (Codex until measured, dsh) carries the agent alone, and
  the editor says *the account's own model* in one line, as D98 does today.

**How the model reaches the agent. One mechanism per door, both the tool's own:**

1. **Claude Code, on both doors: the settings layer Daoris already composes.** The pipe door's `--settings`
   (`Adapters.cs:2208-2240`) and the ACP door's `_meta.claudeCode.options.settings` (`:1794-1811`) carry the rules today.
   They gain `model` and `effortLevel` (`max` included, since this is one session and not a kept default).
   - It is the tool's flag tier, so it sits above the account's `settings.json` and a repository's own
     `.claude/settings.json`. The seat the person chose therefore wins over a repository's preference.
   - `ANTHROPIC_MODEL` in the person's own environment still wins over it, and the session says so (point 3).
   - No configuration home is written, and `ANTHROPIC_MODEL` is never set at spawn. D98 holds.
2. **Any other agent on the protocol door: `session/set_config_option` right after `session/new`**, before the first
   prompt. It is the composer's own call (`Acp.cs:527-539`), made by the driver, and only with values that `session/new`
   just offered.
   - If the value is not offered, that choice is **passed** (`actor.passed`, reason `not-offered`). The process is
     stopped before any prompt, and the walk tries the next choice. It never runs on whatever the agent defaulted to.
   - Whether `codex-acp` offers a model option at all is not measured (brief §3.2). Row b measures it on the install.
     Until then a Codex choice is agent-only.
3. **What ran is read back, never assumed.** At each turn's end the driver already learns the turn's model from the agent
   (`TurnModels`, brief §3.9).
   - Where it differs from the choice, the session says *asked sonnet; ran opus* once, with the likeliest cause where
     Daoris can see it: `ANTHROPIC_MODEL` set, or the repository's settings.
   - Where the agent reports no model, the session says *model not reported*.
   - This is D24's "say the tier that ran", applied to agents.

**A partner's admission (D199 point 4).** The settings' digest is computed without `model` and `effortLevel`, because
neither changes confinement. A measured row still admits a partner whatever choice it runs. This is a note on D199, made
in row b with a test that the digest ignores exactly those two keys.

### C4. The walk, and what happens when an account cools

**At a start**, the actor's choices are tried in order. For each choice:

1. its agent's account walk runs unchanged (D197: the set, the order, `keep`, cool-offs, sign-ins, a slot under the cap);
2. where Claude's usage read gives a window per model, an account cooling on that model is held for this choice only;
3. if no account can run it, the next choice is tried.

**All of a choice's accounts are tried before the next choice.** The person ordered the choices by preference, and
accounts of one agent are interchangeable by D197's design. Spending the second choice while the first has a free
account would quietly overrule that order.

**A fallback is said.** The start's note reads *Build, second choice: the first (Claude Code · opus) had every account
cooling until 14:20*, coded `actor.fell-back` with the reason. The head shows the second choice's chip (C6).

**If every choice is held**, the start waits as today's all-cooling wait does. The wait names the actor and the earliest
reset, and offers *Start anyway on…* for any agent outside the range. A press there is the person's pick for that start
alone (D130 point 4) and never joins the range.

**Mid-session, when an account cools:**

| Case | What happens |
|---|---|
| The next account of the **same choice** is free | As today: the carry-on moves to it |
| The next choice is the **same agent with another model** | The carry-on moves to it without asking, because the person put it in the range, and the conversation gets a note |
| The next choice is **another agent** | D163's hand-over, by the person's `handOver` rule (`ask` by default). The ask names the choice: *Hand over to Codex, Build's second choice?* A range never moves a running line to another agent silently |
| The next start of the **same line** | Begins again from the first choice. The range is a preference, not a ratchet |

**The second opinion keeps D155's independence.** Its walk keeps D155's passes over the range: every choice of another
maker's family first, then a maker not declared, then the same agent, labelled *the same agent, fresh*. A model never
changes a family, and the screen's line says so: *Another maker is asked first, whatever the order*.

**A helper's or partner's named agent.** An ask that names an agent takes the first choice of that agent in the range,
else that agent with the account's own model. The `helpers` and `partners` rules still decide whether the ask runs
(`ask` | `on` | `off`). Their agent lists become the range (C8).

### C5. What a person who sets nothing gets, and the suggested set

**Nothing changes at the upgrade.** An absent or empty seat resolves to today's choice:

| Actor | *As today* means |
|---|---|
| Build, Conversation, Brainstorm | `adapter` with the account's own model; Brainstorm also keeps its policy's agents |
| Intake | `intakeAdapter`, or off |
| Ask Daoris | `helperAdapter`, or off |
| Second opinion | `opinions.reviewers` |
| Helper | the `helpers` list, else another maker |
| Partner | the agent named |

Each row of the screen says this in words: *As today: Claude Code, the account's model*.

**The suggested set** is one card at the top of the *Actors* section, shown until the person applies it or dismisses it:
*Daoris suggests a set that spends less: lighter models for intake, Ask Daoris and helpers, the strongest for builds.
See it…*. The card opens the whole set beside today's, and **Apply** writes it. It is never applied by itself, because it
may spend accounts differently, which D191 point 3 keeps for the owner.

The set at this proposal's writing, in Claude Code's aliases and from `docs/model-roles.md`'s evidence:

| Actor | Suggested |
|---|---|
| Build | `claude-code/opus/high`, then `claude-code/sonnet/high` |
| Conversation | *as today* (the person picks per conversation) |
| Ask Daoris | `claude-code/sonnet/medium` |
| Intake | `claude-code/sonnet/medium` |
| Second opinion | `codex-acp`, then `claude-code/opus/high` |
| Helper | `codex-acp`, then `claude-code/sonnet` |
| Partner | `codex-acp` (where admitted) |
| Brainstorm | `claude-code/sonnet`, `codex-acp` |

**Why this is not D57's registry.**

- It names tiers by the tool's own aliases, which do not go stale (brief §6, lesson 2).
- It never limits what the person can choose; the free field stays.
- It is one data file the driver reads (`Driver/Actors/suggested.json`), changed only by D174's process: a trial, a
  measurement, a dated promotion. Its first lines cite the entry of `docs/model-roles.md` it rests on.
- An agent that is not installed, or has no account signed in, drops out of the card's preview with its reason, so the
  card never suggests a choice that can only wait.

**D98 is amended in one sentence:** *Daoris chooses within what the person set or accepted; it may suggest, in the tool's
own words, and a suggestion applies only by the person's press.* D24's rule stands as it is: features name no model, and
the deployment (the person) chooses.

### C6. Where it lives

**Settings → AI features → *Actors* (角色)**, its first section, above *Search and convergence*. The intake and Ask Daoris
rows of today move into it as their actors; the Ask Daoris access row stays where it is. A sketch:

```
Actors                                                     [Daoris suggests a set that spends less. See it…]
Who does each kind of work Daoris starts, in your order. Another account of the same choice is tried before the next.

Build            Claude Code · opus · high, then Claude Code · sonnet           this week 2.1M tokens   Change…
Conversation     As today: Claude Code, the account's model                    this week 0.4M          Change…
Ask Daoris       Claude Code · sonnet · medium                                 this week 0.2M          Change…
Intake           Off: declarations only                                                                Change…
Second opinion   Codex, then Claude Code · opus · high  · another maker first  this week 0.3M          Change…
Helper           As today: another maker                                                               Change…
Partner          As today: the agent named                                                             Change…
Brainstorm       As today: Claude Code, the account's model                                            Change…
```

**The editor.** *Change…* opens the row in place:

- up to three lines, each three selects: agent, model, effort;
- the effort select appears only where the agent offers one;
- up and down on each line, × to remove, *Add a choice*;
- *As today* to empty the seat;
- Intake and Ask Daoris keep *Off*.

There is nothing else on the screen: no per-workspace override, no conditions, no per-error lists. Three choices is the
cap because the list exists for availability, and a fourth is not used in practice (brief §6, lesson 3).

**Machine-wide, not per workspace or repository** (as D197's one set). A repository that needs another model already has
its own `.claude/settings.json`, which the seat overrides only for Claude's flag tier. Whether a repository may pin a seat
is left for after the first month's use, recorded in the decision, not built.

**On the session.**

- The head's agent chip gains the actor and what ran: *Build · Claude Code · sonnet · high*.
- A second line appears only when something differs: *second choice* (with its reason on hover), *asked opus; ran
  sonnet*, or *model not reported*.
- The composer's per-conversation picker stays as it is. A change there is the person's, recorded as today, and it
  overrides the seat for that conversation.

**At a start.** The person's pick of agent and account in the start dialog (D130 point 4) overrides the seat for that
start, and the record says *your pick*. A workflow node may name an actor (`actor: "opinion"` on a *work* node, to read
with the reviewer's choices), and never a model. A quest and an ask name no actor in this proposal; row g's *small* mark
is the one exception, measured first.

**The terminal.**

- `daoris driver actor` lists the seats.
- `daoris driver actor build claude-code/opus/high claude-code/sonnet` sets a seat.
- `daoris driver actor build --as-today` empties it.
- `daoris driver actor --suggested` prints the set, and `--apply-suggested` writes it.
- `daoris driver intake <agent>|off` and `helper <agent>|off` stay, and write a one-choice seat.

### C7. Ask Daoris

- **The tool.** A new `actors_propose` takes one actor's whole list, or `suggested: true`, and composes a card. The card
  shows the seat before and after, in the screen's own row words, and **Apply** writes it through the same door the
  screen uses.
- **Restricted** (the default): the card and the person's Apply, as every setting card works (D89).
- **Full access** (D183): Ask Daoris acts itself, by D183's rule, through the terminal verb. Every write is said as a
  notice naming the actor, the old list and the new, with *Put back* one press away. Making an exception to D183 here
  would make a full-access mode that is not full, and the owner chose that mode knowingly.
- **What it reads to help.** Each seat, the suggested set, the week's tokens per actor and per model (C9), and which
  agents and accounts are ready. It can then answer "why did my builds cost so much this week" with numbers Daoris holds,
  and propose a seat that answers it.
- `agent_settings_propose` stays for an account's own model (D98's first level).

### C8. Migration

- **Nothing is rewritten at the upgrade.** An old `driver.json` with no `actors` reads as every seat *as today* (C5).
- **The first write of a seat writes only that seat.** The keys it supersedes are kept as they are and read only while
  their seat is empty:
  - `intakeAdapter` and `helperAdapter`;
  - the agents of `opinions.reviewers`, `helpers` and `partners`;
  - the brainstorm policy's agents.
  The rules beside them (`on`, `required`, `minutes`, `ask`/`on`/`off`) stay where they are, because a seat says *who*,
  not *whether*.
- **One writer keeps the two in step.** When the person sets the Second opinion seat, the driver also writes
  `opinions.reviewers` to the seat's agents, in order. An older build reading the file then still finds a sensible list.
  The same holds for intake, Ask Daoris, helpers and partners.
- **The account's own `settings.json` model and effort stay** as D98's first level: what *the account's own model*
  means, edited where it is today on the agent's page. The hint there gains one line: *Used where an actor's choice
  names no model.*
- **The composer's picker is unchanged** (C6).
- **The session record gains two machine-local fields**, beside `serves` and `piece` and never synced (D47 §4):
  - `actor` (one of C2's ids, or `person` for a start dialog's pick);
  - `choice` (its index in the seat as it stood at the start, or null).
  The record still names no model (TOOL3). The model shown is read from the usage history, which already keys each turn
  by session (`UsageHistory.cs:606-640`).

### C9. Cost shown

**No price, ever** (D57). Tokens are what Daoris measures, and they are what the owner's direction is about.

- **Per actor**, on its row: *this week N tokens*. Hover gives the split by model, from the usage history's turn lines,
  which gain the `actor` field, machine-local.
- **Per session**, in the parts strip that already shows its tokens (`PartsStrip.tsx:18`): the split by model, as
  `TurnModels` gave it. This is MIXAGENT3a-iii's reading, kept on the screen and not in the record.
- **No targets on the window in this proposal.** D160's targets are this development's measures. The window shows what
  ran and what it took, and Ask Daoris can read both.
- **What it never claims.** A model shown is the agent's report, never the choice, so *asked sonnet; ran opus* is
  visible, and a turn whose split disagrees with its total is shown as its total alone, as `TurnModels` already rules
  (`TurnModels.cs:9-32`).

### C10. Tests and proof, and the rows

**Rules each get a unit test in the driver's fast loop.**

- `Actors.Resolve`: an empty seat yields today's choice for each of the eight actors; a seat yields its choices; a
  start-dialog pick and a workflow node's `actor` override it.
- `ActorWalk`: all of a choice's accounts before the next; a per-model cooling; every choice held yields the wait; the
  second opinion's maker passes over a range; a helper's named agent picks its first choice.
- The choice parser and writer: `agent[/model[/effort]]`; `max` allowed in a seat; an unknown agent refused with its code.
- The migration readers: old keys read while a seat is empty; one writer keeps `reviewers` in step.
- The partner digest ignores exactly `model` and `effortLevel`.

**Stub and census tests.**

- `acp-stub` gains offered `model` and `thought_level` options. A test sees `session/set_config_option` sent before the
  first prompt, and an option not offered passed with `actor.passed`.
- The pipe stub sees `model` and `effortLevel` in the composed `--settings`.
- **A census reads the driver's source.** Every one of the brief §4's thirteen starts resolves its agent through
  `Actors.Resolve`, so a new start cannot bypass the seat unseen, as the existing censuses guard their seams.
- A test that the read-back says *asked … ran …* from a stub turn reporting another model.

**The page.** A story and unit tests for the *Actors* section and its editor (every seat *as today*, a full seat, the
suggested card); locale parity and glossary tests for *actor* / 角色 and *Build* / 开发.

**Installed proof** (the parent, on the owner's install after a republish):

- one Build on `claude-code/sonnet` whose head and parts strip show it;
- one fallback: a seat whose first choice is an agent with every account cooling, seen falling to the second with its note;
- one Codex seat once row b's measurement says whether `codex-acp` takes a model;
- Ask Daoris proposing the suggested set and the owner's Apply.

**The rows, in build order:**

| Row | What | Proof |
|---|---|---|
| **a** | The seat and the record: `actors` in `driver.json`, `Actors.Resolve` at every start, `actor` and `choice` on the record, the census. Empty seats only, so behaviour is unchanged | The census passes; every existing start test is unchanged |
| **b** | Handing the choice over: Claude's settings layer on both doors, `set_config_option` on the protocol door, *asked/ran* read back, the partner digest note on D199; `codex-acp`'s options measured on the install | Stub tests; one real Build on sonnet seen in the usage history |
| **c** | The walk over a range: fallback, per-model cooling, the all-held wait, mid-session moves and the hand-over ask naming the choice, the second opinion's passes | Walk tests; the installed fallback |
| **d** | The screen and the terminal: *Actors* in AI features, the editor, `daoris driver actor`, intake and Ask Daoris moved in, the head's line | Story, parity, the installed window |
| **e** | The suggested set and Ask Daoris: `suggested.json`, the card, `actors_propose`, full access's notice and *Put back* | The installed Apply |
| **f** | Cost: `actor` on the usage history's turns, the row's week, the parts strip's split by model | Installed numbers matching `tools/usage-report.mjs` for a day |
| **g** | Measured first, then promoted: the *small* mark and its seat; `orient` with the orientation pack; `meeting` with D177's turns | One batch's measurements, as D174 promotes |

**What this proposal amends:**

| Decision | The amendment |
|---|---|
| D98 | Daoris chooses within what the person set or accepted, and may suggest (C5). Its rejected `ANTHROPIC_MODEL` at spawn stays rejected, and its "tool's home never written" stands |
| D174 point 4 | Carried out in these terms: the actors are Daoris's seats, and `suggested.json` is the reference's product-facing half |
| D199 | A note: the settings' digest ignores `model` and `effortLevel` (C3) |
| D155 | A note: `reviewers` becomes the Second opinion seat's mirror, with the same passes (C4) |

Nothing here amends D24, D163, D197 or D201. Every start stays contained through the launcher whatever its choice,
because the choice changes only what is handed to the agent, never how it is started.

<!-- Codex wrote its proposal read-only from the brief alone, never opening this file (D191); the steward ran it from outside the install. -->

## Codex's proposal

### 1. The actors

An **actor / 角色** names the job Daoris starts. An **agent / 智能体** names the product doing it. The distinction belongs in the glossary: changing an actor’s options changes who may do its job; it changes no permissions, account membership or workflow obligation.

Use familiar job names on the page. Do not introduce the company metaphor as another navigation vocabulary.

Paths below use the brief’s abbreviations: `Driver/`, `Modules/`, `Core/`, `Mcp/` and `Web/`. Start numbers refer to the brief’s §4.

| Actor ID | English / Chinese | Job and starts covered | Reason and code |
|---|---|---|---|
| `build` | Development / 开发 | The default driven work, including work requiring judgement. Start **1** and workflow work/look nodes. | Today these share `Driver.RunAsync`, `Driver/Loop/Driver.cs:1661`; workflow execution is classified in `Workflows/Domain/WorkflowGraphRun.cs:314`. This is D174’s `build-judgement` role. |
| `build-exact` | Exact work / 明确工作 | Driven work whose change is already specified: start **1**, explicitly classified before dispatch. | `docs/model-roles.md` distinguishes this work on measured evidence. Today the driver has no such distinction; it must be added deliberately. |
| `intake` | Intake / 受理 | Reads an ask and routes the work. Start **5**. | `Loop/DriverIntakes.cs:104` already starts this job separately, using `IntakeAdapter`. |
| `opinion` | Second opinion / 第二意见 | Reads a candidate, gives findings, and rechecks. Start **6**, including workflow opinion nodes. | `Loop/DriverOpinions.cs:34`; reviewer selection is already a separate policy in `Review/Domain/ReviewerChoice.cs:175`. “Review / 审阅” remains the person’s act under D155. |
| `helper` | Helper / 助手 | Answers one question from the work in progress. Start **7**. | `Loop/DriverHelpers.cs:574`; `Conversations/Helpers/HelperChoice.cs:15`. Keep it distinct from Ask Daoris. |
| `partner` | Partner / 搭档 | Writes one contained piece for its lead. Start **8**. | `Loop/DriverPartners.cs:482`; D199’s measured admission remains mandatory. |
| `brainstorm-proposal` | Brainstorm idea / 方案生成 | Produces one independent proposal. Generator turns of start **9**. | `Loop/DriverBrainstorms.cs:345–358` builds separate generator passes. |
| `brainstorm-synthesis` | Synthesis / 方案汇总 | Compares the anonymous proposals and produces the synthesis. Synthesis turn of start **9**. | `Loop/DriverBrainstorms.cs:418` currently chooses the first answering generator’s agent. This job deserves its own range. |
| `chat` | Chat / 聊天 | The person’s own repository conversation. Start **10**, including starts with words and starts from earlier words. | `Modules/Bridge/Routes/ConversationRoutes.cs:40`; `Conversations/Chat/ChatRunner.cs:390`. Yes, it is an actor: Daoris starts it and must explain its choice. |
| `ask-daoris` | Ask Daoris / 问道衍 | Helps the person use and configure Daoris. Start **11**. | `Modules/Bridge/Routes/HelpRoutes.cs:46`; its independent choice preserves D89. |
| `orient` | Orientation / 定位 | Produces a bounded pack of exact records and code ranges when a suitable pack is absent. **A new start**, introduced separately. | No such start exists today. MODELROLE1 and D174 justify adding it; they do not justify pretending the builder already delegates orientation. |

All these names fit both the section budget and the shorter status budget. Add their glossary meanings and reasons before using them; retain the existing terms for intake, helper, partner, chat and second opinion. Register the actual catalogue keys under their element kinds. Agent names, model IDs and effort values remain the tool’s untranslated content.

Starts **2, 3, 4, 12 and 13** are ways existing work continues, not new actors. They retain the originating actor. A hand-over changes its selected option and records a new part; a resume retains the part’s selection.

The other named kinds resolve as follows:

- **Workflow node:** uses the actor of the work it starts. It is not itself an actor.
- **Set-up:** remains deterministic preparation followed by intake and development. `Driver/Setup/SetupWorld.cs:73–79` publishes asks and probes tool versions; it starts no additional agent.
- **Meeting:** reserve **Meeting turn / 会议发言**, but expose no editable range until a production turn exists. `Loop/DriverMeetings.cs:26–27` explicitly says no real agent takes a turn yet.
- **Steward and decision support:** useful development roles, but not separate product starts today. Decision support can be a helper or brainstorm; the driver does not secretly acquire a steward session.
- **Usage readers, sign-in, installation, version probes, embeddings, titles and commit messages:** no new actor. A subprocess is not necessarily an agent doing work.

### 2. The range

Each actor has an ordered list of options. An option contains:

```json
{
  "agent": "claude-code",
  "model": { "value": "sonnet" },
  "effort": { "value": "high" }
}
```

The strings are examples in the tool’s vocabulary, not Daoris enums. Persist the semantic model and effort values, not ACP config-option IDs: those IDs are discovered again for each opening.

There are two explicit alternatives to a named value:

- **Account default / 账户默认:** use what the selected account and tool configuration choose, with its cascade stated. This is a visible choice, not an omitted field.
- **No effort option:** the agent has no such control. This is different from choosing its default effort.

A tool-provided default may be selected where the tool actually offers it. Daoris must not invent a portable `default` effort or interpret omission as a known reasoning level. Standing ranges reject `max`; a conversation may still choose it where its live tool offers it.

A range may initially contain one option. Its representation and editor remain a list, so adding availability alternatives requires no redesign.

**How the choice reaches the agent.** Introduce one selection object carried through every start, rather than adding model flags independently at callers.

For ACP:

1. Start through the existing contained launcher.
2. Perform `session/new` or the appropriate resume.
3. Establish the required posture.
4. Find the offered `model` category and set the selected model.
5. Read the resulting options again; then set the selected effort from `thought_level`.
6. Check the agent’s answer before releasing work or queued words.

The model precedes effort because changing it can change the effort choices. `Agents/Acp/Acp.cs:527–539` already returns the option list after a change; `:911–945` already reads creation options. Extend this mechanism to driven starts and resumes. Do not change mode through the model selector.

This also honours **Codex and dsh** without writing their configuration homes: use their negotiated session controls. If the required control or value is absent, that option is unavailable. Do not substitute an account’s model while claiming the requested model ran.

For Claude’s native pipe door, use a measured, version-supported per-start mechanism: a model flag, and an effort setting in the existing generated command-line settings where its semantics are demonstrated. Merge that setting with the rules already handed through `ClaudeCodeAdapter.HandSettings`; do not create a competing settings file or overwrite an account’s file. The native adapter currently adds neither model nor effort (`Agents/Adapters/Adapters.cs:2088`, `:2123`, `:2172`), so support is new work with an installed proof.

Do not set `ANTHROPIC_MODEL`. An explicit actor option intentionally overrides account and repository **model selection** for that start; this amends D98’s rationale, while preserving its prohibition on a spawn environment override. Account-default options retain today’s cascade. Conflicting custom `commands[agent]` arguments must produce an actionable refusal unless that command shape has a measured way to accept the selection; appending duplicate flags is not a solution.

**Fallback before work begins.** Resolve an effective range, then walk it in order:

1. For the first option, use D197’s account ordering and eligibility rules.
2. Try its eligible accounts before considering the next option.
3. Pass cooling, signed-out, refused or unsuitable accounts with structured reasons.
4. If no account can honour that option, move to the next selected option.
5. Stop at the first usable option and record why every earlier one was passed.

The account walk remains authoritative for `keep`, usage curves, cool-offs, pins and sign-in state. Add option eligibility to that walk; do not repeatedly call it until it happens to return another account. Count only the committed selection, not every capability attempt. A missing model entitlement does not by itself cool or globally refuse an otherwise usable account.

This ordering respects the person’s model preference before consuming a less-preferred model. It also preserves `AccountRotation.Order`’s existing treatment of accounts kept for conversations (`Accounts/Rotation/AccountRotation.cs:311–336`).

There are explicit constraints on the effective range:

- **Second opinions:** preserve D155’s maker passes and the scoped `reviewers` order. Within each reviewer, try that actor’s options in their configured order, then its account walk. The ordering is therefore maker class, reviewer order, option order, account order. Show this exception in the editor. Another model from the author’s maker is still labelled *same agent, fresh*.
- **Partners:** a named agent is a binding constraint, as it is in `Conversations/Partners/PartnerChoice.cs:17–31`. Its range supplies model/effort alternatives for that agent; it cannot silently replace the named writer. Admission is checked for each alternative.
- **Brainstorms:** the participant list determines how many independent generators run. A fallback list does not fan out into more participants. A named participant stays on that agent; a different maker requires the packet’s disclosure approval as well as an accepted option.
- **Scoped helper and partner rules:** continue to limit which agents are allowed. A range grants no permission to start them.

A fallback is said where it occurs:

> Using option 2: Claude Code · opus · high. Option 1 could not start: its eligible accounts are cooling until 14:30.

Account rotation within the same option retains the existing account-rotation explanation; it is not mislabelled as model fallback.

**After work has been dispatched**, a refusal is not generally an availability signal. Do not replay work on another model after a content refusal, an uncertain send, or a partially completed turn. A limit follows the existing cooling and continuation rules. Another agent requires D163’s hand-over and its `ask | on | off` rule. A model change within the same agent is also recorded explicitly; it is not hidden inside a retry.

Resuming an existing conversation restores its retained selection before its next words. It does not reread today’s actor preference and silently change the work. A same-option account move remains D130/D165’s operation. A new option for ongoing work requires a visible continuation choice; changing agents uses the hand-over door. Preserve the brainstorm’s bounded same-agent account repair rather than adding an unlimited range retry.

When every option is unavailable, keep the work and say **No option ready / 暂无可用选项**. Show the reasons, the earliest known reset, and the relevant doors: accounts, edit options, or an explicit continuation choice. Cooling may recover automatically; a sign-in or a changed range remains the person’s action. No guessed reset countdown, automatic sign-in, unlisted model or unlisted agent.

### 3. Defaults and migration

**The first start after upgrade keeps today’s behaviour.** Migration changes neither the chosen agent nor account defaults, enables no extra job, and installs no “economical” policy without acceptance.

Represent an absent actor setting as a compatibility projection of the existing choices. Render that projection as an ordered range using visible **Account default** options. Its source line says **Current setup / 现有设置**. The start and session head also name account-default selection, so it is never an invisible substitute.

Intake and Ask Daoris remain off where their existing settings are absent. Their no-agent tiers remain declarations and starters respectively. This follows the explicit absence rules in `Platform/DriverConfig.cs:414–437`, not a new spending assumption.

Offer a separate, dated suggestion based on `docs/model-roles.md`:

| Actor | Suggested range or treatment | Evidence and limitation |
|---|---|---|
| Development | Opus 5.5, high; no cheaper automatic substitute initially | The reference assigns judgement work to it. Sonnet and Fable judgement work remain trials. |
| Exact work | Sonnet 5.5 at the tool’s selectable default effort; then Opus 5.5, high, only if accepted | D160’s exact workers had a median 33 turns and 5% of cost across 19 rows. The more expensive fallback must be named in the acceptance. |
| Orientation | Sonnet 5.5 at its selectable default effort | Scouts had a median 23 turns and 2% of cost. Haiku has not earned promotion. Reusing a valid pack precedes spending on a scout. |
| Second opinion | Codex `gpt-6.1-sol`, high; then explicitly listed same-maker Opus, high | The reference establishes the independent read. The second entry remains visibly non-independent where it shares the work’s maker. |
| Helper | Suggest the measured independent reader for diagnostic or decision support | This transfers the reference’s `decide-support` evidence; it is a trial for the product’s helper workload, not proven universal superiority. |
| Intake | Offer a Sonnet trial, rather than recommend a proven replacement | Routing accuracy has not been established by the exact-worker measurements. |
| Brainstorm proposal and synthesis | Offer judgement-role options as a trial | The reference supports their kind of reasoning, but does not measure these product passes. |
| Partner | Keep the named agent and its visible account default until the exact configuration is admitted | Read-only Codex evidence proves nothing about a writing partner. |
| Chat and Ask Daoris | Keep the person’s choices | The reference expressly leaves help to the person; it does not establish their conversational preferences. |

Where an agent cannot explicitly select the recommended default effort, the suggestion must show the actual inherited effort or remain unavailable. It must not silently translate “default” into `high`.

The acceptance wording is concrete:

> Use these options for new work on this machine. Daoris may try later options and other eligible Daoris accounts when earlier choices cannot start. This can spend more tokens. Existing work keeps its choices.

Buttons: **Use these options / 使用这些选项** and **Keep current / 保持现状**. Show the changed actors and complete lists above them. Enabling orientation or another previously disabled job is a separate, named change.

The reference supplies dated evidence, not a shipped model registry. Resolve suggestions against the installed tool’s offered IDs or documented aliases; show unavailable suggestions rather than replacing them. An accepted concrete ID stays fixed. An accepted vendor alias follows that alias’s meaning, with the resolved model reported. A later recommendation changes only after D174’s trial, measurement and dated promotion, and still requires acceptance before replacing a person’s range.

Migration treats the old controls precisely:

- **Account `settings.json`:** unchanged. Its model, effort and per-model effort remain the source for account-default options and for the tool outside an explicit actor selection.
- **`adapter`:** supplies the compatibility default for development and chat.
- **`intakeAdapter`, `helperAdapter`:** supply the two independent compatibility ranges and their enabled state. An explicit actor entry becomes authoritative; obsolete verbs must direct the person to its range rather than editing an unused field or silently replacing the list.
- **`opinions.reviewers`:** retains its scoped order and admission role. Model alternatives enrich each permitted reviewer; they do not flatten away D155.
- **Helper agent lists:** preserve their current selection order in compatibility mode. On accepting an explicit helper range, their agent membership remains a fence; the card states that the actor’s order now chooses among those agents.
- **Partner agent lists:** remain permission fences, and a piece’s named agent remains binding.
- **Brainstorm agent lists:** retain participant selection and permission semantics. They are not reinterpreted as fallback lists.
- **Composer picker:** remains a one-conversation override. Changing it records a personal choice and retains it across resume; it does not edit the standing actor range.

A new reader of an old `driver.json` creates nothing on read and projects the old behaviour. A malformed explicit range holds the affected start with its error; it does not silently revert to legacy defaults. A newer unsupported range version is likewise unreadable, not permission to guess.

An older Daoris build cannot honour actor ranges. Preserve its legacy keys and disclose that rollback uses those previous choices. Do not claim that a new version marker can constrain an already shipped older reader.

### 4. Where it lives and what the person sees

Ranges are **machine-wide**, under **Settings → AI features / AI 功能**. Do not add workspace or repository range editors now. Existing scoped rules still govern whether work starts, allowed agents and opinion independence.

`Web/settings/AiDomain.tsx:27` already separates machine jobs from the shared service’s search tier. Extend that location; a browser on a shared host continues to see no machine ranges.

The resting screen shows short summaries. Only the selected actor opens its list.

```text
Settings › AI features

Actors                                      [Ask Daoris]

Development       Current setup: Claude Code · Account default   [Edit]
Exact work        Same as development                            [Edit]
Intake            Off                                            [Edit]
Second opinion    Uses this work's reviewer order                 [Edit]
Ask Daoris        Codex · Account default                         [Edit]

[Other actors]

Search            Words only
```

A selected editor:

```text
Exact work
For new work on this machine

1  [Claude Code] [sonnet] [tool's offered effort]   [Up] [Down] [Remove]
2  [Claude Code] [opus  ] [high                 ]   [Up] [Down] [Remove]

[Add option]
Later options run only when earlier ones cannot start.
Accounts: Daoris's accounts, using their existing order and use settings.

[Save] [Never mind]
<refusal stays here>

Usage · last 30 days                                      [Open]
daoris driver actors …
```

Use `SettingList`, the existing label track, container-based stacking and D185’s inline refusal pattern. Save and cancellation remain in place until the answer. No drag-only ordering.

The session head keeps one compact provenance line, folded details, and one fallback sentence when needed:

```text
working · Repair the import

Exact work · Claude Code · <reported model> · <reported effort>
Fallback: option 2; option 1's accounts are cooling.        [Details]

[existing work and wait controls]

Details
Requested       sonnet · tool default
Selected        option 2: opus · high
Reported        <model> · effort not reported
Account         <local account label>
Usage           Input … · Cache read … · Cache write … · Output …
```

Before the agent reports a model, say **Not reported / 未报告**. An accepted request or a successfully set config option is not proof of inference usage. The detail distinguishes requested, acknowledged and reported values. If different models answered turns, show **Multiple models / 多个模型** and their attribution.

A reported model outside the accepted choice is a visible discrepancy. Record it immediately and hold the next dispatch for a choice; do not normalize it into success. A chosen alias resolving to its documented model is not such a discrepancy. Provider-internal helper models are disclosed where reported; Daoris cannot promise to control an agent’s internal model calls.

New conversation preserves the person’s start override:

```text
New conversation

[Repository] [Tree] [Chat options ▾]

Chat options
Use actor options
—or—
Agent [Codex]   Account [automatic]   Model [offered value]
For this conversation

[words …]
[Start]
```

A person’s explicitly selected agent, model or account wins for that start. An explicitly pinned account waits if unavailable; it does not quietly become another account. A hand-over’s existing account-preference semantics remain stated separately. An explicit one-time choice outside the standing range is authorized by that start, not added to the range.

When no option is ready:

```text
No option ready
1  Claude Code · sonnet   Accounts cooling; first reset 14:30
2  Codex · chosen model  No signed-in eligible account

[Accounts] [Edit options]
Your words are kept.
```

Use the existing account-wait surface where sufficient. A new actionable wait must enter the shared head/foot `WaitProps`, .NET attention judgement and badge count together; an automatic cool-off alone must not masquerade as a request for confirmation.

The agent’s page keeps account defaults in its existing fold:

```text
Model and effort
<account>       <account default values>                 [Edit]

Explicit actor options take precedence for Daoris starts. [Actors]
Usage                                                     [Open]
```

This preserves `AgentPage.tsx:772–814` without duplicating actor editors on every account.

**Cost means measured consumption, not money.** Use existing turn tokens, `SessionSpent`, account windows and usage history. The session shows its token categories and context high-water separately. The actor’s Usage disclosure aggregates runs over a stated period, with agent/model breakdown, fallback counts and missing-measurement counts. It includes subordinate work once, not both its totals and its parent’s aggregate.

Never turn context occupancy into spend, sum incomparable token categories into a price, distribute an unsplit mixed turn by guesswork, or show missing data as zero. `TurnModels` already carries the split’s source and whether it covers the whole (`Accounts/Usage/TurnModels.cs:9–32`); preserve those qualifications.

Leave out a new Actors navigation place, named presets to manage, routing graphs, price tables, per-error fallback editors, account selectors inside each standing option, and a model-by-role matrix. One list in one selected row is enough.

A quest, workflow or ask may name a **portable work role**, such as exact work, but never a machine’s option number, account or model. The local driver validates that role against the kind of start. Default to development where no classification exists. Exact work requires explicit, settled scope or a person-approved mapping; an intake’s unreviewed judgement must not automatically downgrade arbitrary work. Selecting a role does not change its permission shape.

### 5. Ask Daoris

Add **`actor_ranges_propose`**, alongside `agent_settings_propose`. They do different things: the first changes Daoris’s standing choices; the second changes a particular account’s tool defaults.

The new tool accepts a bounded structured patch for one or several actors, including their ordered options and expected current revision. It can propose:

- One actor’s list.
- A coordinated set of lists.
- The dated suggested set, resolved to what this machine can honour.
- Returning an actor to the visible compatibility/default choice.

It cannot turn a fallback list into permission to sign in, enable full access, widen helper/partner rules, change account membership, or bypass partner admission.

Restricted-mode card:

```text
Ask Daoris · restricted

Change actor options
Exact work
  Before: Claude Code · Account default
  After:
    1 Claude Code · sonnet · <offered effort>
    2 Claude Code · opus · high

New work only. Option 2 can spend more.
Other actors and account settings stay as shown.

Terminal: daoris driver actors …

[Apply] [Dismiss]
<stale or unavailable choice is explained here>
```

The driver revalidates the patch at Apply. If the actor’s list changed meanwhile, require a refreshed card; do not overwrite it. Unrelated changes may be preserved under the existing keep-lock edit contract. Apply the entire reviewed patch atomically.

In **restricted mode**, proposing remains available; direct setting requires a specific configured-access grant that actually covers these operations. Do not silently widen an older generic settings grant to include a new spending choice.

In **full access**, D183 permits direct acts without proposal cards. Ask Daoris may therefore apply an explicitly requested range change through the same driver operation and report the receipt. Full access does not authorize it to invent new spending preferences: adding models, agents or newly enabled jobs beyond the person’s instruction remains a proposal for acceptance under D191 point 3. A request to “help reduce token use” authorizes analysis and a concrete suggestion, not an unbounded financial policy.

Changing Ask Daoris’s own range affects its next new conversation. It does not replace the running conversation to make its own recommendation true.

Implement the tool through the existing proposal-kind and connector censuses, scope checks, room facts, coverage rows and goldens. `Mcp/HelpAccountTool.cs:10–31` and `Driver/Help/Proposals/HelpAccountProposals.cs:10–20` are the relevant existing shape; prose in the room alone is not a setting door.

### 6. The records and terminal

Keep standing choices in **`data/driver.json`**, using the existing `HomeEntry.DriverConfig`. Add a versioned `actors` object with a default range and explicit actor overrides. Inheritance is only from that default, never a graph of actor references. Independent jobs such as intake and Ask Daoris do not start following another actor merely because their entry is absent.

`DriverConfig` owns parsing, effective-choice projection and preserving writes. `StandingChoices` owns the operation used by the window, terminal and Ask Daoris. Keep the `JsonKeeping` lock-and-merge contract, unknown fields and deterministic ordering; update the keeps fixtures with the schema.

Provide these terminal forms through the driver family:

```text
daoris driver actors
daoris driver actors show <actor> [--json]
daoris driver actors set <actor> --option <agent> <model> [--effort <value>] …
daoris driver actors reset <actor>
daoris driver actors apply <file>
```

`--option` repeats in preference order. Account-default selection has an explicit flag rather than a magic model string. `apply` accepts the same versioned, revision-checked patch as the proposal tool. Every form prints the effective order and its constraints.

The CLI forwards the family, following D194; do not recreate a TypeScript writer. Reads are `Open`; standing-choice writes are `Person` in both pipe censuses and use the normal confirmation path. D201’s standing terminal confirmation covers none of these changes.

For what ran, add a machine-local **actor-run record**, keyed by session/part and run attempt, under a declared `data/actor-runs/` entry. A brainstorm uses its existing local turn identity. Record:

- Actor and range revision.
- The effective candidate order and constraints.
- Selected option, agent, account and resolved harness version.
- Earlier attempts and structured reasons.
- Requested values, the agent’s acknowledgement and the mechanism used.
- Reported model/effort with source and time, or explicit absence.
- Links to its continuation and measured usage.

Write the selection before work is dispatched. A crash between selection and dispatch must remain “selected, dispatch not established,” not a completed run. Freeze each run’s choices; editing settings never rewrites history.

This local record reaches the window and terminal through the desktop bridge. **It does not add model names to the service’s synced session record.** `TurnModels` reaches the screen and the local actor-run summary, while its detailed per-turn counts remain in usage history. Keep enough summary provenance to explain an old session after the history’s 90-day retention expires.

The three home twins must declare the new entry, in the same order and with matching tests:

- `src/Daoris.Cli/src/platform/home.ts`.
- `Driver/Platform/DaorisHome.cs`.
- `Core/Platform/DaorisHome.cs`.

Their agreement is about the home’s layout and lifecycle, not permission for a shared host to serve actor choices. Add actor-run files to unit inventory, history clearing and retention; no orphaned private sidecars.

Model-bearing provenance must not enter portable notes as raw values. Keep the shared ledger’s `Adapter` as the agent name, and keep model values absent from `FedSessionRecord`, `SessionWire`, sync and mirrors. Local explanatory events can be worded through bilingual codes. Add explicit tests that neither those events nor their parts leak through the shared feed.

Partner admission gains the complete selection mechanism, model and effort in its configuration fingerprint. Today `Conversations/Domain/PartnerConfiguration.cs:40–77` hashes posture and rules but not these choices. An old measured digest must not admit a newly configured model. Check admission before the piece’s instruction, and recheck after any negotiation that changes the effective configuration.

Every attempt still uses `AgentStarts.StartAsync` and D201’s launcher (`Loop/Driver.cs:3161–3180`). Fallback never creates an alternate uncontained spawning path. Preserve `agent.contained: false` where containment cannot be established; a partner remains refused unless its stricter admission succeeds.

### 7. Amendments and rejected alternatives

Append amendments; preserve each decision’s original evidence.

| Decision | Amendment |
|---|---|
| **D24** | The deployment may delegate selection to Daoris through person-accepted actor ranges. Feature definitions remain independent of model names. “Tier that ran” distinguishes selection from reported execution. |
| **D98** | Replace “Daoris chooses none” with bounded selection from accepted options. Extend session configuration to every appropriate start and resume. Permit measured per-start overrides; retain tool vocabulary, no registry, no `ANTHROPIC_MODEL`, no writes to the person’s tool home and no standing `max`. |
| **D57 / TOOL3**, and **D179’s usage attribution** | Model attribution may be displayed through the local bridge and retained in local run provenance. No price table or remote model record is introduced. |
| **D89 / D110** | Ask Daoris’s independent actor has an ordered range and a structured proposal door. Its no-agent starters remain. |
| **D155** | Each reviewer can have model/effort alternatives. Maker independence, scoped reviewer order, same-agent labelling, findings and gates stand. |
| **D163 / D189** | Continuations retain actor and selection provenance. Initial-start availability fallback is distinct from a hand-over. A running session’s agent is never changed by the range alone. |
| **D174** | Its reference seeds suggestions and trials; it is not copied into an unconditional product model table. |
| **D176** | The synthesizer uses its accepted actor range rather than implicitly inheriting the first answering generator. Participant count, frozen packets and disclosure approval stand. |
| **D199** | Model, effort and selection mechanism participate in effective-configuration admission. No measured row is broadened by implication. |
| **D172 / D195** | Declare the local provenance entry and its inventory/retention behaviour in all three twins. |
| **D116** | Add actor and the new job names, meanings, element kinds and bilingual keys. |

**D183, D185, D191, D197 and D201 stand.** The design applies their access, simplicity, acceptance, account-pool and containment rules. It does not interpret a model range as an amendment to those boundaries.

Reject:

- **A model deciding which model should run every start.** It adds spending and makes routing hard to reproduce. Use explicit job identity and deterministic selection.
- **A hidden “Auto” pool.** A person cannot approve its members or explain a fallback.
- **Cheapest-first routing or automatic quality escalation.** There is no price table, and the reference judges completed work, not price alone. Availability fallback must not become a repeated quality-repair loop.
- **Account defaults as the sole implementation.** Accounts serve several actors; rewriting one races with other work and cannot express per-start intent.
- **Trying a later model before another eligible account of the preferred option.** It reverses the person’s preference for convenience.
- **Treating a provider’s content refusal as permission to try another maker.** That is not an availability failure and may resend work already accepted.
- **A new actor for every workflow node or lifecycle transition.** It multiplies settings without changing the job.
- **Automatic orientation before every build.** The pack may already exist, and an extra scout can consume more than it saves.
- **Automatic migration to the suggested models.** Development evidence is not the owner’s acceptance of a new spending policy.
- **Persisting model names in portable session notes.** Local provenance answers the question without crossing the machine boundary.

### 8. The rows, in build order, and their proofs

Each row lands an observable feature through all its doors. The proofs below are acceptance criteria; stub results do not constitute installed-window evidence.

| Row | Landable outcome | Required proof |
|---|---|---|
| **MODELROLE1a — Every start says its actor and what ran** | Tag all existing starts, add local provenance and the compact head/details/terminal view, with routing unchanged. Include brainstorm turn records and all continuation paths. | A start census covering the brief’s **13** paths; unknown and mixed model attribution; restart and history retention; sync and note-redaction tests; all three home twins and clearing inventory. Inspect the installed head in English and Chinese. |
| **MODELROLE1b — Development and chat use ordered options** | Ship range persistence, Settings editor, terminal and Ask Daoris proposal door, plus selection for development/chat and their continuations. Explicit options are available only on mechanisms already proved. | Table tests for nested option/account ordering, `keep`, cooling, sign-out, entitlement, pins, explicit picks, cancellation and stale patches. ACP stub proves model-before-effort, no prompt before acknowledgement, and resume restoration. Native tests prove argument/settings composition and conflicting-command refusal. Measure supported mechanisms on the install before enabling them. |
| **MODELROLE1c — Intake and Ask Daoris keep independent ranges** | Replace their scalar controls with compact range summaries, preserve off/no-agent behaviour, and apply ranges to placed starts, start-from and go-on. | Old-file fixtures preserve exact absence semantics; queued words survive failed selection; bounded room preparation still works; restricted/full access and required-posture tests; unsupported full-access agent is never quietly narrowed. Installed fallback and failed-open cases. |
| **MODELROLE1d — Opinions and helpers use their allowed alternatives** | Add model alternatives without losing scoped rules, independence, helper limits or actionable waits. Show fallback on both the subordinate record and its work. | Extend `ReviewerChoiceTests` and helper tests for maker passes, scoped reviewer order, repeated same-agent models, unknown makers, all-unavailable outcomes and contributor families. Required opinions still hold. Head/foot/attention/badge parity, room coverage and bilingual stories. |
| **MODELROLE1e — A partner’s selected configuration is admitted** | Resolve options for the named partner, include the selection in its fingerprint, and expose its actual provenance and usage. | Digest changes for model, effort and mechanism; stale measurements refuse; every alternate account/configuration is rechecked. Existing clone/import and descendant-lifetime tests remain. Deterministic confinement probes on the install precede a production measurement row; a test row admits no production start. |
| **MODELROLE1f — Brainstorm proposal and synthesis have separate choices** | Preserve participant count and sealed generators while giving synthesis its own range. Record every pass and its bounded repair. Future meeting starts must carry an actor before they can launch. | No fan-out from fallback options; no substitution outside named participants or disclosure approval; synthesis selection independent of completion order; same packet on repair; deadline/cancellation and once-only delivery tests. Installed packet, fallback and provenance inspection. |
| **MODELROLE1g — Orientation packs reduce repeated reading** | Add the orientation start and exact-work classification. First reuse valid knowledge packs; otherwise an explicitly enabled scout returns a bounded pack with source revisions and ranges. Development remains the fallback when no pack can safely be supplied. | Pack invalidation on source revision and captured working-state changes; bounds, provenance and read-only enforcement; no scout on a valid hit; no automatic cheap classification of judgement work; cap accounting. Measure scout plus builder consumption against the same work without the pack. |
| **MODELROLE1h — Suggestions are earned and accepted** | Ship the dated suggestion preview, explicit acceptance receipt and promotion procedure. Never change existing ranges during a trial or an update. | Migration/idempotence and rollback fixtures; offered vocabulary versus unavailable recommendation; no added agent/model/job before acceptance. Installed comparison of selected and reported values, fallback reasons and token categories. A stale card cannot replace newer choices. |

Across these rows, maintain the bridge route census, publication marks, pipe classification and wording, proposal-kind/tool registration, permission denials, Ask Daoris coverage and regenerated room goldens. Extend the start census so a new production start cannot omit an actor or bypass the shared selection path. Reuse and extend `AgentStartsTests`, `AgentStartsProcessTests`, account-walk, ACP, continuation and partner-configuration suites rather than proving only the new resolver in isolation.

UI verification includes keyboard ordering, inline failures, long provider IDs, English/Chinese, light/dark and D185’s wide, half and 400 px layouts. Rendered geometry checks and installed-window inspection are separate requirements. The session head must retain its conversation-space budget with details closed.

Before promoting a default, measure representative work on the installed agent versions: requested versus reported configuration, turns and token categories, latency, first-run gate results, confirmed findings, rework and the resulting window. Follow D174’s dated trials over more than one sample; judge total orientation-plus-worker consumption. No narrower account window, cheaper-looking name or synthetic benchmark alone earns promotion. Integration runs the reached component gates and the full declared boundary checks, including `npm run verify`; real-process suites and rehearsals follow the repository’s serial execution rules.

The shape is one job identity per start, one ordered range per actor, and the existing account walk beneath each option. Current installations keep their choices until the owner accepts a concrete change; every fallback and every reported tier remains visible. Settings stays a short list with one editor open, while Ask Daoris helps compose the same reviewable choices the window and terminal can apply.

<!-- An independent Opus 5.5 reader wrote this proposal from the brief alone, never opening this file, in Fable's place (D191's amendment of 2026-10-23). -->

## Opus's proposal (an independent reader)

> Written from `docs/2026-10-23-actors-brief.md`, the decisions it cites (D24, D57, D89, D98, D130, D155, D163, D174, D179,
> D183, D185, D191, D193, D197, D199, D201, D203) and the code. No other proposal was read. Paths are abbreviated as the
> brief abbreviates them (`Driver/`, `Modules/`, `Core/`, `Mcp/`, `Web/`), and line numbers are those at `aad5dd409`.

**The shape in one breath.** An *actor* is a kind of start. Its *choices* are the person's ordered list of agent, model and
effort. A start takes the choices in order. For each choice it walks that agent's accounts by D197's walk, unchanged, and takes
the first choice that can run. The model is carried at the session's own level: the protocol's `session/set_config_option`, or
the pipe door's `--model`. The start then records the model the agent reported, beside the model that was asked. On the first
start after the upgrade every actor's choices are read from today's keys, so nothing spends differently. A suggested set,
drawn from `docs/model-roles.md`, is offered once and applied only by the person's press. The repository's rules still say
*who may* do a job; the actor says *with which model*.

### 1. The actors

#### 1.1 The word

- **English: *actor*.** It is the owner's word.
- **Chinese: *角色*** (the part a hand plays in the company, D193 part 4). It appears nowhere in the Chinese chrome today.
- **Considered and rejected:**
  - *岗位*: it reads as a post one hires a person for.
  - *演员*: a performer.
  - *代理*: the glossary already bans it for *agent* (`glossary.json:581`).
  - *人设*: a chatbot's persona.
- **English *role* was also rejected as the name.** D203 already reads a co-worker's *role* (reviewer, partner, helper) from its
  record. D174's roles are this development's dispatch, the source of the suggestion (§3.2) and not the product's word.

The glossary gains three terms:

| Term | en | zh | Means | Avoid |
|---|---|---|---|---|
| actor | actor | 角色 | A kind of work Daoris starts an agent for, with the person's choices of agent, model and effort for it, in order | en *model role*, *profile*; zh 演员, 岗位, 人设 |
| choice | choice | 选择 | One entry of an actor's order: an agent, and its model and effort, or the account's default for each | en *option* (an agent's config option, `work.options.*`), *fallback*, *range*; zh 选项, 备选 |
| account default | account default | 账户默认 | What an agent runs when a choice names no model or effort: the account's own settings, the cascade of today | en *its own*, *inherit*; zh 默认模型 |

*Range* stays a design word and never appears on the page. The person sees *choices* *in your order*.

#### 1.2 The list

Eight actors. Each one covers the starts of §4 of the brief. A continuation (starts 2, 3, 4, 12 and 13) keeps the actor of the
record it continues, so a quest handed over is still *Build* and a chat handed over is still *Chat*.

| Id (wire, terminal) | en (`field` 36/14) | zh | What it does | Starts (brief §4) | Chosen today by |
|---|---|---|---|---|---|
| `build` | Build | 开发 | A quest's session builds in its tree, commits and closes. Covers a workflow's *work* and *look* nodes (`WorkflowGraphRun.cs:314-316`) | 1, and 2 and 3 for its line | `handedQuest?.Agent ?? config.Adapter` (`Driver.cs:1671-1672`) |
| `intake` | Intake | 受理 | Reads an ask the declarations left, publishes quests, asks where it cannot tell | 5 | `config.IntakeAdapter` (`DriverIntakes.cs:111`) |
| `opinion` | Second opinion | 第二意见 | Reads a candidate in a clone of its own and gives findings. Covers the recheck and a workflow *opinion* node | 6 | the walk over `reviewers` (`ReviewerChoice.cs:205-235`) |
| `helper` | Helper | 助手 | Answers one question, read-only | 7 | `HelperChoice.Candidates` (`HelperChoice.cs:15-30`) |
| `partner` | Partner | 搭档 | Writes one piece in a clone, contained | 8 | named by the ask, admitted by a measured row (`PartnerChoice.cs:17-30`) |
| `brainstorm` | Brainstorm | 头脑风暴 | Generators propose, and the first that answered synthesizes | 9 | request, then policy, then `config.Adapter` (`DriverBrainstorms.cs:345-358`) |
| `ask-daoris` | Ask Daoris | 问道衍 | Talks about Daoris itself; proposes, or acts under full access | 11, and 12 and 13 for its conversations | `config.HelperAdapter` (`HelpRoutes.cs:52`) |
| `chat` | Chat | 聊天 | The person's own conversation in a repository | 10, and 4, 12 and 13 for chats | the request's agent, else `config.Adapter` (`ConversationRoutes.cs:48`) |

**Names.**
- *Build* / 开发 is new as an actor's name. It avoids 构建, which the chrome already uses for build output
  (`note.started.tree`'s 构建产物).
- Every other name is already a glossary term or proper noun: *intake* 受理 (`glossary.json:482`), *second opinion* 第二意见
  (`:535`), *helper* 助手 (`:539`), *partner* 搭档 (`:537`), *Ask Daoris* 问道衍 (`:628`), *chat* 聊天 (`:477`).
- *Brainstorm* / 头脑风暴 is already in the notifications (`notifications.json:55`) and enters the glossary.
- Every name also fits a `status` (16/5) for the head.

**How the actors relate to D203.** Every co-worker role (reviewer, helper, partner) is an actor. Ask Daoris, intake and a
brainstorm's pass are actors but not co-workers (D203 point 1).

#### 1.3 What is not an actor

- **The usage readers, and the login, install, update and version probes** (`ClaudeUsage.cs:38`, `CodexUsage.cs:115`,
  `HarnessActions.cs:502-546`). They do no work.
- **A native subagent**, an agent inside a session's own process (D203 point 9). Its model is set by the tool's own
  frontmatter. Its tokens already arrive in `TurnModels`' split as a helper model (`TurnModels.cs:16-17`).

#### 1.4 The named kinds the code does not start

- **Orientation.** There is no start for it today. An actor is a kind of start, so it has none until a start exists. The row
  that builds the scout-before-build start (row j, §8) names it, with *Orientation* as the candidate name.
- **A workflow node.** It has no actor of its own. Its *work* and *look* run as *Build* and its *opinion* as *Second opinion*.
  A node naming an actor is left out (§4.6).
- **A set-up.** It spawns no agent (`SetupWorld.cs:79`). The asks it publishes (`:73-74`) go through *Intake* and *Build*.
- **A meeting.** No agent takes a turn yet (`DriverMeetings.cs:26-27`, `:249`). When `MeetingTurns` lands, its turns become an
  actor, named in that row and defaulting to *Brainstorm*'s choices, since both put a question to the machine's agents.

#### 1.5 The person's own conversation is an actor: *Chat*

Why:
- Today the only way to set a chat's model ahead of time is the account's `settings.json` (`AgentSettings.cs:96-168`). That
  also moves every driven start on the same account.
- The intake's own reasoning applies: two jobs must not quietly move each other (`DriverConfig.cs:419-423`).

What it does: *Chat*'s first choice preselects the agent of a new conversation, and its model is set at the open. Two things
override it:
- the person's pick at the start (D130 point 4);
- the picker beside the composer (`SessionOptions.tsx:22-31`), which keeps working exactly as D98's second level made it.

### 2. The range

#### 2.1 Its shape

An actor holds **one to four choices, in order**. Each choice is `{ agent, model?, effort? }`:

- **`agent`.** An adapter name: `claude-code`, `claude-code-acp`, `codex-acp`, `dsh`, or a plugin's harness. It must be one this
  machine has.
- **`model`.**
  - Absent means *account default*: nothing is passed, and today's cascade decides.
  - Otherwise it is one word in the tool's own vocabulary:
    - **The pipe door:** Claude Code's SDK alias list (`AgentSettings.cs:58-59`) or a full id typed in a free field.
    - **A protocol door:** a value the agent's `model` option offered, as Daoris last read it (§6.2), or the free field.
  - Daoris adds no word of its own and keeps no list (D57, D98).
- **`effort`.**
  - Absent means *account default*.
  - **The pipe door:** one of the four efforts Claude's settings keep (`AgentSettings.cs:65`).
  - **A protocol door:** a value its `thought_level` offered. *`max`* is allowed there where the agent offers it. D98 refuses
    `max` only as a file default (`AgentSettings.cs:185-199`), and here it is a session's option.

**Rules, judged in one place for every door (`ActorChoices.Judge`):**
- At most four choices, since a longer order is a list nobody reads. The ordered lists the survey found are all short (brief §6).
- No two choices are the same.
- An agent this machine lacks is refused, in the words `AgentSettings` uses for an unknown agent.
- *Off* is allowed only for *Intake* and *Ask Daoris*: an empty list, read as today's *absent means off*
  (`DriverConfig.cs:405-423`, `:434`).
- *Build* and *Chat* need at least one choice.
- *Second opinion*, *Helper*, *Partner* and *Brainstorm* may be empty. Their rules and today's fallbacks then decide (§2.4), at
  account default.

**How a Codex or dsh model is honoured without writing their configuration.** It goes through their own offered option on the
protocol door (§2.2). Daoris never writes `CODEX_HOME` or `DSH_HOME` (D98's boundary). Two cases:
- Where an agent's wire offers no `model` option, its only choice is *account default*, and the editor says so.
- If the probe (row a) finds that `codex-acp` offers one option combining model and effort, its values are offered as models,
  and no effort is offered beside them.

#### 2.2 How a start hands the model over

**On the protocol door** (`claude-code-acp`, `codex-acp`, `dsh`):
- **The steps.**
  1. `session/new` runs, then the posture (`SetPostureAsync`, `Acp.cs:944`; on a resume `:2624`).
  2. Before the first prompt, the driver sends `session/set_config_option` for the `model` option.
  3. It then sends it for `thought_level`, read from the agent's answer to step 2, since an effort's levels follow the model
     (`Acp.cs:516-521`).
- **What may be sent.** Only a value the agent offered in that answer, and only in the categories `AcpConfigOption.ThePersons`
  allows (`Acp.cs:82`).
- **Where it is wired.**
  - The driven `AcpSession` is opened with no options today (`Driver.cs:3775`). It gains the asked choice, as the chat's session
    already gains an options callback (`ChatRunner.cs:2185-2189`).
  - `SetConfigOptionAsync` (`Acp.cs:527-539`) is reused as it is.
- **What is kept.** The agent's answer is what it set, in its own words, and that is what gets recorded.

**On the pipe door** (`claude-code`):
- **The model.** `--model <value>` is appended after the person's command, as the other flags are (`Adapters.cs:2086-2092`,
  `:2172-2184`). If the person's own `commands[claude-code]` already names `--model` (`Driver.cs:2583`), nothing is appended:
  their command is more specific. The record says so.
- **The effort.** It goes as `effortLevel` in the start's own settings file. That file is the command-line tier the rules already
  ride on: `HandSettings` (`Adapters.cs:2216-2225`), written by `SpawnSettings.Write` in `HandRules` (`Driver.cs:2672-2720`).
  `max` is not offered on this door until a flag is measured (row a), because settings never keep it.

**Why the session level, given D98's cascade.**
- `session/set_config_option` is the person's own `/model`, and `--model` is the tool's own startup flag. Only managed policy
  ranks above them.
- So a choice that **names** a model outranks, for that start:
  - the account's `settings.json`;
  - a repository's own `.claude/settings.json`;
  - `ANTHROPIC_MODEL` (`AgentsCommand.cs:822` says today that the last two outrank the account's).
- That is deliberate. The actor is the person's deployment choice for that kind of work: D98's "for an agent a person runs, the
  deployment is the person".
- A choice that names **no** model passes nothing and leaves the cascade exactly as it is today.
- The protocol door's mechanism is the same on every agent, so the three protocol-door agents are honoured alike.

**Rejected carriers:**
- **The account's `settings.json`.** Every actor running on that account shares it. Two concurrent starts on one account for two
  actors would race on the tool's own file. A repository's settings would also outrank it, so the choice could quietly not hold.
- **`ANTHROPIC_MODEL` at spawn.** D98's rejection stands. It is Claude-only, and it is inherited by every child the session runs.
- **`_meta.claudeCode.options.model`** (`Adapters.cs:1790-1811`). It is Claude-only and its precedence is unmeasured.
  `set_config_option` does the same job on every agent.
- **The tool's own `--fallback-model`.** It switches models inside a turn, at a moment Daoris cannot report. The turn would
  read `mixed` and nobody would have said why.

#### 2.3 When the agent does not offer the choice

- **Judged before the start.** The driver keeps what each agent and version last offered (§6.2). A choice whose model that
  reading lacks is passed before anything opens, with the code `not-offered`.
- **Caught at the open.** Where no reading exists yet (a first run, or a new version), the open reads the offered options from
  `session/new`. A model that is not among them stops the open before the first prompt.
  - The part ends `failed` with `choice-not-offered`, and **no strike** is counted, just as a limit is not a strike (D125).
  - Nothing is spent beyond the spawn.
  - The reading is kept, so the next start passes the choice before opening.
- **A refusal.** If the agent refuses `session/set_config_option`, the choice is `refused` and handled the same way.
- **An effort that is not offered.** The choice runs at *account default* effort, and the head says so. A model decides what
  runs and what it costs. An effort is a dial within the chosen model, and running without it is exactly today's behaviour.
- **On the pipe door** there is no offer list:
  - The value is judged as one word, as `AgentSettings` judges a model.
  - The words an unknown `--model` prints are measured in row a, then read like the toolchain's `Refused` words
    (`Adapters.cs:2290-2300`) as `refused`, with no strike.

#### 2.4 The walk: all of a choice's accounts before the next choice

```
for each choice of the actor, in order:
    pass it, with its code, if it cannot run here:
        the agent is not installed (unavailable); its reading lacks the model (not-offered);
        Ask Daoris is in full access and the agent has no full posture (no-full-access, D183, Adapters' FullPosture);
        a partner's configuration is not admitted by a measured row (not-admitted, D199);
        the repository's rule does not list the agent (not-listed, §2.8)
    otherwise ask that agent's account walk, unchanged (HarnessRoster.SelectAsync, :883-886;
        AccountRotation.Order, cool-offs, sign-ins, keep, curve, the cap, D197):
        allowed → this choice, on this account
        held    → pass it, with the walk's own reason (cooling until …, signed out, refused, kept for conversations)
no choice allowed → the start waits (§2.6)
```

**Why choice-major.**
- The person's order is about who does the work. An agent's accounts are interchangeable seats of one set (D197 point 1).
  Taking a later choice while a seat of an earlier one is free spends against the person's order.
- Going account by account first would only help where a cool-off belongs to one model. Daoris does not read such a cool-off
  today. `ClaudeUsage` reads `five_hour` and `seven_day` (`ClaudeUsage.cs:78`), though the same answer carries `seven_day_opus`
  and `seven_day_sonnet` (`ClaudeUsage.cs:41`). Reading those is row k.

**What the walk keeps exactly.**
- A start is counted only for the choice taken, so a passed choice takes no slot under the cap. The walk already tells a panel's
  question apart from a start (`ChooseAsync`'s `counts`, `HarnessRoster.cs:1002-1004`, used as a panel's at `:1786`).
- Driven work never takes an account kept for conversations, nor the person's own sign-in (D197 point 3).
- Rotation still moves only within one agent. Changing agent is still a hand-over (D163).

#### 2.5 Mid-session: nothing moves silently

- **A running session never moves** to another choice (D163; brief §5).
- **A part that ends on a limit** carries on as today. It goes on another account of the **same agent** through the walk
  (`CarriedOnAsync`, `HarnessRoster.cs:991`), with the **same choice** re-applied: its model and effort are set again at the
  open.
- **When every account of that agent is cooling**, D163's offer (point 3) names the actor's next choice on another agent:
  - Under `handOver: ask` (the default), the session's page asks: *Hand over to Codex · account default, Build's choice 2?*
  - Under `on`, the hand-over is taken.
  - Under `off`, the session waits for the cool-off.
- **A later choice on the same agent with another model** is never taken mid-line on a cooling account, because the cool-off is
  the account's.
- **A resume re-applies the record's asked choice** (`ResumeAsync`, `HarnessRoster.cs:924`). This closes the gap in the brief's
  §3.2, where a conversation going on did not re-apply an earlier choice.
- **A conversation's own change beside the composer wins** over the actor. It is a note on the record (D98), and the last such
  note is what a go-on re-applies (`ChatGoOn.cs:307`).

#### 2.6 When every choice is cooling or refused: the wait

- **Driven starts.** The start is held through today's hold path, and the quest's row line names the actor and the soonest way
  out:
  > *Build waits: none of its choices can run now. Claude Code's accounts cool until 14:05; Codex is not signed in.*
- **When a choice frees.** The wait is judged again at every look. The first choice that can run **in order** is taken: if
  choice 1's account frees first, choice 1 runs.
- **Conversations.** For *Chat* and *Ask Daoris* the same sentence refuses the press where it was made (D185 R3). No new press
  is added.

#### 2.7 D155: independence is by maker, never by model

- **A choice's family** is its agent's, an owner and a maker (D155 point 4). The model never makes a reviewer independent.
- **The second opinion's walk** keeps its passes (`ReviewerChoice.cs:173-178`):
  1. Another maker's choices first, in order.
  2. Then a maker that is not declared.
  3. Then the work's own family, only where the order lists it. It is labelled *the same agent, fresh*, and the label now names
     the model too (*the same agent, fresh: opus*).
- Claude Code on a stronger model is still Claude Code. The label says so, and nothing implies otherwise.

#### 2.8 The rules say who may; the choices say with what model

`opinions.reviewers`, `helpers.agents`, `partners.agents` and the brainstorm policy's agents are the repository's or workspace's
say over **who may** do a job. They are classed as the person's re-wiring in the census (`PipeCensus.cs:72-79`), and they stay.

**How the two compose:**
- **Where a rule names agents,** the rule's order of agents stands. Each agent takes its model and effort from the actor's
  choices for that agent, in their order, or *account default* where the actor has none for it.
- **Where a rule names none,** the actor's choices are the order.

**Per actor:**
- **Second opinion.** `rule.Reviewers` (`ReviewerChoice.cs:205-211`) expands each named agent into its choices. An empty list
  reads the actor's choices.
- **Helper.** An agent the ask names (if the rule allows it) takes its model from the range. With no rule list, the actor's
  choices replace *every agent of another maker* (`HelperChoice.cs:24-27`), another maker's first.
- **Partner.** It is still named by the ask (D199). The actor supplies only its model.
- **Brainstorm.** The voices come from the request, then the policy, then the actor's choices in place of `config.Adapter`
  (`DriverBrainstorms.cs:348`). A choice that cannot run is passed, and the next unused choice fills its voice, up to
  `MostAgents`. The synthesizer is unchanged.

**Why the rules are not replaced:** a range that replaced them could let an actor widen who may act. A partner writes, and
adding an agent to a partner list is a permission.

#### 2.9 The pool and rotation (D197)

Unchanged inside each choice:
- one set per agent;
- one order and one set of use settings;
- the person's own sign-in outside, and never used for driven work;
- `keep`, `switch`, `curve` and the cap.

An actor chooses the agent, model and effort. **Which account** stays the walk's.

#### 2.10 What is said when it falls back

1. **In the conversation's first lines.** The opening note gains an actor line beside *Opened on `account`: why*
   (`note.json:127`):
   > *Ran as Build's choice 2, Claude Code · sonnet: choice 1, Claude Code · opus · high, was refused by account-1 and
   > account-2.*
2. **On the head.** A `status` chip *choice 2* / *第 2 选择* on the facts line, and the actor row in *Details* (§4.3).
3. **In the machine log:** `actor.passed` per choice passed, with its code; `actor.chose`; `actor.ran`.
4. **In the terminal loop:** *started #412 on claude-code (account-2) as build's choice 2: …*

Nothing goes to Notifications or *What needs you*. A fallback asks nothing of the person. A pattern of fallbacks shows on the
actor's use line (§4.4), where Ask Daoris can read it (§5.4).

#### 2.11 What is never done silently

- **No agent, model or effort outside the actor's choices runs,** with two exceptions, each shown as what it is:
  - the person's pick at a start (D130 point 4), shown as *your pick*;
  - a choice read from today's keys before the person set any, shown as *as before* (沿用原设置).
- ***Account default* is a choice the person sees.** It is shown as such on the screen and on the head. It is not a hidden
  "nothing".
- **A model the agent substitutes on its own** (a plan's limits, an overload) shows as *ran*, beside *asked*. Daoris never judges
  whether `opus` and `claude-opus-…` agree, because that would need a model list (D57). Both are shown and neither is judged.
- **A suggestion never applies itself** (§3.3).
- **A running session never moves** (§2.5).

### 3. Defaults

#### 3.1 The first start after the upgrade: exactly today

Without `actors` in `driver.json`, each actor's choices are **read from today's keys**, every choice at *account default*:

| Actor | Read from | Today's line |
|---|---|---|
| Build | `[adapter]` | `Driver.cs:1672` |
| Intake | `[intakeAdapter]`, or off where it is absent | `DriverConfig.cs:423`, `:1453` |
| Ask Daoris | `[helperAdapter]`, or off where it is absent | `:434`, `:1455` |
| Chat | `[adapter]` | `ConversationRoutes.cs:48` |
| Second opinion, Helper, Partner, Brainstorm | empty: their rules and today's fallbacks decide | §2.8 |

Nothing spends differently, so D191 point 3 is not reached by the upgrade itself.

#### 3.2 The suggestion: from `docs/model-roles.md`, in the tool's own aliases

**What it is.** A dated code table in the driver (`ActorSuggestion`), written only in Claude Code's own aliases
(`AgentSettings.cs:58-59`) and *account default*. It is held by a census to `docs/model-roles.md`'s roles table (§8's tests).
For the reference reviewed 2026-10-16, with the owner's word of 2026-10-23 that Claude Opus 5.5 does this development's
reasoning work for now:

| Actor | Suggested choices | The role it reads, and why |
|---|---|---|
| Build | Claude Code · `opus` · high, then Claude Code · `sonnet` · account default | build-judgement: Opus 5.5, high. Daoris cannot tell a judgement row from an exact one at the start (row i), so the safe choice per the owner's word leads; `sonnet` is there for an account that refuses `opus` |
| Intake | Claude Code · `sonnet` · account default, only where intake is already on | orient: it reads and locates. The suggestion never turns intake on |
| Second opinion | Codex · account default, then Claude Code · `opus` · high | review: another maker, else Opus high (D155's label on the second) |
| Helper | Codex · account default, then Claude Code · `opus` · high | decide-support: another maker, high |
| Partner | Codex · account default, then Claude Code · `opus` · high | another maker writes the piece. It is still named by the ask, so this supplies only the model |
| Brainstorm | Claude Code · `opus` · high, Codex · account default | decide-support: different makers' voices |
| Ask Daoris | unchanged | help: the person's choice (D89, D98) |
| Chat | unchanged | the person's own conversation |

**How it adapts to the machine.**
- *Claude Code* means the Claude door this machine already runs (`adapter`).
- Another maker's choices appear only where that agent is installed with an account signed in for Daoris. Otherwise they are
  left out, and the card says so (*Codex is not set up here, so its choices are left out*).
- *Codex · account default* is whatever the account's own configuration names. On the owner's install today that is
  gpt-6.1-sol at high effort, per `docs/model-roles.md`.

#### 3.3 Who accepts it, and in which words

The **person's press** accepts it. It is never applied by itself.

**The card.** AI features shows one card, once. Its terminal twin is `daoris driver actor suggested --use`.

> **en:** *Daoris can choose a model for each kind of work, from what this development measured: Opus for building and second
> opinions, Sonnet for intake, another maker first for opinions and helpers. It changes which models your accounts spend on.*
> **[Use these]** **[Not now]**
>
> **zh:** *Daoris 可以按这个开发过程实测的结果，为每种工作选择模型：开发与第二意见用 Opus，受理用 Sonnet，第二意见与助手先请另一家厂商的智能体。这会改变你的账户把用量花在哪些模型上。* **［采用这些］** **［暂不］**

The card lists each actor whose choices would change, before → after.

**What each press does.**
- ***Use these*** copies the suggestion into the person's own choices. They are not linked to the suggestion: a later suggestion
  never follows into them.
- ***Not now*** records the suggestion's date as answered (`actorsSuggestion`). The card stays away until the suggestion
  changes.

**D191 point 3.** On the owner's install, this press **is** the owner's yes for the spending the suggestion changes. No row
spends differently without it.

#### 3.4 Naming a model without a list (D57, D98)

The suggestion's only model words are Claude Code's own aliases, which Daoris already carries as the tool's (`AgentSettings.cs:58-59`).
Another maker's model is its *account default*. The table is not a registry:
- no versions;
- no capabilities;
- no prices.

It is one dated table from role to alias, and its single source is `docs/model-roles.md`, held there by a census. That is the
narrow amendment §7 records.

#### 3.5 Following a new model version

- **A newer model behind an alias.** An alias names a tier the tool keeps current. When Claude Code's `opus` moves to a newer
  Opus, the head's *ran* shows the new id and nothing in Daoris changes.
- **A different alias for a role.** For example, Fable for build-judgement, or Haiku for orient (`docs/model-roles.md` §On trial).
  This is promoted only through D174's trial in this development:
  1. `docs/model-roles.md` promotes the alias with its date and batch.
  2. The census fails until the suggestion follows.
  3. The next release offers the changed actors on the card, never applied.
- **A fixed version.** A person who wants one names a full id in the free field.

#### 3.6 Measured on the install before a suggestion changes

- **Per actor, over a batch,** read from the usage history, which now carries the actor (§6.4): starts, turns, tokens by model,
  and the share of starts on a later choice.
- **Per Build start,** the outcome D174 judges by: gates green on the first run, the second opinion's *must* findings, and
  whether the work landed or was sent back.
- **The baseline** is the week before the owner presses *Use these*, when the actors are read from today's keys and so equal
  today's behaviour. The comparison is the batch after.
- **The rule** (D174's rejection stands): no change of the suggestion by price or benchmark alone.

#### 3.7 Migration of today's single model per agent

- **An account's `model` and `effortLevel`** (D98's first level, `AgentSettings.cs:96-168`) stay what *account default* means.
  - They are still set from Agents → *Model and effort* and from `daoris agent settings`.
  - That fold gains one line:
    > *Used where an actor runs this agent on its account default; a model an actor names outranks it.* /
    > *角色以账户默认运行此智能体时使用；角色指定的模型优先于它。*
  - They are not copied into the actors' choices. One account's taste would become every actor's order, and the accounts of one
    agent may differ.
- **`adapter`, `intakeAdapter` and `helperAdapter`** become *Build*'s, *Intake*'s and *Ask Daoris*'s choices as read (§3.1).
  - Once the person sets those actors, the keys are **written through**: the first choice's agent, or `intakeAdapter` and
    `helperAdapter` absent while off. An older build therefore starts the same agent, at account default.
  - `daoris driver adapter|intake|helper <agent>|off` remain, and write a one-choice list at account default. The answer says
    what it replaced.
- **`opinions.reviewers`, `helpers.agents`, `partners.agents` and the brainstorm policy's agents** are unchanged, and are read as
  in §2.8.
- **The composer's picker** is unchanged. It opens showing what the actor set, because the agent answers with its current option
  after the open. A change is still the person's note (D98), and a go-on re-applies it (§2.5).
- **An old `driver.json`.**
  - A new build reads it as today (§3.1).
  - An older build reading a new file ignores `actors`, which `JsonKeeping` keeps on save (KEEPALL1). It reads the
    written-through keys and runs the first choice's agent at account default. A rollback loses only the model choice.

### 4. Where it lives

#### 4.1 Settings → AI features

The glossary already defines *AI features* as "the jobs Daoris gives a model or an agent of its own" (`glossary.json:651`;
`AiDomain.tsx:27`). The actors become its middle section:
- The *Intake* and *Ask Daoris* agent cards (`settings.ai.intake.*`, `settings.ai.helper.*`, `settings.ai.json:7-37`) fold into
  their actor rows.
- *Search and convergence* stays above, unchanged.
- *Ask Daoris's access* and its grants stay below, unchanged. That is a mode, not a model.
- The rows use `SettingList`'s label track (D185 R1).
- **Machine-wide** (§4.6).

**Screen 1: AI features, the Actors section**
```
AI features
The jobs Daoris uses a model for. Each does its part with none, and says which tier answered.

Search and convergence   words only: no embedding model named                       (unchanged)

Actors
Each kind of work Daoris starts runs on the first of its choices that can run, in your
order, and says so when it took a later one.                         [Use these]  ← only while a suggestion is unanswered

Build            Claude Code · opus · high   +1                                  Edit
                 14 starts this week · 2.1M tokens · 2 on a later choice
Intake           Off                                                             Edit
Second opinion   Codex · account default   +1                                    Edit
Helper           Codex · account default   +1                                    Edit
Partner          Codex · account default   +1                                    Edit
Brainstorm       Claude Code · opus · high   +1                                  Edit
Ask Daoris       Claude Code · account default   as before                       Edit
Chat             Claude Code · account default   as before                       Edit
Also `daoris driver actor`.

Ask Daoris's access      Restricted                                                 (unchanged)
```
Each row shows the first choice and *+n* for the rest. The use line under each row is the cost shown (§4.4).

**Screen 2: one actor's editor**, inline where *Edit* was pressed (D185 R3):
```
Build: your order
  1  [Claude Code ▾]  [opus ▾]       [high ▾]              ↑ ↓ ✕
  2  [Claude Code ▾]  [sonnet ▾]     [account default ▾]   ↑ ↓ ✕
  + Add a choice                                            (at most 4)
  Choice 2 runs only when no account of choice 1 can: cooling, signed out, or refusing its model.
  A model named here outranks the account's own and a repository's own setting.
  Also: daoris driver actor build "claude-code opus high" "claude-code sonnet -"
                                                                      Cancel   Save
```
- **The model menu:**
  1. *account default*
  2. the agent's own words: Claude Code's aliases on the pipe door, or what a protocol-door agent last offered
  3. *Other…*, a full id
- **Before Daoris has read an agent's models:** only *account default* and *Other…*, with the line *Codex has not said its
  models yet; its next session here reads them.*
- **The effort menu** follows the same rule.
- **Intake and Ask Daoris** add *Off* as the list's first entry.

**Screen 3: the suggestion card.** Shown in §3.3. It sits above the rows and lists only the actors that would change.

**Screen 4: the session's head**
```
#412  Add the export button                                        Review   ⋯
daoris · 12 min · choice 2                         ← the chip only when not choice 1, or "your pick"
▸ Details   Build · Claude Code · claude-sonnet-5-5
   Actor     Build, choice 2 of 2: choice 1 (Claude Code · opus · high) was refused by account-1 and account-2
   Agent     Claude Code 2.1.3xx on account-2
   Model     sonnet asked · claude-sonnet-5-5 ran
   Effort    account default · not reported
```
- *Details* stays folded (D152 §7).
- The parts fold (`PartsFold.tsx`) gains each part's model as it ran, so a line run on two models shows both.
- The account stays in *Details*, where HEAD2c put it (`headFacts.ts:173`).

**Screen 5: the wait**, on the quest's row line and the session's waiting phase (R2):
```
Build waits: none of its choices can run now. Claude Code's accounts cool until 14:05; Codex is not signed in.
```

**Screen 6: the terminal**
```
$ daoris driver actor
build        1 claude-code opus high      2 claude-code sonnet -       set 2026-10-23
intake       off                                                       as before
opinion      1 codex-acp - -              2 claude-code opus high      set 2026-10-23
helper       1 codex-acp - -              2 claude-code opus high      set 2026-10-23
partner      1 codex-acp - -              2 claude-code opus high      set 2026-10-23
brainstorm   1 claude-code opus high      2 codex-acp - -              set 2026-10-23
ask-daoris   1 claude-code - -                                         as before
chat         1 claude-code - -                                         as before
`-` is the account default. `daoris driver actor <actor>` shows one, with its last week.
```

#### 4.2 The words

Each in English and Chinese, within its kind's budget. Keys are named in the row that builds them.

| What | Kind | en | zh |
|---|---|---|---|
| Section title | `section` 32/12 | Actors | 角色 |
| Section body | sentence | Each kind of work Daoris starts runs on the first of its choices that can run, in your order, and says so when it took a later one. | Daoris 启动的每种工作，按你排的顺序，在第一个能运行的选择上运行；用了后面的选择时会说明。 |
| Actor names | `field` 36/14 | §1.2 | §1.2 |
| account default | `choice` 16/6 | account default | 账户默认 |
| Off | `choice` | Off | 关闭 |
| as before | `status` 16/5 | as before | 沿用原设置 |
| choice n (the chip) | `status` 16/5 | choice {{n}} | 第 {{n}} 选择 |
| your pick | `status` | your pick | 你的选择 |
| Edit / Add a choice / Use these / Not now | `button` 20/8 | Edit / Add a choice / Use these / Not now | 编辑 / 添加选择 / 采用这些 / 暂不 |
| Details rows | `field` | Actor / Model / Effort | 角色 / 模型 / 推理强度 |
| not reported | fragment | not reported | 未报告 |
| The note | sentence | Ran as {{actor}}'s choice {{n}}, {{choice}}: {{why}}. | 作为{{actor}}的第 {{n}} 选择运行（{{choice}}）：{{why}}。 |
| The wait | sentence | {{actor}} waits: none of its choices can run now. {{soonest}} | {{actor}}在等待：它的选择现在都不能运行。{{soonest}} |
| The use line | sentence | {{starts}} starts this week · {{tokens}} tokens · {{later}} on a later choice | 本周启动 {{starts}} 次 · {{tokens}} 个 token · {{later}} 次用了后面的选择 |

The reasons (`not-offered`, `refused`, `cooling`, `signed-out`, `unavailable`, `no-full-access`, `not-admitted`, `not-listed`)
are coded sentences beside `note.opening.why.*`, which already supplies the account's own reasons (`note.json:331-351`).

#### 4.3 A start, a quest, a node, an ask

- **The person's pick at a start** (D130 point 4) overrides the agent and the account. The model is the actor's first choice for
  that agent, else *account default*, and the head says *your pick*.
- ***Bring in another agent…*** (D203, the co-worker design §6.2) and the hand-over's agent list put the actor's choices first,
  and start each with that choice's model.
- **A quest, a workflow node or an ask** names no actor and no choice in this design:
  - A quest travels between machines and must name no model (D24), while an actor is this machine's.
  - A node's actor is fixed by its kind.
  - A quest's declared *weight* is row i: a fact about the work that travels, never a model.

#### 4.4 Cost shown

**Per session.**
- The parts strip's tokens, as today (`PartsStrip.tsx:18`).
- The model that ran, per part, in the agent's own word.
- No price (D57). Absent stays absent, never zero.

**Per actor.** One quiet line over the last 7 days, computed by the driver from the usage history and the machine log:
- starts;
- tokens;
- how many starts ran on a later choice.

**What stays as it is.** The Agents page's *Usage* fold stays per account. A per-model split there is left out (§4.6).

#### 4.5 Machine-wide

**The choices are this machine's, for every workspace.** Why:
- They spend the machine's accounts, which are one set (D197 point 1). Choices per workspace would bring back the scope question
  D197 removed.
- Settings stay machine-wide until the owner asks otherwise (D183 point 2; D158's ASKSCOPE1 note, point 7).

**What stays per repository:** its say over who may work in it, kept in its rules (§2.8).

#### 4.6 Left out to keep the UI simple, and why

- **Choices per workspace or per repository** (§4.5).
- **An *Auto* or intent router** (Cursor's and Copilot's *Balance* and *Intelligence*). Routing needs a model to judge the work.
  The order is for availability, which is the survey's own lesson (brief §6, point 3).
- **Separate lists per kind of failure** (LiteLLM). One order serves every reason, and the reason is said.
- **A price, a currency, a model catalogue, or a chart of capabilities** (D57).
- **A model picker on a driven session while it runs.** Driven sessions still offer none (`Driver.cs:3775`). The person stops
  the session or hands it over, and the next start takes the actor.
- **A separate synthesizer setting for a brainstorm** (D176 point 9's first-answered stands).
- **A *wait up to N minutes for choice 1* setting.** A later choice is taken at once, and the person orders the list.
- **A per-model split on the Agents page's Usage fold.** The head and the actor's line answer the question.
- **A notification per fallback** (§2.10).
- **More than four choices per actor** (§2.1).

### 5. Ask Daoris

#### 5.1 What it may propose

- one actor's choices;
- *off* for *Intake* or *Ask Daoris*;
- *as before*: back to what today's keys say;
- the suggested set, whole or for named actors.

Every proposal is judged by `ActorChoices.Judge`, the same rules the screen uses, so it can propose nothing the screen would
refuse.

What it does **not** propose through this tool:
- a rule's agents (*who may*): those stay `setting_propose`'s `opinion` and `helpers` doors (`HelpSettingProposals.cs:30-34`);
- its own access mode (D183 point 2).

#### 5.2 The tool

- **`actor_propose`**, in `Mcp/HelpActorTool.cs`, beside `agent_settings_propose` (`Mcp/HelpAccountTool.cs:10-31`). Its fields:
  - `actor`;
  - one of: `choices` (`[{ agent, model?, effort? }]`), `off`, `clear` or `suggested`;
  - `why`, the person's words and what the change would do.
- **The driver's kind** is `HelpActorProposals`, after `Help/Proposals/HelpAccountProposals.cs`. It is applied as `SET_ACTOR`
  makes the same edit.
- **The card says**, per actor, before → after, and the terminal twin:
  > *Build: Claude Code · opus · high, then Claude Code · sonnet (was: Claude Code · account default).*
  > *Also `daoris driver actor build "claude-code opus high" "claude-code sonnet -"`.* **[Apply]** **[Not now]**

#### 5.3 How it is confirmed

- **Restricted access.** The card's *Apply* (D89).
- **Full access.** Ask Daoris may run `daoris driver actor …` itself. The running Daoris's pipe still asks the person in the
  window:
  - `SET_ACTOR` is a `Person` route, as every `SET_*` is (`PipeCensus.cs:72-79`, `:234-240`).
  - The standing confirmation covers no standing choice (D201 point 3).
  - Either way the change is the person's press. Full access changes who types the command, not who says yes.
- **Not added to the chosen-settings grant** (`settings_*`, D183's HELPMODE1c3 note). A list of choices spends the accounts, and
  only the person's press should move that.

#### 5.4 What it reads, so it can help

The room (`HelpFacts`, D89's PARALLEL1a note) gains an *Actors* section with:
- each actor's choices and where they came from (set, as before, suggested);
- each agent's offered models as last read (§6.2);
- per actor over the last 7 days: starts, tokens by model, and the number of starts on a later choice with each code why.

That lets it answer *why did my builds run on Sonnet this week?* and propose from the evidence. The doors table gains one row:
*choose who does each kind of work* → Settings → AI features → `daoris driver actor` (`Help/Room/HelpRoomDoors.cs:160-161`).

### 6. The records

#### 6.1 The choices: `driver.json`'s `actors`

They are a standing choice of the driver's, written through `DriverConfig` under the keep lock (`DriverConfig.cs:866`):
```json
"actors": {
  "build":      [{ "agent": "claude-code", "model": "opus", "effort": "high" }, { "agent": "claude-code", "model": "sonnet" }],
  "intake":     [],
  "opinion":    [{ "agent": "codex-acp" }, { "agent": "claude-code", "model": "opus", "effort": "high" }],
  "ask-daoris": [{ "agent": "claude-code" }]
},
"actorsSuggestion": "2026-10-23"
```
- **An actor absent from `actors`** is read from today's keys (§3.1).
- **An empty list** means off (Intake, Ask Daoris) or no preference (the rule-listed actors).
- **`actorsSuggestion`** is the date of the suggestion the person last answered.
- **The writer** is `DriverConfig.WithActor`, judged by `ActorChoices.Judge`. It writes through `adapter`, `intakeAdapter` and
  `helperAdapter` (§3.7).
- **The readers.** The loop reads it at each start, and `HomeWatch` already wakes the loop on its change. Ask Daoris's grants
  never write it.
- **It is machine-local**, like every other choice in that file. A shared host carries no choice and no model (brief §5).

#### 6.2 The three twins: one new home entry, `agent-offers`

- **The file.** `data/agent-offers.json`: what each agent and version last offered for `model` and `thought_level`.
- **When it is read.** At every protocol-door `session/new` (`KeepOptions`, `Acp.cs:940-944`), and kept with its date.
- **Who writes and reads it.** Written by the driver alone. Read by the walk (§2.3) and the editor (§4.1). Taken by a clear of the
  home, and never synced.
- **The twins.** It is added to `Driver/Platform/DaorisHome.cs` (beside `:326`, `:365`), `Core/Platform/DaorisHome.cs` and
  `Daoris.Cli/src/platform/home.ts` (beside `:99`, `:138`).
- **Why a new entry.** HELPMODE1c3 rejected a new entry where the machine log already kept the facts (D183's note). The walk,
  however, reads this at every start, and the machine log is a daily file.
- **Why not `harnesses.json`.** That is D197's file for the account set, with its fold and receipts. A reading is not a setting.
- **`driver.json`** needs no new entry; the three twins already carry it.

#### 6.3 The terminal

```
daoris driver actor                              every actor's choices, and where each came from
daoris driver actor <actor>                      one actor's choices, and its last 7 days
daoris driver actor <actor> "<agent> [<model>|-] [<effort>|-]" …     set its order
daoris driver actor intake|ask-daoris off        off
daoris driver actor <actor> --clear              back to what today's settings say
daoris driver actor suggested [--use]            the suggestion, and use it
```
- `<actor>` is one of the ids in §1.2.
- The verb is handed to the driver's program as the family is (`cli/driver.ts` → `runDriverProgram`), and lives in
  `DriverChoicesCommand` beside `intake` and `helper` (`DriverChoicesCommand.cs:108-109`, `:154-155`, `:540-575`). So there is one
  judge and no TypeScript twin, as `agent settings` became under PUSH1f2d.
- On the bridge: `ACTORS` is `Open`, a look answering choices, sources, offers, the suggestion and the use. `SET_ACTOR` is
  `Person`. Both are classed in the census.

#### 6.4 What a start writes about the tier that ran

**1. On the session record**, four new fields, machine-local exactly as `profile` and `help_mode` are (`Sessions.cs:80-95`,
`:193`, `:584`):

| Field | Holds |
|---|---|
| `actor` | the actor's id |
| `choice` | 1 to 4; `pick` for the person's pick; or `before` for a choice read from today's keys |
| `asked` | `{ agent, model \| null, effort \| null }` |
| `ran` | `{ models: [...up to 3, in order of first use], effort \| null }`, or null for *not reported* |

- **How they are guarded:** answered on loopback only, written with no revision, on no feed, never synced (D47 §4; D183's
  HELPMODE1a2 note).
- **Why they cannot cross a sync:** they are the person's choice and this machine's accounts, which is the same reason `profile`
  never crosses one.
- **Where `ran` comes from**, always the agent's own word:
  - on the protocol door, the option's current value after the set, and the turn's `_meta["_claude/model"]` (D179);
  - on the pipe door, `message.model` (D179).
- **When `ran` is written:** once per part, as soon as the first turn reports, and again only if a later turn adds a model, as
  `opusplan` does.

**2. In the conversation's first lines.** The opening note's actor line (§2.10).

**3. In the machine log.** `actor.chose` (session, actor, choice, agent, model, effort, and each passed choice with its code),
`actor.passed`, and `actor.ran`.

**4. In the usage history.** The turn line (`UsageHistory.cs:606-640`) gains `actor`, bound where a start already binds agent,
account and door (D179), so a turn's tokens are counted per actor.

**Why on the record, and not only in the usage history.**
- The head and the history row read records.
- The usage history is per account and per turn.
- `TurnModels` keeps the model in memory alone (`TurnModels.cs:9-12`).
- What the record keeps is the agent's word, never a model name of Daoris's own.

**Containment and admission are untouched.**
- **Containment (D201).** Every choice starts through the same gate (`Driver.HoldAsync`, `AgentStarts.StartAsync`,
  `Driver.cs:3161-3180`). A start the launcher cannot contain is still made and logged `agent.contained: false`. A choice never
  names a command: `commands[adapter]` stays the person's.
- **Admission (D199).** The model rides `set_config_option` or `--model`, and the effort rides the pipe door's settings file.
  None of them enters `PartnerConfiguration` (`PartnerConfiguration.cs:47-52`), whose digest is over the posture, the rules, the
  hard denials and the guard (`:56-74`). Confinement belongs to the copy and the launcher, and a model cannot widen it.
- **D199 point 9 already asks for the model** on each partner's answer, where its door reported one.

### 7. What it amends, and what it rejects

#### Amends (a dated note appended to each decision, never rewritten into it)

- **D98.** *"Daoris names no model and chooses none"* becomes:
  - Daoris keeps no model list and names no model of its own.
  - The person chooses per actor, in the tool's own words.
  - Daoris carries that choice to each start at the tool's session level (the protocol's option, or the pipe's `--model` and the
    start's own settings file).
  - Daoris's only choosing is to take the first choice that can run, saying so when it took a later one.
  - D98's second level (one conversation's option), until now used only on a conversation, now also opens every protocol-door
    start.
  - D98's first level (the account's settings) becomes what *account default* means.
  - These still stand: the tool's home is never written, `ANTHROPIC_MODEL` stays rejected, `max` is never a file default, and a
    tool whose words Daoris has not read is offered only *account default* and the free field.
- **D24** (a note). The tier that ran now includes the model the agent reported, shown on the session. No feature is welded to a
  model, and every actor works at *account default*.
- **D57** (narrow). One dated table from role to alias, in the tool's own aliases, is suggested and never applied. Its single
  source is `docs/model-roles.md`, held by a census. It is not a registry: no versions, capabilities or prices.
- **D179.** *"The session's record and every page still keep no model's name"* becomes: the record keeps the agent's reported
  model, machine-local, and the head and the actor's use line show it.
- **D174 point 4.** Realized. The product's suggestion reads the reference through a census, and the reference changes by its own
  evidence.
- **D155 point 3.**
  - Each named reviewer takes its model from *Second opinion*'s choices.
  - A rule with no `reviewers` reads the actor's choices, which are still named by the person and never guessed.
  - Point 4's label also names the model.
- **D163 point 3.** The hand-over offer, when an agent's accounts are all out, names the actor's next choice on another agent.
  The `handOver` rule is unchanged.
- **D65 §1b and D89.** *Intake*'s and *Ask Daoris*'s agents become their actors. *Absent means off* stands.
- **D183.** Under full access, a choice with no full posture is passed (`no-full-access`). `actor_propose` joins the proposal
  kinds. No `settings_*` grant covers actors.
- **D176 point 9.** A brainstorm with no named agents falls back to *Brainstorm*'s choices instead of the machine's agent.
- **D130 point 4.** The pick overrides agent and account; the model comes from the actor's choices.
- **D203.** The reviewer, helper and partner roles are actors. *Bring in another agent…* orders its agents by the actor's
  choices.
- **D116.** The glossary's three new terms (§1.1) and *Build* as an actor's name.

**Stands unchanged:**
- D197: the set, the order, the use settings and the walk;
- D201's containment;
- D199's admission (above);
- D125: a limit is not a strike;
- D51;
- D47 §4.

#### Rejected

| Rejected | Why |
|---|---|
| The account's `settings.json` as the carrier | Shared by every actor on the account; concurrent starts race on the tool's file; a repository's settings outrank it (§2.2) |
| `ANTHROPIC_MODEL` at spawn | D98's reason stands; Claude-only; inherited by every child process |
| `_meta.claudeCode.options.model` | Claude-only; precedence unmeasured; `set_config_option` does it for every agent |
| The tool's own `--fallback-model` | Switches models inside a turn, where Daoris cannot say it |
| Going account by account before choice by choice | Spends against the person's order while a seat of the preferred choice is free; cool-offs are the account's (§2.4) |
| Choices per workspace or per repository | D197's single set; D183 point 2's machine-wide settings |
| A router or an intent (*Auto*, *Balance*) | Needs a model to judge the work. A quest's declared weight, later, is a fact and not a guess (row i) |
| Choices replacing the rules' agent lists | An actor would widen who may act. A partner writes, and a rule is the person's re-wiring in the census |
| A suggestion applied by itself, or an accepted one that follows later suggestions | D191 point 3; nothing the person never chose runs silently |
| A model catalogue kept for Codex or dsh | D57. Their protocol's offered option is the tool's own word |
| Writing `CODEX_HOME` or `DSH_HOME` | D98's boundary |
| A `settings_*` grant for actors | A list of choices spends accounts. Every change is the person's press |
| A price shown | D57 |
| A notification per fallback | It asks nothing of the person. The use line shows any pattern |
| Two Build actors now (exact and judgement) | Nothing at a start says which a quest is. Row i adds the signal first |
| Flagging when *ran* differs from *asked* | Telling `opus` from `claude-opus-…` needs a model list. Both are shown, unjudged |
| Moving a running session to the next choice | D163 |
| A model fallback on a cooling account | The cool-off is the account's. Row k reads model-scoped windows first |
| The model as a travelling fact on the record | It is the person's choice and the account's, so machine-local, like `profile` |
| Folding the account's *Model and effort* into actors, or removing it | It is what *account default* means, and D98's first door, which the owner asked for |
| Names: *role* in English; 岗位, 演员, 代理, 人设; *range* or *fallback* in the chrome | §1.1 |

### 8. The rows, in build order

Each row is one feature a person can see working, end to end across its lanes (D187). Each lands alone, behind the one before it.
Each carries its words in both languages, its glossary entries, `i18n:check` and `names:check -- --strict`, and stories at the
frame's three widths (D185 R6).

**a. MODELROLE1a: what each door accepts, measured** (tools, driver stubs; first).
- *What it measures.*
  1. On `claude-agent-acp`, `session/set_config_option` for `model` and `thought_level` at the open of a driven-shaped session,
     before the first prompt, and the values it offers.
  2. `codex-acp` 2.1.x's categories and values: whether model and effort are separate or combined, and whether it reports the
     model on a turn.
  3. dsh's catalogue option.
  4. `claude -p --model <alias>`, an unknown model's words, and whether a flag carries effort, `max` included.
  5. Whether `effortLevel` in the start's own settings file reaches the pipe door.
- *Lands:* an evidence document, and the ACP stub and pipe stub speaking the measured shapes.
- *Proof:* the evidence, and the stubs' tests.

**b. MODELROLE1b: each session says the model it ran on** (driver, service, web).
- *What it builds.*
  - Every start names its actor. `HoldAsync` and the chat runner's start gain a required actor, so a new start without one does
    not compile.
  - The record's four machine-local fields (§6.4).
  - *Details*' *Actor*, *Model* and *Effort* rows, and the parts fold's model per part.
  - The usage history's `actor`, and the machine log's `actor.ran`.
- *Behaviour:* unchanged. Every choice is `before` at account default.
- *Proof:*
  - a table test over the brief's 13 starts, each to its actor;
  - the record answered on loopback only and absent from the feed and from a sync (the `profile` tests' twin);
  - *ran* read from each door's stub, with *not reported* where a door says nothing;
  - stories in English and Chinese;
  - the install: a driven session's *Details* naming the model Claude reported.
- *Why first.* It is the baseline every later change is measured against (§3.6).

**c. MODELROLE1c: Build runs its choices** (driver, modules, web, terminal).
- *What it builds.*
  - `driver.json`'s `actors`, read from today's keys and written through.
  - `ActorChoices.Judge`.
  - The choice-major walk (§2.4).
  - The carriers (§2.2).
  - The passes and the `choice-not-offered` backstop with no strike (§2.3).
  - `agent-offers` in the three twins.
  - `SET_ACTOR` and `ACTORS`.
  - `daoris driver actor`.
  - AI features' Actors section, with *Build*'s editor.
  - The opening note's line, the chip and the wait's sentence.
  - The Agents page fold's one line.
- *Proof.*
  - Unit tests: the judge's table, the reading table from today's keys, and write-through with an older build's reading.
  - Walk tests: cooling, signed out, refused, not offered, kept for conversations, the pick and a handed account; every choice out
    gives the wait naming the soonest, then the first that frees in order.
  - ACP stub: the model is set before effort and before the first prompt; an unoffered model stops before any prompt; a resume
    re-applies the choice.
  - Pipe stub: `--model` exactly when a choice names one, and none when the person's command does; `effortLevel` in the
    settings file; `max` refused.
  - `PipeCensus`: `SET_ACTOR` is `Person` and `ACTORS` is `Open`.
  - `DaorisHomeTests` in the driver and the service, and the CLI's home table.
  - The page's tests and stories.
  - The install: a *Build* list whose first choice names a model the agent does not offer, then runs its second, says so on the
    head and in the opening note.

**d. MODELROLE1d: Intake, Ask Daoris and Chat run their choices** (driver, modules, web, terminal).
- *What it builds.*
  - Starts 5, 10, 11, 12 and 13 take their actor.
  - The intake and Ask Daoris cards fold into their rows.
  - `driver intake|helper` write a one-choice list.
  - Under full access, a choice with no full posture is passed.
  - The pick overrides (§4.3).
  - A go-on re-applies the last composer change.
- *Proof:*
  - each start's actor;
  - *absent means off* kept;
  - full access passing dsh (`no-full-access`);
  - the composer opening on the actor's model;
  - `HelpRoomDoorsTests`;
  - the install: a chat on *Chat* · sonnet shows sonnet beside its composer and in *Details*.

**e. MODELROLE1e: second opinions, helpers, partners and brainstorms take their models from their choices** (driver, web).
- *What it builds.*
  - The rule-and-choices composition (§2.8).
  - The second opinion's label naming the model (§2.7).
  - The brainstorm's voices.
  - The hand-over offer naming the next choice (§2.5).
  - *Bring in another agent…* ordered by the choices.
- *Proof:*
  - `ReviewerChoice`'s walk over choices: another maker first; the same agent on a stronger model labelled *fresh*; an empty
    `reviewers` reading the actor;
  - `HelperChoice` and `PartnerChoice` taking their model from the choices;
  - a partner's digest unchanged by the model (two choices, one digest);
  - a cooling Build offering the hand-over under `ask`, taking it under `on` and waiting under `off`;
  - the install: a second opinion's *Details* naming Codex's reported model.

**f. MODELROLE1f: each actor's use shown** (driver, web).
- *What it builds.* The 7-day use line per actor, from the usage history and `actor.chose`. *Absent* stays absent, never zero.
- *Proof:* the sum's table, stories, and the install's line against the usage history.

**g. MODELROLE1g: Daoris suggests choices from the model-roles reference** (driver, web, terminal).
- *What it builds.*
  - `ActorSuggestion`, held by a census that reads `docs/model-roles.md`'s *Current* and *Effort* columns and maps *Claude Opus …*
    to `opus`, *Claude Sonnet …* to `sonnet`, *Claude Haiku …* to `haiku`, *Claude Fable …* to `fable`, and *another maker* to the
    installed other maker at *account default*. The census fails both ways.
  - The card, with *Use these* and *Not now*.
  - `actor suggested [--use]`.
  - Leaving out agents that are not set up.
- *Proof:*
  - the census;
  - the card's tests and stories;
  - the install: **the owner's press**, which is D191 point 3's yes. The week before it, read from row f, is the baseline (§3.6).

**h. MODELROLE1h: Ask Daoris proposes choices** (service, driver, web).
- *What it builds.* `actor_propose`, `HelpActorProposals`, and the room's *Actors* facts (§5.4).
- *Proof:*
  - `HelpCoverageTests`;
  - the room's goldens;
  - an unmarked or refused proposal refused at its Apply in the judge's words;
  - full access running the verb asking the window (`PipeConfirmations`);
  - the install: Ask Daoris answering *why did builds fall back?* from the room, and its card applied.

**Filed with this design, but waiting for their own designs or measurements:**
- **i. Exact build by a quest's weight.**
  - A quest may declare `weight: exact`, set by its publisher: the person, the intake, or an agent whose pack names the change.
  - The receiving machine starts it as an *Exact build* actor, suggested `sonnet` after D174's build-exact row.
  - The quest carries no model.
  - Filed after a batch of row b's measurements, since D160's target is that at least half the rows are exact.
- **j. Orientation as a start.** A scout before a Build, handing a pack from the knowledge store (the MODELROLE1 row). Its actor
  is named then, suggested after D174's *orient* row.
- **k. Cool-offs scoped to a model.** Read `seven_day_opus` and `seven_day_sonnet` (`ClaudeUsage.cs:41`), and cool a limit whose
  words name a model's window as account plus model, so a later choice on the same account may run.

**The tests across the rows, gathered:**
- *Unit:* the judge, reading from today's keys, write-through, the walk's table, and the suggestion's census.
- *Census:* the start-to-actor table (compile-time through the required parameter, and a table test); `PipeCensus`;
  `HelpCoverageTests`; `DaorisHomeTests` in the three twins.
- *Stub:* both doors' carriers and their refusals.
- *Invariants:* the partner digest; D155's label; D163's offer; loopback-only fields.
- *Words:* `i18n:check` and `names:check -- --strict`.
- *Stories:* at three widths, in both languages.
- *Installed window:* the proofs named in rows b to h. Installed-window proof stays distinct from a story or a mocked bridge.

### Summary of its shape

**The actors.**
- Eight: Build, Intake, Second opinion, Helper, Partner, Brainstorm, Ask Daoris and Chat (en *actor*, zh 角色).
- Each is a kind of start. A continuation keeps the actor of the record it continues.

**Choices and the walk.**
- Each actor holds one to four ordered *choices* of agent, model and effort, in the tool's own words. *Account default* means
  today's cascade.
- A start walks the choices in order. Within each choice it walks that agent's accounts by D197's walk, unchanged, and takes the
  first that can run.

**Carrying and recording.**
- The model is carried at the session's own level: `session/set_config_option` before the first prompt on the protocol door, and
  `--model` with the effort in the start's own settings file on the pipe door.
- The tool's home and the environment are never written.
- A start records, machine-local, the actor, the choice taken, what was asked, and what the agent reported. When it took a later
  choice it says so in the opening note, on a chip on the head, and in the log.

**Who may, and nothing silent.**
- Repositories' rules still say who may do a job; the choices say with which model.
- Nothing moves a running session.
- A model the person never chose never runs silently.

**Defaults and evidence.**
- The upgrade changes nothing: every actor's choices are read from today's keys.
- A suggestion, drawn from `docs/model-roles.md` through a census and led by the owner's Opus 5.5 for judgement work, is offered
  once and applied only by the person's press.
- It is measured against the baseline that row b's records make possible.

**Where it lives.**
- One section on Settings → AI features: a row per actor, its first choice, and one use line.
- An inline editor, the terminal's `daoris driver actor`, and Ask Daoris's `actor_propose`.
- Every change is confirmed by the person's press, in both of Ask Daoris's modes.
