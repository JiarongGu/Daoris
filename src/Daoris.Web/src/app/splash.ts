import { useEffect, useRef } from 'react';
import { useIsFetching } from '@tanstack/react-query';
import { WindowCommands } from '@shenora/react';

// The window's splash, page side (SPLASHDATA1, D56; D22's SHEN3 note). The main window holds its splash over this page
// until the page says its first screen has its data, over the kit's `CLOSE_SPLASH` (`closeSplash`): it came down as the
// page's browser was made, and the person watched an empty frame fill in. The window lifts it at a bound of its own
// when the page never says (`SplashLift`), so nothing here can hold the window for ever.

/**
 * How long nothing may be fetching before the first screen counts as having its data, in ms. A query that waits on
 * another's answer (a view's on the shell's) starts a render after that answer, so a moment with nothing fetching is
 * not yet the end of it; a quiet this long is.
 */
export const SPLASH_QUIET_MS = 150;

/**
 * Tell the window, once, that the first screen has its data: every query the page started has settled, with its data
 * or with its failure (which the screen then says), and nothing more started for {@link SPLASH_QUIET_MS}. The main
 * window's page only: the kit answers a secondary window's call `NO_ROUTE`, and a browser has no window to tell.
 */
export function useSplashLift(): void {
  const fetching = useIsFetching();
  const said = useRef(false);

  useEffect(() => {
    if (said.current || fetching > 0) return;
    const quiet = window.setTimeout(() => {
      said.current = true;
      // Fire-and-forget, as every window command is (`useWindowChrome`): a call that fails is a window that was never
      // ours, and the window's own bound lifts its splash anyway.
      try {
        void new WindowCommands().closeSplash().catch(() => {});
      } catch {
        // `new WindowCommands()` itself throws with no bridge at all.
      }
    }, SPLASH_QUIET_MS);
    return () => window.clearTimeout(quiet);
  }, [fetching]);
}
