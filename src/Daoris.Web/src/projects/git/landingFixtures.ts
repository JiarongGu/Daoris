import type { MilestoneEntry, MilestoneUnit } from '../../work/head/milestone';
import type { TraceAnswer, TraceBranch, TraceDue } from '../../work/head/trace';

// A landed branch's landing as the trace and the work's milestones answer it (FLOWCHART1c): the fixtures `landingLife.test.ts`, the
// branch page's tests and its stories draw. Neutral names only: a repository `engine`, sessions `s2`..`s4`, a placeholder address.

const at = (minute: number) => new Date(Date.UTC(2026, 9, 3, 10, minute)).toISOString();

const entry = (unit: string, kind: string, resource: string, role: string, disposition: string, detail: string, over: Partial<MilestoneEntry> = {}): MilestoneEntry => ({
  unit, kind, resource, role, disposition, detail, ...over,
});

/**
 * A landing that carries three sessions' work: due at its quest's done, held for the person's yes three times, then landed and pushed
 * by `github`, whose pull request completed; moved on once by session `s3` and three times by `s4`.
 */
export const SHARED_BRANCH: TraceBranch = {
  branch: 'review/q6-asset-cache', tip: 'a19c5b0000000000000000000000000000000000', at: at(5), line: 'main', from: '9be03d0000000000',
  plugin: 'github', pushed: true, pullRequest: 'https://example.test/pull/42', acceptedBy: 'auto',
  rule: { plugin: 'github', autoAccept: true, source: 'workspace' },
  review: { said: 'reviewed', environment: 'local', at: at(4) },
  pullRequestState: { state: 'completed', target: 'main', how: 'squash', at: at(50), plugin: 'github', askedAt: at(52) },
  session: 's2',
  advances: [
    { from: 'a19c5b0000', to: 'b2b2b20000', at: at(20), session: 's3', acceptedBy: 'person', unit: 'u-s3', quest: 'q7' },
    { from: 'b2b2b20000', to: 'c3c3c30000', at: at(30), session: 's4', acceptedBy: 'auto', unit: 'u-s4', quest: 'q8' },
    { from: 'c3c3c30000', to: 'd4d4d40000', at: at(32), session: 's4', acceptedBy: 'auto', unit: 'u-s4', quest: 'q8' },
    { from: 'd4d4d40000', to: 'e5e5e50000', at: at(34), session: 's4', acceptedBy: 'auto', unit: 'u-s4', quest: 'q8' },
  ],
  units: ['u-s2', 'u-s3', 'u-s4'],
};

/** Its own session's due entry: uncommitted work once, held three times for the person's yes, then landed. */
export const SHARED_DUE: TraceDue = {
  since: at(0), source: 'auto-landings', closed: at(5),
  tries: [
    { at: at(1), code: 'uncommitted', uncommitted: 2, tip: '9f8e7d6c5b4a' },
    { at: at(2), code: 'held' },
    { at: at(3), code: 'held' },
    { at: at(4), code: 'held' },
    { at: at(5), code: 'landed', branch: 'review/q6-asset-cache', commits: 3, tip: 'a19c5b00' },
  ],
};

const ended = (unit: string, session: string): MilestoneUnit => ({
  unit, repository: 'engine', branch: `daoris/${session}`, session,
  end: { state: 'ended', by: 'pull-request', on: 'main', at: at(55), said: 'its pull request completed' },
  result: [entry(unit, 'landing-branch', 'review/q6-asset-cache', 'result', 'kept', 'its work is on the line')],
  cleanup: {
    state: 'completed', waits: [],
    gone: [
      entry(unit, 'tree', `engine-${session}`, 'temporary', 'removed', 'its work is on the line', { proof: 'pull-request' }),
      entry(unit, 'session-branch', `daoris/${session}`, 'temporary', 'removed', 'a completed pull request carried it', { proof: 'carried' }),
    ],
  },
  recovery: [entry(unit, 'recovery-ref', `refs/daoris/recovery/${session}`, 'recovery', 'retained', 'kept until its date', { until: '2026-11-03T10:00:00Z' })],
  entries: [],
});

/** What the work's milestones say of the three units the landing carries: each ended by its pull request, cleaned up, recovery kept. */
export const SHARED_UNITS: MilestoneUnit[] = [ended('u-s2', 's2'), ended('u-s3', 's3'), ended('u-s4', 's4')];

/** A landing the person accepted after two holds and a wait for their review: its pull request open, its clean-up waiting. */
export const WAITING_BRANCH: TraceBranch = {
  branch: 'review/q4-input-map', tip: '2f7d100000000000000000000000000000000000', at: at(40), line: 'main',
  plugin: 'github', pushed: true, pullRequest: 'https://example.test/pull/43', acceptedBy: 'person',
  rule: { plugin: 'github', autoAccept: false, source: 'repository' },
  pullRequestState: { state: 'open', plugin: 'github', askedAt: at(45) },
  pullRequestAskFailed: { code: 'late', plugin: 'github', at: at(58) },
  session: 's5', advances: [], units: ['u-s5'],
};

export const WAITING_DUE: TraceDue = {
  since: at(30), source: 'auto-landings', closed: at(40),
  tries: [{ at: at(31), code: 'held' }, { at: at(33), code: 'held' }, { at: at(35), code: 'unreviewed' }],
};

export const WAITING_UNITS: MilestoneUnit[] = [{
  unit: 'u-s5', repository: 'engine', branch: 'daoris/s5', session: 's5',
  end: { state: 'handed', said: 'its pull request is open' },
  result: [entry('u-s5', 'landing-branch', 'review/q4-input-map', 'result', 'kept', 'its pull request is open')],
  cleanup: {
    state: 'pending',
    waits: [entry('u-s5', 'session-branch', 'daoris/s5', 'temporary', 'pending', 'its pull request is open')],
    gone: [],
  },
  recovery: [],
  entries: [],
}];

/** A landing as a driver from before FLOWCHART1c answers it: no session, no advances and no units, so the drawing says none were read. */
export const OLDER_BRANCH: TraceBranch = {
  branch: 'review/q2-shader-cache', tip: '0e4b9c0000000000000000000000000000000000', at: at(10), line: 'main', plugin: 'github', pushed: false,
};

/** The trace's answer for a session whose landing names `branch`, with its due entry: what the branch page reads. */
export const traceAnswerOf = (session: string, branch: TraceBranch | null, due: TraceDue | null = null, missing: string | null = null): TraceAnswer => ({
  unread: [],
  chain: {
    kind: 'session', id: session, unread: [],
    links: [{
      kind: 'session',
      session: {
        id: session, source: 'sessions', kind: 'driven', state: { state: 'completed' }, agent: { adapter: 'claude-code' },
        landing: { source: 'landings', missing, branches: branch ? [branch] : [], due },
      },
    }],
  },
});
