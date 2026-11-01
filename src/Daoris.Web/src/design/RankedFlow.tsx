import {
  type ComponentProps, type CSSProperties, Fragment, type FocusEvent, type KeyboardEvent, type MouseEvent, type ReactNode, useCallback, useId,
  useLayoutEffect, useMemo, useRef, useState,
} from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { isComposing } from '../lib/composing';
import { store, stored } from '../lib/stored';
import { Canvas, CANVAS_FLOOR, type CanvasHandle, type CanvasOptions } from './canvas/Canvas';
import { observeWorld, readWorld, type WorldRead } from './canvas/worldBoxes';
import { type Fold, largeFolds, WHOLE_MOST } from './flowFolds';
import { type Box, type InkLabel, type InkPath, placeInserts, placeLabels, routeInk, RUN } from './flowInk';
import { type EdgeStyle, findStacks, FLOW_REM, type FlowLayout, layoutFlow, type Placement, placementFor, type Slot, type Stack } from './flowLayout';
import { Button, type IconName, Pill } from './ui';
import { useRootRem, useWidth } from './useWidth';

/** A pill's tone, from the caller's own table (`QUEST_TONE`, `SESSION_TONE`): the flow adds none. */
export type PillTone = NonNullable<ComponentProps<typeof Pill>['tone']>;

