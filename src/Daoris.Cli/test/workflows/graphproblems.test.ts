import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readGraphParts, type FormWire, type GraphForm } from '../../src/workflows/graphversions.ts';
import { parseWithSource } from '../../src/workflows/jsonsource.ts';
import {
  BUILT_IN_AUTHORITIES, GRAPH_REFUSALS, WIRE_REFUSALS, cycleProblems, graphProblems, readGraph, wireProblems, type GraphProblem,
} from '../../src/workflows/nodegraphs.ts';
import { driverFile } from '../_csharp.ts';

/**
 * FABRIC-GRAPH1c: a graph judged whole when it is saved, by the CLI as the driver judges it (the fabric events design §E3, §E7). One
 * table, `fixtures/graph-problems.json`, row for row, the driver's `NodeWiresTests` and `NodeGraphsTests` reading it as this file does:
 * WIRE1a's codes and GRAPH1's, each named alone with its words, the graphs that hold, a landing rule's cycle after its landing, and
 * what does not read as a graph's shape. Read with each number's own text (`parseWithSource`), as the driver's reader reads it.
 */

type Json = Record<string, unknown>;
interface End { wire: string; end: 'from' | 'to'; node: string; port: string | null; unit: string | null }
interface Row {
  name: string; base?: 'wireBase' | 'graphBase'; graph?: Json; chain?: { count: number; synthetic: number }; dense?: number;
  remove?: { nodes?: string[]; wires?: string[] }; replace?: { nodes?: Json[]; wires?: Json[] }; add?: { nodes?: Json[]; wires?: Json[] };
  set?: Json; ends?: End[]; rule?: { form: string; pattern?: string; plugin?: string }; cycle?: unknown; value?: unknown;
  problems: GraphProblem[];
}

const here = dirname(dirname(fileURLToPath(import.meta.url)));
const TABLE = parseWithSource(readFileSync(join(here, 'fixtures', 'graph-problems.json'), 'utf8')) as {
  wireBase: Json; graphBase: Json; wires: Row[]; graphs: Row[]; fragments: Row[]; unreadable: Row[];
};

const copy = <T>(value: T): T => structuredClone(value);
const ids = (list: unknown): Json[] => (list ?? []) as Json[];

/** A chain of person's tasks, each after the one before, and synthetic tasks after the first — the driver's tests' `Chain`. */
function chain(count: number, synthetic: number): GraphForm {
  const nodes: Json[] = Array.from({ length: count }, (_, index) => ({ id: `h${index}`, kind: 'human', version: 1, reach: 'pressed' }));
  const wires: Json[] = Array.from({ length: count - 1 }, (_, index) => ({ id: `w${index + 1}`, kind: 'after', from: `h${index}`, to: `h${index + 1}` }));
  for (let index = 0; index < synthetic; index += 1) {
    nodes.push({ id: `s${index}`, kind: 'human', version: 1, reach: 'pressed' });
    wires.push({ id: `x${index}`, kind: 'after', from: 'h0', to: `s${index}` });
  }
  const graph = form({ nodes, wires, entries: ['h0'], end: `h${count - 1}` });
  for (const node of graph.nodes) if (node.id.startsWith('s')) node.synthetic = true;
  return graph;
}

/** 24 tasks wired every earlier to every later, to `wires` wires — the driver's tests' `Dense`. */
function dense(wires: number): GraphForm {
  const graph = chain(24, 0);
  for (let source = 0; source < 24 && graph.wires.length < wires; source += 1) {
    for (let target = source + 2; target < 24 && graph.wires.length < wires; target += 1) {
      graph.wires.push({ id: `z${source}-${target}`, kind: 'after', from: { node: `h${source}`, port: null }, to: { node: `h${target}`, port: null },
        waits: null, group: null });
    }
  }
  return graph;
}

function form(value: unknown): GraphForm {
  const read = readGraphParts(value);
  assert.ok(read.parts !== null, `${read.problem ?? read.lossy}`);
  const { parts } = read;
  return { ...parts, end: parts.end ?? '', cleanup: parts.cleanup ?? { until: 'held', days: null } };
}

/** The row's graph, made as the table says: the base changed, or written whole, or made by a twin's own maker; its ends set after. */
function graphOf(row: Row): GraphForm {
  let graph: GraphForm;
  if (row.chain !== undefined) graph = chain(row.chain.count, row.chain.synthetic);
  else if (row.dense !== undefined) graph = dense(row.dense);
  else {
    const json = row.graph !== undefined ? copy(row.graph) : copy(TABLE[row.base!]);
    const byId = (list: Json[], removed: string[] = []) => list.filter((each) => !removed.includes(each['id'] as string));
    json['nodes'] = byId(ids(json['nodes']), row.remove?.nodes);
    json['wires'] = byId(ids(json['wires']), row.remove?.wires);
    for (const key of ['nodes', 'wires'] as const) {
      for (const each of row.replace?.[key] ?? []) {
        const list = json[key] as Json[];
        list[list.findIndex((held) => held['id'] === each['id'])] = copy(each);
      }
      json[key] = [...(json[key] as Json[]), ...copy(row.add?.[key] ?? [])];
    }
    Object.assign(json, copy(row.set ?? {}));
    graph = form(json);
  }
  for (const mark of row.ends ?? []) {
    const wire = graph.wires.find((each) => each.id === mark.wire) as FormWire;
    wire[mark.end] = { node: mark.node, port: mark.port, unit: mark.unit };
  }
  return graph;
}

