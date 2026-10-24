import { hashKey, type QueryClient, type QueryKey } from '@tanstack/react-query';
import { call } from '../call';

// The page's half of the reads the shell answers from what it holds (NOBLOCK1, D56's NOBLOCK1 note): the route answers at once,
// its work runs on a .NET task of its own, and what the work answers comes back as `DAORIS.ANSWERED`, naming the query it is for.

/** `DAORIS.ANSWERED` on the wire: the query the ask named (its key's hash), and the work's answer, or that it failed. */
export type Answered = { held?: unknown; answer?: unknown; failed?: unknown };

/** Whether an answer is the shell's *nothing held yet*: its work runs, and its event brings the answer. */
export const isPending = (answer: unknown): boolean =>
  typeof answer === 'object' && answer !== null && (answer as { pending?: unknown }).pending === true;

/**
 * Ask a read the shell tells by event, for the query under `queryKey`: the payload carries `held`, the key's hash, which the event
 * names. An answer of nothing held yet keeps what the query holds, so the page shows what it held until the event comes; with
 * nothing held either, it is the pending answer itself, which each reader reads as empty.
 */
export async function askHeld<T>(route: string, client: QueryClient, queryKey: QueryKey, payload: Record<string, unknown>): Promise<T> {
  const answer = await call<T>(route, { ...payload, held: hashKey(queryKey) });
  if (!isPending(answer)) return answer;
  return client.getQueryData<T>(queryKey) ?? answer;
}

/**
 * One `ANSWERED` heard: its answer written into the query it names, in every window holding that query; a failure asks that query
 * again, so the shell's refusal is read as the route's. A query this window does not hold is none of its business.
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
  client.setQueryData(query.queryKey, told.answer);
}
