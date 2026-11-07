import { type FocusEvent, type KeyboardEvent, useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useRootRem, useWidth } from '../useWidth';
import { Canvas, CANVAS_FLOOR, type CanvasHandle, type CanvasOptions } from './Canvas';
import { CanvasFind } from './CanvasFind';
import { CanvasList, type ListGroup } from './CanvasList';
import { DotLayer } from './DotLayer';
import { FIND_FROM } from './findNodes';
import { GoneNotice, useSelectionGone } from './GoneNotice';
import { ListToggle } from './ListToggle';
import { useCanvasFind } from './useCanvasFind';
import { createLayoutClient, spawnLayoutWorker } from './layoutClient';
import type { Layout, Scene } from './layoutScene';
import { aggregateLine, type DrawnAggregate, type DrawnNode, type DrawnPath, edgePath, PositionedWorld } from './PositionedWorld';
import { buildModel, displayOf, edgesAmong, GROUP } from './sceneModel';
import { buildGrid, nearestWay, queryGrid, type Way } from './spatialGrid';
import { budgeted, mountedIds, PATH_BUDGET, tierAt, viewRect } from './tiers';
import { graphIdentity } from './viewMemory';
import type { Box, Size, View } from './viewMath';
import type { WorldRead } from './worldBoxes';

// A large positioned world in the canvas: the scene laid out inline or in a worker under its
// revision, the world drawn by tier, culled past 400 nodes, grouped worlds past 1,000 as an overview that opens one group at a time,
// with find, the minimap, the viewer's remembered place and the list a reader or a narrow window gets.

const WAYS: Record<string, Way> = { ArrowUp: 'up', ArrowDown: 'down', ArrowLeft: 'left', ArrowRight: 'right' };
const NONE: ReadonlySet<string> = new Set();
const NO_PATHS: ReadonlySet<number> = new Set();

/** The lines drawn with a count beside them: past this many in view the rest are lines alone. */
const COUNTED = 150;

const meetsRect = (box: Box, rect: Box) => box.x < rect.x + rect.w && rect.x < box.x + box.w && box.y < rect.y + rect.h && rect.y < box.y + box.h;

type Line = { id: string; d: string; mid: { x: number; y: number }; count: number; kind: string; width: number; a: Box; b: Box };

/** Every aggregate as a line between its groups, each direction and kind in a slot of its own (trap 16), the heaviest first. */
function placeAggregates(model: ReturnType<typeof buildModel>, display: ReturnType<typeof displayOf>): Line[] {
  const slots = new Map<string, number>();
  const lines: Line[] = [];
  for (const line of display.aggregates) {
    const [a, b] = [model.groupBoxes.get(line.from)!, model.groupBoxes.get(line.to)!];
    const pair = line.from < line.to ? `${line.from}\u0000${line.to}` : `${line.to}\u0000${line.from}`;
    const slot = (slots.get(pair) ?? 0) + 1;
    slots.set(pair, slot);
    lines.push({
      id: line.id, ...aggregateLine(a, b, slot * 8), count: line.count, kind: line.kind, width: Math.min(6, 1 + Math.log2(line.count) / 2), a, b,
    });
  }
  return lines.sort((x, y) => y.count - x.count);
}

/** The view the culling follows, snapped so that a pan of a few pixels remounts nothing. */
type Frame = { view: View; viewport: Size; key: string };

