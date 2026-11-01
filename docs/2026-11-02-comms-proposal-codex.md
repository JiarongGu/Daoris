# COMMS1 — Codex's proposal: communication is an exchange with an owner, a recipient and an expected outcome

Status: sealed proposal, 2026-11-02, from `docs/2026-11-02-comms-brief.md`; Codex, read-only, kept as it came. Evidence is from
repository revision `e0616aa57db6d12095b5bb1a147c512283515bdb`.

Daoris should describe communication through three acts: **say**, **request** and **hand over**. Each names who sent it, whom it addresses, what record owns it, and what—if anything—would answer it. Delivery and fulfillment are separate facts.

Quests, helper questions, pieces, opinions and approvals remain distinct protocols because they authorize different work and require different proof. They share identities, addressing, receipts and continuation rules. The co-working view reads these protocols together; it owns none of them.

This proposal uses the working tree as evidence. No files were changed and no checks were run.

## 1. Census

“Available” below means implemented machinery, subject to its admission rules. Accepted designs whose production door is absent are identified separately.

| Movement today | Sender → recipient; door | Owning record | Delivery, answer and presentation | Evidence |
|---|---|---|---|---|
| The person’s words to a session | Person → running, parked or ended session; composer, say door, terminal | Session’s `said`; conversation events after delivery | Durable keeping precedes dispatch. Level and provider capability determine when words enter. Words can reopen an eligible ended record. Queue and receipts appear beside the composer. | `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/SaidDoor.cs:32`; `src/Daoris.Web/src/work/conversation/SessionBox.tsx:17` |
| An ask’s channel message | Session or person → everyone on the ask, or one repository; `channel_post`, channel UI, terminal | Channel message and acknowledgments | Agent claims ride existing work; they create no turn. An addressed person’s message may make one. Shown on the ask and in the session’s folded channel. | `src/Daoris.Service/Daoris.Service.Mcp/ChannelTools.cs:18`; `src/Daoris.Desktop/Daoris.Desktop.Driver/WorkOrders/ChannelReach.cs:56`; `src/Daoris.Web/src/work/AttendedSession.tsx:341` |
| A channel question and reply | Session → channel audience; `question`, then `re` | Channel message | Optional question; sender carries on. A repository’s correlated reply produces its `answered` receipt. This is not a work dependency. | `src/Daoris.Service/Daoris.Service.Mcp/ChannelTools.cs:30`; `src/Daoris.Service/Daoris.Service.Core/Asks/Channel.cs:964` |
| Notice that messages wait | Connector → its session; footer on another tool’s answer | Channel remains the store; connector remembers which notices it gave | Pull hint, once per newly noticed message in that connector process. It delivers no message and starts no turn. | `src/Daoris.Service/Daoris.Service.Mcp/WaitingFilter.cs:21` |
| An ask becoming repository work | Person → intake → owning repositories; ask composer/terminal and quest publication | Ask, intake session, published quests | Intake interprets the target; deterministic fallback states its tier. The person’s words and files accompany work under their existing boundaries. | `docs/decisions/D65.md:9`; `docs/decisions/D65.md:45` |
| Work requested from another repository | Session, person or intake → repository, optionally lanes; `quest_publish` | Quest and operation history | Receiver takes or declines; its local driver admits execution. Done/ready/decline carries the result. Quest page, session relations and routing view expose it. | `src/Daoris.Service/Daoris.Service.Core/Quests/QuestExchange.cs:559`; `src/Daoris.Service/Daoris.Service.Core/Quests/QuestExchange.cs:1364` |
| Work requested from the same repository | Session → its own declared lane | Same quest protocol; executing record joins the appropriate entry | Whole-repository self-address is refused. Lane work may run alongside in another tree. Its code joins through the session’s landing, not through a message. | `src/Daoris.Service/Daoris.Service.Core/Quests/QuestExchange.cs:572`; `docs/2026-10-24-cowork-view-design.md:949` |
| A quest waiting for an answer | Taken quest → another quest; `quest_respond wait on` | Waiting quest’s `Awaits` | Sender ends its turn. Driver resumes after the dependency closes without a hold. The resumed instruction carries the answer. | `src/Daoris.Service/Daoris.Service.Core/Quests/QuestExchange.cs:1554`; `src/Daoris.Desktop/Daoris.Desktop.Driver/WorkOrders/Domain/Planner.cs:1157` |
| A workflow requesting work | Workflow request node → another repository | Node attempt and keyed quest | Publish retries use the same request key. Quest taking or completion satisfies the configured condition; decline fails it; absent projection is unknown. | `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/RequestNode.cs:4` |
| A helper question | Lead or person → another agent reading this repository; `helper_ask` or person’s door | Helper ask/state/answer files; helper record with `serves` | Starts at the lead’s turn boundary, reads a captured copy, answers once. Answer returns through the hand door to the line’s successor, ordinarily or at requested urgency. | `src/Daoris.Service/Daoris.Service.Core/Sessions/HelperBox.cs:55`; `src/Daoris.Service/Daoris.Service.Core/Sessions/HelperDesk.cs:149` |
| A partner’s piece | Person → partner; partner returns through `partner_answer` | Piece ask/state, partner record, frozen commit | Separate clone and measured confinement required. Lead takes the commit into its tree. **Production admission currently has no measurement rows; the agent’s asking door is not implemented here.** | `src/Daoris.Service/Daoris.Service.Mcp/PartnerTools.cs:17`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Domain/PartnerMeasurements.cs:40`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/DriverPartners.cs:47` |
| A second opinion | Workflow occasion or person → reviewer; findings → working session; disposition → opinion | Opinion, candidate and reviewer record | One bounded reading, ordinary findings, working session’s answers, then bounded recheck. Disputes remain for the person’s gate. | `src/Daoris.Service/Daoris.Service.Core/Review/OpinionDesk.cs:128`; `src/Daoris.Service/Daoris.Service.Core/Review/OpinionDesk.cs:160`; `docs/decisions/D155.md:48` |
| Native child work | Root agent → harness child; native tools | Harness conversation and recorded lineage | Harness controls dispatch and return. Daoris observes identities, announcements and calls; it has no general independent child mailbox or resumable child session. | `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/ChildLineage.cs:7`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Domain/ChildBindings.cs:8` |
| A question to the person | Driven session → person; `question_ask`, park and answer | Current question file, parked session and answer words | Structured choices, recommendation and owner-only marks. Person answers in the session box/head; park resumes under existing rules. | `src/Daoris.Service/Daoris.Service.Core/Sessions/QuestionBox.cs:70`; `src/Daoris.Web/src/work/conversation/SessionBox.tsx:177` |
| A go-ahead, permission or rule proposal | Session/tool → person; respective typed door | Ask’s go-aheads, permission request or rule proposal | Each has its own authority and answer. They share the session waits presentation and attention judge; a general question is not a substitute for a go-ahead. | `docs/decisions/D135.md:62`; `src/Daoris.Web/src/work/waits/waits.ts:26`; `src/Daoris.Desktop/Daoris.Desktop.Modules/Loop/NeedsAPerson.cs:453` |
| Hand-over or account/agent continuation | Person, agent under policy, or driver → successor executor | Hand-over request, move record, `from`, waiting words and context brief | Old executor yields at its boundary; successor resumes or receives portable context. This changes who continues the line, not who owns the repository. | `docs/decisions/D189.md:18`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/MoveStore.cs:229`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Context/Domain/ContextBrief.cs:69` |
| Handback | Executing session → quest’s readers; `quest_respond` close and `handback_read` | Closing quest operation | Result kept at the act, with writer and requirement answers. Read whole by quest or session. It is not reconstructed from conversation text. | `src/Daoris.Service/Daoris.Service.Core/Quests/QuestExchange.cs:1405`; `src/Daoris.Service/Daoris.Service.Core/Quests/QuestHandbacks.cs:7` |
| Ask Daoris conversation and proposal | Person ↔ Ask Daoris; agent → person’s typed Apply door | Local help conversation; proposal record | Conversation history remains local. Proposal changes are judged through their domain doors; the proposal box applies nothing. Configured access modes remain separate. | `docs/decisions/D158.md:9`; `src/Daoris.Service/Daoris.Service.Core/Help/HelpProposalBox.cs:22`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Help/HelpProposals.cs:13` |
| Brainstorm | Person → bounded independent passes → synthesis/challenge | Request, frozen packet, pass answers and document | Protocol barriers preserve independence. Structured results are validated; output authorizes no work. | `docs/decisions/D176.md:12`; `src/Daoris.Desktop/Daoris.Desktop.Driver/WorkOrders/BrainstormFiles.cs:37` |
| Meeting | Person → named participants through granted turns | Meeting packets, turns and transcript | Current implementation holds a round’s answers until the next approved packet. **Real-agent turns remain unavailable without a message tool.** | `src/Daoris.Desktop/Daoris.Desktop.Driver/WorkOrders/MeetingFiles.cs:129`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/DriverMeetings.cs:19` |
| Supervisor answer | Recorded person-question → appointed holder → offered choice | Supervisor decision and conditionally released word | Appointment and generation authorize the act. Deferral, overrule and restart have explicit outcomes. Its answer remains attributed to the role. | `src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/SessionsRoutes.cs:610`; `docs/decisions/D208.md:71` |
| Steward request and act | Working caller → steward desk; holder → exact management route | Desk request in run memory; admitted role act | Filing is a claim and grants nothing. Only the holder’s separately judged act can answer it. Desk bounds are ten open per caller and one hundred overall. | `src/Daoris.Desktop/Daoris.Desktop.Modules/Platform/StewardDesk.cs:106`; `src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/SessionsRoutes.cs:655` |
| Facts and cross-machine exchange | Record owner → projections/readers; permitted records → shared host | Domain records, journal, session/quest/channel wire | State, events and notification delivery are distinct projections. Machine paths, private runtime context and authority do not travel. | `docs/decisions/D212.md:109`; `src/Daoris.Service/Daoris.Service.Core/Sessions/SessionWire.cs:10`; `src/Daoris.Service/Daoris.Service.Core/Asks/ChannelWire.cs:16` |

### Where the mechanisms disagree

1. **Addressing is usually a repository when the intended reader is a participant.** The channel excludes every message from the recipient’s own entry. Consequently, alongside parts of one entry cannot use it to hear one another. Its delivery claim is also one part per entry, not one receipt per explicitly addressed part.  
   Evidence: `src/Daoris.Service/Daoris.Service.Core/Asks/Channel.cs:1056`; `src/Daoris.Desktop/Daoris.Desktop.Driver/WorkOrders/ChannelReach.cs:66`.

2. **“Answered” describes different things.** D188 permits a turn ending to count as answered; the channel requires a correlated repository reply; a piece also needs observed integration; an opinion has findings, dispositions and disputes. Those facts cannot be one progress state.  
   Evidence: `docs/decisions/D188.md:55`; `src/Daoris.Service/Daoris.Service.Core/Asks/Channel.cs:972`; `docs/decisions/D199.md:28`; `docs/decisions/D155.md:54`.

3. **Waiting uses different resource rules for good reasons, but the explanation is fragmented.** A helper wait holds its tree and releases its running slot. A quest wait does not reserve the tree. Neither is a question for the person.  
   Evidence: `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Domain/HelperWaits.cs:28`; `docs/2026-10-24-cowork-view-design.md:2698`.

4. **The continuation bound misses quest answers.** The implemented predicate recognizes helper, opinion and supervisor words; Round 11 explicitly leaves D79 resumes outside the bound.  
   Evidence: `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Domain/AnswersAlone.cs:28`; `docs/2026-10-24-cowork-view-design.md:522`.

5. **Some accepted cases lack a usable door.** Partner confinement has no production measurements; meetings lack their production result tool; D203’s complete exchange/thread presentation is parked. A unified view must say unavailable rather than draw these as working capabilities.  
   Evidence: `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Domain/PartnerMeasurements.cs:44`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/DriverMeetings.cs:26`; `docs/2026-10-24-cowork-view-design.md:518`.

