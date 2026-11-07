import { type Grid, queryGrid } from './spatialGrid';
import { budgeted, CULL_PAST, mountedIds, type Tier, tierAt, viewRect } from './tiers';
import type { Size, View } from './viewMath';

// How much of a supplied world is mounted: the maps' cards by the same rules as a positioned
// world's nodes. Pure. Past 400 cards only those near the view and the pinned are mounted; at the dot tier only the pinned are, and the
// dot layer draws the rest. A feature draws its own lines, so it is told which cards a line must reach to be drawn.

export type SuppliedCull = {
  tier: Tier;
  /** The cards in the DOM: those to draw as the feature's own content. */
  mounted: ReadonlySet<string>;
  /** Past 400 cards, the cards a line needs an end at to be drawn; `null` where every line is drawn. */
  culled: ReadonlySet<string> | null;
  /** Whether the dot layer draws the cards that are not mounted. */
  dots: boolean;
};

/** Every card, at the full tier: a world not yet in a viewport (below the floor, or not measured). */
const whole = (grid: Grid): SuppliedCull => ({ tier: 'full', mounted: new Set(grid.boxes.keys()), culled: null, dots: false });

export function cullSupplied(grid: Grid, frame: { view: View; viewport: Size } | null, rem: number, pinned: Iterable<string | null>): SuppliedCull {
  if (frame === null) return whole(grid);
  const { view, viewport } = frame;
  const pins = [...pinned];
  const tier = budgeted(tierAt(view.k, rem), queryGrid(grid, viewRect(view, viewport)).size, 0);
  const large = grid.boxes.size > CULL_PAST;
  if (tier === 'dot') {
    const kept = new Set(pins.filter((id): id is string => id !== null && grid.boxes.has(id)));
    return { tier, mounted: kept, culled: large ? kept : null, dots: true };
  }
  const near = mountedIds(grid, viewRect(view, viewport, 0.5), pins);
  return { tier, mounted: near, culled: large ? near : null, dots: false };
}

/** Whether a line between two cards is drawn: every line, or past 400 cards one with an end mounted. */
export const lineDrawn = (culled: ReadonlySet<string> | null, from: string, to: string): boolean =>
  culled === null || culled.has(from) || culled.has(to);
