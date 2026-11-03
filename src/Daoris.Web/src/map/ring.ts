import { howMany } from './MapLines';
import { type Frame, textWidth } from './measure';
import { ASKS, type Topology, layoutRing } from './topology';

// The ring (MAP2): a handful of repositories on a circle, the asks inside it, and every line a curve between two. Pure geometry: the
// map draws it as a fitted world (CANVAS1e), and crossing `RING_MAX` changes this layout for the layers, not the renderer.

export const SIZE = 600;
export const RADIUS = 30;
/** The ring's radius where there is room for it: the square's 0.68, which the map was always drawn at. */
const FULL = (SIZE / 2) * 0.68;
/** The least ring a narrow card gets, below which lines and their counts crowd the nodes. */
const LEAST = 110;
/** Neighbouring nodes' centres are never closer than this, so two rings and a gap stay apart. */
const SPACING = 2 * RADIUS + 30;
/** The sizes a name and its second line are drawn at: `text-small` and `text-meta`. */
const NAME_PX = 12;
const WORD_PX = 11;
/** The asks' disc (MAP4b): smaller than a repository's, since it is a source and holds no count of its own. */
export const ASKS_R = 20;
/** How far a chain's hop bends: past a quest's 36, so the two kinds between one pair stand apart. */
export const CHAIN_BEND = 72;
/** How far a declared dependency bends on the ring: past a chain's, so the three kinds between one pair stand apart. */
export const DEPENDS_BEND = 110;

/**
 * Where everything stands on the ring: the repositories, and the asks inside it, where every line from
 * them runs outward. One repository alone holds the centre, so there the asks stand above it.
 */
export function ringAt(topology: Topology, radius: number): Record<string, { x: number; y: number }> {
  const at = layoutRing(topology.nodes.map((node) => node.id), SIZE, radius);
  if (topology.asks.length === 0) return at;
  at[ASKS] = topology.nodes.length > 1 ? clearest(topology, at, radius) : { x: SIZE / 2, y: SIZE / 2 - 2 * RADIUS - 48 };
  return at;
}

/**
 * The point inside the ring farthest from every repository and every line between them: the centre,
 * or a step toward a gap between two neighbours. 🔴 Seen on the window: at the centre of two
 * repositories the asks sat on both quest lines and hid both counts; a square's diagonals and a
 * triangle's chords cross the middle too.
 */