6. **Portable intent and executable authority have different lifetimes.** A quest outcome travels; a helper answer is local; an appointment expires at restart; the current steward desk is memory-only. One “inbox survived” statement would conceal those differences.  
   Evidence: `src/Daoris.Service/Daoris.Service.Core/Quests/QuestHandbacks.cs:7`; `src/Daoris.Service/Daoris.Service.Core/Sessions/HelperBox.cs:17`; `docs/decisions/D208.md:55`; `src/Daoris.Desktop/Daoris.Desktop.Modules/Platform/StewardDesk.cs:148`.

## 2. The model

### Three acts

| Act | Meaning | Expected return | Records it keeps |
|---|---|---|---|
| **Say** | Make words or a fact available to an audience | None | Session words, channel messages, meeting messages or coded record events |
| **Request** | Ask a named recipient for a typed outcome | Reply, decision, work result, piece or opinion | The existing domain request and its result; an optional channel question is a request with no continuation permission |
| **Hand over** | Continue this line through another executor | A successor admitted and the transfer settled | Existing move record, continuation links, context and word settlements |

A reply names its request. A work result names its work request. Neither creates another request implicitly.

A handback is a quest’s result. A helper answer is a reading’s result. A frozen piece is a piece request’s result. An opinion is a candidate-bound reading’s result. These remain different result types.

