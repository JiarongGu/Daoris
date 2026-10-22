# Co-workers: a session's team, the structures co-working takes, shown and driven like sessions (COWORKUX1, proposed, round 4, 2026-10-23)

**Standing:** proposed with D203, round 4: the synthesis of the three independent proposals in
`docs/2026-10-23-coworker-proposals.md` (Claude's, Codex's through Daoris's helper, Fable's), written from the brief
`docs/2026-10-23-coworker-brief.md` and the owner's direction on structures that reached the steward after the brief. Each
difference is settled in §1, by choosing one proposal's answer and saying what the others' answers lose. Round 1 (`2bdc8f5eb`)
was read by Codex (17 findings, 14 blocking, *revise and read again*) and by Fable as a foundation (F1-F14, F1-F5 blocking,
*accept after them*), both in `docs/2026-10-23-coworker-reads.md`. Round 2 checks every finding against the code and takes or
answers it; §15 is the table. Codex read round 2 (8 findings, 6 blocking, *another round*): the wait's contracts were assigned to
an unfinished HELPERWAIT1, which resolves nothing. HELPERWAIT1 has since landed, so round 3 states §5 as built, each point with its
code and tests by file and line, and takes or answers Codex's round-2 findings against that code (§15.3). Codex read round 3
(6 findings, 5 blocking, *another round*): the design held, but the built wait did not keep four of its guarantees. HELPERWAIT1b
fixed each in the code, red first, and round 4 cites each fix by file and line and makes the refused return's row a prerequisite of
the first showing (§15.4). Two
structures the owner named are not yet specified enough to build, so their rows wait on a design of their own, with the reason
(§12 rows 8 and 9), and the partner's wait waits on COAGENT1d's `partner_ask` (§12 row 12). D203 stays proposed until the steward
accepts. Nothing here is built, publishes, signs in or spends an account; §5's mechanism is HELPERWAIT1's, already built.

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
   A helper in *Ended* nests under a lead in *You* where both are drawn. *Corrected in round 2* (Codex 15, Fable F6): the rail
   already nests before it cuts *Ended* (`nest(byState…)` at `SessionList.tsx:238`, then `ended(…)` at `:246` over the nested
   rows, `cutEnded` at `groups.ts:339-346`), in both arrangements. Round 1's "truncation drops a row before nesting can find it"
   is withdrawn. What can still hide a member is its **lead** cut: an ended lead past *Ended*'s first rows takes its nested
   members behind *Show N more* with it (§6.1 adds the rule). The brief's §4 failure is the lead's state and words, and the
   terminal's flat list.
3. **A parked lead is skipped before the hand is tried** (Codex X4). *Checked:* the driver returns before `HandAsync` for any
   asker that is running or parked unless it is an open conversation or an urgent next step (`Driver/Loop/DriverHelpers.cs:411`);
   `HandAsync` would refuse it anyway (`Core/Sessions/HelperDesk.cs:148-149`). *Corrected in round 2* (Fable F8): neither block
   changes for the waiting lead, which is `completed`, so neither running nor parked, and `HandAsync`'s `_ => null` arm
   (`HelperDesk.cs:157`) already keeps its word with `Reopens: true`. A parked lead keeps both blocks (§5 point 5).
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
| 5 | When no answer comes | the exchange ends, a note reopens the lead | the wait settles visibly, no fabricated answer | a reader's rule places the record in *You*, or one ledger move | **One ledger move** `completed → awaiting-person` at the refusal, with its own coded note (Fable's alternative). A reader's rule over `completed` would have to be taught to every place that keys on `awaiting-person` (person-waits.md: "waiting on you is taken"), the leak row 4 rejects. Round 2: the move is a new, guarded transition in the service, not one the ledger already makes (Codex 4, Fable F1); HELPERWAIT1 built it (§5 point 4). |
| 6 | The list | one nested row per member, the lead's row with marks | children with role, agent, activity, delivery; nest before truncating *Ended*; finished fold behind a count | nesting stays; the row says its role; ended members fold; the parent counts | **Fable's**. Round 1 took Codex's ordering as a change; it is already the code's (§0.2's second point, corrected). The change is the fold, and that a lead with a live member is never cut (§6.1). |
| 7 | The head | a strip of chips; replaces *Helpers*; presses move to the card | a *Team* section | a *Team* section in the parts strip's shape; pending asks keep their presses | **Fable's section, titled *Co-workers* / 同事**. Claude was right that *Team* is taken (§0.2), wrong that the presses can leave the head: a wait the person answers is drawn in the head with `data-wait-section` (person-waits.md). |
| 8 | The card | one per exchange, updated in place, answer folded | one per commission, keyed by id, open, receipts | one per exchange, open, body from the box, never *The target Daoris composed* | **Fable's**, with Codex's three separate facts on it: the run (working, ended, refused), the delivery (waits, handed, taken) and, for a piece, the taking-in. The body comes from the ask's box, never from parsed prompt text, which on a driven door is a whole composed instruction. |
| 9 | The member's conversation | opened in the same surface with a back line, its exchanges as a strip | the actual record, *Back to lead*, confinement shown | a thread head with *Back to lead*, the receipt at its end | **Fable's**, with Codex's confinement line. Claude's exchange strip becomes the `after` thread. |
| 10 | Talking to a member | a further exchange briefed with the earlier ones; the person's answer to the person, *Hand to the lead* | a fresh commission; person-only by default, *Send to lead* | *Ask again*, a new record threaded; the answer always to the lead, *for me only* later | **A fresh record, threaded (all three); the person's follow-up answers the person, and reaches the lead by their press** (Claude, Codex). Fable's lead-always path makes every question the person puts to a co-worker a go-on of the lead: a turn, an account's spend and a step in the line that the person did not ask for. An answer the lead's own `helper_ask` solicited still returns to the lead, as D163 point 2 has it. The one rule (round 2, Fable F11): the answer goes where the person asked from, the lead's page to the lead and the member's thread to the person, and *for the lead* or `--to-me` turns either. |
| 11 | The umbrella word | co-worker / 同事 | co-worker / 协作者 | co-worker / 同事 | **同事** (§8.2). |
| 12 | The helper's role word | *reader* / 读 on chips; 助手 stays the term | keep *helper* 助手 | keep *helper* 助手 | **Keep 助手** (Codex, Fable). 读 sits beside 阅读者, the reviewer's word, and the owner's complaint was where the helper is drawn, which §6 changes. |
| 13 | Structures | not answered | not answered | two links and a channel; named first-class; each door starts one; no chooser, no stored structure | **Fable's model, with one place that offers them** (§3.3): the person chooses a structure by the door they press, and *Bring in another agent…* gathers the doors in one dialog. Fable rejected a chooser because each door decides; a dialog of the doors decides nothing new, and it is what makes "chosen by the person" visible. |
| 14 | The terminal | `sessions team`, `sessions ask <member>`, `sessions hand` | `team`, `exchange`, `follow-up`, `forward-answer` | `team`, the role verbs with `--after` | **Fable's** (the role verbs stay, a person types the role), with `--to-me` and `sessions pass <helper>` for row 10's choice (round 2: *pass*, not *hand*, which the glossary's *hand off* already reads, Fable F10). Codex's `exchange` is `team --json`'s member; Claude's member address `<entry>:<role>:<agent>` is row 1's rejected key. |
| 15 | Delivery identity | not raised | a stable key and a durable receipt; a restart mid-hand is *uncertain*, never sent twice | not raised | **A mechanism, HELPERWAIT1's, as built** (round 2; Codex 7, Fable F3 showed round 1's "taken as a proof" named none; round 3, Codex round-2 2). An attempt mark on the ask's own state, written before the door: before a hand the driver reads the asker's whole line for the word still waiting or a `user` event that took it, and marks `Handed` with it; an attempt with nothing found settles `hand-uncertain`, never handed twice (§5 point 6). Codex's *uncertain* is taken; its receipt is the mark. |
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
its part); Ask Daoris and an intake (they lead no team); a brainstorm's pass (no record); a teammate (a person on another
machine). *Corrected in round 2* (Codex 6): both helper admission judges refuse an intake (`Ask`), a helper (`Serves`) and a
reviewer (`Opinion`) (`Core/Sessions/HelperBox.cs:130-136`, `Driver/Conversations/Domain/HelperJudge.cs:63`), but neither refuses
Ask Daoris; row 4 adds that refusal at both, by the record's help repository, with a test at each.

**Each role's tools are the server's to bound** (round 2, Codex 6). `RoleFilter` holds a partner to an allow-list
(`Mcp/RoleFilter.cs:47-48`, `:68`) but refuses a helper only `quest_publish` and `quest_respond` (`:58`), so a helper can still call
an acting tool such as `permission_propose`, which takes the connector's session id without asking its role
(`Mcp/RuleProposalTools.cs:39`). Handed permission rules are not this boundary. Row 4 gives the helper and the reviewer
allow-lists of their own, as the partner has: the reading tools, the knowledge tools, and their own answer tools (`helper_answer`;
`opinion_give`), every other tool refused with the role's sentence, and `McpRoleFilterTests`' census gaining a row per role so a new
tool is placed on each list or refused by name.

### 2.2 The team

