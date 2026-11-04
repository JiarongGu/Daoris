import { type CSSProperties } from 'react';
import { cn } from '../lib/cn';
import { placeChips } from './canvas/marks';
import { type WorldRead } from './canvas/worldBoxes';
import { type Fold } from './flowFolds';
import { type Box, type InkLabel, placeInserts, placeLabels, routeInk, RUN } from './flowInk';
import { type EdgeStyle, FLOW_REM, type FlowLayout, type Placement, type Stack } from './flowLayout';
import { type FlowMark, type FlowNode, type FlowEdge, type Ink } from './flowTypes';

export const rem = (value: number) => `${value}rem`;

/** How each kind of mark's chip is drawn: the border's style as well as its word, so no kind is told by hue alone (D223 §4.3). */
export const CHIP: Record<FlowMark['kind'], string> = {
  refused: 'border-st-declined text-ink-danger',
  new: 'border-accent text-ink',
  remove: 'border-dashed border-line-strong text-ink-soft',
  changed: 'border-dotted border-accent text-ink',
};

/** A mark's chip: its word, hidden from a reader (the node's description says it) and taking no part in any layout. */
export function MarkChip({ mark, data, className, style }: {
  mark: FlowMark; data?: Record<`data-${string}`, string | undefined>; className?: string; style?: CSSProperties;
}) {
  return (
    <span
      aria-hidden
      data-flow-mark-kind={mark.kind}
      {...data}
      className={cn('whitespace-nowrap rounded-control border bg-raised px-1.5 text-meta leading-tight', CHIP[mark.kind], className)}
      style={style}
    >
      {mark.kind === 'refused' ? '⚠ ' : ''}{mark.word}
    </span>
  );
}

/** The rail's mark beside a node: whole where it is current, dashed where it is not reached, and the line to the next. */
export function RailMark({ node, last }: { node: FlowNode; last: boolean }) {
  return (
    <div aria-hidden className="flex flex-col items-center pt-3">
      <span
        className={cn(
          'block size-2.5 shrink-0 rounded-full border',
          node.current ? 'border-ink-soft bg-ink-soft' : node.reached === false || node.faint ? 'border-dashed border-line-strong' : 'border-ink-soft',
        )}
      />
      {!last && <span className="mt-1 w-px flex-1 bg-ink-faint" />}
    </div>
  );
}

export type Translate = (key: string, values?: Record<string, unknown>) => string;

/** What a stack adds to a node's description (§3.6): what a fold holds, before; the edges between its members, after. */
export type StackNote = { before: string[]; after: string[] };

/**
 * A node's description (§3.7): its state, who acts, whether it was reached, and every edge in and out, in words; a fold's says what
 * it holds and the edges between its members, so no cause is lost to it (§3.6).
 */
export function describe(
  t: Translate, node: FlowNode, edges: FlowEdge[], byId: Map<string, FlowNode>, backs: Set<string>, note?: StackNote,
): string {
  const title = (id: string) => byId.get(id)!.title;
  const sentences: string[] = [...(note?.before ?? [])];
  if (node.state) sentences.push(t('flow.state', { word: node.state.word }));
  if (node.actor) sentences.push(t('flow.role', { role: node.actor }));
  if (node.mark) sentences.push(node.mark.foot ? `${node.mark.word}. ${node.mark.foot}` : node.mark.word);
  if (node.reached === false) sentences.push(t('flow.notReached'));
  for (const edge of edges) if (edge.to === node.id && byId.has(edge.from)) sentences.push(t('flow.from', { node: title(edge.from), says: edge.says }));
  const out = edges.filter((edge) => edge.from === node.id && byId.has(edge.to));
  const next = out.filter((edge) => !backs.has(edge.id)).map((edge) => t('flow.move', { node: title(edge.to), says: edge.says }));
  if (next.length > 0) sentences.push(t('flow.next', { moves: next.reduce((first, second) => t('flow.moves', { first, second })) }));
  for (const edge of out) if (backs.has(edge.id)) sentences.push(t('flow.back', { node: title(edge.to), says: edge.says }));
  for (const edge of out) if (edge.note) sentences.push(edge.note);
  sentences.push(...(note?.after ?? []));
  return sentences.length === 0 ? '' : sentences.reduce((first, second) => t('flow.join', { first, second }));
}

