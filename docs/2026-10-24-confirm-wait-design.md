# A terminal's request waits until the person answers: no two minutes, no ten, a row in Notifications until it is confirmed, refused or withdrawn, kept across a restart (proposed, round 1, 2026-10-24)

**Standing:** proposed, round 1, a note amending D156 (CONFIRMWAIT1), under D191: two reads follow before anything is built.
Written by Claude Opus 5.5 from the code on main at `aa16658e7`. Nothing here is built.

The row CONFIRMWAIT1 in `TASKS.md` carries the case: a press from a terminal that needs the window's Confirm (a setup, a repository
driven, a session's words) ends two minutes after its card shows, or ten if no window shows it, and *Nothing was done*. On 2026-10-24
several were missed that way and each had to be sent again (a setup, one repository's drive twice, a session's go-on). The row's
direction: a request waits as a row in *Notifications* until the person confirms or refuses it, or its asker withdraws it; the press
is made when confirmed and its asker told then; a press whose grounds changed while it waited is refused at the Confirm with its
reason.

D156's CONFIRMSEEN1 note (2026-10-21) rejected exactly this: *a wait with no bound was rejected because the terminal's verb would
hold its line indefinitely* (`docs/decisions/D156.md:249-250`). This design reverses that rejection, and §3 says why its reason no
longer binds.

**What this is not.** TERMTRUST1 (D201, accepted) and STEWARDROLE1 (D209, proposed) aim for development's routine presses to need no
card at all: a press the standing confirmation or the steward's appointment covers is admitted before any card is asked
(`InstancePipe.cs:621-631`), and this design does not touch that path. What remains on the card is the person's own requests and
whatever no grant covers; this is their path, not a substitute for those designs. A press either design admits never waits here.

Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, module paths under `src/Daoris.Desktop/Daoris.Desktop.Modules/`,
page paths under `src/Daoris.Web/src/`.

## 1. Today, read from the code

### 1.1 Where the two minutes and the ten live

- **`PipeConfirmations`** (`Platform/PipeConfirmations.cs`) holds every waiting press in memory, never in the store
  (`:55-58`). Its constructor takes `within`, two minutes by default (`:123`), and `find`, never less than `within` (`:125-126`).
- **`FindWithin`** is ten minutes (`:133`): a press asked waits that long to be shown (`Ask`, `:166-174`, its expiry
  `asked + _find` and its timer made under the same lock).
- **The shown timer**: the window's first word that the card is in front of the person moves the expiry to `now + _within` and
  resets the timer (`Show`, `:191-228`, the change at `:212-213`). An answer or a word at or past its time ends it `Expired`, the
  clock read first (`End`, `:320-339`; NOTIFY2a). The timer's firing is `Expire` (`:314`).
- **The pipe's limits** repeat both: `PipeLimits.Waiting` five and `PipeLimits.Confirm` two minutes (`Platform/InstancePipe.cs:42-46`),
  handed to `PipeConfirmations` at `:199`.
- **The service's own door** has its own two minutes (`src/Daoris.Service/Daoris.Service.Http/PersonConfirmations.cs:127`, `Lasts`,
  and `:130`, `Kept`). It grants a secret to send one HTTP request once; no verb of this machine asks it (only the desktop's reads,
  `Driver/Service/ConfirmationDoors.cs:11-15`). It is out of this design's scope (§9).

### 1.2 The terminal's wait and its exit codes

- `InstanceWorld.Wait` is thirty minutes (`Driver/Platform/InstanceDoor.cs:62`). `PressAsync` (`:234`) keeps the press's id and key
  before sending it (`SentPresses`, `:253`), sends it with that wait, and on `CONFIRMING` writes *waiting for you to confirm this in
  Daoris's window…* (`Told`, `:490-502`).
- An answer lost, the connection's or the wait's, is asked for by `RESULT` on a new connection, never sent again (`:262-279`):
  `answered` is said; `unknown` says *Daoris does not know how it ended … it stopped while this was under way* and `running` says
  *still working on it*, both exit 2 (`:271-276`).
