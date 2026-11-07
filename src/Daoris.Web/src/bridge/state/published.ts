import { useShenora } from '@shenora/react';

// What the driver publishes is read when it is told, not on a clock.

/** How slowly a desktop window reads again what the driver publishes, beside the events (the design keeps a deadline by every event). */
export const RECONCILE_MS = 30_000;

/**
 * How often to read again what the driver publishes as `STATE_CHANGED`, where it does: in a window with no bus, `ms`;
 * in one that hears it, only the slow `RECONCILE_MS`, so an event lost on the way (the bridge replays none) costs a delay and
 * never a card or a size that is never shown.
 *
 * @remarks
 * **Why a desktop window may drop its fast interval from the start**, with no first event to wait for: the page it shows is served
 * by the shell that publishes (one build unless a host is chosen explicitly), and a wire's words are added to, never taken away.
 * **A publish missed** leaves it stale until the slow read, the window's own mount and focus reads (the queries'
 * `staleTime`), the hub's reads at each press and tick, or, on the driver's side, the watched folder's deadline.
 */
export const pollUnlessTold = (hears: boolean, ms: number): number => (hears ? RECONCILE_MS : ms);

/** `pollUnlessTold` for this window: the desktop's has the bus, a plain browser's has none. */
export const usePollUnlessTold = (ms: number): number => pollUnlessTold(useShenora().isAvailable, ms);