### One shared reading, not one universal work table

Introduce a normalized `Communication` reading:

```ts
type Communication = {
  ref: SourceRef;                 // owner, origin, id, revision/operation
  scope: ScopeRef;
  sender: ParticipantRef;
  recipient: Address;
  act: 'say' | 'request' | 'handover';
  expects: 'nothing' | 'reply' | 'decision' | 'work' | 'piece' | 'opinion' | 'successor';
  replyTo?: SourceRef;
  body: SourceRef;                // read from its owner
  delivery: RecipientDelivery[];
  outcome: Outcome;
  waiting: Waiting;
  availableActs: ActRef[];        // offered by the domain judge
};
```

This is a read over authoritative sources. It stores no team, inferred conversation, duplicated result or permission.

Two additions need records because today’s sources lack the required facts:

- **Explicitly addressed messages:** a versioned channel-message family containing the audience and delivery attempts.
- **Versioned person-questions:** immutable question revisions and correlated answers, replacing new writes to the mutable question file.

Continuation admission also needs a durable control record, described below. It owns permission to continue, not communication content.

D212 already establishes participants and work as readings, and separates the desktop store from the service’s records. Preserve that division.  
Evidence: `docs/decisions/D212.md:45`; `docs/decisions/D212.md:152`.

### Identity and authorship

A source reference is namespaced by its owner and origin. Existing IDs remain unchanged. A quest result additionally names the applied closing operation; a question answer names the exact question revision.

Distinguish:

- **Part:** one executor record.
- **Line:** the continuation lineage through `from` and its existing service rules.
- **Entry:** the repository’s grouped work, which may contain alongside lines.

An entry is not a safe substitute for a reply address.

Authorship comes from the bound caller or person’s door. A supplied `from`, role name, quoted instruction or author field grants nothing. Passing an agent’s answer remains an attributed pass by the person; it does not turn the answer into the person’s authored words.

The current partner hand already uses a stable piece key while carrying the legacy helper field. The normalized reading must identify it through its piece source, not display it as a helper.  
Evidence: `src/Daoris.Service/Daoris.Service.Core/Sessions/PartnerDesk.cs:172`; `src/Daoris.Service/Daoris.Service.Core/Sessions/Sessions.cs:392`.

### Addresses and authority

| Address | What may address it | Resolution |
|---|---|---|
| **Person** | Existing person-question, approval and proposal protocols | Current machine’s answering surface; no automatic forwarding to another account or machine |
| **Participant** | Person’s permitted door; passive messages from admitted peers; a protocol’s result | Exact part. No automatic retargeting to its colleagues |
| **Line** | Person’s words and results belonging to that line | Resolve its current local head at keeping and again before dispatch; hold on ambiguity |
| **Entry** | Ask-scoped information intended for the repository’s grouped work | Explicit audience rule: one executor or named executors, never an accidental broadcast |
| **Repository** | Channel information within an ask; quests within the permitted workspace | Messages inform; quests request owned work |
| **Lane** | Quest publication | Repository remains owner; lane constrains work. A lane is not an independent chat mailbox |
| **Working role** | A typed work request | Start plan selects staffing for that capability and scope. Naming a role grants no management acts |
| **Appointed role** | Its exact question or management-request protocol | Appointment, scope, generation and act judge remain mandatory |
| **Native child** | Its harness protocol | Observable identity only unless an actual messaging capability exists |
| **Meeting participant** | Meeting protocol | Delivery and spending remain behind its packet and turn barriers |

Helpers, partners, reviewers and brainstorm passes keep their current restricted result doors. A generic message tool must not give them a back channel around those restrictions. The connector’s work table remains the authority.  
Evidence: `src/Daoris.Service/Daoris.Service.Mcp/CallTable.cs:95`; `src/Daoris.Service/Daoris.Service.Mcp/RoleFilter.cs:64`.

### Records retained

| Case | Canonical request/result | Shared-model change |
|---|---|---|
| Quest, lane work, workflow request | Quest and applied operations; node keeps its quest reference | Add normalized request/result references; retain take, holds, evidence and landing |
| Helper | Helper request, capture, state and answer | Preserve stable correlation and expose one exchange |
| Partner | Piece request, frozen commit, answer and observed taking-in | Show production, delivery and integration separately |
| Opinion | Opinion candidate, findings, dispositions and recheck | Preserve the gate; never reduce it to “message answered” |
| Person-question | New service-owned question revisions and answer references | Conditional answer against the revision shown; legacy files remain readable |
| Go-ahead, permission, rule/help proposal | Existing typed record | Project its requested decision and available acts; retain all authority checks |
| Hand-over | Move record and existing settlements | Carry outstanding request references and reply destinations |
| Meeting/brainstorm | Existing packets, passes and results | Adapt identities and receipts without removing barriers |
| Native child | Lineage and harness observations | Show only observed communication and lifecycle; no invented request or completion |

## 3. Delivery and fulfillment

Keep D188’s delivery rule:

> Keep first; attempt at the earliest boundary the level permits; report the boundary actually available; a message never stops a turn.