**A session's team is its line and every record whose `serves` names any part of that line.** The line is the entry's parts
(D167). Through a hand-over the team follows: the new part's *Co-workers* lists the earlier parts' members as history, each with
the part it served, and open pieces travel along `from` (D199 point 6). A co-worker's record never gains a `from` and is never
handed over: its one turn ends or is stopped.

**Delivery follows the line, not the display** (round 2, Codex 8). A helper's answer goes to the newest live record of the line
it served, along `from`, as a partner's already does (`Core/Sessions/PartnerDesk.cs:114-120`); today `HandAsync` resolves `serves`
alone (`HelperDesk.cs:140`), so an answer could reopen a part that was handed over, against D189 point 6. Where no part is
eligible, the answer stays on the box, shown to the person on the card, never handed to an ended part. The member's `serves` keeps
naming the part that asked (the commissioner), and the hand stays one per ask (§5 point 6). Round 3: HELPERWAIT1 built it, in the
door (`HelperDesk.cs:143-147`) and the driver (§5 point 7).

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
| **several reviewers** / 多位阅读者 (the owner's *panel*) | N reviewers of one candidate, each its own opinion, each a member of the team | nothing | a workflow's occasion; the person's ask at the gate | each a member in *Co-workers* and a card; the gate lists each opinion | D155 points 4 and 8; one gate; **amends D155 point 10's one pass per landing**. Named and kept; its gate contract is a design of its own before it builds (§3.5) |
| **mixed makers** / 多厂商 (more than two makers) | any structure above with members of several makers | as its structure | as its structure | the maker's mark on every member, never a hue alone | D155 point 4 (independence by maker); D197 (accounts); D199 point 9 (usage per maker, *not measured* never zero) |

**Two equals are two leads of two lines, never two leads of one tree.** The symmetric reading, two agents each writing pieces for
the other and each merging the other's frozen ref, is rejected: D51 and D199 point 2 forbid two writers in one tree, a partner
asks nobody, and two trees each taking the other's pieces are two diverging lines with nobody to settle them but a model (D24,
D54). Equals share the ask's channel and the person's one *Accept*.

**The gap alongside parts have:** two parts of one entry do not hear each other on the ask's channel. A message posted by a part
of an entry is not handed to that entry (D167's SESSONE2b note, *checked* at `docs/decisions/D167.md:218`). CHANPART1 (§12
row 8) hands it to every live part of the entry but its poster. Round 1 called that removing an exclusion; it is a new
acknowledgment contract (§3.5).

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
| a workflow | the opinion's occasions `landing` and `steps` (D155 point 2) | team, a reviewer; several reviewers once their design and row 9 are built (§3.5) | the repository's or workspace's rule |
| a workflow | a declared node that asks for a helper, a piece or several reviewers | team | row 10, after that design: a node under D157/D190, never a model's reading |

Ask Daoris proposes any of the person's doors as a `session` card and applies none itself (D110, D183).

### 3.4 Rejected for the structures

- **A `structure` field, or a structure chosen apart from its door**: the shapes are read from `serves`, `from` and the ask.
- **Symmetric partners, or two leads of one tree** (§3.2).
- **A coordinator agent** that dispatches the team and merges its results: D193 rejects agents directing agents.
- **Depth past one**: a helper asking for a helper, a partner asking for anything, a reviewer asking for a reader (D163 point 2,
  D199 point 2).
- **Members talking to each other.** They hear each other only through the lead's turns and the ask's channel; a member-to-member
  message is two agents conversing with no person between (D155, D163, D199 point 10).

### 3.5 Two structures named here and specified apart (round 2)

The owner's direction stands: *alongside* parts and *several reviewers* are named first-class, shown in §3.2 and chosen by their
doors. What round 1 did not do is specify either enough to build (Codex 13, 14; Fable F12). Each changes a foundation other
decisions rest on: one a protocol between the driver and the service, the other D155's gate. Each is therefore a design of its own
under D191, with Fable's foundation read, before its row builds; neither is designed in passing here. What each must answer:

**CHANPART1, alongside parts hearing each other.** Today one acknowledgment is keyed by ask, entry and message
(`Core/Asks/Channel.cs:320-325`), a part claims a message on its entry so that of two live parts only one takes it
(`:386-390`; `Driver/Loop/ChannelReach.cs:66`), and `IsFor` refuses a message posted by any part of the entry (`Channel.cs:1044-1052`).
Handing a message to every live part but its poster needs: an acknowledgment identity per part (ask, entry, message, part) with
its reads, claims, receipts and expiry; what an older reader keyed by entry does with per-part rows; D188's bounds kept **per
part** (ten posts, three urgent); the rule kept that an unsolicited post starts, reopens or extends no run; and amendments to D188's
COWORK1d note as well as D167's SESSONE2b note. Its row waits on that design.