- A refusal is `message (code)` on the error stream; `refused` and `expired` are exit 1, every other refusal exit 2 (`:286-288`).
- `daoris-driver result <id>` (`ResultAsync`, `:413-465`) reads a kept press's answer by its key; a refused answer there is exit 2
  (`RefusedAsync`, `:468-472`), not the 1 `PressAsync` gives the same refusal.
- `SentPresses.Kept` is 24 hours from when it was sent (`Driver/Platform/SentPresses.cs:21`, read at `:52`).
- Today no press can still wait when the thirty minutes end: ten minutes to be found plus two once shown is twelve.

### 1.3 The journal: an id sent again says how it ended, and nothing runs twice

- `PipeJournal` (`Modules/Platform/PipeJournal.cs:22-36`) writes a line when a press begins and one when it ends, with its answer,
  one file per UTC day under `<home>/pipe/`, kept 24 hours (`PipeLimits.Kept`, `InstancePipe.cs:31`). Read once at the start, an id
  whose last line says it began is `Interrupted` (`PipeJournal.cs:28-30`).
- In `Accept`, an id running is joined by the key that sent it; an id the journal holds answers its kept answer, or *Daoris stopped
  while this was under way … it was not run again*; another binding is refused `id-taken` (`InstancePipe.cs:590-611`).
- **A waiting press is not in the journal.** It is in `_running` and `_waitingFor` alone (`:604-609`, `:652-655`); its line is
  written only once it ends (`ConfirmThenRunAsync`, `:686-726`, the journal at `:712-719`) or begins to run (`RunAsync`, `:763-799`).
- `CANCEL` from the terminal that sent it, by its key, withdraws a waiting press (`CancelAsync`, `:809-847`, the withdrawal at
  `:838-843`); `RESULT` answers `running` for a waiting one, then its ending (`Result`, `:850-870`).

### 1.4 Five waiting, and confirmations-full

`Ask` returns null where `Bound` already wait (`PipeConfirmations.cs:165`), and the pipe refuses the sixth `confirmations-full`:
*Five acts already wait for the person's confirmation, so this one was not asked. Nothing was kept.* (`InstancePipe.cs:637-651`).
Only what waits counts, so one answered frees its place. The five was D156 point 4's bound against a session filling the person's
attention (`D156.md:31-32`, `:80-81`).

### 1.5 Shown, the census, the balloon and What needs you

- `PERSON_CONFIRM_SHOWN` is the window's route (`Bridge/Routes/PersonRoutes.cs:56-72`) and `Window` in the pipe's census
  (`Platform/PipeCensus.cs:278-279`): a terminal saying it would start its own two minutes. `PERSON_CONFIRMATIONS` and
  `PERSON_CONFIRM` are the window's too (`:273`).
- `PERSON_CONFIRM` answers a press no longer waiting as data, `gone`, *answered in another window, its two minutes passed, or its
  terminal took it back* (`PersonRoutes.cs:35-54`; `locales/en/person.json:38`).
- On ask, `Attend` raises `SESSION_ATTENTION` of kind `Confirming` (`PipeConfirmations.cs:372-381`), which `SessionBalloon` shows
  while no window is in front (`Windows/SessionBalloon.cs:132-134`; `notifications.system.confirming.*`).
- Each waiting press is a `confirmation` row of *What needs you*, judged in .NET with no workspace (`Loop/NeedsAPerson.cs:464-481`),
  its fields `module`, `route`, `asker`, `expires`, `shownAt` (`Loop/AttentionStateSlice.cs:116`), worded *a terminal waits* and *It
  runs only once you confirm it on its card in Notifications, and is refused at {{time}}* (`locales/en/work.attention.json:112-113`).
