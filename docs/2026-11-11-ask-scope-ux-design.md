# ASKSCOPE2: choosing what an Ask Daoris conversation is about

Status: proposed 2026-11-11 for the owner's yes, under D253 point 3 and its 2026-11-11 note (the machine, a workspace or a
repository; the window's workspace by default; changed between messages; reach stays every workspace). It was written from a
survey of how other assistants scope a request (§0.1) and read against main at `178ea550d`. It settles each screen, its states and
its words in English and Chinese, the record and the wire, what goes, and the rows to build. Nothing here is built yet. Web paths
start at `src/Daoris.Web/src/`, driver paths at `src/Daoris.Desktop/Daoris.Desktop.Driver/`, modules paths at
`src/Daoris.Desktop/Daoris.Desktop.Modules/`.

**Widths.** **N** is 300 px, the side bar's floor at every window width (`docs/archive/2026-10-20-ui-ux-design.md` §3.4). **D** is 24rem
(384 px), the panel's own default (`help/AskPanel.tsx:208`). **W** is the side bar at 45% of a 1546 px window. A region lays out by
its own width (R7).

**Sample names.** Workspaces *Games* and *Work*; Games holds `engine` and `tools`, Work holds `api` and `site`.

## 0. In short

- **One control, in the composer bar.** A focus chip stands before the access chip (`help/AskPanel.tsx:197-198`). It opens a
  picker of places: this window's workspace, *Recent*, *This machine*, then the other workspaces, each opening onto its
  repositories. It reuses the new conversation's repository picker (`work/start/RepositoryPicker.tsx`), whose combobox becomes one
  unit both use.
- **The conversation owns its focus.** The focus is kept on its record. A new conversation takes the focus of the one in view, else
  the window's workspace. A change made between messages applies with the next message: the chip says so until then, and the
  transcript draws a divider where it applied.
- **The focus is where it starts, not where it stops.** The room is written for the focus, its first lines name it and its tools
  start there. Every workspace stays within reach. Each turn that looked or acted beyond the focus ends with one folded receipt,
  which offers *Make … the focus*.
- **Acts beyond the focus follow the access tier.** In full access they run, and they are listed in the receipt and in
  Notifications' Ask Daoris line, marked *outside its focus*. In restricted mode they are held on the person's card, which names the
  place.
- **The window no longer gates a conversation.** A conversation keeps its home workspace: its room, its line and its place in the
  history. It goes on from any window, so the read-only *Switch to … and continue* goes. So does the *Which workspace?* ask before a
  start in a window on every workspace (§7).
- **The word is *focus* / 聚焦** (§0.2).

### 0.1 What the survey found, and what this takes from it

The survey compared Codex, Claude Code (desktop and CLI), Cursor, GitHub Copilot, Sourcegraph Cody, Devin, Windsurf, JetBrains AI,
Zed, ChatGPT and Claude Projects, Notion AI, Slack AI, Glean and Rovo. It found three shapes:

- **A picker in the composer row, set at start.** Claude desktop's folder control sits under the prompt box. Copilot's coding agent
  and Devin start from a repository dropdown.
- **`@` or `#` per message.** Cursor, Zed, Copilot, Notion and Cody use it. It adds context to one message but sets no boundary.
- **A container.** Codex projects and Claude or ChatGPT Projects work this way: to change scope, you start a new chat.

Two products tell the model about a new scope. Claude Code's `/cd` keeps the conversation and reloads the new folder's
instructions; Codex loads only the primary folder's.

On reads outside the scope, Claude Code asks once and the answer can be remembered. Codex asks at the sandbox's boundary. Nobody
shows a receipt that another workspace was searched, and Rovo's users ask for one.

**What users report going wrong:**
- **A silently wrong root.** Cursor starts a multi-root chat in folder 1, so git runs in the wrong repository.
- **Inheritance.** A new session takes the global last-used folder instead of the open session's. A folder picked before the first
  message is ignored. The label does not show the folder the agent actually worked in.
- **A recent list that truncates paths,** so similar repositories cannot be told apart.
- **A scope that is too narrow.** D253 records Daoris's own case: the workspace fence made the person relay requests between rooms
  by hand.

So this design takes:
- the composer picker, because it is always visible;
- the `/cd` model: the conversation is kept, the room is written again and a line is told;
- a receipt where others say nothing;
- inheritance from the conversation in view, and a focus chosen before the first message that is the conversation's first focus;
- names over paths, each repository with its workspace beside it;
- a reach that never narrows.

Sources: learn.chatgpt.com/docs/projects and /docs/sandboxing; code.claude.com/docs/en/desktop, /permissions and
/permission-modes; cursor.com/help/customization/context.md.

### 0.2 The word: *focus* / 聚焦

D253's note and the row say *scope*. The window says **focus** in English and **聚焦** in Chinese:

