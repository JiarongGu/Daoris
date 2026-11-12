# CHATUX1: chatting with Daoris reads and handles like a modern assistant's desktop app

Status: proposed 2026-11-12 for the owner's yes, on the owner's words of that day (TASKS.md, *Conversations and context*: CHATUX1
and ASKPUSH1). D279 is reserved for the choices that reject an alternative (§9). It was written from the code on main at
`91fc3fa49b`, the area `docs/areas/conversations-and-context.md`, D188 and its evidence `docs/2026-10-03-steer-evidence.md`. Nothing here is
built. Web paths start at `src/Daoris.Web/src/`, driver paths at `src/Daoris.Desktop/Daoris.Desktop.Driver/`.

**Widths.** **N** is 300 px, the side bar's floor; **D** is 24rem, the dock's default; **W** is the right region at 45% of a 1546 px
window; **C** is a centre column in a 1920 px window with the list open (`docs/archive/2026-10-20-ui-ux-design.md` §3.4).

## 0. In short

- **Most of the machinery is already there; the frame and a few handles are not.** Daoris already streams chunk by chunk, folds
  work into runs, follows the tail until the reader scrolls away, highlights code with *Copy* and *Wrap*, takes files by drop, paste
  and chooser, lists Ask Daoris's conversations by day with a search over every word, and queues, edits and re-levels words
  said mid-turn. What it lacks is a reading column, a composer that is one box with one primary press, a handle on each message
  (copy, retry, edit), keys, a run that says what it did, a *Send now* that works on every door, and a list of a session's
  conversations by date (§2).
- **Eleven rows, in order** (§8): the column, the composer bar, *Send now* (ASKPUSH1), per-message handles, keys, run summaries,
  attachments in view, one new-conversation press everywhere, the lists, edit-and-resend from an earlier turn, and the installed
  window.
- **Three choices reject an alternative** (D279, §9): the measure belongs to the conversation, never to the page; *Send now* is
  the person's own press and, where a door cannot take words mid-turn, it stops the turn keeping the session; and editing a sent
  word never rewrites the record or the agent's conversation.
- **What Daoris keeps of its own stays Daoris's** (§3): its cards, presses, receipts, the access chip and the focus. They are drawn
  from the record in the column's width, never as the agent's text, so a copy of an answer copies only the agent's words.
- **No model is used to make this nicer.** No title is summarised, no answer scored. A person names a conversation; Daoris words
  only what its records say (D1, `HelpConversations`).

## 1. What was read, and what was not

**Read in the code:** the composer (`work/conversation/Composer.tsx`), the transcript (`ConversationView.tsx`, `ToolCard.tsx`,
`BlockLines.tsx`, `Markdown.tsx`, `CodeBlock.tsx`, `followTail.ts`), Ask Daoris's panel, history and conversation
(`help/AskPanel.tsx`, `AskHistory.tsx`, `AskConversation.tsx`, `help/focus/*`, `help/access/*`), the session rail
(`work/rail/*`), the command table and keys (`menus/commands.ts`, `menus/shortcuts.ts`), and, on the driver, the turn queue, steering,
stop, events and attachment doors (`Loop/Chat/*`, `Agents/AcpSteering.cs`, `Events/Domain/SessionEvents.cs`, `Course/Chat/ChatFiles.cs`).

**The three apps are not measured.** ChatGPT's, Claude's and DeepSeek's desktop apps are read here as the patterns they have in
common in public use, from what is known of them, not from a build run beside Daoris. Each pattern in §2 is named for what it does
for a reader; a row that would depend on one app's exact behaviour says so and measures first (§10).

**Shots.** The window itself was not photographed: `tools/desktop.mjs build` built the platform bundle but stopped at the service
host, whose compile fails here against the local Lyntai library (`LyntaiMemory.cs` names `Lyntai.Memory.IMemoryView` and
`IMemoryViewCheck`, which that library does not have), so there is no shell for `tools/desktop.mjs shot` to photograph. That is a
fault of the library's state, not of this design, and is not fixed here. §1.1 records the shots that were taken in its place, and
row **k** takes the window's own.

### 1.1 The shot pass, as component stories

The surfaces were photographed as the stories the repository keeps for them, served by Storybook from this checkout, in a 1920 px
browser viewport (the window could not be resized, so every story stands in a wide page). They show the components as they draw
today, not the window's frame around them. The files are in the checkout's scratch folder and are not committed.

