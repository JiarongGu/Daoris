import type { ConvertApplied, ConvertChoices, ConvertPlanned, ConvertUndoPlan, WorkflowsPlan } from './convertPlan';

// The conversion's plan as `CONVERT_PLAN` answers it, for the review's tests and stories (WORKFLOWCLEAN1g-i, ONEWAY1ae): a home whose workflow part
// holds a steps workflow, a `handed` one shared by two places, a place on Current and a bound run, whose old rules the plan removes; and the same
// home with a second part that clears finished history and a part whose service did not answer.

const hash = (text: string) => {
  let value = 11;
  for (const each of text) value = (value * 31 + each.charCodeAt(0)) >>> 0;
  return value.toString(16).padStart(8, '0').repeat(8);
};

const workflowsOf = (choices: ConvertChoices): WorkflowsPlan => ({
  saves: [
    { id: 'release', name: 'Release', mode: 'append', shape: 'steps', said: 'its steps become a workflow of nodes', base: 2, places: ['engine'] },
    { id: 'merge-after-accept', name: 'Merge after accept', mode: 'append', shape: 'handed', said: 'its `handed` landing becomes `land merge` with your review', base: 1, places: ['engine', 'site'] },
    { id: 'site-workflow', name: 'site-workflow', mode: 'new', shape: 'current', said: 'its rules are drawn as a workflow of its own', base: null, places: ['site'] },
  ],
  uses: [
    { place: 'engine', isWorkspace: false, workflow: 'release', kind: null, said: '`engine` follows `release`' },
    { place: 'site', isWorkspace: false, workflow: 'site-workflow', kind: null, said: '`site` follows `site-workflow`' },
  ],
  defaultWorkflow: 'merge-after-accept',
  policies: [{ place: 'engine', isWorkspace: false }],
  removals: [{ place: 'site', isWorkspace: false, keys: ['landings', 'tidy'] }, { place: 'aurora', isWorkspace: true, keys: ['workspaceLandings'] }],
  kept: [{ code: 'work-cycle', subject: 'archive', said: '`archive` stays on Current: its quests close by its work cycle, which a saved workflow does not run yet.' }],
  lowers: choices.landAutomatic === 'none' ? ['`release` lands with no review, as it was saved; it never landed, so nothing was lowered before.'] : [],
  census: {
    ended: false,
    versions: [{ workflow: 'release', version: 2, shape: 'steps' }],
    places: [
      { place: 'engine', isWorkspace: false, workspace: 'aurora', shapes: ['landing'], workCycle: false, openUnits: false },
      { place: 'site', isWorkspace: false, workspace: 'aurora', shapes: ['landing', 'tidy'], workCycle: false, openUnits: false },
      { place: 'archive', isWorkspace: false, workspace: 'aurora', shapes: [], workCycle: true, openUnits: false },
    ],
    runs: [{ run: 'r-41', repository: 'engine', workflow: 'release', version: 2, engine: 'steps', door: 'Finish or stop the run in Sessions.' }],
    leftovers: [{ place: 'site', isWorkspace: false, keys: ['landings'], why: 'a Current unit is still open there, so its rule is offered for removal once it ends.' }],
  },
});

export const planOf = (choices: ConvertChoices): ConvertPlanned => {
  const digest = hash(JSON.stringify(choices));
  return {
    digest,
    empty: false,
    ended: false,
    count: 5,
    choices,
    parts: [{ part: 'workflows', digest, empty: false, count: 5, ended: false, lines: [], clears: [], workflows: workflowsOf(choices) }],
    unread: [],
  };
};

const WORKFLOWS_EMPTY: WorkflowsPlan = {
  ...workflowsOf({ composed: 'none', landAutomatic: 'review' }),
  saves: [], uses: [], defaultWorkflow: null, policies: [], removals: [], kept: [], lowers: [],
  census: { ended: true, versions: [], places: [], runs: [], leftovers: [] },
};

