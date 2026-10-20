/**
 * A graph judged whole when it is saved (FABRIC-GRAPH1c, FABRIC-GRAPH1k; the fabric events design §E3, §E7): the declarations' checks,
 * the wires' (WIRE1a's 21 codes) and the graph's own (GRAPH1's, GRAPH1g's `observed-twice` and GRAPH1i's `protected-wire` among them),
 * every problem named at once in a stable order, each with its code, the wire and the node it is about, and its words. A landing rule's
 * cycle is judged after its rule's landing. Pure: nothing evaluated, nothing stored.
 *
 * The driver's twin is `NodeGraphs.Problems` with `NodeWires.Problems`, `NodeDeclarations.Problem` and `NodeGraphHomes.OfLanding`
 * (`NodeGraphs.cs`, `NodeWires.cs`, `NodeContract.cs`); both read `test/fixtures/graph-problems.json`, row for row, every problem's words
 * included (`graphproblems.test.ts`, `NodeWiresTests`, `NodeGraphsTests`). The codes and the built-ins' authorities are duplicated
 * deliberately, and the test holds them to the driver's source.
 */
import { readGraphParts, type FormEnd, type FormNode, type FormWire, type GraphForm } from './graphversions.ts';

/** One thing a save refuses — the driver's `WireProblem`: a stable code, the wire or node it is about, and its words. */
export interface GraphProblem { code: string; wire: string | null; node: string | null; words: string }

/** Why a save refuses a wire set (§E3) — the driver's `WireRefusal`, in its order. */
export const WIRE_REFUSALS: readonly string[] = [
  'no-id', 'wire-twice', 'node-twice', 'unknown-kind', 'shape', 'foreign-node', 'missing-node', 'missing-port', 'incompatible-schema',
  'not-wait-unit', 'wait-target', 'no-wait', 'wait-twice', 'input-unfed', 'input-twice', 'grouped-input', 'group-mode', 'group-split',
  'grouped-member', 'never-fails', 'never-rejected', 'cycle',
];

/** Why a save refuses a graph (§E7), beside the wires' codes — the driver's `GraphRefusal`, in its order. */
export const GRAPH_REFUSALS: readonly string[] = [
  'unreadable', 'schema-version', 'nodes-bound', 'wires-bound', 'synthetic-bound', 'declaration', 'landing-kind', 'into-landing', 'no-entry',
  'entry', 'entry-wired', 'end', 'end-kind', 'unreachable', 'protected-order', 'protected-wire', 'observed-twice', 'close-end', 'after-close',
  'closure-deadlock', 'cleanup',
];

/** The schema a graph is saved in — the driver's `NodeGraphs.SchemaVersion`. */
const SCHEMA_VERSION = 2;
/** The authored nodes a graph holds at most — the driver's `NodeGraphs.MaxNodes`, D157's 24 steps. */
const MAX_NODES = 24;
/** The authored wires a graph holds at most — the driver's `NodeGraphs.MaxWires`. */
const MAX_WIRES = 96;
/** The synthetic nodes, and wires to or from one, a graph holds at most for each authored node — `SyntheticPerNode`, `SyntheticWiresPerNode`. */
const SYNTHETIC_PER_NODE = 3;
const SYNTHETIC_WIRES_PER_NODE = 4;

const WIRE_KINDS: readonly string[] = ['after', 'data', 'wait', 'trigger', 'failed', 'rejected'];
/** The kinds that make a node eligible, which its groups gather — the driver's `WireKinds.Eligibility`. */
const ELIGIBILITY: readonly string[] = ['after', 'trigger', 'failed', 'rejected'];
const GROUP_MODES: readonly string[] = ['allOf', 'anyOf'];

/** Who may report each outcome of a built-in kind (§E2's authority table), as far as a save asks — the driver's `NodeAuthorities.BuiltIn`. */
export interface Authority { reach: string; fails: boolean; judges: boolean; mayEnd: boolean }

export const BUILT_IN_AUTHORITIES: Readonly<Record<string, Authority>> = {
  claimed: { reach: 'observed', fails: false, judges: false, mayEnd: false },
  handed: { reach: 'observed', fails: false, judges: false, mayEnd: true },
  line: { reach: 'observed', fails: false, judges: false, mayEnd: true },
  pushed: { reach: 'observed', fails: true, judges: false, mayEnd: true },
  'pull-request': { reach: 'observed', fails: true, judges: false, mayEnd: true },
  human: { reach: 'pressed', fails: false, judges: true, mayEnd: true },
  look: { reach: 'observed', fails: false, judges: true, mayEnd: true },
  'go-ahead': { reach: 'pressed', fails: false, judges: true, mayEnd: true },
  'wait.unit': { reach: 'waited', fails: true, judges: false, mayEnd: true },
  'quest.close': { reach: 'performed', fails: true, judges: false, mayEnd: true },
  all: { reach: 'observed', fails: true, judges: false, mayEnd: true },
  opinion: { reach: 'observed', fails: false, judges: false, mayEnd: true },
  stop: { reach: 'observed', fails: false, judges: false, mayEnd: false },
};