export const STACK_PRESS = 'cursor-pointer border-0 bg-transparent p-0 text-left text-meta text-ink-soft underline-offset-2 hover:text-ink hover:underline';

/**
 * The flow as drawn with its stacks (FLOWCHART1c; the flowchart design §3.6): each stack not opened is one node in its first
 * member's place, titled by the members' shared word and counted, with their first and last time and its press; every edge across
 * its boundary drawn to it with its cause, and those between its members said in its description. An opened stack is its members
 * whole, the first carrying the press that folds them again, which Enter does where the member has no door of its own.
 */
export function foldStacks(
  t: Translate, nodes: FlowNode[], edges: FlowEdge[], entries: string[], stacks: Stack[], opened: ReadonlySet<string>,
  toggle: (stack: Stack, open: boolean) => void,
): Folded {
  const notes = new Map<string, StackNote>();
  const held = new Map<string, number>();
  if (stacks.length === 0) return { nodes, edges, entries, notes, foldOf: new Map(), held };
  const byId = new Map(nodes.map((node) => [node.id, node]));
  const foldOf = new Map<string, string>();
  const replaced = new Map<string, FlowNode>();
  for (const stack of stacks) {
    const members = stack.members.map((id) => byId.get(id)!);
    const first = members[0]!;
    const last = members[members.length - 1]!;
    const count = members.length;
    if (opened.has(stack.id)) {
      const close = (
        <button type="button" tabIndex={-1} aria-expanded onClick={() => toggle(stack, false)} className={STACK_PRESS}>
          {t('flow.stack.close', { count })}
        </button>
      );
      replaced.set(first.id, {
        ...first,
        lines: <div className="grid min-w-0 justify-items-start gap-0.5">{first.lines}{close}</div>,
        open: first.open ?? (() => toggle(stack, false)),
      });
      notes.set(first.id, { before: [], after: [t(first.open ? 'flow.stack.memberPress' : 'flow.stack.member', { count })] });
      continue;
    }
    for (const id of stack.members) foldOf.set(id, stack.id);
    held.set(stack.id, count);
    const word = first.stack!.word;
    const reached = members.every((member) => member.reached === false) ? false : members.every((member) => member.reached === true) ? true : undefined;
    const firstAt = first.stack?.at;
    replaced.set(first.id, {
      id: stack.id,
      title: t('flow.stack.title', { word, count }),
      ...(first.glyph ? { glyph: first.glyph } : {}),
      ...(first.state && members.every((member) => member.state?.word === first.state!.word) ? { state: first.state } : {}),
      ...(reached === undefined ? {} : { reached }),
      ...(members.every((member) => member.faint) ? { faint: true } : {}),
      ...(members.every((member) => member.settled) ? { settled: true } : {}),
      current: members.some((member) => member.current),
      open: () => toggle(stack, true),
      lines: (
        <div className="grid min-w-0 justify-items-start gap-0.5">
          {firstAt && <p className="m-0 min-w-0 [overflow-wrap:anywhere]">{t('flow.stack.when', { first: firstAt, last: last.stack?.at ?? firstAt })}</p>}
          <button type="button" tabIndex={-1} aria-expanded={false} onClick={() => toggle(stack, true)} className={STACK_PRESS}>
            {t('flow.stack.open', { count })}
          </button>
        </div>
      ),
    });
    // The edges between its members, merged by cause and counted, so none is lost to the fold.
    const causes = new Map<string, number>();
    for (const edge of edges) if (stack.within.includes(edge.id)) causes.set(edge.says, (causes.get(edge.says) ?? 0) + 1);
    const between = [...causes].map(([says, times]) => (times > 1 ? t('flow.stack.times', { says, count: times }) : says));
    notes.set(stack.id, {
      before: [t('flow.stack.holds', { count, word })],
      after: between.length === 0 ? [] : [t('flow.stack.between', { says: between.reduce((one, two) => t('flow.moves', { first: one, second: two })) })],
    });
  }
  return {
    nodes: nodes.flatMap((node) => replaced.get(node.id) ?? (foldOf.has(node.id) ? [] : [node])),
    edges: edges.flatMap((edge) => {
      const from = foldOf.get(edge.from) ?? edge.from;
      const to = foldOf.get(edge.to) ?? edge.to;
      if (foldOf.has(edge.from) && from === to) return [];
      return from === edge.from && to === edge.to ? [edge] : [{ ...edge, from, to }];
    }),
    entries: [...new Set(entries.map((id) => foldOf.get(id) ?? id))],
    notes,
    foldOf,
    held,
  };
}

