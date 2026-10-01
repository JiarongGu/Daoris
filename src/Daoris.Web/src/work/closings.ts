import { useState } from 'react';
import { store, stored } from '../lib/stored';

/**
 * What the person closed in the frame (FRAME6): the output panel and the right dock, the frame's on every
 * view, and whether the view's list lies over the main area.
 *
 * @remarks
 * **Held by the application since DOCK1c**, so the strip's toggles and the View menu (SURF11) reach
 * them from every view, as VS Code's layout toggles sit in its title bar. The Work frame reads them
 * as props, and keeps its own where it is rendered alone.
 *
 * Per-viewer conveniences (D42), in the same keys the frame always used, so nothing a person closed
 * reopens on the upgrade. **A view's list is not here** since FRAME1c: each view remembers its own
 * closing (`listPanes.ts`, D118 §3f).
 */
export type FrameClosings = {
  /**
   * The view's list laid over the main area, from a strip the window drew (D118 §3a). Held here so every
   * door that toggles the list reaches it, and never remembered: it closes on a choice, and a change of
   * view lets it go.
   */
  listOver: boolean;
  panel: boolean;
  dock: boolean;
  setListOver: (over: boolean) => void;
  setPanel: (closed: boolean) => void;
  setDock: (closed: boolean) => void;
};

export const PANEL_CLOSED = 'daoris.panelClosed';
export const DOCK_CLOSED = 'daoris.dockClosed';

export function useFrameClosings(): FrameClosings {
  const [listOver, setListOver] = useState(false);
  const [panel, setPanelState] = useState(() => stored(PANEL_CLOSED) === '1');
  // 🔴 Closed until the person opens it (UX5 U7), as the reference's dock opens on demand: open by
  // default at 45%, it left a 1400px window's conversation 442px. So an absent choice is closed, and
  // opening is remembered as `0` beside closing's `1`.
  const [dock, setDockState] = useState(() => stored(DOCK_CLOSED) !== '0');

  return {
    listOver,
    panel,
    dock,
    setListOver,
    setPanel: (closed) => { setPanelState(closed); store(PANEL_CLOSED, closed ? '1' : '0'); },
    setDock: (closed) => { setDockState(closed); store(DOCK_CLOSED, closed ? '1' : '0'); },
  };
}
