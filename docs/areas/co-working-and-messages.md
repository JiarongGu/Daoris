# Co-working and messages — how it works now

## What it does

Sessions, the person and Daoris talk through records Daoris keeps, never process to process.
A word to a session waits on its record with a level (ordinary, urgent, now) that says when it may reach the agent; nothing but a stop stops a turn.
Each ask has a channel; a line on no ask has its own; a post may go to everyone, to one repository, or to one part of the poster's own line.
What moves is one of three acts: say, request or hand over. One read composes a line's requests (the exchanges) from the records that carry them.
A session may ask a helper (reads, answers once), the person may ask a partner (writes one piece in its own clone), and the landing rule asks a reviewer (a second opinion).
Work for another repository is a quest; a lead or a chat is told one routing line about its own lanes.
Brainstorms and meetings are bounded operations the person starts; they talk and change nothing.
*At work* and the head's *Waiting on* draw the exchanges; *What needs you* lists each obligation once; the steward desk takes requests for the steward as claims.
Who said a word is read from its marks and its door, never from its text.

## Its parts

Prefixes: `Driver/` = `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` = `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Service/` = `src/Daoris.Service/Daoris.Service.Core/`, `Mcp/` = `src/Daoris.Service/Daoris.Service.Mcp/`, `Web/` = `src/Daoris.Web/src/`.

