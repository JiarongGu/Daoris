import {
  type ComponentProps, type CSSProperties, type FocusEvent, type KeyboardEvent, type ReactNode, useCallback, useId, useLayoutEffect, useMemo,
  useRef, useState,
} from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { isComposing } from '../lib/composing';
import { store, stored } from '../lib/stored';
import { type Fold, largeFolds, WHOLE_MOST } from './flowFolds';
import { type Box, type InkLabel, type InkPath, placeLabels, routeInk, RUN } from './flowInk';
import { type EdgeStyle, findStacks, FLOW_REM, type FlowLayout, layoutFlow, type Placement, placementFor, type Slot, type Stack } from './flowLayout';
import { Button, Icon, type IconName, Pill } from './ui';
import { useWidth } from './useWidth';

/** A pill's tone, from the caller's own table (`QUEST_TONE`, `SESSION_TONE`): the flow adds none. */
export type PillTone = NonNullable<ComponentProps<typeof Pill>['tone']>;

/** One place in the flow, worded by its caller (the flowchart design §4.1). */
export type FlowNode = {
  id: string;
  title: string;
  glyph?: IconName;
  /** Its state as a pill, in the caller's tone. */
  state?: { word: string; tone: PillTone };
  /** Who acts there: drawn, unless `who` is, and said to a reader. */
  actor?: string | null;
  /**
   * Who acts, as the caller marks it (FLOWCHART1d): drawn in `actor`'s place where its words need marks a string cannot carry, a
   * workflow step's part by its shape and its executor's names as code. `actor` stays the words a reader hears.
   */
  who?: ReactNode;
  /** The caller's own lines: its moves, a door. */
  lines?: ReactNode;
  /** False: not reached, drawn dashed with its words at the soft ink. Absent: the record does not say. */
  reached?: boolean;
  /**
   * Drawn as a node not reached is, dashed with its words at the soft ink, without saying it was not reached: a node outside the
   * record's own, such as the repository that asked a part (FLOWCHART1b).
   */
  faint?: boolean;
  current?: boolean;
  /**
   * Done (FLOWCHART1d): what a settled stretch folds, said as *n steps done*, once a flow is past 24 nodes (§3.6 step 2). A step
   * passed over is not, so a skip stays in view.
   */
  settled?: boolean;
  /** Its record's door: Enter on the node opens it. */
  open?: () => void;
  /**
   * What makes it like the nodes beside it (FLOWCHART1c; the flowchart design §3.6 step 1): three or more with one `key`, each
   * following the one before in a chain, are drawn as one fold, titled by their shared `word` and counted, with the first and the
   * last `at` (worded by the caller, where its record keeps a time). A try's code is one key; an advance is another.
   */
  stack?: { key: string; word: string; at?: string | null };
};

/** One move between two nodes, with its cause. */
export type FlowEdge = {
  id: string;
  from: string;
  to: string;
  /** The whole cause, said in the source's foot and to a reader. */
  says: string;
  /** A word or two, drawn on the edge where it fits. */
  label?: string;
  style?: EdgeStyle;
  /**
   * On the path its record kept (FLOWCHART1a2): drawn heavier, in ink and never a status hue (D186 point 6). Its cause says so in
   * words, for the rail and a reader. What is lit lays nothing out again: the layout never reads it.
   */
  taken?: boolean;
};

export type FlowProps = {
  /** Names the flow for a reader (*How #q1 moves*). */
  label: string;
  nodes: FlowNode[];
  edges: FlowEdge[];
  entries: string[];
  /** What stands after the flow. */
  end?: ReactNode;
  /**
   * The caller's own rail (FLOWCHART1b): drawn where only the rail fits, or the person chose the list, in place of the nodes' list.
   * The parts' strip is one: its buttons show a part, which the node list's feet cannot. The nodes remain the list wherever the
   * drawing fits.
   */
  rail?: ReactNode;
  /**
   * Its sections, read rank by rank under their titles (FLOWCHART1d): a workflow's *Before it lands* and *After it lands*, the
   * landing's divider (the flowchart design §2.4). A flow in sections is never placed left to right.
   */
  sections?: FlowSection[];
  /**
   * Its node track's floor and most, in rem (FLOWCHART1d): §3.2's 10rem and 16rem unless a caller's node holds more, as a
   * workflow's step holds its settings and its run's facts.
   */
  track?: { least?: number; most?: number };
  className?: string;
};