| Story | What it shows |
|---|---|
| `work-composer--levels-while-a-turn-runs` | A waiting word dashed above the box with *Edit* and *Remove* and its level chevron; the box; then one row of *Send*, its chevron, the paperclip, **Stop turn**, **Finish** and **Stop** in the danger tone. Four presses that stop or end something beside one that sends |
| `work-sessionbox--running-with-controls` (a door told no levels) | A waiting word with *Edit* and *Remove*, the box, and a single *Queue* press with nothing beside it in this story |
| `work-conversationview--running` | The person's words in a bordered card with the accent rule, a closed run *1 tool call · thought*, the agent's words unboxed, a tool card, *working…* and *running Edit src/level.rs for…*. The transcript spans the whole 1280 px the story gives it, lines of well over a hundred characters |
| `help-askpanel--open` at about 300 px and 430 px | Ask Daoris's head (*← History*, title, *+*), the person's words, an answer with a table and a code block with *Wrap* and *Copy*, then the box, and under it *Send*, the paperclip, **Finish**, **Stop**. The focus and access chips and the model are not in this story's composer |
| `help-askhistory--grouped` at both widths | *Conversation history · Kept on this machine only*, *New conversation*, a search box; *Pinned* and *Today* groups; a row is a title, a line of what it is about, and how long ago, with a ⋯ |

What the stories show that the code reading did not: the box has no frame of its own (the textarea sits above a loose row of five
controls); a long pasted title in the history wraps to four lines; and a transcript in a wide page reads as one long measure.

## 2. The patterns against today

One row per pattern the owner named. *Have* means the behaviour is built and kept; *differs* means built another way; *absent* means
nothing exists. The row says where it is built and the row of §8 that closes it.

