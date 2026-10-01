import { useState } from 'react';
import { store, stored } from '../lib/stored';

/**
 * What the person closed in the Work frame (FRAME6): the view's list, the output panel, the right dock.
 *
 * @remarks
 * **Held by the application since DOCK1c**, so the strip's toggles and the View menu (SURF11) reach
 * them from every view, as VS Code's layout toggles sit in its title bar. The Work frame reads them
 * as props, and keeps its own where it is rendered alone.
 *
 * Per-viewer conveniences (D42), in the same keys the frame always used, so nothing a person closed
 * reopens on the upgrade.
 */
export type FrameClosings = {
  /**
   * Sessions' list, the one list pane until each view keeps its own (FRAME1c, D118 §3f). Its key is
   * still the rail's, `daoris.railClosed`, so nothing reopens on the upgrade.
   */
  list: boolean;
  /**
   * Sessions' list laid over the main area, from a strip the window drew (D118 §3a). Held beside its
   * closing so every door that toggles the list reaches it, and never remembered: it closes on a choice.
   */
  listOver: boolean;
  panel: boolean;
  dock: boolean;
  setList: (closed: boolean) => void;
  setListOver: (over: boolean) => void;
  setPanel: (closed: boolean) => void;
  setDock: (closed: boolean) => void;
};

export const RAIL_CLOSED = 'daoris.railClosed';
export const PANEL_CLOSED = 'daoris.panelClosed';
export const DOCK_CLOSED = 'daoris.dockClosed';

export function useFrameClosings(): FrameClosings {
  const [list, setListState] = useState(() => stored(RAIL_CLOSED) === '1');
  const [listOver, setListOver] = useState(false);
  const [panel, setPanelState] = useState(() => stored(PANEL_CLOSED) === '1');
  // 🔴 Closed until the person opens it (UX5 U7), as the reference's dock opens on demand: open by
  // default at 45%, it left a 1400px window's conversation 442px. So an absent choice is closed, and
  // opening is remembered as `0` beside closing's `1`.
  const [dock, setDockState] = useState(() => stored(DOCK_CLOSED) !== '0');

  return {
    list,
    listOver,
    panel,
    dock,
    setList: (closed) => { setListState(closed); store(RAIL_CLOSED, closed ? '1' : null); },
    setListOver,
    setPanel: (closed) => { setPanelState(closed); store(PANEL_CLOSED, closed ? '1' : '0'); },
    setDock: (closed) => { setDockState(closed); store(DOCK_CLOSED, closed ? '1' : '0'); },
  };
}
