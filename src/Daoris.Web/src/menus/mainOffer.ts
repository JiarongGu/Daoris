import { createContext, useContext, useEffect, useRef, useSyncExternalStore } from 'react';
import type { ContextOffer } from './press';

// The record in the main area, as the menu bar reads it (UX7a, D152 §2). A page already offers what is done to its record
// to a right-click (CTX1, D138 §2), from the owner each page has: a session's acts through `sessionActs.ts`, a quest's
// through its page's own rule. The menu bar's record group reads the same offer, so a menu row is enabled exactly when the
// page offers the act and runs it through the same owner, and no page lists its acts a second time.

/** What a menu row reads of an offered act: enough to say whether it applies, never how it runs. */
export type OfferedAct = { id: string; label: string; disabled: boolean; copy?: string };

/** The offer as its readers see it: changed only when what it says changes, so a reader re-renders only then. */
export type OfferSnapshot = { label?: string; acts: readonly OfferedAct[] } | null;

/** Where the main area's page publishes its offer, and the menu bar reads it. */
export type OfferStore = {
  get: () => OfferSnapshot;
  subscribe: (listener: () => void) => () => void;
  /** The page offers this now; `owner` is the page, so a page that went cannot clear the one that came. */
  set: (owner: object, offer: ContextOffer | null) => void;
  clear: (owner: object) => void;
  /** Run an offered act by its page's own press, as its right-click would. False where none is offered or it is off. */
  run: (id: string) => boolean;
};

const said = (offer: ContextOffer | null): OfferSnapshot => (offer
  ? {
    ...(offer.label !== undefined ? { label: offer.label } : {}),
    acts: offer.acts.map((act) => ({
      id: act.id, label: act.label, disabled: Boolean(act.disabled), ...(act.copy !== undefined ? { copy: act.copy } : {}),
    })),
  }
  : null);

/**
 * A store for one window's main area. The snapshot changes only when what is offered changes; the presses are the page's
 * latest, since a page draws its acts anew each render with their closures over what it holds now.
 */
export function offerStore(onCopy?: (text: string) => void): OfferStore {
  let owner: object | null = null;
  let latest: ContextOffer | null = null;
  let snapshot: OfferSnapshot = null;
  let signature = 'null';
  const listeners = new Set<() => void>();

  const publish = (next: ContextOffer | null) => {
    latest = next;
    const nextSnapshot = said(next);
    const nextSignature = JSON.stringify(nextSnapshot);
    if (nextSignature === signature) return;
    signature = nextSignature;
    snapshot = nextSnapshot;
    for (const listener of listeners) listener();
  };

  return {
    get: () => snapshot,
    subscribe: (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    set: (by, offer) => {
      owner = by;
      publish(offer && offer.acts.length > 0 ? offer : null);
    },
    clear: (by) => {
      if (owner !== by) return;
      owner = null;
      publish(null);
    },
    run: (id) => {
      const act = latest?.acts.find((each) => each.id === id);
      if (!act || act.disabled) return false;
      if (act.onSelect) act.onSelect();
      else if (act.copy !== undefined) onCopy?.(act.copy);
      else return false;
      return true;
    },
  };
}

/** The window's store, where it has a menu bar to read it; elsewhere (a story, the monitor) a page publishes to no one. */
export const MainOffer = createContext<OfferStore | null>(null);

/** A page's main area publishes what it offers while it shows its record, and clears it when it goes (`ViewMain`). */
export function usePublishedOffer(offer: ContextOffer | null | undefined): void {
  const store = useContext(MainOffer);
  const owner = useRef({});
  // Every render: the presses close over what the page holds now. The store tells its readers only of a change.
  useEffect(() => { store?.set(owner.current, offer ?? null); });
  useEffect(() => {
    const by = owner.current;
    return () => store?.clear(by);
  }, [store]);
}

/** What the main area offers now, for the menu bar. */
export function useMainOffer(store: OfferStore): OfferSnapshot {
  return useSyncExternalStore(store.subscribe, store.get, store.get);
}
