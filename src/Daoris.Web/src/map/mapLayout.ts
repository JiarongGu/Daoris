import type { SuppliedWorld } from '../design/canvas/SuppliedCanvas';
import type { Box } from '../design/canvas/viewMath';
import { CARD_H, type Card, layoutLayers, lineKey, type Point, ROUND } from './layers';
import type { MapSelection } from './MapLines';
import type { Frame } from './measure';
import { ASKS_R, CHAIN_BEND, DEPENDS_BEND, frameMap, questCurve, RADIUS, ringAt } from './ring';
import {
  ASKS, type AskEdge, type ChainEdge, type DependsEdge, type KnowledgeEdge, type QuestEdge, type Topology,
} from './topology';

// The map's geometry as a positioned world (CANVAS1e; the canvas design §3.3): the ring below `RING_MAX` and the layers above it, each
// one `SuppliedWorld` with its typed lines, so crossing twelve changes this layout and nothing the canvas draws. Pure.

/** A name is the type scale's at 1; the map never opens smaller than this, and pans instead. */
export const START_LEAST = 0.8;
/** How far each direction of a pair asked both ways runs off the middle: past a count bubble's radius. */
const LANE = 12;
/** How far a chain's hop beside a quest between the same two runs off the middle: past the quest's lane. */
const CHAIN_LANE = 20;
/** A workspace's title above its block, and the gap between blocks. */
const TITLE = 28;
const BLOCK_GAP = 40;
const PAD = 16;

export type Drawn =
  | { kind: 'quests'; edge: QuestEdge }
  | { kind: 'asks'; edge: AskEdge }
  | { kind: 'chains'; edge: ChainEdge }
  | { kind: 'depends'; edge: DependsEdge }
  | { kind: 'knowledge'; edge: KnowledgeEdge };

/** One line of the map: its stop's id, its two ends, its path and where its count stands, and what a press chooses. */
export type MapLine = Drawn & { id: string; from: string; to: string; d: string; mid: Point; selection: MapSelection };

export type MapGeometry = {
  world: SuppliedWorld;
  ring: boolean;
  lines: MapLine[];
  /** Where each grid of what nothing connects says so (the layers). */
  captions: Point[];
};

const ends = (drawn: Drawn): { from: string; to: string; id: string; selection: MapSelection } => {
  switch (drawn.kind) {
    case 'quests': return { from: drawn.edge.from, to: drawn.edge.to, id: lineKey.quests(drawn.edge.from, drawn.edge.to), selection: { kind: 'quests', from: drawn.edge.from, to: drawn.edge.to } };
    case 'asks': return { from: ASKS, to: drawn.edge.to, id: lineKey.asks(drawn.edge.to), selection: { kind: 'asks', to: drawn.edge.to } };
    case 'chains': return { from: drawn.edge.from, to: drawn.edge.to, id: lineKey.chains(drawn.edge.from, drawn.edge.to), selection: { kind: 'chains', from: drawn.edge.from, to: drawn.edge.to } };
    case 'depends': return { from: drawn.edge.from, to: drawn.edge.to, id: lineKey.depends(drawn.edge.from, drawn.edge.to), selection: { kind: 'depends', from: drawn.edge.from, to: drawn.edge.to } };
    case 'knowledge': return { from: drawn.edge.a, to: drawn.edge.b, id: lineKey.knowledge(drawn.edge.a, drawn.edge.b), selection: { kind: 'knowledge', a: drawn.edge.a, b: drawn.edge.b } };
  }
};

const line = (drawn: Drawn, d: string, mid: Point): MapLine => ({ ...drawn, ...ends(drawn), d, mid });