| Level | Sender | Boundary |
|---|---|---|
| Ordinary | Person, agent, service, solicited result | Natural point or explicit read |
| Urgent | Person; service fact invalidating current work; bounded agent claim; result whose request allowed urgency | After in-flight tools, before the next model step where supported; otherwise held with the reason |
| Now | Person’s own words alone | Measured steering boundary; never an implicit stop |

Opinion findings, quest outcomes and piece results stay ordinary. Meeting barriers may delay delivery beyond the ordinary boundary. Levels never reorder the queue.

Preserve the implemented channel bound: ten posts per part, with at most three retained as urgent; the fourth urgent request is kept ordinary and reported as such. The co-working skill must say the same.  
Evidence: `src/Daoris.Service/Daoris.Service.Core/Asks/Channel.cs:9`; `src/Daoris.Service/Daoris.Service.Mcp/ChannelTools.cs:47`.

### Two independent accounts

**Delivery** answers what Daoris observed:

`kept → attempted → sent → taken/read`

It also records `partial`, `uncertain`, `not reached` and the current holding reason. Sent is not taken. Read is not agreement.

**Outcome** answers the protocol:

- question: unanswered, replied, withdrawn or superseded;
- decision request: pending, approved, refused, deferred or stale;
- work: open, taken, submitted, done or declined;
- piece: produced, then independently taken into the lead;
- opinion: given, answered, rechecked, disputed or settled;
- hand-over: pending, successor admitted, interrupted or refused.

A turn ending becomes **“turn ended after delivery”**, not proof that a question was answered. Keep legacy `answered` receipts visible as legacy observations; do not retroactively strengthen them.

### Explicit messages

Extend the channel protocol with exact participant and line recipients, including alongside parts of one entry. Introduce `message_send`, `message_read` and `message_reply`; retain `channel_post` and `channel_read` as compatible ask-scoped adapters.

These tools send information or optional questions. They cannot request repository changes, create a helper, satisfy a go-ahead or grant a continuation.

A required answer uses the appropriate work/reading/person protocol. This retains the useful distinction between “please tell me when you next work” and “my work cannot proceed without this result.”

Audience expansion is recorded once. Delivery is tracked per logical recipient, with an executor attached when claimed. A replacement part of the same line inherits that line’s pending delivery; unrelated alongside parts do not.

## 4. Waiting, answering and recovery

### Waiting is a dependency, not an execution state

Every request reading says:

- who owes the result;
- whether the sender carries on or has suspended;
- what resources remain held;
- what event may resume it;
- its age, deadline where one exists, and current reason for waiting.

Keep the existing resource rules:

- Helper suspension releases the running slot and retains its tree.
- Quest suspension releases the slot and does not reserve its tree; the lead commits before waiting.
- A person-question takes precedence over a simultaneous helper wait.
- A chat publishing quests never gains a quest-result wake.
- A workflow request waits through its node record.

Round 11’s routing standings, chat rule and distinction between an answer and code availability stand.  
Evidence: `docs/2026-10-24-cowork-view-design.md:421`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Domain/HelperWaits.cs:10`.

### Answers to the person-question shown

New questions are immutable revisions owned by the service. The connector writes the question against its session and run; the driver binds it to that run’s park. A later question supersedes the prior revision explicitly.

A choice submission carries:

`session + question ID + revision + choice ID + optional person’s words`

In one service transaction, validate the question still stands, record the response, and keep the resulting word. A stale selection is refused beside its control with the current question available to read. Free words remain the person’s intervention; they are never inferred to select a choice or grant an act.

This changes the current mutable file into a source that can support durable correlation. It does not turn questions into approvals.  
Evidence for the current shape: `src/Daoris.Service/Daoris.Service.Core/Sessions/QuestionBox.cs:135`; `src/Daoris.Service/Daoris.Service.Core/Sessions/QuestionBox.cs:158`.

### Restart and local hand-over

Use source keys throughout:

1. Commit the source request or result before acknowledging it.
2. Resolve the recipient line through the service’s existing head rules.
3. Keep a word or a reference to its source under its producer key.
4. Record the attempt before writing to the harness.
5. Settle each word individually.
6. After restart, retry only demonstrably unattempted delivery. An uncertain attempt waits for the person.

This follows existing `said`, keyed moves and uncertain-word handling. It does not promise exactly-once model execution.  
Evidence: `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/SaidDoor.cs:96`; `src/Daoris.Service/Daoris.Service.Core/Sessions/Sessions.cs:1694`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/GoOn/UncertainWords.cs:61`.

The hand-over brief gains a structured section listing outstanding request references, their owners, reply destinations and delivery dispositions. The brief remains bounded, deterministic and attributed. It is context, not another queue.

The reply follows the requester’s line, not its former agent, account or old conversation. A cycle, bound exceeded, missing source or unread head holds delivery visibly. Do not substitute the planner’s resumable record for the service’s head; Round 11 deliberately distinguishes those readings.  
Evidence: `src/Daoris.Service/Daoris.Service.Core/Sessions/Lines.cs:41`; `docs/2026-10-24-cowork-view-design.md:385`.

### Bound answer-driven continuation consistently

**Recommendation: include D79 quest-answer resumes in the two-consecutive-continuations bound.** This explicitly narrows today’s automatic continuation policy and settles the open choice identified by Round 11.

Count a continuation caused solely by a helper answer, piece result, opinion, supervisor answer or waited quest result. An answer entering an already-running turn counts nothing. A declared workflow advancing normally is not an answer-only continuation.

Use a durable admission record per logical line:

- source result key;
- requesting line and continuation epoch;
- reserved, attempted, taken, cancelled or uncertain disposition;
- reset occurrence, when applicable.

Admission is serialized across driven and chat executors. Two results queued together cannot each assume they are the second continuation. An uncertain reserved attempt remains held until resolved.

Only an actual person’s intervention or an independently authorized service action resets the epoch. Wrapping an answer in Daoris’s words, composing a brief, retrying or handing over does not reset it. A role’s answer counts as an answer.

