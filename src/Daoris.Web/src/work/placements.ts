import { useState } from 'react';
import { store, stored } from '../lib/stored';

/**
 * A view that can move (DOCK1b, `docs/2026-09-29-dock-design.md` §3): what the right side bar and the
 * bottom panel hold, as VS Code's views move between its secondary side bar and its panel. The terminal
 * (CONSOLE4b, D96) is the person's own shell, beside the console as VS Code's terminal is beside its output.
 */
export type ViewId = 'timeline' | 'review' | 'workflow' | 'ask' | 'console' | 'terminal';

/**
 * Every view, in the frame's order: a region's tabs read in it, whichever were moved there.
 *
 * @remarks
 * A new view is named here and in `DEFAULT_PLACES`; its tab's name and icon (`ViewsMenu`) and the name Ask
 * Daoris is told (`help/where.ts`) are records over `ViewId`, so the compiler asks for them; its surface and
 * where it is present are the Work frame's. The one place nothing checks is Ask Daoris's room (`Help.cs`,
 * *The window*), which lists the views in prose: its test holds a phrase for each (CONSOLE4b found it).
 */
// The workflow (WORKFLOW1c, the workflow design §7): where the attended session's work stands, beside its timeline and review.
export const VIEW_IDS: readonly ViewId[] = ['timeline', 'review', 'workflow', 'ask', 'console', 'terminal'];

/** A region a view can go: the right side bar, or the panel under the session. */
export type Place = 'right' | 'panel';

/** Where each view stands until the viewer moves it: where the frame always drew it. */
export const DEFAULT_PLACES: Readonly<Record<ViewId, Place>> = {
  timeline: 'right', review: 'right', workflow: 'right', ask: 'right', console: 'panel', terminal: 'panel',
};

const ORDER = VIEW_IDS;

export const VIEW_PLACES = 'daoris.viewPlaces';

/**
 * Where the views stand, from what the viewer kept. What cannot be read is ignored rather than
 * refused (a layout is a convenience, D42), and a view or a place this build does not know is dropped,
 * so a later build's choice never breaks an earlier one.
 */
export function placesOf(raw: string | null): Record<ViewId, Place> {
  const places = { ...DEFAULT_PLACES };
  if (!raw) return places;
  let kept: unknown;
  try {
    kept = JSON.parse(raw);
  } catch {
    return places;
  }
  if (!kept || typeof kept !== 'object' || Array.isArray(kept)) return places;
  for (const [view, place] of Object.entries(kept)) {
    if ((ORDER as readonly string[]).includes(view) && (place === 'right' || place === 'panel')) {
      places[view as ViewId] = place;
    }
  }
  return places;
}

/**
 * The views a region holds: its own first, then what was moved into it, each in the frame's order —
 * so a view moved in arrives at the end, as a tab dragged into VS Code's panel does, and the region's
 * own keep their places. `here` leaves out a view this frame does not have (Ask Daoris with no shell).
 */
export function viewsIn(
  places: Readonly<Record<ViewId, Place>>,
  place: Place,
  here: (view: ViewId) => boolean = () => true,
): ViewId[] {
  const standing = ORDER.filter((view) => places[view] === place && here(view));
  return [
    ...standing.filter((view) => DEFAULT_PLACES[view] === place),
    ...standing.filter((view) => DEFAULT_PLACES[view] !== place),
  ];
}

/**
 * Where the views stand, held by the application like the closings (DOCK1c), so the View menu's
 * *Reset view locations* reaches them from every view.
 */
export type Placements = {
  places: Readonly<Record<ViewId, Place>>;
  /** Whether anything stands somewhere other than its default: what *Reset* would change. */
  moved: boolean;
  move: (view: ViewId, place: Place) => void;
  reset: () => void;
};

export function usePlacements(): Placements {
  const [places, setPlaces] = useState(() => placesOf(stored(VIEW_PLACES)));
  // Only what differs from the default is kept, so a default improved later reaches a viewer who never
  // chose, and a reset leaves no key behind.
  const keep = (next: Record<ViewId, Place>) => {
    setPlaces(next);
    const differs = Object.fromEntries(ORDER.filter((view) => next[view] !== DEFAULT_PLACES[view]).map((view) => [view, next[view]]));
    store(VIEW_PLACES, Object.keys(differs).length > 0 ? JSON.stringify(differs) : null);
  };
  return {
    places,
    moved: ORDER.some((view) => places[view] !== DEFAULT_PLACES[view]),
    move: (view, place) => keep({ ...places, [view]: place }),
    reset: () => keep({ ...DEFAULT_PLACES }),
  };
}
