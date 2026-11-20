# The window, redesigned (UISH, UISE, UIWF, UIRP, UIST, UIDS)

Status: decided, 2026-11-20 (D330 to D345). The owner asked on 2026-11-19 for every screen of the window to be planned anew,
with a design system and a component library, and for every UI and UX row to be set up from it. Sources: the purpose brief
(§1); two fresh designs written from it alone (one by Opus; Daoris's own brainstorm `uiux-window`, passes g1 by Claude and g2
by Codex, with its synthesis s1); an inventory of every screen as it stands at `ed15806428` with 396 shots; six plans, one per
group of screens (the frame UISH, Overview and sessions UISE, asks and workflows UIWF, Workspaces and Company UIRP, Settings
UIST, the design system UIDS), each read against the code; and the owner's answers to the plans' direction questions. The
plans and the shots stay in the steward's ignored scratch (`local/scratch/uiux/`); everything a builder needs is here or in
its row. The rows are `TASKS.md` § *Window redesign*.

Every row's stories are drawn at the window widths of §3.2, in English and Chinese, light and dark (UIDS3's globals), and every
word a person reads goes through the en and zh catalogues, with a glossary row for each new name; a row says only what differs.

## 1. The purpose

1. Daoris runs a small software company of AI coding agents for one owner, who sets direction and owns the outcome.
2. Agents do the work, review each other and ask the owner only the owner's things: direction, money, sign-ins, their
   releases, deleting their data.
3. At a glance the owner sees what waits on them, what is moving and what finished while they were away, and answers each wait
   in one press with enough said beside it.
4. One session reads in one panel; one ask reads across its repositories; accounts say who is signed out, at a limit and what
   it costs; every claim says how it is known and which model ran.
5. Calm and dense, in English and Chinese, light and dark, on a wide screen or a narrow window beside an editor.

## 2. Principles

- **Owner, not manager.** The window shows the owner only the closed list of owner things as waits (§5). The rest is decided
  by the agents and their roles, and shown as decided, with *Reverse…* where it can be reversed.
- **One number.** The window shows one standing number: how many things wait on the owner, across every workspace. Places wear
  marks, never numbers.
- **Amber means you.** Amber and ◆ mark only what waits on the owner. The interactive accent is blue; moving work is told by
  its ● and its word.
- **Acts for any actor.** Every act the owner can perform (answer a wait, approve a release, spend, delete their data, sign
  in, reverse a decision) is defined once: what it does, whether it can be undone, what it needs, who holds it. The window's
  card and held press are the human actor's door to it; an agent actor configured to hold the act performs the same act
  through its tools (§6). The window is built as the human's skill over the acts, never as the only way to do them.
- **One press.** A reversible act runs after a 5-second grace with *Undo*; an act that cannot be taken back is one held press
  with its consequence written on the control. No act asks a second question.
- **Every claim says how it is known.** A check with its time, a reviewer with its model, a reading with its time, an agent's
  word, the owner's own setting, a default, or stale as of a time (`KnownBy`, §8).
- **Which model ran.** Wherever a model's work is shown, the maker and the model that actually ran, and "asked X, ran Y" when
  they differ (`AgentChip`).
- **One way per job.** One home per object and per setting, one address per object, one card per wait, one place per word.
  A screen that duplicates another goes.
- **Calm.** Motion only on a change of state; no spinner without what it waits on; nothing obsolete stays.
- **Both languages, both themes, both widths**, checked by pictures and measurements (§16), never by eye alone.

## 3. The frame's contract

Every screen hangs on this; no row adds a region, a place or a standing number without a decision.

### 3.1 Regions

| Region | Size | Holds | Whose |
|---|---|---|---|
| Title strip | 36 px | the mark and its app menu, the scope switcher, the search field, the waits pill, **+ Ask**, the Ask Daoris door, the caption buttons (3 × 44 px, never covered or dimmed) | frame |
| Away line | 28 px, only after an absence of 30 min to 2 h | one line of what happened while away, *Overview ▸*, *Caught up* | frame |
| Rail | 72 px with labels from a 1280 px window; 48 px icons below | the six places (§4.1) | frame |
| List pane | 264–420 px, 300 to start; Settings' 200 | the place's list under one head | the place |
| Main area | floor 560 px | first row the breadcrumb (the frame's); below it the place's page | the place |
| Side bar | 360–560 px, 400 to start | Ask Daoris, and nothing else | frame |
| Panel | 120 px to 60% of the height; closed at every start, and opens with a terminal at the height last kept (200 px the first time) | the owner's terminal, and nothing else | frame |
| Status strip | 24 px | facts the owner can trust, registered by places (§15.2) | frame, items by places |

The panel is closed at every start and whenever its last terminal closes (UISH25): with no terminal the main area is the window
minus the title strip (36 px) and the status strip (24 px), and no bar or empty tab stands where it was. Opening a terminal opens
it at the height the owner last dragged it to; only that height is kept, never that it was open. (Measured 2026-11-20: open on
every place it took 247 of 734 px, 34%, to say *Attend a session and its console streams here*.)

There are no movable views. A column beside a page (the session's facts rail, an ask's side column) is drawn inside the place's
own main area. Overlays sit between the title strip and the status strip and never cover the rail: the palette (640 px, near
the top), the sheet (right; 640 px for composers, 480 px for forms), the shortcut sheet, menus, toasts (bottom right).

Wide, 1600 px, Sessions in front, Ask Daoris open, after a 47 min absence:

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ 道 ▾ ▦ All workspaces ▾ [ ⌕ Search, go to or run a command…  Ctrl K ] ◆ 3 waiting on you [+ Ask]  ? Ask Daoris ─□✕│
├───────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ While you were away (47 m): ◆ 1 new · ✓ 2 finished · ▲ 1 failed, fixed without you    Overview ▸  Caught up C   ✕ │
├────────┬──────────────────────┬────────────────────────────────────────────────────┬──────────────────────────────┤
│ ◎      │ SESSIONS  9      ⌕ + │ Products › Sessions › CSV export · api-server ⧉ ←→⋯│ ASK DAORIS  Claude · sonnet ✕│
│Overview│ by ask ▾   ⌕ filter  │                                                    │ You: why did the e2e stop?   │
│ ◇ Asks │ Export reports  ◆    │   the session panel (§10): 1600 − 72 − 300 − 400   │ Daoris: it hit its 3rd retry;│
│▌ ≡     │  ◆ infra       3 h   │   = 828 px with the side bar open                  │ the supervisor moved it.     │
│Sessions│ Rate-limit the API ● │                                                    │ Look: run #418 ↗             │
│ ▦ Work-│▌● api-server   41 m  │                                                    │                              │
│  spaces│  ○ web-app      1 h  │                                                    │ [ Ask about this screen… ]   │
│ ⚿ ▲    │ Your conversations   │                                                    │                              │
│ Company│  ● engine       2 h  │                                                    │                              │
│ ⚙      │ Ended today  4 ▸     │                                                    │                              │
├────────┴──────────────────────┴────────────────────────────────────────────────────┴──────────────────────────────┤
│ ● Live  ⇅ Synced 2 s ago   Accounts: 1 at its limit   Key $2.30/$20 Nov  Browser · driven by api-server   1.8.3 ✓ │
└───────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

Narrow, 900 px, the same moment, the side bar closed; the main area has replaced the list:

```text
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ 道 ▾ ▦ All ▾  [ ⌕ Ctrl K ]                              ◆ 3  [ + Ask ]  ?          ─ □ ✕ │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│ Away 47 m: ◆ 1 new · ✓ 2 · ▲ 1 fixed without you                 Overview ▸  Caught up ✕ │
├─────┬────────────────────────────────────────────────────────────────────────────────────┤
│▌ ≡  │ ← Sessions   Products › CSV export · api-server                               ⧉  ⋯ │
│  ◎  │                                                                                    │
│  ◇  │        the session panel at 852 px (§10.3, narrow)                                 │
│  ▦  │                                                                                    │
│  ⚿▲ │                                                                                    │
│  ⚙  │                                                                                    │
├─────┴────────────────────────────────────────────────────────────────────────────────────┤
│ ● Live   1 at its limit   $2.30 of $20                                        1.8.3   +2 │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Widths

| Window | Rail | List and main | Side bar | Sheets |
|---|---|---|---|---|
| ≥ 1280 px (reference 1600) | 72 px, labelled | side by side | beside the main area while it keeps 560 px, else over it | 640 / 480 px at the right |
| 1024–1279 px | 48 px, icons | side by side while the main area keeps 560 px | over the main area | as above |
| 720–1023 px (reference 900) | 48 px, icons | the main area replaces the list (← back) | covers the content area | cover everything right of the rail |

- Layout follows room, never the viewport: the list stands beside the main area only while the main area keeps its 560 px
  floor (`work/frame/layout.ts`); a region lays itself out by its own container width.
- The window's least width becomes 720 px (today 960, `MainForm.cs`); a torn-off window's is 520 px (§15.11). Tests and stories
  use only these widths: 1600, 1280, 1024, 900 and 720, and 520 for a torn-off window.
- Story widths, read from `design/regions.ts` (UIDS13) by Storybook's `withRegion`: the main area at the wide reference with a
  list, 1228 px; at the narrow reference, 852 px; at the least width, 672 px; the side bar 400 px; a list 300 px. The window
  stories draw at 1600, 1280, 1024, 900 and 720.
- Nothing scrolls sideways at any of these widths, in either language (UISH23 measures it).
- A screen's own thresholds are named container tokens with their reason: Overview's three columns from 75rem of main width
  and two from 56rem; the session's facts rail from 68rem; the session head's narrow fold at 40rem; the ask page's two
  columns from 64rem; the code map's list below 36rem.

### 3.3 States the frame shows

| State | The frame shows |
|---|---|
| Starting | the splash (§15.10), then each region's skeleton rows in its real layout after 300 ms |
| Live | as drawn; the status strip's ● Live |
| Stale | "◐ As of 14:58 · the driver has not answered since 14:58"; the pill keeps its number marked ◐; lists keep their rows marked ◐ |
| Waiting on the owner | the pill, amber |
| Failed | a region that cannot draw says so in its own place (§15.9); the rest of the frame works |
| Browser | no pill, side bar or panel; "Read-only in a browser"; Sessions and Company absent, never disabled |

## 4. Places, scope, addresses and moving

### 4.1 The six places

| Place | 中文 | Key | Glyph | List | Main area | In a browser |
|---|---|---|---|---|---|---|
| Overview | 总览 | Ctrl+1 | ◎ | none | what waits on you, what is moving, what happened since you left (§9) | yes |
| Asks | 需求 | Ctrl+2 | ◇ | asks by what they wait on, quests no ask holds as a group | an ask's page, or a quest's page under it (§11) | yes |
| Sessions | 会话 | Ctrl+3 | ≡ | sessions grouped by ask | the session panel (§10) | no |
| Workspaces | 工作区 | Ctrl+4 | ▦ | one tree of workspaces and their repositories | a workspace's or a repository's page with tabs, Wiring and Knowledge among them (§12) | yes |
| Company | 公司 | Ctrl+5 | ⚿ | Accounts by agent, Roles, Connectors, Plugins | an account board, an agent, a role table, a connector or plugin (§13) | no |
| Settings | 设置 | Ctrl+, | ⚙ | domains in two groups | the domain (§14) | Appearance only |

Where today's places go:

| Today | Goes to |
|---|---|
| Overview | Overview, rebuilt as the owner's home (the name and 总览 stay; "Desk", 工作台, is on the glossary's avoid list, and "Home" is the data folder's word) |
| Quests | Asks: an ask's quests are rows of its page; a quest no ask holds is a group of the Asks list; the quest page sits under Asks |
| Repositories | Workspaces |
| Map | a workspace's Wiring tab, and the Workspaces root's Wiring for every workspace |
| Knowledge (Search, Convergence) | a repository's Knowledge tab; a workspace's Knowledge tab holds *Said in more than one repository*; the palette searches all of it |
| Agents (agents, roles, Setup) | Company › Accounts and Roles; Setup's tree goes: a workspace's and a repository's setup is its own Setup tab, the machine's access and rules are Company › Roles › *What stays yours*, the machine's default workflow is on the Workspaces root's Workflow tab |
| Plugins | Company › Connectors and Plugins |
| Notifications (side bar view) | gone: each part has a home (§5.7) |
| Settings | Settings, cut to the owner's and the install's domains (§14) |

Places wear marks, never numbers: Sessions a breathing ● while any session runs; Company a ▲ while an account or connector is
unhealthy (signed out, at its limit, refused). A sign-in that holds work is a wait and is counted in the pill, not on the rail.

### 4.2 The scope

- The scope switcher sits in the title strip after the mark: **▦ All workspaces ▾** or **▦ Products ▾**. It is the scope's
  only home. A new window starts at *All workspaces* (全部工作区); the choice is the window's own.
- It filters every place's list and the palette. It never filters the waits count; Overview scoped ends its waits with
  "2 more in other workspaces ▸". Company ignores it except that its *All places ▾* filters default to it.
- A link never carries the scope; an object's page names its own workspace as the first crumb.
- Its menu lists *All workspaces*, then each workspace with its waits (◆ n, on demand only) and a ● while work runs, then
  *Manage workspaces…*.

### 4.3 Addresses

Every place and object has one address, a path in the window's own address (`https://daoris.localhost/<path>`); the desktop
also answers `daoris://<path>` by bringing the main window forward. Ids, never names; a page reads its state from its address.

| Address | Opens |
|---|---|
| `/overview`, `/overview/waits/<wait>` | Overview; Overview with that wait focused, or its answered line |
| `/asks`, `/asks/<ask>`, `/asks/<ask>/quests/<quest>` | the Asks list; an ask's page; a quest's page under its ask |
| `/quests/<quest>` | a quest no ask holds |
| `/sessions`, `/sessions/<session>[/steps/<n>|/changes/<path>]` | the list; the panel, at a step or a file's change |
| `/workspaces/<workspace>[/<tab>]`, `/workspaces/<workspace>/repositories/<repository>[/<tab>[/<entry>]]` | a workspace's page; a repository's page at a tab |
| `/company/accounts[/<agent>[/<account>]]`, `/company/roles/(working|appointed|yours)[/<role>]`, `/company/connectors/<id>`, `/company/plugins/<id>` | Company's pages |
| `/settings/<domain>[/<sub>][#<row>]` | a Settings domain at a row |
| `/sessions/<session>` for an Ask Daoris conversation | its one address: a conversation is a session (§15.4); the side bar shows the current one, and a link opens it in the session panel |

- **Back and Forward** (Alt+← / Alt+→, the mouse's side buttons, the breadcrumb's ← →) walk the window's history and give
  back the scroll and the chosen row.
- A link to something finished opens it read-only with its outcome; a link to something removed says what it was and when.
- Every old address and `Opening` maps to one of these (a table test); old place spellings (`quests`, `map`, `knowledge`,
  `agents`, `plugins`, `repositories`) open their new homes.
- **The breadcrumb** is the main area's first row on every page: *workspace › place › object*, each crumb a link, then ⧉ (its
  own window), ← → and ⋯.

