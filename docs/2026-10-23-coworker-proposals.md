# Co-workers: one model for every agent that works beside a session: the proposals

> COWORKUX1, phase 1 of a co-design under D191 (2026-10-23). Each section is one maker's complete proposal, written from
> `docs/2026-10-23-coworker-brief.md` alone and sealed before its author read another. Sections are added verbatim and
> never edited afterwards. The synthesis, in a later quest, is written apart. Claude's was written first; Codex's second,
> through a helper's call; Fable's comes with the synthesis.

## Claude (Opus 5.5)

*Sealed 2026-10-23, before any other proposal was read.*

### C1. The shape in one paragraph

The steward's foundation holds, with two corrections and one addition.

**Correction 1: a member is one agent in one role beside one line of work, not one record.** Each exchange is a record
of its own, one turn, as it is today, so a member is to its exchanges what a session is to its parts (D167).

**Correction 2: a reviewer is a member of the line whose work it read**, joined by its candidate, since its record names an
opinion and not a session.

**The addition comes first: the lead's own state learns that it is waiting on a member.** A lead that ended its turn so
that its helper could start is not waiting for the person, and its helper's answer is never refused for the reason
§4 of the brief shows. That is the defect the owner met, and no strip or card fixes it.

On top of that the page draws the team one way everywhere:

- **a strip in the head**, one chip per member: its maker's mark, its role, its state;
- **one card in the lead's conversation per exchange**, live while it works, holding the answer once answered, and opening
  the member's conversation;
- **the member's conversation in the same chat surface**, with the lead one press back;
- **the list nests members under the lead's row**, whatever group each would rest in alone.

The person talks to a member by **asking it a further exchange in its role**, never by resuming it. Its answer comes to
the person, and goes to the lead only by a press. No bound of D163, D199 or D155 loosens.

**The names.** English *co-worker* stands. In Chinese, 同事 rather than 协作者.

### C2. The model (question 1)

**A team is a line of work and the agents working beside it.**

- **The line** is the lead's session: its entry and its parts (D167), whoever leads it now. A hand-over makes a new part,
  not a member, and the team follows it along `from`, as D199 point 6 already has pieces follow.
- **A co-worker (member)** is *one agent, in one role, beside one line*. Its key is `(entry, role, agent)`, where agent is
  the adapter: Codex as a helper and Codex as a partner on the same line are two members.
- **An exchange** is one record of that member: one question and its answer, one piece and its commit, or one pass and its
  findings. Exchanges are numbered like parts, oldest first. The member's conversation is its exchanges' conversations in
  order, as an entry's is its parts'.

Why a member is not one record:

- Each role is one turn with no resume. Resume is unmeasured for Codex (D199 point 2) and refused for a helper's record
  (`SessionSayRefusal.Helper`).
- One record per member would put a new chip on the strip for every question. Two questions to Codex in an hour would
  read as two colleagues.
- The owner's word, *colleague*, is a someone who stays across exchanges, not a message.
- A member with exchanges is also the place where a later measured resume (D199's "a conversation kept across pieces")
  would live without a model change.

**Roles**, today's three by their records:

| Role | Its record has | Its exchange |
|---|---|---|
| reader (helper, D163) | `serves`, no `piece` | a question and its answer |
| partner (D199) | `serves` and `piece` | a piece and the frozen commit Daoris read |
| reviewer (D155) | `opinion` | a pass, its findings, and the lead's answers to them |

**Not members**, each for its reason:

- **A hand-over and every other part.** It *is* the lead, in turn. It is shown in the Parts strip (co-working design
  §1.6), as today.
- **Another repository's session on the same ask.** It is another department (D193 part 3) and talks by quest and
  channel. It stays in *Who it worked with* (`work.relations.*`).
- **A meeting's participant (D177).** A meeting is the person's conversation with several agents, not one line's
  co-workers.
- **A native subagent inside the lead's own process** (the glossary's 子智能体). Daoris neither starts nor confines it.
  When FABRIC-C1a binds one to its part it can join as a fourth role, *inside*, shown only from what Daoris observed. Until
  then it stays inside the lead's own conversation, as its tool calls.
- **Ask Daoris and an intake.** They lead no team: `helper_ask` already refuses them, and no change here.

### C3. The records (question 2)

No new record kind, no new ledger state, nothing on the wire.

- **The role is read, not stored.** `serves` without `piece` is a reader; `serves` with `piece` is a partner; `opinion` is
  a reviewer. One function, `CoWorkerRole`, lives in `Driver/Trees/SessionGroups.cs` beside `Serves`, with its twin in
  `Web/src/work/identity.ts`. A table test (`fixtures/coworker-roles.json`) is held by both.
  - It replaces the page's `isHelper` wherever a name or a word is chosen. Today `isHelper` is `Boolean(serves)`, so a
    partner would be named *Helper*.
  - `isHelper` stays only where the one-turn restrictions are shared (`oneTurn`, `acts.ts`, `sessionLife.ts`), renamed
    `isCoWorker` there.
- **The team is a read**, `Team.Of(entry)`, in the driver's groups. For each record whose `serves` names any part of the
  entry, or whose opinion's candidate is that entry's work, it groups by `(role, adapter)` and orders the exchanges by
  creation.
- **The reviewer's link.** A second opinion is bound to a candidate (a chain's work in one repository, D155 point 2), and
  its box already names the quest and the commits. The team read joins it to the entry whose part took that quest. Where
  the candidate spans parts, it joins to the entry, never to one part. No `serves` on a reviewer's record: it serves the
  work, not a conversation, and D155 point 6's "it does not read the working session's conversation" stays true by
  construction.