/** The built-ins observed from records, whose evidence is their record's — the driver's `BuiltInNodes.Observed`. */
const OBSERVED: readonly string[] = ['claimed', 'handed', 'pushed', 'pull-request', 'line'];
/** The kinds an end is at or before the line by: the cleanup waits for no such end (§C2, §C3). */
const AT_OR_BEFORE_THE_LINE: readonly string[] = ['claimed', 'handed', 'pushed', 'pull-request', 'line'];
const ORDER = "(D157 §2.2's order: holds, opinion, look, landing)";

const ordinal = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

/**
 * .NET's `char.IsWhiteSpace`, which the driver's blank checks ask: the controls tab to carriage return, U+0085, and the space, line and
 * paragraph separators. By their codes, so no separator is written into this file.
 */
const WHITE: ReadonlySet<number> = new Set([
  0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x20, 0x85, 0xa0, 0x1680, 0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008, 0x2009,
  0x200a, 0x2028, 0x2029, 0x202f, 0x205f, 0x3000,
]);
const isWhite = (ch: string): boolean => WHITE.has(ch.charCodeAt(0));
const blank = (value: string | null | undefined): boolean => value === null || value === undefined || [...value].every(isWhite);

/** Whether a kind is a plugin's own node — the driver's `NodeKinds.IsPlugin`: `<plugin>/<node>`, both parts named, no space. */
export function isPluginKind(kind: string): boolean {
  const slash = kind.indexOf('/');
  return slash > 0 && slash < kind.length - 1 && kind.indexOf('/', slash + 1) < 0 && ![...kind].some(isWhite);
}

/** A node's authority: its built-in kind's, or a plugin's own from its declaration; null for a kind this build does not know. */
function authorityOf(node: FormNode): Authority | null {
  if (Object.hasOwn(BUILT_IN_AUTHORITIES, node.kind)) return BUILT_IN_AUTHORITIES[node.kind]!;
  return isPluginKind(node.kind) ? { reach: node.reach, fails: true, judges: node.rejectable, mayEnd: true } : null;
}

/**
 * What does not hold in a declaration, by its field, or null — the driver's `NodeDeclarations.Problem`. A graph read from a file declares
 * no `perform`, so the checks of one are the driver's alone, and a plugin's retry of more than one attempt is one nothing could take.
 */
export function declarationProblem(node: FormNode): string | null {
  if (blank(node.id)) return 'id';
  const authority = authorityOf(node);
  if (authority === null) return 'unknown-kind';
  const builtIn = Object.hasOwn(BUILT_IN_AUTHORITIES, node.kind);
  if (builtIn ? node.reach !== authority.reach : !(node.reach === 'observed' || node.reach === 'performed')) return 'reach';
  if (node.version < 1) return 'version';
  if (node.retry !== null && (node.retry.maxAttempts < 1 || node.retry.delays.some((delay) => delay < 0) || !authority.fails)) return 'retry';
  if (node.timeoutSeconds !== null && node.timeoutSeconds <= 0) return 'timeoutSeconds';
  if (Object.hasOwn(node.outputs, 'evidence')) return 'outputs';
  if (builtIn && node.rejectable) return 'rejectable';
  if (builtIn && !node.skippable) return 'skippable';
  if (!builtIn && node.retry !== null && node.retry.maxAttempts > 1) return 'retry';
  return null;
}

/** The schema a `data` wire takes from a port: a declared output's, or its evidence's — the driver's `NodeWires.SourceSchema`. */
function sourceSchema(node: FormNode, port: string): string | null {
  if (port === 'evidence') return node.evidence ?? (OBSERVED.includes(node.kind) ? `daoris/${node.kind}@1` : null);
  return Object.hasOwn(node.outputs, port) ? node.outputs[port]! : null;
}

const into = (wire: FormWire, node: string): boolean => !wire.to.unit && wire.to.node === node;
const foreign = (end: FormEnd | null): end is FormEnd => end !== null && end.unit !== undefined && end.unit !== null;

/** What does not fit the kind in a wire's ends, or null — the driver's `NodeWires.Shape`. */
function shapeProblem(wire: FormWire): string | null {
  if (wire.kind === 'wait') {
    return wire.from !== null ? 'a `wait` wire waits on another unit\'s work, never on a node of this graph.'
      : wire.waits === null ? 'a `wait` wire names what it waits on.'
      : wire.to.port !== null ? 'only a `data` wire names ports.'
      : null;
  }

  if (wire.waits !== null) return 'only a `wait` wire names what it waits on.';
  if (wire.from === null) return `a \`${wire.kind}\` wire needs its source node.`;
  if (wire.kind === 'data') {
    return blank(wire.from.port) || blank(wire.to.port) ? 'a `data` wire names its source\'s output and its target\'s input.' : null;
  }

  return wire.from.port !== null || wire.to.port !== null ? `only a \`data\` wire names ports; a \`${wire.kind}\` wire joins nodes.` : null;
}

