import type { ReactNode } from 'react';
import type { NodeState } from './SuppliedCanvas';

/**
 * **The chosen and the focused card's name beside it** (MAPUX1a; the map UX design §3): below the full tier a card draws less, but the
 * card a person has chosen or is on keeps its name, at the type scale whatever the zoom, so they always see where they are. Hidden from
 * a reader (`aria-hidden`): the card's button is already named. A molecule: the state and the words are given.
 */
export function CardLabel({ state, children }: { state: Pick<NodeState, 'tier' | 'selected' | 'focused' | 'scale'>; children: ReactNode }) {
  if (state.tier === 'full' || !(state.selected || state.focused)) return null;
  return (
    <span
      aria-hidden="true" data-card-label=""
      className="pointer-events-none absolute left-full top-1/2 z-10 ml-1 whitespace-nowrap rounded-control bg-page/90 px-1 text-small font-semibold text-ink"
      style={{ transform: `translateY(-50%) scale(${1 / state.scale})`, transformOrigin: 'left center' }}
    >
      {children}
    </span>
  );
}