function clearest(topology: Topology, at: Record<string, { x: number; y: number }>, radius: number) {
  const centre = { x: SIZE / 2, y: SIZE / 2 };
  const count = topology.nodes.length;
  const candidates = [centre];
  for (let gap = 0; gap < count; gap += 1) {
    // Halfway between neighbours, in the ring's own angles (from the top, clockwise).
    const angle = ((gap + 0.5) / count) * 2 * Math.PI - Math.PI / 2;
    for (const reach of [0.2, 0.35, 0.5, 0.65]) {
      candidates.push({ x: centre.x + Math.cos(angle) * radius * reach, y: centre.y + Math.sin(angle) * radius * reach });
    }
  }
  // What must stay clear, first: every repository, and every line's count. Then, as far as it can,
  // the lines themselves; a full circle asked every way leaves no point inside clear of every line,
  // and a line passing under the disc hides less than a count would.
  const counts: { x: number; y: number }[] = [];
  const along: { x: number; y: number }[] = [];
  const sample = (curve: ReturnType<typeof questCurve>) => {
    counts.push(curve.middle);
    const [x0, y0, cx, cy, x1, y1] = curve.d.match(/-?[\d.]+/g)!.map(Number);
    for (let step = 0; step <= 10; step += 1) {
      const t = step / 10;
      along.push({
        x: (1 - t) ** 2 * x0! + 2 * (1 - t) * t * cx! + t ** 2 * x1!,
        y: (1 - t) ** 2 * y0! + 2 * (1 - t) * t * cy! + t ** 2 * y1!,
      });
    }
  };
  for (const edge of topology.quests) sample(questCurve(at[edge.from]!, at[edge.to]!));
  for (const edge of topology.chains) sample(questCurve(at[edge.from]!, at[edge.to]!, CHAIN_BEND));
  // A declared dependency has no count, so it only asks for room as a line.
  for (const edge of topology.depends) {
    const { d } = questCurve(at[edge.from]!, at[edge.to]!, DEPENDS_BEND);
    const [x0, y0, cx, cy, x1, y1] = d.match(/-?[\d.]+/g)!.map(Number);
    for (let step = 0; step <= 10; step += 1) {
      const t = step / 10;
      along.push({ x: (1 - t) ** 2 * x0! + 2 * (1 - t) * t * cx! + t ** 2 * x1!, y: (1 - t) ** 2 * y0! + 2 * (1 - t) * t * cy! + t ** 2 * y1! });
    }
  }
  const lines = [...along];
  for (const edge of topology.knowledge) {
    const { d } = questCurve(at[edge.a]!, at[edge.b]!, 0);
    const [x0, y0, , , x1, y1] = d.match(/-?[\d.]+/g)!.map(Number);
    for (let step = 0; step <= 10; step += 1) lines.push({ x: x0! + ((x1! - x0!) * step) / 10, y: y0! + ((y1! - y0!) * step) / 10 });
  }
  const distance = (a: { x: number; y: number }, b: { x: number; y: number }) => Math.hypot(a.x - b.x, a.y - b.y);
  const score = (point: { x: number; y: number }) => {
    const must = Math.min(
      ...topology.nodes.map((node) => distance(at[node.id]!, point) - RADIUS - ASKS_R - 8),
      ...counts.map((middle) => distance(middle, point) - 11 - ASKS_R - 4),
    );
    const may = Math.min(Infinity, ...lines.map((p) => distance(p, point) - ASKS_R));
    return must >= 0 ? 1000 + Math.min(may, 100) : must;
  };
  return candidates.reduce((best, point) => (score(point) > score(best) + 0.5 ? point : best));
}

/**
 * A line from one node's edge to the other's, bent so the two directions never overlap. A chain's
 * hop bends further than a quest does, so the two kinds between one pair stand apart.
 */
export function questCurve(a: { x: number; y: number }, b: { x: number; y: number }, bend = 36, from = RADIUS) {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const length = Math.hypot(dx, dy) || 1;
  const [ux, uy] = [dx / length, dy / length];
  const start = { x: a.x + ux * from, y: a.y + uy * from };
  // Short of the working ring too, so the arrowhead is never drawn inside it.
  const end = { x: b.x - ux * (RADIUS + 10), y: b.y - uy * (RADIUS + 10) };
  // Perpendicular to the direction of travel: A→B and B→A bend to opposite sides by construction.
  const control = { x: (a.x + b.x) / 2 - uy * bend, y: (a.y + b.y) / 2 + ux * bend };
  // The curve's own midpoint (t = ½ of the quadratic), where the count sits — ON the line, so the
  // thing a person aims at is the line itself.
  const middle = {
    x: 0.25 * start.x + 0.5 * control.x + 0.25 * end.x,
    y: 0.25 * start.y + 0.5 * control.y + 0.25 * end.y,
  };
  return { d: `M ${start.x} ${start.y} Q ${control.x} ${control.y} ${end.x} ${end.y}`, middle };
}

/**
 * Where a node's name goes: on the side facing AWAY from the map's centre. Every line runs inward
 * between nodes on the ring, so the outside is where no line arrives. 🔴 Seen on the window: with the
 * names always below, the arrow into the top node ran straight through its name.
 */
export function placeLabel(x: number, y: number): {
  x: number; y: number; anchor: 'start' | 'middle' | 'end'; next: number;
} {
  const [dx, dy] = [x - SIZE / 2, y - SIZE / 2];
  const gap = RADIUS + 12;
  if (Math.abs(dy) >= Math.abs(dx)) {
    // Above or below. A centred node (one alone) takes below, as a caption.
    return dy < 0
      ? { x, y: y - gap - 18, anchor: 'middle', next: 16 }
      : { x, y: y + gap + 8, anchor: 'middle', next: 16 };
  }
  return dx > 0
    ? { x: x + gap, y: y + 4, anchor: 'start', next: 16 }
    : { x: x - gap, y: y + 4, anchor: 'end', next: 16 };
}