| Pattern | What the apps do in common | Daoris today | Verdict, row |
|---|---|---|---|
| Conversation list, search, date groups | A persistent list, newest first under *Today*, *Yesterday*, *Previous 7 days*, a search over titles and text | Ask Daoris: a history that replaces the panel, grouped by day (`help/history.ts`: pinned, today, yesterday, this week, older) or by home, a search over every conversation's words marked in the row, pin, rename, delete of an ended one, *Show more* pages (`AskHistory.tsx:116`, `HELP_CONVERSATIONS`). A session's conversation: one row in the rail, arranged by state, repository or ask (`work/groups.ts:130`), searched by name and by content (`SessionRail.tsx`, `SESSION_SEARCH`); no date groups, no rename, no pin | Ask Daoris *have*. Sessions' conversations *absent* for date, rename and pin. **h** |
| A centred reading column | A transcript and a box in one measure, centred, whatever the window | `ConversationView` is `grid w-full`; its own comment rejects a cap as half a maximized window (`ConversationView.tsx:185`). The box is `px-4` full width (`Composer.tsx:425`). The area's rule 27 says no block keeps a measure of its own | *Absent*. **a** |
| Markdown, code blocks, copy | GitHub-flavoured Markdown, a language label, *Copy* on a block, *Copy* on a message | GFM with tables, task lists, links; fenced code highlighted, its language named, *Copy* and *Wrap* (`Markdown.tsx`, `CodeBlock.tsx`); images are links, never fetched; no math; no message copy | Blocks *have*. Message copy *absent*. **d** |
| Streaming and Stop | Text grows as it arrives; the *Send* button becomes *Stop* while it runs | Chunks arrive pushed, batched in 120 ms (`SESSION_EVENTS`), joined and drawn as they grow; a live dot, *waiting for its first answer*, and *running <call> for 12 s*. Stop is a separate *Stop turn* button beside *Send*, *Finish* and *Stop* (Ask Daoris's session stop), four presses in one row (`Composer.tsx:708-724`) | Streaming *have*. Stop *differs*. **b** |
| Edit and resend, retry | Edit a sent message and resend; retry the last answer | Only a word still waiting behind a turn can be edited, removed or re-levelled (`Composer.tsx:240`). *Try again* is a session's act; *Say it again* re-says words a restart left uncertain (`NoteLines.tsx`). Neither is a turn's retry | *Absent*. **d**, **j** |
| Attachments by drop and paste | Drop anywhere, paste a screenshot, thumbnails | Drop anywhere on the form, paste, chooser, chips with name and size, ten files, 20 MB, stored outside the repository and never named to the page (`compose/carry.tsx`, `ChatFiles.cs:24`). No thumbnail; the transcript shows names only (`Attached`) | *Have*, without sight of an image. **g** |
| Model and mode in the composer | A model picker in the box; a mode (thinking, search) beside it | A session's model and effort sit at the far right of the action row, after four buttons (`Composer.tsx:727`, `SessionOptions.tsx`, `SET_SESSION_OPTION`). Ask Daoris's composer is passed neither `offered` nor `context` (`AskConversation.tsx:267-296`), so no model, no effort, no ring. Its mode (*Restricted* or *Full*) and its focus are two chips in a row above the box (`AskPanel.tsx:204`) | *Differs*. **b** |
| Thinking and tools folded, with a summary | *Thought for 12 s*; a line saying what was read and run | Work between two agent messages folds into one row counting calls, failures and thought (`RunView`); a call is closed unless it failed, with its kind, path and `+n −m`; a thought shows its first line; no summary of what the run did or how long | *Have* the fold. Summary *absent*. **f** |
| Scroll follows until the reader leaves | Follows; a *latest* press once away | `useFollowTail`: follows within 48 px of the bottom, never pulls a reader who scrolled up, *Back to bottom* offered, a live session opens at its tail (`followTail.ts`) | *Have*. Position per conversation not checked. **k** |
| Keyboard shortcuts | New chat, find, focus the box, stop, edit the last message, a cheat sheet | Enter sends, Shift+Enter a newline, an input method first, the `@` list's keys, Escape leaves an edit; F1 or Ctrl+Alt+I opens Ask Daoris; Ctrl+N starts a conversation; a drawer lists the table's keys but no in-field key. No key stops a turn, recalls the last message, finds in a conversation or focuses the box | Mostly *absent*. **e** |
| A new chat in one press | A button and a key | Ask Daoris: *+* in the open conversation's head, or *New conversation* in the history: one press, two if the panel is shut; it finishes the running one first, since Ask Daoris is one conversation at a time per workspace (D158). A session's conversation: Ctrl+N, then the repository, then words | *Differs*. **i** |
| *Send now* | A queued message can be sent at once | Sessions: the level menu has *Now, cutting its step*, enabled only where the agent steers (today Claude Code over ACP), and each waiting word's menu has *Send now*; elsewhere a separate *Stop its turn so this goes next…* press asks once (`work.steer.*`, D188). Ask Daoris is told no levels, so it only queues | Sessions *differ* by door. Ask Daoris *absent*. **c** |

## 3. What Daoris keeps of its own

An assistant app draws a chat. Daoris draws a line of work under a person's control, and these parts have no counterpart in those
apps. They stay, and the new frame must give each its place.

| Kept | Where it is today | Its place in the new frame |
|---|---|---|
| **Cards**: a question, a go-ahead, a held tool call, a helper's request, a proposed rule | `work/waits/*` (`PermissionAsks`, `QuestionCard`, `HelperAsks`), `help/ask/AskHeld.tsx`, `ExchangeCard.tsx` | Blocks in the column at its full width, never inside the agent's Markdown and never a message bubble. A wait shows in the head and above the box, one press from either (area rule 38); the box's *Send* and Enter keep answering a question |
| **Presses**: what Ask Daoris pressed or asked to press, its published list | `help/published/PublishedList.tsx`, Notifications' press lines | A strip under the head, outside the scroll, as now; a press waiting on the person is a card |
| **Receipts**: what a turn looked at beyond its focus, what a start was handed, the turn's meter, when a word was sent, taken and answered | `BeyondReceipt.tsx`, `HandedAccount.tsx`, `TurnMeter`, `ReceiptsLine` | Quiet lines under their turn, folded, in the column's width. A message's copy never includes them |
| **The access chip** (*Restricted* or *Full*, machine-wide) | `help/access/HelpModePicker.tsx` | In the composer bar's left cluster (§4.2). Still a chip that says what it is and opens the person's own choice; never a toggle |
| **The focus** (what a conversation is about, its crumb, its divider) | `help/focus/*` | The chip beside the access chip; the crumb under the head; the divider where a change applied. The keyboard focus is Daoris's own too: a card revealed from a turn takes it, the head's heading takes it from the history, and Escape goes back to the list the conversation was opened from |
| **Levels and waiting words** | `Composer.tsx` | Above the box, in the column; a word the turn took becomes the turn's ask |
| **Dividers and notes**: a move, a hand-over, a restart, a refused word | `NoteLines.tsx`, `FocusDivider.tsx` | As now. They are the record's own lines |

One rule keeps them apart from the agent's words: **what the record says is drawn by Daoris from typed events; what the agent
says is Markdown in the column.** Nothing of Daoris's is ever rendered through Markdown, so no agent can draw a card, and a
*Copy* on an answer takes the agent's text only.

## 4. The surfaces

### 4.1 The column

One measure for a conversation's transcript and its box, centred in the region when the region is wider, full width when it is not.
It is one `em` value held once, on the conversation region (`work/frame/ViewMain.tsx`'s conversation case and Ask Daoris's scroller
and box), so the two never differ. It starts at 52 em, about 680 px at the 13 px body, which holds a line to roughly 100
characters, and is measured at W and C (§10) before it is kept. The transcript scrolls under the whole width, so a scrollbar
stays at the region's edge. Wherever the region is narrower, as the dock at N and D, nothing changes, so no region varies by viewport (area rule 42).

Quick Ask's panel is already `min(42rem, 92vw)` and takes the same value.

### 4.2 The composer bar

One framed box, as the apps draw it: the words above, one bar below.

```
┌──────────────────────────────────────────────────────────┐
│ waiting words, in the order sent …                       │
│ chips of attached files, an image's thumbnail …          │
│ Message …                                                │
├──────────────────────────────────────────────────────────┤
│ ＋ ⌖ Games ▾ ◉ Restricted ▾        Model ▾ Effort ▾ ◔  ➤  │
└──────────────────────────────────────────────────────────┘
```

- **Left:** *Attach* (`＋`), Ask Daoris's focus chip and access chip.
- **Right:** the agent's model and effort as it offers them, the context ring, then **one primary press**.
- **The primary press is *Send* when the box holds words or files, and *Stop* when a turn runs and the box is empty.** It is the
  turn's stop and keeps the conversation (the *Stop turn* press today); Escape does the same in an empty box while a turn runs
  (§4.5). With words it is *Send*, whose chevron holds *When its turn ends*, *At its next step* and *Send now* (§5).
- **The session's two endings, *Finish* and *Stop*, leave the box** for the head's ⋯ menu beside *Details* (Sessions already keeps
  *Stop* in the page header, area rule 37). The acts and their confirms are unchanged; they have no key (area rule 29). A conversation
  that has ended shows the sentence *This session has ended* where the press stood, as today.
- **Ask Daoris's composer is handed the same options and ring a session's is** (`offered`, `onOption`, `context`), read through
  `SESSION_OPTIONS` and set through `SET_SESSION_OPTION`, which are keyed by a conversation's id and so reach Ask Daoris's the same way
  (row **b** confirms that against its protocol door first). A door that offers none shows none, and the model's name is the agent's own word, never Daoris's. The mode is Ask Daoris's access chip;
  a session has no mode (the driver refuses it, `ChatOptions`).
- A chip collapses to its icon at N, by the panel's container width, as the focus chip does today (`@max-[24rem]`).

### 4.3 What a turn reads like

- **Streaming stays chunked.** The driver pushes the agent's message and thought chunks, the page joins and draws them. No caret is
  added: the live dot under the turn and the call line already say a turn runs. What is measured (§10) is the cost of drawing a long
  message again each 120 ms batch, since Markdown is parsed from the start each time.
- **A run says what it did.** The fold line reads *Read 3 files · Edited 2 · Ran 4 commands · 12 s*, from each call's own kind and
  the events' own times, failures in the failed tone as now, an unnamed kind as *used a tool*. A thought reads *Thought for 12 s*
  where its events carry times and *Thought* where they do not, never a guess. A turn's whole line stays the meter after it.
  Nothing is inferred from a title (`ToolCard`'s rule).
- **A message has handles** (§4.4), shown on hover and on focus within, never occupying space at rest.
- **Scroll.** `useFollowTail` already does what the apps do. Two checks only: that a conversation reopened from the history returns to
  where it was left, as the list does, and that *Back to bottom* says nothing the dot does not.

### 4.4 Handles on a message

| On | Press | Does |
|---|---|---|
| An agent message | *Copy* | The message's Markdown source, as the agent wrote it, to the clipboard. A code block keeps its own *Copy* |
| The person's words | *Copy*; *Edit* | Copy as written. *Edit* puts them in the box (§6) |
| The last turn, once it has ended | *Retry* | Says the same words again as a new turn. The agent sees its first answer too: Daoris never takes one back out of the agent's conversation (area rule 6). The press's tip says so |
| An earlier turn's words | *Edit* | Starts a new conversation from there with the words edited (§6) |

No *thumbs*, share or export: Daoris keeps no feedback store, and an export is a different row (§10). *Retry* reuses the path *Say it
again* takes today (`onSayAgain`); the two are one press with two wordings, not two ways (D228).

### 4.5 Keys

One table, `composerKeys`, read by the handler and by the shortcuts drawer, so the drawer lists the in-field keys it lists none of today.

| Key | Does | Where | State |
|---|---|---|---|
| Enter · Shift+Enter | Send at the person's default · a newline | The box | Exists |
| Ctrl+Enter | *Send now* while a turn runs (§5); *Send* when none does | The box | New |
| Escape | Stop the turn, in an empty box while one runs; otherwise the list's, an edit's, the history's, as now | The box | New (stop) |
| ↑ in an empty box | Edit the last waiting word, else the last sent words (§6) | The box | New |
| Ctrl+N | A new conversation of the surface the focus is in: Ask Daoris's inside its panel, the repository's start elsewhere | Everywhere | Changes meaning inside Ask Daoris |
| Ctrl+F | Find in the open conversation, whatever its length | A conversation | New (`ConversationFind` has no key and shows only for a long run) |
| Ctrl+L | Go to the message box of the conversation in view | Everywhere | New |
| F1 · Ctrl+Alt+I · Ctrl+Shift+Alt+L | Open Ask Daoris · Quick Ask | Everywhere | Exists |
| ↑ ↓ Home End | Along the history's rows; Escape clears the search, then closes the history | The history | Exists |

A key that collides in `menus/commands.ts` is chosen again at row **e**, which holds the table to unique keys. Ctrl+L and Escape-stop are
the two least certain; both are measured in the installed window (§10). A key that ends something is never offered (area rule 29): the
Escape stop ends a turn and keeps the conversation, which the *Stop turn* press already does with no confirm.

### 4.6 Attachments

- **In the composer:** an image's chip shows its thumbnail, made by the page from the file it holds (no fetch, no path).
- **Drop:** the whole conversation region takes a drop and lights up with *Drop to attach*, not only the form; a drop outside any
  conversation still opens nothing (`compose/carry.tsx`).
- **In the transcript:** names only, as today, because the page never learns where a file was kept. Thumbnails in the transcript need a
  desktop-only door that reads an attachment by its record and id, size-capped, with no machine path; row **g** decides it at its start
  and ships the composer's half whatever it decides.

### 4.7 The lists

- **One grouping function.** `historyGroups` (pinned, today, yesterday, this week, older) is the only date grouping; the session rail's
  list gains *By date* beside its three arrangements and reads the same function.
- **Rename and pin for every conversation, by one pair of presses.** Today they are Ask Daoris's alone (`HELP_RENAME`, `HELP_PIN`,
  `HelpConversations`). A session that is a chat gets the same fields on its record; the two routes become one pair for every
  conversation and the Ask Daoris pair goes in the same chunk (D228). A name is the person's, 80 characters at most; no model names one.
- **The list stays two places, not a third.** Ask Daoris's history keeps replacing the panel (a dock at N cannot hold both), and the
  session rail is the list pane (area rule 25). A persistent list beside Ask Daoris would be a new place (area rule 34).
- **Search** keeps its two ranges: names at once, content on a pause. Ask Daoris's is one box over every conversation with the
  words marked; both say what they searched.

### 4.8 A new conversation

One press from wherever a conversation is open: Ask Daoris's head *+* (it finishes the running one, as the one-at-a-time rule needs,
and the press says so), and a session's conversation head gains *New conversation*, which opens the start place in the same
repository with the box focused. The palette keeps *Ask Daoris: New conversation* and Ctrl+N does it from inside either (§4.5).

