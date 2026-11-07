import { buildGrid, queryGrid } from '../../design/canvas/spatialGrid';
import type { Box } from '../../design/canvas/viewMath';

// The code map's lines routed around its cards. Pure. A line keeps its direct shape (level
// where two cards stand side by side, down or up where one is below the other) unless that shape crosses a card; then it runs
// orthogonally through the gaps, found by a shortest path over the cards' edges that pays for each bend, and leaves and enters its
// cards as the direct shape would. The work is bounded, never the facts: past the bounds a line keeps its direct shape.

type Point = { x: number; y: number };
export type Routed = { d: string; mid: Point; points: Point[] };

/** How far a routed line keeps from a card it passes. */
const CLEAR = 8;
/** The straight piece a line leaves its card by and enters the next by, so it never runs along a card's edge. */
const STUB = 12;
/** How far beyond its two ends a route may go round. */
const REACH = 160;
/** What a bend costs, in world units of length: two bends are worth a detour of a card's width. */
const BEND = 64;
/** The corner's radius, in world units. */
const CORNER = 6;
/**
 * Bounds on the work, in grid cells searched: what one scene spends in all, and the cards one search holds at a time. A route's cost
 * is its grid's size, so a scene of many short routes is all routed and a few long ones in a crowd are not.
 */
export const ROUTE_CELLS = 2_000_000;
const OBSTACLES_MAX = 64;
/** How many times a route that crossed a card left out of its search is found again with it in. */
const REFINE = 4;

const grow = (box: Box, by: number): Box => ({ x: box.x - by, y: box.y - by, w: box.w + 2 * by, h: box.h + 2 * by });
const spanOf = (points: readonly Point[]): Box => {
  const xs = points.map((point) => point.x);
  const ys = points.map((point) => point.y);
  const [x, y] = [Math.min(...xs), Math.min(...ys)];
  return { x, y, w: Math.max(...xs) - x, h: Math.max(...ys) - y };
};

/** Whether the segment from `p` to `q` runs through the box's open inside (Liang–Barsky): along an edge or through a corner is clear. */
export function crosses(p: Point, q: Point, box: Box): boolean {
  let [t0, t1] = [0, 1];
  const [dx, dy] = [q.x - p.x, q.y - p.y];
  const sides: [number, number][] = [[-dx, p.x - box.x], [dx, box.x + box.w - p.x], [-dy, p.y - box.y], [dy, box.y + box.h - p.y]];
  for (const [pk, qk] of sides) {
    if (pk === 0) {
      if (qk <= 0) return false;
      continue;
    }
    const r = qk / pk;
    if (pk < 0) {
      if (r >= t1) return false;
      if (r > t0) t0 = r;
    } else {
      if (r <= t0) return false;
      if (r < t1) t1 = r;
    }
  }
  return t1 - t0 > 1e-9;
}

type Ends = { s: Point; s1: Point; e1: Point; e: Point; vertical: boolean; cubic: number[] };

/** Where a line leaves `a` and enters `b`, moved aside by `lane`, with the stubs and the direct shape's control points. */
function endsOf(a: Box, b: Box, lane: number): Ends {
  const [ax, bx] = [a.x + a.w / 2 + lane, b.x + b.w / 2 + lane];
  if (b.y > a.y + a.h) {
    const [y1, y2] = [a.y + a.h, b.y - 2];
    const bend = (y2 - y1) / 2;
    return { s: { x: ax, y: y1 }, s1: { x: ax, y: y1 + STUB }, e1: { x: bx, y: y2 - STUB }, e: { x: bx, y: y2 }, vertical: true, cubic: [ax, y1, ax, y1 + bend, bx, y2 - bend, bx, y2] };
  }
  if (b.y + b.h < a.y) {
    const [y1, y2] = [a.y, b.y + b.h + 2];
    const bend = (y1 - y2) / 2;
    return { s: { x: ax, y: y1 }, s1: { x: ax, y: y1 - STUB }, e1: { x: bx, y: y2 + STUB }, e: { x: bx, y: y2 }, vertical: true, cubic: [ax, y1, ax, y1 - bend, bx, y2 + bend, bx, y2] };
  }
  const out = b.x > a.x ? 1 : -1;
  const [sx, ex] = out > 0 ? [a.x + a.w, b.x - 2] : [a.x, b.x + b.w + 2];
  const [sy, ey] = [a.y + a.h / 2 + lane, b.y + b.h / 2 + lane];
  const bend = Math.abs(ex - sx) / 2;
  return {
    s: { x: sx, y: sy }, s1: { x: sx + out * STUB, y: sy }, e1: { x: ex - out * STUB, y: ey }, e: { x: ex, y: ey }, vertical: false,
    cubic: [sx, sy, sx + out * bend, sy, ex - out * bend, ey, ex, ey],
  };
}

