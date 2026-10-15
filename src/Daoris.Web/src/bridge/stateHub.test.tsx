import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
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
import { useBrowserSettings } from './browser';
import { useDriver } from './driver';
import { HELP_HISTORY, HELP_PROPOSALS, useHelpConversations } from './help';
import { historyKey, useHistoryPlan } from './history';
import { treesSyncScopeKey } from './lines';
import { usePlugins } from './plugins';
import { useRules } from './rules';
import { useSessionGroups } from './sessions';
import { useStateHub, useUnderWay } from './stateHub';
import { ALL_TOOLS, useTools } from './tools';
import { useLandSessionTree } from './trees';
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
    // The workspaces and repositories registered: the registry and the repositories the service lists (STATEHUB1h).
    ['registry', [keys.registry(null), keys.registry('aurora'), keys.repositories(null), keys.repositories('aurora')]],
    ['plugins', [keys.plugins]],
    // The agents this machine can run, what each start would run on and the accounts, all under the roster's key.
    ['harnesses', [keys.harnesses, keys.allStarts, keys.accounts]],
    // The remaining presses' families (STATEHUB1h): the tools, with what a switch of git would change; Ask Daoris's history and
    // what each conversation proposed; the browser's settings.
    ['tools', [[...ALL_TOOLS, 'list', false], [...ALL_TOOLS, 'git', 'system', '', '']]],
    ['help', [[...HELP_HISTORY, ''], [...HELP_HISTORY, 'pinned'], [...HELP_PROPOSALS, 's1']]],
    ['browser', [keys.browserSettings]],
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
    ['tools', 'the tools', () => useTools(), 'TOOLS_LIST'],
    ['help', 'Ask Daoris’s history', () => useHelpConversations(''), 'HELP_CONVERSATIONS'],
    ['browser', 'the browser’s settings', () => useBrowserSettings(), 'STATE'],
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

/**
 * What is under way (STATEHUB1c, D169 point 7): the driver tells every window, whole at each change, which presses it has not
 * answered yet, and the hub keeps it at the window's root; so *落地中…* is read by every view of that session, a view mounted
 * mid-press among them, rather than held by the one component that pressed.
 */
describe('the state hub (UNDER_WAY)', () => {
  afterEach(() => {
    eventHandlers.clear();
    invoke.mockReset();
  });

  const told = (underWay: unknown) => act(() => eventHandlers.get('DAORIS.UNDER_WAY')!(underWay));
  const LANDING_S1 = { route: 'LAND_SESSION_TREE', session: 's1' };

  /** Two views of a press, each reading whether it is under way; `shown` toggles the second, as a tab switch does. */
  function views(client = new QueryClient({ defaultOptions: { queries: { retry: false } } })) {
    function Under({ route, session }: { route: string; session: string | null }) {
      return <span data-testid={`${route} ${session}`}>{useUnderWay(route, session) ? 'under way' : 'still'}</span>;
    }
    function Window({ shown }: { shown: boolean }) {
      return (
        <QueryClientProvider client={client}>
          <Hub />
          <Under route="LAND_SESSION_TREE" session="s1" />
          {shown && <Under route="LAND_SESSION_TREE" session="s1" />}
          <Under route="LAND_SESSION_TREE" session="s2" />
          <Under route="DISCARD_SESSION_TREE" session="s1" />
          <Under route="LAND_SESSION_TREE" session={null} />
        </QueryClientProvider>
      );
    }
    const view = render(<Window shown />);
    const read = (route: string, session: string | null) => screen.getAllByTestId(`${route} ${session}`).map((each) => each.textContent);
    return { ...view, client, read, show: (shown: boolean) => view.rerender(<Window shown={shown} />) };
  }

  /** What the views read once the query's notifications, which it batches to the next turn, have reached them. */
  const settled = () => act(() => new Promise((resolve) => { setTimeout(resolve, 0); }));

  it('says a press another window made is under way, that session’s and that route’s alone, until it is told otherwise', async () => {
    const { read } = views();
    expect(read('LAND_SESSION_TREE', 's1')).toEqual(['still', 'still']);

    told({ seq: 1, presses: [LANDING_S1] });
    await settled();

    expect(read('LAND_SESSION_TREE', 's1')).toEqual(['under way', 'under way']);
    expect(read('LAND_SESSION_TREE', 's2')).toEqual(['still']);
    expect(read('DISCARD_SESSION_TREE', 's1')).toEqual(['still']);
    // A view attending no session reads nothing under way.
    expect(read('LAND_SESSION_TREE', null)).toEqual(['still']);

    told({ seq: 2, presses: [] });
    await settled();

    expect(read('LAND_SESSION_TREE', 's1')).toEqual(['still', 'still']);
  });

  it('keeps the newest telling whatever order the bus delivered them in', async () => {
    const { read } = views();

    told({ seq: 4, presses: [LANDING_S1] });
    told({ seq: 3, presses: [] });
    await settled();

    expect(read('LAND_SESSION_TREE', 's1')).toEqual(['under way', 'under way']);
  });

  it('a view mounted mid-press reads it, as one remounted by a tab switch does', async () => {
    const { read, show } = views();
    show(false);

    told({ seq: 1, presses: [LANDING_S1] });
    await settled();
    show(true);

    expect(read('LAND_SESSION_TREE', 's1')).toEqual(['under way', 'under way']);
  });

  it('a telling of any other shape changes nothing, and a press of any other shape is none', async () => {
    const { read } = views();
    told({ seq: 1, presses: [LANDING_S1] });

    told({ seq: 2, presses: 'LAND_SESSION_TREE' });
    told(undefined);
    await settled();
    expect(read('LAND_SESSION_TREE', 's1')).toEqual(['under way', 'under way']);

    told({ seq: 3, presses: [{ route: 7, session: 's1' }, null, { session: 's1' }] });
    await settled();
    expect(read('LAND_SESSION_TREE', 's1')).toEqual(['still', 'still']);
  });

  /**
   * This window's own press is under way from its send, before the driver's word arrives, and in a plain browser where none
   * does: the window's client holds it, never the component that pressed, so another view of that session reads it too.
   */
  it('this window’s own press is under way in every view of that session from its send to its answer', async () => {
    let answer: (value: unknown) => void = () => {};
    invoke.mockImplementation((_module: string, type: string) =>
      (type === 'LAND_SESSION_TREE' ? new Promise((resolve) => { answer = resolve; }) : Promise.resolve({})));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    function Press() {
      const land = useLandSessionTree();
      return <button type="button" onClick={() => land.mutate({ id: 's1', answers: null })}>Accept</button>;
    }
    render(<QueryClientProvider client={client}><Press /></QueryClientProvider>);
    const { read } = views(client);

    act(() => screen.getByRole('button', { name: 'Accept' }).click());

    await waitFor(() => expect(read('LAND_SESSION_TREE', 's1')).toEqual(['under way', 'under way']));
    expect(read('LAND_SESSION_TREE', 's2')).toEqual(['still']);

    await act(async () => answer({ session: 's1', done: true, message: 'Landed.' }));

    await waitFor(() => expect(read('LAND_SESSION_TREE', 's1')).toEqual(['still', 'still']));
  });
});
