import type { OptionField, WorkflowPaletteAnswer } from './answers';

// The step editor's draft (WORKFLOWEDIT1b, FLOWCHART2b): a schema-2 graph as the person composes it, the inverse of the run view's drawing
// (`workflowFlow.ts`, which reads a graph's wires onto edges). Pure: each change returns a new draft, and `graphOf` writes the graph the
// driver reads, judges and saves. A step is added from the palette, its kind copied whole, so a kind this page has no words for is
// composed like any other. What a saved graph holds that the editor does not draw (a group, a retry, its cleanup) is kept as written.
//
// The person never names a wire's kind (D223 §3, §8.1): a way on is a row of the step's *What happens next* and a target, and the kind is
// what the row and where the target stands give, by the driver's wire rules (`Nodes/NodeWires.cs`): on to a later step, back to one its
// source comes after through `after`, `trigger`, `data` or `when`, and nothing that would close a cycle. Insert, move and remove-and-join
// rewire only a plain chain, and never carry a data wire, a group or an exception's path over by guess (§8.2, §8.8).

/** A step the palette offers, a built-in kind's or a plugin's declared step, in one shape. */
export type StepChoice = {
  kind: string; version: number; reach: string; plugin: string | null; outcomes: string[]; outputsOn: Record<string, string[]>;
  inputs: Record<string, string>; outputs: Record<string, string>; evidence: string | null; rejectable: boolean; skippable: boolean; leaves: string[];
  options: OptionField[]; roleTakes: string[]; mayEnd: boolean;
  /** How the picker offers it, as the palette says: `alone`, `tail:<method>` or `retired`. */
  offered: string;
};

/** A step of the draft: its id and kind, and its node's JSON as the graph will hold it. */
export type DraftNode = { id: string; kind: string; node: Record<string, unknown> };

/** A way one step goes on to another: a wire, its ends (a `data` wire's ports apart), a decision's outcome and a way back's `on`. */
export type DraftWire = {
  id: string; kind: string; from: string | null; to: string; fromPort: string | null; toPort: string | null; outcome: string | null;
  on: string | null; wire: Record<string, unknown>;
};

/** A draft: its steps and wires in the order composed, its end, and the graph's other fields and entries' order as read. */
export type GraphDraft = { nodes: DraftNode[]; wires: DraftWire[]; end: string; rest: Record<string, unknown>; order: string[] };

/** A wire as `addWire` takes it. */
export type WireAsk = { kind: string; from: string; to: string; fromPort?: string; toPort?: string; outcome?: string; on?: string };

/** The graph a draft writes: schema 2, what `WORKFLOW_SAVE` takes as `graph`. */
export type GraphJson = {
  schemaVersion: 2; entries: string[]; end: string; nodes: Record<string, unknown>[]; wires: Record<string, unknown>[]; [field: string]: unknown;
};

/** The schema a graph is saved in (`NodeGraphs.SchemaVersion`). */
const SCHEMA = 2;

/** A way back goes to a step its source comes after, so it makes no step a waiting one and leaves an entry one. */
const BACK = 'back';

/** A data wire names its ports after a dot (`find.release`). */
const DATA = 'data';

const record = (value: unknown): Record<string, unknown> =>
  value && typeof value === 'object' && !Array.isArray(value) ? (value as Record<string, unknown>) : {};
