import { useCallback, useEffect, useRef, useSyncExternalStore } from 'react';
import { useIsFetching, useQueryClient, type QueryKey } from '@tanstack/react-query';
import { WindowCommands } from '@shenora/react';

// The window's splash, page side (SPLASHDATA1, D56; D22's SHEN3 note). The main window holds its splash over this page
// until the page says its first screen has its data, over the kit's `CLOSE_SPLASH` (`closeSplash`): it came down as the
// page's browser was made, and the person watched an empty frame fill in. The window lifts it at a bound of its own
// when the page never says (`SplashLift`), so nothing here can hold the window for ever.

/**
 * How long the first screen must stay answered before it counts as having its data, in ms. A query that waits on
 * another's answer (a view's on the shell's) starts a render after that answer, so a moment with nothing loading is
 * not yet the end of it; a quiet this long is.
 */
export const SPLASH_QUIET_MS = 150;

/**
 * The roots of the queries the first screen draws (STARTFAST1b, the start-path design §4-5): a start lands on Overview or
 * Sessions (`rememberedView`), and those views and the frame around them read the service's status, the repositories and
 * the registry, the driver's state, the quests, asks and sessions (the list's groups among them), and what needs the person
 * and the notices. Not the harnesses' probes nor the lines (`['driver', 'lines']`), which queue behind the driver's first
 * looks: the splash waited on them, and on every tick's refetch, until its bound.
 */
const FIRST_SCREEN_ROOTS: ReadonlySet<string> = new Set([
  'status', 'repositories', 'registry', 'quests', 'asks', 'sessions', 'attention', 'notices',
]);

/** Whether a query is one the first screen draws: a root of {@link FIRST_SCREEN_ROOTS}, or the driver's state itself. */
export function isFirstScreen(key: QueryKey): boolean {
  const [root] = key;
  if (typeof root !== 'string') return false;
  return FIRST_SCREEN_ROOTS.has(root) || (root === 'driver' && key.length === 1);
}

/**
 * Whether the first screen has its data: one of its queries has answered, and none is on its first fetch. A query that
 * answered and is asked again (a tick's invalidation) keeps its answer on the screen, and a query not asked yet (one
 * waiting on another's answer) is the quiet's to wait for.
 */
function useFirstScreenAnswered(firstScreen: (key: QueryKey) => boolean): boolean {
  const cache = useQueryClient().getQueryCache();
  const subscribe = useCallback((changed: () => void) => cache.subscribe(() => changed()), [cache]);
  const read = useCallback(() => {
    const queries = cache.findAll({ predicate: (query) => firstScreen(query.queryKey) });
    return queries.some((query) => query.state.status !== 'pending')
      && !queries.some((query) => query.state.status === 'pending' && query.state.fetchStatus === 'fetching');
  }, [cache, firstScreen]);
  return useSyncExternalStore(subscribe, read, read);
}

/**
 * Tell the window, once, that the first screen has its data: its own queries ({@link isFirstScreen}) have answered, with
 * their data or with their failure (which the screen then says), and stayed answered for {@link SPLASH_QUIET_MS}. Or, the
 * fallback, every query the page started has settled and nothing more started for as long. The main window's page only:
 * the kit answers a secondary window's call `NO_ROUTE`, and a browser has no window to tell.
 */
export function useSplashLift(firstScreen: (key: QueryKey) => boolean = isFirstScreen): void {
  const fetching = useIsFetching();
  const answered = useFirstScreenAnswered(firstScreen);
  const ready = answered || fetching === 0;
  const said = useRef(false);

  useEffect(() => {
    if (said.current || !ready) return;
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
  }, [ready]);
}
