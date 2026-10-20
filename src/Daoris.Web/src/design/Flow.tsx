import {
  type ComponentProps, type CSSProperties, type FocusEvent, type KeyboardEvent, type ReactNode, useCallback, useId, useLayoutEffect, useMemo,
  useRef, useState,
} from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { isComposing } from '../lib/composing';
import { store, stored } from '../lib/stored';
import { type Box, type InkLabel, type InkPath, placeLabels, routeInk, RUN } from './flowInk';
import { type EdgeStyle, FLOW_REM, type FlowLayout, layoutFlow, type Placement, placementFor } from './flowLayout';
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
  /** Who acts there. */
  actor?: string | null;
  /** The caller's own lines: its moves, a door. */
  lines?: ReactNode;
  /** False: not reached, drawn dashed with its words at the soft ink. Absent: the record does not say. */
  reached?: boolean;
  current?: boolean;
  /** Its record's door: Enter on the node opens it. */
  open?: () => void;
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
};

export type FlowProps = {
  /** Names the flow for a reader (*How #q1 moves*). */
  label: string;
  nodes: FlowNode[];
  edges: FlowEdge[];
  entries: string[];
  /** What stands after the flow. */
  end?: ReactNode;
  className?: string;
};

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
 *
 * A molecule: the flow arrives worded and its doors go out.
 */
export function Flow({ label, nodes, edges, entries, end, className }: FlowProps) {
  const { t } = useTranslation();
  const keysId = useId();
  const marks = useId().replace(/[^\w-]/g, '');
  const pane = useRef<HTMLDivElement>(null);
  const drawing = useRef<HTMLDivElement>(null);
  const width = useWidth(pane);
  const [asList, setAsList] = useState(() => stored(LIST_KEY) === 'list');

  // The layout reads the shape alone, so a change of what is lit lays nothing out again (§3.1 step 5): it is kept by the
  // shape's key, not by the arrays a caller makes anew on each render.
  const shapeKey = JSON.stringify([nodes.map((node) => node.id), edges.map((edge) => [edge.id, edge.from, edge.to, edge.style]), entries]);
  const layout = useMemo(() => layoutFlow({ nodes, edges, entries }), [shapeKey]);
  const fits = placementFor(layout, width);
  const placement: Placement = asList ? 'rail' : fits;

  const byId = useMemo(() => new Map(nodes.map((node) => [node.id, node])), [nodes]);
  const backs = useMemo(() => new Set(layout.paths.filter((path) => path.back).flatMap((path) => path.edges)), [layout]);

  // The roving stop: the node the person was last on, else the current one, else the first in reading order.
  const [stop, setStop] = useState<string | null>(null);
  const tabStop = stop !== null && byId.has(stop)
    ? stop
    : layout.order.find((id) => byId.get(id)?.current) ?? layout.order[0] ?? null;

  const root = useRef<HTMLDivElement>(null);
  const focused = useRef<string | null>(null);
  const cameBy = useRef(new Map<string, string>());
  const focusNode = useCallback((id: string) => {
    const element = [...(root.current?.querySelectorAll<HTMLElement>('[data-flow-node]') ?? [])].find((each) => each.dataset.flowNode === id);
    element?.focus();
  }, []);

  // The focus kept by node id (§3.7): a node the new placement or layout drew anew takes back the focus its old one had.
  useLayoutEffect(() => {
    const id = focused.current;
    const active = document.activeElement;
    if (!id || (active && active !== document.body && active.isConnected)) return;
    focusNode(id);
  }, [placement, layout, focusNode]);

  const onFocus = (event: FocusEvent<HTMLDivElement>) => {
    const holder = (event.target as HTMLElement).closest<HTMLElement>('[data-flow-node]');
    if (!holder) return;
    focused.current = holder.dataset.flowNode!;
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

  // A node's track: one width for every node, the most its widest rank leaves each, between the floor and 16rem.
  const track = useMemo(() => trackFor(layout, placement, width), [layout, placement, width]);

  // The edges, drawn from the measured boxes, again on each resize of the drawing or of a node; nothing drawn moves a box.
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
    draw();
    const observer = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(() => draw()) : null;
    observer?.observe(element);
    element.querySelectorAll('[data-flow-node]').forEach((node) => observer?.observe(node));
    return () => observer?.disconnect();
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
    description: describe(t, byId.get(id)!, edges, byId, backs),
    onFollow: follow,
  });

  const labelText = (path: FlowLayout['paths'][number]): string | null => {
    if (path.back) return path.edges.length > 1 ? `↺ ×${path.edges.length}` : '↺';
    if (path.edges.length > 1) return `×${path.edges.length}`;
    return edges.find((edge) => edge.id === path.edges[0])?.label ?? null;
  };

  return (
    <div className={cn('min-w-0', className)}>
      {/* Offered wherever the drawing fits (§3.2): where only the rail fits, the list is already what is shown. */}
      {fits !== 'rail' && (
        <div className="mb-2 flex justify-end">
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
        </div>
      )}
      <div ref={pane} data-flow-pane="" data-placement={placement} className="min-w-0 overflow-x-auto">
        <span id={keysId} hidden>{t('flow.keys')}</span>
        <div ref={root} onKeyDown={onKeyDown} onFocus={onFocus} onBlur={onBlur} className="min-w-0">
          {placement === 'rail'
            ? (
              <div role="list" aria-label={label} aria-describedby={keysId} className="m-0 grid min-w-0 gap-0 p-0">
                {layout.order.map((id, index) => (
                  <div key={id} className="grid min-w-0 grid-cols-[1rem_minmax(0,1fr)] gap-x-2">
                    <RailMark node={byId.get(id)!} last={index === layout.order.length - 1} />
                    <div className="min-w-0 pb-2"><FlowNodeBox {...nodeProps(id)} /></div>
                  </div>
                ))}
              </div>
            )
            : (
              <div
                ref={drawing}
                data-drawn={ink?.drawn ?? 0}
                className="relative w-max min-w-full"
                style={padding(layout, placement)}
              >
                <div
                  role="list"
                  aria-label={label}
                  aria-describedby={keysId}
                  className={cn('flex', placement === 'down' ? 'flex-col' : 'items-center justify-center')}
                  style={placement === 'down' ? { rowGap: rem(FLOW_REM.rankDown) } : { columnGap: rem(FLOW_REM.rankAcross) }}
                >
                  {layout.ranks.map((slots, index) => (
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
                  ))}
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
                      d={path.d}
                      fill="none"
                      className="stroke-ink-faint"
                      strokeWidth={1}
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
  const faint = node.reached === false;
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
      {node.actor && <p className="m-0 mt-1 text-small text-ink-soft [overflow-wrap:anywhere]">{node.actor}</p>}
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
          node.current ? 'border-ink-soft bg-ink-soft' : node.reached === false ? 'border-dashed border-line-strong' : 'border-ink-soft',
        )}
      />
      {!last && <span className="mt-1 w-px flex-1 bg-ink-faint" />}
    </div>
  );
}

