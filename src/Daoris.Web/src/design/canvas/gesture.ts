import { type Box, type Point, type Size, toWorld, type View } from './viewMath';

// The pure arithmetic of the canvas's pointer gestures (CANVAS1b; D224, the canvas design §2.4, §2.5): the box drawn to select, the pan
// at the viewport's edge while a connection is dragged, and where a rubber line starts.

/** The box two screen points span, as the world rectangle it covers under `view` (the points are the viewport's own). */
export function worldRect(view: View, a: Point, b: Point): Box {
  const from = toWorld(view, { x: Math.min(a.x, b.x), y: Math.min(a.y, b.y) });
  const to = toWorld(view, { x: Math.max(a.x, b.x), y: Math.max(a.y, b.y) });
  return { x: from.x, y: from.y, w: to.x - from.x, h: to.y - from.y };
}

const meets = (a: Box, b: Box) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;

/** The nodes a drawn box meets, in reading order (top to bottom, then left to right): a box selects what it touches. */
export function boxSelect(rect: Box, boxes: ReadonlyMap<string, Box>): string[] {
  return [...boxes].filter(([, box]) => meets(rect, box)).sort(([, a], [, b]) => a.y - b.y || a.x - b.x).map(([id]) => id);
}

/**
 * The pan, in screen px per frame, while a line is dragged near the viewport's edge: none inside `margin` of the middle, growing to
 * `speed` at the edge itself, toward the edge the pointer is at. The view moves by it as the pointer holds still.
 */
export function edgeDrift(at: Point, viewport: Size, margin: number, speed: number): Point {
  const along = (value: number, extent: number) => {
    if (value < margin) return -speed * Math.min(1, (margin - value) / margin);
    if (value > extent - margin) return speed * Math.min(1, (value - (extent - margin)) / margin);
    return 0;
  };
  return { x: along(at.x, viewport.w), y: along(at.y, viewport.h) };
}