| Candidate | Why not |
|---|---|
| *scope* / 范围 | *Scope* reads as a limit, the fence D253 rejected. The window's own scope is already the workspace in the command center and status bar (`docs/areas/desktop-app.md` rule 36, `data/scope`). The glossary's `place` already avoids 范围. |
| *context* / 上下文 | The glossary's: what a session's model holds at once. |
| *place* / 位置 | The glossary's name for an entry of the Setup tree, which is the same three levels. A focus **is** a place. *Place* names the thing chosen, and *focus* names the conversation's relation to it, so the picker lists places and the chip says the focus. |
| 焦点 | The keyboard focus in the Chinese catalogue (`menu.keys.*`), and what a screen reader announces. |
| 关注, 着眼点, 主题 | 关注 is *follow* in most apps; 着眼点 is literary and long; 主题 is a conversation's title. |

The glossary row it needs (row a):

```json
{ "term": "focus", "en": "focus", "zh": "聚焦",
  "means": "What an Ask Daoris conversation is about: this machine, a workspace or a repository. Its room is written for it, its first lines name it and its tools start there; it limits nothing (D253 point 3).",
  "why": "Scope reads as the fence D253 rejected, and is the window's workspace already; 焦点 is the keyboard's focus. 聚焦 says where attention is put, not where it ends.",
  "avoid": { "en": ["scope"], "zh": ["焦点", "范围"] } }
```

Its `match` must not catch the two sentences that say *focus* for the keyboard (`menu.keys.bar`, `projects.git.changes.readAt`).
Row a shows that with `names:check -- --strict`.

## 1. The focus chip and its picker

**Where.** The chip goes in the composer bar, the row above the box that holds the access chip and *What it has*
(`help/AskPanel.tsx:197-198`), and it comes first. In the tab order it comes after the transcript and before the access chip. At D
and W the row is one line. Below 24rem it wraps by its own width, with the focus chip alone on the first line.

```
 D · 384 px: a conversation in Games, the default focus
│ …the conversation…                                    │
├───────────────────────────────────────────────────────┤
│ [▣ Games · this window ˅]  [Restricted ˅]  What it has│
│ ┌───────────────────────────────────────────────────┐ │
│ │ Ask about Daoris on this machine…                 │ │
│ └───────────────────────────────────────────────────┘ │

 N · 300 px: the row wraps by its own width
├──────────────────────────────────────┤
│ [▣ Games · this window ˅]            │
│ [Restricted ˅]  What it has          │
│ ┌──────────────────────────────────┐ │
```

**The chip** shows its kind's icon (⌂ this machine, ▣ a workspace, ◫ a repository, using the icon set's existing `projects` for a
repository) and the place's name. The name is cut at its end. The kind and the full name are in its label, never in a tooltip
alone.

| State | Chip (en) | Chip (zh) | Label for assistive technology (en / zh) |
|---|---|---|---|
| Default: the window's workspace | `▣ Games · this window` | `▣ Games · 此窗口` | Ask Daoris's focus: workspace Games, this window's / 问道衍的聚焦：工作区 Games（此窗口的） |
| Chosen | `◫ engine` | `◫ engine` | Ask Daoris's focus: repository engine in Games / 问道衍的聚焦：Games 中的仓库 engine |
| This machine | `⌂ This machine` | `⌂ 本机` | Ask Daoris's focus: this machine / 问道衍的聚焦：本机 |
| Pending (D, W) | `▣ Games · engine next message` | `▣ Games · 下一条消息起 engine` | …, engine from your next message / …，从你的下一条消息起为 engine |
| Pending (N) | `◫ engine · next` | `◫ engine · 下一条起` | as above |
| Gone | `⚠ engine · gone` | `⚠ engine · 已不存在` | Ask Daoris's focus: repository engine, no longer on this machine / 问道衍的聚焦：仓库 engine，已不在本机 |
| Gone, widening | `⚠ engine · gone · Games next message` | `⚠ engine · 已不存在 · 下一条消息起 Games` | …, widened to Games with your next message / …，下一条消息起放宽到 Games |
| Reading the places | `… ` disabled, *Reading this machine's workspaces…* under it (`help.scope.reading` stands) | `正在读取本机的工作区…` | |

**Rules for the states:**
- **Not colour alone.** A pending chip carries the accent-soft ground the access chip's switch wears, and its words. A gone one wears
  ⚠ and `--warn`, and its words.
- **No chip where no words can go.** An ended conversation that cannot go on has no composer, so it has no chip; its head still
  names the focus (§2). An ended one that can go on keeps the chip, and a change applies with the words that resume it.
- **The one-workspace machine.** Where the only workspace is `default`, the default focus is *This machine*, and the picker offers
  no workspace level. This is the history's *unlabelled* case today (`help/ask/useHelpHistory.ts:26-31`).
