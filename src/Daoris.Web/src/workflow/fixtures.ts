import type { CurrentWorkflow, WorkflowStart, WorkflowStep } from './current';

// What `WORKFLOW_CURRENT` answers, for the stories and the tests: each step a cell of the driver's shared table
// (`Daoris.Desktop.Driver.Tests/fixtures/workflow-current.json`, WORKFLOW1a), its rows renamed to this suite's
// repositories, so a story cannot draw a step the derivation would never make.

/** The driver's words for each limit, as the table holds them (`WorkflowLimits.Table`). */
export const LIMITS: Record<string, string> = {
  'opinion-partial': "Partial: work here lands only once another agent's reading of it is settled, or you go on without one by "
    + '`daoris-driver opinion`; the review does not draw it yet, and no task chooses its own reviewer yet.',
  'look-partial': "Partial: work here lands only once you say it is reviewed or skip the review, and a set-up step's build is "
    + "shown in Daoris's browser from its session's end until then; no set-up step is composed for you yet, and nothing runs a "
    + 'process of its own for one.',
  'plugin-unready': 'Its plugin cannot land work on this machine now: it is not installed, is switched off, contributes nothing, or '
    + 'speaks on no `work/land` point. `daoris plugin list` says which.',
  'pull-request-unread': 'Its plugin answers no `work/state`, so Daoris never reads whether the pull request was merged or abandoned.',
  'pull-request-at-clean-up': 'Its state is asked of its plugin only when branches are cleaned up, never while someone waits on it.',
  'go-ahead-partial': 'Partial: a session asks you for a go-ahead as it needs one; a step that raises one at its place and waits '
    + 'on your answer is not built yet.',
};

const limits = (steps: WorkflowStep[]) =>
  Object.fromEntries(steps.flatMap((step) => (step.limit ? [[step.limit, LIMITS[step.limit] ?? step.limit]] : [])));

/** The work, always first; with the standing answer where one is set. */
export const WORK = (standing: string | null = null): WorkflowStep => ({
  id: 'work', kind: 'work', participation: 'agent', executor: 'agent', press: null,
  source: { rule: null, level: 'default' }, settings: { standing }, runtime: 'built', limit: null,
});

/** Daoris's default landing: merged into the line once the person accepts. */
export const MERGE_YOU_ACCEPT: WorkflowStep = {
  id: 'landing', kind: 'landing', participation: 'you', executor: 'daoris', press: 'accept',
  source: { rule: 'landing', level: 'default' },
  settings: { form: 'merge', accept: 'you', pattern: null, plugin: null, tidy: false }, runtime: 'built', limit: null,
};

/** The workspace's second opinion before landing and between steps, its recheck off: in part, since XAGENT1f's gate. */
export const OPINION: WorkflowStep = {
  id: 'opinion', kind: 'opinion', participation: 'agent', executor: 'agent', press: null,
  source: { rule: 'opinion', level: 'workspace' },
  settings: { reviewers: ['codex-acp', 'dsh'], on: ['landing', 'steps'], required: false, recheck: false },
  runtime: 'partial', limit: 'opinion-partial',
};

/** The repository's required review: the person's look in `dev`, in part. */
export const LOOK: WorkflowStep = {
  id: 'look', kind: 'look', participation: 'agent-and-you', executor: 'agent', press: 'reviewed',
  source: { rule: 'review', level: 'repository' }, settings: { environment: 'dev' }, runtime: 'partial', limit: 'look-partial',
};

/** The workspace's branch rule, accepted automatically and tidied, its plugin pushing and opening a pull request. */
export const BRANCH_AUTOMATIC: WorkflowStep = {
  id: 'landing', kind: 'landing', participation: 'automatic', executor: 'daoris', press: null,
  source: { rule: 'landing', level: 'workspace' },
  settings: { form: 'branch', accept: 'automatic', pattern: 'work/{quest}-{slug}', plugin: 'example.pull-request', tidy: true },
  runtime: 'built', limit: null,
};

/** The pull request that plugin opens, asked after only when branches are cleaned up. */
export const PULL_REQUEST: WorkflowStep = {
  id: 'pull-request', kind: 'pull-request', participation: 'you', executor: 'plugin', press: 'merge',
  source: { rule: 'landing', level: 'workspace' }, settings: { plugin: 'example.pull-request' },
  runtime: 'partial', limit: 'pull-request-at-clean-up',
};

/**
 * A named version's go-ahead after the landing (FLOWCHART1d; the workflow design §3.6, `WorkflowKindTable`'s `go-ahead` row): the
 * person's yes to release to prod, its refusal waiting for them rather than stopping the run. In part: no step raises one yet.
 */
export const GO_AHEAD_WAIT: WorkflowStep = {
  id: 'release', kind: 'go-ahead', participation: 'you', executor: 'daoris', press: null,
  source: { rule: 'workflow', level: 'task' }, settings: { act: 'release to prod', on: 'ask', refused: 'wait' },
  runtime: 'partial', limit: 'go-ahead-partial',
};

/**
 * A check on the pull request a newer driver may answer (FLOWCHART1d; the workflow design §3.3, §3.7): this build runs none, so its
 * kind has no words here, and what follows its failure is a failure path: sent back once, then you.
 */