### 4.4 Moving

- **Keys**: Ctrl+1–5 and Ctrl+, the places; Ctrl+K the palette; Ctrl+N a new ask; Ctrl+Shift+N a new conversation; ? or Ctrl+/
  the shortcut sheet; on Overview and in a dock J/K, Y/N, 1–4, R, M, L, Enter (a key whose act cannot be taken back is held 800 ms,
  §5.3); C *Caught up*. A single-letter key never fires
  while a field has focus or an input method composes. Every key is in `menus/commands.ts` and printed on its button
  (`KeyHint`).
- **The palette** finds objects and goes there: groups *Waiting on you*, *Go to* (asks, quests, sessions, workspaces,
  repositories, accounts, roles, connectors, plugins, Settings' rows), *Do* (*New ask*, *Sign in <account>*, *Add a
  repository*, …), *Found in Knowledge* (with the tier that answered); an empty query shows the waits and recent objects; a
  pasted link goes; the Ask Daoris row opens the side bar with the words. Quick Ask goes.
- **No menu bar.** The mark's app menu holds the window acts with no page of their own (new window, zoom, theme, language,
  shortcuts, about, quit). Every verb has a visible button and a palette row; a census fails a command with no visible door
  unless it says why it is palette-only.
- **The object menu** (right-click or ⋯) starts with *Open*, *Open in its own window*, *Copy link*, then the acts its page
  shows, never others.

## 5. What waits on the owner

### 5.1 The one number

How many things wait on the owner, across every workspace, not deferred: the waits pill in the title strip, the taskbar
button (9+ past nine) and the window's title. Nowhere else draws it as a standing number; a breakdown (the scope menu,
Overview) appears on demand. Amber and ◆ belong to it, a wait's card and its mark.

### 5.2 The wait kinds

The frame counts an attention row only when its kind is one of these, or when no appointed role can take it yet:

| Kind | 中文 | Arises when |
|---|---|---|
| Question | 问题 | a direction or target only the owner can settle (an ask's words, a requirement, a plan question) |
| Go-ahead | 放行 | an act the repository's declaration keeps the owner's: a production write, a push outside, deleting the owner's data, trusting a folder where the declaration does not decide it |
| Release | 发布 | a release to a stage the owner keeps, or a set-up shown for the owner's look |
| Sign-in | 登录 | an account or a remote is signed out and work needs it |
| Money | 费用 | an account paid per use nears the budget you set for its week or month, or its prepaid balance runs out (a subscription carries no money budget, §13.6) |
| Confirm it was you | 确认是你 | a terminal's press asks to act as the owner (D156's door) |

One census in the driver, `AttentionOwnership` (a unit of its own in `Modules/Loop/`), classes each of today's 36 attention
kinds as the owner's or the agents' with its doctrine reason, and fails a new kind with no class (UISH6). In short:
`signed-out`, `git-held` and an `account-wait` that only a sign-in clears are Sign-in; `confirmation` is Confirm it was you;
`go-ahead`, `git-ask`, `trust` and `space` are Go-aheads where the declaration keeps the act the owner's; `step`, `set-up` and
`integration` are Releases where the owner holds the step's role; `departure`, `intake`, `plan-question`, `design-question`
and `unanswerable` are Questions; `policy-hold` is Money where the hold is a per-use account's budget; `left` and `parked-quest`, once the agents
gave up, are *gave up* lines in *Since you left* (§5.4), never waits; `memory` beyond the leave the owner set is a Go-ahead.
Each conditional kind names its other side: a go-ahead, git ask, trust or deletion the declaration does not keep goes to its
approver role; a `step`, `set-up` or `integration` the owner does not hold goes to that step's role (an acceptor's step to the
acceptor); a `policy-hold` that is no budget goes to the supervisor. The rest (`parked`, `helper-ended`, `answer`,
`request-declined`, `proposal`, `rule`, `helper`, `hand-over`, `permission`, `quiet`, `unclosed`, `work`, `opinion`, `review`,
`steward-stopped`, `second-check`) are the agents': the supervisor's, the steward's or the reviewer's. `proposal` (an ask's
split into quests) is the agents' (D341). `permission`, `answer`, `review`, `set-up` and `rule` stay the owner's only where a
setting the owner holds marks them so.

An agents' row is counted, amber, until a role that takes its kind is appointed in its workspace, and it says so ("No role takes
this yet: appoint one ▸"). Nothing that holds work is hidden while the roles are built.

### 5.3 The wait card

One composite, `WaitCard` (§8.3), is every wait's card: on Overview, in a session's dock, on an ask's and a quest's page,
pinned on an account's row or a repository's page. One wait has one id; answered in one place it is answered everywhere.

```text
┌▌◆ Release · 2.4 to beta ─────────────────────────────────── infra · holds 2 · 3 h ┐  edge: you; field you-soft when focused
│ Ships contract v2 (api-server) and the CSV export (web-app) to beta.              │  the asker's words (AuthoredText)
│ Yes   deploys to beta now; one press rolls it back for 24 h.                      │  from the act's definition (§6)
│ No    stays on stage; the rest goes on.                                           │
│ Look  release notes ↗ · run #418 ↗                                                │  EntityChip
│ Known ✓ checks 46/46 · 13:40 · ✓ reviewed: Codex · gpt-5.1                        │  KnownBy
│ From  release manager · Claude · sonnet-4.5 · asked 12:18 · ask: Export to CSV    │  AgentChip
│ [ Approve release to beta  Y  ▮▮▮▯ hold ]  [ Hold  N ]   Reply R · Not mine M · Later L │  held: cannot be taken back
└───────────────────────────────────────────────────────────────────────────────────┘
```

- Its parts are required props: the title (a question or an imperative); *what it is* (the asker's words); the options, or
  *Yes* and *No*, each with its effect; *Look*; *Known*; *From* (role · repository · ask; the asking agent · the model that ran
  · when); *Meanwhile* where other work goes on; a deadline only from a source. A field the asker did not supply reads "not
  said by its asker" and is counted (`CARD_FIELDS`, UISE2).
- Its layouts: whole, one line, pinned. Its states: arriving, focused, in grace (*Undo*), answered (its outcome line, 10
  minutes, then *Answered today*), queued offline, not sent (it changed), cleared (moot), deferred.
- *Not mine* (M) is offered only on a kind the owner may hand over, never a sign-in, deleting their data or Confirm it was you;
  it writes the act's holder in the one record *Give this act to…* edits (§6, §13.4), under the same rules, and says where to
  take it back.
- Keys answer as buttons do (amending D152 §3, which kept keys off acts that end or remove): a key whose act can be taken back
  runs with its 5-second *Undo*; a key whose act cannot is held 800 ms, as its button is. *Later* (L): 1 h, this evening,
  tomorrow 09:00, when I'm back; it replaces *Set aside*, and a deferred wait leaves the count and returns marked "back from
  later".
- Waits with one cause merge into one card that "holds n" and lists them (one signed-out account holding four starts). Order:
  a real deadline first, then what it holds, then age; the kind never sorts. The order holds still while the pointer or the
  focus is in the queue; an arrival shows a *1 new above* pill.

### 5.4 With the agents

Rows the agents take show on Overview under *With the agents* (交给智能体), uncounted and without amber: who answers each and
how it went. A role's answers and the supervisor's courses since the owner left are *Decided without you*, each with its role,
maker, model and one line of why, and *Reverse…* (which opens New ask with "Reverse: <the decision>" written in) or *Overrule*
while a course is still open. Work the agents could not finish (a step failed after its retries and another agent's try) is a
*Gave up* line in *Since you left*, with what was tried and *Try again* or *Ask about it*, uncounted and without amber (doctrine
rule 1: reported as gave up, never asked); it reaches the owner as a wait only where a direction question names it.

### 5.5 Away and back

- The desktop keeps one presence record for the machine: the last input in any main window, the absences, and the *caught-up*
  mark. Away begins after 30 minutes with no input in any main window, or when every main window is minimised or closed.
- Back after **2 h or more**: the main window in front goes to `/overview/waits/<first>`, and Overview shows the full away banner
  ("You were away 6 h 12 m (09:10–15:22) · ◆ 3 · ✓ 7 · ▲ 1 fixed without you", *Back to where you were*, *Caught
  up*). Back after **30 min to 2 h**: the place stays and the away line opens under the title strip.
- *Caught up* (C, or the button) moves the mark to now in every window. A second absence before it adds to the first.
- The mark is what Overview's *Since you left* reads, and what a place with no read mark of its own uses for its "new since"
  divider. A session's own read mark (rule 38) stays the session's.

### 5.6 Announcements

The operating system is told only of counted waits that hold work, batched, and held in the owner's quiet hours (Settings ›
Notifications, §14). A notification opens `/overview/waits/<id>` in the main window that holds its scope; it never answers.

### 5.7 Where Notifications' parts go

| Today's group | Becomes |
|---|---|
| *What needs you* | Overview's *Waiting on you* (owner kinds) and *With the agents* (the agents') |
| A terminal's press (`person/Confirmations.tsx`) | a *Confirm it was you* wait (D156's door unchanged: no answer without the person key) |
| *Messages* | unread marks where they belong and a *Since you left* line (`messagesRead` stays) |
| *Done and failed* | *Since you left* lines; a step the agents gave up on reads *Gave up* with what was tried (§5.4) |
| The steward's, Ask Daoris's and the terminal's lines | *Since you left* lines with their acts |
| *Set aside* | *Later* |
| `daoris notices` | `daoris waits` (`--all`), the same rows as JSON |

## 6. Acts for any actor

The owner's direction (2026-11-19): every owner act is an act any actor can be given. D325 already made a role's act with an
outside effect its holder's tool, and APPROVEACT1 made a release approval the approver role's act, an agent's tool where the
place casts the approver to an agent and the owner's card where it does not. This extends that to every act the window lets the
owner perform.

