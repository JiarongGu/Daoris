/**
 * A named graph as a named workflow's version, read (FABRIC-GRAPH1b; the fabric events design §E7, D157 §2.4, §5.2): a version of
 * schema 2 holds its `graph` and no `steps`, and a run bound to it binds the graph's own digest. The driver's twin is `WorkflowNamed`'s
 * `ReadVersion` with `NodeGraphs.Read` and `NodeGraphs.Digest` (`NodeGraphs.cs`); both read `test/fixtures/workflow-graph-versions.json`,
 * row for row, every digest included (`graphversions.test.ts`, `WorkflowGraphVersionsTests`).
 *
 * The driver judges a graph whole (`NodeGraphs.Problems`: bounds, entries, the end, the protected order, a node's declaration), which the
 * CLI does not hold, as for a decoded graph (`workflowgraphs.ts`). This reader holds what the digest needs, the graph's shape, and
 * says the driver's sentence where a place is not a graph's shape; the table's `judged: "driver"` rows are the driver's alone, and the
 * CLI never saves a graph version (`planAddVersion` refuses one, and says where it is saved).
 */
import { createHash } from 'node:crypto';

/** A node as the file declares it, each field as written, absence as its default. */
export interface FormNode {
  id: string; kind: string; version: number; reach: string; evidence: string | null; retry: { maxAttempts: number; delays: number[] } | null;
  timeoutSeconds: number | null; rejectable: boolean; skippable: boolean;
  inputs: Record<string, string>; outputs: Record<string, string>; options: Record<string, string>;
}

/** A wire's end: a node, and on a `data` wire its port. */
interface FormEnd { node: string; port: string | null }

/** A wire as the file declares it: its source a node, or what a `wait` waits on. */
export interface FormWire {
  id: string; kind: string; from: FormEnd | null; to: FormEnd;
  waits: { unit: string; revision: string; node: string | null; end: boolean; commitment: string | null } | null;
  group: { id: string; mode: string } | null;
}

/** A graph as a version holds it. */
export interface GraphForm {
  schemaVersion: number; nodes: FormNode[]; wires: FormWire[]; entries: string[]; end: string; cleanup: { until: string; days: number | null };
}

/** The schema of a version that is a graph; a version of steps is schema 1, named or not. */
export const GRAPH_SCHEMA = 2;

class Unread extends Error {
  constructor(where: string, what: string) {
    super(`${where} is not a graph's shape: it holds no ${what} there.`);
  }
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

const absent = (value: unknown): boolean => value === undefined || value === null;

function text(parent: Record<string, unknown>, name: string, where: string): string | null {
  const value = parent[name];
  if (absent(value)) return null;
  if (typeof value === 'string') return value;
  throw new Unread(`${where}'s \`${name}\``, 'text');
}

function whole(parent: Record<string, unknown>, name: string, where: string): number | null {
  const value = parent[name];
  if (absent(value)) return null;
  if (typeof value === 'number' && Number.isInteger(value) && value >= -2147483648 && value <= 2147483647) return value;
  throw new Unread(`${where}'s \`${name}\``, 'whole number');
}

function flag(parent: Record<string, unknown>, name: string, where: string): boolean | null {
  const value = parent[name];
  if (absent(value)) return null;
  if (typeof value === 'boolean') return value;
  throw new Unread(`${where}'s \`${name}\``, '`true` or `false`');
}

function list(parent: Record<string, unknown>, name: string, where: string): unknown[] {
  const value = parent[name];
  if (absent(value)) return [];
  if (Array.isArray(value)) return value;
  throw new Unread(`${where}'s \`${name}\``, 'list');
}

/**
 * A place the driver reads and this build cannot write as the driver does (FABRIC-GRAPH1b's review): an option's value enters the digest
 * as the driver's compact writer writes it, which parsed JSON cannot reproduce for a number (its own text, `1.0`, or past a double), an
 * object's keys that are numbers (JavaScript writes them first, by value), or a character outside the basic plane (the driver writes a
 * pair of escapes). Such a version is read as unreadable, and a file holding one is never written over by this build.
 */
class Lossy extends Error {
  option: string | null;
  node: string | null;

  constructor(option: string | null = null, node: string | null = null) {
    super('lossy');
    this.option = option;
    this.node = node;
  }