/** One wire's first problem, or null; a sound grouped wire's group is noted at its first wire, for the next to agree with. */
function wireProblem(wire: FormWire, nodes: Map<string, FormNode>, ids: Set<string>, groups: Map<string, FormWire>): GraphProblem | null {
  const id = wire.id;
  const refuse = (code: string, node: string | null, words: string): GraphProblem => ({ code, wire: id, node, words: `wire \`${id}\`: ${words}` });

  if (blank(id)) return { code: 'no-id', wire: id, node: null, words: 'a wire has no id: its id is what a witness and a trigger\'s receipt are keyed on.' };
  if (ids.has(id)) return refuse('wire-twice', null, 'another wire has this id; each wire\'s id is its own.');
  ids.add(id);
  if (!WIRE_KINDS.includes(wire.kind)) {
    return refuse('unknown-kind', null, `\`${wire.kind}\` is no wire kind; a wire is \`after\`, \`data\`, \`wait\`, \`trigger\`, \`failed\` or \`rejected\`.`);
  }

  const shape = shapeProblem(wire);
  if (shape !== null) return refuse('shape', null, shape);
  for (const end of [wire.from, wire.to]) {
    if (foreign(end)) {
      return refuse('foreign-node', end.node, `\`${end.node}\` is another unit's node; a dependency on other work ends at a local \`wait.unit\`, through a commitment.`);
    }
  }

  for (const end of [wire.from, wire.to]) {
    if (end !== null && !nodes.has(end.node)) return refuse('missing-node', end.node, `there is no node \`${end.node}\` in this graph.`);
  }

  const to = nodes.get(wire.to.node)!;
  const from = wire.from === null ? null : nodes.get(wire.from.node)!;
  switch (wire.kind) {
    case 'wait': {
      if (to.kind !== 'wait.unit') return refuse('not-wait-unit', to.id, `a \`wait\` wire gives a \`wait.unit\` what it waits on, and \`${to.id}\` is a \`${to.kind}\`.`);
      const waits = wire.waits;
      if (waits === null || blank(waits.unit) || blank(waits.revision) || blank(waits.node) !== waits.end) {
        return refuse('wait-target', to.id, `\`${to.id}\` waits on a unit at its agreed revision, and on one of its nodes or its end.`);
      }
      break;
    }

    case 'data': {
      const output = wire.from!.port!;
      const input = wire.to.port!;
      const produced = sourceSchema(from!, output);
      if (produced === null) {
        return refuse('missing-port', from!.id, output === 'evidence' ? `\`${from!.id}\` declares no evidence to pass on.` : `\`${from!.id}\` has no output \`${output}\`.`);
      }
      if (!Object.hasOwn(to.inputs, input)) return refuse('missing-port', to.id, `\`${to.id}\` has no input \`${input}\`.`);
      const taken = to.inputs[input]!;
      if (produced !== taken) {
        return refuse('incompatible-schema', to.id, `\`${from!.id}\`'s \`${output}\` is \`${produced}\`, and \`${to.id}\`'s input \`${input}\` takes \`${taken}\`.`);
      }
      break;
    }

    case 'failed': {
      const authority = authorityOf(from!);
      if (authority !== null && !authority.fails) {
        return refuse('never-fails', from!.id, `nothing can fail \`${from!.id}\`, a \`${from!.kind}\`: not yet is pending, so this path is never taken.`);
      }
      break;
    }

    case 'rejected': {
      const authority = authorityOf(from!);
      if (authority !== null && !authority.judges) {
        return refuse('never-rejected', from!.id, `no person may reject \`${from!.id}\`, a \`${from!.kind}\`, so this path is never taken.`);
      }
      break;
    }
  }

  const group = wire.group;
  if (group === null) return null;
  if (!ELIGIBILITY.includes(wire.kind)) {
    return refuse('grouped-input', to.id,
      `a \`${wire.kind}\` wire is \`${to.id}\`'s input, mandatory whatever the groups say; only \`after\`, \`trigger\`, \`failed\` and \`rejected\` wires are grouped.`);
  }

  // FABRIC-WIRE1d: every `after` wire into a join is a member whatever its group, so a group there says nothing the evaluation keeps.
  if (wire.kind === 'after' && to.kind === 'all') {
    return refuse('grouped-member', to.id, `\`${to.id}\` is an \`all\` join, and every \`after\` wire into it is a member whatever its group; its members are wired plain.`);
  }

  if (blank(group.id) || !GROUP_MODES.includes(group.mode)) return refuse('group-mode', to.id, `a group into \`${to.id}\` has an id, and is \`allOf\` or \`anyOf\`.`);
  const first = groups.get(group.id);
  if (first === undefined) {
    groups.set(group.id, wire);
    return null;
  }

  if (first.to.node !== to.id) {
    return refuse('group-split', to.id, `group \`${group.id}\` gathers wires into \`${first.to.node}\`, and a group is one node's, never \`${to.id}\`'s as well.`);
  }

  return first.group!.mode !== group.mode ? refuse('group-mode', to.id, `group \`${group.id}\` into \`${to.id}\` is \`${first.group!.mode}\` already; one group has one mode.`) : null;
}

