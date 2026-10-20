import type { EdgeStyle, FlowLayout } from './flowLayout';

/**
 * **A flowchart's edges, drawn from the boxes the page measured** (FLOWCHART1a1; the flowchart design §3.4, §3.5 and §4.2):
 * the nodes and waypoint slots are placed by CSS, and this routes each path through them and places each label, from their
 * boxes alone. Nothing here changes a box, so nothing measured can loop back into the layout.
 *
 * @remarks
 * Written along the flow's own axes, so one routing serves both drawn placements: the **main** axis runs from rank to rank
 * (down the page top to bottom, across it left to right), the **cross** axis along a rank.
 * - **A forward path** leaves its source's far side, crosses each rank gap as a curve that leaves and arrives along the
 *   main axis (the Map's `link`), and runs straight through each waypoint slot it was given, so it never enters a node.
 * - **A return** (§3.5) leaves its source's far side into the gap after its rank, runs along that gap to a lane of its own
 *   outside every node (left of the drawing top to bottom, below it left to right), along the lane, and back along the
 *   gap before its target's rank into its near side. Each run in a gap is set apart from the others by its lane, so two
 *   returns never share a line, and the lanes nest by span (`layoutFlow`).
 * - **Ports** spread the paths that meet one side of a node, ordered by where each comes from or goes, the returns on the
 *   lanes' side, so a node's own paths do not cross at its edge.
 * - **A label** sits at its path's middle in a rank gap, on the page's colour, and only where its box clears every node,
 *   every label kept before it and every other path; a dropped label loses nothing, since the foot says the whole cause.
 *
 * Proved in a rendered browser by the measured-box check (trap 3), never by a unit test's DOM, which lays nothing out.
 */

export type Box = { x: number; y: number; w: number; h: number };
export type Point = { x: number; y: number };

/** A drawn path: its SVG geometry, the points it passes sampled along its length, and where its label would sit. */
export type InkPath = {
  id: string;
  from: string;
  to: string;
  style: EdgeStyle;
  back: boolean;
  d: string;
  points: Point[];
  mid: Point | null;
};

/** A label kept or dropped, and where its box is: its centre and size. */
export type InkLabel = { id: string; x: number; y: number; w: number; h: number; shown: boolean };

/** The room a return's runs take, in px: from a gap's edge, between two runs, a corner's radius; and the ports' spacing. */
export const RUN = { edge: 5, step: 5, corner: 4, port: 14, lane: 24, laneInset: 16, pad: 24 } as const;

type Segment = { kind: 'L'; to: Point } | { kind: 'C'; c1: Point; c2: Point; to: Point } | { kind: 'Q'; c: Point; to: Point };

const round = (value: number) => Math.round(value * 2) / 2;