- **A window on every workspace** starts on *This machine*, marked *this window*.

**The picker** is the repository picker's combobox (`work/start/RepositoryPicker.tsx:56-58`): the focus stays in the field, the
arrows move the active row, Enter takes it and Escape puts it down. Because the chip sits at the panel's foot, the picker opens
**upward**. It is 20rem wide, or the panel's width less 1.5rem where that is smaller, so 276 px at N. Its height is 20rem, or the
space above the chip less 1rem.

```
 N · 300 px: the picker open, Work unfolded
│ ┌──────────────────────────────────┐ │
│ │ ⌕ Find a workspace or repository │ │
│ ├──────────────────────────────────┤ │
│ │ ✓ ▸ ▣ Games          this window │ │
│ │ Recent                           │ │
│ │     ◫ engine               Games │ │
│ │     ▣ Work                       │ │
│ │     ◫ api                   Work │ │
│ │   ⌂ This machine  every workspace│ │
│ │ Other workspaces                 │ │
│ │   ▾ ▣ Work        2 repositories │ │
│ │       ◫ api                      │ │
│ │       ◫ site    no checkout here │ │
│ ├──────────────────────────────────┤ │
│ │ Focus only: Ask Daoris still     │ │
│ │ looks in every workspace.        │ │
│ └──────────────────────────────────┘ │
│ [▣ Games · this window ˄]            │

 the picker while a change is pending: its first line, and the way to keep the focus as it is
│ │ engine applies with your next    │ │
│ │ message.                         │ │
│ │   ▣ Keep Games                   │ │
│ │ ✓ ▸ ▣ Games          this window │ │
│ │ …                                │ │
```

**Order.** It never changes with what is chosen:
1. The window's workspace, marked *this window*. In a window on every workspace, *This machine* takes this row.
2. *Recent*: the last five places chosen in Ask Daoris on this machine, newest first, never the first row's. It is kept as the new
   conversation's Recent is (`work/start/startRecent.ts`), under its own key. A repository names its workspace beside it.
3. *This machine*, with *every workspace* beside it.
4. *Other workspaces*, by name, each with its count of repositories and unfolding onto them by name. A repository with no checkout
   here can be chosen, with *no checkout here* beside it, since a focus is about records as much as a checkout.

**Search.** Typing flattens the list into one, with no place twice: Recent's matches first, then workspaces, then repositories, each
repository with its workspace beside it. A workspace's name also matches its repositories, as the repository picker's does
(`RepositoryPicker.tsx:31`). With nothing found: *Nothing here matches “…”.*

**Same names.** Two repositories with one name in two workspaces are told apart by the workspace beside each. The name is cut before
the workspace is, and the workspace is never cut below six characters.

**Keys:**
- The chip opens on Enter or Space, with the current focus active. If that focus is a repository, its workspace is open.
- ↑ and ↓ move and wrap.
- → opens the active workspace when the caret is at the field's end, and ← closes it, or goes from a repository to its workspace,
  when the caret is at the start. Typing a workspace's name lists its repositories without either.
- Enter chooses, puts the picker down and returns the focus to the chip.
- Escape puts it down with nothing chosen. Tab puts it down and moves on.
- An input method's Enter and Escape are its own, as in the repository picker.
- The command table gains one verb, *Ask Daoris: Change focus…* / *问道衍：更改聚焦…*. It opens the panel with the picker open
  (rule 34). It has no key.

**What a choice does.** The choice is sent at once as `HELP_FOCUS` (§6), never held in the page alone, so a reload or another window
shows the same pending change. Choosing the applied focus again clears a pending one (*Keep Games*). A refusal is said under the
chip, in the composer bar (R3).

| Words | en | zh |
|---|---|---|
| The picker's field | Find a workspace or repository | 查找工作区或仓库 |
| The marks | this window · every workspace · {{count}} repositories · no checkout here | 此窗口 · 全部工作区 · {{count}} 个仓库 · 本机没有检出 (`work.start.noCheckout`) |
| The groups | Recent · Other workspaces | 最近 · 其他工作区 |
| Nothing found | Nothing here matches “{{typed}}”. | 没有与“{{typed}}”匹配的工作区或仓库。 |
| The foot | Focus only: Ask Daoris still looks in every workspace. | 只是聚焦：问道衍仍会查看每个工作区。 |
| Pending | {{next}} applies with your next message. · Keep {{now}} | {{next}} 从你的下一条消息起生效。· 保持 {{now}} |
| Gone | {{place}} is no longer on this machine. Your next message widens the focus to {{wider}}. | {{place}} 已不在本机。你的下一条消息会把聚焦放宽到 {{wider}}。 |

**Which focus a new conversation takes:**

