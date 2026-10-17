/**
 * D157's named versions decoded as graphs, never rewritten (FABRIC-WORKFLOW2; the fabric events design §E7's migration contract, §E4).
 * Pure: a version read as `namedworkflows.ts` reads it becomes `{ schemaVersion, entries, end, cleanup, nodes, wires }`, each node's
 * options as JSON values. Step order is `after` wires; a group is a synthetic `all` join with the group's id, its members wired into it
 * and it into the next step; `failed: stop` is a `failed` wire to a synthetic `stop`, `wait` one to a synthetic person's task, and
 * `send-back` the adapter's one return, then the person's task; a go-ahead's `refused` takes its `rejected` wire the same way.
 *
 * The driver's twin is `WorkflowGraphs` (`WorkflowGraphs.cs`); both read `test/fixtures/workflow-graphs.json`, row for row, every graph
 * and digest included (`workflowgraphs.test.ts`, `WorkflowGraphsTests`). The driver also judges each decoded graph whole
 * (`NodeGraphs.Problems`), which the CLI does not hold; the table's rows show that every version that reads decodes to a graph that
 * holds, so the twins agree on what is unreadable.
 */
import { createHash } from 'node:crypto';
import { field, members, quoted, versionDigest, type NamedStep, type NamedVersion, type StepValue } from './namedworkflows.ts';

/** The decoder's own version — the driver's `WorkflowGraphs.DecoderVersion`: a decoded graph's digest is derived from it. */
export const DECODER_VERSION = 1;

/** The kind a check decodes to after its plugin — the driver's `WorkflowGraphs.ChecksNode`: `work/checks`, its slash a dot. */
const CHECKS_NODE = 'work.checks';

/** The graph's schema — the driver's `NodeGraphs.SchemaVersion`; D157's versions are schema 1, decoded. */
const SCHEMA_VERSION = 2;

export interface GraphNode { id: string; kind: string; version: number; reach: string; synthetic?: true; options?: Record<string, StepOption> }
export interface GraphWire { id: string; kind: string; from: string; to: string }
export interface DecodedGraph { schemaVersion: number; entries: string[]; end: string; cleanup: { until: string }; nodes: GraphNode[]; wires: GraphWire[] }

/** A version as a graph: its identity (`versionDigest`, unchanged) and its graph with the digest a run on it binds, or why it is unreadable. */
export interface DecodedVersion { version: number; identity: string | null; graph: DecodedGraph | null; graphDigest: string | null; unreadable: string | null }

/** An option's value: a field's as written, a group's members apart. */
type StepOption = string | null | boolean | string[];

/** The built-in kinds a decoded graph holds, by their reach — the driver's `NodeAuthorities.BuiltIn`. */
const REACH: Record<string, string> = {
  claimed: 'observed', opinion: 'observed', look: 'observed', handed: 'observed', 'pull-request': 'observed', 'go-ahead': 'pressed',
  all: 'observed', human: 'pressed', stop: 'observed',
};

const NODE_KIND: Record<string, string> = {
  work: 'claimed', opinion: 'opinion', look: 'look', landing: 'handed', 'pull-request': 'pull-request', 'go-ahead': 'go-ahead', all: 'all',
};

const ordinal = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

/** A step as its node (§E4): its kind's node, its reach, and every field it takes but a group's members as an option, by name. */
function nodeOf(step: NamedStep): GraphNode {
  const plugin = field(step, 'plugin') as string;
  const kind = step.kind === 'check' ? `${plugin}/${CHECKS_NODE}` : step.kind === 'stage' ? `${plugin}/${field(step, 'stage') as string}` : NODE_KIND[step.kind];
  if (kind === undefined) throw new Error(`\`${step.kind}\` is no kind of step.`);
  const reach = REACH[kind] ?? (step.kind === 'stage' ? 'performed' : 'observed');
  const options = Object.fromEntries(step.fields
    .filter(([name]) => !(step.kind === 'all' && name === 'steps'))
    .map(([name, value]) => [name, value as StepOption] as const)
    .sort(([a], [b]) => ordinal(a, b)));
  return { id: step.id, kind, version: 1, reach, ...(Object.keys(options).length > 0 ? { options } : {}) };
}

/**
 * The graph of `steps` by §E7's table — the driver's `WorkflowGraphs.Graph` — as `readShape` reads them, every kind included: the first
 * step its entry, the last its end, the cleanup at held. Nodes in step order, each followed by its outcome's synthetic node; wires in the
 * order they are made, each named `from>to`.
 */
