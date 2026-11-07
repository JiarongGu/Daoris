import { Fragment, type FocusEvent, type KeyboardEvent, type MouseEvent, type ReactNode, useCallback, useId, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { isComposing } from '../lib/composing';
import { store, stored } from '../lib/stored';
import { Canvas, CANVAS_FLOOR, type CanvasHandle } from './canvas/Canvas';
import { observeWorld, readWorld } from './canvas/worldBoxes';
import { type Fold, largeFolds, WHOLE_MOST } from './flowFolds';
import { type EdgeStyle, findStacks, FLOW_REM, layoutFlow, type Placement, placementFor, type Slot, type Stack } from './flowLayout';
import { Button } from './ui';
import { useRootRem, useWidth } from './useWidth';
import { type NodeContext, type FlowCanvasProps, type Ink } from './flowTypes';
import { rem, MarkChip, RailMark, describe, foldStacks, foldRanges, padding, DASHES, TRACK_MOST, trackFor, measure, same } from './flowParts';


/** The person's choice of the list over the drawing, remembered on this machine for every flow (§3.2). */
const LIST_KEY = 'daoris.flow.list';

/**
 * **A flowchart**: one process drawn from its record, laid out in
 * layers by `layoutFlow` and placed by the pane's own width, left to right, top to bottom, or as the rail, a list whose
 * edges are words. The same nodes are one list in reading order in every placement, so a reader and a keyboard follow
 * the list and never the drawing.
 *
 * @remarks
 * - **Placed by the measured width** (§3.2), never the window's: `placementFor` over what the layout needs. Where nothing
 *   measures (a unit test's DOM) it is the rail, and *Show as list* chooses the rail at any width, remembered.
 * - **The nodes are HTML, placed by CSS**: each rank a row (or a column), its node tracks one width, a waypoint slot
 *   beside them; text wraps in either language and nothing is drawn smaller to fit. The edges are one SVG hidden from a
 *   reader, drawn from the measured boxes after they are placed (`routeInk`), again on each resize; nothing drawn
 *   changes a box (§4.2).
 * - **A node** (§3.3): its glyph and title, its state's pill, who acts, the caller's lines, and its foot, one press per
 *   edge out that moves the focus to its target, so every branch has a door. The current node has a 2 px border and
 *   `aria-current`; one not reached is dashed, its words at the soft ink, never faint by opacity.
 * - **Keys** (§3.7): one Tab stop, the current node else the first, by a roving `tabindex`; ↓ ↑ Home End in reading order;
 *   → along the first edge out, ← back along the edge it came by else its first edge in; Enter opens its door; a key
 *   aimed at a control inside a node is the control's, and Escape gives the focus back to its node. The focus is kept by
 *   node id across a change of placement and a new layout. A key an input method is composing with is the method's.
 * - **Too large to show whole**: past 24 nodes, after the stacks, settled stretches fold and the flow is focused
 *   on the ranks around every current node, or with none current where it starts, ends or got to (`largeFolds`). A fold is a node
 *   with a press, every edge across its boundary drawn to it, merged by cause and by the node outside and counted; *Show whole*
 *   opens every fold and *Fold again* folds them back. What is open is page state, never remembered, and a fold the person opened
 *   stays open while the state moves; a node folded away gives its focus to its fold, and an opened fold gives it back.
 * - **Sections**: a caller's titles, each from a node's rank, the first heading the flow and each later one a divider
 *   before its rank; a flow in sections is never left to right.
 *
 * - **Ports and data**: the canvas draws the layout, the ink and the keys and a caller draws the nodes (`renderNode`) and
 *   the chips (`renderLabel`). A `when` edge with a `port` leaves the box of that port, which the caller's node draws; a `data` edge
 *   is read by neither the layout nor the folds, and is drawn only while a flow given `selected` has an end selected.
 * - **Editing**: an editor's ＋ on a way (`renderInsert`), placed as a label is and under its source in the rail, and pick
 *   mode (`pick`), in which the keys walk only the nodes that can take the way being chosen. What a press does is the caller's.
 *
 * - **The viewport**: a caller that opts in (`viewport`) has its drawing stand in the canvas (`canvas/Canvas.tsx`) from
 *   36rem of the pane, where a zoom and a pan transform it and lay nothing out again: its boxes are read in world units
 *   (`canvas/worldBoxes.ts`), its track is sized from the pane at 100%, and a focus walked takes `preventScroll`, the canvas revealing it.
 *   A rem is the root's as measured, so the window's zoom places a flow by what it needs.
 *
 * A molecule: the flow arrives worded and its doors go out.
 */
export function RankedFlow({
  label, nodes: given, edges: allEdges, entries: givenEntries, end, rail, sections, track: bounds, className, renderNode, renderLabel, selected,
  renderInsert, pick = null, viewport = 'none', selection = null, connect = null, onEdge,
}: FlowCanvasProps) {
  const { t } = useTranslation();
  // A data wire is no way on: the layout, the stacks and the folds never read it.
  const givenEdges = useMemo(() => allEdges.filter((edge) => edge.style !== 'data'), [allEdges]);
  const dataEdges = useMemo(() => allEdges.filter((edge) => edge.style === 'data'), [allEdges]);
  const showsData = selected !== undefined;
  const keysId = useId();
  const marks = useId().replace(/[^\w-]/g, '');
  const pane = useRef<HTMLDivElement>(null);
  const drawing = useRef<HTMLDivElement>(null);
  const canvas = useRef<CanvasHandle>(null);
  const width = useWidth(pane);
  const remPx = useRootRem();
  const px = (value: number) => value * remPx;
  const options = viewport === 'none' ? null : viewport;
  const [asList, setAsList] = useState(() => stored(LIST_KEY) === 'list');

  // The roving stop (below) and the node that has the focus, kept by id across a fold as across a placement.
  const [stop, setStop] = useState<string | null>(null);
  const focused = useRef<string | null>(null);

  // *Show whole* (§3.6): every fold opened, stacks among them, until *Fold again*. Page state, never remembered.
  const [whole, setWhole] = useState(false);

  // Stacks: three or more like nodes in a chain drawn as one fold, opened by its press. Which are open is
  // page state, never remembered. The stacks are found from the shape alone, as the layout is; the focus follows a fold that opens
  // to its first member, and one that closes to the fold.
  const stackKey = JSON.stringify([given.map((node) => [node.id, node.stack?.key ?? null]), givenEdges.map((edge) => [edge.id, edge.from, edge.to]), givenEntries]);
  const stacks = useMemo(() => findStacks({
    nodes: given.map((node) => (node.stack ? { id: node.id, stack: node.stack.key } : { id: node.id })), edges: givenEdges, entries: givenEntries,
  }), [stackKey]);
  const [opened, setOpened] = useState<ReadonlySet<string>>(() => new Set());
  const toggle = useCallback((stack: Stack, open: boolean) => {
    focused.current = open ? stack.members[0]! : stack.id;
    setStop(focused.current);
    setOpened((was) => {
      const next = new Set(was);
      if (open) next.add(stack.id);
      else next.delete(stack.id);
      return next;
    });
  }, []);
  const stacked = foldStacks(t, given, givenEdges, givenEntries, whole ? [] : stacks, opened, toggle);

  // Past 24 nodes: settled stretches, then the focus, which follow what is current and settled, so
  // they are kept by that and by the folds the person opened. A fold opens to the node that had the focus where it holds it.
  const [openedLarge, setOpenedLarge] = useState<ReadonlySet<string>>(() => new Set());
  const large = stacked.nodes.length > WHOLE_MOST;
  const largeKey = JSON.stringify([
    stacked.nodes.map((node) => [node.id, node.settled === true, node.current === true, node.reached ?? null]),
    stacked.edges.map((edge) => [edge.id, edge.from, edge.to, edge.style]), stacked.entries, [...openedLarge],
  ]);
  const folds = useMemo(
    () => (whole || !large ? [] : largeFolds({ nodes: stacked.nodes, edges: stacked.edges, entries: stacked.entries }, openedLarge)),
    [largeKey, whole],
  );
  const openFold = useCallback((fold: Fold) => {
    const had = focused.current;
    focused.current = had !== null && fold.members.includes(had) ? had : fold.members[0]!;
    setStop(focused.current);
    setOpenedLarge((was) => new Set([...was, fold.id]));
  }, []);
  const ranged = foldRanges(t, stacked, folds, openFold);
  const { nodes, edges, entries } = ranged;
  const notes = new Map([...stacked.notes, ...ranged.notes]);

  // Where a node given stands as drawn: itself, or the fold that holds it (§3.7: a node folded away gives its focus to its fold).
  const drawnOf = (id: string) => {
    const inStack = stacked.foldOf.get(id) ?? id;
    return ranged.foldOf.get(inStack) ?? inStack;
  };

  // The layout reads the shape alone, so a change of what is lit lays nothing out again (§3.1 step 5): it is kept by the
  // shape's key, not by the arrays a caller makes anew on each render. Folds past 24 nodes are the one exception (§3.6).
  const shapeKey = JSON.stringify([
    nodes.map((node) => [node.id, node.ports ?? null]), edges.map((edge) => [edge.id, edge.from, edge.to, edge.style, edge.port]), entries,
  ]);
  const layout = useMemo(
    () => layoutFlow({ nodes: nodes.map((node) => ({ id: node.id, ...(node.ports ? { ports: node.ports } : {}) })), edges, entries }),
    [shapeKey],
  );
  const least = bounds?.least ?? FLOW_REM.node;
  // The data wires a flow may show keep their lanes free at the right, selected or not, so a selection moves no node.
  const dataLanes = showsData ? dataEdges.filter((edge) => nodes.some((node) => node.id === edge.from) && nodes.some((node) => node.id === edge.to)).length : 0;
  const roomed = useMemo(() => ({ ...layout, lanes: layout.lanes + dataLanes }), [layout, dataLanes]);
  // From the canvas's floor a drawing the pane cannot hold is drawn top to bottom and pans; below it, today's placement.
  const roomy = options !== null && width !== undefined && width >= px(CANVAS_FLOOR) - 0.5;
  const placed = placementFor(roomed, width, false, remPx, { least, across: !sections || sections.length === 0 });
  const fits: Placement = roomy && placed === 'rail' && layout.ranks.length > 0 ? 'down' : placed;
  const placement: Placement = asList ? 'rail' : fits;
  const inViewport = roomy && placement !== 'rail';

  const byId = useMemo(() => new Map(nodes.map((node) => [node.id, node])), [nodes]);
  const backs = useMemo(() => new Set(layout.paths.filter((path) => path.back).flatMap((path) => path.edges)), [layout]);
  // The paths that carry an edge the record kept as taken, drawn heavier; the layout never reads it.
  const taken = useMemo(() => {
    const kept = new Set(edges.filter((edge) => edge.taken).map((edge) => edge.id));
    return new Set(layout.paths.filter((path) => path.edges.some((id) => kept.has(id))).map((path) => path.id));
  }, [layout, edges]);

  // Sections: the first heads the flow; each later one divides the drawing before the rank its node is drawn in, and a
  // reader hears its title at that node.
  const [head, ...later] = sections ?? [];
  const divided = new Map<string, string[]>();
  for (const section of later) {
    const at = drawnOf(section.from);
    if (byId.has(at)) divided.set(at, [...(divided.get(at) ?? []), section.title]);
  }
  const dividedAt = new Map<number, string[]>();
  for (const [id, titles] of divided) {
    const rank = layout.rank[id];
    if (rank !== undefined) dividedAt.set(rank, [...(dividedAt.get(rank) ?? []), ...titles]);
  }

  // The roving stop: the node the person last stood on, else the current one, else the first in reading order; in pick mode, a node that
  // can take the way.
  const roving = stop !== null && byId.has(drawnOf(stop))
    ? drawnOf(stop)
    : layout.order.find((id) => byId.get(id)?.current) ?? layout.order[0] ?? null;
  const pickable = pick ? layout.order.filter((id) => pick.can(id)) : [];
  const tabStop = pick && pickable.length > 0 && (roving === null || !pick.can(roving)) ? pickable[0]! : roving;

  const root = useRef<HTMLDivElement>(null);
  const cameBy = useRef(new Map<string, string>());
  // In the viewport the canvas reveals the node a focus lands on, so nothing scrolls behind its transform (trap 2).
  const focusNode = useCallback((id: string) => {
    const element = [...(root.current?.querySelectorAll<HTMLElement>('[data-flow-node]') ?? [])].find((each) => each.dataset.flowNode === id);
    if (element?.closest('[data-canvas-world]')) element.focus({ preventScroll: true });
    else element?.focus();
  }, []);

  // Pick mode started: the focus goes to the first node that can take the way, so the keys start among them.
  const pickKey = pick?.key ?? null;
  useLayoutEffect(() => {
    if (pickKey === null || pickable.length === 0) return;
    focused.current = pickable[0]!;
    focusNode(pickable[0]!);
  }, [pickKey, focusNode]);

  // The focus kept by node id (§3.7): a node the new placement or layout drew anew takes back the focus its old one had, and one
  // folded away gives it to its fold, which keeps the id so the fold opened gives it back.
  useLayoutEffect(() => {
    const id = focused.current;
    const active = document.activeElement;
    if (!id || (active && active !== document.body && active.isConnected)) return;
    focusNode(drawnOf(id));
  }, [placement, layout, focusNode]);

  const onFocus = (event: FocusEvent<HTMLDivElement>) => {
    const holder = (event.target as HTMLElement).closest<HTMLElement>('[data-flow-node]');
    if (!holder) return;
    // A fold that took the focus for a node it holds keeps that node's id.
    if (focused.current === null || drawnOf(focused.current) !== holder.dataset.flowNode) focused.current = holder.dataset.flowNode!;
    if (holder === event.target) setStop(holder.dataset.flowNode!);
  };
  const onBlur = (event: FocusEvent<HTMLDivElement>) => {
    const next = event.relatedTarget as Node | null;
    if (next && event.currentTarget.contains(next)) return;
    // Pressed elsewhere, the flow forgets it had the focus; drawn anew, its node is gone, and the id is kept to give it back.
    const left = event.target as HTMLElement;
    window.setTimeout(() => { if (left.isConnected && !root.current?.contains(document.activeElement)) focused.current = null; }, 0);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (isComposing(event)) return;
    // A key with Ctrl, Alt or the system key is the page's around the flow, such as an editor's move or undo.
    if (event.ctrlKey || event.altKey || event.metaKey) return;
    const target = event.target as HTMLElement;
    const holder = target.closest<HTMLElement>('[data-flow-node]');
    if (!holder) return;
    const id = holder.dataset.flowNode!;
    if (target !== holder) {
      if (event.key === 'Escape') {
        event.preventDefault();
        event.stopPropagation();
        holder.focus();
      }
      return;
    }
    if (pick) {
      // Pick mode walks only the nodes that can take the way; Enter picks, Escape gives the way up.
      const at = pickable.indexOf(id);
      let next: string | undefined;
      if (event.key === 'ArrowDown') next = at < 0 ? pickable[0] : pickable[at + 1];
      else if (event.key === 'ArrowUp') next = at < 0 ? pickable[0] : pickable[at - 1];
      else if (event.key === 'Home') next = pickable[0];
      else if (event.key === 'End') next = pickable[pickable.length - 1];
      else if (event.key === 'Enter') {
        event.preventDefault();
        if (pick.can(id)) pick.onPick(id);
        return;
      } else if (event.key === 'Escape') {
        event.preventDefault();
        event.stopPropagation();
        pick.onCancel();
        return;
      } else if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
        event.preventDefault();
        return;
      } else return;
      event.preventDefault();
      if (next) focusNode(next);
      return;
    }
    const at = layout.order.indexOf(id);
    let next: string | undefined;
    if (event.key === 'ArrowDown') next = layout.order[at + 1];
    else if (event.key === 'ArrowUp') next = layout.order[at - 1];
    else if (event.key === 'Home') next = layout.order[0];
    else if (event.key === 'End') next = layout.order[layout.order.length - 1];
    else if (event.key === 'ArrowRight') {
      next = edges.find((edge) => edge.from === id && byId.has(edge.to))?.to;
      if (next) cameBy.current.set(next, id);
    } else if (event.key === 'ArrowLeft') next = cameBy.current.get(id) ?? edges.find((edge) => edge.to === id && byId.has(edge.from))?.from;
    else if (event.key === 'Enter') {
      const open = byId.get(id)?.open;
      if (open) { event.preventDefault(); open(); }
      return;
    } else return;
    event.preventDefault();
    if (next) focusNode(next);
  };

  const follow = (from: string, to: string) => {
    cameBy.current.set(to, from);
    focusNode(to);
  };

  // A node's track: one width for every node, the most its widest rank leaves each, between the floor and 16rem, or the caller's.
  const most = bounds?.most ?? TRACK_MOST;
  const track = useMemo(() => trackFor(roomed, placement, width, least, most, remPx), [roomed, placement, width, least, most, remPx]);

  // The data wires drawn: those with an end selected, between nodes that are drawn.
  const shownData = useMemo(() => (selected == null ? [] : dataEdges.filter((edge) => (edge.from === selected || edge.to === selected)
    && byId.has(edge.from) && byId.has(edge.to))), [dataEdges, selected, byId]);
  const dataKey = JSON.stringify(shownData.map((edge) => [edge.id, edge.from, edge.to]));

  // The edges, drawn from the measured boxes, again on each resize of the drawing or of a node; nothing drawn moves a box.
  // The resizes of one frame are drawn once, on the next (trap 14), and a geometry that came out the same is not drawn.
  // The ＋ an editor offers on each way: none on a way back, and on the drawing none on a path that merges several edges.
  const inserts = new Map<string, ReactNode>();
  if (renderInsert) {
    for (const edge of edges) {
      if (backs.has(edge.id)) continue;
      const content = renderInsert(edge);
      if (content !== null && content !== undefined) inserts.set(edge.id, content);
    }
  }
  const insertPaths = layout.paths.filter((path) => !path.back && path.edges.length === 1 && inserts.has(path.edges[0]!));
  const railInserts = (id: string) => {
    const own = edges.filter((edge) => edge.from === id && inserts.has(edge.id));
    const marked = byId.get(id)?.marks ?? [];
    if (own.length === 0 && marked.length === 0) return null;
    return (
      <div className="mt-1 flex min-w-0 flex-wrap items-center gap-1">
        {marked.map((mark, at) => <MarkChip key={at} mark={mark} className="shrink-0" />)}
        {own.map((edge) => <span key={edge.id} data-flow-insert={edge.id} className="inline-flex">{inserts.get(edge.id)}</span>)}
      </div>
    );
  };

  // The marks' chips, each placed once the drawing is measured; one arriving or going is measured again though no node moved.
  const chipsOf = nodes.flatMap((node) => (node.marks ?? []).map((mark, at) => ({ key: `${node.id}:${at}`, node: node.id, mark })));
  const chipKey = JSON.stringify(chipsOf.map(({ key, mark }) => [key, mark.word, mark.kind]));
  const [ink, setInk] = useState<Ink | null>(null);
  const insertKey = JSON.stringify(insertPaths.map((path) => [path.id, path.edges[0]]));
  useLayoutEffect(() => {
    const element = drawing.current;
    if (!element || placement === 'rail') { setInk(null); return undefined; }
    const onPaths = new Map(insertPaths.map((path) => [path.edges[0]!, path.id]));
    return observeWorld(element, () => {
      const read = readWorld(element);
      const next = read && measure(element, read, layout, placement, shownData, onPaths);
      // The canvas follows the world the ink is drawn from: its size, its opening and the anchor across a relayout.
      if (read) canvas.current?.measured(read);
      setInk((was) => {
        if (!next) return was === null ? was : null;
        if (was && same(was, next)) return was;
        return { ...next, drawn: (was?.drawn ?? 0) + 1 };
      });
    });
  }, [layout, placement, track, dataKey, insertKey, chipKey, inViewport]);

  const nodeProps = (id: string): NodeContext => ({
    node: byId.get(id)!,
    stop: tabStop === id,
    feet: edges.filter((edge) => edge.from === id && byId.has(edge.to)).map((edge) => ({
      to: edge.to, back: backs.has(edge.id),
      text: backs.has(edge.id)
        ? t('flow.footBack', { node: byId.get(edge.to)!.title, says: edge.says })
        : t('flow.move', { node: byId.get(edge.to)!.title, says: edge.says }),
      ...(edge.port === undefined ? {} : { port: edge.port }),
    })),
    description: describe(t, byId.get(id)!, edges, byId, backs, {
      before: [...(divided.get(id) ?? []).map((title) => t('flow.section', { title })), ...(notes.get(id)?.before ?? [])],
      after: notes.get(id)?.after ?? [],
    }),
    onFollow: follow,
    ...(pick ? { picking: pick.can(id) } : {}),
  });

  // In pick mode a press on a node that cannot take the way leaves the focus where it is.
  const onMouseDown = (event: MouseEvent<HTMLDivElement>) => {
    const holder = (event.target as HTMLElement).closest<HTMLElement>('[data-flow-node]');
    if (pick && holder && !pick.can(holder.dataset.flowNode!)) event.preventDefault();
  };

  // *Show whole* where a flow past 24 nodes draws a fold, *Fold again* once one is opened.
  const anyFold = folds.length > 0 || (!whole && stacks.some((stack) => !opened.has(stack.id)));
  const showWhole = large && !whole && anyFold;
  const foldAgain = large && (whole || openedLarge.size > 0);

  /** A rank's width as drawn top to bottom: its node tracks, waypoints and the gaps between them, which its divider spans. */
  const rankWidth = (slots: readonly Slot[]) => {
    const count = slots.filter((slot) => slot.kind === 'node').length;
    return count * (track ?? px(least)) + (slots.length - count) * px(FLOW_REM.way) + Math.max(0, slots.length - 1) * px(FLOW_REM.gap);
  };
  const divider = (title: string, width?: number) => (
    <div key={title} aria-hidden="true" data-flow-section="" className="flex justify-center">
      <p className="m-0 min-w-0 border-t border-line pt-1.5 text-small font-semibold text-ink-faint" style={width === undefined ? { width: '100%' } : { width }}>
        {title}
      </p>
    </div>
  );
  // The first section heads the flow where the flow draws it, read by a reader; a caller's rail carries its own titles.
  const heading = head && (placement !== 'rail' || !rail) && (
    <div className="mb-3 grid min-w-0 gap-1.5">
      <h3 className="m-0 text-small font-semibold text-ink-faint">{head.title}</h3>
      {head.note}
    </div>
  );

  // A path's chip: what it says by default, else what the caller draws in its place (none, where the caller says so).
  const labelOf = (id: string, style: EdgeStyle, edgeIds: string[], back: boolean): ReactNode => {
    const found = edgeIds.length === 1 ? [...edges, ...dataEdges].find((edge) => edge.id === edgeIds[0]) : undefined;
    const text = back ? (edgeIds.length > 1 ? `↺ ×${edgeIds.length}` : '↺')
      : edgeIds.length > 1 ? `×${edgeIds.length}` : found?.label ?? null;
    if (renderLabel) return renderLabel({ id, style, text: text ?? '', count: edgeIds.length, edge: found });
    return text;
  };
  const labels: { id: string; content: ReactNode }[] = [
    ...layout.paths.map((path) => ({ id: path.id, content: labelOf(path.id, path.style, path.edges, path.back) })),
    ...shownData.map((edge) => ({ id: edge.id, content: labelOf(edge.id, 'data', [edge.id], false) })),
  ];

  const tools = (
    <>
      {showWhole && (
        <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => setWhole(true)}>{t('flow.whole')}</Button>
      )}
      {foldAgain && (
        <Button
          variant="ghost"
          className="min-h-0 px-1.5 py-0.5 text-small"
          onClick={() => { setWhole(false); setOpenedLarge(new Set()); }}
        >
          {t('flow.foldAgain')}
        </Button>
      )}
      {/* Offered wherever the drawing fits (§3.2): where only the rail fits, the list is already what is shown. */}
      {fits !== 'rail' && (
        <Button
          variant="ghost"
          className="min-h-0 px-1.5 py-0.5 text-small"
          onClick={() => {
            store(LIST_KEY, asList ? null : 'list');
            setAsList(!asList);
          }}
        >
          {t(asList ? 'flow.asChart' : 'flow.asList')}
        </Button>
      )}
    </>
  );

  return (
    <div className={cn('min-w-0', className)}>
      <Canvas
        ref={canvas}
        active={inViewport}
        options={options}
        label={label}
        width={width}
        rem={remPx}
        pane={pane}
        paneProps={{ 'data-flow-pane': '', 'data-placement': placement }}
        tools={tools}
        showTools={fits !== 'rail' || showWhole || foldAgain}
        entry={layout.order[0] ?? null}
        selected={selected ?? null}
        selection={selection}
        connect={connect}
      >
        <span id={keysId} hidden>{t('flow.keys')}</span>
        <div ref={root} onKeyDown={onKeyDown} onFocus={onFocus} onBlur={onBlur} onMouseDown={onMouseDown} className="min-w-0">
          {placement === 'rail'
            ? (rail ?? (
              <>
                {heading}
                <div role="list" aria-label={label} aria-describedby={keysId} className="m-0 grid min-w-0 gap-0 p-0">
                  {layout.order.map((id, index) => {
                    const row = (
                      <div key={id} className="grid min-w-0 grid-cols-[1rem_minmax(0,1fr)] gap-x-2">
                        <RailMark node={byId.get(id)!} last={index === layout.order.length - 1} />
                        <div className="min-w-0 pb-2">{renderNode(nodeProps(id))}{railInserts(id)}</div>
                      </div>
                    );
                    const titles = divided.get(id);
                    return titles ? <div key={id} className="grid min-w-0 gap-1">{titles.map((title) => divider(title))}{row}</div> : row;
                  })}
                </div>
              </>
            ))
            : (
              <div
                ref={drawing}
                data-drawn={ink?.drawn ?? 0}
                className="relative w-max min-w-full"
                style={padding(layout, placement, dataLanes)}
              >
                {heading && (
                  <div className="flex justify-center">
                    <div className="min-w-0" style={{ width: layout.ranks[0] ? rankWidth(layout.ranks[0]) : undefined }}>{heading}</div>
                  </div>
                )}
                <div
                  role="list"
                  aria-label={label}
                  aria-describedby={keysId}
                  className={cn('flex', placement === 'down' ? 'flex-col' : 'items-center justify-center')}
                  style={placement === 'down' ? { rowGap: rem(FLOW_REM.rankDown) } : { columnGap: rem(FLOW_REM.rankAcross) }}
                >
                  {layout.ranks.map((slots, index) => {
                    const row = (
                      <div
                        key={index}
                        className={cn('flex', placement === 'down' ? 'items-stretch justify-center' : 'flex-col justify-center')}
                        style={placement === 'down' ? { columnGap: rem(FLOW_REM.gap) } : { rowGap: rem(FLOW_REM.gap), width: track }}
                      >
                        {slots.map((slot) => (slot.kind === 'node'
                          ? (
                            <Fragment key={slot.id}>
                              {renderNode({ ...nodeProps(slot.id), ...(placement === 'down' ? { style: { width: track, flex: 'none' } } : {}) })}
                            </Fragment>
                          )
                          : (
                            <span
                              key={slot.key}
                              aria-hidden
                              data-flow-way={slot.key}
                              className="block shrink-0"
                              style={placement === 'down' ? { width: rem(FLOW_REM.way) } : { height: rem(FLOW_REM.way) }}
                            />
                          )))}
                      </div>
                    );
                    // A later section's divider stands just above its rank, spanning it; a flow in sections is never left to right.
                    const titles = placement === 'down' ? dividedAt.get(index) : undefined;
                    return titles
                      ? <div key={index} className="grid gap-2">{titles.map((title) => divider(title, rankWidth(slots)))}{row}</div>
                      : row;
                  })}
                </div>
                <svg
                  data-flow-ink=""
                  aria-hidden="true"
                  className="pointer-events-none absolute left-0 top-0 overflow-visible"
                  width={ink?.width ?? 0}
                  height={ink?.height ?? 0}
                >
                  <defs>
                    <marker id={`${marks}-arrow`} markerUnits="userSpaceOnUse" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto">
                      <path d="M0 0.5 7 4 0 7.5Z" className="fill-ink-faint" />
                    </marker>
                    <marker id={`${marks}-refused`} markerUnits="userSpaceOnUse" markerWidth="8" markerHeight="8" refX="4" refY="4" orient="auto">
                      <path d="M1 1 7 7M7 1 1 7" className="stroke-ink-faint" strokeWidth="1.2" fill="none" />
                    </marker>
                  </defs>
                  {onEdge && ink?.paths.map((path) => {
                    const ids = layout.paths.find((each) => each.id === path.id)?.edges ?? [];
                    const edge = ids.length === 1 ? edges.find((each) => each.id === ids[0]) : undefined;
                    return edge ? (
                      <path
                        key={`hit:${path.id}`}
                        data-flow-edge-hit={path.id}
                        d={path.d}
                        fill="none"
                        stroke="transparent"
                        strokeWidth={14}
                        vectorEffect="non-scaling-stroke"
                        style={{ pointerEvents: 'stroke', cursor: 'pointer' }}
                        onClick={() => onEdge(edge)}
                      />
                    ) : null;
                  })}
                  {ink?.paths.map((path) => (
                    <path
                      key={path.id}
                      data-flow-edge={path.id}
                      data-from={path.from}
                      data-to={path.to}
                      data-port={path.port !== null && !path.back ? path.port : undefined}
                      data-taken={taken.has(path.id) ? '' : undefined}
                      d={path.d}
                      fill="none"
                      className={taken.has(path.id) ? 'stroke-ink-soft' : 'stroke-ink-faint'}
                      strokeWidth={taken.has(path.id) ? 2 : 1}
                      strokeDasharray={DASHES[path.style]}
                      markerEnd={`url(#${marks}-${path.style === 'refused' ? 'refused' : 'arrow'})`}
                    />
                  ))}
                </svg>
                {labels.map(({ id, content }) => {
                  if (content === null || content === undefined || content === false || content === '') return null;
                  const placed = ink?.labels.find((each) => each.id === id);
                  return (
                    <span
                      key={id}
                      aria-hidden
                      data-flow-label={id}
                      data-shown={placed?.shown ? '' : undefined}
                      className="pointer-events-none absolute whitespace-nowrap rounded-control bg-page px-1 text-meta leading-tight text-ink-soft"
                      style={{
                        left: placed ? placed.x - placed.w / 2 : 0,
                        top: placed ? placed.y - placed.h / 2 : 0,
                        visibility: placed?.shown ? 'visible' : 'hidden',
                      }}
                    >
                      {content}
                    </span>
                  );
                })}
                {insertPaths.map((path) => {
                  const edge = path.edges[0]!;
                  const placed = ink?.inserts.find((each) => each.id === path.id);
                  return (
                    <span
                      key={`insert:${edge}`}
                      data-flow-insert={edge}
                      data-shown={placed?.shown ? '' : undefined}
                      className="absolute inline-flex"
                      style={{
                        left: placed ? placed.x - placed.w / 2 : 0,
                        top: placed ? placed.y - placed.h / 2 : 0,
                        visibility: placed?.shown ? 'visible' : 'hidden',
                      }}
                    >
                      {inserts.get(edge)}
                    </span>
                  );
                })}
                {chipsOf.map(({ key, node, mark }) => {
                  const placed = ink?.chips.find((each) => each.id === key);
                  return (
                    <MarkChip
                      key={key}
                      mark={mark}
                      data={{ 'data-flow-chip': key, 'data-chip-node': node, 'data-shown': placed?.shown ? '' : undefined }}
                      className="pointer-events-none absolute"
                      style={{ left: placed?.x ?? 0, top: placed?.y ?? 0, visibility: placed?.shown ? 'visible' : 'hidden' }}
                    />
                  );
                })}
              </div>
            )}
        </div>
      </Canvas>
      {end}
    </div>
  );
}

