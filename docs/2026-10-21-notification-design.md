# Notifications: everything that comes to the person in one place the title bar opens, a terminal's press waiting there in plain words and never across the page, and Overview kept to a summary (proposed, 2026-10-21)

**Standing:** proposed under D196 (NOTIFY1), round 1 under D191. Nothing here is built; its rows (§9) build it. Codex reads this
round. In the author's view it changes no data model, protocol or concurrency that other designs rest on: it adds one small
machine-local record with one writer (§2.5) and one pushed slice in D182's shape. It does move decided surfaces that other designs
name: where *What needs you* is drawn (D150's UX6c, R5's "a door from *What needs you*"), the moment a terminal's press counts as
shown (CONFIRMSEEN1), and who words the system's notification (SURF5b). Whether that is a foundation in D191's amendment, so that
Fable reads §2.4 and §3 too, is the steward's to say (§11). Nothing here publishes, spends an account, signs in or removes the
person's data; §12 names the one choice the author leaves to the owner.

Read for it: the row NOTIFY1 in `TASKS.md`; CONFIRMSEEN1's archive entry and D156's note on it (lines 237-260); D156 points 4 and
the person-door design §4.2; the terminal RPC design §4.2 (lines 166-176); D182 points 1-6 and `.claude/knowledge/pushed-slices.md`;
D188 and the message-urgency design §1, §2, §8; D185 and the UI/UX design §2 (R1-R7); the platform language §2, §4 (*Toast*) and §5
(*Overview*); the UX6 design §6 and D150's UX6c note; the frame model §1 and §3c; the dock design's view table; D126's NOTIFY1b note;
D171's MAINWINDOWS3 notes; `.claude/knowledge/person-waits.md`, `bridge-presses.md` (rows 7, 9, 11) and `translation-parity.md`;
the glossary. Every statement about today was read from the code at `7c2bce824` and names its file and line; none was run, and
the owner's install was not looked at.

## 0. The case, and what the design rests on

**The case.** On 2026-10-21 the owner reported four things. A terminal's press waiting for the window's Confirm (D156 §4.2,
RPC1c) is drawn across the top of every page and blocks it until answered. Its card says its sentence (一个终端请求以你的名义对会话
说话。) with the route's raw name, `SESSION_INPUT`, beside it. It expires in two minutes, and five such presses expired that day
before the owner acted. And a session's messages are listed one by one on Overview, which makes Overview long and hides what it is
for. The owner's direction (the row): one notification center for everything that comes to the person, with unread counts and a
badge, opened from the title bar, each item answered or dismissed in place; a press shown there and announced without blocking
the page, in plain words; Overview kept to a short summary that links into it. The same day's *who it worked with* repeated eleven
times is RELSPAM1's defect, not this design's.

| Today | Read at |
|---|---|
| A terminal's press waits in a band under the strip, above every view of every main window, one card per press | `App.tsx:1104-1114`, `Confirmations.tsx:60-90` |
| The card's head is the act where the page words the route, followed by the route's name in code; every value follows by its JSON pointer | `Confirmations.tsx:134-139`, `:163-166`, `:176-185` |
| A press waits ten minutes for a window to show it, and two minutes from its first showing in a window that is visible and focused | `PipeConfirmations.cs:114`, `:120-124`, `:166-182`; `Confirmations.tsx:95-133` |
| Each press is also a `confirmation` row of *What needs you* in *Holding work*, its one control opening the card | `NeedsAPerson.cs:333-349`, `attention.ts:48-50`, `:567-592` |
| While no window of Daoris is in front, a press raises an English balloon naming its route; the notify switch does not reach it | `PipeConfirmations.cs:300-311`, `SessionNotifier.cs:75-100` |
| *What needs you* is Overview's lead; only *Ready for you* is bounded, at five rows | `OverviewView.tsx:74-76`, `AttentionList.tsx:14-15`, `:53-55` |
| A park's row carries the agent's question as its line, clamped to two lines | `attention.ts:430-431`, `AttentionRow.tsx:406-416` |
| One count reaches the taskbar: the first main window's, from its own workspace's rows | `windows.ts:104-112`, `Program.cs:218-227`, `SecondaryWindowHost.cs:69-83` |
| What ended or answered is said by a seven-second toast and a balloon, and kept in no list | `ShellSignals.tsx:143-160`, `ui.tsx:1766`, `SessionNotifier.cs:86-100` |

### 0.1 What it rests on

