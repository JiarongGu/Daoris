# Co-workers: a session's team, the structures co-working takes, shown and driven like sessions (COWORKUX1, proposed, round 1, 2026-10-23)

**Standing:** proposed with D203, round 1: the synthesis of the three independent proposals in
`docs/2026-10-23-coworker-proposals.md` (Claude's, Codex's through Daoris's helper, Fable's), written from the brief
`docs/2026-10-23-coworker-brief.md` and the owner's direction on structures that reached the steward after the brief. Each
difference is settled in §1, by choosing one proposal's answer and saying what the others' answers lose. No reader has read this
round. By D191 Codex reads it, and Fable reads it as a foundation, since it changes how a driven turn's end is read (§5) and
what a reviewer's record carries (§4.1). The reads are the steward's to ask, after this quest: none was asked here, since a
helper's ask is the very path HELPERWAIT1 shows broken. Nothing here is built, publishes, signs in or spends an account.

Read for it: the brief and the three proposals whole; D51, D155, D163, D167, D188, D191, D193, D199 and D200 at the points this
cites; `.claude/knowledge/person-waits.md` and `session-record-fields.md`; the glossary. Facts marked *checked* were read in the
code at `0a4a14d9a` (this branch, `fe4809b75` with the proposals merged). `Dnn` is `docs/decisions/Dnn.md`; paths use the
brief's abbreviations (`Core/`, `Driver/`, `Web/`, `Mcp/` for `Daoris.Service/Daoris.Service.Mcp/`).

## 0. What changed since the brief

### 0.1 The owner's direction on structures

After the brief, the owner directed that co-working takes more than one structure, chosen by the task: a lead with subordinate
co-workers (lead and sub), peers working as equals (lead and lead), and others a task may need (a relay, a panel of reviewers,
more than two makers). All are to be supported, named first-class, chosen by the person, the asking agent or a workflow, shown
and driven like sessions, with the rules that hold in each (D51, D199, D163, D188, D155). Claude's and Codex's proposals were
sealed before it and answer only the lead-and-sub shape; Fable's was written with it. §3 is this design's answer.

### 0.2 Corrections to the brief, checked

1. **A reviewer's record is outside the server's role filter** (Fable §1.1). *Checked:* `OpinionDesk.AskAsync` opens the
   reviewer with `opinion: id, ownEntry: true` and no `serves` (`Core/Review/OpinionDesk.cs:128-131`); `RoleFilter` builds the
   role from `Serves` and `Piece` alone (`Mcp/RoleFilter.cs:13-19, 108-118`), so a reviewer reads as `SessionRole.None` and is
   refused neither of `ServingRefused`'s two quest tools (`:53-58`). Its refusals are its handed rules and each tool's own judge.
2. **The nesting did not fail across groups** (Codex X5, Fable §1.2). *Checked:* `nestServing` flattens every drawn group before
   it nests (`Web/work/groups.ts:304-316`), and `keepOwn` asks the nested row's own group (`Web/work/rail/SessionList.tsx:162-166`).
   A helper in *Ended* nests under a lead in *You* where both are drawn. What can still separate them is *Ended*'s truncation
   (`groups.ts:332`, *Show N more*), which drops a row before nesting can find it. The brief's §4 failure is the lead's state
   and words, and the terminal's flat list.
3. **A parked lead is skipped before the hand is tried** (Codex X4). *Checked:* the driver returns before `HandAsync` for any
   asker that is running or parked unless it is an open conversation or an urgent next step (`Driver/Loop/DriverHelpers.cs:411`);
   `HandAsync` would refuse it anyway (`Core/Sessions/HelperDesk.cs:148-149`). Both blocks change in §5.
4. **The lead ends its turn by design** (Fable §1.5). `helper_ask` tells the agent its helper starts once the turn ends, and the
   park is the reading of that end at `Driver/Sessions/Domain/Observation.cs:150-158` (*checked*). The fix is a reading.
5. **Two words the proposals used are taken** (new here, *checked*). The glossary's *panel* (面板) is the region under the centre
   (`Web/locales/glossary.json:635`), so a "panel of reviewers" cannot be the window's word. *Team* (团队) already names the
   people who share a workspace's remote in window strings (`en/errors.json:61`, `en/history.json:28`,
   `en/settings.tools.json:79`, and their zh twins), beside *teammate* (队友). §8 settles both. The glossary's *line* (主线) is
   the branch work lands on (`glossary.json:523`), so the window never says *line* for a session's line of work; it says
   *session*, as today.

## 1. The three proposals, question by question

Each row names what was chosen and what the others' answers lose. Where two proposals agree against the third, the choice still
rests on the reason given, not the count (D191 point 2).

