import type { OptionField, WorkflowPaletteAnswer } from './answers';

// The step editor's draft (WORKFLOWEDIT1b): a schema-2 graph as the person composes it, the inverse of the run view's drawing
// (`workflowFlow.ts`, which reads a graph's wires onto edges). Pure: each change returns a new draft, and `graphOf` writes the graph the
// driver reads, judges and saves. A step is added from the palette, its kind copied whole, so a kind this page has no words for is
// composed like any other. What a saved graph holds that the editor does not draw (a group, a retry, its cleanup) is kept as written.

/** A step the palette offers, a built-in kind's or a plugin's declared step, in one shape. */
export type StepChoice = {
  kind: string; version: number; reach: string; plugin: string | null; outcomes: string[]; inputs: Record<string, string>;
  outputs: Record<string, string>; evidence: string | null; rejectable: boolean; skippable: boolean; leaves: string[];
  options: OptionField[]; roleTakes: string[]; mayEnd: boolean;
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
      kind: kind.kind, version: 1, reach: kind.reach, plugin: null, outcomes: [], inputs: {}, outputs: {}, evidence: null, rejectable: false,
      skippable: true, leaves: kind.leaves ?? [], options: kind.options, roleTakes: kind.roleTakes ?? [], mayEnd: kind.mayEnd,
    })),
    ...palette.plugins.flatMap(({ plugin, steps }) => steps.map((step): StepChoice => ({
      kind: step.kind, version: step.version ?? 1, reach: step.reach, plugin, outcomes: step.outcomes ?? [], inputs: step.inputs, outputs: step.outputs,
      evidence: step.evidence ?? null, rejectable: step.rejectable, skippable: step.skippable, leaves: step.leaves ?? [], options: [], roleTakes: [],
      mayEnd: true,
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

/**
 * A step added from the palette, its declaration copied as the palette says it (a plugin's outcomes, inputs, outputs and evidence), and
 * the draft's end where none is set yet and the kind may end a workflow.
 */
export function addStep(draft: GraphDraft, choice: StepChoice): { draft: GraphDraft; id: string } {
  const id = idFor(choice.kind, new Set(draft.nodes.map((node) => node.id)));
  const node: Record<string, unknown> = {
    id, kind: choice.kind, version: choice.version, reach: choice.reach,
    ...(choice.outcomes.length > 0 ? { outcomes: [...choice.outcomes] } : {}),
    ...(Object.keys(choice.inputs).length > 0 ? { inputs: { ...choice.inputs } } : {}),
    ...(Object.keys(choice.outputs).length > 0 ? { outputs: { ...choice.outputs } } : {}),
    ...(choice.evidence ? { evidence: choice.evidence } : {}),
    ...(choice.rejectable ? { rejectable: true } : {}),
    ...(choice.skippable ? {} : { skippable: false }),
  };
  return {
    draft: { ...draft, nodes: [...draft.nodes, { id, kind: choice.kind, node }], end: draft.end || (choice.mayEnd ? id : '') },
    id,
  };
}

/** A step removed, with every wire into and out of it; the end is unset where it was the end. */
export function removeStep(draft: GraphDraft, id: string): GraphDraft {
  return {
    ...draft,
    nodes: draft.nodes.filter((node) => node.id !== id),
    wires: draft.wires.filter((wire) => wire.from !== id && wire.to !== id),
    end: draft.end === id ? '' : draft.end,
  };
}

/** A way one step goes on to another, under the next free wire id. */
export function addWire(draft: GraphDraft, ask: WireAsk): GraphDraft {
  const wire: DraftWire = {
    id: wireId(draft), kind: ask.kind, from: ask.from, to: ask.to, fromPort: ask.fromPort ?? null, toPort: ask.toPort ?? null,
    outcome: ask.outcome ?? null, on: ask.on ?? null, wire: {},
  };
  return { ...draft, wires: [...draft.wires, wire] };
}

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

/** What a way back from a step may be taken on: the person's rejection, its spent failure, and each of its outcomes, as the palette allows. */
export function onChoices(palette: WorkflowPaletteAnswer, node: DraftNode): string[] {
  const leaves = leavesOf(palette, node);
  if (!leaves.includes(BACK)) return [];
  return [...(leaves.includes('rejected') ? ['rejected'] : []), ...(leaves.includes('failed') ? ['failed'] : []), ...outcomesOf(node)];
}
