import {
  type FocusEvent, type KeyboardEvent, type ReactNode, useCallback, useId, useLayoutEffect, useMemo, useRef, useState,
} from 'react';
import { useTranslation } from 'react-i18next';
import { Canvas, CANVAS_FLOOR, type CanvasHandle, type CanvasOptions } from '../../design/canvas/Canvas';
import { observeWorld, readWorld, type WorldRead } from '../../design/canvas/worldBoxes';
import { type InkLabel, type InkPath, placeLabels, routeInk } from '../../design/flowInk';
import { type EdgeStyle, type FlowLayout, layoutFlow } from '../../design/flowLayout';
import { Button } from '../../design/ui';
import { useRootRem, useWidth } from '../../design/useWidth';
import { isComposing } from '../../lib/composing';
import { type Diagram, litOf, placeId } from './roleStructure';
import { type Bands, readingOrder, structureBands } from './structureBands';
import { type Drawn, drawnOf, inText, outText } from './structureWords';
import { positionOf, type SectionProps, StructureShell } from './StructureSections';
import { StructureNode } from './StructureNode';

/** The drawn geometry: its paths and labels, the drawing's size, and how many times it is drawn. */
type Ink = { paths: InkPath[]; labels: InkLabel[]; width: number; height: number; drawn: number };

/** A band's label, a role's cell and an agent are each this wide, in rem; the gutter between the bands and the agents is four. */
const NODE_REM = 12;
const GUTTER_REM = 4;
/** The region's inner width, in rem, from which a band's label stands beside its roles (12 + 12 + 4 + 12); below it, above them, to the floor. */
const BESIDE_REM = 40;

/**
 * **The agents' structure**: one band for each place in tree
 * order, its roles stacked beside its label (above it from the canvas's floor to 40rem), and the agents once each across a gutter, joined to
 * the roles leaning on them by the *staffed* edges alone, numbered by position and dashed where the last reading says the choice cannot
 * run. *Placed* and *appointed* are what the band says, and stay in each node's description and in the list. Inheritance is lit, never
 * drawn: choosing a place lights, per work, the role the driver answered there and its staffing; an agent, every role leaning on it.
 *
 * @remarks
 * - **In the canvas** (`design/canvas/Canvas.tsx`) from 36rem of the region, panning and zooming 50-200% and never laying out again; below
 *   it, and wherever nothing measures, its list: the same nodes in the same order with their edges in words. There is no drag of a node.
 * - **One identity, many occurrences:** a role placed in two places is drawn in both; choosing either lights both and opens the one detail.
 * - **Legible at size:** past 24 nodes each workspace folds to one band, opened by its press; opening one reads nothing more.
 * - **One list in reading order**: the nodes are one `role="list"`, a roving tab stop, ↓ ↑ Home End in reading
 *   order, → along the first edge out and ← back; Enter chooses a node or opens a fold. The edges are one SVG hidden from a reader, drawn
 *   from the measured boxes, and each node's description says every edge in and out.
 *
 * A molecule: the diagram arrives built and filtered, and a choice goes out.
 */
