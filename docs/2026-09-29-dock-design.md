# Panels that dock — the layout as the person's (DOCK1)

**Carried by:** DOCK1 in `TASKS.md` (SURF11 is archived). **Status:** design, written before the
build; DOCK1c and DOCK1b built, §4 says how. The
owner, 2026-09-29: *"we should be able to dock panels like vscode did"*, said beside *"there is no easy
way to open the daoris chat"* and followed by *"the ask daoris need to be a better location please
refer to a better ui/ux design"*. The first step, Ask Daoris as one right region, landed as `b21b60f`
(`docs/2026-09-29-ask-daoris-design.md` §2).

## 1. The reference

VS Code's workbench is the model the frame already follows (D56, `2026-09-21-desktop-frame-design.md`):
an activity bar, a **primary side bar** that swaps with the activity, the editor, a **panel** below it,
and a **secondary side bar** on the other side, which holds the Chat view by default. Every view lives
in a **view container**, and a view can be **moved**: dragged by its tab to another container, or sent
with *Move View* / *Move to Panel* / *Move to Secondary Side Bar*. Each region toggles (`Ctrl+B`,
`Ctrl+Alt+B`, `Ctrl+J`), resizes, and maximizes; the panel's position (bottom, left, right) is a
setting; *Reset View Locations* puts everything back. The layout toggles sit in the title bar beside the
window controls. Cursor and JetBrains follow the same shape: AI chat in the right region, movable.

## 2. What Daoris has

| Region | Where it exists | Holds | Can the person |
|---|---|---|---|
| Activity bar | every view | the views | — |
| Session rail (primary side bar) | Sessions only | the sessions | resize, close |
| Centre | every view | the view | — |
| Output panel | Sessions only | the console and its streams | resize, hide |
| Right dock (secondary side bar) | Sessions: Timeline, Review, Ask Daoris; elsewhere: Ask Daoris alone | per-session surfaces, and Ask Daoris | resize, close, full |

So the regions are VS Code's, but each is fixed to what it holds, the toggles live inside Sessions
(SURF11), and two of the three side regions exist on one view only.

## 3. The shape

**A view is a thing that can move; a region is a place it can go.** The views: Ask Daoris, the console,
the session timeline, the review. The places: the right region, the bottom panel, and later the left
side bar beside the rail. Where each view stands is the viewer's, remembered like every other layout
choice (the dock's share, the rail's width), and *Reset layout* puts the defaults back.

- **The right region is the application's, not Sessions'**: present on every view, holding the views
  that make sense there — Ask Daoris everywhere, and the attended session's Timeline and Review on
  Sessions. One right region is what `b21b60f` gave the eye; this gives it one component.
- **Move before drag.** A view's tab carries *Move to the right / to the panel* in a small menu, the way
  VS Code's does. Dragging a tab between regions comes after, on the same model.
- **Toggles on the strip** (SURF11): the rail, the panel and the right region, beside the window
  controls, and in the View menu with their keys.
- **Quick Ask**: a palette-like box from the command center for one question, which lands in Ask
  Daoris's conversation. VS Code's Quick Chat.

## 4. Build order

*Reordered 2026-09-29, before any of it was built, and again after DOCK1c:* the toggles come first,
then the moves. The hoist is structural and
shows nothing new (`b21b60f` already gives the eye one right region), while it splits the Work frame's
layout rules (cramped, full, the narrow window's takeover) across two components on the busiest view.
The toggles are what a person sees, use the strip's new trailing slot, and need only the three
closings lifted out of the Work frame.

1. **DOCK1c** (SURF11): the region toggles on the strip and in the View menu, with VS Code's keys
   (`Ctrl+B` the rail, `Ctrl+J` the panel, `Ctrl+Alt+B` the right region). *Built 2026-09-29:* the
   closings lifted into the application (`work/closings.ts`); VS Code's three pictures at the strip's
   right, pressed while shown, the right one alone away from Sessions and named for Ask Daoris there;
   the View menu's items ticked, with their keys. What the look found, and the owner said: Ask Daoris
   lost its own strip button, since its region's toggle, `F1` and `Ctrl+Alt+I` open it; and the dock's
   tabs shrink as a browser's do, the selected one whole and the rest cut to their icons, since a third
   tab clipped the dock's close at its floor. The list for tabs that do not fit came with DOCK1b.
2. **DOCK1b**: *Move to* for Ask Daoris and the console, between the right region and the panel;
   remembered; *Reset layout*. *Built 2026-09-29, ahead of DOCK1a* for the toggles' reason: it is
   what a person uses, and it needs no hoist. As built:
   - **The views are all four**, Ask Daoris, the console, the timeline and the review; the places,
     the right side bar and the panel (`work/placements.ts`). A region's own views read first, then
     what was moved in, as a tab dragged into VS Code's panel lands at the end.
   - **The move is on each region's tab list**, a button at the end of its tab row, and on a
     right-click of a tab, which selects it first so the move names it. The list is Chrome's: every
     tab the region holds by its whole name, which is the answer to the owner's *"display only icon
     does not fix the limitation"*, since a tab cut to its icon is no longer the only way to it.
   - **A moved view is shown where it went**, and a region it leaves with nothing closes, as VS Code
     hides an empty container; opened again, it says how to fill it. A door that does not know where
     a view stands (the palette's review, `F1`) opens whichever region holds it.
   - **Remembered per viewer as what differs from the default** (`daoris.viewPlaces`), so a default
     improved later reaches a viewer who never chose. **Reset view locations**, VS Code's name,
     because it resets where views stand and not the sizes or what is closed: in the tab list once
     something has moved, and in the View menu always.
   - **Only on Sessions**, the one view with a panel. Away from it, Ask Daoris is the right region
     wherever it stands on Sessions.
   - **What the look found.** The regions' names collided: the right dock's controls said *the panel*
     and the panel's said *the console*, so a menu offering *Move to the panel* named two places. The
     right is **the side bar** and the bottom **the panel** in every label now, as VS Code names
     them. And two sentences said where the console was (*below*, *above*), which a move makes false;
     they no longer say.
3. **DOCK1a**: the right region hoisted out of the Work frame into the application, one component on
   every view; Ask Daoris no longer has two hosts.
4. **DOCK1d**: Quick Ask.
5. **DOCK1e**: dragging a tab between regions.

## 5. Not chosen, for now

- **Floating panels inside the window.** VS Code has none; its answer is a separate window, which
  Daoris already has for a session and the monitor (SURF8).
- **Moving the session rail.** It is the navigator of the one view that has one, as VS Code's primary
  side bar is; it moves with the rest only if a person asks for it.