| A conversation started… | Takes |
|---|---|
| Blank, with the panel first opened | the window's workspace (*This machine* in a window on every workspace or on a `default`-only machine) |
| By *New conversation* while one is shown | the shown one's applied focus, never its pending one |
| From an earlier one (`HELP_START_FROM`) | the earlier one's applied focus |
| From the new-conversation composer's *Make it a task with Daoris…* | the repository chosen there; its words go as typed, without today's *In engine:* prefix (§7) |
| From the palette's *Ask Daoris: "…"* | as a blank one |
| Opened ahead, before any words | the default above; a focus chosen before the first message is its first focus, with no divider |

## 2. The conversation's head

The open conversation's head keeps its row: *← History*, the title, and *New* at the end (`help/AskPanel.tsx:239`). Under the title
it gains one quiet line: the **focus as a breadcrumb**, followed by its marks. The focus's own segment is in ink and its ancestors
are faint. A workspace or repository segment is a door to its page in Repositories; *This machine* is no door. The breadcrumb only
names the focus: the chip is the one way to change it (D228).

```
 D · a Games window, the focus Games' engine
│ ← History                                   New ⊕ │
│ Why does the release wait?                        │
│ This machine › Games › engine                     │

 N · a Work window, a conversation of Games opened from All workspaces, focused on its engine
│ ← History                        New ⊕ │
│ Why does the release wait?             │
│ This machine › Games › engine          │
│ ◇ this window: Work · started in Games │
```

| Mark | When | en | zh |
|---|---|---|---|
| The window | The focus is neither the window's workspace nor inside it, and the window shows one workspace | ◇ this window: {{workspace}} | ◇ 此窗口：{{workspace}} |
| The home | The conversation's home is not the window's workspace | started in {{workspace}} | 始于 {{workspace}} |
| Pending | A change waits for the next message | · {{next}} next message | · 下一条消息起 {{next}} |
| Gone | The focus is no longer on this machine | ⚠ no longer on this machine | ⚠ 已不在本机 |

The marks are `text-meta`, each with its glyph, and they wrap at N. The breadcrumb's landmark label is *Focus* / *聚焦*. This line
replaces `help.scope.in` and `help.scope.elsewhere`'s sentence (§7).

## 3. History: by home, filtered by place

Today the history shows the window's workspace's conversations or every workspace's (*Whose conversations*,
`help/ask/useHelpHistory.ts:26-35`). It gains a **Showing** chip at its head, which opens the focus picker with two rows above
Recent:

- ***This workspace · Games*** is the default. It lists the conversations whose home is the window's workspace, grouped by day with
  the pinned first, as today.
- ***All workspaces*** lists every conversation, **grouped by home workspace**: the window's workspace first, marked *this window*,
  then the others by name. Within a group the pinned come first, then the newest, each row with its time.
- ***About {{place}}*** is any place chosen below those two. It lists the conversations whose home is that place or inside it, or
  that held a focus there at any time (`foci`, §6), grouped by home as above.

The search runs within what is shown. A row shows a **focus chip** where its applied focus is not its home workspace. A row whose
focus is its home shows none, so the usual case stays quiet.

```
 N · This workspace (the default)
│ ← Conversation history          New ⊕ │
│ Showing [This workspace · Games ˅]    │
│ ⌕ Search conversations                │
│ Pinned                                │
│   Release checklist        ◫ engine   │
│ Today                                 │
│   Why does the release wait?          │
│   Move the site's quests     ▣ Work   │
│ Yesterday                             │
│   Clean the old trees  ⌂ This machine │

 N · All workspaces
│ Showing [All workspaces ˅]            │
│ Games · this window                   │
│   Release checklist  ⌖    ◫ engine 2h │
│   Why does the release wait?   14:02  │
│ Work                                  │
│   Fix the site deploy      ◫ site Mon │

 N · About engine
│ Showing [About engine ˅]              │
│ Games · this window                   │
│   Release checklist        ◫ engine   │
│   Why does the release wait?          │
```

(⌖ marks a pinned row.)

**Opening a row of another home** opens the conversation in place, with its head marked *started in Work* (§2). It goes on there:
the window is not switched and nothing is read-only (§7). If its home already runs another conversation, words to an ended one are
refused with today's sentence naming the one running, and a press opens it (one running per workspace stands).

| Words | en | zh |
|---|---|---|
| The filter's label (replaces *Whose conversations*) | Showing | 显示 |
| Its values | This workspace · {{workspace}} · All workspaces · About {{place}} | 此工作区 · {{workspace}} · 全部工作区 · 关于 {{place}} |
| A group | {{workspace}} · this window / {{workspace}} | {{workspace}} · 此窗口 / {{workspace}} |
| A row's chip label | focus: {{place}} | 聚焦：{{place}} |
| Empty, About | No conversation is about {{place}} yet. | 还没有关于 {{place}} 的对话。 |
| Searched, About | {{count}} conversation about {{place}} holds “{{words}}”. Every conversation about {{place}} was searched. (and its plural) | 关于 {{place}} 的对话中有 {{count}} 段含有“{{words}}”。已搜索关于 {{place}} 的全部对话。 |

