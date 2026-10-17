import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readShape, readSteps, readVersion, versionDigest } from '../../src/workflows/namedworkflows.ts';
import { DECODER_VERSION, decodeSteps, decodeVersion, workflowGraph, type DecodedGraph } from '../../src/workflows/workflowgraphs.ts';
import { driverFile } from '../_csharp.ts';

/**
 * FABRIC-WORKFLOW2: D157's stored versions decoded as graphs (the fabric events design §E7's migration contract), as the driver decodes
 * them. One table, `fixtures/workflow-graphs.json`, row for row, the driver's `WorkflowGraphsTests` reading it as this file does: each
 * graph, its identity and its digest, or the reason the version is unreadable.
 */

interface Row {
  name: string;
  read: 'version' | 'shape';
  version: Record<string, unknown> & { version: number; steps: unknown };
  identity?: string;
  graphDigest?: string;
  graph?: DecodedGraph;
  unreadable?: string;
}

const here = dirname(dirname(fileURLToPath(import.meta.url)));
const TABLE = JSON.parse(readFileSync(join(here, 'fixtures', 'workflow-graphs.json'), 'utf8')) as { decoderVersion: number; rows: Row[] };

function decoded(row: Row) {
  if (row.read === 'version') return decodeVersion(readVersion(row.version, row.version.version, []));
  const { steps, problem } = readShape(row.version.steps, row.version.version);
  assert.equal(problem, null, row.name);
  return decodeSteps(row.version.version, steps!);
}

test('workflow-graphs: each row of the shared table decodes as the driver decodes it', () => {
  assert.equal(TABLE.decoderVersion, DECODER_VERSION);
  assert.ok(TABLE.rows.length >= 6);
  for (const row of TABLE.rows) {
    const result = decoded(row);
    assert.equal(result.version, row.version.version, row.name);
    if (row.unreadable !== undefined) {
      assert.deepEqual(result, { version: row.version.version, identity: null, graph: null, graphDigest: null, unreadable: row.unreadable }, row.name);
      continue;
    }

    assert.deepEqual(result.graph, row.graph, row.name);
    assert.deepEqual([result.identity, result.graphDigest, result.unreadable], [row.identity, row.graphDigest, null], row.name);
  }
});

test('workflow-graphs: the decoder\'s version and the check\'s node are the driver\'s', () => {
  const source = readFileSync(driverFile('WorkflowGraphs.cs'), 'utf8');
  assert.equal(Number(/DecoderVersion = (\d+);/.exec(source)?.[1]), DECODER_VERSION);
  assert.equal(/ChecksNode = "([^"]+)";/.exec(source)?.[1], 'work.checks');
});

test('workflow-graphs: the shape reads a kind this build does not run, which the steps refuse, so its mapping is proved ahead of it', () => {
  const steps = [{ id: 'work', kind: 'work' }, { id: 'land', kind: 'landing', form: 'branch', accept: 'you' }, { id: 'ci', kind: 'check', plugin: 'acme.ci' }];
  assert.equal(readSteps(steps, 2).problem, 'v2, step `ci`: this build does not run a step of kind `check` yet.');
  const shape = readShape(steps, 2);
  assert.equal(shape.problem, null);
  const graph = workflowGraph(shape.steps!);
  assert.deepEqual(graph.nodes.map((node) => [node.id, node.kind, node.reach, node.synthetic ?? false]), [
    ['work', 'claimed', 'observed', false], ['land', 'handed', 'observed', false], ['ci', 'acme.ci/work.checks', 'observed', false],
    ['ci:failed', 'human', 'pressed', true],
  ]);
  assert.equal(graph.end, 'ci');
});

test('workflow-graphs: decoding twice gives the same graph and digest, and the identity is the version\'s digest', () => {
  const { steps } = readShape(TABLE.rows.find((row) => row.read === 'shape')!.version.steps, 6);
  const first = decodeSteps(6, steps!);
  const second = decodeSteps(6, structuredClone(steps!));
  assert.deepEqual(second, first);
  assert.equal(first.identity, versionDigest(steps!));
  assert.match(first.graphDigest!, /^gph-[0-9a-f]{32}$/);
});
