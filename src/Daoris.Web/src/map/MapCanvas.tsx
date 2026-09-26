import { type KeyboardEvent, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { type Topology, layoutRing } from './topology';
import { useWidth } from './useWidth';

/** What a person has chosen on the map: a repository, the quests one way, or a shared finding. */
export type MapSelection =
  | { kind: 'node'; id: string }
  | { kind: 'quests'; from: string; to: string }
  | { kind: 'knowledge'; a: string; b: string };

/** Whether two choices are the same part of the map. */
export function sameSelection(a: MapSelection | null, b: MapSelection | null): boolean {
  if (a === null || b === null) return a === b;
  if (a.kind === 'node' && b.kind === 'node') return a.id === b.id;
  if (a.kind === 'quests' && b.kind === 'quests') return a.from === b.from && a.to === b.to;
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

/** A rectangle in the map's units: what the drawing's `viewBox` frames. */
export type Frame = { x: number; y: number; width: number; height: number };

/**
 * How wide a line of text is drawn, estimated before it is drawn: a Chinese character is a whole em,
 * anything else about two thirds of one, which is a semibold Latin name's width with room to spare
 * (*game* measured 7.62 units a letter at 12px on the window). The frame holds what this says.
 */
export function textWidth(text: string, px: number): number {
  let width = 0;
  for (const char of text) width += /[⺀-鿿豈-﫿＀-￯　-〿]/.test(char) ? px : px * 0.64;
  return width;
}

/** A line from one node's edge to the other's, bent so the two directions never overlap. */
function questCurve(a: { x: number; y: number }, b: { x: number; y: number }) {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const length = Math.hypot(dx, dy) || 1;
  const [ux, uy] = [dx / length, dy / length];
  const start = { x: a.x + ux * RADIUS, y: a.y + uy * RADIUS };
  // Short of the working ring too, so the arrowhead is never drawn inside it.
  const end = { x: b.x - ux * (RADIUS + 10), y: b.y - uy * (RADIUS + 10) };
  // Perpendicular to the direction of travel: A→B and B→A bend to opposite sides by construction.
  const control = { x: (a.x + b.x) / 2 - uy * 36, y: (a.y + b.y) / 2 + ux * 36 };
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
    const word = node.parked ? words.parked : node.working ? words.working : null;
    const width = Math.max(textWidth(node.id, NAME_PX), word ? textWidth(word, WORD_PX) : 0);
    const from = label.anchor === 'start' ? label.x : label.anchor === 'end' ? label.x - width : label.x - width / 2;
    hold(from, label.y - NAME_PX, from + width, label.y + (word ? label.next : 0) + 4);
  }
  for (const edge of topology.quests) {
    const { middle } = questCurve(at[edge.from]!, at[edge.to]!);
    hold(middle.x - 12, middle.y - 12, middle.x + 12, middle.y + 12);
  }

  if (left === Infinity) return { x: 0, y: 0, width: SIZE, height: SIZE };
  const pad = 8;
  const [x, y] = [Math.floor(left - pad), Math.floor(top - pad)];
  return { x, y, width: Math.ceil(right + pad) - x, height: Math.ceil(bottom + pad) - y };
}

/**
 * The ring's radius for a card this wide: the full ring where it fits, and a smaller one where it
 * does not, so a narrow card draws the names at their size and the ring gives way (UX5 U44: at 500
 * the whole square shrank, and every name to about 8px). Never below what keeps neighbours apart, so
 * a large family keeps its spacing and the drawing shrinks as the last resort. Where nothing measures
 * the card, the full ring.
 */
export function fitRadius(
  topology: Topology, room: number | undefined, words: { parked: string; working: string },
): number {
  const count = topology.nodes.length;
  const floor = Math.max(LEAST, count > 1 ? SPACING / (2 * Math.sin(Math.PI / count)) : 0);
  const most = Math.max(FULL, floor);
  if (room === undefined) return most;
  const ids = topology.nodes.map((node) => node.id);
  for (let radius = most; radius > floor; radius -= 4) {
    if (frameMap(topology, layoutRing(ids, SIZE, radius), words).width <= room) return radius;
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
  // way to a narrow card, and the frame holds what is drawn and no more.
  const box = useRef<HTMLDivElement>(null);
  const room = useWidth(box);
  const words = { parked: t('map.parked'), working: t('map.working') };
  const at = layoutRing(topology.nodes.map((n) => n.id), SIZE, fitRadius(topology, room, words));
  const frame = frameMap(topology, at, words);
  const lit: MapSelection | null = hovered !== null ? { kind: 'node', id: hovered } : selected;
  /** A line stands forward when nothing is in focus, when a node in focus is one of its ends, or when it IS the chosen line. */
  const forward = (line: MapSelection, ...ends: string[]) =>
    lit === null || (lit.kind === 'node' ? ends.includes(lit.id) : sameSelection(lit, line));

  const choose = (selection: MapSelection) => onSelect(sameSelection(selection, selected) ? null : selection);
  const press = (selection: MapSelection) => (event: KeyboardEvent) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      choose(selection);
    }
  };
  const release = (event: KeyboardEvent) => {
    if (event.key === 'Escape' && selected !== null) {
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
        </defs>

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
              aria-label={t('map.questsLabel', { from: edge.from, to: edge.to, count: edge.quests.length })}
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
              <circle
                cx={middle.x} cy={middle.y} r={11}
                className={cn('fill-page stroke-[1.5]', open ? 'stroke-accent' : 'stroke-line-strong')}
              />
              <text
                x={middle.x} y={middle.y} textAnchor="middle" dominantBaseline="central"
                className="fill-ink font-mono text-meta tabular-nums"
              >
                {edge.quests.length}
              </text>
            </g>
          );
        })}

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
                  className={cn('text-meta', there === 'parked' ? 'fill-st-open' : 'fill-ink-soft')}
                >
                  {t(there === 'parked' ? 'map.parked' : 'map.working')}
                </text>
              )}
            </g>
          );
        })}
      </svg>
    </div>
  );
}
