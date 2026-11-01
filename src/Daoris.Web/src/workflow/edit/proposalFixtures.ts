import type { HelpProposal, WorkflowGraphChange } from '../../help/ProposalCard';
import { MERGE_GRAPH, RELEASE_GRAPH } from '../graphFixtures';
import { canon, type DraftProposal } from './proposalTake';

// Ask Daoris's proposals against the open draft, for the take's proofs, the editor's page tests and its stories (FLOWCHART2f): a mirror of
// the driver's `WorkflowGraphDiff` (FLOWCHART2e) saying a proposed graph against its base as the driver says it, change by change with what
// each needs, held to the driver's own answer by `proposalTake.test.ts`; and the release workflow proposed against the Merge workflow.

type Json = Record<string, unknown>;
type End = { node: string; port: string | null } | null;
type Wire = { id: string; kind: string; from: End; to: End; outcome: string | null; on: string | null; waits: string; group: string; json: Json };

const record = (value: unknown): Json => (value && typeof value === 'object' && !Array.isArray(value) ? (value as Json) : {});
const list = (value: unknown): Json[] => (Array.isArray(value) ? value.map(record) : []);
const text = (value: unknown): string | null => (typeof value === 'string' ? value : null);

/** A wire's end as the file names it: a step, and on a data wire its port after the last dot. */
function endOf(named: unknown, kind: string): End {
  const value = text(named);
  if (value === null) return null;
  const dot = value.lastIndexOf('.');
  return kind === 'data' && dot > 0 && dot < value.length - 1 ? { node: value.slice(0, dot), port: value.slice(dot + 1) } : { node: value, port: null };
}

const wiresOf = (graph: Json): Wire[] => list(graph.wires).map((json) => {
  const kind = text(json.kind) ?? '';
  return {
    id: text(json.id) ?? '', kind, from: endOf(json.from, kind), to: endOf(json.to, kind), outcome: text(json.outcome), on: text(json.on),
    waits: canon(json.waits), group: canon(json.group), json,
  };
});

const sameEnd = (one: End, other: End) => one?.node === other?.node && one?.port === other?.port;

const sameStructure = (one: Wire, other: Wire) => one.kind === other.kind && sameEnd(one.from, other.from) && sameEnd(one.to, other.to)
  && one.outcome === other.outcome && one.on === other.on && one.waits === other.waits;

function wayFields(old: Wire, wire: Wire): string[] {
  return [
    ['id', old.id !== wire.id], ['kind', old.kind !== wire.kind], ['from', !sameEnd(old.from, wire.from)], ['to', !sameEnd(old.to, wire.to)],
    ['waits', old.waits !== wire.waits], ['outcome', old.outcome !== wire.outcome], ['on', old.on !== wire.on], ['group', old.group !== wire.group],
  ].filter(([, differs]) => differs).map(([field]) => field as string);
}

/** A step's fields that differ, as `NodeDeclaration` reads them: an absent version is 1, an absent `skippable` true, `rejectable` false. */
function stepFields(old: Json, node: Json): string[] {
  const field = (json: Json, name: string) => {
    if (name === 'version') return json.version ?? 1;
    if (name === 'skippable') return json.skippable ?? true;
    if (name === 'rejectable') return json.rejectable ?? false;
    return json[name] ?? null;
  };
  const differs = (name: string) => canon(field(old, name)) !== canon(field(node, name));
  const options = [...new Set([...Object.keys(record(old.options)), ...Object.keys(record(node.options))])].sort();
  return [
    ...['kind', 'version', 'reach', 'inputs', 'outputs', 'evidence', 'retry', 'timeoutSeconds', 'rejectable', 'skippable', 'perform'].filter(differs),
    ...options.filter((name) => canon(record(old.options)[name] ?? null) !== canon(record(node.options)[name] ?? null)).map((name) => `options.${name}`),
    ...['outcomes', 'outputsOn', 'title', 'about', 'synthetic'].filter(differs),
  ];
}

