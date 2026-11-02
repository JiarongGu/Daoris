import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import type { EdgeContext } from '../design/canvas/SuppliedCanvas';
import type { MapLine } from './mapLayout';
import type { ChainEdge, Live } from './topology';

/**
 * What a person has chosen on the map: a repository, the quests one way, what the asks put on one
 * repository, a chain's hop, or a shared finding.
 */
export type MapSelection =
  | { kind: 'node'; id: string }
  | { kind: 'quests'; from: string; to: string }
  | { kind: 'asks'; to: string }
  | { kind: 'chains'; from: string; to: string }
  | { kind: 'depends'; from: string; to: string }
  | { kind: 'knowledge'; a: string; b: string };

/** Whether two choices are the same part of the map. */
export function sameSelection(a: MapSelection | null, b: MapSelection | null): boolean {
  if (a === null || b === null) return a === b;
  if (a.kind === 'node' && b.kind === 'node') return a.id === b.id;
  if (a.kind === 'quests' && b.kind === 'quests') return a.from === b.from && a.to === b.to;
  if (a.kind === 'asks' && b.kind === 'asks') return a.to === b.to;
  if (a.kind === 'chains' && b.kind === 'chains') return a.from === b.from && a.to === b.to;
  if (a.kind === 'depends' && b.kind === 'depends') return a.from === b.from && a.to === b.to;
  if (a.kind === 'knowledge' && b.kind === 'knowledge') return a.a === b.a && a.b === b.b;
  return false;
}

/** What the asks' lines and a chain's hops are drawn with, beside a quest's solid line (D41: never colour alone). */
export const ASKS_DASH = '10 4';
export const CHAIN_DASH = '1 5';
/** What a repository says it uses (MAP4e): dash and dot, in ink rather than the accent, since it is a declaration and not work. */
export const DEPENDS_DASH = '8 3 2 3';

/** Whether a chain's hop still has work in it: a step open now, or one still to be published. */
export const chainOpen = (edge: ChainEdge) =>
  edge.waiting.length > 0 || edge.steps.some((quest) => quest.status === 'Open' || quest.status === 'Taken');

/** How many sessions a repository's word speaks for, when it is more than one (MAP4d): a number in any language. */
export const howMany = (sessions: number) => (sessions > 1 ? ` · ${sessions}` : '');

/** A line's name, and whether a session is on one of its quests now, since the ring is not words (MAP4d). */
export const lineLabel = (t: (key: string) => string, label: string, live: Live) =>
  (live ? `${label}${t(`map.lineLive.${live}`)}` : label);

/**
 * A line's count, on the line, in the line's own state; ringed as a node is while a session is on one
 * of its quests (MAP4d), in the hue the rail gives that session, and dashed so it is never hue alone.
 */
export function Count({ x, y, open, count, live = null }: { x: number; y: number; open: boolean; count: number; live?: Live }) {
  return (
    <>
      {live && (
        <circle
          cx={x} cy={y} r={16} fill="none" strokeDasharray="3 3"
          className={cn('stroke-2', live === 'parked' ? 'stroke-st-open' : 'stroke-st-taken')}
        />
      )}
      <circle cx={x} cy={y} r={11} className={cn('fill-page stroke-[1.5]', open ? 'stroke-accent' : 'stroke-line-strong')} />
      <text x={x} y={y} textAnchor="middle" dominantBaseline="central" className="fill-ink font-mono text-meta tabular-nums">
        {count}
      </text>
    </>
  );
}

/** The order the kinds are laid down in: the quiet declarations under the work. */
const PAINT = ['depends', 'knowledge', 'asks', 'chains', 'quests'] as const;

/**
 * Every line of the map, drawn in the map's units: a wide invisible stroke to press (🔴 a two-pixel curve was a line nobody could
 * press, measured by the browser gate), the line itself, and its count. Each is a stop the canvas walks, named for what it holds.
 * What is in focus stands forward and the rest steps back (UX5 U47); a choice is a toggle (a second press releases it).
 */