function cubicPoints(p: readonly number[], steps = 16): Point[] {
  const points: Point[] = [];
  for (let at = 0; at <= steps; at += 1) {
    const t = at / steps;
    const u = 1 - t;
    const [a, b, c, d] = [u * u * u, 3 * u * u * t, 3 * u * t * t, t * t * t];
    points.push({ x: a * p[0]! + b * p[2]! + c * p[4]! + d * p[6]!, y: a * p[1]! + b * p[3]! + c * p[5]! + d * p[7]! });
  }
  return points;
}

/** A small binary heap of states by their estimate. */
function heap() {
  const keys: number[] = [];
  const items: number[] = [];
  const swap = (i: number, j: number) => { [keys[i], keys[j]] = [keys[j]!, keys[i]!]; [items[i], items[j]] = [items[j]!, items[i]!]; };
  return {
    get size() { return items.length; },
    push(key: number, item: number) {
      keys.push(key); items.push(item);
      for (let at = items.length - 1; at > 0;) {
        const up = (at - 1) >> 1;
        if (keys[up]! <= keys[at]!) break;
        swap(at, up); at = up;
      }
    },
    pop(): number {
      const top = items[0]!;
      const [lastKey, lastItem] = [keys.pop()!, items.pop()!];
      if (items.length > 0) {
        keys[0] = lastKey; items[0] = lastItem;
        for (let at = 0; ;) {
          const [l, r] = [2 * at + 1, 2 * at + 2];
          let least = at;
          if (l < items.length && keys[l]! < keys[least]!) least = l;
          if (r < items.length && keys[r]! < keys[least]!) least = r;
          if (least === at) break;
          swap(at, least); at = least;
        }
      }
      return top;
    },
  };
}

const MOVES = [[1, 0], [-1, 0], [0, 1], [0, -1]] as const;

/**
 * The shortest orthogonal path from `from` to `to` that runs through no obstacle's inside, over the lines its edges make (an
 * orthogonal visibility grid), each bend paid for. `null` where none is found.
 */
