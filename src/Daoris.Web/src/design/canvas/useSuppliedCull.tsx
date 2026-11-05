import { type ReactNode, useCallback, useMemo, useState } from 'react';
import { DotLayer } from './DotLayer';
import { buildGrid } from './spatialGrid';
import { cullSupplied, type SuppliedCull } from './suppliedCull';
import { tierAt } from './tiers';
import type { Box, Size, View } from './viewMath';

/** The view the culling follows, snapped so that a pan of a few pixels remounts nothing. */
type Frame = { view: View; viewport: Size; key: string };

/**
 * The cull a supplied world is drawn by (CANVAS1e; the canvas design §4), followed as the view moves: what `cullSupplied` decides at the
 * snapped view, the canvas's `onView` that moves it, and the dot layer as the canvas's overlay while the tier is the dot's.
 */
export function useSuppliedCull(boxes: ReadonlyMap<string, Box>, rem: number, pinned: readonly (string | null)[]): {
  cull: SuppliedCull;
  onView: (view: View, viewport: Size) => void;
  overlay: ((view: View, viewport: Size) => ReactNode) | undefined;
} {
  const grid = useMemo(() => buildGrid(boxes), [boxes]);
  const [frame, setFrame] = useState<Frame | null>(null);
  const onView = useCallback((view: View, viewport: Size) => {
    const span = Math.max(viewport.w, viewport.h) / view.k / 4;
    const key = [Math.round(view.x / span), Math.round(view.y / span), tierAt(view.k, rem), Math.round(Math.log2(view.k) * 8), Math.round(viewport.w), Math.round(viewport.h)].join(':');
    setFrame((now) => (now?.key === key ? now : { view, viewport, key }));
  }, [rem]);
  // The pins are compared by value, joined, so a list drawn anew with the same ids keeps the cull.
  const pins = pinned.join('\u0000');
  const cull = useMemo(() => cullSupplied(grid, frame, rem, pins.split('\u0000').map((id) => id || null)), [grid, frame, rem, pins]);
  const overlay = cull.dots
    ? (view: View, viewport: Size) => <DotLayer grid={grid} view={view} viewport={viewport} skip={cull.mounted} />
    : undefined;
  return { cull, onView, overlay };
}
