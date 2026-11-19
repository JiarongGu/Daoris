import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import type { ReadMark } from '../../work/catchup/readMark';
import { call } from '../call';
import { askHeld, isPending } from '../state/held';

/** What the query holds until the shell's work answers: no mark is known yet, which is not the mark of one never read. */
const NOT_YET = 'not-yet' as const;

// Where the person left each conversation on this machine: read as the conversation opens, moved as they read.

const markKey = (conversation: string) => ['read-mark', conversation] as const;

/** A mark as `READ_MARK` and `READ_MARK_MOVE` answer it, or null where none is kept or the answer is no mark. */
const markOf = (answer: unknown): ReadMark | null => {
  const mark = (answer as { mark?: Partial<ReadMark> | null } | null)?.mark;
  return mark && typeof mark.part === 'string' && typeof mark.seq === 'number' && typeof mark.at === 'string' && typeof mark.read === 'string'
    ? { part: mark.part, seq: mark.seq, at: mark.at, read: mark.read, steps: typeof mark.steps === 'number' ? mark.steps : null }
    : null;
};

/**
 * The conversation's mark (`READ_MARK`), read again each time a page opens it, since another window may have moved it; null
 * where the person never read it here. Undefined until it answers.
 */
export function useReadMark(conversation: string | null): { mark: ReadMark | null | undefined } {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = markKey(conversation ?? '');
  const read = useQuery({
    queryKey,
    // Told by event: the shell answers what it holds, and its read of the store comes as `ANSWERED`; until then no mark is known.
    queryFn: () => askHeld('READ_MARK', client, queryKey, { conversation: conversation! }, (answer) => (isPending(answer) ? NOT_YET : markOf(answer))),
    enabled: isAvailable && Boolean(conversation),
    refetchOnMount: 'always',
    staleTime: 0,
  });
  return { mark: read.isError ? null : read.data === NOT_YET ? undefined : read.data };
}

/** The person's mark moved to the newest event they had on screen (`READ_MARK_MOVE`); the answer is the mark that stands. */
export function useMoveReadMark() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async (move: { conversation: string; part: string; seq: number; at: string; steps?: number | null }) =>
      markOf(await call('READ_MARK_MOVE', { ...move, steps: move.steps ?? null })),
    onSuccess: (mark, { conversation }) => {
      if (mark) client.setQueryData(markKey(conversation), mark);
    },
  });
}
