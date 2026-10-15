import { useEffect } from 'react';
import { useMutation } from '@tanstack/react-query';
import { ColorScheme, getBridge, useShenora } from '@shenora/react';
import { effectiveDark, subscribeTheme, themeChoice } from '../design/theme';
import { anotherMainWindow, secondaryWindow } from '../work/window';

// This build's secondary windows (MOD3): opening one, and telling its frame the theme; telling the engine every window
// renders on the viewer's choice (THEMESCHEME1); and telling the main window's taskbar button what waits (NOTIFY1b).

/**
 * Open one of this build's secondary windows (D55 §b, SURF8): the monitor, or one session detached.
 *
 * @remarks
 * **The one thing a page cannot do for itself**, like naming a folder — a window belongs to the
 * shell. In a browser this simply rejects, which is why every surface that offers it is gated on a
 * shell being here rather than on the call succeeding.
 *
 * **Pressing it twice is safe**: one window per name is the framework's contract, and the second
 * press brings the window forward. That is what a person means by it.
 *
 * Nothing is cached. The person can close one of these with its own close button and no page would
 * hear about it, so an "is the monitor open" answer would be stale more often than it was right.
 */
export const useOpenWindow = () => useMutation({
  mutationFn: (name: string) =>
    getBridge().invoke<{ opened: boolean; windows: string[] }>(
      'DAORIS.WINDOWS', 'OPEN', { payload: { name } }),
});

/**
 * A secondary window's page telling its own frame which theme it is in (WINDOW2), by the name the
 * window was opened under — on arrival, when the viewer's choice changes, and when the OS does. The
 * main window's frame is told by Shenora's `SET_THEME`; a second window's has no route there (0.17:
 * NO_ROUTE), so it goes through Daoris's own window module. Fire and forget: a window that cannot be
 * told keeps the OS's caption, which is what it had before. The engine is told the choice too (`useEngineTheme`).
 */
export function useSecondaryWindowTheme(name: string) {
  const { isAvailable } = useShenora();
  useEngineTheme();
  useEffect(() => {
    if (!isAvailable) return undefined;
    const tell = () => tellFrameTheme(name);
    tell();
    const media = typeof window.matchMedia === 'function' ? window.matchMedia('(prefers-color-scheme: dark)') : null;
    media?.addEventListener('change', tell);
    const stop = subscribeTheme(tell);
    return () => {
      media?.removeEventListener('change', tell);
      stop();
    };
  }, [isAvailable, name]);
}

/**
 * Tell the window named `name` the theme this page is in, for the caption its frame paints (WINDOW2): a utility window's, and
 * another main window's (ONEINSTANCE1), which the kit's `SET_THEME` does not reach. Fire and forget, as the theme is.
 */
export function tellFrameTheme(name: string) {
  try {
    void getBridge().invoke('DAORIS.WINDOWS', 'SET_THEME', { payload: { name, dark: effectiveDark() } }).catch(() => {});
  } catch {
    // No bridge at all: a browser has no frame to tell.
  }
}

/**
 * The main window's page telling its frame how much waits on the person (NOTIFY1b): Overview's *What needs you* count, which
 * the frame wears as its taskbar button's overlay and in its title, with `description` the words a screen reader says of
 * the overlay, in the page's language. On arrival and on each change of either.
 *
 * @remarks
 * **The main window's alone.** The taskbar button is the main window's, so a secondary window's page, which reads the same
 * counts, never tells it: the page is a secondary window when its own address names one (`work/window.ts`). Fire and
 * forget, as the theme is: a frame that cannot be told shows no count, which is what it showed before.
 */
export function useTaskbarCount(count: number, description: string) {
  const { isAvailable } = useShenora();
  useEffect(() => {
    const search = globalThis.location?.search ?? '';
    // The first main window's alone: another a later launch opened (ONEINSTANCE1) counts the same, and is no button of its own.
    if (!isAvailable || secondaryWindow(search) !== null || anotherMainWindow(search) !== null) return;
    void getBridge().invoke('DAORIS.WINDOWS', 'SET_BADGE', { payload: { count, description } }).catch(() => {});
  }, [isAvailable, count, description]);
}

/**
 * The viewer's choice told to the engine every window renders on (THEMESCHEME1), on arrival and each time it changes,
 * here or in another window: the kit's colour scheme (`SHENORA.COLOR_SCHEME`), which the engine applies to the one
 * Chromium the main window and each secondary window share. So Chromium's own UI and `prefers-color-scheme` follow an
 * explicit light or dark, and *System* still follows the OS.
 *
 * @remarks
 * **The choice, never the effective theme.** A scheme held at the OS's theme of the moment answers the very media
 * query this page reads the OS through, so a viewer on System would stop following the OS. Turning back to System, a
 * frame is told the held theme first and the OS's a round trip later, as the scheme lands and the media query changes.
 * Fire and forget: an engine that cannot be told keeps following the OS, as it did before.
 */
export function useEngineTheme() {
  const { isAvailable } = useShenora();
  useEffect(() => {
    if (!isAvailable) return undefined;
    let scheme: ColorScheme;
    try {
      scheme = new ColorScheme();
    } catch {
      // No bridge to make the client on: nothing to tell.
      return undefined;
    }
    const tell = () => {
      void scheme.set(themeChoice()).catch(() => {});
    };
    tell();
    const stop = subscribeTheme(tell);
    return () => {
      stop();
    };
  }, [isAvailable]);
}