const shown = (problems: readonly GraphProblem[]) => problems.map(({ code, wire, node, words }) => ({ code, wire, node, words }));

test('graph-problems: each wire row is judged as the driver judges it', () => {
  assert.ok(TABLE.wires.length >= 40);
  for (const row of TABLE.wires) {
    const graph = graphOf(row);
    assert.deepEqual({ name: row.name, problems: shown(wireProblems(graph.nodes, graph.wires)) }, { name: row.name, problems: row.problems });
  }
});

test('graph-problems: each graph row is judged whole as the driver judges it', () => {
  assert.ok(TABLE.graphs.length >= 30);
  for (const row of TABLE.graphs) {
    assert.deepEqual({ name: row.name, problems: shown(graphProblems(graphOf(row))) }, { name: row.name, problems: row.problems });
  }
});

test('graph-problems: a landing rule\'s cycle is judged after its landing, as the driver judges it', () => {
  for (const row of TABLE.fragments) {
    assert.deepEqual({ name: row.name, problems: shown(cycleProblems(row.rule!, row.cycle)) }, { name: row.name, problems: row.problems });
  }
});

test('graph-problems: what is not a graph\'s shape is unreadable, the first place in the reader\'s order, never in part', () => {
  for (const row of TABLE.unreadable) {
    const read = readGraph(row.value);
    assert.equal(read.graph, null, row.name);
    assert.deepEqual({ name: row.name, problems: shown(read.problems) }, { name: row.name, problems: row.problems });
  }
});

test('graph-problems: a problem names its wire and its node in its words', () => {
  for (const row of [...TABLE.wires, ...TABLE.graphs, ...TABLE.fragments]) {
    for (const problem of row.problems) {
      if (problem.wire !== null && problem.wire.length > 0) assert.ok(problem.words.includes(`\`${problem.wire}\``), `${row.name}: ${problem.words}`);
      if (problem.node !== null) assert.ok(problem.words.includes(`\`${problem.node}\``), `${row.name}: ${problem.words}`);
    }
  }
});

/** The constants a C# class declares, by name, in its order: `public const string X = "code";`. */
function constsOf(file: string, owner: string): [string, string][] {
  const source = readFileSync(driverFile(file), 'utf8');
  const start = source.indexOf(`public static class ${owner}`);
  assert.ok(start >= 0, `${file} declares no ${owner}`);
  const body = source.slice(start, source.indexOf('\n}', start));
  return [...body.matchAll(/public const string (\w+) = "([^"]+)";/g)].map((match) => [match[1]!, match[2]!]);
}

test('graph-problems: the codes are the driver\'s, and the table holds each with a row', () => {
  assert.deepEqual([...WIRE_REFUSALS], constsOf('NodeWires.cs', 'WireRefusal').map(([, code]) => code));
  assert.deepEqual([...GRAPH_REFUSALS], constsOf('NodeGraphs.cs', 'GraphRefusal').map(([, code]) => code));
  const named = new Set([...TABLE.wires, ...TABLE.graphs, ...TABLE.fragments, ...TABLE.unreadable].flatMap((row) => row.problems.map((each) => each.code)));
  assert.deepEqual([...WIRE_REFUSALS, ...GRAPH_REFUSALS].filter((code) => !named.has(code)), []);
});

test('graph-problems: each built-in kind\'s authority is the driver\'s', () => {
  const source = readFileSync(driverFile('NodeContract.cs'), 'utf8');
  const kinds = Object.fromEntries(constsOf('NodeContract.cs', 'NodeKinds'));
  const reaches = Object.fromEntries(constsOf('NodeContract.cs', 'NodeReach'));
  const rows = [...source.matchAll(/\[NodeKinds\.(\w+)\] = new\(NodeKinds\.\w+, NodeReach\.(\w+), NodeSource\.\w+, (null|NodeSource\.\w+), (true|false), (true|false), (?:MayEnd: )?(true|false)/g)];
  assert.equal(rows.length, Object.keys(BUILT_IN_AUTHORITIES).length);
  for (const [, kind, reach, failure, judges, , mayEnd] of rows) {
    const authority = BUILT_IN_AUTHORITIES[kinds[kind!]!];
    assert.deepEqual({ kind: kinds[kind!], reach: authority?.reach, fails: authority?.fails, judges: authority?.judges, mayEnd: authority?.mayEnd },
      { kind: kinds[kind!], reach: reaches[reach!], fails: failure !== 'null', judges: judges === 'true', mayEnd: mayEnd === 'true' });
  }
});
