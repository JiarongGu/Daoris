import { type KeyboardEvent, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { type Topology, layoutRing } from './topology';

/** What a person has chosen on the map: a repository, the quests one way, or a shared finding. */
export type MapSelection =
  | { kind: 'node'; id: string }
  | { kind: 'quests'; from: string; to: string }
  | { kind: 'knowledge'; a: string; b: string };

const SIZE = 600;
const RADIUS = 30;

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
 * The workspace map, drawn (MAP2). Presentational: everything arrives as props, so every state is
 * reachable in a test without a service (the rule `presentational.test.ts` holds).
 *
 * @remarks
 * **Status is never colour alone** (D41): an open quest edge is solid and accented, a closed one
 * faint, a shared finding dashed, and a repository with a session working is ringed AND says so in
 * its label. Every node and edge is a button a keyboard can reach, and its name says what it holds.
 */
export function MapCanvas({ topology, selected, onSelect }: {
  topology: Topology;
  selected: MapSelection | null;
  onSelect: (selection: MapSelection) => void;
}) {
  const { t } = useTranslation();
  // What the pointer is over, so its lines stand out and the rest step back.
  const [hovered, setHovered] = useState<string | null>(null);
  const at = layoutRing(topology.nodes.map((n) => n.id), SIZE);
  const focus = hovered ?? (selected?.kind === 'node' ? selected.id : null);
  const touches = (...ends: string[]) => focus === null || ends.includes(focus);

  const press = (selection: MapSelection) => (event: KeyboardEvent) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      onSelect(selection);
    }
  };

  /** A line from one node's edge to the other's, bent so the two directions never overlap. */
  const curve = (from: string, to: string) => {
    const a = at[from]!;
    const b = at[to]!;
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
  };

  return (
    <svg
      viewBox={`0 0 ${SIZE} ${SIZE}`}
      role="group"
      aria-label={t('map.canvas')}
      className="block h-auto w-full max-w-[40rem]"
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
        const chosen = selected?.kind === 'knowledge' && selected.a === edge.a && selected.b === edge.b;
        return (
          <g
            key={`k-${edge.a}-${edge.b}`}
            role="button"
            tabIndex={0}
            aria-label={t('map.knowledgeLabel', { a: edge.a, b: edge.b, count: edge.groups })}
            onClick={() => onSelect({ kind: 'knowledge', a: edge.a, b: edge.b })}
            onKeyDown={press({ kind: 'knowledge', a: edge.a, b: edge.b })}
            className={cn(
              'cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>line.shown]:stroke-accent',
              !touches(edge.a, edge.b) && 'opacity-20',
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
        const { d, middle } = curve(edge.from, edge.to);
        const chosen = selected?.kind === 'quests' && selected.from === edge.from && selected.to === edge.to;
        const open = edge.open > 0;
        return (
          <g
            key={`q-${edge.from}-${edge.to}`}
            role="button"
            tabIndex={0}
            aria-label={t('map.questsLabel', { from: edge.from, to: edge.to, count: edge.quests.length })}
            onClick={() => onSelect({ kind: 'quests', from: edge.from, to: edge.to })}
            onKeyDown={press({ kind: 'quests', from: edge.from, to: edge.to })}
            className={cn(
              'cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>path]:stroke-ink',
              !touches(edge.from, edge.to) && 'opacity-20',
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
        const chosen = selected?.kind === 'node' && selected.id === node.id;
        const label = placeLabel(x, y);
        return (
          <g
            key={node.id}
            role="button"
            tabIndex={0}
            aria-label={t(node.working ? 'map.nodeLabelWorking' : 'map.nodeLabel', { repository: node.id, count: node.open })}
            aria-pressed={chosen}
            onClick={() => onSelect({ kind: 'node', id: node.id })}
            onKeyDown={press({ kind: 'node', id: node.id })}
            onPointerEnter={() => setHovered(node.id)}
            onPointerLeave={() => setHovered(null)}
            className="cursor-pointer outline-none [&:focus-visible>circle.body]:stroke-ink"
          >
            {node.working && (
              <circle cx={x} cy={y} r={RADIUS + 6} fill="none" strokeDasharray="3 3" className="stroke-st-taken stroke-2" />
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
            {node.working && (
              <text x={label.x} y={label.y + label.next} textAnchor={label.anchor} className="fill-ink-soft text-meta">
                {t('map.working')}
              </text>
            )}
          </g>
        );
      })}
    </svg>
  );
}