| Unit | File | Its job |
|---|---|---|
| Senders | `Service/Sessions/Senders.cs`; twins `Driver/Service/Domain/Senders.cs`, `Web/work/conversation/sender.ts` | The one sender rule over a word's marks; held to `fixtures/senders.json` in the service's tests |
| ChannelStore | `Service/Asks/ChannelStore.cs` | The messages, the read marks, the acknowledgments and the push and fetch cursors, in `channel`, `channel_marks`, `channel_acks` and `channel_cursor` |
| ChannelDesk | `Service/Asks/ChannelDesk.cs` | An ask's channel: post (session, person), read, receipts, Daoris's lines from the ask's quests, the judge |
| Channel records | `Service/Asks/Records/` | `ChannelMessage`, `ChannelLine`, `ChannelRead`, `ChannelOutcome`, `ChannelReadOutcome`, `ElsewhereAsk`, `IChannelItem` |
| HistoryDesk | `Service/Asks/HistoryDesk.cs`, its units and refusals in `Service/Asks/Records/` (`HistoryUnit`, `HistoryKept`, `HistoryRefusal` and the words beside them) | The records' half of clearing finished history: listed first, then pressed, each unit judged again inside the transaction that clears it |
| ChannelParts | `Service/Asks/ChannelParts.cs` | A line's own channel and messages to one part, kept in `channel_addressed`, never synced |
| LineChannel | `Service/Asks/LineChannel.cs`, `GET /api/channel/line`, the window's `CHANNEL_LINE` | The person's read of the channel of a line on no ask held here, each message to one part with its receipt |
| TeammateAskChannel | `Service/Asks/TeammateAskChannel.cs` | A teammate's ask's channel, posted to by the person and by a session here on it, and read by that session |
| ChannelBounds, ChannelRefusal, ChannelAcks | `Service/Asks/ChannelTerms.cs` | The bounds (4,000 chars, 10 posts, 3 urgent, 20 handed), refusal codes, acknowledgment states |
| ChannelTools | `Mcp/ChannelTools.cs` | `channel_post` (urgent or ordinary, `to`, `re`) and `channel_read` |
| CallTable, ToolClasses | `Mcp/CallTable.cs`, `Mcp/ToolClasses.cs` | Which work may call which connector tool; a census holds every tool to a class |
| SolicitedAnswers | `Service/Sessions/SolicitedAnswers.cs` | The one door for a helper's answer, a partner's and an opinion's findings, to the line's head |
| HelperDesk, PartnerDesk | `Service/Sessions/HelperDesk.cs`, `PartnerDesk.cs` | Open and judge a helper's and a partner's record |
| BrainstormDesk, MeetingDesk | `Service/Sessions/BrainstormDesk.cs`, `MeetingDesk.cs` | The one writer of a brainstorm's or meeting's request, send and stop |
| QuestExchange, QuestAddress | `Service/Quests/QuestExchange.cs`, `QuestAddress.cs` | Publish, take, wait, done, decline; `repository:lane` addresses; the self-address refusal |
| Exchanges | `Driver/WorkOrders/Domain/Exchanges.cs` | `Exchanges.Of`: a line's requests, delivery apart from outcome, unread sources said |
| ChannelReach | `Driver/WorkOrders/ChannelReach.cs` | Hands channel and addressed messages to running parts at their level, by message id |
| ChannelCommand | `Driver/WorkOrders/ChannelCommand.cs` | The terminal's channel verb |
| RoutingStanding, StandingBlock | `Driver/Participants/Domain/RoutingStanding.cs`, `Driver/Context/Domain/StandingBlock.cs:214` | The routing line told to a lead or a chat |
| DriverHelpers, HelperTurn, HelperWaits, HelperKey | `Driver/Loop/DriverHelpers.cs`, `Driver/Loop/Helpers/HelperTurn.cs`, `Driver/Participants/Domain/HelperWaits.cs`, `Driver/Settings/HelperKey.cs` | A helper's ask judged, run and answered, its pass in `HelperTurn`; the lead's wait on its own helper; the `helpers` rule |
| DriverPartners, PartnerAdmission | `Driver/Loop/DriverPartners.cs`, `Driver/Participants/Domain/PartnerAdmission.cs` | A piece's clone, one frozen commit, the two-piece bound, measured confinement |
| PartnerPersonActs, PartnerPersonAct, PartnerActRunner | `Driver/Participants/Domain/PartnerPersonActs.cs`, `Participants/Partners/PartnerPersonAct.cs`, `Loop/PartnerActRunner.cs` | The person's *Discard…* and *Hand over what it left* as a file the look does, and the read of whether a handed piece was taken in |
| OpinionDeliverer | `Driver/Loop/OpinionDeliverer.cs` | An opinion's findings through the one door (the gate is the review area's) |
| DriverBrainstorms, DriverMeetings | `Driver/Loop/DriverBrainstorms.cs`, `DriverMeetings.cs` | Brainstorm passes and meeting turns at the look, under the cap |
| AnswersAlone, AnswersAloneHold | `Driver/Course/Domain/AnswersAlone.cs`, `Driver/Course/GoOn/AnswersAloneHold.cs` | The answers-alone count, one admission per line, the hold where the line's rule says |
| AtWork read | `Driver/Participants/Domain/AtWork*.cs`, `Driver/WorkOrders/AtWorkExchangeReading.cs`, `AtWorkVerb.cs` | *At work*: parts, co-workers, quests, subagents; `sessions at-work` |
| ContextWorkOrders | `Driver/Context/ContextWorkOrders.cs` | The exchanges and channel bounds as an instruction and a hand-over brief quote them |
| AttentionStateSlice, NeedsAPerson | `Modules/Loop/AttentionStateSlice.cs`, `NeedsAPerson.cs` | *What needs you*'s rows, judged once and pushed to every window |
| StewardDesk | `Modules/Platform/StewardDesk.cs`; holder's verb `Driver/Roles/StewardCommand.cs` | Requests for the steward, as claims, bounded, history in `data/steward-desk.json` |
| Page: conversation | `Web/work/conversation/Composer.tsx`, `ExchangeCard.tsx`, `exchanges.ts`, `answerHeads.ts`, `AskView.tsx`, `SenderChip.tsx`, `channelHome.ts` | The level menu; a helper ask's card and the exchanges drawn; each word's sender chip; a helper's answer no card holds as its agent's block; a channel message's poster and the door to its channel |
| Page: At work, channel, attention | `Web/work/atWork/AtWorkView.tsx`, `exchangeSeek.ts`, `AtWorkAskView.tsx`, `Web/asks/ChannelSection.tsx`, `TeammateAskPage.tsx`, `Web/bridge/sessions/asks.ts`'s `useLineChannel`, `Web/work/attention/attention.ts`, `Web/work/waits/waitObligations.ts`, `Web/notifications/StewardLine.tsx` | *At work* and *Waiting on*, opened at a request, with the line's channel; the ask's channel and a teammate's; *What needs you* and the foot's count by one key; the steward's line |