/** Every edge of a topology, in the order the walk cycles them: the quests, the asks, the chains, what is declared, what is shared. */
const edgesOf = (topology: Topology): Drawn[] => [
  ...topology.quests.map((edge): Drawn => ({ kind: 'quests', edge })),
  ...topology.asks.map((edge): Drawn => ({ kind: 'asks', edge })),
  ...topology.chains.map((edge): Drawn => ({ kind: 'chains', edge })),
  ...topology.depends.map((edge): Drawn => ({ kind: 'depends', edge })),
  ...topology.knowledge.map((edge): Drawn => ({ kind: 'knowledge', edge })),
];

const walkLines = (lines: readonly MapLine[]) => lines.map(({ id, from, to }) => ({ id, from, to }));

/** The ring: the repositories on a circle with the asks inside it, a curve for every line. */
export function ringGeometry(topology: Topology, radius: number, words: { parked: string; working: string }): MapGeometry {
  const at = ringAt(topology, radius);
  const frame = frameMap(topology, at, words);
  const disc = (point: Point, r: number): Box => ({ x: point.x - r, y: point.y - r, w: 2 * r, h: 2 * r });
  const boxes = new Map<string, Box>();
  const order: string[] = [];
  if (at[ASKS]) { boxes.set(ASKS, disc(at[ASKS], ASKS_R)); order.push(ASKS); }
  for (const node of topology.nodes) { boxes.set(node.id, disc(at[node.id]!, RADIUS)); order.push(node.id); }

  const lines = edgesOf(topology).map((drawn) => {
    const { from, to } = ends(drawn);
    const [a, b] = [at[from]!, at[to]!];
    if (drawn.kind === 'knowledge') return line(drawn, `M ${a.x} ${a.y} L ${b.x} ${b.y}`, { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 });
    const bend = drawn.kind === 'chains' ? CHAIN_BEND : drawn.kind === 'depends' ? DEPENDS_BEND : drawn.kind === 'asks' ? 0 : undefined;
    const curve = questCurve(a, b, bend, drawn.kind === 'asks' ? ASKS_R : undefined);
    return line(drawn, curve.d, curve.middle);
  });
  return {
    ring: true, lines, captions: [],
    world: {
      frame: { x: frame.x, y: frame.y, w: frame.width, h: frame.height }, boxes, order, lines: walkLines(lines),
      ...(at[ASKS] ? { statics: new Set([ASKS]) } : {}),
    },
  };
}

const edge = (card: Card) => ({ left: card.x - card.width / 2, top: card.y - CARD_H / 2, right: card.x + card.width / 2, bottom: card.y + CARD_H / 2 });

/**
 * A line from one card to another, through the waypoints the layout gave it: out of the side facing the other card and into the side
 * facing back, each stretch a curve that leaves and arrives level, so a line threads the gaps between cards. Two cards of one column
 * (only a shared finding, since an asker always stands left of whom it asks) bow round the left, where no quest runs.
 */
function link(a: Card, b: Card, ways: Point[] = [], lane = 0): { d: string; mid: Point } {
  const [p, q] = [edge(a), edge(b)];
  // A lane: how far off the cards' middle a line runs, so two directions between one pair stand apart.
  const [ay, by] = [a.y + lane, b.y + lane];
  if (q.left > p.right || p.left > q.right) {
    const rightward = q.left > p.right;
    const points = [
      { x: rightward ? p.right : p.left, y: ay },
      ...ways,
      { x: rightward ? q.left - 10 : q.right + 10, y: by },
    ];
    const d = points.slice(1).reduce((path, to, index) => {
      const from = points[index]!;
      const bend = (to.x - from.x) / 2;
      return `${path} C ${from.x + bend} ${from.y} ${to.x - bend} ${to.y} ${to.x} ${to.y}`;
    }, `M ${points[0]!.x} ${points[0]!.y}`);
    // The count: on the middle waypoint, or halfway along the middle stretch, where a level-ended curve passes through the midpoint of its ends.
    const middle = ways.length % 2 === 1
      ? ways[(ways.length - 1) / 2]!
      : { x: (points[ways.length / 2]!.x + points[ways.length / 2 + 1]!.x) / 2, y: (points[ways.length / 2]!.y + points[ways.length / 2 + 1]!.y) / 2 };
    return { d, mid: middle };
  }
  // A farther pair bows wider, and none past the room the layout keeps for it (a cubic reaches three quarters of the way to its control points).
  const reach = Math.min(p.left, q.left) - Math.min(40 + Math.abs(a.y - b.y) * 0.12, ROUND);
  const [sx, sy, ex, ey] = [p.left, ay, q.left, by];
  return { d: `M ${sx} ${sy} C ${reach} ${sy} ${reach} ${ey} ${ex} ${ey}`, mid: { x: reach + 14, y: (sy + ey) / 2 } };
}