`help.history.every`, `emptyIn`, `noneHintIn` and `searchedIn` stand. `help.history.in`, the per-row *in Games*, goes: the group
says it.

## 4. Beyond the focus: what it looked at, and what it did

**What counts as beyond.** A place is beyond the focus when it is neither the focus nor inside it:
- **This machine:** nothing is beyond.
- **A workspace:** every other workspace and its repositories are beyond.
- **A repository:** other workspaces are beyond, and so is any other repository's checkout, session or press. Its own workspace's
  shared records (knowledge, quests and asks) are near rather than beyond. The room holds them, and its tools start in that
  workspace, so they would mark every turn.

**What is seen.** Only what passes Daoris's own doors is listed:
- the knowledge connector's reads, with the workspace they named or `all`;
- the judged checkout read;
- a delegate press;
- a proposal naming a repository;
- for a harness that reports its tool calls in structured form, a file tool whose path lies in a registered checkout.

A shell command is never parsed. The receipt claims nothing it did not see, and its folded rows say *what Daoris's doors saw*.

**One receipt a turn,** folded, under that turn's answer. A turn that stayed within the focus has none.

```
 D · folded
│ Ask Daoris                                        │
│ The release waits on Work's landing queue, which… │
│ ▸ Looked beyond the focus: Work, api              │

 D · unfolded, full access
│ ▾ Looked beyond the focus · what Daoris's doors saw │
│   Searched workspace Work · 3 found                 │
│                              [Make Work the focus]  │
│   Read repository api (Work): README.md             │
│                              [Make api the focus]   │
│   Did: paused quest Q-12 in Work · Done             │

 D · unfolded, restricted
│   Asked you: pause quest Q-12 in Work · Waiting     │
```

**Make … the focus.** The press chooses that place as the focus. Like any change it is pending until the next message, so the chip
and head say so. While the place is the applied or pending focus, the press is disabled with *Already the focus*.

**Acts beyond the focus follow the access tier** (D253 points 1, 5). The focus is never a fence, so no tier asks *because* of it.
- **Full access.** The press runs at once, as the person's. It is listed in the turn's receipt with its outcome, and in
  Notifications' Ask Daoris line (`notifications/HelpPressLine.tsx`). Each of that line's rows gains its place and, where the place
  was beyond the conversation's focus at the press, *outside its focus*.
- **Restricted.** The press is held on the card that already puts Ask Daoris's presses to the person, which gains one line naming
  the place outside the focus. The receipt lists it as *Asked you…*, with the card's state (waiting, allowed, refused, ended
  unanswered).
- **Reads.** Reads never ask, in either tier.

```
 Notifications · the Ask Daoris line, unfolded
│ What Ask Daoris pressed · 3                                     │
│   Paused quest Q-12 · in Work, outside its focus · Done  Resume │

 Notifications · restricted, the person's card
│ Ask Daoris asks to pause quest Q-12                  │
│ In Work, outside this conversation's focus (Games)   │
│ [Allow]  [Refuse]                                    │
```

| Words | en | zh |
|---|---|---|
| The fold | Looked beyond the focus: {{places}} | 在聚焦之外查看了：{{places}} |
| Its source | what Daoris's doors saw | Daoris 的入口所见 |
| A search | Searched workspace {{workspace}} · {{count}} found / Searched every workspace · {{count}} found | 搜索了工作区 {{workspace}} · 找到 {{count}} 条 / 搜索了全部工作区 · 找到 {{count}} 条 |
| A read | Read repository {{repository}} ({{workspace}}): {{what}} | 读取了仓库 {{repository}}（{{workspace}}）：{{what}} |
| An act, full | Did: {{act}} in {{place}} · {{outcome}} | 已执行：{{place}} 中的{{act}} · {{outcome}} |
| An act, restricted | Asked you: {{act}} in {{place}} · {{state}} | 已请你确认：{{place}} 中的{{act}} · {{state}} |
| The press | Make {{place}} the focus · Already the focus | 改为聚焦 {{place}} · 已是聚焦 |
| Notifications' row | in {{place}}, outside its focus | 在 {{place}} 中，在其聚焦之外 |
| The card's line | In {{place}}, outside this conversation's focus ({{focus}}) | 在 {{place}} 中，在这段对话的聚焦（{{focus}}）之外 |

`{{act}}` is the press in the window's route words, as `HelpPressLine` says it today. `{{outcome}}` and `{{state}}` are its existing
words (`notifications.helpPressed.outcome.*`).

## 5. A change mid-conversation

**It follows the access swap.** A change of focus is a change the next turn takes, as a change of access is (`Help/HelpSwap.cs`,
rule 58). A start hands the agent its room, and harnesses read their instruction file at start, so a room written again under a
running agent would go unread. The new focus would then be told in a line while the room still said the old one.

