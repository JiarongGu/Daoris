import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import { askHeld } from '../state/held';

// A run's review, opened by its run: the record of one attempt as the driver kept it once, where the review stands now, and the steps
// before and after it. The page draws it and derives nothing; its words are the page's, worded from `says`.

/** One file the record's work added, changed or removed, with its hashes and marks. */
export type ReviewRow = { path: string; change: 'added' | 'modified' | 'deleted' | string; before?: string | null; after?: string | null; marks: string[] };

/** What the record could not read or show. */
export type ReviewGap = { code: string; path?: string | null; said: string };

/** One part of the run's chain and its handback. */
export type ReviewPart = {
  quest: string; title: string; session?: string | null; at?: string | null; note?: string | null; commit?: string | null;
  requirements: { quote: string; check: string }[]; unchecked: boolean;
};

/** The record of one attempt of the run's review, written once as the attempt began. */
export type ReviewRecord = {
  run: string; node: string; attempt: number; kind: string; revision: string; at: string; place: string;
  reference: Record<string, unknown>; parts: ReviewPart[]; rows: ReviewRow[]; incomplete: ReviewGap[]; coverage: string;
};

/** A step before or after the review, as it stands, with the way that leads to it, its outcome, its door and the person's recovery. */
export type RunReviewStep = {
  node: string; kind: string; state: string; attempt: number; code?: string | null; words?: string | null; since?: string | null;
  via?: string | null; on?: string | null; outcome?: string | null; door?: string | null; offers: string[];
};

/** The values the review's one sentence of its kind is worded from. */
export type RunReviewSays = {
  kind?: string | null; files: number; parts: number; unchecked: number; turns: number; branch?: string | null; copy?: string | null;
  plan?: number | null; system?: string | null; incomplete: number; coverage?: string | null;
};

/** What `RUN_REVIEW` answers: the run's review, or `refused` (`not-run`) where none is bound here with a review. */
export type RunReviewAnswer = {
  run: string | null; refused?: string; node?: string; record?: ReviewRecord | null; state?: string; standing?: number; since?: string | null;
  by?: string | null; words?: string | null; says?: RunReviewSays; before?: RunReviewStep[]; presses?: string[];
  newer?: { attempt: number; revision: string; changed: string[] } | null; follows?: RunReviewStep[]; ended?: string | null;
};

/** What `RUN_REVIEW_FILE` answers: the file's text at the record's revision, or why it is not drawn (`moved`, `not-row`, `not-kept`, `not-run`). */
export type RunReviewFileAnswer = { run: string; path: string; side?: string; attempt?: number; revision?: string; text?: string | null; refused?: string | null };

/**
 * A run's review at `attempt`, 0 for its newest record. Desktop-only: the records are this machine's. Told by event, so the page shows what
 * it held until the driver's read comes.
 */
export const useRunReview = (run: string | null, attempt = 0, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  return useQuery({
    queryKey: keys.runReview(run ?? '', attempt),
    queryFn: () => askHeld('RUN_REVIEW', client, keys.runReview(run ?? '', attempt), { run, session: null, attempt }) as Promise<RunReviewAnswer>,
    enabled: isAvailable && enabled && !!run,
    staleTime: 0,
  });
};
