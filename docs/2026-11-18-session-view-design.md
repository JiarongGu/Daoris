# A session's one view (SESSIONVIEW1)

Status: proposed, 2026-11-18 (D320). Source: two fresh designs answering one purpose brief alone, with no existing design
assumed: brainstorm `sessionview2` (Anthropic, pass `g1`) and brainstorm `sessionview3` (DeepSeek, pass `g1`, with its own synthesis
`s1` and comparison). Each had one maker answering, so neither is a cross-maker brainstorm; they are two independent answers,
reconciled here against the code. The owner's direction (2026-11-18): the whole surface of a session is one panel, as a good LLM
app's output is, with its sub-agents and background processes shown. Supersedes `docs/archive/2026-11-15-session-panel-design.md`
(OUTPUTPANEL1, SESSIONROWS1). Rows SESSIONVIEW1b to SESSIONVIEW1j in `TASKS.md`.

## 1. Where the two designs agree

1. **One chronology.** Everything that happened is a row in one timeline, whoever did it: the agent's turns and calls, its
   sub-agents, Daoris's helpers and opinions, other sessions' messages, the workflow's steps. No tabs, no lanes.
2. **What is true now is pinned outside the scroll**: the state, the open waits with their real presses, what runs now. Only what
   changes the person's next act pins.
3. **State is derived, never reported.** An open wait on the person outranks everything; done needs nothing open and nothing
   running. The agent's "completed" is an event in the log, not the session's state.
4. **Every wait carries its own press**, never a link to a card elsewhere; settled, it becomes a row where it arose.
5. **A read mark per session**; a returning person lands there under a digest computed from the log, with no model on open.
   Open waits, failures, the person's words, closing messages, step results and acts with an outside effect are never folded.
6. **A running list**: each live thing (the turn, sub-agents, background processes, steps) with its elapsed time, its last line of
   output, and follow, open and stop.
7. **A background process outlives its call**: a row where it started, a row where it ended, the two linked.
8. **The workflow is drawn once as a track** and its steps are also rows in the timeline, from the same facts, so the two cannot
   disagree.
9. **One envelope, adapted at ingest per agent family**, unknown shapes kept and labelled, never dropped; large payloads bounded
   with a preview; rows drawn only where seen.
10. **A teammate's session** reads the same, with no press.

## 2. Where they differ, and which way is taken

| Question | `sessionview2` | `sessionview3` | Taken, and why |
|---|---|---|---|
| Where the pinned parts sit | Head (state, track) on top; running shelf, waits dock and composer at the foot | Everything pinned above the timeline | **Head on top, live band and waits at the foot.** Followed live, the newest output, the composer and the press sit together; a question's answer is typed in the composer. Today's frame already holds the foot (`WaitPanel`, the composer, a resize handle) |
| Helpers, opinions, the channel | Timeline rows by other actors, with filters | A collapsible right rail | **Timeline rows.** A rail is a second place to look and costs about 300 px beside a dock that already competes for width; `sessionview3`'s own comparison names the minimum width as what would flip it |
| How a child opens | In place, and in a drawer over the timeline | In place one level deep; a focus mode beyond | **In place one level; *Open* is the panel's focus** (the head and the bands stay, a breadcrumb leads back). No drawer: at the dock's widths it would cover the timeline anyway |
| Where a running process lives | The running shelf | A pinned Processes list and the now band | **One live band for everything running**; no second list |
| The digest | A row at the read mark | A pinned band | **A row at the read mark**: it is about the past, and only what is true now pins |
| Filters (talk, changes, commands, agents, channel) | Yes, narrowing rows never the bands | None | **Left out** until a measured need: the folding rules answer the scan |
| A two-step press for high-effect acts | No | Asked of the owner (`Q5`) | **A press is today's press, moved.** This design changes where a press is, never what it does; a marked destination keeps its confirmation (D304) |
| A deadline on a wait | No | Asked (`Q6`) | **None here.** An unanswered wait goes up its reporting line (D304 point 3), not into an expiry |
| A teammate's presses | Shown with no presses | Disabled with the reason, or hidden (`Q7`) | **Drawn as lines naming who it waits on**, no press: a disabled press invites a click |
| Sub-agent depth | Any depth in place | One level, deeper flattened under a breadcrumb (`Q8`) | **One level indented; deeper opens in the focus** |
| A model summary | Never | Optional helper, labelled with its cost (`Q2`) | **None on open**; a helper's summary waits for the computed digest's trial to fail |
| Stopping a process from the band | Asks once | Asks once; the comparison asked the owner (`Q4`) | **Asks once, in place**, and keeps who stopped it. It is the person's own press in their window, not a question on doctrine rule 1's list |

