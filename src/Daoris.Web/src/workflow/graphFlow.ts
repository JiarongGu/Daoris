import type { i18n as I18n, TFunction } from 'i18next';
import type { FlowEdge, FlowNode, FlowSection, PillTone } from '../design/Flow';
import type { IconName } from '../design/ui';

// One drawing of a workflow graph for the place, the editor and a run (SETUPUX1n; D221, the Setup design's §2.3): a graph's steps and
// ways on read onto `Flow`'s nodes and edges. Always in sections, so top to bottom or as the rail and never left to right; its
// endings drawn as they are: the configured end flagged where the work is done, a ◎ node for an outcome with no route, and *Finished*
// only from a run's record. The declared checks, requests and holds are one group. Pure and worded: it adds no node's lines or door,
// which are the caller's.

/** A workflow's node track, in rem: a step's title and one fact, with its cell on selection (the Setup design's §2.3). */
export const WORKFLOW_TRACK = { least: 14, most: 36 } as const;

/** The end of a run, a node of its own: an id no step can take, since a step's is lower-case letters, digits and dashes. */
export const FINISHED = ':finished';

/** The kinds of the declared holds, drawn as one group when two or more follow one another. */
const HOLD_KINDS: readonly string[] = ['work.requests', 'quest.holds', 'content.holds'];

/** Whether a step's kind is one of the declared holds the drawing groups. */
export const isHoldKind = (kind: string) => HOLD_KINDS.includes(kind);

/** The kinds of the landing, the hinge between *Before it lands* and *After it lands*. */
const LANDING_KINDS: readonly string[] = ['land', 'landing'];

/** A step to draw: `Flow`'s node, worded by the caller, and the kind and declared outcomes the drawing reads. */
export type DrawnNode = FlowNode & { kind: string; outcomes?: readonly string[] };

/** A way on to draw: a graph's wire, by its kind, with the words of its cause where the caller has its own. */
export type DrawnWire = {
  id: string; kind: string; from: string; to: string; outcome?: string | null; on?: string | null; says?: string; label?: string;
};

export type DrawnGraph = { nodes: DrawnNode[]; wires: DrawnWire[]; entries: readonly string[]; end?: string | null };

export type DrawOptions = {
  /** Names the drawing for a reader. */
  label: string;
  /** The declared holds' group: opened (their steps drawn, the first with the word to fold them), and the press that toggles it. */
  holds?: { open?: boolean; toggle?: () => void };
  /** A run's record of its finish: *Finished* is drawn only where this is. */
  finished?: { word: string; tone: PillTone } | null;
};

export type GraphFlow = {
  label: string;
  nodes: FlowNode[];
  edges: FlowEdge[];
  entries: string[];
  sections: FlowSection[];
  track: typeof WORKFLOW_TRACK;
};