/**
 * A section of a flow (FLOWCHART1d), from the rank of its node `from`. The first heads the flow with its `note`, which a reader
 * hears; each later one divides the drawing before its rank, a mark the eye reads, and a reader hears its title at its first node.
 * A caller's own rail carries its own titles.
 */
export type FlowSection = { title: string; from: string; note?: ReactNode };

/** The person's choice of the list over the drawing, remembered on this machine for every flow (§3.2). */
const LIST_KEY = 'daoris.flow.list';

const rem = (value: number) => `${value}rem`;
const px = (value: number) => value * 16;

/** The drawn geometry: its paths and labels, the drawing's size, and how many times it was drawn (trap 14). */
type Ink = { paths: InkPath[]; labels: InkLabel[]; width: number; height: number; drawn: number };

/**
 * **A flowchart** (FLOWCHART1a1; D186, the flowchart design §3 and §4): one process drawn from its record, laid out in
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
 * - **Too large to show whole** (FLOWCHART1d, §3.6): past 24 nodes, after the stacks, settled stretches fold and the flow is focused
 *   on the ranks around every current node, or with none current where it starts, ends or got to (`largeFolds`). A fold is a node
 *   with a press, every edge across its boundary drawn to it, merged by cause and by the node outside and counted; *Show whole*
 *   opens every fold and *Fold again* folds them back. What is open is page state, never remembered, and a fold the person opened
 *   stays open while the state moves; a node folded away gives its focus to its fold, and an opened fold gives it back.
 * - **Sections** (FLOWCHART1d): a caller's titles, each from a node's rank, the first heading the flow and each later one a divider
 *   before its rank; a flow in sections is never left to right.
 *
 * A molecule: the flow arrives worded and its doors go out.
 */
