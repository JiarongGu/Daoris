# Notifications: everything that comes to the person in one place the title bar opens, a terminal's press waiting there in plain words and never across the page, and Overview kept to a summary (accepted, 2026-10-21)

**Standing:** accepted under D196 (NOTIFY1), settled under D191 on 2026-10-21 after three rounds, each finding checked against the
code by the steward (§11). Codex read the first round (twenty-eight findings, eighteen of them blocking, all taken in round 2).
Since the design touches foundations in D191's amendment's classes (the moment a press counts as shown and the bound it keeps,
§3.4, the person's door, D156's CONFIRMSEEN1 note; a durable record written by two processes, §2.5; a mark the service keeps for a
teammate's ask, §2.5), Fable read §2 to §3 and §5 of round 2 (fourteen findings, one blocking, all taken in round 3). Nothing here
is built; its rows (§9) build it in four steps (§0.2): the presentation first, on the transports that exist and changing no store
(NOTIFY2a to 2e); then one serialized notice record (2f); then the channels' summaries on the service's own marks (2g); and
throughout, arrivals said by their identity apart from the snapshots that list them (§5). Nothing here publishes, spends an
account, signs in or removes the person's data; §12 names the one choice settled by the steward's default and open to the owner.

Read for it: the row NOTIFY1 in `TASKS.md`; CONFIRMSEEN1's archive entry and D156's note on it (lines 237-260); D156 points 4 and
the person-door design §4.2; the terminal RPC design §4.2 (lines 166-176); D182 points 1-6 and `.claude/knowledge/pushed-slices.md`;
D188 and the message-urgency design §1, §2, §8; D185 and the UI/UX design §2 (R1-R7); the platform language §2, §4 (*Toast*) and §5
(*Overview*); the UX6 design §6 and D150's UX6c note; the frame model §1 and §3c; the dock design's view table; D126's NOTIFY1b note;
D171's MAINWINDOWS3 notes; `.claude/knowledge/person-waits.md`, `bridge-presses.md` (rows 7, 9, 11), `translation-parity.md` and
`compatible-builds.md`; the glossary; and for round 2, Codex's read and D195's reads (the cleanup design §11) as the shape to follow.
Every statement about today was read from the code at `7c2bce824` for round 1 and again at `5e6843642` for round 2 (three of the
files it cites moved between, and their lines are cited as they stand now), and, where Fable's read touched it, at `3d3dd47de` for
round 3, and names its file and line; none was run, and the owner's install was not looked at.

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
| A Confirm is taken whenever the press still waits: only the timer's own end reads the clock, so a timer firing late lets a Confirm past its time run | `PipeConfirmations.cs:191-192`, `:271-283` |
| Each press is also a `confirmation` row of *What needs you* in *Holding work*, its one control opening the card | `NeedsAPerson.cs:333-349`, `attention.ts:48-50`, `:567-592` |
| A press at the service's own door waits two minutes from its ask, its grant used within them; the page polls it every five seconds while it holds the key, and no row counts it | `PersonConfirmations.cs:117-121`, `person.ts:74-95`, `AttentionStateSlice.cs:256-257` |
| While no window of Daoris is in front, a press raises an English balloon naming its route; the notify switch does not reach it | `PipeConfirmations.cs:300-311`, `SessionNotifier.cs:75-100` |
| *What needs you* is Overview's lead; only *Ready for you* is bounded, at five rows | `OverviewView.tsx:74-76`, `AttentionList.tsx:14-15`, `:53-55` |
| A park's row carries the agent's question as its line, clamped to two lines | `attention.ts:430-431`, `AttentionRow.tsx:406-416` |
| One count reaches the taskbar: the first main window's, from its own workspace's rows | `windows.ts:104-112`, `Program.cs:218-227`, `SecondaryWindowHost.cs:69-83` |
| What ended or answered is said by a seven-second toast and a balloon, and kept in no list | `ShellSignals.tsx:143-160`, `ui.tsx:1766`, `SessionNotifier.cs:86-100` |

### 0.1 What it rests on

**CONFIRMSEEN1 is the first slice, and most of it stands.** It made a press findable: a row of *What needs you* the badge and the
taskbar count, a balloon while no window is in front, ten minutes to be found, two minutes counted from its first showing, the log
saying `shown` (D156's CONFIRMSEEN1 note). This design keeps the ten minutes, the two minutes, the `shown` line and its route
(`PERSON_CONFIRM_SHOWN`), the count, and the balloon's occasion. It changes five things: the card is drawn in *Notifications* and
nowhere across the page (§3.1); *shown* is said when the card's decision region is in view there, not when any window draws the
band (§3.4); the card speaks plain words that keep what tells one session and one file from another, and keeps the route's name
under *Details* (§3.2); an answer after the bound is refused, as the card says it is (§3.4); and the press is announced by a toast
in the window in front and, behind it, by a lit taskbar button and a notification in the window's language, while the bell wears
the warning hue for as long as a press waits unshown (§3.3).

**What else it rests on.** *What needs you* is judged in .NET once for every window (`NeedsAPerson`, `AttentionStateSlice`, PUSH1d4)
and keeps every rule `person-waits.md` lists; this design moves where it is drawn, never what it judges. The confirmation channel
the page already reads (`bridge/person.ts`) carries a press's values, and stays their one carrier. D156: only the person confirms,
in the window, its default no. D188: urgency is the sender's statement of when a message should reach a working agent, and nobody
but the person sends *now*. D185: the rules the screens keep (R1-R7). D182: .NET judges, the page renders.

### 0.2 The shape, in four steps

Codex's smaller shape (§11, 6.1), taken whole:

1. **Presentation first, on the transports that exist** (NOTIFY2a to 2e). The confirmation cards of both sources and *What needs
   you*'s rows move into one movable view the bell opens; Overview becomes their summary; a press's values stay on the confirmation
   channel (`bridge/person.ts:55-72`, `:78-95`) and are never copied into a row. It changes no store. The pipe's half lands first on
   its own tests, with no view (2a): a press answered or shown past its time runs nothing, its event carries its id, its balloon
   names no route. The view (2b) then fixes the rest of what the owner hit on 2026-10-21: no bar across the page, no
   `SESSION_INPUT` as the sentence, a card that cannot run out before it could be seen, and a short Overview. Then a count per
   window (2c), the announcement behind the window in the person's language (2d), and the hand-over ask as a row (2e).
2. **Then one serialized notice record** and its projection (2f): what ended, failed or went unanswered, kept a week.
3. **Then the channels' summaries on the service's own marks** (2g), with the mark a teammate's ask needs added to the service.
4. **Throughout, arrivals are said by their identity**, apart from the snapshots that list and count them (§5): a snapshot read
   again says nothing twice, and no snapshot carries a field saying what to announce.

## 1. Everything that comes to the person today, and where it is drawn

*Blocks the page* means what the owner meant: it takes the page's room on every view, or covers what the person works on, until
it is answered. Nothing below lays a scrim or traps the focus; the drawers that do are forms the person opened (D41).

| What comes | Where it is drawn | Counted | Announced | Blocks the page |
|---|---|---|---|---|
| **A terminal's press waiting for Confirm** (`PERSON_CONFIRMATIONS`, `CONFIRMATIONS`; answered by `PERSON_CONFIRM`) | The band under the strip in every main window (`App.tsx:1104-1114`): per press its sentence, the route in code, its sender and end, a line per value, *Daoris cannot tell…*, *Refuse* and *Confirm* (`Confirmations.tsx:155-193`); and a `confirmation` row whose control opens the card (`attention.ts:567-592`) | as a row: Overview's badge, the first main window's taskbar | no toast (`ShellSignals.tsx:146-148`); a warning balloon behind, English, the route's name (`PipeConfirmations.cs:300-311`) | **Yes.** The whole width above every view, at least four lines and one more per value for each of up to five presses (the bound, `PipeConfirmations.cs:104`), nothing folds it, and it stays until answered or refused by its time |
| A confirmation at the service's own door (PERSONDOOR1b) | The same band, polled every five seconds while the page holds the key (`person.ts:74-95`); its act in the service's own words | **not counted**: no row (`AttentionStateSlice.cs:256-257` gathers the pipe's alone) | none in Daoris; its terminal says it waits for the window (`PersonConfirmations.cs:136`) | as above; two minutes from its ask, its grant used within them (`:120-121`) |
| The adoption notice (D156 §2.3) | One line in the same band, with *Dismiss* (`Confirmations.tsx:77-82`) | — | — | one line |
| The install's update (D139) | One line under the strip while something is staged (`App.tsx:1093-1102`) | — | — | one line |
| **What needs you** (PUSH1d4) | Overview's lead (`OverviewView.tsx:74-76`): three groups, each row with at most three controls and its door; *Holding work* and *Waiting for your word* unbounded (`AttentionList.tsx:53-55`) | Overview's activity badge, every row (`App.tsx:1132-1139`); the taskbar's overlay and title (`App.tsx:445-449`) | by kind, below | no; it pushes Overview's own lists down |
| Overview's own lists | The tiles, *Outstanding* (six, then a count), the repositories by index size (`OverviewView.tsx:78-243`) | Quests' badge | — | no |
| **A session's question** (a park, `awaiting-person`) | The session's head and foot (`waits.ts:151-153`); a `parked` row, its line the agent's question, two lines (`AttentionRow.tsx:406-416`) | as a row | `SESSION_ATTENTION` `Parked`: a toast in the error tone in every window (`ShellSignals.tsx:143-158`), a warning balloon behind (`SessionBalloon.cs:23-43`), its headline the driver's English (`Attention.cs:63-65`) | no |
| A go-ahead, a helper's ask, a held tool call, an answer the answers-alone bound holds | The session's head and foot (`waits.ts:157-175`), the ask's page for a go-ahead; each a row with its acts (`attention.ts:41-67`) | as rows | none: no `AttentionKind` raises them (`Attention.cs:10-37`). A held call is refused by itself at its bound (PERMUNTIL1) | no |
| **An agent's ask to hand its session over** (MIXAGENT1c) | The session's head and foot alone (`waits.ts:34`, `:180`; `WaitStrip.tsx:268-271`; `HandOverAgentAsk.tsx:47-57`) | **not counted**: no row (`NeedsAPerson.cs` has no such kind) | the look's English line, toasted (`DriverHandOvers.cs:181`) | no |
| **Session and ask channel messages** (D167, D188) | The ask's page (`AskPage.tsx:529`), where reading moves the person's mark; the session's channel panel (`AttendedSession.tsx:307`), which shows the part about its line of work and moves no mark (`ChannelSection.tsx:52-54`); an ask row's *N unread* (`AskRow.tsx:100`) and the rail's heading by ask (`SessionList.tsx:219-224`), each the service's count of every item past the mark, Daoris's lines among them (`Channel.cs:974-982`), read per ask (`asks.ts:44-62`) | in no badge | never, at any level | no |
| **Work done and failed, and an answer** | `SESSION_ATTENTION` `Ended` (a session ended without the person asking), `Answered` (a conversation answered the person's words), `QuestParked`, `Waiting` (`Attention.cs:10-37`); *Ready for you* rows for work to review | `QuestParked` and `Waiting` as rows; an ending and an answer **nowhere** | a toast in every window, except an answer to the conversation a visible window attends (`ShellSignals.tsx:149-153`); a balloon behind | no; once the toast closes nothing lists what ended, and Sessions shows the record's state |
| **The look's lines** (the tick's `events`) | The driver's English log lines, toasted when they change (`ShellSignals.tsx:53-63`) and printed by a headless host (`Driver.Host/Program.cs:887`), written to no machine log (§5.1) | — | as toasts | no |
| **Toasts** (`notify`) | One corner per window, 26rem, at most three (`signals.ts:316`), seven seconds, two lines and the rest in a tip, text only with no door (`ui.tsx:1730-1805`): a refusal whose place has gone (R3), an outcome, a query's error (`useErrorNotify`), and the three rows above | — | — | covers the corner while it shows |
| The strip's chips | The review chip while a set-up waits (`ReviewChip.tsx:33-48`), the browser's door | — | — | no |
| The activity and status bars | Sessions' count with its waiting dot (SESSBADGE1), Agents' signed-out accounts; the status bar's sessions working | — | — | no |
| **Outside the window** | One tray icon over the first main window, quiet while any window of Daoris is in front (`SessionNotifier.cs:43-163`); its headlines and default lines English, from .NET, and a park's question as the session wrote it; the notify switch silences the look's (`DriverLoop.cs:781-790`, `:919-932`) and not a press's; a headless host prints `!  {line}` (`Driver.Host/Program.cs:835`) | the taskbar's count, one window's (above) | — | no |

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
   by an English log line; a press at the service's door is counted nowhere either.
7. **Nothing an agent says on a channel is announced, or counted outside the ask.** It is read on the ask's page and its row, and
   in part on the session's panel, though D188 gave each message a level.

## 2. Notifications

### 2.1 Where it is: a view the title bar opens

- **The title bar's *Notifications*.** A button on the strip, in its trailing group before the browser's door: a bell with the
  count of what needs the person (each press of either source once, and every other row of *What needs you*) in open's hue, the
  badge Overview's icon wears today; and, once something can be unread (2f, 2g), a dot in the accent while anything is. **While any
  press waits unshown** (§3.4), the badge wears the warning hue instead, with the presses' own count (§3.3). Its tip and its name
  for a reader say what it shows: *Notifications: 3 need you, 5 unread*, or *2 waiting for your confirmation* first while a press
  waits unshown. On a narrow window the region toggles leave the strip first (UX7a); this button stays, since it is how anything
  is found.
- **Pressed, it shows the view where it stands**: the right side bar by default, or the panel where the person moved it
  (`placements.ts:23-29`: a view stands in either), opening that region on the view's tab. Pressed while the view is shown, it
  gives the region back to the view it held, or closes the region where it was closed. It is a view like the others (DOCK1b),
  moved by its tab's menu and kept where the person put it. View → *Notifications* and the palette reach it from the one command
  table (R5).
- **Why a view.** It holds what is the same on every view (the frame model §3c), so it stays open and keeps its place while the
  person moves between views; it never lies over the page; and it stands beside the main area or under it, so a press about the
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
│◈ │               │                                         │ ┃ feed (game · 3f2a91c), as your words, at its  │
│⌕ │               │                                         │ ┃ next step:                                    │
│  │               │                                         │ ┃ “Run the database tests before you land.”     │
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

The groups stand in this order, one column, each headed by its name and its count, each drawn once the row that builds it lands.
A group holding nothing is not drawn; with nothing in any, the view says *Nothing needs you* and the sessions working, as Overview's
line does today (`NothingNeedsYou`), and, once anything can be unread, *Nothing unread*.

| Group | What it holds | Read from | Counted in the bell | Leaves when | Row |
|---|---|---|---|---|---|
| **Waiting for your confirmation** (`person.label`) | Each press waiting, a terminal's over the pipe and one at the service's door, each as its whole card (§3) | the confirmation channel as built: `CONFIRMATIONS` and `PERSON_CONFIRMATIONS` for the pipe's, the page's poll for the service's (`person.ts:55-95`); never a row's copy | its number; while one waits unshown, the presses' count in the warning hue | confirmed, refused, withdrawn or refused by its time; an end with no answer becomes a notice in *Done and failed* once 2f keeps them | 2b |
| **What needs you** (`work.attention.title`) | Today's rows in their three groups and order (`attentionInOrder`), with their acts and doors, but the `confirmation` row, which is its card above; and the hand-over ask (2e) | `NeedsAPerson`, unchanged | its number | what it waits on is settled; never dismissed | 2b |
| **Messages** | Per ask with items past the person's mark, a teammate's ask among them: the service's own count (the number the ask's row says), the newest message's first line and who wrote it; and each conversation that answered the person's words | the `notices` slice: its `messages` partition over the service's channel reads, and its `news` partition for an answer (§2.5) | the dot | an ask's items read on its page or by *Mark read*; an answer by *Open*, *Mark read* or its conversation read in a window in front | 2g |
| **Done and failed** | What the person need not answer: a session that ended without them asking, by its end state; a press that ended unanswered; later, the look's news (2h). Newest first | the notice record (§2.5) | the dot | dismissed, or a week old | 2f |

**A park, a parked quest and a start waiting for accounts** are rows of *What needs you* already; their `SESSION_ATTENTION` stays
the announcement (§5) and adds no second item. **Grouping keeps the length down**: messages are one item per ask, not one per
message; *Ready for you* keeps its five and its door into Sessions; a group's head says its count where its rows run long.

### 2.3 What an item does in place

- **A press's card**: *Refuse* first, then *Confirm*, as today; a refusal of the answer is said under the card (R3), or by a toast
  where the card has gone with its press, as a Confirm past its time finds it (§3.4). *Details* opens the route's name and every
  value raw. The card answers its press by its source and id, never by its words (§3.5).
- **A row of *What needs you***: its acts as on Overview today (*Publish to …*, *Try again*, *Allow…*, *Trust this folder…*,
  *Accept the departure*, *Sign in*, *Go on with this answer*, a helper's yes and no), each asking once under itself where it asks
  today (`InlineConfirm`); its door (*Answer…*, *Review*, *Open*) opens its place. **It is never dismissed**: a wait nobody sees is
  refused at its bound or holds work that never moves (`person-waits.md`), so it leaves only when what it waits on is settled.
- **An ask's messages**: *Open* goes to the ask's channel on its page, where reading moves the person's mark, and the person
  replies with the channel's own composer; *Mark read* moves the same mark through the newest item the count stands for. Nothing is
  answered in the view: a reply needs its thread and its receivers.
- **A conversation's answer**: *Open* goes to the conversation; *Mark read*.
- **A notice**: *Open* goes to the session's page (or the quest's); *Dismiss* removes it.
- **A group**: *Mark all read* on *Messages* and *Done and failed*. None on the presses or on *What needs you*: each is the person's
  own decision (D52, D156 §4.2), and there is no *Confirm all*.

### 2.4 Read and unread, in one window and in two

- **What needs you and the presses are never read or unread.** They are open until settled, and always counted. Seeing one is not
  answering it.
- **A notice in *Done and failed* is read once it is shown**, since its one line is all it says: in view in the view by the same
  test as a press's region (§3.4), in a window that is visible and focused. *Open* and *Mark all read* read it too.
- **An ask's items are read where the whole channel is read**: on the ask's page, which moves the person's mark through the newest
  once (`ChannelSection.tsx:52-53`), or by *Mark read* on the item. **A session's channel panel moves no mark**, since it shows only
  the part about its line of work (`:54`), so it leaves the item as it was. Being listed is not reading either: the item shows one
  line of several.
- **One unread model, the service's.** An ask's count is its channel's `unread`: the number its row and the rail already say. The
  view adds the newest message from someone else beside it, and never counts a second way. Today that count is every item past
  the person's mark, the person's own posts and Daoris's lines among them (`Channel.cs:981`), which the view's newest already
  passes over. 2g changes the person's entry's count in the service, in the lane it already opens for a teammate's mark: past the
  person's mark, only what an agent or a teammate wrote, never the person's own posts nor Daoris's lines. The ask's row and the
  rail read the same count, so they say the same new number; a session's entry counts as it does today.
- **A conversation's answer has no channel and no mark.** It is a notice of the kind `answered` in the record (§2.5), read by *Open*,
  by *Mark read*, or when its conversation is the one attended in a window visible and focused at or after the answer's moment,
  which shows the answer on the page (the condition `ShellSignals.tsx:151-152` already reads to keep a toast off it).
- **Kept once per machine.** The channels' marks are the service's (D167), a teammate's ask's too once 2g lets the service keep the
  person's mark on it (today `Channel.cs:1061-1063` refuses an ask the local store does not hold). The notices and whether each is
  read are the record `data/notices.json` (§2.5). One person, one machine, one read state: a notice read in one window is read in
  every window that shows it, by the slice's push.
- **Each main window shows its own workspace** (MAINWINDOWS3): an item carries its workspace, a notice the one it was made in, kept
  on it and never read again from a session that may since be gone (`bridge/attention.ts:91` reads an item with none as every
  window's); a window shows its scope's and every machine-wide item (a press, an account, a press's end). Its counts are its scope's,
  **and so is its taskbar**: each main window wears its own count on its own button and title (2c).
- **What a window keeps of its own** is view state: which group is folded and where it is scrolled, in that window's session
  storage, as its scope is.

### 2.5 What is judged where (D182)

- **What needs you**: `NeedsAPerson` and `AttentionStateSlice`, unchanged. The `confirmation` row stays as built
  (`NeedsAPerson.cs:333-349`): its route, its asker and its times, never a value.
- **The presses**: the confirmation channel as built. The pipe's telling gains `named`, the values its sentence names by role
  (`PipeWording`, §3.2), judged in the modules and added beside `fields`; the service's keeps its own `act`. **The service's
  presses are the one count .NET does not judge**: the page reads them by its own poll (`person.ts:74-95`), so the bell's and the
  taskbar's counts of them are the page's until the service's door is pushed to the windows, which is NOTIFY2j's (§9).
- **A pushed slice, `notices`** (2f, 2g), its owner in the modules (`NoticesStateSlice`), in `pushed-slices.md`'s shape:
  - partition `news`, keyed by notice: `{ id, kind, at, read, workspace?, session?, quest?, repository?, state?, route?, act? }`,
    read from the record in memory;
  - partition `messages` (2g), keyed by ask: `{ ask, workspace, teammate, unread, newest: { id, at, who, line, level, toEveryone }
    }`, `unread` the service's own, judged from each live ask's channel and the asks elsewhere (`ASKS_ELSEWHERE`) through the
    service's read, woken by the asks' pushes and the channel's presses. The page's per-ask counts (`useChannelUnreads`,
    `asks.ts:44-62`) read it once live; a channel is still pulled where it is read whole;
  - no field says what to announce: arrivals are events (§5).
- **The record `data/notices.json`** (2f), machine-local and never on a wire to a shared host:
  - **Its shape**: `{ "schemaVersion": 1, "notices": { "<id>": { … } } }`, the notices an object keyed by id, each as the partition
    holds it, its workspace with it. An object, not an array: `JsonKeeping` merges an object key by key and keeps a key it did not
    write, but writes an array whole (`JsonKeeping.cs:30-32`), so a field a later build wrote inside a notice is kept only in a map.
  - **Its writers**: the driver library's `Notices`, in any process on the home, each write held under the file's keep lock from
    its read to its rename (`KeepLock.Take`, then `SaveHolding`, the one hold `tools.json`'s writers keep, `JsonKeeping.cs:76-80`),
    which serializes writers within a process and across processes. Not the lease holder alone: the desktop's page and
    conversations work before it holds the lease (`DriverLoop.cs:672-681`, `:728-733`), so the page's reads and dismissals and a
    conversation's answer (`:1025`) are written by the desktop's process whoever drives; a session's ending by the watch of the
    loop that drives (`:776-790`); a press's end by the pipe, which the loop holding the lease serves (`:735-737`).
  - **A writer pushes its own write.** The process that wrote pushes its `notices` slice itself once the write is renamed in,
    since a stamp the process marked as its own write wakes nothing on its home watch (`HomeWatch.cs:161-163`).
  - **A write refused is logged and dropped**: the keep lock not taken within its ten seconds (`KeepLock.cs:35`), or
    `SaveHolding` refusing a key written twice or a shape changed under the edit (`JsonKeeping.cs:109-121`). The notice is not
    kept and nothing retries it, since the sessions' records keep what happened and nothing waits on a notice; the machine log
    says the refusal.
  - **Its readers**: each process's slice reads the record at its start and again at each change a home watch hears, as Ask
    Daoris's proposals are heard by the window's process before the lease (`DriverLoop.cs:707-715`), so a notice the headless host
    writes reaches the desktop's windows. A change the watcher misses is read at the topic's deadline, thirty seconds after its
    last judgement whatever was heard (`HomeWatch.cs:172`). A read that fails (a file half seen, one that does not parse) keeps
    the last snapshot pushed and waits for the next wake; a reader never sets a record aside.
  - **Another process's notice is an arrival in the desktop by the desktop host's re-read.** With the headless loop driving, a
    notice it writes reaches the desktop by no event, so the desktop host's re-read of the record is that notice's arrival source:
    a notice whose id it has not seen is an arrival, the ids kept bounded, and nothing older than its first read of the record is
    said (§5).
  - **Its workspace**: stored with the notice when it is made, from the sessions' snapshot then; a notice with none is
    machine-wide.
  - **Its age and its count**: a notice older than seven days is never pushed, from the record's first read on, and is pruned at
    the next write; at most 200 are kept, the oldest going first whether read or not, since nothing in it waits on anyone and the
    sessions' records keep what happened. A restart reads the record whole and prunes nothing until it writes.
  - **A record it cannot take**: one that does not parse is set aside under a dated name and a new one begun, never written over,
    by the writer holding the file's keep lock alone. `Notices` has its own path for it, since `JsonKeeping.SaveHolding` refuses a
    file that does not read (`JsonKeeping.cs:101-107`): under the lock it reads the file, sets an unreadable one aside, and only
    then writes. One whose `schemaVersion` is newer than the build's is left as it is and read as none, and nothing is written
    over it.
  - **The notify switch governs being told, never recording**: the watch records every notice whether it is on or off, as it
    already observes either way (`DriverLoop.cs:781-783`), its `Observe` running before `ticked.Notify` is read (`:783-790`). A
    conversation's answer likewise: its `answered` notice (2g) is recorded in `AnsweredAsync` before the switch's gate
    (`:919-932`), so an answer heard with the switch off is still listed.
- **The presses**: `NOTICES_READ { ids }` and `NOTICE_DISMISS { id | all }`, presses declaring a new `notices` scope; `Person` in the
  pipe's census, since a terminal that marks the person's news read hides it from them; `Exempt` in Ask Daoris's coverage, the
  person's own reading. *Mark read* on an ask is `CHANNEL_MARK` with its moment, as built.
- **The page** orders the groups, words every item in the reader's language, draws them, and keeps which window says a toast (the
  one focused) and its view state.

## 3. A terminal's press

### 3.1 Never a bar

The band under the strip goes for every press, a terminal's and the service's: each card is drawn in *Waiting for your
confirmation* and nowhere else. The adoption notice stays the one line it is under the strip (it is about the window's own host,
said once until dismissed), as the update's line does. The `confirmation` row of *What needs you* is drawn as its card in the
presses' group, so a press is in one place.

### 3.2 Its words: plain, naming the session and the words, never the route

The sentence is composed from Daoris's words for the route's act and the request's own values, never from a sentence the asker
wrote (the person-door design §4.2). What changes is which values it names and how.

- **A wording table beside the census.** For each route the census classes `Person`, a row in the modules (`PipeWording`, beside
  `PipeCensus`) says which values the sentence names and by what role:
  - `session`, named as Sessions names it **and always with its repository and its short id beside**: a session's title can be a
    kind's generic name or another session's (a conversation's or a helper's kind, an intake's ask, a quest's `#id`,
    `identity.ts:56-67`; two sessions on one quest share its name); **and with its workspace too where a repository of that name
    is in more than one workspace**, which the attention rows already carry (`AttentionStateSlice.cs:72`); `sessions` likewise;
  - `words`, quoted as written; `level`, D188's level in the composer's words;
  - `files`, **each by its path as sent, whole**, never a file's name alone, since two files share a name;
  - `quest`, `ask`, `repository`, `agent`, each by its name and id;
  - `form`, a value that chooses how the act runs, which the sentence says in words.
  No value leaves the card's view: there is no `quiet` role, and every value no role names is listed under the sentence. The
  pipe's telling carries the named values by role, `named`, a field added beside `fields`, never in place of it.
- **The card**: the act's sentence with its named values; *It also carries* with every value no role names; who sent it and when it
  was asked; once shown, its time left (R2: *refused by itself in 1 min 40 s*); D156's *Daoris cannot tell whether you ran this or
  an agent did. Confirm only what you ran.*; *Refuse* and *Confirm*; and *Details*, which opens the route's name and every value by
  its pointer, raw, as the card shows them today. Its height is bounded (§3.4).
- **Worked example, `sessions say`.** `SESSION_INPUT` reads `id`, `text`, `level`, `said` and `paths` (`ConversationRoutes.cs:94-114`).
  Its row: `id` the session, `text` the words, `level` the level, `paths` the files, and `said` a `form`: with it, the verb's own
  path runs, which reads each path from this machine's disk and answers in the verb's words; without it, the words go to the
  conversation with the page's files attached whole (`:99-123`). A terminal's `sessions say` sends it. The card reads *A terminal
  asks to say to Fix the export feed (game · 3f2a91c), as your words, at its next step:* and the words, quoted, in the content's
  face, and where it carries paths, *and to attach these files, read from this machine:* with each path whole; 中文 *一个终端请求以
  你的名义，在下一步时对 Fix the export feed（game · 3f2a91c）说：*, the words as written, and *并附上这些文件，从本机读取：*. A
  session the page cannot name is said by its id with *Daoris has no session by this id*.
- **Every `Person` route has its act's words.** Today seventeen are worded (`Confirmations.tsx:43-47`) and any other is named by its
  route; the census test fails on a `Person` route with no row and no `person.route.<ROUTE>` in both catalogues, so no route's
  constant is ever the sentence. A service confirmation's act is the service's own words today (`person.ts:86-94`); it keeps them
  until that door carries a code (§10), its values listed as today, its region and its time as §3.4 says.
- **Why it stays safe.** The person sees every value beside *Confirm*: the named ones in the sentence with what tells them apart,
  the rest under it, all of them raw under *Details*; none leaves the card's view. The words a press carries are its own field,
  quoted and set apart from Daoris's sentence, so an agent's words cannot read as Daoris's. And the card answers its press by its
  id, never by its words: the pipe binds the press's route, payload and key when it is asked (`InstancePipe.cs:545-548`) and runs it
  only on the person's Confirm (`:635-638`); answering and showing stay the window's (`PipeCensus.cs:243-247`); the card is keyed by
  its source and id (`Confirmations.tsx:85`).

### 3.3 Announced without taking the person's focus

| Where the person is | What says it | Why |
|---|---|---|
| At a main window of Daoris, focused | The bell in the warning hue with the presses' count while any waits unshown, and a toast in that window alone: *A terminal asks to say words to Fix the export feed*, with *Open*, which shows the view at the card, staying until the view opens or the press ends | A toast is announced to a reader and takes no focus; its door finds the card; one window speaks so two windows do not say it twice; the bell's hue stays when a toast is missed |
| At a secondary window of Daoris (a detached session, the monitor), focused | A toast there: *A terminal's press waits in Daoris's main window*, with no door, since that window holds no view (`SecondaryWindowRoot.tsx:14-26`); the system says nothing while any window of Daoris is in front (`SessionNotifier.cs:146-157`), so the main window's button is lit, as below | The person is at Daoris, but not where the card is |
| Daoris visible, another program focused, or minimized | The balloon as today, naming no route from 2a and in the window's language from 2d, naming the act and the terminal and no value: *A terminal waits for your confirmation. It asks to say words to a session as yours. Open Daoris to confirm or refuse it.* Its click brings the main window last in front forward with the view open at the card. **And that window's taskbar button flashes while any press waits unshown** (`FlashWindowEx`, `FLASHW_TRAY \| FLASHW_TIMERNOFG`), stopped when a main window comes forward, or when the last press waiting unshown ends or is shown | The notification passes in seconds into the system's own list; the flash stays lit for the ten minutes a press waits to be found, which is the failure of 2026-10-21. A value stays out of a notification the system keeps in its own history (CONFIRMSEEN1: never a value) |
| Never | Opening the view by itself, moving the focus, changing what a region shows, a modal, a sound | Each takes what the person is doing; the view opens only on their press |

**While a press waits unshown, something says so and stays.** With the focused window's view closed, nothing above would last:
a toast closes after seven seconds (`ui.tsx:1766`) and the kit's pump may drop it, the bell's count looks like any other row's,
and the taskbar's flash is only for a window behind. That is how the incident of 2026-10-21 could recur, and a service
confirmation is exposed the same way. So: **while any press waits unshown, of either source, the bell wears the warning hue**,
D185's `--warn` token, the one the press's card already wears (`Confirmations.tsx:161`), **with the presses' own count**, not
what needs the person in all; it goes back to open's hue and the whole count once every press waiting is shown or has ended. A
service confirmation, which tells its door no word that it is shown, is unshown until the page's own *shown* of its region says
otherwise (§3.4). And **the press's toast stays until the view opens or the press ends**, never closed by its seven seconds.
Neither moves the focus, opens the view or covers more than the toast's corner.

**What says it, once.** The toast is said on the event the pipe already tells once, at the press's ask (`SESSION_ATTENTION` of the
kind `Confirming`, `PipeConfirmations.cs:160-161`, `:305-311`), which gains the press's id, a field added; never judged from
`CONFIRMATIONS`, a snapshot of every press told again at each change (§5). A window says it only while it is the one focused, and
keeps the ids it said, so an event delivered twice is said once. A service confirmation has no event, since the service tells no
window (`person.ts:74-77`): the focused window says it the first time its poll sees the id; a window reloaded while one waits may
say it once more, and it still waits on the person. Each window polls on its own clock (`person.ts:83`), so where the focus moves
from one main window to another between their polls, both may say the same service confirmation once each. That is accepted: it
is one press, counted once in each window's bell, and it still waits on the person; NOTIFY2j's push ends it.

**The notification speaks the window's language** (2d). The shell's notifier has no catalogue, so the main window's page hands it
the notices' words at load and at each change of language (`SET_NOTICE_WORDS`, the way `SET_BADGE` takes its description): a
headline and a body per kind, and the acts of the `Person` routes. The notifier words each notification from them with the event's
facts; what a session wrote (a park's question) is shown as written; a shell handed no words says today's English, with no route's
constant from 2a on. This holds for every kind it shows (a park, an ending, an answer, a press), not the press's alone.

**A press is announced whatever the notify switch says.** Its terminal waits on the person, so it is an answer they owe, not news;
the switch's words say so (2d). Every other announcement keeps the switch.

### 3.4 Its time

- **The decision region.** A card's region is its head through its buttons: the act's sentence with its named values, *It also
  carries*, when it was asked and its time left, *Daoris cannot tell…*, and *Refuse* and *Confirm*. Its height is bounded whatever
  the press carries: the quoted words and the values listed each stand in a box at most eight lines high, scrolled inside, so a
  press of 256 values (`PipeConfirmations.cs:89-90`, `PersonConfirmations.cs:129-130`) or of long words is one region of at most
  about 24rem. *Details* opens below the region.
- **Shown is the region in view.** The region is shown when its visible height is at least the smaller of its own height and its
  scroll area's, so a region lying whole in the area is shown and one taller than a short window's area is shown by filling it:
  the view's tab the one its region shows (the side bar's or the panel's), that region open, the card's group not folded, in a
  window visible and focused (CONFIRMSEEN1's `looking`). **Where it is shown by filling its area, the row of *Refuse* and
  *Confirm* must lie whole in view as well**: a short panel can be filled by the region's head with its buttons below the fold,
  and two minutes must not start on a card whose answer cannot be seen. The scroll is the person's; the region's scroll is not
  anchored at its foot, since that would change what a region shows unasked (§3.3). *Shown* is one judgement over all of these,
  judged again at each change of any: the region's intersection with its area, its group's fold, the region's tab, where the
  view is docked, the window's size and its focus. A drawer's scrim (D41) over the view counts as not shown. Not the toast, the
  system notification, the bell or Overview's summary, none of which shows its values beside *Confirm*. The window says it by
  `PERSON_CONFIRM_SHOWN`, as built; the first word of any window starts the two minutes and a later one moves nothing
  (`PipeConfirmations.cs:169-189`).
- **Two minutes still fit.** They are the time to read one card and decide, with the card in front of the person; what the incident
  lacked was being found, which the ten minutes to be found, the toast, the lit button and the notification answer. The card says
  its time left, so the bound is never a surprise (R2). A bound that paused while the card was out of view was weighed and rejected
  (§7): it would make the terminal's wait unpredictable for a gain the ten minutes already give.
- **Ten minutes to be found stay** (`PipeConfirmations.FindWithin`), well inside the terminal's thirty. Past them a person is less
  likely to recognise a press as their own, which is what *Confirm only what you ran* asks of them; the card says when it was asked.
- **No answer after its time.** Today a Confirm is taken while the press still waits, and only the timer's end reads the clock
  (`PipeConfirmations.cs:191-192`, `:271-283`), so a timer firing late lets a Confirm past its time run; and a word that the card is
  shown, past its ten minutes, gives it two more (`:169-189`). Decided: an answer, or a word that it is shown, at or after its time
  ends the press as `Expired`, answered `gone`, and nothing runs. The bound the card states is the bound kept. In `Show` the clock
  is read before its early return for a card already shown (`:177`), so a word past the two minutes of a card shown before ends it
  too. **The refusal is toasted**, as R3 allows where the place has gone: `End` removes the press and tells every window before it
  settles (`:284-292`), so the card is gone by the time its answer returns, and the card's own `setSaid` would run unmounted
  (`Confirmations.tsx:142-153`). The toast says what the card says today of a press that has gone (`person.gone`: it no longer
  waits, answered elsewhere, its time passed, or taken back by its terminal); no new words.
- **A service confirmation's time** is two minutes from its ask, counted by the service, within which its grant is also used
  (`PersonConfirmations.cs:120-121`, `:139-140`). It has no time to be found and no word that it is shown, and gets none here: a
  time to be found would change the person-door protocol and its client, which this design does not. Its card says its time left
  from the first moment (R2); it is counted in the bell and the taskbar, by the page until NOTIFY2j (§2.5), and toasted once in
  the focused window (§3.3); while it waits unshown the bell wears the warning hue, as for the pipe's (§3.3); its terminal
  says it waits for the window (`:136`). Behind the window nothing new says it, as today. The week on the install (§10) says
  whether one runs out unseen.
- **An end with no answer is said**, at the terminal as today, and once 2f keeps notices, as a notice in *Done and failed* (*A
  terminal's press to say words to Fix the export feed was refused by itself at 14:05*), so a press that ran out is never lost
  silently.

### 3.5 What does not change

Only the person confirms, in the window: never from the toast, the system notification, Overview's summary, a row, the terminal or
Ask Daoris; nothing is focused or pressed for them; *Refuse* comes first; five may wait; one use (D156 point 4, the person-door
design §4.2). A press is answered by its id, never rebuilt from its sentence (§3.2). A card is not an approval surface for agents
(D52). There is no *Confirm all*.

## 4. Overview

**What stays.** Overview stays the landing that answers *is anything sitting* (D40). It leads with a short summary of what needs
the person: a line per group with its count and its oldest wait (the presses, *Holding work*, *Waiting for your word*, *Ready for
you*), every line a door that shows *Notifications* at its group (2b). A line for unread messages joins with 2g and one for *Done
and failed* with 2f, so no line counts what nothing produces yet. Then the tiles, *Outstanding* and the repositories by index size,
as today. With nothing waiting, the summary is today's one line, *Nothing needs you* and the sessions working.

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

The lines share one label track while the summary is 32rem wide or more, each value beside its words (R1); below it they stack, as
R1 says (`ui-ux-design.md:168-169`). The summary counts by group and never restates the bell's total.

**What leaves.** Every row of *What needs you*, with its question, its acts and its door, moves to *Notifications*; nothing is listed
one by one on Overview. Overview's activity badge moves to the bell: a count lives in one place (the platform language §2), and the
place that lists the rows is *Notifications* now. The taskbar keeps the same count.

**How everything stays findable.** Every row that left is in *Notifications* with the same words, the same acts and the same order.
The summary's lines are doors into it. The bell is on every view, and View → *Notifications* and the palette reach it. Ask Daoris's
room is told the view's name, as each view's is (`placements.ts:11-19`, *The window*). The terminal reads the same (2i). Sessions'
own *Waiting on you* group and its badge's dot stay where they are, and each wait stays in its session's head and foot.

## 5. Messages, and what is announced how

**D188's levels, mapped onto the person.** For a working agent, *ordinary* waits for a natural point, *urgent* reaches its next step,
and *now* cuts its step, which the person alone may send. For the person, the natural point is opening *Notifications*, the next step
is the next moment they are at the window, and cutting what they do is the desktop: a system notification and a lit taskbar button.
So an agent's *ordinary* message is counted, its *urgent* one is said in the window, and nothing an agent sends ever reaches the
desktop, at any level. The desktop is kept for what waits on the person with a clock or holds work, and what the person waits on.

| What comes | Group | The bell | At a focused window | Behind |
|---|---|---|---|---|
| A terminal's press | Waiting for your confirmation | number; the presses' count in the warning hue while one waits unshown (2b) | toast with *Open* in a main window, staying until the view opens or the press ends; in a secondary one, a toast naming where it waits (2b) | the balloon, naming no route (2a), in the window's language (2d); the main window's button lit (2b) |
| A press at the service's door | Waiting for your confirmation | number, the page's until 2j; the warning hue as above (2b) | toast with *Open*, staying as above (2b) | nothing: its terminal says it waits |
| A park, a parked quest, a start waiting for accounts | What needs you | number | toast with *Open*, as today with a door | notification as today, in the window's language (2d) |
| A held tool call, which its bound refuses by itself | What needs you | number | toast with *Open* (2d) | notification (2d) |
| Every other row (go-ahead, helper, answer alone, proposal, intake, departure, set-up, opinion, rule, unit of work, unanswerable, review, trust, signed-out account, hand-over ask) | What needs you | number | nothing new | nothing |
| A conversation answered the person's words | Messages | dot | toast with *Open*, but not over that conversation on screen (NOTIFY1c) | notification, as today (NOTIFY1a) |
| An agent's or a teammate's message, urgent, to everyone | Messages | dot | toast with *Open*, once per message (2g) | nothing |
| An agent's message, ordinary, or one to a single repository | Messages | dot | nothing | nothing |
| A session ended without the person asking | Done and failed | dot | toast with *Open*, as today; with the headless loop driving, from the desktop host's re-read of the record (2f, §2.5) | notification, as today |
| A press that ended unanswered | Done and failed | dot | toast (2f) | nothing: its terminal says it too |
| The look's lines | Done and failed where they are news (2h, §5.1); else the log | dot where news | nothing | nothing |
| The person's own act: its outcome or its refusal | none | — | where it was pressed (R3), or a toast where that place has gone | — |

- **A message to one repository is that session's**, though the person may read it: it counts on its ask, and never announces.
- **A message from before D188's levels** reads as ordinary.
- **Each toast is said once, in the window the person is looking at**: the focused window speaks, another only counts, so two
  windows never say one thing twice. A secondary window speaks by the same rule when it is the one focused, its toasts with no
  door, since it holds no view.
- **The notify switch** (Settings → Driver, `daoris driver notify`) governs every announcement in this table but a press's, each new
  producer's too (a held call's, a message's); it never governs what is recorded or what *Notifications* lists.

**Arrivals and snapshots.** What lists and counts is a snapshot; what is said aloud is an arrival, and the two never mix (D182
point 3 lets a snapshot be coalesced, skipped to or read again whole, `D182.md:24-27`).

- **An arrival is an event carrying its identity**, never a field of a slice: a press's id (`Confirming`), a session's park or end
  (`SESSION_ATTENTION`, as today), a notice's id, a message's id as the service keeps it.
- **The host says each identity once per run.** It keeps the identities it said in memory, bounded, and at its start says nothing
  older than its first judgement, so a restart repeats nothing. Each message is judged by its own identity and level, so an urgent
  message followed by an ordinary one before the next judgement is still said, and a second snapshot says nothing.
- **For messages, a watermark per ask in the modules.** The first judgement of an ask records its newest message and says
  nothing, the way `AttentionWatch.Observe`'s first look is a baseline, never a backlog (`Attention.cs:191-211`); each later
  judgement says the messages past the ask's watermark and moves it. An ask first seen after the start is a baseline too.
- **A notice another process wrote is an arrival by the desktop host's re-read** (§2.5): with the headless loop driving, no event
  of its reaches the desktop, so a notice id the desktop host's re-read of the record has not seen is the arrival, bounded, and
  nothing older than that host's first read is said.
- **A window says an arrival when it is the window focused as it hears it**, and keeps the identities it said in memory, so an event
  delivered twice is said once. A window not focused says nothing, and the item is in the view, counted.
- **An arrival missed is not said later.** The snapshot carries what is there; the voice is for the moment. There is no queue of
  arrivals waiting to be said and no acknowledgement from the window that said one (§7). That is accepted for news, which the view
  keeps and counts. For a press it is accepted only because a press's signal persists while it waits unshown: the bell's warning
  hue and its toast (§3.3).

### 5.1 The look's lines, classed

A tick's report is a log (D62): its lines are toasted today (`ShellSignals.tsx:61-62`), printed by a headless host
(`Driver.Host/Program.cs:887`), and written to no machine log, whose look line is its timing (`DriverWatch.cs:842-851`). 2h first
writes each line as a declared machine-log event, then stops toasting them, each going where its class says:

| Class | What the line says | Where it goes | The look's own lines |
|---|---|---|---|
| `retry` | the look could not read or do something, and tries at the next | the machine log alone | rules (`Driver.cs:603`), reviews (`:669`), landing (`:1076`), registry (`:1157`, `:1161`), setup (`:1181`), hand-over (`DriverHandOvers.cs:51`, `:152`), meeting (`DriverMeetings.cs:97`), brainstorm (`DriverBrainstorms.cs:133`), intake (`DriverIntakes.cs:69`, `:356`), helper (`DriverHelpers.cs:122`), opinion (`OpinionGateLook.cs:70`), workflow (`WorkflowRunStart.cs:77`) |
| `own` | the person's own act, carried out: a cancel the person pressed is never said back (R3, `ui-ux-design.md:231-232`) | the machine log alone | a meeting or a brainstorm stopped by the person before it ran (`DriverMeetings.cs:63`, `DriverBrainstorms.cs:98`) |
| `wait` | something now waits on the person | its row of *What needs you*; the line the log's | a helper's ask (`DriverHelpers.cs:107`, a row today), an agent's hand-over ask (`DriverHandOvers.cs:181`, 2e) |
| `progress` | what moved as it was asked, which its own place says | the machine log alone | a session going on (`Driver.cs:753`), a start not begun beside another (`:847`), a hand-over carried out (`DriverHandOvers.cs:132`, `:215`, `:251`), an intake ended on its ask's answer (`DriverIntakes.cs:360`) |
| `news` | an end or a failure nobody pressed and nobody must answer | a coded notice in *Done and failed*, and the log | a hand-over that could not be made, refused or dropped (`DriverHandOvers.cs:95`, `:124`, `:170`, `:174`, `:245`), work handed back to this machine's agent (`:70`), a session stopped on a lost claim (`Driver.cs:1310`), a meeting or a brainstorm cut short by a stopped driver (`DriverMeetings.cs:83`, `DriverBrainstorms.cs:119`) |

The lines the look's producers hand it whole (`Driver.cs:590`, `:599`, `:646`, `:653`, `:663`, `:808`, `:1065`, `:1156`, `:1175`,
`:1207`, `:1213`, `:1246`, `:1265`, `:1450`) are classed in 2h by the same rules, each at its producer; an end the watch already
announces (`:1246`) is `progress`. The census fails on a line with no class.

## 6. The rules it keeps

- **The person's door (D156).** §3.5. *Notifications* is the window's; every route it adds that changes the person's state is
  `Person` in the pipe's census, and nothing in it acts for the person.
- **D185's rules.** One column: the view lists its items one under another, nothing side by side, at the side bar's width and in
  the panel alike. The platform's own controls: `Button`, `Pill`, `Dot`, the activity bar's `CountBadge` for the bell, `InlineConfirm`
  for an act that asks once, `AttentionRow` for every row, the toast extended with a door; one new icon, the bell. R1: the summary's
  lines on one label track at 32rem and wider, stacked below it, and the card's facts stacked at the side bar's width
  (`ui-ux-design.md:168-169`). R2: the press says its age and its time left, and nothing else in the view invents a bound; the
  bell's count wears open's hue, and the warning hue with the presses' count while a press waits unshown (§3.3). R3: a refused
  act is said under its row or card, never as a toast but where that place has gone, as a press's card past its time (§3.4); the
  person's own cancel is never said back (§5.1). R5: reached from
  the frame (the bell, View, the palette), a door from the summary, Ask Daoris's rows (an `Exempt` for the person's own reading,
  §2.5), and its terminal twin. R6: stories at the side bar's floor (300 px) and at 45% of the install's window, in English and
  中文, light and dark. R7: the view lays out by its own width; the bell is placed against the window, in the strip.
- **Both languages.** Chrome translates; what a session or an agent wrote, and the words a press carries, are shown as written
  (`translation-parity.md`). A state is said by a key per state, never a raw code in a sentence. The new words are below; their
  budgets are `names:check -- --strict`'s, built by the row that shows them.

| Key | Kind | en | zh |
|---|---|---|---|
| `notifications.title` | title, tab | Notifications | 通知 |
| `notifications.open` | button | Notifications | 通知 |
| `notifications.tip` | sentence | {{count}} need you | {{count}} 个等你处理 |
| `notifications.tipUnread` | sentence | {{count}} need you, {{unread}} unread | {{count}} 个等你处理，{{unread}} 条未读 |
| `notifications.tipConfirm` | sentence | {{count}} waiting for your confirmation | {{count}} 个等你确认 |
| `notifications.group.messages` | section | Messages | 消息 |
| `notifications.group.news` | section | Done and failed | 完成与失败 |
| `notifications.unread` | status | {{count}} unread | {{count}} 条未读 |
| `notifications.markRead` / `markAllRead` | button | Mark read / Mark all read | 标为已读 / 全部标为已读 |
| `notifications.nothingUnread` | sentence | Nothing unread. | 没有未读内容。 |
| `notifications.toast.open` | button | Open | 打开 |
| `notifications.toast.inMain` | sentence | A terminal's press waits in Daoris's main window | 一个终端的请求在 Daoris 主窗口等你确认 |
| `person.say` | sentence | A terminal asks to say to {{session}}, as your words, {{level}}: | 一个终端请求以你的名义，{{level}}对 {{session}} 说： |
| `person.session` | label | {{title}} ({{repository}} · {{id}}) | {{title}}（{{repository}} · {{id}}） |
| `person.sessionIn` | label | {{title}} ({{workspace}} · {{repository}} · {{id}}) | {{title}}（{{workspace}} · {{repository}} · {{id}}） |
| `person.attach` | sentence | and to attach these files, read from this machine: | 并附上这些文件，从本机读取： |
| `person.also` | label | It also carries | 它还携带 |
| `person.asked` | status | asked {{ago}} | {{ago}}发出 |
| `person.left` | status | refused by itself in {{time}} | {{time}}后自动拒绝 |
| `person.details` | button | Details | 详情 |
| `notifications.system.confirming.title` | sentence | A terminal waits for your confirmation | 一个终端在等你确认 |
| `notifications.system.confirming.body` | sentence | It asks to {{act}}. Open Daoris to confirm or refuse it. | 它请求{{act}}。打开 Daoris 确认或拒绝。 |
| `notifications.system.parked.title` | sentence | {{repository}}: a session waits on you | {{repository}}：一个会话等你处理 |
| `notifications.news.ended.<state>` | sentence | one per end state: {{session}} failed, {{session}} was stopped, … | 每个结束状态一句：{{session}}失败，{{session}}已停止，… |
| `notifications.news.expired` | sentence | A terminal's press to {{act}} was refused by itself at {{time}} | 一个终端请求{{act}}，已在 {{time}} 自动拒绝 |

  The existing `person.label` (*Waiting for your confirmation*, 等待你确认) and `work.attention.title` (*What needs you*, 等你处理)
  name their groups. **The glossary gains one term, *notifications*** (en *Notifications*, zh 通知, the steward's default, §12): the
  view and the title bar's button holding everything that came to the person, the presses waiting, what needs them, messages, and
  what ended. Avoid en *inbox* (it holds messages alone) and *Notification Center*, zh 通知中心 and 消息中心: 通知中心 is Windows's and
  macOS's own name for the system's list, where Daoris's notifications land too, and the two must not be confused.
- **.NET judges, the page renders (D182).** §2.5: what needs the person, what is news, what is read, which arrival is new, and which
  values a press's sentence names are judged in .NET once for every window; the page words, orders and draws them.

## 7. Rejected

- **Keeping the band, smaller or folded.** A fold still lays the press over every page, and a band under the strip cannot be read
  beside the session it is about.
- **A popover under the bell** (VS Code's center). It lies over the page and closes at a press elsewhere, while a press's card wants
  to stay open as the person looks at the session.
- **A drawer.** A drawer is a form, with a scrim that dims the page (D41, the frame model §3d): the blocking the owner reported.
- **A tenth place on the activity bar.** A place is a decision with its own counts (the UX6 design §8, R5), and it takes the main
  area, so nothing could be answered beside the session.
- **Overview as the view.** The owner's direction keeps Overview a summary.
- **Opening the view by itself when a press arrives**, or moving the focus to it: it changes what a region holds unasked.
- **Confirming from the toast, the notification, a row, the terminal or Ask Daoris, and *Confirm all*** (D156 §4.2, D52).
- **A press's values copied into its row of *What needs you*.** One carrier for a payload, the confirmation channel (§0.2).
- **A session named by its title alone, a file by its name alone, and a `quiet` value** out of the card's view: a title or a name
  can be another's, and `said` looked quiet and chooses how the act runs (§3.2).
- **Half the card in view as *shown*.** A card taller than twice its area could never be shown, and its two minutes never start
  (§3.4).
- **A two-minute bound that pauses while the card is out of view**, a longer bound, or ten minutes to be found raised toward the
  terminal's thirty (§3.4).
- **A time to be found for the service's confirmations here.** Its two minutes also bound its grant's use, so it is the
  person-door protocol's change, with its client's (§3.4).
- **Dismissing a row of *What needs you*.** A wait nobody sees is refused at its bound or holds work that never moves.
- **A message read by being listed, or by a session's panel.** The item shows one line of several, and the panel a part.
- **A second unread count** beside the service's (§2.4).
- **One number mixing what needs the person and what is unread**, or two numbers on the bell. The number is what holds work, as the
  taskbar's is; unread is a dot, the way Sessions' badge marks what waits beside its count (SESSBADGE1).
- **An unread count on the taskbar** (a mail program's convention). The taskbar counts what holds work.
- **An agent's message as a system notification, at any level.** D188 gives *now* to the person alone.
- **The press's values in the notification.** The system keeps notifications in its own history, outside Daoris.
- **The page composing each notification as it arrives.** A window behind may be held back by the engine; the shell is told by the
  bus directly and words it from what the page handed it at load.
- **An announcement as a snapshot's field**, and **a queue of arrivals each window acknowledges.** A field is said again by every
  snapshot read whole; a queue says old news to the next window brought forward, where the view already counts it (§5).
- **Notices kept in memory alone.** A staged update restarts Daoris often (D139), and what ended overnight would be gone.
- **Notices derived from the session records.** No record says whether the person ended a session, so every ending, the person's
  own stop among them, would read as news.
- **The record written by the lease holder alone.** The desktop's page and conversations work before it holds the lease (§2.5).
- **The notices as an array**, which `JsonKeeping` writes whole, losing a field a later build wrote inside one (§2.5).
- **A notice's workspace read again from the sessions.** A session gone, its notice would read as every window's (§2.4).
- **The look's lines in the view as they are.** They are English log lines, most a look's transient failure that the next look
  answers (D62); and their toasts are not stopped before the lines are logged (§5.1).
- **The whole of *Notifications* before the owner's three reports are fixed.** NOTIFY2a and 2b fix them with no store changed
  (§0.2).
- **通知中心 as the name** (§6).

Round 3 added:

- **A seven-second toast, or the bell's ordinary count, as a press's only signal in a window in front.** The toast closes or is
  dropped unread, and the count looks like any other row's; the bell's warning hue and a toast that stays say it until it is
  shown or ends (§3.3).
- **The region's scroll anchored at its foot** so that its buttons show: it changes what a region shows unasked. A region that
  fills its area is shown only with its buttons' row in view (§3.4).
- **A late refusal drawn on its card.** The card has gone with its press; the refusal is toasted (§3.4).
- **A reader setting an unreadable record aside, or a refused write retried.** Only the writer holding the keep lock sets a
  record aside; a refused write is logged and dropped (§2.5).
- **A notice another process wrote left unsaid in the desktop**, with the headless loop driving: the desktop host's re-read of the
  record is its arrival (§2.5, §5).
- **The person's own posts and Daoris's lines counted as unread** for the person (§2.4).
- **Coordinating the windows' polls so one window alone says a service confirmation.** Two windows polling on their own clocks
  may each say it once, which is accepted until NOTIFY2j pushes the service's presses (§3.3).

## 8. What this amends, once accepted

- **D150's UX6c note and the UX6 design §6.1**: *What needs you* is drawn in *Notifications*, and Overview leads with its summary;
  its groups, order, words and acts stand. **The platform language**: §2, the strip holds *Notifications* and Overview's activity
  badge goes to it; §4's *Toast*, a third use: an arrival worth interrupting for, said once in the focused window, with a door into
  the view; §5's *Overview*, the summary.
- **D185's R5**: "a door from *What needs you*" is a row in the view's *What needs you*; its meaning stands.
- **D156's CONFIRMSEEN1 note**: the card is drawn in the view, never under the strip; *shown* is the card's decision region in view
  there, its buttons' row too where the region fills its area; an answer or a word that it is shown at or past its time ends it as
  expired, its refusal toasted; the press's notification names no route and is worded by the page's words; the main window's
  taskbar button is lit for a press; while a press waits unshown the bell wears the warning hue with the presses' count, and its
  toast stays until the view opens or the press ends. Point 4 and the person-door design §4.2's "on the app strip and in What
  needs you" hold: the strip's *Notifications* and the view. The service door's two minutes stand.
- **D167**: the person's mark on a teammate's ask is kept by this machine's service (2g), never synced; and the person's entry's
  unread count passes over the person's own posts and Daoris's lines (2g, §2.4).
- **D126's NOTIFY1b note and D171's MAINWINDOWS2b note**: each main window tells and wears its own count.
- **SURF5b and NOTIFY1a, and D24's verbatim rule for these headlines**: the notification's headline is worded by kind in the window's
  language from the event's facts; what a session wrote stays as written.
- **The look's report (D62, `ShellSignals`)**: its lines are logged as declared events and are no longer toasts.
- **D195's inventory and D172's layout §4**: `data/notices.json`, the driver's, kept seven days and at most 200 notices, written by
  any process on the home under its keep lock.
- **Settings → Driver's notify switch**: its words say it governs being told, not what is recorded or listed, and that a press is
  always announced.
- **The frame model and the dock design's view table**: a view, *Notifications*, on the right by default.

## 9. Build rows

Each is a feature a person can see working (D187), end to end across its lanes. They are named NOTIFY2, not NOTIFY1a and on: NOTIFY1a
to NOTIFY1d already name rows archived on 2026-10-15 and 2026-10-16 (the answered kind, the taskbar's count, no toast over the
conversation on screen, the balloon's choice tested), as D195 named its KEEP1 for the same reason. Each runs its own tests and the
quick checks; the merge and the CI beside run the rest (D180). **Their order** follows §0.2: the presentation on the transports that
exist, changing no store (2a, the pipe's half, on its own tests and with no view; 2b, the view, the rest of the owner's report; 2c,
a count per window; 2d, behind the window in the person's language; 2e, the hand-over ask as a row); then the notice record (2f);
then the messages (2g), which keep a conversation's answer in 2f's record; then the look's lines (2h), which need 2f's notices and
2e's row; the terminal (2i); and the service's presses told to the windows (2j), which needs 2b alone and may be taken whenever
the service's door is pushed. **Round 3 renamed them** (Fable's F4.2, §11): round 2's 2a split into 2a, the pipe's half, and 2b,
the view; its 2b is 2c, its 2c 2d, its 2d 2e, its 2e 2f, its 2f 2g, its 2g 2h and its 2h 2i; 2j is new (F4.1). Round 1's letters
map to round 2's as §11 says.

- **NOTIFY2a — a terminal's press answered or shown past its time runs nothing, and its balloon names no route** (modules;
  web-shell for the refusal's toast; first). The pipe's half, landing first on its own tests, with no view and no store changed.
  *What the person sees:* a Confirm pressed after a press's time refuses it, said by a toast since its card has gone with it, and
  nothing runs; a press's balloon behind the window names no route's constant. §3.3, §3.4. The parts: in `PipeConfirmations`, an
  answer or a word that the card is shown, at or past its time, ending the press as expired and answered `gone`, the clock read in
  `End` for every answer and in `Show` before its early return for a card shown before (`:177`); the `Confirming` event gaining the
  press's id, a field added; the balloon's words naming no route; the card, today's band's until 2b moves it, toasting a refusal
  whose card has gone with its press (R3). *Proof:* `PipeConfirmationsTests` seen failing first, on a held clock with the timer
  held late (`held-clocks.md`): a Confirm after the press's time, a shown word after its ten minutes, and a shown word for a card
  shown before, after its two minutes, each ending it expired with nothing run; `Confirming` carrying the id; the balloon's words
  naming no route; a `Confirmations` test in en and zh, seen failing first, that a Confirm answered `gone` after its press ended is
  toasted.
- **NOTIFY2b — the title bar's Notifications show a terminal's press in plain words, nothing is drawn across the page, a press
  waiting unshown keeps a signal, and Overview keeps a summary** (web-shell, modules; driver for Ask Daoris's room; after 2a).
  Changes no store. *What the person sees:* a bell on the title bar counting what needs them, in the warning hue with the presses'
  count while a press waits unshown; pressed, the view where it stands (the right side bar by default) shows *Notifications*: the
  presses of both sources first, each worded *A terminal asks to say to Fix the export feed (game · 3f2a91c), as your words, at
  its next step:* with the words quoted, the files by their paths, every other value listed, its time left, *Refuse* and
  *Confirm*; then *What needs you* as Overview lists it today; no band under the title bar for any press; at a main window, a toast
  with *Open* says a press came and stays until the view opens or the press ends; behind it, the window's taskbar button stays lit
  while the press waits unseen; Overview opens with a few lines (each group's count and oldest wait), each opening the view at its
  group, and lists no row. §2.1, §2.2's first two groups, §2.3, §2.4's first point, §3, §4 but its unread lines. The parts: the
  view `notifications` (`placements.ts`, the views menu, `help/where.ts`, Ask Daoris's room and its test); the bell with its count
  (each press of either source once and every other row) and its warning hue with the presses' count while any waits unshown,
  View → *Notifications* and the palette; both sources' cards out of `App.tsx:1104-1114`'s band, the adoption notice left there,
  read from the confirmation channel alone; `PipeWording` in the modules with its census test, and `named` on the pipe's telling;
  the card's sentence with its references (the workspace too where a repository's name is in two), *It also carries*, *Details*,
  its age and time left, in its bounded region; *shown* by the region in view, its buttons' row in view where the region fills its
  area, judged again at each change of intersection, fold, tab, dock, size and focus, and not under a drawer's scrim; `ToastItem`
  gaining a door and a press's toast staying until the view opens or the press ends, said once in the focused window on its
  event's id, a service confirmation's on its poll's first sight, a secondary window's with no door; `FlashWindowEx` on the main
  window last in front, by §3.3's rule; *What needs you* drawn in the view, its `confirmation` row as its card; Overview's summary
  from `useAttention` and the presses (counts by `ATTENTION_GROUP`, the oldest `since`) on R1's track; Overview's activity badge
  gone (`App.tsx:1132-1139`); the glossary's *notifications* (通知) and `overview` meaning; the accepted budgets that name
  "Overview's main area" judged again at the view's width; the end-to-end spec that reads *waiting on you* (`person-waits.md`), its
  hits named for the parent. *Proof:* the census test failing on a `Person` route with no wording row and no act's words, and
  `SESSION_INPUT`'s row naming `id`, `text`, `level`, `paths` and `said` as a `form`; `Confirmations` tests in en and zh, seen
  failing first: no band for a press of either source; `SESSION_INPUT`'s sentence naming the session with its repository and short
  id, two sessions sharing a title told apart, a repository's name in two workspaces said with its workspace, a conversation's
  generic title with its id, the words quoted, each path whole, the route only under *Details*, every value no role names listed;
  a press of 256 values and one of long words each in a bounded region; *shown* said once with the region in view (an
  `IntersectionObserver` stand-in) in a focused window, never while scrolled out, its group folded, the view's tab not shown, the
  window unfocused or a drawer's scrim over it, said when docked in the panel, said when a region taller than its area fills it
  with its buttons' row in view and not before, judged again after a resize, a second window's word moving nothing, and never by
  the toast, the bell or Overview; the bell in the warning hue with the presses' count while a press of either source waits
  unshown, back to open's hue and the whole count once each is shown or ended; the press's toast staying past seven seconds until
  the view opens or the press ends; the toast in the focused main window alone, once for an event heard twice, its *Open* showing
  the view at the card; a service confirmation toasted once per window and counted; a secondary window's toast with no door; a
  modules test of the flash's judgement (which window, lit while any press waits unshown, stopped at a main window's coming
  forward and at the last such press's end or showing); the bell's and the taskbar's count, each press once; `OverviewView` tests
  in en and zh (no row drawn, each group's count and oldest, each door showing the view at its group, *Nothing needs you* with the
  working count, a browser's line) and `App` tests (no badge on Overview); the view's stories at 300 px and 45% and Overview's at
  wide, half and narrow, en and 中文, light and dark, the bell's warning hue in both themes; `i18n:check`,
  `names:check -- --strict`; the window with a press from a terminal, the view closed (the parent's).
- **NOTIFY2c — every main window counts what needs you in its own workspace** (modules, web-shell; after 2b). *What the person sees:* with two
  windows open, one per workspace, each taskbar button and title wears its own workspace's count, and a notification's click opens
  the window whose workspace holds what it is about. §2.4: `useTaskbarCount` told by every main window with its name; `SET_BADGE`
  taking the name as `SET_TITLE` does, and the order kept per window (`WindowsModule.cs:43-45` keeps one for all); a registry in the
  windows module of each main window's workspace by its name, told by the page as data (a `workspace` field added to `SET_TITLE`,
  never read from the display title, `:113-117`), from which a notification's click picks the window holding what it is about, or
  the main window last in front where none or several do; each window wearing its own (`WaitingCount`, `SecondaryWindowHost`).
  *Proof:* `WaitingBadgeTests` and the windows module's tests: two windows' counts told out of order each kept by its own order; the
  registry's pick with one window holding the workspace, none and two; the page's test that each main window tells its own and a
  secondary one none; the taskbar with two windows (the parent's).
- **NOTIFY2d — behind the window, what waits on the person is announced in their language** (modules, web-shell, web-settings).
  *What the person sees:* with Daoris behind another program, a press raises a system notification in 中文 or English as the window
  speaks, naming its act and no value, and its click shows the view at the card in the window that holds it; parks, endings and
  answers speak the window's language too; a held tool call is announced as a park is; Settings → Driver's switch says what it
  governs. §3.3 and §5: `SET_NOTICE_WORDS`; `SessionBalloon` wording by kind from the handed words, today's English where none; an
  end state said by its own key (§6); the held call's announcement where the attention slice first sees its row, under the switch;
  the switch's words. *Proof:* `SessionBalloonTests` per kind with words handed in en and zh and with none, and an end state never
  raw in a sentence; a modules test of the held call announced once, and not with the switch off; the switch's words in both
  languages; a press with the window behind another program (the parent's).
- **NOTIFY2e — an agent's ask to hand its session over waits in What needs you** (modules, web-shell). *What the person sees:* the
  ask is a row with *Hand over to {agent}* and *Not now*, counted by the bell and the taskbar, where today it is drawn only on its
  session's page, in its head and foot. Every place `person-waits.md` lists: the row judged in `NeedsAPerson` from the asks the look
  holds, carrying the ask's own id (`SessionsRoutes.cs:272-282`) and listed only while the look holds it waiting
  (`HandOverAgentAsk.tsx:41-42`), its group by whether its session stands still on it, `KIND_ORDER`, its words and acts, its hook's
  coverage row, its words in the glossary's kinds. *Proof:* `AttentionStateSliceTests`' cases (waiting listed with its id; approved,
  taken or refused not listed); the attention, band and badge tests in en and zh; the coverage row (the parent's at the merge where
  the lane does not reach it).
- **NOTIFY2f — what ended, failed or went unanswered is kept in Notifications, read alike in every window** (driver, modules,
  web-shell). *What the person sees:* *Done and failed* lists each session that ended without them asking and each press refused by
  its time, newest first, the unread marked and a dot on the bell; shown in one window, they are read in every window, the headless
  host driving or not, and what the headless host records is said in the window they look at; *Dismiss* and *Mark all read*; a week
  later they are gone, the sessions' records keeping what happened; Overview's summary gains its *Done and failed* line. §2.2's
  fourth group, §2.4, §2.5, §5: the driver library's `Notices` and `data/notices.json` (the map by id, one keep-lock hold from read
  to rename, the workspace stored, the age and the count, the records it cannot take, set aside by the locked writer alone through
  `Notices`' own path, a refused write logged and dropped); the writer pushing its own slice after its write; a home watch reading it
  again in every process, the topic's thirty-second deadline behind a missed event, a failed read keeping the last snapshot; the
  desktop host's re-read the arrival source of a notice another process wrote; the watch recording every notice whatever the switch
  says, the headless host too; the press's unanswered end recorded by `PipeConfirmations`; the `notices` slice's `news`;
  `NOTICES_READ` and `NOTICE_DISMISS` with their census, publishing and coverage rows; D195's inventory row and D172's layout line.
  *Proof:* driver tests of `Notices` seen failing first: a notice per kind; recorded with the switch off; pruned by age and by count,
  the oldest first whether read or not; a notice older than seven days not read after a restart that wrote nothing; a field a later
  build wrote inside a notice kept across an add, a read, a dismiss and a prune; a record that does not parse set aside by the writer
  under the lock and a new one begun, and left as it is by a reader; a newer `schemaVersion` left as it is and read as none; a write
  refused (the keep lock held past its wait, a key written twice, a shape changed under the edit) logged and dropped, the record
  unchanged; two writers on one home, as two processes and as two threads, adding, reading and dismissing at once and losing none; a
  notice's workspace kept after its session is gone. The slice's tests: allow-listed fields; a read moving one notice; the writer's
  own write pushed with no wake; a write by another process heard and pushed, and read at the deadline with the watcher's event
  missed; a failed read keeping the last snapshot; a notice the headless host wrote said once in the desktop's focused window, none
  older than the desktop host's first read; two windows' scopes. The routes' tests; the page's tests in en and zh; the stories.
- **NOTIFY2g — an agent's message reaches Notifications by its urgency, a teammate's too, and a conversation's answer is kept**
  (service, modules, web-shell). *What the person sees:* *Messages* lists each ask with unread items, a teammate's among them (the
  count its row says, the newest message's first line and who wrote it), and each conversation that answered them; an urgent message
  to everyone is toasted once in the window they look at, with *Open*; an ordinary one, or one to a single repository, only counts;
  nothing an agent says reaches the desktop; reading a teammate's ask clears it; their own posts and Daoris's lines count as unread
  nowhere; Overview's summary gains its *Messages* line. §2.2's third group, §2.4, §5: in the service, the person's mark kept on an
  ask its mirror holds (`Channel.cs:1061-1068` finds the local store's alone), the person's entry alone, on this machine and never on
  the sync wire, its door classed as a local ask's mark is; the person's entry's count passing over their own posts and Daoris's
  lines (`Channel.cs:981`); the slice's `messages` partition with the service's own `unread`, and `useChannelUnreads` reading it
  once live; a watermark per ask in the modules, its first judgement a baseline; the `answered` notice kind in 2f's record, made
  where the desktop's process hears the answer, recorded before the switch's gate, with its read rule; arrivals by identity (§5):
  each message said once per run by the host and once by the focused window; *Open* and *Mark read* through `CHANNEL_MARK` with its
  moment. *Proof:* the service's tests: a teammate's ask marked, an ask nowhere refused, the mark never on the wire; the person's
  count passing over their own posts and Daoris's lines, and a session's entry counting as before; the partition's tests: the same
  `unread` as the ask's row, a teammate's ask counted and marked, the person's own and Daoris's lines never newest, a message to one
  repository counted and never announced, an urgent message to everyone said once, also when an ordinary one follows it before the
  next judgement, a snapshot read again saying nothing, an ask's first judgement saying nothing, a restart saying nothing older than
  its first judgement, one from before the levels ordinary; the `answered` notice recorded with the switch off, and read by opening
  its conversation in a window in front, by *Open* and by *Mark read*; the page's tests in en and zh: the toast once in the focused
  window alone, an event heard twice said once, *Mark read* moving the mark, reading on the ask's page clearing the item, the
  session's panel leaving it.
- **NOTIFY2h — the look's lines stop interrupting: they are logged, what is news is a notice, a wait is its row** (driver, modules,
  web-shell). *What the person sees:* no English log line toasts in a 中文 window; a hand-over that could not be made, work handed
  back, a session stopped on a lost claim appear in *Done and failed* in the window's language; a meeting or a brainstorm they
  stopped is not said back; a look that could not read something and tries at the next is in Settings → Logs and the console only.
  §5.1: first, each line the look adds written as a declared machine-log event (`look.said`, its class and its family), the
  confirmation's codes-only event kept (`PipeConfirmations.cs:321-322`); then each line classed, a `news` line a coded notice through
  2f's `Notices`, a `wait` line its row (2e); `ShellSignals` toasting none of them; the headless console as it is. *Proof:* a driver
  census over every line the look adds and every producer that hands it lines, an unclassed one failing, and `look.said` written for
  each; a meeting and a brainstorm the person stopped making no notice; the notices' words in en and zh; `ShellSignals.test.tsx` (a
  tick's lines toast nothing).
- **NOTIFY2i — the terminal reads what came to the person** (driver, modules). *What the person sees:* `daoris-driver notices`
  prints what waits for their confirmation (by act and session), what needs them by group, unread messages by ask and *Done and
  failed*, with `--json`; it changes nothing. Through the running Daoris, an `Open` read in the census; alone, the notices' record,
  saying that the presses, what needs them and the messages are the running Daoris's to read and cannot be read without it.
  *Proof:* the verb's text and JSON tests through the instance and alone (the live groups said unread there), its census row, its
  usage line.
- **NOTIFY2j — a press at the service's door is told to the windows, not polled by each** (modules, web-shell, service where its
  door must say a change; after 2b). *What the person sees:* the presses waiting at the service's door are counted in every
  window's bell and taskbar alike, and each is said once in the window they look at. §2.5, §3.3: the desktop's host reads the
  service's waiting confirmations and tells them to the windows on the confirmation channel beside the pipe's, so their count is
  judged once in .NET (D182) and each window's five-second poll (`person.ts:74-95`) goes; an older host keeps the poll
  (`pushed-slices.md`). *Proof:* a modules test of the service's presses told at a change and counted once beside the pipe's; the
  page's count read from the told list; the toast said once per press across two windows; an older host's poll kept.

## 10. What this document's gate does not cover

Documents only; nothing is built. The statements about today were read from the code at `7c2bce824`, again at `5e6843642`, and
where Fable's read touched them at `3d3dd47de`, and none was run; the window was not looked at, and the band's height for five
presses is counted from its lines, not measured. Not shown by any test: that a row of *What needs you* with its acts (an account's
sign-in steps under it above all) reads at the side bar's 300 px; that the kit's engine runs `IntersectionObserver` in a window
behind others as Chromium documents; that `FlashWindowEx` keeps a frameless window's button lit until it comes forward (Windows'
documentation); how long Windows shows a notification before its own list takes it; whether the bell's warning hue is seen at a
glance beside the strip's other controls in both themes; and what reading every live ask's channel at each wake costs on the
install. NOTIFY2b, 2d and 2g prove them or say what they found. A service confirmation's act stays the service's English until
that door carries a code, and its two minutes stay counted from its ask; no row here changes either. Its count stays the page's
until NOTIFY2j, and two windows may each say it once until then (§3.3). The seven days, the 200 notices, the eight lines of a
card's boxes and the two minutes kept are judgements, to be read again on the install after a week of NOTIFY2b and 2f, and with
them whether a service confirmation runs out unseen.

## 11. The reads (D191)

**Codex, 2026-10-21, on round 1.** Every finding was checked against the code at `5e6843642` before it was taken (D160's step 9;
where a file moved since the read, *Checked at* gives its lines as they stand now). All hold; 4.1 and 5.8 are taken in part, said in the verdict. (B) marks what
the read called blocking, (N) what it did not. A finding names a row as round 1 named it; *Taken in* names round 2's row (§9
renumbered them: round 1's 2c is 2b, its 2d is 2c, its 2g is 2d, its 2h is 2g, its 2i is 2h, and its 2b joined 2a). Round 3
renamed them again (§9 and Fable's table below): round 2's 2a is 2a and 2b, its 2b is 2c, its 2c 2d, its 2d 2e, its 2e 2f, its
2f 2g, its 2g 2h and its 2h 2i. The steward's own findings from the check follow (S).

| # | Finding | Checked at | Verdict | Taken in |
|---|---|---|---|---|
| 1.1 | (N) D196's "nothing an agent says reaches the person outside the ask" forgets the session's channel panel the inventory names | `D196.md:14`, `AttendedSession.tsx:307` | holds | D196 point 1 and §1.1 point 7: not announced, nor counted outside the ask; read on its page, its row and in part the panel |
| 1.2 | (N) The inventory reads as though an answer were toasted over its conversation, which the page keeps off | `ShellSignals.tsx:149-153` | holds: the row meant the exception and said it backwards | §1's row |
| 1.3 | (B) A session's channel panel moves no mark, by design, since it shows part of a channel | `ChannelSection.tsx:52-54` | holds | §2.4: the panel leaves the item; the ask's page and *Mark read* read it; 2f's proof |
| 2.1 | (N) Moving the card adds no bypass while the pipe's binding and the card's `source:id` stay; never rebuild a request from its sentence | `InstancePipe.cs:545-548`, `:635-638`, `PipeCensus.cs:243-247`, `Confirmations.tsx:85` | holds | §3.2 *Why it stays safe*, §3.5 |
| 2.2 | (B) A title can be generic or shared and a file's name ambiguous, so the sentence can hide which act is confirmed; `said` chooses the run's path, not the answer's words | `identity.ts:56-67`, `ConversationRoutes.cs:99-123` | holds | §3.2: the session always with its repository and short id, each path whole, `said` a `form` the sentence says, no `quiet` role; 2a's proof |
| 2.3 | (B) *Half the card in view* cannot be reached by a card taller than twice its area (256 values, long words) | `PipeConfirmations.cs:89-90`, `:251` | holds | §3.4: a bounded decision region, *shown* as it lies whole in its area or fills it; scrolling, folding, docking, focus and two windows in 2a's proof |
| 2.4 | (B) The service's confirmations lose their one exposure: two minutes from the ask, no row, no word that one is shown | `PersonConfirmations.cs:117-121`, `AttentionStateSlice.cs:256-257`, `person.ts:74-95` | holds | §3.3, §3.4: counted in the bell and the taskbar, toasted once in the focused window, its time left from the first moment, its two minutes kept as the service counts them and why (S2); 2a; §10 |
| 2.5 | (B) A Confirm is taken past the bound when the timer fires late: only the timer's end reads the clock | `PipeConfirmations.cs:191-192`, `:271-283` | holds, and for a word that the card is shown too (S1) | §3.4 *No answer after its time*; 2a's proof on a held clock |
| 3.1 | (B) The lease holder cannot receive every write: the desktop's page and chats work before it holds the lease, and its pipe after | `DriverLoop.cs:672-681`, `:728-737`, `:1025` | holds | §2.5: any process writes under the keep lock from read to rename, and each reads again at a home watch's change; 2e |
| 3.2 | (B) `JsonKeeping` writes an array whole, and its lock covers the reread and the rename only | `JsonKeeping.cs:30-32`, `:69-74`, `:80-127` | holds | §2.5: the notices a map keyed by id; one hold from read to rename (`SaveHolding`'s shape); 2e's proof |
| 3.3 | (B) A workspace read again from the sessions is lost with its session, and an item with none reads as every window's | `bridge/attention.ts:91` | holds | §2.4, §2.5: stored on the notice at its making |
| 3.4 | (B) The service refuses a mark on an ask its local store does not hold, so a teammate's ask cannot be marked | `Channel.cs:1061-1063`, `ChannelSection.tsx:56-58` | holds | §2.4 and 2f: the service keeps the person's mark on an ask its mirror holds, in the service lane with its tests |
| 3.5 | (B) Two unread models; and an `Answered` notice has no mark to settle it | `Channel.cs:974-982` | holds | §2.4: one model, the service's count; an answer a notice read by *Open*, *Mark read* or its conversation read in front; 2f |
| 3.6 | (N) Pruning at each write leaves old notices shown on a quiet machine; say restart, the filter and the cap | round 1's §2.5 | holds | §2.5 *Its age and its count*; 2e's proof |
| 4.1 | (B) One `announce` field cannot say each message once across D182's coalesced and resynced snapshots; give arrivals identities, pending announcements and an acknowledgement | `D182.md:24-27` | holds; taken in part | §5 *Arrivals and snapshots*: an arrival is an event with its identity, said once per run by the host and once by the focused window; no pending queue and no acknowledgement, since an arrival missed is counted in the view and saying it later says old news (§7) |
| 4.2 | (B) The look's lines reach no machine log, so removing their toasts loses them from the window | `ShellSignals.tsx:61-62`, `DriverWatch.cs:842-851`, `Driver.Host/Program.cs:887` | holds | §5.1; 2g: a declared `look.said` event first, the confirmation's codes-only event kept |
| 4.3 | (N) The asks' counts still read every channel per ask; move them onto the projection | `asks.ts:44-62`, `pushed-slices.md:23-25` | holds | §2.5's `messages`; 2f |
| 4.4 | (N) R1 stacks below 32rem, under the side bar's 300 px; translate end states; the switch over every new producer, never over recording | `ui-ux-design.md:168-169` | holds | §4, §6 (R1, a key per state), §5's switch; 2c |
| 5.1 | (B) NOTIFY2a: the findings above, and a bell always opening the right side bar where the view may stand in the panel | `placements.ts:23-29` | holds | §2.1: the bell shows the view where it stands; 2a carries 2.2 to 2.5 |
| 5.2 | (B) NOTIFY2b's unread lines came before their producers | round 1's §9 | holds | §4, §9: the wait lines in 2a, each unread line with its producer (2e, 2f) |
| 5.3 | (B) NOTIFY2c needs a badge order per window and a registry of each window's workspace, not a title | `WindowsModule.cs:43-45`, `:88-119` | holds | 2b |
| 5.4 | (N) The flash's life over several presses; a secondary window holds no view; the notifier is quiet for any window of Daoris in front | `SecondaryWindowRoot.tsx:14-26`, `SessionNotifier.cs:146-157` | holds | §3.3's table; 2a |
| 5.5 | (B) NOTIFY2e: ownership, serialization, stored scope, restart and competing operations in its proof | round 1's §9 | holds | §2.5; 2e's proof |
| 5.6 | (B) NOTIFY2f: the service lane for a teammate's mark, and an answer's identity and settling | round 1's §9 | holds | §2.4; 2f |
| 5.7 | (N) NOTIFY2g fits the existing wait's shape: the ask's exact id, only its waiting state | `SessionsRoutes.cs:272-282`, `HandOverAgentAsk.tsx:41-45` | holds | 2d |
| 5.8 | (B) NOTIFY2h needs the classification itself; a brainstorm the person stopped is no news (R3) | `DriverBrainstorms.cs:95-98`, `ui-ux-design.md:231-232` | holds; taken in part | §5.1: five classes, their rules, and every line the look writes itself classed; the lines its producers hand it classed in 2g under the same rules, an unclassed one failing the census |
| 5.9 | (N) NOTIFY2i last is sound; keep it read-only, and say the live groups the alone path cannot read | round 1's §9 | holds | 2h |
| 6.1 | (N) A smaller shape: presentation first on the transports that exist, a payload on the confirmation channel alone; then one notice store; then summaries on the service's marks; arrivals apart from snapshots | `bridge/person.ts:55` | holds | taken whole: §0.2, §2.5, §5, §9's order |
| S1 | A word that the card is shown is taken past its time too: `Show` gives two more minutes to a press whose ten minutes ended while its timer was late | `PipeConfirmations.cs:169-189` | holds | §3.4; 2a's proof |
| S2 | The service's two minutes also bound its grant's use, so a time to be found there changes the door's protocol and its client | `PersonConfirmations.cs:120-121`, `:139-140` | holds | §3.4, §7 |

**What this round took from Codex's smaller shape** (6.1): all of it. NOTIFY2a moves the cards of both sources and *What needs you*
into one movable view and makes Overview their summary, reading a press's values from the confirmation channel alone and copying
none into a row; it changes no store and fixes the owner's three reports by itself, as the steward directed. The notice record
follows as one serialized writer per hold (2e); the channels' summaries stand on the service's own marks and its own count, with the
teammate's mark added in the service (2f); and every arrival is said by its identity as an event, never as a snapshot's field (§5).

**The choices this round made**, where two shapes remained: the owner's three reports in one row rather than the view and Overview
apart (the steward's direction); no `quiet` role rather than a table of reasons for leaving a value out (2.2); a bounded region
whose *shown* is filling its area rather than a pause of the two minutes (2.3); the service's two minutes kept, counted and toasted,
rather than a time to be found added at its door (2.4, S2); an answer and a shown word past the time both refused (2.5, S1); writers
in every process under the keep lock with a home watch rather than every write routed to the lease holder (3.1); a map keyed by id
rather than an array given a merge of its own (3.2); the service's unread count rather than a second (3.5); arrivals said once per
run and per window rather than a queue each window acknowledges (4.1); the taskbar's flash in 2a, with the press, rather than with
the notification's language in 2c; a secondary window's toast with no door rather than one bringing the main window forward (5.4);
and the Chinese name 通知 by the steward's default (§12).

**Whether it changes a foundation, and who reads next.** It does, in D191's amendment's classes: a decided rule other designs rest
on (D156's CONFIRMSEEN1 note: the moment a press counts as shown, an answer past its time refused, the card's words beside
*Confirm*), a durable record written by two processes under one lock and read again at a watch's change (§2.5), and a service
change to whose marks it keeps (2f). The steward decides whether Fable reads; in the author's view the reads that would add most
are §3 whole, before NOTIFY2a builds it (whether the wording can still hide what the person must see before *Confirm*; whether the
bounded region and its *shown* hold when scrolled, folded, docked and in two windows; the bound refusing a late answer; the service's
two minutes kept), and §2.4, §2.5 and §5's *Arrivals and snapshots*, before NOTIFY2e and 2f (the record's writers across processes;
one unread model; the identity rules). §1, §4 and §6 to §9 are presentation and order. Fable, if it reads, is not shown Codex's
read first (D191 point 1).

**Fable, 2026-10-21, on §2 to §3 and §5 of round 2** (D191's note: a decided rule others rest on, a durable record written by two
processes, a service mark), not shown Codex's read first. Every finding was checked against the code at `3d3dd47de` before it was
taken (D160's step 9), and the steward settled each. All fourteen hold, and each is taken in round 3. (B) marks what the read
called blocking, (N) what it did not. *Taken in* names round 3's rows.

| # | Finding | Checked at | Verdict | Taken in |
|---|---|---|---|---|
| F1.1 | (B) With the focused window's view closed, nothing persistent says a terminal's press waits: the toast lasts seven seconds and may be dropped from the kit's pump, the bell's count looks like any other row's, and the taskbar's flash is only for a window behind; so the incident of 2026-10-21 can recur, and a service confirmation is exposed the same way | `ui.tsx:1766`, round 2's §3.3 | holds, taken: while any press waits unshown, the bell wears the warning hue (D185's `--warn`) with the presses' own count, and the press's toast stays until the view opens or the press ends | §2.1, §3.3, §3.4, §5, §6 (R2), §8; 2b and its proof |
| F1.2 | (N) A late Confirm's refusal cannot be drawn on its card: `End` removes the press before it settles, and the card's `setSaid` runs unmounted. The clock check for a shown word goes before the early return for a card already shown | `PipeConfirmations.cs:177`, `:284-292`; `Confirmations.tsx:142-153` | holds, taken: the refusal is toasted, as R3 allows where the place has gone; `Show` reads the clock first | §2.3, §3.4, §6 (R3), §8; 2a and its proof |
| F1.3 | (N) *Shown by filling the area* can start the two minutes with *Refuse* and *Confirm* out of view in a short panel: require the buttons' row in view in the fill case, or anchor the region's scroll at its foot. *Shown* combines intersection, fold, tab, dock, resize and focus, and a drawer's scrim (D41) counts as not shown | round 2's §3.4 | holds, taken: the buttons' row whole in view where the region fills its area, the scroll left the person's; *shown* one judgement over all six, under a scrim not shown | §3.4, §7, §8; 2b's proof |
| F1.4 | (N) Where repository names collide across workspaces, the wording names the workspace, which the attention rows already carry | `AttentionStateSlice.cs:72` | holds, taken | §3.2, §6 (`person.sessionIn`); 2b's proof |
| F1.5 | (N) A service confirmation can be toasted in two windows, since each polls on its own clock | `person.ts:83` | holds, taken: accepted in words, until 2j's push | §3.3, §7, §10 |
| F2.1 | (N) Setting an unparseable `notices.json` aside is only the locked writer's act; a watch's failed read keeps the last snapshot and waits for the next wake. `Notices` needs its own path, since `SaveHolding` refuses an unreadable file | `JsonKeeping.cs:101-107` | holds, taken | §2.5, §7; 2f and its proof |
| F2.2 | (N) A refused write (the keep lock after 10 s; `SaveHolding` on a twice-written key or a changed shape) is logged and dropped, since the session record keeps what happened; prove it in the record's row | `KeepLock.cs:35`, `JsonKeeping.cs:109-121` | holds, taken | §2.5, §7; 2f's proof |
| F2.3 | (N) The writer in the process pushes the `notices` slice itself after its write, since its own stamp wakes nothing; a missed watcher event is covered by the topic's 30 s deadline | `HomeWatch.cs:161-163`, `:172` | holds, taken | §2.5; 2f and its proof |
| F2.4 | (N) The service's unread count includes the person's own posts and Daoris's lines; the messages row changes the person's entry's count in the service lane it already opens | `Channel.cs:981` | holds, taken: the person's entry counts what an agent or a teammate wrote alone; a session's entry as it is | §2.4, §7, §8 (D167); 2g and its proof |
| F3.1 | (N) With a headless loop driving, a notice another process writes is never an arrival in the desktop; the desktop host's re-read of the record is its arrival source: new ids, bounded, nothing older than the first judgement | round 2's §2.5, §5 | holds, taken | §2.5, §5, §7; 2f and its proof |
| F3.2 | (N) The `answered` notice is recorded before the notify switch's gate, as `Observe` runs before `ticked.Notify` | `DriverLoop.cs:919-932`, `:783-790` | holds, taken | §2.5; 2g and its proof |
| F3.3 | (N) Messages need a watermark per ask in the modules, the way `AttentionWatch.Observe`'s first look is a baseline. A missed arrival is accepted for news, and for a press only because F1.1's signal persists | `Attention.cs:191-211` | holds, taken | §5; 2g and its proof |
| F4.1 | (N) The bell's and the taskbar's counts of the service's presses are the page's until the service's door is pushed: say so, and point it to a row | `person.ts:74-95` | holds, taken: said, and the push is NOTIFY2j | §2.5, §3.4, §5, §10; 2j |
| F4.2 | (N) Split the first view row: a pipe half (the late-answer and late-shown refusal on the held clock, the press's id on `Confirming`, the balloon naming no route) lands first on its own tests with no view; the view row then carries the view, the wording, *shown*, the persistent signal, the toast, the flash and Overview's summary; renumber in build order | round 2's §9 | holds, taken: 2a the pipe's half, with the late refusal's toast so that no refusal goes unsaid before 2b; 2b the view; the rest renamed, the mapping kept | §0.2, §9, §11's mapping |

**The choices this round made**, where two shapes remained: the buttons' row in view where the region fills its area, rather than
the region's scroll anchored at its foot, which would change what a region shows unasked (F1.3); the late refusal's toast in 2a
with the refusal itself, rather than waiting for the view (F1.2, F4.2); a service confirmation said twice across two windows,
accepted, rather than the windows' polls coordinated, since 2j's push ends it (F1.5, F4.1); and the person's unread count passing
over Daoris's lines as well as their own posts, so the count and the view's newest agree (F2.4).

**Settled.** The steward checked each take against this revision: the bell's warning hue with the presses' count and the press's
toast staying while a press waits unshown; a late answer and a late shown word refused, the clock read first, the refusal toasted;
*shown* needing the buttons' row where the region fills its area, judged over every change, never under a scrim; the workspace
named where repositories share a name; the record set aside only by its locked writer, a refused write logged and dropped, the
writer pushing its own write, the deadline behind a missed event; the person's count without their own posts and Daoris's lines;
the desktop host's re-read the arrival of another process's notice; the `answered` notice recorded before the switch; a watermark
per ask; the service's presses the page's count until NOTIFY2j; and the first row split in two. Every finding of both reads is
taken. The design is accepted (D196's note of 2026-10-21); its rows are §9's, NOTIFY2a to 2j.

## 12. For the owner

**What the view is called in Chinese** is settled by the steward's default: 通知, the design's proposal (§6), open to the owner, who
may reopen it as the names of things have been (the glossary's *ask*). The alternative is the 通知中心 the row used; it is avoided
because Windows and macOS give that name to the system's own list, into which Daoris's notifications also go. Either answer changes
only the glossary's term and the words of the view and the button; NOTIFY2b builds 通知 unless the owner says otherwise. Nothing
else here is the owner's under D191 point 3.