`sessionview3`'s comparison put its eight questions to the owner. None is on the closed list of owner things (direction, money,
credentials, their releases, history, their data, publishing), so each is settled above and recorded in D320.

## 3. What neither design settled, settled against the code

1. **Who judges the state.** The driver's one reader (`SESSION_GROUPS`: group, reason, `awaits`; a line's state by `LineStandings`)
   already places a session in Needs you, Working, Pending, Failed or Done, and the rail and *What needs you* read it. The page's
   head still names the record's own state (`shownState`), which is how a session waiting on the person read *completed*. The panel
   reads the reader and nothing else; the page derives no state (desktop-app: .NET judges once and pushes).
2. **Which waits there are.** The page reads them in one pure rule (`work/waits/waits.ts`, `waitsOf`), drawn twice today (the
   head's cards and the foot's strip, desktop-app rule 38), and a run's held or failed step has its press only on its *What needs
   you* row (HELDPRESS1). Until the waits are read in the driver too, the line reads *Waiting on you* where either the reader or
   the dock says so, the window's log names a disagreement, and a census holds a reader reason for each dock kind.
3. **Which children Daoris can see and stop.** The readings exist (`WireReading.ChildSeen` with its call, parent and depth;
   `ChildEnded`; `TaskStarted`; `TaskEnded` with its output file), the lineage keeps children (`ChildBindings`), and each child is a
   console stream (`subagent/<id>`, `task/<id>`). The protocol door can stop a task (`SessionProcesses.StopTaskAsync`); the native
   door can stop none (`ClaudeStreams`); no door stops a sub-agent alone; Codex's children are seen only when they call Daoris
   (`seenByCalls`). *Stop* stands exactly where a door can stop; elsewhere the row's tip says why. What a door does not report is
   said as a label, never a blank.
4. **Where a sub-agent's own calls are kept.** In a record of its own beside the session's, never in the session's record: the
   driver reads the session's record for what the session said to the person (a parked card's question), and a build that predates
   the change would quote a child's words as the session's (`.claude/knowledge/compatible-builds.md`: new rows where older builds
   cannot see them).
5. **What a teammate's machine sends.** A session's events and transcript never leave its machine (conversations-and-context rule
   5), and a shared host exchanges permitted records only. A teammate's panel holds its state, its track, its waits as lines and the
   rows its records carry, and says in one line that its conversation stays on its machine. No band, composer or press.
6. **Where the steps' rows come from.** Steps run while the session waits between turns (D293), and their facts are the run's, not
   the session's record's. The track and the step rows read the run's one reading (`RunPassageReader` and the run's journal), the
   rows placed by time between turns, so one source draws both.
7. **Where the read mark lives.** Per conversation (a line, CONVO1) on this machine: the newest part and event that was on screen
   while the window had focus, in a desktop store table of its own, moving only forward, so every window and the terminal agree.
8. **What bounds a payload.** No new content store: the record already cuts a large field and says how long it was
   (`SessionEventText.Bound`), pages by its heads and offsets (D275), and a task's output is its own file, read on opening.
9. **Text.** The chrome and the digest's sentences are the catalogues' in English and Chinese; the agent's words, commands and
   paths it printed are shown as written, the agent's closing words labelled as its own.

## 4. The panel

One column in the main area, the session's whole surface. Only the timeline scrolls.

- **Head** (fixed): the state line (the reader's state, its cause, its age), the title, the agent, the acts (⋯ holds *Details*,
  rename, pin, delete); under it the workflow's track.
- **Timeline** (scrolls): turns, calls, children, steps, other actors' rows, wait markers, part boundaries, the digest at the mark.
- **Live band** (fixed): one line per running thing. Empty, one line: *Nothing running*. In a short window, a count.
- **Needs-you dock** (fixed): every open wait the person settles, oldest first, with its press. Never folded, filtered or
  scrolled away; empty, not drawn (the state line already says nothing waits); the one region that may push the timeline up.
- **Composer**: as today (CHATUX1 shapes it).

### Working, followed live

