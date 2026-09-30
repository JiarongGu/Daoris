import { getBridge } from '@shenora/react';

// The bridge's own helper, shared by its domains and re-exported by none: a page reaches the driver
// through a domain's named calls, never by naming a route itself (MOD3).

/**
 * One call onto the driver's module, with its payload when it has one. A route whose work is long passes its own
 * bound (WSR7): without one the bridge gives up after its default 30 seconds, while the host carries on unheard.
 */
export const call = <TData,>(type: string, payload?: Record<string, unknown>, options?: { timeoutMs?: number }): Promise<TData> =>
  getBridge().invoke<TData>('DAORIS.DRIVER', type, {
    ...(payload ? { payload } : {}),
    ...(options?.timeoutMs ? { timeoutMs: options.timeoutMs } : {}),
  });

const MINUTE = 60_000;

/**
 * The driver's own bounds on its long routes (WSR7), spelled again so the page waits as long as the host may work. A
 * twin: `SyncBounds` (a fetch, how many at once, a replay) and `LandingPlugins.DefaultPatience` in the driver, each row
 * held to its number by `SyncBoundsTests`. Minutes, or a count.
 */
export const hostBounds = {
  fetchMinutes: 2,
  fetchesAtOnce: 4,
  replayMinutes: 5,
  pluginMinutes: 2,
} as const;

/** Room for what the host does around its bounded steps: the registry, the local git, the answer on its way back. */
const SLACK = 2 * MINUTE;

/** How long a look that fetches `repositories` may take: a few at a time, each within a fetch's bound. */
export const lookBound = (repositories: number) =>
  Math.ceil(Math.max(1, repositories) / hostBounds.fetchesAtOnce) * hostBounds.fetchMinutes * MINUTE + SLACK;

/** How long a press over `rows` may take: at most one replay's bound each, one after another. */
export const pressBound = (rows: number) => Math.max(1, rows) * hostBounds.replayMinutes * MINUTE + SLACK;

/** How long a landing or a hand-off a plugin pushes may take: the plugin's start and its one answer, each within its patience. */
export const pluginBound = 2 * hostBounds.pluginMinutes * MINUTE + SLACK;

/** Whether a call ended because the page stopped waiting (the bridge's `TIMEOUT`), rather than by the host's answer. */
export const stoppedWaiting = (error: unknown) => (error as { code?: unknown } | null)?.code === 'TIMEOUT';
