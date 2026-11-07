import { stored, store } from '../../lib/stored';
import type { Box, Size, View } from './viewMath';

// The minimap's rule and its geometry: offered where the world is past twice the viewport either way,
// at the world's own size (so a zoom does not make it come and go), from the 36rem floor the canvas already holds; switched in the sizing
// menu and remembered by the viewer.

export const MINIMAP_KEY = 'daoris.canvas.minimap';
/** The longer side of the minimap, in px. */
export const MINIMAP_SIDE = 160;

/** Whether the world is large enough to want a minimap: more than twice the viewport, at either side, at 100%. */
export const wantsMinimap = (world: Size, viewport: Size): boolean => world.w > 2 * viewport.w || world.h > 2 * viewport.h;

export type MinimapGeometry = {
  scale: number;
  width: number;
  height: number;
  /** The view's rectangle on the minimap, in px. */
  rect: Box;
};

/** The world drawn into a box of `MINIMAP_SIDE` on its longer side, and the viewport's rectangle on it. */
export function minimapGeometry(world: Size, view: View, viewport: Size): MinimapGeometry {
  const scale = MINIMAP_SIDE / Math.max(world.w, world.h, 1);
  return {
    scale, width: world.w * scale, height: world.h * scale,
    rect: { x: view.x * scale, y: view.y * scale, w: (viewport.w / view.k) * scale, h: (viewport.h / view.k) * scale },
  };
}

/** The view that centres the world point under a press on the minimap. */
export function viewAtMinimap(at: { x: number; y: number }, geometry: MinimapGeometry, view: View, viewport: Size): View {
  return { x: at.x / geometry.scale - viewport.w / (2 * view.k), y: at.y / geometry.scale - viewport.h / (2 * view.k), k: view.k };
}

export function minimapWanted(): boolean {
  return typeof window === 'undefined' || stored(MINIMAP_KEY) !== '0';
}

export function rememberMinimap(on: boolean): void {
  store(MINIMAP_KEY, on ? '1' : '0');
}
