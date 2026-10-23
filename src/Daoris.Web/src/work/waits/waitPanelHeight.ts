import { useState } from 'react';
import { store, stored } from '../../lib/stored';

/**
 * The waiting panel's height (WAITPANEL1): what the person dragged its waits to, the most they take before they scroll within.
 *
 * - `initial`: where it starts for a viewer who never dragged it, lower than the 40% of the window each part took before, which
 *   the owner found too tall (2026-10-23).
 * - `min`: the floor a drag stops at, a line of the strip's head and a line of what waits under it.
 * - `share`: the most of the window the waits take, whatever was kept, so the conversation above stays readable in any window.
 * - `step`: an arrow's move, as the output panel's handle moves.
 */
export const WAIT_PANEL = { initial: 240, min: 72, share: 0.5, step: 48 } as const;

/** Per viewer, like the output panel's height (D42): a preference, never machine wiring and never a tracked file. */
export const WAIT_PANEL_KEY = 'daoris.waitPanelHeight';

/** The most the waits may be dragged to in a window this tall: its share, never below the floor. */
export function waitPanelMax(windowHeight: number): number {
  return Math.max(WAIT_PANEL.min, Math.round(windowHeight * WAIT_PANEL.share));
}

/** A kept height read back, or the initial one where none was kept, it was refused, or it is not a height. */
export function keptWaitPanel(): number {
  const held = Number(stored(WAIT_PANEL_KEY));
  return Number.isFinite(held) && held >= WAIT_PANEL.min ? held : WAIT_PANEL.initial;
}

/**
 * The height and its two changes: a drag or an arrow keeps it, a double-click forgets it. Kept through `stored`, which a missing
 * or refused store answers with nothing, so the panel is drawn at its initial height rather than not at all.
 */
export function useWaitPanelHeight() {
  const [height, setHeight] = useState(keptWaitPanel);
  return {
    height,
    resize: (next: number) => {
      setHeight(next);
      store(WAIT_PANEL_KEY, String(Math.round(next)));
    },
    reset: () => {
      setHeight(WAIT_PANEL.initial);
      store(WAIT_PANEL_KEY, null);
    },
  };
}