**CONFIRMSEEN1 is the first slice, and most of it stands.** It made a press findable: a row of *What needs you* the badge and the
taskbar count, a balloon while no window is in front, ten minutes to be found, two minutes counted from its first showing, the log
saying `shown` (D156's CONFIRMSEEN1 note). This design keeps the ten minutes, the two minutes, the `shown` line and its route
(`PERSON_CONFIRM_SHOWN`), the count, and the balloon's occasion. It changes four things: the card is drawn in the notification center
and nowhere across the page (§3.1); *shown* is said when the card itself is in view there, not when any window draws the band
(§3.4); the card speaks plain words and keeps the route's name under *Details* (§3.2); and the press's notification behind the
window speaks the window's language and lights the taskbar button until a window comes forward, which CONFIRMSEEN1 left unflashed
(§3.3).

**What else it rests on.** *What needs you* is judged in .NET once for every window (`NeedsAPerson`, `AttentionStateSlice`, PUSH1d4)
and keeps every rule `person-waits.md` lists; this design moves where it is drawn, never what it judges. D156: only the person
confirms, in the window, its default no. D188: urgency is the sender's statement of when a message should reach a working agent, and
nobody but the person sends *now*. D185: the rules the screens keep (R1-R7). D182: .NET judges, the page renders.

## 1. Everything that comes to the person today, and where it is drawn

*Blocks the page* means what the owner meant: it takes the page's room on every view, or covers what the person works on, until
it is answered. Nothing below lays a scrim or traps the focus; the drawers that do are forms the person opened (D41).

| What comes | Where it is drawn | Counted | Announced | Blocks the page |
|---|---|---|---|---|
| **A terminal's press waiting for Confirm** (`PERSON_CONFIRMATIONS`, `CONFIRMATIONS`; answered by `PERSON_CONFIRM`) | The band under the strip in every main window (`App.tsx:1104-1114`): per press its sentence, the route in code, its sender and end, a line per value, *Daoris cannot tell…*, *Refuse* and *Confirm* (`Confirmations.tsx:155-193`); and a `confirmation` row whose control opens the card (`attention.ts:567-592`) | as a row: Overview's badge, the first main window's taskbar | no toast (`ShellSignals.tsx:146-148`); a warning balloon behind, English, the route's name (`PipeConfirmations.cs:300-311`) | **Yes.** The whole width above every view, at least four lines and one more per value for each of up to five presses (the bound, `PipeConfirmations.cs:104`), nothing folds it, and it stays until answered or refused by its time |
| A confirmation at the service's own door (PERSONDOOR1b) | The same band, polled every five seconds while the page holds the key (`person.ts:74-95`); its act in the service's own words | no row | none | as above |
| The adoption notice (D156 §2.3) | One line in the same band, with *Dismiss* (`Confirmations.tsx:77-82`) | — | — | one line |
| The install's update (D139) | One line under the strip while something is staged (`App.tsx:1093-1102`) | — | — | one line |
| **What needs you** (PUSH1d4) | Overview's lead (`OverviewView.tsx:74-76`): three groups, each row with at most three controls and its door; *Holding work* and *Waiting for your word* unbounded (`AttentionList.tsx:53-55`) | Overview's activity badge, every row (`App.tsx:1132-1139`); the taskbar's overlay and title (`App.tsx:445-449`) | by kind, below | no; it pushes Overview's own lists down |
| Overview's own lists | The tiles, *Outstanding* (six, then a count), the repositories by index size (`OverviewView.tsx:78-243`) | Quests' badge | — | no |
| **A session's question** (a park, `awaiting-person`) | The session's head and foot (`waits.ts:151-153`); a `parked` row, its line the agent's question, two lines (`AttentionRow.tsx:406-416`) | as a row | `SESSION_ATTENTION` `Parked`: a toast in the error tone in every window (`ShellSignals.tsx:143-158`), a warning balloon behind (`SessionBalloon.cs:23-43`), its headline the driver's English (`Attention.cs:63-65`) | no |
| A go-ahead, a helper's ask, a held tool call, an answer the answers-alone bound holds | The session's head and foot (`waits.ts:157-175`), the ask's page for a go-ahead; each a row with its acts (`attention.ts:41-67`) | as rows | none: no `AttentionKind` raises them (`Attention.cs:10-37`). A held call is refused by itself at its bound (PERMUNTIL1) | no |
| **An agent's ask to hand its session over** (MIXAGENT1c) | The session's head alone (`waits.ts:34`, `:180`; `HandOverAgentAsk.tsx:47-57`) | **not counted**: no row (`NeedsAPerson.cs` has no such kind) | the look's English line, toasted (`DriverHandOvers.cs:181`) | no |
| **Session and ask channel messages** (D167, D188) | The ask's page (`AskPage.tsx:529`), the session's channel panel (`AttendedSession.tsx:305`), an ask row's *N unread* (`AskRow.tsx:100`), the rail's heading by ask (`SessionList.tsx:219-224`); unread past the person's mark (`asks.ts:107-122`), pulled per ask (`asks.ts:44-62`) | in no badge | never, at any level | no |
| **Work done and failed, and an answer** | `SESSION_ATTENTION` `Ended` (a session ended without the person asking), `Answered` (a conversation answered the person's words), `QuestParked`, `Waiting` (`Attention.cs:10-37`); *Ready for you* rows for work to review | `QuestParked` and `Waiting` as rows; an ending and an answer **nowhere** | a toast in each window, but an answer over the conversation on screen (`ShellSignals.tsx:149-152`); a balloon behind | no; once the toast closes nothing lists what ended, and Sessions shows the record's state |
| **The look's lines** (the tick's `events`) | The driver's English log lines, toasted when they change (`ShellSignals.tsx:53-63`): a look that could not read something and tries at the next, a session going on, a hand-over waiting for the person's word (`Driver.cs:603-1310`, `DriverHandOvers.cs:51-251`, `DriverBrainstorms.cs:98-133`) | — | as toasts | no |
| **Toasts** (`notify`) | One corner per window, 26rem, at most three (`signals.ts:316`), seven seconds, two lines and the rest in a tip, text only with no door (`ui.tsx:1730-1805`): a refusal whose place has gone (R3), an outcome, a query's error (`useErrorNotify`), and the three rows above | — | — | covers the corner while it shows |
| The strip's chips | The review chip while a set-up waits (`ReviewChip.tsx:33-48`), the browser's door | — | — | no |
| The activity and status bars | Sessions' count with its waiting dot (SESSBADGE1), Agents' signed-out accounts; the status bar's sessions working | — | — | no |
| **Outside the window** | One tray icon over the first main window, quiet while any window of Daoris is in front (`SessionNotifier.cs:43-163`); every line English, from .NET; the notify switch silences the look's (`DriverLoop.cs:781-790`, `:919-932`) and not a press's; a headless host prints `!  {line}` (`Driver.Host/Program.cs:834`) | the taskbar's count, one window's (above) | — | no |

### 1.1 What cuts across

1. **A wait with a clock is drawn as a page.** The press is the one arrival drawn above every view, because the card must be
   found; the band is how it was made findable, and it is what blocks.
2. **What arrives is said and not kept.** An ending or an answer is a toast for seven seconds and a balloon for a few: nothing
   lists what came while the person was away, and only the waits stay, as rows.
3. **The window's language stops at the window.** The balloons and the look's toasts are the driver's English (D24's verbatim
   rule), and the press's card and balloon name the census's constant, so a person reading 中文 meets `SESSION_INPUT` in English.
4. **Two windows, one count.** MAINWINDOWS3 gave each main window its own workspace, but only the first main window's page tells
   the taskbar; another wears that count only while the first is put away.
5. **Overview is two pages.** It is the landing that answers *is anything sitting* and the place where every wait is answered; the
   longer the second grows, the less the first says.
6. **Something waits on the person without a row.** An agent's ask to hand its session over is counted nowhere and announced only
   by an English log line.
7. **Nothing an agent says on a channel reaches the person** until they open the ask, though D188 gave each message a level.

## 2. The notification center

### 2.1 Where it is: a side bar view the title bar opens

- **The title bar's *Notifications*.** A button on the strip, in its trailing group before the browser's door: a bell with the
  count of what needs the person in open's hue (the badge Overview's icon wears today), and a dot in the accent while anything is
  unread. Its tip and its name for a reader say both: *Notifications: 3 need you, 5 unread*. On a narrow window the region toggles
  leave the strip first (UX7a); this button stays, since it is how anything is found.
