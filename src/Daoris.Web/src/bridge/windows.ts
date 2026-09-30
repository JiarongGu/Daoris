import { useEffect } from 'react';
import { useMutation } from '@tanstack/react-query';
import { getBridge, useShenora } from '@shenora/react';
import { effectiveDark, subscribeTheme } from '../theme';

// This build's secondary windows (MOD3): opening one, and telling its frame the theme.

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
 * told keeps the OS's caption, which is what it had before.
 */
export function useSecondaryWindowTheme(name: string) {
  const { isAvailable } = useShenora();
  useEffect(() => {
    if (!isAvailable) return undefined;
    const tell = () => {
      void getBridge().invoke('DAORIS.WINDOWS', 'SET_THEME', { payload: { name, dark: effectiveDark() } }).catch(() => {});
    };
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