const mark = (change: string) => (change === 'added' ? '+' : change === 'removed' ? '-' : '~');
const qualified = (wire: Wire) => (wire.kind === 'when' && wire.outcome ? `when \`${wire.outcome}\`` : wire.kind === 'back' && wire.on ? `back on \`${wire.on}\`` : wire.kind);
const endSaid = (end: End) => (end === null ? 'a wait' : `\`${end.node}${end.port ? `.${end.port}` : ''}\``);
const ends = (wire: Wire) => `${endSaid(wire.from)} → ${endSaid(wire.to)}`;

/** The driver's `WorkflowGraphDiff.Of`, mirrored: steps by id, ways by structure then id, then the entries, the end and the cleanup. */
export function diffOf(before: Json, after: Json): WorkflowGraphChange[] {
  const was = new Map(list(before.nodes).map((node) => [text(node.id) ?? '', node]));
  const now = new Map(list(after.nodes).map((node) => [text(node.id) ?? '', node]));
  const oldWires = wiresOf(before);
  const newWires = wiresOf(after);
  const stepId = (id: string, change: string) => `step:${mark(change)}${id}`;
  const wayId = (id: string, change: string) => `way:${mark(change)}${id}`;

  const paired = new Map<Wire, Wire>();
  const taken = new Set<Wire>();
  for (const wire of newWires) {
    const same = oldWires.filter((old) => !taken.has(old) && sameStructure(old, wire));
    const match = same.find((old) => old.id === wire.id) ?? same[0];
    if (match) { paired.set(wire, match); taken.add(match); }
  }
  for (const wire of newWires.filter((each) => !paired.has(each))) {
    const match = oldWires.find((old) => !taken.has(old) && old.id === wire.id);
    if (match) { paired.set(wire, match); taken.add(match); }
  }
  const removedWays = oldWires.filter((old) => !taken.has(old));
  const changedWays = newWires.filter((wire) => paired.has(wire) && wayFields(paired.get(wire)!, wire).length > 0);
  const baseChange = new Map<string, string>();
  for (const old of removedWays) baseChange.set(old.id, wayId(old.id, 'removed'));
  for (const wire of changedWays) baseChange.set(paired.get(wire)!.id, wayId(wire.id, 'changed'));
  const endsNeed = (wire: Wire) => [...new Set([wire.from?.node, wire.to?.node].filter((node): node is string => typeof node === 'string'))]
    .filter((node) => !was.has(node) && now.has(node)).map((node) => stepId(node, 'added'));
  const idNeeds = (wire: Wire, self: string) => {
    const other = baseChange.get(wire.id);
    return other !== undefined && other !== self && (!paired.has(wire) || paired.get(wire)!.id !== wire.id) ? [other] : [];
  };

  const change = (id: string, what: WorkflowGraphChange['what'], kind: WorkflowGraphChange['change'], said: string, more: Partial<WorkflowGraphChange>) =>
    ({ id, what, change: kind, step: null, way: null, was: null, kind: null, fields: [], needs: [], said, ...more }) as WorkflowGraphChange;
  const changes: WorkflowGraphChange[] = [];
  for (const [id, node] of was) {
    if (now.has(id)) continue;
    const needs = [...new Set(oldWires.filter((old) => (old.from?.node === id || old.to?.node === id) && baseChange.has(old.id)).map((old) => baseChange.get(old.id)!))];
    changes.push(change(stepId(id, 'removed'), 'step', 'removed', `Remove the step \`${id}\` (\`${text(node.kind)}\`).`, { step: id, kind: text(node.kind), needs }));
  }
  for (const [id, node] of now) {
    const old = was.get(id);
    if (!old) {
      changes.push(change(stepId(id, 'added'), 'step', 'added', `Add the step \`${id}\` (\`${text(node.kind)}\`).`, { step: id, kind: text(node.kind) }));
      continue;
    }
    const fields = stepFields(old, node);
    if (fields.length === 0) continue;
    changes.push(change(stepId(id, 'changed'), 'step', 'changed', `Change the step \`${id}\` (\`${text(node.kind)}\`): its ${fields.map((each) => `\`${each}\``).join(', ')}.`,
      { step: id, kind: text(node.kind), fields }));
  }
  for (const old of removedWays) {
    changes.push(change(wayId(old.id, 'removed'), 'way', 'removed', `Remove the way \`${old.id}\` (${qualified(old)}): ${ends(old)}.`, { way: old.id, kind: old.kind }));
  }
  for (const wire of newWires) {
    const old = paired.get(wire);
    if (!old) {
      const id = wayId(wire.id, 'added');
      changes.push(change(id, 'way', 'added', `Add the way \`${wire.id}\` (${qualified(wire)}): ${ends(wire)}.`,
        { way: wire.id, kind: wire.kind, needs: [...endsNeed(wire), ...idNeeds(wire, id)] }));
      continue;
    }
    const fields = wayFields(old, wire);
    if (fields.length === 0) continue;
    const id = wayId(wire.id, 'changed');
    changes.push(change(id, 'way', 'changed', `Change the way \`${wire.id}\` (${qualified(wire)}): ${ends(wire)}.`,
      { way: wire.id, was: old.id === wire.id ? null : old.id, kind: wire.kind, fields, needs: [...endsNeed(wire), ...idNeeds(wire, id)] }));
  }
  const entries = (graph: Json) => (Array.isArray(graph.entries) ? graph.entries.filter((each): each is string => typeof each === 'string') : []);
  if (canon(entries(before)) !== canon(entries(after))) {
    changes.push(change('graph:~entries', 'graph', 'changed', `The work starts at ${entries(after).join(', ')} now.`, {
      fields: ['entries'], needs: entries(after).filter((entry) => !was.has(entry) && now.has(entry)).map((entry) => stepId(entry, 'added')),
    }));
  }
  const endOfGraph = (graph: Json) => text(graph.end) ?? '';
  if (endOfGraph(before) !== endOfGraph(after)) {
    const end = endOfGraph(after);
    changes.push(change('graph:~end', 'graph', 'changed', `The work ends at \`${end}\` now, not \`${endOfGraph(before)}\`.`, {
      fields: ['end'], needs: !was.has(end) && now.has(end) ? [stepId(end, 'added')] : [],
    }));
  }
  const cleanup = canon(before.cleanup) !== canon(after.cleanup);
  const schema = (before.schemaVersion ?? 2) !== (after.schemaVersion ?? 2);
  if (cleanup || schema) {
    changes.push(change('graph:~cleanup', 'graph', 'changed', 'Its cleanup changed.', {
      fields: [...(cleanup ? ['cleanup'] : []), ...(schema ? ['schemaVersion'] : [])],
    }));
  }
  return changes;
}