/** One place in the flow, worded by its caller (the flowchart design §4.1). */
export type FlowNode = {
  id: string;
  title: string;
  glyph?: IconName;
  /** The outcomes of a decision in the order its ports are drawn (FLOWCHART2a): its routes' targets start the layout in that order. */
  ports?: readonly string[];
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
  /**
   * A flag on the node (SETUPUX1n): the word of a mark the caller draws beside the step, with the foot it adds, such as where a
   * workflow's work is done and that steps after it still run. Kept apart from `lines`, which a caller fills with its own.
   */
  mark?: { word: string; foot?: string | null };
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
  /**
   * The outcome of a decision this edge is the route of (FLOWCHART2a): it leaves that outcome's port, which the caller's node draws
   * with `data-flow-port="<node>:<port>"`, and each port is a path of its own even where two go to one step.
   */
  port?: string;
  /**
   * A problem the driver's judgement found in this way on (FLOWCHART2a): said in a chip beside the edge, which never changes the edge's
   * ink, since an edge wears no status hue (D186 point 6).
   */
  problem?: string;
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

/** One press in a node's foot: the edge out of it, said in words, and the port it leaves where it is a decision's route. */
export type NodeFoot = { to: string; back: boolean; text: string; port?: string };

/** What the canvas hands the node a caller draws (FLOWCHART2a): the node whole, its stop, its feet and its description, in reading order. */
export type NodeContext = {
  node: FlowNode;
  /** Whether it holds the flow's one Tab stop. */
  stop: boolean;
  feet: NodeFoot[];
  description: string;
  onFollow: (from: string, to: string) => void;
  style?: CSSProperties;
  /** In pick mode (FLOWCHART2b), whether this node can take the way being chosen; absent outside it. */
  picking?: boolean;
};

/**
 * Pick mode (FLOWCHART2b; D223 §3): choosing where a way goes on the chart. The keys walk only the nodes that `can` take it, the first of
 * them taking the focus when the mode starts (a new `key`); Enter picks the node, Escape cancels, and a press never focuses one that cannot.
 */
export type FlowPick = { key: string; can: (id: string) => boolean; onPick: (id: string) => void; onCancel: () => void };

/** What the canvas hands the chip a caller draws on a path: what it would say by default, the edge it carries, and how many are merged. */
export type LabelContext = { id: string; style: EdgeStyle; text: string; count: number; edge: FlowEdge | undefined };

export type FlowCanvasProps = FlowProps & {
  /** Draws a node where `Flow`'s own box is not wanted: it must set `data-flow-node` on its element and take the stop's `tabIndex`. */
  renderNode: (context: NodeContext) => ReactNode;
  /** Draws the chip on a path, or none (null): the default is the path's text. */
  renderLabel?: (context: LabelContext) => ReactNode;
  /**
   * The node selected, or none (null) (FLOWCHART2a): the data wires of a flow that is given this prop are drawn while one of their ends is
   * selected, and their lanes are kept free at the right whether or not one is. A flow not given it draws no data wire.
   */
  selected?: string | null;
  /**
   * The ＋ an editor puts on a way (FLOWCHART2b), or none (null): drawn on the edge's path where it clears every node, label and other ＋
   * (`placeInserts`), and under its source in the rail. A way back and a path that merges several edges take none.
   */
  renderInsert?: (edge: FlowEdge) => ReactNode | null;
  /** Pick mode, while a way's target is being chosen on the chart. */
  pick?: FlowPick | null;
  /**
   * The canvas's viewport (CANVAS1a; D224), which a caller opts into: from 36rem of the pane the drawing stands in a region that pans and
   * zooms, and a drawing wider than the pane is drawn top to bottom and pans rather than turning to the rail. Below the floor, and where
   * none is given (`'none'`, an inline flow in a scrolling page), the flow is placed as it always was.
   */
  viewport?: 'none' | CanvasOptions;
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

/** The drawn geometry: its paths, labels and ＋s, the drawing's size, and how many times it was drawn (trap 14). */
type Ink = { paths: InkPath[]; labels: InkLabel[]; inserts: InkLabel[]; width: number; height: number; drawn: number };

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
 * - **Ports and data** (FLOWCHART2a): the canvas draws the layout, the ink and the keys and a caller draws the nodes (`renderNode`) and
 *   the chips (`renderLabel`). A `when` edge with a `port` leaves the box of that port, which the caller's node draws; a `data` edge
 *   is read by neither the layout nor the folds, and is drawn only while a flow given `selected` has an end selected.
 * - **Editing** (FLOWCHART2b): an editor's ＋ on a way (`renderInsert`), placed as a label is and under its source in the rail, and pick
 *   mode (`pick`), in which the keys walk only the nodes that can take the way being chosen. What a press does is the caller's.
 *
 * - **The viewport** (CANVAS1a; D224): a caller that opts in (`viewport`) has its drawing stand in the canvas (`canvas/Canvas.tsx`) from
 *   36rem of the pane, where a zoom and a pan transform it and lay nothing out again: its boxes are read in world units
 *   (`canvas/worldBoxes.ts`), its track is sized from the pane at 100%, and a focus walked takes `preventScroll`, the canvas revealing it.
 *   A rem is the root's as measured, so the window's zoom places a flow by what it needs.
 *
 * A molecule: the flow arrives worded and its doors go out.
 */
export function RankedFlow({
  label, nodes: given, edges: allEdges, entries: givenEntries, end, rail, sections, track: bounds, className, renderNode, renderLabel, selected,
  renderInsert, pick = null, viewport = 'none',
}: FlowCanvasProps) {
  const { t } = useTranslation();
  // A data wire is no way on: the layout, the stacks and the folds never read it (FLOWCHART2a).
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

  // The roving stop: the node the person was last on, else the current one, else the first in reading order; in pick mode, a node that
  // can take the way (FLOWCHART2b).
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
    // A key with Ctrl, Alt or the system key is the page's around the flow, such as an editor's move or undo (FLOWCHART2b).
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
  // The ＋ an editor offers on each way: none on a way back, and on the drawing none on a path that merges several edges (FLOWCHART2b).
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
    if (own.length === 0) return null;
    return (
      <div className="mt-1 flex min-w-0 flex-wrap gap-1">
        {own.map((edge) => <span key={edge.id} data-flow-insert={edge.id} className="inline-flex">{inserts.get(edge.id)}</span>)}
      </div>
    );
  };

  const [ink, setInk] = useState<Ink | null>(null);
  const insertKey = JSON.stringify(insertPaths.map((path) => [path.id, path.edges[0]]));
  useLayoutEffect(() => {
    const element = drawing.current;
    if (!element || placement === 'rail') { setInk(null); return undefined; }
    const onPaths = new Map(insertPaths.map((path) => [path.edges[0]!, path.id]));
    return observeWorld(element, () => {
      const read = readWorld(element);
      const next = read && measure(element, read, layout, placement, shownData, onPaths);
      // The canvas follows the world the ink was drawn from: its size, its opening and the anchor across a relayout.
      if (read) canvas.current?.measured(read);
      setInk((was) => {
        if (!next) return was === null ? was : null;
        if (was && same(was, next)) return was;
        return { ...next, drawn: (was?.drawn ?? 0) + 1 };
      });
    });
  }, [layout, placement, track, dataKey, insertKey, inViewport]);

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

  // In pick mode a press on a node that cannot take the way leaves the focus where it is (FLOWCHART2b).
  const onMouseDown = (event: MouseEvent<HTMLDivElement>) => {
    const holder = (event.target as HTMLElement).closest<HTMLElement>('[data-flow-node]');
    if (pick && holder && !pick.can(holder.dataset.flowNode!)) event.preventDefault();
  };

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
              </div>
            )}
        </div>
      </Canvas>
      {end}
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
  if (node.mark) sentences.push(node.mark.foot ? `${node.mark.word}. ${node.mark.foot}` : node.mark.word);
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
function padding(layout: FlowLayout, placement: Placement, dataLanes: number): CSSProperties {
  const run = `${RUN.pad}px`;
  // The data wires' lanes stand right of the nodes, kept free whether or not one is drawn (FLOWCHART2a).
  const right = dataLanes > 0 && placement === 'down' ? { paddingRight: rem(dataLanes * (FLOW_REM.lane + FLOW_REM.laneGap) + 1) } : {};
  if (layout.lanes === 0) return dataLanes > 0 && placement === 'down' ? { paddingTop: run, paddingBottom: run, ...right } : {};
  const lanes = rem(layout.lanes * (FLOW_REM.lane + FLOW_REM.laneGap));
  return placement === 'down'
    ? { paddingLeft: lanes, paddingTop: run, paddingBottom: run, ...right }
    : { paddingBottom: `calc(${lanes} + 0.5rem)`, paddingLeft: run, paddingRight: run };
}

