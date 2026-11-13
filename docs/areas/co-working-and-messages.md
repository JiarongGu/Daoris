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

Prefixes: `Driver/` = `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` = `src/Daoris.Desktop/Daoris.Desktop.Modules/`,
`Service/` = `src/Daoris.Service/Daoris.Service.Core/`, `Mcp/` = `src/Daoris.Service/Daoris.Service.Mcp/`, `Web/` =
`src/Daoris.Web/src/`. Each type is in a file of its name; `docs/index/README.md` says where a route, verb or fixture is.

| Where | Its job |
|---|---|
| `Service/Asks/` | The channel: its store (messages, read marks, acknowledgments, cursors), `ChannelDesk` (post, read, receipts, Daoris's lines, the judge), `ChannelParts` (a line's own channel and posts to one part, never synced), `LineChannel`, `TeammateAskChannel`, the members (`ChannelMembers`, `ChannelMemberRoll`), the bounds and refusals (`ChannelTerms.cs`), `HistoryDesk` |
| `Service/Sessions/`, `Service/Quests/` | The sender rule (`Senders`, twinned in the driver and the web, held to `fixtures/senders.json`), the one door for solicited answers (`SolicitedAnswers`), the helper's, partner's, brainstorm's and meeting's desks; the quest exchange and its addresses |
| `Mcp/` | `channel_post` and `channel_read`; which work may call which tool (`CallTable`, `ToolClasses`, held by a census) |
| `Driver/WorkOrders/` | `Exchanges.Of`, delivery to running parts (`ChannelReach`), the terminal's channel verb and its member forms, *At work*'s reading, the members' look |
| `Driver/Participants/`, `Driver/Loop/` | Helpers, partners and opinions: their admission, waits, person acts and runs; brainstorms and meetings at the look; the routing line (`RoutingStanding`) |
| `Driver/Course/` | The answers-alone count and its hold |
| `Modules/` | *What needs you*'s judge (`AttentionStateSlice`, `NeedsAPerson`), the steward desk, the join's and the members' routes |
| `Web/work/` (`conversation/`, `pieces/`, `team/`, `atWork/`), `Web/asks/`, `Web/notifications/StewardLine.tsx` | The composer's levels, the sender chip and exchange cards; the Pieces section; *Co-workers*; *At work* and *Waiting on*; an ask's channel and a teammate's; the steward's line |

The agent's half is the canon skill `canon/packs/co-working/skills/co-working/SKILL.md`; it also says what the person sees of an
agent's part (the sender marks, the wait counted once, a line's channel, the steward desk's history).

## Its rules

**Levels and delivery**

1. A message never stops a turn. Only a stop does: the person's *Stop*, *Stop turn*, keeping stop or a conversation's *Send
   now* that cuts in (rule 3), or Daoris's own timeouts, pauses, limits and pressed hand-overs. No agent stops another's work.
   (from D188)
2. A level says when a word may reach its reader, never its order. Ordinary waits for a natural point: the turn's end, between
   turns, the next part, or the agent's own read. Urgent goes at the next step where the door takes words then. Now cuts the step
   in flight where the agent steers. (from D188, D136)