/** The wires between two of this graph's nodes, a source named: what a cycle, a reach and the order are read over. */
function localWires(nodes: Map<string, FormNode>, wires: readonly FormWire[]): FormWire[] {
  return wires.filter((wire) => wire.kind !== 'wait' && WIRE_KINDS.includes(wire.kind) && wire.from !== null && !wire.from.unit
    && nodes.has(wire.from.node) && !wire.to.unit && nodes.has(wire.to.node));
}

/** Each local dependency cycle (§E3), a node's own loop included, once, named by its nodes in id order — the driver's `NodeWires.Cycles`. */
function cycles(nodes: Map<string, FormNode>, wires: readonly FormWire[]): GraphProblem[] {
  const edges = localWires(nodes, wires);
  const next = new Map<string, string[]>();
  for (const wire of edges) next.set(wire.from!.node, [...(next.get(wire.from!.node) ?? []), wire.to.node]);

  // Tarjan's strongly connected components, visited in the driver's order: each of more than a node, or a node wired to itself, is a cycle.
  const index = new Map<string, number>();
  const low = new Map<string, number>();
  const stack: string[] = [];
  const onStack = new Set<string>();
  const components: string[][] = [];
  const visit = (node: string): void => {
    index.set(node, index.size);
    low.set(node, index.get(node)!);
    stack.push(node);
    onStack.add(node);
    for (const to of next.get(node) ?? []) {
      if (!index.has(to)) {
        visit(to);
        low.set(node, Math.min(low.get(node)!, low.get(to)!));
      } else if (onStack.has(to)) {
        low.set(node, Math.min(low.get(node)!, index.get(to)!));
      }
    }

    if (low.get(node) !== index.get(node)) return;
    const component: string[] = [];
    let member: string;
    do {
      member = stack.pop()!;
      onStack.delete(member);
      component.push(member);
    } while (member !== node);
    components.push(component);
  };

  for (const node of [...nodes.keys()].sort(ordinal)) if (!index.has(node)) visit(node);

  const problems: GraphProblem[] = [];
  for (const members of components.map((component) => [...component].sort(ordinal)).sort((a, b) => ordinal(a[0]!, b[0]!))) {
    const inside = edges.filter((wire) => members.includes(wire.from!.node) && members.includes(wire.to.node)).map((wire) => wire.id).sort(ordinal);
    if (members.length === 1 && inside.length === 0) continue;
    problems.push({ code: 'cycle', wire: null, node: members[0]!,
      words: `${members.length === 1 ? 'node' : 'nodes'} ${members.map((member) => `\`${member}\``).join(', ')}: `
        + `${members.length === 1 ? 'its own wire comes back to it' : 'they wait on each other'} through ${inside.map((wire) => `\`${wire}\``).join(', ')}; `
        + 'a graph\'s wires never come back, and a return is its adapter\'s, bounded.' });
  }
  return problems;
}

/**
 * Everything a save refuses in the wires over the nodes, in a stable order — the driver's `NodeWires.Problems`: the nodes' ids; each wire
 * in the order given, by its first problem; each node's inputs and wait, in the order given; then each local cycle, by its first node.
 */
export function wireProblems(nodes: readonly FormNode[], wires: readonly FormWire[]): GraphProblem[] {
  const problems: GraphProblem[] = [];
  const byId = new Map<string, FormNode>();
  const distinct: FormNode[] = [];
  for (const node of nodes) {
    if (!byId.has(node.id)) {
      byId.set(node.id, node);
      distinct.push(node);
    } else {
      problems.push({ code: 'node-twice', wire: null, node: node.id, words: `node \`${node.id}\`: another node has this id, so a wire naming it would name either.` });
    }
  }

  const ids = new Set<string>();
  const groups = new Map<string, FormWire>();
  for (const wire of wires) {
    const problem = wireProblem(wire, byId, ids, groups);
    if (problem !== null) problems.push(problem);
  }

  for (const node of distinct) {
    // The driver keeps a declaration's inputs in name order.
    for (const input of Object.keys(node.inputs).sort(ordinal)) {
      const feeding = wires.filter((wire) => wire.kind === 'data' && into(wire, node.id) && wire.to.port === input);
      if (feeding.length === 0) {
        problems.push({ code: 'input-unfed', wire: null, node: node.id, words: `node \`${node.id}\`: its input \`${input}\` has no \`data\` wire; every input is mandatory.` });
      }
      for (const wire of feeding.slice(1)) {
        problems.push({ code: 'input-twice', wire: wire.id, node: node.id,
          words: `wire \`${wire.id}\`: \`${node.id}\`'s input \`${input}\` is fed by \`${feeding[0]!.id}\` already; alternative producers need an explicitly typed choice input.` });
      }
    }

    if (node.kind !== 'wait.unit') continue;
    const waits = wires.filter((wire) => wire.kind === 'wait' && into(wire, node.id));
    if (waits.length === 0) {
      problems.push({ code: 'no-wait', wire: null, node: node.id, words: `node \`${node.id}\`: a \`wait.unit\` waits on what a \`wait\` wire names, and none does.` });
    }
    for (const wire of waits.slice(1)) {
      problems.push({ code: 'wait-twice', wire: wire.id, node: node.id,
        words: `wire \`${wire.id}\`: \`${node.id}\` waits on what \`${waits[0]!.id}\` names already; a \`wait.unit\` waits on one thing, and several are joined by groups.` });
    }
  }

  problems.push(...cycles(byId, wires));
  return problems;
}