/** How each style's path is dashed: a move and a decision's route are solid, a data wire dotted, the rest dashed. */
const DASHES: Record<EdgeStyle, string | undefined> = {
  next: undefined, when: undefined, data: '1.5 3', failed: '4 3', rejected: '4 3', refused: '4 3', conditional: '4 3', back: '4 3',
};

/** A node track's most, in rem (§3.2): a caller's node that holds more may take more (FLOWCHART1d). */
const TRACK_MOST = 16;

/** One node track's width for the drawn placements: as wide as the binding rank leaves each node, 10rem to 16rem, or the caller's. */
function trackFor(
  layout: FlowLayout, placement: Placement, width: number | undefined, least: number, mostRem: number, remPx: number,
): number | undefined {
  if (placement === 'rail' || width === undefined) return undefined;
  const px = (value: number) => value * remPx;
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

/**
 * The drawn geometry from the world the page laid out (`readWorld`): every box in world units, so the ink is the same at every scale the
 * canvas draws it at (trap 1). A label's and a ＋'s size is its layout's, which a transform leaves alone.
 */
function measure(
  element: HTMLElement, world: WorldRead, layout: FlowLayout, placement: 'across' | 'down', data: readonly { id: string; from: string; to: string }[],
  onPaths: ReadonlyMap<string, string> = new Map(),
): Omit<Ink, 'drawn'> {
  const boxes = new Map<string, Box>([...world.nodes, ...world.ways]);
  const paths = routeInk(layout, placement, boxes, { ports: world.ports, data });
  const sizes = new Map<string, { w: number; h: number }>();
  element.querySelectorAll<HTMLElement>('[data-flow-label]').forEach((label) => {
    sizes.set(label.dataset.flowLabel!, { w: label.offsetWidth, h: label.offsetHeight });
  });
  // A label clears every node and every section's divider (FLOWCHART1d), which stands in a rank gap as a label does.
  const nodes = [...layout.order.map((id) => boxes.get(id)!), ...world.sections];
  const labels = placeLabels(paths, sizes, nodes);
  // An editor's ＋ on a way clears every node, every label shown and every ＋ placed before it (FLOWCHART2b).
  const insertSizes = new Map<string, { w: number; h: number }>();
  element.querySelectorAll<HTMLElement>('[data-flow-insert]').forEach((insert) => {
    const path = onPaths.get(insert.dataset.flowInsert!);
    if (path) insertSizes.set(path, { w: insert.offsetWidth, h: insert.offsetHeight });
  });
  return { paths, labels, inserts: placeInserts(paths, insertSizes, nodes, labels), width: world.width, height: world.height };
}

const sameBoxes = (a: readonly InkLabel[], b: readonly InkLabel[]) => a.length === b.length
  && a.every((label, index) => {
    const other = b[index]!;
    return label.x === other.x && label.y === other.y && label.shown === other.shown && label.w === other.w;
  });

function same(a: Omit<Ink, 'drawn'>, b: Omit<Ink, 'drawn'>): boolean {
  return a.width === b.width && a.height === b.height
    && a.paths.length === b.paths.length && a.paths.every((path, index) => path.d === b.paths[index]!.d)
    && sameBoxes(a.labels, b.labels) && sameBoxes(a.inserts, b.inserts);
}
