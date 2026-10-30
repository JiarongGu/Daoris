import { hashKey, type QueryClient, type QueryKey, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { call } from '../call';
import { heldNumbered, isPending } from '../state/held';
import type { AtWorkA, AtWorkAt, AtWorkHeld } from '../../work/atWork/types';

// Who is at work on a session (COWORKVIEW1a, the cowork view design §3.8 and §4.1): the shell's one numbered read, composed from
// the records Daoris keeps (its parts, its lineage), never from the conversation's text.

export type { AtWorkA, AtWorkAt, AtWorkChild, AtWorkHeld } from '../../work/atWork/types';

export const atWorkKey = (session: string): QueryKey => ['atWork', session];

/** Every session's read, as the hub asks it again. */
export const AT_WORK: QueryKey = ['atWork'];

const READING: AtWorkHeld = { reading: true };

const atOf = (value: unknown): AtWorkAt | undefined => {
  const at = (value as { at?: { epoch?: unknown; n?: unknown } } | null)?.at;
  return typeof at?.epoch === 'string' && typeof at.n === 'number' ? { epoch: at.epoch, n: at.n } : undefined;
};

const whyOf = (value: unknown): string | undefined => {
  const why = (value as { why?: unknown } | null | undefined)?.why;
  return typeof why === 'string' ? why : undefined;
};

/**
 * A delivery, a reply or an event, as the query holds it; null for the shell's *nothing held yet*, which carries no number and keeps
 * what the query holds.
 */
export function readAtWork(wire: unknown): AtWorkHeld | null {
  const at = atOf(wire);
  if (isPending(wire) && !at) return null;
  const told = wire as { pending?: unknown; failed?: unknown; answer?: unknown; stale?: unknown } | null;
  const tagged = <T extends AtWorkHeld>(held: T): T => (at ? { ...held, at } : held);
  if (told?.pending === true) return tagged({ reading: true });
  const failed = whyOf(told?.failed);
  if (failed !== undefined) return tagged({ failed: { why: failed } });
  if (told && typeof told.answer === 'object' && told.answer !== null) {
    const stale = whyOf(told.stale);
    return tagged({ answer: told.answer as AtWorkA, ...(stale !== undefined ? { stale: { why: stale } } : {}) });
  }
  return tagged({ failed: { why: '' } });
}

/**
 * The number rule (§4.1, C4-3.1, C5-2.3): a delivery to a query whose data carries no number is taken; of the same epoch, a higher
 * number replaces and a lower or equal one is dropped; an event of another epoch is never installed (`another`), a reply's taken.
 */
export function byNumber(held: AtWorkHeld | undefined, next: AtWorkHeld, reply: boolean): AtWorkHeld | 'dropped' | 'another' {
  const was = held?.at;
  const now = next.at;
  if (!was || !now) return next;
  if (was.epoch !== now.epoch) return reply ? next : 'another';
  return now.n > was.n ? next : 'dropped';
}

// The events held beside each query's fetch while it is out and its data carries no number (C6-2.5), by the query's hash.
const fences = new Map<string, AtWorkHeld[]>();

/** One event told: written at once by the number rule, or held beside the query's own first fetch, or another epoch's asking again. */
function told(client: QueryClient, queryKey: QueryKey, wire: unknown) {
  const next = readAtWork(wire);
  if (!next) return;
  const fence = fences.get(hashKey(queryKey));
  const held = client.getQueryData<AtWorkHeld>(queryKey);
  if (fence && !held?.at) {
    fence.push(next);
    return;
  }
  const taken = byNumber(held, next, false);
  if (taken === 'another') void client.invalidateQueries({ queryKey, exact: true });
  else if (taken !== 'dropped') client.setQueryData(queryKey, taken);
}

/** The query's own fetch: the fence opened as it begins and closed as it settles, its reply first, then what was held beside it. */
export async function askAtWork(client: QueryClient, queryKey: QueryKey, session: string): Promise<AtWorkHeld> {
  const held = hashKey(queryKey);
  heldNumbered(held, (answer) => told(client, queryKey, answer));
  const fence: AtWorkHeld[] = [];
  fences.set(held, fence);
  let settled: AtWorkHeld | undefined = client.getQueryData<AtWorkHeld>(queryKey);
  let another = false;
  try {
    const reply = readAtWork(await call<unknown>('SESSION_AT_WORK', { session, held }));
    if (reply) {
      const taken = byNumber(settled, reply, true);
      if (typeof taken !== 'string') settled = taken;
    }
    return (settled = applyHeld(settled, fence, () => { another = true; }) ?? READING);
  } catch (error) {
    // A refusal of the bridge itself: what was held beside the fetch is written by the number rule against what the query holds.
    const written = applyHeld(client.getQueryData<AtWorkHeld>(queryKey), fence, () => { another = true; });
    if (written) client.setQueryData(queryKey, written);
    throw error;
  } finally {
    if (fences.get(held) === fence) fences.delete(held);
    if (another) setTimeout(() => void client.invalidateQueries({ queryKey, exact: true }), 0);
  }
}

function applyHeld(start: AtWorkHeld | undefined, fence: AtWorkHeld[], again: () => void): AtWorkHeld | undefined {
  let now = start;
  for (const next of [...fence].sort((a, b) => (a.at?.n ?? 0) - (b.at?.n ?? 0))) {
    const taken = byNumber(now, next, false);
    if (taken === 'another') again();
    else if (taken !== 'dropped') now = taken;
  }
  return now;
}

/**
 * Who is at work on `session`: its entry's parts and the attended part's subagents, held as `AtWorkHeld`. The query never rejects
 * for a work's failure (the shell answers it with its words) and is never retried; the hub's `atWork` asks it again.
 */
export const useSessionAtWork = (session: string | null | undefined) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = atWorkKey(session ?? '');
  return useQuery({
    queryKey,
    queryFn: () => askAtWork(client, queryKey, session ?? ''),
    enabled: isAvailable && !!session,
    retry: false,
    staleTime: 30_000,
  });
};