const text = (value: unknown): string | null => (typeof value === 'string' ? value : null);
const names = (value: unknown): string[] => (Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string') : []);

export const emptyDraft = (): GraphDraft => ({ nodes: [], wires: [], end: '', rest: {}, order: [] });

/** Every step the palette offers: the built-in kinds in the driver's order, then each installed plugin's declared steps. */
export function stepChoices(palette: WorkflowPaletteAnswer): StepChoice[] {
  return [
    ...palette.kinds.map((kind): StepChoice => ({
      kind: kind.kind, version: 1, reach: kind.reach, plugin: null, outcomes: [], outputsOn: {}, inputs: {}, outputs: {}, evidence: null, rejectable: false,
      skippable: true, leaves: kind.leaves ?? [], options: kind.options, roleTakes: kind.roleTakes ?? [], mayEnd: kind.mayEnd,
      offered: kind.offered ?? 'alone',
    })),
    ...palette.plugins.flatMap(({ plugin, steps }) => steps.map((step): StepChoice => ({
      kind: step.kind, version: step.version ?? 1, reach: step.reach, plugin, outcomes: step.outcomes ?? [], outputsOn: step.outputsOn ?? {},
      inputs: step.inputs, outputs: step.outputs,
      evidence: step.evidence ?? null, rejectable: step.rejectable, skippable: step.skippable, leaves: step.leaves ?? [], options: [], roleTakes: [],
      mayEnd: true, offered: 'alone',
    }))),
  ];
}

/** The palette's offer for a step's kind, or null where this build's palette holds none (a plugin no longer installed). */
export const choiceOf = (palette: WorkflowPaletteAnswer, kind: string): StepChoice | null =>
  stepChoices(palette).find((each) => each.kind === kind) ?? null;

/** A draft read from a saved graph's JSON, leniently: what is not a graph's shape reads as an empty part, for the driver to judge. */
export function draftOf(body: unknown): GraphDraft {
  const graph = record(body);
  const nodes = (Array.isArray(graph.nodes) ? graph.nodes : []).map(record).map((node): DraftNode => ({
    id: text(node.id) ?? '', kind: text(node.kind) ?? '', node: { ...node },
  }));
  const wires = (Array.isArray(graph.wires) ? graph.wires : []).map(record).map((wire): DraftWire => {
    const kind = text(wire.kind) ?? '';
    const [from, fromPort] = endOf(text(wire.from), kind);
    const [to, toPort] = endOf(text(wire.to), kind);
    return { id: text(wire.id) ?? '', kind, from, to: to ?? '', fromPort, toPort, outcome: text(wire.outcome), on: text(wire.on), wire: { ...wire } };
  });
  const { schemaVersion: _schema, entries, end, nodes: _nodes, wires: _wires, ...rest } = graph;
  return { nodes, wires, end: text(end) ?? '', rest, order: names(entries) };
}

/** A wire's end as a file names it: a node, and on a data wire its port after the last dot. */
function endOf(named: string | null, kind: string): [string | null, string | null] {
  if (named === null) return [null, null];
  const dot = named.lastIndexOf('.');
  return kind === DATA && dot > 0 && dot < named.length - 1 ? [named.slice(0, dot), named.slice(dot + 1)] : [named, null];
}

/**
 * Where the graph starts: each step no other step's wire goes into, a way back's apart, in the order a saved graph named them and then
 * the order composed (`NodeGraphs`: an entry takes no wire from another node of the graph).
 */
export function entriesOf(draft: GraphDraft): string[] {
  const waits = new Set(draft.wires.filter((wire) => wire.kind !== BACK && wire.from !== null).map((wire) => wire.to));
  const starts = draft.nodes.map((node) => node.id).filter((id) => !waits.has(id));
  return [...draft.order.filter((id) => starts.includes(id)), ...starts.filter((id) => !draft.order.includes(id))];
}

/** The schema-2 graph the draft says, as `WORKFLOW_SAVE` takes it. */
export function graphOf(draft: GraphDraft): GraphJson {
  return {
    schemaVersion: SCHEMA,
    ...draft.rest,
    entries: entriesOf(draft),
    end: draft.end,
    nodes: draft.nodes.map((node) => ({ ...node.node, id: node.id, kind: node.kind })),
    wires: draft.wires.map((wire) => {
      const { from: _from, to: _to, outcome: _outcome, on: _on, ...kept } = wire.wire;
      const from = wire.from === null ? wire.wire.from : wire.fromPort ? `${wire.from}.${wire.fromPort}` : wire.from;
      return {
        ...kept, id: wire.id, kind: wire.kind, from, ...(wire.outcome ? { outcome: wire.outcome } : {}), ...(wire.on ? { on: wire.on } : {}),
        to: wire.toPort ? `${wire.to}.${wire.toPort}` : wire.to,
      };
    }),
  };
}

/** A step's id from its kind: its name after a plugin's slash, in lower-case words joined by dashes, numbered past one already taken. */
function idFor(kind: string, taken: Set<string>): string {
  const base = kind.slice(kind.indexOf('/') + 1).toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'step';
  if (!taken.has(base)) return base;
  for (let n = 2; ; n += 1) if (!taken.has(`${base}-${n}`)) return `${base}-${n}`;
}

/** The next wire id: a letter, as the defaults' wires are named, then `w27` on. */
function wireId(draft: GraphDraft): string {
  const taken = new Set(draft.wires.map((wire) => wire.id));
  for (let n = 0; ; n += 1) {
    const id = n < 26 ? String.fromCharCode(97 + n) : `w${n + 1}`;
    if (!taken.has(id)) return id;
  }
}

const taken = (draft: GraphDraft) => new Set(draft.nodes.map((node) => node.id));

const nodeById = (draft: GraphDraft, id: string) => draft.nodes.find((node) => node.id === id);

/** A copy that shares nothing with what it was copied from. */
const clone = <T>(value: T): T => JSON.parse(JSON.stringify(value)) as T;

/**
 * A step's node as the palette declares its kind: a plugin's outcomes, the outcomes that carry an output, inputs, outputs and evidence,
 * copied so the palette is shared with no draft.
 */
function nodeOf(choice: StepChoice, id: string): Record<string, unknown> {
  return {
    id, kind: choice.kind, version: choice.version, reach: choice.reach,
    ...(choice.outcomes.length > 0 ? { outcomes: [...choice.outcomes] } : {}),
    ...(Object.keys(choice.outputsOn).length > 0 ? { outputsOn: Object.fromEntries(Object.entries(choice.outputsOn).map(([output, on]) => [output, [...on]])) } : {}),
    ...(Object.keys(choice.inputs).length > 0 ? { inputs: { ...choice.inputs } } : {}),
    ...(Object.keys(choice.outputs).length > 0 ? { outputs: { ...choice.outputs } } : {}),
    ...(choice.evidence ? { evidence: choice.evidence } : {}),
    ...(choice.rejectable ? { rejectable: true } : {}),
    ...(choice.skippable ? {} : { skippable: false }),
  };
}

/** A step put into the draft, and its end where none is set yet and the step may end a workflow. */
const place = (draft: GraphDraft, step: DraftNode, mayEnd: boolean): GraphDraft =>
  ({ ...draft, nodes: [...draft.nodes, step], end: draft.end || (mayEnd ? step.id : '') });

/** A step added from the palette, unconnected: a new start of the graph. */
export function addStep(draft: GraphDraft, choice: StepChoice): { draft: GraphDraft; id: string } {
  const id = idFor(choice.kind, taken(draft));
  return { draft: place(draft, { id, kind: choice.kind, node: nodeOf(choice, id) }, choice.mayEnd), id };
}

/** A way one step goes on to another under the next free wire id, and that id. */
function withWire(draft: GraphDraft, ask: WireAsk, fields: Record<string, unknown> = {}): { draft: GraphDraft; id: string } {
  const id = wireId(draft);
  const wire: DraftWire = {
    id, kind: ask.kind, from: ask.from, to: ask.to, fromPort: ask.fromPort ?? null, toPort: ask.toPort ?? null,
    outcome: ask.outcome ?? null, on: ask.on ?? null, wire: fields,
  };
  return { draft: { ...draft, wires: [...draft.wires, wire] }, id };
}

/** A way one step goes on to another, under the next free wire id. */
export const addWire = (draft: GraphDraft, ask: WireAsk): GraphDraft => withWire(draft, ask).draft;

export const removeWire = (draft: GraphDraft, id: string): GraphDraft => ({ ...draft, wires: draft.wires.filter((wire) => wire.id !== id) });

/** The step whose reach ends the work. */
export const setEnd = (draft: GraphDraft, id: string): GraphDraft => ({ ...draft, end: id });

/** A step's option set, or cleared where the value says nothing: none, empty, off or an empty list. */
export function setOption(draft: GraphDraft, id: string, name: string, value: string | boolean | string[] | undefined): GraphDraft {
  return {
    ...draft,
    nodes: draft.nodes.map((each) => {
      if (each.id !== id) return each;
      const { [name]: _was, ...others } = record(each.node.options);
      const empty = value === undefined || value === '' || value === false || (Array.isArray(value) && value.length === 0);
      const options = empty ? others : { ...others, [name]: value };
      const { options: _options, ...node } = each.node;
      return { ...each, node: Object.keys(options).length > 0 ? { ...node, options } : node };
    }),
  };
}

/** A step's option as the draft holds it. */
export const optionOf = (node: DraftNode, name: string): unknown => record(node.node.options)[name];

/** The outcomes a step declares: a decision's routes. */
export const outcomesOf = (node: DraftNode): string[] => names(node.node.outcomes);

/** What a data wire may take from a step: its outputs, and its evidence where it declares one. */
export const outputsOf = (node: DraftNode): string[] =>
  [...Object.keys(record(node.node.outputs)), ...(text(node.node.evidence) ? ['evidence'] : [])];

/** What a data wire may give a step: its inputs. */
export const inputsOf = (node: DraftNode): string[] => Object.keys(record(node.node.inputs));

/** The wire kinds the palette lets leave a step, or a plain `after` where this build's palette does not hold its kind. */
export const leavesOf = (palette: WorkflowPaletteAnswer, node: DraftNode): string[] => choiceOf(palette, node.kind)?.leaves ?? ['after'];

/* ---------------------------------------------------------------- what happens next: rows, and the kind a target gives */

/** A row of a step's *What happens next*: once done, if you ask for changes, if it fails, or one of its outcomes. */
export type WayRowKey = 'done' | 'changes' | 'fails' | `outcome:${string}`;

/** A row and the ways on in it, each a target of its own, removable alone. */
export type WayRow = { key: WayRowKey; outcome: string | null; ways: DraftWire[] };

const OUTCOME = 'outcome:';

/** The kinds a graph's order is read along (`WireKinds.Ordered`): where a way back may return to. */
const ORDERED: readonly string[] = ['after', 'trigger', DATA, 'when'];

/** The result a row's way back is taken on: the person's rejection, a spent failure or an outcome; *Once done* has none. */
export const onOf = (row: WayRowKey): string | null =>
  row === 'changes' ? 'rejected' : row === 'fails' ? 'failed' : row.startsWith(OUTCOME) ? row.slice(OUTCOME.length) : null;

/** The kind of a row's way on to a step after its source. */
const forwardOf = (row: WayRowKey): string => (row === 'done' ? 'after' : row === 'changes' ? 'rejected' : row === 'fails' ? 'failed' : 'when');

/** The rows a step's kind has: those its palette lets leave it, and each outcome it declares where it is a decision. */
function rowKeys(leaves: readonly string[], outcomes: readonly string[]): WayRowKey[] {
  return [
    ...(leaves.includes('after') ? ['done' as const] : []),
    ...(leaves.includes('rejected') ? ['changes' as const] : []),
    ...(leaves.includes('failed') ? ['fails' as const] : []),
    ...(leaves.includes('when') ? outcomes.map((outcome): WayRowKey => `${OUTCOME}${outcome}`) : []),
  ];
}

/** The row a way on stands in, or null where it fits none: a trigger, a kind recorded as written, an outcome its step does not declare. */
function rowKeyOf(keys: readonly WayRowKey[], wire: DraftWire): WayRowKey | null {
  const row = ((): WayRowKey | null => {
    switch (wire.kind) {
      case 'after': return 'done';
      case 'rejected': return 'changes';
      case 'failed': return 'fails';
      case 'when': return wire.outcome ? `${OUTCOME}${wire.outcome}` : null;
      case BACK: return wire.on === 'rejected' ? 'changes' : wire.on === 'failed' ? 'fails' : wire.on ? `${OUTCOME}${wire.on}` : null;
      default: return null;
    }
  })();
  return row !== null && keys.includes(row) ? row : null;
}

const keysOf = (palette: WorkflowPaletteAnswer, node: DraftNode) => rowKeys(leavesOf(palette, node), outcomesOf(node));

/** A step's *What happens next*: a row for each way it may go on, in the palette's order, each with the ways on in it. */
export function rowsOf(palette: WorkflowPaletteAnswer, draft: GraphDraft, id: string): WayRow[] {
  const node = nodeById(draft, id);
  if (!node) return [];
  const keys = keysOf(palette, node);
  const own = draft.wires.filter((wire) => wire.from === id);
  return keys.map((key) => ({
    key, outcome: key.startsWith(OUTCOME) ? key.slice(OUTCOME.length) : null, ways: own.filter((wire) => rowKeyOf(keys, wire) === key),
  }));
}

/** A step's ways on that fit no row (a trigger, a kind recorded as written), its *More ways*; a data wire is its target's *Carries*. */
export function moreWaysOf(palette: WorkflowPaletteAnswer, draft: GraphDraft, id: string): DraftWire[] {
  const node = nodeById(draft, id);
  if (!node) return [];
  const keys = keysOf(palette, node);
  return draft.wires.filter((wire) => wire.from === id && wire.kind !== DATA && rowKeyOf(keys, wire) === null);
}

/** The row a way on stands in from its own step, or null. */
export function rowOfWire(palette: WorkflowPaletteAnswer, draft: GraphDraft, wire: DraftWire): WayRowKey | null {
  const node = wire.from === null ? undefined : nodeById(draft, wire.from);
  return node ? rowKeyOf(keysOf(palette, node), wire) : null;
}

/** The steps upstream of `id` along the wires of the kinds `along` takes, a wire `except` set aside. */
function upstream(draft: GraphDraft, id: string, along: (kind: string) => boolean, except?: string): Set<string> {
  const into = new Map<string, string[]>();
  for (const wire of draft.wires) {
    if (wire.from === null || wire.id === except || !along(wire.kind)) continue;
    into.set(wire.to, [...(into.get(wire.to) ?? []), wire.from]);
  }
  const found = new Set<string>();
  const queue = [id];
  while (queue.length > 0) {
    for (const earlier of into.get(queue.shift()!) ?? []) {
      if (!found.has(earlier)) { found.add(earlier); queue.push(earlier); }
    }
  }
  return found;
}

/**
 * The steps `id` comes after (D223 §8.1): its ancestors upstream through `after`, `trigger`, `data` and `when`, as the driver reads a way
 * back's target (`NodeWires.BackProblem`), never a step merely drawn above it.
 */
export const earlierThan = (draft: GraphDraft, id: string, except?: string): Set<string> =>
  upstream(draft, id, (kind) => ORDERED.includes(kind), except);

/** The steps `id` waits on through any wire but a way back and a wait: a way on to one of them would close a cycle. */
const dependsOn = (draft: GraphDraft, id: string, except?: string): Set<string> =>
  upstream(draft, id, (kind) => kind !== BACK && kind !== 'wait', except);

/**
 * The way a row of `from` makes to `to`, its kind from the row and where `to` stands (the flowchart design §3, §8.1), or null where the
 * driver's wire rules would refuse every way it could make: on to a later step (`after`, `rejected`, `failed`, `when`), back to an earlier
 * one on the row's result (`back`), never to itself, to a step it waits on otherwise (a cycle), back twice on one result, or a second way
 * of one kind to one step. `except` is the wire being given this target, read as gone.
 */
export function wayTo(palette: WorkflowPaletteAnswer, draft: GraphDraft, from: string, row: WayRowKey, to: string, except?: string): WireAsk | null {
  const node = nodeById(draft, from);
  if (!node || to === from || !nodeById(draft, to)) return null;
  const leaves = leavesOf(palette, node);
  if (!rowKeys(leaves, outcomesOf(node)).includes(row)) return null;
  const on = onOf(row);
  const own = draft.wires.filter((wire) => wire.from === from && wire.id !== except);
  if (earlierThan(draft, from, except).has(to)) {
    if (on === null || !leaves.includes(BACK) || own.some((wire) => wire.kind === BACK && wire.on === on)) return null;
    return { kind: BACK, from, to, on };
  }
  if (dependsOn(draft, from, except).has(to)) return null;
  const kind = forwardOf(row);
  const outcome = kind === 'when' ? on : null;
  if (own.some((wire) => wire.kind === kind && wire.to === to && wire.outcome === outcome)) return null;
  return { kind, from, to, ...(outcome === null ? {} : { outcome }) };
}

/** Every step a row of `from` can go to, each with the way it would make: what the row's targets and the chart's pick mode offer. */
export function targetsOf(palette: WorkflowPaletteAnswer, draft: GraphDraft, from: string, row: WayRowKey, except?: string): { id: string; ask: WireAsk }[] {
  return draft.nodes.flatMap((node) => {
    const ask = wayTo(palette, draft, from, row, node.id, except);
    return ask ? [{ id: node.id, ask }] : [];
  });
}

/** The steps a trigger from `from` may start each time it is reached (*More ways*): any it does not wait on, but itself and one it starts already. */
export function triggerTargetsOf(draft: GraphDraft, from: string): string[] {
  const waits = dependsOn(draft, from);
  return draft.nodes.map((node) => node.id)
    .filter((id) => id !== from && !waits.has(id) && !draft.wires.some((wire) => wire.kind === 'trigger' && wire.from === from && wire.to === id));
}

/** A row's way to `to`, by the kind the row and the target give, and its wire's id; null where no way can go there. A forward way proposes `to`'s inputs. */
export function connect(palette: WorkflowPaletteAnswer, draft: GraphDraft, from: string, row: WayRowKey, to: string): { draft: GraphDraft; wire: string; carried: Carried[] } | null {
  const ask = wayTo(palette, draft, from, row, to);
  if (!ask) return null;
  const made = withWire(draft, ask);
  const proposed = ask.kind === BACK ? { draft: made.draft, carried: [] } : proposeInputs(made.draft, to, from);
  return { draft: proposed.draft, wire: made.id, carried: proposed.carried };
}

/**
 * A way on given another target in its row: its id and its fields kept, its kind what the new target gives, and a group left behind, since
 * a group gathers the ways into one step. Null where the new target cannot take the way.
 */
export function retarget(palette: WorkflowPaletteAnswer, draft: GraphDraft, id: string, to: string): GraphDraft | null {
  const was = draft.wires.find((wire) => wire.id === id);
  if (!was) return null;
  const row = rowOfWire(palette, draft, was);
  const ask = row === null ? null : wayTo(palette, draft, was.from!, row, to, id);
  if (!ask) return null;
  const { group: _group, ...fields } = was.wire;
  const moved: GraphDraft = {
    ...draft,
    wires: draft.wires.map((wire) => (wire.id === id
      ? { ...wire, kind: ask.kind, to, toPort: null, outcome: ask.outcome ?? null, on: ask.on ?? null, wire: to === was.to ? was.wire : fields }
      : wire)),
  };
  return ask.kind === BACK || to === was.to ? moved : proposeInputs(moved, to, was.from!).draft;
}

/* ---------------------------------------------------------------- inserting, duplicating, moving, removing */

/** Where a new step goes: on a way (between its ends), at the end of a row, after a step in its chain, or unconnected. */
export type InsertAt = { on: string } | { from: string; row: WayRowKey } | { after: string } | { free: true };

/** The ways a step may be put between the ends of: a way on, a decision's route, a failure's or a refusal's path. */
const INSERTABLE: readonly string[] = ['after', 'when', 'failed', 'rejected'];

/** Whether a step may stand on a way: one of those kinds, in no group, between two steps of the draft. */
export function canInsertOn(draft: GraphDraft, id: string): boolean {
  const wire = draft.wires.find((each) => each.id === id);
  return wire !== undefined && wire.from !== null && INSERTABLE.includes(wire.kind) && !('group' in wire.wire)
    && nodeById(draft, wire.from) !== undefined && nodeById(draft, wire.to) !== undefined;
}

/** A new step at `at`; the draft unchanged where the way cannot take one. */
function placeAt(draft: GraphDraft, step: DraftNode, at: InsertAt, mayEnd: boolean): { draft: GraphDraft; id: string } {
  if ('on' in at) {
    if (!canInsertOn(draft, at.on)) return { draft, id: step.id };
    const way = draft.wires.find((each) => each.id === at.on)!;
    const placed = place(draft, step, mayEnd);
    const rewired = { ...placed, wires: placed.wires.map((wire) => (wire.id === way.id ? { ...wire, to: step.id, toPort: null } : wire)) };
    return { draft: withWire(rewired, { kind: 'after', from: step.id, to: way.to }).draft, id: step.id };
  }
  if ('after' in at) {
    const on = draft.wires.filter((wire) => wire.from === at.after && wire.kind === 'after');
    if (on.length === 1 && canInsertOn(draft, on[0]!.id)) return placeAt(draft, step, { on: on[0]!.id }, mayEnd);
    return placeAt(draft, step, { from: at.after, row: 'done' }, mayEnd);
  }
  if ('from' in at) {
    const kind = forwardOf(at.row);
    const outcome = kind === 'when' ? onOf(at.row) : null;
    return { draft: withWire(place(draft, step, mayEnd), { kind, from: at.from, to: step.id, ...(outcome === null ? {} : { outcome }) }).draft, id: step.id };
  }
  return { draft: place(draft, step, mayEnd), id: step.id };
}

/**
 * A step from the palette put at `at` (the flowchart design §3): on a way, A → new → B, the way keeping its id, kind and outcome into the
 * new step, which goes on to B once done; at a row's end, by the row's own kind; after a step, between it and its one plain way on where
 * it has one, else following it; or unconnected, a new start. Nothing else is rewired.
 */
export function insertStep(_palette: WorkflowPaletteAnswer, draft: GraphDraft, choice: StepChoice, at: InsertAt): { draft: GraphDraft; id: string; carried: Carried[] } {
  const id = idFor(choice.kind, taken(draft));
  const made = placeAt(draft, { id, kind: choice.kind, node: nodeOf(choice, id) }, at, choice.mayEnd);
  if (made.draft === draft) return { ...made, carried: [] };
  // The new step's inputs are proposed from the step it follows, and the step it now precedes is offered the new step's outputs.
  const way = 'on' in at ? draft.wires.find((each) => each.id === at.on) : undefined;
  const before = way ? way.from : 'after' in at ? at.after : 'from' in at ? at.from : null;
  const fed = before === null ? { draft: made.draft, carried: [] } : proposeInputs(made.draft, id, before);
  const onward = way ? proposeInputs(fed.draft, way.to, id) : { draft: fed.draft, carried: [] };
  return { draft: onward.draft, id, carried: [...fed.carried, ...onward.carried] };
}

/** A copy of a step, its options and declaration whole under a new id, put after it in its chain. */
export function duplicateStep(draft: GraphDraft, id: string): { draft: GraphDraft; id: string } {
  const node = nodeById(draft, id);
  if (!node) return { draft, id };
  const copy = idFor(node.kind, taken(draft));
  return placeAt(draft, { id: copy, kind: node.kind, node: { ...clone(node.node), id: copy } }, { after: id }, false);
}

/** The ways into a step, a data wire's apart: what the run comes to it by. */
const waysIn = (draft: GraphDraft, id: string) => draft.wires.filter((wire) => wire.to === id && wire.kind !== DATA);

/** The ways out of a step, a data wire's apart: where it goes on, back or after a failure. */
const waysOut = (draft: GraphDraft, id: string) => draft.wires.filter((wire) => wire.from === id && wire.kind !== DATA);

/** A plain link of a chain: an `after` in no group. */
const plainLink = (wire: DraftWire) => wire.kind === 'after' && !('group' in wire.wire);

/** A move within a chain, as its preview names it: the step before, the two that change places in their new order, and the step after. */
export type MovePlan = { before: string | null; first: string; second: string; after: string | null };

/** The two steps a plain link joins changed in place, and the links that rewire, or null where either is more than a chain's link. */
function swapOf(draft: GraphDraft, link: DraftWire): { plan: MovePlan; into: DraftWire | null; out: DraftWire | null } | null {
  const upper = link.from;
  const lower = link.to;
  if (upper === null || !plainLink(link)) return null;
  const upperOut = waysOut(draft, upper);
  const lowerIn = waysIn(draft, lower);
  if (upperOut.length !== 1 || upperOut[0] !== link || lowerIn.length !== 1 || lowerIn[0] !== link) return null;
  if (draft.wires.some((wire) => wire.kind === DATA && ((wire.from === upper && wire.to === lower) || (wire.from === lower && wire.to === upper)))) return null;
  const upperIn = waysIn(draft, upper);
  const lowerOut = waysOut(draft, lower);
  if (upperIn.length > 1 || (upperIn[0] && !plainLink(upperIn[0])) || lowerOut.length > 1 || (lowerOut[0] && !plainLink(lowerOut[0]))) return null;
  return {
    plan: { before: upperIn[0]?.from ?? null, first: lower, second: upper, after: lowerOut[0]?.to ?? null },
    into: upperIn[0] ?? null,
    out: lowerOut[0] ?? null,
  };
}

/** The link a move of `id` swaps along: its one way in to go earlier, its one way on to go later. */
function linkFor(draft: GraphDraft, id: string, dir: 'earlier' | 'later'): DraftWire | null {
  const ways = dir === 'earlier' ? waysIn(draft, id) : waysOut(draft, id);
  return ways.length === 1 ? ways[0]! : null;
}

/** What moving `id` earlier or later in its chain would do, for its preview; null where it is not a plain chain's link. */
export function movePlan(draft: GraphDraft, id: string, dir: 'earlier' | 'later'): MovePlan | null {
  const link = linkFor(draft, id, dir);
  return link ? swapOf(draft, link)?.plan ?? null : null;
}

/**
 * A step moved one place earlier or later within a plain chain (`… → A → B → C → …` to `… → B → A → …`): the three links keep their ids
 * and only their ends change. Null where it is more than a chain's link; its other ways stay on their steps.
 */
export function moveInChain(draft: GraphDraft, id: string, dir: 'earlier' | 'later'): GraphDraft | null {
  const link = linkFor(draft, id, dir);
  const swap = link ? swapOf(draft, link) : null;
  if (!link || !swap) return null;
  const { first, second } = swap.plan;
  return {
    ...draft,
    wires: draft.wires.map((wire) => {
      if (wire === link) return { ...wire, from: first, to: second };
      if (wire === swap.into) return { ...wire, to: first };
      if (wire === swap.out) return { ...wire, from: second };
      return wire;
    }),
  };
}

/** Where *Remove and join* joins: the step's one plain way in and one plain way on, from the step before to the step after (§8.2). */
export type JoinPlan = { from: string; to: string; in: string; out: string };

/** The join a removal may make, or null where the step's one way in and one way on are not both plain links, or the join is there already. */
export function joinOf(draft: GraphDraft, id: string): JoinPlan | null {
  const into = waysIn(draft, id);
  const out = waysOut(draft, id);
  if (into.length !== 1 || out.length !== 1 || !plainLink(into[0]!) || !plainLink(out[0]!)) return null;
  const [from, to] = [into[0]!.from!, out[0]!.to];
  if (from === to || draft.wires.some((wire) => wire.kind === 'after' && wire.from === from && wire.to === to)) return null;
  return { from, to, in: into[0]!.id, out: out[0]!.id };
}

/**
 * A step removed with every wire into and out of it, nothing bridged; or, `join`, its one plain way in kept and taken on to where its one
 * plain way on went. Where it was the end, the end goes to `end`, else none is set.
 */
export function removeStep(draft: GraphDraft, id: string, options: { join?: boolean; end?: string } = {}): GraphDraft {
  const join = options.join ? joinOf(draft, id) : null;
  return {
    ...draft,
    nodes: draft.nodes.filter((node) => node.id !== id),
    wires: draft.wires
      .filter((wire) => (join !== null && wire.id === join.in) || (wire.from !== id && wire.to !== id))
      .map((wire) => (join !== null && wire.id === join.in ? { ...wire, to: join.to } : wire)),
    end: draft.end === id ? options.end ?? '' : draft.end,
  };
}

/* ---------------------------------------------------------------- what a step carries, and how its ways in join */

/** Where an input may be fed from: a step's output or evidence of the input's schema, on a step that does not come after it. */
export type CarrySource = { from: string; port: string };

/** The sources a step's input may be carried from, in the draft's order; its own and those after it would close a cycle. */
export function sourcesOf(draft: GraphDraft, to: string, input: string): CarrySource[] {
  const schema = text(record(nodeById(draft, to)?.node.inputs)[input]);
  if (schema === null) return [];
  return draft.nodes.flatMap((source) => {
    if (source.id === to || dependsOn(draft, source.id).has(to)) return [];
    const ports = Object.entries(record(source.node.outputs)).filter(([, made]) => made === schema).map(([port]) => port);
    if (text(source.node.evidence) === schema) ports.push('evidence');
    return ports.map((port) => ({ from: source.id, port }));
  });
}

/** The data wire that feeds a step's input, or null. */
export const carryOf = (draft: GraphDraft, to: string, input: string): DraftWire | null =>
  draft.wires.find((wire) => wire.kind === DATA && wire.to === to && wire.toPort === input) ?? null;

/** What a step gives others: the data wires out of it. */
export const givesOf = (draft: GraphDraft, from: string): DraftWire[] => draft.wires.filter((wire) => wire.kind === DATA && wire.from === from);

/** A step's input fed from `source`, the wire that fed it keeping its id; none, and nothing feeds it. */
export function setCarry(draft: GraphDraft, to: string, input: string, source: CarrySource | null): GraphDraft {
  const feeding = draft.wires.filter((wire) => wire.kind === DATA && wire.to === to && wire.toPort === input);
  if (source === null) return { ...draft, wires: draft.wires.filter((wire) => !feeding.includes(wire)) };
  if (feeding.length === 0) return withWire(draft, { kind: DATA, from: source.from, fromPort: source.port, to, toPort: input }).draft;
  const [kept, ...rest] = feeding;
  return {
    ...draft,
    wires: draft.wires.filter((wire) => !rest.includes(wire)).map((wire) => (wire === kept ? { ...wire, from: source.from, fromPort: source.port } : wire)),
  };
}

/** An input given an output when a way was made into its step: the step, its input and the output of the step it comes from. */
export type Carried = { step: string; input: string; from: string; port: string };

/**
 * The inputs of `to` left unfed, fed from an output of the step its way comes from (SETUPUX1r; D221 §2.3): that step's own output of the
 * input's schema where it makes one, else the one other step that makes it. Where two fit, none is chosen and the step's *Carries* asks.
 */
function proposeInputs(draft: GraphDraft, to: string, from: string): { draft: GraphDraft; carried: Carried[] } {
  const node = nodeById(draft, to);
  if (!node) return { draft, carried: [] };
  let next = draft;
  const carried: Carried[] = [];
  for (const input of inputsOf(node)) {
    if (carryOf(next, to, input)) continue;
    const sources = sourcesOf(next, to, input);
    const own = sources.filter((source) => source.from === from);
    const pick = own.length === 1 ? own[0]! : own.length === 0 && sources.length === 1 ? sources[0]! : null;
    if (!pick) continue;
    next = setCarry(next, to, input, pick);
    carried.push({ step: to, input, from: pick.from, port: pick.port });
  }
  return { draft: next, carried };
}

/** The inputs of a step that nothing feeds and more than one output fits: what its *Carries* asks the person to choose. */
export function askedInputs(draft: GraphDraft, id: string): { input: string; sources: CarrySource[] }[] {
  const node = nodeById(draft, id);
  if (!node) return [];
  return inputsOf(node).filter((input) => carryOf(draft, id, input) === null)
    .map((input) => ({ input, sources: sourcesOf(draft, id, input) })).filter((each) => each.sources.length > 1);
}

/* ---------------------------------------------------------------- a landing's method, and the tail it brings */

/** The five ways a `land` lands (`LandNodes.Methods`). */
export const LAND_METHODS = ['merge', 'branch', 'push', 'pull-request', 'integrate'] as const;

/** The steps a method brings after the landing, in order (`NodeGraphs`' `land-tail`): a pull request is pushed, then completed on the host. */
const TAILS: Readonly<Record<string, readonly string[]>> = { push: ['pushed'], 'pull-request': ['pushed', 'pull-request'], integrate: ['integrate'] };

export const tailOf = (method: string | null): readonly string[] => (method === null ? [] : TAILS[method] ?? []);

const TAIL_KINDS: readonly string[] = ['pushed', 'pull-request', 'integrate'];

/** The options a method refuses (`LandNodes.Problem`): a merge makes no branch to name nor plugin to push it, an integrate neither a plugin nor a squash. */
const REFUSED: Readonly<Record<string, readonly string[]>> = { merge: ['pattern', 'plugin'], integrate: ['plugin', 'squash'] };

/** Whether a land option means something under the method it is held beside; `method` unset leaves each to the repository's rule. */
export function landOptionApplies(method: string | null, name: string): boolean {
  return method === null || !(REFUSED[method] ?? []).includes(name);
}

/**
 * A landing's method set and its tail brought in one change (SETUPUX1r; the land node design §2): the steps the new method brings are put
 * after the landing (a pull request's pushed, then its request; a push's pushed; an integrate), between it and what followed it; the tail
 * steps the method does not bring go, each joined over; the options it refuses are cleared; and the end follows the tail where the landing
 * or a removed tail step was the end. Everything else the graph holds is kept as written.
 */
export function setLandMethod(palette: WorkflowPaletteAnswer, draft: GraphDraft, id: string, method: string): { draft: GraphDraft; added: string[]; removed: string[] } {
  if (!nodeById(draft, id)) return { draft, added: [], removed: [] };
  let next = setOption(draft, id, 'method', method);
  for (const name of REFUSED[method] ?? []) next = setOption(next, id, name, undefined);
  if (method === 'push' || method === 'pull-request') {
    if (optionOf(nodeById(next, id)!, 'plugin') === 'none') next = setOption(next, id, 'plugin', undefined);
  }
  const brought = tailOf(method);
  const removed: string[] = [];
  const gone = new Set<string>();
  for (const node of next.nodes.filter((each) => TAIL_KINDS.includes(each.kind) && !brought.includes(each.kind))) {
    next = removeStep(next, node.id, { join: true, end: id });
    removed.push(node.kind);
    gone.add(node.id);
  }
  const added: string[] = [];
  let last = id;
  for (const kind of brought) {
    const held = next.nodes.find((each) => each.kind === kind);
    if (held) { last = held.id; continue; }
    const choice = choiceOf(palette, kind);
    if (!choice) continue;
    const made = insertStep(palette, next, choice, { after: last });
    next = made.draft;
    last = made.id;
    added.push(kind);
  }
  if (gone.has(draft.end) || (draft.end === id && added.length > 0)) next = { ...next, end: last };
  return { draft: next, added, removed };
}

/** The kinds that make a step eligible, which a group gathers (`WireKinds.Eligibility`). */
const ELIGIBILITY: readonly string[] = ['after', 'trigger', 'failed', 'rejected', 'when'];

const eligibleInto = (draft: GraphDraft, id: string) => draft.wires.filter((wire) => wire.to === id && wire.from !== null && ELIGIBILITY.includes(wire.kind));

/**
 * How a step's ways in join: `all` where it waits for every one, `any` where one group takes the first, `recorded` where its groups are
 * other than this page writes, kept as written; null where it has fewer than two.
 */
export function joinModeOf(draft: GraphDraft, id: string): 'all' | 'any' | 'recorded' | null {
  const into = eligibleInto(draft, id);
  if (into.length < 2) return null;
  const groups = into.map((wire) => record(wire.wire.group));
  if (groups.every((group) => Object.keys(group).length === 0)) return 'all';
  const first = text(groups[0]!.id);
  return first !== null && groups.every((group) => group.mode === 'anyOf' && group.id === first) ? 'any' : 'recorded';
}

/** A step's ways in set to wait for every one, or gathered into one group that takes the first. */
export function setJoinMode(draft: GraphDraft, id: string, mode: 'all' | 'any'): GraphDraft {
  const into = new Set(eligibleInto(draft, id));
  return {
    ...draft,
    wires: draft.wires.map((wire) => {
      if (!into.has(wire)) return wire;
      const { group: _group, ...fields } = wire.wire;
      return { ...wire, wire: mode === 'any' ? { ...fields, group: { id: `${id}-any`, mode: 'anyOf' } } : fields };
    }),
  };
}

/* ---------------------------------------------------------------- the local clipboard */

/** Steps copied within one draft: their nodes, the ways between them, and how many ways to other steps were left out. */
export type Clip = { nodes: DraftNode[]; wires: DraftWire[]; dropped: number };

/** Steps copied with the ways between them; a way to a step not copied is left out and counted, for the paste to say. */
export function copySteps(draft: GraphDraft, ids: readonly string[]): Clip {
  const copied = new Set(ids);
  const nodes = draft.nodes.filter((node) => copied.has(node.id));
  const touching = draft.wires.filter((wire) => (wire.from !== null && copied.has(wire.from)) || copied.has(wire.to));
  const wires = touching.filter((wire) => wire.from !== null && copied.has(wire.from) && copied.has(wire.to));
  return { nodes: clone(nodes), wires: clone(wires), dropped: touching.length - wires.length };
}

/** A clip pasted under fresh ids, unconnected to the rest, the ways between its steps kept under fresh wire ids. */
export function pasteSteps(draft: GraphDraft, clip: Clip): { draft: GraphDraft; ids: string[]; dropped: number } {
  let next = draft;
  const named = new Map<string, string>();
  for (const node of clip.nodes) {
    const id = idFor(node.kind, taken(next));
    named.set(node.id, id);
    next = place(next, { id, kind: node.kind, node: { ...clone(node.node), id } }, false);
  }
  for (const wire of clip.wires) {
    next = withWire(next, {
      kind: wire.kind, from: named.get(wire.from!)!, to: named.get(wire.to)!, ...(wire.fromPort ? { fromPort: wire.fromPort } : {}),
      ...(wire.toPort ? { toPort: wire.toPort } : {}), ...(wire.outcome ? { outcome: wire.outcome } : {}), ...(wire.on ? { on: wire.on } : {}),
    }, clone(wire.wire)).draft;
  }
  return { draft: next, ids: [...named.values()], dropped: clip.dropped };
}

/* ---------------------------------------------------------------- the workflow's id, and history */

const slug = (text: string) => text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');

/**
 * A new workflow's id from its name (D221 §2.3, as role ids are made): its words in lower case joined by dashes; a name with no ASCII letter
 * or digit takes the id it was copied from, numbered (`release` → `release-2`), else `workflow`; numbered on past an id saved already.
 */
export function workflowIdFor(name: string, copiedFrom: string | null, saved: readonly string[]): string {
  const named = slug(name);
  const base = named || (copiedFrom ? slug(copiedFrom) : '') || 'workflow';
  let n = named || !copiedFrom ? 1 : 2;
  let id = n === 1 ? base : `${base}-${n}`;
  while (saved.includes(id)) {
    n += 1;
    id = `${base}-${n}`;
  }
  return id;
}

/** The editor's history: what was, what is, and what an undo has set aside for a redo. */
export type History<T> = { past: readonly T[]; present: T; future: readonly T[] };

/** How many changes an undo can go back. */
const HISTORY_MOST = 100;

export const historyOf = <T>(present: T): History<T> => ({ past: [], present, future: [] });

/** One change: a step of history, and nothing left to redo. */
export const commit = <T>(history: History<T>, next: T): History<T> =>
  ({ past: [...history.past, history.present].slice(-HISTORY_MOST), present: next, future: [] });

export const undo = <T>(history: History<T>): History<T> => (history.past.length === 0 ? history
  : { past: history.past.slice(0, -1), present: history.past[history.past.length - 1]!, future: [history.present, ...history.future] });

export const redo = <T>(history: History<T>): History<T> => (history.future.length === 0 ? history
  : { past: [...history.past, history.present], present: history.future[0]!, future: history.future.slice(1) });
