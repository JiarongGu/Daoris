/**
 * **A flowchart's layout** (FLOWCHART1a1; D186 point 2, the flowchart design §3.1, §3.2 and §3.5): a pure, deterministic
 * function of a graph's shape that says which edges return, each node's rank, the waypoint slots an edge takes through
 * the ranks it skips, each rank's order, and what each placement needs. It holds no positions: `Flow` places the slots
 * with CSS and draws the edges from the boxes they make.
 *
 * @remarks
 * **The steps the Map's layers take** (`map/layers.ts`), read for a flow:
 * 1. *Back edges*: a depth-first walk from the entries, in declared order, marks each edge that reaches a node still on
 *    the walk; nodes no entry reaches are walked after them. A back edge is ranked as if reversed and drawn as a return.
 * 2. *Ranks*: each node's rank is the longest path to it over the forward edges.
 * 3. *Waypoints*: a forward edge that skips ranks takes a slot in every rank it crosses, so it threads between nodes.
 * 4. *Order*: each rank starts in declared order (its nodes, then its waypoints in their edges' order), and four
 *    barycentre sweeps (down, up, down, up) order it by its neighbours' places, a tie keeping the earlier order. A
 *    heuristic: it lowers crossings and guarantees no minimum, so the crossing table holds a ceiling per fixture.
 * 5. *Same graph, same picture*: only the shape is read (ids, the edges' ends and styles, the entries), never what is
 *    lit, a path or a time, so a change of state moves no node; a graph that grows is laid out anew, and its nodes may move.
 *
 * **Parallel edges** (§3.4): edges of one style between the same two nodes are one path that carries each of them, since
 * a cause lives in words and two like inks add width and no meaning; edges of different styles are separate paths.
 *
 * **Returns** (§3.5): each back path runs on a lane of its own outside every node, never shared. A return spanning fewer
 * ranks takes a lane nearer the drawing than a longer one, so their runs nest rather than cross.
 */

/**
 * How an edge is drawn: a move, a failure's or a rejection's short dashes, a refused move (a conflict), or a conditional one
 * (FLOWCHART1b): a move made only on a guard, or one that has not happened, such as going on or where Accept would land. Dashed,
 * like the failures; the cause is in words, never the ink.
 */
export type EdgeStyle = 'next' | 'failed' | 'rejected' | 'refused' | 'conditional';

/**
 * What the layout reads of a flow: its nodes in declared order, its edges, and where it is entered. A node's `stack` is what makes
 * it like another for a stack (FLOWCHART1c): a try's code, a kind and its state. Absent, the node never stacks.
 */
export type FlowShape = {
  nodes: readonly { id: string; stack?: string }[];
  edges: readonly { id: string; from: string; to: string; style?: EdgeStyle }[];
  entries: readonly string[];
};

/**
 * A stack (FLOWCHART1c; the flowchart design §3.5, §3.6 step 1): three or more like nodes, each following the one before in a
 * chain, which a flow draws as one fold until it is opened. Its edges are named apart: those into its first member and out of its
 * last cross its boundary and are drawn to the fold; those between its members are said in its description.
 */
export type Stack = {
  /** The fold's id, from its first member's, so it is the same while the shape is. */
  id: string;
  /** What its members share. */
  key: string;
  /** Its members, in the chain's order. */
  members: string[];
  into: string[];
  out: string[];
  within: string[];
};

/** Fewer than three like nodes stay apart, so a single retry stays visible (§3.6). */
const STACK_LEAST = 3;

/** One place in a rank: a node, or a waypoint slot one path takes through the rank. */
export type Slot = { kind: 'node'; id: string } | { kind: 'way'; path: string; key: string };

/** One drawn path: an edge, or several of one style between the same two nodes. */
export type FlowPath = {
  id: string;
  from: string;
  to: string;
  style: EdgeStyle;
  /** The edges it draws, in declared order. */
  edges: string[];
  /** A return (§3.5), drawn on a lane of its own. */
  back: boolean;
  /** Its waypoint slots' keys, rank by rank from its source: a forward path's alone. */
  ways: string[];
  /** A return's lane, 0 nearest the drawing; null for a forward path. */
  lane: number | null;
};

