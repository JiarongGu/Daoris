import type { TFunction } from 'i18next';
import {
  type CSSProperties, type FocusEvent, type KeyboardEvent, type ReactNode, useCallback, useId, useLayoutEffect, useMemo, useRef, useState,
} from 'react';
import { useTranslation } from 'react-i18next';
import { type Box, type InkLabel, type InkPath, placeLabels, routeInk } from '../../design/flowInk';
import type { FlowLayout } from '../../design/flowLayout';
import type { GroupFold } from '../../design/groupFolds';
import { Button } from '../../design/ui';
import { useWidth } from '../../design/useWidth';
import { cn } from '../../lib/cn';
import { isComposing } from '../../lib/composing';
import { entryText } from './Roles';
import { type DiagramShape, diagramPlacement, diagramShape, diagramTrack, layoutDiagram } from './diagram';
import { type Diagram, type DiagramEdge, type DiagramNode, litOf, placeId } from './roleStructure';
import { nameOf, positionOf, type SectionProps, stateText, StructureShell, workList } from './StructureSections';

/** The drawn geometry: its paths and labels, the drawing's size, and how many times it was drawn. */
type Ink = { paths: InkPath[]; labels: InkLabel[]; width: number; height: number; drawn: number };

/** A node as drawn: its title, its lines, and whether it is drawn dashed (a place or an agent as written, an agent not installed). */
type Drawn = { id: string; kind: DiagramNode['kind'] | 'fold'; title: string; lines: string[]; dashed: boolean; fold: GroupFold | null };

function drawnOf(t: TFunction, language: string, node: DiagramNode): Omit<Drawn, 'fold'> {
  switch (node.kind) {
    case 'place': {
      const lines: string[] = [];
      if (node.repository !== null && node.workspace !== null) lines.push(t('agents.roles.diagram.inWorkspace', { workspace: node.workspace }));
      if (!node.known) lines.push(t('agents.roles.diagram.unknownPlace'));
      else if (node.repository === null && node.workspace !== null) {
        if (node.ownRoles) lines.push(t('agents.roles.diagram.own'));
        if (node.counted > 0) lines.push(t(node.ownRoles ? 'agents.roles.diagram.countedHere' : 'agents.roles.diagram.counted', { count: node.counted }));
      }
      if (node.reasons.includes('held')) lines.push(t('agents.roles.held'));
      return { id: node.id, kind: 'place', title: node.repository ?? node.workspace ?? t('agents.roles.appointed.machine'), lines, dashed: !node.known };
    }
    case 'role': {
      const lines = [
        ...(node.takes.length > 0 ? [workList(t, language, node.takes)] : []),
        node.first
          ? `${entryText(t, node.first)}${node.more > 0 ? ` ${t('settings.ai.actors.more', { count: node.more })}` : ''}`
          : t('agents.roles.choices.none'),
      ];
      return { id: node.id, kind: 'role', title: nameOf(t, language, node.role), lines, dashed: false };
    }
    case 'appointed':
      return {
        id: node.id, kind: 'appointed', title: t(`agents.roles.diagram.${node.role}`),
        lines: node.offered ? [] : [t('agents.roles.diagram.notOfferedHere')], dashed: false,
      };
    case 'agent': {
      const said = !node.known ? t('agents.roles.diagram.agentUnknown') : node.installed === false ? t('harness.absent') : node.phrase;
      return { id: node.id, kind: 'agent', title: node.product, lines: said ? [said] : [], dashed: !node.known || node.installed === false };
    }
  }
}

/** An edge as its source's foot says it. */
function outText(t: TFunction, edge: DiagramEdge, title: (id: string) => string): string {
  if (edge.kind === 'placed') return t('agents.roles.diagram.edge.placed', { role: title(edge.to) });
  if (edge.kind === 'appointed') return t(edge.dashed ? 'agents.roles.diagram.edge.appointedDormant' : 'agents.roles.diagram.edge.appointed', { role: title(edge.to) });
  return t('agents.roles.diagram.edge.staffed', { agent: title(edge.to), position: positionOf(t, edge.position), state: stateText(t, edge.state) });
}

/** An edge as its target hears it. */
function inText(t: TFunction, edge: DiagramEdge, title: (id: string) => string): string {
  if (edge.kind === 'placed') return t('agents.roles.diagram.edge.placedIn', { place: title(edge.from) });
  if (edge.kind === 'appointed') return t(edge.dashed ? 'agents.roles.diagram.edge.appointedAtDormant' : 'agents.roles.diagram.edge.appointedAt', { place: title(edge.from) });
  return t('agents.roles.diagram.edge.staffedBy', { role: title(edge.from), position: positionOf(t, edge.position) });
}

