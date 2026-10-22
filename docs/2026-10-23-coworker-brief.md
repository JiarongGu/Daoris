# Co-workers: one model for every agent that works beside a session, shown and driven like sessions: the brief

> COWORKUX1, phase 1 of a co-design under D191 (2026-10-23). This brief is the one document every proposal is written
> from. It holds the direction, the constraints, the facts of today's records and page with `file:line`, what a real
> helper on this install looked like, how other tools present several agents working together, and the questions a
> proposal answers. The proposals are in `docs/2026-10-23-coworker-proposals.md`, one section per maker, each written
> from this brief alone and sealed before its author read another. A synthesis follows in a later quest. `Dnn` is
> `docs/decisions/Dnn.md`. Paths are under `src/`: `Driver/` is `Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is
> `Daoris.Desktop/Daoris.Desktop.Modules/`, `Core/` is `Daoris.Service/Daoris.Service.Core/`, `Http/` is
> `Daoris.Service/Daoris.Service.Http/`, `Web/` is `Daoris.Web/`.

## 1. The direction

The ask: **co-workers, one model for every agent that works beside a session, shown and driven like sessions.** The owner
found three things wrong with today's page: the helper (助手) is shown as a side answer; the partner (搭档, D199) and the
reviewer (D155) are separate notions; and none of them reads as a colleague working beside the session.

The steward gave a foundation **to test, not to obey blindly**:

- A session's **team** is its **lead** and the **co-workers** it brings in.
- Each co-worker is **a session record of its own in a role**: a reader (today's helper, D163), a partner writing a piece
  (D199), a reviewer (D155).
- Each is **shown and driven as sessions are**: its own conversation in the same chat surface, nested under the lead like
  a thread, with its live state.
- A **team strip** in the lead's head names each member and its maker.
- **Each exchange is a card** in the lead's conversation, and the card opens the member's conversation.
- **The person may talk to a member**, within its role's bounds.
- **D199's and D163's confinement, admission and bounds stand.**

**Names in both languages are part of the design.** 协作者 is the steward's first word for *co-worker*, and a proposal
argues for a better one where it has one.

The work order: map today; survey other tools; write this brief and one proposal per maker (Claude's first, sealed, then
Codex's through a helper's call, from this brief alone); Fable's and the synthesis come later. Docs only: nothing is built
here.

**The part of the company it makes work** (D193): the staff (part 4), working the way a team inside one department works
(part 9's rhythm), seen by the owner (part 1) without having to read plumbing.

## 2. The constraints a proposal works within

Each is decided and stands unless a proposal argues to amend it, saying which and why. An amendment is appended to its
decision, never rewritten into it.

- **One writer per tree** (D51; D199 point 1). A co-worker never writes in the lead's tree. A partner writes in a clone
  of its own with no remote, and its piece lands only through the lead's merge of a frozen commit (D199 points 2 and 3).
  Every landing door refuses a partner's record.
- **Confinement and admission** (D199 point 4; D155 point 5; D163 point 2):
  - A helper and a reviewer read in a clone Daoris made, and nothing is read back from the clone.
  - A helper's copy carries the lead's uncommitted work (D199 point 5).
  - A partner is admitted only by a measured row of its whole effective configuration.
  - Each role is enforced by the server from a scope the driver binds (`RoleFilter`), never only by rules handed to the agent.
- **Bounds**:
  - Each co-worker gets one turn within its minutes, depth one: a helper asks for no helper; a partner asks for nothing.
  - At most two helpers at once per asker (`HelperJudge.PerAsker`) and two open pieces per lead and its continuations
    (D199 point 2).
  - A co-worker's record takes a slot under the cap (CAPCOUNT1).
  - The person's rules `helpers` and `partners` (`ask` | `on` | `off`, with the agents listed) decide what an agent's ask
    does (D163 point 3; D199 point 2).
- **No agents conversing with no person between** (D155; D163; D199 point 10; D177).
  - Agents talk only through Daoris's connector.
  - A solicited answer may carry its asker on, but a line goes on by answers alone at most twice before it waits for the
    person (D188).
  - A partner cannot ask back. Nobody but the person sends *now*.
  - Agreement between agents establishes nothing (D155 point 8).
- **Claims are not the person's words** (D137; D155 point 7; D163's MIXAGENT2a-ii note).
  - A co-worker's answer is marked as another agent's claim (`SaidWord.Helper`, `by`), never kept as the person's words.
  - It reaches the lead at a turn's end and never into a step in flight, except an urgent ask's answer at the next step
    (COWORK1d).
- **A session is one line of work in one repository; its records are its parts** (D167). A retry, a carry-on, a resume, a
  hand-over or a later request of the same work is a further part of the same session, each its own conversation. A
  hand-over changes who leads (D163 point 1; D199 point 6), and the lead's pieces travel along `from`.
- **Machine-local** (D47 §4; AGENTS.md). A helper's and a partner's record, `serves`, `piece` and `opinion` are this
  machine's own: no shared host carries them, and a teammate's mirror never shows them (`session-record-fields.md`).
- **Every door, both doors** (D50). What the window does, the terminal does too (`daoris-driver sessions …`), and Ask Daoris
  proposes it where it proposes anything (D110, D183).
- **Names** (D116; `docs/2026-10-01-naming-design.md`).
  - A name is designed in each language for its element, within its kind's budget (`button` 20/8, `status` 16/5,
    `section` 32/12, `tab` 10/4).
  - The glossary (`Web/src/locales/glossary.json`) holds one term per concept. Chrome translates; content stays as
    written (`translation-parity`).
  - D193 rejected "a new vocabulary on the page": a new term enters through the glossary with its reasons.
- **What waits on the person** is one kind in `work/waits/waits.ts` and a row in *What needs you*, judged in .NET
  (`person-waits.md`).
- **Model-free.** Nothing here needs a model. Where a proposal adds one, it says the tier that ran (D24).
- **The process** (D191). Three proposals from this brief, a synthesis, then Codex's read, and Fable's where the design
  changes a foundation. The steward settles; the owner is asked only what is theirs.

## 3. Today: how co-workers are recorded and shown

*Read at `f506863e7`. Line numbers are that commit's.*

### 3.1 The records

**One record type for all of them.** Every co-worker that exists today is a full `Session` (`Core/Sessions/Sessions.cs:101`)
in the SQLite `sessions` table. Its kind is `Chat` and it has no quest. Its role is told only by an optional field, and
these fields never leave the machine (`:145`, `:155`):

| Field | Line | Set on |
|---|---|---|
| `Opinion` | `:148` | a reviewer |
| `Serves` | `:158` | a helper and a partner; it names the lead |
| `Piece` | `:165` | a partner, always beside `Serves` |
| `From`, `FromWhy`, `FromBy` | `:175-181` | a part that continues another |
| `Entry` | `:226` | every record; it joins the parts of one line (D167) |

Words handed to the lead are a `SaidWord` (`:375`). `By` names an opinion, and `Helper` names a helper's or a partner's
record. `Persons` (`:380`) excludes both, so neither is ever read as the person's words.

**A helper (D163).** A record of its own with its own entry, opened by `HelperDesk.OpenAsync`
(`Core/Sessions/HelperDesk.cs:69-111`, `serves` and `ownEntry: true` at `:99-102`).

- *Its conversation.* A transcript of its own (`Driver/Loop/DriverHelpers.cs:551`) and its own events.
- *Its ask.* A box under the home's `helpers/`: `.ask.json`, `.state.json` and `.answer.json`
  (`Core/Sessions/HelperBox.cs:7-10`). The states are `waiting | approved | started | answered | refused`
  (`HelperBox.cs:30`; `Driver/Conversations/Domain/HelperAsks.cs:6-10`).
- *Who asks.* The agent (`helper_ask`, `Daoris.Service.Mcp/HelperTools.cs:17`), the person's page
  (`Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/SessionsRoutes.cs:175`), or the terminal
  (`Driver/Conversations/Helpers/SessionsHelperCommand.cs:91`).
- *Bounds.* One turn, read-only, no words from the person (`HelperPass.TakesNoWords`,
  `Driver/Conversations/Domain/HelperPass.cs:63`). Fifteen minutes (`HelperInstruction.cs:34`). Two at once per asker
  (`HelperJudge.cs:52`). The rule's `ask | on | off` (`HelperRules.cs:50-52`).
- *Its copy.* A clone at the asker's tip with its uncommitted work laid over it (`DriverHelpers.cs:478-494`), removed
  however the turn ends (`:502-506`).
- *Its tools.* The reviewer's read-only rules, with `helper: true` (`:570-572`). A helper, reviewer or intake cannot ask
  for one (`HelperDesk.cs:85-91`).
- *How the answer returns.*
  - `HelperWords.Compose` (`HelperWords.cs:17-27`) wraps it in fixed words: another agent, the question, the answer, *a
    claim to check*.
  - `HelperDesk.HandAsync` (`:126-179`) keeps it once as a word with `Reopens: true`, refused for a lead that is running
    or parked (§4).
  - For a conversation, the driver also writes a `User` event with `Origin = "target"` and `Helper` set
    (`DriverHelpers.cs:439-444`).
  - An urgent ask's answer is put in at the running lead's next step (`:405-430`).
  - A helper that did not use its tool has its last words stand as the answer (`:413-415`).
- *Notes.* `helper.asked`, `answered`, `answered-last-words`, `refused` and `refused-by` on the lead; `helper.helping` on
  the helper (`Driver/Platform/NoteCodes.cs:601-606`).

**A partner (D199, COAGENT1b, partly built).** A record of its own with its own entry, opened by `PartnerDesk.OpenAsync`
(`Core/Sessions/PartnerDesk.cs:31-91`, with `serves`, `piece` and `ownEntry` at `:79-82`), idempotent by piece (`:51`).

- *Its box.* The home's `partners/` holds `.ask`, `.state`, `.answer`, `.facts` and `.delivery`
  (`Driver/Conversations/Partners/PartnerFiles.cs:6-14`). It has fifteen states, from `asked` to `taken-in`,
  `not-taken-in`, `discarded`, `refused` or `uncertain` (`Driver/Conversations/Domain/PartnerPieces.cs:8-59`).
- *Bounds.* A clone of its own with no remote. Forty-five minutes (`PartnerInstruction.cs:28`). Two open pieces along the
  lead's `from` chain (`PartnerAdmission.cs:34, 81-87`).
- *Its tools.* Only reading tools, `line_read` and `partner_answer` (`PartnerPermissions.cs:18-21`), enforced by the
  connector's role (`Daoris.Service.Mcp/RoleFilter.cs:37-52, 68-80`). Every record with `serves` is refused
  `quest_publish` and `quest_respond` (`:58`).
- *Admission.* By a measured row (`PartnerMeasurements.Admitting`, `:60-70`). `Rows` is empty (`:44`), so every
  production start is refused `not-measured`.
- *Who asks.* Nothing outside the tests writes a partner's ask yet: there is no `partner_ask` tool, no page door and no
  terminal verb (D199's COAGENT1b notes; `DriverPartners.cs:41-43`).
- *How its answer returns.* As a word with `Helper` set to the partner, keyed `partner:<piece>`, to the newest live record
  of the lead's line (`PartnerDesk.HandAsync`, `:101-157`, the line at `:115-120`). Its notices are notes only
  (`NoteCodes.cs:612-622`).
- *How its work lands.* Only by the lead's `git merge --no-ff refs/daoris/pieces/<id>` (`Driver/Trees/Pieces/PieceKeeper.cs:14-16`).
  Every landing door refuses a partner's record (`Driver/Trees/TreesWords.cs:240-244`;
  `Modules/Bridge/Routes/TreesRoutes.cs:570-574`).

**A reviewer (D155).** A record of its own with its own entry, opened by `OpinionDesk.AskAsync`
(`Core/Review/OpinionDesk.cs:128-131`, with `opinion` and `ownEntry`).

- *The opinion.* `Opinion` (`Core/Review/Opinions.cs:114-144`) holds the working session, the candidate, the reviewer, its
  findings, whether they were handed, and the lead's answers. It is stored in its own table
  (`Core/Review/OpinionStore.cs:34-49`); its state is derived when read (`Opinions.cs:146-153`).
- *Its tools.* Read-only, with `opinion_give` alone to act (`Driver/Review/Domain/OpinionPermissions.cs:24-40`).
- *How findings return.* As a word with `By` set to the opinion (`OpinionDesk.cs:251`), answered with `opinion_answer`.
  Notes `opinion.*` (`NoteCodes.cs:836-848`).

**A hand-over (D163).** Not beside the session but after it.

- The agent's ask is a box, `sessions/<s>.hand-over-ask.json` (`Core/Sessions/HandOverAskBox.cs:7-10`), under the rule
  `handOver`.
- The take makes a **new record** whose `From` names the old one, in the same tree and the same entry
  (`Driver/Loop/DriverHandOvers.cs:5-17`; reasons at `HandOverRecords.cs:4-41`). It begins with Daoris's brief of the line,
  and no answer flows back.
- `hand_propose` is something else: Ask Daoris's proposal to hand a landed branch to a plugin (`Daoris.Service.Mcp/HelpHandTool.cs:10`).

**A part (D167).** One record of a line, sharing its `Entry`. Its beginnings are `opened`, `retried`, `resumed`,
`carried-on`, `handed-over`, `moved` and `asked` (`Driver/Trees/SessionEntries.cs:4-29`). Parts follow one another and
never run beside each other. Helpers, partners and reviewers each have their **own** entry (`ownEntry: true`), so none
is ever a part of the lead's line.

**A brainstorm's pass** has no session record at all (`Driver/Loop/DriverBrainstorms.cs:637-638`).

### 3.2 The page

- **The list.** A helper and a reviewer are drawn one level under the asker's row (`nestServing`,
  `Web/src/work/groups.ts:285-330`; `Web/src/work/rail/SessionList.tsx:84-94, 162`). This happens only where the asker's
  row is drawn in a group the list shows, and a row the person must act on keeps its own place (`keepOwn`). The nested
  row reads *helps {{title}}* or *second opinion · reads {{title}}* (`Web/src/work/rail/SessionRow.tsx:198-203`).
- **The head, for the lead.**
  - `HelperAsks` (`Web/src/work/waits/HelperAsks.tsx:121`, mounted at `Web/src/work/head/SessionHead.tsx:413-428`)
    sits under the heading *Helpers*. It shows **only open asks** (`openAsks`, `:62`): the question, who asked, the state,
    *Let it ask* / *Not now* and *Open helper*.
  - Once answered or refused, the ask leaves the head. Its comment says the answer is "said where it settled" and the
    helper "is a row under the session in Sessions' list" (`:57-61`).
  - The same ask is in the wait strip (`Web/src/work/waits/WaitStrip.tsx:164, 271-276`) and in *What needs you*
    (`Web/src/work/attention/attention.ts:360, 723`).
  - The person's own ask is `AskHelper` (`Web/src/work/start/AskHelper.tsx:26`), from the head's ⋯ *Ask another agent…*
    (`Web/src/work/acts.ts:135`).
- **The head, for the helper.** Its page shows its own conversation, with a *Helping* fact that links back to the lead
  (`SessionHead.tsx:493`; `Web/src/work/head/headFacts.ts:88`). There is no composer (`oneTurn`,
  `Web/src/work/WorkFrame.tsx:494`), word-taking acts are hidden (`acts.ts:131`), and going on is refused
  (`Web/src/work/head/sessionLife.ts:60-66`, *a helper takes one turn*).
- **The lead's conversation.**
  - The page's `SessionEvent` type has no `helper` field (`Web/src/work/conversation/conversation.ts:45`).
  - So a helper's answer, written as a `target` event, is drawn as **"The target Daoris composed"**, folded by default
    (`Web/src/work/conversation/ConversationView.tsx:388-389, 403`).
  - A reviewer's findings, by contrast, are drawn as *Another agent's findings*, open (`:382, 400`).
  - The helper's asked and answered lines are coded notes in the timeline. TASKS.md's MIXAGENT3a-iii, still open, is
    the row that was to draw the answer "as that agent's block".
- **A partner.** Only timeline notes (`Web/src/work/noteLines.ts:272-283, 422-443`). D199 §6.1's head, *Pieces* section
  and page are not built.
  - The page's `isHelper` is `Boolean(session.serves)` (`Web/src/work/identity.ts:79`), so a partner's record would be
    named *Helper*, its row *helps …*, its head *Helping*.
  - `piece` is read nowhere on the page but its type (`Web/src/data/api.ts:473`).
- **A reviewer.** `OpinionGate` (`Web/src/work/gates/OpinionGate.tsx:62`) in the head's work section
  (`SessionHead.tsx:458-461`), with its findings, answers and presses, and its *What needs you* rows
  (`Web/src/work/attention/AttentionRow.tsx:377`).
- **A hand-over.**
  - The agent's ask: `HandOverAgentAsk` (`Web/src/work/waits/HandOverAgentAsk.tsx:57`).
  - The person's *Go on with another agent…* (`acts.ts:134`).
  - The new part's brief: a folded card, `HandedAccount` (`Web/src/work/conversation/HandedAccount.tsx:24`).
- **Parts.** `PartsStrip` (`Web/src/work/head/PartsStrip.tsx:48, 103`), `PartsFlow` and `PartsFold`: one press per part
  shows that part's conversation (lines from `Web/src/work/head/partsLife.ts:28`).
- **Who it worked with.** `SessionRelations` (`Web/src/work/head/SessionRelations.tsx:36`) is about quests asked of other
  repositories and by them, not about helpers or reviewers.
- **The bound on answers alone.** `AnswerAlone` (`Web/src/work/waits/AnswerAlone.tsx:52`).
- **The Agents page** shows nothing of co-workers.

### 3.3 The words

| Key | English | 中文 | Where |
|---|---|---|---|
| glossary `helper` | helper | 助手 | `Web/src/locales/glossary.json:537` |
| glossary `reviewer` | reviewer | 阅读者 (avoid 审阅者, 审查者) | `:536` |
| glossary `second opinion` | second opinion | 第二意见 | `:535` |
| glossary `hand over` / `hand off` | hand over / hand off | 转交 / 交接 | `:543` / `:542` |
| glossary `part`, `session` | part, session | 段, 会话 | `:476`, `:475` |
| glossary `subagent` | subagent, "an agent a session starts to work beside it" | 子智能体 (avoid 子代理) | `:581` |
| glossary: *partner*, *lead*, *piece*, *co-worker* | none | none | D199 §6.6's names are not yet terms |
| `work.identity.helper` | Helper | 助手 | `en/work.json:4` |
| `work.helper.heading` / `.open` | Helpers / Open helper | 助手 / 打开助手 | `work.helper.json:9, 27` |
| `work.act.askHelper` | Ask another agent… | 问另一个智能体… | `work.json:37` |
| `work.act.handOver` | Go on with another agent… | 换个智能体继续… | `work.json:34` |
| `work.rail.helps` | helps {{title}} | 协助「{{title}}」 | `work.rail.json:33` |
| `work.head.helping` | Helping | 协助对象 | `work.head.json:8` |
| `work.life.cannot.serves` | a helper takes one turn | 助手只答一轮 | `work.life.json:26` |
| `work.conversation.target` | The target Daoris composed | Daoris 撰写的目标 | `work.conversation.json:7` |
| `work.conversation.opinion` | Another agent's findings | 另一个智能体的发现 | `work.conversation.json:10` |
| `opinion.session.reads` | reads {{title}} | 阅读「{{title}}」 | `opinion.json:127` |
| `note.partner.*` | *Partner {{agent}} …*, *piece* | 搭档, 分项 | `note.json:203-231` |
| `work.parts.title` / `.of` | Parts / Part {{n}} of {{total}} | 各段 / 第 {{n}} 段，共 {{total}} 段 | `work.parts.json:2-3` |
| `work.relations.title` | Who it worked with | 协作关系 | `work.relations.json:2` |
| `asks.elsewhere.*`, `history.count.teammates` | teammate (a person on another machine) | 队友 | `asks.json:145-152` |
| `settings.helpers.label` | Helpers | 助手 | `settings.helpers.json:2` |

*Co-worker*, *colleague*, 协作者 and 同事 appear in no catalogue. 协作 appears once, in *Who it worked with* (协作关系),
for other repositories. No catalogue labels *co-working* (D167's name is a design's and a skill's, not the window's).

### 3.4 The doors and tools

| Tool | Defined | Writes |
|---|---|---|
| `helper_ask` | `Daoris.Service.Mcp/HelperTools.cs:17` | `helpers/<id>.ask.json` (`HelperBox.cs:52-89`) |
| `helper_answer` | `HelperTools.cs:43` | `helpers/<id>.answer.json`, once (`HelperBox.cs:95-122`) |
| `partner_answer` | `Daoris.Service.Mcp/PartnerTools.cs:17` | `partners/<id>.answer.json` (`PartnerBox.cs:60-86`) |
| `opinion_give` / `opinion_answer` | `Daoris.Service.Mcp/OpinionTools.cs:16, 49` | the opinion's findings and answers (`OpinionDesk.cs:165, 270`) |
| `handover_propose` | `Daoris.Service.Mcp/HandOverTools.cs:17` | `sessions/<s>.hand-over-ask.json` |

There is no `partner_ask`. The terminal has `daoris-driver sessions helper <id> "<question>" [--agent]` and
`sessions helper --approve|--decline <ask>`; it has no verb that lists a session's helpers, and its list's projection
(`--json`) carries no `serves`.

## 4. A real helper on this install (2026-10-22)

GITAPP1's design session (quest `#8cbc56c9a098`, its record `0d2ab551`, Claude Code) asked Codex as its helper for an
independent proposal. Read from the install on 2026-10-23 through its own read-only doors only:

