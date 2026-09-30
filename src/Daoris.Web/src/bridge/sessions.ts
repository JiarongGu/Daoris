import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { keys } from '../queries';
import type { SessionEvent } from '../work/conversation';
import { call } from './call';

// A session's life on this machine (MOD3): the person's answer to a parked one, a stop and how it is
// said, and finding sessions by their first words and by what they said.

/**
 * The person's answer to a session parked at a checkpoint (design §4).
 *
 * @remarks
 * **Three moves, and it goes through the driver.** `awaiting-person` has meant "only the person can
 * clear this" since D46; the ledger allows a fourth move from it — back to `working` — and that one
 * is the driver observing a session that carried on, which a person causes by *answering* it. So
 * this carries exactly `completed`, `declined` and `stopped`, and the host refuses anything else
 * with a sentence.
 *
 * It lands on the driver rather than on the service because the process and the record must move
 * together: this machine lets the process go, and only then does the record say it ended.
 */
export const useResolveSession = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (move: { id: string; state: 'completed' | 'declined' | 'stopped'; note?: string }) =>
      call<{ session: string; state: string; message: string }>('RESOLVE_SESSION', {
        id: move.id,
        state: move.state,
        ...(move.note ? { note: move.note } : {}),
      }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.allSessions });
      void client.invalidateQueries({ queryKey: keys.allQuests });
      void client.invalidateQueries({ queryKey: keys.driver });
    },
  });
};

/**
 * What the driver answered a stop: whether it ended anything, and whether what it ended was an
 * orphan — a record that said it ran when nothing on this machine ran it (2026-09-25).
 */
export type StopAnswer = { stopped: boolean; orphan?: boolean; elsewhere?: boolean };

/**
 * The notice for a stop, from the driver's answer — never the person's words for an ending that was
 * not theirs. Both doors to a stop (the frame's, the quest record's) say it the same way.
 */
export const stopNotice = (answer: StopAnswer) =>
  answer.orphan ? 'quests.session.orphanEnded'
    : answer.stopped ? 'quests.session.stopped'
      // Another Daoris process here runs it — a terminal's — so the record still says working (REV3).
      : answer.elsewhere ? 'quests.session.runElsewhere'
        : 'quests.session.notRunning';

export const useStopSession = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => call<StopAnswer>('STOP_SESSION', { id }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.allSessions });
      void client.invalidateQueries({ queryKey: keys.driver });
    },
  });
};

/**
 * What the person first said in each of these sessions (RAIL1): a conversation's identity, as
 * `sessionTitle` takes it — read from this machine's own record, never from the session record, which
 * travels (D47 §4).
 *
 * @remarks
 * **Keyed by the whole set**, sorted, so every region that names sessions from the same list — the rail,
 * the head — asks once between them. A conversation is named by its first message, so when one arrives
 * for a session not yet named, the answer is asked for again.
 */
export const useSessionOpenings = (sessions: readonly { id: string }[] | undefined): Record<string, string> => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const ids = [...new Set((sessions ?? []).map((session) => session.id))].sort();
  const answer = useQuery({
    queryKey: keys.openings(ids),
    queryFn: () => call<{ openings?: Record<string, string> }>('SESSION_OPENINGS', { ids }),
    enabled: isAvailable && ids.length > 0,
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  });
  const openings = answer.data?.openings ?? {};

  useShenoraEvent('DAORIS', 'SESSION_EVENTS', (payload) => {
    const batch = payload as { session?: string; events?: SessionEvent[] } | undefined;
    if (!batch?.session || openings[batch.session]) return;
    if (batch.events?.some((event) => event.kind === 'user' && event.origin === 'person')) {
      void client.invalidateQueries({ queryKey: keys.allOpenings });
    }
  });

  return openings;
};

/** Where a search found its words: the session, the event it began at, whose words, and a window of them. */
export type SessionHit = { session: string; seq: number; kind: string; snippet: string };

/**
 * What sessions said, searched (RAIL1): the person's words and the agent's, on this machine's own
 * record, bounded by the host and saying so (`cut`). Asked from two letters on — one letter matches
 * everything — and the caller debounces, so a word typed is one question, not one per key.
 */
export const useSessionSearch = (query: string, session?: string) => {
  const { isAvailable } = useShenora();
  const q = query.trim();
  return useQuery({
    queryKey: keys.sessionSearch(q, session),
    // Within one session, where one is named (SESS1 S9): its words and its calls by their titles.
    queryFn: () => call<{ query: string; hits: SessionHit[]; cut: boolean }>(
      'SESSION_SEARCH', { q, ...(session ? { session } : {}) }),
    enabled: isAvailable && q.length >= 2,
    staleTime: 10_000,
    refetchOnWindowFocus: false,
    retry: false,
  });
};