- The card says *refused by itself in {{time}}* and *It waits until {{time}}, then is refused* (`locales/en/person.json:28`, `:33`;
  zh `{{time}}后自动拒绝`, `它会等到 {{time}}，之后即被拒绝。`).
- A press that ends `Expired` becomes a notice in *Done and failed* (`Unanswered`, `PipeConfirmations.cs:142`, `:355`; wired at
  `Loop/DriverLoop.cs:842`; kept by `Loop/NoticesStateSlice.cs:199-203`), worded *A terminal's press to {{act}} went unanswered, and ran
  out of time* (`locales/en/notifications.json:39`).

### 1.6 What a restart does to a press still waiting

- **A close**: `BeginStopping` refuses every new press and withdraws every waiting one (`InstancePipe.cs:275-279`, called from
  `DriverLoop.cs:1353`). `ConfirmThenRunAsync` answers it `stopping`, *Daoris is closing; run this again once it is back*, and
  journals that ending (`:707-719`). A terminal still connected hears it; one that left reads it by `result`. **The person is told
  nothing**: `Unanswered` fires only for `Expired` (`PipeConfirmations.cs:355`), so the row and the card vanish with no notice.
- **An update's drain** (`IsStopping`, `:225`) leaves a waiting press waiting, but a Confirm during the drain ends it refused
  `stopping` and runs nothing (`:695`, `:707`).
- **A crash** writes nothing: the press was never journaled, so after the start `RESULT` answers `unknown` and the verb says *it
  stopped while this was under way* (`InstanceDoor.cs:273`), which is wrong — it never began. The person is told nothing.

So today a press waiting at a close is told ended to its asker and silently lost to the person, and one waiting at a crash is
silently lost to both.

## 2. The rule

**A request waits until it ends, and it ends one of five ways, each said to its asker and kept in its record:**

| End | By | What runs | The asker hears (code) | The person sees |
|---|---|---|---|---|
| Confirmed | the person's Confirm, its grounds holding (§5) | the press, once | its answer, as today | the card goes; what the press changed |
| Refused | the person's Refuse | nothing | `refused` | the card goes |
| Changed | the person's Confirm, a ground no longer holding (§5) | nothing | `changed`, with the reason | the reason, under the card's place, and a toast |
| Withdrawn | its asker (§6) | nothing | the kit's cancel, as today | the row goes; a notice in *Done and failed* |
| Unreadable | a restart that could not read it back (§7) | nothing | `lost`, where its id read | a notice in *Done and failed* |

There is no time end. `Expired` is no longer made by the pipe; the code stays a word of the wire, as `confirm-in-window` stayed
(`InstancePipe.cs:110-113`), for a terminal talking to an instance from before.

The task row's *declines* is the card's **Refuse** (拒绝), the word the card already uses (`person.json:36`), so the chrome keeps
one word for it.

## 3. Why D156's rejection no longer binds

CONFIRMSEEN1 rejected a wait with no bound because *the terminal's verb would hold its line indefinitely*. That held while the press
existed only as the verb's open request: the instance kept it in memory beside the connection (§1.3), the verb learned its end only
on that line or by `RESULT` within the line's day, and a verb that gave up had no way back to it. Three changes take that away:

1. **The request outlives the terminal's wait.** It is a record of its own, written when it is asked (§7.1), a row of *What needs you*
   and a card in *Notifications* (§8), whether or not any terminal is still connected. The connection is how an asker hears, never
   what keeps the request.
2. **The verb returns with the request's id while the press stays asked.** At the end of its own wait, or at once with `--no-wait`,
   the verb says the request waits, names its id and how to read or withdraw it, and exits (§4). Nothing holds its line.
3. **The asker reads how it ended from the request's record.** `RESULT` answers `waiting` while it waits and its ending after
   (§4.2), from the record the instance keeps as long as it waits and a day after it ends. A terminal still waiting on its line hears
   the ending there, as today.

