import { type CSSProperties, type FocusEvent, type KeyboardEvent, useId } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { NODE_W, type PlacedGroup } from './layoutScene';
import { GROUP } from './sceneModel';
import type { Tier } from './tiers';
import type { Box, Size } from './viewMath';

// The drawing of a positioned world (CANVAS1d; the canvas design §4): what `PositionedCanvas` decided to mount, drawn at its tier. Nodes
// and group frames are absolutely placed in world units; the paths are one SVG in the same units; a dot is the canvas layer's, not here.
// A molecule: it holds no state and reads no data.

/** One node to draw: its id, title, place in the whole (`aria-posinset`) and what the find and the selection say of it. */
export type DrawnNode = {
  id: string; title: string; box: Box; at: number; selected: boolean; dim: boolean;
  /** A group's card says its count beneath its title, and a reader its whole sentence. */
  sub?: string;
  label?: string;
};
export type DrawnPath = { id: string; d: string; lit: boolean };
export type DrawnAggregate = { id: string; d: string; mid: { x: number; y: number }; count: number; words: string; width: number; counted: boolean };
/** A group's frame: the group itself drawn open, with its nodes inside (a closed group of an overview is a node, a card). */
export type DrawnGroup = PlacedGroup & { opened: boolean };

/** The path from the foot of one box to the head of the next, in world units. */
export function edgePath(a: Box, b: Box): string {
  const [x1, y1, x2, y2] = [a.x + a.w / 2, a.y + a.h, b.x + b.w / 2, b.y];
  const reach = Math.max(20, Math.abs(y2 - y1) / 2);
  return `M${x1} ${y1}C${x1} ${y1 + reach} ${x2} ${y2 - reach} ${x2} ${y2}`;
}

/** Where a ray from a box's middle toward a point leaves the box. */
function leaves(box: Box, toward: { x: number; y: number }): { x: number; y: number } {
  const [cx, cy] = [box.x + box.w / 2, box.y + box.h / 2];
  const [dx, dy] = [toward.x - cx, toward.y - cy];
  if (dx === 0 && dy === 0) return { x: cx, y: cy };
  const scale = Math.min(dx === 0 ? Infinity : box.w / 2 / Math.abs(dx), dy === 0 ? Infinity : box.h / 2 / Math.abs(dy));
  return { x: cx + dx * scale, y: cy + dy * scale };
}

/**
 * A straight line between two group boxes, moved aside by `slot` (each direction and kind its own, so a reciprocal pair is two lines,
 * trap 16), with the middle where its count stands.
 */
export function aggregateLine(a: Box, b: Box, slot: number): { d: string; mid: { x: number; y: number } } {
  const [ac, bc] = [{ x: a.x + a.w / 2, y: a.y + a.h / 2 }, { x: b.x + b.w / 2, y: b.y + b.h / 2 }];
  const [dx, dy] = [bc.x - ac.x, bc.y - ac.y];
  const length = Math.hypot(dx, dy) || 1;
  const [nx, ny] = [-dy / length, dx / length];
  const [from, to] = [leaves(a, bc), leaves(b, ac)];
  const [ox, oy] = [nx * slot, ny * slot];
  return { d: `M${from.x + ox} ${from.y + oy}L${to.x + ox} ${to.y + oy}`, mid: { x: (from.x + to.x) / 2 + ox, y: (from.y + to.y) / 2 + oy } };
}