## 5. *Send now* (ASKPUSH1) in the composer

**What exists.** `ChatWords` holds a word at a level (`ordinary`, `urgent`, `now`, default urgent). A word at `now` is steered
where the agent steers, which today is Claude Code through its ACP adapter (`_session/steering`, measured), and elsewhere it is held
for the turn's end (`TurnTakes.TakeNowAsync`, `SendNowWhy`: `no-interrupt`, `behind`, `opening`). On a door that takes words only at
a turn's end, a separate press, the keeping stop (`CANCEL_TURN` with `keep`, `StopKeepingAsync`), stops the turn keeping the
conversation so what waits goes next. Ask Daoris's composer is handed no levels (`AskConversation.tsx:267`).

**What the owner asked.** *Send now* in the composer, delivering into the running turn where the harness takes input mid-turn and
otherwise stopping the turn and starting the next with the word, the queued word staying the default.

**The design.**

1. **Enter and the chevron's first two items stay as they are.** The default is whatever the box says today (the level the person
   chose, never remembered between words). *Send now* is a deliberate second press.
2. **One press, two places:** *Send now* in the *Send* chevron and in each waiting word's menu, and **Ctrl+Enter** in the box. It is
   offered on every door, in sessions and in Ask Daoris, while a turn runs and the box holds words (never with an empty box).
