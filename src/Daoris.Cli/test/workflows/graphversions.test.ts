import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { planAddVersion, readSteps, readVersion, readWorkflowFile, unrun } from '../../src/workflows/namedworkflows.ts';
import { decodeVersion } from '../../src/workflows/workflowgraphs.ts';

/**
 * FABRIC-GRAPH1b: a named graph as a named workflow's version (schema 2), read as the driver reads it. One table,
 * `fixtures/workflow-graph-versions.json`, row for row, the driver's `WorkflowGraphVersionsTests` reading it as this file does: each
 * version's graph, its problem and the digest a run bound to it binds. Since FABRIC-GRAPH1c and GRAPH1k the CLI judges a graph whole
 * (`nodegraphs.ts`), so every row the driver judges is judged here too.
 */

interface ReadRow {
  name: string;
  judged: 'both' | 'cli-refuses';
  cli?: string;
  graph: boolean;
  problem: string | null;
  digest: string | null;
  version: Record<string, unknown> & { version: number };
  older: string | null;
}

interface AddRow { name: string; file: unknown; id: string; workflowName: string | null; door: string; at: string; graph: unknown; problem: string | null }
interface BoundRow { name: string; file: unknown; version: number; digest: string; identity: string | null; unreadable: string | null }

const here = dirname(dirname(fileURLToPath(import.meta.url)));
const TABLE = JSON.parse(readFileSync(join(here, 'fixtures', 'workflow-graph-versions.json'), 'utf8')) as {
  graph: unknown; reads: ReadRow[]; adds: AddRow[]; bound: BoundRow[];
};

/** A row's JSON with each `"$graph"` the table's one graph. */
function filled<T>(value: T): T {
  return JSON.parse(JSON.stringify(value), (_key, each) => (each === '$graph' ? structuredClone(TABLE.graph) : each)) as T;
}

test('graph-versions: each read row of the shared table reads as the driver reads it', () => {
  assert.ok(TABLE.reads.length >= 10);
  for (const row of TABLE.reads) {
    const version = filled(row.version);
    const read = readVersion(version, version.version, []);
    if (row.judged === 'cli-refuses') {
      // The driver reads the graph; the CLI cannot write its option's text as the driver does, so it says so and reads no digest.
      assert.equal(read.problem, row.cli, row.name);
      assert.equal(read.graph, undefined, row.name);
      assert.equal(read.digest, null, row.name);
      continue;
    }

    assert.equal(read.graph !== undefined, row.graph, row.name);
    assert.equal(read.problem, row.problem, row.name);
    assert.equal(read.digest, row.digest, row.name);
  }
});

test('graph-versions: an older build, which reads a version\'s steps alone, reads each version as the table says', () => {
  for (const row of TABLE.reads) {
    const version = filled(row.version);
    const older = readSteps(version['steps'] ?? undefined, version.version);
    assert.equal(older.problem, row.older, row.name);
    assert.equal(older.steps === null, row.older !== null, row.name);
  }
});

test('graph-versions: a file holding a graph version reads its other versions and says why the graph is not run', () => {
  const steps = TABLE.reads[1]!.version;
  const file = filled({ id: 'docs-to-pr', name: 'Docs', versions: [steps, TABLE.reads[0]!.version, { ...steps, version: 3 }] });
  const read = readWorkflowFile(file);
  assert.equal(read.problem, null);
  assert.deepEqual(read.versions.map((each) => each.steps !== null), [true, false, true]);
  assert.ok(read.versions[1]!.graph !== undefined);
  assert.equal(unrun(read.versions[1]!), 'v2: it is a graph (schema 2), which this build reads and does not run yet.');
  assert.equal(unrun(read.versions[0]!), null);
});

test('graph-versions: a run bound to a version has its digest as its identity, a graph version\'s its graph digest', () => {
  for (const row of TABLE.bound) {
    const read = readWorkflowFile(filled(row.file));
    const version = read.versions.find((each) => each.version === row.version)!;
    const decoded = decodeVersion(version);
    const digest = row.digest === '$digest' ? version.digest : row.digest;
    if (row.unreadable === null) {
      assert.equal(decoded.identity, digest, row.name);
      assert.equal(decoded.identity, row.identity === '$digest' ? version.digest : row.identity, row.name);
      if (version.graph !== undefined) assert.equal(decoded.graphDigest, version.digest, row.name);
    } else {
      // The binding's check is the driver's (`WorkflowGraphs.ForRun`); the identity a graph version has is not the steps' digest.
      assert.notEqual(decoded.identity, digest, row.name);
    }
  }
});

test('graph-versions: a file holding a version the CLI cannot write as the driver does is never written over by it', () => {
  const steps = TABLE.reads[1]!.version;
  const lossy = TABLE.reads.find((row) => row.judged === 'cli-refuses')!.version;
  const file = filled({ id: 'docs-to-pr', name: 'Docs', versions: [steps, lossy] });
  const plan = planAddVersion(file, { id: 'docs-to-pr', name: null, door: 'terminal', at: '2026-10-18T09:00:00Z',
    steps: [{ id: 'work', kind: 'work' }, { id: 'land', kind: 'landing', form: 'merge', accept: 'you' }] });
  assert.equal(plan.result, null);
  assert.match(plan.problem!, /^nothing was written: v2 holds a value this build would write differently than the driver does/);
});

test('graph-versions: an option named __proto__ is kept as an option, and moves the digest', () => {
  const row = TABLE.reads.find((each) => each.name.includes('__proto__'))!;
  const read = readVersion(filled(row.version), 2, []);
  assert.deepEqual(Object.keys(read.graph!.nodes[0]!.options), ['__proto__', 'a']);
  assert.equal(read.digest, row.digest);
});

test('graph-versions: the CLI saves no graph, and says where one is saved; steps are planned as before', () => {
  for (const row of TABLE.adds) {
    const plan = planAddVersion(filled(row.file), { id: row.id, name: row.workflowName, door: row.door, at: row.at, steps: null, graph: filled(row.graph) });
    assert.equal(plan.result, null, row.name);
    assert.match(plan.problem!, /a graph is saved where it is judged whole, by the driver/, row.name);
  }

  const steps = [{ id: 'work', kind: 'work' }, { id: 'land', kind: 'landing', form: 'merge', accept: 'you' }];
  const plan = planAddVersion(null, { id: 'docs-to-pr', name: 'Docs', door: 'terminal', at: '2026-10-18T09:00:00Z', steps });
  assert.equal(plan.problem, null);
  assert.deepEqual(Object.keys((plan.result!['versions'] as object[])[0]!), ['version', 'at', 'door', 'steps']);
});
