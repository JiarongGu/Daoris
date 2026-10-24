# A terminal's press waits until the person answers: no two minutes, no ten, a row in Notifications until it is confirmed, refused or withdrawn, kept across a restart (proposed, round 2 with its named changes, 2026-10-24)

**Standing:** proposed, round 2 with its named changes, a note amending D156 (CONFIRMWAIT1), under D191. Round 1 was written by Claude
Opus 5.5 from the code on main at `aa16658e7` and read at `c44121e2a` by the independent Opus foundation reader alone, its read verbatim
in `docs/2026-10-24-confirm-wait-reads.md`: 25 findings, 7 blocking, another round; Codex's read is owed, its credits out. Round 2 is
written by Claude Opus 5.5 from the code on main at `ffcf42926`, where nothing round 1 or the read cites has changed (the one commit
touching the desktop since `6accf4a01` is HOOKMERGE1's, in the merge's tree code). It answers every finding in §13, each checked
against that code. Round 2 was read at `48c614823` by the Opus foundation reader alone: 20 findings, 2 blocking, accept after named
changes the steward checks without another read. Its named changes are taken here by Claude Opus 5.5 from the code on main at
`1248aa393`, where the one change to what the design cites is SUPERVISOR1b's (`SUPERVISOR_DEFER` a second `Role` route, and
`SessionsRoutes.cs`' lines shifted, corrected in §5.1); §14 says where each went. Nothing here is built.

The row CONFIRMWAIT1 in `TASKS.md` carries the case: a press from a terminal that needs the window's Confirm (a setup, a repository
driven, a session's words) ends two minutes after its card shows, or ten if no window shows it, and *Nothing was done*. On 2026-10-24
several were missed that way and each had to be sent again (a setup, one repository's drive twice, a session's go-on). The row's
direction: a press waits as a row in *Notifications* until the person confirms or refuses it, or its asker withdraws it; it is made
when confirmed and its asker told then; a press whose grounds changed while it waited is refused at the Confirm with its reason.

D156's CONFIRMSEEN1 note (2026-10-21) rejected exactly this: *a wait with no bound was rejected because the terminal's verb would
hold its line indefinitely* (`docs/decisions/D156.md:249-250`). This design reverses that rejection, and §3 says why its reason no
longer binds.

**What this is not.** TERMTRUST1 (D201, accepted) admits a press the person's standing confirmation covers before any card is asked
(`InstancePipe.cs:621-631`), and that path is the standing confirmation's alone. In this build it admits nothing: the grant is not
offered (`StandingConfirmation.OfferedInThisBuild = false`, `StandingConfirmation.cs:193`) and the caller's look answers `untold` until
D201's part (i) lands (`:160-162`). STEWARDROLE1 (D209, proposed) gives the steward's acts a route of their own of class `Role`, judged
and refused where uncovered as `SUPERVISOR_ANSWER` is (`InstancePipe.cs:559-571`) and never asked; D209 point 2 makes D201's four
presses the steward's. So **until STEWARDROLE1d lands, every development press from a terminal is a card**, and under this design none
of them ends by a clock. This design does not touch either admission; a press either admits never waits here. A waiting press marks
no `owner-set` (D209's mark is made when a confirmed card is dispatched, the steward design §5.4), so a steward's act during the wait
is not refused by it, and shows at the Confirm as a ground that changed (§5).

**After STEWARDROLE1d.** A role's terminal makes no card: there every press verb refuses `role-terminal` (`D209.md:73`). The person's
own terminal and an agent's job outside any role still make cards, and the owner's three cases from 2026-10-24 stay cards from every
terminal until their steward acts land: the hand-over at STEWARDROLE1e, the go-on at 1f and the set-up at 1g (the steward design's rows,
`docs/2026-10-24-steward-role-design.md:976-1017`). This design's waiting is for those cards, and it stays for every `Person` route no
role takes.

A path without a root is under `src/Daoris.Desktop/Daoris.Desktop.Modules/`; `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`,
`Host/` is `src/Daoris.Desktop/Daoris.Desktop.Driver.Host/`, and page paths are under `src/Daoris.Web/src/`.

**Words.** The English chrome keeps *a terminal's press*, as `notifications.done.expired` says it today (`locales/en/notifications.json:39`):
the glossary's `request` row says *request* is not a name on the window (`locales/glossary.json:488`). The terminal's own sentences say
*request {id}*, as every verb's do today (`InstanceDoor.cs:273-274`). In this design *a press* is one asked over the pipe for the
person's Confirm, and *its id* is the id the terminal sent it under.

## 1. Today, read from the code

### 1.1 Where the two minutes and the ten live

- **`PipeConfirmations`** (`Platform/PipeConfirmations.cs`) holds every waiting press in memory, never in the store
  (`:55-58`). Its constructor takes `within`, two minutes by default (`:123`), and `find`, never less than `within` (`:125-126`).
- **`FindWithin`** is ten minutes (`:133`): a press asked waits that long to be shown (`Ask`, `:159-185`, its expiry `asked + _find`
  and its timer made under the same lock). Each confirmation has an id of its own, a GUID the instance mints (`:168`), beside the id
  the terminal sent.
- **The shown timer**: the window's first word that the card is in front of the person moves the expiry to `now + _within` and
  resets the timer (`Show`, `:191-228`, the change at `:212-213`). An answer or a word at or past its time ends it `Expired`, the
  clock read first (`End`, `:320-339`; NOTIFY2a). The timer's firing is `Expire` (`:314`).
- **The pipe's limits** repeat both: `PipeLimits.Waiting` five and `PipeLimits.Confirm` two minutes (`Platform/InstancePipe.cs:42-46`),
  handed to `PipeConfirmations` at `:199`. The pipe takes eight connections (`PipeLimits.Connections`, `:19`).
- **The service's own door** has its own two minutes and its own five (`src/Daoris.Service/Daoris.Service.Http/PersonConfirmations.cs:124`,
  `Most`; `:127`, `Lasts`; `:130`, `Kept`). It grants a secret to send one HTTP request once; no verb of this machine asks it (only the
  desktop's reads, `Driver/Service/ConfirmationDoors.cs:11-15`). It is D156 point 4's door, and out of this design's scope (§11). The
  window's card draws both doors' presses (`person/Confirmations.tsx:25`, `source: 'pipe' | 'service'`).

### 1.2 The terminal's wait and its exit codes

- `InstanceWorld.Wait` is thirty minutes (`Driver/Platform/InstanceDoor.cs:62`). `PressAsync` (`:234`) keeps the press's id and key
  before sending it (`SentPresses`, `:253`), sends it with that wait, and on `CONFIRMING` writes *waiting for you to confirm this in
  Daoris's window…* (`Told`, `:487-502`), naming no id. `Told` reads only notifications whose `id` is the verb's own (`:495`).
- An answer lost, the connection's or the wait's, is asked for by `RESULT` on a new connection, never sent again (`:262-279`):
  `answered` is said; `unknown` says *Daoris does not know how it ended … it stopped while this was under way* and every other case,
  `running` or no pipe to ask, says *Daoris was still working on it*, both exit 2 (`:271-276`).
- A refusal is `message (code)` on the error stream; `refused` and `expired` are exit 1, every other refusal exit 2 (`:286-288`).
- `daoris-driver result <id>` (`ResultAsync`, `:413-465`) reads a kept press's answer by its key; a refused answer there is exit 2
  (`RefusedAsync`, `:468-472`), not the 1 `PressAsync` gives the same refusal. Its usage line is the host's
  (`Driver/Loop/DriverCommand.cs:78`), held by `DriverCommandTests.The_usage_names_every_verb_the_host_answers` (`DriverCommandTests.cs:101`).
- `SentPresses.Kept` is 24 hours from when it was sent (`Driver/Platform/SentPresses.cs:21`), read by `Find`'s age (`:52`) and
  `Prune`'s file time (`:74`). Only an id of letters, digits and `-` is kept (`:63`).
- **Ctrl+C withdraws nothing.** When the token is cancelled, `InstanceClient.RequestAsync` returns null and sends nothing
  (`Driver/Platform/InstanceClient.cs:134-176`); no client writes the kit's `CANCEL`. Each family's Ctrl+C sentence says so: *A change
  already sent to Daoris's window may still be confirmed there* (`Host/AgentsConsole.cs:22-27`; `DriverChoicesConsole.cs`,
  `PluginConsole.cs`, `ToolsConsole.cs` alike). RPC1h built only the instance's half.
- Today no press can still wait when the thirty minutes end: ten minutes to be found plus two once shown is twelve.

### 1.3 The journal: an id sent again says how it ended, and nothing runs twice

- `PipeJournal` (`Modules/Platform/PipeJournal.cs:22-36`) writes a line when a press begins and one when it ends, with its answer,
  one file per UTC day under `<home>/pipe/`, kept 24 hours (`PipeLimits.Kept`, `InstancePipe.cs:31`; `Find`, `PipeJournal.cs:74`;
  the read at the start, `:189`). Read once at the start, an id whose last line says it began is `Interrupted` (`:28-30`).
- In `Accept`, an id running is joined by the key that sent it; an id the journal holds answers its kept answer, or *Daoris stopped
  while this was under way … it was not run again*; another binding is refused `id-taken` (`InstancePipe.cs:590-611`). The id is
  taken as sent, any text of 1 to 128 characters (`:551`).
- **A waiting press is not in the journal.** It is in `_running` and `_waitingFor` alone (`:604-609`, `:652-655`); its line is
  written only once it ends (`ConfirmThenRunAsync`, `:686-726`, the journal at `:710-718`, a failed write swallowed) or begins to run
  (`RunAsync`, `:763-799`). While it waits, `RESULT` answers `running` from `_running` (`:859-862`).
- The kit's `CANCEL` from the terminal that sent it, by its key, withdraws a waiting press (`CancelAsync`, `:809-847`, the withdrawal
  at `:838-843`); **for a press already confirmed and running, the same `CANCEL` goes to the dispatcher, which aborts it** (`:845-846`).

### 1.4 Five waiting, and confirmations-full

`Ask` returns null where `Bound` already wait (`PipeConfirmations.cs:165`), and the pipe refuses the sixth `confirmations-full`:
*Five acts already wait for the person's confirmation, so this one was not asked. Nothing was kept.* (`InstancePipe.cs:637-651`).
Only what waits counts, so one answered frees its place. The five came with RPC1c (`D156.md:189-190`), the pipe's copy of the service
door's five.

### 1.5 Shown, the census, the balloon and What needs you

- `PERSON_CONFIRM_SHOWN` is the window's route (`Bridge/Routes/PersonRoutes.cs:56-72`) and `Window` in the pipe's census
  (`Platform/PipeCensus.cs:278-279`, its comment *a terminal saying it would start its two minutes*). `PERSON_CONFIRMATIONS` and
  `PERSON_CONFIRM` are the window's too (`:273`).
- `PERSON_CONFIRM` answers a press no longer waiting as data, `gone`, *answered in another window, its two minutes passed, or its
  terminal took it back* (`PersonRoutes.cs:35-54`; `locales/en/person.json:38`).
- On ask, `Attend` raises `SESSION_ATTENTION` of kind `Confirming` (`PipeConfirmations.cs:181-182`, `:372-381`), which `SessionBalloon`
  shows while no window is in front (`Windows/SessionBalloon.cs:132-134`; `notifications.system.confirming.*`). Every ask raises one.
- Each waiting press is a `confirmation` row of *What needs you*, judged in .NET with no workspace (`Loop/NeedsAPerson.cs:464-481`),
  its fields `module`, `route`, `asker`, `expires`, `shownAt` (`Loop/AttentionStateSlice.cs:116`), worded *a terminal waits* and *It
  runs only once you confirm it on its card in Notifications, and is refused at {{time}}* (`locales/en/work.attention.json:112-113`),
  worded by `work/attention/attention.ts:520-522` and `:906`. **No screen draws it**: `needsListed` drops every `confirmation` row
  (`attention.ts:114-116`), for the band in view (`AttentionBand.tsx:152-153`), Overview's summary (`overview/NeedsSummary.tsx:63`), the
  place counts (`attention.ts:185`) and the app's count (`app/App.tsx:513`), since the press's card is drawn from the confirmation
  channel. The twin test of the slice and the page compares a row's own fields and an account's
  (`AttentionStateSliceTests.cs:1648-1656`), not `confirmation`'s.
- The card says *refused by itself in {{time}}* and *It waits until {{time}}, then is refused* (`person.json:28`, `:33`), drawn from
  `expires` (`person/Confirmations.tsx:44`, `:272`, `:332`) for both doors.
- A press that ends `Expired` becomes a notice in *Done and failed* (`Unanswered`, `PipeConfirmations.cs:142`, `:355`; wired at
  `Loop/DriverLoop.cs:842`; kept by `Loop/NoticesStateSlice.cs:198-203`), worded *A terminal's press to {{act}} went unanswered, and ran
  out of time* (`locales/en/notifications.json:39`).

### 1.6 What a restart does to a press still waiting

- **A close**: `BeginStopping` refuses every new press and withdraws every waiting one (`InstancePipe.cs:275-279`, called from
  `DriverLoop.cs:1353`, and from `Dispose`, `InstancePipe.cs:339`). `ConfirmThenRunAsync` answers it `stopping`, *Daoris is closing;
  run this again once it is back*, and journals that ending (`:706-718`). A terminal still connected hears it; one that left reads it by
  `result`. **The person is told nothing**: `Unanswered` fires only for `Expired` (`PipeConfirmations.cs:355`), so the row and the card
  vanish with no notice.
- **An update's drain** (`IsStopping`, `:225`) leaves a waiting press waiting, but a Confirm during the drain ends it refused
  `stopping` and runs nothing (`:695`, `:706`).
