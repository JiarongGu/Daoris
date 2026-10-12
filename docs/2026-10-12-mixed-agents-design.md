# Mixed agents in one task: hand-overs, helpers and the view of who worked

The design under D163, for COWORK1's direction and the rows MIXAGENT1–3. Written 2026-10-12 from a design pack (the
branch-scout's, cited below by file and line) after the owner ran a ticket on Codex, saw its account's allowance run low,
and had the task carried to Claude by hand.

## 0. The owner's direction

> *"I just used a codex for main drive for a new task but I don't see a way to mix the use of both agents (like codex to
> claude) so currently when usage is almost full I'm kind of stuck on that task, also it should be able to run subagents
> to use other agents to support the task and we need a clear view to show those behaviours (and should be decided by the
> agent itself or triggered by user)"*

Three asks: a task **goes on with another agent** (§2); a session **calls other agents to help** (§3); the person **sees**
both (§4). Each may be **started by the person or decided by the agent**, within bounds the person sets (§5).

## 1. What exists, and what it leaves out

- **One agent per machine for driven work.** Every driven start takes the machine's `config.Adapter` (Driver.cs:437,
  972-980, 1164, 1208; DriverConfig.cs:16, 59). Only the intake, the helper (Ask Daoris) and the reviewers have adapters
  of their own (DriverConfig.cs:311, 322; D155). Nothing holds an adapter per quest, per session or per start.
- **Conversations already choose their agent.** `START_CHAT` takes `adapter` (DriverModule.Conversation.cs:40;
  ChatRunner.cs:154-190), which NEW1b's composer chip sets.
- **An agent change is already a carry-on.** `Continuations.Judge` (Continuation.cs:165-196) sends words to a NEW session
  when the record's adapter differs from the machine's (`ContinueWhy.Adapter`, l.46, 115), with a coded reason line. What a
  carry-on hands is Daoris's own record, agent-neutral: `LastPlan`, `LastWords`, `InFlight`, `PersonSaid`, and the tree
  itself, reopened (Adapters.cs:105-157; Driver.cs:2477, 2522; SessionEvents.cs:730).
- **Moving to another account stays inside one agent.** Rotation walks an agent's own accounts (Driver.cs:972-980;
  D125 §3.5); TOOL6d (open) makes *Continue on account-2* the person's press after a limit (D130 pt 11; account-use design
  §8-§9).
- **The nearest presses.** *Start a conversation with these words* (MSG1f2; ChatRunner.StartFrom.cs:33-135) hands a new
  conversation the old one's words with a preface; *Go on in a new session* (MSG1g; GoOnNew.cs) is a press the driver's next
  look takes from a file. Both refuse a running session.
- **One agent starting another exists once, and not by the working agent.** XAGENT1's reviewer is started by the driver's
  look from the rule's `reviewers`, read-only in a clone of its own, one turn, its findings returned as the asker's next turn
  marked by the agent (D155 pts 4-7; second-agent design §3, §5.4, §6.3). The design rejects agents talking until they agree
  with no person between (l.646-647) and a reviewer starting a reviewer (l.284-286).
- **A session never asks Daoris for anything but through a box.** `permission_propose` (KnowledgeTools.cs:743-776) and
  `go_ahead_ask` (KnowledgeTools.GoAhead.cs:9-40) write a request the driver or the person takes; the connector's scope says
  which session asked (Driver.cs:2460). No tool starts a session.
- **"Almost full" has a source, not everywhere.** `AccountReadings.Near` (AccountReadings.cs:141-167) reads an agent's own
  window or warning word against the person's `near` (default 90); an account that said nothing is neither near nor clear.
  Codex's windows are read from its own server; whether Claude Code's door carries the word is not settled (account-use §6).
- **The view knows one record at a time.** The head's facts, the timeline (timeline.ts:65-87, "no event log") and the
  rail's groups are keyed on the record alone; a reviewer's row says *reads {title}* (XAGENT1g4c) but is not grouped under its
  session (XAGENT1g4d, open).

## 2. A task goes on with another agent (MIXAGENT1)

**The hand-over is a carry-on with its agent named.** The words, the tree and the record's facts go to a new session on
the agent the hand-over names, for that one start; the machine's `adapter` setting is not changed (D110 keeps it the
person's setting door). It rides `ContinueWhy.Adapter`'s path, so a carry-on's reason line and its facts are reused, not
written twice.

- **A request is a file the driver's next look takes**, as *Go on in a new session*'s is: `<home>/sessions/<id>.hand-over.json`
  with the agent, an optional account, the reason (`press`, `near`, `limit`, `agent`) and who asked (`person`, `daoris`,
  `agent`). The look judges it, starts the new session and records both ends; a refusal is a coded sentence on the old record.
- **A conversation** goes on in a new conversation on the named agent, in the same tree where the old one ended (its own
  tree, or the checkout it worked in), handed the record's facts as a preface of their own (the BRWHINT1a pattern: a field,
  never the person's words rewritten).
- **A quest's session** ends as *handed over* (a stood-down with its own note), the quest stays taken by this machine, its
  chain and branch unchanged (D145/D149), and the next start of that quest runs on the named agent in the same tree with the
  carry-on's facts. The override is held on the quest's record of this machine until the quest leaves it.
- **A running session is let finish its turn.** A hand-over asked while it works is kept and taken at the turn's end; the
  person's *Stop and hand over* stops it first, as *Stop…* does.