/**
 * A flow as drawn with its folds: its nodes, edges and entries, what each fold adds to a description, which fold holds each node it
 * folded, and how many nodes given each fold holds.
 */
export type Folded = {
  nodes: FlowNode[];
  edges: FlowEdge[];
  entries: string[];
  notes: Map<string, StackNote>;
  foldOf: Map<string, string>;
  held: Map<string, number>;
};

/**
 * The flow as drawn with its folds past 24 nodes (FLOWCHART1d; the flowchart design §3.6 steps 2-3), over the flow its stacks drew:
 * each fold one node in its first member's place, titled by what it holds and counted (a stack inside it by its members), with its
 * press. Every edge across its boundary is drawn to it, merged by its cause and by the node outside and counted, so no cause is lost
 * and no node says one twice; those between its members are said in its description, merged by cause and counted.
 */
export function foldRanges(t: Translate, flow: Folded, folds: Fold[], open: (fold: Fold) => void): Folded {
  const notes = new Map<string, StackNote>();
  if (folds.length === 0) return { ...flow, notes, foldOf: new Map() };
  const byId = new Map(flow.nodes.map((node) => [node.id, node]));
  const foldOf = new Map(folds.flatMap((fold) => fold.members.map((member) => [member, fold.id] as const)));
  const replaced = new Map<string, FlowNode>();
  const held = new Map(flow.held);
  for (const fold of folds) {
    const members = fold.members.map((id) => byId.get(id)!);
    const count = members.reduce((sum, member) => sum + (flow.held.get(member.id) ?? 1), 0);
    held.set(fold.id, count);
    const reached = members.every((member) => member.reached === false) ? false : members.every((member) => member.reached === true) ? true : undefined;
    replaced.set(fold.members[0]!, {
      id: fold.id,
      title: t(`flow.fold.${fold.kind}`, { count }),
      ...(reached === undefined ? {} : { reached }),
      ...(members.every((member) => member.faint) ? { faint: true } : {}),
      ...(members.every((member) => member.settled) ? { settled: true } : {}),
      open: () => open(fold),
      lines: (
        <div className="grid min-w-0 justify-items-start gap-0.5">
          <button type="button" tabIndex={-1} aria-expanded={false} onClick={() => open(fold)} className={STACK_PRESS}>
            {t('flow.stack.open', { count })}
          </button>
        </div>
      ),
    });
    const causes = new Map<string, number>();
    for (const edge of flow.edges) if (fold.within.includes(edge.id)) causes.set(edge.says, (causes.get(edge.says) ?? 0) + 1);
    const between = [...causes].map(([says, times]) => (times > 1 ? t('flow.stack.times', { says, count: times }) : says));
    notes.set(fold.id, {
      before: [t('flow.fold.holds', { count })],
      after: between.length === 0 ? [] : [t('flow.stack.between', { says: between.reduce((one, two) => t('flow.moves', { first: one, second: two })) })],
    });
  }

  // Each edge across a boundary drawn to its fold; those of one cause between one node outside and the fold are one, counted.
  const edges: FlowEdge[] = [];
  const merged = new Map<string, { edge: FlowEdge; count: number }>();
  for (const edge of flow.edges) {
    const from = foldOf.get(edge.from) ?? edge.from;
    const to = foldOf.get(edge.to) ?? edge.to;
    if (foldOf.has(edge.from) && from === to) continue;
    if (from === edge.from && to === edge.to) { edges.push(edge); continue; }
    const key = JSON.stringify([from, to, edge.style ?? 'next', edge.says]);
    const found = merged.get(key);
    if (found) {
      found.count += 1;
      if (edge.taken) found.edge.taken = true;
      continue;
    }
    const drawn: FlowEdge = { ...edge, from, to };
    merged.set(key, { edge: drawn, count: 1 });
    edges.push(drawn);
  }
  for (const { edge, count } of merged.values()) if (count > 1) edge.says = t('flow.stack.times', { says: edge.says, count });

  return {
    nodes: flow.nodes.flatMap((node) => replaced.get(node.id) ?? (foldOf.has(node.id) ? [] : [node])),
    edges,
    entries: [...new Set(flow.entries.map((id) => foldOf.get(id) ?? id))],
    notes,
    foldOf,
    held,
  };
}

