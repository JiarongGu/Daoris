import type { Decorator } from '@storybook/react-vite';
import { type ReactNode, useLayoutEffect, useState } from 'react';
import { store } from '../../lib/stored';
import { memoryKey } from './viewMemory';

// A story's map held at a zoom of its own (MAPUX1a). Stories only: nothing in the product imports this.

/**
 * A viewer's place kept under the map at zoom `k`, anchored on `anchor`: written before the map opens, once per mount, and taken away
 * after. The kept graph's identity is not the story's, so the canvas restores the zoom by the anchor, as it does under a changed map.
 */
function Kept({ feature, storeKey, k, anchor, children }: { feature: string; storeKey: string; k: number; anchor: string; children: ReactNode }) {
  const at = memoryKey(feature, storeKey);
  useState(() => store(at, JSON.stringify({
    identity: 'the story\'s own', view: { x: 0, y: 0, k }, anchor: { id: anchor, group: null, screen: { x: 24, y: 24 } }, selected: null,
  })));
  useLayoutEffect(() => () => store(at, null), [at]);
  return <>{children}</>;
}

/** The map opened at zoom `k` (0.25, 0.5, 1 are the dot, chip and full tiers at the window's 100%). */
export const keptAtZoom = (feature: string, storeKey: string, k: number, anchor: string): Decorator => (Story) => (
  <Kept feature={feature} storeKey={storeKey} k={k} anchor={anchor}><Story /></Kept>
);
