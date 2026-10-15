import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, waitFor } from '@testing-library/react';
import { type QueryKey, QueryClient, QueryClientProvider } from '@tanstack/react-query';

// What a press changed reaches every window as `STATE_CHANGED` (STATEHUB1b, D169): the hub is the one place a scope becomes
// the page's queries asked again, in every window alike.

const { eventHandlers, invoke } = vi.hoisted(() => ({
  // The push channel's seam: handlers land here by "module.type", and a test fires them as the host.
  eventHandlers: new Map<string, (payload: unknown) => void>(),
  invoke: vi.fn(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { keys } from '../data/queries';
import { useHarnesses } from './agents';
import { useDriver } from './driver';
import { historyKey, useHistoryPlan } from './history';
import { treesSyncScopeKey } from './lines';
import { usePlugins } from './plugins';
import { useRules } from './rules';
import { useSessionGroups } from './sessions';
import { useStateHub } from './stateHub';
import { useWorkPlan, workKey } from './work';

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

  /**
   * The rest of the routes (STATEHUB1d, D169 point 10): each scope they added asks again the family its hooks used to ask by
   * hand, every key under it, and nothing of another family.
   */
  const families: [scope: string, held: QueryKey[]][] = [
    ['quests', [keys.allQuests, workKey({ scope: 'quest', id: 'q1' })]],
    ['asks', [keys.allAsks]],
    // The driver's state and everything read under its key: the lines, the plans, the clean-up's list.
    ['driver', [keys.driver, keys.lines, keys.landing('s1'), keys.sweep]],
    ['rules', [keys.rules]],
    ['history', [historyKey({ scope: 'workspace', id: 'aurora' }), historyKey({ scope: 'quest', id: 'q1' })]],
    ['sync', [keys.sync('*'), keys.sync('aurora')]],
    ['registry', [keys.registry(null), keys.registry('aurora')]],
    ['plugins', [keys.plugins]],
    // The agents this machine can run, what each start would run on and the accounts, all under the roster's key.
    ['harnesses', [keys.harnesses, keys.allStarts, keys.accounts]],
  ];

  it.each(families)('%s asks its own family again and no other', (scope, family) => {
    const others = families.filter(([other]) => other !== scope).flatMap(([, held]) => held)
      .filter((key) => !family.some((own) => own.every((part, at) => key[at] === part)));
    const { invalid } = holding(...family, ...others, keys.allSessions);

    changed({ scopes: [scope] });

    for (const key of family) expect(invalid(key)).toBe(true);
    for (const key of [...others, keys.allSessions]) expect(invalid(key)).toBe(false);
  });

  /**
   * The proof the row asks of each domain: an event alone, with no press in this window (a second window's, or another
   * view's), asks the view's own data again, through the query the view already holds.
   */
  const views: [scope: string, what: string, useView: () => unknown, route: string][] = [
    ['sessions', 'the session list', () => useSessionGroups(), 'SESSION_GROUPS'],
    ['quests', 'a quest’s work', () => useWorkPlan({ scope: 'quest', id: 'q1' }), 'WORK_PLAN'],
    ['driver', 'the driver’s state', () => useDriver(), 'STATE'],
    ['rules', 'the rules', () => useRules(), 'RULES'],
    ['history', 'a workspace’s history', () => useHistoryPlan({ scope: 'workspace', id: 'aurora' }), 'HISTORY_PLAN'],
    ['plugins', 'the plugins', () => usePlugins(), 'PLUGINS'],
    ['harnesses', 'the agents', () => useHarnesses(), 'HARNESSES'],
  ];

  it.each(views)('%s: an event alone asks %s again', async (scope, _what, useView, route) => {
    invoke.mockReset();
    invoke.mockImplementation(async () => ({}));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    function View() {
      useStateHub();
      useView();
      return null;
    }
    render(<QueryClientProvider client={client}><View /></QueryClientProvider>);
    const asked = () => invoke.mock.calls.filter(([, type]) => type === route).length;
    await waitFor(() => expect(asked()).toBe(1));

    act(() => changed({ scopes: [scope] }));

    await waitFor(() => expect(asked()).toBe(2));
  });
});