/** A proposal against `base`, its changes the driver's. */
export const proposalOf = (base: Json, proposed: Json, id = 'p7'): DraftProposal => ({ id, base, proposed, changes: diffOf(base, proposed) });

/** "Add a release after the line": the release workflow proposed against the Merge workflow, open in the editor. */
export const RELEASE_PROPOSAL = proposalOf(MERGE_GRAPH, RELEASE_GRAPH);

/** The card Ask Daoris draws for it, as `HELP_PROPOSALS` answers it. */
export const RELEASE_CARD: HelpProposal = {
  id: 'p7', revision: 'r7', kind: 'workflow', terminal: '', why: 'You asked for a release after the line: find, record and copy it.',
  describe: 'A change to your workflow draft of a new workflow for `engine`: 16 changes.',
  workflow: {
    id: '', name: '', base: null, version: 0, scope: 'engine', diff: RELEASE_PROPOSAL.changes.map((each) => each.said), yourPart: [], outward: [],
    needs: [], appliesTo: 'Nothing is saved or chosen: you take the changes into the draft in its editor, and only your Save there saves it.',
    choose: false, saves: false, draft: 'g1-0123456789abcdef', changes: [...RELEASE_PROPOSAL.changes], problems: [], proposed: RELEASE_GRAPH,
  },
};