The third continuation uses the existing answers-alone wait and actions. This needs durable admission because event absence or trimming must not silently reset the bound. The current event-based count remains the legacy reader.  
Evidence: `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Domain/AnswersAlone.cs:10`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Domain/AnswersAlone.cs:100`; `docs/decisions/D188.md:362`.

### Unanswered requests

There is no generic automatic resend.

| Case | Bound and unanswered outcome |
|---|---|
| Passive message/question | Posting bounds and scope lifetime; no automatic turn or reminder loop. Show unanswered age. Expiry never means answered |
| Helper, partner, opinion | Existing admission and run deadlines. Missing, refused or uncertain result is explicit; a suspended lead receives the existing person-facing recovery |
| Person-question | Remains pending until answered, superseded or withdrawn. “By when” is advisory unless a typed protocol owns a deadline |
| Quest | May legitimately remain open without a time limit. Show take state and the planner’s reason; never auto-decline another repository’s work |
| Meeting/brainstorm | Existing turn, round, repair and total bounds; interrupted work does not restart itself |
| Role request | Appointment expiry ends executability. Retain request history as interrupted; no act replays after restart |

A deadline expiring is a failed or unanswered outcome, never permission to choose for the recipient. Attention is raised only where the person has an actual recovery act.

### Another machine

Communication follows the record’s portability, not a universal roaming inbox:

- Quests and their handbacks remain the portable work protocol.
- Existing permitted ask-channel messages retain their workspace exchange.
- Session words, person-questions, helper/piece/opinion state, role requests and execution receipts remain local.
- A remote mirror reports **“delivery not observed on this machine”**, not zero, waiting or taken.
- Explicit participant messages to an executor on another machine are unavailable until that protocol is negotiated. Offer the existing quest or ask-channel door; do not silently change the audience.
- A new executor on another machine reads outstanding portable quest dependencies from those records. It cannot inherit a local helper’s copy or an appointment.
- D178’s losing take remains a loss. A result does not authorize transferring ownership or combining work.

“Hand mine to the winner” retains D178’s prerequisites: immutable commit, person-chosen publication location and receiver-confirmed reachability. COMMS1 supplies correlation; it does not supply git objects or publish branches.  
Evidence: `docs/decisions/D178.md:14`; `docs/decisions/D178.md:64`.

## 5. Co-working

| Participant | How it joins and what it may send | How work returns |
|---|---|---|
| **Lead** | Existing quest or repository conversation. May publish quests, ask permitted readings/pieces, send passive messages and ask the person through its allowed doors | Receives results at its line; verifies claims; owns integration in its tree |
| **Part** | Existing entry and quest routing, including alongside trees | Its handback belongs to its quest. Its code joins through the entry’s landing; a reply makes no commit appear in another part’s tree |
| **Helper** | One-turn captured reading under `helpers` | One answer to its request. Follow-up is a fresh reading; no peer delegation |
| **Partner** | Existing person’s piece door; agent door only when its separately owed implementation and confinement proof land | Frozen commit plus claims. Lead integrates it; observed ancestry settles taking-in |
| **Reviewer** | Candidate-bound opinion occasion or person’s ask | Findings, working-session dispositions and bounded recheck; never acceptance by agreement |
| **Native child** | Harness-owned short work | Harness return only. Daoris shows observed state, generation and visibility limits; no independent resumability claim |
| **Other repository’s session** | Quest addressed to its repository/lanes | Quest result and permitted information; never cross-repository edits |
| **Supervisor/steward holder** | Existing appointment and exact role protocol | Attributed decision or act receipt. No co-worker status, free-word authority or inherited appointment |
| **Meeting/brainstorm voice** | Protocol-issued participant and packet | Its permitted structured result, released only at the protocol barrier |

D203’s team remains the lead’s line and the records serving it. Parts, native children, external repository sessions, role holders and meeting voices appear beside that team under their existing categories; none gains `serves` merely to fit the drawing.

Reviewers join the team through D203’s intended `serves` relationship while legacy reviewers remain readable through their opinion’s working-session relation. The current opener still writes `opinion` without `serves`.  
Evidence: `docs/decisions/D203.md:18`; `src/Daoris.Service/Daoris.Service.Core/Review/OpinionDesk.cs:128`.

COMMS1 should finish the shared exchange reading owed by D203. It should not activate unmeasured partners, real meetings or several-reviewer execution as a side effect.

## 6. Surfaces

One exchange has one source identity and one full-body renderer. Other locations show its state, short reference and a door to that renderer.

The session conversation owns helper/piece/opinion exchange cards. The ask’s channel owns ask-channel message bodies. A delivered channel message appears in the conversation as a linked delivery marker, not another full quoted message. Opening its channel focuses the original.

The head shows current responsibility, waits and doors. *At work* shows participants and dependencies. Neither becomes another transcript. Its newest-three channel references continue Round 11’s shared channel read.

### Session and co-working view — 1546 px

Illustrative resized layout: activity 48, rail 280, conversation 638, dock 580 px. Existing frame resizing remains authoritative.

```text
┌──────────────────────────── Sessions ───────────────────────────────────┐
│ Rail                 │ Session: Update parser        │ At work          │
│ Update parser        │ Working · Part 4              │ This session     │
│   helper · answered  │ [Bring in another agent…]     │ Part 4 · working │
│                      │ Waiting on #q7 [Open]          │ Part 3 · waits   │
│                      ├───────────────────────────────┤                  │
│                      │ You: …                        │ Co-workers       │
│                      │                               │ Helper · answered│
│                      │ Helper question #h2           │ [Open exchange]  │
│                      │ Answer received · taken       │                  │
│                      │ [Read answer]                 │ Asked through    │
│                      │                               │ quests           │
│                      │ Channel #m8 · urgent          │ #q7 · taken      │
│                      │ Taken by this part [Open]     │                  │
│                      ├───────────────────────────────┤ Latest messages  │
│                      │ To this line · next step      │ #m8 → Part 4     │
│                      │ [Your words…] [Send ▾]        │ [All messages]   │
└────────────────────────────────────────────────────────────────────────┘
```

The wait line names the source record. The exchange distinguishes result produced, delivery and integration where relevant.

### Session — 680 px

Activity bar remains; collapsed rail uses its existing strip. The remaining main area is 576 px. Below the existing 768 px threshold, an opened dock occupies the available work frame.

```text
┌────────────────────────────────────────────┐
│ Session: Update parser             [⋯]     │
│ Working · Part 4                           │
│ [At work · 3] [Channel · 1 unread]          │
│ Waiting on #q7                      [Open] │
├────────────────────────────────────────────┤
│ Helper question #h2                        │
│ Answer received · taken                    │
│ [Read answer]                              │
│                                            │
│ Channel #m8 · urgent · taken        [Open] │
├────────────────────────────────────────────┤
│ To this line · next step                   │
│ [Your words…                              ]│
│                                  [Send ▾] │
└────────────────────────────────────────────┘
```

Opening *At work* or the channel uses the existing full dock with an explicit return door. It does not squeeze a second column beside the conversation. Existing geometry already defines the 300 px dock floor and full-width narrow behavior.  
Evidence: `src/Daoris.Web/src/work/frame/layout.ts:42`.

### Ask and channel — 1546 px

```text
┌──────────────────────── Ask: Update parser ─────────────────────────────┐
│ Work: repository A · repository B       [At work]                       │
├────────────────────────────────────────────────────────────────────────┤
│ Channel                    [All] [Questions]       2 unread             │
│ #m8  Part 2 → repository B · urgent                                    │
│ “The accepted input shape changed…”                                   │
│ Sent · delivery not yet confirmed                         [Reply]       │
│                                                                        │
│ #m9  Part 5 → Part 2 · answering #m8                                   │
│ “Checked against the new shape…”                                      │
│ Read by Part 2                                             [Open]       │
├────────────────────────────────────────────────────────────────────────┤
│ To [Everyone ▾]   [Reply wanted □]   Delivery [Urgent ▾]                 │
│ [Your words…                                                ] [Send]   │
└────────────────────────────────────────────────────────────────────────┘
```

The composer says when a selected target cannot receive messages. *Request work* opens the quest door rather than interpreting the message’s prose.

### Ask and channel — 680 px

```text
┌────────────────────────────────────────────┐
│ Ask: Update parser                         │
│ Work · 2 repositories             [At work]│
│ Channel · 2 unread        [All ▾]          │
├────────────────────────────────────────────┤
│ #m8 · Part 2 → repository B                │
│ Urgent                                    │
│ The accepted input shape changed…          │
│ Sent · delivery not yet confirmed          │
│ [Reply] [Open session]                     │
├────────────────────────────────────────────┤
│ To [Everyone ▾]                           │
│ [Reply wanted □] [Urgent ▾]                │
│ [Your words…                              ]│
│                                    [Send] │
└────────────────────────────────────────────┘
```

### What needs you — 1546 and 680 px

The same source key drives the badge, notification row and session wait. Several obligations may exist on a session; the same obligation is never counted twice.

```text
1546 px
┌──────────────────────────── Notifications ─────────────────────────────┐
│ What needs you                                                        │
│ Question #p4 · Update parser · waiting 12 min                           │
│ Which compatibility policy should apply?                     [Answer] │
│                                                                        │
│ Answer held · Update parser                                           │
│ A third answer would continue this line again.          [Go on] [Keep] │
├────────────────────────────────────────────────────────────────────────┤
│ Messages · 2 unread                                      [Open channel]│
└────────────────────────────────────────────────────────────────────────┘