- **A crash** writes nothing: the press was never journaled. A verb still on its line hears the connection drop, finds no pipe to ask
  `RESULT` of, and says *Daoris was still working on it* (`InstanceDoor.cs:274`); a later `result <id>` finds `unknown` and says *it
  stopped while this was under way* (`:461-462`), which is wrong — it never began. The person is told nothing.
- **A pipe not served** (not Windows, the home unreadable, the name held: `Serve`, `InstancePipe.cs:237-255`) takes no press at all.

So today a press waiting at a close is told ended to its asker and silently lost to the person, and one waiting at a crash is
silently lost to both.

## 2. The rule

**A press waits until it ends, and it ends one of six ways, each said to its asker and kept in its record:**

| End | By | What runs | The asker hears (code, exit) | The person sees |
|---|---|---|---|---|
| Confirmed | the person's Confirm, its grounds holding (§5) | the press, once | its answer, as today | the card goes; what the press changed |
| Refused | the person's Refuse | nothing | `refused`, 1 | the card goes |
| Changed | the person's Confirm, a ground no longer holding (§5) | nothing | `changed` with its reason, 1 | the reason, where the card was, and a toast |
| Withdrawn | its asker (§6) | nothing | `withdrawn`, 1 | the row goes; a notice where a window showed it (§8.3) |
| Lost | a restart that could not take it back (§7) | nothing | `lost`, 1, where its id read | a notice in *Done and failed* |
| Interrupted | a stop while it ran, once confirmed (§7) | what ran before the stop | `interrupted`, as today's journal says | as any press under way at a stop |

There is no time end for a pipe's press. `Expired` is no longer made by the pipe; the code stays a word of the wire, as
`confirm-in-window` stayed (`InstancePipe.cs:110-113`), for a terminal talking to an instance from before. The service door's
presses keep their two minutes (§11).

The task row's *declines* is the card's **Refuse** (拒绝), the word the card already uses (`person.json:36`), so the chrome keeps
one word for it.

## 3. Why D156's rejection no longer binds

CONFIRMSEEN1 rejected a wait with no bound because *the terminal's verb would hold its line indefinitely*. That held while the press
existed only as the verb's open request: the instance kept it in memory beside the connection (§1.3), the verb learned its end only
on that line or by `RESULT` within the line's day, and a verb that gave up had no way back to it. Three changes take that away, and
each holds only with the others:

1. **The press outlives the terminal's wait.** It is a record of its own, written when it is asked (§7.1), a row of *What needs you*
   and a card in *Notifications* (§8), whether or not any terminal is still connected. The connection is how an asker hears, never
   what keeps the press.
2. **The asker learns the id at once.** The `CONFIRMING` line names the id and the two verbs that read and withdraw it the moment the
   press is asked (§4.1), before any wait, since an agent's harness ends a tool call long before thirty minutes. The verb then waits
   at most thirty minutes (`--wait`, §4.1) and returns with the press still asked. Nothing holds its line longer than the thirty
   minutes a verb already waits (`InstanceDoor.cs:62`), but for those thirty minutes each waiting verb holds one of the pipe's
   connections, so they, not the cards, are the bound then (§9.2). This change is row 1b's, so 1b lands before 1a-i (§10): without it
   a verb from before misreads `waiting`, and an asker whose harness ended the verb never learns the id.
3. **The asker reads how it ended from the press's record.** `RESULT` answers `waiting` while it waits and its ending after (§4.2),
   from the record the instance keeps as long as it waits and a day after it ends. A terminal still waiting on its line hears the
   ending there, as today.

The two minutes once shown stay rejected as a bound for the reason the row gives: they ended presses the person had not refused.
What CONFIRMSEEN1 meant to keep — a card in front of the person never answered by default — is kept by the card's default being no
(§8.1), by the grounds (§5), and by the bound on how many wait (§9), not by a clock.

What the clock also did, and nothing did on purpose, was drop a press whose asker no longer wanted it. That is the asker's withdrawal
(§6), and the card says whether any terminal still waits on it (§8.1), so the person can refuse one whose asker has gone.

## 4. The asker's side

### 4.1 The verb

- **The id at once.** On `CONFIRMING` the verb writes, on the error stream and flushed: *waiting for you to confirm this in Daoris's
  window (request {id}); `daoris-driver result {id}` says how it ended, and `daoris-driver withdraw {id}` takes it back…* — the
  standing confirmation's *why*, where it is said, kept beside it (`InstanceDoor.cs:497-500`). The `CONFIRMING` payload loses
  `expires` and gains nothing: the verb names its own id, which it already holds.
- **`--wait <minutes>`**, 0 to 30, taken from the arguments in the host's entry before the verb is dispatched, and handed to the world
  the verb is given: `InstanceHost.World(home, verb)` takes no wait today (`Host/InstanceHost.cs:10-13`) and has 23 callers, so the entry
  sets it once on the `InstanceWorld` they make rather than each passing it. Every verb that presses has it; `--no-wait` is `--wait 0`:
  the verb returns once the instance says `CONFIRMING`. The default stays thirty. A wait longer than the default is not offered: a verb on
  its line holds one of the pipe's connections (§9.2), and `result {id}` reads it later.
- **At the end of its wait, a press still waiting is not lost and not an error.** The verb writes *still waiting for your confirmation
  in Daoris's window (request {id}); `daoris-driver result {id}` says how it ended, and `daoris-driver withdraw {id}` takes it back
  (waiting)*, and exits **1**: nothing was done yet, and a caller that reads non-zero as *not done* is right. Today's `running` path
  (exit 2, *still working on it*) stays for a press under way, not one waiting.
- **The verb tells the two apart by `RESULT`'s state**, which `PressAsync` already asks at every lost answer, its own wait's end included
  (`InstanceDoor.cs:262-264`): `waiting` is the sentence above, `running` today's, and `answered` the answer. The `CONFIRMING` it heard
  does not tell: a press confirmed during the verb's wait and now running was preceded by one too.
- **`already`** (§5.1) is exit 0, a branch of its own beside the refusals' exit codes (`InstanceDoor.cs:288`), with its sentence on
  the error stream.
- **At a close**, the instance ends each line waiting on a press with the error code `waiting`, `why: "close"`, *Daoris is closing;
  this still waits for your confirmation (request {id})* (§7.2). The new verb says it as its own wait's end, exit 1; an old verb reads
  an unknown code as a refusal, exit 2, and its sentence still says the press waits.
- **Ctrl+C still withdraws nothing**, as today (§1.2): the person's interrupt ends the verb, and the press stays asked. The four Ctrl+C
  sentences gain *`daoris-driver withdraw <id>` takes it back* beside *`result <id>` asks how it ended*. Withdrawing on Ctrl+C was
  rejected (§11): an agent's harness that kills its tool call sends the same signal, and would take back what the person may still want.

### 4.2 The record it reads

- `RESULT { id, key }` gains the state `waiting`, with `asked`, `waitedOn` (§8.1) and, once shown, `shownAt`; then `answered` with its
  ending as today. While the Confirm's grounds are judged and once it runs, it answers `running`, as today.
- `daoris-driver result <id>` says `waiting` as the verb's own end does, exit 1, and says the press's ending: the answer's words where
  confirmed and run; `refused`, `changed` (with its reason), `withdrawn` and `lost` exit **1**, aligned with `PressAsync`
  (`InstanceDoor.cs:288`), where today every refusal there exits 2 (`:468-472`). Other refusals stay exit 2.
- `unknown` says what is so: *Daoris has no ending for request {id}: it keeps an ending a day, and a press it never began is not in it*,
  not *it stopped while this was under way* (`InstanceDoor.cs:461-462`).
- `SentPresses` keeps a press whose last read state was `waiting` past its 24 hours, at most thirty days from when it was sent, until a
  read finds it ended or the instance answers `unknown`; then the 24 hours run from that read. Both `Find`'s age (`SentPresses.cs:52`)
  and `Prune`'s file time (`:74`) read the same rule: the record gains `waitingAt`, rewritten by each read that answers `waiting`. A
  press is kept under the terminal's home as today (`SentPresses.cs:23`).

### 4.3 Withdrawing one

`daoris-driver withdraw <id>` sends a new pipe route, **`DAORIS.PIPE` `WITHDRAW { id, key }`**, beside `RESULT` (`InstancePipe.cs:525-531`),
with the kept key on a new connection. It ends **only a press still waiting**, under `PipeConfirmations`' lock, where a Confirm is taken
from what waits too, so the first of the two wins:

- waiting, and the key the one that sent it: it ends `Withdrawn`, its card and row go, and it answers `withdrawn`;
- confirmed meanwhile (its grounds being judged, or running): answers `running`, and **nothing is cancelled**: the person's Confirm is
  not undone by an asker's later word;
- ended: answers `ended` and the ending, said as *it had already ended:* and its ending;
- another key's, or an id the pipe does not know: refused `id-taken`, as `CancelAsync` refuses today.

The kit's `CANCEL` stays what it is (`InstancePipe.cs:536-538`, `:809-847`), for a terminal from before; no client of this machine
sends it. The verb's usage line joins `result <id>` in `DriverCommand.Usage` (`DriverCommand.cs:78`), held by
`DriverCommandTests.The_usage_names_every_verb_the_host_answers` (`DriverCommandTests.cs:101`). It is a top-level verb, apart from
`plugins withdraw <id> <server>` (`:313`), which withdraws a plugin's grant.

## 5. A press whose grounds changed while it waited

A press asked at 09:00 and confirmed at 15:00 may name a world that moved. The route's own handler still judges what it always did;
what it cannot know is what the person was shown and what the asker saw when it asked. So **every person route names its grounds**,
read at the ask and read again at the Confirm, and a Confirm whose grounds no longer hold ends the press `Changed`, runs nothing and
says why.

### 5.1 The grounds

| Ground | Read at the ask, by the payload's named field | Read by | Holds at the Confirm when | The reason said (en) |
|---|---|---|---|---|
| `session` | the session's record: its `State` and its `Updated` (`Driver/Trees/SessionGroups.cs:14`, `:35-36`) | the service's record of the session, as `SESSION_QUESTION` reads one (`service.RecordAsync`, `SessionsRoutes.cs:367-372`) | the session exists, and both are the same | *the session moved on while this waited: it was {then}, it is {now}*, or, the state the same, *the session ran another turn while this waited* |
| `setting` | the setting's value where it is kept, and the payload's field holding the value it sets | the read the route's row names (§5.1's setting readers below) | the value is the same | *what this would set changed while this waited ({act})* |
| `repository` | that the repository (or workspace) is registered here | the service's registry, as `CheckoutsAndSessionsAsync` reads it (`LinesRoutes.cs:791-793`) | it still is | *the repository {repository} is no longer registered on this machine* |
| `quest` | the quest's state | the service's quest by its id, as `WORK_PLAN` reads its quests' `Status` (`WorkRoutes.cs:37-70`) | the same | *the quest moved on while this waited: it was {then}, it is {now}* |
| `ask` | the ask's state | the service's ask by its id, as `ASK_LANDING` reads its entries (`EntriesRoutes.cs:67`) | the same | *the ask was closed while this waited* |
| `plan` | the fingerprint of the fields the route's row names in the projection table below, from the read it names; never a field the row leaves out | the read the row names, called in process with the same payload, within the bound the row names | the read gives the same fingerprint | *what this would act on changed while this waited; look at it again and ask again* |
| `branch` | the tip of the branch the press lands, hands off or discards, read where the row says | one `git rev-parse` in the repository's checkout | the tip is the same | *the branch {branch} gained or lost commits while this waited* |
| `wait` | that the held wait the press answers is open: its id as the payload names it | the open read the row names (§5.1's wait readers below) | it is still open, under the same id | *what this answers no longer waits: it was answered or ended* |
| `timeless` | nothing | — | always | — |

**`timeless` is only for a press that deletes nothing, moves no line or branch, and starts no work**: marking news read, a read mark on an
ask's channel, a conversation's name or pin. Every press that deletes, moves refs, lands, sweeps, frees space or abandons work names `plan`, `branch`,
`session` or `wait`, and none is timeless. `SESSION_DELETE` is the one delete resting on `session` alone: it deletes a record, never work, its
handler re-judges its seven refusals at the run, and the record's `State` and `Updated` are what it acts on.

**What each `plan` fingerprint takes.** A plan's answer carries words composed for the person, the moment it was read and versions of
the tools that read it; a fingerprint over the whole answer would refuse a press confirmed the next morning, or after an update, with
nothing it acts on moved. So each `plan` row names the fields its fingerprint takes, and those it leaves out; a field named in neither
column is left out. The fingerprint is a SHA-256 over the named fields in a fixed order, each item list sorted by its id. Each read
is bounded **no shorter than the page's own bound for the same read**: the bound the page's bridge passes for it, or the bridge's
default 30 seconds where it passes none (`bridge/call.ts:8-14`), named in the row as *as the page reads it*; a read of one file is five
seconds. A census case per plan route holds it: two reads of an unchanged world across a moved clock and a new day give one fingerprint,
and one moved item gives another (§10).

| Plan read (its route) | Takes | Leaves out | Bound |
|---|---|---|---|
| `SETUP_PLAN` (`SetupRoutes.cs:25-29`, `Shown` `:96-116`) | `Repository`, `Workspace`, `Line`, `Layout`, `Adopted`, `Declares`, `Asks` (its case), `Rules`, `Agent`, `Lands`, `LandsWith`, each refusal's `Code`, `Pressable` | `Title` and `Body` (the day, `SetupQuests.cs:53-56`; the line's facts, the Daoris version and the neighbours, `SetupPress.cs:140-148`), `Commit` (`SetupRoutes.cs:101`), `Node`, `Daoris`, each refusal's `Sentence` | two minutes, the page's `SETUP_BOUND` (`bridge/repositories/setup.ts:18`) |
| `LANDING` (`TreesRoutes.cs:490-491`, `Planned` `:667-679`) | `Form`, `Target`, `Source`, `Plugin`, `Problem`, `Own`, `Stands`, `Uncommitted`, `Lands`' full shas, `LeftOut`'s shas, `Conflict`, and the gate's states (`Workflow`, `Review`, `Opinion`) without their moments | each commit's `Subject`, every time the gate carries, `LeftOutOf` | the bridge's 30 seconds, as the page reads it |
| `ENTRY_LANDING`, `ASK_LANDING` (`EntriesRoutes.cs:34`, `:67`) | each entry's part: as `LANDING` takes, per part, with what superseded it by id | as `LANDING` leaves out | as the page reads it |
| `HANDOFF_PLAN` (`TreesRoutes.cs:958`) | the landed record it hands (`Session`, `Branch`, its repository) and the branch's tip, the plugin named | the plugin's lines (`said`) | as the page reads it |
| `SWEEP_PLAN` (`LinesRoutes.cs:262`, `SweepAnswer` `:754-759`) | each `Branches` row's repository, branch, kind and tip, each `Landed` row's `Repository`, `Branch`, `Kind`, `Commits`, `Removable` | `Detail`, `Where`, `Files`, `PullRequest`'s words | the bridge's 30 seconds, as the page reads it (`bridge/repositories/lines.ts:173`, `:189`) |
| `TREES_SYNC_PLAN` (`LinesRoutes.cs:440`) | each line's `Repository`, `Line`, `Kind`, `From`, `To`, `Commits`, `Moves`; each rebase row's branch and tip | `Fetch`, `Detail`, `LastFetch`, `Reach` | `lookBound` of its repositories (`bridge/call.ts:36-37`, `bridge/repositories/lines.ts:404-406`) |
| `HISTORY_PLAN` (`HistoryRoutes.cs:35`) | each unit's id and its `Keep` word | counts, bytes and times | `lookBound(1)` (`bridge/sessions/history.ts:35`) |
| `WORK_PLAN` (`WorkRoutes.cs:37`) | `abandon.pieces`: each quest's id, `Status`, `Joined`, `Key`, `Abandon`, and each session's id and state | titles, `Paused.At` | as the page reads it |
| `SYSTEM_DOWNLOADS`, `SYSTEM_SPACE_FREE`'s `plan: true` (`SystemRoutes.cs:138`, `:81`) | each version's or unit's id | sizes and times | as the page reads it |
| `HARNESSES`' row, `PLUGINS`' row, `TOOLS_LIST`' row | the row's id, its installed version and its state | the time it was looked at, words | as the page reads it |
| `PLUGIN_READ` of a folder, an installed plugin, or an offer's folder (`PluginsRoutes.cs:158-185`) | the manifest as `PLUGIN_READ` answers it **and every file under the folder the press copies or runs**, each by its path relative to the folder and its SHA-256: the manifest alone sees no change to the code (`PluginsRoutes.cs:158-185` answers id, name, version, hook, agents and servers) | the files' times | the bridge's 30 seconds, as the page reads it |
| `PLUGIN_UPDATE` without `apply` (`PluginsRoutes.cs:258-282`) | `Source`, `From`, `Changes`, and the files of the source it would copy, as `PLUGIN_READ` above | words | as the page reads it |
| `TOOLS_GIT`'s file | the named file's SHA-256 | — | five seconds |
| `DAORIS.REGISTRY` of a path | the declaration's bytes on disk | — | five seconds |
| `NOTIFICATIONS`' notices (`NoticesRoutes.cs:58`) | the notices' ids its ids or workspace name | their words and times | five seconds |

