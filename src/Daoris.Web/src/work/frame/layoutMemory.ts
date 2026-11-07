import { store, stored } from '../../lib/stored';
import { DOCK } from './layout';

// Per-viewer conveniences, like the language and the workspace scope: a remembered layout is
// a preference, never machine wiring and never a tracked file. A view's list keeps its own
// (`listPanes.ts`).
export const PANEL_HEIGHT = 'daoris.panelHeight';
// The dock's dragged width, as a SHARE of the window. `daoris.dockWidth` is the older pixel key: a
// dock kept in pixels stays the same while the window grows; one held there is read once, as its share
// of the window it is read in, and then forgotten.
export const DOCK_SHARE = 'daoris.dockShare';
const DOCK_WIDTH_PIXELS = 'daoris.dockWidth';

/** The dock's share of the window the person dragged it to, or null where they never did. */
export function rememberedShare(): number | null {
  const share = Number(stored(DOCK_SHARE));
  if (Number.isFinite(share) && share > 0 && share < 1) return share;

  const pixels = rememberedWidth(DOCK_WIDTH_PIXELS);
  if (pixels === null || window.innerWidth <= 0) return null;
  const converted = Math.min(pixels / window.innerWidth, DOCK.cap);
  store(DOCK_SHARE, String(converted));
  store(DOCK_WIDTH_PIXELS, null);
  return converted;
}

export function remembered(key: string, fallback: number): number {
  const held = Number(stored(key));
  return Number.isFinite(held) && held > 0 ? held : fallback;
}

/** A width the person dragged, or null where they never did — which is not the same as a default. */
function rememberedWidth(key: string): number | null {
  const held = Number(stored(key));
  return Number.isFinite(held) && held > 0 ? held : null;
}
