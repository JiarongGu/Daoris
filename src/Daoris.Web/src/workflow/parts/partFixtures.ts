import type { WorkflowStep } from '../current';
import type { RunJoint, WorkflowRun } from '../run';
import { IN_REVIEW } from '../runFixtures';

// A quest across two repositories, its ask's plan in two parts: the app (`q7`, in `engine`) and its configuration (`q9`, in `settings`),
// the configuration starting on the app's `stage-ended`, and the tester's joint check over both parts' receipts on the app's run.

const task = (id: string, role: string): WorkflowStep => ({
  id, kind: 'task', participation: 'you', executor: 'daoris', press: null,
  source: { rule: 'workflow', level: 'workspace' }, settings: { role }, runtime: 'built', limit: null,
});

/** The configuration part, waiting on the app's `stage-ended`: none of its steps is handed yet. */
export const WAITING_PART: WorkflowRun = {
  ...IN_REVIEW,
  repository: 'settings',
  quests: ['q9'],
  at: 'configure',
  graph: {
    run: 'q9', entries: ['work'], end: 'configure', wires: [{ kind: 'after', from: 'work', to: 'configure' }], judged: true, unread: false,
    partWaits: [{ part: 'app', fact: 'stage-ended', quest: 'q7', state: 'waiting', message: 'Stage test waits on its approver.' }],
  },
  steps: [...IN_REVIEW.steps.slice(0, 1), { step: task('configure', 'config-changer'), state: 'working', detail: 'actor', role: 'config-changer', node: 'task' }],
};

const jointRun = (joint: RunJoint): WorkflowRun => ({
  ...IN_REVIEW,
  at: 'together',
  graph: { run: 'q7', entries: ['work'], end: 'together', wires: [{ kind: 'after', from: 'work', to: 'together' }], judged: true, unread: false },
  steps: [...IN_REVIEW.steps.slice(0, 1), { step: task('together', 'tester'), state: 'working', detail: 'actor', role: 'tester', node: 'task', joint }],
});

const APP_ENDED = { part: 'app', fact: 'stage-ended', quest: 'q7', state: 'reached', message: 'Stage test of Release-91 ended.' };

/** The joint check holding the app's receipt and waiting for the configuration's, which its run has not journaled yet. */
export const JOINT_HOLDING = jointRun({ parts: [APP_ENDED, { part: 'config', fact: 'config-written', quest: 'q9' }] });

/** The joint check whose test failed on the configuration part, sent back to that part's holder. */
export const JOINT_RETURNED = jointRun({
  parts: [APP_ENDED, { part: 'config', fact: 'config-written', quest: 'q9', state: 'reached', message: 'Setting store written for the test stage.' }],
  returned: { part: 'config', fact: 'joint-tested', quest: 'q9', state: 'failed', message: 'The app could not read its connection setting.' },
});
