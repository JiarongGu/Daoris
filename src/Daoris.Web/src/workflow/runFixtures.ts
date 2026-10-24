import { BRANCH_AUTOMATIC, GO_AHEAD_WAIT, LIMITS, LOOK, MERGE_YOU_ACCEPT, OPINION, PULL_REQUEST, WORK } from './fixtures';
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
 * review, where the gate's presses stand beside *Accept…*; the attended session's own run draws the gate under the step, *Go on
 * anyway…* among its presses (XAGENT1g).
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

/**
 * A named version's run (FLOWCHART1d): the work done, the landing waiting for the person's accept, then a go-ahead to release to
 * prod whose refusal waits for them, its failure path drawn; a second opinion passed over where nothing asked for one, and the look
 * the person skipped, each saying who passed it over in its own words.
 */
export const GO_AHEAD_REFUSAL = run([
  at(WORK(), 'done', 'finished', { session: 's1', quest: 'q1', agent: 'claude-code', at: AT, count: 1, of: 1 }),
  at(OPINION, 'skipped', 'none'),
  at(LOOK, 'skipped', 'skip', { quest: 'q1', words: 'docs only', at: AT }),
  at(MERGE_YOU_ACCEPT, 'waiting-on-you', 'accept', { session: 's1', at: AT }),
  at(GO_AHEAD_WAIT, 'not-reached', ''),
], { workflow: { version: '5d1c0e7a92b4', startHolds: [], limits: limitsOf([at(OPINION, '', ''), at(LOOK, '', ''), at(GO_AHEAD_WAIT, '', '')]), id: 'release', boundVersion: 2 } });

/** A graph run's node's cell, as `WorkflowGraphRuns.Derive` reads a node as a step (FABRIC-CUTOVER1e): bound by the version, run whole. */
const nodeCell = (
  id: string, kind: string, participation: WorkflowStep['participation'], executor: WorkflowStep['executor'],
  press: WorkflowStep['press'] = null, settings: WorkflowStep['settings'] = {},
): WorkflowStep => ({ id, kind, participation, executor, press, source: { rule: 'workflow', level: 'repository' }, settings, runtime: 'built', limit: null });

const BRANCH = 'daoris/q1-fix-the-header';

/**
 * A run bound to a graph version, drawn from its record (FABRIC-CUTOVER1e): the claim reached on its quest, then a fork, the build observed
 * on the run's branch while the lint is reached there, joined before the second opinion, not reached behind the join (FABRIC-CUTOVER1d begins
 * it as any node once its wires let it start); the landing and the deploy after it not reached; a failure of the join stops the run.
 */
export const GRAPH_RUN = run([
  at(nodeCell('claimed', 'work', 'agent', 'agent'), 'done', 'reached', { node: 'claimed', on: 'quest', quest: 'q1' }),
  at(nodeCell('build', 'acme.ci/built', 'automatic', 'plugin', null, { plugin: 'acme.ci' }), 'working', 'active',
    { node: 'acme.ci/built', on: 'branch', branch: BRANCH, commit: COMMIT, session: 's1' }),
  at(nodeCell('lint', 'acme.ci/linted', 'automatic', 'plugin', null, { plugin: 'acme.ci' }), 'done', 'reached',
    { node: 'acme.ci/linted', on: 'branch', branch: BRANCH, commit: COMMIT, session: 's1' }),
  at(nodeCell('both', 'all', 'automatic', 'daoris'), 'not-reached', 'blocked', { node: 'all' }),
  at(nodeCell('halt', 'stop', 'automatic', 'daoris'), 'not-reached', 'blocked', { node: 'stop' }),
  at(nodeCell('opinion', 'opinion', 'agent', 'agent'), 'not-reached', 'blocked', { node: 'opinion', on: 'branch', branch: BRANCH, session: 's1' }),
  at(nodeCell('handed', 'landing', 'you', 'daoris', 'accept', { form: 'branch', accept: 'you' }), 'not-reached', 'blocked',
    { node: 'handed', on: 'branch', branch: BRANCH, session: 's1' }),
  at(nodeCell('deployed', 'acme.deploy/deployed', 'automatic', 'plugin', null, { plugin: 'acme.deploy' }), 'not-reached', 'blocked',
    { node: 'acme.deploy/deployed' }),
], {
  at: 'build',
  workflow: { version: 'gph-5d1c0e7a92b4f0e1', startHolds: [], limits: {}, id: 'docs', boundVersion: 1, route: { level: 'repository' } },
  graph: {
    run: 'q1', entries: ['claimed'], end: 'deployed',
    wires: [
      { from: 'claimed', to: 'build', kind: 'after' }, { from: 'claimed', to: 'lint', kind: 'after' },
      { from: 'build', to: 'both', kind: 'after' }, { from: 'lint', to: 'both', kind: 'after' }, { from: 'both', to: 'halt', kind: 'failed' },
      { from: 'both', to: 'opinion', kind: 'after' }, { from: 'opinion', to: 'handed', kind: 'after' }, { from: 'handed', to: 'deployed', kind: 'after' },
    ],
    quest: 'q1', branch: BRANCH, judged: true, unread: false, ended: null,
  },
});

/**
 * A graph run whose go-ahead waits on the person, with a look the chain's choice added beside its graph (D190 point 7), which holds the
 * landing as D154's gate does; the version's own graph holds no look.
 */