| # | Question | Claude | Codex | Fable | Chosen, and why |
|---|---|---|---|---|---|
| 1 | What a member is | one agent in one role beside one line, keyed `(entry, role, agent)`, its records its exchanges | one commissioned exchange, one record | one record, one turn, one role | **One record** (Codex, Fable). A key by agent groups two unrelated asks to Codex as one colleague and needs a rule for when a "member" ends; a record is what the boxes, the terminal and the bounds already count. Claude's point, that a colleague stays across questions, is kept by the explicit thread `after` (§4.3): the person's *Ask again* joins the new record to the one it follows, so continuity is what someone did, never inferred from the adapter. |
| 2 | How a reviewer joins | joined through its candidate to the entry | joined through `Opinion.Working`; adding `serves` would misclassify | **the reviewer's record gains `serves`** | **`serves` on the reviewer** (Fable). §0.2's first correction is a confinement gap, not a drawing one: only `serves` puts the record under the server's filter. Codex's misclassification is answered by reading `opinion` before `serves` wherever a role is told, which `HandAsync` already does (`HelperDesk.cs:145-146`, *checked*). The join stays only as the reader of records from before (§4.1). |
| 3 | Stored role, team record | none | none, a projection | none | **None**, all three. The role is read from `serves`, `piece`, `opinion`; the team is a read over `serves` and the entry (D173's guard against a second store). |
| 4 | The lead while a member works | stays `working`, the planner holds it, no ledger state | stays parked in the ledger, with a durable *awaiting-member* cause every reader must tell apart | ends `completed` with `ended.awaits-helper`, the planner holds it from the box, the answer reopens it | **Fable's**, with Codex's guard that a bound question to the person wins and Codex's rule for a partner (§5). A `working` record with no process is what D200's watcher reads as quiet and the come-up reads as cut off, so Claude needs an exception in each (C4.3 already names one). A parked record keeps every `awaiting-person` reader (the bell, *What needs you*, PARKVIEW1's answer box) wrong until each learns the cause: the leak Codex itself names. An ended record held by the planner is the shape a D79 wait and a held word already have (*Resumes later*, `Driver/Trees/SessionGroups.cs:1094, 1116`, *checked*). |
| 5 | When no answer comes | the exchange ends, a note reopens the lead | the wait settles visibly, no fabricated answer | a reader's rule places the record in *You*, or one ledger move | **One ledger move** `completed → awaiting-person` at the refusal, with its own coded note (Fable's alternative). A reader's rule over `completed` would have to be taught to every place that keys on `awaiting-person` (person-waits.md: "waiting on you is taken"), the leak row 4 rejects. |
| 6 | The list | one nested row per member, the lead's row with marks | children with role, agent, activity, delivery; nest before truncating *Ended*; finished fold behind a count | nesting stays; the row says its role; ended members fold; the parent counts | **Fable's, with Codex's ordering**: nest across groups before *Ended* is truncated (§0.2's second point). |
| 7 | The head | a strip of chips; replaces *Helpers*; presses move to the card | a *Team* section | a *Team* section in the parts strip's shape; pending asks keep their presses | **Fable's section, titled *Co-workers* / 同事**. Claude was right that *Team* is taken (§0.2), wrong that the presses can leave the head: a wait the person answers is drawn in the head with `data-wait-section` (person-waits.md). |
| 8 | The card | one per exchange, updated in place, answer folded | one per commission, keyed by id, open, receipts | one per exchange, open, body from the box, never *The target Daoris composed* | **Fable's**, with Codex's three separate facts on it: the run (working, ended, refused), the delivery (waits, handed, taken) and, for a piece, the taking-in. The body comes from the ask's box, never from parsed prompt text, which on a driven door is a whole composed instruction. |
| 9 | The member's conversation | opened in the same surface with a back line, its exchanges as a strip | the actual record, *Back to lead*, confinement shown | a thread head with *Back to lead*, the receipt at its end | **Fable's**, with Codex's confinement line. Claude's exchange strip becomes the `after` thread. |
| 10 | Talking to a member | a further exchange briefed with the earlier ones; the person's answer to the person, *Hand to the lead* | a fresh commission; person-only by default, *Send to lead* | *Ask again*, a new record threaded; the answer always to the lead, *for me only* later | **A fresh record, threaded (all three); the person's follow-up answers the person, and reaches the lead by their press** (Claude, Codex). Fable's lead-always path makes every question the person puts to a co-worker a go-on of the lead: a turn, an account's spend and a step in the line that the person did not ask for. An answer the lead's own `helper_ask` solicited still returns to the lead, as D163 point 2 has it. |
| 11 | The umbrella word | co-worker / 同事 | co-worker / 协作者 | co-worker / 同事 | **同事** (§8.2). |
| 12 | The helper's role word | *reader* / 读 on chips; 助手 stays the term | keep *helper* 助手 | keep *helper* 助手 | **Keep 助手** (Codex, Fable). 读 sits beside 阅读者, the reviewer's word, and the owner's complaint was where the helper is drawn, which §6 changes. |
| 13 | Structures | not answered | not answered | two links and a channel; named first-class; each door starts one; no chooser, no stored structure | **Fable's model, with one place that offers them** (§3.3): the person chooses a structure by the door they press, and *Bring in another agent…* gathers the doors in one dialog. Fable rejected a chooser because each door decides; a dialog of the doors decides nothing new, and it is what makes "chosen by the person" visible. |
| 14 | The terminal | `sessions team`, `sessions ask <member>`, `sessions hand` | `team`, `exchange`, `follow-up`, `forward-answer` | `team`, the role verbs with `--after` | **Fable's** (the role verbs stay, a person types the role), with `--to-me` and `sessions hand <helper>` for row 10's choice. Codex's `exchange` is `team --json`'s member; Claude's member address `<entry>:<role>:<agent>` is row 1's rejected key. |
| 15 | Delivery identity | not raised | a stable key and a durable receipt; a restart mid-hand is *uncertain*, never sent twice | not raised | **Taken as a proof**, not a claim. The box records `Handed` after the keep (`DriverHelpers.cs:431`), and `HandAsync`'s duplicate check reads the record's `said` (`HelperDesk.cs:161-165`), so a crash between the keep and the box write, after the word was taken off `said`, could hand twice. HELPERWAIT1's Process case restarts at that point. |
| 16 | First row | the lead's state (HELPERWAIT1's mechanism) | authorship of the answer | today's helper in the team, no stored change | **Fable's first row**, the quest's order; HELPERWAIT1 keeps its own row and id, second, in another lane, built beside (§12). |