**The setting readers.** A `setting` row's value is read where it is kept, never assumed in `DriverConfig`: `STATE` (`DriverRoutes.cs:23`)
for the repository's and the machine's standing choices, its value set named by the payload's field (`drivable` for `SET_DRIVABLE`,
`held` for `SET_HOLD`, `ownTree` for `SET_TREES`, `DriverRoutes.cs:49`, `:59`, `:73`, and each `SET_*` by the field its handler reads);
`RULES` for `RULE_ACTION` (`RulesRoutes.cs:18`, `:27-55`); `SESSION_OPTIONS` for `SET_SESSION_OPTION` (`ConversationRoutes.cs:412`,
`:423-432`); `ACCOUNTS` for `ACCOUNT_USE` (`AccountsRoutes.cs:29`); `TOOLS_LIST` for `TOOLS_USE` and `TOOLS_LOCATION` (`ToolsRoutes.cs:47`);
and `DAORIS.REMOTES` `STATE` for `SET` and `REMOVE` (`RemotesModule.cs:44`), over the workspace's address alone, never its key.

**The wait readers.** Each `wait` row is judged by the open read that lists that wait: `OPINION_GATE` (`TreesRoutes.cs:760`) for
`OPINION_ANYWAY` and `OPINION_MYSELF`; `OPINION_WAITS` (`:904`) for `STOP_OPINION`; `WORK_STATE`'s held attempts (`LinesRoutes.cs:558`)
for `WORK_HELD`; `WORKFLOW_RUN` (`WorkflowRoutes.cs:222`) for `WORKFLOW_NODE`; `SESSION_STREAMS` (`ConsoleRoutes.cs:44`) for `STOP_TASK`;
`SESSION_QUESTION` (`SessionsRoutes.cs:367`) for `SESSION_INPUT`'s named question; `SESSION_QUEUE` (`ConversationRoutes.cs:437`) for
`SESSION_QUEUE_EDIT`; the ask's go-aheads as the service holds them, by its `number`, for `SESSION_GO_AHEAD`; `HELPER_WAITS`
(`SessionsRoutes.cs:292`) for the helper's answers; `PERMISSION_WAITS` (`:510`) for `SESSION_PERMISSION_ANSWER`; `SESSION_HAND_OVER_ASK`
(`:347`) for the hand-over's answers; `ANSWER_WAITS` (`:532`) for `SESSION_ANSWER_ALONE`; `SESSION_SUPERVISOR` (`:563`) for
`SUPERVISOR_OVERRULE`; `HELP_PROPOSALS` (`HelpRoutes.cs:378`) for `HELP_APPLY` and `HELP_DISMISS`; `RULES` for `RULE_PROPOSAL`; `STATE`'s
trust hold for `TRUST_FOLDER`; and `TOOLS_LIST` for `TOOLS_STOP`.

**`already`.** A `setting` press whose value is already what it sets is answered at the ask, with no card: *{setting} is already set
as this asks; nothing was asked*, code `already`, exit 0, since the world is as asked. At the Confirm the same case is `changed`
(someone set it while this waited), since the person's world moved under the press.

**What `session` sees.** No session record carries a run's id or a turn's count (`Driver/Trees/SessionGroups.cs:14-144`); `Updated` is
set by the service on every move of the state and on an answer's note (`src/Daoris.Service/Daoris.Service.Core/Sessions/Sessions.cs:890`,
`:1562`). So the ground sees a session that moved, or moved and came back (*ran another turn*), and does not see a turn that moved no
state; where a press answers a question, `SESSION_INPUT`'s `question` names it and the `wait` ground and the handler's own
`question-changed` (`ConversationRoutes.cs:124-129`) see that.

What a ground compares is recorded on the press as codes and values the instance read (a state, a time, a fingerprint, a tip), never
the asker's words. `{act}` is the route's act as `person.route.<ROUTE>` words it for every `Person` route, and `{then}` and `{now}` are worded by
the catalogue (`sessionState.*` for a session, `status.*` for a quest, `locales/en/status.json:2-3`),
never by a code (§8.3).

**The table, route by route.** One row per route the census makes `Person`, payload forms included (`PipeCensus.cs:122-124`, `:212-266`,
`:290-294`); the field named is the payload's.

Handlers are the modules' `Bridge/Routes/*Routes.cs`, each by its `[DriverRoute]`; *re-judges* says what the handler already refuses at
its run, which the ground does not replace.

