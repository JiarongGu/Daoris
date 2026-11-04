import { getBridge } from '@shenora/react';
import type { DriverPayloads, DriverRoute } from './generated/routes';

// The bridge's own helper, shared by its domains and re-exported by none: a page reaches the driver
// through a domain's named calls, never by naming a route itself (MOD3).

/** A route whose handler takes a payload record, which the generated routes carry. */
export type TypedRoute = keyof DriverPayloads;

/** A route whose handler still reads its request by hand. */
export type ByHandRoute = Exclude<DriverRoute, TypedRoute>;

/** The record a typed route sends. */
export type Payload<R extends TypedRoute> = DriverPayloads[R]['payload'];

/** What a typed route answers: its handler's record, or unknown where it answers none. */
export type Answer<R extends TypedRoute> = DriverPayloads[R]['answer'];

type Bound = { timeoutMs?: number };

/**
 * One call onto the driver's module, the route one the module answers, as generated from its marks. A typed route sends its
 * generated payload and is answered its generated answer; a route read by hand names its answer's type and sends any payload. A
 * route whose work is long passes its own bound: without one the bridge gives up after its default 30 seconds, while the host
 * carries on unheard.
 */
export function call<R extends TypedRoute>(type: R, payload: Payload<R>, options?: Bound): Promise<Answer<R>>;
// transitional: route-payload-by-hand
export function call<TData>(type: ByHandRoute, payload?: Record<string, unknown>, options?: Bound): Promise<TData>;
export function call(type: DriverRoute, payload?: Record<string, unknown>, options?: Bound): Promise<unknown> {
  return getBridge().invoke<unknown>('DAORIS.DRIVER', type, {
    ...(payload ? { payload } : {}),
    ...(options?.timeoutMs ? { timeoutMs: options.timeoutMs } : {}),
  });
}

const MINUTE = 60_000;

/**
 * The driver's own bounds on its long routes (WSR7), spelled again so the page waits as long as the host may work. A
 * twin: `SyncBounds` (a fetch, how many at once, a replay), `LandingPlugins.DefaultPatience` and `ToolInstall.LookBound`
 * (one resource location's look, TOOLS7) in the driver, each row held to its number by `SyncBoundsTests`. Minutes, or a
 * count.
 */
export const hostBounds = {
  fetchMinutes: 2,
  fetchesAtOnce: 4,
  replayMinutes: 5,
  pluginMinutes: 2,
  toolLookMinutes: 0.5,
} as const;

/** Room for what the host does around its bounded steps: the registry, the local git, the answer on its way back. */
const SLACK = 2 * MINUTE;

/** How long a look that fetches `repositories` may take: a few at a time, each within a fetch's bound. */
export const lookBound = (repositories: number) =>
  Math.ceil(Math.max(1, repositories) / hostBounds.fetchesAtOnce) * hostBounds.fetchMinutes * MINUTE + SLACK;

/**
 * How long a terminal's press's Confirm may take (CONFIRMWAIT1a-i, the confirm-wait design §5.2): its grounds are read again before it
 * runs, as long as its slowest read may take (`checkWithin`, which the shell tells on the press, so the page names no route), and the
 * bridge's slack beside it. None told keeps the bridge's own bound.
 */
export const confirmBound = (checkWithin: number | null | undefined): number | undefined =>
  typeof checkWithin === 'number' && checkWithin > 0 ? checkWithin + SLACK : undefined;

/** How long a press over `rows` may take: at most one replay's bound each, one after another. */
export const pressBound = (rows: number) => Math.max(1, rows) * hostBounds.replayMinutes * MINUTE + SLACK;

/** How long a landing or a hand-off a plugin pushes may take: the plugin's start and its one answer, each within its patience. */
export const pluginBound = 2 * hostBounds.pluginMinutes * MINUTE + SLACK;

/** How long *Look for updates* may take over `locations` (TOOLS7, D121 §3.7): one after another, each within a look's bound. */
export const toolLookBound = (locations: number) => Math.max(1, locations) * hostBounds.toolLookMinutes * MINUTE + SLACK;

/**
 * Whether the driver refused because it is still coming up (`DRIVER_NOT_READY`): its service is not handed to the routes
 * yet. A refusal of this kind is asked again when the driver says it is up (LOOK2a, `useDriverReady`).
 */
export const refusedNotReady = (error: unknown) => (error as { code?: unknown } | null)?.code === 'DRIVER_NOT_READY';

/** Whether a call ended because the page stopped waiting (the bridge's `TIMEOUT`), rather than by the host's answer. */
export const stoppedWaiting = (error: unknown) => (error as { code?: unknown } | null)?.code === 'TIMEOUT';
