import { BRANCH_AUTOMATIC, LIMITS, LOOK, MERGE_YOU_ACCEPT, OPINION, PULL_REQUEST, WORK } from './fixtures';
import type { RunStep, WorkflowRun, WorkflowRunAnswer } from './run';
import type { WorkflowStep } from './current';

// What `WORKFLOW_RUN` answers, for the stories and the tests (WORKFLOW1c): a run at each stage a person meets it, each step a cell
// of the driver's shared table (`fixtures.ts`) beside where `WorkflowRuns.Derive` would put it from the records named in its
// comment (`WorkflowRunTests`' rows), so a story draws what the driver reads.

const AT = '2026-10-09T09:00:00Z';
const COMMIT = '0123456789abcdef0123456789abcdef01234567';

const limitsOf = (steps: RunStep[]) =>
  Object.fromEntries(steps.flatMap(({ step }) => (step.limit ? [[step.limit, LIMITS[step.limit] ?? step.limit]] : [])));

/** A step where it stands, with what the record says of it. */
const at = (step: WorkflowStep, state: string, detail: string, facts: Partial<RunStep> = {}): RunStep => ({ step, state, detail, ...facts });

const run = (steps: RunStep[], fields: Partial<WorkflowRun> = {}): WorkflowRun => ({
  repository: 'engine',
  workspace: 'aurora',
  ask: 'a1',
  quests: ['q1'],
  session: 's1',
  at: steps.find((step) => !['done', 'skipped'].includes(step.state))?.step.id ?? null,
  workflow: { version: '06e90f358edc', startHolds: [], limits: limitsOf(steps) },
  steps,
  ...fields,
});

/** One run, as the shell answers a session's. */
export const answerOf = (...runs: WorkflowRun[]): WorkflowRunAnswer => ({ runs, problem: null });

/** Its agent at work on the quest (the table's *a session at work*): the landing not reached. */
export const WORKING = run([
  at(WORK(), 'working', 'agent', { session: 's1', quest: 'q1', agent: 'claude-code', at: AT, count: 0, of: 1 }),
  at(MERGE_YOU_ACCEPT, 'not-reached', ''),
]);

/** A go-ahead its session asked, waiting on the person (*a go-ahead a session asked*). */
export const WAITING_ON_YOU = run([
  at(WORK(), 'waiting-on-you', 'go-ahead', {
    session: 's1', quest: 'q1', agent: 'claude-code', at: AT, goAhead: 1, words: 'write the configuration to dev', count: 0, of: 1,
  }),
  at(MERGE_YOU_ACCEPT, 'not-reached', ''),
]);

/** The work done, and its set-up shown in `dev`, waiting for the person's look (*a set-up shown waits for your look*). */
export const IN_REVIEW = run([
  at(WORK(), 'done', 'finished', { session: 's1', quest: 'q1', agent: 'claude-code', at: AT, count: 1, of: 1 }),
  at(LOOK, 'waiting-on-you', 'shown', { session: 's2', quest: 'q2', environment: 'dev', commit: COMMIT, at: AT }),
  at(BRANCH_AUTOMATIC, 'not-reached', ''),
  at(PULL_REQUEST, 'not-reached', ''),
], { quests: ['q1', 'q2'], session: 's2' });

/**
 * Held for an opinion: its gate's `with-session` (XAGENT1f), Codex's three findings with the working session, which answers them
 * as its next turn (*findings with the working session wait on its agent*). The landing is not reached until the gate settles.
 */
export const HELD_FOR_OPINION = run([
  at(WORK(), 'done', 'finished', { session: 's1', quest: 'q1', agent: 'claude-code', at: AT, count: 1, of: 1 }),
  at(OPINION, 'waiting-on-agent', 'with-session', { session: 's1', agent: 'Codex (OpenAI)', commit: COMMIT, count: 3 }),
  at(MERGE_YOU_ACCEPT, 'not-reached', ''),
]);

/**
 * A dispute the gate holds on the person (*a dispute waits on you*): a `must` the working session did not fix. Its door opens the
 * review, where the gate's presses stand beside *Accept…*, and *Go on anyway…* is the terminal's until the review draws it.
 */
export const OPINION_DISPUTED = run([
  at(WORK(), 'done', 'finished', { session: 's1', quest: 'q1', agent: 'claude-code', at: AT, count: 1, of: 1 }),
  at(OPINION, 'waiting-on-you', 'disputed', { session: 's1', agent: 'Codex (OpenAI)', commit: COMMIT, count: 1 }),
  at(MERGE_YOU_ACCEPT, 'not-reached', ''),
]);

/** A settled opinion (*a settled opinion lets the landing go*): the landing waits for the person's accept. */
export const OPINION_SETTLED = run([
  at(WORK(), 'done', 'finished', { session: 's1', quest: 'q1', agent: 'claude-code', at: AT, count: 1, of: 1 }),
  at(OPINION, 'done', 'settled', { session: 'r1', agent: 'Codex (OpenAI)', commit: COMMIT, count: 3 }),
  at(MERGE_YOU_ACCEPT, 'waiting-on-you', 'accept', { session: 's1', at: AT }),
]);

/** Landed into its line by the person's accept (*an acceptance the session's record keeps*): finished. */
export const LANDED = run([
  at(WORK(), 'done', 'finished', { session: 's1', quest: 'q1', agent: 'claude-code', at: AT, count: 1, of: 1 }),
  at(MERGE_YOU_ACCEPT, 'done', 'merge', { session: 's1', at: AT }),
]);

/** Landed on a branch with no press, its plugin's pull request open and waiting for the person's merge. */
export const PULL_REQUEST_OPEN = run([
  at(WORK(), 'done', 'finished', { session: 's1', quest: 'q1', agent: 'claude-code', at: AT, count: 1, of: 1 }),
  at(BRANCH_AUTOMATIC, 'done', 'branch', { session: 's1', branch: 'work/q1-fix-the-header', code: 'auto', plugin: 'example.pull-request', at: AT }),
  at(PULL_REQUEST, 'waiting-on-you', 'merge', {
    session: 's1', branch: 'work/q1-fix-the-header', plugin: 'example.pull-request', pullRequest: 'https://example.test/pull/7', code: 'open', at: AT,
  }),
]);

/** Its pull request merged on the platform: finished. */
export const MERGED = run([
  at(WORK(), 'done', 'finished', { session: 's1', quest: 'q1', agent: 'claude-code', at: AT, count: 1, of: 1 }),
  at(BRANCH_AUTOMATIC, 'done', 'branch', { session: 's1', branch: 'work/q1-fix-the-header', code: 'auto', plugin: 'example.pull-request', at: AT }),
  at(PULL_REQUEST, 'done', 'merged', { session: 's1', pullRequest: 'https://example.test/pull/7', plugin: 'example.pull-request', at: AT }),
]);

/** Its last session failed (*a last session that failed*): the run stands at the work, red only there. */
export const FAILED = run([
  at(WORK(), 'failed', 'failed', { session: 's1', quest: 'q1', agent: 'claude-code', at: AT, count: 0, of: 1 }),
  at(MERGE_YOU_ACCEPT, 'not-reached', ''),
]);

/** An ask's work in two repositories, each its own run at its own step (design §3.8). */
export const SEVERAL = answerOf(
  IN_REVIEW,
  { ...PULL_REQUEST_OPEN, repository: 'game', quests: ['q3'], session: 's3' },
);
