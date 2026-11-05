import { type FocusEvent, type KeyboardEvent, type PointerEvent, type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { cn } from '../../lib/cn';
import { isComposing } from '../../lib/composing';
import { useRootRem, useWidth } from '../useWidth';
import { Canvas, CANVAS_FLOOR, type CanvasHandle, type CanvasOptions } from './Canvas';
import { CanvasFind } from './CanvasFind';
import { FIND_FROM } from './findNodes';
import { GoneNotice, useSelectionGone } from './GoneNotice';
import { ListToggle } from './ListToggle';
import { createWalk, type Stop, type WalkLine } from './suppliedWalk';
import { type Tier, tierAt } from './tiers';
import { useCanvasFind } from './useCanvasFind';
import { graphIdentity } from './viewMemory';
import type { Box, Size, View, ZoomBounds } from './viewMath';
import type { WorldRead } from './worldBoxes';

// A positioned world the feature supplies (CANVAS1e; D224, the canvas design §2.2): the feature's own frame, boxes and drawing over the
// canvas, which gives the viewport, the tiers, find with its list, the viewer's place, the minimap and one roving stop. Nodes are the
// feature's content in a button the canvas places; lines are the feature's SVG, each a stop the canvas walks. Display only: a press
// selects a node or a line, a second one releases it, and the canvas adds no act of its own.

/** The keys the walk takes. */
const WALKED = new Set(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End']);
const NO_STATICS: ReadonlySet<string> = new Set();

export type SuppliedWorld = {
  /** The world's extent, in the feature's own coordinates: the drawing is shifted so its top left is the world's origin. */
  frame: Box;
  /** Every card's box, in the feature's coordinates. */
  boxes: ReadonlyMap<string, Box>;
  /** The cards in reading order: Home and End walk it, and a reader meets it. */
  order: readonly string[];
  /** Every line with its two ends, in the order the walk cycles them. */
  lines: readonly WalkLine[];
  /** Cards that draw but are no stop (a source that is no place); the lines from one are reached from the other end. */
  statics?: ReadonlySet<string>;
  /** Frames drawn under the cards (a workspace's block), each with its title. */
  groups?: readonly { id: string; title: string; box: Box }[];
  groupOf?: (id: string) => string | null;
};

/** What a card is drawn with: the tier the zoom asks, whether it is chosen, and whether find or a focus puts it back. */
export type NodeState = { tier: Tier; selected: boolean; dim: boolean };

/** What the feature's lines are drawn with; `line(id)` is what a line's group spreads, so the canvas walks and presses it. */
export type EdgeContext = {
  tier: Tier;
  selected: string | null;
  /** The card the pointer or the focus is on. */
  hovered: string | null;
  line: (id: string) => { 'data-flow-line': string; role: 'button'; tabIndex: number; 'aria-pressed': boolean; onClick: () => void };
};

const stopOf = (target: EventTarget): Stop | null => {
  const element = (target as Element).closest?.('[data-flow-node], [data-flow-line]');
  if (!element) return null;
  const line = element.getAttribute('data-flow-line');
  return line !== null ? { line: true, id: line } : { line: false, id: element.getAttribute('data-flow-node')! };
};
const hoverOf = (target: EventTarget): string | null => {
  const element = (target as Element).closest?.('[data-flow-node], [data-flow-static]');
  return element ? element.getAttribute('data-flow-node') ?? element.getAttribute('data-flow-static') : null;
};

export function SuppliedCanvas({
  world, label, moves, fit, feature, storeKey, selected, onSelect, zoom, opening, nodeLabel, titleOf, frameClass, renderNode, renderEdges,
  tools, findLabel, findFrom = FIND_FROM, findItems, list,
}: {
  world: SuppliedWorld;
  /** Names the region and the world for a reader. */
  label: string;
  /** How to move it, and the feature's words for showing the whole. */
  moves: string;
  fit: string;
  /** The feature and the graph's key the viewer's place is kept under (`daoris.canvas.<feature>.<key>`). */
  feature: string;
  storeKey: string;
  /** The chosen card or line: a press selects it, a second press (or Escape, or the ground) releases it. */
  selected: string | null;
  onSelect: (id: string | null) => void;
  /** Within 25–250% for a map; `'off'` draws it fitted to the region's width, with no zoom. */
  zoom: ZoomBounds | 'off';
  /** Opens fitted, never below this scale. */
  opening?: number;
  nodeLabel: (id: string) => string;
  /** The words find reads a card by. */
  titleOf: (id: string) => string;
  /** The classes of a card's place, for its shape (the button takes its radius). */
  frameClass?: (id: string) => string | undefined;
  renderNode: (id: string, state: NodeState) => ReactNode;
  renderEdges: (context: EdgeContext) => ReactNode;
  /** The feature's own controls, set in the toolbar beside find and the size. */
  tools?: ReactNode;
  findLabel: string;
  findFrom?: number;
  /**
   * What find searches where the world folds items away: each with the card that stands for it now. A match without a card is revealed
   * once the feature, answering `onSelect`, has drawn one.
   */
  findItems?: readonly { id: string; title: string; stands?: string }[];
  /** The world as a list (`CanvasList`), the whole reading: given, *Show as list* is offered at every width. */
  list?: ReactNode;
}) {
  const frameRef = useRef<HTMLDivElement>(null);
  const pane = useRef<HTMLDivElement>(null);
  const handle = useRef<CanvasHandle>(null);
  const width = useWidth(frameRef);
  const rem = useRootRem();
  const wide = width !== undefined && width >= CANVAS_FLOOR * rem - 0.5;
  const statics = world.statics ?? NO_STATICS;
  const { frame } = world;

  const [tier, setTier] = useState<Tier>('full');
  const [active, setActive] = useState<Stop | null>(null);
  const [hovered, setHovered] = useState<string | null>(null);
  const { gone, onGone } = useSelectionGone(selected);
  const [listed, setListed] = useState(false);
  const onView = useCallback((view: View, _viewport: Size) => setTier(tierAt(view.k, rem)), [rem]);

  const stops = useMemo(() => world.order.filter((id) => !statics.has(id) && world.boxes.has(id)), [world, statics]);
  const walk = useMemo(() => createWalk({ order: world.order, boxes: world.boxes, lines: world.lines, statics }), [world, statics]);
  const known = (stop: Stop | null) => stop !== null && (stop.line ? world.lines.some((line) => line.id === stop.id) : stops.includes(stop.id));
  const at: Stop | null = known(active) ? active : stops[0] ? { line: false, id: stops[0] } : null;
  const isAt = (stop: Stop) => at !== null && at.line === stop.line && at.id === stop.id;

  /** A box in the world's own units: the feature's coordinates less the frame's origin. */
  const placed = useCallback((box: Box): Box => ({ x: box.x - frame.x, y: box.y - frame.y, w: box.w, h: box.h }), [frame.x, frame.y]);
  const press = (id: string) => onSelect(selected === id ? null : id);
  const focusStop = (stop: Stop) => {
    setActive(stop);
    const element = [...(pane.current?.querySelectorAll<HTMLElement>('[data-flow-node], [data-flow-line]') ?? [])]
      .find((each) => (stop.line ? each.getAttribute('data-flow-line') : each.getAttribute('data-flow-node')) === stop.id);
    element?.focus({ preventScroll: true });
  };

  // A match the world does not draw yet: shown when the feature's answer to its selection has put a card there.
  const awaited = useRef<string | null>(null);
  const reveal = (id: string) => {
    const box = world.boxes.get(id);
    onSelect(id);
    setActive({ line: false, id });
    if (box) handle.current?.showBox(placed(box)); else awaited.current = id;
  };
  const finding = useMemo(
    () => findItems ?? stops.map((id) => ({ id, title: titleOf(id) })),
    [findItems, stops, titleOf],
  );
  const find = useCanvasFind({ nodes: finding, handle, reveal });
  const identity = useMemo(() => graphIdentity([...world.order, ...world.lines.map((line) => line.id)]), [world]);

  // The drawing tells the canvas its world each time the supplied one changes, and when the region's width does (a fitted opening follows it).
  const shifted = useMemo(() => new Map([...world.boxes].map(([id, box]) => [id, placed(box)])), [world, placed]);
  const groupBoxes = useMemo(() => new Map((world.groups ?? []).map((group) => [group.id, placed(group.box)])), [world, placed]);
  useEffect(() => {
    if (!wide) return;
    const read: WorldRead = { k: 1, width: frame.w, height: frame.h, nodes: shifted, ways: new Map(), ports: new Map(), sections: [] };
    handle.current?.measured(read, groupBoxes);
  }, [wide, width, shifted, groupBoxes, frame.w, frame.h]);
  useEffect(() => {
    const box = awaited.current === null ? undefined : shifted.get(awaited.current);
    if (!box) return;
    awaited.current = null;
    handle.current?.showBox(box);
  }, [shifted]);
  // Find dims what does not match; a card standing for matches is not dimmed.
  const standing = useMemo(() => {
    if (find.matching === null) return null;
    const stands = new Map((findItems ?? []).map((item) => [item.id, item.stands ?? item.id]));
    return new Set([...find.matching].map((id) => stands.get(id) ?? id));
  }, [find.matching, findItems]);

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (isComposing(event)) return;
    if (event.key === 'Escape') {
      if (selected !== null) { event.preventDefault(); onSelect(null); }
      return;
    }
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    const stop = stopOf(event.target);
    if (!stop) return;
    // A card is a button and chooses itself on Enter and Space; a line is the feature's drawing, so the canvas presses it.
    if ((event.key === 'Enter' || event.key === ' ') && stop.line) { event.preventDefault(); press(stop.id); return; }
    if (!WALKED.has(event.key)) return;
    event.preventDefault();
    const next = walk.step(stop, event.key);
    if (next) focusStop(next);
  };
  const onFocus = (event: FocusEvent<HTMLDivElement>) => {
    const stop = stopOf(event.target);
    if (stop) setActive(stop);
    setHovered(hoverOf(event.target));
  };
  const onBlur = (event: FocusEvent<HTMLDivElement>) => {
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setHovered(null);
  };
  const onOver = (event: PointerEvent<HTMLDivElement>) => setHovered(hoverOf(event.target));

  const context: EdgeContext = {
    tier, selected, hovered,
    line: (id) => ({ 'data-flow-line': id, role: 'button', tabIndex: isAt({ line: true, id }) ? 0 : -1, 'aria-pressed': selected === id, onClick: () => press(id) }),
  };

  const options: CanvasOptions = {
    mode: 'display', zoom: zoom === 'off' ? { min: 1, max: 1 } : zoom, fixed: zoom === 'off', fit, moves, stop: true,
    ...(opening !== undefined ? { opening: { least: opening } } : {}),
  };
  const toolbar = (
    <>
      {(findItems?.length ?? stops.length) >= findFrom && (
        <CanvasFind
          label={findLabel} query={find.query} matches={find.matches} at={find.at}
          onQuery={find.onQuery} onStep={find.step} onClear={find.clear} onPick={find.pick}
          onDown={() => { if (find.shown && stops.includes(find.shown)) focusStop({ line: false, id: find.shown }); }}
        />
      )}
      {tools}
      {list !== undefined && <ListToggle listed={false} onToggle={() => setListed(true)} />}
    </>
  );

  if (listed && list !== undefined) {
    return (
      <div ref={frameRef} className="min-w-0">
        <div className="mb-2 flex flex-wrap items-center justify-end gap-x-1"><ListToggle listed onToggle={() => setListed(false)} /></div>
        {list}
        {gone && <GoneNotice />}
      </div>
    );
  }

  return (
    <div ref={frameRef} className="min-w-0">
      <Canvas
        ref={handle}
        active={wide}
        options={options}
        label={label}
        width={width}
        rem={rem}
        pane={pane}
        paneProps={{ 'data-flow-pane': '', 'data-placement': 'down' }}
        tools={toolbar}
        showTools
        entry={stops[0] ?? null}
        selected={selected}
        selection={{ many: false, onGround: () => onSelect(null), onBox: () => undefined }}
        remember={{
          feature, key: storeKey, identity, selected: selected !== null && world.boxes.has(selected) ? selected : null,
          ...(world.groupOf ? { groupOf: world.groupOf } : {}),
          onGone: () => { onGone(); onSelect(null); },
        }}
        minimap={{ boxes: [...shifted.values()] }}
        onView={onView}
      >
        <div
          role="group" aria-label={label} className="relative" style={{ width: frame.w, height: frame.h }}
          onKeyDown={onKeyDown} onFocus={onFocus} onBlur={onBlur} onPointerOver={onOver} onPointerLeave={() => setHovered(null)}
        >
          {(world.groups ?? []).map((group) => {
            const box = placed(group.box);
            return (
              <div key={group.id} data-canvas-group={group.id} className="absolute rounded-card border border-line bg-raised/40" style={{ left: box.x, top: box.y, width: box.w, height: box.h }}>
                <span className="block truncate px-3 pt-2 text-small font-medium text-ink-soft">{group.title}</span>
              </div>
            );
          })}
          <svg width={frame.w} height={frame.h} viewBox={`${frame.x} ${frame.y} ${frame.w} ${frame.h}`} focusable="false" className="pointer-events-none absolute left-0 top-0 overflow-visible">
            {renderEdges(context)}
          </svg>
          {world.order.map((id) => {
            const raw = world.boxes.get(id);
            if (!raw) return null;
            const box = placed(raw);
            const place = { left: box.x, top: box.y, width: box.w, height: box.h };
            const dim = standing !== null && !statics.has(id) && !standing.has(id);
            const state: NodeState = { tier, selected: selected === id, dim };
            if (statics.has(id)) {
              return <div key={id} role="img" aria-label={nodeLabel(id)} data-flow-static={id} className={cn('absolute', frameClass?.(id))} style={place}>{renderNode(id, state)}</div>;
            }
            return (
              <div key={id} className={cn('absolute', frameClass?.(id))} style={place}>
                <button
                  type="button"
                  data-flow-node={id}
                  data-tier={tier}
                  tabIndex={isAt({ line: false, id }) ? 0 : -1}
                  aria-pressed={selected === id}
                  aria-label={nodeLabel(id)}
                  onClick={() => press(id)}
                  className={cn(
                    'block h-full w-full cursor-pointer rounded-[inherit] text-left transition-opacity duration-(--speed)',
                    dim && 'opacity-25',
                    'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ink',
                  )}
                >
                  {renderNode(id, state)}
                </button>
              </div>
            );
          })}
        </div>
      </Canvas>
      {gone && <GoneNotice />}
    </div>
  );
}