export function Flow({ label, nodes: given, edges: givenEdges, entries: givenEntries, end, rail, sections, track: bounds, className }: FlowProps) {
  const { t } = useTranslation();
  const keysId = useId();
  const marks = useId().replace(/[^\w-]/g, '');
  const pane = useRef<HTMLDivElement>(null);
  const drawing = useRef<HTMLDivElement>(null);
  const width = useWidth(pane);
  const [asList, setAsList] = useState(() => stored(LIST_KEY) === 'list');

  // The roving stop (below) and the node that has the focus, kept by id across a fold as across a placement.
  const [stop, setStop] = useState<string | null>(null);
  const focused = useRef<string | null>(null);

  // *Show whole* (§3.6): every fold opened, stacks among them, until *Fold again*. Page state, never remembered.
  const [whole, setWhole] = useState(false);

  // Stacks (FLOWCHART1c; §3.6 step 1): three or more like nodes in a chain drawn as one fold, opened by its press. Which are open is
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

  // Past 24 nodes (FLOWCHART1d; §3.6 steps 2-3): settled stretches, then the focus, which follow what is current and settled, so
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
  const shapeKey = JSON.stringify([nodes.map((node) => node.id), edges.map((edge) => [edge.id, edge.from, edge.to, edge.style]), entries]);
  const layout = useMemo(() => layoutFlow({ nodes: nodes.map((node) => ({ id: node.id })), edges, entries }), [shapeKey]);
  const least = bounds?.least ?? FLOW_REM.node;
  const fits = placementFor(layout, width, false, 16, { least, across: !sections || sections.length === 0 });
  const placement: Placement = asList ? 'rail' : fits;

  const byId = useMemo(() => new Map(nodes.map((node) => [node.id, node])), [nodes]);
  const backs = useMemo(() => new Set(layout.paths.filter((path) => path.back).flatMap((path) => path.edges)), [layout]);
  // The paths that carry an edge the record kept as taken (FLOWCHART1a2), drawn heavier; the layout never reads it.
  const taken = useMemo(() => {
    const kept = new Set(edges.filter((edge) => edge.taken).map((edge) => edge.id));
    return new Set(layout.paths.filter((path) => path.edges.some((id) => kept.has(id))).map((path) => path.id));
  }, [layout, edges]);

  // Sections (FLOWCHART1d): the first heads the flow; each later one divides the drawing before the rank its node is drawn in, and a
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

  // The roving stop: the node the person was last on, else the current one, else the first in reading order.
  const tabStop = stop !== null && byId.has(drawnOf(stop))
    ? drawnOf(stop)
    : layout.order.find((id) => byId.get(id)?.current) ?? layout.order[0] ?? null;

  const root = useRef<HTMLDivElement>(null);
  const cameBy = useRef(new Map<string, string>());
  const focusNode = useCallback((id: string) => {
    const element = [...(root.current?.querySelectorAll<HTMLElement>('[data-flow-node]') ?? [])].find((each) => each.dataset.flowNode === id);
    element?.focus();
  }, []);

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
  const track = useMemo(() => trackFor(layout, placement, width, least, most), [layout, placement, width, least, most]);

  // The edges, drawn from the measured boxes, again on each resize of the drawing or of a node; nothing drawn moves a box.
  // The resizes of one frame are drawn once, on the next (trap 14), and a geometry that came out the same is not drawn.
  const [ink, setInk] = useState<Ink | null>(null);
  useLayoutEffect(() => {
    const element = drawing.current;
    if (!element || placement === 'rail') { setInk(null); return undefined; }
    const draw = () => {
      const next = measure(element, layout, placement);
      setInk((was) => {
        if (!next) return was === null ? was : null;
        if (was && same(was, next)) return was;
        return { ...next, drawn: (was?.drawn ?? 0) + 1 };
      });
    };
    let pending = 0;
    const later = () => {
      if (typeof requestAnimationFrame !== 'function') { draw(); return; }
      cancelAnimationFrame(pending);
      pending = requestAnimationFrame(draw);
    };
    draw();
    const observer = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(later) : null;
    observer?.observe(element);
    element.querySelectorAll('[data-flow-node]').forEach((node) => observer?.observe(node));
    return () => {
      if (typeof cancelAnimationFrame === 'function') cancelAnimationFrame(pending);
      observer?.disconnect();
    };
  }, [layout, placement, track]);

  const nodeProps = (id: string): NodeProps => ({
    node: byId.get(id)!,
    stop: tabStop === id,
    feet: edges.filter((edge) => edge.from === id && byId.has(edge.to)).map((edge) => ({
      to: edge.to, back: backs.has(edge.id),
      text: backs.has(edge.id)
        ? t('flow.footBack', { node: byId.get(edge.to)!.title, says: edge.says })
        : t('flow.move', { node: byId.get(edge.to)!.title, says: edge.says }),
    })),
    description: describe(t, byId.get(id)!, edges, byId, backs, {
      before: [...(divided.get(id) ?? []).map((title) => t('flow.section', { title })), ...(notes.get(id)?.before ?? [])],
      after: notes.get(id)?.after ?? [],
    }),
    onFollow: follow,
  });

  // *Show whole* where a flow past 24 nodes draws a fold, *Fold again* once one was opened (§3.6).
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
  // The first section heads the flow where `Flow` draws it, read by a reader; a caller's rail carries its own titles.
  const heading = head && (placement !== 'rail' || !rail) && (
    <div className="mb-3 grid min-w-0 gap-1.5">
      <h3 className="m-0 text-small font-semibold text-ink-faint">{head.title}</h3>
      {head.note}
    </div>
  );

  const labelText = (path: FlowLayout['paths'][number]): string | null => {
    if (path.back) return path.edges.length > 1 ? `↺ ×${path.edges.length}` : '↺';
    if (path.edges.length > 1) return `×${path.edges.length}`;
    return edges.find((edge) => edge.id === path.edges[0])?.label ?? null;
  };

  return (
    <div className={cn('min-w-0', className)}>
      {(fits !== 'rail' || showWhole || foldAgain) && (
        <div className="mb-2 flex flex-wrap justify-end gap-x-1">
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
        </div>
      )}
      <div ref={pane} data-flow-pane="" data-placement={placement} className="min-w-0 overflow-x-auto">
        <span id={keysId} hidden>{t('flow.keys')}</span>
        <div ref={root} onKeyDown={onKeyDown} onFocus={onFocus} onBlur={onBlur} className="min-w-0">
          {placement === 'rail'
            ? (rail ?? (
              <>
                {heading}
                <div role="list" aria-label={label} aria-describedby={keysId} className="m-0 grid min-w-0 gap-0 p-0">
                  {layout.order.map((id, index) => {
                    const row = (
                      <div key={id} className="grid min-w-0 grid-cols-[1rem_minmax(0,1fr)] gap-x-2">
                        <RailMark node={byId.get(id)!} last={index === layout.order.length - 1} />
                        <div className="min-w-0 pb-2"><FlowNodeBox {...nodeProps(id)} /></div>
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
                style={padding(layout, placement)}
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
                          ? <FlowNodeBox key={slot.id} {...nodeProps(slot.id)} style={placement === 'down' ? { width: track, flex: 'none' } : undefined} />
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
                  {ink?.paths.map((path) => (
                    <path
                      key={path.id}
                      data-flow-edge={path.id}
                      data-from={path.from}
                      data-to={path.to}
                      data-taken={taken.has(path.id) ? '' : undefined}
                      d={path.d}
                      fill="none"
                      className={taken.has(path.id) ? 'stroke-ink-soft' : 'stroke-ink-faint'}
                      strokeWidth={taken.has(path.id) ? 2 : 1}
                      strokeDasharray={path.style === 'next' ? undefined : '4 3'}
                      markerEnd={`url(#${marks}-${path.style === 'refused' ? 'refused' : 'arrow'})`}
                    />
                  ))}
                </svg>
                {layout.paths.map((path) => {
                  const text = labelText(path);
                  if (!text) return null;
                  const placed = ink?.labels.find((each) => each.id === path.id);
                  return (
                    <span
                      key={path.id}
                      aria-hidden
                      data-flow-label={path.id}
                      data-shown={placed?.shown ? '' : undefined}
                      className="pointer-events-none absolute whitespace-nowrap rounded-control bg-page px-1 text-meta leading-tight text-ink-soft"
                      style={{
                        left: placed ? placed.x - placed.w / 2 : 0,
                        top: placed ? placed.y - placed.h / 2 : 0,
                        visibility: placed?.shown ? 'visible' : 'hidden',
                      }}
                    >
                      {text}
                    </span>
                  );
                })}
              </div>
            )}
        </div>
      </div>
      {end}
    </div>
  );
}

type NodeProps = {
  node: FlowNode;
  /** Whether it holds the flow's one Tab stop. */
  stop: boolean;
  feet: { to: string; back: boolean; text: string }[];
  description: string;
  onFollow: (from: string, to: string) => void;
  style?: CSSProperties;
};

/** One node (§3.3): its words in reading order, and its foot's presses. */
function FlowNodeBox({ node, stop, feet, description, onFollow, style }: NodeProps) {
  const titleId = useId();
  const stateId = useId();
  const descriptionId = useId();
  const faint = node.reached === false || node.faint === true;
  return (
    <div
      role="listitem"
      data-flow-node={node.id}
      tabIndex={stop ? 0 : -1}
      aria-current={node.current ? 'step' : undefined}
      aria-labelledby={node.state ? `${titleId} ${stateId}` : titleId}
      aria-describedby={descriptionId}
      style={style}
      className={cn(
        'min-w-0 rounded-card bg-raised px-3 py-2',
        node.current ? 'border-2 border-ink-soft' : faint ? 'border border-dashed border-line-strong' : 'border border-line',
      )}
    >
      <div className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
        <span id={titleId} className={cn('min-w-0 text-body font-semibold [overflow-wrap:anywhere]', faint ? 'text-ink-soft' : 'text-ink')}>
          {node.glyph && <Icon name={node.glyph} size={14} className="mr-1.5 inline translate-y-0.5 text-ink-soft" />}
          {node.title}
        </span>
        {node.state && <span id={stateId}><Pill tone={node.state.tone}>{node.state.word}</Pill></span>}
      </div>
      {node.who
        ? <div className="mt-1 min-w-0 text-small text-ink-soft">{node.who}</div>
        : node.actor && <p className="m-0 mt-1 text-small text-ink-soft [overflow-wrap:anywhere]">{node.actor}</p>}
      {node.lines && <div className="mt-1 min-w-0 text-small text-ink-soft">{node.lines}</div>}
      {feet.length > 0 && (
        <div className="mt-1.5 grid min-w-0 gap-0.5">
          {feet.map((foot, index) => (
            <button
              key={index}
              type="button"
              tabIndex={-1}
              onClick={() => onFollow(node.id, foot.to)}
              className="cursor-pointer border-0 bg-transparent p-0 text-left text-meta text-ink-soft [overflow-wrap:anywhere] hover:text-ink"
            >
              <span aria-hidden className="mr-1">{foot.back ? '↺' : '→'}</span>{foot.text}
            </button>
          ))}
        </div>
      )}
      <span id={descriptionId} hidden>{description}</span>
    </div>
  );
}

/** The rail's mark beside a node: whole where it is current, dashed where it is not reached, and the line to the next. */
function RailMark({ node, last }: { node: FlowNode; last: boolean }) {
  return (
    <div aria-hidden className="flex flex-col items-center pt-3">
      <span
        className={cn(
          'block size-2.5 shrink-0 rounded-full border',
          node.current ? 'border-ink-soft bg-ink-soft' : node.reached === false || node.faint ? 'border-dashed border-line-strong' : 'border-ink-soft',
        )}
      />
      {!last && <span className="mt-1 w-px flex-1 bg-ink-faint" />}
    </div>
  );
}

type Translate = (key: string, values?: Record<string, unknown>) => string;

/** What a stack adds to a node's description (§3.6): what a fold holds, before; the edges between its members, after. */
type StackNote = { before: string[]; after: string[] };

/**
 * A node's description (§3.7): its state, who acts, whether it was reached, and every edge in and out, in words; a fold's says what
 * it holds and the edges between its members, so no cause is lost to it (§3.6).
 */
function describe(
  t: Translate, node: FlowNode, edges: FlowEdge[], byId: Map<string, FlowNode>, backs: Set<string>, note?: StackNote,
): string {
  const title = (id: string) => byId.get(id)!.title;
  const sentences: string[] = [...(note?.before ?? [])];
  if (node.state) sentences.push(t('flow.state', { word: node.state.word }));
  if (node.actor) sentences.push(t('flow.actor', { actor: node.actor }));
  if (node.reached === false) sentences.push(t('flow.notReached'));
  for (const edge of edges) if (edge.to === node.id && byId.has(edge.from)) sentences.push(t('flow.from', { node: title(edge.from), says: edge.says }));
  const out = edges.filter((edge) => edge.from === node.id && byId.has(edge.to));
  const next = out.filter((edge) => !backs.has(edge.id)).map((edge) => t('flow.move', { node: title(edge.to), says: edge.says }));
  if (next.length > 0) sentences.push(t('flow.next', { moves: next.reduce((first, second) => t('flow.moves', { first, second })) }));
  for (const edge of out) if (backs.has(edge.id)) sentences.push(t('flow.back', { node: title(edge.to), says: edge.says }));
  sentences.push(...(note?.after ?? []));
  return sentences.length === 0 ? '' : sentences.reduce((first, second) => t('flow.join', { first, second }));
}

const STACK_PRESS = 'cursor-pointer border-0 bg-transparent p-0 text-left text-meta text-ink-soft underline-offset-2 hover:text-ink hover:underline';

/**
 * The flow as drawn with its stacks (FLOWCHART1c; the flowchart design §3.6): each stack not opened is one node in its first
 * member's place, titled by the members' shared word and counted, with their first and last time and its press; every edge across
 * its boundary drawn to it with its cause, and those between its members said in its description. An opened stack is its members
 * whole, the first carrying the press that folds them again, which Enter does where the member has no door of its own.
 */
function foldStacks(
  t: Translate, nodes: FlowNode[], edges: FlowEdge[], entries: string[], stacks: Stack[], opened: ReadonlySet<string>,
  toggle: (stack: Stack, open: boolean) => void,
): Folded {
  const notes = new Map<string, StackNote>();
  const held = new Map<string, number>();
  if (stacks.length === 0) return { nodes, edges, entries, notes, foldOf: new Map(), held };
  const byId = new Map(nodes.map((node) => [node.id, node]));
  const foldOf = new Map<string, string>();
  const replaced = new Map<string, FlowNode>();
  for (const stack of stacks) {
    const members = stack.members.map((id) => byId.get(id)!);
    const first = members[0]!;
    const last = members[members.length - 1]!;
    const count = members.length;
    if (opened.has(stack.id)) {
      const close = (
        <button type="button" tabIndex={-1} aria-expanded onClick={() => toggle(stack, false)} className={STACK_PRESS}>
          {t('flow.stack.close', { count })}
        </button>
      );
      replaced.set(first.id, {
        ...first,
        lines: <div className="grid min-w-0 justify-items-start gap-0.5">{first.lines}{close}</div>,
        open: first.open ?? (() => toggle(stack, false)),
      });
      notes.set(first.id, { before: [], after: [t(first.open ? 'flow.stack.memberPress' : 'flow.stack.member', { count })] });
      continue;
    }
    for (const id of stack.members) foldOf.set(id, stack.id);
    held.set(stack.id, count);
    const word = first.stack!.word;
    const reached = members.every((member) => member.reached === false) ? false : members.every((member) => member.reached === true) ? true : undefined;
    const firstAt = first.stack?.at;
    replaced.set(first.id, {
      id: stack.id,
      title: t('flow.stack.title', { word, count }),
      ...(first.glyph ? { glyph: first.glyph } : {}),
      ...(first.state && members.every((member) => member.state?.word === first.state!.word) ? { state: first.state } : {}),
      ...(reached === undefined ? {} : { reached }),
      ...(members.every((member) => member.faint) ? { faint: true } : {}),
      ...(members.every((member) => member.settled) ? { settled: true } : {}),
      current: members.some((member) => member.current),
      open: () => toggle(stack, true),
      lines: (
        <div className="grid min-w-0 justify-items-start gap-0.5">
          {firstAt && <p className="m-0 min-w-0 [overflow-wrap:anywhere]">{t('flow.stack.when', { first: firstAt, last: last.stack?.at ?? firstAt })}</p>}
          <button type="button" tabIndex={-1} aria-expanded={false} onClick={() => toggle(stack, true)} className={STACK_PRESS}>
            {t('flow.stack.open', { count })}
          </button>
        </div>
      ),
    });
    // The edges between its members, merged by cause and counted, so none is lost to the fold.
    const causes = new Map<string, number>();
    for (const edge of edges) if (stack.within.includes(edge.id)) causes.set(edge.says, (causes.get(edge.says) ?? 0) + 1);
    const between = [...causes].map(([says, times]) => (times > 1 ? t('flow.stack.times', { says, count: times }) : says));
    notes.set(stack.id, {
      before: [t('flow.stack.holds', { count, word })],
      after: between.length === 0 ? [] : [t('flow.stack.between', { says: between.reduce((one, two) => t('flow.moves', { first: one, second: two })) })],
    });
  }
  return {
    nodes: nodes.flatMap((node) => replaced.get(node.id) ?? (foldOf.has(node.id) ? [] : [node])),
    edges: edges.flatMap((edge) => {
      const from = foldOf.get(edge.from) ?? edge.from;
      const to = foldOf.get(edge.to) ?? edge.to;
      if (foldOf.has(edge.from) && from === to) return [];
      return from === edge.from && to === edge.to ? [edge] : [{ ...edge, from, to }];
    }),
    entries: [...new Set(entries.map((id) => foldOf.get(id) ?? id))],
    notes,
    foldOf,
    held,
  };
}

/**
 * A flow as drawn with its folds: its nodes, edges and entries, what each fold adds to a description, which fold holds each node it
 * folded, and how many nodes given each fold holds.
 */
type Folded = {
  nodes: FlowNode[];
  edges: FlowEdge[];
  entries: string[];
  notes: Map<string, StackNote>;
  foldOf: Map<string, string>;
  held: Map<string, number>;
};

/**
 * The flow as drawn with its folds past 24 nodes (FLOWCHART1d; the flowchart design §3.6 steps 2-3), over the flow its stacks drew:
 * each fold one node in its first member's place, titled by what it holds and counted (a stack inside it by its members), with its
 * press. Every edge across its boundary is drawn to it, merged by its cause and by the node outside and counted, so no cause is lost
 * and no node says one twice; those between its members are said in its description, merged by cause and counted.
 */
function foldRanges(t: Translate, flow: Folded, folds: Fold[], open: (fold: Fold) => void): Folded {
  const notes = new Map<string, StackNote>();
  if (folds.length === 0) return { ...flow, notes, foldOf: new Map() };
  const byId = new Map(flow.nodes.map((node) => [node.id, node]));
  const foldOf = new Map(folds.flatMap((fold) => fold.members.map((member) => [member, fold.id] as const)));
  const replaced = new Map<string, FlowNode>();
  const held = new Map(flow.held);
  for (const fold of folds) {
    const members = fold.members.map((id) => byId.get(id)!);
    const count = members.reduce((sum, member) => sum + (flow.held.get(member.id) ?? 1), 0);
    held.set(fold.id, count);
    const reached = members.every((member) => member.reached === false) ? false : members.every((member) => member.reached === true) ? true : undefined;
    replaced.set(fold.members[0]!, {
      id: fold.id,
      title: t(`flow.fold.${fold.kind}`, { count }),
      ...(reached === undefined ? {} : { reached }),
      ...(members.every((member) => member.faint) ? { faint: true } : {}),
      ...(members.every((member) => member.settled) ? { settled: true } : {}),
      open: () => open(fold),
      lines: (
        <div className="grid min-w-0 justify-items-start gap-0.5">
          <button type="button" tabIndex={-1} aria-expanded={false} onClick={() => open(fold)} className={STACK_PRESS}>
            {t('flow.stack.open', { count })}
          </button>
        </div>
      ),
    });
    const causes = new Map<string, number>();
    for (const edge of flow.edges) if (fold.within.includes(edge.id)) causes.set(edge.says, (causes.get(edge.says) ?? 0) + 1);
    const between = [...causes].map(([says, times]) => (times > 1 ? t('flow.stack.times', { says, count: times }) : says));
    notes.set(fold.id, {
      before: [t('flow.fold.holds', { count })],
      after: between.length === 0 ? [] : [t('flow.stack.between', { says: between.reduce((one, two) => t('flow.moves', { first: one, second: two })) })],
    });
  }

  // Each edge across a boundary drawn to its fold; those of one cause between one node outside and the fold are one, counted.
  const edges: FlowEdge[] = [];
  const merged = new Map<string, { edge: FlowEdge; count: number }>();
  for (const edge of flow.edges) {
    const from = foldOf.get(edge.from) ?? edge.from;
    const to = foldOf.get(edge.to) ?? edge.to;
    if (foldOf.has(edge.from) && from === to) continue;
    if (from === edge.from && to === edge.to) { edges.push(edge); continue; }
    const key = JSON.stringify([from, to, edge.style ?? 'next', edge.says]);
    const found = merged.get(key);
    if (found) {
      found.count += 1;
      if (edge.taken) found.edge.taken = true;
      continue;
    }
    const drawn: FlowEdge = { ...edge, from, to };
    merged.set(key, { edge: drawn, count: 1 });
    edges.push(drawn);
  }
  for (const { edge, count } of merged.values()) if (count > 1) edge.says = t('flow.stack.times', { says: edge.says, count });

  return {
    nodes: flow.nodes.flatMap((node) => replaced.get(node.id) ?? (foldOf.has(node.id) ? [] : [node])),
    edges,
    entries: [...new Set(flow.entries.map((id) => foldOf.get(id) ?? id))],
    notes,
    foldOf,
    held,
  };
}

/** The room a drawn placement keeps outside its nodes for the returns: their lanes, and their runs past the end ranks. */
function padding(layout: FlowLayout, placement: Placement): CSSProperties {
  if (layout.lanes === 0) return {};
  const lanes = rem(layout.lanes * (FLOW_REM.lane + FLOW_REM.laneGap));
  const run = `${RUN.pad}px`;
  return placement === 'down'
    ? { paddingLeft: lanes, paddingTop: run, paddingBottom: run }
    : { paddingBottom: `calc(${lanes} + 0.5rem)`, paddingLeft: run, paddingRight: run };
}

/** A node track's most, in rem (§3.2): a caller's node that holds more may take more (FLOWCHART1d). */
const TRACK_MOST = 16;

/** One node track's width for the drawn placements: as wide as the binding rank leaves each node, 10rem to 16rem, or the caller's. */
function trackFor(layout: FlowLayout, placement: Placement, width: number | undefined, least: number, mostRem: number): number | undefined {
  if (placement === 'rail' || width === undefined) return undefined;
  const floor = px(least);
  const most = px(mostRem);
  if (placement === 'across') {
    const ranks = layout.ranks.length;
    const room = width - (ranks - 1) * px(FLOW_REM.rankAcross) - (layout.lanes > 0 ? 2 * RUN.pad : 0);
    return Math.max(floor, Math.min(most, Math.floor(room / Math.max(1, ranks))));
  }
  const room = width - layout.lanes * px(FLOW_REM.lane + FLOW_REM.laneGap);
  const each = layout.ranks.map((slots) => {
    const nodes = slots.filter((slot) => slot.kind === 'node').length;
    const rest = (slots.length - nodes) * px(FLOW_REM.way) + Math.max(0, slots.length - 1) * px(FLOW_REM.gap);
    return nodes === 0 ? most : (room - rest) / nodes;
  });
  return Math.max(floor, Math.min(most, Math.floor(Math.min(...each))));
}

/** The drawn geometry from the boxes the page laid out, or null where nothing was laid out (a unit test's DOM). */
function measure(element: HTMLElement, layout: FlowLayout, placement: 'across' | 'down'): Omit<Ink, 'drawn'> | null {
  const frame = element.getBoundingClientRect();
  if (frame.width === 0) return null;
  const boxes = new Map<string, Box>();
  const boxOf = (target: Element): Box => {
    const rect = target.getBoundingClientRect();
    return { x: rect.left - frame.left, y: rect.top - frame.top, w: rect.width, h: rect.height };
  };
  element.querySelectorAll<HTMLElement>('[data-flow-node]').forEach((node) => boxes.set(node.dataset.flowNode!, boxOf(node)));
  element.querySelectorAll<HTMLElement>('[data-flow-way]').forEach((way) => boxes.set(way.dataset.flowWay!, boxOf(way)));
  if ([...boxes.values()].some((box) => box.w === 0 && box.h === 0)) return null;
  const paths = routeInk(layout, placement, boxes);
  const sizes = new Map<string, { w: number; h: number }>();
  element.querySelectorAll<HTMLElement>('[data-flow-label]').forEach((label) => {
    sizes.set(label.dataset.flowLabel!, { w: label.offsetWidth, h: label.offsetHeight });
  });
  // A label clears every node and every section's divider (FLOWCHART1d), which stands in a rank gap as a label does.
  const nodes = [...layout.order.map((id) => boxes.get(id)!), ...[...element.querySelectorAll('[data-flow-section]')].map(boxOf)];
  return { paths, labels: placeLabels(paths, sizes, nodes), width: frame.width, height: frame.height };
}

function same(a: Omit<Ink, 'drawn'>, b: Omit<Ink, 'drawn'>): boolean {
  return a.width === b.width && a.height === b.height
    && a.paths.length === b.paths.length && a.paths.every((path, index) => path.d === b.paths[index]!.d)
    && a.labels.length === b.labels.length
    && a.labels.every((label, index) => {
      const other = b.labels[index]!;
      return label.x === other.x && label.y === other.y && label.shown === other.shown && label.w === other.w;
    });
}