3. **What it does is the door's, said in its tip before it is pressed:**
   - **Where the agent steers** (Claude Code over ACP today): the word goes into the running turn at the SDK's priority `now`. The
     turn keeps going and keeps what it has done; the step in flight is cut, as measured (`steer-evidence` §6, turn B).
   - **Where it takes words at its next step only:** it goes there, and says so (`work.steer.notNowNextStep`).
   - **Where it takes nothing mid-turn** (`codex-acp`, the native door, a text door's turn): it stops the turn keeping the
     conversation and the word goes first in the next turn, in the order sent, the other waiting words behind it. This is the keeping stop with
     the box's word, one press that asks once, as the keeping stop does; the tip names it: *This stops the step it is on and its
     turn. What it has done stays, and this goes next.*
4. **The keeping stop's own press goes.** *Stop its turn so this goes next…* beside waiting words and its strings are removed in the
   same chunk; *Send now* on a waiting word is the one way to do it (D228). `StopKeepingAsync` and its census stay the driver's one
   deciding place for a stop.
5. **It never stops a turn that has words on their way.** The read evidence (§3 of `steer-evidence`) says a stop with words
   already handed to Claude Code loses their answer: the agent may act on words whose reply never reaches the page. So *Send now*
   on a door that must stop is refused with *Other words are already on their way to it* (`behind`) while the person's earlier words
   are on the wire, and is offered again once they are taken. The refusal costs a wait, never a lost word. This is read, not measured; the
   row's proof measures it once (§8, **c**).
6. **Nobody else may send now.** Daoris and agents keep *ordinary* and *urgent*; a word from either never stops a turn (D188 point 2
   stands). *Send now*'s stop is the person's own press, which D188's point 1 already allows (*the person's Stop, Stop turn or the keeping
   stop*); what changes is that it travels with a word and is named for what it does (D279 point 2).