- the service's `GET /api/sessions` on loopback;
- the terminal's `daoris-driver sessions --group … --json`.

No file under the install's home was opened, and no sign-in was run.

- **The helper's record** is `29b4fe3d`:
  - `codex-acp` (`@agentclientprotocol/codex-acp 2.1.1`), `kind: chat`, `serves: 0d2ab551`;
  - its own entry `session:29b4fe3d/Daoris`, at base commit `afbb08079`;
  - created 16:29:57 UTC, `completed` at 16:45:03 UTC: about fifteen minutes, its whole one-turn bound;
  - its note is the coded `helper.helping` (*This session is helping session 0d2ab551: it reads, answers one question and
    changes nothing.*).
- **The lead** ended its turn at 16:29:22 to let the helper start, as the tool's description tells it to ("It reads your
  work as it stands when your turn ends … so it starts only then").
  - Its record became `awaiting-person`, its note the coded `ended.parked-asked` (*It stopped with its quest still taken,
    to ask you: …*), and it sits in the list's *You* group (`daoris-driver sessions --group you`).
  - The lead's own closing words say it is waiting for Codex, and its *Needs you* says nothing is needed.
- **The answer has not reached the lead.**
  - The lead's record holds no said word (`said` is empty).
  - Its last update is 16:29:22, before the helper started.
  - `HelperDesk.HandAsync` refuses a lead that is `AwaitingPerson`: *"waits on the person's answer: a helper's answer
    reaches it at a turn's end, once it is answered"* (`Core/Sessions/HelperDesk.cs:141-159`, the state at `:148`).
  - So the helper's finished answer waits, unseen in the lead's conversation, until the person answers a question the
    lead never asked.
