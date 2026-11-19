import { stored } from '../../lib/stored';

// Per-viewer conveniences, like the language and the workspace scope: a remembered layout is
// a preference, never machine wiring and never a tracked file. A view's list keeps its own
// (`listPanes.ts`).
export const PANEL_HEIGHT = 'daoris.panelHeight';
// The dock's dragged width, as a SHARE of the window: a dock kept in pixels would stay the same while the window grows.
export const DOCK_SHARE = 'daoris.dockShare';

/** The dock's share of the window the person dragged it to, or null where they never did. */
export function rememberedShare(): number | null {
  const share = Number(stored(DOCK_SHARE));
  return Number.isFinite(share) && share > 0 && share < 1 ? share : null;
}

export function remembered(key: string, fallback: number): number {
  const held = Number(stored(key));
  return Number.isFinite(held) && held > 0 ? held : fallback;
}
