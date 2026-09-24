# The reference gap — what the working surface cannot do yet

**Date:** 2026-09-24 · **Status:** evidence, awaiting the owner's choice · **Prompted by:** the owner,
after POLISH4/5 — *"lets keep push the ui/ux design and I still think this does not meet the
reference projects capbility"*.

## 0. How this was measured

Two inventories, each read from source rather than remembered: every client package of the
reference console the owner chose to adopt (`docs/2026-09-21-working-surface-components.md` §3a,
read-only, about fifty `ui-*` packages), and every region of Daoris's Sessions frame, down to what the
driver sends over the bridge. Then the attended conversation was looked at on the window. The IDE
study's references (Cursor 3, Zed, VS Code, JetBrains) stay the frame for *why*; this document is
about *what each can do that Daoris cannot*.

## 1. The finding: the centre is a record, not a conversation

Open a conversation in Sessions and the centre holds its head, its moves and a composer. **The words
exchanged are not there.** They are in a collapsed "console" strip at the bottom, as one monospace
block, and after the application restarts that block says *"Nothing from this session is held here"*.

The chrome is not the cause. The pipeline is:

- **ACP's structure is flattened before the bridge.** `Acp.cs` renders each `session/update` into one
  string: a message chunk becomes its text, a thought becomes `· text`, a tool call becomes
  `→ title [status]`, usage becomes `context used/size`. `SESSION_OUTPUT` carries those lines, and
  the page joins them into a `<pre>`.
- **A conversation does not use the protocol at all.** `ChatRunner` pumps a harness's stdout and
  stderr line by line and writes the person's input to stdin. The person's own message is never
  echoed, because nothing records it.
- **Nothing is kept for the page.** The ring buffer holds 500 lines while the app runs. The session
  record has no messages, tokens or transcript field.

So a surface-only pass cannot close this. Every conversation capability the reference has (below,
§2a) is rendered **from structure**, and Daoris throws the structure away one layer below the page.

**This is not the screen-scraping the design rejected.** Working surface design §3 rejected *parsing
the stream into steps*, because the only source then was another program's stdout. That was true of
the pipe door and is still true of it. It is not true of the ACP door (D53), whose `session/update`
is the harness's documented wire. The IDE study already said so (*"the timeline is only buildable
because the protocol is"*). Claude Code on the native door also has a documented structured mode,
which is its wire, not its prose: `--print` with `--input-format stream-json`, `--output-format
stream-json` and `--include-partial-messages` (checked against `claude --help`, 2.1.281, 2026-09-24).
That mode takes turns on stdin as JSON, so it can carry a conversation too.

## 2. The matrix

**Has** · **partly** · **missing** · **not taken**, with the reason.

### 2a. The conversation — the gap that matters

| Capability | Reference | Daoris |
|---|---|---|
| Messages as blocks: the person's and the agent's, in order | yes | **missing**: one text block, the person's words absent |
| Markdown (GFM), links | yes, incremental while streaming | **missing** |
| Code blocks: highlighting, language label, copy | yes (Shiki, lazy languages) | **missing** |
| Thinking folded to one line, expandable | yes | **missing**: `· text` lines |
| Tool calls as collapsible cards, with status | yes, with specialised views: read, edit (diff), search, shell (ANSI, exit code), web, image, to-do | **missing**: `→ title [status]` lines |
| A turn's work folded ("N tool calls · N messages") | yes | **missing** |
| Per-message copy and timestamp | yes | **missing**: select text by hand |
| History after a restart, paged ("load earlier") | yes | **missing**: gone from the page |
| Follow the tail, "back to bottom" when scrolled up | yes | **partly**: follows only while live, no button |
| Jump to a turn (turn navigator) | yes | **missing** |
| Images in messages, lightbox | yes | **missing** |
| Per-turn usage: tokens, cache, time to first token | yes | **missing** in the session; per-account totals in Settings |
| Context meter under the composer | yes (ring, with breakdown) | **missing** |
| Plan / to-do panel above the composer | yes | **missing** |
| Subagents: breadcrumb, catalogue, open one | yes | **missing** |
| Fork a conversation at a turn | yes | **missing** |
| Trajectory (records table, inspector) | yes | **partly**: the timeline is lifecycle only, and cannot link into the conversation |

### 2b. The composer

| Capability | Reference | Daoris |
|---|---|---|
| Multiline, Enter / Shift+Enter | yes | has |
| Attachments: button, drop, paste, progress | yes | **missing** in a session; the quest and ask composers already have the `carry` molecule |
| `@` a file, folder or session | yes | **missing** |
| `/` commands | yes (`/plan`, `/compact`, `/export`, skills…) | **missing** |
| A draft kept per session | yes | **partly**: kept only while the session is attended |
| Send becomes Stop while running; queue or steer | yes | **partly**: *stop* ends the session rather than the turn; no queue |
| Model and effort picker | yes | **not taken**: D24, the harness owns the model |
| Permission preset picker | yes | **partly**: Daoris's rules live in Settings (D72), per scope, not per message |

