/**
 * A named graph as a named workflow's version, read (FABRIC-GRAPH1b; the fabric events design §E7, D157 §2.4, §5.2): a version of
 * schema 2 holds its `graph` and no `steps`, and a run bound to it binds the graph's own digest. The driver's twin is `WorkflowNamed`'s
 * `ReadVersion` with `NodeGraphs.Read` and `NodeGraphs.Digest` (`NodeGraphs.cs`); both read `test/fixtures/workflow-graph-versions.json`,
 * row for row, every digest included (`graphversions.test.ts`, `WorkflowGraphVersionsTests`).
 *
 * This reader holds what the digest needs, the graph's shape, read in the driver's order (`NodeGraphs.Parts`), each place that is not a
 * graph's shape said in the driver's sentence. `nodegraphs.ts` judges the graph whole (FABRIC-GRAPH1c), as `NodeGraphs.Problems` does.
 * An option's value enters the digest as the driver's compact writer writes it (D173's GRAPH1b choice 3): read by `parseWithSource`,
 * its numbers by their own text and its keys in the order written (FABRIC-GRAPH1f); its text in ASCII and Chinese, and the escapes the
 * driver writes for a control character and a character past the basic plane. Any other value is one this build cannot write as the
 * driver does, and says so (`lossy`).
 */
import { createHash } from 'node:crypto';
import { numberText, sourceOf } from './jsonsource.ts';

/** A node as the file declares it, each field as written, absence as its default. */
export interface FormNode {
  id: string; kind: string; version: number; reach: string; evidence: string | null; retry: { maxAttempts: number; delays: number[] } | null;
  timeoutSeconds: number | null; rejectable: boolean; skippable: boolean;
  inputs: Record<string, string>; outputs: Record<string, string>; options: Record<string, string>;
  /** Made by decoding, never authored (the driver's `NodeDeclaration.Synthetic`): a file declares none. */
  synthetic?: boolean;
}

/** A wire's end: a node, on a `data` wire its port, and another unit's node where a wire names one (never read from a file). */
export interface FormEnd { node: string; port: string | null; unit?: string | null }

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