680 px
┌────────────────────────────────────────────┐
│ Notifications                              │
│ What needs you                             │
│ Question #p4 · waiting 12 min               │
│ Update parser                              │
│ Which compatibility policy should apply?   │
│ [Answer]                                   │
│                                            │
│ Answer held                                │
│ Third continuation by answers alone        │
│ [Go on] [Keep]                             │
├────────────────────────────────────────────┤
│ Messages · 2 unread         [Open channel] │
└────────────────────────────────────────────┘
```

Passive unread messages remain in *Messages*, using the existing dot convention. They do not become *What needs you* counts. A supervisor’s held decision is not a wait on the person; a deferral is.

Evidence for the existing separation: `docs/2026-09-19-platform-ux.md:28`; `src/Daoris.Web/src/work/waits/waits.ts:143`.

### Ask Daoris — 1546 and 680 px

```text
1546 px: beside the current work
┌──────────────── Session ────────────────┬──────── Ask Daoris ───────────┐
│ Question #p4 [Open]                    │ “Why is this waiting?”        │
│                                       │ #p4 requires your decision.   │
│                                       │ [Open question]               │
│                                       │                               │
│                                       │ Proposed hand-over #hp2       │
│                                       │ Exact change and terminal twin│
│                                       │ [Apply] [Dismiss]             │
└───────────────────────────────────────┴───────────────────────────────┘