/** Route every path of a drawn layout through the boxes of its nodes and waypoint slots, by id and slot key. */
export function routeInk(layout: FlowLayout, placement: 'across' | 'down', boxes: ReadonlyMap<string, Box>): InkPath[] {
  const down = placement === 'down';
  const at = (cross: number, main: number): Point => (down ? { x: cross, y: main } : { x: main, y: cross });
  const c0 = (box: Box) => (down ? box.x : box.y);
  const cLen = (box: Box) => (down ? box.w : box.h);
  const m0 = (box: Box) => (down ? box.y : box.x);
  const mLen = (box: Box) => (down ? box.h : box.w);
  const centre = (box: Box) => c0(box) + cLen(box) / 2;
  const box = (key: string) => boxes.get(key)!;

  // Each rank's extent along the main axis, and every slot's extent across.
  const key = (slot: FlowLayout['ranks'][number][number]) => (slot.kind === 'node' ? slot.id : slot.key);
  const span = layout.ranks.map((slots) => ({
    start: Math.min(...slots.map((slot) => m0(box(key(slot))))),
    end: Math.max(...slots.map((slot) => m0(box(key(slot))) + mLen(box(key(slot))))),
  }));
  const all = layout.ranks.flat().map((slot) => box(key(slot)));
  const crossLow = Math.min(...all.map(c0));
  const crossHigh = Math.max(...all.map((each) => c0(each) + cLen(each)));
  const after = (rank: number) => ({ start: span[rank]!.end, end: span[rank + 1]?.start ?? span[rank]!.end + RUN.pad });
  const before = (rank: number) => ({ start: span[rank - 1]?.end ?? span[rank]!.start - RUN.pad, end: span[rank]!.start });
  const laneAt = (lane: number) => (down ? crossLow - RUN.laneInset - lane * RUN.lane : crossHigh + RUN.laneInset + lane * RUN.lane);

  const forward = layout.paths.filter((path) => !path.back);
  const back = layout.paths.filter((path) => path.back).sort((a, b) => a.lane! - b.lane!);

  // Where each path meets each node: spread along the side, in the order of what is at each path's other end.
  const nextCross = (path: FlowLayout['paths'][number]) => centre(box(path.ways[0] ?? path.to));
  const lastCross = (path: FlowLayout['paths'][number]) => centre(box(path.ways[path.ways.length - 1] ?? path.from));
  const ports = new Map<string, number>();
  const spread = (node: string, side: 'out' | 'in', forwards: FlowLayout['paths'], returns: FlowLayout['paths']) => {
    const sorted = [...forwards].sort((a, b) => (side === 'out' ? nextCross(a) - nextCross(b) : lastCross(a) - lastCross(b)));
    // The returns on the lanes' side, the innermost lane nearest them, so their runs nest at the node's edge too.
    const inner = [...returns].sort((a, b) => a.lane! - b.lane!);
    const along = down ? [...inner, ...sorted] : [...sorted, ...inner.reverse()];
    const width = cLen(box(node));
    const step = along.length > 1 ? Math.min(RUN.port, (width * 0.7) / (along.length - 1)) : 0;
    along.forEach((path, index) => ports.set(`${side}:${path.id}`, centre(box(node)) + (index - (along.length - 1) / 2) * step));
  };
  for (const node of layout.order) {
    spread(node, 'out', forward.filter((path) => path.from === node), back.filter((path) => path.from === node));
    spread(node, 'in', forward.filter((path) => path.to === node), back.filter((path) => path.to === node));
  }

  const curve = (a: Point, b: Point): Segment => {
    const [am, bm, ac, bc] = down ? [a.y, b.y, a.x, b.x] : [a.x, b.x, a.y, b.y];
    const half = (bm - am) / 2;
    return { kind: 'C', c1: at(ac, am + half), c2: at(bc, bm - half), to: b };
  };

  const drawn: InkPath[] = [];
  for (const path of forward) {
    const source = box(path.from);
    const target = box(path.to);
    let here = at(ports.get(`out:${path.id}`)!, m0(source) + mLen(source));
    const start = here;
    const segments: Segment[] = [];
    const gaps: [Point, Point][] = [];
    for (const way of path.ways) {
      const slot = box(way);
      const top = at(centre(slot), m0(slot));
      segments.push(curve(here, top));
      gaps.push([here, top]);
      here = at(centre(slot), m0(slot) + mLen(slot));
      segments.push({ kind: 'L', to: here });
    }
    const end = at(ports.get(`in:${path.id}`)!, m0(target));
    segments.push(curve(here, end));
    gaps.push([here, end]);
    const [a, b] = gaps[Math.floor(path.ways.length / 2)]!;
    drawn.push(finish(path, start, segments, { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 }));
  }

  // Each gap's runs, set apart by lane: those leaving a rank from its far edge, those entering one from its near edge.
  const outRuns = new Map<number, string[]>();
  const inRuns = new Map<number, string[]>();
  for (const path of back) {
    const from = layout.rank[path.from]!;
    const to = layout.rank[path.to]!;
    outRuns.set(from, [...(outRuns.get(from) ?? []), path.id]);
    inRuns.set(to, [...(inRuns.get(to) ?? []), path.id]);
  }
  for (const path of back) {
    const source = box(path.from);
    const target = box(path.to);
    const from = layout.rank[path.from]!;
    const to = layout.rank[path.to]!;
    const out = after(from).start + RUN.edge + outRuns.get(from)!.indexOf(path.id) * RUN.step;
    const into = before(to).end - RUN.edge - inRuns.get(to)!.indexOf(path.id) * RUN.step;
    const lane = laneAt(path.lane!);
    const leave = ports.get(`out:${path.id}`)!;
    const enter = ports.get(`in:${path.id}`)!;
    const corners = [
      at(leave, m0(source) + mLen(source)), at(leave, out), at(lane, out), at(lane, into), at(enter, into), at(enter, m0(target)),
    ];
    drawn.push(finish(path, corners[0]!, orthogonal(corners), at(lane, (out + into) / 2)));
  }
  return drawn;
}