- **In the terminal's list** the helper is its own entry in *Ended* (`shown: completed`), and no field of that list names
  the session it served. The lead is in *You*. The page's rail nests a serving record under its asker (`nestServing`,
  `Web/src/work/groups.ts:304-330`), but only where both are drawn: here the lead is in *You* and the helper in *Ended*.
- **What the person saw**, read from the records:
  - a session that seems to ask them something and does not;
  - a Codex session in *Ended* that looks unrelated to it;
  - an answer that goes nowhere.

  The same failure the install met with BGWAIT3: a session that ended its turn on its own background command was shown
  as waiting for the person (TASKS.md, DOGFOOD1's findings).

*This is the case the ask names, read at its records. The window itself was not opened here, so how the page drew it is
inferred from the code in §3, not seen.*

## 5. How other tools present several agents working together

A survey of public documentation and user reports, October 2026. Where a source did not describe a UI, nothing is
claimed for it.

### 5.1 Coding agents and IDEs

- **Claude Code.**
  - A subagent shows in the parent's transcript as a collapsed tool call; only its summary returns.
  - The VS Code extension draws the whole subagent transcript inline with no way to fold it, and users report the noise.
  - *Agent teams* (experimental, 2026) have a **team lead** and named **teammates**, each a full session, listed in an
    agent panel under the prompt. Enter opens a teammate's transcript and the person can type to it; Escape interrupts
    it.
  - Idle rows hide after 30 s, which confused users enough to earn a troubleshooting entry.
  - A teammate's final answer returns to the lead as an *idle notification*, and teammates' permission prompts surface in
    the lead.
  - <https://code.claude.com/docs/en/agent-teams>
- **Cursor.**
  - 2.0 introduced a sidebar of named agents with status (running, done, waiting).
  - 3.0's *Agents Window* lists every local and cloud agent, opened as ordinary editor tabs.
  - Subagents (2.4) appear as **cards in the parent chat; a card opens the subagent's chat in a tab**, with reported
    "Loading Chat" failures.
  - <https://cursor.com/changelog/2-4>, <https://cursor.com/blog/cursor-3>
- **GitHub Copilot coding agent.**
  - The agent is shown as a co-worker assigned an issue. It reacts, opens a draft pull request, and keeps the pull
    request's body as its status.
  - *View session* opens its live log beside *Overview* and *Files changed*.
  - Mission control lets the person steer it mid-run.
  - <https://github.blog/changelog/2025-10-28-a-mission-control-to-assign-steer-and-track-copilot-coding-agent-tasks/>
- **VS Code.**
  - A subagent is a collapsed tool call showing the agent's name and current activity.
  - In the Agents window, subagents are read-only chats with a lock that take no input, left out of the Sessions view.
  - <https://code.visualstudio.com/docs/agents/subagents>
- **OpenAI Codex.**
  - Subagents are spawned on request and shown in a panel with *Active* and *Done* lists, each with a nickname.
  - An approval from a background thread names its source thread, and a key opens that thread.
  - The parent returns one consolidated answer, so who found what is lost.
  - <https://learn.chatgpt.com/docs/agent-configuration/subagents>
- **Google Antigravity.**
  - An *Agent Manager* beside the editor; results come back as artifacts the person comments on inline.
  - Removing its Inbox of agent conversations drew complaints that watching many agents had become hard.
- **Devin.** A coordinator session delegates to managed child sessions and is woken when a child reports.
- **Zed.** A threads sidebar, each row with a status indicator and the name of the agent running it. External agents
  join by ACP. <https://zed.dev/docs/ai/parallel-agents>
- **Conductor, Windsurf.** One workspace or pane per agent, each a peer, not a child.

### 5.2 Chat apps with threads

- **Slack.**
  - A reply count and the repliers' avatars under the parent message; the thread opens in a side pane.
  - *Also send to #channel* deliberately promotes one reply back into the main flow.
  - <https://slack.design/?p=18127>
- **Discord.** Named threads with counts; a new thread is easy to miss unless its "thread created" line is seen.
- **Microsoft Teams.** A threads layout replies to one message in a side pane, keeping the main flow clean.
- **Linear.**
  - An agent is **delegated** an issue while a person stays its owner, and carries an agent badge.
  - Its session panel shows typed activities (thought, action, elicitation, response, error) and typed states (pending,
    active, awaitingInput, error, complete, stale).
  - Its guidelines: disclose identity, give instant feedback, make internal state transparent, stop when asked, keep a
    human accountable.
  - <https://linear.app/developers/aig.md>

### 5.3 Pair programming

- **Tuple.** Two cursors in contrasting colours, and a highlighted avatar for who has control.
- **VS Live Share.** A name flag on each cursor, and a participants list where a press *follows* someone.
- **JetBrains Code With Me.** Guests' icons in the toolbar, and a frame labelled with the followed person's name.

### 5.4 Frameworks with a UI

- **AutoGen Studio.** A team builder and a playground streaming messages between agents, with a graph of whose turn
  passed to whom.
- **ChatDev, MetaGPT.** Job-title roles (CEO, programmer, reviewer, tester; product manager, architect, engineer, QA).
  MetaGPT's roles hand each other **documents**, not chat.

### 5.5 What reads well, and what does not

**Reads well:**

1. **A row per co-worker with its live state, near where the person types.** Its name, what it is doing now, and its
   maker or model. Working and failed ones stay visible, and finished ones fold.
2. **A link from the parent into the child.** A card, a reply count or *View session* keeps the parent short and the
   detail one press away.
3. **The work product as the return.** A draft pull request, a commentable artifact or a document, rather than chat.
4. **Typed, named states**, especially *waiting for you* apart from *working*.
5. **An approval that names its source** and opens it.
6. **Ownership apart from execution.** A person stays accountable while an agent is delegated the work.
7. **Presence**: an avatar strip, a name flag, who has control.
8. **Talking directly to a peer**, rather than relaying through the lead.
9. **Promoting a side result into the main flow on purpose**, as Slack's *Also send to channel* does.

**Reads poorly:**

1. **A collapsed tool call as the only trace.** It reads as plumbing, not a colleague, and parallel ones blur together.
2. **A whole child transcript dumped inline.**
3. **Children that cannot be opened**, are locked without saying why, or are left out of the session list.
4. **No single place for what needs the person.**
5. **A result merged silently**, or reduced to a side note.
6. **Rows that hide themselves.**
7. **Progress that approves itself out of the person's sight.**

### 5.6 Names other tools use

- **English role words:** lead/teammate (Claude Code), coordinator/managed (Devin), parent/subagent (VS Code, Cursor),
  delegate/owner (Linear), and job titles (ChatDev, MetaGPT).
- **Chinese**, as far as public pages show:
  - Coding tools' Chinese documentation says 子代理 (Claude Code's zh-CN docs, Cursor's zh changelog). Daoris's glossary
    avoids 代理 for *agent*.
  - Platforms say 智能体 and 子智能体 (TRAE, Qoder), and workplace products 智能伙伴 and AI 数字员工 (Feishu).
  - Chinese coverage of Claude's agent teams says 智能体团队 and 队友.
- **协作者, 同事 and 搭档** were not found as any tool's word for an agent's role. 协作者 is the word Chinese git hosts use
  for a repository's *collaborators*, people granted access to it.

## 6. What the facts add up to

1. **The record model is mostly there.**
   - A helper and a partner are already session records of their own, linked by `serves`, each with its own entry.
   - A reviewer is a record linked by `opinion` to a second opinion, which is bound to a candidate, not to a session.
   - A hand-over is a part of the lead's own line (`from`), not something beside it.
   - What is missing is one notion that holds them together, and a page that draws them as one.
2. **The page draws each one differently** (§3.2).
   - A helper is a nested row, an open ask in the head that leaves once answered, and an answer folded under *The target
     Daoris composed*. That is the "side answer" the owner met, and in the code it is worse than a side answer: it is
     mislabelled.
   - A partner is notes only, and would be named *Helper*.
   - A reviewer is a gate in the head.
   - A hand-over is a part.
3. **A lead's state does not know its co-workers.** A lead that ended its turn to let a helper start is read as waiting
   for the person, and the helper's answer cannot reach it (§4). Anything that "nests under the lead with its live state"
   must first make the lead's own state right.
4. **Partners are not live yet.** No door asks one, and no measured row admits one (§3.1). Whatever a proposal draws for a
   partner is drawn before its first real use.
5. **The surface for a member's conversation exists.** A helper's record opens on the session page as a one-turn record
   with no box (`oneTurn`, `Web/src/work/WorkFrame.tsx:494`). What is missing is the way in from the lead, and a way back.
6. **The person cannot talk to a co-worker today.** A helper's and a reviewer's record refuses words
   (`SessionSayRefusal.Helper`); a partner has one turn and no resume, which is not measured for Codex (D199 point 2).
   "Within its role's bounds" has to say what talking *is* for a one-turn record.
7. **The names collide in places.**
   - *teammate* / 队友 is a person on another machine.
   - *co-working* is D167's name for one session per line and an ask's channel.
   - D199 rejected *co-work* as a name.
   - *Subagent* / 子智能体 is the glossary's word for "an agent a session starts to work beside it", a definition that
     already reads as *co-worker*.
   - 协作 already titles *Who it worked with* (协作关系), the relations with other repositories.
   - 协作者 means a repository's human collaborator in Chinese git hosting.
   - *Partner*, *lead* and *piece* are D199's names but not yet glossary terms.

## 7. The questions each proposal answers

A proposal answers each, and says where it amends a decision.

1. **The model.**
   - What a co-worker is, and what it is not. A hand-over and a part; an ask's other repositories' sessions; a meeting
     (D177); a native subagent inside the lead's own process; Ask Daoris's conversations.
   - Is a member one record, or one agent in one role over several records?
   - What makes a team, and what follows the lead through a hand-over?
2. **The records.**
   - What, if anything, changes in the session record, the boxes or the wire. How a reviewer joins the team, since its
     record names an opinion and not a session.
   - What the terminal lists, and with which verbs.
3. **The lead's state while co-workers work.** How the lead reads while a co-worker it asked is working, and when the
   answer reaches it (§4). Name what changes in `HelperDesk.HandAsync`, the planner and the groups.
4. **The page.**
   - The list (nesting, groups, what folds).
   - The head (the strip, its words, its marks for makers).
   - The lead's conversation (the card per exchange, its live state, what it says when answered).
   - The member's own conversation in the same surface (how it is reached and left, what it shows while it works).
   - What needs the person.
   Both languages, within the budgets.
5. **Talking to a member.** What the person may say to each role, how that is kept within the role's bounds (one turn, no
   resume where it is not measured, read-only, the answers-alone bound), and where the answer goes: to the person, to the
   lead, or both.
6. **The names.** The umbrella term, the team, the lead, each role and each state, in English and Chinese, against the
   glossary's terms and §5.6. Argue for 协作者 or against it.
7. **Both doors and Ask Daoris.** The terminal's verbs, and what Ask Daoris may propose.
8. **The rows.** In build order, each with its proof, and what is measured before it.
9. **What stays unmeasured**, and what the design does not cover.