**Several reviewers.** Today the gate reads the newest first pass and decides by it (`Driver/Review/Domain/OpinionGate.cs:384-390`),
and the desk refuses a second automatic first pass for a working session (`Core/Review/OpinionDesk.cs:395-402`). N reviewers need:
who declares the set (the rule's `opinions` or a workflow node) and its cap; whether each reviewer has its own recheck; how the
findings of N passes are delivered (one batched turn or one each), so that separate deliveries cannot step past D188 point 4's
hold on a third answer alone; how the gate settles over N opinions, with a `must` finding disputed in one opinion standing whatever
later opinions say (D155 point 8: agreement establishes nothing); and what the structure means where the install has only two
makers and the candidate is already of both (Fable F12: a second pass of the same family labelled *the same agent, fresh*, or a
plugin's maker). Its row, and the workflow node that would ask for it, wait on that design.

## 4. The records

### 4.1 The reviewer's record gains `serves`

`OpinionDesk.AskAsync` opens the reviewer with `serves: working.Id` beside `opinion`, through `session-record-fields.md`'s rows that
apply to a field set at the open, of which `serves` already has every one (the create takes it, the column and reader exist, the
response guards it on loopback, the driver reads it, the stand-ins echo it). What it buys:

- the server's filter refuses the reviewer `quest_publish` and `quest_respond` (`RoleFilter.ServingRefused`), and
  `McpRoleFilterTests`' census gains its row;
- the team is one read on the page and in the terminal; the rail's nesting stops needing the opinions join
  (`SessionList.tsx:92-96`, its `serving` prop) for a record made after the change, and keeps it for one made before;
- every desk that refuses a serving asker refuses it, as each already does by `opinion` (round 2, Fable F13: among them
  `DeliverHelperAsync`'s `AskerUnfit` arm, `DriverHelpers.cs:396`, and `HandOverAsks.cs:118`);
- the server's allow-list for the reviewer (§2.1) has a record to bind. `serves` alone is not the boundary.

A recheck's reviewer and *the same agent, fresh* serve the same working session, so the team counts each pass as its own member.

What moves with it: every reader that takes `serves` to mean *helper*. On the page `isHelper` (`Web/work/identity.ts:79`) becomes
`roleOf(session)` (`'reviewer' | 'partner' | 'helper' | null`, in §2.1's order), and its readers move: `sessionTitle`, the rail's
`helps` line (`SessionRow.tsx:198-203`), the head's *Helping* fact (`SessionHead.tsx:493`, `headFacts.ts:88`), `sessionLife`'s
`serves` word and `acts.ts`'s wordless set. In the driver, every reader that tests `Serves` for a helper reads `Opinion` first.
D155 point 6 stays true: `serves` is a link, and the reviewer is handed no conversation.

### 4.2 The page's event gains `helper`

`SessionEvent` (`Web/work/conversation/conversation.ts:45`) gains `helper?: string | null`, which the driver already writes on the
answer's event: for a conversation asker at `DriverHelpers.cs:439-445`, and for a driven asker at the go-on's opening (COWORK1e,
D188's note) (round 2, Fable F7: both writers). The card uses it to place the answer's taking; the body and the delivery come from
the exchange read (§6.3). This is MIXAGENT3a-iii's block half. **The conversation writer's event is a notice, not a taking** (round 3,
Codex round-2 7): it carries `reaches: resume` to tell the chat runner that words wait (`DriverHelpers.cs:689-694`), and the turn that
takes them writes its own event with `reaches` absent. Only the latter is a taking, as `HandedBefore` reads it (§5 point 6).

### 4.3 The helper's ask gains `after` and `to`

Two optional fields on the helper's ask box and its twin, both written only by the person's doors, an absent one read as today:

- `after`: the helper record this ask follows, which threads it (§2.3);
- `to`: `person` where the answer goes to the person and not to the lead (§7). Absent is `lead`, today's one path.

`HelperFiles` reads the box with `JsonDocument` (`Driver/Conversations/Helpers/HelperFiles.cs:205`), so a driver from before `to`
ignores it and hands to the lead (Fable F11). Builds of one install move together, so a newer page with an older driver is
unsupported, as Fable F9 reads the held lead under an older driver; it is said here rather than guarded.

**A partner's follow-up is not threaded by `after`** (round 2, Codex 12). It is a new piece from the lead's current base (§7), and
its link to the piece it follows belongs to the piece's own shape, COAGENT1b's. Row 6 says how, or draws each piece as its own row;
nothing here adds `after` to a piece.

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
  ask|show|stop`, `sessions hand-over`. `sessions pass <helper>` passes a person-only answer to the lead (§7; round 2's word, Fable
  F10, since `hand` sits beside `hand-over`, `hand_propose` and the glossary's *hand off*).

## 5. The lead while its co-workers work: the mechanism this design rests on, as HELPERWAIT1 built it

**The rule: a lead that ended its turn on its own open ask waits on that co-worker, by name, and on the person only where it also
asked the person, or the co-worker ends with no answer.**

Round 1 designed this mechanism here, and both reads found it unbuildable as written (Codex 1-5, 7, 8; Fable F1-F4). Round 2 stated it
as a contract for HELPERWAIT1, then unfinished; Codex's round-2 read answered that a contract assigned to an unfinished row resolves
nothing (`docs/2026-10-23-coworker-reads.md`, *Codex, round 2*, verdict). **Round 3 states each point as built**: HELPERWAIT1 landed
(archived 2026-10-23; FIX-LOG's HELPERWAIT1 entry; D188's and D46's HELPERWAIT1 notes), and this branch reads it at `2323abccc`. Each
point names its code and its tests by file and line, and where the built choice differs from what Codex's round 2 asked, says why it
holds or names the row that closes the gap (§12 row 11). **Round 4** corrects four points Codex's round-3 read found the built code
did not keep (points 1, 3, 6 and 7, each marked *round 4*), as HELPERWAIT1b fixed them at this branch; line numbers in those
corrections are this branch's after the fix. Paths: `Driver/` and `Core/` as above; `Tests/` is
`Daoris.Desktop/Daoris.Desktop.Driver.Tests/`, `Core.Tests/` is `Daoris.Service/Daoris.Service.Tests/`, `Http.Tests/` is
`Daoris.Service/Daoris.Service.Http.Tests/`. *Process* marks a real-process class.

1. **The reading** (Codex 1, 2, round-2 3; Fable F4). A driven run that ends cleanly holding its quest, where it would park, concludes
   `completed` with the coded note `ended.awaits-helper` (`Driver/Sessions/Domain/Observation.cs:154-155`), when the driver reads
   beside the conclusion that its agent has an ask of its own still open and its last turn asked the person nothing
   (`Driver/Loop/Driver.cs:2382-2384`, and a carry-on's run at `Driver/Loop/DriverContinuations.cs:525-528`).
   - *Open* is every ask the agent made (`AskedBy` agent) that is neither refused nor handed, one with no state file included
     (`Driver/Conversations/Domain/HelperWaits.cs:20-22`). An ask the person made is no wait of the session's.
   - *Asked the person* is the session's PARKVIEW1 question asked since the later of the run's opening and the words it was last
     handed (`Driver/Loop/Driver.cs:461-463`), or an unanswered go-ahead on its work's ask that names it (`HelperWaits.cs:47-49`).
   - **Unread is told from absent** (Codex round-2 3). `QuestionFiles.AskedSince` answers `false` only where no question file exists,
     and `null` where one exists and does not read (`Driver/Sessions/Questions/QuestionFiles.cs:68-72`); the reading waits on the
     helper only on `false` (`Driver/Loop/DriverHelpers.cs:169-170`). A go-ahead read the service does not answer is `null` too
     (`DriverHelpers.cs:179-194`), and only `false` waits (`:171`). Either unread, the turn parks, and no park is converted (point 5),
     so there is no conversion guard to give the same read.
   - **The park keeps the unread file** (round 4; Codex round-3 1). Round 3 said the question was kept, but the settle kept only a
     question that read and was fresh, and deleted the rest, at both conclusions (`Driver/Loop/Driver.cs:2452`,
     `DriverContinuations.cs:583`). `QuestionFiles.Settle` now keeps a file that does not read where the conclusion parks
     (`Driver/Sessions/Questions/QuestionFiles.cs:115-120`): neither its question nor its time can be read, so nothing proves it was not
     this turn's. A read of it is still none, so the park shows no half a file, and the next run's opening clears it. A conclusion that
     does not park keeps nothing, as before; a refused resume parks nothing (`DriverContinuations.cs:502`).
   - Its last words are never read for intent (Codex X4).
   - *Tests:* `Tests/Conversations/HelperWaitsTests.cs:26` (open), `:54` (the go-ahead); `Tests/Sessions/ObservationTests.cs:401`;
     `Tests/Loop/HelperLookTests.cs:1046` (own ask only, a question since, and a question file that does not read parks);
     `Tests/Sessions/QuestionFilesTests.cs:221` (round 4: the unread file read as asked, kept by a parked first run's settle and a
     resumed run's, and gone where the conclusion does not park). **Not proved:** the go-ahead read the service does not answer; its
     code parks, and its case is row 11's.
2. **A partner only where the lead yielded for it: deferred** (Codex round-2 6). `ended.awaits-partner` is not built. HELPERWAIT1
   built the helper's wait alone, since a piece is asked today by the person of a working lead and the lead's own `partner_ask` is
   COAGENT1d's, not yet built (D188's HELPERWAIT1 note, *The partner*). The service's half recognizes the helper's code by its spelling
   and nothing else (`Core/Sessions/SessionLedger.cs:1492-1496`, read by the tree's holder at `:1504-1514` and the move back at
   `:1529-1560`), so a partner wait needs the service, not only the web and the driver. It is §12 row 12, deferred until
   `partner_ask` exists, with its own service, driver, modules and web scope and the proofs this section's points 3-7 have for the
   helper, a partner's way (§11).
3. **It is held, and it keeps its tree** (Codex 5). The planner carries a plain `completed` record on from nothing
   (`Planner.CarriesOn`), so the waiting lead gets no carry-on and no strike, takes no running slot, and reads as *Resumes later*,
   awaiting helper (`Driver/Trees/SessionGroups.cs:1148`). It holds its tree as a park does, read by one predicate in each place
   (`HelperWaits.HoldsTree`, `HelperWaits.cs:36-38`, and its service twin):
   - the service's tree exclusion: `SessionLedger.HolderAsync` (`Core/Sessions/SessionLedger.cs:1504-1514`), an active record, else
     a record waiting on its own helper whose quest is still taken, never against itself, read at the start, the take and the move
     (`:609`, `:1306`, `:1462`);
   - the run's lock, kept at its end (`Driver/Loop/Driver.cs:2457-2459`, `:2475`; `DriverContinuations.cs:600`);
   - the planner's further part and go-back-in (`Driver/Quests/Domain/Planner.cs:1605`, `:1842`, `Planner.WaitingInTree` at `:2051`);
   - the tidy, the lock's let-go and Ask Daoris's housekeeping (`Driver/Trees/Branches/BranchTidying.cs:103`, `:147`, `:163-171`;
     `Driver/Help/HelpHousekeeping.cs:193`).
   - **Round 4** (Codex round-3 3): round 3's tidy read the waiting lead as holding only while an ask of its own was open, unlike the
     service's holder. Once its last ask was refused and its move back to the person failed, the lead stayed `completed` and waiting
     with no ask open, so the tidy could let go of its lock and take its tree while the ledger still read it as held.
     `BranchTidying.WaitingOnHelpersAsync` now reads the service's predicate (`BranchTidying.cs:163-171`): waiting on its own helper
     while its quest is taken, never an ask being open. It keeps the tree until the move lands (the lead is then active, a park), it
     goes on (its words wait), or its quest is no longer taken. The lock's let-go (`:103`) and the housekeeping (`HelpHousekeeping.cs:193`)
     read the same set, as the planner's `WaitingInTree` already did.

   *Tests:* `Core.Tests/Sessions/SessionLedgerTests.cs:1162` (a second writer refused while it waits); `Tests/Quests/EntryPartsPlanTests.cs:117`
   (the planner); `Tests/Trees/BranchTidyingTests.cs:694` (the tidy, round 4: held with its ask open, still held once it is refused and
   the move back failed, held with no ask on file at all, and let go once its quest is done); `Tests/Loop/HelperWaitTickTests.cs:103`
   (*Process*, the cap of one, round 4: the driver runs at `Cap = 1` (`:68`), and the helper's pass starts with its asker already
   `completed`, in the one slot the asker's run freed (`:120`); round 3 cited it at the default cap of two, Codex round-3 5).
4. **No answer brings it to the person; a failed move is tried again at every look** (Codex 4, round-2 5; Fable F1, its choice (a)).
   Where none of the lead's own asks is open and the look reads it waiting (`HelperWaits.Waits`, `HelperWaits.cs:28`), the driver moves
   it `completed → awaiting-person`, the reason first (the newest refusal's code and agent, else *no answer*, `HelperWaits.cs:55-65`),
   then the park's own note (`Observation.HelperGone`, `Observation.cs:238`), only while its quest is taken
   (`DriverHelpers.cs:305-341`). The service's move is new and guarded (`SessionLedger.cs:1393`, `:1529-1560`): refused for another
   machine's record, an intake, a pass, a helper, a record with no quest, words waiting (it goes on with them), a quest no longer
   taken, and a tree another holds; `clearSaid` false.
   - **A failed move is tried again at every look, and said once** (`DriverHelpers.cs:323-337`), where Codex round-2 5 asked to tell
     a transient failure from a permanent refusal and retry only the first. **Why it holds:** nothing else moves the record, so any
     wait for it to move holds the lead for good, which was Codex's own finding. Every refusal the ledger gives either clears by
     itself, or never reaches the move. A tree another holds clears when its holder ends. Words waiting go on instead. A quest
     closed meanwhile is filtered before the move (`DriverHelpers.cs:310`). The record kinds the ledger refuses are not read as
     waiting (`HelperWaits.cs:36-38`). An older service is not met, since the local host ships with the driver in one install
     (D46's note). Retrying costs one service call a look for a record in this state alone. The transient half is said as a retry;
     a refusal is said to the person.
   - **The gap that remains** is Codex's second half, *an actionable existing wait*. The refusal is said as a `Wait` look line
     (`DriverHelpers.cs:336`). That class promises a row of *What needs you* (`Driver/Platform/LookSaid.cs:12`). But the record stays
     `completed`, and no row says it. It is filed as §12 row 11 rather than answered here. **Round 4** (Codex round-3 6): row 11,
     renamed HELPERWAIT1c since HELPERWAIT1b is the round-3 fixes, is a prerequisite of row 1's installed proof and landing (§12),
     so the first showing never lands with a refused return the person cannot see. Until it lands, the lead keeps its tree (point 3).
   - **A restart during the helper's turn** (Fable F9; Codex round-2 8): the next look settles the started ask from what the helper
     kept. A `helper_answer` saved before the cut is delivered, whatever the helper's ending (`DriverHelpers.cs:556-559`). With no
     saved answer, the ask is refused `no-answer` (`:564-567`), and the lead comes to the person. Round 2's sentence that a restart
     always brings the lead to the person is withdrawn: it does so **without a saved answer**.
   - *Tests:* `Core.Tests/Sessions/SessionLedgerTests.cs:1107`, `:1130` (the move and its guards); `Tests/Sessions/ObservationTests.cs:438`;
     `Tests/Conversations/HelperWaitsTests.cs:41`, `:74`; `Tests/Loop/HelperLookTests.cs:748` (back to the person, or still waiting on
     a second ask), `:795` (unreachable and refused, each tried again with nothing moved, said once, then moved once the service
     takes it), `:615` (the saved-answer and no-answer restarts).
5. **A lead parked on the person keeps its park, and takes its helper's answer beside the person's** (Codex 3, round-2 1 and 4;
   Fable F2). An unanswered park still refuses the hand: `HandAsync` refuses `AwaitingPerson` with no answer
   (`Core/Sessions/HelperDesk.cs:156-157`), and the driver holds it, saying it once (`DriverHelpers.cs:634`, `Held`).
   - **Beside the person's answer** (Codex round-2 1): a park the person answered takes it. The service's door lets an answered park
     through (`HelperDesk.cs:158-160`). The driver's guard holds only `AnsweredPark: false` (`DriverHelpers.cs:634`;
     `Planner.cs:296`), and the go-on's opening reads the words on the record after its move, the helper's word among them
     (`DriverContinuations.cs:265-268`), so both reach the lead in one prompt. Both guards Codex named changed, in the driver and the
     service, as its smallest fix asked.
   - **Parks from before the rule stay parked** (Codex round-2 4). Round 2's conversion of such a park (`HelperWaits.ParkedOn`) was
     not built: nothing on a park proves it asked the person nothing, since its question may stand in its closing words alone. The
     helper's word alone never moves a park. A park's helper answer waits, said once, and is handed beside the person's answer when
     they give it.
   - *Tests:* `Http.Tests/HelperHostTests.cs:138` (the door: refused unanswered, taken answered);
     `Tests/Loop/HelperLookTests.cs:679` (Codex's case: a park whose question stands only in its closing words, held and said once,
     never moved, then handed beside the answer), `:716` (a question on file, a park from before the ask, a park for another reason,
     the person's own ask: each held); `Tests/Loop/HelperWaitTickTests.cs:144` (*Process*: both answers in one prompt).
6. **The answer reopens it once, by an attempt mark** (Codex 7, round-2 2 and 7; Fable F3, F8). A waiting lead is `completed`, so
   `HandAsync` keeps the word with `Reopens: true`, and the next look goes on with it: one go-on under D188 point 4.
   - **Once per ask** (Codex round-2 2): before the door, the driver writes an attempt mark, `attempted`, on the ask's own state file
     (`Driver/Conversations/Domain/HelperAsks.cs:143`; `Driver/Conversations/Helpers/HelperFiles.cs:59`, `:81`;
     `DriverHelpers.cs:657-660`), clears it where the door refused (`:664-668`), and writes `Handed` with it cleared once the door kept
     the word (`:680`). Before any hand, it reads the asker's **whole line**, the commissioner and each successor, for the word still
     waiting or a `user` event that took it (`HandedBefore`, `DriverHelpers.cs:260-274`). Either found, it marks `Handed` and hands
     nothing (`:641-645`). An attempt mark with nothing found is never handed again: the ask settles `hand-uncertain`
     (`:648-652`; `HelperAsks.cs:61`, `:103`). The helper's conversation keeps the answer, and a lead waiting on it comes to the
     person with that reason (point 4).
   - **Why a mark and not a receipt store** (Codex round-2 2 offered either): the mark *is* the receipt Codex's first fix describes.
     It is keyed once per commission (the ask's id, one state file per ask). It is kept on the helper's side, so neither the word's
     consumption nor a relay of the lead's line touches it. An interrupted attempt is held as uncertain, never retried. A second store
     would hold the same fact in two places that could disagree. The cost is one case: a crash after the mark but before the request
     left settles `hand-uncertain` though nothing was handed. The answer is not lost there (the helper's conversation and the card
     hold it), and the person is told. The safe side is chosen: never twice, sometimes the person.
   - **A notice is not a taking** (Codex round-2 7): a conversation's `user` event with `reaches: resume` tells its runner that words
     wait (`DriverHelpers.cs:689-694`). `HandedBefore` counts only an event with `Reaches` absent (`:266`), and §6.3's card reads
     taking the same way.
   - **Only a refusal clears the mark; a lost answer is uncertain** (round 4; Codex round-3 2). Round 3 read every unsuccessful answer
     as a refusal and cleared the mark, but the desk keeps the word before its answer is made (`Core/Sessions/HelperDesk.cs:189`),
     and the host can still answer 500 after it. A committed hand could then lose its mark, and a word consumed with its event unwritten
     would leave nothing to stop a second hand. `HelperDoors.HandAsync` now tells three outcomes (`HelperHanded`,
     `Driver/Conversations/Helpers/HelperDoors.cs:72-131`). **Refused** is only the desk's own refusal, which keeps nothing: its
     sentence on a 400, 404 or 409 (`HelperReply`), or an older host's bare 404. **Kept** is a success that reads. **Uncertain** is
     everything else: another status, a success that does not read, no answer at all (a timeout, a broken connection). The driver
     clears the mark on a refusal alone (`DriverHelpers.cs:674-678`). On an uncertain outcome it keeps the mark and says a retry line
     (`:683-688`). The next look reads the line back: the word found there marks the ask handed (`HandedBefore`), and nothing found
     settles it `hand-uncertain` for the person. It is never handed twice. The mark stays on the ask's state file, which Codex's read
     kept as sound.
   - *Tests:* `Tests/Loop/HelperLookTests.cs:890` (found waiting, found taken), `:923` (an attempt with nothing, and with only the
     notice, settles uncertain and hands nothing), `:949` (taken by the commissioner, then relayed: found on the line, not handed to
     the successor), `:858` (a refused hand clears its mark, is said once, and is tried again once the asker moves), `:976` (round 4:
     the word kept and its answer lost, as a 500, an unreadable success and a timeout; the mark kept, said uncertain, then the word
     found waiting marks it handed, and the word consumed with no event settles it uncertain; one hand in every case).
7. **It goes to the line's live part** (Codex 8). The service's door follows `from` from the commissioner to the newest record of
   its line, at most sixteen links (`Core/Sessions/HelperDesk.cs:143-155`), and the driver resolves the same line
   (`DriverHelpers.cs:603`, `LineAsync` at `:726-752`). A line whose newest record cannot take it refuses with the reason, and the
   answer stays in the box for the person. **Round 4** (Codex round-3 4): round 3 stopped at sixteen links and took the record
   where it stopped, which the line may have left. Both sides now follow one link further, and a line that goes on past the bound is
   refused, never handed to a part it left. The door refuses it `CannotTake`, keeping nothing (`HelperDesk.cs:146-152`). The driver
   settles the ask refused `cannot-take`, saying the line runs past sixteen links (`DriverHelpers.cs:605-612`, `:743`). The helper's
   conversation keeps the answer for the person. Refused, not resolved to the newest part: a line of seventeen relays is no case a
   person makes, and reading on unbounded is what the bound exists to stop. *Tests:* `Http.Tests/HelperHostTests.cs:114`, `:141`
   (round 4: sixteen links reach the newest part; seventeen are refused, nothing kept on any part);
   `Tests/Loop/HelperLookTests.cs:949`, `:1012` (round 4: the same two lines in the driver, the longer one never handed).
8. **Where it shows.** *Resumes later*, its line *waits for {{agent}}'s answer* (等 {{agent}} 答复), the *Co-workers* section
   showing the member working; *What needs you* lists nothing for it until point 4 parks it.
9. **A conversation lead** stays `working` between turns and takes the answer as its next turn (MIXAGENT2a-iii): no change.

## 6. The page

### 6.1 The list

- **Nesting stays** as MIXAGENT3a-ii built it (moved, never copied; one level; a row the person must act on keeps its place),
  before *Ended* is truncated, as it already is (§0.2's second point; regression cases kept for that order).
- **A lead with a live member is never cut** (round 2, Fable F6): an ended lead with a working, waiting or failed member stays
  above *Show N more*, as `keep` keeps the attended one, so a member that needs the person is never folded away under its lead.
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

- **Read from one exchange read** (round 2, Codex 10). Today the head's helper read filters asks by one asker id and returns
  neither the answer's text nor its delivery (`Daoris.Desktop.Modules/Bridge/Routes/SessionsRoutes.cs:190-200`), and the
  `helper.asked` note carries the helper's session id, not the ask's, and is written only once a helper starts
  (`Driver/Conversations/Domain/HelperNotes.cs:10`). Row 1 extends that read into the **line's exchanges**: every ask whose asker is
  any part of the line (the entry's parts, §2.2), each with its asker, ask id and `at`, who asked (agent or person), the agent, the
  helper's session once started, the run's state, the answer's source (answer file or last words) and its text (bounded as last
  words are), and its delivery (the handed word's id, and the `user` event that took it where one did). A member's thread resolves
  the same read through its `serves` to the line it served.
- **Placed** at the `helper_ask` call where the conversation has one, else at the ask box's `at` (a person's ask has no event on
  the lead's line); updated in place; the answer's event with `helper` (§4.2) gives the moment the lead took it.
- **Its head**: *Codex, as helper* (Codex，作为助手) with the mark, then three facts kept apart: the run (*reading your work as of
  16:29 · running `rg` · 4 min*, *answered*, *refused: out of time*), the delivery (*waits for the turn's end*, *taken as the next
  turn*, *to you*), and for a piece its taking-in. *Taken as the next turn* is said only from a `user` event naming the helper with
  `reaches` absent; an event with `reaches: resume` alone says *waits for the turn* (round 3, Codex round-2 7; §4.2).
- **Its body**: the question as written, then the answer as written from the box (the head's helper read gains the answer's text,
  bounded as last words are, *Show all* past it), under *a claim to check, not your words*. A last-words answer says so.
- **Its doors**: *Open conversation* (打开对话); for a person-only answer (row 5), *Pass to the lead* (转给主导; round 2's words,
  Fable F10: *Hand to the lead* / 交给主导 read as the glossary's *hand off*, the plugin's word).
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
  *waits until you answer {lead}*, *to you*), from the exchange read's delivery facts;
- **row 5 adds**, not row 3 (round 2, Codex 12): *Ask again…* at its end, and its follow-ups as the thread's strip, in the parts
  strip's shape. Row 3 builds the navigation, the thread head, the confinement line and the delivery facts that exist today.

### 6.5 What needs you

No new kind. The helper's yes stays the `helper` wait, worded with the lead and the member (*Claude Code asks Codex to answer*);
its press opens the lead's card. The partner's yes is COAGENT1d's; disputes stay the opinion's; an uncertain piece stays D199's.
§5 point 4 adds none: it is the park row the record would have had.

## 7. Talking to a member

**Talking is asking again**: a new one-turn record of the same agent in the same role, on a fresh copy of the lead's work, its
`after` naming the one it follows. Nothing is resumed and no second prompt enters a finished record (resume is unmeasured for Codex,
SESSCTX1a; a steer is not built, STEER3; a reader that remembers answers about work that moved).

| Role | While it works | Once it answered | Where the answer goes |
|---|---|---|---|
| helper | *Stop…* | *Ask again…*: the earlier question and answer quoted as Daoris's record (bounded), the person's words under it | where the person asked from (Fable F11): from the member's thread the person (`to: person`), reaching the lead only by *Pass to the lead*; from the lead's page the lead; *for the lead* / `--to-me` turns either |
| partner | *Stop…*, *Discard…* | *Ask again…*: a new piece from the lead's current base, the earlier answer quoted | the lead's line as a frozen ref, always (D199 point 3) |
| reviewer | *Stop…* | D155's recheck, once; past its budget, refused with a helper's ask offered beside it, never converted silently | the first pass's findings to the lead (D155 point 7); **the recheck's to the person and the opinion gate**, never back to the lead by themselves (round 2, Codex 9; D155 point 7, `D155.md:52`) |

What holds it (round 2 corrects round 1's count, Codex 11, Fable F5):

- **Today a person-asked helper's answer counts as an answer alone.** `AnswersAlone.Resets` reads a person's `user` event, Daoris's
  word, or an `answers.passed` note (`Driver/Conversations/Domain/AnswersAlone.cs:89-91`); a person's helper ask writes a box file and
  no event on the lead, and the answer's event carries `helper`, which `Answers` counts (`:94-95`). Round 1's "the person's ask is
  their word" was not the code's.
- **A person-only answer counts nowhere**, since it never reaches the lead by itself.
- **The press is the person's word.** *Pass to the lead* (and `sessions pass`) keeps the helper's word on the lead, its `helper` mark
  unchanged, so the claim stays the helper's (D137), and beside it the `answers.passed` note (`AnswersAloneNotes.Passed`, `:110-112`),
  which `Resets` already reads as the person's word. Nothing is relabelled and no taken prompt is made.
- **The person's ask for the lead resets the count.** Both person doors (`Driver/Conversations/Helpers/SessionsHelperCommand.cs:91`,
  `Daoris.Desktop.Modules/Bridge/Routes/SessionsRoutes.cs:175`), when the answer goes to the lead, keep a note of a new code,
  `helper.person-asked`, on the lead's line at the ask; `Resets` learns that code. The answer that follows is then the first alone,
  not the third. The lead's own `helper_ask` counts as today.
- **A follow-up the lead asked** returns to it as D163 point 2 has it.
- **Two helpers at once per asker** counts the follow-up; the minutes and the copy are its own.
- **No agent and no message level stops a member** (D188 point 1); the person's stop and Daoris's declared bounds on a pass still do
  (D188, D200 point 7) (round 2, Codex 17).

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

New: `work.team.*` (the section, the rows, the thread head, *Back to lead* 返回主导, *Open conversation* 打开对话, *Pass to the lead*
转给主导 as a `button` (20/8), *Ask again…* 再问…, *Bring in another agent…* 请另一个智能体…), `work.conversation.helper`, and the note
`helper.person-asked` (§7); `ended.awaits-partner` is row 12's (§5 point 2). `ended.awaits-helper` and the unanswered line are HELPERWAIT1's (its
`note.ended.awaits-helper`, and the refusal's own line before the park's, §5 point 4), not new here.

**Destination-aware helper words** (round 2, Codex 16). `work.helper.says`, `work.helper.asked` and `work.helper.state.answered`
(`Web/locales/en/work.helper.json:3`, `:8`, `:21`, and their zh twins), drawn by `Web/work/waits/HelperAsks.tsx:95`, each promise the
answer is the session's next turn. For a `to: person` ask (row 5) each gains a twin that says the answer comes to the person and
reaches the lead only by *Pass to the lead*, in both catalogues; the helper tool's fixed response is checked for the same promise. State words reuse `work.helper.state.*`, whose sentences are
content-length lines, not statuses; the role words on a row are statuses (*working* 工作中, *answered* 已答复, *given* 已给出, *refused*
已拒绝, *stopped* 已停止, *out of time* 超时, *taken in* 已并入). Each new label is checked against its kind's budget in its row.

## 9. Both doors, and Ask Daoris

- **The terminal**: §4.5. Every refusal is the role door's, in the same words.
- **Ask Daoris** may propose a `session` card for each of the person's doors (*ask Codex as a helper about …*, *ask again after …*,
  *pass Codex's answer to the lead*), with the terminal's equivalent beside it; the person applies. It never asks a co-worker itself and
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
| 1 | the exchange read's taking (round 3, Codex round-2 7): an answer with only the `reaches: resume` notice reads *waits for the turn*, one with the turn's own event *taken as the next turn*; `identity.test.ts` (roles from the fields, in §2.1's order); `groups.test.ts` (three roles nested; the order nest-then-cut kept as a regression case; an ended lead with a live member never cut; the fold; a working member never folded); `CoWorkers.test.tsx` (the lead's row, a member per state, a pending ask with its presses and `data-wait-section`, the mark, nothing for a session alone); `ConversationView.helper.test.tsx` (placed at the call, else at the box's `at`; updated by the answer's event; body from the exchange read; last words said; never *The target Daoris composed*); the bridge's exchange read (every part's asks on the line, answer text bounded, the handed word and its taking; a member resolving it through `serves`); `SessionRow.coworker.test.tsx`; stories per state at 888 px, both themes; `SessionsCommandTests` (`team`, the JSON fields); `glossary.test.ts`, `names:check -- --strict`, `i18n:check` |
| 2 (HELPERWAIT1, built) | Built and run with HELPERWAIT1 (FIX-LOG's entry); each case is cited under its point in §5: `HelperWaitsTests`, `ObservationTests`, `HelperLookTests`, `EntryPartsPlanTests`, `BranchTidyingTests`, `SessionLedgerTests`, `HelperHostTests`, and the *Process* class `HelperWaitTickTests`. Round 4's four fixes (HELPERWAIT1b) add `QuestionFilesTests` and their cases in `HelperLookTests`, `BranchTidyingTests` and `HelperHostTests`, and run `HelperWaitTickTests` at a cap of one. The one case of §5 not proved, a go-ahead read the service does not answer, is row 11's |
| 3 | `WorkFrame.thread.test.tsx` (into a member and back to its card; the detached window's head; the confinement line; the delivery facts as today's records hold them); `sessionLife` (the sentence) |
| 4 | `SessionStoreTests` (a reviewer's `serves` written and read; null before and on a mirror); `OpinionHostTests` (loopback only); `McpRoleFilterTests` (the helper's and the reviewer's allow-lists; every tool of the server placed on each or refused by name, the census row per role; `permission_propose` refused to both); the helper box's judge (`HelperBox`'s tests) and `HelperJudgeTests` (Ask Daoris refused at both admission judges); the three stand-ins; the page's nesting without the join and with it for an older record |
| 5 | `HelperFilesTests`, `HelperBoxShapeTests` (the twin shape with `after` and `to`); `SessionsHelperCommandTests` (`--after`, `--to-me`, `pass`); `HelperDeskTests` (a person-only answer kept on the member, passed once by the press); `AnswersAloneTests` (`helper.person-asked` resets, from both doors; the press's `answers.passed` resets; a person-only answer counts nowhere); the destination-aware strings in both catalogues; `WorkFrame.thread.test.tsx` (*Ask again…* and the follow-up strip); `HelpCoverageTests`; a Process case: a follow-up on a fresh copy, answered to the person, then passed |
| 6 | the Pieces tests moved under *Co-workers*; no wait of the lead's, which is row 12's |
| 7 | the dialog's choices each judged by its door, a choice turned off with its reason, `HelpCoverageTests` |
| 8, 9, 10 | with their designs (§3.5) |
| 11 (before row 1's install proof) | `HelperLookTests`: a refused move back to the person puts the lead in *What needs you* with the refusal, and an unreachable service does not; `HelperLookTests` (or `HelperWaitsTests`): a go-ahead read the service does not answer parks the turn; *What needs you*'s row for it, en and zh |
| 12 | §5's proofs, a partner's way, written with its design: `ObservationTests` (`awaits-partner` only where the lead asked the piece in that turn, never a piece the person asked); `SessionLedgerTests` (the service's tree exclusion and the move back read the partner's code; a second writer refused, cap one); `BranchTidyingTests` and `EntryPartsPlanTests` (the tree kept); the no-answer move back, a failed one tried again; the piece taken once across a restart, and a piece saved before a restart delivered; an answered park takes the piece beside the person's answer; a *Process* case with a stand-in partner |

**The install's proofs**, one per landing row (round 2, Codex 12), by the parent after the republish, with the owner's yes where an
account is spent, shots at 888 px in both themes and languages:

- **after rows 1, 2 and 11** (row 11 a prerequisite since round 4, Codex round-3 6): GITAPP1's case again, a Claude Code session
  asking Codex as a helper and ending its turn. The window shows
  the lead *waiting for Codex's answer* in *Resumes later*, Codex under it in *Co-workers* and the rail as *helper · working*, the card
  filling in, the lead going on with the answer; `sessions team` beside them;
- **after row 3**: from the card into Codex's thread and back to the card;
- **after row 5**: *Ask again…* from the thread, answered to the person, then *Pass to the lead*, and the lead going on with it.

The budgets of the rows and the thread head are the window's to judge (D116 §3).

## 12. The rows, in build order

Each row is one feature a person can see working (D187). Filed in TASKS.md when D203 is accepted; until then COWORKUX1 points here.

1. **COWORKUX1a — today's helper as a co-worker in the session's team** (web, modules, driver's terminal; no stored shape changes).
   The terms (§8.1), `subagent` narrowed; `SessionEvent.helper`; *Co-workers* replacing *Helpers*, the pending asks' presses kept; the
   line's exchange read and the card drawn from it; the nested row's role, the lead's count, a lead with a live member never cut, the
   fold; `sessions --json`'s fields and `sessions team`. Closes MIXAGENT3a-iii's block half. **The first showing.** It lands, and its
   install proof is taken, only after row 11 (round 4, Codex round-3 6): the first showing puts a waiting lead before the person, so a
   refused return to them must already be a row they can see.
2. **HELPERWAIT1 — a session waiting on its own helper is not shown as waiting on the person** (driver, service, modules, web;
   **built**, archived 2026-10-23). The mechanism §5 states as built, with the service's guarded move. Row 1's install proof waits for
   HELPERWAIT1 to reach the install beside it, since every live state on the page reads wrong until the lead's own state is right.
   HELPERWAIT1b (round 4, built here) fixed the four guarantees Codex's round-3 read found the built code did not keep (§5 points 1, 3,
   6 and 7; FIX-LOG's HELPERWAIT1b entry).
3. **COWORKUX1b — the member's conversation as a thread** (web). §6.4's navigation, thread head, confinement and the delivery facts
   that exist today; not *Ask again* or the follow-up strip, which are row 5's (round 2, Codex 12).
4. **COWORKUX1c — the reviewer joins the team, and each role's tools are the server's to bound** (service, driver, modules, web).
   §4.1, §2.1: the reviewer's `serves`, the helper's and the reviewer's allow-lists with their census, Ask Daoris refused at both
   helper admission judges; amends D155 point 11's record.
5. **COWORKUX1d — talking to a helper: *Ask again* and *Pass to the lead*** (service box twins, driver, modules, web, Ask Daoris
   coverage). §4.3, §6.4's follow-ups, §7, §8.3's destination-aware words, `helper.person-asked`; amends D163 point 2 for the
   person's follow-ups.
6. **COWORKUX1e — the partner as a member** (web, driver's read; after COAGENT1b). D199 §6.1's Pieces drawn as *Co-workers* rows and
   cards; how a follow-up piece links to the one it follows, or each piece its own row (§4.3). Presentation only (round 3, Codex
   round-2 6): the lead's wait on its partner is row 12.
7. **COWORKUX1f — *Bring in another agent…*: the structures chosen by the person in one place** (web, Ask Daoris). §6.2's dialog.

**Deferred behind a design of their own** (round 2, Codex 13, 14; §3.5). Each changes a foundation, so each is written and read under
D191 before it is filed; the structure stays named and shown in §3.2 meanwhile:

8. **CHANPART1 — parts alongside in one entry hear each other on the ask's channel** (driver, service). Waits on the per-part
   acknowledgment design; amends D188's COWORK1d note and D167's SESSONE2b note.
9. **COWORKUX1g — several reviewers of one candidate** (service, driver, web; after row 4). Waits on the several-reviewers gate design;
   amends D155 point 10's cap.
10. **COWORKUX1h — a workflow node that brings in a co-worker** (after COAGENT1d and row 9). A declared node under D157/D190 asking for
    a helper, a piece or several reviewers; its several-reviewers half waits with row 9, and its helper and piece halves may be filed
    alone once COAGENT1d lands.

**Round 3's two rows** (Codex round-2 5 and 6):

11. **HELPERWAIT1c — a move back to the person the service refuses shows in *What needs you*** (driver, modules, web; filed in
    TASKS.md now, since it closes a gap in built code and waits on no acceptance; named HELPERWAIT1b until round 4, when that name
    went to the round-3 fixes built under it). **A prerequisite of row 1's landing and its install proof** (round 4, Codex
    round-3 6), built before row 1 or as its first part, with §11 row 11's cases. The lead whose helper ended unanswered and whose move
    back the service refused stays `completed`, said by a `Wait` line no row of *What needs you* carries (§5 point 4). It gains that
    row, with the refusal and the press to look at it; and the go-ahead read the service does not answer gains its case (§5 point 1).
12. **COWORKUX1i — a lead waits on its own partner's piece** (service, driver, modules, web; **deferred until COAGENT1d's
    `partner_ask` exists**, then designed and read under D191 before it is filed). `ended.awaits-partner`, read only where the lead
    asked the piece in the turn that ended; the service's tree holder, its move back to the person and their guards taught the partner's
    code beside the helper's (`SessionLedger.cs:1492-1514`, `:1529-1560`); delivery once per piece through D199's piece receipts
    (`PartnerPieces.cs:252-300`) rather than the helper's mark, which the design must settle; §11 row 12's proofs.

## 13. Not built, and unmeasured

- **Not built:** a composer on a member's page, a resumed or steered member (SESSCTX1a, STEER3 first); member-to-member messages; a
  coordinator; a stored structure; a team on a teammate's mirror; the Agents page's co-worker facts (COAGENT1e's sheet); a native
  subagent as a member (FABRIC-C1b).
- **Unmeasured:** the window was not seen, so the rows' and thread head's budgets and the fold's reading are the install's; §5 was read
  in HELPERWAIT1's landed code at `2323abccc` and its tests read, not run here (they ran with HELPERWAIT1, FIX-LOG's entry); round 4's
  fixes and their tests ran here (FIX-LOG's HELPERWAIT1b entry), not on the install; no real
  agent has yet asked a real helper with it on the install; a real partner's confinement and usage
  are COAGENT1a's; what a real lead does with a passed follow-up is the install's.

## 14. What it amends, once accepted

- **D163**: point 2's "its answer returning as the asker's next turn" holds for the agent's ask; the person's follow-up answers the
  person, and reaches the lead by their press (§7). Point 4's view is §6: *Helpers* is *Co-workers*, the answer a card.
- **D155**: point 11's record gains `serves`, this machine's own as `opinion` is (§4.1); point 7 stands, a recheck's findings to the
  person (§7); point 10's cap is amended by the several-reviewers design when written (§3.5).
- **D199 §6.6**: *co-worker* 同事 and *team* 团队 join the names; the head's partners line becomes *Co-workers* rows. Points 1-10 stand.
- **D167, D188**: CHANPART1's own design amends D167's SESSONE2b note and D188's COWORK1d note (§3.5). Points 1-8 of D167 stand.
- **D46**: the ledger's second move out of an ended state, `completed → awaiting-person` for a lead whose helper ended with no
  answer (§5 point 4), is HELPERWAIT1's, built and noted there (D46's note of 2026-10-23); row 12 extends it to the partner's code.
- **D116's glossary**: *subagent* narrowed; *co-worker*, *lead*, *partner*, *piece* become terms.
- **D51, D137, D177, D188, D189, D193** stand. D193's rejection of agents directing agents is what §3.4 rejects a coordinator by.

## 15. The reads

Round 1 (`2bdc8f5eb`) was read by Codex (every design, D191) and by Fable as a foundation (§5 changes how a driven turn's end is read;
§4.1 what a reviewer's record carries); both reads whole are in `docs/2026-10-23-coworker-reads.md`. Round 2 checked each finding
against the code at this branch (`fe4809b75` with the design merged) and, for the wait's mechanism, against HELPERWAIT1's branch at
`8b87af2be`. Every finding held as a fact about the code; where a finding said a claim of round 1's was wrong, it was. *Taken* means
the design changed as the finding asked, or as close as said; *answered* means the design keeps its course and says why, or points
to where the finding is built. The findings about the wait (Codex 1-5, 7, 8; Fable F1-F4) are taken as a contract and built by
HELPERWAIT1, not designed a second time (§5). Round 3 (§15.3) reads Codex's round-2 read against HELPERWAIT1 as it landed; the round-1
tables below keep round 2's wording, and where they say *not yet on HELPERWAIT1's branch*, §5 now cites where it was built. Round 4
(§15.4) answers Codex's round-3 read with HELPERWAIT1b's fixes; §15.3 keeps round 3's wording, and where §15.4 corrects it, §5 says so.

### 15.1 Codex

| # | Finding | Blocking | Taken or answered | Where |
|---|---|---|---|---|
| 1 | an ask not yet judged is not open, so a lead that exits at once parks | yes | taken: open is every own ask neither refused nor handed, state or none; HELPERWAIT1's `HelperWaits.Open` reads it so | §5 point 1; row 2's cases |
| 2 | `Parks` does not read questions to the person; the conclusion needs its own input, its freshness, unread told from absent | yes | taken: the question on file since the later of opening and last handed, and an unanswered go-ahead naming the session, read beside the conclusion; an unread go-ahead parks, which HELPERWAIT1's branch does not yet do | §5 point 1 |
| 3 | `Reopens: false` does not keep a park blocked | yes | taken: round 1's point 6 withdrawn; a parked lead's answer is held, not kept, and handed only once the person answers | §5 point 5 |
| 4 | `completed → awaiting-person` is refused by the ledger; the service is missing from the row; `clearSaid` | yes | taken: a new guarded service transition, `clearSaid` false, the service lane on row 2; built by HELPERWAIT1 | §5 point 4; §12 row 2; §14 |
| 5 | completing the lead lets go of its tree | yes | taken as the contract: one *holds its tree* predicate for the waiting lead, read by the exclusion, the lock, the alongside choice and the tidy; not yet on HELPERWAIT1's branch, so it is that row's to add | §5 point 3; row 2's cases |
| 6 | `serves` does not bound a helper's or reviewer's tools; Ask Daoris is not refused by `helper_ask` | yes | taken: server allow-lists for helper and reviewer with a census; Ask Daoris refused at both admission judges; round 1's claim corrected | §2.1; §4.1; §12 row 4 |
| 7 | the once-only hand has no mechanism | yes | taken: before a hand, the word found on `said` or as a `user` event naming the helper marks `Handed` and hands nothing; HELPERWAIT1's `HandedBefore` | §1 row 15; §5 point 6 |
| 8 | a helper's answer can reopen a part that was handed over | yes | taken: delivery along `from` to the newest live part, else kept for the person; not yet on HELPERWAIT1's branch, so it is that row's to add | §2.2; §5 point 7 |
| 9 | a reviewer's recheck findings go to the person, not the lead | yes | taken | §7's table; §14 |
| 10 | the head's helper read cannot give whole-line cards and receipts | yes | taken: the line's exchange read defined, placed at the call or the box's `at`, a member resolving it through `serves` | §6.3; §12 row 1 |
| 11 | the answers-alone reset has no writer on the lead's line | yes | taken: `helper.person-asked` kept by both person doors on the lead's line, `Resets` taught it; the pass keeps `answers.passed` | §7 |
| 12 | rows and the install proof depend on later rows; partner follow-ups unlinked | yes | taken: row 3 limited, *Ask again* and the strip in row 5, one proof per landing row; partner follow-ups are row 6's, not `after` | §6.4; §11; §12; §4.3 |
| 13 | CHANPART1 needs a new acknowledgment contract | yes | answered: deferred behind a design of its own, with what it must answer; the structure stays named | §3.2; §3.5; §12 row 8 |
| 14 | several reviewers are not specified enough to build | yes | answered: deferred behind a design of its own, with what it must answer; row 10's several-reviewers half waits with it | §3.2; §3.5; §12 rows 9, 10 |
| 15 | nesting already precedes *Ended*'s truncation | no | taken: round 1's claim withdrawn; the order kept as a regression case | §0.2 point 2; §1 row 6; §6.1 |
| 16 | reused helper words promise the answer to the lead | no | taken: destination-aware twins in both catalogues, the tool's response checked | §8.3; §12 row 5 |
| 17 | "nobody but the person stops a member" contradicts the pass bounds | no | taken: no agent and no level stops a member; the person and Daoris's declared bounds do | §7 |

### 15.2 Fable

| # | Finding | Blocking | Taken or answered | Where |
|---|---|---|---|---|
| F1 | `completed → awaiting-person` is not a ledger move; service lane; `clearSaid`; older service | yes | taken, choice (a), as HELPERWAIT1 builds it; choice (b) not taken, since it spends a turn to tell an agent what the person needs | §5 point 4; §12 row 2 |
| F2 | a helper's word kept on a park goes on at the next look | yes | taken: not kept on a park; held until the person answers, the card reading the box | §5 point 5 |
| F3 | the double hand after a restart is deterministic | yes | taken as HELPERWAIT1's evidence check rather than the partner's receipt shape, since the record carries the evidence; no producer key added | §5 point 6 |
| F4 | the guard on a question to the person reads nothing | yes | taken: the question box and the ask's go-aheads as the conclusion's input | §5 point 1 |
| F5 | the person's ask is said both to reset and to count | yes | taken: the fact corrected; the pass keeps `answers.passed`; the person's ask for the lead keeps `helper.person-asked` | §7 |
| F6 | nesting precedes truncation; a cut lead is the gap | no | taken: a lead with a live member is never cut | §0.2 point 2; §6.1 |
| F7 | `helper` on the event has two writers | no | taken | §4.2 |
| F8 | neither `:411` nor `HandAsync` changes for the held lead; the hold needs no mark of its own | no | taken: no change there; the planner's `CarriesOn` already leaves the plain completed record alone, so no snapshot mark is added | §0.2 point 3; §5 points 3, 6 |
| F9 | a restart during the helper's turn brings the lead to the person; older drivers unsupported | no | taken | §5 point 4; §4.3 |
| F10 | *Hand to the lead* / 交给主导 and `sessions hand` collide with *hand off* | no | taken: *Pass to the lead* / 转给主导, `sessions pass`, a `button` | §4.5; §6.3; §8.3 |
| F11 | the destination rule in one sentence; an older driver ignores `to` | no | taken | §1 row 10; §4.3; §7 |
| F12 | several reviewers with only two makers | no | answered: a question the several-reviewers design must settle | §3.5 |
| F13 | the reviewer's `serves` is sound; two more readers; each pass a member | no | taken | §4.1 |
| F14 | facts that hold | no | recorded; nothing to change | — |

Fable's names read (§8, D203 point 9) agreed with the names but F10; its cost and rows read asked for F1's lane and F3's mechanism,
both taken. Neither read disagreed with the other on a choice the steward had to settle: where both named the wait's defects, they
named the same ones.

### 15.3 Codex, round 2, answered against HELPERWAIT1 as built (round 3)

Codex read round 2 at `c2bddc515` beside HELPERWAIT1's unfinished branch (8 findings, 6 blocking, *another round*; the read whole is
`docs/2026-10-23-coworker-reads.md`, *Codex, round 2*). Its table of the round-1 findings (taken 20, open 5, wrong 2, deferred 3,
answered 1) carries each open and wrong verdict into its own findings 1-5, so only findings 1-8 are tabled here. Each was checked against the code at
`2323abccc`, this branch with HELPERWAIT1 merged; every one held as a fact about round 2's text. *Taken* means the built code does what
the finding asked; *answered* means it chose otherwise, and the row says why that holds or which row closes what remains.

| # | Finding | Blocking | Taken or answered | Code and tests | Where |
|---|---|---|---|---|---|
| 1 | unchanged guards cannot hand the helper's answer beside the person's | yes | taken: an answered park takes it in the door and the driver, and its go-on reads both words in one prompt; an unanswered park still refuses | `HelperDesk.cs:156-160`; `DriverHelpers.cs:634`; `DriverContinuations.cs:265-268`; `HelperHostTests.cs:138`, `HelperLookTests.cs:679`, `HelperWaitTickTests.cs:135` (*Process*) | §5 point 5 |
| 2 | `said` plus events is no durable receipt; a relay hides the evidence | yes | answered: Codex's first fix, built as an attempt mark on the ask's own state rather than a receipt store. Keyed once per ask, on the helper's side so consumption and relays leave it, an interrupted attempt settled `hand-uncertain`, the evidence read along the whole line. It holds, since a second store would hold the same fact twice; its one cost, a crash before the request left, tells the person rather than hand twice | `HelperAsks.cs:143`; `DriverHelpers.cs:260-274`, `:641-652`, `:657-680`; `HelperLookTests.cs:890`, `:923`, `:949`, `:858` | §1 row 15; §5 point 6 |
| 3 | an unread person-question file authorizes helper-only continuation | yes | taken: `AskedSince` tells absent (`false`) from unread (`null`), and only `false` waits; an unanswered go-ahead read likewise. No park is converted, so no conversion guard needs the read. The go-ahead's unread case has no test, so it is row 11's | `QuestionFiles.cs:68-72`; `DriverHelpers.cs:169-171`, `:179-194`; `HelperLookTests.cs:970` | §5 point 1; §12 row 11 |
| 4 | a legacy park cannot be proved to have asked the person nothing | yes | taken: legacy parks stay parked, never converted (round 2's `ParkedOn` not built); the helper's answer waits and is handed beside the person's answer, Codex's first option. The case it asked for, a question in the closing words alone, is the test | `DriverHelpers.cs:634`; `HelperLookTests.cs:679`, `:716`; `HelperWaitTickTests.cs:135` (*Process*) | §5 point 5 |
| 5 | a failed return-to-person move can hold the lead for good | yes | answered: every failure is tried again at each look, said once, rather than permanent and transient told apart. It holds: nothing else moves the record, every refusal the ledger gives clears by itself or never reaches the move, and an older service is not met in one install. What remains, a refusal said as a `Wait` line that no row of *What needs you* carries, is filed as row 11 | `DriverHelpers.cs:305-337`; `SessionLedger.cs:1529-1560`; `HelperLookTests.cs:795`; `SessionLedgerTests.cs:1107`, `:1130` | §5 point 4; §12 row 11 |
| 6 | row 6 assigns the partner wait without its service scope or proofs | yes | taken: row 6 is presentation alone; the partner's wait is row 12, deferred until `partner_ask` exists, with the service, driver, modules and web scope and §5's proofs a partner's way | `SessionLedger.cs:1492-1514` (the helper's code alone) | §5 point 2; §11 rows 6, 12; §12 rows 6, 12 |
| 7 | a resume notification is not the answer taken | no | taken: only a `user` event with `reaches` absent is a taking, in `HandedBefore` and on the card; row 1 gains the notice-only case | `DriverHelpers.cs:266`, `:689-694`; `HelperLookTests.cs:923` (the notice case) | §4.2; §6.3; §11 row 1 |
| 8 | a restart does not always refuse the interrupted helper's ask | no | taken: the sentence qualified, *without a saved answer*; both cases proved | `DriverHelpers.cs:556-567`; `HelperLookTests.cs:615` | §5 point 4 |

Two of Codex's choices were made differently, and both are said above: finding 2's receipt is a mark rather than a store, and
finding 5's recovery is a retry each look rather than a split by failure. Neither is withdrawn. Row 11 carries the part of finding 5
the retry does not cover, and the untested half of finding 3. Codex's verdict asked the next read to check the revised contracts and
their evidence, and D203's corresponding claims; both are revised (D203's round-3 note).

### 15.4 Codex, round 3, answered with HELPERWAIT1b (round 4)

Codex read round 3 at `3a928e71c`, with HELPERWAIT1 as built merged in (6 findings, 5 blocking, *another round*; the read whole is
`docs/2026-10-23-coworker-reads.md`, *Codex, round 3*). Its table of the round-2 findings (taken 4, open 3, deferred 1) carries each
open verdict into its own findings 1, 2 and 6, so only findings 1-6 are tabled here. Each held as a fact about the built code. Findings
1-4 are fixed in the code by HELPERWAIT1b, red first, at this branch (`coworkux1-reads3` merged); finding 5's case is changed and run;
finding 6 changes the build order. The two choices Codex kept as sound, the receipt on the ask's state file and the retry at each look,
are kept. *Taken* means the code or the order now does what the finding asked. Line numbers are this branch's after the fix.

| # | Finding | Blocking | Taken or answered | Code and tests | Where |
|---|---|---|---|---|---|
| 1 | a conclusion deletes the unread question it parked for | yes | taken: a parked conclusion keeps a question file that does not read, at a first run's conclusion and a resumed run's; a read of it is still none, and a conclusion that does not park keeps nothing | `QuestionFiles.cs:115-120`, settled at `Driver.cs:2452` and `DriverContinuations.cs:583`; `QuestionFilesTests.cs:221` (the unread file read as asked, kept by both settles, gone where nothing parks) | §5 point 1 |
| 2 | an ambiguous hand response clears the attempt mark | yes | taken: three outcomes, the desk's own refusal alone clearing the mark; a status no refusal has, a success that does not read and no answer keep it, said as a retry, and the next look reads the line back: found, handed; nothing found, `hand-uncertain`. The case Codex asked for, a word kept, its answer failed, then consumed with no event, is the test | `HelperDoors.cs:72-131`; `DriverHelpers.cs:674-688`; `HelperLookTests.cs:976` (500, unreadable, timeout; found and consumed; one hand each) | §5 point 6 |
| 3 | cleanup lets go of a waiting lead's tree once its last ask is refused and its return failed | yes | taken: the tidy, the lock's let-go and the housekeeping read the service's predicate, waiting on its own helper while its quest is taken, never an ask being open. The let-go reads the same set, so its lock is kept with its tree; the let-go's own keeping of what the set names is `SessionLocksTests`' existing case | `BranchTidying.cs:103`, `:163-171`; `HelpHousekeeping.cs:193`; `BranchTidyingTests.cs:694` (held while open, after the refusal with the return failed, with no ask on file; let go once its quest is done) | §5 point 3 |
| 4 | the sixteen-link bound can deliver to a part the line left | yes | taken: refused, not resolved to the newest. Both sides follow one link past the bound, and a line that goes on is refused with its reason, nothing kept; the helper's conversation keeps the answer. Reading on is what the bound stops, and no person makes seventeen relays | `HelperDesk.cs:143-155`, `:202`; `DriverHelpers.cs:605-612`, `:726-752`; `HelperHostTests.cs:141`, `HelperLookTests.cs:1012` (sixteen reach the newest; seventeen refused) | §5 point 7 |
| 5 | the cap-one Process case runs at the default cap of two | no | taken: the case runs at `Cap = 1` and holds that the helper's pass started with its asker already `completed`, in the slot the asker's run freed; it passed by class with `process.runsettings` | `HelperWaitTickTests.cs:68`, `:103`, `:120` | §5 point 3 |
| 6 | row 11 is not a prerequisite of the first live showing | yes | taken: row 11, renamed HELPERWAIT1c, is a prerequisite of row 1's landing and install proof, with its refused-versus-unreachable cases and both languages; the install proof is after rows 1, 2 and 11 | `TASKS.md`'s HELPERWAIT1c and COWORKUX1 rows | §5 point 4; §11; §12 rows 1, 11 |

Beside the four, HELPERWAIT1b classes three of HELPERWAIT1's look lines where they are made, which the look's census refused once
HELPERWAIT1 met it on this branch (`DriverHelpers.cs:323-336`, `:677`; `LookSaidTests`). The partner's hand door follows its lead's line
under the same sixteen-link bound (`Core/Sessions/PartnerDesk.cs:116`) and is not changed here, since the partner's wait is row 12's.
The same defect there is filed as its own task. Codex's verdict asked the next read to check these fixes and their proofs, and D203's
claims; D203's round-4 note revises them.