The agent's half is the canon skill `canon/packs/co-working/skills/co-working/SKILL.md`; it also says what the person sees of an agent's part (the sender marks, the wait counted once, a line's channel, the steward desk's history).

## Its rules

**Levels and delivery**

1. A message never stops a turn. Only a stop does: the person's *Stop*, *Stop turn* or keeping stop, or Daoris's own timeouts, pauses,
   limits and pressed hand-overs. No agent stops another's work. (from D188)
2. A level says when a word may reach its reader, never its order. Ordinary waits for a natural point: the turn's end, between turns,
   the next part, or the agent's own read. Urgent goes at the next step where the door takes words then. Now cuts the step in flight
   where the agent steers. (from D188, D136)
3. A level is a request. On a door that takes words only at its turn's end, urgent and now wait for that end, and say so. A text-only
   conversation honours no level. (from D188, D136)
4. The person may send all three, urgent by default; now is theirs alone. Daoris sends ordinary, or urgent for a fact that makes the
   work in flight wrong. An agent posts ordinary or urgent, never now. (from D188)
5. Every word to a session is kept on its record's `said`, with its level, sender and producer key, before its sender is told. One
   dispatcher per session drains it in order. A key is kept once while its word waits. (from D188, D212)
6. Receipts show only what a door observed: held, sent, taken, read, partly handed, answered, uncertain, not reached. *Answered* means a
   reply named the word; a turn that ended after taking it is *taken, turn ended*. Answered never means obeyed. (from D188, D225)
7. A word attempted before a restart and never confirmed is *uncertain*, and is never sent again unasked. (from D188)
8. A person's word reaches a session in any state: at its next step, at its turn's end, or on its record when parked or ended, which it
   goes on with. (from D137, D188)

**The channel and posts to one part**

9. Each ask has one channel for its sessions and the person: messages to everyone or to one of its repositories, and Daoris's lines
   read from the ask's quests. It is service records, travels within the ask's workspace, and carries no path or file. A teammate's
   ask, held on another machine, is posted to from here by the person and by a session here on its quest: the message is kept as this
   machine's own, at its level, and travels to the holder, where a teammate's person's message is a claim named as theirs that reopens
   nothing, never the holder's person's words. (from D167, D225)
10. A line on no ask posts on its own channel, on its entry's origin, which the person reads on *At work*. A helper and a reviewer
    have no channel: they neither post nor read. (from D225, D163, D155)
11. A part posts at most 10 messages of at most 4,000 characters, and at most 3 urgent. A further urgent post is kept ordinary, and its
    poster is told. The person has no cap. (from D188, D167)
12. A post may name one other part of the poster's own entry on this machine by its session. It is kept apart, never travels, and is
    for the head of that part's line: a successor inherits it, a sibling does not. Another line's part is refused `bad-part`; one on
    another machine `part-elsewhere`. (from D225)
13. Delivery is by identity. A part is handed what its entry has not acknowledged, by message id, and one part of the entry claims each
    message. The entry's mark is for display. (from D188)
14. A running part takes the person's channel message as their words, at their level. Another session's message reaches it as Daoris's
    words around that agent's claim: urgent at the next step where the door takes words; ordinary only riding a prompt the run sends
    anyway. (from D188, D167)
15. An unsolicited message never starts, reopens or extends a run. What a part cannot take waits for the entry's next part, whose start
    carries the newest 20. A claim never taken expires visibly once its ask has no quest open and no part runs. (from D167, D188)
16. A post asks nothing of anyone. Work, a change in another repository, and an answer the poster cannot go on without are quests.
    (from D167, D32, D79)

**Who a word is from**