export function PositionedWorld({
  size, k, tier, label, total, nodes, groups, paths, aggregates, active, onPress, onOpen, onFold, onKeyDown, onFocus,
}: {
  size: Size;
  /** How many items the whole holds, said on each mounted one with its place (`aria-setsize`, `aria-posinset`). */
  total: number;
  k: number;
  tier: Tier;
  label: string;
  nodes: readonly DrawnNode[];
  groups: readonly DrawnGroup[];
  paths: readonly DrawnPath[];
  aggregates: readonly DrawnAggregate[];
  /** The node that holds the world's one Tab stop. */
  active: string | null;
  onPress: (id: string) => void;
  onOpen: (group: string) => void;
  onFold: () => void;
  onKeyDown: (event: KeyboardEvent<HTMLDivElement>) => void;
  onFocus: (event: FocusEvent<HTMLDivElement>) => void;
}) {
  const { t } = useTranslation();
  const arrow = useId();
  // The type scale's size counter-scaled, so a chip's word is as large on screen as `text-meta` whatever the zoom.
  const word: CSSProperties = { fontSize: 11 / k, lineHeight: 1.2 };
  const stroke = k < 1 ? ('non-scaling-stroke' as const) : undefined;
  return (
    <div role="list" aria-label={label} className="relative" style={{ width: size.w, height: size.h }} onKeyDown={onKeyDown} onFocus={onFocus}>
      {groups.map((group) => (
        <div key={group.id} data-canvas-group={group.id} className="absolute rounded-card border border-line bg-raised/40"
          style={{ left: group.x, top: group.y, width: group.w, height: group.h }}>
          <div className="flex items-start justify-between gap-2 px-3 pt-2 text-ink-soft" style={word}>
            <span className="truncate font-medium">{group.title}</span>
            {group.opened && (
              <button type="button" onClick={onFold} className="shrink-0 rounded-control px-2 text-ink-soft hover:text-ink focus-visible:outline focus-visible:outline-2 focus-visible:outline-accent">
                {t('canvas.foldAgain')}
              </button>
            )}
          </div>
        </div>
      ))}
      <svg width={1} height={1} className="pointer-events-none absolute left-0 top-0 overflow-visible text-line-strong" aria-hidden="true">
        <defs>
          <marker id={arrow} viewBox="0 0 10 10" refX="9" refY="5" markerWidth="10" markerHeight="10" orient="auto" markerUnits="userSpaceOnUse">
            <path d="M0 1 L10 5 L0 9 z" fill="currentColor" />
          </marker>
        </defs>
        {paths.map((path) => (
          <path key={path.id} data-flow-edge={path.id} d={path.d} fill="none" stroke="currentColor" strokeWidth={1} vectorEffect={stroke}
            markerEnd={`url(#${arrow})`} className={path.lit ? 'text-accent' : undefined} />
        ))}
        {aggregates.map((line) => (
          <path key={line.id} data-canvas-aggregate={line.id} d={line.d} fill="none" stroke="currentColor" strokeWidth={line.width} vectorEffect={stroke}
            markerEnd={`url(#${arrow})`} />
        ))}
      </svg>
      {aggregates.filter((line) => line.counted).map((line) => (
        <span key={line.id} aria-hidden="true" data-canvas-aggregate-count={line.id}
          className="pointer-events-none absolute -translate-x-1/2 -translate-y-1/2 rounded-control border border-line bg-page px-1 font-mono text-ink-soft"
          style={{ left: line.mid.x, top: line.mid.y, ...word }}>
          {line.count}
        </span>
      ))}
      {nodes.map((node) => {
        const card = node.id.startsWith(GROUP);
        const fits = (node.title.length * 6.05) / k <= NODE_W - 8;
        const dense = !card && tier !== 'full';
        return (
          <div key={node.id} role="listitem" aria-setsize={total} aria-posinset={node.at} className="absolute"
            style={{ left: node.box.x, top: node.box.y, width: node.box.w, height: node.box.h }}>
            <button
              type="button"
              data-flow-node={node.id}
              data-tier={card ? 'card' : tier}
              data-selected={node.selected ? '' : undefined}
              data-dim={node.dim ? '' : undefined}
              tabIndex={node.id === active ? 0 : -1}
              aria-pressed={card ? undefined : node.selected}
              aria-label={node.label ?? node.title}
              onClick={() => (card ? onOpen(node.id.slice(GROUP.length)) : onPress(node.id))}
              className={cn(
                'block h-full w-full overflow-hidden rounded-card border bg-raised text-left text-ink',
                node.selected ? 'border-accent' : 'border-line',
                node.dim && 'opacity-40',
                'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
                dense ? 'px-1 py-0.5' : 'px-2 py-1.5',
              )}
            >
              {card ? (
                <>
                  <span className="block truncate font-medium" style={{ fontSize: 14 / k }}>{node.title}</span>
                  <span className="block truncate text-ink-soft" style={{ fontSize: 12 / k }}>{node.sub}</span>
                </>
              ) : dense ? (fits ? <span className="block truncate" style={word}>{node.title}</span> : null)
                : <span className="line-clamp-2 text-small">{node.title}</span>}
            </button>
          </div>
        );
      })}
    </div>
  );
}