/** A node's description (§3.7): its state, who acts, whether it was reached, and every edge in and out, in words. */
function describe(
  t: (key: string, values?: Record<string, unknown>) => string, node: FlowNode, edges: FlowEdge[], byId: Map<string, FlowNode>, backs: Set<string>,
): string {
  const title = (id: string) => byId.get(id)!.title;
  const sentences: string[] = [];
  if (node.state) sentences.push(t('flow.state', { word: node.state.word }));
  if (node.actor) sentences.push(t('flow.actor', { actor: node.actor }));
  if (node.reached === false) sentences.push(t('flow.notReached'));
  for (const edge of edges) if (edge.to === node.id && byId.has(edge.from)) sentences.push(t('flow.from', { node: title(edge.from), says: edge.says }));
  const out = edges.filter((edge) => edge.from === node.id && byId.has(edge.to));
  const next = out.filter((edge) => !backs.has(edge.id)).map((edge) => t('flow.move', { node: title(edge.to), says: edge.says }));
  if (next.length > 0) sentences.push(t('flow.next', { moves: next.reduce((first, second) => t('flow.moves', { first, second })) }));
  for (const edge of out) if (backs.has(edge.id)) sentences.push(t('flow.back', { node: title(edge.to), says: edge.says }));
  return sentences.length === 0 ? '' : sentences.reduce((first, second) => t('flow.join', { first, second }));
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

/** One node track's width for the drawn placements: as wide as the binding rank leaves each node, 10rem to 16rem. */
function trackFor(layout: FlowLayout, placement: Placement, width: number | undefined): number | undefined {
  if (placement === 'rail' || width === undefined) return undefined;
  const floor = px(FLOW_REM.node);
  const most = px(16);
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
  const nodes = layout.order.map((id) => boxes.get(id)!);
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
