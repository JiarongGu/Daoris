import { useEffect } from 'react';
import { zoomBy, zoomWheel } from '../design/zoom';
import { frameShortcut } from '../menus/shortcuts';

// The window's zoom, as the window hears it (ZOOM1, the UI/UX design §3.11): the wheel with Ctrl on every Daoris window, and
// the keys on a window without the menu bar's table. The main window's keys are the table's (View's zoom rows), read by its
// one key handler; the level itself is `design/zoom.ts`'s.

/**
 * Ctrl+wheel steps the window's zoom while `on` (a shell's window; a browser zooms its page itself). Heard on the window,
 * after every surface under the pointer, so one that zooms itself (the Map) takes the wheel first and keeps it, and its
 * default prevented, so the engine's own zoom, which scaled the frame with the content, never runs.
 */
export function useZoomWheel(on: boolean): void {
  useEffect(() => {
    if (!on) return undefined;
    const onWheel = zoomWheel((direction) => zoomBy(direction > 0 ? 'in' : 'out'));
    // Not passive: a passive listener cannot prevent the engine's zoom.
    window.addEventListener('wheel', onWheel, { passive: false });
    return () => window.removeEventListener('wheel', onWheel);
  }, [on]);
}

/** The zoom's rows of the table, by what each does. */
const ZOOM_ROWS: Readonly<Record<string, 'in' | 'out' | 'reset'>> = {
  'view.zoomIn': 'in',
  'view.zoomOut': 'out',
  'view.zoomReset': 'reset',
};

/**
 * The zoom's keys on a window with no menu bar of its own (a detached session, the monitor), read from the one table as the
 * terminal reads the frame's keys (`frameShortcut`), so they are the main window's keys and no second list.
 */
export function useZoomKeys(on: boolean): void {
  useEffect(() => {
    if (!on) return undefined;
    const onKey = (event: KeyboardEvent) => {
      if (event.defaultPrevented) return;
      const step = ZOOM_ROWS[frameShortcut(event) ?? ''];
      if (!step) return;
      event.preventDefault();
      zoomBy(step);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [on]);
}