export function StructureCanvas({ diagram, selected, onSelect }: { diagram: Diagram; selected: string | null; onSelect: (id: string) => void }) {
  const { t, i18n } = useTranslation();
  const keysId = useId();
  const marks = useId().replace(/[^\w-]/g, '');
  const pane = useRef<HTMLDivElement>(null);
  const drawing = useRef<HTMLDivElement>(null);
  const root = useRef<HTMLDivElement>(null);
  const canvas = useRef<CanvasHandle>(null);
  const width = useWidth(pane);
  const remPx = useRootRem();
  const inViewport = width !== undefined && width >= CANVAS_FLOOR * remPx - 0.5;
  const beside = width !== undefined && width >= BESIDE_REM * remPx - 0.5;

  // Folds past 24 nodes (§3.2): which the person opened, and *Show whole*. Page state, never remembered.
  const [opened, setOpened] = useState<ReadonlySet<string>>(() => new Set());
  const [whole, setWhole] = useState(false);
  const shapeKey = JSON.stringify([diagram.nodes.map((node) => [node.id, node.kind === 'place' ? node.group : null]), diagram.edges.map((edge) => edge.id), [...opened], whole]);
  const drawn: Bands = useMemo(
    () => structureBands(diagram, whole ? new Set(diagram.nodes.flatMap((node) => (node.kind === 'place' && node.group ? [placeId({ workspace: node.group })] : []))) : opened),
    [shapeKey],
  );
  const folded = drawn.bands.some((band) => band.fold);
  const order = useMemo(() => readingOrder(drawn), [drawn]);

  // What each key is: a band's label (or a fold), a role or an appointment in a band, an agent; and the one identity it is of.
  const byNode = new Map(diagram.nodes.map((node) => [node.id, node]));
  const nodes = new Map<string, Drawn>();
  const identity = new Map<string, string>();
  const indent = new Map<string, number>();
  for (const band of drawn.bands) {
    if (band.fold) {
      nodes.set(band.key, { id: band.key, kind: 'fold', title: band.fold.group, lines: [t('agents.roles.diagram.fold', { count: band.fold.members.length })], dashed: false, fold: band.fold });
    } else nodes.set(band.key, { ...drawnOf(t, i18n.language, byNode.get(band.place)!), id: band.key, fold: null });
    identity.set(band.key, band.key);
    indent.set(band.key, band.depth === 0 ? 0 : band.depth - 1);
    for (const cell of band.cells) {
      const base = drawnOf(t, i18n.language, byNode.get(cell.node)!);
      nodes.set(cell.key, { ...base, id: cell.key, dashed: base.dashed || cell.dashed, fold: null });
      identity.set(cell.key, cell.node);
    }
  }
  for (const agent of drawn.agents) {
    nodes.set(agent, { ...drawnOf(t, i18n.language, byNode.get(agent)!), id: agent, fold: null });
    identity.set(agent, agent);
  }
  const where = (id: string) => drawn.foldOf.get(id) ?? id;
  const titleOf = (id: string) => nodes.get(where(id))?.title ?? nodes.get(id)?.title ?? id;
  const edgeById = new Map(diagram.edges.map((edge) => [edge.id, edge]));
  const bandOf = (key: string) => drawn.bands.find((band) => band.key === key || band.cells.some((cell) => cell.key === key));

  // The edges out of and into a key, as the diagram has them (placed and appointed too, which are said, not drawn), each with the key it reaches.
  const outOf = (key: string) => {
    const id = identity.get(key);
    if (id === undefined) return [];
    if (key === id && byNode.get(id)?.kind === 'agent') return [];
    const band = drawn.bands.find((each) => each.key === key);
    if (band) {
      if (band.fold) return drawn.edges.filter((edge) => edge.from === key).map((edge) => ({ edge: edgeById.get(edge.edge)!, to: edge.to }));
      return diagram.edges.filter((edge) => edge.kind !== 'staffed' && edge.from === band.place)
        .flatMap((edge) => { const cell = band.cells.find((each) => each.node === edge.to); return cell ? [{ edge, to: cell.key }] : []; });
    }
    return drawn.edges.filter((edge) => edge.from === key).map((edge) => ({ edge: edgeById.get(edge.edge)!, to: edge.to }));
  };
  const intoKey = (key: string) => {
    if (nodes.get(key)?.kind === 'agent') {
      const edge = drawn.edges.find((each) => each.to === key);
      return edge ? [{ edge: edgeById.get(edge.edge)!, from: edge.from }] : [];
    }
    const band = bandOf(key);
    if (!band || band.key === key) return [];
    const edge = diagram.edges.find((each) => each.kind !== 'staffed' && each.from === band.place && each.to === identity.get(key));
    return edge ? [{ edge, from: band.key }] : [];
  };

  // What is lit: the chosen identity's path, a fold's members' together, each place folded away lit through its fold.
  const chosen = selected === null ? null : where(selected);
  const lit = useMemo(() => {
    const fold = drawn.bands.find((band) => band.key === chosen)?.fold;
    const sources = fold ? fold.members : chosen === null ? [] : [selected!];
    const lightNodes = new Set<string>();
    const lightEdges = new Set<string>();
    for (const source of sources) {
      const each = litOf(diagram, source);
      each.nodes.forEach((id) => lightNodes.add(where(id)));
      each.edges.forEach((id) => lightEdges.add(id));
    }
    return { nodes: lightNodes, edges: lightEdges };
  }, [diagram, drawn, chosen, selected]);

  // The roving stop and the focus, kept by key across a change of placement and a fold.
  const [stop, setStop] = useState<string | null>(null);
  const focused = useRef<string | null>(null);
  const tabStop = stop !== null && nodes.has(stop) ? stop : chosen !== null ? order.find((key) => identity.get(key) === chosen) ?? order[0] ?? null : order[0] ?? null;
  const focusNode = useCallback((key: string) => {
    const found = [...(root.current?.querySelectorAll<HTMLElement>('[data-flow-node]') ?? [])].find((each) => each.dataset.flowNode === key);
    if (found?.closest('[data-canvas-world]')) found.focus({ preventScroll: true });
    else found?.focus();
  }, []);
  useLayoutEffect(() => {
    const key = focused.current;
    const active = document.activeElement;
    if (!key || (active && active !== document.body && active.isConnected)) return;
    focusNode(nodes.has(key) ? key : where(key));
  }, [inViewport, drawn, focusNode]);

  const openFold = (key: string) => {
    const fold = nodes.get(key)?.fold;
    if (!fold) return;
    focused.current = fold.members[0]!;
    setStop(fold.members[0]!);
    setOpened((was) => new Set([...was, fold.id]));
  };
  const choose = (key: string) => {
    if (nodes.get(key)?.fold) { openFold(key); return; }
    onSelect(identity.get(key)!);
  };

  const cameBy = useRef(new Map<string, string>());
  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (isComposing(event) || event.ctrlKey || event.altKey || event.metaKey) return;
    const target = event.target as HTMLElement;
    const holder = target.closest<HTMLElement>('[data-flow-node]');
    if (!holder) return;
    const key = holder.dataset.flowNode!;
    if (target !== holder) {
      if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); holder.focus(); }
      return;
    }
    const at = order.indexOf(key);
    let next: string | undefined;
    if (event.key === 'ArrowDown') next = order[at + 1];
    else if (event.key === 'ArrowUp') next = order[at - 1];
    else if (event.key === 'Home') next = order[0];
    else if (event.key === 'End') next = order[order.length - 1];
    else if (event.key === 'ArrowRight') {
      next = outOf(key)[0]?.to;
      if (next) cameBy.current.set(next, key);
    } else if (event.key === 'ArrowLeft') next = cameBy.current.get(key) ?? intoKey(key)[0]?.from;
    else if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); choose(key); return; } else return;
    event.preventDefault();
    if (next) focusNode(next);
  };
  const onFocus = (event: FocusEvent<HTMLDivElement>) => {
    const holder = (event.target as HTMLElement).closest<HTMLElement>('[data-flow-node]');
    if (!holder) return;
    focused.current = holder.dataset.flowNode!;
    if (holder === event.target) setStop(holder.dataset.flowNode!);
  };
  const onBlur = (event: FocusEvent<HTMLDivElement>) => {
    const next = event.relatedTarget as Node | null;
    if (next && event.currentTarget.contains(next)) return;
    const left = event.target as HTMLElement;
    window.setTimeout(() => { if (left.isConnected && !root.current?.contains(document.activeElement)) focused.current = null; }, 0);
  };
  const follow = (from: string, to: string) => { cameBy.current.set(to, from); focusNode(to); };

  // A node's words to a reader: its lines, whether it is lit, and every edge in and out.
  const describe = (key: string): string => {
    const node = nodes.get(key)!;
    const sentences = [...node.lines.map((line) => `${line}.`)];
    if (lit.nodes.has(identity.get(key)!) && identity.get(key) !== chosen) sentences.push(t('agents.roles.diagram.litSaid'));
    // Every edge of the node in words, whichever occurrence it is: the keys walk the occurrences, the reader hears the node.
    const self = identity.get(key)!;
    const ins = diagram.edges.filter((edge) => where(edge.to) === self && where(edge.from) !== self).map((edge) => inText(t, edge, titleOf));
    const outs = diagram.edges.filter((edge) => where(edge.from) === self && where(edge.to) !== self).map((edge) => outText(t, edge, titleOf));
    if (ins.length > 0) sentences.push(t('agents.roles.diagram.from', { edges: ins.join(t('agents.roles.diagram.joinEdges')) }));
    if (outs.length > 0) sentences.push(t('agents.roles.diagram.to', { edges: outs.join(t('agents.roles.diagram.joinEdges')) }));
    return sentences.length === 0 ? '' : sentences.reduce((first, second) => t('flow.join', { first, second }));
  };

  const nodeBox = (key: string, rail: boolean) => {
    const node = nodes.get(key)!;
    return (
      <StructureNode
        key={key} node={node} identity={identity.get(key)!} stop={tabStop === key} chosen={chosen === identity.get(key)} lit={lit.nodes.has(identity.get(key)!)}
        description={describe(key)} indent={indent.get(key) ?? 0}
        feet={rail ? outOf(key).map(({ edge, to }) => ({ to, text: outText(t, edge, titleOf) })) : []}
        onChoose={() => choose(key)} onOpenFold={node.fold ? () => openFold(key) : undefined} onFollow={(to) => follow(key, to)}
      />
    );
  };

  // The staffed edges as a two-rank flow across, to be routed and labelled by the flowchart's own ink.
  const flowKey = JSON.stringify(drawn.edges.map((edge) => [edge.id, edge.from, edge.to, edge.dashed]));
  const flow: FlowLayout = useMemo(() => {
    const sources = [...new Set(drawn.edges.map((edge) => edge.from))];
    const ids = [...sources, ...drawn.agents];
    return layoutFlow({
      nodes: ids.map((id) => ({ id })),
      edges: drawn.edges.map((edge) => ({ id: edge.id, from: edge.from, to: edge.to, style: (edge.dashed ? 'conditional' : 'next') as EdgeStyle })),
      entries: sources,
    });
  }, [flowKey]);
  const edgeOfPath = (pathEdges: string[]) => (pathEdges.length === 1 ? drawn.edges.find((edge) => edge.id === pathEdges[0]) : undefined);

  const [ink, setInk] = useState<Ink | null>(null);
  useLayoutEffect(() => {
    const element = drawing.current;
    if (!element || !inViewport) { setInk(null); return undefined; }
    return observeWorld(element, () => {
      const read = readWorld(element);
      const next = read && measure(element, read, flow);
      if (read) canvas.current?.measured(read);
      setInk((was) => {
        if (!next) return was === null ? was : null;
        if (was && same(was, next)) return was;
        return { ...next, drawn: (was?.drawn ?? 0) + 1 };
      });
    });
  }, [flow, inViewport, beside, drawn]);

  const labelOf = (path: FlowLayout['paths'][number]): string | null => {
    const edge = edgeOfPath(path.edges);
    if (path.edges.length > 1) return `×${path.edges.length}`;
    if (!edge) return null;
    if (edge.count > 1) return `×${edge.count}`;
    return /^\d+$/.test(edge.position) ? edge.position : positionOf(t, edge.position);
  };

  const options: CanvasOptions = { mode: 'display', zoom: { min: 0.5, max: 2 }, fit: t('agents.roles.diagram.fit'), moves: t('agents.roles.diagram.moves') };
  const tools = (
    <>
      {folded && <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => setWhole(true)}>{t('flow.whole')}</Button>}
      {(whole || opened.size > 0) && (
        <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => { setWhole(false); setOpened(new Set()); }}>
          {t('flow.foldAgain')}
        </Button>
      )}
    </>
  );

  return (
    <div className="min-w-0">
      <Canvas
        ref={canvas}
        active={inViewport}
        options={options}
        label={t('agents.roles.diagram.label')}
        width={width}
        rem={remPx}
        pane={pane}
        paneProps={{ 'data-flow-pane': '', 'data-placement': inViewport ? 'across' : 'rail' }}
        tools={tools}
        showTools={folded || whole || opened.size > 0}
        entry={order[0] ?? null}
        selected={chosen === null ? null : order.find((key) => identity.get(key) === chosen) ?? null}
      >
        <span id={keysId} hidden>{t('agents.roles.diagram.keys')}</span>
        <div ref={root} onKeyDown={onKeyDown} onFocus={onFocus} onBlur={onBlur} className="min-w-0">
          {!inViewport
            ? (
              <>
                {width !== undefined && <p data-diagram-narrow="" className="m-0 mb-2 min-w-0 text-small text-ink-soft">{t('agents.roles.diagram.narrow')}</p>}
                <div role="list" aria-label={t('agents.roles.diagram.label')} aria-describedby={keysId} className="m-0 grid min-w-0 gap-2 p-0">
                  {order.map((key) => nodeBox(key, true))}
                </div>
              </>
            )
            : (
              <div ref={drawing} data-drawn={ink?.drawn ?? 0} className="relative w-max min-w-full">
                <div role="list" aria-label={t('agents.roles.diagram.label')} aria-describedby={keysId} className="flex items-stretch" style={{ columnGap: `${GUTTER_REM}rem` }}>
                  <div className="grid content-start gap-3" style={{ width: `${beside ? 2 * NODE_REM : NODE_REM}rem` }}>
                    {drawn.bands.map((band) => (
                      <div
                        key={band.key}
                        data-band={band.key}
                        className="grid items-start gap-2 border-t border-line pt-2 first:border-t-0 first:pt-0"
                        style={{ gridTemplateColumns: beside ? `${NODE_REM}rem minmax(0, 1fr)` : 'minmax(0, 1fr)' }}
                      >
                        {nodeBox(band.key, false)}
                        <div className="grid min-w-0 content-start gap-2">{band.cells.map((cell) => nodeBox(cell.key, false))}</div>
                      </div>
                    ))}
                  </div>
                  <div className="flex flex-col justify-center gap-3" style={{ width: `${NODE_REM}rem` }}>
                    <p aria-hidden="true" className="m-0 text-small font-semibold text-ink-faint">{t('agents.roles.diagram.column.agent')}</p>
                    {drawn.agents.map((agent) => nodeBox(agent, false))}
                  </div>
                </div>
                <svg data-flow-ink="" aria-hidden="true" className="pointer-events-none absolute left-0 top-0 overflow-visible" width={ink?.width ?? 0} height={ink?.height ?? 0}>
                  <defs>
                    <marker id={`${marks}-arrow`} markerUnits="userSpaceOnUse" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto">
                      <path d="M0 0.5 7 4 0 7.5Z" className="fill-ink-faint" />
                    </marker>
                  </defs>
                  {ink?.paths.map((path) => {
                    const lights = (flow.paths.find((each) => each.id === path.id)?.edges ?? []).some((id) => lit.edges.has(drawn.edges.find((edge) => edge.id === id)?.edge ?? ''));
                    return (
                      <path
                        key={path.id}
                        data-flow-edge={path.id}
                        data-from={path.from}
                        data-to={path.to}
                        data-lit={lights ? '' : undefined}
                        d={path.d}
                        fill="none"
                        className={lights ? 'stroke-ink-soft' : 'stroke-ink-faint'}
                        strokeWidth={lights ? 2 : 1}
                        strokeDasharray={path.style === 'next' ? undefined : '4 3'}
                        markerEnd={`url(#${marks}-arrow)`}
                      />
                    );
                  })}
                </svg>
                {flow.paths.map((path) => {
                  const text = labelOf(path);
                  if (!text) return null;
                  const placed = ink?.labels.find((each) => each.id === path.id);
                  return (
                    <span
                      key={path.id}
                      aria-hidden
                      data-flow-label={path.id}
                      data-shown={placed?.shown ? '' : undefined}
                      className="pointer-events-none absolute whitespace-nowrap rounded-control bg-page px-1 text-meta leading-tight text-ink-soft"
                      style={{ left: placed ? placed.x - placed.w / 2 : 0, top: placed ? placed.y - placed.h / 2 : 0, visibility: placed?.shown ? 'visible' : 'hidden' }}
                    >
                      {text}
                    </span>
                  );
                })}
              </div>
            )}
        </div>
      </Canvas>
    </div>
  );
}

