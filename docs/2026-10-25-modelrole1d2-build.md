# MODELROLE1d2 build record

Quest `#006b2be2d299`, the backlog's MODELROLE1d2 (*what row d still owes*), D205 §3.5(b) and the curve move's reading of §3.5(a), on
main after MODELROLE1d and MODELROLE1c3. Built in parts on one branch, each committed green. This record says which parts are built
and which remain.

## Parts

1. **The curve move judges each candidate's reading** (built). `HarnessRoster.SwitchCandidates` takes the conversation's agent, its
   version and the model its record asks, the kept `override`'s where one is (`ActorAsked.Overlaid`): a candidate whose offer reading
   lacks or refused that model is not ready (`AgentOffers.Judge`), so `CurveMove` never takes an account that does not offer the
   chat's model.
2. **A conversation's first open re-asks within the same press** (built). `ChatPending`, keyed by the issued id, owns a Chat's or
   Ask Daoris's start that asks its entry on the protocol door, from placement until it is dispatched or exhausted, carrying the
   order read once (a pick's recovery order: the picked entry, then its agent's other entries, each still a pick, on the picked
   account), the passes and the refusing account. Refused at the open, the re-ask keeps the account's `refused` reading, walks the
   order again with that account passed and no count of its own: a later entry of the same agent on the same account is set on the
   same session; anything else ends the record `choice-not-offered` (no strike, no *Ended* notice), its words moved out of its
   queue under the queue's lock (`ChatTurns.MoveOut`), its count given back, and the replacement opened in the same call: a Chat
   in the tree the refused one ran in, continuing it by `not-offered`; Ask Daoris in its workspace's room, linked in the room's
   history (`HelpConversations.NotOffered`). The moved words, and each said meanwhile, reach the replacement once, in the order
   said, taken off the refused record by their ids as its turn takes them. The press's id answers for the replacement
   (`ChatRunner.CurrentOf`, read by `SessionWords`), and the shell tells `CHAT_REPLACED { from, to }`. Nothing left, the
   conversation says why it did not go on and the words wait on the refused record.
3. **A `choice-not-offered` chat's go-on is a fresh start of its actor** (built). A record read as ended `choice-not-offered`
   (`PriorSession.NotOffered`, by its end code) is never resumed: `ChatGoOn` hands it to `BriefedMove.NotOfferedAsync`, the press's
   go-on and the come-up's alike, which walks its actor's order afresh (Chat in the refused record's tree, Ask Daoris in its
   workspace's room) with the record as `from` (`not-offered`) and its waiting words as the first message, taken off it by their ids.
   A pick is never walked afresh: its recovery order on its own account, the refused entry passed by the reading kept, else it
   waits saying why. A refused continuation, whose line holds an earlier part that held a conversation, is handed Daoris's brief
   of its line first (`ContextBriefs.ComposeAsync`), then the words; a refused new conversation goes on with its words alone.
4. **The window follows `chat.replaced`** (to do).

## Checks run

Part 1: `CurveMoveTests` (the refusing candidate seen moved to before the change), `CurveMoveProcessTests` with `process.runsettings`.

Part 2: the new Process class `ChatPendingProcessTests` with `process.runsettings`, its four cases seen failing first with the
pending start switched off (each ran at account default, as row d left it): a Chat refused on its first account going on on the
second in one press, its first words and a word said while the replacement opened each reaching it once; Ask Daoris refused after
its press returned going on in its room, its history linking the two; nothing left, the words waiting on the refused record; a pick
taking its agent's next entry on its own account. Beside it `ProtocolChatTests`, `HelpChatTests`, `CurveMoveProcessTests`,
`LimitGoesElsewhereProcessTests`, `LimitGoesOnProcessTests`, `ChatGoesOnProcessTests`, `HandOverProcessTests`, `ChatRunnerTests`,
`LimitWaitsProcessTests`, `ChatWordsRestartProcessTests` (88 passed); the fast `ChatGoOnTests`, `HelpPlaceWorkspaceTests`,
`HandOverRunnerTests`, `CurveMoveTests`, `NoteSitesTests`, `StructureTests`; modules `DriverModuleConversationTests`,
`DriverModuleConversationProcessTests`, `DriverModuleHelpStartTests`, `DriverModuleSessionsTests`, `SessionWordsTerminalTests`,
`DriverModuleHelpHistoryTests`.

Part 3: `ChatPendingProcessTests`' three go-on cases, seen failing first with the branch switched off (each went on as a lost
conversation, on the refusing account at account default): the come-up's go-on of a refused chat opening the walk's answer on the
second account with its words once; a refused pick never running on another account; a refused continuation's go-on handed a fact
only part A's history held before the words. Beside them, both halves of `AccessCarriedTests`, `ChatGoOnTests`,
`ChatGoesOnProcessTests`, `DrivenAccessTests`, `GoOnBriefedTests`, `NoteSitesTests`, `OpinionDeliveryTests`,
`SessionWordsWaitingTests`, `HelpGoOnTests`, `LimitWaitsProcessTests`, `LimitGoesOnProcessTests`, `LimitGoesElsewhereProcessTests`,
`ChatWordsRestartProcessTests`, `PlannerTests`, `ServiceClientTests` (245 and 43 passed).