/** One of a node's incoming eligibility groups — the driver's `WireGroupOf`: a declared group, or a wire with none (`id` null). */
interface GroupOf { id: string | null; mode: string; wires: string[] }

/** A node's incoming eligibility groups, each declared group once, each wire with none a group of its own — `NodeWires.Groups`. */
function groupsOf(node: string, wires: readonly FormWire[]): GroupOf[] {
  const gathered = new Map<string, FormWire[]>();
  for (const wire of wires) {
    if (!ELIGIBILITY.includes(wire.kind) || !into(wire, node)) continue;
    const key = wire.group !== null && !blank(wire.group.id) ? `group\n${wire.group.id}` : `wire\n${wire.id}`;
    gathered.set(key, [...(gathered.get(key) ?? []), wire]);
  }
  return [...gathered.values()].map((members) => {
    const declared = members[0]!.group;
    const ids = members.map((wire) => wire.id).sort(ordinal);
    return declared !== null && !blank(declared.id) ? { id: declared.id, mode: declared.mode, wires: ids } : { id: null, mode: 'allOf', wires: ids };
  }).sort((a, b) => ordinal(a.wires[0]!, b.wires[0]!));
}

/** The nodes reached from `from` along `wires`, forward or back, the starts included, in the order reached. */
function reach(from: readonly string[], wires: readonly FormWire[], forward: boolean): Set<string> {
  const next = new Map<string, string[]>();
  for (const wire of wires) {
    const [a, b] = forward ? [wire.from!.node, wire.to.node] : [wire.to.node, wire.from!.node];
    next.set(a, [...(next.get(a) ?? []), b]);
  }
  const reached = new Set(from);
  const queue = [...reached];
  while (queue.length > 0) {
    for (const to of next.get(queue.shift()!) ?? []) {
      if (!reached.has(to)) {
        reached.add(to);
        queue.push(to);
      }
    }
  }
  return reached;
}

/** The nodes something may fail: each whose kind's authority names who fails it, and each join a member of which may — `MayFail`. */
function mayFail(byId: Map<string, FormNode>, local: readonly FormWire[]): Set<string> {
  const may = new Set([...byId.values()].filter((node) => node.kind !== 'all' && authorityOf(node)?.fails === true).map((node) => node.id));
  for (let moved = true; moved;) {
    moved = false;
    for (const join of [...byId.values()].filter((node) => node.kind === 'all' && !may.has(node.id))) {
      if (local.some((wire) => wire.kind === 'after' && wire.to.node === join.id && may.has(wire.from!.node))) {
        may.add(join.id);
        moved = true;
      }
    }
  }
  return may;
}

/**
 * The nodes that can start while no look has passed (FABRIC-GRAPH1's review, finding 4) — the driver's `StartsWithNoLook`: the entries,
 * and each node whose groups and inputs can all be met so, a join started once every member is met and failing once one that may fail
 * has started or failed.
 */
function startsWithNoLook(byId: Map<string, FormNode>, starts: readonly string[], local: readonly FormWire[]): Set<string> {
  const started = new Set(starts);
  const failing = new Set<string>();
  const may = mayFail(byId, local);
  const met = (wire: FormWire): boolean => {
    const from = wire.from!.node;
    if (wire.kind === 'failed') return may.has(from) && (started.has(from) || failing.has(from));
    if (wire.kind === 'rejected') return started.has(from) || failing.has(from);
    return started.has(from) && byId.get(from)!.kind !== 'look';
  };
  for (let moved = true; moved;) {
    moved = false;
    for (const node of [...byId.values()].filter((each) => !started.has(each.id))) {
      const join = node.kind === 'all';
      const members = join ? local.filter((wire) => wire.kind === 'after' && wire.to.node === node.id) : [];
      const groups = groupsOf(node.id, join ? local.filter((wire) => !members.includes(wire)) : local);
      const inputs = local.filter((wire) => wire.kind === 'data' && wire.to.node === node.id);
      if (groups.length + inputs.length + members.length === 0 || !inputs.every(met)) continue;
      // A wire id twice is refused above (`wire-twice`); its first is read here, as a group reads it.
      const metBy = (id: string): boolean => met(local.find((wire) => wire.id === id && wire.to.node === node.id)!);
      if (!groups.every((group) => (group.mode === 'anyOf' ? group.wires.some(metBy) : group.wires.every(metBy)))) continue;
      if (members.every(met)) {
        if (!started.has(node.id)) {
          started.add(node.id);
          moved = true;
        }
      } else if (members.some((member) => may.has(member.from!.node) && (started.has(member.from!.node) || failing.has(member.from!.node)))) {
        if (!failing.has(node.id)) {
          failing.add(node.id);
          moved = true;
        }
      }
    }
  }
  return started;
}