  /** The sentence for the node and option it is in. */
  says(): string {
    return `node \`${this.node}\`'s option \`${this.option}\` holds a value this build cannot write as the driver does (a number, a key that is a `
      + 'number, or a character outside ASCII and Chinese); the driver reads it.';
  }
}

/** A text this build and the driver's compact writer write alike: printable ASCII and the Chinese block, no more. */
const ALIKE = /^[ -~一-鿿]*$/;

/** An option's value as compact JSON, the driver's bytes, or a refusal where this build cannot write them alike. */
function compact(value: unknown): string {
  if (value === null) return 'null';
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (typeof value === 'string') {
    if (!ALIKE.test(value)) throw new Lossy();
    return JSON.stringify(value);
  }

  if (Array.isArray(value)) return `[${value.map(compact).join(',')}]`;
  if (isObject(value)) {
    return `{${Object.entries(value).map(([key, each]) => {
      if (!ALIKE.test(key) || /^(0|[1-9][0-9]*)$/.test(key)) throw new Lossy();
      return `${JSON.stringify(key)}:${compact(each)}`;
    }).join(',')}}`;
  }

  throw new Lossy();
}

/** A map of names to text, or, with `anyValue`, to each value's compact JSON (an option's). Its own keys only: `__proto__` is a name. */
function names(parent: Record<string, unknown>, name: string, where: string, anyValue = false): Record<string, string> {
  const map: Record<string, string> = Object.create(null) as Record<string, string>;
  const value = parent[name];
  if (absent(value)) return map;
  if (!isObject(value)) throw new Unread(`${where}'s \`${name}\``, 'object');
  for (const [key, each] of Object.entries(value)) {
    if (anyValue) {
      try {
        map[key] = compact(each);
      } catch (error) {
        if (error instanceof Lossy) throw new Lossy(key, null);
        throw error;
      }
    } else if (typeof each === 'string') map[key] = each;
    else throw new Unread(`${where}'s \`${name}.${key}\``, 'schema');
  }
  return map;
}

function nodeOf(raw: unknown, where: string): FormNode {
  if (!isObject(raw)) throw new Unread(where, 'node');
  if ('synthetic' in raw) throw new Unread(`${where}'s \`synthetic\``, "field a person's graph declares");
  let retry: FormNode['retry'] = null;
  if (!absent(raw['retry'])) {
    const declared = raw['retry'];
    if (!isObject(declared)) throw new Unread(`${where}'s \`retry\``, 'object');
    const here = `${where}'s \`retry\``;
    retry = {
      maxAttempts: whole(declared, 'maxAttempts', here) ?? (() => { throw new Unread(`${here}'s \`maxAttempts\``, 'whole number'); })(),
      delays: list(declared, 'delaysSeconds', here).map((delay) => {
        if (typeof delay === 'number' && Number.isInteger(delay)) return delay;
        throw new Unread(`${here}'s \`delaysSeconds\``, 'whole number');
      }),
    };
  }

  const need = <T>(value: T | null, name: string, what: string): T => {
    if (value === null) throw new Unread(`${where}'s \`${name}\``, what);
    return value;
  };
  let options: Record<string, string>;
  try {
    options = names(raw, 'options', where, true);
  } catch (error) {
    if (error instanceof Lossy) throw new Lossy(error.option, typeof raw['id'] === 'string' ? raw['id'] : where);
    throw error;
  }

  return {
    id: need(text(raw, 'id', where), 'id', 'text'),
    kind: need(text(raw, 'kind', where), 'kind', 'text'),
    version: need(whole(raw, 'version', where), 'version', 'whole number'),
    reach: need(text(raw, 'reach', where), 'reach', 'text'),
    evidence: text(raw, 'evidence', where),
    retry,
    timeoutSeconds: whole(raw, 'timeoutSeconds', where),
    rejectable: flag(raw, 'rejectable', where) ?? false,
    skippable: flag(raw, 'skippable', where) ?? true,
    inputs: names(raw, 'inputs', where),
    outputs: names(raw, 'outputs', where),
    options,
  };
}

/** A wire's end as the file names it: a node, or on a `data` wire its port after the last dot (`qa.evidence`). */
function endOf(named: string, kind: string): FormEnd {
  const dot = named.lastIndexOf('.');
  return kind === 'data' && dot > 0 && dot < named.length - 1 ? { node: named.slice(0, dot), port: named.slice(dot + 1) } : { node: named, port: null };
}

function wireOf(raw: unknown, where: string): FormWire {
  if (!isObject(raw)) throw new Unread(where, 'wire');
  const kind = text(raw, 'kind', where) ?? (() => { throw new Unread(`${where}'s \`kind\``, 'text'); })();
  const to = text(raw, 'to', where) ?? (() => { throw new Unread(`${where}'s \`to\``, 'node'); })();
  let from: FormEnd | null = null;
  let waits: FormWire['waits'] = null;
  const source = raw['from'];
  if (!absent(source)) {
    if (typeof source === 'string') from = endOf(source, kind);
    else if (isObject(source)) {
      const here = `${where}'s \`from\``;
      waits = { unit: text(source, 'unit', here) ?? '', revision: text(source, 'revision', here) ?? '', node: text(source, 'node', here),
        end: flag(source, 'end', here) ?? false, commitment: text(source, 'commitment', here) };
    } else throw new Unread(`${where}'s \`from\``, 'node, or what a wait waits on');
  }

  let group: FormWire['group'] = null;
  if (!absent(raw['group'])) {
    const declared = raw['group'];
    if (!isObject(declared)) throw new Unread(`${where}'s \`group\``, 'object');
    group = { id: text(declared, 'id', `${where}'s \`group\``) ?? '', mode: text(declared, 'mode', `${where}'s \`group\``) ?? '' };
  }

  return { id: text(raw, 'id', where) ?? '', kind, from, to: endOf(to, kind), waits, group };
}

/** A graph read from the version's JSON: whole, or the sentence saying where it is not a graph's shape. Never in part. */
export function readGraphForm(value: unknown): { graph: GraphForm | null; problem: string | null; lossy?: string } {
  try {
    if (!isObject(value)) throw new Unread('the graph', 'an object');
    const here = 'the graph';
    let cleanup: GraphForm['cleanup'] = { until: 'held', days: null };
    if (!absent(value['cleanup'])) {
      const declared = value['cleanup'];
      if (!isObject(declared)) throw new Unread("the graph's `cleanup`", 'object');
      cleanup = { until: text(declared, 'until', "the graph's `cleanup`") ?? 'held', days: whole(declared, 'days', "the graph's `cleanup`") };
    }

    return {
      problem: null,
      graph: {
        schemaVersion: whole(value, 'schemaVersion', here) ?? GRAPH_SCHEMA,
        nodes: list(value, 'nodes', here).map((node, index) => nodeOf(node, `nodes[${index}]`)),
        wires: list(value, 'wires', here).map((wire, index) => wireOf(wire, `wires[${index}]`)),
        entries: list(value, 'entries', here).map((entry, index) => {
          if (typeof entry === 'string') return entry;
          throw new Unread(`entries[${index}]`, "a node's id");
        }),
        end: text(value, 'end', here) ?? '',
        cleanup,
      },
    };
  } catch (error) {
    if (error instanceof Unread) return { graph: null, problem: error.message };
    if (error instanceof Lossy) return { graph: null, problem: null, lossy: error.says() };
    throw error;
  }
}

const ordinal = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

/**
 * A graph's digest — the driver's `NodeGraphs.Digest`, line for line, over every declaration: nodes and wires in id order, entries by
 * name, each node's ports and options by name, so the order they were written in never moves it. `gph-` and 32 hex.
 */
export function graphFormDigest(graph: GraphForm): string {
  let out = '';
  const line = (value: string | number | boolean | null): void => {
    out += (value === null ? '\\0'
      : typeof value === 'boolean' ? (value ? '1' : '0')
      : typeof value === 'number' ? String(value)
      : value.replaceAll('\\', '\\\\').replaceAll('\n', '\\n')) + '\n';
  };
  const map = (entries: Record<string, string>): void => {
    const pairs = Object.entries(entries).sort(([a], [b]) => ordinal(a, b));
    line(pairs.length);
    for (const [name, value] of pairs) {
      line(name);
      line(value);
    }
  };
  const end = (each: FormEnd | null): void => {
    line(each?.node ?? null);
    line(each?.port ?? null);
    line(null); // a wire end's unit, which a file never names
  };

  line('graph');
  line(graph.schemaVersion);
  line(graph.nodes.length);
  for (const node of [...graph.nodes].sort((a, b) => ordinal(a.id, b.id) || ordinal(a.kind, b.kind))) {
    line('node');
    line(node.id);
    line(node.kind);
    line(node.version);
    line(node.reach);
    line(false); // synthetic: a file declares none
    line(node.evidence);
    line(node.retry?.maxAttempts ?? null);
    line(node.retry === null ? null : node.retry.delays.join(','));
    line(node.timeoutSeconds);
    line(node.rejectable);
    line(node.skippable);
    map(node.inputs);
    map(node.outputs);
    map(node.options);
  }

  line(graph.wires.length);
  for (const wire of [...graph.wires].sort((a, b) => ordinal(a.id, b.id) || ordinal(a.kind, b.kind))) {
    line('wire');
    line(wire.id);
    line(wire.kind);
    end(wire.from);
    end(wire.to);
    line(wire.waits?.unit ?? null);
    line(wire.waits?.revision ?? null);
    line(wire.waits?.node ?? null);
    line(wire.waits?.end ?? null);
    line(wire.waits?.commitment ?? null);
    line(wire.group?.id ?? null);
    line(wire.group?.mode ?? null);
  }

  line(graph.entries.length);
  for (const entry of [...graph.entries].sort(ordinal)) line(entry);
  line(graph.end);
  line(graph.cleanup.until);
  line(graph.cleanup.days);
  return `gph-${createHash('sha256').update(out, 'utf8').digest('hex').slice(0, 32)}`;
}