function orthogonal(from: Point, to: Point, obstacles: readonly Box[], region: Box, vertical: boolean): Point[] | null {
  const sides = obstacles.map((box) => ({ l: box.x, r: box.x + box.w, t: box.y, b: box.y + box.h }));
  const unique = (values: number[]) => [...new Set(values)].sort((p, q) => p - q);
  const xs = unique([from.x, to.x, region.x, region.x + region.w, ...sides.flatMap((side) => [side.l, side.r])]);
  const ys = unique([from.y, to.y, region.y, region.y + region.h, ...sides.flatMap((side) => [side.t, side.b])]);
  const [nx, ny] = [xs.length, ys.length];
  const ix = new Map(xs.map((x, at) => [x, at]));
  const iy = new Map(ys.map((y, at) => [y, at]));
  const inside = new Uint8Array(nx * ny);
  const across = new Uint8Array(nx * ny);
  const down = new Uint8Array(nx * ny);
  for (const side of sides) {
    const [i0, i1, j0, j1] = [ix.get(side.l)!, ix.get(side.r)!, iy.get(side.t)!, iy.get(side.b)!];
    for (let j = j0; j <= j1; j += 1) {
      for (let i = i0; i <= i1; i += 1) {
        const at = j * nx + i;
        const [inX, inY] = [i > i0 && i < i1, j > j0 && j < j1];
        if (inX && inY) inside[at] = 1;
        if (i < i1 && inY) across[at] = 1;
        if (j < j1 && inX) down[at] = 1;
      }
    }
  }
  const start = iy.get(from.y)! * nx + ix.get(from.x)!;
  const goal = iy.get(to.y)! * nx + ix.get(to.x)!;
  const cost = new Float64Array(nx * ny * 2).fill(Infinity);
  const back = new Int32Array(nx * ny * 2).fill(-1);
  const done = new Uint8Array(nx * ny * 2);
  const guess = (node: number) => Math.abs(xs[node % nx]! - to.x) + Math.abs(ys[Math.floor(node / nx)]! - to.y);
  const open = heap();
  const first = start * 2 + (vertical ? 1 : 0);
  cost[first] = 0;
  open.push(guess(start), first);
  let reached = -1;
  while (open.size > 0) {
    const state = open.pop();
    if (done[state]) continue;
    done[state] = 1;
    const [node, axis] = [state >> 1, state & 1];
    if (node === goal) { reached = state; break; }
    const [i, j] = [node % nx, Math.floor(node / nx)];
    for (const [di, dj] of MOVES) {
      const [ni, nj] = [i + di, j + dj];
      if (ni < 0 || nj < 0 || ni >= nx || nj >= ny) continue;
      const blocked = di === 1 ? across[j * nx + i] : di === -1 ? across[j * nx + ni] : dj === 1 ? down[j * nx + i] : down[nj * nx + i];
      const next = nj * nx + ni;
      if (blocked || inside[next]) continue;
      const turn = di !== 0 ? 0 : 1;
      const step = cost[state]! + Math.abs(xs[ni]! - xs[i]!) + Math.abs(ys[nj]! - ys[j]!) + (turn !== axis ? BEND : 0);
      const to2 = next * 2 + turn;
      if (step < cost[to2]!) { cost[to2] = step; back[to2] = state; open.push(step + guess(next), to2); }
    }
  }
  if (reached < 0) return null;
  const path: Point[] = [];
  for (let state = reached; state >= 0; state = back[state]!) {
    const node = state >> 1;
    path.push({ x: xs[node % nx]!, y: ys[Math.floor(node / nx)]! });
  }
  return path.reverse();
}

/** The points with every repeat and every point in the middle of a straight run dropped. */
function simplify(points: readonly Point[]): Point[] {
  const kept: Point[] = [];
  for (const point of points) {
    const last = kept[kept.length - 1];
    if (last && last.x === point.x && last.y === point.y) continue;
    const before = kept[kept.length - 2];
    if (before && last && ((before.x === last.x && last.x === point.x) || (before.y === last.y && last.y === point.y))) kept.pop();
    kept.push(point);
  }
  return kept;
}

/** A polyline drawn with its corners rounded. */
function polylineD(points: readonly Point[]): string {
  let d = `M ${points[0]!.x} ${points[0]!.y}`;
  for (let at = 1; at < points.length - 1; at += 1) {
    const [p, c, n] = [points[at - 1]!, points[at]!, points[at + 1]!];
    const r = Math.min(CORNER, Math.hypot(c.x - p.x, c.y - p.y) / 2, Math.hypot(n.x - c.x, n.y - c.y) / 2);
    const toward = (q: Point) => { const length = Math.hypot(q.x - c.x, q.y - c.y) || 1; return { x: c.x + ((q.x - c.x) / length) * r, y: c.y + ((q.y - c.y) / length) * r }; };
    const [a, b] = [toward(p), toward(n)];
    d += ` L ${a.x} ${a.y} Q ${c.x} ${c.y} ${b.x} ${b.y}`;
  }
  const last = points[points.length - 1]!;
  return `${d} L ${last.x} ${last.y}`;
}