export const CHECK_SEND_BACK: WorkflowStep = {
  id: 'checks', kind: 'check', participation: 'automatic', executor: 'plugin', press: null,
  source: { rule: 'workflow', level: 'task' }, settings: { plugin: 'example.checks', checks: 'required', failed: 'send-back' },
  runtime: 'declared', limit: null,
};

const current = (fields: Omit<CurrentWorkflow, 'limits'>): CurrentWorkflow => ({ ...fields, limits: limits(fields.steps) });

/** Nothing set anywhere: the work, then a merge the person accepts (design §2.8). */
export const NOTHING_SET = current({
  repository: 'engine', workspace: 'default', registered: true, startHolds: [],
  steps: [WORK(), MERGE_YOU_ACCEPT], version: '06e90f358edc',
});

/**
 * Everything at once: a plugin that may hold a start, the standing answer, the workspace's second opinion, the
 * repository's look, an automatic landing on a branch and the pull request its plugin opens.
 */
export const FULL = current({
  repository: 'engine', workspace: 'aurora', registered: true, startHolds: ['example.hold-by-title'],
  steps: [WORK('dev writes allowed; prod only on a yes'), OPINION, LOOK, BRANCH_AUTOMATIC, PULL_REQUEST],
  version: '4740fb017006',
});

/** A branch rule whose plugin is not on this machine: the landing and its pull request each say it is not ready. */
export const PLUGIN_UNREADY = current({
  repository: 'engine', workspace: 'aurora', registered: true, startHolds: [],
  steps: [
    WORK(),
    {
      ...BRANCH_AUTOMATIC, participation: 'you', press: 'accept', limit: 'plugin-unready',
      settings: { ...BRANCH_AUTOMATIC.settings, accept: 'you', tidy: false },
    },
    { ...PULL_REQUEST, limit: 'plugin-unready' },
  ],
  version: '24bf4508996f',
});

/** A line's tip and the declaration's blob there, as the driver reads them (FABRIC-PIPELINE1a). */
export const ACCEPTED_BLOB = '3b18e512dba79e4c8300dd08aeb37f8e728b8dad';
export const CHANGED_BLOB = '0e5751c026e543b2e8ab2eb06099daa1d1e5df47';

/**
 * The start node (FABRIC-PIPELINE1a, the pipeline design §1.3): nothing names a workflow by default, Documentation goes to its own
 * workflow by the workspace's choice, and the work that lands is verified by `web-verify`, its gates accepted at the line's tip.
 */
export const START: WorkflowStart = {
  routes: [
    { kind: null, label: null, paths: [], workflow: 'current', level: 'current' },
    {
      kind: 'docs', label: 'Documentation', paths: ['docs/**', '**/*.md'], workflow: 'docs-to-pr', level: 'workspace-kind',
      name: 'Documentation to a pull request',
    },
  ],
  landed: {
    workflow: 'web-verify', level: 'repository-landed', gates: ACCEPTED_BLOB, name: 'Verify the line', state: 'accepted',
    tip: { line: 'main', commit: 'aa11bb22cc33dd44ee55ff6677889900aabbccdd', gates: ACCEPTED_BLOB },
  },
  verifications: [{ id: 'web-verify', name: 'Verify the line' }],
};

/** The declaration at the tip changed since the person accepted it: nothing runs until they accept it again. */
export const GATES_CHANGED: WorkflowStart = {
  ...START,
  landed: {
    ...START.landed, state: 'changed', tip: { ...START.landed.tip!, gates: CHANGED_BLOB },
    declaration: '{\n  "gates": [{ "name": "suite", "run": "npm test", "heavy": true }]\n}',
  },
};

/** No landed route anywhere: the work that lands is not verified, today's. */
export const NO_VERIFICATION: WorkflowStart = {
  ...START, landed: { workflow: 'current', level: 'current', gates: null, state: 'none' }, verifications: [],
};

/** Everything at once, with its start node first. */
export const ROUTED = current({ ...FULL, start: START });

/** A newer driver's check after the pull request, and its failure path (FLOWCHART1d). */
export const WITH_A_FAILURE_PATH = current({ ...FULL, steps: [...FULL.steps, CHECK_SEND_BACK], version: '5d1c0e7a92b4' });

/**
 * A workspace's Current, read from its rules with nothing set for any repository: its opinion, its look in `dev`, and a
 * branch for the person to push. `engine` sets rules of its own; `game` follows this.
 */
export const WORKSPACE = current({
  repository: null, workspace: 'aurora', startHolds: [],
  steps: [
    WORK(),
    { ...OPINION, settings: { reviewers: ['codex-acp'], on: ['landing'], required: true, recheck: true } },
    { ...LOOK, source: { rule: 'review', level: 'workspace' } },
    {
      ...MERGE_YOU_ACCEPT, source: { rule: 'landing', level: 'workspace' },
      settings: { form: 'branch', accept: 'you', pattern: 'work/{quest}', plugin: null, tidy: false },
    },
  ],
  version: '9c9f0df1dbed',
  repositories: [{ repository: 'engine', own: true }, { repository: 'game', own: false }],
});