- **Pressed, it opens the right side bar on its *Notifications* view.** Pressed while that view is shown, it gives the side bar
  back to the view it held, or closes it where it was closed. It is a view like the others (DOCK1b): moved to the panel by its
  tab's menu, kept where the person put it. View → *Notifications* and the palette reach it from the one command table (R5).
- **Why a side bar view.** It holds what is the same on every view (the frame model §3c), so it stays open and keeps its place
  while the person moves between views; it never lies over the page; and it stands beside the main area, so a press about the
  session the person attends is answered beside that session's conversation.
- **A shell's alone.** A plain browser has no side bar and no machine's notices (D47 §4, D118 §4): Overview's summary says there
  what it says today, that this is the app's.

```
┌ strip ──────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ ▦ Daoris  Workspace Edit View Go Run Terminal Help     [ games · Ctrl K ]        🔔 3 •   ◎   ◧ ▤ ◨   ─ □ ✕ │
├──┬───────────────┬─────────────────────────────────────────┬─────────────────────────────────────────────────┤
│▣ │ list          │ main area                               │ Notifications │ Timeline │ Ask Daoris        ⋯ │
│▤ │               │                                         │ Waiting for your confirmation                1 │
│▥ │               │                                         │ ┃ A terminal asks to say to Fix the export      │
│◈ │               │                                         │ ┃ feed, as your words, at its next step:        │
│⌕ │               │                                         │ ┃ “Run the database tests before you land.”     │
│  │               │                                         │ ┃ Sent by bash, process 4120 · asked 1 min ago  │
│  │               │                                         │ ┃ Refused by itself in 1 min 40 s               │
│  │               │                                         │ ┃ Daoris cannot tell whether you ran this or an │
│  │               │                                         │ ┃ agent did. Confirm only what you ran.         │
│  │               │                                         │ ┃ [ Refuse ] [ Confirm ]            Details ▸   │
│  │               │                                         │ What needs you                               2 │
│  │               │                                         │  Holding work                                 │
│  │               │                                         │  ● parked · Fix the export feed · game · 2 h  │
│  │               │                                         │    Should the feed keep yesterday's rows?     │
│  │               │                                         │                                    Answer… ▸  │
│  │               │                                         │ Messages                            4 unread  │
│  │               │                                         │  Add the export feed · 3 unread               │
│  │               │                                         │    db: The schema moved; the feed needs …     │
│  │               │                                         │ Done and failed                     1 unread  │
│  │               │                                         │  • game — Fix the menu failed · 12 min        │
├──┴───────────────┴─────────────────────────────────────────┴─────────────────────────────────────────────────┤
│ status bar                                                                                                   │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 What it holds, in four groups

The groups stand in this order, one column, each headed by its name and its count. A group holding nothing is not drawn; with
nothing in any, the view says *Nothing needs you* and the sessions working, as Overview's line does today (`NothingNeedsYou`),
and *Nothing unread*.

| Group | What it holds | Judged by | Counted in the bell | Leaves when |
|---|---|---|---|---|
| **Waiting for your confirmation** (`person.label`) | Each press waiting: a terminal's over the pipe, and one at the service's door; each as its whole card (§3) | `PipeConfirmations`, the service's confirmations; the row `NeedsAPerson` already judges | its number (the press's row, as today) | it is confirmed, refused, withdrawn or refused by its time; an unanswered end becomes a notice in *Done and failed* |
| **What needs you** (`work.attention.title`) | Today's rows in their three groups and order (`attentionInOrder`), with their acts and doors, but the `confirmation` row, which is its card above; and the hand-over ask (NOTIFY2g) | `NeedsAPerson`, unchanged | its number | what it waits on is settled; never dismissed |
| **Messages** | Per ask, its channel's messages past the person's mark from its sessions or a teammate's, never the person's own or Daoris's lines: the count, the newest message's first line and who wrote it; and each conversation that answered the person's words | .NET, from each channel's read and the watch's `Answered` (§2.5) | the dot | read on the channel, by *Open*, or by *Mark read* |
| **Done and failed** | What the watch judges news and nobody need answer: a session that ended without the person asking, by its end state; a press that ended unanswered; later, the look's news (NOTIFY2h). Newest first | the watch (`AttentionWatch`), the press's end | the dot | dismissed, or a week old |

**A park, a parked quest and a start waiting for accounts** are rows of *What needs you* already; their `SESSION_ATTENTION` stays
the announcement (§5) and adds no second item. **Grouping keeps the length down**: messages are one item per ask, not one per
message; *Ready for you* keeps its five and its door into Sessions; a group's head says its count where its rows run long.

### 2.3 What an item does in place

- **A press's card**: *Refuse* first, then *Confirm*, as today; a refusal of the answer is said under the card (R3). *Details*
  opens the route's name and every value raw.
- **A row of *What needs you***: its acts as on Overview today (*Publish to …*, *Try again*, *Allow…*, *Trust this folder…*,
  *Accept the departure*, *Sign in*, *Go on with this answer*, a helper's yes and no), each asking once under itself where it asks
  today (`InlineConfirm`); its door (*Answer…*, *Review*, *Open*) opens its place. **It is never dismissed**: a wait nobody sees is
  refused at its bound or holds work that never moves (`person-waits.md`), so it leaves only when what it waits on is settled.
- **A message item**: *Open* goes to the ask's channel on its page, or the conversation, where the person replies with the
  channel's own composer; *Mark read*. Nothing is answered in the side bar: a reply needs its thread and its receivers.
- **A notice**: *Open* goes to the session's page (or the quest's); *Dismiss* removes it.
- **A group**: *Mark all read* on *Messages* and *Done and failed*. None on the presses or on *What needs you*: each is the person's
  own decision (D52, D156 §4.2), and there is no *Confirm all*.

### 2.4 Read and unread, in one window and in two

- **What needs you and the presses are never read or unread.** They are open until settled, and always counted. Seeing one is not
  answering it.
- **A notice is read once it is shown**, since its one line is all it says: the center open in a window that is visible and
  focused (CONFIRMSEEN1's `looking`), and the notice in view. *Open* and *Mark all read* read it too.
- **A channel's messages are read where they are read**: on the ask's page or the session's channel panel, which move the person's
  mark as today (`CHANNEL_MARK`, D167: a mark is a moment), or by *Mark read* on the item, which moves the same mark through the
  newest message the item stands for. Being listed in the center is not reading: the item shows one line of several messages.
- **Kept by the person's machine, in .NET, once.** The channels' marks stay the service's (D167). The notices and whether each is
  read are the driver's record `data/notices.json` (§2.5). One person, one machine, one read state: a notice read in one window is
  read in every window that shows it, by the slice's push.
- **Each main window shows its own workspace** (MAINWINDOWS3): an item judged from a service record carries its workspace, as
  *What needs you*'s rows do (`useAttention`, `bridge/attention.ts:91`), and a window shows its scope's and every machine-wide item (a
  press, an account). Its counts are its scope's, **and so is its taskbar**: each main window wears its own count on its own button
  and title (NOTIFY2c), where today one count reaches the taskbar.
- **What a window keeps of its own** is view state: which group is folded and where it is scrolled, in that window's session
  storage, as its scope is.

### 2.5 What is judged where (D182)

- **What needs you**: `NeedsAPerson` and `AttentionStateSlice`, unchanged. The press's row gains the values its sentence names
  (§3.2), pushed as data.
- **A new pushed slice, `notices`**, its owner in the modules (`NoticesStateSlice`), in `pushed-slices.md`'s shape: partition `news`,
  keyed by notice, each `{ id, kind, at, read, session?, quest?, repository?, state?, route?, workspace? }`, the workspace read from
  the sessions' snapshot at judgement and never stored; partition `messages`, keyed by ask, `{ ask, workspace, unread, newest: { id,
  at, who, line, urgent, toEveryone }, announce }`, judged from each live ask's channel (and `ASKS_ELSEWHERE`'s) through the
  service's read, woken by the asks' pushes, the channel presses and the look. Which message is worth a toast (`announce`, §5) is
  judged here, not on the page.
- **The record `data/notices.json`**, machine-local and never on a wire to a shared host: `{ "schemaVersion": 1, "notices": [ … ] }`,
  each notice as the partition holds it but its workspace; the newest 200 within seven days, pruned at each write; written by the
  driver library's `Notices` through `JsonKeeping.Save` under its keep lock (KEEPALL1), by the one process that holds the home's
  lease (the desktop, or the headless host alone, D171); a file that does not read is kept aside and a new one begun, never written
  over. The watch records every notice it judges whether the notify switch is on or off, as it already observes either way
  (`DriverLoop.cs:781-783`): the switch is about being told.
- **The presses**: `NOTICES_READ { ids }` and `NOTICE_DISMISS { id | all }`, presses declaring a new `notices` scope; `Person` in the
  pipe's census, since a terminal that marks the person's news read hides it from them; `Exempt` in Ask Daoris's coverage, the
  person's own reading. *Mark read* on a message is `CHANNEL_MARK`, as built.
- **The page** orders the groups, words every item in the reader's language, draws them, and keeps which window says a toast (the
  one focused) and its view state.

## 3. A terminal's press

### 3.1 Never a bar

The band under the strip goes for every press: the card is drawn in *Waiting for your confirmation* and nowhere else. The adoption
notice stays the one line it is under the strip (it is about the window's own host, said once until dismissed), as the update's
line does. The `confirmation` row of *What needs you* is drawn as its card in the presses' group, so a press is in one place.

### 3.2 Its words: plain, naming the session and the words, never the route

The sentence is composed from Daoris's words for the route's act and the request's own values, never from a sentence the asker
wrote (the person-door design §4.2). What changes is which values it names and how.

- **A wording table beside the census.** For each route the census classes `Person`, a row in the modules (`PipeWording`, beside
  `PipeCensus`) says which values the sentence names and by what role: `session` (a session's id, which the page names as Sessions
  names it), `sessions`, `words` (quoted as written), `level` (D188's level, in the composer's words), `files` (by file name),
  `quest`, `ask`, `repository`, `agent`; and which values change nothing the person decides (`quiet`), each with its reason. The
  confirmation's telling and its row carry the named values by role, a field added beside `fields`, never in place of it.
- **The card**: the act's sentence with its named values, then *It also carries* with every value no role names and no `quiet`
  covers, never hidden; who sent it and when it was asked; once shown, its time left (R2: *refused by itself in 1 min 40 s*); D156's
  *Daoris cannot tell whether you ran this or an agent did. Confirm only what you ran.*; *Refuse* and *Confirm*; and *Details*,
  which opens the route's name and every value by its pointer, raw, as the card shows them today.
- **Worked example, `sessions say`.** `SESSION_INPUT` carries `id`, `text`, `level`, `said` and `paths` (`ConversationRoutes.cs:
  94-110`). Its row: `id` is the session, `text` the words, `level` the level, `paths` the files, `said` quiet (it asks for the verb's
  own words in the answer). The card reads *A terminal asks to say to Fix the export feed, as your words, at its next step:* and the
  words, quoted, in the content's face; 中文 *一个终端请求以你的名义，在下一步时对 Fix the export feed 说：* and the words as written. A
  session the page cannot name is said by its id with *Daoris has no session by this id*.
- **Every `Person` route has its act's words.** Today seventeen are worded (`Confirmations.tsx:43-47`) and any other is named by its
  route; the census test fails on a `Person` route with no row and no `person.route.<ROUTE>` in both catalogues, so no route's
  constant is ever the sentence. A service confirmation's act is the service's own words today (`person.ts:86-94`); it keeps them
  until that door carries a code (§10).
- **Why it stays safe.** The person still sees every value beside *Confirm*: the named ones in the sentence, the rest under it, and
  all of them raw under *Details*. Only a value the table says changes nothing the person decides leaves the sentence's view, and
  never *Details*. The words a press carries are its own field, quoted and set apart from Daoris's sentence, so an agent's words
  cannot read as Daoris's.

### 3.3 Announced without taking the person's focus

| Where the person is | What says it | Why |
|---|---|---|
| At a window of Daoris, focused | The bell's count, and a toast in that window alone: *A terminal asks to say words to Fix the export feed*, with *Open*, which opens the center at the card | A toast is announced to a reader and takes no focus; its door finds the card; one window speaks so two windows do not say it twice |
| Daoris visible, another program focused, or minimized | A system notification, in the window's language, naming the act and the terminal and no value: *A terminal waits for your confirmation. It asks to say words to a session as yours. Open Daoris to confirm or refuse it.* Its click brings the window last in front forward with the center open at the card. **And the taskbar button of that window flashes until a window of Daoris comes forward** (`FlashWindowEx`, `FLASHW_TRAY \| FLASHW_TIMERNOFG`), stopped when the press ends | The notification passes in seconds into the system's own list; the flash stays lit for the ten minutes a press waits to be found, which is the failure of 2026-10-21. A value stays out of a notification the system keeps in its own history (CONFIRMSEEN1: never a value) |
| Never | Opening the center by itself, moving the focus, changing the side bar's view, a modal, a sound | Each takes what the person is doing; the center opens only on their press |

**The notification speaks the window's language.** The shell's notifier has no catalogue, so the main window's page hands it the
notices' words at load and at each change of language (`SET_NOTICE_WORDS`, the way `SET_BADGE` takes its description): a headline
and a body per kind, and the acts of the `Person` routes. The notifier words each notification from them with the event's facts;
what a session wrote (a park's question) is shown as written; a shell handed no words says today's English. This holds for every
kind it shows (a park, an ending, an answer, a press), not the press's alone.

**A press is announced whatever the notify switch says.** Its terminal waits on the person, so it is an answer they owe, not news;
the switch's words say so (NOTIFY2d). Every other announcement keeps the switch.

### 3.4 Its time

- **Its two minutes start when the card itself is first in view**: the center open, the card at least half in view, in a window
  visible and focused. Not the toast, the system notification, the bell or Overview's summary, none of which shows its values
  beside *Confirm*. The window says it by `PERSON_CONFIRM_SHOWN`, as built; the first word starts the two minutes and a later one
  moves nothing (CONFIRMSEEN1).
- **Two minutes still fit.** They are the time to read one card and decide, with the card in front of the person; what the incident
  lacked was being found, which the ten minutes to be found, the toast, the notification and the flash answer. The card says its time
  left, so the bound is never a surprise (R2). A bound that paused while the card was out of view was weighed and rejected (§7): it
  would make the terminal's wait unpredictable for a gain the ten minutes already give.
- **Ten minutes to be found stay** (`PipeConfirmations.FindWithin`), well inside the terminal's thirty. Past them a person is less
  likely to recognise a press as their own, which is what *Confirm only what you ran* asks of them; the card says when it was asked.
- **An end with no answer is said**, not only at the terminal: a notice in *Done and failed* (*A terminal's press to say words to
  Fix the export feed was refused by itself at 14:05*), so a press that ran out is never lost silently.

### 3.5 What does not change

Only the person confirms, in the window: never from the toast, the system notification, Overview's summary, a row, the terminal or
Ask Daoris; nothing is focused or pressed for them; *Refuse* comes first; five may wait; one use (D156 point 4, the person-door
design §4.2). A card is not an approval surface for agents (D52). There is no *Confirm all*.

## 4. Overview

**What stays.** Overview stays the landing that answers *is anything sitting* (D40). It leads with a short summary of what needs
the person: a line per group with its count and its oldest wait, and a line each for unread messages and notices, every line a door
that opens the center at its group. Then the tiles, *Outstanding* and the repositories by index size, as today. With nothing waiting,
the summary is today's one line, *Nothing needs you* and the sessions working.

```
Overview
┌──────────────────────────────────────────────────────────────────────────────┐
│ What needs you                                                               │
│ Waiting for your confirmation   1     refused by itself in 1 min 40 s     ▸  │
│ Holding work                    3     the oldest 2 h 10 min               ▸  │
│ Waiting for your word           2     the oldest 1 d                      ▸  │
│ Ready for you                   1                                         ▸  │
│ Messages                        4 unread                                  ▸  │
│ Done and failed                 1 unread                                  ▸  │
└──────────────────────────────────────────────────────────────────────────────┘
[ Adopted 7 ] [ Open 3 ] [ In progress 2 ] [ Knowledge 9.8k ]
Outstanding, oldest first …                 Repositories by index size …
```

The lines share one label track, each value beside its words (R1); the summary counts by group and never restates the bell's total.

**What leaves.** Every row of *What needs you*, with its question, its acts and its door, moves to the center; nothing is listed one
by one on Overview. Overview's activity badge moves to the bell: a count lives in one place (the platform language §2), and the
place that lists the rows is the center now. The taskbar keeps the same count.

**How everything stays findable.** Every row that left is in the center with the same words, the same acts and the same order. The
summary's lines are doors into it. The bell is on every view, and View → *Notifications* and the palette reach it. Ask Daoris's room
is told the view's name, as each view's is (`placements.ts:11-19`, *The window*). The terminal reads the same (NOTIFY2i). Sessions'
own *Waiting on you* group and its badge's dot stay where they are, and each wait stays in its session's head and foot.

## 5. Messages, and what is announced how

**D188's levels, mapped onto the person.** For a working agent, *ordinary* waits for a natural point, *urgent* reaches its next step,
and *now* cuts its step, which the person alone may send. For the person, the natural point is opening the center, the next step is
the next moment they are at the window, and cutting what they do is the desktop: a system notification and a lit taskbar button. So
an agent's *ordinary* message is counted, its *urgent* one is said in the window, and nothing an agent sends ever reaches the
desktop, at any level. The desktop is kept for what waits on the person with a clock or holds work, and what the person waits on.

| What comes | Group | The bell | At a focused window | Behind |
|---|---|---|---|---|
| A terminal's press | Waiting for your confirmation | number | toast with *Open* | notification in the window's language, no value; the taskbar lit until a window comes forward |
| A park, a parked quest, a start waiting for accounts | What needs you | number | toast with *Open*, as today with a door | notification as today, in the window's language |
| A held tool call, which its bound refuses by itself | What needs you | number | toast with *Open* (new) | notification (new) |
| Every other row (go-ahead, helper, answer alone, proposal, intake, departure, set-up, opinion, rule, unit of work, unanswerable, review, trust, signed-out account, hand-over ask) | What needs you | number | nothing new | nothing |
| A conversation answered the person's words | Messages | dot | toast with *Open*, but over that conversation on screen (NOTIFY1c) | notification, as today (NOTIFY1a) |
| An agent's or a teammate's message, urgent, to everyone | Messages | dot | toast with *Open*, once per message | nothing |
| An agent's message, ordinary, or one to a single repository | Messages | dot | nothing | nothing |
| A session ended without the person asking | Done and failed | dot | toast with *Open*, as today | notification, as today |
| A press that ended unanswered | Done and failed | dot | toast | nothing: its terminal says it too |
| The look's lines | Done and failed where they are news (NOTIFY2h); else the log | dot where news | nothing | nothing |
| The person's own act: its outcome or its refusal | none | — | where it was pressed (R3), or a toast where that place has gone | — |

- **A message to one repository is that session's**, though the person may read it: it counts on its ask, and never announces.
- **A message from before D188's levels** reads as ordinary.
- **Each toast is said once, in the window the person is looking at**: the focused window speaks, another only counts, so two
  windows never say one thing twice; a secondary window keeps its own toasts as today, with no door.
- **The notify switch** (Settings → Driver, `daoris driver notify`) governs every announcement in this table but a press's; it never
  governs what the center keeps.
- **The look's lines stop being toasts.** A tick's report is a log (D62): what is news becomes a coded notice in the reader's
  language, a wait becomes its row (the hand-over ask), and the rest stays in the log (NOTIFY2h).

## 6. The rules it keeps

- **The person's door (D156).** §3.5. The center is the window's; every route it adds that changes the person's state is `Person`
  in the pipe's census, and nothing in it acts for the person.
- **D185's rules.** One column: the center lists its items one under another, nothing side by side, at the side bar's width and in
  the panel alike. The platform's own controls: `Button`, `Pill`, `Dot`, the activity bar's `CountBadge` for the bell, `InlineConfirm`
  for an act that asks once, `AttentionRow` for every row, the toast extended with a door; one new icon, the bell. R1: the summary's
  lines and the card's facts on one label track. R2: the press says its age and its time left, and nothing else in the center
  invents a bound. R3: a refused act is said under its row or card, never as a toast. R5: reached from the frame (the bell, View,
  the palette), a door from the summary, Ask Daoris's rows (an `Exempt` for the person's own reading, §2.5), and its terminal twin.
  R6: stories at the side bar's floor (300 px) and at 45% of the install's window, in English and 中文, light and dark. R7: the view
  lays out by its own width; the bell is placed against the window, in the strip.
- **Both languages.** Chrome translates; what a session or an agent wrote, and the words a press carries, are shown as written
  (`translation-parity.md`). The new words are below; their budgets are `names:check -- --strict`'s, built by the row that shows them.

| Key | Kind | en | zh |
|---|---|---|---|
| `notifications.title` | title, tab | Notifications | 通知 |
| `notifications.open` | button | Notifications | 通知 |
| `notifications.tip` | sentence | {{count}} need you, {{unread}} unread | {{count}} 个等你处理，{{unread}} 条未读 |
| `notifications.group.messages` | section | Messages | 消息 |
| `notifications.group.news` | section | Done and failed | 完成与失败 |
| `notifications.unread` | status | {{count}} unread | {{count}} 条未读 |
| `notifications.markRead` / `markAllRead` | button | Mark read / Mark all read | 标为已读 / 全部标为已读 |
| `notifications.nothingUnread` | sentence | Nothing unread. | 没有未读内容。 |
| `notifications.toast.open` | button | Open | 打开 |
| `person.say` | sentence | A terminal asks to say to {{session}}, as your words, {{level}}: | 一个终端请求以你的名义，{{level}}对 {{session}} 说： |
| `person.also` | label | It also carries | 它还携带 |
| `person.asked` | status | asked {{ago}} | {{ago}}发出 |
| `person.left` | status | refused by itself in {{time}} | {{time}}后自动拒绝 |
| `person.details` | button | Details | 详情 |
| `notifications.system.confirming.title` | sentence | A terminal waits for your confirmation | 一个终端在等你确认 |
| `notifications.system.confirming.body` | sentence | It asks to {{act}}. Open Daoris to confirm or refuse it. | 它请求{{act}}。打开 Daoris 确认或拒绝。 |
| `notifications.system.parked.title` | sentence | {{repository}}: a session waits on you | {{repository}}：一个会话等你处理 |
| `notifications.news.ended` | sentence | {{session}} {{state}} | {{session}}{{state}} |
| `notifications.news.expired` | sentence | A terminal's press to {{act}} was refused by itself at {{time}} | 一个终端请求{{act}}，已在 {{time}} 自动拒绝 |

  The existing `person.label` (*Waiting for your confirmation*, 等待你确认) and `work.attention.title` (*What needs you*, 等你处理)
  name their groups. **The glossary gains one term, *notifications*** (en *Notifications*, zh 通知): the side bar's view and the title
  bar's button holding everything that came to the person, the presses waiting, what needs them, messages, and what ended. Avoid en
  *inbox* (it holds messages alone) and *Notification Center*, zh 通知中心 and 消息中心: 通知中心 is Windows's and macOS's own name for
  the system's list, where Daoris's notifications land too, and the two must not be confused.
- **.NET judges, the page renders (D182).** §2.5: what needs the person, what is news, what is read, which message announces, and
  which values a press's sentence names are judged in .NET once for every window; the page words, orders and draws them.

## 7. Rejected

- **Keeping the band, smaller or folded.** A fold still lays the press over every page, and a band under the strip cannot be read
  beside the session it is about.
- **A popover under the bell** (VS Code's center). It lies over the page and closes at a press elsewhere, while a press's card wants
  to stay open as the person looks at the session.
- **A drawer.** A drawer is a form, with a scrim that dims the page (D41, the frame model §3d): the blocking the owner reported.
- **A tenth place on the activity bar.** A place is a decision with its own counts (the UX6 design §8, R5), and it takes the main
  area, so nothing could be answered beside the session.
- **Overview as the center.** The owner's direction keeps Overview a summary.
- **Opening the center by itself when a press arrives**, or moving the focus to it: it changes what the side bar holds unasked.
- **Confirming from the toast, the notification, a row, the terminal or Ask Daoris, and *Confirm all*** (D156 §4.2, D52).
- **Dismissing a row of *What needs you*.** A wait nobody sees is refused at its bound or holds work that never moves.
- **A message read by being listed.** The item shows one line of several messages.
- **One number mixing what needs the person and what is unread**, or two numbers on the bell. The number is what holds work, as the
  taskbar's is; unread is a dot, the way Sessions' badge marks what waits beside its count (SESSBADGE1).
- **An unread count on the taskbar** (a mail program's convention). The taskbar counts what holds work.
- **An agent's message as a system notification, at any level.** D188 gives *now* to the person alone.
- **The press's values in the notification.** The system keeps notifications in its own history, outside Daoris.
- **The page composing each notification as it arrives.** A window behind may be held back by the engine; the shell is told by the
  bus directly and words it from what the page handed it at load.
- **A two-minute bound that pauses while the card is out of view**, a longer bound, or ten minutes to be found raised toward the
  terminal's thirty (§3.4).
- **Notices kept in memory alone.** A staged update restarts Daoris often (D139), and what ended overnight would be gone.
- **Notices derived from the session records.** No record says whether the person ended a session, so every ending, the person's
  own stop among them, would read as news.
- **The look's lines in the center as they are.** They are English log lines, most a look's transient failure that the next look
  answers (D62).
- **通知中心 as the name** (§6).

## 8. What this amends, once accepted

- **D150's UX6c note and the UX6 design §6.1**: *What needs you* is the center's, and Overview leads with its summary; its groups,
  order, words and acts stand. **The platform language**: §2, the strip holds *Notifications* and Overview's activity badge goes to
  it; §4's *Toast*, a third use: an arrival worth interrupting for, said once in the focused window, with a door into the center;
  §5's *Overview*, the summary.
- **D185's R5**: "a door from *What needs you*" is a row in the center's *What needs you*; its meaning stands.
- **D156's CONFIRMSEEN1 note**: the card is drawn in the center, never under the strip; *shown* is the card in view there; the press's
  notification is worded by the page's words and names no route; the taskbar is flashed for a press. Point 4 and the person-door
  design §4.2's "on the app strip and in What needs you" hold: the strip's *Notifications* and the center.
- **D126's NOTIFY1b note and D171's MAINWINDOWS2b note**: each main window tells and wears its own count.
- **SURF5b and NOTIFY1a, and D24's verbatim rule for these headlines**: the notification's headline is worded by kind in the window's
  language from the event's facts; what a session wrote stays as written.
- **The look's report (D62, `ShellSignals`)**: its lines are no longer toasts.
- **D195's inventory and D172's layout §4**: `data/notices.json`, the driver's, kept seven days and at most 200 notices.
- **Settings → Driver's notify switch**: its words say it governs being told, not what the center keeps, and that a press is always
  announced.
- **The frame model and the dock design's view table**: a view, *Notifications*, on the right by default.

## 9. Build rows

Each is a feature a person can see working (D187), end to end across its lanes. They are named NOTIFY2, not NOTIFY1a and on: NOTIFY1a
to NOTIFY1d already name rows archived on 2026-10-15 and 2026-10-16 (the answered kind, the taskbar's count, no toast over the
conversation on screen, the balloon's choice tested), as D195 named its KEEP1 for the same reason. Each runs its own tests and the
quick checks; the merge and the CI beside run the rest (D180). **Their order**: the owner's report first (2a, the press off the page;
2b, Overview short); then the counts per window (2c) and the announcement behind the window (2d); then what the center adds (2e, the
notices; 2f, the messages); then what arrives without a row today (2g, 2h); the terminal last (2i).

- **NOTIFY2a — the title bar's Notifications open a side bar view where a terminal's press waits in plain words, and nothing is drawn
  across the page** (web-shell, modules). *What the person sees:* a bell on the title bar counting what needs them; pressed, the side
  bar shows *Notifications*, the presses first, each worded *A terminal asks to say to Fix the export feed, as your words, at its next
  step:* with the words quoted, its time left and *Refuse* and *Confirm*, then *What needs you* as Overview lists it; no band under the
  title bar for a press; at the window, a toast says a press came, with *Open*. §2.1, §2.2's first two groups, §2.3, §3.1-§3.5 but the
  notification behind (2d): the view `notifications` (`placements.ts`, the views menu, `help/where.ts`, Ask Daoris's room and its test),
  the bell with the count of *What needs you*, View → *Notifications* and the palette; the card out of `App.tsx:1104-1114`'s band, the
  adoption notice left there; `PipeWording` in the modules with its census test, the named values on the telling and the row; the
  card's sentence, *It also carries*, *Details*, its age and time left; *shown* when the card is in view; `ToastItem` gaining a door,
  said in the focused window alone; *What needs you* drawn in the view, Overview keeping its own until 2b. *Proof:* `Confirmations`
  tests in en and zh, seen failing first: no band for a press; `SESSION_INPUT`'s sentence naming the session as Sessions names it and
  quoting the words, the route only under *Details*, a value no role names listed; *shown* said once, with the card in view (an
  `IntersectionObserver` stand-in) in a focused window, and never by the toast or the bell; the toast in the focused window alone, its
  *Open* opening the view at the card; the census test failing on a `Person` route with no row and no act's words; the view's stories
  at 300 px and 45%, en and 中文, light and dark; `i18n:check`, `names:check -- --strict`; the window with a press from a terminal
  (the parent's).
- **NOTIFY2b — Overview keeps a summary, and what needs you is answered in Notifications** (web-shell). *What the person sees:* Overview
  opens with a few lines (each group's count and oldest wait, unread messages and notices), each opening Notifications at its group;
  no session's question is listed on Overview; the count moves from Overview's icon to the bell. §4: the summary from `useAttention`
  (counts by `ATTENTION_GROUP`, the oldest `since`) on R1's track; the band and Overview's activity badge go (`App.tsx:1132-1139`);
  the platform language §2 and §5 and the UX6 design §6.1 noted; the glossary's `overview` meaning; the accepted budgets that name
  "Overview's main area" judged again at the side bar's width; the end-to-end spec that reads *waiting on you* (`person-waits.md`).
  *Proof:* `OverviewView` tests in en and zh (no row drawn, each group's count and oldest, each door opening the view at its group,
  *Nothing needs you* with the working count, a browser's line); `App` tests (no badge on Overview, the bell's count the rows'); the
  stories at wide, half and narrow; `names:check -- --strict`.
- **NOTIFY2c — every main window counts what needs you in its own workspace** (modules, web-shell). *What the person sees:* with two
  windows open, one per workspace, each taskbar button and title wears its own workspace's count, and a notification's click opens the
  window whose workspace holds what it is about. §2.4: `useTaskbarCount` told by every main window with its name, `SET_BADGE` taking it
  as `SET_TITLE` does, each window wearing its own (`WaitingCount`, `SecondaryWindowHost`); which window a notification opens judged in
  the modules (as `SessionBalloon` judges its words). *Proof:* `WaitingBadgeTests` and the windows module's test with two names; the
  page's test that each main window tells its own and a secondary one none; the taskbar with two windows (the parent's).
- **NOTIFY2d — behind the window, a press is announced in the person's language and its taskbar button stays lit until they come**
  (modules, web-shell, web-settings). *What the person sees:* with Daoris behind another program, a press raises a system notification
  in 中文 or English as the window speaks, naming its act and no value, and the Daoris button flashes until a window comes forward;
  parks, endings and answers speak the window's language too; a held tool call is announced as a park is; Settings → Driver's switch
  says what it governs. §3.3 and §5: `SET_NOTICE_WORDS`; `SessionBalloon` wording by kind from the handed words, today's English where
  none; the flash for a press on the window last in front, stopped at its end; the held call's announcement where the attention slice
  first sees its row; the switch's words. *Proof:* `SessionBalloonTests` per kind with words handed in en and zh and with none; a
  modules test of the flash's judgement (which window, which kinds, stopped at the end) and of a held call announced once; the
  switch's words in both languages; a press with the window behind another program (the parent's).
- **NOTIFY2e — what ended, failed or went unanswered is kept in Notifications, read alike in every window** (driver, modules,
  web-shell). *What the person sees:* *Done and failed* lists each session that ended without them asking and each press refused by
  its time, newest first, the unread marked and a dot on the bell; shown in one window, they are read in every window; *Dismiss* and
  *Mark all read*; a week later they are gone, the sessions' records keeping what happened. §2.2's fourth group, §2.4, §2.5: the
  driver library's `Notices` and `data/notices.json`; the watch recording every notice whatever the switch says, the headless host
  too; the press's unanswered end recorded by `PipeConfirmations`; the `notices` slice's `news`; `NOTICES_READ` and `NOTICE_DISMISS`
  with their census, publishing and coverage rows; D195's inventory row and D172's layout line. *Proof:* driver tests of `Notices`
  seen failing first (a notice per kind, recorded with the switch off, pruned by age and by count, a field a later build wrote kept, a
  file that does not read kept aside and a new one begun); the slice's tests (allow-listed fields, a read moving one notice, two
  windows' scopes); the routes' tests; the page's tests in en and zh; the stories.
- **NOTIFY2f — an agent's message reaches Notifications by its urgency** (modules, web-shell). *What the person sees:* *Messages* lists
  each ask with unread messages from its sessions or a teammate (the count, the newest message's first line and who wrote it) and
  each conversation that answered them; an urgent message to everyone is toasted once in the window they look at, with *Open*; an
  ordinary one, or one to a single repository, only counts; nothing an agent says reaches the desktop. §2.2's third group, §5: the
  slice's `messages` partition and its `announce`; the `Answered` notice; *Open* and *Mark read* through `CHANNEL_MARK`. *Proof:* the
  partition's tests (the person's own and Daoris's lines not counted, a message to one repository counted and never announced, an
  urgent one to everyone announced once, a teammate's counted, one from before the levels ordinary); the page's tests in en and zh
  (the toast once, in the focused window alone; *Mark read* moving the mark; reading on the ask's page clearing the item).
- **NOTIFY2g — an agent's ask to hand its session over waits in What needs you** (modules, web-shell). *What the person sees:* the
  ask is a row with *Hand over to {agent}* and *Not now*, counted by the bell and the taskbar, where today it is drawn only on its
  session's head. Every place `person-waits.md` lists: the row judged in `NeedsAPerson` from the asks the look holds, its group by
  whether its session stands still on it, `KIND_ORDER`, its words and acts, its hook's coverage row, its words in the glossary's kinds.
  *Proof:* `AttentionStateSliceTests`' cases; the attention, band and badge tests in en and zh; the coverage row (the parent's at the
  merge where the lane does not reach it).
- **NOTIFY2h — the look's lines stop interrupting: what is news is a notice, the rest is the log** (driver, modules, web-shell). *What
  the person sees:* no English log line toasts in a 中文 window; a hand-over taken or dropped, a brainstorm stopped and the like appear
  in *Done and failed* in the window's language; a look that could not read something and tries at the next is in Settings → Logs and
  the console only. §5's last rule: each line the look adds (`Driver.cs`, `DriverHandOvers.cs`, `DriverBrainstorms.cs` and the rest)
  classed as a coded notice through 2e's `Notices`, as a wait whose row says it (2g), or as the log's alone; `ShellSignals` toasting
  none of them; the headless console as it is. *Proof:* a driver census test over every line the look adds (an unclassed one fails);
  the notices' words in en and zh; `ShellSignals.test.tsx` (a tick's lines toast nothing).
- **NOTIFY2i — the terminal reads what came to the person** (driver, modules). *What the person sees:* `daoris-driver notices` prints
  what waits for their confirmation (by act and session), what needs them by group, unread messages by ask and *Done and failed*, with
  `--json`; through the running Daoris, an `Open` read in the census; alone, the notices' record, saying that what needs them is the
  running Daoris's judgement. *Proof:* the verb's text and JSON tests through the instance and alone, its census row, its usage line.

## 10. What this document's gate does not cover

Documents only; nothing is built. The statements about today were read from the code at `7c2bce824`, and none was run; the window
was not looked at, and the band's height for five presses is counted from its lines, not measured. Not shown by any test: that a
row of *What needs you* with its acts (an account's sign-in steps under it above all) reads at the side bar's 300 px; that the kit's
engine runs `IntersectionObserver` in a window behind others as Chromium documents; that `FlashWindowEx` keeps a frameless window's
button lit until it comes forward (Windows' documentation); how long Windows shows a notification before its own list takes it; and
what reading every live ask's channel at each wake costs on the install. NOTIFY2a, 2d and 2f prove them or say what they found. A
service confirmation's act stays the service's English until that door carries a code, which no row here builds. The seven days, the
200 notices, *half in view* and the two minutes kept are judgements, to be read again on the install after a week of NOTIFY2e.

## 11. The reads (D191)

None yet. Codex reads this round, every section, asked hardest about §3.2 (whether the wording table can hide anything the person
must see before *Confirm*), §3.4 (the moment *shown* is said, and the two minutes kept), §2.4 and §2.5 (one read state for two
windows, the record's one writer under the lease, the slice's cost), and §9's order. Whether Fable reads too is the steward's to say
under D191's amendment (the standing above). The steward checks every finding against the code before taking it and keeps the
verdicts here.

## 12. For the owner

One choice is the owner's, as the names of things have been (the glossary's *ask*): **what the center is called in Chinese.** The
design proposes 通知 (§6). The alternative is the 通知中心 the row used; the author avoids it because Windows and macOS give that name to
the system's own list, into which Daoris's notifications also go. Either answer changes only the glossary's term and the words of the
view and the button; NOTIFY2a builds whichever is chosen.
