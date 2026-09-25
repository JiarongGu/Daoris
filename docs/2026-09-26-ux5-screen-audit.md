# UX5 — screen by screen (2026-09-26)

> *"you should check screen by screen and all ui ux logic"* — the owner, choosing D76 (2026-09-25).

This is the working ledger of UX5 (`TASKS.md`), the last item of D76's round. Every surface, every
state (empty, loading, error, long, 中文, dark), and every piece of interaction logic (keys, focus,
what a click opens, what survives a reload), against the reference console and D41. **A finding is
written here the moment it is found**, with its verdict and then its disposition, so the audit
survives the session that runs it. When UX5 closes, `docs/task-archive.md` gets the outcome, the rules
it settled are stated in the body of `docs/2026-09-19-platform-ux.md`, and this file stays as the
record of what was found.

**Method.** One surface at a time: read its source against the contract, then look at it on the
scratch window (`npm run desktop -- run`, `shot`, `eval`) in each state the surface can reach, in both
languages and both themes. A finding lands as its own commit, TDD, and the surface's row below records
which states were looked at. **Dispositions.** *fixed `<sha>`*: landed. *row*: a backlog row, because
it is not session-sized or is the owner's call. *drop*: not worth the change, and why.

## Findings

Numbered in the order found. U1–U8 were found before UX5 opened (the backlog row carried them).

| # | Finding | Where | Disposition |
|---|---|---|---|
| U1 | "Waiting on you" wears three hues: declined's red on the liveness dot and the session pill, `--warn` on the map, open's amber on `WaitingCard` and the warn tile. Red is an outcome's hue, which POLISH2 already forbade a liveness mark | `ui.tsx` `DOT_TONE`, `SESSION_TONE`; `map/MapCanvas.tsx` | fixed: open's amber on all three; a failed tool call keeps red as its own `failed` tone. Looked at: the rail (both themes), the band, the map |
| U2 | The monitor's tiles are console-only, and say *Nothing said yet* for a session whose conversation is kept | `work/StreamTile.tsx`, `work/MonitorWindow.tsx` | open |
| U3 | The protocol door's console writes each streamed chunk of a message as its own line, so the raw view breaks words across lines, driven sessions included. `Acp.Render` and `Acp.Map` derive the line and the event side by side | `Daoris.Desktop` `Acp.cs` | open |
| U4 | An agent's one-item-per-line answer renders as one paragraph: Markdown makes a single newline a space | `work/Markdown.tsx` | fixed: every newline left in prose is a line break (a dozen-line remark transform, no new dependency); a blank line is still a paragraph and code keeps its lines. Looked at: a kept one-per-line chat |
| U5 | StartSession's three selects and checkbox, and DiffFileRow's checkbox, are native controls beside `ui.tsx`'s own | `work/StartSession.tsx`, `work/DiffFileRow.tsx` | open |
| U6 | `platform-ux.md` §4 has grown a dated amendment per pass; the rules they settled belong in the body they amend | `docs/2026-09-19-platform-ux.md` | open, done surface by surface |
| U7 | The dock is open by default at 45%, where the reference's opens on demand: on a 1400px window the conversation starts at 442px, near its floor | `work/layout.ts`, `work/WorkFrame.tsx` | fixed: the dock opens on demand (D76 as amended); an absent choice is closed, and opening is remembered as `0`. Looked at: a chat with the dock at its strip, then opened from it at 45% |
| U8 | The head's tree path breaks mid-word in a narrow centre (`family\g` / `ame`) | `ui.tsx` `MetaLine` (`break-all`) | fixed: `PathText` breaks after a separator (a `<wbr>`, so a copy is still the path) and inside a name only when one name is wider than the line; every path and URL site moved onto it, and the account names and raw output wells from `break-all` to `wrap-anywhere`. Looked at: the head of a chat at a 660px centre |
| U9 | An ended session opened from the rail shows an empty composer and a *send*, under *This session is over. What you typed is still here; nothing is listening to it.* Nothing was typed, and it is a box where nothing listens (INT4h's rule). The sentence is right only for a session that ended while the person was typing | `work/Composer.tsx`, `work/WorkFrame.tsx` | fixed: with nothing written, no box and no send, one sentence on the meter's line; with a kept draft, the box stays read-only (a disabled box cannot be selected, so its words could not be copied out) and the send goes. Looked at: both, light and dark |
| U10 | With the dock closed (U7) the centre is wide, and only the conversation held a measure: the composer ran a quarter wider under it, and the head's pill sat about a thousand pixels from its title | `work/AttendedSession.tsx`, `work/Composer.tsx` | fixed: the head and what the composer writes keep the conversation's `max-w-3xl`, one column. Looked at: a chat with the dock closed |
| U11 | The frame's tests passed in file order only: a test that hid the console left `daoris.panelClosed` set, and a leaked `daoris.dockClosed` hid three describes' reliance on an open dock. Four failed in a shuffled order before any UX5 change | `work/WorkFrame.test.tsx` | fixed: every test starts from a viewer with nothing remembered; five shuffled orders green |

## Surfaces

Each row is ticked by the states actually looked at on the window, never by reading alone.

| Surface | Looked at | Notes |
|---|---|---|
| App strip, command center, activity bar, status bar | — | |
| Overview (tiles, *What needs you*, outstanding, repositories) | — | |
| Quests (asks, quest list, compose, detail drawer, chain strip, ask composer and record) | — | |
| Projects (adopted cards, unadopted group, manage) | — | |
| Search, Convergence, the reader drawer | — | |
| Map, code map | — | |
| Settings, each domain | — | |
| Sessions: rail, strip, search, row menu | — | |
| Sessions: head, conversation, tool cards, composer, mentions, context ring | — | |
| Sessions: dock (Timeline, Review), output panel | — | |
| Start session | — | |
| Monitor window, detached session window | — | |
| Command palette, menus, toasts, tooltips | — | |