The sequence:
- **While a turn runs,** the chip and the head say *engine next message*. The turn goes on under the old focus, and its agent ends at
  the turn's end.
- **Where no turn runs,** the agent ends at once, as an idle one does for a swap.
- **The next words that start a turn** resume the same conversation in the same room folder, written again for the new focus, with
  the line below ahead of them. Words queued into a running turn (the composer's *Queue* and *now* levels) leave the change pending
  until the next turn starts.

```
 D · the transcript once the next message carried the change
│ You  What blocks the release?                     │
│ Ask Daoris  The landing queue in Work…            │
│ ─── Focus changed: workspace Games → repository ─ │
│     engine · 14:02                                │
│ You  And in its tests?                            │
```

**The divider** is drawn above the message that carried the change, from that message's record (§6), so a reload, another window and
a later reading all draw it. It is a rule with its words between, `text-meta`, with the time. A widening the driver made for a gone
place reads *Focus widened*.

| Words | en | zh |
|---|---|---|
| Changed | Focus changed: {{from}} → {{to}} | 聚焦已更改：{{from}} → {{to}} |
| Widened | Focus widened: {{from}} is no longer on this machine → {{to}} | 聚焦已放宽：{{from}} 已不在本机 → {{to}} |
| A place in either | this machine / workspace {{workspace}} / repository {{repository}} | 本机 / 工作区 {{workspace}} / 仓库 {{repository}} |

**What the agent is told.** One line goes ahead of the person's words, and ahead of the *Where the person is now* preface
(`help/where.ts:117`). It is agent-facing, in English, and never translated, as the preface is:

```
Focus is now repository `engine` in workspace `Games` (was workspace `Games`). Your reach is unchanged: name a workspace, or `all`, to look beyond it.
```

- **Widened:** `Focus is now workspace `Games`: repository `engine` is no longer on this machine. Your reach is unchanged: …`
- **The machine:** `Focus is now this machine: every workspace (was …).`

The preface's own `workspace \`Games\`` becomes `the window's workspace \`Games\``, since it says where the person is and the focus
says what the conversation is about.

**The room's projection.** The room is written for the focus from the facts as last judged, by one function over the three levels.
This is `Help/HelpRoomScope.cs:19`'s `Project`, taking a place instead of a workspace:
- **This machine:** the whole machine, as `default`'s room is today.
- **A workspace:** today's projection.
- **A repository:** its workspace's projection, with an *In focus* section first that holds the repository's own quests, asks,
  sessions, line and landing, then the rest of the workspace.

The room's first lines name the focus and the reach in the line's words. The knowledge connector is handed the focus as its starting
place: the focus's workspace, or `all` for the machine.

## 6. The record and the wire

**A place**, one shape on every side, is a twin of the page's `help/focus.ts` and the driver's `Help/HelpFocus.cs`. It is listed in
`.claude/knowledge/twins.md` with a test on each side holding the spelling:

```json
{ "kind": "machine" }
{ "kind": "workspace", "workspace": "Games" }
{ "kind": "repository", "workspace": "Games", "repository": "engine" }
```

A repository carries its workspace, because one name may stand in two workspaces. The registry is read for it at each use: a
repository moved to another workspace is followed by name, and the breadcrumb says where it is now.

**What a conversation keeps** on its help session's record, beside its `workspace` (its home, kept from its start and never moved):

| Field | Is | Written |
|---|---|---|
| `focus` | the place its last turn ran under | at its first words, and at each next words that apply a change |
| `focusNext` | the place chosen and not yet applied, or null | by `HELP_FOCUS`; cleared when applied, or by choosing `focus` again |
| `foci` | each place it held, first to last, none twice | appended when `focus` changes; read by the list's *About* filter |

A record from before reads `focus` as its home workspace and `foci` as that alone.

**What a message keeps:** the person's message that carried a change keeps `focus: { from, to, widened? }`. The first message keeps
`from: null` and draws no divider. No other message keeps anything.

**What a turn keeps:** its end writes a `beyond` event with one entry per place outside the focus that Daoris's doors saw:
`{ act: "search" | "read" | "press" | "propose" | "asked", place, what?, count?, outcome? }`. The console prints it as one line.

**The routes**, with their twins:

| Route | Change | Terminal twin |
|---|---|---|
| `START_HELP` (`Bridge/Routes/HelpRoutes.cs:35`) | takes `focus?`, defaulting as §1's table; answers `focus` | none (a start is the window's, as today) |
| `HELP_FOCUS` (new; `PipeClass.Person`, drains `Passes`) | `{ session, focus }` → `{ session, focus, focusNext }`. Before any words it sets `focus` and opens the conversation again ahead. A running one sets `focusNext` and is ended at its turn's end, or now if idle. Refusals: `unknown-place`, `not-help`, `ended` (one that cannot go on) | `daoris help focus <conversation> machine \| <workspace> \| <workspace>/<repository>` (`Help/HelpCommand.cs` gains the verb) |
| `HELP_CONVERSATIONS` (`:485`) | takes `about?: place`, which cannot be sent with `workspace` (refused `both`); rows gain `focus` and `focusNext` | `daoris help list --about <place>` |
| `HELP_START_FROM` (`:586`) | the new one takes the earlier one's applied `focus` | unchanged |
| `HELP_WIRED` | the start's receipt records the focus and the connector's starting place (rule 59) | unchanged |
| `SESSION_INPUT` | unchanged: the driver reads `focusNext` when the words arrive | unchanged |
| `STATE`'s `standingConfirmation.helpPresses[]` | each gains `place` and `beyond` | none |
| The asked-press card | gains `place` and `beyond` | none |

