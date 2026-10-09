import { type KeyboardEvent, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { isComposing } from '../lib/composing';
import { ASKS, type ChainEdge, type Live, type Topology, layoutRing } from './topology';
import { type Frame, textWidth } from './measure';
import { useTall, useWidth } from './useWidth';

/**
 * What a person has chosen on the map: a repository, the quests one way, what the asks put on one
 * repository, a chain's hop, or a shared finding.
 */
export type MapSelection =
  | { kind: 'node'; id: string }
  | { kind: 'quests'; from: string; to: string }
  | { kind: 'asks'; to: string }
  | { kind: 'chains'; from: string; to: string }
  | { kind: 'depends'; from: string; to: string }
  | { kind: 'knowledge'; a: string; b: string };

/** Whether two choices are the same part of the map. */
export function sameSelection(a: MapSelection | null, b: MapSelection | null): boolean {
  if (a === null || b === null) return a === b;
  if (a.kind === 'node' && b.kind === 'node') return a.id === b.id;
  if (a.kind === 'quests' && b.kind === 'quests') return a.from === b.from && a.to === b.to;
  if (a.kind === 'asks' && b.kind === 'asks') return a.to === b.to;
  if (a.kind === 'chains' && b.kind === 'chains') return a.from === b.from && a.to === b.to;
  if (a.kind === 'depends' && b.kind === 'depends') return a.from === b.from && a.to === b.to;
  if (a.kind === 'knowledge' && b.kind === 'knowledge') return a.a === b.a && a.b === b.b;
  return false;
}

const SIZE = 600;
const RADIUS = 30;
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
const ASKS_R = 20;
/** How far a chain's hop bends: past a quest's 36, so the two kinds between one pair stand apart. */
const CHAIN_BEND = 72;
/** What the asks' lines and a chain's hops are drawn with, beside a quest's solid line (D41: never colour alone). */
export const ASKS_DASH = '10 4';
export const CHAIN_DASH = '1 5';
/** What a repository says it uses (MAP4e): dash and dot, in ink rather than the accent, since it is a declaration and not work. */
export const DEPENDS_DASH = '8 3 2 3';
/** How far a declared dependency bends on the ring: past a chain's, so the three kinds between one pair stand apart. */
const DEPENDS_BEND = 110;

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
 * What a repository says it uses (MAP4e): a declaration, so dash and dot in ink, an open head, and no
 * count, since nothing moved along it. A button like every line, named for what it says.
 */
export function DependsLine({ d, marker, label, chosen, back, onChoose, onPress }: {
  d: string; marker: string; label: string; chosen: boolean;
  /** Whether it steps back while something else is in focus. */
  back: boolean;
  onChoose: () => void;
  onPress: (event: KeyboardEvent) => void;
}) {
  return (
    <g
      role="button"
      tabIndex={0}
      aria-label={label}
      aria-pressed={chosen}
      onClick={onChoose}
      onKeyDown={onPress}
      className={cn('cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>path.shown]:stroke-ink', back && 'opacity-20')}
    >
      <path d={d} fill="none" stroke="transparent" strokeWidth={18} pointerEvents="stroke" />
      <path
        d={d} fill="none" strokeDasharray={DEPENDS_DASH} markerEnd={marker}
        className={cn('shown stroke-ink-soft', chosen ? 'stroke-[3]' : 'stroke-[1.5]')}
      />
    </g>
  );
}

/** Whether a chain's hop still has work in it: a step open now, or one still to be published. */
export const chainOpen = (edge: ChainEdge) =>
  edge.waiting.length > 0 || edge.steps.some((quest) => quest.status === 'Open' || quest.status === 'Taken');

/**
 * The asks' mark: a disc on the ring, a pill in the layers. It is a source and not a place, so it is
 * no button; the pointer on it lights its lines, and each line is the button.
 */
export function AsksMark({ x, y, r, count, width, onHover }: {
  x: number; y: number; r: number; count: number;
  /** A pill this wide, where the layers give it a card's room; a disc of radius `r` where absent. */
  width?: number;
  onHover: (on: boolean) => void;
}) {
  const { t } = useTranslation();
  return (
    <g role="img" aria-label={t('map.asksCard', { count })} onPointerEnter={() => onHover(true)} onPointerLeave={() => onHover(false)}>
      {width === undefined
        ? <circle cx={x} cy={y} r={r} className="fill-accent-soft stroke-accent stroke-[1.5]" />
        : <rect x={x - width / 2} y={y - r} width={width} height={2 * r} rx={r} className="fill-accent-soft stroke-accent stroke-[1.5]" />}
      <text x={x} y={y} textAnchor="middle" dominantBaseline="central" className="fill-accent text-meta font-semibold">
        {width === undefined ? t('map.asks') : t('map.asksCard', { count })}
      </text>
    </g>
  );
}

/** How many sessions a repository's word speaks for, when it is more than one (MAP4d): a number in any language. */
export const howMany = (sessions: number) => (sessions > 1 ? ` · ${sessions}` : '');

/** A line's name, and whether a session is on one of its quests now, since the ring is not words (MAP4d). */
export const lineLabel = (t: (key: string) => string, label: string, live: Live) =>
  (live ? `${label}${t(`map.lineLive.${live}`)}` : label);

/**
 * A line's count, on the line, in the line's own state; ringed as a node is while a session is on one
 * of its quests (MAP4d), in the hue the rail gives that session, and dashed so it is never hue alone.
 */
export function Count({ x, y, open, count, live = null }: { x: number; y: number; open: boolean; count: number; live?: Live }) {
  return (
    <>
      {live && (
        <circle
          cx={x} cy={y} r={16} fill="none" strokeDasharray="3 3"
          className={cn('stroke-2', live === 'parked' ? 'stroke-st-open' : 'stroke-st-taken')}
        />
      )}
      <circle cx={x} cy={y} r={11} className={cn('fill-page stroke-[1.5]', open ? 'stroke-accent' : 'stroke-line-strong')} />
      <text x={x} y={y} textAnchor="middle" dominantBaseline="central" className="fill-ink font-mono text-meta tabular-nums">
        {count}
      </text>
    </>
  );
}


/**
 * A line from one node's edge to the other's, bent so the two directions never overlap. A chain's
 * hop bends further than a quest does, so the two kinds between one pair stand apart.
 */
function questCurve(a: { x: number; y: number }, b: { x: number; y: number }, bend = 36, from = RADIUS) {
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
 * What follows the drawing down to the window's foot: the legend's two lines and the off-map count,
 * the card's padding, the page's foot and the status bar. The ring grows into the rest.
 */
const BELOW = 140;

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

/**
 * The workspace map, drawn (MAP2). Presentational: everything arrives as props, so every state is
 * reachable in a test without a service (the rule `presentational.test.ts` holds).
 *
 * @remarks
 * **Status is never colour alone** (D41): an open quest edge is solid and accented, a closed one
 * faint, a shared finding dashed, and a repository with a session there is ringed AND says so in its
 * label — *working now*, or *waiting on you* for one parked on the person, in the waiting hue the
 * band and the rail give that fact (UX5 U1). Every node and edge is a button a keyboard can reach, and
 * its name says what it holds.
 *
 * **A choice is a toggle**, as its `aria-pressed` says: a second press releases it, and so does
 * Escape. What stands forward is what is in focus (a node under the pointer or the keyboard lights
 * its own lines, and a chosen line stands forward alone), and the rest steps back (UX5 U47).
 */
export function MapCanvas({ topology, selected, onSelect }: {
  topology: Topology;
  selected: MapSelection | null;
  /** A part chosen, or `null` when the choice is released. */
  onSelect: (selection: MapSelection | null) => void;
}) {
  const { t } = useTranslation();
  // The node the pointer or the keyboard is on, so its lines stand out and the rest step back.
  const [hovered, setHovered] = useState<string | null>(null);
  // Drawn one unit a pixel, so a name is the type scale's at every width (UX5 U44): the ring gives
  // way to a narrow card and grows with a wide one, as far as the window's height leaves it (U59),
  // and the frame holds what is drawn and no more.
  const box = useRef<HTMLDivElement>(null);
  const room = useWidth(box);
  const tall = useTall(box, BELOW);
  const words = { parked: t('map.parked'), working: t('map.working') };
  const at = ringAt(topology, fitRadius(topology, room, words, tall));
  const askCount = new Set(topology.asks.flatMap((edge) => edge.asks)).size;
  const frame = frameMap(topology, at, words);
  const lit: MapSelection | null = hovered !== null ? { kind: 'node', id: hovered } : selected;
  /** A line stands forward when nothing is in focus, when a node in focus is one of its ends, or when it IS the chosen line. */
  const forward = (line: MapSelection, ...ends: string[]) =>
    lit === null || (lit.kind === 'node' ? ends.includes(lit.id) : sameSelection(lit, line));

  const choose = (selection: MapSelection) => onSelect(sameSelection(selection, selected) ? null : selection);
  // Every handler that reads Enter or Escape asks first, so none acts on an input method's press (IME1).
  const press = (selection: MapSelection) => (event: KeyboardEvent) => {
    if (isComposing(event)) return;
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      choose(selection);
    }
  };
  const release = (event: KeyboardEvent) => {
    if (!isComposing(event) && event.key === 'Escape' && selected !== null) {
      event.preventDefault();
      onSelect(null);
    }
  };

  return (
    <div ref={box}>
      <svg
        viewBox={`${frame.x} ${frame.y} ${frame.width} ${frame.height}`}
        width={frame.width}
        role="group"
        aria-label={t('map.canvas')}
        onKeyDown={release}
        className="mx-auto block h-auto max-w-full"
      >
        <defs>
          {/* In the map's own units, not the line's: a head that grew with the line's width made the
              chosen line's arrow twice the size of the others (seen on the window). */}
          <marker id="map-arrow-open" viewBox="0 0 10 10" refX="8" refY="5" markerUnits="userSpaceOnUse" markerWidth="14" markerHeight="14" orient="auto-start-reverse">
            <path d="M 0 0 L 10 5 L 0 10 z" className="fill-accent" />
          </marker>
          <marker id="map-arrow-closed" viewBox="0 0 10 10" refX="8" refY="5" markerUnits="userSpaceOnUse" markerWidth="14" markerHeight="14" orient="auto-start-reverse">
            <path d="M 0 0 L 10 5 L 0 10 z" className="fill-line-strong" />
          </marker>
          {/* Open, not filled: a declaration points without carrying anything. */}
          <marker id="map-arrow-declared" viewBox="0 0 10 10" refX="8" refY="5" markerUnits="userSpaceOnUse" markerWidth="14" markerHeight="14" orient="auto-start-reverse">
            <path d="M 1 1 L 9 5 L 1 9" fill="none" className="stroke-ink-soft stroke-[1.5]" />
          </marker>
        </defs>

        {topology.depends.map((edge) => {
          const { d } = questCurve(at[edge.from]!, at[edge.to]!, DEPENDS_BEND);
          const line: MapSelection = { kind: 'depends', from: edge.from, to: edge.to };
          return (
            <DependsLine
              key={`d-${edge.from}-${edge.to}`} d={d} marker="url(#map-arrow-declared)"
              label={t('map.dependsLabel', { from: edge.from, to: edge.to })}
              chosen={sameSelection(selected, line)} back={!forward(line, edge.from, edge.to)}
              onChoose={() => choose(line)} onPress={press(line)}
            />
          );
        })}

        {topology.knowledge.map((edge) => {
          const a = at[edge.a]!;
          const b = at[edge.b]!;
          const line: MapSelection = { kind: 'knowledge', a: edge.a, b: edge.b };
          const chosen = sameSelection(selected, line);
          return (
            <g
              key={`k-${edge.a}-${edge.b}`}
              role="button"
              tabIndex={0}
              aria-label={t('map.knowledgeLabel', { a: edge.a, b: edge.b, count: edge.groups })}
              aria-pressed={chosen}
              onClick={() => choose(line)}
              onKeyDown={press(line)}
              className={cn(
                'cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>line.shown]:stroke-accent',
                !forward(line, edge.a, edge.b) && 'opacity-20',
              )}
            >
              {/* The same wide, invisible target the quest lines carry. */}
              <line x1={a.x} y1={a.y} x2={b.x} y2={b.y} stroke="transparent" strokeWidth={18} pointerEvents="stroke" />
              <line
                x1={a.x} y1={a.y} x2={b.x} y2={b.y}
                strokeDasharray="5 5"
                className={cn('shown stroke-ink-faint', chosen ? 'stroke-[3]' : 'stroke-[1.5]')}
              />
            </g>
          );
        })}

        {topology.asks.map((edge) => {
          const { d, middle } = questCurve(at[ASKS]!, at[edge.to]!, 0, ASKS_R);
          const line: MapSelection = { kind: 'asks', to: edge.to };
          const chosen = sameSelection(selected, line);
          const open = edge.open > 0;
          return (
            <g
              key={`a-${edge.to}`}
              role="button"
              tabIndex={0}
              aria-label={lineLabel(t, t('map.asksLabel', { to: edge.to, count: edge.quests.length }), edge.live)}
              aria-pressed={chosen}
              onClick={() => choose(line)}
              onKeyDown={press(line)}
              className={cn(
                'cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>path.shown]:stroke-ink',
                !forward(line, ASKS, edge.to) && 'opacity-20',
              )}
            >
              <path d={d} fill="none" stroke="transparent" strokeWidth={18} pointerEvents="stroke" />
              <path
                d={d}
                fill="none"
                strokeDasharray={ASKS_DASH}
                markerEnd={open ? 'url(#map-arrow-open)' : 'url(#map-arrow-closed)'}
                className={cn('shown', open ? 'stroke-accent' : 'stroke-line-strong', chosen ? 'stroke-[3.5]' : 'stroke-[1.5]')}
              />
              <Count x={middle.x} y={middle.y} open={open} count={edge.quests.length} live={edge.live} />
            </g>
          );
        })}

        {topology.chains.map((edge) => {
          const { d, middle } = questCurve(at[edge.from]!, at[edge.to]!, CHAIN_BEND);
          const line: MapSelection = { kind: 'chains', from: edge.from, to: edge.to };
          const chosen = sameSelection(selected, line);
          const open = chainOpen(edge);
          return (
            <g
              key={`c-${edge.from}-${edge.to}`}
              role="button"
              tabIndex={0}
              aria-label={lineLabel(t, t('map.chainsLabel', { from: edge.from, to: edge.to, count: edge.steps.length + edge.waiting.length }), edge.live)}
              aria-pressed={chosen}
              onClick={() => choose(line)}
              onKeyDown={press(line)}
              className={cn(
                'cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>path.shown]:stroke-ink',
                !forward(line, edge.from, edge.to) && 'opacity-20',
              )}
            >
              <path d={d} fill="none" stroke="transparent" strokeWidth={18} pointerEvents="stroke" />
              <path
                d={d}
                fill="none"
                strokeDasharray={CHAIN_DASH}
                strokeLinecap="round"
                markerEnd={open ? 'url(#map-arrow-open)' : 'url(#map-arrow-closed)'}
                className={cn('shown', open ? 'stroke-accent' : 'stroke-line-strong', chosen ? 'stroke-[3.5]' : 'stroke-2')}
              />
              <Count x={middle.x} y={middle.y} open={open} count={edge.steps.length + edge.waiting.length} live={edge.live} />
            </g>
          );
        })}

        {topology.quests.map((edge) => {
          const { d, middle } = questCurve(at[edge.from]!, at[edge.to]!);
          const line: MapSelection = { kind: 'quests', from: edge.from, to: edge.to };
          const chosen = sameSelection(selected, line);
          const open = edge.open > 0;
          return (
            <g
              key={`q-${edge.from}-${edge.to}`}
              role="button"
              tabIndex={0}
              aria-label={lineLabel(t, t('map.questsLabel', { from: edge.from, to: edge.to, count: edge.quests.length }), edge.live)}
              aria-pressed={chosen}
              onClick={() => choose(line)}
              onKeyDown={press(line)}
              className={cn(
                'cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>path]:stroke-ink',
                !forward(line, edge.from, edge.to) && 'opacity-20',
              )}
            >
              {/* 🔴 The target a pointer actually has: a wide, invisible stroke along the line. A
                  two-pixel curve was a line nobody could press — measured by the browser gate, where
                  the map itself took the click the line should have. */}
              <path d={d} fill="none" stroke="transparent" strokeWidth={18} pointerEvents="stroke" />
              <path
                d={d}
                fill="none"
                markerEnd={open ? 'url(#map-arrow-open)' : 'url(#map-arrow-closed)'}
                style={{ strokeWidth: (chosen ? 2 : 0) + 1.5 + Math.min(edge.quests.length, 6) * 0.5 }}
                className={open ? 'stroke-accent' : 'stroke-line-strong'}
              />
              <Count x={middle.x} y={middle.y} open={open} count={edge.quests.length} live={edge.live} />
            </g>
          );
        })}

        {at[ASKS] && (
          <AsksMark
            x={at[ASKS].x} y={at[ASKS].y} r={ASKS_R} count={askCount}
            onHover={(on) => setHovered(on ? ASKS : null)}
          />
        )}

        {topology.nodes.map((node) => {
          const { x, y } = at[node.id]!;
          const self: MapSelection = { kind: 'node', id: node.id };
          const chosen = sameSelection(selected, self);
          const label = placeLabel(x, y);
          // Parked leads: it is the one a person acts on, and a repository may hold one of each.
          const there = node.parked ? 'parked' : node.working ? 'working' : null;
          return (
            <g
              key={node.id}
              role="button"
              tabIndex={0}
              aria-label={t(
                there === 'parked' ? 'map.nodeLabelParked' : there === 'working' ? 'map.nodeLabelWorking' : 'map.nodeLabel',
                { repository: node.id, count: node.open })}
              aria-pressed={chosen}
              onClick={() => choose(self)}
              onKeyDown={press(self)}
              onPointerEnter={() => setHovered(node.id)}
              onPointerLeave={() => setHovered(null)}
              onFocus={() => setHovered(node.id)}
              onBlur={() => setHovered(null)}
              className="cursor-pointer outline-none [&:focus-visible>circle.body]:stroke-ink"
            >
              {there && (
                <circle
                  cx={x} cy={y} r={RADIUS + 6} fill="none" strokeDasharray="3 3"
                  className={cn('stroke-2', there === 'parked' ? 'stroke-st-open' : 'stroke-st-taken')}
                />
              )}
              <circle
                cx={x} cy={y} r={RADIUS}
                className={cn('body fill-raised', chosen ? 'stroke-accent stroke-[3]' : 'stroke-line-strong stroke-[1.5]')}
              />
              <text x={x} y={y} textAnchor="middle" dominantBaseline="central" className="fill-ink font-mono text-small tabular-nums">
                {node.open}
              </text>
              <text x={label.x} y={label.y} textAnchor={label.anchor} className="fill-ink text-small font-semibold">
                {node.id}
              </text>
              {there && (
                <text
                  x={label.x} y={label.y + label.next} textAnchor={label.anchor}
                  className={cn('text-meta', there === 'parked' ? 'fill-ink-open' : 'fill-ink-soft')}
                >
                  {t(there === 'parked' ? 'map.parked' : 'map.working')}{howMany(node.sessions)}
                </text>
              )}
            </g>
          );
        })}
      </svg>
    </div>
  );
}