export function workflowGraph(steps: readonly NamedStep[]): DecodedGraph {
  const nodes: GraphNode[] = [];
  const wires: GraphWire[] = [];
  const wire = (kind: string, from: string, to: string): void => { wires.push({ id: `${from}>${to}`, kind, from, to }); };
  const outcome = (step: NamedStep, path: 'failed' | 'rejected', policy: StepValue | undefined): void => {
    if (typeof policy !== 'string') return;
    const id = `${step.id}:${path}`;
    const kind = policy === 'stop' ? 'stop' : 'human';
    nodes.push({ id, kind, version: 1, reach: REACH[kind]!, synthetic: true, options: { outcome: path, step: step.id } });
    wire(path, step.id, id);
  };

  let previous: string | null = null;
  for (const step of steps) {
    if (step.kind === 'all') {
      // A member's own `failed` is kept as written and takes no path: a member failed is the group failed (D157 §3.3).
      for (const member of members(step)) {
        nodes.push(nodeOf(member));
        if (previous !== null) wire('after', previous, member.id);
      }
      nodes.push({ ...nodeOf(step), synthetic: true });
      for (const member of members(step)) wire('after', member.id, step.id);
    } else {
      nodes.push(nodeOf(step));
      if (previous !== null) wire('after', previous, step.id);
    }

    if (step.kind === 'check' || step.kind === 'stage' || step.kind === 'all') outcome(step, 'failed', field(step, 'failed'));
    if (step.kind === 'go-ahead') outcome(step, 'rejected', field(step, 'refused'));
    previous = step.id;
  }

  return { schemaVersion: SCHEMA_VERSION, entries: steps.length > 0 ? [steps[0]!.id] : [], end: previous ?? '', cleanup: { until: 'held' }, nodes, wires };
}

/** An option's JSON text — the driver's `OptionText`: text quoted as both twins quote it, a switch, a list, or null. */
function optionText(value: StepOption): string {
  if (value === null) return 'null';
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (Array.isArray(value)) return `[${value.map(quoted).join(',')}]`;
  return quoted(value);
}

/**
 * A decoded graph's own digest — the driver's `NodeGraphs.Digest`, line for line, over what a decoded graph declares: a decoded node
 * names no ports, evidence, retry or timeout, and a decoded wire no port, unit, wait or group, each written as the driver writes its
 * absence.
 */
export function nodeGraphDigest(graph: DecodedGraph): string {
  let text = '';
  const line = (value: string | number | boolean | null): void => {
    text += (value === null ? '\\0'
      : typeof value === 'boolean' ? (value ? '1' : '0')
      : typeof value === 'number' ? String(value)
      : value.replaceAll('\\', '\\\\').replaceAll('\n', '\\n')) + '\n';
  };
  const none = (count: number): void => { for (let index = 0; index < count; index++) line(null); };

  line('graph');
  line(graph.schemaVersion);
  line(graph.nodes.length);
  for (const node of [...graph.nodes].sort((a, b) => ordinal(a.id, b.id) || ordinal(a.kind, b.kind))) {
    line('node');
    line(node.id);
    line(node.kind);
    line(node.version);
    line(node.reach);
    line(node.synthetic === true);
    none(4); // evidence, the retry's attempts and delays, the timeout
    line(false); // rejectable
    line(true); // skippable
    line(0); // inputs
    line(0); // outputs
    const options = Object.entries(node.options ?? {}).sort(([a], [b]) => ordinal(a, b));
    line(options.length);
    for (const [name, value] of options) {
      line(name);
      line(optionText(value));
    }
  }

  line(graph.wires.length);
  for (const each of [...graph.wires].sort((a, b) => ordinal(a.id, b.id) || ordinal(a.kind, b.kind))) {
    line('wire');
    line(each.id);
    line(each.kind);
    line(each.from);
    none(2); // its port, its unit
    line(each.to);
    none(2);
    none(5); // the wait's unit, revision, node, end and commitment
    none(2); // the group's id and mode
  }

  line(graph.entries.length);
  for (const entry of [...graph.entries].sort(ordinal)) line(entry);
  line(graph.end);
  line(graph.cleanup.until);
  line(null); // the cleanup's days
  return `gph-${createHash('sha256').update(text, 'utf8').digest('hex').slice(0, 32)}`;
}

/** A decoded graph's digest (§E7: derived from the stored version and the decoder's version) — the driver's `WorkflowGraphs.GraphDigest`. */
export function decodedDigest(identity: string, graph: DecodedGraph): string {
  const text = `workflow-graph\n${DECODER_VERSION}\n${identity}\n${nodeGraphDigest(graph)}\n`;
  return `gph-${createHash('sha256').update(text, 'utf8').digest('hex').slice(0, 32)}`;
}

/** Steps decoded as a version numbered `version` — the driver's `WorkflowGraphs.DecodeSteps`. */
export function decodeSteps(version: number, steps: readonly NamedStep[]): DecodedVersion {
  const identity = versionDigest(steps);
  const graph = workflowGraph(steps);
  return { version, identity, graph, graphDigest: decodedDigest(identity, graph), unreadable: null };
}

/**
 * A stored version decoded (§E7) — the driver's `WorkflowGraphs.Decode`: one that does not read in this build, a kind it does not run
 * included, is unreadable with that reason, kept as it is; one that reads is decoded whole.
 */
export function decodeVersion(version: NamedVersion): DecodedVersion {
  return version.steps === null || version.problem !== null
    ? { version: version.version, identity: null, graph: null, graphDigest: null, unreadable: version.problem ?? `v${version.version}: it does not read.` }
    : decodeSteps(version.version, version.steps);
}
