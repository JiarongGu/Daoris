import { afterEach, describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import { type QueryKey, QueryClient, QueryClientProvider } from '@tanstack/react-query';

// What a press changed reaches every window as `STATE_CHANGED` (STATEHUB1b, D169): the hub is the one place a scope becomes
// the page's queries asked again, in every window alike.

const { eventHandlers } = vi.hoisted(() => ({
  // The push channel's seam: handlers land here by "module.type", and a test fires them as the host.
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke: vi.fn() }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { keys } from '../data/queries';
import { treesSyncScopeKey } from './lines';
import { useStateHub } from './stateHub';

function Hub() {
  useStateHub();
  return null;
}

/** A client holding an answer under each key, so whether each was invalidated can be read off its state. */
function holding(...held: QueryKey[]) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  for (const key of held) client.setQueryData(key, { held: true });
  render(<QueryClientProvider client={client}><Hub /></QueryClientProvider>);
  const invalid = (key: QueryKey) => client.getQueryState(key)?.isInvalidated ?? false;
  return { client, invalid };
}

const changed = (change: unknown) => eventHandlers.get('DAORIS.STATE_CHANGED')!(change);

describe('the state hub (STATE_CHANGED)', () => {
  afterEach(() => eventHandlers.clear());

  /**
   * The 采纳 a tab switch read stale (STATEHUB1): a landing moves the line every plan is judged against, so every session's plan
   * and gate is asked again, the named session's among them, with its second opinion and what of them waits.
   */
  it("a landing asks every session's plan and gate again, the one it named among them", () => {
    const { invalid } = holding(
      keys.landing('s1'), [...keys.landing('s1'), 'feature/named'], keys.landing('s2'), keys.opinion('s1'), keys.opinionWaits,
      keys.sweep);

    changed({ scopes: ['landing'], sessions: ['s1'] });

    for (const key of [keys.landing('s1'), [...keys.landing('s1'), 'feature/named'], keys.landing('s2'), keys.opinion('s1'), keys.opinionWaits]) {
      expect(invalid(key)).toBe(true);
    }
    // Only what the event names: the clean-up's list did not move.
    expect(invalid(keys.sweep)).toBe(false);
  });

  it("each scope asks its own family again, and a named session narrows the reviews and hand-offs to its own", () => {
    const { invalid } = holding(
      keys.allSessions, keys.diff('s1'), keys.diff('s2'), keys.handOff('s1'), keys.handOff('s2'), keys.sweep, treesSyncScopeKey,
      keys.lines, keys.workflow('repository', 'engine'));

    changed({ scopes: ['sessions', 'diff', 'handOff', 'sweep', 'treesSync', 'lines'], sessions: ['s1'] });

    for (const key of [keys.allSessions, keys.diff('s1'), keys.handOff('s1'), keys.sweep, treesSyncScopeKey, keys.lines,
      keys.workflow('repository', 'engine')]) {
      expect(invalid(key)).toBe(true);
    }
    expect(invalid(keys.diff('s2'))).toBe(false);
    expect(invalid(keys.handOff('s2'))).toBe(false);
  });

  it("a change about no one session asks every session's review and hand-off again", () => {
    const { invalid } = holding(keys.diff('s1'), keys.diff('s2'), keys.handOff('s1'), keys.handOff('s2'));

    changed({ scopes: ['diff', 'handOff'] });

    for (const key of [keys.diff('s1'), keys.diff('s2'), keys.handOff('s1'), keys.handOff('s2')]) expect(invalid(key)).toBe(true);
  });

  /** A wire's words are added to (D169 point 3): a scope a newer shell names that this page does not know is no change here. */
  it('a scope it does not know, an empty event or none at all changes nothing', () => {
    const { client } = holding(keys.allSessions, keys.landing('s1'));
    const invalidate = vi.spyOn(client, 'invalidateQueries');

    // An object's own names are no scopes either.
    changed({ scopes: ['quests-later', 'constructor', '__proto__'], sessions: ['s1'] });
    changed({});
    changed(undefined);
    changed({ scopes: 'landing' });

    expect(invalidate).not.toHaveBeenCalled();
  });
});
