// The frame's geometry (FRAME6, then D118): the reference console's field-tested numbers (components plan
// §3a), decided in one pure function from the frame's width and what the person chose. Pure, so every
// width a window can be is an ordinary assertion, and the frame only measures and renders.

/** A list pane's bounds: the least and the most it is dragged to, and the width it starts at. */
export type ListBounds = { min: number; max: number; initial: number };

const LIST_WIDE: ListBounds = { min: 264, max: 420, initial: 280 };

/**
 * Each view's list pane (D118 §3a), one table: FRAME6's 264–420 px, 280 to start, for every list but
 * Settings', whose rows are single names (its 11 rem). A view that is absent here has no list: Overview
 * and Map (D118 §4).
 *
 * The monitor window's rail is a list of its own (D118 §4, FRAME1h): the session list, live, so its bounds
 * are Sessions', and what it remembers is the monitor's rather than the main window's.
 *
 * Knowledge's pane is one (UX6i, D150 §2.2): its bounds, its closing and its width are the place's. Search and Convergence
 * are its two modes, each still a list's memory, its chosen item and its filters under the keys it had as a place, and
 * their bounds are Knowledge's, since that is the pane they are drawn on.
 */
export const LIST_BOUNDS = {
  sessions: LIST_WIDE,
  quests: LIST_WIDE,
  projects: LIST_WIDE,
  knowledge: LIST_WIDE,
  search: LIST_WIDE,
  convergence: LIST_WIDE,
  agents: LIST_WIDE,
  plugins: LIST_WIDE,
  settings: { min: 176, max: 320, initial: 176 },
  monitor: LIST_WIDE,
} as const satisfies Record<string, ListBounds>;

export type ListView = keyof typeof LIST_BOUNDS;

/** A list closed to a strip: its controls and its marks, one across. */
export const LIST_STRIP = 56;

/**
 * The right dock: 45% of the window to start, never more than 70%, never less than 300px, and the
 * whole frame under 768px. `strip` is what a closed dock leaves, to open it again by.
 */
export const DOCK = { share: 0.45, cap: 0.7, floor: 300, fullBelow: 768, strip: 32 } as const;

/** The main area's own floor: the columns beside it give way before it does. */
export const CENTRE_FLOOR = 400;

/** What the person chose for the view's list. A width is null until they drag one; the closing is theirs alone. */
export type ListChoice = {
  bounds: ListBounds;
  width: number | null;
  closed: boolean;
  /**
   * The person opened a strip the window drew (D118 §3a), or a go brought a group of the list into view
   * (ENTRY1g), so the list lies over the main area. Never remembered: it closes on a choice, on Escape and
   * on a press outside it.
   */
  over: boolean;
};

/** What the person chose. The closings are theirs alone. */
export type FramePrefs = {
  /** The view's list, or null where the view has none (DOCK1a; D118 §4), which leaves no strip either. */
  list: ListChoice | null;
  /**
   * The dock's share of the window, once dragged (LAYOUT1). Never pixels: a width kept in pixels
   * stayed the same while the window grew, and read as a pane stuck at a small size. A list keeps
   * pixels, because a list is as wide as its rows and its bounds say so.
   */
  dockShare: number | null;
  dockClosed: boolean;
  /** The dock over the whole frame, asked for — which a narrow window also does by itself. */
  dockFull: boolean;
};

/**
 * - `open`: beside the main area, at its width.
 * - `strip`: its 56 px strip, closed by the person or drawn by the window.
 * - `over`: a strip the window drew, opened, or any strip a go opened (ENTRY1g): the list lies over the main area,
 *   beside the strip.
 */
export type ListMode = 'open' | 'strip' | 'over';

export type ListLayout = {
  mode: ListMode;
  /** How wide the list is drawn: its width open or laid over, the strip's as a strip. */
  width: number;
  /** What it takes beside the main area: its width open, the strip's otherwise. */
  beside: number;
  /** The strip is the window's doing rather than the person's, and room returning gives the list back. */
  auto: boolean;
};

/**
 * - `docked`: beside the session.
 * - `cramped`: at its floor with the session already squeezed, so it asks to be closed.
 * - `full`: over the whole frame, the same tree drawn larger.
 * - `closed`: the person's, and nothing but the person opens it again.
 */
export type DockMode = 'docked' | 'cramped' | 'full' | 'closed';