export type FlowLayout = {
  /** Each rank's slots in order. */
  ranks: Slot[][];
  /** Each node's rank. */
  rank: Record<string, number>;
  /** The nodes in reading order: rank by rank, each rank in its order. */
  order: string[];
  paths: FlowPath[];
  /** How many lanes the returns take, one each. */
  lanes: number;
};

/** The placements (§3.2): left to right, top to bottom, or the rail. */
export type Placement = 'across' | 'down' | 'rail';

/**
 * The tracks at their floors, in rem (§3.2): a node track at least 10rem, a waypoint 1rem, 1.5rem between tracks, a
 * return's lane 1rem with 0.5rem beside it; ranks 2.5rem apart top to bottom, 3rem left to right, room for a label line.
 */
export const FLOW_REM = { node: 10, way: 1, gap: 1.5, lane: 1, laneGap: 0.5, rankDown: 2.5, rankAcross: 3 } as const;

/** Left to right holds no rank of more than three nodes: a column of four reads as a list, not a step. */
const ACROSS_MOST = 3;
const SWEEPS = 4;

/** The key a path is known by: its two ends and its style. */
const pathKey = (from: string, to: string, style: EdgeStyle) => `${from}>${to}:${style}`;

/** Lay a flow out. Deterministic: the same shape gives the same layout, whatever else its nodes and edges carry. */
export function layoutFlow(shape: FlowShape): FlowLayout {
  const ids = shape.nodes.map((node) => node.id);
  const known = new Set(ids);
  const edges = shape.edges.filter((edge) => known.has(edge.from) && known.has(edge.to));

  // The paths: one per two ends and a style, in the order of their first edge.
  const paths: FlowPath[] = [];
  const byKey = new Map<string, FlowPath>();
  for (const edge of edges) {
    const style = edge.style ?? 'next';
    const key = pathKey(edge.from, edge.to, style);
    const found = byKey.get(key);
    if (found) { found.edges.push(edge.id); continue; }
    const path: FlowPath = { id: key, from: edge.from, to: edge.to, style, edges: [edge.id], back: false, ways: [], lane: null };
    byKey.set(key, path);
    paths.push(path);
  }

  // 1. Back edges: a depth-first walk from the entries, then from whatever no entry reached, each in declared order.
  const out = new Map<string, FlowPath[]>(ids.map((id) => [id, []]));
  for (const path of paths) out.get(path.from)!.push(path);
  const state = new Map<string, 'on' | 'done'>();
  const walk = (id: string) => {
    state.set(id, 'on');
    for (const path of out.get(id)!) {
      const there = state.get(path.to);
      if (there === 'on') path.back = true;
      else if (there === undefined) walk(path.to);
    }
    state.set(id, 'done');
  };
  for (const id of [...shape.entries.filter((entry) => known.has(entry)), ...ids]) if (!state.has(id)) walk(id);
  const forward = paths.filter((path) => !path.back);

  // 2. Ranks: the longest path over the forward edges, from whatever nothing forward reaches.
  const rank: Record<string, number> = Object.fromEntries(ids.map((id) => [id, 0]));
  const incoming = new Map<string, number>(ids.map((id) => [id, 0]));
  for (const path of forward) incoming.set(path.to, incoming.get(path.to)! + 1);
  const ready = ids.filter((id) => incoming.get(id) === 0);
  while (ready.length > 0) {
    const id = ready.shift()!;
    for (const path of out.get(id)!) {
      if (path.back) continue;
      rank[path.to] = Math.max(rank[path.to]!, rank[id]! + 1);
      incoming.set(path.to, incoming.get(path.to)! - 1);
      if (incoming.get(path.to) === 0) ready.push(path.to);
    }
  }
  const count = ids.length === 0 ? 0 : Math.max(...ids.map((id) => rank[id]!)) + 1;
  const ranks: Slot[][] = Array.from({ length: count }, () => []);
  for (const id of ids) ranks[rank[id]!]!.push({ kind: 'node', id });

  // 3. Waypoints: a slot in every rank a forward path skips, chained to its neighbours so the sweeps order it.
  const near = new Map<string, Set<string>>(ids.map((id) => [id, new Set()]));
  const slotKey = (slot: Slot) => (slot.kind === 'node' ? slot.id : slot.key);
  const link = (a: string, b: string) => { near.get(a)!.add(b); near.get(b)!.add(a); };
  for (const path of forward) {
    const chain = [path.from];
    for (let r = rank[path.from]! + 1; r < rank[path.to]!; r += 1) {
      const key = `${path.id}@${r}`;
      ranks[r]!.push({ kind: 'way', path: path.id, key });
      near.set(key, new Set());
      path.ways.push(key);
      chain.push(key);
    }
    chain.push(path.to);
    for (let index = 1; index < chain.length; index += 1) link(chain[index - 1]!, chain[index]!);
  }

  // 4. Order: four barycentre sweeps, each slot toward its neighbours' places in the rank it is compared with.
  for (let sweep = 0; sweep < SWEEPS; sweep += 1) {
    const down = sweep % 2 === 0;
    for (let step = 1; step < count; step += 1) {
      const r = down ? step : count - 1 - step;
      const beside = ranks[down ? r - 1 : r + 1]!;
      const place = new Map(beside.map((slot, index) => [slotKey(slot), index]));
      const weighed = ranks[r]!.map((slot, index) => {
        const hits = [...near.get(slotKey(slot))!].filter((other) => place.has(other)).map((other) => place.get(other)!);
        return { slot, weight: hits.length > 0 ? hits.reduce((sum, value) => sum + value, 0) / hits.length : index };
      });
      // Array.prototype.sort is stable, so a tie keeps the earlier order.
      ranks[r] = weighed.sort((a, b) => a.weight - b.weight).map(({ slot }) => slot);
    }
  }

  // The returns' lanes: the shortest span nearest the drawing, a tie in declared order; none shared.
  const back = paths.filter((path) => path.back);
  const span = (path: FlowPath) => Math.abs(rank[path.from]! - rank[path.to]!);
  [...back].sort((a, b) => span(a) - span(b)).forEach((path, lane) => { path.lane = lane; });

  const order = ranks.flatMap((slots) => slots.flatMap((slot) => (slot.kind === 'node' ? [slot.id] : [])));
  return { ranks, rank, order, paths, lanes: back.length };
}