## 2. The model

### 2.1 A co-worker

**A co-worker is a session record of this machine's whose `serves` names a part of another session's line.** It has its own
entry, one turn and one role, and it never writes the lead's tree. The role is read, never stored, in this order:

| Role | Told by | Writes | Bound |
|---|---|---|---|
| reviewer (阅读者, D155) | `opinion` (and `serves`, §4.1) | nothing; reads a candidate's commits | one pass, one recheck per landing; its minutes |
| partner (搭档, D199) | `serves` and `piece` | a clone of its own, landed only by the lead's merge of a frozen ref | one turn, 45 min, two open pieces per line, admitted by a measured row, asks nobody |
| helper (助手, D163) | `serves` alone | nothing; reads a clone carrying the lead's work in progress | one turn, 15 min, two at once per asker, asks nobody |

**Not co-workers**, each kept where it is drawn today: a part (D167: a hand-over, a move, a carry-on, a retry, a resume are the
lead itself, in turn); another repository's session on the same ask (its own lead, met on the channel and by quest); a meeting's
participant (D177); a native subagent inside the lead's own process (no record, nothing Daoris confines; FABRIC-C1a/b bind it to
its part); Ask Daoris and an intake (they lead no team, and `helper_ask` already refuses them); a brainstorm's pass (no record);
a teammate (a person on another machine).

### 2.2 The team

**A session's team is its line and every record whose `serves` names any part of that line.** The line is the entry's parts
(D167). Through a hand-over the team follows: the new part's *Co-workers* lists the earlier parts' members as history, each with
the part it served, and open pieces travel along `from` (D199 point 6). A co-worker's record never gains a `from` and is never
handed over: its one turn ends or is stopped.

### 2.3 A thread

A record may name the record it follows, `after` (§4.3). A record and those after it are one **thread**: one row on the page, one
card in the lead's conversation with its follow-ups under it, one colleague the person asked again. Nothing else makes a thread.

## 3. The structures

### 3.1 Two links and one channel

Every structure the owner named is made of what the records already carry:

- **serving** (`serves`): a record works *for* a line in a role;
- **continuing** (`from`): a record carries a line *on*, handed Daoris's brief (D189);
- **the ask's channel**: records on one ask, each its own lead, talk at levels (D167, D188).