/** A line between two blocks: out of the facing edge of one card, down or up the gap, into the facing edge of the other. */
function across(a: Card, b: Card, lane: number): { d: string; mid: Point } {
  const down = b.y > a.y;
  const [sx, sy] = [a.x + lane, down ? edge(a).bottom : edge(a).top];
  const [ex, ey] = [b.x + lane, down ? edge(b).top - 10 : edge(b).bottom + 10];
  const my = (sy + ey) / 2;
  return { d: `M ${sx} ${sy} C ${sx} ${my} ${ex} ${my} ${ex} ${ey}`, mid: { x: (sx + ex) / 2, y: my } };
}

type Placed = { at: Record<string, Card>; routes: Record<string, Point[]> };

/** Every line of one block, the lane each takes beside another between the same two (found on the window: two counts stacked). */
function layerLines(topology: Topology, { at, routes }: Placed): MapLine[] {
  const quests = topology.quests;
  const beside = (a: string, b: string, others: readonly { from: string; to: string }[]) =>
    others.some((other) => (other.from === a && other.to === b) || (other.from === b && other.to === a));
  return edgesOf(topology).map((drawn) => {
    const { from, to, id } = ends(drawn);
    const [a, b] = [at[from]!, at[to]!];
    let lane = 0;
    if (drawn.kind === 'depends') lane = beside(from, to, [...quests, ...topology.chains]) ? (a.x < b.x ? CHAIN_LANE : -CHAIN_LANE) : 0;
    if (drawn.kind === 'chains') lane = beside(from, to, quests) ? (a.x < b.x ? -CHAIN_LANE : CHAIN_LANE) : 0;
    // Asked both ways: each direction in a lane of its own, the rightward above and the other below.
    if (drawn.kind === 'quests') lane = quests.some((other) => other.from === to && other.to === from) ? (a.x < b.x ? -LANE : LANE) : 0;
    const { d, mid } = link(a, b, routes[id], lane);
    return line(drawn, d, mid);
  });
}

/** The topology a block lays out alone: its repositories, and the lines whose two ends both stand in it. */
function blockTopology(topology: Topology, names: ReadonlySet<string>, asksHere: boolean): Topology {
  const inside = (a: string, b: string) => names.has(a) && names.has(b);
  return {
    ...topology,
    nodes: topology.nodes.filter((node) => names.has(node.id)),
    quests: topology.quests.filter((each) => inside(each.from, each.to)),
    asks: asksHere ? topology.asks.filter((each) => names.has(each.to)) : [],
    chains: topology.chains.filter((each) => inside(each.from, each.to)),
    depends: topology.depends.filter((each) => inside(each.from, each.to)),
    knowledge: topology.knowledge.filter((each) => inside(each.a, each.b)),
    circles: 1,
  };
}

/**
 * The layers (MAP4): one workspace's map is `layoutLayers` whole; several are blocks, each its own layers under its name, stacked by name,
 * and a line between two blocks runs down the gap between them. `width` is what the viewport shows at the opening scale.
 */