17. Who said a word is read by one rule from its marks and the door it came through: person, Daoris, supervisor, channel, helper,
    partner, opinion or unattributed. A supplied author, a role's name or quoted text grants nothing. (from D225, D177)
18. A claim the person passed on stays the agent's claim. Daoris's words are marked `from: daoris` and the supervisor's as the
    supervisor's; neither is ever the person's. (from D225, D188, D208)
19. Which ask a line is on is its entry's origin: `ask:<id>`, else the publisher's origin, else `quest:<id>`; a conversation is
    `session:<id>`. The key is written once at the open and never rewritten. (from D167, D225)

**Requests and the exchanges**

20. What moves is one of three acts. Say expects nothing. A request names what it expects back: an answer, work, a piece, an opinion or
    a decision. Hand over expects a successor. (from D225)
21. `Exchanges.Of` is the one read of a line's requests, over helpers, pieces, questions, hand-overs, quests, opinions and go-aheads,
    walked back along `from`. It keeps delivery apart from outcome, and says a source it could not read. (from D225)
22. A helper's answer, a partner's and an opinion's findings reach the line's head through one door, kept once under `<case>:<id>`. An
    answered park takes each beside the person's answer. (from D225, D203)
23. A solicited answer may extend or reopen its asker's run once per ask. Go-ons by answers alone are counted durably, one admission
    per line; the person's or Daoris's word resets the count. (from D188, D225)
24. The count is a fact, not a limit. The line's answers-alone rule decides, seeded as *hold after 2 in a row* at the machine's place,
    which the person may move or turn off; a supervisor may read it. (from D225, D229)
25. A hand-over's brief says what the line waits on, and that is the successor's to take. (from D225, D163)
26. A go-ahead the person approved on an ask covers the same act from every session the ask's entry holds. (from D225)

**Helpers, partners and second opinions**

27. A co-worker is a record serving a part of a line in one role, read from its marks: reviewer (`opinion`), partner (`serves` with
    `piece`) or helper (`serves` alone). Parts, other repositories' sessions, meeting voices, subagents, Ask Daoris, intakes and
    brainstorm passes are not co-workers. (from D203, D212)
28. A helper is a read-only session on another agent that answers one question, once, from a clone of its own, and asks nobody. It
    starts when the asker's turn ends and reads uncommitted work too. (from D163, D203)
29. The `helpers` rule is per repository, else its workspace: `ask`, `on` or `off`. With none set, an agent's ask is `on` for agents
    Daoris has set up and signed in here; one naming none goes to another maker than the asker's. (from D163)
30. A driven lead that ends its turn on its own helper ask, with no question or go-ahead on the person, ends `completed`
    (`ended.awaits-helper`), held and unparked; the answer reopens it once. A helper ending with no answer brings it to the person. (from D203)
31. A partner writes one piece in a clone with no remote, at the asked commit. Daoris reads its files and writes one frozen commit; the
    piece lands only when the lead merges it. At most two pieces are open per lead. Only the person asks one today. A handed piece is
    taken in when the look first reads its commit as an ancestor of the lead's tree's branch (recorded with the lead's commit, never
    unset; a cherry-pick is not that), and not taken in when the lead's record completed without it. The person's *Discard…* (a piece
    holding work or a copy, never one taken in) and *Hand over what it left* (an uncertain one) are an act file, `<id>.act.json`,
    from the page's `SESSION_PARTNER_ACT` or `sessions partner <piece> --discard | --hand-over`; the look removes the ref at the commit
    it froze and the copy, or keeps, imports and hands the work, and is the one writer of the state. (from D199)
32. A partner starts only where its confinement was measured in the configuration a start would hand it. (from D199)
33. A second opinion is the landing rule's to ask, never an agent's. Its findings are a solicited answer at ordinary, marked as another
    agent's claims. (from D155, D188)
34. One table says which tools a work may call. A helper, a partner and a reviewer have allow-lists of their own and no quest tools; a
    brainstorm's voice has only its give or synthesize tool. (from D203, D176, D212)

