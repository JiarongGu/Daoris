import { type KeyboardEvent, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { CodeDependency, CodeModule, Provenance } from '../api';
import { ago } from '../format';
import { cn } from '../lib/cn';
import { PathText } from '../ui';
import { layerModules } from './codeLayout';
import { useWidth } from './useWidth';

const BOX_W = 136;
const BOX_H = 36;
const GAP = 20;
const ROW = 92;
const MARGIN = 24;
/** Characters of an id a box can hold at the meta size before it would run past its edge. */
const FITS = 17;

/** Where a box sits: its centre across, its top edge down. */
type Place = { x: number; y: number };

/** The width the drawing is laid out at where nothing measures its card. */
const UNMEASURED = 640;

/**
 * How wide the drawing is, in units that are pixels: its card's width, narrow or wide, so the
 * columns spread with the window (UX5 U59, the owner) and a name stays the type scale's (U44). Never
 * narrower than its widest row of boxes needs; past that, the drawing shrinks as the last resort.
 */
export function codeWidth(widest: number, room: number | undefined): number {
  return Math.max(widest * (BOX_W + GAP) + MARGIN * 2, room ?? UNMEASURED);
}

/**
 * The path of one dependency arrow, from the module that depends to the module it depends on.
 *
 * @remarks
 * 🔴 An arrow that SKIPS a layer leaves by the source's side and bows out past the column, entering
 * its target from the side. Drawn straight down, `runtime → chunks` ran behind `renderer`, which
 * sat between them in the same column, and the dependency vanished from the picture (seen on the
 * window). An arrow to the next layer down stays a short curve from bottom to top. An arrow up is a
 * recorded cycle and goes from top to bottom.
 */
export function codeEdge(a: Place, b: Place): string {
  const layers = Math.round((b.y - a.y) / ROW);
  if (layers > 1) {
    const side = BOX_W / 2;
    const bow = side + 40;
    const start = { x: a.x + side, y: a.y + BOX_H / 2 };
    const end = { x: b.x + side + 2, y: b.y + BOX_H / 2 };
    return `M ${start.x} ${start.y} C ${a.x + bow} ${start.y} ${b.x + bow} ${end.y} ${end.x} ${end.y}`;
  }

  const down = b.y > a.y;
  const start = { x: a.x, y: down ? a.y + BOX_H : a.y };
  const end = { x: b.x, y: down ? b.y - 2 : b.y + BOX_H + 2 };
  const bend = (end.y - start.y) / 2;
  return `M ${start.x} ${start.y} C ${start.x} ${start.y + bend} ${end.x} ${end.y - bend} ${end.x} ${end.y}`;
}

/**
 * A repository's code, drawn (MAP3a): its modules in layers by dependency, each arrow pointing from a
 * module to what it depends on. Presentational, like the workspace map: everything arrives as props.
 *
 * @remarks
 * Every module is a button a keyboard can reach, and its name says how many things it depends on.
 * Choosing or hovering one lights its arrows and steps the rest back. An id too long for its box is
 * cut with an ellipsis on the drawing, and whole in the detail and in the name a reader hears.
 */
export function CodeMapCanvas({ repository, modules, dependencies, selected, onSelect }: {
  repository: string;
  modules: CodeModule[];
  dependencies: CodeDependency[];
  selected: string | null;
  /** A module chosen, or `null` when the choice is released (a second press, or Escape: UX5 U47). */
  onSelect: (id: string | null) => void;
}) {
  const { t } = useTranslation();
  // The module the pointer or the keyboard is on.
  const [hovered, setHovered] = useState<string | null>(null);
  // Drawn one unit a pixel, so a module's name is the type scale's (UX5 U44): it was stretched to its
  // card, 640 units drawn 775px wide at 1400.
  const box = useRef<HTMLDivElement>(null);
  const room = useWidth(box);
  const layers = layerModules(modules.map((m) => m.id), dependencies);
  const widest = Math.max(1, ...layers.map((row) => row.length));
  const width = codeWidth(widest, room);
  const height = layers.length * ROW - (ROW - BOX_H) + MARGIN * 2;

  const at = new Map<string, { x: number; y: number }>();
  layers.forEach((row, depth) => {
    const step = (width - MARGIN * 2) / row.length;
    row.forEach((id, index) => at.set(id, { x: MARGIN + step * (index + 0.5), y: MARGIN + depth * ROW }));
  });

  const focus = hovered ?? selected;
  const lit = (from: string, to: string) => focus === null || focus === from || focus === to;
  const choose = (id: string) => onSelect(id === selected ? null : id);
  const press = (id: string) => (event: KeyboardEvent) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      choose(id);
    }
  };
  const release = (event: KeyboardEvent) => {
    if (event.key === 'Escape' && selected !== null) {
      event.preventDefault();
      onSelect(null);
    }
  };

  return (
    <div ref={box}>
      <svg
        viewBox={`0 0 ${width} ${height}`}
        width={width}
        role="group"
        aria-label={t('code.canvas', { repository })}
        onKeyDown={release}
        className="mx-auto block h-auto max-w-full"
      >
        <defs>
          {/* In the drawing's own units, so a chosen arrow's head is the size of every other (MAP2) —
              and one per hue, so a lit arrow does not end in a grey head (seen on the window). The
              arrows are this drawing's content, so they wear an ink, never a container's line: that
              was 1.6:1 on the card, and stepped back, gone in dark (UX5 U48). */}
          <marker id="code-arrow" viewBox="0 0 10 10" refX="9" refY="5" markerUnits="userSpaceOnUse" markerWidth="12" markerHeight="12" orient="auto">
            <path d="M 0 0 L 10 5 L 0 10 z" className="fill-ink-faint" />
          </marker>
          <marker id="code-arrow-lit" viewBox="0 0 10 10" refX="9" refY="5" markerUnits="userSpaceOnUse" markerWidth="12" markerHeight="12" orient="auto">
            <path d="M 0 0 L 10 5 L 0 10 z" className="fill-accent" />
          </marker>
        </defs>

        {dependencies.map((edge) => {
          const a = at.get(edge.from);
          const b = at.get(edge.to);
          if (!a || !b) return null;
          const accent = focus !== null && lit(edge.from, edge.to);
          return (
            <path
              key={`${edge.from}-${edge.to}`}
              d={codeEdge(a, b)}
              fill="none"
              markerEnd={accent ? 'url(#code-arrow-lit)' : 'url(#code-arrow)'}
              className={cn(
                'stroke-ink-faint transition-opacity duration-(--speed)',
                lit(edge.from, edge.to) ? 'stroke-[1.5]' : 'opacity-20',
                accent && 'stroke-accent stroke-2',
              )}
            />
          );
        })}

        {modules.map((module) => {
          const place = at.get(module.id);
          if (!place) return null;
          const chosen = selected === module.id;
          const count = dependencies.filter((d) => d.from === module.id).length;
          const shown = module.id.length > FITS ? `${module.id.slice(0, FITS - 1)}…` : module.id;
          return (
            <g
              key={module.id}
              role="button"
              tabIndex={0}
              aria-label={t('code.moduleLabel', { id: module.id, count })}
              aria-pressed={chosen}
              onClick={() => choose(module.id)}
              onKeyDown={press(module.id)}
              onPointerEnter={() => setHovered(module.id)}
              onPointerLeave={() => setHovered(null)}
              onFocus={() => setHovered(module.id)}
              onBlur={() => setHovered(null)}
              className="cursor-pointer outline-none [&:focus-visible>rect]:stroke-ink"
            >
              <rect
                x={place.x - BOX_W / 2} y={place.y} width={BOX_W} height={BOX_H} rx={6}
                className={cn('fill-raised', chosen ? 'stroke-accent stroke-[2.5]' : 'stroke-line-strong stroke-[1.5]')}
              />
              <text
                x={place.x} y={place.y + BOX_H / 2} textAnchor="middle" dominantBaseline="central"
                className="fill-ink font-mono text-meta"
              >
                {shown}
              </text>
            </g>
          );
        })}
      </svg>
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
