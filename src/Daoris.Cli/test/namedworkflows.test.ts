import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  KEPT_VERSIONS, MAX_STEPS, WORKFLOW_KINDS, WORKFLOW_PRESETS, planAddVersion, readSteps, readVersion, readWorkflowFile,
  versionCanonical, withGates, workflowDiff, workflowLine, type NamedStep, type SavedStep, type VersionAdded,
} from '../src/namedworkflows.ts';
import { WORKFLOW_LIMITS } from '../src/workflows.ts';

/**
 * WORKFLOW1d (D157 points 5, 6 and 8; the workflow design §2.4–§2.6, §3.2, §3.3, §3.9): named workflows and their versions.
 * 🔴 A TWIN with the driver's `WorkflowNamed`, `WorkflowDiff`, `WorkflowPresets` and `WorkflowStore`: both hold ONE table, the
 * driver suite's `fixtures/workflow-named.json`, cell for cell — every sentence, every digest and every line said. The
 * driver's `WorkflowNamedTests` reads the same file.
 */
const TABLE = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop',
  'Daoris.Desktop.Driver.Tests', 'fixtures', 'workflow-named.json'), 'utf8')) as {
  kinds: { kind: string; runtime: string; limit: string | null; fields: Record<string, unknown>[] }[];
  files: { name: string; file: unknown; problem: string | null; versions: { version: number; problem: string | null; digest: string | null }[] }[];
  versions: { name: string; steps: unknown; problem: string | null; digest: string | null }[];
  diffs: { name: string; before: unknown[] | null; after: unknown[]; entries: unknown[]; said: string[] }[];
  presets: { id: string; name: string; steps: SavedStep[]; line: string }[];
  variants: { preset: string; opinion: boolean; look: boolean; steps: SavedStep[]; line: string }[];
  lines: { name: string; steps: unknown[]; line: string }[];
  store: { name: string; file: unknown; add: VersionAdded; kept: number[]; problem: string | null; version: number | null; result: unknown }[];
};

/** Steps that must read, as v1 does. */
function read(steps: unknown): NamedStep[] {
  const { steps: named, problem } = readSteps(steps, 1);
  assert.equal(problem, null, `the table's steps do not read: ${problem}`);
  return named!;
}

test('the kinds are the table\'s, each its runtime on main, its limit and its fields in order', () => {
  assert.deepEqual(WORKFLOW_KINDS.map((row) => ({
    kind: row.kind, runtime: row.runtime, limit: row.limit,
    fields: row.fields.map((each) => ({
      name: each.name, type: each.type, ...(each.choices === undefined ? {} : { choices: [...each.choices] }),
      required: each.required, default: each.default,
    })),
  })), TABLE.kinds);
  for (const row of WORKFLOW_KINDS) {
    if (row.limit !== null) assert.ok(WORKFLOW_LIMITS[row.limit] !== undefined, `${row.kind}'s limit is no code Current says`);
  }
  assert.equal(MAX_STEPS, 24);
  assert.equal(KEPT_VERSIONS, 20);
});

test('a workflow file is read as the driver reads it: the file\'s problem, then each version\'s, and each digest', () => {
  assert.ok(TABLE.files.length >= 20, `only ${TABLE.files.length} rows, so this proves little`);
  for (const row of TABLE.files) {
    const read = readWorkflowFile(row.file);
    assert.equal(read.problem, row.problem, `${row.name}: the file's problem`);
    assert.deepEqual(read.versions.map((each) => ({ version: each.version, problem: each.problem, digest: each.digest })),
      row.versions, `${row.name}: its versions`);
  }
});

test('a version\'s steps are read as the driver reads them, the first problem said in its words, else its digest', () => {
  assert.ok(TABLE.versions.length >= 60, `only ${TABLE.versions.length} rows, so this proves little`);
  for (const row of TABLE.versions) {
    const read = readVersion({ version: 1, at: '2026-10-09T09:12:00Z', door: 'terminal', steps: row.steps }, 1, []);
    assert.equal(read.problem, row.problem, `${row.name}: the problem`);
    assert.equal(read.digest, row.digest, `${row.name}: the digest`);
  }
});

test('the digest is of a text built by hand, escaped by hand, every field written whatever was left out', () => {
  const canonical = versionCanonical(read([{ id: 'work', kind: 'work' },
    { id: 'ahead', kind: 'go-ahead', act: 'say "yes" to C:\\work\nthen go', on: 'dev' },
    { id: 'landing', kind: 'landing', form: 'merge', accept: 'you' }]));
  assert.equal(canonical, [
    'workflow', 'step "work" work', 'step "ahead" go-ahead', '  act="say \\"yes\\" to C:\\\\work\\u000athen go"', '  on="dev"',
    '  refused="stop"', 'step "landing" landing', '  form="merge"', '  accept="you"', '  pattern=-', '  plugin=-',
  ].join('\n'));
});

test('two versions are compared by step id as the driver compares them: each entry, and every line said', () => {
  for (const row of TABLE.diffs) {
    const diff = workflowDiff(row.before === null ? null : read(row.before), read(row.after));
    assert.deepEqual(diff.entries, row.entries, `${row.name}: the entries`);
    assert.deepEqual(diff.said, row.said, `${row.name}: what is said`);
  }
});

test('the presets are the table\'s, in order, each line composed from its steps', () => {
  assert.deepEqual(WORKFLOW_PRESETS.map((preset) => ({ id: preset.id, name: preset.name, steps: preset.steps })),
    TABLE.presets.map((row) => ({ id: row.id, name: row.name, steps: row.steps })));
  for (const row of TABLE.presets) assert.equal(workflowLine(read(row.steps)), row.line, row.id);
});

test('a preset with a second opinion, your look first, or both: the steps added before the landing, and the line', () => {
  for (const row of TABLE.variants) {
    const preset = WORKFLOW_PRESETS.find((each) => each.id === row.preset)!;
    const steps = withGates(preset.steps, { opinion: row.opinion, look: row.look });
    assert.deepEqual(steps, row.steps, `${row.preset}: its steps`);
    assert.equal(workflowLine(read(steps)), row.line, `${row.preset}: its line`);
  }
});

test('the line other steps compose', () => {
  for (const row of TABLE.lines) assert.equal(workflowLine(read(row.steps)), row.line, row.name);
});

test('a version added to a file is planned as the driver plans it: the file after, or the problem that wrote nothing', () => {
  for (const row of TABLE.store) {
    const plan = planAddVersion(row.file, row.add, row.kept);
    assert.equal(plan.problem, row.problem, `${row.name}: the problem`);
    assert.equal(plan.version, row.version, `${row.name}: the version`);
    assert.deepEqual(plan.result, row.result, `${row.name}: the file after`);
  }
});