/** The drawn geometry from the world the page laid out, in world units, or null where nothing is laid out (a unit test's DOM). */
function measure(element: HTMLElement, world: WorldRead, layout: FlowLayout): Omit<Ink, 'drawn'> | null {
  const boxes = world.nodes;
  if (layout.order.some((id) => !boxes.has(id))) return null;
  const paths = routeInk(layout, 'across', boxes);
  const sizes = new Map<string, { w: number; h: number }>();
  element.querySelectorAll<HTMLElement>('[data-flow-label]').forEach((label) => sizes.set(label.dataset.flowLabel!, { w: label.offsetWidth, h: label.offsetHeight }));
  return { paths, labels: placeLabels(paths, sizes, [...boxes.values()]), width: world.width, height: world.height };
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

/**
 * **The structure as a section's page** (§3.2, *By width*): the shared filters and the read said, as the list's sections say them, then the
 * structure and the chosen node's detail, beside it where the main area holds both and below it otherwise.
 */
export function DiagramSection(props: Pick<SectionProps, 'structure' | 'read' | 'filters' | 'onFilters'> & {
  selected: string | null;
  onSelect: (id: string) => void;
  /** The chosen node's molecule, its row's in the list. */
  detail: ReactNode;
}) {
  const { t } = useTranslation();
  const { structure, selected, onSelect, detail } = props;
  return (
    <StructureShell {...props}>
      {structure && (structure.diagram.nodes.length === 0
        ? <p className="m-0 text-body text-ink-soft">{t('agents.roles.nothing')}</p>
        : (
          <div className="grid min-w-0 gap-6 @min-[68rem]/main:grid-cols-[minmax(0,1fr)_20rem]">
            <StructureCanvas diagram={structure.diagram} selected={selected} onSelect={onSelect} />
            <aside aria-label={t('agents.roles.diagram.detail')} data-diagram-detail="" className="min-w-0 self-start">
              {selected ? detail : <p className="m-0 text-body text-ink-soft">{t('agents.roles.diagram.choose')}</p>}
            </aside>
          </div>
        ))}
    </StructureShell>
  );
}