export const NOTHING: ConvertPlanned = {
  ...planOf({ composed: 'none', landAutomatic: 'review' }),
  empty: true, ended: true, count: 0,
  parts: [{ part: 'workflows', digest: hash('nothing'), empty: true, count: 0, ended: true, lines: [], clears: [], workflows: WORKFLOWS_EMPTY }],
};

/** The workflow part beside a notes part that clears finished history, and a quests part whose service did not answer. */
export const PARTED: ConvertPlanned = {
  ...planOf({ composed: 'none', landAutomatic: 'review' }),
  digest: hash('parted'),
  count: 9,
  parts: [
    ...planOf({ composed: 'none', landAutomatic: 'review' }).parts,
    {
      part: 'notes', digest: hash('notes'), empty: false, count: 4, ended: false, lines: ['  clear  4 finished notes from before codes'],
      clears: [{ said: 'finished notes from before codes', count: 4, from: '2026-03-01T00:00:00Z', to: '2026-06-30T00:00:00Z', bytes: 18432 }],
    },
  ],
  unread: [{ part: 'quests', said: 'the service did not answer' }],
};

export const APPLIED: ConvertApplied = {
  done: true,
  refused: null,
  message: 'converted: 3 workflows saved, 2 places chosen, 3 rules removed',
  digest: planOf({ composed: 'none', landAutomatic: 'review' }).digest,
  operations: [
    { id: 'save:release', kind: 'workflow-save', said: 'a new version of `release`: its steps become a workflow of nodes', outcome: 'done', note: null },
    { id: 'save:site-workflow', kind: 'workflow-save', said: 'a new workflow `site-workflow`', outcome: 'failed', note: 'the save was refused: a node has no door.' },
    { id: 'configuration', kind: 'config', said: 'the choices, the default workflow and the removed rules written', outcome: 'done', note: null },
  ],
  parts: [{ part: 'workflows', digest: planOf({ composed: 'none', landAutomatic: 'review' }).digest, outcome: 'done', said: null, operations: [] }],
};

/** A receipt whose workflow part is done and whose quests part was not read at Apply: failed, pressed again or left. */
export const FAILED_PART: ConvertApplied = {
  done: false,
  refused: null,
  message: 'the conversion is not done: `quests` (failed: not read: the service did not answer)',
  digest: PARTED.digest,
  operations: [{ id: 'configuration', kind: 'configuration', said: 'the choices, in one change', outcome: 'done', note: null }],
  parts: [
    { part: 'workflows', digest: hash('w'), outcome: 'done', said: null, operations: [{ id: 'configuration', kind: 'configuration', said: 'the choices, in one change', outcome: 'done', note: null }] },
    { part: 'quests', digest: hash('q'), outcome: 'failed', said: 'not read: the service did not answer', operations: [] },
  ],
};

export const MOVED: ConvertApplied = {
  done: false,
  refused: 'moved',
  message: 'the plan you read no longer holds: a rule or a workflow moved since, so nothing was written. Plan it again (now `0a1b2c3d4e5f`).',
  digest: planOf({ composed: 'none', landAutomatic: 'review' }).digest,
  operations: [],
};

export const UNDO_OFFERED: ConvertUndoPlan = {
  undo: true,
  digest: 'f'.repeat(64),
  refusal: null,
  lines: ['the landing rule of `site`', 'the home\'s default workflow, taken away', 'the choice of `release` at `engine`, put back to Current'],
};

export const UNDO_REFUSED: ConvertUndoPlan = {
  undo: true, digest: null, refusal: 'the conversion cannot be undone: a rule it wrote has moved since.', lines: [],
};

export const UNDONE: ConvertApplied = {
  done: true,
  refused: null,
  message: 'the conversion is undone; the versions it saved stay saved, unchosen',
  digest: UNDO_OFFERED.digest!,
  operations: [{ id: 'configuration', kind: 'config', said: 'the removed rules put back', outcome: 'done', note: null }],
};
