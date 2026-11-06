import { fingerprintOf } from '../draftFingerprint';
import { MERGE_GRAPH } from '../graphFixtures';
import type { EditorRole, PaletteKind, WorkflowListAnswer, WorkflowPaletteAnswer, WorkflowSaveAnswer } from './answers';
import { draftOf, graphOf } from './graphDraft';

// The step editor's answers as the driver gives them (WORKFLOWEDIT1b), for its page tests and its stories: a palette of every kind the
// shipped workflows hold, each with the driver's own authority, and the Local release plugin's three steps; the defaults listed with their
// graphs, and roles.

/** The wire kinds that may leave a node, as the driver's palette says them (`WorkflowPalette.Leaving`). */
const leaving = (fails: boolean, judged: boolean, decision = false): string[] => [
  'after', 'data', 'trigger', ...(fails ? ['failed'] : []), ...(judged ? ['rejected'] : []), ...(decision ? ['when'] : []),
  ...(fails || judged || decision ? ['back'] : []),
];

/** A built-in kind as the driver's palette answers it (`NodeAuthorities.BuiltIn`): who fails it, whether a person judges it, whether it may end. */
const kind = (name: string, reach: string, more: Partial<PaletteKind> = {}): PaletteKind => {
  const answered: PaletteKind = {
    kind: name, reach, reachBy: 'daoris', failureBy: null, personJudges: false, personSkips: false, mayEnd: true, evidenced: false, skipBy: null,
    options: [], leaves: [], roleTakes: [], ...more,
  };
  return { ...answered, leaves: more.leaves ?? leaving(answered.failureBy !== null, answered.personJudges) };
};

const RELEASE = 'local-release/release@1';
const RECEIPT = 'local-release/receipt@1';

