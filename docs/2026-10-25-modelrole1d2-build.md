# MODELROLE1d2 build record

Quest `#aec91ccbdc36`, what D205's row d still owed (the backlog's MODELROLE1d2; design `docs/2026-10-23-actors-design.md`
§3.5(a)'s curve move and §3.5(b)), on main after MODELROLE1d, LIMITGOON2 and MODELROLE1c3. The row lands in parts on one branch,
each committed green. This record says which parts are built and which remain.

## Parts

1. **The curve move judges each candidate's reading for the record's model** (§3.5(a); built). `SwitchCandidates` takes the
   conversation's agent, the version its record opened on and its model (the composer's `override` where one is kept, else its
   `asked`), and a candidate whose offer reading lacks or refused that model is not ready (`AgentOffers.Judge`), so the move never
   takes it.
2. **A conversation's pending start owns its attempt** (§3.5(b), `ChatPending`; built). A start that asks an entry of its actor's order
   on the protocol door (a new Chat, a new Ask Daoris, LIMITGOON2's part on another agent) owns a pending start keyed by the issued id,
   made where its record opens and held by the run that opens its door. Its agent's refusal of the model at the open is kept as the
   account's reading and re-asked within the start (`ChatReplacing.Refused`): a later entry of the same agent on the same account is set
   on the open session; a pick tries only its agent's other entries on its own account (`ActorChoices.PickAlternatives`). Anything else
   ends the open before its prompt: the refused record's turns give up their words under the pending start's lock (`ChatTurns.Yield`),
   the record ends `choice-not-offered` (`Observation.ChoiceNotOffered`), and once its run has let go its count is given back and the
   walk's answer opens in its place with that account passed (`ChatReplacing.ReplaceAsync`): a Chat in the same tree, continuing the
   refused record by `not-offered` (a new `HandOverWhy`, worded in both languages), told its own maker's trailer and, in a tree its line
   grew, the repository's process; Ask Daoris in the same workspace's room, linked in the room's history (`HelpConversations.Replaced`).
   The moved words go to the replacement's turns as they are made (`ChatPending.Arrived`), their files kept again in its folder, a word
   kept on the refused record taken off it by its id once on the wire; a word said meanwhile, to either id, waits with them, and every
   word door asked by the issued id answers for the current one (`ChatRunner.CurrentOf`). The replacement says where it came from
   (`chat.replaced`) and the runner tells `Replaced(from, to)`. A pick, or a start with nothing left, opens nothing: its words wait on
   the refused record, said why (`chat.choice-waits`).
3. **A `choice-not-offered` conversation's go-on is a fresh start of its actor** (§3.5(b)'s interruption, a pick, a refused
   continuation's brief; built). `ChatGoOn` reads the end code before anything else is judged, the person's press and the come-up alike:
   no conversation began there, so it is never resumed (its open did make a session, which today's go-on would have resumed at account
   default). A record this process's pending start already replaced answers that its replacement takes the words; any other goes on
   through `BriefedMove.NotOfferedAsync`: the actor's order walked afresh (the refusing account passed by the reading kept), a pick only
   its agent's entries on the account it was refused on, the new record continuing the refused one by `not-offered` (Ask Daoris in its
   workspace's room, linked in the history), the waiting words its first message, taken off the refused record by their ids. Where its
   line, by `from` or the room's history, reaches an earlier part that ran a conversation, past every `choice-not-offered` part, Daoris's
   brief of that part's line is composed again and handed before the words. Nothing that can run: the words wait, said in the wait's
   sentence with each choice's reason.
4. **The window follows the replacement** (built). The loop tells the runner's `Replaced` as the event `SESSION_REPLACED` {from, to},
   and the words door (`SessionWords.SayAsync`) answers the issued id for the record its start is on now, or holds the words while it
   moves. On the page, `useSessionReplaced` reads the event defensively (`replacedOf`): the Work frame showing the refused conversation
   attends to its replacement, and Ask Daoris moves what it chose, opened ahead or gave its first words to (`useHelpReadied().replaced`).

## Checks run

Part 1: driver `CurveMoveTests` (the new case seen failing first: the move took `account-3`, whose reading refused `opus`), and
`CurveMoveProcessTests` with `process.runsettings`.

Part 2: the new Process class `PendingStartProcessTests` with `process.runsettings` (Ask Daoris refused after its press answered, the
words reaching its replacement once in the same workspace; a Chat whose first entry is refused running its second on another agent; a
pick never walked afresh), each of its three cases seen failing first with the pending start left out of the run (each timed out
waiting for what the row adds); beside it `ProtocolChatTests`, `HelpChatTests`, `LimitGoesElsewhereProcessTests`,
`LimitGoesOnProcessTests`, `ChatGoesOnProcessTests`, `HandOverProcessTests`, `ChatRunnerTests`, `ChatWordsRestartProcessTests`; the fast
classes under `Conversations`, `ActorChoices`, `Note`, `HandOver` and `Help` (`NoteCodesTests`' reason census gaining `not-offered`); the
web's `src/locales`.

Part 3: `PendingStartProcessTests`' four interrupted cases (a refused Chat's go-on after a restart starting the walk's answer with the
words; a picked start never run elsewhere by the come-up; a refused continuation's brief, for a Chat and for Ask Daoris, carrying a fact
only part A's history held before the words), each seen failing first with the go-on's new branch left out (each ran the refused agent
again at account default, resuming or briefing on `account-1`); then the whole class, the Process neighbours of part 2 with
`HelpGoOnTests`, `BriefedMove*` and `CurveMoveProcessTests`, and the fast classes of part 2.

Part 4: the web's new `WorkFrame.replaced.test.tsx` and the Ask Daoris scope suite's new case, each seen failing with its follow left out;
`bridge/help.test.ts`' event reading and the opened-ahead move; `src/locales`, `src/bridge`, `AskDaoris*`, the Work frame's suites; the
typecheck, `i18n:check`, `names-check.mjs --strict`, `check:web-rules`; modules `SessionWords*`, `DriverModuleConversation*`,
`DriverModuleHelp*`.

## Readings
