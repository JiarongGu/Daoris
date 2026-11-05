import type { WorkflowStep } from './current';
import type { RunLine, RunStep, WorkflowRun, WorkflowRunAnswer } from './run';

// What `WORKFLOW_RUN { line }` answers, for the stories and the tests (FABRIC-PIPELINE1d): a repository's verifications at each verdict a
// person meets, each a verification pipeline's one check where `LineRunReader` would put it from its record and its newest result
// (`LineRunReaderTests`' rows).

const AT = '2026-10-31T09:00:00Z';
const COMMIT = 'fedcba9876543210fedcba9876543210fedcba98';

const CHECK: WorkflowStep = {
  id: 'verify', kind: 'check', participation: 'automatic', executor: 'daoris', press: null,
  source: { rule: 'workflow', level: 'repository' }, settings: {}, runtime: 'built', limit: null,
};

/** A person's verification whose check stands as `state` and `detail` say, its line as `line` says. */
export const verification = (
  state: string, detail: string, line: Partial<RunLine> & Pick<RunLine, 'verdict'>, facts: Partial<RunStep> = {},
): WorkflowRun => {
  const run = line.run ?? 'v-0123456789abcdef';
  return {
    repository: 'engine',
    workspace: 'aurora',
    quests: [],
    at: state === 'done' ? null : 'verify',
    workflow: { version: 'gph-0a2b4c6d8e0f1a2b', startHolds: [], limits: {}, id: 'verify', boundVersion: 1, route: { level: 'person' } },
    steps: [{ step: CHECK, state, detail, node: 'check', ...facts }],
    graph: { run, entries: ['verify'], end: 'verify', wires: [], judged: true, unread: false, ended: null },
    line: { run, commit: COMMIT, branch: 'feature/header', level: 'person', at: AT, gates: [], ...line },
  };
};

/** Every gate passed at its commit. */
export const VERIFIED = verification('done', 'reached', {
  verdict: 'verified',
  gates: [{ name: 'build', verdict: 'pass', exit: 0, wallSeconds: 42 }, { name: 'web-unit', verdict: 'pass', exit: 0, wallSeconds: 95.4 }],
});

/** A gate failed, named with its exit. */
export const FAILED_GATE = verification('failed', 'failed', {
  run: 'v-1111111111111111', verdict: 'failed', gate: 'web-unit', branch: null,
  gates: [{ name: 'build', verdict: 'pass', exit: 0, wallSeconds: 40 }, { name: 'web-unit', verdict: 'fail', exit: 1, wallSeconds: 61 }],
}, { gate: 'web-unit' });

/** Its outcome could not be established, held for the person: *Run again* and *Not now*. */
export const UNKNOWN_HELD = verification('not-known', 'unknown', { run: 'v-2222222222222222', verdict: 'unknown', why: 'bound' }, {
  code: 'bound', held: { unit: 'run:v-2222222222222222', attempt: 1, words: ['again', 'stop'], resolution: null },
});

/** Its gates running now. */
export const RUNNING = verification('working', 'active', { run: 'v-3333333333333333', verdict: 'running' });

/** Waiting its turn behind another heavy run. */
export const WAITING = verification('working', 'ready', { run: 'v-4444444444444444', verdict: 'waiting', branch: 'release/1.4' });

/** Left for now by the person. */
export const NOT_NOW = verification('stopped', 'unknown', { run: 'v-5555555555555555', verdict: 'not-now', why: 'bound' }, {
  held: { unit: 'run:v-5555555555555555', attempt: 1, words: [], resolution: 'stop' },
});

export const lineOf = (...runs: WorkflowRun[]): WorkflowRunAnswer => ({ runs, problem: null });