7. **The record says what happened:** the word's receipt reads *sent now, steered* or *sent now, its turn stopped*, from the door's
   observation (area, co-working rule 6), never from the press.
8. **Ask Daoris's queue gains levels** through the same `reach` the sessions read (`SESSION_QUEUE`'s `reach`: `steers`,
   `keepingStop`, `nextStep`), so one composer reads one door description; there is no Ask Daoris copy of the menu.

**Two halves, in two rows.** **c1** is every door by its fallback: no harness has to be measured, because the keeping stop exists and
its refusal is the driver's own. **c2** is steering on Claude Code's native door and on Codex, after STEER3 measures each (D188 point
3: a level uses a door's steer only once measured); a door that gains steering replaces the stop in its tip, and nothing in the
composer changes.

## 6. Edit and resend, and what a record cannot do

Apps edit a sent message and branch the conversation from it. Daoris cannot do that to the agent's conversation: it never reads or
writes it (area rule 6), and a record, once written, is the line's account (rule 5). So:

- **The last turn's words:** *Edit* (or ↑ in an empty box) puts them in the box. Sending them is a new turn; the first stays above
  and says what it was. If the turn still runs, the box offers *Send now* as usual.
- **An earlier turn's words:** *Edit* opens a new conversation from that turn: the words edited, handed Daoris's brief of the line
  up to it (`ContextBrief` read to that turn: row **j**). Ask Daoris's *Start a new conversation from this one* (`HELP_START_FROM`)
  and a repository's *Start a conversation with these words* are the same press, with the turn as the cut.
- **Whether a harness can rewind its own conversation** (Claude Code's resume at a message, Codex's rollback) is not read and not
  measured. If one can and the account is the same, a later row may offer *Edit here* on it; the design does not wait for that.

## 7. What goes

| Goes | Because |
|---|---|
| The *Stop its turn so this goes next…* press and `work.steer.stopKeeping*` | *Send now* on a waiting word does it (§5) |
| *Stop turn* as a button beside *Send*; Ask Daoris's *Finish* and *Stop* in the box | The primary press is *Stop*; the endings are in the head's menu (§4.2) |
| The row of chips above Ask Daoris's box | They are in the bar (§4.2) |
| Full-width transcript and box in a wide region | The column (§4.1) |
| `HELP_RENAME` and `HELP_PIN` | One pair for every conversation (§4.7) |
| A second wording of *Say it again* | One press with two wordings (§4.4) |

Nothing else is removed: Finish, session stop, the levels, the cards and the receipts stay.

## 8. Rows, in build order

Every visible row proves itself with page tests in en and zh, stories at N, D, W and C in both themes with the keyboard throughout,
`i18n:check` and `names:check -- --strict`, and the installed window where it changes it (through `tools/desktop.mjs`, "installed").
Each writes its own words against the glossary first.

| Row (lanes; after) | What a person sees working | Proof |
|---|---|---|
| **CHATUX1a** the column (web-shell) | A conversation's words and box centred in one measure in a wide region; unchanged in the dock; the same in Quick Ask | one value on the conversation region (`tokens.test`); `ConversationView` and `Composer` tests: measure applied above it, none below; stories at N, D, W, C; area rule 27 rewritten for the conversation; shots before and after at W and C; the measured spec for how far down a conversation starts (`measured/head.spec.ts`) still holds |
| **b** the composer bar (web-shell; a) | One framed box: files above, one bar below; one primary press, *Send* or *Stop*; endings in the head's menu; Ask Daoris has model, effort and ring; its chips in the bar | `Composer.test.tsx`: one test per state (idle, turn running with an empty box, with words, parked answer, ended, editing a waiting word, a text door); `AskDaoris.test.tsx` for the options and ring; `SessionOptions` unchanged; Stop with no confirm as today; the chips at N collapse; stories for each state; installed shots at N, D, W |
| **c1** *Send now* on every door (driver, modules, web-shell; b) | *Send now* in the chevron, on a waiting word and as Ctrl+Enter, in sessions and Ask Daoris; where a door cannot take words mid-turn it stops the turn and the word goes first; the keeping stop's press gone | driver: `ChatTurnsTests` (now on a steering door, on a next-step door, on a turn-end door: stop keeping, the word first and in order, the others behind; `behind` refused with the earlier words on the wire, offered again once taken; `opening`), the census that only the keeping stop decides a stop, `TurnDoors`' reach for Ask Daoris; modules: `DriverModuleConversationTests`; web: the menus in en and zh, the strings of the removed press absent; a real-process proof on each of two doors that cannot take words mid-turn, in the spend sitting, plus the one turn that measures *stop with words on their way* (`steer-evidence` §7) |
| **d** handles on a message (web-shell; b) | Copy on every message, Edit on the person's last words, Retry on the last turn; ↑ recalls; Escape stops | clipboard tests (the agent's source, not the rendered text; a receipt never included); `ConversationView` tests for hover and focus-within, keyboard reachable; *Retry* and *Say it again* one path; stories; installed |
| **e** keys (web-shell; d) | The §4.5 table, listed in the shortcuts drawer with the box's own keys | `composerKeys` read by the handler and the drawer (one test they agree); `commands.ts` census: unique keys, none that ends something; `isPress` cases per key; Ctrl+N's two meanings by focus; installed: each key once, in en and zh, and the two least certain measured |
| **f** a run says what it did (web-shell) | The fold line *Read 3 files · Edited 2 · 12 s*; *Thought for 12 s* | `conversation.test.ts`: counts by wire kind, time from the events' own times, a missing time says none, an unknown kind; `ConversationView` tests; zh words in `names:check`'s budgets; stories of a long run closed, open and failed |
| **g** attachments in view (web-shell; b) | An image's thumbnail in its chip; the region takes the drop with a *Drop to attach* | `carry` tests (drop on the region, not outside any conversation; a non-image has none); object URLs let go on send and on unmount; stories; if a transcript door is decided, `ChatAttachmentReadTests` for the id, the cap and no path on the wire |
| **i** a new conversation in one press (web-shell; e) | *New conversation* in a session conversation's head, in the repository; Ctrl+N in Ask Daoris; *+* says it finishes the running one | head tests; the palette row; the count of presses from a shut panel and from a session, recorded in the installed pass |
| **h** the lists (web-shell, modules, driver; a) | *By date* in the session rail; rename and pin for every conversation; one grouping function | `history.test.ts` and `railSearch.test.ts` over one fixture of days; one route pair for rename and pin, `HELP_RENAME` and `HELP_PIN` gone in the same chunk (route census); driver tests for the name's bound and an ended record; installed |
| **j** edit and resend from an earlier turn (driver, modules, web-shell; d) | *Edit* on an earlier turn's words opens a new conversation from it | `ContextBriefTests`: the brief cut at a turn names what it leaves out; driver tests for the start from a turn on Ask Daoris and a repository's chat; web tests; the *Edit* press's tip says the first conversation is untouched; installed |
| **c2** steering the rest (driver; c1, STEER3) | On Claude Code's native door and on Codex, *Send now* goes into the running turn | STEER3's measured turn on each, then a real-process turn steered on each harness and the fallback where none is taken (ASKPUSH1's proof); MSG1h and MSG1i |
| **k** the installed window (the steward; all) | The whole of it, in both languages and both themes | dated shots at N, D, W and C of a streaming turn, a long run folded and open, *Send now* on each door, a drop with a thumbnail, the history by date and by home, the first-run empty state; the numbers of §10; a conversation reopened where it was left |

## 9. Decision D279, reserved

A decision is written only for a choice that rejected an alternative. Three do, and are carried into D279 when this design is
accepted. D279 amends D188 (points 1 and 3, and its rejected *a level that stops a turn*) and the area's rule on a page's measure (desktop app, rule 27).

1. **The measure belongs to the conversation, not the page.** *Rejected:* a cap on `ViewMain`, which would cap the review, the
   diffs, the maps and every page that lays out by its own width. The conversation's own region keeps one `em` value, once.
2. **Send now is the person's own press everywhere, and where a door cannot take words mid-turn it stops the turn keeping the
   session and sends the word first.** The owner's words ask for it. *Rejected:* a level that stops a turn on anyone's say (D188 point
   1 stands for Daoris and agents: nobody else may send now); holding the word as ordinary on a door that cannot steer, which was D188
   point 3's interim and leaves the owner's press doing nothing; and a *Stop* that quietly takes the word along, since a stop must say so.
3. **A sent word is never rewritten.** *Rejected:* branching or truncating a record or the agent's conversation in place, as the apps
   do. Daoris says it again, or starts from there handed its brief (§6).

Also rejected, without a decision of their own: a persistent third list beside Ask Daoris (a new place, area rule 34); a model that
names or summarises conversations (D1); a thumbs or feedback store (no consumer); a caret drawn at the streaming text (the
dot already says it runs); a *Send now* that waits behind words on their way instead of refusing (a hidden wait is a lost press).

## 10. Open, and what to measure first

**Open:**
- **The measure's value.** 52 em is a start; the shots at W and C decide it.
- **Escape as the turn's stop, and Ctrl+L.** Both can collide with an input method or a habit; the installed pass tests each in en and zh.
- **Transcript thumbnails.** They need a desktop-only door that reads an attachment (§4.6).
- **A harness's own rewind.** Not read; may later offer *Edit here* on one door (§6).
- **Export and sharing of a conversation.** Not part of this design.
- **Ask Daoris as a page of its own.** It lives in a dock, Quick Ask or a tab. Whether the person wants it in the centre is for the owner; the column makes
  it ready, and no new place is added here.

**Measure first:**
1. **At W and C, the characters per line** at 52 em, the share of a conversation's lines that wrap, and the line a reader's eye travels, in en and zh.
2. **Drawing a long message each 120 ms batch:** the frame time for a 20 KB answer growing, against the budget in `measured/`.
3. **A stop with words on their way,** once, on Claude Code's adapter (`steer-evidence` §7), before c1 offers the stop at all.
4. **Presses to a new conversation,** from a shut panel and from a session's conversation, before and after.
5. **Whether a conversation reopened returns to where it was left,** which was not checked.
