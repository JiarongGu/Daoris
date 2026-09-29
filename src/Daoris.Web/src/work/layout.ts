// The Work frame's geometry (FRAME6): the reference console's field-tested numbers (components plan
// §3a), decided in one pure function from the window's width and what the person chose. Pure, so every
// width a window can be is an ordinary assertion, and the frame only measures and renders.

/** The session rail: 264–420px, 280 to start, a 56px strip closed, and a strip by itself under 1024px. */
export const RAIL = { min: 264, max: 420, initial: 280, strip: 56, autoBelow: 1024 } as const;

/**
 * The right dock: 45% of the window to start, never more than 70%, never less than 300px, and the
 * whole frame under 768px. `strip` is what a closed dock leaves, to open it again by.
 */
export const DOCK = { share: 0.45, cap: 0.7, floor: 300, fullBelow: 768, strip: 32 } as const;

/** The attended session's own floor: the columns beside it give way before it does. */
export const CENTRE_FLOOR = 400;

/** What the person chose. A width is null until they drag one; the closings are theirs alone. */
export type FramePrefs = {
  rail: number | null;
  railClosed: boolean;
  /**
   * The dock's share of the window, once dragged (LAYOUT1). Never pixels: a width kept in pixels
   * stayed the same while the window grew, and read as a pane stuck at a small size. The rail keeps
   * pixels, because a list is as wide as its rows and its bounds say so.
   */
  dockShare: number | null;
  dockClosed: boolean;
  /** The dock over the whole frame, asked for — which a narrow window also does by itself. */
  dockFull: boolean;
  /**
   * A view with no rail at all (DOCK1a): the frame around Overview, Quests and the rest, whose centre
   * is a view rather than a session. Not a closed rail, which leaves a strip to open it by.
   */
  noRail?: boolean;
};

/**
 * - `docked`: beside the session.
 * - `cramped`: at its floor with the session already squeezed, so it asks to be closed.
 * - `full`: over the whole frame, the same tree drawn larger.
 * - `closed`: the person's, and nothing but the person opens it again.
 */
export type DockMode = 'docked' | 'cramped' | 'full' | 'closed';

export type FrameLayout = {
  /** `auto` says the strip is the window's doing rather than the person's, and widening undoes it. */
  rail: { width: number; strip: boolean; auto: boolean };
  dock: { mode: DockMode; width: number };
};

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

/**
 * What each column gets, in a window `viewport` wide whose Work frame is `frame` wide.
 *
 * @remarks
 * **Closing is deterministic**: a column the person closed stays closed at every width, and one the
 * window closed comes back when the window widens — the reference console's rule, because a layout
 * that springs back on a resize teaches people not to trust the control.
 *
 * **The session keeps its floor.** The dock gives way to hold the centre at 400px, down to its own
 * 300px floor, and past that it asks its occupant to close rather than squeezing the centre further.
 */
export function frameLayout(viewport: number, frame: number, prefs: FramePrefs): FrameLayout {
  const auto = viewport < RAIL.autoBelow;
  const strip = prefs.railClosed || auto;
  const rail = prefs.noRail
    ? { width: 0, strip: false, auto: false }
    : {
      width: strip ? RAIL.strip : clamp(prefs.rail ?? RAIL.initial, RAIL.min, RAIL.max),
      strip,
      auto: auto && !prefs.railClosed,
    };

  if (prefs.dockClosed) return { rail, dock: { mode: 'closed', width: DOCK.strip } };
  if (prefs.dockFull || viewport < DOCK.fullBelow) return { rail, dock: { mode: 'full', width: frame } };

  const wanted = Math.min(Math.round(viewport * (prefs.dockShare ?? DOCK.share)), Math.floor(viewport * DOCK.cap));
  const room = frame - rail.width - CENTRE_FLOOR;
  return room < DOCK.floor
    ? { rail, dock: { mode: 'cramped', width: DOCK.floor } }
    : { rail, dock: { mode: 'docked', width: Math.max(DOCK.floor, Math.min(wanted, room)) } };
}

/** How far the dock may be dragged now: from its floor to whatever keeps the session at its own. */
export function dockRange(viewport: number, frame: number, railWidth: number): { min: number; max: number } {
  const max = Math.min(Math.floor(viewport * DOCK.cap), frame - railWidth - CENTRE_FLOOR);
  return { min: DOCK.floor, max: Math.max(DOCK.floor, max) };
}