/**
 * Everything a save refuses in a graph, in a stable order; empty when it holds — the driver's `NodeGraphs.Problems`: the schema and the
 * bounds; each node's declaration; the wires'; the entries; the end; each node no entry reaches; the protected order, then its wires
 * (FABRIC-GRAPH1i); two observed nodes of one plugin's step (FABRIC-GRAPH1g, asked of a graph a person saves); the closure's rules; the
 * cleanup.
 */
export function graphProblems(graph: GraphForm, saved = true): GraphProblem[] {
  const problems: GraphProblem[] = [];
  const say = (code: string, wire: string | null, node: string | null, words: string): void => {
    problems.push({ code, wire, node, words });
  };

  if (graph.schemaVersion !== SCHEMA_VERSION) {
    say('schema-version', null, null, `the graph is schema ${graph.schemaVersion}, and this build reads schema ${SCHEMA_VERSION}.`);
  }

  const synthetic = new Set(graph.nodes.filter((node) => node.synthetic === true).map((node) => node.id));
  const authored = graph.nodes.filter((node) => node.synthetic !== true).length;
  const syntheticNodes = graph.nodes.length - authored;
  const syntheticWires = graph.wires.filter((wire) => synthetic.has(wire.to.node) || (wire.from !== null && synthetic.has(wire.from.node))).length;
  if (authored > MAX_NODES) say('nodes-bound', null, null, `the graph has ${authored} nodes, and holds at most ${MAX_NODES}, synthetic ones apart.`);
  if (graph.wires.length - syntheticWires > MAX_WIRES) {
    say('wires-bound', null, null, `the graph has ${graph.wires.length - syntheticWires} wires, and holds at most ${MAX_WIRES}, a synthetic node's apart.`);
  }
  if (syntheticNodes > SYNTHETIC_PER_NODE * authored || syntheticWires > SYNTHETIC_WIRES_PER_NODE * authored) {
    say('synthetic-bound', null, null, `the graph has ${syntheticNodes} synthetic nodes and ${syntheticWires} wires to or from them, and ${authored} authored `
      + `nodes allow at most ${SYNTHETIC_PER_NODE * authored} and ${SYNTHETIC_WIRES_PER_NODE * authored}.`);
  }

  for (const node of graph.nodes) {
    const field = declarationProblem(node);
    if (field !== null) say('declaration', null, node.id, `node \`${node.id}\`: its \`${field}\` does not hold for a \`${node.kind}\`.`);
  }

  problems.push(...wireProblems(graph.nodes, graph.wires));

  const byId = new Map<string, FormNode>();
  for (const node of graph.nodes) if (!byId.has(node.id)) byId.set(node.id, node);
  const local = localWires(byId, graph.wires);

  const starts: string[] = [];
  if (graph.entries.length === 0) say('no-entry', null, null, 'the graph has no entry, so nothing in it would start.');
  for (const entry of graph.entries) {
    if (!byId.has(entry)) say('entry', null, entry, `entry \`${entry}\` names no node of this graph.`);
    else if (starts.includes(entry)) say('entry', null, entry, `entry \`${entry}\` is named twice.`);
    else {
      starts.push(entry);
      for (const wire of local.filter((each) => each.to.node === entry)) {
        say('entry-wired', wire.id, entry, `wire \`${wire.id}\`: \`${entry}\` is an entry, which starts the graph and waits on nothing in it.`);
      }
    }
  }

  const end = !blank(graph.end) ? byId.get(graph.end) ?? null : null;
  if (end === null) {
    say('end', null, blank(graph.end) ? null : graph.end, blank(graph.end) ? 'the graph names no end.' : `the end \`${graph.end}\` names no node of this graph.`);
  } else if (authorityOf(end)?.mayEnd === false) {
    say('end-kind', null, end.id, end.kind === 'stop'
      ? `\`${end.id}\` is a \`${end.kind}\`, which is never an end: a failure stops the run there, short of its end.`
      : `\`${end.id}\` is a \`${end.kind}\`, which is never an end: it is reached on a claim, and an end on evidence.`);
  }

  if (starts.length > 0) {
    const reached = reach(starts, local, true);
    for (const node of [...byId.values()].filter((each) => !reached.has(each.id))) {
      say('unreachable', null, node.id, `node \`${node.id}\`: no entry reaches it, so it would never start.`);
    }
  }

  // D157 §2.2's protected order: each gate reached before every landing starts, through wires a reach satisfies. FABRIC-CUTOVER1b (the
  // cutover design §5.1): the opinion is not asked to reach a look, whose set-up the review gate holds while an agent reads the opinion.
  const ordered = local.filter((wire) => wire.kind === 'after' || wire.kind === 'trigger' || wire.kind === 'data');
  const landings = [...byId.values()].filter((node) => node.kind === 'handed');
  const looks = [...byId.values()].filter((node) => node.kind === 'look');
  const gates = [...byId.values()].filter((node) => node.kind === 'opinion' || node.kind === 'look');
  const later = landings.map((landing): [string, FormNode] => ['landing', landing]);
  const unreached = new Set<string>();
  for (const gate of gates) {
    const after = reach([gate.id], ordered, true);
    if (gate.kind === 'look') for (const landing of landings) if (!after.has(landing.id)) unreached.add(landing.id);
    const early = later.find(([, each]) => !after.has(each.id));
    if (early !== undefined) say('protected-order', null, gate.id, `the ${gate.kind} \`${gate.id}\` comes before the ${early[0]} \`${early[1].id}\` ${ORDER}.`);
  }

  // FABRIC-GRAPH1's review, finding 4: no landing may start before a look has passed, whichever member of an `anyOf` a run takes.
  if (looks.length > 0) {
    const unlooked = startsWithNoLook(byId, starts, local);
    for (const landing of landings.filter((each) => !unreached.has(each.id) && unlooked.has(each.id))) {
      say('protected-order', null, looks[0]!.id, `the landing \`${landing.id}\` can start with no look passed, though \`${looks[0]!.id}\` reaches it: every way to it `
        + `passes a look ${ORDER}.`);
    }
  }

  // FABRIC-GRAPH1i: a skip satisfies `after`, so each gate is wired straight into each later one, in no `anyOf` group; said only where
  // the order is not already refused, so one fault is not two.
  if (!problems.some((problem) => problem.code === 'protected-order')) {
    const wired = (from: string, to: string): boolean => ordered.some((wire) => wire.from!.node === from && wire.to.node === to && wire.group?.mode !== 'anyOf');
    for (const gate of gates) {
      const next = later.find(([, each]) => !wired(gate.id, each.id));
      if (next !== undefined) {
        say('protected-wire', null, gate.id, `the ${gate.kind} \`${gate.id}\` is not wired into the ${next[0]} \`${next[1].id}\`: a skip of a gate between them would let an \`after\` `
          + `wire pass while \`${gate.id}\` is unsettled, so wire \`${gate.id}\` into \`${next[1].id}\` ${ORDER}.`);
      }
    }
  }

  // FABRIC-GRAPH1g: an event names the one observed node of its plugin's step, so two of one step in one graph are reached by neither.
  const firstObserved = new Map<string, string>();
  for (const node of graph.nodes.filter((each) => saved && each.reach === 'observed' && isPluginKind(each.kind))) {
    const first = firstObserved.get(node.kind);
    if (first === undefined) firstObserved.set(node.kind, node.id);
    else {
      say('observed-twice', null, node.id, `node \`${node.id}\` and \`${first}\` are both observed nodes of \`${node.kind}\`: an event of that step names one node, `
        + 'so neither would be reached.');
    }
  }

  // §E5: in a graph that holds `quest.close`, it is the end, only its failure's path leaves it, and nothing before it waits on the quest's
  // own done, which a closure graph never writes before its end (canary 40).
  const deadlocked = new Set<string>();
  for (const close of [...byId.values()].filter((node) => node.kind === 'quest.close')) {
    if (end !== null && close.id !== end.id) {
      say('close-end', null, close.id, `\`${close.id}\` closes the quest, so it is the graph's end, never \`${end.id}\`: the quest would read done while its work still moves.`);
    }
    for (const wire of local.filter((each) => each.from!.node === close.id && each.kind !== 'failed')) {
      say('after-close', wire.id, close.id, `wire \`${wire.id}\`: nothing comes after \`${close.id}\`, which closes the quest; only its failure's path leaves it.`);
    }
    for (const id of reach([close.id], local, false)) {
      const claim = byId.get(id)!;
      if (claim.kind !== 'claimed' || deadlocked.has(claim.id)) continue;
      deadlocked.add(claim.id);
      say('closure-deadlock', null, claim.id, `\`${claim.id}\` waits on the quest's own done before \`${close.id}\` closes it, and a closure graph `
        + 'writes no done before its end: the agent submits as `ready` instead.');
    }
  }

  const { cleanup } = graph;
  if (cleanup.until !== 'held' && cleanup.until !== 'end') {
    say('cleanup', null, null, `the cleanup waits \`${cleanup.until}\`, and waits for \`held\` or \`end\`.`);
  } else if (cleanup.days !== null && (cleanup.until === 'held' || cleanup.days < 1 || cleanup.days > 90)) {
    say('cleanup', null, null, cleanup.until === 'held'
      ? 'a bound in days is for a cleanup waiting on the end; at `held` the line\'s proof is the wait.'
      : `the cleanup's bound is ${cleanup.days} days, and is 1 to 90.`);
  } else if (cleanup.until === 'end' && end !== null && AT_OR_BEFORE_THE_LINE.includes(end.kind)) {
    say('cleanup', null, end.id, `the end \`${end.id}\` is at or before the line, so its cleanup goes at \`held\`: the wait is for an end after the line.`);
  }

  return problems;
}

