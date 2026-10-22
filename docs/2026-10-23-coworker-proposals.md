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
