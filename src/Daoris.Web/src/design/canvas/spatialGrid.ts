import type { Box, Point } from './viewMath';

// The spatial grid a positioned world is culled and walked by (CANVAS1d; the canvas design §4): boxes bucketed by cell, so what is in
// view, and what lies nearest in a direction, is read from a few cells and never from every node.

export type Grid = { cell: number; cells: Map<number, string[]>; boxes: ReadonlyMap<string, Box> };

/** A cell's key: columns and rows are far below 2^20 for any world (a 1,048,576-cell side at 256 px is 268 million px). */
const keyOf = (column: number, row: number) => (column + 0x80000) * 0x100000 + (row + 0x80000);

/** Every box in the cells it touches. */
export function buildGrid(boxes: ReadonlyMap<string, Box>, cell = 256): Grid {
  const cells = new Map<number, string[]>();
  for (const [id, box] of boxes) {
    const [c0, c1, r0, r1] = [Math.floor(box.x / cell), Math.floor((box.x + box.w) / cell), Math.floor(box.y / cell), Math.floor((box.y + box.h) / cell)];
    for (let column = c0; column <= c1; column += 1) {
      for (let row = r0; row <= r1; row += 1) {
        const key = keyOf(column, row);
        const held = cells.get(key);
        if (held) held.push(id);
        else cells.set(key, [id]);
      }
    }
  }
  return { cell, cells, boxes };
}

const meets = (a: Box, b: Box) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;

/** The ids whose box meets `rect`, each once. */
export function queryGrid(grid: Grid, rect: Box): Set<string> {
  const found = new Set<string>();
  const { cell } = grid;
  const [c0, c1, r0, r1] = [Math.floor(rect.x / cell), Math.floor((rect.x + rect.w) / cell), Math.floor(rect.y / cell), Math.floor((rect.y + rect.h) / cell)];
  // A rect far wider than the world holds fewer cells than it spans: walk the cells held instead.
  if ((c1 - c0 + 1) * (r1 - r0 + 1) > grid.cells.size) {
    for (const [id, box] of grid.boxes) if (meets(box, rect)) found.add(id);
    return found;
  }
  for (let column = c0; column <= c1; column += 1) {
    for (let row = r0; row <= r1; row += 1) {
      for (const id of grid.cells.get(keyOf(column, row)) ?? []) if (!found.has(id) && meets(grid.boxes.get(id)!, rect)) found.add(id);
    }
  }
  return found;
}

const RINGS = 40;

export type Way ='up' | 'down' | 'left' | 'right';

/**
 * The box nearest `from` in a direction: its middle lies on that side, and the score is the distance along plus twice the distance
 * across, so the arrow keeps to its row or column when it can. Searches rings of cells outward and stops at the first ring that holds
 * a candidate no farther than the ring's own reach, so a key reaches a node far off screen without reading every node.
 */
export function nearestWay(grid: Grid, fromId: string, way: Way): string | null {
  const from = grid.boxes.get(fromId);
  if (!from) return null;
  const middle: Point = { x: from.x + from.w / 2, y: from.y + from.h / 2 };
  const along = way === 'up' || way === 'down';
  const sign = way === 'down' || way === 'right' ? 1 : -1;
  let best: string | null = null;
  let score = Infinity;
  const consider = (id: string) => {
    if (id === fromId) return;
    const box = grid.boxes.get(id)!;
    const [dx, dy] = [box.x + box.w / 2 - middle.x, box.y + box.h / 2 - middle.y];
    const [main, cross] = along ? [dy * sign, Math.abs(dx)] : [dx * sign, Math.abs(dy)];
    if (main <= 0.5) return;
    const value = main + 2 * cross;
    if (value < score) { score = value; best = id; }
  };
  const [c0, r0] = [Math.floor(middle.x / grid.cell), Math.floor(middle.y / grid.cell)];
  for (let ring = 0; ring <= RINGS; ring += 1) {
    for (let column = c0 - ring; column <= c0 + ring; column += 1) {
      for (let row = r0 - ring; row <= r0 + ring; row += 1) {
        if (Math.max(Math.abs(column - c0), Math.abs(row - r0)) !== ring) continue;
        for (const id of grid.cells.get(keyOf(column, row)) ?? []) consider(id);
      }
    }
    // Anything in a later ring is at least `ring * cell` away in some axis; a score under that cannot be beaten.
    if (best !== null && score <= ring * grid.cell) return best;
  }
  if (best !== null) return best;
  // Nothing within the rings: scan, so a key is never dead where a node exists on that side.
  for (const id of grid.boxes.keys()) consider(id);
  return best;
}