export function layeredGeometry(topology: Topology, width: number): MapGeometry {
  const many = topology.circles > 1;
  const names = many ? [...new Set(topology.nodes.map((node) => node.workspace ?? ''))].sort((a, b) => a.localeCompare(b)) : [''];
  const members = new Map(names.map((name) => [name, new Set(topology.nodes.filter((node) => !many || (node.workspace ?? '') === name).map((node) => node.id))]));
  const blockOf = new Map([...members].flatMap(([name, ids]) => [...ids].map((id) => [id, name] as const)));
  // The asks stand in the block most of them reach.
  const reached = new Map(names.map((name) => [name, topology.asks.filter((each) => members.get(name)!.has(each.to)).length]));
  const home = names.reduce((best, name) => ((reached.get(name) ?? 0) > (reached.get(best) ?? 0) ? name : best), names[0]!);
  blockOf.set(ASKS, home);

  const at: Record<string, Card> = {};
  const lines: MapLine[] = [];
  const captions: Point[] = [];
  const groups: { id: string; title: string; box: Box }[] = [];
  let cursor = 0;
  let extent = 0;
  let single: Frame | null = null;
  for (const name of names) {
    const sub = blockTopology(topology, members.get(name)!, name === home);
    const layout = layoutLayers(sub, width);
    if (!many) single = layout.frame;
    const [dx, dy] = many ? [PAD - layout.frame.x, cursor + TITLE - layout.frame.y] : [0, 0];
    const placed: Placed = {
      at: Object.fromEntries(Object.entries(layout.at).map(([id, card]) => [id, { ...card, x: card.x + dx, y: card.y + dy }])),
      routes: Object.fromEntries(Object.entries(layout.routes).map(([key, points]) => [key, points.map((point) => ({ x: point.x + dx, y: point.y + dy }))])),
    };
    Object.assign(at, placed.at);
    lines.push(...layerLines(sub, placed));
    if (layout.caption) captions.push({ x: layout.caption.x + dx, y: layout.caption.y + dy });
    if (many) {
      const box = { x: PAD / 2, y: cursor, w: layout.frame.width + PAD, h: layout.frame.height + TITLE };
      groups.push({ id: name, title: name, box });
      cursor += box.h + BLOCK_GAP;
      extent = Math.max(extent, box.w + PAD);
    }
  }
  if (many) {
    // What runs between two blocks: each end in its own, so it is drawn down the gap.
    const apart = (a: string, b: string) => blockOf.get(a) !== blockOf.get(b);
    const asksTo = topology.asks.filter((each) => apart(ASKS, each.to));
    const others = edgesOf({ ...topology, asks: asksTo }).filter((drawn) => {
      const { from, to } = ends(drawn);
      return blockOf.has(from) && blockOf.has(to) && apart(from, to);
    });
    for (const drawn of others) {
      const { from, to } = ends(drawn);
      const both = drawn.kind === 'quests' && topology.quests.some((other) => other.from === to && other.to === from);
      const { d, mid } = across(at[from]!, at[to]!, both ? (from < to ? -LANE : LANE) : 0);
      lines.push(line(drawn, d, mid));
    }
  }

  const boxes = new Map<string, Box>(Object.entries(at).map(([id, card]) => [id, { x: card.x - card.width / 2, y: card.y - CARD_H / 2, w: card.width, h: CARD_H }]));
  const frame: Box = single
    ? { x: single.x, y: single.y, w: single.width, h: single.height }
    : { x: 0, y: 0, w: Math.max(extent, 1), h: Math.max(cursor - BLOCK_GAP, 1) };
  const order = [...(at[ASKS] ? [ASKS] : []), ...topology.nodes.map((node) => node.id).filter((id) => boxes.has(id))];
  return {
    ring: false, lines, captions,
    world: {
      frame, boxes, order, lines: walkLines(lines), ...(at[ASKS] ? { statics: new Set([ASKS]) } : {}),
      ...(many ? { groups, groupOf: (id: string) => blockOf.get(id) ?? null } : {}),
    },
  };
}