Each structure is named, because each answers *who writes the tree* and *who may start a turn* differently, and a person who
chooses one sees those answers. None gets a mechanism or a stored field of its own: a stored structure would drift from the links
that make it (D173's guard).

### 3.2 The structures, first-class

| Structure (en / zh) | What it is | Who writes the tree | Who starts a turn | Shown and driven as | Rules that hold |
|---|---|---|---|---|---|
| **team** / 团队 (lead and sub; the window says *Co-workers* / 同事) | a session and the records serving its line: helpers, partners, reviewers, of any makers | the lead alone; a partner its own clone, taken in only by the lead's merge | the person; the lead under the person's rule (`helpers`, `partners`); a workflow's occasion (`opinions`) | *Co-workers* in the head, a card per exchange, nested rows, each member's own conversation (§6) | D51; D199 points 1-4 (clone, admission, depth one, two pieces); D163 points 2-3 (one turn, two helpers, the rule); D155 points 5-8 (a reviewer's copy, agreement establishes nothing); D188 point 4 (a solicited answer reopens its asker once per ask; at most two answers alone); D137 (a claim is never the person's words) |
| **alongside** / 并行 (lead and lead) | two leads of two lines on one ask: two parts alongside in one entry (D167's 2026-10-14 amendment), or two repositories' sessions | each its own tree; never two writers in one tree | the person; each lead its own turns; a post never starts a run | each its own session; the entry's *Parts* strip; the ask's page and channel; one *Accept* for the ask (ACCEPTALL1) | D51; D167 points 6-8; D188 points 2 and 4 (a post never starts, reopens or extends a run; three urgent posts in ten); D45 (never into the other's repository); D154/D155 at each landing |
| **relay** / 接力 | a line's parts in sequence: a hand-over (D163), a move (D189), a carry-on, each `from` the last | one part at a time, the same tree; the old ends before the new starts | the person (*Go on with another agent…*); the lead under `handOver`; Daoris's carry-on | the *Parts* strip and flow; the brief's folded card | D189 (the brief, never the transcript or a model's summary); D163 point 3; D199 point 6; D80's strikes |
| **several reviewers** / 多位阅读者 (the owner's *panel*) | N reviewers of one candidate, each its own opinion, each a member of the team | nothing | a workflow's occasion; the person's ask at the gate | each a member in *Co-workers* and a card; the gate lists each opinion | D155 points 4 and 8; one gate; **amends D155 point 10's one pass per landing** when its row is built (§12 row 9) |
| **mixed makers** / 多厂商 (more than two makers) | any structure above with members of several makers | as its structure | as its structure | the maker's mark on every member, never a hue alone | D155 point 4 (independence by maker); D197 (accounts); D199 point 9 (usage per maker, *not measured* never zero) |

**Two equals are two leads of two lines, never two leads of one tree.** The symmetric reading, two agents each writing pieces for
the other and each merging the other's frozen ref, is rejected: D51 and D199 point 2 forbid two writers in one tree, a partner
asks nobody, and two trees each taking the other's pieces are two diverging lines with nobody to settle them but a model (D24,
D54). Equals share the ask's channel and the person's one *Accept*.

**The gap alongside parts have:** two parts of one entry do not hear each other on the ask's channel. A message posted by a part
of an entry is not handed to that entry (D167's SESSONE2b note, *checked* at `docs/decisions/D167.md:218`). CHANPART1 (§12
row 8) hands it to every live part of the entry but its poster, with D188's receipts per part.

### 3.3 How a task chooses one

Each door starts one structure; the person's rules say what an agent's door does. No model chooses a structure (D24, D54).

| Who | Door | Structure | Rule |
|---|---|---|---|
| the person | *Bring in another agent…* (§6.2, row 7) gathering the doors below, or each door where it is today | any | the door's own |
| the person | *Ask another agent…*, `sessions helper` | team, a helper | none: the press is their yes |
| the person | a piece's door, `sessions partner` (COAGENT1b) | team, a partner | admission by a measured row |
| the person | *Ask now* / *Try again* at the gate, `opinion ask` | team, a reviewer | the opinion rule |
| the person | *Go on with another agent…*, `sessions hand-over` | relay | none |
| the person | a second request on the ask; the intake's split | alongside | D167 |
| the lead | `helper_ask` | team, a helper | `helpers` (`ask` / `on` / `off`) |
| the lead | `partner_ask` (COAGENT1d) | team, a partner | `partners` |
| the lead | `handover_propose` | relay | `handOver` |
| the lead | `channel_post`, `quest_publish` | alongside (across repositories) | the channel's bounds; a quest's doors |
| a workflow | the opinion's occasions `landing` and `steps` (D155 point 2) | team, a reviewer; several reviewers after row 9 | the repository's or workspace's rule |
| a workflow | a declared node that asks for a helper, a piece or several reviewers | team | row 10: a node under D157/D190, never a model's reading |

Ask Daoris proposes any of the person's doors as a `session` card and applies none itself (D110, D183).

### 3.4 Rejected for the structures

- **A `structure` field, or a structure chosen apart from its door**: the shapes are read from `serves`, `from` and the ask.
- **Symmetric partners, or two leads of one tree** (§3.2).
- **A coordinator agent** that dispatches the team and merges its results: D193 rejects agents directing agents.
- **Depth past one**: a helper asking for a helper, a partner asking for anything, a reviewer asking for a reader (D163 point 2,
  D199 point 2).
- **Members talking to each other.** They hear each other only through the lead's turns and the ask's channel; a member-to-member
  message is two agents conversing with no person between (D155, D163, D199 point 10).

## 4. The records

### 4.1 The reviewer's record gains `serves`

`OpinionDesk.AskAsync` opens the reviewer with `serves: working.Id` beside `opinion`, through `session-record-fields.md`'s rows that
apply to a field set at the open, of which `serves` already has every one (the create takes it, the column and reader exist, the
response guards it on loopback, the driver reads it, the stand-ins echo it). What it buys:

- the server's filter refuses the reviewer `quest_publish` and `quest_respond` (`RoleFilter.ServingRefused`), and
  `McpRoleFilterTests`' census gains its row;
- the team is one read on the page and in the terminal; the rail's nesting stops needing the opinions join
  (`SessionList.tsx:92-96`, its `serving` prop) for a record made after the change, and keeps it for one made before;
- every desk that refuses a serving asker refuses it, as each already does by `opinion`.

What moves with it: every reader that takes `serves` to mean *helper*. On the page `isHelper` (`Web/work/identity.ts:79`) becomes
`roleOf(session)` (`'reviewer' | 'partner' | 'helper' | null`, in §2.1's order), and its readers move: `sessionTitle`, the rail's
`helps` line (`SessionRow.tsx:198-203`), the head's *Helping* fact (`SessionHead.tsx:493`, `headFacts.ts:88`), `sessionLife`'s
`serves` word and `acts.ts`'s wordless set. In the driver, every reader that tests `Serves` for a helper reads `Opinion` first.
D155 point 6 stays true: `serves` is a link, and the reviewer is handed no conversation.

### 4.2 The page's event gains `helper`

`SessionEvent` (`Web/work/conversation/conversation.ts:45`) gains `helper?: string | null`, which the driver already writes on the
answer's event (`DriverHelpers.cs:443`, *checked*). The card uses it to place the answer and read its receipts; the body comes from
the box (§6.3). This is MIXAGENT3a-iii's block half.

### 4.3 The helper's ask gains `after` and `to`

Two optional fields on the helper's ask box and its twin, both written only by the person's doors, an absent one read as today:

- `after`: the helper record this ask follows, which threads it (§2.3);
- `to`: `person` where the answer goes to the person and not to the lead (§7). Absent is `lead`, today's one path.

### 4.4 Nothing else on the record or the wire

No role column, no team record, no mark on the said word: a helper's word names its record in `helper`, a partner's the same with
its key, a reviewer's the opinion in `by` (`Core/Sessions/Sessions.cs:375-380`). `serves`, `piece`, `opinion` stay this machine's
own (D47 §4): a teammate's mirror shows no team, and says *not shown for another machine's session* where a mirror's head would
otherwise hold the section.

### 4.5 The terminal

- `daoris-driver sessions --json` gains `serves`, `piece` and `role` on each part; the plain list prints a member under the line it
  serves with its role word, as the page nests it.
- `daoris-driver sessions team <session> [--json]` prints the line's team: each member's id, role, agent and account, state, who
  asked and when, its thread, and where its answer went. A read, no lease.
- The role verbs stay: `sessions helper` (gaining `--after <helper>` and `--to-me`), `sessions partner` (COAGENT1b), `opinion
  ask|show|stop`, `sessions hand-over`. `sessions hand <helper>` hands a person-only answer to the lead (§7).

## 5. The lead while its co-workers work: HELPERWAIT1's mechanism

**The rule: a lead that ended its turn on its own open ask waits on that co-worker, by name, and on the person only where it also
asked the person, or the co-worker ends with no answer.** HELPERWAIT1 (TASKS.md) keeps its row and its id; this is its mechanism.

1. **The reading.** In `Observation` (`Observation.cs:150`), before the park: a clean exit with the quest taken that `Parks(…)`,
   where the session has an open helper ask of its own (`waiting`, `approved`, `started`) and **no bound question to the person**
   (an open `question_ask` or `go_ahead_ask`, the awaits `Parks` already reads), concludes `completed` with the coded note
   `ended.awaits-helper` (`ask`, `agent`): *It ended its turn so {agent} could read its work and answer; it goes on when the answer
   comes.* Its last words are never read for intent (Codex X4). Where a bound question is open, the park stands, and the card says
   the answer reaches the session with the person's (point 6).
2. **A partner only where the lead yielded for it.** `ended.awaits-partner` is read only where the lead itself asked the piece
   (`partner_ask`, COAGENT1d) in the turn that ended. A piece the person asked does not hold its lead (Codex X4).
3. **The planner holds it.** A `completed` record with its quest taken and `ended.awaits-helper` whose ask is open is held: no park
   row, no carry-on, no strike, as a held word is (`SessionGroups.cs:1094`). The look fills the snapshot's helper asks from the box it
   already reads each tick, so the planner keeps no mark of its own. It takes no running slot, as a D79 wait does not, and its tree
   stays its own.
4. **The answer reopens it.** `DriverHelpers` stops skipping a record that is ended and held this way (`:411`), and `HandAsync`
   takes it (`HelperDesk.cs:157`'s `_ => null` arm already does for an ended record), keeping the word with `Reopens: true`; the next
   look goes on with it (D137), one go-on under the answers-alone bound (D188 point 4), unchanged.
5. **No answer brings it to the person.** Where the look writes `helper.refused` (any `HelperWhy`) for an ask the record waits on,
   the record moves `completed → awaiting-person` with the coded note `ended.helper-unanswered` (*{agent} ended with no answer; it
   stopped to wait on that answer, and now waits on you:* then its last words). From there it is the park it would have been, and
   every reader of a park reads it unchanged.
6. **A lead parked on the person keeps its park.** Its helper's answer is kept on its record at once (`HandAsync` stops refusing
   `AwaitingPerson`, keeping the word with `Reopens: false`), so it is never lost and is visible on the card and in the member's
   thread: *answered · reaches the session with your answer*. The person's answer then carries it as *your words and a helper's
   answer* (`words-and-helper`, already built).
7. **Where it shows.** The record sits in *Resumes later*, its line *waits for {{agent}}'s answer* (等 {{agent}} 答复), the
   *Co-workers* section showing the member working; *What needs you* lists nothing for it until point 5.
8. **A conversation lead** stays `working` between turns and takes the answer as its next turn (MIXAGENT2a-iii): no change.

## 6. The page

### 6.1 The list

- **Nesting stays** as MIXAGENT3a-ii built it (moved, never copied; one level; a row the person must act on keeps its place),
  **before *Ended* is truncated**, so a finished member is found under its lead.
- **The nested row says its role**, not its kind: maker mark · *{role} · {state}* (*Cx helper · working*, *CC reviewer · given*).
- **The lead's row** gains one fragment where it has members, *{{count}} co-workers* / {{count}} 个同事.
- **Ended members fold**: under a lead, the newest two are drawn and the rest are *{{count}} more* on one row, opened in place. A
  working, waiting or failed member is never folded. A thread is one row with its count.

### 6.2 The head: *Co-workers*

A section under the head beside *Parts*, in `PartsStrip`'s shape (one button per row, nothing drawn where there is nothing to say).
It replaces *Helpers* (`HelperAsks`, mounted at `SessionHead.tsx:416-430`); the open asks become its pending rows and keep their
presses and `data-wait-section`.

| Row | Reads | Press |
|---|---|---|
| the lead | *Lead · Claude Code* (主导 · Claude Code), its part where the line has parts | none |
| a member working | mark · *Codex · helper · working* · its call line (the rail's `calling`) · spend where measured · *4 min* | opens its thread |
| a member answered | mark · *Codex · helper · answered* · where the answer went (*taken by part 2*, *waits for the turn's end*, *with your answer*, *to you*) | opens its thread |
| a member ended otherwise | *refused: {{why}}* / *stopped* / *out of time*, the timeline's words | opens its thread |
| a pending ask | *Codex · helper · waits for your yes* | *Let it ask* · *Not now*, as today |
| a piece (after COAGENT1b) | *Codex · partner* · D199 §6.1's state | opens its thread; *Stop…*, *Discard…* |
| a reviewer | *Codex · reviewer · reading* / *given* / *answered* | opens its thread; the gate stays where it is |

The section's ⋯ holds *Bring in another agent…* (row 7): one dialog whose choices are the structures' doors (*Answer a question*,
*Write a piece*, *Second opinion*, *Go on with another agent*, *Work alongside*), each judged by its own rule and admission as now, a
choice the rules turn off shown with its reason. The maker mark is two letters from the roster (*CC*, *Cx*, a plugin's declared
mark). Spend per maker is shown only where measured; *not measured* is never zero.

### 6.3 The lead's conversation: a card per exchange

- **Placed** at the ask (the `helper.asked` note or the `helper_ask` call, by the ask's id) and updated in place; the answer's
  event with `helper` (§4.2) gives the moment the lead took it.
- **Its head**: *Codex, as helper* (Codex，作为助手) with the mark, then three facts kept apart: the run (*reading your work as of
  16:29 · running `rg` · 4 min*, *answered*, *refused: out of time*), the delivery (*waits for the turn's end*, *taken as the next
  turn*, *to you*), and for a piece its taking-in.
- **Its body**: the question as written, then the answer as written from the box (the head's helper read gains the answer's text,
  bounded as last words are, *Show all* past it), under *a claim to check, not your words*. A last-words answer says so.
- **Its doors**: *Open conversation* (打开对话); for a person-only answer, *Hand to the lead* (交给主导).
- **A follow-up's card** is drawn under the card it follows.
- **A reviewer's card**: the findings block as today, retitled *A reviewer's findings* (阅读者的发现), with *Open conversation*.
- ***The target Daoris composed*** is never drawn for an event carrying `helper` or `opinion`.

### 6.4 The member's conversation: a thread in the same surface

The same `WorkFrame` attending the member's record (`oneTurn` today), with:

- **a thread head**: *Working for {title}* (为「{title}」工作, `work.rail.under`'s words) with the lead's state, the member's role
  and mark, its confinement in one line (*reads a copy; changes nothing*), and *Back to lead* (返回主导), which attends the lead at
  the card it came from; the browser's back does the same;
- **while it works**: its conversation as any session's, *Stop…* in its head, no composer, and the foot's sentence *a co-worker
  takes one turn; ask it again once it has answered*;
- **once it ended**: its answer, then where it went (*went to {lead} as its next turn at 16:45*, *waits for {lead}'s turn to end*,
  *with your answer to {lead}*, *to you*), and *Ask again…*;
- **its follow-ups** as the thread's strip, in the parts strip's shape.

### 6.5 What needs you

No new kind. The helper's yes stays the `helper` wait, worded with the lead and the member (*Claude Code asks Codex to answer*);
its press opens the lead's card. The partner's yes is COAGENT1d's; disputes stay the opinion's; an uncertain piece stays D199's.
§5 point 5 adds none: it is the park row the record would have had.

## 7. Talking to a member

**Talking is asking again**: a new one-turn record of the same agent in the same role, on a fresh copy of the lead's work, its
`after` naming the one it follows. Nothing is resumed and no second prompt enters a finished record (resume is unmeasured for Codex,
SESSCTX1a; a steer is not built, STEER3; a reader that remembers answers about work that moved).

| Role | While it works | Once it answered | Where the answer goes |
|---|---|---|---|
| helper | *Stop…* | *Ask again…*: the earlier question and answer quoted as Daoris's record (bounded), the person's words under it | the person (`to: person`); the lead only by *Hand to the lead* |
| partner | *Stop…*, *Discard…* | *Ask again…*: a new piece from the lead's current base, the earlier answer quoted | the lead's line as a frozen ref, always (D199 point 3) |
| reviewer | *Stop…* | D155's recheck, once; past its budget, refused with a helper's ask offered beside it, never converted silently | the findings to the lead, the gate (D155 point 7) |

What holds it:

- **The person's ask is their word.** It is not under the rule, and `AnswersAlone.Resets` counts it, so a line the person keeps asking
  on is never held as answers alone; the lead's own `helper_ask` counts as today.
- **A person-only answer reaches the lead only by a press**, *Hand to the lead*, which hands the claim as the helper's, never relabelled
  as the person's, as one solicited answer under D188 point 4. A follow-up the lead asked returns to it as D163 point 2 has it.
- **Two helpers at once per asker** counts the follow-up; the minutes and the copy are its own.
- **Nobody but the person stops a member**, and no level does (D188 point 1).

## 8. The names

### 8.1 The terms

| Concept | English | 中文 | Kind, budget | Note |
|---|---|---|---|---|
| an agent working beside a session | **co-worker** | **同事** | the head's section (32/12); a `status`-sized word (16/5) | new term; `avoid` 协作者 |
| a session and its co-workers | **team** | **团队** | prose and the structure's name; the window titles the section *Co-workers* | §0.2: *team* already names people |
| the session's own agent | **lead** | **主导** | `status` | D199 §6.6's, made a term |
| the roles | **helper**, **partner**, **reviewer** | **助手**, **搭档**, **阅读者** | kept | *partner* made a term |
| a partner's work | **piece** | **分项** | kept | D199's, made a term |
| the structures | **team**, **alongside**, **relay**, **several reviewers**, **mixed makers** | **团队**, **并行**, **接力**, **多位阅读者**, **多厂商** | prose; the dialog's choices are the doors' own words | *panel* is the window's region (面板) |
| a record and those it follows | **thread** | **串** in prose; the window says *follow-ups* / 追问 | — | not a glossary term until the window needs it |
| subagent | subagent | 子智能体 | narrowed | *an agent a session starts inside its own process, with no record of its own*; `avoid` 子代理 stays |

### 8.2 Against 协作者, for 同事

1. On Chinese git hosts 协作者 is a *collaborator*: a person granted rights in a repository (brief §5.6). A co-worker has no rights in
   the lead's tree, and git access is about to reach Daoris's own screens (D198, GITAPP1).
2. 协作 already titles *Who it worked with* (协作关系, `work.relations.title`), the relations with other repositories: exactly what a
   co-worker is not.
3. It spends three of a status word's five units; 同事·助手 leaves room for the state.
4. Codex's argument, that 同事 claims employment, is the point: D193 frames sessions as the staff of a company, and the owner's own
   word for what is missing was a colleague. 同事 is two characters and in no catalogue today.

*Colleague* would serve as well in English; *co-worker* is the ask's word and costs nothing to keep. D199 rejected *co-work* as a
partner's name, where it blurred the writing role; as the umbrella it blurs nothing. *Teammate* stays the person on another machine.

### 8.3 Words that move, and new

Keys stay, values move, both languages, `names:check -- --strict` and `i18n:check`:

- `work.helper.heading` *Helpers* / 助手 → *Co-workers* / 同事;
- `work.conversation.opinion` → *A reviewer's findings* / 阅读者的发现;
- `work.head.helping` *Helping* / 协助对象 → *Works for* / 为谁工作;
- `work.life.cannot.serves` → *a co-worker takes one turn* / 同事只答一轮.

New: `work.team.*` (the section, the rows, the thread head, *Back to lead* 返回主导, *Open conversation* 打开对话, *Hand to the lead*
交给主导, *Ask again…* 再问…, *Bring in another agent…* 请另一个智能体…), `work.conversation.helper`, and the notes
`ended.awaits-helper`, `ended.awaits-partner`, `ended.helper-unanswered`. State words reuse `work.helper.state.*`, whose sentences are
content-length lines, not statuses; the role words on a row are statuses (*working* 工作中, *answered* 已答复, *given* 已给出, *refused*
已拒绝, *stopped* 已停止, *out of time* 超时, *taken in* 已并入). Each new label is checked against its kind's budget in its row.

## 9. Both doors, and Ask Daoris

- **The terminal**: §4.5. Every refusal is the role door's, in the same words.
- **Ask Daoris** may propose a `session` card for each of the person's doors (*ask Codex as a helper about …*, *ask again after …*,
  *hand Codex's answer to the lead*), with the terminal's equivalent beside it; the person applies. It never asks a co-worker itself and
  never hands an answer on (D110, D183). `HelpCoverageTests` names each new hook, the person's own asks as exempt as `useAskHelper` is.
- **The settings doors** (`helpers`, `partners`, `opinions`, `handOver`) are unchanged.

## 10. The cost

- **No model** (D24): every word is Daoris's fixed words or an agent's as written.
- **No new poll**: the team is the session list the page holds; the pending asks and the answer's text ride the head's existing helper
  read for the attended session; a member's call line is the rail's `calling`; spend is the existing read.
- **Accounts**: *Ask again* spends one more one-turn session on the member's agent; the dialog says which account the walk would take.
  Nothing here starts a turn by itself.

## 11. The test plan

Fast halves by row, both languages wherever words are drawn; Process cases where a row changes a turn's end or a hand:

| Row | Tests |
|---|---|
| 1 | `identity.test.ts` (roles from the fields, in §2.1's order); `groups.test.ts` (three roles nested; nested before *Ended* is truncated; the fold; a working member never folded); `CoWorkers.test.tsx` (the lead's row, a member per state, a pending ask with its presses and `data-wait-section`, the mark, nothing for a session alone); `ConversationView.helper.test.tsx` (placed by the ask, updated by the answer's event, body from the box, last words said, never *The target Daoris composed*); `SessionRow.coworker.test.tsx`; stories per state at 888 px, both themes; `SessionsCommandTests` (`team`, the JSON fields); `glossary.test.ts`, `names:check -- --strict`, `i18n:check` |
| 2 (HELPERWAIT1) | `ObservationTests` (a clean taken exit with an open ask reads `awaits-helper`; with an open question to the person, the park; with none, the park as before; a person-asked piece does not hold); the planner's plan tests (held, no carry-on, no park, no strike; released and gone on at `answered`; moved to the person at a refusal); `HelperDeskTests` (an ended held lead takes the word; a parked lead keeps it with `Reopens: false`); `SessionGroupsTests`; `AttentionStateSliceTests` (no row while held; a row at the refusal); Process: `HelperProcessTests` gains a stand-in lead that asks and ends its turn, reads as waiting on its helper and goes on with the answer; one whose helper ends unanswered and reaches the person; one restarted between the keep and the box's `Handed`, which hands once; cap one |
| 3 | `WorkFrame.thread.test.tsx` (into a member and back to its card; the detached window's head); `sessionLife` (the sentence) |
| 4 | `SessionStoreTests` (a reviewer's `serves` written and read; null before and on a mirror); `OpinionHostTests` (loopback only); `McpRoleFilterTests` (a reviewer refused the two quest tools; the census row); the three stand-ins; the page's nesting without the join and with it for an older record |
| 5 | `HelperFilesTests`, `HelperBoxShapeTests` (the twin shape with `after` and `to`); `SessionsHelperCommandTests` (`--after`, `--to-me`, `hand`); `HelperDeskTests` (a person-only answer kept on the member, handed once by the press); `AnswersAloneTests` (the person's follow-up resets; the press's hand counts once); `HelpCoverageTests`; the dialog in both languages; a Process case: a follow-up on a fresh copy, answered to the person, then handed |
| 6 | the Pieces tests moved under *Co-workers*; `ObservationTests` for `awaits-partner` |
| 7 | the dialog's choices each judged by its door, a choice turned off with its reason, `HelpCoverageTests` |
| 8 | the channel's delivery tests: a part's post handed to the entry's other live parts and not to the poster, D188's receipts per part |
| 9, 10 | written with their rows |

**The install's proof** (the parent, after the republish, with the owner's yes where an account is spent): GITAPP1's case again, a
Claude Code session asking Codex as a helper and ending its turn. The window shows the lead *waiting for Codex's answer* in *Resumes
later*, Codex under it in *Co-workers* and the rail as *helper · working*, the card filling in, the lead going on with the answer; then
*Ask again…* from the card, answered to the person, and *Hand to the lead*; shots at 888 px in both themes and languages, and
`sessions team` beside them. The budgets of the rows and the thread head are the window's to judge (D116 §3).

## 12. The rows, in build order

Each row is one feature a person can see working (D187). Filed in TASKS.md when D203 is accepted; until then COWORKUX1 points here.

1. **COWORKUX1a — today's helper as a co-worker in the session's team** (web, modules, driver's terminal; no stored shape changes).
   The terms (§8.1), `subagent` narrowed; `SessionEvent.helper`; *Co-workers* replacing *Helpers*, the pending asks' presses kept; the
   card with its body from the box; the nested row's role, the lead's count, nesting before truncation, the fold; `sessions --json`'s
   fields and `sessions team`. Closes MIXAGENT3a-iii's block half. **The first showing.**
2. **HELPERWAIT1 — a session waiting on its own helper is not shown as waiting on the person** (driver, modules, web; its own row,
   kept). §5 whole. Built beside row 1, since its lanes are the driver's and the modules'; row 1's install proof waits for both, since
   every live state on the page reads wrong until the lead's own state is right.
3. **COWORKUX1b — the member's conversation as a thread** (web). §6.4.
4. **COWORKUX1c — the reviewer joins the team** (service, driver, modules, web). §4.1; amends D155 point 11's record.
5. **COWORKUX1d — talking to a helper: *Ask again* and *Hand to the lead*** (service box twins, driver, modules, web, Ask Daoris
   coverage). §4.3, §7; amends D163 point 2 for the person's follow-ups.
6. **COWORKUX1e — the partner as a member** (web, driver; after COAGENT1b, `awaits-partner` after COAGENT1d). D199 §6.1's Pieces
   drawn as *Co-workers* rows and cards.
7. **COWORKUX1f — *Bring in another agent…*: the structures chosen by the person in one place** (web, Ask Daoris). §6.2's dialog.
8. **CHANPART1 — parts alongside in one entry hear each other on the ask's channel** (driver, service). §3.2's gap; amends D167's
   SESSONE2b note.
9. **COWORKUX1g — several reviewers of one candidate** (service, driver, web; after row 4). N opinions of different makers, one gate,
   any `must` finding standing until answered, agreement establishing nothing; amends D155 point 10's cap.
10. **COWORKUX1h — a workflow node that brings in a co-worker** (after COAGENT1d and row 9). A declared node under D157/D190 asking for
    a helper, a piece or several reviewers.

## 13. Not built, and unmeasured

- **Not built:** a composer on a member's page, a resumed or steered member (SESSCTX1a, STEER3 first); member-to-member messages; a
  coordinator; a stored structure; a team on a teammate's mirror; the Agents page's co-worker facts (COAGENT1e's sheet); a native
  subagent as a member (FABRIC-C1b).
- **Unmeasured:** the window was not seen, so the rows' and thread head's budgets and the fold's reading are the install's; §5 rests
  on `Parks(…)`'s inputs and the box's states as read, not run; whether the held lead needs a mark of its own after a restart (the box
  is on disk, so the reading is that it does not) is row 2's Process case to show; the restart window in §1 row 15 likewise; a real
  partner's confinement and usage are COAGENT1a's; what a real lead does with a handed follow-up is the install's.

## 14. What it amends, once accepted

- **D163**: point 2's "its answer returning as the asker's next turn" holds for the agent's ask; the person's follow-up answers the
  person, and reaches the lead by their press (§7). Point 4's view is §6: *Helpers* is *Co-workers*, the answer a card.
- **D155**: point 11's record gains `serves`, this machine's own as `opinion` is (§4.1); point 10's cap is amended by row 9 when built.
- **D199 §6.6**: *co-worker* 同事 and *team* 团队 join the names; the head's partners line becomes *Co-workers* rows. Points 1-10 stand.
- **D167**: CHANPART1 amends the SESSONE2b note's "not handed to the entry" for alongside parts. Points 1-8 stand.
- **D116's glossary**: *subagent* narrowed; *co-worker*, *lead*, *partner*, *piece* become terms.
- **D51, D137, D177, D188, D189, D193** stand. D193's rejection of agents directing agents is what §3.4 rejects a coordinator by.

## 15. The reads

None yet. Codex's read and Fable's foundation read go here, finding by finding, each with the steward's verdict against the code.