```
┌───────────────────────────────────────────────────────────────────────────┐
│ ● Working · 12 m · editing src/auth.ts            fix-login · Claude   ⋯ │ head
│ Work ● › Checks ○ › Accept ○ › Land ○ › Push ○                      1/5 │ track
├───────────────────────────────────────────────────────────────────────────┤
│ ▸ Turn 11 · "Token refresh in place" · 9 calls · +120 −30 · 6 m          │ timeline
│ ▾ Turn 12                                                                 │
│   You: also cover the expiry path                                         │
│   Read src/auth.ts · Search "expires" · 4 more ▸                          │
│   ⑂ sub-agent "find expiry tests" · 7 calls · running 3 m ▸              │
│   $ npm test --watch · in the background · running ▸                      │
│   Edit src/auth.ts +14 −2 ▸                                               │
│   The refresh now checks expiry before it…▌                              │
├───────────────────────────────────────────────────────────────────────────┤
│ Running 3                                                                 │ live band
│  ● Turn 12 · 1 m 10 s · editing src/auth.ts                               │
│  ⑂ find expiry tests · 3 m · reading test/auth.test.ts         Open      │
│  $ npm test --watch · 12 m · "41 passed, 0 failed"    Follow Open Stop    │
├───────────────────────────────────────────────────────────────────────────┤
│ [ Message the agent…                                         ] Send  Stop │ composer
└───────────────────────────────────────────────────────────────────────────┘
```

### Waiting on you, after being away

```
┌───────────────────────────────────────────────────────────────────────────┐
│ ◆ Waiting on you · 38 m · Accept, and 1 more      fix-login · Claude   ⋯ │
│ Work ✓ › Checks ✓ › Accept ◆ › Land ○ › Push ○                      3/5 │
├───────────────────────────────────────────────────────────────────────────┤
│ ── Since you left · 2 h 10 m ───────────────────────────────────────────  │ digest, at the mark
│   3 turns · 7 files +120 −30 · npm test failed once, then passed          │
│   1 sub-agent ended · checks passed (5 gates) · 2 waits on you            │
│   Its last words (the agent's): "Refresh and expiry are covered."         │
│ ▸ Turn 12 · "Expiry covered" · 11 calls · +40 −6 · 9 m                   │
│ ✗ $ npm test · exit 1 · 2 failed ▸                                       │ never folded
│ ▸ Turn 13 · "Fixed the two tests" · 3 calls · 2 m                        │
│ ── Checks passed · attempt 2 · 5 gates · 4 m ──                           │ step row
│ ◆ Accept asked here                                                       │ wait marker
├───────────────────────────────────────────────────────────────────────────┤
│ Nothing running                                                           │
├───────────────────────────────────────────────────────────────────────────┤
│ Needs you 2                                                               │ dock
│ ◆ Allow `git push origin fix-login` in its tree?  Allow once · Always · Deny │
│ ◆ Accept this work? 7 files +120 −30 · checks passed                      │
│   Accept… · Send back… · Review changes ▸ · where it was asked ↑          │
├───────────────────────────────────────────────────────────────────────────┤
│ [ Answer or message the agent…                                    ] Send │
└───────────────────────────────────────────────────────────────────────────┘
```

### Waiting on a step

```
┌───────────────────────────────────────────────────────────────────────────┐
│ ◇ Waiting on tester: Daoris checks · 4 m          fix-login · Claude   ⋯ │
│ Work ✓ › Checks ● › Accept ○ › Land ○ › Push ○                      2/5 │
├───────────────────────────────────────────────────────────────────────────┤
│ ▸ Turn 13 · "Ready for checks" · 3 calls · 2 m                           │
│ ── Checks started · attempt 2 ──                                          │
├───────────────────────────────────────────────────────────────────────────┤
│ Running 1                                                                 │
│  ▶ Checks · 4 m · "web: 212 of 340 tests"                Follow Open Stop │
├───────────────────────────────────────────────────────────────────────────┤
│ [ Message the agent…                                              ] Send │
└───────────────────────────────────────────────────────────────────────────┘
```

A step another role holds reads "waiting on <role>: <holder>" (D304), an agent holder included (D318). A step the person holds is
a wait: its card is in the dock, its chip marked ◆.

### Done, as a receipt

```
┌───────────────────────────────────────────────────────────────────────────┐
│ ✓ Done · landed on main 14:02 · 2 h 10 m          fix-login · Claude   ⋯ │
│ Work ✓ › Checks ✓ › Accept ✓ › Land ✓ › Push ✓                      5/5 │
├───────────────────────────────────────────────────────────────────────────┤
│ ▸ Turns 1–13 · 41 calls · 7 files +120 −30                               │
│ ── Accepted by you · 13:58 ──                                             │
│ ── Landed on main · 3 commits · 14:02 ──                                  │
│ ┌ Receipt ──────────────────────────────────────────────────────────────┐ │
│ │ End: landed · Result: 3 commits on main · Clean-up: its tree let go   │ │
│ │ Recovery: its branch kept · How it landed ▸ · Details ▸               │ │
│ └───────────────────────────────────────────────────────────────────────┘ │
├───────────────────────────────────────────────────────────────────────────┤
│ Nothing running                                                           │
├───────────────────────────────────────────────────────────────────────────┤
│ [ Go on with this work…                                           ] Send │
└───────────────────────────────────────────────────────────────────────────┘
```

