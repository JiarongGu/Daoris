import type { Box, Point, View } from './viewMath';

// The viewer's place in a graph: kept for the maps under `daoris.canvas.<feature>.<key>`, in a
// try/catch since it is a convenience, and claiming nothing about facts that changed. An unchanged graph gives its view back exactly; a
// changed one keeps the anchor's screen place, else its nearest surviving group's, and says the selected item is gone.

export type Remembered = {
  /** The graph's identity when this is kept: its ids, hashed. */
  identity: string;
  view: View;
  /** The node nearest the middle, its group and where on screen its top left stood. */
  anchor: { id: string; group: string | null; screen: Point } | null;
  selected: string | null;
};

export const memoryKey = (feature: string, key: string) => `daoris.canvas.${feature}.${key}`;

/** A short hash of the ids in their order (FNV-1a), the graph's identity. */
export function graphIdentity(ids: Iterable<string>, extra = ''): string {
  let hash = 0x811c9dc5;
  const feed = (text: string) => {
    for (let at = 0; at < text.length; at += 1) {
      hash ^= text.charCodeAt(at);
      hash = Math.imul(hash, 0x01000193) >>> 0;
    }
    hash ^= 0xff;
    hash = Math.imul(hash, 0x01000193) >>> 0;
  };
  feed(extra);
  for (const id of ids) feed(id);
  return hash.toString(16);
}

type Store = Pick<Storage, 'getItem' | 'setItem'>;
const store = (): Store | null => {
  try { return typeof window === 'undefined' ? null : window.localStorage; } catch { return null; }
};

export function keepView(kept: Remembered, at: string, from: Store | null = store()): void {
  try { from?.setItem(at, JSON.stringify(kept)); } catch { /* a convenience: nothing kept is nothing lost */ }
}

export function recallView(at: string, from: Store | null = store()): Remembered | null {
  try {
    const text = from?.getItem(at);
    if (!text) return null;
    const read = JSON.parse(text) as Partial<Remembered>;
    const view = read.view;
    if (typeof read.identity !== 'string' || !view || ![view.x, view.y, view.k].every(Number.isFinite)) return null;
    return { identity: read.identity, view, anchor: read.anchor ?? null, selected: read.selected ?? null };
  } catch { return null; }
}

/** The anchor to keep: the node nearest the middle of what the view shows. */
export function anchorOf(view: View, viewport: { w: number; h: number }, nodes: ReadonlyMap<string, Box>, groupOf: (id: string) => string | null): Remembered['anchor'] {
  const middle = { x: view.x + viewport.w / (2 * view.k), y: view.y + viewport.h / (2 * view.k) };
  let best: string | null = null;
  let distance = Infinity;
  for (const [id, box] of nodes) {
    const far = Math.hypot(box.x + box.w / 2 - middle.x, box.y + box.h / 2 - middle.y);
    if (far < distance) { distance = far; best = id; }
  }
  if (best === null) return null;
  const box = nodes.get(best)!;
  return { id: best, group: groupOf(best), screen: { x: (box.x - view.x) * view.k, y: (box.y - view.y) * view.k } };
}

export type Restored = { view: View; selectionGone: boolean } | null;

/**
 * The view to open on. The same graph: the kept view, exactly. A changed one: the same scale with the surviving anchor (else its
 * group) where it stood on screen; nothing surviving gives null and the canvas opens as it would. `selectionGone` says the kept
 * selection is no longer in the graph.
 */
export function restoreView(kept: Remembered, identity: string, nodes: ReadonlyMap<string, Box>, groups: ReadonlyMap<string, Box>): Restored {
  const selectionGone = kept.selected !== null && !nodes.has(kept.selected);
  if (kept.identity === identity) return { view: kept.view, selectionGone: false };
  const anchor = kept.anchor;
  if (!anchor) return null;
  const box = nodes.get(anchor.id) ?? (anchor.group !== null ? groups.get(anchor.group) : undefined);
  if (!box) return null;
  const { k } = kept.view;
  return { view: { x: box.x - anchor.screen.x / k, y: box.y - anchor.screen.y / k, k }, selectionGone };
}