export const GRAPH_RUN_ADDED_LOOK = run([
  at(nodeCell('claimed', 'work', 'agent', 'agent'), 'done', 'reached', { node: 'claimed', on: 'quest', quest: 'q1' }),
  at(nodeCell('release', 'go-ahead', 'you', 'daoris', null, { act: 'release to prod' }), 'waiting-on-you', 'active', { node: 'go-ahead', words: 'release to prod' }),
  at(LOOK, 'waiting-on-you', 'not-shown', { added: 'chain', environment: 'dev' }),
  at(nodeCell('handed', 'landing', 'you', 'daoris', 'accept', { form: 'merge', accept: 'you' }), 'not-reached', 'blocked',
    { node: 'handed', on: 'branch', branch: BRANCH, session: 's1' }),
], {
  at: 'release',
  workflow: { version: 'gph-0a2b4c6d8e0f1a2b', startHolds: [], limits: {}, id: 'release', boundVersion: 3 },
  graph: {
    run: 'q1', entries: ['claimed'], end: 'handed',
    wires: [{ from: 'claimed', to: 'release', kind: 'after' }, { from: 'release', to: 'handed', kind: 'after' }],
    quest: 'q1', branch: BRANCH, judged: true, unread: false, ended: null,
  },
});

/**
 * A graph run whose go-ahead nobody raised (FABRIC-CUTOVER1f; the cutover design §5.2): the go-ahead under way offers the person's skip and
 * stop, the landing behind it not reached offers neither.
 */
export const GRAPH_RUN_GO_AHEAD = run([
  at(nodeCell('claimed', 'work', 'agent', 'agent'), 'done', 'reached', { node: 'claimed', on: 'quest', quest: 'q1' }),
  at(nodeCell('release', 'go-ahead', 'you', 'daoris', null, { act: 'release to prod' }), 'waiting-on-you', 'active',
    { node: 'go-ahead', words: 'release to prod', acts: ['skip', 'stop'] }),
  at(nodeCell('handed', 'landing', 'you', 'daoris', 'accept', { form: 'merge', accept: 'you' }), 'not-reached', 'blocked',
    { node: 'handed', on: 'branch', branch: BRANCH, session: 's1' }),
], {
  at: 'release',
  workflow: { version: 'gph-0a2b4c6d8e0f1a2b', startHolds: [], limits: {}, id: 'release', boundVersion: 3 },
  graph: {
    run: 'q1', entries: ['claimed'], end: 'handed',
    wires: [{ from: 'claimed', to: 'release', kind: 'after' }, { from: 'release', to: 'handed', kind: 'after' }],
    quest: 'q1', branch: BRANCH, judged: true, unread: false, ended: null,
  },
});

/** The same run once the person passed its go-ahead over: their word kept, which the driver's next look applies. */
export const GRAPH_RUN_GO_AHEAD_KEPT: WorkflowRun = {
  ...GRAPH_RUN_GO_AHEAD,
  steps: GRAPH_RUN_GO_AHEAD.steps.map((step) => step.step.id === 'release'
    ? { ...step, acts: null, asked: { node: 'release', act: 'skip', words: 'nobody raises it for this work', at: AT } }
    : step),
};

/** A graph run Daoris has not judged yet: every node not reached, standing at its entry; no session of it has a branch yet. */
export const GRAPH_RUN_UNJUDGED = run([
  at(nodeCell('claimed', 'work', 'agent', 'agent'), 'not-reached', 'blocked', { node: 'claimed', on: 'quest', quest: 'q1' }),
  at(nodeCell('handed', 'landing', 'you', 'daoris', 'accept', { form: 'merge', accept: 'you' }), 'not-reached', 'blocked', { node: 'handed' }),
], {
  at: 'claimed',
  session: null,
  workflow: { version: 'gph-0a2b4c6d8e0f1a2b', startHolds: [], limits: {}, id: 'release', boundVersion: 3 },
  graph: {
    run: 'q1', entries: ['claimed'], end: 'handed', wires: [{ from: 'claimed', to: 'handed', kind: 'after' }],
    quest: 'q1', branch: null, judged: false, unread: false, ended: null,
  },
});

/**
 * A chat's run (CHATFLOW1c, the chat-flow design §4.2, §4.4): its conversation between turns with two commits beyond the line, waiting for the
 * person's Accept; its second opinion passed over before CHATFLOW1d; and its checks, `light`, not yet enforced for a conversation.
 */
export const CHAT_RUN = run([
  at(WORK(), 'waiting-on-you', 'land', { session: 'c2', agent: 'claude-code', at: AT, count: 2 }),
  at(OPINION, 'waiting-on-you', 'chat-at-press', { session: 'c2', agent: 'codex-acp' }),
  at(MERGE_YOU_ACCEPT, 'waiting-on-you', 'accept', { session: 'c2', at: AT }),
], { ask: null, quests: [], session: 'c2', chat: { run: 'chat-c1', checks: 'light', enforced: false } });

/** An ask's work in two repositories, each its own run at its own step (design §3.8). */
export const SEVERAL = answerOf(
  IN_REVIEW,
  { ...PULL_REQUEST_OPEN, repository: 'game', quests: ['q3'], session: 's3' },
);
