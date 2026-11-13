import type { ReviewRecord, RunReviewAnswer } from './runReviewWire';

// Runs' reviews as RUN_REVIEW answers them, one per kind, with neutral sample names: the stories and the tests draw these.

const PARTS: ReviewRecord['parts'] = [
  {
    quest: 'q41', title: 'Tidy the budget notes', session: 's-41', at: '2026-11-12T08:00:00Z', unchecked: false,
    note: 'Rewrote the **summary** and dropped the stale rows.', requirements: [{ quote: 'The summary names each total', check: 'met' }],
  },
];

const record = (kind: string, extra: Partial<ReviewRecord> = {}): ReviewRecord => ({
  run: 'q41', node: 'review', attempt: 2, kind, revision: 'b7c1e09a44f2', at: '2026-11-12T08:01:00Z', place: 'budget-notes',
  reference: {}, parts: PARTS, rows: [], incomplete: [], coverage: 'whole', ...extra,
});

const answer = (kind: string, extra: Partial<RunReviewAnswer> = {}, recorded: Partial<ReviewRecord> = {}): RunReviewAnswer => ({
  run: 'q41', node: 'review', record: record(kind, recorded), state: 'active', standing: 2, since: '2026-11-12T08:01:00Z',
  says: { kind, files: recorded.rows?.length ?? 0, parts: 1, unchecked: 0, turns: 2, incomplete: 0 },
  before: [], presses: ['accept', 'changes', 'stop'], newer: null, supervisor: null,
  follows: [{ node: 'land', kind: 'land', state: 'blocked', attempt: 0, via: 'after', offers: [] }],
  ended: null, ...extra,
});

/** A folder worked in place: two files changed, one of them outside its turns. */
export const FOLDER_REVIEW = answer('folder', { follows: [] }, {
  rows: [
    { path: 'notes/summary.md', change: 'modified', before: 'a11', after: 'a12', marks: [] },
    { path: 'notes/old-rows.csv', change: 'deleted', before: 'c31', after: null, marks: ['changed-outside-its-turns'] },
  ],
});

/** A folder in a copy, brought back after the review. */
export const COPY_REVIEW = answer('copy', {
  follows: [{ node: 'return', kind: 'return', state: 'blocked', attempt: 0, via: 'after', offers: [] }],
}, {
  rows: [
    { path: 'chapters/one.md', change: 'modified', before: 'd01', after: 'd02', marks: ['changed-in-your-folder-too'] },
    { path: 'chapters/two.md', change: 'added', before: null, after: 'e01', marks: [] },
    { path: 'chapters/three.md', change: 'modified', before: 'f01', after: 'f02', marks: [] },
  ],
});

/** A system's plan of three operations, applied after the review. */
export const PLAN_REVIEW = answer('plan', {
  says: { kind: 'plan', files: 0, parts: 1, unchecked: 0, turns: 1, plan: 3, system: 'sample-dns', incomplete: 0 },
  follows: [{ node: 'apply', kind: 'apply', state: 'blocked', attempt: 0, via: 'after', offers: [] }],
  operations: [
    { resource: 'record/www', op: 'update', before: '10.0.0.4', after: '10.0.0.9' },
    { resource: 'record/api', op: 'create', before: null, after: '10.0.0.12' },
    { resource: 'record/old', op: 'delete', before: '10.0.0.2', after: null },
  ],
});

/** An answer that changed nothing. */
export const ANSWER_REVIEW = answer('answer', { follows: [] }, {
  parts: [{ ...PARTS[0], note: '## What I found\n\nThe totals agree; **nothing** needed changing.' }],
});

/** A branch's commits, landed after the review, with the supervisor's Accept held for its window. */
export const BRANCH_REVIEW = answer('branch', {
  says: { kind: 'branch', files: 0, parts: 2, unchecked: 1, turns: 0, branch: 'daoris/q41', incomplete: 0 },
  supervisor: {
    id: 'sv-1', session: 's-41', do: 'accept', state: 'held', attempt: 2, work: 'branch', revision: 'b7c1e09a44f2', onRecord: true,
    keptAt: '2026-11-12T08:02:00Z', goesOnAt: '2026-11-12T08:12:00Z', stateAt: '2026-11-12T08:02:00Z',
  },
}, {
  parts: [PARTS[0], { quest: 'q42', title: 'Check the totals', session: 's-42', unchecked: true, requirements: [], note: 'Checked.' }],
});

const PATCH = (path: string, from: string, to: string) => `diff --git a/${path} b/${path}\n--- a/${path}\n+++ b/${path}\n@@ -1,3 +1,3 @@\n line one\n-${from}\n+${to}\n line three\n`;

/** A branch at its review with its outputs: two commits, their patch per file, and the two parts' handbacks. */
export const BRANCH_OUTPUTS: RunReviewAnswer = {
  ...BRANCH_REVIEW,
  supervisor: null,
  outputs: [
    { id: 'handback:q41', type: 'handback', display: 'document', title: 'Tidy the budget notes', part: 'q41', digest: 'h1' },
    { id: 'handback:q42', type: 'handback', display: 'document', title: 'Check the totals', part: 'q42', digest: 'h2' },
    { id: 'changes', type: 'changes', display: 'change-set', title: null, digest: 'c1' },
  ],
  commits: {
    commits: [
      { sha: 'a1b2c3d4e5f6', author: 'Fixture', at: '2026-11-12T08:00:00Z', subject: 'Check the totals', files: ['notes/totals.md'] },
      { sha: 'f6e5d4c3b2a1', author: 'Fixture', at: '2026-11-12T07:00:00Z', subject: 'Tidy the summary', files: ['notes/summary.md'] },
    ],
    uncommitted: [],
  },
  diff: {
    base: '0e4b9c000000',
    truncated: null,
    files: [
      { path: 'notes/summary.md', status: 'modified', added: 1, removed: 1, patch: PATCH('notes/summary.md', 'old summary', 'new summary') },
      { path: 'notes/totals.md', status: 'modified', added: 1, removed: 1, patch: PATCH('notes/totals.md', 'total 10', 'total 12') },
    ],
  },
};

/** The same branch with r3 ready: a moved tip and a corrected handback, no file row changed. */
export const BRANCH_NEWER: RunReviewAnswer = {
  ...BRANCH_OUTPUTS, standing: 3, presses: [], newer: { attempt: 3, revision: 'c9d2f1', changed: [], outputs: ['changes', 'handback:q42'] },
};

/** A folder's review with a newer revision ready, two files changed since. */
export const NEWER_REVIEW = answer('folder', {
  standing: 3, presses: [], newer: { attempt: 3, revision: 'c9d2f1', changed: ['notes/summary.md', 'notes/new.md'] }, follows: [],
}, { rows: FOLDER_REVIEW.record!.rows });
