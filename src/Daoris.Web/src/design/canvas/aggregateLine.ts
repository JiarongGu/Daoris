import type { Box } from './viewMath';

/** Where a ray from a box's middle toward a point leaves the box. */
function leaves(box: Box, toward: { x: number; y: number }): { x: number; y: number } {
  const [cx, cy] = [box.x + box.w / 2, box.y + box.h / 2];
  const [dx, dy] = [toward.x - cx, toward.y - cy];
  if (dx === 0 && dy === 0) return { x: cx, y: cy };
  const scale = Math.min(dx === 0 ? Infinity : box.w / 2 / Math.abs(dx), dy === 0 ? Infinity : box.h / 2 / Math.abs(dy));
  return { x: cx + dx * scale, y: cy + dy * scale };
}

/**
 * A straight line between two group boxes, moved aside by `slot` (each direction and kind its own, so a reciprocal pair is two lines,
 * trap 16), with the middle where its count stands.
 */
export function aggregateLine(a: Box, b: Box, slot: number): { d: string; mid: { x: number; y: number } } {
  const [ac, bc] = [{ x: a.x + a.w / 2, y: a.y + a.h / 2 }, { x: b.x + b.w / 2, y: b.y + b.h / 2 }];
  const [dx, dy] = [bc.x - ac.x, bc.y - ac.y];
  const length = Math.hypot(dx, dy) || 1;
  const [nx, ny] = [-dy / length, dx / length];
  const [from, to] = [leaves(a, bc), leaves(b, ac)];
  const [ox, oy] = [nx * slot, ny * slot];
  return { d: `M${from.x + ox} ${from.y + oy}L${to.x + ox} ${to.y + oy}`, mid: { x: (from.x + to.x) / 2 + ox, y: (from.y + to.y) / 2 + oy } };
}