- **An act is defined once** (`OwnerActs`, a unit of its own in the driver's modules), over the doors that exist: D325's role
  act handed to its holder as a tool and settled by its receipt, APPROVEACT1's `approve-release`, the window's routes and the
  `daoris` verbs. FOUNDATION1's one Act contract (`docs/2026-11-14-foundation-design.md` point 2), when it is built, adopts these
  definitions as its first kinds rather than adding a second registry. Each has its id; its verb and its effect sentence
  (catalogue keys); whether it can be undone (`grace`: the press is held 5 s before it is sent, with *Undo*) or not (`hold`: a
  held press, 800 ms, its consequence on the control); what it needs (a role, a sign-in, a place's declaration, a budget);
  the role that holds it at each place (the place's cast, D304, D318; the owner by default for the closed list); and its
  doors: the window route, the `daoris` verb (rule 34) and, where an agent may hold it, the tool its holder's session is
  handed.
- **The acts**, first: answer a question; give or refuse a go-ahead; approve or hold a release; sign in an account or a remote;
  raise a budget or hold work at it; delete the owner's data (history, files, a branch, a kept entry); rewrite history;
  confirm it was you;
  reverse a decision made without the owner; keep or drop a workflow change to the owner's part; accept a check or a landing
  the owner keeps. A census fails a press that performs an owner act without a definition, and a definition with no door.
- **The window is the human's door.** `WaitCard`'s presses and `Button`'s held press take an act, and read from its definition
  their label, consequence, grace or hold, and whether the window's actor may press it (the person key where D156 asks for
  one). A press that the definition says another actor holds reads "held by <role> (<maker · model>) · Overrule".
- **An agent actor is configured to hold an act** on Company › Roles › *What stays yours* (§13.4): the act is given to an
  appointed role at a place, and that role's holder performs it through its tool (D325's tool door, journaled with its
  receipt, settled by its receipt), as APPROVEACT1's `approve-release` does. Sign-ins and deleting the owner's data can be
  given only where the agent holds the credential or the owner's leave; the definition says what it needs. A wait card's
  *Not mine* writes the same record. Where an act belongs to a step, it is handed only to the session holding that step
  (ACTORFLOW1's open item, which UIRP29 builds on).
- **Person-only acts.** *Confirm it was you* (D156's door) is the person's alone and is never given: its point is that Daoris
  cannot tell the person from an agent there. The census marks it so, *What stays yours* shows it without *Give this act to…*,
  and a request to give it is refused.
- **What goes:** the press-then-confirm dialog and `InlineConfirm` (a second question for an act); arrival toasts; acts that
  only a window press can perform.

## 7. The design language

### 7.1 Colour roles

Components name roles, never base colours; only `src/Daoris.Web/src/tokens.css` holds a value; its first `:root` block is
light and its second dark, then the forced blocks (`ChromePaletteTests` reads the first two). Names stay where the job stays.
The neutrals are near-neutral greys with a faint cool lean, so beside the blue accent the only warm things are the owner's
amber and a failure's crimson (D351).

| Token | Light | Dark | Job |
|---|---|---|---|
| `page` | `#F6F7F8` | `#111316` | the window behind panes |
| `raised` | `#FFFFFF` | `#191B1F` | panes, cards, rows |
| `sunken` | `#EEEFF1` | `#0B0C0E` | code wells, the steer box, the owner's own lines |
| `overlay` | `#FFFFFF` | `#212429` | menus, popovers, sheets, palette, toasts |
| `hover` | `#E9EAED` | `#292C32` | a row or quiet control under the pointer |
| `scrim` | `rgb(15 17 20 / .40)` | `rgb(0 0 0 / .55)` | behind a sheet or the palette (the light shadows share the base) |
| `line-subtle`, `line`, `line-strong` | `#E4E5E8`, `#D1D3D8`, `#7B7F88` | `#282B30`, `#383B42`, `#787C86` | row separators; card edges; control edges (≥ 3:1) |
| `ink`, `ink-soft`, `ink-faint` | `#17191C`, `#4D5158`, `#62666E` | `#EAECEF`, `#B1B5BD`, `#959AA3` | text, secondary, metadata (≥ 4.5:1) |
| `ink-disabled` | `#8E929A` | `#6D717A` | a disabled control's label only (about 3:1, no opacity over it), its reason in a tooltip |
| `accent`, `accent-ink`, `on-accent`, `accent-soft` | `#2F5BD3`, `#2346A8`, `#FFFFFF`, `#E1EAFC` | `#5F86F2`, `#93AEF8`, `#0E1014`, `#1C2A4D` | primary button, focus ring, selected rail; links and words that act; label on accent; selected row and selected text |
| `live`, `live-ink`, `live-soft` | `#2F5BD3`, `#2346A8`, `#E1EAFC` | `#5F86F2`, `#93AEF8`, `#1C2A4D` | the ● mark; *Moving*; a live band (the accent under a second name, its field the same value) |
| `you`, `you-ink`, `you-soft`, `on-you` | `#B27400`, `#875000`, `#FFF3D6`, `#0E1014` | `#F0B03A`, `#F6C567`, `#36280B`, `#0E1014` | only what waits on the owner: ◆, the pill, a wait card's edge and field, the *yes* button, the taskbar's count |
| `fail`, `fail-ink`, `fail-soft`, `on-fail` | `#CF2D3F`, `#A81D2F`, `#FDEBED`, `#FFFFFF` | `#F05C6C`, `#FF9AA3`, `#3B161C`, `#0E1014` | ▲, failure words, a failed step's band, a danger label (a crimson, 51° from amber light, 61° dark) |
| `ok`, `ok-ink`, `ok-soft` | `#1F8050`, `#19683F`, `#E5F4EC` | `#49B279`, `#7AD3A0`, `#11301F` | ✓ (never a large fill), *Landed*, a passed check's band |
| `ident-*` | as `tokens.css` | as `tokens.css` | plugin monograms: identity, never state |

There is no neutral state colour: waiting on others, queued, ended and stale use `ink-soft` and `ink-faint` with their own mark
shapes. A glyph (an icon, the mark) wears the inks, never the accent: `ink-soft` beside the blue words of a link or a door,
`ink` where chosen, its place told by the rail and the field (`design/glyphInk.test.tsx` holds every story to it). A declined or stopped outcome is neutral (–), not red; red means failed. Today's `st-open`, `ink-open` and `warn` are
split site by site into `you` (where it waits on the owner) or neutral, and stay as listed aliases until UIDS18 deletes them.

**Contrast**, computed on these values (the whole table is held by `design/tokens.test.ts`): every text pair is at least 4.5:1
on `page`, `raised`, `sunken`, `overlay`, `hover`, `accent-soft` and the role fields in both themes (worst: `ink-faint` 4.8 light,
5.0 dark); every mark, focus ring and control edge at least 3:1 (worst: `line-strong` 3.3 / 3.3, `you` 3.2 light); labels on
solid fills at least 4.5:1 (`on-you` on `you` 4.9 light); `ink-disabled` at least 3:1 on `raised` and `overlay`. A disabled
control is told by that label in the atoms' neutral frame, never by an opacity over the whole control. The focus ring is
`accent`, 2 px; selected text is `accent-soft` under `ink`.

### 7.2 Type: one scale for Latin and Han

| Token | Size | Line, Latin | Line, Han | Weights | Use |
|---|---|---|---|---|---|
| `caption` | 12 px | 16 | 18 | 400 | metadata, known-by, times, badges, the status strip |
| `small` | 13 px | 18 | 20 | 400, 600 | list rows, card meta, menus, controls |
| `body` | 14 px | 20 | 22 | 400, 600 | card text, the timeline, forms |
| `heading` | 16 px | 22 | 24 | 600 | section heads, an object's title in a pane head |
| `display` | 20 px | 28 | 30 | 600 | the one title of a screen; a number, with tabular figures |
| `mono` | 13 px | 20 | 20 | 400 | code, logs, hashes, branches, paths, never a sentence |

Each step's line height is a variable that `:lang(zh)` resets; the window's words follow the window's language, authored content
its own (`AuthoredText` detects Han and sets `lang`). Stacks: UI `"Segoe UI Variable Text", "Segoe UI", system-ui, "Microsoft
YaHei UI", "Microsoft YaHei", "PingFang SC", "Noto Sans SC", sans-serif` (the Latin face first, so Latin words and numerals in a
Chinese line keep it); under `:lang(zh)` an alias `"Daoris Han Punct"` over the Han faces for U+2018–201D, U+2026, U+00B7 and
U+2014, so curly quotes, the ellipsis, the middle dot and the em dash stay full width; mono `"Cascadia Mono", Consolas,
"Sarasa Mono SC", "Microsoft YaHei UI", ui-monospace, monospace`. The serif goes.

The CJK rules: Han never under 12 px; two weights, 400 and 600, and no italics; no tracking on Han and uppercase only for Latin
labels; Han–Latin spacing by the browser at render time (`text-autospace`), never inserted into text; `line-break: strict` under
`:lang(zh)`; a column, not a paragraph, may stop at the 72ch measure; a label is laid out against the longer of its two languages
(the glossary's kind budgets, NAME1b); tabular numbers in tables, meters, money and times through `locales/format.ts`; status marks
drawn as SVG, never font glyphs; Chinese means Simplified, in the console register; a quest is 委托.

### 7.3 Space, sizes, density

- A 4 px grid; spacing steps 0, px, 0.5, 1, 1.5, 2, 3, 4, 5, 6, 8, 10, 12, 16 (Tailwind units).
- Named sizes: `control-sm` 28 px (the hit-target floor), `control` 32, `row` 32, `row-2` 44, `row-table` 28, `titlebar` 36,
  `statusbar` 24, `rail` 72 / `rail-narrow` 48, `icon-sm` 12 / `icon` 16 / `icon-lg` 20, `mark` 16 (glyph 10).
- Cards pad 12 by 16; 16 between stacked cards, 8 between cards in a queue.
- **One density.** The owner's size control is zoom (ZOOM1: Ctrl +/−, Settings › Appearance). A second density would double
  every baseline and every state.

### 7.4 Radii, borders, elevation, layers, motion

- Radii: `control` 4, `card` 6, `overlay` 8, `full` for badges and tags; rows have none. Borders 1, 2 (the focus ring, a
  selected row's rail) and 3 (a focused wait card's or a failed card's edge, `Card`'s `edge` alone); no other width
  (`design/edges.test.ts`); a card wears no left edge otherwise (UIDS19).
- Borders separate; shadows only on overlays: `shadow-popover` and `shadow-sheet`, a value per theme.
- Layers `--z-sticky` 1, `--z-sheet` 10, `--z-palette` 20, `--z-popover` 30, `--z-toast` 40, `--z-tooltip` 50.
- Motion: `--duration-quick` 120 ms (hover, press), `--duration-move` 180 ms (expand, a card leaving, a wait arriving),
  `--duration-sheet` 240 ms; `--ease-enter` and `--ease-exit`. The ● mark breathes three times on a change of state, then is
  still (FREEZE1: an endless pulse cost a quarter of a GPU core). Reduced motion: every duration 0 except cross-fades of at
  most 80 ms; the visual run forces it.

### 7.5 Widths

Window breakpoints `narrow` 45rem (720), `mid` 64rem (1024), `wide` 80rem (1280), read by the shell only; regions use container
queries. Containers: `list` 18.75rem (300), `side` 25rem (400), `sheet` 40rem (640), `sheet-form` 30rem (480), `palette` 40rem,
`measure` 72ch, `stack` 32rem, plus each screen's named thresholds (§3.2). A component never writes `@max-[…]`.

### 7.6 The status marks

Each is a drawn 10 px glyph in a 16 px box, a different shape for each, so they read in grayscale and with colour blindness, with
its word from the `design` catalogue or a more specific word from the caller:

| Mark | Meaning | Token | Word (en / zh) |
|---|---|---|---|
| ● | moving | `live` | Moving / 进行中 |
| ◆ | waiting on you | `you` | Waiting on you / 等你处理 |
| ○ | waits on another session, helper or connector | `ink-soft` | Waiting on … / 等待… |
| ◌ | queued | `ink-faint` | Queued / 排队中 |
| ▲ | failed (↻ n/m while retrying) | `fail` | Failed / 失败 |
| ✓ | finished with an outcome | `ok` | Landed · Released · Passed / 已合入 · 已发布 · 已通过 |
| – | ended without one (stopped, declined, superseded) | `ink-faint` | Stopped / 已停止 |
| ◐ | stale, shown as of a time | `ink-faint` | As of 14:58 / 截至 14:58 |
| ‖ | paused (by you, or waiting for a window) | `ink-soft` | Paused / 已暂停 |

A group shows its most urgent: ◆ > ▲ > ● > ○ > ◌ > ‖ > ✓ > –, with ◐ drawn beside the others.

### 7.7 Words

| English | 中文 | Note |
|---|---|---|
| Overview | 总览 | kept; its glossary *means* becomes "what waits on you, what is moving, what happened since you left" |
| Asks, Workspaces, Company | 需求, 工作区, 公司 | now places; *Company* gets its glossary row |
| All workspaces | 全部工作区 | replaces "Every workspace · 2" |
| {{count}} waiting on you; Nothing waits on you | {{count}} 个等你处理; 没有等你处理的事 | one phrase per job; *What needs you* goes |
| With the agents; Decided without you; Cleared without you | 交给智能体; 已代你决定; 已自行解除 | |
| While you were away; Since you left; Caught up; Back to where you were | 你离开期间; 你离开之后; 已看完; 回到刚才的位置 | |
| Not mine; Later | 不归我定; 稍后 | *Later* replaces *Set aside* |
| Search, go to or run a command | 搜索、跳转或执行命令 | the palette's placeholder |
| Confirm it was you | 确认是你 | a wait kind |
| Live; As of {{time}} | 实时; 截至 {{time}} | the status strip |
| Summary | 概要 | a workspace's or repository's first tab; *Overview* stays the place's alone |
| connector; What stays yours; the leave; Said in more than one repository | 连接器; 归你决定; 授权范围; 多个仓库同归 | Company's and Knowledge's new names |
| *Notifications* | 通知 | only Settings' domain of the computer's announcements; the view goes |

## 8. The component library

### 8.1 Layers and folders

`src/Daoris.Web/src/design/` holds `primitives/` (one unit per component, its story and test beside it; today's `atoms/` move
here), `composites/` (built from primitives, still presentational), `patterns/` (layouts and keyboard models for whole regions),
`regions.ts` (each region's width at each window width) and `canvas/` (kept, D186, D224). Screens import from the one
`design/ui.tsx` barrel, so the move touches no screen. `primitives.test.ts` keeps `@radix-ui/*` inside `design/primitives/**`;
`presentational.test.ts`, widened to all of `design/**`, keeps out `bridge/`, `data/api`, TanStack Query and screens' folders.
A domain's composite (`SessionRow`, `QuestTrackRow`) lives in its screen's folder and is built from the library (D231).

**Radix stays the one headless layer** and gains popover, hover card and tabs; the library owns the combobox, the tree and the
list keys; React Aria Components is rejected, Base UI is the named exit (D345).

### 8.2 Conventions

State in by props, acts out by callbacks, never a fetch. Words come in through the catalogues: a visible string is a prop the
caller translated; the library's own chrome words (*Close*, *Undo*, *Later*, *Not mine*, the marks' words) come from a `design`
catalogue area; authored content passes as `AuthoredText` and never through `t()`. Closed unions, never domain enums (a domain
maps its states to `StatusMark`'s nine in its own folder). `AgentChip` requires `ran` by type. Times, money and counts go
through `Timestamp`, `Money` and `locales/format.ts`. `className` places a component, never restyles it. Test hooks are
`data-state`, `data-mark`, `data-kind`. Every component has a `States` story, tagged `visual`, that is its picture baseline.

### 8.3 Primitives, composites and patterns

| Layer | Component | What it is | Row |
|---|---|---|---|
| primitive | `Text`, `Heading`, `AuthoredText`, `Icon` (one object-icon table), `Divider` (rule, step rule, dashed new-since), `Timestamp`, `Money` (an amount, with its period where it has one), `KeyHint`, `Tag`, `Fold` (a titled section that opens in place, a summary when closed) | type, words, marks of time and money | UIDS8 |
| primitive | `StatusMark` (the nine marks, with a word and an optional count), `Badge` (amber counts only waits) | state | UIDS9 |
| primitive | `Button` (primary, yes, danger, secondary, quiet; sm, md; busy keeps its label; `disabledReason`; `hold` with its consequence line; `keyHint`), `IconButton`, `TextField` (text, search, inline reply, time), `TextArea` (plain, composer with an attachments slot; Ctrl+Enter sends, never while composing), `SelectField`, `CheckField`, `ChoiceRow`, `Segmented` | controls | UIDS10 |
| primitive | `Menu`, `ContextMenu`, `Tip`, `Popover`, `Peek` (an entity's hover card), `Tabs` (a select at narrow), `Sheet` (composer 640, form 480, full under 1024), `PaletteFrame`, `Toast` (ok, error, undo with its countdown) | overlays, in one layer order | UIDS11 |
| primitive | `Combobox` (chip, field or palette trigger; groups; async rows; composition kept) | pickers, the palette, the scope switcher | UIDS12 |
| pattern | `SplitView`, `ListPane`, `ListRow` (one line 32, two lines 44, with a status line), `PaneHeader`, `StoryAndRail`, `Table`, `TreeList` | lists beside details | UIDS13 |
| composite | `RegionState` (empty, loading, failed, stale, offline, stopped), `Skeleton` | a region's states | UIDS14 |
| composite | `AgentChip`, `KnownBy` (check, review, read, stated, yours, default, inherited, stale), `EntityChip` with `Peek`, `Meter` (limit cells, budget bar, share bar; unknown never zero), `FactList` (read-only facts, each with its `KnownBy` and one act), `DigestLine` (an outcome, a decision or a clearing, with its mark, where, when, how known and *Reverse…*) | who, how known, how much | UIDS15 |
| composite | `WaitCard` (§5.3) and the queue's keys | every wait | UIDS16 |
| composite | `TimelineRow`, `StepTrack` (across; down, with a body per step), `NewAbovePill` | stories and steps | UIDS17 |

### 8.4 Domain composites, by group

Built by the rows that need them, from the library: the frame's `Rail`, `ScopeSwitcher`, `SearchField`, `WaitsPill`,
`AppMenu`, `StatusStrip` (with its item registry), `Breadcrumb`, `AwayBanner` (full and slim), `SideBar`, `ShortcutSheet`,
`CommandPalette` (its groups), `SecondaryHeader`; Overview's `OverviewBand`, `AskGroup`; Sessions' `SessionRow`, `FactsRail`,
`ChangeList`, `CostLine`, `DetailsSheet` (a `Sheet`); Asks' `AskRow`, `QuestMarks`, `QuestTrackRow`, `EdgeLine`,
`RequirementRow`, `TestedTogetherRow`, `ActRow`, `RequestRow`, `PassageStep`, `PlanningFeed`, `ActivityFeed`, `SteerBox` (a
`TextArea` composer); workflows' `ConstraintRow`, `VerdictRow`, `RunRow`, `LessonRow`, `VersionDiff`, `LineLegend`; Workspaces'
`KnowledgeItem`, `FindingRow`; Company's `AccountRow`, `ChoiceLadder`, `ConnectorRow`; Settings' `UpdateLine`,
`RepositoryIntake`, `TimeRangeField`.

### 8.5 One name each

The plans' names and aliases map onto the library so:

| The plans said | One name |
|---|---|
| DecisionCard (UISE), the waits' card (UISH), WaitCard (UIWF, UIRP), WaitingCard (today) | `WaitCard` |
| StatusMark, StateMark, Status, state pill, `Dot`, `DotMark`, `Pill`'s tones, MarkCounts | `StatusMark` (with a count) |
| KnownByChip, KnownBy, AsOf, SourceMark, InheritMark, FreshnessItem | `KnownBy` |
| RegionNotice, RegionStopped, `EmptyState`, `SkeletonRows`, `useErrorNotify` | `RegionState`, `Skeleton` |
| HoldButton, HoldToConfirm | `Button` with `hold` |
| UndoToast | `Toast` (undo) |
| Drawer, DetailsSheet's frame, the sheet | `Sheet` |
| StoryRow, story entry | `TimelineRow` |
| RuleDivider | `Divider` (step rule, new since) |
| LimitMeter, MoneyFigure, ShareBar, `Tile` | `Meter`, `Money` |
| PageTabs | `Tabs` |
| ListHead, ListDetail, StatusNavRow | `PaneHeader`, `SplitView`, `ListRow` (two lines) |
| StepPath, Track, the step rail | `StepTrack` (down, with bodies, serves Get started) |
| DecidedLine, DigestLine | `DigestLine` |
| TokenLine | `CostLine` (use against a plan's windows, tokens, and money where it is real, §13.6) |
| DeskBand | `OverviewBand` |
| AttachmentChips, Composer | `TextArea` (composer, attachments slot) |
| PaletteGroups, QuickPanel | `CommandPalette` over `PaletteFrame` and `Combobox` |

## 9. Overview

**For:** the owner's first look, often after hours away: what waits on them (answered here), what is moving, what finished or
was decided while they were away, read without opening anything. Today it is a summary whose every line opens *Notifications*,
then four tiles, a quest card and repository bars (`overview/OverviewView.tsx`, `overview/NeedsSummary.tsx`); nothing is
answered on it and nothing says what finished.

Wide, 1600 px (no list pane; three columns from 75rem of main width, two from 56rem with the waits on the left and *Since you
left* above *Moving now* on the right, one below):

```text
┌─────┬──────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│     │ Away 6 h 12 m (09:10–15:22) · ◆ 3 · ✓ 7 landed · ▲ 1 failed, then fixed · 2 decided without you [Caught up C]│
│ ◎   ├────────────────────────────────────┬─────────────────────────────────────┬───────────────────────────────────┤
│     │ WAITING ON YOU  3      + 1 later ▾ │ MOVING NOW  9 sessions     by ask ▾ │ SINCE YOU LEFT · 09:10   by ask ▾ │
│ ◇   │ ╭────────────────────────────────╮ │ Rate-limit the public API  Products │ Landed                            │
│     │ │◆ Release 2.4 to beta?  holds 2 │ │  ● api-server  Check · e2e 9/14 41m │  ✓ Dark mode for settings         │
│ ≡●  │ │  release manager · infra · 3 h │ │                Claude · opus-4.1    │    2 repositories · main · 11:02  │
│     │ │  Ships contract v2 and the CSV │ │  ○ web-app     waits on api-server  │ Failed, then fixed without you    │
│ ▦   │ │  export to beta. Both passed.  │ │  ◌ docs        queued               │  ▲ api-server e2e: a flaky test,  │
│     │ │  Yes  deploys to beta now      │ │ Export reports to CSV      Products │    passed on attempt 2 · 10:40 ↗  │
│ ⚿   │ │  No   stays on stage           │ │  ◆ infra       Release · waits 3 h  │ Decided without you  2            │
│     │ │  Look notes ↗ · run #418 ↗     │ │ Billing fixes                  Work │  · COPY instead of row inserts    │
│     │ │  Known ✓ checks 46/46 · 13:40  │ │  ● billing     Build · 4 files 1h02 │    supervisor · Claude · opus-4.1 │
│     │ │  From Claude · sonnet-4.5      │ │  ▲ config      Check · 2 of 3   3 m │    why: 3× faster, measured       │
│     │ │[Approve release ▮ Y] [Hold N]  │ │ Your conversations                  │    Reverse…                       │
│     │ │ Not mine · Later · Reply · Open│ │  ● engine      you opened it    2 h │ Cleared without you  1            │
│     │ ╰────────────────────────────────╯ │                                     │  – Codex · personal signed back in│
│     │ ▸ ◆ Sign in: Claude · work  40 m   │ Pending 2 ▸   Ended today 23 ▸      │ Used  Claude 61% wk · key $2.30 ▸ │
│ ⚙   │ ▸ ◆ CSV or XLSX first?       2 h   │                                     │ Install  updated to 1.8.3 · 12:00 │
│     │ With the agents 6 ▸     Answered 4▸│                                     │                                   │
└─────┴────────────────────────────────────┴─────────────────────────────────────┴───────────────────────────────────┘
```

Narrow, 900 px, in Chinese (the window's words from the catalogues; the asker's words, an ask typed in English and every
repository and model name stay as written):

```text
┌─────┬──────────────────────────────────────────────────────────────────────────────────┐
│ ◎   │ 你离开了 6 小时 12 分 · ◆ 3 等你处理 · ✓ 7 已合入 · ▲ 1 已自行修复   [ 已看完 ]  │
│ ◇   │ 等你处理  3                                                        + 1 稍后 ▾    │
│ ≡●  │ ╭──────────────────────────────────────────────────────────────────────────────╮ │
│ ▦   │ │◆ 将 2.4 发布到 beta？                                  卡住 2 项 · 3 小时    │ │
│ ⚿   │ │  发布负责人 · infra · 需求：Export reports to CSV                          │ │
│     │ │  同意  立即部署到 beta；24 小时内一键可回滚。                                │ │
│     │ │  暂缓  停在 stage；其他工作照常进行。                                        │ │
│     │ │  依据  ✓ 检查 46/46（13:40）· ✓ Codex · gpt-5.1 已评审                       │ │
│     │ │[ 同意发布到 beta ▮ Y ]  [ 暂缓  N ]          不归我定 · 稍后 · 回复… · 打开 ↗ │ │
│     │ ╰──────────────────────────────────────────────────────────────────────────────╯ │
│     │ ▸ ◆ 登录：Claude · work                                      卡住 1 项 · 40 分   │
│     │ 交给智能体 6 ▸     今天已答复 4 ▸                                                │
│     │ 你离开之后 · 09:10 起                                                按需求 ▾    │
│     │ ✓ 设置页暗色模式 — 已合入 2 个仓库 · 11:02 · ✓ 检查 31/31                        │
│     │ ▲ api-server e2e 偶发失败 — 第 2 次尝试通过 · 10:40 ↗                            │
│     │ 已代你决定 2 项 ▸    已自行解除 1 项 ▸    用量 本周 61% ▸                        │
│     │ 进行中  9 个会话                                                    按需求 ▾     │
│     │ ▸ 公共 API 限流          ● ○ ◌  api-server 检查 · e2e 9/14            41 分      │
│ ⚙   │ ▸ 账单修复               ● ▲    config 检查失败 · 第 2 次，共 3 次     3 分      │
└─────┴──────────────────────────────────────────────────────────────────────────────────┘
```

- **Waiting on you**: every owner wait from every workspace, a `WaitCard` each, tagged with its workspace when outside the
  scope; wide, the first card whole and each next whole while the column has height, then one-line rows; narrow, the focused
  card whole and the rest one line each (a one-line wait opens before it is answered). Foot lines *+ n later*, *With the
  agents n ▸*, *Answered today n ▸* (today's presses, a read the driver keeps).
- **Moving now**: the sessions in Working, Pending and Failed-retrying, grouped by ask (switches: by repository, by state);
  *Your conversations* for those the owner opened; a group head with its most urgent mark and workspace; a row with its mark,
  repository, step or whom it waits on, the model that ran, its time in state; a teammate's tagged *on a teammate's machine*,
  with no press; narrow, a group folds to its marks and its most urgent row. Foot *Pending n ▸*, *Ended today n ▸*.
- **Since you left** (after *Caught up*: *Today*): *Landed* (and released), *Failed, then fixed without you*, *Gave up* (§5.4), *Decided without
  you* (with *Reverse…*), *Cleared without you*, *Used* (each subscription's share of its windows and the tokens by the model that ran, "as each agent reported";
  money only for work on per-use accounts),
  *Install*; grouped by ask; a section past five lines ends *+n more ▸*; more than a day away is grouped by day. Composed in the
  driver from records already kept (`OverviewSince`), never by a model.
- **States**: first run, one sentence and *Get started* (*Set up Daoris: n steps left · Continue*); quiet, "Nothing waits on
  you. Last answered 14:02." and *New ask*; loading, skeletons per band after 300 ms; failed, the band says what, since when,
  that it retries, *Try again*, and keeps its rows marked ◐; stale, "◐ as of 14:58" on the band's head, a press made meanwhile
  reads *under way*; in a browser, the waits say they are the app's and Moving shows the service's asks without sessions.
- **Presses**: Y / N / 1–4, R reply, M *Not mine*, L *Later*, Enter open, J / K move, C *Caught up*; a Moving row opens its
  session at its read mark; a group head opens its ask; a *Since* line opens its object at its mark.
- **Goes**: the tiles, the *Outstanding* quest card, the repositories card, `NeedsSummary`, *Set aside* and *Set all aside…*.

## 10. Sessions and the session panel

D320 stands: one panel, a fixed head with its track, one timeline, then the live band, the dock and the composer. These rows
draw its final look and close three gaps: which model ran, what changed in the repository, and what a card says.

### 10.1 The list

Each session reads in two calm lines (`SessionRow` on `ListRow`): its mark and title and its time in state; then its repository,
its step n/m or whom it waits on, and the model that ran. The row's word is the state line's (never a word contradicting its
group). Grouped by ask by default (the choice remembered per view), with *Your conversations*, *Ask Daoris* (its conversations,
§15.4) and *Ended today n ▸*. The selection is a tint with the accent's rail; no monospace on words.

### 10.2 New conversation

Says before the start which agent, model, account and tree it would take now (`START_PREVIEW`, deciding nothing), through
`AgentChip`; *Make it an ask ▸* opens New ask with the words (work across repositories is an ask); five recent conversations
under the box.

### 10.3 The panel

Wide, the list open, the facts rail at 68rem or more of main width:

```text
┌───────────────────────┬──────────────────────────────────────────────────────────────┬──────────────────────────────┐
│ SESSIONS  9      + ⋯  │ ● Working · 41 m · running the e2e suite      [Pause] [Stop] ⋯│                              │
│ by ask ▾   ⌕ filter   │ Rate-limit the public API › token-bucket limits              │                              │
│ Rate-limit public API │ api-server · daoris/q41 · Builder · Claude · opus-4.1 · home │                              │
│▌● api-server     41 m │ Work ✓ › Review ✓ › Check ● 9/14 › PR · › Approve ◆ you › Release · 3/6                    │
│   Check 3/6 · opus    ├──────────────────────────────────────────────────────────────┼──────────────────────────────┤
│ ○ web-app         1 h │ ┄┄ Since you left · 6 h 12 m ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄ │ CHANGES  6 files +248 −37    │
│   waits on api-server │   3 turns · 6 files · 9 commands, 1 failed · 1 wait on you   │ M src/limits/bucket.ts +121  │
│ Export reports    ◆   │ ── 1 Build · Claude · opus-4.1 · 09:12–10:31 ✓ ───────────── │ A src/limits/refill.lua +38  │
│  ◆ infra          3 h │ You  Rate-limit /v1 per key: 100 a minute, burst 20.         │ + 2 more ▸  3 commits        │
│ Your conversations    │ ▸ Turn 4 · “Token bucket in Redis” · 33 calls · 6 edits 22 m │ ✓ checks 46/46 · 13:40       │
│  ● engine         2 h │   ↗ asked the knowledge helper  answered ↓                   │ Review the changes ▸         │
│ Ended today  23 ▸     │   ⇢ handed “show remaining quota” to web-app ↗               │ WITH                         │
│                       │ ── 2 Review · Codex · gpt-5.1 · 10:31–10:52 ✓ ────────────── │ ⇢ web-app · quota ↗          │
│                       │   ⚖ second opinion, Codex · gpt-5.1, on burst size: agreed   │ ↗ knowledge helper · 2 asks  │
│                       │ ✗ $ npm test · exit 1 · 2 failed ▸                           │ MODELS AND USE               │
│                       │ ── 3 Check · since 15:18 ● ───────────────────────── ↓ 3 new │ Claude · opus-4.1 · 6 turns  │
│                       │ RUNNING 2  ● Turn 7 · editing bucket.ts · 1 m 10 s           │   1.3 M in · 88 k out · 4% wk│
│                       │            ▶ Check: e2e · 9/14 · 4 m     Follow · Open · Stop│ Codex · gpt-5.1 · review     │
│                       │ WAITING ON YOU 1  ◆ Approve release 2.4 to beta? (the card)  │   190 k in · 1% of the week  │
│                       │ ╭ Message the agent… it reads it after the current step. ──╮ │ as each agent reported 15:20 │
│                       │ ╰ ⊕  Claude · opus-4.1 · high · home        [Send Ctrl ⏎]  ──╯ │ Details ▸                    │
└───────────────────────┴──────────────────────────────────────────────────────────────┴──────────────────────────────┘
```

Narrow (852 px): no list, no rail; the head's third line carries the changes as one fact ("6 files +248 −37 ▸"); the band folds
to *Running 2 ▸*; the dock shows one card whole and the rest one line; the rail's sections live in the *Details* sheet.

- **The head** (at most four lines): the state (`StatusMark`, its word, time in state, what it does now) with *Pause*,
  *Resume*, *Stop* and ⋯ (*Details*, rename, pin, *Raw output*, delete); the title as *ask › quest*; *where and who*:
  repository · branch · role · maker · the model that ran · account (`agentLabel`; the harness version goes to *Details*); the
  track (`StepTrack`, SESSIONVIEW1g's data), a chip a press that scrolls to its rule. *Details* is a sheet over the timeline's
  top, never pushing it down.
- **The timeline** (the only part that scrolls): every row names its actor, and its model where a model acted; the owner's own
  lines on `sunken`; ◇ *Daoris* lines; steps as labelled rules; the dashed *Since you left* rule with its span; *↓ n new*.
  D320's never-folded rows stay unfolded.
- **The facts rail** (state, never events): *Changes* (files ±, commits on the branch, the pull request, checks, *Review the
  changes ▸*), *With* (one line per relation, a jump to its first row and a door), *Models and use* (per model that ran:
  tokens, the share of its account's windows, money only where the account is paid per use; "asked X, ran Y" where they
  differ), *Details ▸*. A file or *Review the changes* opens the panel's **Changes focus**
  (the review frame, diff, patch, handbacks and gates) with a breadcrumb and Esc, as a child opens; the side bar's Outputs view
  goes (D339).
- **The foot**: the live band (*RUNNING n*, each live thing with its time and last line, *Follow · Open · Stop*); the dock (every
  wait of the session as a `WaitCard`, at most 40% of the panel with its count pinned); the composer (`TextArea`), whose chip
  names the model a default resolves to and whose line says when the agent reads the words; *Finish* in ⋯.
- **States**: loading, a skeleton head then the timeline from the read mark; live; waiting on another, ○ with whom and a link;
  waiting on the owner, ◆ naming the oldest wait and how many more; failed retrying, ▲ "attempt 2 of 3" on the step's chip;
  failed given back, its next press a card; finished, ✓ "Landed on main 16:02" or – "Stopped by you 16:10" and the receipt as
  the last row, the composer reading *Go on with this work…*; stale, "◐ No word from this session since 15:31"; a teammate's,
  no press and "Its conversation stays on its machine".
- **Goes**: the head's seven folds (SESSIONVIEW1i), `CoWorkers` and `Pieces` as surfaces of their own, the Outputs view.

### 10.4 The bottom panel

The panel holds the owner's terminal. A session's raw output opens from its ⋯ as a tab named "Raw output · <title> · <maker ·
model>"; terminal tabs are named by folder; the xterm theme comes from the tokens and is held to 4.5:1.

## 11. Asks, quests and workflows

The owner answers and never dispatches. An ask's split into quests is the supervisor's, made and started without a press, shown
under *Decided without you* with *Reverse…* (D341). A quest is read where it is met: as a row of its ask, and on a page that is
its passage through its repository's workflow. A workflow is read as its steps and their roles, and changed by a proposal read
as before and after (D342).

### 11.1 The Asks list

Groups *Waiting on you*, *Moving*, *Not from an ask* (quests no ask holds: a teammate's, one a session sent), folded *Teammates'
asks*, *Paused by you*, *Finished, last 7 days*. An `AskRow` reads its mark, title, each quest's mark (`QuestMarks`), requirements
met, workspace and time; at *All workspaces* a row carries its workspace. The Quests place, its list and its composer go.

### 11.2 An ask's page

Two columns from 64rem of main width, one below with the waits first:

```text
┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ ◆ Export reports to CSV                              Products · since Mon 10:02 · 9% of the week so far        ⋯ │
│ “Owners need to export any report as CSV, with the same columns as the screen. Big ones too (1M rows)…” more ▸ │
│ ▣ report-screen.png  ▤ columns.md      planned by supervisor · Claude · opus-4.1 · revision 2 ▸              │
├────────────────────────────────────────────────────────────────────────┬─────────────────────────────────────────┤
│ QUESTS  3                                             On the map ▸     │ WAITING ON YOU  1                       │
│ api-server  ✓Build ✓Opinion ✓Checks ✓Land ✓Release        Claude·opus │ (the WaitCard: Approve release 2.4)     │
│   contract v2 + COPY export · released to stage 13:02                  │ REQUIREMENTS  4 of 5 met                │
│ web-app     ✓Build ✓Opinion ✓Checks ✓Land ✓Release      Claude·sonnet │ ✓ Same columns as the screen            │
│   ↳ started after api-server's contract-merged · 10:05                 │   check export.columns · 13:40          │
│ infra       ✓Build ✓Checks ✓Land ●Release ◆Approve ·Check Claude·sonnet│ ✓ 1M rows in under 60 s · bench 41 s    │
│   release 2.4 · waiting on approver: you · 3 h                         │ ○ Owners only can export · on beta      │
│ Together    ✓Land together ✓Tested together ◆Approve                   │ DECIDED WITHOUT YOU  2                  │
│ TESTED TOGETHER                                                        │ · Split over 3 repositories             │
│ ✓ api-server (3f2a) + web-app (91c0) · e2e 14/14 · Claude·sonnet 13:40 │   supervisor · Claude·opus · Reverse…   │
│ ▲ superseded 11:05 · failed on web-app, sent back to it · fixed ▸      │ · COPY instead of row inserts           │
│ WHERE THIS STANDS            the lead's words · Claude·opus · 13:45    │ ACTIVITY  12 ▸                          │
│ “Export works end to end on stage; one approval is left.”              │ [ Say something to everyone…  Ctrl ⏎ ]  │
└────────────────────────────────────────────────────────────────────────┴─────────────────────────────────────────┘
```

- **Head**: the mark, the short title, workspace, age, its cost so far (its sessions' share of each subscription's windows,
  and money for work on per-use accounts; an ask carries no budget, D343); the owner's words untouched, the rest behind *more ▸*;
  attachments as chips; *planned by <role> · <maker · model>* and *revision n ▸*.
- **Quests**: one `QuestTrackRow` per part of the plan, each with its own `StepTrack` (repositories follow different
  workflows), the current step in words, the holder's `AgentChip`, its `EdgeLine`s ("started after …" past, "waits on …"
  live); *Together* for the ask's own delivery run (the parts' landing, the joint check, the ask's Accept).
- **Tested together**: each joint test with the versions it combined, its result, tester and model, where a failure went back;
  superseded tests folded under the next. A test naming no version says *versions not named by the tester's plugin*.
- **Side column**: the ask's waits as cards; requirements with how each is known; *Decided without you*; *Activity* with one
  box to speak to everyone on the ask. *Where this stands* is the lead session's own newest words, labelled as its; no model
  runs to write it.
- **Goes**: the publish section (*Publish to engine*), the kind proposal's *Apply*, the review select, the plan's record words,
  the facts block, the driver note, the channel's *To* and *Reply to* selects.

### 11.3 New ask

One sheet (640 px; covering the content area when narrow), opened by **+ Ask** and Ctrl+N from anywhere, attaching the object in
focus as a link; a file dropped on the window opens it with the file. The owner's words and attachments, *More ▸* (requirements,
a look before landing, a short title); *Send*. The same sheet then shows the words becoming quests: the supervisor session with
its model that ran, the parts and why, the quests as published, the requirements it drafted (editable); *Stop it* is the ask's
abandon. With no planner, a *Where should this go?* card offers the declarations' matches. Esc keeps the draft. The quest
composer goes; *send it back as a quest* becomes *Ask a follow-up*.

### 11.4 A quest's page

Its passage through its repository's workflow, read top to bottom: the head (breadcrumb *ask › quest*, track); each step with
its role, holder and the model that ran, its attempts kept, one `ActRow` per act with its receipt ("release-created ✓
Release-2.4 · read by the plugin 13:31 ↗"), each request its holder asked another role and the answer that brought it back
(`RequestRow`); a side column of its waits, other quests, sessions, requirements and *Details*. Every owner press of today's run
view is a card body. The run view, the run flowchart, the quest life chart (`QuestFlow`) and the quest head's loud *Take* go
(*Take* only on an arrived quest's card).

### 11.5 A repository's Workflow tab

The one home of a repository's workflow (and a workspace's, on its page; the machine's default on the Workspaces root): who
takes each step and what must hold there (`ConstraintRow`s from the version and the place's cast, the owner's steps marked ◆);
how each step is done (*how: release-path ↗*, the repository's knowledge); *The line* (verification verdicts); recent runs, each
opening its quest's page; *Learned from runs* (each lesson proposed for a knowledge entry, carried to its holder, amended);
*Drawing ▸*, the read-only chart. Each step's settings open their form in a sheet; *Follow another workflow…* draws each choice
as its track. Agents › Setup's workflow section, the verifications list as a section and the side bar's Workflow view go.

### 11.6 A workflow change

Arrives as a proposal (Ask Daoris's draft, an agent's `workflow apply`). Only a change to the owner's part or a dropped check
waits on the owner, as a card showing the version before and after (`VersionDiff`); any other is applied and listed under
*Changed without you* with its proposer, model and reason, reversible to the version before. The step editor goes. A held
workflow (a kind's lighter workflow whose work went outside its paths) is one card: *Keep the Documents workflow* or *Use the
full workflow*, never a terminal command.

### 11.7 Wiring and the code map

The Map becomes a workspace's Wiring tab (every workspace on the Workspaces root): declared dependencies as lines with a legend,
live marks from the repositories' quests, and an ask's quests laid forward with their receipts (*On the map ▸* from the ask page,
`?ask=`). The code map is its repository's Code tab: what it was read from and when, names whole where the folder has room, a list
below 36rem. `ChainStrip` goes.

## 12. Workspaces, repositories and knowledge

### 12.1 The tree

One `TreeList` of workspaces and their repositories, each saying what moves, waits on the owner or fails there (`StatusMark`
counts, no new read), following the scope with the scoped workspace open; *Add · Import*. Today's two trees of the same
repositories (`projects/ProjectList.tsx`, `agents/setup/SetupList.tsx`) merge; the Setup mode goes. Narrow, the tree is the
page until one is chosen, then the head carries breadcrumb selects (*[ products ▾ ] › [ engine ▾ ]*).

### 12.2 A repository's page

```text
┌─────────────────────┬──────────────────────────────────────────────────────────────────────────────────────────────┐
│ WORKSPACES      + ⋯ │ engine   ● 2 moving · ◆ 1 waiting on you        products › engine  [ Hold ] [ Set up… ] [ ⋯ ]│
│ ▾ products  ● 3 ◆ 1 │ The runtime half of the example family: simulation, rendering, assets.  (its declaration)    │
│ ▌ engine    ● 2 ◆ 1 │ [ Summary ]  Branches  Checkout  Knowledge ◆1  Workflow  Code  Setup                         │
│   game      ● 1     ├──────────────────────────────────────────────────────────────────────────────────────────────┤
│   sandbox   ◌       │ ┃◆ End the kept entry "v1 export notes"? It ends 4 kept entries      beyond the leave · 2 h  │
│ ▾ work      ● 4 ▲ 1 │ ┃  proposed by session s12 · Claude·opus · 14:02 · cites 3 commits [End them ▮ Y] [Keep N]   │
│   api       ● 1     │ MOVING HERE                                    │ THE LINE   main · read 22:21  ↻             │
│   billing   ▲ 1     │ ● Build · Cache assets by hash  Claude·opus 12m│ c4f1a20 · 2 ahead of origin, 0 behind       │
│ + Add · Import      │ ● Review · Physics step   Codex·gpt-5   3 m    │ ✓ checks 46/46 at c4f1a20, ran 21:40        │
│                     │ ○ Chat · waits on engine: Build                │ ✓ landed 2 h ago · s3 · Claude·opus ›       │
│                     │ Finished today 4 ›   Session history ›         │ TREES 2 of 4 · DECLARATION › · INDEX 42 ›   │
└─────────────────────┴──────────────────────────────────────────────────────────────────────────────────────────────┘
```

Tabs (a select at narrow): **Summary** (what moves there and on which model, its waits pinned with the same id as Overview's,
the line and checks with how each is known, trees, declaration, index); **Branches** (by kind, each session branch with its
session and model, the line's history, a branch's page in place; every git act from one ⋯, each a sheet saying what it keeps and
what undoes it; delete and history edit held; the integration queue with what holds the candidate and who decides each hold);
**Checkout** (conflicts, staged and changed files worst first, the chosen file's hunks beside the list, the commit box at the
foot naming its identity; switch and stash as sheets); **Knowledge** (§12.4); **Workflow** (§11.5); **Code** (§11.7); **Setup**
(how work lands, what agents may do unasked, who runs and decides there, sessions, the standing answer, checks to accept,
doctrine drift, each saying `KnownBy` *inherited* or its own; *Set up…* is the head's sheet). Today's ids (details, history,
changes, files, integration) redirect.

### 12.3 A workspace's page

Summary (its repositories with their marks, line, last landing and checks; one *Runs on* line for the accounts its work may use,
◆ with a door to Company where one is signed out; *Kept on this machine* as one line, its clearing in Settings › Disk and data),
Branches across its repositories, updates and recoveries, Wiring, Knowledge (*Said in more than one repository*), Workflow and
Setup. Adding or importing repositories is one sheet: pick a folder, see what joins and how each will be set up, choose its
workspace, add nothing until the press (it draws Get started's `RepositoryIntake`).

### 12.4 Knowledge

A repository's Knowledge tab shows its memory kept, proposed and retired, each entry with the session and model that proposed
it, who kept it and when, what it cites and when that last held (◐ *not current* where a citation failed); proposals beyond the
leave wait on the owner there and on Overview; *Kept by* names the appointed role that keeps and ends its memory, its decisions
reading "decided by <role> · <maker · model> · Overrule until <time>". Its doctrine (rules, knowledge, skills, decisions,
fixes) is rendered with its fields (never a raw `---`), its source (canon or its own), path and last change; *Ask <repository>
to change it…* publishes a quest citing the path and commit (the window still never writes doctrine; it no longer shows a
command to run). Search lives in the palette's *Found in Knowledge* and the tab's find, saying which tier answered. *Said in more
than one repository* on the workspace's tab lists convergence findings with how alike they are and what became of each; one press
asks the owning repository to settle one. The Knowledge place goes.

## 13. Company

Machine-wide (D197: one set of accounts serves every workspace). Its list: **Accounts** (by agent), **Roles** (*Working roles*,
*Appointed roles*, *What stays yours*), **Connectors**, **Plugins**.

### 13.1 Accounts

```text
┌──────────────────────┬──────────────────────────────────────────────────────────────────────┬──────────────────────────────┐
│ COMPANY          + ⋯ │ ACCOUNTS  ◆ 1 signed out holds work · ‖ 1 at its limit · 4 ready · key $2.30 of $20       ↻ Read all│
│ ▌ Accounts        ▲  ├──────────────────────────────────────────────────────────────────────┼──────────────────────────────┤
│   Claude Code  3     │ ┃◆ Sign in to work@example · Claude Code     holds 2 builds · 40 m   │ WHICH MODEL ACTUALLY RAN 24 h│
│   Codex        1     │ ┃  Signed out at 14:40, not by Daoris; nothing else can run them now.│ 15:18 engine · Build         │
│   DeepSeek     1     │ ┃  Yes opens its sign-in · No they wait for 14:23 on home@example   │  asked Claude·opus ran opus  │
│ Roles                │ ┃  [ Sign in  Y ]  [ Leave signed out  N ]   Not mine · Later        │ 15:02 game · Second opinion  │
│   Working roles  12  │ CLAUDE CODE   Anthropic · 2.1.4                           Open ›     │  asked Codex·gpt-5           │
│   Appointed roles 6  │ work@example  ◆ signed out 14:40   week ▓▓▓▓▓░░░░░ 52%  [Sign in] ⋯  │  ran DeepSeek ↓ Codex cooling│
│   What stays yours   │ home@example  ‖ at its limit to 14:23  5-h ▓▓▓▓▓▓▓▓▓▓ 100% [Try now]│ HOW ACCOUNTS ARE USED        │
│ Connectors      ▲ 1  │ spare@example ● ready              5-h ▓▓░░░░░░░░ 18%          ⋯     │  Claude Code: most of them   │
│   github.com     ●   │ CODEX   OpenAI · 0.160.0                                  Open ›     │  sessions at once: 6 [Change]│
│   Azure DevOps   ●   │ me@example    ‖ cooling to 22:00   week ▓▓▓▓▓▓▓▓▓░ 88%  [Try now] ⋯  │ SPEND AND BUDGETS            │
│ Plugins              │ DEEPSEEK   key ····4f2a                                   Open ›     │  Claude Code: plan's windows │
│   In-app browser ●   │ prepaid       ● ready   balance $12.40 of $20 · read 21:05       ⋯   │  DeepSeek key $2.30 in Nov   │
│   + Add              │ + Add an account   + Add an API key                                  │  budget $20 a month Change › │
└──────────────────────┴──────────────────────────────────────────────────────────────────────┴──────────────────────────────┘
```

One board of every account of every agent, those that need the owner first: each with its state (signed out is – when it holds
nothing, ◆ and a sign-in card when it holds work; never the accent), its windows as `Meter` cells in one column, what uses it now
and which models it served. The sign-out shows at the next look, before a step fails (the look's stored-sign-in read, a file
read and no process). *Which model actually ran* lists the last day's starts with the model asked, the model that ran and why
they differ. *How accounts are used* sets their order, spread, switch and curve (drawn at the window's type size), those kept for
conversations, and *Sessions at once on this machine* (moved from Settings' Driver). ⋯ *Pause until…* and *Put first until…*
are dated overrules ("‖ paused by you until Mon 08:00"). An agent's page: its install and version, ways in, what it may do, the
models it offers, model and effort per account, which models it ran, its usage, its accounts as the board's rows.

### 13.2 Working roles

One table of each work (Build, Review, Second opinion, Chat, Search, Intake, …), its role and its choices in order
(`ChoiceLadder`), each choice saying whether it can run now and what ran in the last day; editing in a sheet that previews which
queued work it changes. The roles diagram goes; its job is the *All places ▾* filter.

### 13.3 Appointed roles

Who decides what for the owner (supervisor, reviewer, release manager, credentials keeper, knowledge keeper, acceptor), held by
which agent and model or by the owner, where, and what each decided in the last day, with the supervisor's rules beneath.

### 13.4 What stays yours

The line between what the agents decide and what the owner keeps, drawn in one place: the closed list (§5.2); **every act** of
§6 with who holds it at each place (*Confirm it was you* shown as the person's alone, never given), *Give this act to…* an appointed role (its holder then performs it through its tool) and
*Take it back*; what agents may do unasked on this machine (the machine's access mode and git access, moved from Settings'
Driver); the rules switched on and every *always* given, with *Take back* (D284 point 6's list in the access design §5, which Settings' *Limits you set* would
have repeated); each place that sets its own, with a door to its Setup tab.

### 13.5 Connectors and plugins

Connectors are the outside systems Daoris reaches, by kind (code hosts, trackers, release pipelines, mail), failing ones first,
each with its state in its own words (authored, never translated) and how old that is, what uses it and its last rounds; a
refusal for want of a sign-in is the same sign-in card as an account's. A code host's page holds its identities, which is the
default, and the owner's own git sign-ins (Settings' Git goes). A connector's or plugin's page: its tabs by what it declares, its
sign-in, *Try*, *Update*, *Remove* (held, saying what removing takes away; the data folder unticked), its switches per place;
Settings' plugin settings move here. *Plugins* lists what extends Daoris without reaching outside (agents, steps and holds,
tools) and Daoris's own plugins offered, with what each needs and what installing starts.

### 13.6 Money

The owner's direction (2026-11-20): budgets follow how an account is paid, and money is shown only where it is real (D343).

- **A subscription** carries no money budget. Its use is shown against its plan's own windows, the session
  and weekly limits the accounts already report, as a share and a reset (`Meter` limit cells). Its price appears only if the owner
  enters it, as the plan's price and period, never spread over the work as an estimate.
- **An account paid per use** (an API key) carries a budget the owner sets on its row in *Spend and budgets*, by the week or by
  the month (the owner's choice of period), beside its measured spend and its prepaid balance (`Meter` budget bar). When the
  period's spend reaches 80% of the budget a Money wait arises on that account: *Yes* raises the budget by half, rounded up to a
  whole unit of its currency; *No* holds new starts on that account until the period resets while running work goes on. The 80%
  and the half are Daoris's defaults, shown as such (`KnownBy` default) on the account's row and settable there. A prepaid
  balance running out raises the same kind.
- A missing reading is *unknown*, never 0.
- *Budget* on the window is this, an account's spend for its period; the glossary's *daily budget* (a role choice's starts or
  tokens in a day) keeps its own row, and UIRP28 adds *budget* beside it.
- **An ask carries no budget.** It shows its cost so far: its sessions' share of each subscription's windows, with their tokens
  by model, and money for the work on per-use accounts. The same figures (`CostLine`) appear in Overview's *Used*, a session's
  *Models and use* and an ask's head; the status strip shows spend only while a per-use account has a budget (§15.2).

## 14. Settings

Settings is the window and the install, and nothing the agents decide:

| Group | Domain (中文) | Holds | From today |
|---|---|---|---|
| first, until done | Get started (开始使用) | sign in an agent; add repositories; optional code hosts and connectors; what Daoris decided | Get started |
| Yours | Appearance (外观) | theme, language, zoom, each saying what it follows | Appearance, View › Zoom |
| Yours | Notifications (通知) | whether this computer announces waits, and hours without announcements | Driver's notify row |
| This machine | Install and updates (安装与更新) | which Daoris runs, the update it installs next and what it would cut, past updates, the terminal command | Updates, This Daoris's Terminal card |
| This machine | Disk and data (磁盘与数据) | what Daoris keeps, how long, freeing it with one held press; the UX trail | Disk space, Driver's UX trail, the log's keep days |
| This machine | This machine (本机) | programs (git, Node.js, PowerShell, the browser), diagnostics (the knowledge store's tier verbatim), Daoris's own dials read only, the machine log | Tools, Browser, This Daoris, Machine log |

The list shows each domain with a one-line status (`ListRow`, two lines: "0.0.2 installs when idle", "1.4 GB · 41 GB free", "▲
Git not found"), from each domain's cheap read, never a measurement; narrow, the list is a picker in the page head. Driver's
tuning dials become Daoris's (read only in This machine; their `daoris settings` verbs stay); the access rows go to Company ›
*What stays yours*; *Sessions at once* to Accounts; AI features' search tier to This machine's diagnostics and Ask Daoris's mode
to its dock's chip; Git to the code hosts' connector pages; plugin settings to each plugin's page. Old domain ids open
Appearance.

**Get started** is a first-run path the owner finishes alone (D338): `StepTrack` down with one step open and done steps folded to
their summary with *Change*: (1) sign in an agent, drawing Accounts' own sign-in per maker; (2) add repositories, through
`RepositoryIntake` (choose folders or an address, each read, a workspace proposed and renamable, each repository's phases while it
registers); then the optional (3) code hosts the repositories' remotes name, signed in on the spot, and (4) connectors offered.
Daoris takes the rest of the setup choices once, at the first account and each new repository (Ask Daoris's agent, the intake's
and the roles' suggested choices, a repository's suggested plan: its own declaration's landing, else *merge after you accept* on
a local branch, never a push unless declared), never overwriting an owner's choice, and lists them under *Daoris decided* with
doors to change them. The finish opens New ask; *Later* replaces *Don't open at start*. The window opens on it while a required
step is open; the status strip says *Setup: n of 2*.

**Install and updates** (D344): updates already install themselves when the work is idle, with no word from the owner
(install-home-update rule 37); what changes is that the page and the status strip's item say, in one sentence from one key per state, staged, holding new work (how many sessions are finishing and
until when), installing, installed, rolled back or refused; *Install now* stays; a history of the last ten installs (version, from,
time, outcome, sessions cut, why). Where releases come from is RELEASEFEED1's (D329: Daoris's own releases from its public
repository); what it leaves to the window is UIST14's: the first release waits on the owner's go as a Release wait, and the
running app looks for a release by itself and says when it last looked. A rolled-back or refused install is a *Since you left*
line, and refused again after the agents' retry a *Gave up* line, never a wait. The update banner goes.

## 15. System surfaces

### 15.1 The title strip

`AppStrip` holds the mark with its app menu, `ScopeSwitcher`, `SearchField` (folding to its glyph), `WaitsPill` (amber at one or
more, quiet at zero, ◐ when stale; opens Overview at the first wait), **+ Ask** (Ctrl+N), the Ask Daoris door and the caption
buttons. The menu bar, the layout toggles, the bell and the review chip go.

### 15.2 The status strip

Facts the owner can trust, through a registry (`work/frame/statusItems.ts`) places add to: ● Live or ◐ *As of* (the driver's
presence worded as freshness), *Synced n s ago*, *Accounts: 1 signed out · 1 at its limit* (Company), and, only while an
account paid per use has a budget, the one nearest its budget with its period (*Key $2.30 of $20 · Nov*, Company), *Browser · driven by <session>* (the browser door's chips move here), the install's version and update state, *Setup: n
of 2* while Get started is open, ▲ when the disk is under its floor. Narrow, the rest fold to *+n*. The recall tier moves to where
a search's results show (the palette, Knowledge).

### 15.3 The palette

§4.4. `CommandPalette` over `PaletteFrame` and `Combobox`; matching in both languages; a failed search says so in its group.

### 15.4 The side bar: Ask Daoris

The side bar holds Ask Daoris: its head names the agent and the model of the conversation's last turn (`AgentChip`, "asked X,
ran Y"); the machine-lacks starters go (Get started keeps its own); its mode chip is Ask Daoris's mode's one home. Its
conversations are sessions: listed in Sessions under *Ask Daoris*, opened in the session panel; *History ▸* opens that group.

### 15.5 The panel

The owner's terminal (§10.4). Placements, the Views menu and view dragging go.

### 15.6 The sheet

Forms and composers open in one `Sheet`, composer 640 px or form 480 px at the right, covering the content area when narrow, ←
back, focus back to its opener, never over the bars; each of today's eleven drawer mount sites chooses a size.

### 15.7 Toasts

Two kinds only: *Undo* for a reversible act in its 5-second grace (nothing is sent before it ends, *Undo* sends nothing, closing
the window sends at once), and a receipt. Arrival toasts and their doors go.

### 15.8 The held press

An act that cannot be taken back is a `Button` with `hold`: held 800 ms, its fill drawn, its consequence written under it in
three short lines where they apply (*Ends*, *Keeps*, *Loses*), the object named by its title with its id in a tip, the press
naming its act ("Clear from this machine", "Stop the session"). No second question; `InlineConfirm` goes.

### 15.9 A failure stops its region

`PageBoundary` mounts per region with `RegionState`'s *stopped* form (what failed, *Try again*, *Copy the details*); the log names
the region and the address; the rail, the pill and the other regions stay pressable. The whole-page screen remains for the frame.

### 15.10 The splash

Wears the kept theme and language the shell passes, shows the mark and the name, and after 10 s the phase the shell reports.

### 15.11 Secondary windows

A session or an ask tears off into its own window by address (⧉, Ctrl+click), one window per address, at least 520 px wide, with
`SecondaryHeader` (breadcrumb, the waits pill reading the same slice, the way back). The monitor window becomes *Moving now* in
its own window, one live line per session. Every object's menu opens it, tears it off and copies its link; *Copy session ID*
moves to *Details*.

## 16. The catalogue and the pictures

- **Storybook is the one place everything is seen** (D331). Toolbar globals `theme` (light, dark), `locale` (English, 中文) and
  `width` (1600, 1280, 1024, 900, 720), applied by `withLocale` (STORY2's detector-safe switch), `withTheme` and `withRegion`
  (the story drawn at its region's width in a window of that width, from `design/regions.ts`). They replace `storyWidths`,
  `sessionWidths`, `NOT_IN_A_MAIN_AREA`, the `chinese` decorator, `InTheme` and the 117 local `dark` decorators; a check fails a
  story that pins a global or frames itself. A one-off codemod deletes the 1,482 pure variant exports, renames a variant with its
  own content by its state, and carries floor widths (about 1,980 of 3,462 stories remain).
- **Order**: `Window/*` (the assembled window over a fixture: waiting on you, a session open, first run, offline and stale),
  `Screens/<place>/<screen>` (`screenStory({ place, route, fixture })` draws the shell around a screen), `Library/Tokens/*` and
  `Library/Status marks` (generated from `tokens.css`, each pair with its computed ratio, held equal to `tokens.test.ts`'s
  reading), `Library/Primitives|Composites|Patterns/*`, then today's books until their rows move them.
- **Pictures**: one Playwright harness for geometry and pictures (`measured/harness.tsx` generalised: any story by id, globals
  from the URL, a frozen clock, UTC, reduced motion), a `visual` project beside `measured`. Stories tagged `visual` (every
  `States` sheet) are shot on the diagonal pair, light·en·1600 and dark·zh·900; `visual-full` (the window, every screen story,
  the `WaitCard` sheet) at both themes × both languages × 1600, 1280 and 900, the window at 720 too. About 500 images, at most
  600 files and 30 MB (`tools/size-ratchet`). Baselines are tracked at `src/Daoris.Web/visual/baselines/<story>/<theme>-<locale>-
  <width>.png`, binary in `.gitattributes`, and change only in the commit that changes the look, whose message names the stories
  moved and why.
- **The machine**: Windows only with its system faces (Segoe UI Variable Text, Microsoft YaHei UI, Cascadia Mono or Consolas),
  probed by canvas width; a missing face stops the run by name. Software raster, sRGB, DPR 1, animations off, caret hidden. The
  tolerance is measured from three runs of an unchanged tree and written with its date.
- **Accessibility in the same pass**: `@axe-core/playwright` on each shot and a clipped-label probe; today's hits in a list that
  may only shrink.
- **Gates**: `web-rules` gains `design/rawValues.test.ts` (no arbitrary length, inline literal, raw shadow, numeric z-index,
  off-step spacing, stray weight or italic, `font-mono` outside the code atoms, raw input or tablist outside the library,
  viewport variant outside the shell, undeclared colour, `dark:`; today's hits listed per file, a list that may only shrink) and
  `design/words.test.ts` (no literal word in `design/**`). A new gate `web-visual` (`npm run test:web:visual`, a browser spec
  the merge's fast plan defers to the run on main, as `web-measured`); `reached-checks` gives a builder the stories its change
  reached, at most 24 shots, before hand-back. Pictures say something moved; `measured/` says what is wrong with it; neither
  proves the installed window (`tools/desktop.mjs shot` does).
- **Rejected**: a hosted picture service (it needs a remote, and local operation needs none); untracked renders (nothing to
  review in the commit); every story at every combination (about 16,000 images no one reads).

## 17. The comparison

The fresh-design pass's comparison (`.claude/knowledge/fresh-design-pass.md`): the two fresh designs agree on the shape (few
places, list and detail, a home of three bands answered in place, one card anatomy, one session panel, state as shape, colour
and word, one interactive accent with amber for the owner) and disagree on numbers and on where Knowledge and Settings sit.
Today differs from both almost everywhere. The six plans read each difference against the code.

**Where today is worse, ranked by value over effort:**

| # | Worse today | Shows it | Value | Effort | Rows |
|---|---|---|---|---|---|
| 1 | Which model ran is buried in *Details* ("not reported"); a head names the harness's version | `work/actors.ts`; shot `work-attendedsession--attending` | high | S–M | UISE1, UIDS15, UIRP6 |
| 2 | A wait is not answered where it is seen: Overview's lines open *Notifications*; no card says what yes and no do | `overview/NeedsSummary.tsx`; shot `work-wait-strip--parked-with-a-go-ahead` | high | M | UIDS16, UISE2, UISE3 |
| 3 | Most rows that reach the owner are manager questions: 36 kinds, the band's story holds 23 rows | `work/attention/attention.ts`; shot `work-attention--band` | high | M | UISH6, UISE9, UISE10 |
| 4 | A sign-out is seen only after a step fails | ACCOUNTREVOKED1's finding; D328 now moves the step, but the window still learns of it at the refusal | high | M | UIRP4 |
| 5 | The owner dispatches: publishes an ask to each repository, applies the kind, takes quests, runs a terminal command to release a hold | `asks/AskPage.tsx`; shot `workflow-workflowhold--outside-kind-paths` | high | M | UIWF2, UIWF6, UIWF11 |
| 6 | Nothing says what finished while the owner was away; there is no window-level mark | `work/catchup/SinceYouLeft.tsx` (per session only) | high | M | UISH8, UISE5, UISE6 |
| 7 | The owner's amber and the interactive bronze are one hue family (1.55:1 apart in dark); the faint ink carries text at 3.38:1; Han at 11 px and in mono | `atoms/Marks.tsx`; `tokens.test.ts`; shot `work-sessionlist--by-state--dark` | high | L, mechanical | UIDS1, UIDS2, UIDS9 |
| 8 | No picture of the whole window, no picture tests, 1,482 variant stories; the token gallery drifted from the tokens | `design/tokens.stories.tsx`; inventory | medium, enabling | M–L | UIDS3, UIDS4, UIDS5 |
| 9 | Nine places, seven menus, two trees of the same repositories, the scope named twice, settings in three homes | `app/App.tsx` `VIEWS`; `menus/commands.ts` | medium | L | UISH2, UISH4, UIRP1, UIRP2 |
| 10 | A session's changes live in the side bar; seven folds above the conversation | `work/AttendedSession.tsx` | medium | M | UISE14, UISE18 |
| 11 | Settings holds eight domains of agents' wiring; Get started sends the owner through five screens | `settings/domains.ts` | medium | M | UIST3, UIST4, UIST10 |
| 12 | A run is six tall cards for five facts; a workflow is composed on a chart the owner does not need | shot `workflow-workflowrunflow--release-run` | medium | L | UIWF3, UIWF8, UIWF10 |

**The route: rebuild the surface, keep the records.** The data layer stays: pushed slices, routes, the driver's readers and
judges, the records. The presentation is rebuilt on the library, one screen per row, each replacing what it supersedes in the
same change (never a second window kept alive beside the old one, and never a switch). Adopted from prior art: Radix (kept), VS
Code's frame grammar (the palette, a status strip of ambient facts), Linear's keyboard queue and a snooze that comes back,
Carbon's status marks (shape and colour, the most urgent per group), Fluent's 14/20 body, CLREQ's rule that Han–Latin spacing is
the renderer's. Evolving screen by screen in today's atoms was rejected: the tokens, the marks and the card cross every screen,
so a screen evolved before them is redone after. A whole rebuild at once was rejected: the records under the screens are sound,
and the window must keep working while it changes.

**What would flip it**, each checkable:

- In a week after UISH6, more than one wait in five that reaches the owner is not an owner kind, or no role can take the rest:
  the roles' work (roles-and-supervision) comes before Overview's rebuild, since the card would make the noise louder.
- `CARD_FIELDS`' "not said" count exceeds one card in ten after UISE2: the askers are fixed before Overview's queue is built.
- The owner answers most waits from the computer's notifications, not the window: a notification carries the answer for
  go-aheads whose fields fit, and Overview's weight moves to *Since you left*.
- The visual run cannot hold a measured tolerance on this machine across three runs: baselines become a report, not a gate.
- The facts rail leaves the timeline under 72ch at 68rem in Chinese: the rail appears only at a wider threshold, or only in the
  sheet.
- Most asks touch one repository: Moving groups by repository by default.
- 32 px rows are tight in Chinese at 900 px: 36 px rows in both languages, still one density.
- An irreversible act is given by mistake once with the held press: that act's definition gains a second step, and only that one.
- Radix stops shipping fixes: Base UI, inside `design/primitives/`.

**The trial**, written before it runs. *Where:* Overview with its `WaitCard` queue, after wave 3. *Baseline*, recorded on the
current window over the week before UISE2 starts by UISE23 (the UX trail switched on, read by `tools/ux-trail.mjs`): the time from a return after 2 h or more to the last waiting item answered;
screens opened per answer; items that reached the owner outside the owner kinds; answers given on a question that had already
changed; amber the owner saw that did not need them. *How the baseline is read:* the owner's install runs with the trail on for seven
whole days (the first and last day are written in the steward's scratch with the five numbers, before UISE2's first commit), and
`node tools/ux-trail.mjs <home's ux-trail folder> --from <first day> --to <last day>` prints them, days as the entries' UTC dates. A return
is an entry after a silence of 2 h or more with waits open, timed to the last of those waits pressed in; a screen is a page opened
between an answer and the one before it, the return or its wait; an answer is the first press in a wait's row, so the numbers are an
upper bound where a press only opens the row; a question has changed when the row's wait began again or a go-ahead went stale before the
press; amber that did not need the owner is a wait outside the owner kinds (`OWNER_KINDS`, §5.2) or one that cleared with no press in
its row. The kept-if rules are read from the same command on the new Overview's week. *Kept if:* at least nine waits in ten are answered from Overview or a dock
without opening another screen; the time from return to the last answer is below the baseline; no answer runs twice and none
lands on a changed question; nothing outside the owner kinds reaches *Waiting on you*; no amber that did not need the owner;
Overview's stories are green in the visual run in both languages, both themes, 1600 and 900. *Retired* (back to the card's fields,
not to the old window) *if:* the owner opens the object before deciding on more than a third of items; cards regularly lack a yes
or no line; *Since you left* is wrong or missing because presence is unknown; an irreversible answer is given by mistake even
once; Moving runs past one screen and stops being read. A second trial follows wave 4: on a session that ran three hours, "why
did it take this long, and what did it touch?" is answered from the panel alone within two minutes (D320's trial, SESSIONVIEW1j,
is its baseline).

## 18. Decisions made or reversed

| Decision | What it decides | Amends or supersedes |
|---|---|---|
| D330 | The token role set: blue accent, amber the owner's alone, red only for failed, text at 4.5:1 and marks at 3:1, one type scale for Latin and Han with 14 px body, one density with zoom; a column may keep the 72ch measure and a region lay out columns by its width | D41's bronze accent, D56's 13 px body, D141 and desktop-app rule 27 (amends) |
| D331 | The catalogue's globals instead of variant stories; tracked, tagged picture baselines on Windows | — |
| D332 | Six places: Overview, Asks, Sessions, Workspaces, Company, Settings; the Map and Knowledge in Workspaces; Overview keeps its name and 总览 | D150 §2's nine places (supersedes), D67's Map place (amends), D30 (convergence's place) |
| D333 | The title strip: no menu bar, one app menu, the scope named once, the waits pill and + Ask | D152's menus, D75's double naming (supersedes) |
| D334 | No movable views: the side bar is Ask Daoris's, the panel the terminal's; a plugin's side view is a section of its page; a closed list hides | D118 (supersedes), desktop-app rule 25's strip, D274's plugin side tabs (amends) |
| D335 | Notifications retire; each part has a home; the computer announces only counted waits | D196 (supersedes) |
| D336 | Only the owner's kinds wait on the owner, counted as one number, each answered on one card in place, a key held for what cannot be undone; work the agents gave up on is a line, not a wait; the rest is with the agents | D196 §5, D40's landing, D152 §3 and rule 29 (amends) |
| D337 | A failure stops only its region | D217 (amends) |
| D338 | Get started is a path the owner finishes alone, and Daoris takes the other setup choices once | D97 (amends) |
| D339 | The session panel's facts rail and its Changes focus | D320 §9 and its rejection of a right rail, D141 for the panel (amends) |
| D340 | Every owner act is defined once over the doors that exist and any actor can be given it, but Confirm it was you; irreversible acts are one held press or held key, reversible ones a 5 s Undo | D325 point 1 and APPROVEACT1 (extends), D284 point 6's rules list (the access design §5), D152 §3 and rule 29, platform UX's ask-once rule |
| D341 | An ask becomes quests without a press: per-part Publish, the kind's Apply and Take go | a rule kept only in code comments (`AskPage.tsx`, `AskPublishing.cs`): a proposal is a person's to accept |
| D342 | The workflow step editor goes; a change arrives as a before-and-after card | D223, D221 (amends) |
| D343 | Budgets follow how an account is paid: a subscription is shown against its plan's windows, a per-use account carries a weekly or monthly budget; money only where it is real; an ask carries no budget | D179's rejection of money, D57's counts never priced, the glossary's *usage* (amends) |
| D344 | An update installs when idle and is never the owner's question; the window says it in one place; the release source is RELEASEFEED1's (D329) | D139's banner (amends) |
| D345 | Radix stays the one headless layer, gaining popover, hover card and tabs; the library owns combobox, tree and list keys; React Aria Components rejected | D42's libraries (amends) |

Decisions the rows record when they build (each named in its row): the addresses by id and a link carrying no scope (UISH3), the
tier's move from the status strip (UISH5), the away thresholds and the machine-wide mark (UISH8), doctrine changes asked by quest
instead of a command shown (UIRP22, amending D31 and desktop-app rule 62).

## 19. Build order

Ten waves. A row starts when every row it names is done; within a wave, the batches below run five at most, each builder in its
own folders (a one-line registration in a shared table, `domains.ts`, `ui.tsx`, `menus/commands.ts`, merges in order).

| Wave | Rows | Batches that run together |
|---|---|---|
| 0 The design system's foundation | UIDS1–UIDS7, UISE23 | UISE23 first and beside the rest (a week of the trial's baseline, recorded before UISE2); UIDS1 alone (its codemod, a re-runnable tool, renames classes across `src/`); UIDS2 ∥ UIDS3; UIDS4; UIDS5; UIDS6 ∥ UIDS7 |
| 1 The library and the acts | UIDS8–UIDS17, UISH24 | UIDS8; UIDS9 ∥ UIDS10; UIDS11; UIDS12 ∥ UIDS13 ∥ UIDS15; UIDS14 ∥ UIDS17; UIDS16; UISH24 |
| 2 The frame | UISH2–10, 14, 15, 17, 18, 25 | UISH2 alone; UISH3 ∥ UISH4 ∥ UISH5 ∥ UISH17 ∥ UISH18 ∥ UISH25; UISH6 ∥ UISH7 ∥ UISH9 ∥ UISH10; UISH8 ∥ UISH14; UISH15 |
| 3 Overview and the waits | UISE1–7, 9–12, UISH12 | UISE1 ∥ UISE2; UISE12 ∥ UISE11 ∥ UISE9; UISE3; UISE4 ∥ UISE5 ∥ UISE10; UISE6 ∥ UISH12; UISE7 |
| 4 The session panel | UISE14–20, 22, UISH13, UISH22 | UISE14 ∥ UISE15 ∥ UISE16 ∥ UISE17 ∥ UISE20; UISE18; UISE19 ∥ UISH13; UISE22 ∥ UISH22 |
| 5 Asks and work | UIWF1–7, UISE13, UISH19–21 | UIWF1; UIWF2 ∥ UIWF3; UIWF4 ∥ UIWF5 ∥ UIWF6 ∥ UISH19; UIWF7 ∥ UISE13 ∥ UISH20 ∥ UISH21 |
| 6 Company | UIRP1, 3, 4, 6–8, 13–17, 25–29, UIWF14 | UIRP1; UIRP3 ∥ UIRP13; UIRP4 ∥ UIRP6 ∥ UIRP14 ∥ UIRP26; UIRP7 ∥ UIRP15 ∥ UIRP16 ∥ UIRP25 ∥ UIRP27; UIRP8 ∥ UIRP28; UIRP17 ∥ UIWF14; UIRP29 |
| 7 Settings and the install | UIST1–8, 10–14, UISH11 | UIST1 ∥ UIST2 ∥ UIST3 ∥ UIST5 ∥ UIST6; UIST4 ∥ UIST7 ∥ UIST8 ∥ UIST12 ∥ UIST13; UIST10 ∥ UISH11; UIST11 ∥ UIST14 |
| 8 Workspaces, knowledge and workflows | UIRP2, 5, 9–12, 18–24, UIWF8–13 | UIRP2; UIRP5; UIRP9 ∥ UIRP11 ∥ UIRP18 ∥ UIRP20 ∥ UIWF8; UIRP10 ∥ UIRP12 ∥ UIRP19 ∥ UIRP22 ∥ UIWF13; UIRP21 ∥ UIRP23 ∥ UIWF9 ∥ UIWF10 ∥ UIWF11; UIRP24 ∥ UIWF12 |
| 9 The sweep | UISH23, UIDS18 | UISH23 ∥ UIDS18 (after every screen row) |

Between UISH2 and a place's own rows, a place shows today's content under its new name (Company mounts today's Agents and
Plugins; Workspaces today's Repositories, Map and Knowledge; Asks today's Quests view), so the window works at every step.
Rows in flight that these name: SESSIONVIEW1d2, 1f2, 1g, 1h, 1i (the panel), CHATUX1 (the composer), ACTORFLOW1 (acts and
receipts; its open item, a role's act handed only to the session holding the step, under UIRP29), FOUNDATION1 (its one Act
contract adopts UISH24's definitions), RELEASEFEED1 (D329, Daoris's own releases; UIST14 takes what it leaves), ACCESS1 rows c,
d and n (rules and access), USAGEMODEL1 and APIKEYACCT1 (usage and balances), SIGNINWAIT3 (a conversation's sign-in in its
dock). UIDS1's and UIDS3's codemods are re-runnable tools, run again over any of these web rows that land after them. Rows the
redesign supersedes, marked so in `TASKS.md`: FLOWCHART2 (UIWF10), AGENTSPLACE1 (UIRP1), MAPUX1 (UIWF12), CANVAS1g (UIWF8 keeps
the read-only drawing), SETTINGSUX1 (UIST11), SETUPUX1x (UIRP9), UIREVIEW1 (its findings are these rows), PLUGINEXT1 f and g
(re-scoped to sections of a plugin's page, D334).

## 20. The reconciliation

Where the plans disagreed, and what was decided:

1. **The home's name.** Overview, 总览: "Desk" (工作台) is on the glossary's avoid list, "Home" is the data folder's word, and
   the glossary's 总览 stands (the steward's note wrote 概览, which no row of the glossary holds). UISE's Desk names follow:
   `src/overview/`, `overview.json`, `OverviewView`, `OverviewSince`, `OVERVIEW_SINCE`, `OverviewBand`.
2. **The places.** Six: UISH's *Agents* becomes *Company*, which holds accounts, roles, connectors and plugins (UIRP; the
   steward); UISH's *Settings › Plugins* becomes *Company › Plugins*; Knowledge and the Map live in Workspaces (the owner).
3. **The waits' card.** One `WaitCard`, built by UIDS16; UISE2 maps every attention and dock kind into it; UISH6 counts; UIWF and
   UIRP pin it on their objects with the same id; its presses read the act's definition (UISH24).
4. **Notifications.** The view retires (UISH12); Settings' *Notifications* is only the domain of the computer's announcements
   (UIST1, UIST13), and loses UIST1's door from the view.
5. **One census.** UISH6's `OwnerKinds` and UISE9's `AttentionOwnership` are one census, `AttentionOwnership`, built in UISH6;
   UISE9 keeps the supervisor's routing and *With the agents*.
6. **One presence.** UISH8's machine-wide record and caught-up mark serve Overview; UISE8 merges into it, and UISE5 reads its
   mark (no `desk_marks` table).
7. **The closed list's strip.** UISE kept it for marks at 900 px; UISH removes it, since marks without names identify nothing and
   the pill and the breadcrumb's ← carry what it did. Removed.
8. **Numbers on the rail.** UISE's count on Overview and UIRP's amber count on Company go: one number (§5.1); Company wears ▲.
9. **Stop, pause and clear.** UISE21's in-place ask gives way to the owner's direction: reversible acts get *Undo* (UISH14);
   irreversible ones a held press carrying UISE21's *Ends*, *Keeps* and *Loses* (UISH15, which takes UISE21); session-level
   *Pause* and *Resume* go to UISE14.
10. **Duplicates cut.** UISH1 (the whole window's stories) into UIDS4; UISH16 (the sheet) into UIDS11; UIST9 (*Limits you
    set*) into UIRP17's *What stays yours*, since it draws the line between the agents' and the owner's.
11. **Widths.** UIDS's 360 px list at a wide window gives way to UISH's resizable list, 300 px to start; the containers `list`
    and `side` are 300 and 400.
12. **Code hosts** are connectors (UIRP26), not Accounts (UIST): the purpose lists code hosts among connectors.
13. **The machine's setup.** Its access mode, git access and rules go to *What stays yours*; its default workflow to the
    Workspaces root's Workflow tab; its programs and dials to Settings › This machine.
14. **The update banner** retires (UISH11); the status strip's item and Install and updates say one sentence from one key.
15. **Money.** The plans differed (UISE: tokens until the owner chose; UIRP and UIWF: money with a weekly budget, and a budget
    per ask). The owner's answer settles it: budgets follow how an account is paid (§13.6). UISE7's and UISE19's lines show each
    subscription's share of its windows with the tokens, and UIRP28 adds money only for per-use accounts and entered prices; no
    price table estimates subscription work; UIWF14 shows an ask's cost and builds no budget.
16. **The status strip's spend** appears only while a per-use account has a budget, showing the one nearest it with its period
    (UISH's "Today $14.20" and UIRP's "Week $37 of $60" both go).
17. **The repository's first tab** is *Summary* (概要), so *Overview* names one thing.
18. **Keys.** Ctrl+N a new ask; a new conversation keeps Sessions' + and gains Ctrl+Shift+N.

Placeholders replaced: `UIDS:you-token` UIDS1; `UIDS:StatusMark`, `UIDS:Badge`, MarkCounts UIDS9; `UIDS:KeyHint`, Fold UIDS8;
`UIDS:ListRow`, StatusNavRow UIDS13; `UIDS:Skeleton` UIDS14; `UIDS:Button-danger`, HoldToConfirm UIDS10;
`UIDS:overlay-tokens`, `UISH[Sheet]`, PageTabs UIDS11; `UIDS·DecisionCard`, `UIDS[WaitCard]` UIDS16; `UIDS·AgentChip`,
`UIDS[KnownBy]`, `UIDS[EntityChip]`, AsOf, SourceMark, InheritMark, FactList, DecidedLine, LimitMeter, ShareBar UIDS15;
`UIDS[StepTrack]`, StepPath UIDS17; `OV:waits-queue`, `OV:moving-now` UISE3; `OV:since-you-left` UISE5;
`UISE:session-panel` UISE14; `UISE:session-panel-views` UISE18; `UISE:session-list-groups` UISE12; `UISE[ChainStripGone]`
SESSIONVIEW1i; `UIWF:ask-page` UIWF2; `UIWF·ask composer` UIWF6; `UIRP:account-health` UIRP4; `UIRP:spend`,
`UIRP[CostPerSession]` UIRP28; `UIRP[RepositoryTabs]` UIRP5; `UIRP[WorkspacePage]` UIRP10; `UISH[Breadcrumb]` UISH3;
`UISH[Places]` UISH2; `UIST:notifications` UIST13; UIST's Disk space UIST6. New rows: UISH24 (the acts), UIRP29 (an act given
to an agent), UIST14 (what RELEASEFEED1 leaves to the window), UISE23 (the trial's baseline). Cut: UISH1, UISH16, UISE8, UISE21, UIST9.

The owner's answers (2026-11-19, and the budgets on 2026-11-20) and the steward's three: an ask becomes quests without a press
(D341); the workflow step editor goes (D342); budgets follow how an account is paid: a subscription against its plan's windows
with no money budget, an account paid per use with a weekly or monthly budget, money only where it is real, and no budget on an
ask (D343); Daoris publishes its own releases, the first on the owner's go
(D344); the Map and Knowledge move into Workspaces, six places on the rail (D332); an irreversible act is one held press,
reversible acts get a 5-second Undo, every owner act is an act any actor can be given (D340); the home keeps the name Overview;
the wide home draws its three bands as columns from 75rem, stacked below; Company is the place for accounts, roles, connectors
and plugins.

## 21. For the owner

None open. The one question the reconciling raised (should an ask carry a budget of its own) was answered on 2026-11-20 with
the wider rule of §13.6, recorded as D343: budgets follow how an account is paid, and an ask carries none.
