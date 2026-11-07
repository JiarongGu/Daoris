import type { Box } from './viewMath';

// The boxes a placed world is drawn from: every node, waypoint, port and section divider
// in world units, the element's rect less the drawing's origin and divided by the scale the world is drawn at, to half a pixel. The ink,
// the reveal and the anchor read these, so a zoom or a pan changes none of them and nothing measured is k times wrong.

/** Where a canvas's world says the scale it is drawn at. */
export const WORLD = 'data-canvas-world';
export const SCALE = 'data-canvas-scale';

/** The scale an element is drawn at: its world's, and 1 outside a canvas. */
export function scaleOf(element: Element): number {
  const k = Number(element.closest(`[${WORLD}]`)?.getAttribute(SCALE));
  return Number.isFinite(k) && k > 0 ? k : 1;
}

const half = (value: number) => Math.round(value * 2) / 2;

/** An element's box in world units, from the top left of `frame`'s rect, at scale `k`. */
export function worldBox(target: Element, frame: DOMRect, k: number): Box {
  const rect = target.getBoundingClientRect();
  return { x: half((rect.left - frame.left) / k), y: half((rect.top - frame.top) / k), w: half(rect.width / k), h: half(rect.height / k) };
}

/** A placed world as read: its scale, its size and every box the ink is drawn between, in world units. */
export type WorldRead = {
  k: number;
  width: number;
  height: number;
  nodes: Map<string, Box>;
  ways: Map<string, Box>;
  ports: Map<string, Box>;
  sections: Box[];
};

/** The world drawn in `frame`, or null where nothing is laid out (a unit test's DOM). */
export function readWorld(frame: HTMLElement): WorldRead | null {
  const rect = frame.getBoundingClientRect();
  if (rect.width === 0) return null;
  const k = scaleOf(frame);
  const read = (selector: string, key: (element: HTMLElement) => string) => new Map(
    [...frame.querySelectorAll<HTMLElement>(selector)].map((element) => [key(element), worldBox(element, rect, k)] as const),
  );
  const nodes = read('[data-flow-node]', (element) => element.dataset.flowNode!);
  const ways = read('[data-flow-way]', (element) => element.dataset.flowWay!);
  if ([...nodes.values(), ...ways.values()].some((box) => box.w === 0 && box.h === 0)) return null;
  const ports = new Map([...read('[data-flow-port]', (element) => element.dataset.flowPort!)].filter(([, box]) => box.w > 0 && box.h > 0));
  const sections = [...frame.querySelectorAll('[data-flow-section]')].map((element) => worldBox(element, rect, k));
  return { k, width: half(rect.width / k), height: half(rect.height / k), nodes, ways, ports, sections };
}

/**
 * One observer for a world: `draw` now, and again once a frame after the drawing or any node or port in it resized, however many did
 * (trap 14). A transform resizes nothing, so a pan or a zoom draws nothing again. Returns the way to let go.
 */
export function observeWorld(frame: HTMLElement, draw: () => void): () => void {
  let pending = 0;
  const later = () => {
    if (typeof requestAnimationFrame !== 'function') { draw(); return; }
    cancelAnimationFrame(pending);
    pending = requestAnimationFrame(draw);
  };
  draw();
  const observer = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(later) : null;
  observer?.observe(frame);
  frame.querySelectorAll('[data-flow-node], [data-flow-port]').forEach((node) => observer?.observe(node));
  return () => {
    if (typeof cancelAnimationFrame === 'function') cancelAnimationFrame(pending);
    observer?.disconnect();
  };
}