**Quests between repositories and the routing line**

35. Work another repository owns is a quest to it, named from the registry or from this repository's `uses`. A quest to its own
    repository must name a lane (`repository:lane`). (from D32, D115, D91)
36. A session that cannot go on without another repository's answer waits on that quest. Its own stays taken, and it resumes in its
    own tree with the outcome, at ordinary. (from D79, D188)
37. A session reads its workspace's other checkouts by default and writes into one only where the person declared it. A change, or
    what only that repository knows, is asked of it. (from D107, D79)
38. A lead or a chat is told one routing line at each start: open, one at a time, no lanes, or not driven. Told nothing, it works its
    rows itself. A serving record is told none. (from D203)
39. A lead that hands rows to its lanes commits, then finishes naming them, or waits on exactly one for its answer. A chat says what
    it published and never waits. (from D203)

**Brainstorms and meetings**

40. A brainstorm is a bounded driver operation: one frozen packet from Daoris's knowledge, shown to the person and sent by their
    press to agents of different makers; generate, synthesize, challenge; a ranked document kept on this machine, never a change. (from D176)
41. No pass may ask for a brainstorm, a helper or a reviewer, or post to a channel. Fan-out is Daoris's fixed plan, never an agent's.
    (from D176)
42. A meeting is the person and named agents in one conversation Daoris relays, each message attributed from its turn's binding.
    Turns go in the person's order, one at a time, and none reads another's message of its round. (from D177)
43. A meeting's next round goes only by the person's press, its packet refreshed under a new digest. A meeting talks: no checkout, no
    publishing, no write. (from D177)
44. The service's brainstorm desk is the one writer of a request, a send and a stop for both kinds. The driver's verbs press it, and
    with no service they say so and exit 1. (from D176, D177)

***At work*, *What needs you* and the steward desk**

45. *At work* draws the attended line from records, never conversation text: its parts, its co-workers (reviewers from records whose
    `serves` names the line), the quests it asked, its subagents and the last words of its channel, its ask's or, on no ask held here,
    its own, a message to one part naming it. *Waiting on* is the requests that hold;
    the head draws the same line, and its door opens *At work* at the first request the line names, that row outlined and focused.
    (from D203, D225)
46. A message is drawn whole once, at its home. Each word's turn is headed by its sender chip: who, by the one rule, and when it was
    taken. An agent's helper ask is one card at its call, and the answer folds into it; a helper's answer no card holds is that
    agent's block, under the question it answers. A channel message's home is the channel: the turn that took it names its poster
    and opens the channel, its words folded where the door is. (from D225)
47. *What needs you* has one row per obligation, keyed by its exchange's id or its kind and id. A park on one decision is that row,
    never a second. Rows are judged once in .NET and pushed. A session's foot counts its waits by the same keys
    (`Web/work/waits/waitObligations.ts`), so one wait is counted once on the foot, the bell, the places and *Notifications*; an
    unread message is counted on none. (from D225, D182)
48. The steward desk files any caller's `{act, target, why}` as claims and grants nothing; the act is judged from the holder alone. At
    most 10 open per caller and 100 in all; a restart reads open ones as interrupted. (from D209, D225)
49. A holder of a role the person appointed reads the asking agent's words as claims, never instructions. A go-ahead, a sign-in,
    access, spending, a deletion and a history rewrite stay the person's. (from D208, D209)

## Where a change goes

- **A new kind of request or wait**: a source in `Exchanges.Of` (`Driver/WorkOrders/Domain/`), with its record owned in Participants.
  *At work*, the head, the brief and *What needs you* read it from there; never add a second read.
- **When a word reaches an agent**: the course's inbox and turns (`Driver/Course/`) and the service's say door. Channel delivery is
  `Driver/WorkOrders/ChannelReach.cs` beside `Service/Asks/`.