680 px: existing full dock
┌────────────────────────────────────────────┐
│ [Back to session]             Ask Daoris   │
│ #p4 requires your decision.                │
│ [Open question]                            │
│                                            │
│ Proposed hand-over #hp2                     │
│ Exact change and terminal twin             │
│ [Apply] [Dismiss]                          │
│ [Your words…                      ] [Send] │
└────────────────────────────────────────────┘
```

Ask Daoris reads the shared status and points to the canonical exchange. It proposes existing acts; it cannot answer for the person, appoint a role or turn explanatory prose into approval.

Across every surface:

- Refusals stay beside the control that caused them.
- Request changes invalidate stale selections and preserve typed drafts.
- Keyboard return restores focus to the originating row.
- Chrome uses catalogue IDs in every shipped language. Authored questions, answers and configured names stay as written.
- Missing, stale and partial sources remain visibly distinct from empty sources.
- All composition and routing are deterministic. Model-produced readings retain their actual provider/tier and limitations.

## 7. What changes in code

Paths marked **new** are proposed files. Existing-file citations identify the behavior being extended.

| File | Change |
|---|---|
| **New:** `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Domain/Communication.cs` | Pure address, source-reference, outcome and delivery-policy shapes. No participant registry or authority inferred from text |
| **New:** `src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/CommunicationRead.cs` | Compose domain adapters at the layer that can see them all; expose source health and available acts |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Domain/ParticipantWork.cs:73` | Retain classification; use it when judging message and result capabilities |
| **New:** `src/Daoris.Service/Daoris.Service.Core/Asks/AddressedMessages.cs` | Versioned explicit-recipient messages, correlated replies and durable per-recipient attempts |
| `src/Daoris.Service/Daoris.Service.Core/Asks/Channel.cs:735` | Adapt existing channel operations; preserve legacy levels and receipt meanings; share scope and audience judgement |
| `src/Daoris.Service/Daoris.Service.Core/Asks/ChannelWire.cs:76` | Keep the legacy wire allow-list. Negotiate a separate version before any new shareable message shape travels |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/WorkOrders/ChannelReach.cs:90` | Dispatch exact recipient obligations, distinguish line from entry, and attach source references without another content queue |
| `src/Daoris.Service/Daoris.Service.Mcp/ChannelTools.cs:18` | Keep legacy tools; describe normalized references and honest delivery results |
| **New:** `src/Daoris.Service/Daoris.Service.Mcp/MessageTools.cs` | Passive send/read/reply tools; no work creation or wake privilege |
| `src/Daoris.Service/Daoris.Service.Mcp/KnowledgeServer.cs:28` | Register tools through the existing census |
| `src/Daoris.Service/Daoris.Service.Mcp/CallTable.cs:95` | Explicit cells for every new tool; restricted participants keep result-only access |
| `src/Daoris.Service/Daoris.Service.Mcp/WaitingFilter.cs:21` | Reuse the normalized unread reading; notices remain hints, not delivery |
| **New:** `src/Daoris.Service/Daoris.Service.Core/Sessions/QuestionRequests.cs` | Immutable question revisions, park binding and atomic correlated answers |
| `src/Daoris.Service/Daoris.Service.Core/Sessions/QuestionBox.cs:70` | New writes use the service-owned request protocol; preserve bounds and owner marks |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/WorkOrders/Questions/QuestionFiles.cs:87` | Retain legacy reading and settlement; use explicit legacy source identities, never parse a historical question from prose |
| `src/Daoris.Service/Daoris.Service.Core/Sessions/Sessions.cs:392` | Carry additive source/reply references on kept words; keep existing author fields and fallback meanings |
| `src/Daoris.Service/Daoris.Service.Core/Sessions/HelperDesk.cs:149` | Return normalized delivery/result references while preserving head following, stable keys and park protections |
| `src/Daoris.Service/Daoris.Service.Core/Sessions/PartnerDesk.cs:172` | Preserve piece correlation; expose partner identity separately from its legacy helper carrier |
| `src/Daoris.Service/Daoris.Service.Core/Review/OpinionDesk.cs:128` | Add the intended serving relation for new reviewers; expose candidate-bound request/result references |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/SaidDoor.cs:34` | Carry source references through keeping and per-word attempt acknowledgment |
| **New:** `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/ContinuationAdmissions.cs` | Durable, serialized answer-driven continuation admission across executors |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Domain/AnswersAlone.cs:28` | Keep the historical reader; delegate new admission to typed causes, including waited quest results |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/WorkOrders/Domain/Planner.cs:1160` | Preserve dependency holds; require continuation admission before an answer-caused resume |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/MoveStore.cs:334` | Link successor delivery and continuation admission without duplicating the result body |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Store/DesktopStore.cs:713` | Add the admission family and migration through the existing transaction, writer-stamp and wake mechanism |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Context/Domain/ContextBrief.cs:58` | Carry structured outstanding references and dispositions; content remains bounded and attributed |
| `src/Daoris.Desktop/Daoris.Desktop.Modules/Platform/StewardDesk.cs:106` | Persist request history and interrupted disposition; live appointment memory remains the sole authority |
| `src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/SessionsRoutes.cs:610` | Project role request/decision references; keep existing proof, generation and route classes |
| `src/Daoris.Desktop/Daoris.Desktop.Modules/Loop/AtWorkRead.cs:54` | Consume exchange snapshots beside routing inputs; include their health and dependency fences |
| `src/Daoris.Desktop/Daoris.Desktop.Modules/Loop/NeedsAPerson.cs:155` | Deduplicate by canonical obligation reference; retain domain-specific actions and workspace scope |
| `src/Daoris.Web/src/work/WorkFrame.tsx:463` | Own one set of communication queries and handlers; share them with head, conversation, channel and dock |
| `src/Daoris.Web/src/work/conversation/conversation.ts:500` | Read explicit source references; preserve legacy interpretation and render delivery markers without duplicated bodies |
| `src/Daoris.Web/src/work/conversation/SessionBox.tsx:177` | Submit exact question revision and choice; show stale-answer refusal locally |
| `src/Daoris.Web/src/work/head/SessionHead.tsx:450` | Replace separate helper/piece presentation with D203’s shared exchange doors and compact state |
| `src/Daoris.Web/src/asks/ChannelSection.tsx:262` | Shared message renderer, explicit recipient controls, correlated replies and separate delivery/outcome |
| `src/Daoris.Web/src/work/atWork/AtWorkView.tsx:54` | Add exchange references beside participants; keep the view a read and preserve partial-visibility wording |
| `src/Daoris.Web/src/work/waits/waits.ts:147` | Reuse canonical obligation identities and one handler set; keep machine waits out of person waits |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/WorkOrders/ChannelCommand.cs:36` | Keep existing syntax; expose normalized references, recipients and receipts in new message verbs |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/WorkOrders/AtWorkVerb.cs:57` | Terminal and window consume the same exchange reading; headless output retains visibility limits |

Terminal mutation verbs continue through the existing authority seam. No new generic “act as person” verb is introduced.

## 8. Decision amendments and older records