/**
 * The stacks in a flow (FLOWCHART1c; the flowchart design §3.6 step 1, D191): each run of three or more like nodes, every one a
 * link of a chain (at most one edge in and one out, never to itself), each following the one before. Two like nodes stay apart.
 * Pure, and reads only the shape: a node's `stack`, its edges' ends and the declared order, so the same graph stacks the same way.
 * A stack applies at any size, where §3.6's other folds wait for 24 nodes: an instance repeats itself (a try, an advance), and a
 * repeat said once is what a person reads.
 */
export function findStacks(shape: FlowShape): Stack[] {
  const known = new Set(shape.nodes.map((node) => node.id));
  const key = new Map(shape.nodes.flatMap((node) => (node.stack === undefined ? [] : [[node.id, node.stack] as const])));
  const into = new Map<string, string[]>();
  const out = new Map<string, { id: string; to: string }[]>();
  for (const edge of shape.edges) {
    if (!known.has(edge.from) || !known.has(edge.to)) continue;
    into.set(edge.to, [...(into.get(edge.to) ?? []), edge.id]);
    out.set(edge.from, [...(out.get(edge.from) ?? []), { id: edge.id, to: edge.to }]);
  }
  const selfLooped = new Set(shape.edges.filter((edge) => edge.from === edge.to).map((edge) => edge.from));
  const link = (id: string) => key.has(id) && !selfLooped.has(id) && (into.get(id)?.length ?? 0) <= 1 && (out.get(id)?.length ?? 0) <= 1;
  const next = (id: string) => out.get(id)?.[0]?.to;
  const previous = new Map<string, string>();
  for (const [from, edges] of out) for (const edge of edges) previous.set(edge.to, from);

  const stacks: Stack[] = [];
  for (const { id } of shape.nodes) {
    if (!link(id)) continue;
    // A run starts where the node before it is not a like link: a loop of like nodes has no start, and stacks nothing.
    const before = previous.get(id);
    if (before !== undefined && link(before) && key.get(before) === key.get(id) && next(before) === id) continue;
    const members = [id];
    for (let at = next(id); at !== undefined && link(at) && key.get(at) === key.get(id) && !members.includes(at); at = next(at)) members.push(at);
    if (members.length < STACK_LEAST) continue;
    const last = members[members.length - 1]!;
    stacks.push({
      id: `stack:${id}`,
      key: key.get(id)!,
      members,
      into: into.get(id) ?? [],
      out: (out.get(last) ?? []).map((edge) => edge.id),
      within: members.slice(0, -1).map((member) => out.get(member)![0]!.id),
    });
  }
  return stacks;
}