/**
 * **The roles' diagram** (COMPANYVIEW1f; the company-view design §4): three columns, the places that set something of their own, the
 * roles, and the agents their choices name, each edge from a record: a role *placed* in its scope, a role *staffed* choice by choice
 * (numbered, dashed where the last reading says that choice cannot run), and an *appointed* role (dashed while dormant). Inheritance is
 * lit, never drawn: choosing a place lights, per work, the role the driver answered there and its staffing; choosing an agent, every
 * role leaning on it.
 *
 * @remarks
 * - **Placed by its own width** (`diagramPlacement`): across where three 12rem columns and their gaps fit, else its list, the same
 *   nodes in the same order with their edges in words; nothing is drawn smaller, and there is no pan, zoom or drag.
 * - **Legible at size**: past 24 nodes each workspace folds to one node, opened by its press (`design/groupFolds.ts`).
 * - **One list in reading order** (the flowchart design §3.7): the nodes are one `role="list"`, a roving tab stop, ↓ ↑ Home End in
 *   reading order, → along the first edge out and ← back; Enter chooses a node or opens a fold. The edges are one SVG hidden from a
 *   reader, drawn from the measured boxes, and each node's description says every edge in and out.
 *
 * A molecule: the diagram arrives built and filtered, and a choice goes out.
 */