- **Who a word is from**: all three sender readers and `fixtures/senders.json` in one change.
- **Which tools a work gets**: `Mcp/CallTable.cs`; a new tool needs its class in `Mcp/ToolClasses.cs`.
- **Helpers and partners**: `Driver/Participants/` (`Domain`, `Helpers`, `Partners`), `Driver/Loop/DriverHelpers.cs`, `DriverPartners.cs`,
  and the service's desks in `Service/Sessions/`.
- **Brainstorms and meetings**: `Driver/Loop/DriverBrainstorms.cs`, `DriverMeetings.cs`, `Driver/WorkOrders/Brainstorm*`, and the desks.
- **Routing**: `RoutingStanding` and `StandingBlock`; quest addressing is `Service/Quests/`.
- ***What needs you***: `Modules/Loop/AttentionStateSlice.cs` and `Web/work/attention/`. **The steward desk**: `Modules/Platform/StewardDesk.cs`.
- **What agents are told to do**: the canon skill, then sync this repository and the examples.

**Tests that pin it.** Driver: `ExchangesTests`, `ChannelReachTests`, `ChannelNextStepTickTests`, `ChannelCommandTests`,
`DrivenInboxTests`, `DaorisWordsTests`, `AnswersAloneTests`, `AnswersAloneHoldTests`, `HelperJudgeTests`, `HelperLookTests`,
`HelperWaitsTests`, `HelperWaitTickTests`, `PartnerAdmissionTests`, `PartnerActsTests`, `PartnerLandingTests` (git half), `SolicitedHandTests`, `RoutingStandingTests`, the `AtWork*Tests`,
`Brainstorm*Tests`, `Meeting*Tests`, `StewardCommandTests`. Service: `ChannelTests`, `ChannelPartsTests`, `TeammateAskChannelTests`, `ChannelSyncTests`,
`SendersTests`, `SolicitedAnswersTests`, `QuestExchangeTests`, `BrainstormDeskTests`, `MeetingDeskTests`, `McpChannelToolTests`,
`McpRoleFilterTests`, `ToolClassesTests`. Modules: `AttentionStateSliceTests`, `StewardDeskTests`, `DriverModuleChannelTests`,
`DriverModuleSessionAnswersAloneTests`. Web: `sender.test.ts`, `ExchangeCard.test.tsx`, `SenderChip.test.tsx`, `Composer.test.tsx`, `AtWork*.test.tsx`,
`ChannelSection.test.tsx`, `TeammateAskPage.test.tsx`, `attention.test.ts`, `waitObligations.test.tsx`, `StewardLine.test.tsx`.

**Gates that bite.** `driver`, `modules`, `service` and `censuses` (the tool-class and transitional censuses), `web-unit`, the web
gate's `i18n:check` and `names:check -- --strict` for any visible word (glossary: *urgency*, *channel*, *co-worker*, *At work*),
and `universal`. The twin fixtures (`senders.json`, `participant-work.json`, `helper-rules.json`) fail every side together.

## Contradictions

