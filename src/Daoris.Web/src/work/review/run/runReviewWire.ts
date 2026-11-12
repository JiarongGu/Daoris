// A run's review, opened by its run: the record of one attempt as the driver kept it once, where the review stands now, and the steps
// before and after it, as `RUN_REVIEW` and `RUN_REVIEW_FILE` answer them. The page draws it and derives nothing; its words are the
// page's, worded from `says`.

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

/** The supervisor's newest decision on the attempt read, as `SESSION_SUPERVISOR`'s landings say it, and whether it names the record read. */
export type RunReviewSupervisor = {
  id: string; session: string; do: string; state: string; attempt: number; work?: string | null; revision?: string | null; onRecord: boolean;
  holder?: string | null; keptAt: string; goesOnAt: string; stateAt: string; why?: string | null;
};

/** One operation of a plan the record names: its resource, `create`, `update` or `delete`, and its value before and after. */
export type ReviewOperation = { resource: string; op: string; before?: string | null; after?: string | null };

/** What `RUN_REVIEW` answers: the run's review, or `refused` (`not-run`) where none is bound here with a review. */
export type RunReviewAnswer = {
  run: string | null; refused?: string; node?: string; record?: ReviewRecord | null; state?: string; standing?: number; since?: string | null;
  by?: string | null; words?: string | null; says?: RunReviewSays; before?: RunReviewStep[]; presses?: string[];
  newer?: { attempt: number; revision: string; changed: string[] } | null; supervisor?: RunReviewSupervisor | null; follows?: RunReviewStep[];
  ended?: string | null; operations?: ReviewOperation[] | null;
  /** A branch's or a checkout's changed files between the record's commits; null where the checkout is not on this machine. */
  files?: ReviewRow[] | null;
};

/** What `RUN_REVIEW_FILE` answers: the file's text at the record's revision, or why it is not drawn (`moved`, `not-row`, `not-kept`, `not-run`). */
export type RunReviewFileAnswer = { run: string; path: string; side?: string; attempt?: number; revision?: string; text?: string | null; refused?: string | null };
