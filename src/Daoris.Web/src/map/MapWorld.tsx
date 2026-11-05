import { type ReactNode, useCallback, useMemo, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { CanvasList } from '../design/canvas/CanvasList';
import { SuppliedCanvas, type NodeState } from '../design/canvas/SuppliedCanvas';
import { useTall, useWidth } from '../design/useWidth';
import { RING_MAX } from './layers';
import { layeredGeometry, ringGeometry, START_LEAST } from './mapLayout';
import { mapListGroups } from './mapList';
import { howMany, lineName, MapEdges, type MapSelection, sameSelection } from './MapLines';
import { fitRadius, placeLabel, RADIUS } from './ring';
import { ASKS, type MapNode, type Topology } from './topology';

/** Where nothing measures the region (a unit test): the width the layers were always laid out for. */
const FALLBACK_WIDTH = 900;
/** What follows the drawing down to the window's foot: the legend's two lines and the off-map count, the card's padding, the status bar. */
const BELOW = 140;
/** Zoom 25–250%: Ctrl+wheel and a pinch are continuous, and a name is the type scale's at 100%. */
const ZOOM = { min: 0.25, max: 2.5 } as const;

/** A repository on the ring: its count inside a circle, its name outside on the side facing away from the middle, the session said in words. */
function RingNode({ node, x, y, state, words }: { node: MapNode; x: number; y: number; state: NodeState; words: { parked: string; working: string } }) {
  // Parked leads: it is the one a person acts on, and a repository may hold one of each.
  const there = node.parked ? 'parked' : node.working ? 'working' : null;
  const label = placeLabel(x + RADIUS, y + RADIUS);
  const [left, top] = [label.x - x, label.y - y];
  const shift = label.anchor === 'middle' ? '-50%' : label.anchor === 'end' ? '-100%' : '0';
  return (
    <span className="relative block h-full w-full">
      {there && (
        <span
          aria-hidden="true" data-session={there}
          className={cn('pointer-events-none absolute -inset-[6px] rounded-full border-2 border-dashed', there === 'parked' ? 'border-st-open' : 'border-st-taken')}
        />
      )}
      <span className={cn(
        'flex h-full w-full items-center justify-center rounded-full border bg-raised font-mono text-small tabular-nums text-ink',
        state.selected ? 'border-[3px] border-accent' : 'border-[1.5px] border-line-strong',
      )}
      >
        {state.tier === 'dot' ? null : node.open}
      </span>
      {state.tier !== 'dot' && (
        <>
          <span className="pointer-events-none absolute whitespace-nowrap text-small font-semibold text-ink" style={{ left, top: top - 12, transform: `translateX(${shift})` }}>{node.id}</span>
          {there && (
            <span
              className={cn('pointer-events-none absolute whitespace-nowrap text-meta', there === 'parked' ? 'text-ink-open' : 'text-ink-soft')}
              style={{ left, top: top + label.next - 11, transform: `translateX(${shift})` }}
            >
              {words[there]}{howMany(node.sessions)}
            </span>
          )}
        </>
      )}
    </span>
  );
}

/** A repository in the layers: a card with its name, the session said under it, and its count at the end. */
function Card({ node, state, words }: { node: MapNode; state: NodeState; words: { parked: string; working: string } }) {
  const there = node.parked ? 'parked' : node.working ? 'working' : null;
  return (
    <span
      className={cn(
        'relative flex h-full w-full items-center justify-between gap-2 rounded-card border bg-raised px-3.5',
        state.selected ? 'border-[3px] border-accent' : 'border-[1.5px] border-line-strong',
      )}
    >
      {there && (
        <span
          aria-hidden="true" data-session={there}
          className={cn('pointer-events-none absolute -inset-[5px] rounded-[12px] border-2 border-dashed', there === 'parked' ? 'border-st-open' : 'border-st-taken')}
        />
      )}
      {state.tier !== 'dot' && (
        <>
          <span className="min-w-0 flex-1">
            <span className="block truncate text-small font-semibold text-ink">{node.id}</span>
            {there && <span className={cn('block truncate text-meta', there === 'parked' ? 'text-ink-open' : 'text-ink-soft')}>{words[there]}{howMany(node.sessions)}</span>}
          </span>
          <span className={cn('font-mono text-small tabular-nums', node.open > 0 ? 'text-ink' : 'text-ink-faint')}>{node.open}</span>
        </>
      )}
    </span>
  );
}

/**
 * The workspace map (MAP2, MAP4; CANVAS1e; D224): a ring below `RING_MAX` repositories, fitted and not zoomed, and layers above it that
 * pan and zoom, one `SuppliedCanvas` either way, so crossing twelve changes the layout and keeps the choice and the focus. Display
 * only: a press chooses a repository or a line, a second press, Escape or the ground releases it.
 *
 * @remarks
 * **Status is never colour alone** (D41): open lines solid and accented, closed ones faint, a shared finding dashed, a session there
 * ringed and said in words. Every card and line is a stop the canvas walks (→ from a card to its first line out, and on to the card
 * beyond), named for what it holds; what is in focus stands forward and the rest steps back (UX5 U47).
 */
export function MapWorld({ topology, selected, onSelect, tools, storeKey }: {
  topology: Topology;
  selected: MapSelection | null;
  /** A part chosen, or `null` when the choice is released. */
  onSelect: (selection: MapSelection | null) => void;
  /** The page's own controls for the map (its lines), set in the toolbar beside find and the size. */
  tools?: ReactNode;
  /** The graph's key the viewer's place is kept under: the workspace, or `all`. */
  storeKey: string;
}) {
  const { t } = useTranslation();
  const holder = useRef<HTMLDivElement>(null);
  const room = useWidth(holder);
  const tall = useTall(holder, BELOW);
  const words = useMemo(() => ({ parked: t('map.parked'), working: t('map.working') }), [t]);
  const geometry = useMemo(
    () => (topology.nodes.length <= RING_MAX
      ? ringGeometry(topology, fitRadius(topology, room, words, tall), words)
      : layeredGeometry(topology, (room ?? FALLBACK_WIDTH) / START_LEAST)),
    [topology, room, tall, words],
  );

  const nodes = useMemo(() => new Map(topology.nodes.map((node) => [node.id, node])), [topology]);
  const askCount = useMemo(() => new Set(topology.asks.flatMap((edge) => edge.asks)).size, [topology]);
  const lines = useMemo(() => new Map(geometry.lines.map((line) => [line.id, line])), [geometry]);
  // The canvas chooses by id; a node's is its name and a line's is its key.
  const chosen = selected === null ? null : selected.kind === 'node' ? selected.id : geometry.lines.find((line) => sameSelection(line.selection, selected))?.id ?? null;
  const choose = (id: string | null) => onSelect(id === null ? null : lines.get(id)?.selection ?? { kind: 'node', id });

  const nodeLabel = (id: string) => {
    if (id === ASKS) return t('map.asksCard', { count: askCount });
    const node = nodes.get(id)!;
    const there = node.parked ? 'parked' : node.working ? 'working' : null;
    return t(there === 'parked' ? 'map.nodeLabelParked' : there === 'working' ? 'map.nodeLabelWorking' : 'map.nodeLabel', { repository: id, count: node.open });
  };
  const titleOf = useCallback((id: string) => id, []);
  const ring = geometry.ring;
  const statics = geometry.world.statics;
  const listed = mapListGroups(geometry.world.order.filter((id) => !statics?.has(id)), geometry.lines, {
    repositories: t('map.listRepositories'), kind: (kind) => t(`map.kind.${kind}`), node: nodeLabel, line: (line) => lineName(t, line),
  });

  return (
    <div ref={holder}>
      <SuppliedCanvas
        world={geometry.world}
        label={t('map.canvas')} moves={t('map.panLabel')} fit={t('map.fit')} findLabel={t('map.find')} findFrom={RING_MAX + 1}
        feature="map" storeKey={storeKey}
        selected={chosen} onSelect={choose}
        zoom={ring ? 'off' : ZOOM} {...(ring ? {} : { opening: START_LEAST })}
        nodeLabel={nodeLabel} titleOf={titleOf}
        frameClass={(id) => (ring || id === ASKS ? 'rounded-full' : 'rounded-card')}
        renderNode={(id, state) => {
          if (id === ASKS) {
            return (
              <span className="flex h-full w-full items-center justify-center rounded-full border-[1.5px] border-accent bg-accent-soft text-meta font-semibold text-accent">
                {state.tier === 'dot' ? null : ring ? t('map.asks') : t('map.asksCard', { count: askCount })}
              </span>
            );
          }
          const node = nodes.get(id)!;
          const box = geometry.world.boxes.get(id)!;
          return ring ? <RingNode node={node} x={box.x} y={box.y} state={state} words={words} /> : <Card node={node} state={state} words={words} />;
        }}
        renderEdges={(context) => (
          <>
            {geometry.captions.map((caption) => (
              <text key={`${caption.x},${caption.y}`} x={caption.x} y={caption.y} className="fill-ink-faint text-meta">{t('map.loose')}</text>
            ))}
            <MapEdges lines={geometry.lines} context={context} />
          </>
        )}
        tools={tools}
        list={<CanvasList label={t('map.canvas')} groups={listed} selected={chosen} onPick={(id) => choose(id === chosen ? null : id)} />}
      />
    </div>
  );
}