The two minutes once shown stay rejected as a bound for the reason the row gives: they ended requests the person had not refused.
What CONFIRMSEEN1 meant to keep — a card in front of the person never answered by default — is kept by the card's default being no
(§8.1) and by the bound on how many wait (§9), not by a clock.

## 4. The asker's side

### 4.1 The verb

- `PressAsync` keeps its thirty-minute wait as the default, and gains `--wait <minutes>` (0 to 1440) on every verb that presses a
  person's route, and `--no-wait`, which is `--wait 0`: the verb returns once the instance says `CONFIRMING`.
- **At the end of its wait, a press still waiting is not lost and not an error.** The verb writes, on the error stream:
  *still waiting for your confirmation in Daoris's window (request {id}); `daoris-driver result {id}` says how it ended, and
  `daoris-driver withdraw {id}` takes it back (waiting)*, and exits **1**: nothing was done yet, and a caller that reads non-zero as
  *not done* is right. Today's `running` path for this case (exit 2, *still working on it*) is for a press under way, not one waiting.
- The verb tells the two apart from the `CONFIRMING` it heard, and, where its line was lost before it heard one, from `RESULT`'s
  `waiting`.
- **Ctrl+C still withdraws** a waiting press it sent (`CANCEL`, §1.3): the person's interrupt of their own verb is their word that it
  is not wanted. The wait's end does not withdraw.

### 4.2 The record it reads

- `RESULT { id, key }` gains the state `waiting`, with `asked` and, once shown, `shownAt`; then `answered` with its ending as today.
- `daoris-driver result <id>` says `waiting` as the verb's own end does, exit 1, and says the request's ending: the answer's words where
  confirmed and run; `refused`, `changed` (with its reason), `withdrawn` and `lost` exit **1**, aligned with `PressAsync`
  (`InstanceDoor.cs:288`), where today it says each refusal exit 2 (`:468-472`).
- `SentPresses` keeps a press whose last read state was `waiting` past its 24 hours, until a read finds it ended or the instance
  answers that it does not know it; then the 24 hours run from that read. A press is kept under the terminal's home as today
  (`SentPresses.cs:25`).

### 4.3 Withdrawing one

`daoris-driver withdraw <id>` sends the kit's `CANCEL { requestId }` with the kept key on a new connection, which `CancelAsync`
already takes for a waiting press of that key (`InstancePipe.cs:838-843`): the request ends `Withdrawn`, its card and row go, and
it answers `withdrawn`. One already ended answers `cancelled: false, state: ended` (`:827-830`), said as *it had already ended:*
and its ending. A key that did not send it is refused `id-taken` as today. Only the key's holder withdraws; the person refuses on
the card. A new CLI verb, so `cli-usage.test.ts`'s refusals fixture is the census its row runs.

## 5. A press whose grounds changed while it waited

A request asked at 09:00 and confirmed at 15:00 may name a world that moved. The route's own handler still judges what it always
did; what it cannot know is what the person was shown and what the asker saw when it asked. So **each person route names its
grounds**, read at the ask and read again at the Confirm, and a Confirm whose grounds no longer hold ends the request `Changed`,
runs nothing and says why.

### 5.1 The grounds

| Ground | Read at the ask, by the payload's named field | Holds at the Confirm when | The reason said (en) |
|---|---|---|---|
| `session` | the session's state and its latest run's id | the session exists and both are the same | *the session moved on while this waited: it was {then}, it is {now}* |
| `setting` | the setting's value | the value is the same, and not already what the press sets | *{setting} is already set as this asks* / *{setting} changed while this waited* |
| `repository` | that the repository is registered here | it still is | *the repository {repository} is no longer registered on this machine* |
| `quest` | the quest's state | the same (a try again's quest still parked or held) | *the quest no longer waits to be tried again* |
| `ask` | the ask's state | the same (a landing's ask still open) | *the ask was closed while this waited* |
| `timeless` | nothing | always | — |

