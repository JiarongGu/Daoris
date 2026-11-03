import type { ConvertApplied, ConvertChoices, ConvertPlanned, ConvertUndoPlan } from './convertPlan';

// The conversion's plan as `CONVERT_PLAN` answers it, for the review's tests and stories (WORKFLOWCLEAN1g-i): a home with a steps workflow, a
// `handed` one shared by two places, a place on Current and a bound run, whose old rules the plan removes.

const hash = (text: string) => {
  let value = 11;
  for (const each of text) value = (value * 31 + each.charCodeAt(0)) >>> 0;
  return value.toString(16).padStart(8, '0').repeat(8);
};

export const planOf = (choices: ConvertChoices): ConvertPlanned => ({
  digest: hash(JSON.stringify(choices)),
  empty: false,
  choices,
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

export const NOTHING: ConvertPlanned = {
  ...planOf({ composed: 'none', landAutomatic: 'review' }),
  empty: true, saves: [], uses: [], defaultWorkflow: null, policies: [], removals: [], kept: [], lowers: [],
  census: { ended: true, versions: [], places: [], runs: [], leftovers: [] },
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