Stopped, declined and abandoned read the same with their own end; a failure reads *Failed* with its one next press in the dock.

### A teammate's session

```
┌───────────────────────────────────────────────────────────────────────────┐
│ ● Working · on a teammate's machine · 40 m        fix-billing          ⋯ │
│ Work ● › Checks ○ › Accept ○ › Land ○                               1/4 │
├───────────────────────────────────────────────────────────────────────────┤
│ Its conversation stays on its machine; its records say:                   │
│ ── Started 13:20 from its quest ──                                        │
│ ── Checks failed · attempt 1 · 2 gates ──                                 │
├───────────────────────────────────────────────────────────────────────────┤
│ Waits on its owner: Accept                                                │
└───────────────────────────────────────────────────────────────────────────┘
```

### A child in focus

```
│ ● Working · 12 m · editing src/auth.ts            fix-login · Claude   ⋯ │ head stays
│ ‹ Session › find expiry tests · sub-agent · running 3 m · 7 calls        │ breadcrumb
├───────────────────────────────────────────────────────────────────────────┤
│ Read test/auth.test.ts · Search "expir" · 5 more ▸                        │ its own timeline
│ It found two tests that cover…▌                                          │
├── live band and dock as before ──────────────────────────────────────────┤
```

A background task's focus is its output file, searchable, with its tail following while it runs.

## 5. The state line

The reader's group and reason, its cause and its age, in one line; never the record's state word, never the agent's.

