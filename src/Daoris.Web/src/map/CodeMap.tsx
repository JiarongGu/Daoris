import { type ReactElement, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { CodeDependency, CodeModule, Provenance } from '../data/api';
import { ago } from '../locales/format';
import { cn } from '../lib/cn';
import { CanvasList } from '../design/canvas/CanvasList';
import { type EdgeContext, type NodeState, SuppliedCanvas } from '../design/canvas/SuppliedCanvas';
import { Button, PathText } from '../design/ui';
import { buildTree, type CodeLine, defaultOpen, layoutCode, listByFolder, openingPath } from './codeClusters';

/** Characters of an id a card can hold at the meta size before it would run past its edge. */
export const FITS = 17;
/** Zoom 10–250%: a repository of thousands is read from far, and a name is the type scale's at 100%. */
const ZOOM = { min: 0.1, max: 2.5 } as const;
/** The code map opens fitted on its closed folders, never below this. */
const OPENING = 0.8;

/** An id cut to what a card holds, with an ellipsis; whole in the detail and in the name a reader hears. */
export const cutId = (id: string) => (id.length > FITS ? `${id.slice(0, FITS - 1)}…` : id);

const isLine = (lines: readonly CodeLine[], id: string) => lines.some((line) => line.id === id);

/** The dependencies the scene draws, each a stop the canvas walks; the chosen module's stand forward and the rest step back. */
function CodeEdges({ lines, context, focus, name }: { lines: readonly CodeLine[]; context: EdgeContext; focus: string | null; name: (id: string) => string }) {
  const { t } = useTranslation();
  const chosenLine = context.selected !== null && isLine(lines, context.selected);
  const forward = (line: CodeLine) => (chosenLine && context.hovered === null
    ? context.selected === line.id
    : focus === null || line.from === focus || line.to === focus);
  const dotted = context.tier === 'dot';
  // What a line says it is, for a reader: a dependency by its kind as written, or the count between two groups.
  const lineName = (line: CodeLine) => {
    const [from, to] = [name(line.from), name(line.to)];
    if (line.aggregated) return t('code.aggregateLabel', { from, to, count: line.count });
    return line.kinds[0] ? t('code.dependsLabel', { from, to, kind: line.kinds[0] }) : t('code.dependsLabelPlain', { from, to });
  };
  return (
    <>
      <defs>
        {/* In the drawing's own units, so a head is the size of every other whatever the zoom. */}
        <marker id="code-arrow" viewBox="0 0 10 10" refX="9" refY="5" markerUnits="userSpaceOnUse" markerWidth="12" markerHeight="12" orient="auto">
          <path d="M 0 0 L 10 5 L 0 10 z" className="fill-ink-faint" />
        </marker>
        <marker id="code-arrow-lit" viewBox="0 0 10 10" refX="9" refY="5" markerUnits="userSpaceOnUse" markerWidth="12" markerHeight="12" orient="auto">
          <path d="M 0 0 L 10 5 L 0 10 z" className="fill-accent" />
        </marker>
      </defs>
      {lines.map((line) => {
        const chosen = context.selected === line.id;
        const lit = (focus !== null && forward(line)) || chosen;
        const back = !forward(line);
        return (
          <g
            key={line.id}
            {...context.line(line.id)}
            aria-label={lineName(line)}
            className={cn('cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>path.shown]:stroke-ink', back && 'opacity-20')}
            style={{ pointerEvents: 'auto' }}
          >
            <path d={line.d} fill="none" stroke="transparent" strokeWidth={18} pointerEvents="stroke" />
            <path
              d={line.d} fill="none" markerEnd={lit ? 'url(#code-arrow-lit)' : 'url(#code-arrow)'}
              strokeDasharray={line.aggregated ? '6 3' : undefined}
              style={{ strokeWidth: (chosen ? 2 : 0) + 1.5 + (line.aggregated ? Math.min(Math.log2(line.count), 4) : 0) }}
              className={cn('shown', lit ? 'stroke-accent' : 'stroke-ink-faint')}
            />
            {line.aggregated && !dotted && (
              <>
                <circle cx={line.mid.x} cy={line.mid.y} r={13} className={cn('fill-page stroke-[1.5]', lit ? 'stroke-accent' : 'stroke-line-strong')} />
                <text x={line.mid.x} y={line.mid.y} textAnchor="middle" dominantBaseline="central" className="fill-ink font-mono text-meta tabular-nums">
                  {t('code.times', { count: line.count })}
                </text>
              </>
            )}
            {/* A kind is written on the line only where the eye is: the chosen module's, or the chosen line. */}
            {!line.aggregated && !dotted && lit && line.kinds[0] && (
              <text
                x={line.mid.x + 6} y={line.mid.y} dominantBaseline="central"
                className="fill-ink-soft text-meta" style={{ paintOrder: 'stroke', stroke: 'var(--color-page)', strokeWidth: 3 }}
              >
                {line.kinds[0]}
              </text>
            )}
          </g>
        );
      })}
    </>
  );
}

/**
 * A repository's code, drawn (MAP3a; CANVAS1f; D224): folders as nested boxes, the modules in a folder in layers by dependency, each
 * arrow pointing from a module to what it depends on. Display only: a press chooses a module or a line, a press on a closed folder
 * opens it, a second press, Escape or the ground releases. Presentational: everything arrives as props.
 *
 * @remarks
 * Past 60 modules the folders open closed, and what is between two closed groups is one line counted (×n). Choosing a module,
 * from the drawing, find, the list or the detail, opens the path to it and stands its neighbourhood forward. An id too long for its
 * card is cut with an ellipsis and whole in the detail and in the name a reader hears. A view kept under another commit restores no
 * coordinates.
 */
export function CodeMapWorld({ repository, modules, dependencies, selected, onSelect, version }: {
  repository: string;
  modules: CodeModule[];
  dependencies: CodeDependency[];
  selected: string | null;
  /** A module chosen, or `null` when the choice is released (a second press, or Escape: UX5 U47). */
  onSelect: (id: string | null) => void;
  /** What the map was read at: the commit it was fed, else any. The viewer's place is kept per version. */
  version?: string;
}) {
  const { t } = useTranslation();
  const tree = useMemo(() => buildTree(modules, dependencies), [modules, dependencies]);
  // Which groups the person opened, for the tree they were opened on; a changed map starts from its own default.
  const [held, setHeld] = useState<{ tree: typeof tree; open: ReadonlySet<string> } | null>(null);
  const [listed, setListed] = useState(false);
  const [line, setLine] = useState<string | null>(null);
  const base = held?.tree === tree ? held.open : defaultOpen(tree);
  // The chosen module stays drawn: its path is open for as long as it is chosen.
  const open = useMemo(() => (selected !== null && tree.modules.has(selected) ? openingPath(tree, base, selected) : base), [tree, base, selected]);
  const scene = useMemo(
    () => layoutCode(tree, open, { cycle: (count) => t('code.cycleTitle', { count }) }),
    [tree, open, t],
  );
  const setOpen = (next: ReadonlySet<string>) => setHeld({ tree, open: next });

  const chosenCard = selected !== null && scene.world.boxes.has(selected) ? selected : null;
  const chosen = line !== null && isLine(scene.lines, line) ? line : chosenCard;
  const neighbours = useMemo(() => {
    if (chosenCard === null) return null;
    const near = new Set([chosenCard]);
    for (const each of scene.lines) {
      if (each.from === chosenCard) near.add(each.to);
      if (each.to === chosenCard) near.add(each.from);
    }
    return near;
  }, [chosenCard, scene]);

  // A folder opened by a press hands the focus to its first card, since the card that was pressed is now a frame.
  const wrapper = useRef<HTMLDivElement>(null);
  const focusing = useRef<string | null>(null);
  useEffect(() => {
    const id = focusing.current;
    if (id === null) return;
    const card = scene.standsFor.get(id);
    if (!card) return;
    focusing.current = null;
    [...(wrapper.current?.querySelectorAll<HTMLElement>('[data-flow-node]') ?? [])]
      .find((each) => each.getAttribute('data-flow-node') === card)?.focus({ preventScroll: true });
  }, [scene]);

  const choose = (id: string | null) => {
    if (id === null) { setLine(null); onSelect(null); return; }
    const group = tree.groups.get(id);
    if (group) {
      setOpen(openingPath(tree, base, id));
      focusing.current = group.members[0] ?? null;
      return;
    }
    if (isLine(scene.lines, id)) { setLine(id === line ? null : id); onSelect(null); return; }
    setLine(null);
    setOpen(openingPath(tree, base, id));
    onSelect(id);
  };

  const outgoing = useMemo(() => {
    const counts = new Map<string, number>();
    for (const each of tree.dependencies) counts.set(each.from, (counts.get(each.from) ?? 0) + 1);
    return counts;
  }, [tree]);
  const nameOf = (id: string) => {
    const group = tree.groups.get(id);
    return group ? (group.kind === 'cycle' ? t('code.cycleTitle', { count: group.modules.length }) : group.dir || group.name || repository) : id;
  };
  const nodeLabel = (id: string) => {
    const group = tree.groups.get(id);
    if (!group) return t('code.moduleLabel', { id, count: outgoing.get(id) ?? 0 });
    return group.kind === 'cycle'
      ? t('code.cycleLabel', { count: group.modules.length })
      : t('code.folderLabel', { name: nameOf(id), count: group.members.length });
  };
  const titleOf = (id: string) => nameOf(id);
  const findItems = useMemo(
    () => [...tree.modules.keys()].map((id) => ({ id, title: id, stands: scene.standsFor.get(id) ?? id })),
    [tree, scene],
  );
  const lists = useMemo(() => listByFolder(tree), [tree]);

  const renderNode = (id: string, state: NodeState): ReactElement => {
    const group = tree.groups.get(id);
    const back = neighbours !== null && !neighbours.has(id);
    const dot = state.tier === 'dot';
    if (group) {
      return (
        <span
          className={cn(
            'flex h-full w-full items-center justify-between gap-2 rounded-card border-[1.5px] border-dashed bg-raised px-2.5',
            state.selected ? 'border-accent' : 'border-line-strong', back && 'opacity-40',
          )}
        >
          {!dot && (
            <>
              <span className="min-w-0 truncate text-small font-semibold text-ink">{group.kind === 'cycle' ? t('code.cycleCard') : group.name || repository}</span>
              <span className="shrink-0 font-mono text-meta tabular-nums text-ink-soft">{t('code.times', { count: group.members.length })}</span>
            </>
          )}
        </span>
      );
    }
    return (
      <span
        className={cn(
          'flex h-full w-full items-center justify-center rounded-card border bg-raised px-2',
          state.selected ? 'border-[3px] border-accent' : 'border-[1.5px] border-line-strong', back && 'opacity-40',
        )}
      >
        {!dot && <span className="truncate font-mono text-meta text-ink">{cutId(id)}</span>}
      </span>
    );
  };

  const tools = (
    <>
      <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => { setLine(null); setOpen(new Set(tree.groups.keys())); }}>
        {t('code.showWhole')}
      </Button>
      <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => { setLine(null); setHeld(null); }}>
        {t('code.foldAgain')}
      </Button>
      <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => setListed(!listed)}>
        {t(listed ? 'canvas.asGraph' : 'canvas.asList')}
      </Button>
    </>
  );

  if (listed) {
    return (
      <div className="grid gap-2">
        <div className="flex flex-wrap items-center gap-1">{tools}</div>
        <CanvasList
          label={t('code.canvas', { repository })} groups={lists} selected={selected}
          onPick={(id) => { setOpen(openingPath(tree, base, id)); onSelect(id === selected ? null : id); }}
        />
      </div>
    );
  }

  return (
    <div ref={wrapper}>
      <SuppliedCanvas
        world={scene.world}
        label={t('code.canvas', { repository })} moves={t('code.moves')} fit={t('code.fit')} findLabel={t('code.find')}
        feature="code" storeKey={`${repository}@${version ?? 'local'}`}
        selected={chosen} onSelect={choose}
        zoom={ZOOM} opening={OPENING}
        nodeLabel={nodeLabel} titleOf={titleOf} findItems={findItems}
        frameClass={() => 'rounded-card'}
        renderNode={renderNode}
        renderEdges={(context) => <CodeEdges lines={scene.lines} context={context} focus={context.hovered ?? chosenCard} name={nameOf} />}
        tools={tools}
      />
    </div>
  );
}

