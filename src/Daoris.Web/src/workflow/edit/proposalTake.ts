import type { WorkflowGraphChange } from '../../help/ProposalCard';
import { draftOf, type DraftNode, type DraftWire, type GraphDraft, graphOf } from './graphDraft';

// Ask Daoris's proposal taken into the open draft (FLOWCHART2f; D223 §4, §8.6). The driver says what a proposed graph changes against the
// draft it was told (`WorkflowGraphDiff`); the page takes a change and every change it needs, as one transaction. Each change's precondition
// is what the proposal's base held of the step, the way or the field it changes, fixed when the proposal is drawn: a take that finds the
// draft no longer holding one, or that would leave a way to a step the draft does not hold, is refused by that change's name, and the
// changes it does not touch stay takeable. Pure: the draft is never written but by the take's own result.

type Json = Record<string, unknown>;

/** A proposal as the editor reviews it: the draft it was made against (null where this window no longer holds it), its graph, its changes. */
export type DraftProposal = {
  id: string;
  base: Json | null;
  proposed: Json;
  changes: readonly WorkflowGraphChange[];
};

/** Who made a proposal: the agent, and the models its conversation reported running. */
export type ProposalMaker = { agent: string; models: readonly string[] };

/** A proposal as the editor shows it: its agent's reason, how many problems a save of it whole would meet, and who made it, where known. */
export type ProposalShown = DraftProposal & { why: string; problems: number; maker: ProposalMaker | null };

/** Which of a proposal's changes the person took into the draft, and which they left. */
export type Decided = { taken: readonly string[]; left: readonly string[] };

export const UNDECIDED: Decided = { taken: [], left: [] };

/** A value with its objects' keys in order: two that run alike say the same. */
const sorted = (value: unknown): unknown => {
  if (Array.isArray(value)) return value.map(sorted);
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.keys(value).sort().map((key) => [key, sorted((value as Json)[key])]));
  }
  return value;
};

/** A value written whole, key order aside. */
export const canon = (value: unknown): string => JSON.stringify(sorted(value) ?? null);