export function PositionedCanvas({ scene, label, feature, storeKey, version = '', selected, onSelect, moves, fit, findLabel }: {
  scene: Scene;
  /** Names the region and the list for a reader. */
  label: string;
  /** The feature and the graph's key the viewer's place is kept under (`daoris.canvas.<feature>.<key>`). */
  feature: string;
  storeKey: string;
  /** What else makes the graph the same graph (a fed commit): a view kept under another restores nothing. */
  version?: string;
  selected: string | null;
  onSelect: (id: string | null) => void;
  moves?: string;
  fit?: string;
  findLabel?: string;
}) {
  const { t } = useTranslation();
  const frameRef = useRef<HTMLDivElement>(null);
  const pane = useRef<HTMLDivElement>(null);
  const handle = useRef<CanvasHandle>(null);
  const width = useWidth(frameRef);
  const rem = useRootRem();
  const wide = width !== undefined && width >= CANVAS_FLOOR * rem - 0.5;

  // The drawing keeps the scene it is laid out for until the next layout arrives: a newer scene's answer replaces it, a stale one is dropped.
  const [held, setHeld] = useState<{ scene: Scene; layout: Layout } | null>(null);
  const client = useMemo(() => createLayoutClient(spawnLayoutWorker()), []);
  useEffect(() => () => client.dispose(), [client]);
  useEffect(() => {
    let live = true;
    void client.run(scene).then((laid) => { if (live && laid) setHeld({ scene, layout: laid }); });
    return () => { live = false; };
  }, [client, scene]);

  const model = useMemo(() => (held ? buildModel(held.scene, held.layout) : null), [held]);
  const shape = model?.scene ?? scene;
  const [opened, setOpened] = useState<string | null>(null);
  const display = useMemo(() => (model ? displayOf(model, opened) : null), [model, opened]);
  const grid = useMemo(() => (display ? buildGrid(display.boxes) : null), [display]);
  const identity = useMemo(() => graphIdentity(shape.nodes.map((node) => node.id), version), [shape, version]);

  const [listed, setListed] = useState(false);
  const { gone, onGone } = useSelectionGone(selected);
  const [active, setActive] = useState<string | null>(null);
  const [frame, setFrame] = useState<Frame | null>(null);
  const focusing = useRef<string | null>(null);
  const [focusTick, setFocusTick] = useState(0);

  // Find: every node, shown or not; stepping opens the group, selects, and shows the node at a readable scale, the focus kept in the field.
  const reveal = (id: string) => {
    if (!model) return;
    const group = model.groupOf.get(id);
    if (model.overview && group !== undefined && group !== opened) setOpened(group);
    onSelect(id);
    setActive(id);
    const box = model.nodeBoxes.get(id);
    if (box) handle.current?.showBox(box);
  };
  const { query, at, matches, matching, shown, onQuery, step, clear, pick } = useCanvasFind({ nodes: shape.nodes, handle, reveal });
  const focusNode = (id: string) => { focusing.current = id; setFocusTick((tick) => tick + 1); setActive(id); };

  const onView = useCallback((view: View, viewport: Size) => {
    const span = Math.max(viewport.w, viewport.h) / view.k / 4;
    const key = [Math.round(view.x / span), Math.round(view.y / span), tierAt(view.k, rem), Math.round(Math.log2(view.k) * 8), Math.round(viewport.w), Math.round(viewport.h)].join(':');
    setFrame((now) => (now?.key === key ? now : { view, viewport, key }));
  }, [rem]);

  // The drawing tells the canvas its world each time what is shown changes; a group opened is an anchored relayout.
  const wideNow = wide && !listed;
  useEffect(() => {
    if (!model || !display || !wideNow) return;
    const read: WorldRead = {
      k: 1, width: model.layout.width, height: model.layout.height, nodes: display.boxes, ways: new Map(), ports: new Map(), sections: [],
    };
    handle.current?.measured(read, model.groupBoxes);
  }, [model, display, wideNow]);

  const asked = frame ? tierAt(frame.view.k, rem) : 'full';
  const drawn = useMemo(() => {
    if (!model || !display || !grid || !frame) return null;
    const inView = queryGrid(grid, viewRect(frame.view, frame.viewport));
    const paths = edgesAmong(model, display, inView, PATH_BUDGET);
    const tier = budgeted(asked, inView.size, paths.size);
    const pinned = [active, selected, shown];
    const cards = display.ids.filter((id) => id.startsWith(GROUP));
    const near = mountedIds(grid, viewRect(frame.view, frame.viewport, 0.5), pinned);
    // A dot is the canvas layer's: only a card and the pinned stay in the DOM then.
    const mounted = tier === 'dot' ? new Set([...cards.filter((id) => near.has(id)), ...pinned.filter((id): id is string => id !== null && display.boxes.has(id))]) : near;
    return { tier, mounted, paths: tier === 'dot' ? NO_PATHS : edgesAmong(model, display, mounted, PATH_BUDGET) };
  }, [model, display, grid, frame, asked, active, selected, shown]);

  // A node reached by a key may not be mounted yet; once it is, it is focused without scrolling.
  useLayoutEffect(() => {
    const id = focusing.current;
    if (id === null) return;
    const element = [...(pane.current?.querySelectorAll<HTMLElement>('[data-flow-node]') ?? [])].find((each) => each.dataset.flowNode === id);
    if (element) { focusing.current = null; element.focus({ preventScroll: true }); }
  }, [focusTick, drawn]);

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (!display || !grid || event.altKey || event.ctrlKey || event.metaKey) return;
    const from = (event.target as HTMLElement).closest<HTMLElement>('[data-flow-node]')?.dataset.flowNode;
    if (!from) return;
    const way = WAYS[event.key];
    const to = way ? nearestWay(grid, from, way) : event.key === 'Home' ? display.ids[0] : event.key === 'End' ? display.ids.at(-1) : undefined;
    if (!to) return;
    event.preventDefault();
    focusNode(to);
  };
  const onFocus = (event: FocusEvent<HTMLDivElement>) => {
    const id = (event.target as HTMLElement).closest<HTMLElement>('[data-flow-node]')?.dataset.flowNode;
    if (id) setActive(id);
  };

  const lineGeometry = useMemo(() => (model && display ? placeAggregates(model, display) : []), [model, display]);
  const options: CanvasOptions = { mode: 'display', zoom: { min: 0.1, max: 2.5 }, fit: fit ?? t('canvas.fit'), moves: moves ?? t('canvas.moves') };
  const lines = useMemo<ListGroup[]>(() => {
    if (!model || model.layout.groups.length === 0) return [{ id: '', title: '', items: shape.nodes }];
    const byGroup = new Map<string, { id: string; title: string }[]>(model.layout.groups.map((group) => [group.id, []]));
    for (const node of shape.nodes) if (node.group !== undefined) byGroup.get(node.group)?.push(node);
    return model.layout.groups.map((group) => ({ id: group.id, title: group.title, items: byGroup.get(group.id)! }));
  }, [model, shape]);

  const finding = shape.nodes.length >= FIND_FROM;
  const tools = (
    <>
      {finding && (
        <CanvasFind
          label={findLabel ?? t('canvas.find')} query={query} matches={matches} at={at}
          onQuery={onQuery} onStep={step} onClear={clear} onPick={pick}
          onDown={() => { if (shown) focusNode(shown); }}
        />
      )}
      <ListToggle listed={listed} onToggle={() => setListed(!listed)} />
    </>
  );

  if (!model || !display || !grid) {
    return <div ref={frameRef} className="min-w-0"><p role="status" className="text-small text-ink-soft">{t('canvas.laying')}</p></div>;
  }
  if (!wide || listed) {
    return (
      <div ref={frameRef} className="min-w-0">
        {wide && <div className="mb-2 flex flex-wrap items-center justify-end gap-x-1">{tools}</div>}
        <CanvasList label={label} groups={lines} selected={selected} onPick={(id) => { onSelect(id); setActive(id); }} />
      </div>
    );
  }

  const tier = drawn?.tier ?? 'full';
  const k = frame?.view.k ?? 1;
  const nodes: DrawnNode[] = [];
  for (const id of drawn?.mounted ?? NONE) {
    const box = display.boxes.get(id);
    if (!box) continue;
    const card = id.startsWith(GROUP);
    const group = card ? model.groups.get(id.slice(GROUP.length)) : undefined;
    nodes.push({
      id, box, at: display.at.get(id)!, selected: id === selected, dim: matching !== null && !card && !matching.has(id),
      title: group ? group.title : shape.nodes[model.indexOf.get(id)!]!.title,
      ...(group ? { sub: t('canvas.items', { count: group.count }), label: t('canvas.groupCard', { title: group.title, count: group.count }) } : {}),
    });
  }
  const paths: DrawnPath[] = [];
  for (const index of drawn?.paths ?? NO_PATHS) {
    const edge = shape.edges[index]!;
    paths.push({ id: edge.id, d: edgePath(display.boxes.get(edge.from)!, display.boxes.get(edge.to)!), lit: edge.from === selected || edge.to === selected });
  }
  // The lines between groups in view, the heaviest first, to the path budget; only the heaviest carry their count.
  const aggregates: DrawnAggregate[] = [];
  if (frame) {
    const rect = viewRect(frame.view, frame.viewport, 0.5);
    for (const line of lineGeometry) {
      if (!meetsRect(line.a, rect) && !meetsRect(line.b, rect)) continue;
      if (aggregates.length >= PATH_BUDGET) break;
      aggregates.push({ id: line.id, d: line.d, mid: line.mid, count: line.count, words: line.kind, width: line.width, counted: aggregates.length < COUNTED });
    }
  }
  const frames = display.frames.map(({ group, opened: open }) => ({ ...group, opened: open }));

  return (
    <div ref={frameRef} className="min-w-0">
      <Canvas
        ref={handle}
        active
        options={options}
        label={label}
        width={width}
        rem={rem}
        pane={pane}
        paneProps={{ 'data-flow-pane': '', 'data-placement': 'down' }}
        tools={tools}
        showTools
        entry={display.ids[0] ?? null}
        selected={selected}
        selection={{ many: false, onGround: () => onSelect(null), onBox: () => undefined }}
        remember={{
          feature, key: storeKey, identity, selected,
          groupOf: (id) => (id.startsWith(GROUP) ? id.slice(GROUP.length) : model.groupOf.get(id) ?? null),
          onGone: () => { onGone(); onSelect(null); },
        }}
        minimap={{ boxes: model.overview ? [...model.groupBoxes.values()] : [...display.boxes.values()] }}
        overlay={tier === 'dot' ? (view, viewport) => <DotLayer grid={grid} view={view} viewport={viewport} skip={drawn?.mounted ?? NONE} /> : undefined}
        onView={onView}
      >
        <PositionedWorld
          size={{ w: model.layout.width, h: model.layout.height }}
          k={k} tier={tier} label={label} total={display.ids.length} nodes={nodes} groups={frames} paths={paths} aggregates={aggregates}
          active={active ?? selected ?? display.ids[0] ?? null}
          onPress={(id) => { onSelect(id); setActive(id); }}
          onOpen={(group) => { setOpened(group); }}
          onFold={() => setOpened(null)}
          onKeyDown={onKeyDown}
          onFocus={onFocus}
        />
      </Canvas>
      {gone && <GoneNotice />}
    </div>
  );
}
