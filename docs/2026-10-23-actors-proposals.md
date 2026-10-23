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