export type FrameLayout = {
  list: ListLayout | null;
  dock: { mode: DockMode; width: number };
};

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

/**
 * The view's list in a frame `frame` wide, beside a side bar that takes `sideBar` (D118 §3a).
 *
 * @remarks
 * **By room, never by a fixed width.** FRAME6 drew the rail as a strip below 1024 px, which kept it a
 * strip at 900 px with the side bar closed and 540 px free beside it, and would strip Settings' 176 px
 * list where it fits. So the list is a strip when, at its width, the main area would fall below its floor
 * beside the side bar as it stands. It gives way before the side bar does, because its strip keeps its
 * doors.
 *
 * A browser's frame asks it with no side bar at all (D118 §4): its view keeps its list and its main area,
 * and never the side bar.
 *
 * **A strip the person closed is laid over only for a go** (ENTRY1g, D161's ENTRY1b note): a go to a group of the list
 * lays it over the main area, room or none, since opening it beside would undo their closing. Their own open still
 * undoes it (`listToggled`), and laying over is never remembered, so the strip is theirs again once it goes.
 */
export function listLayout(frame: number, sideBar: number, choice: ListChoice): ListLayout {
  const strip = { width: LIST_STRIP, beside: LIST_STRIP };
  const width = clamp(choice.width ?? choice.bounds.initial, choice.bounds.min, choice.bounds.max);
  // Opened over the main area, it is never wider than what lies beside the strip.
  const over = (auto: boolean): ListLayout =>
    ({ mode: 'over', width: Math.max(0, Math.min(width, frame - LIST_STRIP)), beside: LIST_STRIP, auto });

  if (choice.closed) return choice.over ? over(false) : { mode: 'strip', ...strip, auto: false };
  if (frame - width - sideBar >= CENTRE_FLOOR) return { mode: 'open', width, beside: width, auto: false };
  return choice.over ? over(true) : { mode: 'strip', ...strip, auto: true };
}

/**
 * What each column gets, in a window `viewport` wide whose frame is `frame` wide.
 *
 * @remarks
 * **Closing is deterministic**: a column the person closed stays closed at every width, and one the
 * window closed comes back when the window widens — the reference console's rule, because a layout
 * that springs back on a resize teaches people not to trust the control.
 *
 * **The main area keeps its floor.** The list gives way first, to its strip, counting the side bar at
 * its 32 px strip closed and its 300 px floor open. The dock then gives way to hold the main area at
 * 400px, down to its own floor, and past that it asks its occupant to close rather than squeezing the
 * main area further.
 */
export function frameLayout(viewport: number, frame: number, prefs: FramePrefs): FrameLayout {
  const list = prefs.list ? listLayout(frame, prefs.dockClosed ? DOCK.strip : DOCK.floor, prefs.list) : null;
  const beside = list?.beside ?? 0;

  if (prefs.dockClosed) return { list, dock: { mode: 'closed', width: DOCK.strip } };
  if (prefs.dockFull || viewport < DOCK.fullBelow) return { list, dock: { mode: 'full', width: frame } };

  const wanted = Math.min(Math.round(viewport * (prefs.dockShare ?? DOCK.share)), Math.floor(viewport * DOCK.cap));
  const room = frame - beside - CENTRE_FLOOR;
  return room < DOCK.floor
    ? { list, dock: { mode: 'cramped', width: DOCK.floor } }
    : { list, dock: { mode: 'docked', width: Math.max(DOCK.floor, Math.min(wanted, room)) } };
}

/**
 * What a toggle of the list asks for (D118 §3a), from any of its four doors: an open list closes, the
 * person's; one laid over the main area is dismissed, its closing left as it was (`closed` absent), since a
 * go lays over a strip the person closed (ENTRY1g); a strip opens — beside where there is room, and over the
 * main area where the window drew it for want of room.
 */
export function listToggled(list: Pick<ListLayout, 'mode'>): { closed?: boolean; over: boolean } {
  if (list.mode === 'open') return { closed: true, over: false };
  if (list.mode === 'over') return { over: false };
  return { closed: false, over: true };
}

/** How far the dock may be dragged now: from its floor to whatever keeps the session at its own. */
export function dockRange(viewport: number, frame: number, beside: number): { min: number; max: number } {
  const max = Math.min(Math.floor(viewport * DOCK.cap), frame - beside - CENTRE_FLOOR);
  return { min: DOCK.floor, max: Math.max(DOCK.floor, max) };
}
