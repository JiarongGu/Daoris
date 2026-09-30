# Panels that dock — the layout as the person's (DOCK1)

**Carried by:** DOCK1 in `TASKS.md` (SURF11 is archived). **Status:** design, written before the
build; all five built, DOCK1a last and widened by the owner; §4 says how. The
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
| Right dock (secondary side bar) | Sessions: Timeline, Review, Ask Daoris; elsewhere: Ask Daoris alone | per-session surfaces, and Ask Daoris; since PREVIEW1 (D111), a file's preview as a tab after them, which is not a view and never moves | resize, close, full |

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
then the moves, by menu and then by drag, then Quick Ask; the hoist, which shows nothing new, last. The hoist is structural and
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
   *Amended by TABS1 (2026-10-01):* the rest gave way by shrinking, each cut with an ellipsis, until
   时间线 read as 时…. A tab is now its whole name or its icon, measured (`work/tabFit.ts`), by the rule
   in `2026-09-19-platform-ux.md` §4; the shown preview alone may still be cut at its cap, and the
   panel's views follow the same rule.
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
   - **Only on Sessions**, the one view with a panel, when it was built. Away from it, Ask Daoris was
     the right region wherever it stood on Sessions — until DOCK1a put the frame on every view.
   - **What the look found.** The regions' names collided: the right dock's controls said *the panel*
     and the panel's said *the console*, so a menu offering *Move to the panel* named two places. The
     right is **the side bar** and the bottom **the panel** in every label now, as VS Code names
     them. And two sentences said where the console was (*below*, *above*), which a move makes false;
     they no longer say.
3. **DOCK1e**: dragging a tab between regions. *Built 2026-09-29, after DOCK1b*, since it is the
   part of VS Code's docking a person sees most, on the model DOCK1b laid (`work/viewDrag.ts`):
   - **A drop is the menu's move**: the same `onMove`, so a drop and *Move to* never disagree, and the
     menu stays the door for keys.
   - **A tab carries where it came from** as a type of its own, because a page may read a drag's
     types while it is over a region but not its data: a region lights up only for a view from the
     other one, as VS Code's accent outline and wash, and ignores a file or a selection.
   - **Every tab drags**, and the panel's lone view by its name, as VS Code's lone view by its title;
     so do the closed side bar's icons.
   - **An emptied region is drawn while a view is dragged**, so there is somewhere to drop it: the
     hidden panel as its header, the closed side bar as its strip, marked by the side bar's own
     picture rather than left a blank column at the window's edge. The frame learns of the drag a
     beat after it starts, since Chromium drops a drag whose page changes in the same task.
   - Not built: reordering tabs within a region, and splitting a region; neither was asked for.
4. **DOCK1d**: Quick Ask. *Built 2026-09-29*, VS Code's Quick Chat (`help/QuickAsk.tsx`):
   - **The same conversation in a box at the palette's place**: the box holds the organism the side
     bar holds, so what is asked there is read on here, and *Open in the side bar* carries on in the
     region with nothing lost.
   - **Three doors**: VS Code's key (`Ctrl+Shift+Alt+L`), the palette's *Quick Ask*, and the palette's
     last row, *Ask Daoris: "…"*, which asks whatever was typed, matched or not; its placeholder says
     so. A question handed in is sent once per id, as a typed one is (`useAskConversation`'s
     `opening`).
   - **What the look found.** The palette closing handed focus back to where it had been, out of a box
     opened in the same step, and the box's trap caught it on its frame: the box opens a beat later
     now, with the keys in its message box. The palette's *Ask Daoris* had only set the region open,
     which on Sessions did not bring the side bar up on it; it opens it on it now, as `F1` does.
   - **And what the answer showed**: asked what the panel held, the helper guessed that the View menu
     names each view's region, which it does not. Its room said nothing of the window; it does now,
     and the preface says where the views stand (HELP2, archived).
5. **DOCK1a**: the right region hoisted out of the Work frame into the application, one component on
   every view; Ask Daoris no longer has two hosts. *Widened by the owner, 2026-09-29:* "the design
   language we using in session screen (dockable, right tool bar, top layout setup) should be apply to
   all screens (for example overview)". So not the right region alone: Sessions' frame on every view,
   the side bar and the panel with their tab lists, moves and toggles, as VS Code's workbench is one
   frame whatever its editor shows. The question to settle first is what the regions hold away from
   Sessions. *Built 2026-09-29:*
   - **One frame, one element**: the application renders the Work frame on every view in a shell, with
     Sessions' own centre on Sessions and the view handed in everywhere else (`content`), so what is
     open, selected and sized survives a change of view. A browser keeps the view alone: the regions
     hold this machine's sessions (D47 §4).
   - **The regions hold what they hold on Sessions**: Ask Daoris, and the attended session's timeline,
     review and console, as VS Code's panel shows the terminal whatever the editor shows. No rail away
     from Sessions (`frameLayout`'s `noRail`): the session list is Sessions' own.
   - **Ask Daoris has one host**: its edge panel and its own width are gone; `F1`, `Ctrl+Alt+I`, the
     palette and Quick Ask's *Open in the side bar* open whichever region holds it.
   - **The strip's toggles**: the panel and the side bar on every view, the rail on Sessions; the View
     menu likewise, with *Reset view locations* on every view.
   - **What the look found**: off Sessions nothing named the session the timeline and the console
     spoke for, so each says *Attending …* with *open in Sessions*; the console's sentence said the
     conversation was "in the centre", which is Overview there, and says Sessions now; and a timeline
     off Sessions keeps a parked note, since no head carries it there.

## 5. Not chosen, for now

- **Floating panels inside the window.** VS Code has none; its answer is a separate window, which
  Daoris already has for a session and the monitor (SURF8).
- **Moving the session rail.** It is the navigator of the one view that has one, as VS Code's primary
  side bar is; it moves with the rest only if a person asks for it.
