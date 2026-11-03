import { buildGrid, nearestWay, type Way } from './spatialGrid';
import type { Box } from './viewMath';

// The walk over a supplied world (CANVAS1e; the canvas design §2.4, §11): every card and every line is a stop, one of them holds the
// world's Tab stop. → from a card reaches its first line out and then the card beyond, ← the line it came by (else its first line in),
// ↑ and ↓ cycle the lines at the end the walk came to, and on a card they go to the nearest card above or below.

export type Stop = { line: boolean; id: string };
export type WalkLine = { id: string; from: string; to: string };

const WAYS: Record<string, Way> = { ArrowUp: 'up', ArrowDown: 'down' };

export function createWalk({ order, boxes, lines, statics = new Set<string>() }: {
  /** The cards in reading order: Home and End go to its ends. */
  order: readonly string[];
  boxes: ReadonlyMap<string, Box>;
  lines: readonly WalkLine[];
  /** Cards that draw but are no stop (a source that is no place): their lines are still reached from the other end. */
  statics?: ReadonlySet<string>;
}) {
  const stops = order.filter((id) => !statics.has(id));
  const grid = buildGrid(new Map([...boxes].filter(([id]) => !statics.has(id))));
  const byId = new Map(lines.map((each) => [each.id, each]));
  const out = (id: string) => lines.filter((each) => each.from === id);
  const into = (id: string) => lines.filter((each) => each.to === id);
  const isStop = (id: string) => !statics.has(id) && boxes.has(id);
  /** The line each card was last reached by, so ← and → return by it. */
  const came = new Map<string, string>();
  /** The card whose lines the walk is cycling, and which way they run from it. */
  let anchor: { node: string; into: boolean } | null = null;

  const cycle = (at: string, back: boolean): Stop | null => {
    const current = byId.get(at);
    if (!current) return null;
    const from = anchor && (anchor.into ? current.to === anchor.node : current.from === anchor.node) ? anchor : { node: current.from, into: false };
    const siblings = from.into ? into(from.node) : out(from.node);
    const index = siblings.findIndex((each) => each.id === at);
    const next = siblings[(index + (back ? -1 : 1) + siblings.length) % siblings.length];
    return next ? { line: true, id: next.id } : null;
  };

  const step = (at: Stop, key: string): Stop | null => {
    if (key === 'Home') return stops[0] ? { line: false, id: stops[0] } : null;
    if (key === 'End') return stops.at(-1) ? { line: false, id: stops.at(-1)! } : null;
    if (at.line) {
      if (key === 'ArrowUp' || key === 'ArrowDown') return cycle(at.id, key === 'ArrowUp');
      const found = byId.get(at.id);
      if (!found) return null;
      const to = key === 'ArrowRight' ? found.to : key === 'ArrowLeft' ? found.from : null;
      if (to === null || !isStop(to)) return null;
      came.set(to, at.id);
      anchor = null;
      return { line: false, id: to };
    }
    if (key === 'ArrowRight' || key === 'ArrowLeft') {
      const rightward = key === 'ArrowRight';
      const choices = rightward ? out(at.id) : into(at.id);
      const kept = came.get(at.id);
      const chosen = choices.find((each) => each.id === kept) ?? choices[0];
      if (!chosen) return null;
      anchor = { node: at.id, into: !rightward };
      return { line: true, id: chosen.id };
    }
    const way = WAYS[key];
    const to = way ? nearestWay(grid, at.id, way) : null;
    return to ? { line: false, id: to } : null;
  };

  return { step };
}