export function RoleDiagram({ diagram, selected, onSelect }: { diagram: Diagram; selected: string | null; onSelect: (id: string) => void }) {
  const { t, i18n } = useTranslation();
  const keysId = useId();
  const marks = useId().replace(/[^\w-]/g, '');
  const pane = useRef<HTMLDivElement>(null);
  const drawing = useRef<HTMLDivElement>(null);
  const root = useRef<HTMLDivElement>(null);
  const width = useWidth(pane);

  // Folds past 24 nodes (§4): which the person opened, and *Show whole*. Page state, never remembered.
  const [opened, setOpened] = useState<ReadonlySet<string>>(() => new Set());
  const [whole, setWhole] = useState(false);
  const shapeKey = JSON.stringify([diagram.nodes.map((node) => [node.id, node.kind === 'place' ? node.group : null]), diagram.edges.map((edge) => edge.id), [...opened], whole]);
  const shape: DiagramShape = useMemo(
    () => diagramShape(diagram, whole ? new Set(diagram.nodes.flatMap((node) => (node.kind === 'place' && node.group ? [placeId({ workspace: node.group })] : []))) : opened),
    [shapeKey],
  );
  const folded = shape.nodes.some((node) => node.fold);
  const layoutKey = JSON.stringify([shape.nodes.map((node) => node.id), shape.edges.map((edge) => [edge.id, edge.from, edge.to, edge.style])]);
  const layout = useMemo(() => layoutDiagram(shape), [layoutKey]);
  const placement = diagramPlacement(layout, width);
  const track = placement === 'across' && width !== undefined ? diagramTrack(layout, width) : undefined;

  const byId = new Map(diagram.nodes.map((node) => [node.id, node]));
  const drawn = new Map<string, Drawn>(shape.nodes.map((node) => {
    if (node.fold) {
      const group = node.fold.group;
      return [node.id, {
        id: node.id, kind: 'fold', title: group, lines: [t('agents.roles.diagram.fold', { count: node.fold.members.length })], dashed: false, fold: node.fold,
      }];
    }
    return [node.id, { ...drawnOf(t, i18n.language, byId.get(node.id)!), fold: null }];
  }));
  const titleOf = (id: string) => drawn.get(shape.foldOf.get(id) ?? id)?.title ?? id;
  const edgeById = new Map(diagram.edges.map((edge) => [edge.id, edge]));

  // What is lit: the chosen node's path, a fold's members' together, each node folded away lit through its fold.
  const where = (id: string) => shape.foldOf.get(id) ?? id;
  const chosen = selected === null ? null : where(selected);
  const lit = useMemo(() => {
    const fold = shape.nodes.find((node) => node.id === chosen)?.fold;
    const sources = fold ? fold.members : chosen === null ? [] : [selected!];
    const nodes = new Set<string>();
    const edges = new Set<string>();
    for (const source of sources) {
      const each = litOf(diagram, source);
      each.nodes.forEach((id) => nodes.add(where(id)));
      each.edges.forEach((id) => edges.add(id));
    }
    return { nodes, edges };
  }, [diagram, shape, chosen, selected]);
  const litPaths = new Set(layout.paths.filter((path) => path.edges.some((id) => lit.edges.has(id))).map((path) => path.id));

  // The roving stop and the focus, kept by node id across a change of placement and a fold.
  const [stop, setStop] = useState<string | null>(null);
  const focused = useRef<string | null>(null);
  const tabStop = stop !== null && drawn.has(where(stop)) ? where(stop) : chosen !== null && drawn.has(chosen) ? chosen : layout.order[0] ?? null;
  const focusNode = useCallback((id: string) => {
    [...(root.current?.querySelectorAll<HTMLElement>('[data-flow-node]') ?? [])].find((each) => each.dataset.flowNode === id)?.focus();
  }, []);
  useLayoutEffect(() => {
    const id = focused.current;
    const active = document.activeElement;
    if (!id || (active && active !== document.body && active.isConnected)) return;
    focusNode(where(id));
  }, [placement, layout, focusNode]);

  const openFold = (fold: GroupFold) => {
    focused.current = fold.members[0]!;
    setStop(fold.members[0]!);
    setOpened((was) => new Set([...was, fold.id]));
  };
  const choose = (id: string) => {
    const fold = drawn.get(id)?.fold;
    if (fold) { openFold(fold); return; }
    onSelect(id);
  };

  const out = (id: string) => shape.edges.filter((edge) => edge.from === id && drawn.has(edge.to));
  const into = (id: string) => shape.edges.filter((edge) => edge.to === id && drawn.has(edge.from));
  const cameBy = useRef(new Map<string, string>());
  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (isComposing(event)) return;
    const target = event.target as HTMLElement;
    const holder = target.closest<HTMLElement>('[data-flow-node]');
    if (!holder) return;
    const id = holder.dataset.flowNode!;
    if (target !== holder) {
      if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); holder.focus(); }
      return;
    }
    const at = layout.order.indexOf(id);
    let next: string | undefined;
    if (event.key === 'ArrowDown') next = layout.order[at + 1];
    else if (event.key === 'ArrowUp') next = layout.order[at - 1];
    else if (event.key === 'Home') next = layout.order[0];
    else if (event.key === 'End') next = layout.order[layout.order.length - 1];
    else if (event.key === 'ArrowRight') {
      next = out(id)[0]?.to;
      if (next) cameBy.current.set(next, id);
    } else if (event.key === 'ArrowLeft') next = cameBy.current.get(id) ?? into(id)[0]?.from;
    else if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); choose(id); return; } else return;
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
  const describe = (id: string): string => {
    const node = drawn.get(id)!;
    const sentences = [...node.lines.map((line) => `${line}.`)];
    if (lit.nodes.has(id) && id !== chosen) sentences.push(t('agents.roles.diagram.litSaid'));
    const ins = into(id).map((edge) => inText(t, edgeById.get(edge.id)!, titleOf));
    const outs = out(id).map((edge) => outText(t, edgeById.get(edge.id)!, titleOf));
    if (ins.length > 0) sentences.push(t('agents.roles.diagram.from', { edges: ins.join(t('agents.roles.diagram.joinEdges')) }));
    if (outs.length > 0) sentences.push(t('agents.roles.diagram.to', { edges: outs.join(t('agents.roles.diagram.joinEdges')) }));
    return sentences.length === 0 ? '' : sentences.reduce((first, second) => t('flow.join', { first, second }));
  };

  const nodeBox = (id: string, rail: boolean, style?: CSSProperties) => {
    const node = drawn.get(id)!;
    return (
      <NodeBox
        key={id} node={node} stop={tabStop === id} chosen={chosen === id} lit={lit.nodes.has(id)} style={style}
        description={describe(id)}
        feet={rail ? out(id).map((edge) => ({ to: edge.to, text: outText(t, edgeById.get(edge.id)!, titleOf) })) : []}
        onChoose={() => choose(id)} onOpenFold={node.fold ? () => openFold(node.fold!) : undefined} onFollow={(to) => follow(id, to)}
      />
    );
  };

  // The edges, drawn from the measured boxes, again on each resize; the resizes of one frame drawn once, and the same geometry not again.
  const [ink, setInk] = useState<Ink | null>(null);
  useLayoutEffect(() => {
    const element = drawing.current;
    if (!element || placement !== 'across') { setInk(null); return undefined; }
    const draw = () => {
      const next = measure(element, layout);
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

  const labelOf = (path: FlowLayout['paths'][number]): string | null => {
    if (path.edges.length > 1) return `×${path.edges.length}`;
    const edge = edgeById.get(path.edges[0]!);
    if (edge?.kind !== 'staffed') return null;
    return /^\d+$/.test(edge.position) ? edge.position : positionOf(t, edge.position);
  };

  const columnOf = (slots: FlowLayout['ranks'][number]) => {
    const first = slots.find((slot) => slot.kind === 'node');
    const kind = first?.kind === 'node' ? drawn.get(first.id)?.kind : undefined;
    return kind === 'agent' ? 'agent' : kind === 'role' || kind === 'appointed' ? 'role' : 'place';
  };

  return (
    <div className="min-w-0">
      {(folded || whole || (placement === 'rail' && width !== undefined)) && (
        <div className="mb-2 flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
          {placement === 'rail' && width !== undefined
            ? <p data-diagram-narrow="" className="m-0 min-w-0 text-small text-ink-soft">{t('agents.roles.diagram.narrow')}</p>
            : <span />}
          <span className="flex flex-wrap gap-x-1">
            {folded && <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => setWhole(true)}>{t('flow.whole')}</Button>}
            {(whole || opened.size > 0) && (
              <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => { setWhole(false); setOpened(new Set()); }}>
                {t('flow.foldAgain')}
              </Button>
            )}
          </span>
        </div>
      )}
      <div ref={pane} data-flow-pane="" data-placement={placement} className="min-w-0 overflow-x-auto">
        <span id={keysId} hidden>{t('agents.roles.diagram.keys')}</span>
        <div ref={root} onKeyDown={onKeyDown} onFocus={onFocus} onBlur={onBlur} className="min-w-0">
          {placement === 'rail'
            ? (
              <div role="list" aria-label={t('agents.roles.diagram.label')} aria-describedby={keysId} className="m-0 grid min-w-0 gap-2 p-0">
                {layout.order.map((id) => nodeBox(id, true))}
              </div>
            )
            : (
              <div ref={drawing} data-drawn={ink?.drawn ?? 0} className="relative w-max min-w-full">
                <div aria-hidden="true" className="mb-2 flex justify-center" style={{ columnGap: '3rem' }}>
                  {layout.ranks.map((slots, index) => (
                    <p key={index} className="m-0 text-small font-semibold text-ink-faint" style={{ width: track }}>
                      {t(`agents.roles.diagram.column.${columnOf(slots)}`)}
                    </p>
                  ))}
                </div>
                <div role="list" aria-label={t('agents.roles.diagram.label')} aria-describedby={keysId} className="flex items-center justify-center" style={{ columnGap: '3rem' }}>
                  {layout.ranks.map((slots, index) => (
                    <div key={index} className="flex flex-col justify-center" style={{ rowGap: '0.75rem', width: track }}>
                      {slots.map((slot) => (slot.kind === 'node' ? nodeBox(slot.id, false) : null))}
                    </div>
                  ))}
                </div>
                <svg data-flow-ink="" aria-hidden="true" className="pointer-events-none absolute left-0 top-0 overflow-visible" width={ink?.width ?? 0} height={ink?.height ?? 0}>
                  <defs>
                    <marker id={`${marks}-arrow`} markerUnits="userSpaceOnUse" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto">
                      <path d="M0 0.5 7 4 0 7.5Z" className="fill-ink-faint" />
                    </marker>
                  </defs>
                  {ink?.paths.map((path) => (
                    <path
                      key={path.id}
                      data-flow-edge={path.id}
                      data-from={path.from}
                      data-to={path.to}
                      data-lit={litPaths.has(path.id) ? '' : undefined}
                      d={path.d}
                      fill="none"
                      className={litPaths.has(path.id) ? 'stroke-ink-soft' : 'stroke-ink-faint'}
                      strokeWidth={litPaths.has(path.id) ? 2 : 1}
                      strokeDasharray={path.style === 'next' ? undefined : '4 3'}
                      markerEnd={`url(#${marks}-arrow)`}
                    />
                  ))}
                </svg>
                {layout.paths.map((path) => {
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
      </div>
    </div>
  );
}

/** One node: its title and lines, a fold's press, and in the list its edges out as presses that move to their target. */
function NodeBox({ node, stop, chosen, lit, description, feet, onChoose, onOpenFold, onFollow, style }: {
  node: Drawn;
  stop: boolean;
  chosen: boolean;
  lit: boolean;
  description: string;
  feet: { to: string; text: string }[];
  onChoose: () => void;
  onOpenFold?: () => void;
  onFollow: (to: string) => void;
  style?: CSSProperties;
}) {
  const { t } = useTranslation();
  const titleId = useId();
  const descriptionId = useId();
  return (
    <div
      role="listitem"
      data-flow-node={node.id}
      data-kind={node.kind}
      data-lit={lit ? '' : undefined}
      tabIndex={stop ? 0 : -1}
      aria-current={chosen ? 'true' : undefined}
      aria-labelledby={titleId}
      aria-describedby={descriptionId}
      onClick={(event) => { if (!(event.target as HTMLElement).closest('button')) onChoose(); }}
      style={style}
      className={cn(
        'min-w-0 cursor-pointer rounded-card bg-raised px-3 py-2',
        node.dashed ? 'border border-dashed border-line-strong' : 'border border-line',
        chosen ? 'ring-2 ring-accent' : lit ? 'ring-2 ring-ink-soft' : '',
      )}
    >
      <span id={titleId} className={cn('block min-w-0 text-body font-semibold [overflow-wrap:anywhere]', node.dashed ? 'text-ink-soft' : 'text-ink')}>
        {node.title}
      </span>
      {node.lines.map((line, index) => (
        <span key={index} className="mt-0.5 block min-w-0 text-small text-ink-soft [overflow-wrap:anywhere]">{line}</span>
      ))}
      {onOpenFold && (
        <button
          type="button"
          tabIndex={-1}
          aria-expanded={false}
          onClick={onOpenFold}
          className="mt-1 cursor-pointer border-0 bg-transparent p-0 text-left text-meta text-ink-soft underline-offset-2 hover:text-ink hover:underline"
        >
          {t('agents.roles.diagram.unfold')}
        </button>
      )}
      {feet.length > 0 && (
        <div className="mt-1.5 grid min-w-0 gap-0.5">
          {feet.map((foot, index) => (
            <button
              key={index}
              type="button"
              tabIndex={-1}
              onClick={() => onFollow(foot.to)}
              className="cursor-pointer border-0 bg-transparent p-0 text-left text-meta text-ink-soft [overflow-wrap:anywhere] hover:text-ink"
            >
              <span aria-hidden className="mr-1">→</span>{foot.text}
            </button>
          ))}
        </div>
      )}
      <span id={descriptionId} hidden>{description}</span>
    </div>
  );
}

/** The drawn geometry from the boxes the page laid out, or null where nothing was laid out (a unit test's DOM). */
function measure(element: HTMLElement, layout: FlowLayout): Omit<Ink, 'drawn'> | null {
  const frame = element.getBoundingClientRect();
  if (frame.width === 0) return null;
  const boxes = new Map<string, Box>();
  element.querySelectorAll<HTMLElement>('[data-flow-node]').forEach((node) => {
    const rect = node.getBoundingClientRect();
    boxes.set(node.dataset.flowNode!, { x: rect.left - frame.left, y: rect.top - frame.top, w: rect.width, h: rect.height });
  });
  if ([...boxes.values()].some((box) => box.w === 0 && box.h === 0) || layout.order.some((id) => !boxes.has(id))) return null;
  const paths = routeInk(layout, 'across', boxes);
  const sizes = new Map<string, { w: number; h: number }>();
  element.querySelectorAll<HTMLElement>('[data-flow-label]').forEach((label) => sizes.set(label.dataset.flowLabel!, { w: label.offsetWidth, h: label.offsetHeight }));
  return { paths, labels: placeLabels(paths, sizes, layout.order.map((id) => boxes.get(id)!)), width: frame.width, height: frame.height };
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
 * **The diagram as a section's page** (§4, *By width*): the shared filters and the read said, as the list's sections say them, then the
 * diagram and the chosen node's detail, beside it where the main area holds both and below it otherwise.
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
            <RoleDiagram diagram={structure.diagram} selected={selected} onSelect={onSelect} />
            <aside aria-label={t('agents.roles.diagram.detail')} data-diagram-detail="" className="min-w-0 self-start">
              {selected ? detail : <p className="m-0 text-body text-ink-soft">{t('agents.roles.diagram.choose')}</p>}
            </aside>
          </div>
        ))}
    </StructureShell>
  );
}
