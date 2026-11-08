import { type CSSProperties, memo } from 'react';
import { cn } from '../../lib/cn';

// One node and one edge of a positioned world, each held to primitive props so that a frame which changes only the view's scale or
// the mounted set draws again just what it changed: a hundred cards redrawn on every zoom step were the canvas's whole frame budget.

export type WorldNodeProps = {
  id: string;
  title: string;
  x: number; y: number; w: number; h: number;
  /** Its place in the whole, said with `aria-posinset`. */
  at: number;
  total: number;
  /** `card` for a closed group of an overview, else the tier it is drawn at. */
  tier: string;
  /** A chip or a dot-sized card: less padding and the title only where it fits. */
  dense: boolean;
  fits: boolean;
  /** The scale its counter-scaled words follow; 1 where its words stand at a fixed size whatever the zoom. */
  scale: number;
  selected: boolean;
  dim: boolean;
  tab: boolean;
  sub: string | undefined;
  label: string | undefined;
  /** The group a card opens, or null for a node, which is selected. */
  opens: string | null;
  onPress: (id: string) => void;
  onOpen: (group: string) => void;
};

export const WorldNode = memo(function WorldNode({
  id, title, x, y, w, h, at, total, tier, dense, fits, scale, selected, dim, tab, sub, label, opens, onPress, onOpen,
}: WorldNodeProps) {
  const card = opens !== null;
  const word: CSSProperties = { fontSize: 11 / scale, lineHeight: 1.2 };
  return (
    <div role="listitem" aria-setsize={total} aria-posinset={at} className="absolute" style={{ left: x, top: y, width: w, height: h }}>
      <button
        type="button"
        data-flow-node={id}
        data-tier={tier}
        data-selected={selected ? '' : undefined}
        data-dim={dim ? '' : undefined}
        tabIndex={tab ? 0 : -1}
        aria-pressed={card ? undefined : selected}
        aria-label={label ?? title}
        onClick={() => (card ? onOpen(opens) : onPress(id))}
        className={cn(
          'block h-full w-full overflow-hidden rounded-card border bg-raised text-left text-ink',
          selected ? 'border-accent' : 'border-line',
          dim && 'opacity-40',
          'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
          dense ? 'px-1 py-0.5' : 'px-2 py-1.5',
        )}
      >
        {card ? (
          <>
            <span className="block truncate font-medium" style={{ fontSize: 14 / scale }}>{title}</span>
            <span className="block truncate text-ink-soft" style={{ fontSize: 12 / scale }}>{sub}</span>
          </>
        ) : dense ? (fits ? <span className="block truncate" style={word}>{title}</span> : null)
          : <span className="line-clamp-2 text-small">{title}</span>}
      </button>
    </div>
  );
});

export const WorldEdge = memo(function WorldEdge({ id, d, lit, marker, stroke }: {
  id: string; d: string; lit: boolean; marker: string; stroke: 'non-scaling-stroke' | undefined;
}) {
  return (
    <path data-flow-edge={id} d={d} fill="none" stroke="currentColor" strokeWidth={1} vectorEffect={stroke} markerEnd={marker}
      className={lit ? 'text-accent' : undefined} />
  );
});
