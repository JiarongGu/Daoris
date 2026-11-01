import type { EditorRole, PaletteKind, WorkflowListAnswer, WorkflowPaletteAnswer, WorkflowSaveAnswer } from './answers';

// The step editor's answers as the driver gives them (WORKFLOWEDIT1b), for its page tests and its stories: a palette of the kinds the
// documents and release workflows hold with the Local release plugin's three steps, the defaults listed with their graphs, and roles.

const kind = (name: string, reach: string, more: Partial<PaletteKind> = {}): PaletteKind => ({
  kind: name, reach, reachBy: 'daoris', failureBy: null, personJudges: false, personSkips: false, mayEnd: true, evidenced: false, skipBy: null,
  options: [], leaves: ['after', 'data', 'trigger'], roleTakes: [], ...more,
});

const RELEASE = 'local-release/release@1';
const RECEIPT = 'local-release/receipt@1';

export const PALETTE: WorkflowPaletteAnswer = {
  kinds: [
    kind('claimed', 'observed', { reachBy: 'agent', mayEnd: false, options: [{ name: 'role', type: 'text', choices: [], required: false }], roleTakes: ['design', 'build'] }),
    kind('work.requests', 'observed'),
    kind('check', 'performed', { failureBy: 'daoris', mayEnd: false, leaves: ['after', 'data', 'trigger', 'failed', 'back'],
      options: [{ name: 'gates', type: 'names', choices: [], required: false }] }),
    kind('review', 'pressed', { reachBy: 'person', personJudges: true, leaves: ['after', 'data', 'trigger', 'rejected', 'back'] }),
    kind('land', 'performed', { reachBy: 'landing', failureBy: 'landing', evidenced: true, leaves: ['after', 'data', 'trigger', 'failed', 'back'],
      options: [{ name: 'method', type: 'choice', choices: ['merge', 'branch', 'push', 'pull-request', 'integrate'], required: false },
        { name: 'squash', type: 'switch', choices: [], required: false }] }),
    kind('line', 'observed'),
  ],
  wires: [
    { kind: 'after', eligibility: true, from: null, to: null },
    { kind: 'when', eligibility: true, from: [], to: null },
    { kind: 'back', eligibility: false, from: null, to: null },
  ],
  plugins: [{ plugin: 'local-release', steps: [
    { kind: 'local-release/find', version: 1, reach: 'performed', rejectable: false, skippable: true, inputs: {}, outputs: { release: RELEASE },
      evidence: RECEIPT, outcomes: ['release', 'none'], leaves: ['after', 'data', 'trigger', 'failed', 'when', 'back'] },
    { kind: 'local-release/release', version: 1, reach: 'performed', rejectable: false, skippable: true, inputs: { release: RELEASE },
      outputs: { release: RELEASE }, evidence: RECEIPT, outcomes: [], leaves: ['after', 'data', 'trigger', 'failed', 'back'] },
    { kind: 'local-release/publish', version: 1, reach: 'performed', rejectable: false, skippable: true, inputs: { release: RELEASE },
      outputs: {}, evidence: RECEIPT, outcomes: [], leaves: ['after', 'data', 'trigger', 'failed', 'back'] },
  ] }],
};

/** The documents default's graph, as `WORKFLOW_LIST` answers it. */
export const DOCUMENTS_GRAPH = {
  schemaVersion: 2, entries: ['work'], end: 'line',
  nodes: [
    { id: 'work', kind: 'claimed', version: 1, reach: 'observed' },
    { id: 'landing', kind: 'land', version: 1, reach: 'performed', options: { method: 'merge' } },
    { id: 'line', kind: 'line', version: 1, reach: 'observed' },
  ],
  wires: [{ id: 'a', kind: 'after', from: 'work', to: 'landing' }, { id: 'c', kind: 'after', from: 'landing', to: 'line' }],
};

export const LIST: WorkflowListAnswer = {
  workflows: [{ id: 'docs-release', name: 'Docs release', problem: null, versions: [1, 2], schema: 2, digest: 'ab', line: 'x', pipeline: null,
    chosen: [{ level: 'repository', name: 'engine', role: 'default', kind: null }] }],
  presets: [
    { id: 'documents', name: 'Documents', graph: true, line: 'The work merges into the line.', preset: true, body: DOCUMENTS_GRAPH,
      names: { en: 'Documents', zh: '文档' } },
    { id: 'pull-request-no-press', name: 'A pull request with no press', graph: false, line: 'y', preset: true, body: null, names: null },
  ],
};

export const ROLES: EditorRole[] = [
  { id: 'build', name: { en: 'Build', zh: '开发' }, takes: ['build'] },
  { id: 'opinion', name: { en: 'Second opinion', zh: '第二意见' }, takes: ['review'] },
  { id: 'designer', name: { en: 'Designer', zh: '设计师' }, takes: ['design'] },
];

const answer = (more: Partial<WorkflowSaveAnswer>): WorkflowSaveAnswer => ({
  id: 'docs', done: true, refused: null, message: null, version: 1, written: false, appended: false, scopes: [], reach: [], problems: [], ...more,
});

/** A plan of steps that hold. */
export const HOLDS = answer({ message: '`docs` v1, a new workflow, from the page.' });

/** A plan refused at a way back that goes forward, and at a step no way reaches. */
export const REFUSED = answer({
  done: false, refused: 'refused', version: null,
  message: 'v1: its graph does not hold: the back wire `i` goes to `landing`, which `review` does not come after.',
  problems: [
    { code: 'back-forward', wire: 'i', node: null, words: 'the back wire `i` goes to `landing`, which `review` does not come after.' },
    { code: 'unreachable', wire: null, node: 'line', words: 'no entry reaches `line`.' },
    { code: 'a-code-this-page-has-no-words-for', wire: null, node: null, words: 'something the page cannot word.' },
  ],
});

/** A plan appended to a saved workflow chosen in two places. */
export const APPEND = answer({
  id: 'docs-release', version: 3, appended: true, message: '`docs-release` v2 → v3.',
  scopes: [
    { level: 'repository', name: 'engine', role: 'default', kind: null, key: 'repository:engine:default' },
    { level: 'workspace', name: 'aurora', role: 'kind', kind: 'docs', key: 'workspace:aurora:kind:docs' },
  ],
});