export const PALETTE: WorkflowPaletteAnswer = {
  kinds: [
    kind('claimed', 'observed', { reachBy: 'agent', mayEnd: false, evidenced: true, options: [{ name: 'role', type: 'text', choices: [], required: false }], roleTakes: ['design', 'build'] }),
    kind('work.requests', 'observed', { personSkips: true, evidenced: true }),
    kind('quest.holds', 'observed', { personSkips: true, mayEnd: false, evidenced: true }),
    kind('content.holds', 'observed', { personSkips: true, mayEnd: false, evidenced: true }),
    kind('check', 'performed', { failureBy: 'gates', evidenced: true, options: [{ name: 'gates', type: 'names', choices: [], required: false }] }),
    kind('review', 'pressed', { reachBy: 'person', personJudges: true, mayEnd: false }),
    kind('land', 'performed', { reachBy: 'landing', failureBy: 'landing', evidenced: true,
      options: [{ name: 'method', type: 'choice', choices: ['merge', 'branch', 'integrate'], required: false },
        { name: 'target', type: 'text', choices: [], required: false }, { name: 'pattern', type: 'text', choices: [], required: false },
        { name: 'squash', type: 'switch', choices: [], required: false },
        { name: 'message', type: 'text', choices: [], required: false }] }),
    kind('line', 'observed', { evidenced: true }),
    kind('push', 'observed', { failureBy: 'plugin', personSkips: true, evidenced: true }),
    kind('integrate', 'waited', { failureBy: 'integration', evidenced: true, offered: 'tail:integrate',
      options: [{ name: 'request', type: 'choice', choices: ['owner', 'done', 'steward'], required: false },
        { name: 'conflicts', type: 'choice', choices: ['session', 'hold'], required: false }] }),
  ],
  wires: [
    { kind: 'after', eligibility: true, from: null, to: null },
    { kind: 'when', eligibility: true, from: [], to: null },
    { kind: 'back', eligibility: false, from: null, to: null },
  ],
  plugins: [{ plugin: 'local-release', steps: [
    { kind: 'local-release/find', version: 1, reach: 'performed', rejectable: false, skippable: true, inputs: {}, outputs: { release: RELEASE },
      evidence: RECEIPT, outcomes: ['release', 'none'], outputsOn: { release: ['release'] }, leaves: ['after', 'data', 'trigger', 'failed', 'when', 'back'] },
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

/** The release default's graph (DEFAULTCONFIG1c): the work checked and reviewed, landed, then Local release's find, release and publish. */
export const RELEASE_GRAPH = {
  schemaVersion: 2, entries: ['work'], end: 'line',
  nodes: [
    { id: 'work', kind: 'claimed', version: 1, reach: 'observed' },
    { id: 'requests', kind: 'work.requests', version: 1, reach: 'observed' },
    { id: 'check', kind: 'check', version: 1, reach: 'performed' },
    { id: 'review', kind: 'review', version: 1, reach: 'pressed' },
    { id: 'landing', kind: 'land', version: 1, reach: 'performed', options: { method: 'merge' } },
    { id: 'line', kind: 'line', version: 1, reach: 'observed' },
    { id: 'find', kind: 'local-release/find', version: 1, reach: 'performed', outcomes: ['release', 'none'], outputs: { release: RELEASE }, evidence: RECEIPT },
    { id: 'release', kind: 'local-release/release', version: 1, reach: 'performed', inputs: { release: RELEASE }, outputs: { release: RELEASE }, evidence: RECEIPT },
    { id: 'publish', kind: 'local-release/publish', version: 1, reach: 'performed', inputs: { release: RELEASE }, evidence: RECEIPT },
  ],
  wires: [
    { id: 'a', kind: 'after', from: 'work', to: 'requests' },
    { id: 'b', kind: 'after', from: 'requests', to: 'check' },
    { id: 'c', kind: 'after', from: 'check', to: 'review' },
    { id: 'd', kind: 'after', from: 'review', to: 'landing' },
    { id: 'e', kind: 'after', from: 'landing', to: 'line' },
    { id: 'f', kind: 'back', from: 'review', on: 'rejected', to: 'work' },
    { id: 'g', kind: 'after', from: 'line', to: 'find' },
    { id: 'h', kind: 'when', from: 'find', outcome: 'release', to: 'release' },
    { id: 'i', kind: 'data', from: 'find.release', to: 'release.release' },
    { id: 'j', kind: 'after', from: 'release', to: 'publish' },
    { id: 'k', kind: 'data', from: 'release.release', to: 'publish.release' },
  ],
};

/** The release default with the work naming a role that only reviews, and a way back from the review into the landing, which goes forward. */
export const REFUSED_GRAPH = {
  ...RELEASE_GRAPH,
  nodes: RELEASE_GRAPH.nodes.map((node) => (node.id === 'work' ? { ...node, options: { role: 'opinion' } } : node)),
  wires: [...RELEASE_GRAPH.wires, { id: 'l', kind: 'back', from: 'review', on: 'rejected', to: 'landing' }],
};

export const LIST: WorkflowListAnswer = {
  workflows: [{ id: 'docs-release', name: 'Docs release', problem: null, versions: [1, 2], schema: 2, digest: 'ab', line: 'x', pipeline: null,
    chosen: [{ level: 'repository', name: 'engine', role: 'default', kind: null }] }],
  presets: [
    { id: 'merge-after-accept', name: 'Merge workflow', graph: true, line: 'You review the work, and it merges into the line.', preset: true, body: MERGE_GRAPH,
      for: 'For a repository whose work you accept: once the work\'s requests are answered you review it, and it merges into the line.', order: 1, setUp: true,
      delivery: { id: 'merge-after-accept', method: 'merge', accept: 'you', name: 'Merge after you accept', for: 'For a repository whose work you accept.' } },
    { id: 'documents', name: 'Documents workflow', graph: true, line: 'The work merges into the line.', preset: true, body: DOCUMENTS_GRAPH,
      for: 'For a kind of task whose paths are documents: the work merges into the line once it is done, with no review.', order: 3, setUp: false,
      delivery: { id: 'merge-automatic', method: 'merge', accept: 'automatic', name: 'Merge with no review', for: 'For a development repository.' } },  ],
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

/** `REFUSED_GRAPH`'s plan: refused at the way back that goes forward and at the work's role, and by a rule this page has no words for. */
export const REFUSED = answer({
  done: false, refused: 'refused', version: null,
  message: 'v1: its graph does not hold: the back wire `l` goes to `landing`, which `review` does not come after.',
  problems: [
    { code: 'back-forward', wire: 'l', node: null, words: 'the back wire `l` goes to `landing`, which `review` does not come after.' },
    { code: 'role', wire: null, node: 'work', words: 'the step `work` names the role `opinion`, which cannot take it: The role `opinion` does not take `design`.' },
    { code: 'a-rule-newer-than-this-page', wire: null, node: null, words: 'a rule this page has no words for refuses `publish`.' },
  ],
});

/** The fingerprint the editor holds for a draft opened on `graph`: what a judgement of it is keyed by (`judgedOf`). */
export const judgedFor = (graph: unknown): string => fingerprintOf(graphOf(draftOf(graph)));

/** A plan appended to a saved workflow chosen in two places. */
export const APPEND = answer({
  id: 'docs-release', version: 3, appended: true, message: '`docs-release` v2 → v3.',
  scopes: [
    { level: 'repository', name: 'engine', role: 'default', kind: null, key: 'repository:engine:default' },
    { level: 'workspace', name: 'aurora', role: 'kind', kind: 'docs', key: 'workspace:aurora:kind:docs' },
  ],
});