**What the driver composes at a turn**, in order, when words arrive for an Ask Daoris conversation:
1. **The place.** It reads `focusNext`, else `focus`, and resolves it against the registry now. A gone repository widens to its
   workspace, and a gone workspace to this machine, marked `widened`.
2. **The room.** If the place differs from the room last written, it writes the room in the home's folder
   (`HelpRoom.PathOf(home, workspace)`, unchanged) for the place.
3. **The agent.** It resumes, or starts where none ran, in that folder under the same conversation, with the connector's starting
   place set to the focus. The swap had already ended the agent (§5).
4. **The words.** It sends the focus line, then the preface, then the person's words.
5. **The record.** It records the message's `focus`, the row's `focus` and `foci`, and clears `focusNext`, then tells the windows
   (`sessions`).
6. **The receipt.** Through the turn, the doors in §4 note each place they touch. At its end, those beyond the focus are written as
   `beyond`, and each press's `HelpPressRecord` has its `place` and `beyond`.

## 7. What goes, what is replaced, what stays

**Goes (D228), in the chunk that brings its replacement:**

| Code or words | Why it goes | Replaced by |
|---|---|---|
| `help/ask/useHelpPlace.ts:58`'s `elsewhere`, and `help/AskConversation.tsx:246`'s `readOnly \|\| elsewhere` | A conversation of another home went on only once the window had switched to it | It goes on in place; its head says *started in …* (§2) |
| `help.scope.elsewhere`, `help.scope.switch`, `help.scope.notOffered`; the panel's use of `useScope().setWorkspace` to continue | as above | §2's marks |
| `help.scope.which`, `help.scope.startIn`, and `useHelpPlace.ts:24`'s `startIn` asking before a start in a window on every workspace | A window on every workspace asked which workspace before any start | The chip: that window starts in `default`'s room, the machine's, as a press naming none does today (`Help/HelpStart.cs:52`), focused on *This machine*. A focus chosen in the chip is the choice the ask made |
| `help.scope.in` | The conversation's workspace said in a sentence | The breadcrumb |
| `help/ask/useHelpHistory.ts:26-35`'s `everyWorkspace`, `offerEvery` and two-way `historyScope`; `help.history.scope`; `help.history.in` | The history's two-way choice and per-row label | *Showing* and its places; groups by home |
| `work/start/NewConversation.tsx:388`'s `work.start.taskIn` prefix (*In engine:*) | The repository was said in the words | The handed draft's focus |
| `HelpRoomScope.Project(whole, workspace)` taking a workspace | one function over one level | the same function over a place |
| The scope tests that hold the old rule (`help/AskDaoris.scope.test.tsx:180,225`) | | rewritten to the new rule |

**Stays:**
- **One running conversation per workspace.** The home's ledger refuses a second while one runs, naming it.
- **The room per workspace.** It is the folder the harness keeps a conversation by, so a conversation never moves its folder; its
  focus changes what is written in it.
- **The rest of the home's ownership.** Its history and its place in the history, the opened-ahead conversation of the window's
  workspace (`readiedIn`), and the panel showing the window's workspace's newest by default.
- **Every workspace within reach,** in both modes. The tools take a workspace or `all` (rule 42).
- **The access chip, its swap and *What it has*,** unchanged except for the focus in the start's receipt.

**The areas' sentences to rewrite when built:**
- conversations-and-context rules 42 and 43: *its tools start there* becomes *start at its focus*; the history by home and by place;
- roles-and-supervision rule 58: the press's place and *outside its focus*;
- desktop-app rule 31: the Ask Daoris line names each press's place.

## 8. Rows, in build order

Every visible row proves itself with:
- page tests in en and zh;
- stories at N, D and W, both themes, keyboard throughout;
- the installed window where the row changes it, driven through `tools/desktop.mjs` ("installed").

Each row writes its own translations against row a's term.