| Decision | What changes; what stays |
|---|---|
| **D32** | Stays: cross-repository work is a quest. Explicit messages confer no editing right |
| **D65** | Ask/intake/workflow remain. Their requests and results gain shared references |
| **D115** | Lane addressing and local ownership stay; lanes gain no mailbox or authority |
| **D83** | Preserve observed parking. Explain suspension through its typed dependency; never reclassify an old park from prose |
| **D135** | Preserve discovery, standing answers and typed go-aheads. Question replies cannot substitute for approval |
| **D155** | Preserve candidate, disputes and gate. Separate findings delivered from findings answered and gate settled |
| **D163** | Preserve helper and hand-over policies. Correlation follows the requester’s line |
| **D167** | Preserve entry grouping and the ask’s channel. Add exact recipients; remove entry-wide exclusion only for explicitly addressed new messages |
| **D188** | Keep levels and no-stop rule. Separate delivery from fulfillment; explicitly extend the continuation bound to quest-answer resumes |
| **D189 / SESSCTX1** | Preserve resume/context rules. Add outstanding request references and dispositions to the portable brief |
| **D199** | Preserve confinement, frozen pieces and lead integration. Shared model does not activate unmeasured production |
| **D203** | Complete the shared exchange read and presentation; team remains a read from serving/continuing relations |
| **D176** | Preserve independent generators, frozen packets, validation and ranked output |
| **D177** | Preserve granted turns and packet barriers. Report current unavailable production capability |
| **D178** | Preserve exclusive ownership, conflict choices and publication prerequisites; no transparent machine hand-over |
| **D89** | Proposals retain their typed application doors and current access policy |
| **D158** | Help history remains machine-local; references create no transcript sync |
| **D170** | Existing proposal-door authority stays; new explanatory/read coverage adds no shell or generic act door |
| **D208** | Preserve appointment, exact offered choices, attribution and overrule. A durable request cannot revive authority |
| **D209** | Preserve exact management acts and owner precedence. Desk history survives; live requests become interrupted at restart |
| **D212** | Keep participant/work readings, role configuration, store boundaries and event ownership. Add typed communication and continuation-admission facts |
| **D219** | Handback remains the applied closing operation, read whole. Shared result references point there |
| **D222** | All new chrome is catalogue-keyed; authored words require no language |
| **D191** | This changes records, concurrency and continuation policy; synthesis requires the foundation reads. This proposal claims no acceptance |

Compatibility rules:

- Read old sources in place; do not rewrite historical IDs, authorship or outcomes.
- Absent channel urgency keeps its historical reach; absent session-word level keeps its existing author-dependent interpretation.
- A legacy `answered` receipt is not upgraded into fulfillment.
- A close without a recorded writer remains the quest’s note.
- Old person-question files remain readable. Their answers are correlated only where the records establish that correlation.
- Put new addressed messages and versioned questions in tables older binaries cannot read. Do not hide stronger semantics in optional fields older executors ignore.
- Negotiate new doors before use. An unsupported exact recipient is refused; it is never broadcast as fallback.
- If historical continuation evidence is incomplete, say the count is unknown and hold the next automatic continuation for the person rather than invent zero.
- Preserve the current legacy channel’s reversible “not reached” reading. New explicit requests have explicit terminal/superseded outcomes.

The new-table boundary follows the repository’s compatibility rule: a flag an older build never reads cannot protect a row it still consumes.  
Evidence: `.claude/knowledge/compatible-builds.md:9`.

## 9. Build rows

Rows F1–F4 form **one foundation wave**. Each is independently reviewable and testable; new protocol writers remain disabled until the wave’s consumers and compatibility proofs are complete.

| Order | Row and contract | Required proof |
|---|---|---|
| **F1** | **Shared identities and read.** Normalize existing sources, authors, outcomes, health and available acts. Change no execution | Table over every census case; partner/helper distinction; legacy reviewer relation; missing and stale sources; no participant/team store |
| **F2** | **Versioned person-questions.** Keep new revisions and answers transactionally; preserve legacy reading and approval separation | Replaced question while answering; double press; restart before/after commit; lost response; free words; owner-only marks; no approval inferred |
| **F3** | **Addressed message delivery.** Exact audience, stable producer keys, correlated replies and per-recipient attempts | Two alongside parts; one/exact-many recipients; wrong workspace; forged author; pre-wire crash; post-wire crash; uncertain attempt; older binaries’ own statements cannot consume new rows |
| **F4** | **Continuation admission.** One serialized two-continuation bound, including waited quest results | Mixed helper/piece/opinion/supervisor/quest sequence; simultaneous second answers; hand-over; restart; missing history; no reset from generated wrappers; held quest result; no change to chat’s no-wake rule |
| **5** | **Result adapters and co-worker wait.** Helper, partner and opinion expose one exchange and preserve their protocols | Answer reaches successor once; real park wins; cap-one helper completion; failure without answer; piece produced but unmerged; disputed opinion |
| **6** | **Role request history.** Retain desk history without retaining executable grants | Restart marks interruption; revoke between admission and execution; stale generation; role name in content grants nothing; owner precedence |
| **7** | **Session surfaces.** Shared exchange cards, question answers and compact head | 1546/680 layouts; 300 px dock; keyboard return; stale selection; one full body per exchange; same handlers in head and foot |
| **8** | **Channel and co-working integration.** Explicit recipients and newest-three references through existing reads | One channel query; source-focused navigation; same-entry reply; recipient removed; partial source health; routing/strip agreement; no state parsed from prose |
| **9** | **Attention and Ask Daoris.** One obligation key, correct counts, canonical doors and proposal coverage | Same wait on head/foot/notifications counts once; unread message produces no actionable count; role deferral; workspace isolation; unsupported act refusal |
| **10** | **Terminal twins and machine boundaries.** Same normalized read and existing mutation authority | Window/headless parity; JSON identity parity; no live-child claim headlessly; old/new remote combinations; no local payload/path/appointment crossing; lost-take behavior unchanged |
| **11** | **Doctrine and installed proof.** Teach the shared model and actual availability | Canon-first changes synced to all materializations; tool-description/call-table census; catalogue parity; real installed interaction and recovery evidence |

Each behavioral row requires its focused service, driver, modules and web checks. The foundation boundary runs the reached component gates and baseline `npm run verify`; real-process suites run serially under the development guide.

Installed proof must separately demonstrate:

- two alongside parts exchanging an exact-recipient message;
- an urgent message waiting honestly on a turn-end door;
- a helper answer following a hand-over once;
- a stale person-question choice being refused;
- the third mixed answer-driven continuation waiting for the person;
- a restart leaving uncertain delivery unsent;
- local operation with no remote;
- a mirrored result showing its limited visibility;
- partner and meeting capabilities remaining visibly unavailable until their own production proofs exist.

Scripted providers prove protocol and recovery behavior. They do not establish provider steering, confinement or installed-window behavior. Those claims retain their own measured evidence.