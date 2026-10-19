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
import { HELP_HISTORY, HELP_PROPOSALS, HELP_WIRED, useHelpConversations } from './help';
import { historyKey, useHistoryPlan } from './history';
import { treesSyncScopeKey } from './lines';
import { usePlugins } from './plugins';
import { useRules } from './rules';
import { useSessionGroups } from './sessions';
import { useLandEntry } from './entries';
import { useStateHub, useUnderWay, useUnderWayStep, useUnderWaySubject } from './stateHub';
import { useSystemWiring } from './system';
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
    // HELPWIREDLIVE1: and the receipts *What it has* shows, which a resume or a change of mode adds while it is open.
    ['help', [[...HELP_HISTORY, ''], [...HELP_HISTORY, 'pinned'], [...HELP_PROPOSALS, 's1'], [...HELP_WIRED, 'h1']]],
    ['browser', [keys.browserSettings]],
    // FABRIC-EVENT5b: How this Daoris is wired, told as a walk starts and as its sizes land.
    ['wiring', [keys.systemWiring]],
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
    ['wiring', 'This Daoris’s wiring', () => useSystemWiring(), 'SYSTEM_WIRING'],
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

  /**
   * FABRIC-EVENT5: a change told while a view's first read is on its way must not be lost to it. The query has no data yet, so an
   * invalidation would reuse the read in flight, whose answer pre-dates the change; the hub asks once more after it.
   */
  it('a change told during a first read asks once more after it, whatever scope', async () => {
    let first: (answer: unknown) => void = () => undefined;
    let reads = 0;
    invoke.mockReset();
    invoke.mockImplementation(async (_module: string, route: string) => {
      if (route !== 'RULES') return {};
      reads += 1;
      if (reads === 1) return new Promise((resolve) => { first = resolve; });
      return { reads };
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    function View() {
      useStateHub();
      useRules();
      return null;
    }
    render(<QueryClientProvider client={client}><View /></QueryClientProvider>);
    await waitFor(() => expect(reads).toBe(1));

    act(() => changed({ scopes: ['rules'] }));
    await act(async () => first({ reads: 'stale' }));

    await waitFor(() => expect(reads).toBe(2));
    await waitFor(() => expect(client.getQueryData(keys.rules)).toEqual({ reads: 2 }));
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

  /**
   * A window opened, or a page reloaded, while a press runs (STATEHUB1c2): it heard none of the tellings, so the hub reads the
   * driver's list as it mounts, and the press shows before the driver tells anything again.
   */
  it('a hub mounted mid-press reads what is under way from the driver, with no telling heard', async () => {
    invoke.mockImplementation(async (module: string, type: string) =>
      (module === 'DAORIS.DRIVER' && type === 'UNDER_WAY' ? { seq: 7, presses: [LANDING_S1] } : {}));

    const { read } = views();

    await waitFor(() => expect(read('LAND_SESSION_TREE', 's1')).toEqual(['under way', 'under way']));
    expect(read('LAND_SESSION_TREE', 's2')).toEqual(['still']);
    expect(read('DISCARD_SESSION_TREE', 's1')).toEqual(['still']);
  });

  /** The read and the tellings race on the bus; the newest by its number is kept, whichever reached the window first. */
  it('keeps a telling newer than what it read, heard before the read answered', async () => {
    let answer: (value: unknown) => void = () => {};
    invoke.mockImplementation((_module: string, type: string) =>
      (type === 'UNDER_WAY' ? new Promise((resolve) => { answer = resolve; }) : Promise.resolve({})));
    const { read } = views();
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'UNDER_WAY', {}));

    // The landing ended after the driver read its list for this window, and its telling arrived first.
    told({ seq: 8, presses: [] });
    await act(async () => answer({ seq: 7, presses: [LANDING_S1] }));
    await settled();

    expect(read('LAND_SESSION_TREE', 's1')).toEqual(['still', 'still']);
  });

  it('reads it again each time it mounts, as a page reloaded does', async () => {
    invoke.mockImplementation(async () => ({ seq: 1, presses: [] }));
    const first = views();
    await waitFor(() => expect(invoke).toHaveBeenCalledTimes(1));
    first.unmount();

    views();

    await waitFor(() => expect(invoke).toHaveBeenCalledTimes(2));
    expect(invoke.mock.calls.map(([module, type]) => `${module} ${type}`)).toEqual(['DAORIS.DRIVER UNDER_WAY', 'DAORIS.DRIVER UNDER_WAY']);
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

  /** PRESSSTEP1: the step the driver says a press has reached, read by every view of that session, and gone with the press. */
  it('says the step a press has reached, that session’s and that route’s alone', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    function Step({ route, session }: { route: string; session: string | null }) {
      return <span data-testid={`step ${route} ${session}`}>{useUnderWayStep(route, session) ?? 'none'}</span>;
    }
    render(
      <QueryClientProvider client={client}>
        <Hub />
        <Step route="LAND_SESSION_TREE" session="s1" />
        <Step route="LAND_SESSION_TREE" session="s2" />
        <Step route="HANDOFF" session="s1" />
        <Step route="LAND_SESSION_TREE" session={null} />
      </QueryClientProvider>,
    );
    const read = (route: string, session: string | null) => screen.getByTestId(`step ${route} ${session}`).textContent;

    told({ seq: 1, presses: [LANDING_S1] });
    await settled();
    // A press that has said no step has reached none the page could word.
    expect(read('LAND_SESSION_TREE', 's1')).toBe('none');

    told({ seq: 2, presses: [{ ...LANDING_S1, step: 'gate' }] });
    await settled();
    expect(read('LAND_SESSION_TREE', 's1')).toBe('gate');
    expect(read('LAND_SESSION_TREE', 's2')).toBe('none');
    expect(read('HANDOFF', 's1')).toBe('none');
    expect(read('LAND_SESSION_TREE', null)).toBe('none');

    told({ seq: 3, presses: [{ ...LANDING_S1, step: 'landing' }] });
    await settled();
    expect(read('LAND_SESSION_TREE', 's1')).toBe('landing');

    told({ seq: 4, presses: [] });
    await settled();
    expect(read('LAND_SESSION_TREE', 's1')).toBe('none');
  });

  /**
   * ACCEPTALL1e: an entry's or an ask's Accept is under way in every window that shows that entry or ask, as another window's
   * telling carries it, and in this window from its send; one entry's press holds no other's, nor the ask's of the same id.
   */
  it('says an entry’s or an ask’s Accept is under way, from the driver’s telling and from this window’s own press', async () => {
    let answer: (value: unknown) => void = () => {};
    invoke.mockImplementation((_module: string, type: string) =>
      (type === 'LAND_ENTRY' ? new Promise((resolve) => { answer = resolve; }) : Promise.resolve({})));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    function Subject({ route, subject }: { route: string; subject: string | null }) {
      return <span data-testid={`subject ${route} ${subject}`}>{useUnderWaySubject(route, subject) ? 'under way' : 'still'}</span>;
    }
    function Press() {
      const land = useLandEntry();
      return <button type="button" onClick={() => land.mutate({ entry: 'e2', parts: 2, leaveOut: [] })}>Accept</button>;
    }
    render(
      <QueryClientProvider client={client}>
        <Hub />
        <Press />
        <Subject route="LAND_ENTRY" subject="e1" />
        <Subject route="LAND_ENTRY" subject="e2" />
        <Subject route="LAND_ASK" subject="a1" />
        <Subject route="LAND_ENTRY" subject="a1" />
        <Subject route="LAND_ENTRY" subject={null} />
      </QueryClientProvider>,
    );
    const read = (route: string, subject: string | null) => screen.getByTestId(`subject ${route} ${subject}`).textContent;

    // Another window's: the driver's telling names the entry, and the ask.
    told({ seq: 1, presses: [{ route: 'LAND_ENTRY', subject: 'e1' }, { route: 'LAND_ASK', subject: 'a1' }] });
    await settled();
    expect(read('LAND_ENTRY', 'e1')).toBe('under way');
    expect(read('LAND_ASK', 'a1')).toBe('under way');
    expect(read('LAND_ENTRY', 'a1')).toBe('still');
    expect(read('LAND_ENTRY', 'e2')).toBe('still');
    expect(read('LAND_ENTRY', null)).toBe('still');

    // This window's own, from its send, before any telling.
    await act(async () => { screen.getByRole('button', { name: 'Accept' }).click(); });
    await waitFor(() => expect(read('LAND_ENTRY', 'e2')).toBe('under way'));
    await act(async () => answer({ entry: 'e2', repository: 'r', done: true, message: 'ok', parts: [] }));
    await waitFor(() => expect(read('LAND_ENTRY', 'e2')).toBe('still'));

    told({ seq: 2, presses: [] });
    await settled();
    expect(read('LAND_ENTRY', 'e1')).toBe('still');
    expect(read('LAND_ASK', 'a1')).toBe('still');
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