### 2c. The frame and the session list

| Capability | Reference | Daoris |
|---|---|---|
| Rail resizable (264–420px) and collapsible to a 56px strip | yes | **missing**: fixed 15rem |
| Right dock resizable (45% default, 70% cap), tabs per session | yes, split into two panes, float, fullscreen | **partly**: fixed 26rem, two fixed tabs (Timeline, Review), hidden below 1024px |
| Conversation width, saved | yes | not needed until there is a conversation |
| Session search: names, then content | yes | **missing** |
| Row menu: rename, fork, archive | yes | **missing**; hand-naming was deferred by design §3 until two sessions cannot be told apart |
| View options: grouping, order | yes | **missing**: grouped by repository only |
| Hover card with the full title and status | yes | **missing** |

### 2d. The right dock's tools

| Capability | Reference | Daoris |
|---|---|---|
| Changed-files review per turn, split or unified, highlighted | yes | **partly**: one multibuffer per session with *viewed*, *accept* and *send back*; no highlighting, no split view |
| Files tree of the working folder | yes | **missing** |
| Document preview: code, Markdown, PDF, HTML, images | yes | **missing** |
| Browser tab | yes | **not taken** as a tab: the browser reaches sessions as a plugin's MCP server (INT1) |
| Terminal (xterm) | yes | **not taken**: design §6 holds a PTY until a harness proves unreadable over a pipe |

### 2e. What Daoris has and the reference does not

A command palette; OS notifications on park and on an unasked end; a status bar; a monitor window and
detached sessions; quests, asks and chains; review with *accept* and *send back as a quest*; the
driver and several machines; one surface for knowledge, convergence and the map. These are the
product's own, and nothing below touches them.

### 2f. Not taken, whatever the reference does

- **A model picker** (D24), **mid-run approval** (*Allow once*, D37/D52: a permission request is
  refused by construction), **editing files** (no editor, D55), **a plugin runtime for the surface**
  (D52/D64), and **like/dislike feedback**, which has no receiver when no model provider is named.

## 3. The decision this forces

| | What | Meets the reference? |
|---|---|---|
| **A. A conversation model, end to end** ← recommended | The driver keeps each session's structured updates as typed events: ACP's `session/update`, and Claude Code's `stream-json` on the native door. They go to a machine-local transcript, transcript-class under D47 §4. The bridge carries events, not lines, and the page renders a conversation. The verbatim console stays, as a *raw* view beside it. | Yes, and it is the only option that can: every row of §2a is rendered from this |
| B. Parse the text lines on the page | Recover structure from `→ title [status]` strings | No, and it is the screen-scraping design §3 rejected, one layer up |
| C. Polish the chrome, keep the console | Resizable columns, search, menus | No: §2a stays missing |

A amends design §3's *"no structured progress events"*: the protocol door made them exist. It needs a
decision (D76), and a stack addition under D42: a Markdown renderer and a highlighter. Both are
headless and neither brings a design language.

## 4. A build order, if A is chosen

Each is one landing, TDD, looked at on the window with a real session (the owner authorised real
sessions on this machine, 2026-09-24).

1. **CONV1 — the event record.** Typed events per session in a machine-local transcript, a history
   read over the bridge, and the page reading a session back after a restart. No new rendering yet.
2. **CONV2 — the conversation view.** Blocks for the person and the agent, Markdown, code with copy,
   thinking folded, tool calls as cards (generic first, then read, edit, shell and search), a turn's
   work folded, follow-the-tail with "back to bottom".
3. **CONV3 — conversations on the structured wire.** A chat rides ACP's turns on the protocol door
   and `stream-json` on the native one, and the person's message is part of the record.
4. **CONV4 — the composer.** Attachments (the `carry` molecule), `@` a file in the session's tree, a
   draft per session, stop-the-turn beside stop-the-session.
5. **CONV5 — meters.** The context ring and per-turn usage, from `usage_update` (TOOL3's source,
   absent never zero).
6. **FRAME6 — the frame.** A resizable, collapsible rail and a resizable dock with tabs per session,
   using the reference's geometry, which §3a already adopted and the build never reached.
7. **RAIL1 — the list.** Search by name and content, and a row menu.
8. **REVIEW2 — review.** Highlighting, and split or unified.

Held, each with its trigger: files and document preview (after CONV2, when a tool card wants to open
a file); a terminal (design §6's trigger); fork and subagents (when a harness on the ACP door reports
them).
