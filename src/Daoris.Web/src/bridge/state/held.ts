import { hashKey, type QueryClient, type QueryKey } from '@tanstack/react-query';
import { call } from '../call';

// The page's half of the reads the shell answers from what it holds (NOBLOCK1, D56's NOBLOCK1 note): the route answers at once,
// its work runs on a .NET task of its own, and what the work answers comes back as `DAORIS.ANSWERED`, naming the query it is for.

/** `DAORIS.ANSWERED` on the wire: the query the ask named (its key's hash), and the work's answer, or that it failed. */
export type Answered = { held?: unknown; answer?: unknown; failed?: unknown };

/** Whether an answer is the shell's *nothing held yet*: its work runs, and its event brings the answer. */
export const isPending = (answer: unknown): boolean =>
  typeof answer === 'object' && answer !== null && (answer as { pending?: unknown }).pending === true;

// How each held query reads its route's answer into what it keeps, by the query's hash: what an event's answer passes through,
// so the query holds one shape whether its ask or its event answered it.
const readers = new Map<string, (answer: unknown) => unknown>();

/**
 * Ask a read the shell tells by event, for the query under `queryKey`, its answer read by `read` into what the query keeps: the
 * payload carries `held`, the key's hash, which the event names. An answer of nothing held yet keeps what the query holds, so the
 * page shows what it held until the event comes; with nothing held either, it is the pending answer read, which each reader
 * reads as empty.
 */
export async function askHeld<T>(
  route: string, client: QueryClient, queryKey: QueryKey, payload: Record<string, unknown>,
  read: (answer: unknown) => T = (answer) => answer as T,
): Promise<T> {
  const held = hashKey(queryKey);
  readers.set(held, read);
  const answer = await call<unknown>(route, { ...payload, held });
  if (!isPending(answer)) return read(answer);
  const kept = client.getQueryData<T>(queryKey);
  return kept === undefined ? read(answer) : kept;
}

/**
 * One `ANSWERED` heard: its answer, read as its query reads it, written into the query it names, in every window holding that
 * query; a failure asks that query again, so the shell's refusal is read as the route's. A query this window does not hold is
 * none of its business.
 */
export function answered(client: QueryClient, told: Answered | null | undefined) {
  if (typeof told?.held !== 'string') return;
  const query = client.getQueryCache().get(told.held);
  if (!query) return;
  if (told.failed === true) {
    void client.invalidateQueries({ queryKey: query.queryKey, exact: true });
    return;
  }
  if (told.answer === undefined) return;
  const read = readers.get(told.held) ?? ((answer: unknown) => answer);
  client.setQueryData(query.queryKey, read(told.answer));
}
