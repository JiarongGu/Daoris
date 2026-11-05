import { type ReactNode, useCallback, useMemo, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { SuppliedCanvas } from '../design/canvas/SuppliedCanvas';
import { useTall, useWidth } from '../design/useWidth';
import { RING_MAX } from './layers';
import { listLine, listNode } from './listWords';
import { layeredGeometry, ringGeometry, START_LEAST } from './mapLayout';
import { mapListGroups } from './mapList';
import { LayerCard, RingNode } from './MapCards';
import { MapEdges, type MapSelection, sameSelection } from './MapLines';
import { MapTree } from './MapTree';
import { fitRadius } from './ring';
import { ASKS, type Topology } from './topology';

/** Where nothing measures the region (a unit test): the width the layers were always laid out for. */
const FALLBACK_WIDTH = 900;
/** What follows the drawing down to the window's foot: the legend's two lines and the off-map count, the card's padding, the status bar. */
const BELOW = 140;
/** Zoom 25–250%: Ctrl+wheel and a pinch are continuous, and a name is the type scale's at 100%. */
const ZOOM = { min: 0.25, max: 2.5 } as const;

/**
 * The workspace map (MAP2, MAP4; CANVAS1e; D224): a ring below `RING_MAX` repositories, fitted and not zoomed, and layers above it that
 * pan and zoom, one `SuppliedCanvas` either way, so crossing twelve changes the layout and keeps the choice and the focus. Display
 * only: a press chooses a repository or a line, a second press, Escape or the ground releases it.
 *
 * @remarks
 * **Status is never colour alone** (D41): open lines solid and accented, closed ones faint, a shared finding dashed, a session there
 * ringed and said in words. Every card and line is a stop the canvas walks (→ from a card to its first line out, and on to the card
 * beyond), named for what it holds. **The choice stands forward** (MAPUX1b): its lines and its neighbours at full ink, the rest
 * stepped back; hovering or focusing a card lights its lines without choosing it.
 */
export function MapWorld({ topology, selected, onSelect, tools, storeKey, reveal, detail }: {
  topology: Topology;
  selected: MapSelection | null;
  /** A part chosen, or `null` when the choice is released. */
  onSelect: (selection: MapSelection | null) => void;
  /** The page's own controls for the map (its lines), set in the toolbar beside find and the size. */
  tools?: ReactNode;
  /** The graph's key the viewer's place is kept under: the workspace, or `all`. */
  storeKey: string;
  /** A repository to show and, with `focus`, to give the focus: the way back from its code map. */
  reveal?: { id: string; focus: boolean } | null;
  /** The choice's detail, where it opens in place under its row in the list (below the 36rem floor). */
  detail?: ReactNode;
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
  const chosen = selected === null ? null
    : selected.kind === 'node' ? (nodes.has(selected.id) ? selected.id : null)
      : geometry.lines.find((line) => sameSelection(line.selection, selected))?.id ?? null;
  const choose = (id: string | null) => onSelect(id === null ? null : lines.get(id)?.selection ?? { kind: 'node', id });
  // What stands forward with the choice: a repository and those it shares a line with, or a line and its two ends.
  const near = useMemo(() => {
    if (chosen === null) return null;
    const line = lines.get(chosen);
    if (line) return new Set([line.from, line.to]);
    const set = new Set([chosen]);
    for (const each of geometry.lines) {
      if (each.from === chosen) set.add(each.to);
      if (each.to === chosen) set.add(each.from);
    }
    return set;
  }, [chosen, lines, geometry]);

  const nodeLabel = (id: string) => {
    if (id === ASKS) return t('map.asksCard', { count: askCount });
    const node = nodes.get(id)!;
    const there = node.parked ? 'parked' : node.working ? 'working' : null;
    return t(there === 'parked' ? 'map.nodeLabelParked' : there === 'working' ? 'map.nodeLabelWorking' : 'map.nodeLabel', { repository: id, count: node.open });
  };
  const titleOf = useCallback((id: string) => id, []);
  const ring = geometry.ring;
  const statics = geometry.world.statics;
  const stops = useMemo(() => geometry.world.order.filter((id) => !statics?.has(id)), [geometry.world, statics]);
  const listed = mapListGroups(stops, geometry.lines, {
    repositories: t('map.listRepositories'), kind: (kind) => t(`map.kind.${kind}`),
    node: (id) => (id === ASKS ? nodeLabel(id) : listNode(t, nodes.get(id)!)), line: (line) => listLine(t, line),
  });
  // Find reaches a repository by its name, then by what it says it owns and accepts, which a result says (MAPUX1d).
  const findItems = useMemo(
    () => stops.map((id) => {
      const node = nodes.get(id);
      return {
        id, title: id,
        also: node ? [
          ...node.owns.map((text) => ({ text, said: t('map.find.owns', { text }) })),
          ...node.accepts.map((text) => ({ text, said: t('map.find.accepts', { text }) })),
        ] : [],
      };
    }),
    [stops, nodes, t],
  );

  return (
    <div ref={holder}>
      <SuppliedCanvas
        world={geometry.world}
        label={t('map.canvas')} moves={t('map.panLabel')} fit={t('map.fit')} findLabel={t('map.find')} findFrom={RING_MAX + 1}
        feature="map" storeKey={storeKey}
        selected={chosen} onSelect={choose} reveal={reveal}
        zoom={ring ? 'off' : ZOOM} {...(ring ? {} : { opening: START_LEAST })}
        nodeLabel={nodeLabel} titleOf={titleOf} findItems={findItems}
        frameClass={(id) => (ring || id === ASKS ? 'rounded-full' : 'rounded-card')}
        renderNode={(id, state) => {
          const back = near !== null && !near.has(id);
          if (id === ASKS) {
            return (
              <span className={cn('flex h-full w-full items-center justify-center rounded-full border-[1.5px] border-accent bg-accent-soft text-meta font-semibold text-accent', back && 'opacity-40')}>
                {state.tier === 'dot' ? null : ring ? t('map.asks') : t('map.asksCard', { count: askCount })}
              </span>
            );
          }
          const node = nodes.get(id)!;
          const box = geometry.world.boxes.get(id)!;
          return ring
            ? <RingNode node={node} x={box.x} y={box.y} state={state} words={words} back={back} />
            : <LayerCard node={node} state={state} words={words} back={back} />;
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
        list={(
          <MapTree
            label={t('map.canvas')} groups={listed} selected={chosen} detail={detail}
            onPick={(id) => choose(id === chosen ? null : id)} onRelease={() => choose(null)}
          />
        )}
      />
    </div>
  );
}