3. A level is a request. On a door that takes words only at its turn's end, urgent waits for that end, and says so. Now goes into
   the running turn where the door can put it there and tell where it landed: the protocol door's `_session/steering` wherever the
   agent offers it (Claude's adapter, `codex-acp`), answered `injected` for the running turn or, on Codex, `startedNewTurn` for the
   next; the native wire's line with no priority, echoed under `--replay-user-messages` before the turn's result (the running turn)
   or after it (the next). The person is told which by a note, and a word that began the next turn holds the turn sent now until
   that turn ends. Where the door neither steers nor echoes but stops a turn keeping its session (a protocol agent with no steering
   extension), it cuts in: the word goes first among what waits, the turn is stopped, and the word is the next turn's first, its
   ask marked sent now. Never on a next-step door, nor with a word on its way. Now waits where the door does none of these (a
   driven native run). A text-only conversation honours no level. What each harness does with a word written mid-turn is measured
   in `docs/2026-10-03-steer-evidence.md` §8. (from D188, D136)
4. The person may send all three, urgent by default; now is theirs alone. Daoris sends ordinary, or urgent for a fact that makes
   the work in flight wrong. An agent posts ordinary or urgent, never now. (from D188)
5. Every word to a session is kept on its record's `said`, with its level, sender and producer key, before its sender is told. One
   dispatcher per session drains it in order. A key is kept once while its word waits. The dispatcher gives every keep a key (its
   producer's, else its own) and tries a keep the record never answered once more under it, so a word stored before the answer was
   lost comes back as that stored copy, held once; held in the dispatcher's process alone only where neither try is answered.
   (from D188, D212)
6. Receipts show only what a door observed: held, sent, taken, read, partly handed, answered, uncertain, not reached. *Answered*
   means a reply named the word; a turn that ended after taking it is *taken, turn ended*. Answered never means obeyed. (from D188, D225)
7. A word attempted before a restart and never confirmed is *uncertain*, and is never sent again unasked. (from D188)
8. A person's word reaches a session in any state: at its next step, at its turn's end, or on its record when parked or ended,
   which it goes on with. (from D137, D188)

**The channel and posts to one part**

9. Each ask has one channel for its sessions and the person: messages to everyone or to one of its repositories, and Daoris's
   lines read from the ask's quests. It is service records, travels within the ask's workspace, and carries no path or file. A
   teammate's ask, held on another machine, is posted to from here by the person and by a session here on its quest: the message
   is kept as this machine's own, at its level, and travels to the holder, where a teammate's person's message is a claim named
   as theirs that reopens nothing, never the holder's person's words. (from D167, D225)
10. A line on no ask posts on its own channel, on its entry's origin, which the person reads and posts on from *At work* (their
    words, to everyone on it or one repository, taken by a part at its look as theirs; a teammate's ask's line posts on that ask's
    channel). A part here on a teammate's ask is handed the mirrored messages at its look, claimed and acknowledged by its entry as
    on an ask held here. A helper and a reviewer have no channel: they neither post nor read. (from D225, D163, D155)
11. A part, and a member outside Daoris, posts at most 10 messages of at most 4,000 characters, and at most 3 urgent. A further
    urgent post is kept ordinary, and its poster is told. The person has no cap. (from D188, D167, D244)
12. A post may name one other part of the poster's own entry on this machine by its session. It is kept apart, never travels, and
    is for the head of that part's line: a successor inherits it, a sibling does not. Another line's part is refused `bad-part`;
    one on another machine `part-elsewhere`. An agent the person runs outside Daoris joins one channel, an ask's or a line's, as a
    member of its own: its press (`ask channel join <ask>` or `--line <session>`, the pipe's `CHANNEL_JOIN`) waits for the person's
    Confirm and is refused `inside` before any card from a process in an agents' job. At most 3 members hold a channel and 8 the
    machine (`too-many`). A member reads and posts under its own id (`o` and seven hex) at the agent-class
    `/api/channel/members/{id}/messages`, each message handed once by id, and names no part; one never admitted or gone is
    `not-member`. Each join and leave journals a `member` row. The person's page lists a line's members (the driver-class `GET
    /api/channel/members`) with whether its claims are spent: its ask has no quest open, no part of its line is active here and no
    helper a part asked still runs, judged as read. The look ends a member whose claims are spent, or quiet for 24 hours
    (`expired`); the person's *Remove* ends it `removed`, its own leave `left`. A member is woken by `channel.posted`, told from
    each `message` row, and subscribes with `events relay --once --kinds channel.posted --not-by <its id>`, so its own posts wake
    nothing. (from D225, D244)
13. Delivery is by identity. A part is handed what its entry has not acknowledged, by message id, and one part of the entry
    claims each message. The entry's mark is for display. (from D188)
14. A running part takes the person's channel message as their words, at their level. Another session's message reaches it as
    Daoris's words around that agent's claim: urgent at the next step where the door takes words; ordinary only riding a prompt
    the run sends anyway. (from D188, D167)
15. An unsolicited message never starts, reopens or extends a run. What a part cannot take waits for the entry's next part, whose
    start carries the newest 20. A claim never taken expires visibly once its ask has no quest open and no part runs. (from D167, D188)
16. A post asks nothing of anyone. Work, a change in another repository, and an answer the poster cannot go on without are
    quests. (from D167, D32, D79)

**Who a word is from**

17. Who said a word is read by one rule from its marks and the door it came through: person, Daoris, supervisor, channel,
    helper, partner, opinion or unattributed. A supplied author, a role's name or quoted text grants nothing. A member's message
    is an agent's claim, marked outside by its poster's id wherever it is read; no session door, the connector's among them,
    takes a member's id. (from D225, D177, D244)
18. A claim the person passed on stays the agent's claim. Daoris's words are marked `from: daoris` and the supervisor's as the
    supervisor's; neither is ever the person's. (from D225, D188, D208)
19. Which ask a line is on is its entry's origin: `ask:<id>`, else the publisher's origin, else `quest:<id>`; a conversation is
    `session:<id>`. A quest or publisher no record holds passes to the next rule, the part a session continues among them. The
    key is written once at the open and never rewritten. (from D167, D225)

**Requests and the exchanges**

20. What moves is one of three acts. Say expects nothing. A request names what it expects back: an answer, work, a piece, an
    opinion or a decision. Hand over expects a successor. (from D225)
21. `Exchanges.Of` is the one read of a line's requests, over helpers, pieces, questions, hand-overs, quests, opinions and
    go-aheads, walked back along `from`. It keeps delivery apart from outcome, and says a source it could not read. (from D225)
22. A helper's answer, a partner's and an opinion's findings reach the line's head through one door, kept once under
    `<case>:<id>`. An answered park takes each beside the person's answer. (from D225, D203)
23. A solicited answer may extend or reopen its asker's run once per ask. Go-ons by answers alone are counted durably, one
    admission per line; the person's or Daoris's word resets the count. (from D188, D225)
24. The count is a fact, not a limit. The line's answers-alone rule decides, seeded as *hold after 2 in a row* at the machine's
    place, which the person may move or turn off; a supervisor may read it. The hold's words say the number the line is held at
    (`answers.held`'s `n`), so a rule at 5 is never told a third. (from D225, D229)
25. A hand-over's brief says what the line waits on, and that is the successor's to take. (from D225, D163)
26. A go-ahead the person approved on an ask covers the same act from every session the ask's entry holds. (from D225)

**Helpers, partners and second opinions**

27. A co-worker is a record serving a part of a line in one role, read from its marks: reviewer (`opinion`), partner (`serves`
    with `piece`) or helper (`serves` alone). Parts, other repositories' sessions, meeting voices, subagents, Ask Daoris, intakes
    and brainstorm passes are not co-workers. (from D203, D212)
28. A helper is a read-only session on another agent that answers one question, once, from a clone of its own, and asks nobody.
    It starts when the asker's turn ends and reads uncommitted work too. (from D163, D203)
29. The `helpers` rule is per repository, else its workspace: `ask`, `on` or `off`. With none set, an agent's ask is `on` for
    agents Daoris has set up and signed in here; one naming none goes to another maker than the asker's. (from D163)
30. A driven lead that ends its turn on its own helper ask, with no question or go-ahead on the person, ends `completed`
    (`ended.awaits-helper`), held and unparked; the answer reopens it once. An answer kept on a part the line moved on from (a
    resume or a new part) goes with the line, said there as the helper's (`HelperAnswersCarried`). A helper ending with no answer brings it to the
    person. A helper the shell's close or the sweep cut off with no answer kept is approved again at the next start's look and
    runs anew while its asker waits, with no word from the person. (from D203)
31. A partner writes one piece in a clone with no remote, at the asked commit. Daoris reads its files and writes one frozen
    commit; the piece lands only when the lead merges it. At most two pieces are open per lead. A lead is a session working in a
    tree of its own, driven or a conversation, and not ended (refused `asker-ended`, `ended`; the page offers the ask only while
    the lead runs); a conversation hears a piece's answer as a turn of its own between its turns, as a helper's. Only the person
    asks one today. A handed piece is taken in when the look first reads its commit as an ancestor of the lead's tree's branch
    (recorded, never unset; a cherry-pick is not that), and not taken in when the lead's record completed without it; with the
    lead's tree gone it is read at the lead's landing, against the commit the landing recorded (`PieceLandedReads`). A partner's
    record lands at no door: each refuses it naming the lead's merge (`PieceLandings`). The person's *Discard…* (a piece holding
    work or a copy, never one taken in) and *Hand over what it left* (an uncertain one) are an act file, `<id>.act.json`
    (`SESSION_PARTNER_ACT`, `sessions partner <piece> --discard | --hand-over`), done by the look, the one writer of the state. A
    piece's ref (`refs/daoris/pieces/<id>`) and box (`data/partners/`) stand through a tidy, a sync, a landing, `git gc` and a
    clear: only *Discard…* removes the ref, and nothing the box. A clear of a closed quest keeps the unit as `needs-you` (context
    `piece`, naming the lead) while a piece one of its sessions asked for is still open (`HistoryPieces`). (from D199, D153)
32. A partner starts only where its confinement was measured in the configuration a start would hand it. (from D199)
33. A second opinion is the landing rule's to ask, never an agent's. Its findings are a solicited answer at ordinary, marked as
    another agent's claims. (from D155, D188)
34. One table says which tools a work may call. A helper, a partner and a reviewer have allow-lists of their own and no quest
    tools; a brainstorm's voice has only its give or synthesize tool. (from D203, D176, D212)

**Quests between repositories and the routing line**

35. Work another repository owns is a quest to it, named from the registry or from this repository's `uses`. A quest to its
    own repository must name a lane (`repository:lane`). (from D32, D115, D91)
36. A session that cannot go on without another repository's answer waits on that quest. Its own stays taken, and it resumes in
    its own tree with the outcome, at ordinary. (from D79, D188)
37. A session reads its workspace's other checkouts by default and writes into one only where the person declared it. A change,
    or what only that repository knows, is asked of it. (from D107, D79)
38. A lead or a chat is told one routing line at each start: open, one at a time, no lanes, or not driven. Told nothing, it
    works its rows itself. A serving record is told none. (from D203)
39. A lead that hands rows to its lanes commits, then finishes naming them, or waits on exactly one for its answer. A chat says
    what it published and never waits. (from D203)

**Brainstorms and meetings**

40. A brainstorm is a bounded driver operation: one frozen packet from Daoris's knowledge, shown to the person and sent by their
    press to agents of different makers; generate, synthesize, challenge; a ranked document kept on this machine, never a
    change. (from D176)
41. No pass may ask for a brainstorm, a helper or a reviewer, or post to a channel. Fan-out is Daoris's fixed plan, never an
    agent's. (from D176)
42. A meeting is the person and named agents in one conversation Daoris relays, each message attributed from its turn's binding.
    Turns go in the person's order, one at a time, and none reads another's message of its round. (from D177)
43. A meeting's next round goes only by the person's press, its packet refreshed under a new digest. A meeting talks: no
    checkout, no publishing, no write. (from D177)
44. The service's brainstorm desk is the one writer of a request, a send and a stop for both kinds. The driver's verbs press it,
    and with no service they say so and exit 1. A send is refused (`state`) for one stopped or ended unsent, as for one with no
    packet; only one already sent is answered as sent. (from D176, D177)

***At work*, *What needs you* and the steward desk**

45. *At work* draws the attended line from records, never conversation text: its parts, its co-workers (reviewers from records
    whose `serves` names the line), the quests it asked, its subagents and the last words of its channel, its ask's or, on no ask
    held here, its own, a message to one part naming it. *Waiting on* is the requests that hold; the head draws the same line,
    and its door opens *At work* at the first request the line names, that row outlined and focused. (from D203, D225)
46. A message is drawn whole once, at its home. Each word's turn is headed by its sender chip: who, by the one rule, and when it
    was taken. An agent's helper ask is one card at its call, and the answer folds into it; a helper's answer no card holds is
    that agent's block, under the question it answers. A channel message's home is the channel: the turn that took it names its
    poster and opens the channel, its words folded where the door is. (from D225)
47. *What needs you* has one row per obligation, keyed by its exchange's id or its kind and id. A park on one decision is that
    row, never a second. Rows are judged once in .NET and pushed. A session's foot counts its waits by the same keys
    (`Web/work/waits/waitObligations.ts`), so one wait is counted once on the foot, the bell, the places and *Notifications*; an
    unread message is counted on none. (from D225, D182)
48. The steward desk files any caller's `{act, target, why}` as claims and grants nothing; the act is judged from the holder
    alone. At most 10 open per caller and 100 in all; a restart reads open ones as interrupted. (from D209, D225)
49. A holder of a role the person appointed reads the asking agent's words as claims, never instructions. A go-ahead, a
    sign-in, access, spending, a deletion and a history rewrite stay the person's. (from D208, D209)

**The person's asks of a co-worker**

50. The person asks a co-worker by two doors besides the first ask: *Ask again…* names the answered ask it follows (`after`),
    whose question and answer its helper is handed quoted; and `to: person` keeps the answer on the member until *Pass to the
    lead* (`SESSION_HELPER_PASS`) hands it with `answers.passed` beside it. An ask for the lead keeps `helper.person-asked`, which
    resets the answers-alone count. The terminal's `sessions helper <id> "…" [--agent] [--after <ask>] [--to-me]` and `sessions
    pass <ask>`, and Ask Daoris's `session_propose` `helper` and `pass` doors, are those same doors, drawn as a `session` proposal
    card. (from D203, D225)
51. A lead's head has *Co-workers*: the lead, each member by state and where its answer went, its thread (*Back to lead*, *Ask
    again…*, the pass), the pending asks, and *Bring in another agent…* (*Answer a question*, *Write a piece* and *Second
    opinion*, the last two only for a session with a tree of its own; *Go on with another agent*; *Work alongside* a sentence with
    no press, since a second request on the ask runs alongside by itself). Each agent outside Daoris on the line's channel is a
    row (agent, repository, joined, last read or posted) with *Remove*; its message is headed *outside Daoris*, and a join's card
    says `untold` where the system would not place the terminal. The Pieces section under a lead's head shows each piece's state
    and acts, and *Ask another agent to work on a piece…* is the one ask its press and the head's ⋯ open. (from D203, D199, D244)

## Where a change goes

- **A new kind of request or wait**: a source in `Exchanges.Of` (`Driver/WorkOrders/Domain/`), with its record owned in
  Participants. *At work*, the head, the brief and *What needs you* read it from there; never add a second read.
- **When a word reaches an agent**: the course's inbox and turns (`Driver/Course/`) and the service's say door. Channel delivery
  is `Driver/WorkOrders/ChannelReach.cs` beside `Service/Asks/`.
- **Who a word is from**: all three sender readers and `fixtures/senders.json` in one change.
- **Which tools a work gets**: `Mcp/CallTable.cs`; a new tool needs its class in `Mcp/ToolClasses.cs`.
- **Helpers and partners**: `Driver/Participants/` (`Domain`, `Helpers`, `Partners`), `Driver/Loop/DriverHelpers.cs`,
  `DriverPartners.cs`, and the service's desks in `Service/Sessions/`.
- **Brainstorms and meetings**: `Driver/Loop/DriverBrainstorms.cs`, `DriverMeetings.cs`, `Driver/WorkOrders/Brainstorm*`, and
  the desks. **Routing**: `RoutingStanding` and `StandingBlock`; quest addressing is `Service/Quests/`.
- ***What needs you***: `Modules/Loop/AttentionStateSlice.cs` and `Web/work/attention/`. **The steward desk**:
  `Modules/Platform/StewardDesk.cs`.
- **What agents are told to do**: the canon skill, then sync this repository and the examples.

**Tests that pin it.** Driver: `ExchangesTests`, `ChannelReachTests`, `ChannelCommandTests`, `AnswersAlone*Tests`, the
`Helper*Tests`, `Partner*Tests` and `Piece*Tests` (some in the git half), `RoutingStandingTests`, the `AtWork*Tests`,
`Brainstorm*Tests`, `Meeting*Tests`, `StewardCommandTests`, `ChannelMemberVerbTests`. Service: the `Channel*Tests`,
`SendersTests`, `SolicitedAnswersTests`, `QuestExchangeTests`, the desks' tests, `McpChannelToolTests`, `ToolClassesTests`.
Modules: `AttentionStateSliceTests`, `StewardDeskTests`, `DriverModuleChannelTests`, `ChannelJoinPipeTests`. Web: beside each
part under `Web/work/`, `Web/asks/` and `Web/notifications/`.

**Gates that bite.** `driver`, `modules`, `service` and `censuses` (the tool-class and transitional censuses), `web-unit`, the web
gate's `i18n:check` and `names:check -- --strict` for any visible word (glossary: *urgency*, *channel*, *co-worker*, *At work*),
and `universal`. The twin fixtures (`senders.json`, `participant-work.json`, `helper-rules.json`) fail every side together.

## Contradictions

- D188 point 4 (the third answer alone waits, fixed) vs D225's note of 2026-11-03 (a fact, no number fixed; the note also
  misnames it "point 5's bound"): D225 and the code, which seeds a rule at 2 (`SupervisorRuleBook.AnswersAloneBound`).
- D188 point 7 (*answered*: the turn that took it ended) vs D225's COMMS1h note (that is *taken, turn ended*): D225.
- D188 point 6 (the channel's mark moves on taking) vs delivery by per-message acknowledgment (`ChannelReach`): the code.
- D167 point 7 (a message reaches a running session at its next step) vs D188 (only urgent does): D188. D167 point 6 and
  `ChannelWords.cs` ("any other origin has none") vs a line on no ask posting on its own channel: the code. `ChannelDesk`'s
  summary ("read at a session's next look") vs D188's levels: D188.
- The co-working skill ("the service refuses one past the bound") vs a fourth urgent post kept ordinary (`ChannelTerms.cs`): the
  code.
- D163 point 3 (helpers default `ask`) vs `on` for agents set up here (`HelperKey.cs`, D163's HELPERDEFAULT1 note): the code.
- D199 point 2 (the lead asks with `partner_ask`) vs no such tool (`ToolClasses.cs` has `partner_answer` alone): the code today.
- D203 point 7 (*What needs you* gains no kind) vs a `helper-ended` row standing in for the park row (D203's HELPERWAIT1c note):
  the code. D225's COMMS1i note (*At work* lists no reviewers) vs its later note and the code: the code.
- D136 point 4 (*Send now* stops the turn on a turn-end door) vs D188 (now stops nothing): a conversation's *Send now* follows
  D136 where its door stops a turn keeping it (`TurnCutIn.cs`); a driven session's follows D188.
- D176 point 1 (the driver's verb writes the request) vs its ONEWAY1t note (the service desk is the one writer): the note.
- D32's shape (a quest is a checklist item in the receiver's backlog) vs its amendment (the service holds quests; the CLI has no
  quest command): the amendment.

## Open

- **MSG1h**, **MSG1i**: an urgent word on Codex and on the native door could ride the measured next-step deliveries (the echoed
  line, the steer), said at `next-step`; today it waits for the turn's end.
- **COWORKUX1** (row i): the lead's wait on its own partner. The lead's own `partner_ask` has no row.
- **CHANNELJOIN1** (`docs/2026-11-06-event-channel-design.md` §5): the installed look.
- **AGENTROOM1b**: `meeting_say`, so a real agent takes a meeting turn (today refused `no-message-tool`); **1c** `daoris
  meeting`; **1d** one account's turns one at a time across meetings.
- **MEETUX1a–d**: a meeting from the window, its rounds and a brainstorm's document on their pages, a waiting meeting in *What
  needs you*.
- **BRAINSTORM1b2, 1b3, 1c, 1d, 1e, 1f, 1a2b, 1a4**: the CLI's stop and id, a limit on a real door, the measured run, a caller
  from another repository and the page, the skeptical pass, a workflow step, a no-connector pass test, a pass's traces.
- **STEWARDROLE1d**: the steward's activation (until then the desk answers `not-staffed`); **1e, 1f, 1g** its hand-over, go-on
  and set-up. **SUPERVISOR1a-iii**: the supervisor offered.
- **COWORKVIEW1** row f, after BRAINSTORM1d and AGENTROOM1b. **MSG1j**, **SESSONE1c-look**: the installed canaries.
- With no row: *Send to its session* from a line's own channel; D177's floor request.