- **The terminal list.** `daoris-driver sessions --json` gains `serves`, `piece` and `role` on each session, and `team`
  on each entry (members, each with its exchanges' ids and states).
  - The plain listing prints members indented under their entry, after its parts, as `with Codex · reader · answered`.
  - A member's record leaves the top level of its group (C5.1), as the page's rail does.
- **Machine-only, as before.** `serves`, `piece` and `opinion` never travel (`session-record-fields.md`). A teammate's
  mirror shows a line with no team, and says nothing about one.

### C4. The lead's state while co-workers work (question 3)

This is the first row, because the owner's case is a state bug before it is a drawing one.

**C4.1 Waiting on a co-worker is not waiting on the person.** Where a turn ends and the quest would park as asked
(`ended.parked-asked`), and the lead has an open exchange it asked, the session stays working and waits for that answer.
An open exchange is a helper's ask that is `waiting`, `approved` or `started`, or a piece that is preparing or working.

- It is the precedent BGWAIT3 set for a background command (`AcpWake.Unseen`): where the quest would park, the session
  stays working and waits.
- Its record is `working`. The planner holds its next start on the exchange: a hold with its reason, as the person's words
  have their holds (`SessionGroups.cs`'s holds), and no new ledger state.
- Its list group is *Working*, its pill *waiting on Codex* / *等 Codex*.
- The exception is a lead whose closing words or tools asked the person something. A `question_ask` or `go_ahead_ask` it
  made, or a *Needs you* item with content, keeps it parked on the person: both are true, so both are said.
- A conversation is the same: it rests between turns, and its head says it waits on Codex.

**C4.2 An answer is never lost to the lead's state.** `HelperDesk.HandAsync` (`Core/Sessions/HelperDesk.cs:141-159`)
stops refusing an `AwaitingPerson` lead. It keeps the word on the record with `Reopens: false`.

- The person's answer, when it comes, carries it as *your words and a helper's answer* (`words-and-helper`, already
  built: `note.resumed.why.words-and-helper`).
- A stood-down or gone lead still refuses. Its answer is then shown on the member's card and its exchange, and goes to the
  person, as D155 point 7 sends findings a session cannot take.
- `PartnerDesk.HandAsync` takes the same rule.

**C4.3 The lead's waits and the bounds.**

- An answer that reopens a lead is a go-on, counted by D188's answers-alone bound, as today.
- Waiting on a co-worker is bounded by the co-worker's own minutes. Past them the exchange ends `refused`/`out of time`,
  and its note reopens the lead with that fact, as a word that is a note and resets no count (D199 point 8).
- The session watch (D200) reads a lead waiting on a co-worker as quiet only once the co-worker is.

### C5. The page (question 4)

#### C5.1 The list

- **A member's exchanges nest under the row of the lead's entry**, in every group the lead's row is drawn in, whatever
  group each would rest in alone. `nestServing` (`Web/src/work/groups.ts:304`) takes the team read rather than `serves`
  alone, so a reviewer nests too and a finished helper no longer stands alone in *Ended*.
- **One nested row per member**, not per exchange: `Codex · reader · 2`, with the newest exchange's state.
- **`keepOwn` stays.** A member that waits on the person (a helper's ask under `ask`, an uncertain piece, a dispute) is
  also drawn in *You*, as today, so nothing that waits on the person is folded away.
- **The lead's row shows its members' marks** after its title: one maker's two letters per member (*Cx*), never a hue
  alone. A working member's mark carries the working dot.
- **Nothing hides itself.** A finished member stays nested until the line is archived.

#### C5.2 The head: the team strip

A row under the facts line, present where the line has a member or an open ask. The lead first, then members in order
of their first exchange:

`Lead CC · Claude Code` | `Cx Codex · reader · answered` | `Cx Codex · partner · working` | `Cx Codex · reviewer · 2 findings`

- **Each chip is a press** that opens that member's conversation (C5.4). The lead's chip comes back.
- **The strip replaces the head's *Helpers* list** (`HelperAsks`). That list shows only open asks and drops each one once
  it is answered, so a finished helper has no place on the lead's page today. A chip stays for the line's life.
  `HelperAsks`'s *Let it ask* / *Not now* move onto the card (C5.3) and stay in the wait strip.
- **A chip's state** is its newest exchange's, in the role's own status words (C8): *working*, *waiting for you*,
  *answered*, *taken in*, *not taken in*, *refused*, *out of time*.
- **The strip's ⋯** holds *Ask another agent…*. One dialog chooses a role (*Read and answer* / *Write a piece* /
  *Second opinion*), the agent and the words. It replaces today's separate entry points; each role's rule and admission
  judge it as now, and a role the line's rules turn off is offered greyed with its reason.
- **The spend per maker** moves onto the strip (D199 §6.1's *Spent: Claude Code 41k · Codex 12k*). *Not measured* is
  never written as zero.
- **At the narrowest frame** the chips fold to marks and states (`Cx ●`), and the strip's ⋯ lists them whole.

#### C5.3 The lead's conversation: one card per exchange

An exchange is one card in the lead's conversation, placed where it was asked, updated in place, never a second block
later.

- It replaces, on the page, today's coded lines for asked and answered, and the answer now folded under *The target
  Daoris composed*. The page's `SessionEvent` gains the `helper` field the driver already writes
  (`DriverHelpers.cs:443`), which closes MIXAGENT3a-iii's block half.
- The coded notes stay in the record and the timeline; the card is drawn from them.

| Phase | The card says | Acts |
|---|---|---|
| asked, waiting for the person | *Asked Codex to read: "{{question}}"*, then *Waiting for your yes* | *Let Codex read* · *Not now* |
| working | the question, then *Codex is reading · 6 min*, the member's last tool line as a live sub-line | *Open its conversation* · *Stop…* |
| answered | the question, then the answer's first lines, folded, *a claim to check* | *Open its conversation* · *Show all* |
| handed to the lead | a line under it: *Went to the lead as its next turn* | — |
| refused, out of time | the reason in words | *Ask again…* |

- **A partner's card** shows the piece, then the commit, its files and *n files beyond its piece*, with the merge line the
  lead was handed. Its acts are those of D199 §6.1's Pieces section, which becomes the partner's cards plus the strip:
  there is no separate section.
- **A reviewer's card** sits where the pass was asked (the gate, or the person's ask). It shows findings with the lead's
  answers (`fixed`, `rejected`, `unresolved`), and disputes beside *Reviewed* as D155 point 9 has them.
- **The answer delivered as the lead's next turn is the same card**, not a second message. The lead's turn that follows
  starts under it with *Went on with Codex's answer*.

#### C5.4 The member's conversation, in the same surface

- **The session page opens on the member's exchange record**, as it does today for a helper (`oneTurn`,
  `Web/src/work/WorkFrame.tsx:494`).
- **Above the conversation, a back line**: *Working with {{lead}} · reader* / *与「{{lead}}」共事 · 读*, which returns to
  the lead at the card.
- **A member with several exchanges** shows them as an exchange strip, the Parts strip's shape, newest first.
- **While it works**, its conversation streams as any session's does, with the session's live state. A member is a
  session, so nothing is drawn specially.
- **The box** is replaced by the role's door (C6).
- **A partner's page** shows its piece's diff against its base, from the frozen ref, and offers no *Accept…* (D199 §6.1).
- **A reviewer's page** shows what it read (D155 point 6) and its findings.

#### C5.5 What needs the person

No new kind. The kinds stay as they are (a helper's yes, a piece's yes, an uncertain piece, a dispute), and each wait's
row in *What needs you* names the lead as well as the member: *Claude Code asks Codex to read · Let Codex read*. Its
press opens the lead's card, not the member's page, because the card holds the question and its context.

### C6. Talking to a member (question 5)

**Talking is asking a further exchange of the same member, in its role.** It is never a word into a finished record:
records stay one turn, and nothing is resumed. The box under a member's conversation is that role's door:

| Role | What the person may say | Bounds kept | Where its answer goes |
|---|---|---|---|
| reader | a further question | read-only, one turn, the minutes, two at once per lead | to the person, on the member's page; to the lead only by *Hand to the lead* |
| partner | a further piece, or *Redo this piece…* with words | one turn, its clone at the lead's current commit, two open pieces | the piece's card in the lead's conversation; taken in only by the lead's merge |
| reviewer | *Read again* after the lead's commits | D155's recheck, once | the findings to the lead, as D155 point 7 |

- **Each further exchange is briefed with the member's earlier exchanges.** Their questions and answers, bounded as
  `ParkedWords` is, are handed as Daoris's record, never as the member's transcript (D163's rejection of carrying a
  transcript stands). So the member reads as one colleague who remembers what it was asked, without resume.
- **The person's question to a reader answers the person.** It goes to the lead only through a press, *Hand to the
  lead* / *交给主导*, Slack's *Also send to channel* made deliberate. So the person can ask a co-worker something without
  setting the lead off, and no answer reaches the lead that the person did not choose.
- **The person's own ask is not under the rule.** It is the person's yes (`askedBy: person`, as `sessions helper` is
  today).
- **D188 is unchanged.** A further exchange the lead asks is still under the rule and the answers-alone bound. A further
  exchange the person asks is the person's turn.

### C7. Both doors and Ask Daoris (question 7)

- `daoris-driver sessions team <session>` prints the team: the lead, each member with its exchanges and states.
- `daoris-driver sessions ask <member> "<words>"` asks a further exchange in the member's role.
  - The member is named `<entry>:<role>:<agent>` or by any of its exchange ids.
  - It refuses where the role's door would, in the same words.
  - `sessions helper` and `sessions partner` remain as the first exchange's verbs.
- `daoris-driver sessions hand <exchange>` hands a reader's answer to the lead.
- Ask Daoris may propose any of these as a `session` card. It never asks a co-worker itself (D110, D183).

### C8. The names (question 6)

**The umbrella.**

| Concept | English | 中文 | Why |
|---|---|---|---|
| an agent working beside a line | **co-worker** | **同事** | see below |
| the line and its co-workers | **team** (in prose only) | 团队 (in prose only) | the strip carries no title; the chips are the name |
| the line's own agent | **lead** | **主导** | D199's word, kept |
| one record of a co-worker | **exchange** | **一轮** | a turn given and answered; *part* (段) is the line's own |
| the reader role | **reader** | **读** on a chip; 助手 stays the term | see below |
| the partner role | **partner** | **搭档** | D199's, kept |
| the reviewer role | **reviewer** | **阅读者** | D155's, kept |

**Against 协作者.**

1. In Chinese git hosting, 协作者 is a repository's *collaborators*: people granted access. GITAPP1 and D198's git
   access are about to put repository access on Daoris's own screens, and the word would mean two things a screen apart.
2. 协作 already titles *Who it worked with* (协作关系), the relations with other repositories (`work.relations.title`).
   A co-worker is exactly *not* another repository, so the word would join the two things the model keeps apart.
3. 协作者 is formal and symmetric. It names a peer contributing to shared work, while these agents work *for* one line,
   under the person's rule.
4. It is three characters where a chip has five units, beside a maker's name.

**For 同事.**

- It is the everyday word for the people you work beside. D193 frames sessions as staff of a company, and that is exactly
  the relation the owner said is missing: *a colleague working beside the session*.
- It has two characters, and does not collide with the glossary: 队友 is the person on another machine, 搭档 the partner,
  助手 the helper.
- In English, *co-worker* is already the ask's word and fits the same relation.
  - *Colleague* would do as well, but the steward's word costs nothing to keep.
  - It does not collide with any window string. *Co-working* (D167) is a design's and a skill's name, never a label
    (`Web/src/locales/en/*.json` holds none).
  - D199 rejected *co-work* as a *partner's* name, where it blurred the writing role. Here it names the umbrella, which is
    what that rejection left free.
- *Teammate* stays the person on another machine (`asks.elsewhere.*`, `history.count.teammates`). That is why the
  collective is not titled *Team* on the window.

**The reader's role word.** The owner found *helper* (助手) reads as a side answer. 助手 is an *assistant*, someone
subordinate who answers. On chips, where the role sits beside a maker's name, the role is said as what it does:

- *reader* / 读, *partner* / 搭档, *reviewer* / 阅读者.
- The glossary term *helper* / 助手 stays for settings and the rule (`settings.helpers.*`), where it names the rule.
- A proposal to rename it there waits on the synthesis.

**States, `status` kind, ≤16 / ≤5:**

| | English | 中文 |
|---|---|---|
| the lead waits on a co-worker | waiting on {{agent}} | 等 {{agent}} |
| a member works | working | 工作中 |
| answered | answered | 已答复 |
| waits for the person's yes | waiting for you | 等你处理 |

The partner's states are D199 §6.1's, unchanged.

**Buttons, `button` kind, ≤20 / ≤8:**

| English | 中文 |
|---|---|
| *Ask another agent…* | *请同事…* |
| *Open its conversation* | *打开对话* |
| *Hand to the lead* | *交给主导* |
| *Ask again…* | *再问…* |

**The glossary.**

- It gains *co-worker* / 同事 and *exchange* / 一轮, with `avoid` 协作者 and 子代理 for co-worker.
- It gains D199's *lead* / 主导, *partner* / 搭档 and *piece* / 分项, which are not terms yet.
- *Subagent* / 子智能体 is narrowed to "an agent started inside a session's own process", so it no longer defines
  *co-worker*.

### C9. The rows (question 8)

In order, each one feature (D187 point 5), each with its proof:

1. **COWORKUX1a — the lead waits on its co-worker, and no answer is lost** (driver, service). C4 whole.
   - Proof: a Process case where a driven lead asks a helper and ends its turn, rests in *Working* with the hold, and goes
     on with the answer; one where the lead also asked the person, and the answer goes with the person's words.
   - Then GITAPP1's case on the install, the parent's watch reading it.
2. **COWORKUX1b — the team read and the terminal** (driver). `CoWorkerRole`, `Team.Of`, the list's nesting in the
   terminal, `sessions team`.
   - Proof: the role table test held by both twins; the groups test with a reviewer, a helper of a non-leading part, and a
     finished member.
3. **COWORKUX1c — the strip and the list** (web-shell). C5.1 and C5.2 in both languages, within the budgets.
   - Proof: stories at 888 px, both themes, both languages; the naming check.
4. **COWORKUX1d — the exchange card** (web-shell, driver events). C5.3 for the reader and the reviewer, with the event's `helper`
   field; MIXAGENT3a-iii's block half is closed by it. The partner's card comes with COAGENT1c, since no partner can be
   asked yet. The coded notes' twins stay.
   - Proof: the conversation's tests from recorded events, and the installed window's proof on the next real helper.
5. **COWORKUX1e — the member's conversation and talking to it** (service, driver, web-shell). C5.4 and C6: the back line,
   the exchange strip, the further exchange with its brief, *Hand to the lead*, `sessions ask` and `sessions hand`.
   - Proof: a Process case of a further question briefed with the first, answered to the person; *Hand to the lead*
     reaching the lead at its turn's end.
6. **COWORKUX1f — the names**. The glossary terms, the zh review, the reader's chip word.

### C10. What stays unmeasured, and what this does not cover (question 9)

- **Resume of a co-worker is not used.** Every further exchange is fresh with a brief. If D199's measurement admits resume
  for a maker, a member's exchanges could become one kept conversation: a later row, the model unchanged.
- **The partner's card waits for the Pieces section to exist** (COAGENT1b/c). This proposal only says the section's
  content becomes cards.
- **The live sub-line** (the member's last tool) depends on the member's events reaching the page while it runs. For a
  helper they are its own record's, so they should; this is not checked on Codex here.
- **Foundation.** C4 changes how a turn's end is judged, a rule other designs rest on (D83, D137), so by D191's note this
  design merits Fable's read.
- **Not covered:** native subagents (C2), meetings (D177), the ask's channel, and teammates' mirrors.

## Codex (codex-acp, through Daoris's helper, ask e30ef527)

Its answer as the helper gave it, verbatim but for its headings set one level down. Written from the brief alone; it did not read Claude's section.

#### X1. The shape in one paragraph

A team is a local view of one lead's line and the bounded exchanges it commissions. Every co-worker is a real session record in a role, with its own conversation and attributable result. Keep the helper, partner and opinion mechanisms; join their identities and receipts into one team view. Put each exchange in the lead's conversation, give it a live state and a door into the member's conversation, and keep its result readable even when delivery is held. A follow-up starts a fresh bounded exchange; it never secretly resumes a one-turn member. First repair the lead that awaits its helper while being presented as awaiting the person.

Independent proposal; excluded proposals/history/commit contents and TASKS.md were not opened. Nothing changed; no repository programs ran. Citations refer to this copy. Abbreviations: Core = src/Daoris.Service/Daoris.Service.Core; Driver = src/Daoris.Desktop/Daoris.Desktop.Driver; MCP = src/Daoris.Service/Daoris.Service.Mcp; Web = src/Daoris.Web/src. Dnn means docs/decisions/Dnn.md.

#### X2. The model

A member is one commissioned exchange, represented by one session record. Role describes authority; agent/maker describes who performed it. Another question makes another record, optionally a follow-up; there is no persistent “Codex colleague” spanning copies/accounts.

A team is the lead lineage and its commissioned helpers, partners and reviewers. An exchange includes commission, execution, result and delivery, even before a member starts. Helpers already have serves and ownEntry (Core/Sessions/HelperDesk.cs:99–102); partners retain their own entry (D199:16–19). Sharing a chat surface does not make a member a part.

Hand-over changes lead. History follows the line for display; pending obligations/results follow only explicit from successors, not visual entry leadership (docs/2026-10-22-coagent-design.md:170–177). Preserve commissioner/capture/candidate. Never redeliver because leadership changed.

Exclude lead parts/successors, cross-repository sessions (D167:34–43), D177 meetings, native harness subagents and Ask Daoris. These retain their existing surfaces/authority.

Amend D163 point 4 and D199 point 1 for common presentation/terminology. D167's parts/entries distinction stands.

#### X3. The records

No team/persona store. Derive a local projection over existing boxes and opinions:

| Field | Meaning |
|---|---|
| exchangeId, role | Namespaced ask/piece/opinion id; helper, partner or reviewer |
| commissionedBy, currentLead | Original responsible record; explicit eligible successor along from |
| memberSession | Actual record; absent before start |
| request, result | Authored content, untranslated |
| execution | Pending, queued, working, ended, failed, refused |
| delivery | Waiting, sent, taken, answered, uncertain; observed receipts only |
| work | Partner import/incorporation, separate from delivery |
| identity, usage | Observed agent/maker/version/account/usage; absent stays unknown |
| follows | Optional previous exchange on a fresh commission |

The reviewer link exists: Session.Opinion -> Opinion.Id -> Opinion.Working; Opinion.Session identifies the reviewer (Core/Review/Opinions.cs:114–119). Join it; adding serves would misclassify reviewers. Keep candidate, recheck, findings and dispositions in the opinion store.

No umbrella role column: Opinion, Serves, Piece and From already distinguish records (Core/Sessions/Sessions.cs:145–175). Optional follow-up metadata belongs on existing commissions, with both readers updated. Keep the projection off sync.

Add helper to the web event reader and preserve it through parsing. Service words distinguish helpers (Core/Sessions/Sessions.cs:375–380); Web/work/conversation/conversation.ts:45–80 has opinion but no helper. Resolve a helper-marked partner through its piece.

Give helper delivery a stable exchange key and durable receipt. Today's duplicate check only examines queued words (Core/Sessions/HelperDesk.cs:161–168); consumption must not erase delivery identity. An attempted delivery across restart becomes uncertain, never automatically sent twice. Ending, delivery and incorporation remain separate facts. Terminal reads expose this same projection, including commissions never started, plus serves/piece/opinion in raw JSON.

#### X4. The lead while members work

Waiting for a member is not waiting for the person. Record a durable local continuation cause, members, with exchange ids and yielding turn. Project awaiting-member while retaining the existing parked ledger/tree/quest hold. Every reader must distinguish this cause from a person-question; no new ledger lifecycle is needed.

At a clean boundary reconcile commissions before the generic Taken park. A bound person-question wins. Otherwise an ordinary helper commission records member-wait; a partner needs an explicit yielded boundary because its commission alone does not imply waiting. Reconcile/clear on result, person instructions, hand-over or terminal outcomes.

Repair GITAPP1 only from its ordinary ask, successful boundary and absence of a bound person-question. If unprovable, preserve the park and show the retained result with its hold; never infer intent from last words.

| Place | Change |
|---|---|
| Observation | Typed member-wait before generic park; no ended.parked-asked |
| DriverHelpers | Remove parked blanket pre-handoff block |
| HelperDesk.HandAsync | Permit durable keeping on local parked lead; validate destination/exchange, stable-key deduplication |
| Planner and chat runner | No result: wait. Result: existing go-on, subject to genuine person-question, pause/stop, eligibility and answers-alone |
| Groups | Member-wait in Later, awaiting answer; no You/bell merely for parking |

Keeping and continuation authority are separate: an answer can be visible/queued while an actual person-question still blocks execution.

Both blocks exist today: Driver/Loop/DriverHelpers.cs:411 skips parked leads; Core/Sessions/HelperDesk.cs:148–159 refuses them. Generic park: Driver/Sessions/Domain/Observation.cs:144–158. Groups: Driver/Trees/SessionGroups.cs:1080–1084. Fix planner's “you answered/wrote” attribution too (Driver/Quests/Domain/Planner.cs:1461–1467).

Preserve CAPCOUNT1: parked driven lead and running member each retain their slot. Full cap means queued, not running. Cap one cannot progress this combination; changing slot/tree reservation requires a separate design, not silent over-admission.

Amend D163 and D131/STANDDOWN2's generic park interpretation. D167's no-new-ledger-state rule and D188's bound stand.

#### X5. The page

Use Sessions, with no new top-level place.

**List:** lead entry plus member children with role, agent, activity and delivery. Nest across groups before truncating Ended. Active, failed, uncertain and actionable members stay apparent. Finished children fold only behind an explicit count. Preserve selection. 

**Brief correction:** You versus Ended alone does not prevent nesting. Web/work/groups.ts:309–329 flattens supplied groups; Web/work/rail/SessionList.tsx:238–247 supplies state groups together. Its keepOwn (:164–166) tests the child's You/Review status. Missing rows/links or filtering could separate them, but the brief's §4 causal explanation is unsupported.

**Head:** “Team / 团队”, lead first, then role, agent, maker's textual mark and state per member. CC/Cx or a plugin mark supplements the name; hue is insufficient. Show usage per maker only where measured, never absent as zero. Wrap into rows on narrow panes.

**Conversation:** one card starts at the commission event, keyed by exchange id. Show commission, member, capture/candidate, live activity, queue/approval reason and a door. Answered cards expose the attributed result, open by default, plus observed receipts. Fold long evidence separately. Partner cards show frozen commit, files, import and incorporation. Opinion cards connect findings/dispositions; retain the opinion gate.

Use typed commission references, not parsed note sentences. Legacy results without recoverable history are labelled accordingly. Fix today's helper mislabelling first: Web/work/conversation/ConversationView.tsx:382–403 recognises opinion/Daoris/channel claims, then falls back to composed target.

**Member conversation:** open its actual record in the same chat surface. Keep role, commission, confinement and Back to lead visible. Show observed activity, or honestly queued/no transcript yet. Finished members offer fresh follow-up, not resume. Do not dump their transcript into the lead's.

**What needs you:** approvals, held answers, opinion disputes and uncertain delivery/import reuse WaitProps and the .NET judge, named by member. Strip/card open the same wait; one bell count. Reading, working, cap queue and automatically deliverable results are not themselves person tasks. Follow .claude/knowledge/person-waits.md; reviewed remains the person's (D155:54–73).

| Kind | English | 中文 |
|---|---|---|
| section/tab | Team | 团队 |
| section | Exchanges | 协作记录 |
| button | Open conversation | 打开会话 |
| button | Back to lead | 返回主导 |
| button | Ask a follow-up… | 继续提问… |
| button | Ask for a piece… | 请做分项… |
| button | Allow helper / Allow partner | 允许助手 / 允许搭档 |
| button | Not now | 暂不 |

Labels fit the budgets; dynamic names/questions occupy content lines. Prove the narrow layout against docs/2026-10-01-naming-design.md:181–200.

#### X6. Talking within the role

“Talk” means a fresh bounded commission, not unlimited chat.

| Role | Follow-up | Execution / destination |
|---|---|---|
| Helper | Clarify/challenge evidence; related question | Fresh capture, record and one turn; person sees answer |
| Partner | Another piece or repair | Fresh base, admission and allowance; person sees result, lead receives it once |
| Reviewer | Permitted focused recheck | Fresh pass through existing opinion mechanism/cap and D155 routing |
| Reviewer after budget | Explanation of recorded evidence | Explicit helper commission linked to opinion; cannot alter its gate |

Explain that the last action starts a helper interpreting evidence, not the original reviewer remembering its reasoning. Preserve D155's independent first packet (D155:45–53).

While a member runs, the person may submit a separate pending follow-up, cancellable before start. It starts after that member ends, with fresh bounds; no words enter the old one-turn record. Stop remains explicit.

Person-commissioned helper follow-ups default to person-only results. Offer Send to lead / 转给主导 separately: forward an agent claim and record the press without relabelling the claim as the person's. Agent-commissioned helpers retain automatic return. Partners always retain the lead/import route; reviewer rechecks retain their person-directed findings route.

Fresh commissions spend their own slot/run and obey depth/admission. Opening a card or drafting a question never resets answers-alone. Two answers in one prompt are one go-on; an urgent answer inside a running turn counts none (Driver/Conversations/Domain/AnswersAlone.cs:10–16,30–31,65–75). Thus qualify D199's “every answer is a go-on”: the unit is a turn. Team notes must not become Daoris prompts that bypass the third-continuation hold.

Amend D163's “nobody can ask afterwards” wording (MCP/HelperTools.cs:43–47) to distinguish a finished pass from a fresh commission. One-turn/no-resume, D155's cap and D199 admission stand.

#### X7. Names in both languages

| Concept | English | 中文 |
|---|---|---|
| umbrella | co-worker | 协作者 |
| collection | team | 团队 |
| responsible role | lead | 主导 |
| question role | helper | 助手 |
| writing role | partner | 搭档 |
| candidate-reading role | reviewer | 阅读者 |
| writing commission | piece | 分项 |
| cross-machine person | teammate | 队友 |
| harness conversation | part | 段 |
| internal harness child | subagent | 子智能体 |

Favour 协作者: activity without claiming employment (同事), another partner (伙伴), or 队友. Its human repository-collaborator usage is disambiguated by team, role and agent; access screens name people explicitly. Record that reason. Do not rename helper “reader”: 阅读者 is already reviewer (Web/locales/glossary.json:535–537). Lead/partner/piece retain D199's terms (docs/2026-10-22-coagent-design.md:482–492). Narrow the broad subagent glossary definition to harness-internal children: a D116 terminology amendment, no tool rename.

| English status | 中文 status |
|---|---|
| needs you | 等你处理 |
| queued / preparing / working | 排队中 / 准备中 / 工作中 |
| awaiting answer | 等待答复 |
| answered / waiting | 已答复 / 等待中 |
| sent / taken | 已发送 / 已接收 |
| uncertain / failed / refused | 不确定 / 失败 / 已拒绝 |
| taken in / discarded | 已并入 / 已丢弃 |
| stopped / out of time / account limited | 已停止 / 超时 / 账户受限 |

All fit 16/5. Label execution, delivery and incorporation separately; detailed box states remain facts, not fifteen head badges.

#### X8. Both doors and Ask Daoris

Use daoris-driver sessions. Keep role-specific mutations; add shared reads.

| Terminal door | Page equivalent |
|---|---|
| sessions team <lead> [--json] | Team and all exchanges |
| sessions exchange <exchange> [--json] | Commission, result, provenance, receipts, holds |
| sessions helper <lead> "<question>" [--agent ...] | Ask helper |
| sessions helper --approve/--decline <ask> | Existing approval |
| sessions follow-up <exchange> "<request>" [--agent ...] | Fresh bounded commission; returns new id |
| sessions forward-answer <exchange> | Person's Send to lead |
| sessions partner <lead> "<piece>" --agent ... | Ask admitted partner |
| Existing opinion ask/recheck/disposition | Reviewer follow-up/gate |
| Existing stop/words/go-on/hand-over | Same actions, with role refusals |

Both doors state follow-up role and destination. Reviewer requests beyond budget are refused with an explicit helper alternative, never silently converted. Forwarding refuses missing/already-settled results.

Ask Daoris may read the projection and propose the same typed session actions/settings. Its card identifies role, lead, new commission, destination and terminal equivalent; the person applies. Mentioning a member commissions nothing. It cannot approve itself, lift admission, forward autonomously or reopen a member as general chat.

#### X9. Build order and proofs



| Order | Row | Proof / prerequisite |
|---|---|---|
| 1 | COWORKUX2a: authorship/result visibility | Helper-marked event attributed in en/zh before delivery; terminal agrees; ordinary targets unchanged |
| 2 | 2b: typed member-wait | GITAPP1-shaped ask/boundary: Later, no false bell, answer kept/taken once through driven and conversation paths |
| 3 | 2c: delivery/succession | Restart before/after keep, attempt and wire; duplicate looks, two results, hand-over; no duplicate prompt or left conversation resurrected |
| 4 | 2d: team/navigation | All roles; cross-group nesting, missing/archived parent, selected child, Ended truncation; JSON/page agree |
| 5 | 2e: cards/waits | Refused/no-start included; answer survives head changes; opinion gate remains; one count per real wait |
| 6 | 2f: helper follow-up/forward | New clone/record/capture, no resume; person-only default, one claim; reading/drafting never reset bound |
| 7 | 2g: reviewer follow-up | Recheck consumes allowance; no third pass; helper explanation changes role visibly; no verdict bypass |
| 8 | 2h: partner pieces | Depends on COAGENT admission/keeper/hand-over rows; unmeasured refuses; frozen diff/import/incorporation/uncertainty agree |
| 9 | 2i: installed proof | Real helper/carry-on, open/back, approval and held third answer; en/zh narrow/wide, keyboard/focus/accessibility; names/parity |

Before 2, specify bound-question evidence and both boundaries. Test person-question, pause, stop, refusal, cap and missing answer-tool use. Refusal must settle the wait visibly without fabricating an answer.

Before 3, inspect older-reader statements: new marks do not protect against older builds ignoring them (.claude/knowledge/compatible-builds.md). Prove conservative old-park behaviour or declare mixed-version rollout unsupported; a new flag alone proves nothing.

Before 8, require deterministic confinement measurement of the whole effective configuration. Production rows are empty (Driver/Conversations/Domain/PartnerMeasurements.cs:40–44); matching is configuration-specific (:60–70). Fixtures prove presentation only.

Update reached twins, dispatcher, bridge census, waits, bell, glossary and Ask coverage. Implementation runs relevant declared checks then integration gates; this read-only proposal ran none.

#### X10. Unmeasured and excluded

Further brief corrections: RoleFilter is not a complete helper/reviewer allow-list: MCP/RoleFilter.cs:13–19 models serves/piece; :37–58 supplies partner and serving restrictions. Other posture machinery matters. “Missing way back” (§6) is too broad: §3.2 already identifies Helping's backlink; the gap is consistent team navigation/return position. The parked hand and cross-group corrections are in X4/X5.

I cannot independently verify install timestamps, empty said, current state or actual window appearance from this copy. I did not inspect the install, forbidden material or external survey links.

Unmeasured: real partner confinement/usage, provider resume/steering, follow-up cost/usability, layout, real restart/succession delivery and cap/park redesign. Static reads establish none of these.

Excluded: persistent personas, arbitrary agent chat, role escalation, partner repair by resume, native-subagent management, cross-machine team sync, meeting/quest/channel redesign, parallel tree writers, automatic disputes or landing-verdict bypass.

No model routing/summarisation is needed. Waiting cause, exchange identity/delivery and fresh follow-ups change foundations and warrant D191's foundation read. Confinement, admission, one-turn, candidate and answers-alone bounds remain.

## Fable (written by the steward's Fable agent from the brief at 2b57fe1c4, with the owner's structures direction)

Verbatim but for its headings set one level down. Written from the brief alone, plus one direction that reached the steward after the brief: co-working takes more than one structure, chosen by the task (lead and sub, peers, a relay, a panel, more than two makers). It did not read either other section.

> Written from `docs/2026-10-23-coworker-brief.md` at `2b57fe1c4` alone; the proposals document was not read. Every fact below
> was checked against the main checkout on 2026-10-23 (HEAD `f506863e7` with the working tree's uncommitted changes; line
> numbers are the working tree's, so a few differ from the brief's by a line or two). Nothing was run, no window was seen, no
> agent was started. `Dnn` is `docs/decisions/Dnn.md`; paths are under `src/` with the brief's abbreviations.

### 0. The headline

1. **A co-worker is a session record that serves another** (`serves`), one record, one turn, one role; the role is read from
   the fields the record already carries (`piece` → partner, `opinion` → reviewer, `serves` alone → helper), never stored. A
   **team** is a lead's line and every record serving any part of it. The one record change: **the reviewer's record gains
   `serves`**, which puts it in the team by the same read as the others and under the server's role filter, where today it is not.
2. **Structures are compositions of two links and one channel, not a new mechanism**: *serving* (`serves`) makes a team, *continuing*
   (`from`) makes a relay, and the ask's channel makes peers. The decision names them first-class and says which rules bind each; this
   design builds surfaces for the team alone and files the one gap peers have (parts of one entry do not hear each other).
3. **The lead waits on its co-worker, not on the person**: a driven lead that ends its turn with an open ask of its own ends
   `completed` with `ended.awaits-helper`, is held by the planner, reopens with the answer, and reaches the person only where the
   co-worker ends with none. This is TASKS.md's HELPERWAIT1 (`TASKS.md:326`), given its mechanism here.
4. **The page**: a *Team* section in the lead's head (the lead's chip, then each member with its maker mark, role, state and door;
   pending asks as members with *Let it ask* / *Not now*); each exchange a card in the lead's conversation whose body is read from the
   ask's box, so it draws the same on both doors; the member's conversation opened in the same centre as a thread with a way back;
   the nested row's line naming the role.
5. **Talking to a member is asking again**: one more one-turn record of the same agent, threaded under the same card, on a fresh copy;
   never a composer that resumes a one-turn conversation. The answer goes to the lead and to the person's card; *for me only* is a
   later row.
6. **Names**: *co-worker* / **同事**, *team* / **团队**, *lead* / 主导 (D199), and the three roles keep their glossary words (*helper* 助手,
   *partner* 搭档, *reviewer* 阅读者). 协作者 is argued against: it is git hosts' word for a human with repository rights, 协作 already
   titles *Who it worked with*, and it spends three of a status's five units before the role is said.
7. **Cost**: no model; no new poll (the team is the session list the page holds, the live state the head's `SESSION_HELPERS` read); one
   account turn per *Ask again*. **First row**: today's helper shown as a co-worker in the session's team, web and terminal, no record
   change.

### 1. Where the brief does not hold, checked against the code

1. **"Each role is enforced by the server from a scope the driver binds (`RoleFilter`)"** (brief §2) holds for a partner only. The
   filter knows two things: a partner's allow-list (`Daoris.Service.Mcp/RoleFilter.cs:19, :37-40, :71-75`) and the two quest tools
   refused to any record with `serves` (`:58, :77-81`). A helper gets the second and nothing else from the server; its other refusals
   are each tool's own judge (`HelperBox.Ask` refuses an asker with `serves`; `OpinionDesk.GiveAsync` refuses a record with no
   `opinion`, `Core/Review/OpinionDesk.cs:175-181`; the channel refuses both roles, D167's SESSONE2a note) and the handed rules. **A
   reviewer's record carries no `serves`** (`OpinionDesk.cs:128-131`: `opinion: id, ownEntry: true`), so the filter reads it as
   `SessionRole.None` (`RoleFilter.cs:114`) and the server refuses it nothing: `quest_publish` from a reviewer on a copy-alone agent is
   held off by `OpinionPermissions` alone, which Codex does not take. The connector advertises every tool class to every session
   (`Daoris.Service.Mcp/KnowledgeServer.cs:28-45`). §4.1 below closes this with the one field it needs.
2. **§4's nesting.** `nestServing` flattens every drawn group before it nests (`Web/src/work/groups.ts:309-316`) and `keepOwn` is asked
   of the *nested* row, not its parent (`Web/src/work/rail/SessionList.tsx:164-167`), so a helper in *Ended* is drawn under its lead in
   *You* where both are listed. By the code, the install's case nested on the page; what read wrong was the lead's own group and words,
   and the terminal's flat list. The window was not seen, as the brief says.
3. **The §4 failure has a row already**: HELPERWAIT1 (`TASKS.md:326`, filed 2026-10-23 after the brief's reading). §5 below is that row's
   mechanism; the design should name it rather than file a second.
4. **The card is cheaper than §3.2 implies.** The driver already marks a helper's answer on the record's event
   (`Driver/Loop/DriverHelpers.cs:439-445`, `Helper = state.Helper`; COWORK1e's mark on every door), and the box keeps the answer's
   text (`helpers/<id>.answer.json`). Only the page's `SessionEvent` lacks `helper` (`Web/src/work/conversation/conversation.ts:45-80`),
   which is why `AskView` falls through to *The target Daoris composed* (`ConversationView.tsx:386-389, 403`). MIXAGENT3a-iii
   (`TASKS.md:99`) is that half.
5. **The lead ends its turn by design.** `helper_ask`'s description tells the agent the helper "starts only then" and to end its turn
   (`Daoris.Service.Mcp/HelperTools.cs:18-26`), and `HelperJudge` holds an ask `turn` until the asker is between turns
   (`Driver/Conversations/Domain/HelperJudge.cs:58, :72`). So the §4 failure is not the helper's start but the lead's reading at that
   end (`Driver/Sessions/Domain/Observation.cs:150-158`: a clean exit with the quest taken parks it). The fix is a reading, not a timing.
6. **A partner's word carries no mark of its own**: it names the partner's record in `helper` and tells itself by its key
   (`Core/Sessions/PartnerDesk.cs:146`, `partner:<piece>`). So the page tells a word's role by the record it names, which is the team
   read anyway (§4.2); nothing needs a `partner` mark on the wire.

### 2. The model

#### 2.1 What a co-worker is

**A co-worker is a session record of this machine's that serves another session's line: `serves` names the lead, the record has its
own entry, one turn, one role, and it never writes the lead's tree.** Three roles exist, told by the record's own fields:

| Role | Told by | Writes | Record opened by | Bound |
|---|---|---|---|---|
| helper (助手) | `serves`, no `piece`, no `opinion` | nothing; reads a clone carrying the lead's work in progress | `HelperDesk.OpenAsync` (`Core/Sessions/HelperDesk.cs:99-102`) | one turn, 15 min, two per asker, depth one |
| partner (搭档) | `serves` and `piece` | a clone of its own; lands only through the lead's merge of a frozen ref | `PartnerDesk.OpenAsync` (`PartnerDesk.cs:79-82`) | one turn, 45 min, two open pieces per line, admitted by a measured row, asks nobody |
| reviewer (阅读者) | `opinion` (and, after §4.1, `serves`) | nothing; reads a candidate's commits | `OpinionDesk.AskAsync` (`OpinionDesk.cs:128-131`) | one turn, the rule's minutes, one pass and one recheck per landing |

The role is **derived, never stored**: a `role` column would be a second authoritative store for what two fields already say (D173's
guard), and every reader that exists today reads the fields.

#### 2.2 What a co-worker is not

- **A part** (D167): a record that *continues* the line (`from`). A hand-over, a move, a carry-on, a retry and a resume are the same
  work going on; they hold the tree in turn, and the page already draws them as parts. A co-worker never continues the lead and never
  holds its tree.
- **Another repository's session on the same ask** (D167 points 6-8): a peer, its own lead of its own line, met on the ask's channel. It
  serves nobody; D45 keeps it that way.
- **A meeting's participant** (D177): no tree, no record of a session's own, a protocol on a transcript. D177 point 6 says the opinion
  and the helper may one day reuse its participants; nothing here moves them.
- **A native subagent** inside the lead's own process: no record, no door, no state Daoris observes. It is the glossary's *subagent*
  (`glossary.json:581`), whose definition ("an agent a session starts to work beside it") now reads as a co-worker and must narrow to
  "inside its own process, with no record of its own" (§8). FABRIC-C1a binds it to its part later; not here.
- **Ask Daoris's conversation**: the front desk (D193 part 2). It may *propose* a co-worker; it is never one.
- **A brainstorm's pass**: no record (`Driver/Loop/DriverBrainstorms.cs`), a protocol's turn.
- **A teammate** (队友): a person on another machine (`asks.json`).

#### 2.3 One record, one turn, one role

**A member is one record.** The alternative, "one agent in one role over several records" (Codex as *the* helper, asked and asked
again in one conversation), needs a resume that is not measured for Codex (D199's rejection of a kept conversation across pieces) and
reads stale work: a reader that remembers the capture it read an hour ago answers a question about a tree that moved. So *Codex, asked
again* is a new record on a fresh capture, threaded under the first on the page (§6.3, §7). The team is therefore a list of records, each
one exchange, which is also what the terminal can print and what the records already are.

#### 2.4 The team, and what follows the lead

**The team of a session is every record whose `serves` names any part of its line.** The line is the lead's `from` chain, which the
service already walks downward for a partner's hand (`PartnerDesk.cs:114-120`, `ContinuedByAsync`) and the page holds as the entry
(`SessionEntry.parts`). Through a hand-over the team follows: the new part's strip lists the earlier parts' co-workers as history (ended,
with the part they served) and the open pieces move along `from` (D199 point 6), as built. A co-worker's own record never gains a
`from`, and is never handed over: its one turn ends or is stopped.

### 3. The structures

#### 3.1 Two links and one channel

Every structure the owner named is already expressible with what the records carry, and nothing else should be added for it:

- **serving**, `serves`: one record works *for* another's line in a role (§2);
- **continuing**, `from`: one record carries another's work *on*, with Daoris's brief (D189);
- **the ask's channel**: records on one ask, each its own lead, talk at levels (D167, D188).

A structure is a shape these make. Naming the shapes is worth a decision because each has a different answer to *who writes the tree*
and *who may start a turn*, and a person choosing a shape should see those answers; building a mechanism per shape is not, because each
shape is already one of the three.

#### 3.2 The structures, first-class

| Structure | What it is in Daoris | Who writes the tree | Shown as | The rules that bind it |
|---|---|---|---|---|
| **Team** (lead and co-workers; the owner's *lead and sub*) | a session and the records that serve its line: helpers, partners, reviewers, of any makers | the lead alone; a partner its own clone, landed only by the lead's merge | the *Team* section, the cards, the nested rows, the threads (§6) | D51; D199 points 2-4 (clone, strict job, admission, depth one, two pieces); D163 (two helpers, one turn, no words); D155 point 5 (a reviewer's copy); D188 (an answer is ordinary unless its ask was urgent; it is a go-on under the answers-alone bound); D155 point 8 (agreement establishes nothing); D137 (claims, never the person's words) |
| **Peers** (lead and lead) | two leads of two lines on one ask: two repositories (two entries), or two parts alongside in one entry of one repository (D167's 2026-10-14 amendment), each in its own tree, on the ask's channel | each its own tree, never one tree between them; the **person** is the merge point: one Accept for the ask replays each part in ledger order and names a superseded part (ACCEPTALL1) | the ask's page (*Sessions* by repository, the channel), the *Parts* strip, a session's channel panel; no new surface | D51; D167 points 6-8 (a post never starts, reopens or extends a run; work and a change elsewhere stay quests); D188 points 2 and 4 (three urgent posts of ten; answers-alone); D45 (a peer never edits the other's repository); D154/D155 at each landing |
| **Relay** (the work passed along) | a line's parts in sequence: a hand-over (D163), a move (D189), a carry-on, each `from` the last, handed the brief | one part at a time, the same tree, the old ending at its turn's end before the new starts | the *Parts* strip and flow; the brief's folded card; the closing note | D189 (the brief, bounded; never the transcript or a model's summary); D163 point 3 (`handOver` rule); D199 point 6 (pieces travel along `from`); the strikes bound a carry-on (D80) |
| **Panel** (several readers of one candidate) | several reviewers, each a co-worker giving its own opinion on the same candidate; today one pass and one recheck per landing (D155 point 10) and the walk takes the first that can run | nothing | the gate listing each opinion, each reviewer a member of the team | D155 points 4, 8 and 10 (another maker first; a dispute is the person's; three readers agreeing establish no more than one); **not built first** (§13) |
| **Many makers** | any shape above with makers mixed: helpers walk makers (`HelperChoice`), partners from the listed agents, a reviewer another maker first | as the shape | the maker marks on every member | D155 point 4 (independence is by maker), D197 (accounts), D199 point 9 (usage per maker) |

What this table settles about *lead and lead*: **two equals are two leads of two lines, never two leads of one tree.** The symmetric
reading (Claude and Codex each writing a piece for the other, each merging the other's frozen ref) is rejected (§3.4): D51 and D199
point 2 forbid two writers in one tree, a partner asks nobody (depth one), and two trees each taking the other's pieces are two diverging
lines with no one to settle them but a model, which D24 and D54 forbid. What equals share is the ask's channel and the person's one
Accept.

#### 3.3 How a task chooses a structure

Nothing chooses a structure for a task as a whole; **each door starts one shape**, and the person's rules decide what an agent's door
does:

| Who | Door | Shape | Rule |
|---|---|---|---|
| the person | ⋯ *Ask another agent…* (`acts.ts:138`), `sessions helper` | team, a helper | none: their yes is the press |
| the person | *Ask another agent to work on a piece…*, `sessions partner` (COAGENT1b) | team, a partner | admission by a measured row |
| the person | *Ask now* / *Try again* at the gate, `opinion ask` | team, a reviewer | the rule's `reviewers` |
| the person | *Go on with another agent…* (`acts.ts:137`), `sessions hand-over` | relay | none |
| the person | a second request on the ask; the intake's split; *Group by ask* | peers | D167 |
| the lead | `helper_ask` (`HelperTools.cs:17`) | team, a helper | `helpers` (`ask`/`on`/`off`) |
| the lead | `partner_ask` (COAGENT1d) | team, a partner | `partners` |
| the lead | `handover_propose` | relay | `handOver` |
| the lead | `channel_post`, `quest_publish` | peers | the channel's bounds; a quest's doors |
| a workflow | the opinion's occasions `landing` and `steps` (D155 point 2) | team, a reviewer | the repository's or workspace's rule |
| a workflow | a step that asks for a piece, or a panel | team | **later**: a declared node under D157/D190, never a model's reading |

Ask Daoris proposes any of the person's doors as a `session` card and applies none itself (D110, D183).

#### 3.4 Rejected

- **A `structure` field on a record, or a structure chooser on the page.** The shapes are read from `serves`, `from` and the ask; a
  stored word would drift from them (D173's guard), and a chooser would offer the person a decision each door already makes.
- **Symmetric partners and two leads of one tree** (§3.2).
- **A coordinator agent** that dispatches the team and merges its results: D193 rejects agents directing agents; every start here is the
  person's, the lead's under a rule, or a workflow's declared node, and every merge is the lead's own turn or the person's Accept.
- **A model choosing the shape** (D24, D54).
- **Agents forming teams past the rules**: a helper asking for a helper, a partner asking for anything, a reviewer asking for a reader. Depth
  one stands (D163 point 2, D199 point 2).
- **A chat between members.** Members hear each other only through the lead's turns and the ask's channel; a member-to-member message
  would be two agents conversing with no person between (D155, D163, D199 point 10).

### 4. The records

#### 4.1 The reviewer's record gains `serves`

`OpinionDesk.AskAsync` opens the reviewer with `opinion: id, ownEntry: true` and no `serves` (`OpinionDesk.cs:128-131`). It gains
`serves: working.Id`, walked through `session-record-fields.md`'s rows that apply to a field set by its own open: the create already
takes `serves` (row 3, `SessionStore.CreateAsync`), the column and reader exist (rows 2 and 4), the response guards it on loopback (row
8), the driver reads it (row 10) and the stand-ins echo it (row 11). What it buys, each a place that reads `serves` today and needs no
new code:

- **the team is one read**: `isCoworker = Boolean(session.serves)`, and the rail's nesting no longer needs the `GET /api/opinions` join
  (`SessionList.tsx:93-96`) to find a reviewer's parent;
- **the server binds the reviewer**: `RoleFilter` refuses it `quest_publish` and `quest_respond` as it refuses a helper (`RoleFilter.cs:58`),
  and `McpRoleFilterTests`' census gains the reviewer's row;
- **every judge that refuses a serving asker already refuses it**: `HelperDesk.OpenAsync` (`HelperDesk.cs:86`), `PartnerDesk.OpenAsync`
  (`PartnerDesk.cs:66`), `HelperJudge` (`HelperJudge.cs:63`), the hand doors (`HelperDesk.cs:146`, `PartnerDesk.cs:125`), each of which
  also refused it by `opinion`, so nothing changes in behaviour;
- **the terminal's list** can name whose work a reviewer reads without the opinions table.

What must move with it: every place that reads `serves` *as helper*. On the page: `isHelper` (`Web/src/work/identity.ts:79`) becomes
a role read (`roleOf(session)`: `'partner' | 'reviewer' | 'helper' | null`), `sessionTitle` (`identity.ts:61-63`) titles by role, the
rail's `isHelper(session) && helps` line (`SessionRow.tsx:203`), the head's *Helping* row (`SessionHead.tsx:493`), `headFacts`'
*helping* fact (`headFacts.ts:88`), `cannotGoOn`'s `'serves'` word (`sessionLife.ts:60-66`) and `offeredActs`' `wordless`
(`acts.ts:131`), which already joins helper and reviewer. In the driver: `SessionRecord.Partner` reads `serves` with `piece`
(unchanged); `PriorSession.Serves` readers that word "helper" take the role. Opinion-specific readers (`OpinionGate`, `OPINION_WAITS`)
keep reading `opinion`.

Rejected: keeping the join. It leaves the reviewer outside the one server filter, the terminal without the relation, and the team a
two-source read on the page.

#### 4.2 Nothing else changes on the record or the wire

- **No `role` column** (§2.1). **No `team` record**: a team is a query over `serves` and `from` (D167's rejection of an entry record,
  for the same reason).
- **No mark on the said word**: a helper's names its record in `helper` (`Core/Sessions/Sessions.cs:376`), a partner's the same with its
  key, a reviewer's names the opinion in `by`; the page tells the role by the record named, which the team read holds.
- **Machine-local stays**: `serves`, `piece`, `opinion` never leave the machine (`Sessions.cs:145-165`), and a teammate's mirror shows no
  team; the page says *Team: not shown for another machine's session* only where the record is a mirror.
- **The boxes are unchanged** for the first rows. *Ask again* (§7) adds one optional field to the helper's ask, `after` (the helper
  record it follows), written by the two person doors and read by the page to thread the cards; absent reads as today.

#### 4.3 The page's event gains `helper`

`SessionEvent` (`conversation.ts:45`) gains `helper?: string | null`, the record the driver already writes
(`DriverHelpers.cs:443`; the go-on's opening and the step-taken word by COWORK1e). The card (§6.3) uses it to *place* the exchange
and read its receipt; the answer's *body* comes from the ask's box through the head's read, never from the prompt's text, which on the
driven door is a whole composed instruction and on a conversation is `HelperWords.Compose`'s wrapping
(`Driver/Conversations/Domain/HelperWords.cs:17-27`).

#### 4.4 The terminal

- `daoris-driver sessions --json` parts gain `serves`, `piece` and `role` (`Driver/Sessions/SessionsCommand.cs:220`, `JsonPartFields`),
  and the plain list prints a serving record under the line it serves with its role word, as the page nests it.
- `daoris-driver sessions team <session>` prints the line's team: each member's id, role, agent and account, state, who asked and when,
  and where its answer went (handed to which part at which moment, or waiting); `--json` the same. A read, no lease.
- The role verbs stay as they are: `sessions helper`, `sessions partner` (COAGENT1b), `opinion ask|show|stop`, `sessions hand-over`. A
  person types the role; an umbrella verb would make them say it twice.

### 5. The lead's state while co-workers work

**The case** (brief §4): a driven lead ends its turn so its helper can read the work whole; `Observation` reads a clean exit with the
quest taken and last words as a park (`Observation.cs:150-158`, `ended.parked-asked`); `HelperDesk.HandAsync` refuses a parked asker
(`HelperDesk.cs:148-149`); the driver retries the hand at each look (`DriverHelpers.cs:418-421`); the answer waits unseen until the
person answers a question nobody asked.

**The rule: a lead that ended its turn on its own open ask waits on that co-worker, by name, and on the person only where the co-worker
ends with no answer or the ask's bound passes.** This is HELPERWAIT1's sentence and BGWAIT3's shape for a session's own background work.
What changes:

1. **`Observation`** (`Observation.cs:150`): before the park, `"Taken" when exitCode == 0 && Parks(…) && openAsk is { } ask` concludes
   `completed` with a new coded note `ended.awaits-helper` (`ask`, `agent`): *It ended its turn so {agent} could read its work and answer;
   it goes on when the answer comes.* The look already reads the asker's asks each tick (`HelpersAsync`), so the conclusion is handed the
   session's open asks (`waiting`, `approved`, `started`) as it is handed `backgroundCut`. The same reading for an open piece of the lead's
   own (`partner.asked`, `DriverPartners`), coded `ended.awaits-partner`, so COAGENT1d's lead reads right from its first ask. A
   conversation lead stays `working` between turns and takes the answer as its next turn (MIXAGENT2a-iii): no change there.
2. **The planner holds it.** A `completed` record with its quest taken and `ended.awaits-helper` whose ask is still open is held: no park
   row, no carry-on, no strike, as `elsewhere` holds a record (D189's SESSCTX1c note). The look fills `Snapshot.HelperAsks` from the box
   beside `Snapshot.Pieces`, so the planner reads the box the look already read and keeps no mark of its own.
3. **The answer reopens it.** `HandAsync` takes an ended record that takes words (`HelperDesk.cs:157`, the `_ => null` arm) and keeps the
   word with `Reopens: true`; the next look goes on with it (D137), one go-on under the answers-alone bound (`AnswersAlone.cs:21-24,
   :93-95`). Nothing changes in the hand or the count.
4. **No answer brings it to the person.** Where the look writes `helper.refused` (`no-answer`, `out-of-time`, `declined`, the rest of
   `HelperWhy`) against an ask the record waits on, the hold lifts and the reader places the record in *Waiting on you* with the lead's
   last words as its question and the refusal beside them: the park it would have been, read by the groups reader from the record and the
   box, with no ledger move. The person's words reach it as words to an ended record (D137 §3), which it already takes. If the reader's rule
   leaks into too many places (PARKVIEW1's answer box keys on `awaiting-person`), the alternative is one ledger move `completed →
   awaiting-person` at the refusal, said with its own note; I prefer the reader's rule because it changes no stored state.
5. **The groups and the rail**: the record sits in the group a D79 wait sits in (*Resumes later*), its line *waits for {agent}'s answer*
   (等 {{agent}} 答复), its head *Waiting for Codex, as helper* and the *Team* section showing the member working; *What needs you* lists
   nothing for it until step 4.
6. **A lead parked on the person that the person then asks a helper for** keeps its park: the answer is handed when the person answers
   (`HelperDesk.cs:148`), and the card says so, *answered · reaches the session with your answer*, with the answer readable in the member's
   thread meanwhile (§6.3). This is the one case the person reads a co-worker's answer before the lead does, and it is right: the park is
   theirs.

Measured nowhere yet: the reading rests on `Parks(…)`'s inputs and the box's states; its Process proof is §11's.

### 6. The page

#### 6.1 The list

- **Nesting stays** as MIXAGENT3a-ii built it: moved, never copied; one level; a row the person must act on keeps its place. The join for
  a reviewer's parent comes from `serves` after §4.1 and from the opinions list until then.
- **The nested line names the role**, not the kind: today a helper's row reads *chat · helps {title}* (`SessionRow.tsx:198-203`, `work.kind.chat`)
  and under a parent drops *helps* and reads *chat* alone. It reads **{role} · {state}** with the maker mark: *Cx helper · working*,
  *Cx partner · answered*, *CC reviewer · given*. The parent's row gains one meta fragment where it has any member, *{{count}} co-workers*
  ({{count}} 个同事), and the rows under it keep their list's title *Working for {title}* (为「{title}」工作, `work.rail.under`).
- **Ended members fold**: under a parent, the newest two are drawn and the rest are *{{count}} more* on one row, opened in place (survey
  §5.5: finished ones fold, working and failed ones stay visible). A working member is never folded. A member whose lead is archived or
  not drawn keeps its own place, as today.
- **The lead's own line while it waits** is §5's.

#### 6.2 The head: the *Team* section

A section under the head beside *Parts*, with `PartsStrip`'s shape (`Web/src/work/head/PartsStrip.tsx:103-112`: a molecule handed its
rows, one button each, nothing drawn where there is nothing to say). It replaces the *Helpers* section (`SessionHead.tsx:416-430`),
whose open asks become its pending rows, so the page keeps no second list.

| Row | Reads | Press |
|---|---|---|
| the lead | *Lead · Claude Code* (主导 · Claude Code), its part number where the line has parts | none (it is the page) |
| a member with a record | maker mark · *{{agent}} · helper · working* (Codex · 助手 · 工作中) · what it is doing now (the rail's `calling`, *running `git diff` for 2 min*) · what it spent where measured (`SESSION_SPENT`, as the parts strip says it) · *4 min ago* | opens its thread (§6.4) |
| a member that answered | mark · *Codex · helper · answered* · where the answer went (*taken by part 2*, *waits for the turn's end*, *held until you answer*) | opens its thread |
| a member that ended otherwise | *refused: {{why}}* / *stopped* / *out of time* in the timeline's words (`note.helper.why.*`) | opens its thread |
| a pending ask | *Codex · helper · waits for your yes* (`helperLine`'s words, `HelperAsks.tsx:86-107`) | *Let it ask* · *Not now* (as today; still a wait, still `data-wait-section`) |
| a piece (after COAGENT1b) | *Codex · partner · working* · its state in D199 §6.1's words | opens its thread; *Stop…*, *Discard…* as D199 §6.1 has them |
| a reviewer (after §4.1) | *Codex · reviewer · reading* / *given* / *answered* | opens its thread; the gate stays where it is |

The members are the records the page's session list holds with `serves` naming any part of this line, plus the open asks from
`SESSION_HELPERS`; the maker mark is the roster's product and maker by adapter, two letters (*CC*, *Cx*, a plugin's declared mark; D199
§6.1), never a hue alone. A line with no member and no ask draws nothing. The section's budget is a `section` (32/12): *Team* / 团队.

#### 6.3 The lead's conversation: a card per exchange

Each exchange is one card, open, in the hue of another agent's words (the findings' `border-l-line-strong`, `ConversationView.tsx:395`),
never the person's accent:

- **Placed** at the ask (the `helper.asked` note, or the lead's `helper_ask` tool call, by the ask's id) and updated in place; the answer's
  `user` event with `helper` (§4.3) gives the moment the lead took it and its receipts (COWORK1c's *sent*, *taken*, *answered*).
- **Its head**: *Codex, as helper* (Codex，作为助手) with the mark, then the state: *reading your work as of 16:29 · running `rg` · 4 min* while it
  works, *answered · taken as the next turn* when done, *refused: it ran out of time* otherwise.
- **Its body**: the question as written (content, as it is), then the answer as written, read from the ask's box through the head's read
  (`SESSION_HELPERS` gains the answer's text, bounded as `ParkedWords` bounds last words, with *Show all* past it), under the fixed line
  *a claim to check, not your words* (`HelperWords.cs:25`'s sentence, on the page in both languages). A last-words answer says so.
- **Its door**: *Open conversation* (打开对话), which opens the thread (§6.4).
- **A partner's card** (COAGENT1b): D199 §6.1's row, *A partner's answer: claims to check*, the files, those beyond the piece, the merge line.
- **A reviewer's card**: the findings block as XAGENT1g draws it, retitled *A reviewer's findings* (阅读者的发现) from *Another agent's
  findings* (the key `work.conversation.opinion` stays, its value moves), with *Open conversation*.
- **The folded *The target Daoris composed*** is never drawn for an event carrying `helper` or `opinion`; it stays for the composed instruction
  itself.

Survey §5.5's lesson this keeps: a link from the parent into the child, the parent short; never the child's transcript inline.

#### 6.4 The member's conversation: a thread in the same surface

A member's page exists today (`WorkFrame.tsx:494`, `oneTurn`: no composer; `sessionLife.ts:60-66`: no going on). What is missing is the way
in and back, and the words at its head. The thread is the same `WorkFrame` attending the member's record, with three additions:

- **A thread head** above its own head: *Working for {title}* (为「{title}」工作) with the lead's state dot, the member's role and mark, and
  one press, *Back to lead* (返回主导, a `button`, 12/4), which attends the lead again at the card the thread was opened from (the frame
  keeps `sought = helper:<ask>` as `HelperAsks` already marks it, `SessionHead.tsx:427`). The browser's back does the same, since attending
  is a route.
- **While it works**: its conversation as any session's (its tool calls, its messages), the rail's call line, *Stop…* in its head (the
  person's only press on a running member), and nothing to type into: the foot says *a helper takes one turn; ask it again from the lead's
  conversation once it has answered* (`work.life.cannot.serves`'s sentence, extended).
- **When it ended**: its answer at the end of its conversation, then one line *Its answer went to {lead} as its next turn at 16:45* / *waits
  for {lead}'s turn to end* / *waits until you answer {lead}* (the receipt from the box's `handed` and the lead's word), and the foot's
  *Ask again…* (§7).
- **Its own *Details*** keeps *Helping* → renamed *Works for* (为……工作), the lead's title as the door (`SessionHead.tsx:493`).
- **In the rail**, attending a member selects it under its lead, as today (`SessionList.tsx:154` marks the entry attended while any part is;
  a member is not a part, so its own row is marked).

A detached window (UX7c) shows the thread head with the lead's id and no door, as the *Details* row does.

#### 6.5 What needs you

No new kind. The helper's yes stays the `helper` wait (`waits.ts:33`, `NeedsAPerson.cs:296-303`, `attention.ts:723-727`) worded by role
(*Codex asks to bring in a helper*); the partner's yes is COAGENT1d's row of the same shape; the answers-alone hold stays `answerAlone`. §5
step 4 adds no kind either: a lead whose co-worker ended with no answer is the park row the record would have had. A member that is
*uncertain* after a restart is D199's `piece is uncertain` row.

### 7. Talking to a member

"Within its role's bounds" means, for a one-turn record that refuses words (`SessionSayRefusal.Helper`), that **talking is asking again**:

| Role | While it works | Once it answered | Where the answer goes |
|---|---|---|---|
| helper | *Stop…* (its last words stand where the turn completed; else refused `no-answer`) | *Ask again…*: a new ask of the same agent, the question prefilled with the member's answer quoted and the person's words under it, `after` naming the member; a new record on a fresh capture of the lead's work, threaded under the first card | to the lead as its next turn (or next step where asked urgent), and to the person's card |
| partner | *Stop…* (work kept and imported, D199 §2.3 step 11), *Discard…* | *Ask again…*: a new piece from the lead's current base, the earlier answer quoted | to the lead's line as a frozen ref, and the card |
| reviewer | *Stop…* | *Ask again* is D155's recheck or the person's ask occasion; disputes are answered at the gate | to the lead as findings, and the gate |

The bounds that hold it:

- **One turn, one record**: never a composer that sends a second prompt into the member's conversation. Resume is unmeasured for Codex
  (SESSCTX1a) and rejected across pieces (D199); a steer (STEER3) is not built; and a remembered reader answers about work that moved.
- **The person's ask is their word**: `AnswersAlone.Resets` counts it (`AnswersAlone.cs:89-91`, the person's `user` event), so a line the
  person keeps asking on is never held as answers alone; the lead's own `helper_ask` is not their word and counts as today.
- **Two helpers at once per asker** (`HelperJudge.cs:52`) counts the follow-up; the person's terminal verb was always theirs
  (`HelperJudge.cs:65-69`), so no rule holds it.
- **The lead is always told**: a follow-up's answer reaches the lead as any helper's does. ***For me only*** (an answer kept on the member's
  record and never handed) is a later row (§13): it needs `to: person` on the ask, the hand skipped, and a word for an answer the lead never
  saw. The first version keeps D163's one path.
- **Nobody but the person sends *now*** and no level stops a member; a member's stop is the person's press (D188 point 1).

### 8. The names

| Term | English | 中文 | Kind, budget | Why |
|---|---|---|---|---|
| **co-worker** (new) | co-worker | **同事** | the umbrella; a `status`-sized word on chips (16/5) | *co-worker* is the steward's word and says *works beside*; 同事 is the plain company word (D193: the staff are agents in one department), two characters, in no catalogue today. The owner's own English was *colleague*; 同事 is its name. |
| **team** (new) | team | **团队** | `section` 32/12 | the section's name; Chinese coverage of agent teams says 智能体团队, and the window says 团队 beside the lead's chip |
| lead (D199 §6.6) | lead | 主导 | `status` | kept; 牵头 is the verb where a sentence needs one |
| helper (`glossary.json:537`) | helper | 助手 | kept | the role that answers one question. Not renamed *reader*: 阅读者 is the reviewer's word and 读者 is a book's reader |
| partner (D199) | partner | 搭档 | kept | writes one piece |
| piece (D199) | piece | 分项 | kept | |
| reviewer (`:536`) | reviewer | 阅读者 | kept | |
| maker | maker | 厂商 | kept | the mark beside every member |
| subagent (`:581`) | subagent | 子智能体 | narrowed | *an agent a session starts inside its own process, with no record of its own*; a co-worker has a record. Its `avoid` keeps 子代理 |

**Against 协作者.** (1) On Chinese git hosts 协作者 is a *collaborator*: a person granted rights in a repository (brief §5.6). A co-worker has
no rights in the lead's tree, and the one word a Chinese developer already reads as "someone who may write here" is the wrong first
impression for a reader that may not. (2) 协作 already titles *Who it worked with* (协作关系, `work.relations.json`) for the quests between
repositories: one word, two things. (3) Three characters spend three of a status word's five units before the role or the state is said;
协作者·助手 is five and leaves no room for 工作中. 同事·助手·工作中 fits a chip. (4) It names a role of a person, as every word here does
(助手, 搭档, 阅读者), and the maker mark beside it says which agent; that objection, if it held, would hold against the existing terms too.

**Against the alternatives.** 队友 is a person on another machine (`asks.json:145-152`). 伙伴 is Feishu's 智能伙伴, a product's marketing word,
and reads as *buddy*. 子智能体 says *sub*, which a peer and a reviewer are not. In English, *colleague* is a standing, *co-worker* an act;
*teammate* is taken; *member* is the word for one co-worker inside a sentence about the team and needs no term.

**States**, as `status` words (16/5), reusing what exists: *waits for your yes* 等你同意 (`work.helper.state.yours`), *working* 工作中,
*answered* 已答复, *given* 已给出 (a reviewer), *refused* 已拒绝, *stopped* 已停止, *out of time* 超时 (D199 §6.1), *taken in* 已并入.

**Words that move** (keys stay, values move, both languages, `names:check --strict` and `i18n:check`): `work.helper.heading` *Helpers* →
*Team* 团队; `work.conversation.opinion` → *A reviewer's findings* 阅读者的发现; `work.head.helping` → *Works for* 为……工作;
`work.life.cannot.serves` → *a co-worker takes one turn* 同事只答一轮 (the helper's and the reviewer's one sentence); `work.identity.helper`
stays for the role's title, with `work.identity.partner` and `opinion.session.title` beside it. New: `work.team.*` for the section, the
chips, the thread head and *Back to lead*; `work.conversation.helper` for the card; `note.ended.awaits-helper` and `.awaits-partner`.

### 9. Both doors, and Ask Daoris

- **The terminal** (§4.4): `sessions team`, the JSON fields, `sessions helper <lead> "<q>" --after <helper>` for *Ask again*, and the list's
  nesting. The person's presses `--approve|--decline` stay.
- **Ask Daoris** may propose a `session` card for each of the person's doors (*ask Codex as a helper about …*, *ask again after …*), the
  terminal's equivalent beside it; the person applies. It never asks for a co-worker itself (D110, D183). `useAskHelper` keeps its
  exemption in `HelpCoverageTests` (the person's own ask); the *Ask again* hook is exempt the same way; `sessions team` is a read.
- **The settings doors** (`helpers`, `partners`, `opinions`) are unchanged; the Agents page's *In a shared session* rows are COAGENT1e's.

### 10. The cost

- **No model.** Every word is Daoris's fixed words or the agent's as written; the tier is *none* (D24).
- **No new poll.** The team is the session list the page already holds (`serves` on each record); the pending asks and live states are the
  head's existing `SESSION_HELPERS` read for the attended session; a member's call line is the rail's `calling`; spend is `SESSION_SPENT`,
  read once for the strip. §4.1 removes one read (`GET /api/opinions`) from the rail's nesting. The terminal's `sessions team` is one
  `/api/sessions` read and the box's files.
- **Accounts.** *Ask again* spends one more one-turn session on the member's agent within the helper's 15 minutes; the ask's dialog says
  which account the walk would take, as `AskHelper` does. Nothing here starts a turn by itself.
- **Build.** Row 1 is the largest page row (a section, a card, a row line, a verb, two glossary terms) and touches no stored shape;
  rows 2 and 4 are the two that touch the driver's reading and the record; the rest are small.

### 11. The test plan

Fast halves, by row, in both languages where words are drawn:

- **Row 1**: `identity.test.ts` (roles from the fields; a record with `serves` and `piece` is a partner, with `opinion` a reviewer),
  `groups.test.ts` (nesting with three roles; the fold of ended members; a working member never folded), `TeamSection.test.tsx` (the lead's
  chip, a member per state, a pending ask with its two presses, the maker mark, nothing drawn for a line alone), `ConversationView.helper.test.tsx`
  (the card placed by the ask, updated by the answer's event, its body from the box, last words said, the door), `SessionRow.coworker.test.tsx`
  (the role line, the parent's count), stories per state; `SessionsCommandTests` (`team`, the JSON fields); `glossary.test.ts`, `names:check
  -- --strict`, `i18n:check`.
- **Row 2 (HELPERWAIT1)**: `ObservationTests` (a clean taken exit with an open ask reads `awaits-helper`; with none, the park as before; an
  open piece the same), `SessionMessagesPlanTests` (held while the ask is open: no carry-on, no park, no strike; released at `answered` and
  gone on with the word; placed for the person at a refusal), `SessionGroupsTests` (the group and the line), `AttentionStateSliceTests` (no
  row while held; a row at the refusal), `DriverModuleSessionsTests` for the wire. Process: `HelperProcessTests` gains one real tick: a
  stand-in asker ends its turn after `helper_ask`, reads as waiting on its helper, and goes on when the stand-in helper answers; a second
  case where the helper ends unanswered brings it to the person.
- **Row 3**: `WorkFrame.thread.test.tsx` (into a member and back to the card; the detached window's head), `sessionLife` (no going on,
  the extended sentence).
- **Row 4**: `SessionStoreTests` (a reviewer's `serves` written and read; null for a record from before and for a mirror), `OpinionHostTests`
  (answered on loopback only), `McpRoleFilterTests` (a reviewer refused the two quest tools; the census row), `SessionLedgerTests`,
  `HandOverTests` and the three stand-ins, the page's nesting without the opinions join.
- **Row 5**: `HelperFilesTests` and `HelperBoxShapeTests` (the twin shape with `after`), `SessionsHelperCommandTests` (`--after`),
  `HelpCoverageTests` (the hook exempt), `AnswersAloneTests` (the person's follow-up resets the count), the dialog in both languages.
- **The install proof** (the parent, after the republish, with the owner's yes where an account is spent): GITAPP1's case again, a Claude
  Code session asking Codex as a helper and ending its turn: the window shows the lead *waiting for Codex*, Codex under it in *Team* and in
  the rail as *helper · working*, the card filling in, the lead going on with the answer; then *Ask again…* from the card; shots at 888 wide
  in both themes and languages (UX5's sizing), and the terminal's `sessions team` beside them. The chips' and the thread head's budgets are
  the window's to judge, as D116 §3 has it.

### 12. The rows, in build order

1. **COWORKUX1a — today's helper as a co-worker in the session's team** (web, modules, driver-terminal; no stored shape changes).
   The glossary terms *co-worker* 同事 and *team* 团队, `subagent` narrowed; `SessionEvent.helper`; the *Team* section replacing *Helpers*,
   with the pending asks' presses kept; the card per exchange with its body from the box (`SESSION_HELPERS` gains the answer's text);
   the nested row's role line and the parent's count; `sessions --json`'s fields and `sessions team`. Proof: §11 row 1, the stories, the
   install's shots. *This is the row the brief asks for first.*
2. **COWORKUX1b — HELPERWAIT1** (driver, modules, web; `TASKS.md:326` closes into it). §5 whole: `ended.awaits-helper` and
   `.awaits-partner`, the planner's hold from the box, the refusal's placing, the group and the words. Proof: §11 row 2, with the Process
   tick. *Before any further surface, since every "live state" on the page is wrong until the lead's own state is right (brief §6.3).*
3. **COWORKUX1c — the member's conversation as a thread** (web). The thread head, *Back to lead*, the foot's receipt and sentence, the fold
   of ended members in the rail. Proof: §11 row 3.
4. **COWORKUX1d — the reviewer joins the team** (service, driver, modules, web; amends D155's record note). `serves` on the reviewer's
   record through `session-record-fields.md`'s rows, `RoleFilter`'s census, the nesting without the join, the findings card retitled, the
   gate unchanged. Proof: §11 row 4.
5. **COWORKUX1e — *Ask again*** (web, modules, driver, Ask Daoris coverage). The ask's `after`, the dialog prefilled, the terminal's
   `--after`, the threading under the card, the count's reset by the person's word. Proof: §11 row 5.
6. **COWORKUX1f — the partner as a member** (web; after COAGENT1b's Pieces section). The Pieces rows drawn as *Team* rows with D199
   §6.1's states and acts, the piece's card, `sessions team` listing pieces, `.awaits-partner` read on the lead. Proof: the Pieces tests
   moved under the section, both languages.
7. **COWORKUX1g — the decision** (documents; written with row 1, since the names enter the glossary there). The structures named with
   their rules (§3.2) and how each is chosen (§3.3); D199 §6.6 amended with *co-worker* and *team*; D163 and D155 notes pointing here; the
   peers' gap filed as its own row, **CHANPART1**: a message on an ask's channel reaches every live part of its entry but the poster, where
   today one part claims it per entry (D167's SESSONE2b note, "a message posted by a part of the same entry is not handed to the entry";
   COWORK1d's one claim per entry), with D188's acknowledgments made per part.

Each row is one feature a person can see working (D187); a split is by sub-feature only.

### 13. What I would not build first

- **A panel of reviewers.** D155 caps a landing at one pass and one recheck and the walk takes one reviewer; a panel multiplies readings
  whose agreement establishes nothing (point 8) and multiplies disputes for the person. It is first-class in the decision and built when a
  repository asks for it, as N opinions on one candidate with one gate.
- **Peers inside one entry hearing each other** (CHANPART1). It changes D188's delivery by identity and is the one real mechanism among the
  structures; across repositories peers already work.
- ***For me only*** answers (§7).
- **A composer on a member's page**, a resumed member, a steered member (STEER3, SESSCTX1a first).
- **A structure chooser**, a `structure` field, a coordinator (§3.4).
- **Member-to-member messages** (§3.4).
- **A workflow node that asks for a piece or a panel** (D157/D190, after COAGENT1d).
- **The Agents page's co-worker facts** (COAGENT1e's sheet).
- **A team on a teammate's mirror**: `serves` is machine-local by D47 §4; the mirror says so and shows none.

### 14. What stays unmeasured, and what this does not cover

- No window was seen: the chips' budgets, the thread head on a narrow side bar, the fold's reading and the card's length are the
  install's to judge (D116 §3).
- §5's reading rests on `Parks(…)`'s inputs and the box's states as read, not run; whether the planner's hold needs a mark of its own
  after a restart (the box is on disk, so I expect not) is the Process tick's to show.
- What a real lead does with a follow-up's answer, and whether Codex's `helper_answer` joins its own record (D199's M1), are the install's.
- The token cost of a member's turn (M3) and what codex-acp's counts mean (M4) are COAGENT1a's measurements; the strip says *not measured*
  until then.
- Peers' landing in one entry is ACCEPTALL1's, proved with git; nothing here touches it.
- A meeting's participants (D177) and a brainstorm's passes are outside the model on purpose.

### 15. What this amends, once accepted

- **D199 §6.6**: the glossary gains *co-worker* 同事 and *team* 团队; *Partners: Codex, 1 piece working* in the head becomes a *Team* row;
  *Working for {{title}}* stays as the thread's title. Nothing in points 1-10 moves.
- **D163 point 4** ("the view says who worked"): the head's *Helpers* is the *Team*; the answer is a card, not a folded target; point 2's
  "its answer returning as the asker's next turn" stands, with §5's reading of the asker's end.
- **D155 point 11**: the reviewer's record gains `serves`, this machine's own as `opinion` is; the server's role filter binds it.
- **D167**: nothing moves; CHANPART1 is filed against its SESSONE2b note.
- **D188**: nothing moves; a co-worker's answer stays an ordinary solicited answer under point 4's bound, and the person's *Ask again* is
  their word.
- **D51, D137, D177, D189, D193**: stand. D193's rejection of a hierarchy of agents is what §3.4 rejects a coordinator by.