- **What the new agent reads**: Daoris's record facts (the plan, the last words, what is uncommitted, the request as
  written), and, where the old agent wrote one through its tool (§5), its own hand-over note, kept and shown as that agent's
  claim. Never the old transcript whole, never a summary Daoris writes with a model.
- **The terminal twin**: `daoris-driver sessions hand-over <id> --agent <adapter> [--account <name>]`.

## 3. A session calls other agents to help (MIXAGENT2)

**A helper is a bounded, read-only session on another agent, asked by the working session or the person, whose answer
returns as the asker's next turn.** It reuses the reviewer's machinery (a clone of its own, the account walk, a slot under
the cap, usage recorded or *not measured*), and it is not a reviewer: it answers a question the asker wrote.

- **The asker's door is a tool**, `helper_ask` (the connector's, D156: an agent door, recorded as the agent's): the question,
  an optional agent, and what it may read (this repository at the asker's commit, by default). It writes a box the driver
  takes; the person's own door is *Ask another agent…* on the session's page, with the same fields.
- **Read-only in v1.** A helper that writes needs a place its work lands (a branch the asker takes, D145/D149) and is a later
  row; v1 helpers research, explain, review and propose.
- **The answer returns as the asker's next turn**, marked as the helper's (D137 pt 3, the `by` mark), a claim and never the
  person's words, as a reviewer's findings do (second-agent design §6.3).
- **A helper cannot ask for a helper.** Its connector has no `helper_ask` (depth one), as a reviewer cannot start a reviewer.
- **Which agent**: the one the asker names, if the rule allows it; else the person's `helpers` list for the repository;
  else another maker first, as D155 pt 4 chooses reviewers.

## 4. The view of who worked (MIXAGENT3)

- **Links on the record.** A session gains `from` (the session it continues, with who asked and why) and a helper gains
  `serves` (the session it answers), as the reviewer's `opinion` column does (Sessions.cs:402).
- **The head.** A session that continues another says so in its facts line (*continues a Codex session · its account was
  near its limit*), with a door to it; a helper says *helping* and the session's title.
- **The timeline and the conversation.** Coded note lines, both languages: a hand-over asked, taken or refused (with the
  agent, the account and why), a helper asked, answered or refused; the helper's answer shown in the conversation as that
  agent's block, as findings are.
- **The rail.** A helper's row sits under the session it serves (closing XAGENT1g4d for reviewers too); a session that
  continues another names it on its row.
- **Cost.** Each helper's and each hand-over's usage on its own row; *not measured* where the door says none, never zero.

## 5. Who decides, and the bounds the person sets

Every hand-over and every helper can be **started by the person** (a press, a terminal verb) or **asked by the agent** (a
tool). What an agent's ask does is the person's rule, per repository or workspace, in Settings and `driver.json`:

| Rule | `ask` (default) | `on` | `off` |
|---|---|---|---|
| `handOver` | the agent's ask, and Daoris's offer when the account is near or cut, wait for the person's yes on the session's page | taken at once when near or cut, to the next agent in the person's order | only the person's press |
| `helpers` | each `helper_ask` waits for the person's yes | started at once, within the limits below | the tool is not handed |

- **Limits for helpers**: at most two at once per session, each one turn and a minutes bound, each a slot under the cap,
  each on an account the walk finds ready; past a limit the ask is refused in a coded sentence the asker reads.
- **Daoris's offer** when an account is near (`AccountReadings.Near` at the person's `near`) or cut by a limit (a cool-off):
  on the session's page, *Go on with Claude Code (account-2)?*, never taken without the person under `ask`. An account that
  says nothing is never offered as near.
- **No loop with no person**: an agent cannot hand over twice in a row without a turn of its own between, a helper cannot
  hand over, and the person's rule is the only way anything is taken without a press.

## 6. Rejected

- **Changing the machine's agent for a hand-over**: the setting is the person's door (D110), and a hand-over is one task's.
- **A hand-over that carries the transcript, or a model-written summary of it**: Daoris carries its own record's facts;
  the old agent may write its own note, kept as its claim.
- **Automatic hand-overs by default**: the owner asked for *decided by the agent or triggered by the person*; the agent's
  decision is an ask the person's rule settles, off for automatic until they choose it.
- **Writing helpers in v1**: their work needs a landing design.
- **Agents conversing freely**: D155's rejection stands; a helper answers one question in one turn.

## 7. Build rows

| Row | Lanes | What |
|---|---|---|
| MIXAGENT1a | driver | the hand-over box, its judgement on the next look, a per-start agent override riding `ContinueWhy.Adapter`, the quest's held override, the record's `from` link, the terminal verb |
| MIXAGENT1b | modules, web | *Go on with another agent…* on a session's page (agent and account chips, as the composer's), *Stop and hand over*, the offer when near or cut |
| MIXAGENT1c | service, driver | the agent's `handover_propose` tool (its note), into the box under the `handOver` rule; the rule in Settings and `driver.json` |
| MIXAGENT2a | service, driver | `helper_ask`, its box and the `helpers` rule, read-only helper sessions on the reviewer's machinery, the answer as the asker's next turn, `serves` |
| MIXAGENT2b | modules, web | *Ask another agent…* on a session's page; the helper's rows |
| MIXAGENT3a | web | the head's line, the coded notes, the rail's grouping (XAGENT1g4d), cost per row |

Each new door gets Ask Daoris's classification (D110, `HelpCoverageTests`) and its terminal twin.
