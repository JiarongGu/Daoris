import { type QueryClient, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { api, holdPersonKey } from '../data/api';
import { keys } from '../data/queries';
import type { Confirmation, ConfirmationAnswer, ConfirmationField } from '../person/Confirmations';
import { call } from './call';

// The person's door in the window (PERSONDOOR1f, RPC1c; D156 points 2 and 4, the person-door design §2.2 and §4.2): the person
// key the shell holds for its host, kept in memory; the presses a terminal sent over the pipe that wait for the person's Confirm;
// and, once the page holds the key, what waits at the service's own confirmations. Every route here is the window's alone: the
// pipe's census refuses each to a terminal, which asks and never answers.

/** A press a terminal sent over the pipe, waiting for the window, as `PERSON_CONFIRMATIONS` answers it and `CONFIRMATIONS` tells it. */
export type PipeConfirmation = {
  id: string; module: string; route: string; fields: ConfirmationField[];
  asker?: { process?: string | null; pid?: number | null } | null; asked: string; expires: string;
};

/** What waits, whole, numbered so the newest telling is kept (as `UNDER_WAY` is). */
export type PipeConfirmations = { seq: number | null; waiting: PipeConfirmation[] };

const NONE: PipeConfirmations = { seq: null, waiting: [] };

/** A telling or a read of what waits, or null where it is of no shape this reads. */
function confirmationsOf(told: unknown): PipeConfirmations | null {
  const shaped = told as { seq?: unknown; waiting?: unknown } | null | undefined;
  if (!Array.isArray(shaped?.waiting)) return null;
  return {
    seq: typeof shaped.seq === 'number' ? shaped.seq : null,
    waiting: (shaped.waiting as PipeConfirmation[]).filter((each) => typeof each?.id === 'string' && typeof each.route === 'string'),
  };
}

/** Keep a telling where it is newer than what is kept: the bus may deliver one late, and a read may land after a telling. */
function keep(client: QueryClient, told: unknown) {
  const now = confirmationsOf(told);
  if (!now) return;
  const kept = client.getQueryData<PipeConfirmations>(keys.terminalAsks)?.seq ?? null;
  if (now.seq !== null && kept !== null && now.seq <= kept) return;
  client.setQueryData(keys.terminalAsks, now);
}

const fromPipe = (waiting: PipeConfirmation): Confirmation => ({
  source: 'pipe', id: waiting.id, route: waiting.route, fields: waiting.fields ?? [], asker: waiting.asker ?? null, expires: waiting.expires,
});

/**
 * What a terminal asked this window to confirm over the pipe (RPC1c): read as the page mounts, for a window opened or reloaded
 * while one waits, then kept by the shell's `CONFIRMATIONS` at each change. None in a browser, which has no pipe, and none from a
 * shell older than the route.
 */
export function useTerminalConfirmations(): Confirmation[] {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  useShenoraEvent<PipeConfirmations>('DAORIS', 'CONFIRMATIONS', (told) => keep(client, told));
  const query = useQuery({
    queryKey: keys.terminalAsks,
    queryFn: async () => {
      const read = confirmationsOf(await call<PipeConfirmations>('PERSON_CONFIRMATIONS')) ?? NONE;
      const kept = client.getQueryData<PipeConfirmations>(keys.terminalAsks);
      return kept && kept.seq !== null && read.seq !== null && kept.seq >= read.seq ? kept : read;
    },
    enabled: isAvailable,
    // The telling keeps it current: nothing asks again by itself.
    staleTime: Infinity,
    retry: false,
  });
  return (query.data ?? NONE).waiting.map(fromPipe);
}

/**
 * What waits at the service for the person's confirmation (PERSONDOOR1b's doors, D156 §4.2), asked only while the page holds the
 * person key: its Confirm and Refuse take the key, and a host handed none lists none. Polled, since the service tells no window.
 */
export function useServiceConfirmations(held: boolean): Confirmation[] {
  const query = useQuery({
    queryKey: keys.serviceAsks,
    queryFn: ({ signal }) => api.confirmations(signal),
    enabled: held,
    refetchInterval: 5_000,
    retry: false,
  });
  return (query.data ?? []).filter((each) => each.state === 'waiting').map((each): Confirmation => ({
    source: 'service',
    id: each.id,
    route: `${each.method} ${each.route}`,
    act: each.act,
    fields: [...(each.values ?? []), ...(each.fields ?? [])],
    asker: null,
    expires: each.expires,
  }));
}

/**
 * The person's Confirm or Refuse (RPC1c, PERSONDOOR1f): a terminal's press by its confirmation's id over the bridge, the service's
 * at its own door with the key. One no longer waiting is answered, never thrown, for the card to say where it was pressed (the
 * UI/UX design's R3): the bridge's code (`gone`), or the host's own sentence.
 */
export function useAnswerConfirmation() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async ({ waiting, confirm }: { waiting: Confirmation; confirm: boolean }): Promise<ConfirmationAnswer> => {
      if (waiting.source === 'pipe') {
        const answer = await call<{ answered?: boolean; why?: string | null; message?: string | null }>(
          'PERSON_CONFIRM', { id: waiting.id, confirm });
        return answer?.answered === true
          ? { answered: true }
          : { answered: false, why: answer?.why ?? null, message: answer?.message ?? '' };
      }

      try {
        await api.answerConfirmation(waiting.id, confirm);
        return { answered: true };
      } catch (error) {
        return { answered: false, why: null, message: (error as Error).message };
      }
    },
    onSettled: (_answer, _error, { waiting }) => {
      if (waiting.source === 'service') void client.invalidateQueries({ queryKey: keys.serviceAsks });
    },
  });
}

/**
 * The person key the shell holds for its host (D156 point 2, design §2.2), asked as the page loads and held in `data/api.ts`'s
 * memory, never in the query cache, the page's storage or a URL; a reload asks again. Answers whether one is held, and whether the
 * ask has settled. None in a browser, from a shell older than the route, or where the shell holds none.
 */
export function usePersonKey(): { held: boolean; settled: boolean } {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: keys.personKeyHeld,
    queryFn: async () =>
      (await holdPersonKey(async () => (await call<{ key?: string | null }>('PERSON_KEY'))?.key ?? null)) !== null,
    enabled: isAvailable,
    staleTime: Infinity,
    retry: false,
  });
  return { held: query.data === true, settled: !isAvailable || query.isSuccess || query.isError };
}

/**
 * Whether the window should say the host gates the person's acts with a key this page does not hold (PERSONDOOR1f, design §2.3's
 * adoption notice): asked once the page's own ask for the key has settled empty, of the host's proof of possession. A host that
 * proves a key this window was never handed was started by something other than this window's shell.
 */
export function useUnkeyedHost(person: { held: boolean; settled: boolean }): boolean {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: keys.hostKeyed,
    queryFn: ({ signal }) => api.hostHoldsAKey(signal),
    enabled: isAvailable && person.settled && !person.held,
    staleTime: Infinity,
    retry: false,
  });
  return isAvailable && person.settled && !person.held && query.data === true;
}
