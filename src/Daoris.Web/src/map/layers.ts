import { type Frame, textWidth } from './measure';
import { ASKS, type Topology } from './topology';

/**
 * A circle too big for a ring, in layers by who asks whom (MAP4, the design's deferred *layers by
 * quest flow*): an asker to the left of whom it asks, one column a step, and what nothing connects set
 * apart below in a grid read by name.
 *
 * @remarks
 * **Why layers.** The first real workspace has twenty-nine repositories, and on a ring every name had
 * to shrink to about 7px for the drawing to fit its card: a ring holds a handful. Layers read at any
 * size, because the drawing grows and the viewport pans and zooms over it (`LayeredMap`), so a name
 * stays the type scale's.
 *
 * **Quests decide the columns; shared findings only keep company.** A quest is a direction, which is
 * what a column step means; a shared finding has none, so a repository known only by one stands in its
 * pair's column, and both kinds of neighbour pull a card toward its neighbours' rows.
 *
 * **Pure, deterministic, and still no graph library** (design §1): longest-path layering with a loop of
 * asks broken where a depth-first walk meets it, then a few barycentre sweeps to uncross the rows.
 *
 * **A line that skips a column takes a slot in every column it crosses** — a waypoint, ordered with the
 * cards, so the sweeps make room for it and the line threads between cards rather than through one.
 * Found on the window: the quest closing a loop of three drew straight across the card between.
 */

/**
 * The most a ring holds before the layers take over: at twelve its names still stand apart at the type
 * scale's size in a card of ordinary width; at twenty-nine they had shrunk to about 7px.
 */
export const RING_MAX = 12;

/** A card's height, the same for every card: its name and the line under it. */
export const CARD_H = 48;
const CARD_MIN = 132;
/** Room around the name inside a card, and the count beside it. */
const CARD_PAD = 44;
const COL_GAP = 112;
const ROW_GAP = 20;
/** Between the connected part and the grid below, which carries a caption. */
const LOOSE_GAP = 64;
const LOOSE_COL_GAP = 20;
/**
 * Room left of the columns for a shared finding between two cards of one column, which bows out round
 * that side. `LayeredMap`'s bow stays inside it, so the frame holds it and *fit* shows it. A quest
 * never needs it: an asker always stands left of whom it asks, and a loop's closing quest runs back
 * through waypoints.
 */
export const ROUND = 80;
/** A waypoint's slot in a column: a line needs far less room than a card. */
const WAY_H = 8;
/** The width the frame fits when nothing says otherwise: a unit test's viewport at the opening scale. */
const WIDTH = 900;
const NAME_PX = 12;
const PAD = 16;
const SWEEPS = 4;

/** A card: its centre and its width; its height is `CARD_H`. */
export type Card = { x: number; y: number; width: number };

export type Point = { x: number; y: number };

export type Layered = {
  at: Record<string, Card>;
  /**
   * The waypoints of every line that skips a column, in the line's own direction and keyed by
   * `lineKey`; a line absent here joins its two cards directly.
   */
  routes: Record<string, Point[]>;
  /** What nothing connects, by name: the grid below. */
  loose: string[];
  /** Where the grid's caption goes, or null when there is no grid. */
  caption: { x: number; y: number } | null;
  frame: Frame;
};

export const lineKey = {
  quests: (from: string, to: string) => `q:${from}>${to}`,
  asks: (to: string) => `a:${to}`,
  chains: (from: string, to: string) => `c:${from}>${to}`,
  depends: (from: string, to: string) => `d:${from}>${to}`,
  knowledge: (a: string, b: string) => `k:${a}|${b}`,
};

const cardWidth = (id: string) => (id === ASKS ? CARD_MIN : Math.max(CARD_MIN, Math.ceil(textWidth(id, NAME_PX)) + CARD_PAD));
const byName = (a: string, b: string) => a.localeCompare(b);

/**
 * Lay the circle out. `width` is what the viewport shows at the opening scale: the grid of what nothing
 * connects wraps inside it, so the grid reads by going down, never across out of sight. The connected
 * part keeps the width its columns need, and the viewport pans over it.
 */