/** The room a drawn placement keeps outside its nodes for the returns: their lanes, and their runs past the end ranks. */
export function padding(layout: FlowLayout, placement: Placement, dataLanes: number): CSSProperties {
  const run = `${RUN.pad}px`;
  // The data wires' lanes stand right of the nodes, kept free whether or not one is drawn (FLOWCHART2a).
  const right = dataLanes > 0 && placement === 'down' ? { paddingRight: rem(dataLanes * (FLOW_REM.lane + FLOW_REM.laneGap) + 1) } : {};
  if (layout.lanes === 0) return dataLanes > 0 && placement === 'down' ? { paddingTop: run, paddingBottom: run, ...right } : {};
  const lanes = rem(layout.lanes * (FLOW_REM.lane + FLOW_REM.laneGap));
  return placement === 'down'
    ? { paddingLeft: lanes, paddingTop: run, paddingBottom: run, ...right }
    : { paddingBottom: `calc(${lanes} + 0.5rem)`, paddingLeft: run, paddingRight: run };
}

/** How each style's path is dashed: a move and a decision's route are solid, a data wire dotted, the rest dashed. */
export const DASHES: Record<EdgeStyle, string | undefined> = {
  next: undefined, when: undefined, data: '1.5 3', failed: '4 3', rejected: '4 3', refused: '4 3', conditional: '4 3', back: '4 3',
};

/** A node track's most, in rem (§3.2): a caller's node that holds more may take more (FLOWCHART1d). */
export const TRACK_MOST = 16;

/** One node track's width for the drawn placements: as wide as the binding rank leaves each node, 10rem to 16rem, or the caller's. */
export function trackFor(
  layout: FlowLayout, placement: Placement, width: number | undefined, least: number, mostRem: number, remPx: number,
): number | undefined {
  if (placement === 'rail' || width === undefined) return undefined;
  const px = (value: number) => value * remPx;
  const floor = px(least);
  const most = px(mostRem);
  if (placement === 'across') {
    const ranks = layout.ranks.length;
    const room = width - (ranks - 1) * px(FLOW_REM.rankAcross) - (layout.lanes > 0 ? 2 * RUN.pad : 0);
    return Math.max(floor, Math.min(most, Math.floor(room / Math.max(1, ranks))));
  }
  const room = width - layout.lanes * px(FLOW_REM.lane + FLOW_REM.laneGap);
  const each = layout.ranks.map((slots) => {
    const nodes = slots.filter((slot) => slot.kind === 'node').length;
    const rest = (slots.length - nodes) * px(FLOW_REM.way) + Math.max(0, slots.length - 1) * px(FLOW_REM.gap);
    return nodes === 0 ? most : (room - rest) / nodes;
  });
  return Math.max(floor, Math.min(most, Math.floor(Math.min(...each))));
}