| Reader says | The line reads |
|---|---|
| `needs-you` | ◆ *Waiting on you* · age · the oldest wait's name, and how many more |
| `working`, its record awaiting a step | ◇ *Waiting on* role: holder · age (the step's attempt on its chip) |
| `working` | ● *Working* · age · the agent's current act |
| `pending` | ○ *Not started*, or *Held* and why |
| `failed` | ✗ *Failed* · why, its next press in the dock |
| `done` | ✓ *Done* · how it ended (landed, stopped by you, declined, abandoned) · when |

An open wait the person settles places the session in Needs you whatever its record says, so it cannot read done while it waits;
the dock and the line read the same facts, and their disagreement is logged (§3.2).

## 6. Catch-up

- **The read mark** (§3.7). Opened with anything after it, the panel sits at the mark; otherwise at the tail, following.
- **The digest** is a row at the mark, computed from the record and the run with no model: turns, files changed, commands and
  which failed, sub-agents and tasks and how they ended, steps passed or failed, waits opened and settled, messages to the person,
  and the agent's last words quoted as its own. Each line jumps to its rows.
- **Folded**, opening in place: each ended turn to one line (its closing words' first line, calls by kind, files changed,
  duration); thinking; successful calls; a command's output to its head and tail; a diff to one row per file; retries; a sub-agent
  to its header row. The rule: a row folds only where it changed nothing outside the conversation and failed nothing.
- **Never folded or filtered**: an open wait's marker; a failure (a failed call, a nonzero exit, a failed step, a stop, a limit);
  the person's own words; each turn's closing words; step results; acts with an outside effect (land, push, pull request,
  release); messages to the person; part boundaries (*continued after a limit*, *handed over to …*).
- **Following**: at the bottom the tail follows; scrolled up, *N new below* (today's *Back to bottom*); new rows never move what
  is being read.

## 7. The live band

- **What is in it**: the agent's current act (*editing src/auth.ts*, *thinking 40 s*, *turn ended*); each sub-agent and background
  task of the session's streams; the run's running step; a helper or an opinion working for the session. Children are indented
  under their parent once the envelope keeps it (§8).
- **Each line**: its kind and name, its elapsed time ticking from its start (the page's clock, not the event stream), its last line
  of output or last act, and a quiet mark past a threshold (*no output 6 m*), the stall signal.
- **Its presses**: *Follow* pins a three-line live tail under it; *Open* puts the panel in its focus; *Stop* asks once in place,
  names what ends with it, reads *stopping* until its end is read, and keeps who stopped it. *Stop* only where its door can (§3.3).
- **Its source**: the session's streams (`ISessionStreams`, today the output panel's tabs), the record's last open call, the run's
  passage. Updates are drawn once per frame.

## 8. The event envelope

Agent-family neutral: each door maps its wire into one closed set at ingest (`WireReading`, then `SessionEvent`), and the page
learns one model. What it keeps today: the session's record (`<id>.events.jsonl`: `user`, `message`, `thought`, `tool`, `plan`,
`usage`, `turn`, `note`, `transient`, `raw`, `receipt`, numbered by `seq`, a call merged by its `id`); children's lineage
(`ChildBindings`); the streams' lines on the console. What it drops: the link from a child or a task to the call that started it,
and every structured event of a sub-agent (its calls reach only its console stream as text).

What is added:

1. **A lifetime for every child.** The session's record keeps each sub-agent's and each background task's start and end as two
   kinds, `child` and `task`: its id, the starting call's id, its parent child and depth where the wire says, its name and task
   words, its end's word (ok, failed, stopped, lost), and who says it (`ReadingBy`: reported, observed, inferred, so an end implied
   by the session's close says so). One `seq` orders them with the turns, so a task's end lands where it happened.
2. **A task's starting call.** `TaskStarted` gains its call where the wire names it; measured first on real captures of each door
   (Claude Code native, Claude Code over the protocol, Codex), and said per door where it does not.
3. **A sub-agent's own record.** Its messages and calls, mapped by its door's own mapping into the same `SessionEvent` shape, are
   kept in a record of its own in a folder beside the session's record that no enumeration of the sessions folder reads as a
   session; the session's record never holds them (§3.4).
4. **A door's capabilities are stated**: whether it reports sub-agents, tasks and their output, and whether it can stop each. The
   panel draws a label where a door reports nothing.
5. **Unchanged**: a large field is cut and says how long it was; a shape with no kind is `raw`, drawn as a labelled row; waits stay
   their owners' (the record keeps no wait); the run's steps stay the run's (§3.6).

A build that predates this reads the session's record with two kinds it does not know; the row that adds them checks what each
reader that walks every event does with an unknown kind, and the children's records are out of its sight.

## 9. Today's areas, mapped

One way per job: each part below moves to its one place, and its old place is deleted in the row that moves it.

| Today | Where it goes | Row |
|---|---|---|
| `SessionHead`'s state pill (`shownState`) | The state line, read from the reader | 1b |
| `SessionHead`'s wait cards and `WaitStrip` (rule 38's two places) | The dock, one place; the head's copies deleted | 1b |
| A run's held or failed step, pressed only from *What needs you* | The dock (HELDPRESS1's presses) | 1b |
| `RunView`'s *N tool calls*, `TurnView`, `ToolCard` | Turn rows folded to one line, call rows; a spawning call's card names its child | 1c, 1e |
| The output panel's sub-agent and task tabs | The live band and the focus; the session's console stays the raw console | 1d |
| At work's subagents (per session) | Nested rows and the band | 1f |
| The RightDock's Workflow view, per session | The track; the run's flowchart opens from it | 1g |
| At work's exchanges, `SessionRelations`, `ChannelPanel` | Rows by their actors, the band while alive, the dock while they wait on the person | 1h |
| `PartsFold`, `ChainLine` and `ChainStrip` | Part boundary rows (CHATUX1's word) and *Details* | 1i |
| `RelatedSessions`, `HowItCameToBe` | *Details* | 1i |
| `SessionMemory`'s end awaiting approval, `TogetherDoor`'s asks | The dock; their history rows | 1i |
| `WorkMilestone`, `LandedBranchDoor`, `FinishedRecord` | The receipt at a finished session's end | 1i |
| `SessionTimeline` and the RightDock's Timeline view | State changes as rows; deleted | 1i |
| `HandedAccount` | A row at the start, as today | stays |
| Review (`outputs`) | Stays the document a row or the Accept opens, not a per-session status tab | stays |
| *Notifications*, Ask Daoris, the file preview, Console, Terminal | Stay: they are not one session's | stays |
| Session rows (SESSIONROWS1) | Built as desktop-app rule 37's one status line; the passage lives in the track | 1g |

## 10. Rows, in build order

1. **1b** the state line and the dock over today's transcript: the smallest slice that shows value. Its baseline first.
2. **1c** catch-up: the read mark, the digest, the folds.
3. **1d** the live band from today's streams, the output panel's child tabs gone.
4. **1e** the envelope: children's lifetimes with their calls, sub-agents' own records. Before any row that needs parent ids.
5. **1f** sub-agents and background tasks in the timeline.
6. **1g** the track and the steps as rows.
7. **1h** helpers, pieces, opinions and the channel as rows.
8. **1i** the head's folds and the Timeline view into their one place.
9. **1j** a long session stays fast, measured; then D320's trial read.

## 11. Left out

Filters; a landmark scroll map; a model's summary; a deadline on a wait; a composer that writes to another session (the channel's
own reply stays where its row is); a separate content store. Each waits on a measured need.