/** What a graph's or a cycle's JSON declares, each part as written: an absent end or cleanup is null — the driver's `GraphParts`. */
export interface GraphParts {
  schemaVersion: number; nodes: FormNode[]; wires: FormWire[]; entries: string[]; end: string | null; cleanup: GraphForm['cleanup'] | null;
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

/**
 * A whole number as the driver's `TryGetInt32` reads it: within 32 bits, and where the text was kept, written as one — `1.0` and `1e2`
 * are numbers the driver reads as no whole number.
 */
function isWhole(value: unknown, holder: unknown, key: string | number): value is number {
  const own = numberText(holder, key);
  return typeof value === 'number' && Number.isInteger(value) && value >= -2147483648 && value <= 2147483647
    && (own === undefined || /^-?[0-9]+$/.test(own));
}

function whole(parent: Record<string, unknown>, name: string, where: string): number | null {
  const value = parent[name];
  if (absent(value)) return null;
  if (isWhole(value, parent, name)) return value;
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

/** Why an option's value cannot be written as the driver's compact writer writes it. */
class Lossy extends Error {}

/** A JSON escape's hex, as the driver's writer spells it: four upper-case digits. */
const hex = (unit: number): string => `\\u${unit.toString(16).toUpperCase().padStart(4, '0')}`;

const SHORT: Record<string, string> = { '\b': '\\b', '\t': '\\t', '\n': '\\n', '\f': '\\f', '\r': '\\r', '"': '\\"', '\\': '\\\\' };

/**
 * A text as the driver's compact writer escapes it (`JavaScriptEncoder.UnsafeRelaxedJsonEscaping`): printable ASCII and the Chinese
 * block as they are, a quote and a backslash and the short controls by their short escapes, any other control and a character past
 * the basic plane by `\u` escapes, the latter as its two halves. Any other character is the driver's own Unicode table's to escape or
 * not, which this build does not hold: refused.
 */
function compactText(value: string): string {
  let out = '"';
  for (const ch of value) {
    const code = ch.codePointAt(0)!;
    if (SHORT[ch] !== undefined) out += SHORT[ch];
    else if (code < 0x20 || code === 0x7f) out += hex(code);
    else if (code < 0x7f || (code >= 0x4e00 && code <= 0x9fff)) out += ch;
    else if (code > 0xffff) out += hex(ch.charCodeAt(0)) + hex(ch.charCodeAt(1));
    else throw new Lossy();
  }
  return `${out}"`;
}

/**
 * An option's value as compact JSON, the driver's bytes: a number by its own text and an object's keys in the order written, where
 * `parseWithSource` read them; or a refusal where this build cannot write them alike — a number or a key that is a number read with no
 * record of its text, a key written twice, a character `compactText` refuses.
 */
function compact(value: unknown, holder: unknown, key: string | number): string {
  if (value === null) return 'null';
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (typeof value === 'string') return compactText(value);
  if (typeof value === 'number') {
    const own = numberText(holder, key);
    if (own === undefined) throw new Lossy();
    return own;
  }

  if (Array.isArray(value)) return `[${value.map((each, index) => compact(each, value, index)).join(',')}]`;
  if (isObject(value)) {
    const source = sourceOf(value);
    if (source?.twice === true) throw new Lossy();
    const keys = source?.keys ?? Object.keys(value);
    if (source === undefined && keys.some((name) => /^(0|[1-9][0-9]*)$/.test(name))) throw new Lossy();
    return `{${keys.map((name) => `${compactText(name)}:${compact(value[name], value, name)}`).join(',')}}`;
  }

  throw new Lossy();
}

/** The sentence for an option this build cannot write as the driver does. */
const lossySaid = (node: string, option: string): string =>
  `node \`${node}\`'s option \`${option}\` holds a value this build cannot write as the driver does (a character outside ASCII and `
  + 'Chinese, a key written twice, or a number read with no record of its text); the driver reads it.';

/** A map of names to text, or, with `anyValue`, to each value's compact JSON (an option's). Its own keys only: `__proto__` is a name. */
function names(parent: Record<string, unknown>, name: string, where: string, anyValue: ((option: string) => void) | null = null): Record<string, string> {
  const map: Record<string, string> = Object.create(null) as Record<string, string>;
  const value = parent[name];
  if (absent(value)) return map;
  if (!isObject(value)) throw new Unread(`${where}'s \`${name}\``, 'object');
  for (const [key, each] of Object.entries(value)) {
    if (anyValue !== null) {
      try {
        map[key] = compact(each, value, key);
      } catch (error) {
        if (!(error instanceof Lossy)) throw error;
        // Said once the whole graph has read, so a place that is not a graph's shape is said first, as the driver says it.
        anyValue(key);
        map[key] = '';
      }
    } else if (typeof each === 'string') map[key] = each;
    else throw new Unread(`${where}'s \`${name}.${key}\``, 'schema');
  }
  return map;
}

/** A node, its fields read in the driver's order (`NodeGraphs.Node`): its retry, then id, kind, version, reach, ports, options and the rest. */
function nodeOf(raw: unknown, where: string, lossy: (said: string) => void): FormNode {
  if (!isObject(raw)) throw new Unread(where, 'node');
  if ('synthetic' in raw) throw new Unread(`${where}'s \`synthetic\``, "field a person's graph declares");
  let retry: FormNode['retry'] = null;
  if (!absent(raw['retry'])) {
    const declared = raw['retry'];
    const here = `${where}'s \`retry\``;
    if (!isObject(declared)) throw new Unread(here, 'object');
    const maxAttempts = whole(declared, 'maxAttempts', here) ?? (() => { throw new Unread(`${here}'s \`maxAttempts\``, 'whole number'); })();
    const delays = list(declared, 'delaysSeconds', here);
    retry = {
      maxAttempts,
      delays: delays.map((delay, index) => {
        if (isWhole(delay, delays, index)) return delay;
        throw new Unread(`${here}'s \`delaysSeconds\``, 'whole number');
      }),
    };
  }

  const need = <T>(value: T | null, name: string, what: string): T => {
    if (value === null) throw new Unread(`${where}'s \`${name}\``, what);
    return value;
  };
  const id = need(text(raw, 'id', where), 'id', 'text');
  const kind = need(text(raw, 'kind', where), 'kind', 'text');
  const version = need(whole(raw, 'version', where), 'version', 'whole number');
  const reach = need(text(raw, 'reach', where), 'reach', 'text');
  const inputs = names(raw, 'inputs', where);
  const outputs = names(raw, 'outputs', where);
  const options = names(raw, 'options', where, (option) => lossy(lossySaid(id, option)));
  return {
    id, kind, version, reach, inputs, outputs, options,
    evidence: text(raw, 'evidence', where),
    retry,
    timeoutSeconds: whole(raw, 'timeoutSeconds', where),
    rejectable: flag(raw, 'rejectable', where) ?? false,
    skippable: flag(raw, 'skippable', where) ?? true,
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

/**
 * What a graph's or a cycle's JSON declares, read in the driver's order (`NodeGraphs.Parts`: its schema, nodes, wires, entries, end,
 * then cleanup), or the sentence saying the first place that is not a graph's shape. Never read in part. `lossy` names the first option
 * this build cannot write as the driver does: the parts still read, and are judged, but no digest is taken of them.
 */
export function readGraphParts(value: unknown): { parts: GraphParts | null; problem: string | null; lossy: string | null } {
  let lossy: string | null = null;
  try {
    if (!isObject(value)) throw new Unread('the graph', 'an object');
    const here = 'the graph';
    const schemaVersion = whole(value, 'schemaVersion', here) ?? GRAPH_SCHEMA;
    const nodes = list(value, 'nodes', here).map((node, index) => nodeOf(node, `nodes[${index}]`, (said) => { lossy ??= said; }));
    const wires = list(value, 'wires', here).map((wire, index) => wireOf(wire, `wires[${index}]`));
    const entries = list(value, 'entries', here).map((entry, index) => {
      if (typeof entry === 'string') return entry;
      throw new Unread(`entries[${index}]`, "a node's id");
    });
    const end = text(value, 'end', here);
    let cleanup: GraphParts['cleanup'] = null;
    if (!absent(value['cleanup'])) {
      const declared = value['cleanup'];
      if (!isObject(declared)) throw new Unread("the graph's `cleanup`", 'object');
      cleanup = { until: text(declared, 'until', "the graph's `cleanup`") ?? 'held', days: whole(declared, 'days', "the graph's `cleanup`") };
    }

    return { parts: { schemaVersion, nodes, wires, entries, end, cleanup }, problem: null, lossy };
  } catch (error) {
    if (error instanceof Unread) return { parts: null, problem: error.message, lossy: null };
    throw error;
  }
}

/** A graph read from the version's JSON: whole, or the sentence saying where it is not a graph's shape. Never in part. */
export function readGraphForm(value: unknown): { graph: GraphForm | null; problem: string | null; lossy?: string } {
  const { parts, problem, lossy } = readGraphParts(value);
  if (parts === null) return { graph: null, problem };
  if (lossy !== null) return { graph: null, problem: null, lossy };
  return { graph: { ...parts, end: parts.end ?? '', cleanup: parts.cleanup ?? { until: 'held', days: null } }, problem: null };
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
    line(each?.unit ?? null);
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
    line(node.synthetic === true);
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