const plain = (text: string) => text.replace(/`/g, '');

/** A step's kind in the reader's words: a plugin's step by its plugin, a built-in by its name, a kind this page has none for as spelled. */
export function kindTitle(t: TFunction, i18n: I18n, kind: string): string {
  const slash = kind.indexOf('/');
  if (slash > 0) return t('workflow.edit.kind.plugin', { step: kind.slice(slash + 1), plugin: kind.slice(0, slash) });
  const key = `workflow.edit.kind.${kind}`;
  return i18n.exists(key) ? t(key) : t('workflow.edit.unknownKind', { kind });
}

const record = (value: unknown): Record<string, unknown> =>
  value && typeof value === 'object' && !Array.isArray(value) ? (value as Record<string, unknown>) : {};
const text = (value: unknown): string | null => (typeof value === 'string' ? value : null);
const names = (value: unknown): string[] => (Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string') : []);

const GLYPH: Record<string, IconName> = { claimed: 'stepWork', opinion: 'stepOpinion', look: 'stepLook', 'pull-request': 'stepPullRequest' };

/** A step's glyph from its kind; a landing by its method. */
const glyphOfKind = (kind: string, options: Record<string, unknown>): IconName | undefined =>
  kind === 'land' ? (options.method === 'branch' ? 'stepBranch' : 'stepMerge') : GLYPH[kind];

/**
 * A saved graph's JSON (a schema-2 graph, as a version or a shipped workflow holds it) read onto the drawing, each step titled by its kind.
 * Lenient: what is not a graph's shape reads as an empty part, for the driver to judge.
 */
export function drawnFromJson(t: TFunction, i18n: I18n, json: unknown): DrawnGraph {
  const graph = record(json);
  const nodes = (Array.isArray(graph.nodes) ? graph.nodes : []).map(record).map((node): DrawnNode => {
    const kind = text(node.kind) ?? '';
    const glyph = glyphOfKind(kind, record(node.options));
    return {
      id: text(node.id) ?? '', kind, title: plain(kindTitle(t, i18n, kind)), outcomes: names(node.outcomes), ...(glyph ? { glyph } : {}),
    };
  });
  const wires = (Array.isArray(graph.wires) ? graph.wires : []).map(record).map((wire): DrawnWire => {
    const kind = text(wire.kind) ?? '';
    // A data wire names its ports after a dot; it is no way on and is not drawn, but its ends read as steps.
    const end = (named: string | null) => (named === null ? '' : kind === 'data' && named.includes('.') ? named.slice(0, named.lastIndexOf('.')) : named);
    return { id: text(wire.id) ?? '', kind, from: end(text(wire.from)), to: end(text(wire.to)), outcome: text(wire.outcome), on: text(wire.on) };
  });
  return { nodes, wires, entries: names(graph.entries), end: text(graph.end) };
}

/** A wire's edge: what it waits on, said; a failure's and a refusal's path in their own style; a decision's route labelled with its outcome. */
function wireEdge(t: TFunction, wire: DrawnWire): FlowEdge {
  const base = { id: wire.id, from: wire.from, to: wire.to };
  const own = wire.says === undefined ? {} : { says: wire.says };
  const edge = ((): FlowEdge => {
    switch (wire.kind) {
      case 'after': return { ...base, says: t('workflow.flow.move.automatic') };
      case 'failed': return { ...base, style: 'failed', says: t('workflow.flow.failed'), label: t('workflow.flow.failedLabel') };
      case 'rejected': return { ...base, style: 'rejected', says: t('workflow.flow.refused'), label: t('workflow.flow.refusedLabel') };
      case 'when': {
        const outcome = wire.outcome ?? '';
        return { ...base, style: 'conditional', says: t('workflow.run.flow.when', { outcome }), label: outcome };
      }
      case 'back': {
        const on = wire.on ?? '';
        const says = on === 'rejected' ? t('workflow.run.flow.back.rejected') : on === 'failed' ? t('workflow.run.flow.back.failed')
          : t('workflow.run.flow.back.outcome', { outcome: on });
        return { ...base, style: 'back', says, label: t('workflow.run.flow.back.label') };
      }
      case 'wait': case 'trigger': return { ...base, says: t(`workflow.flow.wire.${wire.kind}`) };
      default: return { ...base, says: t('workflow.flow.wire.recorded', { kind: wire.kind }) };
    }
  })();
  return { ...edge, ...own, ...(wire.label === undefined ? {} : { label: wire.label }) };
}

/** A way that goes on from a step, as opposed to one that returns or merely carries what a step made. */
const goesOn = (wire: DrawnWire) => wire.kind !== 'back' && wire.kind !== 'failed' && wire.kind !== 'rejected';

/**
 * The runs of two or more declared holds that follow one another, each a link of a chain: one way in and one out, and no outcome, end or
 * failure of its own. Found from the shape alone, in the graph's declared order. `Flow` stacks only three or more like nodes, and a
 * workflow of documents holds two.
 */
function holdGroups(graph: DrawnGraph, wires: DrawnWire[]): DrawnNode[][] {
  const byId = new Map(graph.nodes.map((node) => [node.id, node]));
  const into = (id: string) => wires.filter((wire) => wire.to === id);
  const out = (id: string) => wires.filter((wire) => wire.from === id);
  const link = (node: DrawnNode) =>
    HOLD_KINDS.includes(node.kind) && (node.outcomes?.length ?? 0) === 0 && node.id !== graph.end
    && into(node.id).every((wire) => wire.kind === 'after') && out(node.id).every((wire) => wire.kind === 'after') && out(node.id).length <= 1;
  const taken = new Set<string>();
  const groups: DrawnNode[][] = [];
  for (const start of graph.nodes) {
    if (taken.has(start.id) || !link(start)) continue;
    const before = into(start.id)[0];
    // A run starts where the step before it is not a link of the same chain.
    const previous = before ? byId.get(before.from) : undefined;
    if (previous && link(previous) && into(start.id).length === 1) continue;
    const members = [start];
    for (let next = out(start.id)[0]; next;) {
      const node = byId.get(next.to);
      if (!node || !link(node) || into(node.id).length !== 1 || members.includes(node)) break;
      members.push(node);
      next = out(node.id)[0];
    }
    if (members.length < 2) continue;
    for (const member of members) taken.add(member.id);
    groups.push(members);
  }
  return groups;
}

/** What a group wears of its members' lit state: the one the run stands at, else the last not done; reached if any is. */
function groupLit(members: DrawnNode[]): Pick<FlowNode, 'state' | 'reached' | 'current' | 'settled'> {
  if (members.every((member) => member.state === undefined && member.reached === undefined)) return {};
  const standing = members.find((member) => member.current) ?? members.find((member) => member.state && member.state.tone !== 'done')
    ?? [...members].reverse().find((member) => member.state);
  const reached = members.some((member) => member.reached === true) ? true : members.every((member) => member.reached === false) ? false : undefined;
  return {
    ...(standing?.state ? { state: standing.state } : {}),
    ...(reached === undefined ? {} : { reached }),
    ...(members.some((member) => member.current) ? { current: true } : {}),
    ...(members.every((member) => member.settled) ? { settled: true } : {}),
  };
}

const without = ({ kind: _kind, outcomes: _outcomes, ...node }: DrawnNode): FlowNode => node;

/**
 * **A workflow graph as a flowchart**: its steps and ways on, the declared holds folded to one group (or opened), the configured end
 * flagged *Work done here*, a drawing-only ◎ *No further step* for each declared outcome nothing follows, *Finished* only where a run's
 * record says it finished, and *Before it lands* and *After it lands* as its sections (else *Steps*), so `Flow` never lays it across.
 */
export function graphFlow(t: TFunction, graph: DrawnGraph, options: DrawOptions): GraphFlow {
  const known = new Set(graph.nodes.map((node) => node.id));
  // A data wire carries what a step made and is no way on; a wire to a step this graph does not hold is not drawn.
  const wires = graph.wires.filter((wire) => wire.kind !== 'data' && known.has(wire.from) && known.has(wire.to));
  const groups = holdGroups(graph, wires);
  const folded = options.holds?.open !== true;

  const groupOf = new Map<string, { id: string; members: DrawnNode[] }>();
  for (const members of groups) for (const member of members) groupOf.set(member.id, { id: `holds:${members[0]!.id}`, members });
  const rep = (id: string) => (folded ? groupOf.get(id)?.id ?? id : id);

  const nodes: FlowNode[] = [];
  const seen = new Set<string>();
  for (const node of graph.nodes) {
    const group = groupOf.get(node.id);
    if (group && folded) {
      if (seen.has(group.id)) continue;
      seen.add(group.id);
      nodes.push({
        id: group.id, title: t('workflow.graph.holds', { count: group.members.length }), glyph: 'stepHold',
        lines: t('workflow.graph.holdsMembers', { members: group.members.map((member) => member.title).join(t('workflow.listJoin')) }),
        ...(options.holds?.toggle ? { open: options.holds.toggle } : {}),
        ...groupLit(group.members),
      });
      continue;
    }
    const first = group && group.members[0] === node;
    nodes.push({
      ...without(node),
      ...(first ? {
        lines: node.lines ?? t('workflow.graph.holdsOpen', { count: group.members.length }),
        ...(options.holds?.toggle ? { open: options.holds.toggle } : {}),
      } : {}),
    });
  }

  const edges: FlowEdge[] = [];
  for (const wire of wires) {
    const from = rep(wire.from);
    const to = rep(wire.to);
    if (from === to && wire.from !== wire.to) continue;
    edges.push(wireEdge(t, { ...wire, from, to }));
  }

  // The end the configuration names: flagged where the work is done, and the foot says so where steps still follow it.
  const end = graph.end ? rep(graph.end) : null;
  const stepsFollow = end !== null && wires.some((wire) => rep(wire.from) === end && goesOn(wire));
  const marked = nodes.map((node) => (node.id === end
    ? { ...node, mark: { word: t('workflow.graph.end'), foot: stepsFollow ? t('workflow.graph.endStill') : null } }
    : node));

  // A declared outcome nothing follows: a drawing-only node, never saved and never a success.
  const nowhere: FlowNode[] = [];
  for (const node of graph.nodes) {
    for (const outcome of node.outcomes ?? []) {
      if (wires.some((wire) => wire.kind === 'when' && wire.from === node.id && wire.outcome === outcome)) continue;
      const id = `${node.id}:none:${outcome}`;
      nowhere.push({ id, title: t('workflow.graph.noFurther'), glyph: 'stepNoFurther', faint: true });
      edges.push({
        id: `${node.id}>${id}`, from: rep(node.id), to: id, style: 'conditional', label: outcome, says: t('workflow.graph.noFurtherSays', { outcome }),
      });
    }
  }

  // Finished is the record's: drawn after the steps nothing follows, those the run reached where it says which.
  const finished: FlowNode[] = [];
  if (options.finished) {
    const sinks = marked.filter((node) => !edges.some((edge) => edge.from === node.id && edge.style !== 'back'));
    const reached = sinks.filter((node) => node.reached !== false);
    for (const node of reached.length > 0 ? reached : sinks) {
      edges.push({ id: `${node.id}>${FINISHED}`, from: node.id, to: FINISHED, says: t('workflow.flow.move.automatic') });
    }
    finished.push({ id: FINISHED, title: t('workflow.finished'), glyph: 'stepFinished', state: options.finished, reached: true, settled: true });
  }

  // The sections: the landing divides before from after; a graph with none is one section of steps.
  const all = [...marked, ...nowhere, ...finished];
  const ids = new Set(all.map((node) => node.id));
  const entries = [...new Set(graph.entries.map(rep))].filter((id) => ids.has(id));
  const first = entries[0] ?? marked[0]?.id;
  const landing = graph.nodes.find((node) => LANDING_KINDS.includes(node.kind));
  const afterIt = landing ? wires.find((wire) => wire.from === landing.id && goesOn(wire)) : undefined;
  const sections: FlowSection[] = first === undefined ? [] : landing
    ? [{ title: t('workflow.section.before'), from: first }, ...(afterIt ? [{ title: t('workflow.section.after'), from: rep(afterIt.to) }] : [])]
    : [{ title: t('workflow.section.steps'), from: first }];

  return {
    label: options.label, nodes: all, edges, entries: entries.length > 0 ? entries : first ? [first] : [], sections, track: WORKFLOW_TRACK,
  };
}
