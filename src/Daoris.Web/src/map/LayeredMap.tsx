import {
  type KeyboardEvent, type PointerEvent, type ReactNode, type SetStateAction, useEffect, useMemo, useRef, useState,
} from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Icon, Menu, Tip } from '../ui';
import { CARD_H, type Card, layoutLayers, lineKey, type Point, ROUND } from './layers';
import {
  ASKS_DASH, AsksMark, CHAIN_DASH, chainOpen, Count, DependsLine, howMany, lineLabel, type MapSelection, sameSelection,
} from './MapCanvas';
import { ASKS, type Topology } from './topology';
import { useTall, useWidth } from './useWidth';

/** What follows the viewport down to the window's foot: the legend, the card's padding, the status bar. */
const BELOW = 150;
/** The viewport where nothing measures it (a unit test), and the least a short window gives it. */
const FALLBACK = { width: 900, height: 560 };
const LEAST_HEIGHT = 360;
/** A name is the type scale's at 1; the map never starts smaller than this, and pans instead. */
const START_LEAST = 0.8;
const ZOOM = { min: 0.25, max: 2.5, step: 1.25 } as const;
/** How far each direction of a pair asked both ways runs off the middle: past a count bubble's radius. */
const LANE = 12;
/** How far a chain's hop beside a quest between the same two runs off the middle: past the quest's lane. */
const CHAIN_LANE = 20;
/** The sizes the sizing menu offers by name, as a map tool's zoom menu does. */
const PRESETS = [0.5, 1, 2] as const;

/** One way to size the map: its name, a tick when it is the size now, and its key where it has one. */
function SizeItem({ label, keys, current = false, onSelect }: { label: string; keys?: string; current?: boolean; onSelect: () => void }) {
  return (
    <Menu.Item tick={current} onSelect={onSelect}>
      <span className="flex-1 truncate">{label}</span>
      {keys && <kbd className="font-mono text-meta text-ink-faint">{keys}</kbd>}
    </Menu.Item>
  );
}

type View = { x: number; y: number; scale: number };

/** A card's rectangle: left, top, right, bottom. */
const box = (card: Card) => ({
  left: card.x - card.width / 2, top: card.y - CARD_H / 2, right: card.x + card.width / 2, bottom: card.y + CARD_H / 2,
});

/**
 * A line from one card to another, through the waypoints the layout gave it: out of the side facing the
 * other card and into the side facing back, each stretch a curve that leaves and arrives level, so a
 * line threads the gaps between cards. Two cards of one column (only a shared finding, since an asker
 * always stands left of whom it asks) bow round the left, where no quest runs — found on the first real
 * workspace, where five such lines braided through the quests between two columns.
 */
function link(a: Card, b: Card, ways: Point[] = [], lane = 0) {
  const [p, q] = [box(a), box(b)];
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
    // The count: on the middle waypoint, or halfway along the middle stretch, where a level-ended
    // curve passes through the midpoint of its ends.
    const middle = ways.length % 2 === 1
      ? ways[(ways.length - 1) / 2]!
      : { x: (points[ways.length / 2]!.x + points[ways.length / 2 + 1]!.x) / 2, y: (points[ways.length / 2]!.y + points[ways.length / 2 + 1]!.y) / 2 };
    return { d, mid: middle };
  }
  // A farther pair bows wider, and none past the room the layout keeps for it (a cubic reaches three
  // quarters of the way to its control points).
  const reach = Math.min(p.left, q.left) - Math.min(40 + Math.abs(a.y - b.y) * 0.12, ROUND);
  const [sx, sy, ex, ey] = [p.left, ay, q.left, by];
  return { d: `M ${sx} ${sy} C ${reach} ${sy} ${reach} ${ey} ${ex} ${ey}`, mid: { x: reach + 14, y: (sy + ey) / 2 } };
}