/** What the chosen module is, and what it depends on and is used by — each a door to that module. */
export function CodeMapDetail({ modules, dependencies, selected, file, fed, onSelect }: {
  modules: CodeModule[];
  dependencies: CodeDependency[];
  selected: string | null;
  /** Which committed file the map was read from — said, so a person knows where to change it. */
  file: string;
  /**
   * Where the map came from when this machine has no checkout of the repository (MAP3e): a teammate's,
   * brought down by the sync. Said, because the file line alone would claim a checkout read here.
   */
  fed?: Provenance;
  onSelect: (id: string) => void;
}) {
  const { t } = useTranslation();
  const module = modules.find((m) => m.id === selected);

  if (!module) {
    return (
      <div className="grid gap-2">
        <p className="m-0 text-body text-ink-soft">{t('code.detail.hint')}</p>
        <p className="m-0 text-meta text-ink-faint">
          {fed
            ? t('code.fed', {
              file, commit: fed.shortCommit, branch: fed.branch, when: ago(fed.committedAt),
              origin: fed.origin ?? t('code.fedUnknownOrigin'),
            })
            : t('code.file', { file })}
        </p>
      </div>
    );
  }

  const out = dependencies.filter((d) => d.from === module.id);
  const into = dependencies.filter((d) => d.to === module.id);
  const list = (edges: CodeDependency[], end: 'from' | 'to') => edges.length === 0
    ? <p className="m-0 text-small text-ink-faint">{t('code.detail.none')}</p>
    : (
      <ul className="m-0 grid list-none gap-1 p-0">
        {edges.map((edge) => (
          <li key={`${edge.from}-${edge.to}`} className="flex min-w-0 items-baseline gap-2">
            <button
              type="button"
              onClick={() => onSelect(edge[end])}
              className="min-w-0 cursor-pointer truncate border-0 bg-transparent p-0 text-left font-mono text-small text-ink underline decoration-line-strong underline-offset-2 hover:text-accent hover:decoration-accent"
            >
              {edge[end]}
            </button>
            {edge.kind && <span className="shrink-0 text-meta text-ink-faint">{edge.kind}</span>}
          </li>
        ))}
      </ul>
    );

  return (
    <div className="grid gap-3">
      <header className="grid gap-1">
        <span className="font-mono text-body font-semibold text-ink wrap-anywhere">{module.id}</span>
        <PathText path={module.path} className="text-meta text-ink-faint" />
      </header>
      {module.summary && <p className="m-0 text-small text-ink-soft">{module.summary}</p>}
      <section>
        <p className="m-0 mb-1 text-meta text-ink-faint">{t('code.detail.dependsOn')}</p>
        {list(out, 'to')}
      </section>
      <section>
        <p className="m-0 mb-1 text-meta text-ink-faint">{t('code.detail.usedBy')}</p>
        {list(into, 'from')}
      </section>
    </div>
  );
}
