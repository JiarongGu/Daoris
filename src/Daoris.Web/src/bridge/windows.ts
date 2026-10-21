import { useEffect } from 'react';
import { useMutation } from '@tanstack/react-query';
import { ColorScheme, getBridge, useShenora } from '@shenora/react';
import { effectiveDark, subscribeTheme, themeChoice } from '../design/theme';
import { useScope } from '../data/scope';
import { anotherMainWindow, secondaryWindow } from '../work/window';

// This build's secondary windows (MOD3): opening one, and telling its frame the theme; telling the engine every window
// renders on the viewer's choice (THEMESCHEME1); and telling each main window's taskbar button what waits (NOTIFY1b, NOTIFY2c).

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
 *
 * **The window it opens follows this one's scope** (MAINWINDOWS3), or holds `workspace`, a detached session's own, where
 * the session names one: said before the shell is asked, so the new page finds it on arrival. The shell is asked by the
 * name alone.
 */
export const useOpenWindow = () => {
  const { opens } = useScope();
  return useMutation({
    mutationFn: (target: string | { name: string; workspace?: string | null }) => {
      const { name, workspace } = typeof target === 'string' ? { name: target, workspace: null } : target;
      opens(name, workspace);
      return getBridge().invoke<{ opened: boolean; windows: string[] }>(
        'DAORIS.WINDOWS', 'OPEN', { payload: { name } });
    },
  });
};

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
 * A main window's page telling its frame its own title (MAINWINDOWS3b), which names the workspace the window holds: the frame
 * wears the application's name itself and takes only what follows it, so the taskbar and Alt-Tab read two windows on two
 * workspaces apart. `name` is another main window's (`main:<n>`); none is the first. Fire and forget, as the theme is.
 *
 * `workspace` is the one the window holds, none for every workspace, told beside the title as data (NOTIFY2c): the shell picks the
 * window a notification opens from it, and never reads the title, which is the person's words.
 */
export function tellFrameTitle(title: string, name: string | null, workspace: string | null) {
  try {
    void getBridge().invoke('DAORIS.WINDOWS', 'SET_TITLE', { payload: name ? { name, title, workspace } : { title, workspace } })
      .catch(() => {});
  } catch {
    // No bridge at all: a browser has no frame to tell.
  }
}

/**
 * A main window's page telling its own frame how much waits on the person in its workspace (NOTIFY1b, NOTIFY2c): its *What needs
 * you* count, which the frame wears as its taskbar button's overlay and in its title, with `description` the words a screen reader
 * says of the overlay, in the page's language. On arrival and on each change of either.
 *
 * @remarks
 * **Each main window its own** (NOTIFY2c, the notification design §2.4). Every main window holds its own workspace and counts its
 * own, so each tells its own frame: another main window a later launch opened (ONEINSTANCE1) by the name it was opened under, as its
 * title is, and the first by none. A utility window's page (the monitor, a detached session), which reads the same counts, never
 * tells it: it has no button of its own. Fire and forget, as the theme is: a frame that cannot be told shows no count, which is what
 * it showed before.
 *
 * 🔴 **In order** (SESSBADGE1): two counts told close together may reach the frame in either order, and the title once kept 5
 * while the band read 4 from the same value. Each carries `order`, later than the one before (`nextBadgeOrder`), and the frame
 * never wears a count told before the one it wears; it keeps the order per window, since each page orders by its own clock.
 */
export function useTaskbarCount(count: number, description: string) {
  const { isAvailable } = useShenora();
  useEffect(() => {
    const search = globalThis.location?.search ?? '';
    if (!isAvailable || secondaryWindow(search) !== null) return;
    const name = anotherMainWindow(search);
    const order = nextBadgeOrder();
    void getBridge().invoke('DAORIS.WINDOWS', 'SET_BADGE', {
      payload: name ? { name, count, description, order } : { count, description, order },
    }).catch(() => {});
  }, [isAvailable, count, description]);
}

/** The order the last count was told in. */
let badgeOrder = 0;

/**
 * The order of a count told now: the clock's milliseconds, so a reloaded page tells later than the one before it, and one past the
 * last where two are told within one.
 */
function nextBadgeOrder(): number {
  badgeOrder = Math.max(badgeOrder + 1, Date.now());
  return badgeOrder;
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