/**
 * The drawn geometry from the world the page laid out (`readWorld`): every box in world units, so the ink is the same at every scale the
 * canvas draws it at (trap 1). A label's and a ＋'s size is its layout's, which a transform leaves alone.
 */
export function measure(
  element: HTMLElement, world: WorldRead, layout: FlowLayout, placement: 'across' | 'down', data: readonly { id: string; from: string; to: string }[],
  onPaths: ReadonlyMap<string, string> = new Map(),
): Omit<Ink, 'drawn'> {
  const boxes = new Map<string, Box>([...world.nodes, ...world.ways]);
  const paths = routeInk(layout, placement, boxes, { ports: world.ports, data });
  const sizes = new Map<string, { w: number; h: number }>();
  element.querySelectorAll<HTMLElement>('[data-flow-label]').forEach((label) => {
    sizes.set(label.dataset.flowLabel!, { w: label.offsetWidth, h: label.offsetHeight });
  });
  // A label clears every node and every section's divider (FLOWCHART1d), which stands in a rank gap as a label does.
  const nodes = [...layout.order.map((id) => boxes.get(id)!), ...world.sections];
  const labels = placeLabels(paths, sizes, nodes);
  // An editor's ＋ on a way clears every node, every label shown and every ＋ placed before it (FLOWCHART2b).
  const insertSizes = new Map<string, { w: number; h: number }>();
  element.querySelectorAll<HTMLElement>('[data-flow-insert]').forEach((insert) => {
    const path = onPaths.get(insert.dataset.flowInsert!);
    if (path) insertSizes.set(path, { w: insert.offsetWidth, h: insert.offsetHeight });
  });
  const inserts = placeInserts(paths, insertSizes, nodes, labels);
  // A mark's chip clears every node, divider, label and ＋ shown, and every point of ink (CANVAS1b).
  const asks = [...element.querySelectorAll<HTMLElement>('[data-flow-chip]')].flatMap((chip) => {
    const node = boxes.get(chip.dataset.chipNode!);
    return node ? [{ id: chip.dataset.flowChip!, node, w: chip.offsetWidth, h: chip.offsetHeight }] : [];
  });
  const shown = [...labels, ...inserts].filter((each) => each.shown && each.w > 0.5 && each.h > 0.5)
    .map((each) => ({ x: each.x - each.w / 2, y: each.y - each.h / 2, w: each.w, h: each.h }));
  const chips = placeChips(asks, [...nodes, ...shown], paths.flatMap((path) => path.points));
  return { paths, labels, inserts, chips, width: world.width, height: world.height };
}

export const sameBoxes = (a: readonly InkLabel[], b: readonly InkLabel[]) => a.length === b.length
  && a.every((label, index) => {
    const other = b[index]!;
    return label.x === other.x && label.y === other.y && label.shown === other.shown && label.w === other.w;
  });

export function same(a: Omit<Ink, 'drawn'>, b: Omit<Ink, 'drawn'>): boolean {
  return a.width === b.width && a.height === b.height
    && a.paths.length === b.paths.length && a.paths.every((path, index) => path.d === b.paths[index]!.d)
    && sameBoxes(a.labels, b.labels) && sameBoxes(a.inserts, b.inserts)
    && a.chips.length === b.chips.length && a.chips.every((chip, index) => {
      const other = b.chips[index]!;
      return chip.id === other.id && chip.x === other.x && chip.y === other.y && chip.shown === other.shown;
    });
}
