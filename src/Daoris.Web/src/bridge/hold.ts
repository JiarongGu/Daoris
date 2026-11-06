import { type QueryClient, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { keys } from '../data/queries';
import { call } from './call';

// A terminal's hold of a conversation (RPC1g1, D238 point 2, the terminal-conversation design §3.1): which conversations a terminal
// holds over the pipe, read as the page mounts and kept by the shell's `SESSION_HOLDS` at each change, and the person taking one back.
// Both routes are the window's alone: a terminal lets go of its own hold by the pipe's `LET_GO`.

/** A conversation a terminal holds: the conversation, the terminal as the system names it, and since when. */
export type SessionHold = { session: string; asker?: { process?: string | null; pid?: number | null } | null; since?: string | null };

/** Every hold, whole, numbered so the newest telling is kept. */
export type SessionHolds = { seq: number | null; holds: SessionHold[] };

const NONE: SessionHolds = { seq: null, holds: [] };

/** A telling or a read of the holds, or null where it is of no shape this reads. */
export function holdsOf(told: unknown): SessionHolds | null {
  const shaped = told as { seq?: unknown; holds?: unknown } | null | undefined;
  if (!Array.isArray(shaped?.holds)) return null;
  return {
    seq: typeof shaped.seq === 'number' ? shaped.seq : null,
    holds: (shaped.holds as SessionHold[]).filter((each) => typeof each?.session === 'string'),
  };
}

/** Keep a telling where it is newer than what is kept: the bus may deliver one late, and a read may land after a telling. */
function keep(client: QueryClient, told: unknown) {
  const now = holdsOf(told);
  if (!now) return;
  const kept = client.getQueryData<SessionHolds>(keys.sessionHolds)?.seq ?? null;
  if (now.seq !== null && kept !== null && now.seq <= kept) return;
  client.setQueryData(keys.sessionHolds, now);
}

/** The terminal holding `session`, or null: none in a browser, which has no pipe. */
export function useSessionHold(session: string): SessionHold | null {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  useShenoraEvent<SessionHolds>('DAORIS', 'SESSION_HOLDS', (told) => keep(client, told));
  const query = useQuery({
    queryKey: keys.sessionHolds,
    queryFn: async () => {
      const read = holdsOf(await call<SessionHolds>('SESSION_HOLDS')) ?? NONE;
      const kept = client.getQueryData<SessionHolds>(keys.sessionHolds);
      return kept && kept.seq !== null && read.seq !== null && kept.seq >= read.seq ? kept : read;
    },
    enabled: isAvailable,
    // The telling keeps it current: nothing asks again by itself.
    staleTime: Infinity,
    retry: false,
  });
  return (query.data ?? NONE).holds.find((each) => each.session === session) ?? null;
}

/** How *Let go of the terminal* went: done, or not, by the bridge's code (`not-held`) and its sentence. */
export type LetGoAnswer = { done: boolean; why?: string | null; message?: string | null };

/** *Let go of the terminal*: the person takes the conversation back; it goes on in Daoris, and the terminal is told. */
export function useLetGoOfTerminal() {
  return useMutation({
    mutationFn: async (session: string): Promise<LetGoAnswer> =>
      ((await call('SESSION_LET_GO', { id: session })) as LetGoAnswer | null) ?? { done: false },
  });
}