1. D188 point 4 (the third answer alone waits, fixed) vs D225's note of 2026-11-03 (a fact, no number fixed). Code seeds a rule at 2 (`Driver/Roles/SupervisorRuleBook.cs:100`) and keeps `AnswersAlone.Bound = 2` for unreached homes (`Driver/Course/Domain/AnswersAlone.cs:22`, registered). D225 and the code read right.
2. D225's note of 2026-11-03 says it amends D188 "point 4's bound" and then repeats the line as "point 5's bound" (`docs/decisions/D225.md:42-43`). Point 4 reads right.
3. D188 point 7 (*answered*: the turn that took it ended with an answer) vs D225's COMMS1h note (that is *taken, turn ended*; *answered* is a reply). D225 reads right.
4. D188 point 6 (the channel's mark moves on taking) vs code (delivery by per-message acknowledgment, the mark for display, `Driver/WorkOrders/ChannelReach.cs:87-92`). Code reads right.
5. D167 point 7 (a message reaches a running session at its next step) vs D188 and code (only urgent does, `Driver/WorkOrders/ChannelReach.cs:77-85`). D188 reads right.
6. D167 point 6 (each ask has one channel) and `Driver/WorkOrders/Domain/ChannelWords.cs:17-18` ("any other origin has none") vs code (a line on no ask posts on its own channel, `Service/Asks/ChannelDesk.cs`'s `SpeakerAsync`). Code reads right.
7. `ChannelDesk`'s summary ("read at a session's next look") vs D188's levels. D188 reads right.
8. The co-working skill ("the service refuses one past the bound" for urgent posts) vs code (a fourth urgent post is kept ordinary, `Service/Asks/ChannelTerms.cs:18-22`, `Mcp/ChannelTools.cs:39`). Code reads right.
9. D163 point 3 (helpers default `ask`) vs code (default `on` for agents set up here, `Driver/Settings/HelperKey.cs:17`, D163's HELPERDEFAULT1 note). Code reads right.
10. D199 point 2 (the lead asks a piece with `partner_ask`) vs code (no such tool; `Mcp/ToolClasses.cs` has `partner_answer` alone). Code reads right today.
11. D203 point 7 (*What needs you* gains no kind) vs code (a `helper-ended` row, `Modules/Loop/AttentionStateSlice.cs:304`, from D203's own HELPERWAIT1c note). Code reads right: it stands in for the park row.
12. D225's COMMS1i note (*At work* lists no reviewers) vs its later note and code (reviewers from records with `serves`, `Driver/WorkOrders/AtWorkExchangeReading.cs:14-31`). Code reads right.
13. D136 point 4 (*Send now* stops the turn on a turn-end door) vs D188 (now stops nothing; `--now` is refused, naming `--level now`). D188 reads right.
14. D176 point 1 (the driver's verb writes the request) vs its ONEWAY1t note and code (the service desk is the one writer, `Service/Sessions/BrainstormDesk.cs`). Code reads right.
15. D32's shape (a quest is a checklist item written into the receiver's backlog by `daoris quest post`) vs its amendment and code (the service holds quests; the CLI has no quest command). The amendment reads right.

## Open

- **STEER3**: measure Codex's `turn/steer` and Claude Code's stream-json input before a level uses them. **MSG1h**, **MSG1i**: next-step words on Codex and on the native door.
- **COWORKUX1** (rows d, i): the head's *Co-workers* with its presses, *Bring in another agent…*, a member's thread with *Back to lead*, talking to a member as asking again.
- **COAGENT1b2** (built: *Discard…*, *Hand over what it left*, the taken-in read; left: the not-taken-in verdict at the landing's own moment where the lead's tree is gone, the retention proof through tidy, clear and sync, and the partner's real-process case) **, 1c, 1g**: a conversation as the lead; a hand-over that waits for children. The lead's own `partner_ask` has no row.
- **CHANNELJOIN1**: an agent the owner runs outside Daoris joins a channel.
- No row: a look handing a teammate's ask's messages to a part here that runs on its quest (today they wait for the agent's own
  `channel_read`); the person posting on a line's own channel; *Send to its session* from a line's own channel.
- **AGENTROOM1b**: `meeting_say`, so a real agent takes a meeting turn (today refused `no-message-tool`). **AGENTROOM1c**: `daoris meeting`. **AGENTROOM1d**: one account's turns one at a time across meetings. D177's floor request has no row.
- **MEETUX1a-d**: a meeting from the window, its rounds and a brainstorm's document on their pages, a waiting meeting in *What needs you*.
- **BRAINSTORM1b2, 1b3, 1c, 1d, 1e, 1f, 1a2b, 1a4**: the CLI's stop and id, a limit on a real door, the measured run, a caller from another repository and the page, the skeptical pass, a workflow step, a no-connector pass test, a pass's traces.
- **STEWARDROLE1d**: the steward offered (today `StewardOfferedInThisBuild = false`, so the desk answers `not-offered`). **1e, 1f, 1g**: its hand-over, go-on and set-up. **SUPERVISOR1a-iii**: the supervisor offered.
- **COWORKVIEW1** row f, after BRAINSTORM1d and AGENTROOM1b. **MSG1j**, **SESSONE1c-look**: the installed canaries.