A route names one or more grounds, or is `timeless` with its reason (a sweep, bringing branches up to date, marking news read: each
acts on what is there when it runs, and the person's Confirm is their word on that). **The grounds are a table beside the census**
(`PipeGrounds`, next to `PipeCensus`), and a test holds that every route `PipeCensus` makes `Person` (`PipeCensus.cs:212-266`) names
grounds or `timeless`, as `PipeCensusTests` holds the census; a route added without them fails it.

What a ground compares is recorded on the request as codes and values the instance read (a state, a run's id, a setting's value),
never the asker's words. The *already set* case refuses rather than runs as a no-op because it is evidence the person's world
changed under the request, and the reason tells them so.

### 5.2 Where and when it is judged

- In `PipeConfirmations.Answer` on Confirm, under its lock, before the request is taken from what waits: the grounds are read through
  the reader the loop hands it (as `RunsHere` and `HeldNow` are handed to the standing confirmation, `DriverLoop.cs:844-849`). A ground
  that does not hold ends it `Changed` with its reason; `PERSON_CONFIRM` answers `{ answered: false, why: "changed", reason }` and
  the card's place says it (§8.1).
- The grounds are not a lock: between the read and the route's run the world can move again, and the route's own judgement and the
  store's transactions stay the guard, as today. They catch what moved during the wait, which is hours where the race is milliseconds.
- A refusal is the person's to see, not a blocking check on the card's drawing: grounds are not judged to hide or grey a card,
  since a read on every telling of the list would run on every change of every window.

## 6. The asker withdraws one

§4.3 is the asker's door; it is the only one besides the person's answer. Each withdrawal is said to the person as a notice in *Done
and failed* (`notifications.done.withdrawn`, §8.3), since a row that vanishes from *What needs you* with nothing said reads as lost.
The instance's close no longer withdraws (§7.2).

## 7. A restart

### 7.1 The request's record

- **Written when asked**, before `CONFIRMING` is told: `<home>/pipe/waiting/<id>.json`, the `pipe` entry of the home's table
  (`DaorisHome.Entry(_home, HomeEntry.Pipe)`, as the journal's folder is asked, `InstancePipe.cs:260-261`), written atomically as
  BOM-less UTF-8. It holds the request's id, its binding (route, payload fingerprint, key fingerprint, as `PipeBinding`), the
  payload as sent, its card's fields and named values, the asker as the system named it, `asked`, `shownAt`, its grounds as read,
  and a version. A request whose record cannot be written is refused `unrecorded` and nothing is asked, as a press whose journal
  line cannot be written is today (`:770-774`).
- **Removed when it ends**, after its ending's line is in the journal. A Confirm writes the journal's *begin* line before the record
  is removed, so the order is: begin, remove, run, end.
- Holding the payload on disk adds no reach: the session records and the notices already keep the person's words under the home, and
  D156 point 7 says what a process of the account reaches there (`D156.md:42-48`). A record a process wrote itself grants nothing it
  could not ask over the pipe: its card shows what it would do and the default is no. What it could forge is the asker's name, so a
  record read back after a restart shows the asker as *asked before Daoris restarted*, never a process name (§8.1).

### 7.2 What a restart does

- **A close no longer withdraws.** `BeginStopping` refuses new presses as today, ends the connections' waits with `stopping` as their
  answer *on the line* (*Daoris is closing; this still waits for your confirmation, request {id}*), and leaves the requests waiting
  in their records. The pipe's `WithdrawAll` on close goes; `Dispose` stops the timers it no longer has.
- **At the start**, before the pipe is served (`InstancePipe.Serve`, after the journal is read): each record in `waiting/` is read.
  One whose id the journal holds (begun or answered) is removed and not asked again: it was confirmed before the stop, and the journal
  says how it ended, so nothing runs twice. Every other is waiting again, its row and card back, and the balloon says once
  *{count} requests from a terminal still wait for you* while no window is in front.
- **One that does not read** (cut, or written by a newer Daoris) is moved aside into `waiting/unread/` and never asked; where its id
  reads, the journal gets its ending `lost` so `RESULT` says so; the person gets one notice, *a terminal's request could not be read
  back after Daoris restarted, and was not done*. Never silently lost.
- **An update's drain** leaves requests waiting as above; a Confirm during the drain is refused on the card with *Daoris is about to
  restart; this still waits*, and the request stays waiting, rather than ending `stopping` (`InstancePipe.cs:695`).
- **A crash** loses nothing written: the record was written at the ask.

### 7.3 How long the records keep

A waiting request's record keeps until it ends. Its ending keeps in the journal 24 hours, as every press's does
(`PipeJournal.cs:225-228`). A request that waits a week is a week's row in *What needs you*, which the bound (§9) keeps from growing.

## 8. What the person sees

### 8.1 The card (`person/Confirmations.tsx`, drawn in *Notifications*, NOTIFY2b)

- *asked {{ago}}* stays and grows in weight once a request has waited over an hour, so a stale-looking card reads its age first.
- *refused by itself in {{time}}* (`person.left`) and *It waits until {{time}}, then is refused* (`person.until`) go; one line,
  `person.waits`, says it waits until they answer.
- The shown word stays (`PERSON_CONFIRM_SHOWN`, the census's `Window` row unchanged): it moves no clock, it records `shownAt` once and
  logs `shown`, and it ends the bell's unshown warning hue as NOTIFY2b has it.
- A request read back after a restart names its asker as *asked before Daoris restarted*.
- A `changed` answer is said where the card was, with its reason, and toasted where the card has gone.
- `person.gone` loses *its two minutes passed*.

### 8.2 What needs you and the balloon

- The row stays a `confirmation` row, no workspace. Its field `expires` goes and `restored` joins, both in `AttentionStateSlice`'s
  fields (`:116`) and the page's `PushedAttention`, which the slice's test classes against each other.
- Its group: *Holding work* only while a terminal is still connected waiting on it; otherwise the group of a request the person owes
  an answer to, with no work holding. Its why loses *is refused at {{time}}*.
- The balloon on ask is unchanged (`Attend`); the restart's one balloon is new (§7.2). No reminder repeats: the bell's count and
  the taskbar's lit button already stay while a request waits unshown (NOTIFY2b).

### 8.3 The words, en and zh

Glossary words: 委托 for quest, 需求 for ask, 重试 for try again, 终端 for terminal, 通知 for Notifications, 仓库 for repository.
请求 is free for a terminal's request: the glossary avoids it only as the word for *ask* (`locales/glossary.json`, the `ask` row).

| Key | en | zh |
|---|---|---|
| `person.waits` | It waits until you answer. | 它会一直等到你回答。 |
| `person.restored` | Asked before Daoris restarted; Daoris can no longer name what sent it. | 在 Daoris 重启前发出；Daoris 已无法说出是什么发出的。 |
| `person.gone` | This no longer waits: it was answered in another window, or its terminal took it back. | 此请求已不再等待：它已在另一个窗口中得到回答，或其终端已将它撤回。 |
| `person.changed.session` | Not done: the session moved on while this waited (it was {{then}}, it is {{now}}). | 未执行：等待期间该会话已有进展（当时{{then}}，现在{{now}}）。 |
| `person.changed.already` | Not done: {{setting}} is already set as this asks. | 未执行：{{setting}} 已是此请求要设的值。 |
| `person.changed.setting` | Not done: {{setting}} changed while this waited. | 未执行：等待期间 {{setting}} 已被更改。 |
| `person.changed.repository` | Not done: the repository {{repository}} is no longer registered on this machine. | 未执行：仓库 {{repository}} 已不再登记在本机。 |
| `person.changed.quest` | Not done: the quest no longer waits to be tried again. | 未执行：该委托已不再等待重试。 |
| `person.changed.ask` | Not done: the ask was closed while this waited. | 未执行：等待期间该需求已关闭。 |
| `person.draining` | Daoris is about to restart; this still waits. | Daoris 即将重启；此请求仍在等待。 |
| `work.attention.confirmationWhy` | It runs only once you confirm it on its card in Notifications; it waits until you answer. | 只有你在通知中它的卡片上确认后它才会运行；它会一直等到你回答。 |
| `notifications.done.withdrawn` | A terminal took back its request to {{act}} | 一个终端撤回了请求：{{act}} |
| `notifications.done.lost` | A terminal's request could not be read back after Daoris restarted, and was not done | Daoris 重启后无法读回一个终端的请求，它未被执行 |
| `notifications.system.restored.title` | Requests from a terminal still wait | 终端的请求仍在等待 |
| `notifications.system.restored.body` | {{count}} still wait for your confirmation. Open Daoris to confirm or refuse them. | {{count}} 个请求仍在等你确认。打开 Daoris 确认或拒绝它们。 |

`notifications.done.expired` stays for a notice kept before this build. Each new key is in both catalogues
(`.claude/knowledge/translation-parity.md`). The terminal's sentences are the verb's own English, as every verb's are.

## 9. The bound, once requests no longer expire

The five bounded a flood a clock also drained; without the clock it is the only drain besides the person. Three changes:

1. **One press, one request.** A press asked while a request with the same route and payload fingerprint waits joins it rather than
   making a second card: the second asker is told `CONFIRMING` naming the waiting request's id, keeps its own key and id, and hears
   the same ending. The 2026-10-24 case (one repository's drive sent twice) is one card. The journal binds each asker's id to its own
   key, so `RESULT` answers each.
2. **The bound is ten** cards waiting at once across every terminal, counted as today (only what waits), and a press that joins one
   counts nothing. Five was set when a card lasted at most twelve minutes; the day of the incident held four distinct requests in a
   few hours, and a person away for a day should come back to a list, not a refusal. Ten is two screens' worth of the card list, the
   most a person can read in one sitting without the list becoming the work.
3. **`confirmations-full` names the way out.** Its sentence says *Ten requests already wait for the person's confirmation, the oldest
   asked {{ago}}; this one was not asked. Nothing was kept.* The asker can withdraw its own; the person refuses on the cards.

## 10. The rows, with their proofs

Order keeps main working: the instance first, with the verb's old reading still sound; the verb second; the window last.

- **CONFIRMWAIT1a — the instance** (modules). No time end; the record written at ask and read back at the start; the close stops
  withdrawing; `RESULT`'s `waiting`; the grounds table and its census; `Changed`; joining; the bound of ten; the withdrawn and lost
  notices; the row's fields. Held by `PipeConfirmationsTests`, `PipeConfirmTests`, `PipeCensusTests` (the grounds census beside it),
  `AttentionStateSliceTests`, `NoticesStateSliceTests`, `DriverModulePersonTests`; the home's new folder by `home-census.test.ts`.
- **CONFIRMWAIT1b — the verb** (driver). `--wait`, `--no-wait`, the `waiting` end and its exit 1; `result`'s `waiting` and its exit
  codes aligned; `withdraw <id>`; `SentPresses` keeping a waiting press. Held by the driver's `InstanceDoor` tests and
  `cli-usage.test.ts`.
- **CONFIRMWAIT1c — the window** (web). The card's lines, `changed` said in place and toasted, the restored asker, the row's why and
  group, the restart's balloon, the words in en and zh. Held by `person/Confirmations`, `bridge/person`, `work/attention` and
  `app/notifications` tests, en and zh, and `SessionBalloonTests`.

The rows the task names, each with its proof:

| Row | Proof |
|---|---|
| A card left unanswered for an hour, then confirmed, makes its press and tells its asker | The services' clock moved an hour past the ask (and past a `shown`): the request still waits; Confirm runs the route once; a verb still on its line (`--wait 120`) hears the route's answer; a verb that returned at `--no-wait` reads it by `result`, exit 0. `PipeConfirmationsTests` and the end-to-end case in `DriverModuleSessionsTests` through the real pipe. |
| A refused one does nothing and says so | Refuse: no dispatch (the dispatcher's stub counts zero), the terminal's line says *The person refused this in Daoris's window. Nothing was done. (refused)*, exit 1; `result` says the same, exit 1; the journal holds `refused`. |
| A stale one is refused with its reason | One case per ground: a `SESSION_INPUT` whose session's run changed; a `SET_DRIVABLE` already set; a `SET_TREES` whose repository was removed from the registry; a `RETRY_QUEST` whose quest was tried again elsewhere. Each: no dispatch, `PERSON_CONFIRM` answers `changed` with the reason's code and values, the terminal hears `changed` with its sentence, exit 1; the card's line in en and zh. |
| One waiting across a restart | A request asked on one `InstancePipe` over a home, the pipe closed (`BeginStopping`, then dispose) and a second made on the same home: the request is listed again with `restored`, Confirm runs it once, and `RESULT` from a new connection with the first key answers it. A record whose id the journal already began is not asked again. A cut record is moved aside, the journal says `lost`, a notice says so. A crash is the same test with no `BeginStopping`. |

**What the gates will not cover.** No installed window will have shown a request across a real restart or an hour's wait: the
installed proof is the parent's. The grounds are read through the loop's readers, not raced against a store's transaction.

## 11. Rejected

- **A longer clock** (an hour, a day): the row's case is a person who was away, and any clock is a time at which a request they never
  refused ends as if refused.
- **The verb holding its line until the answer**, the rejection CONFIRMSEEN1 made, rightly: an agent's terminal or a script cannot
  hold a process for a day.
- **Ending every waiting request at a close, said as a notice**, the simpler of the two the row allows: restarts are routine (an
  update drains and restarts, D139), and each would cost the person every request again, the loss the row was written for.
- **A record sealed with a key**, so a forged one is refused: D156 rejected every home-held key (`D156.md:66-68`), and the record
  grants nothing a pipe ask does not.
- **Grounds judged on the route's own handler only**: a handler judges what is true now, and cannot know what the request rested on
  when it was asked.
- **Running an *already set* press as a no-op**: it would report success for a world the person did not see when they confirmed.
- **A per-terminal bound**: the asker is named by a process id, which is not an identity (`D156.md:67-68`) and does not survive a
  restart.
- **Keeping requests in the store**: the pipe's requests are the machine's and never leave it (D46); the home's `pipe` entry is
  where the journal already is.
- **The service door's two minutes** changed in the same design: its grant is a secret a client sends once, and no verb of this
  machine asks it; a wait there would need a client that holds the grant across a restart. Left as D196 point 4 has it.

## 12. What this amends, once accepted

- **D156 point 4**: *within two minutes* reads *once the person confirms it, however long it waited, its grounds still holding*.
  *Five may wait* reads *ten*.
- **D156's PERSONDOOR1b note**: unchanged; it is the service's door.
- **D156's RPC1c note**: a waiting press is kept in its record and survives the close; `expired` and `stopping` are no longer how a
  waiting press ends.
- **D156's CONFIRMSEEN1 note**: the two minutes from shown and the ten to be found go; the rejection of a wait with no bound is
  reversed (§3); the row, the balloon and the shown word stay.
- **D196's NOTIFY2a amendment of it**: the clock read first stays for nothing on the pipe, since nothing on the pipe has a time;
  NOTIFY2f's unanswered notice is no longer made by the pipe.
- **RPC1j** (`result <id>`): `waiting` is a state, and its refusals exit 1.
- **These stand**: D201 and D209's admissions before any card; D52 (no approval surface for agents: the card's default is no);
  `confirm-in-window` and `expired` as words of the wire.