/**
 * What each placement needs of the pane, in px at `rem` px a rem (§3.2): left to right, every rank side by side with the
 * rank gaps between; top to bottom, the widest rank's node tracks, waypoints and gaps, and a lane for each return. `least`
 * is the node track's floor in rem: §3.2's 10rem, or a caller's wider one (FLOWCHART1d: a workflow's step, which holds its
 * settings and its run's facts, is 18rem).
 */
export function needs(layout: FlowLayout, rem = 16, least: number = FLOW_REM.node): { across: number; down: number } {
  const ranks = layout.ranks.length;
  const across = ranks === 0 ? 0 : (ranks * least + (ranks - 1) * FLOW_REM.rankAcross) * rem;
  const widest = Math.max(0, ...layout.ranks.map((slots) => {
    const nodes = slots.filter((slot) => slot.kind === 'node').length;
    const ways = slots.length - nodes;
    return nodes * least + ways * FLOW_REM.way + Math.max(0, slots.length - 1) * FLOW_REM.gap;
  }));
  const down = (widest + layout.lanes * (FLOW_REM.lane + FLOW_REM.laneGap)) * rem;
  return { across, down };
}

/**
 * What a caller bounds the placement by (FLOWCHART1d): its node track's floor in rem, and whether left to right is one of its
 * placements. A flow in sections is read rank by rank under their titles, so it is never left to right (the workflow's
 * *Before it lands* and *After it lands*, the flowchart design §2.4 and §5.1).
 */
export type PlacementBounds = { least?: number; across?: boolean };

/**
 * Where the flow is placed at a measured inner width (§3.2): left to right where every rank fits side by side and none
 * holds more than three nodes, and the caller allows it; else top to bottom where the widest rank and the lanes fit; else,
 * wherever nothing measured the pane, and wherever the person chose the list, the rail.
 */
export function placementFor(
  layout: FlowLayout, width: number | undefined, asList = false, rem = 16, { least = FLOW_REM.node, across = true }: PlacementBounds = {},
): Placement {
  if (asList || width === undefined || layout.ranks.length === 0) return 'rail';
  const need = needs(layout, rem, least);
  const most = Math.max(0, ...layout.ranks.map((slots) => slots.filter((slot) => slot.kind === 'node').length));
  if (across && need.across <= width && most <= ACROSS_MOST) return 'across';
  if (need.down <= width) return 'down';
  return 'rail';
}

/**
 * The crossings between the ranks' slots: for each two neighbouring ranks, the forward segments between them whose ends
 * are in opposite orders. The first half of the crossing table (trap 3): the drawn paths are counted in a rendered
 * browser, since a unit test's DOM lays nothing out.
 */
export function slotCrossings(layout: FlowLayout): number {
  const place = new Map<string, number>();
  for (const slots of layout.ranks) slots.forEach((slot, index) => place.set(slot.kind === 'node' ? slot.id : slot.key, index));
  const between: [number, number][][] = layout.ranks.map(() => []);
  for (const path of layout.paths) {
    if (path.back) continue;
    const chain = [path.from, ...path.ways, path.to];
    for (let index = 1; index < chain.length; index += 1) {
      between[layout.rank[path.from]! + index - 1]!.push([place.get(chain[index - 1]!)!, place.get(chain[index]!)!]);
    }
  }
  let crossings = 0;
  for (const segments of between) {
    for (let a = 0; a < segments.length; a += 1) {
      for (let b = a + 1; b < segments.length; b += 1) {
        const [[a0, a1], [b0, b1]] = [segments[a]!, segments[b]!];
        if ((a0 - b0) * (a1 - b1) < 0) crossings += 1;
      }
    }
  }
  return crossings;
}