export function layoutLayers(topology: Topology, width = WIDTH): Layered {
  const ids = [...(topology.asks.length > 0 ? [ASKS] : []), ...topology.nodes.map((node) => node.id)];
  const out = new Map<string, string[]>(ids.map((id) => [id, []]));
  const near = new Map<string, Set<string>>(ids.map((id) => [id, new Set()]));
  // Every line with a direction orders the columns: a quest, what an ask became, a chain's hop.
  const directed: [string, string][] = [
    ...topology.quests.map((edge) => [edge.from, edge.to] as [string, string]),
    ...topology.asks.map((edge) => [ASKS, edge.to] as [string, string]),
    ...topology.chains.map((edge) => [edge.from, edge.to] as [string, string]),
    // What a repository says it uses orders them too: whoever depends would be the one asking.
    ...topology.depends.map((edge) => [edge.from, edge.to] as [string, string]),
  ];
  for (const [from, to] of directed) {
    if (!out.get(from)?.includes(to)) out.get(from)?.push(to);
    near.get(from)?.add(to);
    near.get(to)?.add(from);
  }
  const known = new Map<string, string[]>(ids.map((id) => [id, []]));
  for (const edge of topology.knowledge) {
    known.get(edge.a)?.push(edge.b);
    known.get(edge.b)?.push(edge.a);
    near.get(edge.a)?.add(edge.b);
    near.get(edge.b)?.add(edge.a);
  }

  const flows = new Set(directed.flat());
  const connected = ids.filter((id) => (near.get(id)?.size ?? 0) > 0).sort((a, b) => Number(b === ASKS) - Number(a === ASKS) || byName(a, b));
  const loose = ids.filter((id) => (near.get(id)?.size ?? 0) === 0).sort(byName);

  // The asks, with a loop broken where a depth-first walk meets it, so the rest is a DAG.
  const kept: [string, string][] = [];
  const state = new Map<string, 'on' | 'done'>();
  const walk = (id: string) => {
    state.set(id, 'on');
    for (const to of [...(out.get(id) ?? [])].sort(byName)) {
      if (state.get(to) === 'on') continue;
      kept.push([id, to]);
      if (!state.has(to)) walk(to);
    }
    state.set(id, 'done');
  };
  for (const id of connected) if (!state.has(id)) walk(id);

  // Longest path from the askers nobody asks.
  const layer = new Map<string, number>(connected.map((id) => [id, 0]));
  const incoming = new Map<string, number>(connected.map((id) => [id, 0]));
  for (const [, to] of kept) incoming.set(to, (incoming.get(to) ?? 0) + 1);
  const ready = connected.filter((id) => incoming.get(id) === 0);
  while (ready.length > 0) {
    const id = ready.shift()!;
    for (const [from, to] of kept) {
      if (from !== id) continue;
      layer.set(to, Math.max(layer.get(to) ?? 0, (layer.get(id) ?? 0) + 1));
      incoming.set(to, (incoming.get(to) ?? 1) - 1);
      if (incoming.get(to) === 0) ready.push(to);
    }
  }
  // Known only by a shared finding: the column of its pair, by their median.
  for (let pass = 0; pass < 2; pass += 1) {
    for (const id of connected) {
      if (flows.has(id)) continue;
      const pairs = (known.get(id) ?? []).map((other) => layer.get(other) ?? 0).sort((a, b) => a - b);
      if (pairs.length > 0) layer.set(id, pairs[Math.floor((pairs.length - 1) / 2)]!);
    }
  }

  const count = connected.length === 0 ? 0 : Math.max(...connected.map((id) => layer.get(id) ?? 0)) + 1;
  const columns: string[][] = Array.from({ length: count }, () => []);
  for (const id of connected) columns[layer.get(id) ?? 0]!.push(id);

  // A line that skips a column: a waypoint in each column between, chained to its neighbours so the
  // sweeps order it with the cards. Its id holds a character no repository's name can.
  const ways = new Map<string, string[]>();
  const isWay = (id: string) => id.startsWith('\u0000');
  const thread = (key: string, from: string, to: string) => {
    const [a, b] = [layer.get(from) ?? 0, layer.get(to) ?? 0];
    if (Math.abs(a - b) < 2) return;
    const direction = Math.sign(b - a);
    const chain: string[] = [];
    for (let c = a + direction; c !== b; c += direction) {
      const id = `\u0000${key}\u0000${c}`;
      columns[c]!.push(id);
      layer.set(id, c);
      near.set(id, new Set());
      chain.push(id);
    }
    [from, ...chain, to].forEach((id, index, all) => {
      if (index === 0) return;
      near.get(all[index - 1]!)!.add(id);
      near.get(id)!.add(all[index - 1]!);
    });
    ways.set(key, chain);
  };
  for (const edge of topology.quests) thread(lineKey.quests(edge.from, edge.to), edge.from, edge.to);
  for (const edge of topology.asks) thread(lineKey.asks(edge.to), ASKS, edge.to);
  for (const edge of topology.chains) thread(lineKey.chains(edge.from, edge.to), edge.from, edge.to);
  for (const edge of topology.depends) thread(lineKey.depends(edge.from, edge.to), edge.from, edge.to);
  for (const edge of topology.knowledge) thread(lineKey.knowledge(edge.a, edge.b), edge.a, edge.b);

  // Barycentre sweeps: each card toward the rows of its neighbours in the column it is compared with.
  const row = () => new Map(columns.flatMap((column) => column.map((id, index) => [id, index] as const)));
  for (let sweep = 0; sweep < SWEEPS; sweep += 1) {
    const forward = sweep % 2 === 0;
    const order = forward ? columns.map((_, index) => index) : columns.map((_, index) => columns.length - 1 - index);
    for (const c of order) {
      const beside = forward ? c - 1 : c + 1;
      if (beside < 0 || beside >= columns.length) continue;
      const rows = row();
      const there = new Set(columns[beside]);
      const weight = (id: string) => {
        const hits = [...(near.get(id) ?? [])].filter((other) => there.has(other)).map((other) => rows.get(other)!);
        return hits.length > 0 ? hits.reduce((sum, value) => sum + value, 0) / hits.length : rows.get(id)!;
      };
      columns[c] = [...columns[c]!]
        .map((id) => ({ id, w: weight(id) }))
        .sort((a, b) => a.w - b.w || byName(a.id, b.id))
        .map(({ id }) => id);
    }
  }

  const at: Record<string, Card> = {};
  const step = CARD_H + ROW_GAP;
  const height = (id: string) => (isWay(id) ? WAY_H : CARD_H);
  const total = (column: string[]) => column.reduce((sum, id) => sum + height(id), 0) + ROW_GAP * Math.max(0, column.length - 1);
  const tallest = Math.max(0, ...columns.map(total));
  const waypoint = new Map<string, Point>();
  let left = 0;
  for (const column of columns) {
    const cards = column.filter((id) => !isWay(id));
    const width = cards.length > 0 ? Math.max(...cards.map(cardWidth)) : CARD_MIN;
    let top = (tallest - total(column)) / 2;
    for (const id of column) {
      const y = top + height(id) / 2;
      if (isWay(id)) waypoint.set(id, { x: left + width / 2, y });
      else at[id] = { x: left + width / 2, y, width: cardWidth(id) };
      top += height(id) + ROW_GAP;
    }
    left += width + COL_GAP;
  }
  const routes: Record<string, Point[]> = {};
  for (const [key, chain] of ways) routes[key] = chain.map((id) => waypoint.get(id)!);
  const connectedBottom = tallest;
  // Whether a shared finding bows round the left, which then needs room.
  const column = (id: string) => layer.get(id) ?? 0;
  const roundLeft = topology.knowledge.some((edge) => column(edge.a) === column(edge.b));

  // What nothing connects: a flowing grid, each card its own width, wrapping inside the width the
  // viewport opens on. Equal cells sized to the longest name, in rows as wide as a window, ran past
  // the view on the first real workspace, where the detail beside the map leaves it half the window.
  let caption: Layered['caption'] = null;
  if (loose.length > 0) {
    const across = Math.max(2 * CARD_MIN, width - 2 * PAD - (roundLeft ? ROUND : 0));
    const top = connected.length > 0 ? connectedBottom + LOOSE_GAP : 0;
    caption = { x: 0, y: top + 14 };
    let [x, y] = [0, top + 28];
    for (const id of loose) {
      const card = cardWidth(id);
      if (x > 0 && x + card > across) [x, y] = [0, y + step];
      at[id] = { x: x + card / 2, y: y + CARD_H / 2, width: card };
      x += card + LOOSE_COL_GAP;
    }
  }

  let [x0, y0, x1, y1] = [Infinity, Infinity, -Infinity, -Infinity];
  for (const card of Object.values(at)) {
    [x0, y0] = [Math.min(x0, card.x - card.width / 2), Math.min(y0, card.y - CARD_H / 2)];
    [x1, y1] = [Math.max(x1, card.x + card.width / 2), Math.max(y1, card.y + CARD_H / 2)];
  }
  if (caption !== null) y0 = Math.min(y0, caption.y - 14);
  if (roundLeft) x0 = Math.min(x0, -ROUND);
  const frame = x0 === Infinity
    ? { x: 0, y: 0, width: 0, height: 0 }
    : { x: Math.floor(x0 - PAD), y: Math.floor(y0 - PAD), width: Math.ceil(x1 - x0 + 2 * PAD), height: Math.ceil(y1 - y0 + 2 * PAD) };

  return { at, routes, loose, caption, frame };
}