export function MapEdges({ lines, context }: { lines: readonly MapLine[]; context: EdgeContext }) {
  const { t } = useTranslation();
  const { selected, hovered } = context;
  const chosenLine = selected !== null && lines.some((line) => line.id === selected);
  const litNode = hovered ?? (selected !== null && !chosenLine ? selected : null);
  const forward = (line: MapLine) => (hovered === null && chosenLine ? selected === line.id : litNode === null || line.from === litNode || line.to === litNode);
  const dotted = context.tier === 'dot';

  return (
    <>
      <defs>
        {/* In the map's own units, not the line's: a head that grew with the line's width made the chosen line's arrow twice the size. */}
        <marker id="map-arrow-open" viewBox="0 0 10 10" refX="8" refY="5" markerUnits="userSpaceOnUse" markerWidth="14" markerHeight="14" orient="auto-start-reverse">
          <path d="M 0 0 L 10 5 L 0 10 z" className="fill-accent" />
        </marker>
        <marker id="map-arrow-closed" viewBox="0 0 10 10" refX="8" refY="5" markerUnits="userSpaceOnUse" markerWidth="14" markerHeight="14" orient="auto-start-reverse">
          <path d="M 0 0 L 10 5 L 0 10 z" className="fill-line-strong" />
        </marker>
        {/* Open, not filled: a declaration points without carrying anything. */}
        <marker id="map-arrow-declared" viewBox="0 0 10 10" refX="8" refY="5" markerUnits="userSpaceOnUse" markerWidth="14" markerHeight="14" orient="auto-start-reverse">
          <path d="M 1 1 L 9 5 L 1 9" fill="none" className="stroke-ink-soft stroke-[1.5]" />
        </marker>
      </defs>
      {PAINT.flatMap((kind) => lines.filter((line) => line.kind === kind)).map((line) => {
        const chosen = selected === line.id;
        const back = !forward(line);
        const marker = (open: boolean) => (open ? 'url(#map-arrow-open)' : 'url(#map-arrow-closed)');
        let name: string;
        let shown: ReactElement;
        let count: ReactElement | null = null;
        switch (line.kind) {
          case 'depends':
            name = t('map.dependsLabel', { from: line.edge.from, to: line.edge.to });
            shown = <path d={line.d} fill="none" strokeDasharray={DEPENDS_DASH} markerEnd="url(#map-arrow-declared)" className={cn('shown stroke-ink-soft', chosen ? 'stroke-[3]' : 'stroke-[1.5]')} />;
            break;
          case 'knowledge':
            name = t('map.knowledgeLabel', { a: line.edge.a, b: line.edge.b, count: line.edge.groups });
            shown = <path d={line.d} fill="none" strokeDasharray="5 5" className={cn('shown stroke-ink-faint', chosen ? 'stroke-[3]' : 'stroke-[1.5]')} />;
            break;
          case 'asks': {
            const open = line.edge.open > 0;
            name = lineLabel(t, t('map.asksLabel', { to: line.edge.to, count: line.edge.quests.length }), line.edge.live);
            shown = <path d={line.d} fill="none" strokeDasharray={ASKS_DASH} markerEnd={marker(open)} className={cn('shown', open ? 'stroke-accent' : 'stroke-line-strong', chosen ? 'stroke-[3.5]' : 'stroke-[1.5]')} />;
            count = <Count x={line.mid.x} y={line.mid.y} open={open} count={line.edge.quests.length} live={line.edge.live} />;
            break;
          }
          case 'chains': {
            const open = chainOpen(line.edge);
            const steps = line.edge.steps.length + line.edge.waiting.length;
            name = lineLabel(t, t('map.chainsLabel', { from: line.edge.from, to: line.edge.to, count: steps }), line.edge.live);
            shown = <path d={line.d} fill="none" strokeDasharray={CHAIN_DASH} strokeLinecap="round" markerEnd={marker(open)} className={cn('shown', open ? 'stroke-accent' : 'stroke-line-strong', chosen ? 'stroke-[3.5]' : 'stroke-2')} />;
            count = <Count x={line.mid.x} y={line.mid.y} open={open} count={steps} live={line.edge.live} />;
            break;
          }
          case 'quests': {
            const open = line.edge.open > 0;
            name = lineLabel(t, t('map.questsLabel', { from: line.edge.from, to: line.edge.to, count: line.edge.quests.length }), line.edge.live);
            shown = (
              <path
                d={line.d} fill="none" markerEnd={marker(open)}
                style={{ strokeWidth: (chosen ? 2 : 0) + 1.5 + Math.min(line.edge.quests.length, 6) * 0.5 }}
                className={cn('shown', open ? 'stroke-accent' : 'stroke-line-strong')}
              />
            );
            count = <Count x={line.mid.x} y={line.mid.y} open={open} count={line.edge.quests.length} live={line.edge.live} />;
            break;
          }
        }
        return (
          <g
            key={line.id}
            {...context.line(line.id)}
            aria-label={name}
            className={cn('cursor-pointer outline-none transition-opacity duration-(--speed) [&:focus-visible>path.shown]:stroke-ink', back && 'opacity-20')}
            style={{ pointerEvents: 'auto' }}
          >
            <path d={line.d} fill="none" stroke="transparent" strokeWidth={18} pointerEvents="stroke" />
            {shown}
            {!dotted && count}
          </g>
        );
      })}
    </>
  );
}