/**
 * A whole graph from its JSON (a work cycle, a named graph) — the driver's `NodeGraphs.Read`: the graph and what its save refuses, or no
 * graph and the one `unreadable` problem naming where it is not a graph's shape. Never read in part. `lossy` is set where an option holds a
 * value this build cannot write as the driver does: the graph is judged, and no digest is taken of it; `rewrites` where a write of the
 * file holding it would change what the driver reads (`readGraphParts`).
 */
export function readGraph(value: unknown): { graph: GraphForm | null; problems: GraphProblem[]; lossy: string | null; rewrites: boolean } {
  const { parts, problem, lossy, rewrites } = readGraphParts(value);
  if (parts === null) return { graph: null, problems: [{ code: 'unreadable', wire: null, node: null, words: problem! }], lossy: null, rewrites: false };
  const graph: GraphForm = { ...parts, end: parts.end ?? '', cleanup: parts.cleanup ?? { until: 'held', days: null } };
  const problems = graphProblems(graph);
  return { graph: problems.length === 0 ? graph : null, problems, lossy, rewrites };
}

/** A landing rule as its cycle is judged after it: its form, and the plugin a branch names. */
export interface CycleRule { form: string; plugin?: string | null }

const LANDED = ['handed', 'line'];
const PULLED = ['handed', 'pushed', 'pull-request', 'line'];

