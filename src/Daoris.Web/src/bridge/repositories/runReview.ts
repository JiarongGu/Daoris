import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import type { RunReviewAnswer, RunReviewFileAnswer } from '../../work/review/run/runReviewWire';
import { askHeld, isPending } from '../state/held';

const reviewOf = (answer: unknown): RunReviewAnswer | null => (isPending(answer) ? null : answer as RunReviewAnswer);
const fileOf = (answer: unknown): RunReviewFileAnswer | null => (isPending(answer) ? null : answer as RunReviewFileAnswer);

/**
 * A run's review at `attempt`, 0 for its newest record. Desktop-only: the records are this machine's. Told by event, so the page shows what
 * it held, or null where it holds none, until the driver's read comes.
 */
export const useRunReview = (run: string | null, attempt = 0, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  return useQuery({
    queryKey: keys.runReview(run ?? '', attempt),
    queryFn: () => askHeld('RUN_REVIEW', client, keys.runReview(run ?? '', attempt), { run, session: null, attempt }, reviewOf),
    enabled: isAvailable && enabled && !!run,
    staleTime: 0,
  });
};

/** The review of the run `session`'s quest is, its newest record: `refused` `not-run` where no run governs the session. */
export const useSessionRunReview = (session: string | null, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  return useQuery({
    queryKey: keys.sessionRunReview(session ?? ''),
    queryFn: () => askHeld('RUN_REVIEW', client, keys.sessionRunReview(session ?? ''), { run: null, session, attempt: 0 }, reviewOf),
    enabled: isAvailable && enabled && !!session,
    staleTime: 0,
  });
};

/** One file of `run`'s record at `attempt`, `before` or `after`: read once, since a record is never rewritten. */
export const useRunReviewFile = (run: string, attempt: number, path: string | null, side: 'before' | 'after', { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  return useQuery({
    queryKey: keys.runReviewFile(run, attempt, path ?? '', side),
    queryFn: () => askHeld('RUN_REVIEW_FILE', client, keys.runReviewFile(run, attempt, path ?? '', side), { run, attempt, path: path ?? '', side }, fileOf),
    enabled: isAvailable && enabled && !!path,
    staleTime: Infinity,
  });
};
