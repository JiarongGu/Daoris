import { cn } from '../lib/cn';
import { CardLabel } from '../design/canvas/CardLabel';
import type { NodeState } from '../design/canvas/SuppliedCanvas';
import { howMany } from './MapLines';
import { placeLabel, RADIUS } from './ring';
import type { MapNode } from './topology';

/** What the cards say of a session there, in the viewer's language. */
export type SessionWords = { parked: string; working: string };

/** A session on the repository, parked first: it is the one a person acts on, and a repository may hold one of each. */
const sessionOf = (node: MapNode) => (node.parked ? 'parked' : node.working ? 'working' : null);

/** The ring a session there draws, kept at every tier: a shape as well as a hue (D41). */
function SessionRing({ there, inset, radius }: { there: 'parked' | 'working'; inset: string; radius: string }) {
  return (
    <span
      aria-hidden="true" data-session={there}
      className={cn('pointer-events-none absolute border-2 border-dashed', inset, radius, there === 'parked' ? 'border-st-open' : 'border-st-taken')}
    />
  );
}

/**
 * A repository on the ring: its count inside a circle, its name outside on the side facing away from the middle, the session said in
 * words. The ring is fitted and never zoomed, so it is drawn at the full tier; a dot only where the canvas asks for one.
 */
export function RingNode({ node, x, y, state, words, back = false }: {
  node: MapNode; x: number; y: number; state: NodeState; words: SessionWords; back?: boolean;
}) {
  const there = sessionOf(node);
  const label = placeLabel(x + RADIUS, y + RADIUS);
  const [left, top] = [label.x - x, label.y - y];
  const shift = label.anchor === 'middle' ? '-50%' : label.anchor === 'end' ? '-100%' : '0';
  return (
    <span className={cn('relative block h-full w-full', back && 'opacity-40')}>
      {there && <SessionRing there={there} inset="-inset-[6px]" radius="rounded-full" />}
      <span className={cn(
        'flex h-full w-full items-center justify-center rounded-full border bg-raised font-mono text-small tabular-nums text-ink',
        state.selected ? 'border-[3px] border-accent' : 'border-[1.5px] border-line-strong',
      )}
      >
        {state.tier === 'dot' ? null : node.open}
      </span>
      {state.tier === 'full' && (
        <>
          <span className="pointer-events-none absolute whitespace-nowrap text-small font-semibold text-ink" style={{ left, top: top - 12, transform: `translateX(${shift})` }}>{node.id}</span>
          {there && (
            <span
              className={cn('pointer-events-none absolute whitespace-nowrap text-meta', there === 'parked' ? 'text-ink-open' : 'text-ink-soft')}
              style={{ left, top: top + label.next - 11, transform: `translateX(${shift})` }}
            >
              {words[there]}{howMany(node.sessions)}
            </span>
          )}
        </>
      )}
      <CardLabel state={state}>{node.id}</CardLabel>
    </span>
  );
}

/**
 * A repository in the layers, each tier its own (MAPUX1a; the map UX design §3): at the full tier its name, the session said in
 * words under it and its count at the end; at the chip tier its name on one line and its count, the words dropped; at the dot tier
 * nothing inside. The session's ring is kept at every tier, and the chosen and the focused keep their name beside the card.
 */
export function LayerCard({ node, state, words, back = false }: { node: MapNode; state: NodeState; words: SessionWords; back?: boolean }) {
  const there = sessionOf(node);
  const { tier } = state;
  return (
    <span
      className={cn(
        'relative flex h-full w-full items-center justify-between gap-2 rounded-card border bg-raised px-3.5',
        state.selected ? 'border-[3px] border-accent' : 'border-[1.5px] border-line-strong', back && 'opacity-40',
      )}
    >
      {there && <SessionRing there={there} inset="-inset-[5px]" radius="rounded-[12px]" />}
      {tier !== 'dot' && (
        <>
          <span className="min-w-0 flex-1">
            <span className="block truncate text-small font-semibold text-ink">{node.id}</span>
            {tier === 'full' && there && (
              <span className={cn('block truncate text-meta', there === 'parked' ? 'text-ink-open' : 'text-ink-soft')}>{words[there]}{howMany(node.sessions)}</span>
            )}
          </span>
          <span className={cn('font-mono text-small tabular-nums', node.open > 0 ? 'text-ink' : 'text-ink-faint')}>{node.open}</span>
        </>
      )}
      <CardLabel state={state}>{node.id}</CardLabel>
    </span>
  );
}