/** An orthogonal run through its corners, each corner rounded, from the second point on. */
function orthogonal(corners: Point[]): Segment[] {
  const segments: Segment[] = [];
  for (let index = 1; index < corners.length; index += 1) {
    const here = corners[index]!;
    const next = corners[index + 1];
    if (!next) { segments.push({ kind: 'L', to: here }); break; }
    const before = corners[index - 1]!;
    const into = toward(here, before, RUN.corner);
    const out = toward(here, next, RUN.corner);
    segments.push({ kind: 'L', to: into }, { kind: 'Q', c: here, to: out });
  }
  return segments;
}

/** The point `distance` from `from` toward `to`, never past halfway. */
function toward(from: Point, to: Point, distance: number): Point {
  const length = Math.hypot(to.x - from.x, to.y - from.y);
  if (length === 0) return from;
  const step = Math.min(distance, length / 2) / length;
  return { x: from.x + (to.x - from.x) * step, y: from.y + (to.y - from.y) * step };
}

function finish(path: FlowLayout['paths'][number], start: Point, segments: Segment[], mid: Point | null): InkPath {
  let d = `M${round(start.x)} ${round(start.y)}`;
  const points: Point[] = [start];
  let here = start;
  for (const segment of segments) {
    const parts = segment.kind === 'L' ? [segment.to] : segment.kind === 'Q' ? [segment.c, segment.to] : [segment.c1, segment.c2, segment.to];
    d += ` ${segment.kind}${parts.map((point) => `${round(point.x)} ${round(point.y)}`).join(' ')}`;
    const length = Math.hypot(segment.to.x - here.x, segment.to.y - here.y);
    const steps = Math.max(1, Math.ceil(length / 3));
    for (let step = 1; step <= steps; step += 1) points.push(sample(here, segment, step / steps));
    here = segment.to;
  }
  return { id: path.id, from: path.from, to: path.to, style: path.style, back: path.back, d, points, mid };
}

function sample(from: Point, segment: Segment, t: number): Point {
  const u = 1 - t;
  if (segment.kind === 'L') return { x: from.x + (segment.to.x - from.x) * t, y: from.y + (segment.to.y - from.y) * t };
  if (segment.kind === 'Q') {
    return {
      x: u * u * from.x + 2 * u * t * segment.c.x + t * t * segment.to.x,
      y: u * u * from.y + 2 * u * t * segment.c.y + t * t * segment.to.y,
    };
  }
  return {
    x: u ** 3 * from.x + 3 * u * u * t * segment.c1.x + 3 * u * t * t * segment.c2.x + t ** 3 * segment.to.x,
    y: u ** 3 * from.y + 3 * u * u * t * segment.c1.y + 3 * u * t * t * segment.c2.y + t ** 3 * segment.to.y,
  };
}

const overlaps = (a: Box, b: Box, margin = 0) =>
  a.x < b.x + b.w + margin && b.x < a.x + a.w + margin && a.y < b.y + b.h + margin && b.y < a.y + a.h + margin;
const inside = (point: Point, area: Box) =>
  point.x > area.x && point.x < area.x + area.w && point.y > area.y && point.y < area.y + area.h;

/**
 * Keep each label whose box, centred at its path's middle, clears every node, every label kept before it, and every
 * other path; drop the rest. Sizes are the labels' own, measured: a word in 中文 is not an English word's width.
 */
export function placeLabels(paths: readonly InkPath[], sizes: ReadonlyMap<string, { w: number; h: number }>, nodes: readonly Box[]): InkLabel[] {
  const kept: Box[] = [];
  const placed: InkLabel[] = [];
  for (const path of paths) {
    const size = sizes.get(path.id);
    if (!size || !path.mid) continue;
    const area = { x: path.mid.x - size.w / 2, y: path.mid.y - size.h / 2, w: size.w, h: size.h };
    const shown = nodes.every((node) => !overlaps(area, node, 2))
      && kept.every((other) => !overlaps(area, other, 2))
      && paths.every((other) => other.id === path.id || other.points.every((point) => !inside(point, area)));
    if (shown) kept.push(area);
    placed.push({ id: path.id, x: path.mid.x, y: path.mid.y, w: size.w, h: size.h, shown });
  }
  return placed;
}