/**
 * The part of the map's square that holds what is drawn: every node and its ring, every name and the
 * word under it, every line's count. 🔴 Seen on the window (UX5 U44): framing the whole square left a
 * three-node ring's lower third blank, and cut a side node's long name at the square's edge
 * (*engine-asset-p*).
 */
export function frameMap(
  topology: Topology,
  at: Record<string, { x: number; y: number }>,
  words: { parked: string; working: string },
): Frame {
  let [left, top, right, bottom] = [Infinity, Infinity, -Infinity, -Infinity];
  const hold = (x0: number, y0: number, x1: number, y1: number) => {
    [left, top, right, bottom] = [Math.min(left, x0), Math.min(top, y0), Math.max(right, x1), Math.max(bottom, y1)];
  };

  for (const node of topology.nodes) {
    const { x, y } = at[node.id]!;
    const ring = node.parked || node.working ? RADIUS + 7 : RADIUS + 2;
    hold(x - ring, y - ring, x + ring, y + ring);
    const label = placeLabel(x, y);
    const said = node.parked ? words.parked : node.working ? words.working : null;
    const word = said === null ? null : `${said}${howMany(node.sessions)}`;
    const width = Math.max(textWidth(node.id, NAME_PX), word ? textWidth(word, WORD_PX) : 0);
    const from = label.anchor === 'start' ? label.x : label.anchor === 'end' ? label.x - width : label.x - width / 2;
    hold(from, label.y - NAME_PX, from + width, label.y + (word ? label.next : 0) + 4);
  }
  for (const edge of topology.quests) {
    const { middle } = questCurve(at[edge.from]!, at[edge.to]!);
    hold(middle.x - 12, middle.y - 12, middle.x + 12, middle.y + 12);
  }
  for (const edge of topology.chains) {
    const { middle } = questCurve(at[edge.from]!, at[edge.to]!, CHAIN_BEND);
    hold(middle.x - 12, middle.y - 12, middle.x + 12, middle.y + 12);
  }
  for (const edge of topology.depends) {
    const { middle } = questCurve(at[edge.from]!, at[edge.to]!, DEPENDS_BEND);
    hold(middle.x - 4, middle.y - 4, middle.x + 4, middle.y + 4);
  }
  const asks = at[ASKS];
  if (asks) hold(asks.x - ASKS_R, asks.y - ASKS_R, asks.x + ASKS_R, asks.y + ASKS_R);

  if (left === Infinity) return { x: 0, y: 0, width: SIZE, height: SIZE };
  const pad = 8;
  const [x, y] = [Math.floor(left - pad), Math.floor(top - pad)];
  return { x, y, width: Math.ceil(right + pad) - x, height: Math.ceil(bottom + pad) - y };
}

/** How far a ring may grow when the card and the window's height leave it room. */
const MOST = 1200;

/**
 * The ring's radius for a card this wide and a window this tall. A narrow card gets a smaller ring,
 * so the names stay at their size (UX5 U44: at 500 the whole square shrank, and every name to about
 * 8px); a wide one gets a larger ring, as far as the height the window leaves it, so the map
 * follows the window (U59: it stayed 531px in a card three times as wide). Never below
 * what keeps neighbours apart, so a large family keeps its spacing and the drawing shrinks as the
 * last resort. Where nothing measures the card, the full ring; where nothing measures the height,
 * no larger than that.
 */
export function fitRadius(
  topology: Topology, room: number | undefined, words: { parked: string; working: string }, tall?: number,
): number {
  const count = topology.nodes.length;
  const floor = Math.max(LEAST, count > 1 ? SPACING / (2 * Math.sin(Math.PI / count)) : 0);
  if (room === undefined) return Math.max(FULL, floor);
  const most = Math.max(tall === undefined ? FULL : MOST, floor);
  for (let radius = most; radius > floor; radius -= 4) {
    const frame = frameMap(topology, ringAt(topology, radius), words);
    if (frame.width <= room && (tall === undefined || frame.height <= tall)) return radius;
  }
  return floor;
}