const record = (value: unknown): Json => (value && typeof value === 'object' && !Array.isArray(value) ? (value as Json) : {});
const list = (value: unknown): Json[] => (Array.isArray(value) ? value.map(record) : []);
const names = (value: unknown): string[] => (Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string') : []);

/** A graph's steps and ways by id, its entries, its end and its cleanup. */
type Held = { nodes: Map<string, Json>; wires: Map<string, Json>; entries: string[]; end: string; cleanup: unknown };

function heldOf(graph: Json): Held {
  return {
    nodes: new Map(list(graph.nodes).map((node) => [String(node.id ?? ''), node])),
    wires: new Map(list(graph.wires).map((wire) => [String(wire.id ?? ''), wire])),
    entries: names(graph.entries),
    end: typeof graph.end === 'string' ? graph.end : '',
    cleanup: graph.cleanup,
  };
}

/** The kinds a `land`'s method brings after it (`NodeGraphs`' land-tail): a push's, a pull request's, an integration's. */
const TAIL_KINDS: readonly string[] = ['push','pull-request', 'integrate'];

/** The groups a way's change touches: the base's where it was, the proposal's where it is. */
function groupsOf(change: WorkflowGraphChange, base: Held, proposed: Held): string[] {
  if (change.what !== 'way') return [];
  const group = (wire: Json | undefined) => {
    const id = record(wire?.group).id;
    return typeof id === 'string' ? [id] : [];
  };
  return [
    ...(change.change === 'added' ? [] : group(base.wires.get(change.was ?? change.way ?? ''))),
    ...(change.change === 'removed' ? [] : group(proposed.wires.get(change.way ?? ''))),
  ];
}

/**
 * What each change needs taken with it: the driver's `needs`, and what the graph's meaning ties to it besides (§8.6): a step removed that
 * the base ends at goes with the end's change; a `land`'s method, added or changed, goes with the steps of its tail added or removed and the
 * ways that reach them; and a way of a group goes with the group's other ways.
 */
export function needsOf(proposal: DraftProposal): Map<string, string[]> {
  const { changes } = proposal;
  const known = new Set(changes.map((change) => change.id));
  const ties = new Map(changes.map((change) => [change.id, new Set(change.needs.filter((id) => known.has(id)))]));
  const tieAll = (group: readonly WorkflowGraphChange[]) => {
    for (const one of group) for (const other of group) if (one !== other) ties.get(one.id)!.add(other.id);
  };
  const base = heldOf(proposal.base ?? {});
  const proposed = heldOf(proposal.proposed);

  const end = changes.find((change) => change.id === 'graph:~end');
  if (end) {
    for (const change of changes) if (change.what === 'step' && change.change === 'removed' && change.step === base.end) ties.get(change.id)!.add(end.id);
  }

  const landings = changes.filter((change) => change.what === 'step' && change.kind === 'land'
    && (change.change !== 'changed' || change.fields.includes('options.method')));
  const tails = changes.filter((change) => change.what === 'step' && change.change !== 'changed' && TAIL_KINDS.includes(change.kind ?? ''));
  const reaching = changes.filter((change) => change.what === 'way' && change.change !== 'removed'
    && change.needs.some((id) => tails.some((tail) => tail.change === 'added' && tail.id === id)));
  if (landings.length > 0 && tails.length > 0) tieAll([...landings, ...tails, ...reaching]);

  const byGroup = new Map<string, WorkflowGraphChange[]>();
  for (const change of changes) for (const group of groupsOf(change, base, proposed)) byGroup.set(group, [...(byGroup.get(group) ?? []), change]);
  for (const group of byGroup.values()) tieAll(group);

  return new Map([...ties].map(([id, needs]) => [id, [...needs]]));
}

/** A change and every change it needs, transitively, in the driver's order. */
export function closureOf(proposal: DraftProposal, id: string, needs = needsOf(proposal)): string[] {
  const found = new Set<string>();
  const queue = [id];
  while (queue.length > 0) {
    const next = queue.shift()!;
    if (found.has(next) || !needs.has(next)) continue;
    found.add(next);
    queue.push(...needs.get(next)!);
  }
  return proposal.changes.map((change) => change.id).filter((each) => found.has(each));
}

/** A change and every change that needs it, which cannot be taken once it is left, in the driver's order. */
export function leftWith(proposal: DraftProposal, id: string, needs = needsOf(proposal)): string[] {
  return proposal.changes.map((change) => change.id).filter((each) => closureOf(proposal, each, needs).includes(id));
}

/** A record with changes taken (no longer left) and changes left (none taken). */
export function decide(decided: Decided, { taken = [], left = [] }: { taken?: readonly string[]; left?: readonly string[] }): Decided {
  const nowTaken = [...decided.taken, ...taken.filter((id) => !decided.taken.includes(id))];
  return {
    taken: nowTaken,
    left: [...decided.left, ...left.filter((id) => !decided.left.includes(id))].filter((id) => !nowTaken.includes(id)),
  };
}

/** Whether every change of the proposal is named once, taken or left: the press that settles it may go. */
export function everyNamed(proposal: DraftProposal, decided: Decided): boolean {
  const named = [...decided.taken, ...decided.left];
  const ids = new Set(proposal.changes.map((change) => change.id));
  return named.length === ids.size && new Set(named).size === named.length && named.every((id) => ids.has(id));
}

/** Whether the draft still holds what the base held where the change applies. */
function holds(change: WorkflowGraphChange, draft: Held, base: Held): boolean {
  if (change.what === 'step') {
    const id = change.step ?? '';
    if (change.change === 'added') return !draft.nodes.has(id);
    return draft.nodes.has(id) && canon(draft.nodes.get(id)) === canon(base.nodes.get(id));
  }
  if (change.what === 'way') {
    if (change.change === 'added') return true;
    const id = change.was ?? change.way ?? '';
    return draft.wires.has(id) && canon(draft.wires.get(id)) === canon(base.wires.get(id));
  }
  if (change.fields.includes('end')) return draft.end === base.end;
  if (change.fields.includes('cleanup')) return canon(draft.cleanup) === canon(base.cleanup);
  // The entries are an order over the steps nothing leads to, which the draft reads from its ways: no state of the draft refuses one.
  return true;
}

const nodeFrom = (json: Json): DraftNode => draftOf({ nodes: [json] }).nodes[0]!;
const wireFrom = (json: Json): DraftWire => draftOf({ wires: [json] }).wires[0]!;

/** The changes written into the draft: removals, replacements in place, additions at the end, and the graph's own fields. */
function applied(draft: GraphDraft, proposed: Held, set: readonly WorkflowGraphChange[]): GraphDraft {
  const of = (what: string, change: string) => set.filter((each) => each.what === what && each.change === change);
  const goneSteps = new Set(of('step', 'removed').map((each) => each.step ?? ''));
  const newSteps = new Map(of('step', 'changed').map((each) => [each.step ?? '', proposed.nodes.get(each.step ?? '')!]));
  const goneWays = new Set(of('way', 'removed').map((each) => each.way ?? ''));
  const newWays = new Map(of('way', 'changed').map((each) => [each.was ?? each.way ?? '', proposed.wires.get(each.way ?? '')!]));
  const graph = set.filter((each) => each.what === 'graph');
  let rest = draft.rest;
  if (graph.some((each) => each.fields.includes('cleanup'))) {
    const { cleanup: _was, ...others } = rest;
    rest = proposed.cleanup === undefined ? others : { ...others, cleanup: proposed.cleanup };
  }
  return {
    nodes: [
      ...draft.nodes.filter((node) => !goneSteps.has(node.id)).map((node) => (newSteps.has(node.id) ? nodeFrom(newSteps.get(node.id)!) : node)),
      ...of('step', 'added').map((each) => nodeFrom(proposed.nodes.get(each.step ?? '')!)),
    ],
    wires: [
      ...draft.wires.filter((wire) => !goneWays.has(wire.id)).map((wire) => (newWays.has(wire.id) ? wireFrom(newWays.get(wire.id)!) : wire)),
      ...of('way', 'added').map((each) => wireFrom(proposed.wires.get(each.way ?? '')!)),
    ],
    end: graph.some((each) => each.fields.includes('end')) ? proposed.end : draft.end,
    rest,
    order: graph.some((each) => each.fields.includes('entries')) ? proposed.entries : draft.order,
  };
}

/** Whether a draft is whole: each id once, each way between steps it holds, its end one of them. */
function whole(draft: GraphDraft): boolean {
  const steps = new Set(draft.nodes.map((node) => node.id));
  return steps.size === draft.nodes.length && new Set(draft.wires.map((wire) => wire.id)).size === draft.wires.length
    && draft.wires.every((wire) => steps.has(wire.to) && (wire.from === null || steps.has(wire.from)))
    && (draft.end === '' || steps.has(draft.end));
}

/** A take: the draft with the changes in, and every change it took; or the change it was refused at. */
export type Take = { ok: true; draft: GraphDraft; taken: string[] } | { ok: false; at: string };

/**
 * The changes `wanted` taken into the draft with every change they need (§8.6), atomically: the changes already taken are not taken again,
 * and those left are taken back. Refused, and nothing written, at the first change whose base the draft no longer holds, or at the change
 * asked where the result would not be whole (a way to a step it takes away, an id the person's own step or way holds).
 */
export function take(draft: GraphDraft, proposal: DraftProposal, wanted: readonly string[], decided: Decided): Take {
  if (wanted.length === 0) return { ok: true, draft, taken: [] };
  if (proposal.base === null) return { ok: false, at: wanted[0]! };
  const needs = needsOf(proposal);
  const within = new Set(wanted.flatMap((id) => closureOf(proposal, id, needs)));
  const set = proposal.changes.filter((change) => within.has(change.id) && !decided.taken.includes(change.id));
  const base = heldOf(proposal.base);
  const proposed = heldOf(proposal.proposed);
  const now = heldOf(graphOf(draft));
  const stale = set.find((change) => !holds(change, now, base));
  if (stale) return { ok: false, at: stale.id };
  const next = applied(draft, proposed, set);
  if (!whole(next)) return { ok: false, at: wanted[0]! };
  return { ok: true, draft: next, taken: set.map((change) => change.id) };
}

/** Each change still to take whose take the draft refuses now, with the change it was refused at. */
export function staleOf(draft: GraphDraft, proposal: DraftProposal, decided: Decided): Map<string, string> {
  const stale = new Map<string, string>();
  for (const change of proposal.changes) {
    if (decided.taken.includes(change.id)) continue;
    const result = take(draft, proposal, [change.id], decided);
    if (!result.ok) stale.set(change.id, result.at);
  }
  return stale;
}

/** How a step or a way stands in the proposal's drawing. */
export type Mark = 'added' | 'removed' | 'changed';

/** The id a proposed way is drawn under beside the draft's own, which may hold the same id. */
export const PROPOSED = '+';

/** The drawing of the draft with the proposal's open changes on it, and how each step and way it marks stands. */
export type Union = { draft: GraphDraft; steps: ReadonlyMap<string, Mark>; ways: ReadonlyMap<string, Mark> };

/**
 * The union drawing (D223 §4.3): the draft as it is, the open changes drawn on it, a step added or a way added (a changed way drawn as the
 * one to remove and the one new) marked beside what it changes, and nothing the draft holds altered. A way added to a step not drawn is
 * left out of the drawing, its change listed still.
 */
export function unionOf(draft: GraphDraft, proposal: DraftProposal, open: readonly string[]): Union {
  const proposed = heldOf(proposal.proposed);
  const steps = new Map<string, Mark>();
  const ways = new Map<string, Mark>();
  const nodes = [...draft.nodes];
  const drawn = new Set(nodes.map((node) => node.id));
  const changes = proposal.changes.filter((change) => open.includes(change.id));
  for (const change of changes) {
    if (change.what !== 'step' || !change.step) continue;
    if (change.change === 'added') {
      const json = proposed.nodes.get(change.step);
      if (!json || drawn.has(change.step)) continue;
      nodes.push(nodeFrom(json));
      drawn.add(change.step);
      steps.set(change.step, 'added');
    } else if (draft.nodes.some((node) => node.id === change.step)) {
      steps.set(change.step, change.change);
    }
  }
  const wires = [...draft.wires];
  for (const change of changes) {
    if (change.what !== 'way' || !change.way) continue;
    const old = change.was ?? change.way;
    const held = draft.wires.find((wire) => wire.id === old);
    // A way that keeps its ends (its id, its group or what it is taken on changed) is the one drawn, marked changed, never drawn twice.
    if (change.change === 'changed' && held && !change.fields.some((field) => field === 'from' || field === 'to')) {
      ways.set(old, 'changed');
      continue;
    }
    if (change.change !== 'added' && held) ways.set(old, 'removed');
    if (change.change === 'removed') continue;
    const json = proposed.wires.get(change.way);
    if (!json) continue;
    const wire = { ...wireFrom(json), id: `${PROPOSED}${change.way}` };
    if (!drawn.has(wire.to) || (wire.from !== null && !drawn.has(wire.from))) continue;
    wires.push(wire);
    ways.set(wire.id, 'added');
  }
  return { draft: { ...draft, nodes, wires }, steps, ways };
}