const landingNode = (kind: string): FormNode => ({
  id: kind, kind, version: 1, reach: BUILT_IN_AUTHORITIES[kind]!.reach, evidence: null, retry: null, timeoutSeconds: null, rejectable: false,
  skippable: true, inputs: {}, outputs: {}, options: {},
});

/**
 * The landing's own graph, as its rule reaches — the driver's `NodeGraphHomes.Landing`: `handed`, then `pushed` and `pull-request` where a
 * branch rule names the plugin that pushes, then `line`, each after the one before; entered at `handed`, ended at `line`, cleaned at held.
 */
export function landingGraph(rule: CycleRule): GraphForm {
  const kinds = rule.form === 'branch' && rule.plugin !== undefined && rule.plugin !== null ? PULLED : LANDED;
  return {
    schemaVersion: SCHEMA_VERSION, nodes: kinds.map(landingNode), entries: ['handed'], end: 'line', cleanup: { until: 'held', days: null },
    wires: kinds.slice(1).map((kind, index) => ({ id: `landing/${kinds[index]}-${kind}`, kind: 'after', from: { node: kinds[index]!, port: null },
      to: { node: kind, port: null }, waits: null, group: null })),
  };
}

/**
 * What a landing rule's cycle refuses after its landing; empty where it holds — the driver's `NodeGraphHomes.OfLanding`. A node of a
 * landing's kind and a wire into the landing are the cycle's own refusals (the latter left out of the graph judged, so it is not said
 * again as an entry's wire); the rest is the whole graph's, the landing's nodes and the cycle's, its entries beside `handed`, its end
 * and cleanup the cycle's or the landing's.
 */
export function cycleProblems(rule: CycleRule, cycle: unknown): GraphProblem[] {
  const landing = landingGraph(rule);
  const { parts, problem } = readGraphParts(cycle);
  if (parts === null) return [{ code: 'unreadable', wire: null, node: null, words: problem! }];

  const problems: GraphProblem[] = [];
  const held = new Set(landing.nodes.map((node) => node.id));
  for (const node of parts.nodes.filter((each) => PULLED.includes(each.kind))) {
    problems.push({ code: 'landing-kind', wire: null, node: node.id,
      words: `node \`${node.id}\` is a \`${node.kind}\`, a landing's node, which the landing rule holds already: wire from it.` });
  }

  const intoLanding = (wire: FormWire): boolean => !wire.to.unit && held.has(wire.to.node);
  for (const wire of parts.wires.filter(intoLanding)) {
    problems.push({ code: 'into-landing', wire: wire.id, node: wire.to.node,
      words: `wire \`${wire.id}\`: the cycle comes after the landing, and \`${wire.to.node}\` is the landing's; what comes before it is the rules' gates.` });
  }

  problems.push(...graphProblems({
    schemaVersion: parts.schemaVersion,
    nodes: [...landing.nodes, ...parts.nodes],
    wires: [...landing.wires, ...parts.wires.filter((wire) => !intoLanding(wire))],
    entries: [...landing.entries, ...parts.entries],
    end: parts.end ?? landing.end,
    cleanup: parts.cleanup ?? landing.cleanup,
  }));
  return problems;
}