/**
 * The workspace map for a circle too big for a ring (MAP4): cards in layers by who asks whom
 * (`layoutLayers`), what nothing connects below, in a viewport that pans and zooms so a name stays the
 * type scale's however big the circle, and a search that finds a repository and chooses it.
 *
 * @remarks
 * **The ring's rules, kept** (design §1, UX5 U47): status never by colour alone (open lines solid and
 * accented, closed ones faint, a shared finding dashed, a session there outlined and said in words);
 * every card and line a button a keyboard reaches, named for what it holds; a choice is a toggle that a
 * second press or Escape releases; what is in focus stands forward and the rest steps back.
 *
 * **Moving about is VS Code's and a map's**: drag the empty ground to pan, the wheel to pan and
 * `Ctrl`+wheel to zoom about the pointer, `+` `-` `0` and the arrows on the viewport, and the size
 * itself, which opens a sizing menu, for a pointer that has no wheel. It opens at a readable size on the connected part, never
 * smaller than four fifths of the type scale; *fit* shows it all when the person wants the whole.
 */
export function LayeredMap({ topology, selected, onSelect, tools }: {
  topology: Topology;
  selected: MapSelection | null;
  onSelect: (selection: MapSelection | null) => void;
  /** The page's own controls for the map (its lines), set in this toolbar beside the size. */
  tools?: ReactNode;
}) {
  const { t } = useTranslation();
  const holder = useRef<HTMLDivElement>(null);
  const measuredWidth = useWidth(holder);
  const tall = useTall(holder, BELOW);
  const viewport = {
    width: measuredWidth ?? FALLBACK.width,
    height: tall === undefined ? FALLBACK.height : Math.max(LEAST_HEIGHT, tall),
  };
  // The grid below wraps inside what the viewport shows at the opening scale, so it reads downward.
  const layout = useMemo(() => layoutLayers(topology, viewport.width / START_LEAST), [topology, viewport.width]);
  const askCount = new Set(topology.asks.flatMap((edge) => edge.asks)).size;

  const fitted = (whole: boolean): View => {
    const { frame } = layout;
    const fit = Math.min(viewport.width / Math.max(1, frame.width), viewport.height / Math.max(1, frame.height));
    const scale = whole ? Math.min(1, fit) : Math.min(1, Math.max(START_LEAST, fit));
    const [w, h] = [viewport.width / scale, viewport.height / scale];
    // Centred where it fits; from the top left where it does not, which is where the layers begin.
    return {
      scale,
      x: frame.width <= w ? frame.x - (w - frame.width) / 2 : frame.x,
      y: frame.height <= h ? frame.y - (h - frame.height) / 2 : frame.y,
    };
  };
  const [view, setView] = useState<View>(() => fitted(false));
  // Whether the person has moved the view: until they do, it follows the drawing.
  const moved = useRef(false);
  const move = (next: SetStateAction<View>) => {
    moved.current = true;
    setView(next);
  };
  // A new circle, or a card that measured itself: open again at the readable size.
  const measured = `${viewport.width}x${viewport.height}:${topology.nodes.length}`;
  useEffect(() => {
    moved.current = false;
    setView(fitted(false));
  }, [measured]);
  // 🔴 The data arrives in parts, the shared findings after the repositories and quests, and each part
  // can move the frame. Fitted once, the view kept the first frame, and a shared finding's bow round
  // the left ran off the edge on the first real workspace. It follows the frame until the person moves it.
  const framed = [layout.frame.x, layout.frame.y, layout.frame.width, layout.frame.height].join(',');
  useEffect(() => {
    if (!moved.current) setView(fitted(false));
  }, [framed]);

  const zoomBy = (next: (scale: number) => number, at?: { x: number; y: number }) => move((was) => {
    const scale = Math.min(ZOOM.max, Math.max(ZOOM.min, next(was.scale)));
    const [w0, h0] = [viewport.width / was.scale, viewport.height / was.scale];
    const [w1, h1] = [viewport.width / scale, viewport.height / scale];
    // About the pointer, or the centre: the point under it stays under it.
    const [fx, fy] = at ? [at.x / viewport.width, at.y / viewport.height] : [0.5, 0.5];
    return { scale, x: was.x + (w0 - w1) * fx, y: was.y + (h0 - h1) * fy };
  });
  const zoomAt = (factor: number, at?: { x: number; y: number }) => zoomBy((scale) => scale * factor, at);
  const zoomTo = (scale: number) => zoomBy(() => scale);
  const pan = (dx: number, dy: number) => move((was) => ({ ...was, x: was.x + dx / was.scale, y: was.y + dy / was.scale }));

  // The wheel: pan, and zoom with Ctrl — a listener of its own, since a passive React one cannot stop the page scrolling.
  useEffect(() => {
    const element = holder.current;
    if (!element) return undefined;
    const onWheel = (event: WheelEvent) => {
      event.preventDefault();
      const rect = element.getBoundingClientRect();
      if (event.ctrlKey || event.metaKey) zoomAt(event.deltaY < 0 ? ZOOM.step : 1 / ZOOM.step, { x: event.clientX - rect.left, y: event.clientY - rect.top });
      else pan(event.deltaX, event.deltaY);
    };
    element.addEventListener('wheel', onWheel, { passive: false });
    return () => element.removeEventListener('wheel', onWheel);
  });

  const dragging = useRef<{ x: number; y: number } | null>(null);
  const onDown = (event: PointerEvent<SVGSVGElement>) => {
    // The empty ground pans; a card or a line is pressed.
    if ((event.target as Element).closest('[role="button"]')) return;
    dragging.current = { x: event.clientX, y: event.clientY };
    event.currentTarget.setPointerCapture?.(event.pointerId);
  };
  const onMove = (event: PointerEvent<SVGSVGElement>) => {
    if (!dragging.current) return;
    pan(dragging.current.x - event.clientX, dragging.current.y - event.clientY);
    dragging.current = { x: event.clientX, y: event.clientY };
  };
  const onUp = () => { dragging.current = null; };

  const [query, setQuery] = useState('');
  const wanted = query.trim().toLowerCase();
  const matches = wanted ? topology.nodes.map((node) => node.id).filter((id) => id.toLowerCase().includes(wanted)) : [];
  const centreOn = (id: string) => {
    const card = layout.at[id];
    if (!card) return;
    move((was) => ({ ...was, x: card.x - viewport.width / was.scale / 2, y: card.y - viewport.height / was.scale / 2 }));
  };

  const [hovered, setHovered] = useState<string | null>(null);
  const lit: MapSelection | null = hovered !== null ? { kind: 'node', id: hovered } : selected;
  const forward = (line: MapSelection, ...ends: string[]) =>
    lit === null || (lit.kind === 'node' ? ends.includes(lit.id) : sameSelection(lit, line));
  const choose = (selection: MapSelection) => onSelect(sameSelection(selection, selected) ? null : selection);
  const press = (selection: MapSelection) => (event: KeyboardEvent) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      choose(selection);
    }
  };
  const onKeys = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.target !== event.currentTarget && event.key !== 'Escape') return;
    const moves: Record<string, () => void> = {
      '+': () => zoomAt(ZOOM.step), '=': () => zoomAt(ZOOM.step), '-': () => zoomAt(1 / ZOOM.step),
      0: () => move(fitted(true)),
      ArrowLeft: () => pan(-60, 0), ArrowRight: () => pan(60, 0), ArrowUp: () => pan(0, -60), ArrowDown: () => pan(0, 60),
      Escape: () => { if (selected !== null) onSelect(null); },
    };
    const key = moves[event.key];
    if (!key) return;
    event.preventDefault();
    key();
  };

  const viewBox = `${view.x} ${view.y} ${viewport.width / view.scale} ${viewport.height / view.scale}`;
  const percent = Math.round(view.scale * 100);

  return (
    <div className="grid gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <label className="flex min-w-0 flex-1 items-center gap-2 rounded-control border border-line-strong bg-raised px-2.5 py-1">
          <Icon name="search" size={14} className="shrink-0 text-ink-faint" />
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && matches[0]) {
                event.preventDefault();
                onSelect({ kind: 'node', id: matches[0] });
                centreOn(matches[0]);
              } else if (event.key === 'Escape') setQuery('');
            }}
            placeholder={t('map.find')}
            aria-label={t('map.find')}
            className="min-w-0 flex-1 bg-transparent text-small text-ink outline-none placeholder:text-ink-faint"
          />
          {wanted && <span className="shrink-0 text-meta text-ink-faint">{t('map.matches', { count: matches.length })}</span>}
        </label>
        {/* The size, and every way to change it, behind the size itself — a map tool's zoom menu, where
            there were an arrow down, an arrow up and a corner that meant *fit*. */}
        {tools}
        <Menu.Root>
          <Tip content={t('map.sizing')}>
            <Menu.Trigger asChild>
              <button
                type="button"
                aria-label={t('map.size', { percent })}
                className={cn(
                  'h-7 min-w-12 shrink-0 rounded-control px-2 font-mono text-meta tabular-nums text-ink-soft',
                  'transition-colors duration-(--speed) hover:bg-raised hover:text-ink data-[state=open]:bg-raised data-[state=open]:text-ink',
                  'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
                )}
              >
                {percent}%
              </button>
            </Menu.Trigger>
          </Tip>
          <Menu.Content align="end" className="min-w-52">
            <SizeItem label={t('map.zoomIn')} keys="+" onSelect={() => zoomAt(ZOOM.step)} />
            <SizeItem label={t('map.zoomOut')} keys="-" onSelect={() => zoomAt(1 / ZOOM.step)} />
            <Menu.Separator />
            <SizeItem label={t('map.fit')} keys="0" onSelect={() => move(fitted(true))} />
            {PRESETS.map((scale) => (
              <SizeItem
                key={scale}
                label={`${scale * 100}%`}
                current={percent === scale * 100}
                onSelect={() => zoomTo(scale)}
              />
            ))}
          </Menu.Content>
        </Menu.Root>
      </div>

      <div
        ref={holder}
        tabIndex={0}
        role="region"
        aria-label={t('map.canvas')}
        aria-roledescription={t('map.panLabel')}
        onKeyDown={onKeys}
        className="relative overflow-hidden rounded-control border border-line bg-page outline-none focus-visible:outline focus-visible:outline-2 focus-visible:outline-accent"
        style={{ height: viewport.height }}
      >
        <svg
          viewBox={viewBox}
          width="100%"
          height="100%"
          role="group"
          aria-label={t('map.canvas')}
          onPointerDown={onDown}
          onPointerMove={onMove}
          onPointerUp={onUp}
          onPointerCancel={onUp}
          className="block cursor-grab touch-none select-none active:cursor-grabbing"
        >
          <defs>
            <marker id="layer-arrow-open" viewBox="0 0 10 10" refX="8" refY="5" markerUnits="userSpaceOnUse" markerWidth="14" markerHeight="14" orient="auto-start-reverse">
              <path d="M 0 0 L 10 5 L 0 10 z" className="fill-accent" />
            </marker>
            <marker id="layer-arrow-closed" viewBox="0 0 10 10" refX="8" refY="5" markerUnits="userSpaceOnUse" markerWidth="14" markerHeight="14" orient="auto-start-reverse">
              <path d="M 0 0 L 10 5 L 0 10 z" className="fill-line-strong" />
            </marker>
            <marker id="layer-arrow-declared" viewBox="0 0 10 10" refX="8" refY="5" markerUnits="userSpaceOnUse" markerWidth="14" markerHeight="14" orient="auto-start-reverse">
              <path d="M 1 1 L 9 5 L 1 9" fill="none" className="stroke-ink-soft stroke-[1.5]" />
            </marker>
          </defs>

          {topology.depends.map((edge) => {
            const [from, to] = [layout.at[edge.from]!, layout.at[edge.to]!];
            // Beside a quest or a chain between the same two, it runs on the far side of its direction's.
            const beside = [...topology.quests, ...topology.chains].some((other) =>
              (other.from === edge.from && other.to === edge.to) || (other.from === edge.to && other.to === edge.from));
            const lane = beside ? (from.x < to.x ? CHAIN_LANE : -CHAIN_LANE) : 0;
            const { d } = link(from, to, layout.routes[lineKey.depends(edge.from, edge.to)], lane);
            const line: MapSelection = { kind: 'depends', from: edge.from, to: edge.to };
            return (
              <DependsLine
                key={`d-${edge.from}-${edge.to}`} d={d} marker="url(#layer-arrow-declared)"
                label={t('map.dependsLabel', { from: edge.from, to: edge.to })}
                chosen={sameSelection(selected, line)} back={!forward(line, edge.from, edge.to)}
                onChoose={() => choose(line)} onPress={press(line)}
              />
            );
          })}

          {layout.caption !== null && (
            <text x={layout.caption.x} y={layout.caption.y} className="fill-ink-faint text-meta">{t('map.loose')}</text>
          )}

          {topology.knowledge.map((edge) => {
            const { d } = link(layout.at[edge.a]!, layout.at[edge.b]!, layout.routes[lineKey.knowledge(edge.a, edge.b)]);
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
                className={cn('cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>path.shown]:stroke-accent', !forward(line, edge.a, edge.b) && 'opacity-20')}
              >
                <path d={d} fill="none" stroke="transparent" strokeWidth={18} pointerEvents="stroke" />
                <path d={d} fill="none" strokeDasharray="5 5" className={cn('shown stroke-ink-faint', chosen ? 'stroke-[3]' : 'stroke-[1.5]')} />
              </g>
            );
          })}

          {topology.asks.map((edge) => {
            const { d, mid } = link(layout.at[ASKS]!, layout.at[edge.to]!, layout.routes[lineKey.asks(edge.to)]);
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
                className={cn('cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>path.shown]:stroke-ink', !forward(line, ASKS, edge.to) && 'opacity-20')}
              >
                <path d={d} fill="none" stroke="transparent" strokeWidth={18} pointerEvents="stroke" />
                <path
                  d={d}
                  fill="none"
                  strokeDasharray={ASKS_DASH}
                  markerEnd={open ? 'url(#layer-arrow-open)' : 'url(#layer-arrow-closed)'}
                  className={cn('shown', open ? 'stroke-accent' : 'stroke-line-strong', chosen ? 'stroke-[3.5]' : 'stroke-[1.5]')}
                />
                <Count x={mid.x} y={mid.y} open={open} count={edge.quests.length} live={edge.live} />
              </g>
            );
          })}

          {topology.chains.map((edge) => {
            const [from, to] = [layout.at[edge.from]!, layout.at[edge.to]!];
            // Beside a quest between the same two, a chain's hop runs further out on its direction's side.
            const beside = topology.quests.some((other) =>
              (other.from === edge.from && other.to === edge.to) || (other.from === edge.to && other.to === edge.from));
            const lane = beside ? (from.x < to.x ? -CHAIN_LANE : CHAIN_LANE) : 0;
            const { d, mid } = link(from, to, layout.routes[lineKey.chains(edge.from, edge.to)], lane);
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
                className={cn('cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>path.shown]:stroke-ink', !forward(line, edge.from, edge.to) && 'opacity-20')}
              >
                <path d={d} fill="none" stroke="transparent" strokeWidth={18} pointerEvents="stroke" />
                <path
                  d={d}
                  fill="none"
                  strokeDasharray={CHAIN_DASH}
                  strokeLinecap="round"
                  markerEnd={open ? 'url(#layer-arrow-open)' : 'url(#layer-arrow-closed)'}
                  className={cn('shown', open ? 'stroke-accent' : 'stroke-line-strong', chosen ? 'stroke-[3.5]' : 'stroke-2')}
                />
                <Count x={mid.x} y={mid.y} open={open} count={edge.steps.length + edge.waiting.length} live={edge.live} />
              </g>
            );
          })}

          {topology.quests.map((edge) => {
            const [from, to] = [layout.at[edge.from]!, layout.at[edge.to]!];
            // Asked both ways: each direction in a lane of its own, the rightward above and the other
            // below, so neither line nor count hides the other (found on the window: two counts stacked).
            const both = topology.quests.some((other) => other.from === edge.to && other.to === edge.from);
            const lane = both ? (from.x < to.x ? -LANE : LANE) : 0;
            const { d, mid } = link(from, to, layout.routes[lineKey.quests(edge.from, edge.to)], lane);
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
                className={cn('cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>path.shown]:stroke-ink', !forward(line, edge.from, edge.to) && 'opacity-20')}
              >
                <path d={d} fill="none" stroke="transparent" strokeWidth={18} pointerEvents="stroke" />
                <path
                  d={d}
                  fill="none"
                  markerEnd={open ? 'url(#layer-arrow-open)' : 'url(#layer-arrow-closed)'}
                  style={{ strokeWidth: (chosen ? 2 : 0) + 1.5 + Math.min(edge.quests.length, 6) * 0.5 }}
                  className={cn('shown', open ? 'stroke-accent' : 'stroke-line-strong')}
                />
                <Count x={mid.x} y={mid.y} open={open} count={edge.quests.length} live={edge.live} />
              </g>
            );
          })}

          {layout.at[ASKS] && (
            <AsksMark
              x={layout.at[ASKS].x} y={layout.at[ASKS].y} r={CARD_H / 2 - 6} width={layout.at[ASKS].width} count={askCount}
              onHover={(on) => setHovered(on ? ASKS : null)}
            />
          )}

          {topology.nodes.map((node) => {
            const card = layout.at[node.id]!;
            const { left, top, right } = box(card);
            const self: MapSelection = { kind: 'node', id: node.id };
            const chosen = sameSelection(selected, self);
            const there = node.parked ? 'parked' : node.working ? 'working' : null;
            const unmatched = wanted !== '' && !matches.includes(node.id);
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
                className={cn('cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>rect.body]:stroke-ink', unmatched && 'opacity-25')}
              >
                {there && (
                  <rect
                    x={left - 5} y={top - 5} width={card.width + 10} height={CARD_H + 10} rx={12} fill="none" strokeDasharray="3 3"
                    className={cn('stroke-2', there === 'parked' ? 'stroke-st-open' : 'stroke-st-taken')}
                  />
                )}
                <rect
                  x={left} y={top} width={card.width} height={CARD_H} rx={8}
                  className={cn('body fill-raised', chosen ? 'stroke-accent stroke-[3]' : 'stroke-line-strong stroke-[1.5]')}
                />
                <text x={left + 14} y={there ? card.y - 4 : card.y} dominantBaseline={there ? 'auto' : 'central'} className="fill-ink text-small font-semibold">
                  {node.id}
                </text>
                {there && (
                  <text x={left + 14} y={card.y + 14} className={cn('text-meta', there === 'parked' ? 'fill-st-open' : 'fill-ink-soft')}>
                    {t(there === 'parked' ? 'map.parked' : 'map.working')}{howMany(node.sessions)}
                  </text>
                )}
                <text x={right - 14} y={card.y} textAnchor="end" dominantBaseline="central" className={cn('font-mono text-small tabular-nums', node.open > 0 ? 'fill-ink' : 'fill-ink-faint')}>
                  {node.open}
                </text>
              </g>
            );
          })}
        </svg>
      </div>
    </div>
  );
}