/** The point halfway along a polyline, where a count stands. */
function halfway(points: readonly Point[]): Point {
  const lengths = points.slice(1).map((point, at) => Math.hypot(point.x - points[at]!.x, point.y - points[at]!.y));
  let left = lengths.reduce((sum, each) => sum + each, 0) / 2;
  for (let at = 0; at < lengths.length; at += 1) {
    const length = lengths[at]!;
    if (left <= length && length > 0) {
      const [p, q] = [points[at]!, points[at + 1]!];
      return { x: p.x + ((q.x - p.x) * left) / length, y: p.y + ((q.y - p.y) * left) / length };
    }
    left -= length;
  }
  return points[points.length - 1]!;
}

/**
 * A router over one scene's cards: `route` gives the line from one card to another, moved aside by `lane` where a pair runs both
 * ways, in its direct shape where that crosses no card, and else around them.
 */
export function createRouter(boxes: ReadonlyMap<string, Box>): { route: (from: string, to: string, lane: number) => Routed } {
  const grid = buildGrid(boxes);
  let spent = 0;
  return {
    route(from, to, lane) {
      const [a, b] = [boxes.get(from)!, boxes.get(to)!];
      const ends = endsOf(a, b, lane);
      const points = cubicPoints(ends.cubic);
      const p = ends.cubic;
      const direct: Routed = {
        d: `M ${p[0]} ${p[1]} C ${p[2]} ${p[3]} ${p[4]} ${p[5]} ${p[6]} ${p[7]}`,
        mid: { x: (p[0]! + 3 * p[2]! + 3 * p[4]! + p[6]!) / 8, y: (p[1]! + 3 * p[3]! + 3 * p[5]! + p[7]!) / 8 },
        points,
      };
      const blockers = [...queryGrid(grid, grow(spanOf(points), CLEAR))].filter((id) => id !== from && id !== to).map((id) => grow(boxes.get(id)!, CLEAR / 2));
      if (!blockers.some((box) => points.some((point, at) => at > 0 && crosses(points[at - 1]!, point, box)))) return direct;
      const region = grow(spanOf([ends.s1, ends.e1]), REACH);
      // A crowded region is searched among the cards nearest the direct shape, then again with any card the found route still crosses.
      const near = [...queryGrid(grid, region)].filter((id) => id !== from && id !== to);
      const mid = { x: (ends.s1.x + ends.e1.x) / 2, y: (ends.s1.y + ends.e1.y) / 2 };
      const gap = (id: string) => { const box = boxes.get(id)!; return Math.hypot(box.x + box.w / 2 - mid.x, box.y + box.h / 2 - mid.y); };
      const held = new Set([from, to, ...near.sort((p, q) => gap(p) - gap(q)).slice(0, OBSTACLES_MAX)]);
      let path: Point[] | null = null;
      for (let round = 0; round <= REFINE; round += 1) {
        const cells = (2 * held.size + 4) ** 2;
        if (spent + cells > ROUTE_CELLS) return direct;
        spent += cells;
        path = orthogonal(ends.s1, ends.e1, [...held].map((id) => grow(boxes.get(id)!, CLEAR)), region, ends.vertical);
        if (!path) return direct;
        const left = near.filter((id) => !held.has(id) && path!.some((point, at) => at > 0 && crosses(path![at - 1]!, point, grow(boxes.get(id)!, CLEAR))));
        if (left.length === 0) break;
        if (round === REFINE) return direct;
        for (const id of left) held.add(id);
      }
      if (!path) return direct;
      const all = simplify([ends.s, ...path, ends.e]);
      return { d: polylineD(all), mid: halfway(all), points: all };
    },
  };
}
