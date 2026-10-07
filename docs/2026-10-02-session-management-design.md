# Sessions that are easy to manage (SESSUX1)

**Carried by:** SESSUX1 in `TASKS.md`. **Status:** design, written before the build, and **D126** records what it
settles. §1 is the audit, read from the code at `c4c9725`; §2 onward is the design. It is built on the frame of
`docs/2026-10-01-frame-model-design.md` (D118), and every name in it follows `docs/2026-10-01-naming-design.md` (D116)
and its glossary.

**What it amends**, each marked where the row that builds it lands:
- the working surface design's §3 (the session list grouped by repository; RAIL1's row menu holding only what has
  no other home) and §4 (the parked session's three moves, whose stop moves to the header);
- the platform language's §3, which gains that open's hue is for what waits on the person alone, so `queued` leaves
  it, and its §5 *Sessions*;
- the planner's reading of D80 and D104: a person's stop now holds its quest on this machine, open or taken, until
  *Try again*;
- RETRY1's *Try again*, which also releases that hold;
- D110's coverage, which reads Sessions' acts as it reads Settings' domains.

**Read with D125** (`docs/2026-10-01-account-rotation-design.md`, TOOL4), designed beside this one and not yet on
main when this was written. Its *waits for an account* is one of the states below, and the row that shows it waits on
TOOL4c and TOOL4d.

> *"there is no way to easily managed sessions in daoris right now and it's not really smooth for ui/ux"* — the
> owner, 2026-10-02.

Today a session is attended by pressing its row, and nearly everything else a person does to one happens somewhere
else: a driven session is stopped from its quest's page, a quest parked on its failed sessions is started again from
its quest's page, a parked session is answered at the foot of its conversation, and nothing that ended can be cleared.
On 1 October the owner's work stood exactly there. An account limit refused a driven session, the driver carried the
quest on twice, each carry-on was refused at once, and the quest parked within seconds. Its sessions read *failed*
among the ended, and the one press that moved it, *Try again*, was on another view under a fact called *Sitting*.

This design puts every act where its session is, lists sessions by what they need from the person, lets the person
clear what ended, gives a list a title that fits, and gives each new act its terminal twin and its Ask Daoris door.

## 1. The audit: what a person can do with a session today

### 1.1 What was read

The web: `work/SessionRow.tsx`, `work/SessionRail.tsx`, `work/rail.ts`, `work/SessionHead.tsx`,
`work/AttendedSession.tsx`, `work/AwaitingPerson.tsx`, `work/RunningIntake.tsx`, `work/AwaitingIntake.tsx`,
`work/Composer.tsx`, `work/WorkFrame.tsx`, `work/identity.ts`, `work/attention.ts`, `work/TerminalView.tsx`,
`quests/QuestPage.tsx`, `QuestsView.tsx`, `OverviewView.tsx`, `App.tsx`, `commands.ts`, `ui.tsx`, `bridge/sessions.ts`,
`bridge/driver.ts`, the `sessionState`, `work.rail`, `work.awaiting`, `work.composer`, `work.review` and `quests`
catalogues in both languages, and `locales/glossary.json`.

The driver: `Planner.cs`, `Observation.cs`, `Attention.cs`, `ServiceClient.cs` (`ReadStrikes`), `SessionEvents.cs`,
`SessionTrees.cs`, `Orphans.cs`, `DriverConfig.cs` (`forgiven`), `DriverCommand.cs`, the headless host's `Program.cs`,
and Ask Daoris's kinds (`Help/Proposals/*`) and room (`Help/Room/HelpRoomDoors.cs`, `HelpRoomMachineNow.cs`). The
modules: `DriverModule.Sessions.cs`, `DriverModule.Driver.cs` (`RETRY_QUEST`), `DriverModule.Lines.cs` (the clean-up),
`DriverModule.Trees.cs`. The service: `Sessions.cs`, `SessionLedger.cs`, and the HTTP host's session routes. The CLI:
`cli/driver.ts`.

### 1.2 State by state

What decides each state, where a person sees it today, how it reads, and what is missing. *The rail* is the session
list (the glossary's *session list*); *the head* is the attended session's record at the top of the main area.

| State | What decides it | Where a person sees it today | Word, mark, pill today | What is missing |
|---|---|---|---|---|
| **Queued** | the record's `queued`: waiting on its repository's slot, a hold or its tree (`Sessions.cs`) | the rail's live group under its repository; the head; the quest page's session section | *queued* 排队中; the idle mark; the pill in **open's hue** (`ui.tsx:198`) | open's hue is the one every other screen keeps for *waiting on you* (platform language §3); here only the word tells a queued session from one waiting on the person |
| **Starting, working**, and *idle* (a chat between turns, UX5 U17) | the record, and for a chat the driver's turns | the rail; the head; the strip's mark; the monitor's tile; the status bar's count | *working* 工作中, the live mark, taken's blue; *idle* 空闲, quiet | **a running driven session has no stop in Sessions.** The steer composer's endings are off (`WorkFrame.tsx:895–917`), the head's stop is passed only for an intake (`WorkFrame.tsx:820`), and the one stop is on the quest's page (`QuestPage.tsx:354`) |
| **Awaiting the person** | `awaiting-person`: a driven session that took its quest and ended its turn to ask (STANDDOWN2, `Observation.cs:53–60`), a parked chat, an intake that asked | the rail, first within its repository's group (`rail.ts:11`); the head's card with its moves; Overview's *What needs you* (`attention.ts:104`); both badges; the OS notification, once (`Attention.cs`); the room's count | *waiting on you* 等你处理; the parked mark and the pill in open's hue | none of its moves is on its row. The groups go by repository name, so a session waiting on the person sits below every repository with live work whose name comes before its own |
| **Waiting on another repository** (D79) | not a session state: the asking session concludes `completed` (`Observation.cs:37–41`), and its quest stays taken with `awaits` | the quest page's *Waits on* fact with a door (`QuestPage.tsx:179`); the quest list | in Sessions, an ended row reading **completed in done's green** | the row says the work is done while its quest waits on an answer |
| **Parked on its failed sessions** (DRV6) | not a session state: the planner's `Exhausted` verdict (`Planner.cs:195`), from the failed records counted (`ServiceClient.cs:655–672`) less RETRY1's mark (`forgiven`) | the quest page's *Sitting* fact with *Try again* (`QuestPage.tsx:206–224`); Overview's *Outstanding*, as the driver's sentence, when the quest is among the six oldest; the room's parked list; the tick's line | in Sessions, its last session reads *failed* among the ended | **nothing in Sessions says the quest is parked or offers *Try again*.** It is not in *What needs you* (`attention.ts:167–173`), no badge counts it, and nothing says it once: the attention kinds are a park and an end (`Attention.cs:4–10`). This is where the owner's work stood on 1 October |
| **Waiting for an account** (D125) | does not exist today: a refused turn for an account's limit is `failed`, a strike, and the quest parks as above | — | — | D125 makes it a hold (`Blocked`, *waits for an account*) and puts `limit` on the record (TOOL4c, TOOL4d). The list must show it once built |
| **Stopped** | `stopped`: the person's stop, or with `Interrupted` the orphan sweep's or a shutdown's (D104) | the rail's *Ended*; the head's note; the quest page | *stopped* 已停止, neutral | **after a person's stop the quest has no way back.** One the session had taken stays taken, and the planner never considers it again, so no sentence says why it sits (`Planner.cs:315–328`). One the session had not yet taken is open, and nothing in the planner reads a person's stop of it, so the next look plans it again: read from the code, not seen on the window |
| **Failed** | `failed` | the rail's *Ended*; the head's note | *failed* 失败, declined's red, the ended mark | counted as a strike and carried on by D80 while the strikes allow, which the row does not say |
| **Completed** | `completed` | the rail's *Ended*; the head | *completed* 已完成, done's green | — |
| **Landed** (D113) | not a state: where a completed session's work landed (`landings.json`, LOOK2b) | the row's line, *landed on `<branch>`* (`SessionRow.tsx:115`); the head's line (`SessionHead.tsx:196–210`); the review's note | a fact on the row | landed work is said on the row, and **work that did not land is not**: commits no branch of the person's holds are said on the head (`SessionHead.tsx:199, 205`) and in Settings → Workspace's clean-up list only |
| **Stood down** | `stood-down` | the rail's *Ended* | *stood down* 已让位, neutral | — |
| **Declined** | `declined` | the rail's *Ended* | *declined* 已谢绝, declined's red | — |

Two facts cut across every row:
- **The rail lists the twelve newest that ended and counts the rest** (`rail.ts:5`; `SessionRail.tsx:242–246`). An
  older session is reached only by searching.
- **Nothing removes a session record.** The service has no delete for one (`Sessions.cs`; the service's session
  routes, `Daoris.Service.Http/Program.cs:656–794`), and the page asks for every record, closed ones included, on
  each change (`SessionRail.tsx:62`). The list and every ask grow with every session ever run.

### 1.3 Act by act

Where each act is today, its terminal twin (D50) and its Ask Daoris door (D110).

| Act | Where it is today | Terminal | Ask Daoris | What is missing |
|---|---|---|---|---|
| **Attend** | the row; the strip's mark; *What needs you*'s parked row; the chain strip and *Who it worked with*; the quest page's *Open in Sessions* | nothing lists sessions: `daoris-driver` has no `sessions` verb (`DriverCommand.cs`) | a go to the Sessions view, never to a session (FRAME1i owes the item) | a terminal listing |
| **Answer** | a parked driven session at the box at the foot (`WorkFrame.tsx:295`) or the card's *Answer and carry on…*; a parked chat at its composer | `daoris-driver answer <session> ["…"]` | none: the room names the screen and the command (`HelpRoomDoors.cs:88`) | not on the row |
| **Finish, decline** a parked session | the card (`AwaitingPerson.tsx`) | none | none | a terminal door |
| **Stop** | five places with two words: a chat's composer, *Stop* (`Composer.tsx:380`); the parked card, *Stop session*; the running and the parked intake's cards, *Stop session*; the quest page's session section, *Stop session* (`QuestPage.tsx:354`). A running driven session in Sessions: none | none for a session the window runs; `daoris-driver chat`'s own Ctrl+C for its own | none | the stop for the most common case is on another view; one verb has five owners (D56) and two words (D116); no terminal; no Ask Daoris |
| **Try again, carry on** | *Try again* on the quest's page under *Sitting*, for a quest parked on its failed sessions only (RETRY1) | `daoris driver retry <quest> [--at <n>]` | the `setting` kind's `retry` door (HELP10) | not where the session is. A quest a person's stop left has no act at all. The room's row still says *Quests → the quest's drawer → Try again* (`HelpRoomDoors.cs:52`), a drawer FRAME1d made a page |
| **Land, send back, discard, hand off** | the side bar's review (`DiffPane.tsx`), reached from the row's ⋯ *Review*, the head's *Review* beside unlanded work, and the palette | `daoris-driver trees land\|remove\|hand` | the `hand` kind; landing has none | outside this design (§11) |
| **Review** | the row's ⋯ (`SessionRow.tsx:131`); the head, beside unlanded work only; the palette (`commands.ts`) | the review is read on the screen; `trees land --plan` prints a landing | — | — |
| **Open its tree or branch** | the panel's terminal, shown with none open, opens one in the attended session's tree (`TerminalView.tsx:45`); a file opens in the preview; the head's line names the branch | `daoris-driver trees list` | — | no press opens the folder a session worked in, or a terminal there |
| **Find** | the rail's search, by name at once and by what was said once the typing settles (RAIL1, `SessionRail.tsx:161–198`) | none | none | a terminal listing |
| **Filter** | none: grouped by repository only (`SessionRail.tsx:111–123`), and Sessions' list pane has no ⋯ (`WorkFrame.tsx:731–756`) | none | — | a view by state |
| **Clear or archive what ended** | none. Settings → Workspace's clean-up removes landed trees and branches (D88), never a record | `daoris-driver trees clean` (trees and branches) | — | everything |
| **Delete** | none for a session (a quest and an ask have *Delete…*, D95) | none | — | a delete for a record that holds nobody's work |
| **Own window, copy ID** | the row's ⋯ (RAIL1) | — | — | — |

### 1.4 What the audit found

| # | Finding | Where |
|---|---|---|
| M1 | A running driven session cannot be stopped from Sessions; its stop is on its quest's page | `WorkFrame.tsx:820, 895–917`; `QuestPage.tsx:354` |
| M2 | A quest parked on its failed sessions is not where its sessions are: no word on the row, no *Try again* beside it, nothing in *What needs you*, no badge, no notification | `QuestPage.tsx:218`; `attention.ts:69–173`; `Attention.cs:4–10` |
| M3 | A person's stop leaves a taken quest with no way back and no sentence, and an open one is planned again at the next look | `Planner.cs:315–328` |
| M4 | The session stop has five owners and two words | `Composer.tsx:380`; `AwaitingPerson.tsx`; `RunningIntake.tsx:46`; `AwaitingIntake.tsx:59`; `QuestPage.tsx:354` |
| M5 | A parked session's moves are on its page only, and finishing or declining one has no terminal door | `AwaitingPerson.tsx`; `DriverCommand.cs` |
| M6 | `queued` wears the waiting hue beside *waiting on you* | `ui.tsx:198` |
| M7 | A session waiting on another repository reads *completed* | `Observation.cs:37–41` |
| M8 | The list has no view by state: what waits on the person is found inside its repository's group | `SessionRail.tsx:111–123` |
| M9 | Nothing that ended can be cleared, older ones are reached only by search, and the records grow without bound | `rail.ts:5`; `Sessions.cs` |
| M10 | A driven session's title is its quest's title, cut at the list's width wherever a list has one line for it | `identity.ts:29` |
| M11 | Only *answer* and *retry* have a terminal door, only *retry* an Ask Daoris door, and D110's coverage does not read Sessions | `DriverCommand.cs`; `HelpCoverageTests.cs:423` |
| M12 | No press opens a session's folder or a terminal in it | `TerminalView.tsx:45` |
| M13 | The row's menu holds only what has no other home (RAIL1), so managing many sessions means attending each | `SessionRow.tsx:91–97, 129–133` |

## 2. The states a person sees

### 2.1 Five groups, in the order the person acts on them

The list shows sessions **by state** (§4), in five groups. A session is in the first group it qualifies for, in this
order. A group with no session is absent, and each heading counts its rows.

| Group | Holds | Why here |
|---|---|---|
| 1. **Waiting on you** 等你处理 | *waiting on you* (`awaiting-person`); *parked*: the last session here of a quest parked on its failed sessions | only the person's press moves it |
| 2. **To review** 待审阅 | an ended session whose own tree holds work no branch of the person's holds: commits, by D88's proof, or uncommitted changes | the person verifies the outcome (D37), and nothing else will |
| 3. **Working** 工作中 | *queued*, *starting*, *working*, *idle* | it is moving, and the person may watch it |
| 4. **Resumes later** 稍后继续 | *awaiting reply* (D79); *account cooling* (D125) | it moves by itself when what it waits on arrives |
| 5. **Ended** 已结束 | *completed* (landed or not), *declined*, *failed* where the quest is not parked, *stopped*, *stood down* | a record |
| **Archived** 已归档 | archived sessions, only while *Show archived* is ticked (§5) | out of the way, kept whole |

**Within a group:** *Waiting on you* and *Resumes later* go oldest first, the longest wait first, as *What needs you*
orders its rows (`attention.ts:167`). *To review* and *Working* go by repository name, then by when each started, so
rows do not reshuffle as their states move (the rail's existing reason). *Ended* goes newest first.

**Why To review is its own group, and not in *What needs you*.** SURF5 left finished-and-unreviewed work out because
nothing recorded that anybody looked. Unlanded commits in a session's own tree are the fact it lacked: work nobody has
accepted yet, read by D88's proof.
Overview's band stays without it: the proof is a git walk per repository, which Sessions already asks for and
Overview should not (§11).

### 2.2 Each row's word, mark and pill

A row shows its mark and word (`Dot`); the page header shows the same word on its pill. The words below that are not
a record state are the page's derived words, decided by one reader (§2.4).

| Shown state | When | Group | Word, en / zh | Mark | Pill | The row's second line adds |
|---|---|---|---|---|---|---|
| waiting on you | `awaiting-person` | 1 | waiting on you / 等你处理 | parked | open | as today |
| **parked** | a quest's last session here, failed or cut off, and the quest parked on its failed sessions | 1 | parked / 已挂起 | parked | open | *after 3 failed sessions* |
| any ended, with work | ended, and its own tree holds unlanded or uncommitted work | 2 | its own state's word | ended | its own | *3 commits to review*, or *uncommitted changes* |
| queued | `queued` | 3 | queued / 排队中 | idle | **neutral** (was open) | |
| starting, working | as today | 3 | as today | live | taken | |
| idle | a live chat between turns (UX5 U17) | 3 | idle / 空闲 | idle | neutral | |
| **awaiting reply** | a quest's last session here, `completed`, and the quest taken and waiting on an open quest | 4 | awaiting reply / 等回复 | idle | neutral | *waits on `#q`, asked of `<repository>`* |
| **account cooling** | a quest's last session here, failed with D125's `limit`, and the quest held by its account's cool-off | 4 | account cooling / 账户冷却中 | idle | neutral | *until 16:02 (<zone>)*, D125's time |
| completed | `completed` | 5 | completed / 已完成 | ended | done | *landed on `<branch>`* where it landed (LOOK2b) |
| declined | `declined` | 5 | declined / 已谢绝 | ended | declined | |
| failed | `failed`, its quest not parked | 5 | failed / 失败 | ended | declined | |
| stopped | `stopped` | 5 | stopped / 已停止 | ended | neutral | *held here until you try again*, where its quest is held by this stop (§3.3) |
| stood down | `stood-down` | 5 | stood down / 已让位 | ended | neutral | |

### 2.3 The hue rules

- **Open's hue means it waits on you.** *Waiting on you* and *parked* wear it, and nothing else in the list does.
  `queued` moves to neutral, keeping its idle mark. The code let `queued` share the hue and left the word to tell
  them apart (`ui.tsx:193–198`); *the platform language's §3 gains the rule* that open's hue is the person's alone.
- **Red is an outcome**: failed and declined. A parked quest's last session wears open's hue on its row, since what
  the person meets there is the park; the failure's sentence leads its page.
- **Never hue alone** (D41 §6): each mark has its word (`Dot`), each strip mark its name and tip (`StripMark`), each
  group its heading.
- **No new token.** The palette is D41's validated set; *awaiting reply* and *account cooling* are neutral, since
  nothing runs and nothing waits on the person.

### 2.4 One reader decides a session's group

`SessionGroups.Read`, in the driver library, decides each session's group and shown state from:
- the service's records and quests;
- the planner's own verdict for each quest, never a second copy of the strikes rule: the loop's last look where a
  loop runs, and the planner run over a fresh snapshot where none does (the terminal);
- the trees' judgement of each ended session's own tree, by D88's proof (`SessionTrees`);
- D125's `limit` on the record and its cool-off, once TOOL4c and TOOL4d build them;
- the archive marks (§5.2).

It answers per session: its group, its shown state, the facts its second line says (the strikes, the awaited quest,
the commits to review, the cool-off's time, whether its stop holds its quest), whether it is archived, and whether it
may be deleted (§5.4). `SESSION_GROUPS` hands it to the page and `daoris-driver sessions` prints it, so the screen and
the terminal cannot disagree; one table of cases holds it.

The page keeps one reading of its own: *idle*, from the driver's turns (UX5 U17), since the record says `working`
for a live chat's whole life. Sessions is shell-only (`commands.ts:18`), so none of this reaches a browser.

**Why one reader in the driver, and not a helper on the page.** The page's helpers are the usual home of a
derivation (`needsAPerson`, `sessionTitle`). This one needs the planner's verdict and a git judgement per tree, which
only the driver has, and its terminal twin must print the same groups. A helper in each would be twins of a rule with
eleven cases.

### 2.5 The strip and the badges

- **The strip** (the list closed) holds groups 1 and 3 in the open list's order: what waits on the person first,
  then what runs. It held each running session; a parked quest's session is not running and needs the person, so it
  joins.
- **Sessions' badge counts group 1** (`waitingInSessions`, `attention.ts:23`, which counted `awaiting-person` alone).
- **Overview's band gains the parked quest** (§4.6), so its count rises with it. Both counts read the same two facts.

## 3. Every act where its session is

### 3.1 The acts

Each act is offered where it applies and absent where it does not, never disabled (D119 §3.2). The row's ⋯ and the
page header call one module, `work/sessionActs.ts`, so an act has one implementation and one owner whichever door
pressed it.

| Act | Offered for | Page header | Row ⋯ | List ⋯ | Refuses, and why |
|---|---|---|---|---|---|
| **Answer…** | *waiting on you*, this machine's, not an intake | none: the card under the header keeps it, as today | *Answer…*: attends, then opens the box at the foot with the focus | — | the ledger's own: not parked; an intake is answered through its ask |
| **Finish, Decline…** | *waiting on you*, not an intake | none: the card keeps them | — | — | the ledger's own |
| **Stop…** | live (*queued*, *starting*, *working*, *idle*, *waiting on you*), this machine's | *Stop…*, danger; asks once under the header (§3.3) | *Stop…*: attends, then opens the same ask | — | a teammate's record (its process is on their machine); one already ended (an answer, said); one another Daoris process here runs (`STOP_SESSION`'s *elsewhere*, named) |
| **Try again** | *parked*; *stopped* where its stop holds its quest (§3.4) | *Try again*, primary | *Try again* | — | the quest is closed; it is neither parked nor held here; a teammate's record |
| **Review** | a session with work to read: its own tree, or its landed branch (D113) | *Review*, primary in *To review* | *Review* (as today) | — | none: it opens the side bar's review |
| **Open folder** | the folder it worked in is on this machine: its own tree, or the repository's checkout | ⋯ | ⋯ | — | `SESSION_FOLDER_GONE`: a tidy or the clean-up took the tree |
| **Open a terminal here** | the same | ⋯ | ⋯ | — | the same |
| **Open in its own window** | this machine's | ⋯ | ⋯ (as today) | — | — |
| **Archive** | ended, and in neither *Waiting on you* nor *To review* | ⋯ | *Archive* | *Archive what ended…* (§5.3) | `SESSION_LIVE`; `SESSION_NEEDS_YOU` |
| **Unarchive** | archived | ⋯ | *Unarchive* | — | an information-class answer when it is not archived (D48 §6) |
| **Delete…** | §5.4's sessions only | ⋯; asks once | *Delete…*: attends, then asks | — | §5.4's refusals |
| **Copy session ID** | always | ⋯ | ⋯ (as today) | — | — |
| **Group by**, **Show archived** | — | — | — | ✓ (§4.1) | — |

**This amends RAIL1's rule** (the working surface design's §3 note) that a row's menu holds only what has no other
home. A list is where many sessions are managed, and a rule that sends the person into each session to act on it is
the friction the owner named. D56's one owner holds: the module is the owner, and each door calls it.

### 3.2 The session's page header

The main area of Sessions gains a page header in FRAME1c's `ViewMain`, as every view's chosen item has (D118 §3b),
with one difference: **it is pinned** at the top of the main area while the page scrolls under it. A session's page is
a conversation that runs to thousands of events (SESS1 looked at runs of 1,855), and its acts must stay in reach
there. The long run's toolbar (SESS1 S9) pins beneath it.

- **On the left**: the shown state's mark and word; the short title (§6), one line, whole in its tip; the session's
  id in the meta face beneath.
- **On the right**: the primary act where the state has one (*Try again* while parked or held; *Review* in *To
  review*), then *Stop…* while live, then ⋯ with the rest in §3.1's order.
- **Below it, scrolling**: the record head as today, now opening on the quest's whole title (SESS2's two lines, whole
  on its tip), then the cards, the record's sentence, what its tree left and the meta line. The head's pill and id move
  up to the header; nothing is said twice.
- **A teammate's record** (SYNC4) is read here, and its header offers *Copy session ID* alone: nothing this window
  sends could reach its process.
- **A narrow main area**: below 560 px the acts take their own line under the title, as the plugin page's do
  (D119 §3.6).
- **With nothing attended**, the main area is today's *Nothing attended*, with no header.

### 3.3 Stop

**One owner, the header** (D56), with one word, *Stop…*, then *Stop session* to mean it.
- A chat's composer keeps *Finish*, the chat's own ending (D49), and *Stop turn* (CONV4b). Its session *Stop* goes.
- The parked card keeps *Answer and carry on…*, *Finish* and *Decline…*. Its *Stop session* goes.
- The intake cards keep their door to the ask. Their stop goes to the header, with the sentence they say today.
- The quest page's session section keeps the session's record and its door *Open in Sessions*. Its *Stop session*
  goes. The quest page keeps the quest's own acts (§3.6).
- **One exception, Ask Daoris's panel** (`help/AskConversation.tsx`), which keeps its own stop. That conversation is
  held in the side bar on every view (D89, DOCK1a), so the panel is its page wherever the person is; in Sessions its
  header offers *Stop…* as a chat's does, and both call the one route.

**It asks once**, under the header, since it ends work in flight (the platform language's §4: a destructive edit asks
once). The ask says what follows, by what the session is. The moves are *Stop session* (danger) and *Never mind*.

| The session | The ask says | After the stop |
|---|---|---|
| driven, holding its quest | *Stops the session now. Its quest stays taken here, its tree keeps what it wrote, and the driver will not carry it on until you choose Try again.* | the quest sits, held by the stop |
| driven, before taking its quest | *Stops the session now. The driver will not start `#q` again on this machine until you choose Try again; another machine may still take it.* | the quest sits, held by the stop |
| waiting on you, with no process left (STANDDOWN2) | *Stops it unanswered. Its quest stays taken here until you choose Try again.* | the record ends `stopped` through `RESOLVE_SESSION`; the quest sits |
| a chat | *Ends this conversation; it takes no more messages. Finish lets its agent wind up instead.* | ends `stopped` |
| an intake, running or parked | today's sentence: *Stopping ends the intake without answering: the ask stays a proposal for you to publish or close.* | as today |

**Which route it calls is today's**: a session waiting on you is stopped through `RESOLVE_SESSION`'s `stopped`, as the
card's stop was, since that route lets a parked process go before the record ends; every other live session through
`STOP_SESSION`, whose three answers (stopped, an orphan ended, run by another Daoris process here) are said as
`stopNotice` says them now.

**A person's stop holds its quest on this machine.** The planner gives a quest whose last session here is the
person's stop (`stopped`, not interrupted) a verdict of its own, `Stopped`, whether the quest is open or taken. Its
sentence is *you stopped session `<id>`; Try again carries it on* (taken) or *… starts it again* (open). It sits as any
held quest sits: under *Sitting* on its page, on the tick's line and in the room.
- **Why**: D104 already said a person's stop *"is their decision, and is never carried on"*, and the planner honoured
  it for a taken quest by never looking at it again, which left no sentence and no way back (M3). For an open quest it
  did not honour it at all. A stop that undoes itself at the next look is not a stop a person can manage with.
- **What does not change**: a stop is not a strike (D58); another machine may still take an open quest; an
  interrupted stop is carried on as D104 says; the quest's own acts (*Mark done*, *Decline…*) stay on its page.

### 3.4 Try again: one act, two holds

*Try again* (重试) starts a quest's work again on this machine. It is offered on the session that holds the quest, its
last session here, and on the quest's page: one act, one word, one route.

- **A quest parked on its failed sessions**: RETRY1's mark, `forgiven`, counted from where it stands, as today.
- **A quest held by the person's stop**: a release. `driver.json` gains `released`, the quest's id against the
  stopped session's. The next look then carries a taken quest on in its tree (D80, its instruction saying the person
  stopped it and released it) or plans an open one. A later stop holds it again, since its session differs.
- **`RETRY_QUEST` and `daoris driver retry <quest>`** do whichever applies and say which. Where neither applies they
  refuse: *`#q` is neither parked nor held by a stop on this machine*.
- `released` is a field of a twin file: `DriverConfig.cs` and `driverconfig.ts` read and write it, each with its table
  (`.claude/knowledge/twins.md`).
- **Ask Daoris**: the `setting` kind's `retry` door, judged against the parked and the held, which the facts carry.

### 3.5 Open folder, and a terminal there

- **Open folder** opens the folder the session worked in, its own tree or the repository's checkout, in the system's
  file manager through the window kit's launcher, as the machine log's folder opens (`SESSION_OPEN_FOLDER`). The page
  never prints the path (the platform language's §4); it reads that the folder is here.
- **Open a terminal here** opens the panel's terminal (CONSOLE4, D96) with that folder as its working directory, and
  shows the panel if it is hidden. Today a terminal opens there only when the terminal view is shown with none open
  (`TerminalView.tsx:45`); this makes it a press.

### 3.6 What stays on the quest's page

The quest's own acts (*Take*, *Mark done*, *Decline…*, *Delete…*), *Try again* under *Sitting*, the session's record
and its door *Open in Sessions*. A quest is decided on its page, and its sessions are managed on theirs.

## 4. A view by state, What needs you first

### 4.1 The list's ⋯

Sessions' list pane gains its ⋯ (FRAME1d's `ListMore`), which it has never had:
- **Group by**: *State*, the default, or *Repository*, today's arrangement; ticked as a choice of two.
- **Show archived**, ticked as it stands.
- A rule, then **Archive what ended…** (§5.3).

What is chosen is remembered in `daoris.list.sessions.filters` as `{ "group": "state" | "repository", "archived":
boolean }` (D118 §3f). A first open with nothing remembered is by state.

### 4.2 By state

§2.1's groups, each heading counted. A row's second line says its repository, since no group header does. The group
headers carry no repository facts (*drives here*, *held*, *busy*): those belong to a repository, and stay on the view
by repository and on Repositories' page.

### 4.3 By repository

Today's arrangement: a group per repository with its facts, *waiting on you* first within it, and *Ended* beneath. It
gains every word and act above, and *parked* sorts with *waiting on you*.

### 4.4 Ended

Newest first, the first twelve, then **Show N more**, a press that shows the rest. Today the remainder is only counted
(`work.rail.endedMore`), and an older session is reached by search alone (M9).

### 4.5 Search

As RAIL1 built it, over everything the list holds, archived sessions included and marked *archived*: a search is how
one is found without the tick.

### 4.6 What needs you, on Overview

**The band gains a `parked-quest` row**, after the parked sessions. Its title is the quest's short title (§6); its place
the repository; its time when its last session failed; its detail the driver's own sitting sentence. Its door opens
the quest's page, where *Try again* is, and from there its session. It appears only where a driver answers
(`useConsidered`), so a browser has none, as with a trust hold's row. Overview's badge counts the band, as now.

### 4.7 Said once when a quest parks

**A third attention kind, `QuestParked`**, when a quest first parks on its failed sessions here:
*<repository> — `#q` parked after 3 failed sessions*, with the last failure's note. The same switch governs it as a
session's park (*Notify me when a session parks*), and it is said once, as a park is, never again on a relaunch. It is
never said for a hold the person caused: a stop. On 1 October it would have told the owner the moment the quest parked.

## 5. Clearing and archiving what ended

### 5.1 What a session leaves on this machine

| What | Where | Archive | Delete (§5.4 only) |
|---|---|---|---|
| The record | the service's `sessions` table; a remote's copy once pushed (SYNC4) | kept, unchanged | removed |
| Its conversation | `<home>/sessions/<id>.events.jsonl` (D76) | kept | removed |
| Its transcript | `<home>/sessions/<id>.log` | kept | removed |
| A conversation's files | `<home>/sessions/<id>/files/` (CONV4c) | kept | removed |
| Its tree and branch | under `<home>/trees/`, and `daoris/…` in the checkout (D51) | untouched | refused while the tree is here |
| A landing's record | `landings.json` (D102) | untouched | refused while it names the session |
| Its usage | one entry in `usage.json` (TOOL3) | kept | kept: a count the account's totals are made of, with no words |
| The machine log's lines | under the home's log folder (D94) | kept | kept: the log is append-only; `session.deleted` is added |
| A draft in its composer | the viewer's own storage (`work/drafts.ts`) | kept | cleared |
| The console's ring | memory, for this run | — | dropped |
| Its archive mark | `<home>/sessions/archived.json` | written | removed |

### 5.2 Archive

- **An archive mark is this machine's, never the record's.** It is a file under the home, `sessions/archived.json`,
  holding each archived id and when, written atomically by the driver library (`SessionArchive`) at both doors. The
  record is unchanged and travels as before. A teammate's record may be archived here too.
- **Why not a field on the record**: the record is the service's and travels (D47 §4). What one person keeps in their
  list is not a fact about the work.
- **A missing or unreadable file is nothing archived**: a mark lost costs a row back in the list, never a row gone.
- **What it refuses**: a live session (`SESSION_LIVE`: *it is still running; stop it first*); one in *Waiting on you*
  or *To review* (`SESSION_NEEDS_YOU`, naming which). **Archive never hides what needs the person.**
- **Unarchive** brings the session back to the group its state puts it in.

### 5.3 Archive what ended

**Listed first, then pressed**, as the clean-up is (D88).
1. The first press says, under the list's header, what it would take and what it keeps: *Archives 14 sessions that
   ended. Kept in the list: 2 waiting on you, 1 to review.* The moves are *Archive 14* and *Never mind*.
2. The second press archives the sessions the first listed, each judged again as it goes, since a list is a fact
   about a moment.

It takes every session in *Ended* that is not archived. The toast says where they went: *Show archived*, in the list's
⋯, brings them back. At a terminal, `sessions archive --ended` lists and `--yes` archives.

### 5.4 Delete

**What may be deleted: a session that served no quest**, which is a chat that took none (its record's `quest` empty and
`took` false) or an Ask Daoris conversation, and which also:
- has ended, and is this machine's own;
- is named by nothing: no ask's intake, no quest's `publishedBy`, no landing;
- has no tree on this machine (a chat with a tree of its own: discard the tree, or let the clean-up take it, first);
- is held by no remote (a record pushed to a wired workspace's remote: a delete here would leave the team's copy, and
  a session record does not travel as a deletion).

**Why so narrow.** A session that served a quest is that work's record: which agent, which version, which account and
which tree did it (D49 §4). The quest's strikes and its carry-ons are read from those records (D58, D80), so deleting
one would make the quest's history and its derived count disagree. Archive is how such a session is cleared.

**It asks once**: *Deletes this conversation's record and what this machine kept of it: its words, its transcript and
its files. Nothing brings it back.* The moves are *Delete session* (danger) and *Never mind*.

**The refusals**, each a code in `Refusals`, an entry in both catalogues and a throw site:

| Code | Says |
|---|---|
| `SESSION_LIVE` | *it is still running; stop it first* |
| `SESSION_NOT_OURS` | *it ran on `<machine>`; its record is theirs* |
| `SESSION_SERVED_QUEST` | *it worked on `#q`, and its record is that work's; archive it instead* |
| `SESSION_NAMED` | *ask `#a` names it as its intake* · *`#q` was published by it* · *a landing names it* |
| `SESSION_TREE_HERE` | *its tree is still on this machine; discard the tree or clean it up first* |
| `SESSION_ON_REMOTE` | *the remote for `<workspace>` holds it; a delete here would not reach the team's copy, so archive it instead* |

**Where each half is judged.** The service gains `DELETE /api/sessions/{id}`, on a local host only (a shared
deployment has none, as D95's quest delete has none), and its ledger judges the record's half: its own, ended, served
no quest, named by no quest or ask, not pushed. The driver judges the machine's half first (the tree, the landing),
calls the service, and removes the files after the service's yes. `SESSION_GROUPS` answers `deletable` per session,
so the page offers *Delete…* only where it would be taken (D95's way).

### 5.5 What is never touched

A branch. A tree with any work in it: the clean-up and *Discard tree* keep their proof (D88, D51 rule 7). A teammate's
record, which may only be archived. The machine log. The usage counts. And **nothing archives or deletes by itself**:
there is no rule by age (§11).

## 6. Titles that fit

### 6.1 A quest carries a short title

**`short`**, optional, at most 40 characters on one line, **written by whoever publishes the quest**: the intake
session, a session asking another repository (D79), a chain step's `then`, the person in the quest composer.
- The service keeps it with the quest, and it travels in the publish operation (D68), so every machine shows the same
  words. An older host ignores it; an older quest has none.
- **The words are the publisher's, never Daoris's**: content, never translated, never made by cutting the title. A
  cut at the list's width is what the row already does, and it keeps the start, which is where quests in one family
  tend to say the same thing.
- **Agents are told**, in the intake's prompt, in the driven instruction's paragraph on publishing, and in
  `quest_publish`'s description: *give a short title, the few words that tell this quest apart in a list: at most 40
  characters, about 20 in Chinese.*
- The service refuses one longer than 40 characters or with a line break, in its own words.

### 6.2 Where each is shown

| Place | Shows |
|---|---|
| A session's row, its strip mark's name and tip, its page header | the quest's short title, else its title; else today's `sessionTitle`: a chat's opening line, *Ask Daoris*, *Intake for ask #a* |
| The record head, under the header | the quest's whole title (SESS2's two lines, whole on its tip) |
| Quests' list rows, the chain strip, *What needs you*, the monitor's tiles | the short title, else the title |
| The quest's page | the title as its header, and the short title on its id line |

`questName(quest)` answers the short title, else the title; `sessionTitle` calls it. One helper each, so no two
places name one quest differently.

### 6.3 Not naming sessions by hand

D52 stands: a session is named by hand once two real sessions cannot be told apart. The owner's complaint is a title's
length, which the publisher's short title answers at its source.

## 7. The doors

### 7.1 The terminal (D50)

`daoris-driver` gains `sessions`:

```
sessions [--group you|review|working|later|ended|archived] [--repository <name>] [--json]
    this machine's sessions by what they need: waiting on you first, then to review, working,
    resumes later and ended.
sessions stop <id>  ·  sessions finish <id> [--note "…"]  ·  sessions decline <id> --reason "…"
    stop a live session (its quest is then held here until `daoris driver retry`), or finish or
    decline one that waits on you.
sessions archive <id>… | --ended [--yes]  ·  sessions unarchive <id>…  ·  sessions delete <id>
    take ended sessions out of the list, or bring them back; delete a conversation that served no quest.
```

- **The listing** prints each group, *waiting on you* first, a row per session: its id, its word, its short title, its
  repository, how long, and its line. `--json` prints `SESSION_GROUPS`' answer.
- **Stop, finish and decline reach a session another process runs through a request the running loop honours.**
  1. The verb writes `<home>/sessions/requests/<id>.json`, the move and its note, atomically.
  2. Every loop on the home watches that folder. The loop whose own registry runs the session acts on the request as
     its own route does, recording the person's move as the person's, then removes the request.
  3. The verb waits up to 10 seconds for the record to move, and says what happened.
  4. Where nothing on this machine runs the session, the verb ends it as the screen's stop ends an orphan
     (`Orphans.EndAsync`). A parked session with no process left is moved by the ledger directly.
- **`daoris driver retry <quest>`** releases a stop too (§3.4); it stays the CLI's, since `driver.json` is its file too.
- **Exit codes** are the family's: 0 done, 1 refused (a policy answer), 2 could not.
- **Why `daoris-driver`'s**: the verbs read the service's records and the planner's verdicts and touch the home's
  machine-local files. One reader serves the route and the verb, as `plugins show` does (D119 §4.4).

### 7.2 The routes

Every route is `DAORIS.DRIVER`'s, in `DriverModule.Sessions.cs`, each with MOD5's three things: a `[DriverRoute]`
handler, its name in the Desktop README's row, and a call from `bridge/sessions.ts`.

| Route | Status | Payload | Answers |
|---|---|---|---|
| `SESSION_GROUPS` | new | `{ ids? }` | per session: its group, shown state, line facts, archived, deletable (§2.4) |
| `SESSION_ARCHIVE` | new | `{ ids, archived }` | the marks as they now stand, or a refusal (§5.2) |
| `SESSION_DELETE` | new | `{ id }` | deleted, with what was removed and what the disk kept (SESSDEL1), or a refusal (§5.4) |
| `SESSION_OPEN_FOLDER` | new | `{ id }` | opened, or `SESSION_FOLDER_GONE` |
| `STOP_SESSION` | unchanged | `{ id }` | as today, from the header's one owner |
| `RESOLVE_SESSION` | unchanged | `{ id, state, note? }` | as today, from the card and the processless stop |
| `RETRY_QUEST` | extended | `{ quest }` | which it did, `marked` or `released`, or the refusal (§3.4) |

`SESSION_WHERE` (LOOK2b) stays as it is: where a row's work is now. `SESSION_GROUPS` adds what it needs.

### 7.3 Ask Daoris (D110)

Every act on Sessions is a door, a door owed, or exempt with its reason. **`HelpCoverageTests` reads Sessions' acts**,
`work/sessionActs.ts` and what it imports, as it reads Settings' domains and the Plugins view.

| Control | Hook, or local | Its answer |
|---|---|---|
| *Stop…* | `useStopSession` | door: the new `session` kind's `stop` |
| *Try again* | `useRetryQuest` | door: the `setting` kind's `retry` (exists), judged against the held too |
| *Archive*, *Archive what ended…*, *Unarchive* | `useArchiveSessions` (new) | door: the `session` kind's `archive` and `unarchive` |
| *Delete…* | `useDeleteSession` (new) | door: the `delete` kind's new `session` door, judged by §5.4 |
| *Answer…*, *Finish*, *Decline…* | `useAnswerSession`, `useResolveSession` | exempt: the person's answer to a session's question; a helper would be answering for them (D37) |
| *Review*, *Open folder*, *Open a terminal here*, *Open in its own window*, *Copy session ID* | local, `useOpenSessionFolder` (new), `useOpenWindow` | exempt: nothing changes |
| *Group by*, *Show archived* | local | exempt: a viewer's own look, as the theme is (D66) |
| a door into a session | `open('sessions', id)` | door: `go`, naming the view and the session, through FRAME1i's `item` |

- **The `session` kind** (`session_propose`, its doors `stop`, `archive`, `unarchive`) is judged against the facts,
  which gain this machine's sessions: id, shown state, group, short title, repository. A card says what follows in
  §3.3's or §5.2's words and its terminal line, and nothing applies until Apply (D89). *Stop what is working in this
  repository* is a thing a person asks, and Apply is still their press, so stop is a door rather than an exemption.
- **The room**: its doors table gains *stop a session*, *archive what ended*, *delete a conversation* and *list
  sessions by what they need*; its retry row names the quest's page and the session, where it named a drawer; *The
  machine now* counts sessions by group.

### 7.4 The machine log

Two events join D94's catalogue, names and counts only (D94 §5): `session.deleted` (`session`, `kind`, `door`) and
`sessions.archived` (`count`, `door`). An archive changes no work, so it is counted, not listed. A deletion removes a
record, so the log keeps that it happened, without a word of it.

## 8. Both languages

Every name follows D116: its kind decides its form and its budget, and a concept takes the glossary's term. A count is
English characters and Chinese units (D116 §4), with a number counted as two characters. Existing keys keep their key
and are marked *(exists)*. A key used both as a button and as a menu item is budgeted as a button, the tighter.

| Key | Kind | English | 中文 | Measured / budget |
|---|---|---|---|---|
| `work.group.you` | section | Waiting on you ({{count}}) | 等你处理（{{count}}） | 19, 7 / 32, 12 |
| `work.group.review` | section | To review ({{count}}) | 待审阅（{{count}}） | 14, 6 / 32, 12 |
| `work.group.working` | section | Working ({{count}}) | 工作中（{{count}}） | 12, 6 / 32, 12 |
| `work.group.later` | section | Resumes later ({{count}}) | 稍后继续（{{count}}） | 18, 7 / 32, 12 |
| `work.group.ended` | section | Ended ({{count}}) | 已结束（{{count}}） | 10, 6 / 32, 12 |
| `work.group.archived` | section | Archived ({{count}}) | 已归档（{{count}}） | 13, 6 / 32, 12 |
| `work.list.groupBy` | section | Group by | 分组方式 | 8, 4 / 32, 12 |
| `work.list.byState` | menu | State | 状态 | 5, 2 / 24, 10 |
| `work.list.byRepository` | menu | Repository | 仓库 | 10, 2 / 24, 10 |
| `work.list.showArchived` | menu | Show archived | 显示已归档 | 13, 5 / 24, 10 |
| `work.list.archiveEnded` | menu | Archive what ended… | 归档已结束的会话… | 19, 9 / 24, 10 |
| `work.list.archiveMeanIt` | button | Archive {{count}} | 归档 {{count}} 个 | 10, 5 / 20, 8 |
| `work.list.showMore` | button | Show {{count}} more | 再显示 {{count}} 个 | 12, 6 / 20, 8 |
| `work.shown.parked` | status | parked | 已挂起 | 6, 3 / 16, 5 |
| `work.shown.awaitingReply` | status | awaiting reply | 等回复 | 14, 3 / 16, 5 |
| `work.shown.accountCooling` | status | account cooling | 账户冷却中 | 15, 5 / 16, 5 |
| `work.act.stop` | button | Stop… | 停止… | 5, 3 / 20, 8 |
| `work.act.stopMeanIt` | button | Stop session | 确认停止 | 12, 4 / 20, 8 |
| `work.act.retry` | button | Try again | 重试 | 9, 2 / 20, 8 |
| `work.act.answer` | button | Answer… | 回答… | 7, 3 / 20, 8 |
| `work.head.review` *(exists)* | button | Review | 审阅 | 6, 2 / 20, 8 |
| `work.act.openFolder` | button | Open folder | 打开文件夹 | 11, 5 / 20, 8 |
| `work.act.terminal` | button | Open a terminal here | 在此打开终端 | 20, 6 / 20, 8 |
| `work.act.archive` | button | Archive | 归档 | 7, 2 / 20, 8 |
| `work.act.unarchive` | button | Unarchive | 取消归档 | 9, 4 / 20, 8 |
| `work.act.delete` | button | Delete… | 删除… | 7, 3 / 20, 8 |
| `work.act.deleteMeanIt` | button | Delete session | 确认删除 | 14, 4 / 20, 8 |
| `work.head.more` | button | More actions | 更多操作 | 12, 4 / 20, 8 |
| `work.rail.menu.copy` *(exists)* | menu | Copy session ID | 复制会话 ID | 15, 5.5 / 24, 10 |
| `work.archived.none` | headline | Nothing archived | 没有已归档的会话 | 16, 8 / 40, 16 |
| `quests.field.short` | field | Short title | 短标题 | 11, 3 / 36, 14 |
| `quests.field.shortPlaceholder` | placeholder | a few words that tell it apart | 在列表里区分这条委托的几个字 | 30, 14 / 40, 16 |

**Retired**, each with its place: `work.rail.endedMore` (the count becomes `work.list.showMore`'s press),
`work.composer.stop` (the composer's session stop), `work.awaiting.stop` (the parked and intake cards' stop), and
`quests.session.stop` (the quest page's). The stop's one name is `work.act.stop`, and its second press
`work.act.stopMeanIt`.

**Why these names, where a reason is owed:**
- **Resumes later, 稍后继续.** The group names what the person need not do: each row in it moves by itself. *Waiting*
  alone would sit beside *Waiting on you* as two names for two different waits.
- **parked, 已挂起.** The glossary's *park* (挂起) already says *a quest stops after too many failed sessions*; a
  status is 已 and the verb for an outcome (D116 §3b).
- **awaiting reply, 等回复.** The session asked another repository and the quest waits on its answer; the glossary
  keeps *waiting on you* for the person alone.
- **account cooling, 账户冷却中.** D125's *Cooling until* on the account's row, said of the session; 中 for a state in
  progress.
- **Try again, 重试** is RETRY1's button's name (`quests.detail.retry`), kept for the one act on both pages.
- **Stop…, 停止…** in the session's own header and menu: the verb alone, since the object is the page's own
  (D116 §3b); the confirming press is 确认 and the verb.

**The glossary gains three terms**, each with its reason:
- `try again`: en *try again*, zh 重试. *Start a quest's work again on this machine: one parked after its failed
  sessions, or one your stop holds.* Avoid: zh 再试, 重新尝试. No English word is avoided: `retry` is the command's
  word, which sentences name in backticks. The `try` term's `match` already sets *try again* aside.
- `archive`: en *archive*, zh 归档; its inverse *unarchive*, 取消归档. *Take an ended session out of the session list
  on this machine, kept whole.* Avoid: zh 存档, a game's save, and 隐藏, which promises less than archive keeps.
- `short title`: en *short title*, zh 短标题. *A quest's few words, written by whoever publishes it, shown where a
  list gives it one line.* Avoid: zh 简称, an abbreviation, and 副标题, a subtitle.

**The glossary's doors**: `work.head.review` → the review's tab (`work.review.tab`); `work.act.terminal` → the
terminal view's name; `work.group.you` names the term *waiting on you*.

**The sentences** are the build's to write in both languages: the stop's asks (§3.3), the rows' lines (§2.2), the bulk
archive's sentence (§5.3), the delete's ask and refusals (§5.4), the toasts, and the terminal's lines. A sentence has
no budget; a toast keeps to two lines (110 and 55). The agent's and the driver's own sentences are content and are not
translated (`translation-parity`).

## 9. The build

These rows are ready for `TASKS.md`. Their lanes are `daoris.lanes.json`'s ids. D126 decides all of it, so no row
takes a decision number unless building it finds something D126 did not decide.

**Every row's proof**, besides what each names:
- a failing test first, and the fast half of each .NET suite it touches;
- the stories of what it adds, each smoke-tested by `composeStories`, and vitest over the mocked bridge;
- `npm run verify`, with parity and `names:check` clean for every new key;
- for a row that changes words a test reads, the old words searched in what a worktree cannot run: the desktop
  suites' `Process` half, `tools/*rehearsal*.mjs` and the web's `e2e/` specs;
- for a row that changes the window, the parent's look after the merge: both themes, both languages, at 1280, 888 and
  680 px.

**Order.**
1. SESSUX1a and SESSUX1j can start at once, in their own lanes.
2. The driver lane runs SESSUX1a → SESSUX1b → SESSUX1g.
3. The web shell lane runs SESSUX1c → SESSUX1d → SESSUX1e → SESSUX1i, one at a time.
4. SESSUX1f after SESSUX1e; SESSUX1h after SESSUX1d–g and FRAME1i; SESSUX1k after TOOL4c, TOOL4d and SESSUX1c.
5. SESSUX1l, the look on the install, last.

**SESSUX1b alone already ends today's silent limbo**: a person's stop says why its quest sits, and *Try again* on its
quest's page carries it on. **SESSUX1i alone would have told the owner on 1 October.**

- [ ] **SESSUX1a — the reader of a session's group, and the archive marks.** Covers §2.4, §5.2's file and §7.2's
  first two routes.
  - **What:** `SessionGroups.Read` over the records, the quests, the planner's verdicts (a seam on `Planner` answering
    a quest's verdict, from the loop's last look or a fresh plan), the trees' judgement of each ended session's own
    tree, and the marks; `SessionArchive` (`<home>/sessions/archived.json`, atomic); `SESSION_GROUPS` and
    `SESSION_ARCHIVE`; `SESSION_LIVE`, `SESSION_NEEDS_YOU` and `SESSION_UNKNOWN`, each with its three things; the
    Desktop README's rows. No case produces *account cooling* until SESSUX1k.
  - **Lanes:** `driver`; `modules`; `web-shell` for the refusals' entries in both catalogues.
  - **Proof:** `SessionGroupsTests`, one table: each shown state and group, the precedence (a stopped session with
    unlanded work is in *To review*), a parked quest's last session only, a teammate's record, an archived one;
    `SessionArchiveTests` (atomic, a missing or torn file reads as none, a mark for a gone record dropped on the next
    write); `DriverModuleSessionsTests` for both routes and each refusal; `DriverModuleRoutesTests`; the refusal
    catalogue tests.
  - **Look:** nothing on the window.
- [ ] **SESSUX1b — a person's stop holds its quest, and Try again releases it.** Covers §3.3's hold and §3.4.
  - **What:** `StartVerdict.Stopped` and its sentences; `released` in `driver.json`, both twins and `twins.md`'s row;
    `RETRY_QUEST` and `daoris driver retry` doing whichever applies, saying which, and refusing when neither does;
    the carry-on's instruction saying the person stopped it and released it; Ask Daoris's `retry` judged against held
    quests, which the facts carry; the room's retry row naming the quest's page and the session.
  - **Lanes:** `driver`; `cli`; `modules`.
  - **Proof:** `PlannerTests`: an open and a taken quest held by a person's stop, an interrupted stop carried on as
    before, a released stop planned, a second stop held again; `DriverConfigTests` and `driverconfig.test.ts` holding
    one table for `released`; `HelpSettingProposalsTests`; the room's golden files. In the `Process` half, run by the
    parent: the person's stop, nothing spawned at the next looks, *Try again*, the carry-on in the same tree.
  - **Look:** a stopped quest's page, its *Sitting* sentence and *Try again*.
- [ ] **SESSUX1c — the list by state.** After SESSUX1a. Covers §2.1–§2.3, §2.5 and §4.1–§4.4.
  - **What:** the list reads `SESSION_GROUPS`; the five groups with their counts and orders; the rows' words, marks
    and lines; `queued` neutral (`SESSION_TONE`); the list's ⋯ with *Group by*, remembered; a row's repository on its
    line by state; *Show N more*; the strip's order with group 1; Sessions' badge counting group 1. The view by
    repository keeps today's arrangement with the new words.
  - **Lanes:** `web-shell`.
  - **Proof:** stories for the list (by state with every group; by repository; one group only; empty; loading; the
    strip; laid over; a 中文 title) and for `SessionRow` (*parked*, *awaiting reply*, a to-review line, a stopped
    session holding its quest); `SessionRail.test.tsx` over a stubbed `SESSION_GROUPS`; `ui.test.tsx` for `queued`'s
    tone; the badge's test in `App.test.tsx`.
  - **Look:** Sessions at each width in both themes and both languages, by state and by repository; the strip with a
    waiting session first.
- [ ] **SESSUX1d — the page header, and every act where its session is.** After SESSUX1b and SESSUX1c. Covers
  §3.1–§3.3, §3.5 and §3.6.
  - **What:** the pinned page header with the acts by state; *Stop…* asking with §3.3's sentences; *Try again*; the
    row's *Answer…* attending and opening the box with the focus; *Review*; *Open folder* (`SESSION_OPEN_FOLDER`,
    `SESSION_FOLDER_GONE`); *Open a terminal here*; the row's ⋯ with the same acts through `work/sessionActs.ts`; the
    composer keeping *Finish* and *Stop turn*; the parked and intake cards losing their stop; the quest page's session
    section losing its stop and keeping its door; the working surface design's §3 note amended.
  - **Lanes:** `web-shell`; `modules` for the route and its refusal.
  - **Proof:** stories for the header (each state's acts, each stop's ask, a teammate's record, below 560 px) and for
    the row's menu per state; `WorkFrame.test.tsx` (a running driven session stopped from its header; no session stop
    in a chat's composer; the parked card's moves without a stop); `QuestPage` stories and tests (no stop, the door);
    `AttendedSession.test.tsx`; the modules' route test. The retired words searched in `e2e/` and the rehearsals.
  - **Look:** a running driven session's header and its *Stop…* ask; a parked session; a run of a thousand events
    scrolled, the header pinned and the toolbar beneath it; 680 px.
- [ ] **SESSUX1e — archive on the screen.** After SESSUX1a and SESSUX1c. Covers §5.2, §5.3 and §4.5.
  - **What:** *Archive* and *Unarchive* in the row's ⋯ and the header's; the list's ⋯ *Show archived* and *Archive
    what ended…* with its two presses; the *Archived* group and its empty state; search marking archived rows; the
    toast.
  - **Lanes:** `web-shell`.
  - **Proof:** stories (the *Archived* group; the bulk's first press with rows kept; nothing to archive); vitest:
    archive a row, the bulk's second press sending only what its first listed, an unarchived row back in its group, a
    refusal said in the host's words.
  - **Look:** the bulk's ask at 1280 and 680 px; the *Archived* group in both languages.
- [ ] **SESSUX1f — delete a conversation that served no quest.** After SESSUX1a and SESSUX1e. Covers §5.4 and §7.4's
  `session.deleted`.
  - **What:** the ledger's `DeleteAsync` and `DELETE /api/sessions/{id}`, on a local host only, judging the record's
    half; the driver judging the machine's half and removing the files; `SESSION_DELETE`; `deletable` in
    `SESSION_GROUPS`; *Delete…* asking once; `session.deleted` (the machine log design's §4 gains the row).
  - **Lanes:** `service`; `driver`; `modules`; `web-shell`.
  - **Proof:** the service's ledger and HTTP tests (each refusal, a pushed record refused, no route at a shared
    deployment); the driver's (the files removed, a tree or a landing refusing, the log line holding no words); the
    modules' route test; vitest (*Delete…* offered only where deletable, its ask).
  - **Look:** a chat's *Delete…* ask, and a driven session whose ⋯ offers none.
- [ ] **SESSUX1g — the terminal's `sessions`.** After SESSUX1a, SESSUX1b and SESSUX1f. Covers §7.1.
  - **What:** `daoris-driver sessions` (the listing by group, `--group`, `--repository`, `--json`), `stop`, `finish`
    and `decline` through the requests folder every loop on the home watches, `archive`, `unarchive` and `delete`; the
    usage and its golden; the archive's and the delete's `door` in the log.
  - **Lanes:** `driver`; `modules` where the shell's loop watches the requests.
  - **Proof:** `SessionsCommandTests`, in-process, with the exits; `SessionRequestsTests` (a request for a session
    this loop runs, one it does not, an orphan ended, a parked session with no process moved by the ledger); the
    usage's golden. A family rehearsal phase, run by the parent: a stub session stopped from the terminal while the
    loop runs it, its record the person's stop and its quest held; archived from the terminal and gone from the
    listing.
  - **Look:** nothing on the window.
- [ ] **SESSUX1h — Ask Daoris reaches sessions.** After SESSUX1d–g and FRAME1i. Covers §7.3.
  - **What:** the `session` kind (`session_propose`: `stop`, `archive`, `unarchive`); the `delete` kind's `session`
    door; the facts' sessions by group; the room's doors table and *The machine now*; a go naming a session;
    `HelpCoverageTests` reading `work/sessionActs.ts`.
  - **Lanes:** `driver`; `service` (`KnowledgeTools.Help.*` and `HelpProposalBox.*` for the kind and the delete door);
    `web-shell` (the card, `where.ts`).
  - **Proof:** `HelpSessionProposalsTests` (each door; an unknown id refused naming this machine's; a teammate's
    refused; a live session's archive refused); `HelpDeleteProposalsTests`; `HelpProposalKindsTests`;
    `HelpCoverageTests` (a control on Sessions answered for, a removed one failing); `HelpRoomGoldenTests`; the
    service's help tests; `ProposalCard.test.tsx`.
  - **Look:** Ask Daoris asked to stop what runs in a repository, and to archive what ended; each card.
- [ ] **SESSUX1i — What needs you holds a parked quest, and a park is said once.** After SESSUX1c in the web shell
  lane. Covers §4.6 and §4.7.
  - **What:** `needsAPerson`'s `parked-quest` row and its `AttentionRow` state; `AttentionKind.QuestParked` from the
    loop's parked quests, said once, as a toast and a headless line, behind the notify switch.
  - **Lanes:** `web-shell`; `driver`; `modules` where the shell delivers the toast.
  - **Proof:** `attention.test.ts` (the row, its order, absent in a browser); the `AttentionRow` story;
    `AttentionTests` (said once, never for a stop, never again on a relaunch); the toast's test.
  - **Look:** Overview with a parked quest in *What needs you*, both languages.
- [ ] **SESSUX1j — a quest's short title.** Any time. Covers §6.
  - **What:** `short` on the quest: the store's column, the publish operation and its replay, HTTP, `quest_publish`,
    the sync's wire, the refusal past 40 characters or a line break; the intake's prompt, the driven instruction and the
    tool's description; the quest composer's optional *Short title*; `questName`, and `sessionTitle` using it in every
    place §6.2 names; the quest page's id line.
  - **Lanes:** `service`; `driver` (the prompts and their goldens); `web-shell`.
  - **Proof:** the service's store, exchange, log, HTTP, MCP and sync tests (a round trip; an older quest has none;
    the refusal); the driver's instruction goldens; vitest for the helpers and the composer. `test:web`'s quest life
    with a short title, run by the parent.
  - **Look:** a list of long-titled quests and their sessions at 680 px, both languages.
- [ ] **SESSUX1k — waits for an account, in the list.** After TOOL4c, TOOL4d and SESSUX1c. Covers §2.2's *account
  cooling*.
  - **What:** the case in `SessionGroups.Read` reading D125's `limit` and the cool-off; the row in *Resumes later*
    with its line; ⋯ *Open Agents*, a door to the account's row, where D125's *Try now* is.
  - **Lanes:** `driver`; `web-shell`.
  - **Proof:** `SessionGroupsTests`' new rows; the row's story; vitest.
  - **Look:** a session whose account cools, in both languages.
- [ ] **SESSUX1l — looked at on the install.** The parent's, after a republish carrying SESSUX1a–i. The owner's real
  sessions by state; a parked quest carried on from its session; a stop holding its quest and *Try again* releasing
  it; *Archive what ended*; every width, both themes, both languages; a ledger kept as SESS1's was.

## 10. What does not change

- **D55**: the session is the organising object, and there is no editor.
- **D118**: the frame. The list pane and the main area are the view's; the side bar and the panel hold nothing of the
  view's own (§3c), and this design adds nothing to them.
- **D51 rule 7, as D88 amended it**: no tree with work in it is removed by anything here.
- **D58**: the strikes are derived from the records, and a stop is not one.
- **D46 §4**: a session's record is concluded from its exit and its quest. The groups are derived from the record and
  never written back to it: **no new session state** (the working surface design's §4).
- **D47 §4**: Sessions is shell-only, so nothing here reaches a browser. Overview's parked-quest row needs a driver's
  answer, so a browser's band has none.
- **D52**: no session is named by hand.
- **D89**: every change Ask Daoris proposes is the person's Apply.
- **D41's palette** and **D116's names**.

## 11. Not chosen

- **Naming a session by hand.** D52's trigger is two sessions that cannot be told apart; what was asked about is
  length, and the publisher's short title answers it.
- **A short title made by cutting the title.** It keeps the start, where quests tend to say the same thing.
- **Archive as a field on the record.** The record travels (D47 §4); what one person keeps in their list does not.
- **Archiving or deleting by age.** Nothing tidies itself (D51 rule 7's reason): a session gone from the list without
  a press is one the person never saw go.
- **Deleting a session that served a quest.** It is that work's record, and the strikes are counted from it.
- **A delete that travels to a remote.** A session record does not travel as a deletion, and a second kind of sync
  operation for a tidy is a larger change than the tidy.
- **Several sessions chosen at once.** It would be a second selection beside the attended session. *Archive what
  ended* covers the case asked about.
- **A filter by kind** (driven, chats, intakes, Ask Daoris). The view by state and archive cover the clutter; a look
  that finds a list needing one asks for it.
- **Stopping many sessions at once.** A stop holds its quest; many holds at once is a decision per quest.
- **Unlanded work in Overview's *What needs you*.** D88's proof is a git walk per repository, which Overview's every
  open would run. Sessions' *To review* holds it.
- **Moving *Try again* off the quest's page.** A parked quest is decided there too.
- **New ledger states for *parked*, *awaiting reply* or *account cooling*.** Each is a fact about the quest, derived;
  a state of its own would be a second lifecycle to keep in step.
- **Ask Daoris answering, finishing or declining a parked session.** The answer is the person's.
- **An unpinned page header.** In a run of a thousand events its acts would be out of reach.
- **A toast that undoes an archive.** The platform's toasts carry no action, and *Show archived* is one tick away.
- **A bound on what the session listing answers.** Not before a machine's records make the list slow, measured.

## 12. What only the window and a real run can prove

- **The widths**: the header's acts at 560 px, the groups' headings at 888 px in both languages, and the names'
  budgets, which are estimates until the window measures them (D116 §4).
- **The pinned header with the long run's toolbar beneath it**, in a real run of a thousand events or more.
- **Whether *by state* is the right default**, from the owner's look at their own sessions.
- **The stop's hold on a real driven session**, and *Try again* carrying it on in its tree.
- **A terminal stop taken by the desktop's loop on the install**, within the verb's ten seconds.
- **Whether agents write short titles that tell quests apart.**
- **How long `SESSION_GROUPS` takes** on the owner's workspace of 29 repositories, with its trees.

## 13. What this document's gate does not cover

This change is documents only, and nothing is built.

- **Read from the code at `c4c9725`**: the files §1.1 names, and §1.2 and §1.3 cite the line that decides a row
  wherever one line does. Two findings are read from the code and not seen on the window: a person's stop of an open
  quest's session planned again at the next look (M3), and a running driven session with no stop in Sessions (M1),
  which three places in `WorkFrame.tsx` decide (820, 862, 895–917) and no look confirmed.
- **D125's design** was read from its branch's commit, since it was not on main at this branch's start.
- **The owner's 1 October** is as the dispatch and D125's §0.2 record it; no record or transcript was read here.
- **Not measured**: every item of §12.
- **Found while reading, and filed in a row rather than fixed here**: the room's retry row names *the quest's drawer*
  (`HelpRoomDoors.cs:52`), which FRAME1d made a page (SESSUX1b).
- **`verify` checks** this document's links, the decision log's shape, the budgets and the duplicates, and none of
  these words.
