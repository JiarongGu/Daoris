import { type Grid, queryGrid } from './spatialGrid';
import type { Box, Size } from './viewMath';

// How much of a positioned world is drawn, and how (CANVAS1d; the canvas design §4): the tier follows the effective size of `text-meta`,
// the budget lowers it before the frame drops, and past 400 nodes only what is in view, and what must stay, is mounted.

export type Tier = 'full' | 'chip' | 'dot';

/** `text-meta` in px at the root's default size and a zoom of 1. */
const META = 11;
/** The effective size from which a node is its own component, and from which it is a chip. */
export const FULL_FROM = 9;
export const CHIP_FROM = 4;
/** The budget: full-tier nodes and drawn paths in view. */
export const FULL_BUDGET = 250;
export const PATH_BUDGET = 2000;
/** The chips a frame carries before the rest are dots. */
export const CHIP_BUDGET = 1500;
/** A positioned world culls past this many nodes. */
export const CULL_PAST = 400;
/** A grouped world shows its groups, not its nodes, past this many nodes. */
export const OVERVIEW_PAST = 1000;

/** The effective size of `text-meta` on screen: 11 px × the window's zoom (the root's size over 16) × the canvas's scale. */
export const effectiveMeta = (k: number, rem: number): number => META * (rem / 16) * k;

/** The tier the person's zoom asks for. */
export function tierAt(k: number, rem: number): Tier {
  const size = effectiveMeta(k, rem);
  return size >= FULL_FROM ? 'full' : size >= CHIP_FROM ? 'chip' : 'dot';
}

/** The tier that is drawn: the asked one, dropped while the view holds more than the budget carries. */
export function budgeted(asked: Tier, nodesInView: number, pathsInView: number): Tier {
  let tier = asked;
  if (tier === 'full' && (nodesInView > FULL_BUDGET || pathsInView > PATH_BUDGET)) tier = 'chip';
  if (tier === 'chip' && (nodesInView > CHIP_BUDGET || pathsInView > PATH_BUDGET)) tier = 'dot';
  return tier;
}

/** The world rect the viewport shows, in world units, grown by `margin` of the viewport on every side. */
export function viewRect(view: { x: number; y: number; k: number }, viewport: Size, margin = 0): Box {
  const [w, h] = [viewport.w / view.k, viewport.h / view.k];
  return { x: view.x - w * margin, y: view.y - h * margin, w: w * (1 + 2 * margin), h: h * (1 + 2 * margin) };
}

/**
 * The ids to mount: all of them to 400, else those the grown viewport meets plus the `pinned` (the focused, the selected, the find's
 * match), which stay mounted wherever they lie, so a reader's focus is never lost to the cull.
 */
export function mountedIds(grid: Grid, rect: Box, pinned: Iterable<string | null>): Set<string> {
  if (grid.boxes.size <= CULL_PAST) return new Set(grid.boxes.keys());
  const ids = queryGrid(grid, rect);
  for (const id of pinned) if (id !== null && grid.boxes.has(id)) ids.add(id);
  return ids;
}