| Row (lanes; after) | What a person sees working | Proof |
|---|---|---|
| **ASKSCOPE2a** the term (web) | *focus* / 聚焦 in the glossary, before any visible word | `names:check -- --strict` knows the row, passes the two keyboard sentences, and fails on a planted 焦点 or *scope* in a focus label |
| **2b** the focus on the record (driver, modules) | `daoris help focus <id> Games/engine` and `daoris help list --about Games/engine` answer from the record; `START_HELP` takes and answers `focus`; `HELP_START_FROM` carries it | driver tests: a focus set before words is the first and draws no change; a change sets `focusNext` and nothing else; choosing `focus` again clears it; `unknown-place`, `not-help`, `ended`; `about` matches home or any focus held, and inside; the terminal's answer equals the route's; a record from before reads its home |
| **2c** the room and the turn (driver; b) | in a terminal, a conversation focused on `engine` answers from a room that starts with *In focus*; the next words after a change carry the line | `HelpRoomScope` over the three levels; the swap ends an idle agent now and a busy one at its turn's end, and resumes in the same folder; the line exact for change, widening and the machine; a gone repository widens with the message's `widened`; `HELP_WIRED` names the focus; one running per workspace still refuses |
| **2d** the picker, the chip and the head (web; a, b) | §1's chip and picker in Ask Daoris and §2's breadcrumb with its marks; the combobox shared with the new conversation's picker; the window-bound paths of §7 removed in the same chunk | picker tests (order, Recent, search none-twice, the arrow keys at the caret, upward at N, same names told apart, `default`-only machine); chip states, every one in a story; §1's inheritance table, one test a row; `AskDaoris.scope.test.tsx` rewritten (another home goes on in place; a window on every workspace starts on *This machine* with no ask); the repository picker's tests unchanged; installed |
| **2e** the divider (web; c, d) | §5's divider from the record, after a reload and in another window | the message's `focus` draws it; `widened` reads *Focus widened*; the first message draws none; installed: a change while a turn runs, pending until the next message |
| **2f** the history (web, modules; b) | §3's *Showing*, groups by home, *About*, a row's chip | each filter against a fixture of homes and foci; search within each; the chip only where focus ≠ home; opening another home's row goes on in place; installed |
| **2g** the receipt (driver, web; c) | §4's receipt under a turn that looked beyond, *Make … the focus* | each door notes its place; near versus beyond for each level; no receipt for a turn within; a shell command claims nothing; the press makes a pending change; installed: a search across Work from a Games conversation |
| **2h** acts beyond the focus (modules, web; g) | Notifications' Ask Daoris line and the restricted card name the place and *outside its focus* | `HelpPressRecordTests` with `place` and `beyond`; `HelpPressLine.test.tsx` in both languages; the card's line; installed in both tiers |
| **2i** the installed window (the steward; h) | the whole flow: Games window, focus engine, a search into Work, *Make Work the focus*, a change mid-turn, the history by home, a Work conversation continued from a Games window | dated shots in en and zh, both themes, at N and D |

## 9. Rejected

- **`@` per message** (the survey's B). It is invisible between messages and filters no history, it leaves open whether it narrows
  or adds, and it would be a second way to say what a conversation is about.
- **The focus as a container** (the survey's C). A change would mean a new conversation, which D253's note rules out, and requests
  would be relayed between conversations again.
- **Writing the room again under a running agent.** A harness reads its instructions at start, so the line and the room would
  disagree until the next start. The swap at the next turn is the way access already changes.
- **The home following the first focus.** A Games window could then start a conversation homed in Work while Work runs one, so the
  start would have to choose between two running conversations, and the window's panel would show a conversation another workspace
  owns.
- **Asking before the first read beyond the focus** (Claude Code's model). That is the fence with a door, which D253 rejected, and
  restricted mode already asks for every act.
- **A receipt per tool call, inline.** Noise in a narrow panel; one folded receipt a turn says the same.
- **Breadcrumb segments that change the focus.** A second way beside the chip.
- **A row's chip for every focus held.** The filter answers *about*; the row says where it is now.

## 10. Open, and what to measure first

**Open:**
- **A focus offered from where the person is.** When the window's view has a repository chosen, the picker could offer it first.
  The survey found a request to infer the place and confirm it; the default stays the window's workspace until it is measured.
- **More than one place at once.** The survey's case against A. One focus until a person asks for two.
- **Shell commands in the receipt.** A structured harness's shell tool names its command, never its reach; parsing commands would
  guess.

**Measure first:**
1. **In the installed window, in en and zh:** from a Games window, ask about Work's `api` and count the presses to focus it. The
   path is chip, then type, then Enter: three presses.
2. **Whether the receipt is read:** how often *Make … the focus* is pressed from it, against how often the chip is.
3. **The cost of a change:** the seconds from the next message to the first answer, against a message with no change, since each
   change resumes the agent.
