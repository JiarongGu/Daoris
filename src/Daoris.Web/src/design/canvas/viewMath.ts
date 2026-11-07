// The canvas's arithmetic, pure. A view is the world point at the viewport's top left and the
// scale, the map's convention: `screen = origin + k × (world − view)`, so the world's transform is a translation of −k × the view's point
// and then the scale, about the world's own top left.

/** The world point at the viewport's top left, and the scale. */
export type View = { x: number; y: number; k: number };
export type Point = { x: number; y: number };
export type Size = { w: number; h: number };
export type Box = { x: number; y: number; w: number; h: number };
export type ZoomBounds = { min: number; max: number };

/** How far a wheel turns for one step of 1.25×, in pixels: a mouse's notch. */
const NOTCH = 100;
const STEP = 1.25;
const EPSILON = 1e-6;

/** A world point where it stands in the viewport. */
export const toScreen = (view: View, point: Point): Point => ({ x: (point.x - view.x) * view.k, y: (point.y - view.y) * view.k });

/** A viewport point where it stands in the world. */
export const toWorld = (view: View, point: Point): Point => ({ x: view.x + point.x / view.k, y: view.y + point.y / view.k });

/** The world's CSS translation, applied before its scale. */
export const translation = (view: View): Point => ({ x: 0 - view.k * view.x, y: 0 - view.k * view.y });

export const clampScale = (k: number, bounds: ZoomBounds): number => Math.min(bounds.max, Math.max(bounds.min, k));

/** A wheel's travel in pixels, whatever it counts in: lines at a line's height, pages at a window's (as `zoom.ts` counts it). */
export const wheelTravel = (event: { deltaY: number; deltaMode: number }): number =>
  (event.deltaMode === 1 ? event.deltaY * 33 : event.deltaMode === 2 ? event.deltaY * 800 : event.deltaY);

/** The view at scale `k`, within the bounds, the world point under `at` kept under it. */
export function zoomAbout(view: View, k: number, at: Point, bounds: ZoomBounds): View {
  const next = clampScale(k, bounds);
  if (Math.abs(next - view.k) < EPSILON) return view;
  const under = toWorld(view, at);
  return { x: under.x - at.x / next, y: under.y - at.y / next, k: next };
}

/** Ctrl+wheel and a pinch: continuous, `k × 1.25^(−Δ/100)`, so a notch is one step and a touchpad's small travel a small change. */
export const zoomByWheel = (view: View, travel: number, at: Point, bounds: ZoomBounds): View =>
  zoomAbout(view, view.k * STEP ** (-travel / NOTCH), at, bounds);

/** A pan by screen pixels. */
export const panBy = (view: View, dx: number, dy: number): View => ({ x: view.x + dx / view.k, y: view.y + dy / view.k, k: view.k });

/** One axis held: centred where the world fits, else kept in view with `margin` screen pixels past either edge. */
function clampAxis(at: number, world: number, room: number, k: number, margin: number): number {
  const span = room / k;
  if (world <= span) return (world - span) / 2;
  return Math.min(world - span + margin / k, Math.max(0 - margin / k, at));
}

/** The view held to the world: what overflows stays in view, what fits is centred. */
export function clampView(view: View, world: Size, viewport: Size, margin = 0): View {
  const x = clampAxis(view.x, world.w, viewport.w, view.k, margin);
  const y = clampAxis(view.y, world.h, viewport.h, view.k, margin);
  return Math.abs(x - view.x) < EPSILON && Math.abs(y - view.y) < EPSILON ? view : { x, y, k: view.k };
}

/**
 * A plain wheel's pan, and whether the canvas takes it: only while the world overflows that way and is not at that edge, so a wheel at
 * either edge, or over a world that fits, scrolls the page (trap 3).
 */
export function wheelPan(view: View, dx: number, dy: number, world: Size, viewport: Size, margin = 0): { view: View; taken: boolean } {
  const held = clampView(view, world, viewport, margin);
  const next = clampView(panBy(held, dx, dy), world, viewport, margin);
  const taken = Math.abs(next.x - held.x) > EPSILON || Math.abs(next.y - held.y) > EPSILON;
  return { view: taken ? next : view, taken };
}

/** The view that shows `box` whole and centred, never above 100% and within the bounds, `pad` screen pixels clear at each side. */
export function fitView(box: Box, viewport: Size, bounds: ZoomBounds, pad: number): View {
  const room = { w: Math.max(1, viewport.w - 2 * pad), h: Math.max(1, viewport.h - 2 * pad) };
  const k = clampScale(Math.min(1, room.w / Math.max(1, box.w), room.h / Math.max(1, box.h)), bounds);
  return { x: box.x + box.w / 2 - viewport.w / (2 * k), y: box.y + box.h / 2 - viewport.h / (2 * k), k };
}

/** One axis revealed: the least move that brings [start, start + size] inside the margin, its start where it is larger than the room. */
function revealAxis(at: number, start: number, size: number, room: number, k: number, margin: number): number {
  const inset = margin / k;
  const span = room / k - 2 * inset;
  if (size > span || start < at + inset) return start - inset;
  if (start + size > at + inset + span) return start + size - inset - span;
  return at;
}

/** The smallest pan that brings a world box inside a margin of the viewport, never zooming; the same view where it is inside. */
export function reveal(view: View, box: Box, viewport: Size, margin: number): View {
  const x = revealAxis(view.x, box.x, box.w, viewport.w, view.k, margin);
  const y = revealAxis(view.y, box.y, box.h, viewport.h, view.k, margin);
  return x === view.x && y === view.y ? view : { x, y, k: view.k };
}

/** The view that keeps a node where it stood on screen after a relayout moved its box in the world (yEd's mental map). */
export const keepAnchor = (view: View, before: Box, after: Box): View =>
  ({ x: view.x + (after.x - before.x), y: view.y + (after.y - before.y), k: view.k });