| Route (handler) | Field named | Grounds | Why, and what the handler re-judges |
|---|---|---|---|
| `LAND_SESSION_TREE` (`TreesRoutes.cs:491`) | `id` | `session`, `branch` (the session's tree head, read by `id` as the handler reads its tree, `:498`), `plan` (`LANDING`, its `Lands`' shas) | Lands commits; a branch that gained some during the wait would land them. The payload's `branch` is the name the person entered for the landing (LANDNAME1a, `:470-477`), absent by default, and is not the branch read. Re-judges its gate token and quest result (`:584-590`). |
| `LAND_ENTRY` (`EntriesRoutes.cs:35`) | `entry` | `plan` (`ENTRY_LANDING`: each branch and its tip) | Lands an entry's branches. |
| `LAND_ASK` (`EntriesRoutes.cs:68`) | `ask` | `ask`, `plan` (`ASK_LANDING`: each branch and its tip) | Lands an ask's branches; the ask still open is not enough. |
| `HANDOFF` (`TreesRoutes.cs:959`) | `id`, or `repository` and `branch` | `session` (where `id`), `repository`, `branch`, `plan` (`HANDOFF_PLAN`) | Hands a branch to a plugin that may push. Re-judges the newest landed record (`:1068`). |
| `OPINION_ANYWAY`, `OPINION_MYSELF` (`TreesRoutes.cs:762-763`) | `id` | `session`, `wait` (the opinion gate, `OPINION_GATE`) | Answers a held gate. |
| `ASK_OPINION` (`TreesRoutes.cs:761`) | `id` | `session`, `branch` | Starts a review on an account, of the work as it was asked on. |
| `STOP_OPINION` (`TreesRoutes.cs:764`) | `opinion` | `wait` (that opinion running) | Stops a review. |
| `DISCARD_SESSION_TREE` (`TreesRoutes.cs:434`) | `id` or `path` | `session` (where `id`), `branch` (the tree's head); with `force`, also `plan` (the tree's uncommitted paths, each by path and SHA-256) | Deletes a tree. Re-judges its unforced proof where `force` is absent; a press with `force` skips that proof (`:429-431`), so the paths it would lose are its ground. |
| `DISCARD_SESSION_BRANCH` (`LinesRoutes.cs:339`) | `repository`, `branch` or `named` | `repository`, `branch` | Deletes a branch. Re-judges D88's unforced proof. |
| `RECOVERY_DROP` (`LinesRoutes.cs:379`) | `repository`, `refs[]` with their commits | `repository`, `branch` (each ref's commit, as the payload names it) | Deletes refs. Re-judges each, a moved ref skipped. |
| `SWEEP` (`LinesRoutes.cs:263`) | `workspace`, `only[]` | `plan` (`SWEEP_PLAN`: the branches and their tips) | Deletes branches and trees. |
| `TREES_SYNC` (`LinesRoutes.cs:441`) | `repository`, `workspace`, `all`, `also[]`, `only[]` | `plan` (`TREES_SYNC_PLAN` for the same scope: each line's and branch's tip) | Fast-forwards lines, replays and deletes branches, every repository's at once (`PipeCensus.cs:263-266`). |
| `HISTORY_CLEAR` (`HistoryRoutes.cs:58`) | `workspace`, `quest`, `ask`, `units[]` | `plan` (`HISTORY_PLAN`: the units) | Deletes history. Re-judges each unit. |
| `SYSTEM_DOWNLOADS_CLEAR` (`SystemRoutes.cs:152`) | `versions[]` | `plan` (`SYSTEM_DOWNLOADS`: the versions) | Deletes downloads. |
| `SYSTEM_SPACE_FREE`, not `plan: true` (`SystemRoutes.cs:81`) | `units` | `plan` (its own `plan: true` form: the units) | Deletes what it names. |
| `WORK_ABANDON` (`WorkRoutes.cs:128`) | `ask` or `quest`, `pieces[]` | `ask` or `quest`, `plan` (`WORK_PLAN`'s `abandon.pieces`) | Abandons work. Re-judges each piece. |
| `WORK_PAUSE`, `WORK_RESUME` (`WorkRoutes.cs:217`, `:241`) | `ask` or `quest` | `ask` or `quest` | Stops or restarts work. |
| `WORK_HELD` (`LinesRoutes.cs:690`) | `unit`, `node`, `attempt` | `wait` (the held attempt) | Answers a held attempt. |
| `WORKFLOW_NODE` (`WorkflowRoutes.cs:254`) | `run`, `node` | `wait` (the node held in that run) | Skips or stops a run's node. |
| `SESSION_ARCHIVE` (`SessionsRoutes.cs:820`) | `ids[]` | `session` (each) | Archives records. |
| `SESSION_DELETE` (`SessionsRoutes.cs:885`) | `id` | `session` | Deletes a record. Re-judges its seven refusals. |
| `STOP_SESSION`, `CANCEL_TURN`, `END_CHAT` (`SessionsRoutes.cs:21`; `ConversationRoutes.cs:324`, `:305`) | `id` | `session` | Stops or ends what runs. |
| `STOP_TASK` (`ConsoleRoutes.cs:61`) | `id`, `key` | `session`, `wait` (that task running) | Stops a task. |
| `RESOLVE_SESSION` (`SessionsRoutes.cs:73`) | `id` | `session` | Finishes or declines a park. |
| `SESSION_QUIET_CARRY_ON`, `SESSION_QUIET_LEAVE` (`SessionsRoutes.cs:481`, `:464`) | `id` | `session` | The person's word on a quiet session. |
| `SESSION_GO_ON_NEW`, `SESSION_START_FROM`, `SESSION_GO_ON_BRIEF`, `SESSION_HAND_OVER`, `SESSION_STOP_WAITING` (`SessionsRoutes.cs:118`; `ConversationRoutes.cs:160`, `:186`, `:223`, `:204`) | `id` | `session` | Starts or carries on work on an account. |
| `SESSION_HELPER_ASK`, `SESSION_PARTNER_ASK` (`SessionsRoutes.cs:139`, `:223`) | `id` | `session` | Starts a helper or a piece on an account. |
| `SESSION_INPUT` (`ConversationRoutes.cs:93`) | `id`, and `question` where named | `session`; `wait` (the question) where `question` is named | Words as the person's. Re-judges a stale question, `question-changed` (`:124-129`). |
| `SESSION_QUEUE_EDIT` (`ConversationRoutes.cs:355`) | `session`, `id` (the word) | `wait` (the word still queued) | Rewords or takes back a word. |
| `SET_SESSION_OPTION` (`ConversationRoutes.cs:423`) | `id`, `option` | `session`, `setting` | A session's option. |
| `SESSION_GO_AHEAD` (`ConversationRoutes.cs:280`) | `id`, `ask`, `number` | `wait` (that go-ahead open) | Answers a go-ahead. |
| `SESSION_HELPER_APPROVE`, `SESSION_HELPER_DECLINE` (`SessionsRoutes.cs:308`, `:330`) | `ask`, `session` | `wait` (the helper's ask) | Answers a helper's ask. |
| `SESSION_PERMISSION_ANSWER` (`SessionsRoutes.cs:519`) | `session`, `request` | `wait` (the permission held) | Answers a held permission. |
| `SESSION_HAND_OVER_APPROVE`, `SESSION_HAND_OVER_DECLINE` (`SessionsRoutes.cs:408`, `:447`) | `session`, `ask` (absent: the current one, recorded at the ask) | `wait` (the hand-over ask) | Answers a hand-over ask. |
| `SESSION_ANSWER_ALONE` (`SessionsRoutes.cs:540`) | `session` | `wait` (the answer held) | Lets a held answer go on. |
| `SUPERVISOR_OVERRULE` (`SessionsRoutes.cs:591`) | `id`, `decision` | `wait` (that decision live) | Overrules a decision. |
| `RETRY_QUEST` (`DriverRoutes.cs:413`) | `quest` | `quest` | Tries a parked or held quest again. |
| `HELP_APPLY`, `HELP_DISMISS` (`HelpRoutes.cs:494`, `:568`) | `id`, `revision` | `wait` (the proposal open, at its revision) | Applies or dismisses a proposal. Re-judges its revision. |
| `RULE_PROPOSAL` (`RulesRoutes.cs:60`) | `id` | `wait` (the proposal open) | Answers a rule proposal. |
| `TRUST_FOLDER` (`DriverRoutes.cs:361`) | `folder` | `wait` (the trust hold) | Re-judges the hold from the last tick. |
| `CHANNEL_POST`, `CHANNEL_FORWARD` (`AsksRoutes.cs:44`, `:85`) | `ask`; `repository` for a forward | `ask`; `repository` for a forward | Words on an ask's channel. |
| `START_CHAT` (`ConversationRoutes.cs:38`) | `repository` | `repository` | Starts a conversation on an account. |
| `START_HELP`, `HELP_START_FROM` (`HelpRoutes.cs:46`, `:1285`) | `workspace`; `id` | `repository` (the workspace registered here); `session` (the conversation) | Starts Ask Daoris on an account. |
| `SETUP_PRESS` (`SetupRoutes.cs:34`) | `repository` | `repository`, `plan` (`SETUP_PLAN`, its projection above: the case, the rules, the landing form and plugin, the agent, the refusal codes and `pressable`, never its title, body, commit or tool versions) | Publishes a set-up. Reads its plan again at the press (`:31-47`), which the ground compares with the one asked on; a set-up confirmed the next morning, after an update or after a commit on its line runs where nothing it publishes moved. |
| `SET_DRIVABLE`, `SET_TREES`, `SET_HOLD`, `SET_STANDING`, `SET_SESSION_CHECKS`, `SET_HEAVY_GATES` (`DriverRoutes.cs:44`, `:69`, `:54`, `:82`, `:117`, `:99`) | `repository` | `repository`, `setting` | A repository's standing choice. |
| `SET_ACCESS`, `SET_COMMITS`, `SET_HAND_OVER`, `SET_HELPERS`, `SET_LANDING`, `SET_LANGUAGE`, `SET_LINE`, `SET_OPINION`, `SET_REVIEW` (`LinesRoutes.cs`), `SET_READ_ACROSS`, `SET_WRITE_ACROSS` (`AcrossRoutes.cs:50`, `:73`), `WORKFLOW_LANDED` (`WorkflowRoutes.cs:129`) | `repository` or `workspace` | `repository` (the repository or workspace registered here), `setting` | A standing choice at its scope. |
| `SET_ACTOR`, `SET_AGENT_SETTINGS`, `SET_CAP`, `SET_COOLOFF`, `SET_HELP_MODE`, `SET_HELP_GRANT`, `SET_NOTIFY`, `SET_STRIKES`, `SET_KEEP` (`DriverRoutes.cs`, `AgentsRoutes.cs:513`), `RULE_ACTION` (`RulesRoutes.cs:27`), `ACCOUNT_USE` (`AccountsRoutes.cs:395`), `TOOLS_LOCATION`, `TOOLS_USE` (`ToolsRoutes.cs:208`, `:126`) | the setting the payload names | `setting` | A machine's standing choice. |
| `HARNESS_ACTION`, `HARNESSES_REFRESH` (`AgentsRoutes.cs:345`, `:33`) | `harness` or `agent`, `profile` | `plan` (`HARNESSES`' row for it) | Installs, updates, signs in, or lets a refused account through. |
| `PLUGIN_ACTION` (`PluginsRoutes.cs:299`) | `id` | `plan` (`PLUGINS`' row for it) | Sets or removes a plugin's grant. |
| `PLUGIN_ADD`, `PLUGIN_NEW`, `PLUGIN_TRY` (`PluginsRoutes.cs:190`, `:373`, `:391`) | `folder` or `id` | `plan` (`PLUGIN_READ`'s manifest and every file under the folder it copies or runs: the payload's `folder`, or the installed plugin's folder by `id`; for `PLUGIN_NEW`, the target folder's files, none where it is new) | Installs, writes or runs code in the driver, so the ground covers the code it runs, not the manifest alone. |
| `PLUGIN_INSTALL` (`PluginsRoutes.cs:286`) | `offer` | `plan` (`PLUGINS`' offer and every file under the offer's folder) | Installs a plugin. |
| `PLUGIN_UPDATE`, `apply: true` (`PluginsRoutes.cs:258`) | `id` | `plan` (its own preview without `apply`, and the files of the source it copies) | Replaces a plugin. |
| `TOOLS_DELETE`, `TOOLS_DOWNLOAD` (`ToolsRoutes.cs:179`, `:161`) | `tool`, `version` | `plan` (`TOOLS_LIST`'s row) | Deletes or downloads a program. |
| `TOOLS_STOP` (`ToolsRoutes.cs:168`) | `tool` | `wait` (the tool's run) | Stops a download or install. |
| `TOOLS_GIT`, every `way` but `system` and `managed`, an absent one included (`ToolsRoutes.cs:231`; `PipeCensus.cs:290`) | `file` | `plan` (the named file's SHA-256) | Runs the file it names. |
| `DAORIS.REGISTRY` `WRITE_DECLARATION` (`Platform/RegistryModule.cs:44`) | `path` | `plan` (the registry's read of that path: the declaration on disk) | Writes `daoris.json` into a repository. |
| `DAORIS.REMOTES` `SET`, `REMOVE` (`Platform/RemotesModule.cs:46`, `:73`) | `workspace` | `setting` (the remote's address as `STATE` holds it) | Wires or unwires the machine. `SET` carries a deployment key (`RemotesModule.cs:51`), so it is the census's one **keyed** press: its record never holds its payload, and it is not kept across a restart (§7.1). |
| `CHANNEL_MARK` (`AsksRoutes.cs:67`) | `ask`, `through` | `timeless` | A read mark, which never moves back. |
| `NOTICES_READ` (`NoticesRoutes.cs:28`) | `ids[]` | `timeless` | Marks news read. |
| `NOTICE_DISMISS` (`NoticesRoutes.cs:37`) | `id`, or `all` and `workspace` | `plan` (`NOTIFICATIONS`' notices for its ids or workspace) | Its `all` form would dismiss news that came during the wait. |
| `HELP_RENAME`, `HELP_PIN` (`HelpRoutes.cs:1241`, `:1261`) | `id` | `timeless` | A conversation's name or pin. |

**One table, not two.** The fields that name a session, a repository, a quest or an ask are D209's `PipeCensus.Targets` (the steward
design §5.4), declared over the same routes. The grounds are that table's second column, not a table of their own: whichever of
STEWARDROLE1a and CONFIRMWAIT1a-i lands first makes `PipeCensus.Targets` with both columns, and the other fills its own. Until
STEWARDROLE1a lands, `Targets` holds the grounds' fields alone. D209's `Targets` names a repository, a quest or a session (the steward
design §5.4) and no ask; the shared table adds `ask`, which the `ask` ground and `LAND_ASK`, `WORK_*` and `CHANNEL_*` rows need, and which
the steward's marks may ignore.

**The census.** A test holds that every route `PipeCensus.Routes` lists as `Person`, in every form its rule gives, and every route a
route-by-route module leaves to the `Person` default (`PipeCensus.cs:137`), names grounds or `timeless` with its reason; that a
`timeless` route is none the census comment or the table calls a delete, a close or a move; and that each named field is one the
route's handler reads. **A `Person` press with no row is refused at the ask, `ungrounded`**, never treated as timeless: *Daoris does
not know what this press rests on, so it cannot wait for the person; nothing was asked*. A route added later is grounded in the change
that adds it (`.claude/knowledge/bridge-presses.md`).

### 5.2 Where and when it is judged

- **At the ask.** `Accept` stays synchronous (`InstancePipe.cs:521`, called on the connection's loop at `:980`). After the standing
  confirmation's judgement (`:624-631`), so a press it admits is never refused here, the census's row is looked up and a `Person` press
  with none is refused `ungrounded` there and then, no read made. Then the bound is checked and a place taken under `PipeConfirmations`'
  lock, told to no window yet, so an eleventh press is refused `confirmations-full` before any read (§9.3). The grounds are read
  **inside the press's later task** (`ConfirmThenRunAsync`, `:686`), outside every lock; on that read the place becomes the card and
  `CONFIRMING` is told, or the place is let go and the press answered `already` or `unread-grounds`, nothing asked.
- **The readers** are the loop's, handed to the pipe as `RunsHere` and `HeldNow` are handed to the standing confirmation
  (`DriverLoop.cs:844-849`), each ground's named in §5.1: the session's record, the registry, a quest's and an ask's state from the service;
  a setting from the read its row names; a branch's tip by one `git rev-parse`; a plan by its route's own handler called in process,
  projected as its row says; an open wait by the read its row names. A state read is bounded by five seconds, a plan by its row's bound
  (§5.1), never shorter than the page's own for the same read.
- **At the Confirm**, `PipeConfirmations.Answer` marks the press **checking** under its lock and tells every window, the card kept with
  `checking: true` and *checking that nothing changed…* in place of its buttons; it is not taken from what waits until it is judged. The
  pipe reads the grounds outside every lock (the standing confirmation's rule, `StandingConfirmation.cs:171-175`), then, under the lock,
  takes the press and ends it `Changed` with its reason or runs it. A `WITHDRAW` while it is checking answers `running` (§4.3), and a
  second window's answer meanwhile is `gone`. `PERSON_CONFIRM` answers once that judgement is made: `{ answered: true }`, or
  `{ answered: false, why: "changed", reason }`, said on the card, which is still drawn (§8.1). The page waits for that answer as long as
  the press's slowest ground may take: `PERSON_CONFIRM` is sent with the bound of the route's plan read plus the bridge's slack
  (`bridge/call.ts:33`), not the bridge's default 30 seconds (`bridge/person.ts:164-165`), the bound carried on the confirmation as
  `checkWithin`, so the page names no route.
- **A read that fails or times out.** At the ask it refuses `unread-grounds`, nothing asked. **At the Confirm it ends nothing**: the
  press goes back from checking to waiting, its card says *Daoris could not read what this rests on just now; it still waits*
  (`person.unread`), and the person confirms again or refuses. A service reconnecting or a git lock never costs the person a press
  that waited hours, as the drain's refusal does not (§7.2).
- The grounds are not a lock: between the read and the route's run the world can move again, and the route's own judgement and the
  store's transactions stay the guard, as today. They catch what moved during the wait, which is hours where the race is milliseconds.
- A change made before the ask is not a change: the ask's reading is the baseline, so a press asked after its session moved is judged
  against the session as it was at the ask.
- Grounds are not judged to hide or grey a card, since a read on every telling of the list would run on every change of every window.

## 6. The asker withdraws one

§4.3 is the asker's door; it is the only one besides the person's answer. A withdrawal of a press some window showed (its `shownAt`
set) is said to the person as a notice in *Done and failed* (`notifications.done.withdrawn`, §8.3), since a card they saw that vanishes
with nothing said reads as lost; one nobody showed goes with its row and no notice, so an asker that loops ask-then-withdraw floods
nothing (§9). The instance's close no longer withdraws (§7.2).

## 7. A restart

### 7.1 The press's record

- **Written when asked**, before `CONFIRMING` is told: `<home>/data/pipe-waiting/<confirmation id>.json`, named by **the confirmation's
  own id**, the GUID the instance mints (`PipeConfirmations.cs:168`), never by the terminal's id, which the instance takes as any text of
  1 to 128 characters (`InstancePipe.cs:551`) and which therefore never reaches a path. Written atomically as BOM-less UTF-8.
- **Its folder is a kept place of the home, not `run/`.** The journal's `pipe` entry is `run/pipe` (`HomeEntry.Pipe`,
  `Driver/Platform/DaorisHome.cs:406`), and D172 lets anyone delete `run/` while Daoris is closed (`D172.md:19`) and never backs it up
  (point 7, `:45-47`): a press kept there across a restart would rest on a folder anyone may clear. So the records get an entry of their
  own, **`HomeEntry.PipeWaiting`, `("pipe-waiting", HomeCategory.Data, "data/pipe-waiting", "data/pipe-waiting")`**, a new entry whose old
  place is its new one, as `StandingConfirmation`'s is (`DaorisHome.cs:365`), asked of the table (HOMELAYOUT2). It is added in all three
  home twins (the driver's and the service's `DaorisHome`, the CLI's `src/Daoris.Cli/src/platform/home.ts`) and in
  `home-census.test.ts`. `data/` is backed up (D172 point 7), so a person who restores a backup restores what waited; the restore's
  re-judgement (§7.2) and the grounds read at the Confirm (§5) make a record from a backup no more than a press asked again.
- **No key is ever written into it.** D172 point 3 keeps every file that holds a key in `profiles/` alone (`D172.md:27-28`), so that
  `data/` can be copied and handed on. One `Person` press carries a key: `DAORIS.REMOTES` `SET`, its `key` the workspace's deployment key
  (`RemotesModule.cs:51`). The census marks such a route **keyed** (`PipeCensus.Keyed`, today `DAORIS.REMOTES` `SET` alone, the census test
  holding that every `Person` route whose handler reads a field `profiles/` keeps is in it). **A keyed press is not kept across a restart**,
  said: its record holds its binding (the payload's fingerprint, never the payload), its terminal's id, route, asker and `asked`, and
  `keyed: true`; its payload stays in memory alone. At a close it ends `Lost` (§7.2), and at the next start its record ends `Lost` and is
  removed, journaled where its id reads, with the lost notice; a crash does the same at the next start. Its card says so: *this carries a
  key, so it does not wait across a restart of Daoris* (`person.keyed`). Keeping the key where it lives and the record pointing at it was
  the other way the read allowed; it was not taken, since the key a waiting `SET` carries lives nowhere yet: writing it to `profiles/`
  before the person confirms would be the very write the press asks for (§11).
- **What it holds**: a version; the confirmation's id; the terminal's id; the binding (route, payload fingerprint, key fingerprint, as
  `PipeBinding`); the module, route and payload as sent; the grounds as read at the ask; the asker as the system named it; `asked`,
  `shownAt`; and its state (`waiting`, `begun`, or `ended` with its ending's answer). **It holds no card fields and no named values**:
  those are made again from the payload (§7.2), so a record cannot show one thing and run another.
- A press whose record cannot be written is refused `unrecorded` and nothing is asked, as a press whose journal line cannot be written
  is today (`:770-774`).
- **The order of an ending**, so the record alone says what happened: on a Confirm whose grounds hold, the record is rewritten `begun`,
  then the journal's *begin* line, then the record removed, then the run, then the journal's *end*. On every other ending the record is
  rewritten `ended` with its answer, then the journal's line, then the record removed — removed **even where the journal's line failed**
  (today's swallowed failure, `InstancePipe.cs:712-718`), since the record already said it ended. A record that cannot be removed is
  left `ended`, and the next start removes it (§7.2).
- **What reaches the disk**: the payload of every waiting press but a keyed one, and of a refused or withdrawn one until its record is
  removed, in `data/`, which is backed up. That is a reach it did not have in memory, and it is bounded: no key reaches it (above); the
  session records and the notices already keep the person's words in `data/`; and D156 point 7 says what a process of the account
  reaches under the home (`D156.md:42-48`). A record a process wrote itself grants nothing it could not ask over the pipe, once the
  restore judges it as `Accept` judges a press (§7.2), and its card's default is no.

### 7.2 What a restart does

- **A close no longer withdraws.** `BeginStopping` refuses new presses as today, and leaves the presses waiting in their records.
  It **completes each waiting press's line answer** with `waiting`, `why: "close"` (§4.1), and removes it from `_running` and `_owners`
  (`InstancePipe.cs:609-610`), keeping its record: `Drain` waits on every `_running` answer (`:288-316`), so a waiting press left there
  would spend the whole drain, 15 seconds (`DriverLoop.cs:652`), and log `pipe.drained`, at every close with a press waiting. A keyed
  press is ended `Lost` there instead (§7.1). `PipeConfirmations.WithdrawAll` on close goes, and `Dispose` stops the timers it no
  longer has.
- **The restore** runs in the loop, before `Serve` (`DriverLoop.cs:852`), once the journal is read; the journal is made there rather
  than inside `Serve` (`InstancePipe.cs:261`). **The home's lease is its ground**: the loop holds it before either step
  (`DriverLoop.cs:834`; D104), so the restoring loop is the home's one writer, and no other instance on this home has records of its
  own. It restores whatever `Serve` then answers. Each file in `data/pipe-waiting/` is taken in `asked` order:
  1. a name that is not a confirmation id (32 hexadecimal characters) is removed and logged, nothing asked;
  2. a record of a **newer version** is left in place, not asked, and logged, as the standing confirmation leaves a record it cannot
     read (`StandingConfirmation.cs:146-150`); the person gets one notice, *a terminal's press from a newer Daoris waits for it*;
  3. state `begun`: never asked again; where the journal holds no line for its id, its ending `interrupted` is journaled, so `RESULT`
     says it stopped while under way, which is now so; the record is removed;
  4. state `ended`: its ending is journaled where the journal lacks it; the record is removed;
  5. `keyed: true` (§7.1), whatever its state: ends `Lost`, journaled where its terminal's id reads, the record removed, nothing asked;
     it holds no payload to ask;
  6. state `waiting`: **judged again as `Accept` judges a press**, from the module, route and payload alone: the census classes it
     (`PipeCensus.Of(module, route, payload)`) and only `Person` passes; the payload's fingerprint equals the binding's, and the route
     equals the binding's; the shape is checked again (`PipeConfirmations.FieldsOf`) and the card's fields and named values are made by
     `FieldsOf` and `PipeWording.Named`, never read from the record; the route has a grounds row; the terminal's id is no id the
     journal already holds; and the bound (§9) is not passed. One that passes waits again, its row and card back, its grounds those
     the record holds (a forged ground that equals the world at the Confirm is the same as a press asked fresh; one that does not is
     `changed`). One that fails, or does not read, ends `Lost`: journaled where its terminal's id reads, the record removed, nothing
     asked;
  7. the person gets one notice for all the presses lost at that start, *{count} presses from a terminal could not be restored after
     Daoris restarted, and were not done*, and, while no window is in front, one balloon for those waiting again,
     *{count} presses from a terminal still wait for you*.
- **A pipe not served** after the restore (`Serve` answering null, for any reason: not Windows, the home unreadable, or the name held,
  `InstancePipe.cs:237-255`): the presses waiting again stay cards, which the window answers as any; a Confirm runs them through the same
  dispatcher. Their terminals cannot read `RESULT` or `WITHDRAW` until a Daoris serves the pipe, and the card says *no terminal waits on
  it* (§8.1). A name held, a closing instance's say, is no second writer of these records: the lease makes this loop the home's one
  (above), so the restore never waits on `Serve`'s answer and never hides what waits.
- **An update's drain** leaves presses waiting; a Confirm during the drain is refused on the card with *Daoris is about to restart;
  this still waits*, and the press stays waiting, rather than ending `stopping` (`InstancePipe.cs:695`).
- **A crash** loses nothing written: the record was written at the ask. A keyed press's record ends `Lost` at the next start, said.

### 7.3 How long the records keep

A waiting press's record keeps until it ends. Its ending keeps in the journal 24 hours, as every press's does
(`PipeJournal.cs:74`, `:189`). A press that waits a week is a week's row in *What needs you*, which the bound (§9) keeps from growing.

## 8. What the person sees

### 8.1 The card (`person/Confirmations.tsx`, drawn in *Notifications*, NOTIFY2b)

- *asked {{ago}}* stays and grows in weight once a press has waited over an hour, so a stale-looking card reads its age first.
- **For `source: 'pipe'`**, one line, `person.waits`, says it waits until they answer, and *refused by itself in {{time}}* and *It waits
  until {{time}}, then is refused* are not drawn. **For `source: 'service'`** (`Confirmations.tsx:241`) both lines stay, from that
  door's own `expires`, since its two minutes stand (§11).
- `person.gone` for a pipe's press says *answered in another window, or its terminal took it back*; a service press's gone keeps *its
  two minutes passed* (`person.goneService`).
- **Whether a terminal still waits on it.** Each pipe's confirmation carries `waitedOn`, true while a connection waits on its line for
  it, re-told when that line ends (its wait over, the terminal gone, a close). The card says *a terminal waits on this* or *no terminal
  waits on this now; it can read how it ended later*. The card is where it is said, since *What needs you*'s row is drawn nowhere (§8.2).
- The shown word stays (`PERSON_CONFIRM_SHOWN`, the census's `Window` row unchanged; its census comment reads *a terminal saying it
  would mark it seen*): it moves no clock, records `shownAt` once, logs `shown`, and ends the bell's unshown warning hue as NOTIFY2b
  has it.
- A press waiting again after a restart names its asker as *the record says it was sent by {{process}}, process {{pid}}, before Daoris
  restarted*: the name is the record's, which a process of the account could have written (D156 point 7), so it is said as the
  record's, never as the system's.
- **While its Confirm is judged** the card stays, `checking: true`, its buttons replaced by *checking that nothing changed…*
  (`person.checking`), until `PERSON_CONFIRM` answers (§5.2). A `changed` answer is then said on the card, with its reason, before the
  card goes; it is toasted only where the card has gone meanwhile (another window's view closed). A read that failed says
  `person.unread` on the card and gives its buttons back.
- **A press that rests on a plan** (a land, a set-up, a sweep, a sync, a clear) has a door from the card to the plan's own page, where the
  current plan is shown: the session's review for a land, the repository's *Set up*, the branches' sweep or sync, the history's clear.
  The grounds hold the press to the world at the ask; the door lets the person see that world before confirming a press hours old.
- A keyed press (§7.1) says `person.keyed`: it does not wait across a restart.

### 8.2 What needs you and the balloon

- **The row is .NET's and undrawn, as the page draws it today.** It stays a `confirmation` row, no workspace, judged in .NET
  (`NeedsAPerson.cs:464-481`); the page's `needsListed` drops it from every list and count (`attention.ts:114-116`, §1.5), since the card
  in *Notifications* is the press's place. So this design gives the row no group and draws it nowhere new. Its field `expires` is sent
  `null` for a pipe's press, so the slice and the page's `PushedAttention` stay twins and the page's why (`attention.ts:906`), drawn
  for no one today, loses *is refused at {{time}}*; the twin test (`AttentionStateSliceTests.cs:1648-1656`) is extended to
  `confirmation`'s fields, so one side's change fails it. `waitedOn` is the card's alone (§8.1).
- **Balloons coalesce in the balloon, never on the event.** `Attend` stays one `SESSION_ATTENTION` of kind `Confirming` per press, keyed
  by its id (`PipeConfirmations.cs:363-371`), since that event is also the page's toast's arrival and the window's announcement
  (`notifications/announce.ts:15-21`), which D196 point 4 says on the press's own event by its id: folding the event would fold the
  page's per-press toasts with it. `SessionBalloon` (`Windows/SessionBalloon.cs:132-134`) does the coalescing: at most one balloon a
  minute across every press, the later asks of that minute folded into one, *{{count}} presses from a terminal wait for you*. The page's
  toasts stay one per press in the focused main window, as D196 point 4 has them; the restart's one balloon is §7.2's. No reminder
  repeats: the bell's count and the taskbar's lit button already stay while a press waits unshown (NOTIFY2b).

### 8.3 The words, en and zh

Glossary words: 委托 for quest, 需求 for ask, 终端 for terminal, 通知 for Notifications, 仓库 for repository. The English names the thing
*a terminal's press*, never *request* (`glossary.json:488`); the Chinese keeps 请求, as `notifications.done.expired` and
`notifications.system.confirming.body` say it today (`locales/zh/notifications.json:32`, `:39`). Counts carry `_one` and `_other`, as
`agents.json:8-9` does.

| Key | en | zh |
|---|---|---|
| `person.waits` | It waits until you answer. | 它会一直等到你回答。 |
| `person.waitedOn` | A terminal waits on this. | 有终端正在等它。 |
| `person.notWaitedOn` | No terminal waits on this now; it can read how it ended later. | 目前没有终端在等它；之后仍可读取它的结果。 |
| `person.restored` | The record says it was sent by {{process}}, process {{pid}}, before Daoris restarted. | 记录显示它在 Daoris 重启前由 {{process}}（进程 {{pid}}）发出。 |
| `person.gone` | This no longer waits: it was answered in another window, or its terminal took it back. | 此请求已不再等待：它已在另一个窗口中得到回答，或其终端已将它撤回。 |
| `person.goneService` | This no longer waits: it was answered in another window, its two minutes passed, or its terminal took it back. | (today's `person.gone`, unchanged) |
| `person.changed.session` | Not done: the session moved on while this waited (it was {{then}}, it is {{now}}). | 未执行：等待期间该会话已有进展（当时{{then}}，现在{{now}}）。 |
| `person.changed.turn` | Not done: the session ran another turn while this waited. | 未执行：等待期间该会话又运行了一轮。 |
| `person.changed.setting` | Not done: what this would set changed while this waited ({{act}}). | 未执行：等待期间它要设置的内容已被更改（{{act}}）。 |
| `person.changed.repository` | Not done: the repository {{repository}} is no longer registered on this machine. | 未执行：仓库 {{repository}} 已不再登记在本机。 |
| `person.changed.quest` | Not done: the quest moved on while this waited (it was {{then}}, it is {{now}}). | 未执行：等待期间该委托已有进展（当时{{then}}，现在{{now}}）。 |
| `person.changed.ask` | Not done: the ask was closed while this waited. | 未执行：等待期间该需求已关闭。 |
| `person.changed.plan` | Not done: what this would act on changed while this waited. Look at it again and ask again. | 未执行：等待期间它要处理的内容已变化。请重新查看后再发起。 |
| `person.changed.branch` | Not done: the branch {{branch}} gained or lost commits while this waited. | 未执行：等待期间分支 {{branch}} 的提交已有增减。 |
| `person.changed.wait` | Not done: what this answers no longer waits. | 未执行：它所回答的事项已不再等待。 |
| `person.draining` | Daoris is about to restart; this still waits. | Daoris 即将重启；此请求仍在等待。 |
| `work.attention.confirmationWhy` | It runs only once you confirm it on its card in Notifications; it waits until you answer. | 只有你在通知中它的卡片上确认后它才会运行；它会一直等到你回答。 |
| `notifications.done.withdrawn` | A terminal took back its press to {{act}} | 一个终端撤回了请求：{{act}} |
| `notifications.done.lost_one` / `_other` | A press from a terminal could not be restored after Daoris restarted, and was not done / {{count}} presses from a terminal could not be restored after Daoris restarted, and were not done | Daoris 重启后无法恢复一个终端的请求，它未被执行 / Daoris 重启后无法恢复 {{count}} 个终端的请求，它们未被执行 |
| `person.checking` | Checking that nothing changed… | 正在检查是否有变化… |
| `person.unread` | Daoris could not read what this rests on just now; it still waits. | Daoris 暂时无法读取它所依据的内容；它仍在等待。 |
| `person.keyed` | This carries a key, so it does not wait across a restart of Daoris. | 它带有密钥，因此不会在 Daoris 重启后继续等待。 |
| `person.restoredPid` | The record says it was sent by process {{pid}} before Daoris restarted. | 记录显示它在 Daoris 重启前由进程 {{pid}} 发出。 |
| `person.restoredUnknown` | The record does not say what sent it before Daoris restarted. | 记录未说明 Daoris 重启前是什么发出了它。 |
| `person.openPlan` | Look at it now | 查看当前情况 |
| `notifications.done.newer` | A terminal's press from a newer Daoris waits for it | 一个来自较新版本 Daoris 的终端请求在等待该版本 |
| `notifications.system.waiting.title` | Presses from a terminal wait | 终端的请求在等待 |
| `notifications.system.waiting.body_one` / `_other` | One waits for your confirmation. Open Daoris to confirm or refuse it. / {{count}} wait for your confirmation. Open Daoris to confirm or refuse them. | 有一个请求在等你确认。打开 Daoris 确认或拒绝它。 / {{count}} 个请求在等你确认。打开 Daoris 确认或拒绝它们。 |

*Taken back* is the asker's withdrawal alone (`person.gone`, `done.withdrawn`); a press a restart could not bring back is *could not be
restored*. The restored asker has three forms, as today's `person.asker.process`, `.pid` and `.unknown` (`locales/en/person.json:30-32`).
`notifications.done.expired` stays for a notice kept before this build. Each new key is in both catalogues
(`.claude/knowledge/translation-parity.md`). The terminal's sentences are the verb's own English, as every verb's are.

## 9. The bound, once presses no longer expire

The five bounded a flood a clock also drained; without the clock it is the only drain besides the person and the asker.

1. **Each press its own card.** No press joins another (§11): with the id said at once (§4.1) and no clock, an asker has no reason to
   send one again, and the 2026-10-24 case (one repository's drive sent twice) was a resend after the first had expired.
2. **The bound is ten** cards waiting at once across every terminal, counted as today (only what waits). Five was set when a card
   lasted at most twelve minutes; a person away for a day should come back to a list, not a refusal. Ten is two screens' worth of the
   card list. **For a verb's first thirty minutes the connections, not the cards, are the real limit**: each waiting verb on its line
   holds one of the pipe's eight connections (`PipeLimits.Connections`, `InstancePipe.cs:19`; the slots, `:262`), which never goes idle
   while its press is under way (`:1029-1033`). Today a press ends within twelve minutes; under this design eight verbs waiting their
   default thirty fill the pipe, and the ninth asker, and every `result` and `withdraw`, meet *did not answer*, exit 2. So
   `PipeLimits.Connections` rises to **sixteen**: the ten cards' verbs on their lines with room for the pipe's own requests (`RESULT`,
   `WITHDRAW`, a hello). `PipeLimits.Waiting` and `PipeLimits.Connections` are declared beside each other, each comment naming the other,
   and a test holds `Connections > Waiting`. A waiting card holds no connection once its verb has returned.
3. **`confirmations-full` names the way out**: *Ten presses already wait for the person's confirmation, the oldest asked {{ago}}; this
   one was not asked. Nothing was kept.* The asker can withdraw its own; the person refuses on the cards.
4. **Floods.** An asker that loops ask-then-withdraw raises one balloon a minute at most (§8.2) and no notice for a card nobody showed
   (§6). The page's toast stays one per press in the focused main window (D196 point 4), which the coalescing never folds: the bound of
   ten is what keeps those few.
5. **The ten are shared** by agents' jobs and the person's own terminal. A bound per D201 caller class (`StandingPlace.InAgentJob` and
   `Outside`, `StandingCaller.cs:15-18`), so agents cannot fill the person's ten, is the later answer once D201 part (i) makes that look
   tell; until it does, the look answers `untold` for every caller (§ *What this is not*), and a per-class bound would count them all as
   one.

## 10. The rows, with their proofs

Order keeps main working, and no row leaves the window drawing a time a press no longer has: the window's half of each change lands
in the same row as the instance's. **1b lands first, then 1a-i, then 1a-ii directly after it.**

- **CONFIRMWAIT1b — the verb** (driver, host), **first**. The id at `CONFIRMING`; `--wait` and `--no-wait` taken in the host's entry and
  set on the `InstanceWorld`; the verb's end chosen by `RESULT`'s state, `waiting` its exit 1 and `already` its exit 0; `result`'s
  `waiting`, its exit codes aligned and its `unknown` sentence; `withdraw <id>`; `SentPresses` keeping a waiting press, at most thirty
  days; the four Ctrl+C sentences. Held by the driver's `InstanceDoorTests`, `SentPressesTests`,
  `DriverCommandTests.The_usage_names_every_verb_the_host_answers`, and each family's usage test and golden its usage text changes
  (`DriverCommand`, `AgentsCommand`, `ToolsCommand`, `DriverChoicesCommand`, the plugin commands, and the `daoris` forwards'
  `cli-usage.test.ts` where a forward's line names `--wait`). It is safe against today's instance, which never answers `waiting`. It
  lands before 1a-i because 1a-i's instance answers `waiting`, which today's `result <id>` reads in its default branch as *it stopped
  while this was under way* (`InstanceDoor.cs:460-464`) and today's press, at its wait's end, as *Daoris was still working on it*
  (`:272-274`); and because without 1b an asker whose harness ended the verb never learns the id (§3's second change).
- **CONFIRMWAIT1a-i — no time end** (modules, web), after 1b. The pipe's presses lose their clock; `expires` is sent `null` for them and
  the card and the row's why draw `person.waits` and the new why for `source: 'pipe'`, keeping the service door's lines; the grounds (the
  table in `PipeCensus.Targets` with `ask`, each `plan` row's projection and bound, each ground's reader, the census test, `already`,
  `ungrounded`, the read in the press's task after the standing judgement and the bound, `changed` judged with the card kept `checking`,
  a failed read at the Confirm leaving it waiting, `PERSON_CONFIRM`'s bound); `RESULT`'s `waiting` and `waitedOn`; `WITHDRAW`; the bound
  of ten and the connections at sixteen; the balloon coalesced in `SessionBalloon`, one `Confirming` event per press kept; the withdrawn
  notice; and **a press still ends at a close** (`stopping`, as today), now said to the person as a notice. Held by
  `PipeConfirmationsTests`, `PipeConfirmTests`, `PipeCensusTests` (the grounds census and the per-plan-route cases beside it),
  `AttentionStateSliceTests` (the twin test extended), `NoticesStateSliceTests`, `DriverModulePersonTests`, `SessionBalloonTests`, and
  the page's `person/Confirmations`, `notifications/announce` and `bridge/person` tests, en and zh. Its gain is partial: on an install
  that is republished while the person is away, every close still ends what waits, so 1a-ii follows it directly.
- **CONFIRMWAIT1a-ii — the records and the restart** (modules, web), directly after 1a-i. The record written at the ask in
  `data/pipe-waiting/` and named by the confirmation's id, no keyed payload in it; the ending's order; the close's `waiting` answer
  completing each waiting line and leaving `_running`, so the drain is not spent, and no withdrawal; a keyed press ended `Lost` at a
  close; the restore before `Serve` on the lease, and its re-judgement; the newer record left; the lost and newer notices; `restored`
  and the record's asker on the card; the restart's balloon; the drain's refusal on the card. Held by `PipeConfirmationsTests`,
  `PipeConfirmTests`, `InstancePipeTests`, `NoticesStateSliceTests`, the home twins' census (`home-census.test.ts`, and the driver's
  and the service's `DaorisHome` tests) for `pipe-waiting`, and the page's `person/Confirmations` and `app/notifications` tests, en and zh.

The rows the task names, and what the read found unproved, each with its proof:

| Row | Proof |
|---|---|
| A card left unanswered for an hour, then confirmed, makes its press and tells its asker | The services' clock moved an hour past the ask (and past a `shown`): the press still waits; Confirm runs the route once; a verb still on its line (`--wait 30`, the clock moved inside it) hears the route's answer; a verb that returned at `--no-wait` reads it by `result`, exit 0. `PipeConfirmationsTests` and the end-to-end case in `DriverModuleSessionsTests` through the real pipe. |
| A refused one does nothing and says so | Refuse: no dispatch (the dispatcher's stub counts zero), the terminal's line says *The person refused this in Daoris's window. Nothing was done. (refused)*, exit 1; `result` says the same, exit 1; the journal holds `refused`. |
| A stale one is refused with its reason | One case per ground: `SESSION_INPUT` whose session ran another turn, and one whose session ended; `SET_DRIVABLE` changed by another press; `SET_TREES` whose repository was removed from the registry; `RETRY_QUEST` whose quest was tried again elsewhere; `LAND_ASK` whose ask was closed; `SWEEP` whose plan changed; `LAND_SESSION_TREE` whose branch gained a commit; `SESSION_GO_AHEAD` whose go-ahead was answered elsewhere. Each: no dispatch, `PERSON_CONFIRM` answers `changed` with the reason's code and values, the terminal hears `changed`, exit 1; the card's line in en and zh. |
| A change made before the ask holds | A session moved on, then `SESSION_INPUT` asked, then confirmed with nothing moved since: it runs. |
| Already set, and ungrounded | `SET_DRIVABLE` for a repository already drivable answers `already` at the ask, exit 0, no card; a census `With` a new `Person` route and no grounds row refuses its ask `ungrounded`. |
| The id at once | A press asked: the verb's error stream holds *request {id}*, `result {id}` and `withdraw {id}` before any answer; `--no-wait` returns at that line, exit 1, the press still waiting. |
| Withdraw, and its race with a Confirm | `withdraw <id>` on a waiting press: it ends `Withdrawn`, no dispatch, the card goes, a notice only where it was shown. Confirm then withdraw: `withdraw` answers `running`, the dispatcher sees no cancel, the press's answer is the route's. Withdraw then Confirm: `PERSON_CONFIRM` answers `gone`. Another key's withdraw: `id-taken`. |
| The record's file name | A press whose terminal id is `..\..\data\standing-confirmation`: its record is `data/pipe-waiting/<32 hex>.json`, nothing is written or deleted outside `data/pipe-waiting/`, and the press asks and ends as any. |
| One waiting across a restart | A press asked on one `InstancePipe` over a home, the pipe closed (`BeginStopping`, then dispose): the line answers `waiting`, `why: close`; a second pipe on the same home lists it again with `restored`, Confirm runs it once, and `RESULT` from a new connection with the first key answers it. |
| A crash | A press asked on a first pipe that is **never disposed** and a second made over the same home, whose name the first still holds (`Serve` answering *held*): the record is restored as above, and its card is back though the second pipe serves nothing. |
| A set-up confirmed the next morning | `SETUP_PRESS` asked; the clock moved past midnight, the Daoris version and the line's commit changed, nothing it publishes moved; confirmed: it runs once. Then one moved item (a rule, the case): `changed`. One such pair per plan route in `PipeCensusTests`: two reads of an unchanged world across a moved clock and a new day give one fingerprint, one moved item another. |
| A plugin's code changed while it waited | `PLUGIN_ADD` of a folder asked; one file under the folder changed, the manifest not; confirmed: `changed`, nothing copied. |
| A keyed press | `DAORIS.REMOTES` `SET` asked: its record holds no `key` and no payload (the file's bytes read back); a close ends it `Lost` with the notice and the line `lost`; a crash's record ends `Lost` at the next start; its card says `person.keyed`. |
| `data/pipe-waiting` is the home's | The three home twins name `pipe-waiting` as `data`, and `home-census.test.ts` holds it. |
| `waitedOn` re-told | A verb on its line: `waitedOn` true; its wait ends (`--wait 1`, the clock moved), the terminal gone, a close: each re-tells the list with `waitedOn` false. |
| A read that fails or times out | At the ask: a stub reader that throws, and one past its bound, each refuses `unread-grounds`, no card. At the Confirm: the same, and the press still waits, its card `person.unread`, its buttons back; a second Confirm with the reader mended runs it. |
| `WITHDRAW` during the Confirm's read | A reader held open: the card is `checking: true`; `withdraw` answers `running`; once the reader answers, the press runs or is `changed` as judged. |
| The read's place at the ask | A press the standing confirmation admits is never `ungrounded`; an eleventh press is refused `confirmations-full` with no reader called. |
| `PERSON_CONFIRM`'s bound | The page's call for a press whose route's plan bound is two minutes (`SETUP_PRESS`) is sent with that bound and the slack, not 30 seconds (`bridge/person` test). |
| The drain's refusal | During an update's drain a Confirm is refused on the card, `person.draining`, and the press still waits; after the restart it is restored. |
| 1a-i's close notice | Under 1a-i alone, a close with a press waiting ends it `stopping`, its line said, and the person gets the notice. |
| A close with a press waiting drains at once | Under 1a-ii, a press waiting, `BeginStopping` then `Drain(15 s)`: it returns true at once, no `pipe.drained` logged, the record kept. |
| Two asks within a minute | Two presses asked within a minute while no window is in front: one balloon naming two; while the main window is focused, two toasts, one per press, each keyed by its id (`notifications/announce` test). |
| The restore's judgement | One record each: a route the census makes `Open`, then one it makes `Window`; a payload whose fingerprint differs from its binding; a payload of 257 values; a route with no grounds row; eleven waiting records. Each fails as named and ends `Lost`, one notice for all; none is asked; the card's fields of a passing one equal `FieldsOf`'s for its payload, whatever the record holds beside. A record `begun` is not asked and its id answers `interrupted`; one `ended` whose journal line was never written answers its ending. A newer record is left in place, not asked. |
| A journal line that fails | A refused press whose journal write throws: the record is removed, and a restart does not ask it again. |

**What the gates will not cover.** No installed window will have shown a press across a real restart or an hour's wait: the
installed proof is the parent's. The grounds are read through the loop's readers, not raced against a store's transaction.

## 11. Rejected

- **A longer clock** (an hour, a day): the row's case is a person who was away, and any clock is a time at which a press they never
  refused ends as if refused.
- **The verb holding its line until the answer**, the rejection CONFIRMSEEN1 made, rightly: an agent's terminal or a script cannot
  hold a process for a day, and a held line keeps one of the pipe's connections. Hence `--wait`'s thirty minutes at most.
- **Ending every waiting press at a close, said as a notice**, the simpler of the two the row allows: restarts are routine (an update
  drains and restarts, D139), and each would cost the person every press again, the loss the row was written for. It is 1a-i's interim
  only, until 1a-ii lands.
- **Joining the same press asked twice into one card** (round 1's §9.1). Each joined asker's withdrawal ended the shared card for every
  asker (`InstancePipe.cs:825`, `:840`); a `CONFIRMING` naming the first asker's id is dropped by the second verb (`InstanceDoor.cs:495`);
  a later press would join grounds read before its own ask; and the joined askers were unbounded while the record held one binding.
  Mending each costs a set of askers in every record and every judgement, for a resend that the id said at once removes.
- **Withdrawing on Ctrl+C**: the signal is the same whether the person pressed it or an agent's harness killed its tool call, and the
  harness's kill would take back what the person may still want; the four consoles already say the opposite (§1.2).
- **The kit's `CANCEL` as the withdrawal**: it reaches a confirmed, running press and aborts it (`InstancePipe.cs:845-846`), undoing part
  of the person's Confirm.
- **Naming the record by the terminal's id**, or by its hash: the confirmation's own id is already minted and never the asker's.
- **A record sealed with a key**, so a forged one is refused: D156 rejected every home-held key (`D156.md:66-68`). The restore's
  re-judgement (§7.2) makes a forged record no more than a press asked over the pipe.
- **Card fields kept in the record**: a record could then show one thing and run another (PERSONDOOR1b's *the card shows only what
  binds*).
- **Grounds judged on the route's own handler only**: a handler judges what is true now, and cannot know what the press rested on
  when it was asked.
- **`timeless` for a sweep, bringing branches up to date, freeing space, clearing history or abandoning work**: each acts on a set its
  plan route reads, and hours later that set is not the one anyone saw.
- **Running an *already set* press as a no-op at the Confirm**: it would report success for a world that moved while the person was
  away. At the ask it is answered `already`, with no card, since nothing moved.
- **A per-terminal bound**: the asker is named by a process id, which is not an identity (`D156.md:67-68`) and does not survive a
  restart. A bound per D201 caller class is the later answer (§9).
- **Skipping the restore where `Serve` finds the pipe's name held** (round 2): the restore runs before `Serve` and cannot know its
  answer, and the home's lease (D104) already makes the restoring loop the home's one writer; a name held by a closing instance would
  have hidden every waiting press for the whole run, with no notice.
- **Keeping presses in the store**: the pipe's presses are the machine's and never leave it (D46).
- **The records in `run/pipe`, beside the journal** (round 2): D172 lets anyone delete `run/` while Daoris is closed and never backs it
  up, and a press kept across a restart cannot rest on it. The journal stays there: it keeps a day, and a lost journal loses only how
  ended presses ended.
- **A keyed press's key kept in `profiles/`, its record pointing at it**: the key a waiting `DAORIS.REMOTES` `SET` carries lives nowhere
  until the person confirms it, and writing it to `profiles/` first is the write the press asks for; a separate key file per waiting
  press would be a second keys store for one route. The keyed press is said not to wait across a restart instead (§7.1).
- **Coalescing on the `Confirming` event**: the event is also the page's toast's arrival and the window's announcement, each per press
  by its id (D196 point 4); folding it would fold them. The balloon coalesces instead (§8.2).
- **A failed read at the Confirm ending the press `Changed`** (round 2): a service reconnecting or a git lock would cost the person a
  press that waited hours, the loss the row was written for. It stays waiting, said (§5.2).
- **A plan fingerprint over the whole answer** (round 2): `SETUP_PLAN`'s title and body carry the day, its body the Daoris version, and
  its answer the line's commit, so a set-up confirmed the next morning would be refused with nothing it publishes moved. Each `plan`
  row names its fields (§5.1).
- **The service door's two minutes** changed in the same design: its grant is a secret a client sends once, and no verb of this
  machine asks it; a wait there would need a client that holds the grant across a restart. D156 point 4 and D196 point 4's service
  sentence stand.

## 12. What this amends, once accepted

- **D156 point 4** stands as written: it is the service door's (`POST /api/confirmations` … *within two minutes. Five may wait.*,
  `D156.md:28-33`; `PersonConfirmations.cs:124`, `:127`). It gains one sentence: *A terminal's press over the pipe waits for the
  person's Confirm however long it takes, its grounds still holding (the RPC1c and CONFIRMSEEN1 notes, as CONFIRMWAIT1 amends them).*
- **D156's RPC1c note** (`D156.md:180-200`): *its two minutes over* and *the close (`BeginStopping` withdraws what waits)* go from how a
  press ends; it ends confirmed, refused, changed, withdrawn by `WITHDRAW`, lost, or interrupted, and survives the close in its record under `data/`, but for a keyed press (§7.1).
  *Five wait at once* reads *ten*, the pipe's connections sixteen; *the two minutes count from the ask* goes.
- **D172's table** gains `pipe-waiting` in `data/`, as `standing-confirmation` joined it, in all three twins; its points stand, point 3
  kept by the keyed press (§7.1).
- **D156's CONFIRMSEEN1 note** (`D156.md:237-260`): the two minutes from shown and the ten to be found go; the rejection of a wait with
  no bound is reversed (§3); the row, the balloon and the shown word stay, the balloon coalesced (§8.2).
- **D196 point 4** (`D196.md:36-38`): *its two minutes start then and stay two; its ten minutes to be found stay; an answer or a word
  that it is shown at or past its time ends it as expired, the clock read first, nothing runs, and the refusal is toasted since the card
  has gone* reads, for a terminal's press, *it waits until the person answers, and its card says whether a terminal still waits on it*.
  Its service sentence (*its two minutes from its ask stand*, `:41-42`) stands. D196 gains a dated note saying so at acceptance; D156's
  NOTIFY1 note names it.
- **D171's notes.** Its RPCOPEN1 note (`D171.md:348`), in its **RPC1j** sentence (`:365-367`): `result <id>`'s states gain `waiting`, its
  refusals exit 1, and a press last read as `waiting` is kept past the day, thirty days at most (§4.2); in its **RPC1h** sentence
  (`:367-371`): *the verb's Ctrl+C that sends it is not built* stands, and is now decided, not owed: Ctrl+C withdraws nothing, and
  `withdraw <id>` sends the pipe's own `WITHDRAW`, never the kit's `CANCEL` (§4.3, §11). Its PUSH1f note (`:392`), whose point *a person's
  press is said once at the terminal* (`:403-405`) reads: on `CONFIRMING` the verb names the press's id and the `result` and `withdraw`
  verbs; *the window's refusal or the two minutes' end … exit 1* reads *the window's refusal, a `changed` ground, a withdrawal or a loss …
  exit 1, and the verb's own wait's end with the press still waiting exit 1 `waiting`*. D171 gains a dated note saying so at acceptance.
- **D196 point 4**'s per-press toast and announcement stand as written: the balloon coalesces, never the event (§8.2).
- **These stand**: D201's admission before any card and D209's `Role` route; D52 (no approval surface for agents: the card's default is
  no); the kit's `CANCEL`, `confirm-in-window` and `expired` as words of the wire.

## 13. Round 2: the read's findings and their answers

Every finding of the Opus foundation read of round 1 (`docs/2026-10-24-confirm-wait-reads.md` §4), each checked against the code on
main at `ffcf42926`. *Taken* means the design now says it; *answered* means it is not taken, for the reason given; *deferred* means
a later row carries it. The read's §1 corrections (a to j) are findings 8, 12, 13, 14, 22 and 25 below, and the rest are taken where
they are named.

| # | Finding (blocking in bold) | Checked against | Answer |
|---|---|---|---|
| **1** | **The record is named by the asker's own id** | `InstancePipe.cs:551` takes any id of 1 to 128 characters; `SentPresses.cs:63` vets only the verb's own; `PipeConfirmations.cs:168` mints a GUID | **Taken** as named: the file is the confirmation's own id; the terminal's id is kept inside and never reaches a path; the restore removes any name that is not 32 hexadecimal characters (§7.1, §7.2 step 1). Proved by the record's file name row (§10). |
| **2** | **The restore trusts the record** | `PipeCensus.cs:131-138`, `:280-284`; `InstancePipe.cs:573-580` (`FieldsOf`), `:582-585` (the binding); `PipeConfirmations.cs:168-170` (`PipeWording.Named`) | **Taken** as named: the record holds no card fields; the restore re-classes by the census (`Person` only), compares route and payload fingerprint with the binding, checks the shape, remakes the fields and named values, requires a grounds row, applies the bound, and ends what fails `Lost` with one notice (§7.1, §7.2 step 5). Proved by the restore's judgement row (§10). |
| **3** | **No per-route grounds table; set-ups, lands, sweeps and answers unjudged** | `PipeCensus.cs:122-124`, `:137`, `:212-266`, `:290-294`; the plan routes at `:193-200`; `SetupRoutes.cs:31-47` | **Taken**: the route-by-route table is in §5.1, every `Person` route and form and the `Person` default covered; `plan`, `branch` and `wait` grounds added; `timeless` only for what deletes, moves and starts nothing, and none of `SWEEP`, `TREES_SYNC`, `HISTORY_CLEAR`, `SYSTEM_SPACE_FREE`, `WORK_ABANDON`, a set-up or a land is; a route with no row refused `ungrounded` at the ask; the census test runs over `PipeCensus.Routes` with its forms and the default. |
| **4** | **The id is said only when the verb's wait ends** | `InstanceDoor.cs:487-502` (`Told`, no id said) | **Taken**: the `CONFIRMING` line names the id, `result {id}` and `withdraw {id}` at once, before any wait (§4.1). Proved by the id-at-once row. |
| **5** | **Joining breaks four ways** | `InstancePipe.cs:825`, `:840` (one `_waitingFor` per id, one withdrawal ends the card); `InstanceDoor.cs:495` (the filter); round 1 §7.1 (one binding) | **Taken, the first way**: joining is dropped (§9.1, §11). Every press its own card, bound and withdrawn alone; the id said at once removes the resend joining answered. |
| **6** | **`withdraw <id>` cancels a press already confirmed** | `InstancePipe.cs:838-846` | **Taken**: `withdraw` sends the pipe's own `WITHDRAW { id, key }`, which ends only what waits, under the lock a Confirm takes; one confirmed answers `running` and nothing is cancelled (§4.3). The kit's `CANCEL` is unchanged and sent by no client of this machine. Proved by the race row. |
| **7** | **§12 and the D156 note amend the wrong text** | `D156.md:28-33` (point 4, the service door's); `PersonConfirmations.cs:124`, `:127`; `D196.md:36-38` | **Taken**: point 4 keeps its text and gains one sentence for the pipe; the amendment is aimed at the RPC1c and CONFIRMSEEN1 notes and D196 point 4's two and ten minutes, D196 to gain a dated note at acceptance (§12). The D156 note is corrected to match. |
| 8 | Ctrl+C does not withdraw today | `InstanceClient.cs:134-176` returns null and sends nothing; `AgentsConsole.cs:22-27` and the three others | **Taken**, the first way: Ctrl+C stays as it is; the four sentences gain `withdraw <id>` (§4.1). §1.2 says what is so; withdrawing on Ctrl+C is rejected (§11). |
| 9 | Grounds read under `PipeConfirmations`' lock; the ask's read has no place | `StandingConfirmation.cs:171-175`; `InstancePipe.cs:574-637` | **Taken**: read at the ask in `Accept` before `Ask`, outside every lock; at the Confirm taken from what waits under the lock, read outside it, then `Changed` or run; readers bounded at five seconds, a failed read `unread-grounds` (§5.2). |
| 10 | The `session` ground compares a field no record has | `Driver/Trees/SessionGroups.cs:14-144`: `State` and `Updated`, no run id; `Updated` moves with the state (`Sessions.cs:890`, `:1562`) | **Taken**: the ground compares `State` and `Updated`; *ran another turn* is its own reason; `{then}`/`{now}` are worded by `sessionState.*` (§5.1, §8.3). §5.1 says what it does not see: a turn that moved no state, which a named question's `wait` ground covers. |
| 11 | A press asked for a value already set makes a card that can only be refused | round 1 §5.1 | **Taken**: judged at the ask, answered `already` with no card, exit 0 (§5.1). At the Confirm the same case is `changed`. |
| 12 | The card's time lines and `gone` also serve the service door | `Confirmations.tsx:24-50`, `:241`, `:331-333`; `PersonConfirmations.cs:127` | **Taken**: `person.waits` for `source: 'pipe'` alone; `person.left`, `person.until` and the two minutes' `gone` (`person.goneService`) kept for `source: 'service'` (§8.1). |
| 13 | Row 1a breaks the window until 1c; no test sees it | `Confirmations.tsx:44`, `:272`, `:332`; `attention.ts:521`, `:906`; `AttentionStateSliceTests.cs:1648-1656` | **Taken**: `expires` is sent `null` for a pipe's press in the same row that ends the clock, and that row's web lane draws it; the twin test extends to `confirmation`'s fields (§8.2, §10). |
| 14 | The verb's proof and `--wait` placed wrongly | `DriverCommand.cs:78`; `DriverCommandTests.cs:101`; `cli-usage.test.ts:9-16` is the TypeScript CLI's | **Taken**: `withdraw <id>` in `DriverCommand.Usage`, held by `DriverCommandTests`; `--wait` parsed once in `InstanceHost.World`; each family's usage text and golden named in 1b (§4.3, §10). |
| 15 | The bound is ten, but eight connections, and `--wait` to a day | `InstancePipe.cs:19`, `:262`, `:1029-1033` | **Taken**: `--wait` capped at thirty minutes, the default today; the bound of ten stated beside `Connections`, which waiting cards do not hold once their verbs return (§4.1, §9.2). |
| 16 | The restart's ordering leaves holes | `PipeJournal.cs:74`, `:189` (24 hours); `InstancePipe.cs:712-718` (a failed write swallowed); `:237-255` (`Serve` may not serve) | **Taken** for the first two: the record carries `begun` and `ended` itself, and is removed even where the journal line failed (§7.1). **Taken in part** for the third: the restore runs in the loop before `Serve`, so cards come back where the pipe is not served; where another instance already serves the home's pipe it does not, since two instances would show one press twice (§7.2, §11). |
| 17 | Unread records handled badly | `StandingConfirmation.cs:146-150`; `RemotesModule.cs:51` (`SET` carries a key) | **Taken**: a newer record left in place, not asked, one notice; a cut record ended `Lost` and removed, no `unread/` folder; §7.1 says which payloads reach the disk and for how long. |
| 18 | Withdrawals and asks flood the person | `PipeConfirmations.cs:181-182` (a balloon per ask) | **Taken**: a withdrawal is a notice only where some window showed the card; the ask's balloon coalesces to one a minute (§6, §8.2, §9.4). |
| 19 | An old card does not say whether anyone waits on it, or who sent it after a restart | round 1 §8.1-§8.2; `PipeConfirmations.cs:11` (`PipeAsker`) | **Taken**: `waitedOn`, re-told when a line ends, drives the card's line and the row's group; a restored card says the record's asker as the record's (§8.1, §8.2). |
| 20 | The words conflict with the glossary and miss forms | `glossary.json:488`; `agents.json:8-9`; `notifications.json:39` | **Taken**: English keeps *a terminal's press*; the zh keeps 请求 as today's catalogue does; counts carry `_one`/`_other`; `{setting}`, `{then}`, `{now}` worded by the catalogue (§8.3). |
| 21 | `SentPresses`' longer keep incomplete; a late `result` says something false | `SentPresses.cs:52`, `:74`; `InstanceDoor.cs:461-462` | **Taken**: thirty days at most, `Find` and `Prune` reading one rule through `waitingAt`; `unknown` says Daoris keeps an ending a day (§4.2). |
| 22 | The account of D201, D208 and D209 is off | `InstancePipe.cs:559-571`, `:621-631`; `StandingConfirmation.cs:160-162`, `:193`; the steward design §5.4 | **Taken**: *What this is not* corrected; until STEWARDROLE1d every press is a card; a waiting press marks no `owner-set`; the grounds are `PipeCensus.Targets`' second column, one table (§5.1). **Deferred**: a bound per D201 caller class, until D201 part (i) makes the look tell (§9.5). |
| 23 | The proofs leave claims unproved; the crash is not a crash | `InstancePipe.cs:339` (`Dispose` calls `BeginStopping`) | **Taken**: one case each for withdraw and its race, the `ask`, `plan`, `branch`, `wait` and `setting`-changed grounds, a change before the ask, `already`, `ungrounded`, the file name, the restore's judgement and bound, a failed journal line; the crash is a second pipe over a home whose first was never disposed (§10). Joining is dropped, so it has no case. |
| 24 | The first row is too large | round 1 §10 | **Taken**: 1a-i (no time end, the grounds whole, `waiting`, `WITHDRAW`, the card's and row's lines, a close still ending presses with a notice) then 1a-ii (the records and the restart); the verb, 1b, between them or beside them (§10). The grounds land whole in 1a-i rather than with today's clock kept for unclassified routes, since the table is one change and a clock kept for some routes would be a second rule to retire. |
| 25 | Smaller misstatements; the close's line names no code | `InstancePipe.cs:859-862` (`RESULT` reads `_running`); `InstanceDoor.cs:274`, `:461-462` | **Taken**: §1.3 says a waiting press is in `_running` alone and `RESULT` reads it there; §1.6 quotes the crash's sentences as they are; the close answers `waiting`, `why: close`, which an old verb reads as a refusal, exit 2 (§4.1, §7.2). |

## 14. Round 2's read: where each named change went

Every finding of the Opus foundation read of round 2 (`docs/2026-10-24-confirm-wait-reads.md`, round 2 §4), taken as the read names it,
each checked against the code on main at `1248aa393`. Blocking in bold.

| # | Finding | Where it went | Checked against |
|---|---|---|---|
| **1** | **The `plan` fingerprint is a rule, not a projection per route** | §5.1, *What each `plan` fingerprint takes*: each plan read names the fields it takes and leaves out; `SETUP_PLAN` leaves out its title, body, commit and tool versions; a plugin press takes every file under the folder it copies or runs, not `PLUGIN_READ`'s manifest alone; §10's next-morning and plugin-code rows | `SetupQuests.cs:53-56`; `SetupPress.cs:140-148`; `SetupRoutes.cs:96-116`; `PluginsRoutes.cs:158-185` |
| **2** | **The record lives in `run/` and may hold a deployment key** | §7.1: the records in `data/pipe-waiting`, a `HomeEntry` of their own in the three twins and `home-census.test.ts`; a keyed press (`DAORIS.REMOTES` `SET`) keeps no payload and does not wait across a restart, said on its card; §11 says why the key is not kept in `profiles/` | `DaorisHome.cs:365`, `:406`; `D172.md:19`, `:27-28`, `:45-47`; `RemotesModule.cs:51` |
| 3 | The held-pipe exception contradicts the restore's order and the lease | §7.2: the restore's ground is the home's lease, and it restores whatever `Serve` answers; §11 rejects the exception; §10's crash row has the first pipe still holding the name | `DriverLoop.cs:834`, `:852`; `InstancePipe.cs:237-255` |
| 4 | 1a-i can land before 1b | §10: 1b first, then 1a-i, then 1a-ii directly after; 1a-i's close still ends what waits, said; §3's second change names it | `InstanceDoor.cs:272-274`, `:460-464` |
| 5 | The verb's end chosen by the wrong signal; three details | §4.1: the end chosen by `RESULT`'s state; `--wait` taken in the host's entry and set on the world; `already` exit 0 beside `:288` | `InstanceDoor.cs:262-264`, `:288`; `InstanceHost.cs:10-13` |
| 6 | At the Confirm the press leaves the card before it is judged | §5.2: the card kept `checking: true` until judged, `WITHDRAW` answering `running` then; `PERSON_CONFIRM` sent with the plan's bound; §8.1 | `PipeConfirmations.cs:340-357`; `Confirmations.tsx:230-237`; `bridge/person.ts:164-165`; `bridge/call.ts:8-14`, `:33` |
| 7 | A failed read at the Confirm ends a press that waited hours | §5.2: it stays waiting, `person.unread` on the card; §11 | `InstancePipe.cs:695` (the drain's case it mirrors) |
| 8 | The plan read's 60 seconds is shorter than the page's | §5.1: each plan row's bound, never shorter than the page's own for the same read | `bridge/repositories/setup.ts:18`; `bridge/call.ts:36-37`; `lines.ts:173`, `:404-406`; `history.ts:35` |
| 9 | Readers named for four grounds of nine | §5.1: a *Read by* column, the setting readers and the wait readers, each with the payload's field for `already` | `DriverRoutes.cs:23`, `:49`, `:59`, `:73`; `RulesRoutes.cs:18`; `ConversationRoutes.cs:412`, `:437`; `SessionsRoutes.cs:292`, `:347`, `:367`, `:510`, `:532`, `:563`; `TreesRoutes.cs:760`, `:904`; `WorkflowRoutes.cs:222`; `HelpRoutes.cs:378`; `ToolsRoutes.cs:47`; `AccountsRoutes.cs:29`; `ConsoleRoutes.cs:44`; `LinesRoutes.cs:558` |
| 10 | Four rows name the wrong thing | §5.1: `LAND_SESSION_TREE`'s branch is the tree head by `id`; `DISCARD_SESSION_TREE` with `force` fingerprints the uncommitted paths; `SESSION_DELETE` excepted with its reason; `TOOLS_GIT` every `way` but `system` and `managed` | `TreesRoutes.cs:429-431`, `:470-477`, `:498`; `PipeCensus.cs:290` |
| 11 | The ask's read placed before the standing judgement and the bound | §5.2: `ungrounded` after the standing judgement, the bound and its place taken before any read, the read in the press's later task | `InstancePipe.cs:521`, `:624-631`, `:637`, `:686`, `:980` |
| 12 | *What needs you*'s row is drawn nowhere | §1.5 says so; §8.2: the row .NET's and undrawn, its group rule dropped, `waitedOn` the card's alone | `attention.ts:114-116`, `:185`; `AttentionBand.tsx:152-153`; `NeedsSummary.tsx:63`; `App.tsx:513` |
| 13 | Coalescing `Attend` folds the page's toasts | §8.2: coalesced in `SessionBalloon`, one `Confirming` event per press kept; §9.4; §11; §12's D196 line | `PipeConfirmations.cs:363-371`; `notifications/announce.ts:15-21`; `SessionBalloon.cs:132-134` |
| 14 | For thirty minutes the connections, not the ten, are the bound | §3 and §9.2 say so; `PipeLimits.Connections` sixteen, declared beside `Waiting`, a test holding the order | `InstancePipe.cs:19`, `:262`, `:1029-1033` |
| 15 | What stays a card after STEWARDROLE1d is unstated | *What this is not*'s *After STEWARDROLE1d*; §5.1's shared table adds `ask` | `D209.md:73`; the steward design's rows 1e-1g (`:976-1017`) |
| 16 | §12 misses D171 | §12: D171's RPCOPEN1 note (its RPC1j and RPC1h sentences) and its PUSH1f note; the D156 note names them | `D171.md:348`, `:365-371`, `:392`, `:403-405` |
| 17 | The close must complete the waiting presses' answers | §7.2: the close completes each waiting line with `waiting` and takes it from `_running` and `_owners`, its record kept; §10's drain row | `InstancePipe.cs:288-316`, `:609-610`; `DriverLoop.cs:652` |
| 18 | An hours-old card that rests on a plan shows the payload alone | §8.1: a door from the card to the plan's own page, `person.openPlan` | — (a page door; no code yet) |
| 19 | The words | §8.3: *could not be restored*; `person.checking`, `person.unread`, `person.restoredPid`, `person.restoredUnknown` (and `person.keyed`, `person.openPlan`), en and zh; `status.*` for a quest; `{act}` by `person.route.<ROUTE>` | `locales/en/status.json:2-3`; `locales/en/person.json:30-32`, `:105` |
| 20 | Proofs missing | §10: one row each for `waitedOn`, a failed read at the ask and at the Confirm, the set-up across midnight, `WITHDRAW` during the read, the drain's refusal, 1a-i's close notice, a close draining at once, two asks in a minute and the toasts | — |

**What stays owed.** Codex's read, on this text (D191, its 2026-10-23 amendment). The design stays proposed until both reads' blocking
findings are taken.
