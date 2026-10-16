import { useShenora } from '@shenora/react';

// What the driver publishes is read when it is told, not on a clock (FABRIC-EVENT5, the fabric events design §A, D169 point 4).

/**
 * How often to read again what the driver publishes as `STATE_CHANGED`, where it does: never in a window that hears it, `ms` in
 * one that cannot, which has no bus and polls as before.
 *
 * @remarks
 * **Why a desktop window may drop its interval from the start**, with no first event to wait for: the page it shows is served
 * by the shell that publishes (they are one build), and a wire's words are added to, never taken away (D169 point 3). **And a
 * publish missed leaves it stale for no longer than the next of these**, never for good: the window's own mount and focus reads
 * (the queries' `staleTime`), the state hub's reads at each press and tick, and, on the driver's side, each watched folder's
 * deadline, which finds what its hint missed and tells it then (`HomeWatch`).
 */
export const pollUnlessTold = (hears: boolean, ms: number): number | false => (hears ? false : ms);

/** `pollUnlessTold` for this window: the desktop's has the bus, a plain browser's has none. */
export const usePollUnlessTold = (ms: number): number | false => pollUnlessTold(useShenora().isAvailable, ms);
